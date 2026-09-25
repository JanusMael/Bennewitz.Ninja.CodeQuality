# AGENTS.md — `tests/`

The test projects. `tests/Directory.Build.props` makes every project here an xUnit v3 test
executable that is never packed.

<!-- bbpkg: list the test projects and what each one covers beyond the packaging guards -->

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| Tests run on Microsoft.Testing.Platform | `dotnet test` takes `--solution` and rejects VSTest-only switches | `global.json`, `test.runner` |
| `tests/Directory.Build.props` imports the root props explicitly | Without it, every test project silently loses the root's target framework and nullable settings | the import line itself |
| The tests under `Packaging/` stay as strong as they are | They are the only thing standing between a packaging mistake and a permanent release | `PackagingTests`, `TrimmableTests` |

⛔ **Never weaken a packaging test to make it pass.** `PackagingTests` reads the project files and
both package lists; `TrimmableTests` reads the trimmable mark off the compiled assembly. When one
fails, the package list or the project is wrong, not the test.
