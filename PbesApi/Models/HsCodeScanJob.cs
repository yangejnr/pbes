namespace PbesApi.Models;

public enum HsCodeScanJobStatus
{
    Pending,
    Completed,
    Failed
}

public class HsCodeScanJob
{
    public HsCodeScanJob(Guid id, DateTimeOffset createdAt, string? requestId = null)
    {
        Id = id;
        CreatedAt = createdAt;
        RequestId = requestId;
    }

    public Guid Id { get; }
    public string? RequestId { get; }
    public HsCodeScanJobStatus Status { get; set; } = HsCodeScanJobStatus.Pending;
    public HsCodeScanResponse? Result { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? CompletedAt { get; set; }
}
