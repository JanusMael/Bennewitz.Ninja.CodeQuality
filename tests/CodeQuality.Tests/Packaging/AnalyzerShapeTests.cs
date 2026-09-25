using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace CodeQuality.Tests.Packaging;

/// <summary>
/// Guards that every shipped assembly has the shape its role requires, read off the compiled
/// output: an analyzer targets netstandard2.0 and references only what the compiler host loads
/// beside it; a library carries the IsTrimmable mark.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>An analyzer built for the wrong framework fails silently.</b> The dotnet CLI's compiler
/// loads a net10.0 analyzer without complaint; Visual Studio's .NET Framework MSBuild cannot, and
/// reports it as a warning most builds never show. netstandard2.0 is the one target both load,
/// and the mark is compiled into the assembly, so it is read from there.
/// </para>
/// <para>
/// ⛔ <b>An analyzer cannot load a DLL that is not packed beside it.</b> The package carries the
/// analyzer alone under <c>analyzers/dotnet/cs</c>, and a package reference it took would be
/// nowhere near it at run time: the compiler reports <c>AD0001</c> and the rule goes quiet. So the
/// compiled assembly may reference only what the compiler host itself provides. The list below is
/// deliberately short; grow it only for an assembly the host is known to ship.
/// </para>
/// <para>
/// ⚠ <b>The trimmable mark travels inside a library's package</b>, as
/// <c>[AssemblyMetadata("IsTrimmable", "True")]</c>, and an app publishing with
/// <c>TrimMode=partial</c> trims ONLY assemblies that carry it. The SDK does not support the mark
/// on netstandard2.0 (NETSDK1212), so an analyzer is not held to it; <c>repo-conventions check</c>
/// derives the same two roles and holds only a library to trimming.
/// </para>
/// </remarks>
public sealed class AnalyzerShapeTests
{
    /// <summary>What the compiler host loads beside an analyzer, by simple assembly name.</summary>
    private static readonly string[] HostProvided =
    [
        "netstandard",
        "Microsoft.CodeAnalysis",
        "Microsoft.CodeAnalysis.CSharp",
        "System.Collections.Immutable",
    ];

    [Fact]
    public void Every_shipped_assembly_has_the_shape_its_role_requires()
    {
        string output = Path.GetDirectoryName(typeof(AnalyzerShapeTests).Assembly.Location)!;
        string[] projects = [.. Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.csproj", SearchOption.AllDirectories)];

        // ⛔ Without this, an empty src/ would pass every assertion below.
        Assert.NotEmpty(projects);

        List<string> problems = [];

        foreach (string project in projects)
        {
            string name = Path.GetFileNameWithoutExtension(project);
            string path = Path.Combine(output, name + ".dll");

            // A project missing from the output would otherwise be skipped rather than checked.
            Assert.True(File.Exists(path), $"{name}.dll is not in {output}, so its shape cannot be checked.");

            Assembly assembly = Assembly.LoadFrom(path);

            if (IsAnalyzer(project))
            {
                string? framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
                if (framework != ".NETStandard,Version=v2.0")
                {
                    problems.Add($"{name} targets '{framework}', and an analyzer must target netstandard2.0 to load in every compiler host.");
                }

                string[] foreign = [.. assembly.GetReferencedAssemblies().Select(reference => reference.Name!).Except(HostProvided).Order()];
                if (foreign.Length > 0)
                {
                    problems.Add($"{name} references {string.Join(", ", foreign)}, which the compiler host does not load beside an analyzer (AD0001 at the consumer).");
                }
            }
            else if (!IsMarkedTrimmable(assembly))
            {
                problems.Add($"{name} is not marked trimmable; an app publishing with TrimMode=partial would ship it whole. Check src/Directory.Build.props.");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>
    /// An analyzer's project packs its DLL under <c>analyzers/dotnet/cs</c> and nowhere else, and
    /// declares no dependencies. Read from the project file, so the guard runs before anything is
    /// packed.
    /// </summary>
    /// <remarks>
    /// ⛔ Under <c>lib/</c> a consumer gets a runtime reference and the compiler never loads the
    /// analyzer. A declared dependency is restored for the consumer and is still nowhere near the
    /// analyzer when the compiler loads it. The consumer builds recorded in <c>PROGRESS.md</c> proved
    /// that this layout loads; this keeps the declarations that produce it.
    /// </remarks>
    [Fact]
    public void Every_analyzer_packs_only_itself_under_analyzers_dotnet_cs()
    {
        string[] analyzers = [.. Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.csproj", SearchOption.AllDirectories).Where(IsAnalyzer)];

        // ⛔ Without this, a repository whose analyzer lost IsRoslynComponent would pass.
        Assert.NotEmpty(analyzers);

        foreach (string project in analyzers)
        {
            XDocument document = XDocument.Load(project);
            string name = Path.GetFileNameWithoutExtension(project);

            Assert.True(Property(document, "IncludeBuildOutput") == "false", name + " packs its build output under lib/, where the compiler never loads an analyzer.");
            Assert.True(Property(document, "SuppressDependenciesWhenPacking") == "true", name + " declares package dependencies, which an analyzer cannot load.");
            Assert.True(Property(document, "DevelopmentDependency") == "true", name + " is not a development dependency, so it flows to whoever references the consumer.");
            Assert.True(
                document.Descendants("None").Any(item =>
                    (string?)item.Attribute("Pack") == "true"
                    && (string?)item.Attribute("PackagePath") == "analyzers/dotnet/cs"
                    && ((string?)item.Attribute("Include"))?.EndsWith("$(AssemblyName).dll", StringComparison.Ordinal) == true),
                name + " does not pack its own DLL under analyzers/dotnet/cs, where the compiler looks for it.");
        }
    }

    private static string? Property(XDocument document, string name) =>
        document.Descendants(name).LastOrDefault()?.Value.Trim().ToLowerInvariant();

    /// <summary>
    /// The role the family conventions derive: <c>IsRoslynComponent</c> makes a project an analyzer.
    /// Read from the project file, as <c>PackagingTests</c> reads <c>IsPackable</c>.
    /// </summary>
    private static bool IsAnalyzer(string project) =>
        File.ReadLines(project).Any(line => line.Contains("<IsRoslynComponent>true</IsRoslynComponent>", StringComparison.OrdinalIgnoreCase));

    private static bool IsMarkedTrimmable(Assembly assembly) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(a => a.Key == "IsTrimmable" && string.Equals(a.Value, "True", StringComparison.OrdinalIgnoreCase));

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.EnumerateFiles(directory.FullName, "*.slnx").Any())
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
