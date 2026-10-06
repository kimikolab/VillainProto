# コマンド一覧（`CLAUDE.md` の「コマンド」から移した本文・第259期）

description: 第259期に `CLAUDE.md` の「コマンド」節を**逐語で**移したもの。`BattleSim` の全診断モードと `DemoApp` の頭なしの門の一覧。**`sweep` はこの表を読んで走らせる本を組む**（`BattleSim/Sweep.cs` の `Extract`・行頭 `    dotnet run --project BattleSim -c Release ` の行だけを見る）ので、**行の書式を変えない**。**本文は1文字も書き換えていない。**

`CLAUDE.md` には毎回使う十数本だけが残っている。**新しいモードはこのファイルの該当する位置に足す**（`sweep list` に載る）。

---

## コマンド

テストプロジェクトは無い。検証はすべて BattleSim の実行結果で行う。

    dotnet build                                            # 全体ビルド（WPF を含むので Windows のみ）
    dotnet run --project PrototypeApp                       # 編成UI（Windows / WPF）
    dotnet run --project BattleSim -c Release <n>           # ステージ n (0-4) を総当たり
    dotnet run --project BattleSim -c Release <n> <unitId>  # 指定ユニットを含む編成に絞る（例: rica）
    dotnet run --project BattleSim -c Release 0 compare > docs/balance.md  # 代表編成 × 全ステージの勝率比較
    dotnet run --project BattleSim -c Release 0 compare quality > docs/quality.md  # 勝ち方の質（残存/圧勝率/**完全勝利**/全滅勝ち・第2〜5波）
    dotnet run --project BattleSim -c Release 0 dump > docs/units.md      # ユニット・特性・ステージ一覧
    dotnet run --project BattleSim -c Release 0 layout      # 代表編成の全配置総当たり（並列・決定的。**第140期に波別最良の測り直しも並列にした**）
    dotnet run --project BattleSim -c Release 0 reseat [絞り込み] [skip] [take]  # 配置候補を seed 200 で測り直す
    dotnet run --project BattleSim -c Release 0 confirm     # 差し替え候補を別 seed で追試する
    dotnet run --project BattleSim -c Release 0 chain > docs/chain.md    # 勝率だけでは見えない「連鎖の深さ」（最大同時撃破数・決着ターン数）
    dotnet run --project BattleSim -c Release 0 ablate [絞り込み] > docs/ablation.md  # 編成から1体ずつ抜いた勝率変化（入れ得の検出）
    dotnet run --project BattleSim -c Release 0 pulse [絞り込み] > docs/pulse.md      # 駒ごとの活動量（振/干渉）と与被ダメージの内訳
    dotnet run --project BattleSim -c Release 0 engage [絞り込み] > docs/engage.md    # 会戦（地点主表: 突破率・期待突破数×投入部隊数1-3・非線形・入場戦力・第1削り）
    dotnet run --project BattleSim -c Release 0 seats [絞り込み]    # 会戦の隊列持ち越し診断（診断用。docs/ に置かない）
    dotnet run --project BattleSim -c Release 0 seats2 list         # 隣接／列を読む駒の一覧と行数（戦闘0回。第45期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 seats2 degree       # 次数分布と、現行48行 × その鏡像の差（角の対称性）
    dotnet run --project BattleSim -c Release 0 seats2 [skip] [take]  # 駒ごとに「編成が変わると席が変わるか」を測る（全48行で約7分）
    dotnet run --project BattleSim -c Release <n> demo      # 固定編成1戦の詳細ログを表示
    dotnet run --project BattleSim -c Release <n> demo "編成名" [seed]  # compare の編成で1戦の詳細ログ
    dotnet run --project BattleSim -c Release <n> replay "編成名" <seed>  # 1戦を再生用JSON（台本）で吐く

第4〜18期に足した診断モード。**どれも docs/ には置かない**（標準出力で読むだけ）。
所要は全編成でおおむね 10〜30秒、`bridge` だけ 30秒前後。

    dotnet run --project BattleSim -c Release 0 handoff [絞り込み]  # 会戦の部隊引き継ぎ（第4期 Phase K）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 cost [絞り込み]     # 波の「代金」= 100% − 勝った試行の残HP%（第5期 Phase M）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 gradient [絞り込み] # 勾配のある部隊列の候補を測る（第5期 Phase N）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 aim [絞り込み]      # 安い波の再設計・素体候補（第6期 Phase P）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 flip [絞り込み]     # 代金の「向き」を作れるか（第7期 Phase R）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 bridge [絞り込み]   # 向きは序列を動かすか（第7期 Phase S〜第10期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 bill [絞り込み]     # 代金を 敵由来/自傷/回復/残差 に割る（第9期 Phase X）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 charge [絞り込み]   # 大技の発火率とチャージ化の前後（第10期 Phase AC）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 timing [絞り込み]   # 味方の行動パターンの変種（第11期 Phase BC）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 power [絞り込み]    # 「地力」の分解（第12期 CA/CB・第13期 DA・第14期 EA/EB）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 bench [絞り込み]    # 台をまたぐ入れ替わりは構造的か（第13期 Phase DB）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 wave [絞り込み]     # 編成 × 波の交互作用（単発戦。第15期 FA/FB）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 dissect [絞り込み]  # 交互作用の個別事例の解剖 + 敵側の特徴量（第16期 GA/GB）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 output [絞り込み]   # 参照台と出力特徴量 (A)(B)(C)（第17期 HA/HB）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 convert [絞り込み]  # 個体HP だけを振った台の系列と変換率（第18期 IA）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 ptrace [絞り込み]   # 毒の立ち上がり診断
    dotnet run --project BattleSim -c Release 0 life [絞り込み] [駒Id]  # 駒の寿命と稼働率（第19期。既定は kado）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 route             # 自傷の燃料は変換器まで届くか（第19期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 swap              # 同じ席で駒を入れ替えて比べる（第21期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 spread [除外語]   # 波の分離度（飽和・波間相関・固有の勝者敗者）（第22期。除外語で行を外して同じ行数で前後比較・第49期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 gullet [gain|log] # 巨躯の吐き戻し（4版の対照 / 返す効率の振り / 1戦の監査）（第23期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 gullet belly      # 腹の規模の実測（閾値と還し率の導出。盤面は動かさない）（第36期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 gullet belly4     # まどろみ／還しの4版対照（V0/V2/V3/V4）（第36期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 yield [絞り込み]  # 攻撃力1点は誰の手なら出力になるか（注入テスト）（第24期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 yoke [sweep|log]  # 第四波の軛（5版の対照 / 上限の振り / 1戦の監査）（第25期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 yoke map          # **上限の地図**（第132期 段1・測定だけ）。型 × 陣営 × 出どころの帳簿・上限 on/off・回避経路・段との 2×2  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 hush [log]        # 第二波の粛（3版の対照 / 1戦の監査）（第27期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 guard [percent]   # 殉教者の体（HP 4点）／介入の密度（p 3点）の掃引（第34・35期）
    dotnet run --project BattleSim -c Release 0 sever [絞り込み]  # 断ちの発火・手番の放棄・傷の取り合い（第37期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 sever sale        # 捨てた手番は号令・据えに売れないか（陽性対照つき）（第37期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 sever reach       # 傷はどこまで積み得るか（閾値を決める前のゲート）（第38期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 suture [絞り込み] # 縫いの繕い・塞ぎ・渇きの封じ／渇きの帰属（同数値対照）（第39期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 expose [絞り込み] # 曝きの発火・読み手の起動・上限の掃引（同数値対照つき）（第40期）  → LESSONS_001_040.md
    dotnet run --project BattleSim -c Release 0 shove [絞り込み]  # 突き返しの発火・供給元・ウツの攻撃力の成長・Penalty 掃引（陽性対照つき）（第41期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 dull [絞り込み]   # 弱体の経路別・横取り・鎧と死蔵・排他・ArmorPerDull 掃引（陽性対照つき）（第42期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 relay [絞り込み]  # 転嫁の横取り・流し先・崖の検算・自弁率・効き・TransferPercent 掃引（陽性対照つき）（第43期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 relay kubi        # 変種Cだけを回す（萎縮との同居。主表と検算は重いので分けてある）
    dotnet run --project BattleSim -c Release 0 slander [絞り込み] # 誹りの発火・対象・横取り／素通り・攻ゼロ・早逝・Penalty 掃引・別 seed の追試（第44期・**採用しなかった**）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 overbear [絞り込み] # 驕りの削り・成立率／成立時刻・2倍・横取り／素通り・逆行・攻ゼロ・Drain 掃引・席の分散（第46期・**採用しなかった**）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 scale [絞り込み]  # 鱗の獲得（死/破片）・纏い率・初纏い・貫き／後列到達・二重支出・枯渇／死蔵・素体対照（第47期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 scale phase0     # 実装前の地図（キーの読み手 / 敵の範囲攻撃 / 味方の死亡数。盤面は動かさない）
    dotnet run --project BattleSim -c Release 0 scale sweep      # CostPerAttack 0/1/2 の掃引だけを回す
    dotnet run --project BattleSim -c Release 0 scale seats      # 席の分散（seats2 の写し）だけを回す
    dotnet run --project BattleSim -c Release 0 census           # 棚卸し: 員数・compare の出現数・特性の保持者一覧（戦闘0回。第48期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 scapegoat [絞り込み] # 業の引き取り・種類数・到達／成立率・転写と転写の効き・自傷／味方（第49期・**採用しなかった**）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 scapegoat phase0  # 実装前の地図（味方に載る種類の分母 / 寿命 / 候補台）。戦闘は回すが盤面は動かさない
    dotnet run --project BattleSim -c Release 0 scapegoat sweep   # Threshold 2/3/4 の掃引だけ
    dotnet run --project BattleSim -c Release 0 scapegoat stun    # 痺のある台（引き取りと発揮が資源を奪い合う）だけ
    dotnet run --project BattleSim -c Release 0 scapegoat confirm # 配置の追試（seed 200..599）だけ
    dotnet run --project BattleSim -c Release 0 scapegoat alt     # 機構の帰属を別 seed 帯（200..599）で追試
    dotnet run --project BattleSim -c Release 0 divert [絞り込み] # 逸らしの発火・外し・焦点と焦点の効き・撃破順・代金の分離（第50期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 divert phase0     # 実装前の地図（**engine の窓口一覧** / MarkPullPercent の実装 / 標の現在値）
    dotnet run --project BattleSim -c Release 0 divert probe      # 採用2行を選ぶための候補探索（ソラ版 − 素体版の波ごとの帰属）
    dotnet run --project BattleSim -c Release 0 divert sweep      # TargetCount 1/2/3 の掃引だけ
    dotnet run --project BattleSim -c Release 0 divert seats      # 席の分散だけ
    dotnet run --project BattleSim -c Release 0 divert confirm    # 配置の追試（seed 200..599）。**発火が 0 の席を採らないための列つき**
    dotnet run --project BattleSim -c Release 0 divert alt        # 機構の帰属を別 seed 帯で追試
    dotnet run --project BattleSim -c Release 0 goad [絞り込み]   # 駆り立ての発火・空振り・渡した量・効き・被弾増・早逝・ソラ／ウツ干渉（第52期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 goad sweep       # Boost 0/2/4/6 の掃引だけ
    dotnet run --project BattleSim -c Release 0 goad cross       # ソラ・ウツとの同居版だけ
    dotnet run --project BattleSim -c Release 0 goad seats       # 席の分散だけ（seats2 の写し）
    dotnet run --project BattleSim -c Release 0 goad confirm     # 配置の追試（seed 200..599）。**発火／空振りの列つき**
    dotnet run --project BattleSim -c Release 0 goad alt         # 機構の帰属を別 seed 帯で追試
    dotnet run --project BattleSim -c Release 0 finisher [絞り込み] # 止めの発火・空振り・**列越え**・消費・**止めた砲火**・遊休・撃破（第53期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 finisher sweep   # Multiplier 1/2/3/4 の掃引だけ（第四波は軛の Cap で頭打ち）
    dotnet run --project BattleSim -c Release 0 finisher cross   # ザン・カリとの同居だけ（発火が動かないことの確認）
    dotnet run --project BattleSim -c Release 0 finisher seats   # 席の分散だけ（seats2 の写し）
    dotnet run --project BattleSim -c Release 0 finisher confirm # 配置の追試（seed 200..599）。**発火／列越え／止めた砲火の列つき**
    dotnet run --project BattleSim -c Release 0 finisher alt     # 機構の帰属を別 seed 帯で追試
    dotnet run --project BattleSim -c Release 0 wave2 [波番号 1-5]  # 波の解剖: 敵を1体ずつ空席にし、盤面ルールを切って関門を数える（第51期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 pace              # 勝率以外の物差し（決着T/残存数/被ダメ/与ダメ）の分離度を波ごとに比べる（第54期・調査）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 audit             # docs/ の生成物が現行の編成数と整合しているか（戦闘0回・1秒。第55期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 roster audit > docs/roster_audit.md  # ロスターの棚卸し（第177期）。**新しい測定は0件**——`checkup ideal` と `stage catalog` を**そのまま呼んで**1枚の表に集める（130 秒）
    dotnet run --project BattleSim -c Release 0 whet [絞り込み]   # 強化の分布: 経路別・受け手・収支（Whet-Dull）・死蔵・逆しま（第56期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 burn [絞り込み]   # 燃焼の解剖: 供給・捨て率・稼働率・帰属・通貨どうしの接続（第57期・調査）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 burn phase0      # 窓口の一覧と接続の地図（戦闘0回）
    dotnet run --project BattleSim -c Release 0 burn alt         # 帰属の符号を別 seed 帯（200..599）で追試
    dotnet run --project BattleSim -c Release 0 favor [絞り込み]  # 火選り: 発火・空振り・燃体/非燃体・熾火への配分・死蔵・火の粉の符号の反転（第58期。**旧 kindle**）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 favor phase0     # 実装前の地図（**熾火の乗算監査**・在庫・試験行の選定）
    dotnet run --project BattleSim -c Release 0 favor sweep      # Gain / Loss の掃引（V0〜V4・素体の対照つき）
    dotnet run --project BattleSim -c Release 0 favor seats      # 席の分散（seats2 の写し）＋ **reseat 120通りの帯の形**
    dotnet run --project BattleSim -c Release 0 favor confirm    # 配置の追試（seed 200..599）。**発火／空振り／燃体／非燃体の列つき**
    dotnet run --project BattleSim -c Release 0 favor alt        # 機構の帰属と火の粉の符号を別 seed 帯で追試
    dotnet run --project BattleSim -c Release 0 turn [phase0|sweep|alt]  # 火選りを手番へ降ろす（第60期）。V0 移設前 / V1 移設（＝現行）/ V2・V3 素体  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 turn ratio             # 火選りの係数: 乗算の比を両 seed 帯で取る（第61期。**(6,2) は採らなかった**）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 miasma [モード]  # 瘴気を手番へ降ろす（第61期・**採用しなかった**）。V0 現行 / V1 移設 / V2 周期 / V3 素体  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 miasma phase0    # 実装前の地図（**`OnTurnStart` の発火順**・手番を止めうる経路・出力・分母）
    dotnet run --project BattleSim -c Release 0 miasma seat      # 席の入れ替えの追試（順序の判断が消えたかの直接の証拠）
    dotnet run --project BattleSim -c Release 0 miasma mio       # 損の出どころ（ミオを素体に落とす対照。**4行中3行が床に落ちて無効**）
    dotnet run --project BattleSim -c Release 0 miasma alt       # 帰属の符号を別 seed 帯（200..599）で追試
    dotnet run --project BattleSim -c Release 0 blaze [モード] [a|b] # 火種: 破裂の着火（V0/A/B/C）・計数・層・試験行（第59期）  → LESSONS_041_060.md
    dotnet run --project BattleSim -c Release 0 blaze phase0     # 実装前の地図（破裂時点の盤面・墓守の層・情報セル）
    dotnet run --project BattleSim -c Release 0 blaze main       # 既存7行の主表・計数・Q2 だけ
    dotnet run --project BattleSim -c Release 0 blaze rows       # 試験行（死軸×ホタ / 死軸×ヒヨ）だけ
    dotnet run --project BattleSim -c Release 0 blaze scan       # **120通り × 情報セル**（席は「勝つ席」ではなく「測れる席」で選ぶ）
    dotnet run --project BattleSim -c Release 0 blaze seats / confirm / alt / check
    dotnet run --project BattleSim -c Release 0 funnel [モード] # 横流し（第62〜64期・**3回測って採用しなかった。残置で確定**）。席ごとの帰属と幅  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 funnel phase0    # 地図（試験行 / **器具の両側 40〜95%** / **真の捨て場** / 強化の在庫 / ウツ同席 / 主判定）
    dotnet run --project BattleSim -c Release 0 funnel pick      # **試験行の選定規則のスキャン**（61行 × ablate + 5席の素体。結果を見る前に規則を固定するための道具）
    dotnet run --project BattleSim -c Release 0 funnel reseat    # 配置の粗探索＋追試（**第64期はこれを使わないと決めた**。理由は design/PHASE64_FUNNEL3.md §4-1）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 funnel alt       # 同じ表を再現帯（0..199）で（第64期の主帯は 1000..1399）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 spend           # 強化の使い道: 7経路を1本ずつ落として帰属・到着・使用率・受け手（第65期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 spend map       # 判断の地図（**戦闘0回**。出力経路4本 / 行き先の決まり方 / 陰性対照の分母）
    dotnet run --project BattleSim -c Release 0 spend alt       # 同じ表を再現帯（seed 200..599）で
    dotnet run --project BattleSim -c Release 0 creak          # 軋みが響く: WhetReceived >= 閾値 で単体→薙ぎ（第66・67期・**2回とも採用しなかった**）
    dotnet run --project BattleSim -c Release 0 creak phase0   # 実装前の地図（ヨミの WhetReceived の分布＝格子9点の到達%と到達T / Q6 の実効レンジ / Q3' の素体対照）
    dotnet run --project BattleSim -c Release 0 creak alt      # 同じ表を再現帯（seed 200..599）で
    dotnet run --project BattleSim -c Release 0 creak3         # 自己供給 対 外部供給: `CreakSource` を Whet/Bonus/Both で振る（第77期・**3回目も採用しなかった。この駒は閉じた**）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 creak3 phase0  # 紙の計算と分布（在席率・相方の在席・格子12点の到達%。**盤面は動かない**）
    dotnet run --project BattleSim -c Release 0 creak3 alt     # 同じ表を B 帯（ドラフト seed 200..207 / 理想 200..399）で
    dotnet run --project BattleSim -c Release 0 creak3 check   # 陰性対照（305 セル 0 件・ヨミを含まない 290 セル × 9 版 0 件）
    dotnet run --project BattleSim -c Release 0 creak3 ideal66 # 第66期の帯（seed 200..599）での再現確認だけ（判定基準は変えない）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 carry          # 棚卸し: 駒 × キー の「外から届いた量」の在庫と格子の形（第68期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 carry keys     # 表C（供給経路 × 開戦時一括／毎ターン／事象ごと の分類と、受け手の形）
    dotnet run --project BattleSim -c Release 0 carry solo     # 表D（単独成立度＝相方あり行／なし行の寄与）
    dotnet run --project BattleSim -c Release 0 carry leak     # 表E（`AcceptsSupport` の漏れ。**ガルドから Stoic だけを外した対照**）
    dotnet run --project BattleSim -c Release 0 carry check    # 陰性対照（`verbose` の有無で 305 セル 0 件）
    dotnet run --project BattleSim -c Release 0 draft          # ドラフト台: 無作為5体 × 配置2版（表A〜F）（第69期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 draft phase0   # N と M を決めるパイロット（式は測る前に固定）
    dotnet run --project BattleSim -c Release 0 draft alt      # 同じ標本を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 draft check    # 陰性対照（抽選の再現性・含まれない駒の素体化で 50,000 セル 0 件）
    dotnet run --project BattleSim -c Release 0 draft2         # ドラフト台の 2×2: 編成の作り方 × 敵の強さ（第70期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 draft2 phase0  # 紙の計算（**戦闘0回**。超幾何 / 抽選10万回 / 倍率の導出 / N の決め方）
    dotnet run --project BattleSim -c Release 0 draft2 alt     # 同じ標本を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 draft2 check   # 陰性対照（弱い波が HP 以外を1つも動かしていないこと）
    dotnet run --project BattleSim -c Release 0 draft3         # ドラフトの選択規則3つ（無作為/素朴/シナジー志向）× 2波（第71期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 draft3 phase0  # 紙の計算（**戦闘0回**。3規則の編成の中身 / 超幾何との照合 / 理想61行の分母）
    dotnet run --project BattleSim -c Release 0 draft3 alt     # 同じ標本を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 draft3 check   # 陰性対照（規則 P が第70期の C/D と一致・S が規則どおり）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 slope          # 傾きで選ぶ規則 S' ＋ キーの傾きの地図（第72期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 slope phase0   # 紙の計算（**戦闘0回**。傾きの出どころ / S' の選好 / 超幾何との照合）
    dotnet run --project BattleSim -c Release 0 slope alt      # 同じ標本を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 slope check    # 陰性対照（P が第70期と一致・S' が規則どおり・傾きの定数が第71期の写し）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 wound          # 傷の解剖: 在庫の収支・対照3種・代金・犯人（理想台。第73期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 wound phase0   # 窓口一覧と紙の計算（**戦闘0回**。5枚のマイナスの分離可能性 / 超幾何）
    dotnet run --project BattleSim -c Release 0 wound alt      # 同じ表を B 帯（seed 200..399）で
    dotnet run --project BattleSim -c Release 0 wound draft [alt]  # ドラフト台（Pw と S'w・**弱い波**）の傾きの分解
    dotnet run --project BattleSim -c Release 0 wound check    # 陰性対照（素体・裂き二重が供給だけを動かすことの1戦監査）
    dotnet run --project BattleSim -c Release 0 wcost          # 傷の代金の分離: 5つのマイナスを1つずつ外す ＋ 断ちの待ち方（理想台。第74期）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 wcost phase0   # 分割の一覧と紙の計算（**戦闘0回**。V1 が動かす分母を先に数える）
    dotnet run --project BattleSim -c Release 0 wcost check    # 受け入れ基準（`compare` 305 セルの突き合わせ・代金なし版の1戦監査）
    dotnet run --project BattleSim -c Release 0 wcost alt      # 同じ表を B 帯（seed 200..399）で
    dotnet run --project BattleSim -c Release 0 wcost draft [alt]  # ドラフト台 Pw（**主判定はこちら**。理想台はナタの行が1つしか無い）
    dotnet run --project BattleSim -c Release 0 blade          # 薄刃の払い方: 代金を条件付きにする4版（理想台。第75期・**採用しなかった**）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 blade phase0   # 窓口の一覧と紙の計算（**戦闘0回**。call-site 全数 / 敵の速さ / 解除率の見積り）
    dotnet run --project BattleSim -c Release 0 blade check    # 陰性対照（305 セル 0 件・キリを含まない行 0 件・1戦の監査）
    dotnet run --project BattleSim -c Release 0 blade alt      # 同じ表を B 帯（seed 200..399）で
    dotnet run --project BattleSim -c Release 0 blade draft [alt]  # ドラフト台 Pw（**主判定はこちら**）
    dotnet run --project BattleSim -c Release 0 body           # 体の解剖: 回帰3段・群の説明力・数値を揃えた組・残差（第76期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 body phase0    # ロスターの分布と紙の計算（**戦闘0回**。回帰の設計を測る前に固定する）
    dotnet run --project BattleSim -c Release 0 body alt       # 同じ表を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 body wound [alt]  # 表E（−7.2 の分解。傷の5枚を1枚ずつ素体に）
    dotnet run --project BattleSim -c Release 0 traits         # 特性の数の分解: 特性数/発火口/入口/キー の識別力（第78期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 traits phase0   # 表A と相関行列（**戦闘0回**。器具の定義を測る前に固定する）
    dotnet run --project BattleSim -c Release 0 traits alt      # 同じ表を B 帯（ドラフト seed 200..207 / 理想 200..399）で
    dotnet run --project BattleSim -c Release 0 lastslot        # 最後の1枠: 首刈りのオノを両方の台で測る（第79期・**採用しなかった**）。A 帯  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 lastslot phase0 # 空白の地図・候補の選定規則・数値・試験行の選定（在席時勝率と理想台の帰属のぶんだけ戦闘を回す）
    dotnet run --project BattleSim -c Release 0 lastslot seats  # 単騎・軸あり の配置（reseat 120通り → confirm seed 200..599・撃破/戦の列つき）
    dotnet run --project BattleSim -c Release 0 lastslot check  # 受け入れ基準: `compare` 305 セルの突き合わせ（`All` は 51 のまま）
    dotnet run --project BattleSim -c Release 0 lastslot alt    # 同じ表を B 帯（ドラフト seed 200..207 / 理想 200..399）で
    dotnet run --project BattleSim -c Release 0 pairs           # 組み合わせの表: 51 体・1,275 組の 単独/組/相乗（在席差・A/B 両帯・約 4 分）（第80期・調査）  → LESSONS_061_080.md
    dotnet run --project BattleSim -c Release 0 pairs phase0    # 紙の計算（**戦闘0回**。抽選だけ回して組ごとの在席標本数・SE の目安・測れる組の割合）
    dotnet run --project BattleSim -c Release 0 pairs check     # 受け入れ基準: `compare` 305 セルの突き合わせ
    dotnet run --project BattleSim -c Release 0 pairs2          # 組の 2×2: 同じ台で A/B の中身だけを4通りに振る（全 1,275 組 × 128 台）（第81期・調査）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 pairs2 phase0   # 紙の計算（**戦闘0回**。予算・群の大きさ・台の抽選・**2系列が編成を共有しないこと**）
    dotnet run --project BattleSim -c Release 0 pairs2 run <skip> <take>  # 組の区間だけを測って TSV で吐く（**分割実行。1回で全部回すとメモリ不足で落ちる**）
    dotnet run --project BattleSim -c Release 0 pairs2 tables <path>      # `run` の TSV を連結して渡す（表A〜F・Q1〜Q5・20 秒）
    dotnet run --project BattleSim -c Release 0 pairs2 check    # 受け入れ基準: `compare` 305 セルの突き合わせ
    dotnet run --project BattleSim -c Release 0 checkup phase0   # ロスター健康診断（第82期・調査）。紙の計算（**戦闘0回**。線・拒否権の紙で出る2本）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 checkup run <skip> <take>  # 2×2 を測って TSV（**分割実行**。先頭 9+NT 列は `pairs2 run` と同一）
    dotnet run --project BattleSim -c Release 0 checkup tables <a> [<b>] [<c>]  # 残す/転生/差し替え に3分（表A〜F・Q1〜Q6。<b> は `pairs2 run` / <c> は `run51` の TSV）
    dotnet run --project BattleSim -c Release 0 checkup ideal    # 理想台の帰属だけ（`CompareBuilds()` 61 行 × 在席枠 305 か所・素体差し替え）
    dotnet run --project BattleSim -c Release 0 checkup check    # 受け入れ基準: `compare` 305 セル ＋ 第119期の自己検査 (a)〜(d)
                                                                 # **第132期 段0-a に直した**（`GradeStep` の分類を1行足した。第128期から3期ぶん止まっていた）
    dotnet run --project BattleSim -c Release 0 checkup runp <skip> <take>   # **第119期**: 8 版（2×2 ＋ yP/yM）。**本物と同じ版は測らない**（分割実行・69 分）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 checkup run51 <skip> <take>  # **第119期**: 第82期のロスター（51 枚＝ソムを抜きトモの席にハリ）で 4 版。Q1 の再現用
    dotnet run --project BattleSim -c Release 0 breadth phase0 [<TSV>]  # 物差しの引き直し（第83期・調査）。紙の計算（**戦闘0回**。共有しない組の数・床の分布・埋め草の予算）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 breadth run <skip> <take>  # **埋め草を無作為3枚にした版**の 2×2（§2-5・分割実行。列は `checkup run` と同形式）
    dotnet run --project BattleSim -c Release 0 breadth tables <a> [<b>] [<c>]  # 表A〜G・Q1〜Q7（<a> は `checkup run` / <b> は `pairs2 run` / <c> は `breadth run` の TSV）
    dotnet run --project BattleSim -c Release 0 breadth check    # 受け入れ基準: `compare` 305 セルの突き合わせ
    dotnet run --project BattleSim -c Release 0 thorn phase0     # 棘に傷を載せる（第84期・**測って採用しなかった**）。窓口の全数・反撃の発火・粛・交わり  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 thorn run <v> [skip] [take]  # 2×2（v = 0 現行 / 1 敵に傷 / 2 味方にも）。50 組 × 128 台・約 40 秒/版。TSV を標準出力へ
    dotnet run --project BattleSim -c Release 0 thorn tables <v0> <v1> [<v2>]  # 表A〜E・Q1〜Q4（`run` の TSV を渡す）
    dotnet run --project BattleSim -c Release 0 thorn check      # `compare` 61 行の V0/V1/V2 突き合わせ・拒否権1・2・`docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 suture2 phase0   # 糸を味方にも通す（第85期・**測って採用しなかった**）。**紙のスループット**（停止条件）・窓口の全数・交わり  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 suture2 run <w> [skip] [take]  # 2×2（w = 0 現行 / 1 両側・カドだけ / 2 両側・巻き込み則）。50 組 × 128 台・約 50 秒/版
    dotnet run --project BattleSim -c Release 0 suture2 tables <w0> <w1> <w2>  # 表A〜F・Q1a/Q1b/Q2/Q3/Q5・自己検査（`run` の TSV を渡す）
    dotnet run --project BattleSim -c Release 0 suture2 nata     # Q4 の専用台（カド × ハリ × ナタ ＋ 埋め草2枚・16 台。`CompareBuilds()` は触らない）
    dotnet run --project BattleSim -c Release 0 suture2 check    # `compare` 61 行の W0/W1/W2・拒否権1・2・(e)・`docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 mender phase0    # 繕いに傷を読ませる（第86期・**紙で停止した。2×2 は1戦も回していない**）。紙のスループット・窓口の全数・交わり  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 mender run <x> [skip] [take]  # 2×2（x = 0 現行 / 1 巻き込み則・全 / 2 吸い・余波だけ）。**未実行**
    dotnet run --project BattleSim -c Release 0 mender tables <x0> <x1> <x2>  # 表A〜F・Q1〜Q5・副判定 (A)。**未実行**
    dotnet run --project BattleSim -c Release 0 mender check     # `compare` 61 行の X0/X1/X2/**X1P**・拒否権1〜3・(e)・`docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 blaze2 phase0    # 傷口に毒を流す（第87期・**測って採用しなかった**）。紙のスループット・持続係数・交わり  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 blaze2 run <y> [skip] [take]  # 2×2（y = 0 現行 / 1 着火）。50 組 × 128 台・約 45 秒/版
    dotnet run --project BattleSim -c Release 0 blaze2 ideal      # 理想台4台（副判定 (C) の分子）。**`CompareBuilds()` は触らない**
    dotnet run --project BattleSim -c Release 0 blaze2 tables <y0> <y1>  # 表A〜G・Q1〜Q4・自己検査
    dotnet run --project BattleSim -c Release 0 blaze2 check      # `compare` 61 行の Y0/Y1・拒否権1〜3・`docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 gauge phase0     # 物差しを直す（第88期）。ノブの既定・各期の A と B・2×2 の定数（**戦闘0回**）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 gauge run <p> <v> [skip] [take]  # 2×2 を TSV へ（p = 84/85/87・v = 0/1・分割実行・約 40 秒/版）
    dotnet run --project BattleSim -c Release 0 gauge null <87v0.tsv>  # 陰性対照（ノイズ床）。**§5 より先に回す。追加費用ゼロ**
    dotnet run --project BattleSim -c Release 0 gauge redo <p> <v0> <v1> <null>  # 1機構ぶんの再検定
    dotnet run --project BattleSim -c Release 0 gauge tables <6つの TSV>  # 表A〜F（台の内訳 / ノイズ床 / 3機構 / 判定の異同）
    dotnet run --project BattleSim -c Release 0 gauge ideal       # 理想台にも情報帯を当てる（副判定 (C)）
    dotnet run --project BattleSim -c Release 0 gauge veto <p> <v>  # 拒否権（`compare` 61 行）
    dotnet run --project BattleSim -c Release 0 gauge check       # `docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 gather redo87    # 傷も肩代わりする（第89期・**紙で止めた**）。(P1) 第87期を別標本で再判定  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 gather run87 <v> [skip] [take]  # (P1) の 2×2 を TSV へ（分割実行）
    dotnet run --project BattleSim -c Release 0 gather seats     # (P2) 席。**`confirm` ではなく `docs/reseat.md` から生きた判定を取る**
    dotnet run --project BattleSim -c Release 0 gather phase0    # 窓口の確認と**紙のスループット**（停止条件）
    dotnet run --project BattleSim -c Release 0 gather ideal     # 源・中継・終端を手でそろえた台（**紙で止まった原因の切り分け**）
    dotnet run --project BattleSim -c Release 0 gather check     # 自己検査（Z0/Z1 の `compare` 61 行・`docs/balance.md` との 305 セル）
    dotnet run --project BattleSim -c Release 0 soak redo89      # 傷口から滲む（第90期・**採用**）。(P1) 第89期を門を外して再判定  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 soak phase0      # 表B（毒を書く箇所の全数）・**門（鎖が繋がっているか）**・表C（紙）
    dotnet run --project BattleSim -c Release 0 soak ideal       # 理想61行で W0 対 W1（Q2/Q3/Q4・副判定・拒否権・全セル差分）
    dotnet run --project BattleSim -c Release 0 soak run <w> <a> [skip] [take]  # 2×2 の TSV（a = kiri / nomi / gald・分割実行）
    dotnet run --project BattleSim -c Release 0 soak tables <TSV...>  # 表D（主判定）
    dotnet run --project BattleSim -c Release 0 soak check       # 自己検査 (a)〜(j)
    dotnet run --project BattleSim -c Release 0 soak phase0b     # 第91期: 表A（**軸交差の行数**・各駒の「他の行」）・門・紙  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 soak split       # `compare` を V0 / Vc / Vp の3版で（表B・C・**壊れか制約かの分解**）
    dotnet run --project BattleSim -c Release 0 soak run2 <a> [skip] [take]  # 2×2 の TSV（**V0 と Vp を両方**吐く・分割実行）
    dotnet run --project BattleSim -c Release 0 soak foe         # **敵側をローカル台で測る**（第90期の「発火 0.0%」は台の欠陥だった）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 soak tables2 <TSV...>  # 表D（**(G3) によりフィルタ無しが主判定**）
    dotnet run --project BattleSim -c Release 0 soak check2      # 第91期の自己検査  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 cross phase0     # 交差帯（第92期）。交差の空白表・55組×61行・選定規則の適用（**戦闘0回**）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 cross quality > docs/crossing.md  # 交差帯 12 行の勝率表と品質（Q1〜Q3・席）
    dotnet run --project BattleSim -c Release 0 cross redo <84|86>  # 第84期（`ThornRule`）／第86期（`MendRule`）の再判定  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 cross check       # 自己検査（`compare` 305 セル・規則の既定・`PickOne`）
    dotnet run --project BattleSim -c Release 0 deep phase0       # 深手（第93期・**測って採用しなかった**）。表A（門・傷を書く箇所の全数・紙）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 deep run <a> [skip] [take]  # 2×2 の TSV（a = nomi / kiri。**W0 と W1 を両方**吐く・分割実行）
    dotnet run --project BattleSim -c Release 0 deep tables <TSV...>  # 表C（主判定。フィルタ有無の両方）
    dotnet run --project BattleSim -c Release 0 deep cross        # 表B・D・E（理想61行・(G2) の壊れ／制約・交差帯12行）
    dotnet run --project BattleSim -c Release 0 deep foe          # 表A'（**敵側をローカル台で測る**。消費型の読み手を入れた対照つき）
    dotnet run --project BattleSim -c Release 0 deep check        # 表F（自己検査 (a)〜(j)）
    dotnet run --project BattleSim -c Release 0 derive rules > docs/rules.md  # ノブ一覧を生成する（第94期・**戦闘0回**）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 derive scan       # 手で写した表を、走らせて観測した事実と突き合わせる（4秒）
    dotnet run --project BattleSim -c Release 0 derive check      # 自己検査（`compare` 305 セル・交差帯12行・印の全列挙）
    dotnet run --project BattleSim -c Release 0 curse grid       # 交差の空白表を**観測から機械で引き直す**（第95期）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 curse phase0     # 呪いの数え物（`StatusKeys` / なまりの書き手・読み手 / 減算の全数 / `Stoic` / `Kinds`）
    dotnet run --project BattleSim -c Release 0 curse pick       # 選定規則を先に固定して3形に当てる
    dotnet run --project BattleSim -c Release 0 curse run <v> [skip] [take]  # 2×2 を TSV へ（v = 0 対照 / 1 呪い則・分割実行）
    dotnet run --project BattleSim -c Release 0 curse tables <v0> <v1>  # 表E（主判定・ノイズ床）
    dotnet run --project BattleSim -c Release 0 curse veto       # 拒否権（`compare` 61行・交差帯12行）
    dotnet run --project BattleSim -c Release 0 curse check      # 自己検査（**陽性対照の経路別の表つき**）
    dotnet run --project BattleSim -c Release 0 hex rename      # 呪い（第96期・**測って採用しなかった**）。(R1) `CurseRule` → `SoakRule` の畳み込みが ±0.0  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 hex phase0      # 表B・F（門・紙・事実・**盤面に書き込まない駒の一覧**）
    dotnet run --project BattleSim -c Release 0 hex run <v> [skip] [take]  # 2×2 を TSV へ（v = 0 対照 / 1 呪い・分割実行）
    dotnet run --project BattleSim -c Release 0 hex tables <v0> <v1>  # 表C（主判定・ノイズ床・情報帯の割合）
    dotnet run --project BattleSim -c Release 0 hex veto        # 表D（拒否権・(G2) の分解・交差帯12行）
    dotnet run --project BattleSim -c Release 0 hex fire        # 表E（呪いの分布・共有の内訳・味方側の損・ネルの自己課税）
    dotnet run --project BattleSim -c Release 0 hex check       # 表G（自己検査。**V2 ＝ 印だけ・共有 0 が V0 と一致すること**）
    dotnet run --project BattleSim -c Release 0 run tables      # 会戦の指標と「勝ち方の質」（73行・第100期・調査。19秒）  → LESSONS_081_100.md
    dotnet run --project BattleSim -c Release 0 run phase0      # 表A（`EnemyCatalog.Columns` の全数）。**戦闘0回**
    dotnet run --project BattleSim -c Release 0 run engage <col>  # 会戦だけ（col = 5 順路 / 3 地点）
    dotnet run --project BattleSim -c Release 0 run solo        # 勝ち方の質だけ（＋ `docs/chain.md` との再現検算）
    dotnet run --project BattleSim -c Release 0 recover tables  # 境界の回復を5版で掃引（73行 × 地点3波・第101期。8分）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 recover phase0  # ノブと境界の窓口の地図（**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 choice tables   # 境界の3つの罰から1つ免除する（4方策＋最良・第102期。8分）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 choice phase0   # `OnCarryOver` の全数・`StatusKeys` の全数・併用の扱い（**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 betray phase0   # 背かれ（第103期・**採用**）。事実・門・紙・予測（V1 毎ターン / V2 死体が席を塞ぐ）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 betray run <skip> <take>  # 2×2（A = ソム。**V0 と V1 を両方吐く**・分割実行・約 4.5 分）
    dotnet run --project BattleSim -c Release 0 betray tables <TSV...>    # 表B・C（主判定・間接の読み手・損の側）
    dotnet run --project BattleSim -c Release 0 betray engage   # 表D（会戦 地点3波 Q4・餌の代金 Q5）
    dotnet run --project BattleSim -c Release 0 betray check    # 表E・F（拒否権が原理的に立たないことの実測・自己検査 (a)〜(k)）
    dotnet run --project BattleSim -c Release 0 encore phase0   # 再行動（第104期・**採用**）。表A（事実・門・紙・予測）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 encore run <a> <skip> <take>  # 2×2（a = nomi / kiri。**V0 と V1 を両方吐く**・分割実行・約 5 分/A）
    dotnet run --project BattleSim -c Release 0 encore tables <TSV...>  # 表B〜E（主判定・Q4/Q5・傷軸8行・拒否権）
    dotnet run --project BattleSim -c Release 0 encore check    # 表F（自己検査 (a)〜(k)）
    dotnet run --project BattleSim -c Release 0 tempo phase0    # 手番の値段（第105期・調査）。境界の定義・帰属できない経路・§1-4 の数え直し（**戦闘0回**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 tempo run       # 表B（駒ごとの手番。73行 × 5波 × seed 0..199・約 13 秒）
    dotnet run --project BattleSim -c Release 0 tempo tables    # 表A〜D（Q1 の再現・駒ごと・分類・内訳）
    dotnet run --project BattleSim -c Release 0 tempo check     # 自己検査（(b) 版に依らない ／ (c) 手番数 ／ (d) 出力の3分割）
    dotnet run --project BattleSim -c Release 0 hold phase0     # 保留の4枚（第106期）。**AtkBonus を動かす経路の全数**（観測）・ムドの実測・弾く先の規則  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 hold run <p>    # 駒ごとの4通貨の表（p = 0..5）
    dotnet run --project BattleSim -c Release 0 hold hari       # (T3) ハリの現状だけ
    dotnet run --project BattleSim -c Release 0 hold tables     # 表A〜F（73行 × 5波 × 200 seed × 6版 ＝ 438,000 戦・約 64 秒）
    dotnet run --project BattleSim -c Release 0 hold check      # 自己検査 (a)〜(f)
    dotnet run --project BattleSim -c Release 0 hold2 phase0    # 保留を閉じる（第107期）。地図（傷の窓口の全数・憤怒の保持者・ハリの行）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 hold2 seats     # (S1) 61 行の席を追試し直す（**`confirm` の台帳は使わない**・約 3 分）
    dotnet run --project BattleSim -c Release 0 hold2 rage      # (S2) ムドの再判定（**seed 200..599**。第106期と帯を変えてある）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 hold2 suture    # (S3) 縫いの発火口（4通りの対照つき。**採用しなかった**）
    dotnet run --project BattleSim -c Release 0 hold2 check     # 自己検査（必須4項目）
    dotnet run --project BattleSim -c Release 0 taillight        # 尾灯（トモ）の受け入れ確認（第108期。**測定ではない**。自己検査 (a)〜(g) だけ）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 tomo [モード]   # 尾灯のトモの測定（第109期）。門・4台×対照・譲渡・灯・ミオの再測定  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 tomo phase0     # 地図と門（速5以下の全数 / 窓口 / `刻み×澱み` の席 / `crossing.md` のハリ）
    dotnet run --project BattleSim -c Release 0 tomo run        # 4台 × 対照（表A〜D）。**`Presets` は1行も触らない。台は診断のローカル**
    dotnet run --project BattleSim -c Release 0 tomo mio        # 表E（`刻み×澱み` の素体差し替えと、着火が実際に走っているかの波別）
    dotnet run --project BattleSim -c Release 0 tomo check      # 自己検査（必須4項目 ＋ 灯の台帳の収支）
    dotnet run --project BattleSim -c Release 0 tomo yield [モード] # 譲渡条件の3版（第110期）。V0 現行 / **V1 窓（採用）** / V2 即時（**採用しなかった**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 tomo yield phase0  # 旧台での V0 の再現・**土台 220 組の全数探索**・`CanReact` の全数・8項目
    dotnet run --project BattleSim -c Release 0 tomo yield run     # 新4台 × 3版 × 素体
    dotnet run --project BattleSim -c Release 0 tomo yield tables  # 表A〜E（**表A' は V2 の段。どこで鎖が切れるか**）
    dotnet run --project BattleSim -c Release 0 tomo yield check   # 自己検査（(g') 粛は伝令を割ると解ける、が入っている）
    dotnet run --project BattleSim -c Release 0 ledger [モード]  # 帳簿: `Presets` の行を差し替える（第111期）。**機構は1つも変えない**  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 ledger phase0    # 61 行の冗長表（r / max|Δ| / 情報セル）・唯一の観測点・不変量（**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 ledger unique    # **規則を1本ずつ切って動く行を数える**（「唯一の観測点」の実測。2分14秒）
    dotnet run --project BattleSim -c Release 0 ledger drop      # 落とす行が「なぜ測らなくなったか」を帯A/帯B で
    dotnet run --project BattleSim -c Release 0 ledger seat      # 新しい行の席（120通り・情報セルつき）
    dotnet run --project BattleSim -c Release 0 ledger cross     # 交差帯の終端の候補（他の 11 行が ±0.0 であることつき）
    dotnet run --project BattleSim -c Release 0 ledger check     # 判定と自己検査
    dotnet run --project BattleSim -c Release 0 lit [モード]    # 灯の対象選択（第113期）。**濾しは残置・採否は第114期**  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 lit phase0      # (G14) の波及調査（情報セル 1 以下の行 × `docs/reseat.md`）・濾しに掛かる駒の全数（**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 lit reseat [絞り込み]  # **第113期の本体**。情報セル 1 以下の行の席を帯A で測り直す（120通り・6行で約4分）
    dotnet run --project BattleSim -c Release 0 lit run         # 灯の濾し 3版（W0/W1/W2）× 61行 ＋ 副判定4台 ＋ 素体（16秒）
    dotnet run --project BattleSim -c Release 0 lit seat [w0|w1|w2]  # 採用版が決まってからトモの行の席を測り直す
                                                                   # **第114期の段つき**（採る席の優先順は情報セルが先・狙は線にも採る条件にも掛かる）
    dotnet run --project BattleSim -c Release 0 lit check       # 自己検査（必須4項目 ＋ (a)〜(f)）
    dotnet run --project BattleSim -c Release 0 reader [モード] # 強化の2枚目の読み手（第115期・**判定は採用／搭載は次期**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 reader phase0   # 表P（**(B) の索引照合**・窓口の再確認・載せる駒の機械的な絞り込み）。**戦闘0回**
    dotnet run --project BattleSim -c Release 0 reader supply   # 選定規則 (b)（強化の受け手の実測。61行 × 5波 × seed 0..49）
    dotnet run --project BattleSim -c Release 0 reader run      # 表A〜D（5台 × 2版 × 5波 × seed 0..199 ＝ 10,000 戦・2.3 秒）
    dotnet run --project BattleSim -c Release 0 reader check    # 自己検査（必須4項目 ＋ (a)〜(d)。(d) は (G15) の逐語照合）
    dotnet run --project BattleSim -c Release 0 reader load [モード] # 積み過ぎを据えのバンに載せる（第116期・**採用**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 reader load phase0   # 表P（バンを含む行の全数・ガルドの隣接・経路の内訳・`AtkBonus` の格子）
    dotnet run --project BattleSim -c Release 0 reader load run      # 表A〜E（61行 ＋ 交差帯12行 × 2版・門・拒否権・`Baseline`・情報セル）
    dotnet run --project BattleSim -c Release 0 reader load seat     # 表C（(T3) の条件に当たった行だけ。**候補ごとにガルドの隣接と到達率を併記**）
    dotnet run --project BattleSim -c Release 0 reader load check [採用前のbalance.md]  # 自己検査（必須4項目 ＋ (a)〜(e)）
    dotnet run --project BattleSim -c Release 0 boss [モード]   # 「被害で育つ駒」×「死なない駒」の 4×4（第117期・**土台は立たなかった**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 boss phase0     # 表P（8枚の機構・土台候補の全数列挙・決着Tの分布・既存行）
    dotnet run --project BattleSim -c Release 0 boss run        # 表A〜D（16組 × 対照 × 波・傾き・Q1〜Q5）
    dotnet run --project BattleSim -c Release 0 boss long       # 同じ表を**長期戦の台**（敵の最大HP ×4。他は1つも触らない）で
    dotnet run --project BattleSim -c Release 0 boss check      # 自己検査（必須4項目 ＋ (a)〜(e)）
    dotnet run --project BattleSim -c Release 0 tank [モード]   # 時間を買う機構（糧タンク）（第118期・**測って採用しなかった**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 tank phase0     # 表P（**経路表を1行ずつ実測**・§1-B の再測定・波ルール・紙）
    dotnet run --project BattleSim -c Release 0 tank run        # 表A〜E（8台 × 対照 × 波・門1〜3・傾き・Q1〜Q6）
    dotnet run --project BattleSim -c Release 0 tank check      # 自己検査（必須4項目 ＋ (a)〜(g)）
    dotnet run --project BattleSim -c Release 0 wound2 [モード] # 傷という通貨の棚卸し（第120期・**測定だけ**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 wound2 phase0   # 表P（書き手6・counter を書き換える7箇所・読む行32・記録。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 wound2 run      # 表A〜E（在庫・消滅の帳簿・実効単価・鎖と帰属・案Aの材料）
    dotnet run --project BattleSim -c Release 0 wound2 check    # 自己検査（必須4項目 ＋ (a) 帳簿が閉じる ／ (b) 無効版で 0 ／ (f) 走査で盤面が動かない）
    dotnet run --project BattleSim -c Release 0 wound2 spill [モード] # 巻き込み則の版を並べる（第121期・**測定だけ。既定は変えていない**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 wound2 spill phase0 # 表P（味方の刃の全数・味方の傷の読み手・代金の予告・第85/86/88期の線。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 wound2 spill run    # 3版（S0 全 / S1 `Dense` / **S2 停止＝第122期からの既定**）× 73行 ＋ Q1 の再現（66 秒）
    dotnet run --project BattleSim -c Release 0 wound2 spill check  # 自己検査（第121期 ＋ **第122期**。(a') は「**どの版が既定と一致するか**」に作り直した）
    dotnet run --project BattleSim -c Release 0 wound2 spill phase0 adopt # **第122期**: 表P（予告した行・`PrimaryRows` の別・旧文の出所。**戦闘0回**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 wound2 spill adopt        # **第122期**: A0 / A1 / A3 × 73行（**A3 − A1 が読み手2枚を降ろしたぶん**・61 秒）
    dotnet run --project BattleSim -c Release 0 wound2 spill seat         # **第122期**: 席の再判定（条件に当たった行だけ・(G16) の「狙で落ちた席」の列つき）
    dotnet run --project BattleSim -c Release 0 watch [モード]   # 見る地図（第123期・**採否の判定を持たない**。盤面は1ビットも動かない）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 watch phase0     # 盲点表（**戦闘0回**。窓口の無い通貨 / `ActorId` を持たない種類 / 既知の欠落）
    dotnet run --project BattleSim -c Release 0 watch            # 主表（候補行 28 行 × 波 × 代表 seed）→ `docs/watch.md`
    dotnet run --project BattleSim -c Release 0 watch cast [行名の部分一致]  # 台本の出演内訳（1文と出演を横に並べる）
                                                            # **第124期に「書き手（`ActorId`）が載った件数」の表を足した**（種類 × あり/null）
    dotnet run --project BattleSim -c Release 0 offturn [モード]  # 手番の外（第125期・**採否の判定を持たない**。盤面は1ビットも動かない）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 offturn phase0    # Q0-1〜Q0-9（**戦闘0回**。介入の鎖の全段 / `Reaction`・`Relayed` の全経路 / ターン頭の一括 / 再生の間）
    dotnet run --project BattleSim -c Release 0 offturn check     # A3（段の数 ＝ `InterceptLabels.All`）・A4（`Intercept` を読む規則 0 件）と実戦の発火
    dotnet run --project BattleSim -c Release 0 offturn tempo <旧Main.cs>  # A6（再生の総尺の前後。**無条件の間だけで重み付けする**）
    dotnet run --project BattleSim -c Release 0 time [モード]     # 軸が回る前に落ちる問題（第126期・**3案とも採用しなかった**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 time phase0       # Q0-1〜Q0-8（誰が何ターン目に落ちるか / 紙のT / `Stoic` と自前の回復 / 圧勝率の定義）
    dotnet run --project BattleSim -c Release 0 time run          # 3案 × 第117期の台（表A / **表A' 門** / 表B / 表C）
    dotnet run --project BattleSim -c Release 0 grade [モード]    # 段（攻撃型の格上げ）（第127期・**段2 で据えのバンに載せた**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 grade phase0      # Q0-1〜Q0-9（**格上げの一覧の正本**・状態キーの書き手と読み手・`AtkBonus` の供給）
    dotnet run --project BattleSim -c Release 0 grade run         # V0 対照 / V1 薙ぎ / V2 貫き / V3 全体 / V4 段 × 台3つ（**台3 は供給なしの陰性対照**）
    dotnet run --project BattleSim -c Release 0 grade2 [モード]   # 段の載せ替え（第128期・**ドルガに載せた**）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 grade2 phase0     # Q0-1〜Q0-9（`GradeStep` の形・`Whet` の宛先・休む駒・ドルガの行と席）。**戦闘0回**
    dotnet run --project BattleSim -c Release 0 grade2 stock > docs/stock.md  # 強化の棚卸し（52枚 × `外`／`到達`／休み／読み手）
    dotnet run --project BattleSim -c Release 0 grade2 run        # 発火率（全体で振った割合）・到達率・`AtkBonus` の格子（**分母を先に割る**）
    dotnet run --project BattleSim -c Release 0 grade2 check [採用前のbalance.md]  # 自己検査（必須4項目）
    dotnet run --project BattleSim -c Release 0 survive [モード]  # 指標の直しと「起動まで守れるか」（第129期）  → LESSONS_101_.md
    dotnet run --project BattleSim -c Release 0 survive phase0    # Q0-1〜Q0-9（現行「完全勝利」の実証・標の書き手・生存Tの取り直し）
    dotnet run --project BattleSim -c Release 0 survive run       # 段2 の延命台（`TraitId.Undying`・**発火の密度**）
    dotnet run --project BattleSim -c Release 0 survive hp <駒Id> # 段3 の逆算（`MaxHp` だけを振って 61 行 × 5 波・拒否権・情報セル）
    dotnet run --project BattleSim -c Release 0 survive check [採用前のbalance.md]  # 自己検査（必須4項目 ＋ 免除の集合 ＋ 決定性）
    dotnet run --project BattleSim -c Release 0 ember [モード]    # 火を配る（第130期・**測って採用しなかった。既定 `Off` で残置**）
    dotnet run --project BattleSim -c Release 0 ember phase0      # Q0-1〜Q0-9（燃焼の書き手2枚・読み手2枚・`Ignite` の再付与・隣接・行）。**戦闘0回**
    dotnet run --project BattleSim -c Release 0 ember run         # V0 配らない 対 V1 配る（`EmberRule.On`）× 61行 × 5波
    dotnet run --project BattleSim -c Release 0 ember check [採用前のbalance.md]  # 自己検査（必須4項目 ＋ A2/A3/A4 ＋ `Off` の 0 件一致）
    dotnet run --project BattleSim -c Release 0 wildfire [モード]  # 火勢（第133期・**段1 で落ちた。既定 `Off` で残置**）。撒いた火を読む（ボルグ）
    dotnet run --project BattleSim -c Release 0 wildfire phase0    # Q0-1〜Q0-10（`ModifyAttack` の署名・`Board`・着火の全件・行）。**戦闘0回**
    dotnet run --project BattleSim -c Release 0 wildfire run       # 段1（V0 対照 / V1 加算 / V2 乗算 / V3 二値 × 2台 × 第2〜5波）
    dotnet run --project BattleSim -c Release 0 wildfire compare   # 段2 の器具（`compare` 61行 ＋ 交差帯12行）。**第133期は使っていない**
    dotnet run --project BattleSim -c Release 0 wildfire check [採用前のbalance.md]  # 自己検査（必須4項目 ＋ A4/A6/A10 ＋ `Off` の 0 件一致）
    dotnet run --project BattleSim -c Release 0 stacks [モード]   # 重ね掛けと盤面ルールの対称性（第134期・**測定だけ**。盤面は1ビットも動かない）
    dotnet run --project BattleSim -c Release 0 stacks phase0     # Q0-1〜Q0-8（`relit` の用途・`Burn` の意味・書き手・保持者と波・窓口）。**戦闘0回**
    dotnet run --project BattleSim -c Release 0 stacks burn       # 段1（**区間ごとの点け直し回数**・陣営別・駒別・行別）
    dotnet run --project BattleSim -c Release 0 stacks rules      # 段2（**軛・渇き・粛の3ルール一覧**・陣営別・経路別・保持者が落ちるターン）
    dotnet run --project BattleSim -c Release 0 stacks check [採用前のbalance.md]  # 自己検査（A1/A2/A4/A10 ＋ 必須4 ＋ 帳簿が閉じるか）
    dotnet run --project BattleSim -c Release 0 parry [モード]    # ガルドは何で死んでいるか（と、受け流し）（第135期）
    dotnet run --project BattleSim -c Release 0 parry phase0      # Q0-1〜Q0-10（段の並び・軽減の全件・`Stoic` の素体対照）
    dotnet run --project BattleSim -c Release 0 parry harm > docs/harm.md  # 段1 の害の帳簿（経路別の総量と**致命打**）
    dotnet run --project BattleSim -c Release 0 parry scan        # 段2 の台の下見（**版を1つも振らない**。40〜95% の帯）
    dotnet run --project BattleSim -c Release 0 parry run         # 段2（受け流し V0/V1/V2 × N ＝ 中央値から引く）
    dotnet run --project BattleSim -c Release 0 parry check [採用前のbalance.md]  # 自己検査（必須4項目 ＋ (a)〜(d)）
    dotnet run --project BattleSim -c Release 0 wall [モード]     # ガルドを壁にする（第136期・**段1〜3 を採用**）。本体は `Wall.cs`
    dotnet run --project BattleSim -c Release 0 wall phase0       # Q0-1〜Q0-10（構えの3形・`SurrendersTurn`・`Stoic` の経路・群A/B/C・段の並び）。**戦闘0回**
    dotnet run --project BattleSim -c Release 0 wall n            # §5-2 の N の導出（`ParryRule(0)` ＝ 段1 の帳簿から）
    dotnet run --project BattleSim -c Release 0 wall run [段1のbalance.md] [n=4,5,6] [relay]  # 版 × 群A/B/B'/C・ガルドの帳簿・採否の線（13 版で 3 分）
    dotnet run --project BattleSim -c Release 0 wall check [段1のbalance.md]  # 自己検査（必須1・(c) `Uses=0` が段1 と同値・A10・敵側 0 件・段の位置）
    dotnet run --project BattleSim -c Release 0 shard [モード]    # 砕けの鍵を自前にする（第137期・**測って採用しなかった**）。本体は `Shard.cs`
    dotnet run --project BattleSim -c Release 0 shard phase0    # Q0-1（現行の帳簿）・Q0-3（ヒビを含む行を**走らせて引く**）・掰引の中心
    dotnet run --project BattleSim -c Release 0 shard scan      # 段1 の台の下見（**V0 の第2〜5波平均が 40〜95% か**。版を振らない）
    dotnet run --project BattleSim -c Release 0 shard run [costのカンマ区切り]  # V0 受動 / V1 自前 / V2 両方 × 台4つ × 既存５行
    dotnet run --project BattleSim -c Release 0 shard check docs/balance.md  # 自己検査（必須1 ＋ (a)〜(d)／第138期の (G-a)〜(G-e)）
    dotnet run --project BattleSim -c Release 0 shard gscan     # **第138期 段4**: 礫の台の下見（V0 ＝ ガレを素体に落とした版が 40〜95% か）＋ 掃引の中心
    dotnet run --project BattleSim -c Release 0 shard gscan probe [駒Idのカンマ区切り]  # 埋め草の組の走査（**4台とも帯に入る組を探す**）
    dotnet run --project BattleSim -c Release 0 shard gare [Multiplierのカンマ区切り]   # **第138期 段5**: 掃引（V0 素体 × M）・ウロの纏い率・軛の通し率
    dotnet run --project BattleSim -c Release 0 brace [モード]    # ササの転生（第143期・**段C を採用**）。本体は `Modes/Brace.cs`
    dotnet run --project BattleSim -c Release 0 brace phase0     # Q0-1〜Q0-7（**敵の一撃の分布から N を引く**・ササの帳簿・台の下見）
    dotnet run --project BattleSim -c Release 0 brace scan [probe]  # 台の下見だけ／`probe` は埋め草の総当たり（帯 40〜95% ＋ 情報セル 3 以上）
    dotnet run --project BattleSim -c Release 0 brace run [Nのカンマ区切り]  # 段A〜D（既定 7,12,20）＋ 波別の供給
    dotnet run --project BattleSim -c Release 0 brace log "台名 N seed"      # 1戦の監査
    dotnet run --project BattleSim -c Release 0 brace check       # 自己検査（必須1・必須4 ＋ (a)(a')(b)(c)(d)）
    dotnet run --project BattleSim -c Release 0 tumult [モード]   # バサの転生（第144期・**段B を採用**）。本体は `Modes/Tumult.cs`
    dotnet run --project BattleSim -c Release 0 tumult phase0    # Q0-1〜Q0-7（**敵の召喚枠**・敵の `IdleTurn` の読み手・**波ごとの席順と特性**）
    dotnet run --project BattleSim -c Release 0 tumult scan       # 台の下見だけ（V0 の第2〜5波平均が 40〜95% ＋ 情報セル）
    dotnet run --project BattleSim -c Release 0 tumult scan probe  # 埋め草の総当たり（帯 40〜95% ＋ 情報セル 3 以上。台名を続けると1台だけ）
    dotnet run --project BattleSim -c Release 0 tumult run       # 段A（敵も乱す）／段B（前に出た敵が転ぶ）／段C（2体とも）× 台4つ ＋ `compare` のバサ行
    dotnet run --project BattleSim -c Release 0 tumult check [採用前のbalance.md]  # 自己検査（必須1・必須4 ＋ (a)〜(f)）
    dotnet run --project BattleSim -c Release 0 confuse [モード]  # 混乱（第146期・**測って採用しなかった**）。本体は `Modes/Confuse.cs`
    dotnet run --project BattleSim -c Release 0 confuse phase0   # Q0-1〜Q0-7 を**実装から引き直す**（戦闘0回）
    dotnet run --project BattleSim -c Release 0 confuse scan      # 台の下見だけ（V0 の第2〜5波平均が 40〜95% ＋ 情報セル）
    dotnet run --project BattleSim -c Release 0 confuse scan probe  # 埋め草の強さを 12 点掃引（4台とも帯に入る組を探す）
    dotnet run --project BattleSim -c Release 0 confuse run       # 段B（V0 対 V1 × 台4つ）＋ 混乱の帳簿
    dotnet run --project BattleSim -c Release 0 confuse dir       # **段B' 供給の向きを分ける**（敵だけ／両陣営／味方だけ／なし）
    dotnet run --project BattleSim -c Release 0 confuse half      # 段C（弱めた版 50% を1点だけ・採用はしない）
    dotnet run --project BattleSim -c Release 0 confuse compare   # 拒否権（`compare` 61 行・主判定19行の第五波）
    dotnet run --project BattleSim -c Release 0 confuse check [採用前のbalance.md]  # 自己検査（必須1・必須4 ＋ (a)〜(e)）
    dotnet run --project BattleSim -c Release 0 derange [モード]  # バサの転倒 → 混乱（第147期・**段C を採用**）。本体は `Modes/Derange.cs`
    dotnet run --project BattleSim -c Release 0 derange phase0   # 前提を実装から引き直す（戦闘0回）
    dotnet run --project BattleSim -c Release 0 derange scan     # 台の下見（V0 ＝ **転倒版**。40〜95% の帯）
    dotnet run --project BattleSim -c Release 0 derange run      # 段A（絞りなし）＋ 混乱の帳簿 ＋ **下流**（同士討ち・処刑・殉教）
    dotnet run --project BattleSim -c Release 0 derange knob     # 段B（確率 100/50/25）と 段B'（回数制 0/3/1）を**同じ帯で**
    dotnet run --project BattleSim -c Release 0 derange compare [a|c]  # 拒否権（`compare` 61行 ＋ 交差帯12行）
    dotnet run --project BattleSim -c Release 0 derange check [balance.md]  # 自己検査（必須1・必須4 ＋ (a)〜(f)）
    dotnet run --project BattleSim -c Release 0 tumult2 [モード]  # バサの手番を混乱に使う（第148期・**測って採用しなかった**）。本体は `Modes/Tumult2.cs`
    dotnet run --project BattleSim -c Release 0 tumult2 phase0   # 前提を実装から引き直す（戦闘0回）
    dotnet run --project BattleSim -c Release 0 tumult2 seats    # **席の棚卸し**（`docs/reseat.md` を全61行ぶん読む・戦闘0回）
    dotnet run --project BattleSim -c Release 0 tumult2 scan     # 台の下見（V0 ＝ 現行の既定だけ）
    dotnet run --project BattleSim -c Release 0 tumult2 run      # 段B（V0 ターン頭 / V1 手番 / **V2 供給だけ** / **V3 周期**）＋ 号令の死蔵
    dotnet run --project BattleSim -c Release 0 tumult2 compare  # 拒否権（`compare` 61行 ＋ 交差帯12行）
    dotnet run --project BattleSim -c Release 0 tumult2 check    # 自己検査（必須1・必須4 ＋ (a)〜(f)）
    dotnet run --project BattleSim -c Release 0 haste [モード]    # 行動順という通貨の値段（第149期・**測って採用しなかった**）。本体は `Modes/Haste.cs`
    dotnet run --project BattleSim -c Release 0 haste phase0     # 前提を実装から引き直す（戦闘0回）
    dotnet run --project BattleSim -c Release 0 haste run        # 段B/C（V0 既定 / S 遅い / A 強い × `compare` 61行 × 5波 × seed 0..199・21 秒）
    dotnet run --project BattleSim -c Release 0 haste check [balance.md]  # 自己検査（必須1・必須4 ＋ (a)〜(e)）
    dotnet run --project BattleSim -c Release 0 gust [モード]     # バサの一撃を突風にする（第151期・**段A+段B を採用**）。本体は `Modes/Gust.cs`
    dotnet run --project BattleSim -c Release 0 gust phase0     # 前提を実装から引き直す（**薙ぎの出力の勘定**・供給の見積り）
    dotnet run --project BattleSim -c Release 0 gust scan       # 台の下見（V0 の第2〜5波平均が 40〜95% か）
    dotnet run --project BattleSim -c Release 0 gust run [攻撃力のカンマ区切り]  # 段A: 薙ぎ化だけ（既定 2,3,4,5）
    dotnet run --project BattleSim -c Release 0 gust gale "4 10,20,30"  # 段B/C: 転倒の確率と巻き込み（引数は「攻撃力 確率のカンマ区切り」。既定 4 と 10,20,30）
    dotnet run --project BattleSim -c Release 0 gust compare "4 20"  # 拒否権（`compare` 61行 ＋ 交差帯12行。引数は「攻撃力 確率 [primary]」）
    dotnet run --project BattleSim -c Release 0 gust check [balance.md]  # 自己検査（必須1・必須4 ＋ (a)〜(f)）
    dotnet run --project BattleSim -c Release 0 mark [モード]     # 標（`Marked`）の軸を診る（第150期・**測定だけ**。盤面は1ビットも動かない）。本体は `Modes/Mark.cs`
    dotnet run --project BattleSim -c Release 0 mark phase0     # Q0-1〜Q0-7 を**実装から引き直す**（戦闘0回）
    dotnet run --project BattleSim -c Release 0 mark run        # 段B（標の一生の帳簿 × V0 / `Slowest` / `Strongest` × 5行）
    dotnet run --project BattleSim -c Release 0 mark rows       # **全 61 行で分母を数え直す**（取り分 ＝ Δ ÷ (100 − V0)・規約 (G4)）
    dotnet run --project BattleSim -c Release 0 mark check [balance.md]  # 自己検査（必須1・必須4 ＋ (a)〜(e)。**帳簿が閉じるか**）
    dotnet run --project BattleSim -c Release 0 stall [モード]    # 膠着（30 ターン上限）を塞ぐ（第152期・**段C を採用**）。本体は `Modes/Stall.cs`
    dotnet run --project BattleSim -c Release 0 stall phase0    # **膠着の分母を数える**（上限到達が出るセル / 行。V0 だけ）
    dotnet run --project BattleSim -c Release 0 stall run       # 段C（Off / WhenFull / WhenStocked × compare 61行）＋ ガルドの帳簿 ＋ 情報セルの内訳
    dotnet run --project BattleSim -c Release 0 stall check [balance.md]  # 自己検査（必須1・必須4 ＋ (a)〜(f)）
    dotnet run --project BattleSim -c Release 0 ward [モード]     # 預かり（第153期・**測って採用しなかった**）。本体は `Modes/Ward.cs`
    dotnet run --project BattleSim -c Release 0 ward phase0     # 前提を実装から引き直す（中央値・被弾の読み手・死の経路・状態キーの費用）
    dotnet run --project BattleSim -c Release 0 ward scan       # 台の下見（素体版の第2〜5波が 40〜95% か）
    dotnet run --project BattleSim -c Release 0 ward scan probe  # 埋め草の掃引（**台ごとに**帯へ入れる）
    dotnet run --project BattleSim -c Release 0 ward run        # 段B（V0 素体 / Burst / **Burst 預かりのみ** / Drip / **Drip 預かりのみ** × 台5つ）
    dotnet run --project BattleSim -c Release 0 ward sweep [percent]  # 段C（`Threshold` と `Drip` ／ `percent` で `Percent`）
    dotnet run --project BattleSim -c Release 0 ward check [balance.md]  # 自己検査（(a)〜(g)。**収支が閉じるか**）
    dotnet run --project BattleSim -c Release 0 wardcost [モード] # 預かりの代金を付け替える（第154期・**採否はポンの判断待ち**）。本体は `Modes/Wardcost.cs`
    dotnet run --project BattleSim -c Release 0 wardcost phase0 # 変換器を実装から数え直す（`Burden` の変換器 ／ `Laden` が無料になる駒 ／ 段の並び）
    dotnet run --project BattleSim -c Release 0 wardcost scan [probe]  # 台の下見（素体版が 40〜95% か）／`probe` は埋め草の掃引
    dotnet run --project BattleSim -c Release 0 wardcost run    # 段B（V0 素体 / **Vp 預かりのみ** / V1 没収 / V2 荷 / V3 重り × 台5つ）
    dotnet run --project BattleSim -c Release 0 wardcost sweep  # 段B'（`BurdenPercent` 25/50/75 と `LadenPer` 5/10/20）
    dotnet run --project BattleSim -c Release 0 wardcost band   # 段C（`Percent` と `Threshold` を同率で下げる。**返却率は保たれない**）
    dotnet run --project BattleSim -c Release 0 wardcost check [balance.md]  # 自己検査（(a)〜(h)。**(d) は第153期の値の再現**）
    dotnet run --project BattleSim -c Release 0 toll [モード]     # 贖いのアガ（第155期・**測って採用しなかった**）。本体は `Modes/Toll.cs`
    dotnet run --project BattleSim -c Release 0 toll phase0     # 前提を実装から引き直す（中央値 ／ 肩代わり ／ 標的選択 ／ 帳簿 ／ 過去の同型）
    dotnet run --project BattleSim -c Release 0 toll scan [probe|probevp]  # 台の下見（`probe` は V0 で帯へ／**`probevp` は Vp で帯へ**）
    dotnet run --project BattleSim -c Release 0 toll run        # 段B（V0 素体 / Vp 前借りのみ / V1 Single / V2 All × 台5つ）
    dotnet run --project BattleSim -c Release 0 toll cost       # 段B' 決着T のはしご（**代金の分母を `Vp` で帯に入れる**）
    dotnet run --project BattleSim -c Release 0 toll contract   # 追補: 契約枠の検証（**`Vp` が `Contracts = 1` で壊れていないか**）
    dotnet run --project BattleSim -c Release 0 toll sweep [contract|advance]  # 段C（`Threshold` / `Contracts` / `Advance`）
    dotnet run --project BattleSim -c Release 0 toll check [balance.md]  # 自己検査（(a)〜(j)。**収支が閉じるか**）
    dotnet run --project BattleSim -c Release 0 stage [モード]    # ステージ単位で駒を測る（第157期・**器具の期**）。本体は `Modes/Stage.cs`
    dotnet run --project BattleSim -c Release 0 stage phase0    # 前提を実装から引き直す（列 ／ 版 ／ 線 ／ 素体 ／ **予測を先に書く**）。戦闘0回
    dotnet run --project BattleSim -c Release 0 stage scan ["列名"]  # 段1 台の帯（列3本 × 版13版 × 61行。31 秒）
    dotnet run --project BattleSim -c Release 0 stage run ["列 版"]  # 段2 `ablate` の会戦版（**会戦帰属 − 単発帰属**。既定は `順路5 RFull`）
    dotnet run --project BattleSim -c Release 0 stage life ["列 版" 駒Id...]  # 段3 寿命の帳簿（既定は nono・vel・rica・gald。**`verbose: true`**）
    dotnet run --project BattleSim -c Release 0 stage perm [版のカンマ区切り]  # **第159期** 段A 5波の全120順列 × 版（既定 R0,BCarry・145 秒）
    dotnet run --project BattleSim -c Release 0 stage permsd [版のカンマ区切り]  # **第159期** 段A' **帰属SD そのもの**で 240 点（822 秒・ρ は測らない）
    dotnet run --project BattleSim -c Release 0 stage obj phase0  # **第162期** 段0 Q0-2/3/5（`EngagementResult` から取れる量・戦闘0回）
    dotnet run --project BattleSim -c Release 0 stage obj floor   # **第162期** 段0 Q0-4 床と天井（61 行ぶんだけ・**`ablate` を回す前**・2.3 秒）
    dotnet run --project BattleSim -c Release 0 stage obj ["列 版,..."]  # **第162期** 段A/B/C 目的変数4つ（突破度／生存枚数／残HP割合／**与えた総害**）を**1回の走行から同時に**取る
                                                                 # 既定は `順路5 R0,順路5 BCarry,地点3 R0,地点3 BCarry`（30.6 秒）。
                                                                 # **線3 は `順路5` と `地点3` の<u>両方</u>で線1・線2 を要求する**（列長の交絡を切る）
    dotnet run --project BattleSim -c Release 0 stage cross phase0  # **第163期** 前提（列 / 版 / 順位付けの規則 / 走行の見積り。**戦镘0回**）
    dotnet run --project BattleSim -c Release 0 stage cross ["列のカンマ区切り"]  # **第163期** 本体 —— **相手（列）が変わると駒の序列は入れ替わるか**
                                                                 # 既定は 12 列 × 版2（R0 / BCarry）× 目的変数 `与えた総害`（125 秒）。
                                                                 # **ρ は使わない。単発側は1回も回さない**——比べるのは列どうしで、
                                                                 # **対照は seed 帯 12 本**（`順路5 × BCarry` を固定して seed 0..2399 を 200 ずつ）。
                                                                 # **帯の本数は列の本数に揃える**（順位レンジは群の数が多いほど大きく出る・R245）
    dotnet run --project BattleSim -c Release 0 stage catalog phase0  # **第164期** 前提（持っている量 / 線 / `checkup` の読み口。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 stage catalog ["列のカンマ区切り"]  # **第164期** 52枚の送り先カタログと列の型
                                                                 # **走行は `stage cross` と同じ 35 点**（12 列 × 版2 ＋ 帯 12 本・153 秒）で、
                                                                 # **同じ帰属から別の集計を作るだけ**——列どうしの相関行列 ／ 型（単連結 r >= 0.70）／
                                                                 # 52 枚の3群（化ける / どこでも同じ / ノイズ）／ `checkup`（第152期）との4象限。
                                                                 # **型は1個に潰れた**（R248）ので、送り先は**連続量**
                                                                 # （重い波が列の何番目に来るかの平均）で書く
    dotnet run --project BattleSim -c Release 0 stage short phase0  # **第165期** 前提（w の単調性 / `P12`≡`地点2` / 走行の見積り / 作戦マップの持ち越し。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 stage short          # **第165期** 短い列の対 —— **中身が同じで並びだけ逆**の 40 列
                                                                 # 長さ2 が 20・長さ3 が 20（5波から 2/3 を選ぶ 10 組 × 2 順）× 版2 ＋
                                                                 # 対照は**長さごとに** seed 帯 12 本の**隣り合う 6 対**（R245。12 群のレンジと比べない）。
                                                                 # **向きは波の番号順ではなく w の順**（w が単調でない＝第三波 < 第二波・Q0-1）。
                                                                 # 114 点・377 秒。**本丸は長さ3 で ○**（線1 5.25 / 5.00・線2 2/3 体）
    dotnet run --project BattleSim -c Release 0 stage catalog seeds=800  # **第165期 部B** seed だけ 4 倍にして読み直す
                                                                 # （**既定 200 は第164期と1ビットも違わない**。seed を増やした版は R0 だけを回す）
    dotnet run --project BattleSim -c Release 0 stage rho ["列 版,列 版,..."]  # **第158期** 段B 目的変数を ρ に（**点の指定は完全一致**。列は `P12345` の順列名も可）
                                                                 # **第161期に線を引き直した**——線1 は `ρ(絞) ≤ 0.75`・
                                                                 # 線2 は **会戦の帰属SD ÷ 単発の帰属SD ≥ 0.80**（旧 `帰属SD ÷ 列長 ≥ 0.162` は参考へ降ろした）
    dotnet run --project BattleSim -c Release 0 stage rhocarry ["列 版,版"]  # **第158期** 段C 入場時の実効攻撃力の上乗せ（`CurrentAttack − Def.Attack`）
    dotnet run --project BattleSim -c Release 0 stage map phase0  # **第166期** 検証用マップ 1-1（`Modes/StageMap.cs`）。前提を実装から引き直す（戦闘0回）
    dotnet run --project BattleSim -c Release 0 stage map rank    # Q0-1 / Q0-2（2 本の道での 5 枚の順位を引き直す）
    dotnet run --project BattleSim -c Release 0 stage map pick    # Q0-2 が落ちたときの列の選び直し（第一波を含まない長さ2 の 12 本）
    dotnet run --project BattleSim -c Release 0 stage map seats   # 席だけ（**道に依らない規則**＝4 部隊への単発勝率の合計）
    dotnet run --project BattleSim -c Release 0 stage map [b]     # 段A 本体（正 対 逆 × seed 帯2本。`b` で埋め草を第2候補に）
    dotnet run --project BattleSim -c Release 0 stage map sweep   # 追補: 拠点の手当て（`BaseRecover`）の掃引。**6 版すべて同値**
    dotnet run --project BattleSim -c Release 0 stage map diag    # 追補: 床の原因の切り分け（隊×敵の単発勝率／**負け方の内訳**／道1本の踏破）
    dotnet run --project BattleSim -c Release 0 stage band phase0  # **第167期** 道中の回復（`Modes/StageMap.cs`）。前提を実装から引き直す（戦闘は下見のぶんだけ）
    dotnet run --project BattleSim -c Release 0 stage band         # 本体 —— 5 回復量（0/25/50/75/100）× 2 割り当て × seed 帯2本。
                                                                 # **隊は `compare` の行そのまま**（第166期 F1 の直し）＋ **控えの第3隊**（F2 の直し）。
                                                                 # 敵も駒も1体も作らない。24 点・約 4 秒
    dotnet run --project BattleSim -c Release 0 stage band b       # 第3隊を第2候補にして確かめる（依存変数）
    dotnet run --project BattleSim -c Release 0 stage scout phase0  # **第168期** 斥候級（`Modes/StageScout.cs`）。前提を実装から引き直す（戦闘は見積りのぶんだけ）
    dotnet run --project BattleSim -c Release 0 stage scout a       # 部A 体数の曲線 —— 既存の第二〜五波から駒を抜くだけの 4 形
                                                                 # （M5 全席 ／ S4 後1 抜き ／ S3r 前1前3中央 ／ S3n 前1前3後3）× 4 波 ＋ 第一波 ＝ 17 編成 ×
                                                                 # `compare` 61 行 × seed 200。**新しい敵の定義は1体も作らない**（12 秒）
    dotnet run --project BattleSim -c Release 0 stage scout b ["北/南,..."]  # 部B マップを「斥候 → 主力」で（既定は S3n/S3r,S3n,S3r,S4）。
                                                                 # 形は `北/南`（スラッシュ無しは両方の道に同じ形）。**16 通りの全格子でも 29 秒**
    dotnet run --project BattleSim -c Release 0 stage scout check   # 自己検査（(b)(b')(d)(f)）
    dotnet run --project BattleSim -c Release 0 stage hane ["駒Idのカンマ区切り"]  # **第172期 部C**（`Modes/StageHane.cs`）
                                                                 # **かき回し隊の 後3 の1枠だけ**を振る
                                                                 # （対照 ＝ 突き返しのハネ ／ 素体 ／ 控えの駒 6 枚）。
                                                                 # 台は第168期 部B の通った1点そのまま
                                                                 # （`S4/S4` × 回復 50% × 正 × seed 800）で、
                                                                 # **敵も道も隊も1つも作らない**。**線は置かない**
                                                                 # （採否ではなくハネの転生の材料）。
                                                                 # 自己検査 (f) ＝ 対照が第169期の 95.6% / 38.0% と一致すること
    dotnet run --project BattleSim -c Release 0 stage check [balance.md]  # 自己検査（(a)〜(g)）
    dotnet run --project BattleSim -c Release 0 rebirth [モード]  # B群の転生（第178期・熾のホタ／逆しまのウツ）。本体は `Modes/Rebirth.cs`
    dotnet run --project BattleSim -c Release 0 mudo [モード]     # 泥人形ムドの手直し（第181期・床＋泥散りの発火口）。本体は `Modes/Mudo.cs`
    dotnet run --project BattleSim -c Release 0 mudo phase0      # Q0-1〜Q0-5 を実装から引き直す（**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 mudo run         # R0〜R3 の4版（**線は置かない**）＋ 診断台 ＋ 勝ち方
    dotnet run --project BattleSim -c Release 0 mudo ledger      # 帳簿（1発の値・床が効いた発・泥・**波ごとの泥と暴発**）
    dotnet run --project BattleSim -c Release 0 mudo check       # 自己検査（(a)〜(g)。**ムド非在席 53 行 0 件差分**）
    dotnet run --project BattleSim -c Release 0 mudohex [モード]  # ムドの呪いを既定でオン（第182期）。本体は `Modes/MudoHex.cs`
    dotnet run --project BattleSim -c Release 0 mudohex phase0   # Q0-1〜Q0-5（Q0-3 だけ `CurseRule(true, 0)` で戦闘を回す）
    dotnet run --project BattleSim -c Release 0 mudohex run      # H0〜H3（呪い × 上乗せ 5/10。**線は置かない**）＋ 診断台 ＋ 勝ち方 ＋ (G2)
    dotnet run --project BattleSim -c Release 0 mudohex ledger   # 帳簿（呪われた敵・共有の出どころ・味方への共有・格差）
    dotnet run --project BattleSim -c Release 0 mudohex check [balance.md]  # 自己検査（(a)〜(f)。**ムド非在席 64 行 0 件差分**・H0 が採用前の balance と一致）
    dotnet run --project BattleSim -c Release 0 rebirth3 [モード] # B群の転生 最後の2枚（第183期・ヴェル／ラウ）。本体は `Modes/Rebirth3.cs`
    dotnet run --project BattleSim -c Release 0 rebirth3 phase0  # Q0-1〜Q0-7 を実装から引き直す（在席・同席・隣・`Actions` の読み手・最大HPの経路）
    dotnet run --project BattleSim -c Release 0 rebirth3 run     # 1枚ずつの対照・**漏れなし（`yP`）**・(G2)・勝ち方・全体の指標（45 秒）
    dotnet run --project BattleSim -c Release 0 rebirth3 ledger  # 縫い（相手の内訳・縫い跡・封じ・蘇生の前後）と うつし・漏れ（受け手・ヴィオが吸った分）
    dotnet run --project BattleSim -c Release 0 rebirth3 check [採用前のbalance.md]  # 自己検査（(a)〜(g)。**55 行 0 件差分**・旧に戻すと採用前と 0 セル）
    dotnet run --project BattleSim -c Release 0 mark184 [モード]  # 標の軸（第184期・ヒサ／ザン／敵の標 +50%）。本体は `Modes/Mark184.cs`
    dotnet run --project BattleSim -c Release 0 mark184 phase0   # Q0-1〜Q0-8（書き手の陣営・`ApplyDamage` の段の並びを本文の位置から・移動の読み手をリフレクションで）
    dotnet run --project BattleSim -c Release 0 mark184 run      # 対照8版（旧／§1 だけ／§1＋ヒサ／§1＋ザン／新／新・§1 なし／逃げない／返り血なし）・(G2)・勝ち方・指標（13 秒）
    dotnet run --project BattleSim -c Release 0 mark184 ledger   # 帳簿（§1 の出どころ・ヒサの付け替えと逃げ・ザンの与害 ÷ 他のアタッカー）
    dotnet run --project BattleSim -c Release 0 mark184 bench    # 診断台（ヒサ×ムド×ザン×トメ・2席）と1戦のログ
    dotnet run --project BattleSim -c Release 0 mark184 strip    # ソラがヒサの守りの標を剥がす率（ポンの追加の問い）
    dotnet run --project BattleSim -c Release 0 mark184 check [採用前のbalance.md]  # 自己検査（(a)〜(e)。**58 行 0 件差分**・旧に戻すと採用前と 0 セル）
    dotnet run --project BattleSim -c Release 0 rebirtha2 [モード] # A群の転生 3〜5枚目（第185期・クグ／シガ／バン）。本体は `Modes/RebirthA2.cs`
    dotnet run --project BattleSim -c Release 0 rebirtha2 phase0  # Q0-1〜Q0-9（在席と同席・専用キーの読み手・標的選択と ApplyDamage の段の並び・SwapSlots の呼び出し口）
    dotnet run --project BattleSim -c Release 0 rebirtha2 run     # 1枚ずつの対照・3枚・`yP`（据えた足なし）・(G2)・勝ち方・**動けない敵の数の分布**
    dotnet run --project BattleSim -c Release 0 rebirtha2 ledger  # 帳簿（組み付き・見せしめ・踏みしめ＋範囲の盾＋空振り）
    dotnet run --project BattleSim -c Release 0 rebirtha2 bench   # 診断台（クグ×シガ／シガ×ガン×2／バン×バサ）と1戦のログ
    dotnet run --project BattleSim -c Release 0 rebirtha2 check [採用前のbalance.md]  # 自己検査（(a)〜(f)。**65 行 0 件差分**・組み付きの帳簿が両側で一致）
    dotnet run --project BattleSim -c Release 0 sora186 [モード]  # 逸らしのソラ（第186期・半分を逸らす）。本体は `Modes/Sora186.cs` / `Sora186.Run.cs`
    dotnet run --project BattleSim -c Release 0 sora186 phase0   # Q0-1 在席・敵の単体攻撃の数（**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 sora186 run      # 旧ソラ 対 新ソラ × 73 行・ソラの生存T と死亡率・(G2)・勝ち方・全体の指標
    dotnet run --project BattleSim -c Release 0 sora186 ledger   # 帳簿（逸らした回数と量・**殴った本人へ**・§1・倒した数・**ソラが何で削られ何で倒れたか**・波別）
    dotnet run --project BattleSim -c Release 0 sora186 bench    # 診断台（ヒサ×ソラ×ザン・ソラ中央／ヒサなしの対照）と1戦のログ
    dotnet run --project BattleSim -c Release 0 sora186 check [採用前のbalance.md]  # 自己検査（(a)〜(d)。**55 行 0 件差分**・§1 を敵が入れていない）
    dotnet run --project BattleSim -c Release 0 sora186 thrust   # **追補**: 逸らし先の内訳（指差した敵／ほかの標持ち／補助）と突きの威力の分布（新 と 素の倍率）。本体は `Sora186.Add.cs`
    dotnet run --project BattleSim -c Release 0 sora186 carry    # **追補**: ハイパーキャリーの診断台（ソラ×ガン×ガルド＋ヒサ／ヒサ → ノミ）
    dotnet run --project BattleSim -c Release 0 sora186 seat     # **追補**: `逸らし (ソラ×カド)` の席替えを帯B（seed 200..599・閾値 +5.0pt）で追試
    dotnet run --project BattleSim -c Release 0 escale [モード]  # 敵の難易度のつまみ（第187期・**S1 = 115/115 を採用**）。本体は `Modes/EnemyScale.cs` / `EnemyScale.Run.cs`
    dotnet run --project BattleSim -c Release 0 escale phase0    # `docs/balance.md` を行名で読む数え物（セルの分布・転生した駒の在席・敵の駒と倍率後の攻撃力。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 escale run [採用前のbalance.md]  # 7 版（S0〜S4・H3・A3）× `compare` 61 行 ＋ 診断台4つ（33 秒）。**S0 は採用前の表を渡して回帰を取る**
    dotnet run --project BattleSim -c Release 0 escale fine      # 参考の 5% 刻み 100〜130（**採否には使わない**）
    dotnet run --project BattleSim -c Release 0 kata [モード]     # 触媒のカタ（第188期・起爆）。本体は `Modes/Kata.cs`
    dotnet run --project BattleSim -c Release 0 kata phase0      # Q0-1（ナタの行・軸の駒の在席・毒と燃焼の書き手の同席。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 kata run         # 診断台3つ × 素体／カタ／逆流なし ＋ 起爆の帳簿 ＋ 味方の死・層の最大・残存・全滅勝ち（5 秒）
    dotnet run --project BattleSim -c Release 0 kata rows        # 表E（事後）: compare の毒・燃焼の 27 行で書き手以外の1枚をカタに替える
    dotnet run --project BattleSim -c Release 0 kata check [採用前のbalance.md]  # 自己検査（(a)〜(f)。**305 セル 0 件**・逆流なしは味方に 0・乱数なし）
                                                                 # **第214期から旧カタは `UnitCatalog.KataOld`**（`kata` / `beni` / `mio` / `debuff` の診断はそちらを引く）
    dotnet run --project BattleSim -c Release 0 shock [モード]    # 感電と雷のカタ（第214期）。本体は `Modes/Shock.cs` / `Shock.Run.cs` / `Shock.Check.cs`
    dotnet run --project BattleSim -c Release 0 shock phase0     # Q0-2〜Q0-7（敵の隣接・数える状態異常 16 本・撃破の読み手・台の枠の選定）
    dotnet run --project BattleSim -c Release 0 shock run        # 表A〜E ＋ 表E'（4台 × X字／P2 × K1/K2/K1−/K0・席は K1 で 120 通り総当たり・27 秒）
    dotnet run --project BattleSim -c Release 0 shock check      # 自己検査（verbose の有無・同じ駒の二重放電・雷で起爆しない・破片で起爆しない・K1 の刻みで起爆しない ほか）
    dotnet run --project BattleSim -c Release 0 shockdigest k0|aka  # 台本の指紋（旧カタの台／ススの台）。**第213期の worktree にも同じファイルを置いて回して突き合わせる**
    dotnet run --project BattleSim -c Release 0 shock run215     # 第215期: 雷の跳ね先 T0 / T1 / T1′（表A・B・C・E'・E''。台と席は第214期のまま・20 秒）
    dotnet run --project BattleSim -c Release 0 shock check215   # 第215期の自己検査（`ThunderTrait.Pick` を盤の外で組んだ駒で直に呼ぶ ほか）
    dotnet run --project BattleSim -c Release 0 shock phase216   # 第216期 Q0-1〜Q0-5（毒の寿命・開戦の順・痺れの付き方・台の現状。本体は `Modes/Shock.P216.cs`）
    dotnet run --project BattleSim -c Release 0 shock run216     # 第216期 表A〜F（台A・B・C × O0〜O4 ／ S0〜S3 × 倍率 115・150・15 秒。本体は `Modes/Shock.R216.cs`）
    dotnet run --project BattleSim -c Release 0 shock run216 g   # 第216期 表G（ベニ・ミオ・カタ ＋ 2枠の 15 通り × 陣形2 × 現行／候補 × 倍率2・35 秒）
    dotnet run --project BattleSim -c Release 0 shock check216   # 第216期の自己検査（verbose・撒きは1戦に1回・倒れた駒に痺れなし・S1 は1体・台本の並び）
    dotnet run --project BattleSim -c Release 0 shockdigest t216 # 第216期の台の台本の指紋（O0・S0 が第215期と一致すること）
    dotnet run --project BattleSim -c Release 0 whip [モード]     # 電気鞭のシガ（第217期・版 G0〜G3H ＋ 参考 G3K）。本体は `Modes/Whip.cs` / `Whip.Run.cs` / `Whip.Check.cs`
    dotnet run --project BattleSim -c Release 0 whip phase0      # Q0-1〜Q0-6（見せしめの優先の段・追い打ちと2倍の違い・電気鞭の判定の時点・G0 の席で追い打ちの内訳）
    dotnet run --project BattleSim -c Release 0 whip run         # 表A〜E（W1〜W3 × 席115（指示書）／席150（参考）× 版 × 倍率 115・150 ＋ W4 ＝ compare のシガの2行・18 秒）
    dotnet run --project BattleSim -c Release 0 whip check       # 自己検査（verbose・悲鳴は1手番に1回・巻き込みで怖気づかない・電気鞭の後に感電 0・放電しない・G3H・台本）
    dotnet run --project BattleSim -c Release 0 whip log "W1 X字 G3 2 0 150"  # 1戦のログ（台 陣形 版 波（0始まり） seed 倍率）
    dotnet run --project BattleSim -c Release 0 shockdigest w217 # 第217期の台の台本の指紋（G0＝今のシガが実装の前後で一致すること）
    dotnet run --project BattleSim -c Release 0 mire [モード]     # 澱みのミオの版（第218期・M1〜M5）。本体は `Modes/Mire.cs` / `Mire.Run.cs` / `Mire.Check.cs`
    dotnet run --project BattleSim -c Release 0 mire phase0      # Q0-1〜Q0-6（感電がミオの番まで残るか・ミオより遅い敵・与ダメの窓口・失速の実測・M0 の仮の席）
    dotnet run --project BattleSim -c Release 0 mire run         # 表A〜F（M台1〜M台4 × X字／P2 ＋ ポンの席 ＋ M台5 × 版7 × 倍率 115・150・16 秒）。席は M4 × 倍率 150 で総当たり
    dotnet run --project BattleSim -c Release 0 mire check       # 自己検査（verbose・叩きつけは1手番に1回・通電の条件と宛先・味方の与ダメ・上限・運び・移り・台本 ＝ 帳簿）
    dotnet run --project BattleSim -c Release 0 mire log "M台2 X字|M2|1|3|150"  # 1戦のログ（台名の部分一致|版|波（0始まり）|seed|倍率）
    dotnet run --project BattleSim -c Release 0 shockdigest m218 # 第218期の台の台本の指紋（M0＝第194期のミオが実装の前後で一致すること・第219期から `MireDiag.VerOf("M0")` を引く）
    dotnet run --project BattleSim -c Release 0 shockdigest m219 cat|m5  # 第219期: 規定のミオ（cat）と第218期の診断の版 M5（m5・第218期の worktree で回す）の台本が一致すること（第220期の追記で規定に爆発が入ったので、今は一致しない）
    dotnet run --project BattleSim -c Release 0 scorch [モード]  # 燃焼の脆さ（第219期・F0〜F4）。本体は `Modes/Scorch.cs` / `Scorch.Run.cs` / `Scorch.Check.cs`
    dotnet run --project BattleSim -c Release 0 scorch phase0    # Q0-1〜Q0-6（被ダメの口・反転の口・燃焼の在り方・燃焼を読む札・台と天井）
    dotnet run --project BattleSim -c Release 0 scorch run [a|b|p5|yoke]  # 表A（compare × F0〜F4）・表B〜E（台）・P5（第二波の1ターン目）・yoke（第四波の読み）・26 秒
    dotnet run --project BattleSim -c Release 0 scorch check     # 自己検査（盤面を直に組んで1発ずつ・燃焼の付かない戦・verbose・台本の欄）
    dotnet run --project BattleSim -c Release 0 burst [モード]   # 澱みが爆ぜる（第220期・B0〜B2x）。本体は `Modes/Burst.cs` / `Burst.Run.cs` / `Burst.Check.cs`
    dotnet run --project BattleSim -c Release 0 burst phase0     # Q0-1〜Q0-6（倒れた瞬間の毒・印・隣・ラウ・仮の席と天井）
    dotnet run --project BattleSim -c Release 0 burst run        # `compare` 61 行 × 版 ＋ 表A〜F（台 R1〜R5 × X字 / P2 × 倍率 115・150・200・24 秒）
    dotnet run --project BattleSim -c Release 0 burst check      # 自己検査（盤面を直に組んで・印の駒が倒れない戦・verbose・台本の爆発）
    dotnet run --project BattleSim -c Release 0 nine [モード]     # 9体の検証波（第221期・敵専用の9枠）。本体は `Modes/Nine.cs` / `Nine.Run.cs` / `Nine.Check.cs`
    dotnet run --project BattleSim -c Release 0 nine phase0      # 検証の波・9マスの表・行の群（**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 nine run [a|b]   # 表A・C・D（61 行 × 検証の3波 ＋ 第一波）・表B（雷の台 × 倍率 115・150）・12 秒
    dotnet run --project BattleSim -c Release 0 nine check       # 自己検査（新しい入口が5体の波で既存と一致・プレイヤーから届かない・9席・前列の規則・貫きが 7／8 に当たらない・完走）
    dotnet run --project BattleSim -c Release 0 drift [モード]    # 移動軸の加速（第222期・シオに手番／ヨミに薙ぎ・版 V0〜V3b）。本体は `Modes/Drift.cs` / `Drift.P0.cs` / `Drift.Run.cs` / `Drift.Check.cs`
    dotnet run --project BattleSim -c Release 0 drift phase0     # Q0-1 移動の供給（V0）／ Q0-2 後ろ側の隣の表 ／ Q0-3 移動を読む札 ／ Q0-5 台
    dotnet run --project BattleSim -c Release 0 drift run        # 表A〜E（台 D1〜D4 × 本編の第2〜5波 ＋ 九/新兵・九/農兵 × 版 × 敵 115/115・150/115・約 6 秒）。D4 は V3 × 九の波 × 150/115 × seed 1000..1049 の 120 通りで選ぶ
    dotnet run --project BattleSim -c Release 0 drift check      # 自己検査（盤面を直に組んで1手番・閾値の前後・30,000 戦で verbose 非依存と台本の並び）
    dotnet run --project BattleSim -c Release 0 sero [モード]     # 逃げ上手のセロ（第223期・版 E0〜E2 ＋ 参考 E1−）。本体は `Modes/Sero.cs` / `Sero.P0.cs` / `Sero.Run.cs` / `Sero.Check.cs`
    dotnet run --project BattleSim -c Release 0 sero phase0      # Q0-1〜Q0-6（E0 だけで回る・移動の供給と回避の機会・隣の数・S2 の5枚目の規則）
    dotnet run --project BattleSim -c Release 0 sero run         # 表A〜F（S1 ポン／選・S2・S3・S3 中央（参考）・S4 compare の12行 × 5波 × 115/115・150/115 × 版4・12 秒）
    dotnet run --project BattleSim -c Release 0 sero check       # 自己検査（盤面を直に組んで・15,000 戦で verbose 非依存と台本の並び・34 項目）
    dotnet run --project BattleSim -c Release 0 shockdigest e223 # 第223期の台（E0＝今のセロの台本が実装の前後で一致すること）
    dotnet run --project BattleSim -c Release 0 seroshio [モード] # セロの火力を移動に寄せる／シオの回復量（第224期・版 F0〜F3 ／ H0〜H2）。本体は `Modes/SeroShio.cs` / `SeroShio.P0.cs` / `SeroShio.Run.cs` / `SeroShio.Check.cs`
    dotnet run --project BattleSim -c Release 0 seroshio phase0  # Q0-1 シオの回復（移り木を動かした駒で割る・溢れ）／ Q0-2 リリ・ツギの compare の行 ／ Q0-3 セロが動かされた回数とターン（5 秒）
    dotnet run --project BattleSim -c Release 0 seroshio run     # 表A〜E（S1 ポン・S2 ネル・S4 compare の12行・K-シオ／リリ／ツギ × 5波 × 115/115・150/115・14 秒）
    dotnet run --project BattleSim -c Release 0 seroshio check   # 自己検査（段・動かされるたびの +2・7 本・移り木の 20%・手当て・verbose・台本・37 項目）
    dotnet run --project BattleSim -c Release 0 retreat seats     # 第225期 前段: compare のセロの行の席（帯A で線 → 帯B で追試・84 秒）。本体は `Modes/Retreat*.cs`
    dotnet run --project BattleSim -c Release 0 retreat phase0    # Q0-1 移動の累計 ／ Q0-2 4割を切る被弾と後ろ側 ／ Q0-4 カドの身代わりと巻き込み（J0・6 秒）
    dotnet run --project BattleSim -c Release 0 retreat run       # 表A〜F（M-ハネ・M-カド・参考ガルド × J0〜J4 × 6 波 × 200/115・150/115・115/115）＋ 移り木を外した J1′・J3′（6 秒）
    dotnet run --project BattleSim -c Release 0 retreat check     # 自己検査（盤面を直に組んで・verbose 非依存 6,000 戦・台本の `Retreat` / `ShioStage`・46 項目）
    dotnet run --project BattleSim -c Release 0 decoy phase0      # 第226期 Q0-1〜Q0-8（K0 だけで回る・介入の順・セロの列・敵の移動の累計と出どころ・止まり続け）。本体は `Modes/Decoy*.cs`
    dotnet run --project BattleSim -c Release 0 decoy run         # 表A〜F（M-ハネ 4 台 × K0〜K4・K3s・K4s × 6 波 × 200/200・200/115・115/115 ＋ 参考の雷・9 秒）。席は版 × 九/新兵 × 200/200 × seed 1000..1099 の総当たり
    dotnet run --project BattleSim -c Release 0 decoy check       # 自己検査（盤面を直に組んで・verbose 非依存 4,320 戦・台本の Decoy / Disarray / DisarrayStage / Squall・37 項目）
    dotnet run --project BattleSim -c Release 0 shockdigest k226 # 第226期の台（K0＝前段の規定の台本が実装の前後で一致すること）
    dotnet run --project BattleSim -c Release 0 lastdodge phase0  # 第227期 Q0-1 一撃死の実数 ／ Q0-2 倒れ方の残り（L0 だけ・3 秒）。本体は `Modes/LastDodge*.cs`
    dotnet run --project BattleSim -c Release 0 lastdodge run     # 表A〜D（M-ハネ 総当たりの1位／セロ前列の1位（版ごと）／225の席 × L0〜L3 × 6 波 × 200/200・200/115・115/115 ＋ 参考の雷・17 秒）
    dotnet run --project BattleSim -c Release 0 lastdodge check   # 自己検査（盤面を直に組んで・逃げ足が発動しない戦は L0 と一致・verbose 非依存・台本の LastDodge・26 項目）
    dotnet run --project BattleSim -c Release 0 shockdigest l227 # 第227期の台（L0＝前段の規定の台本が実装の前後で一致すること）
    dotnet run --project BattleSim -c Release 0 spring phase0     # 第228期 Q0-1 経路の並べ替え ／ Q0-4 ハネの被弾 ／ Q0-5 止まり続け（H0 だけ・1 秒）。本体は `Modes/Spring*.cs`
    dotnet run --project BattleSim -c Release 0 spring run        # 表A〜F（M-ハネ H3 の総当たりの1位／227 L2 の席 ＋ compare のハネの行 × H0〜H3w × 6 波 × 200/200・200/115・115/115 ＋ 参考の雷・6 秒）
    dotnet run --project BattleSim -c Release 0 spring check      # 自己検査（盤面を直に組んで並べ替え・混乱・弾き返しの回数と粛・verbose 非依存・台本の Blast / Spring・33 項目）
    dotnet run --project BattleSim -c Release 0 shockdigest h228 # 第228期の台（H0＝前段の規定の台本が実装の前後で一致すること）
    dotnet run --project BattleSim -c Release 0 gale phase0       # 第229期 Q0-1 転倒の出どころ ／ Q0-2 前列の規則 ／ Q0-3 介入の一覧 ／ Q0-4 追い風の実数 ／ Q0-5 緊急退避との重なり（G0 だけ・6 秒）。本体は `Modes/Gale*.cs`
    dotnet run --project BattleSim -c Release 0 gale run          # 表A〜E（M-ハネ G4 の総当たりの1位／228 H3 の席 × G0〜G4 × 6 波 × 200/200・200/115・115/115 ＋ 参考の雷 ＋ compare 61 行 × G0〜G4・16 秒）
    dotnet run --project BattleSim -c Release 0 gale check        # 自己検査（盤面を直に組んで嵐の段・追い風の条件と4割・転倒の穴の pool と介入・痺れ・verbose 非依存・台本の Tailwind / StaggerBreach・42 項目）
    dotnet run --project BattleSim -c Release 0 shockdigest g229 # 第229期の台（G0＝前段の規定の台本が実装の前後で一致すること）
    dotnet run --project BattleSim -c Release 0 cycle phase0      # 第230期 Q0-1 追い風の踏み込み先と攻撃力順の見込み ／ Q0-2 ヨミの撃破と後ろの敵 ／ Q0-4 移り木・手当ての溢れ（W0 だけ・1 秒）。本体は `Modes/Cycle*.cs`
    dotnet run --project BattleSim -c Release 0 cycle run         # 表A〜F（M-ハネ W4 の総当たりの1位／228 H3 の席 × W0〜W4 × 6 波 × 200/200・200/115・115/115 ＋ 参考の雷 ＋ compare 61 行 × W0〜W4・17 秒）
    dotnet run --project BattleSim -c Release 0 cycle check       # 自己検査（盤面を直に組んで追い風の攻撃力順・撃破の衝撃の3通りと1ターン2回・溢れの半分と +15・verbose 非依存・札が働かない戦は1つ前の版と台本一致・27 項目）
    dotnet run --project BattleSim -c Release 0 tune phase0       # 第231期 Q0-1 倒れた一撃の直前の HP ／ Q0-2 ハネの位置と隣の被弾 ／ Q0-3 段2 後のセロの移動 ／ Q0-4 挑発の表示（V0 だけ・1 秒）。本体は `Modes/Tune*.cs`
    dotnet run --project BattleSim -c Release 0 tune run          # 表A〜D（228 の席／VX2 × 九/新兵 × 400/300 で選び直した席 × V0〜VX2 × 6 波 × 400/300・200/200・115/115 ＋ 組み合わせの分解 1000 戦 ＋ 参考の雷・約 10 秒）
    dotnet run --project BattleSim -c Release 0 tune check        # 自己検査（盤面を直に組んで退避の線・隣の弾き返し・移動の追撃・挑発の表示・verbose 非依存・台本の SpringGuard / MoveShot・31 項目）
    dotnet run --project BattleSim -c Release 0 spring2 phase0    # 第232期 Q0-1 取り合い ／ Q0-2 隣の弾き返しの弾けなかった内訳（S0・S1・1 秒）。本体は `Modes/Spring2*.cs`
    dotnet run --project BattleSim -c Release 0 spring2 run       # 表A〜C（M-ハネ 228 × S0〜S2 × 6 波 × 200/200・400/300・115/115 ＋ 1000 戦 ＋ compare のハネの行 ＋ 参考の雷・4 秒）
    dotnet run --project BattleSim -c Release 0 spring2 check     # 自己検査（S2 でハネが動かない・上限の共有・verbose 非依存・台本・8 項目）
    dotnet run --project BattleSim -c Release 0 spring2 run243    # 第243期 S2・①〜⑤ の単独・①②③④・①②③④⑤（M-ハネ 228 × 本編の第2〜5波 ＋ 九/新兵 × 200/200・400/300・4 秒）
    dotnet run --project BattleSim -c Release 0 spring2 adopt243  # 第243期の追記 採用前の規定（HaneR2）対 ＋③⑤（同じ台・4 秒）
    dotnet run --project BattleSim -c Release 0 spring2 check243  # 第243期 自己検査（札の無い版は旧と一致・verbose 非依存・札の発火・9 項目）
    dotnet run --project BattleSim -c Release 0 burnaudit phase0  # 第233期 Q0-1 仕様 ／ Q0-3 台の駒・相方の役割（観測と `Traits.cs` の走査で機械的に分ける）・所要の見積もり・参考の台。本体は `Modes/BurnAudit*.cs`
    dotnet run --project BattleSim -c Release 0 burnaudit pick [TSV] # 段1（B3 1,081 組 ＋ B4 46 組 × 席 120 × seed 1000..1039・4.2 分）を TSV に書く（既定は一時フォルダ）
    dotnet run --project BattleSim -c Release 0 burnaudit run [TSV]  # 段2 ＋ 表A〜F（TSV が無ければ段1 を回し直す・段2 は 6 秒）
    dotnet run --project BattleSim -c Release 0 burnaudit check     # 自己検査（①②③ の帳簿を盤面を直に組んで・verbose 非依存・死因の合計 ＝ 倒れた駒・9 項目）
    dotnet run --project BattleSim -c Release 0 burnaudit log <前1,前3,中央,後1,後3> [seed] [波]  # 1戦のログ（駒 Id・200/200）
    dotnet run --project BattleSim -c Release 0 borgguard phase0  # 第234期 Q0-1/Q0-2 被ダメの段の並び（本文の位置から機械で）／ Q0-3 G0 のボルグ ／ Q0-4 台 ／ Q0-5 過去の実験の grep。本体は `Modes/BorgGuard*.cs`
    dotnet run --project BattleSim -c Release 0 borgguard pick [dir] # 段1（G0〜G4 × 1,081 組 × 席 120 × seed 1000..1039・約 22 分）を版ごとの TSV に書く（既定は一時フォルダ・既にあれば飛ばす）
    dotnet run --project BattleSim -c Release 0 borgguard run [dir]  # 表A〜G（T3・T3′＝シオ・ササ抜き・T3-1 × 版 × 6 波 × 倍率3 × seed 0..199。TSV が無ければ段1 を回す）
    dotnet run --project BattleSim -c Release 0 borgguard check      # 自己検査（着火の条件・半減・刻み・焼け残り・verbose 非依存・台本 ＝ 帳簿・乱数なし・17 項目）
    dotnet run --project BattleSim -c Release 0 borgguard log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 borgfront digest [path]  # 第235期 受け入れ 1 の台本の指紋（G0 ／ B・832,574 行・engine を触る前後で cmp）。本体は `Modes/BorgFront*.cs`
    dotnet run --project BattleSim -c Release 0 borgfront phase0     # Q0-1 燃焼ダメージの経路 ／ Q0-2 熾火の免除とベニの反転（実測つき）／ Q0-3 火の回復の経路 ／ Q0-5 台と所要
    dotnet run --project BattleSim -c Release 0 borgfront pick [dir] # 段1（9 版 × T3 1,081 組 ＋ 雷＋ボルグ 44 枚 × 席 120 × seed 40・約 56 分）を版ごとの TSV に（既にあれば飛ばす）
    dotnet run --project BattleSim -c Release 0 borgfront run [dir]  # 段2 ＋ 表A〜R・表H（H1a ／ H1b × 第三波）（146 台 × 6 波 × 倍率3 × seed 0..199・43 秒。TSV が無ければ段1 を回す）
    dotnet run --project BattleSim -c Release 0 borgfront check      # 自己検査（S・O・H1・H1a/b・H2 を盤面を直に組んで・verbose 非依存・死因の分割・台本 ＝ 帳簿・乱数なし・23 項目）
    dotnet run --project BattleSim -c Release 0 borgfront log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]  # 1戦のログ（版は G0 / B / BS / BSO / BSOH1 / ALL / ALL-S / ALL-O / ALL-H1 / H1b）
    dotnet run --project BattleSim -c Release 0 fireward digest [path]  # 第238期 受け入れ 1 の台本の指紋（G0 ／ A・875,550 行・engine を触る前後で cmp）。本体は `Modes/FireWard*.cs`
    dotnet run --project BattleSim -c Release 0 fireward phase0     # Q0-1 半減の置き場所 ／ Q0-2 V の3口の順 ／ Q0-3 過去の実験 ／ Q0-4 仮の席で燃える味方と燃焼ダメージ
    dotnet run --project BattleSim -c Release 0 fireward pick [dir] # 段1（10 版 × T3 1,081 組 × 席 120 × seed 40・並びは 200/200 → 400/300 → 落 → 決着T・107 分）を版ごとの TSV に
    dotnet run --project BattleSim -c Release 0 fireward run [dir]  # 段2 ＋ 表A〜H（118 台 × 6 波 × 倍率3 × seed 0..199・同じ席で版を並べる表つき・40 秒。TSV が無ければ段1 を回す）
    dotnet run --project BattleSim -c Release 0 fireward check      # 自己検査（D・V を盤面を直に組んで・verbose 非依存・死因の役ごとの合計・台本 ＝ 帳簿・乱数なし・45 項目）
    dotnet run --project BattleSim -c Release 0 fireward log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]  # 1戦のログ（版は G0 / A / AD1 / AD2 / AV1 / AV2 / AD1V1 / AD1V2 / AD2V1 / AD2V2）
    dotnet run --project BattleSim -c Release 0 firescale moved <旧 balance.md>  # 第239期 前段: 規定化（ボルグ A+D2・ヒヨ V1）で動いた行の一覧（受け入れ 1）。本体は `Modes/FireScale*.cs`
    dotnet run --project BattleSim -c Release 0 firescale phase0     # Q0-1 燃焼の書き手・読み手（`Traits.cs` / `BattleEngine.cs` の走査）／ Q0-2 着火 ＝ `StatusGain` burn ／ Q0-3 手番の枠 ／ Q0-4 T3-1 の席 ／ Q0-5 材料（4 秒）
    dotnet run --project BattleSim -c Release 0 firescale run        # 表0・表A〜C（影の火勢 K1/K2/K3・手番の絵・放つ／段／大焚きの発火見込み × 台5 × 波6 × 倍率3 × seed 0..199・verbose・5 秒）
    dotnet run --project BattleSim -c Release 0 firescale check      # 自己検査（影の規則を盤の外で・verbose 非依存・枠 ＝ TurnsTaken・着火 ＝ 帳簿・規定 ＝ 第238期の版・18 項目）
    dotnet run --project BattleSim -c Release 0 firescale digest [path]  # 台本の指紋（手番の枠を止める前後で突き合わせる）
    dotnet run --project BattleSim -c Release 0 firescale log <台> [seed] [波] [倍率 0/1/2]  # 1戦のログ（台は T3 / T3-1 / 雷＋ボルグ / 参考 移動 / 参考 雷）
    dotnet run --project BattleSim -c Release 0 firescale feed0      # 第240期 Phase 0（ヒヨの手番の頭で燃えている味方・切った量/戦・閾値ごとの育つ回数の上限・自火）
    dotnet run --project BattleSim -c Release 0 firescale feed       # 段1（72 組 × T3・T3-1 × 九/新兵 × 倍率3）→ 段2（選んだ組 × 全台・全波・倍率）の表A〜G・F'（5 秒）
    dotnet run --project BattleSim -c Release 0 firescale feedcheck  # 自己検査（保つ・育てる・萎む・余熱を盤の外で・影を数えても台本が同じ・切った量 ＝ 帳簿・14 項目）
    dotnet run --project BattleSim -c Release 0 firescale gift0      # 第241期 Phase 0（開幕の着火の出どころ・周回ごとの燃えている敵と燃えている敵に当たった数・ヒヨの手番と行動順・決着T・1 秒）
    dotnet run --project BattleSim -c Release 0 firescale gift       # 段1（96 組 × T3・T3-1 × 九/新兵 × 倍率3）→ 段2（選んだ組 ＋ 参考 F0 × 全台・全波・倍率）の表A〜I・D'（5 秒）
    dotnet run --project BattleSim -c Release 0 firescale giftcheck  # 自己検査（F1・F2・F3・F4 を盤の外で・影を数えても台本が同じ・F1 ＝ 台本から直に読んだ値・ギフトの条件と相手の順・E1・15 項目）
    dotnet run --project BattleSim -c Release 0 firelevel phase0      # 第242期 Q0-7（R0 の固定席の数え物・4 秒）。本体は `Modes/FireLevel*.cs`
    dotnet run --project BattleSim -c Release 0 firelevel pick [dir] [版]  # 段1（版 R0〜R3g4 × T3 1,081 組 × 席 120 × seed 1000..1039・版ごとに 7〜10 分）を版ごとの TSV に（既にあれば飛ばす）
    dotnet run --project BattleSim -c Release 0 firelevel run [dir]   # 段2 ＋ 表A〜H ＋ `compare` 61 行の版ごとの動き（台4 × 版5 × 波6 × 倍率3 × seed 0..199・verbose・約 1 分。TSV が無ければ段1 を回す）
    dotnet run --project BattleSim -c Release 0 firelevel check       # 自己検査（保つ・燃え広がり・段・萎む・ギフトを盤の外で・5,760 戦の台本と帳簿・R0 の compare ＝ balance.md・35 項目）
    dotnet run --project BattleSim -c Release 0 firelevel log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 fireburst phase0      # 第244期 Q0-6（S0 ＝ 規定の駒で §6 の台を数える・2 秒）。本体は `Modes/FireBurst*.cs`
    dotnet run --project BattleSim -c Release 0 fireburst pick [dir] [版]  # 段1（版 S0〜S2R-g4 × T3 1,081 組 × 席 120 × seed 40・版ごとに約 10 分）を版ごとの TSV に（既にあれば飛ばす）
    dotnet run --project BattleSim -c Release 0 fireburst run [dir]   # 段2 ＋ 表A〜J ＋ `compare` 61 行の版ごとの動き（台5 × 版5 × 波6 × 倍率3 × seed 0..199・verbose・23 秒。TSV が無ければ段1 を回す）
    dotnet run --project BattleSim -c Release 0 fireburst check       # 自己検査（上限・比べ方・大技の条件・残り火・呼び火・火の雨 D・乱数を数える `Random` を差し込んで・5,760 戦の台本と帳簿・41 項目）
    dotnet run --project BattleSim -c Release 0 fireburst digest [path]  # S0 の台本の指紋（実装の前のコミットに置いても回る・518,518 行）
    dotnet run --project BattleSim -c Release 0 fireburst log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 enemyfire pre          # 第245期 前段（P- ／ P′ ／ 参考 S2R × T3-238 ／ T3-244・6 秒）。本体は `Modes/EnemyFire*.cs`
    dotnet run --project BattleSim -c Release 0 enemyfire phase0       # Q0-6（E0 ＝ 規定の駒の台本から敵の火勢の影を組み直す・1 秒）
    dotnet run --project BattleSim -c Release 0 enemyfire pick [dir] [版]  # 段1（版 E0〜E2 × T3 1,081 組 × 席 120 × seed 40・版ごとに約 7 分）を版ごとの TSV に（既にあれば飛ばす）
    dotnet run --project BattleSim -c Release 0 enemyfire run [dir]    # 段2 ＋ 表A〜F ＋ `compare` 61 行の版ごとの動き（24 秒。TSV が無ければ段1 を回す）
    dotnet run --project BattleSim -c Release 0 enemyfire check        # 自己検査（育つ・萎む・刻みの回数・脆さ・延焼・味方の刻み・乱数・5,760 戦の台本と帳簿・E0 の compare ＝ balance.md・41 項目）
    dotnet run --project BattleSim -c Release 0 enemyfire digest [path]  # E0 の台本の指紋（実装の前のコミットに置いても回る・541,979 行）
    dotnet run --project BattleSim -c Release 0 enemyfire log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 firecycle phase0       # 第246期 Q0-4（重い波の決着T）・Q0-5（Q0 で台を数える）・的の波（4 秒）。本体は `Modes/FireCycle*.cs`
    dotnet run --project BattleSim -c Release 0 firecycle pick [dir] [版]  # 段1（版 Q0〜Q2-HP・Q1-選 × T3 1,081 組 × 席 120 × seed 40・版ごとに 8〜14 分）を版ごとの TSV に
    dotnet run --project BattleSim -c Release 0 firecycle run [dir]    # 段2 ＋ 表A〜I ＋ 的の表 F-1〜F-5 ＋ `compare` 61 行の版ごとの動き（35 秒）
    dotnet run --project BattleSim -c Release 0 firecycle check        # 自己検査（相手選び・火の粉・放熱・臨界・大火槍・乱数・13,800 戦の台本と帳簿・的の打ち切り・Q0 の compare ＝ balance.md・42 項目）
    dotnet run --project BattleSim -c Release 0 firecycle digest [path]  # Q0 の台本の指紋（このファイルだけで閉じる・前段の規定化だけのコードにも置ける・550,240 行）
    dotnet run --project BattleSim -c Release 0 firecycle log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]  # 1戦のログ（波 6 重い ／ 7 的・一 ／ 8 的・九）
    dotnet run --project BattleSim -c Release 0 firetri phase0         # 第247期 Q0-4（段2 火槍・段3 大火槍の台本）・Q0-5（T0 で台を数える・緩い三角・ボルグの火勢・(b) の機会・2 秒）。本体は `Modes/FireTri*.cs`
    dotnet run --project BattleSim -c Release 0 firetri pick [dir] [版]  # 段1（版 T0〜T1-放粉 × T3 1,081 組 × 席 120 × seed 40・版ごとに約 7.5 分）を版ごとの TSV に（既にあれば飛ばす）
    dotnet run --project BattleSim -c Release 0 firetri run [dir]      # 段2 ＋ 表A〜I ＋ `compare` 61 行の版ごとの動き（TSV が無ければ段1 を回す）
    dotnet run --project BattleSim -c Release 0 firetri check          # 自己検査（印・指名・火の粉・(b)・乱数・16,560 戦の台本と帳簿・T0 の compare ＝ balance.md・60 項目）
    dotnet run --project BattleSim -c Release 0 firetri digest [path]  # T0 の台本の指紋（このファイルだけで閉じる・前段の規定化だけのコードにも置ける・535,503 行）
    dotnet run --project BattleSim -c Release 0 firetri log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 firewrap run           # 第248期 線（§3）＋ 表A〜C（版 U0 ／ U1 ／ U2 × T3-244・T3-238・雷＋ボルグ ＋ 参考2 × 波9 × 倍率3 × seed 0..199・verbose・9 秒）。本体は `Modes/FireWrap*.cs`
    dotnet run --project BattleSim -c Release 0 firewrap check         # 自己検査（指名は火勢4 のときだけ・未満なら印は残る・指名で消える・規定の札・U2 の戦闘で火勢4 未満の指名 0・verbose 非依存・55 項目）
    dotnet run --project BattleSim -c Release 0 firewrap log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 firefinish phase0         # 第249期 Q0-6（K0 ＝ 規定で台を数える・11 秒）。本体は `Modes/FireFinish*.cs`
    dotnet run --project BattleSim -c Release 0 firefinish pick [dir] [版]  # T3 の段1（K0 と K4 だけ・1,081 組 × 席 120 × seed 40・版ごとに約 8 分）
    dotnet run --project BattleSim -c Release 0 firefinish run [dir]      # 段2 ＋ 表A〜H（台 5 × 版 K0〜K4t × 波 9 × 倍率 3 × seed 0..199・verbose・24 秒）
    dotnet run --project BattleSim -c Release 0 firefinish check          # 自己検査（火の癒し・贔屓・火勢・爆炎・連撃・刻み一撃を盤面を直に組んで・15,120 戦の台本と帳簿・42 項目）
    dotnet run --project BattleSim -c Release 0 firefinish digest [path]  # K0 の台本の指紋（このファイルだけで閉じる・540,620 行）
    dotnet run --project BattleSim -c Release 0 firefinish log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 fireatk phase0         # 第250期 Q0-1〜Q0-4（L0 で台を数える・札が無くても数える機会・7 秒）。本体は `Modes/FireAtk*.cs`
    dotnet run --project BattleSim -c Release 0 fireatk pick [dir] [版]  # T3 の段1（L0 と L3 だけ・1,081 組 × 席 120 × seed 40・版ごとに約 7 分）
    dotnet run --project BattleSim -c Release 0 fireatk run [dir]      # 段2 ＋ 表A〜G（台 5 × 版 L0〜L3 × 波 9 × 倍率 3 × seed 0..199・verbose・18 秒）
    dotnet run --project BattleSim -c Release 0 fireatk check          # 自己検査（あぶれた火・くべられる火・焼き尽くす ×7・爆炎・独りを盤面を直に組んで・12,960 戦の台本と帳簿・ヒヨが落ちなかった戦は L3 ＝ L2・42 項目）
    dotnet run --project BattleSim -c Release 0 fireatk digest [path]  # L0 の台本の指紋（このファイルだけで閉じる・463,636 行）
    dotnet run --project BattleSim -c Release 0 fireatk log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 firekindle phase0       # 第252期 Q0-1〜Q0-5（M0 ＝ 規定で台を数える・ボルグが切った被ダメ・火勢4 で来た育ち・5 秒）。本体は `Modes/FireKindle*.cs`
    dotnet run --project BattleSim -c Release 0 firekindle pick [dir] [版]  # T3 の段1（M0 と M1 だけ・1,081 組 × 席 120 × seed 40・版ごとに約 7 分）
    dotnet run --project BattleSim -c Release 0 firekindle run [dir]    # 段2 ＋ 表A〜H（台 5 × 版 M0〜M2 × 波 9 × 倍率 3 × seed 0..199・verbose・20 秒）
    dotnet run --project BattleSim -c Release 0 firekindle check        # 自己検査（札7枚を盤面を直に組んで・11,340 戦の台本と帳簿・B1 の残り・溜め火の倍率・38 項目）
    dotnet run --project BattleSim -c Release 0 firekindle digest [path]  # M0 の台本の指紋（このファイルだけで閉じる・実装の前のコミットにも置ける・430,117 行）
    dotnet run --project BattleSim -c Release 0 firekindle log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 giftorder phase0       # 第253期 Q0（N0 ／ N1 ／ ref で2体のギフトの並び・爆炎の後の焼き尽くす・線の数字・12 秒）。本体は `Modes/GiftOrder*.cs`（表は `firekindle` の器具を呼ぶ）
    dotnet run --project BattleSim -c Release 0 giftorder run          # 線（§4）＋ 表A〜G（台 3 ＋ 参考 2 × 版 N0〜N2・ref × 波 9 × 倍率 3 × seed 0..199・verbose・16 秒）
    dotnet run --project BattleSim -c Release 0 giftorder check        # 自己検査（盤面を直に組んで QueueGift の並び・1体と2体・渡す火の上げ・乱数 ／ 2,520 戦で N1 と台本一致・ボルグ先・死因の合計・21 項目）
    dotnet run --project BattleSim -c Release 0 giftorder digest <版> [path]  # 版の台本の指紋（`firekindle digest` と同じ形式・N0 は第251期の規定と 430,117 行で一致）
    dotnet run --project BattleSim -c Release 0 giftorder log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 blazesurge phase0       # 第254期 Q0-1〜Q0-3（V0 で爆炎の瞬間の火勢・ギフトの中の順・台を数える・4 秒）。本体は `Modes/BlazeSurge*.cs`（表A と第252期の表は `firekindle` の器具を呼ぶ）
    dotnet run --project BattleSim -c Release 0 blazesurge run          # 表A〜H（台 3 ＋ 参考 2 × 版 V0 ／ V2 ／ V4 × 波 9 × 倍率 3 × seed 0..199・verbose・1戦を1回だけ回して5つの集計に流す・8 秒）
    dotnet run --project BattleSim -c Release 0 blazesurge check        # 自己検査（盤面を直に組んで上げ2 ／ 上げ満・あぶれた火・対象外・乱数 ／ 3,780 戦で対象・量・独り・verbose・死因の合計・29 項目）
    dotnet run --project BattleSim -c Release 0 blazesurge digest <版> [path]  # 版の台本の指紋（`giftorder digest` と同じ形式・V0 は第253期の規定と 438,164 行で一致）
    dotnet run --project BattleSim -c Release 0 blazesurge log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]  # 1戦のログ
    dotnet run --project BattleSim -c Release 0 burnhit phase0|pick|run|check   # 第255期 被弾の燃焼の版（H0 ／ 足す ／ 分担 ／ 分担1 ／ 敵だけ）。**第256期から `EmberRule.Pre256` に固定**（版は札で足す）。本体は `Modes/BurnHit*.cs`
    dotnet run --project BattleSim -c Release 0 burnadopt check     # 第256期 自己検査（規定 ＝ 第255期の H-分担・セロの状態の矢が外れている・12 項目）。本体は `Modes/BurnAdopt*.cs`
    dotnet run --project BattleSim -c Release 0 burnadopt compare   # `compare` 61 行の動いたセルを原因（燃焼の規則 ／ セロ）で分ける（13 秒）
    dotnet run --project BattleSim -c Release 0 burnadopt doha      # T3-238 でドハが落ちやすい理由（H0 ／ H-分担 ／ H-敵だけ・害の帳簿・中継の機会）
    dotnet run --project BattleSim -c Release 0 burnadopt log <版> [seed] [波 0〜3] [倍率 0/1/2]   # T3-238 の1戦（版は H0 ／ H-分担 ／ H-敵だけ）
    dotnet run --project BattleSim -c Release 0 foesurge phase0      # 第257期 Q0-2・Q0-3（W0 ＝ 規定で台を数える・爆炎の後に残る敵・8 秒）。本体は `Modes/FoeSurge*.cs`（第252・254期の表を呼ぶ・燃焼の規則は規定＝`Pre256` に固定しない）
    dotnet run --project BattleSim -c Release 0 foesurge run         # 表A〜G・D′（台 5 ＋ 参考 2 × 版 W0 ／ W2 ／ W4 ／ ref × 波 9 × 倍率 3 × seed 0..199・verbose・18 秒）
    dotnet run --project BattleSim -c Release 0 foesurge check       # 自己検査（渡す量の振り分け・敵上げの量と見出しを盤面を直に組んで・6,300 戦で敵だけ・味方は前 ＝ 後・独りでも・爆炎の出ない戦は W0 と一致・死因の合計・27 項目）
    dotnet run --project BattleSim -c Release 0 foesurge digest <版> [path]  # 版の台本の指紋（W0 は実装の前と 834,087 行で一致）
    dotnet run --project BattleSim -c Release 0 foesurge log <版> <台> [seed] [波 0〜8] [倍率 0/1/2]   # 1戦のログ（台は T3-244 ／ T3-238 ／ T3-255 ／ 混ぜ-255 ／ 雷＋ボルグ）
    dotnet run --project BattleSim -c Release 0 checkwave phase0     # 第260期 Phase 0（刻みと回復の順・第四波の写しの一致・軛と回復・ボスの攻撃力・机上計算）＋ 段1 の棚卸し（6台 × 8波 × seed 0..199・窓の与ダメと命中・5 秒）。本体は `Modes/CheckWave*.cs`
    dotnet run --project BattleSim -c Release 0 checkwave run        # 第264期 規定の組（ボス B3-桁 ／ 手数 W3-割合）× 7台・連続量が主で勝率は副（まとめは design/CHECK_WAVE_SUMMARY.md・5 秒）
    dotnet run --project BattleSim -c Release 0 checkwave poison     # 第264期 7台目（毒台）の選定（compare の毒の行 × 素の 400/300 第四波・ほかの軸の核を含まない行に絞る）
    dotnet run --project BattleSim -c Release 0 checkwave run263     # 第263期 桁合わせ版（B3-桁 ／ W3-割合 ＋ 対照・6台・第263期の run と全行一致）
    dotnet run --project BattleSim -c Release 0 checkwave ranks263   # 第263期の ranks（6台・第263期の出力と全行一致）
    dotnet run --project BattleSim -c Release 0 checkwave ranks      # 第264期 第260〜263期の全版の連続量（倒しT・回復を上回った窓・窓あたり出力・癒し手を割ったT・純実入り）の順位と、ボス × 手数の順位の逆転（7台・5 秒）
    dotnet run --project BattleSim -c Release 0 checkwave run262     # 第262期 段2・段3（400/300 の手数チェック W2-* ＋ 全体攻撃の動じないボス B2-*・棄却・対照・第262期の run と全行一致）
    dotnet run --project BattleSim -c Release 0 checkwave run261     # 第261期 段2・段3（重装兵の体の癒し手 W-* ＋ 単体の動じないボス B-*・棄却・対照・第261期の run と全行一致）
    dotnet run --project BattleSim -c Release 0 checkwave run260     # 第260期の段2・段3（司祭の体の癒し手 T-* ＋ 動じない無しのボス・HP 500 の参考）と採否の表 D-1〜D-4（棄却・対照・3 秒）
    dotnet run --project BattleSim -c Release 0 bosswave run265      # 第265期 本編ボス波「勇者パーティー」2版（H-制御税 ／ H-回復税）＋ 対照（B3-桁 ／ B3-速14）× 7台・倒しT・割った順・取り巻きを割ったT・勇者の手番・純実入り・採否（棄却・対照・1 秒）。本体は `Modes/BossWave.cs`
    dotnet run --project BattleSim -c Release 0 bosswave body        # 第265期 参考: 取り巻きの HP だけを ×1 ／ ×2 ／ ×4 ／ ×8 にしたときの割った順（採否には使わない・2 秒）
    dotnet run --project BattleSim -c Release 0 bosswave check265    # 第265期 自己検査（16 項目: 取り巻き ＝ 重装兵 ＋ 札1枚・2版は席だけ・勇者の印の条件・癒し手は勇者だけ・保持者 0・乱数なし・決定性）
    dotnet run --project BattleSim -c Release 0 bosswave indom0      # 第266期 Phase 0: 不屈 N=0（素通し）で各台が手番を奪う回数・振れた手番（完全無効 D-40 と並べる）・N=11 ／ 22 の積みの見積もり（1 秒）。本体は `Modes/BossWave.Indom.cs`
    dotnet run --project BattleSim -c Release 0 bosswave indom       # 第266期 勇者1体・不屈 N = 0 ／ 11 ／ 22 ＋ 対照（D-40 ／ B3-速14）× 7台・倒しT・勝率・手番を奪った回数・不屈と天井の積み・採否（棄却・対照・1 秒）
    dotnet run --project BattleSim -c Release 0 bosswave check266    # 第266期 自己検査（15 項目: 5 種が通る・上がったときだけ数える・手番を奪わない状態は数えない・天井と別の箱・F-0 は札なしと台本一致）
    dotnet run --project BattleSim -c Release 0 bosswave guard0      # 第267期 Phase 0: 癒し手の席（前1 ／ 後1 ／ ○後2）× 庇い 75 ／ 100 × 7台・庇った ／ 漏れた単体・割ったT と手段（5 秒）。本体は `Modes/BossWave.Guard.cs`
    dotnet run --project BattleSim -c Release 0 bosswave guard       # 第267期 勇者が癒し手を庇う（前1・75 ／ 100）＋ D-40 × 7台・勝率・倒しT・割ったT と手段（単体 ／ 範囲 ／ 刻み ／ その他）・採否（採らない・対照・1 秒）
    dotnet run --project BattleSim -c Release 0 bosswave guardbody   # 第267期 参考: 癒し手の HP だけを ×1 ／ ×2 ／ ×4 ／ ×8 にした G-庇75（採否には使わない・3 秒）
    dotnet run --project BattleSim -c Release 0 bosswave check267    # 第267期 自己検査（9 項目: 庇い 100 ／ 75 の割合・癒し手がいなければ差し替えない・薙ぎは差し替えない・刻みは癒し手に入る・保持者 0・決定性）
    dotnet run --project BattleSim -c Release 0 bosswave venom0      # 第268期 Phase 0: D-40 で勇者に積まれる毒の層（回復が入るときの値・T1〜T15）× 7台・毒の書き手の棚卸し（1 秒）。本体は `Modes/BossWave.Venom.cs`
    dotnet run --project BattleSim -c Release 0 bosswave venom       # 第268期 蝕み N=18 ／ 37 ＋ V-0（D-40）× 7台・勝率・倒しT・蝕みで減った回復・層・採否（1 秒）
    dotnet run --project BattleSim -c Release 0 bosswave check268    # 第268期 自己検査（8 項目: 層 × N を引く・層は消費しない・下限 0・札なしは 1,200・V-0 ＝ D-40・保持者 0・決定性）
    dotnet run --project BattleSim -c Release 0 bosswave run         # 第269期 ボスの規定形（`EnemyCatalog.BossRegular`・蝕み N=37）＋ 版 V-18 ／ V-0 × 7台・正式な指標を見出しに（1 秒）。本体は `Modes/BossWave.Adopt.cs`・まとめは design/BOSS_SUMMARY.md
    dotnet run --project BattleSim -c Release 0 bosswave compare     # 第269期 ボス別表: `compare` の全行 × 規定形（勝率・倒しT・崩れ始め・層・蝕みで減った回復・毒の書き手の枚数・`compare` 本体には列を足さない・2 秒）
    dotnet run --project BattleSim -c Release 0 bosswave knobs       # 第269期 参考: 規定形からノブを1つ緩めた3版（HP 1,500 ／ 天井 +4 ／ 回復なし）× `compare` の全行（集計だけ・6 秒）
    dotnet run --project BattleSim -c Release 0 bosswave check       # 第269期 自己検査を全部（check265 ／ check266 ／ check267 ／ check268 ／ check269・53 項目）
    dotnet run --project BattleSim -c Release 0 bosswave check269    # 第269期 自己検査（5 項目: 規定形の定義・第268期の V-封 と台本一致・`Stages` に載っていない・保持者 0・書き手5体）
    dotnet run --project BattleSim -c Release 0 relic p0             # 第270期 レリックの一覧・素の保持者・ゴミの読み手の在籍・7台の付け先（戦闘0回）。本体は `Modes/Relic.cs`・札は `BattleCore/Relics.cs`
    dotnet run --project BattleSim -c Release 0 relic check          # 第270期 器の自己検査（10 項目: 付けない編成は素の札と同一・付けて外すと台本一致・レリック以外は例外・1枠1枚・重複は積まない・写し・保持者 0・読み手・身を固めるの1度きり・7枚とも発火）
    dotnet run --project BattleSim -c Release 0 relic sweep          # 第270期 気配の確認: ボスの規定形 × 7台 × 札7枚 × 付け先の規則2つ（攻 ／ 前）・倒しT と勝率の差・発火・付けた駒の与ダメ（5 秒）
    dotnet run --project BattleSim -c Release 0 relic keep           # 第270期 個性の保存: 移動の台のヨミに軋む足 ／ 身を固める（ヨミの軋みの回数と与ダメ・1 秒）
    dotnet run --project BattleSim -c Release 0 relic p0j            # 第271期 Phase 0: 総当たりの規模・61 行の軸タグ（混成 ＝ 2軸以上の核）・ウツ ／ ガンを含む行（戦闘0回）。本体は `Modes/Relic.Grid.cs`
    dotnet run --project BattleSim -c Release 0 relic grid [帯の頭]  # 第271期（第272期に帯の頭を引数にした・既定 0） 本判定: 61 行 × 札7枚 × 枠5（部隊に1枚）× ボス ＋ 本編第2〜5波・固有の勝者（帯B で追試）・個性の保存・混成率・ゴミの成立・既存波の桁・素の本編 244 セルの回帰（110 秒）
    dotnet run --project BattleSim -c Release 0 relic junk           # 第271期 ゴミの成立だけ（ウツ × 萎える心 ／ ガン × 痺れる足・対照は読み手のいない行・総当たりを回すので 110 秒）
    dotnet run --project BattleSim -c Release 0 relic mainwin        # 第271期 参考（採否には使わない）: 固有の勝者の手続きを本編第2〜5波に当てる ＋ 個性の保存（111 秒）
    dotnet run --project BattleSim -c Release 0 relic p0r            # 第272期 Phase 0: 帯A（seed 200..399）で素が負けるセル（本編第2〜5波 × 61 行）の数と行（7 秒）。本体は `Modes/Relic.Rejudge.cs`
    dotnet run --project BattleSim -c Release 0 relic rejudge        # 第272期 再判定: 版 G0 ／ G1（火付けの矢 N=3）／ G2（N=5）× 61 行 × 札7枚 × 枠5 × 本編第2〜5波・固有の勝者（帯B 400..599 で追試）・混成の成立・個性の保存・ゴミの再現・弾数制の効き・ボスの付録（310 秒）
    dotnet run --project BattleSim -c Release 0 relic check272       # 第272期 弾数制の自己検査（6 項目: 版の札は `All` の外・保持者 0・3発を超えない・3発以内の戦は G0 と台本一致・4発以上の戦がある・境界で弾数が戻る）
    dotnet run --project BattleSim -c Release 0 relic p0x            # 第273期 Phase 0: 新札5枚の読み手の在籍（`compare` 61 行・戦闘0回）。本体は `Modes/Relic.Expand.cs`・物差しは design/RELIC_MEASURES.md
    dotnet run --project BattleSim -c Release 0 relic check273       # 第273期 新札5枚の自己検査（14 項目: 一覧の並び・保持者 0・発火・発火しない戦は素と台本一致・毒を招くの2倍・カタの検証台で帯電の足が発火。帯電の足の `compare` 行での発火 0 は × のまま記録）
    dotnet run --project BattleSim -c Release 0 relic expand         # 第273期 本判定: 61 行 × 札12枚 × 枠5 × 本編第2〜5波・帯A 600..799 ／ 帯B 800..999・固有の勝者・橋の内訳・ゴミ（ベニ ／ ヴィオ ／ ウツ）・個性の保存（札種別）・符号反転（175 秒）
    dotnet run --project BattleSim -c Release 0 relic logrow <行名> <札> <枠0-4> <波1-4> [seed]   # 第273期 `compare` の行に札を付けた1戦のログ
    dotnet run --project BattleSim -c Release 0 relic shock276       # 第276期 帯電の足の再測: 62 行 × 帯電の足 × 枠5 × 本編第2〜5波・帯A 1000..1199 ／ 帯B 1200..1399・感電の行の全枠の発火（20 秒）
    dotnet run --project BattleSim -c Release 0 relic grid smoke     # 第279期 煙試験（sweep が回す形）: `relic grid` を seed 20（本体 200・帯B の追試も 20）で。数字は測定に使わない——完走して同じ指紋を返すかだけ
    dotnet run --project BattleSim -c Release 0 relic junk smoke     # 第279期 煙試験: `relic junk` を seed 20 で
    dotnet run --project BattleSim -c Release 0 relic mainwin smoke  # 第279期 煙試験: `relic mainwin` を seed 20 で
    dotnet run --project BattleSim -c Release 0 relic rejudge smoke  # 第279期 煙試験: `relic rejudge` を seed 20（帯A ／ 帯B とも）で
    dotnet run --project BattleSim -c Release 0 relic expand smoke   # 第279期 煙試験: `relic expand` を seed 20（帯A ／ 帯B とも）で
    dotnet run --project BattleSim -c Release 0 som276 run           # 第276期 ソムの転生: 検証行 A ／ B × 版 S0 ／ S1 ／ S1x ／ S1p ／ S2 ／ S2′ × 本編第2〜5波・ボス規定形・チェック波（B3-桁 ／ W3-割合）・機構の実測（5 秒）
    dotnet run --project BattleSim -c Release 0 som276 digest        # 第276期 旧ソム（`SomS0`）を含む戦の台本の指紋（背かれの切り出し・規定化の前後で一致が門）
    dotnet run --project BattleSim -c Release 0 som276 bands         # 第276期 感電の行の第2〜5波を 200 seed の帯ごとに（参考）
    dotnet run --project BattleSim -c Release 0 som276 log <A|B> <S0|S1|S1x|S1p|S2|S2′> <2..5|ボス|B3|W3> [seed]   # 第276期 1戦のログ
    dotnet run --project BattleSim -c Release 0 nomi277 run          # 第277期 ノミの転生: ノミ在席の `compare` 6 行・交差帯 2 行・燃焼の検証行 × 版 N0 ／ N1 ／ N2 × 本編第2〜5波・ボス規定形・チェック波（B3 ／ W3）・機構の実測（5 秒）
    dotnet run --project BattleSim -c Release 0 nomi277 bandb        # 第277期 帯B（seed 200..599）の追試: 9 台 × 版 × 本編第2〜5波の勝率
    dotnet run --project BattleSim -c Release 0 nomi277 seat         # 第277期 燃焼×ノミの行の席: ボルグ入りの `compare` 行の火の駒以外の全枠に N0 ／ N1 を差して本編第2〜5波（情報セル・被弾の燃焼・25 秒）
    dotnet run --project BattleSim -c Release 0 nomi277 check        # 第277期 自己検査（保持者 0・N0 の写しの台本一致・弾数・再行動の上限・刻みの回数・決定性・verbose・9 項目）
    dotnet run --project BattleSim -c Release 0 nomi277 log <行 0..8> <N0|N1|N2> <2..5|ボス|B3|W3> [seed]   # 第277期 1戦のログ
    dotnet run --project BattleSim -c Release 0 tou279 run        # 第279期 トウの転生: トウ在席の `compare` 2 行・感電の行の2形（E1 後3 ミオ → トウ ／ E2 中央 ソム → トウ）・移動の台2つ（M1 ／ M2）・感電の行そのまま × 版 T0 ／ T1 ／ T2 × 本編第2〜5波・ボス・B3 ／ W3 × seed 0..199。本体は `Modes/Tou279.cs`
    dotnet run --project BattleSim -c Release 0 tou279 bandb      # 第279期 帯B（seed 200..599）の追試: 7 台 × 版 × 本編第2〜5波の勝率
    dotnet run --project BattleSim -c Release 0 tou279 relic      # 第279期 帯電の足の再測: 移動の台（M0 ／ M1 ／ M2）・感電の行の2形・責め苦 × 版 × 札の枠5 × 本編第2〜5波・帯A 1400..1599 ／ 帯B 1600..1799（固有の勝者と発火）
    dotnet run --project BattleSim -c Release 0 tou279 check      # 第279期 自己検査（T0 の写しが規定と台本一致・粉の付与と漏れ・旧の痺れ粉・カタのいない台でも感電で痺れる・決定性・verbose）
    dotnet run --project BattleSim -c Release 0 tou279 log <台 0..6> <版 T0|T1|T2> <波 2..5|ボス|B3|W3> [seed]   # 第279期 1戦のログ
    dotnet run --project BattleSim -c Release 0 relic log <台> <札> <規則> [seed]   # 第270期 1戦のログ（札は日本語名・規則は 攻 ／ 前）
    dotnet run --project BattleSim -c Release 0 checkwave check      # 自己検査（写しの台本一致・癒し手 ＝ 従軍司祭＋札1枚・保持者 0・回復は状態を消さない・刻みの後・軛に切られない・ボスの攻撃力・verbose・乱数・動じない札・重装兵の体の癒し手・全体のボス・400/300 の癒し手・天井 +11・割合の回復・7台目の毒台・規定の組・26 項目）
    dotnet run --project BattleSim -c Release 0 checkwave log <台> <波> [seed]   # 1戦のログ（台は 燃焼 T3-244 ／ 燃焼 T3-255 ／ 移動 ／ 雷 ／ 毒 ／ 混ぜ-255・波は B3-桁 ／ W3-割合 ／ W2-対照 ／ W2-50後 ／ W2-50奥 ／ B2-全4 ／ B2-半4、第261期の版は `261:`・第260期の版は `260:` を前に付ける）
    dotnet run --project BattleSim -c Release 0 shockdigest s232 # 第232期の台（前段の規定・固定なし）
    dotnet run --project BattleSim -c Release 0 shockdigest a231 # 第231期の台（V0＝前段の規定の台本が実装の前後で一致すること）
    dotnet run --project BattleSim -c Release 0 shockdigest w230 # 第230期の台（W0＝前段の規定の台本が実装の前後で一致すること・今の既定の規則で回す唯一のモード）
    dotnet run --project BattleSim -c Release 0 shockdigest r225 # 第225期の台（J0＝規定のシオ・規定のセロの台本が実装の前後で一致すること）
    dotnet run --project BattleSim -c Release 0 shockdigest f224 # 第224期の台（F0＝第223期 E2・H0＝規定のシオの台本が実装の前後で一致すること・第223期の出来事も指紋に入れる）
    dotnet run --project BattleSim -c Release 0 shockdigest d222 # 第222期の台（V0＝今のシオ・ヨミの台本が実装の前後で一致すること）
    dotnet run --project BattleSim -c Release 0 debuff [モード]   # デバッファー3枚の転生（第189期・ネル／クビ／ハネ）。本体は `Modes/Debuff.cs` / `Debuff.Run.cs`
    dotnet run --project BattleSim -c Release 0 debuff phase0    # Q0-1（3枚の在席行・隣の駒・同席の相手。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 debuff run       # 8版（旧／1枚ずつ新／新／マイナスだけ外した3版）× 在席行・(G2)・勝ち方・全体の指標（20 秒）
    dotnet run --project BattleSim -c Release 0 debuff ledger    # 帳簿（呪い・萎縮・突き返し・入れ替えと読み手）
    dotnet run --project BattleSim -c Release 0 debuff bench     # 診断台3つ（台N・台K・台H）× 素体／旧／新／マイナスなし
    dotnet run --project BattleSim -c Release 0 debuff rows      # 表F（事後）: クビを毒の行へ・ハネを移動の行へ入れる
    dotnet run --project BattleSim -c Release 0 debuff check [採用前のbalance.md]  # 自己検査（(a) 53 行 0 件・(b) 旧に戻すと 305 セル 0 件・(c) 乱数なし・(d) 台本・(e) 旧の札の保持者 0）
    dotnet run --project BattleSim -c Release 0 beni [モード]     # 毒喰らいのベニの転生（第190期・反転の結界）。本体は `Modes/Beni.cs` / `Beni.Run.cs`
    dotnet run --project BattleSim -c Release 0 beni phase0      # Q0-1〜Q0-7（在席と隣・`Heal` の口と宛先・`OnDamaged` の門・行動順・名前。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 beni phase191    # 第191期 Q0-1〜Q0-3（周期と燃焼の持続の机上の表・燃焼を読む札・ベニの隣のホタ／ヒヨ）
    dotnet run --project BattleSim -c Release 0 beni phase192    # 第192期 Q0-2 / Q0-3（ベニ自身に入った毒・火・回復を台本から書き手別に・被ダメ総量）
    dotnet run --project BattleSim -c Release 0 beni ledger192   # 第192期 ベニ自身の帳簿（倒れた戦の割合・倒れたT・自身の反転と裏・溢れた回復）。**`IncludesSelf` はビルドで切り替える**
    dotnet run --project BattleSim -c Release 0 beni phase193    # 第193期 Q0-3 / Q0-4（`OverflowToHolder = false` のビルドで溢れと受け取れた上限を数える）
    dotnet run --project BattleSim -c Release 0 beni ledger193   # 第193期 啜りの帳簿（書き手あり／なし・隣の枚数・流れ込んだ量・倒れた戦）。**`OverflowToHolder` はビルドで切り替える**
    dotnet run --project BattleSim -c Release 0 beni run         # 表A 在席行 × 5版（旧／新／マイナスなし／手番なし／素体）＋ 表B 差し替え表（毒・燃焼の 25 行・主表）
    dotnet run --project BattleSim -c Release 0 beni ledger      # 帳簿（反転の回復・延べ人数・最大同時・澱み分け・裏の出どころ・離れた後の刻み・旧との回復総量）
    dotnet run --project BattleSim -c Release 0 beni bench       # 診断台3つ（台P 毒・台F 燃焼・台H 回復役）× 5版 ＋ 駒ごとの生存T
    dotnet run --project BattleSim -c Release 0 beni check [採用前のbalance.md]  # 自己検査（(a) 59 行 0 件・(b) **第190期に戻すと** 305 セル 0 件・(c) 乱数なし・(d) 再反転 0・(e)〜(h)・(i)(j) 啜り）
    dotnet run --project BattleSim -c Release 0 mio [モード]      # 澱みのミオの転生（第194期・濃縮の印＝刻みの追加回数）。本体は `Modes/Mio.cs` / `Mio.Run.cs`
    dotnet run --project BattleSim -c Release 0 mio phase0       # Q0-1〜Q0-11（在席と隣・`Thicken`・層の型・毒と燃焼の読み手・起爆・刻みごとに走るもの。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 mio run          # 表A 在席行 × 4版（旧／新／マイナスなし／素体）＋ 表B 差し替え表（主表）
    dotnet run --project BattleSim -c Release 0 mio ledger       # 帳簿（毒と燃焼を分けた刻み・2回目以降・印の最大・1回の刻みの発火回数の最大・味方の側・波ごと）
    dotnet run --project BattleSim -c Release 0 mio check [採用前のbalance.md]  # 自己検査（(a) 56 行 0 件・(b) 旧に戻すと 305 セル 0 件・(c) 乱数なし・(d)(e)(g) 刻みが 1+n 回・(f)）
    dotnet run --project BattleSim -c Release 0 galdlast [モード] # ガルドの剣の段（第198期）。本体は `Modes/GaldLast*.cs`
    dotnet run --project BattleSim -c Release 0 galdlast phase0  # Q0-1（ガルドが最後の1体になる戦・第197期の盤面・計数だけ）
    dotnet run --project BattleSim -c Release 0 galdlast phase199 # 第199期 Q0-2 / Q0-3（抜く瞬間の受け流した刃の累計・在庫の分布。第198期の盤面で読む）
    dotnet run --project BattleSim -c Release 0 galdlast run     # 版（無／198／199／傷は旧／在庫なし／相打ち負け／盾剣）× ガルドの在席 35 行・入った戦・上乗せ・ダメージの内訳
    dotnet run --project BattleSim -c Release 0 galdlast ledger  # 帳簿（波ごと・行ごと・傷の累計と加えた攻撃力）
    dotnet run --project BattleSim -c Release 0 galdlast check [採用前のbalance.md]  # 自己検査（第199期: (a) 198 の版 305 セル・ガルドなし 130 セル 0 件・(b) 入るTが版に依らない・(c) 抜いた後の構え直し 0・受け流した一撃に返さない・(d) 生存 0 の勝ちは相打ち勝ちだけ・(e) 会戦・(f) 乱数なし）
    dotnet run --project BattleSim -c Release 0 form2 [モード]    # 味方の陣形パターン2（ひし形・前衛1枚・第200期）。本体は `Modes/Formation2*.cs`
    dotnet run --project BattleSim -c Release 0 form2 phase0    # Q0-1〜Q0-7（`FormationRules` の呼び出しの数・列・召喚・席を読む札・表B の行・敵の攻撃型。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 form2 run       # 表A（毒パ 7 行 × 元／X・スィド／P2）・表B（タンク 6 行 × X／P2 × ガルド・ゴルム・ササ・スィド）・帳簿（7 秒）
    dotnet run --project BattleSim -c Release 0 form2 log "<行名>" <波> <seed> [x]  # 表B の P2・ガルド（x で X 字）の1戦のログ
    dotnet run --project BattleSim -c Release 0 form2 check [採用前のbalance.md]  # 自己検査（表の全セル・X 字の表の一致・`compare` 0 件・乱数なし・召喚の重なり）
    dotnet run --project BattleSim -c Release 0 form2 phase201  # 第201期 Q0-1〜Q0-4（編成画面の入口・席の総当たりの道具・戦闘数・鏡像。**戦闘0回**）。本体は `Modes/Formation2.P201.cs`
    dotnet run --project BattleSim -c Release 0 form2 run201    # 第201期 表A・表B・表C・帳簿・最良の並び（**選ぶ** 120 通り × 第2〜5波 × seed 0..49 ／ **測る** seed 100..299・52 秒）
    dotnet run --project BattleSim -c Release 0 form2 check201 [採用前のbalance.md]  # 第201期 自己検査（選び方の決定性・席名・第200期の検査を丸ごと）
    dotnet run --project BattleSim -c Release 0 form2 phase202  # 第202期 Q0-1〜Q0-4（貫きの入口・敵の貫き持ちのレーン・スィドの在席・席札の出どころ。**戦闘0回**）。本体は `Modes/Formation2.P202.cs`
    dotnet run --project BattleSim -c Release 0 form2 run202    # 第202期 表A・表B のパターン2の列を新しい貫きで選び直す・上下の入れ替え・帳簿（109 秒）
    dotnet run --project BattleSim -c Release 0 form2 check202 [第201期のbalance.md]  # 第202期 自己検査（スィドなし 0 件・S に戻すと一致・乱数なし・**台本で貫きの経路を突き合わせ**・席名）
    dotnet run --project BattleSim -c Release 0 lili [モード]     # 施しのリリ（第204期・ノノの転生）。本体は `Modes/Lili*.cs`
    dotnet run --project BattleSim -c Release 0 lili phase0    # Q0-1 在席行と状態の書き手・読み手／Q0-3 状態キーの分類（抜けがあれば名指しで落ちる。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 lili run       # 在席行 × 5版（旧／新／移さない／吸うだけ／新・30）・情報セル・膠着
    dotnet run --project BattleSim -c Release 0 lili ledger    # 帳簿（吸い取り・儀式・還る・倒れ方・負け戦の回復/T・破片の最大値・移した状態 × 受け取り手）
    dotnet run --project BattleSim -c Release 0 lili check [第203期のbalance.md]  # 自己検査（(a) 53 行 0 件・旧で一致／(c) 台本で 儀式で移らない・聖痕が味方に付かない・渇きで破片にならない・儀式の後に聖痕が消える）
    dotnet run --project BattleSim -c Release 0 lili phase205  # 第205期 Q0-2・Q0-3・Q0-5（味方が失った HP の出どころ・1手番の痛み・吸った敵の AtkBonus）。本体は `Modes/Lili.P205*.cs`
    dotnet run --project BattleSim -c Release 0 lili run2      # 第205期 在席行 ＋ 診断行 × 梯子 V0〜V4（第2〜5波）
    dotnet run --project BattleSim -c Release 0 lili ledger2   # 第205期 第四波・第五波を別々に（痛み・吸った量・与ダメ:回復・段・破片・カド・強弱の移し）
    dotnet run --project BattleSim -c Release 0 lili check2 [第204期のbalance.md]  # 第205期 自己検査（53 行 0 件・V0 で一致／台本で 痛み ＝ Damage から組み直した値・段は下がらない・儀式で移らない ほか）
    dotnet run --project BattleSim -c Release 0 lili phase206  # 第206期 Q0-1〜Q0-4（儀式の頭の敵の数・儀式で決着・施した累計が 40/120/240/400 を越えたターン・敵1体の編成）。本体は `Modes/Lili.P206*.cs`
    dotnet run --project BattleSim -c Release 0 lili run3      # 第206期 在席行 × W0〜W2（第2〜5波）
    dotnet run --project BattleSim -c Release 0 lili ledger3   # 第206期 第四波・第五波を別々に（儀式で決着・敵の数・1体最大・段・与ダメ:回復）
    dotnet run --project BattleSim -c Release 0 lili check3 [第205期のbalance.md]  # 第206期 自己検査（段の閾値・総量 ＝ 吸う量 × 5・残り HP を超えない・儀式で移らない）
    dotnet run --project BattleSim -c Release 0 tsugi [モード]    # 継ぎ当てのツギ（第207期・ナラの転生）。本体は `Modes/Tsugi*.cs`
    dotnet run --project BattleSim -c Release 0 tsugi phase0   # Q0-1 在席行・火の書き手・破片／Q0-6 被弾で動く駒（リフレクション・戦闘0回）
    dotnet run --project BattleSim -c Release 0 tsugi run      # 在席行 × 版 T0〜T3（勝率・全員生存勝ち・倒れた数・決着T・膠着）
    dotnet run --project BattleSim -c Release 0 tsugi swap     # リリの席にツギ（8 行 × 波）＋ 火の診断（燃焼の行）＋ ベニの診断
    dotnet run --project BattleSim -c Release 0 tsugi ledger   # 帳簿（貼った・厚さ・在庫・受け止めた・拾い・倍・ツギ倒れ・ガルドに貼った割合）
    dotnet run --project BattleSim -c Release 0 tsugi phase208 # 第208期 Q0-1 敵の範囲攻撃／Q0-4 ドハと破片の書き手（戦闘0回）
    dotnet run --project BattleSim -c Release 0 tsugi run2     # 第208期 在席 3 行 × U0〜U3（反射・燃えやすさの差し替え・分かちの後置）
    dotnet run --project BattleSim -c Release 0 tsugi swap2    # 第208期 リリの席にツギ（L / U0〜U3）＋ 火（U1/U2）＋ ベニ（U1/U2）
    dotnet run --project BattleSim -c Release 0 tsugi ledger2  # 第208期 反射の帳簿（回・量・倒した・1回の攻撃で返った本数と主が倒れた割合）・分かちの後置
    dotnet run --project BattleSim -c Release 0 tsugi check2 [第207期のbalance.md]  # 第208期 自己検査（U0 一致・印の無い駒は返さない・返す先は敵・量は失った破片まで・軛 25・倍は印のときだけ・分かちの後置）
    dotnet run --project BattleSim -c Release 0 tsugi check [第206期のbalance.md]  # 自己検査（T0 一致・verbose 非依存・渇き／支援拒否でも貼る・印は破片 0 で消える・倍は印のときだけ・在庫・倒れた後は拾わない）
    dotnet run --project BattleSim -c Release 0 ep3 [モード]      # 敵のパターン3（前衛1枚・第203期）。本体は `Modes/EnemyP3*.cs`
    dotnet run --project BattleSim -c Release 0 ep3 phase0     # Q0-1〜Q0-6（敵の陣形の入口・敵の札が席を読むか・召喚・味方の攻撃型の群・敵の席を動かす札・再生。**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 ep3 run        # 主表（61 行 × 第三波・第五波 × 元／写し）・攻撃の型で分けた読み・帳簿（5 秒）
    dotnet run --project BattleSim -c Release 0 ep3 check [balance.md]  # 自己検査（§2 の表・`compare` 0 件・乱数なし・背かれの餌が湧いて編成の席に立たない）
    dotnet run --project BattleSim -c Release 0 rebirth2 [モード] # B群の転生 3〜5枚目（第180期・ムド／ヴィオ／ガン）。本体は `Modes/Rebirth2.cs`
    dotnet run --project BattleSim -c Release 0 rebirth2 phase0  # Q0-1〜Q0-10 を実装から引き直す（在席・被弾内訳・窓口・隣接）
    dotnet run --project BattleSim -c Release 0 rebirth2 run     # 1枚ずつの対照と3枚同時（**線は置かない**）＋ **表B' 読み手を隣に置いた台**
    dotnet run --project BattleSim -c Release 0 rebirth2 ledger  # 3枚の帳簿（暴発・泥散り・吐き戻し・叩き起こし）
    dotnet run --project BattleSim -c Release 0 rebirth2 check   # 自己検査（(a)〜(h)。**42 行 0 件差分**とカドが起こされないこと）
    dotnet run --project BattleSim -c Release 0 susu [モード]    # A群の転生1枚目（第179期・拾い屋のスス）。本体は `Modes/Susu.cs`
    dotnet run --project BattleSim -c Release 0 susu phase0      # Q0-1〜Q0-5 を実装から引き直す（＋味方由来ダメージの供給量の実測）
    dotnet run --project BattleSim -c Release 0 susu run         # §2-2 ススを入れた行の変化表（**線は置かない**）
    dotnet run --project BattleSim -c Release 0 susu foes        # §2-3 敵の体数への反応（3体／4体／5体）
    dotnet run --project BattleSim -c Release 0 susu check        # 自己検査（(a')〜(g')。**灰の収支が閉じるか**）
    dotnet run --project BattleSim -c Release 0 rebirth phase0   # Q0-1〜Q0-5 を実装から引き直す（戦闘0回）
    dotnet run --project BattleSim -c Release 0 rebirth hota     # §1-1 の数え物（V0 ＝ `EmberRule.Charred` 対 V1 ＝ 既定）
    dotnet run --project BattleSim -c Release 0 rebirth utsu     # ウツの帳簿（手数・出力・寿命 ＋ **台本に `Attack` が何件並ぶか**）
    dotnet run --project BattleSim -c Release 0 rebirth rows     # 動いた行の全表（`docs/balance.md` との差・拒否権・歯止め）
    dotnet run --project BattleSim -c Release 0 rebirth check [balance.md]  # 自己検査（(a)(a')(b)(c)(d)(e)）

`DemoApp` の検証用マップ 1-1（第169期・**`BattleSim` ではなく Godot で回す**）。
本体は `DemoApp/Map11.cs`（進行の規則）と `Map11Main.cs`（画面。判定は持たない）。

    Godot_console.exe --path DemoApp --headless -- --map11-phase0
        # **第170期**: 説明文の出どころ・帳簿・§4 の材料を**実装から引く**（戦闘は12回だけ）。
        # **第173期に門の相手が移った**——手書きの札の表ではなく、
        # **このマップに出る敵 18 体の `UnitDef.PlusText`** に抜けがあれば名指しで落ちる（R260）
    Godot_console.exe --path DemoApp --headless -- --map11-time[=N[,起点]]
        # **第174期 段B1 / 第175期 §3**: 時間を器具で当てる（`Map11Time.cs`）。**画面を1つも作らない。**
        # **第175期に K を振るのをやめ**（採用値 1）、**迎撃の回復（`InterceptRecover`）あり/なし ×
        # 方針4つ**（貪欲／全快／籠城／即出し ＋ 参考の「**籠城・控えから出す**」「即出し・控えは南」）
        # × seed N（既定 800）
        # ＋ `TimeOn=false` の対照にした。**4.8 秒**。起点を渡すと帯B の追試（`--map11-time=800,800`）。
        # **方針が持つのは「行き先」だけ**で、そこから命令を作るのは遊ぶ側と同じ `Map11Orders`（R259）
    Godot_console.exe --path DemoApp --headless --quit-after 90000 res://Map11Main.tscn -- --map11-time-smoke
        # **第174期 自己検査 (d)**: seed 0 を全快方針で通す（**「休む」と「迎撃」を1回ずつ以上含む**）。
        # 合否は `MAP11_FLOW_SMOKE_COMPLETE` と `MAP11_TIME_SMOKE rests=… intercepts=…` で読む
    Godot_console.exe --path DemoApp --headless -- --map11-verify[=N]
        # **遊ぶ側と同じ `Map11State`** を器具と同じ自動規則で N seed 回す（既定 800）。
        # 第168期 部B の5つの量と **差 0.0pt で一致する**のが受け入れ条件（R259）
    Godot_console.exe --path DemoApp --headless -- --map11-phase176
        # **第176期 Phase 0**: Q0-1〜Q0-5（`PortalOn = false` が第175期の 18 量と一致するか ／
        # 重なりと同点の割り方 ／ 湧いた部隊を満タンで作る口 ／ 方針の手順 ／ 走行時間）。約 1 秒
    Godot_console.exe --path DemoApp --headless -- --map11-portal[=N[,起点]]
        # **第176期 段1**: 敵の拠点（`Map11Portal.cs`）。**湧きの間隔 S（2/3/4/6）× 方針 7 つ** ×
        # seed N（既定 800）＋ `PortalOn = false` の対照 ＋ **湧き 0（S = 9999）の点**。約 11 秒。
        # **画面を1つも作らない**（`Map11Time` の方針に `Seize` 系を足しただけで、器具は1本のまま）。
        # **完全一致か `=` 付きだけ**を受ける（`--map11-portal-scan` は別物・R231）
    Godot_console.exe --path DemoApp --headless -- --map11-portal-scan
        # **第176期 §6-3**: 湧きの間隔を 1 刻みで掃引する診断（**線には使わない**）。
        # 参考方針が **S = 3 と 6（制圧T 3.0 の倍数）で凹む**のを見るためだけの表。約 25 秒
    Godot_console.exe --path DemoApp --headless -- --map11-portal-seed[=本数]
        # **第177期 自己検査 (c)**: 「制圧 → ワープ → 第2拠点で組み直し → 勝利」の4つが
        # 全部立つ seed を**頭なしで探す**（既定 64 本・約 20 秒）。方針は `Map11Smoke.Orders` で、
        # **画面の通し（`--map11-portal-smoke`）とまったく同じ1本**を通る（R259）。
        # **`--map11-portal` より先に見る**——札を増やすたびに前後関係を確かめること（R231）
    Godot_console.exe --path DemoApp --headless --quit-after 60000 res://Map11Main.tscn -- --map11-flow-smoke
        # 画面とシーン遷移を通して1マップを最後まで（戦闘の再生も本物を通る）。**数字を作る道具ではない**。
        # **合否は `MAP11_FLOW_SMOKE_COMPLETE` の有無で読む**——終了コードは 0 にならない
        # （`RunFinalizers` の Fatal error。**第169期から同じ**で、第170期が入れたものではない）
    Godot_console.exe --path DemoApp --headless -- --map11-phase171
        # **第171期 Phase 0**: Q0-1〜Q0-5 ＋ **自己検査 (c)**（帳簿の数 ＝ 台本の封じイベントの数）。
        # `Traits.cs` / `BattleEngine.cs` を走査し、手書きの `Map11Relations.Rules` と
        # `BoardRuleTags` に抜けがあれば**名指しで落ちる**（R260）。戦闘は 240 戦
    Godot_console.exe --path DemoApp --headless -- --map11-phase172
        # **第172期 Phase 0**: Q0-1〜Q0-6 ＋ 自己検査 (d)（組み直しの規則が破れないこと）。
        # 控えの駒 7 枚を**規則で選び直して**表にし、手書きの
        # `Map11Relations.Rules` の**意味の1行・得／損の印・説明文に出る語**に欠けがあれば
        # **名指しで落ちる**（R260）。
        # **関係の門は 52 枚（`UnitCatalog.All`）**で、第171期の「このマップに出る札」より広い
    Godot_console.exe --path DemoApp --headless -- --map11-phase173
        # **第173期 Phase 0**: Q0-1（`Unresolved` を (a) 席から引けない ／ (b) 条件が席の外 に仕分ける）
        # Q0-2（得／損／両方 の内訳）／**Q0-3（線の1語と説明文の語を 52 枚の全保持者に当てる）**／
        # Q0-4（敵 18 体の説明文）／Q0-5（文字列を変えても門が動かないこと）＋
        # **自己検査 (d)（拠点へ戻す規則）と (e)（ボルグをリィカの隣に置くと損の点線が 2 本出る）**
    Godot_console.exe --path DemoApp --headless --quit-after 60000 res://Map11Main.tscn -- --map11-flow-smoke --map11-reform-smoke
        # **第172期 自己検査 (c)**: **1 回組み直してから**通す（席／隊どうし／控えの 3 操作を1回ずつ）。
        # 合否は `MAP11_FLOW_SMOKE_COMPLETE` の有無（終了コードは第169期から 0 にならない）
    Godot_console.exe --path DemoApp --headless --quit-after 60000 res://Map11Main.tscn -- --map11-flow-smoke --map11-recall-smoke
        # **第173期 自己検査 (d)**: **道へ出した隊を拠点へ戻し、組み直してから**通す。
        # 合否は `MAP11_RECALL_SMOKE`（4 つとも True）と `MAP11_FLOW_SMOKE_COMPLETE` の有無
    Godot_console.exe --path DemoApp --headless --quit-after 240000 res://Map11Main.tscn -- --map11-portal-smoke
        # **第177期 自己検査 (c)**: **制圧 → ワープ → 第2拠点で組み直し → 勝利**を通す（seed 20 固定）。
        # 合否は `MAP11_PORTAL_SMOKE_COMPLETE ok=True`。
        # **方針はわざと「湧きが 1 部隊出るまで道の奥で待つ」**——そうしないと
        # **制圧した瞬間に盤上の敵が 0 になって勝ってしまい**、ワープを1度も通せない（R280）
    Godot_console.exe --path DemoApp --headless --quit-after 240000 res://Map11Main.tscn -- --map11-draft-smoke
        # **第177期 自己検査 (d)**: **空の手札から3隊を組んで出撃する**（15 枚を1枚ずつ3隊へ回す）。
        # **既定の3隊とは別の編成になる**のが要点。合否は `MAP11_DRAFT_SMOKE_COMPLETE ok=True`
    Godot_console.exe --path DemoApp --headless --quit-after 240000 res://Map11Main.tscn -- --map11-run-smoke
        # **第177期（ポンの2巡目を受けて足した）**: **人が押す経路**（`RunUntilEvent`）を通す。
        # **他の通しは全部 `AutoStep` という別のループ**なので、ボタンの経路には門が1つも無かった（R283）。
        # 見るのは1つだけ——**終わっているのに結果画面が出ていない状態を作らないこと**。
        # 合否は `MAP11_RUN_SMOKE_COMPLETE ok=True`
    Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast --demo-preset=<行名の部分一致> --demo-stage=<0始まり> --demo-seed=<n>
        # 単発の戦闘を頭なしで最後まで再生する。**合否は `DEMO_SMOKE_COMPLETE` の行で読む**（終了コードではない）。
        # **第178期に計数を2つ足した**——`attackPlays`（`Attack` の絵を流した回数）と
        # `maxRun`（**同じ駒が `TurnStart` を挟まずに連続で振った最大回数**）。**表示専用**で、
        # 連撃（ウツ）が1発ずつ別の絵・別の音になっていることの確認に使う
    Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast --demo-preset=<行名> --demo-stage=<n> --demo-seed=<n> --demo-shape=p2 [--demo-p2-front=<駒Id>]
    Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast --demo-setup-shape=p2 --demo-preset=<行名> --demo-stage=<n> --demo-seed=<n>
        # **第201期**: 編成画面の陣形の切り替え（ボタンと同じ `SetShape`）を通して出撃する。`DEMO_SHAPE ... source=setup` の行に並びが出る
        # **第200期**: 組んだ X 字の編成をパターン2（D ＝ 前1 の駒か指定の駒・残りを X 字の席の順で A・C・E・B）に詰め替えて出撃する。
        # **再生の確認用**（編成画面は X 字の5枠のまま）。`DEMO_SHAPE` の行に並びが出る。本体は `Main.Shape.cs`
    Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast --demo-enemy-p3 --demo-preset=<行名> --demo-stage=2|4 --demo-seed=<n>
        # **第203期**: 第三波・第五波を敵のパターン3の写しで始める（`DEMO_ENEMY_SHAPE` の行に並びが出る）。写しの無い波では元の波のまま
    Godot.exe --path DemoApp --quit-after 900 -- "--demo-setup-capture=out.png[,<席>]"
        # **編成画面**の絵を1枚撮る（第172期 部B の見た目の確認だけ）。席を渡すとその駒を選んだ絵
    Godot_console.exe --path DemoApp --headless -- --map11-artgap
        # **第171期 部C**: 演出の穴の棚卸し（**手書き 0 行**）。240 戦の台本の実数 ＋
        # `Main.cs` の `case BattleEventKind.X:` の走査から、駒 × 出来事 × 絵の表を作る
    Godot.exe --path DemoApp --quit-after 400 res://Map11Main.tscn -- "--map11-capture=out.png[,detail|encounter|pick|reform]"
        # 画面の当たりを1枚撮る（見た目の確認だけ）。`pick` は**図の札を1枚選んだ絵**、
        # `reform` は**組み直し中（札をつまんで控えの駒が見えている）の絵**

`BattleSim` の表に戻る。

    dotnet run --project BattleSim -c Release 0 gust scan probe  # **第152期 段0**: 埋め草の強さを掃引して台を帯（40〜95%）へ戻す
    dotnet run --project BattleSim -c Release 0 sweep [上限秒] [絞り込み]  # **全診断の exit 検査**（第141期・本体は `Sweep.cs`）。この表を自分で読んで引数の穴の無い本を子プロセスで回す。引数なしは `full`（規約 (G17)）
    dotnet run --project BattleSim -c Release 0 sweep full      # 第278期 全量（指紋照合あり・直列）。鍵が動かなかった本は走らせず前回の結果を `（照合）` で返す。**第280期から「全量」は現役網（`SweepDiag.Active`・57 本）**——ほかは凍結庫。作法は「日中は fast・full は夜間（`sweep nightly`）」
    dotnet run --project BattleSim -c Release 0 sweep fast      # 第278期 主判定系の系統だけ（`SweepDiag.FastFamilies`: compare ／ dump ／ audit ／ derive ／ spread ／ chain ／ cross ／ checkup ／ checkwave ／ bosswave ／ relic・58 本 → 第280期から現役網のうち 22 本）。**期中の判定用**
    dotnet run --project BattleSim -c Release 0 sweep full nocache   # 第278期 指紋照合を使わず全部走らせる（キャッシュ `.sweep/cache.tsv` は更新する）。`j=N`（または環境変数 SWEEP_JOBS）で並列（**早見用・上限の判定が直列と揃わない**。既定は `j=1` の直列）
    dotnet run --project BattleSim -c Release 0 sweep check     # 第278期 自己検査（鍵の作り方: 同じ入力で同じ鍵・BattleCore 1文字で全本・器具1本で辿る系統だけ・design で読む本だけ・振り分け1行で生ソースを読む本だけ）。子プロセス0本
    dotnet run --project BattleSim -c Release 0 sweep list      # 走らせる一覧だけ（戦闘0回・子プロセス0本）
    dotnet run --project BattleSim -c Release 0 sweep frozen    # 第279期 凍結庫の一覧と封印（`design/SWEEP_FREEZER.md`・子プロセス0本）。凍結 70 本 ／ audit が正の生成器 2 本（layout ／ reseat）／ 煙試験に置き換えた relic 5 本は sweep の一覧から外れる。既定の上限は 150 秒・`roster audit` は 600 秒・**上限は異常に数える**
    dotnet run --project BattleSim -c Release 0 sweep seal [上限秒] [j=N] [群か絞り込み]   # 第279期 凍結した本を上限つきで完走させ、出力の指紋を `design/freezer/seals.tsv` に封印する（完走した出力は `design/freezer/out/`）
    dotnet run --project BattleSim -c Release 0 sweep thaw <絞り込み> [上限秒]  # 第279期 解凍: 凍結した本を走らせて封印と照合（既定の上限 7200 秒・出力は `.sweep/thaw/`）。不一致なら git bisect で原因のコミットを探す
    dotnet run --project BattleSim -c Release 0 sweep roster    # 第280期 現役網の台帳（本・層 a ／ b・所要・理由）と検算を markdown で出す（`design/SWEEP_ROSTER.md` に貼る・子プロセス0本）。一覧の本体は `SweepDiag.Active`
    dotnet run --project BattleSim -c Release 0 sweep nightly   # 第280期 夜間の full: 現役網・指紋照合なし・直列。前回の指紋と比べた朝のサマリを `.sweep/nightly/<日付>.md` に書く（指紋の差分の本の名・異常終了 ／ 打ち切りの本数・所要・前回との差・末尾に `<!-- nightly minutes=… -->`）。無人で回すのは `tools/sweep_nightly.ps1`（DemoApp の門も足す・手順は design/SWEEP_NIGHTLY.md）
