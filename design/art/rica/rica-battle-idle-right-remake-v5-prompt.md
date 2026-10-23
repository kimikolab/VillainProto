# 墓守リィカ 戦闘待機立ち絵リメイク v5

生成日: 2026-10-24
生成方法: Codex 組み込み ImageGen（CLI 不使用）
出力: `rica-battle-idle-right-remake-v5.png`（1024×1536、透過PNG）
状態: 確認用。ゲーム未適用。
キャラクター・衣装・頭身の正: `rica-standing-remake-v3-shovel-lean.png`
構えの参考: ユーザー提供の2枚目の設定資料風画像の右上3体、その中央。`rica-v5-battle-pose-reference.png` として保存。

腰の高さを保ち、膝を軽く緩めた右向きの待機姿勢。後ろの右手をD型の持ち手付近に、前の左手を柄の中ほどに置く。設定資料の構えと持ち方を参照し、通常立ち絵の体格・配色とアニメ寄りの絵柄を使う。初回生成で胸に資料側の革ストラップが入ったため、胸当て付きの前掛けへ局所修正した。既存の通常・戦闘画像とゲームの参照先は変更していない。

## 構え生成プロンプト

```text
Use case: identity-preserve battle pose from a supplied pose sheet.
Deliver ONE full-body right-facing battle-idle portrait of Rica the elf gravekeeper. This is a new composition, not a character sheet.

REFERENCE ROLES:
Image 1 (normal Rica leaning on shovel) is AUTHORITATIVE for identity, adult proportions, precise costume, hairstyle, palette, shovel construction and clean anime art style.
Image 2 is a POSE SHEET. Use ONLY the MIDDLE of the THREE SMALL FULL-BODY FIGURES IN THE UPPER-RIGHT ROW as the exact pose/holding reference. That small middle figure faces screen right with relaxed bent knees, waist high, rear hand near D grip, front hand at shaft midpoint, shovel blade low in front. Do not copy the large left figure, detail insets, chibi/pixel figures, sheet layout, gray background, or costume changes in the sheet.

POSE — FOLLOW THE SPECIFIED SMALL UPPER MIDDLE FIGURE:
A natural sustainable combat WAITING guard facing SCREEN RIGHT in a more side-on three-quarter view, around 60 degrees from frontal. Body, head, eyes, BOTH feet all face screen right. Calm quietly attentive expression looking at enemy offscreen right, not viewer.
Waist remains at ordinary standing height with only a small drop. Feet a comfortable staggered step apart, moderate width approximately 1.2-1.4 shoulder widths, both soles grounded. Knees SOFTLY flexed, around 15 degrees; enough readiness without crouching. Chest gently inclined forward, shoulders relaxed, elbows have room. NOT a deep squat, wide martial arts stance, rigid locked knees, or long lunge. Same slender adult skeleton and head/body proportion as image 1, no enlarged head or compressed torso.
Hold ONE shovel in two hands at waist level, shaft diagonally crossing front of body, descending modestly toward SCREEN RIGHT. Rear RIGHT hand near the D handle beside rear hip at screen left; front LEFT hand grips the wooden shaft around its middle beside front hip at screen right. Blade extends forward/right at lower knee height, not raised to shoulder. Compact balanced silhouette like selected small pose. D grip only a little behind her rear hand, not far up by shoulder.
HANDS: rear hand naturally grasps the D grip's wooden crossbar or immediately adjacent shaft; front LEFT hand is an anatomically clear supporting underhand grip with palm upward under the shaft, one distinct thumb opposing four curled fingers. Shaft goes THROUGH the grasp, correct occlusion of near fingers and far fingers, wrists straight and elbows comfortably flexed. Correct five-digit hands, natural thumb location, no fingertips pasted flat on top, missing thumb, detached digits or twisted wrist.

EXACT DESIGN FROM IMAGE 1:
Slender young adult female elf with pale skin, ice-blue eyes, white/silver side-swept bangs and face-framing locks, ONE thick braid over HER LEFT shoulder, long horizontal pointed elf ears. Same quiet pensive face, no angry grimace or big smile.
Charcoal/navy long hooded cloak with hood DOWN and small metal throat clasp, muted dark teal high-neck long-sleeved calf-length work dress; muted tan/taupe-brown long work apron with the SAME single flat rectangular reinforced utility pocket and sparse cross repair stitches/earth marks; brown wide belt with rectangular iron buckle and hanging belt tail; brown leather flap pouch at HER LEFT hip, cemetery keys and short secured cord loop; brown fingerless work gloves, opaque black stockings and sturdy brown folded-cuff ankle-to-midcalf work boots. Keep these unchanged. Do not copy sheet's split apron, extra crossing chest straps, extra braids, shredded cloak, or swapped pouch/key positions.
Shovel: same D-shaped iron grip with wooden crossbar, STRAIGHT wooden shaft, straight reinforced metal socket and flat pointed digging blade with central rib and equal boot-pressure shoulders. Rear grip center, shaft, socket, rib and tip follow one straight axis. No bent/banana blade, twisted socket or curved shaft. Normal practical spade dimensions and restrained earth wear.

ART STYLE: polished modern Japanese anime illustration matching image 1, delicate clear outlines, smooth controlled cel shadows, restrained soft shading in face/hair, clean readable folds, minimal texture. No photorealism, 3D or gritty noise.
OUTPUT: vertical 1024x1536 single full-body portrait, eye-level camera, minimal foreshortening, whole head, ears, cloak, both boots, whole D grip and spade visible with generous clear margins. Actual character occupies about 88-90% canvas height. No sheets or multiple figures.
Genuinely transparent RGBA background and empty gaps. Fully opaque character interior. No background, gray field, gradient, vignette, halo, floor, cast shadow, checkerboard, text, signature, watermark or border.
```

## 最終・胸元の衣装修正プロンプト

```text
Precise LOCAL COSTUME CORRECTION ONLY. Image 1 is the edit target. Image 2 is the authoritative exact original outfit reference.
Keep image 1's entire new right-facing battle pose, head and body proportions, face, hair, hands and hand grips, all limb positions, foot positions, shallow knee flexion, shovel position and straight geometry, framing, colors and anime rendering IDENTICAL.
Change ONLY the chest garment between cloak clasp and waist belt: REMOVE the brown crossing leather harness straps on chest. Restore the original TAN CANVAS APRON BIB from image 2, a plain solid tan/taupe cloth panel covering front of chest and continuing naturally into long apron under waist belt. Subtle stitched upper apron edge across chest like image 2, dark teal high-neck shirt visible above and at sides. No crossing straps on chest. Braid remains in front of apron. Do not change pose, hands, face, belt, pocket, keys, cord, dress, cloak, shovel, boots or any other detail. Preserve true transparent background and dimensions.
Output ONE full-body transparent portrait, NOT a sheet. This is chest-bib correction only.
```
