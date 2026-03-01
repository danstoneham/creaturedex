using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class CharacteristicRepository(DbConnectionFactory db)
{
    public async Task<IEnumerable<AnimalCharacteristic>> GetByAnimalIdAsync(Guid animalId)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<AnimalCharacteristic>(
            "SELECT * FROM AnimalCharacteristics WHERE AnimalId = @AnimalId ORDER BY SortOrder",
            new { AnimalId = animalId });
    }

    public async Task BulkInsertAsync(IEnumerable<AnimalCharacteristic> characteristics)
    {
        using var conn = db.CreateConnection();
        foreach (var c in characteristics)
        {
            c.Id = c.Id == Guid.Empty ? Guid.NewGuid() : c.Id;
        }

        await conn.ExecuteAsync("""
            INSERT INTO AnimalCharacteristics (Id, AnimalId, CharacteristicName, CharacteristicValue, SortOrder)
            VALUES (@Id, @AnimalId, @CharacteristicName, @CharacteristicValue, @SortOrder)
            """, characteristics);
    }

    public async Task DeleteByAnimalIdAsync(Guid animalId)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM AnimalCharacteristics WHERE AnimalId = @AnimalId",
            new { AnimalId = animalId });
    }
}
