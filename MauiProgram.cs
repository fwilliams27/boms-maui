using BOMS.Services;
using CommunityToolkit.Maui;

namespace BOMS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // IMPORTANT: Toolkit is chained to UseMauiApp in the same fluent call
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

        // DI
        builder.Services.AddSingleton<IDispatcher>(Dispatcher.GetForCurrentThread()!);
        builder.Services.AddSingleton(sp => new RealtimeClient(sp.GetRequiredService<IDispatcher>(), baseUrl));

        // Pages
        builder.Services.AddTransient<BOMS.Pages.TelemetryPage>();
        builder.Services.AddTransient<BOMS.MainPage>();

        return builder.Build();
    }
}
