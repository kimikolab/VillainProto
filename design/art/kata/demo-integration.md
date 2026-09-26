# カタ 立ち絵のゲーム適用

- 採用コンセプト: kata-concept-v8-female.png
- 待機: kata-standing-v2.png → DemoApp/assets/portraits/kata.png
- 戦闘: kata-idle-right-v3.png → DemoApp/assets/portraits/battle/kata_idle_right.png
- UiKitの表示高2.50、戦闘画像の足元余白0.00716（α128超の最下端1524px、画像高1536px）。
- 髪飾りの左右・手指を修正した画像を使用。戦闘ロジック・雷演出は今回変更していない。

検証: DemoAppビルド成功。KataPortraitCheck.tscnで編成・戦闘の採用画像、状態更新後の維持、勝利アニメーション後の待機画像を確認。
画面: formation-game-check.png / battle-game-check.png。透過とベニ・ミオとの並びを目視確認。
