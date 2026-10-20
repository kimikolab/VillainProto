# ヒビ 元海軍版・戦闘待機 v4 — ユーザー採用構図の透過・余白調整

方式: 内蔵 image_gen。画像制作のみ・ゲーム未適用。
編集対象: ユーザー添付 `C:/Users/Pon/Downloads/巨大な盾を構える傷戦士.png`。
参照保存先: `hibi-naval-bodyguard-battle-user-reference.png`。
出力予定: `hibi-naval-bodyguard-idle-right-v4.png`。

ユーザーが選んだ構え・遠近感・顔・髪のメッシュ・衣装・大盾を極力そのまま維持し、背景と床影の除去、全身と盾の余白確保を行う。
前後に開いた足は同じ接地平面として扱い、両足の画像上の高さを無理に揃えて迫力を失わせない。正面待機・ゲーム素材・コードは変更しない。待機モーションの制作は今回の範囲外。

## 生成プロンプト

初回の透過化で構えは維持できたが、alpha 128以上の前景範囲が(10, 69, 1020, 1458)と左右の余白が不足したため、下記「追加の余白調整」を続けて実施。

Use case: background-extraction / identity-preserve / production framing.
Asset type: transparent full-body battle-idle standing portrait for a fantasy game, preview only.

The supplied image is the APPROVED EDIT TARGET, not loose inspiration. Preserve its compelling combat composition as closely as possible. The user specifically chose THIS image over earlier alternatives. Do NOT redesign or repose the character.

Primary request: prepare this exact dynamic shield-warrior illustration as a clean game sprite by removing the white/gray background and ground shadow, adding safe empty margins, and retaining believable planted feet.

LOCKED:
- Same recognizable adult woman Hibi: exact face, stern eyes and expression, short charcoal hair, the single white/silver streak on the SCREEN-RIGHT fore-temple as shown, and lean strong physique.
- Same head angle and enemy-directed gaze toward screen right, same torso lean, shoulder engagement, arm angles, freely extended open hand, bent knees, widely staggered planted boots, large foreground leg and relative perspective. Preserve the energetic diagonal flow and solid weight behind the shield. Do not straighten her into a neutral portrait, make a new shield-thrust pose or shrink the foreground boot independently.
- Same huge broken oval mirror shield, same angle in front of her body on SCREEN RIGHT, overlapping the shield-bearing shoulder and torso, same size relative to the body, same aged brass/black iron rim, fasteners, cracks, reflective pale silver-blue mirror panels and missing dark-backed pieces.
- Same exact navy/aged brass/ivory/brown/burgundy naval costume, partial armor, dark trousers, gauntlets, knee plates, buckled steel-toed boots, thigh straps/pouches and wind-swept tattered cloth silhouettes. Do not import another costume or add a sword or gun.
- Same polished finely drawn anime fantasy-game rendering and detailed restrained painterly shading, colors and material finish.

ONLY REQUESTED CHANGES:
1. GENUINE TRANSPARENCY: remove ALL surrounding white/gray background and ALL cast shadows on the floor. All empty pixels must have alpha ZERO, including gaps under arms, between legs and between cloth strips. Character surfaces, mirror panels, cloth, hair, leather and metal stay opaque; mirror is reflective material, not transparent glass. Preserve fine hair and torn cloth edges with clean antialiasing, no white matte fringe. NO new dark gradient or glowing halo.
2. SAFE FRAMING: output a portrait 1024x1536 RGBA PNG. Fit the WHOLE figure, complete oval shield rim, fingertips, every cloth tip and both entire boot soles inside the canvas with approximately 4–5 percent clear margin left and right and a little clear space above/below. Use a modest UNIFORM zoom-out / canvas reframing of the entire figure if needed, rather than changing body/weapon proportions. If a tiny cloth/edge tip was cut by the source, complete only that existing tip faithfully. Do not cut off the shield or boots.
3. CONTACT: keep the SAME credible stagger and perspective of the two planted feet, soles resting on an implied common ground PLANE. Do not force both feet to the same image row or flatten the dramatic perspective. Keep her grounded without rendering a floor or shadow; do not lift heels or make her float. There is no scene or ground texture in the final output.

No new pose, no character redesign, no additional characters, no floating mirror shards or debris, no particles, smoke, fire, magical aura, action effects, text, labels, panels, watermark or painted checkerboard. This is a faithful transparent adaptation of the selected image, NOT a new concept variant.

## 追加の余白調整

初回出力 `exec-2a112de7-233e-4d60-bc09-85016b4febdd.png` を編集対象として指定。

Use case: identity-preserve / precise production reframing.
The supplied transparent portrait is the edit target and its character is LOCKED. Make ONLY one compositional adjustment: zoom out the ENTIRE existing foreground character-and-shield together by approximately 10 percent within the SAME portrait 1024x1536 canvas, keeping their proportions, pose and relative positions unchanged.

The previous version has almost no space at the left tattered cloth tips and the right shield rim. This edit MUST create visibly wider EMPTY TRANSPARENT margins on both sides. Put ALL opaque character pixels, shield edges, cloth tips, fingers and boots inside an approximately x=60 to x=964 safe region. Also keep clear space above the hair and below the lowest sole. Do NOT fill the canvas again. The whole character should appear a little smaller within a roomy transparent canvas, not cropped or stretched.

Preserve the exact current silhouette and dynamic braced guard: same face, eyes, right-forehead white hair streak, hair strands, forward leaning torso and engaged shoulder behind the shield, freely extended open hand, staggered bent legs and large foreground boot perspective. Same outfit, cloth flow, shield size RELATIVE TO HER BODY, shield angle, cracks, mirror fragments, metalwork, palette, fine anime linework and painterly rendering. Do not change the shield pose, pull it away from her shoulder, make a neutral stance, flatten the perspective, alter the boot size independently or redesign anything.

GENUINE transparent RGBA PNG. Keep alpha zero throughout all empty margins and gaps. No surrounding glow, background, white/gray floor, ground shadow, scenery, gradient, vignette, particles, text or checkerboard illustration. All character, costume and mirror materials opaque. No added objects. Final result should look like the same illustration faithfully scaled down slightly and framed with proper sprite padding.
