# ヒサ：庇い差分

初版の生成記録。脚と体勢の修正依頼を受け、ゲーム内素材は `hisa-cover-v2-prompt.md` のv2へ差し替えた。

生成：組み込み imagegen。参照：`DemoApp/assets/portraits/battle/hisa_rally_idle_right.png`。
採用：`DemoApp/assets/portraits/battle/hisa_cover_idle_right.png`（透過PNG、1024×1536）。

プロンプト：

> Use case: identity-preserve. Create one full-body game character sprite pose variant of the supplied Hisa reference. Preserve exactly his face identity, tousled brown hair, light stubble, blue-gray eyes, burgundy scarf, dark charcoal teal mage coat with fine gold geometric embroidery, cream inner robes, belts, pouch, brown trousers and boots, anime illustration style and proportions. Change pose: bravely stepping forward toward screen right to shield a friend, torso three-quarter toward screen right, one index finger clearly pointing at his OWN chest (not outward), other arm lowered slightly outward protecting someone behind him. Mouth nearly closed, determined understated expression: '...over here.' One character only, entire body including both boots visible, no cropping, no text, no scenery, no shadow/background. Truly transparent alpha background with clean game-ready edges. Preserve reference colors and line quality. Tall portrait composition with small consistent margins.

玉の枠は既存SVG素材に合わせて `hisa_command_orb.svg` を作成。左右反転・庇い位置・玉のHPバーとの距離は `ShockMarkCheck --hisa` の画像で確認する。
