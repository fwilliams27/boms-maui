namespace BOMS;

using Microsoft.Maui.Controls;
using BOMS.Data;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Use Shell navigation (make sure you have AppShell.xaml/.cs with x:Class="BOMS.AppShell")
        MainPage = new AppShell();

        // Ensure the SQLite DB and tables exist (runs once on first launch)
        try
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            // Surface issues (e.g., path/permissions) so you see them during dev
            _ = Current?.MainPage?.DisplayAlert("Database init failed", ex.Message, "OK");
        }
    }
}
