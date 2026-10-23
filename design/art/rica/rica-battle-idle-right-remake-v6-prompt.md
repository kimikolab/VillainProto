# 墓守リィカ 戦闘待機立ち絵リメイク v6

生成日: 2026-10-24
生成方法: Codex 組み込み ImageGen（CLI 不使用）
出力: `rica-battle-idle-right-remake-v6.png`（1024×1536、透過PNG）
状態: 2026-10-24 採用済み。`DemoApp/assets/portraits/battle/rica_idle_right.png` に適用。
編集対象: `rica-battle-idle-right-remake-v5.png`
髪型の正: `rica-standing-remake-v3-shovel-lean.png`

ユーザー指摘: 結った髪が画面左側から出ている。通常立ち絵では画面右側。
v5の画面左の三つ編みを取り除き、通常立ち絵と同じ画面右の肩・胸へ垂れる一本の三つ編みに修正。構え・手の握り・頭身・シャベル・衣装を維持し、全体の左右反転は行わない。

## 最終プロンプト

```text
Use case: precise localized hairstyle correction ONLY.
IMAGE 1 is the battle portrait edit target. IMAGE 2 is the authoritative normal portrait hairstyle reference.
The battle portrait mistakenly puts the SINGLE thick braid on SCREEN LEFT. Move it to SCREEN RIGHT to match the normal portrait.

HAIR EDIT:
Remove the entire existing braid that currently descends from the ear at SCREEN LEFT across left/center chest, including its tie and loose tail. Reconstruct that side with the same short smooth silver hair around the nape, original dark cloak and tan apron beneath. No braid on SCREEN LEFT.
Draw exactly ONE thick silver-white braid emerging from the side of head on SCREEN RIGHT, behind the screen-right side of face, draping forward over the shoulder on SCREEN RIGHT. It should hang down along the RIGHT side of her neck and outer chest, to the RIGHT of the centerline of her torso, like the normal portrait's braid. Same braid thickness, woven structure, small muted gold tie and white tail as image 2, reaching upper-waist/chest region. It must clearly originate from the opposite side to current braid; do not merely move the braid tip or duplicate it. Maintain natural strand continuity from scalp around the ear/neck to shoulder. Her face is still looking right, so place braid just behind/right of cheek, not over eyes, mouth or in front of the face.
Use SCREEN COORDINATES, not anatomical left/right, for this correction: the resulting single braid is on the VIEWER'S RIGHT side of head and chest. No second braid anywhere.

PRESERVE EVERYTHING ELSE EXACTLY FROM IMAGE 1: same face, facial expression, eye direction, head/body proportions, screen-right-facing battle pose, shallow relaxed knees, legs, feet, both arms, both hand grips and finger anatomy, rigid straight shovel and angle, D handle, blade geometry, costume, apron bib/pocket/stitches, belt, keys, cord, cloak, gloves and boots. Do not mirror/flip the entire image. Do not swap hands, shovel ends, body direction or accessories. Do not alter the pose or enlarge head. Same precise anime linework, colors and cel shading.
One full-body character with whole shovel, original 1024x1536 framing and safe margins, genuine transparent RGBA background and gaps. No background, halo, vignette, floor, cast shadow, text or watermark. This is a HAIR-SIDE correction only.
```
