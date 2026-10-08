# 第291期 Codex 向けメモ —— 感電軸・標軸の台本（試遊の準備）

盤面は `design/PHASE291_PLAYTEST_PREP.md`。**このメモは再生（絵と音）の材料だけ**で、BattleCore / BattleSim 側は台本を出すところまでで止めてある。
**Codex の担当ファイル（DemoApp の演出）は1つも触っていない**（`Main.cs` は波の選択とプリセットの連結だけ・`NineWaveCheck.cs` は項目数の門を試遊の波に合わせただけ——報告 §5）。

- **足した出来事はすべて表示専用**: 盤面に触らず乱数を引かない。verbose の有無で勝敗・決着T・全員の HP が同じ（`playtest291 check` (d)・1,580 戦）。
- **保持者がいない戦では1件も出ない**（`playtest291 check` (e)）。
- 演出の意図のメモは `design/CODEX_BRIEF_SHOCK_MARK.md`（ポンが置いた・本文はそのまま）。台本と食い違う所は、ブリーフの末尾の「注記」に9項目足した。細部はこのメモを正とする。

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

## 6. 第294期の追記 —— 網（`Web`）と連鎖の鞭（`WhipChain`）が規定に入った

第294期にクグ ＝ KW-a（帯電の網）・シガ ＝ SW-a（連鎖の鞭）が規定になったので、第293期に足した表示専用の2種類が**デモ（試遊プリセット）でも出る**ようになった。
どちらも盤面に触らず乱数を引かない（verbose の有無で勝敗・決着T が同じ・`shock293 check` (l) ／ `guard294 check` (n)）。演出の意図は `design/CODEX_BRIEF_SHOCK_WEB.md`（ポンの追補）。
並びは `guard294 memo <行名の一部> <boss|guard|bat> <seed> <web|whip>` で出る（規定の駒・試遊の波の倍率のまま）。

### 6-1. 種類と欄

| 種類 | 札（`Text`） | いつ | `ActorId` | `TargetId` | `Amount` | `Slot` | ほか |
|---|---|---|---|---|---|---|---|
| `Web` | `張る`（`WebLabels.Spin`） | クグが組み付いている手番ごとに1本（`Skill`「組み付いている」の直後）。張る先は ① 組み付いた敵の隣の敵 → ② ほかの敵（糸の掛かっていない駒・席番号の若い順） | クグ | 糸を張った敵 | 0 | 張った敵の席 | `PartnerId` ＝ 組み付いた敵・`Team` ＝ 敵の陣営。その敵がまだ帯電していなければ直後に `StatusGain` shock（書き手 クグ） |
| `Web` | `糸玉`（`WebLabels.Ball`） | 敵がすべて糸の中で張る先が無い手番。組み付いた敵の隣の空き席に糸玉を張る（直前に `SilkBall`「張る」） | クグ | 糸玉（`SilkBall` と同じ番号・`AllUnits` に無い） | 0 | 糸玉の席 | `PartnerId` ＝ 組み付いた敵 |
| `Web` | `帯電`（`WebLabels.Recharge`） | ターンの頭（状態異常の刻みの後）に、糸の敵が帯電し直した（もう帯電していた敵は出ない） | クグ（倒れていても） | 糸の敵 | 0 | 糸の敵の席 | **直前に** `StatusGain` shock（書き手 クグ） |
| `ShockGauge` | `割り込み・倍率`（`ShockGaugeLabels.WhipChain`） | シガの割り込みの見出し（`割り込み`）の直後・鞭の `Attack` の直前 | シガ | 鞭の主目標（`割り込み` と同じ） | **倍率** ＝ 1 ＋ 合図の連鎖で弾けた数（糸玉を含む） | 合図の連鎖で弾けた数（`割り込み` の `Slot` と同じ） | 続く鞭の `Damage`（`Reaction`）はこの倍率を掛けた後の量 |

- **糸の切れ目の出来事は無い**: 糸の敵が倒れたら糸も消える（その敵の `Death` で判断する）。組み付きがほどけても張った糸は残る（次の帯電し直しも続く）。
- **放電は糸を伝わない**: 糸の敵が弾けたときの放電は、いつもどおり**隣の駒**へ（`Discharge`）。糸は「ターンの頭に帯電し直す」経路だけ。糸の上を電気が走る絵にするなら、`Web`「帯電」の瞬間（クグ → 糸の敵）。
- 勇者（ボス）は組み付いても止められないので、`張る` の代わりに糸玉（第292期の `SilkBall`）が張られる。ボスの台では `Web` は `糸玉` ／ `帯電` が出ない戦もある（勇者1体で張る先が無い）。

### 6-2. 試遊・感電 糸 × 精鋭・近衛 × seed 0（勝ち T5）——網が広がる（`Web` 10 件: 張る 3 ／ 帯電 6 ／ 糸玉 1）

```
--- T1
#19   StatusGain shock  トウ#3 → ソラ#0（粉の漏れ）
#21   Skill 組み付いている  クグ#2
#23   Web 張る   クグ#2 → 新兵#7  Partner=新兵#5（組み付いた敵）  Slot 2
--- T2
#103  StatusGain shock  クグ#2 → 新兵#7
#104  Web 帯電   クグ#2 → 新兵#7  Slot 2                       （ターンの頭・糸の敵が帯電し直す）
#133  Skill 組み付いている  クグ#2
#134  Web 張る   クグ#2 → 新兵#8  Partner=新兵#5  Slot 3
--- T3
#223  StatusGain shock  クグ#2 → 新兵#7   #224  Web 帯電  新兵#7
#225  StatusGain shock  クグ#2 → 新兵#8   #226  Web 帯電  新兵#8   （網全体がまたたく）
#274  Skill 組み付いている  クグ#2
#275  Web 張る   クグ#2 → 新兵#6  Partner=新兵#5  Slot 1
#276  StatusGain shock  クグ#2 → 新兵#6                         （KW-a は張った瞬間にも帯電）
--- T4
#355〜#360  StatusGain shock ＋ Web 帯電 × 3（新兵#6 ／ #7 ／ #8）
#410  Skill 組み付いている  クグ#2
#412  Web 糸玉   クグ#2 → 糸玉#10  Partner=新兵#5  Slot 5       （残りの敵はすべて糸の中）
```

### 6-3. 試遊・感電 雷の型 × 精鋭・近衛 × seed 0（勝ち T6）——連鎖が大きいほど鞭が重い（`WhipChain` 8 件）

```
--- T1
#43   ShockGauge 割り込み       シガ#2 → 新兵#7  Amount 1（振る前の蓄電）  Slot 3（弾けた数）  Partner=新兵#6（合図）
#44   Highlight 「そばで弾けた電気に、電気鞭のシガ の鞭が割り込む」
#45   ShockGauge 割り込み・倍率  シガ#2 → 新兵#7  Amount 4（＝ 1 ＋ 3）  Slot 3
#46   Attack  シガ#2 → 新兵#7（Reaction）
#48   Damage  シガ#2 → 新兵#7  96（Reaction）
#129  ShockGauge 割り込み       シガ#2 → 新兵#9  Slot 5
#131  ShockGauge 割り込み・倍率  シガ#2 → 新兵#9  Amount 6  Slot 5      （5 体の連鎖 → ×6）
#134  Damage  シガ#2 → 新兵#9  216（Reaction）
--- T2
#229  ShockGauge 割り込み・倍率  Amount 4  Slot 3   → #232 Damage 144
#296  ShockGauge 割り込み・倍率  Amount 2  Slot 1   → #299 Damage 48       （1 体だけの連鎖 → ×2）
```

- 倍率が 1（連鎖で弾けた数 0）の割り込みは無い（割り込みは「敵の感電が弾けた連鎖の直後」だけなので、弾けた数は 1 以上）。`割り込み・倍率` は割り込みのたびに必ず1件出る。
- 第293期 §4-2 の実測: 合図の連鎖の大きさの平均は 近衛 2.4〜2.8・大隊 3.4〜3.5・ボス 1.0（糸玉があれば 2.2）。倍率の絵の段は 2 ／ 3〜4 ／ 5 以上の3段で足りる。

## 7. 第295期の追記 —— ソラの見切り（`Insight`・規定）とヒサの「あいつを狙え！」（`MarkRally`・版）

第295期にソラ ＝ SR-b（見切り）が規定になった。表示専用の出来事を2つ足した（どちらも盤面に触らず乱数を引かない・`markheal295 check` (k)）。
`MarkRally` はヒサの版（HK-a ／ HK-b）だけが出す——**規定のヒサには出ない**（規定に入ったら演出の対象）。並びは `markheal295 memo <boss|guard|bat> <seed> <insight|rally> [hk0|hka|hkb]` で出る（循環の台 ＝ 試遊・標 ボス台の バン → ソラ）。

### 7-1. 種類と欄

| 種類 | いつ | `ActorId` | `TargetId` | `Amount` | `Slot` | ほか |
|---|---|---|---|---|---|---|
| `Insight` | 標を持つ敵の攻撃1回の打点を、ソラが見切って削った瞬間（その攻撃の `Attack` の直前） | ソラ | 攻撃の主（標を持つ敵） | 削った量（1回の攻撃ぶん・全体攻撃でも1件） | その敵の標の層 | `StatusRemaining` ＝ 削った割合（層 × 15%・上限 45）・`Team` ＝ 敵の陣営。直後の `Attack` ／ `Damage` の量は削った後 |
| `MarkRally` | 味方の攻撃のひとまとまり（手番 ／ 反撃 ／ 割り込み）が標の敵に当たり終えた後の回復1回 | ヒサ | 癒やした味方 | 実際に癒えた量（溢れは捨てる） | きっかけの標の層（1回の量 ＝ 層 × 6） | **直前に同じ回復の `Heal`**・`PartnerId` ＝ きっかけの攻撃の主・`HpAfter` ＝ 癒えた後。HK-b は1回の叫びで2件（攻撃した駒 → 最も傷ついた味方・同じ駒なら1件） |

- 見切りは「敵の腕が鈍る」ではなく「ソラが読んで受け流す」（ポンの判断）——絵はソラ側に置く（勇者が振りかぶった瞬間、ソラが目で追って手前で弾く、など）。層が深いほど大きく受け流す。
- 叫び（「あいつを狙え！ まだ倒れるな！」）はログの1行で、出来事は回復ごとの `MarkRally` だけ。叫びの演出は同じ `PartnerId` ・同じターンの `MarkRally` の先頭に1回。

### 7-2. 循環の台 × ボス・勇者 × seed 0 × 規定（負け T10）——見切り（`Insight` 9 件）

```
--- T1
#14  MarkLayer  ソラ#3 → 勇者#5  1 ← 0
#15  Insight    ソラ#3 → 勇者#5  Amount 1   Slot 1（層）  rem=15（%）
#16  Attack     勇者#5 → ザン#0（全体）
--- T2
#72  Heal       勇者#5 → 勇者#5  70（自前の回復）
#73  Insight    ソラ#3 → 勇者#5  Amount 10  Slot 3  rem=45
#74  Attack     勇者#5 → ゴルム#1
--- T5
#259 Insight    ソラ#3 → 勇者#5  Amount 25  Slot 7  rem=45（上限）
```

### 7-3. 同じ台 × seed 0 × HK-b（勝ち T7）——あいつを狙え！（`MarkRally` 37 件）

```
--- T1
#32  Heal       ヒサ#4 → ザン#0  6
#33  MarkRally  ヒサ#4 → ザン#0  Amount 6   Slot 1  Partner=ザン#0（ザンの手番の一撃が標の勇者に当たった）
#40  Heal       ヒサ#4 → ソラ#3  5
#41  MarkRally  ヒサ#4 → ソラ#3  Amount 5   Slot 2  Partner=ソラ#3（ソラの突き・1件目 ＝ 攻撃した駒）
#42  Heal       ヒサ#4 → ザン#0  12
#43  MarkRally  ヒサ#4 → ザン#0  Amount 12  Slot 2  Partner=ソラ#3（2件目 ＝ 最も傷ついた味方）
#44  Feather 連射  ミサ#2  Amount 3
 …   羽 3 発（標の勇者へ）
#57  MarkRally  ヒサ#4 → ミサ#2  Amount 5   Slot 2  Partner=ミサ#2（羽が何枚でも1回）
#59  MarkRally  ヒサ#4 → ゴルム#1  Amount 12  Slot 2  Partner=ミサ#2
```
