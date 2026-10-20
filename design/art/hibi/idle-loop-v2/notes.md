# ヒビ 待機モーション v2 — 元絵の変形を外すリテイク

状態: 2026-10-21、`candidate-natural/loop.gif` をユーザーが承認し、「適用してコミットしておいて」の指示でゲームへ適用済み。静止立ち絵は正面v1・戦闘v4を維持。

## 指摘と方式変更

ユーザーからv1のA/B両方について「絵自体が歪んで動いている」と指摘された。
v1は生成動画の動きを光学フローで抽出し、元絵を変形して振幅を0.40へ縮めていた。
盾の剛体制約と周囲の連続変形を使っても、身体や衣装がゴム状に伸縮して見えた。数値の滑らかさだけではこの問題を評価できなかった。

v2はWanが生成した連続コマを直接使う。元絵を変形するノード、振幅縮小、周期のフーリエ平滑化、固定した顔・腕・盾の上貼り、往復再生、補間コマは使用しない。
背景除去と緑の縁の処理は既存手順のまま。切り出した動画フレームに対して幾何変形を加えず、PNGはComfyUI保存物とのSHA256一致を全コマで確認する。

## 今回の再生成と選別

`generate.py` でWan2.2 I2V 14Bを新たに2本生成し、両方とも正常終了した。
640×960・81コマ・16fps・20steps・CFG 3.5・shift 8。今回のプロンプトは `prompt.json`、実際のグラフ・ジョブ・結果は同じフォルダーに保存。

| タグ | 区間 | コマ数 | 頭頂の上下 | 継ぎ目 | 判断 |
|---|---|---:|---:|---:|---|
| `hibi_native_v2_s2022` | [10,49) | 39 | 13px | 1.61x | 元絵の変形はないが継ぎ目が目立つため提示版から除外 |
| `hibi_native_v2_s3033` | [16,59) | 43 | 2px | 2.41x | 動きが小さく、布が画面端へ接触するコマもあるため除外 |
| `hibi_s2022` の生成コマを直接使用 | [33,63) | 30 | 19px | 0.90x | 今回の確認用候補 |

確認用候補は新規生成2本のどちらかではなく、前回の元動画を変形せず切り出し直す方式を選んだもの。
ユーザーが見たv1 Aの32コマとは別物で、振幅縮小や周期平滑化を行う前の30コマ。
元の生成ジョブは `e25555f2-7d2d-4914-be48-63216c852f20`。今回の候補フォルダーにも実際の `workflow.json` と `result.json` を保存している。
元動画は `../idle-loop-v1/source-s2022.mp4`、元プロンプトは `../idle-loop-v1/hibi-s2022-workflow.json`。

## 確認用候補

- `candidate-natural/frames/`: 640×960 RGBA、30コマ。ComfyUI保存物をそのままコピー。
- `candidate-natural/loop.gif`: 全身プレビュー、480×720。共通パレット・ディザなし。
- `candidate-natural/loop.webp`: 透過・可逆圧縮、30コマ、合計1875ms。
- `candidate-natural/detail.gif`: 顔・肩・腕・盾の拡大。
- `candidate-natural/still-vs-loop.gif`: 左が元の静止立ち絵、右がリテイク。
- `candidate-natural/contact.jpg`: 8時刻の確認シート。
- `candidate-natural/metrics.json`: 各コマのSHA256、外部ジョブID、足元・透明背景・継ぎ目。
- `native-s2022/` / `native-s3033/`: 今回新規生成したが除外した2本。候補と混同しない。

30コマ・16fps、周期1.875秒。靴底は元絵と同じ868行で固定、頭頂の上下19px、頭と腰の横ずれ幅14.7px。
画像端接触0、緑の残り0。全身と8時刻のシートで構え・手足・髪の白いメッシュ・盾の外形と衣装のつながりを確認した。
上記は明白な破綻を落とす補助であり、動きの自然さは動画を見たユーザーの評価で決める。

購入済みNatureBlocks／EffectBlocksの一覧と実ファイルを再確認したが、関節の待機アニメーションを直す素材ではないため使用しない。ゲームの背景・演出は維持。
リテイクの提示時点ではプレビューのみだったためゲームのビルドは行っていない。今回の承認後の検証は次節。静止絵の適用時の検証は `../hibi-naval-demo-integration.md`。

再作成: `D:/comfyui/venv/Scripts/python.exe -X utf8 design/art/hibi/idle-loop-v2/preview.py hibi_s2022`
## 採用・ゲーム適用

- `idle_loop.py install hibi_s2022 hibi` で30コマを配置。PNGの圧縮のみ最適化し、画素と透過は提示版と全コマ一致。
- `../verify-naval-integration.py`: `HIBI_NAVAL_INSTALL_OK front=v1 battle=v4 motion=native-v2 frames=30 unchanged_pixels=true`。
- `../naval-installed-manifest.json`: 静止画像2枚と連番30コマのSHA256、元の採用コマの指紋、接地と表示補正。
- `IdleLoopCheck.cs`: 30コマ・640×960・開戦再生・表示高・シェーダー同期・全30コマの循環・倍速・燃焼・死亡停止・蘇生・敵側反転・勝利絵保持を検証。
- `dotnet build DemoApp/DemoApp.csproj -c Debug`: 成功、エラー0、既存の `BorgAttackClipCheck.cs:47` CS8602警告1件。
- `IdleLoopCheck` / `PortraitStateCheck` / `MovementPortraitCheck`: それぞれ `*_CHECK_OK`、終了コード0。
- `FireCheck`: `FIRE_CHECK_OK` の後に既知のGodot/C#終了処理のFATALが再発、終了コード-1073741795。機能確認の到達と正常終了は区別し、既存の終了処理には手を加えていない。
- `capture-game.gd`: Vulkan Forward+の実戦画面で採用PNGの画素指紋を照合し、4枚の撮影中に3種類のヒビのコマを検出。`HIBI_IDLE_V2_GAME_CAPTURE_OK frames=4 distinct_hibi_frames=3`、終了コード0。既存のRID/ObjectDB解放警告あり。
- `game/battle-00.png` から `battle-03.png`: ガレとの並び、盾・手足・透過・接地・HP表示を目視確認。戦闘イベントを一時停止した状態でも待機が回るのは既存の再生仕様。
- `prompts.json`: `adopted` を採用元 `hibi_s2022`・30コマ・16fps・crossfade 0へ更新。再生成用の本文も実際の採用元プロンプトへ戻し、除外した新規プロンプトは `prompt.json` に保存。

ログは `output/hibi-idle-v2-install/`。戦闘ルール・他キャラクター・共通の待機再生ロジックは変更なし。
不採用候補の大きな連番・比較GIFはローカル作業物として保持し、採用版の素材・生成情報・比較と適用記録をコミット対象にする。
