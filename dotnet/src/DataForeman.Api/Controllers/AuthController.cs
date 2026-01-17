using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DataForeman.Api.Services;
using DataForeman.Shared.DTOs;

namespace DataForeman.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        
        var result = await _authService.LoginAsync(request.Email, request.Password, userAgent, ip);
        
        if (result == null)
        {
            return Unauthorized(new { error = "invalid_credentials" });
        }
        
        return Ok(new { token = result.Token, refresh = result.Refresh, role = result.Role });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _authService.RefreshAsync(request.Refresh);
        
        if (result == null)
        {
            return Unauthorized(new { error = "invalid_refresh" });
        }
        
        return Ok(new { token = result.Token, refresh = result.Refresh });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = userIdClaim != null ? Guid.Parse(userIdClaim) : null;
        
        await _authService.LogoutAsync(request.Refresh, userId);
        
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst("role")?.Value;
        
        return Ok(new { sub = userId, role });
    }
}

public record LogoutRequest(string Refresh);
