using Serilog;

namespace AgentBus.Examples.Shared.ExternalSystems;

/// <summary>
/// Mock Payment Gateway (e.g., Stripe, PayPal, Authorize.net)
/// </summary>
public class MockPaymentGateway
{
    private readonly Dictionary<string, PaymentTransaction> _transactions = new();

    public async Task<PaymentAuthorizationResult> AuthorizePaymentAsync(
        string customerId, 
        decimal amount, 
        string currency = "USD")
    {
        await Task.Delay(200); // Simulate payment gateway latency
        
        // Simple fraud check
        var riskScore = CalculateRiskScore(amount);
        var transactionId = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        var authCode = $"AUTH-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        
        var approved = riskScore < 80 && amount < 5000m;
        
        var result = new PaymentAuthorizationResult
        {
            TransactionId = transactionId,
            AuthorizationCode = approved ? authCode : null,
            Status = approved ? "approved" : (riskScore >= 80 ? "fraud_review" : "declined"),
            Amount = amount,
            Currency = currency,
            RiskScore = riskScore,
            ProcessedAt = DateTime.UtcNow,
            Message = approved ? "Payment authorized successfully" :
                     riskScore >= 80 ? "Transaction requires manual review" :
                     "Declined - amount exceeds limit"
        };

        _transactions[transactionId] = new PaymentTransaction
        {
            TransactionId = transactionId,
            CustomerId = customerId,
            Amount = amount,
            Currency = currency,
            Status = result.Status,
            Timestamp = DateTime.UtcNow
        };

        Log.Information("[Payment Gateway] Authorization {Status}: {TransactionId} for ${Amount} (Risk: {RiskScore})",
            result.Status, transactionId, amount, riskScore);
        
        return result;
    }

    public async Task<CaptureResult> CapturePaymentAsync(string transactionId, string authorizationCode)
    {
        await Task.Delay(150);
        
        if (!_transactions.ContainsKey(transactionId))
            throw new InvalidOperationException($"Transaction {transactionId} not found");

        var transaction = _transactions[transactionId];
        var updated = transaction with { Status = "captured" };
        _transactions[transactionId] = updated;

        Log.Information("[Payment Gateway] Captured payment: {TransactionId}", transactionId);
        
        return new CaptureResult
        {
            TransactionId = transactionId,
            Status = "captured",
            CapturedAt = DateTime.UtcNow
        };
    }

    private int CalculateRiskScore(decimal amount)
    {
        // Simple risk scoring
        if (amount > 1000m) return 65;
        if (amount > 500m) return 35;
        return 12;  // Low risk
    }
}

public record PaymentAuthorizationResult
{
    public required string TransactionId { get; init; }
    public string? AuthorizationCode { get; init; }
    public required string Status { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required int RiskScore { get; init; }
    public required DateTime ProcessedAt { get; init; }
    public required string Message { get; init; }
}

public record CaptureResult
{
    public required string TransactionId { get; init; }
    public required string Status { get; init; }
    public required DateTime CapturedAt { get; init; }
}

public record PaymentTransaction
{
    public required string TransactionId { get; init; }
    public required string CustomerId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Status { get; init; }
    public required DateTime Timestamp { get; init; }
}
