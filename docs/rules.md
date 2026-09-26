# ノブ一覧

`dotnet run --project BattleSim -c Release 0 derive rules > docs/rules.md` の出力。手で編集しない。

**この表は全部が実装から derive されている**（第94期 (T1)）。型名・引数名・既定値は reflection、`測った診断` は `BattleSim/Program.cs` の `focusId == "..."` の区間（**別ファイルの診断はそこが名前を挙げているクラスで結ぶ**——第142期に全モードが `BattleSim/Modes/` へ出たので、いまはほぼ全部がこの経路）、`初出` は `design/PHASE*.md` の本文からそれぞれ引いた（**初出の期だけ。範囲は出さない**——第132期 段0-b）。**手で書いた項目は1つも無い。**

> **以後の指示書は既定値をここから引くこと。手で写さない。**
> 第93期は「`GatherRule` / `IgniteRule` は既定 off」と書いて測り始めたが、**どちらも第89・90期に採用済みで既定 on** だった。

## 1. `BattleEngine.Run` の引数（ノブの正本）

| # | 引数名 | 型 | 既定値（実装） | `= default(T)` | 測った診断 | 初出（design/） | CLAUDE.md / LESSONS |
|--:|---|---|---|:-:|---|---|:-:|
| 1 | `colossus` | `ColossusRule` | `ColossusRule { Percent = 90, DamagePerGain = 4, Regurgitate = True, Slumber = False, SlumberThreshold = 60, Refund = True, RefundPercent = 25 }` |  | `gullet` / `whet` / `miasma` / `ledger` | 第35期 | ○ |
| 2 | `yoke` | `YokeRule` | `YokeRule { Cap = 25, Active = True }` |  | `curse` / `yoke` / `goad` / `wave2` / `ledger` / `parry` / `brace` | 第35期 | ○ |
| 3 | `hush` | `HushRule` | `HushRule { Active = True }` |  | `curse` / `hush` / `goad` / `wave2` / `ledger` | 第35期 | ○ |
| 4 | `martyr` | `MartyrRule` | `MartyrRule { RedirectPercent = 75 }` |  | `curse` / `guard` / `gather` / `ledger` / `wall` | 第35期 | ○ |
| 5 | `expose` | `ExposeRule` | `ExposeRule { MaxPerBattle = 3 }` |  | `expose` / `creak3` / `ledger` | 第40期 | ○ |
| 6 | `shove` | `ShoveRule` | `ShoveRule { Penalty = 2 }` |  | `shove` / `ledger` | 第41期 | ○ |
| 7 | `bear` | `BearRule` | `BearRule { ArmorPerDull = 2 }` |  | `curse` / `dull` / `ledger` | 第42期 | ○ |
| 8 | `relay` | `RelayRule` | `RelayRule { TransferPercent = 100 }` |  | `curse` / `relay` | 第43期 | ○ |
| 9 | `slander` | `SlanderRule` | `SlanderRule { Penalty = 0 }` | ○ | `slander` | 第44期 | ○ |
| 10 | `overbear` | `OverbearRule` | `OverbearRule { Drain = 2 }` |  | `overbear` / `wildfire` | 第46期 |  |
| 11 | `scale` | `ScaleRule` | `ScaleRule { CostPerAttack = 1 }` |  | `scale` / `scapegoat` / `divert` / `favor` / `miasma` / `goad` / `finisher` / `ledger` | 第47期 | ○ |
| 12 | `scapegoat` | `ScapegoatRule` | `ScapegoatRule { Threshold = 3, Audit = False }` |  | `scapegoat` | 第49期 |  |
| 13 | `divert` | `DivertRule` | `DivertRule { TargetCount = 1, SelfMark = True, Audit = False }` |  | `divert` / `survive` / `wildfire` / `mark` | 第50期 | ○ |
| 14 | `goad` | `GoadRule` | `GoadRule { Boost = 4, Mark = True }` |  | `derive` / `whet` / `goad` / `ledger` / `mark` | 第52期 | ○ |
| 15 | `finisher` | `FinisherRule` | `FinisherRule { Multiplier = 2, Consume = True }` |  | `finisher` / `ledger` / `survive` / `wildfire` / `mark` | 第53期 | ○ |
| 16 | `favor` | `FavorRule` | `FavorRule { Gain = 4, Loss = 2 }` |  | `curse` / `favor` / `turn` / `ledger` | 第58期 | ○ |
| 17 | `blaze` | `BlazeRule` | `BlazeRule { Targets = Both, Allies = True, Foes = True }` |  | `blaze` / `ledger` / `ember` | 第59期 | ○ |
| 18 | `funnel` | `FunnelRule` | `FunnelRule { Slowest = True, Both = False }` |  | `funnel` / `cross` | 第62期 | ○ |
| 19 | `whetMask` | `WhetMask` | `WhetMask { Bits = 0 }` |  | `spend` | 第65期 |  |
| 20 | `creak` | `CreakRule` | `CreakRule { Threshold = 0, Source = Whet }` | ○ | `creak` / `pairs` / `creak3` | 第66期 | ○ |
| 21 | `sever` | `SeverRule` | `SeverRule { Wait = Swing, Threshold = 2 }` |  | `wcost` / `ledger` / `lit` / `wound2` / `cross` | 第74期 | ○ |
| 22 | `thinBlade` | `ThinBladeRule` | `ThinBladeRule { Cost = Always }` | ○ | `blade` / `ledger` / `cross` | 第75期 | ○ |
| 23 | `thorn` | `ThornRule` | `ThornRule { Wound = None }` | ○ | `derive` / `curse` / `thorn` / `suture2` / `gauge` / `wound2` / `cross` | 第84期 | ○ |
| 24 | `suture` | `SutureRule` | `SutureRule { Side = Both }` |  | `curse` / `hex` / `suture2` / `mender` / `gauge` / `gather` / `soak` / `taillight` / `hold2` / `ledger` / `wound2` / `cross` | 第85期 | ○ |
| 25 | `sutureFire` | `SutureFireRule` | `SutureFireRule { Fire = Swing }` | ○ | `taillight` / `hold2` | 第107期 | ○ |
| 26 | `spillWound` | `SpillWoundRule` | `SpillWoundRule { Enabled = False, Scope = All }` | ○ | `curse` / `hex` / `suture2` / `mender` / `gauge` / `gather` / `soak` / `hold2` / `ledger` / `wound2` / `parry` / `cross` | 第85期 | ○ |
| 27 | `mend` | `MendRule` | `MendRule { Side = Plain }` | ○ | `curse` / `hex` / `mender` / `gauge` / `hold2` / `ledger` / `wound2` / `cross` | 第86期 | ○ |
| 28 | `woundIgnite` | `IgniteRule` | `IgniteRule { Enabled = True }` |  | `derive` / `curse` / `blaze2` / `gauge` / `gather` / `deep` / `soak` / `ledger` / `wound2` / `cross` / `demo` | 第87期 | ○ |
| 29 | `gather` | `GatherRule` | `GatherRule { Enabled = False }` | ○ | `derive` / `curse` / `hex` / `encore` / `gather` / `deep` / `soak` / `ledger` / `wound2` / `parry` / `wall` / `cross` / `demo` | 第89期 | ○ |
| 30 | `soak` | `SoakRule` | `SoakRule { Poison = True, Burn = False, DullPerKind = 1 }` |  | `derive` / `curse` / `hex` / `soak` / `ledger` / `lit` / `wound2` / `ember` / `rebirth3` / `rebirtha2` / `beni` / `cross` | 第90期 | ○ |
| 31 | `deep` | `DeepRule` | `DeepRule { Enabled = False }` | ○ | `derive` / `curse` / `hex` / `encore` / `deep` / `wound2` | 第93期 | ○ |
| 32 | `curse` | `CurseRule` | `CurseRule { Enabled = True, SharePercent = 50 }` |  | `curse` / `hex` / `lit` / `mudohex` | 第95期 | ○ |
| 33 | `betray` | `BetrayRule` | `BetrayRule { Enabled = True, Respawn = True }` |  | `betray` / `tempo` / `ledger` | 第103期 | ○ |
| 34 | `encore` | `EncoreRule` | `EncoreRule { Enabled = True }` |  | `encore` / `tempo` / `tomo` / `hold2` / `ledger` / `lit` / `rebirth` | 第104期 | ○ |
| 35 | `rage` | `RageRule` | `RageRule { Mode = Amount, Gain = 3 }` |  | `hold` / `hold2` | 第106期 | ○ |
| 36 | `menderCost` | `MenderCostRule` | `MenderCostRule { Percent = 50 }` |  | `hold` / `hold2` / `ledger` / `lit` / `wound2` | 第106期 | ○ |
| 37 | `loose` | `LooseRule` | `LooseRule { Shove = True }` |  | `hold` / `hold2` / `ledger` / `lit` | 第106期 | ○ |
| 38 | `taillight` | `TaillightRule` | `TaillightRule { Mode = OwnTurnWindow, Filter = ActingNow }` |  | `tomo` / `ledger` / `lit` | 第110期 | ○ |
| 39 | `reader` | `ReaderRule` | `ReaderRule { Threshold = 5 }` |  | `reader` / `boss` / `tank` / `grade` / `grade2` | 第115期 | ○ |
| 40 | `boss` | `BossRule` | `BossRule { Census = False, Scale = EnemyScaleRule { HpPercent = 115, AtkPercent = 115, Active = True } }` | ○ | `boss` / `tank` / `time` / `grade` / `escale` / `whip` / `shock` / `lili` / `demo` | 第117期 | ○ |
| 41 | `nourish` | `NourishRule` | `NourishRule { Gain = 2 }` |  | `tank` / `time` | 第117期 | ○ |
| 42 | `wound` | `WoundRule` | `WoundRule { Enabled = True, Census = False }` |  | `wound2` | 第85期 | ○ |
| 43 | `ember` | `EmberRule` | `EmberRule { Enabled = False, Fireproof = True, TickHeal = 0 }` |  | `survive` / `ember` / `wildfire` / `rebirth` | 第130期 | ○ |
| 44 | `wildfire` | `WildfireRule` | `WildfireRule { Mode = None, Amount = 0, Active = False }` | ○ | `wildfire` | 第133期 | ○ |
| 45 | `harm` | `HarmRule` | `HarmRule { Census = False }` | ○ | `parry` / `wall` / `sora186` / `mio` / `sid` | 第135期 | ○ |
| 46 | `parry` | `ParryRule` | `ParryRule { Uses = 2, Scope = Any, Relay = True, Swing = WhenStocked }` |  | `parry` / `wall` / `stall` | 第135期 | ○ |
| 47 | `shatter` | `ShatterRule` | `ShatterRule { Mode = Passive, SelfCostPerTurn = 0 }` | ○ | `shard` | 第137期 | ○ |
| 48 | `shrapnel` | `ShrapnelRule` | `ShrapnelRule { Multiplier = 3, SelfDamagePercent = 100, ArmorCensus = False }` |  | `shard` | 第138期 | ○ |
| 49 | `brace` | `BraceRule` | `BraceRule { Cap = 7, Refuse = True, Stagger = False }` |  | `brace` / `tsugi` | 第143期 | ○ |
| 50 | `shuffler` | `ShufflerRule` | `ShufflerRule { Foes = True, Stagger = Confuse, ConfusePercent = 100, ConfuseUses = 3, GustPercent = 20, GustSecondary = True }` |  | `tumult` / `gust` / `tumult2` / `derange` / `confuse` | 第144期 | ○ |
| 51 | `confusion` | `ConfusionRule` | `ConfusionRule { Active = False, Percent = 100 }` |  | `mark184` / `derange` / `confuse` | 第146期 | ○ |
| 52 | `haste` | `HasteRule` | `HasteRule { Pick = None }` | ○ | `mark` / `haste` | 第149期 | ○ |
| 53 | `ward` | `WardRule` | `WardRule { Return = Burst, Percent = 50, Threshold = 40, Drip = 10, Cost = Forfeit, BurdenPercent = 50, LadenPer = 10 }` |  | `ward` / `wardcost` | 第153期 | ○ |
| 54 | `indulgence` | `IndulgenceRule` | `IndulgenceRule { Advance = 30, Threshold = 60, Contracts = 0, Blast = Single }` |  | `toll` | 第155期 | ○ |
| 55 | `ash` | `AshRule` | `AshRule { CountHavoc = True, CountDot = True, Multiplier = 100, FalloutOnDeath = True, ThrowEvery = 2 }` |  | `susu` | 第179期 | ○ |
| 56 | `erupt` | `EruptRule` | `EruptRule { Floor = True, Smear = PerErupt, Heavy = True }` |  | `mudohex` / `mudo` | 第180期 | ○ |
| 57 | `markRule` | `MarkRule` | `MarkRule { VulnerablePercent = 50 }` |  | `mark` / `mark184` | 第150期 | ○ |

引数 57 本（`verbose` と観測子を除く）。

**`= default(T)` は「その規則が既定で何もしない」の機械的な手がかりであって、判定ではない。**
採否そのものは**既定値の列**を読むこと——`ThornRule { Wound = None }` は残置、`SoakRule { Poison = True, Burn = False }` は毒側だけ採用、という具合に既定値が全部を語る。

## 2. `Default` を持つ型の全数（`Run` の引数に出ないものを含む）

| 型 | 既定値 | `Run` の引数 | 測った診断 | 初出（design/） |
|---|---|:-:|---|---|
| `AshRule` | `AshRule { CountHavoc = True, CountDot = True, Multiplier = 100, FalloutOnDeath = True, ThrowEvery = 2 }` | ○ | `susu` | 第179期 |
| `BearRule` | `BearRule { ArmorPerDull = 2 }` | ○ | `curse` / `dull` / `ledger` | 第42期 |
| `BetrayRule` | `BetrayRule { Enabled = True, Respawn = True }` | ○ | `betray` / `tempo` / `ledger` | 第103期 |
| `BlazeRule` | `BlazeRule { Targets = Both, Allies = True, Foes = True }` | ○ | `blaze` / `ledger` / `ember` | 第59期 |
| `BossRule` | `BossRule { Census = False, Scale = EnemyScaleRule { HpPercent = 115, AtkPercent = 115, Active = True } }` | ○ | `boss` / `tank` / `time` / `grade` / `escale` / `whip` / `shock` / `lili` / `demo` | 第117期 |
| `BoundaryRule` | `BoundaryRule { Choice = None, Plan = , Active = False }` |  | `stage` / `choice` | 第102期 |
| `BraceRule` | `BraceRule { Cap = 7, Refuse = True, Stagger = False }` | ○ | `brace` / `tsugi` | 第143期 |
| `ColossusRule` | `ColossusRule { Percent = 90, DamagePerGain = 4, Regurgitate = True, Slumber = False, SlumberThreshold = 60, Refund = True, RefundPercent = 25 }` | ○ | `gullet` / `whet` / `miasma` / `ledger` | 第35期 |
| `ConfusionRule` | `ConfusionRule { Active = False, Percent = 100 }` | ○ | `mark184` / `derange` / `confuse` | 第146期 |
| `CreakRule` | `CreakRule { Threshold = 0, Source = Whet }` | ○ | `creak` / `pairs` / `creak3` | 第66期 |
| `CurseRule` | `CurseRule { Enabled = True, SharePercent = 50 }` | ○ | `curse` / `hex` / `lit` / `mudohex` | 第95期 |
| `DeepRule` | `DeepRule { Enabled = False }` | ○ | `derive` / `curse` / `hex` / `encore` / `deep` / `wound2` | 第93期 |
| `DivertRule` | `DivertRule { TargetCount = 1, SelfMark = True, Audit = False }` | ○ | `divert` / `survive` / `wildfire` / `mark` | 第50期 |
| `EmberRule` | `EmberRule { Enabled = False, Fireproof = True, TickHeal = 0 }` | ○ | `survive` / `ember` / `wildfire` / `rebirth` | 第130期 |
| `EncoreRule` | `EncoreRule { Enabled = True }` | ○ | `encore` / `tempo` / `tomo` / `hold2` / `ledger` / `lit` / `rebirth` | 第104期 |
| `EnemyScaleRule` | `EnemyScaleRule { HpPercent = 115, AtkPercent = 115, Active = True }` |  | `escale` / `whip` / `shock` / `sid` / `ep3` / `dump` | 第187期 |
| `EruptRule` | `EruptRule { Floor = True, Smear = PerErupt, Heavy = True }` | ○ | `mudohex` / `mudo` | 第180期 |
| `ExposeRule` | `ExposeRule { MaxPerBattle = 3 }` | ○ | `expose` / `creak3` / `ledger` | 第40期 |
| `FavorRule` | `FavorRule { Gain = 4, Loss = 2 }` | ○ | `curse` / `favor` / `turn` / `ledger` | 第58期 |
| `FinisherRule` | `FinisherRule { Multiplier = 2, Consume = True }` | ○ | `finisher` / `ledger` / `survive` / `wildfire` / `mark` | 第53期 |
| `FormationShape` | `BattleCore.FormationShape` |  | `whip` / `shock` / `shockdigest` / `form2` / `ep3` | 第200期 |
| `FunnelRule` | `FunnelRule { Slowest = True, Both = False }` | ○ | `funnel` / `cross` | 第62期 |
| `GatherRule` | `GatherRule { Enabled = False }` | ○ | `derive` / `curse` / `hex` / `encore` / `gather` / `deep` / `soak` / `ledger` / `wound2` / `parry` / `wall` / `cross` / `demo` | 第89期 |
| `GoadRule` | `GoadRule { Boost = 4, Mark = True }` | ○ | `derive` / `whet` / `goad` / `ledger` / `mark` | 第52期 |
| `HarmRule` | `HarmRule { Census = False }` | ○ | `parry` / `wall` / `sora186` / `mio` / `sid` | 第135期 |
| `HasteRule` | `HasteRule { Pick = None }` | ○ | `mark` / `haste` | 第149期 |
| `HushRule` | `HushRule { Active = True }` | ○ | `curse` / `hush` / `goad` / `wave2` / `ledger` | 第35期 |
| `IgniteRule` | `IgniteRule { Enabled = True }` | ○ | `derive` / `curse` / `blaze2` / `gauge` / `gather` / `deep` / `soak` / `ledger` / `wound2` / `cross` / `demo` | 第87期 |
| `IndulgenceRule` | `IndulgenceRule { Advance = 30, Threshold = 60, Contracts = 0, Blast = Single }` | ○ | `toll` | 第155期 |
| `LooseRule` | `LooseRule { Shove = True }` | ○ | `hold` / `hold2` / `ledger` / `lit` | 第106期 |
| `MarkRule` | `MarkRule { VulnerablePercent = 50 }` | ○ | `mark` / `mark184` | 第150期 |
| `MartyrRule` | `MartyrRule { RedirectPercent = 75 }` | ○ | `curse` / `guard` / `gather` / `ledger` / `wall` | 第35期 |
| `MendRule` | `MendRule { Side = Plain }` | ○ | `curse` / `hex` / `mender` / `gauge` / `hold2` / `ledger` / `wound2` / `cross` | 第86期 |
| `MenderCostRule` | `MenderCostRule { Percent = 50 }` | ○ | `hold` / `hold2` / `ledger` / `lit` / `wound2` | 第106期 |
| `NourishRule` | `NourishRule { Gain = 2 }` | ○ | `tank` / `time` | 第117期 |
| `OverbearRule` | `OverbearRule { Drain = 2 }` | ○ | `overbear` / `wildfire` | 第46期 |
| `ParryRule` | `ParryRule { Uses = 2, Scope = Any, Relay = True, Swing = WhenStocked }` | ○ | `parry` / `wall` / `stall` | 第135期 |
| `RageRule` | `RageRule { Mode = Amount, Gain = 3 }` | ○ | `hold` / `hold2` | 第106期 |
| `ReaderRule` | `ReaderRule { Threshold = 5 }` | ○ | `reader` / `boss` / `tank` / `grade` / `grade2` | 第115期 |
| `RecoverRule` | `RecoverRule { HpPercent = 0, ReviveDead = False, Active = False }` |  | `stage` / `choice` / `recover` | 第101期 |
| `RelayRule` | `RelayRule { TransferPercent = 100 }` | ○ | `curse` / `relay` | 第43期 |
| `ScaleRule` | `ScaleRule { CostPerAttack = 1 }` | ○ | `scale` / `scapegoat` / `divert` / `favor` / `miasma` / `goad` / `finisher` / `ledger` | 第47期 |
| `ScapegoatRule` | `ScapegoatRule { Threshold = 3, Audit = False }` | ○ | `scapegoat` | 第49期 |
| `SeverRule` | `SeverRule { Wait = Swing, Threshold = 2 }` | ○ | `wcost` / `ledger` / `lit` / `wound2` / `cross` | 第74期 |
| `ShatterRule` | `ShatterRule { Mode = Passive, SelfCostPerTurn = 0 }` | ○ | `shard` | 第137期 |
| `ShoveRule` | `ShoveRule { Penalty = 2 }` | ○ | `shove` / `ledger` | 第41期 |
| `ShrapnelRule` | `ShrapnelRule { Multiplier = 3, SelfDamagePercent = 100, ArmorCensus = False }` | ○ | `shard` | 第138期 |
| `ShufflerRule` | `ShufflerRule { Foes = True, Stagger = Confuse, ConfusePercent = 100, ConfuseUses = 3, GustPercent = 20, GustSecondary = True }` | ○ | `tumult` / `gust` / `tumult2` / `derange` / `confuse` | 第144期 |
| `SlanderRule` | `SlanderRule { Penalty = 0 }` | ○ | `slander` | 第44期 |
| `SoakRule` | `SoakRule { Poison = True, Burn = False, DullPerKind = 1 }` | ○ | `derive` / `curse` / `hex` / `soak` / `ledger` / `lit` / `wound2` / `ember` / `rebirth3` / `rebirtha2` / `beni` / `cross` | 第90期 |
| `SpillWoundRule` | `SpillWoundRule { Enabled = False, Scope = All }` | ○ | `curse` / `hex` / `suture2` / `mender` / `gauge` / `gather` / `soak` / `hold2` / `ledger` / `wound2` / `parry` / `cross` | 第85期 |
| `SutureFireRule` | `SutureFireRule { Fire = Swing }` | ○ | `taillight` / `hold2` | 第107期 |
| `SutureRule` | `SutureRule { Side = Both }` | ○ | `curse` / `hex` / `suture2` / `mender` / `gauge` / `gather` / `soak` / `taillight` / `hold2` / `ledger` / `wound2` / `cross` | 第85期 |
| `TaillightRule` | `TaillightRule { Mode = OwnTurnWindow, Filter = ActingNow }` | ○ | `tomo` / `ledger` / `lit` | 第110期 |
| `ThinBladeRule` | `ThinBladeRule { Cost = Always }` | ○ | `blade` / `ledger` / `cross` | 第75期 |
| `ThornRule` | `ThornRule { Wound = None }` | ○ | `derive` / `curse` / `thorn` / `suture2` / `gauge` / `wound2` / `cross` | 第84期 |
| `WardRule` | `WardRule { Return = Burst, Percent = 50, Threshold = 40, Drip = 10, Cost = Forfeit, BurdenPercent = 50, LadenPer = 10 }` | ○ | `ward` / `wardcost` | 第153期 |
| `WhetMask` | `WhetMask { Bits = 0 }` | ○ | `spend` | 第65期 |
| `WildfireRule` | `WildfireRule { Mode = None, Amount = 0, Active = False }` | ○ | `wildfire` | 第133期 |
| `WoundRule` | `WoundRule { Enabled = True, Census = False }` | ○ | `wound2` | 第85期 |
| `YokeRule` | `YokeRule { Cap = 25, Active = True }` | ○ | `curse` / `yoke` / `goad` / `wave2` / `ledger` / `parry` / `brace` | 第35期 |

61 型。

## 3. 規則が使う列挙型

**`default(T)` は必ず値 0 の要素**なので、この並びが上の `= default(T)` 列の意味を決めている。

| 列挙型 | 値（0 から） |
|---|---|
| `BlazeTargets` | `None`=0 / `AllyOnly`=1 / `Both`=2 / `FoeOnly`=3 |
| `BoundaryChoice` | `None`=0 / `Revive`=1 / `Heal`=2 / `Carry`=3 |
| `BrandBlast` | `Single`=0 / `All`=1 |
| `CreakSource` | `Whet`=0 / `Bonus`=1 / `Both`=2 |
| `HastePick` | `None`=0 / `Slowest`=1 / `Strongest`=2 |
| `LitFilter` | `SupportOnly`=0 / `ActingOnly`=1 / `ActingNow`=2 |
| `MendSide` | `Plain`=0 / `Wound`=1 |
| `ParryScope` | `Guarded`=0 / `Any`=1 |
| `ParrySwing` | `Off`=0 / `WhenFull`=1 / `WhenStocked`=2 |
| `PierceRule` | `Random`=0 / `MostOccupied`=1 / `Facing`=2 |
| `RageMode` | `Amount`=0 / `Count`=1 |
| `SeverWait` | `Yield`=0 / `Swing`=1 |
| `ShatterMode` | `Passive`=0 / `Turn`=1 / `Both`=2 |
| `ShuffleStagger` | `None`=0 / `Advanced`=1 / `Both`=2 / `Confuse`=3 / `ConfuseOnAction`=4 |
| `SmearWhen` | `PerHit`=0 / `PerErupt`=1 / `None`=2 |
| `SpillScope` | `All`=0 / `Dense`=1 |
| `SutureFire` | `Swing`=0 / `OnWound`=1 |
| `SutureSide` | `Foe`=0 / `Both`=1 |
| `ThinBladeCost` | `Always`=0 / `Unwounded`=1 / `Carving`=2 / `Slower`=3 |
| `ThornWound` | `None`=0 / `Foe`=1 / `Both`=2 |
| `WardCost` | `Forfeit`=0 / `Burden`=1 / `Laden`=2 |
| `WardReturn` | `Burst`=0 / `Drip`=1 |
| `WildfireMode` | `None`=0 / `Add`=1 / `Scale`=2 / `Flat`=3 |
| `YieldMode` | `OwnTurn`=0 / `OwnTurnWindow`=1 / `Immediate`=2 |

24 型。
