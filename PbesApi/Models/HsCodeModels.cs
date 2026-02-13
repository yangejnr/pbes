namespace PbesApi.Models;

public record HsCodeSubsection(string HsCode, string Title, string Notes);

public record HsCodeMatch(
    string HsCode,
    string Description,
    double MatchPercent,
    string Comment,
    List<HsCodeSubsection> Subsections,
    Dictionary<string, string>? RagColumns = null,
    bool RagValidated = false);

public record RecentHsCodeEntry(string HsCode, string Description);

public record HsCodeModelResponse(List<HsCodeMatch> Matches, string? Note);

public record HsCodeScanResponse(List<HsCodeMatch> Matches, string? Note, List<RecentHsCodeEntry> RecentHsCodes);
