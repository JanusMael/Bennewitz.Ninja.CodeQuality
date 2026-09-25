using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bennewitz.Ninja.CodeQuality.Rules;

/// <summary>
/// BNCQ1001: no visible method lets a caller leave the <see cref="CancellationToken"/> out, by a
/// default value on the parameter or by an overload without it.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>A defaulted token is a cancellation contract that silently opts out.</b> Write
/// <c>CancellationToken token = default</c> and every caller who forgets it gets
/// <see cref="CancellationToken.None"/>: an operation that cannot be cancelled, chosen by nobody,
/// visible nowhere. The call compiles, runs, and hangs on shutdown. Requiring the parameter makes
/// the caller write <c>CancellationToken.None</c> when they mean it, which a reviewer can see.
/// </para>
/// <para>
/// ⛔ <b>A token-less overload is the same default, moved.</b> Answering a finding with
/// <c>Build(a, b) => Build(a, b, CancellationToken.None)</c> removes the defaulted parameter and
/// keeps the opt-out. So a method is reported when a same-named sibling takes a token and this
/// one's parameters are that sibling's with the token removed, or a leading run of them.
/// </para>
/// <para>
/// ⚠ <b>The fix that holds: put the token ahead of any optional parameter.</b> Dropping the
/// token's default alone is CS1737 when an optional parameter follows it, and dropping that one's
/// default too leaves every caller writing a literal <c>null</c> for an argument nobody thought
/// about. Measured on a real adoption of <c>BNAQ1001</c>: moving the token ahead of the optional
/// parameter touched 71 call sites and left one literal <c>null</c>; dropping both defaults would
/// have left 44.
/// </para>
/// <para>
/// ⚠ <b>This is a POLICY rule, not a defect rule, so it is off until a consumer turns it on</b>
/// with <c>dotnet_diagnostic.BNCQ1001.severity = warning</c> in <c>.editorconfig</c>. The BCL
/// defaults its own tokens everywhere, so this fires on code written in the most conventional
/// style there is, and a finding is not evidence of a bug. It is the rule an SDK adopts when its
/// cancellation contract is load-bearing and it would rather be verbose than let a caller opt out
/// by accident.
/// </para>
/// <para>
/// ⚠ <b>Visible means public or protected, at every level.</b> <c>BNAQ1001</c> reads public
/// methods of exported types; here a protected method counts too, because a subclass in another
/// assembly binds to it just the same. Constructors count, which <c>BNAQ1001</c> does not read: a
/// caller passes a constructor its token like any other method. Compiler-generated members are
/// skipped, and so are accessors, operators and explicit interface implementations, which a caller
/// cannot pass a token to by name.
/// </para>
/// <para>
/// ⛔ <b>Inert where <see cref="CancellationToken"/> does not exist.</b> A compilation that cannot
/// name the type cannot declare a parameter of it, so nothing is registered.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CancellationTokenAnalyzer : ScopedAnalyzer<CancellationTokenAnalyzer.Scope>
{
    /// <summary>The rule's ID. Public API: a consumer's suppressions name it.</summary>
    public const string Id = "BNCQ1001";

    private static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "A visible method lets the caller leave the cancellation token out",
        messageFormat: "'{0}' lets a caller leave the cancellation token out: {1}. Require the token, ahead of any optional parameter, so that a caller who means CancellationToken.None has to write it.",
        Catalog.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "A CancellationToken parameter with a default value, or an overload that omits the token a sibling takes, hands every caller who forgets it CancellationToken.None: an operation that cannot be cancelled, chosen by nobody and visible nowhere. A policy rule, off by default; enable it where the cancellation contract is load-bearing.",
        helpLinkUri: Catalog.HelpLink(Id));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    protected override Scope? EnterScope(Compilation compilation, AnalyzerOptions options, CancellationToken cancellationToken) =>
        compilation.GetTypeByMetadataName("System.Threading.CancellationToken") is { } token ? new Scope(token) : null;

    /// <inheritdoc />
    protected override void Register(CompilationStartAnalysisContext context, Scope scope) =>
        context.RegisterSymbolAction(symbolContext => Check(symbolContext, scope), SymbolKind.NamedType);

    private static void Check(SymbolAnalysisContext context, Scope scope)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!Surface.IsVisibleOutside(type))
        {
            return;
        }

        List<Signature> methods = [];
        foreach (ISymbol member in type.GetMembers())
        {
            // A delegate's Invoke is implicitly declared and is the delegate's whole signature. A
            // constructor takes its token from the caller like any method; found by review, the
            // first version read only ordinary methods.
            if (member is IMethodSymbol method
                && (method.MethodKind == MethodKind.DelegateInvoke
                    || (method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor && !method.IsImplicitlyDeclared))
                && Surface.IsVisibleOutside(method))
            {
                methods.Add(Signature.Of(method, scope.Token));
            }
        }

        foreach (Signature method in methods)
        {
            foreach (IParameterSymbol parameter in method.Method.Parameters)
            {
                if (parameter.HasExplicitDefaultValue && SymbolEqualityComparer.Default.Equals(parameter.Type, scope.Token))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        Location(parameter, type),
                        Name(method.Method),
                        $"the CancellationToken parameter '{parameter.Name}' has a default value, so a caller who omits it gets CancellationToken.None and an operation that cannot be cancelled, chosen by nobody and visible nowhere"));
                }
            }
        }

        foreach (Signature method in methods)
        {
            if (method.TakesToken)
            {
                continue;
            }

            foreach (Signature sibling in methods)
            {
                if (sibling.TakesToken && sibling.Method.Name == method.Method.Name && method.Abbreviates(sibling))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        Location(method.Method, type),
                        Name(method.Method),
                        $"it is the overload '{Name(sibling.Method)}' with the token left out, so a caller who uses it gets CancellationToken.None without choosing it: the defaulted token again, one overload over"));
                    break;
                }
            }
        }
    }

    private static string Name(IMethodSymbol method) => method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    /// <summary>The symbol's own location, or its type's where it has none in source, as a delegate's Invoke may not.</summary>
    private static Location Location(ISymbol symbol, INamedTypeSymbol type) =>
        symbol.Locations.Length > 0 ? symbol.Locations[0] : type.Locations[0];

    /// <summary>The <see cref="CancellationToken"/> type as this compilation sees it.</summary>
    internal sealed class Scope
    {
        public Scope(INamedTypeSymbol token)
        {
            Token = token;
        }

        public INamedTypeSymbol Token { get; }
    }

    /// <summary>
    /// A method with its parameter types other than the token rendered by NAME rather than
    /// identity: two generic methods each declare their own <c>T</c>, and identity would call them
    /// different types.
    /// </summary>
    private sealed class Signature
    {
        private readonly string[] _tokenless;

        private Signature(IMethodSymbol method, string[] tokenless, bool takesToken)
        {
            Method = method;
            _tokenless = tokenless;
            TakesToken = takesToken;
        }

        public IMethodSymbol Method { get; }

        public bool TakesToken { get; }

        public static Signature Of(IMethodSymbol method, INamedTypeSymbol token)
        {
            List<string> tokenless = [];
            bool takesToken = false;

            foreach (IParameterSymbol parameter in method.Parameters)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, token))
                {
                    takesToken = true;
                }
                else
                {
                    tokenless.Add(parameter.RefKind + " " + parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
            }

            return new Signature(method, [.. tokenless], takesToken);
        }

        /// <summary>
        /// Whether this method's parameters are <paramref name="sibling"/>'s with every token
        /// removed, or a leading run of them: the overload that exists only to omit the token.
        /// </summary>
        public bool Abbreviates(Signature sibling)
        {
            if (_tokenless.Length > sibling._tokenless.Length)
            {
                return false;
            }

            for (int i = 0; i < _tokenless.Length; i++)
            {
                if (!string.Equals(_tokenless[i], sibling._tokenless[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
