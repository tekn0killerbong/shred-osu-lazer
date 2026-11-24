# Tests

From `osu-shred/`, compile the official runner first:

```powershell
dotnet build ./Shred.Official.Tests/Shred.Official.Tests.csproj -c Release -p:ShredOfficial=true
$runner = './Shred.Official.Tests/bin/Release/net10.0/Shred.Official.Tests.dll'
dotnet $runner --v69-gameplay
dotnet $runner --v70-editor
dotnet $runner --v58-scroll
dotnet $runner --v68-gameplay
dotnet $runner --v60-gameplay
dotnet $runner --v61-gameplay
dotnet $runner --v53-gameplay
```

For changes to editor placement and timing, the runner also includes:

```powershell
dotnet $runner --v68-snap
dotnet $runner --v68-editor
dotnet $runner --v67-editor
dotnet $runner --v66-editor
dotnet $runner --v65-editor
dotnet $runner --v64-editor
```

Editor checks use a headless game host and still need the game's native dependencies. They do not replace a manual test of rendering, audio and keyboard behaviour.

For v0.1.1 (internal revision v70), `--v70-editor` checks the default/reset scroll speed of 8 and editor geometry at 1%, 50%, 100% and 200% playback, with Pitched on/off, paused/running and several native zoom levels. `--v58-scroll` checks that gameplay scroll remains independent of playback rate.

Do not run the official runner without arguments as a current full-suite command: its default path contains historical checks for earlier APIs. Choose suites relevant to the change.

The other `Shred.*.Tests` projects and `osu.Game.Rulesets.Shred.Verification` remain at their original paths because they link source files by relative path. Some are historical. Scripts in `tools/legacy-tests/` are archived launchers, not the current release gate.

The separate `osu.Game.Rulesets.Shred.Tests` directory was excluded from the owner's upload. This reorganisation does not delete, recreate or validate it. Keep its source files when publishing if you intend to offer its solution; `bin/` and `obj/` do not belong in the repository.
