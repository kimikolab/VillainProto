# 移り木のシオ 戦闘待機・右向き v2

組み込み ImageGenで右手に薬瓶を持つ形を生成し、右腕・袖の周辺のみをv1に合成。
範囲外の画素はv1と完全一致。合成手順は `compose_shio_battle_v2.py`。

## Prompt

```text
Localized edit of this exact Shio battle idle character. Change ONLY her RIGHT arm/hand (the empty outstretched hand on VIEWER LEFT) and its immediate sleeve folds. Instead of an open palm reaching for a handshake, bend the elbow comfortably and turn the forearm upward/inward, holding a small corked amber glass medicine bottle at lower-chest/upper-waist height. The bottle is the same teardrop herbal medicine vial design already hanging on her belt, palm-sized, filled with golden amber liquid; fingers wrap naturally around its body, thumb stabilizes it, bottle upright. A relaxed ready-to-treat healer stance, not presenting a gift, no reaching open palm. Preserve the sleeve's cream membrane and brown fur trim; adapt only the folds necessary for the bent arm. Preserve EVERYTHING else as precisely as possible: face, expression, hair, animal ears with no human ears, left hand and its herbal dressing bundle on viewer right, torso, clothing, waist pouches, huge tail, legs, boots, pose, proportions, crop, original color values and texture. No global color grading, no darkening or additional grain. Same polished fine-line fantasy anime rendering. Genuine transparent background with clean alpha including all gaps, no glow or backdrop. Full original canvas, one character only, no text.
```
