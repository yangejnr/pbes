namespace PbesApi.Services;

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2-vision";
    public string TextModel { get; set; } = "llama3:8b";
    public int TimeoutSeconds { get; set; } = 300;
}
