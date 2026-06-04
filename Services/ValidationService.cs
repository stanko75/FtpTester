using System.ComponentModel.DataAnnotations;

namespace FtpTester.Services;

/// <summary>
/// Validates request models using data annotations and protocol-specific safeguards.
/// </summary>
public static class ValidationService
{
    /// <summary>Validates a request object and returns validation failures.</summary>
    public static IReadOnlyCollection<string> Validate<T>(T request)
    {
        if (request is null)
        {
            return ["Request body is required."];
        }

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);

        return results.Select(r => r.ErrorMessage ?? "Invalid input.").ToArray();
    }

    /// <summary>Returns true when a remote path appears safe for remote use.</summary>
    public static bool IsSafeRemotePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && !path.Contains('\0') && !path.Contains("..", StringComparison.Ordinal);
}
