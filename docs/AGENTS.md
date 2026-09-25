# AGENTS.md — `docs/`

Documents for humans. What an agent needs lives in the `AGENTS.md` files, not here.

| Document | What it is | Kept in step with |
|---|---|---|
| `publishing.md` | The release runbook: one-time trusted-publishing setup, the version rule, releasing, and verifying against the feed | `.github/workflows/release.yml`. A step renamed or reordered there is renamed or reordered here in the same change |

The rules are documented in the root `README.md`, not here: it is packed, so it is also the
nuget.org page each rule's help link points into, and `RulesCatalogTests` checks every rule has its
section there.
