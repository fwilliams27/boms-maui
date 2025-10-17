using BOMS.Services;

namespace BOMS.Pages;

public partial class TelemetryPage : ContentPage
{
    private readonly RealtimeClient _client;
    private string _currentGroup = "alpha";

    public TelemetryPage(RealtimeClient client)
    {
        InitializeComponent();
        _client = client;
        BindingContext = _client;
    }

    // Connect button
    private async void OnConnect(object sender, EventArgs e)
    {
        await _client.StartAsync(_currentGroup);
        // Optional: show a quick in-app toast substitute
        await DisplayAlert("Connected", $"Listening to '{_currentGroup}'", "OK");
    }

    // Picker.SelectedIndexChanged
    private void OnGroupChanged(object sender, EventArgs e)
    {
        if (sender is Picker p && p.SelectedItem is string s && !string.IsNullOrWhiteSpace(s))
            _currentGroup = s.Trim();
    }
}
