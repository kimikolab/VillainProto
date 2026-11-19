# 第482期 観察ログ —— 素材の道具で作った候補の絵

記入はポン。問いは指示書 `design/PHASE482_ART_TOOL_SPEC.md` §8。見るもの: 見比べの絵 4 枚

- `design/art/_candidates/foe_face/sheet.png`（敵の顔 6 種・今の顔も並べてある）
- `design/art/_candidates/saint/sheet.png`（聖女）
- `design/art/_candidates/gan_quiet/sheet.png`（ガンの静かな版）
- `design/art/_candidates/courier/sheet.png`（伝令）

候補の番号は、見比べの絵の下の札の頭の数字。Claude Code が外した絵と理由は報告 `design/PHASE482_ART_TOOL.md` §4。

## 問い

1. 絵柄は今の立ち絵と並べて合っているか

2. 敵の顔は兵種の見分けがつくか・味方と陣営の違いが分かるか

3. 採用するもの（題材ごとに番号）・作り直したいもの

   | 題材 | 採用する番号 | 作り直したい所 |
   |---|---|---|
   | 徴募兵 `levy` | | |
   | 物見の兵 `recruit` | | |
   | 傭兵崩れ `drifter` | | |
   | 騎士 `knight` | | |
   | 弓兵 `archer` | | |
   | 砦の守将 `champion` | | |
   | 聖女 | | |
   | ガンの静かな版 | | |
   | 伝令 | | |

4. 次にこの道具で作りたい絵

## 採用した後の置き場（置くだけで出る）

| 絵 | 置く所 |
|---|---|
| 敵の顔 | `DemoApp/assets/portraits/enemy/<駒の id>_face.png` |
| 聖女・伝令 | `design/art/story/聖女.png`・`design/art/story/伝令.png` |
| ガンの静かな版 | `DemoApp/assets/portraits/battle/gan_静_right.png`（台本に `ガン:静｜左｜文` と書いた行で出る） |
