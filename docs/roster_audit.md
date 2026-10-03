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

固有の勝者の行（第2〜5波）は **8 行** —— 刻み (ノミ単騎) / 刻み×抉り (ノミ×エグ) / 反撃改2 (ガン×カド) / 死の連鎖 (リィカ軸) / 死の連鎖+後備え / 溜め改 (クグ×バン×ガン) / 追撃×死 (ハギ×リィカ) / 鱗 (ウロ×死軸)。

枠の値段の出どころは `design/PHASE172_REFORM.md`（7 枚ぶん）。**無い駒は空欄**——あの表は1つの枠でしか測っていないので、空欄は「0」ではない。

## 52 枚（**理想台の帰属の昇順**）

| # | 駒 | 理想台の帰属 | `checkup` 単独 | `checkup` 3分 | 在席枠 | 固有の勝者 | 列レンジ | 送り先 | 象限 | 枠の値段 | 札 |
|--:|---|--:|--:|---|--:|--:|--:|---|---|--:|---|
| 1 | のろまの巨兵ドルガ | -12.40 | -11.99 | 別扱い | 24 | 3 | 9.0 | どこでも同じ | 右下（選んでも働かない） |  | Sluggish / GradeStep |
| 2 | 追い打ちのハギ | -6.70 | -33.36 | 転生 | 5 | 1 | 4.0 | どこでも同じ | 右下（選んでも働かない） | −5.3pt | Pursuer |
| 3 | 駆り立てのカリ | -6.38 | +1.94 | 残す | 2 | 0 | 28.0 | ノイズ | 上（単独で強い） |  | Goad |
| 4 | 据えのバン | -1.16 | +1.44 | 転生 | 4 | 2 | 30.0 | 化ける | 左下（送り先を選べば働く） |  | Footing / Planted |
| 5 | 電気鞭のシガ | -0.69 | — | — | 2 | 0 | 19.0 | 化ける | — |  | Scourge / Shame / Lash / LiveWire / ScourgeShock |
| 6 | 尾灯のトモ | +0.00 | +17.75 | 残す | 1 | 0 | 16.0 | どこでも同じ | 上（単独で強い） |  | Taillight |
| 7 | 引き受けのウケ | +0.00 | +1.96 | 残す | 1 | 0 | 36.0 | 化ける | 上（単独で強い） |  | Bear |
| 8 | 渡しのワタ | +0.00 | +2.05 | 残す | 1 | 0 | 40.0 | 化ける | 上（単独で強い） |  | Relay |
| 9 | 熾のホタ | +0.00 | +1.44 | 転生 | 2 | 0 | 29.0 | 化ける | 左下（送り先を選べば働く） |  | Pyre / PyreStage / PyreBurnout / PyreEmbers / CallFire / PyreLance / PyreCritical / PyreMend / EmbersChain / PyreOverflow / PyreFed / BurnoutHeavy |
| 10 | 疫みのラウ | +0.00 | +1.47 | 転生 | 3 | 0 | 11.0 | どこでも同じ | 右下（選んでも働かない） |  | Contagion / Touch / TouchLeak |
| 11 | 移り木のシオ | +0.00 | +8.54 | 残す | 1 | 0 | 8.0 | どこでも同じ | 上（単独で強い） |  | Drifter / Regroup / DrifterMend / RegroupTend / RegroupTendSelf / ShioStage / Retreat / DriftSurge / RetreatHalf |
| 12 | 錯乱のササ | +0.00 | +10.02 | 残す | 1 | 0 | 27.0 | ノイズ | 上（単独で強い） |  | Brace |
| 13 | 泥人形ムド | +0.02 | +16.35 | 残す | 8 | 0 | 15.0 | どこでも同じ | 上（単独で強い） |  | Erupt / Smear / Hex |
| 14 | 痺れ粉のトウ | +0.38 | +3.87 | 残す | 2 | 0 | 22.0 | 化ける | 上（単独で強い） |  | Paralyze |
| 15 | 止めのトメ | +1.38 | +1.55 | 残す | 2 | 0 | 30.0 | 化ける | 上（単独で強い） |  | Finisher |
| 16 | 縛めのクグ | +3.33 | +5.91 | 残す | 3 | 1 | 20.0 | 化ける | 上（単独で強い） |  | Grapple |
| 17 | 逃亡兵セロ | +4.58 | +1.42 | 転生 | 12 | 0 | 17.0 | ノイズ | 左下（送り先を選べば働く） | +0.3pt | Evade / EvadeSwap / EvadeQuick / EvadeDrift / LastDodge / Decoy / EvadeMoveShot |
| 18 | 喧噪のバサ | +4.67 | +34.83 | 残す | 3 | 0 | 36.0 | 化ける | 上（単独で強い） |  | Shuffler / Disarray / Squall / Gale / Tailwind / TailwindFighter |
| 19 | 後備えのセッキ | +4.79 | +13.02 | 残す | 3 | 1 | 17.0 | 化ける | 上（単独で強い） | −4.6pt | RearGuard / Rage |
| 20 | 軋みのヨミ | +4.92 | +10.38 | 残す | 3 | 0 | 16.0 | どこでも同じ | 上（単独で強い） |  | Displaced / CreakSweep / KillImpact / ImpactTailwind |
| 21 | 毒吐きのスィド | +5.00 | +15.82 | 残す | 4 | 0 | 14.0 | どこでも同じ | 上（単独で強い） |  | Spew / VenomHeavy / Numb |
| 22 | 萎縮のクビ | +5.25 | -12.52 | 転生 | 2 | 0 | 30.0 | 化ける | 左下（送り先を選べば働く） | +2.3pt | Huddle / Daunt / DauntLeak |
| 23 | 火選りのヒヨ | +5.53 | -12.35 | 転生 | 4 | 0 | 13.0 | どこでも同じ | 右下（選んでも働かない） |  | Favor / FireConvert / FireStoke / TurnGift / StokeStageAtk / GiftQuiet / SparkCatch / SparkUnleash / FavorLevel / GiftHoard / GiftOrder |
| 24 | 大喰らいゴルム | +5.77 | +8.03 | 残す | 24 | 6 | 26.0 | 化ける | 上（単独で強い） |  | Colossus / Drain |
| 25 | 澱み喰いのヴィオ | +6.00 | +2.62 | 残す | 2 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | Blightfed / Spit |
| 26 | 鱗のウロ | +7.29 | +4.52 | 残す | 3 | 1 | 10.0 | どこでも同じ | 上（単独で強い） |  | Scale |
| 27 | 分かちのドハ | +7.38 | +9.03 | 残す | 5 | 1 | 22.0 | 化ける | 上（単独で強い） |  | Sharer / SharerArmored |
| 28 | 廃棄聖騎士ガルド | +8.15 | +15.59 | 残す | 35 | 0 | 11.0 | どこでも同じ | 上（単独で強い） |  | Guardian / Stoic / Parry / LastStandHold |
| 29 | 突き返しのハネ | +8.88 | -3.59 | 転生 | 1 | 0 | 40.0 | 化ける | 左下（送り先を選べば働く） | 対照 0.0pt | Rebound / Overrun / Disarray / Blast / Spring / Tailwind / TailwindFighter / SpringGuard / SpringStay / SpringRow / Landing / SpringStrike / SpringDaunt / BlastReach / BlastStay |
| 30 | 刻みのノミ | +10.69 | +12.05 | 残す | 6 | 2 | 19.0 | 化ける | 上（単独で強い） |  | Carve / Fixate |
| 31 | 瘴気袋のグザ | +12.64 | +21.36 | 残す | 8 | 0 | 16.0 | どこでも同じ | 上（単独で強い） |  | Miasma |
| 32 | 砕け盾のヒビ | +14.75 | +4.64 | 残す | 4 | 0 | 14.0 | どこでも同じ | 上（単独で強い） |  | Shatter / Frail |
| 33 | 焼け残りのボルグ | +15.00 | -4.51 | 転生 | 12 | 0 | 48.0 | 化ける | 左下（送り先を選べば働く） | 0.0pt | Splash / FireFeed / Cinder / FireArmor / FireSplash / SelfKindle / FireMend / FireWardAll / FireLevel / CinderWide / FireKeep / FireSpreadCap / FireUnleash / FoeFireLevel / FoeFireTick / FoeFireBrittle / FoeFireSpread / AllyFireTick / RadiateCall / CallFull / UnleashBlaze / BlazeSolo / KindleGuard / BlazeHoard / KindleOpen / BlazeFoeSurgeMax |
| 34 | 棘鎧のカド | +15.54 | +32.59 | 残す | 10 | 2 | 31.0 | 化ける | 上（単独で強い） |  | ThornGuard / Thorns / Immobile / Havoc / ThornsArmored |
| 35 | 毒喰らいのベニ | +20.06 | +2.11 | 残す | 2 | 0 | 23.0 | 化ける | 上（単独で強い） |  | Inverse / Guren / Kindle / Taint / InverseLeak / GurenOpeningBurn |
| 36 | 囃し立てのヒサ | +20.64 | +0.86 | 転生 | 9 | 1 | 12.0 | どこでも同じ | 右下（選んでも働かない） |  | Beckon / Flee |
| 37 | 胞子体ムグ | +23.53 | +5.08 | 残す | 4 | 2 | 21.0 | 化ける | 上（単独で強い） |  | Splitter |
| 38 | 継ぎ接ぎのヴェル | +24.04 | +10.99 | 残す | 14 | 5 | 26.0 | 化ける | 上（単独で強い） |  | Reviver / Stitch |
| 39 | 鬨の号令ガン | +25.83 | +20.85 | 残す | 9 | 3 | 9.0 | どこでも同じ | 上（単独で強い） | −1.0pt | Rally / Reveille |
| 40 | 仇討ちのザン | +26.12 | -5.91 | 転生 | 3 | 0 | 27.0 | 化ける | 左下（送り先を選べば働く） |  | Vendetta / Recoil |
| 41 | 澱みのミオ | +26.35 | -0.16 | 転生 | 5 | 0 | 15.0 | どこでも同じ | 右下（選んでも働かない） |  | Concentrate / ConcentrateLeak / MireSlam / MireConduct / MireDull / MireCarry / MireHandoff / MireBurstStack |
| 42 | 継ぎ当てのツギ | +26.58 | — | — | 3 | 0 | 11.0 | どこでも同じ | — |  | Plank / PlankScorch / Scrap / PlankRebound / PlankThick / PlankBase / FirstAid / AidSkill / PlankNeediest / FirstAidArmored / PlankOpening |
| 43 | 墓守リィカ | +27.44 | +9.81 | 残す | 11 | 4 | 24.0 | 化ける | 上（単独で強い） |  | Necro / Sacrifice |
| 44 | 逸らしのソラ | +28.94 | +1.62 | 残す | 6 | 0 | 17.0 | 化ける | 上（単独で強い） |  | Divert / Deflect / Thrust |
| 45 | 爆ぜるゾト | +29.02 | +13.04 | 残す | 8 | 4 | 32.0 | 化ける | 上（単独で強い） |  | Bomber |
| 46 | 施しのリリ | +30.27 | — | — | 8 | 0 | 6.0 | どこでも同じ | — |  | Kiss / KissSpill / KissPain / KissVoid / KissTier / KissSteal / KissTri / KissRite5 |
| 47 | 逆しまのウツ | +34.25 | +3.89 | 残す | 5 | 0 | 13.0 | どこでも同じ | 上（単独で強い） |  | Perverse |
| 48 | 呪詛官ネル | +35.52 | +17.08 | 残す | 6 | 0 | 29.0 | 化ける | 上（単独で強い） |  | Curse / Hexer / HexLeak |
| 49 | 礫のガレ | +57.75 | -3.82 | 転生 | 1 | 0 | 26.0 | 化ける | 左下（送り先を選べば働く） |  | Shrapnel |
| 50 | 禍導のカタ | — | — | — | 0 | 0 | — | — | — |  | Thunder / ThunderLeak / ThunderPath / ShockStunHalf |
| 51 | 背かれのソム | — | — | — | 0 | 0 | — | — | — |  | Betrayed |
| 52 | 血詠みのアカ | — | — | — | 0 | 0 | — | — | — |  | Ash |

## 自己検査

- `UnitCatalog.All` の 52 枚すべてに行がある: **○**（52 行）
- `checkup ideal` が値を返した駒: 49 / 52 （返さないのは `CompareBuilds()` に在席 0 枠の駒だけ）
- `stage catalog` が引けた駒: 49 / 52

所要 308.2 秒（うち `stage catalog` が 290.1 秒）。
