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
    class Program
    {


        static int bufferSize = 2048;
        private static int crcByteSize = 8;
        static double delayms = 0.001;
        private const string ReadyString = "%READY%";

        static void Main()
        {

            Thread t = new Thread(UpdateSettings);
            t.Start();

            bool writeToSerial = true;
            bool writeToFile = false;
            var serialPortName = "COM3";
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
                byte[] standardInputBuffer = new byte[bufferSize];

                inStream.Read(standardInputBuffer, 0, bufferSize - crcByteSize);
                var crc = CalculateCRC(standardInputBuffer);
                var crcBytes = Encoding.ASCII.GetBytes(crc);
                int byteOffset = bufferSize - crcByteSize - 1;
                foreach (var crcByte in crcBytes)
                {
                    standardInputBuffer[byteOffset] = crcByte;
                    byteOffset++;
                }
                

                Thread.Sleep(TimeSpan.FromMilliseconds(delayms));
                if (writeToSerial)
                {
                    // wait until we have the all clear
                    ReadUntil(serial, "%READY%");

                    if (first)
                    {
                        first = false;
                        var str = System.Text.Encoding.UTF8.GetString(standardInputBuffer);
                        var valid = str.StartsWith("#!rtpplay1.0 127.0.0.1");
                        Console.WriteLine(valid ? "ok" : "bad");
                    }

                    WriteBytesToSerialWithRetry(serial, standardInputBuffer, crc);


                }
                if (writeToFile)
                {
                    outFileStream.Write(standardInputBuffer, 0, standardInputBuffer.Length);
                }
            }
        }

        private static void WriteBytesToSerialWithRetry(SerialPort serial, byte[] standardInputBuffer, string crc)
        {
            serial.Write(standardInputBuffer, 0, standardInputBuffer.Length);
            Console.WriteLine("read bytes wirh crc " + crc);
            // await response
            var response = ReadCharsAsASCII(serial, "7c5f0cc8 CRC OK  ".Length);
            while (!ResponseIsOk(response, crc))
            {
                Console.WriteLine("Response NOT ok, waiting for ready signal to retry");
                ReadUntil(serial, ReadyString);
                serial.Write(standardInputBuffer, 0, standardInputBuffer.Length);
                response = ReadCharsAsASCII(serial, "7c5f0cc8 CRC OK  ".Length);
            }
            Console.WriteLine("Response ok, waiting for next ready signal to continue");
        }

        private static bool ResponseIsOk(string response, string crc)
        {
            return response.StartsWith(crc) && response.Contains("CRC OK");
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

        private static string CalculateCRC(byte[] data)
        {
            Crc32 crc32 = new Crc32();
            string hash = string.Empty;


            foreach (byte b in crc32.ComputeHash(data))
            {
                hash += b.ToString("x2").ToLower();
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
