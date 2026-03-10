using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class TaxonomyRepository(DbConnectionFactory db)
{
    public async Task<Taxonomy?> GetByIdAsync(Guid id)
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Taxonomy>(
            "SELECT * FROM Taxonomy WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<Guid> CreateAsync(Taxonomy taxonomy)
    {
        using var conn = db.CreateConnection();
        taxonomy.Id = taxonomy.Id == Guid.Empty ? Guid.NewGuid() : taxonomy.Id;

        await conn.ExecuteAsync("""
            INSERT INTO Taxonomy (Id, Kingdom, Phylum, Class, TaxOrder, Family, Genus, Species, Subspecies,
                ColTaxonId, Authorship, Synonyms)
            VALUES (@Id, @Kingdom, @Phylum, @Class, @TaxOrder, @Family, @Genus, @Species, @Subspecies,
                @ColTaxonId, @Authorship, @Synonyms)
            """, taxonomy);

        return taxonomy.Id;
    }
}
