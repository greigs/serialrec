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
    ///     so keep sending thesxe with delays inbetween to
    ///     "flush" the data flow
    /// 
    ///     around 4k seems to be the magic number, then send an %IGNORE% message
    ///     with delays in between until a CRC response is returned
    /// 
    /// </summary>
    public class Program
    {
        static int bufferSize = 16384;
        //static int hwBufferSize = 2048;
        private static int crcByteSize = 11;
        static double delayms = 0.001;
        private const string ReadyString = "%READY%";
        private static string clearHwBufferString;
        private static bool updateSettingsLiveEnabled = false;
        private static bool writeToSerial = true;
        private static bool writeToFile = false;
        private static string serialPortName = "COM3";
        private static int sent = 0;

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


            var serial = new SerialPort(serialPortName, 115200)
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
                //byte[] buffertmp = new byte[4095];
                //byte[] buffertmp2 = new byte[buffertmp.Length + 1];

                //for (int i = 0; i < 1; i++)
                //{
                //    inStream.Read(buffertmp, 0, buffertmp.Length);

                //    buffertmp.CopyTo(buffertmp2,0);
                //    buffertmp2[buffertmp2.Length - 1] = (byte) '\n';
                //    serial.Write(buffertmp2, 0, buffertmp2.Length);
                //    //Thread.Sleep(TimeSpan.FromMilliseconds(delayms));
                //}
                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(3);
                    serial.Write("%IGNORE%");
                }

                Console.WriteLine("Serial Open");

                //for (int i = 0; i < 15; i++)
                //{
                //    Thread.Sleep(10);
                //    serial.Write("%IGNORE%");
                //}
            }

           
            FileStream outFileStream = null;
            if (writeToFile)
            {
                outFileStream = File.Create("out.avi");
            }
            bool first = true;

            int set = 0;
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Set " + set);
                Console.WriteLine(sent + "K");
                Console.WriteLine(sent + "K");
                Console.WriteLine(sent + "K");
                Console.WriteLine(sent + "K");
                Console.WriteLine(sent + "K");
                Console.WriteLine(sent + "K");
                Console.WriteLine(sent + "K");
                sent += (bufferSize / 1024);
                set++;
                byte[] standardInputBuffer = new byte[bufferSize];

                if (first)
                {

                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(3);
                        serial.Write("%IGNORE%");
                    }
                }

                inStream.Read(standardInputBuffer, 0, bufferSize - crcByteSize);
                string str;
                if (first)
                {
                    str = System.Text.Encoding.UTF8.GetString(standardInputBuffer).Substring(13);
                    first = false;
                }
                else
                {
                    str = System.Text.Encoding.UTF8.GetString(standardInputBuffer);
                }
                var len = str.Length - 11;
                var bytes = System.Text.Encoding.UTF8.GetBytes(str);

                var crc = CalculateCRC(bytes, len);
                AddCrcToEndOfBuffer(standardInputBuffer, crc);

                //Thread.Sleep(TimeSpan.FromMilliseconds(delayms));
                if (writeToSerial)
                {
                    // wait until we have the all clear
                    //ReadUntil(serial, ReadyString);
                    Console.WriteLine("Waiting for READY");
                    ReadUntilWhileSendingIgnore(serial, "%READY%");
                    Console.WriteLine("Got READY");


                    //if (first)
                    //{
                    //    first = false;
                    //    var str = System.Text.Encoding.UTF8.GetString(standardInputBuffer);
                    //    var valid = str.StartsWith("#!rtpplay1.0 127.0.0.1");
                    //    Console.WriteLine(valid ? "ok" : "bad");
                    //}
                    WriteBytesToSerialWithRetry(serial, standardInputBuffer, crc);
                    //Console.WriteLine("Response ok, waiting for next ready signal to continue");
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
            //Console.WriteLine(Encoding.ASCII.GetString(standardInputBuffer));
            serial.Write(standardInputBuffer, 0, standardInputBuffer.Length);
            while (serial.BytesToWrite > 0)
            {
                Thread.Sleep(1);
            }
            Console.WriteLine("Sent data, waiting for CRC response (sending ignores)");
            // await response

            var response = SendIgnoreUntilResponse(serial, "7c5f0cc8 CRC OK%%%".Length);
            while (!ResponseIsOk("CRC" + response, crc))
            {
                Console.WriteLine($"Response NOT ok ({response}), waiting for ready signal to retry");
                ReadUntil(serial, ReadyString, response);
                Console.WriteLine("Got READY, resending");
                //Console.WriteLine(Encoding.ASCII.GetString(standardInputBuffer));
                serial.Write(standardInputBuffer, 0, standardInputBuffer.Length);
                //Thread.Sleep(10);
                while (serial.BytesToWrite > 0)
                {
                    Thread.Sleep(1);
                }
                Console.WriteLine("waiting for CRC response (sending ignores)");
                response = SendIgnoreUntilResponse(serial, "7c5f0cc8 CRC OK%%%".Length);
            }

            Console.WriteLine("Response OK");
           
        }

        private static string SendIgnoreUntilResponse(SerialPort serial, int length)
        {
            if (serial.BytesToRead > 0)
            {
                //var read = serial.ReadExisting();
                var read = ReadCharsAsASCII(serial, length);
                return read;
            }

            bool keepsending = true;
            var t = new Task(() =>
            {
                while (keepsending)
                {
                    for (int i = 0; i < 1; i++)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(1));
                        serial.Write("%IGNORE%");
                    }
                }
            });
            t.Start();
            var response = ReadCharsAsASCII(serial, length);

            if (response.Contains("OK"))
            {
                
            }

            else if (response.Contains("CRC ERROR"))
            {

            }
            else
            {
                ;
            }



            keepsending = false;
            t.Wait();
            return response;
        }

        private static bool ResponseIsOk(string response, string crc)
        {
            if (response != "CRC00000000 CRC ERROR")
            {
                
            }

            return response.StartsWith(crc) && response.Contains("CRC OK!!!");
        }

        private static void ReadUntil(SerialPort serial, string matchString, string alreadyRead = null)
        {
            var allMatch = false;
            var matchOnResult = matchString;

            if (alreadyRead != null)
            {
                matchOnResult = CalculateRequiredMatch(matchString, alreadyRead);
                if (alreadyRead.EndsWith("ERROR"))
                {
                    matchOnResult = matchString;
                }
                else if (matchOnResult == null)
                {
                    allMatch = true;
                    return;
                }
            }

            if (serial.BytesToRead > 0)
            {
                var read = ReadCharsAsASCII(serial, serial.BytesToRead);
                if (read.Length > matchOnResult.Length)
                {
                    matchOnResult = matchString;
                }
                if (read.EndsWith(matchString))
                {
                    return;
                }
            }

            bool keepsending = true;
            //var t = new Task(() =>
            //{
            //    while (keepsending)
            //    {
            //        for (int i = 0; i < 1; i++)
            //        {
            //            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            //            serial.Write("%IGNORE%");
            //        }
            //    }
            //});
            //t.Start();

            int charIndex = 0;
            while (!allMatch)
            {

                if (serial.BytesToRead > 0)
                {
                    var read = serial.ReadExisting();
                    if (read.Length > matchOnResult.Length)
                    {
                        matchOnResult = matchString;
                    }
                    if (read.EndsWith(matchString))
                    {
                        return;
                    }
                }

                var data = (char) serial.ReadChar();
                if (data == matchOnResult[charIndex])
                {
                    charIndex++;
                }
                else
                {
                    charIndex = 0;
                }

                if (charIndex == matchOnResult.Length)
                {
                    allMatch = true;
                    //Console.WriteLine("got %READY%");
                }
            }

            keepsending = false;




        }

        /// <summary>
        /// Calculates how much of a string is still needed to be read given the matching partial or whole string.
        /// Returns null if whole string matches
        /// </summary>
        /// <param name="matchString"></param>
        /// <param name="alreadyRead"></param>
        /// <returns></returns>
        public static string CalculateRequiredMatch(string matchString, string alreadyRead)
        {
            string matchOn = matchString;

            // read back the "alreadyRead" string, looking for a full, then partial match

            if (alreadyRead.EndsWith(matchString))
            {
                // done, full match, no need to read more
                return null;
            }

            var lastChar = alreadyRead.Last();
            if (matchString.Contains(lastChar))
            {
                // find the position of the last char from the alreadyread in the match string
                var indexWithinMatchString = matchString.LastIndexOf(lastChar);

                // match the rest of the string back fowards the start,
                // make sure to not go below the first char

                if (indexWithinMatchString == (matchString.Length - 1))
                {
                    // the last character is a match, but the endswith check failed
                    // therefore must be a partial match containing the
                    // same matched character somewhere earlier in the matchstring
                    // need to find the entry point of this substring and match the rest

                    // note the previous character - the second last character that matched in alreadyread
                    var indexOfPrevCharAfterMatched = alreadyRead.Length - matchString.Length + indexWithinMatchString - 1;
                    var prevChar = alreadyRead[indexOfPrevCharAfterMatched];
                    int matchedIndex = -1;
                    for (var i = indexWithinMatchString - 1; i >= 0; i--)
                    {
                        if (prevChar == matchString[i])
                        {
                            matchedIndex = i;
                            break;
                        }
                    }

                    if (matchedIndex > -1)
                    {
                        if (matchedIndex == 0)
                        {
                            //TODO
                        }
                        else
                        {
                            bool ok = true;
                            for (var i = matchedIndex - 1; i >= 0; i--)
                            {
                                var ch = alreadyRead[alreadyRead.Length - matchString.Length - matchedIndex + i - 1 + indexWithinMatchString];
                                if (ch != matchString[i])
                                {
                                    ok = false;
                                    break;
                                }
                            }
                            if (ok)
                            {
                                return matchString.Substring(matchedIndex + 2);
                            }
                        }                        
                    }
                }
                else if (indexWithinMatchString == 0) 
                {
                    // should not happen (this would be a full match)?
                    throw new Exception();
                }
                else // didn't match on the last character of matchedstring
                {
                    var matchedLength = 0;
                    for (var i = indexWithinMatchString; i >= 0; i--)
                    {
                        var ch = alreadyRead[alreadyRead.Length - (indexWithinMatchString - i) - 1];
                        if (matchString[i] == ch)
                        {
                            matchedLength++;
                        }
                        else
                        {
                            break;
                        }

                    }

                    return matchString.Substring(matchedLength);

                }
            }

            return matchOn;
        }

        private static void ReadUntilWhileSendingIgnore(SerialPort serial, string matchString)
        {

            bool keepsending = true;
            var t = new Task(() =>
            {
                while (keepsending)
                {
                    for (int i = 0; i < 1; i++)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(1));
                        serial.Write("%IGNORE%");
                    }
                }
            });
            t.Start();

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
                    keepsending = false;
                    t.Wait();

                    //Console.WriteLine("got %READY%");
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
            var crc32 = new Crc32();
            var hash = string.Empty;

            int bytecount = 0;
            foreach (var b in crc32.ComputeHash(data,0,length))
            {
                if (bytecount < length)
                {
                    hash += b.ToString("x2").ToUpper();
                    bytecount++;
                }
                else
                {
                    break;
                }
            };

            return  "CRC" + hash;
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
