# 拾い屋のスス 放つ v3 — 右上へ灰を打ち上げる

生成方法: Codex 組み込み ImageGen。
参照: susu-release-right-v2.png。
袋の口を右上へ向けて灰を打ち上げ、敵陣に降り注ぐ軌道を表現。
生成後、腰布と尻尾付近の描画の乱れを同ツールで修正。
透過PNG。ゲームの放出差分へ採用済み（demo-integration.md参照）。

## 生成プロンプト

```text
Use case: identity-preserve. Edit this Susu imp game attack cutout. Preserve exact character identity, compact four-head proportions, face, hair, horns, lavender skin, amber eyes, purple wings/tail, outfit, mittens, boots, patched canvas sack, unused dustpan tucked at waist, palette and polished anime rendering. Change the RELEASE DIRECTION and related arm/bag/body pose: he explosively flings ALL accumulated ash UPWARD toward UPPER SCREEN RIGHT, into the air above the enemy so it can rain down across their ranks. The sack mouth must point unambiguously 45-60 DEGREES UPWARD TO THE RIGHT, not sideways or downward. Raise the open rim near/above right shoulder height and angle the sack diagonally from lower-left to upper-right. BOTH hands firmly grip the ONE sack, one hand raises its mouth, other supports and forcefully swings its lower rear end upward from the waist. His arms and bag form an ascending diagonal. Sack stays in his hands, partly collapsing as all contents leave. Feet remain firmly planted wide, knees pushing upward with torso straightening into a big upward heave; head tilted up, eyes follow upper-right trajectory, confident exuberant shout. Face unobstructed. A broad clearly ASCENDING plume/fan of soft dry gray ASH erupts from sack mouth toward the UPPER RIGHT corner, expands into a loose high overhead cloud; far upper-right wisps and a few specks start to curl downward, implying ash will rain on distant enemies. Dominant motion is UP, no low horizontal spray, absolutely no discharge toward lower right. Keep lower-right area largely empty of ash. Fine gray powder, not gravel, not water, not fire. Ash should be readable as rising arc with softly translucent edges, not a full background. Preserve all other design details, no second bag, exactly two coherent arms and mittens, no dustpan in hands. Single full-body character with whole horns wings tail boots raised sack and ash arc safely inside canvas. Add enough upper canvas room for ascending plume, use square or portrait composition if needed; do NOT crop cloud or character. Genuine transparent RGBA background and empty gaps, no ground shadow floor environment colored halo vignette gradient checkerboard text labels watermark. Character solid opaque, ash edges softly translucent. Output one revised production battle RELEASE sprite: a powerful upward bag heave showering the enemy from above.
```

## 修正プロンプト

```text
Repair this exact image, preserving the upward sack-dump pose, character identity, face, full composition, colors and ascending gray ash plume. There is an obvious corrupted horizontal band through the lower waist/apron and tail at approximately 70-78% image height: duplicated displaced apron patches, triangular debris, disconnected tail segments. Reconstruct ONLY that corrupted band into a clean coherent single patched apron hanging over trousers, intact smooth single purple tail curving behind left, natural continuous outlines and matching shading. Remove all floating angular cloth fragments and duplicate apron geometry. Exactly one apron, one tail, two arms two legs. Keep the sack raised diagonally toward upper right and all ash flowing upward, same face and pose. Preserve genuine transparent RGBA background and empty gaps; no floor shadow or backdrop. Finish a clean game sprite without any glitch band. Do not redesign or change anything else.
```
