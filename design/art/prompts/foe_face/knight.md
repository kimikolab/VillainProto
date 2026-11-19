# 敵の顔: 騎士（巡礼騎士）（`knight`）

題材 1（指示書 §5-1）。`gen --job foe_face/knight --n 3 --size 1024x1024`。採用したら `DemoApp/assets/portraits/enemy/knight_face.png`。
陣営は王国の兵（人間・手入れされた装備・青と銀）。味方（髑髏・赤い光・継ぎはぎ）と並べて陣営の違いが分かること。兵種は兜と武器の形で分ける。

```text
Composition: square head-and-shoulders bust that will be shown as a tiny 40-pixel round unit icon, so the
silhouette of the headgear and one weapon must read at a glance. The head fills about 60% of the frame height
and is centered; the top of the headgear and both shoulders are inside the frame. Three-quarter view with the
face and gaze turned toward SCREEN LEFT. Background: one flat, even, dark slate-grey fill (#2A2C33), nothing else.
Faction: a human soldier of an orderly kingdom's punitive army. Clean, maintained, issued equipment in a
blue-and-silver livery: polished steel, deep royal-blue cloth, small silver trim. Human skin, calm disciplined
bearing. No skulls, no red glow, no stitches, no rust, no monster features.
Subject: a full knight in a closed great helm: polished silver-steel helm with a narrow eye slit and vertical breathing slots, a tall royal-blue horsehair plume on the crest. Bright steel gorget and layered pauldrons with silver edging, a royal-blue surcoat collar. The face is fully hidden by the helm. The upper edge of a kite shield with a plain blue field and silver border shows at the lower corner.
```
