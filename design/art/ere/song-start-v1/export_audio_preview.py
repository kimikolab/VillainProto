"""Godot MovieWriterの実音声付きAVIを、確認用のMP4へ変換する。"""
from pathlib import Path
from fractions import Fraction
import argparse
import json
import av
import numpy as np

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('--source', type=Path, default=REPO / '.tmp/ere-harpsichord-game.avi')
parser.add_argument('--target', type=Path, default=ROOT / 'ere-harpsichord-trial.mp4')
parser.add_argument('--sound', default='400 Sounds Pack / harpsichord_mystery.wav / -6 dB')
parser.add_argument('--width', type=int, default=1440)
args = parser.parse_args()
target = args.target.resolve()
assert target.parent.is_dir() and target.suffix == '.mp4'
assert args.width > 0 and args.width % 2 == 0
packets = []
count = samples = 0
peak = 0.0
with av.open(str(args.source)) as original, av.open(str(target), 'w', options={'movflags': '+faststart'}) as output:
    source_video = original.streams.video[0]
    video = output.add_stream('libx264', rate=30)
    video.width = args.width
    video.height = round(source_video.height * video.width / source_video.width / 2) * 2
    video.pix_fmt = 'yuv420p'
    video.options = {'crf': '21', 'preset': 'medium'}
    audio = output.add_stream('aac', rate=48000)
    audio.layout, audio.bit_rate = 'stereo', 192000
    for frame in original.decode(video=0):
        frame = frame.reformat(width=video.width, height=video.height, format='yuv420p')
        frame.pts, frame.time_base = count, Fraction(1, 30)
        packets.extend(video.encode(frame))
        count += 1
    packets.extend(video.encode())
    # 音の時刻・音量はゲームの録音を維持。後付けのSEや正規化は行わない。
    resampler = av.AudioResampler(format='fltp', layout='stereo', rate=48000)
    with av.open(str(args.source)) as recording:
        def encode(frame):
            global samples, peak
            peak = max(peak, float(np.max(np.abs(frame.to_ndarray()))))
            frame.pts, frame.time_base = samples, Fraction(1, 48000)
            samples += frame.samples
            packets.extend(audio.encode(frame))
        for frame in recording.decode(audio=0):
            for converted in resampler.resample(frame):
                encode(converted)
        for converted in resampler.resample(None):
            encode(converted)
    packets.extend(audio.encode())
    packets.sort(key=lambda p: p.dts * p.time_base)
    for packet in packets:
        output.mux(packet)

assert count > 300 and .005 < peak < .98, (count, peak)
with av.open(str(target)) as check:
    assert check.streams.audio[0].codec_context.name == 'aac'
    assert sum(1 for _ in check.decode(video=0)) == count
with av.open(str(target)) as check:
    sound = np.concatenate([f.to_ndarray() for f in check.decode(audio=0)], axis=1)
    # 最初の演奏区間にも実際に音があることを検査。
    start_peak = float(np.max(np.abs(sound[:, int(.3 * 48000):int(1.9 * 48000)])))
    assert .005 < start_peak < .98, start_peak
report = {'source': str(args.source.relative_to(REPO)), 'frames': count, 'fps': 30,
          'seconds': count / 30, 'audio': 'Godot MovieWriterの実録音 / AAC 48kHz stereo',
          'peak': peak, 'performance_start_peak': start_peak,
          'motion': '既存C・変更なし', 'sound': args.sound}
target.with_suffix('.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('ERE_AUDIO_PREVIEW_OK', json.dumps(report, ensure_ascii=False))
