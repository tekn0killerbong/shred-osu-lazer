#!/usr/bin/env python3
"""Rewrap the four supplied Drum WAVs as minimal PCM WAVs, without DSP.

Usage: repack-drum-wav.py INPUT_DIRECTORY OUTPUT_DIRECTORY
Accepts drum-hitnormal.wav or the attachment name drum-hitnormal(1).wav.
The PCM payload, stereo channels, rate, length and amplitude are preserved.
"""
import argparse
import hashlib
from pathlib import Path
import wave


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    for sound in ("hitnormal", "hitwhistle", "hitclap", "hitfinish"):
        name = f"drum-{sound}"
        source = args.input / f"{name}.wav"
        if not source.exists():
            source = args.input / f"{name}(1).wav"
        with wave.open(str(source), "rb") as reader:
            params = reader.getparams()
            if (params.nchannels, params.sampwidth, params.framerate, params.comptype) != (2, 2, 44100, "NONE"):
                raise ValueError(f"Unexpected format: {source}: {params}")
            pcm = reader.readframes(params.nframes)
        destination = args.output / f"{name}.wav"
        with wave.open(str(destination), "wb") as writer:
            writer.setparams(params)
            writer.writeframes(pcm)
        with wave.open(str(destination), "rb") as reader:
            if reader.getparams() != params or reader.readframes(params.nframes) != pcm:
                raise ValueError(f"Audio changed: {destination}")
        print(f"{destination.name}: PCM SHA256={hashlib.sha256(pcm).hexdigest()}, WAV SHA256={hashlib.sha256(destination.read_bytes()).hexdigest()}")


if __name__ == "__main__":
    main()
