using System.Threading.Tasks;

namespace SerialSenderNetCore
{
    public class NamedPipeWindowsOutputStream : IOutputStream
    {
        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public void Close()
        {
            throw new System.NotImplementedException();
        }

        public bool IsOpen { get; }
        public int BytesToRead { get; }
        public int BytesToWrite { get; }
        public bool IsDisposed { get; }
        public Task WriteAsync(byte[] buffer, int i, int bufferLength)
        {
            throw new System.NotImplementedException();
        }

        public void Write(string ignore)
        {
            throw new System.NotImplementedException();
        }

        public void Open()
        {
            throw new System.NotImplementedException();
        }

        public Task ReadAsync(byte[] buffer, int i, int charsToRead)
        {
            throw new System.NotImplementedException();
        }

        public string ReadExisting()
        {
            throw new System.NotImplementedException();
        }

        public int ReadChar()
        {
            throw new System.NotImplementedException();
        }

        public void Write(byte[] buffer, int v, int length)
        {
            throw new System.NotImplementedException();
        }
    }
}