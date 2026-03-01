namespace Creaturedex.Core.Entities;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
}
