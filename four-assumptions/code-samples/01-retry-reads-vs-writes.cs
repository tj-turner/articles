// Article 1 — "Building AI Infrastructure: Four Backend Assumptions to Rewire"
// Assumption 4: retrying is safe (it isn't).
//
// The point of this snippet is not the Polly configuration — it's the SPLIT.
// A model/retrieval call is transient-failure-prone and idempotent enough to
// retry with backoff. A write is neither: the same prompt can yield a
// different action, and a re-driven action is a *second* action. So writes
// don't retry — they carry an idempotency key fixed before the first attempt,
// and a re-drive collapses onto the same operation instead of repeating it.
//
// Uses Polly v8 (Microsoft.Extensions.Resilience) + Azure.AI style 429s.

using Polly;
using Polly.Retry;

public sealed class FoundryResilience
{
    // Model + retrieval CALLS: bounded, backoff-driven retry that honors the
    // rate-limit response. 429s on a hot region are exactly what this is for.
    public static ResiliencePipeline BuildCallPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<RequestFailedException>(ex => ex.Status == 429),
                MaxRetryAttempts = 3,
                BackoffType      = DelayBackoffType.Exponential,
                Delay            = TimeSpan.FromSeconds(2),   // ~2s, 4s, 8s
                UseJitter        = true,                      // spread the retry storm
            })
            .Build();
}

// WRITES take the other path. No retry pipeline — an idempotency key decided
// up front. If anything re-drives this call (a queue redelivery, a client
// retry, a duplicate confirm), the downstream API treats the same key as the
// same operation. "Try again" is a question already answered before we ran.
public sealed record WriteAction(
    Guid   IdempotencyKey,   // fixed BEFORE the first attempt, never regenerated
    string Operation,
    object Payload);

public interface IWriteExecutor
{
    // Implementations MUST NOT wrap this in a retry policy. Safety comes from
    // the key + a single-writer downstream, not from re-driving the call.
    Task<WriteResult> ExecuteOnceAsync(WriteAction action, CancellationToken ct);
}
