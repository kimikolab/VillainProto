"""背きの雷の短い放出音と二音の共鳴音を再生成する（外部音源なし）。"""
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
RATE = 44100


def render(name, duration, resonance):
    rng = random.Random(320)
    phase = 0.0
    frames = bytearray()
    for i in range(int(duration * RATE)):
        t = i / RATE
        u = t / duration
        frequency = 540 + 960 * u if resonance else 1400 * (1 - u) + 170
        phase += 2 * math.pi * frequency / RATE
        envelope = min(1, t * 500) * (1 - u) ** 1.8
        tone = math.sin(phase) * .28
        if resonance:
            tone += math.sin(phase * 1.5) * .22 + math.sin(phase * 2) * .12
        crackle = (rng.random() * 2 - 1) * (.08 if resonance else .2)
        value = int(max(-.9, min(.9, (tone + crackle) * envelope)) * 32767)
        frames.extend(struct.pack("<h", value))
    output = ROOT / "DemoApp/assets/audio/se" / name
    with wave.open(str(output), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(frames)


render("som_throw.wav", .24, False)
render("som_resonance.wav", .30, True)
