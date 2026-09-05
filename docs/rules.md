# ノブ一覧

`dotnet run --project BattleSim -c Release 0 derive rules > docs/rules.md` の出力。手で編集しない。

**この表は全部が実装から derive されている**（第94期 (T1)）。型名・引数名・既定値は reflection、`測った診断` は `BattleSim/Program.cs` の `focusId == "..."` の区間、`期` は `design/PHASE*.md` の本文からそれぞれ引いた。**手で書いた項目は1つも無い。**

> **以後の指示書は既定値をここから引くこと。手で写さない。**
> 第93期は「`GatherRule` / `IgniteRule` は既定 off」と書いて測り始めたが、**どちらも第89・90期に採用済みで既定 on** だった。

## 1. `BattleEngine.Run` の引数（ノブの正本）

| # | 引数名 | 型 | 既定値（実装） | `= default(T)` | 測った診断 | 期（design/） | CLAUDE.md |
|--:|---|---|---|:-:|---|---|:-:|
| 1 | `colossus` | `ColossusRule` | `ColossusRule { Percent = 90, DamagePerGain = 4, Regurgitate = True, Slumber = False, SlumberThreshold = 60, Refund = True, RefundPercent = 25 }` |  | `swap` / `gullet` / `guard` / `whet` / `miasma` | 第35期〜（10 期） | ○ |
| 2 | `yoke` | `YokeRule` | `YokeRule { Cap = 25, Active = True }` |  | `yoke` / `replay` / `wave2` | 第35期〜（7 期） | ○ |
| 3 | `hush` | `HushRule` | `HushRule { Active = True }` |  | `yoke` / `hush` / `replay` / `wave2` | 第35期〜（8 期） | ○ |
| 4 | `martyr` | `MartyrRule` | `MartyrRule { RedirectPercent = 75 }` |  | `guard` / `gather` | 第35期〜（6 期） | ○ |
| 5 | `expose` | `ExposeRule` | `ExposeRule { MaxPerBattle = 3 }` |  | `expose` / `creak3` | 第40期〜（6 期） | ○ |
| 6 | `shove` | `ShoveRule` | `ShoveRule { Penalty = 2 }` |  | `shove` | 第41期〜（6 期） | ○ |
| 7 | `bear` | `BearRule` | `BearRule { ArmorPerDull = 2 }` |  | `dull` | 第42期〜（3 期） | ○ |
| 8 | `relay` | `RelayRule` | `RelayRule { TransferPercent = 100 }` |  | `dull` / `relay` | 第43期〜（3 期） | ○ |
| 9 | `slander` | `SlanderRule` | `SlanderRule { Penalty = 0 }` | ○ | `relay` / `slander` | 第44期〜（3 期） | ○ |
| 10 | `overbear` | `OverbearRule` | `OverbearRule { Drain = 2 }` |  | `slander` / `overbear` | 第46期〜（2 期） |  |
| 11 | `scale` | `ScaleRule` | `ScaleRule { CostPerAttack = 1 }` |  | `scale` / `scapegoat` / `divert` / `favor` / `miasma` / `goad` / `finisher` | 第47期〜（11 期） | ○ |
| 12 | `scapegoat` | `ScapegoatRule` | `ScapegoatRule { Threshold = 3, Audit = False }` |  | `scapegoat` | 第49期〜（4 期） |  |
| 13 | `divert` | `DivertRule` | `DivertRule { TargetCount = 1, SelfMark = True, Audit = False }` |  | `divert` | 第50期〜（5 期） | ○ |
| 14 | `goad` | `GoadRule` | `GoadRule { Boost = 4, Mark = True }` |  | `derive` / `guard` / `whet` / `goad` | 第52期〜（4 期） | ○ |
| 15 | `finisher` | `FinisherRule` | `FinisherRule { Multiplier = 2, Consume = True }` |  | `finisher` | 第53期〜（3 期） | ○ |
| 16 | `favor` | `FavorRule` | `FavorRule { Gain = 4, Loss = 2 }` |  | `favor` / `turn` | 第58期〜（4 期） | ○ |
| 17 | `blaze` | `BlazeRule` | `BlazeRule { Targets = Both, Allies = True, Foes = True }` |  | `blaze` / `demo` | 第59期〜（3 期） | ○ |
| 18 | `funnel` | `FunnelRule` | `FunnelRule { Slowest = True, Both = False }` |  | `funnel` / `cross` | 第62期〜（5 期） | ○ |
| 19 | `whetMask` | `WhetMask` | `WhetMask { Bits = 0 }` |  | `creak3` / `spend` | 第65期〜（2 期） |  |
| 20 | `creak` | `CreakRule` | `CreakRule { Threshold = 0, Source = Whet }` | ○ | `creak` / `traits` / `creak3` | 第66期〜（5 期） | ○ |
| 21 | `sever` | `SeverRule` | `SeverRule { Wait = Swing, Threshold = 2 }` |  | `wcost` / `cross` | 第74期〜（4 期） | ○ |
| 22 | `thinBlade` | `ThinBladeRule` | `ThinBladeRule { Cost = Always }` | ○ | `wcost` / `blade` / `cross` | 第75期〜（4 期） | ○ |
| 23 | `thorn` | `ThornRule` | `ThornRule { Wound = None }` | ○ | `derive` / `breadth` / `thorn` / `suture2` / `gauge` / `cross` / `demo` | 第84期〜（9 期） | ○ |
| 24 | `suture` | `SutureRule` | `SutureRule { Side = Both }` |  | `suture2` / `mender` / `gauge` / `gather` / `soak` / `cross` | 第85期〜（7 期） | ○ |
| 25 | `spillWound` | `SpillWoundRule` | `SpillWoundRule { Enabled = True, Scope = All }` |  | `suture2` / `mender` / `gauge` / `gather` / `soak` / `cross` | 第85期〜（7 期） | ○ |
| 26 | `mend` | `MendRule` | `MendRule { Side = Wound }` |  | `mender` / `gauge` / `cross` / `demo` | 第86期〜（7 期） | ○ |
| 27 | `woundIgnite` | `IgniteRule` | `IgniteRule { Enabled = True }` |  | `audit` / `derive` / `mender` / `blaze2` / `gauge` / `gather` / `deep` / `soak` / `cross` / `demo` | 第87期〜（5 期） | ○ |
| 28 | `gather` | `GatherRule` | `GatherRule { Enabled = True }` |  | `audit` / `derive` / `gather` / `deep` / `soak` / `cross` / `demo` | 第89期〜（6 期） | ○ |
| 29 | `soak` | `SoakRule` | `SoakRule { Poison = True, Burn = False }` |  | `derive` / `soak` / `cross` | 第90期〜（5 期） | ○ |
| 30 | `deep` | `DeepRule` | `DeepRule { Enabled = False }` | ○ | `derive` / `deep` | 第93期〜（2 期） | ○ |

引数 30 本（`verbose` と観測子を除く）。

**`= default(T)` は「その規則が既定で何もしない」の機械的な手がかりであって、判定ではない。**
採否そのものは**既定値の列**を読むこと——`ThornRule { Wound = None }` は残置、`SoakRule { Poison = True, Burn = False }` は毒側だけ採用、という具合に既定値が全部を語る。

## 2. `Default` を持つ型の全数（`Run` の引数に出ないものを含む）

| 型 | 既定値 | `Run` の引数 | 測った診断 | 期（design/） |
|---|---|:-:|---|---|
| `BearRule` | `BearRule { ArmorPerDull = 2 }` | ○ | `dull` | 第42期〜（3 期） |
| `BlazeRule` | `BlazeRule { Targets = Both, Allies = True, Foes = True }` | ○ | `blaze` / `demo` | 第59期〜（3 期） |
| `ColossusRule` | `ColossusRule { Percent = 90, DamagePerGain = 4, Regurgitate = True, Slumber = False, SlumberThreshold = 60, Refund = True, RefundPercent = 25 }` | ○ | `swap` / `gullet` / `guard` / `whet` / `miasma` | 第35期〜（10 期） |
| `CreakRule` | `CreakRule { Threshold = 0, Source = Whet }` | ○ | `creak` / `traits` / `creak3` | 第66期〜（5 期） |
| `DeepRule` | `DeepRule { Enabled = False }` | ○ | `derive` / `deep` | 第93期〜（2 期） |
| `DivertRule` | `DivertRule { TargetCount = 1, SelfMark = True, Audit = False }` | ○ | `divert` | 第50期〜（5 期） |
| `ExposeRule` | `ExposeRule { MaxPerBattle = 3 }` | ○ | `expose` / `creak3` | 第40期〜（6 期） |
| `FavorRule` | `FavorRule { Gain = 4, Loss = 2 }` | ○ | `favor` / `turn` | 第58期〜（4 期） |
| `FinisherRule` | `FinisherRule { Multiplier = 2, Consume = True }` | ○ | `finisher` | 第53期〜（3 期） |
| `FunnelRule` | `FunnelRule { Slowest = True, Both = False }` | ○ | `funnel` / `cross` | 第62期〜（5 期） |
| `GatherRule` | `GatherRule { Enabled = True }` | ○ | `audit` / `derive` / `gather` / `deep` / `soak` / `cross` / `demo` | 第89期〜（6 期） |
| `GoadRule` | `GoadRule { Boost = 4, Mark = True }` | ○ | `derive` / `guard` / `whet` / `goad` | 第52期〜（4 期） |
| `HushRule` | `HushRule { Active = True }` | ○ | `yoke` / `hush` / `replay` / `wave2` | 第35期〜（8 期） |
| `IgniteRule` | `IgniteRule { Enabled = True }` | ○ | `audit` / `derive` / `mender` / `blaze2` / `gauge` / `gather` / `deep` / `soak` / `cross` / `demo` | 第87期〜（5 期） |
| `MartyrRule` | `MartyrRule { RedirectPercent = 75 }` | ○ | `guard` / `gather` | 第35期〜（6 期） |
| `MendRule` | `MendRule { Side = Wound }` | ○ | `mender` / `gauge` / `cross` / `demo` | 第86期〜（7 期） |
| `OverbearRule` | `OverbearRule { Drain = 2 }` | ○ | `slander` / `overbear` | 第46期〜（2 期） |
| `RelayRule` | `RelayRule { TransferPercent = 100 }` | ○ | `dull` / `relay` | 第43期〜（3 期） |
| `ScaleRule` | `ScaleRule { CostPerAttack = 1 }` | ○ | `scale` / `scapegoat` / `divert` / `favor` / `miasma` / `goad` / `finisher` | 第47期〜（11 期） |
| `ScapegoatRule` | `ScapegoatRule { Threshold = 3, Audit = False }` | ○ | `scapegoat` | 第49期〜（4 期） |
| `SeverRule` | `SeverRule { Wait = Swing, Threshold = 2 }` | ○ | `wcost` / `cross` | 第74期〜（4 期） |
| `ShoveRule` | `ShoveRule { Penalty = 2 }` | ○ | `shove` | 第41期〜（6 期） |
| `SlanderRule` | `SlanderRule { Penalty = 0 }` | ○ | `relay` / `slander` | 第44期〜（3 期） |
| `SoakRule` | `SoakRule { Poison = True, Burn = False }` | ○ | `derive` / `soak` / `cross` | 第90期〜（5 期） |
| `SpillWoundRule` | `SpillWoundRule { Enabled = True, Scope = All }` | ○ | `suture2` / `mender` / `gauge` / `gather` / `soak` / `cross` | 第85期〜（7 期） |
| `SutureRule` | `SutureRule { Side = Both }` | ○ | `suture2` / `mender` / `gauge` / `gather` / `soak` / `cross` | 第85期〜（7 期） |
| `ThinBladeRule` | `ThinBladeRule { Cost = Always }` | ○ | `wcost` / `blade` / `cross` | 第75期〜（4 期） |
| `ThornRule` | `ThornRule { Wound = None }` | ○ | `derive` / `breadth` / `thorn` / `suture2` / `gauge` / `cross` / `demo` | 第84期〜（9 期） |
| `WhetMask` | `WhetMask { Bits = 0 }` | ○ | `creak3` / `spend` | 第65期〜（2 期） |
| `YokeRule` | `YokeRule { Cap = 25, Active = True }` | ○ | `yoke` / `replay` / `wave2` | 第35期〜（7 期） |

30 型。

## 3. 規則が使う列挙型

**`default(T)` は必ず値 0 の要素**なので、この並びが上の `= default(T)` 列の意味を決めている。

| 列挙型 | 値（0 から） |
|---|---|
| `BlazeTargets` | `None`=0 / `AllyOnly`=1 / `Both`=2 / `FoeOnly`=3 |
| `CreakSource` | `Whet`=0 / `Bonus`=1 / `Both`=2 |
| `MendSide` | `Plain`=0 / `Wound`=1 |
| `SeverWait` | `Yield`=0 / `Swing`=1 |
| `SpillScope` | `All`=0 / `Dense`=1 |
| `SutureSide` | `Foe`=0 / `Both`=1 |
| `ThinBladeCost` | `Always`=0 / `Unwounded`=1 / `Carving`=2 / `Slower`=3 |
| `ThornWound` | `None`=0 / `Foe`=1 / `Both`=2 |

8 型。
