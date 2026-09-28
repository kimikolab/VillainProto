# ハネ・弾き返しの双掌打（2026-09-29）

ユーザーがヒップアタックを取りやめたため、弾き返し（Spring）を双掌打に変更。通常立ち絵から新規生成し、前膝を深く曲げ、後ろ脚から両掌へ体重を伝える低い踏み込みを描く。手番のドロップキックは維持。

- 採用差分: `DemoApp/assets/portraits/battle/hane_palm_idle_right.png`
- 参照: `DemoApp/assets/portraits/battle/hane_idle_right.png`
- 内蔵image_gen、透過PNG。1536×1024、下端余白15px（15/1024）。表示高は待機絵の1024/1536。
- 両掌の平均接点は（1410,425）。中心から右642px・上87px。掌の高さはハネの接地した構えから取り、相手の身長で足を浮かせない。
- 0.08秒の溜め、0.14秒の踏み込み、0.08秒の接触静止、0.20秒の復帰。跳躍の弧は使わない。
- 接触に「パンチを受け止める」と星形の衝撃。実際の接触完了を待って台本のMoveを再生する。攻撃者だけを押し返して転倒させる既存の仕様を使い、対象・ダメージ・席を表示側で決めない。
- 死亡、対象死亡、差分上書き、停止、再開、退場で古い接触・音を残さない。

## 確認

検証: DemoAppビルド成功（警告・エラー0）。DropkickCheckで左右×1/2倍速の両掌接触・接地・音・後続Move・復帰・中断を確認（`.tmp/hane-palm/` に撮影）。MovementPortraitCheck成功。MovementCheckの実戦リプレイ2回成功（各267イベント、Spring 2回を含む）。Godotの既存環境警告（userログ・shader cache・証明書ストア・終了時Font RID）は残るが、各検査は終了コード0。

## 生成プロンプト

Create a NEW full-body transparent battle sprite of Hane, the same adult rabbit-eared woman in the supplied character reference. Keep her identity, costume, colors and detailed hand-painted fantasy anime style. Reference is for character design ONLY, do not repeat its upright standing pose.

Action: a forceful martial-arts DOUBLE PALM THRUST aimed to SCREEN RIGHT, at the instant both palm heels slam forward to knock an enemy away. A very LOW, DEEP FORWARD LUNGE. Her whole body forms a powerful diagonal from rear boot at lower LEFT through hips and shoulders toward both hands at upper RIGHT. Her FRONT KNEE at screen RIGHT is deeply bent; REAR LEG stretches back LEFT and pushes hard from the planted boot. Both feet grounded in a wide stable stance. Hips and head lowered, torso leaning forward around 40 degrees from vertical, clear explosive transfer of body weight. This must look substantially more active, lower and more extended than the original standing pose.

Both arms thrust forcefully RIGHT from the shoulders, nearly straight but elbows naturally unlocked, open palm heels leading at chest height. Stagger the two hands slightly vertically so both hands read separately. Fingers gently spread and naturally bent, wrists aligned, five fingers per hand, no extra arms. Head stays behind the hands, eyes fixed RIGHT, fierce focused expression. Use a near side view with slight front three-quarter angle; no extreme foreshortening, no twisted torso. Anatomically coherent shoulders, elbows, wrists, hips, knees and ankles. Entire figure visible, including both boots and both rabbit ears.

Preserve silver-gray ponytail, white rabbit ears with pink inner fur, amber eyes, rust-red cropped jacket with white fur trim, white shirt, opaque brown leather shorts, belts and pouch, leather bracers, heavy fur-trimmed brown boots and long red waist sash. Hair and cloth whip LEFT behind the drive. Normal athletic proportions, original outfit and natural shading. Wide landscape canvas with clear transparent margins. Genuine transparent alpha background, all corners alpha zero. Single character, no opponent, no impact glow, motion trails, flowers, ground, shadow, gradient, scenery, text or watermark.
