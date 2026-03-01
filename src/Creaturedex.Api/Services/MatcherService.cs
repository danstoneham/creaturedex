using Creaturedex.Shared.Requests;
using Creaturedex.Shared.Responses;

namespace Creaturedex.Api.Services;

public class MatcherService()
{
    // TODO: Wire up MatcherAIService from Creaturedex.AI once content pipeline is ready
    public async Task<MatcherResultResponse> GetRecommendationsAsync(MatcherRequest request)
    {
        // Placeholder -- will call MatcherAIService for AI-powered recommendations
        await Task.CompletedTask;
        return new MatcherResultResponse
        {
            Recommendations = []
        };
    }
}
