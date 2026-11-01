"""チリの効果音。外部音源を使わない短い合成音を再現可能に生成する。"""
import math
from pathlib import Path
import random
import struct
import wave

RATE = 48000
DEST = Path(__file__).resolve().parents[4] / "DemoApp/assets/audio/se/chiri"
DEST.mkdir(parents=True, exist_ok=True)


def sound(kind, duration, seed):
    rng = random.Random(seed)
    smooth = 0.0
    phase = 0.0
    samples = []
    for i in range(round(RATE * duration)):
        t = i / RATE
        p = t / duration
        noise = rng.uniform(-1, 1)
        smooth += (noise - smooth) * 0.065
        edge = min(1, t / 0.003) * min(1, (duration - t) / 0.015)
        if kind == "plant":
            value = (math.sin(2 * math.pi * 320 * t) * 0.22 + smooth * 0.8) * math.exp(-t * 34)
        elif kind == "fuse":
            value = (noise - smooth) * 0.18 * (1 - p) ** 1.7
        elif kind == "blast":
            phase += 2 * math.pi * (47 + 95 * math.exp(-t * 16)) / RATE
            value = (smooth * 1.8 + math.sin(phase) * 0.30) * math.exp(-t * 7.5)
            value += noise * 0.14 * math.exp(-t * 60)
        elif kind == "mirror":
            phase += 2 * math.pi * (180 + 620 * (1 - p) ** 2) / RATE
            value = (smooth * 0.65 + math.sin(phase) * 0.08) * math.sin(math.pi * p) ** 0.7
        else:
            value = smooth * 0.45 * math.exp(-t * 22) + noise * 0.025 * (1 - p) ** 3
        samples.append(max(-0.85, min(0.85, value * edge)))
    path = DEST / f"{kind}.wav"
    if path.exists():
        raise FileExistsError(path)
    with wave.open(str(path), "wb") as wav:
        wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        wav.writeframes(b"".join(struct.pack("<h", round(x * 32767)) for x in samples))
    print(f"{path}: {duration:.2f}s, {max(abs(x) for x in samples):.3f} peak")


for number, (name, seconds) in enumerate([
    ("plant", 0.14), ("fuse", 0.16), ("blast", 0.70), ("mirror", 0.43), ("dud", 0.22)
]):
    sound(name, seconds, 429000 + number)
