namespace FtpTester.Models;

/// <summary>
/// Supported remote transfer protocols.
/// </summary>
public enum TransferProtocol
{
    /// <summary>Plain FTP.</summary>
    Ftp,
    /// <summary>FTP over TLS.</summary>
    Ftps,
    /// <summary>SSH File Transfer Protocol.</summary>
    Sftp
}
