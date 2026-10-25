# ハギ・一撃必殺の刈り取り演出

2026-10-25。「刈り取る時のエフェクトを派手に美しく、一撃必殺感が出るように」に対応。
採用済みの前傾突進・衣装・配色は維持し、刈り取りだけの光と消散を強化した。

## 追加試作：リィカの青と対になる赤（reap-crimson-v4）

2026-10-25。銀白の炎状オーラを評価したうえで、「リィカの青い斬撃と対になる赤」を試す依頼に対応。
白熱した芯は維持し、斬線の縁と残光を朱〜赤、炎の外縁を深紅へ変更した。
刃に収束する光、衝撃輪、斬撃から散る光片も暖色に揃えた。
弧・炎の揺らぎ・時間・範囲は直前の試作と同じ。

明るい草原では加算だけの赤が橙色に寄ったため、炎の層は透過合成へ変更し、
背景の緑を抑えて赤い外縁を残す。芯と残光は加算を継続する。
キャラクターや鎌の画像、通常攻撃、魂の吸収、敵の銀白の消散は変更していない。

購入素材の採用記録と `fire_light.tscn` の赤〜黄の炎を再確認。
専用の弧と再生時計を維持し、EffectBlocks の `sparkle.png` と `circle_1.png` を引き続き色調整して使用した。
購入素材の原本は変更していない。

ビルド成功。`HagiPresentationCheck` の9戦、停止・倍速・反転・中断・再戦・シークが通過。
実描画の白い芯・赤い炎・残光を確認し、単発と連続攻撃のMP4を最後までデコードした。
終了時の既存RID／ObjectDB解放警告は残る。戦闘判定・対象・件数・命中時刻は変更していない。

- [赤・刈り取り・等速MP4](../reap-crimson-v4/video/hagi-reap-crimson-1x.mp4)
- [赤・刈り取り・0.5倍速GIF](../reap-crimson-v4/video/hagi-reap-crimson-half-speed.gif)
- [赤・連続攻撃・等速MP4](../reap-crimson-v4/video/hagi-chain-crimson-1x.mp4)
- [赤・連続攻撃・0.5倍速GIF](../reap-crimson-v4/video/hagi-chain-crimson-half-speed.gif)

直前の銀白版の動画は以下に保存。比較用に上書きしていない。

## 追加試作：白熱する芯と炎状のオーラ（reap-aura-v3）

2026-10-25。添付スクリーンショットの「明るい芯を、炎のような光が包む」構成を参考に追加。
芯を太めの白光にし、その周囲に淡い鋼青色の火舌が流れる層を重ねた。
外縁はノイズを移流させて不規則に伸縮し、芯は揺らさず連続した斬線を保つ。
局所的な光の重なりで明るさを作り、環境全体のブルーム設定は変更しない。

- `hagi_reap_arc.gdshader`：白い芯の幅と発光を強化。遅れた弧は弱め、芯の存在感を優先。
- `hagi_reap_aura.gdshader`：斬線と同じ弧の上下へ火舌を生成。長い上側と短い下側で巻き返しを作る。
- `HagiReapFx3D.cs`：新しい層も既存の再生時計・反転・寿命で制御。`TIME` は使わず、停止中は炎も停止する。

購入素材の `assets/fire/fire_magic.tscn` と `assets/fire/fire_light.tscn`（上昇する粒子の炎）、
`assets/attacks/attack_fire.tscn`（地面からの炎攻撃）を実ファイルで確認。
斬線に密着する連続した火舌には専用シェーダーを使い、光粒と衝撃輪には既採用の `sparkle.png` と `circle_1.png` を継続利用した。
購入素材の原本は変更していない。

ビルド成功。`HagiPresentationCheck` の9戦と停止・倍速・反転・中断・再戦、
`DeathAxisCheck` の15戦および既存リィカの再生チェックが通過。
Godot実描画の命中直後と残光を確認し、書き出したMP4を最後までデコードできることも確認した。
終了時の既存RID／ObjectDB解放警告は残る。戦闘判定・件数・対象・命中時刻は変更していない。

- [刈り取り・等速MP4](../reap-aura-v3/video/hagi-reap-aura-1x.mp4)
- [刈り取り・0.5倍速GIF](../reap-aura-v3/video/hagi-reap-aura-half-speed.gif)
- [連続攻撃・等速MP4](../reap-aura-v3/video/hagi-chain-aura-1x.mp4)
- [連続攻撃・0.5倍速GIF](../reap-aura-v3/video/hagi-chain-aura-half-speed.gif)

以下は追加試作前の記録。以前の動画も比較用に残している。

## 見せ方

1. 到着して構えた刃へ、銀の光が収束。対象の後ろだけを淡く落として光を際立たせる。
2. 命中で、白い芯と幅のある銀の刃が大きく走る。短い局所閃光、遅れて消える残光、薄い衝撃輪を重ねる。
3. 敵の輪郭が銀白に浮き、細かな光の縁からほどける。銀片と光粒が弧に沿って舞い上がり、静かに消える。

通常攻撃・味方へのかすり傷は従来の小さな弧を維持する。
列刈りも一振りにつき一つの演出とし、実際の対象の範囲へ弧を広げる。
数字は実ダメージのまま、大きな銀色で表示。直前の小さな数字と重ならないよう入れ替える。
効果音は既存の物理解放音を刈り取りにだけ使い、一打の重さを補う。
血・切断・新しい技名テロップや全画面の白塗りは加えていない。

## 実装と素材

- `HagiReapFx3D.cs` が予兆→命中→余韻を一つの再生時計で制御。
- `hagi_reap_arc.gdshader` が刃の芯・幅・残光を描く。
- `hagi_reap_glow.gdshader` が局所閃光・背景の陰・銀片を描く。
- 敵の消散は既存立ち絵シェーダー内の専用ノイズ。ゴルムの四角い崩れを流用しない。
- `BattleCore`、イベントの件数・標的・HP、移動や刈り取りの溜め時間は変更していない。

購入素材は `GodotApp/ASSET_RESEARCH_AND_PLAN.md` と EffectBlocks v4 の実ファイルを確認。

|素材／候補|採否と理由|
|---|---|
|`source_files/textures/sparkle.png`|採用。刃へ収束する光、斬光の火花、死の光粒に使用。色・寸法・分布・寿命を調整|
|`source_files/textures/circle_1.png`|採用。命中直後の薄い楕円の衝撃輪に使用|
|`assets/impacts/impact_1.tscn`|確認。円形の着弾を中心とした構成なので、シーン全体の自動再生は使わず、上記テクスチャを部分利用|

購入素材の原本やシーンは改変していない。新規の描画はコードとシェーダーで作成した。
購入素材未導入の環境では、既存 `death_light.svg` の代替光粒を使用する。

## 確認結果

- DemoAppビルド成功。
- `HagiPresentationCheck`: 指定9戦で観測あり／なしの台本・勝敗・最終HP・攻撃／刈り取り件数が一致。
- 新しい予兆と命中後の残光について、途中停止・倍速・接触時の切り替え・再戦時の破棄を追加確認。
- 既存の前傾差分・接地・反転・中断・シークも通過。
- `DeathAxisCheck`: 既存の死軸15戦と、リィカの覚醒・吸収・連撃・再戦を確認。
- Godotの実描画で単発／列刈りと二連続攻撃を録画し、命中の閃光・粒の位置・消散を目視確認。
- 終了時の既存RID／ObjectDB解放警告は残る。

動画は `hagi.live.0.2` の実戦の攻撃列を使い、周囲の演出を省いた確認用。
以前のエフェクトは [step-v2の動画](../step-v2/video/) と比較できる。

- [刈り取り・等速MP4](video/hagi-reap-beauty-1x.mp4)
- [刈り取り・0.5倍速GIF](video/hagi-reap-beauty-half-speed.gif)
- [連続攻撃・等速MP4](video/hagi-chain-beauty-1x.mp4)
- [連続攻撃・0.5倍速GIF](video/hagi-chain-beauty-half-speed.gif)

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --path DemoApp res://HagiPresentationCheck.tscn
Godot_console.exe --headless --path DemoApp res://DeathAxisCheck.tscn
Godot_console.exe --path DemoApp --fixed-fps 60 --write-movie C:/works/VillainProto/output/hagi-movie/raw-reap-beauty-v2.avi res://HagiMovieCapture.tscn -- --hagi-movie-dir=res://../design/art/hagi/reap-beauty-v2/video
```
