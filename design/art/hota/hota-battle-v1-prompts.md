# ホタ 戦闘中立ち絵 v1

内蔵 image_gen で生成。hota-concept-v2.png の採用デザインと既存の yomi_idle_right.png の向き・画風を参照。DemoApp 未実装。

保存画像は両方 1024×1536 RGB。透過指定に反して格子背景が描画されており、背景除去の再試行でも解消しなかったため、現段階は構え・外見の確認用。透過素材としては未完成。

## 非燃焼

Use case: identity-preserve. Create ONE standalone full-body battle idle character sprite of Hota, NON-BURNING state, genuinely transparent RGBA background.
Reference 1 is the approved Hota design: preserve exactly her identity, orange-blonde waist-length flowing hair, teal eyes, hair ornament, charcoal fitted armor with worn gold trim, deep crimson short skirt and long split rear panels, straps and bandages, armored boots, blue-jewel ornate ONE-HANDED sword. Use LEFT non-burning character as identity/costume source. Reference 2 is Yomi battle sprite: use ONLY for three-quarter right-facing orientation, camera framing and existing game anime rendering style; do not transfer Yomi's clothes or identity.
Turn Hota into a right-facing three-quarter battle idle stance: torso, face, feet and gaze aimed toward an enemy OFF SCREEN RIGHT, face still readable, not looking at viewer. All head-to-toe including hair, boots and sword tip within canvas with comfortable margin. Standing grounded, feet slightly staggered, modestly bent knees, shoulders a little tense; youthful unsure but attentive expression, ready to fight. Sword gripped ONE HANDED in a low forward guard aimed diagonally down toward SCREEN RIGHT, free hand lightly near chest, not holding blade. Hair flows behind her toward SCREEN LEFT reaching waist. Natural anatomy. Her design is unchanged. Clear silhouette, restrained shading and dark clean line art matching both references, no splash art.
NO flames, sparks, glow, environment, floor, cast shadow, text, panels, second character, borders, checkerboard pattern or opaque backdrop. Portrait canvas. Transparent cutout intended as a battle standing asset, not an action attack frame.

## 燃焼

Use case: identity-preserve. Create BURNING battle idle version of Hota from reference 1 (the just-approved non-burning battle sprite). Reference 2 is her approved concept sheet, right character for burning attitude and fire treatment.
Output ONE full-body character only on genuinely transparent alpha background. Preserve reference 1's exact face identity, teal eyes, orange-blonde long hair, anatomy, charcoal and gold armor, crimson short skirt and rear cloth, bandages/straps, armored boots and blue-jewel one-handed sword. Preserve same right-facing three-quarter camera, character scale, feet baseline and portrait framing as reference 1, so these are a matched battle sprite pair.
Change demeanor to calm assured strong warrior: eyebrows relaxed, slight confident smile, gaze firmly at enemy OFF SCREEN RIGHT, chin modestly raised, shoulders open, torso more upright and poised. Free hand relaxed and lowered rather than tense against chest; sword remains one-handed low diagonally toward screen right in easy controlled guard. Stable feet, not lunging or attacking. Hair flowing to screen left, stays waist-length.
Add readable orange flames curling along her hair silhouette, shoulders, outer rear skirt panels and armored shins, subtle warm reflections on metal and hair. Match the restrained flame design of right figure in concept sheet. Flames are attached to character silhouette, small local sparks only, no enormous fire cloud, no opaque background behind fire, do not obscure face or sword. SAME costume, no damage or transformation. She is already burning, no spellcasting gesture.
Keep existing restrained anime game rendering, clean dark lines, cel shading with modest soft transitions. Entire sword tip, hair, fire and boots inside image with margin. Transparent cutout, no floor, cast shadow, text, second figure, scenery, checkerboard drawn into pixels or opaque gray/white background.
