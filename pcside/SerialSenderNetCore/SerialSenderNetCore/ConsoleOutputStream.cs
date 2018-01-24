using System.Threading.Tasks;

namespace SerialSenderNetCore
{
    public class ConsoleOutputStream : IOutputStream
    {
        public void Dispose()
        {
            
        }

        public void Close()
        {
            
        }

        public bool IsOpen { get; }
        public int BytesToRead { get; }
        public int BytesToWrite { get; }
        public bool IsDisposed { get; }
        public async Task WriteAsync(byte[] buffer, int i, int bufferLength)
        {
            
        }

        public void Write(string ignore)
        {
            
        }

        public void Open()
        {
            
        }

        public async Task ReadAsync(byte[] buffer, int i, int charsToRead)
        {
            
        }

        public string ReadExisting()
        {
            return null;
        }

        public int ReadChar()
        {
            return -1;
        }

        public void Write(byte[] buffer, int v, int length)
        {
            
        }
    }
}