# ソムの演出と素材の実装記録

`CODEX_BRIEF_SOM_SPARK.md` と `PHASE291_CODEX_MEMO.md` の14節を基に、DemoAppへ召喚、降る光、光の衣、光の消失を追加した。BattleCoreと戦闘数値は変更していない。

## ゲーム内の動き

1. ソムが腕を掲げると敵陣の召喚枠に魔法陣が開く。獣はソムを一度振り返り、そっぽを向いて敵の側に立つ。ソムは呆然とした差分へ切り替わる。初回のみ大げさな召喚の台詞と「……え？」を表示する。
2. 敵側の感電が弾けた地点から粒が浮き、連鎖の終了時にソムの頭上へ集まる。敵・獣・糸玉を出どころとして扱う。味方全員へ短い光の雨を降らせ、回復の数字と同期する。
3. 回復の溢れは各駒の周囲に薄い衣として残る。量に応じて明るくなるが、常駐時の不透明度は0.38までに抑える。攻撃が当たると短い波紋と粒が出て、衣が尽きると散る。
4. `Spark 止まる` では帰る粒を途中で消す。粛のひびは既存の `HushState` に任せ、重複して描かない。反転回復の相手へは紫に濁った光を送る。

浮かび上がる粒は台本の弾けた数と一致させる。雨は1人あたり最大18粒とし、巨大な連鎖でも白く塗り潰さない。召喚と雨の待ちは再生速度に従う。再戦・戦闘終了・編成への帰還では専用の粒、衣、差分、音を消す。

## 衣の残量を扱う範囲

`Spark 衣` の `Amount` を衣の増分、`Slot` を付与後の合算破片として記録する。他の破片が混ざる場合も、合算残量の減りを衣から先に引く。ツギの板、リリの破片、状態の受け渡し、ターン頭のスナップショットに合わせて照合する。

完全吸収した一撃には破片消費量のイベントがない。命中時の反応を出し、厚さの正確な更新は次の既知残量通知まで待つ。`Attack.Amount` と `Damage.Amount` の差で消費量を推定しない。実際のHP減少を示す `Damage` が出た場合は破片が0まで削れたことが確定するので、その場で膜を散らす。このため、ちょうど全破片を使って完全吸収した場合の破砕表示には通知まで遅れが生じる。

## 追加素材

| 用途 | 制作原画 | ゲーム内ファイル |
|---|---|---|
| ソムの召喚 | `som_summon-right-v1.png` | `DemoApp/assets/portraits/battle/som_summon_idle_right.png` |
| ソムの呆然 | `som_stunned-right-v1.png` | `DemoApp/assets/portraits/battle/som_stunned_idle_right.png` |
| 獣の振り返り | `fodder_lookback-right-v1.png` | `DemoApp/assets/portraits/battle/fodder_lookback_idle_right.png` |
| 獣のそっぽ向き | 瞳を修正済みの `fodder-idle-right-v2.png` を再利用 | `DemoApp/assets/portraits/battle/fodder_snub_idle_right.png` |
| 魔法陣 | SVG | `DemoApp/assets/fx/som_circle.svg` |
| 光の粒 | SVG | `DemoApp/assets/fx/som_light.svg` |
| 薄い衣 | SVG | `DemoApp/assets/fx/som_veil.svg` |

通常立ち絵と戦闘待機絵は前回の承認済み素材を使用する。差分原画は透過を維持し、獣の瞳孔は各眼1つに揃えた。生成指示は `som-fx-prompts.md`、採用音源と対応表は `som-sfx-request.md` に記録した。指定音源7本を原形式で配置済み。光の消失は既存の渇きによる回復無効音を直接参照する。

## 実装の入口

- `Main.Som.cs`：台本の処理、召喚の間、回復、衣の帳簿と命中の接続。
- `SomPresentation.cs`：連鎖の出どころと払出しの対応、反転対象の取得、衣の表示用帳簿。
- `BattlefieldView3D.Som.cs` と `SomFx.cs`：魔法陣、粒の軌道、雨、衣の描画。
- `BattlePawn3D.Som.cs`：衣の追従と差分の後片付け。
- `BattleAttackAudio.Som.cs`：8種類の任意音源を読み込む。

## 検証

2026年10月10日、seed 0。実戦台本を通常再生と再戦の2回ずつ完走し、降る・止まる・衣・粒・召喚の件数と最終HPが一致することを確認した。

| 編成と相手 | 降る | 止まる | 回収した粒 | 衣の付与 | 衣の被弾反応 | 破砕 |
|---|---:|---:|---:|---:|---:|---:|
| 試遊・感電 光の盾 × ボス | 57 | 0 | 268 | 284 | 140 | 0 |
| 試遊・感電 光の盾 × 近衛 | 58 | 0 | 78 | 290 | 32 | 3 |
| 試遊・感電 雷の型 × 近衛 | 0 | 0 | 0 | 0 | 0 | 0 |
| 試遊・感電 光の盾 × 第2波 | 15 | 1 | 23 | 6 | 4 | 3 |
| トウ・クグ・ベニ・カタ・ソム × 近衛 | 3 | 0 | 10 | 0 | 0 | 0 |

雷の型にはソムが含まれないので、専用演出が出ない回帰確認として使用した。ベニ入りの台では濁った光の対象が実際の反転イベントに対応することを確認した。板と衣の混在時の優先消費、再戦・終了・編成帰還の消去も検証した。

左右両陣営・等速と4倍速の描画を `fx-check/` に保存。召喚、振り返り、そっぽ向き、収束、雨、衣の7・70・7000の濃さ、命中、消失を撮影した。GodotのCompatibility描画で確認した。

実行コマンド：

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --path DemoApp --headless res://SomCheck.tscn --audio-driver Dummy -- --verify-only
Godot_console.exe --path DemoApp res://SomCheck.tscn --audio-driver Dummy --rendering-method gl_compatibility -- --visual-only
dotnet run --project BattleSim -c Release --no-build -- 0 compare
dotnet run --project BattleSim -c Release --no-build -- 0 dump
dotnet run --project BattleSim -c Release --no-build -- 0 audit
```

DemoAppビルドは警告0・エラー0。`compare` と `dump` は既存の `docs/balance.md`・`docs/units.md` と行単位で一致し、`audit` はずれ0件。生成物は書き換えていない。ヘッドレス検証の終了時にはFont・CanvasItemの解放警告が残る。これはソムの戦闘を始めずに `Main.tscn --headless --quit-after 5` で終了した場合にも再現する。検証完了の判定には `SOM_CHECK_COMPLETE` と終了コード0を使う。
