namespace BOMS;

using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using BOMS.Data;
using System.Linq;
using System.Threading.Tasks;
using System;

public partial class MainPage : ContentPage
{
    public ObservableCollection<Order> Orders { get; set; } = new();

    // --- Error simulation config ---
    private bool _simulateErrors = true;             // bound to the UI toggle
    private const double BaseErrorRate = 0.05;       // 5% base
    private const double IncrementPer5Orders = 0.02; // +2% per 5 orders
    private const double MaxErrorRate = 0.25;        // 25% cap
    private static readonly Random _rng = new();

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
        OrderList.ItemsSource = Orders;

        // Seed some orders on first run so the list isn't empty
        _ = SeedMockOrdersAsync(5);
    }

    private async void OnAddOrderClicked(object sender, EventArgs e)
    {
        using var db = new AppDbContext();
        try
        {
            // Decide whether to simulate an error for this click
            var totalSoFar = await db.Orders.CountAsync();
            var simulateError = _simulateErrors && ShouldSimulateError(totalSoFar);

            var drink = MockData.GetRandomDrink();
            var weight = Math.Clamp(0.8 - drink.Complexity, 0.0, 1.0);

            var newOrder = new Order
            {
                DrinkType = drink.Name,
                Status = "Pending",
                PrepTimeWeight = weight
            };

            // If simulating error, pick which one:
            // ~50% "Process Failed" (invalid data) or ~50% "Connection Lost" (exception)
            bool planConnectionLoss = false;
            if (simulateError)
            {
                if (_rng.NextDouble() < 0.5)
                {
                    // PROCESS FAILED: make data invalid so validation catches it
                    newOrder.DrinkType = string.Empty;
                }
                else
                {
                    planConnectionLoss = true;
                }
            }

            db.Orders.Add(newOrder);

            // If we plan a connection loss, throw before saving most of the time
            if (planConnectionLoss && _rng.NextDouble() < 0.95)
                throw new InvalidOperationException("Simulated connection loss");

            await db.SaveChangesAsync();

            // ✅ Only show valid orders in the visual list
            if (!string.IsNullOrWhiteSpace(newOrder.DrinkType))
            {
                Orders.Add(newOrder);
                await DisplayAlert("Order Added",
                    $"{newOrder.DrinkType}\nPriority Weight: {newOrder.PrepTimeWeight:F2}",
                    "OK");
            }
            else
            {
                // VALIDATION branch (PROCESS FAILED) — log + alert, but do NOT show in list
                db.AuditLogs.Add(new AuditLog { Event = "Invalid Order (Process Failed)", Timestamp = DateTime.Now });
                await db.SaveChangesAsync();
                await DisplayAlert("Error", "Process Failed: Invalid data", "OK");
            }
        }
        catch (Exception ex)
        {
            // Catch branch (CONNECTION LOST)
            db.AuditLogs.Add(new AuditLog { Event = $"Connection Lost: {ex.Message}", Timestamp = DateTime.Now });
            await db.SaveChangesAsync();
            await DisplayAlert("Error", "Connection Lost - Reconnect Attempt", "OK");
        }
    }

    private void OnPrioritizeClicked(object sender, EventArgs e)
    {
        var sorted = Orders
            .OrderByDescending(o => o.PrepTimeWeight)
            .ToList();

        Orders.Clear();
        foreach (var order in sorted)
            Orders.Add(order);
    }

    /// <summary>
    /// Mark an order complete (updates DB and UI, logs audit).
    /// Removes it from the visual list but keeps it in the DB for audit.
    /// Bound to each row's "Complete" button via CommandParameter.
    /// </summary>
    private async void OnCompleteOrderClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Order selected)
            return;

        using var db = new AppDbContext();
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
                // Ensure it's not shown in the visual list
                Orders.Remove(selected);
                return;
            }

            tracked.Status = "Complete";
            await db.SaveChangesAsync();

            // Write audit log
            db.AuditLogs.Add(new AuditLog
            {
                Event = $"Order completed: #{tracked.Id} - {tracked.DrinkType}",
                Timestamp = DateTime.Now
            });
            await db.SaveChangesAsync();

            // ✅ Remove from UI list so barista sees only active work
            Orders.Remove(selected);
        }
        catch (Exception ex)
        {
            db.AuditLogs.Add(new AuditLog { Event = $"Completion Failed: {ex.Message}", Timestamp = DateTime.Now });
            await db.SaveChangesAsync();
            await DisplayAlert("Error", "Could not complete order. Please try again.", "OK");
        }
    }

    /// <summary>
    /// Toggle handler for the error simulation switch.
    /// Updates internal flag and colored status label.
    /// </summary>
    private void OnErrorToggleChanged(object sender, ToggledEventArgs e)
    {
        _simulateErrors = e.Value;

        if (_simulateErrors)
        {
            ErrorStatusLabel.Text = "ON";
            ErrorStatusLabel.TextColor = Colors.IndianRed;
            DisplayAlert("Simulation", "Error simulation enabled.", "OK");
        }
        else
        {
            ErrorStatusLabel.Text = "OFF";
            ErrorStatusLabel.TextColor = Colors.ForestGreen;
            DisplayAlert("Simulation", "Error simulation disabled.", "OK");
        }
    }

    private async Task SeedMockOrdersAsync(int count)
    {
        using var db = new AppDbContext();
        try
        {
            if (await db.Orders.AnyAsync()) return;

            var batch = new System.Collections.Generic.List<Order>();
            for (int i = 0; i < count; i++)
            {
                var drink = MockData.GetRandomDrink();
                var weight = Math.Clamp(0.8 - drink.Complexity, 0.0, 1.0);
                batch.Add(new Order
                {
                    DrinkType = drink.Name,
                    Status = "Pending",
                    PrepTimeWeight = weight
                });
            }

            db.Orders.AddRange(batch);
            await db.SaveChangesAsync();

            // Add only pending/active to visual list
            foreach (var o in batch.Where(b => !string.Equals(b.Status, "Complete", StringComparison.OrdinalIgnoreCase)))
                Orders.Add(o);
        }
        catch (Exception ex)
        {
            db.AuditLogs.Add(new AuditLog { Event = $"Seed Failed: {ex.Message}", Timestamp = DateTime.Now });
            await db.SaveChangesAsync();
            await DisplayAlert("Warning", "Failed to seed mock orders. Check logs.", "OK");
        }
    }

    /// <summary>
    /// Increases error probability as more orders exist, but caps at MaxErrorRate.
    /// Example: at 0 orders ~5%; at 25 orders => 5% + 5*2% = 15%; capped at 25%.
    /// </summary>
    private static bool ShouldSimulateError(int totalOrdersInDb)
    {
        var increments = totalOrdersInDb / 5; // integer division
        var p = Math.Min(BaseErrorRate + increments * IncrementPer5Orders, MaxErrorRate);
        return _rng.NextDouble() < p;
    }
}
