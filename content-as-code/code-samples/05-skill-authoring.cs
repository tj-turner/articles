// Article 3 — the skill half of the pattern.
//
// A skill is two files. The C# class carries the metadata the loop enforces;
// the companion Markdown (04-list-invoices.md) carries the prose the model
// reads. The split is by who enforces it: prose is for humans to review,
// metadata is for the compiler to hold.
//
// Nothing here asks the model to behave. [SkillWrite] is not advice — the
// orchestrator reads it and routes the call into propose-confirm-execute
// whether or not the model agrees it should.

namespace SharedAi.Skills;

public enum SkillCategory { Read, Write, Analysis }

public enum Classification { Public, Internal, Restricted }

[AttributeUsage(AttributeTargets.Class)]
public sealed class SkillAttribute(string id, SkillCategory category) : Attribute
{
    public string Id { get; } = id;
    public SkillCategory Category { get; } = category;
}

/// <summary>Highest data classification this skill may return.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SkillClassificationAttribute(Classification classification) : Attribute
{
    public Classification Classification { get; } = classification;
}

/// <summary>
/// Marks a mutation. Never dispatched directly — the orchestrator turns it into
/// a proposal that a human confirms on a separate endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SkillWriteAttribute : Attribute;

/// <summary>
/// Whether the result must clear the sanitization gate before it can re-enter
/// the model's context on the next turn.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SkillOutputBlockingAttribute(bool blocking) : Attribute
{
    public bool Blocking { get; } = blocking;
}

/// <summary>
/// Scope the caller must hold. Checked when the manifest is built, so a skill
/// the caller cannot use is never offered to the model in the first place.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SkillScopeRequiredAttribute(string scope) : Attribute
{
    public string Scope { get; } = scope;
}

/// <summary>
/// Per-parameter description. Feeds the generated JSON schema, so argument docs
/// cannot drift away from the signature they describe.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class SkillArgAttribute(string description, bool required = false) : Attribute
{
    public string Description { get; } = description;
    public bool Required { get; } = required;
}

/// <summary>
/// What a skill is allowed to know about the turn it is running in. Note what is
/// absent: no conversation history, and no way to reach the model.
/// </summary>
public sealed record SkillContext(
    Guid ConversationId,
    Guid TurnId,
    Guid TenantId,
    string CallerSubject,
    IReadOnlySet<string> CallerScopes,
    Classification CallerMaxClassification);

public interface ISkill<in TArgs, TResult>
{
    Task<TResult> ExecuteAsync(TArgs args, SkillContext ctx, CancellationToken ct);
}

// --- a real one -------------------------------------------------------------

public sealed record InvoiceSummary(string InvoiceId, string Status, DateOnly Issued, decimal Amount);

public sealed record ListInvoicesResult(IReadOnlyList<InvoiceSummary> Invoices);

public sealed record ListInvoicesArgs(
    [property: SkillArg("Filter by status (open, paid, void).")]
    string? Status,
    [property: SkillArg("Return items issued after this date (ISO-8601).")]
    DateOnly? Since);

public interface IInvoicesClient
{
    Task<IReadOnlyList<InvoiceSummary>> ListAsync(
        Guid tenantId, string? status, DateOnly? since, CancellationToken ct);
}

[Skill(id: "list-invoices", category: SkillCategory.Read)]
[SkillClassification(Classification.Internal)]
[SkillScopeRequired("invoices.read")]
public sealed class ListInvoicesSkill(IInvoicesClient invoices)
    : ISkill<ListInvoicesArgs, ListInvoicesResult>
{
    public async Task<ListInvoicesResult> ExecuteAsync(
        ListInvoicesArgs args,
        SkillContext ctx,
        CancellationToken ct)
    {
        var results = await invoices.ListAsync(ctx.TenantId, args.Status, args.Since, ct);
        return new ListInvoicesResult(results);
    }
}
