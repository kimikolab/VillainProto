# ハギ 死神少女の立ち絵をゲームへ適用

適用日: 2026-10-25
ユーザーの選択: 配色をいじる前の、新規描き起こし版（黒・くすんだ赤・グレー）。

追記: 横向きの大鎌の穴数を修正。現在の戦闘絵は ../scythe-unify-v1/integration.md を参照（以下は初回適用時の記録）。

## 採用画像

- 通常: ../fresh-redraw-v1/hagi-standing-fresh-v1.png → DemoApp/assets/portraits/hagi.png
- 戦闘: ../fresh-redraw-v1/hagi-battle-idle-right-fresh-v2.png → DemoApp/assets/portraits/battle/hagi_idle_right.png

元画像と適用画像のSHA-256はそれぞれ一致（manifest.json）。画像はどちらも1024×1536の透過RGBA PNG。色や絵そのものは加工していない。

通常表示・ロスター・編成には通常絵、戦闘中には右向き絵、勝利時には通常絵を使う既存の読み込みを利用。ハギには優先されるready差分、燃焼差分、待機連番が無いため、古い絵への差し戻しは無い。

## サイズと接地

DemoApp/UiKit.cs のハギの設定だけを変更。

- PortraitWorldHeight: 2.55 → 2.25。大鎌の上端を含むキャンバスで、本人はリィカと近い体格になるように表示。
- BattlePortraitBottomPaddingRatio: 0.0534 → 30 / 1536。アルファ128以上の最終足元はy=1505（下端まで30px）。

UiKit.cs の既存の他の変更は保持。BattleCoreや戦闘ルール、数値、編成は変更していない。

## 旧素材

originals/ に退避。

- hagi-before-reaper.png: 旧通常絵
- hagi-idle-right-before-reaper.png: 旧戦闘絵
- UiKit-before-integration.cs: 適用直前の表示設定（他の未コミット変更を含むため、戻す場合はハギの2設定だけを対象にする）

## 確認

- dotnet build DemoApp/DemoApp.csproj --no-restore: 成功、エラー0。既存のBattleCore警告CS0162・CS8602の2件。
- capture-integration.gd で実際のMain.tscnを起動。編成画面・戦闘画面・勝利絵を撮影し目視確認。
- 実際のTextureRect / Sprite3Dが採用画像を参照し、戦闘シェーダーのテクスチャも一致。
- 燃焼の設定・解除後も新戦闘絵を維持。勝利時は新通常絵へ切り替わる。
- 表示高2.25、足元y=0.04999991（地面基準0.05）を確認。
- HAGI_REAPER_GAME_CAPTURE_OK selection battle burning victory source_hash height=2.25 foot=0.04999991098884

### 通し再生

- 試遊・ハギ 台B、第5波相当の起動指定stage=4、seed=0: events=153、2ターン、勝利、DEMO_SMOKE_COMPLETE。hagi-smoke.log。
- 試遊・死神と墓守、同じstage=4、seed=0: events=261、4ターン、勝利、DEMO_SMOKE_COMPLETE。battle-smoke.log。
- 後者では今回触っていないBattlePawn3D.RicaMotion.csのSetRicaMotionClockから、終了済みTweenへのPlay呼び出しエラーが27回出た。戦闘は完走。ハギ画像差し替えとは別のリィカのモーション処理として記録し、ここでは変更していない。
- Godot終了時に既存のFontAdvanced / CanvasItem / ObjectDBの解放警告が出る。

## 実画面

- selection-game-check.png
- battle-game-check.png
- victory-game-check.png

撮影コマンド:

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --windowed --resolution 1440x900 --script ../design/art/hagi/integration-v1/capture-integration.gd --audio-driver Dummy
```
