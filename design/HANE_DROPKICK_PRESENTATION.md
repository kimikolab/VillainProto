# ハネ・手番のドロップキック（2026-09-29更新）

9/28に弾き返しへ追加した差分を、9/29のユーザー指定で手番の吹っ飛ばし（`Blast`）へ移動。
被弾時の弾き返し（`Spring`）は押し返す差分へ交換。その後ヒップアタックへ更新（`HANE_HIP_PRESENTATION.md`）。
9/29の追加指定: 手番は接触時の「大キック」に敵射出時の「氷魔法1」を重ねる。弾き返し音は「パンチを受け止める」に分離。
内蔵 image_gen で通常立ち絵を参照し、透過の両足ドロップキック差分を生成。
採用先: `DemoApp/assets/portraits/battle/hane_dropkick_idle_right.png`。
参照: `DemoApp/assets/portraits/battle/hane_idle_right.png`。

1536×1024の透過PNG。通常1536px高の立ち絵と画素あたりの体格を合わせるため、表示高は通常の1024/1536倍。
下端余白は19/1024。両靴の平均接点（画像中心から右へ約700px、下へ約65px）をカメラの右・上ベクトルで相手の命中点へ合わせる。
0.055秒の予備動作→0.14秒の跳躍→接触音と衝撃→0.06秒静止→0.20秒で復帰。全区間を倍速に追従。
本体だけを動かし、席・HPは台本を再生する側が更新する。死亡・別差分・停止・再開では古い接触を無効化。
手番の攻撃は固定秒数ではなく、実際の接触完了を待ってから敵の射出・ピンの巻き込みへ進む。中断・退場も待機を解除する。

## 検証

- DemoAppビルド成功。DropkickCheckで左右×1/2倍速、帰還中の標的、接触前の無音、接触音1回、後続Move・着地、死亡・別差分・停止・再開を確認。
- 実画面を撮影し、透過・体格・左右の跳躍と接触を確認。MovementPortraitCheckで待機への復帰・生死・勝利絵を確認。
- MovementAudioCheckで指定3素材の復号、連撃5本の音と残光各5回、貫通との音の重複防止を確認。
- SeroPierceCheckで貫通／連撃の残光を左右×1/2倍速で撮影し、消滅と再開を確認。
- MovementCheckの実戦リプレイ2回成功（238イベント・矢6本）。最終HP・席の一致を確認。
- 現行ソースを別出力先へビルドしたBattleSimのcompareは勝率表と差分0。

## 生成プロンプト（原文）

Use case: stylized-concept. Asset type: transparent full-body 2D battle sprite variant for an existing fantasy game character, Hane. Input image 1 is the character identity/costume/style reference. Create the SAME adult rabbit-eared woman doing a forceful airborne DOUBLE-FOOT DROPKICK toward SCREEN RIGHT, shown in readable side/three-quarter view. Her head and torso are at LEFT, hips center, TWO legs extend together almost horizontally toward RIGHT, both boot soles leading the attack on the right. Knees slightly flexed naturally, thighs clearly connect to the same pelvis. Torso leans back left, arms bent and drawn back near torso for balance, not reaching forward. Exactly two arms, two legs, two feet, two rabbit ears. Preserve the reference face, silver-gray ponytail, amber eyes, long white rabbit ears with pink inner fur, short red-brown fur-trimmed jacket, white top, dark brown shorts, brown leather belts/pouches/bracers and fur-trimmed heavy brown boots. Athletic determined expression. Ponytail, ears, red waist cloth stream to LEFT from rightward motion. Anatomically coherent hips, knees, ankles and shoulders. Dramatic unmistakable horizontal flying kick silhouette, NOT standing, not a single-leg high kick. Same finely drawn anime fantasy illustration, subdued earthy palette, subtle detailed shading and clean edges. Transparent alpha background, isolated SINGLE character, full body and ears/boots fully inside canvas with small margin. Landscape composition. No floor, no shadow, no halo, no dust, no glow, no motion trails, no other character, no text or watermark; game engine adds effects separately.
