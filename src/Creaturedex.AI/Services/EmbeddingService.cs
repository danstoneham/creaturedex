using Microsoft.Extensions.AI;
using Creaturedex.Data.Repositories;

namespace Creaturedex.AI.Services;

public class EmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    EmbeddingRepository embeddingRepo)
{
    public async Task<float[]> GenerateAsync(string text, CancellationToken ct = default)
    {
        var embedding = await embeddingGenerator.GenerateAsync(text, cancellationToken: ct);
        return embedding.Vector.ToArray();
    }

    public async Task GenerateAndStoreAsync(Guid animalId, string text, string modelUsed, CancellationToken ct = default)
    {
        var vector = await GenerateAsync(text, ct);
        await embeddingRepo.UpsertAsync(animalId, vector, modelUsed);
    }
}
