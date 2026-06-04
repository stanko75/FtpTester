using FtpTester.Models;

namespace FtpTester.Services;

/// <summary>
/// Resolves the protocol-specific transfer service.
/// </summary>
public sealed class ServiceSelector(IEnumerable<IFtpTestService> services)
{
    private readonly IReadOnlyDictionary<TransferProtocol, IFtpTestService> _services = services.ToDictionary(s => s.Protocol);

    /// <summary>Gets the service that supports the requested protocol.</summary>
    public IFtpTestService Resolve(TransferProtocol protocol) =>
        _services.TryGetValue(protocol, out var service)
            ? service
            : throw new InvalidOperationException($"No service is registered for protocol '{protocol}'.");
}
