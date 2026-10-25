# リィカの4段階モーション・ゲーム接続

2026-10-24。ユーザー承認済みの段階別の構えと攻撃構成を、DemoApp本編とFxViewerへ接続する。

最新の演出は[v14の三連撃・待機絵に合わせた体格と頭の寸法](../proportions-v14/README.md)。
[4段階の連続比較・30fps](rica-four-stage-flow-comparison-proportions-v14.mp4)。
二連撃の発光は[v13](../white-slash-v13/README.md)を継続する。
攻撃SEは[v15のSword Critical](../sword-audio-v15/README.md)へ、[v16の段別の命中音](../sword-hit-v16/README.md)を重ねる。
覚醒・光の吸収・落下斬りの接地は[v17の指定SE](../event-audio-v17/README.md)を使用する。
着地音は[v18で-6 dBに増量](../landing-audio-v18/README.md)した。
v5の原画を基礎に、v6で立体の回転軌跡、v7で振り抜き、v8で移動中の構え、v9で連撃の全身原画20枚と打の間の動きを追加した。
以下の過去の動画・検証数は各時点の履歴として残す。

## 攻撃の採用方式

攻撃は全身原画によるセルアニメーション。内蔵image_genの初版4枚のシート・28姿勢から27姿勢を採用した。
v4で3–5層の6姿勢を新しい回転8姿勢へ置換した。
v5では0–2層を頭上からの打ち下ろし6姿勢、3–5層を回転12姿勢へ変更した。
v7では回転の命中後に3枚を追加し、旧1枚を外して35姿勢とした。
v9では二連撃8枚・三連撃12枚を追加し、二連撃の旧構え1枚を置き換え、攻撃とつなぎに54姿勢を使う。移動構えは別に4姿勢。
v11では二連撃の振りかぶりと2撃目を新規10姿勢へ置き換え、攻撃・つなぎ57姿勢＋移動4姿勢とした。
v12では2撃目の中間1姿勢を省き、溜めから速く振り抜く保持時間へ調整した。採用は攻撃・つなぎ56姿勢＋移動4姿勢。
v14では9層以上の攻撃・つなぎ19姿勢と踏み込み1姿勢を同じ待機絵の成人体格へ描き直した。
頭頂から顎の軸長で全身セルを同率に拡縮し、待機・移動・三連撃で顔の大きさが跳ねるのを抑える。
最終段の引き戻しの1姿勢はシャベルの鋼部が消えていたため除外し、鋼部が見える構えを次の回転斬りの起点にする。
顔・腕・武器を個別に貼り付けたり、画像を半透明で混ぜたり、斬撃を逆再生したりしない。
速い振りを数コマで通し、予備動作・接触・振り切りを明確に見せる。
滑らかな動画補間を全コマ採用したものではない。髪や服まで連続的に動かす待機は、別のComfyUI動画を使う。

| 層 | 動作 | 連番・30fps | 接触（0始まり） |
|---|---|---:|---:|
| 0–2 | 頭上からのシャベル打ち・短い止め | rica_shovel / 31 | 15 |
| 3–5 | 溜め→横回転→敵の向こうまで振り抜く薙ぎ | rica_crescent / 44 | 27 |
| 6–8・1打目 | 大剣の切り上げ | rica_rise / 13 | 9 |
| 6–8・2打目 | 頭上に構え直し、斜め前へ切り下ろす | rica_return / 16 | 8 |
| 9以上・1打目 | 大槍の突き | rica_thrust / 12 | 6 |
| 9以上・2打目 | 回転斬り | rica_sweep / 13 | 4 |
| 9以上・3打目 | 高い跳躍からの落下斬り・着地 | rica_descend / 28 | 16 |

`DemoApp/assets/portraits/battle/attack_clip/rica_*/` が本番の攻撃157コマと、打の間のつなぎ27コマ（9コマ×3本）。
つなぎは既存の0.3秒の移動と並行して再生し、接触通知・攻撃数には加えない。
切り上げ・突き・回転の振り抜き後の保持を1コマに縮め、次の構えへ運ぶ原画へ続ける。
`clip.json` は接触・足元・腰の基準と倍率、`motion.json` は全コマのシャベルのソケット・先端・目・武器形状。
原画の形を変えず、セルを共通1152×896のキャンバスへ移動・等比縮小した。
人物の大きさを維持し、突きのシャベル先端と回転時のマントが切れない余白を確保する。
`cel-animation.json` がセルごとの基準点・並び・保持時間の正。
`RicaCelExport.tscn` が連結した全身を原画から分離してゲームへ書き出す。
シート内でセルが隣の列へ張り出すため、固定の等間隔矩形では切り出さない。

回転斬りと落下斬りの `vertical` は駒の席を動かさず絵だけを上下させる表示曲線。
回転斬りの途中から2.8まで上昇し、同じ空中の構えを次の落下斬りへ保持する。
最高3.18でためた後、接触16コマ目に高さ0へ着地。身体の輪郭・武器・HPも同じ高さへ追従する。

## 別レイヤーの身体・武器

- 本体PNGは物理シャベルのみ。木の柄・Dグリップ・鋼のスコップを保持する。
- `BattlePawn3D.RicaMotion.cs` が身体の輪郭・目と武器の位置を現在のコマへ合わせる。
- 三日月・大剣・大槍は `assets/fx/rica_motion_v1/weapon_aura.gdshader` の同じ形状式を3Dへ適用。
  ソケットを根元、ソケット→先端を刃の軸とし、原画の後ろへ描く。
- 身体は同フォルダーの輪郭シェーダーと既存 `DeathAura3D` の燐光・紫の焔を併用。
  位相は再生時計で進め、ShaderのTIMEは使わない。
- 武器は面の模様を除き、白熱した芯・色の縁・柔らかな外光を加算合成する。
  原画は後ろの発光へ描き重なり、物理シャベルの柄と鋼部を残す。
- `RicaWeaponTrail3D` は実際のソケットと剣先から曲面の残光を作る。
  寿命0.18秒。姿勢間の角度を分割して滑らかに結び、別の打の間を線でつながない。
- `RicaSlashFx3D` は横・上・斜め下・直線を打ち分ける接触時の剣閃。
  範囲攻撃も命中点を覆う一枚の刃にし、敵ごとの発射線を増やさない。
  白い芯、青紫の内側の余韻、細い外周線を重ね、回転斬りは交差する2本の弧にする。
  落下斬りには着地の光輪を加える。8個の火花も同じ0.52秒の時計で消す。

購入済みEffectBlocks v4の `sparkles.tscn`・`fire_magic.tscn`・`sparkle.png` を確認。
キャラの手足の動作は含まないため原画を追加し、燐光には購入の光粒画像を流用する。
武器の殻と接触剣閃は形状・接触時刻・停止を統一するため専用描画。
購入素材の原本・共有材質を変更せず、本体をGitへ追加しない。
ビーム改修では購入の `energy/laser.tscn` と `energy/electric_sparks_1.tscn` も確認。
原本の自動放出・反復は使わず、剣閃の火花に既存 `sparkle.png` を流用する。
共有の環境Glowは変更せず、専用シェーダーの発光と透明度の勾配で光をにじませる。

## 台本との接続

本編の層は既存 `DeathPresentation` の実カウンタの写しを使う。
`BattlefieldView3D.RicaMotion.cs` は実際のAttackごとに対応する一打を選び、接触コマまで待つだけ。
ダメージ・標的・攻撃回数・覚醒条件をUIで判定しない。手番外は最初の一打。

連撃の振り切りを次のAttackまで保持し、実際の次の標的へ跳んで次のモーションへ入る。
連撃の終わりに振り切りを完了してから待機・元の席へ戻る。
戦闘末尾も最終モーションを待ち、勝利絵や次のCueで切らない。
死亡・中断・非表示・再戦では古い接触待ちと武器を破棄する。

## FxViewer

`FxViewer.bat` → 駒「墓守リィカ」または検索「リィカ」→「死・リィカの覚醒」。
通常・霊刃・大剣・覚醒の攻撃と待機を直接選べる。
固定台本は表示確認専用で、実戦と同じ `Main.ApplyEvent` と攻撃再生経路を使う。
左右反転・停止・速度・繰り返し・次の見せ場・シークに対応する。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://FxViewer.tscn -- --fx-cue=rica.stage3.attack
```

## 動画補間の検品履歴

最初はWan2.2 Fun Inpaint 14Bの始点終点指定で7打と横薙ぎの別seedを生成した。
`build_workflows.py`・`jobs.json`・`*-workflow.json` にサイズ896×768・25コマ・20 steps・CFG3.5・seedを保存。
原画はComfyUIの節点で同じ足元と体格へ置く。元のPNGを加工して上書きしない。

途中で手が重なり、シャベルが消失・変形する候補があった。
横薙ぎ5055ではフレームの枠から出るコマもあり、これらの動画を完成モーションとして採用しない。
`*-sheet.jpg`・`inspect_motion.py` はその検品用。
`export_clips.py` と `selected.json` は途中の動画候補の書き出しであり、**本番へ再適用しない**。
本番の再生成は下記の `RicaCelExport.tscn` だけを使う。
`*-tracking.json` のLK推定も不採用候補のデータで、本番の追跡点には使わない。

原画の最終指示全文と内蔵画像生成の記録は `clean-cel-prompts.json`。
前段の20枚は `../motion-source-v1/` に保持している。

## 再生成と確認

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaMotionCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
# 実描画を15fps連番で撮影
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_preview.py
```

初版のゲーム内動画は `rica-four-stages-ingame.mp4`、各段の短い確認は `stage0-ingame.gif`〜`stage3-ingame.gif`。
跳躍とビームの改修後は、下記の`-beam-v2`付きの4段比較動画を参照する。
撮影の元コマとログは `output/rica-fxviewer/` と `output/rica-motion-integration/`。
終了時の既存RID・ObjectDB警告は、機能検査の `_OK` と分けて記録する。

待機3段は検品済み候補を仮適用。最終の好みについてはユーザーの選択を待つ。
コマ数・採否・ループ検査と比較GIFは [idle-results.md](idle-results.md)。
ユーザーの追加回答で、今回の制作範囲は新しい待機ループまでと確認済み。
3段の生成・検品・ゲームへの接続を完了し、
[オーラ付き待機4段の同時比較](rica-four-idles-comparison.mp4)と
[FxViewerで各8秒の待機再生](rica-four-idles-ingame.mp4)を追加した。
実描画は各121コマ・15fps。全段で同じ画面領域を切り出している。
待機のみの動画はビーム改修前の記録として保持する。現在の発光と動作は下のv2連続比較で確認できる。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-idle-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_idle_preview.py
```

## 接続後の検証記録

### シャベル打撃・回転薙ぎv4

現在の動作と追加原画は [spin-impact-v4/README.md](../spin-impact-v4/README.md)。
0–2層の命中を金属の打撃へ変更し、短い止めと「金属叩き」のSEを追加。
3–5層は、背中と左向きのコマを挟んだ全身8姿勢の回転薙ぎへ置き換えた。
現在の比較動画は[打撃SE入り4段比較v4](rica-four-stage-flow-comparison-spin-impact-v4.mp4)。
回転の背面では目の発光も消す。RicaMotionCheckは403項目、DeathAxisCheckは15台本で成功。

### 待機大剣・剣閃の拡大v3

6–8層の武器を基準として、9層以上の待機も同じ大剣型へ変更した。
9層の立ち絵では剣の長さ3.1・幅1.65。6–8層の2.9・1.25より少し幅広くする。
攻撃中の槍は突きのときだけ使い、幅を0.75から1.05へ増やした。
待機43コマの`motion.json`、静止絵へのフォールバック、追跡データの再生成も大剣型に統一する。

| 層 | 剣閃の光の幅 | 全体の大きさ | 余韻 | 火花 |
|---|---:|---:|---:|---:|
| 3–5 | 1.0 | 1.0 | 0.52秒 | 8 |
| 6–8 | 2.0 | 1.14 | 0.58秒 | 10 |
| 9以上 | 3.1 | 1.30 | 0.64秒 | 14 |

倍率はv2の剣閃を基準とする表示値。9層の回転斬りは太い弧と細い2本の弧を交差させる。
突きには短い閃光、落下斬りには広がる地面の光輪と閃光を加える。
大きいビルボードの弧が地面との交差で水平に切れないよう、剣閃だけは深度検査を外す。
光の面の外縁は透明度の勾配で消す。人物・足元の移動や台本の命中判定は変更しない。
購入済みEffectBlocksの`other/shockwave.tscn`も確認した。
原本の自動放出は使わず、既存の光輪を同じ停止・倍速時計に合わせ、火花に購入の`sparkle.png`を流用する。
剣閃は従来と同じ1打1つのコンテナで、戦闘台本の攻撃数・命中点は維持する。

現在の比較は [4段の連続再生v3](rica-four-stage-flow-comparison-greatsword-v3.mp4)と
[盤面全体v3](rica-four-stage-flow-full-board-greatsword-v3.mp4)。
RicaMotionCheckは381項目、FxViewerの8 Cue・左右反転、待機の検査が成功。
v3のビルドは成功（エラー0、BattleCoreの既存警告2）。
ログは`output/rica-motion-integration/greatsword-v3-*.log`。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix greatsword-v3
```

### 待機→攻撃→待機の4段比較

[追従拡大・4分割（跳躍・ビームv2）](rica-four-stage-flow-comparison-beam-v2.mp4)と
[盤面全体・4分割（跳躍・ビームv2）](rica-four-stage-flow-full-board-beam-v2.mp4)を追加。
ユーザーの参考画像3点の、白熱した芯・色の余韻・重なる細い軌跡を専用描画へ取り入れた。
改修前の動画は同名の`-beam-v2`のないファイルに保持する。
どちらも通常速度・10秒・150コマ/15fps。原画の大きさは全段で同じ倍率にする。
左上0–2層、右上3–5層、左下6–8層、右下9層以上。

各段で同じ駒・同じ盤面を連続撮影する。
待機2秒を追加してから、本編と同じ台本再生を開始し、連撃・帰還が終わっても撮影を続ける。
待機と攻撃の別動画の貼り合わせや、末尾の静止コマによる尺合わせはしない。
追従版は、実描画を駒の地面上の位置へ合わせて切り出す。
位置関係を確認するための固定画角版も残す。

全段の攻撃原画の開始は3.20秒で一致。
同じ席の待機へ戻るのは0–2層/3–5層が4.27秒、6–8層が5.27秒、9層以上が6.47秒。
帰還後もそれぞれ39/60/25/41種類の待機コマが進み、最後の位置と開始位置が一致する。
`flow-preview-beam-v2.json`が実測、`output/rica-fxviewer-flow/stageN/capture.json`が全コマの記録。
`FX_VIEWER_RICA_FLOW_CAPTURE_OK`と既存の`RicaMotionCheck`377項目が成功。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix beam-v2
```

### 共通検証

- DemoAppビルド成功（警告0・エラー0）。
- `RicaMotionCheck`: 377項目成功。7打・左右・接触・時計・中断・再戦・固定台本HPを照合。
  高い跳躍の保持・次の打への高さの連続・命中時の着地・空中での停止と死亡の片付けも確認。
- `FxViewer --fx-rica-verify`: 8 Cue、左右反転で完走。
- `FxViewer --fx-smoke`: 全422 Cue（登録15・生成407）成功、スキップ0。シーク後のHP・状態・席・停止も一致。
- `IdleLoopCheck --rica-idles-required`: 通常39コマと覚醒3段の進行・全コマ追跡数を確認。
- `DeathAxisCheck`: 実戦15台本のengineとの一致・覚醒・死亡・蘇生・再戦を確認。
- `PortraitStateCheck`・`MovementPortraitCheck`・`FireCheck --replay`: 成功。
- BattleSim Releaseの`compare`: `docs/balance.md`との差分0。`audit`: ずれているファイル0件。
- v3では原画133コマの寸法1152×896・追跡数・透明余白を確認。v4の本番は147コマ。
- Vulkan Forward+で待機4段の実描画とFxViewerの4段を撮影。
  MP4は1440×900・175コマ・15fps・約11.67秒。撮影連番を再エンコードしたもの。

ログは `output/rica-motion-integration/*-final.log` と `compare-final.md`・`audit-final.txt`。
跳躍・ビーム改修後の記録は同フォルダーの`beam-*.log`・`beam-compare.md`・`beam-audit.txt`。
既存の待機、ボルグ25コマ、ガルド56項目、炎の実戦再生、死軸15台本も再確認して成功した。
Godot終了時には既存のTexture/Font/CanvasItem/ObjectDB解放警告が出る検査がある。
本編の機能検査・描画中のエラーとは分けて扱い、今回この共通終了処理は変更しない。
