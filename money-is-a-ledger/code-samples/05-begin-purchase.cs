// The Stripe provider's BeginPurchaseAsync — the spot where the small but
// load-bearing ordering happens: RecordPendingAsync runs BEFORE the
// Checkout URL is returned to the client.
//
// Why the order matters: the webhook (checkout.session.completed) can
// arrive in milliseconds after the client navigates. If the pending row
// doesn't already exist, the grant SP would see NotFound and bail — the
// user paid and the system "lost" the receipt. Writing the pending row
// first means the webhook always has a row to settle against rather than
// racing to create one.
//
// `session.Id` is what later becomes ProviderTransactionId on the purchase
// row. It's unique per Stripe Checkout Session, which is what makes the
// (Provider, ProviderTransactionId) idempotency key work — a re-delivered
// webhook finds the existing row and the grant SP no-ops.
//
// From UpAllNight.Infrastructure.Payments.StripeProvider.

public class StripeProvider : IPaymentProvider
{
    private readonly StripeSettings _settings;
    private readonly IChipPackRepository _chipPackRepository;
    private readonly ILogger<StripeProvider> _logger;

    public StripeProvider(
        StripeSettings settings,
        IChipPackRepository chipPackRepository,
        ILogger<StripeProvider> logger)
    {
        _settings = settings;
        _chipPackRepository = chipPackRepository;
        _logger = logger;
        if (settings.Enabled)
        {
            StripeConfiguration.ApiKey = settings.SecretKey;
        }
    }

    public PaymentProviderKind Kind => PaymentProviderKind.Stripe;
    public bool IsEnabled => _settings.Enabled;
    public bool IsSandbox => _settings.Sandbox;

    public async Task<BeginPurchaseResult> BeginPurchaseAsync(
        BeginPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            throw new InvalidOperationException("Stripe is not enabled in this environment.");

        var pack = await _chipPackRepository.GetBySkuAsync(request.Sku, cancellationToken)
            ?? throw new InvalidOperationException($"Chip pack '{request.Sku}' not found.");

        if (string.IsNullOrEmpty(pack.StripePriceId))
            throw new InvalidOperationException($"Chip pack '{request.Sku}' has no StripePriceId configured.");

        var sessionOptions = new SessionCreateOptions
        {
            Mode       = "payment",
            SuccessUrl = _settings.SuccessUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl  = _settings.CancelUrl,
            LineItems  = new List<SessionLineItemOptions>
            {
                new() { Price = pack.StripePriceId, Quantity = 1 }
            },
            // Carry the user/sku across the redirect so the webhook can find
            // them again. The webhook payload contains the session, the
            // session contains this metadata.
            Metadata = new Dictionary<string, string>
            {
                ["userId"]     = request.UserId.ToString(),
                ["sku"]        = request.Sku,
                ["chipPackId"] = pack.ChipPackId.ToString()
            },
            ClientReferenceId = $"{request.UserId:N}|{request.Sku}"
        };

        var service = new SessionService();
        var session = await service.CreateAsync(sessionOptions, cancellationToken: cancellationToken);

        // *** This is the line the article is about. ***
        // Pending row is written BEFORE the client gets the URL it'll use to
        // pay. The webhook from `checkout.session.completed` arrives later
        // and the grant SP looks the row up by (Stripe, session.Id) — which
        // is exactly the (Provider, ProviderTransactionId) pair this just
        // inserted. RecordPendingAsync is itself idempotent: if the same
        // session.Id already exists, it returns the existing row.
        await _chipPackRepository.RecordPendingAsync(
            request.UserId,
            pack.ChipPackId,
            PaymentProviderKind.Stripe,
            session.Id,
            _settings.Sandbox ? PaymentEnvironment.Sandbox : PaymentEnvironment.Production,
            cancellationToken);

        return new BeginPurchaseResult
        {
            Payload = session.Url,    // Browser navigates here.
            ProviderTransactionId = session.Id
        };
    }

    // HandleWebhookAsync and ValidateReceiptAsync omitted — see article §5
    // for why Stripe's ValidateReceiptAsync returns null and what the webhook
    // handler actually does (verifies signature, dispatches
    // checkout.session.completed → PurchaseCompleted, ignores everything else).
}
