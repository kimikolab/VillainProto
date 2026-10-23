# 演出ビューア

リポジトリ直下の `FxViewer.bat`、または次のコマンドで起動する。

```powershell
Godot_console.exe --path DemoApp res://FxViewer.tscn
```

「駒カタログ」を先頭に表示する。味方は `UnitCatalog.All`、敵は通常波に出る種類を自動走査し、
駒ごとに待機・攻撃・被弾・撃破を用意する。駒の節は既定で閉じ、開閉状態は選び直しても維持する。
「駒」「軸」「検索」は基本動作と登録済みの演出の両方を絞り込む。

味方の実戦は第1・第2波を各1本。編成は試遊 → 版 → 比較の順に本人のいる最初の行を使い、
見つからなければ中央に本人、残りを現役の先頭から埋める。敵の実戦は初登場の波を1本、試遊の先頭編成で回す。
実戦行の右の「見せ場 n」は、本人の個体IDが書き手または対象になっているイベントの件数。
ターン開始と能力・状態の写しを除く。見せ場0の行も残す。集計した台本をそのまま再生する。

再生・停止、速度、左右反転、撮影は第1段と共通。待機は空台本のまま3秒間保持し、停止時間を数えず、速度変更に追従する。
「次の見せ場」は繰り返しOFFなら終端で止まり、ONなら最初の見せ場に戻る。
「視点を調節」でカメラを調整できる。初期ズームは一覧の幅に合わせて0.85倍。
撮影は `design/art/_viewer/<cueId>.png` に保存する。

## 実装

- `FxCatalog.Generated.cs`: `AllUnits`／`ForUnit` と共通の盤面・台本の雛形。駒ごとの登録は不要。
- `FxLivePlayback.cs`: engine で実戦台本を作り、開始時の盤面と engine が振った個体IDを対応付ける。
- `FxViewer.Catalog.cs`: 折りたたみ・絞り込み・実戦台本と見せ場数のキャッシュ。
- 既存の手組み Cue と Check はそのまま。再生は基本動作も実戦も `Main.ApplyEvent` を通す。

生成CueのIDは `unit.<id>.idle`／`attack`／`hit`／`death`／`live.<stage>`。
`FxCatalog.All` は登録済み → 自動生成の順、画面は駒カタログを先頭に並べる。
基本攻撃の対象とHPは試写用の固定台本で、特性の判定や威力の減衰は再計算しない。
特殊行動は実戦の台本で確認する。背景・演出・購入済み素材は本編の導入済みのものを再利用する。

## 検証

```powershell
dotnet build DemoApp/DemoApp.csproj
Godot_console.exe --path DemoApp res://FxViewer.tscn --headless -- --fx-smoke
Godot_console.exe --path DemoApp res://FxViewer.tscn -- --fx-generated-capture
```

`--fx-smoke` は生成を含めた全Cueを一巡し、味方全駒の基本4本、敵の基本動作、
カタ・バサ・ゴルムの見せ場数、絞り込み、開閉状態、待機の時計、シーク、終端と繰り返しを検査する。
生成に失敗した駒は `FX_VIEWER_SMOKE_SKIP <id> <理由>` で報告し、他の駒へ進む。
成功時の `FX_VIEWER_SMOKE_OK` に登録済み・生成・スキップの件数を出す。
合否は従来どおり `_OK` の行で読む（Godot終了時のリソース解放警告とは分ける）。

`--fx-generated-capture` は一覧の全体と、カタ・バサ・ゴルムの基本攻撃と実戦、
巡礼騎士・戦斧兵の基本4本を撮影する。第1段の `--fx-capture` は登録済みの5本だけを撮影する。
