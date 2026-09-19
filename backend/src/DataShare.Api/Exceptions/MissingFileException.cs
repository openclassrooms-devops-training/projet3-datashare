namespace DataShare.Api.Exceptions;

public class MissingFileException : Exception
{
    public MissingFileException() : base("No file provided")
    {
    }
}
