# ムド：泥袋型・泥の外套型の怒り差分 v12

2026-10-28。組み込みの image_gen で通常案を参照して作成。ゲームへの適用・コミットは未実施。

- `mudo-sack-angry-v12.png`：へたった連続した泥袋の胴体を保ち、左右の腕と拳だけ巨大化。初稿に残った余分な小腕は追加生成で除去した。
- `mudo-mantle-angry-v12.png`：フードと泥の膜、内側の空洞を保ち、左右の腕と拳だけ巨大化。
- 怒った泥の眉、丸い開口、グレーの泥と暖色の反射・寒色の影を共通にした。
- 形状検討用の静止画。外周に生成由来の淡い霞が残っているため、ゲーム用の切り抜き素材としての最終調整は未実施。

## 参照

- 泥袋型：`../slumped-mud-sack-v10/mudo-normal-sack-v10.png`
- 泥の外套型：`../alternative-forms-v11/mudo-mantle-v11.png`

## 泥袋型のプロンプト

```text
Use case: identity-preserve.
Asset type: Mudo fantasy game character, angry transformation concept, single complete isolated full-body illustration.
Input image: edit target, the approved slumped mud sack NORMAL form. Create its ANGRY version.
Preserve exactly its recognizable crooked continuous sack-shaped body, slumped asymmetrical top, embedded simple face, single puddle base, flowing diagonal folds, little mineral grains and wet gray clay surface. Body remains the same modest size and unstructured sagging shape.
Transform ONLY its two tiny side arms into ENORMOUS heavy clay arms and clenched mud fists. Each fist is about half the body height and much thicker than the body, arms extend out from the same attachment points. One fist drawn back to strike, the other thrust forward and slightly downward, readable three-quarter battle stance. Thick goo bridges and stretched flowing folds show body mud surging into the arms. Fists should look like malleable compacted wet clay with broad knuckle ridges and drips, not stone boulders or armored gloves. No anatomical biceps, chest muscles, torso, waist, hips, legs or feet.
Anger expression: original small dark eye openings become sharply inward-slanted under thick furrowed mud ridges, fierce focused glare. Keep the characteristic open ROUND O mouth, no teeth, tongue, lips or human face. Don't add glowing eyes or magic.
Material: neutral GRAY wet mineral mud, subtle warm ivory reflected highlights and cool blue-slate shadows as in the reference, small embedded gravel, handmade painterly fantasy game rendering, clean readable contour. Gray material, not grayscale filter or brown slime.
Composition: full character including both huge fists and puddle visible, comfortably framed with margins on every side, landscape canvas if needed. True transparent background, no environment, no haze, no text, no labels, no watermark, no motion-blur. Exactly one angry character. This is the SAME mud sack creature with giant arms, not a humanoid muscular golem.
```

## 泥袋型：余分な腕の修正プロンプト

```text
Use case: precise-object-edit. Fix this angry mud sack concept with ONE localized correction: REMOVE the extra little dangling hand/forearm resting along the front-left edge of the central body (approximately x=480-620, y=510-740 in this 1536x1024 image). Replace only that little third limb with natural continuous sagging diagonal mud folds belonging to the sack. The TWO enormous fists and their arms already present at the upper left and right are the ONLY two arms and hands. Keep both giant fists, both thick arm connections, angry eyes, round O mouth, continuous legless sack body, puddle base, camera and gray wet clay rendering unchanged. Exactly two arms total, no little hands remaining. Preserve the whole silhouette and composition. Transparent background, remove diffuse surrounding haze where possible; no text or labels.
```

## 泥の外套型のプロンプト

```text
Use case: identity-preserve.
Asset type: Mudo fantasy game character ANGRY transformation, single complete isolated full-body concept.
Input image is the edit target: approved hollow MUD MANTLE normal form.
Keep the same hood made from folded wet gray mud sheets, suspended little clay face inside the hood, dark open hollow cavities between overlapping hanging membranes, asymmetrical sagging silhouette, one continuous puddle hem. Keep the torso thin, hollow and draped, exactly like the normal reference; there is no body inside the mantle.
Change its TWO existing tiny hanging side arms into TWO enormous mud forearms ending in clenched giant clay fists. These replace the original hands completely: exactly two arms and two hands TOTAL, no extra tiny arms, no duplicate limbs. One huge fist raised back ready to pound; the other extends forward lower, a fierce punching-ready stance. Each fist about half the hood-to-ground height, much wider than the hollow center. Side sheets stretch and gather into the arms; ragged muddy underarm bridges retain some visible openings through the empty cloak. The contrast is an empty flimsy mantle carrying outrageously heavy fists. Do not fill out its hollow body with a muscular torso.
Angry face: recognizable small embedded face inside the hood, sharp inward-tilted dark eye slits with downturned sculpted mud brow ridges. Keep its simple open round O mouth, no teeth, lips, tongue, nose or glowing eyes. Still recognizably the SAME character.
Gray mud MATERIAL with subtle warm ivory highlight reflections, cool blue-slate shadow reflections and sparse little gravel, same painterly outlined polished game illustration as reference. Wet malleable clay, not brown slime, stone, metal, cloth, human skin or a grayscale filter.
Composition landscape, full character, BOTH massive fists fully visible with comfortable margins, front three-quarter view, clear silhouette. Exactly one angry character on true transparent background. No atmosphere or glow halo, no environment, no text or labels, no motion blur. No human chest, abs, waist, hips, groin, legs, shoes, biceps, or person under a robe.
```
