using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using WindowWatcherCore;

namespace SerialSenderNetCore
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
        private static int _bufferSize = 783; // 1044 encoded  //170;
        //static int hwBufferSize = 2048;
        private static int crcByteSize = 12;
        private static int _sentBytes;
        private static int _sentKb;
        
        private static DateTime? _transferTimestamp;
        private static readonly TimeSpan Onemilli = TimeSpan.FromMilliseconds(1);

        private static Stopwatch _sw;

        private static IWindowWatcherService _windowWatcherService;
        private static readonly ConcurrentQueue<string> ApplicationWindowChanges = new ConcurrentQueue<string>();
        private static IStreamFactory _streamFactory;

        public static void Main()
        {
            // chooses the correct implementation based on configuration
            var startupConfig = new StartupConfig();
            ConfigureImplementations(startupConfig);
            var fileWatcher = new FileSystemWatcher(@"C:\repo\usb_serial_receive\pcside\SerialSenderNetCore\SerialSenderNetCore\bin\Debug\netcoreapp2.0")
                { NotifyFilter =  NotifyFilters.LastWrite};
            CreateStreams();
            var dw = new DataWriter(_streamFactory, _bufferSize, crcByteSize, fileWatcher);

            _windowWatcherService.WindowSelected += WindowWatcherServiceOnWindowSelected;
            _windowWatcherService.StartService();

            _sw = new Stopwatch();
            _sw.Start();
            

            var initFrameInputStream = GetInitFrameInputStream();
            
      
            Console.WriteLine("Opening Serial");

            _streamFactory.OutputStream.Open();
            Console.WriteLine("Clearing write HW buffer");
           
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(Onemilli);
                _streamFactory.OutputStream.Write("%IGNORE%");
            }

            Console.WriteLine("Serial Open");
            
            
            var first = true;
            var crcCalcCount = 0;
            
            var standardInputBuffer = new byte[_bufferSize];

            var stop = false;
            while (!stop)
            {
                try
                {

                    if (!_transferTimestamp.HasValue)
                    {
                        _transferTimestamp = DateTime.Now;
                    }

                    _sentBytes += _bufferSize;

                    if (_transferTimestamp.Value.Add(TimeSpan.FromSeconds(1)) < DateTime.Now)
                    {
                        Console.WriteLine(_sw.Elapsed + "   Transferred: " + _sentBytes / 1024 + "KB at " +
                                          ((_sentBytes / 1024) - _sentKb) + "KB/s");
                        _sentKb = _sentBytes / 1024;
                        _transferTimestamp = DateTime.Now;
                    }

                    crcCalcCount++;

                    var dequeued = false;

                    if (first)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            Thread.Sleep(1);
                            _streamFactory.OutputStream.Write("%IGNORE%");
                        }
                        initFrameInputStream.Read(standardInputBuffer, 0, _bufferSize);
                    }
                    else
                    {
                        string applicationName = null;
                        string applicationNameTmp = null;
                        while (ApplicationWindowChanges.TryDequeue(out applicationNameTmp))
                        {
                            if (applicationNameTmp != null)
                            {
                                applicationName = applicationNameTmp;
                            }
                            // repeat until false
                        }

                        
                     
                        if (applicationName != null)
                        {
                            Console.WriteLine("Dequeued:" + applicationName);
                            dequeued = true;
                            var keysetId = MapApplicationToKeysetId(applicationName);
                            var commandBytesList = CreateCommandBytes(0, "switch_layout", keysetId);
                            foreach (var commandBytes in commandBytesList)
                            {
                                commandBytes.CopyTo(standardInputBuffer, 0);
                            }
                        } 
                        else
                        {
                            var msg = PadToMessageOrSplit(Encoding.UTF8.GetBytes("IGNORE|")).First();
                            msg.CopyTo(standardInputBuffer, 0);
                        }
                    }
                    
                    if (first)
                    {
                        first = false;
                    }

                    dw.WriteData(standardInputBuffer, crcCalcCount);
                    if (dequeued)
                    {
                        Console.WriteLine("Sent");
                    }
                }
                catch (Exception ex)
                {
                    var ok = false;
                    while (!ok)
                    {
                        try
                        {
                            dw.ReInitialiseSerial();
                            ok = true;
                        }
                        catch (Exception ex2)
                        {
                            // something is _really_ wrong.
                            Thread.Sleep(100);
                        }
                    }

                    first = true;
                    initFrameInputStream = GetInitFrameInputStream();
                    dw.KeepSending = true;
                }
            }

            _windowWatcherService.StopService();
        }


        private static void ConfigureImplementations(StartupConfig startupConfig)
        {
            _windowWatcherService =  (IWindowWatcherService) Activator.CreateInstance(startupConfig.WindowWatcherService);
            _streamFactory = (IStreamFactory) Activator.CreateInstance(startupConfig.OutputStreamFactory);
        }

        private static string MapApplicationToKeysetId(string applicationName)
        {
            return applicationName;
        }

        private static void WindowWatcherServiceOnWindowSelected(object sender, string s)
        {
            Console.WriteLine(s);
            ApplicationWindowChanges.Enqueue(s);
        }

        private static string CreateCommand(int i, string commandstring, string commandargs)
        {       
            var tmpString = "|command|" + i.ToString().PadLeft(6, '0') + "|" + commandstring + "|" + commandargs + "|";
            var length = tmpString.Length;
            var result = "START|" + length.ToString().PadLeft(4,'0') + tmpString;
            return result;
        }

        private static IEnumerable<byte[]> CreateCommandBytes(int i, string commandstring, string commandargs)
        {
            return PadToMessageOrSplit(System.Text.Encoding.UTF8.GetBytes(CreateCommand(i, commandstring, commandargs)));
        }

       
        private static IEnumerable<byte[]> PadToMessageOrSplit(byte[] bytes)
        {
            if (bytes.Length == _bufferSize)
            {
                return new List<byte[]>
                {
                    bytes
                };
            }
            if (bytes.Length < _bufferSize)
            {
                var newBytes = new byte[_bufferSize];
                bytes.CopyTo(newBytes, 0);
                return new List<byte[]>
                {
                    newBytes
                };
            }
            else
            {
                // split
                var output = new List<byte[]>();
                int i = 0;
                while (i < bytes.Length)
                {
                    var length = bytes.Length - i;
                    if (length > _bufferSize)
                    {
                        length = _bufferSize;
                    }
                    var newBytes = new byte[_bufferSize];
                    Array.Copy(bytes,i,newBytes,0,length);
                    output.Add(newBytes);
                    i += length;
                }
                return output;
            }
        }


        
        private static void CreateStreams()
        {
             _streamFactory.CreateNewOutputStream();
             _streamFactory.CreateNewInputStream();
        }



        
        private static Stream GetInitFrameInputStream()
        {
            //return Console.OpenStandardInput(bufferSize);
            return File.OpenRead("bunny.avi");
        }
    }
}
