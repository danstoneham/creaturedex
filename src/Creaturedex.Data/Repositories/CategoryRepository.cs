using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class CategoryRepository(DbConnectionFactory db)
{
    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<Category>(
            "SELECT * FROM Categories ORDER BY SortOrder");
    }

    public async Task<Category?> GetBySlugAsync(string slug)
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Category>(
            "SELECT * FROM Categories WHERE Slug = @Slug",
            new { Slug = slug });
    }

    public async Task<IEnumerable<(Category Category, int AnimalCount)>> GetWithCountsAsync()
    {
        using var conn = db.CreateConnection();
        var results = await conn.QueryAsync<Category, int, (Category, int)>(
            """
            SELECT c.*, COUNT(a.Id) AS AnimalCount
            FROM Categories c
            LEFT JOIN Animals a ON c.Id = a.CategoryId AND a.DeletedAt IS NULL AND a.IsPublished = 1
            GROUP BY c.Id, c.Name, c.Slug, c.Description, c.IconName, c.ParentCategoryId, c.SortOrder
            ORDER BY c.SortOrder
            """,
            (category, count) => (category, count),
            splitOn: "AnimalCount");

        return results;
    }
}
