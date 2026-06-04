namespace FtpTester.Models;

/// <summary>
/// Composite benchmark result containing upload and download metrics.
/// </summary>
public sealed class BenchmarkResult
{
    /// <summary>Unique benchmark identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Protocol used by the benchmark.</summary>
    public TransferProtocol Protocol { get; init; }

    /// <summary>Remote host.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Whether the full benchmark succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Upload operation result.</summary>
    public TestResult? Upload { get; init; }

    /// <summary>Download operation result.</summary>
    public TestResult? Download { get; init; }

    /// <summary>Total benchmark duration in milliseconds.</summary>
    public double DurationMilliseconds { get; init; }

    /// <summary>Benchmark timestamp.</summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
