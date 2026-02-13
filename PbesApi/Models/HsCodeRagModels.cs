namespace PbesApi.Models;

public record HsCodeRagRowResponse(Dictionary<string, string> Columns);

public record HsCodeRagSearchResponse(int Total, List<HsCodeRagRowResponse> Rows, string? Note = null);

public record HsCodeRagSearchRequest(string Query, int TopK = 5);
