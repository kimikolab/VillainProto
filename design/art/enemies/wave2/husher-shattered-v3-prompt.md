# 粛の伝令 — 砕けた後 v3

- 作成日: 2026-10-10
- 生成: 内蔵image_gen。ComfyUIは未使用。
- 編集元: `husher-shattered-right-v2.png` と同一のゲーム用PNG。
- 保存原本: `husher-shattered-right-v3.png`
- ゲーム素材: `DemoApp/assets/portraits/battle/husher_shattered_idle_right.png`
- 1024×1536、RGBA透過。v1/v2原本も保存。
- 修正内容: 剣と余分な腕を除去し、二本の腕・二つの手へ修正。胸元と腰の既存の布を押さえる。顔・表情・衣装の破れ方・立ち姿を維持。
- ゲームの平手攻撃演出は既存のまま使用。画像修正の保留を解消。

## 確認

- 原本とゲーム用PNGのSHA-256一致。背景のアルファ0を確認。
- Godot再インポート成功。戦場と拡大表示で剣が消え、二本の腕になった差分を確認。
- 左右×1倍/2倍の4条件で `HUSH_VISUAL_OK`、最終 `SHOCK_MARK_CHECK_OK`。平手攻撃・蘇生・再戦の既存検証も合格。
- 撮影は `.tmp/hush/art-v3-images/`、ログは `.tmp/hush/art-v3-visual-console.log`。

## 使用プロンプト

```text
Use case: precise-object-edit.
Edit the supplied adult fantasy game character sprite with only two corrections:
1. Remove the entire sword, including blade, hilt and guard.
2. Correct the duplicated arm anatomy. The character must have exactly two arms and two hands total, each naturally connected to one shoulder. Remove the extra sword-holding armored arm on the viewer's left. Reconstruct the two remaining arms clearly: one hand continues holding the existing cloth at her chest, and the other hand rests over the existing hanging cloth at her waist. Both arms have natural elbows and wrists, and all fingers are anatomically consistent.
Preserve the existing character identity, face, expression, hairstyle, standing pose, proportions, costume design, amount of coverage, cloth damage, colors, armored boots, scattered fragments and cel-shaded illustration style. Do not add exposure or change the clothing. No other creative changes.
Preserve the full-body portrait framing and genuine RGBA transparency. No background, text, watermark, extra limbs, duplicate hands or weapons.
```
