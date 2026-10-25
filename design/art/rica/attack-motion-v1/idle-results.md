# 覚醒待機の検品・仮適用

2026-10-24。攻撃7種と同時に、覚醒3段の待機動画をComfyUIで追加した。
既存の通常リィカ39コマは、ユーザーが選んだ採用版をそのまま維持する。
以下の3本は、動作検品を通してゲームとFxViewerへ**仮適用**した候補。
最終の好みについてユーザーの選択はまだ受けていない。
その後の制作範囲の回答「新しい待機ループまで仕上げる」を反映し、待機3段の生成・検品・接続と
オーラ付きの実描画動画まで仕上げた。この回答を候補そのものの見た目の承認とは扱わない。

| 層 | 仮適用タグ | コマ / 16fps | 頭頂の上下 | 靴底（元絵→動画） | 継ぎ目比 | 緑残り |
|---|---|---:|---:|---|---:|---:|
| 3–5 | rica_stage1_s5055_final | 62 / 3.88秒 | 64px | 851→850–853 | 0.87× | 0px |
| 6–8 | rica_stage2_s3033 | 25 / 1.56秒 | 43px | 856→855–856 | 0.99× | 0px |
| 9以上 | rica_stage3_s2022_final | 43 / 2.69秒 | 23px | 857→856 | 1.23× | 0px |

比較GIFは左が元絵、右が候補。元絵からどのくらい身体・服・シャベルが動くかを確認できる。

- [3–5層：低い構え](idle-review/rica_stage1_s5055_final_compare.gif)
- [6–8層：大剣の上段構え](idle-review/rica_stage2_s3033_compare.gif)
- [9層以上：覚醒段の直立構え](idle-review/rica_stage3_s2022_final_compare.gif)

身体・武器のオーラを重ねたゲーム内の動きは
[4段の同時比較](rica-four-idles-comparison.mp4)と[FxViewer各8秒の再生](rica-four-idles-ingame.mp4)。
通常を含む各段を121コマ・15fpsで撮影し、最長の62コマ/16fpsも2周以上確認できる。
実描画はVulkan Forward+。全段で同じ範囲を切り出し、人物・武器の大きさを比べられる。
その後のフィードバックを受け、9層以上の待機武器は6–8層と同じ大剣型へ変更した。
現在の武器と剣閃は、READMEの`greatsword-v3`付き動画を参照する。身体の待機連番は同じ43コマ。

## 判断と制限

3–5層はseed2022が小さく、切り直しても継ぎ目比が1.5×を超えた。
seed5055は全身の動きが出ている。crossfade6・上限64で62コマを得た。
靴底の最大差は2pxで、道具の目安1pxを一部超える。補正変形は加えず、実画面で確認できる候補として残す。

6–8層は最初のseed2022/5055がほぼ静止したため不採用。
膝と腰の上下を強めたseed3033を追加し、25コマの自然周期を使う。crossfadeは0。

9層以上はseed2022をcrossfade8で切り直した43コマ。
seed5055は頭頂3pxで動きが小さく、継ぎ目3.01×のため不採用。
直立した上体の揺れとマントの動きは残す。頭と腰の横ずれ幅は38.4pxで道具の警告値25pxを超えるため、
4コマ検品画像とゲーム実描画を確認した。髪・マントも含む輪郭指標であり、足の滑りや猫背の確定判定としては使わない。

上に掲げたシャベルを動画の端で切らないため、6–8層と9層以上の元絵は1280×1920へ透明余白を追加した。
人物自体の画素・縦横比は変えず、表示高と足元余白を同じ比率で補正した。
動画は640×960のままなので人物の画面占有面積が旧入力の0.64倍になる。
生成時の全画面平均差2.47/1.74はこの余白込みの値。元の占有面積換算は3.86/2.72であり、
実際の上下43px/23px、手・肩・腰の連動を見て静止候補と区別した。

全身動画をそのまま使い、シャベルや腕を別画像で貼り合わせない。
ループ終端のcrossfadeは待機だけに使用し、攻撃は原画セルの切り替え。
元絵・最初の動画コマ・隣接コマから目とシャベルの鋼部を追跡し、`motion.json`へ記録した。
3–5層だけは初期の鋼部先端推定がずれたため、`rica_stage1_s5055_final-anchors.json`の実コマ上の点を使う。
各段の`rica_stageN-tracking.jpg`に4時点の追跡位置を保存している。

## 再現データ

`idle-review/workflows/`はComfyUIの実行履歴から取り出したAPIグラフ。
`idle-review/jobs.json`がprompt IDと成功状態を対応付ける。
生成はWan2.2 I2V high/low noise 14B fp8、81コマ、20 steps、CFG3.5、640×960。
`tools/comfy_idle_loop/prompts.json`の`rica_stage1/2/3`にプロンプトと仮適用タグを保存。
余白追加前の静止候補は`rica_stage2_v1`の参照を残した。
`prepare_idle.py`を再実行しても検品後の`adopted`は保たれる。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://IdleLoopCheck.tscn -- --rica-idles-required
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
```

通常・覚醒全段の進行、倍速、左右反転、死亡・蘇生、勝利、静止絵へのフォールバック、
武器の全コマ追跡数を検査済み。FxViewerでは段別の「待機」を選んで実物を確認できる。
