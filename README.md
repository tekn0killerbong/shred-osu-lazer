to install, go to the [latest release](https://github.com/tekn0killerbong/shred-osu-lazer/releases/latest) and download the .dll file. Then, place it in the rulesets folder of your osu!lazer folder. You can find it using the in-game button "Open osu! folder".

# shred for osu!lazer

![shred gameplay]("C:\Users\Utilisateur\source\osu-shred\images\osu post shred demo image 1.png")

## Welcome to shred !

shred is a custom ruleset made for the lazer installation of osu!, with the objective of optimizing, further developing and make available the concepts found within the Guitar Hero series on this platform. The project was vibe-coded with ChatGPT, so I have no idea how well or disastrously the code was constructed. But don't get disheartened too soon, as the ruleset seems to work pretty well and the vision and design is pretty much all human-inspired.

## How to play

### Frets and fingerpicks

You get 2 rows of 5 fret keys each for the left hand and 4 fingerpick keys each for the right hand. An inverted mod was made for the left-handed aspiring shredders.

Notes come from the top and bottom of the screen. Keys on the top row play notes arriving from the top, keys on the bottom row play notes arriving from the bottom.

### Fingerpicking

You can hold frets in preparation of incoming notes. When notes reach the frets at the center of the playfield, you have to pluck them using the good frets and the fingerpick keys of the corresponding row. Any fingerpick keys can be used to hit notes.

To play chords (multiple notes), you need the same number of fingerpick keys as the number of notes you're playing with the frets. For example, single notes need one fingerpick key but a 3-note chord would need 3, let's say 2 on the top row and one on the bottom row.

You'll also encounter long notes. Simply hold the fret keys til you reach the end of the long notes' tails.

It is allowed to hold any number of frets on either side of hit notes. You can also hold frets that are in between multiple notes that are apart from each other.

### Hammer-pulls

You'll find glowing notes during gameplay. You can hit these notes without fingerpicking. You can do them by either pressing the frets (hammer-on) or holding while releasing from other ones (pull-offs). Holding them preemptively will make you miss the glowing notes. On sequences of these notes coming from both sides, you can continue hammer-ons on the other row. However, pull-offs cannot be used by releasing from the other row.

Fingerpicking a glowing note will make you lose points. (to be implemented)

### Shredding

When notes arrive at fast speeds, you can emulate fast guitar picking. This is done by keeping a fingerpick key down, then alternating between pressing and releasing another fingerpick key. Each press and release will hit notes if you keep holding that first key.

This also can be done for fast chords. The way this mechanic works is that you have to hold no more than a single note and press and release the rest of what you would need to fully hit the chord. You can also carry releases on any number of chord notes but your presses have to match them.

## Step Recorder

Here's some indications for the step recorder in shred :

- Select first note type you wanna create

- Press frets to create notes. Grid will scroll automatically on release of last held note.

- Press '/' or RIGHT while holding frets create long notes. Those won't make an automatic scroll

- 'Return on pause' makes grid return on last position that you navigated or stopped on (ctrl-pause stops normally on current position)

For the snap toggle, it's not related to the step recorder but only for selections you paste on different BPM sections.

---
Make many maps my friends, hope you enjoy, and I'll see you on the shreds.


## Developer documentation

- [Build the official DLL or the development client](docs/development/BUILDING.md)
- [Source layout and contribution workflow](docs/development/CONTRIBUTING.md)
- [Tests](docs/development/TESTING.md)
- [Documentation index](docs/README.md)
