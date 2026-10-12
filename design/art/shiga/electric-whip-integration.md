# シガ・強化状態の電撃鞭（2026-10-12）

## 変更

`HasShockAura` または `InterruptWhip` の一振りを `ElectricWhip3D` へ接続した。
初版の太い革の芯・蛇行する輪郭・扇状の面は、ユーザーに「触手に見える」と評価された。
提示された参考画像の、白く鋭い交差する光跡と、大きく振り回す円弧を基準に描画を作り直した。

- A・連続打ち（本編の既定）: 12本の短い打ち付けの光跡と、実際の接触に合わせた光跡。
  縦・横・斜めの滑らかな曲線が次々走る。一本は0.07秒で走り抜け、0.16秒で消える。
  白い芯を細く絞り、外側の青・紫の光は縁へ向けて透明に落とす。上体も短く往復する。
- B・大振り（比較用）: 横に広い円弧を一気に振り回し、弧の後方に薄い残光を残す。
  一振りの表示時間の74%で先端が円周の94%を回る。全面を塗る扇は出さない。

薄い光跡は立ち絵の手前に描き、交差する動きが埋もれないようにした。
光跡の追加は表示だけで、追加のダメージ・命中通知・効果音を発生させない。
既存の `shiga_interrupt` 差分を使用し、新しい立ち絵は作らず軌道を実行時に生成する。

命中点から18本の短い電気の破片が飛び、落下しながら0.36秒で消える。
連鎖数による太さ・飛散範囲の拡大は8連鎖で頭打ち。4連鎖以上の白閃光は既存の入口を使う。
通常の革の鞭は従来の描画を使う。

## 購入素材

EffectBlocks v4の実ファイル `electric_sparks_1` / `electric_sparks_2` /
`electric_sparks_3` を確認した。
連続放出の1、点状の3より、短い破片が同時に広がる2が命中時の飛散に合うため採用した。
改修時にはenergyのレーザー・電波・雷と軌跡素材の有無も確認した。
打ち付け・大振りの軌道に合う鞭素材がないため、光跡は専用メッシュで描き、火花は購入素材を継続使用する。

- 原本: `D:/Assets/GodotAssets/EffectBlocks v4/PolyBlocks/EffectBlocks/`
- 配置先: `DemoApp/PolyBlocks/EffectBlocks/`
- 使用シーン: `assets/energy/electric_sparks_2.tscn`
- 既存の `DemoApp/PrepareAssetPreview.ps1` で配置する。購入素材本体はGitへ追加しない。
- 発動ごとに粒子処理材・描画材を複製する。22粒、単発、寿命0.32秒、青白い色、初速3.8〜7.0。
- シーンのスクリプト・自動反復は使わない。購入素材なしでも鞭・残像・18本の破片を描く。

## 台本・時計・後片付け

対象・接触順・待ち時間は既存の鞭と同じ。
薙ぎは0.64秒中の `0.22 + 0.55 × 対象の順番 / max(1, 対象数 - 1)`、
単体は0.60秒中の0.62（0.372秒）で接触する。
鞭と電撃の2音は最初の接触で一組だけ鳴らす。HP・戦闘規則・乱数には触れない。

`Main.TogglePause` / `SetSpeed` から `SetWhipClock` を呼び、鞭・破片・購入粒子・
攻撃差分・上体の傾きを同じ再生速度に合わせる。途中停止で接触を予約し直さない。
戦闘終了・非表示・再戦・書き手の死亡で発動ノードを破棄し、待っている再生処理も解放する。

## 検証・観察

```powershell
dotnet build DemoApp/DemoApp.csproj -v minimal
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://ElectricWhipCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://ShockMarkAudioCheck.tscn
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://ShigaMioCheck.tscn -- --verify
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --headless --path DemoApp res://ShockMarkCheck.tscn -- --web --verify
dotnet run --project BattleSim -c Release -- 0 audit
```

ビルド、左右・1倍/2倍の接触順と時刻、単体、5連鎖、命中前後の一時停止、途中の2倍への変更、
死亡・終了・非表示・再戦、購入素材なし、演出だけでHPが変わらないことを検証した。
`ELECTRIC_WHIP_CHECK_OK` / `SHOCK_MARK_AUDIO_CHECK_OK` / `SHIGA_MIO_CHECK_OK` /
`SHOCK_MARK_CHECK_OK` を確認。実台本は再戦を含み最終HP・演出件数を照合する。
実台本の終了時には既存のFont/CanvasItem/ObjectDB等の解放警告が残る。

観察用は `res://ElectricWhipCheck.tscn -- --preview`。
`--compare` でAとBを順に、`--wide` でBだけを再生する。指定なしは本編と同じA。
`--once` で味方側の薙ぎと5連鎖の単体だけを再生する。
`--sweep-only` で薙ぎだけを比較できる。
`--capture-dir=<既存の絶対パス>` でPNG連番と演出内の時刻を保存する。
書き出し中は時計を遅くし、時刻ファイルから実速・スローのプレビューを作れる。
