# ヒサ：庇い差分 v3（右向きの受け構え）

組み込み imagegen を使用。v2の正面寄りの体を、右の敵から一撃を受ける横向きの構えへ修正。顔だけでなく肩・腰・膝・足先の向きを合わせ、自分を指す仕草を維持。
採用先：`DemoApp/assets/portraits/battle/hisa_cover_idle_right.png`。

検証：透過PNG・1024×1536。靴底の下余白37pxに接地値を合わせた。左右×1倍/2倍の画面確認と `SHOCK_MARK_CHECK_OK`（キャプチャ：`.tmp/hisa-v3-images/`）。

## 初回プロンプト（胸がまだ正面寄りだったため再修正）

> Edit the supplied Hisa cover sprite. Main correction: ROTATE HIS ENTIRE BODY TO FACE SCREEN RIGHT, ready to RECEIVE an enemy blow arriving from the right. The current image has a nearly frontal torso with only the head turned; replace that with a clear RIGHT-FACING near-side-profile three-quarter stance, about 65-75 degrees rotated from frontal view. Shoulder line, ribcage, belt/pelvis, thighs, knees and both boot toes all follow the same rightward orientation. We see mostly his left side, with a little of the front of his chest. Do not just turn his head.
> He has stepped between his ally on the left and the enemy on the right. Brace gently toward screen right: rightmost forward boot firmly planted under a slightly bent forward knee, rear boot one small step to the left, heel down. Torso inclines slightly forward as one coherent body above a stable pelvis; normal straight anatomical leg chains with no twisting or crossed knees. Compact natural staggered stance, no deep lunge, no oversized feet, no levitation. Both boots rest on the same imaginary ground plane.
> Keep the small visible gesture of his nearer hand pointing its index finger to his OWN upper chest ('...over here'). The other arm extends gently backward toward screen left, protecting the ally. Focused determined expression, mouth closed, eyes toward screen right. This is a protective bracing pose, not an attacking pose.
> Preserve exactly this adult character's face identity, tousled brown hair, slight chin stubble, gray-blue eyes, burgundy scarf, charcoal teal embroidered coat, fine gold geometric trim, cream shirt and robe lining, belts, pouch, trousers and brown boots. Preserve the detailed anime linework, rendering quality and proportions. Allow clothing to drape naturally around the newly rotated body, with scarf tails trailing slightly left.
> One full-body character only, complete head and both boots visible, centered with small margins, 1024x1536 portrait. Soles about 30 pixels from bottom. Transparent background with clean alpha, no scenery, floor, shadow, text, weapon or effects. Preserve transparency.

## 採用プロンプト（初回出力を参照）

> Pose correction only. Use this exact character, costume, illustration style and transparent full-body format, but redraw him in a TRUE RIGHT-FACING SIDE VIEW. Strict left-side profile, viewed from his LEFT SIDE; the camera sees his left shoulder and left hip nearest. His nose, breastbone, navel, both kneecaps and BOTH boot toes point toward the RIGHT EDGE OF THE IMAGE. His back faces the LEFT EDGE. His torso should appear NARROW in profile, not broad and front-facing. The far right shoulder is largely hidden behind the near left shoulder. Show a side view of his waist belt, NOT the buckle presented to the camera. This rotation must affect the whole skeleton, not just face or front leg. The current input remains too frontal.
> Natural stationary defensive stance receiving an impact from the right: one foot slightly forward to the right, rear foot to the left, both soles flat, modest knee bend, pelvis and ribcage aligned, slight forward bracing lean, no twisted knees. Keep plausible equal-length legs and modest step length.
> His visible LEFT hand is bent up, index fingertip touching his own chest in profile. His other arm reaches slightly backwards to the left to keep the protected ally behind him. No attack, no weapon, no enemy included.
> Preserve the same face identity, brown tousled hair, light chin stubble, stern closed-mouth expression, burgundy scarf, dark teal coat with gold embroidery, cream lining, brown trousers and boots. All clothing must wrap around the side-on body. Fine anime linework and matching colors. Full head-to-toe single sprite, 1024x1536 portrait, both boots uncut, ~30 pixels below lowest sole, clean transparent alpha. No scene, ground, shadows, text or visual effects.
