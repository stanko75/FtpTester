using System.Diagnostics;
using FtpTester.Models;

namespace FtpTester.Services;

/// <summary>
/// Coordinates benchmark upload, download, and cleanup operations.
/// </summary>
public sealed class BenchmarkService(ServiceSelector selector, TestHistoryStore history, ILogger<BenchmarkService> logger)
{
    /// <summary>Runs an upload/download benchmark against a remote server.</summary>
    public async Task<BenchmarkResult> RunAsync(BenchmarkRequest request, CancellationToken cancellationToken)
    {
        var service = selector.Resolve(request.Protocol);
        var sw = Stopwatch.StartNew();
        var remotePath = CombineRemotePath(request.RemoteDirectory, $"ftp-tester-benchmark-{Guid.NewGuid():N}.bin");
        var payload = CreatePayload(request.FileSizeBytes);

        TestResult? upload = null;
        TestResult? download = null;
        try
        {
            await using var uploadStream = new MemoryStream(payload, writable: false);
            upload = await service.UploadAsync(request, uploadStream, remotePath, payload.LongLength, cancellationToken);
            if (upload.Success)
            {
                download = (await service.DownloadAsync(new OperationRequest
                {
                    Protocol = request.Protocol,
                    Host = request.Host,
                    Port = request.Port,
                    Username = request.Username,
                    Password = request.Password,
                    PassiveMode = request.PassiveMode,
                    RemotePath = remotePath
                }, cancellationToken)).Result;
            }
        }
        finally
        {
            try
            {
                await service.DeleteFileAsync(new OperationRequest
                {
                    Protocol = request.Protocol,
                    Host = request.Host,
                    Port = request.Port,
                    Username = request.Username,
                    Password = request.Password,
                    PassiveMode = request.PassiveMode,
                    RemotePath = remotePath
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete benchmark artifact {RemotePath}", remotePath);
            }
        }

        sw.Stop();
        var result = new BenchmarkResult
        {
            Protocol = request.Protocol,
            Host = request.Host,
            Upload = upload,
            Download = download,
            Success = upload?.Success == true && download?.Success == true,
            DurationMilliseconds = sw.Elapsed.TotalMilliseconds
        };
        history.Add(result);
        return result;
    }

    private static byte[] CreatePayload(long size)
    {
        var payload = new byte[checked((int)size)];
        Random.Shared.NextBytes(payload);
        return payload;
    }

    private static string CombineRemotePath(string directory, string fileName)
    {
        var normalized = string.IsNullOrWhiteSpace(directory) ? "/" : directory.Trim();
        return normalized.EndsWith('/') ? $"{normalized}{fileName}" : $"{normalized}/{fileName}";
    }
}
