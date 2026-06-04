using FtpTester.Models;

namespace FtpTester.Services;

/// <summary>
/// Common abstraction for FTP-compatible test operations.
/// </summary>
public interface IFtpTestService
{
    /// <summary>Gets the protocol handled by the service.</summary>
    TransferProtocol Protocol { get; }

    /// <summary>Tests authentication and connectivity.</summary>
    Task<TestResult> TestConnectionAsync(ConnectionRequest request, CancellationToken cancellationToken);

    /// <summary>Uploads a stream to a remote path.</summary>
    Task<TestResult> UploadAsync(ConnectionRequest request, Stream fileStream, string remotePath, long length, CancellationToken cancellationToken);

    /// <summary>Downloads a remote file into memory.</summary>
    Task<(TestResult Result, byte[] Content)> DownloadAsync(OperationRequest request, CancellationToken cancellationToken);

    /// <summary>Lists a remote directory.</summary>
    Task<TestResult> ListDirectoryAsync(OperationRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes a remote file.</summary>
    Task<TestResult> DeleteFileAsync(OperationRequest request, CancellationToken cancellationToken);
}
