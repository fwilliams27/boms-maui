using Microsoft.AspNetCore.SignalR;

namespace NetworkApi.Hubs;

public interface INetworkClient
{
    Task ReceiveTelemetry(TelemetryDto payload);
    Task ReceiveNotification(string message);
}

public class NetworkHub : Hub<INetworkClient>
{
    public Task JoinDeviceGroup(string deviceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceId}");

    public Task LeaveDeviceGroup(string deviceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device:{deviceId}");
}

public record TelemetryDto(string DeviceId, double Cpu, double Mem, DateTimeOffset At);
