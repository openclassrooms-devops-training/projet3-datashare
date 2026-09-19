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
}
