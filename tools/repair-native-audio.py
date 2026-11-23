"""Create an isolated Riff audio test from a native Shred OSZ (no geometry edits)."""
import argparse
import base64
import copy
import hashlib
import json
from pathlib import Path
import re
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("destination", type=Path)
args = parser.parse_args()
prefix = "shred-native-v1-"

with zipfile.ZipFile(args.source) as original:
	name, = [n for n in original.namelist() if n.endswith("[Riff].osu")]
	text = original.read(name).decode("utf-8-sig")
	section = ""
	lines = []
	count = 0
	old_samples = 0
	for line in text.splitlines(keepends=True):
		if line.startswith("["):
			section = line.strip()
		if section == "[Metadata]":
			if line.startswith(("Title:", "TitleUnicode:")):
				line = line.rstrip("\r\n") + " [Shred v49 audio test]\n"
			elif line.startswith("Version:"):
				line = "Version: Riff - audio v49\n"
			elif line.startswith(("BeatmapID:", "BeatmapSetID:")):
				line = line.split(":")[0] + ": -1\n"
		if section == "[HitObjects]" and prefix in line:
			before, encoded = line.rstrip("\r\n").split(prefix)
			data = json.loads(base64.urlsafe_b64decode(encoded + "=" * (-len(encoded) % 4)))
			old_geometry = copy.deepcopy(data)
			old_geometry.pop("NormalSamples")
			old_samples += len(data["NormalSamples"])
			data["NormalSamples"] = [{
				"Name": "hitnormal", "Bank": "normal", "Suffix": None,
				"Volume": 100, "AutoBank": False, "Beatmap": False,
				"File": None, "CustomBank": None, "Layered": False,
			}]
			encoded = base64.urlsafe_b64encode(json.dumps(data, separators=(",", ":")).encode()).decode().rstrip("=")
			# Native carrier is a filename sample. Suppress legacy addition bits;
			# all actual audio now lives in the single ordinary sample in its payload.
			fields = before.split(",")
			fields[4] = "0"
			line = ",".join(fields) + prefix + encoded + "\n"
			verified = json.loads(base64.urlsafe_b64decode(encoded + "=" * (-len(encoded) % 4)))
			verified.pop("NormalSamples")
			assert verified == old_geometry
			count += 1
		lines.append(line)
	assert count == 366, f"Unexpected Riff note count: {count}"
	result = "".join(lines)
	# Metadata and hit sample fields are the only permitted changes.
	def section_text(content, title):
		return re.search(r"(?ms)^\[" + title + r"\]\s*\n(.*?)(?=^\[|\Z)", content).group(1)
	for title in ["General", "Editor", "Difficulty", "Events", "TimingPoints"]:
		assert section_text(text, title) == section_text(result, title), title
	args.destination.parent.mkdir(parents=True, exist_ok=True)
	with zipfile.ZipFile(args.destination, "x", zipfile.ZIP_DEFLATED) as target:
		target.writestr("Jimi_Hendrix_Riff_audio_v49.osu", result)
		# No WAVs in this map: the test must resolve guitars from the DLL itself.
		for asset in ["audio.mp3", "bg.png"]:
			target.writestr(asset, original.read(asset))
	with zipfile.ZipFile(args.destination) as target:
		for asset in ["audio.mp3", "bg.png"]:
			assert hashlib.sha256(original.read(asset)).digest() == hashlib.sha256(target.read(asset)).digest()
	print(json.dumps({"notes": count, "old_samples": old_samples, "new_samples": count,
		"geometry_preserved": True, "timing_events_audio_preserved": True,
		"destination": str(args.destination)}))
