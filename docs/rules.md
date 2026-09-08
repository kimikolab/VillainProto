# ノブ一覧

`dotnet run --project BattleSim -c Release 0 derive rules > docs/rules.md` の出力。手で編集しない。

**この表は全部が実装から derive されている**（第94期 (T1)）。型名・引数名・既定値は reflection、`測った診断` は `BattleSim/Program.cs` の `focusId == "..."` の区間、`期` は `design/PHASE*.md` の本文からそれぞれ引いた。**手で書いた項目は1つも無い。**

> **以後の指示書は既定値をここから引くこと。手で写さない。**
> 第93期は「`GatherRule` / `IgniteRule` は既定 off」と書いて測り始めたが、**どちらも第89・90期に採用済みで既定 on** だった。

## 1. `BattleEngine.Run` の引数（ノブの正本）

| # | 引数名 | 型 | 既定値（実装） | `= default(T)` | 測った診断 | 期（design/） | CLAUDE.md |
|--:|---|---|---|:-:|---|---|:-:|
| 1 | `colossus` | `ColossusRule` | `ColossusRule { Percent = 90, DamagePerGain = 4, Regurgitate = True, Slumber = False, SlumberThreshold = 60, Refund = True, RefundPercent = 25 }` |  | `swap` / `gullet` / `guard` / `whet` / `miasma` / `ledger` | 第35期〜（11 期） | ○ |
| 2 | `yoke` | `YokeRule` | `YokeRule { Cap = 25, Active = True }` |  | `curse` / `yoke` / `replay` / `wave2` / `ledger` | 第35期〜（7 期） | ○ |
| 3 | `hush` | `HushRule` | `HushRule { Active = True }` |  | `curse` / `yoke` / `hush` / `replay` / `wave2` / `ledger` | 第35期〜（9 期） | ○ |
| 4 | `martyr` | `MartyrRule` | `MartyrRule { RedirectPercent = 75 }` |  | `curse` / `guard` / `gather` / `ledger` | 第35期〜（6 期） | ○ |
| 5 | `expose` | `ExposeRule` | `ExposeRule { MaxPerBattle = 3 }` |  | `expose` / `creak3` / `ledger` | 第40期〜（6 期） | ○ |
| 6 | `shove` | `ShoveRule` | `ShoveRule { Penalty = 2 }` |  | `shove` / `ledger` | 第41期〜（6 期） | ○ |
| 7 | `bear` | `BearRule` | `BearRule { ArmorPerDull = 2 }` |  | `curse` / `dull` / `ledger` | 第42期〜（4 期） | ○ |
| 8 | `relay` | `RelayRule` | `RelayRule { TransferPercent = 100 }` |  | `curse` / `dull` / `relay` | 第43期〜（3 期） | ○ |
| 9 | `slander` | `SlanderRule` | `SlanderRule { Penalty = 0 }` | ○ | `relay` / `slander` | 第44期〜（3 期） | ○ |
| 10 | `overbear` | `OverbearRule` | `OverbearRule { Drain = 2 }` |  | `slander` / `overbear` | 第46期〜（2 期） |  |
| 11 | `scale` | `ScaleRule` | `ScaleRule { CostPerAttack = 1 }` |  | `scale` / `scapegoat` / `divert` / `favor` / `miasma` / `goad` / `finisher` / `ledger` | 第47期〜（11 期） | ○ |
| 12 | `scapegoat` | `ScapegoatRule` | `ScapegoatRule { Threshold = 3, Audit = False }` |  | `scapegoat` | 第49期〜（4 期） |  |
| 13 | `divert` | `DivertRule` | `DivertRule { TargetCount = 1, SelfMark = True, Audit = False }` |  | `divert` | 第50期〜（5 期） | ○ |
| 14 | `goad` | `GoadRule` | `GoadRule { Boost = 4, Mark = True }` |  | `derive` / `guard` / `whet` / `goad` / `ledger` | 第52期〜（4 期） | ○ |
| 15 | `finisher` | `FinisherRule` | `FinisherRule { Multiplier = 2, Consume = True }` |  | `finisher` / `ledger` | 第53期〜（3 期） | ○ |
| 16 | `favor` | `FavorRule` | `FavorRule { Gain = 4, Loss = 2 }` |  | `curse` / `favor` / `turn` / `ledger` | 第58期〜（4 期） | ○ |
| 17 | `blaze` | `BlazeRule` | `BlazeRule { Targets = Both, Allies = True, Foes = True }` |  | `blaze` / `ledger` | 第59期〜（5 期） | ○ |
| 18 | `funnel` | `FunnelRule` | `FunnelRule { Slowest = True, Both = False }` |  | `funnel` / `cross` | 第62期〜（5 期） | ○ |
| 19 | `whetMask` | `WhetMask` | `WhetMask { Bits = 0 }` |  | `creak3` / `spend` | 第65期〜（2 期） |  |
| 20 | `creak` | `CreakRule` | `CreakRule { Threshold = 0, Source = Whet }` | ○ | `creak` / `traits` / `creak3` | 第66期〜（6 期） | ○ |
| 21 | `sever` | `SeverRule` | `SeverRule { Wait = Swing, Threshold = 2 }` |  | `wcost` / `ledger` / `cross` | 第74期〜（4 期） | ○ |
| 22 | `thinBlade` | `ThinBladeRule` | `ThinBladeRule { Cost = Always }` | ○ | `wcost` / `blade` / `ledger` / `cross` | 第75期〜（4 期） | ○ |
| 23 | `thorn` | `ThornRule` | `ThornRule { Wound = None }` | ○ | `derive` / `curse` / `breadth` / `thorn` / `suture2` / `gauge` / `cross` | 第84期〜（16 期） | ○ |
| 24 | `suture` | `SutureRule` | `SutureRule { Side = Both }` |  | `curse` / `hex` / `suture2` / `mender` / `gauge` / `gather` / `soak` / `taillight` / `hold2` / `ledger` / `cross` | 第85期〜（11 期） | ○ |
| 25 | `sutureFire` | `SutureFireRule` | `SutureFireRule { Fire = Swing }` | ○ | `taillight` / `hold2` | 第107期〜（2 期） | ○ |
| 26 | `spillWound` | `SpillWoundRule` | `SpillWoundRule { Enabled = True, Scope = All }` |  | `curse` / `hex` / `suture2` / `mender` / `gauge` / `gather` / `soak` / `hold2` / `ledger` / `cross` | 第85期〜（9 期） | ○ |
| 27 | `mend` | `MendRule` | `MendRule { Side = Wound }` |  | `curse` / `hex` / `mender` / `gauge` / `hold2` / `ledger` / `cross` | 第86期〜（11 期） | ○ |
| 28 | `woundIgnite` | `IgniteRule` | `IgniteRule { Enabled = True }` |  | `audit` / `derive` / `curse` / `mender` / `blaze2` / `gauge` / `gather` / `deep` / `soak` / `ledger` / `cross` / `demo` | 第87期〜（9 期） | ○ |
| 29 | `gather` | `GatherRule` | `GatherRule { Enabled = True }` |  | `audit` / `derive` / `curse` / `hex` / `encore` / `gather` / `deep` / `soak` / `ledger` / `cross` / `demo` | 第89期〜（10 期） | ○ |
| 30 | `soak` | `SoakRule` | `SoakRule { Poison = True, Burn = False, DullPerKind = 1 }` |  | `derive` / `curse` / `hex` / `soak` / `ledger` / `cross` | 第90期〜（9 期） | ○ |
| 31 | `deep` | `DeepRule` | `DeepRule { Enabled = False }` | ○ | `derive` / `curse` / `hex` / `encore` / `deep` | 第93期〜（7 期） | ○ |
| 32 | `curse` | `CurseRule` | `CurseRule { Enabled = False, SharePercent = 50 }` |  | `curse` / `hex` | 第95期〜（2 期） | ○ |
| 33 | `betray` | `BetrayRule` | `BetrayRule { Enabled = True, Respawn = True }` |  | `betray` / `tempo` / `ledger` | 第103期〜（3 期） | ○ |
| 34 | `encore` | `EncoreRule` | `EncoreRule { Enabled = True }` |  | `encore` / `tempo` / `tomo` / `hold2` / `ledger` | 第104期〜（4 期） | ○ |
| 35 | `rage` | `RageRule` | `RageRule { Mode = Amount, Gain = 3 }` |  | `tempo` / `hold` / `hold2` | 第106期〜（3 期） | ○ |
| 36 | `menderCost` | `MenderCostRule` | `MenderCostRule { Percent = 50 }` |  | `tempo` / `hold` / `hold2` / `ledger` | 第106期〜（2 期） | ○ |
| 37 | `loose` | `LooseRule` | `LooseRule { Shove = True }` |  | `tempo` / `hold` / `hold2` / `ledger` | 第106期〜（2 期） | ○ |
| 38 | `taillight` | `TaillightRule` | `TaillightRule { Mode = OwnTurnWindow }` |  | `tomo` / `ledger` | 第110期 | ○ |

引数 38 本（`verbose` と観測子を除く）。

**`= default(T)` は「その規則が既定で何もしない」の機械的な手がかりであって、判定ではない。**
採否そのものは**既定値の列**を読むこと——`ThornRule { Wound = None }` は残置、`SoakRule { Poison = True, Burn = False }` は毒側だけ採用、という具合に既定値が全部を語る。

## 2. `Default` を持つ型の全数（`Run` の引数に出ないものを含む）

| 型 | 既定値 | `Run` の引数 | 測った診断 | 期（design/） |
|---|---|:-:|---|---|
| `BearRule` | `BearRule { ArmorPerDull = 2 }` | ○ | `curse` / `dull` / `ledger` | 第42期〜（4 期） |
| `BetrayRule` | `BetrayRule { Enabled = True, Respawn = True }` | ○ | `betray` / `tempo` / `ledger` | 第103期〜（3 期） |
| `BlazeRule` | `BlazeRule { Targets = Both, Allies = True, Foes = True }` | ○ | `blaze` / `ledger` | 第59期〜（5 期） |
| `BoundaryRule` | `BoundaryRule { Choice = None, Plan = , Active = False }` |  | `chain` / `choice` | 第102期〜（2 期） |
| `ColossusRule` | `ColossusRule { Percent = 90, DamagePerGain = 4, Regurgitate = True, Slumber = False, SlumberThreshold = 60, Refund = True, RefundPercent = 25 }` | ○ | `swap` / `gullet` / `guard` / `whet` / `miasma` / `ledger` | 第35期〜（11 期） |
| `CreakRule` | `CreakRule { Threshold = 0, Source = Whet }` | ○ | `creak` / `traits` / `creak3` | 第66期〜（6 期） |
| `CurseRule` | `CurseRule { Enabled = False, SharePercent = 50 }` | ○ | `curse` / `hex` | 第95期〜（2 期） |
| `DeepRule` | `DeepRule { Enabled = False }` | ○ | `derive` / `curse` / `hex` / `encore` / `deep` | 第93期〜（7 期） |
| `DivertRule` | `DivertRule { TargetCount = 1, SelfMark = True, Audit = False }` | ○ | `divert` | 第50期〜（5 期） |
| `EncoreRule` | `EncoreRule { Enabled = True }` | ○ | `encore` / `tempo` / `tomo` / `hold2` / `ledger` | 第104期〜（4 期） |
| `ExposeRule` | `ExposeRule { MaxPerBattle = 3 }` | ○ | `expose` / `creak3` / `ledger` | 第40期〜（6 期） |
| `FavorRule` | `FavorRule { Gain = 4, Loss = 2 }` | ○ | `curse` / `favor` / `turn` / `ledger` | 第58期〜（4 期） |
| `FinisherRule` | `FinisherRule { Multiplier = 2, Consume = True }` | ○ | `finisher` / `ledger` | 第53期〜（3 期） |
| `FunnelRule` | `FunnelRule { Slowest = True, Both = False }` | ○ | `funnel` / `cross` | 第62期〜（5 期） |
| `GatherRule` | `GatherRule { Enabled = True }` | ○ | `audit` / `derive` / `curse` / `hex` / `encore` / `gather` / `deep` / `soak` / `ledger` / `cross` / `demo` | 第89期〜（10 期） |
| `GoadRule` | `GoadRule { Boost = 4, Mark = True }` | ○ | `derive` / `guard` / `whet` / `goad` / `ledger` | 第52期〜（4 期） |
| `HushRule` | `HushRule { Active = True }` | ○ | `curse` / `yoke` / `hush` / `replay` / `wave2` / `ledger` | 第35期〜（9 期） |
| `IgniteRule` | `IgniteRule { Enabled = True }` | ○ | `audit` / `derive` / `curse` / `mender` / `blaze2` / `gauge` / `gather` / `deep` / `soak` / `ledger` / `cross` / `demo` | 第87期〜（9 期） |
| `LooseRule` | `LooseRule { Shove = True }` | ○ | `tempo` / `hold` / `hold2` / `ledger` | 第106期〜（2 期） |
| `MartyrRule` | `MartyrRule { RedirectPercent = 75 }` | ○ | `curse` / `guard` / `gather` / `ledger` | 第35期〜（6 期） |
| `MendRule` | `MendRule { Side = Wound }` | ○ | `curse` / `hex` / `mender` / `gauge` / `hold2` / `ledger` / `cross` | 第86期〜（11 期） |
| `MenderCostRule` | `MenderCostRule { Percent = 50 }` | ○ | `tempo` / `hold` / `hold2` / `ledger` | 第106期〜（2 期） |
| `OverbearRule` | `OverbearRule { Drain = 2 }` | ○ | `slander` / `overbear` | 第46期〜（2 期） |
| `RageRule` | `RageRule { Mode = Amount, Gain = 3 }` | ○ | `tempo` / `hold` / `hold2` | 第106期〜（3 期） |
| `RecoverRule` | `RecoverRule { HpPercent = 0, ReviveDead = False, Active = False }` |  | `choice` / `recover` | 第101期〜（2 期） |
| `RelayRule` | `RelayRule { TransferPercent = 100 }` | ○ | `curse` / `dull` / `relay` | 第43期〜（3 期） |
| `ScaleRule` | `ScaleRule { CostPerAttack = 1 }` | ○ | `scale` / `scapegoat` / `divert` / `favor` / `miasma` / `goad` / `finisher` / `ledger` | 第47期〜（11 期） |
| `ScapegoatRule` | `ScapegoatRule { Threshold = 3, Audit = False }` | ○ | `scapegoat` | 第49期〜（4 期） |
| `SeverRule` | `SeverRule { Wait = Swing, Threshold = 2 }` | ○ | `wcost` / `ledger` / `cross` | 第74期〜（4 期） |
| `ShoveRule` | `ShoveRule { Penalty = 2 }` | ○ | `shove` / `ledger` | 第41期〜（6 期） |
| `SlanderRule` | `SlanderRule { Penalty = 0 }` | ○ | `relay` / `slander` | 第44期〜（3 期） |
| `SoakRule` | `SoakRule { Poison = True, Burn = False, DullPerKind = 1 }` | ○ | `derive` / `curse` / `hex` / `soak` / `ledger` / `cross` | 第90期〜（9 期） |
| `SpillWoundRule` | `SpillWoundRule { Enabled = True, Scope = All }` | ○ | `curse` / `hex` / `suture2` / `mender` / `gauge` / `gather` / `soak` / `hold2` / `ledger` / `cross` | 第85期〜（9 期） |
| `SutureFireRule` | `SutureFireRule { Fire = Swing }` | ○ | `taillight` / `hold2` | 第107期〜（2 期） |
| `SutureRule` | `SutureRule { Side = Both }` | ○ | `curse` / `hex` / `suture2` / `mender` / `gauge` / `gather` / `soak` / `taillight` / `hold2` / `ledger` / `cross` | 第85期〜（11 期） |
| `TaillightRule` | `TaillightRule { Mode = OwnTurnWindow }` | ○ | `tomo` / `ledger` | 第110期 |
| `ThinBladeRule` | `ThinBladeRule { Cost = Always }` | ○ | `wcost` / `blade` / `ledger` / `cross` | 第75期〜（4 期） |
| `ThornRule` | `ThornRule { Wound = None }` | ○ | `derive` / `curse` / `breadth` / `thorn` / `suture2` / `gauge` / `cross` | 第84期〜（16 期） |
| `WhetMask` | `WhetMask { Bits = 0 }` | ○ | `creak3` / `spend` | 第65期〜（2 期） |
| `YokeRule` | `YokeRule { Cap = 25, Active = True }` | ○ | `curse` / `yoke` / `replay` / `wave2` / `ledger` | 第35期〜（7 期） |

40 型。

## 3. 規則が使う列挙型

**`default(T)` は必ず値 0 の要素**なので、この並びが上の `= default(T)` 列の意味を決めている。

| 列挙型 | 値（0 から） |
|---|---|
| `BlazeTargets` | `None`=0 / `AllyOnly`=1 / `Both`=2 / `FoeOnly`=3 |
| `BoundaryChoice` | `None`=0 / `Revive`=1 / `Heal`=2 / `Carry`=3 |
| `CreakSource` | `Whet`=0 / `Bonus`=1 / `Both`=2 |
| `MendSide` | `Plain`=0 / `Wound`=1 |
| `RageMode` | `Amount`=0 / `Count`=1 |
| `SeverWait` | `Yield`=0 / `Swing`=1 |
| `SpillScope` | `All`=0 / `Dense`=1 |
| `SutureFire` | `Swing`=0 / `OnWound`=1 |
| `SutureSide` | `Foe`=0 / `Both`=1 |
| `ThinBladeCost` | `Always`=0 / `Unwounded`=1 / `Carving`=2 / `Slower`=3 |
| `ThornWound` | `None`=0 / `Foe`=1 / `Both`=2 |
| `YieldMode` | `OwnTurn`=0 / `OwnTurnWindow`=1 / `Immediate`=2 |

12 型。
