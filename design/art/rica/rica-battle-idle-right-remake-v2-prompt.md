# 墓守リィカ 戦闘待機立ち絵リメイク v2

生成日: 2026-10-24
生成方法: Codex 組み込み ImageGen（CLI 不使用）
出力: `rica-battle-idle-right-remake-v2.png`（1024×1536、透過PNG）
状態: 確認用。ゲーム未適用。
修正対象: `rica-battle-idle-right-remake-v1.png`
体格・デザインの正: `rica-standing-remake-v3-shovel-lean.png`

ユーザー指摘: 戦闘版の頭身が通常立ち絵と違って見え、シャベルが反って見える。
修正方針: 頭を相対的に小さくし、胴と四肢の比率を通常版に寄せる。シャベルは柄・金属ソケット・刃の中心線が一直線に見えるようにし、幅と曲面の表現を抑える。右向きの両手構えと衣装を維持。

## 最終プロンプト

```text
Use case: identity-preserve targeted proportion and equipment geometry correction.
Asset: ONE full-body right-facing BATTLE IDLE portrait of Rica, transparent PNG, revision v2.
INPUT 1 is the battle v1 EDIT TARGET. INPUT 2 is the approved normal standing v3, authoritative for SAME body build, head-to-body proportions, face, costume and straight spade.
Make two clearly visible corrections, retaining the established character and right-facing low-guard concept:
1. BODY PROPORTIONS: battle v1 has too large a head and compressed torso compared with normal standing v3. Redraw the battle figure with the SAME slender adult elf anatomy as input 2. Reduce battle v1's head including hair mass by approximately 12-15% relative to the shoulders and rest of body. Lengthen the neck-to-belt torso moderately, restore natural upper arm/forearm lengths and longer adult legs beneath the dress. A relaxed less-wide stance with only slight knee flexion prevents compressed, squat appearance. Aim for the same 7-to-7.5-heads-tall underlying standing skeleton as the normal portrait, about 6.5-7 heads apparent in this slight combat lean. Do not make a giant head, broad childlike shoulders, stubby upper body or big doll eyes. Do not achieve the change by simply shrinking the entire existing figure or stretching only boots/legs. It must look like the same slim woman changing her posture between the two portraits. Preserve recognizable blue-eyed quiet face, silver hair and thick single braid.
2. STRAIGHT SHOVEL: the battle spade currently looks bent/swept at its blade and socket. Replace with a physically straight simple gravedigger's spade. All of the following share ONE exact straight unbroken diagonal centerline: center of rear D grip, center of straight wooden shaft, straight cylindrical metal socket, straight central strengthening rib of spade, and the blade's pointed tip. Blade is a flat rigid symmetrical pointed plate with parallel upper side edges, two equal boot-pressure shoulders and a symmetric lower taper to its tip; no cupped spoon, curved banana profile, swept tip, twisted socket, hooked cutting edge or exaggerated concave side. Show the blade in a readable nearly front-facing plane so its flatness and symmetry can be seen. Small perspective rotation is fine but absolutely no bend between shaft and blade and no visible twist. Modest controlled metallic highlights must not falsely imply a curved blade. The wooden shaft is a rigid straight cylinder with the same line angle on both sides of both hands. Keep realistic practical dimensions, blade a little narrower and less bulbous than battle v1. A few restrained stylized earth marks only.
POSE TO PRESERVE/REFINE: head, body and both feet face SCREEN RIGHT, gaze at enemy to right, three-quarter toward right profile. Standing combat-ready wait, not attack. Calm slightly melancholy expression. Both boots on same ground plane, modest staggered feet with lightly bent knees, no extreme spread. Two hands hold shovel at separated points in low diagonal guard: rear D handle toward upper-left, blade toward lower-right beside front shin. Exactly two anatomically correct hands and arms, fingers wrap naturally, no fused fingers. Keep full continuous shovel visible and comfortably contained. No chin-on-hands pose.
COSTUME IDENTITY FROM NORMAL PORTRAIT: down hood, charcoal/navy long cloak and iron throat fastening; high-neck dark teal long-sleeve lower-calf work dress; tan/taupe long canvas work apron with utility pocket, sparse repair stitches and earth marks; dark brown belt with rectangular iron buckle, hanging belt tail, brown flap pouch, old cemetery keys, short secured cord loop; brown fingerless gloves, black opaque stockings and sturdy brown folded-cuff boots. Same coverage, modest long hem. No additions, armor, new jewelry or magic.
ART STYLE: approved refined anime illustration from input 2: delicate clean lines, intentional cel shadows, smooth materials with restrained soft face/hair shading. Clear blue eyes, pale skin, pointed elf ears, white/silver side-swept bangs, one thick braid over HER LEFT shoulder. No heavy outlines, painterly noise, photorealism or 3D.
COMPOSITION: 1024x1536 portrait, full figure and full D handle and spade, generous clear margins on every side, no silhouette touching edges. Whole character fills about 88-90% canvas height. Eye-level camera, minimal foreshortening so proportions are comparable to normal portrait.
BACKGROUND: genuine transparent RGBA and all empty gaps, fully opaque character. No backdrop, halo, vignette, floor, cast shadow, gradient, checkerboard, text or watermark. Single character only.
```
