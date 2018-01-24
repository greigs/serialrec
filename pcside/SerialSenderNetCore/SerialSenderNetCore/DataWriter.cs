using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DamienG.Security.Cryptography;

namespace SerialSenderNetCore
{
    public class DataWriter
    {
        private readonly IStreamFactory _streamFactory;
        private readonly int _bufferSize;
        private readonly int _crcByteSize;
        private TimeSpan Delay1 = TimeSpan.FromMilliseconds(1);
        private TimeSpan Delay2 = TimeSpan.FromMilliseconds(1);
        private TimeSpan Delay3 = TimeSpan.FromMilliseconds(1);
        private TimeSpan Delay4 = TimeSpan.FromMilliseconds(1);
        private TimeSpan Delay5 = TimeSpan.FromMilliseconds(1);
        private TimeSpan Delay6 = TimeSpan.FromMilliseconds(1);
        private TimeSpan Delay7 = TimeSpan.FromMilliseconds(1);
        private TimeSpan Delay8 = TimeSpan.FromMilliseconds(1);
        private TimeSpan Delay9 = TimeSpan.FromMilliseconds(1);
        private TimeSpan Delay10 = TimeSpan.FromMilliseconds(50);
        private TimeSpan Delay11 = TimeSpan.FromMilliseconds(30);
        private TimeSpan Delay12 = TimeSpan.FromMilliseconds(100);
        private int writeIgnoreCount = 100;
        private int numFailuresLocalThreshold = 10;
        private int numFailuresGlobalThreshold = 100;
        private int failCountThreshold = 5;
        private int triesThreshold = 10;
        private int failureCountThreshold = 500;
        private const string ReadyString = "%READY%";


        public DataWriter(IStreamFactory streamFactory, int bufferSize, int crcByteSize, FileSystemWatcher f)
        {
            _streamFactory = streamFactory;
            _bufferSize = bufferSize;
            _crcByteSize = crcByteSize;            
            f.Changed += F_Changed;
            f.EnableRaisingEvents = true;

            //f.BeginInit();
        }

        private void F_Changed(object sender, FileSystemEventArgs e)
        {
            var read = false;
            string[] lines = new string[]{};
            while (!read)
            {
                try
                {
                    lines = File.ReadAllLines(e.FullPath);
                    read = true;
                }
                catch
                {

                }
            }
            Delay1 = TimeSpan.FromMilliseconds(double.Parse(lines[0].Split(':').Last()));
            Delay2 = TimeSpan.FromMilliseconds(double.Parse(lines[1].Split(':').Last()));
            Delay3 = TimeSpan.FromMilliseconds(double.Parse(lines[2].Split(':').Last()));
            Delay4 = TimeSpan.FromMilliseconds(double.Parse(lines[3].Split(':').Last()));
            Delay5 = TimeSpan.FromMilliseconds(double.Parse(lines[4].Split(':').Last()));
            Delay6 = TimeSpan.FromMilliseconds(double.Parse(lines[5].Split(':').Last()));
            Delay7 = TimeSpan.FromMilliseconds(double.Parse(lines[6].Split(':').Last()));
            Delay8 = TimeSpan.FromMilliseconds(double.Parse(lines[7].Split(':').Last()));
            Delay9 = TimeSpan.FromMilliseconds(double.Parse(lines[8].Split(':').Last()));
            Delay10 = TimeSpan.FromMilliseconds(double.Parse(lines[9].Split(':').Last()));
            Delay11 = TimeSpan.FromMilliseconds(double.Parse(lines[10].Split(':').Last()));
            Delay12 = TimeSpan.FromMilliseconds(double.Parse(lines[11].Split(':').Last()));
            writeIgnoreCount = int.Parse(lines[12].Split(':').Last());
            numFailuresLocalThreshold = int.Parse(lines[13].Split(':').Last());
            numFailuresGlobalThreshold = int.Parse(lines[14].Split(':').Last());
            failCountThreshold = int.Parse(lines[15].Split(':').Last());
            triesThreshold = int.Parse(lines[16].Split(':').Last());
            failureCountThreshold = int.Parse(lines[17].Split(':').Last());
        }

        public bool KeepSending { get; set; }


        private static string PadTo12(int i)
        {
            return i.ToString().PadRight(12, ' ');
        }


        /// <summary>
        /// Calculates how much of a string is still needed to be read given the matching partial or whole string.
        /// Returns null if whole string matches
        /// </summary>
        /// <param name="matchString"></param>
        /// <param name="alreadyRead"></param>
        /// <returns></returns>
        public string CalculateRequiredMatch(string matchString, string alreadyRead)
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


        private void ReadUntilWhileSendingIgnore(IInputStream serialInput, string matchString)
        {
            try
            {
                var allMatch = false;
                int charIndex = 0;
                byte[] singleByte = new byte[1];
                var failureCount = 0;
                while (!allMatch)
                {
                    bool read = serialInput.ReadAsync(singleByte, 0, 1).Wait(Delay11);
                    if ((char)singleByte[0] == matchString[charIndex])
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
                        KeepSending = false;
                        //t.Wait();
                        //Console.WriteLine("got %READY%");
                    }

                    if (!read)
                    {
                        var cancellationSource = new CancellationTokenSource();
                        var t = SendIgnoresThread(cancellationSource.Token);
                        t.Wait(Delay8);
                        cancellationSource.Cancel();

                    }

                    if (!allMatch)
                    {
                        failureCount++;
                        if (failureCount > failureCountThreshold)
                        {
                            throw new Exception();
                        }
                    }
                }
            }
            finally
            {
                var prevKeepSendingValue = KeepSending;
                KeepSending = false;
                var cancellationSource = new CancellationTokenSource();
                var t = SendIgnoresThread(cancellationSource.Token);
                t.Wait(Delay9);
                cancellationSource.Cancel();
                KeepSending = prevKeepSendingValue;
            }
        }

        private Task SendIgnoresThread(CancellationToken cancellationToken)
        {
            var t = new Task(() =>
            {
                while (KeepSending && !cancellationToken.IsCancellationRequested)
                {
                    for (int i = 0; i < 1; i++)
                    {
                        Thread.Sleep(Delay7);
                        bool ok = false;
                        while (!ok && !cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                if (!_streamFactory.OutputStream.IsDisposed && _streamFactory.OutputStream.IsOpen)
                                {
                                    _streamFactory.OutputStream.Write("%IGNORE%");
                                    ok = true;
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }, cancellationToken);
            t.Start();
            return t;
        }

        private void WriteBytesToSerialWithRetry(byte[] buffer, string crc)
        {
            //Console.WriteLine(Encoding.ASCII.GetString(standardInputBuffer));
            _streamFactory.OutputStream.WriteAsync(buffer, 0, buffer.Length).Wait(Delay12);
            int waitCount = 0;
            while (_streamFactory.OutputStream.BytesToWrite > 0)
            {
                Thread.Sleep(Delay1);
                waitCount++;
            }
            //Console.WriteLine("Sent data, waiting for CRC response (sending ignores)");
            // await response

            var response = SendIgnoreUntilResponse("D8BD394B CRC OK!!!".Length);
            int tries = 0;
            while (!ResponseIsOk("CRC" + response, crc))
            {
                tries++;

                if (tries > triesThreshold)
                {
                    //Console.WriteLine("Tries:" + tries);
                    ReInitialiseSerial();
                }

                if (tries > 20)
                {
                    throw new Exception();
                }
                //Console.WriteLine($"Response NOT ok ({response}), waiting for ready signal to retry");
                ReadUntil(ReadyString, response);
                //Console.WriteLine("Got READY, resending");
                //Console.WriteLine(Encoding.ASCII.GetString(standardInputBuffer));
                //var str = System.Text.Encoding.UTF8.GetString(buffer);

                _streamFactory.OutputStream.Write(buffer, 0, buffer.Length);
                //Thread.Sleep(10);
                while (_streamFactory.OutputStream.BytesToWrite > 0)
                {
                    Thread.Sleep(Delay2);
                }
                //Console.WriteLine("waiting for CRC response (sending ignores)");
                response = SendIgnoreUntilResponse("D8BD394B CRC OK!!!".Length);
            }

        }

        private int GetLengthOfBase64Bytes(int byteLength)
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

        private void AddCrcToEndOfBuffer(byte[] buffer, string crc, int calcCount)
        {
            var crcBytes = Encoding.UTF8.GetBytes(crc + PadTo12(calcCount));
            var offset = buffer.Length - _crcByteSize - _crcByteSize;
            crcBytes.CopyTo(buffer, offset);
        }

        private string ReadCharsAsAscii(IOutputStream serialOutput, IInputStream serialInput, int charsToRead, bool sendIgnores = false)
        {
            byte[] buffer = new byte[charsToRead];
            var tmp = Encoding.UTF8.GetString(buffer);
            string stringResult = null;
            bool ok = false;
            int failcount = 0;
            while (!ok)
            {
                bool finishedInTime = serialInput.ReadAsync(buffer, 0, charsToRead).Wait(Delay10);

                if (!finishedInTime)
                {
                    if (sendIgnores)
                    {
                        for (int i = 0; i < 1; i++)
                        {
                            try
                            {
                                serialOutput.Write("%IGNORE%");
                                failcount++;
                                if (failcount > 20)
                                {
                                    throw new Exception();
                                }
                            }
                            catch
                            {
                                failcount++;
                                if (failcount > 25)
                                {
                                    throw new Exception();
                                }
                                Thread.Sleep(Delay3);
                            }
                            //Thread.Sleep(1);
                        }
                    }
                }
                if (finishedInTime)
                {
                    stringResult = Encoding.UTF8.GetString(buffer);

                    if (tmp != stringResult)
                    {
                        ok = true;
                    }
                }
            }
            return stringResult;
        }


        private string CalculateCrc(byte[] data, int length)
        {
            var crc32 = new Crc32();
            var hash = string.Empty;

            int bytecount = 0;
            foreach (var b in crc32.ComputeHash(data, 0, length))
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
            }

            return "CRC:" + hash;
        }



        private string SendIgnoreUntilResponse(int length)
        {
            int numErrors = 0;

            if (_streamFactory.InputStream.BytesToRead > 0)
            {
                //var read = serial.ReadExisting();
                var read = ReadCharsAsAscii(_streamFactory.OutputStream, _streamFactory.InputStream, length);
                return read;
            }

            var response = ReadCharsAsAscii(_streamFactory.OutputStream, _streamFactory.InputStream, length, true);

            if (response.Contains("OK"))
            {
                Console.Write('#');
            }
            else if (response.Contains("CRC ERROR"))
            {
                numErrors++;
            }
            else
            {
                Console.WriteLine("Unknown Response");
            }

            //keepsending = false;
            //t.Wait();

            if (numErrors == 0)
            {
                //Console.WriteLine("ALL OK");
            }

            return response;
        }

        private bool ResponseIsOk(string response, string crc)
        {
            if (response != "CRC00000000 CRC ERROR" && !response.Contains("CRC OK"))
            {
                Console.Write('b');
            }

            return response.Replace(":", string.Empty).StartsWith(crc.Replace(":", string.Empty)) && response.Contains("CRC OK!!!");
        }

        private void ReadUntil(string matchString, string alreadyRead = null)
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

            if (_streamFactory.InputStream.BytesToRead > 0)
            {
                var read = ReadCharsAsAscii(_streamFactory.OutputStream, _streamFactory.InputStream, _streamFactory.InputStream.BytesToRead);
                if (read.Length > matchOnResult.Length)
                {
                    matchOnResult = matchString;
                }
                if (read.EndsWith(matchString))
                {
                    return;
                }
            }

            //bool keepsending = true;
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

                if (_streamFactory.InputStream.BytesToRead > 0)
                {
                    var read = _streamFactory.InputStream.ReadExisting();
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
                var numFailuresGlobal = 0;
                var n = 0;
                while (!readOne)
                {
                    if (_streamFactory.InputStream.BytesToRead > 0)
                    {
                        var data = (char)_streamFactory.InputStream.ReadChar();
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
                        numFailuresGlobal++;
                        Thread.Sleep(Delay4);
                        if (numFailures > numFailuresLocalThreshold)
                        {
                            ReInitialiseSerial();
                            numFailures = 0;

                            for (int i = 0; i < 1; i++)
                            {
                                _streamFactory.OutputStream.Write("%IGNORE%");
                            }

                        }

                        if (numFailuresGlobal > numFailuresGlobalThreshold)
                        {
                            throw new Exception();
                        }

                    }
                }
            }

            //keepsending = false;
        }

        public void ReInitialiseSerial()
        {
            //Console.WriteLine("Reinitialise");
            _streamFactory.OutputStream.Close();
            while (_streamFactory.OutputStream.IsOpen)
            {
                Thread.Sleep(Delay5);
            }

            _streamFactory.OutputStream.Dispose();
            CreateStreams();

            _streamFactory.OutputStream.Open();
            while (!_streamFactory.OutputStream.IsOpen)
            {
                Thread.Sleep(Delay6);
            }
            try
            {
                for (int index = 0; index < writeIgnoreCount; index++)
                {
                    _streamFactory.OutputStream.Write("%IGNORE%");
                }


            }
            catch { }

            //Console.WriteLine("Reinitialise Complete");
            //GC.Collect();
        }

        private void CreateStreams()
        {
            _streamFactory.CreateNewOutputStream();
            _streamFactory.CreateNewInputStream();
        }


        public void WriteData(byte[] dataToWrite, int crcCalcCount)
        {
            var lengthofBase64Normal = GetLengthOfBase64Bytes(_bufferSize);
            var lengthofBase64NormalPlusCrcSize = lengthofBase64Normal + _crcByteSize + _crcByteSize; // not sure why the extra!


            var base64Bytes = new byte[lengthofBase64NormalPlusCrcSize];
            var convertedChars = new char[lengthofBase64NormalPlusCrcSize];

            var lengthOfConverted = Convert.ToBase64CharArray(dataToWrite, 0,
                dataToWrite.Length, convertedChars, 0, Base64FormattingOptions.None);
            Encoding.UTF8.GetBytes(convertedChars, 0, convertedChars.Length, base64Bytes, 0);


            var len = base64Bytes.Length - _crcByteSize - _crcByteSize; // again, not sure why two of these needed
            //var bytes = System.Text.Encoding.UTF8.GetBytes(str);


            var crc = CalculateCrc(base64Bytes, len);
            //Console.WriteLine(crc);
            AddCrcToEndOfBuffer(base64Bytes, crc, crcCalcCount);

            //var str = System.Text.Encoding.UTF8.GetString(base64Bytes);

            //Thread.Sleep(TimeSpan.FromMilliseconds(delayms));

            // wait until we have the all clear
            //ReadUntil(serial, ReadyString);
            //Console.WriteLine("Waiting for READY");

            var failCount = 0;
            var ok = false;
            while (!ok)
            {
                failCount++;
                if (failCount > failCountThreshold)
                {
                    throw new Exception();
                }
                try
                {
                    ReadUntilWhileSendingIgnore(_streamFactory.InputStream, "%READY%");
                    ok = true;
                }
                catch (Exception ex)
                {
                    ReInitialiseSerial();
                    if (failCount > 5)
                    {
                        throw new Exception();
                    }
                }
            }
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
    }
}