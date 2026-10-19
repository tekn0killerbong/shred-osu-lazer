// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.Shred.Replays
{
    public class ShredReplayFrame : ReplayFrame
    {
        public List<ShredAction> Actions = new List<ShredAction>();

        public ShredReplayFrame(ShredAction? button = null)
        {
            if (button.HasValue)
                Actions.Add(button.Value);
        }

        public override bool IsEquivalentTo(ReplayFrame other)
            => other is ShredReplayFrame scrollingFrame && Time == scrollingFrame.Time && Actions.SequenceEqual(scrollingFrame.Actions);
    }
}
