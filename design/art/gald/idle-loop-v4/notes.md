# ガルド 待機ループ v4（2026-10-16）

## 修正対象

ユーザーはv3のA/Bの動きを「どちらも良い感じ」と評価。一方で兜の頭頂の突起が伸び縮みして見え、剣の周囲に揺らぎが見えると指摘した。振幅30% / 45%と2秒の周期を保ち、これらの変形・輪郭の問題を修正する。

## 変更

- 兜にも動く剛体の制約を適用し、突起の長さを保持する。頭そのものを止める固定レイヤーは使わない。
- 剣・盾・兜の周囲は、元の運動に従いながら滑らかにつながる変形場で解く。従来の狭いぼかし境界で服の模様が波打つ現象を抑える。
- 元絵のBiRefNet・クロマキー処理を1回だけ行い、そのRGBAを全コマへ引き継ぐ。RGBと透過を同一の写像で動かす。コマごとに輪郭を推定し直さない。
- 透過RGBは乗算済みの色で補間してから戻し、透明部分の色が縁へ混ざることを防ぐ。

`tools/comfy_idle_loop/comfy_node/__init__.py` の `VPScaleIdleMotion` に、任意入力 `rigid_mask_c` / `elastic_constraints` / `reference_rgba` と透過・RGBAの出力を追加。従来のRGB出力0と報告出力1、任意入力を省略した動作は維持する。

兜・剣・盾のマスク、投入したAPIワークフロー、結果・確認シートをこのフォルダーに保存する。元絵と参照する全身の動きはv3と同じ。購入済み素材の適否はv1の記録に従う。

## 数値検証

`verify-contours.py` で既知の波打つ変形を入力し、硬い部位の移動を保持しつつ曲率を低下させ、写像の反転が無いことを確認した（粗さ1.1890→0.0081、最小Jacobian 0.783）。RGBと透過が同じ写像で動き、半透明の縁に背景色が混ざらないことも確認（`ELASTIC_FIELD_CHECK_OK` / `SHARED_ALPHA_CHECK_OK`）。

v3の既知運動の振幅・周期と剛体の検証も通過。これらの数値検証は、人物の見え方の合格判定の代用にはしない。

## 確認結果

兜内部の特徴点で移動・回転だけを除いた後、頭頂の位置の変動を測定した（拡大縮小は除去しない）。
v3はA 7px / B 11px、v4は両案とも0px。追跡残差の中央値もA 0.061px / B 0.083pxとなった。
測定は `helmet-crest-comparison.json`、拡大確認は `v3-detail-inspection.jpg` / `v4-detail-inspection.jpg`。
兜の突起が形を保ったまま動き、剣の周囲の服・脚の模様に出ていた細かな波打ちが改善したことを目視した。

| 確認箇所 | A（30%） | B（45%） |
|---|---|---|
| コマ数・周期 | 32 / 2秒 | 32 / 2秒 |
| 兜の上下（追跡） | 10.0px | 14.9px |
| 手の上下 | 4.3px | 6.6px |
| 首 / 腰の上下 | 11.5 / 11.4px | 17.1 / 17.2px |
| 靴底（元絵906） | 906固定 | 906〜907（1px以内） |
| 緑の残り | 0px | 0px |
| 透過後の継ぎ目比 | 0.56 | 0.58 |

`loop-boundaries.jpg` の末尾3コマ→先頭3コマ、暗背景・市松の4姿勢、頭頂と剣周囲の拡大を確認。
GIFのコマ別パレット・ディザ由来のちらつきも避けるため、比較GIFは共通パレット・ディザなしで出力した（`idle_loop.py check`）。PNG連番の色は変えていない。

両ジョブsuccess、既知入力の変形と透過の検証、局所追跡・頭頂の比較、JSON・Python構文、比較GIF32コマ、`git diff --check` を確認済み。
作業用8189は停止し、常用8188は維持。最終採用・ゲーム反映はユーザー確認後。

## 採用とゲーム反映（2026-10-17）

ユーザーの「Aでお願いします。ゲームに組み込んでください。」を受け、A（`gald_coherent_v4_a30`）を採用。
seed 2022 の元運動・振幅30%・crossfade 0、640×960 RGBAの32コマを16fps・2秒周期で再生する。
`idle_loop.py install gald_coherent_v4_a30 gald` で `DemoApp/assets/portraits/battle/idle_loop/gald/` へ配置（PNG約24.6 MB）。
インストール後の全32コマの画素が選択された元出力と一致することを照合し、容量とSHA256は `installed-manifest.json` に記録した。

本番の再生コードは既存の自動読み込みを使用。`IdleLoopCheck.cs` にガルドのコマ数・開戦からの再生・同じ高さ・シェーダーとの同期・時間による進行を追加し、死亡で停止／蘇生で再開、抜剣・膝つき・勝利絵を待機連番で上書きしないことも確認した。

- `dotnet build DemoApp/DemoApp.csproj -c Debug`: 成功。既存の `BorgAttackClipCheck.cs:47` にCS8602警告1件。
- `IdleLoopCheck.tscn -- --verify`: `IDLE_LOOP_CHECK_OK`（gald / sword / kneelを含む）。
- `PortraitStateCheck.tscn -- --verify`: `PORTRAIT_STATE_CHECK_OK`。
- `FireCheck.tscn -- --replay`: `FIRE_CHECK_OK`。
- `MovementPortraitCheck.tscn -- --verify`: `MOVEMENT_PORTRAIT_CHECK_OK`。
- 実戦の `速攻 (ボルグ×ムド)`・stage 2・seed 1を窓ありで再生し、`game/battle-2_5d-01.png`〜`06.png` を保存。表示サイズ・接地・透過を目視確認。

終了時の既存RID／ObjectDB解放警告はFireCheckと実戦キャプチャで出たが、全チェック成功・終了コード0。
