using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class TagRepository(DbConnectionFactory db)
{
    public async Task<IEnumerable<AnimalTag>> GetByAnimalIdAsync(Guid animalId)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<AnimalTag>(
            "SELECT * FROM AnimalTags WHERE AnimalId = @AnimalId",
            new { AnimalId = animalId });
    }

    public async Task<IEnumerable<(string Tag, int Count)>> GetAllUniqueAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<(string, int)>(
            """
            SELECT Tag, COUNT(*) AS Count FROM AnimalTags
            INNER JOIN Animals a ON AnimalTags.AnimalId = a.Id AND a.DeletedAt IS NULL AND a.IsPublished = 1
            GROUP BY Tag ORDER BY Count DESC
            """);
    }

    public async Task<IEnumerable<AnimalTag>> GetByTagAsync(string tag)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<AnimalTag>(
            "SELECT * FROM AnimalTags WHERE Tag = @Tag",
            new { Tag = tag });
    }

    public async Task BulkInsertAsync(IEnumerable<AnimalTag> tags)
    {
        using var conn = db.CreateConnection();
        foreach (var t in tags)
        {
            t.Id = t.Id == Guid.Empty ? Guid.NewGuid() : t.Id;
        }

        await conn.ExecuteAsync(
            "INSERT INTO AnimalTags (Id, AnimalId, Tag) VALUES (@Id, @AnimalId, @Tag)",
            tags);
    }

    public async Task DeleteByAnimalIdAsync(Guid animalId)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM AnimalTags WHERE AnimalId = @AnimalId",
            new { AnimalId = animalId });
    }
}
