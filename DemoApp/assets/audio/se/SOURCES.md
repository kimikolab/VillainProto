# 通常攻撃の効果音

ユーザー指定の Helton Yan's Pixel Combat - Single Files よりコピー。
元の素材は D:/Assets/SE/Helton Yan's Pixel Combat - Single Files に保管。

| アプリ内ファイル | 元ファイル |
|---|---|
| sword_slash_001.wav | DSGNMisc_MELEE-Sword Slash_HY_PC-001.wav |
| sword_slash_002.wav | DSGNMisc_MELEE-Sword Slash_HY_PC-002.wav |
| sword_slash_003.wav | DSGNMisc_MELEE-Sword Slash_HY_PC-003.wav |

上記3音は味方の通常攻撃に使用。

敵の通常攻撃も同じ Helton Yan's Pixel Combat - Single Files よりコピー。

| アプリ内ファイル | 元ファイル |
|---|---|
| enemy_swish_hit_004.wav | FGHTImpt_MELEE-Swish Hit_HY_PC-004.wav |
| enemy_swish_hit_005.wav | FGHTImpt_MELEE-Swish Hit_HY_PC-005.wav |
| enemy_swish_hit_006.wav | FGHTImpt_MELEE-Swish Hit_HY_PC-006.wav |

音源は無加工。攻撃者の陣営ごとの共通音としてランダム再生し、それぞれ直前と同じ音を避ける。
キャラ・攻撃型の専用音が登録されている場合はそちらを優先する。
音量は -16 dB、最大4音、再生速度によるピッチ変更なし。
配布・利用条件の原本は素材パックのライセンスを参照する。

## 死亡音

ユーザー指定の D:/Assets/SE/Springin より無加工でコピー。

| アプリ内ファイル | 元ファイル | 対象 |
|---|---|---|
| death_enemy.mp3 | 会心の一撃1.mp3 | 敵の死亡 |
| death_player.mp3 | 会心の一撃3.mp3 | 味方の死亡 |

死亡イベントで倒れる動きと同時に再生。攻撃音とは別枠で最大2音、-8 dB。
再開・戦闘画面を閉じたときは停止。`--effect=death-audio` で敵→味方の順に確認できる。

## ガルドの受け流し音

ユーザー指定の D:/Assets/SE/Springin より無加工でコピー。

| アプリ内ファイル | 元ファイル |
|---|---|
| parry_1.mp3 | 剣ぶつかり合い1.mp3 |
| parry_2.mp3 | 剣ぶつかり合い2.mp3 |
| parry_3.mp3 | 剣ぶつかり合い3.mp3 |

ガルドの Parry イベントの演出開始時にランダム再生し、直前と同じ音を避ける。
単なる庇いや通常被弾では鳴らない。攻撃音と合わせ最大4音、-16 dB。
`--effect=parry-audio` で連続発動を確認できる。

## ヨミ・カドの専用音

ユーザー指定の D:/Assets/SE/Springin より無加工でコピー。

| アプリ内ファイル | 元ファイル | タイミング |
|---|---|---|
| yomi_bonus.mp3 | 高速斬撃2.mp3 | ヨミの Reaction 付き Attack。共通攻撃音を置換 |
| yomi_attack.mp3 | 抜刀.mp3 | ヨミの通常攻撃（Reaction なし）。共通攻撃音を置換 |
| kado_hit.mp3 | 金属叩き.mp3 | カドの被弾。正のダメージかつ他者から、継続ダメージは除外 |
| kado_counter.mp3 | 斬撃11.mp3 | カドの反撃開始。巻き込み人数ぶんは重ねない |

専用音は攻撃音と同じ4音枠・-16 dB。`--effect=character-audio` で確認できる。

## 回復音

`D:/Assets/SE/効果音ラボ/戦闘/回復魔法1.mp3` を `heal_magic_1.mp3` へ無加工でコピー。
以前の ComfyUI 生成音（heal.wav）から差し替え。原本は変更しない。
ナラ・ノノ・ハリ・従軍司祭長・施しの司祭長が、別の駒を回復するときに使用。

特性・自己回復には `D:/Assets/SE/効果音ラボ/戦闘/回復魔法2.mp3` を
`heal_trait.mp3` へ無加工でコピーして使用する。書き手なし、自己回復、シオなど
上記の専任回復役以外からの回復が対象。

正の回復量で回復の光が出る際に再生する。
攻撃音と同じ4音枠・-16 dB。`--effect=heal` で確認可能。

## フィニッシュ音

`D:/Assets/SE/効果音ラボ/戦闘/K.O..mp3` を `finish_ko.mp3` へ無加工でコピー。
以前の「シャキーン3」から差し替え。
勝利結果の台本で、敵が最後に全滅する死亡イベントの通常死亡音を置換する。
蘇生・召喚後に敵が残る場合や敗北時には鳴らさない。リプレイごとに1回。
死亡音と同じ2音枠・-5 dB。`--effect=finish-audio` で確認可能。

## チャージ音

ユーザー指定の D:/Assets/SE/効果音ラボ/戦闘 より無加工でコピー。

| アプリ内ファイル | 元ファイル | 対象・タイミング |
|---|---|---|
| charge_magic.mp3 | 魔法陣を展開.mp3 | 詠唱兵（chanter）のチャージ開始 |
| charge_magic_release.mp3 | 雷魔法4.mp3 | 詠唱兵のチャージ解放 |
| charge_physical.mp3 | パワーチャージ.mp3 | 狙撃手（archer / archer_g）のチャージ開始 |
| charge_physical_release.mp3 | 必殺技ヒット.mp3 | 狙撃手のチャージ解放 |

開始音は予兆が初めて付いたときに1回。解放音は通常攻撃音を置換し、対象人数ぶん重ねない。
チャージ後の Skill も解放音を使用する。手番外攻撃ではチャージを消費せず、専用音も鳴らさない。
チャージ専用の2音枠・-8 dB。開始時0.75秒、解放時0.55秒だけ通常SEを -25 dBへ下げ、
0.18秒で -16 dBへ戻す。死亡・フィニッシュ音は下げない。
`--effect=charge-audio` で詠唱系→物理系を確認可能。

## 召喚・蘇生音

| アプリ内ファイル | 元ファイル | タイミング |
|---|---|---|
| summon.mp3 | D:/Assets/SE/Springin/モンスター召喚から出現.mp3 | Summonイベントで姿が現れ始める瞬間 |
| revive.mp3 | D:/Assets/SE/効果音ラボ/戦闘/魔法のステッキ.mp3 | Reviveイベントで姿が戻り始める瞬間 |

いずれも無加工。通常の開戦配置では召喚音を鳴らさない。
攻撃音と同じ4音枠・-16 dB。`--effect=life-audio` で召喚→蘇生を確認可能。

## 戦闘開始音

`D:/Assets/SE/効果音ラボ/戦闘/剣を抜く.mp3` を `battle_start.mp3` へ無加工でコピー。
「BATTLE START」表示と同時に1回再生し、開幕の状態付与が続く編成でも無音にしない。
攻撃音と同じ4音枠・-16 dB。リプレイ開始時にも毎回鳴らす。

## 攻撃力の増減音

ユーザー保管の `D:/Assets/SE/Springin` より無加工でコピー。

| アプリ内ファイル | 元ファイル | 対象 |
|---|---|---|
| attack_up_1.mp3 ～ attack_up_3.mp3 | 上昇1.mp3 ～ 上昇3.mp3 | 攻撃力上昇 |
| attack_down_1.mp3 ～ attack_down_3.mp3 | 下降1.mp3 ～ 下降3.mp3 | 攻撃力低下 |

それぞれランダム再生し、直前と同じ音を避ける。開戦時の全体強化・弱体など、
同じ向きの変化が180ミリ秒以内に連続した場合は1音にまとめる。
攻撃音と同じ4音枠・-16 dB。`--effect=stats` で上昇→下降の順に確認可能。

## 盤面ルール音

ユーザー指定の `D:/Assets/SE/効果音ラボ/戦闘` より無加工でコピー。

| アプリ内ファイル | 元ファイル | タイミング |
|---|---|---|
| rule_yoke.mp3 | 重力魔法1.mp3 | 軛が一撃を25で止める瞬間 |
| rule_drought.mp3 | HP吸収魔法2.mp3 | 渇きで回復が通らず枯れる瞬間 |
| hush_block.mp3 | ガラスにひびが入る.mp3 | 粛が反撃を止め、鎖が締まる瞬間 |
| hush_break_1.mp3 ～ hush_break_3.mp3 | ガラスが割れる1.mp3 ～ 3.mp3 | 粛の保持者が倒れ、鎖が砕ける瞬間 |

粛の破砕音はランダム再生し、直前と同じ音を避ける。一人の保持者から鎖が複数本
伸びていても、死亡一回につき一音だけ鳴らす。封じが発動するたびには鳴らさない。
軛は専用1音枠・-13 dB。粛の発動・破砕は専用2音枠・-12 dBで、死亡音と重なっても切らない。
渇きは通常SEと同じ4音枠・-16 dB。
軛・渇きは `--effect=yoke` / `--effect=drought`、
粛は `SealEffectCheck.tscn` で確認可能。
