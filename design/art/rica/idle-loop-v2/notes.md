# リィカ 候補Bのループプレビュー修正

日付: 2026-10-24。ユーザーはBを選好し、「開始と終了がつながっていない」と指摘。
状態: 2026-10-24、ユーザーがBの正しい周期のプレビューを承認し、ゲーム適用を依頼。採用Bをゲームへ適用済み。

## 原因と修正

初版の3列比較GIFは固定64コマで打ち切っていた。Bの固有周期は39コマなので、GIF自身の境界でBの24コマ目から0コマ目へ飛んでしまう。初版GIFのB列を実際に読み戻した継ぎ目は通常差分の3.99倍。初版で「継ぎ目1.02x」と記録した値はB本体の39コマ周期を測ったもので、ユーザーに提示した64コマGIFを測れていなかった。

今回はBの39コマ全体を1周期として保存し、末尾38→先頭0の境界で繰り返す。GIFの10ms刻みに合わせた60/70msの配分で合計2440ms（元の39/16秒との差2.5ms）。共通パレット、ディザなし。生成連番の画素・順番・構えを変更せず、往復再生、末尾の複製、補間、固定レイヤーの上貼りも使わない。

`B-loop.gif` を読み戻し、39コマ、実際の周期2440ms、継ぎ目差分0.904xを確認した。`seam-contact.jpg` は36/37/38/0/1/2コマの境界確認。元のPNGは `../idle-loop-v1/B/frames/` を使用し、指紋を `metrics.json` に保存。

`../idle-loop-v1/package_previews.py` も修正。比較GIFの長さには候補周期の最小公倍数を使用し、長すぎる場合は個別プレビューを案内する。固定64コマで周期途中を打ち切る方式は廃止した。初版の `../idle-loop-v1/comparison.gif` は不具合のある旧資料であり、採用確認に使わない。

## 試したが採用しなかった切り直し

既存のComfyUI `recut rica_s5055 --crossfade 6` も試した。正常終了したが、動画読み戻しで区間が[26,65)に変わり、混ぜた後の継ぎ目が1.51xとなった。元のBより良くないため採用せず、PNGやGIFには使っていない。実行履歴は `discarded-recut-result.json`。

## 再現と成果物

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/rica/idle-loop-v2/preview.py
```

- `B-loop.gif`: B単体、420×630、固有周期どおり。
- `still-vs-B.gif`: 左が静止・右がB、640×480、同じ39コマ周期。
- `seam-contact.jpg`: 境界の前後6コマ。
- `metrics.json`: GIF読み戻しの測定、元PNGのSHA-256。

## ゲームへの適用

`idle_loop.py install rica_s5055 rica` で `DemoApp/assets/portraits/battle/idle_loop/rica/rica_idle_00.png`〜`rica_idle_38.png` に配置。39コマ、16fps、周期2.4375秒、640×960 RGBA、約30.8MB。PNG圧縮の最適化は行うが、採用Bの全39コマと復号後の画素が完全一致することを確認（`install-audit.json`）。

静止絵、相対表示高2.10、足元余白40/1536、戦闘ルールと数値は変更していない。既存の `BattlePawn3D.IdleLoop` が自動読込する。ランタイム側のコード変更はなく、`IdleLoopCheck.cs` にリィカの39コマ・寸法・開戦時再生・同じ表示高・周期跨ぎ・倍速・燃焼・敵側反転・死亡/蘇生・勝利表示の確認を追加した。

## 適用後の検証

`dotnet build DemoApp/DemoApp.csproj -c Debug --no-restore` 成功、警告0・エラー0。

| シーン | 結果 |
|---|---|
| `IdleLoopCheck.tscn --verify` | `IDLE_LOOP_CHECK_OK`（ricaを含む） |
| `PortraitStateCheck.tscn --verify` | `PORTRAIT_STATE_CHECK_OK` |
| `FireCheck.tscn --replay` | 2回の台本再生と `FIRE_CHECK_OK` |
| `MovementPortraitCheck.tscn --verify` | `MOVEMENT_PORTRAIT_CHECK_OK` |

全シーン終了コード0。ログは同フォルダーの `idle-loop.log` / `portrait-state.log` / `fire-replay.log` / `movement-portrait.log`。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/rica/capture-integration.gd --audio-driver Dummy -- --small-roster --idle-loop
```

Vulkan Forward+の実戦画面でミオ・シオ・ササ・トウと並べ、全身・接地・表示高を確認。戦闘停止中にも待機連番が進む既存の挙動を確認し、`RICA_IDLE_GAME_CAPTURE_OK height=2.10 paused_idle_advance` と `RICA_REMAKE_GAME_CAPTURE_OK result=0` を出力。`selection-game-check.png` / `battle-game-check.png` を保存。終了時のFontAdvanced・CanvasItem・ObjectDB等の解放警告は既存と同様。
