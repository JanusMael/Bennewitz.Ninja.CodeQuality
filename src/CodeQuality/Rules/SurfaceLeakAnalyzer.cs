using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bennewitz.Ninja.CodeQuality.Rules;

/// <summary>
/// BNCQ1002: no type from a leak-prone namespace appears in the visible surface, directly or
/// through the members of a referenced type that does.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>A leaked implementation type is a dependency the consumer did not choose.</b> Return a
/// <c>JsonNode</c> from one public method and every consumer now binds against
/// <c>System.Text.Json</c>, its version, its behaviour and its breaking changes, whether or not
/// they serialize anything. Swapping the serializer later is then a breaking change to an API that
/// was never about serialization.
/// </para>
/// <para>
/// ⭐ <b>The walk is transitive through referenced types, and stops at your own.</b>
/// <c>BNAQ1002</c> reads signature types. Here a signature type from another assembly is opened
/// as well: a public <c>Envelope</c> whose <c>Payload</c> is a <c>JObject</c> binds the consumer to
/// Newtonsoft just as surely, one hop further. A type declared in this compilation is not opened,
/// because its own visible members are examined where they are declared and would otherwise be
/// reported twice. Generic arguments, the arguments of a containing type, array elements and
/// constraints are always read: a <c>Task&lt;JsonNode&gt;</c>, a <c>List&lt;JsonNode&gt;.Enumerator</c>
/// and a <c>where T : JsonNode</c> leak exactly as much as a bare <c>JsonNode</c>. Found by review:
/// the first version read neither containing types' arguments nor constraints.
/// </para>
/// <para>
/// ⭐ <b>A covered type the signature names directly is reported before any path.</b> A member
/// taking both a vendor envelope and a <c>JsonArray</c> is reported for the <c>JsonArray</c>, the
/// plainest explanation, even when the envelope comes first and reaches one too.
/// </para>
/// <para>
/// ⚠ <b>The receiver of a C# 14 <c>extension(...)</c> block is not read.</b> The analyzer is
/// compiled against Roslyn 4.14, which has no API for it; the members inside the block are
/// examined as usual, and a classic <c>this JsonNode</c> extension method is reported. Measured by
/// review on SDK 10.0.401.
/// </para>
/// <para>
/// ⛔ <b>The walk opens generic DEFINITIONS, breadth first, without recursion.</b> Measured: a first
/// version opened constructed types recursively, and <c>Nest&lt;T&gt;</c> with a member returning
/// <c>Nest&lt;Nest&lt;T&gt;&gt;</c> names infinitely many of them; it recursed 2,144 levels and
/// killed the process with a stack overflow, which in a consumer's build is the compiler. A
/// definition's members name its type parameters, never a covered type, so nothing is lost: a
/// covered type argument is caught where the constructed type is named. Breadth first also makes
/// the reported path the shortest one.
/// </para>
/// <para>
/// ⛔ <b>Only a complete answer is remembered.</b> A search that finds nothing has explored the
/// whole closure of its start, so every type it saw is clean and is remembered as clean. A search
/// that finds a leak remembers only its start. Measured: the first version remembered "clean" for
/// a type whose only way out ran back through a type still being examined, and a cycle then
/// reported one of its two types.
/// </para>
/// <para>
/// ⚠ <b>A referenced type is opened only if its assembly can reach a covered namespace</b>:
/// declares one, or references, directly or transitively, an assembly that does. Measured: a first
/// version asked only about direct references, and a package exposing another package's
/// <c>JsonNode</c> went unreported. The reach is computed once per compilation from the references'
/// metadata, and it keeps the walk off <c>Task&lt;T&gt;</c>, <c>List&lt;T&gt;</c> and most of the
/// framework.
/// </para>
/// <para>
/// ⚠ <b>A type from the covered package itself can report the type it reaches.</b> Exposing
/// <c>Newtonsoft.Json.JsonSerializer</c> binds the consumer to the package that
/// <c>Newtonsoft.Json.Linq</c> ships in, and if its surface reaches a <c>JToken</c> the rule says
/// so. That is the rule working, not a false positive: the consumer is bound either way.
/// </para>
/// <para>
/// ⚠ <b>The default set is curated, not exhaustive</b>: the namespaces that leak in practice.
/// <c>bennewitz_ninja_codequality.BNCQ1002.namespaces</c> in <c>.editorconfig</c> REPLACES it with
/// the consumer's own list, separated by commas or semicolons, because the type most likely to
/// leak from a given library is one no general list will name. Read once per compilation, from a
/// global config first and then from the first source file whose options carry it, so it belongs
/// in the repository's root <c>.editorconfig</c>. A prefix covers the namespace and everything
/// beneath it, on a segment boundary: <c>System.Text.Json</c> covers <c>System.Text.Json.Nodes</c>
/// and not <c>System.Text.JsonX</c>.
/// </para>
/// <para>
/// ⛔ <b>Inert where nothing covered is in reach.</b> A signature can name a type only from the
/// compilation itself or from a reference. When no covered namespace exists in either, the
/// predicate cannot be satisfied and nothing is registered, rather than every member being walked
/// past for a result that was never possible.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SurfaceLeakAnalyzer : ScopedAnalyzer<SurfaceLeakAnalyzer.Scope>
{
    /// <summary>The rule's ID. Public API: a consumer's suppressions name it.</summary>
    public const string Id = "BNCQ1002";

    /// <summary>The <c>.editorconfig</c> key that replaces the default namespace list.</summary>
    public const string NamespacesOption = "bennewitz_ninja_codequality.BNCQ1002.namespaces";

    /// <summary>Namespaces whose types are classic accidental exports.</summary>
    public static readonly ImmutableArray<string> LeakProneNamespaces = ImmutableArray.Create(
        "System.Text.Json.Nodes",
        "Newtonsoft.Json.Linq");

    private static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "A type from a leak-prone namespace appears in the visible surface",
        messageFormat: "'{0}' exposes {1}, so every consumer binds against that package whether they use it or not, and replacing it later becomes a breaking change to an API that was never about it. Expose your own type instead.",
        Catalog.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A visible member whose signature names a type from a leak-prone namespace, or a referenced type whose visible members do, makes every consumer depend on that package. The default namespaces are System.Text.Json.Nodes and Newtonsoft.Json.Linq; bennewitz_ninja_codequality.BNCQ1002.namespaces in .editorconfig replaces the list.",
        helpLinkUri: Catalog.HelpLink(Id));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    protected override Scope? EnterScope(Compilation compilation, AnalyzerOptions options, CancellationToken cancellationToken) =>
        Scope.Of(compilation, Namespaces(compilation, options), cancellationToken);

    /// <inheritdoc />
    protected override void Register(CompilationStartAnalysisContext context, Scope scope) =>
        context.RegisterSymbolAction(symbolContext => Check(symbolContext, scope), SymbolKind.NamedType);

    /// <summary>The configured list where a consumer set one, else the default.</summary>
    private static ImmutableArray<string> Namespaces(Compilation compilation, AnalyzerOptions options)
    {
        AnalyzerConfigOptionsProvider provider = options.AnalyzerConfigOptionsProvider;

        if (provider.GlobalOptions.TryGetValue(NamespacesOption, out string? configured))
        {
            return Parse(configured);
        }

        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            if (provider.GetOptions(tree).TryGetValue(NamespacesOption, out configured))
            {
                return Parse(configured);
            }
        }

        return LeakProneNamespaces;
    }

    private static ImmutableArray<string> Parse(string configured)
    {
        ImmutableArray<string>.Builder namespaces = ImmutableArray.CreateBuilder<string>();

        foreach (string item in configured.Split(',', ';'))
        {
            string trimmed = item.Trim();
            if (trimmed.Length > 0 && !namespaces.Contains(trimmed))
            {
                namespaces.Add(trimmed);
            }
        }

        return namespaces.ToImmutable();
    }

    private static void Check(SymbolAnalysisContext context, Scope scope)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!Surface.IsVisibleOutside(type))
        {
            return;
        }

        // The type's own surface: what it derives from and implements, and its constraints.
        if (scope.FindIn([.. Inherited(type), .. Constraints(type.TypeParameters)], context.CancellationToken) is { } own)
        {
            Report(context, type, type, own);
        }

        foreach (ISymbol member in type.GetMembers())
        {
            if (Surface.IsVisibleOutside(member)
                && scope.FindIn([.. SignatureTypes(member)], context.CancellationToken) is { } leak)
            {
                Report(context, member, type, leak);
            }
        }
    }

    private static void Report(SymbolAnalysisContext context, ISymbol member, INamedTypeSymbol type, Leak leak)
    {
        Location location = member.Locations.Length > 0 ? member.Locations[0] : type.Locations[0];
        string offender = "the type '" + leak.Offender.ToDisplayString() + "'";
        if (leak.Path.Length > 0)
        {
            offender += " through '" + leak.Path + "'";
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, member.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), offender));
    }

    private static IEnumerable<ITypeSymbol> Inherited(INamedTypeSymbol type)
    {
        if (type.BaseType is { } baseType)
        {
            yield return baseType;
        }

        foreach (INamedTypeSymbol implemented in type.Interfaces)
        {
            yield return implemented;
        }
    }

    /// <summary>
    /// What type parameters are constrained to. A caller has to satisfy a constraint, so a covered
    /// type named there binds the consumer as surely as a parameter type does.
    /// </summary>
    private static IEnumerable<ITypeSymbol> Constraints(ImmutableArray<ITypeParameterSymbol> typeParameters)
    {
        foreach (ITypeParameterSymbol typeParameter in typeParameters)
        {
            foreach (ITypeSymbol constraint in typeParameter.ConstraintTypes)
            {
                yield return constraint;
            }
        }
    }

    /// <summary>
    /// The types a member's signature names, constraints included. Accessors are covered by their
    /// property or event, nested types by their own visit, and compiler-generated members by
    /// nobody, except a delegate's <c>Invoke</c>, which is the delegate's whole signature.
    /// </summary>
    private static IEnumerable<ITypeSymbol> SignatureTypes(ISymbol member)
    {
        switch (member)
        {
            case IMethodSymbol method:
                bool signature = method.MethodKind == MethodKind.DelegateInvoke
                    || (!method.IsImplicitlyDeclared && method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator or MethodKind.Conversion);
                if (!signature)
                {
                    yield break;
                }

                yield return method.ReturnType;
                foreach (IParameterSymbol parameter in method.Parameters)
                {
                    yield return parameter.Type;
                }

                foreach (ITypeSymbol constraint in Constraints(method.TypeParameters))
                {
                    yield return constraint;
                }

                break;
            case IPropertySymbol property when !property.IsImplicitlyDeclared:
                yield return property.Type;
                foreach (IParameterSymbol parameter in property.Parameters)
                {
                    yield return parameter.Type;
                }

                break;
            case IFieldSymbol field when !field.IsImplicitlyDeclared:
                yield return field.Type;
                break;
            case IEventSymbol @event when !@event.IsImplicitlyDeclared:
                yield return @event.Type;
                break;
        }
    }

    /// <summary>
    /// The named types a type is made of: itself, its element, its type arguments and those of the
    /// types it is nested in, however deep.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> Unwrap(ITypeSymbol type)
    {
        Stack<ITypeSymbol> pending = new();
        pending.Push(type);

        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case INamedTypeSymbol named:
                    yield return named;

                    // ⛔ A nested type of a constructed generic carries its container's arguments:
                    // List<JsonNode>.Enumerator names JsonNode. Only the arguments are pushed, never
                    // the container itself, which would open the container's members too.
                    for (INamedTypeSymbol? outer = named.ContainingType; outer is not null; outer = outer.ContainingType)
                    {
                        for (int i = outer.TypeArguments.Length - 1; i >= 0; i--)
                        {
                            pending.Push(outer.TypeArguments[i]);
                        }
                    }

                    for (int i = named.TypeArguments.Length - 1; i >= 0; i--)
                    {
                        pending.Push(named.TypeArguments[i]);
                    }

                    break;
                case IArrayTypeSymbol array:
                    pending.Push(array.ElementType);
                    break;
                case IPointerTypeSymbol pointer:
                    pending.Push(pointer.PointedAtType);
                    break;
            }
        }
    }

    /// <summary>A covered type, and the hops it was reached through, if any.</summary>
    internal sealed class Leak
    {
        public Leak(INamedTypeSymbol offender, string path)
        {
            Offender = offender;
            Path = path;
        }

        public INamedTypeSymbol Offender { get; }

        /// <summary>
        /// The hops from the signature type to the offender, each <c>Type.Member</c>, comma
        /// separated; empty when the signature names the offender itself.
        /// </summary>
        public string Path { get; }
    }

    /// <summary>
    /// The covered namespaces as this compilation can reach them, and the walk that finds them.
    /// </summary>
    internal sealed class Scope
    {
        private readonly IAssemblySymbol _self;
        private readonly ImmutableArray<string> _namespaces;
        private readonly HashSet<string> _reaching;

        /// <summary>
        /// The complete answer for a generic definition or plain type: its leak, or null where its
        /// whole closure is clean.
        /// </summary>
        private readonly ConcurrentDictionary<INamedTypeSymbol, Leak?> _answers = new(SymbolEqualityComparer.Default);

        private Scope(IAssemblySymbol self, ImmutableArray<string> namespaces, HashSet<string> reaching)
        {
            _self = self;
            _namespaces = namespaces;
            _reaching = reaching;
        }

        /// <summary>
        /// The scope for this compilation, or null where no covered namespace is in reach.
        /// </summary>
        public static Scope? Of(Compilation compilation, ImmutableArray<string> namespaces, CancellationToken cancellationToken)
        {
            // ⚠ Every prefix is resolved in every assembly, never short-circuited: each assembly
            // declaring a covered namespace has to be known, because that set seeds which referenced
            // types are worth opening.
            //
            // ⛔ Each assembly's OWN global namespace, not the compilation's merged one. A reference
            // behind an extern alias is left out of the merge, yet a signature can still name its
            // types as Alias::Namespace.Type. Found as the twin of BNCQ1004's alias defect: the
            // merged namespace left this rule inert for a covered package referenced that way.
            List<IAssemblySymbol> assemblies = [compilation.Assembly];
            foreach (MetadataReference reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    assemblies.Add(assembly);
                }
            }

            HashSet<string> declaring = new(StringComparer.OrdinalIgnoreCase);

            foreach (string prefix in namespaces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (IAssemblySymbol assembly in assemblies)
                {
                    if (Resolve(assembly.GlobalNamespace, prefix) is not null)
                    {
                        declaring.Add(assembly.Name);
                    }
                }
            }

            return declaring.Count == 0
                ? null
                : new Scope(compilation.Assembly, namespaces, Reaching(compilation, declaring, cancellationToken));
        }

        /// <summary>
        /// The covered type a signature names directly, if any; otherwise the first that one of its
        /// types reaches. The plainest explanation wins over the first one found.
        /// </summary>
        public Leak? FindIn(ITypeSymbol[] types, CancellationToken cancellationToken)
        {
            foreach (ITypeSymbol type in types)
            {
                foreach (INamedTypeSymbol candidate in Unwrap(type))
                {
                    if (candidate.TypeKind != TypeKind.Error && Covered(candidate))
                    {
                        return new Leak(candidate, string.Empty);
                    }
                }
            }

            foreach (ITypeSymbol type in types)
            {
                if (Find(type, cancellationToken) is { } leak)
                {
                    return leak;
                }
            }

            return null;
        }

        /// <summary>The covered type <paramref name="type"/> names or reaches, if any.</summary>
        private Leak? Find(ITypeSymbol type, CancellationToken cancellationToken)
        {
            foreach (INamedTypeSymbol candidate in Unwrap(type))
            {
                if (candidate.TypeKind == TypeKind.Error)
                {
                    continue;
                }

                if (Covered(candidate))
                {
                    return new Leak(candidate, string.Empty);
                }

                if (Openable(candidate) && Search(candidate.OriginalDefinition, cancellationToken) is { } leak)
                {
                    return leak;
                }
            }

            return null;
        }

        /// <summary>
        /// Breadth first from <paramref name="start"/> through the visible members and the bases of
        /// every openable type it reaches, to the first covered type.
        /// </summary>
        private Leak? Search(INamedTypeSymbol start, CancellationToken cancellationToken)
        {
            if (_answers.TryGetValue(start, out Leak? known))
            {
                return known;
            }

            Dictionary<INamedTypeSymbol, (INamedTypeSymbol? From, string Hop)> seen = new(SymbolEqualityComparer.Default)
            {
                [start] = (null, string.Empty),
            };
            Queue<INamedTypeSymbol> queue = new();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                INamedTypeSymbol current = queue.Dequeue();

                foreach ((string hop, ITypeSymbol used) in Edges(current))
                {
                    foreach (INamedTypeSymbol candidate in Unwrap(used))
                    {
                        if (candidate.TypeKind == TypeKind.Error)
                        {
                            continue;
                        }

                        if (Covered(candidate))
                        {
                            Leak leak = new(candidate, PathTo(seen, current, hop));
                            _answers.TryAdd(start, leak);
                            return leak;
                        }

                        INamedTypeSymbol next = candidate.OriginalDefinition;

                        // ⚠ A type known to be clean is skipped, and so is everything it reaches.
                        // A type known to LEAK is searched again rather than spliced in: its
                        // remembered path may not be the shortest from here, and a message that
                        // depended on which search ran first would differ from build to build.
                        if (!Openable(next) || seen.ContainsKey(next)
                            || (_answers.TryGetValue(next, out Leak? answer) && answer is null))
                        {
                            continue;
                        }

                        seen.Add(next, (current, hop));
                        queue.Enqueue(next);
                    }
                }
            }

            // Nothing covered anywhere in the closure, so every type in it is clean.
            foreach (INamedTypeSymbol clean in seen.Keys)
            {
                _answers.TryAdd(clean, null);
            }

            return null;
        }

        /// <summary>
        /// What a type's surface names: each visible member's signature, then its bases, then its
        /// constraints.
        /// </summary>
        private static IEnumerable<(string Hop, ITypeSymbol Used)> Edges(INamedTypeSymbol type)
        {
            foreach (ISymbol member in type.GetMembers())
            {
                if (!Surface.IsVisibleOutside(member))
                {
                    continue;
                }

                string hop = member is IMethodSymbol { MethodKind: MethodKind.Constructor }
                    ? type.Name + " constructor"
                    : type.Name + "." + member.Name;

                foreach (ITypeSymbol used in SignatureTypes(member))
                {
                    yield return (hop, used);
                }
            }

            foreach (ITypeSymbol inherited in Inherited(type))
            {
                yield return (type.Name + " base", inherited);
            }

            foreach (ITypeSymbol constraint in Constraints(type.TypeParameters))
            {
                yield return (type.Name + " constraint", constraint);
            }
        }

        private static string PathTo(Dictionary<INamedTypeSymbol, (INamedTypeSymbol? From, string Hop)> seen, INamedTypeSymbol current, string hop)
        {
            List<string> hops = [hop];
            for (INamedTypeSymbol? at = current; at is not null && seen[at].From is { } from; at = from)
            {
                hops.Add(seen[at].Hop);
            }

            hops.Reverse();
            return string.Join(", ", hops);
        }

        /// <summary>
        /// Whether a type is worth opening: declared in another assembly, one that can reach a
        /// covered namespace. This compilation's own types report their members where declared.
        /// </summary>
        private bool Openable(INamedTypeSymbol type) =>
            type.ContainingAssembly is { } assembly
            && !SymbolEqualityComparer.Default.Equals(assembly, _self)
            && _reaching.Contains(assembly.Name);

        private bool Covered(INamedTypeSymbol type)
        {
            if (type.ContainingNamespace is not { IsGlobalNamespace: false } ns)
            {
                return false;
            }

            string name = ns.ToDisplayString();
            foreach (string prefix in _namespaces)
            {
                if (name.Equals(prefix, StringComparison.Ordinal) || name.StartsWith(prefix + ".", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The referenced assemblies that declare a covered namespace or reference, however
        /// indirectly, one that does: a fixed point over what each one's metadata references.
        /// </summary>
        private static HashSet<string> Reaching(Compilation compilation, HashSet<string> declaring, CancellationToken cancellationToken)
        {
            List<(string Name, string[] References)> assemblies = [];
            foreach (MetadataReference reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    assemblies.Add((assembly.Name, [.. assembly.Modules.SelectMany(module => module.ReferencedAssemblies).Select(identity => identity.Name)]));
                }
            }

            HashSet<string> reaching = new(declaring, StringComparer.OrdinalIgnoreCase);
            bool grew = true;
            while (grew)
            {
                cancellationToken.ThrowIfCancellationRequested();
                grew = false;

                foreach ((string name, string[] references) in assemblies)
                {
                    if (!reaching.Contains(name) && references.Any(reaching.Contains))
                    {
                        reaching.Add(name);
                        grew = true;
                    }
                }
            }

            return reaching;
        }

        /// <summary>The namespace a dotted prefix names, as the compilation sees it, or null.</summary>
        private static INamespaceSymbol? Resolve(INamespaceSymbol root, string prefix)
        {
            INamespaceSymbol current = root;
            foreach (string segment in prefix.Split('.'))
            {
                INamespaceSymbol? next = null;
                foreach (INamespaceSymbol child in current.GetNamespaceMembers())
                {
                    if (child.Name == segment)
                    {
                        next = child;
                        break;
                    }
                }

                if (next is null)
                {
                    return null;
                }

                current = next;
            }

            return current;
        }
    }
}
