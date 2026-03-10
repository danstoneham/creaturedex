namespace Creaturedex.Core.Entities;

public class Taxonomy
{
    public Guid Id { get; set; }
    public string Kingdom { get; set; } = "Animalia";
    public string? Phylum { get; set; }
    public string? Class { get; set; }
    public string? TaxOrder { get; set; }
    public string? Family { get; set; }
    public string? Genus { get; set; }
    public string? Species { get; set; }
    public string? Subspecies { get; set; }

    // GBIF/CoL extended taxonomy
    public string? ColTaxonId { get; set; }
    public string? Authorship { get; set; }
    public string? Synonyms { get; set; }
}
