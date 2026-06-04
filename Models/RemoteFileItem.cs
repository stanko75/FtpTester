namespace FtpTester.Models;

/// <summary>
/// Represents one remote directory listing entry.
/// </summary>
public sealed record RemoteFileItem(string Name, string FullPath, bool IsDirectory, long Size, DateTimeOffset? ModifiedUtc);
