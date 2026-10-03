# ハネ・本人の被弾からの飛び蹴り（2026-09-29）

本人への弾き返し（Spring・SpringGuardの見出しなし）を飛び蹴りへ変更。
味方を守る掌と救援の横っ飛び、手番の両足ドロップキックは維持。

- 差分: `DemoApp/assets/portraits/battle/hane_flying_kick_idle_right.png`
- 原画・プロンプト: `design/art/hane/hane-flying-kick-right-v1*`
- 音源: `D:/Assets/SE/効果音ラボ/戦闘/中キック.mp3` を無加工で `hane_spring_kick.mp3` へコピー。SHA-256一致。
- 原画1536×1024、表示高は通常の1024/1536倍。踵の接点（1504,486）を相手の胴の手前へ合わせる。
- 0.055秒の予備動作 → 0.14秒の跳躍 → 接触音（通常時-12 dB） → 0.06秒静止 → 0.20秒で復帰。
- 跳躍の弧0.18／復帰0.14、単体用カメラ揺れ。手番の弧0.35／0.28・全体用カメラ揺れより控えめ。
- 追加依頼で接触の星形を、白い芯・金色の衝撃波・短い放射光へ更新。幅2.5の面に描き0.18秒で消える。相手の本体に0.16秒の小さなのけぞりを合わせる。
- 実際の接触完了を待って台本のMoveへ進む。死亡・停止・再開・差分変更は既存のキャンセル経路を共用。
- 戦闘の対象・ダメージ・席の判定は変更しない。

## 検証

DemoAppビルド成功（BattleCoreの既存CS0162警告1件）。DropkickCheckは描画あり・なしで左右×1/2倍速の踵接触、音1回、後続Move、復帰と中断を通過。
MovementPortraitCheck・MovementAudioCheck・MovementCheck --followupも通過。画面の撮影は `.tmp/hane-flying-kick/`。
MovementCheck --replayは描画あり・なしともseed 51で「吹っ飛ばしを実際のMoveへ接続」の検査に失敗し、総合リプレイは未通過。専用テストでは手番の吹っ飛ばしと本人の飛び蹴りの後続Moveを確認済み。
Godot実行には既存環境のuserログ・shader cache・証明書ストアの警告がある。

接触光の追加後もDemoAppビルドと描画ありのDropkickCheckを再実行。左右×1/2倍速の接触・音・復帰・中断を確認。撮影は `.tmp/hane-kick-impact/`。
