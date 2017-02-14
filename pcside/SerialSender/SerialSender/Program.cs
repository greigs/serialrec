using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DamienG.Security.Cryptography;

namespace SerialSender
{
    /// <summary>
    /// Waits for %READY%
    /// Sends data with CRC32 at the end
    /// Waits for response
    /// Checks response is in the form "7c5f0cc8 CRC OK%%"
    ///     if true
    ///         waits for %READY% and sends more 
    ///     else
    ///         retries sending after %READY% recieved
    /// 
    ///     issue: a write() on the serial connection
    ///     cause the message to be truncated and sometimes not get thorough
    ///     Possible solution: send smaller messages?
    ///     Seems to help but some messages are still lost
    ///     
    ///     Update: medium length messages seem to remain in order still get truncated and lost in a
    ///     buffer somewhere if too much is sent at once. Waiting and then
    ///     sending single bytes seems to push the message through,
    ///     so keep sending these with delays inbetween to
    ///     "flush" the data flow
    /// 
    ///     around 4k seems to be the magic number, then send an %IGNORE% message
    ///     until a CRC response is returned
    /// 
    /// 
    /// 
    ///     
    /// 
    /// </summary>
    class Program
    {
        static int bufferSize = 20;
        //static int hwBufferSize = 2048;
        private static int crcByteSize = 8;
        static double delayms = 0.001;
        private const string ReadyString = "%READY%";
        private static string clearHwBufferString;
        private static bool updateSettingsLiveEnabled = false;
        private static bool writeToSerial = true;
        private static bool writeToFile = false;
        private static string serialPortName = "COM3";

        static void Main()
        {
            StringBuilder sb = new StringBuilder();
            for (int i=0; i < 16; i++)
            {
                sb.Append("3");
            }

            clearHwBufferString = sb.ToString();

            Thread t = new Thread(UpdateSettings);

            if (updateSettingsLiveEnabled)
            {
                t.Start();
            }


            var serial = new SerialPort(serialPortName, 9600)
            {
                //
                //ReadBufferSize = 2000,
                //DiscardNull = true,Handshake = Handshake.None,
                //DtrEnable = false,
                Handshake = Handshake.None,
                //RtsEnable = false,
                //DiscardNull = false,
                //ReadBufferSize = bufferSize,
                WriteBufferSize = bufferSize,
                //Handshake = Handshake.XOnXOff,
                //Parity = Parity.Even,
                DataBits = 8,
                StopBits = StopBits.One,
                
                //ReceivedBytesThreshold = bufferSize,
                DtrEnable = false,
                RtsEnable = false,
            };

            Stream inStream = GetStandardInputStream();

            if (writeToSerial)
            {
                Console.WriteLine("Opening Serial");

                serial.Open();
                Console.WriteLine("Clearing write HW buffer");
                byte[] buffertmp = new byte[4095];
                byte[] buffertmp2 = new byte[buffertmp.Length + 1];

                for (int i = 0; i < 1; i++)
                {
                    inStream.Read(buffertmp, 0, buffertmp.Length);

                    buffertmp.CopyTo(buffertmp2,0);
                    buffertmp2[buffertmp2.Length - 1] = (byte) '\n';
                    serial.Write(buffertmp2, 0, buffertmp2.Length);
                    //Thread.Sleep(TimeSpan.FromMilliseconds(delayms));
                }


                Console.WriteLine("Serial Open");

                for (int i = 0; i < 15; i++)
                {
                    Thread.Sleep(10);
                    serial.Write("%IGNORE%");
                }
            }

           
            FileStream outFileStream = null;
            if (writeToFile)
            {
                outFileStream = File.Create("out.avi");
            }
            bool first = true;
            
            while (true)
            {
                byte[] standardInputBuffer = new byte[bufferSize];

                inStream.Read(standardInputBuffer, 0, bufferSize - crcByteSize);
                var crc = CalculateCRC(standardInputBuffer, bufferSize - crcByteSize);
                AddCrcToEndOfBuffer(standardInputBuffer, crc);

                Thread.Sleep(TimeSpan.FromMilliseconds(delayms));
                if (writeToSerial)
                {
                    // wait until we have the all clear
                    ReadUntil(serial, ReadyString);
                    //if (first)
                    //{
                    //    first = false;
                    //    var str = System.Text.Encoding.UTF8.GetString(standardInputBuffer);
                    //    var valid = str.StartsWith("#!rtpplay1.0 127.0.0.1");
                    //    Console.WriteLine(valid ? "ok" : "bad");
                    //}
                    WriteBytesToSerialWithRetry(serial, standardInputBuffer, crc);
                    Console.WriteLine("Response ok, waiting for next ready signal to continue");
                }
                if (writeToFile)
                {
                    outFileStream.Write(standardInputBuffer, 0, standardInputBuffer.Length);
                }
            }
        }

        private static void AddCrcToEndOfBuffer(byte[] standardInputBuffer, string crc)
        {
            var crcBytes = Encoding.ASCII.GetBytes(crc);
            var offset = standardInputBuffer.Length - crcByteSize;
            crcBytes.CopyTo(standardInputBuffer, offset);
        }

        private static void WriteBytesToSerialWithRetry(SerialPort serial, byte[] standardInputBuffer, string crc)
        {
            Console.WriteLine(Encoding.ASCII.GetString(standardInputBuffer));
            serial.Write(standardInputBuffer, 0, standardInputBuffer.Length);
            while (serial.BytesToWrite > 0)
            {
                Thread.Sleep(1);
            }
            Console.WriteLine("read bytes wirh crc " + crc);
            // await response
            var response = ReadCharsAsASCII(serial, "7c5f0cc8 CRC OK%%".Length);
            while (!ResponseIsOk(response, crc))
            {
                Console.WriteLine("Response NOT ok, waiting for ready signal to retry");
                ReadUntil(serial, ReadyString);
                Console.WriteLine(Encoding.ASCII.GetString(standardInputBuffer));
                serial.Write(standardInputBuffer, 0, standardInputBuffer.Length);
                while (serial.BytesToWrite > 0)
                {
                    Thread.Sleep(1);
                }
                response = ReadCharsAsASCII(serial, "7c5f0cc8 CRC OK%%".Length);
            }
           
        }

        private static bool ResponseIsOk(string response, string crc)
        {
            return response.StartsWith(crc) && response.Contains("CRC OK%%");
        }

        private static void ReadUntil(SerialPort serial, string matchString)
        {
            var allMatch = false;
            int charIndex = 0;
            while (!allMatch)
            {

                var data = (char)serial.ReadChar();
                if (data == matchString[charIndex])
                {
                    charIndex++;
                }
                else
                {
                    charIndex = 0;
                }

                if (charIndex == matchString.Length)
                {
                    allMatch = true;
                    Console.WriteLine("got %READY%");
                }
            }
        }

        private static string ReadCharsAsASCII(SerialPort serial, int charsToRead)
        {
            string s = string.Empty;
            for (int i = 0; i < charsToRead; i++)
            {
                s += (char)serial.ReadChar();
            }

            return s;
        }

        private static string CalculateCRC(byte[] data, int length)
        {
            Crc32 crc32 = new Crc32();
            string hash = string.Empty;

            int bytecount = 0;
            foreach (byte b in crc32.ComputeHash(data))
            {
                if (bytecount < length)
                {
                    hash += b.ToString("x2").ToLower();
                    bytecount++;
                }
                else
                {
                    break;
                }
            };

            return hash;
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

    }
}
