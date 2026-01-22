using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        var serialPort = new SerialPort
        {
            PortName = "COM3",
            BaudRate = 115200,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One
        };

        serialPort.Open();

        serialPort.Write("SKSREG S1\r");
        Console.WriteLine("SK");
        List<byte> buff = new List<byte> ();
        byte[] rb = new byte[1];
        bool flag = false;
        while(!flag)
        {
            int rlen = serialPort.Read(rb, 0, 1);
            if (rlen != 1)
            {
                Thread.Sleep(10000);
                continue;
            }
            buff.Add(rb[0]);
            if(rb[0]=='\n') flag = false;
            Console.Write((char)rb[0]);
            Console.Write("");
        }
        
        serialPort.Close();
    }
}