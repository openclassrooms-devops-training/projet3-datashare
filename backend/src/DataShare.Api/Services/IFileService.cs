using DataShare.Api.DTOs;

namespace DataShare.Api.Services;

public interface IFileService
{
    Task<FileResponse> UploadAsync(FileRequest request, Guid? userId);
    Task<List<FileResponse>> GetFilesForUserAsync(Guid userId, string status); // US05 - a ajouter
    Task DeleteAsync(Guid fileId, Guid userId);                          // US06 - a completer (userId ajoute)
    Task<FileMetadataResponse> GetMetadataByTokenAsync(string token);    // US02 - a ajouter
    Task<(Stream Content, string ContentType, string Filename)> DownloadByTokenAsync(string token, string? password); // US02 - a ajouter
}
