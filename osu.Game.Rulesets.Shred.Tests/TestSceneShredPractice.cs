using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	public partial class TestSceneShredPractice : OsuTestScene
	{
		private const float left = 40;
		private const float pitch = 60;
		private const double approachTime = 1600;
		private readonly Key[,] fretKeys =
		{
			{ Key.A, Key.S, Key.D, Key.F, Key.G },
			{ Key.Z, Key.X, Key.C, Key.V, Key.B },
		};
		private readonly Key[,] pickKeys =
		{
			{ Key.J, Key.K, Key.L, Key.Semicolon },
			{ Key.M, Key.Comma, Key.Period, Key.Slash },
		};
		private readonly string[,] labels =
		{
			{ "A", "S", "D", "F", "G", "J", "K", "L", ";" },
			{ "Z", "X", "C", "V", "B", "M", ",", ".", "/" },
		};
		private readonly Color4[] colours =
		{
			new Color4(100, 80, 225, 255), new Color4(150, 70, 230, 255),
			new Color4(215, 60, 205, 255), new Color4(240, 65, 140, 255),
			new Color4(245, 80, 85, 255),
		};
		private readonly Container stage;
		private readonly Container noteLayer;
		private readonly SpriteText summary;
		private readonly SpriteText feedback;
		private readonly SpriteText scrollLabel;
		private readonly SpriteText[] stringLabels = new SpriteText[2];
		private readonly Box[,] keys = new Box[2, 9];
		private readonly List<Container> noteViews = new List<Container>();
		private readonly HashSet<Key> controlKeys = new HashSet<Key>();
		private ShredPracticeSession session;
		private bool running;
		private double clockOrigin;
		private double scrollSpeed = 1;

		public TestSceneShredPractice()
		{
			stage = new Container
			{
				Anchor = Anchor.Centre,
				Origin = Anchor.Centre,
				Size = new Vector2(800, 740),
			};
			Children = new Drawable[] { stage };
			stage.Add(new Box
			{
				RelativeSizeAxes = Axes.Both,
				Colour = new Color4(14, 16, 27, 255),
			});
			addText("SHRED - playable practice", 30, 20);
			addText("SPACE: start / pause     R: restart     No music / diagnostic +/-120 ms", 30, 48);
			summary = addText("Ready", 30, 78);
			feedback = addText("Normal = pick | H = hammer-on | P = pull-off | wide bar = OPEN", 30, 110);
			scrollLabel = addText("", 400, 600);

			for (int row = 0; row < 2; row++)
			{
				float y = receptor(row);
				for (int fret = 0; fret < 5; fret++)
				{
					float laneTop = y - 160;
					if (row == 1)
						laneTop = y;
					stage.Add(new Box
					{
						Position = new Vector2(left + fret * pitch + 25, laneTop),
						Size = new Vector2(1, 160),
						Colour = new Color4(70, 75, 95, 255),
					});
				}
				stage.Add(new Box
				{
					Position = new Vector2(left, y),
					Size = new Vector2(290, 2),
					Colour = Color4.White,
				});
				for (int column = 0; column < 9; column++)
				{
					float x = left + column * pitch;
					if (column >= 5)
						x += pitch;
					float keyY = y + 8;
					if (row == 1)
						keyY = y - 58;
					Container key = new Container
					{
						Position = new Vector2(x, keyY),
						Size = new Vector2(50),
					};
					keys[row, column] = new Box { RelativeSizeAxes = Axes.Both };
					key.Add(keys[row, column]);
					key.Add(new SpriteText
					{
						Anchor = Anchor.Centre,
						Origin = Anchor.Centre,
						Text = labels[row, column],
					});
					stage.Add(key);
				}
				stringLabels[row] = addText("", 400, y - 95);
			}

			addText("Each string: OPEN -> fret 1 PICK -> fret 3 H -> fret 1 P -> OPEN P", 30, 660);
			addText("P: tap target OR release higher fret. A miss requires a pick. UP/DOWN: scroll.", 30, 690);
			noteLayer = new Container { RelativeSizeAxes = Axes.Both };
			stage.Add(noteLayer);
			session = new ShredPracticeSession(createNotes());
			createNoteViews();
			AddStep("Reset practice (then SPACE)", reset);
		}

		private SpriteText addText(string text, float x, float y)
		{
			SpriteText drawable = new SpriteText
			{
				Position = new Vector2(x, y),
				Text = text,
				Font = new FontUsage(size: 17),
			};
			stage.Add(drawable);
			return drawable;
		}

		private static float receptor(int row)
		{
			if (row == 0)
				return 310;
			return 470;
		}

		private static IEnumerable<ShredPracticeNote> createNotes()
		{
			// Alternate strings so the player can learn one hand sequence at a time.
			for (int round = 0; round < 2; round++)
			{
				for (int row = 0; row < 2; row++)
				{
					double start = 3000 + round * 20000 + row * 10000;
					yield return new ShredPracticeNote(start, row, 0);
					yield return new ShredPracticeNote(start + 1500, row, 1);
					yield return new ShredPracticeNote(start + 3000, row, 3, ShredPracticeArticulation.HammerOn);
					yield return new ShredPracticeNote(start + 4500, row, 1, ShredPracticeArticulation.PullOff);
					yield return new ShredPracticeNote(start + 6000, row, 0, ShredPracticeArticulation.PullOff);
					yield return new ShredPracticeNote(start + 7500, row, 5);
				}
			}
		}

		private void createNoteViews()
		{
			noteLayer.Clear();
			noteViews.Clear();
			foreach (ShredPracticeNote note in session.Notes)
			{
				float width = 46;
				Color4 colour = Color4.White;
				if (note.Fret == 0)
					width = 290;
				else
					colour = colours[note.Fret - 1];
				Container view = new Container
				{
					Size = new Vector2(width, 14),
					Origin = Anchor.CentreLeft,
					Alpha = 0,
				};
				if (note.Articulation != ShredPracticeArticulation.Pick)
				{
					view.Add(new Box
					{
						Position = new Vector2(-5),
						Size = new Vector2(width + 10, 24),
						Colour = new Color4(250, 215, 110, 90),
					});
					string label = "H";
					if (note.Articulation == ShredPracticeArticulation.PullOff)
						label = "P";
					view.Add(new SpriteText
					{
						Position = new Vector2(width + 7, -4),
						Text = label,
						Font = new FontUsage(size: 19),
					});
				}
				view.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = colour });
				noteViews.Add(view);
				noteLayer.Add(view);
			}
		}

		private void reset()
		{
			running = false;
			session = new ShredPracticeSession(createNotes());
			createNoteViews();
		}

		private void advance()
		{
			if (running)
				session.AdvanceTo(Math.Max(session.Time, Time.Current - clockOrigin));
			if (session.Completed)
				running = false;
		}

		protected override bool OnKeyDown(KeyDownEvent e)
		{
			if (e.Key == Key.Up || e.Key == Key.Down)
			{
				if (!controlKeys.Add(e.Key))
					return true;
				if (e.Key == Key.Up)
					scrollSpeed = Math.Min(3, scrollSpeed + 0.25);
				else
					scrollSpeed = Math.Max(0.5, scrollSpeed - 0.25);
				return true;
			}
			if (e.Key == Key.Space || e.Key == Key.R)
			{
				if (!controlKeys.Add(e.Key))
					return true;
				advance();
				if (e.Key == Key.R)
					reset();
				else
				{
					if (session.Completed)
						reset();
					running = !running;
					clockOrigin = Time.Current - session.Time;
				}
				return true;
			}
			advance();
			if (processKey(e.Key, true))
				return true;
			return base.OnKeyDown(e);
		}

		protected override void OnKeyUp(KeyUpEvent e)
		{
			controlKeys.Remove(e.Key);
			advance();
			processKey(e.Key, false);
			base.OnKeyUp(e);
		}

		private bool processKey(Key key, bool pressed)
		{
			for (int row = 0; row < 2; row++)
			{
				for (int fret = 0; fret < 5; fret++)
				{
					if (fretKeys[row, fret] != key)
						continue;
					session.SetFret(row, fret, pressed, running);
					return true;
				}
				for (int pick = 0; pick < 4; pick++)
				{
					if (pickKeys[row, pick] != key)
						continue;
					session.SetPick(row, pick, pressed, running);
					return true;
				}
			}
			return false;
		}

		protected override void Update()
		{
			base.Update();
			stage.Scale = new Vector2(Math.Max(0.1f, Math.Min(DrawWidth / 800, DrawHeight / 740)));
			advance();
			string state = "PAUSED - SPACE to play";
			if (running)
				state = "PLAYING";
			if (session.Completed)
				state = "FINISHED - R to reset";
			summary.Text = $"{state} | {session.Time / 1000:0.0}s | HIT {session.Hits} | MISS {session.Misses} | COMBO {session.Combo}";
			feedback.Text = session.LastEvent;
			scrollLabel.Text = $"Scroll x{scrollSpeed:0.00} | UP / DOWN";
			for (int row = 0; row < 2; row++)
			{
				stringLabels[row].Text = $"String {row + 1}";
				for (int fret = 0; fret < 5; fret++)
				{
					Color4 colour = new Color4(38, 42, 58, 255);
					if (session.GetString(row).IsFretHeld(fret))
						colour = colours[fret];
					if (session.GetString(row).SelectedFret == fret + 1)
						colour = new Color4(185, 140, 35, 255);
					keys[row, fret].Colour = colour;
				}
				for (int pick = 0; pick < 4; pick++)
				{
					Color4 colour = new Color4(38, 42, 58, 255);
					if (session.GetString(row).IsPickHeld(pick))
						colour = new Color4(150, 160, 175, 255);
					keys[row, pick + 5].Colour = colour;
				}
			}
			double visibleTime = approachTime / scrollSpeed;
			for (int index = 0; index < session.Notes.Count; index++)
			{
				ShredPracticeNote note = session.Notes[index];
				Container view = noteViews[index];
				double remaining = note.Time - session.Time;
				view.Alpha = 0;
				if (note.Result != ShredPracticeResult.Pending || remaining > visibleTime)
					continue;
				float x = left;
				if (note.Fret > 0)
					x += (note.Fret - 1) * pitch + 2;
				float distance = (float)(remaining / visibleTime * 160);
				float y = receptor(note.Row) - distance;
				if (note.Row == 1)
					y = receptor(note.Row) + distance;
				view.Position = new Vector2(x, y);
				view.Alpha = 1;
			}
		}
	}
}
