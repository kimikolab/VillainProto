# 敵の顔: 弓兵（狙撃手）（`archer`）

題材 1（指示書 §5-1）。`gen --job foe_face/archer --n 3 --size 1024x1024`。採用したら `DemoApp/assets/portraits/enemy/archer_face.png`。
陣営は王国の兵（人間・手入れされた装備・青と銀）。味方（髑髏・赤い光・継ぎはぎ）と並べて陣営の違いが分かること。兵種は兜と武器の形で分ける。

```text
Composition: square head-and-shoulders bust that will be shown as a tiny 40-pixel round unit icon, so the
silhouette of the headgear and one weapon must read at a glance. The head fills about 60% of the frame height
and is centered; the top of the headgear and both shoulders are inside the frame. Three-quarter view with the
face and gaze turned toward SCREEN LEFT. Background: one flat, even, dark slate-grey fill (#2A2C33), nothing else.
Faction: a human soldier of an orderly kingdom's punitive army. Clean, maintained, issued equipment in a
blue-and-silver livery: polished steel, deep royal-blue cloth, small silver trim. Human skin, calm disciplined
bearing. No skulls, no red glow, no stitches, no rust, no monster features.
Subject: a sharpshooter archer, a focused woman around 28 with narrowed eyes. A deep royal-blue hood pulled up over a light open steel skullcap, a blue half-cloak, a silver feather pin at the collar. The upper limb of a longbow crosses the frame diagonally beside her face and the fletched ends of arrows in a quiver show above her shoulder.
```
