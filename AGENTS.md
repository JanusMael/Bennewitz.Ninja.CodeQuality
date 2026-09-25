# AGENTS.md — Bennewitz.Ninja.CodeQuality

> For anyone changing this repository, human or agent. The invariants a change must not break, the
> commands, and the checklists for recurring work. Each top-level directory has an `AGENTS.md` of
> its own for what only its files show. Work state is [`PROGRESS.md`](PROGRESS.md). What every
> repository in this family carries, and how it is checked, is prescribed in
> [`docs/repository-conventions.md`](https://github.com/JanusMael/Bennewitz.Ninja.Templates/blob/main/docs/repository-conventions.md)
> in Bennewitz.Ninja.Templates.

## What this repository is

<!-- bbpkg: describe what this repository is for, which packages it ships, and who consumes them -->

## Layout

| Directory | What it holds |
|---|---|
| `src/` | The shipped projects. Every one is packable and declared in a package list |
| `tests/` | The test projects, including the packaging guards |
| `scripts/` | File-based apps: `assert-packages.cs` and `repo-conventions.cs` |
| `docs/` | `publishing.md`, the release runbook |
| `.github/` | The workflows, `repository.json`, and the pointer for tools that read `.github/` |

## Invariants

| Invariant | Failure if broken | Guarded by |
|---|---|---|
| Every packable project's id is in exactly one of `packages.push` and `packages.local` | A package nobody chose is published, permanently | `PackagingTests`; `scripts/assert-packages.cs`; the release step `Assert packed matches declared` |
| The release pushes the ids `packages.push` names and never globs `*.nupkg` | A new packable project is published by the next tag | `.github/workflows/release.yml`, step `Push to NuGet.org` |
| Every shipped assembly declares itself trimmable | A consumer's trimmed publish keeps it whole and outside its trim analysis, and says nothing | `src/Directory.Build.props`; `TrimmableTests` |
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

## Checklists

**Adding a package**
1. Add the project under `src/`.
2. Add its id to `packages.push`, or to `packages.local` with the reason as a comment above it.
3. Widen the trusted-publishing policy's glob on nuget.org to cover a new `packages.push` id; see
   `docs/publishing.md`.
4. Add it to the package table in `README.md`, and record it in `PROGRESS.md`.

**Adding a top-level directory:** give it an `AGENTS.md` and a `CLAUDE.md` containing `@AGENTS.md`,
or exempt it in `.github/repository.json` under `undocumented`, with the reason. CI fails until
one of the two is done.

**Releasing:** `docs/publishing.md`.

**Avalonia and drivable-UI lessons go to XamlQuality.** `docs/avalonia-gotchas.md` and
`docs/ai-drivable-ui.md` in
[JanusMael/Bennewitz.Ninja.XamlQuality](https://github.com/JanusMael/Bennewitz.Ninja.XamlQuality)
are the one living copy of each. Send a new finding or a correction to the XamlQuality session by
message, with the versions and the measurement or source behind it, or open an issue there when no
session is running. Keep no copy here.

**Every change:** update `PROGRESS.md` in the same commit. Commits are Conventional Commits.
