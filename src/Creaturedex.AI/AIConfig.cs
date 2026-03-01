namespace Creaturedex.AI;

public class AIConfig
{
    public string OllamaEndpoint { get; set; } = "http://10.1.1.71:11436";
    public string ChatModel { get; set; } = "llama3:70b-instruct";
    public string FastModel { get; set; } = "llama3:8b-instruct";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}
