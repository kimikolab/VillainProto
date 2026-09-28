# ハネ・弾き返しのヒップアタック（2026-09-29）

この案はユーザーの依頼で取りやめ。現在の実装は [双掌打](HANE_PALM_PRESENTATION.md)。以下は旧案の履歴。

通常立ち絵との違いを強めるため、被弾時の弾き返し（Spring）を臀部を先頭に跳び込む姿勢へ変更。
ユーザー添付のデイジーの技画像を動きの参考に、敵を見据えながら上体を傾け、腰をぶつける。下半身の構造を作り直し、片膝を曲げてもう一方を少し伸ばす。股関節・膝・すね・足首のつながりを分けて見せる。腕は自然に開き、肩の接続を保つ。
内蔵image_genを使用。元の衣装・体格・塗りを参照し、透過PNGを作成。
採用先: `DemoApp/assets/portraits/battle/hane_hip_idle_right.png`。
再作成時の画像参照は `DemoApp/assets/portraits/battle/hane_idle_right.png` のみ。その採用差分に対し、画面右側の脚を股関節から約45°前へ出す編集を加えた。右側の膝が腰のほぼ真下に来る形。
今回の編集プロンプト: [HANE_HIP_FORWARD45_PROMPT.md](HANE_HIP_FORWARD45_PROMPT.md)。元の作り直しは [HANE_HIP_REBUILD_PROMPT.md](HANE_HIP_REBUILD_PROMPT.md)。[それ以前の生成記録](HANE_HIP_LEAP_PROMPT.md)と下の旧プロンプトは不採用案の履歴。

採用PNGは1536×1024、下端余白0px。四隅のalphaはすべて0。表示高は待機絵の1024/1536×1.25とし、頭・胴の体格を合わせる。
臀部の接点は画像座標（1010,550）、中心から右へ242px・下へ38pxとして左右反転し、相手の胴へ合わせる。
相手の中心からカメラ横方向へ0.65だけ手前側の縁へ寄せ、相手に差分が隠れすぎない位置に接触させる。

0.08秒で低く溜め、0.18秒で腰を先に出して接触、0.08秒静止、0.20秒で復帰。
跳び込みの弧は0.32、跳ね戻りの弧は0.18。空中のポーズを平行移動だけで見せず、浮き上がって接触して戻る。
接触に「パンチを受け止める」と局所の衝撃を合わせる。台本は実際の接触完了を待って敵のMoveへ進む。
本体だけを運び、HP・席・巻き込む敵・ダメージは変更しない。手番のドロップキックも維持。
死亡・差分上書き・停止・再開・退場で古い接触や音を残さない。

## 確認

右側の脚を前へ振る編集後、DemoAppビルド成功（警告・エラー0）。DropkickCheckで左右×1/2倍速の接触を撮影し、ヒップ・ドロップキックの描き分け、音、後続Move、復帰と中断を確認。
MovementPortraitCheckで透過、待機への復帰、燃焼、死亡・蘇生・勝利を確認。今回の撮影先は `.tmp/hane-hip-forward45/`。Godotの既存環境警告（userログ・shader cache・証明書ストア）は残るが、両検査は終了コード0。
前回実装時にMovementCheckの実戦リプレイ2回成功（267イベント、Spring 2回を含む）。初回実装時のcompare・dumpは既存表と差分0、auditはずれ0件。今回は差分PNG、表示寸法・接触位置と文書のみを修正。

## 初回プロンプト（不採用・履歴）

Use case: stylized-concept. Asset type: transparent full-body battle sprite variant for a fantasy game. Image 1 is the identity, outfit, proportions and art style reference for Hane, the SAME adult rabbit-eared woman. Draw a forceful athletic HIP CHECK / HIP ATTACK pushing an opponent toward SCREEN RIGHT with her body weight. Single character only. Full-body readable side / rear three-quarter view: pelvis and clothed outer hip lead prominently to the RIGHT, shoulders and head lean to the LEFT as counterbalance, torso rotated away from opponent. She looks back over her RIGHT shoulder toward the unseen enemy on the RIGHT, determined battle expression. Low center of gravity, knees bent, weight drives off the trailing left boot, forward boot braced beneath her hip. Both bent arms are held close toward the LEFT for balance, NOT hands pushing right. The rightmost part of her main body silhouette at contact height is the side/back of her shorts-covered hip; arms and head are well behind it to the LEFT. Clearly different from the reference standing pose and from a flying kick. Keep natural athletic human anatomy, normal hip size and ordinary proportions. Preserve gray-silver ponytail, white rabbit ears with pink inner fur, amber eyes, red-brown short jacket with white fur trim, white shirt, fully opaque dark brown tailored shorts with belts and pouches, leather bracers and heavy fur-trimmed brown boots. Waist cloth and ponytail trail LEFT; do not hide the hip-contact silhouette with the cloth. Exactly two arms, two legs, two boots, two rabbit ears. Same restrained earthy colors, fine anime fantasy linework and detailed shaded illustration. Entire figure including ears and both boots visible with small transparent margins, portrait or square composition. Truly transparent alpha background. No floor, backdrop, shadow, halo, glow, particles, text, watermark, enemy or separate effects. Neutral action-game framing, no close-up or sexualized framing.

## 姿勢の修正プロンプト（不採用・腕が背中へ回り、側面からの接触になるため）

Edit the provided Hane battle sprite, preserving the same character, clothing, proportions, linework and transparent background. Change ONLY the pose to make the HIP ATTACK much more readable. In this current image the face and hip are nearly vertically aligned; move her shoulders, chest and head substantially LEFT of her pelvis. She leans her upper torso LEFT and rotates away, while forcefully thrusting her shorts-covered rear/outer hip RIGHT. The hip must be the RIGHTMOST body part at opponent contact height, extending much farther right than head, elbows or knees. Keep her face visible looking back toward RIGHT over her right shoulder. Full-body dynamic braced stance, both knees bent, trailing boot pushing off at bottom-left and supporting boot under the hip; use the entire body weight for a compact lateral shove. Right hip leads, shoulder line trails LEFT, head approximately over the left knee. Hands bent and held on LEFT behind her, no hands pushing. Athletic combat action, normal anatomy and proportions, no body enlargement. Preserve exact costume including fully opaque dark leather shorts, red fur-trimmed jacket and white shirt; no clothing removal. Same full-body neutral game sprite presentation, no close-up. Ears, hair and boots completely within frame. True transparent alpha background; no glow, scenery, floor, text, enemy or effects.

## 背面からの体当たり・腕の修正プロンプト（不採用・左向きの立ち姿に見えるため）

元の通常立ち絵だけを参照し、誤った差分のポーズを引き継がずに作成。顔・胸・骨盤を左へ揃え、腕を胸の前へ置き、臀部の背面を右の相手へ押し当てる指定。内蔵image_genを使用。敵を見ることと跳び込む動勢が不足し、再修正。

Use case: stylized-concept. Transparent full-body battle sprite for a fantasy game. Input image 1 is ONLY the character identity, costume and rendering reference. Create a NEW anatomically correct pose for the same adult rabbit woman Hane.
Action: a compact backward body-weight shove, inspired by a martial-arts rear body check. Her CLOTHED BUTTOCKS / POSTERIOR push into an unseen opponent on SCREEN RIGHT. This is NOT a sideways hip check. Her face, chest, abdomen and the FRONT of her pelvis all face SCREEN LEFT, aligned without a twisted spine. Her BACK and the REAR of her shorts point SCREEN RIGHT. Show a clear side-profile view, slightly front three-quarter so the natural arm connections can be read. Her face looks LEFT in natural profile, not over her shoulder. Both shoulders connect naturally to upper arms hanging slightly forward LEFT; elbows bent about 90 degrees, both forearms and hands held IN FRONT of her chest toward LEFT in a compact combat guard. No arm or hand behind her back, no reaching backward, no impossible shoulder rotation.
Low center of gravity: torso inclines modestly forward LEFT from the hips, hips sink back RIGHT, knees bent in a broad planted stance, boot at left pushing her entire body backward to RIGHT and other boot bracing below pelvis. The posterior silhouette at mid-body height clearly projects RIGHT behind the spine; rear-facing surface of dark shorts makes contact, not the lateral hip bone. Normal athletic proportions, no exaggerated body parts. Full-body neutral action-game framing.
Preserve silver-gray ponytail, white rabbit ears with pink inner fur, amber eye, short rust-red brown jacket with white fur collar/cuffs, white shirt, opaque dark brown leather shorts with belts and pouch, brown bracers, heavy fur-trimmed boots, long red waist cloth swept away to LEFT so contact point remains visible. Exactly two arms and two legs with readable joints. Same detailed anime fantasy linework, shading and earthy colors. Entire ears and boots in frame, portrait composition with modest margins. Genuine transparent alpha background, no floor, shadows, backdrop, enemy, effects, lettering or watermark.
