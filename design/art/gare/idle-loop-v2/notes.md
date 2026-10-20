# ガレの待機モーション v2 — Aの弾みを少し抑える

2026-10-20。ユーザーの「Aのほうが好みだけど、もう少しだけ弾みは少なくても良いかな」を受けた修正。
v1のA（seed 2022・30コマ）と同じ周期を使用し、全身の運動を振幅0.8に調整した。

**状態: 修正版の比較プレビューまで完成。最終確認待ち・ゲーム未適用。**

追記: ユーザーが髪・袖付近の揺らぎを指摘し、もう一段弾みを抑えるよう指定。ゲーム適用を保留し、`../idle-loop-v3/` に修正版を制作した。

## 方法

制作手順 `design/IDLE_LOOP_COMFYUI_GUIDE.md` の「全身の振れ幅を抑える」に従い、ComfyUIの既存ノード `VPScaleIdleMotion` を使用。
元のseed 2022動画から同じ区間[48,78)・末尾crossfade 6で運動を取り出し、振幅0.8・30コマ・周期3次・空間平滑化2.0で全身の動きを縮小。
現在のゲーム立ち絵のRGBAを一度だけ縮小し、そのRGBとアルファを同じ運動場で変形する。顔・腕などの固定レイヤーの貼り付け、画像同士の半透明合成は使用しない。
生成し直した動画ではなく、ユーザーが好んだAの運動を用いた。周期は約1.9秒のまま。

APIワークフロー: `gare-A80-workflow.json`。
ComfyUI prompt ID: `cc156a4b-3cf8-4e5d-bf10-18d8a9bc8357`、success。
外部出力タグ: `gare_A80`（`D:/comfyui/output/VillainProto/idle_loop/`）。

## 保存物

- `A80-frames/`: 640×960の透過PNG30コマ、20,218,130 bytes。16fps。
- `A80-loop.webp`: 修正版の透過ループ。
- `A80_compare.gif`: 左が元の静止立ち絵、右が修正版。
- `A80_check.jpg`: 暗背景・市松の4姿勢。
- `A-vs-A80.gif`: 左がv1のA、右が弾みを抑えた修正版。両方30コマを同じ時刻で比較。
- `preview.py`: 比較GIFの再作成。共通パレット・ディザなし。

## 確認

| 指標 | 元のA | 修正版A80 |
|---|---:|---:|
| コマ数 | 30 | 30 |
| 周期 | 1.875秒 | 1.875秒 |
| 頭頂の上下 | 26px | 21px |
| 頭と腰の横ずれ幅 | 18.4px | 14.5px |
| 足元（元絵940） | 939〜940 | 939固定 |
| 継ぎ目 / 通常コマ差 | 1.13x | 1.41x |
| 緑の残り | 0px | 0px |

全30コマでalpha 128以上の領域が画像端へ接触していない。確認シートで全身の構え・顔・砲筒・髪・コートの輪郭と透過を確認。
比較GIFが640×480・30コマであることを確認。ゲーム使用中の静止画や表示設定は変更せず、待機連番の配置先もまだ作成していない。

再確認:

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 tools/comfy_idle_loop/idle_loop.py check gare_A80 gare
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/gare/idle-loop-v2/preview.py
```

最終採用の指示後に `idle_loop.py install gare_A80 gare` で配置し、制作手順のゲーム内確認と `prompts.json` の `adopted` 更新を行う。
