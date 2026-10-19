using System;
using System.Collections.Generic;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Shred.Input
{
	// Exactly two logical strings. Fret rank never depends on screen position.
	public sealed class ShredPracticeBindings
	{
		private readonly Key[,] keys;
		private static readonly Dictionary<Key, Vector2> positions = createPositions();

		public ShredPracticeBindings(bool leftHanded)
		{
			if (leftHanded)
			{
				keys = new Key[,]
				{
					{ Key.Semicolon, Key.L, Key.K, Key.J, Key.H, Key.F, Key.D, Key.S, Key.A },
					{ Key.Slash, Key.Period, Key.Comma, Key.M, Key.N, Key.V, Key.C, Key.X, Key.Z },
				};
			}
			else
			{
				keys = new Key[,]
				{
					{ Key.A, Key.S, Key.D, Key.F, Key.G, Key.J, Key.K, Key.L, Key.Semicolon },
					{ Key.Z, Key.X, Key.C, Key.V, Key.B, Key.M, Key.Comma, Key.Period, Key.Slash },
				};
			}
		}

		public Key GetKey(int row, int action) => keys[row, action];
		public Vector2 GetPosition(int row, int action) => positions[keys[row, action]];

		public bool TryFind(Key key, out int row, out int action)
		{
			for (row = 0; row < 2; row++)
			{
				for (action = 0; action < 9; action++)
				{
					if (keys[row, action] == key)
						return true;
				}
			}
			row = -1;
			action = -1;
			return false;
		}

		public bool TryRebind(int row, int action, Key key)
		{
			if (!positions.ContainsKey(key))
				return false;
			Key old = keys[row, action];
			if (TryFind(key, out int oldRow, out int oldAction))
				keys[oldRow, oldAction] = old;
			keys[row, action] = key;
			return true;
		}

		public string GetLabel(int row, int action)
		{
			Key key = keys[row, action];
			switch (key)
			{
				case Key.Semicolon: return ";";
				case Key.Comma: return ",";
				case Key.Period: return ".";
				case Key.Slash: return "/";
			}
			string label = key.ToString();
			if (label.StartsWith("Number", StringComparison.Ordinal))
				return label.Substring(6);
			return label;
		}

		private static Dictionary<Key, Vector2> createPositions()
		{
			Dictionary<Key, Vector2> result = new Dictionary<Key, Vector2>();
			Key[][] rows =
			{
				new[] { Key.Number1, Key.Number2, Key.Number3, Key.Number4, Key.Number5, Key.Number6, Key.Number7, Key.Number8, Key.Number9, Key.Number0 },
				new[] { Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P },
				new[] { Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L, Key.Semicolon },
				new[] { Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M, Key.Comma, Key.Period, Key.Slash },
			};
			float[] offsets = { 0, 0.5f, 0.75f, 1.25f };
			for (int row = 0; row < rows.Length; row++)
			{
				for (int index = 0; index < rows[row].Length; index++)
					result.Add(rows[row][index], new Vector2(index + offsets[row], row));
			}
			return result;
		}
	}
}
