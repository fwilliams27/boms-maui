using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;

namespace BOMS.Services;

public record TelemetryDto(string DeviceId, double Cpu, double Mem, DateTimeOffset At);

public class RealtimeClient : IAsyncDisposable
{
    private readonly HubConnection _hub;
    private readonly IDispatcher _dispatcher;

    public ObservableCollection<TelemetryDto> Telemetry { get; } = new();

    public RealtimeClient(IDispatcher dispatcher, string baseUrl)
    {
        _dispatcher = dispatcher;
        _hub = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/network")
            .WithAutomaticReconnect()
            .Build();

        _hub.On<TelemetryDto>("ReceiveTelemetry", dto =>
        {
            _dispatcher.Dispatch(() =>
            {
                Telemetry.Insert(0, dto);
                if (Telemetry.Count > 200) Telemetry.RemoveAt(Telemetry.Count - 1);
            });
        });

        _hub.On<string>("ReceiveNotification", message =>
        {
            System.Diagnostics.Debug.WriteLine("[NOTIFY] " + message);
        });
    }

    public async Task StartAsync(string deviceGroup = "alpha")
    {
        if (_hub.State == HubConnectionState.Disconnected)
        {
            await _hub.StartAsync();
            await _hub.InvokeAsync("JoinDeviceGroup", deviceGroup);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await _hub.DisposeAsync(); } catch { }
    }
}
