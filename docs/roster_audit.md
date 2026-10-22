# ロスターの棚卸し

`dotnet run --project BattleSim -c Release 0 roster audit > docs/roster_audit.md` の出力。手で編集しない。

**素体より弱い駒は 5 / 52 枚**（`理想台の帰属` が負 ＝ その駒を同数値・特性なしの素体に落としたほうが編成が強くなる）。

**判断は書かない。** この表は次の期（転生候補の洗い出し）の材料で、**新しい測定は1つもしていない**——既にある数字を集めただけである。

## 出どころ

| 列 | 定義 | 出どころ |
|---|---|---|
| 理想台の帰属 | `CompareBuilds()` 61 行の在席枠で、**その駒だけを素体に落とした差**（第2〜5波・seed 0..199） | `checkup ideal`（第82期） |
| `checkup` 単独 / 3分 | ドラフト台の 2×2 の `y11 − y01` と、それで割った 残す / 転生 / 差し替え | `design/PHASE152_CHECKUP.md` 表A（**再計算しない**） |
| 在席枠 | `Presets.Compare` 61 行に何枠いるか | `Presets`（**戦闘0回**） |
| 固有の勝者 | **その波でだけ 100%** の行（第2〜5波）に何枠いるか | `docs/balance.md`（生成済み） |
| 列レンジ / 送り先 | 12 列で順位がどれだけ動くか・第164期の3群 | `stage catalog`（第164期・版 R0） |
| 枠の値段 | かき回し隊の 後3 の**1枠だけ**を振ったときの踏破率の差 | `design/PHASE172_REFORM.md` 部C |

固有の勝者の行（第2〜5波）は **4 行** —— 刻み×澱み (ノミ×ミオ) / 反撃改3 (カド×ハギ) / 感電 (シガ×カタ×ソム) / 裂き (キリ×エグ)。

枠の値段の出どころは `design/PHASE172_REFORM.md`（7 枚ぶん）。**無い駒は空欄**——あの表は1つの枠でしか測っていないので、空欄は「0」ではない。

## 52 枚（**理想台の帰属の昇順**）

| # | 駒 | 理想台の帰属 | `checkup` 単独 | `checkup` 3分 | 在席枠 | 固有の勝者 | 列レンジ | 送り先 | 象限 | 枠の値段 | 札 |
|--:|---|--:|--:|---|--:|--:|--:|---|---|--:|---|
| 1 | 駆り立てのカリ | -3.81 | +1.94 | 残す | 2 | 0 | 45.0 | 化ける | 上（単独で強い） |  | Goad |
| 2 | 追い打ちのハギ | -3.05 | -33.36 | 転生 | 5 | 1 | 15.0 | ノイズ | 右下（選んでも働かない） | −5.3pt | Pursuer |
| 3 | 爆ぜるゾト | -1.12 | +13.04 | 残す | 8 | 0 | 41.0 | 化ける | 上（単独で強い） |  | Bomber |
| 4 | のろまの巨兵ドルガ | -0.96 | -11.99 | 別扱い | 24 | 2 | 12.0 | どこでも同じ | 右下（選んでも働かない） |  | Sluggish / GradeStep |
| 5 | 萎縮のクビ | -0.31 | -12.52 | 転生 | 2 | 0 | 14.0 | どこでも同じ | 右下（選んでも働かない） | +2.3pt | Huddle / Daunt / DauntLeak |
| 6 | 尾灯のトモ | +0.00 | +17.75 | 残す | 1 | 0 | 21.0 | 化ける | 上（単独で強い） |  | Taillight |
| 7 | 引き受けのウケ | +0.00 | +1.96 | 残す | 1 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Bear |
| 8 | 泥人形ムド | +0.00 | +16.35 | 残す | 8 | 0 | 21.0 | 化ける | 上（単独で強い） |  | Erupt / Smear / Hex / MudoCurseRage / MudoCurseHeavy |
| 9 | 渡しのワタ | +0.00 | +2.05 | 残す | 1 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Relay / RelayWide / RelayFeed / RelayFeedMudo |
| 10 | 澱み喰いのヴィオ | +0.00 | +2.62 | 残す | 2 | 0 | 20.0 | 化ける | 上（単独で強い） |  | Blightfed / BlightfedSkipCloak / StrikePoison / PoisonProof / VioFlurryA / VioBloodSip |
| 11 | 火選りのヒヨ | +0.00 | -12.35 | 転生 | 4 | 0 | 24.0 | 化ける | 左下（送り先を選べば働く） |  | Favor / FireConvert / FireStoke / TurnGift / StokeStageAtk / GiftQuiet / SparkCatch / SparkUnleash / FavorLevel / GiftHoard / GiftOrder / BurnHitNoMend |
| 12 | 熾のホタ | +0.00 | +1.44 | 転生 | 3 | 0 | 32.0 | 化ける | 左下（送り先を選べば働く） |  | Pyre / PyreStage / PyreBurnout / PyreEmbers / CallFire / PyreLance / PyreCritical / PyreMend / EmbersChain / PyreOverflow / PyreFed / BurnoutHeavy / BurnHitNoMend |
| 13 | 疫みのラウ | +0.00 | +1.47 | 転生 | 3 | 0 | 8.0 | どこでも同じ | 右下（選んでも働かない） |  | Contagion / Touch / TouchLeak |
| 14 | 移り木のシオ | +0.00 | +8.54 | 残す | 1 | 0 | 12.0 | どこでも同じ | 上（単独で強い） |  | Drifter / Regroup / DrifterMend / RegroupTend / RegroupTendSelf / ShioStage / Retreat / DriftSurge / RetreatHalf |
| 15 | 錯乱のササ | +0.00 | +10.02 | 残す | 1 | 0 | 16.0 | どこでも同じ | 上（単独で強い） |  | Brace / BraceHold / BraceWide / BraceNear |
| 16 | 毒喰らいのベニ | +0.12 | +2.11 | 残す | 2 | 0 | 10.0 | どこでも同じ | 上（単独で強い） |  | Inverse / Guren / Kindle / Taint / InverseLeak / GurenOpeningBurn |
| 17 | 鱗のウロ | +0.12 | +4.52 | 残す | 3 | 0 | 17.0 | 化ける | 上（単独で強い） |  | Scale |
| 18 | 毒吐きのスィド | +0.16 | +15.82 | 残す | 4 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Spew / PoisonProof / PoisonCloak / CloakRetort / Numb / SidOpeningCloak10 |
| 19 | 後備えのセッキ | +0.25 | +13.02 | 残す | 3 | 0 | 23.0 | ノイズ | 上（単独で強い） | −4.6pt | RearGuard / Rage |
| 20 | 礫のガレ | +1.25 | -3.82 | 転生 | 1 | 0 | 2.0 | どこでも同じ | 右下（選んでも働かない） |  | PebbleGather / ScatterGather |
| 21 | 痺れ粉のトウ | +1.50 | +3.87 | 残す | 2 | 0 | 30.0 | ノイズ | 上（単独で強い） |  | ChargedPowder / ChargedPowderLeak / ShockStunHalf / ChargedPowderSpread / ShockDaunt / PowderDense / PowderDenseKeep / PowderDenseBurn / PowderContagion / PowderMarkFoeOnly / DenseConduct / DustBurst / PowderBurstMarked / PowderBurstFall / ShockStunOnce |
| 22 | 軋みのヨミ | +1.54 | +10.38 | 残す | 3 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Displaced / CreakSweep / KillImpact / ImpactTailwind |
| 23 | 逃亡兵セロ | +1.56 | +1.42 | 転生 | 12 | 0 | 5.0 | どこでも同じ | 右下（選んでも働かない） | +0.3pt | Evade / EvadeSwap / EvadeQuick / EvadeDrift / LastDodge / Decoy / EvadeMoveShot |
| 24 | 見境なしのミサ | +1.92 | +1.55 | 残す | 3 | 0 | 31.0 | ノイズ | 上（単独で強い） |  | Rupture / RuptureKeep / Feathers / FeatherMarkLayer / FeatherGrowUnmarked |
| 25 | 廃棄聖騎士ガルド | +2.29 | +15.59 | 残す | 37 | 1 | 7.0 | どこでも同じ | 上（単独で強い） |  | Guardian / Stoic / Parry / LastStandHold |
| 26 | 刻みのノミ | +3.29 | +12.05 | 残す | 7 | 1 | 5.0 | どこでも同じ | 上（単独で強い） |  | Pellet / Carve / CarveOnce / Fixate |
| 27 | 据えのバン | +3.38 | +1.44 | 転生 | 4 | 0 | 29.0 | 化ける | 左下（送り先を選べば働く） |  | Footing / Planted |
| 28 | 電気鞭のシガ | +3.62 | — | — | 3 | 1 | 17.0 | 化ける | — |  | Scourge / Shame / Lash / LiveWire / ScourgeShock / StoredCharge / ShockWhipFlurry / ShockWhipChain / FearNoStun / ScourgeDaunt / BetrayBackswing / ChargeUncapped / ChargeKeep / ScourgeFearDoubled |
| 29 | 縛めのクグ | +3.71 | +5.91 | 残す | 3 | 0 | 25.0 | 化ける | 上（単独で強い） |  | Grapple / Thread / ThreadCharge / WebCharge / ThreadPowderFlow / SilkBattery |
| 30 | 大喰らいゴルム | +3.89 | +8.03 | 残す | 24 | 2 | 25.0 | 化ける | 上（単独で強い） |  | Colossus / Drain / GolmSwallowSpit / GolmSwallowSpend / GolmSwallowWeak / GolmRocketPunch / GolmRocketGrab |
| 31 | 喧噪のバサ | +4.67 | +34.83 | 残す | 3 | 0 | 37.0 | 化ける | 上（単独で強い） |  | Shuffler / Disarray / Squall / Gale / Tailwind / TailwindFighter |
| 32 | 鬨の号令ガン | +6.54 | +20.85 | 残す | 10 | 1 | 16.0 | どこでも同じ | 上（単独で強い） | −1.0pt | Rally / Reveille |
| 33 | 背かれのソム | +7.00 | -13.43 | 転生 | 1 | 1 | 16.0 | どこでも同じ | 右下（選んでも働かない） |  | BetrayedShockNoThunder / SparkRain / SparkVeil / BeastBurstAlways / SwarmCall / EmergencyCall / EmergencyFocus / BeastDeathCount / BeastStand / BeastBetray / BeastResonance / BeastResonanceArc / BeastThrowBackfire / SparkVeilCap |
| 34 | 仇討ちのザン | +7.75 | -5.91 | 転生 | 4 | 0 | 14.0 | どこでも同じ | 右下（選んでも働かない） |  | Vendetta / Recoil / VendettaFrameAll / VendettaRound / VendettaMarkFirst / VendettaDirectOnly |
| 35 | 胞子体ムグ | +8.19 | +5.08 | 残す | 4 | 0 | 39.0 | ノイズ | 上（単独で強い） |  | Splitter / MugSowAll |
| 36 | 分かちのドハ | +9.00 | +9.03 | 残す | 5 | 0 | 20.0 | 化ける | 上（単独で強い） |  | Sharer / SharerArmored / ShareBack / SharerNoDull / SharePush / SharePushPower / SharePushStrikers / DohaSplit |
| 37 | 継ぎ当てのツギ | +9.03 | — | — | 4 | 1 | 44.0 | ノイズ | — |  | Plank / PlankScorch / Scrap / PlankRebound / PlankThick / PlankBase / FirstAid / AidSkill / PlankNeediest / FirstAidArmored / PlankOpening |
| 38 | 焼け残りのボルグ | +9.35 | -4.51 | 転生 | 13 | 0 | 48.0 | 化ける | 左下（送り先を選べば働く） | 0.0pt | Splash / FireFeed / Cinder / FireArmor / FireSplash / SelfKindle / FireMend / FireWardAll / FireLevel / CinderWide / FireKeep / FireSpreadCap / FireUnleash / FoeFireLevel / FoeFireTick / FoeFireBrittle / FoeFireSpread / AllyFireTick / RadiateCall / CallFull / UnleashBlaze / BlazeSolo / KindleGuard / BlazeHoard / KindleOpen / BlazeFoeSurgeMax / BurnHitNoMend |
| 39 | 突き返しのハネ | +9.50 | -3.59 | 転生 | 1 | 0 | 40.0 | 化ける | 左下（送り先を選べば働く） | 対照 0.0pt | Rebound / Overrun / Disarray / Blast / Spring / Tailwind / TailwindFighter / SpringGuard / SpringStay / SpringRow / Landing / SpringStrike / SpringDaunt / BlastReach / BlastStay |
| 40 | 禍導のカタ | +10.00 | — | — | 1 | 1 | 14.0 | どこでも同じ | — |  | Thunder / ThunderLeak / ThunderPath / ShockStunHalf / Thundercloud / ThundercloudKeep / ThundercloudUncapped / ShockDaunt / ThunderPowderReturn / ShockStunOnce / ThundercloudAllyPops / KataRod / KataRodChain |
| 41 | 瘴気袋のグザ | +12.50 | +21.36 | 残す | 8 | 0 | 12.0 | どこでも同じ | 上（単独で強い） |  | Miasma / ScatterGather / MiasmaVent / MiasmaSplash / SplashUncapped / PoisonProof |
| 42 | 澱みのミオ | +14.25 | -0.16 | 転生 | 6 | 2 | 12.0 | どこでも同じ | 右下（選んでも働かない） |  | Concentrate / ConcentrateLeak / MireSlam / MireConduct / MireDull / MireCarry / MireHandoff / MireBurstStack |
| 43 | 砕け盾のヒビ | +14.75 | +4.64 | 残す | 4 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | ShardGift / ShardOpen / ShardBareNoPlank / ShardFull |
| 44 | 囃し立てのヒサ | +15.51 | +0.86 | 転生 | 10 | 1 | 7.0 | どこでも同じ | 右下（選んでも働かない） |  | Beckon / Flee / MarkRallyWide / MarkRallySelf / BeckonFeather / FrameAccuseQuiet / CommandBurst3 / HisaCover / RallyQuiet / CoverSkipLethal / CommandBurstCap9 / MarkInheritSpread / RallyFeatherQuiet |
| 45 | 施しのリリ | +18.40 | — | — | 9 | 0 | 15.0 | どこでも同じ | — |  | Kiss / KissSpill / KissPain / KissVoid / KissTier / KissSteal / KissTri / KissRite5 |
| 46 | 棘鎧のカド | +22.59 | +32.59 | 残す | 10 | 1 | 15.0 | どこでも同じ | 上（単独で強い） |  | ThornGuard / Thorns / Immobile / Havoc / ThornsArmored / ThornsWound / ThornsBare |
| 47 | 逆しまのウツ | +25.43 | +3.89 | 残す | 5 | 0 | 27.0 | 化ける | 上（単独で強い） |  | Perverse / PerverseOver |
| 48 | 逸らしのソラ | +25.96 | +1.62 | 残す | 6 | 0 | 8.0 | どこでも同じ | 上（単独で強い） |  | Divert / Deflect / Thrust / DivertPressure / DivertKeepBeckon |
| 49 | 継ぎ接ぎのヴェル | +31.81 | +10.99 | 残す | 14 | 2 | 3.0 | どこでも同じ | 上（単独で強い） |  | SoulSplitHalf / SoulSew |
| 50 | 呪詛官ネル | +32.48 | +17.08 | 残す | 6 | 0 | 18.0 | 化ける | 上（単独で強い） |  | Curse / Hexer / HexLeak / CurseScar |
| 51 | 墓守リィカ | +42.69 | +9.81 | 残す | 11 | 0 | 1.0 | どこでも同じ | 上（単独で強い） |  | Necro / Sacrifice / RicaRaiseDeadStack / RicaAwakenRaise / RicaLayerFlurry / UncapRaise |
| 52 | 血詠みのアカ | — | — | — | 0 | 0 | — | — | — |  | BloodRead / ScatterGather |

## 自己検査

- `UnitCatalog.All` の 52 枚すべてに行がある: **○**（52 行）
- `checkup ideal` が値を返した駒: 51 / 52 （返さないのは `CompareBuilds()` に在席 0 枠の駒だけ）
- `stage catalog` が引けた駒: 51 / 52

所要 441.3 秒（うち `stage catalog` が 418.4 秒）。
