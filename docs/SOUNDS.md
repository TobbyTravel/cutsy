# Cutsy sound sources

The eight PCM cues in packs/cutsy/runtime/sounds are original Cutsy assets. The generator in scripts/generate-sounds.py synthesizes them from pitch sweeps, soft envelopes, harmonics and seeded filtered noise. It uses no samples or third-party sound libraries.

The WAVs are mono signed 16-bit PCM at 22,050 Hz, with an 18 ms soft attack, 75 ms release and peak below 0.34 full scale. The application defaults to 80% volume and also applies each cue's pack gain. Ambient cues share a 90-second cooldown, and click, pickup and drop cues share a five-second cooldown. Mute, pause and quiet mode stop playback.
