# 規定ムドの演出（第413期の依頼・2026-10-27）

添付報告書の「Codex への依頼の下書き」に対応。変更は DemoApp 内のみ。

- 既定「速攻 (ボルグ×ムド)」× 精鋭・近衛・seed 0 の T14、台本 index 1085（攻撃力0のボルグ）で停止する不具合を再現。
  前の連番終了を待つ帰還処理が、次の攻撃の踏み込みTweenを消していた。
  攻撃ごとの世代で古い帰還を無効化する。通常・燃焼の入口の両方で更新する。
- ムドの `Highlight` で既存 `HexMudFx` の泥を短く揺らす。
  怒り+1自体は台本に無いため推測表示しない。閾に届かない加算の可視化には別の表示通知が必要。
- ムドから敵への `StatusGain`（`dull`・量1）を、小さな泥色の「−1」で表示する。
  他の浮き文字と同様、数字の寿命は実時間で読みやすさを保つ。泥の動きは停止・倍速に追従する。
  台本・ダメージ・HP・状態の判定は変更していない。

## 素材確認

`CONTRIBUTING.md` と `GodotApp/ASSET_RESEARCH_AND_PLAN.md` を読み、購入済み原本
`D:/Assets/GodotAssets/EffectBlocks v4/PolyBlocks/EffectBlocks/` の
`assets/impacts/impact_1.tscn`、`assets/smoke/smoke_light.tscn` を確認。
前者は発光する着弾、後者は上昇する煙で、今回の小さな泥の揺れには合わないため不採用。
既存の呪いのしみと形・色を揃える `HexMudFx.Stain` を再利用した。
原本・共有素材の編集、新規立ち絵の追加は無し。

## 検証

- `dotnet build DemoApp/DemoApp.csproj --no-restore`：警告0・エラー0（最終増分ビルド）。
- `MudoCheck.tscn`：停止していた近衛戦が完走。
  「試遊・呪い」ボスは暴発10件・弱体30件、本編第4波は暴発4件・弱体10件。
  初回・再戦とも全通知を消費し、表示件数と最終HPが台本に一致。
  停止・2倍速・死亡・再戦・中断時の後片付けと「−1」の色を確認。
- `NineWaveCheck.tscn`：全11波の選択・再生・再戦、`NINE_WAVE_CHECK_COMPLETE ok=True`。
- `BorgAttackClipCheck.tscn`：通常速度で前の振り切りと次の攻撃力0の攻撃が重なる回帰検査を追加し合格。
- `DischargeCheck.tscn` / `ScorePanelCheck.tscn`：合格。
- `compare`：ビルド警告を除く出力73行が `docs/balance.md` と一致（差分0）。生成docsは書き換えていない。
- `audit`：ずれているファイル0件。
- GPU描画：`MudoCheck.tscn -- --mudo-capture=<PNGの絶対パス>` で確認画像を保存できる。

Godot終了時の既存Font／CanvasItem／ObjectDB解放警告は残る。
