using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SerialSender
{
    class Program
    {


        static int bufferSize = 2048;
        static double delayms = 0.001;

        static void Main()
        {

            Thread t = new Thread(UpdateSettings);
            t.Start();

            bool writeToSerial = true;
            bool writeToFile = false;
            var serialPortName = "COM6";
            var serial = new SerialPort(serialPortName, 9600)
            {
                //
                //ReadBufferSize = 2000,
                //DiscardNull = true,Handshake = Handshake.None,
                //DtrEnable = false,
                Handshake = Handshake.None,
                //RtsEnable = false,
                //DiscardNull = false,
                ReadBufferSize = 5000,
                WriteBufferSize = 5000,
                //Handshake = Handshake.XOnXOff,
                //Parity = Parity.Even,
                DataBits = 8,
                StopBits = StopBits.One,
                ReceivedBytesThreshold = 3000,
                DtrEnable = false,
                RtsEnable = false,
            };

            if (writeToSerial)
            {
                Console.WriteLine("Opening Serial");
                serial.Open();
                Console.WriteLine("Serial Open");
            }

            Stream inStream = GetStandardInputStream();
            FileStream outFileStream = null;
            if (writeToFile)
            {
                outFileStream = File.Create("out.avi");
            }
            bool first = true;
            
            while (true)
            {
                byte[] buffer = new byte[bufferSize];

                inStream.Read(buffer, 0, buffer.Length);
                Thread.Sleep(TimeSpan.FromMilliseconds(delayms));
                if (writeToSerial)
                {

                    if (first)
                    {
                        first = false;
                        var str = System.Text.Encoding.UTF8.GetString(buffer);
                        var valid = str.StartsWith("#!rtpplay1.0 127.0.0.1");
                        Console.WriteLine(valid ? "ok" : "bad");
                    }
                    //Console.WriteLine("Streaming " + bufferSize + " bytes");
                    serial.Write(buffer, 0, buffer.Length);
                    //Thread.Sleep(10);
                    //Console.WriteLine("Finished streaming " + bufferSize + " bytes");
                }
                if (writeToFile)
                {
                    outFileStream.Write(buffer, 0, buffer.Length);
                }
            }
        }

        private static void UpdateSettings()
        {
            while (true)
            {
                Thread.Sleep(1000);
                var lines = File.ReadLines("settings.txt").ToArray();
                var buffer = int.Parse(lines[0]);
                bufferSize = buffer;
                double delay = double.Parse(lines[1]);
                delayms = delay;
            }
        }

        private static Stream GetStandardInputStream()
        {

            return Console.OpenStandardInput(bufferSize);
            //return File.OpenRead("C:\\repo\\rtptools\\rtptools-1.21\\Debug\\out.avi");
        }

        //private static Stream GetSerialInputStream()
        //{
            
        //}
    }
}
