# エレ：弦を弾く演奏モーションの強化（2026-10-28）

ギフト前の3秒が待ちに見えるという会話を受け、ユーザーから演奏モーションの作成依頼。
右手の小さい動きに留まっていた採用Cに対し、前腕・肘・手首で弦を明確に往復する2候補を作る。

- 原本：承認済みの `DemoApp/assets/portraits/battle/ere_singing_idle_right.png`。
- `design/IDLE_LOOP_COMFYUI_GUIDE.md` と `tools/comfy_idle_loop/idle_loop.py gen` を使用。
- 新しいプロンプト項目は `ere_performance_v2`。既存の `ere` / `ere_singing` の採用情報は保持。
- Wan2.2 I2V 14B high / low fp8、640×960、81コマ、16fps、20steps、CFG3.5、shift8。
- seed 2022 / 3033。同じ指示と原本から作り、見える演奏の大きさ・自然さを比較する。
- 右腕で3回の明確なダウン／アップストローク、指で弦を押さえる左手、上半身の呼吸と最後の一弾き。
- 楽器は一つの硬い物体として維持し、足元・顔・衣装・向きを固定。音符・光・文字は焼き込まない。
- 演奏はループではなく48コマ／3秒の単発。自動ループ抽出の静かな区間を採らず、元動画の連続48コマを透過する。
- `postprocess.py` が既存のBiRefNet＋クロマキーを使う。フレームの間引きやループ用の末尾混合はしない。
- ComfyUIは共有のため再起動・他の仕事の中断をしない。待機Aと採用Cの通常ディレクトリは比較中に上書きしない。

生成IDは `jobs.json`、実行したワークフローは `*-workflow.json`、実行結果は `*-history.json`。

## 候補と検品

| 候補 | 内容 | 連番・接地 |
|---|---|---|
| A（seed2022） | 右手が大きく上がり、弦を横切って戻る。俯いて弾いたあと顔を上げ、後半は目を開く。演奏の読みやすさを優先する場合の推奨。 | 48コマ／16fps／3秒。足底951固定、元絵951。位置補正なし、緑の抜け残り0。 |
| B（seed3033） | 目を閉じたまま、手首で弾き、左手で弦を押さえ直す。Aより控えめで、歌唱に集中した雰囲気。 | 48コマ／16fps／3秒。足底951固定、元絵951。位置補正なし、緑の抜け残り0。 |

自動ループ抽出はAを42〜72、Bを21〜69コマから選び、どちらも動きが小さいという報告だった。
今回は単発の演奏を作るため、その抽出結果は使わず、両方の元動画の0〜48コマを採った。
右手が上下するAと、目を閉じたBの違いは動画で確認する。腕・楽器を部位ごとに変形する補正は使わない。

連番は `candidates/A/` と `candidates/B/`。元絵と採用CのSHA-256は `canonical-before.json` に記録し、
制作後に原本と既存48コマが変わっていないことを照合した。

[比較GIF](performance-CAB.gif)／[高画質の比較動画](performance-CAB.mp4) は、左から現行C・新A・新B。
上段は全身、下段は右手とリュートの拡大。0.5秒の待機→3秒の演奏→0.5秒の終端保持→0.5秒の待機。
MP4は72コマ／16fps、GIFも合計4.5秒。確認用に繰り返しているが、実際の演奏は単発。

## ゲーム上での確認

- DemoAppビルド成功、警告0・エラー0。
- `EreMotionCheck` はA／Bの両方で、頭なしとForward+の実描画に合格。
- Spriteと立ち絵シェーダーの同一コマ・体格・接地・左右反転・倍速・停止・死亡／蘇生・勝利を検査。
- 本番Mainの経路で、演奏の終端保持→2体への受け渡し→追加手番→待機への復帰を検査。
- 実描画の連番は `.tmp/ere-performance-A-game/` と `.tmp/ere-performance-B-game/`。
- 既存と同じGodot終了時のRID／ObjectDB警告は残る。再生の検証は両方とも `ERE_MOTION_CHECK_OK`。

```powershell
Godot_console.exe --headless --path DemoApp res://EreMotionCheck.tscn -- --idle-dir=res://assets/portraits/battle/idle_loop/ere --song-dir=C:/works/VillainProto/design/art/ere/performance-v2/candidates/A
```

2026-10-29、ユーザーは比較の結果、既存Cの自然さを選択。新しいA／Bは採用せず、通常の `ere_singing` はCのまま継続する。
通常立ち絵との違いを大きくするより、演奏開始時にエレの周りへ音楽のエフェクトと光を出す方向へ変更。
開始エフェクトの記録は `design/ERE_GIFT_PRESENTATION.md`。待機Aと採用Cの連番は変更していない。
