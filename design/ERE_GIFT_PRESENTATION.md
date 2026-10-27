# エレの哀歌：手番の受け渡し（2026-10-27）

歌唱だけでは、誰が追加で動いたかを追いづらかったため、受け渡しと実際の手番開始を分けて描く。

- 受け渡し：エレが歌い、五線に沿って音符が受け手へ流れる。届いた瞬間に小さな光片が弾ける。
- 受け取り：実手番まで受け手に小さく巡る音符・淡い光紋・胴と装備の発光を残す。
- 実手番の直前：音符を胸へ集め、回転する光紋が収縮→拡大する。身体の発光の頂点（約0.25秒）から実際の追加行動（約0.28秒）へ繋ぐ。
- 味方は青白、エレから見た敵は桃色。左右反転でも陣営関係から色を決める。
- テロップ・「SKILL」・汎用の見せ場バナーを重ねない。文章は戦闘ログに残す。

最初は受け取りと開始をテロップでも示していたが、ユーザーの指定で文字なしの演出へ変更した。
`DirgeAura3D` が受け取りから実手番までの光を保持し、台本の `Act` で解放する。
文字の削除だけで終わらせず、到着→保持→凝縮→発光→行動の時間の繋がりを強めた。

`BattleEventKind.Dirge` の `Gift`（控えた時）と `Act`（実際に動く直前）を読む。
画面は攻撃力から相手を選び直さず、ログ本文も解析しない。
倒れた受け手・敵の全滅で飛ばされた手番には `Act` が出ないので、実際に動くという表示も出ない。
通知は verbose のときだけ記録し、盤面・乱数・計数を変更しない。
従来の `Main.Death.cs` にあった Highlight と手番枠からの推測を、この通知に置き換えた。

## 素材

購入済み EffectBlocks v4 の実ファイル `energy/electric_waves.tscn` と `impacts/impact_3.tscn` を確認。
前者は雷との混同と素材固有の時間進行を避けるため不採用。
後者は受け渡しの着点に小さな光片として採用。既存の `GatherCast3D.Purchased` を通し、
インスタンスの粒子・色・寿命だけを調整する。原本・共有リソースは変更しない。
素材がない環境でも五線・音符・光紋・身体の発光は表示できる。
五線と音符はコードで作る。SEは既存の魔法陣展開／ステータス上昇音を小さく使う。

`GatherCast3D` の時計と後片付けを共有し、一時停止・途中の速度変更・中断・死亡・再戦に追従する。
購入素材の場所・導入方法は `GodotApp/ASSET_RESEARCH_AND_PLAN.md`。

## 確認

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --path DemoApp res://EreGiftCheck.tscn
Godot_console.exe --path DemoApp res://EreGiftCheck.tscn -- --capture
Godot_console.exe --path DemoApp --fixed-fps 30 res://EreGiftCheck.tscn -- --clip
Godot_console.exe --headless --path DemoApp res://ErePortraitCheck.tscn
Godot_console.exe --headless --path DemoApp res://DeathAxisCheck.tscn
Godot_console.exe --path DemoApp res://FxViewer.tscn -- --fx-cue=live.ere.gift
```

`EreGiftCheck` は2編成 × 3波 × 2 seedの12戦について、verbose有無の勝敗・ターン・生存数・各個体のHP／席／攻撃力を照合。
控えた32件と実手番24件を計数と突き合わせ、受け手と実手番の順序を確認する。
描画は敵味方・左右・途中停止・倍速変更・受け手の死亡・中断・再戦を確認し、本番経路で2回再生して件数と最終HPを照合する。
受け渡し・解放の両方でテロップを作らないこと、実手番まで光を保持すること、解放後・死亡・再戦で光が消えることも検査する。
画像は `.tmp/ere-gift-check/`。文字なしの連続描画は `.tmp/ere-gift-textless/`（音の受け渡しから実際の攻撃まで）。

`compare` は着手時・変更後・既存の `docs/balance.md` で一致。`dump` は既存の `docs/units.md` と一致。
`watch` と `watch phase0` を再生成し、出来事が107→108種、初期化子が168→169箇所になった差分のみ。
`audit` はずれ0件。終了時のGodotリソース解放警告は既存の検査でも出ている。
