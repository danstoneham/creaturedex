using Creaturedex.Api.Services;
using Creaturedex.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController(SearchService searchService, TagRepository tagRepo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string? type)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        var results = await searchService.SearchAsync(q, type ?? "text");
        return Ok(results);
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags()
    {
        var tags = await tagRepo.GetAllUniqueAsync();
        return Ok(tags);
    }
}
