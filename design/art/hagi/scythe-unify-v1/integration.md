# ハギ 大鎌の穴数を統一

適用日: 2026-10-25
生成方法: Codex 組み込み ImageGen

ユーザー指摘: 正面の大鎌の根元には大きな丸穴が1つ、横向きには2つあり、同じ武器に見えない。

正面の1つ穴を正とし、横向きの下側の余分な丸穴を閉じて、暗い金属の地と金のリブで補った。正面画像は変更していない。キャラクター・握り・衣装・元の配色・スカート丈・刃の外形は維持。

## 現在の採用画像

- 通常: ../fresh-redraw-v1/hagi-standing-fresh-v1.png → DemoApp/assets/portraits/hagi.png（変更なし）
- 戦闘: hagi-battle-idle-right-one-hole-v1.png → DemoApp/assets/portraits/battle/hagi_idle_right.png

変更前の戦闘絵は originals/hagi-idle-right-before-hole-unify.png に保存。旧画像は2つ穴のため、現在の採用元には使わない。

## 確認

- 1024×1536の透過RGBAを維持、上側の穴も透明。
- 足元のアルファ128以上の下端は1506pxのまま。余白30pxと表示高2.25は変更不要。
- 原本と適用画像のSHA-256が一致。正面画像のSHA-256は変更前と同じ。
- 実際のMain.tscnで編成・戦闘・燃焼解除・勝利時のテクスチャ参照を確認し、画面を保存。
- HAGI_SCYTHE_ONE_HOLE_GAME_CAPTURE_OK selection battle burning victory source_hash height=2.25 foot=0.04999997199047
- ゲーム終了時には既存のCanvasItem / ObjectDB / FontAdvanced解放警告が残る。
- 画像差し替えのみ。表示コード・戦闘ロジックは今回変更していない。

画面: selection-game-check.png / battle-game-check.png / victory-game-check.png
ハッシュと寸法: manifest.json
再確認用: capture-integration.gd

## 修正プロンプト

```text
Use case: precise-object-edit.
IMAGE 1 is the SIDE / RIGHT-FACING BATTLE PORTRAIT to edit. IMAGE 2 is the FRONT NORMAL PORTRAIT, authoritative for the scythe's design.
Make ONE tiny localized weapon correction: the battle scythe mistakenly has TWO LARGE ROUND/OVAL CUTOUT HOLES in the gold-and-black blade root, while the front scythe has ONE. Match the front scythe.

At the UPPER RIGHT blade root of image 1, preserve the UPPER primary large round opening beneath the upper pointed gold crest. REMOVE/CLOSE the SECOND LOWER OVAL HOLE immediately below that opening, closer to the shaft mounting socket. Fill that lower oval with a continuous solid dark forged-metal structural plate/spine and the same fine gold ribs/trim arrangement as image 2. Exactly ONE large circular/oval root opening remains. Do not remove the upper intended hole or just hide a hole with a floating ornament. Do not add another hole, ring or gem. Keep small decorative gold openwork consistent with the front reference. Scythe root must now be the SAME weapon as image 2, seen at its current battle angle.

Preserve EVERYTHING ELSE from image 1 exactly: huge silver crescent outline, blade shape and three etched tally marks, clean neutral silver/no blood, gold details away from that tiny lower-hole patch, perfectly straight shaft, both grips and finger anatomy, arms, face/expression, hair, all soft garment folds and colors, skirt length/coverage, stocking, boots, stance and framing. Do not move scythe, hands, feet, cloth or head. No entire-image mirroring or redesign. No new bells, chains, purple, white-cloth recoloring or other variant features.
One full-body 1024x1536 transparent RGBA image, same dimensions and placement, preserve genuine alpha and gaps. No background, halo, vignette, floor shadow, labels or watermark. This is only a TWO-HOLES-to-ONE-HOLE correction on the blade root.
```
