using System.Text.Json;
using Serilog;

namespace AgentBus.Examples.Shared.ExternalSystems;

/// <summary>
/// Mock Order Management System API (e.g., SAP, Salesforce, custom ERP)
/// </summary>
public class MockOrderSystemApi
{
    private readonly Dictionary<string, Order> _orders = new()
    {
        ["ORD-2024-001"] = new Order
        {
            OrderId = "ORD-2024-001",
            CustomerId = "C12345",
            CustomerName = "John Doe",
            Status = "confirmed",
            Items = new[]
            {
                new OrderItem { Sku = "SKU-456", ProductName = "Premium Widget", Quantity = 3, UnitPrice = 49.99m }
            },
            Subtotal = 149.97m,
            TaxAmount = 11.99m,
            ShippingAmount = 9.99m,
            Total = 171.95m,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ShippingAddress = "123 Main St, Seattle, WA 98101"
        }
    };

    public async Task<Order?> GetOrderAsync(string orderId)
    {
        await Task.Delay(100); // Simulate API latency
        
        if (_orders.TryGetValue(orderId, out var order))
        {
            Log.Information("[OrderSystem API] Retrieved order: {OrderId}", orderId);
            return order;
        }
        
        Log.Warning("[OrderSystem API] Order not found: {OrderId}", orderId);
        return null;
    }

    public async Task<Order> UpdateOrderAsync(string orderId, OrderItem[] additionalItems)
    {
        await Task.Delay(150); // Simulate API latency
        
        if (!_orders.TryGetValue(orderId, out var order))
            throw new InvalidOperationException($"Order {orderId} not found");

        var updatedItems = order.Items.Concat(additionalItems).ToArray();
        var newSubtotal = updatedItems.Sum(i => i.Quantity * i.UnitPrice);
        var newTax = newSubtotal * 0.08m;
        var newTotal = newSubtotal + newTax + order.ShippingAmount;

        var updatedOrder = order with
        {
            Items = updatedItems,
            Subtotal = newSubtotal,
            TaxAmount = newTax,
            Total = newTotal,
            Status = "pending_payment"
        };

        _orders[orderId] = updatedOrder;
        
        Log.Information("[OrderSystem API] Updated order {OrderId}: New total ${Total}", orderId, newTotal);
        return updatedOrder;
    }

    public async Task<Order> ConfirmOrderAsync(string orderId, string paymentAuthCode)
    {
        await Task.Delay(100);
        
        if (!_orders.TryGetValue(orderId, out var order))
            throw new InvalidOperationException($"Order {orderId} not found");

        var confirmed = order with { Status = "confirmed", PaymentAuthCode = paymentAuthCode };
        _orders[orderId] = confirmed;
        
        Log.Information("[OrderSystem API] Order confirmed: {OrderId} with payment {AuthCode}", orderId, paymentAuthCode);
        return confirmed;
    }
}

public record Order
{
    public required string OrderId { get; init; }
    public required string CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public required string Status { get; init; }
    public required OrderItem[] Items { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal TaxAmount { get; init; }
    public required decimal ShippingAmount { get; init; }
    public required decimal Total { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string ShippingAddress { get; init; }
    public string? PaymentAuthCode { get; init; }
}

public record OrderItem
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}
