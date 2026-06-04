using System.ComponentModel.DataAnnotations;

namespace FtpTester.Models;

/// <summary>
/// Request model for remote operations that target a path.
/// </summary>
public sealed class OperationRequest : ConnectionRequest
{
    /// <summary>Remote file or directory path.</summary>
    [Required, StringLength(4096, MinimumLength = 1)]
    public string RemotePath { get; init; } = string.Empty;
}
