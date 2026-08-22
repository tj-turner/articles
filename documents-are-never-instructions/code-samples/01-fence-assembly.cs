// Article 4 — "Documents Are Never Instructions: Safety Walls That Don't Ask Who Wrote It"
//
// Fence assembly. Every retrieved chunk gets wrapped, whatever its origin —
// there is no branch here on where the content came from, and that absence is
// the whole point of the file.
//
// Note the record's second member. Article 2 committed this type as
//
//     public sealed record RetrievedChunk(string Text, TrustLevel TrustLevel, string Source);
//
// and the rename to `ContentOrigin` is the change this article is about. It altered
// no behavior. It stopped the type from telling the next reader that some
// content is trusted enough to be obeyed.

namespace SharedAi.Retrieval;

public enum ContentOrigin
{
    // The zero value of a trust-adjacent enum is a decision, not a formality.
    // Without Unknown here, a chunk whose origin failed to bind — a null index
    // field, a deserializer default, a record built with `default` — would
    // silently render as the most-believed value, and internal is the one origin
    // that isn't scanned. Wire() throws on Unknown, so this fails closed.
    Unknown = 0,
    Internal,
    CustomerUploaded,
    PartnerSystem,
}

public sealed record RetrievedChunk(string Text, ContentOrigin ContentOrigin, string Source);

public static class FenceRenderer
{
    public const string CloseMarker = "<<<END-SRC-BLOCK>>>";

    public static string Render(RetrievedChunk chunk) =>
        $"""
        <<<SRC-BLOCK contentOrigin="{Wire(chunk.ContentOrigin)}" source="{SafeName(chunk.Source)}">>>
        {EscapeBody(chunk.Text)}
        {CloseMarker}
        """;

    /// <summary>
    /// Same job as escaping a string on its way into SQL: the content must not be
    /// able to terminate the construct that contains it.
    ///
    /// Without this, a document containing the literal closing token closes its own
    /// fence. One chunk, two closes, and every character after the first one sits
    /// in the position where operator text lives.
    ///
    /// Quotes are deliberately left alone. The body is not inside a quoted
    /// attribute, so flattening its quotes buys nothing and costs verbatim
    /// quotation — which is the entire point of retrieving the document. A memo
    /// reading Re: "final" invoice has to come back out the way it went in.
    ///
    /// The place the SQL analogy breaks is the place worth remembering. There,
    /// escaping is the fallback and a parameterized query removes the mixing
    /// entirely — values travel in a channel the parser will never read as syntax.
    /// There is no parameterized prompt. This is as structural as a delimiter gets,
    /// and a delimiter still depends on the model choosing to respect it.
    /// </summary>
    private static string EscapeBody(string value) => (value ?? string.Empty)
        .Replace("<<<", "‹‹‹")
        .Replace(">>>", "›››");

    /// <summary>
    /// The source name is a different problem and takes a different answer.
    ///
    /// Escaping it is not enough, because it is interpolated into the marker line
    /// itself. A name carrying a newline puts attacker prose on its own line in the
    /// marker region — outside the fenced body, in the position the escape above
    /// exists to protect. A name carrying quotes gets a second contentOrigin
    /// attribute onto the open tag.
    ///
    /// So the name is constrained rather than escaped: an allow-list, capped. This
    /// is the same move the write skills make with their arguments — stop accepting
    /// arbitrary text where a bounded value belongs.
    ///
    /// Be precise about what that buys. The name can no longer break out of its
    /// attribute, add a second one, or reach a new line. What survives is still
    /// text a model can read, sitting in the marker region rather than inside the
    /// fence. That is what the length cap is for, and it is the honest limit of
    /// the technique.
    /// </summary>
    private static string SafeName(string value)
    {
        var name = value ?? string.Empty;
        if (name.Length > 80)
            name = name[..80];

        return string.Create(name.Length, name, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ' '
                    ? c
                    : '_';
            }
        });
    }

    private static string Wire(ContentOrigin origin) => origin switch
    {
        ContentOrigin.Internal => "internal",
        ContentOrigin.CustomerUploaded => "customer-uploaded",
        ContentOrigin.PartnerSystem => "partner-system",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
    };
}
