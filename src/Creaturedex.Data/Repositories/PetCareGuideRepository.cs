using Creaturedex.Core.Entities;
using Dapper;

namespace Creaturedex.Data.Repositories;

public class PetCareGuideRepository(DbConnectionFactory db)
{
    public async Task<PetCareGuide?> GetByAnimalIdAsync(Guid animalId)
    {
        using var conn = db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<PetCareGuide>(
            "SELECT * FROM PetCareGuides WHERE AnimalId = @AnimalId",
            new { AnimalId = animalId });
    }

    public async Task<Guid> CreateAsync(PetCareGuide guide)
    {
        using var conn = db.CreateConnection();
        guide.Id = guide.Id == Guid.Empty ? Guid.NewGuid() : guide.Id;

        await conn.ExecuteAsync("""
            INSERT INTO PetCareGuides (Id, AnimalId, DifficultyRating, CostRangeMin, CostRangeMax,
                CostCurrency, SpaceRequirement, TimeCommitment, Housing, DietAsPet,
                Exercise, Grooming, HealthConcerns, Training,
                GoodWithChildren, GoodWithOtherPets, Temperament, LegalConsiderations)
            VALUES (@Id, @AnimalId, @DifficultyRating, @CostRangeMin, @CostRangeMax,
                @CostCurrency, @SpaceRequirement, @TimeCommitment, @Housing, @DietAsPet,
                @Exercise, @Grooming, @HealthConcerns, @Training,
                @GoodWithChildren, @GoodWithOtherPets, @Temperament, @LegalConsiderations)
            """, guide);

        return guide.Id;
    }

    public async Task UpdateAsync(PetCareGuide guide)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE PetCareGuides SET
                DifficultyRating = @DifficultyRating, CostRangeMin = @CostRangeMin,
                CostRangeMax = @CostRangeMax, CostCurrency = @CostCurrency,
                SpaceRequirement = @SpaceRequirement, TimeCommitment = @TimeCommitment,
                Housing = @Housing, DietAsPet = @DietAsPet,
                Exercise = @Exercise, Grooming = @Grooming,
                HealthConcerns = @HealthConcerns, Training = @Training,
                GoodWithChildren = @GoodWithChildren, GoodWithOtherPets = @GoodWithOtherPets,
                Temperament = @Temperament, LegalConsiderations = @LegalConsiderations
            WHERE Id = @Id
            """, guide);
    }
}
