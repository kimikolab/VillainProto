# ロスターの棚卸し

`dotnet run --project BattleSim -c Release 0 roster audit > docs/roster_audit.md` の出力。手で編集しない。

**素体より弱い駒は 4 / 52 枚**（`理想台の帰属` が負 ＝ その駒を同数値・特性なしの素体に落としたほうが編成が強くなる）。

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

固有の勝者の行（第2〜5波）は **1 行** —— 感電 (シガ×カタ×ソム)。

枠の値段の出どころは `design/PHASE172_REFORM.md`（7 枚ぶん）。**無い駒は空欄**——あの表は1つの枠でしか測っていないので、空欄は「0」ではない。

## 52 枚（**理想台の帰属の昇順**）

| # | 駒 | 理想台の帰属 | `checkup` 単独 | `checkup` 3分 | 在席枠 | 固有の勝者 | 列レンジ | 送り先 | 象限 | 枠の値段 | 札 |
|--:|---|--:|--:|---|--:|--:|--:|---|---|--:|---|
| 1 | 駆り立てのカリ | -8.50 | +1.94 | 残す | 2 | 0 | 35.0 | 化ける | 上（単独で強い） |  | Goad |
| 2 | 爆ぜるゾト | -1.45 | +13.04 | 残す | 8 | 0 | 43.0 | ノイズ | 上（単独で強い） |  | Bomber |
| 3 | のろまの巨兵ドルガ | -1.02 | -11.99 | 別扱い | 24 | 0 | 30.0 | 化ける | 左下（送り先を選べば働く） |  | Sluggish / GradeStep / SluggishShift / DolgaWall / DolgaWallRetreat / DolgaFocus / DolgaHeavy |
| 4 | 追い打ちのハギ | -0.10 | -33.36 | 転生 | 5 | 0 | 25.0 | 化ける | 左下（送り先を選べば働く） | −5.3pt | HagiReap / HagiHalfPursuit / HagiDepthSoul / HagiSoulMoves / HagiReapMark / HagiPursuitReap / HagiPursuitHeavy / HagiSpillFixed |
| 5 | 哀歌のエレ | +0.00 | — | — | 2 | 0 | 28.0 | 化ける | — |  | DirgeSoundG1 / DirgeEchoAlways |
| 6 | 尾灯のトモ | +0.00 | +17.75 | 残す | 1 | 0 | 19.0 | 化ける | 上（単独で強い） |  | Taillight / TaillightKeep / TaillightWake / TaillightCarry / TaillightIdleYield |
| 7 | 泥人形ムド | +0.00 | +16.35 | 残す | 9 | 0 | 21.0 | 化ける | 上（単独で強い） |  | Erupt / Smear / Hex / MudoCurseRage / MudoCurseHeavy |
| 8 | 渡しのワタ | +0.00 | +2.05 | 残す | 1 | 0 | 10.0 | どこでも同じ | 上（単独で強い） |  | Relay / RelayWide / RelayFeed / RelayFeedMudo |
| 9 | 澱み喰いのヴィオ | +0.00 | +2.62 | 残す | 2 | 0 | 22.0 | 化ける | 上（単独で強い） |  | Blightfed / BlightfedSkipCloak / StrikePoison / PoisonProof / VioFlurryA / VioBloodSip |
| 10 | 火選りのヒヨ | +0.00 | -12.35 | 転生 | 5 | 0 | 24.0 | 化ける | 左下（送り先を選べば働く） |  | Favor / FireConvert / FireStoke / TurnGift / StokeStageAtk / GiftQuiet / SparkCatch / SparkUnleash / FavorLevel / GiftHoard / GiftOrder / BurnHitNoMend |
| 11 | 熾のホタ | +0.00 | +1.44 | 転生 | 4 | 0 | 22.0 | 化ける | 左下（送り先を選べば働く） |  | Pyre / PyreStage / PyreBurnout / PyreEmbers / CallFire / PyreLance / PyreCritical / PyreMend / EmbersChain / PyreOverflow / PyreFed / BurnoutHeavy / BurnHitNoMend |
| 12 | 疫みのラウ | +0.00 | +1.47 | 転生 | 3 | 0 | 8.0 | どこでも同じ | 右下（選んでも働かない） |  | Contagion / Touch / TouchLeak |
| 13 | 移り木のシオ | +0.00 | +8.54 | 残す | 1 | 0 | 12.0 | どこでも同じ | 上（単独で強い） |  | Drifter / Regroup / DrifterMend / RegroupTend / RegroupTendSelf / ShioStage / Retreat / DriftSurge / RetreatHalf |
| 14 | 錯乱のササ | +0.00 | +10.02 | 残す | 1 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | Brace / BraceHold / BraceWide / BraceNear |
| 15 | 毒吐きのスィド | +0.12 | +15.82 | 残す | 5 | 0 | 16.0 | どこでも同じ | 上（単独で強い） |  | Spew / PoisonProof / PoisonCloak / CloakRetort / Numb / SidOpeningCloak10 |
| 16 | 毒喰らいのベニ | +0.12 | +2.11 | 残す | 2 | 0 | 9.0 | どこでも同じ | 上（単独で強い） |  | Inverse / Guren / Kindle / Taint / InverseLeak / GurenOpeningBurn |
| 17 | 鱗のウロ | +0.17 | +4.52 | 残す | 3 | 0 | 12.0 | どこでも同じ | 上（単独で強い） |  | Scale |
| 18 | 痺れ粉のトウ | +0.94 | +3.87 | 残す | 2 | 0 | 27.0 | 化ける | 上（単独で強い） |  | ChargedPowder / ChargedPowderLeak / ShockStunHalf / ChargedPowderSpread / ShockDaunt / PowderDense / PowderDenseKeep / PowderDenseBurn / PowderContagion / PowderMarkFoeOnly / DenseConduct / DustBurst / PowderBurstMarked / PowderBurstFall / ShockStunOnce |
| 19 | 礫のガレ | +1.38 | -3.82 | 転生 | 1 | 0 | 4.0 | どこでも同じ | 右下（選んでも働かない） |  | PebbleGather / ScatterGather |
| 20 | 逃亡兵セロ | +1.47 | +1.42 | 転生 | 12 | 0 | 8.0 | どこでも同じ | 右下（選んでも働かない） | +0.3pt | Evade / EvadeSwap / EvadeQuick / EvadeDrift / LastDodge / Decoy / EvadeMoveShot |
| 21 | 廃棄聖騎士ガルド | +1.48 | +15.59 | 残す | 37 | 0 | 9.0 | どこでも同じ | 上（単独で強い） |  | Guardian / Stoic / Parry / LastStandHold |
| 22 | 軋みのヨミ | +1.54 | +10.38 | 残す | 3 | 0 | 10.0 | どこでも同じ | 上（単独で強い） |  | Displaced / CreakSweep / KillImpact / ImpactTailwind |
| 23 | 刻みのノミ | +1.64 | +12.05 | 残す | 7 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Pellet / Carve / CarveOnce / Fixate |
| 24 | 縛めのクグ | +1.67 | +5.91 | 残す | 3 | 0 | 26.0 | 化ける | 上（単独で強い） |  | Grapple / Thread / ThreadCharge / WebCharge / ThreadPowderFlow / SilkBattery |
| 25 | 見境なしのミサ | +1.92 | +1.55 | 残す | 3 | 0 | 30.0 | 化ける | 上（単独で強い） |  | Rupture / RuptureKeep / Feathers / FeatherMarkLayer / FeatherGrowUnmarked |
| 26 | 澱みのミオ | +1.96 | -0.16 | 転生 | 6 | 1 | 17.0 | 化ける | 左下（送り先を選べば働く） |  | Concentrate / ConcentrateLeak / MireSlam / MireConduct / MireDull / MireCarry / MireHandoff / MireBurstStack |
| 27 | 分かちのドハ | +2.90 | +9.03 | 残す | 5 | 0 | 17.0 | 化ける | 上（単独で強い） |  | Sharer / SharerArmored / ShareBack / SharerNoDull / SharePush / SharePushPower / SharePushStrikers / DohaSplit |
| 28 | 据えのバン | +3.22 | +1.44 | 転生 | 4 | 0 | 27.0 | ノイズ | 左下（送り先を選べば働く） |  | Footing / Planted |
| 29 | 電気鞭のシガ | +3.62 | — | — | 3 | 1 | 12.0 | どこでも同じ | — |  | Scourge / Shame / Lash / LiveWire / ScourgeShock / StoredCharge / ShockWhipFlurry / ShockWhipChain / FearNoStun / ScourgeDaunt / BetrayBackswing / ChargeUncapped / ChargeKeep / ScourgeFearDoubled |
| 30 | 萎縮のクビ | +3.88 | -12.52 | 転生 | 2 | 0 | 30.0 | 化ける | 左下（送り先を選べば働く） | +2.3pt | Huddle / Daunt / DauntLeak |
| 31 | 突き返しのハネ | +4.12 | -3.59 | 転生 | 1 | 0 | 29.0 | 化ける | 左下（送り先を選べば働く） | 対照 0.0pt | Rebound / Overrun / Disarray / Blast / Spring / Tailwind / TailwindFighter / SpringGuard / SpringStay / SpringRow / Landing / SpringStrike / SpringDaunt / BlastReach / BlastStay |
| 32 | 大喰らいゴルム | +4.18 | +8.03 | 残す | 24 | 0 | 25.0 | 化ける | 上（単独で強い） |  | Colossus / Drain / GolmSwallowSpit / GolmSwallowSpend / GolmSwallowWeak / GolmRocketPunch / GolmRocketGrab |
| 33 | 喧噪のバサ | +4.62 | +34.83 | 残す | 3 | 0 | 34.0 | 化ける | 上（単独で強い） |  | Shuffler / Disarray / Squall / Gale / Tailwind / TailwindFighter |
| 34 | 施しのリリ | +5.67 | — | — | 9 | 0 | 17.0 | 化ける | — |  | Kiss / KissSpill / KissPain / KissVoid / KissTier / KissSteal / KissTri / KissRite5 |
| 35 | 鬨の号令ガン | +5.84 | +20.85 | 残す | 10 | 0 | 15.0 | どこでも同じ | 上（単独で強い） | −1.0pt | Rally / Reveille |
| 36 | 胞子体ムグ | +6.28 | +5.08 | 残す | 5 | 0 | 31.0 | ノイズ | 上（単独で強い） |  | Splitter / MugSowAll / MugSporeP |
| 37 | 背かれのソム | +7.00 | -13.43 | 転生 | 1 | 1 | 14.0 | ノイズ | 右下（選んでも働かない） |  | BetrayedShockNoThunder / SparkRain / SparkVeil / BeastBurstAlways / SwarmCall / EmergencyCall / EmergencyFocus / BeastDeathCount / BeastStand / BeastBetray / BeastResonance / BeastResonanceArc / BeastThrowBackfire / SparkVeilCap |
| 38 | 継ぎ当てのツギ | +8.81 | — | — | 4 | 1 | 45.0 | ノイズ | — |  | Plank / PlankScorch / Scrap / PlankRebound / PlankThick / PlankBase / FirstAid / AidSkill / PlankNeediest / FirstAidArmored / PlankOpening |
| 39 | 仇討ちのザン | +9.38 | -5.91 | 転生 | 4 | 0 | 8.0 | どこでも同じ | 右下（選んでも働かない） |  | Vendetta / Recoil / VendettaFrameAll / VendettaRound / VendettaMarkFirst / VendettaDirectOnly |
| 40 | 瘴気袋のグザ | +9.62 | +21.36 | 残す | 8 | 0 | 12.0 | どこでも同じ | 上（単独で強い） |  | Miasma / ScatterGather / MiasmaVent / MiasmaSplash / SplashUncapped / PoisonProof |
| 41 | 禍導のカタ | +10.00 | — | — | 1 | 1 | 16.0 | どこでも同じ | — |  | Thunder / ThunderLeak / ThunderPath / ShockStunHalf / Thundercloud / ThundercloudKeep / ThundercloudUncapped / ShockDaunt / ThunderPowderReturn / ShockStunOnce / ThundercloudAllyPops / KataRod / KataRodChain |
| 42 | 囃し立てのヒサ | +13.75 | +0.86 | 転生 | 10 | 0 | 12.0 | どこでも同じ | 右下（選んでも働かない） |  | Beckon / Flee / MarkRallyWide / MarkRallySelf / BeckonFeather / FrameAccuseQuiet / CommandBurst3 / HisaCover / RallyQuiet / CoverSkipLethal / CommandBurstCap9 / MarkInheritSpread / RallyFeatherQuiet |
| 43 | 棘鎧のカド | +15.14 | +32.59 | 残す | 10 | 0 | 14.0 | どこでも同じ | 上（単独で強い） |  | ThornGuard / Thorns / Immobile / Havoc / ThornsArmored / ThornsWound / ThornsBare |
| 44 | 焼け残りのボルグ | +15.82 | -4.51 | 転生 | 14 | 0 | 44.0 | 化ける | 左下（送り先を選べば働く） | 0.0pt | Splash / FireFeed / Cinder / FireArmor / FireSplash / SelfKindle / FireMend / FireWardAll / FireLevel / CinderWide / FireKeep / FireSpreadCap / FireUnleash / FoeFireLevel / FoeFireTick / FoeFireBrittle / FoeFireSpread / AllyFireTick / RadiateCall / CallFull / UnleashBlaze / BlazeSolo / KindleGuard / BlazeHoard / KindleOpen / BlazeFoeSurgeMax / BurnHitNoMend |
| 45 | 砕け盾のヒビ | +17.78 | +4.64 | 残す | 4 | 0 | 21.0 | 化ける | 上（単独で強い） |  | ShardGift / ShardOpen / ShardBareNoPlank / ShardFull |
| 46 | 逸らしのソラ | +18.10 | +1.62 | 残す | 6 | 0 | 7.0 | どこでも同じ | 上（単独で強い） |  | Divert / Deflect / Thrust / DivertPressure / DivertKeepBeckon |
| 47 | 継ぎ接ぎのヴェル | +19.57 | +10.99 | 残す | 15 | 0 | 6.0 | どこでも同じ | 上（単独で強い） |  | SoulSplitHalf / SoulSew |
| 48 | 逆しまのウツ | +25.60 | +3.89 | 残す | 5 | 0 | 27.0 | 化ける | 上（単独で強い） |  | Perverse / PerverseOver |
| 49 | 包みのセッキ | +25.62 | — | — | 3 | 0 | 35.0 | 化ける | — |  | SlimeWrap / SlimeOverflow / SlimeDeep / SlimeSwell / SlimeMelt |
| 50 | 呪詛官ネル | +32.48 | +17.08 | 残す | 6 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Curse / Hexer / HexLeak / CurseScar |
| 51 | 墓守リィカ | +33.30 | +9.81 | 残す | 12 | 0 | 1.0 | どこでも同じ | 上（単独で強い） |  | Necro / Sacrifice / RicaRaiseDeadStack / RicaAwakenRaise / RicaLayerFlurry / UncapRaise |
| 52 | 血詠みのアカ | — | — | — | 0 | 0 | — | — | — |  | BloodRead / ScatterGather |

## 自己検査

- `UnitCatalog.All` の 52 枚すべてに行がある: **○**（52 行）
- `checkup ideal` が値を返した駒: 51 / 52 （返さないのは `CompareBuilds()` に在席 0 枠の駒だけ）
- `stage catalog` が引けた駒: 51 / 52

所要 538.6 秒（うち `stage catalog` が 510.6 秒）。
