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

固有の勝者の行（第2〜5波）は **6 行** —— 刻み (ノミ単騎) / 刻み×抉り (ノミ×エグ) / 感電 (シガ×カタ×ソム) / 死の連鎖 (リィカ軸) / 死の連鎖+後備え / 突き出し (セロ×ヨミ)。

枠の値段の出どころは `design/PHASE172_REFORM.md`（7 枚ぶん）。**無い駒は空欄**——あの表は1つの枠でしか測っていないので、空欄は「0」ではない。

## 52 枚（**理想台の帰属の昇順**）

| # | 駒 | 理想台の帰属 | `checkup` 単独 | `checkup` 3分 | 在席枠 | 固有の勝者 | 列レンジ | 送り先 | 象限 | 枠の値段 | 札 |
|--:|---|--:|--:|---|--:|--:|--:|---|---|--:|---|
| 1 | 追い打ちのハギ | -12.20 | -33.36 | 転生 | 5 | 0 | 5.0 | どこでも同じ | 右下（選んでも働かない） | −5.3pt | Pursuer |
| 2 | のろまの巨兵ドルガ | -8.85 | -11.99 | 別扱い | 24 | 2 | 9.0 | どこでも同じ | 右下（選んでも働かない） |  | Sluggish / GradeStep |
| 3 | 駆り立てのカリ | -8.81 | +1.94 | 残す | 2 | 0 | 32.0 | 化ける | 上（単独で強い） |  | Goad |
| 4 | 据えのバン | -3.47 | +1.44 | 転生 | 4 | 0 | 26.0 | 化ける | 左下（送り先を選べば働く） |  | Footing / Planted |
| 5 | 尾灯のトモ | +0.00 | +17.75 | 残す | 1 | 0 | 22.0 | 化ける | 上（単独で強い） |  | Taillight |
| 6 | 引き受けのウケ | +0.00 | +1.96 | 残す | 1 | 0 | 8.0 | どこでも同じ | 上（単独で強い） |  | Bear |
| 7 | 渡しのワタ | +0.00 | +2.05 | 残す | 1 | 0 | 8.0 | どこでも同じ | 上（単独で強い） |  | Relay / RelayWide / RelayFeed |
| 8 | 熾のホタ | +0.00 | +1.44 | 転生 | 3 | 0 | 29.0 | 化ける | 左下（送り先を選べば働く） |  | Pyre / PyreStage / PyreBurnout / PyreEmbers / CallFire / PyreLance / PyreCritical / PyreMend / EmbersChain / PyreOverflow / PyreFed / BurnoutHeavy / BurnHitNoMend |
| 9 | 疫みのラウ | +0.00 | +1.47 | 転生 | 3 | 0 | 13.0 | どこでも同じ | 右下（選んでも働かない） |  | Contagion / Touch / TouchLeak |
| 10 | 移り木のシオ | +0.00 | +8.54 | 残す | 1 | 0 | 12.0 | どこでも同じ | 上（単独で強い） |  | Drifter / Regroup / DrifterMend / RegroupTend / RegroupTendSelf / ShioStage / Retreat / DriftSurge / RetreatHalf |
| 11 | 錯乱のササ | +0.00 | +10.02 | 残す | 1 | 0 | 22.0 | 化ける | 上（単独で強い） |  | Brace |
| 12 | 泥人形ムド | +0.50 | +16.35 | 残す | 8 | 0 | 22.0 | 化ける | 上（単独で強い） |  | Erupt / Smear / Hex |
| 13 | 萎縮のクビ | +1.00 | -12.52 | 転生 | 2 | 0 | 13.0 | どこでも同じ | 右下（選んでも働かない） | +2.3pt | Huddle / Daunt / DauntLeak |
| 14 | 痺れ粉のトウ | +1.50 | +3.87 | 残す | 2 | 0 | 36.0 | ノイズ | 上（単独で強い） |  | ChargedPowder / ChargedPowderLeak / ShockStunHalf / ChargedPowderSpread / ShockDaunt / PowderDense / PowderDenseKeep / PowderDenseBurn / PowderContagion / PowderMarkFoeOnly / DenseConduct / DustBurst / PowderBurstMarked / PowderBurstFall / ShockStunOnce |
| 15 | 逃亡兵セロ | +1.59 | +1.42 | 転生 | 12 | 1 | 13.0 | どこでも同じ | 右下（選んでも働かない） | +0.3pt | Evade / EvadeSwap / EvadeQuick / EvadeDrift / LastDodge / Decoy / EvadeMoveShot |
| 16 | 見境なしのミサ | +1.92 | +1.55 | 残す | 3 | 0 | 23.0 | 化ける | 上（単独で強い） |  | Rupture / RuptureKeep / Feathers / FeatherMarkLayer / FeatherGrowUnmarked |
| 17 | 軋みのヨミ | +2.50 | +10.38 | 残す | 3 | 1 | 13.0 | どこでも同じ | 上（単独で強い） |  | Displaced / CreakSweep / KillImpact / ImpactTailwind |
| 18 | 後備えのセッキ | +2.92 | +13.02 | 残す | 3 | 1 | 13.0 | どこでも同じ | 上（単独で強い） | −4.6pt | RearGuard / Rage |
| 19 | 火選りのヒヨ | +2.97 | -12.35 | 転生 | 4 | 0 | 10.0 | どこでも同じ | 右下（選んでも働かない） |  | Favor / FireConvert / FireStoke / TurnGift / StokeStageAtk / GiftQuiet / SparkCatch / SparkUnleash / FavorLevel / GiftHoard / GiftOrder / BurnHitNoMend |
| 20 | 電気鞭のシガ | +3.42 | — | — | 3 | 1 | 14.0 | どこでも同じ | — |  | Scourge / Shame / Lash / LiveWire / ScourgeShock / StoredCharge / ShockWhipFlurry / ShockWhipChain / FearNoStun / ScourgeDaunt / BetrayBackswing / ChargeUncapped / ChargeKeep / ScourgeFearDoubled |
| 21 | 喧噪のバサ | +4.67 | +34.83 | 残す | 3 | 0 | 40.0 | 化ける | 上（単独で強い） |  | Shuffler / Disarray / Squall / Gale / Tailwind / TailwindFighter |
| 22 | 毒吐きのスィド | +5.00 | +15.82 | 残す | 4 | 0 | 10.0 | どこでも同じ | 上（単独で強い） |  | Spew / VenomHeavy / Numb |
| 23 | 廃棄聖騎士ガルド | +5.09 | +15.59 | 残す | 37 | 1 | 8.0 | どこでも同じ | 上（単独で強い） |  | Guardian / Stoic / Parry / LastStandHold |
| 24 | 澱み喰いのヴィオ | +6.00 | +2.62 | 残す | 2 | 0 | 20.0 | 化ける | 上（単独で強い） |  | Blightfed / Spit |
| 25 | 背かれのソム | +7.00 | -13.43 | 転生 | 1 | 1 | 16.0 | ノイズ | 右下（選んでも働かない） |  | BetrayedShockNoThunder / SparkRain / SparkVeil / BeastBurstAlways / SwarmCall / EmergencyCall / EmergencyFocus / BeastDeathCount / BeastStand / BeastBetray / BeastResonance / BeastResonanceArc / BeastThrowBackfire / SparkVeilCap |
| 26 | 毒喰らいのベニ | +7.56 | +2.11 | 残す | 2 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Inverse / Guren / Kindle / Taint / InverseLeak / GurenOpeningBurn |
| 27 | 鱗のウロ | +7.75 | +4.52 | 残す | 3 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Scale |
| 28 | 分かちのドハ | +8.82 | +9.03 | 残す | 5 | 0 | 24.0 | 化ける | 上（単独で強い） |  | Sharer / SharerArmored / ShareBack / SharerNoDull / SharePush / SharePushPower / SharePushStrikers / DohaSplit |
| 29 | 突き返しのハネ | +9.50 | -3.59 | 転生 | 1 | 0 | 42.0 | 化ける | 左下（送り先を選べば働く） | 対照 0.0pt | Rebound / Overrun / Disarray / Blast / Spring / Tailwind / TailwindFighter / SpringGuard / SpringStay / SpringRow / Landing / SpringStrike / SpringDaunt / BlastReach / BlastStay |
| 30 | 縛めのクグ | +9.71 | +5.91 | 残す | 3 | 0 | 26.0 | 化ける | 上（単独で強い） |  | Grapple / Thread / ThreadCharge / WebCharge / ThreadPowderFlow / SilkBattery |
| 31 | 禍導のカタ | +10.00 | — | — | 1 | 1 | 12.0 | どこでも同じ | — |  | Thunder / ThunderLeak / ThunderPath / ShockStunHalf / Thundercloud / ThundercloudKeep / ThundercloudUncapped / ShockDaunt / ThunderPowderReturn / ShockStunOnce / ThundercloudAllyPops / KataRod / KataRodChain |
| 32 | 大喰らいゴルム | +10.67 | +8.03 | 残す | 24 | 5 | 14.0 | どこでも同じ | 上（単独で強い） |  | Colossus / Drain / GolmSwallowSpit / GolmSwallowSpend / GolmSwallowWeak / GolmRocketPunch / GolmRocketGrab |
| 33 | 焼け残りのボルグ | +11.45 | -4.51 | 転生 | 13 | 0 | 52.0 | 化ける | 左下（送り先を選べば働く） | 0.0pt | Splash / FireFeed / Cinder / FireArmor / FireSplash / SelfKindle / FireMend / FireWardAll / FireLevel / CinderWide / FireKeep / FireSpreadCap / FireUnleash / FoeFireLevel / FoeFireTick / FoeFireBrittle / FoeFireSpread / AllyFireTick / RadiateCall / CallFull / UnleashBlaze / BlazeSolo / KindleGuard / BlazeHoard / KindleOpen / BlazeFoeSurgeMax / BurnHitNoMend |
| 34 | 瘴気袋のグザ | +12.64 | +21.36 | 残す | 8 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | Miasma |
| 35 | 鬨の号令ガン | +14.29 | +20.85 | 残す | 10 | 1 | 8.0 | どこでも同じ | 上（単独で強い） | −1.0pt | Rally / Reveille |
| 36 | 砕け盾のヒビ | +15.19 | +4.64 | 残す | 4 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | Shatter / Frail |
| 37 | 棘鎧のカド | +16.65 | +32.59 | 残す | 10 | 0 | 27.0 | 化ける | 上（単独で強い） |  | ThornGuard / Thorns / Immobile / Havoc / ThornsArmored |
| 38 | 刻みのノミ | +16.82 | +12.05 | 残す | 7 | 2 | 7.0 | どこでも同じ | 上（単独で強い） |  | Pellet / Carve / CarveOnce / Fixate |
| 39 | 澱みのミオ | +18.94 | -0.16 | 転生 | 6 | 1 | 13.0 | どこでも同じ | 右下（選んでも働かない） |  | Concentrate / ConcentrateLeak / MireSlam / MireConduct / MireDull / MireCarry / MireHandoff / MireBurstStack |
| 40 | 仇討ちのザン | +21.25 | -5.91 | 転生 | 4 | 0 | 8.0 | どこでも同じ | 右下（選んでも働かない） |  | Vendetta / Recoil / VendettaFrameAll / VendettaRound / VendettaMarkFirst / VendettaDirectOnly |
| 41 | 施しのリリ | +22.07 | — | — | 9 | 0 | 14.0 | どこでも同じ | — |  | Kiss / KissSpill / KissPain / KissVoid / KissTier / KissSteal / KissTri / KissRite5 |
| 42 | 逸らしのソラ | +25.96 | +1.62 | 残す | 6 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Divert / Deflect / Thrust / DivertPressure / DivertKeepBeckon |
| 43 | 胞子体ムグ | +26.59 | +5.08 | 残す | 4 | 1 | 18.0 | 化ける | 上（単独で強い） |  | Splitter |
| 44 | 継ぎ接ぎのヴェル | +26.95 | +10.99 | 残す | 14 | 4 | 23.0 | 化ける | 上（単独で強い） |  | Reviver / Stitch |
| 45 | 囃し立てのヒサ | +26.99 | +0.86 | 転生 | 10 | 0 | 10.0 | どこでも同じ | 右下（選んでも働かない） |  | Beckon / Flee / MarkRallyWide / MarkRallySelf / BeckonFeather / FrameAccuseQuiet / CommandBurst3 / HisaCover / RallyQuiet / CoverSkipLethal / CommandBurstCap9 / MarkInheritSpread |
| 46 | 継ぎ当てのツギ | +29.38 | — | — | 4 | 1 | 7.0 | どこでも同じ | — |  | Plank / PlankScorch / Scrap / PlankRebound / PlankThick / PlankBase / FirstAid / AidSkill / PlankNeediest / FirstAidArmored / PlankOpening |
| 47 | 爆ぜるゾト | +30.41 | +13.04 | 残す | 8 | 2 | 27.0 | 化ける | 上（単独で強い） |  | Bomber |
| 48 | 墓守リィカ | +31.50 | +9.81 | 残す | 11 | 2 | 21.0 | 化ける | 上（単独で強い） |  | Necro / Sacrifice |
| 49 | 呪詛官ネル | +32.23 | +17.08 | 残す | 6 | 1 | 18.0 | 化ける | 上（単独で強い） |  | Curse / Hexer / HexLeak |
| 50 | 逆しまのウツ | +33.08 | +3.89 | 残す | 5 | 0 | 24.0 | 化ける | 上（単独で強い） |  | Perverse / PerverseOver |
| 51 | 礫のガレ | +57.88 | -3.82 | 転生 | 1 | 0 | 28.0 | 化ける | 左下（送り先を選べば働く） |  | Shrapnel |
| 52 | 血詠みのアカ | — | — | — | 0 | 0 | — | — | — |  | Ash |

## 自己検査

- `UnitCatalog.All` の 52 枚すべてに行がある: **○**（52 行）
- `checkup ideal` が値を返した駒: 51 / 52 （返さないのは `CompareBuilds()` に在席 0 枠の駒だけ）
- `stage catalog` が引けた駒: 51 / 52

所要 381.3 秒（うち `stage catalog` が 358.7 秒）。
