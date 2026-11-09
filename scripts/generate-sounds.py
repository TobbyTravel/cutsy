"""Synthesize Cutsy's eight original, short PCM cues. Run from the repository root."""
import math
from pathlib import Path
import random
import struct
import wave

RATE = 22050
OUT = Path("packs/cutsy/runtime/sounds")
OUT.mkdir(parents=True, exist_ok=True)
rng = random.Random(21)

# (length, start pitch, end pitch, second voice ratio, colored noise mix)
SPECS = {
    "pop": (.105, 660, 330, 1.52, .07),
    "hop": (.160, 370, 740, 1.50, .02),
    "landing-puff": (.150, 155, 80, 1.25, .45),
    "happy-chirp": (.245, 550, 830, 1.50, .01),
    "affection-chime": (.385, 587, 587, 1.50, 0),
    "surprise-squeak": (.190, 640, 970, 1.22, .025),
    "sleepy-tone": (.410, 330, 293, 1.50, .015),
    "completion-sparkle": (.330, 740, 990, 1.33, .01),
}


def smoothstep(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3 - 2 * value)


for name, (length, low, high, interval, noise_mix) in SPECS.items():
    samples = []
    noise = 0.0
    for i in range(round(length * RATE)):
        t = i / RATE
        phase = 2 * math.pi * (low * t + (high - low) * t * t / (2 * length))
        noise = .82 * noise + .18 * rng.uniform(-1, 1)
        envelope = smoothstep(t / .018) * smoothstep((length - t) / .075)
        first = .72 * math.sin(phase)
        second = .21 * math.sin(phase * interval) * smoothstep((t - .025) / .035)
        sample = .34 * envelope * ((1 - noise_mix) * (first + second) + noise_mix * noise)
        samples.append(round(32767 * sample))
    with wave.open(str(OUT / f"{name}.wav"), "wb") as wav:
        wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        wav.writeframes(struct.pack(f"<{len(samples)}h", *samples))
