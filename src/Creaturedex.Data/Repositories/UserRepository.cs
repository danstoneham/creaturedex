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
