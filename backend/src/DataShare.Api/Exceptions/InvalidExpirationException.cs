namespace DataShare.Api.Exceptions;

public class InvalidExpirationException : Exception
{
    public InvalidExpirationException() : base("Expiration must be between 1 and 7 days")
    {
    }
}
