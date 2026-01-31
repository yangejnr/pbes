using System.Collections.Concurrent;
using PbesApi.Models;

namespace PbesApi.Services;

public class HsCodeScanStore
{
    private readonly ConcurrentQueue<RecentHsCodeEntry> _recent = new();
    private readonly object _lock = new();

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
}
