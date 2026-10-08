# 標の循環・演出追加（2026-10-08）

依頼元は `CODEX_BRIEF_MARK_RALLY.md`、台本の契約は `PHASE291_CODEX_MEMO.md` §7。
BattleCore・編成・数値・生成済み docs は変更していない。

## 追加した表示

- ヒサ: 指差して叫ぶ差分、頭上の吹き出し、橙色の活力の光。叫びは視覚演出のみで、回復の効果音を鳴らす。
- `Heal → MarkRally → Heal → MarkRally` を1声にまとめる。直前の `Heal` の汎用回復演出を置き換え、HPと数字は元のイベント位置で反映する。
- 同じターン・同じ攻撃者でも、次の攻撃を挟んだ叫びは別扱い。0回復では立ち絵と吹き出しだけ。対象が1人なら光も1本。
- ソラ: 目の光と敵を追う細線、攻撃の着弾に合わせた受け流し。全体攻撃の1件の `Insight` から台本上の被攻撃者の前に一斉に火花を出す。表示強度は台本の割合を読む。
- ザン: `Reaction` 付きの敵への直接ダメージに短い交差斬撃を出す。返り血を2回目の仇討ちに数えない。標の追加、叫び、羽の既存順序を保つ。
- 再戦・終了・非表示時には吹き出しと音を止め、途中の回復光の完了処理を持ち越さない。

実装: `DemoApp/MarkRallyPresentation.cs`、`Main.MarkRally.cs`、`BattlefieldView3D.MarkRally.cs`。
既存の `Main.cs`、`BattlefieldView3D.ShockMark.cs`、`BattleAttackAudio.ShockMark.cs`、`UiKit.cs` に接続している。

## 検証

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --path DemoApp --headless res://ShockMarkCheck.tscn -- --rally --verify
Godot_console.exe --path DemoApp --rendering-method gl_compatibility --resolution 1440x900 res://ShockMarkCheck.tscn -- --rally --capture-dir=C:/works/VillainProto/output/mark-rally-visual-final
```

- DemoApp ビルド: 警告0・エラー0。
- 左右の陣営×等速／2倍速の4ケース。2本の光、0回復、全員への見切り、再戦初期化を確認。
- 循環／三人組／守り型×ボス／近衛、seed 0、および三人組×近衛 seed 2。各ケース再戦込みの14再生を確認。
- 回復イベント数・正の回復にだけ出る光・見切り・仇討ち・最終HPを台本と照合。叫び回数は engine の独立集計 `RallyFires - RallyNone` とも照合。
- `compare` と `dump` を `output/mark-rally-balance.md`／`mark-rally-units.md` へ再生成し、既存 docs と差分0。`audit` はずれているファイル0件。
- ログ: `output/mark-rally-check-final.log`、`output/mark-rally-visual-final.log`、`output/mark-rally-audit.log`。
- Godot終了時には既存の確認ログにもあるテクスチャ／RID／ObjectDBの解放警告が残る。検証本体は `SHOCK_MARK_CHECK_OK` まで完走している。

## アセット

立ち絵: `DemoApp/assets/portraits/battle/hisa_rally_idle_right.png`。
内蔵 image_gen で既存の `hisa_idle_right.png` を参照し、透過背景を指定して生成。
全身・指先・足元・左右反転時の表示を確認。元画像は置換していない。

生成プロンプト:

> Use-case: identity-preserve. Edit target: attached full-body game portrait of Hisa. Produce one new action-pose sprite variant for the same Japanese fantasy autobattler character, preserving exactly his face, tousled brown hair, scruffy chin, dark teal long coat with gold trim, burgundy scarf, cream shirt, leather belt pouches, trousers and boots and matching detailed anime painting style. Action: he is a cowardly male rallying healer shouting 'Aim at him! Don't fall yet!' from safety: mouth wide open with visibly forceful urgent shouting, one hand cupped by mouth, other arm thrust forward pointing strongly toward screen right; torso leans back, feet braced as if staying safely behind allies, scarf swishes. Maintain full body visible, no crop of fingers or boots, 5% clear margin, portrait 1024x1536 composition. Transparent background with real alpha. Character only, no letters, no speech bubble, no effects, no floor shadow or scenery. The game will draw speech and light itself.

「狙え！」の男性合成音声は、浮いて聞こえるとのユーザー判断で不採用。
音声素材・再生処理・音声専用の検証を削除した。
回復と見切りの効果音は既存の `BattleAttackAudio` の音生成方式で追加した。
