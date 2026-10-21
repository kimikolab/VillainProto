# グザとヴィオの立ち絵 確認記録

2026-10-22、編成用待機と右向き戦闘待機の計4枚を追加。原本・プロンプトはそれぞれ [グザ](../guza/guza-production-v1-notes.md) と [ヴィオ](../vio/vio-production-v1-notes.md) に記録。

## 表示の変更

- `UiKit.PortraitWorldHeight` にグザ2.00、ヴィオ2.25を追加。
- `BattlePortraitBottomPaddingRatio` にグザ78 / 1536、ヴィオ17 / 1024を追加し、足元を地面へ合わせた。
- 画像は既存のファイル名規約で読み込む。戦闘の規則・数値・台本は変更していない。

## 検証

- `dotnet build DemoApp/DemoApp.csproj --no-restore` 成功、エラー0件。警告3件は今回編集していないBattleCoreとBorgAttackClipCheckの行。
- 全4枚がRGBA PNGで四隅のalpha=0。生成原本とゲーム配置先のSHA256一致を確認。
- 「試遊・毒」の[編成画面](formation-game-check.png)で通常画像を目視確認。
- 同編成の第三波で[戦闘開始時](battle/battle-2_5d-01.png)と[毒の状態が出た場面](battle/battle-2_5d-06.png)を目視確認。右向き画像・透過・足元・体格を確認。
- 同編成・第三波の頭なし再生が `DEMO_SMOKE_COMPLETE events=261 turns=5 won=True` に到達。
- Godotの終了時にCanvasItem / ObjectDB / FontAdvancedの解放診断が出る。画像保存はすべて `error=Ok`、再生は上記の完了行で判定した。
- `git diff --check` でUiKitの空白エラーなし。

## 素材

購入素材の一覧とローカルの `DemoApp/PolyBlocks` を確認。NatureBlocksは背景、EffectBlocksは演出部品で、今回の個別キャラクター立ち絵に適合する素材はないため、ユーザー採用絵を参照して内蔵image_genで制作した。
背景・既存の毒エフェクトは変更していない。画面で見える袋口の小さい瘴気は立ち絵の一部。
