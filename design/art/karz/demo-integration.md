# カーズの立ち絵をゲームへ適用

適用日: 2026-10-30。ユーザー指定の採用方向は `karz-concept-v6-cast-style.png`。
待機用正面・戦闘用右向きの静止画を内蔵 image_gen で作成し、既存の ID 規約で読み込ませた。

- 正面原本: `karz-front-v1.png` → `DemoApp/assets/portraits/kari.png`。
- 戦闘原本: `karz-idle-right-v1.png` → `DemoApp/assets/portraits/battle/kari_idle_right.png`。
- 両方1024×1536の透過RGBA PNG。採用原本とゲーム素材のSHA-256が一致。
- 内部IDは改名前からの `kari`。追加前に当該IDの独立画像・ready差分・待機連番が無いことを確認。
- `UiKit.PortraitWorldHeight("kari")` はネルと同じ2.50、下端補正は裾のアルファ128以上の最下行1505に合わせて30/1536。骨の足は裾より高い浮遊姿勢。
- 画像のプロンプトと参照元は `karz-production-v1-prompts.md`。
- 戦闘ルール・数値・特性・既存の刻印演出は変更していない。アニメーション連番は本作業の対象ではない。

## 確認

`dotnet build DemoApp/DemoApp.csproj --no-restore` 成功。警告0、エラー0。
Godot 4.7.2のheadless editor importで新規画像2枚をインポート。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/karz/capture-integration.gd --audio-driver Dummy --log-file 'C:/works/VillainProto/design/art/karz/game-capture.log'
```

Vulkan Forward+、RTX 5090で実際の編成・戦闘画面を撮影。
`KARZ_GAME_CAPTURE_OK result=0`、終了コード0。
`selection-game-check.png` と `battle-game-check.png` を目視し、全身、透過、右向き、足元、周囲の駒との相対サイズを確認。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless --quit-after 120000 --log-file 'C:/works/VillainProto/design/art/karz/battle-smoke.log' -- --demo-autoplay --demo-quit --demo-fast '--demo-preset=試遊・カーズ 呪い' --demo-stage=3 --demo-seed=0
```

`DEMO_SMOKE_COMPLETE events=239 turns=4 won=True attackPlays=29`、終了コード0。
両方の実行で終了時にFontAdvanced・CanvasItem・ObjectDBの解放警告あり。画像読み込み・戦闘再生の失敗なし。
`git diff --check -- DemoApp/UiKit.cs` も成功。
