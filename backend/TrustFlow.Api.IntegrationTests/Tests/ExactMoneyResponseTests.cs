using System.Globalization;
using System.Text.Json;
using TrustFlow.Api.Dtos.Escrows;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class ExactMoneyResponseTests
{
    [Fact]
    public void FinancialResponseFields_PreserveExactDecimalsAndNumericCompatibility()
    {
        var monetaryNames = new HashSet<string>
        {
            "Budget", "AllocatedAmount", "AvailableAmount", "TotalBudget", "MyTotalAmount",
            "Amount", "BidAmount", "AssignedTotal", "TotalAmount", "ReleasedAmount",
            "PlatformFeesPaid", "PlatformFeePaid", "FreelancerNetPaid", "PaidAmount",
            "MilestoneAmount", "PaidToFreelancer", "ReturnedToClient", "FreelancerAmount", "ClientAmount"
        };
        var types = typeof(EscrowResponse).Assembly.GetTypes().Where(type =>
            type.Namespace?.StartsWith("TrustFlow.Api.Dtos.") == true &&
            !type.Name.EndsWith("Request") && !type.Name.EndsWith("Query") &&
            type.GetProperties().Any(property => monetaryNames.Contains(property.Name)));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var previousCulture = CultureInfo.CurrentCulture;
        var checkedFields = 0;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fa-IR");
            foreach (var type in types)
            foreach (var property in type.GetProperties().Where(property => monetaryNames.Contains(property.Name)))
            {
                var companion = type.GetProperty(property.Name + "Exact");
                Assert.NotNull(companion);
                foreach (var value in new[] { 0m, 0.000001m, 9007199254740991.01m, decimal.MaxValue, 0.0000000000000000000000000001m })
                {
                    var response = Activator.CreateInstance(type)!;
                    property.SetValue(response, value);
                    var json = JsonSerializer.SerializeToElement(response, type, options);
                    var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                    Assert.Equal(JsonValueKind.Number, json.GetProperty(name).ValueKind);
                    Assert.Equal(value, json.GetProperty(name).GetDecimal());
                    Assert.Equal(value.ToString(CultureInfo.InvariantCulture), json.GetProperty(name + "Exact").GetString());
                }
                if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
                {
                    var json = JsonSerializer.SerializeToElement(Activator.CreateInstance(type), type, options);
                    Assert.Equal(JsonValueKind.Null, json.GetProperty(JsonNamingPolicy.CamelCase.ConvertName(companion.Name)).ValueKind);
                }
                checkedFields++;
            }
            Assert.True(checkedFields >= 35);
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }
}
