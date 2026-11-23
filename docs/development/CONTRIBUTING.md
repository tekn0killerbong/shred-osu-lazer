# Source layout and contributions

Start with [Building](BUILDING.md) and [Testing](TESTING.md).

| Location | Purpose |
| --- | --- |
| `osu.Game.Rulesets.Shred/` | Ruleset and editor implementation |
| `osu.Game.Rulesets.Shred/Resources/` | Embedded samples, fonts and artwork |
| `Shred.Official.Tests/` | Official-client test runner |
| `Shred.*.Tests/`, `osu.Game.Rulesets.Shred.Verification/` | Additional or historical checks |
| `ShredSkin/` | Optional runtime skin configuration |
| `docs/` | Current developer guides and historical documentation |
| `tools/` | Maintenance utilities and archived scripts |
| `local/` | Ignored local reports and obsolete extraction copies |
| `../osu-shred-lazer/` | Modified lazer client sources |

Keep project files, solutions, `Directory.Build.props`, `global.json`, `app.manifest` and solution settings at their existing locations. Keep gameplay resources and `ShredSkin` in their existing locations too.

Work in the active source folders, not in old copies under `local/archives/`. If a change affects shared editor code in the client tree, include those files in the same review or identify the matching companion commit.

Before committing, inspect `git status --short` and `git diff --stat`. Build outputs, headless profiles, local archives and personal IDE state should not be newly committed. Ignore rules do not automatically untrack files already committed; inspect `git ls-files` if old build output appears in the index.

Use the repository's existing licence and third-party notices. Reorganising files does not change any licence. The root `OSU-LICENCE.txt` is preserved as supplied.
