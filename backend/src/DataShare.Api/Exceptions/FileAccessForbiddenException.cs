namespace DataShare.Api.Exceptions;

public class FileAccessForbiddenException : Exception
{
    public FileAccessForbiddenException() : base("You do not have permission to access this file")
    {
    }
}
