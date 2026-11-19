# 第482期 報告 —— 素材の道具（OpenAI の画像 API で絵を作る）

指示書: `design/PHASE482_ART_TOOL_SPEC.md`。この期は道具と候補の絵。規則・盤・駒・BattleCore は触らない。push はしない。

## Phase 0 —— 調べたこと（2026-11-19）

### 0-0. 先に言うこと（止まる所）

- **`OPENAI_API_KEY` がこの機械に無い。** プロセス・ユーザー・マシンの環境変数の 3 か所とも未設定・リポジトリに `.env` も無い。
  道具・受け口・検査は API を呼ばずに作れるので進める。**候補の絵を作る段（指示書 §5）は、キーが入るまで止める**（§1-1）。
- **敵の顔は、1 章に出る駒の大半にもう絵がある**（§0-3）。「仮の字」が出るのは 7 種だけ。指示書 §5-1 の 6 種のうち、字が出ているのは砦の守将だけ。
  今ある 15 枚は味方の立ち絵と絵柄が違う（暗い地・厚塗りの正面の顔）ので、「違いがわからない」の原因は字ではなく、絵柄と色が似通っていることの方かもしれない。
  指示書どおり 6 種の候補は作るが、見比べの絵には今の顔も並べる。

### 0-1. OpenAI の画像の API（公式の資料から・developers.openai.com）

資料は要約して読む取り込みを通したので、言い回しは写しではない。読めなかった所は「未確認」と書く。

| 項目 | 資料で読めたこと |
|---|---|
| モデル | `gpt-image-2.6-sunburst` ／ `-flare`・`gpt-image-2.5-sunburst` ／ `-flare`・`gpt-image-2`・`gpt-image-1.5`・`gpt-image-1-mini`・`chatgpt-image-latest`。`gpt-image-1` は 2026-10-23 に終了。編集の口の既定は `gpt-image-2.5-sunburst`（sunburst ＝ 編集の精度・flare ＝ 速い日常の生成） |
| 生成の口 | `POST /v1/images/generations`: `model`・`prompt`（32,000 字まで）・`n`（1〜10）・`size`・`quality`（low ／ medium ／ high ／ auto ほか）・`background`（transparent ／ opaque ／ auto）・`output_format`（png ほか）・`moderation`。返りは `data[].b64_json` と `usage`（トークン数）。URL は返らない |
| 編集の口 | `POST /v1/images/edits`（multipart）: `image`（1 枚）／ `image[]`（複数・16 枚まで・各 50MB 未満）・`mask`・`prompt`、ほかは生成と同じ。`input_fidelity` は `gpt-image-2` では書かない（常に高い）。2.5 ／ 2.6 での扱いは未確認 |
| 透過 | `background: "transparent"` ＋ `output_format` が png か webp。`gpt-image-2` では「プレビュー」。2.5 ／ 2.6 での対応は未確認 → 道具は「頼んでみて、α が無ければ無地の背景を自分で抜く」の 2 段 |
| 大きさ | 全モデル: `1024x1024`・`1536x1024`・`1024x1536`・`auto`。`gpt-image-2` 以降は 16 の倍数の自由な大きさも可 |
| 料金 | トークン課金。`gpt-image-2` ／ 2.5 ／ 2.6: 文の入力 $5・絵の入力 $8・絵の出力 $30（100 万トークンあたり）。`gpt-image-2` の 1 枚の目安（1024² ／ 1024×1536）: low $0.006 ／ $0.005・**medium $0.053 ／ $0.041**・high $0.211 ／ $0.165。**2.5 ／ 2.6 の 1 枚の目安は資料に無い**（返りの `usage` から数えるしかない）。編集は参照の絵のぶん入力のトークンが乗る（1 枚あたりの表は無い） |
| 制限 | 2.5 の既定: いちばん下の段で 1 分 20 枚。「組織の確認（Organization Verification）が要ることがある」と手引きにある |
| 断られたとき | `error.code = "moderation_blocked"`（HTTP の番号は資料に無い・SDK の例からは 400 らしい）。「文か入力を変えずに自動でやり直すな」とある |

**決めたこと**

- 既定のモデルは **`gpt-image-2`・品質 medium**。理由: 1 枚の料金の目安が資料に載っているのはこれだけ（指示書「料金が資料から読めない場合は止まる」）。
  2.5 ／ 2.6 は `--model` で指定でき、料金は返りの `usage` から数える。モデルと単価は `tools/artgen/config.json` の 1 か所。
- 見積もり: 立ち絵 1024×1536 ＝ 約 $0.04・顔 1024² ＝ 約 $0.05。§5 の 28 枚で **約 $1.3〜1.5**（編集 3 枚は参照の絵の入力ぶんが少し乗る）。上限の 40 枚でも $2 前後。
- 料金は「読めた」と判断して先へ進む。口が実際に使えるか（組織の確認が要るか）は、キーが入って最初の 1 枚で分かる。

### 0-2. 道具の言語

| | 入っているか | 判断 |
|---|---|---|
| Python | 3.14.3（`requests`・`openai` は無い） | **これで書く。** 通信は標準の `urllib`、絵の並べと背景抜きに Pillow だけ足した（`pip install --user pillow`） |
| Node | 24.14.1 | 通信は楽だが、絵を並べる・抜くのに結局ライブラリが要る |
| dotnet | 9.0.304 | 書けるがいちばん長くなる |

`tools/` には Python の先例（`codex_bridge.py`・`comfy_idle_loop`）がある。

### 0-3. ゲームが絵を読んでいる場所と決まり

- **味方の立ち絵**: `DemoApp/assets/portraits/battle/<id>_idle_right.png`（1024×1536・右向き・透過 PNG。`_ready_right` があればそちらが先）。
  編成用は `DemoApp/assets/portraits/<id>.png`。引く所は `UiKit.FindBattlePortrait`（`DemoApp/UiKit.cs`）。敵は画面の側で左右を反転する。
- **顔**: `UnitFaceIcon.TextureFor(id)`（`DemoApp/UnitFaceIcon.cs`）。探す順は
  `assets/map/enemies/<id>.png`（敵の専用の顔・四角のまま使う）→ `assets/portraits/<id>.png` → `assets/portraits/battle/<id>_idle_right.png`（駒ごとの表 `Framing` で顔を切り出す）。
  128px の丸に縮める。**どれも無ければ字**（部隊の印なら名前の頭 1 字や「敵」・席の顔なら駒の名前の頭 1 字）。
- **敵の顔が出る所**（全部 `TextureFor` を通る・絵を 1 か所に置けば全部に出る）:
  盤の旗（`Map3DPiece.Token`・部隊の頭の駒）・2D の札（`Map11Main.Faces.SetBadge`）・遭遇の台と結果（`Map11Main.Clash` の `ClashBoard` ／ `ClashLead`）。
  **戦闘の記録（`HistoryLine`）は字だけで、顔は出していない。**
- **勇者の絵**: `HunterArt`（`Map11Main.Hunter.cs`）が `design/art/enemies/hero/idle-left.png` をディスクから直に読み、顔（上の真ん中）と立ち絵を作って `UnitFaceIcon.Override` で差す。
- **会話の画面**: `StoryScene.Slot.Set`。話者 → `StoryScript.ArtSpeakers`（聖女 → `design/art/story/saint_shadow.png`）→ 勇者 → 駒の立ち絵。`design/art/story/` はまだ無い（聖女は名札と本文だけ）。
- **今ある敵の顔（`assets/map/enemies/`・15 枚）**: accuser・archer・axeman・boss_regular・drifter・droughter・husher・knight・laborer・levy・priest・recruit・warden・yoker・zealot_squire。
  暗い地に厚塗りの正面の顔で、味方の立ち絵（くっきりした線のアニメ調）とは絵柄が違う。
- **顔が無くて字になる駒（7 種）**: champion（聖騎士長）・chaplain（従軍司祭長）・pyromancer（火の魔術師）・fire_archer（火矢の射手）・shock_mage（雷杖の魔導兵）・shove_lancer（突き飛ばしの槍兵）・miasma_priest（毒霧の司祭）。

### 0-4. 1 章に出る敵の部隊（既定 `ChapterRule.V480`）

席は 前1・前3・中央・後1・後3 の順。**太字**は顔が無くて字になる駒。旗に出るのは前1 の駒。

| 部隊 | 中身 | 出る所 |
|---|---|---|
| 物見の兵 | recruit・axeman・recruit（3 体） | 1-1 北・1-2 中・1-3 中 |
| 徴募兵 `band_levy` | levy・levy・laborer・levy・levy | 1-1 南と本陣・1-2・1-3 の各所 |
| 傭兵崩れ `band_sellswords` | drifter・drifter・axeman・drifter・axeman | 1-1 の増援・1-2 南・1-3 中 |
| 騎士の小隊 `band_knights` | knight・knight・priest・zealot_squire・zealot_squire | 1-1 南 |
| 盾の列 `band_shields`（峠の関） | warden・warden・recruit・recruit・recruit | 1-1 北 |
| 火の魔術師の部隊 `band_fire` | warden・recruit・**pyromancer**・**fire_archer**・recruit | 1-2 北 |
| 本陣の守り `band_garrison` | **champion**・knight・priest・archer・knight | 1-2 中・1-3 中（野営地の守り） |
| 砦の守将 `band_garrison_heavy` | **champion**・**champion**・**chaplain**・archer・knight_g | 1-2 中 |
| 弓隊 `band_archers` | recruit・recruit・priest・archer・archer | 1-3 北 |
| 雷杖の部隊 `band_shock` | warden・recruit・**shock_mage**・archer・recruit | 1-3 北 |
| 槍兵の部隊 `band_lance` | **shove_lancer**・**shove_lancer**・priest・recruit・recruit | 1-3 南 |
| 勇者 | boss_regular（白雷の勇者 ライゼル） | 1-3 |

指示書 §5-1 の 6 種と駒の id の対応: 徴募兵 ＝ `levy`・物見の兵 ＝ `recruit`・傭兵崩れ ＝ `drifter`・騎士 ＝ `knight`・弓兵 ＝ `archer`・砦の守将 ＝ `champion`。
旗が字になっているのは 本陣の守り・砦の守将（champion）と 槍兵の部隊（shove_lancer）の 3 つ。
