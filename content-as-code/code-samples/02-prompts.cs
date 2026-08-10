// Article 3 — "Content-as-Code for AI: Prompts and Skills You'd Actually Review"
//
// The prompt side of the pattern: Markdown files embedded as resources, a
// frontmatter header describing each one, and strongly-typed identifiers so a
// missing prompt is a compile error rather than an empty system prompt.
//
// PromptId and the context binding are shown here as hand-written stand-ins for
// what a source generator emits at build time. The generator is what makes the
// two guarantees in the article real: an unknown prompt id doesn't compile, and
// a placeholder that doesn't exist on the bound context type doesn't compile.

using System.Reflection;

namespace SharedAi.Prompts;

/// <summary>
/// Identifies one embedded prompt resource. Emitted per prompt by the source
/// generator; the constructor is internal so callers cannot invent an id that
/// has no file behind it.
/// </summary>
public readonly record struct PromptId
{
    internal PromptId(string value) => Value = value;

    public string Value { get; }

    // PromptId is a struct, so default(PromptId) is reachable from anywhere and
    // carries a null Value however internal the constructor is. The generator
    // never emits one, but the type must not promise what it can't hold.
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// One member per prompt file found in the assembly at build time. A rename of
/// the underlying <c>.md</c> file breaks every call site, which is the point.
/// </summary>
public static class Prompts
{
    public static PromptId AgentChatTenant { get; } = new("agent-chat-tenant");
    public static PromptId WorkerDigest { get; } = new("worker-digest");

    // Fragments composed into the prompts above.
    public static PromptId FragmentSafetyHeader { get; } = new("fragment-safety-header");
    public static PromptId FragmentTone { get; } = new("fragment-tone");
    public static PromptId FragmentWorkerScope { get; } = new("fragment-worker-scope");
}

public enum PromptKind
{
    AgentSystem,
    WorkerSystem,
    Fragment,
    SkillDescription,
}

/// <summary>
/// The YAML frontmatter of a prompt file, parsed once at startup.
/// </summary>
public sealed record PromptManifestEntry(
    PromptId Id,
    PromptKind Kind,
    string? BoundTo,
    bool SafetyCritical,
    Type? ContextType,
    string ResourceName)
{
    /// <summary>
    /// Only meaningful on <see cref="PromptKind.SkillDescription"/> entries,
    /// where it must agree with the attribute on the paired class. The startup
    /// check is what makes "must agree" true.
    /// </summary>
    public string? Classification { get; init; }

    /// <summary>Authored version of the prose, recorded against each turn.</summary>
    public string? Version { get; init; }
}

public interface IPromptStore
{
    /// <summary>Raw body of a prompt, frontmatter stripped.</summary>
    string GetBody(PromptId id);

    IReadOnlyList<PromptManifestEntry> Entries { get; }
}

/// <summary>
/// Reads the embedded resources once and holds them. There is no reload path
/// and no setter — the only way to change a prompt is to ship a new build.
/// </summary>
public sealed class EmbeddedPromptStore : IPromptStore
{
    private readonly Dictionary<string, string> _bodies;

    public EmbeddedPromptStore(Assembly promptAssembly)
    {
        var entries = new List<PromptManifestEntry>();
        _bodies = [];

        foreach (var resourceName in promptAssembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".md", StringComparison.Ordinal))
                continue;

            using var stream = promptAssembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            var raw = reader.ReadToEnd();

            var (frontmatter, body) = SplitFrontmatter(raw);
            var entry = PromptFrontmatter.Parse(frontmatter, resourceName);

            // Two files claiming one id is the drift this whole pattern exists to
            // prevent. Letting enumeration order decide the winner would be the
            // silent failure the article complains about, committed by the loader.
            if (!_bodies.TryAdd(entry.Id.Value, body))
                throw new InvalidOperationException(
                    $"Duplicate prompt id '{entry.Id}' in '{resourceName}'.");

            entries.Add(entry);
        }

        Entries = entries;
    }

    public IReadOnlyList<PromptManifestEntry> Entries { get; }

    public string GetBody(PromptId id) =>
        _bodies.TryGetValue(id.Value, out var body)
            ? body
            // Unreachable when the generator is doing its job: every PromptId it
            // emits came from a file in this assembly. Throwing rather than
            // returning "" matters anyway — an empty system prompt is an
            // assistant with no rules, and it does not look like a failure.
            : throw new InvalidOperationException($"No embedded prompt for id '{id}'.");

    private static (string Frontmatter, string Body) SplitFrontmatter(string raw)
    {
        const string fence = "---";
        var normalized = raw.Replace("\r\n", "\n");

        if (!normalized.StartsWith(fence + "\n", StringComparison.Ordinal))
            throw new InvalidOperationException("Prompt file is missing its frontmatter block.");

        var end = normalized.IndexOf("\n" + fence, fence.Length, StringComparison.Ordinal);
        if (end < 0)
            throw new InvalidOperationException("Prompt frontmatter block is not terminated.");

        var frontmatter = normalized[(fence.Length + 1)..end];
        var body = normalized[(end + fence.Length + 1)..].TrimStart('\n');
        return (frontmatter, body);
    }
}

/// <summary>
/// Deliberately small: a closed set of scalar fields and no expressions.
/// Anything that needs logic belongs in the assembler, in C#, under review.
/// </summary>
internal static class PromptFrontmatter
{
    private static readonly HashSet<string> KnownFields = new(StringComparer.Ordinal)
    {
        "id", "kind", "boundTo", "safetyCritical", "contextType", "classification",
        // Bumped by hand when the prose changes meaningfully. It doesn't drive
        // anything at runtime — it exists so a turn record can say which text
        // produced it, and so a review can ask what 3.0.0 fixed about 2.0.0.
        "version",
        // Optional. Present, it pins the model and the pin is reviewed with the
        // prose. Absent — which is the common case — the orchestrator picks
        // whichever configured model fits the request.
        "model",
    };

    public static PromptManifestEntry Parse(string frontmatter, string resourceName)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in frontmatter.Split('\n'))
        {
            if (line.Trim().Length == 0) continue;

            var separator = line.IndexOf(':');
            if (separator <= 0)
                throw new InvalidOperationException($"'{resourceName}' has a malformed frontmatter line: {line}");

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            // A closed set, checked. Skipping unknown keys means `safteyCritical:
            // true` parses to false and the adversarial suite silently never runs
            // — the flag would exist only to make you feel it was covered.
            if (!KnownFields.Contains(key))
                throw new InvalidOperationException(
                    $"'{resourceName}' has an unrecognised frontmatter key '{key}'.");

            fields[key] = value;
        }

        if (!fields.TryGetValue("id", out var id))
            throw new InvalidOperationException($"'{resourceName}' has no id in its frontmatter.");

        if (!fields.TryGetValue("kind", out var kindText)
            || !TryParseKind(kindText, out var kind))
            throw new InvalidOperationException($"'{resourceName}' has no valid kind in its frontmatter.");

        fields.TryGetValue("boundTo", out var boundTo);
        fields.TryGetValue("contextType", out var contextTypeName);

        var safetyCritical =
            fields.TryGetValue("safetyCritical", out var flag)
            && bool.TryParse(flag, out var parsed)
            && parsed;

        fields.TryGetValue("classification", out var classification);

        // Type.GetType only searches the calling assembly and corelib, so a
        // context record living anywhere else resolves to null — as does a typo.
        // Both would disarm the placeholder check while looking fine.
        Type? contextType = null;
        if (contextTypeName is { Length: > 0 })
        {
            contextType = Type.GetType(contextTypeName, throwOnError: false)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(contextTypeName, throwOnError: false))
                    .FirstOrDefault(t => t is not null)
                ?? throw new InvalidOperationException(
                    $"'{resourceName}' names contextType '{contextTypeName}', which does not resolve.");
        }

        return new PromptManifestEntry(
            new PromptId(id),
            kind,
            boundTo,
            safetyCritical,
            contextType,
            resourceName)
        {
            Classification = classification,
            Version = fields.GetValueOrDefault("version"),
        };
    }

    // Enum.TryParse happily accepts "3", so a frontmatter kind of 3 would parse
    // to SkillDescription. An explicit map is the only version that says no.
    private static bool TryParseKind(string text, out PromptKind kind)
    {
        (var found, kind) = text switch
        {
            "agent-system"      => (true, PromptKind.AgentSystem),
            "worker-system"     => (true, PromptKind.WorkerSystem),
            "fragment"          => (true, PromptKind.Fragment),
            "skill-description" => (true, PromptKind.SkillDescription),
            _                   => (false, default),
        };
        return found;
    }
}
