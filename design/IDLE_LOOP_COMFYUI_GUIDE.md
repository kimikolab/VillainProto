# 戦闘待機ループ（格ゲーの待機）を ComfyUI で作る手順書

立ち絵（静止画の差分）を描いた側が、そのまま **ComfyUI で待機モーションを作り、DemoApp に入れるまで**の手順。
Claude Code・Codex・人のどれが作業しても同じ結果になるように、操作は `tools/comfy_idle_loop/idle_loop.py` の命令にまとめてある。
採用までの経緯と数値は `design/art/hane/hane-idle-loop-v1-notes.md`（方式の比較・ボルグ／ホタ／ヒヨ）と `design/art/hiyo/hiyo-idle-loop-comfyui-v1.md`（ゲームへの接続・燃焼差分）。

**いまゲームに入っているもの**: ヒヨ（`hiyo`）・ボルグ（`borg`）・ホタ（`hota`）・ホタの燃焼差分（`hota_burning`）・ガルド（`gald`、v4 A・32コマ、記録は `design/art/gald/idle-loop-v4/notes.md`）・ガレ（`gare`、v3 A70・30コマ、記録は `design/art/gare/idle-loop-v3/notes.md`）・ヒビ（`hibi`、v2・元動画の30コマを直接使用、記録は `design/art/hibi/idle-loop-v2/notes.md`）・リィカ（`rica`、B・39コマ、記録は `design/art/rica/idle-loop-v2/notes.md`）・ハギ（`hagi`、修正版B・48コマ、記録は `design/art/hagi/idle-loop-v2/integration.json`）。

---

## 0. 全体の流れ

    元絵を用意 ─→ prompts.json に 1 項目足す ─→ gen（seed 2〜3 本・1 本 約 13 分）
        ─→ check（数値で明らかな失敗を落とす）─→ 必要なら recut（継ぎ目を混ぜる・1 分）
        ─→ 比較 GIF をユーザーに見せて 1 本選んでもらう ─→ install ─→ ビルドとチェック ─→ コミット

**最後の 1 本はユーザーが動画を見て選ぶ。** 数値（頭の上下・継ぎ目・前傾）で落とせるのは「止まっている」「周期にならない」「足が滑る」までで、
「凛々しいか」「腕や脚が自然に揺れるか」は数値では決まらない（ホタの燃焼差分で、頭の上下 1px の版がユーザーに選ばれた）。

## 1. 前提

| もの | 場所 |
|---|---|
| ComfyUI | `D:\comfyui`（RTX 5090 32GB）。起動・停止は §2 |
| Python | **ComfyUI の venv**: `D:\comfyui\venv\Scripts\python.exe`（PIL / numpy / av が入っている） |
| 使うモデル | Wan2.2 I2V 14B（high / low・fp8）・umt5 xxl・wan_2.1_vae・BiRefNet_toonout（ComfyUI-RMBG）。すべて導入済み |
| 自作ノード | `VP Idle Loop Find` / `VP Green Key Alpha`。原本は `tools/comfy_idle_loop/comfy_node/__init__.py`、ComfyUI 側は `D:\comfyui\custom_nodes\VillainProto_IdleLoop\` |
| 出力 | `D:\comfyui\output\VillainProto\idle_loop\<出力名>_*`（リポジトリの外） |
| Godot | `C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe`（頭なし）／ `Godot.exe`（窓あり・撮影用） |

以下、`$PY` は `D:\comfyui\venv\Scripts\python.exe`、`$T` は `tools\comfy_idle_loop\idle_loop.py`（リポジトリの根から）。

```bash
D:/comfyui/venv/Scripts/python.exe tools/comfy_idle_loop/idle_loop.py status
```

`ComfyUI: 動作中  実行中 0 / 待ち 0  自作ノード: あり` なら準備はできている。

## 2. ComfyUI を起動する・再起動する

**この ComfyUI は複数のセッション（Claude Code・Codex・人）が共有している。待ち行列に他人の仕事があるときは、止めない・再起動しない。**
`status` の「実行中 / 待ち」が両方 0 のときだけ再起動してよい。生成を投入するだけなら、他人の仕事の後ろに並ぶので止める必要はない。

- 起動（止まっているとき）: `D:\comfyui\start.bat`、または PowerShell で
  `Start-Process D:\comfyui\venv\Scripts\python.exe -ArgumentList "D:\comfyui\main.py","--enable-cors-header","--listen","127.0.0.1","--port","8188" -WorkingDirectory D:\comfyui -WindowStyle Minimized`。
  1 分ほどで `http://127.0.0.1:8188` が応答する。
- 自作ノードを入れ直す（`comfy_node/__init__.py` を直したとき・ComfyUI を入れ直したとき）: `$PY $T install-node` → 再起動。
- 再起動: 8188 番を待ち受けている python を止めてから起動し直す（待ち行列が空のときだけ）。

## 3. 元絵の条件

- **右向き・全身・足元まで入っている**。DemoApp が戦闘で使う絵そのもの（`UiKit.FindBattlePortrait` が選ぶ絵: `<駒ID>_ready_right.png` があればそれ、無ければ `<駒ID>_idle_right.png`）を使う。
  **連番は元絵と同じキャンバスを縮小したものとして表示される**（足元の余白比と表示の高さを元絵から借りる）。別の絵から作ると足元がずれる。
- 縦横比 2:3（いまの絵はすべて 1024×1536）。生成は 640×960 に縮めて行う。
- **透過 PNG が基本。透過のない単色の緑背景（ホタの通常絵）もそのまま入れてよい**（緑の板に乗せて生成し、緑で抜くため）。
- **緑の衣装・緑の小物があるキャラは、いまの道具では抜けてしまう**（緑を透明にする）。その場合は板の色と抜く色をマゼンタ等に替える改修が先に要る（未対応）。
- 状態差分（燃焼など）も、通常絵と**同じキャンバス・同じ足元**で描く。差分の連番は `<駒ID>_<状態>` の名前で入れる（§8）。

## 4. プロンプトを書く（`tools/comfy_idle_loop/prompts.json`）

1 キャラ（1 差分）につき 1 項目。既存の項目を写して書き換える。組み立ては
`共通の頭（格ゲーの待機） + char + motion + 共通の縛り（足は固定・跳ばない・向きを変えない・sway・同じ顔と衣装・カメラ固定・緑の背景）`。

```json
"hiyo": {
  "still": "DemoApp/assets/portraits/battle/hiyo_ready_right.png",
  "pronoun": "she",
  "char": "姿と構え。髪・服・武器・手の位置を、元絵どおりに具体的に（英語）",
  "motion": "Like a fighting game character, she makes a steady rhythmic springy idle motion in place: her knees clearly bend and straighten, ...",
  "sway": "揺れるもの（髪・リボン・マント・裾）。武器は straight and rigid と書く",
  "neg": "（任意）共通の負のプロンプトに足すもの",
  "note": "経緯"
}
```

`$PY $T prompt <key>` で組み上がった正・負のプロンプトを確かめられる（生成はしない）。

**書き方で結果が決まる。** これまでに分かったこと:

| 書き方 | 結果 | 例 |
|---|---|---|
| 「格ゲーのキャラのように弾む・**膝がはっきり曲がって伸びる**・腰と胴が沈んで戻る」 | 周期のある弾みになる（採用の基本形） | ハネ・ホタ・ヒヨ |
| 「ゆっくり重く」「わずかに」「落ち着いた立ち姿」 | **ほぼ止まる** | ボルグ初版・ヒヨ v3/v4（別セッション） |
| 重いキャラ | 「格ゲーの**重量級**のように弾む」と書けば動く | ボルグ |
| 「弾む」だけ（跳ばない・足は固定を書かない） | 跳び上がって脚が溶ける | ハネ B |
| 弾みが強いと前かがみになる | 状態差分（燃焼）では「凛々しさが無い」と言われた | ホタ燃焼の初版 |
| 「**背筋をまっすぐ・胸を張る・顎を上げる・弾むのは膝だけ**」＋ neg に「猫背・前かがみ・うつむき・疲れ」 | 姿勢が保たれ、腕・脚が自然に揺れる | ホタ燃焼（採用） |
| 弾まず「体重を左右に移す」 | ほぼ止まるか、周期にならない | ホタ燃焼 案B |
| 炎・光などの効果 | 書かない（`No fire, no flames, no glow.`）。演出はゲーム側が重ねる | ホタ燃焼 |

## 5. 生成する（`gen`）

```bash
D:/comfyui/venv/Scripts/python.exe tools/comfy_idle_loop/idle_loop.py gen hiyo --seeds 2022,5055
```

- 1 本 約 13 分。複数の seed はまとめて投入され、順に終わる（2 本で約 26 分）。**seed は 2〜3 本回す**（当たり外れが大きい）。
  これまで動いた seed: 2022（5 体中 5 体）・5055・3033。**4044 はボルグ・ホタで 4 本ともほぼ静止**した。
- 出力名は `<key>_s<seed>`。同じ名前の出力が既にあると止まる（消すか別の seed）。
- 同じseedで指示だけを変えて再試作するときは `gen <key> --seeds 5055,3033 --prefix <別の試作名>` と指定できる。出力は `<別の試作名>_s<seed>` になり、以前の生成物を残せる。
- 終わると `VP Idle Loop Find` の報告が 1 行出る:
  - `loop [a, b) = n frames / seam … = 0.50x … (滑らか)` … 区間と継ぎ目。**1.0x 以下は滑らか、1.5x 超は §7 の recut**。
  - `動きが小さすぎる` … 1 コマの差が 2.5 未満。ほぼ静止。**seed を替える**（あるいは motion を強める）。
  - `周期が見つからない` … 繰り返しの動きにならなかった。**seed を替える**。
- 中身: 元絵を緑の板に乗せる → 640×960 → Wan2.2 I2V 14B（Lightning なし・20 steps・CFG 3.5・shift 8・81 コマ = 約 5 秒）
  → 周期の区間を切る → BiRefNet（toonout）の前景 × 緑のクロマキー → 透過 PNG の連番。生の動画（緑背景）も残る（§7 で使う）。

## 6. 確かめる（`check`）

```bash
D:/comfyui/venv/Scripts/python.exe tools/comfy_idle_loop/idle_loop.py check hiyo_s2022 hiyo
```

| 行 | 見方 |
|---|---|
| 靴底の行 | 元絵と連番が 1px 以内なら足元が合う（ずれたら元絵が違う） |
| 頭頂の上下 | 体の上下の大きさ。参考: ヒヨ 34・ハネ 23・ボルグ 17・ホタ燃焼 1（小さくても手足や髪が自然に揺れていれば採れる） |
| 頭と腰の横ずれ | 平均が元絵から大きく離れる／幅が 25px を超えると前かがみ・猫背を疑う（ホタ燃焼の初版 29.8px） |
| 継ぎ目 | 1.5x 超は recut |
| 緑の抜け残り | 0 が正常 |

確認シート（`<出力名>_check.jpg`・暗い背景と市松の 4 コマ）と**比較 GIF**（`<出力名>_compare.gif`・左が元絵・右が連番）ができる。
**候補の比較 GIF をユーザーに見せて選んでもらう。** 数値で落としてよいのは「動きが小さすぎる」「周期が見つからない」「足元がずれる」「緑が残る」だけ。

## 7. 継ぎ目を直す（`recut`）

```bash
D:/comfyui/venv/Scripts/python.exe tools/comfy_idle_loop/idle_loop.py recut hota_s2022 --crossfade 6
```

生の動画から区間と透過だけやり直す（生成し直さない・1 分ほど）。出力名は `<tag>_final`。
`--crossfade n` はループの最後の n コマを始点の直前のコマへ少しずつ混ぜる（髪がわずかに二重になる代わりに継ぎ目が消える）。
報告に「混ぜた → 継ぎ目 …x」が出る。長い髪が流れ続けるキャラ（ホタ）は周期が閉じにくいので 6 を基本にする。
48 コマ以内で閉じないときは `--max-length 64 --crossfade 8`（ヒヨ 3033 で 2.71x → 0.91x）。
序盤に武器の画面外へのはみ出しなどがあり、後半に完全な周期が残っている場合は、`--skip-head <先頭から除くコマ数>` で探索開始を遅らせられる（既定8）。切れたコマを途中から間引かず、残った連続区間から周期を選び直す。

### 全身の振れ幅を抑える（ガルド v3 の例）

大きすぎる動きを調整する `VP Scale Idle Motion` を用意した（`comfy_node/__init__.py`、OpenCV / numpy が必要）。
元絵と、全身が連動している動画の1周期を入力し、全身の運動を同じ割合で弱めてから元絵を変形させる。
ガルドでは振幅0.30 / 0.45、32コマ・16fps・周期3次で比較している。頭・首・腕・膝も小さく動く。
剣・盾・兜などの `rigid_mask_a/b/c` は部位を停止させるマスクではなく、移動・回転しながら長さと形を保つためのもの。
細かな波打ちが出るときは `elastic_constraints` で周囲を滑らかにつなぐ。
`reference_rgba` に一度だけ透過した元絵を与えると、RGBと透過が同じ写像で動く。
この場合はRGBAの出力3をそのまま保存し、コマごとの背景除去を重ねない（ガルドv4のワークフローを参照）。

ガルドの顔と腕を元絵の固定レイヤーで止めた版は、ユーザーから「首が切られたように見える」と不採用になった。
「揺れすぎない」を「動かさない」に置き換えない。自然さは全身のつながりと動画で確認する。
ヒビv1では振幅縮小後のA/Bが「絵自体が歪む」として両方不採用になった。盾の追跡残差や継ぎ目が良くても、元絵の変形が自然な関節運動に見えるとは限らない。
この場合は変形の係数を調整し続けず、生成動画のコマを直接使う版と比較する（`design/art/hibi/idle-loop-v2/`）。
再現用ワークフロー・マスク・測定は `design/art/gald/idle-loop-v3/`、ノードの数値検証は同フォルダーの `verify-motion-node.py`。
兜の突起の伸縮と剣周囲の波打ち・輪郭の検証は `design/art/gald/idle-loop-v4/`。

## 8. ゲームに入れる（`install`）

```bash
D:/comfyui/venv/Scripts/python.exe tools/comfy_idle_loop/idle_loop.py install hiyo_s2022 hiyo
D:/comfyui/venv/Scripts/python.exe tools/comfy_idle_loop/idle_loop.py install hotabA_s2022_final hota_burning
```

`DemoApp/assets/portraits/battle/idle_loop/<連番名>/<連番名>_idle_NN.png` に入れる（古い連番は消してから入れる）。**コードは触らない**。

- 連番名 `<駒ID>` … 通常の待機絵のときに回る。動作差分（贈り物・煽り・回収など）のあいだは止まり、戻ると再開。死亡・勝利では止まる。再生速度に追従。
- 連番名 `<駒ID>_burning` … その駒に燃焼差分の絵（`<駒ID>_burning_idle_right.png`）があるとき、燃えているあいだ回る。無ければ燃焼差分の静止絵のまま。
- 仕組みは `DemoApp/BattlePawn3D.IdleLoop.cs`（読み込みは `IdleLoopFramesOf` の 1 箇所）。

入れたら:

```bash
dotnet build DemoApp/DemoApp.csproj -c Debug
C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe --path DemoApp --headless res://IdleLoopCheck.tscn -- --verify
C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe --path DemoApp --headless res://PortraitStateCheck.tscn -- --verify
C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe --path DemoApp --headless res://FireCheck.tscn -- --replay
C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe --path DemoApp --headless res://MovementPortraitCheck.tscn -- --verify
```

合否は `IDLE_LOOP_CHECK_OK` / `PORTRAIT_STATE_CHECK_OK` / `FIRE_CHECK_OK` / `MOVEMENT_PORTRAIT_CHECK_OK` の行で読む
（終了時の `RID allocations … leaked` は既存の警告）。

- **`IdleLoopCheck.cs` はコマ数を数えている**（ヒヨ 19・ボルグ 44・ホタ 36・ガルド 32・ガレ 30・ヒビ 30・リィカ 39・ハギ 48）。入れ替えたら数を直す。新しい駒を足したら、その駒の「開戦から連番・同じ高さ」の確認を足す。
- 実戦の画面は窓ありで撮る:
  `Godot.exe --path DemoApp -- --demo-autoplay "--demo-preset=火選り (ヒヨ×ホタ)" --demo-stage=2 --demo-seed=1 "--demo-capture-dir=<出力先>"`（6 枚撮って終わる）。

## 9. 記録とコミット

- 採用した版（出力名・seed・crossfade・コマ数・容量・ユーザーの評価）を `design/art/<キャラ>/` の記録に書く。
  プロンプトを足した・直したら `prompts.json` の `note` と `adopted` も直す。
- **他のセッションの未コミットの変更が混ざっていることがある。** 自分が触ったパスだけを `git add` する（`git add -A` を根でしない）。
- Godot のエディタを開くと連番の `.png.import` ができる。あってもなくても動く（読み込みは `Image.LoadFromFile`）。

## 10. やらないこと・既知の制限

- **始点＝終点（06/07 の Fun Inp）でループを作らない。** 「動かないのが正解」と解かれて止まる（上下 0.3px）。強く書くと跳ぶ。
- **Lightning（4 steps）を使わない。** 構えが流れ、継ぎ目も目立った。
- 生成した区間には元の立ち絵そのものの 1 コマは入らない（動作差分へ切り替える瞬間に小さな飛びがある）。
- 解像度は 640×960。服の細かい模様は少し崩れる。
- **容量**: PNG で 1 コマ 0.6〜0.8 MB（4 本で約 90 MB）。表示中は 1 コマ約 3 MB（非圧縮＋ミップマップ）を使う。
  他のキャラが揃ったら、`IdleLoopFramesOf` での VRAM 圧縮・解像度の見直し・WebP 化をまとめて行う予定（未着手）。
- ComfyUI の画面から触りたいときは `VillainProto/08_戦闘待機ループ_Wan22_I2V.json`（④b だけ替える）と `09_待機ループの切り直し.json`。
  道具の節点を変えたら `$PY $T workflows` で画面用 JSON を書き直す（手で直さない）。
