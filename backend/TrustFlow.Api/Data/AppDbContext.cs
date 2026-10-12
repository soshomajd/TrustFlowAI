using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Files;

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

    public DbSet<AuthCode> AuthCodes => Set<AuthCode>();

    public DbSet<Escrow> Escrows =>
        Set<Escrow>();

    public DbSet<ProposalMilestone> ProposalMilestones =>
        Set<ProposalMilestone>();

    public DbSet<FreelancerProfile> FreelancerProfiles => Set<FreelancerProfile>();
    public DbSet<FreelancerResume> FreelancerResumes => Set<FreelancerResume>();
    public DbSet<MilestoneSubmission> MilestoneSubmissions => Set<MilestoneSubmission>();
    public DbSet<MilestoneSubmissionAttachment> MilestoneSubmissionAttachments => Set<MilestoneSubmissionAttachment>();
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();

    public DbSet<ProfilePhoto> ProfilePhotos => Set<ProfilePhoto>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {



        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ProfilePhoto>(photo =>
        {
            photo.HasKey(item => item.UserId);
            photo.HasOne<ApplicationUser>().WithOne().HasForeignKey<ProfilePhoto>(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            photo.Property(item => item.Content).HasColumnType("bytea");
        });

        modelBuilder.HasPostgresExtension("pg_trgm");

        ConfigureProjects(modelBuilder);
        ConfigureMilestones(modelBuilder);
        ConfigureProposals(modelBuilder);
        ConfigureProposalMilestones(modelBuilder);
        ConfigureEscrows(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
        ConfigureAuthCodes(modelBuilder);
        ConfigureWalletIdentity(modelBuilder);
        ConfigureFreelancerProfiles(modelBuilder);
        ConfigureFreelancerResumes(modelBuilder);
        ConfigureClientProfiles(modelBuilder);
        ConfigureMilestoneSubmissions(modelBuilder);
    }

    private static void ConfigureMilestoneSubmissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MilestoneSubmission>(submission =>
        {
            submission.HasKey(item => item.Id);
            submission.HasOne(item => item.Milestone).WithMany()
                .HasForeignKey(item => item.MilestoneId).OnDelete(DeleteBehavior.Cascade);
            submission.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.FreelancerId).OnDelete(DeleteBehavior.Restrict);
            submission.Property(item => item.Note).HasMaxLength(2000);
            submission.Property(item => item.PreviewLink).HasMaxLength(1000);
            submission.Property(item => item.SourceFilesLink).HasMaxLength(1000);
            submission.Property(item => item.ClientFeedback).HasMaxLength(1000);
            submission.HasIndex(item => new { item.MilestoneId, item.Version }).IsUnique();
            submission.ToTable(table => table.HasCheckConstraint(
                "CK_MilestoneSubmissions_Version_Positive", "\"Version\" > 0"));
        });

        modelBuilder.Entity<MilestoneSubmissionAttachment>(attachment =>
        {
            attachment.HasKey(item => item.Id);
            attachment.HasOne(item => item.Submission).WithMany(item => item.Attachments)
                .HasForeignKey(item => item.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            attachment.Property(item => item.OriginalFileName).HasMaxLength(200);
            attachment.Property(item => item.ContentType).HasMaxLength(50);
            attachment.Property(item => item.Sha256).HasMaxLength(64);
            attachment.Property(item => item.Content).HasColumnType("bytea");
            attachment.ToTable(table => table.HasCheckConstraint(
                "CK_MilestoneSubmissionAttachments_FileSize",
                $"\"FileSize\" BETWEEN 1 AND {MilestoneDeliveryFileValidator.MaximumFileSize}"));
        });
    }

    private static void ConfigureClientProfiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientProfile>(profile =>
        {
            profile.HasKey(item => item.UserId);
            profile.HasOne<ApplicationUser>().WithOne()
                .HasForeignKey<ClientProfile>(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            profile.Property(item => item.AccountType).HasMaxLength(20);
            profile.Property(item => item.CompanyName).HasMaxLength(120);
            profile.Property(item => item.Overview).HasMaxLength(1000);
            profile.Property(item => item.Industry).HasMaxLength(80);
            profile.Property(item => item.Location).HasMaxLength(100);
            profile.Property(item => item.Website).HasMaxLength(500);
            profile.Property(item => item.CompanySize).HasMaxLength(20);
            profile.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ClientProfiles_AccountType", "\"AccountType\" IN ('Individual', 'Company')");
                table.HasCheckConstraint("CK_ClientProfiles_CompanySize", "\"CompanySize\" IN ('NotSpecified', 'JustMe', '2-10', '11-50', '51-200', '201Plus')");
            });
        });
    }

    private static void ConfigureFreelancerResumes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FreelancerResume>(resume =>
        {
            resume.HasKey(item => item.UserId);
            resume.HasOne<ApplicationUser>().WithOne()
                .HasForeignKey<FreelancerResume>(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            resume.Property(item => item.Content).HasColumnType("bytea");
            resume.Property(item => item.Sha256).HasMaxLength(64);
            resume.ToTable(table => table.HasCheckConstraint(
                "CK_FreelancerResumes_FileSize", "\"FileSize\" BETWEEN 1 AND 5242880"));
        });
    }

    private static void ConfigureFreelancerProfiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FreelancerProfile>(profile =>
        {
            profile.HasKey(item => item.UserId);
            profile.HasOne<ApplicationUser>().WithOne()
                .HasForeignKey<FreelancerProfile>(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            profile.Property(item => item.Headline).HasMaxLength(100);
            profile.Property(item => item.About).HasMaxLength(1200);
            profile.Property(item => item.Location).HasMaxLength(100);
            profile.Property(item => item.Availability).HasMaxLength(20);
            profile.Property(item => item.PortfolioItemsJson).HasColumnType("jsonb");
            profile.ToTable(table => table.HasCheckConstraint(
                "CK_FreelancerProfiles_YearsOfExperience",
                "\"YearsOfExperience\" IS NULL OR \"YearsOfExperience\" BETWEEN 0 AND 60"));
        });
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



    private static void ConfigureAuthCodes(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthCode>(authCode =>
        {
            authCode.Property(item => item.CodeHash)
                .HasMaxLength(64);

            authCode.Property(item => item.Purpose)
                .HasConversion<string>()
                .HasMaxLength(32);

            authCode.HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One live code per user and purpose. A second concurrent
            // request loses the insert instead of leaving two valid codes.
            authCode.HasIndex(item => new
            {
                item.UserId,
                item.Purpose
            })
                .IsUnique()
                .HasFilter("\"ConsumedAt\" IS NULL")
                .HasDatabaseName("IX_AuthCodes_Live_UserId_Purpose");

            authCode.HasIndex(item => item.ExpiresAt)
                .HasDatabaseName("IX_AuthCodes_ExpiresAt");
        });
    }

    private static void ConfigureWalletIdentity(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(
            user =>
            {
                user.Property(item =>
                        item.WalletAddress)
                    .HasMaxLength(42);

                user.Property(item =>
                        item.WalletAddressNormalized)
                    .HasMaxLength(42);

                user.Property(item =>
                        item.PendingWalletAddress)
                    .HasMaxLength(42);

                user.Property(item =>
                        item.WalletVerificationNonce)
                    .HasMaxLength(64);

                user.HasIndex(item =>
                        item.WalletAddressNormalized)
                    .IsUnique()
                    .HasFilter(
                        "\"WalletAddressNormalized\" " +
                        "IS NOT NULL"
                    )
                    .HasDatabaseName(
                        "IX_AspNetUsers_" +
                        "WalletAddressNormalized"
                    );
            }
        );
    }


    private static void ConfigureEscrows(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Escrow>(escrow =>
        {
            escrow.Property(item => item.TotalAmount)
                .HasPrecision(18, 2);

            escrow.Property(item => item.ReleasedAmount)
                .HasPrecision(18, 2);

            escrow.Property(item => item.Status)
                .HasConversion<string>()
                .HasDefaultValue(
                    EscrowStatus.PendingDeployment
                );

            escrow.Property(item => item.TokenAddress)
                .HasMaxLength(42);

            escrow.Property(item => item.ContractAddress)
                .HasMaxLength(42);

            escrow.Property(
                    item => item.ClientWalletAddress)
                .HasMaxLength(42);

            escrow.Property(
                    item => item.FreelancerWalletAddress)
                .HasMaxLength(42);

            escrow.Property(
                    item =>
                        item.DeploymentTransactionHash)
                .HasMaxLength(66);

            escrow.Property(
                    item =>
                        item.FundingTransactionHash)
                .HasMaxLength(66);

            escrow.Property(
                    item =>
                        item.CancellationTransactionHash)
                .HasMaxLength(66);

            escrow.HasOne(item => item.Project)
                .WithMany(project => project.Escrows)
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            escrow.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.FreelancerId)
                .OnDelete(DeleteBehavior.Restrict);

            // One escrow per freelancer per project, not one per project.
            escrow.HasIndex(item => new
            {
                item.ProjectId,
                item.FreelancerId
            })
                .IsUnique();

            escrow.HasIndex(item =>
                    item.ContractAddress)
                .IsUnique()
                .HasFilter(
                    "\"ContractAddress\" IS NOT NULL"
                )
                .HasDatabaseName(
                    "IX_Escrows_ContractAddress"
                );

            escrow.HasIndex(item => new
            {
                item.Status,
                item.UpdatedAt
            })
                .HasDatabaseName(
                    "IX_Escrows_Status_UpdatedAt"
                );

            escrow.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Escrows_ChainId_Positive",
                    "\"ChainId\" > 0"
                );

                table.HasCheckConstraint(
                    "CK_Escrows_TotalAmount_Positive",
                    "\"TotalAmount\" > 0"
                );

                table.HasCheckConstraint(
                    "CK_Escrows_ReleasedAmount_NonNegative",
                    "\"ReleasedAmount\" >= 0"
                );

                table.HasCheckConstraint(
                    "CK_Escrows_ReleasedAmount_WithinTotal",
                    "\"ReleasedAmount\" <= \"TotalAmount\""
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

            milestone.HasOne(item => item.Freelancer)
                .WithMany()
                .HasForeignKey(item => item.FreelancerId)
                .OnDelete(DeleteBehavior.Restrict);

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

    private static void ConfigureProposalMilestones(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProposalMilestone>(link =>
        {
            link.HasKey(item => new
            {
                item.ProposalId,
                item.MilestoneId
            });

            link.HasOne(item => item.Proposal)
                .WithMany(proposal => proposal.ProposalMilestones)
                .HasForeignKey(item => item.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);

            link.HasOne(item => item.Milestone)
                .WithMany(milestone => milestone.ProposalMilestones)
                .HasForeignKey(item => item.MilestoneId)
                .OnDelete(DeleteBehavior.Cascade);
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
