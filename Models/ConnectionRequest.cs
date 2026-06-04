using System.ComponentModel.DataAnnotations;

namespace FtpTester.Models;

/// <summary>
/// Connection settings supplied by the dashboard for FTP, FTPS, and SFTP operations.
/// </summary>
public class ConnectionRequest
{
    /// <summary>Protocol used for the operation.</summary>
    [Required]
    public TransferProtocol Protocol { get; init; }

    /// <summary>Remote host name or IP address.</summary>
    [Required, StringLength(255, MinimumLength = 1)]
    public string Host { get; init; } = string.Empty;

    /// <summary>Remote server port.</summary>
    [Range(1, 65535)]
    public int Port { get; init; }

    /// <summary>User name used to authenticate to the remote server.</summary>
    [Required, StringLength(255, MinimumLength = 1)]
    public string Username { get; init; } = string.Empty;

    /// <summary>Password used to authenticate to the remote server.</summary>
    [Required, StringLength(4096, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;

    /// <summary>Whether passive mode should be used for FTP and FTPS.</summary>
    public bool PassiveMode { get; init; } = true;
}
