# 哀歌のエレ コンセプト v2

2026-10-23。内蔵 image_gen で v1 を編集。
ユーザー指示: 既存のベニ、リリ、ヨミ、ネルを参考に絵柄を合わせ、少女ではなく大人の女性として描く。
参照画像（順序）: DemoApp/assets/portraits/beni.png、lili.png、yomi.png、nel.png、design/art/ere/ere-concept-v1.png。
最初の4枚は絵柄・成人女性の描写の参照、最後の1枚は編集対象。
線と陰影を既存のアニメ調に整理。喪のヴェール、編み髪、吟遊詩人の衣装、リュートの方向性を継承。
画像: ere-concept-v2.png。コンセプト検討用、ゲーム未組み込み。

## 生成プロンプト

Use case: style-transfer / identity-preserve.
Edit target: Image 5 is Ere, an adult human female medieval funeral bard, the previous concept. Images 1-4 are STYLE AND ADULT FEMALE CHARACTER RENDERING REFERENCES from the SAME GAME: 1 Beni, 2 Lili, 3 Yomi, 4 Nel.
Redraw Ere fully so she belongs in EXACTLY the same cast and illustration style as the four references. This is a substantial style correction, not just a filter. The strongest priorities are same anime drawing language, same simplified material rendering, and a clearly mature adult woman's character presence.

STYLE TARGET:
Match especially Yomi and Nel's clean dark anime contour lines, sculpted graphic hair locks, smooth skin with restrained shading, defined but elegant facial outline, simple anime nose and mouth, clean cel-shaded fabric planes, selective soft gradients, and clearly readable large forms. Use Beni and Lili as additional references for graceful mature feminine proportions and polished expressive eyes. The face should have an adult slightly elongated shape, narrow gently half-lidded eyes, composed eyebrows, a graceful neck and balanced adult shoulders, waist and hips; she reads as a woman in her late twenties to early thirties. Calm, quietly sorrowful, capable and warm. No childish round baby face or huge round eyes. Do not age her into an elderly person or add wrinkles.
Remove the first concept's photorealistic face, rendered lips, brown painterly wash, heavy surface grain, hyper-detailed embroidery, realistic fabric noise and numerous tiny distressed details. The final art should have the same clean anime readability as the references, with clearer light/dark separation and less muddy brown coloration. Avoid photo realism, oil paint, western realistic character concept rendering and fashion photography.

ERE DESIGN INVARIANTS:
Preserve the idea and identity of Image 5: human woman with ordinary rounded ears, dark chestnut long hair in a loose side braid, small black traveling cap and a sheer mourning veil behind her shoulders with uncovered face. Mourning clothing adapted for travel: charcoal short shoulder cape, dark muted wine-purple fitted bodice, ivory gathered long-sleeve blouse, flowing layered dark skirt, belt pouch and practical knee-high brown leather boots. Simplify embroidery to a few elegant trim accents in the same graphic language as the refs. A few repaired hems, not shredded into rags. Retain a medieval wooden pear-shaped lute held naturally at her waist by a shoulder strap, right hand ready to pluck strings, left hand fretting the neck. Readable lute rosette and strings, bent-back medieval pegbox. A small dark mourning ribbon tied to lute.
She must remain a distinct human bard: do not copy the reference characters' exact faces, hairstyle, nun costume, horns, pointed ears, tail, weapons, staff or magical effects. No added fantasy race features. Natural mature feminine proportions comparable to the references; preserve the bard outfit's covered torso.
COMPOSITION: one entire full-body figure, portrait orientation, 3/4 front standing posture, face and both hands clearly visible. A quiet singing expression with slightly parted anime-drawn lips and warm melancholy eyes. Full cap, veil, lute, skirt and boot soles in frame, comfortable margins. Plain pale neutral-gray background with a subtle grounding shadow as in Yomi and Nel, no dark vignette.
No text, watermarks, other people, inset panels, scenery, particles or floating music notes.
The result must feel like a new character illustrated by the same artist as Images 1-4.
