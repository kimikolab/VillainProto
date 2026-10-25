# リィカの命中音を重ねる v16

2026-10-25。v15のSword Criticalへ、ユーザー指定の効果音ラボの刀音を追加する。

| 段 | 基本音 | 同時に重ねる音 |
|---|---|---|
| 3–5層 | Sword Critical 001 | 刀で斬る5.mp3 |
| 6–8層 | Sword Critical 002 | 刀で斬る4.mp3 |
| 9層以上 | Sword Critical 003 | 刀で斬る1.mp3 |

各打の接触コマで両方を同時に開始する。原本は無加工でコピーしSHA256一致を確認した。配置名・元フォルダは [SOURCES.md](../../../../DemoApp/assets/audio/se/SOURCES.md)。追加音本体とインポート情報はGitから除外する。

現在はSword Criticalが-10 dB、「刀で斬る」が-8 dB（刀音だけ+2 dB）・ピッチ1。Godotの `AudioStreamSynchronized` で1組にまとめ、通常SEの1枠を使う。三連撃で2音ずつ鳴らしても、追加音が元の剣音を押し出さない。既存の4枠・音量調整・停止・再戦時の処理を共用する。0–2層のシャベルは金属叩きを継続する。

[SE確認動画・刀音+2 dB](rica-four-stage-layered-audio-v16-hit-plus2db.mp4) は各段を順番に再生。映像はv14のFxViewer撮影、音声は本編と同じ素材・音量を命中コマに同期した確認用で、BGMやほかのSEは省略する。[初版・両方-10 dB](rica-four-stage-layered-audio-v16.mp4)も比較用に残す。

ビルドは警告0・エラー0。既存のRicaMotionCheckは頭なしで520項目を通過。全7攻撃・4段階・左右反転・接触・停止・速度・中断・再戦・HPを確認した。ログは `output/rica-motion-integration/v16-*.log`。終了時の既存RID・Font・ObjectDB解放警告は残るが、音源読み込みエラーはない。

[動画検証](video-check.json)：780コマ・26秒・30fps、全コマを再デコード済み。音声ピーク0.410でクリップなし。[同期記録](preview-check.json) に各打の接触コマ・2レイヤーの元ファイルとハッシュを保存する。

音量調整版の検証は [video-check-hit-plus2db.json](video-check-hit-plus2db.json)。保持時間・接触・音源には変更なし。音量変更後のビルドと動画の全コマ再デコード・音割れ検査を実施する。

再生成：`D:/comfyui/venv/Scripts/python.exe design/art/rica/sword-audio-v15/encode_preview.py --hit-layer --hit-boost-db 2 --suffix hit-plus2db`
