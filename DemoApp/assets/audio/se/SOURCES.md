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
音量は -10 dB、最大4音、再生速度によるピッチ変更なし。
配布・利用条件の原本は素材パックのライセンスを参照する。

## 死亡音

ユーザー指定の D:/Assets/SE/Springin より無加工でコピー。

| アプリ内ファイル | 元ファイル | 対象 |
|---|---|---|
| death_enemy.mp3 | 会心の一撃1.mp3 | 敵の死亡 |
| death_player.mp3 | 会心の一撃3.mp3 | 味方の死亡 |

死亡イベントで倒れる動きと同時に再生。攻撃音とは別枠で最大2音、-5 dB。
再開・戦闘画面を閉じたときは停止。`--effect=death-audio` で敵→味方の順に確認できる。

## ガルドの受け流し音

ユーザー指定の D:/Assets/SE/Springin より無加工でコピー。

| アプリ内ファイル | 元ファイル |
|---|---|
| parry_1.mp3 | 剣ぶつかり合い1.mp3 |
| parry_2.mp3 | 剣ぶつかり合い2.mp3 |
| parry_3.mp3 | 剣ぶつかり合い3.mp3 |

ガルドの Parry イベントの演出開始時にランダム再生し、直前と同じ音を避ける。
単なる庇いや通常被弾では鳴らない。攻撃音と合わせ最大4音、-10 dB。
`--effect=parry-audio` で連続発動を確認できる。

## ウツの攻撃音

ユーザー指定の `D:/Assets/SE/効果音ラボ/戦闘` より無加工でコピー。

| アプリ内ファイル | 元ファイル |
|---|---|
| utsu_attack_1.mp3 | 剣で斬る1.mp3 |
| utsu_attack_2.mp3 | 剣で斬る2.mp3 |
| utsu_attack_3.mp3 | 剣で斬る3.mp3 |
| utsu_attack_4.mp3 | 剣で斬る4.mp3 |
| utsu_attack_5.mp3 | 剣で斬る5.mp3 |

連撃を含む各攻撃の着弾で1音ずつランダム再生し、直前と同じ音を避ける。
通常攻撃と同じ4音枠・-10 dB。再生速度によるピッチ変更なし。

## ヨミ・カドの専用音

ユーザー指定の D:/Assets/SE/Springin より無加工でコピー。

| アプリ内ファイル | 元ファイル | タイミング |
|---|---|---|
| yomi_bonus.mp3 | 高速斬撃2.mp3 | ヨミの Reaction 付き Attack。共通攻撃音を置換 |
| yomi_attack.mp3 | 抜刀.mp3 | ヨミの通常攻撃（Reaction なし）。共通攻撃音を置換 |
| kado_hit.mp3 | 金属叩き.mp3 | カドの被弾。正のダメージかつ他者から、継続ダメージは除外 |
| kado_counter.mp3 | 斬撃11.mp3 | カドの反撃開始。巻き込み人数ぶんは重ねない |

専用音は攻撃音と同じ4音枠・-10 dB。`--effect=character-audio` で確認できる。

## 回復音

`D:/Assets/SE/効果音ラボ/戦闘/回復魔法1.mp3` を `heal_magic_1.mp3` へ無加工でコピー。
以前の ComfyUI 生成音（heal.wav）から差し替え。原本は変更しない。
ナラ・ノノ・ハリ・従軍司祭長・施しの司祭長が、別の駒を回復するときに使用。

特性・自己回復には `D:/Assets/SE/効果音ラボ/戦闘/回復魔法2.mp3` を
`heal_trait.mp3` へ無加工でコピーして使用する。書き手なし、自己回復、シオなど
上記の専任回復役以外からの回復が対象。

正の回復量で回復の光が出る際に再生する。
攻撃音と同じ4音枠・-10 dB。`--effect=heal` で確認可能。

## フィニッシュ音

`D:/Assets/SE/効果音ラボ/戦闘/K.O..mp3` を `finish_ko.mp3` へ無加工でコピー。
以前の「シャキーン3」から差し替え。
勝利結果の台本で、敵が最後に全滅する死亡イベントの通常死亡音を置換する。
蘇生・召喚後に敵が残る場合や敗北時には鳴らさない。リプレイごとに1回。
死亡音と同じ2音枠・-2 dB。`--effect=finish-audio` で確認可能。

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
チャージ専用の2音枠・-5 dB。開始時0.75秒、解放時0.55秒だけ通常SEを -19 dBへ下げ、
0.18秒で -10 dBへ戻す。死亡・フィニッシュ音は下げない。
`--effect=charge-audio` で詠唱系→物理系を確認可能。

## 召喚・蘇生音

| アプリ内ファイル | 元ファイル | タイミング |
|---|---|---|
| summon.mp3 | D:/Assets/SE/Springin/モンスター召喚から出現.mp3 | Summonイベントで姿が現れ始める瞬間 |
| revive.mp3 | D:/Assets/SE/効果音ラボ/戦闘/魔法のステッキ.mp3 | Reviveイベントで姿が戻り始める瞬間 |

いずれも無加工。通常の開戦配置では召喚音を鳴らさない。
攻撃音と同じ4音枠・-10 dB。`--effect=life-audio` で召喚→蘇生を確認可能。

## 戦闘開始音

`D:/Assets/SE/効果音ラボ/戦闘/剣を抜く.mp3` を `battle_start.mp3` へ無加工でコピー。
「BATTLE START」表示と同時に1回再生し、開幕の状態付与が続く編成でも無音にしない。
攻撃音と同じ4音枠・-10 dB。リプレイ開始時にも毎回鳴らす。

## 攻撃力の増減音

ユーザー保管の `D:/Assets/SE/Springin` より無加工でコピー。

| アプリ内ファイル | 元ファイル | 対象 |
|---|---|---|
| attack_up_1.mp3 ～ attack_up_3.mp3 | 上昇1.mp3 ～ 上昇3.mp3 | 攻撃力上昇 |
| attack_down_1.mp3 ～ attack_down_3.mp3 | 下降1.mp3 ～ 下降3.mp3 | 攻撃力低下 |

それぞれランダム再生し、直前と同じ音を避ける。開戦時の全体強化・弱体など、
同じ向きの変化が180ミリ秒以内に連続した場合は1音にまとめる。
攻撃音と同じ4音枠・-10 dB。`--effect=stats` で上昇→下降の順に確認可能。

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
軛は専用1音枠・-7 dB。粛の発動・破砕は専用2音枠・-6 dBで、死亡音と重なっても切らない。
渇きは通常SEと同じ4音枠・-10 dB。
軛・渇きは `--effect=yoke` / `--effect=drought`、
粛は `SealEffectCheck.tscn` で確認可能。

## ガルドの強化反射

ユーザー指定の `D:/Assets/SE/効果音ラボ/戦闘/魔法反射.mp3` を無加工で
`gald_reflect.mp3` にコピー。強化が盾に到着した瞬間に、分配先の人数によらず1回再生する。
通常SEと同じ音量・原音のピッチを使う。回復拒否では鳴らさない。

## シガの鞭

ユーザー指定の効果音ラボ素材を無加工でコピー。シガの攻撃時に3種類から選び、同じ音の連続を避ける。

| アプリ内ファイル | 元ファイル |
|---|---|
| shiga_attack_1.mp3 | D:/Assets/SE/効果音ラボ/戦闘/鞭で攻撃1.mp3 |
| shiga_attack_2.mp3 | D:/Assets/SE/効果音ラボ/戦闘/鞭で攻撃2.mp3 |
| shiga_attack_3.mp3 | D:/Assets/SE/効果音ラボ/戦闘/鞭で攻撃3.mp3 |

## ソラの突き

ユーザー指定の効果音ラボ素材を無加工でコピー。突きを放つ瞬間に1回再生する。

| アプリ内ファイル | 元ファイル | 段数 |
|---|---|---|
| sora_thrust_2_3.mp3 | D:/Assets/SE/効果音ラボ/演出/シャキーン1.mp3 | 2〜3段 |
| sora_thrust_4.mp3 | D:/Assets/SE/効果音ラボ/演出/シャキーン2.mp3 | 4段（表示上の最大） |

0〜1段は通常の攻撃音。専用音は解放音用の2音枠・-5 dB・原音ピッチで鳴らし、
頭の0.35秒だけ通常SEを下げる。通常の攻撃音とは重ねない。

## 状態異常音

ユーザー指定の次のファイルを無加工でコピー。通常SEと同じ4音枠・-10 dBで再生する。

| アプリ内ファイル | 元ファイル | タイミング |
|---|---|---|
| burn_gain.mp3 | D:/Assets/SE/Springin/着火1.mp3 | 燃焼付与時 |
| burn_damage.mp3 | D:/Assets/SE/Springin/着火2.mp3 | 燃焼ダメージ表示時 |
| poison_gain.mp3 | D:/Assets/SE/効果音ラボ/戦闘/毒魔法1.mp3 | 毒付与時 |
| poison_damage.mp3 | D:/Assets/SE/効果音ラボ/戦闘/毒魔法2.mp3 | 毒ダメージ表示時 |
| stagger_gain.mp3 | D:/Assets/SE/効果音ラボ/演出/足首がグキッ.mp3 | 転倒付与時 |
| confused_gain.mp3 | D:/Assets/SE/効果音ラボ/演出/ヒヨコが頭の上を回るmp3.mp3 | 混乱付与時 |
| cowed_gain.mp3 | D:/Assets/SE/効果音ラボ/演出/カタカタ震える.mp3 | シガの悲鳴による竦み付与時（同時付与は1音） |

残量の写し・解除・転倒の手番喪失・混乱の攻撃時には付与音を鳴らさない。
燃焼・毒のダメージ音は既存の状態効果とダメージの対応表を使い、正のダメージにのみ鳴らす。

## 移動編成のSE

ユーザー指定の `D:/Assets/SE/効果音ラボ/戦闘` から無加工でコピー（Springin・Helton Yanは下表のフルパス）。元ファイルとSHA-256の一致を確認。

| アプリ内ファイル | 元ファイル | タイミング | 通常時の音量 |
|---|---|---|---|
| sero_bow.mp3 | 弓矢を放つ.mp3 | セロの通常単体矢・回避からの単体撃ち返し | -10 dB |
| sero_pierce.wav | D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNTonl_SKILL IMPACT-Retro Laser 1_HY_PC-006.wav | セロの貫通矢の発射 | -10 dB |
| sero_barrage.wav | D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNMisc_PROJECTILE-Laser Shot_HY_PC-006.wav | セロの乱れ撃ち、1本ごとの発射 | -14 dB |
| shio_vine_pull.mp3 | 鞭を振り回す2.mp3 | シオの退避・立て直しの蔓 | -12 dB |
| basa_wind.mp3 | 風魔法1.mp3 | 専用演出に結び付かないバサ由来の隊列移動（予備） | -15 dB |
| basa_tornado.mp3 | D:/Assets/SE/Springin/強風1.mp3 | 隊列入れ替えの羽ばたき・竜巻発生時、両陣地で1回 | -15 dB |
| basa_sweep.mp3 | D:/Assets/SE/Springin/強風3.mp3 | バサの攻撃開始（通常の薙ぎ・追加の突風） | -12 dB |
| basa_tailwind.mp3 | D:/Assets/SE/Springin/強風2.mp3 | 追い風の発動時 | -14 dB |
| hane_smash_ice.mp3 | 氷魔法1.mp3 | 手番の敵射出（大キックに重ねる）・吹っ飛ばしの巻き込み衝突 | 主 -9 dB / 巻き込み -16 dB |
| hane_dropkick.mp3 | 大キック.mp3 | 手番の吹っ飛ばしでドロップキックが接触した瞬間 | -10 dB |
| hane_spring_block.mp3 | パンチを受け止める.mp3 | 味方を守る弾き返しの掌の接触 | -12 dB |
| hane_spring_kick.mp3 | 中キック.mp3 | 本人の被弾からの飛び蹴りの接触 | -12 dB |
| hane_ally_bump.mp3 | D:/Assets/SE/効果音ラボ/演出/ボヨン.mp3 | 着地の反動で味方に接触した瞬間、1回 | -10 dB |
| movement_land.mp3 | 倒れる.mp3 | 吹っ飛ばし・弾き返し・ピンの着地 | 主 -14 dB / ピン -17 dB |
| yomi_sheathe.mp3 | 刀を鞘にしまう1.mp3 | ヨミの通常・追加攻撃の差分が終了したとき | -13 dB |
| sero_arrow_hit.mp3 | 弓矢が刺さる.mp3 | セロの矢の飛行終了時、1射につき1回 | -12 dB |
| sero_evade.mp3 | 逃走.mp3 | 通常回避・必死の回避。必死の回避直後のEvade通知とは重ねない | -12 dB |
| hane_windup.mp3 | D:/Assets/SE/Springin/高速移動.mp3 | ハネの吹っ飛ばしの射出直前 | -13 dB |

以前の `hane_smash_hit.mp3`（重いキック1.mp3）は比較・差し戻し用に残し、現在は再生しない。

通常SEの4音枠と溜め中の音量抑制を共用し、原音ピッチで再生。
予備の風と追い風は各140ms・蔓70ms・巻き込み衝突65ms・ピン着地80ms以内の密集を間引く。竜巻・薙ぎ・追い風は独立した再生判定で、直前の別の風に抑止されない。弓・薙ぎ・主衝突・主目標の着地は省略しない。
着地は描画アニメーション完了時に1回だけ再生し、後続Moveでは重複させない。
死亡・再開・画面を閉じる操作で古い着地・着弾・納刀の予約を無効化する。
ヨミの差分が別の姿勢に上書きされた場合も、その攻撃の納刀音はキャンセルする。
`res://MovementAudioCheck.tscn` で復号・最大音数・原音ピッチ・1/2倍速の着地／着弾／納刀タイミングと停止を検査。

## 燃焼軸の演出

2026-10-02、ユーザー指定の掲剣音・大剣＋爆発2と、承認された候補を無加工でコピー。
12ファイルすべて元素材とのSHA-256一致を確認。効果音ラボの元フォルダは
`D:/Assets/SE/効果音ラボ/戦闘/`。

燃焼軸はユーザーの音量調整指示により一律 **+3 dB**。以下の表と本文は調整前の基準値で、
実際の再生音量は各値に3 dBを加えた値（例: 掲剣 -6 dB、大剣 -7 dB、爆発 -9 dB）。
大技の4層と通常枠の補助音の両方へ適用する。

| アプリ内ファイル | 元素材 | 使用箇所／基準音量（再生時+3 dB） |
|---|---|---|
| fire_hota_raise.wav | D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/MAGSpel_CAST-High Powering Up_HY_PC-004.wav | ホタ掲剣／-9 dB |
| fire_sword_slam.mp3 | 大剣で斬る.mp3 | 炎剣の着弾／-10 dB |
| fire_explosion.mp3 | 爆発2.mp3 | 上記と同時／-12 dB |
| fire_inferno.mp3 | 火炎魔法3.mp3 | 着弾の0.07秒後に火柱／-16 dB |
| fire_jet.mp3 | 火炎魔法2.mp3 | ボルグ爆炎／-12 dB、ホタ段3・臨界／-11 dB |
| fire_projectile.mp3 | 火炎魔法1.mp3 | ホタ段1・2の発射／-13 dB |
| fire_aura.mp3 | オーラ2.mp3 | 爆炎の解放／-12 dB、火勢4到達／-15 dB |
| fire_gift.mp3 | 魔法陣を展開.mp3 | ギフトの受け渡し／-13 dB |
| fire_gift_turn.mp3 | ステータス上昇魔法2.mp3 | ギフトの手番開始／-11 dB |
| fire_condense.mp3 | ステータス上昇魔法1.mp3 | あぶれた火の凝縮／-15 dB |
| fire_heal.mp3 | 回復魔法1.mp3 | 味方への爆炎が癒しになる／-17 dB |
| fire_charge.mp3 | パワーチャージ.mp3 | 大火槍・臨界・ボルグの溜め／-9 dB |

くべられる火は既存の `burn_gain.mp3`（Springin 着火1、-15 dB）、残り火の各着弾は
`burn_damage.mp3`（Springin 着火2、-14 dB）。火の雨は同音を-18 dBで鳴らす。
育ち・煽り・防御も既存の着火音。旧版の薙ぎは既存の斬撃、放熱の印は既存の回復音を維持。

大技は掲剣／斬撃／爆発／轟音の4枠を確保し、火の雨や着火に余韻を切らせない。
掲剣0.70秒（振り下ろし前に静けさ）、大剣0.85秒、爆発1.05秒、火柱1.35秒を上限に、
末尾22%をフェード。ボルグはオーラ0.60秒＋火炎0.85秒。長さ・開始遅延は再生速度に追従し、
ピッチは原音を保つ。原本の加工はしない。
通常の4音枠に入る育ち・防御・煽り・補充・凝縮・癒しは種類ごと85ms、火勢4は180ms以内を間引く。
ギフトの手番・残り火・火の雨は毎件鳴る。停止・再戦時は大技のフェードと遅延再生も破棄する。
`res://FireCheck.tscn -- --audio` で全音源の復号、1/2倍速の着弾・3層の重なり、
原音ピッチ、連続着弾で余韻が切れないこと、停止時の予約破棄と5連撃を確認する。

## ミサ・シガ・カタの指定SE（2026-10-08）

ユーザー指定の11ファイルを無加工でコピー。すべて元素材とSHA-256が一致する。
同日の追加指定でカタの大落雷を「雷魔法4」のみに変更。雷魔法1は未使用で、使用音源は10ファイル。
音量・再生尺・左右定位は実行時に調整し、音程と原本を維持する。

| アプリ内ファイル | 元素材 |
|---|---|
| `misa_beam_1.wav` | `D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNTonl_SKILL IMPACT-Retro Laser 2_HY_PC-001.wav` |
| `misa_beam_2.wav` | `D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNTonl_SKILL IMPACT-Retro Laser 2_HY_PC-002.wav` |
| `misa_beam_3.wav` | `D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNTonl_SKILL IMPACT-Retro Laser 2_HY_PC-003.wav` |
| `misa_beam_hit.wav` | `D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNMisc_HIT-Zap Laser_HY_PC-003.wav` |
| `misa_deploy.wav` | `D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/WHSH_MOVEMENT-Windy Passby_HY_PC-005.wav` |
| `misa_funnel_move.mp3` | `D:/Assets/SE/効果音ラボ/戦闘/高速移動.mp3` |
| `misa_feather_lost.mp3` | `D:/Assets/SE/効果音ラボ/戦闘/石が砕ける.mp3` |
| `shiga_electric_whip.mp3` | `D:/Assets/SE/効果音ラボ/戦闘/鞭で攻撃5.mp3` |
| `shiga_electric_hit.wav` | `D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNImpt_EXPLOSION-Electric Hit_HY_PC-006.wav` |
| `kata_thunder_heavy_1.mp3` | `D:/Assets/SE/効果音ラボ/戦闘/雷魔法1.mp3` |
| `kata_thunder_heavy_4.mp3` | `D:/Assets/SE/効果音ラボ/戦闘/雷魔法4.mp3` |

| 使用箇所 | 基準音量 | 1倍速の再生上限・鳴らし方 |
|---|---|---|
| ミサ・発射 | -17 dB | 3種類を順に使用。0.24秒、発射8枠。毎発鳴らす |
| ミサ・着弾 | -14 dB | 0.20秒、発射と別の6枠。毎発鳴らす |
| ミサ・全域展開 | -13 dB | 0.65秒、展開開始に1回 |
| ミサ・旋回／帰還 | -17 dB | 0.38秒。次の標的へ移る時と実際の帰還開始に1回 |
| ミサ・羽の消失 | -18 dB | 0.32秒。粒子へ変わる時に鳴らし、85ms以内の密集はまとめる |
| シガ・電撃鞭 | 鞭 -10 dB／電撃 -15 dB | 接触で2音同時。鞭0.55秒・電撃0.65秒。薙ぎは最初の接触に1組 |
| カタ・大落雷 | 雷魔法4 -15 dB | 1.50秒。雷雲ありの主雷で単独再生し、後続の跳ねで重複しない |

末尾28%をフェードし、長さは再生速度に追従する。高速連射の発射は最低0.08秒、着弾は最低0.06秒を確保する。
発射位置と命中位置から左右定位を付ける。展開／移動2枠・消失2枠・電撃鞭4枠・大落雷2枠を分離し、指定音源は最大24枠。
従来の合成光線・合成展開・合成の重い雷は指定音に差し替え。電撃鞭に通常の鞭SE、大落雷に従来の主雷SEを重複再生しない。
蓄電・乱射移行・粉・糸など未指定の合成音は継続する。
終了・画面離脱・再戦では全音とフェードを停止し、破棄時には左右定位用のバスも取り除く。

`res://ShockMarkAudioCheck.tscn` で使用する10音源の復号、左右・1倍／2倍の発射／着弾／帰還／消失、
鞭の2層と雷魔法4の単独再生、倍速時の音程、死亡・再戦・画面離脱・終了での停止を検査する。
