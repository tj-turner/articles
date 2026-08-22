// Article 4 — "Documents Are Never Instructions: Safety Walls That Don't Ask Who Wrote It"
//
// The second wall. Doc-action separation, applied immediately before dispatch.
//
// Three things here that the article's prose can only assert:
//
//   1. The latch is set through AddRetrieved, never by assignment. Article 2's
//      TurnState has `public bool HasRetrieved { get; private set; }` for exactly
//      this reason, so `state.HasRetrieved = true` does not compile. An invariant
//      stated in the type stops being a rule anyone has to remember.
//
//   2. Whether a call is a write is looked up, not asked. The classification
//      comes from the catalog by tool id at dispatch time, so a ToolCall carries
//      no say in how it is treated. A control that reads a boolean the call
//      brought with it is not structural, whatever the prose around it claims.
//      An id the catalog does not know is refused rather than dispatched.
//
//   3. A blocked call gets a refusal result, never silence. The provider APIs
//      this runs against reject the next request when an emitted tool call has no
//      paired result — so a bare .Where(...) filter over the batch breaks the loop
//      on the following turn. Article 2 committed this as that .Where filter; this
//      file is the correction. Silence also leaves the model having emitted an
//      approval and heard nothing, and the likeliest completion is a confident
//      report that the payment went through.

using SharedAi.Retrieval;

namespace SharedAi.Orchestration;

public sealed record ToolCall(string ToolId, string ArgsJson);

public sealed record ToolResult(string ToolId, string Payload, bool IsRefusal)
{
    public static ToolResult Refused(string toolId, string reason) => new(toolId, reason, true);
    public static ToolResult Ok(string toolId, string payload) => new(toolId, payload, false);
}

/// <summary>
/// Zero is Unknown for the same reason it is on ContentOrigin: an id that fails to
/// resolve must not land on the permissive value. Read is the permissive value here.
/// </summary>
public enum SkillKind
{
    Unknown = 0,
    Read,
    Write,
}

public interface ISkillCatalog
{
    /// <summary>Unknown for an id that is not registered. Never throws — the caller
    /// decides what an unrecognized id means, and here it means refused.</summary>
    SkillKind KindOf(string toolId);
}

public interface IToolDispatcher
{
    Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken ct);
}

/// <summary>Trimmed to the two members this file needs. Full version in Article 2.</summary>
public sealed class TurnState
{
    // Read back by prompt assembly and citation rendering, both elided here.
    private readonly List<RetrievedChunk> _chunks = [];

    public bool HasRetrieved { get; private set; }

    public void AddRetrieved(RetrievedChunk chunk)
    {
        _chunks.Add(chunk);
        HasRetrieved = true;
    }
}

public sealed class DocActionSeparation(IToolDispatcher dispatcher, ISkillCatalog catalog)
{
    public const string WriteBlockedAfterRetrieval =
        "Write skills are unavailable for the remainder of a turn that retrieved content. " +
        "The request was not performed and nothing was proposed.";

    public const string UnknownSkill =
        "That skill is not registered. The request was not performed.";

    /// <summary>
    /// Called wherever retrieval results come back — one path, so there is one
    /// place to get this wrong. No branch on origin: an internal wiki page
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
    ///
    /// The flag is read per call as the loop advances, which is deliberate. A
    /// retrieval earlier in the same batch has already latched by the time a later
    /// call is tested. A write emitted *before* any retrieval in the batch runs,
    /// and should: at that point in the response nothing had come back, so nothing
    /// retrieved could have steered it.
    /// </summary>
    public async Task<IReadOnlyList<ToolResult>> DispatchAsync(
        IReadOnlyList<ToolCall> batch,
        TurnState state,
        CancellationToken ct)
    {
        var results = new List<ToolResult>(batch.Count);

        foreach (var call in batch)
        {
            var kind = catalog.KindOf(call.ToolId);

            if (kind is SkillKind.Unknown)
            {
                results.Add(ToolResult.Refused(call.ToolId, UnknownSkill));
                continue;
            }

            if (state.HasRetrieved && kind is SkillKind.Write)
            {
                results.Add(ToolResult.Refused(call.ToolId, WriteBlockedAfterRetrieval));
                continue;
            }

            results.Add(await dispatcher.InvokeAsync(call, ct));
        }

        return results;
    }
}
