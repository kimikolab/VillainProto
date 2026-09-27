# Codex への依頼 —— 検証の波（9体）を DemoApp で選べるようにする（第221期）

**BattleCore 側は済んでいる。DemoApp の中だけの作業。** 盤面の規則・数値には触らない。

## 1. BattleCore が公開したもの

| もの | 場所 | 中身 |
|---|---|---|
| `EnemyWave` | `BattleCore/Models.cs` | 敵専用の9枠の波。`Occupied()` が `(Seat, Def)` を席の昇順で返す（席 0〜8 ＝ `FormationRules.SeatNames`） |
| `BattleEngine.MaterializeEnemy(EnemyWave wave, EnemyScaleRule scale)` | `BattleCore/BattleEngine.cs` | 9体を**そのままの席**で `List<UnitState>` にする（必ず敵陣営・陣形は X 字）。倍率を省くと採用値（115/115） |
| `EnemyCatalog.TestStages` | `BattleCore/UnitCatalog.cs` | `TestStage(string Name, EnemyWave Enemy)` の一覧（3本） |

`TestStages` の中身:

| 波 | 敵 | 席 |
|---|---|---|
| 検証・九 / 新兵 | 討伐隊の新兵 × 9 | 9マス全部 |
| 検証・九 / 農兵 | 駆り出された農兵 × 9 | 9マス全部 |
| 検証・五 / 新兵（対照） | 討伐隊の新兵 × 5 | X 字（0〜4） |

## 2. やってほしいこと

1. **波の選択（`_stagePicker`・`Main.cs` の 513 行あたり）に検証の波を足す**。本編の5波（`EnemyCatalog.Stages`）の後ろに、見出し（例「── 検証 ──」）を挟んで3本
   - いま `Stages[_stagePicker.Selected]` を読んでいる所（1008・1021・2356 行あたり）は、選ばれたのが検証の波ならそちらを引くこと（添字で落とさない）
2. **出撃の経路**: 検証の波を選んだら、`BattleEngine.MaterializeEnemy(testStage.Enemy, scale)` で敵を作って `EnterBattle(players, enemies, seed, stageIndex, title)` に渡す
   - `EnterBattle` は既にリストを受け取るので、変えるのは敵を作る1行だけのはず
   - 倍率は本編の単発戦と同じもの（`Materialize(enemyFormation, EnemyTeam, scale)` に渡しているのと同じ `scale`）
   - `stageIndex` は背景を選ぶのに使われている（`stageIndex == 3` で城塞）。検証の波は**本編の番号と重ならない値**にするか、背景は第一波のものにする
3. **起動引数**: `--demo-test-stage=<0始まり>`（例 `--demo-test-stage=0` で 検証・九 / 新兵）を足し、`--demo-quit` の頭なしの再生で最後まで流れることを確かめる（`DEMO_SMOKE_COMPLETE`）
4. **作戦マップ・会戦には入れない**（本編の波だけ）

## 3. 描画について（第221期 Q0-8 の調査・BattleCore からは直していない）

- `BattlefieldView3D.PawnPosition(team, slot)` は **9席すべてに座標がある**ので、9体は描けるはず。**ただし 9 体が一度に立つのは初めて**なので、重なり・はみ出し・名札のかぶりを目で確かめてほしい
- 席 5〜8（○中1・○中3・○前2・○後2）の名札は「○」が付く（`SeatNames`）。**召喚の印に見える**ので、検証の波では気になるなら表示だけ変えてよい（`SeatNames` 自体は変えないこと——診断が読んでいる）
- 大きさ 5 の配列は味方の編成画面（`BattlefieldView._slots`）と演出の粒の数だけで、敵の席は無い

## 4. 触らないもの

- `BattleCore/` と `BattleSim/`
- 本編の5波の選択・出撃の経路（検証の波を選んでいないときは今と1ビットも違わないこと）
- 作戦マップ（`Map11*`）——`--map11-phase0` / `171` / `172` / `173` が `ok=True` のままであること

## 5. 見どころ（第221期の測定から）

- 雷の台（ベニ・ミオ・カタ ＋ 2枠）は **9体の新兵を1本の感電の連鎖でほぼ全部弾く**（連鎖の平均の大きさ 3.5 → 7.3 体）。9体が一度に弾ける絵になる
- 澱みの爆発も1本が長くなる（平均 2.2 → 3.2 体）
- 本編の 61 行では 9 体の新兵は平均 86.0%（5体なら 99.5%）——**全員生存は 68.8% → 29.8% に落ちる**。手数の多い敵の絵
