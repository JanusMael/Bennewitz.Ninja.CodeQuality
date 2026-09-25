using System.Collections.Immutable;
using Bennewitz.Ninja.CodeQuality.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace CodeQuality.Tests.Rules;

/// <summary>
/// BNCQ1002 fires on a covered type in the visible surface, named or reached, and on nothing else.
/// </summary>
public sealed class SurfaceLeakTests
{
    [Fact]
    public Task A_property_of_a_covered_type_fires_at_the_property() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            using System.Text.Json.Nodes;

            public class Document
            {
                public JsonNode {|BNCQ1002:Root|} { get; set; }
            }
            """);

    /// <summary>A generic argument leaks as much as a bare type, however deep.</summary>
    [Fact]
    public Task A_covered_type_argument_fires_however_deep() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            using System.Collections.Generic;
            using System.Text.Json.Nodes;
            using System.Threading.Tasks;

            public class Document
            {
                public Task<JsonNode> {|BNCQ1002:ReadAsync|}() => Task.FromResult<JsonNode>(null);
                public IReadOnlyList<KeyValuePair<string, JsonArray[]>> {|BNCQ1002:Sections|} { get; set; }
            }
            """);

    /// <summary>
    /// A nested type of a constructed generic carries its container's type arguments:
    /// <c>List&lt;JsonNode&gt;.Enumerator</c> names <c>JsonNode</c> as surely as
    /// <c>List&lt;JsonNode&gt;</c> does. Found by review: the first version read only the nested
    /// type's own arguments.
    /// </summary>
    [Fact]
    public Task A_covered_type_argument_of_a_containing_type_fires() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            using System.Collections.Generic;
            using System.Text.Json.Nodes;

            public class Document
            {
                public Dictionary<string, JsonNode>.ValueCollection {|BNCQ1002:Values|} => null;
                public List<JsonNode>.Enumerator {|BNCQ1002:GetEnumerator|}() => default;
            }
            """);

    /// <summary>
    /// A constraint is part of the signature a caller must satisfy, so a covered type named there
    /// binds the consumer as a parameter type would. Found by review.
    /// </summary>
    [Fact]
    public Task A_constraint_naming_a_covered_type_fires() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            using System.Text.Json.Nodes;

            public class Document
            {
                public void {|BNCQ1002:Add|}<T>(T node) where T : JsonNode { }
            }

            public class {|BNCQ1002:Box|}<T> where T : JsonNode
            {
            }

            public delegate void {|BNCQ1002:Handler|}<T>(T node) where T : JsonNode;
            """);

    /// <summary>
    /// A referenced type is reached through its constraints as well as its members: naming
    /// <c>Vendor.Box&lt;Derived&gt;</c> binds the consumer to what <c>T</c> must derive from, and so
    /// does a referenced method's constraint.
    /// </summary>
    /// <remarks>
    /// ⚠ A covered namespace of the vendor's own, because with <c>JsonNode</c> the case cannot be
    /// built: every closed <c>Box&lt;X&gt;</c> would have a covered <c>X</c>, and the rule would fire
    /// on that argument directly, fix or no fix.
    /// </remarks>
    [Fact]
    public async Task A_referenced_type_reaches_a_covered_type_through_its_constraints()
    {
        Images.Image vendor = await Images.Compile("Vendor", """
            namespace Vendor.Internal
            {
                public class Model { }
            }

            namespace Vendor
            {
                public class Box<T> where T : Internal.Model { }

                public class Derived : Internal.Model { }

                public class Builder
                {
                    public void Add<T>(T item) where T : Internal.Model { }
                }
            }
            """);

        RuleTest<SurfaceLeakAnalyzer> test = new("""
            public class Client
            {
                public Vendor.Box<Vendor.Derived> {|#0:Fetch|}() => null;
                public Vendor.Builder {|#1:Create|}() => null;
            }
            """);
        test.TestState.AdditionalReferences.Add(vendor.Reference);
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true

            [*.cs]
            bennewitz_ninja_codequality.BNCQ1002.namespaces = Vendor.Internal
            """));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(SurfaceLeakAnalyzer.Id, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Box<Derived> Client.Fetch()", "the type 'Vendor.Internal.Model' through 'Box constraint'"));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(SurfaceLeakAnalyzer.Id, DiagnosticSeverity.Warning)
                .WithLocation(1)
                .WithArguments("Builder Client.Create()", "the type 'Vendor.Internal.Model' through 'Builder.Add'"));

        await test.Run();
    }

    /// <summary>
    /// A covered namespace declared only in a reference behind an extern alias is still in reach:
    /// the alias hides it from the merged global namespace, not from a signature that names it as
    /// <c>V::Vendor.Internal.Model</c>. Found as the twin of <c>BNCQ1004</c>'s alias defect.
    /// </summary>
    [Fact]
    public async Task A_covered_namespace_behind_an_extern_alias_fires()
    {
        Images.Image vendor = await Images.Compile("Vendor", """
            namespace Vendor.Internal
            {
                public class Model { }
            }
            """);

        RuleTest<SurfaceLeakAnalyzer> test = new("""
            extern alias V;

            public class Client
            {
                public V::Vendor.Internal.Model {|BNCQ1002:Fetch|}() => null;
            }
            """);
        test.TestState.AdditionalReferences.Add(vendor.Reference.WithAliases(["V"]));
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true

            [*.cs]
            bennewitz_ninja_codequality.BNCQ1002.namespaces = Vendor.Internal
            """));

        await test.Run();
    }

    /// <summary>
    /// A signature that names a covered type directly is reported for that, not for a longer path
    /// through another of its types, whichever comes first in the signature.
    /// </summary>
    [Fact]
    public async Task A_direct_naming_is_reported_before_a_path()
    {
        Images.Image vendor = await Images.Compile("Vendor", """
            namespace Vendor
            {
                public class Envelope
                {
                    public System.Text.Json.Nodes.JsonNode Payload { get; set; }
                }
            }
            """);

        RuleTest<SurfaceLeakAnalyzer> test = new("""
            public class Client
            {
                public void {|#0:Send|}(Vendor.Envelope envelope, System.Text.Json.Nodes.JsonArray extra) { }
            }
            """);
        test.TestState.AdditionalReferences.Add(vendor.Reference);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(SurfaceLeakAnalyzer.Id, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("void Client.Send(Envelope envelope, JsonArray extra)", "the type 'System.Text.Json.Nodes.JsonArray'"));

        await test.Run();
    }

    [Fact]
    public Task Parameters_fields_events_and_constructors_are_surface() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            using System;
            using System.Text.Json.Nodes;

            public class Document
            {
                public JsonNode {|BNCQ1002:Root|};
                public event EventHandler<JsonNode> {|BNCQ1002:Changed|};
                public {|BNCQ1002:Document|}(JsonNode root) { }
                public void {|BNCQ1002:Replace|}(JsonNode root) { }
                public static implicit operator {|BNCQ1002:Document|}(JsonNode root) => null;
            }
            """);

    /// <summary>What a type implements is its surface as much as its members are.</summary>
    [Fact]
    public Task An_implemented_interface_naming_a_covered_type_fires_at_the_type() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            using System.Collections;
            using System.Collections.Generic;
            using System.Text.Json.Nodes;

            public class {|BNCQ1002:Documents|} : IEnumerable<JsonNode>
            {
                public IEnumerator<JsonNode> {|BNCQ1002:GetEnumerator|}() => null;
                IEnumerator IEnumerable.GetEnumerator() => null;
            }
            """);

    /// <summary>
    /// A referenced type is opened: an envelope whose payload is covered binds the consumer one
    /// hop further, and the message says which hop. The vendor project leaks in its own right and
    /// is reported there, since the analyzer runs over every project in the test solution.
    /// </summary>
    [Fact]
    public async Task A_referenced_type_reaching_a_covered_type_fires_and_names_the_path()
    {
        RuleTest<SurfaceLeakAnalyzer> test = new("""
            public class Client
            {
                public Vendor.Envelope {|#0:Fetch|}() => null;
            }
            """);
        test.TestState.AdditionalProjects["Vendor"].Sources.Add("""
            using System.Text.Json.Nodes;

            namespace Vendor
            {
                public class Envelope
                {
                    public Wrapper Body { get; set; }
                }

                public class Wrapper
                {
                    public JsonNode {|BNCQ1002:Payload|} { get; set; }
                }
            }
            """);
        test.TestState.AdditionalProjectReferences.Add("Vendor");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(SurfaceLeakAnalyzer.Id, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Envelope Client.Fetch()", "the type 'System.Text.Json.Nodes.JsonNode' through 'Envelope.Body, Wrapper.Payload'"));

        await test.Run();
    }

    /// <summary>
    /// A package that never names a covered type itself, but exposes one from a package that does,
    /// leaks it all the same: whether a type is worth opening is decided through its assembly's
    /// references transitively, not only directly.
    /// </summary>
    [Fact]
    public async Task A_leak_two_packages_away_fires()
    {
        Images.Image inner = await Images.Compile("Inner", """
            namespace Inner
            {
                public class Wrapper
                {
                    public System.Text.Json.Nodes.JsonNode Payload { get; set; }
                }
            }
            """);
        Images.Image outer = await Images.Compile("Outer", """
            namespace Outer
            {
                public class Envelope
                {
                    public Inner.Wrapper Body { get; set; }
                }
            }
            """, inner.Reference);

        // ⛔ The case exists only if Outer's own metadata does not reference System.Text.Json.
        Assert.Contains("Inner", outer.References);
        Assert.DoesNotContain("System.Text.Json", outer.References);

        RuleTest<SurfaceLeakAnalyzer> test = new("""
            public class Client
            {
                public Outer.Envelope {|#0:Fetch|}() => null;
            }
            """);
        test.TestState.AdditionalReferences.Add(inner.Reference);
        test.TestState.AdditionalReferences.Add(outer.Reference);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(SurfaceLeakAnalyzer.Id, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Envelope Client.Fetch()", "the type 'System.Text.Json.Nodes.JsonNode' through 'Envelope.Body, Wrapper.Payload'"));

        await test.Run();
    }

    /// <summary>
    /// A type whose only way to a covered type is back through a type that was still being
    /// examined when it was reached: its answer is the cycle's, whichever member is met first.
    /// </summary>
    [Fact]
    public async Task Both_types_of_a_cycle_that_reaches_a_covered_type_fire()
    {
        Images.Image vendor = await Images.Compile("Vendor", """
            namespace Vendor
            {
                public class Node
                {
                    public Link Next { get; set; }
                    public System.Text.Json.Nodes.JsonNode Value { get; set; }
                }

                public class Link
                {
                    public Node Back { get; set; }
                }
            }
            """);

        RuleTest<SurfaceLeakAnalyzer> test = new("""
            public class Client
            {
                public Vendor.Node {|BNCQ1002:First|} { get; set; }
                public Vendor.Link {|BNCQ1002:Second|} { get; set; }
            }
            """);
        test.TestState.AdditionalReferences.Add(vendor.Reference);

        await test.Run();
    }

    /// <summary>
    /// A generic type whose members nest its own argument one level deeper names infinitely many
    /// constructed types. The walk opens the generic definition once, so it ends.
    /// </summary>
    [Fact]
    public async Task A_generic_type_that_nests_itself_ends_and_stays_silent()
    {
        Images.Image vendor = await Images.Compile("Vendor", """
            namespace Vendor
            {
                public class Nest<T>
                {
                    public Nest<Nest<T>> Deeper() => null;
                    public T Value { get; set; }
                }

                public class Unrelated
                {
                    public System.Text.Json.Nodes.JsonNode Payload { get; set; }
                }
            }
            """);

        RuleTest<SurfaceLeakAnalyzer> test = new("""
            public class Client
            {
                public Vendor.Nest<string> Fetch() => null;
            }
            """);
        test.TestState.AdditionalReferences.Add(vendor.Reference);

        await test.Run().WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A C# 14 extension block's receiver is part of what every member in the block exposes: calling
    /// <c>node.Touch()</c> binds the consumer to <c>JsonNode</c>. Reported once, at the block.
    /// </summary>
    [Fact]
    public Task An_extension_block_whose_receiver_is_covered_fires_at_the_block() =>
        RuleTest<SurfaceLeakAnalyzer>.RunPreview("""
            using System.Text.Json.Nodes;

            public static class JsonExtensions
            {
                {|BNCQ1002:extension|}(JsonNode node)
                {
                    public int Depth => 0;
                    public void Touch() { }
                }
            }
            """);

    /// <summary>
    /// A generic block names its covered type in the constraint on its own type parameter, which the
    /// block exposes as surely as a receiver.
    /// </summary>
    [Fact]
    public Task A_generic_extension_block_constrained_to_a_covered_type_fires_at_the_block() =>
        RuleTest<SurfaceLeakAnalyzer>.RunPreview("""
            public static class JsonExtensions
            {
                {|BNCQ1002:extension|}<T>(T node) where T : System.Text.Json.Nodes.JsonNode
                {
                    public bool IsEmpty => false;
                }
            }
            """);

    /// <summary>
    /// Inside a block, a member is checked like any other member, and the static method the
    /// compiler adds to the enclosing class for it is not reported a second time.
    /// </summary>
    [Fact]
    public Task A_member_of_an_extension_block_is_checked_like_any_member() =>
        RuleTest<SurfaceLeakAnalyzer>.RunPreview("""
            public static class TextExtensions
            {
                extension(string text)
                {
                    public System.Text.Json.Nodes.JsonNode {|BNCQ1002:Parse|}() => null;
                    public int Words => 0;
                }
            }
            """);

    [Fact]
    public Task Extension_blocks_on_uncovered_receivers_or_outside_the_surface_stay_silent() =>
        RuleTest<SurfaceLeakAnalyzer>.RunPreview("""
            public static class TextExtensions
            {
                extension(string text)
                {
                    public int Words => 0;
                }
            }

            internal static class JsonExtensions
            {
                extension(System.Text.Json.Nodes.JsonNode node)
                {
                    public int Depth => 0;
                }
            }
            """);

    /// <summary>
    /// This compilation's own types are not opened: the member that leaks reports itself, once.
    /// </summary>
    [Fact]
    public Task An_own_type_is_reported_where_it_leaks_and_not_where_it_is_used() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            using System.Text.Json.Nodes;

            public class Document
            {
                public JsonNode {|BNCQ1002:Root|} { get; set; }
            }

            public class Client
            {
                public Document Fetch() => null;
            }
            """);

    /// <summary>
    /// A framework type whose assembly never references System.Text.Json is opaque; its members
    /// are not walked, and the surface is clean.
    /// </summary>
    [Fact]
    public Task A_referenced_type_that_cannot_reach_a_covered_namespace_stays_silent() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            using System.Net.Http;
            using System.Threading.Tasks;

            public class Client
            {
                public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => null;
            }
            """);

    [Fact]
    public Task Members_outside_the_visible_surface_stay_silent() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            using System.Text.Json.Nodes;

            public class Document
            {
                internal JsonNode Root { get; set; }
                private JsonNode _cached;
                private protected JsonNode Read() => null;
                public string Text => JsonNode.Parse("{}").ToJsonString();
            }

            internal class Hidden
            {
                public JsonNode Root { get; set; }
            }
            """);

    /// <summary>
    /// The configured list replaces the default one: JsonNode is no longer covered, and the
    /// consumer's own namespace is.
    /// </summary>
    [Fact]
    public async Task The_editorconfig_list_replaces_the_default()
    {
        RuleTest<SurfaceLeakAnalyzer> test = new("""
            using System.Text.Json.Nodes;

            namespace Acme.Internal
            {
                public class Model { }
            }

            namespace Acme
            {
                public class Client
                {
                    public JsonNode Root { get; set; }
                    public Internal.Model {|BNCQ1002:Load|}() => null;
                }
            }
            """);
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true

            [*.cs]
            bennewitz_ninja_codequality.BNCQ1002.namespaces = Acme.Internal; Other.Place
            """));

        await test.Run();
    }

    /// <summary>A prefix covers the namespace and those beneath it, on a segment boundary only.</summary>
    [Fact]
    public async Task A_prefix_covers_nested_namespaces_but_not_a_longer_word()
    {
        RuleTest<SurfaceLeakAnalyzer> test = new("""
            namespace Acme.Internal.Deep
            {
                public class Model { }
            }

            namespace Acme.Internals
            {
                public class Model { }
            }

            namespace Acme
            {
                public class Client
                {
                    public Internal.Deep.Model {|BNCQ1002:Load|}() => null;
                    public Internals.Model Lookalike() => null;
                }
            }
            """);
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true

            [*.cs]
            bennewitz_ninja_codequality.BNCQ1002.namespaces = Acme.Internal
            """));

        await test.Run();
    }

    private const string LeakingDocument = """
        public class Document
        {
            public System.Text.Json.Nodes.JsonNode Root { get; set; }
        }
        """;

    /// <summary>An empty list forbids nothing, and the rule registers nothing.</summary>
    [Fact]
    public async Task An_empty_list_makes_the_rule_register_nothing()
    {
        ImmutableArray<MetadataReference> references = await Plain.Net80();

        Plain.Run inert = await Plain.Analyze(new SurfaceLeakAnalyzer(), LeakingDocument, references, """
            root = true

            [*.cs]
            bennewitz_ninja_codequality.BNCQ1002.namespaces =
            """);
        Plain.Run control = await Plain.Analyze(new SurfaceLeakAnalyzer(), LeakingDocument, references);

        Assert.Equal(0, inert.Registrations);
        Assert.Empty(inert.Diagnostics);
        Assert.True(control.Registrations > 0, "In scope, the rule registered nothing, so a zero proves nothing.");
        Assert.Single(control.Diagnostics);
    }

    /// <summary>
    /// With no covered namespace in reach, nothing is registered: the default list over a
    /// compilation that references neither JSON library has nothing it could match.
    /// </summary>
    [Fact]
    public async Task With_no_covered_namespace_in_reach_the_rule_registers_nothing()
    {
        Plain.Run inert = await Plain.Analyze(new SurfaceLeakAnalyzer(), LeakingDocument, await Plain.NetStandard20());
        Plain.Run control = await Plain.Analyze(new SurfaceLeakAnalyzer(), LeakingDocument, await Plain.Net80());

        Assert.Equal(0, inert.Registrations);
        Assert.Empty(inert.Diagnostics);
        Assert.True(control.Registrations > 0, "In scope, the rule registered nothing, so a zero proves nothing.");
        Assert.Single(control.Diagnostics);
    }

    [Fact]
    public Task Generated_code_is_not_analyzed() =>
        RuleTest<SurfaceLeakAnalyzer>.Run("""
            // <auto-generated/>
            using System.Text.Json.Nodes;

            public class Document
            {
                public JsonNode Root { get; set; }
            }
            """);
}
