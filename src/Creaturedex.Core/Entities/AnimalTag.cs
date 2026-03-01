namespace Creaturedex.Core.Entities;

public class AnimalTag
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public string Tag { get; set; } = string.Empty;
}
