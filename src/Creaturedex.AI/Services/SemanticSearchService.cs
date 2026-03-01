using Creaturedex.Data.Repositories;

namespace Creaturedex.AI.Services;

public class SemanticSearchService(
    EmbeddingService embeddingService,
    EmbeddingRepository embeddingRepo)
{
    public async Task<List<(Guid AnimalId, double Score)>> SearchAsync(string query, int topK = 10, CancellationToken ct = default)
    {
        var queryVector = await embeddingService.GenerateAsync(query, ct);
        var allEmbeddings = await embeddingRepo.GetAllAsync();

        var results = allEmbeddings
            .Select(e => (e.AnimalId, Score: CosineSimilarity(queryVector, e.Vector)))
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        return results;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }
}
