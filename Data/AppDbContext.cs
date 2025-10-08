namespace BOMS.Data;

using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Use a writable location for SQLite in MAUI
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "starbucks.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }
}

public class AuditLog
{
    public int Id { get; set; }  // Primary Key
    public string? Event { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class Order
{
    public int Id { get; set; }  // Primary Key
    public string? DrinkType { get; set; }
    public string? Status { get; set; }
    public double PrepTimeWeight { get; set; } = 0.0;
}
