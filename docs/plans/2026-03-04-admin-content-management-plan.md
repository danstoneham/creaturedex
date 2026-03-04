# Admin Content Management Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add authentication, inline content editing, AI generation, image upload, and AI review to Creaturedex so admins can manage animal content from within the site.

**Architecture:** Cookie-based JWT auth on .NET 10 backend, inline editing on Next.js 16 frontend. Admin controls overlay public pages when logged in. AI review via Ollama returns structured suggestions with accept/dismiss.

**Tech Stack:** .NET 10 (ASP.NET Core, Dapper, BCrypt.Net, JWT), Next.js 16 (React 19, TypeScript, Tailwind CSS v4), Ollama gpt-oss:20b, SQL Server LocalDB

---

## Task 1: Database — Users Table & Migration

**Files:**
- Create: `src/Creaturedex.Database/Tables/Users.sql`
- Create: `src/Creaturedex.Data/Scripts/002_CreateUsersTable.sql`
- Create: `src/Creaturedex.Core/Entities/User.cs`

**Step 1: Create the DACPAC schema file**

Create `src/Creaturedex.Database/Tables/Users.sql`:

```sql
CREATE TABLE [dbo].[Users] (
    [Id]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Username]     NVARCHAR(50)     NOT NULL,
    [PasswordHash] NVARCHAR(255)    NOT NULL,
    [DisplayName]  NVARCHAR(100)    NOT NULL,
    [Role]         NVARCHAR(50)     NOT NULL DEFAULT 'Admin',
    [CreatedAt]    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Users_Username] UNIQUE ([Username])
);
```

**Step 2: Create the DbUp migration script**

Create `src/Creaturedex.Data/Scripts/002_CreateUsersTable.sql`:

```sql
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [Username]     NVARCHAR(50)     NOT NULL,
        [PasswordHash] NVARCHAR(255)    NOT NULL,
        [DisplayName]  NVARCHAR(100)    NOT NULL,
        [Role]         NVARCHAR(50)     NOT NULL DEFAULT 'Admin',
        [CreatedAt]    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt]    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Users_Username] UNIQUE ([Username])
    );
END
```

Note: Do NOT seed user accounts in the migration. Users will be created via a one-time setup endpoint or CLI command with BCrypt hashing. Plain-text passwords must never appear in migration scripts.

**Step 3: Create the User entity**

Create `src/Creaturedex.Core/Entities/User.cs`:

```csharp
namespace Creaturedex.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Step 4: Build and verify**

Run: `dotnet build` from solution root
Expected: Build succeeds, migration script is embedded in Creaturedex.Data assembly

**Step 5: Commit**

```bash
git add src/Creaturedex.Database/Tables/Users.sql src/Creaturedex.Data/Scripts/002_CreateUsersTable.sql src/Creaturedex.Core/Entities/User.cs
git commit -m "feat: add Users table schema, migration, and entity"
```

---

## Task 2: Backend — User Repository & Auth Service

**Files:**
- Create: `src/Creaturedex.Data/Repositories/UserRepository.cs`
- Create: `src/Creaturedex.Api/Services/AuthService.cs`
- Modify: `src/Creaturedex.Api/Creaturedex.Api.csproj` (add BCrypt NuGet)

**Step 1: Add BCrypt NuGet package**

Run from `src/Creaturedex.Api/`:
```bash
dotnet add package BCrypt.Net-Next
```

**Step 2: Create UserRepository**

Create `src/Creaturedex.Data/Repositories/UserRepository.cs`:

```csharp
using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class UserRepository(DbConnectionFactory db)
{
    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Username = @Username",
            new { Username = username });
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<Guid> CreateAsync(User user)
    {
        using var conn = db.CreateConnection();
        user.Id = user.Id == Guid.Empty ? Guid.NewGuid() : user.Id;
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await conn.ExecuteAsync("""
            INSERT INTO Users (Id, Username, PasswordHash, DisplayName, Role, CreatedAt, UpdatedAt)
            VALUES (@Id, @Username, @PasswordHash, @DisplayName, @Role, @CreatedAt, @UpdatedAt)
            """, user);

        return user.Id;
    }

    public async Task<int> CountAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users");
    }
}
```

**Step 3: Create AuthService**

Create `src/Creaturedex.Api/Services/AuthService.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Creaturedex.Core.Entities;
using Creaturedex.Data.Repositories;
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
```

**Step 4: Build and verify**

Run: `dotnet build`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add src/Creaturedex.Api/Creaturedex.Api.csproj src/Creaturedex.Data/Repositories/UserRepository.cs src/Creaturedex.Api/Services/AuthService.cs
git commit -m "feat: add UserRepository and AuthService with BCrypt + JWT"
```

---

## Task 3: Backend — Auth Controller & JWT Middleware

**Files:**
- Create: `src/Creaturedex.Api/Controllers/AuthController.cs`
- Modify: `src/Creaturedex.Api/Program.cs` (add JWT auth middleware, register services)
- Modify: `src/Creaturedex.Api/appsettings.json` (add JWT config)
- Modify: `src/Creaturedex.Api/Creaturedex.Api.csproj` (add JWT NuGet)
- Modify: `src/Creaturedex.Api/Controllers/AdminController.cs` (add `[Authorize]`)

**Step 1: Add JWT NuGet package**

Run from `src/Creaturedex.Api/`:
```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

**Step 2: Add JWT config to appsettings.json**

Add to `src/Creaturedex.Api/appsettings.json` at root level:

```json
"Jwt": {
    "Key": "creaturedex-dev-secret-key-change-in-production-min-32-chars!",
    "Issuer": "Creaturedex",
    "Audience": "Creaturedex"
}
```

**Step 3: Create AuthController**

Create `src/Creaturedex.Api/Controllers/AuthController.cs`:

```csharp
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
```

**Step 4: Configure JWT middleware in Program.cs**

Add to `src/Creaturedex.Api/Program.cs` after the AI service registrations (~line 56), before `AddCors`:

```csharp
// Authentication
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key not configured");
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Creaturedex",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Creaturedex",
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtKey))
        };
        // Read JWT from cookie
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies["creaturedex_token"];
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
```

Add after `app.UseCors();` (~line 95):

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

**Step 5: Add `[Authorize]` to AdminController**

Modify `src/Creaturedex.Api/Controllers/AdminController.cs`:
- Add `using Microsoft.AspNetCore.Authorization;`
- Add `[Authorize]` attribute to the class (line 10, before `[Route("api/admin")]`)

**Step 6: Build and verify**

Run: `dotnet build`
Expected: Build succeeds

**Step 7: Commit**

```bash
git add src/Creaturedex.Api/Controllers/AuthController.cs src/Creaturedex.Api/Program.cs src/Creaturedex.Api/appsettings.json src/Creaturedex.Api/Creaturedex.Api.csproj src/Creaturedex.Api/Controllers/AdminController.cs
git commit -m "feat: add JWT auth middleware, AuthController, and protect admin endpoints"
```

---

## Task 4: Backend — Admin Animal Update & Image Upload Endpoints

**Files:**
- Modify: `src/Creaturedex.Api/Controllers/AdminController.cs` (add update, image upload, tag update endpoints)
- Modify: `src/Creaturedex.Api/Controllers/AnimalsController.cs` (auth-aware slug lookup)
- Modify: `src/Creaturedex.Api/Services/AnimalService.cs` (add auth-aware methods)
- Modify: `src/Creaturedex.Data/Repositories/AnimalRepository.cs` (add GetBySlugIncludingUnpublished)
- Create: `src/Creaturedex.Shared/Requests/UpdateAnimalRequest.cs`

**Step 1: Create UpdateAnimalRequest**

Create `src/Creaturedex.Shared/Requests/UpdateAnimalRequest.cs`:

```csharp
namespace Creaturedex.Shared.Requests;

public class UpdateAnimalRequest
{
    public string CommonName { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public bool IsPet { get; set; }
    public string? ConservationStatus { get; set; }
    public string? NativeRegion { get; set; }
    public string? Habitat { get; set; }
    public string? Diet { get; set; }
    public string? Lifespan { get; set; }
    public string? SizeInfo { get; set; }
    public string? Behaviour { get; set; }
    public string? FunFacts { get; set; }
    public List<string> Tags { get; set; } = [];
}
```

**Step 2: Add GetBySlugIncludingUnpublished to AnimalRepository**

Add to `src/Creaturedex.Data/Repositories/AnimalRepository.cs`:

```csharp
public async Task<Animal?> GetBySlugIncludingUnpublishedAsync(string slug)
{
    using var conn = db.CreateConnection();
    return await conn.QuerySingleOrDefaultAsync<Animal>(
        "SELECT * FROM Animals WHERE Slug = @Slug AND DeletedAt IS NULL",
        new { Slug = slug });
}
```

**Step 3: Add auth-aware GetBySlug to AnimalService**

Add to `src/Creaturedex.Api/Services/AnimalService.cs`:

```csharp
public async Task<AnimalProfileResponse?> GetBySlugAsync(string slug, bool includeUnpublished)
{
    var animal = includeUnpublished
        ? await animalRepo.GetBySlugIncludingUnpublishedAsync(slug)
        : await animalRepo.GetBySlugAsync(slug);
    if (animal == null) return null;

    // ... same composition logic as existing GetBySlugAsync
}
```

Refactor the existing `GetBySlugAsync` to call the new overload with `includeUnpublished: false`.

**Step 4: Modify AnimalsController for auth-aware slug lookup**

Modify `src/Creaturedex.Api/Controllers/AnimalsController.cs` `GetBySlug` method:

```csharp
[HttpGet("{slug}")]
public async Task<IActionResult> GetBySlug(string slug)
{
    var isAuthenticated = User.Identity?.IsAuthenticated == true;
    var animal = await animalService.GetBySlugAsync(slug, includeUnpublished: isAuthenticated);
    if (animal == null) return NotFound();
    return Ok(animal);
}
```

**Step 5: Add admin endpoints to AdminController**

Add these endpoints to `src/Creaturedex.Api/Controllers/AdminController.cs`:

```csharp
[HttpPut("animals/{id:guid}")]
public async Task<IActionResult> UpdateAnimal(Guid id, [FromBody] UpdateAnimalRequest request)
{
    var animal = await animalRepo.GetByIdAsync(id);
    if (animal == null) return NotFound();

    animal.CommonName = request.CommonName;
    animal.ScientificName = request.ScientificName;
    animal.Summary = request.Summary;
    animal.Description = request.Description;
    animal.CategoryId = request.CategoryId;
    animal.IsPet = request.IsPet;
    animal.ConservationStatus = request.ConservationStatus;
    animal.NativeRegion = request.NativeRegion;
    animal.Habitat = request.Habitat;
    animal.Diet = request.Diet;
    animal.Lifespan = request.Lifespan;
    animal.SizeInfo = request.SizeInfo;
    animal.Behaviour = request.Behaviour;
    animal.FunFacts = request.FunFacts;
    animal.ReviewedBy = User.Identity?.Name;

    await animalRepo.UpdateAsync(animal);

    // Update tags
    await tagRepo.DeleteByAnimalIdAsync(animal.Id);
    if (request.Tags.Count > 0)
    {
        var tags = request.Tags.Select(t => new AnimalTag { AnimalId = animal.Id, Tag = t }).ToList();
        await tagRepo.BulkInsertAsync(tags);
    }

    return Ok(new { message = "Updated", id = animal.Id });
}

[HttpPost("animals/{id:guid}/image/upload")]
public async Task<IActionResult> UploadImage(Guid id, IFormFile file, [FromServices] AIConfig aiCfg)
{
    var animal = await animalRepo.GetByIdAsync(id);
    if (animal == null) return NotFound();

    if (file.Length == 0) return BadRequest(new { error = "No file provided" });

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
        return BadRequest(new { error = "Only PNG, JPG, and WebP images are allowed" });

    var fileName = $"{animal.Slug}{ext}";
    var storagePath = Path.Combine(AppContext.BaseDirectory, aiCfg.ImageStoragePath);
    Directory.CreateDirectory(storagePath);
    var filePath = Path.Combine(storagePath, fileName);

    await using var stream = new FileStream(filePath, FileMode.Create);
    await file.CopyToAsync(stream);

    var imageUrl = $"/images/animals/{fileName}";
    await animalRepo.UpdateImageUrlAsync(id, imageUrl);

    return Ok(new { imageUrl });
}
```

**Step 6: Add DeleteByAnimalIdAsync to TagRepository**

Add to `src/Creaturedex.Data/Repositories/TagRepository.cs`:

```csharp
public async Task DeleteByAnimalIdAsync(Guid animalId)
{
    using var conn = db.CreateConnection();
    await conn.ExecuteAsync("DELETE FROM AnimalTags WHERE AnimalId = @AnimalId", new { AnimalId = animalId });
}
```

Note: Add `TagRepository` to AdminController's constructor parameters and add `using Creaturedex.Core.Entities;` for `AnimalTag`.

**Step 7: Build and verify**

Run: `dotnet build`
Expected: Build succeeds

**Step 8: Commit**

```bash
git add -A
git commit -m "feat: add admin animal update, image upload, and auth-aware slug lookup"
```

---

## Task 5: Backend — AI Content Review Service & Endpoint

**Files:**
- Create: `src/Creaturedex.AI/Services/ContentReviewService.cs`
- Create: `src/Creaturedex.Shared/Responses/ReviewSuggestionResponse.cs`
- Modify: `src/Creaturedex.Api/Controllers/AdminController.cs` (add review endpoint)
- Modify: `src/Creaturedex.Api/Program.cs` (register ContentReviewService)

**Step 1: Create ReviewSuggestionResponse**

Create `src/Creaturedex.Shared/Responses/ReviewSuggestionResponse.cs`:

```csharp
namespace Creaturedex.Shared.Responses;

public class ReviewSuggestionResponse
{
    public List<ReviewSuggestion> Suggestions { get; set; } = [];
}

public class ReviewSuggestion
{
    public string Field { get; set; } = string.Empty;
    public string Severity { get; set; } = "info"; // "info" or "warning"
    public string Message { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string SuggestedValue { get; set; } = string.Empty;
}
```

**Step 2: Create ContentReviewService**

Create `src/Creaturedex.AI/Services/ContentReviewService.cs`:

```csharp
using System.Text.Json;
using Creaturedex.Core.Entities;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public class ContentReviewService(AIService aiService, ILogger<ContentReviewService> logger)
{
    private const string ReviewPrompt = """
        You are a content reviewer for an animal encyclopedia called Creaturedex.
        The audience is teenagers and adults without a scientific background.

        Review the following animal profile and suggest improvements. For each suggestion:
        - Identify the field name (e.g. "summary", "description", "habitat", "diet", etc.)
        - Set severity to "warning" for factual errors or serious issues, "info" for style/completeness improvements
        - Provide a brief message explaining WHY the change is needed
        - Provide the current value of the field
        - Provide your suggested replacement text for that field

        Check for:
        1. Factual accuracy — are the facts correct?
        2. Completeness — are any sections thin or missing important info?
        3. Tone — is it accessible and age-appropriate? Not too technical, not condescending?
        4. Consistency — do fields contradict each other?
        5. Engagement — is it interesting and well-written?

        If the content is good and needs no changes, return an empty suggestions array.

        Respond with ONLY valid JSON matching this schema:
        {
          "suggestions": [
            {
              "field": "fieldName",
              "severity": "info" or "warning",
              "message": "why this change is needed",
              "currentValue": "the current text",
              "suggestedValue": "your improved text"
            }
          ]
        }

        Respond with ONLY the JSON, no markdown fences or extra text.
        """;

    public async Task<List<ReviewSuggestionDto>> ReviewAnimalAsync(Animal animal, List<string> tags, CancellationToken ct = default)
    {
        var profileText = $"""
            Common Name: {animal.CommonName}
            Scientific Name: {animal.ScientificName ?? "N/A"}
            Summary: {animal.Summary}
            Description: {animal.Description}
            Habitat: {animal.Habitat ?? "N/A"}
            Diet: {animal.Diet ?? "N/A"}
            Lifespan: {animal.Lifespan ?? "N/A"}
            Size Info: {animal.SizeInfo ?? "N/A"}
            Behaviour: {animal.Behaviour ?? "N/A"}
            Native Region: {animal.NativeRegion ?? "N/A"}
            Conservation Status: {animal.ConservationStatus ?? "N/A"}
            Fun Facts: {animal.FunFacts ?? "N/A"}
            Tags: {string.Join(", ", tags)}
            Is Pet: {animal.IsPet}
            """;

        try
        {
            var response = await aiService.CompleteAsync(ReviewPrompt, profileText, ct);

            // Strip markdown fences if present
            response = response.Trim();
            if (response.StartsWith("```")) response = response[response.IndexOf('\n')..];
            if (response.EndsWith("```")) response = response[..response.LastIndexOf("```")];
            response = response.Trim();

            var json = JsonDocument.Parse(response);
            var suggestions = json.RootElement.GetProperty("suggestions").EnumerateArray()
                .Select(s => new ReviewSuggestionDto
                {
                    Field = s.GetProperty("field").GetString() ?? "",
                    Severity = s.GetProperty("severity").GetString() ?? "info",
                    Message = s.GetProperty("message").GetString() ?? "",
                    CurrentValue = s.GetProperty("currentValue").GetString() ?? "",
                    SuggestedValue = s.GetProperty("suggestedValue").GetString() ?? ""
                })
                .ToList();

            return suggestions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to review animal: {AnimalName}", animal.CommonName);
            return [new ReviewSuggestionDto
            {
                Field = "general",
                Severity = "warning",
                Message = $"AI review failed: {ex.Message}",
                CurrentValue = "",
                SuggestedValue = ""
            }];
        }
    }
}

public class ReviewSuggestionDto
{
    public string Field { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string SuggestedValue { get; set; } = string.Empty;
}
```

**Step 3: Add review endpoint to AdminController**

Add to `src/Creaturedex.Api/Controllers/AdminController.cs`:

```csharp
[HttpPost("animals/{id:guid}/review")]
public async Task<IActionResult> ReviewAnimal(Guid id, [FromServices] ContentReviewService reviewService, [FromServices] TagRepository tagRepoSvc, CancellationToken ct)
{
    var animal = await animalRepo.GetByIdAsync(id);
    if (animal == null) return NotFound();

    var tags = (await tagRepoSvc.GetByAnimalIdAsync(id)).Select(t => t.Tag).ToList();
    var suggestions = await reviewService.ReviewAnimalAsync(animal, tags, ct);

    return Ok(new { suggestions });
}
```

**Step 4: Register ContentReviewService in Program.cs**

Add after the existing AI service registrations (~line 48):

```csharp
builder.Services.AddScoped<ContentReviewService>();
```

**Step 5: Build and verify**

Run: `dotnet build`
Expected: Build succeeds

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add AI content review service and endpoint"
```

---

## Task 6: Backend — Modify Generate Endpoint to Skip Image

**Files:**
- Modify: `src/Creaturedex.Shared/Requests/GenerateAnimalRequest.cs` (add SkipImage)
- Modify: `src/Creaturedex.Api/Controllers/AdminController.cs` (pass SkipImage)
- Modify: `src/Creaturedex.AI/Services/ContentGeneratorService.cs` (accept skipImage param)

**Step 1: Add SkipImage to GenerateAnimalRequest**

Modify `src/Creaturedex.Shared/Requests/GenerateAnimalRequest.cs`:

```csharp
namespace Creaturedex.Shared.Requests;

public class GenerateAnimalRequest
{
    public string AnimalName { get; set; } = string.Empty;
    public string? CategorySlug { get; set; }
    public bool? IsPet { get; set; }
    public bool SkipImage { get; set; } = true;
}
```

**Step 2: Add skipImage parameter to ContentGeneratorService.GenerateAnimalAsync**

Modify `src/Creaturedex.AI/Services/ContentGeneratorService.cs` line 83:

Change signature to:
```csharp
public async Task<Guid?> GenerateAnimalAsync(string animalName, bool skipImage = false, CancellationToken ct = default)
```

Change the image generation block (lines 205-216) to:

```csharp
if (!skipImage && aiConfig.AutoGenerateImages)
{
    // ... existing image generation code
}
```

**Step 3: Update AdminController.Generate to pass SkipImage**

Modify `src/Creaturedex.Api/Controllers/AdminController.cs` line 27:

```csharp
var id = await contentGenerator.GenerateAnimalAsync(request.AnimalName, request.SkipImage, ct);
```

**Step 4: Build and verify**

Run: `dotnet build`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add skipImage parameter to animal generation"
```

---

## Task 7: Frontend — Auth Hook & Login Page

**Files:**
- Create: `src/creaturedex-web/src/lib/auth.tsx`
- Create: `src/creaturedex-web/src/app/login/page.tsx`
- Modify: `src/creaturedex-web/src/app/layout.tsx` (wrap with AuthProvider)
- Modify: `src/creaturedex-web/src/lib/api.ts` (add auth API methods)
- Modify: `src/creaturedex-web/src/lib/types.ts` (add AuthUser type)

**Step 1: Add AuthUser type**

Add to `src/creaturedex-web/src/lib/types.ts`:

```typescript
export interface AuthUser {
  id: string;
  username: string;
  displayName: string;
  role: string;
}
```

**Step 2: Add auth API methods**

Add to `src/creaturedex-web/src/lib/api.ts`:

```typescript
auth: {
    login: (username: string, password: string) =>
      fetchApi<AuthUser>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ username, password }),
        credentials: "include",
      }),
    logout: () =>
      fetchApi<{ message: string }>("/api/auth/logout", {
        method: "POST",
        credentials: "include",
      }),
    me: () =>
      fetchApi<AuthUser>("/api/auth/me", { credentials: "include" }),
  },
```

Also update the `fetchApi` function to include `credentials: "include"` by default:

```typescript
async function fetchApi<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    credentials: "include",
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
  });
  // ...
}
```

**Step 3: Create auth context**

Create `src/creaturedex-web/src/lib/auth.tsx`:

```tsx
"use client";

import { createContext, useContext, useState, useEffect, useCallback } from "react";
import type { AuthUser } from "./types";
import { api } from "./api";

interface AuthContextType {
  user: AuthUser | null;
  isLoggedIn: boolean;
  loading: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType>({
  user: null,
  isLoggedIn: false,
  loading: true,
  login: async () => {},
  logout: async () => {},
});

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.auth.me()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, []);

  const login = useCallback(async (username: string, password: string) => {
    const user = await api.auth.login(username, password);
    setUser(user);
  }, []);

  const logout = useCallback(async () => {
    await api.auth.logout();
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, isLoggedIn: !!user, loading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
```

**Step 4: Create login page**

Create `src/creaturedex-web/src/app/login/page.tsx`:

```tsx
"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";

export default function LoginPage() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const router = useRouter();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await login(username, password);
      router.push("/");
    } catch {
      setError("Invalid username or password");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-[60vh] flex items-center justify-center px-4">
      <div className="w-full max-w-sm">
        <h1 className="text-2xl font-bold text-text text-center mb-8">Sign in to Creaturedex</h1>
        <form onSubmit={handleSubmit} className="space-y-4">
          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-3 text-sm text-red-800">
              {error}
            </div>
          )}
          <div>
            <label htmlFor="username" className="block text-sm font-medium text-text mb-1">
              Username
            </label>
            <input
              id="username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:ring-primary focus:border-primary"
              required
              autoFocus
            />
          </div>
          <div>
            <label htmlFor="password" className="block text-sm font-medium text-text mb-1">
              Password
            </label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:ring-primary focus:border-primary"
              required
            />
          </div>
          <button
            type="submit"
            disabled={loading}
            className="w-full bg-primary text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-primary/90 disabled:opacity-50 transition-colors"
          >
            {loading ? "Signing in..." : "Sign in"}
          </button>
        </form>
      </div>
    </div>
  );
}
```

**Step 5: Wrap layout with AuthProvider**

Modify `src/creaturedex-web/src/app/layout.tsx` to wrap children:

```tsx
import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import Header from "@/components/layout/Header";
import Footer from "@/components/layout/Footer";
import { AuthProvider } from "@/lib/auth";

// ... existing font setup ...

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body className={`${geistSans.variable} ${geistMono.variable} antialiased`}>
        <AuthProvider>
          <Header />
          <main>{children}</main>
          <Footer />
        </AuthProvider>
      </body>
    </html>
  );
}
```

Note: Check if Header and Footer are already in layout.tsx or in individual pages. If they're in pages, move them to layout. If layout already includes them, just add the AuthProvider wrapper.

**Step 6: Add login/logout to Header**

Modify `src/creaturedex-web/src/components/layout/Header.tsx`:

Add after the search icon, before the mobile menu button:

```tsx
import { useAuth } from "@/lib/auth";
import Link from "next/link";

// Inside Header component:
const { user, isLoggedIn, logout } = useAuth();

// In the JSX, add before mobile menu button:
{isLoggedIn ? (
  <div className="hidden md:flex items-center gap-3">
    <span className="text-sm text-text-muted">{user?.displayName}</span>
    <button
      onClick={logout}
      className="text-sm text-text-muted hover:text-primary transition-colors"
    >
      Logout
    </button>
  </div>
) : (
  <Link
    href="/login"
    className="hidden md:block text-sm text-text-muted hover:text-primary transition-colors"
  >
    Login
  </Link>
)}
```

**Step 7: Verify frontend builds**

Run from `src/creaturedex-web/`:
```bash
npm run build
```
Expected: Build succeeds

**Step 8: Commit**

```bash
git add -A
git commit -m "feat: add auth context, login page, and header login/logout"
```

---

## Task 8: Frontend — Admin API Client Methods

**Files:**
- Modify: `src/creaturedex-web/src/lib/api.ts` (add admin methods)
- Modify: `src/creaturedex-web/src/lib/types.ts` (add admin types)

**Step 1: Add admin types**

Add to `src/creaturedex-web/src/lib/types.ts`:

```typescript
export interface UpdateAnimalRequest {
  commonName: string;
  scientificName: string | null;
  summary: string;
  description: string;
  categoryId: string;
  isPet: boolean;
  conservationStatus: string | null;
  nativeRegion: string | null;
  habitat: string | null;
  diet: string | null;
  lifespan: string | null;
  sizeInfo: string | null;
  behaviour: string | null;
  funFacts: string | null;
  tags: string[];
}

export interface ReviewSuggestion {
  field: string;
  severity: "info" | "warning";
  message: string;
  currentValue: string;
  suggestedValue: string;
}
```

**Step 2: Add admin API methods**

Add to the `api` object in `src/creaturedex-web/src/lib/api.ts`:

```typescript
admin: {
    updateAnimal: (id: string, data: UpdateAnimalRequest) =>
      fetchApi<{ message: string; id: string }>(`/api/admin/animals/${id}`, {
        method: "PUT",
        body: JSON.stringify(data),
      }),
    uploadImage: async (id: string, file: File) => {
      const formData = new FormData();
      formData.append("file", file);
      const res = await fetch(`${API_BASE}/api/admin/animals/${id}/image/upload`, {
        method: "POST",
        body: formData,
        credentials: "include",
      });
      if (!res.ok) throw new Error(`Upload failed: ${res.status}`);
      return res.json() as Promise<{ imageUrl: string }>;
    },
    generateImage: (id: string) =>
      fetchApi<{ imageUrl: string; animalName: string }>(`/api/admin/image/generate/${id}`, {
        method: "POST",
      }),
    generateAnimal: (animalName: string) =>
      fetchApi<{ id: string; message: string }>("/api/admin/generate", {
        method: "POST",
        body: JSON.stringify({ animalName, skipImage: true }),
      }),
    reviewAnimal: (id: string) =>
      fetchApi<{ suggestions: ReviewSuggestion[] }>(`/api/admin/animals/${id}/review`, {
        method: "POST",
      }),
    publishAnimal: (id: string) =>
      fetchApi<{ message: string }>(`/api/admin/publish/${id}`, {
        method: "PUT",
      }),
    unpublishAnimal: (id: string) =>
      fetchApi<{ message: string }>(`/api/admin/animals/${id}`, {
        method: "PUT",
        body: JSON.stringify({ isPublished: false }),
      }),
  },
```

Note: For unpublish, we may need a dedicated endpoint. Consider adding `PUT /api/admin/animals/{id}/unpublish` to AdminController if the generic update doesn't handle publish status separately.

**Step 3: Verify frontend builds**

Run: `npm run build` from `src/creaturedex-web/`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add admin API client methods and types"
```

---

## Task 9: Frontend — Edit Toolbar Component

**Files:**
- Create: `src/creaturedex-web/src/components/admin/EditToolbar.tsx`

**Step 1: Create EditToolbar**

Create `src/creaturedex-web/src/components/admin/EditToolbar.tsx`:

```tsx
"use client";

interface EditToolbarProps {
  isEditing: boolean;
  isPublished: boolean;
  isSaving: boolean;
  isGeneratingImage: boolean;
  isReviewing: boolean;
  onToggleEdit: () => void;
  onSave: () => void;
  onCancel: () => void;
  onGenerateImage: () => void;
  onUploadImage: () => void;
  onReview: () => void;
  onTogglePublish: () => void;
}

export default function EditToolbar({
  isEditing, isPublished, isSaving, isGeneratingImage, isReviewing,
  onToggleEdit, onSave, onCancel, onGenerateImage, onUploadImage,
  onReview, onTogglePublish,
}: EditToolbarProps) {
  return (
    <div className="bg-surface border border-gray-200 rounded-xl p-3 mb-6 flex flex-wrap items-center gap-2">
      {isEditing ? (
        <>
          <button
            onClick={onSave}
            disabled={isSaving}
            className="bg-primary text-white px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-primary/90 disabled:opacity-50"
          >
            {isSaving ? "Saving..." : "Save"}
          </button>
          <button
            onClick={onCancel}
            disabled={isSaving}
            className="bg-gray-100 text-text px-4 py-1.5 rounded-lg text-sm hover:bg-gray-200"
          >
            Cancel
          </button>
          <div className="w-px h-6 bg-gray-200 mx-1" />
        </>
      ) : (
        <button
          onClick={onToggleEdit}
          className="bg-primary/10 text-primary px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-primary/20"
        >
          Edit
        </button>
      )}

      <button
        onClick={onGenerateImage}
        disabled={isGeneratingImage}
        className="bg-secondary/10 text-secondary px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-secondary/20 disabled:opacity-50"
      >
        {isGeneratingImage ? "Generating..." : "Generate Image"}
      </button>

      <button
        onClick={onUploadImage}
        className="bg-gray-100 text-text px-4 py-1.5 rounded-lg text-sm hover:bg-gray-200"
      >
        Upload Image
      </button>

      <button
        onClick={onReview}
        disabled={isReviewing}
        className="bg-blue-50 text-blue-700 px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-blue-100 disabled:opacity-50"
      >
        {isReviewing ? "Reviewing..." : "AI Review"}
      </button>

      <div className="ml-auto">
        <button
          onClick={onTogglePublish}
          className={`px-4 py-1.5 rounded-lg text-sm font-medium ${
            isPublished
              ? "bg-red-50 text-red-700 hover:bg-red-100"
              : "bg-green-50 text-green-700 hover:bg-green-100"
          }`}
        >
          {isPublished ? "Unpublish" : "Publish"}
        </button>
      </div>
    </div>
  );
}
```

**Step 2: Commit**

```bash
git add -A
git commit -m "feat: add EditToolbar component"
```

---

## Task 10: Frontend — AI Review Panel Component

**Files:**
- Create: `src/creaturedex-web/src/components/admin/ReviewPanel.tsx`

**Step 1: Create ReviewPanel**

Create `src/creaturedex-web/src/components/admin/ReviewPanel.tsx`:

```tsx
"use client";

import type { ReviewSuggestion } from "@/lib/types";

interface ReviewPanelProps {
  suggestions: ReviewSuggestion[];
  onAccept: (suggestion: ReviewSuggestion) => void;
  onDismiss: (index: number) => void;
  onClose: () => void;
}

export default function ReviewPanel({ suggestions, onAccept, onDismiss, onClose }: ReviewPanelProps) {
  if (suggestions.length === 0) {
    return (
      <div className="bg-green-50 border border-green-200 rounded-xl p-4 mb-6">
        <div className="flex items-center justify-between">
          <p className="text-sm text-green-800 font-medium">
            No suggestions — content looks good!
          </p>
          <button onClick={onClose} className="text-green-600 hover:text-green-800 text-sm">
            Dismiss
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-blue-50 border border-blue-200 rounded-xl p-4 mb-6 space-y-3">
      <div className="flex items-center justify-between mb-2">
        <h3 className="text-sm font-semibold text-blue-900">
          AI Review — {suggestions.length} suggestion{suggestions.length !== 1 ? "s" : ""}
        </h3>
        <button onClick={onClose} className="text-blue-600 hover:text-blue-800 text-sm">
          Close
        </button>
      </div>
      {suggestions.map((s, i) => (
        <div key={i} className="bg-white rounded-lg border border-blue-100 p-3">
          <div className="flex items-start gap-2 mb-2">
            <span className={`text-xs font-medium px-1.5 py-0.5 rounded ${
              s.severity === "warning"
                ? "bg-amber-100 text-amber-800"
                : "bg-blue-100 text-blue-800"
            }`}>
              {s.severity === "warning" ? "Warning" : "Info"}
            </span>
            <span className="text-xs font-medium text-gray-500 uppercase">{s.field}</span>
          </div>
          <p className="text-sm text-gray-700 mb-2">{s.message}</p>
          {s.suggestedValue && (
            <div className="text-sm bg-green-50 border border-green-100 rounded p-2 mb-2">
              <p className="text-xs text-gray-500 mb-1">Suggested:</p>
              <p className="text-gray-800 whitespace-pre-line">{s.suggestedValue}</p>
            </div>
          )}
          <div className="flex gap-2">
            {s.suggestedValue && (
              <button
                onClick={() => onAccept(s)}
                className="text-xs bg-green-100 text-green-800 px-2 py-1 rounded hover:bg-green-200"
              >
                Accept
              </button>
            )}
            <button
              onClick={() => onDismiss(i)}
              className="text-xs bg-gray-100 text-gray-600 px-2 py-1 rounded hover:bg-gray-200"
            >
              Dismiss
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
```

**Step 2: Commit**

```bash
git add -A
git commit -m "feat: add ReviewPanel component with accept/dismiss"
```

---

## Task 11: Frontend — Inline Edit Mode on Animal Profile Page

**Files:**
- Modify: `src/creaturedex-web/src/app/animals/[slug]/page.tsx` (add edit mode, toolbar, review panel)

This is the largest frontend task. The animal profile page needs to:
1. Show EditToolbar when logged in
2. Switch between view/edit modes
3. Show editable fields in edit mode
4. Handle save, image upload/generate, AI review, publish/unpublish
5. Show draft badge for unpublished animals

**Step 1: Add edit state and handlers to the page**

This involves significant changes to `src/creaturedex-web/src/app/animals/[slug]/page.tsx`. The page needs:

- Import `useAuth`, `EditToolbar`, `ReviewPanel`, admin API methods
- Add state: `isEditing`, `editData` (copy of animal fields), `isSaving`, `isGeneratingImage`, `isReviewing`, `reviewSuggestions`
- Handler functions: `handleSave`, `handleCancel`, `handleGenerateImage`, `handleUploadImage`, `handleReview`, `handleTogglePublish`, `handleAcceptSuggestion`, `handleDismissSuggestion`
- Conditional rendering: when `isEditing`, text becomes inputs/textareas
- Hidden file input for image upload
- Draft badge when `!animal.isPublished`

Key implementation details:

```tsx
// At top of component
const { isLoggedIn } = useAuth();
const [isEditing, setIsEditing] = useState(false);
const [editData, setEditData] = useState<Record<string, any>>({});
const [isSaving, setIsSaving] = useState(false);
const [isGeneratingImage, setIsGeneratingImage] = useState(false);
const [isReviewing, setIsReviewing] = useState(false);
const [reviewSuggestions, setReviewSuggestions] = useState<ReviewSuggestion[] | null>(null);
const fileInputRef = useRef<HTMLInputElement>(null);

// Initialize editData when entering edit mode
const handleEdit = () => {
  setEditData({
    commonName: animal.commonName,
    scientificName: animal.scientificName,
    summary: animal.summary,
    description: animal.description,
    categoryId: animal.categoryId,
    isPet: animal.isPet,
    conservationStatus: animal.conservationStatus,
    nativeRegion: animal.nativeRegion,
    habitat: animal.habitat,
    diet: animal.diet,
    lifespan: animal.lifespan,
    sizeInfo: animal.sizeInfo,
    behaviour: animal.behaviour,
    funFacts: animal.funFacts,
  });
  setIsEditing(true);
};
```

For each text field, render conditionally:

```tsx
{isEditing ? (
  <textarea
    value={editData.description || ""}
    onChange={(e) => setEditData(prev => ({ ...prev, description: e.target.value }))}
    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm min-h-[120px]"
  />
) : (
  <p className="text-text leading-relaxed whitespace-pre-line">{animal.description}</p>
)}
```

The `handleAcceptSuggestion` maps suggestion field names to editData keys and applies the `suggestedValue`:

```tsx
const handleAcceptSuggestion = (suggestion: ReviewSuggestion) => {
  setEditData(prev => ({ ...prev, [suggestion.field]: suggestion.suggestedValue }));
  // Enable edit mode if not already
  if (!isEditing) handleEdit();
};
```

**Step 2: Verify frontend builds**

Run: `npm run build` from `src/creaturedex-web/`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add inline edit mode to animal profile page"
```

---

## Task 12: Frontend — Add Animal Modal on Browse Page

**Files:**
- Create: `src/creaturedex-web/src/components/admin/AddAnimalModal.tsx`
- Modify: `src/creaturedex-web/src/app/animals/page.tsx` (add button and modal)

**Step 1: Create AddAnimalModal**

Create `src/creaturedex-web/src/components/admin/AddAnimalModal.tsx`:

```tsx
"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";

interface AddAnimalModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function AddAnimalModal({ isOpen, onClose }: AddAnimalModalProps) {
  const [animalName, setAnimalName] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const router = useRouter();

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!animalName.trim()) return;

    setError("");
    setLoading(true);
    try {
      const result = await api.admin.generateAnimal(animalName.trim());
      // Redirect to the new animal's page
      // The slug is derived from the animal name — fetch it via the ID
      // For now, convert name to slug format
      const slug = animalName.trim().toLowerCase().replace(/\s+/g, "-");
      router.push(`/animals/${slug}`);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to generate animal");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 px-4">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-6">
        <h2 className="text-lg font-bold text-text mb-4">Add New Animal</h2>
        <p className="text-sm text-text-muted mb-4">
          Enter the animal name and AI will generate all the content. You can edit it afterwards.
        </p>
        <form onSubmit={handleSubmit}>
          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-3 mb-4 text-sm text-red-800">
              {error}
            </div>
          )}
          <input
            type="text"
            value={animalName}
            onChange={(e) => setAnimalName(e.target.value)}
            placeholder="e.g. Red Fox, Emperor Penguin..."
            className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm mb-4 focus:ring-primary focus:border-primary"
            disabled={loading}
            autoFocus
          />
          {loading && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 mb-4 text-sm text-blue-800">
              Generating {animalName}... This may take a minute.
            </div>
          )}
          <div className="flex gap-2 justify-end">
            <button
              type="button"
              onClick={onClose}
              disabled={loading}
              className="px-4 py-2 rounded-lg text-sm text-text-muted hover:bg-gray-100"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || !animalName.trim()}
              className="bg-primary text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-primary/90 disabled:opacity-50"
            >
              {loading ? "Generating..." : "Generate with AI"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
```

**Step 2: Add button to browse page**

Modify `src/creaturedex-web/src/app/animals/page.tsx`:

Add imports and state:
```tsx
import { useAuth } from "@/lib/auth";
import AddAnimalModal from "@/components/admin/AddAnimalModal";

// Inside BrowsePage:
const { isLoggedIn } = useAuth();
const [showAddModal, setShowAddModal] = useState(false);
```

Add button next to the heading:
```tsx
<div className="flex items-center justify-between mb-6">
  <h1 className="text-3xl font-bold text-text">Browse Animals</h1>
  {isLoggedIn && (
    <button
      onClick={() => setShowAddModal(true)}
      className="bg-primary text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-primary/90"
    >
      + Add Animal
    </button>
  )}
</div>
```

Add modal at the end of the component JSX:
```tsx
<AddAnimalModal isOpen={showAddModal} onClose={() => setShowAddModal(false)} />
```

**Step 3: Verify frontend builds**

Run: `npm run build`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add Add Animal modal to browse page"
```

---

## Task 13: Integration Testing & Verification

**Step 1: Start the backend**

```bash
cd src/Creaturedex.Api
dotnet run
```

Verify:
- `GET http://localhost:5163/health` returns healthy
- `GET http://localhost:5163/health/db` returns healthy (confirms DB migration ran, Users table created)

**Step 2: Create initial admin accounts**

```bash
# First user (no auth required when no users exist)
curl -X POST http://localhost:5163/api/auth/setup \
  -H "Content-Type: application/json" \
  -d '{"username":"daniel","password":"<your-password>","displayName":"Daniel"}'

# Login as first user to create second account
curl -X POST http://localhost:5163/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"daniel","password":"<your-password>"}'
# Note the cookie from response

# Create second account (requires auth cookie)
curl -X POST http://localhost:5163/api/auth/create-user \
  -H "Content-Type: application/json" \
  -H "Cookie: creaturedex_token=<jwt-from-login>" \
  -d '{"username":"daughter","password":"<password>","displayName":"<name>"}'
```

**Step 3: Start the frontend**

```bash
cd src/creaturedex-web
npm run dev
```

**Step 4: Manual verification checklist**

- [ ] Visit http://localhost:3000 — no admin controls visible
- [ ] Navigate to /login — login form appears
- [ ] Login with created credentials — redirected to home, header shows display name + logout
- [ ] Visit /animals — "+ Add Animal" button visible
- [ ] Click "+ Add Animal" — modal appears
- [ ] Enter animal name, click generate — loading state, then redirect to new animal page
- [ ] On animal page — Edit toolbar visible with Edit, Generate Image, Upload Image, AI Review, Publish buttons
- [ ] Click Edit — fields become editable
- [ ] Modify a field, click Save — changes persist on page reload
- [ ] Click Upload Image — file picker, select image, image appears
- [ ] Click Generate Image — loading spinner, image generated
- [ ] Click AI Review — review panel appears with suggestions
- [ ] Accept a suggestion — field updates with suggested text
- [ ] Dismiss a suggestion — suggestion removed from panel
- [ ] Click Publish — animal becomes publicly visible
- [ ] Click Logout — admin controls disappear, unpublished animals hidden

**Step 5: Final commit**

```bash
git add -A
git commit -m "feat: complete admin content management system"
```

---

## Dependencies Between Tasks

```
Task 1 (DB schema) → Task 2 (Repository + AuthService) → Task 3 (Auth middleware + controller)
Task 3 → Task 4 (Admin endpoints) → Task 5 (AI review)
Task 3 → Task 6 (Skip image param)
Task 7 (Frontend auth) depends on Task 3
Task 8 (Frontend API client) depends on Tasks 4, 5, 6
Task 9 (EditToolbar) — independent component
Task 10 (ReviewPanel) — independent component
Task 11 (Inline editing) depends on Tasks 7, 8, 9, 10
Task 12 (Add Animal modal) depends on Tasks 7, 8
Task 13 (Integration testing) depends on all previous tasks
```

**Parallelizable work:**
- Tasks 9 + 10 can be built alongside backend tasks (4, 5, 6)
- Task 7 can start as soon as Task 3 is done
- Tasks 8, 9, 10 are independent of each other
