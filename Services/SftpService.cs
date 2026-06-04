using System.Diagnostics;
using FtpTester.Models;
using Renci.SshNet;

namespace FtpTester.Services;

/// <summary>
/// Implements SFTP operations using SSH.NET.
/// </summary>
public sealed class SftpService(ILogger<SftpService> logger) : IFtpTestService
{
    /// <inheritdoc />
    public TransferProtocol Protocol => TransferProtocol.Sftp;

    /// <inheritdoc />
    public async Task<TestResult> TestConnectionAsync(ConnectionRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await Task.Run(() =>
            {
                using var client = CreateClient(request);
                client.Connect();
                client.Disconnect();
            }, cancellationToken);
            sw.Stop();
            logger.LogInformation("SFTP connection test succeeded for {Host}:{Port} in {ElapsedMs} ms", request.Host, request.Port, sw.Elapsed.TotalMilliseconds);
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
            await Task.Run(() =>
            {
                using var client = CreateClient(request);
                client.Connect();
                EnsureRemoteDirectory(client, remotePath);
                client.UploadFile(fileStream, remotePath, canOverride: true);
                client.Disconnect();
            }, cancellationToken);
            sw.Stop();
            return new TestResult
            {
                Operation = "upload",
                Protocol = Protocol,
                Host = request.Host,
                Success = true,
                Message = "Upload completed.",
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
            var content = await Task.Run(() =>
            {
                using var client = CreateClient(request);
                using var output = new MemoryStream();
                client.Connect();
                client.DownloadFile(request.RemotePath, output);
                client.Disconnect();
                return output.ToArray();
            }, cancellationToken);
            sw.Stop();
            return (new TestResult
            {
                Operation = "download",
                Protocol = Protocol,
                Host = request.Host,
                Success = true,
                Message = "Download completed.",
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
            var items = await Task.Run(() =>
            {
                using var client = CreateClient(request);
                client.Connect();
                var listing = client.ListDirectory(request.RemotePath)
                    .Where(file => file.Name is not "." and not "..")
                    .Select(file => new RemoteFileItem(file.Name, file.FullName, file.IsDirectory, file.Length, file.LastWriteTimeUtc == DateTime.MinValue ? null : new DateTimeOffset(DateTime.SpecifyKind(file.LastWriteTimeUtc, DateTimeKind.Utc))))
                    .ToArray();
                client.Disconnect();
                return listing;
            }, cancellationToken);
            sw.Stop();
            return new TestResult
            {
                Operation = "list-directory",
                Protocol = Protocol,
                Host = request.Host,
                Success = true,
                Message = $"Listed {items.Length} item(s).",
                DurationMilliseconds = sw.Elapsed.TotalMilliseconds,
                Items = items
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
            await Task.Run(() =>
            {
                using var client = CreateClient(request);
                client.Connect();
                client.DeleteFile(request.RemotePath);
                client.Disconnect();
            }, cancellationToken);
            sw.Stop();
            return Success("delete-file", request, sw.Elapsed, "File deleted.");
        }
        catch (Exception ex)
        {
            return Failure("delete-file", request, sw.Elapsed, ex);
        }
    }

    private static SftpClient CreateClient(ConnectionRequest request)
    {
        var connectionInfo = new ConnectionInfo(request.Host, request.Port, request.Username, new PasswordAuthenticationMethod(request.Username, request.Password))
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        return new SftpClient(connectionInfo);
    }

    private static void EnsureRemoteDirectory(SftpClient client, string remotePath)
    {
        var directory = remotePath.Contains('/') ? remotePath[..remotePath.LastIndexOf('/')] : string.Empty;
        if (string.IsNullOrWhiteSpace(directory) || directory == "/")
        {
            return;
        }

        var current = directory.StartsWith('/') ? "/" : string.Empty;
        foreach (var part in directory.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current == "/" ? $"/{part}" : $"{current}/{part}";
            if (!client.Exists(current))
            {
                client.CreateDirectory(current);
            }
        }
    }

    private TestResult Success(string operation, ConnectionRequest request, TimeSpan duration, string message) => new()
    {
        Operation = operation,
        Protocol = Protocol,
        Host = request.Host,
        Success = true,
        Message = message,
        DurationMilliseconds = duration.TotalMilliseconds
    };

    private TestResult Failure(string operation, ConnectionRequest request, TimeSpan duration, Exception ex, long? bytes = null)
    {
        logger.LogError(ex, "SFTP {Operation} failed for {Host}:{Port}", operation, request.Host, request.Port);
        return new TestResult
        {
            Operation = operation,
            Protocol = Protocol,
            Host = request.Host,
            Success = false,
            Message = "Operation failed.",
            Error = ex.Message,
            DurationMilliseconds = duration.TotalMilliseconds,
            BytesTransferred = bytes
        };
    }

    private static double CalculateBytesPerSecond(long bytes, TimeSpan duration) => duration.TotalSeconds <= 0 ? 0 : bytes / duration.TotalSeconds;
}
