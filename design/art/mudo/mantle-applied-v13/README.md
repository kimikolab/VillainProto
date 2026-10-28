# ムド：泥の外套型への適用 v13

2026-10-28。ユーザーが選択した泥の外套型を編成・勝利・戦闘待機・暴発中の立ち絵に適用。

| 用途 | 制作原本 | 適用先 |
|---|---|---|
| 編成・勝利・通常待機 | mudo-mantle-normal-v13.png | DemoApp/assets/portraits/mudo.png、battle/mudo_idle_right.png |
| 暴発中の巨大な腕 | mudo-mantle-angry-shadow-restored-v14.png | DemoApp/assets/portraits/battle/mudo_erupt_idle_right.png |

通常は v11 の画像をそのまま複製。怒り姿は採用 v12 を組み込み image_gen で背景抽出し、外周の透過と外套の開口部を調整した。生成原本は保持。新しい待機アニメーションは作成せず、静止画を既存の再生処理に接続する。

フード〜泥だまりの実画素高は通常1335px（y119〜1454）、怒り933px（y75〜1008）。拳は頭上に出るため、拳を含む全体高で胴を縮めない。足元余白は通常61/1515、怒り16/1024。暴発・連打終了・死亡・勝利・中断の切替は既存処理を利用。

## 内側の陰の復元 v14

v13の背景抽出が外套の内側の陰まで透過したため、ユーザーの指摘を受けて修正。
添付 `codex-clipboard-3c2ef9de-34bc-4ce4-9f7d-7e5c63d60464.png` を一切加工せず `mudo-mantle-angry-shadow-restored-v14.png` として保存・ゲームへ複製した。
元のv12と同じ陰を保持し、追加の画像生成・背景抽出は行っていない。
`MudoCheck` は陰の3箇所が不透明・外側が透明であることを検査する。
v13の透過版は比較履歴として残すが使用しない。

v14検証：ビルド警告0・エラー0、`MUDO_CHECK_COMPLETE ok=True`。
陰の3箇所のアルファ検査と実描画で復元を確認。
修正後の実画面は `output/mudo-mantle-applied/angry-shadow-restored-v14.png`。
同フォルダーの既存動画は修正前v13の記録。

購入素材一覧と EffectBlocks v4 の impacts/impact_1.tscn を再確認。今回は採用立ち絵の差し替えであり、追加エフェクトは不要。既存の泥の揺れを保持する。

## 背景抽出のプロンプト（組み込み image_gen）

```text
Use case: background-extraction.
Edit target: the attached approved angry gray mud mantle game character.
Remove ALL diffuse brown/gray gradient haze and background surrounding the creature. Make every pixel outside the actual mud silhouette completely transparent alpha=0. This includes the huge soft glows between the two arms above the head, outside the fists, and around the puddle, and the empty openings within the mantle. Keep dark interior surfaces of mud, but empty spaces between sheets should be transparent.
Preserve the actual creature exactly: hood, angry face with round O mouth, two enormous fists, stretched clay sheet arms, hollow draped body and puddle hem, gravel, wet gray mineral texture, warm highlights and cool shadows. Same pose, proportions, colors, canvas layout. No redesign, no new anatomy, no added effects. Output a crisp transparent-background game sprite cutout with only a narrow antialiased contour, absolutely no haze, glow, shadow cloud or colored backdrop.
```

## 検証

- `dotnet build DemoApp/DemoApp.csproj --no-restore`：警告0・エラー0。
- `MudoCheck.tscn`：`MUDO_CHECK_COMPLETE ok=True`。攻撃力0の旧停止箇所、ボス・本編第4波の再戦、暴発10/4件・弱体30/10件、台本HP一致を確認。通常への復帰、足元・外套高、停止・2倍速・敵の反転・死亡・中断も合格。
- GPU実画面：編成の通常姿、戦闘の通常姿・怒り姿を確認。外周の霞は戦場に表示されず、外套の空洞も保持。
- 規定ボス・seed0：416通知・65攻撃、6ターン勝利で完走。動画32.07秒。`output/mudo-mantle-applied/` に `formation.png`、`normal-in-battle.png`、`angry-in-battle.png`、`boss-seed0.mp4`、拡大半速 `mudo-focus-half-speed.mp4` を保存。
- `compare` は `docs/balance.md` と差分0。`audit` はずれているファイル0件。BattleCore・BattleSim・生成docsは変更していない。
- Godot終了時の既存Font/CanvasItem/ObjectDB解放警告は残る。コミットは未実施。
