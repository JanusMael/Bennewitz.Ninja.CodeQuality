# Bennewitz.Ninja.CodeQuality

Roslyn analyzers for the shape of a .NET library's source: a namespace segment that shadows a
referenced root namespace, an implementation type leaking into the public surface, and a
cancellation token a caller can leave out. Each rule is the compile-time counterpart of a
[Bennewitz.Ninja.AssemblyQuality](https://github.com/JanusMael/Bennewitz.Ninja.AssemblyQuality)
rule that reads the shipped assembly, and carries the same number: `BNCQ1004` finds while you type
what `BNAQ1004` finds after the build.

## Install

```bash
dotnet add package Bennewitz.Ninja.CodeQuality
```

A development dependency: the analyzer runs inside the compiler, ships nothing at run time, and is
not passed on to whoever references your package. It needs a compiler from Roslyn 4.14 on, which is
Visual Studio 2022 17.14 or a .NET 9.0.3xx SDK or later. An older compiler, such as the .NET 8
SDK's, reports `CS9057` and skips the analyzer: every rule goes quiet, and the build fails only
where warnings are errors.

## Rules

<!-- BEGIN GENERATED RULES -->
| Id | Default | Reports |
|---|---|---|
| [`BNCQ1001`](#bncq1001) | off | A visible method lets the caller leave the cancellation token out |
| [`BNCQ1002`](#bncq1002) | on | A type from a leak-prone namespace appears in the visible surface |
| [`BNCQ1004`](#bncq1004) | on | A namespace segment shadows the root namespace of a referenced assembly |
<!-- END GENERATED RULES -->

The table is rendered from the analyzers' own descriptors, and a test fails if it drifts.
`BNCQ1003` is reserved: its counterpart
[`BNAQ1003`](https://github.com/JanusMael/Bennewitz.Ninja.AssemblyQuality#bnaq1003) forbids a
reference, and a forbidden use site at compile time is already
[Microsoft.CodeAnalysis.BannedApiAnalyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers)'
job.

Where a rule comes from is shown by its help link (its section below), its category
(`Bennewitz.Ninja.CodeQuality`) and the package id, not by a longer ID. `BNCQ` is `BN` plus the
product's initials, the scheme across the Bennewitz.Ninja quality packages, and it will not change.

**Every rule decides once per compilation whether it applies**, and registers nothing where it
does not: a project the rule has nothing to compare against pays a few lookups at compilation
start and nothing per file. A build with no findings cannot tell that from "checked and clean",
which is why each rule's tests show it firing on a real violation as well as staying silent.

### BNCQ1001

**A visible method lets the caller leave the cancellation token out.** Off by default. Counterpart:
[`BNAQ1001`](https://github.com/JanusMael/Bennewitz.Ninja.AssemblyQuality#bnaq1001).

A `CancellationToken` parameter with a default value hands every caller who forgets it
`CancellationToken.None`: an operation that cannot be cancelled, chosen by nobody, visible nowhere.
The call compiles, runs, and hangs on shutdown. An overload that leaves the token out while a
same-named sibling takes it is the same default, moved one overload over, and is reported too:
when its parameters are the sibling's with the token removed, or a leading run of them.

The fix that holds is to require the token **ahead of any optional parameter**, so that a caller
who means `CancellationToken.None` has to write it. Dropping the token's default alone is CS1737
when an optional parameter follows it.

This is a policy, not a defect: the BCL defaults its own tokens everywhere, and a finding is not
evidence of a bug. It is the rule a library adopts when its cancellation contract is load-bearing.
Turn it on in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.BNCQ1001.severity = warning
```

Visible means public or protected at every level, so a protected method counts, and so does a
constructor; `BNAQ1001` reads neither. Compiler-generated members, accessors, operators and
explicit interface implementations are skipped: a caller cannot pass them a token by name. Inert in
a compilation that cannot name `CancellationToken`.

### BNCQ1002

**A type from a leak-prone namespace appears in the visible surface.** Counterpart:
[`BNAQ1002`](https://github.com/JanusMael/Bennewitz.Ninja.AssemblyQuality#bnaq1002).

Return a `JsonNode` from one public method and every consumer now binds against
`System.Text.Json`, its version, its behaviour and its breaking changes, whether or not they
serialize anything. Swapping the serializer later is then a breaking change to an API that was
never about serialization. The rule reads every visible member's signature, what a type derives
from and implements, and generic constraints. It unwraps generic arguments, including a containing
type's, and array elements, so a `Task<JsonNode>`, a `List<JsonNode>.Enumerator` and a
`where T : JsonNode` leak exactly as much as a bare `JsonNode`.

Unlike `BNAQ1002`, the walk is **transitive through referenced types**: a public `Envelope` from
another package whose `Payload` is a `JObject` binds the consumer to Newtonsoft one hop further,
and the message names the hop. A covered type the signature names itself is reported before any
longer path. Your own types are not opened, because their members are reported where they are
declared. A referenced type is opened only if its assembly can reach a covered namespace, directly
or through its own references, which keeps the walk off `Task<T>`, `List<T>` and most of the
framework.

A C# 14 `extension(...)` block exposes its receiver: calling `node.Touch()` binds the consumer to
`JsonNode` as surely as a classic `this JsonNode` parameter does. So a covered receiver, or a
covered constraint on a generic block, is reported at the block, and the members inside are checked
like any other.

The default namespaces are the ones that leak in practice: `System.Text.Json.Nodes` and
`Newtonsoft.Json.Linq`. The type most likely to leak from your library is one no general list
will name, so `.editorconfig` **replaces** the list with yours, comma or semicolon separated. A
prefix covers the namespace and everything beneath it on a segment boundary.

```ini
[*.cs]
bennewitz_ninja_codequality.BNCQ1002.namespaces = System.Text.Json.Nodes, Newtonsoft.Json.Linq, Acme.Internal
```

The list is read once per compilation, from a global config first and then from the first source
file whose options carry it, so it belongs in the repository's root `.editorconfig`. An empty list
forbids nothing. Inert where no covered namespace is in reach. A library whose surface is JSON on
purpose, such as a JSON editor, exposes these types by design: set the list to the namespaces it
does not mean to expose, or empty it.

### BNCQ1004

**A namespace segment shadows the root namespace of a referenced assembly.** Counterpart:
[`BNAQ1004`](https://github.com/JanusMael/Bennewitz.Ninja.AssemblyQuality#bnaq1004).

C# resolves the first identifier of a qualified name by walking outward from the current
namespace. Inside `Acme.Widgets.Avalonia`, the name `Avalonia` finds your namespace first, and
`Avalonia.Media.Color` fails to compile looking for `Acme.Widgets.Avalonia.Media.Color`: CS0234,
reported at the use site, about a type that plainly exists, possibly years after the namespace was
declared. The rule reports the segment itself, in every declaration that carries it.

Only segments after the first can shadow. A namespace's own root is the tree it already lives in,
so `Acme.Widgets` beside a referenced `Acme.Other` resolves perfectly. A root is a top-level
namespace a referenced assembly declares a type you can see in; a namespace holding only internal
types counts only where that assembly makes its internals visible to yours, and a reference behind
an `extern alias` counts only when one of its aliases is `global`.

Two fixes, not one. Where namespaces follow the assembly name, renaming only the namespace breaks
that convention, so the fix there renames assembly and namespace together, `.Avalonia` to
`.AvaloniaUI`. The package id takes no part in name resolution and may keep the word. Inert in a
compilation whose references declare no visible namespace.

## Releasing

See [docs/publishing.md](docs/publishing.md). The short version:

1. Add the `NUGET_USER` repository **variable**, not a secret: your nuget.org **profile name**,
   not an email.
2. Create **one** trusted-publishing policy whose glob patterns cover every id in
   [`packages.push`](packages.push) and match nothing in [`packages.local`](packages.local).
3. Run **Release** → *Run workflow* with the version **blank**. That logs in and stops, proving the
   credentials without publishing.
4. Tag `vYYYY.Q.MMDD` and push.

⛔ **One policy, never one per package id.** nuget.org mints one API key per token exchange, scoped
to one matching policy — so a second policy is never consulted and its package is rejected `403`
after the first has already published permanently.

## Licence

MIT. See [LICENSE](LICENSE).
