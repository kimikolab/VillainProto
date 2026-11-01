# チリ — ボトムス修正案 v1

2026-11-01。内蔵image_genで既存通常立ち絵を編集（CLI不使用）。

依頼: ゆったりしたズボンが男子のズボンやステテコに見えるため、ハロウィンの女の子らしさが出るスカート、または女の子向けの半ズボンへ変更したい。

- chiri-skirt-v1.png: 黒の短いフレアスカートに、かぼちゃ色の裏地・裾。上着の裾と馴染む少し不揃いな形。
- chiri-pumpkin-shorts-v1.png: 黒×かぼちゃ色のコンパクトなショートパンツ。丸いシルエットと小さな裾飾り。

顔・髪・上着・手・爆弾・鞄・ポーズ・靴の印象を維持する方針。通常立ち絵を使った比較候補2枚で、ゲーム配置先は変更していない。戦闘待機・仕掛ける・手を離すへの展開は、採用するボトムスの方向が決まってから行う。

参照画像: DemoApp/assets/portraits/chiri.png
形式: 1024×1536、RGBA。生成時の透過をそのまま保存。

## プロンプト全文

### skirt

```text
Use case: precise-object-edit / identity-preserve.
Asset type: an alternative costume-bottom revision of the existing full-body Chiri normal game portrait.
Image 1 is the exact approved portrait EDIT TARGET. The user feels the current loose below-knee culotte trousers resemble men's trousers or long underwear and wants a skirt or feminine short shorts to recover the charming Halloween-girl feeling.
Change ONLY the bottoms from the existing waist/belt down to the garment hem. Keep the rest of the character as identical as possible: exact recognizable face, mischievous happy smile, teal eyes, ash blonde messy low ponytails, hair ornament, soot, head angle, torso proportions, charcoal hooded short cape, orange scarf, ivory rolled sleeves, leather gloves, both arm/hand poses, black spherical bomb held out, large brown bomb satchel and brass star-cut spherical charm, waistband belt and its orange ties. Keep the same legs, their exact stance and positions, original orange-and-cream striped socks, and original chunky brown boots. Where the old long trousers covered upper legs, draw anatomically natural legs that fit the unchanged body and feet. Do not change the head size, body height, age or body shape. Young adult woman, lively cute fantasy adventurer, modest game costume, not sexualized.
Maintain the same crisp refined anime linework and nuanced textured painterly cel shading. Use the existing charcoal, rust orange, brown and cream palette. Keep small repair stitches and modest worn edges to match the playful powder-workshop troublemaker, but make the bottom silhouette clearly cute and feminine, with a distinct finished designed garment rather than simply chopping off the trousers.
Genuine transparent RGBA background, preserve empty gaps and full opaque figure. Full body and every prop visible at the same 1024x1536 portrait scale and framing, same margins and feet locations. No backdrop, no ambient halo, no floor or cast shadow, no crop, no new headgear, no new accessories outside the garment, no text, no panels or labels, one figure only.
BOTTOM DESIGN OPTION 1 — SHORT FLARED HALLOWEEN SKIRT:
Replace the two enormous trouser legs completely with ONE continuous, clearly recognizable short A-line skirt, ending around mid-thigh and comfortably above the knees. Neatly fitted at the existing belted waist, then flaring outward with softly structured broad pleats, not tight around hips. Charcoal outer skirt with a restrained scalloped / slightly irregular hem suggesting the existing ragged little witch cape, a narrow muted pumpkin-orange underskirt peeking evenly below the dark hem, one small row of cream hand-stitch repairs. A few soft gathered folds give it a lively bounce, without huge frills or excessive lace. Fully opaque skirt with concealed safety shorts underneath; no visible underwear, no lifted skirt, no upskirt view. Make this a practical cute mischievous little-witch-inspired adventurer skirt that visibly restores the Halloween-girl mood. The bag remains in front of the left side exactly as before; keep the visible continuous skirt hem on the other side and at center so the silhouette reads as a skirt rather than shorts. Keep feet/pose exactly unchanged.
```

### shorts

```text
Use case: precise-object-edit / identity-preserve.
Asset type: an alternative costume-bottom revision of the existing full-body Chiri normal game portrait.
Image 1 is the exact approved portrait EDIT TARGET. The user feels the current loose below-knee culotte trousers resemble men's trousers or long underwear and wants a skirt or feminine short shorts to recover the charming Halloween-girl feeling.
Change ONLY the bottoms from the existing waist/belt down to the garment hem. Keep the rest of the character as identical as possible: exact recognizable face, mischievous happy smile, teal eyes, ash blonde messy low ponytails, hair ornament, soot, head angle, torso proportions, charcoal hooded short cape, orange scarf, ivory rolled sleeves, leather gloves, both arm/hand poses, black spherical bomb held out, large brown bomb satchel and brass star-cut spherical charm, waistband belt and its orange ties. Keep the same legs, their exact stance and positions, original orange-and-cream striped socks, and original chunky brown boots. Where the old long trousers covered upper legs, draw anatomically natural legs that fit the unchanged body and feet. Do not change the head size, body height, age or body shape. Young adult woman, lively cute fantasy adventurer, modest game costume, not sexualized.
Maintain the same crisp refined anime linework and nuanced textured painterly cel shading. Use the existing charcoal, rust orange, brown and cream palette. Keep small repair stitches and modest worn edges to match the playful powder-workshop troublemaker, but make the bottom silhouette clearly cute and feminine, with a distinct finished designed garment rather than simply chopping off the trousers.
Genuine transparent RGBA background, preserve empty gaps and full opaque figure. Full body and every prop visible at the same 1024x1536 portrait scale and framing, same margins and feet locations. No backdrop, no ambient halo, no floor or cast shadow, no crop, no new headgear, no new accessories outside the garment, no text, no panels or labels, one figure only.
BOTTOM DESIGN OPTION 2 — FEMININE PUMPKIN SHORTS:
Replace the loose below-knee trousers completely with compact high-waisted tailored pumpkin-style short shorts, ending at the upper-to-middle thigh, distinctly short with long visible knees/legs beneath. A cute small rounded/puffed silhouette at the hips, tapered into narrow structured cuffed leg openings. Outerwear construction with charcoal opaque fabric, muted pumpkin-orange inset side panels / narrow vertical seams and a tiny scalloped edge along the outside of the cuffs; restrained decorative cream stitching matching her existing repairs. Existing leather belt defines the waist. No extended trouser tubes or knee-length panels; make them genuinely compact shorts. Not oversized bloomers or loose long underwear, not lingerie, not tight hot pants, not a skirt. Give the silhouette the adorable playful pumpkin shape associated with Halloween fantasy costumes, in a practical adventurous way. Preserve the same stance and boots and orange/cream striped socks. Do not change any upper clothing or the character's identity.
```
