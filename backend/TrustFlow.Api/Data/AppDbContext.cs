using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<MileStone> Milestones => Set<MileStone>();

    public DbSet<Proposal> Proposals => Set<Proposal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MileStone>()
            .Property(milestone => milestone.Status)
            .HasConversion<string>();

        modelBuilder.Entity<MileStone>()
            .HasOne(milestone => milestone.Project)
            .WithMany(project => project.Milestones)
            .HasForeignKey(milestone => milestone.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Proposal>()
            .Property(proposal => proposal.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Proposal>().
             HasOne(proposal => proposal.Project)
            .WithMany(project => project.Proposals)
            .HasForeignKey(proposal => proposal.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Proposal>()
            .HasOne(proposal => proposal.Freelancer)
            .WithMany(user => user.FreelancerProposals)
            .HasForeignKey(proposal => proposal.FreelancerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Project>()
            .HasOne(project => project.Client)
            .WithMany(user => user.ClientProjects)
            .HasForeignKey(project => project.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MileStone>()
            .HasIndex(milestone => new
            {
                milestone.ProjectId,
                milestone.SequenceNumber
            })
            .IsUnique();
    }
}