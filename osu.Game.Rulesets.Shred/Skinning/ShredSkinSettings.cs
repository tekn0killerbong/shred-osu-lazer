using System;
using System.Globalization;
using System.IO;
using System.Linq;
using osuTK;
using osuTK.Graphics;
namespace osu.Game.Rulesets.Shred.Skinning
{
	public sealed class ShredSkinSettings
	{
		public float[] ColumnWidth = { 48, 48, 48, 48, 48 };
		public float[] ColumnSpacing = { 17, 17, 17, 17 };
		public float NoteHeight = 26;
		public float WhamPressAngle = 240;
		public float WhamPressDistance = 12;
		public float WhamScrollLength = 350;
		public float SectionGap = 94;
		public float RowGap = 34;
		public float RowOffsetBottom = 32.5f;
		public bool AlignRows = true;
		public float EffectiveRowOffsetBottom
		{
			get
			{
				// Place the top-left corner of the lower key below the upper centre.
                // The framework applies X shear as x -= shear * y.
                if (AlignRows) return ColumnWidth[0] / 2 - Shear(1) * NoteHeight / 2;
				return RowOffsetBottom;
			}
		}
		public float FretX = -350;
		public float TopAngle = -65;
		public float BottomAngle = 115;
		public float ScrollLength = 165.53f;
		public float HaloRadius = 12;
		public float HaloOpacity = 0.75f;
		public float HaloPulse = 0.12f;
		public Color4 HaloColour = new Color4(255, 210, 90, 255);
		public Color4 ColourLight = new Color4(255, 225, 155, 255);
		public float LightPosition = 0;
		public float LightLength = 85;
		public float LightFadeMs = 180;
		public float AnimationFPS = 60;
		public float HitExpand = 1.8f;
		public float HitFadeMs = 260;
		public float MissFadeMs = 220;
		public float ShakeAmplitude = 6;
		public float ShakeMs = 180;
		public Color4 ErrorColour = new Color4(255, 75, 85, 255);
		public string Folder { get; private set; }
		public static ShredSkinSettings Load()
		{
			string folder = Path.Combine(Environment.CurrentDirectory, "ShredSkin");
			if (!File.Exists(Path.Combine(folder, "skin.ini"))) folder = Path.Combine(AppContext.BaseDirectory, "ShredSkin");
			if (!File.Exists(Path.Combine(folder, "skin.ini"))) return new ShredSkinSettings();
			using StreamReader reader = File.OpenText(Path.Combine(folder, "skin.ini"));
			ShredSkinSettings settings = Read(reader);
			settings.Folder = folder;
			return settings;
		}
		public static ShredSkinSettings Read(TextReader reader)
		{
			ShredSkinSettings settings = new ShredSkinSettings();
			string section = "";
			string line;
			int lineNumber = 0;
			while ((line = reader.ReadLine()) != null)
			{
				lineNumber++;
				line = line.Trim();
				if (line.Length == 0 || line.StartsWith("//") || line.StartsWith(";")) continue;
				if (line.StartsWith("[")) { section = line; continue; }
				if (section != "[Shred]") continue;
				int colon = line.IndexOf(':');
				if (colon < 0) continue;
				string key = line.Substring(0, colon).Trim();
				string value = line.Substring(colon + 1).Trim();
				try
				{
					switch (key)
					{
						case "ColumnWidth": settings.ColumnWidth = list(value, 5, 12, 100); break;
						case "ColumnSpacing": settings.ColumnSpacing = list(value, 4, 0, 100); break;
						case "NoteHeight": settings.NoteHeight = number(value, 8, 70); break;
						case "WhamPressAngle": settings.WhamPressAngle = number(value, -360, 360); break;
						case "WhamPressDistance": settings.WhamPressDistance = number(value, 0, 100); break;
						case "WhamScrollLength": settings.WhamScrollLength = number(value, 30, 1000); break;
						case "SectionGap": settings.SectionGap = number(value, 0, 250); break;
						case "RowGap": settings.RowGap = number(value, 0, 300); break;
						case "AlignRows": settings.AlignRows = bool.Parse(value); break;
						case "RowOffsetBottom": settings.RowOffsetBottom = number(value, -200, 200); break;
						case "FretX": settings.FretX = number(value, -450, 100); break;
						case "ScrollAngleTop": settings.TopAngle = number(value, -170, -10); break;
						case "ScrollAngleBottom": settings.BottomAngle = number(value, 10, 170); break;
						case "ScrollLength": settings.ScrollLength = number(value, 30, 280); break;
						case "HaloRadius": settings.HaloRadius = number(value, 0, 30); break;
						case "HaloOpacity": settings.HaloOpacity = number(value, 0, 1); break;
						case "HaloPulse": settings.HaloPulse = number(value, 0, 0.5f); break;
						case "HaloColour": settings.HaloColour = colour(value); break;
						case "ColourLight": settings.ColourLight = colour(value); break;
						case "LightPosition": settings.LightPosition = number(value, -100, 100); break;
						case "LightLength": settings.LightLength = number(value, 10, 250); break;
						case "LightFadeMs": settings.LightFadeMs = number(value, 1, 2000); break;
						case "AnimationFPS": settings.AnimationFPS = number(value, 1, 120); break;
						case "HitExpand": settings.HitExpand = number(value, 1, 4); break;
						case "HitFadeMs": settings.HitFadeMs = number(value, 1, 2000); break;
						case "MissFadeMs": settings.MissFadeMs = number(value, 1, 2000); break;
						case "ShakeAmplitude": settings.ShakeAmplitude = number(value, 0, 25); break;
						case "ShakeMs": settings.ShakeMs = number(value, 1, 1500); break;
						case "ErrorColour": settings.ErrorColour = colour(value); break;
					}
				}
				catch (FormatException e) { throw new FormatException($"skin.ini line {lineNumber}, {key}: {e.Message}", e); }
			}
			return settings;
		}
		private static float number(string value, float min, float max)
		{
			if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) || !float.IsFinite(result) || result < min || result > max)
				throw new FormatException($"Expected number in {min}..{max}.");
			return result;
		}
		private static float[] list(string value, int count, float min, float max)
		{
			float[] values = value.Split(',').Select(part => number(part.Trim(), min, max)).ToArray();
			if (values.Length == 1) return Enumerable.Repeat(values[0], count).ToArray();
			if (values.Length != count) throw new FormatException($"Expected one value or {count} values.");
			return values;
		}
		private static Color4 colour(string value)
		{
			float[] rgb = list(value, 3, 0, 255);
			return new Color4(rgb[0] / 255, rgb[1] / 255, rgb[2] / 255, 1);
		}
		public Vector2 Direction(int row)
		{
			double angle = TopAngle;
			if (row == 1)
			{
				angle = BottomAngle;
				if (AlignRows) angle = TopAngle + 180;
			}
			double radians = angle * Math.PI / 180;
			return new Vector2((float)Math.Cos(radians), -(float)Math.Sin(radians));
		}
		public float Shear(int row)
		{
			Vector2 direction = Direction(row);
			return -direction.X / direction.Y;
		}
		public float FretCentre(int fret)
		{
			float x = FretX;
			for (int index = 1; index < fret; index++)
				x += ColumnWidth[index - 1] / 2 + ColumnSpacing[index - 1] + ColumnWidth[index] / 2;
			return x;
		}
		public float OpenWidth => FretCentre(5) - FretCentre(1) + (ColumnWidth[0] + ColumnWidth[4]) / 2;
		public float OpenCentre => (FretCentre(1) - ColumnWidth[0] / 2 + FretCentre(5) + ColumnWidth[4] / 2) / 2;
	}
}
