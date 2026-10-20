# ガレ（海賊・散弾砲筒版）の戦闘待機モーション v1

2026-10-20。現行ゲームの右向き戦闘立ち絵 `DemoApp/assets/portraits/battle/gare_idle_right.png` を基準に、ComfyUIで2候補を作成。
元絵は `design/art/gare/gare-pirate-scattergun-idle-right-v1.png` とSHA-256一致。

**状態: 比較プレビューまで完成・ユーザーの選択待ち。ゲーム未適用。**
追記: ユーザーはAを好み、弾みを少し抑えるよう指定。修正版は `../idle-loop-v2/notes.md` に記録。最終採用はまだ行っていない。
`DemoApp/assets/portraits/battle/idle_loop/gare/` は作成していない。
制作手順 `design/IDLE_LOOP_COMFYUI_GUIDE.md` の「最後の1本はユーザーが動画を見て選ぶ」に従う。

## 候補

両方640×960の透過PNG連番、16fps、末尾6コマをcrossfade。比較GIFは左が元絵、右がモーション。

| 候補 | タグ | seed | コマ数・秒 | 足元の行（元絵940） | 頭頂の上下 | 頭と腰の横ずれ幅 | 継ぎ目 / 通常コマ差 | 緑の残り |
|---|---|---:|---|---|---:|---:|---:|---:|
| A・弾みが大きめ | gare_s2022_final_aligned | 2022 | 30・1.875秒 | 939〜940 | 26px | 18.4px | 1.13x | 0px |
| B・控えめ | gare_s5055_final | 5055 | 36・2.25秒 | 940 | 17px | 8.3px | 1.35x | 0px |

- A: `A_compare.gif` / `A-loop.webp` / `A_check.jpg` / `A-frames/`（30コマ・21,381,081 bytes）。
- B: `B_compare.gif` / `B-loop.webp` / `B_check.jpg` / `B-frames/`（36コマ・25,908,824 bytes）。
- 両候補とも静止ではなく、砲筒を両手で支えたまま膝・身体が連動し、髪と長いコートが追随する。生成に伴う服の細部の変化は残る。
- 全66コマのalpha 128以上の領域は画像端へ接触していない。靴底と帽子・砲口・髪・コートの欠け、透過を確認シートで目視確認。

## 生成と仕上げ

共有ComfyUIは停止していたため非表示で起動。`idle_loop.py status` で実行中0・待ち0・自作ノードありを確認して投入。
既存ノードの原本とComfyUI配置先はSHA-256一致。ノードやモデルの更新は行わない。

1. `tools/comfy_idle_loop/prompts.json` に `gare` を追加。現在の散弾砲筒版の元絵を使用し、銃の形・両手の握り・右向き・固定した足、膝の屈伸、髪とコートの追随を指定。
2. Wan2.2 I2V 14B fp8、640×960、81コマ、20 steps、CFG 3.5、shift 8、Lightningなし。seed 2022 / 5055を生成。1本目902秒、2本目600秒（後者の待ち時間は含まない）。
3. 元のAは30コマ・継ぎ目1.74x、Bは36コマ・1.87xで、`recut --crossfade 6` を両方に実施。
4. Aの再切り出しは区間[48,78)。足元が元絵より1〜2px上にあるため、全コマのRGBAを同じ写像で1px下へ移動。ComfyUI標準の透明キャンバスと `ImageCompositeMasked` を使用し、身体や部位を個別には固定していない。仕上げワークフロー `gare-s2022-aligned-workflow.json` に保存。
5. Bは区間[41,77)。再切り出し時の緑背景MP4には圧縮差があり、区間検出の1コマ差が2.49で閾値2.5をわずかに下回る警告が出た。元生成では2.58で閾値以上、透過後のコマ差の中央値は4.87、頭頂の上下17pxがあり、静止ではない。最終の自然さは比較GIFで選ぶ。
6. BiRefNet_toonoutの前景マスクと緑のクロマキーを使い、緑の残り0pxを確認。足元差は両候補とも1px以内。頭と腰の横ずれ幅は両候補とも25px以内。

## 再現資料

- `gare-s2022-workflow.json` / `gare-s5055-workflow.json`: 初回生成のプロンプトIDとAPIワークフロー。
- `gare-s2022-aligned-workflow.json`: Aの再切り出し・透過・1px接地補正。
- `gare-s5055-final-workflow.json`: Bの再切り出し・透過。
- `source-s2022.mp4` / `source-s5055.mp4`: 生の緑背景動画。
- 外部出力原本: `D:/comfyui/output/VillainProto/idle_loop/gare_s2022*` / `gare_s5055*`。
- ログ: `output/gare-idle-loop-v1/`。

チェックの再実行:

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 tools/comfy_idle_loop/idle_loop.py check gare_s2022_final_aligned gare
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 tools/comfy_idle_loop/idle_loop.py check gare_s5055_final gare
```

採用指示を受けたら、選ばれたタグを `idle_loop.py install <tag> gare` で配置し、`prompts.json` の `adopted` を更新する。
その際に `IdleLoopCheck.cs` へガレの開戦時・コマ数・高さの確認を追加し、制作手順のビルドと各チェック、実画面の確認を行う。
現時点のコード変更はプロンプト登録のみで、戦闘・再生コード・ゲーム使用中の画像は変更していない。
