# 墓守リィカ 戦闘待機立ち絵リメイク v3

生成日: 2026-10-24
生成方法: Codex 組み込み ImageGen（CLI 不使用）
出力: `rica-battle-idle-right-remake-v3.png`（1024×1536、透過PNG）
状態: 確認用。ゲーム未適用。
出発点: `rica-battle-idle-right-remake-v2.png`

ユーザー指摘: 左手（画面右）の指と持ち方がおかしい。膝をもう少し曲げて棒立ちを解消。
左手を掌側が見える下手の握りに変え、親指と四指が柄を挟む形を明示した。両膝を曲げて腰を落とし、腿・膝・脛の角度と衣装のドレープを描き直した。頭身・キャラクター・衣装と一直線のシャベルは踏襲。初回修正では握りと膝の変化が弱く、追加の修正生成を行った。

## 初回修正プロンプト

```text
Use case: precise anatomy and battle stance correction.
EDIT TARGET: supplied Rica battle v2. Produce ONE revised full-body right-facing combat idle portrait, v3. Preserve SAME identity, corrected adult head-to-body proportions, exact costume, face, hair, palette, rendering and STRAIGHT shovel geometry. Change only the two issues below plus the natural cloth/arm adjustments required by them.

CORRECTION A — LEFT HAND AT SCREEN RIGHT:
Redraw the forward LEFT hand, wrist and forearm anatomically from scratch. It must convincingly HOLD the cylindrical shovel shaft, rather than paste fingers onto its front. Use a clear natural UNDERHAND supporting grip: left palm cradles shaft from underneath, the four fingers wrap around the shaft toward its far/top side; a single correctly positioned opposable thumb closes around the opposite side across the shaft. Show a clearly identifiable thumb near index finger and four fingers with sensible knuckle progression. Exactly FIVE digits, each connected to the same hand, thumb shorter and separate from the four fingers. Some far-side fingertips may be naturally occluded by the cylinder; do not line up all fingertips flat in one plane. The wooden shaft occludes parts of the wrapped fingers and is occluded by the near fingers/thumb in physically consistent order. Keep a continuous straight shaft through this grip. Bend LEFT elbow comfortably about 100-120 degrees, bring hand a little closer to waist so forearm actively supports the tool. Wrist mostly straight in line with forearm, natural left-handed anatomy. Brown fingerless work glove with visible bare fingertips and thumb, no finger drawn emerging through solid glove. Make the grip easy to read at gameplay scale. Rear RIGHT hand on screen left retains a credible overhand grip near D handle. Exactly two arms/two hands total. No loose fingertips, duplicated thumb, fused fingers, extra knuckles or detached palm.

CORRECTION B — KNEES/WEIGHT:
The target stance looks upright and stiff with straight knees. Make a VISIBLY bent-knee, grounded ready stance, moderately lower her hips and center of gravity, BOTH knees noticeably flexed around 25-35 degrees. Front leg on screen right bends forward over foot, rear leg on screen left also flexes to bear weight. Heels stay planted. Torso inclines subtly forward from hips with relaxed back. Elbows softly bent, shoulders calm. Clear spring-loaded readiness to move or swing shovel, NOT standing erect, not wide split and not a deep squat. Preserve long adult leg proportions; joint flexion lowers body naturally, do not shorten legs or enlarge head. Feet in comfortable staggered combat stance facing SCREEN RIGHT, not viewer. Lower legs must clearly change angle at knee beneath the drape rather than form two straight sticks. Dress and apron fold naturally over bent thighs/knees and between legs, retaining SAME long hems and modest full leg coverage. No exposed thighs or shortened skirt.

PRESERVE:
Rica the young adult slender female elf, pale skin, ice-blue eyes, long pointed ears, white/silver side-swept hair and ONE thick braid over her LEFT shoulder, quiet pensive focused expression looking toward enemy offscreen RIGHT. Same small adult head and slender 7-7.5-head underlying skeleton as supplied, refined anime face. Hood down charcoal/navy long cloak, metal throat fastening; dark teal high-neck long-sleeved calf-length work dress; tan/taupe long canvas apron with reinforced pocket, sparse cross repairs and earth marks near hem; brown belt/rectangular iron buckle, belt tail, brown flap pouch, cemetery keys and secured cord loop; brown fingerless gloves; black opaque stockings and sturdy folded-cuff brown work boots.
Shovel stays in two-hand low diagonal guard, D handle to upper-left and blade lower-right at shin/knee height. Rigid straight wooden shaft, D-shaped iron grip with wooden crossbar, metal socket, central blade rib and pointed blade tip on a SINGLE straight unbroken axis. Symmetric flat digging spade, central rib and boot shoulders, no bend, twist, curved tip or curved shaft. Both grips must accommodate this line rather than warp the tool.
Match supplied clean anime art with fine linework, controlled cel shadows and restrained soft face/hair highlights, no photorealism or noisy painterly finish. Restrained cloak drape, not billowing huge.
1024x1536 vertical, entire figure and whole shovel inside generous clear safe margins, both boots fully visible on same ground plane. Single figure only. Genuine transparent RGBA and gaps, fully opaque character. No backdrop, glow, vignette, ground plane or cast shadow, checkerboard, text, label or watermark.
```

## 最終修正プロンプト

```text
Redraw this same Rica elf gravekeeper in a noticeably LOWER, genuinely BENT-KNEE battle-ready guard facing screen right. This is a new anatomy redraw, do not merely trace the current straight legs and problematic hand.

Two essential changes:
LEFT HAND (the forward hand at screen right): ROTATE the hand to a clear PALM-UP SUPPORTING GRIP, a different orientation from the current back-of-hand-facing-viewer fist. Left elbow flexed and tucked nearer waist, left forearm comes forward, palm facing up under the wooden shovel shaft. The shaft rests diagonally across the palm. Show the glove's PALM side and a SINGLE distinct bare THUMB hooked OVER the top of shaft, opposing four curled fingers UNDER the shaft. Thumb is visibly separated from index finger and crosses the shaft from the other side. Four naturally staggered fingers wrap around the underside and far side, some fingers appropriately occluded by shaft. Exactly one thumb and four fingers, no row of four fingertips pasted onto the front. Correct left-handed anatomy and straight relaxed wrist, glove has coherent open fingertips. Give this hand a simple unambiguous readable grip even if it requires moving it slightly along the handle. Rear hand stays overhand, so the hands visibly use opposite palm orientations.

KNEES: lower the pelvis and bend BOTH knees substantially into a medium half-crouched guard, approximately 40-45 degrees knee flexion. The front knee on SCREEN RIGHT must visibly project forward TO THE RIGHT of its ankle; the front shin slopes BACK toward screen left as it goes down from knee to ankle. Clearly angular knee-to-shin silhouette, not a straight diagonal thigh-to-ankle. Rear leg also flexes with weight shared; both boot soles stay grounded. Comfortable staggered feet, torso leans a little forward over hips, center of gravity low. Show some knee articulation in stocking silhouette just below naturally draping dress hem. Long dress and apron fold over bent legs and hang under gravity; do not keep a perfectly vertical column of fabric concealing all the bending. No miniskirt, exposed skin, kneeling or acrobatic lunge. Do NOT shorten actual limbs or increase head size to make crouch. Legs keep adult anatomical lengths, joints are BENT.

Preserve this exact character's slender adult proportions, same small head, white silver hair with one thick side braid, long elf ears, ice-blue eyes, calm pensive face looking right. Same charcoal hood-down cloak, dark teal long-sleeve work dress, tan long repaired canvas apron and flat utility pocket, brown belt, rectangular iron buckle, old cemetery keys, cord loop, brown leather pouch, fingerless work gloves, black opaque stockings, brown sturdy cuffed work boots. Same clean fine-line anime rendering and cel shading.
The shovel is held in two-handed low diagonal guard, D handle upper-left and spade lower-right; ONE perfectly straight wooden shaft and straight metal socket and flat symmetric pointed blade, all on one line, no curved blade or shaft. Keep all equipment and clothing details.
Full-body, full shovel, vertical 1024x1536, comfortable clear margins, boots on same ground plane. Only one figure. TRUE TRANSPARENT RGBA background and empty gaps, no glow, vignette, backdrop, floor, shadow, lettering, logo or watermark.
```
