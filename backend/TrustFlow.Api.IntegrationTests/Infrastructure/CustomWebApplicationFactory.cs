using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TrustFlow.Api.Blockchain;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

public sealed class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private const string TestDatabaseEnvironmentVariable =
        "TRUSTFLOW_TEST_DB";

    protected override IHost CreateHost(
        IHostBuilder builder)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                TestDatabaseEnvironmentVariable
            );

        if (string.IsNullOrWhiteSpace(
                connectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable " +
                $"'{TestDatabaseEnvironmentVariable}' " +
                "is missing."
            );
        }

        var testSettings =
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    connectionString,

                ["Jwt:Issuer"] =
                    "TrustFlow.Api.IntegrationTests",

                ["Jwt:Audience"] =
                    "TrustFlow.Api.IntegrationTests.Client",

                ["Jwt:Key"] =
                    "TrustFlow.IntegrationTests.Secret.Key." +
                    "Must.Be.At.Least.32.Bytes.Long",

                ["Jwt:ExpirationMinutes"] = "15",

                ["Jwt:RefreshTokenExpirationDays"] = "30",

                ["Cors:AllowedOrigins:0"] =
                    "https://localhost",

                ["Blockchain:ChainId"] = "31337",

                ["Blockchain:RpcUrl"] =
                    "http://127.0.0.1:8545",

                ["Blockchain:DeployerPrivateKey"] =
                    // Well-known local Hardhat/Anvil test account #0 key.
                    // Not a real secret; used only so config
                    // validation passes when tests never call the chain.
                    "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"
            };

        builder.ConfigureHostConfiguration(
            configuration =>
            {
                configuration.AddInMemoryCollection(
                    testSettings
                );
            }
        );

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseContentRoot(
            FindApiProjectDirectory()
        );

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEscrowChainDeployer>();

            services.AddScoped<
                IEscrowChainDeployer,
                FakeEscrowChainDeployer
            >();
        });
    }

    private static string FindApiProjectDirectory()
    {
        var currentDirectory =
            new DirectoryInfo(
                AppContext.BaseDirectory
            );

        while (currentDirectory is not null)
        {
            var apiProjectFile = Path.Combine(
                currentDirectory.FullName,
                "TrustFlow.Api",
                "TrustFlow.Api.csproj"
            );

            if (File.Exists(apiProjectFile))
            {
                return Path.GetDirectoryName(
                    apiProjectFile
                )!;
            }

            currentDirectory =
                currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            "TrustFlow.Api project directory " +
            "could not be found."
        );
    }
}