# 第438期 報告 —— 段0: 「試遊・呪い」の入れ替え ／ 本体: 作戦マップの盤を「マスのグラフ」に読み替える（段A1・規則不変・見た目不変）

指示書: `design/PHASE438_MAP_GRAPH_SPEC.md` ／ 新しいファイル: `DemoApp/MapGraph.cs`（盤）・`DemoApp/MapGraphSmoke.cs`（門 `--map11-graph-smoke`）。
**この期で足した遊びは 0。** `BattleCore` は段0 の `Presets.cs` だけ・`docs/` は動かない・engine は触っていない。

## 先に（ポンに出すもの）

- **段0**: 「試遊・呪い」＝ 第437期の「試遊・呪い 萎縮」の並び（前1 ワタ ／ 前3 ムド ／ 中央 ウツ ／ 後1 クビ ／ 後3 ネル・`Presets.CurseDaunt437`）。「試遊・呪い 萎縮」の行は外した。`compare` 0 セル・`docs/` 差 0・`audit` ずれ 0。照合 218 本で差 8 本（どれも現役網の外・判定は ○ のまま）→ **固定 0 本・封印 9 本**（§1-2）。DemoApp の単発の再生はボス 5T ／ 本編 第4波 3T で最後まで通る。
- **本体**: 作戦マップ 1-1 の位置を「道 × 番号」から**盤（`MapGraph`）のマス Id** に持ち替えた。`Road` ／ `Cell` は派生値になり、位置を書くのは `Map11State.Place` の 1 箇所だけ。
- **回帰: 決定的な門 12 本はすべて行単位で完全一致**（`--map11-verify` 800 seed の 5 量は差 0.0pt）。通しの煙 4 本（flow ／ reform ／ recall ／ run）は**もとから起動ごとに乱数の seed で回る**ので素のままでは比べられず、seed を 0 に留めた一時の写しで前後を回して**一致**を確かめた（§4）。
- **グラフの門 3 項目は ○**。ただし (c) は指示書の「3 歩」ではなく **2 歩**（先頭の敵の初期位置は S1・盤の距離は 2）。
- **玩具の盤**（N1–S1 を横に繋いだ盤）× 貪欲 100 seed: **落ちた 0**・踏破 100%・横の辺を通った手 2.0 ／ 本（報告のみ）。
- **画面は動いていない**（同じビルドを 2 回撮っても 55 px ずれる動く札の範囲の中で、前後の差は 8 px）。
- **規則を書き換えた箇所は 0。ただし指示書のグラフの規則を 2 つ読み替えた**（§3-2）: ①「拠点は通り抜けられない」は**敵の拠点だけ**（味方の拠点を通さないと §5-2 (a) が成り立たない）②**敵の拠点にいる隊は入ってきた経路を覚える**（`Squad.Via`）——1-1 では北と南が敵の拠点で合流し、第437期はそこで `Road` を持ったまま「同じ道を戻る」ので、覚えないと第2拠点から別の道へ直行できてしまう（規則が変わる）。
- 次の段 A2（時計）: 時計は `Map11Session.StepTurn` の作戦ターンの終わり（`TurnRunning = false` の直後）で刻み、`Map11Main.RunUntilEvent` の「何かが起きるまで回す」をその時計に置き換える。

## 1. 段0

### 1-1. 入れ替え

| 項目 | 中身 |
|---|---|
| 新しい並び | `Presets.CurseDaunt437()`（前1 ワタ ／ 前3 ムド ／ 中央 ウツ ／ 後1 クビ ／ 後3 ネル）。`Presets.Versions` の「試遊・呪い」がこれを引く |
| 旧の並び | `Presets.CurseCenterNel()` は残した（`utsu437` ほかが直に読む）。第437期の一覧の写しは `Common.VersionsH437`（「試遊・呪い」だけを `CurseCenterNel` に差し戻し、末尾に「試遊・呪い 萎縮」を足し戻す） |
| 「試遊・呪い 萎縮」 | `Presets.Versions` から外した。`Common.VersionRows436` に名前は残した（鎖の終端 `VersionsH407` が外すものが無くなっただけで、鎖の終端の検査は通る） |
| `compare` ／ `docs/` | `compare` 0 セル・`dump` 差 0・`audit` ずれ 0。`docs/` の生成器（compare ／ chain ／ ablate ／ pulse ／ engage ／ watch ／ layout ／ reseat ／ dump ／ parry ／ grade2 ／ cross ／ derive ／ roster）は `Presets.Versions` を読まないので、残りも動かない |
| `sweep fast` | §1-5 |

### 1-2. 照合・固定・封印（R426）

照合は `digest_cache.ps1 full`（218 本・HEAD 47cb8d07 は DemoApp だけのコミットなので、BattleCore ／ BattleSim が同じ保存 9767a31f と比べた）。

- **一致 210 ／ 差 8**。差はどれも現役網（`SweepDiag.Active`）の外の `check` で、**判定はすべて ○ のまま**:
  - `flow347` ／ `burn349` ／ `db351` ／ `sh358` ／ `sh359` ／ `sh360` の `check`——`Presets.Versions` の名前を並べる行から「試遊・呪い 萎縮」が消えた
  - `sh400 check`——`Presets.Versions` の台数 38 → 37
  - `sh387 check`——試遊の台 6 台の前1 に駒を置いて札の働きを数える行（カリ 1158 → 1160・トモ 170 → 168・ノミ 12886 → 13470）。「試遊・呪い」の並びが替わったぶん
- **固定した器具: 0 本**（現役網の内で差が出た器具が無い。`shockdigest` 18 本と燃焼の `digest` はすべて一致）。
- **封印した器具（現役網の外）: 9 本**——上の 8 本と `utsu437 check`（照合の一覧の外・判定は ○ のまま、「試遊の台『試遊・呪い 萎縮』は置いていない（§4 の線を通らなかった）」と出るようになった——実際は「試遊・呪い」に入れ替えた）。起こすときは `Presets.Versions` を `Common.VersionsH437` に読み替える。
- 指示書が名指しした器具のうち `db351` ／ `sh378` ／ `sh381` ／ `sh383` ／ `sh384` の `check` は照合の一覧に入っている（`db351` だけが上の差・残りは一致）。一覧に入っていない軽い期の器具（`inv414` ／ `script415` ／ `mudo412` ／ `wata413` ／ `nomi425` ／ `chiri427` ／ `chiri428` ／ `boss435` ／ `utsu436`）はどれも現役網の外なので、回さずに封印の側に置く（起こすときは同じく `VersionsH437`）。

### 1-3. DemoApp

- `MudoCheck`（コミット済み・他の人の作業中ではない）は「試遊・呪い」を名前で引くが、**値を読まない**（ボスと第4波の再生が最後まで通り、再戦の台本が保たれるかだけ）。→ **固定も期待値の替えもしない。**
- `DebuffCheck`（未追跡・他の人の作業中）も「試遊・呪い」を名前で引く。触っていない。
- 単発の再生（頭なし・seed 0）: **ボス 5T 勝ち**（events 338）／ **本編 第4波 3T 勝ち**（events 206）。第437期の「試遊・呪い 萎縮」と同じ 5T ／ 3T。

### 1-4. コミット

段0 のコミット ＝ `BattleCore/Presets.cs`・`BattleSim/Common.cs`・`design/`（この報告・指示書・スクリーンショット・期の一行）。本体の回帰の基準はこの中身（段0 は DemoApp の作戦マップに 1 ビットも届かない——`Map11*` は `Presets.Compare` しか読まない）。

### 1-5. `sweep fast`

**合格**（22 本・異常終了 0 本・上限 0 本）。`Presets.Versions` に触ったので夜間の `sweep nightly` でも拾われる。

## 2. Phase 0

### 2-1. 位置を読んでいる箇所（§1-1・HEAD 47cb8d07・コメント行を除く）

`Road` ／ `Cell` ／ `RoadCells` ／ `RoadCount` ／ `PortalCell` ／ `StartCellOf` ／ `Dest(road, cell)` ／ `Dest.Deep` ／ `Dest.Portal` を読む行（153 行）:

| ファイル | 行 |
|---|--:|
| `Map11.cs` | 62 |
| `Map11Main.cs` | 54 |
| `Map11Orders.cs` | 17 |
| `Map11Time.cs` | 7 |
| `Map11Verify.cs` | 6 |
| `Map11Smoke.cs` ／ `Map11Phase173.cs` | 2 ／ 2 |
| `Map11Session.cs` ／ `Map11Portal.cs` ／ `Map11Phase172.cs` | 1 ／ 1（文字列）／ 1 |

| 分類 | 行 | 中身 |
|---|--:|---|
| 位置の読み替えで済む | 131 | `Road < 0`（拠点）・`Cell` の比較・`FoesOn(road)`・`Dest` の比較。派生値 `Road` ／ `Cell` と盤の `IndexOn` で読める |
| 画面の座標を読んでいる | 19 | `Map11Main` の `CellPos` ／ `NodePos` ／ `PortalPos` ／ `BasePos`・盤の帯・空きマスの板・敵の初期位置・行き先の札・隊 ／ 敵の板・矢印 |
| 2 本抜きの分子（`Home` ／ `Cleared`）に関わる | 3 | `Resolve` の `node.Def.Road == s.Home`・`Warp` と `PrepareIntercept` の `s.Home = …`（`Send` の `s.Home = road` は `Road` を読まないので数に入らない）。**4 行とも 1 ビットも動かしていない** |

書き手は 1 箇所だけ例外があった: `Map11Verify` の `sq.Road = alt`（時間なしの器具が隊を別の道へ回す）。`st.Send(s, alt)` に替えた（`Send` は道の上から呼ばれたら同じ番号のまま道だけ替える＝第437期の `sq.Road = alt` と同じ）。

### 2-2. 回帰の基準（§1-2）

段0 の中身の上で、本体に触る前に 16 本を保存した（指示書の 6 本 ＋ 敵の拠点の門 ／ Phase 0 の門 4 本 ／ 通しの煙 4 本）。`--map11-verify` ／ `--map11-time` ／ `--map11-portal` ／ `--map11-phase176` は `ok=False`（第178期から・5 量の値が合うかで読む）。

### 2-3. BattleSim（§1-3）

`BattleSim` は `Map11*` の型を 1 つも参照しない。名前が出るのは `Beni.cs`（`DemoApp/Map11.cs` を文字列で読み、名指しの控えに `UnitCatalog.Beni` があるかを見るだけ・この期の差で答えは変わらない）と、コメント 3 箇所。

### 2-4. 止まる条件（§1-4）

**道と番号でしか書けない規則は無かった。** ただし 1 つだけ、マス Id だけでは足りないものがあった——**敵の拠点にいる隊が「どの道にいることになっているか」**（§3-2 ②）。マスと一緒に `Via`（入ってきた経路）を持てばグラフのまま書けるので、止まらずに進めた。

## 3. 盤と読み替え

### 3-1. `MapGraph`（`DemoApp/MapGraph.cs`・Godot の型なし）

- `Cell { Id, Name, Kind, Neighbors, Lane, Pos }`・`Kind = Home ／ Open ／ Portal`・`Pos` は (x, z) の float の組。**味方の拠点の Id はどの盤でも 0**（`Dest.Home` が盤を引かずに作れるように）。
- `Lanes`（経路ごとのマス Id の列・拠点の隣 → 奥）・`FoeStarts`（最初からいる敵部隊の初期位置）・`Edges`（足した順・画面の帯はこの順に引く）。
- `IndexOn(lane, id)`（第437期の「マスの番号」・拠点 -1・敵の拠点は経路の長さ）・`At(lane, index)`・`TowardHome`（敵の 1 歩）・`Path` ／ `NextStep`（幅優先・辺の重さ 1）。
- `Build11(portalOn)`: 拠点 1 ＋ 北 3 ＋ 南 3 ＋（あれば）敵の拠点 1。`Pos` は第437期の `Map11Main` の座標と同じ値・マスの名前は第437期の `Map11Orders.Where` と同じ文字列。

### 3-2. 指示書のグラフの規則から読み替えた 2 つ

1. **「拠点（Home ／ Portal）は通り抜けられない」→ 敵の拠点だけ。** 味方の拠点も通れないと、1-1 では北から南への道が 1 本も無い（§5-2 (a) は「N2 から S1 への最短路が Home を通る」）。味方の拠点は「1 歩で入り、次の 1 歩で出る」＝第437期の「別の道ならいったん拠点まで戻る」そのもの。
2. **敵の拠点にいる隊は入ってきた経路を覚える（`Squad.Via`）。** 第437期は敵の拠点の隊も `Road` を持っていて、そこから出るときは同じ道を戻る（`Plan` の `s.Road != dest.Road → Back`）。行き先の札も「敵の拠点（北）／（南）」の 2 枚で、**どの道を通って向かうかを道が決める**。マス Id だけだと第2拠点から別の道へ直行できてしまう（規則が変わる）ので、`Path` は「敵の拠点からは `fromVia` の経路へ出る・敵の拠点へは `toVia` の経路から入る」。`Dest` も `(Id, Lane, Kind)` で、敵の拠点の `Lane` が向かう道。

### 3-3. `Map11State`

| 第437期 | 第438期 |
|---|---|
| `Squad.Road` ／ `Cell`（書ける） | `Squad.Pos` ／ `Via`（`Place` の 1 箇所が書く）・`Road` ／ `Cell` は派生値 |
| `Node.Cell`（書ける） | `Node.Pos`・`Cell` は派生値（`Def.Road` の経路の上の位置） |
| `Advance`: `Math.Min(Cell + 1, 先頭の敵のマス, MaxCell)` | 道の奥への `NextStep` の 1 歩・先頭の敵より先へは行かない（式は同じ。下の注） |
| `StepBack`: `Cell--` | 拠点への `NextStep` の 1 歩 |
| `AdvanceFoes`: `n.Cell--` ／ 詰まりは `x.Cell == sp.Cell - 1` | `Graph.TowardHome` ／ `x.Pos == target` |
| `SquadAt(road, cell)` ／ `FoeAt(road, cell)` | `SquadAt(cellId)` ／ `FoeAt(road, cellId)` |
| `PortalFoe` ／ `TryCapture` ／ `AtSecondBase`: `Cell >= PortalCell` | `Pos == Graph.Portal` |
| 拠点にいる: `Cell < 0` | `Pos == Graph.Home` |

- **`Advance` の 1 つの癖をそのまま写した**: 決着しなかった戦闘の後に敵が脇を抜けた（敵が自分より拠点寄りにいる）とき、第437期の `Math.Min` は**その敵のマスへ引き返して**当たる——敵が拠点に着いて迎撃が決着しないまま居座っていれば、道の上の隊は拠点のマスまで戻って（`Via` は道のまま）**迎撃ではなく道の戦闘として**当たる。規則を変えない期なので残した。A2（時計）で途中到着を作るときに見直す候補。
- `Map11Orders.Plan` は「別の道ならいったん拠点へ戻る」を書かず、最短路の次のマスから命令を引く（自分の経路の奥へなら `Advance`・拠点寄りへなら `Back`・**経路をまたぐ辺なら新しい `Move`**）。1-1 には経路をまたぐ辺が無いので `Move` は 1 度も出ない（玩具の盤のために置いた・`Map11State.MoveTo`）。
- 盤を差し替える口: `new Map11State(…, graph: …)`（省けば 1-1）。いまは玩具の盤だけが渡す——区画の定義 `Map11.Roads` は 1-1 のままなので、渡す盤は 1-1 と同じ経路の数・長さ・敵の初期位置を持つこと。

### 3-4. 画面（`Map11Main`）

- `BasePos` ／ `NodePos` ／ `CellPos` ／ `PortalPos` を `PosOf(cellId)`（盤の `Pos`）の 1 本にまとめた。
- 盤の帯は `Graph.Edges` から、行き先の札は盤の経路のマスから引く。`AtDest` は `Map11State.AtDest` の 1 箇所。
- 判定は 1 つも足していない。

## 4. 回帰（§5-1）

段0 の中身で保存した出力（前）と、この期の末尾の出力（後）。所要秒の行は比べない。

| 門 | 行 | 結果 |
|---|--:|---|
| `--map11-verify`（800 seed） | 18 | **完全一致**（5 量とも差 0.0pt） |
| `--map11-time` | 94 | **完全一致** |
| `--map11-portal` | 155 | **完全一致** |
| `--map11-phase176` | 74 | 一致（所要の見込みの 1 行だけ違う） |
| `--map11-portal-seed` | 41 | **完全一致** |
| `--map11-phase0` ／ `171` ／ `172` ／ `173` | 113 ／ 117 ／ 205 ／ 179 | **完全一致**・どれも `ok=True` |
| `--map11-time-smoke`（seed 0） | 27 | **完全一致** |
| `--map11-portal-smoke`（seed 固定） | 37 | **完全一致** |
| `--map11-draft-smoke`（seed 0） | — | **完全一致**（`ok=True`） |
| `--map11-flow-smoke` ／ `reform` ／ `recall` ／ `--map11-run-smoke` | — | 素のままでは比べられない（下）→ seed 0 に留めて**一致**（20 ／ 48 ／ 24 ／ 5 行） |

- **flow ／ reform ／ recall ／ run の 4 本は、起動のたびに `Map11Session.Reset()` が `new Random()` で seed を引く**（第169期から）。前後で seed が違う（694881 ／ 625377 など）ので戦闘の行が合わない。判定の行（`won=True cleared=4/4`・`RUN_SMOKE ok=True`）は前後とも同じ。
- そこで `Map11Session.Reset` に**一時の写し**（環境変数 `MAP11_SEED` があればそれを seed にする 1 行）を入れ、HEAD の `Map11*` を戻したビルド（前）とこの期のビルド（後）で seed 0 の 4 本を回した。**`MAP11_` の行がすべて一致**。写しは消した（コミットに入っていない）。
- `--map11-flow-smoke` と `--map11-run-smoke` が終わりに `FATAL ... csharp_script.cpp` で非 0 の終了コードを返すのは第169期からの既知（合否は `*_COMPLETE` の行）。

## 5. グラフの門（§5-2・`--map11-graph-smoke`・画面なし）

| 項目 | 結果 |
|---|---|
| 盤そのもの | 拠点なし 7 マス ／ あり 8 マス・経路 2 本 × 3・辺は両方向・敵の初期位置 ＝ `StartCellOf`（N1・N2 ／ S1・S2）——○ |
| (a) N2 → S1 | 北の道 2 戦目 → 1 戦目 → 手前 → **拠点** → 南の道 手前 → 1 戦目。敵の拠点を通らない——○ |
| (a') 敵の拠点（北から入った）→ S1 | 敵の拠点 → **北の道 2 戦目** → … → 拠点 → 南の道 1 戦目（入ってきた道へ出る）——○ |
| (b) | N0 の隊を N2 へ: 1 歩目 `Advance` → N0（拠点から）・2 歩目 `Advance` → **N1 で第二波・先遣と接敵**（`Prepare` が立つ）——○ |
| (c) | 南の先頭（第四波・先遣・S1）: S1 → S0 → **拠点**・**2 歩**で迎撃が立つ（`NextInterceptor` ≥ 0）——○。指示書の「3 歩」とは違う: 先頭の初期位置が S1 なので盤の距離は 2（経路の長さ 3 とは別） |

## 6. 玩具の盤（§5-3・報告のみ・採否に使わない）

`MapGraphSmoke.ToyCross`: 1-1（敵の拠点あり）に **N1 ⇔ S1** の辺を 1 本足しただけ。N2 → S1 は N2 → N1 → S1（拠点を通らない）。
頭なしの貪欲方針（各隊は盤の上でいちばん近い敵部隊のマスへ・敵が尽きたら敵の拠点へ・控えは隊が全滅してから・時間 K=1・敵の拠点 S=8 ／ R=3）× seed 0..99:

| 量 | 値 |
|---|---|
| 落ちた（例外） | **0** |
| 踏破（勝ち） | 100 ／ 100（100.0%） |
| 陥落 | 0 |
| 横の辺を通った手（`Move`） | 200（1 本あたり 2.0） |
| 作戦ターン | 平均 4.0 |

横の辺は使われる（`Move` の口が通る）。数字は線にしない。

## 7. 画面

`design/PHASE438_map11_before.png`（HEAD の `Map11*`）／ `design/PHASE438_map11_after.png`（この期）——`--map11-capture=…,default`・seed 0。
画素の差は 8 px（「既定の3隊で始める」の後の盤・左の道の入口の板のあたり）。**同じビルドを 2 回撮っても同じ場所が 55 px 違う**（動く札と矢印）ので、差はその揺れの中。組み直しの絵（`reform`）も 58 px（同じビルド 2 回で 70 px）。

## 8. 自己検査

| 項目 | 結果 |
|---|---|
| 回帰（§4） | 決定的な 12 本は完全一致・乱数の 4 本は seed 0 に留めて一致 |
| グラフの門（§5） | `MAP11_GRAPH_SMOKE_COMPLETE ok=True` |
| `BattleCore` | 段0 の `Presets.cs` だけ（本体のコミットは DemoApp だけ） |
| `docs/` | 差 0（`compare` 0 セル・`dump` 差 0・`audit` ずれ 0） |
| `sweep fast` | 合格（段0 の中身で・22 本・異常終了 0 ／ 上限 0） |

## 9. 次（A2: 時計の自動進行）

- 時計は `Map11Session.StepTurn` の作戦ターンの終わり（`TurnRunning = false` の直後）で刻み、`Map11Main.RunUntilEvent`（「▶ 進める」の何かが起きるまで回すループ）をその時計の刻みに置き換える。器具（`Map11Time` ／ `Map11Portal`）は `Map11State` を直に回すので時計を通らない。
- §3-3 の `Advance` の癖（脇を抜けた敵へ引き返す・拠点に居座る敵に道の戦闘で当たる）と、敵の拠点の `Via`（第2拠点から別の道へは直行できない）は、途中到着を作るときにポンが決める候補。
