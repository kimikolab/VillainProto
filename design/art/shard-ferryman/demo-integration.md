# ガレ・ヒビ・ワタの立ち絵適用

2026-10-19。ユーザー指定のガレ v2・ヒビ v2・ワタ v2 を基準に、内蔵 image_gen で正面待機と右向き戦闘待機を各1枚作成し、DemoApp に配置した。
ヒビは背負い盾・傷んだ白と淡青の布を持つ v2 を採用。衣装を整理した v3 は使用していない。

以下は初回適用の記録。ガレは2026-10-20に海賊キャプテン版へ差し替えた。現在の素材と表示設定は `../gare/gare-pirate-demo-integration.md` を参照。ヒビとワタはこの記録のまま。

## 採用素材

| 駒 | 原本（design/art/ 以下） | 編成・待機画面 | 戦闘待機 |
|---|---|---|---|
| ガレ | gare/gare-front-v1.png、gare/gare-idle-right-v1.png | DemoApp/assets/portraits/gare.png | DemoApp/assets/portraits/battle/gare_idle_right.png |
| ヒビ | hibi/hibi-front-v1.png、hibi/hibi-idle-right-v1.png | DemoApp/assets/portraits/hibi.png | DemoApp/assets/portraits/battle/hibi_idle_right.png |
| ワタ | wata/wata-front-v1.png、wata/wata-idle-right-v1.png | DemoApp/assets/portraits/wata.png | DemoApp/assets/portraits/battle/wata_idle_right.png |

全6枚は1024×1536のRGBA PNG。原本とゲーム配置先はSHA-256一致。生成されたアルファを加工せず保持。
全画像の四隅はalpha 0または1、画像全体のalpha範囲は0〜254。透過部分に残るRGBは実ゲームでは表示されない。
浮遊する鏡片・発砲・灯籠の外へ流れる呪いの煙は焼き込まず、灯籠内部の呪いの表現だけ残した。
静止画の立ち絵であり、待機モーションの連番は今回の対象外。

生成・編集プロンプト全文:

- ../gare/gare-production-v1-prompts.md
- ../hibi/hibi-production-v1-prompts.md
- ../wata/wata-production-v1-prompts.md

## 表示調整

画像は既存の駒IDによるパス規約で読み込む。UiKit.cs の表示高と足元余白のみ追加。

| 駒 | 表示高 | 戦闘絵のalpha 128以上の最下端（0始まり） | 下端の余白 | 設定値 |
|---|---:|---:|---:|---|
| ガレ | 1.90 | 1472 | 63px | 63 / 1536 |
| ヒビ | 2.65 | 1495 | 40px | 40 / 1536 |
| ワタ | 2.50 | 1510 | 25px | 25 / 1536 |

ガレは小柄な小鬼、ヒビは重装の力強い女性として体格を調整。戦闘ルール・数値・特性・台本生成は変更なし。

## 検証

- `dotnet build DemoApp/DemoApp.csproj -v minimal` 成功。エラー0。既存のBattleCoreにCS0162とCS8602、DemoApp/BorgAttackClipCheck.csにCS8602の計3警告。
- 全6枚の寸法・RGBA・四隅の透過・原本とのハッシュ一致を確認。
- `capture-integration.gd` で実際の編成画面に3人を配置し、各選択状態と右向き戦闘待機を撮影。
- 通常のVulkan Forward+とCompatibilityの両方で `SHARD_FERRYMAN_ART_CAPTURE result=0`、終了コード0。保存画像は最後に撮影したForward+のもの。
- 保存画像を目視確認し、背景透過・画像の欠け・体格差・右向き・足元の位置を確認。
- 終了時にCanvasItem/FontAdvancedのRIDとObjectDBの解放警告が残る。画像読込・撮影の失敗はなし。
- `git diff --check` 成功。

確認画像:

- gare-selection-game-check.png
- hibi-selection-game-check.png
- wata-selection-game-check.png
- battle-game-check.png

再撮影:

```powershell
dotnet build DemoApp/DemoApp.csproj -v minimal
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/shard-ferryman/capture-integration.gd --audio-driver Dummy
```
