using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects;
using osuTK;

namespace osu.Game.Rulesets.Shred.Beatmaps
{
    // The official import pipeline calculates native standard difficulty first.
    // Give it valid geometry; retain the independent shred data for our converter.
    public class ShredCarrierNote : HitObject, IHasPosition
    {
        public Vector2 Position { get; set; }
        public float X { get => Position.X; set => Position = new Vector2(value, Position.Y); }
        public float Y { get => Position.Y; set => Position = new Vector2(Position.X, value); }
        private readonly int row;
		private readonly int headSnapDivisor;
		private readonly int tailSnapDivisor;
        private readonly int fret;
        private readonly ShredPracticeArticulation articulation;
        public double Duration { get; set; }
        public double EndTime => StartTime + Duration;

        protected ShredCarrierNote(ShredHitObject note)
        {
            StartTime = note.StartTime;
            Samples = note.Samples.Select(s => s.With()).ToList();
            row = note.Row;
            fret = note.Fret;
            articulation = note.Articulation;
            Duration = note.Duration;
			headSnapDivisor = note.HeadSnapDivisor;
			tailSnapDivisor = note.TailSnapDivisor;
            Position = new Vector2((note.Column + 0.5f) * 512 / 12, 192);
        }

        public static ShredCarrierNote From(ShredHitObject note)
        {
            if (note.IsLong) return new Sustain(note);
            return new ShredCarrierNote(note);
        }

        public ShredHitObject ToShred() => new ShredHitObject
        {
            StartTime = StartTime, Duration = Duration, Row = row, Fret = fret,
			HeadSnapDivisor = headSnapDivisor, TailSnapDivisor = tailSnapDivisor,
            Articulation = articulation, Samples = Samples.Select(s => s.With()).ToList()
        };

        // Preserve the real last-note end for metadata, preview and song duration.
        private sealed class Sustain : ShredCarrierNote, IHasDuration
        {
            public Sustain(ShredHitObject note) : base(note) { }
        }
    }
}
