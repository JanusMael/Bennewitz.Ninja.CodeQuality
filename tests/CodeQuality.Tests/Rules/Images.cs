using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace CodeQuality.Tests.Rules;

/// <summary>
/// Compiles a source text to a real assembly image, as a package's DLL arrives in a consumer's
/// compilation: a PE reference whose metadata names only the assemblies it actually uses.
/// </summary>
/// <remarks>
/// ⚠ <b>A project reference in the testing framework is not this.</b> Its compilation carries every
/// reference the project was given, the whole .NET reference pack included, so it "references"
/// System.Text.Json whether it uses it or not. A rule that decides from what an assembly references
/// has to be tested against an image, where the compiler has already dropped what went unused.
/// </remarks>
internal static class Images
{
    public static async Task<Image> Compile(string name, string source, params MetadataReference[] references)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ImmutableArray<MetadataReference> framework = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, cancellationToken);

        CSharpCompilation compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
            [.. framework, .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream, cancellationToken: cancellationToken);
        Assert.True(result.Success, name + " did not compile: " + string.Join("; ", result.Diagnostics));

        byte[] bytes = stream.ToArray();
        return new Image(MetadataReference.CreateFromImage(bytes), ReferencedNames(bytes));
    }

    private static string[] ReferencedNames(byte[] image)
    {
        using PEReader reader = new(new MemoryStream(image));
        MetadataReader metadata = reader.GetMetadataReader();

        return [.. metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))];
    }

    /// <summary>A compiled assembly, and the names of the assemblies its metadata references.</summary>
    internal sealed record Image(MetadataReference Reference, string[] References);
}
