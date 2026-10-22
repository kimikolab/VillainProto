# ラウの接触・伝染演出 v1

2026-10-22、立ち絵に続いて接触攻撃と伝染反応を追加。戦闘の計算・対象・付与量はBattleCoreの台本を使い、DemoAppに表示を接続した。

現行の差分画像は [接触v3](rau_touch-v3.png) / [反応v2](rau_spread-v2.png)。[手指の修正記録](rau-hand-fix-v1-notes.md)を参照。以下の初版画像と仕様は制作時点の記録。

## 差分

Codex 内蔵 image_gen で既存の右向き待機から制作。PNGの生成アルファを保持し、画像の後処理はしていない。いずれも1024×1536、RGBA、alphaの範囲は0〜254。反応の四隅は0、接触は右下のみ1・他の三隅は0。

- [接触攻撃](rau_touch-v2.png): 前へ重心を落として手を伸ばす。ゲーム配置先 `DemoApp/assets/portraits/battle/rau_touch_idle_right.png`。初稿v1から余白を修正したv2を採用。通常攻撃に接続し、毒が無い相手にも同じ差分を使う。
- [伝染反応](rau_spread-v1.png): 両手を開き、指から毒が垂れる。ゲーム配置先 `DemoApp/assets/portraits/battle/rau_spread_idle_right.png`。味方への漏れと死亡時の拡散に接続。
- [生成プロンプト](rau-effects-v1-prompts.txt)。足元余白は接触158 / 1536、反応76 / 1536。原本とゲーム配置先は同一ファイル。

## 演出と接続

- 接触は小さな淡色の衝撃を実際の攻撃対象に描く。ここでは毒を付与しない。既存の `enemy_swish_hit_004〜006.wav` を5 dB抑えて使う。
- `PoisonRoute.Touch`: `SpreadFromId`（殴られた敵）から `TargetId` へ、高さと幅の異なる紫の滴が飛ぶ。ラウ自身から新しい毒を撃つ形にはしない。
- `TouchLeak`: `ActorId`（ラウ）の足元から実対象の味方へ、平たい小さな滴が順に滲む。
- `Contagion`: 実対象の周囲に紫の飛沫が咲き、毒の泡へつながる。
- 同じターン・経路・書き手・伝染元の連続した `StatusGain` だけを束ねる。死亡・経路変更・別の出来事を跨がない。到着まで待ち、各件の残量加算は通常の `StatusGain` 処理で一度だけ行う。状態数字・HPを演出側で再計算しない。
- 旧伝染のTweenを毒軸の時計へ移し、飛沫・漏れ・差分の復帰を停止と速度変更に対応。非表示・再戦で飛沫と待ち手を解放し、死亡・勝利で差分を通常絵へ戻す。

## 台本の制限

`Contagion` の `StatusGain` には書き手と受け手があるが、毒を撒いた死骸のIDは無い。複数の死亡・蘇生・死亡時反応が入るため、直前のDeathを始点とみなさない。現段階では受け手の局所演出までとし、死骸から飛ぶ方向付きの軌跡には追加データが必要。BattleCoreは変更していない。

## 購入素材

購入一覧と `DemoApp/PolyBlocks/EffectBlocks/assets/smoke/smoke_toxic.tscn` の実ファイルを確認。死亡時の受け手の薄い毒煙に、既存の `PoisonFlowFx` にある複製処理を利用。共有の粒子材・描画材・原本は変更しない。毒煙が無い環境でも滴の形と到着時刻は同じ。接触の滴と地面の滲みは、方向・停止・到着を台本へ合わせるためコードで描く。購入素材本体はGitへ追加しない。

## 検証

- DemoAppビルド成功。今回触っていない `BorgAttackClipCheck.cs` の既存警告あり。
- `RauCheck.tscn`: 左右両陣営の接触、伝染元と受け手、到着前の残量、停止中の飛沫・差分保持、途中の倍速変更、死亡・勝利・非表示・飛行中の再戦を確認。
- 同検証の「毒+ベニ+ラウ」第三波・seed 0は初回・再戦とも伝染5、漏れ6、死亡時の拡散10、接触3。各件を台本と照合し、最終HPも一致。完了印 `RAU_CHECK_OK`。頭なしとVulkan描画の両方で完了。
- 既存の `PoisonAxisCheck.tscn` も `POISON_AXIS_CHECK_OK` に到達。
- 旧 `StagingEffectCheck.tscn -- --effect=poison-transfer` は伝染・漏れ・再開中断の検査を通過後、共通末尾のポップアップ数確認（533行「再開で表示を破棄」）で失敗。このポップアップ処理は今回変更していない。旧総合検証全体の成功とは扱わない。
- Godot終了時にFontAdvanced / CanvasItem / ObjectDB等の解放診断が残る。完走・件数・画面保存の成功は上記の印と保存画像で確認。

画面: [接触](effects-captures/0-touch.png) / [伝染](effects-captures/0-transfer.png) / [漏れ](effects-captures/0-leak.png) / [死亡時の拡散](effects-captures/0-contagion.png)。同フォルダーの `1-*.png` は敵側に立ったときの反転確認。
