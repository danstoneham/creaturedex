using Creaturedex.Api.Services;
using Creaturedex.Shared.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Route("api/animals")]
public class AnimalsController(AnimalService animalService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Browse([FromQuery] BrowseAnimalsRequest request)
    {
        var (animals, totalCount) = await animalService.BrowseAsync(request);
        return Ok(new { animals, totalCount });
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var animal = await animalService.GetBySlugAsync(slug, includeUnpublished: isAuthenticated);
        if (animal == null) return NotFound();
        return Ok(animal);
    }

    [HttpGet("random")]
    public async Task<IActionResult> GetRandom()
    {
        var animal = await animalService.GetRandomAsync();
        if (animal == null) return NotFound();
        return Ok(animal);
    }
}
