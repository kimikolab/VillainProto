# エレの待機・哀歌の演奏モーション（2026-10-27）

ユーザー依頼による作成。2026-10-27、比較動画を確認したユーザーが「A+Cで反映してください」と選択。待機A＋スキルCを通常プレイに導入した。

## 元絵と生成

- 待機：現在の A `DemoApp/assets/portraits/battle/ere_idle_right.png`。
- スキル：現在の C `DemoApp/assets/portraits/battle/ere_singing_idle_right.png`。
- `design/IDLE_LOOP_COMFYUI_GUIDE.md` に従い、`tools/comfy_idle_loop/idle_loop.py` の gen / check / 必要なら recut を使用する。
- Wan2.2 I2V 14B high / low fp8、640×960、81コマ、16fps、20steps、CFG3.5、shift8。Lightning・始点終点固定は使わない。
- 待機の seed は2022 / 5055、演奏は2022 / 3033。元絵・プロンプト・条件を同じにして比較する。
- 実行したAPI形式のワークフローは各 `*-workflow.json`、実行IDは `jobs.json`。終了時に保存する `*-history.json` が切り出しと出力の記録。

待機は膝の屈伸と呼吸に、肩・頭・両腕・楽器が繋がって動く。髪・ベール・袖・裾は遅れて揺れる。
演奏は右肘と手首で弦を明確に往復し、胸を上げて歌い、次のフレーズへ戻る。楽器の形と握り、両足の接地は保つ。
新しい音符・魔法・文字は生成へ焼き込まず、既存の文字なしターンギフト演出をゲーム側で重ねる。
購入済みの背景・汎用エフェクトには、今回必要なキャラクターの全身連番に該当する素材はないため、既存の承認済み立ち絵を原本にする。

## ゲームとの接続

通常待機は既存の `idle_loop/ere/`、歌唱中は `idle_loop/ere_singing/` を16fpsで単発再生する。保存先名は既存の待機読み込みと共有するが、歌唱だけは巡回せず終端を保つ。
演奏開始は先頭コマから、1フレーズ（0.8〜3秒）を再生する。2体へ続けて音を渡す間も巻き戻さず終端を保ち、実際の追加手番の開始で待機へ戻す。テクスチャと立ち絵シェーダーを同じコマにし、元絵の表示高・足元を維持する。
採用前の候補は `EreMotionCheck` の引数で外部の連番を読み、通常の採用ディレクトリを上書きせずに比較できる。
モーションが無ければ従来の静止画に戻る。死亡・勝利・再戦では待機／演奏を止め、蘇生後は待機へ戻す。

```powershell
Godot_console.exe --headless --path DemoApp res://EreMotionCheck.tscn -- --idle-dir=D:/comfyui/output/VillainProto/idle_loop/<待機候補>_rgba --song-dir=D:/comfyui/output/VillainProto/idle_loop/<演奏候補>_rgba
# 実際のゲーム表示を16fpsで連続撮影
Godot_console.exe --path DemoApp --fixed-fps 16 res://EreMotionCheck.tscn -- --idle-dir=<待機候補のフォルダ> --song-dir=<演奏候補のフォルダ> --capture-dir=<画像の保存先>
```

## 比較と採用

制作手順の「最後の1本はユーザーが動画を見て選ぶ」に従う。A/Bは待機、C/Dはスキルの演奏。
数値だけで自然さを決めず、足の滑り・楽器と指の崩れ・前傾・継ぎ目を確認する。
選択されたA＋Cを導入し、`prompts.json` の adopted と `candidates.json` に採用版・コマ数・導入先を記録した。


## 生成・検査の結果

| 案 | 版・連番 | コマ | 備考 |
|---|---|---:|---|
| 待機A | `ere_idle_v1_s2022_final_aligned` | 40 | 16fps・2.50秒。末尾6コマの継ぎ目補修、全体を上へ1pxだけ位置合わせ。靴底949〜950（元絵949）、頭頂の上下9px、継ぎ目1.17x、緑の残り0。 |
| 待機B | `ere_idle_v1_s5055` 系 | 16→45 | 32〜64コマで区間を探し直し、末尾6／12コマを補修したが継ぎ目が目立つため採用候補から除外。 |
| スキルC | `ere_skill_v1_s2022_aligned` | 48 | 16fps・3秒の単発。頭頂の上下11px、靴底951固定（元絵951）、緑の残り0。 |
| スキルD | `ere_skill_v1_s3033_aligned` | 48 | 16fps・3秒の単発。頭頂の上下9px、靴底951固定、緑の残り0。Cより楽器と腕の変化がやや大きい。 |

演奏を自動でループにすると、動作後の小さい動きの区間が選ばれた。
スキルは周期ではなく、生成元動画の先頭48コマを `ImageFromBatch` で選び、既存のBiRefNet＋クロマキーで透過した。
演奏の始点・終点はループとして繋がないので、`idle_loop.py check` が出す演奏のwrap比は採否に使わない。
身体・腕・楽器が動く1フレーズを本番で1回だけ再生し、そのまま音の受け渡しへ繋ぐ。

比較時の連番は `candidates/idle-A`・`candidates/skill-C`・`candidates/skill-D` に保存。
出力タグ・コマ数・先頭コマのSHA-256と採用済みの導入先は `candidates.json`。

- DemoAppビルド成功。
- `EreMotionCheck` はA＋C、A＋Dで Sprite／シェーダー／体格／接地／左右／倍速／停止／死亡／蘇生／勝利を検査。
- 本番の `Main` でも、演奏後に2体へ渡す間の終端保持、先の受け渡しの後片付けが次の歌唱を消さないこと、実手番で待機へ戻ることを検査。
- Forward+で両組み合わせを実描画。撮影は `.tmp/ere-motion-game-C/` と `.tmp/ere-motion-game-D/`。
- 既存の待機・状態差分・炎・移動・エレ歌唱・ギフトの検査も通過。終了時のGodotのリソース解放警告は残る。
- `compare` と `dump` は既存の生成物と一致、`audit` はずれ0件。戦闘規則・数値は変更しない。

## A＋Cの反映（2026-10-27）

- 待機A：`DemoApp/assets/portraits/battle/idle_loop/ere/`、40コマ・16fps・2.50秒のループ、26.1MB。
- スキルC：`DemoApp/assets/portraits/battle/idle_loop/ere_singing/`、48コマ・16fps・3秒の単発、31.5MB。
- 比較用のバックアップと導入元を全コマのSHA-256で照合してから、規定の `idle_loop.py install` で導入。導入後も全88コマのRGBA画素が採用候補と一致することを確認した。
- `IdleLoopCheck` に既定読み込みの40／48コマ・寸法・左右・接地・単発の終端保持・待機への復帰を追加。
- `EreMotionCheck --installed` は候補差し替えを使わず、通常プレイと同じ読み込みで検査する。

```powershell
Godot_console.exe --headless --path DemoApp res://EreMotionCheck.tscn -- --installed
Godot_console.exe --path DemoApp --fixed-fps 16 res://EreMotionCheck.tscn -- --installed --capture-dir=C:/works/VillainProto/.tmp/ere-motion-installed
```


導入後の検証：ビルドは警告0・エラー0。`IdleLoopCheck` / `PortraitStateCheck` / `FireCheck --replay` / `MovementPortraitCheck --verify` / `ErePortraitCheck` / `EreGiftCheck` はすべて合格。
`EreMotionCheck --installed` は頭なし・Forward+の両方で `installed=True idle=40 song=48` と合格し、候補差し替えを使わない通常読み込みを確認した。
通常読み込みの実画像は `.tmp/ere-motion-installed/`。`compare` / `dump` は既存生成物と一致、`audit` はずれ0件。Godot終了時の既存のリソース解放警告は残る。
