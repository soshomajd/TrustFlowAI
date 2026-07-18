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

        modelBuilder.Entity<MileStone>()
            .HasIndex(milestone => new
            {
                milestone.ProjectId,
                milestone.SequenceNumber
            })
            .IsUnique();
    }
}