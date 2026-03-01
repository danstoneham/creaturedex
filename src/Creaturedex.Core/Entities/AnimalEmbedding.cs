namespace Creaturedex.Core.Entities;

public class AnimalEmbedding
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public byte[] Embedding { get; set; } = [];
    public int Dimensions { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
