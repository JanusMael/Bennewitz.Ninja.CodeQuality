using System.Reflection;
using Bennewitz.Ninja.CodeQuality;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeQuality.Tests;

/// <summary>
/// Guards the convention every analyzer in the package keeps: it derives from
/// <see cref="ScopedAnalyzer{TScope}"/>, so it decides per compilation whether it applies and
/// registers nothing where it does not.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Derivation is enforced, not intent.</b> The compiler loads the analyzer assembly whole and
/// runs every analyzer in it in every compilation that references the package. One analyzer that
/// registers an action unconditionally runs everywhere, and nothing in a consumer's build says so.
/// </para>
/// <para>
/// ⚠ <b>The base is checked as well as the derivation.</b> A base whose <c>Initialize</c> became
/// overridable, or whose gate received something it could register on, would let a subclass
/// bypass the convention while still deriving from it.
/// </para>
/// </remarks>
public sealed class AnalyzerConventionTests
{
    private static readonly Assembly Analyzers = typeof(ScopedAnalyzer<>).Assembly;

    [Fact]
    public void Every_analyzer_derives_from_the_scoped_base()
    {
        Type[] analyzers = AnalyzerTypes();

        // ⛔ Without this, an assembly with no analyzers at all would pass.
        Assert.NotEmpty(analyzers);

        string[] outside = [.. analyzers.Where(type => !DerivesFromScopedBase(type)).Select(Name).Order()];
        Assert.True(
            outside.Length == 0,
            "These analyzers do not derive from ScopedAnalyzer<TScope>, so nothing stops them registering in every compilation:\n  "
            + string.Join("\n  ", outside));
    }

    [Fact]
    public void Every_analyzer_is_sealed()
    {
        string[] open = [.. AnalyzerTypes().Where(type => !type.IsSealed).Select(Name).Order()];
        Assert.True(open.Length == 0, "These analyzers are not sealed: " + string.Join(", ", open));
    }

    /// <summary>
    /// The package has no API: a consumer configures it through <c>.editorconfig</c>, never through
    /// a type. A public type would be a surface to keep compatible for no reader.
    /// </summary>
    /// <remarks>
    /// ⚠ AutoVersioning generates a public <c>DirectoryBuildInfo</c> into every family assembly,
    /// under its own namespace and with no attribute that marks it generated. It is excluded by that
    /// namespace; everything this repository writes is held to internal.
    /// </remarks>
    [Fact]
    public void The_assembly_exports_no_public_type_of_its_own()
    {
        string[] exported =
        [
            .. Analyzers.GetExportedTypes()
                .Where(type => type.Namespace != "Bennewitz.Ninja.AutoVersioning")
                .Select(Name)
                .Order(),
        ];

        Assert.True(exported.Length == 0, "Public types in the analyzer assembly: " + string.Join(", ", exported));
    }

    [Fact]
    public void The_base_seals_Initialize()
    {
        MethodInfo initialize = typeof(ScopedAnalyzer<>).GetMethod(nameof(DiagnosticAnalyzer.Initialize))!;

        Assert.True(initialize.IsFinal, "ScopedAnalyzer<TScope>.Initialize is overridable, so a subclass could register outside the gate.");
    }

    /// <summary>
    /// The gate receives no Roslyn context with a <c>Register…</c> method on it, so it structurally
    /// cannot register an action; only the registration hook, which runs in scope, can.
    /// </summary>
    /// <remarks>
    /// ⚠ Roslyn types only. <see cref="CancellationToken"/> has <c>Register</c> methods too, and
    /// they register callbacks, not analysis actions.
    /// </remarks>
    [Fact]
    public void The_gate_receives_nothing_it_could_register_on()
    {
        MethodInfo gate = typeof(ScopedAnalyzer<>).GetMethod("EnterScope", BindingFlags.NonPublic | BindingFlags.Instance)!;

        string[] registrable =
        [
            .. gate.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Where(type => type.Namespace?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true)
                .Where(type => type.GetMethods().Any(method => method.Name.StartsWith("Register", StringComparison.Ordinal)))
                .Select(type => type.Name),
        ];

        Assert.True(registrable.Length == 0, "The gate receives a Roslyn type with Register methods: " + string.Join(", ", registrable));
    }

    private static Type[] AnalyzerTypes() =>
        [.. Analyzers.GetTypes().Where(type => type.IsDefined(typeof(DiagnosticAnalyzerAttribute), inherit: false))];

    private static bool DerivesFromScopedBase(Type type)
    {
        for (Type? parent = type.BaseType; parent is not null; parent = parent.BaseType)
        {
            if (parent.IsGenericType && parent.GetGenericTypeDefinition() == typeof(ScopedAnalyzer<>))
            {
                return true;
            }
        }

        return false;
    }

    private static string Name(Type type) => type.FullName ?? type.Name;
}
