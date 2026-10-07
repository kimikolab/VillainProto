# 第291期 Codex 向けメモ —— 感電軸・標軸の台本（試遊の準備）

盤面は `design/PHASE291_PLAYTEST_PREP.md`。**このメモは再生（絵と音）の材料だけ**で、BattleCore / BattleSim 側は台本を出すところまでで止めてある。
**Codex の担当ファイル（DemoApp の演出）は1つも触っていない**（`Main.cs` は波の選択とプリセットの連結だけ・`NineWaveCheck.cs` は項目数の門を試遊の波に合わせただけ——報告 §5）。

- **足した出来事はすべて表示専用**: 盤面に触らず乱数を引かない。verbose の有無で勝敗・決着T・全員の HP が同じ（`playtest291 check` (d)・1,580 戦）。
- **保持者がいない戦では1件も出ない**（`playtest291 check` (e)）。
- 演出の意図のメモ `design/CODEX_BRIEF_SHOCK_MARK.md` は**本期のリポジトリには届いていない**（指示書 §5 は「一緒に渡す」）。届いたらそのまま置き、細部はこのメモを正とする。

## 1. 新しい出来事（`BattleEventKind` の末尾に4つ・既存の番号は動いていない）

| 種類 | いつ | `Text` | `ActorId` | `TargetId` | `Amount` | `Slot` | ほか |
|---|---|---|---|---|---|---|---|
| `ShockGauge` | シガの蓄電・割り込み・怖気、カタの雷雲 | `ShockGaugeLabels` の札（下の表） | 札による | 札による | 札による | 札による | `PartnerId` ／ `StatusRemaining`（札による）・`Team` ＝ `TargetId` の陣営・`HpAfter` ＝ `TargetId` の HP |
| `Feather` | ミサの羽の増減・連射の見出し・1発ごと | `FeatherLabels` の札（下の表） | ミサ（増えた ＝ 標の書き手） | 札による | 札による | 札による | `PartnerId` ／ `StatusRemaining`（札による） |
| `Scar` | 炸裂で削った分だけ最大HPが縮んだ（爪痕） | — | ミサ | 刻まれた敵 | **縮んだ量** | **新しい最大HP** | `HpAfter` ＝ そのときの HP。直前に炸裂の `Damage` |
| `MarkLayer` | 敵に付いた標の層が変わった（0 → 1 も） | — | 書き手（ソラ・ザン ほか） | 標の付いた敵 | **新しい層** | 前の層 | **炸裂の保持者（ミサ）がいる戦だけ**。消える瞬間・ヒサの付け替えは出ない |

### 1-1. `ShockGaugeLabels`

| 札 | いつ | `ActorId` | `TargetId` | `Amount` | `Slot` | ほか |
|---|---|---|---|---|---|---|
| `蓄電・増えた` | シガに感電が新しく付いた（直前にその `StatusGain` shock） | 感電を付けた駒 | シガ | 新しい蓄電（1〜4） | 前の蓄電 | 上限 4 のときは出ない |
| `蓄電・使った` | 割り込みの鞭を振り終えた（SI-b・規定） | シガ | シガ | 新しい蓄電 | 前の蓄電 | |
| `蓄電・使い果たした` | 雷霆の後（SG-b ／ SI-a・**規定のシガには無い**） | シガ | シガ | 0 | 前の蓄電 | |
| `割り込み` | 敵の感電が弾けた連鎖の直後に割り込む（**見出し**） | シガ | 鞭の主目標 | 振る前の蓄電 | その連鎖で弾けた敵の数 | `PartnerId` ＝ その連鎖で最初に弾けた敵（合図）。直後に `Highlight`「…鞭が割り込む」→ シガの `Attack`（**`Reaction = true`**・的 ＝ `TargetId`）→ `Damage` … → `蓄電・使った` |
| `怖気` | シガが動ける主目標に怖気づいた（次の手番を失う） | シガ | シガ | 0 | 0 | `PartnerId` ＝ 動けた主目標。直前にシガの鞭の `Attack` ／ `Damage` |
| `雷雲` | 雷雲が湧いた（敵の感電が弾けた連鎖の後） | その連鎖で最初に弾けた敵 | カタ | 新しい雷雲（上限 8） | 前の雷雲 | `StatusRemaining` ＝ その連鎖で弾けた敵の数 |
| `雷雲の雷` | 雷雲を乗せて雷を落とす（カタの手番・雷雲 ≧ 1） | カタ | カタ | その手番の雷雲 | 0 | 直前に `Skill`「雷を落とした」、直後に `Thunder`（`Amount` は雷雲を足した後の1発 ＝ 攻 × (1 ＋ 種類 ＋ 雷雲)） |

- 規定のカタ（KR-b）は**雷を落としても雷雲が減らない**（上限 8）。雷雲の絵は「育ち続ける」でよい。減る札（KR-a）は規定に無い。
- 規定のシガ（SI-b）の割り込みでは**怖気は出ない**（割り込みの鞭は怖気づかない）。怖気が出るのは手番の鞭だけ。

### 1-2. `FeatherLabels`（羽の枚数 ＝ 1 ＋ 増えた分）

| 札 | いつ | `ActorId` | `TargetId` | `Amount` | `Slot` | ほか |
|---|---|---|---|---|---|---|
| `増えた` | 味方が敵に標を書いた（`LayerMark` の頭・積み増しでも） | 標の書き手 | ミサ | 新しい枚数 | 0 | `PartnerId` ＝ 標を書かれた敵。続いて `MarkLayer` |
| `連射` | ミサの手番の頭（**見出し**） | ミサ | ミサ | 発数（＝ そのときの枚数） | 0 | |
| `追う` | 標を追う1発 | ミサ | — | 発数 | **何発目（1 始まり）** | 直後に単体の `Attack`（的 ＝ 標の段で選ばれた敵）と `Damage` |
| `流れた` | 前の1発が的を倒し、次の標へ流れた1発 | ミサ | — | 発数 | 何発目 | `PartnerId` ＝ 倒れた前の的。中身は「追う」と同じ |
| `乱射` | 標が尽きて（または初めから無く）敵味方構わず飛ぶ1発 | ミサ | — | 発数 | 何発目 | `StatusRemaining` ＝ その連射で何発目の乱射か（**1 ＝ 標が尽きた瞬間**）。直後に `Damage` だけ（的は乱数・味方なら `FriendlyFire`） |
| `失った` | 撃ち出した羽が戻らなかった（M-b・乱射した数だけ・下限 1） | ミサ | ミサ | 新しい枚数 | 失った枚数 | 連射の最後の1発の後 |

## 2. 既存の出来事に足した印（表示専用）

| 出来事 | 印 | 中身 |
|---|---|---|
| `StatusGain`（`Text = "shock"`・書き手 トウ） | **`PowderRoute`**（新しい欄・`Main` ／ `Spread` ／ `Leak`） | `Main` ＝ 殴った主目標 ／ `Spread` ＝ 主目標の隣の敵（**`SpreadFromId` ＝ 主目標**）／ `Leak` ＝ トウの隣の味方（**`FriendlyFire = true`**）。並びは 主目標 → 隣の敵（席の順）→ 漏れ。トウ以外の書き手では `null` |
| `Discharge`（`SourceTrait = Thread`・第290期からある糸の放電） | `PartnerId` ／ `Text` | `ActorId` ＝ クグ ／ `TargetId` ＝ 糸の先の敵。**`PartnerId` ＝ 電気を持ち込んだ隣の味方**（② 隣の放電を移した）／ `null` ＝ クグ自身の弾け（①）。**`Text = "ほどけ"`（`ThreadLabels.Release`）＝ 殴られてほどける一撃の中**（切れる直前の糸を電気が駆け抜ける）。直後に `Damage`（出どころ ＝ クグ）、規定（KG-b）では生きていて感電していなかった敵に `StatusGain` shock（`ActorId` ＝ クグ） |

- 標の層: `MarkLayer` が出る戦では、ザンの初回の標は `MarkLayer`（0 → 1）と既存の `StatusGain`（`Text = "marked"`）の**両方**が出る（同じ瞬間）。ソラの標は `MarkLayer` だけ。
- 爪痕: 最大HP のバーを縮めるのは `Scar` の `Slot`（新しい最大HP）。`StatSnapshot` には最大HP は載っていない。

## 3. 欲しい絵 → 台本（指示書 §4-2）

| 駒・機構 | 欲しい絵 | 使う台本 |
|---|---|---|
| シガ・蓄電 | 纏う電気が蓄電 0〜4 で段階的に増える | `ShockGauge`「蓄電・増えた」／「蓄電・使った」の `Amount`（新しい値）。戦の頭は 0 |
| シガ・割り込み | 感電が弾けた直後、シガが割り込んで鞭を振るう | `ShockGauge`「割り込み」（合図 ＝ `PartnerId`・主目標 ＝ `TargetId`）→ `Attack`（`Reaction = true`） |
| シガ・怖気 | 怖気づく | `ShockGauge`「怖気」（**本期に足した**——それまでは痺れのカウンタとログだけで台本に無かった） |
| カタ・雷雲 | 雷雲が育ち、落雷が太く大きくなる | `ShockGauge`「雷雲」（育つ瞬間）・「雷雲の雷」（その雷の雷雲）・`Thunder` の `Amount` |
| トウ・粉 | 主目標と隣の敵にふわっと舞う／隣の味方に漏れる | 既存の `StatusGain` shock ＋ `PowderRoute` |
| クグ・糸 | 組み付いた糸を電気が走る／ほどける瞬間に電気が駆け抜ける | 既存の `Discharge`（`SourceTrait = Thread`）＋ `PartnerId` ／ `Text = "ほどけ"`。組み付き自体は既存の `Skill`「組み付いている」と組み付きの `StatusGain` |
| ミサ・羽 | 羽の在庫が浮く／標を追って1発ずつ／倒したら次の標へ／尽きると乱射／戻らない | `Feather` の6札 |
| ミサ・爪痕 | 削った分だけ最大HPが恒久的に縮む | `Scar` |
| 標の層 | 敵に付いた標の層（ロックオンの数）が見える | `MarkLayer`（`Amount` ＝ 新しい層） |

## 4. 試遊プリセットで実際に出る並び（1プリセット1本・`playtest291 memo <行名の一部> <boss|guard|bat> <seed>` で全部出る）

`#` は `BattleResult.Events` の添字、`T` はターン。駒名の後の `#n` は InstanceId。

### 4-1. 試遊・感電 火の型 × 精鋭・近衛 × seed 0（勝ち T7）——粉・蓄電・割り込み

```
#57  StatusGain shock  トウ#3 → 新兵#7   粉=Main
#58  StatusGain shock  トウ#3 → 新兵#5   粉=Spread  SpreadFrom=新兵#7     （#59〜#61 も Spread）
#62  StatusGain shock  トウ#3 → シガ#0   粉=Leak  FriendlyFire
#63  ShockGauge 蓄電・増えた  Actor=トウ#3 Target=シガ#0  Amount 1  Slot 0
#64  StatusGain shock  トウ#3 → ベニ#2   粉=Leak  FriendlyFire
 …（味方の一撃で敵の感電が弾け、放電の連鎖）
#100 ShockGauge 割り込み  Actor=シガ#0 Target=新兵#7  Amount 1（振る前の蓄電） Slot 5（弾けた敵の数）  Partner=新兵#5（合図）
#101 Highlight 「そばで弾けた電気に、電気鞭のシガ の鞭が割り込む」
 …   Attack シガ#0 → 新兵#7（Reaction）・Damage …
#114 ShockGauge 蓄電・使った  シガ#0  Amount 0  Slot 1
```

### 4-2. 試遊・感電 雷の型 × 精鋭・近衛 × seed 0（勝ち T5）——割り込みと雷雲

```
#43  ShockGauge 割り込み  シガ#2 → 新兵#7  Amount 1 Slot 3 Partner=新兵#6
 …
#55  ShockGauge 蓄電・使った  シガ#2  0 ← 1
#56  ShockGauge 雷雲  Actor=新兵#6 Target=カタ#3  Amount 3  Slot 0  rem=3     （同じ連鎖の後・シガの割り込みの後に並ぶ）
#67  ShockGauge 雷雲  カタ#3  4 ← 3  rem=1
#69  Skill 「雷を落とした」 カタ#3
#70  ShockGauge 雷雲の雷  カタ#3  Amount 4
#71  Thunder カタ#3 → 新兵#5  Amount 36（= 6 ×（1 ＋ 1 ＋ 4））Slot 1
 …
#87  StatusGain shock カタ#3 → シガ#2（雷の漏れ）
#88  ShockGauge 蓄電・増えた  Actor=カタ#3 Target=シガ#2  1 ← 0
#142 ShockGauge 雷雲  カタ#3  8 ← 4  rem=5（上限 8）
```

### 4-3. 試遊・感電 糸 × ボス・勇者 × seed 0（負け T9）——糸 ② と、ほどける一撃

```
#15  StatusGain shock トウ#3 → 勇者#5 粉=Main   #16〜#17 粉=Leak（ソラ#0・クグ#2）
#22  ShockSpent Actor=ソラ#0 Target=勇者#5（ソラの一撃で勇者の感電が弾ける）
#23  ShockGauge 雷雲  Actor=勇者#5 Target=カタ#4  1 ← 0
 …（T2）ソラ#0 の感電が弾け、隣のクグへ放電が来る
#52  Discharge  Actor=クグ#2 → 勇者#5  Amount 8  src=Thread  Partner=ソラ#0     （② 隣の放電を糸で移した）
#53  Damage     クグ#2 → 勇者#5  12
 …   勇者の一撃でクグ自身の感電が弾け、組み付きがほどける
#61  ShockSpent 勇者#5 → クグ#2
#62  Discharge  クグ#2 → 勇者#5  Amount 8  src=Thread  Text=ほどけ   Partner なし（① 自分の弾け・切れる直前の糸）
#63  Damage     クグ#2 → 勇者#5  12
```

### 4-4. 試遊・標 ボス台 × ボス・勇者 × seed 0（勝ち T6）——標の層・羽・爪痕

```
#17  Damage    ザン#0 → 勇者#5  20（Reaction・仇討ち）
#18  Feather 増えた  Actor=ザン#0 Target=ミサ#2  Amount 2  Partner=勇者#5
#19  MarkLayer  ザン#0 → 勇者#5  1 ← 0
#20  StatusGain marked  ザン#0 → 勇者#5
#27  Feather 増えた  ミサ#2  3        #28  MarkLayer 勇者#5  2 ← 1
 …（標が5層まで積もり、羽は6枚）
     Feather 連射  ミサ#2  Amount 6
#55  Feather 追う  ミサ#2  Slot 1  Amount 6     → Attack ミサ#2 → 勇者#5・Damage
#58  Scar  ミサ#2 → 勇者#5  Amount 36  Slot 2964（新しい最大HP）  hp=2824
#59  Feather 追う  Slot 2   …   #62  Scar  勇者#5  36  2928
```

### 4-5. 試遊・標 道中 × 精鋭・近衛 × seed 4（勝ち T9）——乱射と、戻らない羽

```
#51  Feather 連射  ミサ#0  Amount 2
 …
#186 Feather 連射  ミサ#0  Amount 2
 …   1発目は標を追う
#211 Feather 乱射  ミサ#0  Slot 2  Amount 2  rem=1（標が尽きた瞬間）   → Damage（的は乱数）
#219 Feather 失った  ミサ#0  Amount 1（新しい枚数）  Slot 1（失った枚数）
```

## 5. 過去のメモとの重なり（指示書 §6-4）

| 機構 | 過去のメモ | 本期との関係 |
|---|---|---|
| 感電・放電の連鎖・カタの杭 | `CODEX_BRIEF_KATA.md` §2〜§4、`PHASE214_CODEX_MEMO.md` §1 | `Thunder` ／ `ShockSpent` ／ `Discharge` はそのまま。雷雲は杭の雷の「太さ」に足す（§2 の「種類の数で太さ」に雷雲の量が加わる） |
| 放電の表示（[放電]・欄） | `CODEX_BRIEF_DISCHARGE.md` | 糸の放電も `Discharge` → `Damage` の並びのまま（出どころ ＝ クグ）。「感電を付けた駒」の帰属の規則はそのまま使える |
| シガの鞭・竦み | `CODEX_BRIEF_SHIGA_MIO.md`・`CODEX_BRIEF_SHIELD_COWED.md` | 竦み（敵の `Cowed`）とシガ自身の怖気（`ShockGauge` 怖気）は**別物**。割り込みの鞭は既存の電気鞭の絵（`LiveWire`）と同じ並びに見出しが1件増えるだけ |
| クグの組み付き（糸が敵に伸びて巻き付く） | `CODEX_BRIEF_SHIELD_COWED.md`（「このメモの対象外」と書いてある） | 本期の糸の放電は、その「糸」の絵の上を電気が走る形になる |
