// Article 4 — "Documents Are Never Instructions: Safety Walls That Don't Ask Who Wrote It"
//
// The second wall. Doc-action separation, applied immediately before dispatch.
//
// Two things here that the article's prose can only assert:
//
//   1. The latch is set through AddRetrieved, never by assignment. Article 2's
//      TurnState has `public bool HasRetrieved { get; private set; }` for exactly
//      this reason, so `state.HasRetrieved = true` does not compile. An invariant
//      stated in the type stops being a rule anyone has to remember.
//
//   2. A blocked call gets a refusal result, never silence. Both major provider
//      APIs reject the next request when an emitted tool call has no paired
//      result — so a bare .Where(...) filter over the batch breaks the loop on
//      the following turn. It also leaves the model having emitted an approval
//      and heard nothing, and the likeliest completion is a confident report
//      that the payment went through.

using SharedAi.Retrieval;

namespace SharedAi.Orchestration;

public sealed record ToolCall(string ToolId, string ArgsJson, bool IsWrite);

public sealed record ToolResult(string ToolId, string Payload, bool IsRefusal)
{
    public static ToolResult Refused(string toolId, string reason) => new(toolId, reason, true);
    public static ToolResult Ok(string toolId, string payload) => new(toolId, payload, false);
}

public interface IToolDispatcher
{
    Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken ct);
}

/// <summary>Trimmed to the two members this file needs. Full version in Article 2.</summary>
public sealed class TurnState
{
    private readonly List<RetrievedChunk> _chunks = [];

    public bool HasRetrieved { get; private set; }

    public void AddRetrieved(RetrievedChunk chunk)
    {
        _chunks.Add(chunk);
        HasRetrieved = true;
    }
}

public static class DocActionSeparation
{
    public const string WriteBlockedAfterRetrieval =
        "Write skills are unavailable for the remainder of a turn that retrieved content. " +
        "The request was not performed and nothing was proposed.";

    /// <summary>
    /// Called wherever retrieval results come back — one path, so there is one
    /// place to get this wrong. No branch on provenance: a first-party wiki page
    /// latches the turn exactly as a customer upload does.
    /// </summary>
    public static void RecordRetrieval(TurnState state, IReadOnlyList<RetrievedChunk> chunks)
    {
        foreach (var chunk in chunks)
            state.AddRetrieved(chunk);
    }

    /// <summary>
    /// Applied immediately before dispatch rather than only while building the
    /// manifest. A model can emit a search and an approval in one response, and
    /// when the manifest was assembled nothing had been retrieved yet — so the
    /// manifest filter structurally cannot catch a retrieve-then-write turn.
    /// Both filters run. One removes the temptation, the other removes the
    /// possibility.
    /// </summary>
    public static async Task<IReadOnlyList<ToolResult>> DispatchAsync(
        IReadOnlyList<ToolCall> batch,
        TurnState state,
        IToolDispatcher dispatcher,
        CancellationToken ct)
    {
        var results = new List<ToolResult>(batch.Count);

        foreach (var call in batch)
        {
            if (state.HasRetrieved && call.IsWrite)
            {
                results.Add(ToolResult.Refused(call.ToolId, WriteBlockedAfterRetrieval));
                continue;
            }

            results.Add(await dispatcher.InvokeAsync(call, ct));
        }

        return results;
    }
}
