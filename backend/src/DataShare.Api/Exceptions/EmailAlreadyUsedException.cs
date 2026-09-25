namespace DataShare.Api.Exceptions;

public class EmailAlreadyUsedException : Exception
{
    public EmailAlreadyUsedException() : base("Email already in use")
    {
    }
}
