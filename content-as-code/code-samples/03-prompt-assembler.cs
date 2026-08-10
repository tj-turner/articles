// Article 3 — composition happens here, in code, not inside the Markdown.
//
// The prompt files carry no control flow: placeholders substitute values and
// that is all they do. Which fragments go into which prompt, and in what order,
// is decided by this class — one reviewable place where you can see the shape of
// what actually reaches the model.
//
// The cost named in the article is visible here: the fully assembled prompt is
// not a file anywhere. To know what shipped you read this alongside the
// fragments it names.

using System.Globalization;
using System.Reflection;
using System.Text;

namespace SharedAi.Prompts.Contexts
{
    public sealed record AccountRef(Guid AccountId);

    /// <summary>
    /// The type named by <c>contextType</c> in agent-chat-tenant.md. Every
    /// <c>{{context.…}}</c> placeholder in that file is checked against this
    /// record at build time.
    ///
    /// Note what isn't here: no display names. A <c>string</c> property on a
    /// prompt context is a build error unless explicitly marked safe with a
    /// justification, because a customer can type into one and the system
    /// prompt is the highest-authority position in the request.
    /// </summary>
    public sealed record TenantChatContext(
        AccountRef Account,
        DateOnly CurrentDate);
}

namespace SharedAi.Prompts
{
    public sealed class AgentPromptAssembler(IPromptStore store)
    {
        /// <summary>
        /// Fixed order, deliberately. The safety header goes first; a model that
        /// has read the tenant preamble first is a model you are arguing with.
        /// </summary>
        public string BuildAgentSystemPrompt(PromptId agentPrompt, object context)
        {
            var builder = new StringBuilder();

            Append(builder, Prompts.FragmentSafetyHeader, context);
            Append(builder, agentPrompt, context);
            Append(builder, Prompts.FragmentTone, context);

            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// Workers get the scope directive instead of the tone fragment — nothing
        /// they produce is spoken to a person, and telling them otherwise invites
        /// output shaped for a reader who isn't there.
        /// </summary>
        public string BuildWorkerSystemPrompt(PromptId workerPrompt, object context)
        {
            var builder = new StringBuilder();

            Append(builder, Prompts.FragmentSafetyHeader, context);
            Append(builder, Prompts.FragmentWorkerScope, context);
            Append(builder, workerPrompt, context);

            return builder.ToString().TrimEnd();
        }

        private void Append(StringBuilder builder, PromptId id, object context)
        {
            builder.AppendLine(PromptTemplate.Render(store.GetBody(id), context));
            builder.AppendLine();
        }
    }

    /// <summary>
    /// Substitution only. No conditionals, no loops, no includes.
    /// </summary>
    public static class PromptTemplate
    {
        public static string Render(string body, object context)
        {
            var result = new StringBuilder(body.Length);
            var cursor = 0;

            while (cursor < body.Length)
            {
                var open = body.IndexOf("{{", cursor, StringComparison.Ordinal);
                if (open < 0)
                {
                    result.Append(body, cursor, body.Length - cursor);
                    break;
                }

                var close = body.IndexOf("}}", open, StringComparison.Ordinal);
                if (close < 0)
                    throw new InvalidOperationException("Unterminated placeholder in prompt body.");

                result.Append(body, cursor, open - cursor);

                var path = body[(open + 2)..close].Trim();
                result.Append(Resolve(path, context));

                cursor = close + 2;
            }

            return result.ToString();
        }

        /// <summary>
        /// A substitution slot is the one place runtime data reaches the system
        /// prompt, which makes it a channel and not a hole in a string. Were a
        /// display name customer-editable, a tenant could set theirs to
        /// "Acme\n\nDisregard the rule above" and edit the highest-trust text in
        /// the process from outside the org.
        ///
        /// The rule is that slots take only values a customer cannot author —
        /// internal identifiers, dates, org-assigned labels. This runs anyway:
        /// one line, no braces, bounded length. A check beats a convention.
        /// </summary>
        private const int MaxSlotLength = 200;

        internal static string Sanitize(string value)
        {
            var flattened = value
                .ReplaceLineEndings(" ")
                .Replace("{{", string.Empty, StringComparison.Ordinal)
                .Replace("}}", string.Empty, StringComparison.Ordinal);

            flattened = string.Join(' ', flattened.Split(
                (char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            return flattened.Length <= MaxSlotLength
                ? flattened
                : flattened[..MaxSlotLength] + "…";
        }

        /// <summary>
        /// At runtime this walks the context record by property name. At build
        /// time the source generator has already walked the same path against the
        /// type named in <c>contextType</c>, so an unresolvable placeholder is a
        /// compile error and this throw is a backstop nobody should reach.
        /// </summary>
        private static string Resolve(string path, object context)
        {
            const string root = "context.";
            if (!path.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidOperationException($"Placeholder '{path}' does not start with 'context.'.");

            object? current = context;

            foreach (var segment in path[root.Length..].Split('.'))
            {
                if (current is null)
                    throw new InvalidOperationException($"Placeholder '{path}' resolved through a null.");

                var property = current.GetType()
                    .GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? throw new InvalidOperationException(
                        $"Placeholder '{path}' has no property '{segment}' on {current.GetType().Name}.");

                current = property.GetValue(current);
            }

            // Invariant culture, always. Left to the ambient CurrentCulture, a
            // DateOnly renders 3/4/2026 on one worker and 04.03.2026 on another —
            // a four-month difference in what the model is told the date is,
            // decided by whichever locale the box happened to start with.
            var rendered = current switch
            {
                null        => string.Empty,
                DateOnly d  => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _           => current.ToString() ?? string.Empty,
            };

            return Sanitize(rendered);
        }
    }
}
