// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.Edit.GameplayTest
{
    /// <summary>Waits for completed judgements, the last tail, and an unpaused outro.</summary>
    public sealed class EditorTestCompletion
    {
        private double? readySince;
        private double lastGameplayTime = double.NegativeInfinity;

        public bool Update(bool scoreCompleted, bool paused, double gameplayTime, double lastObjectEnd, double realTime, double delay)
        {
            bool rewound = gameplayTime < lastGameplayTime;
            lastGameplayTime = gameplayTime;

            if (!scoreCompleted || paused || rewound || gameplayTime < lastObjectEnd)
            {
                readySince = null;
                return false;
            }

            readySince ??= realTime;
            return realTime - readySince.Value >= delay;
        }
    }
}
