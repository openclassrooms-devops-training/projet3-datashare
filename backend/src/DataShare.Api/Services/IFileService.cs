using DataShare.Api.DTOs;

namespace DataShare.Api.Services;

public interface IFileService
{
    Task<FileResponse> UploadAsync(FileRequest request, Guid? userId);
    Task<FileResponse> GetAsync(Guid fileId);
    Task DeleteAsync(Guid fileId);
}
