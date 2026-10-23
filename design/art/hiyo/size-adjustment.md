# ヒヨの表示サイズ調整

日付: 2026-10-24。リィカと並べたときに小さすぎるとの指摘を受け、ユーザー指定の2.10へ変更。

- `UiKit.PortraitWorldHeight("hiyo")`: 1.89 → 2.10（約11.1%拡大）。
- 待機連番は `BattlePawn3D.IdleLoop` が同じ基準高を使用するため、同率で拡大。
- 支援動作は `RefreshBattlePortrait` の既存の `2.10f / 1.89f` の画像間補正を維持。待機との比率を変えずに同率で拡大（動作絵のキャンバス高は従来2.10 → 約2.333）。補正を削除したり分母を新基準高に置き換えたりしない。
- 足元余白率は変更せず、拡大後も接地を維持。画像原本・連番・戦闘ルール・能力値は変更していない。

## 確認

`dotnet build DemoApp/DemoApp.csproj --no-restore` 成功（0エラー、BattleCoreのCS0162・CS8602の警告各1件）。待機連番と支援差分が基準高を使用する経路をソースで確認。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/hiyo/capture-size-integration.gd --audio-driver Dummy
```

ミオ・ヒヨ・シオ・トウ・リィカを同時に表示し、`size-battle-game-check.png` を保存。全身の大きさ、接地、HP表示との間隔を目視確認。`HIYO_SIZE_GAME_CAPTURE_OK result=0`、終了コード0。終了時にCanvasItem・ObjectDB・FontAdvanced等の解放警告あり。
