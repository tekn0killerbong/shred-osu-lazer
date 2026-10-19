using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Shred.Input;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.Shred.Tests
{
	// A keyboard diagnostics scene, separate from the template's scoring scene.
	[TestFixture]
	public partial class TestSceneShredInput : OsuTestScene
	{
		private readonly ShredStringState[] strings =
		{
			new ShredStringState(), new ShredStringState(),
		};

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

		private readonly Box[,] cells = new Box[2, 9];
		private readonly SpriteText[] status = new SpriteText[2];
		private readonly SpriteText[] lastPick = new SpriteText[2];
		private readonly int[] pickCounts = new int[2];

		public TestSceneShredInput()
		{
			Container panel = new Container
			{
				RelativeSizeAxes = Axes.Both,
			};
			Children = new Drawable[] { panel };
			panel.Add(new Box
			{
				RelativeSizeAxes = Axes.Both,
				Colour = new Color4(15, 18, 28, 255),
			});
			panel.Add(new SpriteText
			{
				Position = new Vector2(30, 30),
				Text = "SHRED - input prototype / 2 strings",
			});
			panel.Add(new SpriteText
			{
				Position = new Vector2(30, 65),
				Text = "Frets: blue held, gold selected. Picks: silver held.",
			});

			for (int row = 0; row < 2; row++)
			{
				float y = 125 + row * 170;
				for (int column = 0; column < 9; column++)
				{
					float gap = 0;
					if (column >= 5)
						gap = 60;
					Container key = new Container
					{
						Position = new Vector2(30 + column * 60 + gap, y),
						Size = new Vector2(52, 52),
					};
					cells[row, column] = new Box { RelativeSizeAxes = Axes.Both };
					key.Add(cells[row, column]);
					key.Add(new SpriteText
					{
						Anchor = Anchor.Centre,
						Origin = Anchor.Centre,
						Text = labels[row, column],
					});
					panel.Add(key);
				}
				status[row] = new SpriteText { Position = new Vector2(30, y + 65) };
				lastPick[row] = new SpriteText
				{
					Position = new Vector2(30, y + 95),
					Text = "No pick yet",
				};
				panel.Add(status[row]);
				panel.Add(lastPick[row]);
			}

			AddStep("Reset input", () =>
			{
				for (int row = 0; row < strings.Length; row++)
				{
					strings[row].Reset();
					pickCounts[row] = 0;
					lastPick[row].Text = "No pick yet";
				}
			});
		}

		protected override bool OnKeyDown(KeyDownEvent e)
		{
			if (processKey(e.Key, true))
				return true;
			return base.OnKeyDown(e);
		}

		protected override void OnKeyUp(KeyUpEvent e)
		{
			processKey(e.Key, false);
			base.OnKeyUp(e);
		}

		private bool processKey(Key key, bool pressed)
		{
			for (int row = 0; row < strings.Length; row++)
			{
				for (int fret = 0; fret < 5; fret++)
				{
					if (fretKeys[row, fret] != key)
						continue;
					strings[row].SetFret(fret, pressed);
					return true;
				}
				for (int pick = 0; pick < 4; pick++)
				{
					if (pickKeys[row, pick] != key)
						continue;
					if (strings[row].SetPick(pick, pressed))
					{
						pickCounts[row]++;
						lastPick[row].Text = $"Pick #{pickCounts[row]} / finger {pick + 1} / fret {strings[row].SelectedFret} (0 = OPEN)";
					}
					return true;
				}
			}
			return false;
		}

		protected override void Update()
		{
			base.Update();
			for (int row = 0; row < strings.Length; row++)
			{
				ShredStringState state = strings[row];
				status[row].Text = $"String {row + 1}: selected fret {state.SelectedFret} (0 = OPEN)";
				for (int fret = 0; fret < 5; fret++)
				{
					Color4 colour = new Color4(40, 46, 64, 255);
					if (state.IsFretHeld(fret))
						colour = new Color4(50, 90, 165, 255);
					if (state.SelectedFret == fret + 1)
						colour = new Color4(155, 112, 20, 255);
					cells[row, fret].Colour = colour;
				}
				for (int pick = 0; pick < 4; pick++)
				{
					Color4 colour = new Color4(40, 46, 64, 255);
					if (state.IsPickHeld(pick))
						colour = new Color4(130, 140, 155, 255);
					cells[row, pick + 5].Colour = colour;
				}
			}
		}
	}
}
