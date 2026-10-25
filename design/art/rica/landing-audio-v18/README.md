# リィカの着地音を聞き取りやすくする v18

2026-10-25。「着地音が聞こえない」という指摘への調整。着地音だけを-12 dBから **-6 dB（+6 dB）** に変更した。

録音の16.140秒付近を元の着地音と照合し、初版にも着地音の成分があることを確認した。刀音-8 dBと同時に鳴るため、着地音が弱く埋もれやすい設定だった。
着地の時刻・素材・ピッチ・斬撃音・刀音・覚醒音・吸収音は同じ。2音を比べて判断しやすいように、[三連撃の調整前→調整後・14秒](rica-landing-before-after-v18.mp4)を用意した。

[全体の実録画・22秒](rica-awaken-absorb-landing-v18.mp4)も保存。どちらもGodot Movie Makerの実ミックスを使用し、音の後付けはしていない。

- ビルド：警告0・エラー0。
- 実録画の接地は両方とも485コマ目、着地音は1回。
- [比較検証](comparison-check.json)：420コマ・30fps。実ミックスから推定した着地音成分は約-11.7→-5.8 dB。元音との相関も上昇した。音声ピーク0.533でクリップなし。
- [全体の検証](video-check.json)：660コマ・30fps。音声ピーク0.544。覚醒と吸収の録音値は初版と一致。
- 音量だけの変更のため新規のモーション検査は追加せず、実録画の同期・再生回数と音声成分を確認した。ログは `output/rica-motion-integration/v18-*.log`。

再生成はv17と同じ `--rica-event-audio-capture` を使い、Movie Makerの保存先を `.tmp/rica-event-audio-v18/review.avi` にする。その後は次を実行する。

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/event-audio-v17/encode_review.py --input .tmp/rica-event-audio-v18/review.avi --output-dir design/art/rica/landing-audio-v18 --name rica-awaken-absorb-landing-v18.mp4
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/landing-audio-v18/encode_comparison.py
```
