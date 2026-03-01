using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class SearchRepository(DbConnectionFactory db)
{
    public async Task<IEnumerable<(Animal Animal, double Score)>> FullTextSearchAsync(string query, int maxResults = 50)
    {
        using var conn = db.CreateConnection();

        // Try CONTAINS first (full-text), fall back to LIKE
        try
        {
            var results = await conn.QueryAsync<Animal, double, (Animal, double)>(
                """
                SELECT a.*, ft.[RANK] AS Score
                FROM Animals a
                INNER JOIN CONTAINSTABLE(Animals, (CommonName, ScientificName, Summary, Description), @Query) AS ft ON a.Id = ft.[KEY]
                WHERE a.DeletedAt IS NULL AND a.IsPublished = 1
                ORDER BY ft.[RANK] DESC
                OFFSET 0 ROWS FETCH NEXT @MaxResults ROWS ONLY
                """,
                (animal, score) => (animal, score),
                new { Query = $"\"{query}\"", MaxResults = maxResults },
                splitOn: "Score");

            return results;
        }
        catch
        {
            // Fallback to LIKE if full-text isn't available
            var results = await conn.QueryAsync<Animal>(
                """
                SELECT * FROM Animals
                WHERE DeletedAt IS NULL AND IsPublished = 1
                  AND (CommonName LIKE @Pattern OR ScientificName LIKE @Pattern
                       OR Summary LIKE @Pattern OR Description LIKE @Pattern)
                ORDER BY
                    CASE WHEN CommonName LIKE @Pattern THEN 0
                         WHEN ScientificName LIKE @Pattern THEN 1
                         ELSE 2 END,
                    CommonName
                OFFSET 0 ROWS FETCH NEXT @MaxResults ROWS ONLY
                """,
                new { Pattern = $"%{query}%", MaxResults = maxResults });

            return results.Select((a, i) => (a, (double)(maxResults - i)));
        }
    }
}
