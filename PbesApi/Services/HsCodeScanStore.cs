using System.Collections.Concurrent;
using PbesApi.Models;

namespace PbesApi.Services;

public class HsCodeScanStore
{
    private readonly ConcurrentQueue<RecentHsCodeEntry> _recent = new();
    private readonly ConcurrentDictionary<Guid, HsCodeScanJob> _jobs = new();
    private readonly object _lock = new();
    private readonly TimeSpan _jobTtl = TimeSpan.FromMinutes(30);

    public void Add(RecentHsCodeEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.HsCode))
        {
            return;
        }

        lock (_lock)
        {
            _recent.Enqueue(entry);
            while (_recent.Count > 10)
            {
                _recent.TryDequeue(out _);
            }
        }
    }

    public List<RecentHsCodeEntry> GetRecent()
    {
        lock (_lock)
        {
            return _recent.Reverse().ToList();
        }
    }

    public HsCodeScanJob CreateJob(string? requestId = null)
    {
        CleanupJobs();
        var job = new HsCodeScanJob(Guid.NewGuid(), DateTimeOffset.UtcNow, requestId);
        _jobs[job.Id] = job;
        return job;
    }

    public bool TryGetJob(Guid jobId, out HsCodeScanJob job)
    {
        CleanupJobs();
        return _jobs.TryGetValue(jobId, out job!);
    }

    public void CompleteJob(Guid jobId, HsCodeScanResponse response)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = HsCodeScanJobStatus.Completed;
            job.Result = response;
            job.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    public void FailJob(Guid jobId, string error)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = HsCodeScanJobStatus.Failed;
            job.Error = error;
            job.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    private void CleanupJobs()
    {
        var cutoff = DateTimeOffset.UtcNow - _jobTtl;
        foreach (var entry in _jobs)
        {
            if (entry.Value.CreatedAt < cutoff)
            {
                _jobs.TryRemove(entry.Key, out _);
            }
        }
    }
}
