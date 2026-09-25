# AGENTS.md — Bennewitz.Ninja.CodeQuality

> For anyone changing this repository, human or agent. The invariants a change must not break, the
> commands, and the checklists for recurring work. Each top-level directory has an `AGENTS.md` of
> its own for what only its files show. Work state is [`PROGRESS.md`](PROGRESS.md). What every
> repository in this family carries, and how it is checked, is prescribed in
> [`docs/repository-conventions.md`](https://github.com/JanusMael/Bennewitz.Ninja.Templates/blob/main/docs/repository-conventions.md)
> in Bennewitz.Ninja.Templates.

## What this repository is

`Bennewitz.Ninja.CodeQuality`: Roslyn analyzers for the shape of a .NET library's source, each the
compile-time counterpart of a Bennewitz.Ninja.AssemblyQuality rule that reads the shipped assembly,
with the same number. It ships one package, `Bennewitz.Ninja.CodeQuality`, consumed as a
development dependency by any C# project whose compiler is Roslyn `RoslynVersion` (in
`Directory.Packages.props`) or later. [README.md](README.md) describes each rule for a consumer;
this file covers how to change them.

## Layout

| Directory | What it holds |
|---|---|
| `src/` | `CodeQuality`, the analyzer assembly: the base every rule derives from, one analyzer per rule, and the release-tracking files. File by file in [src/AGENTS.md](src/AGENTS.md) |
| `tests/` | `CodeQuality.Tests`: the convention, shape, catalog and packaging guards, and one test class per rule |
| `scripts/` | File-based apps: `assert-packages.cs` and `repo-conventions.cs` |
| `docs/` | `publishing.md`, the release runbook |
| `.github/` | The workflows, `repository.json`, and the pointer for tools that read `.github/` |

## Invariants

| Invariant | Failure if broken | Guarded by |
|---|---|---|
| Every analyzer derives from `ScopedAnalyzer<TScope>`, whose gate cannot register actions | The compiler loads the assembly whole, so one analyzer that registers unconditionally runs in every compilation that references the package | `AnalyzerConventionTests` |
| The analyzer targets `netstandard2.0` and its compiled assembly references only what the compiler host loads beside it | A `net10.0` analyzer fails to load in Visual Studio's MSBuild; a package dependency is not packed beside the DLL and fails as `AD0001` | `AnalyzerShapeTests`; `repo-conventions check`, analyzer role |
| The DLL is packed under `analyzers/dotnet/cs` with `IncludeBuildOutput` off and no dependencies, as a `DevelopmentDependency` | Under `lib/` a consumer gets a runtime reference and the compiler never loads the analyzer | `AnalyzerShapeTests.Every_analyzer_packs_only_itself_under_analyzers_dotnet_cs`; the consumer builds under "Measured before the first release" in `PROGRESS.md` |
| `Microsoft.CodeAnalysis.CSharp` is pinned to the oldest compiler the package supports, `RoslynVersion` in `Directory.Packages.props`, and the tests resolve the same version | An API newer than the pin loads in no older compiler; a test on a newer Roslyn proves nothing about the pin | `Directory.Packages.props`; `CodeQuality.Tests.csproj`, the Workspaces reference |
| Every rule ID is `BNCQ` plus four digits, its category is `Bennewitz.Ninja.CodeQuality`, its help link is its README section, and the README rules table is generated from the descriptors | A consumer's suppressions name the ID; a help link to a missing section is a 404 on the rule they were just told about | `RulesCatalogTests` |
| Every rule is recorded in `AnalyzerReleases.Unshipped.md`, and moved to `AnalyzerReleases.Shipped.md` under its release in the same change as the tag | RS2000 and RS2008 fail the build; a release that leaves the shipped file empty has no record of what it shipped | `Microsoft.CodeAnalysis.Analyzers`, release tracking; the releasing checklist |
| Every rule is tested both ways: firing on a real violation, silent on clean code, and inert outside its scope, which is measured as registered actions and not as silence | A rule whose gate is wrong fails as silence, and a clean build cannot tell inert from checked | The rule test classes under `tests/CodeQuality.Tests/Rules/` |
| The analyzer assembly exports no public type of its own | A public type is API to keep compatible for no reader; a consumer configures the package through `.editorconfig` | `AnalyzerConventionTests` |
| Every packable project's id is in exactly one of `packages.push` and `packages.local` | A package nobody chose is published, permanently | `PackagingTests`; `scripts/assert-packages.cs`; the release step `Assert packed matches declared` |
| The release pushes the ids `packages.push` names and never globs `*.nupkg` | A new packable project is published by the next tag | `.github/workflows/release.yml`, step `Push to NuGet.org` |
| Packages resolve from nuget.org only | A second source added later silently starts supplying packages | `NuGet.config`, `packageSourceMapping` |
| Versions are pinned centrally | Two projects drift to different versions of one dependency | `Directory.Packages.props` |
| Warnings are errors | A warning ships | `Directory.Build.props`; `ci.yml` builds with `-warnaserror` |
| The version is the tag, `vYYYY.Q.MMDD` | The package and the tag disagree. A published version can never be replaced | `release.yml`, step `Resolve version and tag` |
| `NUGET_USER` is a repository **variable**, not a secret | A masked value hides why a trusted-publishing login fails | `release.yml`, step `Refuse to release without NUGET_USER` |
| The repository meets the family conventions | Documentation or settings go missing unnoticed | `scripts/repo-conventions.cs`, run by CI |

## Commands

```bash
dotnet build CodeQuality.slnx -c Release -warnaserror
dotnet test --solution CodeQuality.slnx
dotnet pack CodeQuality.slnx -c Release --output ./packages/Release
dotnet run scripts/assert-packages.cs -- ./packages/Release
dotnet run --file scripts/repo-conventions.cs -- check
```

- Tests run on Microsoft.Testing.Platform (`global.json`), so `dotnet test` takes `--solution` and
  rejects VSTest-only switches such as `--nologo`.
- Write `-p:` rather than `/p:`: Git Bash on Windows rewrites a leading-slash argument into a path.
- The rule tests resolve the .NET reference assemblies from nuget.org on first run and cache them.
- `BNCQ_UPDATE_DOCS=1 dotnet test --solution CodeQuality.slnx` regenerates the README rules table.

## Checklists

**Adding a rule**
1. Its number is its `BNAQ` counterpart's, or the next free number for a rule with no counterpart.
   `BNCQ1003` stays reserved. The code is written here: analyzers the maintainer wrote for another
   project may be read for ideas and traps, but none of their code, names, IDs or links is copied.
2. `src/CodeQuality/Rules/<Name>Analyzer.cs`, internal and sealed, deriving from
   `ScopedAnalyzer<TScope>`. The gate returns null wherever the rule has nothing to compare against.
   The descriptor takes its category and help link from `Catalog`.
3. Its line in `src/CodeQuality/AnalyzerReleases.Unshipped.md`.
4. `tests/CodeQuality.Tests/Rules/<Name>Tests.cs`: firing on a violation, silent on clean code,
   inert outside its scope. Inert is a count of registered actions through `Plain`, zero beside a
   control in scope that is not. Make the gate return null and watch the firing tests fail; force
   it open and watch the inert test fail; then put the gate back. A test that never failed proves
   nothing.
5. Its `### BNCQ####` section in `README.md`, linking the counterpart; regenerate the table.
6. Its entry in `PROGRESS.md`, and a message to the AssemblyQuality session so the counterpart's
   README links back.

**Raising the Roslyn pin:** only for an API the rules cannot do without. Measure the lowest version
that builds and passes the tests, set `RoslynVersion`, and change the compiler named in
`README.md`'s install section in the same change.

**Adding a package**
1. Add the project under `src/`.
2. Add its id to `packages.push`, or to `packages.local` with the reason as a comment above it.
3. Widen the trusted-publishing policy's glob on nuget.org to cover a new `packages.push` id; see
   `docs/publishing.md`.
4. Add it to the package table in `README.md`, and record it in `PROGRESS.md`.

**Adding a top-level directory:** give it an `AGENTS.md` and a `CLAUDE.md` containing `@AGENTS.md`,
or exempt it in `.github/repository.json` under `undocumented`, with the reason. CI fails until
one of the two is done.

**Releasing:** `docs/publishing.md`, and in the same change as the tag, move the rules from
`AnalyzerReleases.Unshipped.md` into `AnalyzerReleases.Shipped.md` under `## Release <version>`.

**Avalonia and drivable-UI lessons go to XamlQuality.** `docs/avalonia-gotchas.md` and
`docs/ai-drivable-ui.md` in
[JanusMael/Bennewitz.Ninja.XamlQuality](https://github.com/JanusMael/Bennewitz.Ninja.XamlQuality)
are the one living copy of each. Send a new finding or a correction to the XamlQuality session by
message, with the versions and the measurement or source behind it, or open an issue there when no
session is running. Keep no copy here.

**Every change:** update `PROGRESS.md` in the same commit. Commits are Conventional Commits.
