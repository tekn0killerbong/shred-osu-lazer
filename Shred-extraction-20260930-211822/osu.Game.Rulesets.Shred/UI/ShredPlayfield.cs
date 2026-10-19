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
		public ShredPlayfield() { Masking = true; }
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
			else x = Skin.FretCentre(5) + Skin.ColumnWidth[4] / 2 + Skin.SectionGap + 60 + 12 + (fret - 7) * 65;
			if (row == 1) x += Skin.EffectiveRowOffsetBottom;
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
		public double MaximumVisibleLeadTime => 11485 + 500;

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
                : Receiver(note.Row, note.Fret) - LaneTravel(note.Row, note.Fret) * (float)((time - Time.Current) / ApproachTime);
        public void ShowWhamSuccess()
        {
            Drawable flash = new Box { Colour = ShredWhamGeometry.Colour, Origin = Anchor.Centre };
            flash.Position = WhamReceiver;
            flash.Size = WhamSize;
            effects.Add(flash);
            flash.ScaleTo(Skin.HitExpand, Skin.HitFadeMs).FadeOut(Skin.HitFadeMs).Expire();
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
            if (drawableRuleset.IsPaused.Value) return false;
            if (e.Action == ShredAction.Wham)
            {
                if (whamHeld) return true;
                whamHeld = true;
                foreach (var overdue in AllHitObjects.OfType<DrawableShredHitObject>().Where(n => n.HitObject.IsWham).ToArray()) overdue.CheckExpiry();
                var next = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => n.HitObject.IsWham && !n.HeadJudged).OrderBy(n => n.HitObject.StartTime).FirstOrDefault();
                if (next != null)
                {
                    if (!ShredInputJudgement.IsTooEarlyToTarget(next.HitObject.HitWindows, next.HeadOffset)) next.TryHit();
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
                strings[row].SetFret(action, true);
                tryJudge(row, false, true, previous);
            }
            else if (strings[inputRow].SetPick(action - 5, true))
            {
                tracePick(e.Action, "down");
                tryJudge(row, true, true, previous);
            }
            return true;
        }
        public void OnReleased(KeyBindingReleaseEvent<ShredAction> e)
        {
            if (e.Action == ShredAction.ScrollFaster || e.Action == ShredAction.ScrollSlower) { scrollDirection = 0; return; }
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
                }
            }
            else
            {
                strings[inputRow].SetPick(action - 5, false);
                tracePick(e.Action, "up");
            }
        }
		private void tracePick(ShredAction action, string transition)
		{
			// Diagnostic state only: no typed text or unrelated keyboard keys are recorded.
			Logger.Log($"Shred pick {action} {transition}; masks row1={strings[0].PickMask}, row2={strings[1].PickMask}", LoggingTarget.Runtime, LogLevel.Debug);
		}
        private void tryJudge(int row, bool pick, bool pressed, int previous)
        {
            if (drawableRuleset.IsPaused.Value) return;
            var rowNotes = AllHitObjects.OfType<DrawableShredHitObject>().Where(n => !n.HitObject.IsWham && n.HitObject.Row == row).ToArray();
            foreach (var overdue in rowNotes) overdue.CheckExpiry();
            int held = strings[row].FretMask;
            int sustaining = rowNotes.Where(n => n.IsHolding).Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));

            // A pick targets one chord and keeps its original timestamp.
            // Both missing and surplus frets may be corrected before expiry.
            var chord = armed[row];
            if (chord != null)
            {
                int required = chord.Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
                if (Time.Current < armedPickTime[row] || chord.Any(n => n.HeadJudged)) armed[row] = null;
                else if (ShredChordRules.IsExact(held, required, sustaining))
                {
                    foreach (var n in chord) n.TryHit(armedPickTime[row]);
                    armed[row] = null;
                    return;
                }
                else return;
            }
            var first = rowNotes.Where(n => !n.HeadJudged).OrderBy(n => n.HitObject.StartTime).FirstOrDefault();
            if (first == null)
            {
                if (pick) emptyPick(row);
                return;
            }
            if (pick && ShredInputJudgement.IsTooEarlyToTarget(first.HitObject.HitWindows, first.HeadOffset))
            {
                emptyPick(row);
                return;
            }
            chord = rowNotes.Where(n => !n.HeadJudged && Math.Abs(n.HitObject.StartTime - first.HitObject.StartTime) < 0.01).ToArray();
            int wanted = chord.Aggregate(0, (mask, n) => mask | ShredChordRules.MaskFor(n.HitObject.Fret));
            if (pick)
            {
                // Picks inside the miss-only band cannot award a hit, but
                // must not consume a future note. Let its normal expiry decide.
                if (!first.HitObject.HitWindows.ResultFor(first.HeadOffset).IsHit()) return;
                if (!ShredChordRules.IsExact(held, wanted, sustaining))
                {
                    armed[row] = chord;
                    armedPickTime[row] = Time.Current;
                    return;
                }
            }
            else
            {
                if (chord.Any(n => n.HitObject.Articulation == ShredPracticeArticulation.Pick)
                    || !ShredChordRules.IsExact(held, wanted, sustaining)
                    || !ShredChordRules.IsHammerPull(previous, held, wanted, pressed, sustaining)) return;
            }
            foreach (var n in chord) n.TryHit();
        }

        private void emptyPick(int row)
        {
            // Empty strums have no scoring or chain penalty.
            armed[row] = null;
        }

		public void ShowSuccess(ShredHitObject note)
		{
			int row = note.Row;
			int fret = note.Fret;
			Color4 colour = Color4.White;
			if (fret > 0) colour = FretColour(fret);
			if (note.IsLong) colour = ShredNoteVisual.SustainColour(colour);
			Drawable flash = new ShredGlyph(colour, shear: Skin.Shear(row), solid: true) { Origin = Anchor.Centre };
			flash.Position = Receiver(row, fret);
			flash.Size = NoteSize(fret);
			effects.Add(flash);
			flash.ScaleTo(Skin.HitExpand, Skin.HitFadeMs, Easing.OutQuint);
			flash.FadeOut(Skin.HitFadeMs).Expire();
		}
		public void ShowWrongFret(int row, int fret)
		{
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
			bool active = whamHeld && AllHitObjects.OfType<DrawableShredHitObject>().Any(n => n.HitObject.IsWham && n.IsHolding);
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
