# 逆しまのウツ 双剣の戦闘待機モーション v1

2026-10-27、ユーザー依頼。ゲーム採用済みの `DemoApp/assets/portraits/battle/utsu_idle_right.png` を原本とする。同日、ユーザーがAを選択しゲーム反映を依頼。Aを導入・検証した。
`design/IDLE_LOOP_COMFYUI_GUIDE.md` に従い `tools/comfy_idle_loop/idle_loop.py` の gen / check / 必要なら recut で作る。

- Wan2.2 I2V 14B high/low fp8、640×960、81コマ、16fps、20steps、CFG3.5、shift8。seed 2022／5055。
- 両足を固定し、膝の屈伸・腰・胴・肩・頭・両腕が連動する。前の剣を右上、後ろの剣を左下に構え、握りと刃の直線・長さを保つ。実際の斬撃は含めない。
- 髪・短い外套・腰布は身体より少し遅れて揺れる。光・煙・斬撃軌跡は焼き込まない。
- 購入素材の一覧とローカルのNatureBlocks／EffectBlocksを再確認。背景・汎用エフェクトで今回の人物全身連番には該当しないため、承認済み立ち絵から生成する。
- 生成条件・実行IDは `*-workflow.json` と `jobs.json`、終了履歴は `*-history.json` に保存する。
- 比較GIFを確認したユーザーがAを選択。Aを通常プレイへ導入し、Bも比較用として保存。

```powershell
$env:PYTHONUTF8='1'
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py gen utsu --seeds 2022,5055 --prefix utsu_idle_v1
```

## 比較候補と検品結果

両候補とも自動抽出の初版は動きが小さく継ぎ目が目立ったため、元動画から24〜48コマの連続区間を選び直し、末尾6コマを混ぜた。Aは24コマ、Bは37コマ。足・頭・腕を別々の固定レイヤーで止める処理や、全身の変形による振幅縮小は使っていない。動きの控えめさは保持し、ユーザーが比較GIFで自然さを判断できるようにした。

| 候補 | タグ | コマ／秒（16fps） | 足元 | 頭頂の上下 | 頭と腰の横ずれ幅 | 継ぎ目／通常のコマ差 | 緑の残り |
|---|---|---|---|---|---|---|---|
| A | `utsu_idle_v1_s2022_final` | 24／1.50 | 926固定 | 11px | 11.9px | 0.84x | 0px |
| B | `utsu_idle_v1_s5055_final` | 37／2.31 | 926固定 | 9px | 14.6px | 0.92x | 0px |

両候補とも原画縮小後の靴底926行と全コマで一致。640×960のRGBA、全コマの四辺にアルファ128超の画素は0で、刃先と全身のはみ出しなし。検品シートで双剣の構え・握り・顔・衣装・足元を目視確認。6コマの継ぎ目補修では薄い残像が出る可能性があるため、比較GIFで確認する。

生成・切り直しはいずれも成功。候補は `candidates/A/` と `candidates/B/` に透過連番・比較GIF・確認シート・透過WebPを保存した。左が原画、右がモーション。Aは小刻み、Bは長めの周期で外套が大きく揺れる。
各候補の保存先・容量・フレーム数・元絵のSHA-256は `candidates.json`。透過連番はAが約14.6MB、Bが約22.3MB。

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py recut utsu_idle_v1_s2022 --crossfade 6 --min-length 24 --max-length 48
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py recut utsu_idle_v1_s5055 --crossfade 6 --min-length 24 --max-length 48
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py check utsu_idle_v1_s2022_final utsu
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py check utsu_idle_v1_s5055_final utsu
& 'D:/comfyui/venv/Scripts/python.exe' design/art/utsu/idle-loop-v1/package-candidates.py
```

## Aのゲーム適用（2026-10-27）

`idle_loop.py install utsu_idle_v1_s2022_final utsu` で `DemoApp/assets/portraits/battle/idle_loop/utsu/utsu_idle_00.png`〜`utsu_idle_23.png` に導入。640×960のRGBA、24コマ・16fps・1.50秒、約14.5MB。`verify-install.py` で全コマが保存済み候補Aと画素単位で一致することを確認した（`UTSU_IDLE_INSTALL_OK`）。採用情報は `candidates.json`、`integration.json`、`prompts.json` の adopted に記録。

戦闘用原画、表示高2.50、接地補正52/1536は採用済みのまま。ゲームの再生処理・戦闘ルール・数値・生成docsには今回の変更なし。`IdleLoopCheck.cs` にはウツの24コマ・開戦時の連番・全周期・表示高と接地・倍速・燃焼・死亡・蘇生・勝利・敵側反転の確認を追加した。

### 検証

- `dotnet build DemoApp/DemoApp.csproj --no-restore` 成功（警告0・エラー0）。
- 所定の4チェックで `IDLE_LOOP_CHECK_OK`（utsuを含む）、`PORTRAIT_STATE_CHECK_OK`、`FIRE_CHECK_OK`、`MOVEMENT_PORTRAIT_CHECK_OK` を確認。
- `capture-game.gd` で本番MainをVulkan Forward+で描画。実際のSprite3Dが採用Aの画素と一致し、連番が進み、右向き・表示高2.50・足元0.05であることを確認。`UTSU_IDLE_GAME_CAPTURE_OK frames=24 height=2.50 ground=0.05 advance=true`、終了コード0。
- 撮影検査の接地式は、既存の呼吸によるSpriteのscale.yを含めて測定するよう修正した。ゲームの呼吸・接地処理は変更していない。
- `battle-idle-a.png` と `battle-idle-c.png` を目視し、双剣の構え・全身・透過・周囲の駒とのバランスを確認。
- ウツ入り編成 `逆しま (ネル×ウツ)`、第1波、seed7を頭なしで自動再生。`DEMO_SMOKE_COMPLETE events=98 turns=3 won=True attackPlays=16 maxRun=5 maxRunBy=逆しまのウツ`、終了コード0。5連撃を含む1戦を完走。
- Godot終了時のFontAdvanced・CanvasItem・ObjectDB・リソース解放の警告は残る。成功マーカーと終了コードで確認し、画像読み込み・待機再生・戦闘完走の失敗なし。

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py install utsu_idle_v1_s2022_final utsu
& 'D:/comfyui/venv/Scripts/python.exe' design/art/utsu/idle-loop-v1/verify-install.py
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --script ../design/art/utsu/idle-loop-v1/capture-game.gd --audio-driver Dummy
```
