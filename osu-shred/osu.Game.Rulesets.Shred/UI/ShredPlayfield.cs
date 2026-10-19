using System;
using osu.Framework.Logging;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Rulesets.Shred.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Shred.Scoring;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Shred.Skinning;
using osu.Framework.Platform;
using osu.Game.Skinning;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
namespace osu.Game.Rulesets.Shred.UI
{
	[Cached]
	public partial class ShredPlayfield : Playfield, IKeyBindingHandler<ShredAction>
	{
		protected override HitObjectContainer CreateHitObjectContainer() => new ShredHitObjectContainer();
		[Resolved] private DrawableShredRuleset drawableRuleset { get; set; }
		private readonly ShredStringState[] strings = { new ShredStringState(), new ShredStringState() };
		public ShredSkinSettings Skin { get; } = ShredSkinSettings.Load();
		public ShredSkinAssets Assets { get; private set; }
		private readonly Container[] handPanels =
        {
            createHandPanel(), createHandPanel()
        };
        private static Container createHandPanel() => new Container
        {
            Masking = true,
            CornerRadius = 5,
            BorderThickness = 1,
            BorderColour = new Color4(170, 160, 205, 55),
            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(95, 80, 125, 12) }
        };
        private readonly Container lights = new Container { RelativeSizeAxes = Axes.Both };
		private readonly Container effects = new Container { RelativeSizeAxes = Axes.Both };
		private readonly Drawable[,] stageLights = new Drawable[2, 9];
		private readonly bool[,] lightHeld = new bool[2, 9];
		private readonly double[,] shakeStarts = new double[2, 5];
		private readonly bool[,] shaking = new bool[2, 5];
		private readonly Container stage = new Container { RelativeSizeAxes = Axes.Both };
		private readonly Box whamLine = new Box { Origin = Anchor.Centre, Colour = ShredWhamGeometry.Colour };
		private readonly DrawableShredHitObject[][] armed = new DrawableShredHitObject[2][];
		private bool whamHeld;
		private bool whamDepressed;
		private int scrollDirection;
		private double nextScrollRepeat;
		private readonly double[] armedPickTime = new double[2];
        private readonly int[] armedPickCount = new int[2];
        private readonly ShredFretSequence sequence = new ShredFretSequence();
        private readonly ShredPickSequence pickSequence = new ShredPickSequence();
        private readonly ShredPickBuffer<DrawableShredHitObject[]> pickBuffer = new ShredPickBuffer<DrawableShredHitObject[]>();
        private double lastInputTime = double.NegativeInfinity;
		private readonly SpriteText feedback = new SpriteText { Anchor = Anchor.BottomCentre, Origin = Anchor.BottomCentre, Y = -30 };
		private readonly Container receptors = new Container { RelativeSizeAxes = Axes.Both };
		private readonly ShredGlyph[,] glyphs = new ShredGlyph[2, 9];
		private readonly Box[] sectionLines = new Box[6];
		private readonly Box[,] lanes = new Box[2, 5];
		public static double ApproachTime => ShredScrollSpeed.ApproachTime;
		public bool GuitarHero => drawableRuleset?.GuitarHero == true;
		public float MovementUnit
		{
			get
			{
				float scale = Math.Min(DrawWidth / 1000, DrawHeight / 720);
				if (GuitarHero) scale *= 1.3f;
				return scale;
			}
		}
		public float Unit => MovementUnit * ShredPlayfieldGeometry.VisualScale;
		public ShredPlayfield()
        {
            Masking = true;
            NewResult += (_, result) => { if (result.Type == HitResult.Miss) sequence.Miss(); };
        }
		public static Color4 FretColour(int fret)
		{
			Color4[] colours = { new Color4(95,65,225,255), new Color4(155,55,235,255), new Color4(210,50,205,255), new Color4(240,55,135,255), new Color4(245,65,70,255) };
			return colours[fret - 1];
		}
		public Vector2 Receiver(int row, int fret)
		{
			if (GuitarHero)
			{
				return ShredPlayfieldGeometry.GuitarHeroReceiver(Skin, DrawSize, Unit, fret);
			}
			float x;
			if (fret == 0) x = Skin.OpenCentre;
			else if (fret <= 5) x = Skin.FretCentre(fret);
			else x = ShredPlayfieldGeometry.FirstPickCentre(Skin) + (fret - 7) * 65;
			if (row == 1) x += fret > 5 ? -Math.Abs(Skin.EffectiveRowOffsetBottom) : Skin.EffectiveRowOffsetBottom;
			return new Vector2(DrawWidth / 2 + x * Unit, DrawHeight / 2 + (-Skin.RowGap / 2 + row * Skin.RowGap) * Unit);
		}
		public Vector2 NoteSize(int fret)
		{
			if (fret == 0) return new Vector2(Skin.OpenWidth, 8) * Unit;
			return new Vector2(Skin.ColumnWidth[fret - 1], Skin.NoteHeight) * Unit;
		}
		public Vector2 LaneDirection(int row)
		{
			if (GuitarHero) return Skin.Direction(0);
			return Skin.Direction(row);
		}
		public Vector2 Travel(int row) => LaneDirection(row) * Skin.ScrollLength * MovementUnit;
		public Vector2 LaneTravel(int row, int fret)
		{
			Vector2 incoming = -LaneDirection(row);
			return -incoming * ShredPlayfieldGeometry.DistanceToEdge(Receiver(row, fret), incoming, DrawSize);
		}
		// Reserve enough lifetime at the slowest supported scroll speed, even
		// when a note enters from a much more distant screen edge.
		public double MaximumVisibleLeadTime => GuitarHero
            ? 11485 * Math.Max(1, DrawSize.Length / Math.Max(1, Travel(1).Length)) + 500
            : 11485 + 500;

		[BackgroundDependencyLoader]
		private void load(GameHost host, ISkinSource skinSource)
		{
			if (GuitarHero) { Skin.AlignRows = false; Skin.BottomAngle = Skin.TopAngle; }
			Assets = new ShredSkinAssets(Skin, host, skinSource);
			AddInternal(stage);
			foreach (var panel in handPanels) stage.Add(panel);
			stage.Add(lights);
			receptors.Add(whamLine);
			stage.Add(receptors);
			for (int index = 0; index < sectionLines.Length; index++)
			{
				sectionLines[index] = new Box { Colour = new Color4(180, 175, 210, 120) };
				receptors.Add(sectionLines[index]);
			}
			stage.Add(HitObjectContainer);
			stage.Add(effects);
			stage.Add(feedback);
			for (int row = 0; row < 2; row++)
			{
				for (int action = 0; action < 9; action++)
				{
					Color4 colour = new Color4(160,145,200,255);
					if (action < 5)
						colour = FretColour(action + 1);
					ShredGlyph glyph = new ShredGlyph(colour, action >= 5 && !GuitarHero, Skin.Shear(row)) { Origin = Anchor.Centre };
					glyphs[row, action] = glyph;
					receptors.Add(glyph);
					Texture[] lightFrames = Assets.GetFrames("shred-stage-light");
					if (lightFrames.Length == 0) lightFrames = Assets.GetFrames("mania-stage-light");
					Drawable light;
					if (lightFrames.Length > 0 && action < 5)
						light = new ShredAnimatedSprite(lightFrames, Skin.AnimationFPS) { Colour = Skin.ColourLight, Blending = BlendingParameters.Additive, Origin = Anchor.BottomCentre };
					else
						light = new ShredGlow(Skin.ColourLight, Skin.HaloRadius, Skin.Shear(row)) { Origin = Anchor.Centre };
					if (action >= 5 && !GuitarHero)
					{
						light.Shear = Vector2.Zero;
						light.Rotation = 45;
					}
					light.Alpha = 0;
					stageLights[row, action] = light;
					lights.Add(light);
					if (action < 5)
					{
						lanes[row, action] = new Box { Colour = new Color4(160,165,180,100) };
						receptors.Add(lanes[row, action]);
					}
				}
			}
            NewResult += (drawable, result) =>
            {
                ShredHitObject note = (drawable as DrawableShredHitObject)?.HitObject
                    ?? (drawable as DrawableShredHoldHead)?.HitObject.Parent;
                if (note == null) return;
                double offset = (result as ShredJudgementResult)?.PickOffset ?? result.TimeOffset;
                feedback.Text = $"{result.Type} / {offset:+0;-0;0} ms";
            };
        }
		public float WhamLineWidth => Math.Max(1, Unit);
		public Vector2 WhamLineCentre => ShredWhamGeometry.JudgementCentre(Skin, Receiver(0, 5), Receiver(1, 5), Unit, WhamLineWidth);
		public Vector2 WhamReceiver => ShredWhamGeometry.ContactEdge(WhamLineCentre, WhamLineWidth);
        public Vector2 WhamSize => new Vector2(8, Math.Min(10, Math.Max(4, Skin.RowGap - 12))) * Unit;
        public Vector2 PositionAt(ShredHitObject note, double time)
            => note.IsWham
                ? WhamReceiver + new Vector2(Math.Max(1, DrawWidth - WhamReceiver.X) * (float)((time - Time.Current) / ApproachTime), 0)
                : Receiver(note.Row, note.Fret) - (GuitarHero ? Travel(note.Row) : LaneTravel(note.Row, note.Fret)) * (float)((time - Time.Current) / ApproachTime);
        public void ShowWhamSuccess()
        {
            Drawable flash = new Box { Colour = ShredWhamGeometry.Colour, Origin = Anchor.Centre };
            flash.Position = WhamReceiver;
            flash.Size = WhamSize;
            effects.Add(flash);
            flash.ScaleTo(Skin.HitExpand, Skin.HitFadeMs).FadeOut(Skin.HitFadeMs).Expire();
        }
        public void ShowWhamError()
        {
            Drawable flash = new Box { Colour = Skin.ErrorColour, Origin = Anchor.Centre, Position = WhamReceiver, Size = WhamSize };
            effects.Add(flash);
            flash.FadeOut(Skin.ShakeMs).Expire();
        }
        public bool OnPressed(KeyBindingPressEvent<ShredAction> e)
        {
            if (e.Action == ShredAction.ScrollFaster || e.Action == ShredAction.ScrollSlower)
            {
                if (e.Repeat) return true;
                scrollDirection = -1;
                if (e.Action == ShredAction.ScrollFaster) scrollDirection = 1;
                nextScrollRepeat = Time.Current + 350;
                ShredScrollSpeed.Adjust(scrollDirection);
                feedback.Text = $"Scroll speed {ShredScrollSpeed.Speed} · {ApproachTime:0} ms";
                return true;
            }
            if (drawableRuleset.IsPaused.Value) { pickBuffer.Reset(); return false; }
            prepareInputTime();
            if (e.Action == ShredAction.Wham)
            {
                if (whamHeld) return true;
                whamHeld = true;
                foreach (var overdue in AllHitObjects.OfType<DrawableShredHitObject>().Where(n => n.HitObject.IsWham).ToArray()) overdue.CheckExpiry();
                var next = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => n.HitObject.IsWham && !n.HeadJudged).OrderBy(n => n.HitObject.StartTime).FirstOrDefault();
                if (next != null)
                {
                    if (next.HitObject.HitWindows.ResultFor(next.HeadOffset) == HitResult.Miss)
                    {
                        next.MissHead(Time.Current);
                        sequence.Miss();
                        ShowWhamError();
                    }
                    else if (!ShredInputJudgement.IsTooEarlyToTarget(next.HitObject.HitWindows, next.HeadOffset)) next.TryHit();
                }
                return true;
            }
            if (!ShredBindingProfile.TryDecode(e.Action, out int row, out int action)) return false;
            int inputRow = row;
            if (GuitarHero)
            {
                if (action < 5 && row != 1) return true;
                row = 1;
            }
            int previous = strings[row].FretMask;
            if (action < 5)
            {
                if (e.Repeat || strings[row].IsFretHeld(action)) return true;
                sequence.Press(action, strings[0].FretMask | strings[1].FretMask, row);
                strings[row].SetFret(action, true);
                tryJudge(row, false, true, previous);
                if (armed[1 - row] != null) tryJudge(1 - row, false, true, strings[1 - row].FretMask);
            }
            else if (strings[inputRow].SetPick(action - 5, true))
            {
                pickBuffer.PhysicalPress(Time.Current, commitBufferedPull);
                tracePick(e.Action, "down");
                pickSequence.Press(inputRow, action - 5, currentPickTarget(row), Time.Current, Clock?.Rate ?? 1);
                tryJudge(row, true, true, previous);
            }
            return true;
        }
        public void OnReleased(KeyBindingReleaseEvent<ShredAction> e)
        {
            if (e.Action == ShredAction.ScrollFaster || e.Action == ShredAction.ScrollSlower) { scrollDirection = 0; return; }
            prepareInputTime();
            if (drawableRuleset.IsPaused.Value) pickBuffer.Reset();
            if (e.Action == ShredAction.Wham)
            {
                whamHeld = false;
                updateWhamDepression();
                if (!drawableRuleset.IsPaused.Value)
                    foreach (var note in AllHitObjects.OfType<DrawableShredHitObject>().Where(n => n.HitObject.IsWham && n.IsHolding).ToArray()) note.Release();
                return;
            }
            if (!ShredBindingProfile.TryDecode(e.Action, out int row, out int action)) return;
            int inputRow = row;
            if (GuitarHero)
            {
                if (action < 5 && row != 1) return;
                row = 1;
            }
            int previous = strings[row].FretMask;
            if (action < 5)
            {
                strings[row].SetFret(action, false);
                if (!drawableRuleset.IsPaused.Value)
                {
                    foreach (var note in AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham && n.HitObject.Row == row && n.HitObject.Fret == action + 1 && n.IsHolding).ToArray()) note.Release();
                    tryJudge(row, false, false, previous);
                    if (armed[1 - row] != null) tryJudge(1 - row, false, false, strings[1 - row].FretMask);
                }
            }
            else
            {
                if (!strings[inputRow].IsPickHeld(action - 5)) return;
                strings[inputRow].SetPick(action - 5, false);
                tracePick(e.Action, "up");
                int revealed = pickSequence.Release(inputRow, action - 5);
                if (revealed != 0 && !drawableRuleset.IsPaused.Value) queuePull(revealed);
            }
        }
		private void tracePick(ShredAction action, string transition)
		{
			// Diagnostic state only: no typed text or unrelated keyboard keys are recorded.
			Logger.Log($"Shred pick {action} {transition}; masks row1={strings[0].PickMask}, row2={strings[1].PickMask}", LoggingTarget.Runtime, LogLevel.Debug);
		}
        private double? currentPickTarget(int row)
        {
            var notes = AllHitObjects.OfType<DrawableShredHitObject>().ToArray();
            foreach (var note in notes) note.CheckExpiry();
            var next = notes.Where(n => !n.HitObject.IsWham && n.HitObject.Row == row && !n.HeadJudged)
                .OrderBy(n => n.HitObject.StartTime).FirstOrDefault();
            return next != null && ShredInputJudgement.IsWrongPickInWindow(next.HitObject.HitWindows, next.HeadOffset)
                ? next.HitObject.StartTime : null;
        }

        private void prepareInputTime()
        {
            if (Time.Current < lastInputTime)
            {
                sequence.Reset();
                pickSequence.Reset();
                pickBuffer.Reset();
                Array.Clear(armed, 0, armed.Length);
                Array.Clear(armedPickCount, 0, armedPickCount.Length);
            }
            lastInputTime = Time.Current;
            if (!drawableRuleset.IsPaused.Value) pickBuffer.Flush(Time.Current, commitBufferedPull);
        }

        /// <summary>Uses map time for deterministic replay, scaling the cap to real time.</summary>
        public void ProcessPendingPicks()
        {
            prepareInputTime();
            if (drawableRuleset.IsPaused.Value) { pickBuffer.Reset(); return; }
            pickBuffer.Flush(Time.Current, commitBufferedPull);
        }

        // Only an already valid, timestamped buffered return can defer expiry.
        // This does not extend the window for new physical inputs.
        public bool HasPendingPick(DrawableShredHitObject note)
            => pickBuffer.PendingValues.Any(targets => targets.Contains(note));
        public bool HasPendingPick(ShredHitObject note)
            => pickBuffer.PendingValues.Any(targets => targets.Any(target => target.HitObject == note));

        private void queuePull(int revealed)
        {
            var notes = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham).ToArray();
            foreach (var note in notes) note.CheckExpiry();
            if (!sequence.HammerPullReady) return;
            var reserved = pickBuffer.PendingValues.SelectMany(n => n).ToHashSet();
            var targets = new System.Collections.Generic.List<DrawableShredHitObject>();
            double nearestGap = double.PositiveInfinity;
            for (int row = 0; row < 2; row++)
            {
                if (GuitarHero && row == 0) continue;
                int count = System.Numerics.BitOperations.PopCount((uint)(GuitarHero ? revealed : (revealed >> (row * 4)) & 15));
                if (count == 0) continue;
                var first = notes.Where(n => n.HitObject.Row == row && !n.HeadJudged && !reserved.Contains(n))
                    .OrderBy(n => n.HitObject.StartTime).FirstOrDefault();
                if (first == null || !first.HitObject.HitWindows.ResultFor(first.HeadOffset).IsHit()) continue;
                var chord = notes.Where(n => n.HitObject.Row == row && Math.Abs(n.HitObject.StartTime - first.HitObject.StartTime) < 0.01).ToArray();
                var pending = chord.Where(n => !n.HeadJudged && !reserved.Contains(n)).ToArray();
                int required = pending.Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
                int sustaining = notes.Where(n => n.HitObject.Row == row && (n.IsHolding || (chord.Contains(n) && (n.HeadHit || reserved.Contains(n)))))
                    .Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
                int otherSustaining = notes.Where(n => n.HitObject.Row != row && n.IsHolding)
                    .Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
                int otherRequired = notes.Where(n => n.HitObject.Row != row && Math.Abs(n.HitObject.StartTime - first.HitObject.StartTime) < 0.01)
                    .Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
                int held = strings[row].FretMask;
                if (!sequence.IsExact(held, required, sustaining)
                    || !sequence.IsExact(held | strings[1 - row].FretMask, required | (otherRequired & strings[1 - row].FretMask), sustaining | otherSustaining)) continue;
                targets.AddRange(pending.OrderBy(n => sequence.PressOrder(n.HitObject.Fret, row)).ThenBy(n => n.HitObject.Fret).Take(count));
                foreach (var note in notes.Where(n => n.HitObject.Row == row))
                {
                    double gap = Math.Abs(note.HitObject.StartTime - first.HitObject.StartTime);
                    if (gap >= 0.01) nearestGap = Math.Min(nearestGap, gap);
                }
            }
            if (targets.Count == 0) return;
            double window = ShredPickBuffer<DrawableShredHitObject[]>.WindowFor(nearestGap, Clock?.Rate ?? 1);
            pickBuffer.Queue(Time.Current, window, targets.ToArray());
        }

        private void commitBufferedPull(DrawableShredHitObject[] targets, double releaseTime)
        {
            // Eligibility/frets and target identity were captured at release.
            // Never search for a replacement note after the buffering interval.
            foreach (var note in targets)
            {
                if (!note.TryHit(releaseTime, picked: true)) continue;
                int row = note.HitObject.Row;
                if (armed[row]?.Contains(note) == true) armedPickCount[row]++;
            }
            // The chain was already valid at capture. Do not re-arm it over an
            // intervening Miss which happened after the original release time.
        }

        private void tryJudge(int row, bool pick, bool pressed, int previous)
        {
            if (drawableRuleset.IsPaused.Value) return;
            // Expire both rows before checking the shared hammer-pull chain.
            var allNotes = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham).ToArray();
            foreach (var overdue in allNotes) overdue.CheckExpiry();
            var rowNotes = allNotes.Where(n => n.HitObject.Row == row).ToArray();
            int held = strings[row].FretMask;
            int sustaining = rowNotes.Where(n => n.IsHolding).Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            int otherSustaining = allNotes.Where(n => n.HitObject.Row != row && n.IsHolding)
                .Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            bool matches(int required, double time)
            {
                int otherRequired = allNotes.Where(n => n.HitObject.Row != row && Math.Abs(n.HitObject.StartTime - time) < 0.01)
                    .Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
                return sequence.IsExact(held, required, sustaining)
                    && sequence.IsExact(held | strings[1 - row].FretMask, required | (otherRequired & strings[1 - row].FretMask),
                        sustaining | otherSustaining);
            }

            var chord = armed[row];
            if (chord != null && (chord.All(n => n.HeadJudged) || chord.Any(n => n.HeadJudged && !n.HeadHit)))
            {
                armed[row] = chord = null;
                armedPickCount[row] = 0;
            }
            if (chord == null)
            {
                var first = rowNotes.Where(n => !n.HeadJudged).OrderBy(n => n.HitObject.StartTime).FirstOrDefault();
                if (first == null) { if (pick) emptyPick(row); return; }
                if (!ShredInputJudgement.IsWrongPickInWindow(first.HitObject.HitWindows, first.HeadOffset)) return;
                chord = rowNotes.Where(n => !n.HeadJudged && Math.Abs(n.HitObject.StartTime - first.HitObject.StartTime) < 0.01).ToArray();
            }
            var pending = chord.Where(n => !n.HeadJudged).ToArray();
            if (pick && pending.Length > 0
                && pending[0].HitObject.HitWindows.ResultFor(pending[0].HeadOffset) == HitResult.Miss)
            {
                // One pick targets one member, as with successful partial chords.
                var target = pending.OrderBy(n => sequence.PressOrder(n.HitObject.Fret, row))
                    .ThenBy(n => n.HitObject.Fret).First();
                target.MissHead(Time.Current);
                ShowWrongFret(row, target.HitObject.Fret);
                sequence.Miss();
                armed[row] = null;
                armedPickCount[row] = 0;
                return;
            }
            if (pending.Length == 0) return;
            bool currentHitWindow = pending[0].HitObject.HitWindows.ResultFor(pending[0].HeadOffset).IsHit();
            bool validArmedPick = armed[row] != null && pending[0].HitObject.HitWindows
                .ResultFor(armedPickTime[row] - pending[0].HitObject.StartTime).IsHit();
            if (!currentHitWindow && !validArmedPick) return;
            int wanted = chord.Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            if (pick)
            {
                if (armed[row] == null) armedPickCount[row] = 0;
                armed[row] = chord;
                armedPickTime[row] = Time.Current;
                armedPickCount[row]++;
            }
            if (armed[row] != null)
            {
                // Already paid members may stay held without blocking the rest.
                sustaining |= chord.Where(n => n.HeadHit).Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
                int remaining = chord.Where(n => !n.HeadJudged).Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
                if (!matches(remaining, chord[0].HitObject.StartTime)) return;
                // Each edge pays for one note, oldest pressed fret first. Unpaid
                // notes stay targetable until expiry, then receive their own Miss.
                int available = armedPickCount[row] - chord.Count(n => n.HeadHit);
                foreach (var note in chord.Where(n => !n.HeadJudged)
                    .OrderBy(n => sequence.PressOrder(n.HitObject.Fret, row)).ThenBy(n => n.HitObject.Fret).Take(Math.Max(0, available)))
                    note.TryHit(armedPickTime[row], picked: true);
                if (chord.All(n => n.HeadHit))
                {
                    sequence.PickSucceeded();
                    armed[row] = null;
                    armedPickCount[row] = 0;
                }
                return;
            }
            if (!sequence.HammerPullReady
                || chord.Any(n => n.HitObject.Articulation == ShredPracticeArticulation.Pick)
                || !matches(wanted, chord[0].HitObject.StartTime)
                || !sequence.IsHammerPull(previous, held, wanted, pressed, sustaining)) return;
            foreach (var n in chord) n.TryHit();
        }

        private void emptyPick(int row)
        {
            // Empty strums have no scoring or chain penalty.
            armed[row] = null;
            armedPickCount[row] = 0;
        }

		public void ShowSuccess(ShredHitObject note, bool completion = true)
		{
			int row = note.Row;
			int fret = note.Fret;
			Color4 colour = Color4.White;
			if (fret > 0) colour = FretColour(fret);
			if (note.IsLong && completion) colour = ShredNoteVisual.SustainColour(colour);
			Drawable flash = new ShredGlyph(colour, shear: Skin.Shear(row), solid: true) { Origin = Anchor.Centre };
			flash.Position = Receiver(row, fret);
			flash.Size = NoteSize(fret);
			effects.Add(flash);
			flash.ScaleTo(Skin.HitExpand, Skin.HitFadeMs, Easing.OutQuint);
			flash.FadeOut(Skin.HitFadeMs).Expire();
		}
		public void ShowWrongFret(int row, int fret)
		{
            if (fret == 0)
            {
                for (int f = 1; f <= 5; f++) ShowWrongFret(row, f);
                return;
            }
			for (int index = 0; index < 5; index++)
			{
				if (fret > 0 && fret != index + 1) continue;
				shaking[row, index] = true;
				shakeStarts[row, index] = Time.Current;
			}
			Drawable flash = effectDrawable("shred-error", Skin.ErrorColour, row);
			flash.Position = Receiver(row, fret);
			flash.Size = NoteSize(fret);
			effects.Add(flash);
			flash.FadeOut(Skin.ShakeMs).Expire();
		}
		private Drawable effectDrawable(string name, Color4 colour, int row)
		{
			Texture[] frames = Assets?.GetFrames(name) ?? Array.Empty<Texture>();
			if (frames.Length > 0)
				return new ShredAnimatedSprite(frames, Skin.AnimationFPS) { Colour = colour, Origin = Anchor.Centre, Shear = new Vector2(Skin.Shear(row), 0), Blending = BlendingParameters.Additive };
			return new ShredGlow(colour, Skin.HaloRadius, Skin.Shear(row)) { Origin = Anchor.Centre };
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);
			Assets?.Dispose();
		}

		private void placeHorizontalLine(Box line, float left, float right, float y)
		{
			left = Math.Clamp(left, 0, DrawWidth);
			right = Math.Clamp(right, 0, DrawWidth);
			line.Position = new Vector2(left, y);
			line.Size = new Vector2(Math.Max(0, right - left), Math.Max(Unit, 0.5f));
			line.Rotation = 0;
		}
		private void updateSectionLines()
		{
			// One horizontal string per row, interrupted by each complete hand section.
			// No border, cross-row divider, or lines between individual keys.
			for (int row = 0; row < 2; row++)
			{
				float y = Receiver(row, 1).Y;
				float fretLeft = Receiver(row, 1).X - Skin.ColumnWidth[0] / 2 * Unit;
				float fretRight = Receiver(row, 5).X + Skin.ColumnWidth[4] / 2 * Unit;
				float pickHalfWidth = 24 * MathF.Sqrt(2) / 2 * Unit;
				float pickLeft = Receiver(row, 7).X - pickHalfWidth;
				float pickRight = Receiver(row, 10).X + pickHalfWidth;
				placeHorizontalLine(sectionLines[row * 3], 0, fretLeft, y);
				placeHorizontalLine(sectionLines[row * 3 + 1], fretRight, pickLeft, y);
				placeHorizontalLine(sectionLines[row * 3 + 2], pickRight, DrawWidth, y);
			}
		}

		private void updateHandPanels()
        {
            for (int section = 0; section < 2; section++)
            {
                var panel = handPanels[section];
                panel.Alpha = GuitarHero ? 0 : 1;
                if (GuitarHero) continue;
                float left = float.PositiveInfinity, top = float.PositiveInfinity;
                float right = float.NegativeInfinity, bottom = float.NegativeInfinity;
                for (int row = 0; row < 2; row++)
                {
                    int first = section == 0 ? 1 : 7;
                    int last = section == 0 ? 5 : 10;
                    for (int column = first; column <= last; column++)
                    {
                        var centre = Receiver(row, column);
                        float halfHeight = (section == 0 ? Skin.NoteHeight / 2 : 12 * MathF.Sqrt(2)) * Unit;
                        float halfWidth = section == 0
                            ? (Skin.ColumnWidth[column - 1] / 2 + Math.Abs(Skin.Shear(row)) * Skin.NoteHeight / 2) * Unit
                            : halfHeight;
                        left = Math.Min(left, centre.X - halfWidth); right = Math.Max(right, centre.X + halfWidth);
                        top = Math.Min(top, centre.Y - halfHeight); bottom = Math.Max(bottom, centre.Y + halfHeight);
                    }
                }
                float padding = 5 * Unit;
                panel.Position = new Vector2(left - padding, top - padding);
                panel.Size = new Vector2(right - left + 2 * padding, bottom - top + 2 * padding);
            }
        }

        private void updateWhamDepression()
		{
			bool active = whamHeld;
			if (active == whamDepressed) return;
			whamDepressed = active;
			Vector2 offset = Vector2.Zero;
			if (active)
			{
				double radians = Skin.WhamPressAngle * Math.PI / 180;
				offset = new Vector2((float)Math.Cos(radians), -(float)Math.Sin(radians)) * Skin.WhamPressDistance * 0.25f * Unit;
			}
			stage.MoveTo(offset, 100);
		}

		protected override void Update()
		{
			base.Update();
            ProcessPendingPicks();
            if (!drawableRuleset.IsPaused.Value)
            {
                foreach (DrawableShredHitObject note in AllHitObjects.OfType<DrawableShredHitObject>().Where(n => n.IsHolding).ToArray())
                {
                    bool held = whamHeld;
                    if (!note.HitObject.IsWham && note.HitObject.Fret > 0)
                        held = strings[note.HitObject.Row].IsFretHeld(note.HitObject.Fret - 1);
                    if (!held) note.Release();
                }
            }
            if (scrollDirection != 0 && Time.Current >= nextScrollRepeat)
            {
                ShredScrollSpeed.Adjust(scrollDirection);
                nextScrollRepeat = Time.Current + 75;
                feedback.Text = $"Scroll speed {ShredScrollSpeed.Speed} · {ApproachTime:0} ms";
            }
			updateWhamDepression();
			updateSectionLines();
            updateHandPanels();
            if (GuitarHero) foreach (Box line in sectionLines) line.Alpha = 0;
			whamLine.Alpha = GuitarHero ? 0 : 1;
			whamLine.Position = WhamLineCentre;
			whamLine.Size = new Vector2(WhamLineWidth, Math.Max(4, Skin.RowGap - 10) * Unit);
			whamLine.Shear = new Vector2(Skin.Shear(0), 0);
			for (int row = 0; row < 2; row++)
			{
				for (int action = 0; action < 9; action++)
				{
					int column = action + 1;
					if (action >= 5) column++;
					ShredGlyph glyph = glyphs[row, action];
					if (glyph == null) continue;
                    bool visible = !GuitarHero || (row == 1 && (action < 5 || action == 5));
                    glyph.Alpha = 0;
                    if (visible) glyph.Alpha = 1;
					glyph.Position = Receiver(row, column);
					glyph.Size = new Vector2(24) * Unit;
					if (action < 5)
					{
						glyph.Size = NoteSize(action + 1);
						if (shaking[row, action])
						{
							double elapsed = Time.Current - shakeStarts[row, action];
							if (elapsed >= Skin.ShakeMs) shaking[row, action] = false;
							else glyph.X += (float)(Math.Sin(elapsed / Skin.ShakeMs * Math.PI * 8) * (1 - elapsed / Skin.ShakeMs)) * Skin.ShakeAmplitude * Unit;
						}
					}
					if (action >= 5) glyph.Size = new Vector2(24) * Unit;
					bool active;
					if (action < 5) active = strings[row].IsFretHeld(action);
					else active = strings[row].IsPickHeld(action - 5);
                    if (GuitarHero && action >= 5)
                    {
                        active = Enumerable.Range(0, 4).Any(i => strings[0].IsPickHeld(i) || strings[1].IsPickHeld(i));
                        glyph.Size = new Vector2(Skin.OpenWidth, Skin.NoteHeight * 0.45f) * Unit;
                    }
					glyph.SetPressed(active);
					bool held = active;
					if (action < 5) held = strings[row].IsFretHeld(action);
					Drawable light = stageLights[row, action];
                    if (!visible) { light.ClearTransforms(); light.Alpha = 0; }
					light.Position = Receiver(row, column) - Skin.Direction(row) * Skin.LightPosition * Unit;
					light.Size = glyph.Size;
					if (light is ShredAnimatedSprite animation)
					{
						light.Height = Skin.LightLength * Unit;
						Vector2 direction = Skin.Direction(row);
						light.Rotation = (float)(Math.Atan2(direction.Y, direction.X) * 180 / Math.PI) - 90;
						if (held && !lightHeld[row, action]) animation.Restart();
					}
					if (visible && held != lightHeld[row, action])
					{
						lightHeld[row, action] = held;
						if (held) light.FadeTo(0.8f, 35);
						else light.FadeOut(Skin.LightFadeMs);
					}
					if (action >= 5) continue;
					Vector2 travel = LaneTravel(row, action + 1);
					Box lane = lanes[row, action];
                    lane.Alpha = 0;
                    if (visible) lane.Alpha = 1;
					lane.Position = glyph.Position - travel;
					lane.Size = new Vector2(travel.Length, Unit);
					lane.Rotation = (float)(Math.Atan2(travel.Y, travel.X) * 180 / Math.PI);
				}
			}
		}
	}
}
