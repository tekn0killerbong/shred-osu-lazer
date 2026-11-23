using System;
using System.Collections.Generic;
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
        private readonly Container lights = new Container { RelativeSizeAxes = Axes.Both };
        private readonly Container fretLights = new Container { RelativeSizeAxes = Axes.Both };
		private readonly Container effects = new Container { RelativeSizeAxes = Axes.Both };
		private readonly Drawable[,] stageLights = new Drawable[2, 9];
        public const float PressedLightOpacity = 0.8f;
        private readonly ShredGlow[,] fretOutlines = new ShredGlow[2, 5];
		private readonly bool[,] lightHeld = new bool[2, 9];
		private readonly double[,] shakeStarts = new double[2, 5];
		private readonly bool[,] shaking = new bool[2, 5];
		private readonly Container stage = new Container { RelativeSizeAxes = Axes.Both };
		private readonly Box whamLine = new Box { Origin = Anchor.Centre, Colour = ShredWhamGeometry.Colour };
		// Keep the contact light above both sustain bodies and the existing hit flashes.
		private readonly ShredWhamGrind whamGrind = new ShredWhamGrind { Depth = -1 };
		private readonly DrawableShredHitObject[][] armed = new DrawableShredHitObject[2][];
		private bool whamHeld;
		private bool whamDepressed;
		private int scrollDirection;
		private double nextScrollRepeat;
		private readonly double[] armedPickTime = new double[2];
        private readonly int[] armedPickCount = new int[2];
        private readonly ShredFretSequence sequence = new ShredFretSequence();
        private readonly ShredPickSequence pickSequence = new ShredPickSequence();
        private readonly ShredStrumHistory strumHistory = new ShredStrumHistory();
        private readonly ShredPickSequence.Gesture[] armedGesture = new ShredPickSequence.Gesture[2];
        private readonly ShredPickSequence.Gesture[] armedPartner = new ShredPickSequence.Gesture[2];
        private readonly bool[] armedReturn = new bool[2];
        private readonly int[] armedPhysicalPicks = new int[2];
        private sealed class BufferedRowPick
        {
            public int Row;
            public int Count;
            public int AnchorMask;
            public DrawableShredHitObject[] Chord;
            public ShredPickSequence.Gesture Gesture;
            public ShredPickSequence.Gesture Partner;
            public readonly List<DrawableShredHitObject> Ready = new List<DrawableShredHitObject>();
        }
        private readonly ShredPickBuffer<BufferedRowPick[]> pickBuffer = new ShredPickBuffer<BufferedRowPick[]>();
        private double lastInputTime = double.NegativeInfinity;
		private readonly SpriteText feedback = new SpriteText { Anchor = Anchor.BottomCentre, Origin = Anchor.BottomCentre, Y = -30 };
		private readonly Container receptors = new Container { RelativeSizeAxes = Axes.Both };
		private readonly ShredGlyph[,] glyphs = new ShredGlyph[2, 9];
		private readonly Box[] sectionLines = new Box[6];
		private readonly Box[,] lanes = new Box[2, 5];
		public static double ApproachTime => ShredScrollSpeed.ApproachTime;
		public double PlaybackRate => ShredScrollSpeed.ValidPlaybackRate(Clock?.Rate ?? 1);
		public double MapApproachTime => ShredScrollSpeed.MapDuration(ApproachTime, PlaybackRate);
		private double maximumScrollLead => 11485 * Math.Max(ShredScrollSpeed.MaximumPlaybackRate, PlaybackRate);
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
		public float WhamTravel
		{
			get
			{
				if (GuitarHero) return Travel(0).Length;
				// Use a regular lane's distance to derive pixels/ms. Whams may
				// enter earlier because their horizontal path is longer.
				return LaneTravel(0, 3).Length;
			}
		}
		public double MaximumVisibleLeadTime
		{
			get
			{
				if (GuitarHero) return maximumScrollLead * Math.Max(1, DrawSize.Length / Math.Max(1, Travel(1).Length)) + 500;
				return maximumScrollLead + 500;
			}
		}
		public double MaximumWhamVisibleLeadTime
		{
			get
			{
				float distance = Math.Max(0, DrawWidth - WhamReceiver.X);
				double factor = Math.Max(1, distance / Math.Max(1, WhamTravel));
				return maximumScrollLead * factor + 500;
			}
		}

		[BackgroundDependencyLoader]
		private void load(GameHost host, ISkinSource skinSource)
		{
			if (GuitarHero) { Skin.AlignRows = false; Skin.BottomAngle = Skin.TopAngle; }
			Assets = new ShredSkinAssets(Skin, host, skinSource);
			AddInternal(stage);
			stage.Add(lights);
			receptors.Add(whamLine);
			stage.Add(receptors);
			for (int index = 0; index < sectionLines.Length; index++)
			{
				sectionLines[index] = new Box { Colour = new Color4(180, 175, 210, 120) };
				receptors.Add(sectionLines[index]);
			}
			stage.Add(HitObjectContainer);
            // Fret halos must remain visible where descending sustain bodies cross them.
            stage.Add(fretLights);
			stage.Add(effects);
			effects.Add(whamGrind);
			stage.Add(feedback);
			for (int row = 0; row < 2; row++)
			{
				for (int action = 0; action < 9; action++)
				{
					Color4 colour = new Color4(160,145,200,255);
					if (action < 5)
						colour = FretColour(action + 1);
					ShredGlyph glyph = new ShredGlyph(colour, action >= 5 && !GuitarHero, Skin.Shear(row), lightOnly: action >= 5) { Origin = Anchor.Centre };
					glyphs[row, action] = glyph;
					receptors.Add(glyph);
					Texture[] lightFrames = Assets.GetFrames("shred-stage-light");
					// Use the Shred halo when no dedicated texture exists; an active
					// mania skin must not replace the fret-string lighting.
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
					// Restore the v39 backing light: its additive fill brightens the
                    // translucent fret colour from behind, without covering notes.
                    lights.Add(light);
                    // Only the separate hollow outline belongs in front of notes.
                    if (action < 5)
                    {
                        var outline = new ShredGlow(Skin.ColourLight, Skin.HaloRadius, Skin.Shear(row), hollow: true)
                        { Origin = Anchor.Centre, Alpha = 0 };
                        fretOutlines[row, action] = outline;
                        fretLights.Add(outline);
                    }
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
        public Vector2 WhamSize => ShredWhamGeometry.BarSize(Skin, Unit);
        public Vector2 PositionAt(ShredHitObject note, double time)
            => note.IsWham
                ? WhamReceiver + new Vector2(WhamTravel * (float)((time - Time.Current) / MapApproachTime), 0)
                : Receiver(note.Row, note.Fret) - (GuitarHero ? Travel(note.Row) : LaneTravel(note.Row, note.Fret)) * (float)((time - Time.Current) / MapApproachTime);
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
                    if (next.HitObject.TimingWindows.ResultFor(next.HeadOffset) == HitResult.Miss)
                    {
                        next.MissHead(Time.Current);
                        ShowWhamError();
                    }
                    else if (!ShredInputJudgement.IsTooEarlyToTarget(next.HitObject.TimingWindows, next.HeadOffset)) next.TryHit();
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
                sequence.Press(action, previous, row);
                strings[row].SetFret(action, true);
                traceHeldState(e.Action, "down");
                capturePendingFrets();
                tryJudge(row, false, true, previous);
                if (armed[1 - row] != null) tryJudge(1 - row, false, true, strings[1 - row].FretMask);
                completeFullStrumPress();
                tryBeginAnchoredStrum();
            }
            else if (strings[inputRow].SetPick(action - 5, true))
            {
                pickBuffer.PhysicalPress(Time.Current, commitBufferedPull);
                tracePick(e.Action, "down");
                traceHeldState(e.Action, "down");
                int bit = 1 << (inputRow * 4 + action - 5);
                if (isExcessChordPress(bit)) return true;
                pickSequence.Press(inputRow, action - 5, currentPickTarget(row), Time.Current, Clock?.Rate ?? 1);
                if (!tryFullStrumPress(bit))
                {
                    tryJudge(row, true, true, previous, bit);
                    tryBeginAnchoredStrum();
                }
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
                strumHistory.ForgetFret(row, 1 << action);
                pickSequence.ForgetFret(row, 1 << action);
                traceHeldState(e.Action, "up");
                if (!drawableRuleset.IsPaused.Value)
                {
                    capturePendingFrets();
                    foreach (var note in AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham && n.HitObject.Row == row && n.HitObject.Fret == action + 1 && n.IsHolding).ToArray()) note.Release();
                    tryJudge(row, false, false, previous);
                    // A fret release can judge only its own row. Pick releases
                    // continue through their independent cross-row buffer below.
                }
            }
            else
            {
                if (!strings[inputRow].IsPickHeld(action - 5)) return;
                strings[inputRow].SetPick(action - 5, false);
                tracePick(e.Action, "up");
                traceHeldState(e.Action, "up");
                int revealed = pickSequence.Release(inputRow, action - 5, out var released);
                int heldPicks = strings[0].PickMask | (strings[1].PickMask << 4);
                bool special = fullStrumRelease(1 << (inputRow * 4 + action - 5), heldPicks);
                if (!special && revealed != 0 && !drawableRuleset.IsPaused.Value) queuePull(revealed, released);
                pickBuffer.CancelWhere(rows => rows.Any(g => (g.AnchorMask & heldPicks) == 0));
                if (pickCountForRow(heldPicks, row) == 0) strumHistory.ResetRow(row);
            }
        }
        private int diagnosticHeldEvents;
        private void traceHeldState(ShredAction action, string transition)
        {
            if (diagnosticHeldEvents++ >= 128) return;
            Logger.Log($"Shred state v34 {action} {transition}; frets={strings[0].FretMask}/{strings[1].FretMask}; picks={strings[0].PickMask}/{strings[1].PickMask}", LoggingTarget.Runtime, LogLevel.Verbose);
        }
		private void tracePick(ShredAction action, string transition)
		{
			// Diagnostic state only: no typed text or unrelated keyboard keys are recorded.
			ShredBindingProfile.TryDecode(action, out int row, out _);
            if (GuitarHero) row = 1;
            var nearest = AllHitObjects.OfType<DrawableShredHitObject>()
                .Where(n => !n.HitObject.IsWham && n.HitObject.Row == row)
                .MinBy(n => Math.Abs(n.HitObject.StartTime - Time.Current));
            string timing = "no nearby note";
            if (nearest != null)
                timing = $"note={nearest.HitObject.StartTime:0.###}, offset={nearest.HeadOffset:0.###}, great={nearest.HitObject.TimingWindows.WindowFor(HitResult.Great):0.###}, judged={nearest.HeadJudged}, hit={nearest.HeadHit}";
            Logger.Log($"Shred pick {action} {transition}; time={Time.Current:0.###}, rate={Clock?.Rate:0.###}; {timing}; frets={strings[0].FretMask}/{strings[1].FretMask}; masks row1={strings[0].PickMask}, row2={strings[1].PickMask}", LoggingTarget.Runtime, LogLevel.Debug);
		}
        private double? currentPickTarget(int row)
        {
            var notes = AllHitObjects.OfType<DrawableShredHitObject>().ToArray();
            foreach (var note in notes) note.CheckExpiry();
            var next = targetNote(notes.Where(n => n.HitObject.Row == row));
            return next != null && ShredInputJudgement.IsWrongPickInWindow(next.HitObject.TimingWindows, next.HeadOffset)
                ? next.HitObject.StartTime : null;
        }

        private void prepareInputTime()
        {
            if (drawableRuleset.IsPaused.Value)
            {
                strumHistory.Reset();
                resetChordInput();
            }
            if (Time.Current < lastInputTime)
            {
                resetChordInput();
                sequence.Reset();
                pickSequence.Reset();
                pickBuffer.Reset();
                strumHistory.Reset();
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

        // Only frets already validated in Great are protected across the buffer.
        // Waiting for a missing fret never extends the late Great boundary.
        public bool HasPendingPick(DrawableShredHitObject note)
            => pickBuffer.PendingValues.SelectMany(rows => rows).Any(row => row.Ready.Contains(note));
        public bool HasPendingPick(ShredHitObject note)
            => pickBuffer.PendingValues.SelectMany(rows => rows).Any(row => row.Ready.Any(target => target.HitObject == note));

        private int pickCountForRow(int mask, int row)
        {
            if (!GuitarHero) mask &= 15 << (row * 4);
            return System.Numerics.BitOperations.PopCount((uint)mask);
        }

        private int strumPartnerFrets(int row, DrawableShredHitObject[] chord,
            ShredPickSequence.Gesture gesture, ShredPickSequence.Gesture partner, bool returning, out int otherPicks)
        {
            otherPicks = 0;
            if (gesture == null || partner == null || pickCountForRow(gesture.Mask, row) == 0) return 0;
            if (chord.Any(n => n.HitObject.Fret == 0 || n.HitObject.Articulation != ShredPracticeArticulation.Pick)) return 0;
            int required = chord.Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            if (returning && gesture.FretsFor(row) != required) return 0;
            int other = partner.FretsFor(row);
            if (!strumHistory.CanAlternate(row, required, other, strings[row].FretMask)) return 0;
            int pickMask = partner.Mask;
            if (returning) pickMask = partner.OriginalMask;
            otherPicks = pickCountForRow(pickMask, row);
            if (otherPicks == 0) return 0;
            return other;
        }

        private int strumCapacity(int row, DrawableShredHitObject[] chord, int picks,
            ShredPickSequence.Gesture gesture, ShredPickSequence.Gesture partner, bool returning)
        {
            int other = strumPartnerFrets(row, chord, gesture, partner, returning, out int otherPicks);
            if (other == 0) return picks;
            int required = chord.Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            int availablePicks = 4;
            if (GuitarHero) availablePicks = 8;
            return strumHistory.Capacity(required, other, picks, otherPicks, availablePicks);
        }

        private DrawableShredHitObject[] eligibleChordMembers(int row, DrawableShredHitObject[] chord,
            ShredPickSequence.Gesture gesture = null, ShredPickSequence.Gesture partner = null, bool returning = false)
        {
            var notes = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham).ToArray();
            int held = strings[row].FretMask;
            int fullMask = chord.Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            int sustaining = notes.Where(n => n.HitObject.Row == row && (n.IsHolding || (chord.Contains(n) && n.HeadHit)))
                .Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            int partial = chord.Where(n => !n.HeadJudged).Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret)) & held;
            // Held frets inside a regular picked chord are harmless, even when
            // its members arrive one at a time. HP priority remains unchanged.
            int regularMask = chord.Where(n => n.HitObject.Articulation == ShredPracticeArticulation.Pick)
                .Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            sustaining |= ShredChordRules.InteriorMask(regularMask);
            sustaining |= strumPartnerFrets(row, chord, gesture, partner, returning, out _);
            // Missing chord members do not block the correct ones.
            if (fullMask != 0)
            {
                int left = 0, right = 4;
                while ((fullMask & (1 << left)) == 0) left++;
                while ((fullMask & (1 << right)) == 0) right--;
                int span = ((1 << (right + 1)) - 1) & ~((1 << left) - 1);
                if ((held & ~fullMask & ~sustaining & span) != 0) return Array.Empty<DrawableShredHitObject>();
            }
            if (!sequence.IsExact(held, partial, sustaining, row))
                return Array.Empty<DrawableShredHitObject>();
            return chord.Where(n => !n.HeadJudged && n.HitObject.TimingWindows.ResultFor(n.HeadOffset).IsHit()
                    && (n.HitObject.Fret == 0 || (partial & ShredChordRules.MaskFor(n.HitObject.Fret)) != 0))
                .OrderBy(n => sequence.PressOrder(n.HitObject.Fret, row)).ThenBy(n => n.HitObject.Fret).ToArray();
        }

        private void captureFrets(BufferedRowPick gesture)
        {
            foreach (var note in eligibleChordMembers(gesture.Row, gesture.Chord, gesture.Gesture, gesture.Partner, returning: true))
            {
                if (gesture.Ready.Count >= gesture.Count) break;
                if (!gesture.Ready.Contains(note)) gesture.Ready.Add(note);
            }
        }

        private void capturePendingFrets()
        {
            foreach (var gesture in pickBuffer.PendingValues.SelectMany(rows => rows)) captureFrets(gesture);
        }

        private void queuePull(int revealed, ShredPickSequence.Gesture released)
        {
            var notes = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham).ToArray();
            foreach (var note in notes) note.CheckExpiry();
            var gestures = new List<BufferedRowPick>();
            double nearestGap = double.PositiveInfinity;
            for (int row = 0; row < 2; row++)
            {
                if (GuitarHero && row == 0) continue;
                int count = System.Numerics.BitOperations.PopCount((uint)(GuitarHero ? revealed : (revealed >> (row * 4)) & 15));
                if (count == 0) continue;
                var first = targetNote(notes.Where(n => n.HitObject.Row == row));
                if (first == null || !first.HitObject.TimingWindows.ResultFor(first.HeadOffset).IsHit()) continue;
                var chord = notes.Where(n => n.HitObject.Row == row && ShredChordRules.SameChordTime(n.HitObject.StartTime, first.HitObject.StartTime)).ToArray();
                count = strumCapacity(row, chord, count, pickSequence.Active, released, returning: true);
                var gesture = new BufferedRowPick
                {
                    Row = row, Count = Math.Min(count, chord.Count(n => !n.HeadJudged)), Chord = chord, AnchorMask = revealed,
                    Gesture = pickSequence.Active, Partner = released,
                };
                captureFrets(gesture);
                gestures.Add(gesture);
                foreach (var note in notes.Where(n => n.HitObject.Row == row))
                {
                    double gap = Math.Abs(note.HitObject.StartTime - first.HitObject.StartTime);
                    if (gap >= ShredChordRules.ChordTolerance) nearestGap = Math.Min(nearestGap, gap);
                }
            }
            if (gestures.Count == 0) return;
            double window = ShredPickBuffer<BufferedRowPick[]>.WindowFor(nearestGap, Clock?.Rate ?? 1);
            pickBuffer.Queue(Time.Current, window, gestures.ToArray());
        }

        private void commitBufferedPull(BufferedRowPick[] gestures, double releaseTime)
        {
            foreach (var gesture in gestures)
            {
                int row = gesture.Row;
                int remaining = gesture.Count;
                foreach (var note in gesture.Ready)
                {
                    // A note independently played by a fret HP still consumes
                    // its reserved pick; it cannot pay another note as well.
                    remaining--;
                    note.TryHit(releaseTime, picked: true);
                }
                foreach (var note in gesture.Chord) note.CheckExpiry();
                if (remaining <= 0 || gesture.Chord.All(n => n.HeadJudged)) continue;
                if (armed[row] == null || !armed[row].Intersect(gesture.Chord).Any())
                {
                    armed[row] = gesture.Chord;
                    armedPickCount[row] = gesture.Chord.Count(n => n.HeadHit);
                    armedGesture[row] = gesture.Gesture;
                    armedPartner[row] = gesture.Partner;
                    armedReturn[row] = true;
                    armedPhysicalPicks[row] = 0;
                }
                armedPickTime[row] = releaseTime;
                armedPickCount[row] = Math.Max(armedPickCount[row], gesture.Count);
                judgeArmedChord(row);
            }
        }

        private void judgeArmedChord(int row)
        {
            var chord = armed[row];
            if (chord == null) return;
            int capacity = armedPickCount[row];
            if (!armedReturn[row])
                capacity = strumCapacity(row, chord, capacity, armedGesture[row], armedPartner[row], returning: false);
            int available = capacity - chord.Count(n => n.HeadHit && !freeHammerPull(n));
            foreach (var note in eligibleChordMembers(row, chord, armedGesture[row], armedPartner[row], armedReturn[row])
                .Where(n => !freeHammerPull(n)).Take(Math.Max(0, available)))
                note.TryHit(armedPickTime[row], picked: true);
            if (chord.All(n => n.HeadHit))
            {
                armed[row] = null;
                armedPickCount[row] = 0;
            }
        }

        private void judgeFretHammerPulls(int row, DrawableShredHitObject[] chord, int previous, bool pressed, int sustaining)
        {
            var hammerPulls = chord.Where(n => n.HitObject.Articulation != ShredPracticeArticulation.Pick).ToArray();
            if (hammerPulls.Length == 0) return;
            int wanted = hammerPulls.Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            // Regular members of this same chord do not obstruct its HP frets,
            // but still require their own picks. Only a gesture on this row can hit HPs.
            sustaining |= chord.Where(n => n.HitObject.Articulation == ShredPracticeArticulation.Pick)
                .Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            if (!sequence.IsHammerPull(previous, strings[row].FretMask, wanted, pressed, sustaining, row)) return;
            if (hammerPulls.Any(n => !n.HeadJudged && n.HitObject.TimingWindows.ResultFor(n.HeadOffset) == HitResult.Miss))
            {
                missChord(chordAt(hammerPulls[0]), Time.Current);
                return;
            }
            foreach (var note in hammerPulls.Where(n => !n.HeadJudged))
            {
                note.TryHit();
            }
        }

        private void tryJudge(int row, bool pick, bool pressed, int previous, int physicalPick = 0)
        {
            if (drawableRuleset.IsPaused.Value) return;
            // Resolve overdue notes before choosing the next playable chord.
            var allNotes = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham).ToArray();
            foreach (var overdue in allNotes) overdue.CheckExpiry();
            var rowNotes = allNotes.Where(n => n.HitObject.Row == row).ToArray();
            int sustaining = rowNotes.Where(n => n.IsHolding).Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            var chord = armed[row];
            if (chord != null && (chord.All(n => n.HeadJudged) || chord.Any(n => n.HeadJudged && !n.HeadHit)
                || chord.All(n => n.HeadOffset > n.HitObject.TimingWindows.WindowFor(HitResult.Great))))
            {
                armed[row] = chord = null;
                armedPickCount[row] = 0;
            }
            if (chord == null)
            {
                var first = targetNote(rowNotes);
                if (first == null) { if (pick) emptyPick(row); return; }
                if (!ShredInputJudgement.IsWrongPickInWindow(first.HitObject.TimingWindows, first.HeadOffset)) return;
                chord = rowNotes.Where(n => !n.HeadJudged && ShredChordRules.SameChordTime(n.HitObject.StartTime, first.HitObject.StartTime)).ToArray();
            }
            var pending = chord.Where(n => !n.HeadJudged).ToArray();
            if (pick && pending.Length > 0
                && pending[0].HitObject.TimingWindows.ResultFor(pending[0].HeadOffset) == HitResult.Miss)
            {
                var target = pending[0];
                missChord(chordAt(target), Time.Current);
                ShowWrongFret(row, target.HitObject.Fret);
                armed[row] = null;
                armedPickCount[row] = 0;
                return;
            }
            if (pending.Length == 0) return;
            bool currentHitWindow = pending[0].HitObject.TimingWindows.ResultFor(pending[0].HeadOffset).IsHit();
            bool validArmedPick = armed[row] != null && pending[0].HitObject.TimingWindows
                .ResultFor(armedPickTime[row] - pending[0].HitObject.StartTime).IsHit();
            if (!pick) judgeFretHammerPulls(row, chord, previous, pressed, sustaining);
            if (!currentHitWindow && !validArmedPick) return;
            if (pick)
            {
                if (armed[row] == null || armedReturn[row]) armedPhysicalPicks[row] = 0;
                if (armed[row] == null) armedPickCount[row] = 0;
                armed[row] = chord;
                armedPickTime[row] = Time.Current;
                if ((armedPhysicalPicks[row] & physicalPick) == 0) armedPickCount[row]++;
                armedGesture[row] = pickSequence.Active;
                armedPartner[row] = pickSequence.PreviousFor(row, GuitarHero);
                armedReturn[row] = false;
                armedPhysicalPicks[row] |= physicalPick;
            }
            if (armed[row] != null) judgeArmedChord(row);
        }

        private void emptyPick(int row)
        {
            // Empty strums have no scoring penalty.
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
			bool whamGrinding = false;
            if (!drawableRuleset.IsPaused.Value)
            {
                foreach (DrawableShredHitObject note in AllHitObjects.OfType<DrawableShredHitObject>().Where(n => n.IsHolding).ToArray())
                {
                    bool held = whamHeld;
                    if (!note.HitObject.IsWham && note.HitObject.Fret > 0)
                        held = strings[note.HitObject.Row].IsFretHeld(note.HitObject.Fret - 1);
                    if (!held) note.Release();
					else if (note.HitObject.IsWham && Time.Current >= note.HitObject.StartTime && Time.Current < note.HitObject.EndTime)
						whamGrinding = true;
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
            if (GuitarHero) foreach (Box line in sectionLines) line.Alpha = 0;
			whamLine.Alpha = GuitarHero ? 0 : 1;
			whamLine.Position = WhamLineCentre;
			whamLine.Size = new Vector2(WhamLineWidth, ShredWhamGeometry.LineHeight(Skin) * Unit);
			whamLine.Shear = new Vector2(Skin.Shear(0), 0);
			whamGrind.Layout(whamLine.Position, whamLine.Size, Unit, Skin.Shear(0));
			whamGrind.Animate(Time.Current, whamHeld, whamGrinding, !GuitarHero && !drawableRuleset.IsPaused.Value);
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
						if (held) light.FadeTo(PressedLightOpacity, 35);
						else light.FadeOut(Skin.LightFadeMs);
					}
                    if (action < 5 && fretOutlines[row, action] != null)
                    {
                        var outline = fretOutlines[row, action];
                        outline.Position = glyph.Position;
                        outline.Size = glyph.Size;
                        outline.Alpha = light.Alpha;
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
