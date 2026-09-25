using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics.Telemetry;
using Microsoft.CodeAnalysis.Testing;

namespace CodeQuality.Tests.Rules;

/// <summary>
/// Runs one analyzer over a plain compilation, configured from <c>.editorconfig</c> text the way
/// the compiler configures it, and reports what it found and how many actions it registered.
/// </summary>
/// <remarks>
/// <para>
/// ⚠ <b>The testing framework cannot show two things this can.</b> <c>CSharpAnalyzerTest</c>
/// enables every diagnostic of the analyzer under test, the disabled-by-default ones included, so
/// it cannot prove a rule is off until asked for. And it reports diagnostics only, so an inert test
/// written with it proves silence, which a rule whose gate never closes also produces on quiet
/// code. Found by review: every inert test still passed with all three gates forced open.
/// </para>
/// <para>
/// ⭐ <b>Registrations are what inert means.</b> A closed gate registers nothing beyond the
/// compilation-start action every analyzer here has, so <see cref="Run.Registrations"/> is zero.
/// A test pairs that with a control in scope, where it must not be, so a count that is always zero
/// cannot pass for proof.
/// </para>
/// <para>
/// The compiler hands severities to the driver through a <see cref="SyntaxTreeOptionsProvider"/> and
/// options such as <c>bennewitz_ninja_codequality.BNCQ1002.namespaces</c> through an
/// <see cref="AnalyzerConfigOptionsProvider"/>; both are built here from the same parsed file.
/// </para>
/// </remarks>
internal static class Plain
{
    public static Task<ImmutableArray<MetadataReference>> Net80() =>
        ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, TestContext.Current.CancellationToken);

    public static Task<ImmutableArray<MetadataReference>> NetStandard20() =>
        ReferenceAssemblies.NetStandard.NetStandard20.ResolveAsync(LanguageNames.CSharp, TestContext.Current.CancellationToken);

    public static async Task<Run> Analyze(DiagnosticAnalyzer analyzer, string source, IEnumerable<MetadataReference> references, string? editorconfig = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: "/Test0.cs", cancellationToken: cancellationToken);
        CSharpCompilationOptions options = new(OutputKind.DynamicallyLinkedLibrary);
        AnalyzerOptions analyzerOptions = new([]);

        if (editorconfig is not null)
        {
            AnalyzerConfigSet configs = AnalyzerConfigSet.Create(ImmutableArray.Create(AnalyzerConfig.Parse(editorconfig, "/.editorconfig")));
            options = options.WithSyntaxTreeOptionsProvider(new Severities(configs));
            analyzerOptions = new AnalyzerOptions([], new Options(configs));
        }

        CSharpCompilation compilation = CSharpCompilation.Create("Test", [tree], references, options);
        CompilationWithAnalyzers run = compilation.WithAnalyzers(
            [analyzer],
            new CompilationWithAnalyzersOptions(analyzerOptions, onAnalyzerException: null, concurrentAnalysis: false, logAnalyzerExecutionTime: true));

        ImmutableArray<Diagnostic> diagnostics = await run.GetAnalyzerDiagnosticsAsync(cancellationToken);
        AnalyzerTelemetryInfo telemetry = await run.GetAnalyzerTelemetryInfoAsync(analyzer, cancellationToken);

        return new Run(
            diagnostics,
            [.. compilation.GetDiagnostics(cancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)],
            Registered(telemetry));
    }

    /// <summary>Every action registered beyond compilation start.</summary>
    private static int Registered(AnalyzerTelemetryInfo telemetry) =>
        telemetry.SymbolActionsCount + telemetry.SymbolStartActionsCount + telemetry.SymbolEndActionsCount
        + telemetry.SyntaxNodeActionsCount + telemetry.SyntaxTreeActionsCount + telemetry.SemanticModelActionsCount
        + telemetry.OperationActionsCount + telemetry.OperationBlockStartActionsCount + telemetry.OperationBlockActionsCount
        + telemetry.OperationBlockEndActionsCount + telemetry.CodeBlockStartActionsCount + telemetry.CodeBlockActionsCount
        + telemetry.CodeBlockEndActionsCount + telemetry.CompilationActionsCount + telemetry.CompilationEndActionsCount
        + telemetry.AdditionalFileActionsCount;

    /// <summary>What one analyzer run produced.</summary>
    /// <param name="Diagnostics">What the analyzer reported.</param>
    /// <param name="Errors">The compilation's own errors, for tests that need a source that compiles.</param>
    /// <param name="Registrations">Actions registered beyond compilation start: zero when the gate stayed closed.</param>
    internal sealed record Run(ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<Diagnostic> Errors, int Registrations);

    /// <summary>The severities an <c>.editorconfig</c> sets, as the compiler hands them to the analyzer driver.</summary>
    private sealed class Severities(AnalyzerConfigSet configs) : SyntaxTreeOptionsProvider
    {
        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) => GeneratedKind.Unknown;

        public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity) =>
            configs.GetOptionsForSourcePath(tree.FilePath).TreeOptions.TryGetValue(diagnosticId, out severity);

        public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity) =>
            configs.GlobalConfigOptions.TreeOptions.TryGetValue(diagnosticId, out severity);
    }

    /// <summary>The options an <c>.editorconfig</c> sets, as an analyzer reads them.</summary>
    private sealed class Options(AnalyzerConfigSet configs) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => new Values(configs.GlobalConfigOptions.AnalyzerOptions);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Values(configs.GetOptionsForSourcePath(tree.FilePath).AnalyzerOptions);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Values(configs.GetOptionsForSourcePath(textFile.Path).AnalyzerOptions);
    }

    private sealed class Values(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
    }
}
