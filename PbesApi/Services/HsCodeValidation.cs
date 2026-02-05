namespace PbesApi.Services;

public static class HsCodeValidation
{
    public static bool IsAllowedFileType(string contentType)
    {
        return contentType is "application/pdf" or "image/jpeg" or "image/png";
    }

    public static bool IsDescriptionSpecific(string description)
    {
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 5 && description.Length >= 25;
    }

    public static bool IsGoodsRelated(string description)
    {
        var text = description.ToLowerInvariant();

        var blockedPhrases = new[]
        {
            "weather",
            "football",
            "soccer",
            "match",
            "scores",
            "news",
            "politic",
            "election",
            "president",
            "governor",
            "import duty",
            "customs duty",
            "tariff",
            "tax rate",
            "exchange rate",
            "visa",
            "passport"
        };

        foreach (var phrase in blockedPhrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static string? NormalizeImageBase64(string? imageBase64)
    {
        if (string.IsNullOrWhiteSpace(imageBase64))
        {
            return null;
        }

        var trimmed = imageBase64.Trim();
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = trimmed.IndexOf(',');
            if (commaIndex > -1 && commaIndex < trimmed.Length - 1)
            {
                return trimmed[(commaIndex + 1)..];
            }
        }

        return trimmed;
    }
}
