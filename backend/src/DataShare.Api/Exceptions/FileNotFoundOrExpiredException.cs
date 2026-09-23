namespace DataShare.Api.Exceptions;

public class FileNotFoundOrExpiredException : Exception
{
    public FileNotFoundOrExpiredException() : base("File not found or expired")
    {
    }
}
