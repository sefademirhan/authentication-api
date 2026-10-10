using AuthenticationApi.Models;
using Microsoft.EntityFrameworkCore;
namespace AuthenticationApi.Data;
public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<AppUser>().Property(u => u.Email).HasMaxLength(320);
    }
}