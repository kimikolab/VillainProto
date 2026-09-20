# 第169期 報告 —— 検証用マップ 1-1 を `DemoApp` に載せた（段C）

指示書: `design/PHASE169_MAP11_DEMO_SPEC.md`。前提: 第168期（`S4/S4` × 回復 50%・R257・R258）／
第167期（R255・R256）／第166期（F2・R252・`EngagementEngine.CrossBoundary`）。

**測る期ではない。** 器具で3つの線を同時に通した1点を、ポンが遊んで確かめられる形に写した。
**数値・敵・隊は第168期から1つも変えていない。**

---

## 0. 結論

| # | 項目 | 結果 |
|---|---|---|
| 本体 | 検証用マップ 1-1 を `DemoApp` に載せた | **○**（新しいシーン `Map11Main.tscn`） |
| (a) | `compare` 305 セルが `docs/balance.md` と 0 件差分 | **○** |
| (a') | `docs/` 14 ファイルのうち動いたもの | **0 ファイル**（`rules.md` / `units.md` / `watch.md` を再生成して 0 行差分・`audit` ずれ 0 件） |
| (b) | `stage scout` の `S4/S4` × 回復 50% が `EnemyCatalog` の定義に差し替えた後も一致 | **○**（踏破 95.6% / 32.0% ほか全量が第168期と一致） |
| (c) | `DemoApp` の進行ロジックを頭なしで 800 seed 回す | **○ —— 5 つの量すべてが差 0.0pt**（±3.0pt の線どころか完全一致） |
| (d) | 単発の戦闘デモが従来どおり動く | **○**（`DEMO_SMOKE_COMPLETE` / `CAMPAIGN_FLOW_SMOKE_COMPLETE` とも通る） |
| (e) | `UnitCatalog.All` / `Presets` / `EnemyCatalog.Stages` / `Columns` に触っていない | **○** |

**盤面は1ビットも動いていない。** `BattleCore` で足したのは
`EnemyCatalog.Vanguards`（`Stages` / `Columns` に載せない先遣隊 2 つ）と、それを引く
`VanguardOf` / `WithoutBack1` の 36 行だけで、**既存の宣言は1つも触っていない。**

---

## 1. Phase 0 の答え

### Q0-1 —— ポンの未コミット差分

**作業ツリーは追跡ファイルの差分ゼロだった**（`git status --porcelain` の非 `??` 行が 0）。
未追跡は音声・立ち絵の `.import` と `design/` のメモ 2 本だけで、
指示書 §2 の停止条件（`CampaignMain.cs` / `CampaignSession.cs` / `Main.cs` の未コミット差分）には当たらない。

### Q0-2 —— 持ち越した `UnitState` を受ける口

**`Main.StartBattle` を2つに割った。**

    StartBattle()          編成画面から。Formation を Materialize して EnterBattle へ
    StartCarriedBattle()   マップから。**持ってきたリストをそのまま** EnterBattle へ
    EnterBattle(players, enemies, seed, stageIndex, title)   共通部分（台本の生成と再生）

窓口は `CampaignSession.CarriedPlayers` / `CarriedEnemies` / `CarriedSeed` /
`CarriedEnemyName` / `CarriedStageIndex` の 5 つ。**リストは写さない**
——`BattleEngine.Run` が書き換えた<u>同じ参照</u>を `Map11State.Resolve` が読むので、
間に写しを1つでも挟むと持ち越しが切れる。

`Main._Ready` の末尾で `HasCarriedBattle` を見て、真なら**編成画面を1フレームも見せずに出撃する。**

### Q0-3 —— 台本は「HP が満タンでない駒」「5体未満の陣営」を描けるか

**描ける。手を入れた箇所は 0。** 根拠は2つ:

1. **`DemoOpening` は最初から開戦時の `Hp` / `MaxHp` を持っている**（`StartBattle` が
   `Run` の<u>前</u>に控えている）。`PartyBar` も `BattlefieldView3D` も
   「体数が 5」「HP が満タン」を前提にした定数を1つも持っていない
   （`PlayableSlotCount` / `Take(5)` / `== 5` の走査が 0 件）。
2. **5体未満は第一波（3体）で 168 期ぶん動いている**（第168期 Q0-2）。

実測でも、(d') の通し確認が **4体の先遣・傷ついた味方・死者を含む編成**で 6 戦を再生し切った。

### Q0-4 —— `CrossBoundary` を `DemoApp` から呼んで `InstanceId` は壊れないか

**壊れない。** `InstanceId` は `BattleContext` の `_nextInstanceId` が
**`Run` のたびに振り直す**（`u.InstanceId = _nextInstanceId++`・engine 側 1 箇所）。
`DemoApp` は `_openingById` を **`Run` の後に**組み直すので、戦闘をまたいで番号を持ち越さない
——`EngagementEngine.Run` が `Opening` を `Run` の後に組んでいるのと同じ理由・同じ形。

### Q0-5 —— 頭なしの通し確認

**既存の `FlowSmoke` には乗せなかった。** あれは `CampaignMain` の自由移動と
`grey-wolves` / `iron-hunt` という固有の部隊 ID に結び付いているので、乗せると両方が壊れる。
代わりに**2本立て**にした:

    --map11-verify[=N]    Map11State だけを N seed 回す（画面を1つも作らない。既定 800）
    --map11-flow-smoke    画面を通して1マップを最後まで（戦闘の再生も本物を通る）

---

## 2. 何を作ったか

### 2-1. `BattleCore`（36 行）

`EnemyCatalog.Vanguards` —— **第二波・先遣** と **第四波・先遣**。
既存の波から **後1 を空けるだけ**（第二波は施しの司祭長、第四波は詠唱兵）で、
**数値も席も1つも変えていない。** `Stages` / `Columns` には載せない
（`Inverter` / `Levy` / `Slanderer` と同じ「定義だけ残す」扱い）。

**「斥候級」という札は付けなかった**——第四波の先遣は斥候級の線（残り枚数 4.0〜4.7 枚）に
届いていない（第168期の実測 3.89 枚）。呼び名は「先遣」で足りる。

### 2-2. `BattleSim`（9 行）

`stage scout` の `S4` が、この定義を引くようになった。
**形としては1ビットも違わない**ことを自己検査 (b'') が**全波・全形・全席（80 席）を参照まで**突き合わせている。

### 2-3. `DemoApp`（新規 4 ファイル）

| ファイル | 中身 |
|---|---|
| `Map11.cs` | マップの定義（道・区画・3 隊・駒の一行・盤面ルールの1行）と **進行の規則 `Map11State`** |
| `Map11Session.cs` | シーンを跨いで残る通し（出した順の記録・精算の1回きり） |
| `Map11Main.cs` | 画面。**判定を1つも持たない** |
| `Map11Main.tscn` | シーン（`Node3D` にスクリプトを1本） |

**`Map11.cs` には Godot の型が1つも入っていない**——同じクラスを頭なしの (c) が回すため。
`Map11State` が呼ぶのは `BattleEngine.Materialize` / `BattleEngine.Run` /
`EngagementEngine.CrossBoundary` の3つだけで、**判定はこのクラスに1つも無い。**

`Map11.DeriveSeed` だけは `EngagementEngine` の private な同名メソッドと**同じ式を写してある**
——第168期の器具がこの式で回しているので、(c) を成立させるには一致していなければならない。

### 2-4. 遊び方

編成画面の「作戦マップへ」が、**第169期から検証用マップ 1-1 に入る**。

1. 左のパネルで隊を選び、「北の道へ」「南の道へ」で送り出す
2. 送り出すと接敵し、敵部隊の**名前／盤面ルール1行／体数／残り HP** が出る
3. 「戦う」で戦闘画面へ。**編成画面は挟まない**（傷も死者もそのまま持ち込む）
4. 戦闘が終わったら「作戦マップへ」でマップに戻る。**勝った隊は次の戦闘の前に HP が 50% 戻る**
5. 4 部隊すべて抜けば踏破。3 隊とも消えれば撤退。結果画面に seed が出る

「この隊の中身を見る」で席・駒名・現在 HP（死者は灰色）と**駒の一行**（4 枚ぶん）が出る。

---

## 3. 自己検査の実測

### (c) 頭なしで 800 seed（`--map11-verify`）

| 量 | 器具（第168期 `S4/S4` × 50%） | `DemoApp` | 差 |
|---|--:|--:|--:|
| 踏破率 正（カド隊→南／ハネ隊→北） | 95.6% | **95.6%** | 0.0 |
| 踏破率 逆 | 32.0% | **32.0%** | 0.0 |
| 2 本抜き カド隊×南（正） | 40.4% | **40.4%** | 0.0 |
| 2 本抜き ハネ隊×北（正） | 38.0% | **38.0%** | 0.0 |
| 挽回率 逆 | 38.5% | **38.5%** | 0.0 |

部分点 正 **3.989** ／ 逆 **3.483**（対比 **+0.506**）、控えが出た試行 正 **627 / 800** ／ 逆 **800 / 800**
——**第168期 部B の表と、丸めの桁まで全部一致する。**

**線は ±3.0pt だったが、実測は 0.0pt だった。** これは偶然ではない
——(c) が回しているのは `Map11State` そのもので、器具と<u>同じ規則・同じ順序・同じ seed の導き方</u>だからである。
**「±3pt 以内」で通っていたら、どこかで規則がずれていたことになる。**

### (d') 画面を通した1マップ（`--map11-flow-smoke`）

    MAP11_FLOW_SMOKE_STEP squad=カド隊 road=南の道 battles=0
    MAP11_FLOW_SMOKE_STEP squad=カド隊 road=南の道 battles=1
    MAP11_FLOW_SMOKE_STEP squad=カド隊 road=北の道 battles=2
    MAP11_FLOW_SMOKE_STEP squad=ハネ隊 road=北の道 battles=3
    MAP11_FLOW_SMOKE_STEP squad=ハネ隊 road=北の道 battles=4
    MAP11_FLOW_SMOKE_STEP squad=控え隊 road=北の道 battles=5
    MAP11_FLOW_SMOKE_COMPLETE won=True cleared=4/4 battles=6

**6 戦とも本物の台本の再生を通っている**（`MAP11_BATTLE_DONE` が 6 回）。
なお**この通し確認の割り当て規則は (c) の器具とは別物**（手番を回さず、動ける隊を先頭から選ぶ）。
(d') は「画面とシーン遷移が最後まで壊れないか」を見るためのもので、数字を作る道具ではない。

### (d) 既存のデモ

    DEMO_SMOKE_COMPLETE events=156 turns=5 won=True
    CAMPAIGN_FLOW_SMOKE_COMPLETE won=True remaining_enemies=1

既存の自由移動の作戦マップ（`CampaignMain.tscn`）は**1行も触っていない**。
`--campaign-*` の各スモークからは今までどおり入れる。

---

## 4. 指示書から外したこと（と、その理由）

| 指示書 | 実装 | 理由 |
|---|---|---|
| 「編成画面は中身を見るだけで開ける」 | **マップ上の「この隊の中身を見る」パネル**にした | `Main.tscn` の編成画面は**ドラッグで組み替える編集器**で、読み取り専用にするには出撃ボタン・プリセット・ロスターを全部殺す必要がある。§7 が「この期では組み直しなし」と決めているので、**出す情報（席・駒名・現在 HP・死者の灰色・駒の一行）を同じにして置き場所だけ変えた** |
| 「マップの既存の4部隊を置き換えてよい」 | **置き換えず、別のシーンを足した** | `CampaignMain` は自由移動・拠点制圧・敵 AI が絡み合っていて、1-1 に作り替えると (d) の `--campaign-flow-smoke` が道連れになる。**新しいシーンなら既存は1行も触らずに済む** |

**`CampaignMain.tscn` は UI からは入れなくなった**（編成画面の「作戦マップへ」は 1-1 を指す）。
消してはいないので、スモークと直接指定からは今までどおり動く。

---

## 5. 触った行

| ファイル | 行 |
|---|--:|
| `BattleCore/UnitCatalog.cs` | +36 |
| `BattleSim/Modes/StageScout.cs` | +21 / −1 |
| `DemoApp/CampaignSession.cs` | +40 |
| `DemoApp/Main.cs` | +80 / −5 |
| `DemoApp/Map11.cs` | 新規 |
| `DemoApp/Map11Session.cs` | 新規 |
| `DemoApp/Map11Verify.cs` | 新規 |
| `DemoApp/Map11Main.cs` | 新規 |
| `DemoApp/Map11Main.tscn` | 新規 |

`docs/` は**1ファイルも動いていない**。`DemoApp/assets/` は1つも触っていない。

---

## 6. 次にやること

**ポンが遊んで、`design/PHASE169_WATCH_LOG.md` の 7 つの問いに答える。**
その答えを見てから、§7 の「この期で決めないこと」（編成の組み直し・手札・回復を時間で買う形・
3戦の道・難度のつまみ）に手を付ける。

**いまわかっている歪みを3つだけ書き残しておく**（遊ぶ前の予告であって、直す約束ではない）。

1. **失敗の 90〜91% は北の 2 戦目（第三波）に集まる**（第167期から動いていない）。
   1-1 で負けたなら、ほぼそこである。
2. **カド隊は北の道の1戦目（粛の伝令が中央に残る先遣）に 0.0% で負ける**（R251・R258）。
   **これは設計であって不具合ではない**——第168期の実測で、敵が 3 体まで減れば
   カド以外の 4 枚が伝令を割れるようになって 38〜100% まで戻る。
   「粛のいない道へ」という駒の一行は、この1点を指している。
3. **傷ついた隊が「勝てないが死にもしない」相手に当たると、30 ターンの膠着が繰り返せてしまう。**
   (d') の最後の走行（seed 960536）がこれを踏んだ——カド隊が南の 1 戦目を抜いた後、
   第五波に当たって **23 連続で `turns=30` の引き分け**になり、`BattleCap` (24) まで回った。
   **これは移植で入った不具合ではない**——第168期の器具（`BandOnce`）も同じ形で、
   だからこそ (c) が 0.0pt で一致している。engine の上限ターンがそのまま出ているだけである
   （第152期がガルドの膠着を1つ塞いだが、**持ち越しで削れた隊が相手に届かなくなる形は別口**）。
   **この期では規則を1つも足さず、結果の文面だけ直した**
   ——「30 ターン決着せず（両軍とも残っている）。もう一度当てても同じになる」と出して、
   引き返す判断をプレイヤーに返す。自動で回す (d') だけが素直に嵌まる。
