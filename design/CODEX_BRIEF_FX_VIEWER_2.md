# 発注メモ —— 演出ビューア 第2段: 駒カタログ（全駒の動きを登録なしで見る）

置き場所: `design/CODEX_BRIEF_FX_VIEWER_2.md`。第1段（`design/CODEX_BRIEF_FX_VIEWER.md`）の続き。触るのは `DemoApp` だけ。

## 0. 何を足すか

第1段のビューアは、Cue を**手で登録した演出だけ**が一覧に出る。今は7本。
残り40本の Check を順次移しても「登録した駒しか見られない」のは変わらないので、**登録なしで全駒の動きが見られる段**を足す。

やり方: `UnitCatalog.All`（＋敵は `EnemyCatalog.Stages` の駒）を起動時に走査し、**駒ごとに Cue を自動生成**して一覧の「駒カタログ」の節に並べる。Codex が駒ごとに書くものは無い。

## 1. 仕上がり

一覧の構成（上から）:

```
▸ 駒カタログ            ← 新規・自動生成
    ▸ 禍導のカタ
        待機
        攻撃（単体）
        被弾
        撃破
        実戦: 手番と特殊（第1波）
        実戦: 手番と特殊（第2波）
    ▸ 毒喰らいのベニ
        …
    ▸ 敵: 巡礼騎士
        …
▸ 雷                   ← 第1段の手組み Cue（そのまま）
▸ 移動
…
▸ 台本（実戦）
```

「駒: ▼」の絞り込みで1駒に絞ると、その駒の節だけが残る。

## 2. 自動生成する Cue

### 2-1. 基本4本（手組み・雛形から生成）

`FxCatalog.Generated.cs` に雛形を置く。駒 `d`（`UnitDef`）に対して:

- 盤面: `d` を席2（中央）に味方、相手は `knight` を席0・2・4に3体（第1段の `ThunderCheck.Preview` と同じ並び）。`DemoOpening` は `d.Id, d.Name, d.MaxHp, d.Attack, d.Pattern, d.Advances, d.Traits` から組む。敵の駒を見るときは `Team` を入れ替える（左右反転と同じ扱い）
- **待機**: イベント列は空。`BeginBattle` の後、待機ループ（`IdleLoopCheck` の対象）を 3 秒見せる
- **攻撃**: `Attack`（`ActorId=d`・`TargetId=`席2の knight）→ `Damage`。`d.Pattern` が `Sweep` なら両隣にも `Damage`、`Pierce` ならレーン順に `Damage`。`Amount` は `d.Attack`
- **被弾**: knight の `Attack`（`TargetId=d`）→ `Damage`（`Amount=10`）
- **撃破**: 被弾 → `Damage` で HP を 0 に → `Death`
- 既に手組み Cue が登録されている駒（例: カタ）でも、基本4本は出す（基本は基本、演出は演出で別物）

イベントの組み方で分からない所は `Main.ApplyEvent` の `switch` を読んで、**その種類が読むフィールドだけ**埋める（`HpAfter` を読んでいるなら HpAfter を正しく積む）。

### 2-2. 実戦2本（Live・自動生成）

- 編成: `Presets.Playtest` → `Presets.Versions` → `Presets.Compare` の順に **`d` を含む行**を探し、最初の1行を使う。無ければ `Formation.Build` で `d` を中央、残りを `UnitCatalog.All` の先頭から穴埋め（`d` と同じ Id は避ける）
- 波: 第1波（stage 0）・第2波（stage 1）。seed 0
- `Highlight`: `ActorId == d` または `TargetId == d` のイベントで、種類が `TurnStart` `StatSnapshot` `StatusSnapshot` 以外
- 敵の駒: その駒が出る最初の波を `EnemyCatalog.Stages` から探し、`Presets.Playtest` の先頭の行で回す。`Highlight` は同じ規則

これで、**手組みの雛形に無い特殊行動（手番の技・マイナス特性の発動）も、エンジンが出した実イベントで見られる**。特殊が1つも出ない組み合わせなら「見せ場 0」と一覧に添える（編成を変えるのは次の段）。

### 2-3. 生成の置き場

- `FxCatalog.Generated.cs`: `static IEnumerable<FxCue> ForUnit(UnitDef d, bool enemy)` と、全駒を回す `static IEnumerable<FxCue> AllUnits()`
- `FxCatalog.All` は「手組み → 生成」の順に結合。一覧の表示順は §1 のとおり駒カタログを先頭に
- 生成 Cue の `Id` は `unit.<id>.idle` `unit.<id>.attack` `unit.<id>.hit` `unit.<id>.death` `unit.<id>.live.<stage>`

## 3. ビューアの小さな追加

- 一覧の節を折りたためるようにする（駒カタログは 50 駒 × 6 本で 300 行になるので、既定は駒の節を閉じておく）
- 一覧の各行の右に、Live の Cue は「見せ場 n」を添える
- 「次の見せ場」を押し切って終端に来たら、繰り返し ON のときは先頭へ戻す（今の挙動の確認）

## 4. 検証

- ビルド: 警告0・エラー0
- `--fx-smoke` が生成 Cue を含めて全数一巡し、`FX_VIEWER_SMOKE_OK` と件数（手組み n・生成 m）を出す。生成で例外が出た駒があれば **その駒だけ飛ばして** 駒 Id を `FX_VIEWER_SMOKE_SKIP <id> <理由>` で出し、全体は止めない
- `UnitCatalog.All` の全駒について基本4本が例外なく終端に到達する
- 任意の3駒（カタ・バサ・ゴルム）で「実戦: 手番と特殊」の見せ場数が 1 以上
- 敵の駒 2 種（巡礼騎士ともう1種）で基本4本が表示される

## 5. 触らないもの

- `BattleCore` `Presets` `EnemyCatalog` には触らない（読むだけ）
- 第1段の手組み Cue と Check は変えない
- 再生は引き続き `Main.ApplyEvent` のみ
