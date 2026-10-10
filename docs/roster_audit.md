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

固有の勝者の行（第2〜5波）は **8 行** —— 刻み (ノミ単騎) / 刻み×抉り (ノミ×エグ) / 反撃改2 (ガン×カド) / 感電 (シガ×カタ×ソム) / 死の連鎖 (リィカ軸) / 死の連鎖+後備え / 追撃×死 (ハギ×リィカ) / 鱗 (ウロ×死軸)。

枠の値段の出どころは `design/PHASE172_REFORM.md`（7 枚ぶん）。**無い駒は空欄**——あの表は1つの枠でしか測っていないので、空欄は「0」ではない。

## 52 枚（**理想台の帰属の昇順**）

| # | 駒 | 理想台の帰属 | `checkup` 単独 | `checkup` 3分 | 在席枠 | 固有の勝者 | 列レンジ | 送り先 | 象限 | 枠の値段 | 札 |
|--:|---|--:|--:|---|--:|--:|--:|---|---|--:|---|
| 1 | のろまの巨兵ドルガ | -11.22 | -11.99 | 別扱い | 24 | 2 | 12.0 | どこでも同じ | 右下（選んでも働かない） |  | Sluggish / GradeStep |
| 2 | 追い打ちのハギ | -7.97 | -33.36 | 転生 | 5 | 1 | 5.0 | どこでも同じ | 右下（選んでも働かない） | −5.3pt | Pursuer |
| 3 | 駆り立てのカリ | -6.50 | +1.94 | 残す | 2 | 0 | 37.0 | 化ける | 上（単独で強い） |  | Goad |
| 4 | 据えのバン | -4.50 | +1.44 | 転生 | 4 | 1 | 30.0 | 化ける | 左下（送り先を選べば働く） |  | Footing / Planted |
| 5 | 尾灯のトモ | +0.00 | +17.75 | 残す | 1 | 0 | 18.0 | 化ける | 上（単独で強い） |  | Taillight |
| 6 | 引き受けのウケ | +0.00 | +1.96 | 残す | 1 | 0 | 10.0 | どこでも同じ | 上（単独で強い） |  | Bear |
| 7 | 渡しのワタ | +0.00 | +2.05 | 残す | 1 | 0 | 10.0 | どこでも同じ | 上（単独で強い） |  | Relay |
| 8 | 熾のホタ | +0.00 | +1.44 | 転生 | 3 | 0 | 39.0 | 化ける | 左下（送り先を選べば働く） |  | Pyre / PyreStage / PyreBurnout / PyreEmbers / CallFire / PyreLance / PyreCritical / PyreMend / EmbersChain / PyreOverflow / PyreFed / BurnoutHeavy |
| 9 | 疫みのラウ | +0.00 | +1.47 | 転生 | 3 | 0 | 12.0 | どこでも同じ | 右下（選んでも働かない） |  | Contagion / Touch / TouchLeak |
| 10 | 移り木のシオ | +0.00 | +8.54 | 残す | 1 | 0 | 9.0 | どこでも同じ | 上（単独で強い） |  | Drifter / Regroup / DrifterMend / RegroupTend / RegroupTendSelf / ShioStage / Retreat / DriftSurge / RetreatHalf |
| 11 | 錯乱のササ | +0.00 | +10.02 | 残す | 1 | 0 | 23.0 | 化ける | 上（単独で強い） |  | Brace |
| 12 | 痺れ粉のトウ | +0.12 | +3.87 | 残す | 2 | 0 | 37.0 | ノイズ | 上（単独で強い） |  | ChargedPowder / ChargedPowderLeak / ShockStunHalf / ChargedPowderSpread |
| 13 | 見境なしのミサ | +0.42 | +1.55 | 残す | 3 | 0 | 28.0 | 化ける | 上（単独で強い） |  | Rupture / RuptureScar / RuptureKeep / Feathers / FeatherLoss / FeatherMarkLayer |
| 14 | 泥人形ムド | +1.14 | +16.35 | 残す | 8 | 0 | 22.0 | 化ける | 上（単独で強い） |  | Erupt / Smear / Hex |
| 15 | 電気鞭のシガ | +3.17 | — | — | 3 | 1 | 22.0 | ノイズ | — |  | Scourge / Shame / Lash / LiveWire / ScourgeShock / StoredCharge / ShockWhipFlurry / ShockWhipChain |
| 16 | 逃亡兵セロ | +4.54 | +1.42 | 転生 | 12 | 0 | 13.0 | どこでも同じ | 右下（選んでも働かない） | +0.3pt | Evade / EvadeSwap / EvadeQuick / EvadeDrift / LastDodge / Decoy / EvadeMoveShot |
| 17 | 軋みのヨミ | +4.58 | +10.38 | 残す | 3 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Displaced / CreakSweep / KillImpact / ImpactTailwind |
| 18 | 喧噪のバサ | +4.67 | +34.83 | 残す | 3 | 0 | 39.0 | 化ける | 上（単独で強い） |  | Shuffler / Disarray / Squall / Gale / Tailwind / TailwindFighter |
| 19 | 後備えのセッキ | +4.79 | +13.02 | 残す | 3 | 1 | 14.0 | どこでも同じ | 上（単独で強い） | −4.6pt | RearGuard / Rage |
| 20 | 大喰らいゴルム | +4.82 | +8.03 | 残す | 24 | 6 | 28.0 | 化ける | 上（単独で強い） |  | Colossus / Drain |
| 21 | 毒吐きのスィド | +5.00 | +15.82 | 残す | 4 | 0 | 14.0 | どこでも同じ | 上（単独で強い） |  | Spew / VenomHeavy / Numb |
| 22 | 萎縮のクビ | +5.25 | -12.52 | 転生 | 2 | 0 | 29.0 | 化ける | 左下（送り先を選べば働く） | +2.3pt | Huddle / Daunt / DauntLeak |
| 23 | 火選りのヒヨ | +5.53 | -12.35 | 転生 | 4 | 0 | 17.0 | 化ける | 左下（送り先を選べば働く） |  | Favor / FireConvert / FireStoke / TurnGift / StokeStageAtk / GiftQuiet / SparkCatch / SparkUnleash / FavorLevel / GiftHoard / GiftOrder |
| 24 | 澱み喰いのヴィオ | +6.00 | +2.62 | 残す | 2 | 0 | 16.0 | どこでも同じ | 上（単独で強い） |  | Blightfed / Spit |
| 25 | 廃棄聖騎士ガルド | +6.29 | +15.59 | 残す | 37 | 0 | 12.0 | どこでも同じ | 上（単独で強い） |  | Guardian / Stoic / Parry / LastStandHold |
| 26 | 鱗のウロ | +7.29 | +4.52 | 残す | 3 | 1 | 9.0 | どこでも同じ | 上（単独で強い） |  | Scale |
| 27 | 分かちのドハ | +8.35 | +9.03 | 残す | 5 | 1 | 26.0 | 化ける | 上（単独で強い） |  | Sharer / SharerArmored / ShareBack / SharerNoDull |
| 28 | 突き返しのハネ | +9.50 | -3.59 | 転生 | 1 | 0 | 41.0 | 化ける | 左下（送り先を選べば働く） | 対照 0.0pt | Rebound / Overrun / Disarray / Blast / Spring / Tailwind / TailwindFighter / SpringGuard / SpringStay / SpringRow / Landing / SpringStrike / SpringDaunt / BlastReach / BlastStay |
| 29 | 縛めのクグ | +9.71 | +5.91 | 残す | 3 | 0 | 27.0 | 化ける | 上（単独で強い） |  | Grapple / Thread / ThreadCharge / WebCharge |
| 30 | 刻みのノミ | +11.50 | +12.05 | 残す | 7 | 2 | 6.0 | どこでも同じ | 上（単独で強い） |  | Pellet / Carve / CarveOnce / Fixate |
| 31 | 焼け残りのボルグ | +11.56 | -4.51 | 転生 | 13 | 0 | 50.0 | 化ける | 左下（送り先を選べば働く） | 0.0pt | Splash / FireFeed / Cinder / FireArmor / FireSplash / SelfKindle / FireMend / FireWardAll / FireLevel / CinderWide / FireKeep / FireSpreadCap / FireUnleash / FoeFireLevel / FoeFireTick / FoeFireBrittle / FoeFireSpread / AllyFireTick / RadiateCall / CallFull / UnleashBlaze / BlazeSolo / KindleGuard / BlazeHoard / KindleOpen / BlazeFoeSurgeMax |
| 32 | 瘴気袋のグザ | +12.64 | +21.36 | 残す | 8 | 0 | 14.0 | どこでも同じ | 上（単独で強い） |  | Miasma |
| 33 | 背かれのソム | +13.38 | -13.43 | 転生 | 1 | 1 | 37.0 | 化ける | 左下（送り先を選べば働く） |  | BetrayedShockNoThunder / SparkRain / SparkVeil / BeastBurstAlways |
| 34 | 禍導のカタ | +13.88 | — | — | 1 | 1 | 26.0 | ノイズ | — |  | Thunder / ThunderLeak / ThunderPath / ShockStunHalf / Thundercloud / ThundercloudKeep / ThundercloudUncapped |
| 35 | 砕け盾のヒビ | +15.22 | +4.64 | 残す | 4 | 0 | 16.0 | どこでも同じ | 上（単独で強い） |  | Shatter / Frail |
| 36 | 鬨の号令ガン | +15.40 | +20.85 | 残す | 10 | 2 | 11.0 | どこでも同じ | 上（単独で強い） | −1.0pt | Rally / Reveille |
| 37 | 棘鎧のカド | +16.43 | +32.59 | 残す | 10 | 1 | 31.0 | 化ける | 上（単独で強い） |  | ThornGuard / Thorns / Immobile / Havoc / ThornsArmored |
| 38 | 毒喰らいのベニ | +17.94 | +2.11 | 残す | 2 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Inverse / Guren / Kindle / Taint / InverseLeak / GurenOpeningBurn |
| 39 | 澱みのミオ | +18.19 | -0.16 | 転生 | 6 | 1 | 14.0 | どこでも同じ | 右下（選んでも働かない） |  | Concentrate / ConcentrateLeak / MireSlam / MireConduct / MireDull / MireCarry / MireHandoff / MireBurstStack |
| 40 | 胞子体ムグ | +23.59 | +5.08 | 残す | 4 | 2 | 20.0 | 化ける | 上（単独で強い） |  | Splitter |
| 41 | 継ぎ接ぎのヴェル | +24.30 | +10.99 | 残す | 14 | 5 | 25.0 | 化ける | 上（単独で強い） |  | Reviver / Stitch |
| 42 | 囃し立てのヒサ | +26.80 | +0.86 | 転生 | 10 | 1 | 15.0 | どこでも同じ | 右下（選んでも働かない） |  | Beckon / Flee / MarkRallyWide / MarkRallySelf / BeckonFeather / FrameAccuseQuiet / CommandBall / HisaCover / RallyQuiet / CoverSkipLethal |
| 43 | 墓守リィカ | +27.85 | +9.81 | 残す | 11 | 4 | 22.0 | 化ける | 上（単独で強い） |  | Necro / Sacrifice |
| 44 | 仇討ちのザン | +27.88 | -5.91 | 転生 | 4 | 0 | 15.0 | どこでも同じ | 右下（選んでも働かない） |  | Vendetta / Recoil / VendettaFrameAll / VendettaRound / VendettaMarkFirst |
| 45 | 逸らしのソラ | +28.48 | +1.62 | 残す | 6 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Divert / Deflect / Thrust / DivertPressure / DivertKeepBeckon |
| 46 | 施しのリリ | +28.85 | — | — | 9 | 0 | 14.0 | どこでも同じ | — |  | Kiss / KissSpill / KissPain / KissVoid / KissTier / KissSteal / KissTri / KissRite5 |
| 47 | 爆ぜるゾト | +29.25 | +13.04 | 残す | 8 | 4 | 30.0 | 化ける | 上（単独で強い） |  | Bomber |
| 48 | 逆しまのウツ | +33.98 | +3.89 | 残す | 5 | 0 | 20.0 | 化ける | 上（単独で強い） |  | Perverse |
| 49 | 呪詛官ネル | +34.96 | +17.08 | 残す | 6 | 0 | 24.0 | 化ける | 上（単独で強い） |  | Curse / Hexer / HexLeak |
| 50 | 継ぎ当てのツギ | +38.19 | — | — | 4 | 1 | 6.0 | どこでも同じ | — |  | Plank / PlankScorch / Scrap / PlankRebound / PlankThick / PlankBase / FirstAid / AidSkill / PlankNeediest / FirstAidArmored / PlankOpening |
| 51 | 礫のガレ | +57.88 | -3.82 | 転生 | 1 | 0 | 26.0 | 化ける | 左下（送り先を選べば働く） |  | Shrapnel |
| 52 | 血詠みのアカ | — | — | — | 0 | 0 | — | — | — |  | Ash |

## 自己検査

- `UnitCatalog.All` の 52 枚すべてに行がある: **○**（52 行）
- `checkup ideal` が値を返した駒: 51 / 52 （返さないのは `CompareBuilds()` に在席 0 枠の駒だけ）
- `stage catalog` が引けた駒: 51 / 52

所要 676.9 秒（うち `stage catalog` が 633.8 秒）。
