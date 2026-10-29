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

## リィカの覚醒攻撃（2026-10-25）

ユーザー指定の同じ Helton Yan's Pixel Combat - Single Files より、以下を無加工でコピー。

| アプリ内ファイル | 元ファイル | 対象 |
|---|---|---|
| rica_sword_critical_001.wav | DSGNTonl_MELEE-Sword Critical_HY_PC-001.wav | 3–5層・回転薙ぎ |
| rica_sword_critical_002.wav | DSGNTonl_MELEE-Sword Critical_HY_PC-002.wav | 6–8層・二連撃の各打 |
| rica_sword_critical_003.wav | DSGNTonl_MELEE-Sword Critical_HY_PC-003.wav | 9層以上・三連撃の各打 |

本編とFxViewer共通の実接触コマで、1打につき1音。-10 dB・ピッチ1・通常SEの4音枠を使用する。
0–2層のシャベルは従来の `kado_hit.mp3`（金属叩き・+2 dB）を継続する。
購入音源本体とインポート情報はGit管理から除外し、別環境では冒頭の原本フォルダから上記名で配置する。
未配置の段は従来の共通斬撃音へ戻る。

2026-10-25 追加指定：効果音ラボ `D:/Assets/SE/効果音ラボ/戦闘/` から以下を無加工でコピーし、同じ接触コマでSword Criticalへ重ねる。

| アプリ内ファイル | 元ファイル | 重ねる段 |
|---|---|---|
| rica_sword_hit_5.mp3 | 刀で斬る5.mp3 | 3–5層：Critical 001と同時 |
| rica_sword_hit_4.mp3 | 刀で斬る4.mp3 | 6–8層：Critical 002と各打で同時 |
| rica_sword_hit_1.mp3 | 刀で斬る1.mp3 | 9層以上：Critical 003と各打で同時 |

Sword Criticalは-10 dB、「刀で斬る」は-8 dB（刀音だけ+2 dB）、ピッチ1。2音を同期ストリームにまとめ、1打で通常SEの1枠を使う。
追加音が元の剣音を途中で押し出さず、停止・再戦で両方とも止まる。追加音が未配置ならSword Criticalのみ。
今回の3本とインポート情報もGitから除外し、上表の原本から配置する。

## リィカの覚醒・着地・光の吸収（2026-10-25）

Helton Yan's Pixel Combat - Single Files の指定音源を無加工でコピーする。

| アプリ内ファイル | 元ファイル | 再生位置・音量 |
|---|---|---|
| rica_awaken_3.wav | DSGNSynth_BUFF-Plus Damage_HY_PC-001.wav | 3層の段へ上がった瞬間・-8 dB |
| rica_awaken_6.wav | DSGNSynth_BUFF-Plus Damage_HY_PC-003.wav | 6層の段へ上がった瞬間・-8 dB |
| rica_awaken_9.wav | DSGNSynth_BUFF-Plus Damage_HY_PC-006.wav | 9層の段へ上がった瞬間・-8 dB |
| rica_landing.wav | DSGNImpt_EXPLOSION-Mecha Limb Explosion_HY_PC-001.wav | 三連撃の落下斬りが接地した接触コマ・-6 dB（初版-12 dBから+6 dB） |
| rica_absorb.wav | MAGAngl_BUFF-Effect Success_HY_PC-001.wav | 生きている味方リィカへ光が流れる瞬間・-16 dB |

覚醒は実層の写しから段が上がった場合だけ。18層など9層以上で積み増す際は再生しない。
落下斬りの着地は命中先の人数によらず1打で1音。吸収は複数の仲間・リィカがいても180ms以内の連続を1音にまとめる。
斬撃とは独立した3枠（覚醒・着地・吸収）を使い、ピッチは速度によらず1。一時停止に追従し、非表示・再戦時に停止する。
購入音源本体とインポート情報はGitから除外する。未配置の専用音は省略する。

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
| kado_hit.mp3 | 金属叩き.mp3 | カドの被弾。正のダメージかつ他者から、継続ダメージは除外。リィカ0–2層のシャベル命中にも流用（+2 dB） |
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
| fire_jet.mp3 | 火炎魔法2.mp3 | ボルグ爆炎／-12 dB |
| fire_hota_strong_pierce.mp3 | D:/Assets/SE/Springin/炎の風2.mp3 | ホタ段3・強貫通の発射時／-11 dB（共通補正+3 dB）、原音ピッチ、0.65秒を再生速度で調整し末尾をフェード |
| fire_projectile.mp3 | 火炎魔法1.mp3 | ホタ段1・2の発射／-13 dB |
| fire_aura.mp3 | オーラ2.mp3 | 爆炎の解放／-12 dB、火勢4到達／-15 dB |
| fire_gift.mp3 | 魔法陣を展開.mp3 | ギフトの受け渡し／-13 dB、エレの哀歌の受け渡し／-18 dB |
| fire_gift_turn.mp3 | ステータス上昇魔法2.mp3 | ギフトの手番開始／-11 dB、エレの追加手番開始／-15 dB |
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

## 粛の伝令・破砕時の悲鳴（2026-10-10）

ユーザー指定の `D:/Assets/SE/効果音ラボ/声素材/「きゃああーー！」.mp3` を
`husher_shatter_cry.mp3` へ無加工でコピー。
SHA-256: `A56870574A8D9DF5C2810A113F310C89DA8EF87611E1CB5F0EDDACEAA2CB3DC8`。

粛の「砕けた」の瞬間に、既存の鎖の破裂音と重ねて一度再生する。
合成の仮悲鳴を置き換え、専用枠・-8 dB・原音程・全尺で再生。倍速でも短縮や末尾フェードをしない。
終了・画面離脱・再戦では停止する。

## 粛の展開音（2026-10-10）

ユーザー指定の `D:/Assets/SE/効果音ラボ/戦闘/重力魔法2.mp3` を `hush_opening.mp3` へ無加工でコピー。
SHA-256: `EE595A4C0D4DDE8FF2AB7D246209FBC4FE8B5A8DDC2F0B7BA8F4BC907CE84DB1`。
開戦の集光0.28秒後、粛の波が広がり始めるときに一度だけ鳴らす。複数保持者でも一度。
専用枠・-10 dB・原音程・全尺。Master経由で粛のローパスを通さず、倍速でも短縮しない。
粛の解除、終了・画面離脱・再戦で停止。集光途中に中断した場合は再生予約も破棄する。

## ドハ・痛みの受け取りと札の離脱（2026-10-10）

ユーザー指定の2音を無加工でコピー。原本は `D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/`。

| アプリ内ファイル | 原本ファイル | SHA-256 |
|---|---|---|
| `doha_pain.wav` | `DSGNMisc_HIT-Noisy Hit_HY_PC-001.wav` | `17922f9b1292a69f9b2efb0b5ce9d018ca1ced19a3308d0f8ff6c9f104a99b01` |
| `doha_give.wav` | `MAGAngl_BUFF-Effect Success_HY_PC-006.wav` | `fc8b94f23a6c37c2c4e54bc70e63a20bd6742590aa7b4f9ffc8fcde6f7f746b4` |

胸に痛みが届くとNoisy Hit（-13 dB、0.45秒上限）、0.10秒の受け止めの後、札の離脱でEffect Success（-14 dB、0.60秒上限）。
末尾28%をフェード。倍速では尺を縮めるが各0.24／0.30秒を下限とし、音程は原音を維持する。
各2枠を交互に使用し、往復の2音が互いの余韻を切らない。左右定位はドハの画面上の胸位置。
痛みの移動を描く時計に同期し、到達前の死亡・画面離脱・再戦では予約を捨てる。
終了・画面離脱・再戦では音とフェードを止める。同じ被弾の連鎖は2026-10-11の調整でまとめ、ドハごとの往復につき1組を再生する。

## ソム・背きの雷と共鳴返し（2026-10-13）

`som_throw.wav`（0.24秒）と `som_resonance.wav`（0.30秒）は外部音源を使わない合成音。
生成コードは `design/art/som/generate-resonance-sfx.py`。前者は下がる電撃音、後者は三つの倍音が重なる上昇音。
往路の放出と体表の共鳴で別枠から重ねて再生する。-11 dB、原音程、100ms以内の群れの同種音はまとめる。
粛で止まる分岐は既存の無効化音、打ち返しは既存の割り込み音と電撃鞭音、暴発は既存のソムの破裂音を再利用。
一時停止・終了・中断・再戦は既存の `PauseSomSounds`／`StopSomSounds` に従う。

## トウ・粉焼（2026-10-13）

ユーザー指定のHelton Yan's Pixel Combatの `DSGNImpt_EXPLOSION-Phoenix Spark_HY_PC-001/002/003.wav`。
原本から `tou_powder_spark_1/2/3.wav` へ無加工でコピー。全て96kHz / 24bit / stereo / 2.5秒。
火花の連続閃光と共通の段階で選ぶ：粉の濃さ1〜2は001（小）、3〜14は002（中）、15以上は003（大）。

| 原本の末尾 | 配置名 | SHA-256 |
|---|---|---|
| 001 | tou_powder_spark_1.wav | `93F249B4B648B19689806AF3106845BA28C03A7E9AB8FCFDB28B0A456C1C975C` |
| 002 | tou_powder_spark_2.wav | `F58F2030A679DF6B692076E4586EA978D94FFBC200C348725A4DF880CB972FD0` |
| 003 | tou_powder_spark_3.wav | `6714C1A672EAB63E23F63432FAD1C29B3F89B7ABAB53E10D486C76440BDC2295` |

先に指定されたSand Impact 001（`tou_powder_burn.wav`、SHA-256 `C2E466AD19D7B87D03A6BAC743A09A8F39D54AF45CF0F5159EAF972A6D11ECFA`）は差し替え済みで再生しない。原本・既存配置は保存する。

配置コマンド（購入音源はGitに含めない）：

```powershell
1..3 | ForEach-Object {
    Copy-Item -LiteralPath ("D:\Assets\SE\Helton Yan's Pixel Combat - Single Files\DSGNImpt_EXPLOSION-Phoenix Spark_HY_PC-00{0}.wav" -f $_) -Destination ("DemoApp/assets/audio/se/tou_powder_spark_{0}.wav" -f $_)
}
```

`ShockGauge` の「濃さ・焼いた」で、本人の点火と同時に再生。
小-6 / 中-4 / 大-2 dB、専用3枠、原音程。1倍速の上限は小0.45 / 中0.55 / 大0.65秒、倍速でも最低0.30秒。
末尾28%をフェードし、同じフレームまたは85ms以内の発動は最大段階の1音にまとめる。小が先なら同じ枠を大に差し替える。左右定位は採用した音の焼ける駒の画面位置。
通常の散粉・放電・濃さの加算では鳴らさない。一時停止は音とフェードを止め、再開時の速度変更にも追従。
終了・再戦・画面離脱では全枠を停止する。未配置の環境では無音で演出を続ける。
`res://ShockMarkCheck.tscn -- --powder` で3音源の復号、段階の境界、点火時の選択、密集時の大の優先、原音程、一時停止・再戦を確認する。
音量は当初の小-16 / 中-14 / 大-12 dBから各10 dB増加。倍速の最低尺も0.18秒から0.30秒へ延ばす。
`--powder --powder-mix` で小・中・大×左右×1・2・4倍速のMaster出力を測定し、ピーク約0.35〜0.62を確認する。

### 粉塵爆発の爆竹（2026-10-14）

ユーザー指定により `DSGNImpt_EXPLOSION-Bit Bomb_HY_PC-001.wav` へ差し替え。原本を `tou_dust_burst.wav` に無加工でコピー（96kHz / 24bit / stereo / 2.5秒）。SHA-256: `376F7A914533A9F8EEF7FEF1A5CF7EB326C43CDDD1E486E04FE408A3D0EE0717`。
`BattleAttackAudio.DustBurst.cs` がこの専用音源を短い破裂として再生する。通常の粉焼はPhoenix Spark 001〜003を引き続き使う。
音源は起動時に復号しておき、最初の爆発での読み込み待ちを避ける。

配置コマンド（購入音源はGitに含めない）：

```powershell
Copy-Item -LiteralPath "D:\Assets\SE\Helton Yan's Pixel Combat - Single Files\DSGNImpt_EXPLOSION-Bit Bomb_HY_PC-001.wav" -Destination "DemoApp/assets/audio/se/tou_dust_burst.wav"
```

粉の濃さの段階は粉焼と共通。小-5 / 中-4 / 大-3 dB、8枠、原音程。従来から6〜8dB上げ、連続発生でも余韻を他の破裂で早く切らない。
冒頭0.40秒（倍速時も最低0.30秒）、末尾28%をフェードする。35ms未満・同じフレームの発火は集約する。
専用の定位バスから爆発用ミックスバスを通ってMasterへ送る。粛のこもりフィルターで可視の爆発音を消さず、専用のHardLimiter（上限-3dB）で連続発生時の音割れを抑える。バスは離脱時に削除する。
再生時刻は0.60秒の連鎖の時計で予約し、後半ほど間隔を詰める。通常の大きな粉焼SEとの二重再生を避ける。
一時停止・途中の速度変更・離脱・再戦は音とフェードにも反映。原本・購入素材本体は変更しない。
`--dust-burst` で粉の印だけ・濃さ1・12・30×1・2・4倍速のMaster実出力、複数回の再生と単発のクリップなしを確認する。
初期の単発音は左右・中央、1・2・4倍速、粛あり／なしで余韻と実効音量を確認する。鞭・標のSEとの同時再生も確認し、`--dust-audio-dir=<絶対パス>` でMasterの単発・連続・同時再生をWAVへ保存できる。

### ゴルムのパンチ命中（2026-10-15）

ユーザー指定の `D:/Assets/SE/効果音ラボ/演出/カークラッシュ.mp3` を `golm_punch_hit.mp3` に無加工でコピー。
SHA-256: `2FADC90398FE925AB258F8BA7CAC2E96C257D3CF5BD7C42B245345E4ECFA5507`。
`GolmFx3D.Impact` がパンチの接触時に1回再生する。捕獲へ続くパンチも同じ命中音を使う。
命中専用の再生枠で帰還・捕獲の音との重なりを保ち、一時停止・速度変更・中断に追従する。踏み潰しは既存の合成音を使う。

### ガレの結晶弾幕・集束砲（2026-10-21）

ユーザー指定の2音を無加工でコピーし、`BattlefieldView3D.GareVolley` で重ねる。

| アプリ内ファイル | 原本 | SHA-256 |
|---|---|---|
| `gare_crystal_shine.mp3` | `D:/Assets/SE/効果音ラボ/演出/シャキーン3.mp3` | `DA1C9B93D1319E04F30E6FE5A59BC204292FEAD00E12FE7C2F36F68010B37419` |
| `gare_shotgun.mp3` | `D:/Assets/SE/効果音ラボ/戦闘/ショットガン発射.mp3` | `C3BD4D90A5298D172AB96C8409E50E567CEAD5330E4D41BEF50815716565139C` |

配置手順：

```powershell
Copy-Item -LiteralPath 'D:/Assets/SE/効果音ラボ/演出/シャキーン3.mp3' -Destination DemoApp/assets/audio/se/gare_crystal_shine.mp3
Copy-Item -LiteralPath 'D:/Assets/SE/効果音ラボ/戦闘/ショットガン発射.mp3' -Destination DemoApp/assets/audio/se/gare_shotgun.mp3
```

集団向け弾幕は発射と反動の開始0.32秒にショットガンを-12dBで再生し、0.03秒後にシャキーンを-8dBで重ねる。シャキーンは立ち上がりの弱い冒頭0.10秒を再生時に飛ばす。単体への集束砲は下記の専用音を加えた配分にする。
初回のシャキーン-6dB／ショットガン-17dBから、ユーザーの「銃から放つ感を強めたい」に合わせてショットガンを5dB上げ、シャキーンを2dB下げた。発射直後の銃声が先に立ち、その後に結晶の響きが広がる。
汎用の `charge_magic_release` を置き換える。着弾の `armor_break` は当初維持していたが、現在は下記の専用の着弾・飛散音へ差し替えた。
演出末尾0.22秒を共通時計で滑らかにフェードし、1.40秒の終了で切れ目が目立たないようにする。原音程、一時停止・中断・再戦の後片付けは既存の演出所有プレイヤーに従う。原本は変更しない。

### ガレの集束砲専用の溜め・発射（2026-10-21）

単体への集束砲だけ、ユーザー指定の以下2音を追加。無加工コピーでSHA-256一致を確認した。

| アプリ内ファイル | 原本 | SHA-256 |
|---|---|---|
| `gare_focus_charge.wav` | `D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNSynth_CAST-Mecha Energy Gathering_HY_PC-001.wav` | `4783F909550F00E9611F5D3D4F5C06A6E1148AE9AAFD1EF7E88C96AC72509F1F` |
| `gare_focus_beam.mp3` | `D:/Assets/SE/効果音ラボ/戦闘/ビーム砲1.mp3` | `BE624697D0E421F9366640621EFF9AF12E1365F62496ED00671321875F50BEA3` |

```powershell
Copy-Item -LiteralPath "D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNSynth_CAST-Mecha Energy Gathering_HY_PC-001.wav" -Destination DemoApp/assets/audio/se/gare_focus_charge.wav
Copy-Item -LiteralPath 'D:/Assets/SE/効果音ラボ/戦闘/ビーム砲1.mp3' -Destination DemoApp/assets/audio/se/gare_focus_beam.mp3
```

溜めは0.04〜1.02秒、-2dB、原音程。素材先頭0.08秒を飛ばし、0.82秒かけて立ち上げ、発射前の0.04秒で落として発射時に停止する。以前の0.28秒の抜粋から0.70秒延ばし、光と音の高まりを作った。元の3.75秒を全尺再生しない。
1.02秒にビーム砲-6dBとショットガン-11dBを重ねる。ユーザー指定により単体への集束砲ではシャキーンを再生しない。ビーム砲を中心に銃の衝撃を添える。命中1.40秒、演出終了2.10秒、末尾0.22秒をフェードする。集団向け弾幕の配分・発射と命中の時刻は維持する。
購入素材の `gare_focus_charge.wav` と `.import` はGitの対象外。再配置は上記コマンドを使う。原本・共有音源は変更しない。

### ガレの発散・集束の命中と結晶の飛散（2026-10-22）

ユーザー指定の3音を無加工コピーで採用。原本とのSHA-256一致を確認した。

| アプリ内ファイル | 原本（`D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/`） | SHA-256 |
|---|---|---|
| `gare_hit_scatter.wav` | `DSGNMisc_HIT-Mecha Laser Pistol_HY_PC-003.wav` | `4A26AA7D74BF545360CFE45C98CEA1DF70D2BE4BCBCF2AD515F40D51C9D545C7` |
| `gare_hit_focus.wav` | `DSGNMisc_HIT-Mecha Laser Pistol_HY_PC-006.wav` | `76C31C89F84E3B5A763898484937621F95FC97FE5003E725AE3C83D8B1D23F31` |
| `gare_crystal_scatter.wav` | `DSGNMisc_SKILL IMPACT-Glassy Sprites_HY_PC-005.wav` | `D8DAF9C1C7DC89D633A07086036BE8E8F5900FA030E442B462F5E0CDA23D25BF` |

```powershell
Copy-Item -LiteralPath "D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNMisc_HIT-Mecha Laser Pistol_HY_PC-003.wav" -Destination DemoApp/assets/audio/se/gare_hit_scatter.wav
Copy-Item -LiteralPath "D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNMisc_HIT-Mecha Laser Pistol_HY_PC-006.wav" -Destination DemoApp/assets/audio/se/gare_hit_focus.wav
Copy-Item -LiteralPath "D:/Assets/SE/Helton Yan's Pixel Combat - Single Files/DSGNMisc_SKILL IMPACT-Glassy Sprites_HY_PC-005.wav" -Destination DemoApp/assets/audio/se/gare_crystal_scatter.wav
```

発散は命中0.70秒に003を-10dB、集束は命中1.40秒に006を-7dBで鳴らす。それぞれ0.07秒後に005を発散-8dB／集束-6dBで重ねる。敵の人数分は鳴らさず、一斉着弾の芯と飛散を各1回にまとめる。原音程、命中音は演出末尾0.16秒、飛散音は末尾0.22秒でフェードする。
購入素材3音と各 `.import` はGit対象外。上記手順で配置する。原本は変更せず、一時停止・速度変更・中断・再戦は既存の演出時計と後片付けへ従う。

## エレの演奏開始（2026-10-29）

ユーザーが試用を依頼した400 Sounds Packのチェンバロを、無加工でコピー。

| アプリ内ファイル | 元ファイル | 使用箇所／音量 |
|---|---|---|
| ere_harpsichord.wav | D:/Assets/SE/400 Sounds Pack/Musical Effects/harpsichord_mystery.wav | 演奏Cの開始0.02秒後に一度／-6 dB |

原音は約1.497秒、44.1kHzの16bit PCM。原音のピークが約-11.9 dBFSと小さいため、再生音量は-6 dBで試す。
ピッチは1、ループなし。最初の0.02秒をフェードインし、演出の中断や終了では既存の音符と同じ時計で停止する。
倍速でもピッチは変えず、演出終了に近づく最後の0.18秒でフェードする。通常速度では原音の余韻をそのまま再生する。
受け渡しの `fire_gift.mp3` と追加手番の `fire_gift_turn.mp3` は継続。
購入音源本体とインポート情報はGitから除外する。別環境では上表の元ファイルを同名で配置し、Godotでインポートする。
未配置なら従来どおり、開始の光と音符だけで再生できる。

## エレの演奏音の比較（2026-10-29）

ユーザー提供のSuno生成音源2曲を、同じ演奏Cの3秒へ切り出して比較する。原本は変更しない。

| 比較／アプリ内ファイル | 元ファイル | 使用区間 |
|---|---|---|
| A / ere_song_ah_a.wav | C:/Users/Pon/Downloads/Ah.mp3 | 0.08〜3.08秒 |
| B / ere_song_ah_b.wav | C:/Users/Pon/Downloads/Ah (1).mp3 | 1.10〜4.10秒 |

`design/art/ere/song-audio-v1/prepare.py` で再作成。48kHz／16bit PCM／ステレオ。
Bの冒頭の静かな部分を詰め、両曲ともRMS約-18 dBFSへそろえる。ピッチとテンポは変えない。
切断点の10msフェードに加え、ゲーム内で-8 dB、開始20msのフェードイン、終端240msのフェードアウト。
既存の受け渡し／追加手番の音も継続する。
音源とimportファイルはGit除外。切り出し位置と原本SHA256は同フォルダーの `clips.json` に記録。
`EreGiftCheck -- --ere-audio=a` または `b` で検証・比較録画する。この最初の比較時点では本番はチェンバロで、後述の時計まで続けるA/Bを交互再生として採用した。
プレビューは `design/art/ere/song-audio-v1/ere-ah-a.mp4` と `ere-ah-b.mp4`。

### 時計の解放まで続ける比較版

同日の追加要望で3秒で切るのをやめ、原曲の末尾まで保持した別音源を作成。
`prepare.py --through-clock` で `ere_song_clock_a.wav`（0.08秒以降／6.6935秒）と
`ere_song_clock_b.wav`（1.10秒以降／5.66秒）を配置する。原本と3秒版は維持。
RMS約-18 dBFS、ゲーム内-8 dB、ピッチ・テンポはそのまま、ループなし。
演奏Cの光とは別の `GatherCast3D` で音を管理し、最初の追加手番の時計が消える時刻まで継続。
終端0.35秒でフェードする。原曲の終わりが先ならそこで終了し、引き延ばさない。
停止・倍速・中断・死亡・再戦は共通の時計と終了処理へ従う。
比較指定は `--ere-audio=clock-a` / `clock-b`。最終採用したこの2音とimport情報はGit管理する。
記録と再作成手順は `design/art/ere/song-audio-v1/clock-notes.md`。

### A/B交互再生として採用

ユーザーが両曲を選択。通常の演奏発動ごとに `ere_song_clock_a.wav` → `ere_song_clock_b.wav` を繰り返す。
順序は戦闘ごとに管理し、受け渡し先が増えても曲を選び直さず、再戦ではAへ戻す。
既定の音量・時計の解放までの寿命・終端0.35秒のフェードは上記と同じ。
片方が未配置なら配置済みの方を使い、両方ない環境では以前のチェンバロへ戻る。
明示的な比較指定では交互再生の順序を消費しない。無指定の `EreGiftCheck -- --movie` でA→Bを録画できる。
確認動画は `design/art/ere/song-audio-v1/ere-alternate.mp4`。
採用したユーザー生成音源A/Bはアプリ用WAVとimport設定をコミットし、取得後の再生にDownloads内の原本を必要としない。購入音源と不採用の3秒比較音源はGit除外を継続する。
