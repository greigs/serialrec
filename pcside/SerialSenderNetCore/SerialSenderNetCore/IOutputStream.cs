using System;
using System.Threading.Tasks;

namespace SerialSenderNetCore
{
    public interface IOutputStream : IDisposable
    {
        void Close();
        bool IsOpen { get; }
        int BytesToWrite { get; }
        bool IsDisposed { get; }
        Task WriteAsync(byte[] buffer, int i, int bufferLength);
        void Write(string ignore);
        void Open();
        void Write(byte[] buffer, int v, int length);
    }
}