
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Models;

namespace TrustFlow.Api.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Project> Projects => Set<Project>();
    }
}