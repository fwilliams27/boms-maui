using NetworkApi.Hubs;
using NetworkApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin()
     .AllowAnyHeader()
     .AllowAnyMethod()));
builder.Services.AddHostedService<NetworkSimulatorService>();

var app = builder.Build();

// Pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();


// Endpoints
app.MapGet("/api/health", () => Results.Ok(new { ok = true, at = DateTimeOffset.UtcNow }));

app.MapPost("/api/devices/{id}/status",
    async (string id,
           Microsoft.AspNetCore.SignalR.IHubContext<NetworkHub, INetworkClient> hub,
           DeviceStatus body) =>
{
    await hub.Clients.Group($"device:{id}")
        .ReceiveNotification($"Status updated for {id}: {body.Status} at {DateTimeOffset.UtcNow:O}");
    return Results.Accepted();
});

app.MapPost("/api/broadcast",
    async (Microsoft.AspNetCore.SignalR.IHubContext<NetworkHub, INetworkClient> hub,
           Broadcast body) =>
{
    await hub.Clients.All.ReceiveNotification(body.Message);
    return Results.Ok(new { sent = true });
});

app.MapHub<NetworkHub>("/hubs/network");

app.Run();

// ---- Types go AFTER all top-level statements ----
public record DeviceStatus(string Status);
public record Broadcast(string Message);

// Keep this LAST so tests can find Program
public partial class Program { }
