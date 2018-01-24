namespace SerialSenderNetCore
{

    public interface IStreamFactory
    {
        IOutputStream OutputStream { get; }
        IInputStream InputStream { get; }

        IOutputStream CreateNewOutputStream();
        IInputStream CreateNewInputStream();
    }
}
