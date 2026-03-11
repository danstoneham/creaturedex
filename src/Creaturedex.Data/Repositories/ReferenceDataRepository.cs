using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class ReferenceDataRepository(DbConnectionFactory db)
{
    public async Task<IReadOnlyList<ReferenceColour>> GetColoursAsync()
    {
        using var conn = db.CreateConnection();
        var results = await conn.QueryAsync<ReferenceColour>(
            "SELECT * FROM ReferenceColours ORDER BY SortOrder");
        return results.ToList();
    }

    public async Task<IReadOnlyList<ReferenceTag>> GetTagsAsync()
    {
        using var conn = db.CreateConnection();
        var results = await conn.QueryAsync<ReferenceTag>(
            "SELECT * FROM ReferenceTags ORDER BY TagGroup, SortOrder");
        return results.ToList();
    }

    public async Task<IReadOnlyList<ReferenceHabitatType>> GetHabitatTypesAsync()
    {
        using var conn = db.CreateConnection();
        var results = await conn.QueryAsync<ReferenceHabitatType>(
            "SELECT * FROM ReferenceHabitatTypes ORDER BY SortOrder");
        return results.ToList();
    }

    public async Task<IReadOnlyList<ReferenceDietType>> GetDietTypesAsync()
    {
        using var conn = db.CreateConnection();
        var results = await conn.QueryAsync<ReferenceDietType>(
            "SELECT * FROM ReferenceDietTypes ORDER BY SortOrder");
        return results.ToList();
    }

    public async Task<IReadOnlyList<ReferenceActivityPattern>> GetActivityPatternsAsync()
    {
        using var conn = db.CreateConnection();
        var results = await conn.QueryAsync<ReferenceActivityPattern>(
            "SELECT * FROM ReferenceActivityPatterns ORDER BY SortOrder");
        return results.ToList();
    }

    public async Task<IReadOnlyList<ReferenceConservationStatus>> GetConservationStatusesAsync()
    {
        using var conn = db.CreateConnection();
        var results = await conn.QueryAsync<ReferenceConservationStatus>(
            "SELECT * FROM ReferenceConservationStatuses ORDER BY Severity");
        return results.ToList();
    }

    public async Task<IReadOnlyList<ReferenceDomesticationStatus>> GetDomesticationStatusesAsync()
    {
        using var conn = db.CreateConnection();
        var results = await conn.QueryAsync<ReferenceDomesticationStatus>(
            "SELECT * FROM ReferenceDomesticationStatuses ORDER BY SortOrder");
        return results.ToList();
    }
}
