namespace Creaturedex.Shared.Requests;

public class BatchGenerateRequest
{
    public List<GenerateAnimalRequest> Animals { get; set; } = [];
}
