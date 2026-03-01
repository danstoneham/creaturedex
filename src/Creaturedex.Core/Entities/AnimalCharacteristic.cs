namespace Creaturedex.Core.Entities;

public class AnimalCharacteristic
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public string CharacteristicName { get; set; } = string.Empty;
    public string CharacteristicValue { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
