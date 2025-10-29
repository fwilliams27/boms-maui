using System;
using System.IO;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;

namespace BOMS.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "starbucks.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            Debug.WriteLine($"[BOMS] SQLite path: {dbPath}");
            System.Diagnostics.Debug.WriteLine($"[BOMS] SQLite path: {dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Keep simple column mapping; DO NOT mark PrepTimeWeight as DB-generated,
            // we set it in app code at insert time.
            modelBuilder.Entity<Order>()
                .Property(o => o.Complexity)
                .HasColumnType("REAL");

            modelBuilder.Entity<Order>()
                .Property(o => o.PrepTimeWeight)
                .HasColumnType("REAL");

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Ensure tables/columns exist and run lightweight data fixes for older DBs.
        /// Safe to call at startup.
        /// </summary>
        public void EnsureUpToDate()
        {
            try
            {
                // Reliability: WAL + busy timeout
                Exec("PRAGMA journal_mode=WAL;");
                Exec("PRAGMA busy_timeout=5000;");

                // Tables
                Exec(@"
CREATE TABLE IF NOT EXISTS Orders(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DrinkType TEXT,
    Status TEXT,
    PrepTimeWeight REAL NOT NULL DEFAULT 0.0,
    Complexity REAL NOT NULL DEFAULT 0.0,
    CreatedAt TEXT,
    LastUpdated TEXT
);");

                Exec(@"
CREATE TABLE IF NOT EXISTS AuditLogs(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Event TEXT,
    Timestamp TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);");

                // Columns (idempotent 'add column if missing' pattern)
                TryAddColumn("Orders", "Complexity",      "REAL NOT NULL DEFAULT 0.0");
                TryAddColumn("Orders", "PrepTimeWeight",  "REAL NOT NULL DEFAULT 0.0");
                TryAddColumn("Orders", "CreatedAt",       "TEXT");
                TryAddColumn("Orders", "LastUpdated",     "TEXT");

                // Backfill: if any rows have null/zero weight, use Complexity
                Exec(@"UPDATE Orders
                       SET PrepTimeWeight = Complexity
                       WHERE PrepTimeWeight IS NULL OR PrepTimeWeight = 0.0;");

                // Timestamps safety (optional)
                Exec(@"UPDATE Orders
                       SET CreatedAt   = COALESCE(CreatedAt, datetime('now')),
                           LastUpdated = COALESCE(LastUpdated, datetime('now'))
                       WHERE CreatedAt IS NULL OR LastUpdated IS NULL;");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BOMS] EnsureUpToDate failed: {ex.Message}");
                // Non-fatal: keep app running; details captured in Debug output.
            }
        }

        private void TryAddColumn(string table, string column, string columnDef)
        {
            // Will only add when missing (SQLite supports IF NOT EXISTS for columns in modern builds).
            // If the SQLite runtime doesn’t support IF NOT EXISTS on columns, the Exec() will throw,
            // which we swallow quietly here.
            try
            {
                Exec($"ALTER TABLE {table} ADD COLUMN IF NOT EXISTS {column} {columnDef};");
            }
            catch { /* ignore if column already exists */ }
        }

        private void Exec(string sql)
        {
            Database.ExecuteSqlRaw(sql);
        }
    }

    public class Order
    {
        public int Id { get; set; }                 // PK
        public string? DrinkType { get; set; }
        public string? Status { get; set; }
        public double Complexity { get; set; }
        public double PrepTimeWeight { get; set; }  // now set by app code on insert
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class AuditLog
    {
        public int Id { get; set; }                 // PK
        public string? Event { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
