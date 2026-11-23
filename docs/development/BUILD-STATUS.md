# Build verification and repaired source mismatch

The original uploaded sources had four compiler errors: `ShredDefaultKeys.EmptyStep` and `EmptyStepAlternate` were missing (three calls), and `ShredGlyph` lacked its `lightOnly` constructor parameter (one call).

This complete source archive already includes the two restored Shred files below from `shred-v64-sources-github-compile.zip`:

- `Input/ShredDefaultKeys.cs`: keyboard-layout defaults used by the current bindings and migration.
- `UI/ShredGlyph.cs`: transparent pick receptors and the existing fret-fill behaviour.

No subsequent v65-v69 source delta replaces these files. All other current sources are preserved. No additional repair or reorganisation package is needed: these definitions are already present at their final paths.

The full development client also required two absent helpers, restored from the same archive: `osu.Game.Rulesets.Mania/Edit/ManiaRecorderBindingSource.cs` and `ManiaRecorderConfig.cs`. Their existing caller matches that source revision. These helpers belong only to the development client, not to the official Shred DLL.

With the repair, the official-client test runner builds in Release using .NET SDK 10.0.401 with zero errors. Two existing test warnings remain (CS8632 and OFSG001). The complete `osu.Desktop` development client also builds in Debug with zero errors and three existing CA1512 warnings in `ManiaGridTicks.cs`. The validation used `-m:1 -nr:false` for this build.

Focused checks cover two Empty Step slots, legacy Realm migration, preservation of custom keys, transparent pick fill, fret colours and solid notes (22 checks). The existing keyboard checks pass for 306 ordered pairs; the v69 gameplay suite passes 5,405 checks.

Validation ran on Linux. Windows keyboard-layout APIs and live visual rendering were not exercised. The separately omitted `osu.Game.Rulesets.Shred.Tests` project was not required for these builds and was not validated. Keep that directory locally if you have it.

## Verification of this complete organised archive

Both builds were rerun from this final directory layout with .NET SDK 10.0.401:

- Official runner, Release, `ShredOfficial=true`: zero errors, two existing test warnings.
- Complete `osu.Desktop`, Debug, `ShredOfficial=false`: zero errors, three existing CA1512 warnings.
- The v69 gameplay suite was rerun against the final official build: 5,405 checks passed.

The archive omits generated build output, Git metadata, local profiles, reports and redundant old extraction copies. Active code, resources, project files and supplied licences match the repaired source tree.
