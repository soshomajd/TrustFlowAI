using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class AdminDeliveryTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
    private static string List(Guid project) => $"/api/admin/projects/{project}/deliveries";
    private static string FileUrl(Guid project, Guid submission, Guid file) => List(project) + $"/{submission}/files/{file}";

    [Fact]
    public async Task AnonymousAndParticipantRolesCannotReadDeliveryNotesOrFiles()
    {
        var people = new[] { await NewPersonAsync(client,"Client","Delivery owner"), await NewPersonAsync(client,"Freelancer","Delivery author") };
        foreach (var url in new[] { List(Guid.NewGuid()), FileUrl(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()) })
        {
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(url)).StatusCode);
            foreach (var person in people) Assert.Equal(HttpStatusCode.Forbidden,(await SendAsync(client,HttpMethod.Get,url,person.Token)).StatusCode);
        }
    }

    [Fact]
    public async Task AllVersionsKeepTheirOriginalAuthorsFeedbackAndSafeMetadataAfterAssignmentsClear()
    {
        var s = await SeedAsync();
        var rows = new List<JsonElement>();
        for (var page = 1; page <= 3; page++)
        {
            var response = await GetAsync(s, List(s.Project.Id) + $"?pageSize=1&page={page}");
            var body = await ReadAsync(response);
            Assert.Equal(3,body.GetProperty("totalItems").GetInt32());
            rows.Add(Assert.Single(body.GetProperty("items").EnumerateArray()));
        }
        Assert.Equal(new[] { s.New.Id, s.Old.Id, s.OtherStage.Id },rows.Select(row=>row.GetProperty("id").GetGuid()));
        Assert.True(rows[0].GetProperty("isLatest").GetBoolean());
        Assert.False(rows[1].GetProperty("isLatest").GetBoolean());
        Assert.True(rows[2].GetProperty("isLatest").GetBoolean());
        Assert.Equal(s.Former.Id,rows[1].GetProperty("freelancerId").GetGuid());
        Assert.Equal(s.Current.Id,rows[0].GetProperty("freelancerId").GetGuid());
        Assert.Equal("Please revise the title",rows[1].GetProperty("clientFeedback").GetString());
        Assert.Equal(JsonValueKind.Null,rows[0].GetProperty("clientFeedback").ValueKind);
        Assert.Equal("Pending",rows[1].GetProperty("currentMilestoneStatus").GetString());
        var file = Assert.Single(rows[1].GetProperty("attachments").EnumerateArray());
        Assert.Equal(s.File.Id,file.GetProperty("id").GetGuid());
        Assert.Equal(s.File.Sha256,file.GetProperty("sha256").GetString());
        Assert.False(file.TryGetProperty("content",out _));
        Assert.False(file.TryGetProperty("storagePath",out _));
        Assert.All(rows,row=>Assert.False(row.TryGetProperty("email",out _)));
    }

    [Fact]
    public async Task DownloadIsProjectAndVersionScoped_AttachmentOnly_PrivateAndAudited()
    {
        var s = await SeedAsync();
        var response = await GetAsync(s,FileUrl(s.Project.Id,s.Old.Id,s.File.Id) + "?adminId=" + s.Owner.Id);
        Assert.Equal(s.File.Content,await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/octet-stream",response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment",response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff",response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("sandbox",response.Headers.GetValues("Content-Security-Policy").Single());
        using var scope = factory.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs=await db.AdminAccessLogs.Where(item=>item.ProjectId==s.Project.Id && item.Action.StartsWith("AdminDeliveryFileDownloaded")).ToListAsync();
        Assert.Equal(2,logs.Count);
        Assert.All(logs,log=>Assert.Equal(s.Admin.Id,log.AdminId));
        Assert.Contains(logs,log=>log.ResourceId==s.File.Id);
        var original=await db.MilestoneSubmissions.SingleAsync(item=>item.Id==s.Old.Id);
        Assert.Equal(s.Old.ClientFeedback,original.ClientFeedback);
        Assert.Equal(s.Old.ReviewedAt,original.ReviewedAt);
        Assert.Equal(MileStoneStatus.Pending,(await db.Milestones.SingleAsync(item=>item.Id==s.Old.MilestoneId)).Status);
    }

    [Fact]
    public async Task WrongProjectSubmissionAndFileCombinationsCannotDownloadOrWriteSuccessAudit()
    {
        var s=await SeedAsync();
        foreach(var url in new[] { FileUrl(s.OtherProject.Id,s.Old.Id,s.File.Id), FileUrl(s.Project.Id,s.New.Id,s.File.Id),
            FileUrl(s.Project.Id,s.Old.Id,s.OtherFile.Id),FileUrl(Guid.NewGuid(),s.Old.Id,s.File.Id),FileUrl(s.Project.Id,Guid.NewGuid(),s.File.Id) })
            Assert.Equal(HttpStatusCode.NotFound,(await SendAsync(client,HttpMethod.Get,url,s.Admin.Token)).StatusCode);
        using var scope=factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().AdminAccessLogs.AnyAsync(item=>item.AdminId==s.Admin.Id && item.Action.StartsWith("AdminDeliveryFileDownloaded")));
    }

    [Fact]
    public async Task DeliveryOnlyAuthorsRemainInTheParticipantRosterAndDossierCounts()
    {
        var s=await SeedAsync();
        var root=await ReadAsync(await GetAsync(s,$"/api/admin/projects/{s.Project.Id}"));
        Assert.Equal(3,root.GetProperty("deliveryCount").GetInt32());
        Assert.Equal(3,root.GetProperty("participantCount").GetInt32());
        var roster=(await ReadAsync(await GetAsync(s,$"/api/admin/projects/{s.Project.Id}/participants"))).GetProperty("items").EnumerateArray().ToArray();
        foreach(var id in new[] {s.Former.Id,s.Current.Id})
        {
            var person=roster.Single(row=>row.GetProperty("id").GetGuid()==id);
            Assert.True(person.GetProperty("hasDelivery").GetBoolean());
            Assert.False(person.GetProperty("currentFreelancer").GetBoolean());
            Assert.False(person.GetProperty("hasEscrow").GetBoolean());
        }
    }

    [Fact]
    public async Task PagingAndEmptyDeliveryHistoryAreBoundedAndAuditedHonestly()
    {
        var s=await SeedAsync();
        foreach(var query in new[]{"?page=0","?pageSize=101","?page=1000001"})
            Assert.Equal(HttpStatusCode.BadRequest,(await SendAsync(client,HttpMethod.Get,List(s.Project.Id)+query,s.Admin.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await SendAsync(client,HttpMethod.Get,List(Guid.NewGuid()),s.Admin.Token)).StatusCode);
        var empty=await ReadAsync(await GetAsync(s,List(s.Project.Id)+"?page=20"));
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Equal(3,empty.GetProperty("totalItems").GetInt32());
        using var scope=factory.Services.CreateScope();
        var logs=await scope.ServiceProvider.GetRequiredService<AppDbContext>().AdminAccessLogs.Where(item=>item.ProjectId==s.Project.Id).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("AdminProjectDeliveriesViewed",logs[0].Action);
        Assert.Null(logs[0].ResourceId);
    }

    private async Task<HttpResponseMessage> GetAsync(Scenario s,string url)
    {
        var response=await SendAsync(client,HttpMethod.Get,url,s.Admin.Token);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return response;
    }
    private async Task<Scenario> SeedAsync()
    {
        var admin=await NewPersonAsync(client,"Client","Delivery administrator");
        using(var scope=factory.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().AddToRoleAsync(
                (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(admin.Id.ToString()))!,AppRoles.Admin)).Succeeded);
        var login=await TestAdminSignIn.CompleteAsync(client,factory.Services,admin.Email,Password);
        admin=admin with {Token=(await ReadAsync(login)).GetProperty("accessToken").GetString()!};
        var owner=await NewPersonAsync(client,"Client","Delivery client");
        var former=await NewPersonAsync(client,"Freelancer","Original delivery author");
        var current=await NewPersonAsync(client,"Freelancer","New delivery author");
        var now=new DateTimeOffset(2026,10,19,12,0,0,TimeSpan.Zero);
        var project=new Project {ClientId=owner.Id,Title="Delivery history",Description="Historical versions",Budget=100,Deadline=now.AddDays(30)};
        var otherProject=new Project {ClientId=owner.Id,Title="Other delivery project",Description="Private unrelated work",Budget=100,Deadline=now.AddDays(30)};
        var stage=new MileStone {ProjectId=project.Id,Title="Homepage",Amount=50,SequenceNumber=1,Deadline=now};
        var stage2=new MileStone {ProjectId=project.Id,Title="Second stage",Amount=50,SequenceNumber=2,Deadline=now};
        var foreignStage=new MileStone {ProjectId=otherProject.Id,Title="Other stage",Amount=100,SequenceNumber=1,Deadline=now};
        var old=new MilestoneSubmission {MilestoneId=stage.Id,FreelancerId=former.Id,Version=1,Note="Original delivery",PreviewLink="https://example.com/preview",ClientFeedback="Please revise the title",SubmittedAt=now,ReviewedAt=now.AddHours(1)};
        var fresh=new MilestoneSubmission {MilestoneId=stage.Id,FreelancerId=current.Id,Version=2,Note="Revised delivery",SubmittedAt=now.AddDays(1)};
        var second=new MilestoneSubmission {MilestoneId=stage2.Id,FreelancerId=current.Id,Version=1,Note="Second stage work",SubmittedAt=now.AddDays(1)};
        var foreign=new MilestoneSubmission {MilestoneId=foreignStage.Id,FreelancerId=current.Id,Version=1,Note="Never leak this",SubmittedAt=now};
        var bytes=Encoding.UTF8.GetBytes("historical admin delivery bytes");
        var file=new MilestoneSubmissionAttachment {SubmissionId=old.Id,OriginalFileName="design.pdf",ContentType="application/pdf",FileSize=bytes.Length,Content=bytes,Sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()};
        var foreignFile=new MilestoneSubmissionAttachment {SubmissionId=foreign.Id,OriginalFileName="other.pdf",ContentType="application/pdf",FileSize=bytes.Length,Content=bytes,Sha256=file.Sha256};
        using var seed=factory.Services.CreateScope();var db=seed.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Projects.AddRange(project,otherProject);db.Milestones.AddRange(stage,stage2,foreignStage);
        db.MilestoneSubmissions.AddRange(old,fresh,second,foreign);db.MilestoneSubmissionAttachments.AddRange(file,foreignFile);await db.SaveChangesAsync();
        return new Scenario(admin,owner,former,current,project,otherProject,old,fresh,second,file,foreignFile);
    }
    private sealed record Scenario(Person Admin,Person Owner,Person Former,Person Current,Project Project,Project OtherProject,
        MilestoneSubmission Old,MilestoneSubmission New,MilestoneSubmission OtherStage,MilestoneSubmissionAttachment File,MilestoneSubmissionAttachment OtherFile);
}
