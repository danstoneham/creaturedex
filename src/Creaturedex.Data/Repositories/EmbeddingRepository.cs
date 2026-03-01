using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class EmbeddingRepository(DbConnectionFactory db)
{
    public async Task<AnimalEmbedding?> GetByAnimalIdAsync(Guid animalId)
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AnimalEmbedding>(
            "SELECT * FROM AnimalEmbeddings WHERE AnimalId = @AnimalId",
            new { AnimalId = animalId });
    }

    public async Task UpsertAsync(Guid animalId, float[] vector, string modelUsed)
    {
        using var conn = db.CreateConnection();
        var bytes = FloatsToBytes(vector);
        var dimensions = vector.Length;

        var existing = await conn.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT Id FROM AnimalEmbeddings WHERE AnimalId = @AnimalId",
            new { AnimalId = animalId });

        if (existing.HasValue)
        {
            await conn.ExecuteAsync("""
                UPDATE AnimalEmbeddings SET Embedding = @Embedding, Dimensions = @Dimensions,
                    ModelUsed = @ModelUsed, CreatedAt = SYSUTCDATETIME()
                WHERE AnimalId = @AnimalId
                """, new { AnimalId = animalId, Embedding = bytes, Dimensions = dimensions, ModelUsed = modelUsed });
        }
        else
        {
            await conn.ExecuteAsync("""
                INSERT INTO AnimalEmbeddings (Id, AnimalId, Embedding, Dimensions, ModelUsed, CreatedAt)
                VALUES (@Id, @AnimalId, @Embedding, @Dimensions, @ModelUsed, SYSUTCDATETIME())
                """, new { Id = Guid.NewGuid(), AnimalId = animalId, Embedding = bytes, Dimensions = dimensions, ModelUsed = modelUsed });
        }
    }

    public async Task<List<(Guid AnimalId, float[] Vector)>> GetAllAsync()
    {
        using var conn = db.CreateConnection();
        var embeddings = await conn.QueryAsync<AnimalEmbedding>(
            "SELECT AnimalId, Embedding FROM AnimalEmbeddings");

        return embeddings.Select(e => (e.AnimalId, BytesToFloats(e.Embedding))).ToList();
    }

    public static byte[] FloatsToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * 4];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
