# AGENTS.md — `.github/`

The workflows and the repository's GitHub settings.

| File | What it is |
|---|---|
| `workflows/ci.yml` | Build and test, pack with the packaging guard, and the conventions check, on every push and pull request |
| `workflows/release.yml` | Publishes to nuget.org through trusted publishing, on a `v*.*.*` tag. Dispatched with the version blank, it only proves the credentials |
| `repository.json` | What this repository's GitHub settings vary by: description, topics, required checks, exemptions. `scripts/repo-conventions.cs` applies and checks it |
| `copilot-instructions.md` | A pointer to the root `AGENTS.md` for tools that look here |

## Rules

| Rule | Why |
|---|---|
| **A job's name is part of the `main` ruleset.** Renaming or removing a job that `repository.json` lists under `requiredChecks` means updating the list and running `apply` in the same change | A required check that no job reports blocks every pull request, and GitHub never says why. `check` fails on the mismatch |
| **`release.yml` keeps its file name** | The trusted-publishing policy on nuget.org names this file, and the OIDC token is bound to the name. Renamed, every login fails |
| The release names each package it pushes, from `packages.push` | A glob publishes whatever is in the folder, permanently |
| `release.yml` holds the `id-token: write` permission and nothing broader than it needs | That permission is what replaces a stored API key |
