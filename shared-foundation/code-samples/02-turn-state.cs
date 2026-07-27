// Article 2 — "The Shared Foundation: Building an AI Library You'd Actually Reuse"
// Per-turn state, and the doc-action separation filter that hangs off it.
//
// This object IS the safety architecture — not a diagram of it. It is scoped to
// one turn and thrown away when RunTurnAsync returns.
//
// The thing worth copying here is the LATCH. An earlier version of this class
// had a public `HasRetrieved` with a comment above it reading "never reset this
// within a turn" — a rule enforced by hope. It also let the flag and the chunk
// list drift apart: add a chunk, forget to set the flag, and doc-action
// separation is silently off for the rest of the turn with nothing to show for
// it. No exception, no log line, no failing test.
//
// If a safety invariant can be stated in a comment, it can be stated in the
// type instead. Then it stops being a rule anyone has to remember.

namespace SharedAi;

internal sealed class TurnState
{
    private readonly List<RetrievedChunk> _chunks = [];

    /// <summary>
    /// True once ANY retrieval has returned ANY chunk, from any index, at any
    /// trust level. Monotonic within a turn — nothing outside this class can
    /// set it, so nothing can put it back.
    /// </summary>
    public bool HasRetrieved { get; private set; }

    /// <summary>
    /// Every chunk is wrapped in fence markers before it enters the prompt. The
    /// trust level rides along so the model can weight factual reliability — it
    /// never confers permission to treat the content as instructions.
    /// </summary>
    public IReadOnlyList<RetrievedChunk> RetrievedChunks => _chunks;

    /// <summary>The flag and the list move together. There is no way to add a
    /// chunk without tripping the latch, because there is no other way to add one.</summary>
    public void AddRetrieved(RetrievedChunk chunk)
    {
        _chunks.Add(chunk);
        HasRetrieved = true;
    }

    /// <summary>Write-capable skills produce proposals, never executions. The
    /// confirmed proposal executes in a later turn carrying the user's explicit
    /// confirmation from a separate endpoint.</summary>
    public List<WriteProposal> WriteProposals { get; } = [];

    public TokenAccumulator BillableTokens { get; } = new();
}

public sealed record RetrievedChunk(string Text, TrustLevel TrustLevel, string Source);

public sealed class TokenAccumulator
{
    private readonly Dictionary<string, long> _byModel = [];
    public void Add(string model, long tokens) =>
        _byModel[model] = _byModel.GetValueOrDefault(model) + tokens;
    public IReadOnlyDictionary<string, long> ByModel => _byModel;
}

internal static class DocActionSeparation
{
    // Doc-action separation: once we've read anything, we don't write anything.
    //
    // This runs against each BATCH of tool calls the model returns, immediately
    // before dispatch — NOT once per turn. A model can emit several calls in one
    // response (a search and a delete together), and a filter applied while the
    // manifest was being assembled would have offered the write tool before the
    // search had run.
    public static IEnumerable<ToolCall> Dispatchable(
        IEnumerable<ToolCall> batch, TurnState state) =>
        batch.Where(call => !(state.HasRetrieved && call.IsWrite));

    // The manifest gets filtered too — no reason to dangle a tool you intend to
    // refuse. One removes the temptation, the other removes the possibility.
    public static IEnumerable<ToolDescriptor> Offerable(
        IEnumerable<ToolDescriptor> manifest, TurnState state) =>
        manifest.Where(tool => !(state.HasRetrieved && tool.IsWrite));
}

public sealed record ToolCall(string ToolId, string ArgsJson, bool IsWrite);
public sealed record ToolDescriptor(string ToolId, string SchemaJson, bool IsWrite);
