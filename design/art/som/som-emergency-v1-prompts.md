# ソムの緊急下僕素材（2026-10-11）

imagegen スキルの組み込み image_gen を使用。既存の猫をスタイル参照にして、大型の角・紫の毛並み・金の首輪と宝石を持つ獅子型の下僕を作成。2枚ともRGBAの透過PNGを保持し、既存素材は置換しない。

- `DemoApp/assets/portraits/battle/fodder_majestic_lookback_idle_right.png` — 出現・主人を一瞥。1344×1170。
- `DemoApp/assets/portraits/battle/fodder_majestic_run_idle_right.png` — 右向きに走る差分。1536×1024。
- 敵側のソムでは左右反転。描画高さは出現1.95／走り1.70。小型猫は0.95。
- 画像加工による背景削除はしていない。生成されたアルファをそのまま使用。

## 出現のプロンプト

Use case: stylized-concept. Asset type: transparent full-body 2D game summon sprite. Image 1 is reference for the established purple cat character, coloring and detailed hand painted anime fantasy style. Create a distinct much larger majestic lionlike horned feline servant, proud muscular lion body and voluminous violet mane, dark purple fur, pale lilac muzzle, golden curved horns, golden ornate collar with amethyst jewel, yellow slit eyes. Body is standing on four legs in side three-quarter view facing RIGHT, head looking back LEFT with a dismissive smug look at its summoner. All four paws visible, tail visible, complete silhouette centered with generous transparent margins. This is a visually imposing adult lion sized relative to the small cat reference, not a kitten. Match the reference's crisp painted game illustration style. Single character only, no scene, no text, no magic effects, no shadows, true transparent background. Save as the emergency servant standing/lookback sprite.

## 走りのプロンプト

Use case: identity-preserve. Asset type: transparent 2D game running sprite. Image 1 is the exact majestic emergency lion servant whose identity must be preserved. Image 2 is the small cat running pose and painting style reference. Change ONLY pose and face of Image 1 into a bounding run facing RIGHT with head also facing RIGHT, arrogant dismissive half-lidded yellow eyes, powerful four legs stretched in mid stride and flowing large violet mane. Preserve its adult lionlike broad chest, proportions, dark purple fur, pale lilac muzzle and paws, same gold curved horns and same gold amethyst collar, tail with golden tip. Single entire full-body character with all legs and tail fully inside frame, generous transparent margins, crisp anime painted fantasy game illustration. No text, no ground, no shadow, no effects, true alpha background.
