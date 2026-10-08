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

固有の勝者の行（第2〜5波）は **8 行** —— 刻み (ノミ単騎) / 刻み×抉り (ノミ×エグ) / 反撃改2 (ガン×カド) / 感電 (シガ×カタ×ソム) / 死の連鎖 (リィカ軸) / 死の連鎖+後備え / 追撃×死 (ハギ×リィカ) / 鱗 (ウロ×死軸)。

枠の値段の出どころは `design/PHASE172_REFORM.md`（7 枚ぶん）。**無い駒は空欄**——あの表は1つの枠でしか測っていないので、空欄は「0」ではない。

## 52 枚（**理想台の帰属の昇順**）

| # | 駒 | 理想台の帰属 | `checkup` 単独 | `checkup` 3分 | 在席枠 | 固有の勝者 | 列レンジ | 送り先 | 象限 | 枠の値段 | 札 |
|--:|---|--:|--:|---|--:|--:|--:|---|---|--:|---|
| 1 | のろまの巨兵ドルガ | -11.27 | -11.99 | 別扱い | 24 | 2 | 11.0 | どこでも同じ | 右下（選んでも働かない） |  | Sluggish / GradeStep |
| 2 | 追い打ちのハギ | -6.70 | -33.36 | 転生 | 5 | 1 | 5.0 | どこでも同じ | 右下（選んでも働かない） | −5.3pt | Pursuer |
| 3 | 駆り立てのカリ | -6.38 | +1.94 | 残す | 2 | 0 | 33.0 | ノイズ | 上（単独で強い） |  | Goad |
| 4 | 据えのバン | -3.69 | +1.44 | 転生 | 4 | 1 | 31.0 | 化ける | 左下（送り先を選べば働く） |  | Footing / Planted |
| 5 | 見境なしのミサ | -0.12 | +1.55 | 残す | 3 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Rupture / RuptureScar / RuptureKeep / Feathers / FeatherLoss |
| 6 | 尾灯のトモ | +0.00 | +17.75 | 残す | 1 | 0 | 21.0 | 化ける | 上（単独で強い） |  | Taillight |
| 7 | 引き受けのウケ | +0.00 | +1.96 | 残す | 1 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Bear |
| 8 | 渡しのワタ | +0.00 | +2.05 | 残す | 1 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Relay |
| 9 | 熾のホタ | +0.00 | +1.44 | 転生 | 3 | 0 | 38.0 | 化ける | 左下（送り先を選べば働く） |  | Pyre / PyreStage / PyreBurnout / PyreEmbers / CallFire / PyreLance / PyreCritical / PyreMend / EmbersChain / PyreOverflow / PyreFed / BurnoutHeavy |
| 10 | 疫みのラウ | +0.00 | +1.47 | 転生 | 3 | 0 | 13.0 | どこでも同じ | 右下（選んでも働かない） |  | Contagion / Touch / TouchLeak |
| 11 | 移り木のシオ | +0.00 | +8.54 | 残す | 1 | 0 | 8.0 | どこでも同じ | 上（単独で強い） |  | Drifter / Regroup / DrifterMend / RegroupTend / RegroupTendSelf / ShioStage / Retreat / DriftSurge / RetreatHalf |
| 12 | 錯乱のササ | +0.00 | +10.02 | 残す | 1 | 0 | 26.0 | 化ける | 上（単独で強い） |  | Brace |
| 13 | 泥人形ムド | +0.02 | +16.35 | 残す | 8 | 0 | 18.0 | ノイズ | 上（単独で強い） |  | Erupt / Smear / Hex |
| 14 | 背かれのソム | +0.12 | -13.43 | 転生 | 1 | 1 | 29.0 | ノイズ | 左下（送り先を選べば働く） |  | BetrayedShockNoThunder |
| 15 | 痺れ粉のトウ | +0.19 | +3.87 | 残す | 2 | 0 | 30.0 | ノイズ | 上（単独で強い） |  | ChargedPowder / ChargedPowderLeak / ShockStunHalf / ChargedPowderSpread |
| 16 | 逃亡兵セロ | +4.58 | +1.42 | 転生 | 12 | 0 | 15.0 | どこでも同じ | 右下（選んでも働かない） | +0.3pt | Evade / EvadeSwap / EvadeQuick / EvadeDrift / LastDodge / Decoy / EvadeMoveShot |
| 17 | 喧噪のバサ | +4.67 | +34.83 | 残す | 3 | 0 | 38.0 | 化ける | 上（単独で強い） |  | Shuffler / Disarray / Squall / Gale / Tailwind / TailwindFighter |
| 18 | 後備えのセッキ | +4.79 | +13.02 | 残す | 3 | 1 | 14.0 | どこでも同じ | 上（単独で強い） | −4.6pt | RearGuard / Rage |
| 19 | 軋みのヨミ | +4.92 | +10.38 | 残す | 3 | 0 | 17.0 | 化ける | 上（単独で強い） |  | Displaced / CreakSweep / KillImpact / ImpactTailwind |
| 20 | 毒吐きのスィド | +5.00 | +15.82 | 残す | 4 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Spew / VenomHeavy / Numb |
| 21 | 大喰らいゴルム | +5.02 | +8.03 | 残す | 24 | 6 | 26.0 | 化ける | 上（単独で強い） |  | Colossus / Drain |
| 22 | 萎縮のクビ | +5.25 | -12.52 | 転生 | 2 | 0 | 32.0 | 化ける | 左下（送り先を選べば働く） | +2.3pt | Huddle / Daunt / DauntLeak |
| 23 | 電気鞭のシガ | +5.50 | — | — | 3 | 1 | 18.0 | ノイズ | — |  | Scourge / Shame / Lash / LiveWire / ScourgeShock / StoredCharge / ShockWhipFlurry / ShockWhipChain |
| 24 | 火選りのヒヨ | +5.53 | -12.35 | 転生 | 4 | 0 | 13.0 | どこでも同じ | 右下（選んでも働かない） |  | Favor / FireConvert / FireStoke / TurnGift / StokeStageAtk / GiftQuiet / SparkCatch / SparkUnleash / FavorLevel / GiftHoard / GiftOrder |
| 25 | 澱み喰いのヴィオ | +6.00 | +2.62 | 残す | 2 | 0 | 16.0 | どこでも同じ | 上（単独で強い） |  | Blightfed / Spit |
| 26 | 廃棄聖騎士ガルド | +6.52 | +15.59 | 残す | 37 | 0 | 8.0 | どこでも同じ | 上（単独で強い） |  | Guardian / Stoic / Parry / LastStandHold |
| 27 | 鱗のウロ | +7.29 | +4.52 | 残す | 3 | 1 | 11.0 | どこでも同じ | 上（単独で強い） |  | Scale |
| 28 | 分かちのドハ | +7.78 | +9.03 | 残す | 5 | 1 | 24.0 | 化ける | 上（単独で強い） |  | Sharer / SharerArmored / ShareBack / SharerNoDull |
| 29 | 突き返しのハネ | +8.88 | -3.59 | 転生 | 1 | 0 | 43.0 | 化ける | 左下（送り先を選べば働く） | 対照 0.0pt | Rebound / Overrun / Disarray / Blast / Spring / Tailwind / TailwindFighter / SpringGuard / SpringStay / SpringRow / Landing / SpringStrike / SpringDaunt / BlastReach / BlastStay |
| 30 | 縛めのクグ | +9.71 | +5.91 | 残す | 3 | 0 | 29.0 | 化ける | 上（単独で強い） |  | Grapple / Thread / ThreadCharge / WebCharge |
| 31 | 刻みのノミ | +11.25 | +12.05 | 残す | 7 | 2 | 6.0 | どこでも同じ | 上（単独で強い） |  | Pellet / Carve / CarveOnce / Fixate |
| 32 | 瘴気袋のグザ | +12.64 | +21.36 | 残す | 8 | 0 | 17.0 | 化ける | 上（単独で強い） |  | Miasma |
| 33 | 焼け残りのボルグ | +12.68 | -4.51 | 転生 | 13 | 0 | 50.0 | 化ける | 左下（送り先を選べば働く） | 0.0pt | Splash / FireFeed / Cinder / FireArmor / FireSplash / SelfKindle / FireMend / FireWardAll / FireLevel / CinderWide / FireKeep / FireSpreadCap / FireUnleash / FoeFireLevel / FoeFireTick / FoeFireBrittle / FoeFireSpread / AllyFireTick / RadiateCall / CallFull / UnleashBlaze / BlazeSolo / KindleGuard / BlazeHoard / KindleOpen / BlazeFoeSurgeMax |
| 34 | 禍導のカタ | +13.25 | — | — | 1 | 1 | 44.0 | 化ける | — |  | Thunder / ThunderLeak / ThunderPath / ShockStunHalf / Thundercloud / ThundercloudKeep / ThundercloudUncapped |
| 35 | 砕け盾のヒビ | +14.81 | +4.64 | 残す | 4 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | Shatter / Frail |
| 36 | 鬨の号令ガン | +15.31 | +20.85 | 残す | 10 | 2 | 7.0 | どこでも同じ | 上（単独で強い） | −1.0pt | Rally / Reveille |
| 37 | 棘鎧のカド | +15.76 | +32.59 | 残す | 10 | 1 | 32.0 | 化ける | 上（単独で強い） |  | ThornGuard / Thorns / Immobile / Havoc / ThornsArmored |
| 38 | 毒喰らいのベニ | +17.88 | +2.11 | 残す | 2 | 0 | 17.0 | 化ける | 上（単独で強い） |  | Inverse / Guren / Kindle / Taint / InverseLeak / GurenOpeningBurn |
| 39 | 澱みのミオ | +18.35 | -0.16 | 転生 | 6 | 1 | 15.0 | どこでも同じ | 右下（選んでも働かない） |  | Concentrate / ConcentrateLeak / MireSlam / MireConduct / MireDull / MireCarry / MireHandoff / MireBurstStack |
| 40 | 胞子体ムグ | +23.53 | +5.08 | 残す | 4 | 2 | 19.0 | 化ける | 上（単独で強い） |  | Splitter |
| 41 | 継ぎ接ぎのヴェル | +24.46 | +10.99 | 残す | 14 | 5 | 23.0 | 化ける | 上（単独で強い） |  | Reviver / Stitch |
| 42 | 囃し立てのヒサ | +24.60 | +0.86 | 転生 | 10 | 1 | 19.0 | 化ける | 左下（送り先を選べば働く） |  | Beckon / Flee / MarkRallyWide |
| 43 | 墓守リィカ | +27.44 | +9.81 | 残す | 11 | 4 | 20.0 | 化ける | 上（単独で強い） |  | Necro / Sacrifice |
| 44 | 逸らしのソラ | +28.42 | +1.62 | 残す | 6 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | Divert / Deflect / Thrust / DivertPressure |
| 45 | 施しのリリ | +29.01 | — | — | 9 | 0 | 11.0 | どこでも同じ | — |  | Kiss / KissSpill / KissPain / KissVoid / KissTier / KissSteal / KissTri / KissRite5 |
| 46 | 爆ぜるゾト | +29.02 | +13.04 | 残す | 8 | 4 | 31.0 | 化ける | 上（単独で強い） |  | Bomber |
| 47 | 逆しまのウツ | +34.25 | +3.89 | 残す | 5 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Perverse |
| 48 | 呪詛官ネル | +35.31 | +17.08 | 残す | 6 | 0 | 27.0 | 化ける | 上（単独で強い） |  | Curse / Hexer / HexLeak |
| 49 | 仇討ちのザン | +35.97 | -5.91 | 転生 | 4 | 0 | 29.0 | 化ける | 左下（送り先を選べば働く） |  | Vendetta / Recoil |
| 50 | 継ぎ当てのツギ | +38.09 | — | — | 4 | 1 | 6.0 | どこでも同じ | — |  | Plank / PlankScorch / Scrap / PlankRebound / PlankThick / PlankBase / FirstAid / AidSkill / PlankNeediest / FirstAidArmored / PlankOpening |
| 51 | 礫のガレ | +57.75 | -3.82 | 転生 | 1 | 0 | 27.0 | 化ける | 左下（送り先を選べば働く） |  | Shrapnel |
| 52 | 血詠みのアカ | — | — | — | 0 | 0 | — | — | — |  | Ash |

## 自己検査

- `UnitCatalog.All` の 52 枚すべてに行がある: **○**（52 行）
- `checkup ideal` が値を返した駒: 51 / 52 （返さないのは `CompareBuilds()` に在席 0 枠の駒だけ）
- `stage catalog` が引けた駒: 51 / 52

所要 369.0 秒（うち `stage catalog` が 347.1 秒）。
