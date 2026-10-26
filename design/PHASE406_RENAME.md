# 第406期 報告 —— 【軽い期・小】改名: 彼岸花のハギ ／ 人呑みのセッキ（規則は変えない）

指示書: `PHASE406_RENAME_HAGI_SEKKI_SPEC.md`（ポンの判断 2026-10-26）。前期は `design/PHASE405_HAGI_SETTLE.md`。改名の作法は第285期（止めのトメ → 見境なしのミサ）に合わせた。

| 駒 | 旧 | 新 | フレーバー |
|---|---|---|---|
| ハギ（`Id = "hagi"`）| 追い打ちのハギ | **彼岸花のハギ** | 彼岸花の咲くころ、大鎌は赤く染まる。刈り取った命は、迷わず向こう岸へ送られる。 |
| セッキ（`Id = "sekki"`）| 包みのセッキ | **人呑みのセッキ** | 人を呑む化け物と恐れられ、追われてきた。呑まれた者は、みな無傷で吐き出されていたというのに。 |

## 1. 決めたこと

- **規定の駒は旧の作り口から名前を引き継いでいた**。`UnitCatalog.Hagi` は `HagiOn404` → `HagiOn388`（`Name = HagiH389.Name`・フレーバーも作り口の固定の文字列）、`UnitCatalog.Sekki` は `SekkiOn397`（`Name = "包みのセッキ"`）で作られていた。
  作り口に省略できる引数 `name` ／ `flavor` を足し（既定は今の値）、**規定の2つの定義だけがそれを渡す**。旧の定義（`HagiH389`〜`HagiH404`・`SekkiH396`〜`SekkiH398`）と版の駒（`HagiReapR50` ／ `HagiBigA405` ／ `SekkiK1` など）は旧名と旧フレーバーのまま。
  `HagiBigA405`（「版・大物 ハギ」に置く 規定 ＋ A）も版の駒なので旧名のまま。
- 札 ／ 数値 ／ プラス ／ マイナスの文面は1文字も変えていない（`docs/units.md` の差は名前とフレーバーだけ・下 §2）。
- `Common.UnitRenames` に `("追い打ちのハギ", "彼岸花のハギ")`・`("包みのセッキ", "人呑みのセッキ")` を足した。あわせて **`Common.SameUnitName(a, b)`**（旧名を新名に読み替えて比べる）を足した——規定と旧 ／ 版の名前を比べる自己検査がこれを使う（§3）。
- 由来は規定の定義のコメントに短く残した（萩とお彼岸 ／ 呑み込んで守っているという誤解の名）。
- DemoApp: 名前は台本の `UnitDef.Name` から出る。**名前を直書きしている箇所は 0**（`DemoApp` ／ `GodotApp` を grep・`追い打ち` が出るのは札の名前を書いたコメントの 4 箇所だけ）。DemoApp には手を入れていない。
- 試遊の台の名前（「試遊・ハギ 台B」「試遊・手番 包み」など）は替えていない。`design/CODEX_BRIEF_*.md` は触っていない。

## 2. 動いたファイルと `compare` 0 件

- **`compare` 全 66 行 × 5 波 × seed 0..199 が `docs/balance.md` と 0 件**（`balance.md` は再生成して差分 0）。全66行 / 主判定19行 / 情報セル 28 / 10 は第405期のまま。
- `docs/` は PowerShell から全部再生成した（`roster audit` を含む）。動いたのは 9 ファイル。どれも**旧名を新名に置き換えると HEAD と一致**し（機械で照合）、残る差は下の行だけ:

| ファイル | 動いた行 |
|---|---|
| `units.md` | 名前（駒の表 2 行・札の保持者の表のハギ ／ セッキの行）＋ フレーバー 2 行 |
| `roster_audit.md` | 名前 2 行 ＋ 末尾の所要秒数 |
| `ablation.md` ／ `pulse.md` ／ `harm.md` ／ `layout.md` ／ `reseat.md` ／ `stock.md` ／ `watch.md` | 名前だけ |

  `balance.md` ／ `quality.md` ／ `elite.md` ／ `chain.md` ／ `engage.md` ／ `crossing.md` は動かない。`rules.md` は `CLAUDE.md` を書いた後に最後に回した（§4）。
- **過去の器具の照合**（`tools/digest_cache.ps1 light -Units hagi,sekki`・HEAD 0346946d の保存は無いが、BattleCore ／ BattleSim が同じ 1ffc2fa7 の保存と比べた）: 124 本のうち最初は 一致 121 ／ 差 3（`sh388 check` ／ `sh389 check` ／ `sh390 check`・どれも「`docs/units.md` のハギの行」を名前で引く検査が × になった）。
  §3 で直した後の出力は**保存の指紋と 1 バイトも違わず**、work/ に置き直して `recheck` → **一致 124 ／ 差 0**。som310〜320 の check は exit 1 だが保存の指紋と一致（前からそう）。
- 自己検査: `sh405` ／ `sh404` ／ `sh399` ／ `sh388` ／ `sh389` ／ `sh390` ／ `elite` ／ `boss283` ／ `checkup` が ○。`audit` ずれ 0。`CLAUDE.md` は「改名の対応」の行に今回の2つを足し、「最後に動かした期」を差し替えた（47.7 KB・280 行）。
- DemoApp: ビルド 0 エラー。map11 の門（phase0 ／ 171 ／ 172 ／ 173）`ok=True`。単発の再生（ボス ＝ `--demo-playtest-stage=0`・seed 0）は「試遊・ハギ 台B」3T 勝ち ／「試遊・手番 包み」5T 勝ちで `DEMO_SMOKE_COMPLETE`。台本の席の行（`DEMO_SEATS`）に**彼岸花のハギ** ／ **人呑みのセッキ**が出て、旧名は 0 回。
- `sweep fast`: **合格**（異常終了 0 本・上限 0 本）。駒の名前が動いたので、名前を出力に含む器具は今夜の `sweep nightly` でも差として拾われる（中身は名前だけのはず）。

## 3. 直した器具

名前で駒を引いていて、改名で落ちた ／ 空欄になったもの。どれも `Common.UnitRenames` を通して旧名 ／ 新名のどちらでも引けるようにした（過去の報告書は書き換えない）。

| 器具 | 何が起きたか | 直し方 |
|---|---|---|
| `roster audit`（`Roster.cs`）| 「枠の値段」の列（`design/PHASE172_REFORM.md` 部C を名前で引く）でハギの −5.3pt が空欄になった | `SeatPriceOf` で旧名の行も引く（辞書には足さない——足すと「7 枚ぶん」が 8 になるため）|
| `sh405 check` | 段0 の「名前は旧と同じ」が × | `SameUnitName(hagi.Name, old.Name)` |
| `sh399 check` | 段0 の「規定のセッキ ＝ K1 の札と名前とフレーバー」が × | 名前は `SameUnitName`・フレーバーは名前が替わったときだけ差を許す |
| `sh388 check` ／ `sh389 check` | `docs/units.md` のハギの行を `UnitCatalog.Hagi.Name` で引いていて、過去の表（旧名）の行が見つからない | `Common.UnitRowOf`（旧名 ／ 新名のどちらの行でも引く）|
| `sh390 check` | 今の `docs/units.md` の行を旧の `HagiH391.Name` で引いていて見つからない | `UnitRowOf` |
| `sh389` の版の検査 ／ `lit`（`Lit.cs` の Q4）| 版の名前（旧名）を規定の名前と比べている ／ 灯の対象を規定の名前の鍵で数えている | `SameUnitName`（`lit` は第113期の器具で照合の一覧にも `sweep fast` にも入っていない。ビルドは通るが回していない）|

## 4. コミット

自分の変更パスだけをコミットした（BattleCore ／ BattleSim ／ `docs/` ／ `CLAUDE.md` ／ この報告 ／ `HISTORY_PHASES.md` ／ `PHASE_INDEX.md`）。作業ツリーに残っている第405期の Codex の演出の変更（DemoApp ／ `design/DEMOAPP_HISTORY.md` ／ `CONTRIBUTING.md` など）は混ぜずにそのまま置いてある。
