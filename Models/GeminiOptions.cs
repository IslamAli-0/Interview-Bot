namespace TelegramInterviewBot.Models;

public class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public bool EnableEmbeddings { get; set; } = true;
    public string EmbeddingModel { get; set; } = "text-embedding-004";
    public string[] GenerationModels { get; set; } = Array.Empty<string>();
}
