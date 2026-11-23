# Optional skin configuration

The actual configuration stays in `ShredSkin/skin.ini`, in its `[Shred]` section. This directory is not documentation and must not be moved into `docs/`.

In the supplied source, `ShredSkinSettings.Load()` checks `ShredSkin/skin.ini` in the current working directory, then in the application's base directory. If neither exists, built-in defaults are used. Reload the scene after editing the configuration.

Consult `osu.Game.Rulesets.Shred/Skinning/ShredSkinSettings.cs` for supported fields and ranges, and `ShredSkinAssets.cs` for optional textures. Deleting a historical text guide does not delete the settings or resources.
