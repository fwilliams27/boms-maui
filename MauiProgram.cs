using BOMS.Services;
using BOMS.Data;
using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;

namespace BOMS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                // fonts
            });

        // Base URL for your backend
        string baseUrl =
#if ANDROID
            "http://10.0.2.2:5246";
#else
            "http://localhost:5246";
#endif

        // Dependency Injection (DI)
        builder.Services.AddSingleton<IDispatcher>(Dispatcher.GetForCurrentThread()!);
        builder.Services.AddSingleton(sp => new RealtimeClient(sp.GetRequiredService<IDispatcher>(), baseUrl));

        // ✅ Use factory instead of singleton for AppDbContext
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "starbucks.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Pages
        builder.Services.AddTransient<BOMS.Pages.TelemetryPage>();
        builder.Services.AddTransient<BOMS.MainPage>();

        var app = builder.Build();

        // Initialize the ServiceHelper for global service access
        ServiceHelper.Initialize(app.Services);

        return app;
    }
}
