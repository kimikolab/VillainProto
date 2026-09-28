# ヨミ追加居合・前傾修正版

内蔵 image_gen を使用。追加攻撃だけを置換。右手で抜刀、左手で鞘を引き、前脚へ重心を移す低い横一閃。

保存先: `DemoApp/assets/portraits/battle/yomi_iai_extra_idle_right.png`

初期参照: `yomi_idle_right.png`。初稿: `exec-ad30c716-c04a-4c7b-a1a8-3157a6650bfa.png`。
最終生成物: `exec-88a08d54-1978-472a-afce-5f0171118aea.png`（1254×1254、元の透過を維持）。

## 初稿プロンプト

```text
Use case: identity-preserve. Create a replacement extra-attack sprite for the original adult woman Yomi in the reference. Retain exactly her face, long charcoal-black hair with small dark red ribbons, tired red eyes, tattered black/charcoal kimono with dark red lining, white chest wraps, brown waist belts, thigh straps and strapped boots, and black/red katana scabbard. Detailed polished anime game illustration matching the reference. The requested action is a deeply forward-leaning explosive battoujutsu single horizontal flash, with the low lunging momentum associated with Amakakeru Ryu no Hirameki in Rurouni Kenshin, but entirely Yomi's own identity/costume. Rebuild the pose with coherent anatomy from scratch: facing screen RIGHT, full body, three-quarter side view with face visible. Hips low; torso inclines strongly forward, spine a coherent straight diagonal approximately 25 degrees above the ground, head projects ahead of hips toward right, neck naturally aligned and eyes aiming at the enemy. LEFT leg steps far forward toward right with knee bent over planted left boot; RIGHT leg extends behind toward left, pushing off toes. No splits, no broken knee or ankle. RIGHT hand grips ONE katana hilt naturally with thumb opposing fingers, wrist straight with forearm; right shoulder, bent elbow, forearm and hand form a clearly continuous anatomically plausible chain. Right hand finishes a low fast draw across the body, at lower-chest/waist level, blade extended almost horizontally toward screen right with only slight upward slope. LEFT elbow stays close to ribs and LEFT hand holds the mouth of the EMPTY scabbard firmly at left hip, pulling scabbard backward toward screen left. Blade and scabbard are separate and correctly aligned for a draw; sword has one hilt, one guard, one blade. Hands are separated and unobscured, exactly two arms and two hands. Weight, ribcage and pelvis share the forward momentum, avoid twisting abdomen 180 degrees. Hair and kimono stream behind toward left. Readable compact silhouette of a swordswoman rushing THROUGH the enemy. Do NOT raise sword above shoulder, no overhead pose, no upright torso. All of sword tip, scabbard, hair, both entire boots fully INSIDE a generous transparent margin. Square canvas preferred to accommodate the forward reach; subject occupies lower/middle part, allow empty transparent upper area rather than enlarging the head. Real transparent alpha background, no scene, floor, shadow, text, painted glow, sword trails, motion lines or particles (engine renders the flash).
```

## 前傾を深くする修正プロンプト

参照: 上記初稿。

```text
Use case: identity-preserve. Edit this transparent full-body Yomi game sprite. Keep EXACT face, costume, body proportions, rendering style, single sword and scabbard, right-facing orientation, full boots and generous transparent margins. Correct ONLY the forward-lunging posture and its arm mechanics: make the torso MUCH more forward leaning, nearly horizontal (spine just 15–20 degrees above horizontal), hinging naturally at the hips. Bring ribcage, shoulders, neck and head together forward and DOWN over the leading bent thigh; the head is ahead of the lead knee, chin tucked and eyes aimed right. Keep pelvis low and forward foot planted; trailing leg pushes back. The entire silhouette should feel like launching forward THROUGH the enemy in a single low iaijutsu flash, not posing with a vertical chest. Right upper arm connects visibly and naturally at shoulder, elbow softly bent, wrist neutral, five fingers convincingly gripping the ONE sword hilt. Right hand draws and cuts horizontally forward just below the face at waist height; left hand grips the empty scabbard mouth at left hip pulling it back. Avoid impossible twisting, disconnected arms, shoulder dislocations, fused fingers, duplicated hilts or weapons. Both arms/hands must remain understandable. Hair and cloth stream behind. No effects, no painted trails, no ground or scene. Real transparent background; keep full blade and entire feet inside square frame.
```
