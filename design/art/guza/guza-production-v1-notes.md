# グザ 瘴気の運び手 本番立ち絵 v1

2026-10-22、ユーザーが採用した低頭身・フードの奥に目の光だけが見える運び手を基準に制作。生成は Codex 内蔵 image_gen。

## 採用点

- 約4〜5頭身の小柄な体と、大きな縫い合わせの革袋。
- 黄土色のぼろ布のフード。顔は不透明な暗がりで、暖色の目が2つだけ光る。素顔・鼻・嘴・マスクは描かない。
- ペストマスクはラウの方向性として分ける。
- 袋の口を肩の横で押さえ、もう一方の手で底を支える。口から少量の紫の瘴気が漏れる。
- 毒とその照り返しは紫系。既存立ち絵に合わせた輪郭線とアニメ調の塗り。

## 出力

- [guza-carrier-concept-v1.png](guza-carrier-concept-v1.png): 採用コンセプト。
- [guza-front-v1.png](guza-front-v1.png): 編成・勝利用の待機。1024 × 1536。
- [guza-idle-right-v1.png](guza-idle-right-v1.png): 右向きの戦闘待機。1024 × 1536。
- ゲーム配置先: `DemoApp/assets/portraits/guza.png` と `DemoApp/assets/portraits/battle/guza_idle_right.png`。原本と同一ファイル。
- [guza-production-v1-prompts.txt](guza-production-v1-prompts.txt): 最終画像の生成プロンプト。

RGBA PNGの生成原本を保存。四隅のalpha=0、アルファの範囲0〜254。袋・顔・衣服の暗部も画像として保持し、背景除去などの後処理はしていない。

## ゲーム内確認

`UiKit` のファイル名規約で読まれる。体格は高さ2.00、戦闘画像の足元余白は78 / 1536に設定。
「試遊・毒」の編成画面と第三波の戦闘画面で、画像切り替え・背景透過・足元・周囲の駒との並びを確認。
共通の確認記録は [毒軸立ち絵の確認](../poison-portraits-v1/verification-notes.md)。

待機モーションの連番、噴き出しや返り毒の演出、攻撃ポーズは今後の制作対象。
