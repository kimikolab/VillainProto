# チリ — かぼちゃショートパンツとダーク縞ソックスのゲーム適用

**演出も本編接続済み:** 仕掛ける／手を離すを本編で切り替える。爆弾の印・導火線・爆発・転送・不発の接続は [演出記録](../../../CHIRI_PRESENTATION.md)。以下は衣装適用時点の記録。

2026-11-01。ユーザーの指定により、承認済みのかぼちゃショートパンツとオレンジ×チャコールの膝上ソックスへ統一した。

| 表示 | 採用原本 | ゲーム配置先 |
|---|---|---|
| 通常 | chiri-standing-v2.png | DemoApp/assets/portraits/chiri.png |
| 戦闘待機 | chiri-battle-idle-right-v2.png | DemoApp/assets/portraits/battle/chiri_idle_right.png |
| 仕掛ける | chiri-battle-plant-right-v2.png | DemoApp/assets/portraits/battle/chiri_plant_idle_right.png |
| 手を離す | chiri-battle-release-right-v2.png | DemoApp/assets/portraits/battle/chiri_release_idle_right.png |

通常は `../outfit-revision-v1/chiri-pumpkin-shorts-darkstripe-v3.png` の無加工コピー。
戦闘待機と仕掛けるは既存ポーズを編集対象・上記通常絵を衣装参照として内蔵image_genで編集した。
手を離すは、新しい仕掛ける差分から前の手の爆弾だけを除いた。プロンプト全文は `prompts.md`。CLI不使用。
全4枚は1024×1536・RGBAで、生成時の透過を維持している。

## 適用と復元

差し替える直前の4枚を `previous-game-assets/` に控えた。
原本・ゲーム配置先・変更前後のSHA256・画像寸法・靴底の座標は `assets.json`。
ゲーム配置先のハッシュが新原本と全4枚で一致することを確認した。
UiKitの足元補正は、戦闘待機34/1536・仕掛ける48/1536・手を離す47/1536へ更新した。

BattleCore、Main.csの爆弾イベント処理、演出素材の時刻や件数は今回変更していない。
通常・待機は本編に反映される。仕掛ける・手を離すは既存の行動素材の配置先へ反映され、素材プレビューで切替を確認できる。
本編でこの2差分を選ぶ爆弾専用のイベント接続は、素材制作時から引き続き別作業。

## 確認

- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 成功、エラー0。今回変更していないBattleEngine.csでCS0162とCS8602の警告2件。
- `selection-game-check.png`: 編成画面の後1に新しい通常絵を表示。右の説明欄もチリを選択した状態。
- `battle-game-check/battle-2_5d-01.png`〜`06.png`: 実戦の待機絵・透過・足元を確認。`DEMO_CAPTURE_COMPLETE`。
- `previews/01.png`〜`05.png`: 新衣装で行動差分とエフェクトを試写。`CHIRI_MATERIAL_CAPTURE_COMPLETE`。
- 素材の検査: `CHIRI_MATERIAL_VERIFY_COMPLETE phases=5 repeat=2 pause=ok speed=ok fuse=ok arrival=ok clear=ok`。
- 頭なし実戦: 試遊・導火線 チリ、ボス規定形、seed0。`DEMO_SMOKE_COMPLETE events=320 turns=10 won=False`、exit0。
- `git diff --check`成功。

素材プレビューに `--capture-dir=<path>` を足し、この衣装の確認画像を旧衣装の記録とは別に保存できるようにした。

## ログの留意点

実戦の描画キャプチャを途中で終了した後、MakeGroundRing/Link経路のadd_childエラーと描画リソースの解放メッセージが出た。
320イベントを最後まで再生した頭なし検証は完了し、終了時はTextServerの解放メッセージのみが出た。
同じ種類の終了メッセージは旧衣装の検証記録にもあるが、原因の切り分けや終了処理の修正は今回行っていない。
詳細は `logs/battle-capture.log` と `logs/replay.log`。
