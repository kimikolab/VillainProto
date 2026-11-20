# 第482期 報告 —— 素材の道具（OpenAI の画像 API で絵を作る）

指示書: `design/PHASE482_ART_TOOL_SPEC.md`。この期は道具と候補の絵。規則・盤・駒・BattleCore は触らない。push はしない。

## Phase 0 —— 調べたこと（2026-11-19）

### 0-0. 先に言うこと（止まる所）

- （追記: キーはこの後に入り、候補は §4 のとおり作った。）**Phase 0 の時点では `OPENAI_API_KEY` がこの機械に無かった。** プロセス・ユーザー・マシンの環境変数の 3 か所とも未設定・リポジトリに `.env` も無い。
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

## 1. 道具 `tools/artgen/`（Python・標準の `urllib` ＋ Pillow）

```
python tools/artgen/artgen.py init                                   # 候補の置き場 design/art/_candidates を作る（git に入れない）
python tools/artgen/artgen.py gen   --job foe_face/levy --n 3 --size 1024x1024
python tools/artgen/artgen.py gen   --job saint --n 4 --size 1024x1536 --transparent
python tools/artgen/artgen.py edit  --job gan_quiet --ref DemoApp/assets/portraits/battle/gan_idle_right.png --n 3 --transparent
python tools/artgen/artgen.py rekey --job saint                         # もう作った絵の背景を抜き直す（API なし）
python tools/artgen/artgen.py reject --job saint --file <絵の名前> --why "手が崩れている"
python tools/artgen/artgen.py sheet --job foe_face                   # design/art/_candidates/foe_face/sheet.png
python tools/artgen/artgen.py status                                 # この期の枚数・料金・キーと置き場の有無
python tools/artgen/artgen.py check                                  # API を呼ばない検査（14 項目）
```

| 決まり（指示書 §1） | 道具での形 |
|---|---|
| 1 キー | 環境変数 `OPENAI_API_KEY` を読むだけ。無ければ `ARTGEN_STOP` で止まる（終了コード 2）。記録・帳簿・出力のどこにも書かない（検査で確かめる）。`.gitignore` に `.env`・`.env.*` |
| 2 枚数 | 1 回 4 枚・この期 40 枚（`config.json`）。超えるなら API を呼ぶ前に止まる。帳簿は `design/art/_candidates/_ledger.json` |
| 3 採用はポン | 出力は `design/art/_candidates/<job>/` だけ（`.gitignore`）。ゲームが読む場所には書かない |
| 4 記録 | 絵と同じ名前の `.json` に 文（絵柄 ＋ 依頼）・文の出どころ・参照の絵・モデル・大きさ・品質・背景の扱い・日付・`usage`・料金 |
| 5 参照 | `--ref` は `DemoApp/assets/portraits` と `design/art` の下だけ。`PolyBlocks`・`Bukkbeek`・候補の置き場は断る。送るファイル名は `ref.png`（置き場の道筋を外へ出さない） |
| 6 文 | 絵柄は `design/art/prompts/_style.md` の 1 つ。依頼は `design/art/prompts/<job>.md`（どちらも最初の ```text の囲みの中だけを送る）。作品名・作家名・他作のキャラ名は書いていない |
| 7 断られたとき | `moderation_blocked` を数える。同じ依頼で 2 回（最初の 1 回 ＋ 言い換えの 1 回）断られたら、3 回目は呼ばずに止まる |

- 実行のたびに 1 行: `ARTGEN_DONE job=… 作った n 枚・この期の合計 n／40 枚・今回 約 $…・この期 約 $…・背景 …`。料金は返りの `usage` から数え、無ければ資料の 1 枚の目安。
- 透過: `--transparent` はまず API に頼む。API が受けなければ無地（マゼンタ）の背景で作り直し、縁からつながった背景の色だけを道具で抜く（絵の中の同じ色は残す・縁は 1px なじませる）。
  α が無く、地も無地でなければ、抜かずにそのまま出して記録に書く。**本物の絵での縁のにじみは、キーが入ってから見る。**
- 見比べの絵 `sheet`: 両端に味方の立ち絵（既定 リィカ・ドルガ）・間に残した候補・下の段に 40px の顔と 2 倍。顔の切り方はゲームと同じ（味方は `UnitFaceIcon.cs` の表をそのまま読む）。
  敵の顔の題材では、今の顔（`assets/map/enemies/`）も並べる。地は明るい色と暗い色を半分ずつ（透過の縁を両方で見る）。`reject` で外した絵は並べない。
- 依頼の名前は `題材/小分け` の 2 段まで（`foe_face/levy`）。`sheet --job foe_face` は小分けごとに 1 段。

依頼の文（コミット済み）: `foe_face/{levy,recruit,drifter,knight,archer,champion}.md`・`saint.md`・`gan_quiet.md`・`courier.md`。

## 2. ゲーム側の受け口（採用の絵は置いていない——口だけ）

| 受け口 | 置く所 | 出る所 |
|---|---|---|
| 敵の顔 | `DemoApp/assets/portraits/enemy/<駒の id>_face.png`（四角い顔の絵・丸く切られる） | `UnitFaceIcon.TextureFor` の先頭。今の顔（`assets/map/enemies/`）と字より先。盤の旗・2D の札・遭遇の台・結果の席の顔が全部ここを通る |
| 人物の絵 | `design/art/story/<話者の名前>.png`（`聖女.png`・`伝令.png`。前の期の `saint_shadow.png` も読む） | 会話の画面。駒でも勇者でもない話者を名前で引く。絵があれば「名札のみ」の行でも出る |
| 立ち絵の差分 | `DemoApp/assets/portraits/battle/<id>_<差分>_right.png`（`gan_静_right.png`） | 台本に `ガン:静｜左｜文`（`：` でもよい・候補 `リリ/ヒサ:怒` は全員に掛かる）。絵が無ければ待機の絵。同じ人の差分の入れ替えは、出直さずにその場で替わる |

- 絵はどれもファイルを直に読む（Godot の取り込みを待たない）。置くだけで出る・外すと元に戻る。
- **戦闘の記録は字だけの行で、顔を出す所が無い**（Phase 0）。指示書 §6 の「戦闘の記録」には何も足していない——顔を出すなら行の形を変える話になるので、ポンの判断を待つ。
- 台本（`DemoApp/story/chapter1.txt`）は触っていない。差分の行を書くのは、ガンの静かな絵を採用した後。

## 3. 検査

| 検査 | 結果 |
|---|---|
| `python tools/artgen/artgen.py check` | ○ 14 項目: キーが無い ／ 1 回の上限 ／ 期の上限 ／ 置き場が無い ／ 文が無い ／ 購入素材の参照 ／ 参照なしの edit —— どれも API を呼ばずに止まる。偽の返りで 記録の形・背景抜き・キーが書かれない・帳簿・multipart・断られた回数・見比べの絵 |
| `--story-smoke` | ○ 16 項目（差分の書式の見本 4 行が読める・読めない書き方 3 つを断る）。台本が使う差分の絵: なし |
| 場面の検査 `--map11-capture=<絶対パス>/a.png,m3d-art-hooks`（`Map11Main.ArtHooks.cs`） | ○ 20 項目・写し 9 枚。敵の顔: champion は 字 → 絵 → 字・levy は 今の顔 → 絵 → 今の顔（盤の札と旗も）。会話: 差分の行は 待機 → 差分 → 待機・人物は 名札だけ → 絵 → 名札だけ。仮の絵は検査が作って必ず消す |

## 4. 候補の絵（指示書 §5）—— 30 枚作った（上限 40）・約 $1.59

キーが入ってから作った（2026-11-19）。モデルは `gpt-image-2`・品質 medium。料金は返りの `usage` から数えた: 顔（1024²）は 1 枚 約 $0.055・立ち絵（1024×1536）は 1 枚 約 $0.044・編集は 3 枚で $0.169（参照の絵の入力ぶんが乗る）。断られた依頼は 0。

| 題材 | 作った | 外した | 残した | 見比べの絵 |
|---|---|---|---|---|
| 敵の顔 徴募兵 `levy` | 6 | 3 | 3 | `design/art/_candidates/foe_face/sheet.png` |
| 敵の顔 物見の兵 `recruit` | 3 | 0 | 3 | 同上 |
| 敵の顔 傭兵崩れ `drifter` | 3 | 0 | 3 | 同上 |
| 敵の顔 騎士 `knight` | 3 | 0 | 3 | 同上 |
| 敵の顔 弓兵 `archer` | 2 | 0 | 2 | 同上（3 枚頼んで、API が返したのは 2 枚） |
| 敵の顔 砦の守将 `champion` | 3 | 0 | 3 | 同上 |
| 聖女 | 4 | 0 | 4 | `design/art/_candidates/saint/sheet.png` |
| ガンの静かな版 | 3 | 0 | 3 | `design/art/_candidates/gan_quiet/sheet.png` |
| 伝令 | 3 | 0 | 3 | `design/art/_candidates/courier/sheet.png` |

### 4-1. 外した絵と理由

- **徴募兵の最初の 3 枚**（`20261119-221823_1〜3`）: 絵柄。最初の絵柄の文では写実寄りの厚塗り（肌のしわ・布のざらつき）になり、味方の立ち絵とも合わない。
  ここで絵柄の文（`_style.md`）を直した——「日本のアニメ ／ JRPG の人物の絵・顔はアニメの省略・陰は 2 段まで・写真のような質感なし」。直した文で徴募兵を作り直し（指示書 §4 の「文を直して 1 回だけ」）、残りの題材は全部直した文で作った。
- ほかに外した絵は無い。顔や手の崩れ・武器が体を貫く・文字やロゴ・向きの違い・切れた全身は、見比べの絵と拡大（縁・手元）で見た範囲では見つからなかった。
  **1 枚ずつ原寸で隅まで見たわけではない**——採用の前に、選んだ絵は原寸で見てほしい。

### 4-2. 絵柄は合っているか（正直に）

- **敵の顔**: 陣営の違いははっきり出た。青と銀・人間・手入れされた装備で、味方（緑の肌・錆・継ぎはぎ）とも今の顔（茶色の厚塗り）とも一目で分かれる。
  兵種も 40px で読める: 徴募兵 ＝ 青い布の帽子と長柄・物見 ＝ つばの広い鉄帽・傭兵崩れ ＝ 鉢巻きと斧・騎士 ＝ 閉じた兜と青い房・弓兵 ＝ 青いフードと弓・守将 ＝ 翼の兜と白い外套。
  ただし**味方の立ち絵より塗りが平たい**（味方は布と金属にもう一段の描き込みがある）。40px の丸の中では気にならないが、大きく出す所（遭遇の台の頭の顔）では差が見えるかもしれない。
  **守将は構図が引きで、40px では顔が小さい**（兜の翼と鎧の方が目立つ）。採るなら顔に寄せて切り直すか、寄りで作り直したい。
- **聖女**: ライゼルと同じ陣営に見える（白・金の縁・紺の襟と帯・金の太陽の紋）。フードの影で目は見えず、口元だけ。左向き・腰から上。4 枚とも使える水準で、違いは手の形と袖の量。塗りは味方の立ち絵にいちばん近い。
- **ガンの静かな版**: 顔・服・体格は今のガンのまま、腕を下ろして口を閉じた。3 枚とも同じ人物に見える。耳は「少し伏せる」がほとんど効いていない（立ったまま）。
  今の絵より足の開きが狭くなったぶん細く見える——会話の画面で待機の絵と入れ替えると、背丈と肩幅が少し動くはず。採用の後に受け口で並べて見たい。
- **伝令**: 黒い外套・顔は影・封蝋の巻物。3 枚とも使える。目の形の留め金が思ったより目立つ（悪くない）。

### 4-3. 道具で分かったこと

- **`gpt-image-2` は透過を受けない**（`Transparent background is not supported for this model.`・資料の「プレビュー」は使えなかった）。道具は自動でマゼンタの地で作り直して、自分で抜いた。
- 最初の抜き方には穴が 3 つあった（本物の絵で初めて分かった）: 縁にマゼンタのにじみが 1 列残る ／ 尻尾と外套のあいだの閉じた穴が抜けない ／ 裾が隅にかかる絵を「地が無地でない」と見て抜かない。
  直した: 縁を 1px 削る・縁から 4px の帯のマゼンタかぶりを引く・鮮やかな地なら閉じた穴も抜く・地の色は縁の画素の真ん中の値で決める。
  `rekey --job <名前>` で、もう作った絵を API なしで抜き直せる（聖女 4・伝令 3・ガン 3 を抜き直した）。明るい地と暗い地の両方で縁を拡大して見て、にじみは残っていない。
- 3 枚頼んで 2 枚しか返らないことがある（弓兵）。帳簿は返った枚数で数える。
- 見比べの絵の顔の位置は `--face x,y,幅` で合わせた（聖女 `.42,.29,.30`・伝令 `.47,.32,.30`・ガン `.52,.11,.34`）。採用するなら、会話の画面の顔の位置もこの値が出発点になる。

## 5. 回帰

- 頭なしの門 30 本（第481期の終わりの写し `DemoApp/.tmp/p481/after2` と比べる）: **差は 0 行**。違うのは seed を毎回振る `map11-flow-smoke`／`run-smoke` の 2 本だけ（前の期から同じ）。
  `--chapter1-*-quick` 17 本・`map12`／`map13`／`graph`／`change` の smoke・`phase0`／`171`／`172`／`173`・`tuning-tools-quick` は 1 字も動いていない。`map11-verify=50` の `ok=False` は期の前から同じ出力。
- 会話の場面 `,story-all`: 3 盤 × 2 画面 ○（16／10／16 枚 × 2）。
- `audit`: OK（`CLAUDE.md` 281 行・50,749 バイト）。BattleCore・BattleSim・`docs/` は触っていない。
- `git status`: キー・`.env`・候補の絵（`design/art/_candidates/`）は出ていない。検査が置いた仮の絵も残っていない。

## 6. 追記: ポンの採用と作り直し（2026-11-19）

ポンの答え（`design/PHASE482_WATCH_LOG.md`）: 絵柄は合っている・兵種の見分けはつく。採用は 徴募兵 2・物見 2・傭兵崩れ 2・騎士 1・弓兵 2・守将 3・聖女 2・ガン 2・伝令 2。
作り直したい所は 2 つ: 徴募兵は「今の顔にあるような隠密感」・伝令は「顔が黒すぎる。陰で目元が見えないくらいに」。

- **採用の 9 枚を置いた**（指示書 §6 の受け口・「この期では置かない」はポンが選んだので外した）:
  `DemoApp/assets/portraits/enemy/{levy,recruit,drifter,knight,archer,champion}_face.png`・`design/art/story/聖女.png`・`design/art/story/伝令.png`・`DemoApp/assets/portraits/battle/gan_静_right.png`。
- **作り直し 6 枚**（合計 36／40 枚・約 $1.89）: 依頼の文を直して 徴募兵 3（藁の頭巾で目元が影）・伝令 3（影は目元だけ・鼻と口と顎は見える）。どちらも注文どおりに出た。見比べの絵の 4〜6 番。ポンの答え（2026-11-20）: 徴募兵は 2 番のまま・伝令は 5 番 → 伝令を差し替えた。
- 会話の画面: 人物の絵の顔の位置（`StoryScene.PersonFrame`・聖女と伝令）を足した——腰から上の絵は顔が下にあるので、駒の立ち絵の既定の位置では頭が画面の上に寄りすぎる。
- 台本: 場面 2 のガンの 1 行に `:静`（「か、壁が厚いのはいいな。……奥の部屋は俺のだぞ。」）。ほかのガンの行は叫んでいるので待機の絵のまま。
- 受け口の検査 `,m3d-art-hooks` は、採用の絵と重ならない駒（chaplain・warden）で確かめる形に替えた。
- 検査: `--story-smoke` ○（台本が使う差分の絵 ＝ `gan_静_right.png`）・`,m3d-art-hooks` ○・`,story-all` 3 盤 ○（1920×1080・伝令と聖女の絵・ガンの差分が読まれている）。
- 守将は採用のまま（3 番）。40px で顔が小さい件は、遊んで気になったら寄りで切り直す。

## 7. 追記: 顔も立ち絵も無い 1 章の 5 体の顔（2026-11-20）

ポンの問い「立ち絵が付いていない敵は他にあるか」: 敵の駒 49 体のうち立ち絵のファイルがあるのは 20 体・無いのは 29 体（1 章に出るのは 10 体）。
そのうち顔も無い 1 章の 5 体（`chaplain`・`pyromancer`・`fire_archer`・`shock_mage`・`shove_lancer`）の**顔**を作った（立ち絵はまだ）。

- この期の上限 40 枚には収まらないので、帳簿を別の枠に分けた: `config.json` の `phase` を `482b`・上限 16 枚。作ったのは **15 枚・約 $0.84**（第482期の 36 枚・$1.89 と合わせて 51 枚・約 $2.73）。
- 依頼の文は `design/art/prompts/foe_face2/<id>.md`（先の 6 種と同じ型・絵柄の文は直した後のもの）。断られた依頼 0・外した絵 0。
- 見分けの形: 司祭長 ＝ 白と青の高い司祭帽とメイス ／ 火の魔術師 ＝ 青い頭巾と火の入った籠の杖 ／ 火矢の射手 ＝ 革の帽子に青い鉢巻き・燃える矢 ／ 雷杖 ＝ ゴーグルと二股の杖の火花 ／ 槍兵 ＝ 鼻当ての兜・大きな肩当て・横木つきの槍。
  40px では、火の 2 体は炎の橙・雷杖は二股・司祭長は白い帽子で読める。**槍兵は騎士（閉じた兜と青い房）と近い**——兜と青い房が同じ記号なので、小さいと取り違えるかもしれない。
- 火矢の射手は 3 枚とも右手で矢を握っている（手は崩れていないように見えるが、原寸で指を見てほしい）。
- 採用の番号はポン待ち（`design/PHASE482_WATCH_LOG.md` の追加の表）。置き場は `DemoApp/assets/portraits/enemy/<id>_face.png`。
- **槍兵の作り直し（ポン「槍兵を作り直す」）**: 兜と房をやめて、鎖頭巾（顔を囲む鎖の頭巾）と青い額当てに。3 枚（見比べの絵の 4〜6 番）。騎士（閉じた兜・青い房）とは 40px でも形が分かれた。枠 `482b` の上限を 16 → 19 にした（18／19 枚・約 $1.00・全体で 54 枚・約 $2.89）。
- **追加の 5 体を置いた（ポン 2026-11-20「全て 2 で」）**: `chaplain`・`pyromancer`・`fire_archer`・`shock_mage` は 2 番。槍兵は作り直しの 2 枚目（見比べの絵の 5 番・鎖頭巾）を置いた——作り直しを頼んだ後の答えなのでそう読んだ（兜の版の 2 番がよければ差し替える）。これで 1 章に出る敵の駒に、字で出る顔は無い。受け口の検査は、顔の無い駒を `miasma_priest` に替えた。
- **槍兵は兜の版の 2 番に差し替えた**（ポン「兜の 2 番に」）。「全て 2 で」は見比べの絵の番号そのままだった——鎖頭巾の版は採らない。騎士と記号が近い点は承知の上の採用。
