using Creaturedex.Api.Services;
using Creaturedex.Data.Repositories;
using Creaturedex.Shared.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController(
    ContentGenerationService contentGenService,
    AnimalRepository animalRepo) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await contentGenService.GetStatusAsync();
        return Ok(status);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateAnimalRequest request)
    {
        // TODO: Wire up AI content generation pipeline
        return Ok(new { message = $"Generation queued for {request.AnimalName}" });
    }

    [HttpPost("generate/batch")]
    public async Task<IActionResult> GenerateBatch([FromBody] BatchGenerateRequest request)
    {
        // TODO: Wire up AI batch content generation pipeline
        return Ok(new { message = $"Batch generation queued for {request.Animals.Count} animals" });
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
