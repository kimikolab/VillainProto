# ベニ 鬼娘版 本番立ち絵 v1

生成: Codex 内蔵 image_gen。銀髪・白い聖衣・血赤と深紫・燃える毒花を持つ鬼娘ヒーラー。
正面版は承認済みコンセプトから、戦闘版は正面版を参照して生成。
角の根元を額の左右の生え際に合わせ、正面版では頭の傾きを抑えた。

## 出力

- `beni-front-v1.png`: 正面待機。`DemoApp/assets/portraits/beni.png` に同一ファイルを配置。
- `beni-idle-right-v1.png`: 右向き戦闘待機。`DemoApp/assets/portraits/battle/beni_idle_right.png` に同一ファイルを配置。
- どちらも 1024 × 1536、RGBA PNG。空白部分の alpha=0 を確認。内部の代表点は alpha=253（生成原本のまま）。ゲーム画面上の確認は未実施。
- `UiKit` の既存ファイル名規約で読み込まれるため、コード変更なし。

## 正面待機プロンプト

```text
Use case: identity-preserve. Production FRONT STANDBY full-body TRANSPARENT PNG game portrait of Beni, the exact adult silver-haired oni fallen saint in the reference. Preserve approved identity and outfit: pale clean skin, beautiful mature anime face, half-lidded dull ruby eyes without bright catchlights, subtly troubled faint smile, long flowing silver hair, white fitted saint bustier, exposed shoulders and midriff, black fitted shorts, long white split skirt panels and detached draped sleeves with blood-red AND deep plum-purple lining, sparse black-purple flowers glowing red at their cores, black thorn tracery, thigh straps, dark ankle boots. Preserve amount of skin exposure, no extra revealing changes. Crisp anime outlines and controlled cel-shaded illustration matching reference. Simplify minute filigree for small game readability. Important correction: head upright nearly frontal, tiny turn only, do not tilt head sideways. TWO elegant red oni horns anchored bilaterally at the same anatomical height on upper frontal skull near left and right hairline, roots symmetrical about nose bridge, both above outer eyebrows and visibly emerging from skin at hairline, bangs part naturally around roots. NOT horns pasted on hair. Equal length and coherent perspective, ivory-pink bases to crimson tips. They angle slightly outward and upward in a balanced pair, separate from thin black thorn halo BEHIND head, avoid overlaps between horns and halo where possible. Calm frontal standing pose, relaxed weight on one leg, feet stable. One hand holds a single black-purple burning flower at upper chest, other open gently at waist toward viewer. Keep fire small and contained around blossom, no large detached particles. Complete horns halo hair sleeves fingers skirt and both boots inside portrait frame with at least 6 percent transparent padding on every side. Genuine transparent RGBA background including gaps between hair strands, arms and body and inside halo. No gray background, no white backdrop, no floor, no ground shadow, no haze, no checkerboard painting, no text, no watermark. Opaque character interiors. One character only.
```

## 戦闘右向きプロンプト

```text
Use case: identity-preserve. Generate BATTLE IDLE facing SCREEN RIGHT from this exact adult oni healer Beni reference. Same character, costume and crisp anime game illustration style. Face, gaze, shoulders, torso, hips and BOTH feet oriented toward an enemy off-screen RIGHT, three-quarter side view, not looking at viewer. Calm ominous healer ready stance, slim staggered balanced feet at same floor baseline, knees relaxed, no attack motion. Near hand holds the same black-purple burning flower before chest toward screen right, far hand extended a little forward at waist as if ready to bless. Long silver hair and long split white robe panels fall and drift subtly toward screen LEFT behind her; keep face and limbs readable. Preserve beautiful adult face, half-lidded lifeless dull ruby eyes, faint troubled smile, pale clean skin, white fitted bustier, bare shoulders and midriff, black fitted shorts, exposed thighs with black straps, detached long white sleeves and robe panels lined BOTH blood red and deep plum purple, purple-black flowers with ember-red cores, black thorn tracery, dark ankle boots. TWO red oni horns at bilaterally correct matching frontal hairline positions attached to skull, ivory-pink base crimson tip, projected correctly for right-facing 3/4 turn: far horn partly occluded naturally, roots follow head orientation, neither horn grows from face or floats on hair. Thin dark thorn halo behind head projected as an ellipse for the 3/4 angle. Preserve garment design and exposure, no redesign, no weapons added, no wings or tail. Full body, complete horn tips, halo, hands, hair, hem and both boots within image, at least 6 percent margin all sides. TRUE transparent RGBA background, isolated clean cutout. Absolutely NO diffuse background glow or shadow or haze or dark vignette, including near glowing flowers. Fire limited to tiny crisp flame directly on flower; all empty space must have alpha zero. Character white clothing and skin stay fully opaque. No floor, checkerboard, text or watermark. Simplify tiny filigree to match legible existing game sprites.
```
