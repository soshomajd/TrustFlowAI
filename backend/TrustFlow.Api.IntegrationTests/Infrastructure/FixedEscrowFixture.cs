using Npgsql;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

// Old fixed-array contract regressions deliberately retain their historical
// lifecycle. New API projects use V3 and are exercised in FundedHiringTests.
public static class FixedEscrowFixture
{
    public static async Task UseAsync(Guid projectId)
    {
        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("TRUSTFLOW_TEST_DB"));
        if (string.IsNullOrWhiteSpace(connection.Database) || connection.Database == "trustflow_dev")
            throw new InvalidOperationException("Legacy fixtures require the isolated integration database.");
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE \"Projects\" SET \"HiringFlowVersion\" = 2 WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", projectId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}
