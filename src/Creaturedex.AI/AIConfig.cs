namespace Creaturedex.AI;

public class AIConfig
{
    public string OllamaEndpoint { get; set; } = "http://10.1.1.71:11436";
    public string ChatModel { get; set; } = "gpt-oss:20b";
    public string FastModel { get; set; } = "gpt-oss:20b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text:latest";
    public string StableDiffusionEndpoint { get; set; } = "http://10.1.1.71:8000";
    public string ImageStoragePath { get; set; } = "wwwroot/images/animals";
    public bool AutoGenerateImages { get; set; } = false;
}
