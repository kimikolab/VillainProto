# 第236・237期 Codex 向けメモ —— ハネの「同じ列の弾き返し」と「着地の反動」

盤面は `design/PHASE236_SPRING_ROW.md` / `design/PHASE237_LANDING.md`。**このメモは再生（絵と音）の材料だけ**。
**台本の種類は1つだけ足した**——着地の反動の表示専用の見出し `BattleEventKind.Landing`（§2・追記）。同じ列の弾き返しは既存の `SpringGuard` のまま。Codex の担当ファイルは1つも触っていない。

規定のハネ（`UnitCatalog.Hane`）は今 `SpringGuard` ＋ `SpringStay` ＋ **`SpringRow`（第236期）** ＋ **`Landing`（第237期）** を持つ。

## 1. 同じ列の弾き返し（第236期・`SpringRow`）

- **台本は今までと同じ**: `SpringGuard`（`ActorId` ＝ ハネ ／ `TargetId` ＝ 殴られた味方 ／ `PartnerId` ＝ 殴った敵 ／ `Slot` ＝ 弾く先の席）→ 直後に `Spring`。
- **変わったのは `TargetId` の位置**: これまでは必ずハネの隣だったが、**同じ列の隣接しない味方**も来る。X 字で増えるのは **前1 ⇔ 前3** と **後1 ⇔ 後3** の2組だけ（角どうしは隣接しない）。
- 今の再生: `ShowMovementCue` の `SpringGuard` は `Flow(actor, target, Bounce, 0.16)`、掌の構えは `BeginPalmStrike(…, guarded)` の `windup = (ally − FxPoint).LimitLength(0.85)`。
  **距離が隣の倍近くになるので、列の反対端へ「横っ飛び」する絵が欲しい**（今は 0.85 で頭打ちになり、味方の方へ少し寄るだけに見える）。
  - 案: `guarded` がハネの隣でない（同じ列の反対端）ときだけ、Flow の曲がりを大きく・秒を少し長くする／掌の立ち絵 `hane_spring_guard` の前に短い横移動の残像。
  - 判定材料は再生側で取れる: `FormationRules.AreAdjacent(ハネの席, 味方の席)` が偽なら同じ列の分。
- ハネは**動かない**（`SpringStay`）。入れ替わりの `Move` は来ない（第232期から同じ）。
- 頻度: 九体の波で隣の分の弾き返しが 1戦 0.3〜0.9 回（本編の5体の波では 0.0〜0.3 回・増えた判定の大半は「経路の最後尾」で弾けない）。

## 2. 着地の反動（第237期・`Landing`）

- **ハネが動かされるたび**（理由を問わない: 追い風で踏み込んだ／前に踏み込まれて下がった／勢い余っての入れ替え／バサの入れ替え など）、
  **ハネの隣の味方1体が、その味方の隣の別の味方と入れ替わる**（HP5割未満の味方は動かさない・1ターンに 1 ＋ 敵の乱れの段 回まで）。
- **見出し `BattleEventKind.Landing`（表示専用・末尾に足した・既存の番号は動いていない）**:

  | 欄 | 中身 |
  |---|---|
  | `ActorId` | ハネ |
  | `TargetId` | 動かされた隣の味方（B） |
  | `PartnerId` | 入れ替わった相手（C・B の隣） |
  | `Slot` | その戦で何回目か（1 始まり） |
  | `Team` | ハネの陣営 |

- 見出しの後に **`Move` が2件**（`ActorId` ＝ ハネ）: **1件目が B、2件目が C**。
  **ただし2件は連続するとは限らない**——1件目の `Move` に反応した出来事が間に挟まる。実測（`spring2 check237`・2,280 回）で間に挟まったもの:
  `Whet` 2571 ・ **`Damage` 2143** ・ `Sealed` 992 ・ **`Attack` 881** ・ `Overflow` 660 ・ `Highlight` 606 ・ **`Death` 463** ・ `Squall` 386 ・ `Stagger` 349 ・ `KillImpact` 258 ・
  `EvadeStage` 213 ・ `DecoyShow` 180 ・ `MoveShot` 124 ・ `DisarrayStage` 70 ・ `Heal` 66 ・ `Confused` 41 ・ `Disarray` 41 ・ `StaggerBreach` 36 ・ `ShioStage` 8。
  **バサの突風・セロの移動の追撃・ヨミの軋み（割り込み攻撃）が、B が動いた瞬間に1発撃ってから C が動く**。
  再生で B と C を同時に動かすなら、2件目の `Move` まで待つか、見出しで B・C を先に掴んでおくこと（`PartnerId` はそのため）。
- 据えた足（バン）で空振りしたときは見出しだけで `Move` は続かない（この台では起きていない）。
- **並びの位置**: ハネを動かした一連の入れ替えが**全部終わった直後**（追い風の後）。実戦で多い形:

  ```
  Blast / Spring …         （ハネの吹っ飛ばし・弾き返し）
  Move 敵 …                （突き崩し）
  Tailwind  →  Move 味方A / Move ハネ     （追い風でハネが踏み込む、または味方がハネの前へ踏み込んでハネが下がる）
  Landing ハネ（B, C）
  Move B  …（反応: Whet / Squall→Attack→Damage など）…  Move C
  （その後に「吹っ飛ばされて転んだ」などの続き）
  ```

  ログの文字列は「着地の反動: 突き返しのハネ の着地で B が C と入れ替わった」。
- 頻度が高い: **1戦 2〜9 回**（九/新兵 400/300 で 4.0、第四波 400/300 で 9.0）。**1回 0.2 秒以内・再生を止めない**くらいがよさそう。
- **気になる点**: `MovementPresentation` の保留（`Tailwind` の `pending`）が、直後の着地の反動の `Move` を追い風の移動として取り込まないか——
  追い風で動いた味方が同じ瞬間に着地の反動でもう一度動くことがある（ログ例: 「ドルガ が ハネ の前へ踏み込んだ」→「ドルガ が ガルド と入れ替わった」）。
  見出し `Landing` を `cue` として扱えば区別できる。
- 絵の案（`design/art/hane/` に未コミットの素材あり: `hane-ally-bump-*`・`hane-oshikura-*`）:
  - ハネの足元に着地の土煙（小さな輪）→ B がよろけて（`ally-bump-accidental`）、C と押しくらまんじゅうのように入れ替わる（`oshikura-bump`）。
  - ハネが振り返って謝る差分（`ally-bump-apology`）は、1戦に何度も出るので**毎回ではなく初回（`Slot == 1`）だけ**などにすると煩くない。
  - 音は既存の `MovementSound.Collision`（ハネの着地）を軽く。

## 3. Claude 側で足せるもの（必要なら言ってください）

- ~~表示専用の見出し `BattleEventKind.Landing`~~ → **足した**（§2。盤面は動かない・`compare` 305 セル不動・`shockdigest` は指紋から外す）。
- 同じく `SpringGuard` に「同じ列の分か」の印（例: `Text = "row"`）を載せることもできる（再生側で隣接を引き直さなくて済む）。
