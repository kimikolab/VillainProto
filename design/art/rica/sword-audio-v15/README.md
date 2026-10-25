# リィカの覚醒攻撃SE v15

2026-10-25。指定のHelton Yan's Pixel CombatのSword Criticalを段別に割り当てる。

| 層 | 指定ファイル末尾 | 再生 |
|---|---|---|
| 0–2 | 金属叩き（従来） | シャベルの命中で1音・-8 dB |
| 3–5 | PC-001.wav | 回転薙ぎの命中で1音・-10 dB |
| 6–8 | PC-002.wav | 二連撃の各打に1音・-10 dB |
| 9以上 | PC-003.wav | 三連撃の各打に1音・-10 dB |

コピー先と元ファイル名は [SOURCES.md](../../../../DemoApp/assets/audio/se/SOURCES.md)。原本から無加工でコピーし、3本ともSHA256一致を確認した。
購入音源本体はGitから除外する。未配置なら従来の共通斬撃音を使う。

本編・FxViewer共通の `BattlefieldView3D.Attack` で、連番の接触を待った直後に `PlayRicaHit` を1回呼ぶ。範囲攻撃の対象人数ぶんは重ねない。通常SEの4音枠・停止処理を共用し、再生速度によって音程を変えない。

[SE確認動画](rica-four-stage-sword-audio-v15.mp4) は4段階を順番に再生する。映像はv14の実際のFxViewer撮影、音声はゲームと同じ素材・音量を接触コマへ同期した確認用。BGMとほかのSEは省略する。

ビルドは警告0・エラー0。既存のRicaMotionCheckは頭なしで520項目を通過し、全7攻撃・4段階・左右反転・接触・停止・速度・中断・再戦・HPを確認した。
終了時の既存RID・Font・ObjectDB解放警告は残る。音源読込エラーなし。ログは `output/rica-motion-integration/v15-*.log`。
[動画検証](video-check.json)：780コマ・26秒・30fps、全コマ再デコード済み、音声ピーク0.410でクリップなし。
再生成は `D:/comfyui/venv/Scripts/python.exe design/art/rica/sword-audio-v15/encode_preview.py`。
