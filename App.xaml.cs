using System.IO;
using Microsoft.Maui.Storage;
using BOMS.Data;
using Microsoft.EntityFrameworkCore;       // ExecuteSqlRaw
using Microsoft.EntityFrameworkCore.Infrastructure; // IDbContextFactory

namespace BOMS;

public partial class App : Application
{
    // Use factory so we don't hold a long-lived context
    public App(IDbContextFactory<AppDbContext> dbFactory)
    {
        InitializeComponent();

        using var dbContext = dbFactory.CreateDbContext();

        // Ensure DB file exists, then upgrade schema if needed
        dbContext.Database.EnsureCreated();
        dbContext.EnsureUpToDate();   // add missing columns like Complexity, timestamps, etc.

#if DEBUG
        // Start with an EMPTY queue in DEBUG (no orders on open)
        try
        {
            dbContext.Database.ExecuteSqlRaw("DELETE FROM Orders;");
            dbContext.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='Orders';");
        }
        catch
        {
            // non-fatal; continue launching the app
        }
#endif

        MainPage = new AppShell();

#if DEBUG
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "starbucks.db");
        System.Console.WriteLine($"[BOMS] SQLite path: {dbPath}");
        MainPage.Dispatcher.Dispatch(async () =>
            await MainPage.DisplayAlert("SQLite path", dbPath, "OK"));
#endif
    }
}
