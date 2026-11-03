# 作戦マップ：編成と作戦の画面分離

2026-11-03。`CODEX_BRIEF_MAP_SCREEN.md` の①と、`PHASE441_WATCH_LOG.md` の画面の窮屈さ・結果の見切れに対応。

## 画面

- 編成：上に各隊の枚数・HP・空席と「出撃」、左に手札、中央に拡大した5席と関係線、右に小さい地図と選んだ駒の説明。手札を選んだ時点でも説明が出る。
- 作戦：地図を中心に、時計を上、隊ごとの名前・枚数・HP・任務を下の1行に置く。左右の詳細は最初は閉じる。隊または敵の盤上の札から詳細を開ける。
- 拠点の隊を選んで「組み直す」で編成へ戻る。時計は止まり、閉じても勝手に動かない。道の上の隊は従来の `CanReform` に従い編成不可。
- 次の操作を光らせる案内は、新しい席・隊一覧・出撃ボタンにも追従。行き先の札は隊を選んだときに出す。
- 結果の本文だけスクロールし、「もう一度」を常に下に固定。ウィンドウの高さに合わせた枠にした。

`Map11Main.Layout.cs` に配置・表示切替をまとめた。同じ `SeatDiagram`・手札・操作ハンドラを使い回し、切替で状態を作り直さない。
席は画像拡大ではなく文字・カード・線の座標を拡大して描画する。地図のクリック領域より行き先ボタンを手前に置き、押し間違いを防ぐ。

作業中に入った第442期の2隊化・既定編成・途中到着・横の辺の表示を保持した。隊数と地図は現行の State から読む。
第442期の結果本文のスクロール修正を引き継ぎ、枠の高さ・ボタンの高さを調整して再プレイまで検査した。
同じマスにいる敵の表示位置は斜めにずらし、新しいカメラでも名前が重ならないようにした。
今回の担当では `Map11.cs` / `Map11Orders.cs` / `Map11Session.cs` / `MapGraph.cs` / `MapStage.cs`・BattleCore・BattleSim・docsを編集していない。

素材：購入済み NatureBlocks v1 / EffectBlocks v4 の所在と用途を確認。今回の対象は操作用パネルなので、既存の UiKit・SeatDiagram・地図の形を使用した。

## 検証

| 検査 | 結果 |
|---|---|
| DemoApp Debug build | 警告0・エラー0 |
| 新設 `--map-layout-smoke` | 1-1・1-2の1440×900、1-2の1280×720で `ok=True` |
| `--map-guide-smoke` | 1-1・1-2とも `ok=True` |
| phase0 / phase171 / phase172 / phase173 / graph | 全て `ok=True` |
| map12-smoke / map12-change-smoke | 全て `ok=True` |
| clock / draft / portal / flow＋recall | 完走。時計、編成、制圧、ワープ、第2拠点での組み直し、帰還の各検査が成功 |
| BattleSim compare / dump | 一時ファイルに再出力して生成物と照合、差分0 |
| BattleSim audit | ずれているファイル0件 |
| diff --check | 問題なし |

新しい検査は signal の直接呼び出しではなくウィンドウ座標のマウス入力を使う。canvas_items の表示倍率も通す。
5席への配置、Spaceで編成中の時計が動かないこと、画面切替で駒・HP・行き先・ターンが保たれること、道の上では組み直せないこと、敵・履歴の開閉、長い結果のスクロールとボタン位置、実クリックによるシーン再読込まで確認した。

既存の `--map11-phase176` は第175期の固定期待値との差で `ok=False`。
変更前の HEAD（第441期）の規則・診断ソースを一時コンソールに取り出して再実行しても同じ不合格で、数値の表は差分0行。
先行する②の検証で確認済みの `--map11-verify=800` の既存不合格は `MAP_SCREEN_GUIDE_IMPLEMENTATION.md` に記録している。
戦闘シーンを跨ぐ既存smokeには終了時の FontAdvancedE / CanvasItem / ObjectDB リソース警告が残る。新設layout・guideの検査では発生していない。

ログ：`.tmp/map-screen-split/`。実画面：`output/map-screen-split/1-1/`、`1-2/`、`small/`。
各ディレクトリの `01-formation.png`、`02-operation.png`、`03-selected.png`、`06-result.png` で配置を確認できる。
起動コマンドは `design/COMMANDS.md` に追加した。
