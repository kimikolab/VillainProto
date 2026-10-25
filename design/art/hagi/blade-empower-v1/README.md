# ハギ：大鎌に密着する赤い強化オーラ

2026-10-26。魂の蓄積を示す銀白の独立した弧を、現在の赤い斬撃に合わせた刃の強化表示へ変更した。

- [魂0・3・6・9の比較、通常速度MP4](video/hagi-empower-stages-1x.mp4)
- [比較GIF、通常速度](video/hagi-empower-stages.gif)
- [比較静止画](stages.png)
- [通常攻撃→刈り取り→連撃、通常速度・ゲーム実音声](video/hagi-empowered-battle-1x.mp4)
- [刈り取り、半速GIF](video/reap-half.gif)

魂3は刃の赤熱、6は炎と火の粉の増加、9は白熱した芯と深紅の火舌で表す。光の根元は刃に固定し、強化段階によって炎の長さ・明るさ・火の粉が育つ。魂が減れば段階も下がり、未強化では消える。周囲の魂の欠片は別の情報として継続している。

## 刃への追従

従来の「輪郭を根元から外側へ拡大する」処理を外し、実際の刃の外周へ光を配置した。採用済み待機48コマをSIFT・RANSACで追跡し、実画像のアルファ外周へ吸着した輪郭を `idle_loop/hagi/weapon.json` に保存した。全48コマの画像は読み取りだけで変更していない。追跡の再投影誤差は中央値の最大1.328px。[tracking-audit.json](tracking-audit.json) に各フレームの値を記録した。

攻撃中は既存の全身セルに対応した追跡点を使う。座標はカメラの基底で再構築せず、実際の `Sprite3D.ToGlobal` で変換する。これにより、斜めの戦闘カメラ、呼吸の拡縮、反転、傾き、移動にも合わせる。正面比較だけでは見えなかった斜めカメラでのずれを修正し、本番の録画で刃と一致することを確認した。

炎は専用の `hagi_blade_empower.gdshader`。進行は戦闘と同じ時計から渡す `age` と魂の実段階を使用する。独立した自動ループやゲーム判定は持たない。購入済み素材の一覧と使用中のEffectBlocks `sparkle.png` を確認し、新しい火の粉にも同素材を利用した。素材原本は変更していない。

承認済みの三日月斬撃の白い刃・赤い炎のシェーダーは変更していない。BattleCore、打数、ダメージ、標的、攻撃の全身原画、待機PNG、生成済みdocsも変更していない。

## 検証

- DemoAppビルド：警告0、エラー0。
- `HagiPresentationCheck`：9種類の実戦台本で打数・刈り取り数・最終HP一致。
- 追加確認：魂0/3/6/9と発光段階の一致、未強化時の非表示、立ち絵の実頂点と同じ座標変換、待機中の追従、停止・再開。
- 継続確認：倍速・左右反転・連撃・中断・死亡・復活・再戦・シーク。
- GPU録画：段階比較210フレーム、実戦動作518フレーム。シェーダーエラー・描画エラー・終了警告なし。

headless検査成功後の終了時には、前版と同じRID/ObjectDB解放警告がある。詳細はローカルの `output/hagi-blade-empower/`、検査結果と承認済み素材の保持確認は [verification.json](verification.json)。動画は本番と同じ駒・素材・描画を切り出したもので、後付けの効果や動作補間はない。比較シーンでは足元の陣営リングとHUDを隠している。

## 再現

待機PNGが更新された場合は輪郭データも再生成する。元画像には書き込まない。

```powershell
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/blade-empower-v1/track_idle_blade.py
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://HagiPresentationCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 60 --write-movie 'C:/works/VillainProto/output/hagi-movie/raw-blade-stages-v1.avi' res://HagiEmpowerCapture.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 60 --write-movie 'C:/works/VillainProto/output/hagi-movie/raw-blade-empower-battle-v1.avi' res://HagiMovieCapture.tscn -- --hagi-acrobatics --hagi-movie-dir=res://../design/art/hagi/blade-empower-v1/video
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/blade-empower-v1/encode_preview.py
```
