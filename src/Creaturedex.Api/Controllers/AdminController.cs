using Creaturedex.AI.Services;
using Creaturedex.Api.Services;
using Creaturedex.Shared.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public class AdminController(
    ContentGenerationService contentGenService,
    ContentGeneratorService contentGenerator,
    AnimalService animalService,
    GbifService gbifService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await contentGenService.GetStatusAsync();
        return Ok(status);
    }

    [HttpGet("species/search")]
    public async Task<IActionResult> SearchSpecies([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required" });

        var suggestions = await gbifService.SearchSpeciesAsync(q.Trim(), ct);
        return Ok(suggestions);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateAnimalRequest request, CancellationToken ct)
    {
        try
        {
            var id = await contentGenerator.GenerateAnimalAsync(request.AnimalName, request.SkipImage, request.TaxonKey, request.ScientificName, ct);
            if (id == null)
                return StatusCode(500, new { error = $"Failed to generate content for {request.AnimalName}" });
            var animal = await animalService.GetByIdAsync(id.Value);
            return Ok(new { id, slug = animal?.Slug, message = $"Generated {request.AnimalName}" });
        }
        catch (DuplicateAnimalException ex)
        {
            return Conflict(new { error = ex.Message, slug = ex.Slug, duplicate = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to generate content for {request.AnimalName}", detail = ex.Message, innerError = ex.InnerException?.Message });
        }
    }

    [HttpPost("animals/{id:guid}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid id, CancellationToken ct)
    {
        try
        {
            var (newId, slug) = await contentGenerator.RegenerateAnimalAsync(id, ct);
            return Ok(new { id = newId, slug, message = "Regenerated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("animals/{id:guid}")]
    public async Task<IActionResult> DeleteAnimal(Guid id)
    {
        var animal = await animalService.GetByIdAsync(id);
        if (animal == null)
            return NotFound(new { error = "Animal not found" });

        await animalService.DeleteAsync(id);
        return Ok(new { message = $"Deleted {animal.CommonName}" });
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
        var animals = await animalService.GetUnreviewedAsync();
        return Ok(animals);
    }

    [HttpPut("review/{id:guid}")]
    public async Task<IActionResult> MarkReviewed(Guid id)
    {
        await animalService.MarkReviewedAsync(id);
        return Ok(new { message = "Marked as reviewed" });
    }

    [HttpPut("publish/{id:guid}")]
    public async Task<IActionResult> Publish(Guid id)
    {
        await animalService.PublishAsync(id);
        return Ok(new { message = "Published" });
    }

    [HttpPut("unpublish/{id:guid}")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        await animalService.UnpublishAsync(id);
        return Ok(new { message = "Unpublished" });
    }

    [HttpPut("publish/all")]
    public async Task<IActionResult> PublishAll()
    {
        await animalService.PublishAllAsync();
        return Ok(new { message = "All animals published" });
    }

    [HttpPost("image/test")]
    public async Task<IActionResult> TestImageGeneration([FromBody] TestImageRequest request, [FromServices] ImageGenerationService imageService, CancellationToken ct)
    {
        try
        {
            var fileName = $"test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
            var result = await imageService.GenerateWithCustomPromptAsync(request.Prompt, fileName, null, ct);
            if (result == null)
                return StatusCode(500, new { error = "Image generation returned null" });

            return Ok(new { imageUrl = result.ImageUrl, prompt = result.PromptUsed, seed = result.Seed });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpGet("image/preview-prompt")]
    public IActionResult PreviewPrompt([FromQuery] string animalName, [FromQuery] string? summary = null,
        [FromQuery] string? description = null, [FromQuery] string? habitat = null, [FromQuery] string? sizeInfo = null)
    {
        var prompt = ImageGenerationService.BuildPrompt(animalName, null, summary ?? "", description, habitat, sizeInfo);
        var negativePrompt = ImageGenerationService.GetNegativePrompt();
        return Ok(new { prompt, negativePrompt });
    }

    [HttpGet("image/preview-prompt/{id:guid}")]
    public async Task<IActionResult> PreviewPromptForAnimal(Guid id)
    {
        var animal = await animalService.GetByIdAsync(id);
        if (animal == null)
            return NotFound(new { error = "Animal not found" });

        var result = animalService.PreviewPromptForAnimal(animal);
        return Ok(new { animalName = animal.CommonName, prompt = result!.Value.Prompt, negativePrompt = result.Value.NegativePrompt });
    }

    [HttpPost("image/generate/{id:guid}")]
    public async Task<IActionResult> GenerateImageForAnimal(Guid id, CancellationToken ct)
    {
        var (imageUrl, animalName) = await animalService.GenerateImageAsync(id, ct);
        if (animalName == null)
            return NotFound(new { error = "Animal not found" });
        if (imageUrl == null)
            return StatusCode(500, new { error = $"Image generation failed for {animalName}" });

        return Ok(new { imageUrl, animalName });
    }

    [HttpPut("animals/{id:guid}")]
    public async Task<IActionResult> UpdateAnimal(Guid id, [FromBody] UpdateAnimalRequest request)
    {
        var animal = await animalService.UpdateAnimalAsync(id, request, User.Identity?.Name);
        if (animal == null) return NotFound();
        return Ok(new { message = "Updated", id = animal.Id });
    }

    [RequestSizeLimit(10_485_760)]
    [HttpPost("animals/{id:guid}/image/upload")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        if (file.Length == 0) return BadRequest(new { error = "No file provided" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "File exceeds maximum size of 10 MB" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            return BadRequest(new { error = "Only PNG, JPG, and WebP images are allowed" });

        await using var stream = file.OpenReadStream();
        var imageUrl = await animalService.UploadImageAsync(id, stream, file.FileName, file.Length);
        if (imageUrl == null) return NotFound();

        return Ok(new { imageUrl });
    }

    [HttpPost("animals/{id:guid}/wikipedia-image")]
    public async Task<IActionResult> FetchWikipediaImage(Guid id, CancellationToken ct)
    {
        var (imageUrl, source, license) = await animalService.FetchWikipediaImageAsync(id, ct);
        if (imageUrl == null)
            return NotFound(new { error = "No Wikipedia image found for this animal" });

        return Ok(new { imageUrl, source, license });
    }

    [HttpPost("animals/{id:guid}/review")]
    public async Task<IActionResult> ReviewAnimal(Guid id, CancellationToken ct)
    {
        var suggestions = await animalService.ReviewAnimalAsync(id, ct);
        if (suggestions == null) return NotFound();
        return Ok(new { suggestions });
    }
}

public record TestImageRequest(string Prompt);
