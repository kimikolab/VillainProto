# ガレの待機モーション v3 — 袖の揺らぎと弾みの再調整

2026-10-20。v2右側のA80で髪・袖付近に描画の揺らぎが見えるとの指摘を受けた修正版。
直前のゲーム適用指示をいったん保留して比較プレビューを制作した後、ユーザーが今回の修正版を採用。**ゲーム適用済み。**

## 方法

`design/IDLE_LOOP_COMFYUI_GUIDE.md` に従い、既存ComfyUIノード `VPScaleIdleMotion` を使用。
好評だったv1 Aのseed 2022・同じ周期[48,78)・crossfade 6・30コマ・16fpsを保持し、振幅を0.8から0.7へ変更。
空間平滑化を2から6へ増やし、`elastic_constraints` で全身の変形場を滑らかにつなぐ。
両袖と砲筒はそれぞれ元の動きから移動・回転を推定し、描かれた形を保持する。
袖・髪を静止レイヤーで貼り付けず、髪も周囲と連動して動く。RGBとアルファは同じ写像で一度だけ変形する。

平滑化で足元が938〜939行になったため、透明キャンバス上で全コマを一律1px下へ移動。
コマごとの位置合わせや部位の停止ではない。元絵940行との差を1px以内に収めた。
購入済み素材の適否はv1の記録を引き継ぐ。キャラクター固有の輪郭の修正に汎用エフェクト素材は使用しない。

## 出力・再現

- 外部タグ: `gare_A70_coherent_aligned`。
- ComfyUI prompt ID: `52ba2f24-bd61-43c9-abe2-0996114ef094`。
- `workflow.json` / `job.json`: 実際のAPIワークフローと投入結果。
- `prepare.py`: 元ワークフローを引き継ぎ、袖・砲筒の制約マスクを作成して投入。既存出力の上書きを拒否する。
- `gare-v3-*-mask.png`: 元絵に対する640×960の制約領域。
- `frames/`: 640×960 RGBAの30コマ。
- `loop.webp`: 透過・可逆圧縮の修正版。
- `A80-vs-A70.gif`: 左が前回A80、右が今回A70。共通パレット・ディザなし。
- `sleeve-detail.gif`: 同じ左右配置で髪と袖の境界を拡大。
- `check.jpg` / `compare.gif`: 標準の確認シート・元絵との比較。
- `sleeve-contact.jpg`: 6時刻の拡大比較。
- `preview.py` / `metrics.json`: 比較出力と測定。

## 確認

| 指標 | A80 | 今回A70 |
|---|---:|---:|
| コマ数・周期 | 30・1.875秒 | 30・1.875秒 |
| 頭頂の上下 | 21px | 18px |
| 靴底の行（元絵940） | 939 | 939〜940 |
| 頭と腰の横ずれ幅 | 14.5px | 7.1px |
| 継ぎ目 / 通常コマ差 | 1.41x | 1.45x |
| 緑の残り | 0px | 0px |
| 袖の非剛体変形の追跡残差中央値 | 1.266px | 0.430px |

追跡残差は元絵の左袖の特徴点を追い、移動・回転のみを取り除いた後の残差。
拡大縮小は取り除かず、比較は同じ領域・同じ追跡条件。自然さの最終評価の代わりにはしない。
全30コマでalpha 128以上の領域が画像端に接触していない。
拡大比較の6時刻で袖のしわ・肩・髪との重なりと、標準シートの全身・足元・砲筒を目視確認。
プレビュー制作時点ではゲームの静止画・待機連番・再生コードは変更していない。採用後の配置は次節。

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 tools/comfy_idle_loop/idle_loop.py check gare_A70_coherent_aligned gare
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/gare/idle-loop-v3/preview.py
```

## 採用とゲーム反映（2026-10-20）

ユーザーの「良い感じ。今回の修正版をゲームに適用してください」を受け、v3のA70を採用。
`idle_loop.py install gare_A70_coherent_aligned gare` で `DemoApp/assets/portraits/battle/idle_loop/gare/` に30コマを配置した。
16fps・1.875秒周期・640×960 RGBA、配置容量19,247,946 bytes。
`verify-install.py` でユーザーが確認した `frames/` と配置後の全画素が一致することを確認。
各コマの寸法・SHA256・容量・一致結果は `installed-manifest.json`。

本番の再生コード・静止画・高さ・戦闘ルールは変更せず、既存の自動読み込みを利用。
`IdleLoopCheck.cs` にガレの30コマ・開戦時の再生・表示高・シェーダー同期・時間経過・倍速・燃焼・死亡停止・蘇生再開・敵側反転・勝利絵の保持を追加した。
`prompts.json` の採用情報と制作手順の現状一覧も更新。

### 検証

- `dotnet build DemoApp/DemoApp.csproj -c Debug`: 成功、エラー0。既存の `BorgAttackClipCheck.cs:47` にCS8602警告1件。
- `IdleLoopCheck.tscn -- --verify`: `IDLE_LOOP_CHECK_OK`、ガレを含め成功、終了コード0。終了時にRID/ObjectDB解放警告あり。
- `PortraitStateCheck.tscn -- --verify`: `PORTRAIT_STATE_CHECK_OK`、終了コード0。
- `MovementPortraitCheck.tscn -- --verify`: `MOVEMENT_PORTRAIT_CHECK_OK`、終了コード0。
- `FireCheck.tscn -- --replay`: 2回とも2周の `FIRE_REPLAY_OK` と `FIRE_CHECK_OK` に到達。ただし判定後のGodot/C#終了処理で `csharp_lang->script_bindings` のFATALと大量の未解放参照が出て、終了コード-1073741795。通常終了の成功とは扱わず、本タスクでは終了処理を変更していない。
- `capture-game.gd`: Vulkan Forward+の実ゲームでガレ・ヒビ・ガルド・ワタ・ネルを配置し、`game/battle-00.png`〜`03.png` を撮影。`GARE_IDLE_V3_GAME_CAPTURE_OK`、終了コード0。全身・表示サイズ・接地・透過を目視確認。終了時に少量のRID/ObjectDB解放警告あり。
- `verify-install.py`: `GARE_IDLE_V3_INSTALL_OK`、30コマ全画素一致、靴底939〜940行。
- `git diff --check`: 今回触った追跡対象ファイルの差分に空白エラーなし。

検証ログは `output/gare-idle-loop-v3/`。FireCheckの初回・単独再試行ログも分けて保存。
