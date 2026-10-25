# 頭上からのシャベル打ちと、溜めて加速する回転薙ぎ v5

2026-10-24。ユーザーの「スイカ割り／剣道の面のようなスイング」「回転を滑らかにして、溜めから一気に振り抜く」を反映。

## 採用原画と制作記録

**内蔵image_gen**で全身原画を制作。入力・全プロンプト・生成元は [generation-record.json](generation-record.json)。

- [シャベル6姿勢](shovel-sheet.png)：低い構え、頭上の振りかぶり、振り下ろしの途中、前上方への接触、低い振り切り、構え。
- [回転薙ぎ12姿勢・採用アトラス](spin-sheet-selected.png)：前右、溜め、背面、左側、正面、右への打ち抜き、振り切り、構え。
- `shovel-cels/`・`spin-cels/`：全身セルを共通1152×896へ位置合わせした原画。

シャベルは6枚の最終シートを全採用。最初の8枚案は武器の欠け・握りの不一致があり不採用。
6枚案では振りかぶりと接触の三つ編みの重複を画像生成で修正した。
回転は初版の11枚と、修正版の下段左の1枚を全身セル単位で採用。
修正版は最初の構えの三つ編みも消してしまったため、他の11セルには適用しない。
部位の貼り替え・輪郭ワープ・クロスフェード・逆再生を使わない。

## 動きと接触

| 動作 | 全コマ数／30fps | 接触・0始まり | 配分 |
|---|---:|---:|---|
| シャベル | 31 | 15 | 構え4、頭上の溜め9、振り下ろし2、接触5、低い振り切り6、構え5 |
| 回転薙ぎ | 40 | 27 | 構え3、予備動作4、深い溜め9、回転の中間各1〜2、接触3、振り切り5、構え5 |

回転は元の8姿勢から12姿勢へ増やし、背面→左側→正面の途中を追加。
原画の8番と9番は体の角度を確認して並べ替え、`cel-animation.json` の `keys` で順序を指定する。
前半16コマの踏み込みを0に抑え、回転を解放してから前進を加速する。
接触時の前進18pxは位置合わせ用の表示曲線で、駒の席や攻撃範囲は変えない。
背面では目の発光を停止する。

シャベルも振りかぶりの間は前進を抑え、振り下ろしに合わせて前へ押し出す。
打撃衝撃は実際のシャベル先端を主に追い、標的の上側へ寄せる。
好評だった `kado_hit.mp3`（Springin「金属叩き」）と音量はv4のまま。
音とダメージ表示は新しい実接触コマに同期する。

全7打の本番は160コマ、採用する全身姿勢は33枚（入力原画34枚のうち最終段の不整合1枚を従来どおり除外）。
仕様の正は [cel-animation.json](../attack-motion-v1/cel-animation.json)。
本番は `DemoApp/assets/portraits/battle/attack_clip/rica_*/`。

## 素材と再生経路

購入済みEffectBlocks v4の `assets/impacts/impact_1.tscn` と `source_files/textures/sparkle.png` を再確認。
全身の攻撃ポーズは含まないため原画を制作し、既存の光粒・打撃衝撃・金属音を継続使用する。
購入原本・共有リソースは編集しない。
本編とFxViewerで同じ攻撃クリップを再生し、台本の打数・対象・HPは維持する。

## 確認動画

[4段階の待機→攻撃→待機・30fps](../attack-motion-v1/rica-four-stage-flow-comparison-overhead-spin-v5.mp4)と
[盤面全体版](../attack-motion-v1/rica-four-stage-flow-full-board-overhead-spin-v5.mp4)。
各10秒／300コマ。同じ駒を連続撮影し、同倍率で比較する。
動画の音声は左上のシャベル命中音だけを実接触コマへ同期している。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaCelExport.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://RicaMotionCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --headless res://FxViewer.tscn -- --fx-rica-verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 30 res://FxViewer.tscn -- --fx-rica-flow-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/attack-motion-v1/encode_flow_preview.py --suffix overhead-spin-v5 --shovel-audio
```

`RicaMotionCheck` は416項目成功、FxViewerは全8Cueの左右反転を含め成功。
`DeathAxisCheck` も実戦15台本のHP・死亡・蘇生・再戦で成功。
本番160コマの寸法・追跡数・透明余白（最小42px）を確認。
比較動画2本は300コマ／30fps、打撃音のAAC音声付きでデコード検証済み。
ログは `output/rica-motion-integration/v5-*.log`。
終了時の既存RID・Font・ObjectDB解放警告は、動作・Shaderエラーとは分けて記録する。
