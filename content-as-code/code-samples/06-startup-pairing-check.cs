// Article 3 — the check that refuses to boot.
//
// A skill is two halves in two places, so drift is a matter of time. This runs
// once at startup, pairs every decorated class with its description file, and
// throws if it can't. The service does not come up.
//
// A unit test catches the same three problems earlier and more cheaply, and you
// should have one. This is the copy nobody can merge around.

using System.Reflection;
using SharedAi.Prompts;
using SharedAi.Skills;

namespace SharedAi.Skills.Registry;

public sealed record SkillRegistration(
    string Id,
    Type ImplementationType,
    Classification Classification,
    bool IsWrite,
    IReadOnlyList<string> RequiredScopes,
    string Description);

public sealed class SkillPairingException(IReadOnlyList<string> problems)
    : Exception(BuildMessage(problems))
{
    public IReadOnlyList<string> Problems { get; } = problems;

    private static string BuildMessage(IReadOnlyList<string> problems) =>
        $"Skill registry failed validation ({problems.Count} problem(s)):"
        + Environment.NewLine
        + string.Join(Environment.NewLine, problems.Select(p => "  - " + p));
}

public static class SkillRegistryBuilder
{
    /// <summary>
    /// Pairs decorated classes against skill-description prompts. Every problem
    /// is collected before throwing — a startup failure that reports one missing
    /// file at a time turns a five-minute fix into five deploys.
    /// </summary>
    public static IReadOnlyList<SkillRegistration> Build(
        IEnumerable<Assembly> skillAssemblies,
        IPromptStore prompts)
    {
        var problems = new List<string>();

        // Group before indexing. Copy-pasting a skill class and forgetting to
        // change the id is the likeliest drift of all, and ToDictionary would
        // throw on it — outside the collector, naming neither class.
        var classGroups = skillAssemblies
            .SelectMany(SafeGetTypes)
            .Select(t => (Type: t, Skill: t.GetCustomAttribute<SkillAttribute>()))
            .Where(x => x.Skill is not null)
            .GroupBy(x => x.Skill!.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var group in classGroups.Where(g => g.Count() > 1))
            problems.Add(
                $"Skill id '{group.Key}' is declared by more than one class: "
                + string.Join(", ", group.Select(x => x.Type.Name)));

        var classes = classGroups.ToDictionary(g => g.Key, g => g.First().Type, StringComparer.Ordinal);

        var descriptionGroups = prompts.Entries
            .Where(e => e.Kind == PromptKind.SkillDescription)
            .GroupBy(e => e.BoundTo ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        foreach (var group in descriptionGroups.Where(g => g.Count() > 1 && g.Key.Length > 0))
            problems.Add(
                $"Skill id '{group.Key}' is described by more than one file: "
                + string.Join(", ", group.Select(e => e.ResourceName)));

        var descriptions = descriptionGroups.ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // 1. A decorated class with no description file. The model would be
        //    offered a tool with nothing to tell it what the tool is for.
        foreach (var (id, type) in classes)
        {
            if (!descriptions.ContainsKey(id))
                problems.Add($"Skill '{id}' ({type.Name}) has no description file with boundTo: {id}.");
        }

        // 2. A description file with no class. The manifest would advertise a
        //    capability that cannot run.
        foreach (var (boundTo, entry) in descriptions)
        {
            if (boundTo.Length == 0)
                problems.Add($"Skill description '{entry.Id}' has no boundTo in its frontmatter.");
            else if (!classes.ContainsKey(boundTo))
                problems.Add($"Skill description '{entry.Id}' is bound to '{boundTo}', which no class declares.");
        }

        // 3. The two halves disagreeing about classification. This is the one
        //    that would otherwise ship quietly: both files exist, both look
        //    fine on their own, and the system holds two different beliefs
        //    about how sensitive the results are.
        var registrations = new List<SkillRegistration>();

        foreach (var (id, type) in classes)
        {
            if (!descriptions.TryGetValue(id, out var entry))
                continue;   // already reported above

            var declared = type.GetCustomAttribute<SkillClassificationAttribute>()?.Classification
                           ?? Classification.Internal;

            if (entry.Classification is { Length: > 0 } text)
            {
                if (!Enum.TryParse<Classification>(text, ignoreCase: true, out var documented))
                    problems.Add($"Skill '{id}' description declares unknown classification '{text}'.");
                else if (documented != declared)
                    problems.Add(
                        $"Skill '{id}' classification mismatch: attribute says {declared}, "
                        + $"description says {documented}.");
            }

            // 4. The write bit declared twice, disagreeably. [SkillWrite] is what
            //    the orchestrator routes on; the category is what the manifest
            //    groups by. A skill that is one and not the other is the article's
            //    stated enemy sitting inside its own attribute set.
            var isWrite = type.GetCustomAttribute<SkillWriteAttribute>() is not null;
            var category = type.GetCustomAttribute<SkillAttribute>()!.Category;

            if (isWrite != (category == SkillCategory.Write))
                problems.Add(
                    $"Skill '{id}' disagrees with itself about writing: category is {category}, "
                    + $"[SkillWrite] is {(isWrite ? "present" : "absent")}.");

            registrations.Add(new SkillRegistration(
                Id: id,
                ImplementationType: type,
                Classification: declared,
                IsWrite: isWrite,
                RequiredScopes: type.GetCustomAttributes<SkillScopeRequiredAttribute>()
                                    .Select(a => a.Scope)
                                    .ToList(),
                Description: prompts.GetBody(entry.Id)));
        }

        if (problems.Count > 0)
            throw new SkillPairingException(problems);

        return registrations;
    }

    /// <summary>
    /// A partially-loadable assembly throws from GetTypes, which at startup is an
    /// unhelpful crash in place of the specific diagnostic this class exists for.
    /// </summary>
    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}

// Where this runs matters as much as what it checks. Resolved lazily, it fires on
// the first request: the container comes up green, the health probe passes, the
// deploy proceeds, and one unlucky user gets the 500. Called before the host
// starts, a failure fails the revision and the previous one keeps serving.
//
//   var registry = SkillRegistryBuilder.Build(assemblies, promptStore);
//   builder.Services.AddSingleton<IReadOnlyList<SkillRegistration>>(registry);
//   // …
//   app.Run();
