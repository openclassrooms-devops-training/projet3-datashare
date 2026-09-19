namespace DataShare.Api.Exceptions;

public class FileTooLargeException : Exception
{
    public FileTooLargeException() : base("File exceeds the 1 GB limit")
    {
    }
}
