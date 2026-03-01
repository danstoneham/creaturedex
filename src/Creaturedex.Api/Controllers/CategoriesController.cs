using Creaturedex.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Creaturedex.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(CategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await categoryService.GetAllAsync();
        return Ok(categories);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var category = await categoryService.GetBySlugAsync(slug);
        if (category == null) return NotFound();
        return Ok(category);
    }
}
