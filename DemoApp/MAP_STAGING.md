# 作戦マップの演出

添付の `CODEX_BRIEF_MAP_STAGING.md` に対応する表示側の実装。進行・戦闘の規則は変更していない。

- `Map11Main.Staging.cs`: 勇者（門番／追う勇者）の対の紋章、開閉する門、中間拠点の取得、拠点の陥落、倒れた旗、炎、追跡の足跡。通しごとに前回見た状態を弱参照の表へ記録し、再描画や戦闘からの復帰で繰り返さない。通知は1.25秒、2倍速では0.625秒。左クリック／Escで消え、進行を遮らない。仮音の差し替えは同ファイルの `MapStagingAudio`。
- `UnitFaceIcon.cs`: 勇者の盾形・王冠・大きな残り歩数、重なる大群の札。連戦の窓は顔札・残る部隊数・大きなHP帯・回復なしの印・大きな選択ボタンを使う。
- `Map11Main.PlaceNames.cs`: 1-3の仮の地名。決まった地名は辞書の右側へ。GraphのID・名前・命令判定は変更しない。
- `Main.RelicCue.cs`: 既存の `BattleEventKind.Relic` の持ち主へ◆。一時停止・再生速度を読み、中断・死亡・再戦時に消す。待ち時間を追加しない。
- `MapStageIllustration.cs`: 章の頭・結果の挿絵枠。現在は各盤の道と拠点を描く仮の絵。章の結果はステージ別の札と合計、討伐／逃げ切りの印を表示する。
- レリックの候補・一覧は画面内の固定枠で折り返し・縦スクロール。金の演出より前に表示する。

## 購入素材

`GodotApp/ASSET_RESEARCH_AND_PLAN.md` と導入済み実ファイルを確認した。EffectBlocks v4の `PolyBlocks/EffectBlocks/assets/fire/fire_light.tscn` を陥落した拠点の炎・煙・火の粉に使用。原本は `D:/Assets/GodotAssets/EffectBlocks v4/`。インスタンスの位置・倍率だけを変更し、素材の点滅スクリプトは外す。遠景で札に埋もれないよう、幾何形状の炎の芯も重ねた。門・札・地面の目印は既存の盤の簡素な形に合わせた。NatureBlocksの植生は長い経路・顔札の視認性を優先して追加していない。購入素材本体は追加・改変していない。

## 確認

- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 警告・エラー0。
- `--map12-smoke` / `--map12-change-smoke` / `--map13-smoke` / `--map13-hero-smoke` / `--chapter1-smoke`: 成功。
- `--map11-clock-smoke` / `--map-layout-smoke`（1-1・1-3）: 成功。
- `BattleSim compare`: 保存済み `docs/balance.md` と出力の差0。`audit`: ずれ0。
- `--map11-verify`（800 seed）は基準値との差で失敗する。HEADのDemoAppソースを `.tmp/map-staging-baseline` へ取り出して独立ビルドし、変更前でも失敗し、レポートが全文一致することを確認した。規則や基準値を演出作業で変更していない。
- レリック付き・無しの頭なし戦闘再生はどちらも完走。終了時にどちらも同じFont/CanvasItem/ObjectDBの解放警告が残る。
- 実画像: `output/map-staging-arrival.png`、`map-staging-guard.png`、`map-staging-fall.png`、`map-staging-siege.png`、`map-staging-intro.png`、`map-staging-chapter.png`、`map-staging-relics.png`、`map-staging-relic-offer.png`。

演出中の画像を撮るには既存の `--map11-capture=<絶対パス>,chase-cue` / `chase-lost-cue`（1-3）、`guard-cue`（1-2）。通常の `chase` / `chase-lost` は演出終了後を撮る。レリックの候補は `relic,offer`、一覧は `relic,book`。
