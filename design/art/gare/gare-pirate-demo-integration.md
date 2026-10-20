# ガレの女海賊キャプテン版適用

2026-10-20。ユーザーの差し替え指示により、海賊キャプテンのコンセプト v2 から作成した正面・戦闘立ち絵をDemoAppへ採用。

以下は肩担ぎ大砲版の初回適用記録。現在は末尾の「散弾砲筒版への差し替え」を採用。

## 採用画像

| 用途 | 原本（design/art/gare/ 以下） | ゲーム内ファイル |
|---|---|---|
| 編成・待機画面 | gare-pirate-captain-front-v1.png | DemoApp/assets/portraits/gare.png |
| 右向き戦闘待機 | gare-pirate-captain-idle-right-v1.png | DemoApp/assets/portraits/battle/gare_idle_right.png |

両方1024×1536のRGBA透過PNG。原本とゲーム配置先はSHA-256一致。生成アルファを加工せずコピー。
旧ゴブリン版の原本 `gare-front-v1.png` / `gare-idle-right-v1.png` は保持。差し替え前のゲーム画像がこれらと一致することも確認。
内蔵 image_gen の生成プロンプトは `gare-pirate-captain-production-v1-prompts.md`。

## 表示調整

- `UiKit.PortraitWorldHeight("gare")`: 1.90 → 2.65。小鬼から成人女性の体格へ変更し、巨大砲を含む全身で表示。
- `UiKit.BattlePortraitBottomPaddingRatio("gare")`: 63 / 1536 → 20 / 1536。戦闘画像のalpha 128以上の最下端は1515行目（0始まり）。
- 名前・ID・能力・数値・戦闘ルールは変更なし。ヒビとワタの画像・表示設定も維持。

## 確認

- `dotnet build DemoApp/DemoApp.csproj -v minimal` 成功、エラー0。既存の `BorgAttackClipCheck.cs` にCS8602警告1件。
- `capture-pirate-integration.gd` で寸法・背景透過・原本とのSHA-256一致を検証。
- 実際の編成画面と戦闘画面をVulkan Forward+で撮影し、海賊版の読込、背景透過、右向き、巨大砲の輪郭、体格、接地を目視確認。
- `GARE_PIRATE_ART_CAPTURE result=0`、終了コード0。終了時のみ既存のRID/ObjectDB解放警告が残る。

確認画像: `pirate-selection-game-check.png` / `pirate-battle-game-check.png`。

再撮影:

```powershell
dotnet build DemoApp/DemoApp.csproj -v minimal
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/gare/capture-pirate-integration.gd --audio-driver Dummy
```

## 散弾砲筒版への差し替え

2026-10-20。ユーザー指示で、同じ女海賊が短い砲筒を両手で構える版へ差し替え。

| 用途 | 現在の原本（design/art/gare/ 以下） | ゲーム内ファイル |
|---|---|---|
| 編成・待機画面 | gare-pirate-scattergun-front-v1.png | DemoApp/assets/portraits/gare.png |
| 右向き戦闘待機 | gare-pirate-scattergun-idle-right-v1.png | DemoApp/assets/portraits/battle/gare_idle_right.png |

両方1024×1536のRGBA透過PNG。原本と適用先のSHA-256一致を確認。旧ゴブリン版・肩担ぎ大砲版の原本は保持。
顔・衣装・赤髪を維持し、砲口の破片と腰の破片袋で拾った欠片を弾にする役割を表す。
生成プロンプトは `gare-pirate-scattergun-production-v1-prompts.md`。

- 表示高は2.65を維持。足元余白を20 / 1536から31 / 1536へ変更（戦闘画像のalpha 128以上の最下端は1504行目・0始まり）。
- 名前・ID・能力・数値・戦闘ルールに変更なし。
- DemoAppビルド成功、エラー0。既存のBattleCoreにCS0162とCS8602、BorgAttackClipCheck.csにCS8602の計3警告。
- 撮影スクリプトに `--scattergun` を追加し、この版の原本とのSHA-256一致・寸法・背景透過を確認。
- Vulkan Forward+の実際の編成・戦闘画面で、画像読込・右向き・砲筒の輪郭・体格・接地を目視確認。
- `GARE_PIRATE_SCATTERGUN_ART_CAPTURE result=0`、終了コード0。終了時のみ既存のRID/ObjectDB等の解放警告が残る。

現在の確認画像: `pirate-scattergun-selection-game-check.png` / `pirate-scattergun-battle-game-check.png`。

再撮影:

```powershell
dotnet build DemoApp/DemoApp.csproj -v minimal
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/gare/capture-pirate-integration.gd --audio-driver Dummy -- --scattergun
```
