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
// Builds against Polly.Core v8 + Azure.Core. Note the two exception types below:
// which one you get depends on which SDK you are holding, and getting it wrong
// gives you a pipeline that compiles, reads correctly, and retries nothing.

using Azure;                 // RequestFailedException  (Azure.Core)
using System.ClientModel;    // ClientResultException   (Azure.AI.OpenAI 2.x / OpenAI v2)
using Polly;
using Polly.Retry;

public sealed class ModelCallResilience
{
    // Model + retrieval CALLS: bounded, backoff-driven retry that honors the
    // rate-limit response. 429s under load are exactly what this is for.
    public static ResiliencePipeline BuildCallPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                // Azure.AI.Inference and Azure AI Search throw RequestFailedException.
                // Azure.AI.OpenAI 2.x throws ClientResultException, which does NOT
                // derive from it — handle only the first and the predicate silently
                // never matches on the SDK most readers are actually using.
                ShouldHandle = new PredicateBuilder()
                    .Handle<RequestFailedException>(ex => ex.Status == 429)
                    .Handle<ClientResultException>(ex => ex.Status == 429),

                MaxRetryAttempts = 3,
                BackoffType      = DelayBackoffType.Exponential,

                // 2s is the exponential BASE, not the delay you will observe.
                // UseJitter switches Polly v8 to a decorrelated-jitter formula, so
                // the real sequence is neither 2/4/8 nor monotonic — a measured run
                // gave 2.48s, 1.67s, 3.03s. That spread is the entire point: it is
                // what stops N pods retrying in lockstep and re-flooding the region.
                Delay     = TimeSpan.FromSeconds(2),
                UseJitter = true,
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

public sealed record WriteResult(bool Accepted, string OperationId);

public interface IWriteExecutor
{
    // Implementations MUST NOT wrap this in a retry policy. And the key is inert
    // on its own — safety comes from a downstream that persists it under a
    // uniqueness constraint and replays the stored result on a repeat. A key
    // nobody enforces is a comment.
    Task<WriteResult> ExecuteOnceAsync(WriteAction action, CancellationToken ct);
}
