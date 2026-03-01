using Creaturedex.AI.Services;
using Creaturedex.Api.Services;
using Creaturedex.Data.Repositories;
using Creaturedex.Shared.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController(
    ContentGenerationService contentGenService,
    ContentGeneratorService contentGenerator,
    AnimalRepository animalRepo) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await contentGenService.GetStatusAsync();
        return Ok(status);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateAnimalRequest request, CancellationToken ct)
    {
        var id = await contentGenerator.GenerateAnimalAsync(request.AnimalName, ct);
        if (id == null)
            return StatusCode(500, new { error = $"Failed to generate content for {request.AnimalName}" });
        return Ok(new { id, message = $"Generated {request.AnimalName}" });
    }

    [HttpPost("generate/batch")]
    public async Task<IActionResult> GenerateBatch([FromBody] BatchGenerateRequest request, CancellationToken ct)
    {
        var names = request.Animals.Select(a => a.AnimalName).ToList();
        var results = await contentGenerator.BatchGenerateAsync(names, ct);
        return Ok(results.Select(r => new { name = r.Name, id = r.Id, error = r.Error }));
    }

    [HttpGet("review")]
    public async Task<IActionResult> GetUnreviewed()
    {
        var animals = await animalRepo.GetUnreviewedAsync();
        return Ok(animals);
    }

    [HttpPut("review/{id:guid}")]
    public async Task<IActionResult> MarkReviewed(Guid id)
    {
        await animalRepo.MarkReviewedAsync(id);
        return Ok(new { message = "Marked as reviewed" });
    }

    [HttpPut("publish/{id:guid}")]
    public async Task<IActionResult> Publish(Guid id)
    {
        await animalRepo.PublishAsync(id);
        return Ok(new { message = "Published" });
    }

    [HttpPut("publish/all")]
    public async Task<IActionResult> PublishAll()
    {
        await animalRepo.PublishAllAsync();
        return Ok(new { message = "All animals published" });
    }
}
