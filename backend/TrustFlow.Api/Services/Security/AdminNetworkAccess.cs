using System.Net;

namespace TrustFlow.Api.Services.Security;

// Read only the socket peer. Forwarded headers are not an authentication factor.
public sealed class AdminNetworkAccess(IConfiguration configuration)
{
    private readonly IPNetwork[] _networks = Parse(configuration);

    public bool IsAllowed(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress;
        if (peer is null) return false;
        if (peer.IsIPv4MappedToIPv6) peer = peer.MapToIPv4();
        // Fail closed for a forwarding deployment until its private gateway and
        // trusted-peer configuration have been designed explicitly.
        if (context.Request.Headers.ContainsKey("Forwarded") || context.Request.Headers.ContainsKey("X-Forwarded-For")) return false;
        return _networks.Any(network => network.Contains(peer));
    }

    public static bool IsManagementPath(PathString path) => path.StartsWithSegments("/api/admin")
        || path.StartsWithSegments("/api/review") || path.StartsWithSegments("/api/auth/admin");

    private static IPNetwork[] Parse(IConfiguration configuration)
    {
        var values = configuration.GetSection("AdminSecurity:AllowedNetworks").Get<string[]>()
            ?? ["127.0.0.1/32", "::1/128"];
        if (values.Length == 0) throw new InvalidOperationException("At least one private admin network is required.");
        return values.Select(value => IPNetwork.TryParse(value, out var network) && network.PrefixLength > 0
            ? network : throw new InvalidOperationException("Admin networks must be explicit valid CIDRs; unrestricted networks are refused.")).ToArray();
    }
}
