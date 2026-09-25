# スペシャルの立ち絵

内蔵 `image_gen` による既存立ち絵の編集。元絵は置き換えず、以下の差分を追加した。
描画は既存の `UiKit.BattlePortrait` と透過・背景除去シェーダーを使う。

- `beni_release_idle_right.png`: 紅蓮を放つ間だけ使用。
- `gald_sword_idle_right.png`: `LastStand` 以後の構え。
- `gald_kneel_idle_right.png`: `LastStandVictory` の後の死亡姿勢。

## 使用したプロンプト

### ガルド・剣

Reference/edit target: `gald_idle_right.png`.

Edit target: this game knight sprite. Create ONE full-body sword stance variant for the exact same knight, same helmet, weathered plate armor, proportions, maroon torn cloth and hand-painted anime game style. He has discarded his shield: NO SHIELD anywhere. He steps forward facing right, gripping a drawn longsword ready for a horizontal slash. Pale platinum-white light escapes cracks in chest and pauldrons (not red). Full helmet, no face. Entire boots and sword visible with 4 percent empty margin. Isolated on solid chroma green #00ff00 background to match original game's shader. No floor, no text, no frame. Maintain the original character identity strictly.

### ガルド・膝つき

Reference/edit target: `gald_idle_right.png`.

Edit target game knight sprite: same exact worn silver full plate armor, same closed angular helmet and maroon torn tabard, same hand painted anime illustration. Create final kneeling pose, facing right: exhausted knight on one knee, head bowed, both gauntlets resting on hilt of longsword planted vertically in ground. NO SHIELD. Very faint platinum light in armor cracks. This is a quiet final sacrifice, not celebration. Isolated transparent background, no scenery, no ground shadow, no text. Entire body and sword tip visible, 4 percent margin. Keep original character identity.

### ベニ・紅蓮

2026-09-25: 内蔵 `image_gen` で腕・手を修正し、同じファイル名へ差し替えた。花を持つ腕の肩→肘→手首を連続させ、伸ばした手は指を揃えた形に変更。胸の下の余分な肌色部分を除去。以下は初版の記録で、採用した修正プロンプトは末尾に記載。

Reference/edit target: `beni_idle_right.png`.

Edit target: same exact adult oni woman character game sprite, same silver long hair, red horns, thorn halo, white crimson and purple robes, black-purple flowers. Full body facing right. Special attack pose: raises black purple flower in left hand close to lips, right arm extended sweeping outward toward right, sleeves and hair flowing with gesture, intense crimson light inside flower. Only a few tiny flaming red petals immediately near flower, main effects will be rendered by game. Keep same detailed anime painting and character identity, outfit unchanged. Isolated transparent background, no scenery or text. Include entire boots and sleeves, 4 percent margin.

### ベニ・手と腕の修正（採用版）

Reference/edit target: 初版の `beni_release_idle_right.png`.

ANATOMY CORRECTION, redraw both arms entirely rather than retaining their current contours. This adult oni woman has a BROKEN THREE-ARM appearance in source. Preserve face, hairstyle, horns, halo, flower, legs and overall costume identity. Give her exactly two arms with visibly correct connections: ARM A starts at the bare shoulder on IMAGE LEFT, upper arm descends to an exposed elbow at IMAGE LEFT waist (x about 57%, y 30%), then its bare forearm crosses DIAGONALLY UP across the front of her chest to the flower at her lips (x about 69%, y 15%). Show this entire continuous bent arm and elbow clearly, sleeve hanging below elbow, NOT a vertical forearm at the right edge of her body. Hand grips flower stem normally. ARM B starts at her far shoulder on IMAGE RIGHT and extends toward image right; end it in a simple graceful SIDE PROFILE HAND, four parallel gently curved fingers held together, a single visible thumb, NOT spread fingers, NOT a branching starfish. Remove the extra skin fragment below bust. There are TWO hands total. White bodice is continuous under the crossing forearm. Match existing anime painterly style. Full body same framing and scale. Transparent background. No text. Prioritize readable believable shoulder-elbow-wrist anatomy above preserving broken original pose.
