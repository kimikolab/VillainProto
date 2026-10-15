"""ゴルム用の短い合成音。外部の音源を使わず、固定 seed で再生成する。"""
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "DemoApp/assets/audio/se"
RATE = 22050
random.seed(345)

for cue, seconds in [("launch", .32), ("hit", .22), ("dock", .16), ("inhale", .43), ("crumble", .24), ("spit", .46), ("toss", .38), ("land", .12)]:
    samples = []
    low = 0
    for i in range(int(RATE * seconds)):
        t = i / RATE
        p = t / seconds
        noise = random.uniform(-1, 1)
        low = low * .80 + noise * .20
        env = min(1, p * 25) * (1 - p) ** 1.6
        if cue == "launch":
            value = .50 * low + .12 * math.sin(math.tau * (130 * t + 350 * t * t))
        elif cue == "hit":
            value = .65 * math.sin(math.tau * (65 * t + 9 * (1-math.exp(-t*25)))) * math.exp(-t*12) + .25 * low
        elif cue == "dock":
            value = sum(math.sin(math.tau * f * t) * math.exp(-t * decay) for f, decay in [(280, 28), (687, 35), (1127, 45)]) * .20 + .15 * noise
        elif cue == "inhale":
            value = .30 * low + .14 * math.sin(math.tau * (420 * t - 220 * t * t)) + .06 * math.sin(math.tau * 840 * t)
            env = math.sin(math.pi * p) ** .7
        elif cue == "crumble":
            value = noise * max(0, math.sin(t * 370)) ** 9 * .45 + low * .12
        elif cue == "toss":
            value = low * .30 + .045 * math.sin(math.tau * (270*t-160*t*t))
            env = math.sin(math.pi*p) ** 1.4
        elif cue == "land":
            value = .25 * math.sin(math.tau*90*t) * math.exp(-t*30) + .10*low
        else:
            value = low * .65 + noise * .09 + .06 * math.sin(math.tau * (100 * t - 60 * t * t))
        samples.append(int(max(-1, min(1, value * env)) * 24000))
    with wave.open(str(OUT / f"golm_{cue}.wav"), "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(struct.pack(f"<{len(samples)}h", *samples))
    print(cue, len(samples))
