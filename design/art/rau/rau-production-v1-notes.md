# ラウ ペストマスクの立ち絵 v1

2026-10-22、ユーザーと相談済みのペストマスクの方向で、編成用待機と右向き戦闘待機を制作。Codex 内蔵 image_gen を使用。

戦闘待機の現行画像は [手指修正v2](rau-idle-right-v2.png)。[修正記録](rau-hand-fix-v1-notes.md)を参照。以下の画像仕様・照合結果は初版制作時点の記録。

## 意匠

- 長い象牙色の嘴、真鍮縁の丸い暗紫色レンズ、革の留め具で人工のペストマスクと分かる形。
- つば広帽、墨色の長衣、紫の裏地、手首の包帯、革の肩掛け鞄。素顔は隠す。
- 頭身を抑えた細身の体。グザの大きなフードと袋とは異なるシルエットにする。
- 指先の小さな紫の滴で、触れた相手から疫病を広げる役割を表現。毒の色は紫系。
- グザとスィドを絵柄の参照に使用。戦闘用はラウの編成用画像を参照し、向きと構えを変更。

## 画像と組み込み

- [rau-front-v1.png](rau-front-v1.png): 編成・勝利用。ゲーム配置先 `DemoApp/assets/portraits/rau.png`。
- [rau-idle-right-v1.png](rau-idle-right-v1.png): 右向き戦闘待機。ゲーム配置先 `DemoApp/assets/portraits/battle/rau_idle_right.png`。
- [生成プロンプト](rau-production-v1-prompts.txt)。両画像とも1024×1536のRGBA PNG。四隅のalphaは0、全体の範囲は0〜254。生成画像のアルファを保持し、画像加工はしていない。
- 原本とゲーム配置先のSHA256一致を確認。
- 既存のファイル名規約で読み込む。`UiKit` の表示高さを2.45、戦闘絵の下端余白を54 / 1536に設定。戦闘の規則・台本は変更していない。
- 購入素材一覧とローカルのNatureBlocks / EffectBlocksを確認。背景・演出部品のため、個別の立ち絵は内蔵image_genで制作。

## 確認

- `dotnet build DemoApp/DemoApp.csproj --no-restore` 成功。エラー0、今回編集していない箇所の警告3件。
- 「毒+ベニ+ラウ」の[編成画面](captures/formation.png)と、第三波・seed 0の[戦闘開始](captures/battle/battle-2_5d-01.png)・[状態付与後](captures/battle/battle-2_5d-06.png)で、絵柄・体格・向き・透過・足元を目視確認。
- 同編成・第三波・seed 0の頭なし再生は `DEMO_SMOKE_COMPLETE events=382 turns=5 won=True poisonSpreads=5 poisonLeaks=6` に到達。
- 終了時には既存のFontAdvanced / CanvasItem / ObjectDBなどの解放診断が出る。画面保存は全件 `error=Ok`、戦闘再生は完了行で確認。

待機モーションの連番は未制作。接触攻撃・伝染反応の差分は後続の [演出の記録](rau-effects-v1-notes.md) に追加。
