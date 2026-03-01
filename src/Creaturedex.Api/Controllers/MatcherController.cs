using Creaturedex.Api.Services;
using Creaturedex.Shared.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Route("api/matcher")]
public class MatcherController(MatcherService matcherService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Match([FromBody] MatcherRequest request)
    {
        var result = await matcherService.GetRecommendationsAsync(request);
        return Ok(result);
    }
}
