# Progress

Work state for Bennewitz.Ninja.CodeQuality, updated in the same change as the work it describes. What has stopped
changing moves out rather than piling up.

## Published

Nothing on nuget.org yet. The GitHub repository has been public since 2026-09-25, with the family
settings applied: `check --admin` conforms, and CI is green on every job of `1b6af59`.

## On `main`, not yet released

| Change | Commit |
|---|---|
| The repository, generated with `dotnet new bbpkg -n CodeQuality --RepoOwner JanusMael` from Bennewitz.Ninja.Templates `2026.3.925` | `e457b5d` |
| The scaffold becomes an analyzer: `netstandard2.0`, `IsRoslynComponent`, the DLL packed under `analyzers/dotnet/cs` with no `lib/` and no dependencies, a development dependency, trimming opted out with the reason; `ScopedAnalyzer<TScope>` and `AnalyzerConventionTests`; `AnalyzerShapeTests` in place of `TrimmableTests`; the tests run on the Roslyn pin; release tracking from the first release; `Directory.Build.targets` takes `PublicVersion` from the template's `main` (Templates `e6b7930`, after `2026.3.925`) | `af80b7b` |
| `BNCQ1004`: a namespace segment that shadows a referenced root namespace | `af80b7b` |
| `BNCQ1001`: a defaulted cancellation token, or a token-less overload of a tokened sibling, on a visible method or constructor; off by default | `af80b7b` |
| `BNCQ1002`: a leak-prone type in the visible surface, walked transitively through referenced types | `af80b7b` |
| `BNCQ1002` also sees a covered namespace declared only in a reference behind an extern alias; before, the rule stayed inert there. The twin of the alias defect fixed in `BNCQ1004` | `c8ce144` |

## Decisions

Settled with the maintainer on 2026-09-25, first recorded in Bennewitz.Ninja.AssemblyQuality's
`PROGRESS.md`, except where a row says otherwise.

| Decision | Choice | Why |
|---|---|---|
| Scope | Roslyn analyzers only. Code generators go elsewhere, such as beside Bennewitz.Ninja.AutoVersioning | One kind of package, one kind of test |
| Source | A clean-room rewrite. Analyzers the maintainer wrote for another project were read for ideas, patterns and the traps their documentation records; no code, names, IDs or links were carried over | |
| Base class | `ScopedAnalyzer<TScope>`, internal: a sealed `Initialize` that enables concurrent execution and sets generated-code handling, a gate that returns null to stay inert and receives nothing it could register on, and a hook that registers only in scope. `AnalyzerConventionTests` fails on any analyzer that does not derive from it | The compiler loads the assembly whole and runs every analyzer in it in every compilation. Generic, because every analyzer here is internal, so its scope type can be internal too and reaches the hook without a cast; a public analyzer would force its scope type public |
| Scaffolding | Generated with `bbpkg`, adapted to an analyzer, released, and only then upstreamed to Bennewitz.Ninja.Templates as a third template beside `bbpkg` and `bbavalonia` | The shape is proved by a release before it is copied |
| First rules | Source-side counterparts of `BNAQ1004`, `BNAQ1001` (opt-in: a policy, not a defect) and `BNAQ1002` (with a transitive member walk). Not `BNAQ1003` | A forbidden use site at compile time is `Microsoft.CodeAnalysis.BannedApiAnalyzers`' job already |
| Rule IDs | Prefix `BNCQ`: `BN` plus the product's initials, the family scheme. Once one ID ships, the prefix never changes. Each rule's README section links its `BNAQ` counterpart, and AssemblyQuality's README links back | One flat namespace of IDs across every analyzer a project loads |
| Rule numbers | **Mirror `BNAQ`**: `BNCQ1004` is `BNAQ1004`'s counterpart. `BNCQ1003` is reserved. Decided by the maintainer in this repository's first session | A consumer who knows one rule knows the other |
| Where a rule comes from | `helpLinkUri` (its README section), `Category` `Bennewitz.Ninja.CodeQuality`, and the package id, not a longer ID | |
| Roslyn pin | **`4.14.0`**, matching AutoVersioning. Decided by the maintainer in this repository's first session | Measured: the rules build and pass on `4.0.1`, `4.4.0`, `4.8.0` and `4.14.0`; `3.11.0` cannot compile them (`SyntaxKind.FileScopedNamespaceDeclaration` is Roslyn 4.0). A consumer needs Visual Studio 2022 17.14 or a .NET 9.0.3xx SDK. Measured on SDK `8.0.425` (Roslyn 4.11): `CS9057`, the analyzer is skipped, and the build succeeds |
| Trimming | The analyzer's csproj sets `IsTrimmable` and `EnableTrimAnalyzer` false | The SDK warns `NETSDK1212` for both on `netstandard2.0`, and CI builds with `-warnaserror`; `repo-conventions` holds only libraries to trimming |
| Tests on the pin | The tests reference `Microsoft.CodeAnalysis.CSharp.Workspaces` at `RoslynVersion` | The testing package floors it at `1.0.1`; without the pin the restore fails with `NU1701` |
| No package dependencies | `SuppressDependenciesWhenPacking` | Every reference is build-only, so pack wrote an empty dependency group and failed with `NU5128`; an analyzer cannot load a package dependency anyway |
| Release tracking | `AnalyzerReleases.Unshipped.md` and `AnalyzerReleases.Shipped.md` from the first release | AutoVersioning left its shipped file empty across three releases |

## Measured before the first release

On `af80b7b`, after an independent verification pass that found five rule defects, each fixed with
a test that failed first.

| What | Result |
|---|---|
| The rules against older Roslyn | Build and pass on `4.0.1`, `4.4.0`, `4.8.0` and `4.14.0`; `3.11.0` cannot compile them |
| The compiler each installed SDK ships | `5.0.408`: 3.11; `6.0.428`: 4.3; `7.0.410`: 4.7; `8.0.425`: 4.11; `9.0.318`: 4.14; `10.0.401`: 5.9 |
| A consumer of the packed package | SDK `10.0.401` and `9.0.318`: all three rules fire. SDK `8.0.425`: `CS9057`, the analyzer is skipped, and the build succeeds |
| Seven family repositories, copied, with the analyzer injected into every project and `BNCQ1001` enabled | AssemblyQuality: 8 findings, all in its own `BNAQ` fixtures; `Orphaned.cs` is a shadow its reflection rule is documented as unable to see. DiffView: 2 `BNCQ1001`, `DiffDocumentBuilder.Build` and `DiffSearch.Find`. JsonC: 2 `BNCQ1002`, `JsonNode` in a JSON editor's API by design. XamlQuality, AppServices, ScopedEditors and FileServer: none, with the analyzer confirmed in every project. No `AD0001` anywhere. DiffView's build stopped at a test project's step that clones reference sources, on a path too long for the copy, after all five of its projects had compiled; GeoHash was not built, because it needs a package that is not on nuget.org |

## Next

1. **Trusted publishing**, the maintainer's to set up because it is account-level: the `NUGET_USER`
   repository variable and the one nuget.org policy, then **Release**, *Run workflow*, with the
   version blank, which proves the credentials without publishing.
   [docs/publishing.md](docs/publishing.md) is the runbook.
2. **The first release**, only on the maintainer's explicit go. In the same change as the tag, move
   the three rules from `AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md` under
   `## Release <version>`. Verify against the feed, not the workflow.
3. After the release, **AssemblyQuality links back**: its README's `BNAQ1001`, `BNAQ1002` and
   `BNAQ1004` gain links to this README's sections. A message to the AssemblyQuality session, or an
   issue in `JanusMael/Bennewitz.Ninja.AssemblyQuality` when none is running.
4. After the release, **upstream the analyzer shape** to Bennewitz.Ninja.Templates as a third
   template: the diff from the scaffold commit to the release tag is the measured difference from
   `bbpkg`. A message to the Templates session, or an issue in `JanusMael/Bennewitz.Ninja.Templates`.
   What it holds, for that message:
   - `CodeQuality.csproj`: `netstandard2.0`, `IsRoslynComponent`, `EnforceExtendedAnalyzerRules`,
     `IncludeBuildOutput` off and the DLL packed under `analyzers/dotnet/cs`, `DevelopmentDependency`,
     `SuppressDependenciesWhenPacking` (`NU5128`), trimming off (`NETSDK1212`), the release-tracking
     files, `InternalsVisibleTo` the tests.
   - `Directory.Packages.props`: a `RoslynVersion` property for `Microsoft.CodeAnalysis.CSharp` and
     `Microsoft.CodeAnalysis.CSharp.Workspaces` (`NU1701` without the second), plus
     `Microsoft.CodeAnalysis.Analyzers` and `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`.
   - `TrimmableTests` becomes the role-aware `AnalyzerShapeTests`, which also reads the packing
     declarations; `AnalyzerConventionTests` and `RulesCatalogTests` are new. So are the rule-test
     helpers: `Plain`, which proves inertness by counting registered actions, since a silent test
     passes against a gate that never closes, and `Images`, for rules that read an assembly's
     references.
   - AutoVersioning generates a public `DirectoryBuildInfo` into the analyzer assembly, so the
     no-public-type test excludes its namespace.
   - A defect in `bbpkg` itself, whatever the third template becomes: its `src/Directory.Build.props`
     says `PackageMetadataTests` reads the trimmable mark, and no such test exists; the template's
     test is `TrimmableTests`.
5. **C# 14 extension blocks**, once the Roslyn pin reaches 5.x: `BNCQ1002` does not read the
   receiver of an `extension(...)` block, because Roslyn 4.14 has no API for it. The README and the
   analyzer's remarks say so.
