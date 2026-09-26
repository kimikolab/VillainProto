# アカの赤い魔法陣・ルビー槍

- チャージ: 二重の赤い魔法陣と脈打つ赤い光を追加。ChargeAura3Dの寿命に従い、放出・死亡・キャンセルで解除する。
- 放出: 敵1体につき7本の多面体ルビー槍を収束させ、赤い閃光と12片の結晶へ炸裂。敵全体の対象は従来どおり台本から取得。
- 血滴の集積は燃料を示す前段として維持。放出の血の雨を結晶へ置き換えた。
- 立ち絵・数値・戦闘ロジック・カタ関連は変更しない。

## 指定SE

- `D:/Assets/SE/効果音ラボ/戦闘/魔法陣を展開.mp3` → `DemoApp/assets/audio/se/aka_circle.mp3`
- `D:/Assets/SE/効果音ラボ/戦闘/氷魔法2.mp3` → `DemoApp/assets/audio/se/aka_ruby_burst.mp3`

既存のチャージ開始・放出の専用SE経路に追加。開始済みのチャージでは再発音しない。再生速度によってピッチは変えない。コピー前後のSHA256一致を確認。

## 検証

DemoAppビルド成功（最終ビルド警告0・エラー0）。第一波seed 7・4倍速で赤い魔法陣の持続、集積2体・放出3体、通常復帰・死亡・蘇生・勝利を検証し、AKA_BLOOD_CHECK_OK / SUSU_PORTRAIT_CHECK_OK。
Dummy音声ドライバで指定MP3の読み込み・再生経路を確認（聴感の音量バランスは未確認）。実画面はaction-charging-game-check.pngとaction-blood-rain-game-check.png。終了時の既存RID等の警告は残る。
