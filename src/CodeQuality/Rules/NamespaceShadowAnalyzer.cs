using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bennewitz.Ninja.CodeQuality.Rules;

/// <summary>
/// BNCQ1004: no declared namespace carries a segment that shadows the root namespace of a
/// referenced assembly.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>C# resolves the first identifier of a qualified name by walking outward.</b> Inside
/// <c>Acme.Widgets.Avalonia</c>, the name <c>Avalonia</c> finds YOUR namespace first, and
/// <c>Avalonia.Media.Color</c> fails to compile looking for <c>Acme.Widgets.Avalonia.Media.Color</c>:
/// CS0234, reported at the use site, about a type that obviously exists. The fix at the use site is
/// <c>global::</c>, which is a scar rather than a solution.
/// </para>
/// <para>
/// ⛔ <b>Only segments after the first.</b> A namespace's own root is the tree it already lives in:
/// <c>Acme.Widgets</c> beside a referenced <c>Acme.Other</c> resolves perfectly, because the merged
/// <c>Acme</c> holds both. Flagging the first segment would fire on every library that shares a
/// vendor prefix with its own dependencies. A nested declaration's segments all count, since the
/// enclosing declaration already supplied the first.
/// </para>
/// <para>
/// ⭐ <b>A root is a top-level namespace a referenced assembly declares a visible type in.</b>
/// Read from the compilation's own view of each reference, so what counts is exactly what a name in
/// this compilation can bind to. Internal-only namespaces count only where the reference grants
/// this assembly its internals, and a reference behind an extern alias counts only when one of its
/// aliases is <c>global</c>; otherwise nothing in it can be named that way, so nothing is shadowed.
/// </para>
/// <para>
/// ⚠ <b>Reference assemblies declare their types; facades do not, and need no special case.</b>
/// The reflection rule (<c>BNAQ1004</c>) had to read forwarded types because a runtime facade such as
/// <c>System.Runtime</c> exports nothing. At compile time the .NET reference pack's <c>System.*</c>
/// assemblies declare the types themselves, and a facade that only forwards contributes no namespace
/// of its own, which is correct: a name can bind through it only if the assembly it forwards to is
/// referenced as well, and that assembly supplies the root. Verified by <c>NamespaceShadowTests</c>
/// against the .NET 8 reference assemblies, whose <c>mscorlib</c> and <c>netstandard</c> are facades.
/// </para>
/// <para>
/// ⚠ <b>The message offers two fixes, not one.</b> Where the namespace follows the assembly name,
/// renaming only the namespace breaks that convention, so the fix there renames assembly and
/// namespace together, <c>.Avalonia</c> to <c>.AvaloniaUI</c>. The package id takes no part in name
/// resolution and may keep the word.
/// </para>
/// <para>
/// ⛔ <b>Inert with nothing to compare against.</b> A compilation whose references declare no visible
/// namespace at all registers no action: there is no root a segment could shadow.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NamespaceShadowAnalyzer : ScopedAnalyzer<NamespaceShadowAnalyzer.Roots>
{
    /// <summary>The rule's ID. Public API: a consumer's suppressions name it.</summary>
    public const string Id = "BNCQ1004";

    private static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "A namespace segment shadows the root namespace of a referenced assembly",
        messageFormat: "The segment '{0}' shadows the root namespace declared by {1}, so inside this namespace the name '{0}' resolves here first and a qualified name through it fails with CS0234 at the use site, about a type that plainly exists. Rename the segment: in the namespace alone, or, where namespaces follow the assembly name, in the assembly name as well (for example '{0}UI'). A package id is not a namespace and may keep the word.",
        Catalog.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "C# resolves the first identifier of a qualified name by walking outward from the current namespace, so a namespace segment that repeats the root namespace of a referenced assembly captures every qualified name through that root. The failure is CS0234 at the first use site that needs one, which can be years after the namespace was declared.",
        helpLinkUri: Catalog.HelpLink(Id));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    protected override Roots? EnterScope(Compilation compilation, AnalyzerOptions options, CancellationToken cancellationToken)
    {
        Roots roots = Roots.Of(compilation, cancellationToken);
        return roots.IsEmpty ? null : roots;
    }

    /// <inheritdoc />
    protected override void Register(CompilationStartAnalysisContext context, Roots scope) =>
        context.RegisterSyntaxNodeAction(
            nodeContext => Check(nodeContext, scope),
            SyntaxKind.NamespaceDeclaration,
            SyntaxKind.FileScopedNamespaceDeclaration);

    private static void Check(SyntaxNodeAnalysisContext context, Roots roots)
    {
        var declaration = (BaseNamespaceDeclarationSyntax)context.Node;

        // ⛔ The first segment of the FULL namespace can never shadow anything, and skipping it is
        // not an optimisation: inside Acme.Widgets the name Acme binds to the merged Acme that a
        // referenced Acme.Other lives in too. A declaration nested in another has no first segment
        // of its own to skip.
        bool first = declaration.Parent is not BaseNamespaceDeclarationSyntax;

        foreach (IdentifierNameSyntax segment in Segments(declaration.Name))
        {
            if (first)
            {
                first = false;
                continue;
            }

            string name = segment.Identifier.ValueText;
            if (roots.TryDescribe(name, out string? declarers))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, segment.GetLocation(), name, declarers));
            }
        }
    }

    /// <summary>The identifiers of a dotted name, left to right.</summary>
    private static IEnumerable<IdentifierNameSyntax> Segments(NameSyntax name)
    {
        switch (name)
        {
            case QualifiedNameSyntax qualified:
                foreach (IdentifierNameSyntax left in Segments(qualified.Left))
                {
                    yield return left;
                }

                if (qualified.Right is IdentifierNameSyntax right)
                {
                    yield return right;
                }

                break;
            case IdentifierNameSyntax identifier:
                yield return identifier;
                break;
        }
    }

    /// <summary>
    /// The top-level namespaces the compilation's references declare a visible type in, each with
    /// how many assemblies declare it and the one the message names.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The assembly named is a representative, chosen to be recognisable.</b> Measured: naming
    /// the first declaring assembly in reference order blamed <c>Microsoft.Win32.Primitives</c> for
    /// the root <c>System</c>, because the .NET reference pack is listed alphabetically. An assembly
    /// named after the root wins, then one named <c>Root.*</c>, then reference order; the message
    /// says how many others declare it too.
    /// </remarks>
    internal sealed class Roots
    {
        private readonly Dictionary<string, Declarers> _byRoot;

        private Roots(Dictionary<string, Declarers> byRoot)
        {
            _byRoot = byRoot;
        }

        /// <summary>Whether no reference declares a visible namespace, so nothing can be shadowed.</summary>
        public bool IsEmpty => _byRoot.Count == 0;

        /// <summary>Who declares <paramref name="root"/> as a top-level namespace, in words, if anyone does.</summary>
        public bool TryDescribe(string root, out string? declarers)
        {
            if (!_byRoot.TryGetValue(root, out Declarers? found))
            {
                declarers = null;
                return false;
            }

            declarers = found.Count == 1
                ? "the referenced assembly '" + found.Named + "'"
                : found.Count + " referenced assemblies, '" + found.Named + "' among them";
            return true;
        }

        public static Roots Of(Compilation compilation, CancellationToken cancellationToken)
        {
            Dictionary<string, Declarers> byRoot = new(StringComparer.Ordinal);

            foreach (MetadataReference reference in compilation.References)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ⛔ A reference behind extern aliases, none of them global, adds nothing to the global
                // namespace: its roots are reachable only as Alias::Root, so no segment shadows them.
                // Found by review: the first version reported them.
                ImmutableArray<string> aliases = reference.Properties.Aliases;
                if (!aliases.IsDefaultOrEmpty && !aliases.Contains("global"))
                {
                    continue;
                }

                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                {
                    continue;
                }

                bool internalsVisible = assembly.GivesAccessTo(compilation.Assembly);

                foreach (INamespaceSymbol root in assembly.GlobalNamespace.GetNamespaceMembers())
                {
                    if (!DeclaresVisibleType(root, internalsVisible))
                    {
                        continue;
                    }

                    if (byRoot.TryGetValue(root.Name, out Declarers? declarers))
                    {
                        declarers.Add(assembly.Name, root.Name);
                    }
                    else
                    {
                        byRoot.Add(root.Name, new Declarers(assembly.Name));
                    }
                }
            }

            return new Roots(byRoot);
        }

        /// <summary>How many assemblies declare one root, and the one a reader will recognise.</summary>
        private sealed class Declarers
        {
            public Declarers(string first)
            {
                Named = first;
                Count = 1;
            }

            public string Named { get; private set; }

            public int Count { get; private set; }

            public void Add(string assembly, string root)
            {
                Count++;
                if (Rank(assembly, root) < Rank(Named, root))
                {
                    Named = assembly;
                }
            }

            private static int Rank(string assembly, string root) =>
                string.Equals(assembly, root, StringComparison.Ordinal) ? 0
                : assembly.StartsWith(root + ".", StringComparison.Ordinal) ? 1
                : 2;
        }

        /// <summary>
        /// Whether the namespace, or one beneath it, holds a type this compilation can name.
        /// </summary>
        private static bool DeclaresVisibleType(INamespaceSymbol ns, bool internalsVisible)
        {
            foreach (INamedTypeSymbol type in ns.GetTypeMembers())
            {
                if (internalsVisible || type.DeclaredAccessibility == Accessibility.Public)
                {
                    return true;
                }
            }

            foreach (INamespaceSymbol child in ns.GetNamespaceMembers())
            {
                if (DeclaresVisibleType(child, internalsVisible))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
