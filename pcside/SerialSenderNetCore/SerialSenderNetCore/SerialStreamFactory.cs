using System.Linq;
using RJCP.IO.Ports;

namespace SerialSenderNetCore
{
    public class SerialStreamFactory : IStreamFactory
    {
        private string _foundPort;
        private static readonly int BaudRate = 9600;
        private static SerialStreamWrapper _instance;

        public IOutputStream OutputStream
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }
                return CreateNewOutputStream();
            }
        }

        public IInputStream InputStream
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }
                return CreateNewInputStream();
            }
        }

        public IOutputStream CreateNewOutputStream()
        {
            if (_foundPort == null)
            {
                _foundPort = FindSerialPort();
            }
            _instance = new SerialStreamWrapper(new SerialPortStream(_foundPort, BaudRate)
            {
                //
                //ReadBufferSize = 2000,
                //DiscardNull = true,Handshake = Handshake.None,
                //DtrEnable = false,
                Handshake = Handshake.None,
                //RtsEnable = false,
                //DiscardNull = false,
                //ReadBufferSize = bufferSize,
                WriteBufferSize = 1100,
                ReadBufferSize = 256,
                //Handshake = Handshake.XOnXOff,
                //Parity = Parity.Even,
                DataBits = 8,
                StopBits = StopBits.One,

                //ReceivedBytesThreshold = bufferSize,
                //DtrEnable = false,
                //RtsEnable = true,
            });

            return _instance;
        }

        public IInputStream CreateNewInputStream()
        {
            if (_instance == null)
            {
                CreateNewOutputStream();
            }
            return _instance;
        }

        private static string FindSerialPort()
        {
            var portNames = SerialPortStream.GetPortNames();
            return portNames.OrderByDescending(x => x).FirstOrDefault();
        }
    }
}