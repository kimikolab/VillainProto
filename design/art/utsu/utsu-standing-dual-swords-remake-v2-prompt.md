# 逆しまのウツ 双剣士・通常立ち絵 v2

生成日: 2026-10-27
生成方法: Codex 組み込み ImageGen（CLI 不使用）
出力: `utsu-standing-dual-swords-remake-v2.png`（1024×1536、透過PNG）
状態: 2026-10-27にゲーム適用済み。適用と検証は `integration.md` を参照。
参照: `utsu-standing-dual-swords-remake-v1.png`。

ユーザー指定により画面右の剣を下向きへ変更。左の剣と同様に、両手で剣を下へ垂らす持ち方とし、右手首と握りを調整した。顔・衣装・脚の姿勢を維持するよう指示。両剣の下向き、握り、刃先の収まりを目視確認。RGBA形式と背景のアルファ0を確認。旧版・戦闘用立ち絵・ゲームの参照先は変更していない。

## 最終プロンプト

```text
Use case: precise-object-edit.
Asset type: transparent full-body ordinary standing portrait of Utsu, small targeted revision of the approved image.
Input image 1 is the EDIT TARGET. Keep the entire character portrait as unchanged as possible. Change ONLY the SCREEN-RIGHT sword, its grip orientation, and the minimum necessary wrist/hand adjustment.
Requested edit: the sword in the hand on the viewer's RIGHT currently points upward toward the upper-right. Lower and rotate this sword so its blade hangs DOWNWARD and slightly outward toward the lower-right, mirroring the relaxed downward direction of the existing sword on the viewer's LEFT. Both swords are carried loosely lowered at his sides, with both tips aimed toward the ground. Same straight steel blade length and thickness, tarnished brass guard, dark wrapped grip and pommel. The screen-right hand securely grips the handle above the guard, blade emerges below the guard, connected in a single correct straight axis. Adjust the wrist naturally for ordinary forward-grip tip-down carry, not a reverse icepick grip. Keep the arm hanging naturally at its existing position and the sword clearly outside the leg silhouette. Keep the tip inside the canvas; maintain natural sword length, shifting only minimally if needed.
Mood: lowering BOTH swords reinforces his tired, subdued, melancholy withdrawn character. Preserve the exact original face and expression.
Invariants: keep the screen-LEFT sword, left-side arm/hand, face, hair, gaze, body proportions, entire standing pose, both legs/feet, navy short torn half-cape, ivory tabard, leather straps/pouches, silver asymmetric armor, colors, linework, rendering and composition unchanged. Do not redesign the costume, change the silhouette elsewhere, or turn him to face right. This is the ordinary portrait, not the battle stance.
Remove the former upward blade completely, leaving true transparent alpha in its old position. Preserve true transparent background. No halo, background, floor, cast shadow, text, effects, extra weapons or extra fingers. Exactly two swords, both pointing downward, both sword tips fully visible.
```
