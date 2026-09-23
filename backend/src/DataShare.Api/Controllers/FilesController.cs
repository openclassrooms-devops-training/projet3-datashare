using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataShare.Api.DTOs;
using DataShare.Api.Exceptions;
using DataShare.Api.Services;

namespace DataShare.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<FileResponse>> UploadFile([FromForm] FileRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        try
        {
            var fileResponse = await _fileService.UploadAsync(request, userId);
            return StatusCode(201, fileResponse);
        }
        catch (MissingFileException ex)
        {
            return BadRequest(new ErrorResponse { Message = ex.Message, Code = "MISSING_FILE" });
        }
        catch (FileTooLargeException ex)
        {
            return BadRequest(new ErrorResponse { Message = ex.Message, Code = "FILE_TOO_LARGE" });
        }
        catch (InvalidExpirationException ex)
        {
            return BadRequest(new ErrorResponse { Message = ex.Message, Code = "INVALID_EXPIRATION" });
        }
        catch (WeakFilePasswordException ex)
        {
            return BadRequest(new ErrorResponse { Message = ex.Message, Code = "WEAK_FILE_PASSWORD" });
        }
        catch (UnsupportedFileTypeException ex)
        {
            return BadRequest(new ErrorResponse { Message = ex.Message, Code = "UNSUPPORTED_FILE_TYPE" });
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<FileResponse>>> GetFiles([FromQuery] string status = "all")
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var files = await _fileService.GetFilesForUserAsync(userId, status);
        return Ok(files);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        try
        {
            await _fileService.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (FileRecordNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Message = ex.Message, Code = "FILE_NOT_FOUND" });
        }
        catch (FileAccessForbiddenException ex)
        {
            return StatusCode(403, new ErrorResponse { Message = ex.Message, Code = "FILE_ACCESS_FORBIDDEN" });
        }
    }

    [HttpGet("download/{token}")]
    public async Task<ActionResult<FileMetadataResponse>> GetFileMetadata(string token)
    {
        try
        {
            var metadata = await _fileService.GetMetadataByTokenAsync(token);
            return Ok(metadata);
        }
        catch (FileNotFoundOrExpiredException ex)
        {
            return NotFound(new ErrorResponse { Message = ex.Message, Code = "FILE_NOT_FOUND_OR_EXPIRED" });
        }
    }

    [HttpPost("download/{token}")]
    public async Task<IActionResult> DownloadFile(string token, [FromBody] DownloadRequest? request)
    {
        try
        {
            var (content, contentType, filename) = await _fileService.DownloadByTokenAsync(token, request?.Password);
            return File(content, contentType, filename);
        }
        catch (FileNotFoundOrExpiredException ex)
        {
            return NotFound(new ErrorResponse { Message = ex.Message, Code = "FILE_NOT_FOUND_OR_EXPIRED" });
        }
        catch (InvalidFilePasswordException ex)
        {
            return Unauthorized(new ErrorResponse { Message = ex.Message, Code = "INVALID_FILE_PASSWORD" });
        }
    }
}
