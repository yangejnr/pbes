using PbesApi.Models;

namespace PbesApi.Services;

public class HsCodeMatchEnrichmentService
{
    private readonly HsCodeRagService _ragService;

    public HsCodeMatchEnrichmentService(HsCodeRagService ragService)
    {
        _ragService = ragService;
    }

    public List<HsCodeMatch> Enrich(List<HsCodeMatch>? matches, string? queryContext = null)
    {
        if (matches is null || matches.Count == 0)
        {
            return new List<HsCodeMatch>();
        }

        var enriched = new List<HsCodeMatch>(matches.Count);

        foreach (var match in matches)
        {
            var normalized = HsCodeRagService.FormatHsCode(match.HsCode) ?? match.HsCode;
            var ragRow = _ragService.LookupByHsCode(normalized);
            var ragColumns = ragRow?.Columns;
            var validatedByCode = ragColumns is not null && ragColumns.Count > 0;

            if (!validatedByCode)
            {
                var fallback = _ragService.FindBestByDescription(queryContext) ??
                               _ragService.FindBestByDescription(match.Description);
                if (fallback?.Columns is not null && fallback.Columns.Count > 0)
                {
                    ragColumns = fallback.Columns;
                }
            }

            var validated = ragColumns is not null && ragColumns.Count > 0;
            var canonicalHsCode = ExtractColumn(ragColumns, "HS Code") ??
                                  ExtractColumn(ragColumns, "HS code") ??
                                  normalized;
            var canonicalDescription = ExtractColumn(ragColumns, "Description") ??
                                       ExtractColumn(ragColumns, "description") ??
                                       match.Description;

            enriched.Add(match with
            {
                HsCode = canonicalHsCode,
                Description = canonicalDescription,
                RagColumns = validated ? ragColumns : null,
                RagValidated = validated
            });
        }

        return enriched;
    }

    private static string? ExtractColumn(Dictionary<string, string>? columns, string key)
    {
        if (columns is null)
        {
            return null;
        }

        if (!columns.TryGetValue(key, out var value))
        {
            return null;
        }

        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
