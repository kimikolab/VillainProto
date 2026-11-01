# チリの待機モーション候補

2026-11-01。ユーザーの「チリの待機モーションを作成してください」に対応。
`design/IDLE_LOOP_COMFYUI_GUIDE.md`に従い、Wan2.2 I2V 14Bで2候補を作る。

元絵は`DemoApp/assets/portraits/battle/chiri_idle_right.png`。
かぼちゃショートパンツ、オレンジとダーク系の膝上ソックス、低いツインテール、
破れたマント、マフラー、爆弾入りのかばんを維持する。
両足を接地し、膝・腰・胴体が連動して弾む待機。髪・マフラー・かばんが遅れて揺れる。
爆弾は持ったままで着火・投擲・設置せず、爆発や火花を生成へ焼き込まない。

## 再現

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py gen chiri --seeds 2022,5055 --prefix chiri_idle_v1
```

`generation.json`に元絵のSHA-256、正負のプロンプト、投入IDを記録。
投入したAPIワークフローは`chiri_idle_v1_s2022_raw-workflow.json`と`chiri_idle_v1_s5055_raw-workflow.json`。
モデルは既存のhigh/low fp8、20steps、CFG3.5、shift8、640×960、81コマ、16fps。
ローカルComfyUIを使用し、モデル・カスタムノードの更新は行わない。

## 候補と採用

生成2本が完了。接地・透過・継ぎ目を測定し、各周期の4コマを暗色背景と市松で確認した。
現行の顔・かぼちゃショートパンツ・暗色とオレンジのソックス・手の爆弾を保っている。
頭の上下は小さいが、腕・腰・マフラー・かばんに動きがあるため、静止と判定して落とさない。

| 候補 | seed | コマ・秒数 | 靴底の行（元絵938） | 頭頂の上下 | 頭と腰の横ずれの幅 | 継ぎ目／1歩 | 緑・端の欠け |
|---|---:|---|---|---:|---:|---:|---|
| A | 2022 | 20コマ・1.25秒 | 937〜938 | 3px | 6.7px | 1.27x | 0px |
| B | 5055 | 33コマ・2.0625秒 | 937〜938 | 2px | 19.8px | 1.38x | 0px |

両候補とも手順書の切り直し基準1.5x未満のため、クロスフェードで輪郭を二重にせず元のコマを直接使う。
Aは短く小気味よい周期、Bはかばんとマフラーの揺れが大きく、周期が長い。
チリの軽快さにはAを推奨するが、採用はユーザーが比較GIFで決める。

`candidates/A`と`candidates/B`に透過PNGの連番・ループWebP・比較GIF・検品シート・生成元動画を保存。
`candidates.json`にタグ・寸法・接地・容量を記録した。
両候補とも640×960・16fps。透過PNGはAが約18.7MB、Bが約31.9MB。
`comparison.mp4`は元絵・A・Bを同倍率で並べた6秒の映像。
2候補の周期の最小公倍数は660コマと長いため、無限比較GIFは各候補の元の周期のまま別々に提示する。

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py check chiri_idle_v1_s2022 chiri
& 'D:/comfyui/venv/Scripts/python.exe' tools/comfy_idle_loop/idle_loop.py check chiri_idle_v1_s5055 chiri
& 'D:/comfyui/venv/Scripts/python.exe' design/art/chiri/idle-loop-v1/package_candidates.py chiri_idle_v1_s2022 chiri_idle_v1_s5055
```

生成履歴は`*-history.json`。実行時間はAが約15分、Bが約12分。
PNG全コマのRGBA・寸法・接地・緑・枠への接触と元絵のハッシュを検査し、比較動画を書き出した。
プロンプトJSONの読み込み、制作スクリプト実行、差分の空白検査も通過。
購入素材にチリの全身待機はないため、採用立ち絵から生成した。戦闘ロジックやC#は変更していない。

候補作成時点では本編へ未導入。下記のユーザー選択を受け、Aを導入した。

## Aのゲーム適用（2026-11-01）

ユーザーの「Aで」を受け、`idle_loop.py install chiri_idle_v1_s2022 chiri`で
`DemoApp/assets/portraits/battle/idle_loop/chiri/chiri_idle_00.png`〜`chiri_idle_19.png`へ導入。
640×960・20コマ・16fps・1.25秒、約17.2MB。最適化したPNGの全画素が採用Aと一致する。
既存の読み込みと再生を使用し、連番の再生機構は増やしていない。

既存の停止時計は一部の駒だけが待機連番へ接続していたため、チリも同じ方式で接続した。
`BattlePawn3D.ChiriIdlePaused`を`SetDeathClock`から更新し、既存の`ProcessIdleLoop`で停止する。
同じ窓口で`AnimationSpeed`も更新し、通常プレイ／比較再生の両方で速度へ追従する。
設置／手を離す差分は待機連番で上書きせず、差分から戻るとAを再開。死亡と勝利では停止し、蘇生で再開する。

確認:

- DemoAppビルド成功、警告0・エラー0。
- `IDLE_LOOP_CHECK_OK`。チリの開戦・全20コマ・高さ・接地・シェーダー・反転・停止／倍速・
  設置2差分と復帰・死亡／蘇生・勝利を追加確認。
- `PORTRAIT_STATE_CHECK_OK`、`FIRE_CHECK_OK`、`MOVEMENT_PORTRAIT_CHECK_OK`。
- 本番経路の3台本を再戦込みで確認し、`CHIRI_CHECK_COMPLETE ok=True`。
  既存の設置・導火線・爆発・転送・不発・画面揺れの件数と最終HP・台本も一致。
- 窓ありの草原画面で待機Aと設置差分を撮影。固定台本の動画は`output/chiri-idle/in-game.mp4`。
  実戦の試遊プリセット・第5波（CLIのstage=4）・seed0の冒頭12秒も`output/chiri-idle/battle-idle.mp4`に録画。
- 検査ログは`.tmp/chiri-idle-integration/`、採用ファイルの検証は`integration.json`。
- 既知のGodot終了時の資源保持メッセージは残る。BattleCoreと生成物`docs/`は変更していない。
