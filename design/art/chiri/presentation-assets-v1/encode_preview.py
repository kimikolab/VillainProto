"""Godotで収録したプレビューを共有用MP4へ変換する（元の立ち絵は編集しない）。"""
from pathlib import Path
import av

root = Path(__file__).resolve().parent / "previews"
destination = root / "chiri-materials.mp4"
if destination.exists():
    raise FileExistsError(destination)

with av.open(str(root / "chiri-materials.avi")) as source:
    with av.open(str(destination), "w") as output:
        video = output.add_stream("libx264", rate=24)
        video.width, video.height = 1440, 900
        video.pix_fmt = "yuv420p"
        video.options = {"crf": "20", "preset": "fast"}
        audio = output.add_stream("aac", rate=48000)
        audio.layout = "stereo"
        audio.bit_rate = 128000
        resampler = av.AudioResampler(format="fltp", layout="stereo", rate=48000)
        count = 0
        for frame in source.decode():
            if isinstance(frame, av.VideoFrame):
                for packet in video.encode(frame):
                    output.mux(packet)
                count += 1
            elif isinstance(frame, av.AudioFrame):
                for converted in resampler.resample(frame):
                    for packet in audio.encode(converted):
                        output.mux(packet)
        for converted in resampler.resample(None):
            for packet in audio.encode(converted):
                output.mux(packet)
        for stream in [video, audio]:
            for packet in stream.encode(None):
                output.mux(packet)

with av.open(str(destination)) as check:
    assert len(check.streams.video) == 1 and len(check.streams.audio) == 1
    decoded = sum(1 for _ in check.decode(video=0))
    assert decoded == count, (decoded, count)
print(f"{destination}: {count} frames / 24fps, video+audio, decode verified")
