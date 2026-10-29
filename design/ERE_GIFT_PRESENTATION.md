# エレの哀歌：手番の受け渡し（2026-10-27）

歌唱だけでは、誰が追加で動いたかを追いづらかったため、受け渡しと実際の手番開始を分けて描く。

- 演奏開始：Cの自然なモーションを保ち、楽器の光から金の五線譜と音符を開く。エレの周りを音符が巡り、足元の明るい光だまりと上向きの薄い金色の光を舞台照明にする。顔とHPを空け、既存の3秒の演奏に合わせて表示する。
- 受け渡し：エレが歌い、五線に沿って音符が受け手へ流れる。届いた瞬間に小さな光片が弾ける。
- 受け取り：受け手を大きな音楽の文字盤で囲み、長針を時計回りに巻く。円形の五線譜、12か所の金の音符、外周の蔓飾りを重ね、文字や数字を置かない。実手番までは時計・巡る音符・胴と装備の発光を淡く保持する。
- 実手番の直前：音符を胸へ集め、長針が残像と外周の光の軌跡を引いて2周以上進む。文字盤は正立したまま、長針が12時に達する約0.70秒で身体の光と小さな光片を解放し、実際の追加行動へ繋ぐ。文字盤と波紋はさらに0.46秒で消える。
- 味方は青白、エレから見た敵は桃色。左右反転でも陣営関係から色を決める。
- テロップ・「SKILL」・汎用の見せ場バナーを重ねない。文章は戦闘ログに残す。

最初は受け取りと開始をテロップでも示していたが、ユーザーの指定で文字なしの演出へ変更した。
`DirgeAura3D` が受け取りから実手番までの光を保持し、台本の `Act` で解放する。
文字の削除だけで終わらせず、到着→保持→時計の加速→発光→行動の時間の繋がりを強めた。

2026-10-27の強化では、追加手番こそエレの見せ場という要望に合わせ、従来の円形矢印を
`assets/fx/ere_clock.gdshader` の専用時計へ置き換えた。直径3.15の板に透過した線と針だけを描き、
顔とHPを読み取れる範囲で受け手を囲む。時刻は `DirgeAura3D` から渡し、シェーダーの `TIME` を使わない。
長針が進み切る時刻と実手番への接点は `ReleaseContact` の1箇所で共有する。

続く調整では、60本の細い刻みが現代の時計に見えるという指摘を受け、
`assets/fx/ere_clock_face.svg` の円形の五線譜・12か所の音符・金の蔓飾りへ置き換えた。
音符は正立させ、上下左右を連桁のある音符で示す。長針・短針・加速・発光・行動開始の時刻はそのまま。
SVGは通常のGodotテクスチャとして読み込み、赤と青のマスクを金と陣営色に着色する。
文字盤はエレの音楽に合わせた専用ベクターで、購入素材の `impact_3` による解放の光片は継続利用する。

2026-10-29、ユーザーは新しい演奏A／Bより既存Cを自然と評価し、Cを継続する方針を選択。
通常立ち絵との差を大きくする代わりに、演奏開始をエフェクトで伝えることにした。
`Main.HoldDirgeSong` の開始で `ShowDirgePerformance` を呼び、ギフト前から五線譜・音符・楽器と足元の光を出す。
既存の演奏の待ち時間と同時に進め、追加の待ち時間は入れない。演奏後の0.45秒は受け渡しと重ねて消す。
足元の光をスポットライトの代わりにしたいという要望で、光だまりの明るさと広がりを強め、`ere_song_uplight.gdshader` の扇状の光を追加。顔の手前で薄く消し、衣装の陰影と陣営リングを保つ。
モーションの選択記録は `art/ere/performance-v2/notes.md`。既存Cの48コマと原画はSHA-256でも変更なしを確認した。

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
時計の強化でも両実ファイルを再確認し、`impact_3` を12時の解放にも小さく追加した。
演奏開始の強化でも実ファイルを確認し、`impact_3` を楽器の小さな光片として利用。`electric_waves` は雷に見えるため引き続き不採用。
五線譜は専用の薄い曲面、足元の光は `ere_song_light.gdshader`。光と音符は `GatherCast3D` の同じ時計で動かし、シェーダーの `TIME` は使わない。
針と静止目盛りの読みやすさを優先し、雷の輪は引き続き使わない。
素材がない環境でも五線・音符・光紋・身体の発光は表示できる。
五線と音符はコードと専用SVGで作る。SEは既存の魔法陣展開／ステータス上昇音を小さく使う。

`GatherCast3D` の時計と後片付けを共有し、一時停止・途中の速度変更・中断・死亡・再戦に追従する。
購入素材の場所・導入方法は `GodotApp/ASSET_RESEARCH_AND_PLAN.md`。

## 確認

演奏開始SEの試用（2026-10-29）：400 Sounds Pack の `harpsichord_mystery.wav` を
`ere_harpsichord.wav` として配置し、開始0.02秒後に-6 dBで一度鳴らす。原音は約1.497秒。
ピッチとループは変更せず、音の一時停止・終了は `GatherCast3D` に従う。
音源本体はGitに含めず、配置元は `DemoApp/assets/audio/se/SOURCES.md` に記録する。
既存の受け渡し・追加手番のSEは継続。
[実録音付きの確認動画](art/ere/song-start-v1/ere-harpsichord-trial.mp4) は味方→敵の順。
`--write-movie <絶対パス>.avi` と `-- --movie` でGodotの映像・音声を同時収録し、
`design/art/ere/song-start-v1/export_audio_preview.py` でMP4へ変換できる。
`EreGiftCheck` は音源がある環境で、開始・原音ピッチ・停止・倍速後の再開も検査する。

同日のユーザー提供曲A/Bの比較では、3秒の演奏後も音を続ける版を追加した。
開始の光と音楽の寿命を分け、最初の追加手番の時計が消える時刻に音楽も終える。
終端0.35秒でフェード。演奏C・受け渡し・時計・追加行動の時刻は維持。
音源の比較は `EreGiftCheck -- --ere-audio=clock-a` / `clock-b`。
録画と導入・検証記録は [時計まで音を続ける比較](art/ere/song-audio-v1/clock-notes.md)。
ユーザーが両曲を採用したため、現在は通常の演奏発動ごとにA→B→A→Bを繰り返す。再戦ではAから始める。
[交互再生の確認動画](art/ere/song-audio-v1/ere-alternate.mp4)。

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
控えた件数と実手番の件数を計数と突き合わせ、受け手と実手番の順序を確認する。
描画は敵味方・左右・途中停止・倍速変更・受け手の死亡・中断・再戦を確認し、本番経路で2回再生して件数と最終HPを照合する。
受け渡し・解放の両方でテロップを作らないこと、実手番まで光を保持すること、解放後・死亡・再戦で光が消えることも検査する。
時計の針も停止すること、加速途中で行動開始へ進まないことを追加で検査する。
演奏開始の強化では、文字なしの開始・途中停止・倍速・演奏者の死亡・余韻の消去を `EreGiftCheck` で確認。
`EreMotionCheck --installed` では本番MainでCと開始の光を同時に止め、2体への受け渡しまでに余韻が消えることも確認する。
`--clip --capture-dir=<絶対パス>` で連番の保存先を指定できる。時計の強化後の連番は `.tmp/ere-clock-v2/`。
確認用の動画は [gift-clock-v2.mp4](art/ere/gift-clock-v2.mp4)（味方→敵・158コマ／30fps）、
動きの抜粋は [gift-clock-v2.gif](art/ere/gift-clock-v2.gif)。
音符と蔓飾りへ変更した版は [gift-clock-score-v3.mp4](art/ere/gift-clock-score-v3.mp4) と
[gift-clock-score-v3.gif](art/ere/gift-clock-score-v3.gif)。連番は `.tmp/ere-score-v3/`。
DemoAppビルドは警告・エラー0、`EreGiftCheck` は合格。Forward+で受け取りから追加攻撃までの連続描画も確認した。

時計の強化後は、DemoAppビルド・`EreGiftCheck`（頭なしとForward+）・`EreMotionCheck --installed` が合格。
`compare` / `dump` の再出力は既存の生成物と一致し、`audit` はずれ0件。
ビルド時の未変更BattleCore内の2警告と、Godot終了時の既存のRID／ObjectDB警告は残る。
画像は `.tmp/ere-gift-check/`。文字なしの連続描画は `.tmp/ere-gift-textless/`（音の受け渡しから実際の攻撃まで）。

`compare` は着手時・変更後・既存の `docs/balance.md` で一致。`dump` は既存の `docs/units.md` と一致。
`watch` と `watch phase0` を再生成し、出来事が107→108種、初期化子が168→169箇所になった差分のみ。
`audit` はずれ0件。終了時のGodotリソース解放警告は既存の検査でも出ている。
