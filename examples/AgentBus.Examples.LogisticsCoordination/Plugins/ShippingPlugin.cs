using System.ComponentModel;
using Microsoft.SemanticKernel;
using Serilog;

namespace AgentBus.Examples.LogisticsCoordination.Plugins;

/// <summary>
/// Shipping operations plugin - agents can query routes, carriers, and shipping feasibility
/// </summary>
public class ShippingPlugin
{
    private readonly ILogger _logger;

    public ShippingPlugin(ILogger logger)
    {
        _logger = logger;
    }

    [KernelFunction("check_carrier_options")]
    [Description("Check available shipping carriers and costs for a route. Returns cost, time, and capacity.")]
    public Task<string> CheckCarrierOptionsAsync(
        [Description("Destination city (e.g., 'Tokyo', 'Singapore')")] string destination,
        [Description("Whether express (24h) or standard (48h+) shipping")] bool isExpressed,
        [Description("Package weight in kg")] decimal weightKg)
    {
        // Mock carrier data
        var carriers = isExpressed switch
        {
            true => new[]
            {
                "Express Courier (24h): $3,500, capacity unlimited",
                "Fast Freight (express): $2,800, capacity unlimited",
                "Premium Air (24h): $4,200, fastest option"
            },
            false => new[]
            {
                "Standard Shipping (5-7 days): $450",
                "Ocean Freight (14-21 days): $200",
                "Rail + Sea Combo (10-14 days): $300"
            }
        };

        _logger.Information("📍 Shipping Agent checking carriers to {Destination}", destination);
        return Task.FromResult(string.Join("\n", carriers));
    }

    [KernelFunction("estimate_consolidation_feasibility")]
    [Description("Check if consolidating shipments from multiple warehouses is feasible for a deadline")]
    public Task<string> EstimateConsolidationFeasibilityAsync(
        [Description("Required delivery time in hours")] int hoursUntilDeadline,
        [Description("Whether items need to come from multiple locations")] bool multipleLocations)
    {
        var feasible = multipleLocations && hoursUntilDeadline >= 6;
        var message = feasible
            ? $"Consolidation IS feasible with {hoursUntilDeadline} hours. Will need ~4 hours for coordination + 2 hours for buffer."
            : $"Consolidation NOT feasible. Need at least 6 hours; you have {hoursUntilDeadline} hours available.";

        _logger.Information("🔄 Consolidation feasibility: {Feasible}", message);
        return Task.FromResult(message);
    }

    [KernelFunction("calculate_shipping_cost")]
    [Description("Calculate total shipping cost for a shipment")]
    public Task<string> CalculateShippingCostAsync(
        [Description("Base carrier cost")] decimal carrierCost,
        [Description("Weight of shipment in kg")] decimal weightKg,
        [Description("Any surcharges (consolidation, hazmat, etc)")] decimal surcharges = 0)
    {
        var total = carrierCost + (weightKg * 5) + surcharges; // $5 per kg handling
        return Task.FromResult($"Total shipping cost: ${total:F2} (carrier: ${carrierCost:F2}, handling: ${weightKg * 5:F2}, surcharges: ${surcharges:F2})");
    }
}

/// <summary>
/// Warehouse operations plugin - query inventory, consolidation costs, picking/packing
/// </summary>
public class WarehousePlugin
{
    private readonly ILogger _logger;

    public WarehousePlugin(ILogger logger)
    {
        _logger = logger;
    }

    [KernelFunction("check_inventory_distribution")]
    [Description("Check inventory levels across all warehouse locations")]
    public Task<string> CheckInventoryDistributionAsync(
        [Description("SKU or product name")] string productName,
        [Description("Quantity needed")] int quantityNeeded)
    {
        // Mock warehouse data
        var distribution = new Dictionary<string, int>
        {
            { "Sydney Local Warehouse", quantityNeeded == 10 ? 8 : 150 },
            { "Melbourne Hub", quantityNeeded == 10 ? 1 : 30 },
            { "Osaka Regional Hub", quantityNeeded == 10 ? 1 : 20 },
            { "Singapore Hub", quantityNeeded == 10 ? 0 : 15 }
        };

        var report = $"Inventory Report for '{productName}' (need {quantityNeeded} units):\n";
        var totalAvailable = 0;
        foreach (var (location, count) in distribution)
        {
            report += $"  • {location}: {count} units\n";
            totalAvailable += count;
        }

        var percentageLocal = distribution["Sydney Local Warehouse"] > 0
            ? (distribution["Sydney Local Warehouse"] * 100) / quantityNeeded
            : 0;

        report += $"\nTotal available: {totalAvailable} units\n";
        report += $"Percentage in local warehouse (Sydney): {percentageLocal}%\n";
        report += $"Can fulfill: {(totalAvailable >= quantityNeeded ? "YES ✓" : "NO ✗")}";

        _logger.Information("📦 Warehouse inventory check: {ProductName}", productName);
        return Task.FromResult(report);
    }

    [KernelFunction("estimate_consolidation_cost")]
    [Description("Estimate cost and time to consolidate inventory from multiple locations")]
    public Task<string> EstimateConsolidationCostAsync(
        [Description("Number of source warehouses to consolidate from")] int sourceLocationCount,
        [Description("Expected pick time in minutes")] int pickTimeMinutes)
    {
        var laborHours = (pickTimeMinutes / 60.0) + 1; // +1 hour for coordination
        var costPerHour = 50m; // $50/hour labor

        var laborCost = (decimal)laborHours * costPerHour;
        var transportCost = sourceLocationCount > 1 ? 100m : 0; // Inter-warehouse transport
        var total = laborCost + transportCost;

        var report = $"Consolidation Cost Estimate:\n";
        report += $"  • Labor (pick + pack + coord): {laborHours:F1} hours @ ${costPerHour}/hr = ${laborCost:F2}\n";
        report += $"  • Inter-warehouse transport: ${transportCost:F2}\n";
        report += $"  • Total additional cost: ${total:F2}\n";
        report += $"  • Time required: ~{Math.Ceiling(laborHours) + 1} hours";

        _logger.Information("💰 Consolidation cost estimate calculated");
        return Task.FromResult(report);
    }

    [KernelFunction("confirm_warehouse_readiness")]
    [Description("Confirm that warehouse can execute a pick/pack operation by a specific time")]
    public Task<string> ConfirmWarehouseReadinessAsync(
        [Description("Time remaining until the operation must be complete (in hours)")] int hoursRemaining,
        [Description("Complexity of operation (simple, moderate, complex)")] string complexity)
    {
        var canExecute = complexity switch
        {
            "simple" => hoursRemaining >= 2,
            "moderate" => hoursRemaining >= 4,
            "complex" => hoursRemaining >= 6,
            _ => hoursRemaining >= 4
        };

        var message = canExecute
            ? $"✓ CONFIRMED: Warehouse can execute {complexity} operation within {hoursRemaining} hours"
            : $"✗ NOT POSSIBLE: {complexity} operation needs more time than available ({hoursRemaining}h)";

        _logger.Information("🏭 Warehouse readiness: {Message}", message);
        return Task.FromResult(message);
    }
}
