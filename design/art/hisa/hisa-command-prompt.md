# ヒサ：手番の号令・標付与差分（2026-10-10）

組み込み imagegen 使用。参照は `DemoApp/assets/portraits/battle/hisa_idle_right.png`（顔・衣装・画風）。
採用先：`DemoApp/assets/portraits/battle/hisa_command_idle_right.png`。
片手を腰に置き、もう片方で敵を指し示し、薄く笑う全身差分。標付与の `Command` で使用。
玉は生成画像に焼き込まず、実行時に指先（1007, 204）から飛ばす。
透過PNG・1024×1536。靴底下の余白12pxに接地値を合わせる。
検証：DemoAppビルド成功、Godot素材取り込み成功。左右×1倍/2倍の描画・既存号令チェックで
`SHOCK_MARK_CHECK_OK`、終了コード0。接地・反転・玉の軌道を確認。
キャプチャ：`.tmp/hisa-command-images/`。

## 生成プロンプト

Use case: stylized-concept. Create a NEW full-body battle sprite pose variant for Hisa, the EXACT adult male character in the supplied reference image. Reference role: identity, outfit, proportions, detailed Japanese fantasy anime linework and rendering only. Output is a transparent PNG game sprite.
Action: his own-turn command to mark an enemy: a sly, composed 'there, that one' pointing gesture. His head and entire torso face screen RIGHT in a clear three-quarter view, about 45 degrees from frontal. One arm extends toward screen RIGHT at upper-chest height, a single straight index finger clearly designating an enemy offscreen. Fingertip should be clearly silhouetted near x=940, y=440 on a 1024x1536 canvas, with margin beyond the fingertip. The other hand rests naturally on his own hip/belt, elbow relaxed. No hand at mouth. A small knowing asymmetric smile, narrowed calculating eyes looking right along his finger; mouth only slightly open as if speaking quietly, not shouting. Strong readable silhouette and relaxed confidence.
Anatomy: natural grounded standing stance, moderate stagger, both boots firmly on the same ground plane, hips over the feet, knees naturally aligned with toes, no twisted limbs, no deep lunge or crouch. Full head and both boots entirely visible, character fills nearly full height with about 30 pixels below the lowest sole, adequate margins at top and sides. Preserve his tousled brown hair, gray-blue eyes, light chin stubble, burgundy scarf, charcoal dark-teal long coat with fine gold geometric embroidery and shoulder cape, cream shirt and lining, brown belts and pouch, brown trousers and modest brown boots. Natural fabric drape.
Keep the original refined anime illustration style, face identity, adult age, costume details, colors and normal body proportions. Single character only. Genuine transparent background and clean alpha edges. No black backdrop or gradients, no ground plane or shadow, no text, no symbols, no baked-in glow or magic balls, no weapons, no extra hands or fingers. The game adds effects separately. Portrait canvas 1024x1536.
