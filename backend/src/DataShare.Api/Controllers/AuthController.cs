using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataShare.Api.DTOs;
using DataShare.Api.Exceptions;
using DataShare.Api.Services;

namespace DataShare.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var tokenResponse = await _authService.LoginAsync(request);
            return Ok(tokenResponse);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new ErrorResponse { Message = ex.Message, Code = "INVALID_CREDENTIALS" });
        }
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var userResponse = await _authService.RegisterAsync(request);
            return StatusCode(201, userResponse);
        }
        catch (EmailAlreadyUsedException ex)
        {
            return BadRequest(new ErrorResponse { Message = ex.Message, Code = "EMAIL_ALREADY_USED" });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> RefreshToken([FromBody] RefreshRequest request)
    {
        try
        {
            var tokenResponse = await _authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(tokenResponse);
        }
        catch (InvalidRefreshTokenException ex)
        {
            return Unauthorized(new ErrorResponse { Message = ex.Message, Code = "INVALID_REFRESH_TOKEN" });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        try
        {
            await _authService.LogoutAsync(request.RefreshToken);
            return NoContent();
        }
        catch (InvalidRefreshTokenException ex)
        {
            return Unauthorized(new ErrorResponse { Message = ex.Message, Code = "INVALID_REFRESH_TOKEN" });
        }
    }
}
