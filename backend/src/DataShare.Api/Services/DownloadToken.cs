public class DownloadToken : IDownloadToken
{
    public async Task<string> GenerateTokenAsync(Guid fileId)
    {
        // Implementation for generating a download token
        return Guid.NewGuid().ToString();
    }
}


