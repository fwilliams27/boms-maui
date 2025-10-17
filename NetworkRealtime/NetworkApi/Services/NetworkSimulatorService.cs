using Microsoft.AspNetCore.SignalR;
using NetworkApi.Hubs;

namespace NetworkApi.Services;

public class NetworkSimulatorService : BackgroundService
{
    private readonly IHubContext<NetworkHub, INetworkClient> _hub;
    private readonly Random _rng = new();

    public NetworkSimulatorService(IHubContext<NetworkHub, INetworkClient> hub)
        => _hub = hub;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var devices = new[] { "alpha", "bravo", "charlie" };

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var id in devices)
            {
                var dto = new TelemetryDto(
                    id,
                    Cpu: Math.Round(20 + _rng.NextDouble() * 60, 1),
                    Mem: Math.Round(30 + _rng.NextDouble() * 50, 1),
                    At: DateTimeOffset.UtcNow);

                await _hub.Clients.Group($"device:{id}").ReceiveTelemetry(dto);
                await _hub.Clients.All.ReceiveTelemetry(dto);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
