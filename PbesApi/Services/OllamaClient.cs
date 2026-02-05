using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PbesApi.Models;

namespace PbesApi.Services;

public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<HsCodeModelResponse> ScanAsync(string? description, string? imageBase64, CancellationToken cancellationToken)
    {
        var systemPrompt =
            "You are an HS Code assistant for customs inspection. " +
            "Given a passenger baggage item description and optional image, return up to 5 likely HS codes. " +
            "Respond strictly in JSON per the provided schema. " +
            "If information is insufficient, return an empty matches array and include a short note asking for specifics.";

        var userPrompt =
            "Item description:\n" + (string.IsNullOrWhiteSpace(description) ? "N/A" : description) +
            "\n\nReturn best HS code matches with clear, concise descriptions.";

        var userMessage = new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = userPrompt
        };

        if (!string.IsNullOrWhiteSpace(imageBase64))
        {
            userMessage["images"] = new[] { imageBase64 };
        }

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["matches"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object?>
                        {
                            ["hsCode"] = new Dictionary<string, object?> { ["type"] = "string" },
                            ["description"] = new Dictionary<string, object?> { ["type"] = "string" },
                            ["matchPercent"] = new Dictionary<string, object?> { ["type"] = "number" },
                            ["comment"] = new Dictionary<string, object?> { ["type"] = "string" },
                            ["subsections"] = new Dictionary<string, object?>
                            {
                                ["type"] = "array",
                                ["items"] = new Dictionary<string, object?>
                                {
                                    ["type"] = "object",
                                    ["properties"] = new Dictionary<string, object?>
                                    {
                                        ["hsCode"] = new Dictionary<string, object?> { ["type"] = "string" },
                                        ["title"] = new Dictionary<string, object?> { ["type"] = "string" },
                                        ["notes"] = new Dictionary<string, object?> { ["type"] = "string" }
                                    },
                                    ["required"] = new[] { "hsCode", "title", "notes" }
                                }
                            }
                        },
                        ["required"] = new[] { "hsCode", "description", "matchPercent", "comment", "subsections" }
                    }
                },
                ["note"] = new Dictionary<string, object?> { ["type"] = "string" }
            },
            ["required"] = new[] { "matches" }
        };

        var modelToUse = string.IsNullOrWhiteSpace(imageBase64) ? _options.TextModel : _options.Model;

        var payload = new Dictionary<string, object?>
        {
            ["model"] = modelToUse,
            ["messages"] = new object?[]
            {
                new Dictionary<string, object?> { ["role"] = "system", ["content"] = systemPrompt },
                userMessage
            },
            ["stream"] = false,
            ["format"] = schema,
            ["options"] = new Dictionary<string, object?>
            {
                ["temperature"] = 0.2,
                ["top_p"] = 0.9,
                ["num_predict"] = 200
            }
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/chat", payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseJson);
        var messageElement = document.RootElement.GetProperty("message").GetProperty("content");

        if (messageElement.ValueKind == JsonValueKind.Object || messageElement.ValueKind == JsonValueKind.Array)
        {
            var raw = messageElement.GetRawText();
            return TryDeserializeResponse(raw) ??
                   new HsCodeModelResponse(new List<HsCodeMatch>(), "Unable to parse model response. Please refine the item description.");
        }

        var content = messageElement.ValueKind == JsonValueKind.String ? messageElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(content))
        {
            return new HsCodeModelResponse(new List<HsCodeMatch>(), "No response from model.");
        }

        content = content.Trim();
        content = StripCodeFences(content);

        return TryDeserializeResponse(content) ??
               TryDeserializeResponse(ExtractJsonObject(content)) ??
               new HsCodeModelResponse(new List<HsCodeMatch>(), "Unable to parse model response. Please refine the item description.");
    }

    private static HsCodeModelResponse? TryDeserializeResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<HsCodeModelResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return content[start..(end + 1)];
    }

    private static string StripCodeFences(string content)
    {
        if (!content.StartsWith("```", StringComparison.Ordinal))
        {
            return content;
        }

        var firstNewline = content.IndexOf('\n');
        if (firstNewline < 0)
        {
            return content;
        }

        var stripped = content[(firstNewline + 1)..];
        var lastFence = stripped.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence >= 0)
        {
            stripped = stripped[..lastFence];
        }

        return stripped.Trim();
    }
}
