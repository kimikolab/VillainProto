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

固有の勝者の行（第2〜5波）は **6 行** —— 刻み (ノミ単騎) / 刻み×抉り (ノミ×エグ) / 反撃改3 (カド×ハギ) / 惨禍×死の連鎖 / 感電 (シガ×カタ×ソム) / 駆り立て改 (カリ×死軸)。

枠の値段の出どころは `design/PHASE172_REFORM.md`（7 枚ぶん）。**無い駒は空欄**——あの表は1つの枠でしか測っていないので、空欄は「0」ではない。

## 52 枚（**理想台の帰属の昇順**）

| # | 駒 | 理想台の帰属 | `checkup` 単独 | `checkup` 3分 | 在席枠 | 固有の勝者 | 列レンジ | 送り先 | 象限 | 枠の値段 | 札 |
|--:|---|--:|--:|---|--:|--:|--:|---|---|--:|---|
| 1 | のろまの巨兵ドルガ | -8.19 | -11.99 | 別扱い | 24 | 2 | 15.0 | どこでも同じ | 右下（選んでも働かない） |  | Sluggish / GradeStep |
| 2 | 駆り立てのカリ | -4.62 | +1.94 | 残す | 2 | 1 | 40.0 | 化ける | 上（単独で強い） |  | Goad |
| 3 | 追い打ちのハギ | -4.20 | -33.36 | 転生 | 5 | 1 | 23.0 | 化ける | 左下（送り先を選べば働く） | −5.3pt | Pursuer |
| 4 | 萎縮のクビ | -0.31 | -12.52 | 転生 | 2 | 0 | 11.0 | どこでも同じ | 右下（選んでも働かない） | +2.3pt | Huddle / Daunt / DauntLeak |
| 5 | 尾灯のトモ | +0.00 | +17.75 | 残す | 1 | 0 | 17.0 | 化ける | 上（単独で強い） |  | Taillight |
| 6 | 引き受けのウケ | +0.00 | +1.96 | 残す | 1 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Bear |
| 7 | 泥人形ムド | +0.00 | +16.35 | 残す | 8 | 0 | 26.0 | 化ける | 上（単独で強い） |  | Erupt / Smear / Hex / MudoCurseRage / MudoCurseHeavy |
| 8 | 渡しのワタ | +0.00 | +2.05 | 残す | 1 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Relay / RelayWide / RelayFeed / RelayFeedMudo |
| 9 | 澱み喰いのヴィオ | +0.00 | +2.62 | 残す | 2 | 0 | 22.0 | 化ける | 上（単独で強い） |  | Blightfed / BlightfedSkipCloak / StrikePoison / PoisonProof / VioFlurryA / VioBloodSip |
| 10 | 熾のホタ | +0.00 | +1.44 | 転生 | 3 | 0 | 30.0 | 化ける | 左下（送り先を選べば働く） |  | Pyre / PyreStage / PyreBurnout / PyreEmbers / CallFire / PyreLance / PyreCritical / PyreMend / EmbersChain / PyreOverflow / PyreFed / BurnoutHeavy / BurnHitNoMend |
| 11 | 疫みのラウ | +0.00 | +1.47 | 転生 | 3 | 0 | 7.0 | どこでも同じ | 右下（選んでも働かない） |  | Contagion / Touch / TouchLeak |
| 12 | 移り木のシオ | +0.00 | +8.54 | 残す | 1 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | Drifter / Regroup / DrifterMend / RegroupTend / RegroupTendSelf / ShioStage / Retreat / DriftSurge / RetreatHalf |
| 13 | 錯乱のササ | +0.00 | +10.02 | 残す | 1 | 0 | 14.0 | どこでも同じ | 上（単独で強い） |  | Brace / BraceHold / BraceWide / BraceNear |
| 14 | 毒喰らいのベニ | +0.12 | +2.11 | 残す | 2 | 0 | 10.0 | どこでも同じ | 上（単独で強い） |  | Inverse / Guren / Kindle / Taint / InverseLeak / GurenOpeningBurn |
| 15 | 鱗のウロ | +0.12 | +4.52 | 残す | 3 | 0 | 22.0 | 化ける | 上（単独で強い） |  | Scale |
| 16 | 毒吐きのスィド | +0.16 | +15.82 | 残す | 4 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Spew / PoisonProof / PoisonCloak / CloakRetort / Numb / SidOpeningCloak10 |
| 17 | 後備えのセッキ | +0.33 | +13.02 | 残す | 3 | 0 | 17.0 | ノイズ | 上（単独で強い） | −4.6pt | RearGuard / Rage |
| 18 | 火選りのヒヨ | +0.38 | -12.35 | 転生 | 4 | 0 | 19.0 | 化ける | 左下（送り先を選べば働く） |  | Favor / FireConvert / FireStoke / TurnGift / StokeStageAtk / GiftQuiet / SparkCatch / SparkUnleash / FavorLevel / GiftHoard / GiftOrder / BurnHitNoMend |
| 19 | 礫のガレ | +1.25 | -3.82 | 転生 | 1 | 0 | 1.0 | どこでも同じ | 右下（選んでも働かない） |  | PebbleGather / ScatterGather |
| 20 | 痺れ粉のトウ | +1.50 | +3.87 | 残す | 2 | 0 | 32.0 | ノイズ | 上（単独で強い） |  | ChargedPowder / ChargedPowderLeak / ShockStunHalf / ChargedPowderSpread / ShockDaunt / PowderDense / PowderDenseKeep / PowderDenseBurn / PowderContagion / PowderMarkFoeOnly / DenseConduct / DustBurst / PowderBurstMarked / PowderBurstFall / ShockStunOnce |
| 21 | 軋みのヨミ | +1.54 | +10.38 | 残す | 3 | 0 | 12.0 | どこでも同じ | 上（単独で強い） |  | Displaced / CreakSweep / KillImpact / ImpactTailwind |
| 22 | 逃亡兵セロ | +1.56 | +1.42 | 転生 | 12 | 0 | 6.0 | どこでも同じ | 右下（選んでも働かない） | +0.3pt | Evade / EvadeSwap / EvadeQuick / EvadeDrift / LastDodge / Decoy / EvadeMoveShot |
| 23 | 爆ぜるゾト | +1.58 | +13.04 | 残す | 8 | 2 | 16.0 | どこでも同じ | 上（単独で強い） |  | Bomber |
| 24 | 見境なしのミサ | +1.92 | +1.55 | 残す | 3 | 0 | 33.0 | ノイズ | 上（単独で強い） |  | Rupture / RuptureKeep / Feathers / FeatherMarkLayer / FeatherGrowUnmarked |
| 25 | 廃棄聖騎士ガルド | +2.29 | +15.59 | 残す | 37 | 1 | 8.0 | どこでも同じ | 上（単独で強い） |  | Guardian / Stoic / Parry / LastStandHold |
| 26 | 電気鞭のシガ | +3.25 | — | — | 3 | 1 | 22.0 | 化ける | — |  | Scourge / Shame / Lash / LiveWire / ScourgeShock / StoredCharge / ShockWhipFlurry / ShockWhipChain / FearNoStun / ScourgeDaunt / BetrayBackswing / ChargeUncapped / ChargeKeep / ScourgeFearDoubled |
| 27 | 据えのバン | +3.34 | +1.44 | 転生 | 4 | 0 | 21.0 | 化ける | 左下（送り先を選べば働く） |  | Footing / Planted |
| 28 | 縛めのクグ | +3.71 | +5.91 | 残す | 3 | 0 | 25.0 | 化ける | 上（単独で強い） |  | Grapple / Thread / ThreadCharge / WebCharge / ThreadPowderFlow / SilkBattery |
| 29 | 喧噪のバサ | +4.67 | +34.83 | 残す | 3 | 0 | 35.0 | 化ける | 上（単独で強い） |  | Shuffler / Disarray / Squall / Gale / Tailwind / TailwindFighter |
| 30 | 胞子体ムグ | +4.69 | +5.08 | 残す | 4 | 1 | 27.0 | 化ける | 上（単独で強い） |  | Splitter |
| 31 | 背かれのソム | +7.00 | -13.43 | 転生 | 1 | 1 | 16.0 | ノイズ | 右下（選んでも働かない） |  | BetrayedShockNoThunder / SparkRain / SparkVeil / BeastBurstAlways / SwarmCall / EmergencyCall / EmergencyFocus / BeastDeathCount / BeastStand / BeastBetray / BeastResonance / BeastResonanceArc / BeastThrowBackfire / SparkVeilCap |
| 32 | 仇討ちのザン | +7.75 | -5.91 | 転生 | 4 | 0 | 14.0 | どこでも同じ | 右下（選んでも働かない） |  | Vendetta / Recoil / VendettaFrameAll / VendettaRound / VendettaMarkFirst / VendettaDirectOnly |
| 33 | 大喰らいゴルム | +7.77 | +8.03 | 残す | 24 | 4 | 17.0 | 化ける | 上（単独で強い） |  | Colossus / Drain / GolmSwallowSpit / GolmSwallowSpend / GolmSwallowWeak / GolmRocketPunch / GolmRocketGrab |
| 34 | 分かちのドハ | +9.00 | +9.03 | 残す | 5 | 0 | 22.0 | 化ける | 上（単独で強い） |  | Sharer / SharerArmored / ShareBack / SharerNoDull / SharePush / SharePushPower / SharePushStrikers / DohaSplit |
| 35 | 焼け残りのボルグ | +9.35 | -4.51 | 転生 | 13 | 0 | 49.0 | 化ける | 左下（送り先を選べば働く） | 0.0pt | Splash / FireFeed / Cinder / FireArmor / FireSplash / SelfKindle / FireMend / FireWardAll / FireLevel / CinderWide / FireKeep / FireSpreadCap / FireUnleash / FoeFireLevel / FoeFireTick / FoeFireBrittle / FoeFireSpread / AllyFireTick / RadiateCall / CallFull / UnleashBlaze / BlazeSolo / KindleGuard / BlazeHoard / KindleOpen / BlazeFoeSurgeMax / BurnHitNoMend |
| 36 | 突き返しのハネ | +9.50 | -3.59 | 転生 | 1 | 0 | 38.0 | 化ける | 左下（送り先を選べば働く） | 対照 0.0pt | Rebound / Overrun / Disarray / Blast / Spring / Tailwind / TailwindFighter / SpringGuard / SpringStay / SpringRow / Landing / SpringStrike / SpringDaunt / BlastReach / BlastStay |
| 37 | 禍導のカタ | +10.00 | — | — | 1 | 1 | 15.0 | どこでも同じ | — |  | Thunder / ThunderLeak / ThunderPath / ShockStunHalf / Thundercloud / ThundercloudKeep / ThundercloudUncapped / ShockDaunt / ThunderPowderReturn / ShockStunOnce / ThundercloudAllyPops / KataRod / KataRodChain |
| 38 | 鬨の号令ガン | +10.15 | +20.85 | 残す | 10 | 2 | 16.0 | どこでも同じ | 上（単独で強い） | −1.0pt | Rally / Reveille |
| 39 | 継ぎ当てのツギ | +10.50 | — | — | 4 | 1 | 24.0 | 化ける | — |  | Plank / PlankScorch / Scrap / PlankRebound / PlankThick / PlankBase / FirstAid / AidSkill / PlankNeediest / FirstAidArmored / PlankOpening |
| 40 | 瘴気袋のグザ | +12.50 | +21.36 | 残す | 8 | 0 | 8.0 | どこでも同じ | 上（単独で強い） |  | Miasma / ScatterGather / MiasmaVent / MiasmaSplash / SplashUncapped / PoisonProof |
| 41 | 澱みのミオ | +14.58 | -0.16 | 転生 | 6 | 1 | 9.0 | どこでも同じ | 右下（選んでも働かない） |  | Concentrate / ConcentrateLeak / MireSlam / MireConduct / MireDull / MireCarry / MireHandoff / MireBurstStack |
| 42 | 砕け盾のヒビ | +14.75 | +4.64 | 残す | 4 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | ShardGift / ShardOpen / ShardBareNoPlank / ShardFull |
| 43 | 囃し立てのヒサ | +15.51 | +0.86 | 転生 | 10 | 1 | 8.0 | どこでも同じ | 右下（選んでも働かない） |  | Beckon / Flee / MarkRallyWide / MarkRallySelf / BeckonFeather / FrameAccuseQuiet / CommandBurst3 / HisaCover / RallyQuiet / CoverSkipLethal / CommandBurstCap9 / MarkInheritSpread / RallyFeatherQuiet |
| 44 | 刻みのノミ | +16.82 | +12.05 | 残す | 7 | 2 | 7.0 | どこでも同じ | 上（単独で強い） |  | Pellet / Carve / CarveOnce / Fixate |
| 45 | 継ぎ接ぎのヴェル | +16.82 | +10.99 | 残す | 14 | 3 | 21.0 | 化ける | 上（単独で強い） |  | Reviver / Stitch |
| 46 | 施しのリリ | +18.40 | — | — | 9 | 0 | 15.0 | どこでも同じ | — |  | Kiss / KissSpill / KissPain / KissVoid / KissTier / KissSteal / KissTri / KissRite5 |
| 47 | 棘鎧のカド | +22.06 | +32.59 | 残す | 10 | 2 | 18.0 | 化ける | 上（単独で強い） |  | ThornGuard / Thorns / Immobile / Havoc / ThornsArmored / ThornsWound / ThornsBare |
| 48 | 逆しまのウツ | +25.43 | +3.89 | 残す | 5 | 0 | 27.0 | 化ける | 上（単独で強い） |  | Perverse / PerverseOver |
| 49 | 逸らしのソラ | +25.96 | +1.62 | 残す | 6 | 0 | 8.0 | どこでも同じ | 上（単独で強い） |  | Divert / Deflect / Thrust / DivertPressure / DivertKeepBeckon |
| 50 | 呪詛官ネル | +32.48 | +17.08 | 残す | 6 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Curse / Hexer / HexLeak / CurseScar |
| 51 | 墓守リィカ | +51.24 | +9.81 | 残す | 11 | 2 | 12.0 | どこでも同じ | 上（単独で強い） |  | Necro / Sacrifice / RicaRaiseDeadStack / RicaAwakenRaise / RicaLayerFlurry / UncapRaise |
| 52 | 血詠みのアカ | — | — | — | 0 | 0 | — | — | — |  | BloodRead / ScatterGather |

## 自己検査

- `UnitCatalog.All` の 52 枚すべてに行がある: **○**（52 行）
- `checkup ideal` が値を返した駒: 51 / 52 （返さないのは `CompareBuilds()` に在席 0 枠の駒だけ）
- `stage catalog` が引けた駒: 51 / 52

所要 406.5 秒（うち `stage catalog` が 384.3 秒）。
