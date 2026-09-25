using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace CodeQuality.Tests.Rules;

/// <summary>
/// One analyzer run over one test compilation, on the .NET 8 reference assemblies unless a test
/// says otherwise, with <c>{|BNCQ1004:span|}</c> markup naming the diagnostics it expects.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Every rule is proved both ways.</b> A test that only asserts "no diagnostics" passes just
/// as well on a rule whose gate is wrong, because a rule that never registers reports nothing. So
/// each rule's tests show it firing on a real violation as well as staying silent on clean code
/// and outside its scope, and the firing tests are the ones that catch a broken gate.
/// </para>
/// <para>
/// ⚠ <b>The reference assemblies are real.</b> <see cref="ReferenceAssemblies.Net"/> resolves the
/// .NET reference pack from nuget.org on first use and caches it, so the rules meet the same
/// <c>System.*</c> assemblies a consumer's compilation does, facades included.
/// </para>
/// </remarks>
internal sealed class RuleTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public RuleTest(string source)
    {
        TestCode = source;
        ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
    }

    /// <summary>Runs the analyzer over <paramref name="source"/> and checks its markup.</summary>
    public static Task Run(string source) => new RuleTest<TAnalyzer>(source).Run();

    /// <summary>
    /// Runs the analyzer over <paramref name="source"/> parsed as the preview language version.
    /// </summary>
    public static Task RunPreview(string source) => new RuleTest<TAnalyzer>(source).InPreview().Run();

    /// <summary>
    /// Parses this test's sources as the preview language version.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>How the Roslyn 4.14 pin parses C# 14 extension blocks.</b> Measured: under the latest
    /// language version 4.14 rejects them with CS1513, and under preview it builds the same extension
    /// type the released compiler does. The suite also runs against Roslyn 5.9, where preview
    /// includes C# 14 as released.
    /// </remarks>
    public RuleTest<TAnalyzer> InPreview()
    {
        SolutionTransforms.Add((solution, projectId) =>
            solution.WithProjectParseOptions(
                projectId,
                ((CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!).WithLanguageVersion(LanguageVersion.Preview)));
        return this;
    }

    /// <summary>Runs this test under the test framework's cancellation.</summary>
    public Task Run() => RunAsync(TestContext.Current.CancellationToken);
}
