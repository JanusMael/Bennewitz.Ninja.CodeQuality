# AGENTS.md — `src/`

The shipped project. Everything here becomes the package, so everything here is permanent once
released.

| Path | What it is |
|---|---|
| `CodeQuality/CodeQuality.csproj` | The analyzer: `netstandard2.0`, `IsRoslynComponent`, `EnforceExtendedAnalyzerRules`, the DLL packed under `analyzers/dotnet/cs` with `IncludeBuildOutput` off, a `DevelopmentDependency`, trimming opted out with the reason, internals visible to the tests |
| `CodeQuality/ScopedAnalyzer.cs` | `ScopedAnalyzer<TScope>`, the base every rule derives from: a sealed `Initialize`, a gate that receives no context it could register on, and a registration hook that runs only in scope |
| `CodeQuality/Catalog.cs` | What every rule says about where it comes from: the category and the help link into the README |
| `CodeQuality/Surface.cs` | `IsVisibleOutside`: public or protected at every level, which is the surface the rules examine |
| `CodeQuality/Rules/NamespaceShadowAnalyzer.cs` | `BNCQ1004`: a namespace segment that shadows a referenced root namespace |
| `CodeQuality/Rules/SurfaceLeakAnalyzer.cs` | `BNCQ1002`: a type from a leak-prone namespace in the visible surface, walked transitively through referenced types |
| `CodeQuality/Rules/CancellationTokenAnalyzer.cs` | `BNCQ1001`: a defaulted cancellation token, or a token-less overload of a tokened sibling; off by default |
| `CodeQuality/AnalyzerReleases.Unshipped.md`, `AnalyzerReleases.Shipped.md` | Release tracking (RS2007, RS2008): every rule is listed in one of them |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| The project is packable and its id is declared in `packages.push` | An undeclared package is either published by accident or silently never published | `PackagingTests` |
| `src/Directory.Build.props` imports the root props explicitly | MSBuild applies only the closest `Directory.Build.props`. Without the import, the project silently loses the root's nullable settings, package metadata and AutoVersioning reference | the import line itself; `repo-conventions check --offline` evaluates the result |
| The analyzer's csproj sets `TargetFramework` to `netstandard2.0`, overriding the root's `net10.0` | The compiler inside Visual Studio's .NET Framework MSBuild cannot load a `net10.0` analyzer, and says so only as a warning most builds never show | `AnalyzerShapeTests` reads the target off the compiled assembly; `repo-conventions check` holds the analyzer role to `netstandard2.0` |
| The analyzer opts out of `IsTrimmable` and `EnableTrimAnalyzer` in its csproj, with the reason; a library added here gets both from `src/Directory.Build.props` | The SDK does not support either on `netstandard2.0`: it warns `NETSDK1212`, which CI's `-warnaserror` build makes an error. The compiler loads an analyzer, and no consumer publishes it trimmed | `CodeQuality.csproj`; `AnalyzerShapeTests` holds a library to the mark and an analyzer to its target |
| `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` are `PrivateAssets="all"`, and nothing else is referenced | An analyzer takes no package dependency at run time: a DLL not packed beside it fails to load as `AD0001`, and the rule goes quiet | `AnalyzerShapeTests` lists what the compiled assembly may reference |
| Every analyzer is `internal sealed` and derives from `ScopedAnalyzer<TScope>`; its scope type is `internal` too | The compiler runs every analyzer in the assembly in every compilation; the base is what makes each one decide for itself. A constructed base type may not be less accessible than the class deriving from it: a private scope type fails with `CS9338` | `AnalyzerConventionTests`; the compiler |
| Source stays within what `netstandard2.0` compiles without polyfills: no records, no `init`, no `required`, no nullable attributes such as `[NotNullWhen]` | The root props set `LangVersion` to `preview`, so the syntax is accepted and the missing runtime type is the error | the build |
| An extension block's receiver is read only through `SurfaceLeakAnalyzer.ExtensionBlock`, which reads the members Roslyn 4.14 declares on `ITypeSymbol` from a method that is never inlined | Those members are the older copy Roslyn keeps beside its newer `INamedTypeSymbol` pair. Read directly, a future compiler that dropped them would raise `AD0001` on every type; read there, the rule stops reading receivers instead | `SurfaceLeakAnalyzer.ExtensionBlock`, its remarks |
| Symbols compare through `SymbolEqualityComparer`, never `==` | RS1024; two symbols for one type from different compilations are not reference-equal | `Microsoft.CodeAnalysis.Analyzers` |
| A rule's gate does cheap work only: metadata-name lookups, top-level namespace walks, a read of the options | The gate runs at every compilation start in every project that references the package, in scope or not | review; the remarks on each analyzer say what its gate reads |

XML doc remarks follow the family's house style: one `<para>` per point, each opening with a marker
and a bold claim: ⭐ key idea, ⚠ caution, ⛔ trap. Explain *why*, and say "Measured:" when a claim
comes from an observed failure. Finding messages state the defect, why it bites, and every valid fix.
