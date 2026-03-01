using Creaturedex.Data.Repositories;
using Creaturedex.Shared.Responses;

namespace Creaturedex.Api.Services;

public class ContentGenerationService(AnimalRepository animalRepo)
{
    // TODO: Wire up ContentGeneratorService from Creaturedex.AI once prompts are finalized

    public async Task<GenerationStatusResponse> GetStatusAsync()
    {
        // Simple status based on database counts
        var total = await animalRepo.CountAsync(null, null, null);
        var unreviewed = (await animalRepo.GetUnreviewedAsync()).Count();

        return new GenerationStatusResponse
        {
            TotalAnimals = total + unreviewed,
            GeneratedCount = total + unreviewed,
            PublishedCount = total,
            PendingReviewCount = unreviewed,
            IsGenerating = false,
            CurrentAnimal = null
        };
    }
}
