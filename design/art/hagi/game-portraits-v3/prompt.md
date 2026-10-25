# ハギ 正面立ち絵 v3 — 確認済みの横向きの握りを反映

生成日: 2026-10-25
生成方法: Codex 組み込み ImageGen
状態: 正面は確認待ち。ゲームには未適用。

## 現在の確認対象

- 正面: hagi-standing-v3.png
- 戦闘: ../game-portraits-v2/hagi-battle-idle-right-v4.png（ユーザーが握れていると確認した画像。変更していない）

ユーザーは最後の横向きの握りと現行のスカート丈を良いと確認。以前の正面や旧版の握りは使わず、正面も肘を曲げて手を腰寄りへ移す形に変更した。スカート丈・鐘なしの武器を保持。

## 参照

1. ../game-portraits-v2/hagi-standing-v2.png（正面のデザイン・スカート丈・構図）
2. ../game-portraits-v2/hagi-battle-idle-right-v4.png（ユーザー確認済みの握り・腕の形）

## プロンプト

```text
Use case: precise-object-edit.
IMAGE 1 is the FRONT-FACING NORMAL PORTRAIT to correct.
IMAGE 2 is the APPROVED RIGHT-FACING BATTLE PORTRAIT. The USER has explicitly confirmed that image 2's lower hand REALLY GRIPS the shaft. Transfer that approved HAND + BENT LOWER ARM construction to image 1. This reference is authoritative for the grip; do not invent another extended-arm fingertip hold.

MAKE THIS SINGLE CHANGE TO IMAGE 1:
Completely REMOVE its current extended-down right forearm and low lower hand on VIEWER LEFT. REPOSE her anatomical RIGHT arm to match the bent elbow and secure waist-level hold in IMAGE 2:
- Right elbow bends outward at screen-left, near her lower ribs.
- Forearm runs from screen-left toward screen-right at waist height.
- Right hand moves UP THE SAME STRAIGHT SHAFT to beside/just above the left edge of her waist belt. Approximately one hand-length higher than the old thigh-level hold, not down by thigh.
- Copy image 2's exact actual wrap of the gloved hand and bare fingertips around the pole, relative wrist rotation, finger curling, opposing thumb, contact and occlusion. Match it at natural same hand scale and angle around the pole. Shaft is seated inside grip, not alongside fingertips. Entire shoulder-elbow-wrist chain coherent. The old hand silhouette must disappear.
- Her two hands stay separated comfortably. Upper hand remains as in image 1.
- Keep the pole in front, on ONE absolutely STRAIGHT axis from bottom-left pommel through new lower grasp and upper grasp to blade mount. Do not bend the pole to fit the hand; adjust arm instead.

CRITICAL: Keep image 1's FRONT THREE-QUARTER normal portrait, head and eye direction toward the viewer, self-assured small smile, relaxed hip/leg pose. Do not turn the woman sideways or copy image 2's battle stance. Copy ONLY image 2's approved bent right arm and lower grip.
PRESERVE THE ALREADY APPROVED SKIRT LENGTH EXACTLY from image 1, opaque overlapping short skirt with no visible underwear. Preserve NO bells, NO weapon hanging chains. Same black bob and tiny braid, face, body, crop bodice, clothing, one stocking and bare opposite leg, boots, antique-gold integrated scythe design, blade tally marks, waist key and earrings, flowing rear cloth, colors and art style. Do not restore old shorter skirt or bells. Keep whole woman and weapon inside frame.
Deliver ONE full-body vertical 1024x1536 genuinely TRANSPARENT RGBA normal portrait, including transparent gaps. No background, glow, floor shadow, vignette, labels, text, border or watermark.
```
