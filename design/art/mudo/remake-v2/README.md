# ムド・濡れた泥人形へのリメイク案

2026-10-27。ユーザーの「現在の絵は泥人形に見えず、泥が跳ねそうな感じがない」という指摘を受けたコンセプト案。

## 現在の既定デザイン

- 最新案：`mudo-wet-mud-round-mouth-v3.png`（透過PNG）。
- ユーザー指定：口は埴輪やマリオのヘイホーのような、ぽかんと開いた丸い穴を通常表情にする。
  歯・舌・唇は付けず、泥に開いた暗い穴として描く。今後の立ち絵・待機・動作差分でもこの口を基準にする。
- v2を編集対象として組み込み `image_gen` で口の差分を生成。湿った泥の身体・目・構えを維持する。
- v2は比較用に保持。以下はv2制作時の記録。

- 出力：`mudo-wet-mud-concept-v2.png`（透過PNG）。現行のゲーム内画像はまだ差し替えていない。
- 方向：焼き物の硬い外殻・球体関節・装飾帯をなくし、湿った泥がつながった身体へ。
  垂れる前腕、たわむ腹、指跡と折れ目、滴、足元の泥溜まりで柔らかさと重さを伝える。
- 生成：組み込み `image_gen`。参照は `DemoApp/assets/portraits/battle/mudo_idle_right.png`。
  参照からは画風・全身の収まり・右向きの構えを取り、材質と身体構造を作り直した。
- 元の生成ファイルはCodexの生成画像フォルダーにも保持。

## v2 使用プロンプト（全文）

Use case: stylized-concept. Asset type: redesigned dark-fantasy tactical-game character concept and full-body battle sprite, named Mudo, a living mud doll. Input image 1 is a reference only for the polished hand-painted outlined fantasy game illustration style, full-body framing and right-facing three-quarter battle stance. Fundamentally redesign the character's construction and material. The present design looks like a fired terracotta robot; the redesign must clearly be a person-shaped mass of WET, SOFT, SPLASHABLE MUD.

One character only, full body from head to soles, isolated on genuinely transparent background, generous transparent margin, no text or frame. Three-quarter view facing screen-right, slight forward hunch with loose, heavy arms ready to lash out. Broad squat silhouette, lopsided rounded head sunk into sloping shoulders, two simple uneven hollow eyes and a small crooked mouth indentation; an awkward, long-suffering, quietly angry living handmade doll, expressive without a human face. Its body is continuous saturated dark earthen-brown sludge: drooping heavy forearms and blunt oversized fists, sagging belly, folds and finger marks pushed into wet clay, soft creases where elbows and knees bend, strands of sticky mud connecting the creases. Legs taper into broad squashed muddy feet that merge with small attached puddles. A few thick droplets hanging from the elbows, fists and abdomen, one or two small droplets separating near the hand, enough to imply splashing when it punches. It must remain a legible humanoid, not a featureless slime blob. Rich dark brown/umber mud with warm ochre highlights on thick ridges and cooler dark wet recesses; restrained glistening wet highlights that communicate moist sticky mud, not shiny metal or rubber. Large readable shapes rather than noisy granular detail. The visible shoulder and forearm surfaces visibly slump under gravity, never a hard shell. No armor plates, no geometric limb sections, no ball joints, no wrist or ankle bands, no carved rune chest panel, no mechanical parts, no stone boulders, no dry terracotta, no baked pottery cracks. No clothing, weapons or extra props. Stylized hand-painted dark-fantasy RPG art, confident contour lines, painterly shaded volume, consistent lighting from upper-left, suitable for compositing into a 2.5D battle. Keep head, hands, feet, attached mud puddles and every droplet fully inside the frame. Do not include a green screen or any colored background.
