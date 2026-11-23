# Building Shred

This guide describes the supplied v69 sources. The official-client package target is `ppy.osu.Game` **2026.916.0**; this is not a claim of compatibility with every later lazer version.

**Ready-to-build source layout:** this complete archive already contains the v69 source repairs and the organised directories. See [Build verification](BUILD-STATUS.md).

## Source directories

Keep these directories beside each other, with these local names:

- `osu-shred/`: ruleset, editor, tests and this documentation.
- `osu-shred-lazer/`: the modified lazer source tree.

Repository names on GitHub may differ, but the current project references use these local directory names. Do not rename either directory without updating those references. Both trees are required even for the official DLL, which links several editor source files from the client tree.

Install a .NET 10 SDK accepted by each project's `global.json` (`10.0.100`, with `latestFeature` roll-forward). Compilation was checked with SDK 10.0.401; see the build-status note above. Package restore needs access to NuGet.

Run the following commands **from `osu-shred/`**.

## DLL for the official client

```powershell
dotnet build ./Shred.Official.Tests/Shred.Official.Tests.csproj -c Release -p:ShredOfficial=true
```

The official ruleset DLL is copied to:

`Shred.Official.Tests/bin/Release/net10.0/osu.Game.Rulesets.Shred.dll`

Use that copy when distributing to users of the official client. Build output under the ruleset project's own `bin/` can later be overwritten by a developer-client build. Close lazer before replacing its ruleset DLL.

To compile only the ruleset against the official package:

```powershell
dotnet build ./osu.Game.Rulesets.Shred/osu.Game.Rulesets.Shred.csproj -c Release -p:ShredOfficial=true
```

Its DLL is then in `osu.Game.Rulesets.Shred/bin/Release/net10.0/`.

## Development client

```powershell
dotnet build ./shred.sln -c Release -p:ShredOfficial=false
dotnet run --project ../osu-shred-lazer/osu.Desktop/osu.Desktop.csproj -c Debug -p:ShredOfficial=false
```

The development configuration uses the neighbouring `osu.Game` project. Its DLL is intended for that matching client build. Run builds of the two configurations sequentially and allow restore when switching configurations.

`shred.sln` is the small ruleset solution. The older `osu.Game.Rulesets.Shred.sln` additionally references `osu.Game.Rulesets.Shred.Tests`. Keep this test directory if you have it; it was omitted from the archive supplied for this reorganisation. It is not required by the official runner above or by `shred.sln`.

## Other guides

- [Tests](TESTING.md)
- [Source layout](CONTRIBUTING.md)
- [Optional skin](../skinning.md)

Historical patch scripts are not setup steps for these current sources.
