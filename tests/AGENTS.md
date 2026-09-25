# AGENTS.md — `tests/`

The test projects. `tests/Directory.Build.props` makes every project here an xUnit v3 test
executable that is never packed.

| Path | What it covers |
|---|---|
| `CodeQuality.Tests/AnalyzerConventionTests.cs` | Every analyzer derives from `ScopedAnalyzer<TScope>` and is sealed; the base seals `Initialize` and its gate receives nothing it could register on; the assembly exports no public type of its own |
| `CodeQuality.Tests/RulesCatalogTests.cs` | Every rule's ID, category and help link; its README section exists; the README rules table matches the descriptors |
| `CodeQuality.Tests/Rules/RuleTest.cs` | One analyzer run over one test compilation on the .NET 8 reference assemblies, with `{|BNCQ1004:span|}` markup |
| `CodeQuality.Tests/Rules/Plain.cs` | One analyzer over a plain compilation, configured from `.editorconfig` text as the compiler configures it, reporting its findings and how many actions it registered. For what the testing framework cannot show: a rule that is off until enabled, and a gate that stayed closed |
| `CodeQuality.Tests/Rules/Images.cs` | Compiles a source to a real assembly image, whose metadata references only what it uses, as a package's DLL does |
| `CodeQuality.Tests/Rules/*Tests.cs` | One class per rule: firing, silent, and inert outside its scope |
| `CodeQuality.Tests/Packaging/PackagingTests.cs` | Every packable project is classified in exactly one package list; the release workflow globs nothing |
| `CodeQuality.Tests/Packaging/AnalyzerShapeTests.cs` | Read off the compiled assembly: an analyzer targets `netstandard2.0` and references only what the compiler host provides; a library carries the trimmable mark. Read from the project file: an analyzer packs only itself, under `analyzers/dotnet/cs`, with no dependencies |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| Tests run on Microsoft.Testing.Platform | `dotnet test` takes `--solution` and rejects VSTest-only switches | `global.json`, `test.runner` |
| `tests/Directory.Build.props` imports the root props explicitly | Without it, every test project silently loses the root's target framework and nullable settings | the import line itself |
| The tests reference `Microsoft.CodeAnalysis.CSharp.Workspaces` at the analyzer's own pin | The testing package floors Workspaces at 1.0.1; without the reference the tests restore a 2015 Workspaces (NU1701) and prove nothing about the compiler the package supports | `CodeQuality.Tests.csproj`; `Directory.Packages.props`, `RoslynVersion` |
| Every rule is proved firing, silent and inert, and every firing test was watched failing with the gate returning null | A rule whose gate is wrong fails as silence, so a suite of silence tests passes against a rule that does nothing | the rule test classes; the adding-a-rule checklist in the root `AGENTS.md` |
| An inert test counts registrations through `Plain`, beside a control in scope where the count must not be zero, and was watched failing with the gate forced open | Silence is not inertness: every inert test written with `RuleTest` still passed with all three gates forced open, because the test code was quiet anyway | the `*_registers_nothing` tests in each rule's class |
| A disabled-by-default rule's opt-in is tested through `Plain`, not `RuleTest` | `CSharpAnalyzerTest` enables every diagnostic of the analyzer under test, so a rule that is off by default reads as on there | `CancellationTokenTests.Until_enabled_a_defaulted_token_is_not_reported` |
| A rule that decides from what an assembly references is tested against `Images`, not a project reference | A project reference in the testing framework carries the whole reference pack, so it "references" everything | `SurfaceLeakTests.A_leak_two_packages_away_fires` |
| In a `RuleTest` with additional projects, the analyzer runs over every project, so a leak in the referenced project is expected there too | An expectation written for the primary project alone fails on the extra diagnostic | `SurfaceLeakTests.A_referenced_type_reaching_a_covered_type_fires_and_names_the_path` |
| The tests under `Packaging/` stay as strong as they are | They are the only thing standing between a packaging mistake and a permanent release | `PackagingTests`, `AnalyzerShapeTests` |

⛔ **Never weaken a packaging or convention test to make it pass.** When one fails, the project,
the analyzer or the package list is wrong, not the test.
