using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Creaturedex.Core.Entities;
using Creaturedex.Data.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace Creaturedex.Api.Services;

public class AuthService(UserRepository userRepo, IConfiguration config)
{
    public async Task<(User? User, string? Token)> LoginAsync(string username, string password)
    {
        var user = await userRepo.GetByUsernameAsync(username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (null, null);

        var token = GenerateToken(user);
        return (user, token);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await userRepo.GetByIdAsync(userId);
    }

    public async Task<Guid> CreateUserAsync(string username, string password, string displayName)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = new User
        {
            Username = username,
            PasswordHash = hash,
            DisplayName = displayName
        };
        return await userRepo.CreateAsync(user);
    }

    public async Task<Guid?> SetupAsync(string username, string password, string displayName)
    {
        var count = await userRepo.CountAsync();
        if (count > 0) return null;

        return await CreateUserAsync(username, password, displayName);
    }

    public CookieOptions GetAuthCookieOptions(bool isDevelopment) => new()
    {
        HttpOnly = true,
        Secure = !isDevelopment,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromDays(7),
        Path = "/"
    };

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("displayName", user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"] ?? "Creaturedex",
            audience: config["Jwt:Audience"] ?? "Creaturedex",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
