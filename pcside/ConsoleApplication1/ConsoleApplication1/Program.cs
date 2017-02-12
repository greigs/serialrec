using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace ConsoleApplication1
{
    class Program
    {
        static FileStream fileStream;
        static BinaryWriter bw;
        private static bool first = true;

        private static SerialPort sp;
        static void Main(string[] args)
        {

            int buffsize = 16;
            var f = File.Open("c:\\temp\\big_buck_bunny_480p_surround-fix.avi",FileMode.Open);

            sp = new SerialPort("COM6", 9600)
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
                
                Encoding = System.Text.Encoding.GetEncoding("iso-8859-1"),
            };
            
            File.Delete("c:\\temp\\out.avi");
            
            fileStream = File.Create("c:\\temp\\out.avi");
            bw = new BinaryWriter(fileStream,Encoding.GetEncoding("iso-8859-1"));

            Console.WriteLine("Press any key to continue (1)...");
            Console.WriteLine();
            Console.ReadKey();

            sp.Open();
            sp.DiscardInBuffer();


            sp.ErrorReceived += SpOnErrorReceived;
            //sp.DataReceived += Sp_DataReceived;

            Console.WriteLine("Press any key to continue (2)...");
            Console.WriteLine();
            Console.ReadKey();

            
            Console.WriteLine("Running");

            //remove byte at 0x3328, 13096
            //int bufferSize = int.Parse("4000", System.Globalization.NumberStyles.HexNumber);

            long bytesWritten = 0;
            byte[] buff = new byte[16];
            while (true)
            {
                var bytesToRead = sp.BytesToRead;
                if (bytesToRead > 0)
                {

                    var buffer = new byte[bytesToRead];

                    sp.Read(buffer,0, bytesToRead);

                    

                    f.Seek(bytesWritten, SeekOrigin.Begin);
                    
                    f.Read(buff, 0, buffsize);

                    bool match = MatchesFirstBytes(buffsize, buff, buffer);

                    if (match)
                    {
                        Console.WriteLine(bytesToRead + "OK");
                    }
                    else
                    {
                        Console.WriteLine(bytesToRead + "ERROR");
                    }

                    //bw.Write(buffer);

                    bytesWritten += bytesToRead;

                }
            }

        }

        private static bool MatchesFirstBytes(int buffsize, byte[] buff, byte[] buffer)
        {
            bool match = true;
            for (var i = 0; i < buffsize; i++)
            {
                if (buff[i] != buffer[i])
                {
                    match = false;
                }
            }
            return match;
        }

        //private static void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        //{

        //    var bytesToRead = sp.BytesToRead;
        //    if (bytesToRead == 3000)
        //    {
                
        //    }

        //    if (bytesToRead > 0)
        //    {
        //       // Console.WriteLine(bytesToRead);
        //        byte[] buffer = new byte[bytesToRead];
        //        sp.Read(buffer, 0, bytesToRead);
        //        bw.Write(buffer);
        //    }
        //    //Thread t = new Thread(Go);
        //    //t.Start();


        //}

        private static void Go()
        {

        }

        private static void SpOnErrorReceived(object sender, SerialErrorReceivedEventArgs serialErrorReceivedEventArgs)
        {
            throw new NotImplementedException();
        }
    }
}
