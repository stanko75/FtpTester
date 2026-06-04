using System.ComponentModel.DataAnnotations;

namespace FtpTester.Models;

/// <summary>
/// Request used to benchmark upload and download throughput.
/// </summary>
public sealed class BenchmarkRequest : ConnectionRequest
{
    /// <summary>Remote directory where the temporary benchmark file will be created.</summary>
    [Required, StringLength(4096, MinimumLength = 1)]
    public string RemoteDirectory { get; init; } = "/";

    /// <summary>Benchmark payload size in bytes.</summary>
    [Range(1024, 104857600)]
    public long FileSizeBytes { get; init; } = 1048576;
}
