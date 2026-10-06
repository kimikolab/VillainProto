# トウ 立ち絵のゲーム適用

- 通常: `DemoApp/assets/portraits/tou.png` ← `tou-front-v3.png`。
- 戦闘右向き: `DemoApp/assets/portraits/battle/tou_idle_right.png` ← `tou-idle-right-v1.png`。
- 通常は5本指・垂れた触角・頭の傾きに沿う付け根の修正版。戦闘は既存の右向き版。
- 両方1024×1536の透過PNG。既存のID命名規約で読み込まれる。
- 戦闘画像のアルファ128以上の最下端は1504行目（0始まり）。下端余白31px/1536を `UiKit.BattlePortraitBottomPaddingRatio` に設定。

## 確認

- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 警告0・エラー0。
- `selection-game-check.png` / `battle-game-check.png`: 実画面で画像の切替、透過、右向き、足元を目視確認。
- `TOU_ART_CAPTURE result=0`、撮影終了コード0。
- 制限環境での初回起動はGodotSharpの.NETアセンブリ読込で失敗。通常の実行環境に切り替えた撮影で成功。既存のアンカー・終了時RID解放警告あり。
- 戦闘ルール・数値・生成表は今回変更していない。

再撮影: `Godot_v4.7.2-stable_mono_win64_console.exe --path DemoApp --script ../design/art/tou/capture-integration.gd --audio-driver Dummy --rendering-method gl_compatibility`
