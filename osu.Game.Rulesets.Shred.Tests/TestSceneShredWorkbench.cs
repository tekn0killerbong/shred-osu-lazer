using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Shred.Gameplay;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.Shred.Tests
{
	[TestFixture]
	public partial class TestSceneShredWorkbench : OsuTestScene
	{
		private readonly Container stage;
		private readonly Container field;
		private readonly Container noteLayer;
		private readonly SpriteText summary;
		private readonly SpriteText feedback;
		private readonly SpriteText notice;
		private readonly SpriteText settings;
		private readonly SpriteText helpOne;
		private readonly SpriteText helpTwo;
		private readonly SpriteText limits;
		private readonly SpriteText[] stringLabels = new SpriteText[2];
		private readonly ClickCell[,] cells = new ClickCell[2, 9];
		private readonly List<Container> noteViews = new List<Container>();
		private readonly HashSet<Key> heldKeys = new HashSet<Key>();
		private ShredPracticeBindings rightBindings = new ShredPracticeBindings(false);
		private ShredPracticeBindings leftBindings = new ShredPracticeBindings(true);
		private ShredPracticeBindings bindings;
		private ShredPracticeSession session;
		private bool leftHanded;
		private bool running;
		private double clockOrigin;
		private double scrollSpeed = 1;
		private int captureRow = -1;
		private int captureAction = -1;
		private readonly Color4[] fretColours =
		{
			new Color4(100, 80, 225, 255), new Color4(150, 70, 230, 255),
			new Color4(215, 60, 205, 255), new Color4(240, 65, 140, 255),
			new Color4(245, 80, 85, 255),
		};

		public TestSceneShredWorkbench()
		{
			bindings = rightBindings;
			session = new ShredPracticeSession(createNotes());
			stage = new Container
			{
				Anchor = Anchor.Centre,
				Origin = Anchor.Centre,
				Size = new Vector2(1000, 820),
			};
			Children = new Drawable[] { stage };
			stage.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(14, 16, 27, 255) });
			addText(stage, "SHRED / TWO-STRING WORKBENCH", 25, 12, 23);
			addText(stage, "F5 play/pause | F6 restart | F7 slower | F8 faster | click a key to rebind", 25, 44);
			button("Play / pause", 25, 75, 130, togglePlayback);
			button("Restart", 165, 75, 100, reset);
			button("Right / Left", 275, 75, 140, switchHand);
			button("Defaults", 425, 75, 110, restoreDefaults);
			button("Slower", 545, 75, 100, () => changeSpeed(-0.25));
			button("Faster", 655, 75, 100, () => changeSpeed(0.25));
			button("Cancel bind", 765, 75, 160, cancelCapture);
			settings = addText(stage, "", 25, 119);
			summary = addText(stage, "", 25, 145);
			notice = addText(stage, "Exactly 2 strings. Click any fret or pick to change its key.", 25, 174);
			feedback = addText(stage, "", 25, 695);
			helpOne = addText(stage, "F1..F5 = fret ranks; P1..P4 = picks. H = hammer-on; P = pull-off; wide bar = OPEN.", 25, 729);
			helpTwo = addText(stage, "Pull-off: tap target OR release a higher fret. Highest fret rank wins, including left-handed.", 25, 756);
			limits = addText(stage, "No music | diagnostic +/-120 ms | QWERTY geometry | bindings kept only while this scene stays open", 25, 783, 15);
			field = new Container { RelativeSizeAxes = Axes.Both };
			noteLayer = new Container { RelativeSizeAxes = Axes.Both };
			stage.Add(field);
			stage.Add(noteLayer);
			buildField();
			buildNotes();
			AddStep("Reset workbench", reset);
		}

		private static SpriteText addText(Container parent, string text, float x, float y, float size = 17)
		{
			SpriteText drawable = new SpriteText
			{
				Position = new Vector2(x, y), Text = text, Font = new FontUsage(size: size),
			};
			parent.Add(drawable);
			return drawable;
		}

		private void button(string text, float x, float y, float width, Action action)
		{
			stage.Add(new ClickCell(text, action)
			{
				Position = new Vector2(x, y), Size = new Vector2(width, 32),
			});
		}

		private void physicalRange(int row, out float minimum, out float maximum)
		{
			minimum = float.MaxValue;
			maximum = float.MinValue;
			for (int action = 0; action < 9; action++)
			{
				float y = bindings.GetPosition(row, action).Y;
				minimum = Math.Min(minimum, y);
				maximum = Math.Max(maximum, y);
			}
		}

		private float rowY(int row)
		{
			if (row == 0)
				return 350;
			physicalRange(0, out float minimum, out float maximum);
			return 510 + (maximum - minimum) * 46;
		}

		private Vector2 target(int row, int action)
		{
			Vector2 physical = bindings.GetPosition(row, action);
			physicalRange(row, out float minimum, out float maximum);
			return new Vector2(100 + physical.X * 65 + 26, rowY(row) + (physical.Y - minimum) * 46);
		}

		private void fretBounds(int row, out float minimum, out float maximum)
		{
			minimum = float.MaxValue;
			maximum = float.MinValue;
			for (int fret = 0; fret < 5; fret++)
			{
				float x = target(row, fret).X;
				minimum = Math.Min(minimum, x - 26);
				maximum = Math.Max(maximum, x + 26);
			}
		}

		private void buildField()
		{
			field.Clear();
			physicalRange(1, out float bottomMinimum, out float bottomMaximum);
			float footerY = rowY(1) + (bottomMaximum - bottomMinimum) * 46 + 182;
			feedback.Y = footerY;
			helpOne.Y = footerY + 32;
			helpTwo.Y = footerY + 59;
			limits.Y = footerY + 86;
			stage.Height = footerY + 117;
			field.Add(new Box { Position = new Vector2(25, rowY(1) - 80), Size = new Vector2(950, 1), Colour = new Color4(130, 135, 150, 255) });
			for (int row = 0; row < 2; row++)
			{
				stringLabels[row] = addText(field, "", 25, rowY(row) - 4, 14);
				for (int action = 0; action < 9; action++)
				{
					Vector2 receptor = target(row, action);
					if (action < 5)
					{
						Vector2 travel = new Vector2(-70, 150);
						if (row == 1)
							travel = new Vector2(70, -150);
						field.Add(new Box
						{
							Position = receptor - travel,
							Size = new Vector2(travel.Length, 1),
							Rotation = (float)(Math.Atan2(travel.Y, travel.X) * 180 / Math.PI),
							Colour = new Color4(100, 105, 125, 170),
						});
						field.Add(new Box { Position = receptor - new Vector2(26, 0), Size = new Vector2(52, 2), Colour = Color4.White });
					}
					float keyOffset = 9;
					if (row == 1)
						keyOffset = -53;
					int selectedRow = row;
					int selectedAction = action;
					string rank = "F" + (action + 1);
					if (action >= 5)
						rank = "P" + (action - 4);
					ClickCell cell = new ClickCell(bindings.GetLabel(row, action), () => beginCapture(selectedRow, selectedAction))
					{
						Position = receptor + new Vector2(-26, keyOffset), Size = new Vector2(52, 42),
					};
					cell.Add(new SpriteText { Position = new Vector2(3, 2), Text = rank, Font = new FontUsage(size: 11) });
					cells[row, action] = cell;
					field.Add(cell);
				}
			}
		}

		private static IEnumerable<ShredPracticeNote> createNotes()
		{
			for (int row = 0; row < 2; row++)
			{
				double start = 3000 + row * 15000;
				yield return new ShredPracticeNote(start, row, 0);
				yield return new ShredPracticeNote(start + 1500, row, 1);
				yield return new ShredPracticeNote(start + 3000, row, 3, ShredPracticeArticulation.HammerOn);
				yield return new ShredPracticeNote(start + 4500, row, 1, ShredPracticeArticulation.PullOff);
				yield return new ShredPracticeNote(start + 6000, row, 0, ShredPracticeArticulation.PullOff);
				yield return new ShredPracticeNote(start + 7500, row, 2, ShredPracticeArticulation.PullOff);
				yield return new ShredPracticeNote(start + 9000, row, 4, ShredPracticeArticulation.HammerOn);
				yield return new ShredPracticeNote(start + 10500, row, 5);
			}
			// Finale: independent strings, then simultaneous open notes.
			for (int index = 0; index < 4; index++)
			{
				yield return new ShredPracticeNote(34000 + index * 1500, 0, index + 1);
				yield return new ShredPracticeNote(34750 + index * 1500, 1, index + 1);
			}
			yield return new ShredPracticeNote(41500, 0, 0);
			yield return new ShredPracticeNote(41500, 1, 0);
		}

		private void buildNotes()
		{
			noteLayer.Clear();
			noteViews.Clear();
			foreach (ShredPracticeNote note in session.Notes)
			{
				float width = 46;
				Color4 colour = Color4.White;
				if (note.Fret == 0)
				{
					fretBounds(note.Row, out float minimum, out float maximum);
					width = maximum - minimum;
				}
				else
					colour = fretColours[note.Fret - 1];
				Container view = new Container { Size = new Vector2(width, 14), Origin = Anchor.CentreLeft, Alpha = 0 };
				if (note.Articulation != ShredPracticeArticulation.Pick)
				{
					view.Add(new Box { Position = new Vector2(-5), Size = new Vector2(width + 10, 24), Colour = new Color4(250, 215, 110, 90) });
					string label = "H";
					if (note.Articulation == ShredPracticeArticulation.PullOff)
						label = "P";
					view.Add(new SpriteText { Position = new Vector2(width + 7, -4), Text = label, Font = new FontUsage(size: 19) });
				}
				view.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = colour });
				noteLayer.Add(view);
				noteViews.Add(view);
			}
		}

		private void clearInput()
		{
			for (int row = 0; row < 2; row++)
				session.GetString(row).Reset();
		}

		private void reset()
		{
			running = false;
			captureRow = -1;
			captureAction = -1;
			session = new ShredPracticeSession(createNotes());
			notice.Text = "Ready. Release held keys, then F5 or Play. Click a key to rebind.";
			buildNotes();
		}

		private void advance()
		{
			if (running)
				session.AdvanceTo(Math.Max(session.Time, Time.Current - clockOrigin));
			if (session.Completed)
				running = false;
		}

		private void togglePlayback()
		{
			if (captureRow >= 0)
				return;
			advance();
			if (session.Completed)
				reset();
			running = !running;
			clockOrigin = Time.Current - session.Time;
		}

		private void changeSpeed(double amount)
		{
			scrollSpeed = Math.Clamp(scrollSpeed + amount, 0.5, 3);
		}

		private void switchHand()
		{
			leftHanded = !leftHanded;
			bindings = rightBindings;
			if (leftHanded)
				bindings = leftBindings;
			reset();
			buildField();
		}

		private void restoreDefaults()
		{
			if (leftHanded)
			{
				leftBindings = new ShredPracticeBindings(true);
				bindings = leftBindings;
			}
			else
			{
				rightBindings = new ShredPracticeBindings(false);
				bindings = rightBindings;
			}
			reset();
			buildField();
		}

		private void beginCapture(int row, int action)
		{
			advance();
			running = false;
			clearInput();
			captureRow = row;
			captureAction = action;
			notice.Text = $"Binding string {row + 1}, slot {action + 1}: press a key (letters, digits, ; , . /). ESC cancels.";
		}

		private void cancelCapture()
		{
			captureRow = -1;
			captureAction = -1;
			notice.Text = "Binding cancelled. F5 resumes.";
		}

		protected override bool OnKeyDown(KeyDownEvent e)
		{
			if (!heldKeys.Add(e.Key))
				return true;
			if (captureRow >= 0)
			{
				if (e.Key == Key.Escape)
					cancelCapture();
				else if (bindings.TryRebind(captureRow, captureAction, e.Key))
				{
					captureRow = -1;
					captureAction = -1;
					buildField();
					buildNotes();
					notice.Text = "Binding updated (occupied keys swap). Release the key, then F5 to resume.";
				}
				else
					notice.Text = "Unsupported key. Use letters, digits, ; , . /, or ESC to cancel.";
				return true;
			}
			advance();
			switch (e.Key)
			{
				case Key.F5: togglePlayback(); return true;
				case Key.F6: reset(); return true;
				case Key.F7: changeSpeed(-0.25); return true;
				case Key.F8: changeSpeed(0.25); return true;
			}
			if (processKey(e.Key, true))
				return true;
			return base.OnKeyDown(e);
		}

		protected override void OnKeyUp(KeyUpEvent e)
		{
			heldKeys.Remove(e.Key);
			advance();
			if (captureRow < 0)
				processKey(e.Key, false);
			base.OnKeyUp(e);
		}

		private bool processKey(Key key, bool pressed)
		{
			if (!bindings.TryFind(key, out int row, out int action))
				return false;
			if (action < 5)
				session.SetFret(row, action, pressed, running);
			else
				session.SetPick(row, action - 5, pressed, running);
			return true;
		}

		protected override void Update()
		{
			base.Update();
			stage.Scale = new Vector2(Math.Max(0.1f, Math.Min(DrawWidth / 1000, DrawHeight / stage.Height)));
			advance();
			string hand = "RIGHT-HANDED";
			if (leftHanded)
				hand = "LEFT-HANDED";
			settings.Text = $"2 strings | {hand} | scroll x{scrollSpeed:0.00}";
			string state = "PAUSED";
			if (running)
				state = "PLAYING";
			if (session.Completed)
				state = "FINISHED";
			summary.Text = $"{state} | {session.Time / 1000:0.0}s | HIT {session.Hits} | MISS {session.Misses} | COMBO {session.Combo}";
			feedback.Text = session.LastEvent;
			for (int row = 0; row < 2; row++)
			{
				stringLabels[row].Text = $"String {row + 1}";
				for (int action = 0; action < 9; action++)
				{
					Color4 colour = new Color4(38, 42, 58, 255);
					if (action < 5)
					{
						if (session.GetString(row).IsFretHeld(action))
							colour = fretColours[action];
						if (session.GetString(row).SelectedFret == action + 1)
							colour = new Color4(185, 140, 35, 255);
					}
					else if (session.GetString(row).IsPickHeld(action - 5))
						colour = new Color4(150, 160, 175, 255);
					if (captureRow == row && captureAction == action)
						colour = new Color4(40, 140, 130, 255);
					cells[row, action].SetColour(colour);
				}
			}
			double visibleTime = 1600 / scrollSpeed;
			for (int index = 0; index < session.Notes.Count; index++)
			{
				ShredPracticeNote note = session.Notes[index];
				Container view = noteViews[index];
				double remaining = note.Time - session.Time;
				view.Alpha = 0;
				if (note.Result != ShredPracticeResult.Pending || remaining > visibleTime)
					continue;
				Vector2 destination;
				if (note.Fret == 0)
				{
					fretBounds(note.Row, out float minimum, out float maximum);
					destination = new Vector2(minimum, rowY(note.Row));
				}
				else
					destination = target(note.Row, note.Fret - 1) - new Vector2(23, 0);
				Vector2 travel = new Vector2(-70, 150);
				if (note.Row == 1)
					travel = new Vector2(70, -150);
				view.Position = destination - travel * (float)(remaining / visibleTime);
				view.Alpha = 1;
			}
		}

		private partial class ClickCell : Container
		{
			private readonly Action action;
			private readonly Box background;

			public ClickCell(string label, Action action)
			{
				this.action = action;
				background = new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(45, 55, 78, 255) };
				Add(background);
				Add(new SpriteText { Anchor = Anchor.Centre, Origin = Anchor.Centre, Text = label, Font = new FontUsage(size: 17) });
			}

			public void SetColour(Color4 colour) => background.Colour = colour;

			protected override bool OnClick(ClickEvent e)
			{
				action();
				return true;
			}
		}
	}
}
