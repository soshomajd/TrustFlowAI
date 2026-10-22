using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

// TestServer has no socket peer. Only the test host accepts this override.
public sealed class TestPeerAddress : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, proceed) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.TryParse(context.Request.Headers["X-Test-Peer-IP"], out var peer) ? peer : IPAddress.Loopback;
            return proceed(context);
        });
        next(app);
    };
}
