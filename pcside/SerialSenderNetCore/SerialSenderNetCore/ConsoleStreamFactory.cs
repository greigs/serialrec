using System.Threading.Tasks;

namespace SerialSenderNetCore
{
    public class ConsoleStreamFactory : IStreamFactory
    {
        private static IOutputStream outputStreamInstance;
        private static IInputStream inputStreamInstance;

        public IOutputStream OutputStream
        {
            get
            {
                if (outputStreamInstance == null)
                {
                    return CreateNewOutputStream();
                }
                return outputStreamInstance;
            }
        }

        public IInputStream InputStream
        {
            get
            {
                if (inputStreamInstance == null)
                {
                    return CreateNewInputStream();
                }
                return inputStreamInstance;
            }
        }


        public IOutputStream CreateNewOutputStream()
        {
            outputStreamInstance = new ConsoleOutputStream();
            return outputStreamInstance;
        }

        public IInputStream CreateNewInputStream()
        {
            inputStreamInstance = new ConsoleInputStream();
            return inputStreamInstance;
        }
    }

    public class ConsoleInputStream : IInputStream
    {
        public int BytesToRead { get; }
        public async Task ReadAsync(byte[] buffer, int i, int charsToRead)
        {
            
        }

        public string ReadExisting()
        {
            return null;
        }

        public int ReadChar()
        {
            return 0;
        }
    }
}