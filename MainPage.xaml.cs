namespace BOMS;

using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using BOMS.Data;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.DependencyInjection; // GetRequiredService
using Microsoft.EntityFrameworkCore.Infrastructure; // IDbContextFactory

public partial class MainPage : ContentPage
{
    public ObservableCollection<Order> Orders { get; set; } = new();

    // DbContext factory (replace 'new AppDbContext()' usage)
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    // Error simulation config
    private bool _simulateErrors = true;
    private const double BaseErrorRate = 0.05;
    private const double IncrementPer5Orders = 0.02;
    private const double MaxErrorRate = 0.25;
    private static readonly Random _rng = new();

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;

        // ✅ Resolve factory from DI container via ServiceHelper
        _dbFactory = ServiceHelper.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

        // No auto seeding here; list will reflect DB
        EnsureStressToolbar();
    }

    private void EnsureStressToolbar()
    {
        if (ToolbarItems.FirstOrDefault(t => t.AutomationId == "stress100") != null)
            return;

        var stressButton = new ToolbarItem
        {
            Text = "Stress (100)",
            AutomationId = "stress100",
            Order = ToolbarItemOrder.Primary,
            Priority = 0,
            Command = new Command(async () =>
            {
                if (SeedingLabel != null) SeedingLabel.IsVisible = true;
                await RunStressSeedAsync();
                if (SeedingLabel != null) SeedingLabel.IsVisible = false;
            })
        };

        ToolbarItems.Add(stressButton);

#if MACCATALYST
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () =>
        {
            this.Handler?.UpdateValue(nameof(this.ToolbarItems));
        });
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshFromDbAsync();
    }

    private async Task RefreshFromDbAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var items = await db.Orders
            .Where(o => !EF.Functions.Like(o.Status, "Complete"))
            .OrderBy(o => o.PrepTimeWeight)           // 🧩 Ascending (lightest first)
            .ThenBy(o => o.LastUpdated)
            .ToListAsync();

        Orders.Clear();
        foreach (var o in items)
            Orders.Add(o);
    }

    private async void OnAddOrderClicked(object sender, EventArgs e)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        try
        {
            var totalSoFar = await db.Orders.CountAsync();
            var simulateError = _simulateErrors && ShouldSimulateError(totalSoFar);

            var drink = MockData.GetRandomDrink();

            var newOrder = new Order
            {
                DrinkType      = drink.Name,
                Status         = "Pending",
                Complexity     = drink.Complexity,
                PrepTimeWeight = drink.Complexity,
                CreatedAt      = DateTime.Now,
                LastUpdated    = DateTime.Now
            };

            bool planConnectionLoss = false;
            if (simulateError)
            {
                if (_rng.NextDouble() < 0.5)
                    newOrder.DrinkType = string.Empty;
                else
                    planConnectionLoss = true;
            }

            db.Orders.Add(newOrder);

            if (planConnectionLoss && _rng.NextDouble() < 0.95)
                throw new InvalidOperationException("Simulated connection loss");

            await db.SaveChangesAsync();
            await db.Entry(newOrder).ReloadAsync();

            if (!string.IsNullOrWhiteSpace(newOrder.DrinkType))
            {
                Orders.Add(newOrder);
                await DisplayAlert("Order Added",
                    $"{newOrder.DrinkType}\nPriority Weight: {newOrder.PrepTimeWeight:F2}",
                    "OK");

                await RefreshFromDbAsync();
            }
            else
            {
                db.AuditLogs.Add(new AuditLog { Event = "Invalid Order (Process Failed)", Timestamp = DateTime.Now });
                await db.SaveChangesAsync();
                await DisplayAlert("Error", "Process Failed: Invalid data", "OK");
            }
        }
        catch (Exception ex)
        {
            db.AuditLogs.Add(new AuditLog { Event = $"Connection Lost: {ex.Message}", Timestamp = DateTime.Now });
            await db.SaveChangesAsync();
            await DisplayAlert("Error", $"Connection Lost - Reconnect Attempt\n{ex.Message}", "OK");
        }
    }

    private void OnPrioritizeClicked(object sender, EventArgs e)
    {
        var sorted = Orders
            .OrderBy(o => o.PrepTimeWeight)           // lightest first
            .ThenBy(o => o.LastUpdated)
            .ToList();

        Orders.Clear();
        foreach (var order in sorted)
            Orders.Add(order);
    }

    private async void OnCompleteOrderClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Order selected)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        try
        {
            var tracked = await db.Orders.FindAsync(selected.Id);
            if (tracked is null)
            {
                await DisplayAlert("Notice", "Order no longer exists.", "OK");
                return;
            }

            if (string.Equals(tracked.Status, "Complete", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("Already Complete", $"Order #{tracked.Id} is already marked complete.", "OK");
                Orders.Remove(selected);
                return;
            }

            tracked.Status = "Complete";
            tracked.LastUpdated = DateTime.Now;
            await db.SaveChangesAsync();

            db.AuditLogs.Add(new AuditLog
            {
                Event = $"Order completed: #{tracked.Id} - {tracked.DrinkType}",
                Timestamp = DateTime.Now
            });
            await db.SaveChangesAsync();

            Orders.Remove(selected);
            await RefreshFromDbAsync();
        }
        catch (Exception ex)
        {
            db.AuditLogs.Add(new AuditLog { Event = $"Completion Failed: {ex.Message}", Timestamp = DateTime.Now });
            await db.SaveChangesAsync();
            await DisplayAlert("Error", $"Could not complete order. Please try again.\n{ex.Message}", "OK");
        }
    }

    private async void OnErrorToggleChanged(object sender, ToggledEventArgs e)
    {
        _simulateErrors = e.Value;

        if (_simulateErrors)
        {
            ErrorStatusLabel.Text = "ON";
            ErrorStatusLabel.TextColor = Colors.IndianRed;
            await DisplayAlert("Simulation", "Error simulation enabled.", "OK");
        }
        else
        {
            ErrorStatusLabel.Text = "OFF";
            ErrorStatusLabel.TextColor = Colors.ForestGreen;
            await DisplayAlert("Simulation", "Error simulation disabled. Use the Stress (100) button to run the test.", "OK");
        }
    }

    private async Task RunStressSeedAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var drinks = MockData.GetRandomDrinkBatch(MockData.DefaultStressBatch);
            var batch = new System.Collections.Generic.List<Order>(drinks.Count);

            foreach (var d in drinks)
            {
                batch.Add(new Order
                {
                    DrinkType      = d.Name,
                    Status         = "Pending",
                    Complexity     = d.Complexity,
                    PrepTimeWeight = d.Complexity,
                    CreatedAt      = DateTime.Now,
                    LastUpdated    = DateTime.Now
                });
            }

            db.Orders.AddRange(batch);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await RefreshFromDbAsync();

            var total = await db.Orders.CountAsync();
            await DisplayAlert("Seed Complete", $"Added {batch.Count} orders.\nTotal rows now: {total}.", "OK");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            await using var db2 = await _dbFactory.CreateDbContextAsync();
            db2.AuditLogs.Add(new AuditLog { Event = $"Stress Seed Failed: {ex.Message}", Timestamp = DateTime.Now });
            await db2.SaveChangesAsync();
            await DisplayAlert("Error", $"Stress seed failed.\n{ex.Message}", "OK");
        }
    }

    private static bool ShouldSimulateError(int totalOrdersInDb)
    {
        var increments = totalOrdersInDb / 5;
        var p = Math.Min(BaseErrorRate + increments * IncrementPer5Orders, MaxErrorRate);
        return _rng.NextDouble() < p;
    }
}
