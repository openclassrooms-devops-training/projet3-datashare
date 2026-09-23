namespace DataShare.Api.Exceptions;

public class InvalidFilePasswordException : Exception
{
    public InvalidFilePasswordException() : base("Invalid password for this file")
    {
    }
}
