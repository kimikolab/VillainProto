# 感電・標 演出追加用の絵素材 v1

作成: 2026-10-07。内蔵 image_gen で既存のキャラクター立ち絵を参照し、立ち絵5点・独立FX2点を制作。
同日の追加作業でDemoAppへ接続済み。待機絵は維持し、該当する動作中だけ差分へ切り替える。
実装と検証は [SHOCK_MARK_IMPLEMENTATION.md](../../SHOCK_MARK_IMPLEMENTATION.md)。

透過確認用一覧 `assets-alpha-preview.png` は [inspect-assets.ps1](inspect-assets.ps1) で生成できる。リポジトリの規約に従い、確認用画像はコミットに含めない。

## 納品素材

| 素材 | 画像 | サイズ | プロンプト |
|---|---|---|---|
| トウ・散粉姿勢 | [tou-powder-right-v1.png](../tou/tou-powder-right-v1.png) | 1024×1536 | [生成指示](../tou/tou-powder-right-v1-prompt.md) |
| ミサ・羽の管制 | [misa-control-right-v1.png](../misa/misa-control-right-v1.png) | 1024×1536 | [生成指示](../misa/misa-control-right-v1-prompt.md) |
| ミサ・乱射移行 | [misa-spray-right-v1.png](../misa/misa-spray-right-v1.png) | 1024×1536 | [生成指示](../misa/misa-spray-right-v1-prompt.md) |
| シガ・割り込み | [shiga-interrupt-right-v1.png](../shiga/shiga-interrupt-right-v1.png) | 1024×1536 | [生成指示](../shiga/shiga-interrupt-right-v1-prompt.md) |
| カタ・落雷指示 | [kata-thunder-right-v1.png](../kata/kata-thunder-right-v1.png) | 1024×1536 | [生成指示](../kata/kata-thunder-right-v1-prompt.md) |
| カタ・雷雲 | [kata-thundercloud-v1.png](../kata/kata-thundercloud-v1.png) | 1536×1024 | [生成指示](../kata/kata-thundercloud-v1-prompt.md) |
| トウ・鱗粉 | [tou-powder-puff-v1.png](../tou/tou-powder-puff-v1.png) | 1254×1254 | [生成指示](../tou/tou-powder-puff-v1-prompt.md) |

## 参照元

- トウ: `DemoApp/assets/portraits/battle/tou_idle_right.png`
- ミサ2点: `DemoApp/assets/portraits/battle/tome_idle_right.png` と `tome_attack_idle_right.png`
- シガ: `DemoApp/assets/portraits/battle/shiga_idle_right.png`
- カタ: `DemoApp/assets/portraits/battle/kata_idle_right.png`
- 雷雲・鱗粉: 新規生成。別々に重ねて使う独立素材。

## 接続時の注意

- ミサは本人のみ。衣装に付いた銀細工・結晶は残し、浮遊羽・光線は含めていない。既存の `misa_feather.png` を独立して動かし、その先端から光線を描く。
- シガは鞭の柄のみ。画像右側の柄先から、ゲーム側で鞭と電撃を伸ばす。しっぽは身体の一部として画像に含む。
- カタは本人のみ。杭、雲、雷はそれぞれ別に描画する。雷雲素材には主落雷を焼き込んでいない。
- トウの散粉姿勢と粉は分離。手番の頭で散粉姿勢へ替え、その絵の眼状紋に発光を重ねる。眼状紋だけの位置合わせ済みマスクは今回の7点には含まない。
- 雷雲は複製・拡縮・重ね合わせと色調整で成長を表す。鱗粉は透過のある霞素材なので、細かな個別粒子と合わせて使う。
- 各立ち絵は同じキャンバスサイズだが、動きによって人物の高さ・足位置・左右の余白が違う。接続時に頭身と足元を補正。ゲーム内の左右両陣営・1倍／2倍で確認済み。
- `assets-alpha-preview.png` は確認用の背景合成であり、ゲーム用の素材ではない。実素材は上表の各透過PNGを使う。

## 確認済み

- 7点すべて32ビットRGBAで完全透明画素を保持。4px間隔のアルファ検査結果は [alpha-inspection.json](alpha-inspection.json)。
- チェック背景で全身・輪郭・内部の抜けを目視確認。原画の見かけの背景色は透明部分のRGBであり、通常のアルファ合成では表示されない。
- 元の生成PNGのアルファをそのまま保持。立ち絵にも微小な半透明があるため、一括で不透明化していない。
- 原画の画像編集は内蔵 image_gen のみ。`inspect-assets.ps1` は読み取り検査と確認用一覧の作成だけを行い、7点の原画には変更を加えない。

## ゲーム側の配置

| 元素材 | DemoApp側のコピー |
|---|---|
| ミサ・管制 | `assets/portraits/battle/tome_control_idle_right.png` |
| ミサ・乱射 | `assets/portraits/battle/tome_spray_idle_right.png` |
| シガ・割り込み | `assets/portraits/battle/shiga_interrupt_idle_right.png` |
| カタ・落雷 | `assets/portraits/battle/kata_thunder_idle_right.png` |
| トウ・散粉 | `assets/portraits/battle/tou_powder_idle_right.png` |
| 雷雲 | `assets/fx/kata_thundercloud.png` |
| 鱗粉 | `assets/fx/tou_powder.png` |
