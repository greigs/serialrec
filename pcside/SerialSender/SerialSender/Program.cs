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
        private static int bufferSize = 783; // 1044 encoded  //170;
        //static int hwBufferSize = 2048;
        private static int crcByteSize = 12;
        private const string ReadyString = "%READY%";
        private static bool updateSettingsLiveEnabled = false;
        private static bool writeToSerial = true;
        private static bool writeToFile = false;
        private static string serialPortName = "COM3";
        private static int sentBytes;
        private static int sentKb;
        private static SerialPort serial;
        private static DateTime? TransferTimestamp;
        private static int baudRate = 115200;


        static void Main()
        {

            int largest = 0;
            for (int i = 0; i < 12000; i++)
            {
                if (GetLengthOfBase64Bytes(i) == 1044)
                {
                    largest = i;
                }
            }



            StringBuilder sb = new StringBuilder();
            for (int i=0; i < 16; i++)
            {
                sb.Append("3");
            }


            Thread t = new Thread(UpdateSettings);

            if (updateSettingsLiveEnabled)
            {
                t.Start();
            }


            serial = CreateSerial();

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
                outFileStream = File.Create("out.txt");
            }
            bool first = true;

            int set = 0;
            int crcCalcCount = 0;

            var lengthofBase64Normal = GetLengthOfBase64Bytes(bufferSize);
            var lengthofBase64NormalPlusCrcSize = lengthofBase64Normal + crcByteSize + 12;

            
            byte[] standardInputBuffer = new byte[bufferSize];
            byte[] base64Bytes = new byte[lengthofBase64NormalPlusCrcSize];
            char[] convertedChars = new char[lengthofBase64NormalPlusCrcSize];

            int lastAmountSent = 0;

            while (true)
            {
                if (!TransferTimestamp.HasValue)
                {
                    TransferTimestamp = DateTime.Now;
                }

                //Console.WriteLine();
                

                sentBytes += bufferSize;
                set++;

                if (TransferTimestamp.Value.Add(TimeSpan.FromSeconds(1)) < DateTime.Now )
                {
                    Console.WriteLine("Transferred: " + sentBytes/1024 + "KB at "  + ((sentBytes / 1024) - sentKb ) + "KB/s");
                    sentKb = sentBytes/1024;
                    TransferTimestamp = DateTime.Now;
                }
                

                crcCalcCount++;


                if (first)
                {

                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(3);
                        if (writeToSerial)
                        {
                            serial.Write("%IGNORE%");
                        }
                    }
                }                
                inStream.Read(standardInputBuffer, 0, bufferSize);

                if (first)
                {
                    first = false;
                }

                var lengthOfConverted = Convert.ToBase64CharArray(standardInputBuffer, 0, standardInputBuffer.Length, convertedChars, 0, Base64FormattingOptions.None);
                System.Text.Encoding.UTF8.GetBytes(convertedChars, 0, convertedChars.Length, base64Bytes, 0);


                var len = base64Bytes.Length - crcByteSize - 12;
                //var bytes = System.Text.Encoding.UTF8.GetBytes(str);
                

                var crc = CalculateCRC(base64Bytes, len);
                //Console.WriteLine(crc);
                AddCrcToEndOfBuffer(base64Bytes, crc, crcCalcCount);

                //var str = System.Text.Encoding.UTF8.GetString(base64Bytes);

                //Thread.Sleep(TimeSpan.FromMilliseconds(delayms));
                if (writeToSerial)
                {
                    // wait until we have the all clear
                    //ReadUntil(serial, ReadyString);
                    //Console.WriteLine("Waiting for READY");
                    ReadUntilWhileSendingIgnore(serial, "%READY%");
                    //Console.WriteLine("Got READY");


                    //if (first)
                    //{
                    //    first = false;
                    //    var str = System.Text.Encoding.UTF8.GetString(standardInputBuffer);
                    //    var valid = str.StartsWith("#!rtpplay1.0 127.0.0.1");
                    //    Console.WriteLine(valid ? "ok" : "bad");
                    //}
                    WriteBytesToSerialWithRetry(base64Bytes, crc);
                    //Console.WriteLine("Response ok, waiting for next ready signal to continue");
                }
                if (writeToFile)
                {
                    outFileStream.Write(base64Bytes, 0, base64Bytes.Length - 12);
                }
            }
        }


        private static int GetLengthOfBase64Bytes(int byteLength)
        {
            char a = 'a';
            List<byte> chars = new List<byte>();
            for (int i = 0; i < byteLength; i++)
            {
                chars.Add((byte)a);
            }

            var str4 = Convert.ToBase64String(chars.ToArray());
            return str4.Length;
        }

        private static void AddCrcToEndOfBuffer(byte[] buffer, string crc, int calcCount)
        {
            var crcBytes = Encoding.UTF8.GetBytes(crc + PadTo12(calcCount));
            var offset = buffer.Length - crcByteSize - 12;
            crcBytes.CopyTo(buffer, offset);
        }

        private static string PadTo12(int i)
        {
            return i.ToString().PadRight(12,' ');
        }

        private static void WriteBytesToSerialWithRetry(byte[] buffer, string crc)
        {
            //Console.WriteLine(Encoding.ASCII.GetString(standardInputBuffer));
            serial.Write(buffer, 0, buffer.Length);
            while (serial.BytesToWrite > 0)
            {
                Thread.Sleep(1);
            }
            //Console.WriteLine("Sent data, waiting for CRC response (sending ignores)");
            // await response

            var response = SendIgnoreUntilResponse("D8BD394B CRC OK!!!".Length);
            int tries = 0;
            while (!ResponseIsOk("CRC" + response, crc))
            {
                tries++;

                if (tries > 1)
                {
                    Console.WriteLine("Tries:" + tries);
                }
                //Console.WriteLine($"Response NOT ok ({response}), waiting for ready signal to retry");
                ReadUntil(ReadyString, response);
                //Console.WriteLine("Got READY, resending");
                //Console.WriteLine(Encoding.ASCII.GetString(standardInputBuffer));
                //var str = System.Text.Encoding.UTF8.GetString(buffer);

                serial.Write(buffer, 0, buffer.Length);
                //Thread.Sleep(10);
                while (serial.BytesToWrite > 0)
                {
                    Thread.Sleep(1);
                }
                //Console.WriteLine("waiting for CRC response (sending ignores)");
                response = SendIgnoreUntilResponse( "D8BD394B CRC OK!!!".Length);
            }

            //Console.WriteLine("Response OK");
        }

        private static string SendIgnoreUntilResponse(int length)
        {
            int numOK = 0;
            int numErrors = 0;

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
                        Thread.Sleep(TimeSpan.FromMilliseconds(0.5));
                        serial.Write("%IGNORE%");
                    }
                }
            });
            t.Start();
            var response = ReadCharsAsASCII(serial, length);

            if (response.Contains("OK"))
            {
                numOK++;
            }

            else if (response.Contains("CRC ERROR"))
            {
                Console.WriteLine(response + ", Num OK: " + numOK);
                numErrors++;
            }
            else
            {
                Console.WriteLine("Unknown Response");
            }
            
            keepsending = false;
            t.Wait();

            if (numErrors == 0)
            {
                //Console.WriteLine("ALL OK");
            }

            return response;
        }

        private static bool ResponseIsOk(string response, string crc)
        {
            if (response != "CRC00000000 CRC ERROR" && !response.Contains("CRC OK"))
            {
                
            }

            return response.Replace(":", string.Empty).StartsWith(crc.Replace(":", string.Empty)) && response.Contains("CRC OK!!!");
        }

        private static void ReadUntil(string matchString, string alreadyRead = null)
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

                bool readOne = false;
                var numFailures = 0;
                while (!readOne)
                {
                    if (serial.BytesToRead > 0)
                    {
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

                        readOne = true;
                    }
                    else
                    {
                        numFailures++;
                        Thread.Sleep(1);
                        if (numFailures > 100)
                        {
                            ReInitialiseSerial();
                            numFailures = 0;

                            for (int i = 0; i< 100; i++)
                            {
                                serial.Write("%IGNORE%");
                            }
                            
                        }

                    }
                }
            }

            keepsending = false;
            
        }

        private static void ReInitialiseSerial()
        {
            Console.WriteLine("Reinitialise");
            serial.Close();
            while (serial.IsOpen)
            {
                Thread.Sleep(10);
            }
            
            serial.Dispose();
            serial = CreateSerial();
            
            serial.Open();
            while (!serial.IsOpen)
            {
                Thread.Sleep(10);
            }
            Console.WriteLine("Reinitialise Complete");

        }

        private static SerialPort CreateSerial()
        {
            return new SerialPort(serialPortName, baudRate)
            {
                //
                //ReadBufferSize = 2000,
                //DiscardNull = true,Handshake = Handshake.None,
                //DtrEnable = false,
                Handshake = Handshake.None,
                //RtsEnable = false,
                //DiscardNull = false,
                //ReadBufferSize = bufferSize,
                WriteBufferSize = 128,
                //Handshake = Handshake.XOnXOff,
                //Parity = Parity.Even,
                DataBits = 8,
                StopBits = StopBits.One,

                //ReceivedBytesThreshold = bufferSize,
                DtrEnable = false,
                RtsEnable = false,
            };
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
                        Thread.Sleep(TimeSpan.FromMilliseconds(0.5));
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

            return  "CRC:" + hash;
        }

        private static void UpdateSettings()
        {
            while (true)
            {
                Thread.Sleep(1000);
                var lines = File.ReadLines("settings.txt").ToArray();
                var buffer = int.Parse(lines[0]);
                bufferSize = buffer;

            }
        }

        private static Stream GetStandardInputStream()
        {

            return Console.OpenStandardInput(bufferSize);
            //return File.OpenRead("C:\\repo\\rtptools\\rtptools-1.21\\Debug\\out.avi");
        }

    }
}
