# 墓守リィカ 立ち絵リメイクのゲーム適用

適用日: 2026-10-24。ユーザーが戦闘v6を採用し、ゲーム適用を依頼。

- 通常: `rica-standing-remake-v3-shovel-lean.png` → `DemoApp/assets/portraits/rica.png`
- 戦闘: `rica-battle-idle-right-remake-v6.png` → `DemoApp/assets/portraits/battle/rica_idle_right.png`
- 旧画像は `originals/rica-before-remake.png` と `originals/rica-idle-right-before-remake.png` に保存。
- リィカには優先される ready 差分・待機連番がないため、既存のID規約による読み込みで適用。
- `UiKit.BattlePortraitBottomPaddingRatio("rica")` を旧画像用の `0.0600f` から新画像の `40f / 1536f` に更新。アルファ128以上の足元は最終行1495、下端余白40px。
- 並べた際に小さすぎるとの指摘を受け、表示サイズを1.75から2.10へ20%拡大。シオ・トウ・ミオの2.25より少し小柄に見える基準。ササは1.90。画像原本と接地の余白率は維持。
- 戦闘ルール・数値・特性は変更していない。

## 確認

`dotnet build DemoApp/DemoApp.csproj --no-restore` 成功（0エラー）。BattleCoreに警告CS0162・CS8602が各1件。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/rica/capture-integration.gd --audio-driver Dummy
```

Vulkan Forward+で実際の編成・戦闘画面を撮影。`RICA_REMAKE_GAME_CAPTURE_OK result=0`、終了コード0。採用原本とゲーム素材のSHA-256一致、1024×1536の寸法、透過を確認。

`selection-game-check.png` と `battle-game-check.png` を目視し、新しい通常絵・戦闘絵、透過、全身、接地を確認。終了時にFontAdvanced・CanvasItem・ObjectDBの解放警告あり。

## 表示サイズ調整後の確認

表示サイズ2.10への変更後、`dotnet build DemoApp/DemoApp.csproj --no-restore` 成功（警告0・エラー0）。上記の撮影コマンド末尾に `-- --small-roster` を付け、ミオ・シオ・ササ・トウ・リィカを並べて確認した。`RICA_REMAKE_GAME_CAPTURE_OK result=0`、終了コード0。

`size-selection-game-check.png` と `size-battle-game-check.png` を保存。戦闘画面でリィカの拡大後の全身、足元の接地、HP表示と周囲の駒との間隔を目視確認。終了時の解放警告は前回と同様。
