# ヒサ：庇い差分 v2

この版は姿勢修正の履歴。現在のゲーム内素材は右向きの受け構えにしたv3（`hisa-cover-v3-prompt.md`）。

組み込み imagegen で再生成。初版の脚・重心・ひねりの不自然さを直す依頼に対応。
参照：初版 `hisa_cover_idle_right.png`（顔・衣装・自分を指す仕草）、`hisa_idle_right.png`（既存デザイン）。
採用先：`DemoApp/assets/portraits/battle/hisa_cover_idle_right.png`。初版の生成記録は `hisa-cover-prompt.md`。

検証：1024×1536・透明アルファあり。靴底の下余白31pxに合わせて `UiKit` の接地値を更新。左右×1倍/2倍の画面で脚と接地を確認し、`SHOCK_MARK_CHECK_OK`。キャプチャは `.tmp/hisa-v2-images/`。

## プロンプト

> Use case: identity-preserve. Asset: revised full-body Hisa cover pose for a 2.5D Japanese fantasy game, 1024x1536 portrait transparent PNG.
> Image 1 is the rejected cover-pose edit target: preserve character identity, clothing and chest-pointing narrative, but REBUILD THE ENTIRE BODY POSE AND LEG ANATOMY. Image 2 is the canonical character design reference.
> Keep the adult man's tousled brown hair, gray-blue eyes, faint chin stubble, burgundy scarf, dark charcoal teal coat with fine gold geometric embroidery, cream inner layers, belt and brown pouch, trousers, brown leather boots, detailed anime linework and painted shading.
> He has just stepped in front of an ally and STOPPED in a grounded protective stance facing screen right in three-quarter view. Calm, resolute expression, nearly closed mouth. His right index finger lightly touches his own upper chest: '...over here.' Other arm is lowered slightly outward toward the ally behind him.
> CRITICAL POSE CORRECTION: simple upright standing guard, believable human anatomy, shoulders and pelvis aligned, spine vertical above pelvis. Balanced weight supported by BOTH feet. Feet about shoulder width apart, one foot only half a foot-length ahead of the other. Both heels and soles firmly on the same imaginary level floor, nearly the same baseline with minimal perspective difference. Knees just slightly relaxed, each directly aligned with its respective thigh, shin and foot. Normal equal leg lengths and matching boot sizes. Hip sockets must connect naturally to the two thighs. Legs fully distinguishable, uncrossed. No lunging, no deep crouch, no bent-back knee, no tiptoes, no floating feet, no wide split stance, no twisting pelvis, no excessive foreshortening or giant front boot. Coat drapes naturally with subtle movement; clear readable lower legs and boots. Ensure the gesture does not pull the torso out of balance.
> Single character centered, entire head and both boots visible, small top margin, soles approximately 30 pixels above the bottom. Orthographic-like game sprite perspective. Genuine transparent alpha around the silhouette, no backdrop, no ground plane, no cast shadow, no text, no effects. Preserve costume and identity rather than the rejected anatomy.
