

public interface IDownloadToken
{
    Task<string> GenerateTokenAsync(Guid fileId);
}


