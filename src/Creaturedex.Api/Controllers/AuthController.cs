using Creaturedex.Api.Services;
using Creaturedex.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, UserRepository userRepo) : ControllerBase
{
    public record LoginRequest(string Username, string Password);
    public record SetupRequest(string Username, string Password, string DisplayName);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (user, token) = await authService.LoginAsync(request.Username, request.Password);
        if (user == null || token == null)
            return Unauthorized(new { error = "Invalid username or password" });

        Response.Cookies.Append("creaturedex_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Set true in production with HTTPS
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(7),
            Path = "/"
        });

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

    /// <summary>
    /// One-time setup endpoint to create initial admin accounts.
    /// Only works when no users exist in the database.
    /// </summary>
    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request)
    {
        var count = await userRepo.CountAsync();
        if (count > 0)
            return BadRequest(new { error = "Setup already completed. Users already exist." });

        var id = await authService.CreateUserAsync(request.Username, request.Password, request.DisplayName);
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
