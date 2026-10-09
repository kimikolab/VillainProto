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

### 7-4. 第296期の追記 —— ヒサ HK-b が規定に入った（`MarkRally` が規定で出る）

第296期にヒサ ＝ HK-b（攻撃した味方 ＋ 最も傷ついた味方）が規定になった。**`MarkRally` は規定のヒサで出る**（7-1 の「規定のヒサには出ない」は第295期の話）。
試遊プリセットに **「試遊・標 循環」「試遊・標 三人組」「試遊・標 守り型」** を足した（`Presets.Playtest` の末尾・DemoApp は連結するだけ）。並びは `rally296 memo <行名の一部> <boss|guard|bat> <seed> [T から] [T まで]` で全件出る（ログも付く）。
**新しい出来事は足していない**——仇討ち・返り血・味方の刃は既存の種類で読める（下の 7-7）。

7-1 の表の補足（第296期に台本で確かめた）:

- `MarkRally` の**直前の `Heal` は、癒えた量が 1 以上のときだけ**出る。攻撃した駒が満タンだと `Heal` 無しの `MarkRally`（`Amount` 0）が1件だけ出る（循環 × ボスで 1戦 6.4 件）。叫びの演出は `Amount` 0 の件でも出してよい（声は出ている）が、癒しの光は飛ばさない。
- `Slot`（層）は**当たった瞬間の層**。ザンの仇討ちは「当ててから仇の標を足す」ので、仇討ちの `MarkRally` の `Slot` はその直前の `MarkLayer` の**前**の層。**標の無い敵への初めての仇討ちは叫ばない**（当たった時点で標が無い・7-6 の T1）。

### 7-5. 試遊・標 循環 × ボス・勇者（規定形）× seed 0（勝ち T7・`Insight` 6 件・`MarkRally` 37 件）——1つの循環の並び

T1 の勇者の全体攻撃から、ミサの羽の後の叫びまで（`rally296 memo 循環 boss 0 1 1`）。`…` は省いた区間。

```
--- T1
#3   StatusSnapshot 標  ゴルム#1  1                  ← ヒサの矢面（標を持つ味方）はゴルム
#14  MarkLayer   ソラ#3 → 勇者#5  1                  ← ソラの焦点（勇者の標 層1）
#15  Insight     ソラ#3 → 勇者#5  Amount 1  Slot 1  rem=15   ① 見切り（勇者の一撃を15%受け流す）
#16  Attack      勇者#5 → ザン#0  Amount 11  All             ② 勇者の全体攻撃
#17  Damage      勇者#5 → ザン#0  11  hp=41
 …   ゴルムの肩代わり（Damage relayed）／ ミサ・ヒサ・ソラへ 1 ずつ
#21  Damage      勇者#5 → ゴルム#1  6  All  hp=139           ③ 標を持つ味方（ゴルム）への被弾
#28  Damage      ザン#0 → 勇者#5  30  Reaction               ④ ザンの仇討ち（`Attack` は出ない・`Reaction` の `Damage` だけ）
#29  Feather 増えた  ザン#0 → ミサ#2  3  Partner=勇者#5      　 ミサの羽が増える
#30  MarkLayer   ザン#0 → 勇者#5  2                          ⑤ 仇の標（勇者の層 1 → 2）
#31  Damage      ザン#0 → ザン#0  3  Reaction  ff  hp=38     　 返り血（自傷 3）
#32  Heal        ヒサ#4 → ザン#0  6  hp=44
#33  MarkRally   ヒサ#4 → ザン#0  6  Slot 1  Partner=ザン#0  ⑥ あいつを狙え！（仇討ちの主＝ザンを癒す・当たった瞬間の層 1 × 6）
#34  Skill 隣の味方を指差した  ヒサ#4                        　 ヒサの手番（ゴルムを指差して逃げる）
#38  Attack      ソラ#3 → 勇者#5  7  Pierce                  　 ソラの突き
#40  Heal / #41 MarkRally  ヒサ#4 → ソラ#3  5  Slot 2  Partner=ソラ#3   叫び（1件目 ＝ 攻撃した駒）
#42  Heal / #43 MarkRally  ヒサ#4 → ザン#0 12  Slot 2  Partner=ソラ#3   　 （2件目 ＝ 最も傷ついた味方）
#44  Feather 連射  ミサ#2  3                                  ⑦ ミサの羽（3 枚・標の勇者を追う）
#45  Feather 追う → #46 Attack → #47 Damage 39 → #48 Scar   ×3 発
#57  Heal / #58 MarkRally  ヒサ#4 → ミサ#2   5  Slot 2  Partner=ミサ#2   ⑧ あいつを狙え！（羽が何枚でも1回）
#59  Heal / #60 MarkRally  ヒサ#4 → ゴルム#1 12  Slot 2  Partner=ミサ#2   　 （最も傷ついた味方 ＝ 矢面のゴルム）
#61  Attack      ザン#0 → 勇者#5  10  Single                 　 ザンの手番
#63  MarkRally   ヒサ#4 → ザン#0  0  Slot 2  Partner=ザン#0  　 ザンは満タン → `Heal` 無し・`Amount` 0
#64  Heal / #65 MarkRally  ヒサ#4 → ゴルム#1  9
```

- 循環の1周 ＝ ②〜⑧。T2 も同じ形で回る（勇者 → ゴルムへの被弾 → #99 仇討ち 30 → #101 層 3 → 4 → #104 `MarkRally` 14（層 3 × 6 ＝ 18 のうち癒えた分・残りは溢れ）→ ミサの羽 5 枚 → 叫び）。層が深くなるほど叫びの量が大きい。
- ボスは1ターンに全体攻撃1回なので、仇討ちは1ターン1回（1戦 6 回）。叫びは1戦 20 回・回復の件数 29（50 戦の平均・`rally296 events`）。

### 7-6. 試遊・標 三人組 × 精鋭・近衛 × seed 0（勝ち T6・`Insight` 0 件・`MarkRally` 18 件）——ソラなしで回る循環

ソラがいない（後1 ＝ ドルガ）ので見切りは出ない。新兵 5 体が1ターンに単体の一撃を5発打つので、**仇討ちが連打される**（`rally296 memo 三人組 guard 0 1 2`）。

```
--- T1
#27  Attack      新兵#5 → ゴルム#1  33  Single               標を持つゴルムへの一撃
#28  Damage      新兵#5 → ゴルム#1  17  hp=133                （矢面で半分）
#29  Damage      ザン#0 → 新兵#5  20  Reaction               仇討ち
#31  MarkLayer   ザン#0 → 新兵#5  1  ／ #32 StatusGain marked  仇の標（この敵は初めて標を持つ）
#33  Damage      ザン#0 → ザン#0  3  Reaction ff             返り血
 …   （叫ばない——当たった時点で新兵#5 に標が無かった）
#36〜#57  同じ形があと3回（新兵#6 ／ #9 ／ #7）——4 体に仇の標・ザンは返り血と被弾で hp 7 まで落ちる
#59  Attack      ザン#0 → 新兵#9  10  Single                 ザンの手番（標の敵へ）
#61  Heal / #62 MarkRally  ヒサ#4 → ザン#0  6  Slot 1  Partner=ザン#0   初めての叫び
#64  Attack      ゴルム#1 → 新兵#5  10
#66  Heal / #67 MarkRally  ヒサ#4 → ゴルム#1  6  Partner=ゴルム#1
#68  Heal / #69 MarkRally  ヒサ#4 → ザン#0   6  Partner=ゴルム#1   最も傷ついた味方 ＝ ザン
--- T2
#94  Feather 連射  ミサ#2  5  → 羽 5 発（標の新兵 4 体へ）
#119 Heal / #120 MarkRally  ヒサ#4 → ミサ#2  6  Partner=ミサ#2
#121 Heal / #122 MarkRally  ヒサ#4 → ザン#0  6  Partner=ミサ#2
#124 Attack      新兵#9 → ゴルム#1  33                        標を持つゴルムへの一撃
#126 Damage      ザン#0 → 新兵#9  30  Reaction               仇討ち（新兵#9 は標 層1 を持っている）
#128 MarkLayer   ザン#0 → 新兵#9  2                           層 1 → 2
#129 Damage      ザン#0 → ザン#0  3  Reaction ff              返り血
#130 Heal / #131 MarkRally  ヒサ#4 → ザン#0  6  Slot 1  Partner=ザン#0   仇討ち1回ごとに叫ぶ
#133〜#140  同じ形がもう1回（新兵#7）→ 叫び
```

- **ソラがいなくても回る**理由: ヒサの矢面（ゴルム）が殴られるたびにザンが仇討ちし、2巡目からは仇の標を持つ敵への仇討ちが毎回叫びになる。癒しの先はザン（攻撃した駒）と、最も傷ついた味方（たいていザン）。
- 近衛 × seed 0..49 で叫び 1戦 17.3 回・仇討ち 12.8 回・返り血 12.8 回（`rally296 events`）。

### 7-7. 既存の出来事で読めるもの（第296期に確かめた・新しい種類は足していない）

| 見せたいもの | 台本 | 見分け方 |
|---|---|---|
| ザンの仇討ち | `Damage`（`ActorId` ＝ ザン・`TargetId` ＝ 敵・`Reaction`・`Pattern` ＝ null） | **`Attack` は出ない**。直前の「標を持つ味方への `Damage`」が引き金。続いて `Feather 増えた`（ミサがいれば）・`MarkLayer`（＋ 初めてなら `StatusGain marked`） |
| ザンの返り血 | `Damage`（`ActorId` ＝ `TargetId` ＝ ザン・`Reaction`・`FriendlyFire`） | 仇討ちの `Damage` の直後（羽 ／ 標の出来事を挟む）。量は 3 |
| ミサの乱射が味方に当たる | `Feather 乱射` の直後の `Damage`（`ActorId` ＝ ミサ・`TargetId` ＝ 味方・`FriendlyFire`・`Pattern` ＝ Single） | 乱射の一撃には `Attack` が無い（`Feather 乱射` が代わり）。例: 三人組 × 近衛 × seed 2 の #21 → #22（ミサ → ゴルム 6）。HK-b の台では乱射が少ない（循環 × ボス 0・近衛 ／ 大隊で 1戦 0.0〜0.6 回）——標が尽きにくいため |
| 味方の刃を味方の声が塞ぐ | 乱射の `Damage` と、その後の `MarkRally`（最も傷ついた味方への2件目） | 直接は結ばれていない（叫びは標の敵に当たった攻撃のひとまとまりにだけ出る）。乱射で傷ついた味方が「最も傷ついた味方」になれば、次の叫びの2件目がそこへ行く |

### 7-8. 第299期: 羽と濡れ衣が規定になった（試遊・標 循環 × 近衛 × seed 0・`zan299 memo 循環 guard 0 規定`）

第299期にミサ（MF-b）とザン（ZN-b）を規定にしたので、`FeatherMark` ／ `Framed` は試遊でも出る。ヒサの叫びは自分も癒す（`MarkRally` の `ActorId` ＝ `TargetId` ＝ ヒサ）。

```
--- T0（開戦: ヒサの矢面の標がゴルムに付いた瞬間）
FeatherMark 味方  ミサ#2 → ゴルム#1  Amount 1  Slot 1        標が付いた瞬間の羽（味方へ）
Damage      ミサ#2 → ゴルム#1  24  Reaction ff                 羽の一撃（同士討ち・矢面の半減なし）
Framed あいつがやった  ヒサ#4 → 新兵#5  Partner=ゴルム#1       ヒサが敵を指差す（撃たれた味方 ＝ Partner）
Framed 濡れ衣         ザン#0 → 新兵#5  Partner=ミサ#2          ザンの濡れ衣の仇討ちの印（本当に撃った味方 ＝ Partner）
Damage      ザン#0 → 新兵#5  20  Reaction                      濡れ衣の仇討ち（倍の刃）
Feather 増えた  ザン#0 → ミサ#2  Amount 2  Partner=新兵#5       仇の標でミサの羽が増える
Damage      ザン#0 → ザン#0  3  Reaction ff                    返り血
--- T1
FeatherMark 味方  ミサ#2 → ソラ#3  Amount 1  Slot 1          ソラが自分に付けた標へ羽
…（濡れ衣がもう1回）…
FeatherMark 敵    ミサ#2 → 新兵#7  Amount 1  Slot 1          敵に新しい標が付いた瞬間の羽
Attack / Damage   ミサ#2 → 新兵#7  24 → 36  Reaction           標の +50%
MarkRally   ヒサ#4 → ミサ#2 ／ ゴルム#1                         羽の1発は攻撃のひとまとまり1つ → 叫び
…
MarkRally   ヒサ#4 → ヒサ#4  Amount 4  Slot 3                  叫びがヒサ自身を癒す（第299期・最も傷ついた味方 ＝ ヒサ）
```

- 並びの型: **標が付いた瞬間の羽（`FeatherMark`）→ 味方への羽の `Damage`（`FriendlyFire`）→「あいつがやった！」（`Framed` Accuse）→ 濡れ衣（`Framed` Vendetta）→ 仇討ちの `Damage`（`Reaction`）→ 返り血**。`Framed` の2件は必ず対で、直後に仇討ちの `Damage` が来る。
- 羽の `FeatherMark` の `Slot` は 1 ＝ 新しい標・0 ＝ 層の追加（MF-b は層の追加でも飛ぶ）。`Amount` ＝ 的の標の層。
- **版だけの出来事（規定では出ない）**: ザンの仇巡り（ZM-a ／ ZM-1）の `VendettaRound`。`Text` ＝「仇巡り」（始まり・`Slot` ＝ 巡る敵の数 ／ `Amount` ＝ 太刀の合計）と「太刀」（1太刀ごと・`Slot` ＝ 何太刀目か・直後にその太刀の `Attack` ／ `Damage`）。仇巡りの太刀は手番の攻撃（`Reaction` でない）。例: `zan299 memo 循環 boss 0 ZM-a`（T2 から毎手番 勇者へ 8 太刀）。

## 8. 第300期の追記 —— ザンの仇巡りが規定に・矢面が羽を半分にする（`BeckonFeather`・新しい表示専用の出来事）

第300期にザンの仇巡り（ZM-a）を規定にしたので、`VendettaRound` は試遊でも出る。ヒサの矢面の半減がミサの羽にも効くようになった（規定のヒサの札 `BeckonFeather`）。
例はすべて規定の駒・`round300 memo <台> <波> <seed>` で再現できる（件数の一覧は `round300 find`）。

### 8-1. 種類と欄（足したのは1種類）

| 出来事 | 欄 | いつ出るか |
|---|---|---|
| `BeckonFeather`（新・表示専用） | `ActorId` ＝ 標を付けたヒサ ／ `TargetId` ＝ 撃たれた味方 ／ `PartnerId` ＝ 撃ったミサ ／ `Amount` ＝ 矢面で防いだ量 | ヒサの標を持つ味方にミサの羽が当たり、半分にした瞬間。**直後にその一撃の `Damage`**（`Amount` は半分にした後の量・`FriendlyFire`）。敵の攻撃の矢面の半減には出ない（第184期から台本に出ていない——ログの行だけ） |

既存の出来事だけでは読めなかった（羽の `Damage` の量は半分にした後の値で、半分にしたかどうかの印が無い）ので足した。DemoApp は知らない種類を素通りする。

### 8-2. 見せ場ごとの例（seed 0..49 の件数は `round300 find`）

| 見せ場 | 出来事 | 例にする戦 | 1戦あたり | seed 0 で最初に出る T |
|---|---|---|--:|---|
| ドハの力配り | `ShareGive`「力」 | 試遊・標 守り型 × ボス × seed 0（勝ち T3） | 31.0 | T0（`ShareGive 力 ドハ#2 → ゴルム#0 Amount 1 Slot 1`、直後に中継の `Damage`） |
| ミサの標撃ち・敵 | `FeatherMark`「敵」 | 試遊・標 循環 × 近衛 × seed 0（勝ち T4） | 12.8 | T1 |
| ミサの標撃ち・味方 | `FeatherMark`「味方」 | 同上 | 4.0 | T0（矢面の標がゴルムに付いた瞬間） |
| 「あいつがやった！」と濡れ衣の仇討ち | `Framed`「あいつがやった」→「濡れ衣」 | 同上 | 4.0 ／ 4.0 | T0（味方への羽の直後） |
| 仇巡り・ボスの連撃 | `VendettaRound` | 試遊・標 循環 × ボス × seed 0（勝ち T6） | 4.0 手番 | T1（勇者1体へ 5 太刀・T2 から 8 太刀） |
| 仇巡り・精鋭の巡り | `VendettaRound`（`Slot` ≧ 2） | 試遊・標 三人組 × 大隊 × seed 0（勝ち T5） | 2.1 手番 | T1（7体を巡って 8 太刀）・T2（4体）・T3（2体） |
| ヒサの叫びがヒサ自身を癒す | `MarkRally`（`TargetId` ＝ `ActorId` ＝ ヒサ） | 試遊・標 三人組 × 近衛 × seed 0（勝ち T5） | 3.9 | T1 |
| 矢面の半減が羽に効いた | `BeckonFeather` | 標経済 (ヒサ×ザン×ミサ) × 大隊 × seed 0（勝ち T4） | 1.2 | T0（`BeckonFeather ヒサ#4 → ガルド#1 Amount 16 Partner=ミサ#0`、直後に `Damage ミサ#0 → ガルド#1 16 ff`） |

精鋭の巡り（三人組 × 大隊 × seed 0 × T1）:

```
VendettaRound 仇巡り  ザン#0           Amount 8  Slot 7      始まり（太刀の合計 8 ／ 巡る敵 7）
VendettaRound 太刀    ザン#0 → 農兵#5   Slot 1 → Attack / Damage 15
VendettaRound 太刀    ザン#0 → 農兵#5   Slot 2 → Attack / Damage 15   層 2 の敵には 2 太刀
VendettaRound 太刀    ザン#0 → 農兵#6   Slot 3 → …                    以下 農兵#7 ／ #8 ／ #9 ／ #10 ／ #12 へ1太刀ずつ
VendettaRound 太刀    ザン#0 → 農兵#12  Slot 8 → Attack / Damage 15
MarkRally             ヒサ#4 → ザン#0   Amount 12                     叫びは手番に1回
```

### 8-3. 1つの循環の並び（試遊・標 循環 × ボス・勇者（規定形）× seed 0・勝ち T6・`round300 memo 循環 boss 0 1`）

```
--- T0（開戦: ヒサの矢面の標がゴルムに付いた瞬間）
FeatherMark 味方      ミサ#2 → ゴルム#1  Amount 1 Slot 1     標撃ちの羽（味方へ・誤射）
BeckonFeather         ヒサ#4 → ゴルム#1  Amount 12 Partner=ミサ#2   矢面が羽を半分にした（第300期）
Damage                ミサ#2 → ゴルム#1  12  Reaction ff      半分になった羽の一撃
Framed あいつがやった  ヒサ#4 → 勇者#5  Partner=ゴルム#1     ヒサが勇者を指差す
Framed 濡れ衣          ザン#0 → 勇者#5  Partner=ミサ#2       濡れ衣の仇討ちの印
Damage                ザン#0 → 勇者#5   20  Reaction         仇討ち（倍の刃）
Feather 増えた         ザン#0 → ミサ#2   Amount 2             仇の標でミサの羽が増える
MarkLayer             ザン#0 → 勇者#5   Amount 1             勇者の標の層
Damage                ザン#0 → ザン#0   3  Reaction ff        返り血
--- T1
（ソラが勇者に標を足す → FeatherMark 味方 ミサ#2 → ソラ#3（ソラが自分に付けた標）→ Framed ×2 → 仇討ち → 叫び）
FeatherMark 敵        ミサ#2 → 勇者#5   Amount 3             勇者の層が増えた瞬間の羽
Attack / Damage       ミサ#2 → 勇者#5   24 → 36  Reaction     標の +50%
MarkRally             ヒサ#4 → ミサ#2 ／ ゴルム#1            叫び（攻撃した味方と最も傷ついた味方）
Attack / Damage       勇者#5 → ザン#0 …（全体攻撃）
Damage                ザン#0 → 勇者#5   30  Reaction         標の付いた味方が殴られた → 仇討ち
…
MarkRally             ヒサ#4 → ヒサ#4   Amount 5             叫びがヒサ自身を癒す
FeatherMark 味方 → BeckonFeather → Damage 13 → Framed ×2 → 仇討ち   （ヒサが指差し直した矢面の味方へまた羽）
Feather 連射          ミサ#2           Amount 6              ミサの手番: 羽 6 枚が勇者を追う（Attack / Damage 39 × 6）
VendettaRound 仇巡り  ザン#0           Amount 5 Slot 1       ザンの手番: 勇者1体へ 5 太刀（層 5）
VendettaRound 太刀    ザン#0 → 勇者#5  Slot 1..5 → Attack / Damage 15 × 5
--- T2 以降は同じ循環（勇者はターンの頭に大きく癒える——Heal 198）。T2 からの仇巡りは 8 太刀
```

- 並びの型: **被弾（または標が付く）→ 標撃ちの羽 →（味方なら）矢面の半減 → 誤射の `Damage` →「あいつがやった！」→ 濡れ衣の仇討ち → 標の層 → 羽が増える → 叫び → … → ミサの手番の連射 → ザンの手番の仇巡り**。
- `BeckonFeather` は必ず `FeatherMark 味方`（または味方に流れた手番の羽）の直後・その一撃の `Damage` の直前に1件。
- 仇巡りの太刀は手番の攻撃（`Reaction` でない）。仇討ち（割り込み・`Reaction`）とは別の絵にする（ブリーフ §3）。

## 9. 第301期の追記 —— ヒサ単独の「あいつがやった！」（規定）・号令（`Command`・版）・庇い（`Cover`・版）

第301期にソラ（矢面の標を剥がさない）・ザン（標を付けてから斬る）・ヒサ（「あいつがやった！」をヒサの札に）を規定にした。号令と庇いは版で、採否はポン（第301期の報告 §7）。
例は `hisa301 memo <台> <波> <seed> <版> [最後のT]` で再現できる（件数の一覧は `hisa301 find`）。

### 9-1. 種類と欄（足したのは2種類・どちらも版でしか出ない）

| 出来事 | 欄 | いつ出るか |
|---|---|---|
| `Command`（新・表示専用） | `ActorId` ＝ ヒサ ／ `TargetId` ＝ 的 ／ `Amount` ＝ 刻んだ層の数 ／ `Slot` ＝ 使った溢れ（層 × 20）／ `Text` ＝ 「即時」（HL-i）か「手番」（HL-t3 ／ HL-t8） | 叫びの溢れを層に換えた瞬間。**層は先に全部刻まれる**（`Command` の直前にその数だけ `MarkLayer` と、層ごとの `Feather`「増えた」）。**羽はその後に層1つにつき1発**（`FeatherMark`「敵」が `Amount` 本・どれも同じ的・`Amount` は刻んだ後の層）。手番版はヒサの手番の中（指差し ／ 逃げ回るの後）、即時版は叫び（`MarkRally`）の直後 |
| `Cover`（新・表示専用） | `ActorId` ＝ ヒサ ／ `TargetId` ＝ 庇った味方 ／ `PartnerId` ＝ 攻撃の主 ／ `Amount` ＝ 元の一撃の量 | 味方への敵の倒れる一撃をヒサが代わりに受ける瞬間。**直後にヒサの標の `StatusGain`（`ActorId` ＝ `TargetId` ＝ ヒサ）と、攻撃の主 → ヒサの `Damage`**。続いて（ザンがいれば）攻撃の主への仇討ち。庇われた味方には `Damage` が出ない。1戦に1度 |

**「あいつがやった！」は既存の `Framed`「あいつがやった」のまま**（種類は足していない）。第300期までは必ず直後に「濡れ衣」（ザン）が続いたが、**第301期からはヒサだけで出る**——直後に `Feather`「増えた」（ミサがいれば）・`MarkLayer` ／ `StatusGain marked`（ヒサ → 指差した敵）が続き、ザンがいればその後に「濡れ衣」と仇討ちの `MarkLayer` → `Damage`（**仇討ちは標を付けてから斬る**）。

### 9-2. 見せ場ごとの例（seed 0..49 の件数は `hisa301 find`）

| 見せ場 | 出来事 | 例にする戦 | 1戦あたり | seed 0 で最初に出る T |
|---|---|---|--:|---|
| ヒサ単独の「あいつがやった！」（規定） | `Framed`「あいつがやった」（「濡れ衣」が続かない） | ザンなし (ヒサ×ミサ×ボルグ×カド) × 近衛 × seed 0（勝ち T6） | 2.94 | T0（ミサの標撃ちがゴルムに当たった直後） |
| あいつがやった！ → 濡れ衣（規定） | `Framed`「あいつがやった」→「濡れ衣」 | 試遊・標 循環 × 近衛 × seed 0 | 2.00 | T0 |
| 号令・即時（HL-i） | `Command`「即時」 | 試遊・標 循環 × ボス × seed 0 | 3.00 | T1 |
| 号令・手番（HL-t3） | `Command`「手番」 | 試遊・標 循環 × ボス × seed 0（勝ち T4） | 3.00 | T1 |
| 号令・手番（HL-t8） | `Command`「手番」 | 同上 | 2.00 | T1 |
| 庇い（HC） | `Cover` | 試遊・標 三人組 × 大隊 × seed 4（`hisa301 memo 三人組 bat 4 K4 3`） | 0.08 | seed 0 では出ない（seed 4 の T2） |

ザンのいない台の T0（`hisa301 memo ザンなし guard 0 K0 1`）:

```
StatusGain marked     ヒサ#3 → ゴルム#2              矢面の標
FeatherMark 味方      ミサ#4 → ゴルム#2  Amount 1     標撃ちの羽（味方へ）
BeckonFeather         ヒサ#3 → ゴルム#2  Amount 18 Partner=ミサ#4   矢面が羽を半分に
Damage                ミサ#4 → ゴルム#2  18  Reaction ff
Framed あいつがやった  ヒサ#3 → 新兵#5   Partner=ゴルム#2   ヒサが敵を指差す（ザンはいない）
Feather 増えた        ヒサ#3 → ミサ#4    Amount 2 Partner=新兵#5
MarkLayer             ヒサ#3 → 新兵#5    Amount 1          ヒサが付けた標
StatusGain marked     ヒサ#3 → 新兵#5
```

庇い（三人組 × 大隊 × seed 4 × HC の T2）:

```
Attack                農兵#11 → ザン#0   Amount 24          倒れる一撃
Cover                 ヒサ#4 → ザン#0    Amount 24 Partner=農兵#11   ヒサが前に飛び出す
StatusGain marked     ヒサ#4 → ヒサ#4                       自分に標（ミサの羽は飛ばない）
Damage                農兵#11 → ヒサ#4   24                 ヒサが受ける（ザンへの Damage は出ない）
Feather 増えた        ザン#0 → ミサ#2    Amount 13 Partner=農兵#11
MarkLayer             ザン#0 → 農兵#11   Amount 2           ザンの仇討ち（標を付けてから）
Damage                ザン#0 → 農兵#11   30  Reaction
Damage                ザン#0 → ザン#0    3  Reaction ff     返り血
MarkRally             ヒサ#4 → ザン#0    Amount 12          叫び
FeatherMark 敵        ミサ#2 → 農兵#11   Amount 2           層が増えたので羽
```

### 9-3. 手番版の「4段」（試遊・標 循環 × ボス・勇者（規定形）× seed 0 × HL-t3・勝ち T4・`hisa301 memo 循環 boss 0 K2 2` の T2）

ヒサ（速 10）の号令 → 層の数だけミサの羽が割り込みで撃つ → ミサ（速 6）の一斉射撃（在庫も増えている）→ ザン（速 5）の仇巡り。

```
MarkLayer             ヒサ#4 → 勇者#5   Amount 20 / 21 / 22    号令の3層（直前に Feather 増えた ×3）
Command 手番          ヒサ#4 → 勇者#5   Amount 3  Slot 60      ① 号令（溢れ 60 → 層 3）
FeatherMark 敵        ミサ#2 → 勇者#5   Amount 22              ② 羽の割り込み 1本目
Attack / Damage       ミサ#2 → 勇者#5   42  Reaction
MarkRally             ヒサ#4 → ミサ#2   Amount 0               （羽ごとに叫び——溢れがまた溜まる）
FeatherMark 敵        ミサ#2 → 勇者#5   Amount 22              ② 2本目
FeatherMark 敵        ミサ#2 → 勇者#5   Amount 22              ② 3本目
Attack / Damage       ソラ#3 → 勇者#5   19                     （ソラの突き）
Feather 連射          ミサ#2 → ミサ#2   Amount 23              ③ ミサの一斉射撃（23 枚）
Feather 追う ×23 → Attack / Damage …
VendettaRound 仇巡り  ザン#0           Amount 8 Slot 1         ④ ザンの仇巡り（勇者1体へ 8 太刀）
VendettaRound 太刀    ザン#0 → 勇者#5   Slot 1 → Attack / Damage 15 …
```

- 4段が揃ったターンは `hisa301 four` で数える（同じ台本の順を見る: `Command` → その後の `FeatherMark`「敵」→ その後の `Feather`「連射」→ その後の `VendettaRound`「仇巡り」）。HL-t3 で循環 × 近衛 ／ 大隊は 1戦 1.9 ／ 1.7 回（揃った戦 98 ／ 100%）、ボスは 2.0 回（100%）。
- 号令の層は**先に全部刻んでから**羽が飛ぶので、`FeatherMark` の `Amount`（的の層）は3本とも 22 で同じ（1本ごとに増えるのではない）。

## 10. 第302期の追記 —— 号令の玉（`CommandBall`・規定）・粛で黙る（`Framed`「黙る」・規定）・庇い（`Cover`・規定）・鼓舞（`Rouse`・版）

第302期に、ヒサの「あいつがやった！」を粛 ／ 痺れで止め（段0-1）、号令 HL-t3 に溜まりの上限 60（玉3つ）を付けて規定にし（段0-2）、庇い HC を規定にした（段0-3）。**`Command`「手番」と `Cover` は規定で出る**ようになった（第301期は版だけ）。鼓舞（HB-t ／ HB-p）は版で、採否はポン（第302期の報告 §7）。
例は `hisa302 memo <台> <波> <seed> <版 S3|HVs|HBt|HBp> [最後のT]` で再現できる（件数の一覧は `hisa302 find`）。Web 側の意図は `design/CODEX_BRIEF_HISA_COMMAND.md`。

### 10-1. 種類と欄（足したのは2種類・`Framed` に1ラベル）

| 出来事 | 欄 | いつ出るか |
|---|---|---|
| `CommandBall`（新・表示専用） | `ActorId` ＝ ヒサ ／ `Amount` ＝ **その後の玉の数（0〜3）** ／ `Slot` ＝ その後の溜まり（溢れの量・0〜60）／ `Text` ＝ 「溜まる」「使う」「捨てる」（`CommandBallLabels`） | 「溜まる」＝ 叫びの溢れで玉が増えた瞬間（**その叫びの `MarkRally` より前**に出る・1回で 0 → 3 に飛ぶこともある）。「使う」＝ 号令で玉を使った瞬間（**`Command` の直後**・`Amount` は使った後の玉）。「捨てる」＝ 満杯（60）で溢れを捨てた瞬間（玉の数は変わらない）。**上限の無い第301期の版（HL-i ／ HL-t3 ／ HL-t8）には出ない** |
| `Rouse`（新・表示専用・**版 HB-t ／ HB-p だけ**） | `ActorId` ＝ ヒサ ／ `Amount` ＝ 1体あたりの攻撃力の上げ幅（玉1つ ＝ +5）／ `Slot` ＝ 受けた体数 ／ `Text` ＝ 「次の手番まで」（HB-t）・「戦の終わりまで」（HB-p）・「解ける」（`RouseLabels`） | 号令の `Command` → `CommandBall`「使う」の直後・**羽の `FeatherMark` より前**（号令 → 鼓舞 → 羽 → 一斉射撃 → 仇巡り）。HB-t は**ヒサの次の手番の頭**に「解ける」（`Amount` ＝ 引き上げた量）→ 新しい号令 → 「次の手番まで」の順。ヒサが倒れたら解けない |
| `Framed`「黙る」（ラベルを足した・表示専用） | `ActorId` ＝ ヒサ ／ `TargetId` ＝ 撃たれた味方 ／ `PartnerId` ＝ 撃った味方（ミサなど） | 規定のヒサの「あいつがやった！」が粛 ／ 痺れで止まった瞬間。**直前にヒサへの `Sealed`「粛」**（粛が単独の原因のとき）が出る。「あいつがやった」も「濡れ衣」も続かない |

- **台本で玉の数（0〜3）を追うには `CommandBall` の `Amount` を読むだけでよい**（増える ／ 減る ／ 満杯のどの瞬間にも出る）。
- **「捨てる」は多い**: ボスでは溜まりがほぼずっと満杯なので、叫び（`MarkRally`）のたびに出る（循環 × ボス × seed 0..49 で 1戦 74.6 件）。演出は間引くか、満杯になった最初の1回だけ光らせるのがよい（ブリーフ §1 の「うるさくしない」）。
- 叫びの回復の溢れは「最も傷ついた味方」が満タンのときにも出る（`MarkRally` の `Amount` が 0 の行）——玉はそこでも増える。

### 10-2. 見せ場ごとの例（seed 0..49 の件数は `hisa302 find`）

| 見せ場 | 出来事 | 例にする戦 | 1戦あたり | seed 0 で最初に出る T |
|---|---|---|--:|---|
| 玉が溜まる | `CommandBall`「溜まる」 | 試遊・標 循環 × ボス × seed 0（勝ち T4） | 6.00 | T1 |
| 号令で玉を使う | `Command`「手番」→ `CommandBall`「使う」 | 同上 | 3.00 | T1 |
| 満杯で捨てる | `CommandBall`「捨てる」 | 同上 | 74.60 | T1 |
| 粛で黙る | `Sealed`「粛」→ `Framed`「黙る」 | 試遊・標 三人組 × 第2波 × seed 0（負け T9） | 1.28 | T2 |
| 庇い（規定） | `Cover` | 試遊・標 三人組 × 大隊 × seed 5 | 0.06 | seed 0 では出ない（seed 5 の T1） |
| 鼓舞（HB-t） | `Rouse`「次の手番まで」／「解ける」 | 試遊・標 循環 × ボス × seed 0 × HB-t（勝ち T3） | 2.00 ／ 1.00 | T1 ／ T2 |
| 鼓舞（HB-p） | `Rouse`「戦の終わりまで」 | 同上 × HB-p | 2.00 | T1 |

玉が溜まって、号令で使う（`hisa302 memo 循環 boss 0 S3 2` の T1・ダメージの行は省いた）:

```
CommandBall 溜まる   ヒサ#4  Amount 1  Slot 32        叫びの溢れで玉 1
MarkRally            ヒサ#4 → ザン#0  Amount 7       （その叫び）
CommandBall 溜まる   ヒサ#4  Amount 2  Slot 41        玉 2
MarkRally            ヒサ#4 → ゴルム#1  Amount 21
FeatherMark 敵       ミサ#2 → 勇者#5  Amount 5
CommandBall 溜まる   ヒサ#4  Amount 3  Slot 60        玉 3（満杯）
CommandBall 捨てる   ヒサ#4  Amount 3  Slot 60        満杯で溢れを捨てた
MarkRally            ヒサ#4 → ミサ#2  Amount 4
CommandBall 捨てる   ヒサ#4  Amount 3  Slot 60        （以後、叫びのたびに）
…
Feather 増えた ×3    ヒサ#4 → ミサ#2  Amount 12 / 13 / 14   号令の3層（羽の在庫が増える）
Command 手番         ヒサ#4 → 勇者#5  Amount 3  Slot 60     号令（玉3つ → 層 3）
CommandBall 使う     ヒサ#4  Amount 0  Slot 0               玉を使い切った
FeatherMark 敵       ミサ#2 → 勇者#5  Amount 13             層の数だけ羽が飛ぶ（1本目）
CommandBall 溜まる   ヒサ#4  Amount 3  Slot 60              羽の叫びの溢れで、すぐまた満杯
```

粛で黙る（`hisa302 memo 三人組 2 0 S3 2` の T2）:

```
Sealed 粛            — → ヒサ#4                              粛がヒサを止めた
Framed 黙る          ヒサ#4 → ゴルム#1  Partner=ミサ#2      ミサの羽が矢面のゴルムに当たったが、ヒサは指差さない
```

鼓舞（`hisa302 memo 循環 boss 0 HBt 2`・T1 の号令と T2 の頭）:

```
--- T1
Command 手番         ヒサ#4 → 勇者#5  Amount 3  Slot 120    号令（重い玉3つ ＝ 溢れ 120 → 層 3）
CommandBall 使う     ヒサ#4  Amount 0  Slot 0
Rouse 次の手番まで   ヒサ#4  Amount 15  Slot 5              仲間が奮い立つ（5 体に +15）
FeatherMark 敵       ミサ#2 → 勇者#5  Amount 13             羽（攻撃力 +15 が乗る）
…
Feather 連射         ミサ#2  Amount 14                       ミサの一斉射撃
VendettaRound 仇巡り ザン#0  Amount 8                        ザンの仇巡り
--- T2
Rouse 解ける         ヒサ#4  Amount 15  Slot 5              ヒサの手番の頭で解ける
Feather 増えた ×3 → Command 手番 → CommandBall 使う → Rouse 次の手番まで（同じ並び）
```

- 庇いの並びは §9-2 のまま（規定になっただけ）。粛（第2波）では庇いも出ない（`Cover` が出ず、倒れる一撃の `Damage` がそのまま出る）。
- **叫び（`MarkRally`）は規定では粛でも止まらない**（版 HV-s だけが止める。採否はポン）。

## 11. 第303期の追記 —— 粛でヒサが完全に黙る（規定）・身振り（`Framed`「身振り」・版）

第303期に、ヒサの叫び（あいつを狙え！）も粛 ／ 痺れで止めた（規定・`RallyQuiet`）。**粛の保持者が生きている間、ヒサの指差し（`Framed`「黙る」）・叫び（`MarkRally` が出ない）・庇い（`Cover` が出ない）はすべて止まる**。叫びが止まった瞬間は `Sealed`「粛」（ヒサ）で読める（専用の出来事は足していない）。
粛を狩る版（Q1 ／ Q3 ／ QA）は版で、採否はポン（第303期の報告 §8）。例は `hush303 memo <台> <波> <seed> <版 S|Q1|Q3|QA|HCd|HCs> [最後のT]`（件数は `hush303 find`）。

### 11-1. 種類と欄（`Framed` に1ラベル）

| 出来事 | 欄 | いつ出るか |
|---|---|---|
| `Framed`「身振り」（ラベルを足した・表示専用・**版 Q1 ／ Q3 ／ QA だけ**） | `ActorId` ＝ ヒサ ／ `TargetId` ＝ 粛の保持者 ／ `Amount` ＝ 付ける層 ／ `Slot` ＝ 付ける前の層 | 粛の保持者が生きている間の**ヒサの手番**（矢面の指差しの後・号令の前）。**層より先に出る**——直後に層の数だけ `Feather`「増えた」（ミサがいれば）→ `MarkLayer`。手番の中の動作なので粛の下でも出る（`Sealed` は出ない） |

### 11-2. 1本の並び —— 身振り → 一斉射撃 → 粛が倒れる → 声が戻る（`hush303 memo 三人組 2 0 Q3 1`・試遊・標 三人組 × 第2波 × seed 0・勝ち T3・ダメージ ／ `Sealed` の行は省いた）

```
--- T1
Framed 身振り        ヒサ#4 → 粛の伝令#7  Amount 3  Slot 0     声を奪われたヒサが、黙って粛を指差す
Feather 増えた       ヒサ#4 → ミサ#2  Amount 2  Partner=粛の伝令#7
MarkLayer            ヒサ#4 → 粛の伝令#7  Amount 1
（Feather 増えた ／ MarkLayer がもう2組・層 3）
Feather 連射         ミサ#2  Amount 4                          ミサの一斉射撃（標の付いた粛へ・列を越える）
Feather 追う ×2 → Attack ／ Damage
Death                ミサ#2 → 粛の伝令#7                       粛が倒れる
Feather 乱射 ×2                                                （追う標が尽きた残りの羽）
MarkRally            ヒサ#4 → ミサ#2  Amount 6                 声が戻る（叫び）
MarkRally            ヒサ#4 → ザン#0  Amount 18
```

- 粛が倒れた後は、同じターンのうちに叫び（`MarkRally`）・仇討ち（`Reaction` の `Damage`）・羽（`FeatherMark`）が戻る。版では粛はほぼ必ず T1 のミサの手番で倒れる（「黙っていた時間」は T0〜T1 の前半だけ）。
- 規定（身振りなし）の第2波では、ヒサは `Framed`「黙る」のあと何もしない（`hush303 memo 三人組 2 0 S 2`）。

## 12. 第304期の追記 —— 庇いは自分が倒れる一撃を避ける（規定）・粛の版（`HushState`・版）

第304期に、ヒサの庇いを「自分まで倒れる一撃なら立たない」にした（規定・`CoverSkipLethal`）。**見送った一撃には何も出ない**（`Cover` が出ず、元の味方への `Damage` がそのまま出る）。見送っても「1戦1度」は残るので、同じ戦の後の倒れる一撃で `Cover` が出ることがある。
粛の版（HB 叩けば破れる ／ HD 抑えきれず砕ける ／ HC 諸刃 ／ HCD）は第2波の敵だけを差し替える版で、採否はポン（第304期の報告 §8）。例は `hush304 memo <台> <seed> <版 S|HB|HD10|HD20|HC|HCD> [最後のT]`（件数は `hush304 find`）。

### 12-1. 種類と欄（`HushState` を足した・表示専用・**版の粛の伝令だけ**・既定の第2波には出ない）

| `Text` | 欄 | いつ出るか |
|---|---|---|
| 「破れた」（HB） | `ActorId` ＝ 粛の保持者 ／ `Amount` 0 ／ `Slot` 0 | 粛の保持者が傷を受けた直後（**直前にその一撃の `Damage`**）。破れている間に受けた傷では出ない |
| 「戻った」（HB） | 同 | 粛の保持者の手番の始まり（痺れで潰れる手番でも出る）。この後また `Sealed`「粛」が出始める |
| 「ひび」（HD） | `ActorId` ＝ 粛の保持者 ／ `TargetId` ＝ 止められた駒 ／ `Amount` ＝ ひびの数（1, 2, …）／ `Slot` ＝ 砕けるまでの数（10 ／ 20） | 粛が単独の原因で行動を止めるたび（**直前にその行動の `Sealed`「粛」**）。HCD では巡礼騎士の斬り返しが止められても出る（`TargetId` ＝ 騎士） |
| 「砕けた」（HD） | `ActorId` ＝ 粛の保持者 ／ `Amount` ＝ `Slot` | 最後の「ひび」の直後。**以後その戦では `Sealed`「粛」が出ない**（粛の伝令は生きたまま、ただの駒として殴ってくる） |

HC ／ HCD の巡礼騎士の斬り返しは新しい出来事を足していない——騎士が出どころの `Reaction` の `Damage`（攻撃してきた駒へ・攻撃力の半分）として読める。

### 12-2. 1本の並び —— ひび → 砕けた → 割り込みが戻る（`hush304 memo ボス台 0 HD10 2`・試遊・標 ボス台 × 第2波 × seed 0・勝ち T3・抜粋）

```
--- T0
Sealed 粛            — → ミサ#2                               （開戦時の羽が止まる）
HushState ひび       粛の伝令#7 → ミサ#2  Amount 1  Slot 10    ひびが1つ
--- T1
Sealed 粛            — → ザン#0
HushState ひび       粛の伝令#7 → ザン#0  Amount 2  Slot 10    （仇討ちが止まるたびに1つずつ）
… ひび 3〜8（狙撃手 ／ 司祭長 ／ 騎士 ×2 ／ 粛の伝令の攻撃のたび）
--- T2
Attack ／ Damage     狙撃手#9 → ゴルム#1
Sealed 粛            — → ザン#0
HushState ひび       粛の伝令#7 → ザン#0  Amount 9  Slot 10
Sealed 粛            — → ザン#0
HushState ひび       粛の伝令#7 → ザン#0  Amount 10  Slot 10
HushState 砕けた     粛の伝令#7 → —  Amount 10  Slot 10        沈黙が砕ける（演出の見せ場・ログは Highlight）
Damage               ザン#0 → 狙撃手#9  Amount 30  Reaction    割り込みが戻る（同じ一撃への次の仇討ち）
MarkRally            ヒサ#4 → ザン#0 ／ ゴルム#1               叫びが戻る
FeatherMark 敵       ミサ#2 → 狙撃手#9 → Attack ／ Damage ／ Death   羽が戻る
…（司祭長 ／ 騎士 ×2 の攻撃のたびに 仇討ち → 叫び → 羽 が続く）
Framed あいつがやった ／ 濡れ衣 → ザン#0 → 粛の伝令#7 Reaction → ミサの羽 → Death 粛の伝令#7
```

- HD10 の第2波では、標軸の台はほぼ T1〜T2 に砕ける（ボス台 T2.0・守り型 ／ 循環 T1.0）。「ひび」は1戦に 10 本出るので、毎回大きく見せず、数（`Amount` ／ `Slot`）を残り何本と見せるのがよい。
- HB の並び（`hush304 memo 循環 0 HB 2`）: 粛の伝令への `Damage` → `HushState`「破れた」→ 仇討ち ／ 叫び ／ 羽 → 粛の伝令の手番の頭で `HushState`「戻った」。

## 13. 第306期の追記 —— 第2波の規定が HCD15 に（粛は 15 回で砕ける・騎士は斬り返す）

第306期に、本編 第2波（巡礼騎士団）を第304〜305期の版 HCD15 にした: **粛の伝令 ＝ `HusherHD15`**（粛 ＋ 単独の原因で 15 回止めたら砕ける）・**巡礼騎士 ×2 ＝ `KnightGR`**（斬り返し）。§12 の `HushState`（「ひび」「砕けた」）は**規定の第2波で出る**ようになった（「破れた」「戻った」は版 HB だけ）。出来事の種類は足していない。
意図とトーンは `design/CODEX_BRIEF_HUSH_SHATTER.md`（追補）。例は `hush306 memo <台> <seed> [最後のT]`（件数は `hush306 find`）。

### 13-1. 騎士の斬り返しの読み方（新しい出来事は足していない）

- **出た斬り返し** ＝ 巡礼騎士が出どころ（`ActorId`）の **`Reaction` の `Damage`**（攻撃してきた駒へ・攻撃力の半分）。第2波の騎士はほかにターン外の行動を持たないので、これだけで斬り返しと読める。巨躯（ゴルム）などが肩代わりすると、同じ斬り返しが `Damage`（`ff` ＝ 肩代わりの分）と `Damage`（元の相手）の2行に割れる。
- **止められた斬り返し** ＝ 騎士への `Sealed`「粛」→ `HushState`「ひび」（`TargetId` ＝ 騎士）。騎士1体につき1ターンに1本まで。

### 13-2. 1本の並び（`hush306 memo ボス台 0 2`・試遊・標 ボス台 × 規定の第2波 × seed 0・勝ち T4・抜粋）

§5 の3つ（①ひび 1〜15 → 砕けた → 割り込みが戻る ／ ②騎士を殴って入るひび ／ ③砕けた後の騎士の斬り返し）がこの1戦に揃う。

```
--- T0
HushState ひび       粛の伝令#7 → ミサ#2  Amount 1  Slot 15     （開戦時の羽が止まる）
--- T1
HushState ひび       粛の伝令#7 → ザン#0  Amount 2〜7            仇討ちが止まるたびに1本
HushState ひび       粛の伝令#7 → 巡礼騎士#5  Amount 8           ② 騎士を殴って入るひび（斬り返しが止められた）
HushState ひび       粛の伝令#7 → ザン#0  Amount 9
HushState ひび       粛の伝令#7 → 巡礼騎士#6  Amount 10          ② もう1体の騎士
--- T2
HushState ひび       粛の伝令#7 → ザン#0  Amount 11〜15
HushState 砕けた     粛の伝令#7 → —  Amount 15  Slot 15          ① 沈黙が砕ける
Damage               ザン#0 → 巡礼騎士#6  Amount 30  Reaction    ① 仇討ちが戻る
MarkRally            ヒサ#4 → ザン#0 ／ ゴルム#1                  ① 叫びが戻る
FeatherMark 敵       ミサ#2 → 巡礼騎士#6 → Attack ／ Damage 42    ① 羽が戻る
Damage               巡礼騎士#6 → ゴルム#1  Amount 11  Reaction ff   ③ 砕けた後の騎士の斬り返し（ゴルムが肩代わり）
Damage               巡礼騎士#6 → ミサ#2  Amount 2  Reaction
Death                ミサ#2 → 巡礼騎士#6
Damage               巡礼騎士#5 → ゴルム#1 ／ ミサ#2  Reaction    ③ もう1体の騎士も斬り返す
… Damage ザン#0 → 粛の伝令#7 Reaction → FeatherMark → Death 粛の伝令#7（砕けた同じターンに伝令が倒れる）
```

- 規定の第2波 × seed 0..49 で、①は ボス台 ／ 循環 ／ 守り型 50 戦・三人組 41 戦・標経済 47 戦、②はどの台も 15〜50 戦、③は 37〜49 戦に出る（`hush306 find`）。
- DemoApp の頭なしの起動口で、試遊・標 ボス台 × 第2波 × seed 0 は `DEMO_SMOKE_COMPLETE events=276 turns=4 won=True`、標経済 × seed 0 は `events=260 turns=7 won=False`（どちらも BattleSim の台本と同じ決着・完走）。
