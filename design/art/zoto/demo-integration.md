# ゾトの浮遊鉱石立ち絵

2026-10-05。ユーザー採用の不規則な結晶のトゲ・発光する核・手足のない浮遊体から、組み込み image_gen で通常用と右向き戦闘用を生成。
プロンプト全文は `zoto-production-v1-prompts.md`、採用コンセプトは `zoto-approved-concept-v1.png`。

- 通常: `zoto-front-v1.png` → `DemoApp/assets/portraits/zoto.png`
- 戦闘: `zoto-idle-right-v1.png` → `DemoApp/assets/portraits/battle/zoto_idle_right.png`
- 旧画像: `originals/` に保存。

両画像は1038×1515のRGBA PNG。通常用の完全透明画素数753999、戦闘用877962。
アルファ128以上の領域は通常用 (9,48)〜(1027,1352)、戦闘用 (26,34)〜(1016,1333)（座標は0始まり・両端含む）。
全トゲがキャンバス内に収まり、核と鉱石外殻の形を保持。戦闘用は核を右へ向ける。

表示高は従来の1.58のまま。`UiKit.BattlePortraitBottomPaddingRatio` のゾトだけ0に変更し、下端181pxの透明域を地面へ詰めずに残す。これにより約0.19ワールド単位の浮遊間隔を確保。
戦闘の数値・特性・ルールは変更していない。

## 確認

- DemoAppビルド成功、エラー0。既存のBattleCore/BattleEngine.cs:5646のCS0162警告1件。
- 編成と戦闘の実画面で透過・核の向き・表示サイズ・浮遊位置を確認。
- `selection-game-check.png` / `battle-game-check.png` に撮影結果を保存。
- `ZOTO_ART_CAPTURE result=0`、起動終了コード0。
- 既存UIのアンカー警告と終了時のRID/ObjectDB解放警告あり。画像の読込・撮影は完了。
- 生成原本とゲーム内配置ファイルのSHA256一致を確認。

撮影スクリプトは `capture-integration.gd`。通常起動には関与しない。
