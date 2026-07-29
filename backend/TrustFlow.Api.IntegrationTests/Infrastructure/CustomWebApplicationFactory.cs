using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
                    "https://localhost"
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