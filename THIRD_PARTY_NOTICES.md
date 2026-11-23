# shred — third-party and pre-existing license notices

This notice accompanies the reorganised v69 sources. It supplements existing notices; it does not replace them, change their terms, or attribute third-party work to the author of shred. The scope of the shred-specific license is defined in [LICENCE.md](LICENCE.md).

## osu! code and retained MIT notices

The shred ruleset includes code originating from osu!lazer and its ruleset templates, copyright ppy Pty Ltd and other applicable contributors. The separate `osu-shred-lazer` development client is a modified osu!lazer source tree.

Preserve the original copyright and license notices, including:

- [OSU-LICENCE.txt](OSU-LICENCE.txt), at the root of the `osu-shred` repository;
- `LICENCE`, at the root of the separate `osu-shred-lazer` repository;
- existing file-level and component-level license headers.

Upstream source: <https://github.com/ppy/osu>.

The shred-specific license does not replace MIT for material covered by those notices. Existing permissions, including commercial permissions under MIT, remain available for the material to which they apply. Adding `LICENCE.md` does not revoke earlier licenses or change the license of a file already carrying an MIT notice.

Examples still carrying MIT headers in the inspected v69 ruleset sources include:

- `osu.Game.Rulesets.Shred/ShredRuleset.cs`
- `osu.Game.Rulesets.Shred/ShredDifficultyCalculator.cs`
- `osu.Game.Rulesets.Shred/Mods/ShredModAutoplay.cs`
- `osu.Game.Rulesets.Shred/Replays/ShredReplayFrame.cs`
- `osu.Game.Rulesets.Shred/Replays/ShredFramedReplayInputHandler.cs`

These are examples, not an exhaustive provenance inventory. Existing headers in tests, templates and the development client also remain in force.

### Retained osu! MIT text

The following is the text retained in `OSU-LICENCE.txt` and in the development client's `LICENCE`:

```text
Copyright (c) 2025 ppy Pty Ltd <contact@ppy.sh>.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

## Dependency versions and license information

The official ruleset build references `ppy.osu.Game` version `2026.916.0`. The development build instead references the sibling `osu-shred-lazer/osu.Game` source project. Their dependencies keep their own licenses.

The following is a non-exhaustive summary checked against the v69 project configuration, restored package metadata and included package license files. A restored dependency is not necessarily embedded in the Shred DLL or redistributed in every release. Build-only dependencies and native components need to be distinguished from files actually shipped.

| Component / inspected version | License or retained notice |
| --- | --- |
| [ppy.osu.Game 2026.916.0](https://www.nuget.org/packages/ppy.osu.Game/2026.916.0) | MIT |
| [ppy.osu.Framework 2026.914.0](https://www.nuget.org/packages/ppy.osu.Framework/2026.914.0) | MIT |
| [AutoMapper 13.0.1](https://www.nuget.org/packages/AutoMapper/13.0.1) | MIT |
| [DiffPlex 1.9.0](https://www.nuget.org/packages/DiffPlex/1.9.0) | Apache-2.0 |
| [Realm 20.1.0](https://www.nuget.org/packages/Realm/20.1.0) | Apache-2.0 |
| [SQLitePCLRaw.core 3.0.5](https://www.nuget.org/packages/SQLitePCLRaw.core/3.0.5) | Apache-2.0; native SQLite and related packages have their own notices |
| [Markdig 0.23.0](https://www.nuget.org/packages/Markdig/0.23.0) | BSD-2-Clause |
| [OpenTabletDriver 0.6.7](https://www.nuget.org/packages/OpenTabletDriver/0.6.7) | LGPL-3.0-or-later |
| [TagLibSharp 2.3.0](https://www.nuget.org/packages/TagLibSharp/2.3.0) | LGPL-2.1-only |
| [FFmpeg.AutoGen 4.3.0.1](https://www.nuget.org/packages/FFmpeg.AutoGen/4.3.0.1) | LGPL version 3 text included as `LICENSE.txt`; native FFmpeg is a separate component |
| [SixLabors.ImageSharp 3.1.11](https://www.nuget.org/packages/SixLabors.ImageSharp/3.1.11) | Six Labors Split License 1.0, included as `LICENSE` |
| [ppy.ManagedBass 2022.1216.0](https://www.nuget.org/packages/ppy.ManagedBass/2022.1216.0) | MIT for this managed wrapper, copyright Mathew Sachin; native BASS has separate terms |
| [ppy.osu.Game.Resources 2026.909.0](https://www.nuget.org/packages/ppy.osu.Game.Resources/2026.909.0) | Resource-specific terms; see the upstream resource notices |

The inspected package metadata does not provide an explicit license field for `HidSharpCore 1.3.0`, `ppy.SDL2-CS 1.0.741-alpha`, `ppy.Vk 1.0.26` and `ppy.osuTK.NS20 1.0.211`. This metadata gap is not a declaration that those components are unlicensed or unrestricted. Their applicable upstream and bundled notices must be checked for any release that redistributes them.

Preserve any additional `NOTICE`, license or third-party notice files that accompany components. If an earlier distribution contains `THIRD-PARTY-NOTICES.md` or a `Notices/` directory, retain them with the components they cover. Those items are not assumed to exist in the reorganised source tree, and this file does not recreate their contents.

## Binary distributions and native libraries

LGPL-covered components retain their applicable rights and obligations, including source, modification and relinking requirements where applicable. The shred-specific license does not override those rights. This summary alone does not fulfill all obligations for distributing those components.

The managed wrapper's license does not establish the license of a native library. Native BASS, its add-ons, FFmpeg and other native libraries must be considered separately for the actual versions and builds distributed. This repository grants no commercial BASS permission.

- BASS and licensing information: <https://www.un4seen.com/bass.html>
- FFmpeg legal and licensing information: <https://ffmpeg.org/legal.html>

A standalone client or Windows preview may distribute dependencies that a ruleset-only DLL release leaves to the installed osu!lazer client. Preserve the notices required by the files actually shipped; do not treat this package summary as a complete binary-distribution notice bundle.

## Resources, fonts, music and community content

The upstream osu!resources project states that most of its content is under CC BY-NC 4.0 and that some fonts have separate licenses. Consult the notices supplied with the resource version being used:

- <https://github.com/ppy/osu-resources/blob/master/README.md>
- <https://github.com/ppy/osu-resources/blob/master/LICENCE.md>

Original shred assets are covered only to the extent described in `LICENCE.md`. Imported or sampled sounds, music, fonts, skins, artwork and independently authored beatmaps retain their respective owners' rights. A filename, inclusion in a repository or conversion to another format does not establish ownership or grant redistribution rights.

A shred permission for media, mapping or events grants no rights over third-party music or other protected content.

## PolyForm license text

The unmodified PolyForm Noncommercial License 1.0.0 is reproduced in `LICENCE.md`, separately from the shred-specific permissions. The PolyForm Project permits reuse of its license texts; no authorship of that text is claimed by the shred author.

- <https://polyformproject.org/licenses/noncommercial/1.0.0>
- <https://github.com/polyformproject/polyform-licenses/tree/1.0.0>

## Scope of this notice

This notice reflects the inspected reorganised v69 source configuration. It is not a complete audit of asset provenance, every transitive dependency, or every future binary release. Actual component licenses and notices govern their respective material.

shred is an independent project. No affiliation with, endorsement by, or trademark permission from osu!, ppy or any other owner is implied by this repository or these notices.
