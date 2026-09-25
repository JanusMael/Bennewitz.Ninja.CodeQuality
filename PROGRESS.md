# Progress

Work state for Bennewitz.Ninja.CodeQuality, updated in the same change as the work it describes. What has stopped
changing moves out rather than piling up.

## Published

| Version | Released | Verified from the feed |
|---|---|---|
| `2026.3.925` | 2026-09-25: tag `v2026.3.925` on `bed0f7c`, release run `36193678454`, after a credential preflight and green CI on that commit | The flat container lists it. The DLL nuget.org serves is byte-identical to the GitHub release asset's. A consumer restoring from nuget.org alone, on SDK `10.0.401`, gets `BNCQ1001`, `BNCQ1002` and `BNCQ1004`. Each rule's help link resolves to its README section on GitHub |

The repository is public, with the family settings applied: `check --admin` conforms.

What `2026.3.925` holds:

| Change | Commit |
|---|---|
| The repository, generated with `dotnet new bbpkg -n CodeQuality --RepoOwner JanusMael` from Bennewitz.Ninja.Templates `2026.3.925` | `e457b5d` |
| The scaffold becomes an analyzer: `netstandard2.0`, `IsRoslynComponent`, the DLL packed under `analyzers/dotnet/cs` with no `lib/` and no dependencies, a development dependency, trimming opted out with the reason; `ScopedAnalyzer<TScope>` and `AnalyzerConventionTests`; `AnalyzerShapeTests` in place of `TrimmableTests`; the tests run on the Roslyn pin; release tracking from the first release; `Directory.Build.targets` takes `PublicVersion` from the template's `main` (Templates `e6b7930`, after `2026.3.925`) | `af80b7b` |
| `BNCQ1004`: a namespace segment that shadows a referenced root namespace | `af80b7b` |
| `BNCQ1001`: a defaulted cancellation token, or a token-less overload of a tokened sibling, on a visible method or constructor; off by default | `af80b7b` |
| `BNCQ1002`: a leak-prone type in the visible surface, walked transitively through referenced types | `af80b7b` |
| `BNCQ1002` also sees a covered namespace declared only in a reference behind an extern alias; before, the rule stayed inert there. The twin of the alias defect fixed in `BNCQ1004` | `c8ce144` |
| The three rules recorded under `## Release 2026.3.925` in `AnalyzerReleases.Shipped.md` | `bed0f7c` |

## On `main`, not yet released

| Change | Commit |
|---|---|
| README: each counterpart link points at its own section in AssemblyQuality's README, `#bnaq1001`, `#bnaq1002`, `#bnaq1003` and `#bnaq1004`, instead of `#rules`. Documentation only: the nuget.org README keeps `#rules` until the next release | `95ae6ee` |
| `BNCQ1002` reads the receiver of a C# 14 `extension(...)` block and reports a covered one at the block. Measured: the shipped 4.14 build does this inside the .NET 10 compiler, on released C# 14, with `BNCQ1001` firing once in the block and nothing reported twice. The rules now need Roslyn 4.14, the pin. The README, the analyzer's remarks and this file had said 4.14 had no API for it, which was never checked | `f938ff3` |

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
| Roslyn pin | **`4.14.0`**, matching AutoVersioning. Decided by the maintainer in this repository's first session | Since `BNCQ1002` reads extension-block receivers, 4.14 is also the oldest the rules can use: the members it reads first appear there, and `4.13.0` cannot compile them. Before that change they built and passed on `4.0.1`, `4.4.0`, `4.8.0` and `4.14.0`, and `3.11.0` could not compile them. The suite also passes against `5.9.0`. A consumer needs Visual Studio 2022 17.14 or a .NET 9.0.3xx SDK. Measured on SDK `8.0.425` (Roslyn 4.11): `CS9057`, the analyzer is skipped, and the build succeeds |
| Extension blocks in one build | Read through the members Roslyn 4.14 declares on `ITypeSymbol`, not a second build for Roslyn 5. Decided by the maintainer on 2026-09-25 | Measured: those members survive in 5.9 beside the new `INamedTypeSymbol` pair, and the shipped 4.14 build reads receivers correctly inside the .NET 10 compiler. Dual `roslynX.Y` folders, measured working with CommunityToolkit.Mvvm on SDKs 9 and 10, are for an API the pin lacks entirely |
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

## Waiting on others

Sent on 2026-09-25, at the maintainer's word, to the session that owns the repository. The change
is theirs to make; nothing here waits on it. The AssemblyQuality link-back, sent the same way, is
done: its `2c40de6` gives each rule a README section that links its counterpart, and a test there
fails if a rule loses its heading.

| Sent to | What |
|---|---|
| The Templates session | The measured differences from `bbpkg` for a third template: the diff `e457b5d..v2026.3.925`, with what each change is for. Also: `bbpkg`'s `src/Directory.Build.props` names a `PackageMetadataTests` that does not exist; and `verify-release` uninstalls the developer's `Bennewitz.Ninja.Templates` registration. Acknowledged the same day: that session's own scripts had wiped the registration on this machine; `verify-release` moves to a custom hive and the comment is fixed under its `plans/00005` step 5, pull request #8; the analyzer template is a candidate for a later plan |

## Next

1. **The next release**, `2026.3.926` at the earliest, carrying the table above. The maintainer
   approved shipping it on 2026-09-25; only the version rule holds it, since one version per day
   and `2026.3.925` is taken. The same steps as the first: move nothing in the release-tracking
   files, because no rule is new; CI green on the commit; tag; verify from the feed.
