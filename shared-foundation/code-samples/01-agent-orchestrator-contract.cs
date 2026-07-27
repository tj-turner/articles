// Article 2 — "The Shared Foundation: Building an AI Library You'd Actually Reuse"
// The orchestrator contract: the whole agent loop behind one interface.
//
// The point of this file is the SHAPE, not the implementation. Four parameters,
// and each one is a decision:
//
//   TurnRequest   — what the caller is asking for, including the idempotency key
//                   so a retried POST collapses onto the same turn.
//   ITurnObserver — how this consumer wants events delivered. The chat service
//                   pushes tokens onto an SSE response; the partner-tool service
//                   buffers them into a single body. Same loop, different observer.
//   TurnOptions   — where consumers differ. Every option can only ever NARROW
//                   what a turn may do; nothing on it widens anything.
//   CancellationToken — on a streaming endpoint this is RequestAborted.
//
// There is no service named "orchestrator". This is a library type that compiles
// into each consumer.

namespace SharedAi;

public interface IAgentOrchestrator
{
    Task<TurnResult> RunTurnAsync(
        TurnRequest       request,
        ITurnObserver     observer,
        TurnOptions       options,
        CancellationToken ct);
}

public sealed record TurnRequest(
    Guid    ConversationId,
    string  Message,
    Caller  Caller,
    Guid    IdempotencyKey);

/// <summary>
/// Consumer-supplied configuration for a single turn.
/// INVARIANT: every property here may only restrict what the turn can do.
/// Adding an option that widens privilege breaks the composition argument —
/// consumers choose what the model may do, never whether the controls run.
/// </summary>
public sealed record TurnOptions
{
    public bool Streaming { get; init; }

    /// <summary>False blocks all write-capable skills at dispatch and returns a
    /// policy error to the caller. It never silently drops the call — a blocked
    /// action is a signal about what the model thinks it is allowed to do.</summary>
    public bool WriteSkillsAllowed { get; init; }

    public required OriginatingService OriginatingService { get; init; }

    /// <summary>Applied to inbound text before it enters the loop. The partner-agent
    /// service tags another party's agent output as untrusted here.</summary>
    public TrustLevel? InboundTrustOverride { get; init; }
}

public enum OriginatingService { Chat, PartnerTool, PartnerAgent }

public enum TrustLevel { Trusted, Untrusted, AgentUntrusted }

/// <summary>
/// Callback surface implemented by the consuming service. All callbacks are
/// best-effort and non-blocking; the orchestrator does not depend on what the
/// consumer does with them.
/// </summary>
public interface ITurnObserver
{
    Task OnTokenAsync(string token, CancellationToken ct);
    Task OnToolCallAsync(string toolId, CancellationToken ct);
    Task OnWriteProposalAsync(Guid proposalId, CancellationToken ct);
    Task OnOverCapAsync(CancellationToken ct);
}

public sealed record TurnResult(
    string                       FinalText,
    IReadOnlyList<string>        ToolCallsExecuted,
    IReadOnlyList<WriteProposal> WriteProposals,
    TokenAccumulator             BillableTokens);

public sealed record Caller(Guid UserId, Guid TenantId);

public sealed record WriteProposal(Guid ProposalId, string SkillId, string ResolvedArgsJson);
