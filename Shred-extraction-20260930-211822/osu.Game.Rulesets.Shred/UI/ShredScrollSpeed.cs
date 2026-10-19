using System;
using System.Globalization;
using System.IO;

namespace osu.Game.Rulesets.Shred.UI
{
	public static class ShredScrollSpeed
	{
		private const int default_speed = 20;
		public const double DefaultApproachTime = 11485.0 / default_speed;
		private static readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "osu-shred", "scroll-speed.txt");
		private static int speed = load();

		public static int Speed => speed;
		// osu!mania uses 11485 / speed milliseconds for the full scroll distance.
		public static double ApproachTime => 11485.0 / speed;

		public static void Adjust(int change)
		{
			speed = Math.Clamp(speed + change, 1, 40);
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, speed.ToString(CultureInfo.InvariantCulture));
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}

		private static int load()
		{
			try
			{
				if (int.TryParse(File.ReadAllText(path), NumberStyles.Integer, CultureInfo.InvariantCulture, out int saved))
					return Math.Clamp(saved, 1, 40);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
			return default_speed;
		}
	}
}
