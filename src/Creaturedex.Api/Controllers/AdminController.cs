using Creaturedex.AI;
using Creaturedex.AI.Services;
using Creaturedex.Api.Services;
using Creaturedex.Core.Entities;
using Creaturedex.Data.Repositories;
using Creaturedex.Shared.Requests;
using Creaturedex.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public class AdminController(
    ContentGenerationService contentGenService,
    ContentGeneratorService contentGenerator,
    ImageGenerationService imageService,
    AnimalRepository animalRepo,
    TagRepository tagRepo) : ControllerBase
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
        try
        {
            var id = await contentGenerator.GenerateAnimalAsync(request.AnimalName, request.SkipImage, ct);
            if (id == null)
                return StatusCode(500, new { error = $"Failed to generate content for {request.AnimalName}" });
            var animal = await animalRepo.GetByIdAsync(id.Value);
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

    [HttpPut("unpublish/{id:guid}")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        await animalRepo.UnpublishAsync(id);
        return Ok(new { message = "Unpublished" });
    }

    [HttpPut("publish/all")]
    public async Task<IActionResult> PublishAll()
    {
        await animalRepo.PublishAllAsync();
        return Ok(new { message = "All animals published" });
    }

    /// <summary>
    /// Test image generation with a custom prompt. Returns the generated image URL, the prompt used, and the seed.
    /// </summary>
    [HttpPost("image/test")]
    public async Task<IActionResult> TestImageGeneration([FromBody] TestImageRequest request, CancellationToken ct)
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

    /// <summary>
    /// Preview the full positive and negative prompts that would be generated for an animal.
    /// </summary>
    [HttpGet("image/preview-prompt")]
    public IActionResult PreviewPrompt([FromQuery] string animalName, [FromQuery] string? summary = null,
        [FromQuery] string? description = null, [FromQuery] string? habitat = null, [FromQuery] string? sizeInfo = null)
    {
        var prompt = ImageGenerationService.BuildPrompt(animalName, null, summary ?? "", description, habitat, sizeInfo);
        var negativePrompt = ImageGenerationService.GetNegativePrompt();
        return Ok(new { prompt, negativePrompt });
    }

    /// <summary>
    /// Preview the prompt that would be used for an existing animal by ID.
    /// </summary>
    [HttpGet("image/preview-prompt/{id:guid}")]
    public async Task<IActionResult> PreviewPromptForAnimal(Guid id)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null)
            return NotFound(new { error = "Animal not found" });

        var prompt = ImageGenerationService.BuildPrompt(
            animal.CommonName, animal.ScientificName, animal.Summary,
            animal.Description, animal.Habitat, animal.SizeInfo);
        var negativePrompt = ImageGenerationService.GetNegativePrompt();
        return Ok(new { animalName = animal.CommonName, prompt, negativePrompt });
    }

    /// <summary>
    /// Generate (or regenerate) an image for an existing animal by ID.
    /// </summary>
    [HttpPost("image/generate/{id:guid}")]
    public async Task<IActionResult> GenerateImageForAnimal(Guid id, CancellationToken ct)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null)
            return NotFound(new { error = "Animal not found" });

        var imageUrl = await imageService.GenerateAnimalImageAsync(
            animal.CommonName, animal.Slug, animal.ScientificName,
            animal.Summary, animal.Description, animal.Habitat, animal.SizeInfo, ct);
        if (imageUrl == null)
            return StatusCode(500, new { error = $"Image generation failed for {animal.CommonName}" });

        await animalRepo.UpdateImageUrlAsync(id, imageUrl);
        return Ok(new { imageUrl, animalName = animal.CommonName });
    }
    [HttpPut("animals/{id:guid}")]
    public async Task<IActionResult> UpdateAnimal(Guid id, [FromBody] UpdateAnimalRequest request)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null) return NotFound();

        animal.CommonName = request.CommonName;
        animal.ScientificName = request.ScientificName;
        animal.Summary = request.Summary;
        animal.Description = request.Description;
        animal.CategoryId = request.CategoryId;
        animal.IsPet = request.IsPet;
        animal.ConservationStatus = request.ConservationStatus;
        animal.NativeRegion = request.NativeRegion;
        animal.Habitat = request.Habitat;
        animal.Diet = request.Diet;
        animal.Lifespan = request.Lifespan;
        animal.SizeInfo = request.SizeInfo;
        animal.Behaviour = request.Behaviour;
        animal.FunFacts = request.FunFacts;
        animal.ReviewedBy = User.Identity?.Name;

        await animalRepo.UpdateAsync(animal);

        // Update tags
        await tagRepo.DeleteByAnimalIdAsync(animal.Id);
        if (request.Tags.Count > 0)
        {
            var tags = request.Tags.Select(t => new AnimalTag { AnimalId = animal.Id, Tag = t }).ToList();
            await tagRepo.BulkInsertAsync(tags);
        }

        return Ok(new { message = "Updated", id = animal.Id });
    }

    [RequestSizeLimit(10_485_760)]
    [HttpPost("animals/{id:guid}/image/upload")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, [FromServices] AIConfig aiCfg)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null) return NotFound();

        if (file.Length == 0) return BadRequest(new { error = "No file provided" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "File exceeds maximum size of 10 MB" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            return BadRequest(new { error = "Only PNG, JPG, and WebP images are allowed" });

        var fileName = $"{animal.Slug}{ext}";
        var storagePath = Path.Combine(AppContext.BaseDirectory, aiCfg.ImageStoragePath);
        Directory.CreateDirectory(storagePath);
        var filePath = Path.Combine(storagePath, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var imageUrl = $"/images/animals/{fileName}";
        await animalRepo.UpdateImageUrlAsync(id, imageUrl);

        return Ok(new { imageUrl });
    }

    [HttpPost("animals/{id:guid}/wikipedia-image")]
    public async Task<IActionResult> FetchWikipediaImage(Guid id, [FromServices] WikipediaService wikipediaService, CancellationToken ct)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null) return NotFound();

        var article = await wikipediaService.GetAnimalArticleAsync(animal.CommonName, ct);
        if (article?.ImageUrl == null)
            return NotFound(new { error = "No Wikipedia image found for this animal" });

        await animalRepo.UpdateImageUrlAsync(id, article.ImageUrl);
        return Ok(new { imageUrl = article.ImageUrl, source = article.Url, license = article.ImageLicense });
    }

    [HttpPost("animals/{id:guid}/review")]
    public async Task<IActionResult> ReviewAnimal(Guid id, [FromServices] ContentReviewService reviewService, CancellationToken ct)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null) return NotFound();

        var tags = (await tagRepo.GetByAnimalIdAsync(id)).Select(t => t.Tag).ToList();
        var suggestions = await reviewService.ReviewAnimalAsync(animal, tags, ct);

        return Ok(new { suggestions });
    }
}

public record TestImageRequest(string Prompt);
