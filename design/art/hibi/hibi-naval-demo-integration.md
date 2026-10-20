# ヒビ 元海軍版のゲーム適用

ユーザーの「ゲームへの立ち絵の適用と、戦闘中立ち絵のモーション作成もおねがい」を受け、静止画像を先に適用した。
モーションv1のA/Bは元絵の歪みを指摘され、ともに不採用。2026-10-21に `idle-loop-v2/candidate-natural/` の変形処理を外したリテイクをユーザーが承認し、30コマ・16fpsの待機モーションも適用した。

## 採用画像

| 用途 | 原本 | ゲーム内ファイル |
|---|---|---|
| 編成・待機画面 | `hibi-naval-bodyguard-front-v1.png` | `DemoApp/assets/portraits/hibi.png` |
| 右向き戦闘待機 | `hibi-naval-bodyguard-idle-right-v4.png` | `DemoApp/assets/portraits/battle/hibi_idle_right.png` |

両方1024×1536のRGBA透過PNG。生成アルファや画素を変更せずコピーし、原本と配置先のSHA256一致を確認した。
旧騎士版の原本 `hibi-front-v1.png` / `hibi-idle-right-v1.png` と試作の戦闘v1〜v3は保持。
名前・性能・特性・戦闘ルールには変更なし。ガレ・ワタなど他キャラクターの画像は変更していない。

## 表示と接地

- `UiKit.PortraitWorldHeight("hibi")`: 2.65から3.15へ。広めの透明余白を含むv4でも、前景の表示高をガレと近い約2.49に維持。
- `UiKit.BattlePortraitBottomPaddingRatio("hibi")`: 40/1536から146/1536へ。前後に開いた脚の遠近感は画像のまま残し、最下端の靴底を地面に合わせる。
- `UiKit.PortraitHudHeight`: ヒビだけ前景高1212/1536を使ってHP・席表示の基準を補正。大きな透明余白のためにHP表示が隣の駒へ浮き上がるのを防ぐ。他の駒は従来の基準高を維持。
- 本番側の変更は既存の表示設定とHUD基準の接続のみ。待機連番の再生ロジックや台本は変更していない。

## 確認

- `verify-naval-integration.py`: 原本とのSHA256一致、RGBA、寸法、透過、画像端接触なしを検証。結果は `naval-installed-manifest.json`。
- `PortraitStateCheck.cs`: ヒビの正面と戦闘絵の分離、寸法、表示高、足元・HUDの基準、敵側反転、勝利時の正面絵への切り替えを追加。
- `dotnet build DemoApp/DemoApp.csproj -c Debug`: 成功、エラー0。最終ビルドでは既存 `BorgAttackClipCheck.cs:47` のCS8602警告1件。
- `PortraitStateCheck`: `PORTRAIT_STATE_CHECK_OK`（hibiを含む）、終了コード0。
- `IdleLoopCheck`: `IDLE_LOOP_CHECK_OK`、終了コード0。
- `MovementPortraitCheck`: `MOVEMENT_PORTRAIT_CHECK_OK`、終了コード0。
- `FireCheck`: `FIRE_CHECK_OK` に到達するが、判定後のGodot/C#終了処理でFATALと未解放参照が出て終了コード-1073741795。通常終了の成功とは扱わない。ガレ適用時にも確認した終了処理の問題で、今回は変更していない。
- `capture-naval-integration.gd`: Vulkan Forward+で実際の編成・戦闘画面を撮影。`HIBI_NAVAL_GAME_CAPTURE_OK result=0`、終了コード0。終了時にRID/ObjectDB解放警告あり。
- `naval-integration-game/selection.png` / `battle.png`: 正面絵、盾を構えた戦闘絵、ガレとの並び、体格・接地・透明背景・HP表示位置を目視確認。

ログは `output/hibi-naval-integration/`。購入済み草原背景と演出は従来どおり使用し、原本は変更していない。

## 待機モーション v2 の適用

`DemoApp/assets/portraits/battle/idle_loop/hibi/hibi_idle_00.png` から `hibi_idle_29.png` を配置。
承認された `hibi_s2022` の元動画コマを直接使用し、元絵の変形・振幅縮小・固定画像の上貼りはしない。
`verify-naval-integration.py` は静止画像と30コマの画素一致・接地・寸法を検証し、`naval-installed-manifest.json` に配置先のSHA256を記録する。

`IdleLoopCheck` にヒビの30コマ・寸法・開戦再生・同じ表示高・シェーダー同期・全周期・倍速・燃焼・死亡停止・蘇生・敵側反転・勝利絵保持を追加した。
ビルド、IdleLoopCheck、PortraitStateCheck、MovementPortraitCheckは成功。FireCheckはFIRE_CHECK_OKに到達した後に既知のGodot/C#終了時FATALが再発し、終了コード-1073741795。今回この既存問題は変更していない。
実戦画面でも採用PNGの画素指紋と一致するヒビの連番が進むことを確認した。詳細は `idle-loop-v2/notes.md`、ログは `output/hibi-idle-v2-install/`。
