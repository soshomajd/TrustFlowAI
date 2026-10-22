using System.Net;
using System.Text.Json;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using TrustFlow.Api.Services.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Admin;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class AdminFinanceTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
    private static string Url(Guid project, Guid? escrow = null, string section = "payouts") =>
        $"/api/admin/projects/{project}/finance" + (escrow.HasValue ? $"/{escrow}/{section}" : "");

    [Fact]
    public async Task AnonymousAndParticipantsCannotReadFinanceOrEntries()
    {
        foreach (var person in new[] { await NewPersonAsync(client, "Client", "Owner"), await NewPersonAsync(client, "Freelancer", "Talent") })
            foreach (var url in new[] { Url(Guid.NewGuid()), Url(Guid.NewGuid(), Guid.NewGuid()), Url(Guid.NewGuid(), Guid.NewGuid(), "settlements"), Url(Guid.NewGuid(), Guid.NewGuid(), "report.csv") })
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, url, person.Token)).StatusCode);
            }
    }

    [Fact]
    public async Task ExactTotalsAndOrdinaryPayoutsDoNotInventHashesOrTimes_AndReadIsAudited()
    {
        var s = await SeedAsync();
        var row = Assert.Single((await GetAsync(s, Url(s.Project.Id))).GetProperty("items").EnumerateArray());
        Money(row, "grossReleasedExact", 100); Money(row, "platformFeesPaidExact", 10); Money(row, "freelancerNetPaidExact", 90);
        Money(row, "fundingAmountExact", 100); Money(row, "remainingRecordedAmountExact", 0);
        Assert.Empty(row.GetProperty("gaps").EnumerateArray());
        var payout = Assert.Single((await GetAsync(s, Url(s.Project.Id, s.Escrow.Id))).GetProperty("items").EnumerateArray());
        Money(payout, "grossAmountExact", 100); Money(payout, "platformFeePaidExact", 10); Money(payout, "freelancerNetPaidExact", 90);
        Assert.Equal(JsonValueKind.Null, payout.GetProperty("recordedAt").ValueKind);
        Assert.False(payout.TryGetProperty("transactionHash", out _));
        Assert.Equal(JsonValueKind.Null, payout.GetProperty("caseId").ValueKind);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.AdminAccessLogs.Where(item => item.ProjectId == s.Project.Id).ToListAsync();
        Assert.All(logs, log => Assert.Equal(s.Admin.Id, log.AdminId));
        Assert.Contains(logs, log => log.Action == "AdminFinanceViewedRecord" && log.ResourceId == s.Escrow.Id);
        Assert.Contains(logs, log => log.Action == "AdminFinancePayoutsViewedRecord" && log.ResourceId == s.Stage.Id);
        var saved = await db.Escrows.SingleAsync(item => item.Id == s.Escrow.Id);
        Assert.Equal(100, saved.ReleasedAmount); Assert.Equal(10, saved.PlatformFeesPaid); Assert.Equal(EscrowStatus.Completed, saved.Status);
    }

    [Fact]
    public async Task SettledCaseIsScopedAndUsesMatchingRecordedMilestoneFee_WithoutDoubleCounting()
    {
        var s = await SeedAsync(); var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var escrow = await db.Escrows.SingleAsync(item => item.Id == s.Escrow.Id);
            escrow.Status = EscrowStatus.Funded; escrow.ReleasedAmount = 20; escrow.PlatformFeesPaid = 2;
            var stage = await db.Milestones.SingleAsync(item => item.Id == s.Stage.Id);
            stage.Status = MileStoneStatus.Settled; stage.PaidAmount = 20; stage.PlatformFeePaid = 2;
            db.EscrowCases.Add(new EscrowCase { EscrowId = escrow.Id, MilestoneId = stage.Id, OpenedAt = now.AddDays(-15),
                ClosedAt = now, Status = EscrowCaseStatus.Settled, PaidToFreelancer = 20, PlatformFeePaid = 0, ReturnedToClient = 30,
                ReviewNote = "Private opinion", AdviceJson = "Private advice" });
            await db.SaveChangesAsync();
        }
        var row = Assert.Single((await GetAsync(s, Url(s.Project.Id))).GetProperty("items").EnumerateArray());
        Money(row, "grossReleasedExact", 20); Money(row, "recordedCaseRefundsExact", 30); Money(row, "remainingRecordedAmountExact", 50);
        var result = Assert.Single((await GetAsync(s, Url(s.Project.Id, s.Escrow.Id, "settlements"))).GetProperty("items").EnumerateArray());
        Money(result, "grossAmountExact", 20); Money(result, "platformFeePaidExact", 2); Money(result, "freelancerNetPaidExact", 18); Money(result, "returnedToClientExact", 30);
        Assert.False(result.TryGetProperty("reviewNote", out _)); Assert.False(result.TryGetProperty("adviceJson", out _));
        var payout = Assert.Single((await GetAsync(s, Url(s.Project.Id, s.Escrow.Id))).GetProperty("items").EnumerateArray());
        Assert.Equal(result.GetProperty("id").GetGuid(), payout.GetProperty("caseId").GetGuid());
        Assert.NotEqual(JsonValueKind.Null, payout.GetProperty("recordedAt").ValueKind);
    }

    [Fact]
    public async Task FundedCancellationHasFullRefund_ButUnfundedOrUntracedTerminationStaysUnknown()
    {
        var s = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var e = await db.Escrows.SingleAsync(item => item.Id == s.Escrow.Id);
            e.Status = EscrowStatus.Cancelled; e.ReleasedAmount = 0; e.PlatformFeesPaid = 0; e.CancellationTransactionHash = "verified-cancel";
            var stage = await db.Milestones.SingleAsync(item => item.Id == s.Stage.Id); stage.Status = MileStoneStatus.Pending; stage.PaidAmount = null; stage.PlatformFeePaid = 0;
            await db.SaveChangesAsync();
        }
        var row = Assert.Single((await GetAsync(s, Url(s.Project.Id))).GetProperty("items").EnumerateArray());
        Money(row, "cancellationRefundExact", 100); Money(row, "remainingRecordedAmountExact", 0);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var e = await db.Escrows.SingleAsync(item => item.Id == s.Escrow.Id);
            e.FundedAt = null; e.FundingTransactionHash = null; await db.SaveChangesAsync();
        }
        row = Assert.Single((await GetAsync(s, Url(s.Project.Id))).GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("cancellationRefundExact").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("fundingAmountExact").ValueKind);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var e = await db.Escrows.SingleAsync(item => item.Id == s.Escrow.Id);
            e.Status = EscrowStatus.Terminated; e.FundedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        }
        row = Assert.Single((await GetAsync(s, Url(s.Project.Id))).GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("remainingRecordedAmountExact").ValueKind);
        Assert.Contains(row.GetProperty("gaps").EnumerateArray(), gap => gap.GetString()!.Contains("termination refunds"));
    }

    [Fact]
    public async Task UnknownLegacyPayoutAndCaseAmountsAreNotConvertedToZero()
    {
        var s = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stage = await db.Milestones.SingleAsync(item => item.Id == s.Stage.Id); stage.PaidAmount = null;
            var e = await db.Escrows.SingleAsync(item => item.Id == s.Escrow.Id); e.Status = EscrowStatus.Funded;
            db.EscrowCases.Add(new EscrowCase { EscrowId = e.Id, MilestoneId = stage.Id, OpenedAt = DateTimeOffset.UtcNow, Status = EscrowCaseStatus.Settled });
            await db.SaveChangesAsync();
        }
        var row = Assert.Single((await GetAsync(s, Url(s.Project.Id))).GetProperty("items").EnumerateArray());
        Assert.Equal(1, row.GetProperty("unknownCaseRefundCount").GetInt32()); Assert.Equal(1, row.GetProperty("unknownPayoutAmountCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("remainingRecordedAmountExact").ValueKind);
        var payout = Assert.Single((await GetAsync(s, Url(s.Project.Id, s.Escrow.Id))).GetProperty("items").EnumerateArray());
        foreach (var key in new[] { "grossAmountExact", "platformFeePaidExact", "freelancerNetPaidExact" }) Assert.Equal(JsonValueKind.Null, payout.GetProperty(key).ValueKind);
        var settlement = Assert.Single((await GetAsync(s, Url(s.Project.Id, s.Escrow.Id, "settlements"))).GetProperty("items").EnumerateArray());
        foreach (var key in new[] { "grossAmountExact", "platformFeePaidExact", "freelancerNetPaidExact", "returnedToClientExact" }) Assert.Equal(JsonValueKind.Null, settlement.GetProperty(key).ValueKind);
    }

    [Fact]
    public async Task NetworkIdentityPaginationAndProjectBoundariesArePreserved()
    {
        var s = await SeedAsync(); var otherTalent = await NewPersonAsync(client, "Freelancer", "Other chain talent");
        Guid foreignProject; Guid foreignEscrow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Escrows.Add(new Escrow { ProjectId = s.Project.Id, FreelancerId = otherTalent.Id, ChainId = 1, TokenAddress = "different-token", TotalAmount = 9007199254740991.01m });
            var other = new Project { ClientId = s.Owner.Id, Title = "Unrelated", Deadline = DateTimeOffset.UtcNow, Budget = 1 };
            var e = new Escrow { ProjectId = other.Id, FreelancerId = otherTalent.Id, ChainId = 1, TokenAddress = "hidden", TotalAmount = 1 };
            db.Projects.Add(other); db.Escrows.Add(e); await db.SaveChangesAsync(); foreignProject = other.Id; foreignEscrow = e.Id;
        }
        var a = await GetAsync(s, Url(s.Project.Id) + "?pageSize=1");
        var b = await GetAsync(s, Url(s.Project.Id) + "?pageSize=1&page=2");
        Assert.Equal(2, a.GetProperty("totalItems").GetInt32());
        var rows = new[] { Assert.Single(a.GetProperty("items").EnumerateArray()), Assert.Single(b.GetProperty("items").EnumerateArray()) };
        Assert.Equal(2, rows.Select(row => row.GetProperty("id").GetGuid()).Distinct().Count());
        Assert.Contains(rows, row => row.GetProperty("chainId").GetInt64() == 1 && row.GetProperty("totalAmountExact").GetString() == "9007199254740991.01");
        foreach (var url in new[] { Url(s.Project.Id, foreignEscrow), Url(foreignProject, s.Escrow.Id), Url(s.Project.Id, s.Escrow.Id, "anything"), Url(Guid.NewGuid()), Url(s.Project.Id) + "?page=0", Url(s.Project.Id) + "?pageSize=101" })
            Assert.Contains((await SendAsync(client, HttpMethod.Get, url, s.Admin.Token)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
        var empty = await GetAsync(s, Url(foreignProject, foreignEscrow, "settlements")); Assert.Empty(empty.GetProperty("items").EnumerateArray());
        using var checkScope = factory.Services.CreateScope(); var check = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.DoesNotContain(await check.AdminAccessLogs.Where(log => log.ProjectId == s.Project.Id).ToListAsync(), log => log.ResourceId == foreignEscrow);
    }

    [Fact]
    public void ExactDisplayPreservesSubCentFeesAndGuardsInvalidArithmetic()
    {
        var payout = new AdminFinancePayout(Guid.NewGuid(), "Tiny", MileStoneStatus.Approved, 0.000009m, 0m, null, null);
        Assert.Equal("0.000009", payout.FreelancerNetPaidExact); Assert.Equal("0", payout.PlatformFeePaidExact);
        var legacy = new AdminFinanceEscrow { Status = EscrowStatus.Completed, TotalAmount = decimal.MaxValue, GrossReleased = decimal.MaxValue,
            RecordedMilestoneGross = decimal.MaxValue, FundedAt = DateTimeOffset.UtcNow };
        Assert.Equal("0", legacy.PlatformFeesPaidExact); Assert.Equal("0", legacy.RemainingRecordedAmountExact); Assert.Empty(legacy.Gaps);
        var bad = new AdminFinanceEscrow { Status = EscrowStatus.Completed, TotalAmount = 1, GrossReleased = decimal.MaxValue, RecordedCaseRefunds = decimal.MaxValue, PlatformFeesPaid = -1 };
        Assert.Null(bad.RemainingRecordedAmountExact); Assert.Null(bad.FreelancerNetPaidExact); Assert.NotEmpty(bad.Gaps);
    }

    [Fact]
    public async Task CsvReportExportsTheWholeEscrowWithExactValuesAndActorResourceAudit()
    {
        var s = await SeedAsync();
        Guid[] extraIds;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var extras = Enumerable.Range(2, 24).Select(index => new MileStone { ProjectId = s.Project.Id, FreelancerId = s.Escrow.FreelancerId,
                Title = index == 2 ? "=FORMULA(1)" : "Extra stage " + index, Amount = 1, PaidAmount = 0, SequenceNumber = index,
                Status = MileStoneStatus.Approved, Deadline = DateTimeOffset.UtcNow }).ToArray();
            db.Milestones.AddRange(extras); await db.SaveChangesAsync(); extraIds = extras.Select(item => item.Id).ToArray();
        }
        var displayed = await GetAsync(s, Url(s.Project.Id, s.Escrow.Id));
        Assert.Equal(20, displayed.GetProperty("items").GetArrayLength());
        var response = await SendAsync(client, HttpMethod.Get, Url(s.Project.Id, s.Escrow.Id, "report.csv") + "?adminId=" + s.Owner.Id, s.Admin.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Contains(s.Escrow.Id.ToString(), response.Content.Headers.ContentDisposition.FileName);
        Assert.True(response.Headers.CacheControl?.Private); Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        var bytes = await response.Content.ReadAsByteArrayAsync(); Assert.Equal(new byte[] { 239, 187, 191 }, bytes.Take(3));
        var rows = ParseCsv(bytes);
        Assert.All(rows, row => Assert.Equal(6, row.Length));
        Assert.Equal(25, rows.Where(row => row[0] == "milestone_payout").Select(row => row[3]).Distinct().Count());
        foreach (var id in extraIds) Assert.Contains(rows, row => row[3] == id.ToString());
        Assert.Contains(rows, row => row[4] == "gross_released" && decimal.Parse(row[5], System.Globalization.CultureInfo.InvariantCulture) == 100);
        Assert.Contains(rows, row => row[4] == "freelancer_net_paid" && decimal.Parse(row[5], System.Globalization.CultureInfo.InvariantCulture) == 90);
        Assert.Contains(rows, row => row[4] == "title" && row[5] == "'=FORMULA(1)");
        Assert.Contains(rows, row => row[4] == "payment_transaction_hash" && row[5] == "Not recorded");
        Assert.DoesNotContain(rows, row => row.Any(value => value == s.Admin.Email || value == s.Owner.Email));
        using var checkScope = factory.Services.CreateScope(); var check = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await check.AdminAccessLogs.Where(log => log.ProjectId == s.Project.Id && log.Action.StartsWith("AdminFinanceReportExported")).ToListAsync();
        Assert.Equal(26, logs.Count); Assert.All(logs, log => Assert.Equal(s.Admin.Id, log.AdminId));
        Assert.Contains(logs, log => log.ResourceId == s.Escrow.Id);
        Assert.Equal(EscrowStatus.Completed, (await check.Escrows.SingleAsync(item => item.Id == s.Escrow.Id)).Status);
    }

    [Fact]
    public async Task CsvReportRefusesCrossProjectIdsWithoutCreatingExportAudits()
    {
        var s = await SeedAsync();
        foreach (var path in new[] { Url(Guid.NewGuid(), s.Escrow.Id, "report.csv"), Url(s.Project.Id, Guid.NewGuid(), "report.csv") })
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, path, s.Admin.Token)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.AdminAccessLogs.AnyAsync(log => log.ProjectId == s.Project.Id && log.Action.StartsWith("AdminFinanceReportExported")));
    }

    [Theory]
    [InlineData("=SUM(1,2)")]
    [InlineData(" +1")]
    [InlineData("-1+2")]
    [InlineData("@SUM(1)")]
    [InlineData("\t=1")]
    [InlineData("\r=1")]
    public void CsvNeutralizesFormulaTextAndQuotesUnicodeCommasQuotesAndNewlines(string title)
    {
        var e = new AdminFinanceEscrow { TotalAmount = 9007199254740991.01m, Status = EscrowStatus.AwaitingFunding, FreelancerName = "رضا, \"نام\"\nنسخه" };
        var payout = new AdminFinancePayout(Guid.NewGuid(), title, MileStoneStatus.Approved, .000009m, 0, null, null);
        var outcome = new AdminFinanceSettlement(Guid.NewGuid(), Guid.NewGuid(), "Unknown", null, null, null, true, null);
        var rows = ParseCsv(AdminFinanceCsv.Create(Guid.NewGuid(), title, e, [payout], [outcome], DateTimeOffset.UtcNow));
        Assert.All(rows, row => Assert.Equal(6, row.Length));
        Assert.Contains(rows, row => row[4] == "project_title" && row[5] == "'" + title);
        Assert.Contains(rows, row => row[4] == "title" && row[5] == "'" + title);
        Assert.Contains(rows, row => row[4] == "freelancer_name" && row[5] == e.FreelancerName);
        Assert.Contains(rows, row => row[4] == "contract_total" && row[5] == "9007199254740991.01");
        Assert.Contains(rows, row => row[0] == "milestone_payout" && row[4] == "freelancer_net_paid" && row[5] == "0.000009");
        Assert.Contains(rows, row => row[0] == "settled_case" && row[4] == "returned_to_client" && row[5] == "Unknown / not recorded");
    }

    private static List<string[]> ParseCsv(byte[] bytes)
    {
        using var parser = new TextFieldParser(new MemoryStream(bytes), Encoding.UTF8) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        var rows = new List<string[]>();
        while (!parser.EndOfData) rows.Add(parser.ReadFields()!);
        return rows;
    }

    private static void Money(JsonElement row, string key, decimal expected) => Assert.Equal(expected, decimal.Parse(row.GetProperty(key).GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    private async Task<JsonElement> GetAsync(Scenario s, string url)
    {
        var response = await SendAsync(client, HttpMethod.Get, url, s.Admin.Token); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private); Assert.True(response.Headers.CacheControl?.NoStore); return await ReadAsync(response);
    }
    private async Task<Scenario> SeedAsync()
    {
        var admin = await NewPersonAsync(client, "Client", "Finance administrator");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(admin.Id.ToString()))!, AppRoles.Admin)).Succeeded);
        }
        admin = admin with { Token = (await ReadAsync(await TestAdminSignIn.CompleteAsync(client, factory.Services, admin.Email, Password))).GetProperty("accessToken").GetString()! };
        var owner = await NewPersonAsync(client, "Client", "Finance client"); var talent = await NewPersonAsync(client, "Freelancer", "Finance talent");
        var project = new Project { ClientId = owner.Id, Title = "Finance test", Budget = 100, Deadline = DateTimeOffset.UtcNow, Status = ProjectStatus.Completed };
        var escrow = new Escrow { ProjectId = project.Id, FreelancerId = talent.Id, ChainId = 31337, TokenAddress = "test-token", TokenDecimals = 6,
            TotalAmount = 100, ReleasedAmount = 100, PlatformFeesPaid = 10, PlatformFeeBasisPoints = 1000, Status = EscrowStatus.Completed,
            FundedAt = DateTimeOffset.UtcNow, FundingTransactionHash = "verified-funding" };
        var stage = new MileStone { ProjectId = project.Id, FreelancerId = talent.Id, Title = "Paid stage", Amount = 100, PaidAmount = 100, PlatformFeePaid = 10,
            SequenceNumber = 1, Status = MileStoneStatus.Approved, Deadline = DateTimeOffset.UtcNow };
        using var seed = factory.Services.CreateScope(); var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Projects.Add(project); db.Escrows.Add(escrow); db.Milestones.Add(stage); await db.SaveChangesAsync();
        return new Scenario(admin, owner, project, escrow, stage);
    }
    private sealed record Scenario(Person Admin, Person Owner, Project Project, Escrow Escrow, MileStone Stage);
}
