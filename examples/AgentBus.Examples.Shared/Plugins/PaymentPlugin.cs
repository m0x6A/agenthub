using System.ComponentModel;
using AgentBus.Examples.Shared.ExternalSystems;
using Microsoft.SemanticKernel;

namespace AgentBus.Examples.Shared.Plugins;

/// <summary>
/// Semantic Kernel plugin for payment processing and fraud detection.
/// Agent uses this to authorize payments and assess risk autonomously.
/// </summary>
public class PaymentPlugin
{
    private readonly MockPaymentGateway _paymentGateway;

    public PaymentPlugin(MockPaymentGateway paymentGateway)
    {
        _paymentGateway = paymentGateway;
    }

    [KernelFunction("authorize_payment")]
    [Description("Authorizes a payment for an order, including fraud detection. Returns authorization code and risk assessment. ALWAYS check this before confirming orders.")]
    public async Task<string> AuthorizePaymentAsync(
        [Description("The customer ID for this payment")] string customerId,
        [Description("Payment amount in USD")] decimal amount,
        [Description("Currency code (default: USD)")] string currency = "USD")
    {
        var result = await _paymentGateway.AuthorizePaymentAsync(customerId, amount, currency);
        var riskLevel = result.RiskScore > 70 ? "HIGH" : result.RiskScore > 30 ? "MEDIUM" : "LOW";
        var needsReview = result.Status == "fraud_review";
        return $"Payment authorization: {result.Status}, Auth Code: {result.AuthorizationCode ?? "N/A"}, Risk Score: {result.RiskScore}/100 ({riskLevel}), {(needsReview ? "MANUAL REVIEW REQUIRED" : "Auto-processed")}";
    }

    [KernelFunction("capture_payment")]
    [Description("Captures (charges) a previously authorized payment. Only use this after order is ready to ship.")]
    public async Task<string> CapturePaymentAsync(
        [Description("The transaction ID from previous authorization")] string transactionId,
        [Description("The authorization code")] string authorizationCode)
    {
        var result = await _paymentGateway.CapturePaymentAsync(transactionId, authorizationCode);
        return $"Payment capture: {result.Status}, Transaction ID: {result.TransactionId}";
    }
}
