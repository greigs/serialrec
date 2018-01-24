using System.Threading.Tasks;
using RJCP.IO.Ports;

namespace SerialSenderNetCore
{
    public class SerialStreamWrapper : IOutputStream, IInputStream
    {
        private readonly SerialPortStream serialPortStream;

        public SerialStreamWrapper(SerialPortStream serialPortStream)
        {
            this.serialPortStream = serialPortStream;
        }

        public void Dispose() => this.serialPortStream.Dispose();

        public void Close() => this.serialPortStream.Close();

        public bool IsOpen => serialPortStream.IsOpen;
        public int BytesToRead => serialPortStream.BytesToRead;
        public int BytesToWrite => serialPortStream.BytesToWrite;
        public bool IsDisposed  => serialPortStream.IsDisposed;

        public async Task WriteAsync(byte[] buffer, int i, int bufferLength)
        {
            await serialPortStream.WriteAsync(buffer, i, bufferLength);
        }

        public async Task ReadAsync(byte[] buffer, int i, int charsToRead)
        {
            await serialPortStream.ReadAsync(buffer, i, charsToRead);
        }

        public string ReadExisting() => serialPortStream.ReadExisting();
        public int ReadChar() => serialPortStream.ReadChar();
        public void Write(byte[] buffer, int v, int length) => serialPortStream.Write(buffer, v, length);

        public void Write(string ignore) => serialPortStream.Write(ignore);

        public void Open() => serialPortStream.Open();

    }
}