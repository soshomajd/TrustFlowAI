using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Data;

public class AppDbContext(
    DbContextOptions<AppDbContext> options)
    : IdentityDbContext<
        ApplicationUser,
        IdentityRole<Guid>,
        Guid
    >(options)
{
    public DbSet<Project> Projects =>
        Set<Project>();

    public DbSet<MileStone> Milestones =>
        Set<MileStone>();

    public DbSet<Proposal> Proposals =>
        Set<Proposal>();
    public DbSet<RefreshToken> RefreshTokens =>
        Set<RefreshToken>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {

        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("pg_trgm");

        ConfigureProjects(modelBuilder);
        ConfigureMilestones(modelBuilder);
        ConfigureProposals(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
    }

    private static void ConfigureRefreshTokens(
    ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(refreshToken =>
        {
            refreshToken.Property(item => item.TokenHash)
                .HasMaxLength(64);

            refreshToken.Property(
                    item => item.ReplacedByTokenHash)
                .HasMaxLength(64);

            refreshToken.HasOne(item => item.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            refreshToken.HasIndex(item => item.TokenHash)
                .IsUnique();

            refreshToken.HasIndex(item => new
            {
                item.UserId,
                item.FamilyId
            })
                .HasDatabaseName(
                    "IX_RefreshTokens_UserId_FamilyId"
                );

            refreshToken.HasIndex(item => item.ExpiresAt)
                .HasDatabaseName(
                    "IX_RefreshTokens_ExpiresAt"
                );

            refreshToken.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_RefreshTokens_Expiration",
                    "\"ExpiresAt\" > \"CreatedAt\""
                );
            });
        });
    }

    private static void ConfigureProjects(
        ModelBuilder modelBuilder)
    {


        modelBuilder.Entity<Project>(project =>
        {
            project.Property(item => item.Title)
                .HasMaxLength(200);

            project.Property(item => item.Description)
                .HasMaxLength(5000);

            project.Property(item => item.Budget)
                .HasPrecision(18, 2);

            project.Property(item => item.Status)
                .HasConversion<string>()
                .HasDefaultValue(ProjectStatus.Open);

            project.HasOne(item => item.Client)
                .WithMany(user => user.ClientProjects)
                .HasForeignKey(item => item.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            project.HasOne(item => item.Freelancer)
                .WithMany(user => user.FreelancerProjects)
                .HasForeignKey(item => item.FreelancerId)
                .OnDelete(DeleteBehavior.Restrict);

            project.HasIndex(item => new
            {
                item.Status,
                item.CreatedAt
            })
                .HasDatabaseName(
                    "IX_Projects_Status_CreatedAt"
                );

            project.HasIndex(item => new
            {
                item.Status,
                item.Budget
            })
                .HasDatabaseName(
                    "IX_Projects_Status_Budget"
                );

            project.HasIndex(item => new
            {
                item.Status,
                item.Deadline
            })
                .HasDatabaseName(
                    "IX_Projects_Status_Deadline"
                );

            project.HasIndex(item => new
            {
                item.FreelancerId,
                item.CreatedAt
            })
                .HasDatabaseName(
                    "IX_Projects_FreelancerId_CreatedAt"
                );

            project.HasIndex(item => item.Title)
                .HasDatabaseName(
                    "IX_Projects_Title_Trgm"
                )
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            project.HasIndex(item => item.Description)
                .HasDatabaseName(
                    "IX_Projects_Description_Trgm"
                )
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            project.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Projects_Budget_Positive",
                    "\"Budget\" > 0"
                );
            });
        });
    }

    private static void ConfigureMilestones(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MileStone>(milestone =>
        {
            milestone.Property(item => item.Title)
                .HasMaxLength(200);

            milestone.Property(item => item.Description)
                .HasMaxLength(5000);

            milestone.Property(item => item.Amount)
                .HasPrecision(18, 2);

            milestone.Property(item => item.Status)
                .HasConversion<string>();

            milestone.HasOne(item => item.Project)
                .WithMany(project => project.Milestones)
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            milestone.HasIndex(item => new
            {
                item.ProjectId,
                item.SequenceNumber
            })
                .IsUnique();

            milestone.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Milestones_Amount_Positive",
                    "\"Amount\" > 0"
                );

                table.HasCheckConstraint(
                    "CK_Milestones_SequenceNumber_Positive",
                    "\"SequenceNumber\" > 0"
                );
            });
        });
    }

    private static void ConfigureProposals(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Proposal>(proposal =>
        {
            proposal.Property(item => item.CoverLetter)
                .HasMaxLength(5000);

            proposal.Property(item => item.BidAmount)
                .HasPrecision(18, 2);

            proposal.Property(item => item.Status)
                .HasConversion<string>();

            proposal.HasOne(item => item.Project)
                .WithMany(project => project.Proposals)
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            proposal.HasOne(item => item.Freelancer)
                .WithMany(user =>
                    user.FreelancerProposals)
                .HasForeignKey(item =>
                    item.FreelancerId)
                .OnDelete(DeleteBehavior.Restrict);

            proposal.HasIndex(item => new
            {
                item.ProjectId,
                item.FreelancerId
            })
                .IsUnique();

            proposal.HasIndex(item => new
            {
                item.ProjectId,
                item.CreatedAt
            })
                .HasDatabaseName(
                    "IX_Proposals_ProjectId_CreatedAt"
                );

            proposal.HasIndex(item => new
            {
                item.FreelancerId,
                item.CreatedAt
            })
                .HasDatabaseName(
                    "IX_Proposals_FreelancerId_CreatedAt"
                );

            proposal.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Proposals_BidAmount_Positive",
                    "\"BidAmount\" > 0"
                );

                table.HasCheckConstraint(
                    "CK_Proposals_EstimatedDays_Positive",
                    "\"EstimatedDays\" > 0"
                );
            });
        });

    }
}