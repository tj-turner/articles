// The provider-agnostic seam for three real-money storefronts (Stripe, Apple
// IAP, Google Play Billing) that don't agree on what "purchase complete"
// means. One interface, three implementations, a resolver — the controller
// never knows which storefront it's talking to.
//
// The interface admits BOTH completion shapes:
//   - Stripe completes server-side. HandleWebhookAsync is the meaningful
//     method; ValidateReceiptAsync returns null (there's no client receipt).
//   - Apple/Google complete client-side. ValidateReceiptAsync is the
//     meaningful method; HandleWebhookAsync is a no-op in practice.
//
// That asymmetry — each method only meaningfully implemented by some
// members — is a smell we accepted deliberately. The alternative (two
// interfaces, two resolvers, two controller branches) leaks storefront
// identity right back into the call site we were trying to keep ignorant.
//
// From UpAllNight.Services.Interfaces.Services.IPaymentProvider
// and UpAllNight.Services.Economy.PaymentProviderResolver.

public interface IPaymentProvider
{
    PaymentProviderKind Kind { get; }
    bool IsEnabled { get; }

    // Sandbox is a first-class flag per provider, NOT an environment guess.
    // A sandbox purchase and a production purchase produce different rows
    // with different Environment values, queryable apart.
    bool IsSandbox { get; }

    Task<BeginPurchaseResult> BeginPurchaseAsync(
        BeginPurchaseRequest request,
        CancellationToken cancellationToken = default);

    // Returns null if the webhook should be ignored (replay, irrelevant
    // event, signature mismatch, etc.). Stripe is the only meaningful
    // implementer; mobile providers return null in practice.
    Task<WebhookEvent?> HandleWebhookAsync(
        string payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);

    // Validates a client-submitted receipt (Apple JWS / Google purchaseToken).
    // Stripe returns null — its purchases land via webhook only.
    Task<ReceiptValidationResult?> ValidateReceiptAsync(
        string receiptPayload,
        CancellationToken cancellationToken = default);
}

public class ReceiptValidationResult
{
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public PaymentEnvironment Environment { get; set; }
    public string? RawPayload { get; set; }
}

public class BeginPurchaseRequest
{
    public Guid UserId { get; set; }
    public string Sku { get; set; } = string.Empty;
}

public class BeginPurchaseResult
{
    // Provider-specific value the client uses to complete the purchase.
    //   Stripe: hosted Checkout URL the browser navigates to.
    //   Apple/Google: a nonce/identifier the native client uses to complete
    //   the IAP, then posts the resulting receipt back to the server.
    public string Payload { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
}

public enum WebhookEventKind
{
    PurchaseCompleted,
    PurchaseRefunded
}

public class WebhookEvent
{
    public WebhookEventKind Kind { get; set; }
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string? RawPayload { get; set; }
}

// The resolver indexes DI-registered providers by Kind. The controller asks
// for "the Apple one" or "the Stripe one" without knowing how either works.
public interface IPaymentProviderResolver
{
    IPaymentProvider Resolve(PaymentProviderKind kind);
}

public class PaymentProviderResolver : IPaymentProviderResolver
{
    private readonly Dictionary<PaymentProviderKind, IPaymentProvider> _providers;

    public PaymentProviderResolver(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Kind);
    }

    public IPaymentProvider Resolve(PaymentProviderKind kind) =>
        _providers.TryGetValue(kind, out var p)
            ? p
            : throw new InvalidOperationException(
                $"No registered IPaymentProvider for kind {kind}.");
}
