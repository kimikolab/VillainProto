# シオ・回復と強化の支援差分 v1

組み込み ImageGen で作成。回復・通常強化・溢れ強化で共用する右向きの全身差分。
片手を対象へ差し出し、もう一方の手は薬瓶を保持する。背景は透過、葉や光の演出は再生側で重ねる。
音は既存の回復・攻撃力上昇SEを使用する想定。

参照: `DemoApp/assets/portraits/battle/shio_idle_right.png`、`shio_retreat_idle_right.png`。

## 生成プロンプト

```text
Use case: identity-preserve.
Asset type: one production full-body transparent PNG battle-pose variant for Shio, a support character in a 2.5D Japanese fantasy RPG.
Input images: Image 1 is the authoritative character identity, costume and painting-style reference (Shio idle); Image 2 is supporting reference for the same character's movement and outfit construction (vine rescue). Create a NEW support-casting pose, do not copy the vine-pulling gesture.
Primary request: Shio turns in three-quarter profile toward SCREEN RIGHT, looking warmly and attentively toward an off-canvas ally. She leans slightly forward with stable lightly bent legs and extends ONE arm toward SCREEN RIGHT at lower-chest height, elbow nearly straight, open hand palm gently up and outward, fingers relaxed and anatomically clear. The hand and forearm must visibly project beyond the torso silhouette, communicating 'sending healing and strength to this ally'. Her other hand stays near her chest holding her small corked amber medicine vial. A modest reassuring smile, focused assistance rather than panic or attack. Keep the full body including both boots, fingertips, ear tips and the entire large tail inside the canvas, with comfortable clear margins.
Identity invariants: exactly the same chestnut short tousled hair, amber eyes, rounded furry animal ears (NO human ears), youthful adult face, giant broad fluffy brown squirrel tail with pale cream edging, olive green hooded short tunic, cream inner fabric and flowing cream sleeve membranes with brown fur edging, leather chest straps and two waist belts, brown herbal pouches, small hanging amber medicine bottles, leaf and tiny white-flower COSTUME ornaments, dark shorts and brown tights, dark brown boots with straps, buckles and herbal leaf trim. Preserve the same body proportions and polished delicate fine-line, softly painted fantasy anime rendering, materials, colors and detail density. Single same character, not a redesign.
Composition: same slightly elevated 2.5D battle-sprite viewpoint and three-quarter side view as the references, clear readable silhouette, approximately 1024x1536 portrait format. The large tail balances the silhouette on screen left while the extended hand reaches screen right.
Background: genuine transparent alpha everywhere outside the character, including gaps between limbs, tail and clothes. NO colored backdrop, gradient, ground plane or cast shadow.
Avoid: no floating leaves, particles, magic light, rays, glow, aura, circles, trails or visual effects baked into the sprite; these are added by the game. Costume leaf ornaments remain. No vine, staff, weapon, other character, injured ally, text, UI, border or watermark. No duplicated limbs or extra fingers.
```
