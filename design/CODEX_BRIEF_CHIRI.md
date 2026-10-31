# Codex への依頼 —— 導火線のチリ（第429期・DemoApp だけ）

件名: 導火線のチリ——爆弾を仕掛ける／導火線が縮む／爆ぜる・向かいへ飛ぶ見せ方（DemoApp だけ）

判定はすべて BattleCore にある。DemoApp は台本の出来事を読むだけで、ログの文字列から規則を作らない。
爆弾の出来事はすべて `BattleEventKind.StatusGain` で、`Text` で見分ける（定数は `BattleContext.BombPlantText` ほか・第429期）。

1. **仕掛ける**（`bomb_plant`・`ActorId` ＝ チリ・`TargetId` ＝ 持ち主・`Amount` ＝ 3）: チリの `Skill`「仕掛ける」の直後に、置かれた敵（最前列の 2 体）と隣の味方の頭上に爆弾の印を出す。導火線は 3 段。
2. **導火線が縮む**（`bomb_fuse`・`ActorId` ＝ 縮めた一撃の主・`Amount` ＝ 残り 2 → 1 → 0）: 印の導火線を 1 段縮める。直前の `Damage` の主と同じ駒なので、その一撃に合わせて火花を散らす。
3. **爆ぜる**（`bomb_blast`・`TargetId` ＝ 持ち主・`Amount` ＝ 重さ）: 敵の爆弾。持ち主と両隣の敵に爆発を出す。続く範囲の `Damage` はこの爆発の分。誘爆の `bomb_blast` が続くときは少し遅らせて連続に見せる。
4. **向かいへ飛ぶ**（`bomb_mirror`・`TargetId` ＝ 持ち主（味方）・`FromId` ＝ 向かいの敵・`Amount` ＝ 重さ）: 味方の爆弾。持ち主から `FromId` の敵へ火の玉を飛ばし、そこで爆ぜる（向かいの敵とその両隣に `Damage` が続く）。味方は巻き込まない。
5. **不発**（`bomb_dud`・`Amount` ＝ 0）: 持ち主の `Death` の後に来る。印を静かに消す。
6. いまの `Main.cs` の `StatusGain` の汎用の枝は、知らない `Text` を「＋3 bomb_plant」のような浮き文字 ／ 印 ／ 音で出してしまう。爆弾の 5 つはその枝に落とさず、上の専用の見せ方だけにする。
7. 確かめ: 「試遊・導火線 チリ」のボス（`--demo-playtest-stage=0`・台本 320 件）と本編 第4波（`--demo-stage=4`・95 件）が最後まで再生される。数の目安は `dotnet run --project BattleSim -c Release 0 chiri429 events`（近衛 seed 0 で bomb_plant 9 ／ 爆ぜた 5（うち向かい 1）／ 不発 4）。
