using System.Collections.Concurrent;
using FtpTester.Models;

namespace FtpTester.Services;

/// <summary>
/// In-memory bounded history store suitable for stateless Azure App Service deployments.
/// </summary>
public sealed class TestHistoryStore
{
    private const int MaxEntries = 200;
    private readonly ConcurrentQueue<object> _entries = new();

    /// <summary>Adds a result to the history store.</summary>
    public void Add(object result)
    {
        _entries.Enqueue(result);
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _)) { }
    }

    /// <summary>Returns the most recent test history entries.</summary>
    public IReadOnlyCollection<object> GetAll() => _entries.Reverse().ToArray();
}
