# 粛の伝令 — 砕けた後 v2

- 作成日: 2026-10-10
- 生成: 内蔵image_gen。ComfyUIは未使用。
- 参照1: `DemoApp/assets/portraits/battle/husher_idle_right.png`（人物・立ち姿の体格）
- 参照2: `husher-shattered-right-v1.png`（顔・羞恥の表情）
- 原本: `husher-shattered-right-v2.png`
- ゲーム素材: `DemoApp/assets/portraits/battle/husher_shattered_idle_right.png`
- 1024×1536、RGBA透過。v1原本は保存。
- 追加指示: 普段着程度の破損から、法衣と鎧が弾けて下着同然の状態へ。うずくまらず、羞恥に耐えて体を隠しながら戦える立ち姿へ。
- 片腕で胸元の布を押さえ、剣を低く構える。通常時の身長を保ち、足元余白を0.0234に調整。

## 確認

- RGBA、画像端と背景のアルファ0を確認。
- DemoAppビルド成功（エラー0。今回未変更のBattleCore側にCS0162/CS8602の警告2件）。
- Godot再インポート成功。左右×1倍/2倍の4条件で `HUSH_VISUAL_OK`、最終 `SHOCK_MARK_CHECK_OK`。
- 戦場と拡大表示で立ち姿・剣・足元・透過を確認。画像は `.tmp/hush/art-v2-images/`。

## 使用プロンプト

```text
Use case: identity-preserve.
Asset: revised transparent full-body battle sprite, portrait 1024x1536.
Image 1 is the original character identity and standing proportions reference. Image 2 is the edit target: keep her successful mature face, plum-black bob, blush, teary embarrassed expression and clean cel-shaded anime drawing style, but change her pose and damaged costume as follows.
Subject: the SAME adult female herald, late twenties. Her magical armor and ceremonial robe have burst apart, but she still has to fight. Lighthearted fantasy battle slapstick.
Change the crouch to a FULLY STANDING, combat-capable posture facing SCREEN RIGHT in three-quarter view. Both feet planted, one half-step ahead, knees only slightly bent, shoulders defensively drawn in. She endures her embarrassment with flushed cheeks, wet eyes and a tense open mouth, brows retaining some of her original stern defiance. She is not sitting, kneeling or squatting.
Costume must look MUCH more destroyed than image 2: the intact undershirt, shorts, trousers and long robe are gone. She wears simple opaque ivory full-coverage bra and opaque matching ordinary briefs, clearly underwear, with no lace or transparent fabric. Bare midriff and thighs visibly establish the loss of her uniform. Keep her remaining armored boots, one damaged vambrace, a few shredded ivory/gold-edged cloth strips and charcoal-violet mantle scraps. Broken circular gold brooch on a tiny torn shoulder remnant preserves her herald identity. Her LEFT forearm and a clutched scrap cross her chest protectively; the chest stays fully covered. A short ragged tabard remnant attached at the waist hangs over the front of her briefs, adding her attempt to cover herself; do not make this a whole skirt. All intimate areas stay covered by opaque underwear.
Her RIGHT hand still grips her original slim straight sword, held low in front in a guarded but functional position, blade angled down toward screen right. She can lift it for her next attack. Keep the entire sword tip in frame. Natural two arms and hands. No voluptuous exaggeration, no pinup arch, no erotic focus, no nudity, no injury or blood.
Show a few small broken silver armor pieces and sharply torn cloth strips near her boots. Otherwise a clean silhouette. Same adult body proportions as original standing reference.
Full figure with entire hair, boots, sword and scraps inside safe margins, about 4% margin top and bottom. Neutral eye-level full-body composition, readable at small game size. Genuine transparent RGBA background including all gaps; no backdrop, floor, haze, shadows or painted checkerboard. No text, speech bubbles or watermark.
```
