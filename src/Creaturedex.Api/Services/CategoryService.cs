using Creaturedex.Data.Repositories;
using Creaturedex.Shared.Responses;

namespace Creaturedex.Api.Services;

public class CategoryService(CategoryRepository categoryRepo)
{
    public async Task<List<CategoryDto>> GetAllAsync()
    {
        var categoriesWithCounts = await categoryRepo.GetWithCountsAsync();
        return categoriesWithCounts.Select(c => new CategoryDto
        {
            Id = c.Category.Id,
            Name = c.Category.Name,
            Slug = c.Category.Slug,
            Description = c.Category.Description,
            IconName = c.Category.IconName,
            AnimalCount = c.AnimalCount
        }).ToList();
    }

    public async Task<CategoryDto?> GetBySlugAsync(string slug)
    {
        var category = await categoryRepo.GetBySlugAsync(slug);
        if (category == null) return null;

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            IconName = category.IconName,
            AnimalCount = 0 // Would need a count query
        };
    }
}
