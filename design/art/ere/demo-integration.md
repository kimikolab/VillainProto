# 哀歌のエレ 立ち絵適用

2026-10-23に初回適用。2026-10-24のユーザー指示で、戦闘待機はv3のA、哀歌を奏でる手番はCに変更。

| 用途 | 原本（design/art/ere/ 以下） | ゲーム内ファイル |
|---|---|---|
| 編成・待機画面 | ere-front-v1.png | DemoApp/assets/portraits/ere.png |
| 右向き戦闘待機（A） | ere-master-bard-right-v3a-still.png | DemoApp/assets/portraits/battle/ere_idle_right.png |
| 哀歌の歌唱（C） | ere-master-bard-right-v3c-song.png | DemoApp/assets/portraits/battle/ere_singing_idle_right.png |

3枚とも1024×1536のRGBA透過PNG。画像は加工せずコピー。ゲーム配置先と原本のSHA-256一致を確認。
生成プロンプト・原本の検証は ere-production-v1-prompts.md と ere-master-bard-right-v3-prompts.md。

## 表示調整

- 駒ID `ere` の既存命名規則で通常絵・戦闘絵を読み込む。
- `UiKit.PortraitWorldHeight("ere")` は成人女性の体格として2.50に設定。ネルと同じ表示高。
- 足元余白はAが16 / 1536、Cが13 / 1536。各画像のalpha 128以上の最下端に基づき接地を補正。
- 名前・ID・能力・数値・戦闘ルールには変更なし。

## 歌唱の切り替え（2026-10-24）

- 待機は前を見て静かに弾くA。実際に哀歌を奏でる手番だけ、目を閉じて歌うCを約0.8秒（1倍速）表示し、Aへ戻す。
- `Skill` は音が足りず通常攻撃になる手番にも出るため、`ErePresentation` が同じ行動内の `Highlight` / `Attack` の順序を読む。ログ本文・音の閾値からは判断しない。
- `Main.HoldDirgeSong` は停止中に時間を進めず、途中の速度変更に追従する。再戦・中断は再生トークンで終了。死亡・蘇生・勝利も歌唱状態を解除する。
- 購入済みNatureBlocks/EffectBlocksの用途と配置を確認。今回の変更は承認済みキャラクター画像の切り替えのみで、これらの背景・汎用エフェクト素材の適用箇所はない。

## 更新後の確認（2026-10-24）

- DemoAppビルド成功、警告0・エラー0。
- 実戦6台本で歌唱8件・通常攻撃への代替7件を検査。歌唱の判定件数は `TallyByUnit["ere"].R382Songs` と全戦一致。
- `ErePortraitCheck` は実際の台本から取り出した歌唱を本番の `EnterScripted` で再生し、Sprite3DとシェーダーのA→C→A、停止保持・速度変更・歌唱中の再戦・死亡・蘇生・勝利・敵側反転を確認。
- `ERE_PORTRAIT_CHECK_OK actual-song fallback idle singing pause speed replay death revive victory enemy`、終了コード0。終了時には既存と同種のRID/ObjectDB解放警告あり。
- 実画面: battle-idle-a-game-check.png / battle-song-c-game-check.png。接地・体格・楽器の持ち方・歌唱差分を目視確認。

```powershell
dotnet build DemoApp/DemoApp.csproj -v minimal
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://ErePortraitCheck.tscn --audio-driver Dummy -- --capture
```

## 初回適用の確認（2026-10-23）

- `dotnet build DemoApp/DemoApp.csproj -v minimal` 成功、エラー0。既存のBattleCoreのCS0162・CS8602警告が各1件。
- `capture-integration.gd` で画像寸法・四隅の透過・採用原本とのSHA-256一致を確認。
- Vulkan Forward+の実際の編成画面・戦闘画面を撮影し、正面絵と右向き絵の切り替え、背景透過、顔と楽器の輪郭、体格と接地を目視確認。
- `ERE_ART_CAPTURE result=0`、終了コード0。終了時のみ既存と同種のRID/ObjectDB解放警告あり。
- 確認画像: selection-game-check.png / battle-game-check.png。

再撮影:

```powershell
dotnet build DemoApp/DemoApp.csproj -v minimal
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/ere/capture-integration.gd --audio-driver Dummy
```
