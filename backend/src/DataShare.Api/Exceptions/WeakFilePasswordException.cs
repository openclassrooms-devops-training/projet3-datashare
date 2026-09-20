namespace DataShare.Api.Exceptions;

public class WeakFilePasswordException : Exception
{
    public WeakFilePasswordException() : base("Password must be at least 6 characters")
    {
    }
}
