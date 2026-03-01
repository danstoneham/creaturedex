using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class AnimalRepository(DbConnectionFactory db)
{
    public async Task<Animal?> GetBySlugAsync(string slug)
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Animal>(
            "SELECT * FROM Animals WHERE Slug = @Slug AND DeletedAt IS NULL AND IsPublished = 1",
            new { Slug = slug });
    }

    public async Task<Animal?> GetByIdAsync(Guid id)
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Animal>(
            "SELECT * FROM Animals WHERE Id = @Id AND DeletedAt IS NULL",
            new { Id = id });
    }

    public async Task<IEnumerable<Animal>> BrowseAsync(Guid? categoryId, bool? isPet, string? tag, int page, int pageSize, string sortBy = "name")
    {
        using var conn = db.CreateConnection();
        var orderBy = sortBy == "newest" ? "CreatedAt DESC" : "CommonName ASC";

        var sql = $"""
            SELECT DISTINCT a.* FROM Animals a
            {(tag != null ? "INNER JOIN AnimalTags t ON a.Id = t.AnimalId AND t.Tag = @Tag" : "")}
            WHERE a.DeletedAt IS NULL AND a.IsPublished = 1
              AND (@CategoryId IS NULL OR a.CategoryId = @CategoryId)
              AND (@IsPet IS NULL OR a.IsPet = @IsPet)
            ORDER BY a.{orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        return await conn.QueryAsync<Animal>(sql,
            new { CategoryId = categoryId, IsPet = isPet, Tag = tag, Offset = (page - 1) * pageSize, PageSize = pageSize });
    }

    public async Task<int> CountAsync(Guid? categoryId, bool? isPet, string? tag)
    {
        using var conn = db.CreateConnection();
        var sql = $"""
            SELECT COUNT(DISTINCT a.Id) FROM Animals a
            {(tag != null ? "INNER JOIN AnimalTags t ON a.Id = t.AnimalId AND t.Tag = @Tag" : "")}
            WHERE a.DeletedAt IS NULL AND a.IsPublished = 1
              AND (@CategoryId IS NULL OR a.CategoryId = @CategoryId)
              AND (@IsPet IS NULL OR a.IsPet = @IsPet)
            """;

        return await conn.ExecuteScalarAsync<int>(sql,
            new { CategoryId = categoryId, IsPet = isPet, Tag = tag });
    }

    public async Task<Animal?> GetRandomAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Animal>(
            "SELECT TOP 1 * FROM Animals WHERE DeletedAt IS NULL AND IsPublished = 1 ORDER BY NEWID()");
    }

    public async Task<Guid> CreateAsync(Animal animal)
    {
        using var conn = db.CreateConnection();
        animal.Id = animal.Id == Guid.Empty ? Guid.NewGuid() : animal.Id;
        animal.CreatedAt = DateTime.UtcNow;
        animal.UpdatedAt = DateTime.UtcNow;

        await conn.ExecuteAsync("""
            INSERT INTO Animals (Id, Slug, CommonName, ScientificName, Summary, Description,
                CategoryId, TaxonomyId, IsPet, ImageUrl, ConservationStatus, NativeRegion,
                Habitat, Diet, Lifespan, SizeInfo, Behaviour, FunFacts,
                GeneratedAt, IsPublished, CreatedAt, UpdatedAt, Version)
            VALUES (@Id, @Slug, @CommonName, @ScientificName, @Summary, @Description,
                @CategoryId, @TaxonomyId, @IsPet, @ImageUrl, @ConservationStatus, @NativeRegion,
                @Habitat, @Diet, @Lifespan, @SizeInfo, @Behaviour, @FunFacts,
                @GeneratedAt, @IsPublished, @CreatedAt, @UpdatedAt, @Version)
            """, animal);

        return animal.Id;
    }

    public async Task UpdateAsync(Animal animal)
    {
        using var conn = db.CreateConnection();
        animal.UpdatedAt = DateTime.UtcNow;

        await conn.ExecuteAsync("""
            UPDATE Animals SET
                Slug = @Slug, CommonName = @CommonName, ScientificName = @ScientificName,
                Summary = @Summary, Description = @Description, CategoryId = @CategoryId,
                TaxonomyId = @TaxonomyId, IsPet = @IsPet, ImageUrl = @ImageUrl,
                ConservationStatus = @ConservationStatus, NativeRegion = @NativeRegion,
                Habitat = @Habitat, Diet = @Diet, Lifespan = @Lifespan,
                SizeInfo = @SizeInfo, Behaviour = @Behaviour, FunFacts = @FunFacts,
                GeneratedAt = @GeneratedAt, ReviewedAt = @ReviewedAt, ReviewedBy = @ReviewedBy,
                IsPublished = @IsPublished, UpdatedAt = @UpdatedAt,
                Version = Version + 1
            WHERE Id = @Id AND Version = @Version AND DeletedAt IS NULL
            """, animal);
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Animals SET DeletedAt = SYSUTCDATETIME() WHERE Id = @Id AND DeletedAt IS NULL",
            new { Id = id });
    }

    public async Task<IEnumerable<Animal>> GetUnreviewedAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<Animal>(
            "SELECT * FROM Animals WHERE DeletedAt IS NULL AND IsPublished = 0 ORDER BY CreatedAt DESC");
    }

    public async Task MarkReviewedAsync(Guid id)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Animals SET ReviewedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id",
            new { Id = id });
    }

    public async Task PublishAsync(Guid id)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Animals SET IsPublished = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id",
            new { Id = id });
    }

    public async Task PublishAllAsync()
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Animals SET IsPublished = 1, UpdatedAt = SYSUTCDATETIME() WHERE DeletedAt IS NULL AND IsPublished = 0");
    }

    public async Task UpdateImageUrlAsync(Guid id, string imageUrl)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Animals SET ImageUrl = @ImageUrl, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND DeletedAt IS NULL",
            new { Id = id, ImageUrl = imageUrl });
    }
}
