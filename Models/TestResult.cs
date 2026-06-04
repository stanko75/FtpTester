namespace FtpTester.Models;

/// <summary>
/// Result returned by each transfer test operation.
/// </summary>
public sealed class TestResult
{
    /// <summary>Unique result identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Operation name.</summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>Protocol used by the operation.</summary>
    public TransferProtocol Protocol { get; init; }

    /// <summary>Remote host.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Human-readable message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Error details when the operation fails.</summary>
    public string? Error { get; init; }

    /// <summary>Operation duration in milliseconds.</summary>
    public double DurationMilliseconds { get; init; }

    /// <summary>Bytes transferred, if applicable.</summary>
    public long? BytesTransferred { get; init; }

    /// <summary>Transfer speed in bytes per second, if applicable.</summary>
    public double? BytesPerSecond { get; init; }

    /// <summary>Remote directory entries, if applicable.</summary>
    public IReadOnlyCollection<RemoteFileItem> Items { get; init; } = Array.Empty<RemoteFileItem>();

    /// <summary>Timestamp when the result was recorded.</summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
