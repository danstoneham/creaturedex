using Creaturedex.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, IWebHostEnvironment env) : ControllerBase
{
    public record LoginRequest(string Username, string Password);
    public record SetupRequest(string Username, string Password, string DisplayName);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (user, token) = await authService.LoginAsync(request.Username, request.Password);
        if (user == null || token == null)
            return Unauthorized(new { error = "Invalid username or password" });

        Response.Cookies.Append("creaturedex_token", token, authService.GetAuthCookieOptions(env.IsDevelopment()));

        return Ok(new { id = user.Id, username = user.Username, displayName = user.DisplayName, role = user.Role });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("creaturedex_token", new CookieOptions { Path = "/" });
        return Ok(new { message = "Logged out" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var id))
            return Unauthorized();

        var user = await authService.GetUserByIdAsync(id);
        if (user == null) return Unauthorized();

        return Ok(new { id = user.Id, username = user.Username, displayName = user.DisplayName, role = user.Role });
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request)
    {
        var id = await authService.SetupAsync(request.Username, request.Password, request.DisplayName);
        if (id == null)
            return BadRequest(new { error = "Setup already completed. Users already exist." });

        return Ok(new { id, message = $"Admin account '{request.Username}' created" });
    }

    /// <summary>
    /// Create additional admin accounts. Requires authentication.
    /// </summary>
    [Authorize]
    [HttpPost("create-user")]
    public async Task<IActionResult> CreateUser([FromBody] SetupRequest request)
    {
        var id = await authService.CreateUserAsync(request.Username, request.Password, request.DisplayName);
        return Ok(new { id, message = $"Account '{request.Username}' created" });
    }
}
