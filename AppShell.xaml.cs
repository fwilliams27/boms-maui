namespace BOMS;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("MainPage", typeof(MainPage));
        Routing.RegisterRoute("TelemetryPage", typeof(Pages.TelemetryPage));
    }
}
