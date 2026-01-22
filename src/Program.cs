using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace PowerMeterSample472
{
    class Program
    {
        static void Main(string[] args)
        {
            // 通信サービスを using ブロックで管理
            using (var service = new PowerMeterService())
            {
                // タイマー（10秒デモ用。実運用では 24時間 = 86_400_000 ms）
                var timer = new System.Timers.Timer(10_000);
                timer.Elapsed += async (s, e) =>
                {
                    try
                    {
                        double energy = await service.ReadIntegratedEnergyAsync();
                        Console.WriteLine("[TIMER] 積算電力量 = {energy:F2} kWh");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Timer error: " + ex.Message);
                    }
                };
                timer.Start();

                Console.WriteLine("Enterキー：瞬時電力取得 / q：終了");

                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q')
                        break;

                    try
                    {
                        // await は使わず同期呼び出し
                        double power = service.ReadInstantPowerAsync().GetAwaiter().GetResult();
                        Console.WriteLine("[MANUAL] 瞬時電力 = {power:F2} kW");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error reading instant power: " + ex.Message);
                    }
                }

                timer.Stop();
            }
        }
    }

    /// <summary>
    /// 通信サービス（要求キュー型）
    /// </summary>
    class PowerMeterService : IDisposable
    {
        private const bool USE_REAL_SERIAL = false; // 実機接続時は true
        private readonly BlockingCollection<Func<object>> _queue = new BlockingCollection<Func<object>>();
        private readonly Thread _worker;
        private SerialPort _port;

        public PowerMeterService()
        {
            if (USE_REAL_SERIAL)
            {
                _port = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One);
                _port.Open();
            }

            _worker = new Thread(WorkLoop) { IsBackground = true };
            _worker.Start();
        }

        private void WorkLoop()
        {
            foreach (var job in _queue.GetConsumingEnumerable())
            {
                job();
            }
        }

        private Task<T> Enqueue<T>(Func<T> action)
        {
            var tcs = new TaskCompletionSource<T>();

            _queue.Add(() =>
            {
                try
                {
                    T result = action();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                return null;
            });

            return tcs.Task;
        }

        // ===== 公開API =====

        public Task<double> ReadInstantPowerAsync()
        {
            return Enqueue(() =>
            {
                SendInstantPowerCommand();
                byte[] res = Receive();
                return ParseInstantPower(res);
            });
        }

        public Task<double> ReadIntegratedEnergyAsync()
        {
            return Enqueue(() =>
            {
                SendIntegratedEnergyCommand();
                byte[] res = Receive();
                return ParseIntegratedEnergy(res);
            });
        }

        // ===== 通信処理（実機では差し替え） =====

        private void SendInstantPowerCommand()
        {
            if (USE_REAL_SERIAL)
            {
                byte[] cmd = { 0x01, 0x03, 0x00, 0x10 };
                _port.Write(cmd, 0, cmd.Length);
            }
        }

        private void SendIntegratedEnergyCommand()
        {
            if (USE_REAL_SERIAL)
            {
                byte[] cmd = { 0x01, 0x03, 0x00, 0x20 };
                _port.Write(cmd, 0, cmd.Length);
            }
        }

        private byte[] Receive()
        {
            if (USE_REAL_SERIAL)
            {
                byte[] buf = new byte[256];
                int len = _port.Read(buf, 0, buf.Length);
                Array.Resize(ref buf, len);
                return buf;
            }
            else
            {
                // 疑似応答（デモ用）
                Thread.Sleep(300);
                return BitConverter.GetBytes(new Random().Next(100, 5000));
            }
        }

        private double ParseInstantPower(byte[] res)
        {
            return BitConverter.ToInt32(res, 0) / 100.0;
        }

        private double ParseIntegratedEnergy(byte[] res)
        {
            return BitConverter.ToInt32(res, 0) / 10.0;
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _port?.Close();
        }
    }
}
