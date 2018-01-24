using System.Threading.Tasks;

namespace SerialSenderNetCore
{
    public interface IInputStream
    {
        int BytesToRead { get; }
        Task ReadAsync(byte[] buffer, int i, int charsToRead);
        string ReadExisting();
        int ReadChar();
    }
}