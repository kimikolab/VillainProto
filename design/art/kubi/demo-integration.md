# 萎縮のクビ — 立ち絵のゲーム適用

2026-11-01。ユーザー依頼「ゲームの待機画面と戦闘中の立ち絵にしてみて」に対応。

| 用途 | 原本 | ゲーム配置先 |
|---|---|---|
| 待機・編成画面 | kubi-front-v1.png | DemoApp/assets/portraits/kubi.png |
| 戦闘待機・右向き | kubi-idle-right-v1.png | DemoApp/assets/portraits/battle/kubi_idle_right.png |

内蔵 image_gen で承認された鐘番コンセプト v2 から生成。プロンプトは [kubi-production-v1-prompts.md](kubi-production-v1-prompts.md)。
両方とも1024×1536のRGBA透過PNG。加工なしでコピーし、配置先と原本のSHA-256一致を確認。

## 表示と確認

- UiKit の既存ID規約で読み込む。表示高は2.05、戦闘絵の足元余白は33 / 1536（alpha 128以上の最下端1502行目）。
- 鐘を抱えて鳴子を押さえる仕草、伏せた耳、華奢で内側に縮んだ姿勢を維持。
- dotnet build DemoApp/DemoApp.csproj -v minimal 成功。警告0・エラー0。
- capture-integration.gd で寸法・四隅の透過・原本のハッシュを検査し、本番 Main の編成画面と戦闘画面を撮影。
- KUBI_ART_CAPTURE result=0 size=True alpha=True source_hash=True、終了コード0。
- selection-game-check.png / battle-game-check.png を目視確認。正面と右向きの切り替え、透過、体格、接地、鐘と顔の視認性を確認。
- 撮影終了時に既存のエレ撮影と同種のRID/ObjectDB・リソース解放警告あり。撮影と画像検査は成功。
- 今回の変更は静止立ち絵2枚と表示高・接地補正。戦闘ルール・能力・演出のタイミングには変更なし。

再撮影:
```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script C:/works/VillainProto/design/art/kubi/capture-integration.gd --audio-driver Dummy
```
