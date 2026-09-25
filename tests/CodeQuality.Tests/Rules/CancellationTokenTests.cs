using Bennewitz.Ninja.CodeQuality.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace CodeQuality.Tests.Rules;

/// <summary>
/// BNCQ1001 is off until enabled; enabled, it fires on a defaulted token and on a token-less
/// overload of a tokened sibling, in the visible surface, and on nothing else.
/// </summary>
public sealed class CancellationTokenTests
{
    /// <summary>What a consumer writes to turn the policy on.</summary>
    private const string Enabled = """
        root = true

        [*.cs]
        dotnet_diagnostic.BNCQ1001.severity = warning
        """;

    private static Task Run(string source, bool enabled = true, bool preview = false)
    {
        RuleTest<CancellationTokenAnalyzer> test = new(source);
        if (enabled)
        {
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", Enabled));
        }

        return preview ? test.InPreview().Run() : test.Run();
    }

    /// <summary>
    /// A method inside a C# 14 extension block is checked where it is written, once: the static
    /// method the compiler adds to the enclosing class for it is not reported a second time.
    /// </summary>
    [Fact]
    public Task A_method_in_an_extension_block_fires_once() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public static class ClientExtensions
            {
                extension(object client)
                {
                    public Task GoAsync(CancellationToken {|BNCQ1001:token|} = default) => Task.CompletedTask;
                }
            }
            """, preview: true);

    /// <summary>
    /// The policy is opt-in: the most conventional style there is must not light up on install.
    /// </summary>
    /// <remarks>
    /// ⚠ On a plain compilation, not the testing framework: the framework enables every
    /// diagnostic of the analyzer under test, disabled-by-default ones included, so it cannot show
    /// what a consumer sees before enabling the rule. This is the compiler's own path.
    /// </remarks>
    [Fact]
    public async Task Until_enabled_a_defaulted_token_is_not_reported()
    {
        Plain.Run run = await Plain.Analyze(new CancellationTokenAnalyzer(), DefaultedToken, await Plain.Net80());

        Assert.Empty(run.Errors);
        Assert.Empty(run.Diagnostics);
    }

    /// <summary>
    /// Enabling the rule the way the README says, in <c>.editorconfig</c>, is what turns it on.
    /// </summary>
    [Fact]
    public async Task Enabled_in_editorconfig_a_defaulted_token_is_reported()
    {
        Plain.Run run = await Plain.Analyze(new CancellationTokenAnalyzer(), DefaultedToken, await Plain.Net80(), Enabled);

        Assert.Empty(run.Errors);
        Diagnostic finding = Assert.Single(run.Diagnostics);
        Assert.Equal(CancellationTokenAnalyzer.Id, finding.Id);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
    }

    private const string DefaultedToken = """
        using System.Threading;
        using System.Threading.Tasks;

        public class Client
        {
            public Task GoAsync(CancellationToken token = default) => Task.CompletedTask;
        }
        """;

    [Fact]
    public Task A_defaulted_token_fires_at_the_parameter() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public class Client
            {
                public Task GoAsync(CancellationToken {|BNCQ1001:token|} = default) => Task.CompletedTask;
            }
            """);

    [Fact]
    public Task An_overload_that_leaves_the_token_out_fires_at_its_name() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public class Client
            {
                public Task {|BNCQ1001:GoAsync|}(int count) => GoAsync(count, CancellationToken.None);
                public Task GoAsync(int count, CancellationToken token) => Task.CompletedTask;
            }
            """);

    /// <summary>A leading run of the sibling's parameters is the same overload, shortened.</summary>
    [Fact]
    public Task An_overload_taking_a_leading_run_of_the_siblings_parameters_fires() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public class Client
            {
                public Task {|BNCQ1001:GoAsync|}(int count) => Task.CompletedTask;
                public Task GoAsync(int count, string name, CancellationToken token) => Task.CompletedTask;
            }
            """);

    /// <summary>Same name, different parameters: an overload in its own right, not an abbreviation.</summary>
    [Fact]
    public Task An_overload_with_other_parameters_stays_silent() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public class Client
            {
                public Task GoAsync(string name) => Task.CompletedTask;
                public Task GoAsync(int count, string name, CancellationToken token) => Task.CompletedTask;
            }
            """);

    /// <summary>Type parameters compare by name, so a generic pair is recognised.</summary>
    [Fact]
    public Task A_generic_overload_that_leaves_the_token_out_fires() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public class Client
            {
                public Task<T> {|BNCQ1001:GoAsync|}<T>(T value) => Task.FromResult(value);
                public Task<T> GoAsync<T>(T value, CancellationToken token) => Task.FromResult(value);
            }
            """);

    [Fact]
    public Task A_required_token_stays_silent() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public class Client
            {
                public Task GoAsync(CancellationToken token) => Task.CompletedTask;
                public Task GoAsync(int count, CancellationToken token) => Task.CompletedTask;
            }
            """);

    /// <summary>
    /// A caller passes a constructor its token like any other method, so a constructor is held to
    /// the same policy. Found by review: the first version read only ordinary methods.
    /// </summary>
    [Fact]
    public Task A_constructor_with_a_defaulted_token_fires() =>
        Run("""
            using System.Threading;

            public class Poller
            {
                public Poller(CancellationToken {|BNCQ1001:token|} = default) { }
            }
            """);

    [Fact]
    public Task A_constructor_that_leaves_the_token_out_fires() =>
        Run("""
            using System.Threading;

            public class Poller
            {
                public {|BNCQ1001:Poller|}(int interval) : this(interval, CancellationToken.None) { }
                public Poller(int interval, CancellationToken token) { }
            }
            """);

    /// <summary>Protected is part of the surface a subclass elsewhere binds to.</summary>
    [Fact]
    public Task A_protected_method_fires() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public class Client
            {
                protected virtual Task GoAsync(CancellationToken {|BNCQ1001:token|} = default) => Task.CompletedTask;
            }
            """);

    [Fact]
    public Task An_interface_method_fires() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public interface IClient
            {
                Task GoAsync(CancellationToken {|BNCQ1001:token|} = default);
            }
            """);

    [Fact]
    public Task A_delegate_fires() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public delegate Task Operation(CancellationToken {|BNCQ1001:token|} = default);
            """);

    [Fact]
    public Task Methods_outside_the_visible_surface_stay_silent() =>
        Run("""
            using System.Threading;
            using System.Threading.Tasks;

            public class Client
            {
                internal Task GoAsync(CancellationToken token = default) => Task.CompletedTask;
                private Task RunAsync(int count) => RunAsync(count, CancellationToken.None);
                private Task RunAsync(int count, CancellationToken token) => Task.CompletedTask;
                private protected Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
            }

            internal class Hidden
            {
                public Task GoAsync(CancellationToken token = default) => Task.CompletedTask;

                public class Nested
                {
                    public Task GoAsync(CancellationToken token = default) => Task.CompletedTask;
                }
            }
            """);

    [Fact]
    public Task Generated_code_is_not_analyzed() =>
        Run("""
            // <auto-generated/>
            using System.Threading;
            using System.Threading.Tasks;

            public class Client
            {
                public Task GoAsync(CancellationToken token = default) => Task.CompletedTask;
            }
            """);

    /// <summary>
    /// Where the compilation cannot name <c>CancellationToken</c>, nothing is registered. The rule is
    /// enabled in both runs, so the count is the gate's doing and not the opt-in's.
    /// </summary>
    [Fact]
    public async Task Without_CancellationToken_in_reach_the_rule_registers_nothing()
    {
        const string Source = """
            public class Client
            {
                public void Go(System.Threading.CancellationToken token = default) { }
            }
            """;

        Plain.Run inert = await Plain.Analyze(new CancellationTokenAnalyzer(), Source, [], Enabled);
        Plain.Run control = await Plain.Analyze(new CancellationTokenAnalyzer(), Source, await Plain.Net80(), Enabled);

        Assert.Equal(0, inert.Registrations);
        Assert.Empty(inert.Diagnostics);
        Assert.True(control.Registrations > 0, "In scope, the rule registered nothing, so a zero proves nothing.");
        Assert.Single(control.Diagnostics);
    }
}
