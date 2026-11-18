# 第480期 —— 自分で遊んだ通し（`--map3d-play=ha|hb1|hb2|hb3|hc|hd|hn`）の出力の抜き書き（手で編集しない）

1-3 の始めの写し（`design/plays/752749` ・ 火の隊は `120065`）から。`ha` ＝ 全員まっすぐ帰る ／ `hb1` ＝ 硬い 1 枚の殿 ／ `hb2` ＝ 弱い 1 枚の殿 ／ `hb3` ＝ 3 枚の殿 ／ `hc` ＝ 隊を順に当てる ／
`hd` ＝ 主力 1 隊だけで 2 つ取る ／ `hn` ＝ 主力は北の砦跡・別働は谷の橋。`MAP3D_CLASH_RESULT` の「倒れた」は（nT）＝ その戦闘の n ターン目。
**`ha` ／ `hb1`〜`hb3` の 4・5 刻目の第2隊の行は、席が入れ替わる駒を倒れた顔で出していた不具合の出力**（この期に直した・駒は倒れていない——`hc` ／ `hd` ／ `hn` は直した後）。
`hb2` の殿は ヒサ の予定が、呼び戻した ゾト と入れ替わっていた（遊ぶ口の不具合・直した）。

## ha（752749）

```
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=—
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 4／5 敵 0／3 賞金 6 2 刻目 倒れた: 爆ぜるゾト（2T）
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 1／5 敵 0／5 賞金 6 4 刻目 倒れた: 突き返しのハネ・呪詛官ネル・渡しのワタ・逆しまのウツ
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 4／4 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 0／5 敵 0／5 賞金 14 5 刻目 倒れた: 渡しのワタ・泥人形ムド・突き返しのハネ・逆しまのウツ・呪詛官ネル
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 4／4 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_CLASH_RESULT 第1隊 対 白雷の勇者　ライゼル 負け 味方 0／4 敵 1／1 賞金 0 10 刻目 倒れた: 胞子体ムグ（6T）・施しのリリ（6T）・継ぎ接ぎのヴェル（4T）・墓守リィカ（5T）
MAP3D_HUNTER_HOME 第2隊 5 枚 10 刻目
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 4枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 討伐隊の砦 5枚 ／ 第4隊 討伐隊の砦 5枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」 ／ 第4隊「生還」
MAP3D_PLAY_HUNTER 9 刻目 第1隊（4 枚: 胞子体ムグ・施しのリリ・継ぎ接ぎのヴェル・墓守リィカ）が追いつかれた ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「……無理だ。引け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 終わり: 生還 ・ 勇者が現れた 7 刻目 ・ 終わった 10 刻目（登場から 3 刻） ・ 生還 15／19 ・ 殿 なし ・ 足を止めた 0 回 ／ 止められなかった 0 回 ／ 追いつかれた 1 回 ・ 勇者 HP 11077／19200 ・ 退かせた False ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=ha ok=True won=True fallen=False captured_turn=-1 gold=32 lost=5 battles=6 spawns=0 shots=42 covers=2（レリックの候補・レリックの候補） sides=6 says=1 declines=0 rest_clicks=0 
```

## hb1

```
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=第4隊（分かちのドハ HP62）
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 4／5 敵 0／3 賞金 6 2 刻目 倒れた: 爆ぜるゾト（2T）
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 6 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 4／4 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 4／4 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_PLAY 殿の間合い 第4隊 中の谷道 3 マス目 勇者まで 4 歩 → 谷の橋
MAP3D_PLAY 殿の間合い 第4隊 谷の橋 勇者まで 1 歩 → 中の谷道 5 マス目
MAP3D_CLASH_RESULT 第4隊 対 白雷の勇者　ライゼル 負け 味方 0／1 敵 1／1 賞金 0 8 刻目 倒れた: 分かちのドハ（2T）
MAP3D_CLASH_RESULT 第1隊 対 白雷の勇者　ライゼル 負け 味方 0／4 敵 1／1 賞金 0 10 刻目 倒れた: 胞子体ムグ（4T）・施しのリリ（6T）・継ぎ接ぎのヴェル（2T）・墓守リィカ（4T）
MAP3D_HUNTER_HOME 第2隊 5 枚 10 刻目
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 4枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 討伐隊の砦 5枚 ／ 第4隊 中の谷道 3 マス目 1枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」 ／ 第4隊「足止めできない」
MAP3D_PLAY_HUNTER 8 刻目 第4隊（1 枚: 分かちのドハ）が自分から入った ・ 台の印「足止めできない」勝ち目「劣勢」セリフ 「ここは任せろ。行け！」 ・ 隊の札の印「足止めできない」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 9 刻目 第1隊（4 枚: 胞子体ムグ・施しのリリ・継ぎ接ぎのヴェル・墓守リィカ）が追いつかれた ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「……無理だ。引け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19198
MAP3D_PLAY_HUNTER 終わり: 生還 ・ 勇者が現れた 7 刻目 ・ 終わった 10 刻目（登場から 3 刻） ・ 生還 10／15 ・ 殿 第4隊 ・ 足を止めた 0 回 ／ 止められなかった 1 回 ／ 追いつかれた 1 回 ・ 勇者 HP 14928／19200 ・ 退かせた False ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=hb1 ok=True won=True fallen=False captured_turn=-1 gold=25 lost=5 battles=7 spawns=0 shots=57 covers=3（確かめの窓・レリックの候補・レリックの候補） sides=7 says=1 declines=0 rest_clicks=0 
```

## hb2

```
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=第4隊（囃し立てのヒサ HP26）
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 4／5 敵 0／3 賞金 6 2 刻目 倒れた: 爆ぜるゾト（2T）
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 6 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 4／4 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 4／4 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_PLAY 殿の間合い 第4隊 中の谷道 3 マス目 勇者まで 4 歩 → 谷の橋
MAP3D_PLAY 殿の間合い 第4隊 谷の橋 勇者まで 1 歩 → 中の谷道 5 マス目
MAP3D_CLASH_RESULT 第4隊 対 白雷の勇者　ライゼル 負け 味方 0／1 敵 1／1 賞金 0 8 刻目 倒れた: 爆ぜるゾト（1T）
MAP3D_CLASH_RESULT 第1隊 対 白雷の勇者　ライゼル 負け 味方 0／4 敵 1／1 賞金 0 10 刻目 倒れた: 胞子体ムグ（4T）・施しのリリ（6T）・継ぎ接ぎのヴェル（2T）・墓守リィカ（4T）
MAP3D_HUNTER_HOME 第2隊 5 枚 10 刻目
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 4枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 討伐隊の砦 5枚 ／ 第4隊 中の谷道 3 マス目 1枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」 ／ 第4隊「足止めできない」
MAP3D_PLAY_HUNTER 8 刻目 第4隊（1 枚: 爆ぜるゾト）が自分から入った ・ 台の印「足止めできない」勝ち目「劣勢」セリフ 「ここは任せろ。行け！」 ・ 隊の札の印「足止めできない」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 9 刻目 第1隊（4 枚: 胞子体ムグ・施しのリリ・継ぎ接ぎのヴェル・墓守リィカ）が追いつかれた ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「……無理だ。引け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19186
MAP3D_PLAY_HUNTER 終わり: 生還 ・ 勇者が現れた 7 刻目 ・ 終わった 10 刻目（登場から 3 刻） ・ 生還 10／15 ・ 殿 第4隊 ・ 足を止めた 0 回 ／ 止められなかった 1 回 ／ 追いつかれた 1 回 ・ 勇者 HP 14916／19200 ・ 退かせた False ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=hb2 ok=True won=True fallen=False captured_turn=-1 gold=25 lost=5 battles=7 spawns=0 shots=57 covers=3（確かめの窓・レリックの候補・レリックの候補） sides=7 says=1 declines=0 rest_clicks=0 
```

## hb3

```
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=第4隊（のろまの巨兵ドルガ HP51・分かちのドハ HP62・見境なしのミサ HP35）
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 4／5 敵 0／3 賞金 6 2 刻目 倒れた: 爆ぜるゾト（2T）
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 6 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 4／4 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 4／4 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_PLAY 殿の間合い 第4隊 中の谷道 3 マス目 勇者まで 4 歩 → 谷の橋
MAP3D_PLAY 殿の間合い 第4隊 谷の橋 勇者まで 1 歩 → 中の谷道 5 マス目
MAP3D_CLASH_RESULT 第4隊 対 白雷の勇者　ライゼル 負け 味方 0／3 敵 1／1 賞金 0 8 刻目 倒れた: のろまの巨兵ドルガ（2T）・分かちのドハ（2T）・見境なしのミサ（2T）
MAP3D_CLASH_RESULT 第1隊 対 白雷の勇者　ライゼル 負け 味方 0／4 敵 1／1 賞金 0 10 刻目 倒れた: 胞子体ムグ（4T）・施しのリリ（6T）・継ぎ接ぎのヴェル（2T）・墓守リィカ（4T）
MAP3D_HUNTER_HOME 第2隊 5 枚 10 刻目
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 4枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 討伐隊の砦 5枚 ／ 第4隊 中の谷道 3 マス目 3枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」 ／ 第4隊「五分」
MAP3D_PLAY_HUNTER 8 刻目 第4隊（3 枚: のろまの巨兵ドルガ・分かちのドハ・見境なしのミサ）が自分から入った ・ 台の印「五分」勝ち目「劣勢」セリフ 「ここは任せろ。行け！」 ・ 隊の札の印「五分」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 9 刻目 第1隊（4 枚: 胞子体ムグ・施しのリリ・継ぎ接ぎのヴェル・墓守リィカ）が追いつかれた ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「……無理だ。引け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19190
MAP3D_PLAY_HUNTER 終わり: 生還 ・ 勇者が現れた 7 刻目 ・ 終わった 10 刻目（登場から 3 刻） ・ 生還 10／17 ・ 殿 第4隊 ・ 足を止めた 0 回 ／ 止められなかった 1 回 ／ 追いつかれた 1 回 ・ 勇者 HP 14920／19200 ・ 退かせた False ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=hb3 ok=True won=True fallen=False captured_turn=-1 gold=25 lost=7 battles=7 spawns=0 shots=57 covers=3（確かめの窓・レリックの候補・レリックの候補） sides=7 says=1 declines=0 rest_clicks=0 
```

## hc

```
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=—
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 4／5 敵 0／3 賞金 6 2 刻目 倒れた: 爆ぜるゾト（2T）
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 6 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 4／4 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 4／4 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_CLASH_RESULT 第1隊 対 白雷の勇者　ライゼル 負け 味方 0／4 敵 1／1 賞金 0 8 刻目 倒れた: 胞子体ムグ（6T）・施しのリリ（6T）・継ぎ接ぎのヴェル（4T）・墓守リィカ（5T）
MAP3D_HUNTER_ROUTED turn=9 hp=9189
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 4枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 討伐隊の砦 5枚 ／ 第4隊 討伐隊の砦 5枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」 ／ 第4隊「生還」
MAP3D_PLAY_HUNTER 8 刻目 第1隊（4 枚: 胞子体ムグ・施しのリリ・継ぎ接ぎのヴェル・墓守リィカ）が自分から入った ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「ここは任せろ。行け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 9 刻目 第2隊（5 枚: 突き返しのハネ・呪詛官ネル・逆しまのウツ・渡しのワタ・泥人形ムド）が追いつかれた ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「……無理だ。引け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 11077
MAP3D_PLAY_HUNTER 終わり: 勇者は退いた ・ 勇者が現れた 7 刻目 ・ 終わった 9 刻目（登場から 3 刻） ・ 生還 10／19 ・ 殿 第1隊 ・ 足を止めた 1 回 ／ 止められなかった 0 回 ／ 追いつかれた 1 回 ・ 勇者 HP 9189／19200 ・ 退かせた True ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=hc ok=True won=True fallen=False captured_turn=-1 gold=47 lost=10 battles=7 spawns=0 shots=52 covers=2（レリックの候補・レリックの候補） sides=7 says=1 declines=0 rest_clicks=0 
```

## hd

```
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=—
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 4／5 敵 0／3 賞金 6 2 刻目 倒れた: 爆ぜるゾト（2T）
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 4／4 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 4／4 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 6 9 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 10 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=11 gate=True hero=19200
MAP3D_HUNTER_HOME 第1隊 5 枚 14 刻目
MAP3D_PLAY_HUNTER 勇者が現れた: 11 刻目 ・ 中間拠点 2 ・ 隊 第1隊 森の集落 5枚 ／ 第2隊 討伐隊の砦 5枚 ／ 第3隊 討伐隊の砦 5枚 ／ 第4隊 討伐隊の砦 5枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「生還」 ／ 第3隊「生還」 ／ 第4隊「生還」
MAP3D_PLAY_HUNTER 終わり: 生還 ・ 勇者が現れた 11 刻目 ・ 終わった 14 刻目（登場から 3 刻） ・ 生還 20／20 ・ 殿 なし ・ 足を止めた 0 回 ／ 止められなかった 0 回 ／ 追いつかれた 0 回 ・ 勇者 HP 19200／19200 ・ 退かせた False ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=hd ok=True won=True fallen=False captured_turn=-1 gold=35 lost=0 battles=5 spawns=0 shots=40 covers=2（レリックの候補・レリックの候補） sides=5 says=0 declines=0 rest_clicks=0 
```

## hn

```
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=—
MAP3D_CLASH_RESULT 第2隊 対 物見の兵 勝ち 味方 5／5 敵 0／3 賞金 6 2 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 6 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 弓隊 勝ち 味方 5／5 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 傭兵崩れ 勝ち 味方 5／5 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_CLASH_RESULT 第2隊 対 白雷の勇者　ライゼル 負け 味方 0／5 敵 1／1 賞金 0 10 刻目 倒れた: 逆しまのウツ（4T）・呪詛官ネル（2T）・突き返しのハネ（4T）・渡しのワタ（1T）・泥人形ムド（6T）
MAP3D_HUNTER_ROUTED turn=10 hp=8811
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 尾根の砦跡 5枚 ／ 第2隊 谷の橋 5枚 ／ 第3隊 討伐隊の砦 5枚 ／ 第4隊 討伐隊の砦 5枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」 ／ 第4隊「生還」
MAP3D_PLAY_HUNTER 9 刻目 第2隊（5 枚: 逆しまのウツ・呪詛官ネル・突き返しのハネ・渡しのワタ・泥人形ムド）が追いつかれた ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「……無理だ。引け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 10 刻目 第1隊（5 枚: 胞子体ムグ・爆ぜるゾト・施しのリリ・継ぎ接ぎのヴェル・墓守リィカ）が追いつかれた ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「分が悪い。引くか」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 18046
MAP3D_PLAY_HUNTER 終わり: 勇者は退いた ・ 勇者が現れた 7 刻目 ・ 終わった 10 刻目（登場から 4 刻） ・ 生還 10／20 ・ 殿 なし ・ 足を止めた 0 回 ／ 止められなかった 0 回 ／ 追いつかれた 2 回 ・ 勇者 HP 8811／19200 ・ 退かせた True ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=hn ok=True won=True fallen=False captured_turn=-1 gold=61 lost=10 battles=8 spawns=0 shots=46 covers=2（レリックの候補・レリックの候補） sides=8 says=0 declines=0 rest_clicks=0 
```

## ha（火の隊 120065）

```
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=—
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 5／5 敵 0／3 賞金 9 2 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 9 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 5／5 敵 0／5 賞金 9 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 21 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 21 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_HUNTER_ROUTED turn=9 hp=0
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 5枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 討伐隊の砦 5枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」
MAP3D_PLAY_HUNTER 9 刻目 第1隊（5 枚: 焼け残りのボルグ・泥人形ムド・火選りのヒヨ・熾のホタ・刻みのノミ）が追いつかれた ・ 台の印「足止めできる」勝ち目「優勢」セリフ 「……無理だ。引け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 終わり: 勇者は退いた ・ 勇者が現れた 7 刻目 ・ 終わった 9 刻目（登場から 3 刻） ・ 生還 15／15 ・ 殿 なし ・ 足を止めた 0 回 ／ 止められなかった 0 回 ／ 追いつかれた 1 回 ・ 勇者 HP 0／19200 ・ 退かせた True ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=ha ok=True won=True fallen=False captured_turn=-1 gold=69 lost=0 battles=6 spawns=0 shots=41 covers=3（確かめの窓・レリックの候補・レリックの候補） sides=6 says=1 declines=0 rest_clicks=0 
```

## hc（火の隊 120065）

```
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=—
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 5／5 敵 0／3 賞金 9 2 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 9 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 5／5 敵 0／5 賞金 9 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 21 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 21 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_HUNTER_ROUTED turn=8 hp=0
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 5枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 討伐隊の砦 5枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」
MAP3D_PLAY_HUNTER 8 刻目 第1隊（5 枚: 焼け残りのボルグ・泥人形ムド・火選りのヒヨ・熾のホタ・刻みのノミ）が自分から入った ・ 台の印「足止めできる」勝ち目「優勢」セリフ 「ここは任せろ。行け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 終わり: 勇者は退いた ・ 勇者が現れた 7 刻目 ・ 終わった 8 刻目（登場から 1 刻） ・ 生還 15／15 ・ 殿 第1隊 ・ 足を止めた 1 回 ／ 止められなかった 0 回 ／ 追いつかれた 0 回 ・ 勇者 HP 0／19200 ・ 退かせた True ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=hc ok=True won=True fallen=False captured_turn=-1 gold=69 lost=0 battles=6 spawns=0 shots=44 covers=3（確かめの窓・レリックの候補・レリックの候補） sides=6 says=1 declines=0 rest_clicks=0 
```

## 追記 4 の後（殿は (C)・勇者の駒を押して「殿に出す」）: ha ／ hr（殿を 1 隊）／ hb1（752749）

### ha

```
MAP3D_HUNTER_ART C:\works\VillainProto\design\art\enemies\hero\lightning-final-v2\idle-left.png 顔 ○ 板 ○
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=—
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 4／5 敵 0／3 賞金 6 2 刻目 倒れた: 爆ぜるゾト（2T）
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 6 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 4／4 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 4／4 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_CLASH_RESULT 第1隊 対 白雷の勇者　ライゼル 負け 味方 0／4 敵 1／1 賞金 0 10 刻目 倒れた: 胞子体ムグ（6T）・施しのリリ（6T）・継ぎ接ぎのヴェル（4T）・墓守リィカ（5T）
MAP3D_HUNTER_HOME 第2隊 5 枚 10 刻目
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 4枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 討伐隊の砦 5枚 ／ 第4隊 討伐隊の砦 5枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」 ／ 第4隊「生還」
MAP3D_PLAY_HUNTER 9 刻目 第1隊（4 枚: 胞子体ムグ・施しのリリ・継ぎ接ぎのヴェル・墓守リィカ）が追いつかれた ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「……無理だ。引け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 終わり: 生還 ・ 勇者が現れた 7 刻目 ・ 終わった 10 刻目（登場から 3 刻） ・ 生還 15／19 ・ 殿 なし ・ 足を止めた 0 回 ／ 止められなかった 0 回 ／ 追いつかれた 1 回 ・ 勇者 HP 11077／19200 ・ 退かせた False ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=ha ok=True won=True fallen=False captured_turn=-1 gold=32 lost=5 battles=6 spawns=0 shots=42 covers=2（レリックの候補・レリックの候補） sides=6 says=1 declines=0 rest_clicks=0
```

### hr

```
MAP3D_HUNTER_ART C:\works\VillainProto\design\art\enemies\hero\idle-left.png 顔 ○ 板 ○
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=第3隊（廃棄聖騎士ガルド HP60・焼け残りのボルグ HP60・火選りのヒヨ HP42・熾のホタ HP47・痺れ粉のトウ HP28）
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 4／5 敵 0／3 賞金 6 2 刻目 倒れた: 爆ぜるゾト（2T）
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 6 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 4／4 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 4／4 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_HUNTER_REAR 第3隊 を殿に出す（行き先 敵の拠点）
MAP3D_CLASH_RESULT 第3隊 対 白雷の勇者　ライゼル 負け 味方 0／5 敵 1／1 賞金 0 8 刻目 倒れた: 廃棄聖騎士ガルド（12T）・焼け残りのボルグ（10T）・火選りのヒヨ（10T）・熾のホタ（2T）・痺れ粉のトウ（1T）
MAP3D_HUNTER_HOME 第2隊 5 枚 11 刻目
MAP3D_HUNTER_HOME 第1隊 4 枚 11 刻目
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 4枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 中の谷道 3 マス目 5枚 ／ 第4隊 討伐隊の砦 5枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「足止めできる」 ／ 第4隊「生還」
MAP3D_PLAY_HUNTER 7 刻目 第3隊 で勇者の駒に触れた: 「殿に出す」の札 出た
MAP3D_PLAY_HUNTER 7 刻目 第3隊 を殿に出した（勇者の駒を押した）
MAP3D_PLAY_HUNTER 8 刻目 第3隊（5 枚: 廃棄聖騎士ガルド・焼け残りのボルグ・火選りのヒヨ・熾のホタ・痺れ粉のトウ）が自分から入った ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「ここは任せろ。行け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 終わり: 生還 ・ 勇者が現れた 7 刻目 ・ 終わった 11 刻目（登場から 4 刻） ・ 生還 14／19 ・ 殿 第3隊 ・ 足を止めた 1 回 ／ 止められなかった 0 回 ／ 追いつかれた 0 回 ・ 勇者 HP 11985／19200 ・ 退かせた False ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=hr ok=True won=True fallen=False captured_turn=-1 gold=31 lost=6 battles=6 spawns=0 shots=53 covers=2（レリックの候補・レリックの候補） sides=6 says=1 declines=0 rest_clicks=0
```

### hb1

```
MAP3D_HUNTER_ART C:\works\VillainProto\design\art\enemies\hero\idle-left.png 顔 ○ 板 ○
MAP3D_PLAY roles main=第1隊 second=第2隊 rear=第4隊（分かちのドハ HP62）
MAP3D_CLASH_RESULT 第1隊 対 物見の兵 勝ち 味方 4／5 敵 0／3 賞金 6 2 刻目 倒れた: 爆ぜるゾト（2T）
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 6 4 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 傭兵崩れ 勝ち 味方 4／4 敵 0／5 賞金 6 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第2隊 対 徴募兵 勝ち 味方 5／5 敵 0／5 賞金 14 5 刻目 倒れた: —
MAP3D_CLASH_RESULT 第1隊 対 徴募兵 勝ち 味方 4／4 敵 0／5 賞金 14 6 刻目 倒れた: —
MAP3D_HUNTER_ENTRANCE turn=7 gate=True hero=19200
MAP3D_HUNTER_REAR 第4隊 を殿に出す（行き先 敵の拠点）
MAP3D_CLASH_RESULT 第4隊 対 白雷の勇者　ライゼル 負け 味方 0／1 敵 1／1 賞金 0 8 刻目 倒れた: 分かちのドハ（2T）
MAP3D_CLASH_RESULT 第1隊 対 白雷の勇者　ライゼル 負け 味方 0／4 敵 1／1 賞金 0 10 刻目 倒れた: 胞子体ムグ（4T）・施しのリリ（6T）・継ぎ接ぎのヴェル（2T）・墓守リィカ（4T）
MAP3D_HUNTER_HOME 第2隊 5 枚 10 刻目
MAP3D_PLAY_HUNTER 勇者が現れた: 7 刻目 ・ 中間拠点 2 ・ 隊 第1隊 谷の橋 4枚 ／ 第2隊 森の集落 5枚 ／ 第3隊 討伐隊の砦 5枚 ／ 第4隊 中の谷道 3 マス目 1枚
MAP3D_PLAY_HUNTER 隊の札の印: 第1隊「足止めできる」 ／ 第2隊「足止めできる」 ／ 第3隊「生還」 ／ 第4隊「足止めできない」
MAP3D_PLAY_HUNTER 7 刻目 第4隊 で勇者の駒に触れた: 「殿に出す」の札 出た
MAP3D_PLAY_HUNTER 7 刻目 第4隊 を殿に出した（勇者の駒を押した）
MAP3D_PLAY_HUNTER 8 刻目 第4隊（1 枚: 分かちのドハ）が自分から入った ・ 台の印「足止めできない」勝ち目「劣勢」セリフ 「長くは持たない。急げ！」 ・ 隊の札の印「足止めできない」 ・ 勇者 HP 19200
MAP3D_PLAY_HUNTER 9 刻目 第1隊（4 枚: 胞子体ムグ・施しのリリ・継ぎ接ぎのヴェル・墓守リィカ）が追いつかれた ・ 台の印「足止めできる」勝ち目「劣勢」セリフ 「……無理だ。引け！」 ・ 隊の札の印「足止めできる」 ・ 勇者 HP 19198
MAP3D_PLAY_HUNTER 終わり: 生還 ・ 勇者が現れた 7 刻目 ・ 終わった 10 刻目（登場から 3 刻） ・ 生還 10／15 ・ 殿 第4隊 ・ 足を止めた 0 回 ／ 止められなかった 1 回 ／ 追いつかれた 1 回 ・ 勇者 HP 14928／19200 ・ 退かせた False ・ 時間切れ False
MAP3D_PLAY_COMPLETE plan=hb1 ok=True won=True fallen=False captured_turn=-1 gold=25 lost=5 battles=7 spawns=0 shots=55 covers=3（確かめの窓・レリックの候補・レリックの候補） sides=7 says=1 declines=0 rest_clicks=0
```
