namespace SerialSenderNetCore
{
    public class NamedPipeWindowsStreamFactory : IStreamFactory
    {
        public IOutputStream OutputStream { get; }
        public IInputStream InputStream { get; }

        public IOutputStream CreateNewOutputStream()
        {
            return new NamedPipeWindowsOutputStream();
        }

        public IInputStream CreateNewInputStream()
        {
            throw new System.NotImplementedException();
        }
    }
}