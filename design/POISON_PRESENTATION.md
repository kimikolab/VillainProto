# 毒軸の演出

2026-10-22、[演出メモ](CODEX_BRIEF_POISON.md)をDemoAppの通常再生へ接続した。毒は紫系、紅蓮は赤、吸血は赤い光の粒で表現する。

## 見せ方

| 対象 | 表示 |
|---|---|
| 毒の在庫 | 現在の立ち絵のアルファ形状に沿った嚢。残量が多いほど数と大きさが増す。描画数は固定上限で抑える |
| 毒の刻み | 実際のStatus通知に合わせて嚢が同時に弾ける。ターン頭の複数回の刻みは、その区間の実在する毒の通知数だけ波を重ねる |
| 次の刻みの見込み | 敵の表示上の毒がHP以上なら輪郭を紫に脈動させる。回復や吸収で条件を外れれば消す。毒に強い駒は対象外 |
| スィド | 開戦時の付与から濃紫の膜。自身の毒が増えるほど厚くなり、Venom通知で殴った敵へ量に応じた毒流が返る |
| ヴィオ | 仲間の毒が紫黒の塊として口へ流れ込む。AttackAfterまたは実際の攻撃力通知に従い、50と150で立ち絵が変わる。四足の跳躍と上下から閉じる牙で噛みつく |
| ヴィオの吸血 | BloodSip通知ごとに味方の首元から口へ赤い光の粒が流れ、味方が小さく揺れる。HPはHpAfterだけで合わせ、被弾の数字やDamageを作らない |
| グザ | 袋口の常時の小さい漏れと、圧力を受ける差分。Vent通知の連続部分を束ねて実対象へ瘴気を流す。量と対象数が増すほど霧が濃くなる |
| 返り毒 | Backlashの書き手グザから線を出さず、直前の実攻撃・被弾通知から殴った敵を特定して、殴られた味方へ飛沫を飛ばす |
| 毒に強い駒 | 残量は嚢と状態アイコンに残す。刻み通知がなければ弾けさせない |
| ベニの紅蓮 | 既存の火の花弁に赤紫の奔流と毒煙を重ね、放出量が多いほど大きくする |
| 大きな数 | 状態アイコンの数値を万・億で4文字以内へ縮める。層そのものや台本は書き換えない |

新しい説明テロップは追加していない。すべてBattleEventの表示で、戦闘の規則をDemoAppへ複製していない。毒の刻みの書き手は推定しない。

## 立ち絵差分と素材

生成はCodex内蔵image_gen。採用済みの右向き待機立ち絵を参照した。

- [ヴィオ 第一段階](art/poison-axis-v1/vio_fed-v1.png): `DemoApp/assets/portraits/battle/vio_fed_idle_right.png`。1536 × 1024。
- [ヴィオ 第二段階](art/poison-axis-v1/vio_gorged-v1.png): `DemoApp/assets/portraits/battle/vio_gorged_idle_right.png`。1536 × 1024。
- [グザ 噴き出し](art/poison-axis-v1/guza_vent-v1.png): `DemoApp/assets/portraits/battle/guza_vent_idle_right.png`。1024 × 1536。
- [生成プロンプト](art/poison-axis-v1/image-prompts.txt)。

ヴィオは硬い腹甲と喉に毒が満ちる形とし、凶悪な毒竜の印象を保った。通常の編成・戦闘待機の原本は変更していない。RGBA生成原本のまま保存し、画像の拡縮や背景除去の後処理はしていない。

購入素材一覧と実ファイルのsmoke_light / smoke_toxicを確認。EffectBlocks v4の `assets/smoke/smoke_toxic.tscn` を噴き出し・返し・紅蓮に採用した。発動ごとにプロセス材と描画材を複製し、色・粒数・寿命・大きさを調整する。元の共有リソースは書き換えない。購入素材がない環境でも、独自の飛沫と嚢で表示する。
泡と膜には立ち絵の輪郭を読む専用シェーダーを使い、地面の煙だけにならないようにした。

## 再生と検証

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --path DemoApp res://PoisonAxisCheck.tscn
Godot_console.exe --path DemoApp res://PoisonAxisCheck.tscn -- --capture
Godot_console.exe --path DemoApp -- --demo-preset=試遊・毒 --demo-playtest-stage=0 --demo-autoplay
```

専用チェックは成長の境界、回復による予告解除、衣の保持者、刻みの波数、返り毒の向き、大きな数、一時停止・倍速・連撃の対象・死亡・勝利・再戦・吸収中の中断・跳躍中の中断を確認する。成功行は `POISON_AXIS_CHECK_OK`。

- ビルド成功、エラー0件。
- 専用の頭なし検証とVulkan描画で成功行を確認。
- 「試遊・毒」「試遊・毒 席替え」「試遊・毒 紅」「試遊・ヴィオ 台B」× ボス・近衛・大隊の12戦がすべて `DEMO_SMOKE_COMPLETE` へ到達。seedは0。勝敗を合格条件にせず、台本の完走で確認した。
- 実台本でヴィオの最大3連撃とBloodSipの発生を確認。
- `compare` の変更前後で表が完全一致、`audit` は整合ずれ0件。
- 既存の `BeniMioCheck --verify` は成功。
- 既存の `LiliCheck --verify` も成功。最終HPの照合がDamage/Healしか見ず、呑み込み後のDeathを落としていたため、検証側でDeathを含めて照合するように修正した。
- 既存チェックや通常アプリの終了時にGodotのリソース解放診断が出る場合がある。上記は各検証の完了行で判定した。専用チェックはツリーを片付けてから終了する。

## 確認画像

- [泡・衣・ヴィオの成長](art/poison-axis-v1/captures/stock-mantle-growth.png)
- [グザの噴き出し](art/poison-axis-v1/captures/guza-vent.png)
- [吸収](art/poison-axis-v1/captures/vio-drain.png)
- [スィドの毒返し](art/poison-axis-v1/captures/sid-retort.png)
- [毒の刻み](art/poison-axis-v1/captures/poison-burst.png)
- [赤い光の吸血](art/poison-axis-v1/captures/vio-blood-light.png)
- [紅蓮](art/poison-axis-v1/captures/beni-guren.png)
- [跳躍](art/poison-axis-v1/captures/vio-pounce.png)

現在は静止差分・シェーダー・粒子・位置移動による演出。待機ループの連番は今回追加していない。
