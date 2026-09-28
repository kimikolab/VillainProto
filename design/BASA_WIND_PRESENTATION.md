# バサの羽ばたき・竜巻・追い風（2026-09-28）

## 表示

- ターン頭の隊列入れ替え: 0.24秒で離陸 → 両翼を振り下ろす差分 → 風を両陣営へ送り、竜巻の中で実際のMoveを再生。
- 段0: 各陣地1本（高さ2.35）。段1: 1本のまま大型化（2.69）。段2: 各陣地2本（3.03）。段3: 2本をさらに大型化（3.37）。半径も段ごとに拡大。値は表示用のワールド単位。
- 段はDisarrayStageの通知だけを読み、表示中の竜巻にも反映。Moveが無い陣営には竜巻を出さない。
- 竜巻の対象: 実際にMoveする駒だけを明るい金白色の渦で包む。0.64秒で本体が一回転し、横に曲がる浮遊軌道で次の席へ移る。名前・HPバーは回さない。隊列入れ替えの最後のMoveでは着地まで待ち、直後の攻撃で回転が途切れないようにする。
- 薙ぎ: 通常の薙ぎと追加のSquallを同じ風の攻撃へ接続。既存の羽ばたき差分から、実際の被弾者の幅を覆う風圧面4層と流線9本を放つ。0.24秒後に実際の命中位置で風を弾かせる。
- 追い風: 踏み込む味方の背後から、入れ替え相手の席の先へ流線9本と風圧の輪3枚を走らせる。進行方向へ流れる大きな山形4個、対象に追従するミント色の足元の輪・風の尾・残像・「追い風」の表示を付ける。移動は0.26秒の小さな跳躍。後ろへ退く相手も記録のMoveに従う。
- ユーザー提供のSpringin音源を使用。竜巻の羽ばたきは強風1.mp3（-15 dB）、薙ぎ・追加の突風は強風3.mp3（-12 dB）、追い風は強風2.mp3（-14 dB）。隊列入れ替えの各Moveや竜巻の本数で重複再生しない。原音ピッチ、通常SEの4音枠を共用。

## 接続と終了

BattleCoreの規則・乱数・イベントを変更しない。MovementPresentationで既存の見出しに結び付いたMove（追い風など）を除き、同じターン・書き手のMoveをまとめる。Mainは書き手のUnitIdがbasaのグループだけを羽ばたきへ接続する。最初のMoveで1回だけ開始し、後続の記録をそのまま再生する。

バサ自身の浮上は描画スプライトだけ。席・本体のHome・HPは動かさない。運ばれる対象の席とHomeは従来のMoveの値に合わせ、本体の回転だけを追加する。死亡・勝利・別の移動への切り替えで回転を解除。死亡・勝利でバサの浮上を消し、離陸待ち中の死亡・画面終了・再開では古い竜巻予約を無効にする。風のメッシュは短命で、再開時はFX親の既存の片付けに従う。

## 画像

- 保存先: `DemoApp/assets/portraits/battle/basa_flap_idle_right.png`
- 参照画像: `DemoApp/assets/portraits/battle/basa_idle_right.png`
- 作成: imagegenスキル、内蔵image_genツール。透過PNG、無加工でコピー。通常絵は保持。
- サイズ: 1374×1145。足先までの下余白は約1.485%。横に翼を広げた絵に合わせ、通常絵の表示高の90%で配置。
- 用途: 両翼を下へ広げた羽ばたきの瞬間。通常絵からの上昇、差分での一打、通常絵へ着地。

### 最終生成プロンプト

Use case: identity-preserve. Asset type: transparent full-body 2.5D Japanese fantasy game battle sprite, new action variant. Input image 1 is reference for EXACT character identity, costume, palette, and anime rendering. Depict this SAME youthful silver-haired harpy character Basa hovering in the air while making one mighty symmetrical wing downstroke to summon a storm. Face looking to screen RIGHT in three-quarter view, energetic determined expression, same amber eyes, silver tousled hair, teal scarf, off-white sleeveless belted tunic, dark shorts, teal-grey and cream feathers, same bird legs and black talons. ANATOMY: exactly two wing-arms attached at the shoulder joints (NOT human arms plus extra wings), two bird legs. Both large feathered wing-arms sweep outward and DOWN in a powerful broad downstroke, distinct from reference's one raised wing; outer flight feathers spread wide, body nearly upright with chest lifted, knees slightly bent under body and talons curled in flight. Keep full head, wing tips and feet visible with 5% transparent margin. Large broad readable silhouette. Match same detailed clean anime outlines and soft painted shading. Character only; no cyclone, no swirls, no trails, no ground, no shadows, no text or border. Truly transparent RGBA background. Landscape or square canvas so both wings fully fit. Preserve face, clothing, feather pattern and anatomy.

## 検証

`BasaWindCheck.tscn`: 左右×1/2倍速×段0〜3、透過画像、通常姿勢への復帰、FX消滅、表示中の段上昇、再開と死亡の競合。
`MovementCheck.tscn -- --replay`: 実台本の隊列入れ替えと追い風の回数、2回の再生、最終HP・席の一致。
