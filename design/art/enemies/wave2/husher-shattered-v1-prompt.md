# 粛の伝令 — 砕けた後 v1

- 作成日: 2026-10-10
- 生成: 内蔵 image_gen（ComfyUIは未使用）
- 参照: `DemoApp/assets/portraits/battle/husher_idle_right.png`（同一人物・画風）
- 保存原本: `husher-shattered-right-v1.png`
- ゲーム素材: `DemoApp/assets/portraits/battle/husher_shattered_idle_right.png`
- 1024×1536、RGBA透過。通常絵は既存のまま。砕けた後の戦場表示と拡大表示で共用。
- 成人のコミカルな赤面・涙目・悲鳴。透けない肌着と衣装の残骸で体を覆う。法具と飛散する破片は既存のコード演出を併用。

## 組み込み確認

- Godot再インポート成功。左右×1倍/2倍の4条件で `HUSH_VISUAL_OK`、最終 `SHOCK_MARK_CHECK_OK`。
- 実描画の戦場と拡大表示で透過・向き・全身の収まりを確認。撮影は `.tmp/hush/art-images/`。
- `compare` / `dump` と既存生成物の差分は0。戦闘規則・数値の変更なし。

## 使用プロンプト

```text
Use case: identity-preserve.
Asset type: transparent character sprite for a Japanese fantasy autobattler, full adult figure, portrait 1024x1536.
Input image 1: character identity, costume colors and drawing style reference for the SAME character in a new pose. Do not reproduce its dark background.
Primary request: Create the post-shatter state of this adult woman, a stern herald of silence in her late twenties whose magical armor and outer ceremonial robe have comically fallen apart. She is now crouching, bright red-faced, teary-eyed, mouth open in a startled comic cry. This is lighthearted fantasy slapstick, not an erotic scene.
Identity invariants: same mature angular oval face, narrow almond eyes, same chin-length dark plum-black bob with side tucked behind ear, same adult body proportions. Match the reference's clean dark anime outlines, restrained colors, broad simple cel shading and modest highlights. Do not make her a child or chibi.
Pose: three-quarter view facing SCREEN RIGHT, crouched low with knees together and shoulders hunched, one forearm protectively across upper torso holding a remnant of her ivory gold-edged tabard, the other hand clutching the cloth over her lap. Face remains fully visible and large enough to read in a small game sprite. Compact, readable silhouette; anatomically natural arms, hands and legs.
Clothes: outer plate armor and long tabard are gone except small loose ivory/charcoal-violet remnants. Wear fully opaque, simple ivory sleeveless under-shirt with a modest neckline and plain high-waisted mid-thigh under-shorts, practical medieval underclothes, no lace or sheer material. Chest and pelvis completely covered by clothing. Keep charcoal stockings and damaged silver armored boots from the reference, and the broken gold circular throat brooch pinned to the cloth she clutches. Small detached silver plate fragment and torn ivory gold-trim cloth beside her feet only. No sword held, no injury or blood.
Composition: one whole character with all hair, elbows, knees, boots and cloth completely inside frame with safe clear margins. Fill most of the canvas with the crouched figure, centered. Eye-level character presentation, no suggestive camera angle, no emphasis on breasts or buttocks.
Background: genuine fully transparent RGBA including all gaps, no floor, no shadow, no gray or black haze, no vignette, no scenery, no checkerboard painted into the image.
No text, speech bubble, lettering, label, watermark, extra characters or large effects.
```
