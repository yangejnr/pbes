using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using PbesApi.Models;

namespace PbesApi.Services;

public class HsCodeRagService
{
    private static readonly Regex TokenRegex = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HsCodeRagOptions _options;
    private readonly ILogger<HsCodeRagService> _logger;
    private readonly string _contentRootPath;
    private readonly object _sync = new();

    private DateTime _loadedFileWriteTimeUtc;
    private List<RagRow> _rows = new();

    public HsCodeRagService(
        Microsoft.Extensions.Options.IOptions<HsCodeRagOptions> options,
        ILogger<HsCodeRagService> logger,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _logger = logger;
        _contentRootPath = environment.ContentRootPath;
    }

    public (bool Loaded, string Message, int RowCount) Reload()
    {
        lock (_sync)
        {
            return LoadInternal(force: true);
        }
    }

    public HsCodeRagRowResponse? LookupByHsCode(string hsCode)
    {
        lock (_sync)
        {
            var loadResult = LoadInternal(force: false);
            if (!loadResult.Loaded)
            {
                return null;
            }

            var match = FindBestByHsCode(hsCode);
            if (match is null)
            {
                return null;
            }

            return new HsCodeRagRowResponse(FilterColumns(match.Columns));
        }
    }

    public HsCodeRagSearchResponse Search(string query, int topK)
    {
        lock (_sync)
        {
            var loadResult = LoadInternal(force: false);
            if (!loadResult.Loaded)
            {
                return new HsCodeRagSearchResponse(0, new List<HsCodeRagRowResponse>(), loadResult.Message);
            }

            var trimmed = query?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                return new HsCodeRagSearchResponse(0, new List<HsCodeRagRowResponse>(), "Query is required.");
            }

            var requested = topK <= 0 ? 5 : topK;
            var limit = Math.Min(requested, 50);

            var tokens = Tokenize(trimmed);
            var formattedHs = FormatHsCode(trimmed);

            var scored = _rows
                .Select(row => new { Row = row, Score = Score(row, trimmed, tokens, formattedHs) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Row.HsCode)
                .Take(limit)
                .Select(x => new HsCodeRagRowResponse(FilterColumns(x.Row.Columns)))
                .ToList();

            return new HsCodeRagSearchResponse(scored.Count, scored);
        }
    }

    public HsCodeRagRowResponse? FindBestByDescription(string? query)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return null;
        }

        var result = Search(trimmed, 1);
        if (result.Total <= 0 || result.Rows.Count == 0)
        {
            return null;
        }

        return result.Rows[0];
    }

    private (bool Loaded, string Message, int RowCount) LoadInternal(bool force)
    {
        try
        {
            var path = ResolvePath(_options.FilePath, _contentRootPath);
            if (!File.Exists(path))
            {
                _rows = new List<RagRow>();
                _loadedFileWriteTimeUtc = DateTime.MinValue;
                return (false, $"Excel file not found at '{path}'. Place your file there and retry.", 0);
            }

            var writeTime = File.GetLastWriteTimeUtc(path);
            if (!force && _rows.Count > 0 && _loadedFileWriteTimeUtc == writeTime)
            {
                return (true, "Loaded", _rows.Count);
            }

            using var workbook = new XLWorkbook(path);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null)
            {
                _rows = new List<RagRow>();
                _loadedFileWriteTimeUtc = writeTime;
                return (false, "Workbook has no worksheets.", 0);
            }

            var usedRange = worksheet.RangeUsed();
            if (usedRange is null)
            {
                _rows = new List<RagRow>();
                _loadedFileWriteTimeUtc = writeTime;
                return (false, "Worksheet is empty.", 0);
            }

            var firstRow = usedRange.FirstRow().RowNumber();
            var lastRow = usedRange.LastRow().RowNumber();
            var firstColumn = usedRange.FirstColumn().ColumnNumber();
            var lastColumn = usedRange.LastColumn().ColumnNumber();

            var headers = new List<(int Column, string Header)>();
            for (var col = firstColumn; col <= lastColumn; col++)
            {
                var header = worksheet.Cell(firstRow, col).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(header))
                {
                    headers.Add((col, header));
                }
            }

            if (headers.Count == 0)
            {
                _rows = new List<RagRow>();
                _loadedFileWriteTimeUtc = writeTime;
                return (false, "Header row is empty.", 0);
            }

            var hsCodeHeader = headers.FirstOrDefault(h => string.Equals(NormalizeHeader(h.Header), "hscode", StringComparison.OrdinalIgnoreCase));
            if (hsCodeHeader == default)
            {
                _rows = new List<RagRow>();
                _loadedFileWriteTimeUtc = writeTime;
                return (false, "Missing required 'HS Code' column.", 0);
            }

            var parsed = new List<RagRow>();
            for (var row = firstRow + 1; row <= lastRow; row++)
            {
                var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                {
                    var raw = worksheet.Cell(row, header.Column).GetFormattedString().Trim();
                    if (string.Equals(NormalizeHeader(header.Header), "hscode", StringComparison.OrdinalIgnoreCase))
                    {
                        raw = FormatHsCode(raw) ?? string.Empty;
                    }

                    columns[header.Header] = raw;
                }

                var hsCode = columns[hsCodeHeader.Header];
                if (string.IsNullOrWhiteSpace(hsCode))
                {
                    continue;
                }

                var searchableText = string.Join(' ', columns.Values).ToLowerInvariant();
                parsed.Add(new RagRow(hsCode, columns, searchableText));
            }

            _rows = parsed;
            _loadedFileWriteTimeUtc = writeTime;
            return (true, "Loaded", _rows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load HS code RAG workbook from {Path}", _options.FilePath);
            _rows = new List<RagRow>();
            _loadedFileWriteTimeUtc = DateTime.MinValue;
            return (false, $"Failed to read Excel file: {ex.Message}", 0);
        }
    }

    private static int Score(RagRow row, string query, List<string> tokens, string? formattedHs)
    {
        var score = 0;
        var lowerQuery = query.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(formattedHs))
        {
            if (string.Equals(row.HsCode, formattedHs, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
            else if (row.HsCode.StartsWith(formattedHs, StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }
        }

        if (row.SearchableText.Contains(lowerQuery, StringComparison.Ordinal))
        {
            score += 10;
        }

        foreach (var token in tokens)
        {
            if (row.SearchableText.Contains(token, StringComparison.Ordinal))
            {
                score += 2;
            }
        }

        return score;
    }

    private static Dictionary<string, string> FilterColumns(Dictionary<string, string> columns)
    {
        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in columns)
        {
            var value = pair.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (IsZeroLike(value))
            {
                continue;
            }

            output[pair.Key] = value;
        }

        return output;
    }

    private static bool IsZeroLike(string value)
    {
        var cleaned = value.Trim().Replace(",", string.Empty).Replace("%", string.Empty);
        if (cleaned.Length == 0)
        {
            return true;
        }

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed == 0;
    }

    private static string ResolvePath(string configuredPath, string contentRootPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }

    private static string NormalizeHeader(string header)
    {
        return new string(header.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private RagRow? FindBestByHsCode(string hsCode)
    {
        var formatted = FormatHsCode(hsCode);
        if (!string.IsNullOrWhiteSpace(formatted))
        {
            var exact = _rows.FirstOrDefault(r => string.Equals(r.HsCode, formatted, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        var digits = new string((hsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 6)
        {
            return null;
        }

        var global6 = digits[..6];
        var regional8 = digits.Length >= 8 ? digits[..8] : null;
        var country10 = digits.Length >= 10 ? digits[..10] : null;

        var candidates = _rows
            .Select(row => new
            {
                Row = row,
                Digits = new string(row.HsCode.Where(char.IsDigit).ToArray())
            })
            .Where(x => x.Digits.StartsWith(global6, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates
            .Select(x => new
            {
                x.Row,
                Score = ScoreHsCodePrefix(x.Digits, global6, regional8, country10)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Row.HsCode)
            .FirstOrDefault();

        return best?.Row;
    }

    private static int ScoreHsCodePrefix(string candidateDigits, string global6, string? regional8, string? country10)
    {
        var score = 0;
        if (candidateDigits.StartsWith(global6, StringComparison.Ordinal))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(regional8) && candidateDigits.StartsWith(regional8, StringComparison.Ordinal))
        {
            score += 20;
        }

        if (!string.IsNullOrWhiteSpace(country10) && candidateDigits.StartsWith(country10, StringComparison.Ordinal))
        {
            score += 40;
        }

        if (candidateDigits.Length >= 10 && candidateDigits[6] == '0' && candidateDigits[7] == '0' && candidateDigits[8] == '0' && candidateDigits[9] == '0')
        {
            score += 5;
        }

        return score;
    }

    private static List<string> Tokenize(string input)
    {
        return TokenRegex.Matches(input.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 1)
            .Distinct()
            .ToList();
    }

    public static string? FormatHsCode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        if (digits.Length >= 10)
        {
            digits = digits[..10];
            return $"{digits[..4]}.{digits.Substring(4, 2)}.{digits.Substring(6, 2)}.{digits.Substring(8, 2)}";
        }

        if (digits.Length > 8)
        {
            return $"{digits[..4]}.{digits.Substring(4, 2)}.{digits.Substring(6, 2)}.{digits[8..]}";
        }

        if (digits.Length > 6)
        {
            return $"{digits[..4]}.{digits.Substring(4, 2)}.{digits[6..]}";
        }

        if (digits.Length > 4)
        {
            return $"{digits[..4]}.{digits[4..]}";
        }

        return digits;
    }

    private sealed record RagRow(string HsCode, Dictionary<string, string> Columns, string SearchableText);
}
