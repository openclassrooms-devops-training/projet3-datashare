namespace DataShare.Api.Exceptions;

public class FileRecordNotFoundException : Exception
{
    public FileRecordNotFoundException() : base("File not found")
    {
    }
}
