using System.Diagnostics;
using FluentFTP;
using FtpTester.Models;

namespace FtpTester.Services;

/// <summary>
/// Implements FTP and FTPS operations using FluentFTP.
/// </summary>
public class FluentFtpService(ILogger<FluentFtpService> logger) : IFtpTestService
{
    /// <inheritdoc />
    public virtual TransferProtocol Protocol => TransferProtocol.Ftp;

    /// <summary>Creates an FTPS-capable wrapper that shares this implementation.</summary>
    public static IFtpTestService CreateFtps(ILogger<FluentFtpService> logger) => new FtpsFluentFtpService(logger);

    /// <inheritdoc />
    public async Task<TestResult> TestConnectionAsync(ConnectionRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = CreateClient(request);
            await client.Connect(cancellationToken);
            sw.Stop();
            logger.LogInformation("{Protocol} connection test succeeded for {Host}:{Port} in {ElapsedMs} ms", request.Protocol, request.Host, request.Port, sw.Elapsed.TotalMilliseconds);
            return Success("test-connection", request, sw.Elapsed, "Connection succeeded.");
        }
        catch (Exception ex)
        {
            return Failure("test-connection", request, sw.Elapsed, ex);
        }
    }

    /// <inheritdoc />
    public async Task<TestResult> UploadAsync(ConnectionRequest request, Stream fileStream, string remotePath, long length, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = CreateClient(request);
            await client.Connect(cancellationToken);
            var status = await client.UploadStream(fileStream, remotePath, FtpRemoteExists.Overwrite, createRemoteDir: true, token: cancellationToken);
            sw.Stop();
            var ok = status is FtpStatus.Success;
            return new TestResult
            {
                Operation = "upload",
                Protocol = request.Protocol,
                Host = request.Host,
                Success = ok,
                Message = ok ? "Upload completed." : $"Upload finished with status {status}.",
                DurationMilliseconds = sw.Elapsed.TotalMilliseconds,
                BytesTransferred = length,
                BytesPerSecond = CalculateBytesPerSecond(length, sw.Elapsed)
            };
        }
        catch (Exception ex)
        {
            return Failure("upload", request, sw.Elapsed, ex, length);
        }
    }

    /// <inheritdoc />
    public async Task<(TestResult Result, byte[] Content)> DownloadAsync(OperationRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = CreateClient(request);
            await client.Connect(cancellationToken);
            using var output = new MemoryStream();
            var ok = await client.DownloadStream(output, request.RemotePath, token: cancellationToken);
            sw.Stop();
            var content = output.ToArray();
            return (new TestResult
            {
                Operation = "download",
                Protocol = request.Protocol,
                Host = request.Host,
                Success = ok,
                Message = ok ? "Download completed." : "Download failed.",
                DurationMilliseconds = sw.Elapsed.TotalMilliseconds,
                BytesTransferred = content.Length,
                BytesPerSecond = CalculateBytesPerSecond(content.Length, sw.Elapsed)
            }, content);
        }
        catch (Exception ex)
        {
            return (Failure("download", request, sw.Elapsed, ex), Array.Empty<byte>());
        }
    }

    /// <inheritdoc />
    public async Task<TestResult> ListDirectoryAsync(OperationRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = CreateClient(request);
            await client.Connect(cancellationToken);
            var listing = await client.GetListing(request.RemotePath, cancellationToken);
            sw.Stop();
            return new TestResult
            {
                Operation = "list-directory",
                Protocol = request.Protocol,
                Host = request.Host,
                Success = true,
                Message = $"Listed {listing.Length} item(s).",
                DurationMilliseconds = sw.Elapsed.TotalMilliseconds,
                Items = listing.Select(item => new RemoteFileItem(item.Name, item.FullName, item.Type == FtpObjectType.Directory, item.Size, item.Modified == DateTime.MinValue ? null : new DateTimeOffset(DateTime.SpecifyKind(item.Modified, DateTimeKind.Utc)))).ToArray()
            };
        }
        catch (Exception ex)
        {
            return Failure("list-directory", request, sw.Elapsed, ex);
        }
    }

    /// <inheritdoc />
    public async Task<TestResult> DeleteFileAsync(OperationRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = CreateClient(request);
            await client.Connect(cancellationToken);
            await client.DeleteFile(request.RemotePath, cancellationToken);
            sw.Stop();
            return Success("delete-file", request, sw.Elapsed, "File deleted.");
        }
        catch (Exception ex)
        {
            return Failure("delete-file", request, sw.Elapsed, ex);
        }
    }


    private AsyncFtpClient CreateClient(ConnectionRequest request)
    {
        var client = new AsyncFtpClient(request.Host, request.Username, request.Password, request.Port);
        client.Config.EncryptionMode = request.Protocol == TransferProtocol.Ftps ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None;
        client.Config.DataConnectionType = request.PassiveMode ? FtpDataConnectionType.AutoPassive : FtpDataConnectionType.AutoActive;
        client.Config.ValidateAnyCertificate = false;
        return client;
    }

    private TestResult Success(string operation, ConnectionRequest request, TimeSpan duration, string message) => new()
    {
        Operation = operation,
        Protocol = request.Protocol,
        Host = request.Host,
        Success = true,
        Message = message,
        DurationMilliseconds = duration.TotalMilliseconds
    };

    private TestResult Failure(string operation, ConnectionRequest request, TimeSpan duration, Exception ex, long? bytes = null)
    {
        logger.LogError(ex, "{Protocol} {Operation} failed for {Host}:{Port}", request.Protocol, operation, request.Host, request.Port);
        return new TestResult
        {
            Operation = operation,
            Protocol = request.Protocol,
            Host = request.Host,
            Success = false,
            Message = "Operation failed.",
            Error = ex.Message,
            DurationMilliseconds = duration.TotalMilliseconds,
            BytesTransferred = bytes
        };
    }

    private static double CalculateBytesPerSecond(long bytes, TimeSpan duration) => duration.TotalSeconds <= 0 ? 0 : bytes / duration.TotalSeconds;

    private sealed class FtpsFluentFtpService(ILogger<FluentFtpService> logger) : FluentFtpService(logger)
    {
        public override TransferProtocol Protocol => TransferProtocol.Ftps;
    }
}
