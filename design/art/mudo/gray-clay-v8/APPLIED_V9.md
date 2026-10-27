# ムドのグレーの泥人形・ゲーム適用（2026-10-28）

## 適用画像

| 用途 | 制作原本（このフォルダー） | ゲーム内 |
|---|---|---|
| 編成選択・勝利 | `mudo-selection-front-v9.png` | `DemoApp/assets/portraits/mudo.png` |
| 通常待機 | `mudo-idle-right-v9.png` | `DemoApp/assets/portraits/battle/mudo_idle_right.png` |
| 暴発中 | `mudo-arm-erupt-gray-clay-v9.png` | `DemoApp/assets/portraits/battle/mudo_erupt_idle_right.png` |

編成選択は中央の滴を取り除いた修正版。戦闘用2枚にも同じ局所修正を加えた。
通常待機と怒り姿はそれぞれ組み込み `image_gen` でv8を編集し、灰色の泥・暖色の反射・寒色の影を保持。
編集プロンプトは `game-battle-v9-cleanup-prompt.md`。生成原本も保持。

## 接続

- `MudoPresentation` が書き手ムドの `Highlight` と続く割り込みの記録を読む。台本の順序と打数は変更しない。
- `BattlePawn3D.Mudo` が暴発中だけ腕の大きい立ち絵へ切り替える。最後の打撃・回復まで表示して通常姿へ戻す。
- キャンバスの違いは実画素高（待機1413px／怒り963px）で補正。足元の余白（23/1515／69/1166）も補正する。
- 死亡・勝利・中断・再戦では通常へ戻す。敵側は左右反転。表示の停止・再開・速度変更を既存の再生時計に接続。
- ムドの泥の揺れと「−1」はグレー系へ変更。呪い全般の紫色は保持。
- 待機は静止画。BattleCore／BattleSim／生成docsの変更は無し。

## 購入素材の確認

`GodotApp/ASSET_RESEARCH_AND_PLAN.md` と購入原本の EffectBlocks v4 の
`assets/impacts/impact_1.tscn`、`assets/smoke/smoke_light.tscn` を確認。
今回の主体は立ち絵の差し替えで、発光する着弾・煙の追加は不要なため不採用。
泥の揺れは既存 `HexMudFx.Stain` のインスタンスに色を設定して再利用。購入原本・共有素材は編集していない。

## 検証

- DemoApp増分ビルド：警告0・エラー0。初回には未変更のBattleCoreの既存警告2件。
- `MudoCheck`：停止していた攻撃力0のボルグ、規定呪いのボス・本編第4波の初回と再戦が完走。
  暴発10／4件、弱体30／10件、全打撃の変身区間・最終HPが一致。
  別の反撃が挟まる区切り・単発・未知の書き手も検査。
  変身時の絵・足元・胴の身長、停止・2倍速・敵の反転、死亡・中断・再戦の後片付けを確認。
- `NineWaveCheck`：全11波の選択・再生・再戦が `ok=True`。
- `compare`：出力73行が `docs/balance.md` と完全一致。`audit`：ずれているファイル0件。
- GPU実画面：`output/mudo-applied/formation.png` とボス戦を収録。
  ボスは397通知・65攻撃を消費して6ターンで勝利。`boss-seed0.mp4`、拡大半速の `mudo-focus-half-speed.mp4` を保存。
- Godot終了時の既存Font／CanvasItem／ObjectDB解放警告は残る。
