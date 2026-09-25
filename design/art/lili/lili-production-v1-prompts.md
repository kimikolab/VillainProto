# リリ 本番立ち絵 v1

生成: Codex 内蔵 image_gen。承認済みコンセプトを参照して正面版を生成し、正面版を参照して戦闘右向き版を生成。

## 配置・検証

- `lili-concept-v1.png`: 承認済みコンセプト。
- `lili-front-v1.png`: `DemoApp/assets/portraits/lili.png` に同一ファイルを配置。
- `lili-idle-right-v1.png`: `DemoApp/assets/portraits/battle/lili_idle_right.png` に同一ファイルを配置。
- 本番2枚は1024 × 1536、RGBA PNG。角と左端中央の alpha=0、中央の代表点 alpha=253。生成原本の透過を保持。
- 配置先と原本の SHA256 一致を確認。`UiKit` の駒IDによる既存規約で読み込む。コード変更なし。
- ゲーム画面上の表示確認は未実施。

## 正面待機プロンプト

```text
Use case: identity-preserve. Create production FRONT STANDBY transparent full-body game portrait of the EXACT approved adult succubus nun Lili in reference. Preserve face, gentle half-lidded warm smile, dusty rose beige long wavy hair, black curved horns, pointed ears, black nun veil with ivory forehead band, fitted black nun dress with ivory high collar and chest opening, grape-purple lining, long draping sleeves, worn patched hems, single high slit, silver droplet-petal emblems, prayer beads, lace-up boots, slender tail with adorned teardrop tip. Preserve same tasteful exposure and proportions. Nearly frontal calm standing pose, head upright with slight gentle tilt, both feet grounded. Her left hand holds silver alms chalice at chest, right hand open palm-up offering toward viewer. No attack motion. Reduce magic to a tiny contained rose light INSIDE cup; no floating ribbons or particles. Fine clean anime outlines and controlled soft cel shading as reference, simplify tiny cloth ornament for game readability. FULL body including horns veil tail fingers and both boots with 6 percent clear margins all sides. Genuine transparent RGBA background, empty space alpha zero, including enclosed gaps. Character interior opaque. NO gray background, vignette, haze, glow outside cup, ground shadow, floor, checkerboard, text or watermark. One character only.
```

## 戦闘右向きプロンプト

```text
Use case: identity-preserve. Production BATTLE IDLE facing SCREEN RIGHT, full body transparent PNG of EXACT adult succubus nun Lili in reference. Same identity face clothing colors proportions and exposure. Rotate pose: face gaze chest hips and BOTH feet oriented screen RIGHT in three-quarter side view, eyes toward offscreen right, NOT viewer. Gentle half-lidded warm smile, calm healer ready stance, feet slightly staggered on common baseline. Left hand holds same silver alms cup close to chest, right hand extended palm up a little toward screen right ready to bestow blessing, no attack motion. Preserve dusty rose-beige wavy hair, black backward-curved horns correctly attached to head, pointed ears, long black nun veil and ivory forehead band, ivory collar, fitted black dress with chest opening, purple grape lining, long sleeves, silver teardrop-petal emblems, prayer beads, single side slit, lace-up boots and slender ornate teardrop-tipped tail. Hair and veil trail gently to screen LEFT, don't obscure face or cup. Worn hems as reference. Fine clean anime outlines and soft controlled cel shading, small-size readability. Only tiny rose light contained inside chalice, NO floating magic effects. Entire character horns veil fingers tail dress hem and both boots fully in portrait frame with 6 percent margins. GENUINE TRANSPARENT RGBA background: all empty spaces alpha ZERO including hair gaps and tail loop. Opaque character interior. NO background color, diffuse glow, vignette, haze, floor, shadow, painted checkerboard, text or watermark. One character.
```

