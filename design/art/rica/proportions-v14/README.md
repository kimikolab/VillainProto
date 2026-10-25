# リィカ 9層以上・体格の統一 v14

2026-10-25。待機と三連撃で頭身・顔の大きさが変わって見えるという指摘への修正。

待機絵に比べて旧攻撃原画の頭が大きく、胴・脚が短めだった。初版8姿勢とv9の追加12姿勢の原画の比率に加え、それぞれ別の固定倍率で書き出していたため、コマの切り替わりで差が見えた。

踏み込み1枚・攻撃とつなぎ19枚の全身原画を作り直した。基準は `DemoApp/assets/portraits/battle/motion_sources/rica/v1/stage3/idle.png` の顔と成人体格。頭頂から顎の軸長を測り、全身セルを縦横同率で配置する。膝を畳んだ空中姿勢を、全身の外接高さで拡大しない。顔・腕などの部位合成、ワープ、クロスフェードは使わない。

## 素材・校正

- [待機との同倍率比較](rica-stage3-proportions-v14.jpg)：黄色線は頭頂〜顎の校正軸。魔法を重ねる前の全身素材。
- [登録用スクリプト](register_sources.py)、[校正値・対応表](calibration.json)、[検証スクリプト](check_proportions.py)。
- 制作用シートは `group1-adult.png`〜`group5-adult.png`。group4は `group4-selected.png` の左上だけを `group4-adult.png` の全身セルで採用し、`group4-final.png` に保存。
- 正規化後の全身セルは `cels/group1/`〜`cels/group5/`。本番の連番・武器追跡点・踏み込み絵は既存の `DemoApp/assets/portraits/battle/` に書き出す。
- 内蔵 **image_gen** で生成・編集。プロンプト全文と生成原本の保存先は [generation-record.json](generation-record.json)。原本を残し、採用画像をこのフォルダへコピーした。CLI/APIの代替生成は使用していない。
- 購入済みNatureBlocks・EffectBlocksの一覧と実ファイルを確認。人物の頭身の差分は含まれないため全身原画を制作し、既存の背景・着弾火花を継続する。購入素材の原本は変更していない。

基準の頭の軸長と、待機の1920pxキャンバス・高さ倍率1.3、攻撃の基準1536px・倍率2.8から、攻撃キャンバス上の目標軸長を **73.6085px** にした。各セルの拡縮は全身に適用し、シャベルのsocket/tip・眼・接地基準も同じ変換で動かす。空中で脚を折る姿勢には仮想の接地基準を置き、膝を畳むだけで腰が持ち上がらないようにした。

実行時に顔を変形する補正はない。新規の頭の基準点は書き出し器と素材検証用のメタデータで、戦闘ロジックは参照しない。

## ゲームと動画

- [修正前後・同倍率・通常→1/2速](rica-triple-proportions-before-after-v14.mp4)。
- [4段階の待機→踏み込み→攻撃→待機・30fps](../attack-motion-v1/rica-four-stage-flow-comparison-proportions-v14.mp4)。

DemoApp本編とFxViewerの共通素材へ適用した。FxViewerで「死・リィカの覚醒」→「リィカ 覚醒 9層以上・三連撃（攻撃）」を選ぶ。直接指定は `--fx-cue=rica.stage3.attack`。

突き12コマ・接触6、空中回転13コマ・接触4、落下斬り28コマ・接触16、打間のつなぎ各9コマを維持した。跳躍・回転角・武器形状・ヒット時刻・対象・件数も同じ。二連撃の速度と青白い発光はv12/v13を継続する。

動画はFxViewerの実描画を切り出し、映像側の動作補間や剣閃の描き足しはしていない。4段階版の0〜2段は前回の撮影を再利用し、今回変更した3段だけを撮り直した。待機への復帰は193コマ目、帰還後の待機107コマを収録。

## 検証・再生成

- ビルド成功、最終ビルドは警告0・エラー0。
- RicaMotionCheck：実描画・撮影込み **556項目**。7攻撃、4段階、左右反転、接触、一時停止、速度、中断、再戦、シーク後のHP。
- FxViewer：攻撃・待機8 Cueの左右反転で成功。
- 段別の実撮影300コマ。三連撃3発を再生し元の席の待機へ復帰。
- [素材検証](asset-check.json)：頭の軸長が全コマで目標値に一致。他段の132ファイルのSHA256不変。キーの保持時間・接触・跳躍・回転・形状はv13と一致。
- [動画検証](video-check.json)：比較495コマ・16.5秒、4段階300コマ・10秒。ともに30fpsで全コマ再デコード。4段階版の音声ピーク0.411、クリップなし。
- ログ：`output/rica-motion-integration/v14-*.log`。Godot終了時の既存のRID・Font・CanvasItem・ObjectDB解放警告は残る。実再生中のShaderエラー・検証失敗とは分けて確認した。

```powershell
# 初回の原画登録。既存のv13仕様と校正値からv14を再現する
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/proportions-v14/register_sources.py
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/proportions-v14/check_proportions.py
New-Item -ItemType Directory -Force output/rica-motion-integration/v14-shots
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp res://RicaMotionCheck.tscn -- --capture-dir=C:/works/VillainProto/output/rica-motion-integration/v14-shots
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture --fx-rica-flow-stage=3
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix proportions-v14 --shovel-audio --comparison-only
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/proportions-v14/encode_comparison.py
```
