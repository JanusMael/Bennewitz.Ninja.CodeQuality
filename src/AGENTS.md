# AGENTS.md — `src/`

The shipped projects. Everything here becomes a package, so everything here is permanent once
released.

<!-- bbpkg: list the projects under src/ and what each one ships -->

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| Every project is packable and its id is declared in `packages.push` or `packages.local` | An undeclared package is either published by accident or silently never published | `PackagingTests` |
| `src/Directory.Build.props` imports the root props explicitly | MSBuild applies only the closest `Directory.Build.props`. Without the import, every project here silently loses the root's target framework, nullable settings and package metadata | the import line itself; `TrimmableTests` fails if the file stops applying |
| `IsTrimmable` and `EnableTrimAnalyzer` stay on | `IsTrimmable` is compiled into the assembly and travels in the package; the analyser runs here or nowhere, because no consumer's build analyses a packaged library | `TrimmableTests` reads the mark off the compiled assembly |
| `IsAotCompatible` stays off | It would also switch on the AOT and single-file analysers, a claim about these libraries that nothing has measured | `src/Directory.Build.props`, comment |
| Assembly names are unprefixed; namespaces and package ids carry `Bennewitz.Ninja.` | The family's naming convention | the root `Directory.Build.props`, `RootNamespace` |

⚠ **The trim analyser cannot see compiled XAML.** A project with `.axaml` files needs an ILLink pass
as well; `src/Directory.Build.props` explains how.
