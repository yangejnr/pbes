namespace PbesApi.Models;

public record HsCodeSubsection(string HsCode, string Title, string Notes);

public record HsCodeMatch(
    string HsCode,
    string Description,
    double MatchPercent,
    string Comment,
    List<HsCodeSubsection> Subsections);

public record RecentHsCodeEntry(string HsCode, string Description);

public record HsCodeModelResponse(List<HsCodeMatch> Matches, string? Note);

public record HsCodeScanResponse(List<HsCodeMatch> Matches, string? Note, List<RecentHsCodeEntry> RecentHsCodes);
