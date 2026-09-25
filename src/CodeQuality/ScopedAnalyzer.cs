using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bennewitz.Ninja.CodeQuality;

/// <summary>
/// The base of every analyzer in this assembly: one that decides once per compilation whether it
/// applies, and registers nothing at all where it does not.
/// </summary>
/// <typeparam name="TScope">
/// What the gate hands to the registration: the per-compilation state a rule resolves once, such
/// as the symbols it compares against. A reference type, so that null can mean "out of scope".
/// </typeparam>
/// <remarks>
/// <para>
/// ⭐ <b>The compiler loads an analyzer assembly whole.</b> Every analyzer in this package is
/// instantiated and initialised in every compilation that references the package, whether or not
/// the rule has anything to say there. So each one decides for itself, per compilation, and an
/// analyzer that has nothing to compare against registers no per-symbol or per-node action at all.
/// The cost of an inert analyzer is then a few lookups at compilation start, and nothing per file.
/// </para>
/// <para>
/// ⛔ <b>The gate cannot register actions, by construction.</b> <see cref="EnterScope"/> receives
/// the compilation, the options and a token, never the <see cref="CompilationStartAnalysisContext"/>,
/// so nothing a subclass writes there can register an action; only <see cref="Register"/> can, and
/// it runs only when the gate returned a scope. <see cref="Initialize"/> is sealed so that a
/// subclass has no other path to a registration. <c>AnalyzerConventionTests</c> fails if any
/// analyzer in this assembly does not derive from this class.
/// </para>
/// <para>
/// ⚠ <b>Inert and clean look the same from outside.</b> A build with no findings cannot tell
/// "the rule had nothing to check" from "the rule checked and found nothing", so every rule's
/// tests show it firing on a real violation as well as staying silent on clean code and outside
/// its scope; a gate that is wrong fails as silence.
/// </para>
/// </remarks>
internal abstract class ScopedAnalyzer<TScope> : DiagnosticAnalyzer
    where TScope : class
{
    /// <summary>
    /// How generated code is treated; <see cref="GeneratedCodeAnalysisFlags.None"/> unless a rule
    /// overrides it, because a finding in a file nobody edits by hand is a finding nobody can fix.
    /// </summary>
    protected virtual GeneratedCodeAnalysisFlags GeneratedCode => GeneratedCodeAnalysisFlags.None;

    /// <inheritdoc />
    public sealed override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCode);
        context.RegisterCompilationStartAction(start =>
        {
            TScope? scope = EnterScope(start.Compilation, start.Options, start.CancellationToken);
            if (scope is null)
            {
                return;
            }

            Register(start, scope);
        });
    }

    /// <summary>
    /// The gate: whether this compilation is in scope and, if so, the state the rule needs in it.
    /// </summary>
    /// <returns>
    /// Null to stay inert, so that nothing is registered for this compilation; otherwise the scope
    /// that <see cref="Register"/> receives. Do only cheap work here: metadata-name lookups, a walk
    /// of the top-level namespaces, a read of the options.
    /// </returns>
    protected abstract TScope? EnterScope(Compilation compilation, AnalyzerOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Registers the rule's actions for a compilation the gate let in.
    /// </summary>
    /// <param name="context">The compilation-start context to register on.</param>
    /// <param name="scope">What <see cref="EnterScope"/> returned for this compilation.</param>
    protected abstract void Register(CompilationStartAnalysisContext context, TScope scope);
}
