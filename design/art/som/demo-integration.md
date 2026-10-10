# ソムと背いた獣 立ち絵適用

2026-10-10。承認済みの `som-and-beast-concept-v1.png` から、Codex 内蔵 image_gen で生成。
全文は `som-production-v1-prompts.md`。

| 駒 | 素材 | ゲームの読込先 |
|---|---|---|
| ソム・通常 | `som-front-v1.png` | `DemoApp/assets/portraits/som.png` |
| ソム・戦闘右向き | `som-idle-right-v1.png` | `DemoApp/assets/portraits/battle/som_idle_right.png` |
| 背いた獣・通常 | `fodder-front-v1.png` | `DemoApp/assets/portraits/fodder.png` |
| 背いた獣・戦闘右向き | `fodder-idle-right-v2.png` | `DemoApp/assets/portraits/battle/fodder_idle_right.png` |

ソムは1024×1536、背いた獣は1254×1254。全4枚RGBA PNGで、背景のアルファ0を確認。
ソムは召喚書を持つ尊大な召喚士、背いた獣は同じ契約紋章を着けたふてぶてしい猫悪魔。
戦闘絵は右向きで、敵陣では既存の左右反転が適用される。雷・魔法陣は絵に焼き込んでいない。
背いた獣は編成選択できない駒で、通常絵は素材として用意しIDの読込先にも配置。

`UiKit` の表示高はソム2.50・背いた獣0.95。
戦闘絵のアルファ128以上の最下端（0始まり）はソム1505・背いた獣1236（v2）。
足元余白は30/1536・17/1254として設定。戦闘ロジック・数値・台本の変更なし。

## 確認

- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 成功、エラー0。変更していないBattleCoreでCS0162・CS8602の警告2件。
- `capture-integration.gd`: `SOM_ART_CAPTURE beast_visible=true result=0`、終了コード0。
- `selection-game-check.png`: 編成画面でソムの通常絵、背景透過を確認。
- `battle-game-check.png`: ソムの右向き・接地、敵陣に召喚された背いた獣の画像と小型の体格を目視確認。
- 撮影終了時にGodotのFont/CanvasItem/ObjectDB解放警告あり。撮影は正常に完了。
- `git diff --check -- DemoApp/UiKit.cs`: 問題なし。

2026-10-10追記: 背いた獣の戦闘絵をv2へ更新。v1にあった瞳孔の重複を、両目それぞれ1本に修正。
内蔵 image_gen による編集。全文は `fodder-idle-right-v2-prompt.md`。v1は履歴として保持。

再撮影:

```powershell
Godot_console.exe --path DemoApp --script ../design/art/som/capture-integration.gd --audio-driver Dummy --rendering-method gl_compatibility
```
