# ラウ 戦闘立ち絵の手指修正

2026-10-22、画面右の左手が4本指に見えるというユーザー指摘に対応。待機・接触攻撃・伝染反応の3枚をCodex内蔵image_genで編集し、親指と残り4本の輪郭を分離した。伝染反応は初回の修正でも小指が不足したため、追加修正した画像を採用。

## 採用画像

| 生成原本 | ゲーム配置先 |
|---|---|
| [rau-idle-right-v2.png](rau-idle-right-v2.png) | `DemoApp/assets/portraits/battle/rau_idle_right.png` |
| [rau_touch-v3.png](rau_touch-v3.png) | `DemoApp/assets/portraits/battle/rau_touch_idle_right.png` |
| [rau_spread-v2.png](rau_spread-v2.png) | `DemoApp/assets/portraits/battle/rau_spread_idle_right.png` |

[生成プロンプト](rau-hand-fix-v1-prompts.txt)。各画像を参照して手元を編集。生成アルファを保ち、画像の後処理はしていない。旧原本も保存。

3枚とも1024×1536のRGBA PNG、四隅のalphaは0。原本とゲーム配置先のSHA256一致を確認。足元の不透明領域の下端は旧画像との差が1〜2pxに収まり、表示高さと足元余白の設定は継続して使用。

差し替え後にVulkan描画の `RauCheck.tscn -- --capture` を再実行。左右の差分・透過・足元を画面で確認し、初回・再戦とも `RAU_REPLAY_OK`、最後に `RAU_CHECK_OK` に到達。終了時の既存リソース解放診断は演出記録のとおり残る。
