# Maintenance tools

- `repair-native-audio.py` and `repack-drum-wav.py`: existing audio maintenance utilities. Read each script's arguments before using it on a copy of a map or sample.
- `legacy-tests/`: historical v16/v17 launchers. Their working directory is resolved relative to the script, so they can still locate the existing test projects after the move. Their assertions may describe earlier mechanics.
- `legacy-patches/`: historical client source patch. It is not required for the supplied current source tree. Its default client location is now the sibling `osu-shred-lazer` directory, rather than the original author's absolute Windows path.

Current test commands are in [Testing](../docs/development/TESTING.md). These tools are source files and are no longer hidden by the old blanket `/tools/**` ignore rule.
