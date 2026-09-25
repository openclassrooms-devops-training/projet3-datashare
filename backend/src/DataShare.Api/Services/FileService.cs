using DataShare.Api.DTOs;
using DataShare.Api.Exceptions;
using DataShare.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataShare.Api.Services;

public class FileService : IFileService
{
    private readonly AppDbContext _dbContext;
    private readonly IDownloadToken _downloadToken;
    private readonly string _fileStoragePath; 
    private readonly IFileTypeValidationService _fileTypeValidationService;
    private readonly string _baseUrl;
    private readonly ILogger<FileService> _logger;

    public FileService(AppDbContext dbContext, IDownloadToken downloadToken, IConfiguration configuration, IFileTypeValidationService fileTypeValidationService, ILogger<FileService> logger)
    {
        _dbContext = dbContext;
        _downloadToken = downloadToken;
        _fileStoragePath = configuration["fileStorage:path"]
        ?? throw new InvalidOperationException("fileStorage:path configuration is missing");
        _baseUrl = configuration["App:BaseUrl"]
        ?? throw new InvalidOperationException("App:BaseUrl configuration is missing");
        _fileTypeValidationService = fileTypeValidationService;
        _logger = logger;
    }

    public async Task<FileResponse> UploadAsync(FileRequest request, Guid? userId)
    {
        if(request == null || request.File is null || request.File.Length == 0)
        {
            _logger.LogWarning("Upload rejected: no file provided");
            throw new MissingFileException();
        }

        var file = request.File;

        _logger.LogInformation("Upload request received: FileName={FileName}, SizeBytes={SizeBytes}", file.FileName, file.Length);

        if(file.Length > 1048576 * 1024) // 1Go  limit
        {
            _logger.LogWarning("Upload rejected: {FileName} exceeds the 1 GB limit ({SizeBytes} bytes)", file.FileName, file.Length);
            throw new FileTooLargeException();
        }

        if(request.ExpiresInDays < 1 || request.ExpiresInDays > 7)
        {
            _logger.LogWarning("Upload rejected: invalid ExpiresInDays={ExpiresInDays} for {FileName}", request.ExpiresInDays, file.FileName);
            throw new InvalidExpirationException();
        }

        if(!string.IsNullOrEmpty(request.Password) && request.Password.Length < 6)
        {
            _logger.LogWarning("Upload rejected: password too short for {FileName}", file.FileName);
            throw new WeakFilePasswordException();
        }



        //generate download token
        var downloadToken = await _downloadToken.GenerateTokenAsync(Guid.NewGuid());

        // Create the file path if not exist
        Directory.CreateDirectory(_fileStoragePath);
        var extension = Path.GetExtension(file.FileName);
        var filePath = Path.Combine(_fileStoragePath, downloadToken + extension);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {

             await file.CopyToAsync(stream);
            stream.Position = 0;

            //Ensure the Mime type is valid
            var contentType = await _fileTypeValidationService.DetectAndValidateAsync(stream, file.FileName);
            if(contentType is null)
            {
                _logger.LogWarning("Upload rejected: unsupported or inconsistent file type for {FileName}", file.FileName);
                stream.Close();
                System.IO.File.Delete(filePath);
                throw new UnsupportedFileTypeException();
            }

            _logger.LogDebug("Detected content type {ContentType} for {FileName}", contentType, file.FileName);

            //Store the file information in the database
            var fileRecord = new Models.File
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OriginalFilename = file.FileName,
                ContentType = contentType,
                StoragePath = filePath,
                SizeBytes = file.Length,
                DownloadToken = downloadToken,
                PasswordHash = string.IsNullOrEmpty(request.Password) ? null : BCrypt.Net.BCrypt.HashPassword(request.Password),
                ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays),
                CreatedAt = DateTime.UtcNow,
                Tags = request.Tags?.Select(tag => new Tag { Label = tag }).ToList() ?? new List<Tag>()

            };
        
        _dbContext.Files.Add(fileRecord);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation(
            "File uploaded: Id={FileId}, SizeBytes={SizeBytes}, ExpiresAt={ExpiresAt}",
            fileRecord.Id, fileRecord.SizeBytes, fileRecord.ExpiresAt);
        // _baseUrl pointe vers l'origine du FRONTEND (pas l'API) : le lien partage doit mener a une
        // page Angular /download/:token (US02, pas encore implementee), pas directement a l'API -
        // le telechargement reel necessite un POST (mot de passe eventuel), pas juste un lien GET.
        var downloadUrl = $"{_baseUrl}/download/{fileRecord.DownloadToken}";

        return new FileResponse
        {
            Id = fileRecord.Id,
            Filename = fileRecord.OriginalFilename,
            ContentType = fileRecord.ContentType,
            SizeBytes = fileRecord.SizeBytes,
            DownloadUrl = downloadUrl,
            HasPassword = fileRecord.PasswordHash != null,
            ExpiresAt = fileRecord.ExpiresAt,
            CreatedAt = fileRecord.CreatedAt,
            Status = fileRecord.ExpiresAt > DateTime.UtcNow ? "valid" : "expired",
            Tags = fileRecord.Tags?.Select(t => t.Label).ToList() ?? new List<string>()
        };
        }
    }


    public async Task DeleteAsync(Guid fileId, Guid userId)
    {
        // Retrieve the file record from the database
        var fileRecord = await _dbContext.Files.FirstOrDefaultAsync(f => f.Id == fileId);
        if (fileRecord == null)
        {
            throw new FileRecordNotFoundException();
        }

        // Verify that the file belongs to the user attempting to delete it
        if (fileRecord.UserId != userId)
        {
            throw new FileAccessForbiddenException();
        }

        _dbContext.Files.Remove(fileRecord);
        await _dbContext.SaveChangesAsync();

        if (System.IO.File.Exists(fileRecord.StoragePath))
        {
            System.IO.File.Delete(fileRecord.StoragePath);
        }
    }

    public async Task<List<FileResponse>> GetFilesForUserAsync(Guid userId, string status)
    {
        // Implementation for retrieving files for a user with a specific status
        var query = _dbContext.Files.Include(f => f.Tags).AsQueryable();
        query = query.Where(f => f.UserId == userId);
        if (status == "active")
        {
            query = query.Where(f => f.ExpiresAt > DateTime.UtcNow);
        }
        else if (status == "expired")
        {
            query = query.Where(f => f.ExpiresAt <= DateTime.UtcNow);
        }

        var fileRecords = await query.ToListAsync();

        return fileRecords.Select(fileRecord =>
        {
            var downloadUrl = $"{_baseUrl}/download/{fileRecord.DownloadToken}";
            return new FileResponse
            {
                Id = fileRecord.Id,
                Filename = fileRecord.OriginalFilename,
                ContentType = fileRecord.ContentType,
                SizeBytes = fileRecord.SizeBytes,
                DownloadUrl = downloadUrl,
                HasPassword = fileRecord.PasswordHash != null,
                ExpiresAt = fileRecord.ExpiresAt,
                CreatedAt = fileRecord.CreatedAt,
                Status = fileRecord.ExpiresAt > DateTime.UtcNow ? "valid" : "expired",
                Tags = fileRecord.Tags?.Select(t => t.Label).ToList() ?? new List<string>()
            };
        }).ToList();
    }

    public async Task<FileMetadataResponse> GetMetadataByTokenAsync(string token)
    {
        var fileRecord = await _dbContext.Files.FirstOrDefaultAsync(f => f.DownloadToken == token);
        if (fileRecord == null || fileRecord.ExpiresAt <= DateTime.UtcNow)
        {
            throw new FileNotFoundOrExpiredException();
        }

        return new FileMetadataResponse
        {
            Filename = fileRecord.OriginalFilename,
            ContentType = fileRecord.ContentType,
            SizeBytes = fileRecord.SizeBytes,
            ExpiresAt = fileRecord.ExpiresAt,
            RequiresPassword = fileRecord.PasswordHash != null
        };
    }

    public async Task<(Stream Content, string ContentType, string Filename)> DownloadByTokenAsync(string token, string? password)
    {
        //search for the file by token
        var fileRecord = await _dbContext.Files.FirstOrDefaultAsync(f => f.DownloadToken == token);
        if (fileRecord == null || fileRecord.ExpiresAt <= DateTime.UtcNow)
        {
            throw new FileNotFoundOrExpiredException();
        }

        //check password if required
        if (fileRecord.PasswordHash != null)
        {
            if (string.IsNullOrEmpty(password) || !VerifyPassword(password, fileRecord.PasswordHash))
            {
                throw new InvalidFilePasswordException();
            }
        }

        // Pas de using ici : le stream est renvoye a l'appelant (le controller, via File(...)),
        // qui le lit puis le dispose une fois la reponse HTTP ecrite. Un using ici le fermerait
        // avant meme que l'appelant ait pu le lire (ObjectDisposedException garantie).
        var contentStream = System.IO.File.OpenRead(fileRecord.StoragePath);
        return (contentStream, fileRecord.ContentType, fileRecord.OriginalFilename);
    }

    private bool VerifyPassword(string password, string passwordHash)
    {
        //verify the password against the stored hash
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
