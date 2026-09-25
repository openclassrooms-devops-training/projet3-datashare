namespace DataShare.Api.Exceptions;

public class UnsupportedFileTypeException : Exception
{
    public UnsupportedFileTypeException() : base("Unsupported or inconsistent file type")
    {
    }
}
