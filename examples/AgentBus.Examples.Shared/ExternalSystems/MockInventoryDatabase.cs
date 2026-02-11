using Serilog;

namespace AgentBus.Examples.Shared.ExternalSystems;

/// <summary>
/// Mock Inventory Database (e.g., SQL Server, PostgreSQL, CosmosDB)
/// </summary>
public class MockInventoryDatabase
{
    private readonly Dictionary<string, InventoryItem> _inventory = new()
    {
        ["SKU-789"] = new InventoryItem
        {
            Sku = "SKU-789",
            ProductName = "Premium Widget Plus",
            QuantityAvailable = 47,
            QuantityReserved = 3,
            ReorderPoint = 10,
            UnitPrice = 24.99m,
            WarehouseLocation = "WH-East-01",
            LastRestocked = DateTime.UtcNow.AddDays(-5)
        },
        ["SKU-456"] = new InventoryItem
        {
            Sku = "SKU-456",
            ProductName = "Premium Widget",
            QuantityAvailable = 15,
            QuantityReserved = 8,
            ReorderPoint = 20,
            UnitPrice = 49.99m,
            WarehouseLocation = "WH-East-01",
            LastRestocked = DateTime.UtcNow.AddDays(-3)
        },
        ["SKU-123"] = new InventoryItem
        {
            Sku = "SKU-123",
            ProductName = "Basic Widget",
            QuantityAvailable = 0,
            QuantityReserved = 0,
            ReorderPoint = 15,
            UnitPrice = 19.99m,
            WarehouseLocation = "WH-West-02",
            LastRestocked = DateTime.UtcNow.AddDays(-15)
        }
    };

    public async Task<InventoryItem?> CheckInventoryAsync(string sku)
    {
        await Task.Delay(50); // Simulate DB query latency
        
        if (_inventory.TryGetValue(sku, out var item))
        {
            Log.Debug("[Inventory DB] Checked {Sku}: {Available} available", sku, item.QuantityAvailable);
            return item;
        }
        
        Log.Warning("[Inventory DB] SKU not found: {Sku}", sku);
        return null;
    }

    public async Task<string> ReserveInventoryAsync(string sku, int quantity)
    {
        await Task.Delay(70);
        
        if (!_inventory.TryGetValue(sku, out var item))
            throw new InvalidOperationException($"SKU {sku} not found");

        if (item.QuantityAvailable < quantity)
            throw new InvalidOperationException($"Insufficient inventory for {sku}");

        var updated = item with
        {
            QuantityAvailable = item.QuantityAvailable - quantity,
            QuantityReserved = item.QuantityReserved + quantity
        };
        
        _inventory[sku] = updated;
        
        var reservationId = $"RES-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        Log.Information("[Inventory DB] Reserved {Quantity}x {Sku}: Reservation {ReservationId}", 
            quantity, sku, reservationId);
        
        return reservationId;
    }

    public async Task<WarehouseFulfillment> DetermineFulfillmentAsync(string[] skus, string shippingZipCode)
    {
        await Task.Delay(100);
        
        // Simple logic - in reality would calculate distances, costs, etc.
        var warehouse = shippingZipCode.StartsWith("9") ? "WH-West-02" : "WH-East-01";
        var shippingDays = shippingZipCode.StartsWith("9") ? 2 : 1;
        
        Log.Information("[Inventory DB] Fulfillment: {Warehouse}, ETA {Days} days", warehouse, shippingDays);
        
        return new WarehouseFulfillment
        {
            WarehouseCode = warehouse,
            EstimatedShippingDays = shippingDays,
            ShippingCost = shippingDays == 1 ? 9.99m : 14.99m
        };
    }
}

public record InventoryItem
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public required int QuantityAvailable { get; init; }
    public required int QuantityReserved { get; init; }
    public required int ReorderPoint { get; init; }
    public required decimal UnitPrice { get; init; }
    public required string WarehouseLocation { get; init; }
    public required DateTime LastRestocked { get; init; }
}

public record WarehouseFulfillment
{
    public required string WarehouseCode { get; init; }
    public required int EstimatedShippingDays { get; init; }
    public required decimal ShippingCost { get; init; }
}
