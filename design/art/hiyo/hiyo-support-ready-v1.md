# ヒヨ：支援役の戦闘待機姿勢 v1

作成: 2026-10-11。Codex組み込みImageGenで、現在の `hiyo_ready_right.png` を参照して生成した透過PNGの試作。
保存先: `design/art/hiyo/hiyo-support-ready-right-v1.png`。
採用先: `DemoApp/assets/portraits/battle/hiyo_ready_right.png`（同一PNG）。

元のニュートラル立ち絵は棒立ちに近く、待機モーションを付けにくそうというユーザー評価を受けた案。
両肘を曲げ、杖を身体の前で両手で支え、足を開いて膝に余裕を持たせる。落ち着いて右側を見ている支援役の戦闘準備姿勢を狙う。
呼吸に連動した両肩・両腕・杖の揺れと、脚の小さな体重移動へつながる元絵として検討する。

生成した立ち絵だけでは、i2vの動き・顔や装備の保持・ループの品質は判断できない。i2v試作の比較後、ユーザー指示でこの通常立ち絵だけを採用した。
以前の部分変形v2の接続をゲームから外し、i2vのモーションも未採用。v2ソースは `idle-rig-v2/source/` へ試作用に退避した。
既存の動作差分は維持。表示高1.89、足元余白0.0215（透明域約33px / 1536px）で接地を合わせる。
モーションは、この採用画像を元にボルグ・ホタで使ったClaude Code / ComfyUIの方法・フローと比較する予定。

検証: DemoAppビルド成功（警告・エラー0）。`FireCheck.tscn --replay` の2回の再生が `FIRE_CHECK_OK`。
実ゲーム画面で通常スプライトの画素と採用PNGの一致、試作リグ・待機シェーダーが接続されていないことを確認し `HIYO_SUPPORT_STATIC_OK`。

## 最終プロンプト

```text
Use case: identity-preserve.
Asset type: transparent full-body anime fantasy RPG battle-ready idle sprite, ONE pose, proposed new base for idle animation.
Input image 1 is the edit target and authoritative reference for Hiyo's identity, costume, weapon and drawing style. Change ONLY the stance, arm positions and naturally resulting cloth/hair placement. Preserve her recognizable face, amber eyes, long pointed elf ears, blonde ponytail and red bow, slender proportions, red cape with gold edging and diamond decorations, short white blouse, brown layered belts and pouches, red split overskirt, white patterned layered skirt, thigh strap, gloves and brown boots with red bows. Preserve the SAME long straight brown staff, gold flame-shaped finial surrounding a faceted red gemstone, red bow under the finial, and the small gold pointed end. Do not redesign the staff or clothing.

Primary request: turn her rigid casual standing pose into the calm, lightly engaged READY STANCE of a staff-using SUPPORT MAGE who watches her allies and is prepared to help. This is a restful battle idle key pose. She should look poised and attentive, kind and quietly confident.

POSE: full body and head still in three-quarter view facing SCREEN RIGHT, gaze clearly to the right rather than toward the viewer. Closed mouth, small reassuring smile. Feet comfortably separated in a modest staggered stance: the right-facing lead boot slightly ahead, rear boot a little behind, both soles on the same ground plane. Knees gently flexed, hips carrying balanced weight slightly toward the rear leg, torso upright with a subtle forward readiness, relaxed shoulders. Give her a clear soft bend in elbows and knees, with room for natural breathing and a small weight shift. No perfectly parallel locked legs.
She holds her staff with BOTH hands lightly in front of and just beside the torso, at two separate grip points near waist and lower chest; elbows soft and naturally bent, forearms anatomically sound. The staff is carried at a mild diagonal toward screen right, its flame finial beside and slightly above her right shoulder, visibly separated from her face, NOT overhead. Its lower pointed end stays near the rear ankle but hovers slightly above the ground, so arms, staff and body can move together during idle. It must be ONE uninterrupted rigid straight shaft with believable hand grips and original full length. Her two hands support its weight, a gentle supportive caster guard rather than a spear fighter. Keep her face, gold chest clasp and overall body silhouette readable. Avoid dense overlapping hands, hair and staff head.
Ponytail, ribbons, cape and skirt hang mostly by gravity with modest loose curves, appropriate to a resting pose; no storm or dramatic flutter.

Style and framing: faithfully match the reference's clean fine linework, polished soft anime shading, red/gold/brown palette and level of detail. Portrait 1024x1536, one character centered, full hair, weapon and both boots visible with safe margins. Same head-to-body scale as the reference. Actual transparent alpha background including internal gaps; fully opaque character interior. No black/brown gradient background even if hidden RGB is present in the reference. No floor drawing, ground shadow, scene, text, watermark or new props.
Avoid: raised-staff celebration, active spellcasting, magical effects, fire, aura, glow, staff striking/thrusting, deep combat crouch, running, walking, crossed legs, huge wide stance, angry or shouting face, looking at camera, weapon or costume redesign, extra hands/fingers/limbs, duplicate staff, cropped extremities.
```
