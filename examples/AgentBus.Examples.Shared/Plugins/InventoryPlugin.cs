using System.ComponentModel;
using AgentBus.Examples.Shared.ExternalSystems;
using Microsoft.SemanticKernel;

namespace AgentBus.Examples.Shared.Plugins;

/// <summary>
/// Semantic Kernel plugin for inventory management operations.
/// Agent uses this to check stock levels and make inventory decisions.
/// </summary>
public class InventoryPlugin
{
    private readonly MockInventoryDatabase _inventory;

    public InventoryPlugin(MockInventoryDatabase inventory)
    {
        _inventory = inventory;
    }

    [KernelFunction("check_inventory")]
    [Description("Checks current inventory levels for a product. Returns quantity available, reserved, and location.")]
    public async Task<string> CheckInventoryAsync(
        [Description("The product SKU or identifier")] string productId)
    {
        var result = await _inventory.CheckInventoryAsync(productId);
        if (result == null) return $"Product {productId} not found in inventory";
        return $"Product {productId}: {result.QuantityAvailable} available, {result.QuantityReserved} reserved, Location: {result.WarehouseLocation}";
    }

    [KernelFunction("reserve_inventory")]
    [Description("Reserves inventory for an order, preventing other orders from claiming it. Use this when confirming an order.")]
    public async Task<string> ReserveInventoryAsync(
        [Description("The product to reserve")] string productId,
        [Description("Quantity to reserve")] int quantity)
    {
        try
        {
            var reservationId = await _inventory.ReserveInventoryAsync(productId, quantity);
            return $"Reserved {quantity} units of {productId}, Reservation ID: {reservationId}";
        }
        catch (Exception ex)
        {
            return $"Failed to reserve {quantity} units of {productId}: {ex.Message}";
        }
    }

    [KernelFunction("determine_fulfillment_strategy")]
    [Description("Analyzes inventory distribution and determines optimal fulfillment strategy (single warehouse, split shipment, drop ship, etc.)")]
    public async Task<string> DetermineFulfillmentAsync(
        [Description("List of products needed (comma-separated SKUs)")] string products,
        [Description("Shipping ZIP code")] string zipCode)
    {
        var skus = products.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var strategy = await _inventory.DetermineFulfillmentAsync(skus, zipCode);
        return $"Fulfillment from {strategy.WarehouseCode}: {strategy.EstimatedShippingDays} days, Shipping: ${strategy.ShippingCost:F2}";
    }
}
