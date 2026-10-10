# 分かちのドハ — 演出用差分

生成方式: 内蔵 image_gen。2026-10-10。

通常・待機: `doha-standing-v2-anime.png`。
受け止め: `doha-receive-v1-anime.png`。
力返し: `doha-give-v1-anime.png`。

紫の痛みの筋・金の光・飛ぶ札はゲームの既存エフェクトで描画するため、追加差分には焼き込まない。

## 力返しの生成プロンプト

Use case: precise-object-edit.
Edit the supplied anime Doha battle image into a production-ready game sprite called "give". KEEP EXACT same anime adult male identity, face, hair, horns, bandages, chain and name-tag costume, worn ivory and charcoal palette, full-body three-quarter screen-right orientation, one hand clutching chest and the other palm outstretched toward screen right, staggered feet, camera, body proportions, canvas framing and clean anime drawing style.
ONLY REMOVE all magical effects: the purple pain stream, all glow, all light trails, all sparks, and the detached floating golden tag above his open hand. Restore the underlying cloth and bandages naturally where effects overlapped his body. All physical name tags still attached to his chains remain in place but are plain unlit worn wood/metal, INCLUDING the bright one at his chest. His hand is empty.
Genuinely transparent alpha background, with completely empty space outside the character and no halo/vignette, no ground plane or shadow. No background rectangle. Preserve full horns, fingertips and toes without cropping. No text, no new props, no design changes. This is an ANIME 2D game sprite matching the input, not a realistic render.

## 受け止めの生成プロンプト

Use case: identity-preserve / precise-object-edit.
Create a short pain-absorption pose variant of Doha for a 2D anime game. Image 1 is the clean giving pose and is authoritative for costume, adult face, horns, hair, canvas scale and position of feet. Image 2 is the same character's calm standing portrait for further identity context.
KEEP exact same clean anime style, tall broad-shouldered but lean adult man, dark long tousled hair, tired gray eyes, human face with lower face wrapped, curled goat horns, ivory bandages, black iron chains and many rectangular name tags, charcoal and ivory layered ragged robe, wrapped human feet. No redesign, no new equipment.
Change ONLY the pose and expression to the instant of accepting an ally's pain: keep the same staggered foot positions and full-body size as image 1, draw his upper torso bent forward slightly, head lowered and eyes partly shut in restrained pain. Both hands close to his body, one firmly clutching the chest chain at sternum and the other gently bracing below it against upper abdomen. Elbows close in, shoulders briefly tense. He remains standing and dignified, not falling, kneeling, screaming or attacking. Three-quarter facing SCREEN RIGHT, leg positions stay stable for switching to the giving pose. His gesture should be visibly distinct from the extended giving hand.
All tags are physical unlit wood/metal attached to chains. NO magic effects, no purple stream, no glow, no detached tag, no blood, no wound, no weapon.
True transparent alpha background, empty all around, no vignette or ground shadow, one full body with all horns and toes inside margins. Same portrait canvas aspect and body scale as image 1. Clean anime linework and shaded color shapes, no photorealistic texture.
