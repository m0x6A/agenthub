using System.ComponentModel;
using AgentBus.Examples.Shared.ExternalSystems;
using Microsoft.SemanticKernel;

namespace AgentBus.Examples.Shared.Plugins;

/// <summary>
/// Semantic Kernel plugin that exposes Order System capabilities as AI-callable functions.
/// The agent decides when and how to use these tools based on context.
/// </summary>
public class OrderSystemPlugin
{
    private readonly MockOrderSystemApi _orderSystem;

    public OrderSystemPlugin(MockOrderSystemApi orderSystem)
    {
        _orderSystem = orderSystem;
    }

    [KernelFunction("get_order_details")]
    [Description("Retrieves complete order information including items, customer details, and status. Use this when you need to answer questions about an order or verify order information.")]
    public async Task<string> GetOrderDetailsAsync(
        [Description("The unique order identifier (e.g., ORD-12345)")] string orderId)
    {
        var order = await _orderSystem.GetOrderAsync(orderId);
        if (order == null) return $"Order {orderId} not found";
        return $"Order {order.OrderId}: {order.Items.Length} items, Status: {order.Status}, Customer: {order.CustomerName}, Total: ${order.Total:F2}";
    }

    [KernelFunction("update_order")]
    [Description("Updates an order by adding additional items. Use this when customer wants to modify their order.")]
    public async Task<string> UpdateOrderAsync(
        [Description("The order ID to update")] string orderId,
        [Description("Product SKU to add")] string productSku,
        [Description("Quantity to add")] int quantity,
        [Description("Unit price")] decimal unitPrice)
    {
        var items = new[] { new ExternalSystems.OrderItem { Sku = productSku, ProductName = productSku, Quantity = quantity, UnitPrice = unitPrice } };
        var updated = await _orderSystem.UpdateOrderAsync(orderId, items);
        return $"Successfully updated order {orderId}: New total ${updated.Total:F2}";
    }

    [KernelFunction("confirm_order_fulfillment")]
    [Description("Marks an order as confirmed and ready for fulfillment. Use this after all checks (inventory, payment) are complete.")]
    public async Task<string> ConfirmOrderAsync(
        [Description("The order ID to confirm")] string orderId,
        [Description("The payment authorization code")] string paymentAuthCode)
    {
        await _orderSystem.ConfirmOrderAsync(orderId, paymentAuthCode);
        return $"Order {orderId} confirmed and forwarded to fulfillment";
    }
}
