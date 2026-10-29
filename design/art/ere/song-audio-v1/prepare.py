"""提供された2曲を演出用に整える。時計まで流す版では3秒で切らない。原本は変更しない。"""
from pathlib import Path
import hashlib
import json
import wave
import argparse
import av
import numpy as np

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[3]
RATE = 48000
parser = argparse.ArgumentParser()
parser.add_argument('--through-clock', action='store_true')
args = parser.parse_args()
report = []
for label, filename, start in [('a', 'Ah.mp3', .08), ('b', 'Ah (1).mp3', 1.10)]:
    source = Path('C:/Users/Pon/Downloads') / filename
    with av.open(str(source)) as container:
        assert container.streams.audio[0].codec_context.sample_rate == RATE
        data = np.concatenate([f.to_ndarray() for f in container.decode(audio=0)], axis=1)
    assert data.shape[0] == 2
    finish = data.shape[1] if args.through_clock else round((start + 3) * RATE)
    clip = data[:, round(start * RATE):finish].copy()
    assert clip.shape[1] >= RATE * 3
    # 比較時の音量差を抑える。ピークには余裕を残す。
    rms = float(np.sqrt(np.mean(clip ** 2)))
    gain = min(10 ** (-18 / 20) / rms, 10 ** (-1 / 20) / float(np.max(np.abs(clip))))
    clip *= gain
    # 録音の切断点だけを短くなじませる。終端の余韻はゲームの共通時計で制御する。
    fade = round(.01 * RATE)
    clip[:, :fade] *= np.linspace(0, 1, fade)
    clip[:, -fade:] *= np.linspace(1, 0, fade)
    name = 'clock' if args.through_clock else 'ah'
    output = REPO / f'DemoApp/assets/audio/se/ere_song_{name}_{label}.wav'
    with wave.open(str(output), 'wb') as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(np.rint(clip.T * 32767).astype('<i2').tobytes())
    row = {'label': label, 'source': str(source), 'sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
           'source_seconds': data.shape[1] / RATE, 'start': start, 'seconds': clip.shape[1] / RATE,
           'gain_db': float(20 * np.log10(gain)), 'rms_dbfs': float(20 * np.log10(np.sqrt(np.mean(clip ** 2)))),
           'peak_dbfs': float(20 * np.log10(np.max(np.abs(clip)))), 'runtime_db': -8,
           'output': str(output.relative_to(REPO))}
    report.append(row)
metadata = 'clips-clock.json' if args.through_clock else 'clips.json'
(ROOT / metadata).write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('ERE_SONG_CLIPS_OK', [(r['label'], r['source_seconds'], round(r['rms_dbfs'], 2)) for r in report])
