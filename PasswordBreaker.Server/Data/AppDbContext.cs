using Microsoft.EntityFrameworkCore;

namespace PasswordBreaker.Server.Data;

public class CrackedPassword
{
    public int Id { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public int WorkerCount { get; set; }
    public DateTime CrackedAt { get; set; } = DateTime.UtcNow;
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CrackedPassword> CrackedPasswords => Set<CrackedPassword>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CrackedPassword>().HasIndex(x => x.Hash);
    }
}
