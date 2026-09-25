using System.Reflection;

namespace CodeQuality.Tests.Packaging;

/// <summary>
/// Guards that every shipped assembly carries the IsTrimmable mark, read off the compiled output.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>The mark travels inside the package</b>, as <c>[AssemblyMetadata("IsTrimmable", "True")]</c>,
/// and an app publishing with <c>TrimMode=partial</c> trims ONLY assemblies that carry it. Without it
/// a consumer's trimmed publish keeps the assembly whole and outside its trim analysis. Nothing fails,
/// so nothing would notice.
/// </para>
/// <para>
/// ⚠ Read from the DLL, never the project file: the property is inherited from
/// <c>src/Directory.Build.props</c>, so no csproj mentions it, and the DLL is what a consumer's publish
/// obeys. The first packages generated from this template shipped without the mark, which is why this
/// test is part of the template rather than something each repository remembers to add.
/// </para>
/// </remarks>
public sealed class TrimmableTests
{
    [Fact]
    public void Every_shipped_assembly_is_marked_trimmable()
    {
        string output = Path.GetDirectoryName(typeof(TrimmableTests).Assembly.Location)!;

        string[] projects =
        [
            .. Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension)
                .Select(name => name!),
        ];

        // ⛔ Without this, an empty src/ would pass every assertion below.
        Assert.NotEmpty(projects);

        List<string> unmarked = [];

        foreach (string project in projects)
        {
            string path = Path.Combine(output, project + ".dll");

            // A project missing from the output would otherwise be skipped rather than checked.
            Assert.True(File.Exists(path), $"{project}.dll is not in {output}, so its mark cannot be checked.");

            bool marked = Assembly.LoadFrom(path)
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Any(a => a.Key == "IsTrimmable"
                          && string.Equals(a.Value, "True", StringComparison.OrdinalIgnoreCase));

            if (!marked)
            {
                unmarked.Add(project);
            }
        }

        Assert.True(
            unmarked.Count == 0,
            "These shipped assemblies are not marked trimmable:\n  " + string.Join("\n  ", unmarked)
            + "\n\nAn app publishing with TrimMode=partial trims ONLY marked assemblies, so these would "
            + "ship whole and outside its trim analysis. Check src/Directory.Build.props.");
    }

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
