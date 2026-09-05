# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 概要

「捨てられた駒に役割を与えて噛み合わせる」編成が面白いかどうかだけを確かめる実験装置（オートバトラーのプロトタイプ）。グラフィックや演出は対象外。コメント・ユニット名・ログはすべて日本語で書かれており、追加するコードもそれに合わせる。

## コマンド

テストプロジェクトは無い。検証はすべて BattleSim の実行結果で行う。

    dotnet build                                            # 全体ビルド（WPF を含むので Windows のみ）
    dotnet run --project PrototypeApp                       # 編成UI（Windows / WPF）
    dotnet run --project BattleSim -c Release <n>           # ステージ n (0-4) を総当たり
    dotnet run --project BattleSim -c Release <n> <unitId>  # 指定ユニットを含む編成に絞る（例: rica）
    dotnet run --project BattleSim -c Release 0 compare > docs/balance.md  # 代表編成 × 全ステージの勝率比較
    dotnet run --project BattleSim -c Release 0 dump > docs/units.md      # ユニット・特性・ステージ一覧
    dotnet run --project BattleSim -c Release 0 layout      # 代表編成の全配置総当たり（並列・決定的）
    dotnet run --project BattleSim -c Release 0 reseat [絞り込み] [skip] [take]  # 配置候補を seed 200 で測り直す
    dotnet run --project BattleSim -c Release 0 confirm     # 差し替え候補を別 seed で追試する
    dotnet run --project BattleSim -c Release 0 chain > docs/chain.md    # 勝率だけでは見えない「連鎖の深さ」（最大同時撃破数・決着ターン数）
    dotnet run --project BattleSim -c Release 0 ablate [絞り込み] > docs/ablation.md  # 編成から1体ずつ抜いた勝率変化（入れ得の検出）
    dotnet run --project BattleSim -c Release 0 pulse [絞り込み] > docs/pulse.md      # 駒ごとの活動量（振/干渉）と与被ダメージの内訳
    dotnet run --project BattleSim -c Release 0 engage [絞り込み] > docs/engage.md    # 会戦（地点主表: 突破率・期待突破数×投入部隊数1-3・非線形・入場戦力・第1削り）
    dotnet run --project BattleSim -c Release 0 seats [絞り込み]    # 会戦の隊列持ち越し診断（診断用。docs/ に置かない）
    dotnet run --project BattleSim -c Release 0 seats2 list         # 隣接／列を読む駒の一覧と行数（戦闘0回。第45期）
    dotnet run --project BattleSim -c Release 0 seats2 degree       # 次数分布と、現行48行 × その鏡像の差（角の対称性）
    dotnet run --project BattleSim -c Release 0 seats2 [skip] [take]  # 駒ごとに「編成が変わると席が変わるか」を測る（全48行で約7分）
    dotnet run --project BattleSim -c Release <n> demo      # 固定編成1戦の詳細ログを表示
    dotnet run --project BattleSim -c Release <n> demo "編成名" [seed]  # compare の編成で1戦の詳細ログ
    dotnet run --project BattleSim -c Release <n> replay "編成名" <seed>  # 1戦を再生用JSON（台本）で吐く

第4〜18期に足した診断モード。**どれも docs/ には置かない**（標準出力で読むだけ）。
所要は全編成でおおむね 10〜30秒、`bridge` だけ 30秒前後。

    dotnet run --project BattleSim -c Release 0 handoff [絞り込み]  # 会戦の部隊引き継ぎ（第4期 Phase K）
    dotnet run --project BattleSim -c Release 0 cost [絞り込み]     # 波の「代金」= 100% − 勝った試行の残HP%（第5期 Phase M）
    dotnet run --project BattleSim -c Release 0 gradient [絞り込み] # 勾配のある部隊列の候補を測る（第5期 Phase N）
    dotnet run --project BattleSim -c Release 0 aim [絞り込み]      # 安い波の再設計・素体候補（第6期 Phase P）
    dotnet run --project BattleSim -c Release 0 flip [絞り込み]     # 代金の「向き」を作れるか（第7期 Phase R）
    dotnet run --project BattleSim -c Release 0 bridge [絞り込み]   # 向きは序列を動かすか（第7期 Phase S〜第10期）
    dotnet run --project BattleSim -c Release 0 bill [絞り込み]     # 代金を 敵由来/自傷/回復/残差 に割る（第9期 Phase X）
    dotnet run --project BattleSim -c Release 0 charge [絞り込み]   # 大技の発火率とチャージ化の前後（第10期 Phase AC）
    dotnet run --project BattleSim -c Release 0 timing [絞り込み]   # 味方の行動パターンの変種（第11期 Phase BC）
    dotnet run --project BattleSim -c Release 0 power [絞り込み]    # 「地力」の分解（第12期 CA/CB・第13期 DA・第14期 EA/EB）
    dotnet run --project BattleSim -c Release 0 bench [絞り込み]    # 台をまたぐ入れ替わりは構造的か（第13期 Phase DB）
    dotnet run --project BattleSim -c Release 0 wave [絞り込み]     # 編成 × 波の交互作用（単発戦。第15期 FA/FB）
    dotnet run --project BattleSim -c Release 0 dissect [絞り込み]  # 交互作用の個別事例の解剖 + 敵側の特徴量（第16期 GA/GB）
    dotnet run --project BattleSim -c Release 0 output [絞り込み]   # 参照台と出力特徴量 (A)(B)(C)（第17期 HA/HB）
    dotnet run --project BattleSim -c Release 0 convert [絞り込み]  # 個体HP だけを振った台の系列と変換率（第18期 IA）
    dotnet run --project BattleSim -c Release 0 ptrace [絞り込み]   # 毒の立ち上がり診断
    dotnet run --project BattleSim -c Release 0 life [絞り込み] [駒Id]  # 駒の寿命と稼働率（第19期。既定は kado）
    dotnet run --project BattleSim -c Release 0 route             # 自傷の燃料は変換器まで届くか（第19期）
    dotnet run --project BattleSim -c Release 0 swap              # 同じ席で駒を入れ替えて比べる（第21期）
    dotnet run --project BattleSim -c Release 0 spread [除外語]   # 波の分離度（飽和・波間相関・固有の勝者敗者）（第22期。除外語で行を外して同じ行数で前後比較・第49期）
    dotnet run --project BattleSim -c Release 0 gullet [gain|log] # 巨躯の吐き戻し（4版の対照 / 返す効率の振り / 1戦の監査）（第23期）
    dotnet run --project BattleSim -c Release 0 gullet belly      # 腹の規模の実測（閾値と還し率の導出。盤面は動かさない）（第36期）
    dotnet run --project BattleSim -c Release 0 gullet belly4     # まどろみ／還しの4版対照（V0/V2/V3/V4）（第36期）
    dotnet run --project BattleSim -c Release 0 yield [絞り込み]  # 攻撃力1点は誰の手なら出力になるか（注入テスト）（第24期）
    dotnet run --project BattleSim -c Release 0 yoke [sweep|log]  # 第四波の軛（5版の対照 / 上限の振り / 1戦の監査）（第25期）
    dotnet run --project BattleSim -c Release 0 hush [log]        # 第二波の粛（3版の対照 / 1戦の監査）（第27期）
    dotnet run --project BattleSim -c Release 0 guard [percent]   # 殉教者の体（HP 4点）／介入の密度（p 3点）の掃引（第34・35期）
    dotnet run --project BattleSim -c Release 0 sever [絞り込み]  # 断ちの発火・手番の放棄・傷の取り合い（第37期）
    dotnet run --project BattleSim -c Release 0 sever sale        # 捨てた手番は号令・据えに売れないか（陽性対照つき）（第37期）
    dotnet run --project BattleSim -c Release 0 sever reach       # 傷はどこまで積み得るか（閾値を決める前のゲート）（第38期）
    dotnet run --project BattleSim -c Release 0 suture [絞り込み] # 縫いの繕い・塞ぎ・渇きの封じ／渇きの帰属（同数値対照）（第39期）
    dotnet run --project BattleSim -c Release 0 expose [絞り込み] # 曝きの発火・読み手の起動・上限の掃引（同数値対照つき）（第40期）
    dotnet run --project BattleSim -c Release 0 shove [絞り込み]  # 突き返しの発火・供給元・ウツの攻撃力の成長・Penalty 掃引（陽性対照つき）（第41期）
    dotnet run --project BattleSim -c Release 0 dull [絞り込み]   # 弱体の経路別・横取り・鎧と死蔵・排他・ArmorPerDull 掃引（陽性対照つき）（第42期）
    dotnet run --project BattleSim -c Release 0 relay [絞り込み]  # 転嫁の横取り・流し先・崖の検算・自弁率・効き・TransferPercent 掃引（陽性対照つき）（第43期）
    dotnet run --project BattleSim -c Release 0 relay kubi        # 変種Cだけを回す（萎縮との同居。主表と検算は重いので分けてある）
    dotnet run --project BattleSim -c Release 0 slander [絞り込み] # 誹りの発火・対象・横取り／素通り・攻ゼロ・早逝・Penalty 掃引・別 seed の追試（第44期・**採用しなかった**）
    dotnet run --project BattleSim -c Release 0 overbear [絞り込み] # 驕りの削り・成立率／成立時刻・2倍・横取り／素通り・逆行・攻ゼロ・Drain 掃引・席の分散（第46期・**採用しなかった**）
    dotnet run --project BattleSim -c Release 0 scale [絞り込み]  # 鱗の獲得（死/破片）・纏い率・初纏い・貫き／後列到達・二重支出・枯渇／死蔵・素体対照（第47期）
    dotnet run --project BattleSim -c Release 0 scale phase0     # 実装前の地図（キーの読み手 / 敵の範囲攻撃 / 味方の死亡数。盤面は動かさない）
    dotnet run --project BattleSim -c Release 0 scale sweep      # CostPerAttack 0/1/2 の掃引だけを回す
    dotnet run --project BattleSim -c Release 0 scale seats      # 席の分散（seats2 の写し）だけを回す
    dotnet run --project BattleSim -c Release 0 census           # 棚卸し: 員数・compare の出現数・特性の保持者一覧（戦闘0回。第48期）
    dotnet run --project BattleSim -c Release 0 scapegoat [絞り込み] # 業の引き取り・種類数・到達／成立率・転写と転写の効き・自傷／味方（第49期・**採用しなかった**）
    dotnet run --project BattleSim -c Release 0 scapegoat phase0  # 実装前の地図（味方に載る種類の分母 / 寿命 / 候補台）。戦闘は回すが盤面は動かさない
    dotnet run --project BattleSim -c Release 0 scapegoat sweep   # Threshold 2/3/4 の掃引だけ
    dotnet run --project BattleSim -c Release 0 scapegoat stun    # 痺のある台（引き取りと発揮が資源を奪い合う）だけ
    dotnet run --project BattleSim -c Release 0 scapegoat confirm # 配置の追試（seed 200..599）だけ
    dotnet run --project BattleSim -c Release 0 scapegoat alt     # 機構の帰属を別 seed 帯（200..599）で追試
    dotnet run --project BattleSim -c Release 0 divert [絞り込み] # 逸らしの発火・外し・焦点と焦点の効き・撃破順・代金の分離（第50期）
    dotnet run --project BattleSim -c Release 0 divert phase0     # 実装前の地図（**engine の窓口一覧** / MarkPullPercent の実装 / 標の現在値）
    dotnet run --project BattleSim -c Release 0 divert probe      # 採用2行を選ぶための候補探索（ソラ版 − 素体版の波ごとの帰属）
    dotnet run --project BattleSim -c Release 0 divert sweep      # TargetCount 1/2/3 の掃引だけ
    dotnet run --project BattleSim -c Release 0 divert seats      # 席の分散だけ
    dotnet run --project BattleSim -c Release 0 divert confirm    # 配置の追試（seed 200..599）。**発火が 0 の席を採らないための列つき**
    dotnet run --project BattleSim -c Release 0 divert alt        # 機構の帰属を別 seed 帯で追試
    dotnet run --project BattleSim -c Release 0 goad [絞り込み]   # 駆り立ての発火・空振り・渡した量・効き・被弾増・早逝・ソラ／ウツ干渉（第52期）
    dotnet run --project BattleSim -c Release 0 goad sweep       # Boost 0/2/4/6 の掃引だけ
    dotnet run --project BattleSim -c Release 0 goad cross       # ソラ・ウツとの同居版だけ
    dotnet run --project BattleSim -c Release 0 goad seats       # 席の分散だけ（seats2 の写し）
    dotnet run --project BattleSim -c Release 0 goad confirm     # 配置の追試（seed 200..599）。**発火／空振りの列つき**
    dotnet run --project BattleSim -c Release 0 goad alt         # 機構の帰属を別 seed 帯で追試
    dotnet run --project BattleSim -c Release 0 finisher [絞り込み] # 止めの発火・空振り・**列越え**・消費・**止めた砲火**・遊休・撃破（第53期）
    dotnet run --project BattleSim -c Release 0 finisher sweep   # Multiplier 1/2/3/4 の掃引だけ（第四波は軛の Cap で頭打ち）
    dotnet run --project BattleSim -c Release 0 finisher cross   # ザン・カリとの同居だけ（発火が動かないことの確認）
    dotnet run --project BattleSim -c Release 0 finisher seats   # 席の分散だけ（seats2 の写し）
    dotnet run --project BattleSim -c Release 0 finisher confirm # 配置の追試（seed 200..599）。**発火／列越え／止めた砲火の列つき**
    dotnet run --project BattleSim -c Release 0 finisher alt     # 機構の帰属を別 seed 帯で追試
    dotnet run --project BattleSim -c Release 0 wave2 [波番号 1-5]  # 波の解剖: 敵を1体ずつ空席にし、盤面ルールを切って関門を数える（第51期）
    dotnet run --project BattleSim -c Release 0 pace              # 勝率以外の物差し（決着T/残存数/被ダメ/与ダメ）の分離度を波ごとに比べる（第54期・調査）
    dotnet run --project BattleSim -c Release 0 audit             # docs/ の生成物が現行の編成数と整合しているか（戦闘0回・1秒。第55期）
    dotnet run --project BattleSim -c Release 0 whet [絞り込み]   # 強化の分布: 経路別・受け手・収支（Whet-Dull）・死蔵・逆しま（第56期）
    dotnet run --project BattleSim -c Release 0 burn [絞り込み]   # 燃焼の解剖: 供給・捨て率・稼働率・帰属・通貨どうしの接続（第57期・調査）
    dotnet run --project BattleSim -c Release 0 burn phase0      # 窓口の一覧と接続の地図（戦闘0回）
    dotnet run --project BattleSim -c Release 0 burn alt         # 帰属の符号を別 seed 帯（200..599）で追試
    dotnet run --project BattleSim -c Release 0 favor [絞り込み]  # 火選り: 発火・空振り・燃体/非燃体・熾火への配分・死蔵・火の粉の符号の反転（第58期。**旧 kindle**）
    dotnet run --project BattleSim -c Release 0 favor phase0     # 実装前の地図（**熾火の乗算監査**・在庫・試験行の選定）
    dotnet run --project BattleSim -c Release 0 favor sweep      # Gain / Loss の掃引（V0〜V4・素体の対照つき）
    dotnet run --project BattleSim -c Release 0 favor seats      # 席の分散（seats2 の写し）＋ **reseat 120通りの帯の形**
    dotnet run --project BattleSim -c Release 0 favor confirm    # 配置の追試（seed 200..599）。**発火／空振り／燃体／非燃体の列つき**
    dotnet run --project BattleSim -c Release 0 favor alt        # 機構の帰属と火の粉の符号を別 seed 帯で追試
    dotnet run --project BattleSim -c Release 0 turn [phase0|sweep|alt]  # 火選りを手番へ降ろす（第60期）。V0 移設前 / V1 移設（＝現行）/ V2・V3 素体
    dotnet run --project BattleSim -c Release 0 turn ratio             # 火選りの係数: 乗算の比を両 seed 帯で取る（第61期。**(6,2) は採らなかった**）
    dotnet run --project BattleSim -c Release 0 miasma [モード]  # 瘴気を手番へ降ろす（第61期・**採用しなかった**）。V0 現行 / V1 移設 / V2 周期 / V3 素体
    dotnet run --project BattleSim -c Release 0 miasma phase0    # 実装前の地図（**`OnTurnStart` の発火順**・手番を止めうる経路・出力・分母）
    dotnet run --project BattleSim -c Release 0 miasma seat      # 席の入れ替えの追試（順序の判断が消えたかの直接の証拠）
    dotnet run --project BattleSim -c Release 0 miasma mio       # 損の出どころ（ミオを素体に落とす対照。**4行中3行が床に落ちて無効**）
    dotnet run --project BattleSim -c Release 0 miasma alt       # 帰属の符号を別 seed 帯（200..599）で追試
    dotnet run --project BattleSim -c Release 0 blaze [モード] [a|b] # 火種: 破裂の着火（V0/A/B/C）・計数・層・試験行（第59期）
    dotnet run --project BattleSim -c Release 0 blaze phase0     # 実装前の地図（破裂時点の盤面・墓守の層・情報セル）
    dotnet run --project BattleSim -c Release 0 blaze main       # 既存7行の主表・計数・Q2 だけ
    dotnet run --project BattleSim -c Release 0 blaze rows       # 試験行（死軸×ホタ / 死軸×ヒヨ）だけ
    dotnet run --project BattleSim -c Release 0 blaze scan       # **120通り × 情報セル**（席は「勝つ席」ではなく「測れる席」で選ぶ）
    dotnet run --project BattleSim -c Release 0 blaze seats / confirm / alt / check
    dotnet run --project BattleSim -c Release 0 funnel [モード] # 横流し（第62〜64期・**3回測って採用しなかった。残置で確定**）。席ごとの帰属と幅
    dotnet run --project BattleSim -c Release 0 funnel phase0    # 地図（試験行 / **器具の両側 40〜95%** / **真の捨て場** / 強化の在庫 / ウツ同席 / 主判定）
    dotnet run --project BattleSim -c Release 0 funnel pick      # **試験行の選定規則のスキャン**（61行 × ablate + 5席の素体。結果を見る前に規則を固定するための道具）
    dotnet run --project BattleSim -c Release 0 funnel reseat    # 配置の粗探索＋追試（**第64期はこれを使わないと決めた**。理由は design/PHASE64_FUNNEL3.md §4-1）
    dotnet run --project BattleSim -c Release 0 funnel alt       # 同じ表を再現帯（0..199）で（第64期の主帯は 1000..1399）
    dotnet run --project BattleSim -c Release 0 spend           # 強化の使い道: 7経路を1本ずつ落として帰属・到着・使用率・受け手（第65期・調査）
    dotnet run --project BattleSim -c Release 0 spend map       # 判断の地図（**戦闘0回**。出力経路4本 / 行き先の決まり方 / 陰性対照の分母）
    dotnet run --project BattleSim -c Release 0 spend alt       # 同じ表を再現帯（seed 200..599）で
    dotnet run --project BattleSim -c Release 0 creak          # 軋みが響く: WhetReceived >= 閾値 で単体→薙ぎ（第66・67期・**2回とも採用しなかった**）
    dotnet run --project BattleSim -c Release 0 creak phase0   # 実装前の地図（ヨミの WhetReceived の分布＝格子9点の到達%と到達T / Q6 の実効レンジ / Q3' の素体対照）
    dotnet run --project BattleSim -c Release 0 creak alt      # 同じ表を再現帯（seed 200..599）で
    dotnet run --project BattleSim -c Release 0 creak3         # 自己供給 対 外部供給: `CreakSource` を Whet/Bonus/Both で振る（第77期・**3回目も採用しなかった。この駒は閉じた**）
    dotnet run --project BattleSim -c Release 0 creak3 phase0  # 紙の計算と分布（在席率・相方の在席・格子12点の到達%。**盤面は動かない**）
    dotnet run --project BattleSim -c Release 0 creak3 alt     # 同じ表を B 帯（ドラフト seed 200..207 / 理想 200..399）で
    dotnet run --project BattleSim -c Release 0 creak3 check   # 陰性対照（305 セル 0 件・ヨミを含まない 290 セル × 9 版 0 件）
    dotnet run --project BattleSim -c Release 0 creak3 ideal66 # 第66期の帯（seed 200..599）での再現確認だけ（判定基準は変えない）
    dotnet run --project BattleSim -c Release 0 carry          # 棚卸し: 駒 × キー の「外から届いた量」の在庫と格子の形（第68期・調査）
    dotnet run --project BattleSim -c Release 0 carry keys     # 表C（供給経路 × 開戦時一括／毎ターン／事象ごと の分類と、受け手の形）
    dotnet run --project BattleSim -c Release 0 carry solo     # 表D（単独成立度＝相方あり行／なし行の寄与）
    dotnet run --project BattleSim -c Release 0 carry leak     # 表E（`AcceptsSupport` の漏れ。**ガルドから Stoic だけを外した対照**）
    dotnet run --project BattleSim -c Release 0 carry check    # 陰性対照（`verbose` の有無で 305 セル 0 件）
    dotnet run --project BattleSim -c Release 0 draft          # ドラフト台: 無作為5体 × 配置2版（表A〜F）（第69期・調査）
    dotnet run --project BattleSim -c Release 0 draft phase0   # N と M を決めるパイロット（式は測る前に固定）
    dotnet run --project BattleSim -c Release 0 draft alt      # 同じ標本を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 draft check    # 陰性対照（抽選の再現性・含まれない駒の素体化で 50,000 セル 0 件）
    dotnet run --project BattleSim -c Release 0 draft2         # ドラフト台の 2×2: 編成の作り方 × 敵の強さ（第70期・調査）
    dotnet run --project BattleSim -c Release 0 draft2 phase0  # 紙の計算（**戦闘0回**。超幾何 / 抽選10万回 / 倍率の導出 / N の決め方）
    dotnet run --project BattleSim -c Release 0 draft2 alt     # 同じ標本を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 draft2 check   # 陰性対照（弱い波が HP 以外を1つも動かしていないこと）
    dotnet run --project BattleSim -c Release 0 draft3         # ドラフトの選択規則3つ（無作為/素朴/シナジー志向）× 2波（第71期・調査）
    dotnet run --project BattleSim -c Release 0 draft3 phase0  # 紙の計算（**戦闘0回**。3規則の編成の中身 / 超幾何との照合 / 理想61行の分母）
    dotnet run --project BattleSim -c Release 0 draft3 alt     # 同じ標本を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 draft3 check   # 陰性対照（規則 P が第70期の C/D と一致・S が規則どおり）
    dotnet run --project BattleSim -c Release 0 slope          # 傾きで選ぶ規則 S' ＋ キーの傾きの地図（第72期・調査）
    dotnet run --project BattleSim -c Release 0 slope phase0   # 紙の計算（**戦闘0回**。傾きの出どころ / S' の選好 / 超幾何との照合）
    dotnet run --project BattleSim -c Release 0 slope alt      # 同じ標本を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 slope check    # 陰性対照（P が第70期と一致・S' が規則どおり・傾きの定数が第71期の写し）
    dotnet run --project BattleSim -c Release 0 wound          # 傷の解剖: 在庫の収支・対照3種・代金・犯人（理想台。第73期・調査）
    dotnet run --project BattleSim -c Release 0 wound phase0   # 窓口一覧と紙の計算（**戦闘0回**。5枚のマイナスの分離可能性 / 超幾何）
    dotnet run --project BattleSim -c Release 0 wound alt      # 同じ表を B 帯（seed 200..399）で
    dotnet run --project BattleSim -c Release 0 wound draft [alt]  # ドラフト台（Pw と S'w・**弱い波**）の傾きの分解
    dotnet run --project BattleSim -c Release 0 wound check    # 陰性対照（素体・裂き二重が供給だけを動かすことの1戦監査）
    dotnet run --project BattleSim -c Release 0 wcost          # 傷の代金の分離: 5つのマイナスを1つずつ外す ＋ 断ちの待ち方（理想台。第74期）
    dotnet run --project BattleSim -c Release 0 wcost phase0   # 分割の一覧と紙の計算（**戦闘0回**。V1 が動かす分母を先に数える）
    dotnet run --project BattleSim -c Release 0 wcost check    # 受け入れ基準（`compare` 305 セルの突き合わせ・代金なし版の1戦監査）
    dotnet run --project BattleSim -c Release 0 wcost alt      # 同じ表を B 帯（seed 200..399）で
    dotnet run --project BattleSim -c Release 0 wcost draft [alt]  # ドラフト台 Pw（**主判定はこちら**。理想台はナタの行が1つしか無い）
    dotnet run --project BattleSim -c Release 0 blade          # 薄刃の払い方: 代金を条件付きにする4版（理想台。第75期・**採用しなかった**）
    dotnet run --project BattleSim -c Release 0 blade phase0   # 窓口の一覧と紙の計算（**戦闘0回**。call-site 全数 / 敵の速さ / 解除率の見積り）
    dotnet run --project BattleSim -c Release 0 blade check    # 陰性対照（305 セル 0 件・キリを含まない行 0 件・1戦の監査）
    dotnet run --project BattleSim -c Release 0 blade alt      # 同じ表を B 帯（seed 200..399）で
    dotnet run --project BattleSim -c Release 0 blade draft [alt]  # ドラフト台 Pw（**主判定はこちら**）
    dotnet run --project BattleSim -c Release 0 body           # 体の解剖: 回帰3段・群の説明力・数値を揃えた組・残差（第76期・調査）
    dotnet run --project BattleSim -c Release 0 body phase0    # ロスターの分布と紙の計算（**戦闘0回**。回帰の設計を測る前に固定する）
    dotnet run --project BattleSim -c Release 0 body alt       # 同じ表を B 帯（seed 200..207）で
    dotnet run --project BattleSim -c Release 0 body wound [alt]  # 表E（−7.2 の分解。傷の5枚を1枚ずつ素体に）
    dotnet run --project BattleSim -c Release 0 traits         # 特性の数の分解: 特性数/発火口/入口/キー の識別力（第78期・調査）
    dotnet run --project BattleSim -c Release 0 traits phase0   # 表A と相関行列（**戦闘0回**。器具の定義を測る前に固定する）
    dotnet run --project BattleSim -c Release 0 traits alt      # 同じ表を B 帯（ドラフト seed 200..207 / 理想 200..399）で
    dotnet run --project BattleSim -c Release 0 lastslot        # 最後の1枠: 首刈りのオノを両方の台で測る（第79期・**採用しなかった**）。A 帯
    dotnet run --project BattleSim -c Release 0 lastslot phase0 # 空白の地図・候補の選定規則・数値・試験行の選定（在席時勝率と理想台の帰属のぶんだけ戦闘を回す）
    dotnet run --project BattleSim -c Release 0 lastslot seats  # 単騎・軸あり の配置（reseat 120通り → confirm seed 200..599・撃破/戦の列つき）
    dotnet run --project BattleSim -c Release 0 lastslot check  # 受け入れ基準: `compare` 305 セルの突き合わせ（`All` は 51 のまま）
    dotnet run --project BattleSim -c Release 0 lastslot alt    # 同じ表を B 帯（ドラフト seed 200..207 / 理想 200..399）で
    dotnet run --project BattleSim -c Release 0 pairs           # 組み合わせの表: 51 体・1,275 組の 単独/組/相乗（在席差・A/B 両帯・約 4 分）（第80期・調査）
    dotnet run --project BattleSim -c Release 0 pairs phase0    # 紙の計算（**戦闘0回**。抽選だけ回して組ごとの在席標本数・SE の目安・測れる組の割合）
    dotnet run --project BattleSim -c Release 0 pairs check     # 受け入れ基準: `compare` 305 セルの突き合わせ
    dotnet run --project BattleSim -c Release 0 pairs2          # 組の 2×2: 同じ台で A/B の中身だけを4通りに振る（全 1,275 組 × 128 台）（第81期・調査）
    dotnet run --project BattleSim -c Release 0 pairs2 phase0   # 紙の計算（**戦闘0回**。予算・群の大きさ・台の抽選・**2系列が編成を共有しないこと**）
    dotnet run --project BattleSim -c Release 0 pairs2 run <skip> <take>  # 組の区間だけを測って TSV で吐く（**分割実行。1回で全部回すとメモリ不足で落ちる**）
    dotnet run --project BattleSim -c Release 0 pairs2 tables <path>      # `run` の TSV を連結して渡す（表A〜F・Q1〜Q5・20 秒）
    dotnet run --project BattleSim -c Release 0 pairs2 check    # 受け入れ基準: `compare` 305 セルの突き合わせ
    dotnet run --project BattleSim -c Release 0 checkup phase0   # ロスター健康診断（第82期・調査）。紙の計算（**戦闘0回**。線・拒否権の紙で出る2本）
    dotnet run --project BattleSim -c Release 0 checkup run <skip> <take>  # 2×2 を測って TSV（**分割実行**。先頭 9+NT 列は `pairs2 run` と同一）
    dotnet run --project BattleSim -c Release 0 checkup tables <a> [<b>]   # 51 体を 残す/転生/差し替え に3分（表A〜F・Q1〜Q6。<b> は `pairs2 run` の TSV）
    dotnet run --project BattleSim -c Release 0 checkup ideal    # 理想台の帰属だけ（`CompareBuilds()` 61 行 × 在席枠 305 か所・素体差し替え）
    dotnet run --project BattleSim -c Release 0 checkup check    # 受け入れ基準: `compare` 305 セルの突き合わせ
    dotnet run --project BattleSim -c Release 0 breadth phase0 [<TSV>]  # 物差しの引き直し（第83期・調査）。紙の計算（**戦闘0回**。共有しない組の数・床の分布・埋め草の予算）
    dotnet run --project BattleSim -c Release 0 breadth run <skip> <take>  # **埋め草を無作為3枚にした版**の 2×2（§2-5・分割実行。列は `checkup run` と同形式）
    dotnet run --project BattleSim -c Release 0 breadth tables <a> [<b>] [<c>]  # 表A〜G・Q1〜Q7（<a> は `checkup run` / <b> は `pairs2 run` / <c> は `breadth run` の TSV）
    dotnet run --project BattleSim -c Release 0 breadth check    # 受け入れ基準: `compare` 305 セルの突き合わせ
    dotnet run --project BattleSim -c Release 0 thorn phase0     # 棘に傷を載せる（第84期・**測って採用しなかった**）。窓口の全数・反撃の発火・粛・交わり
    dotnet run --project BattleSim -c Release 0 thorn run <v> [skip] [take]  # 2×2（v = 0 現行 / 1 敵に傷 / 2 味方にも）。50 組 × 128 台・約 40 秒/版。TSV を標準出力へ
    dotnet run --project BattleSim -c Release 0 thorn tables <v0> <v1> [<v2>]  # 表A〜E・Q1〜Q4（`run` の TSV を渡す）
    dotnet run --project BattleSim -c Release 0 thorn check      # `compare` 61 行の V0/V1/V2 突き合わせ・拒否権1・2・`docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 suture2 phase0   # 糸を味方にも通す（第85期・**測って採用しなかった**）。**紙のスループット**（停止条件）・窓口の全数・交わり
    dotnet run --project BattleSim -c Release 0 suture2 run <w> [skip] [take]  # 2×2（w = 0 現行 / 1 両側・カドだけ / 2 両側・巻き込み則）。50 組 × 128 台・約 50 秒/版
    dotnet run --project BattleSim -c Release 0 suture2 tables <w0> <w1> <w2>  # 表A〜F・Q1a/Q1b/Q2/Q3/Q5・自己検査（`run` の TSV を渡す）
    dotnet run --project BattleSim -c Release 0 suture2 nata     # Q4 の専用台（カド × ハリ × ナタ ＋ 埋め草2枚・16 台。`CompareBuilds()` は触らない）
    dotnet run --project BattleSim -c Release 0 suture2 check    # `compare` 61 行の W0/W1/W2・拒否権1・2・(e)・`docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 mender phase0    # 繕いに傷を読ませる（第86期・**紙で停止した。2×2 は1戦も回していない**）。紙のスループット・窓口の全数・交わり
    dotnet run --project BattleSim -c Release 0 mender run <x> [skip] [take]  # 2×2（x = 0 現行 / 1 巻き込み則・全 / 2 吸い・余波だけ）。**未実行**
    dotnet run --project BattleSim -c Release 0 mender tables <x0> <x1> <x2>  # 表A〜F・Q1〜Q5・副判定 (A)。**未実行**
    dotnet run --project BattleSim -c Release 0 mender check     # `compare` 61 行の X0/X1/X2/**X1P**・拒否権1〜3・(e)・`docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 blaze2 phase0    # 傷口に毒を流す（第87期・**測って採用しなかった**）。紙のスループット・持続係数・交わり
    dotnet run --project BattleSim -c Release 0 blaze2 run <y> [skip] [take]  # 2×2（y = 0 現行 / 1 着火）。50 組 × 128 台・約 45 秒/版
    dotnet run --project BattleSim -c Release 0 blaze2 ideal      # 理想台4台（副判定 (C) の分子）。**`CompareBuilds()` は触らない**
    dotnet run --project BattleSim -c Release 0 blaze2 tables <y0> <y1>  # 表A〜G・Q1〜Q4・自己検査
    dotnet run --project BattleSim -c Release 0 blaze2 check      # `compare` 61 行の Y0/Y1・拒否権1〜3・`docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 gauge phase0     # 物差しを直す（第88期）。ノブの既定・各期の A と B・2×2 の定数（**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 gauge run <p> <v> [skip] [take]  # 2×2 を TSV へ（p = 84/85/87・v = 0/1・分割実行・約 40 秒/版）
    dotnet run --project BattleSim -c Release 0 gauge null <87v0.tsv>  # 陰性対照（ノイズ床）。**§5 より先に回す。追加費用ゼロ**
    dotnet run --project BattleSim -c Release 0 gauge redo <p> <v0> <v1> <null>  # 1機構ぶんの再検定
    dotnet run --project BattleSim -c Release 0 gauge tables <6つの TSV>  # 表A〜F（台の内訳 / ノイズ床 / 3機構 / 判定の異同）
    dotnet run --project BattleSim -c Release 0 gauge ideal       # 理想台にも情報帯を当てる（副判定 (C)）
    dotnet run --project BattleSim -c Release 0 gauge veto <p> <v>  # 拒否権（`compare` 61 行）
    dotnet run --project BattleSim -c Release 0 gauge check       # `docs/balance.md` との 305 セル
    dotnet run --project BattleSim -c Release 0 gather redo87    # 傷も肩代わりする（第89期・**紙で止めた**）。(P1) 第87期を別標本で再判定
    dotnet run --project BattleSim -c Release 0 gather run87 <v> [skip] [take]  # (P1) の 2×2 を TSV へ（分割実行）
    dotnet run --project BattleSim -c Release 0 gather seats     # (P2) 席。**`confirm` ではなく `docs/reseat.md` から生きた判定を取る**
    dotnet run --project BattleSim -c Release 0 gather phase0    # 窓口の確認と**紙のスループット**（停止条件）
    dotnet run --project BattleSim -c Release 0 gather ideal     # 源・中継・終端を手でそろえた台（**紙で止まった原因の切り分け**）
    dotnet run --project BattleSim -c Release 0 gather check     # 自己検査（Z0/Z1 の `compare` 61 行・`docs/balance.md` との 305 セル）
    dotnet run --project BattleSim -c Release 0 soak redo89      # 傷口から滲む（第90期・**採用**）。(P1) 第89期を門を外して再判定
    dotnet run --project BattleSim -c Release 0 soak phase0      # 表B（毒を書く箇所の全数）・**門（鎖が繋がっているか）**・表C（紙）
    dotnet run --project BattleSim -c Release 0 soak ideal       # 理想61行で W0 対 W1（Q2/Q3/Q4・副判定・拒否権・全セル差分）
    dotnet run --project BattleSim -c Release 0 soak run <w> <a> [skip] [take]  # 2×2 の TSV（a = kiri / nomi / gald・分割実行）
    dotnet run --project BattleSim -c Release 0 soak tables <TSV...>  # 表D（主判定）
    dotnet run --project BattleSim -c Release 0 soak check       # 自己検査 (a)〜(j)
    dotnet run --project BattleSim -c Release 0 soak phase0b     # 第91期: 表A（**軸交差の行数**・各駒の「他の行」）・門・紙
    dotnet run --project BattleSim -c Release 0 soak split       # `compare` を V0 / Vc / Vp の3版で（表B・C・**壊れか制約かの分解**）
    dotnet run --project BattleSim -c Release 0 soak run2 <a> [skip] [take]  # 2×2 の TSV（**V0 と Vp を両方**吐く・分割実行）
    dotnet run --project BattleSim -c Release 0 soak foe         # **敵側をローカル台で測る**（第90期の「発火 0.0%」は台の欠陥だった）
    dotnet run --project BattleSim -c Release 0 soak tables2 <TSV...>  # 表D（**(G3) によりフィルタ無しが主判定**）
    dotnet run --project BattleSim -c Release 0 soak check2      # 第91期の自己検査
    dotnet run --project BattleSim -c Release 0 cross phase0     # 交差帯（第92期）。交差の空白表・55組×61行・選定規則の適用（**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 cross quality > docs/crossing.md  # 交差帯 12 行の勝率表と品質（Q1〜Q3・席）
    dotnet run --project BattleSim -c Release 0 cross redo <84|86>  # 第84期（`ThornRule`）／第86期（`MendRule`）の再判定
    dotnet run --project BattleSim -c Release 0 cross check       # 自己検査（`compare` 305 セル・規則の既定・`PickOne`）
    dotnet run --project BattleSim -c Release 0 deep phase0       # 深手（第93期・**測って採用しなかった**）。表A（門・傷を書く箇所の全数・紙）
    dotnet run --project BattleSim -c Release 0 deep run <a> [skip] [take]  # 2×2 の TSV（a = nomi / kiri。**W0 と W1 を両方**吐く・分割実行）
    dotnet run --project BattleSim -c Release 0 deep tables <TSV...>  # 表C（主判定。フィルタ有無の両方）
    dotnet run --project BattleSim -c Release 0 deep cross        # 表B・D・E（理想61行・(G2) の壊れ／制約・交差帯12行）
    dotnet run --project BattleSim -c Release 0 deep foe          # 表A'（**敵側をローカル台で測る**。消費型の読み手を入れた対照つき）
    dotnet run --project BattleSim -c Release 0 deep check        # 表F（自己検査 (a)〜(j)）
    dotnet run --project BattleSim -c Release 0 derive rules > docs/rules.md  # ノブ一覧を生成する（第94期・**戦闘0回**）
    dotnet run --project BattleSim -c Release 0 derive scan       # 手で写した表を、走らせて観測した事実と突き合わせる（4秒）
    dotnet run --project BattleSim -c Release 0 derive check      # 自己検査（`compare` 305 セル・交差帯12行・印の全列挙）

`census` は**駒と通貨の対応表を作るための素材**を出す（第48期）。**戦闘を1回も回さない。**
ロスターの上限を **52体**（トランプ1組）と決めたので、新規追加の合否テストに
「どの通貨の空白を埋めるか」を足す必要が出た。表そのものは design/PHASE48_CENSUS.md に手で書く
——**通貨の書き手/読み手の判定は grep と目視でやる**（Trait に属性を足すと、判定の根拠が
「誰かが属性を正しく付けたか」に化けて grep で検算できなくなる）。

**残り枠は 1**（第64期時点も変わらず。`UnitCatalog.All` は 51 体＝ソラ・カリ・トメ・ヒヨを含む。上限は 52）。
**その1枠に充てられる素材は4枚ある**——オゴ（第46期）・ゴウ（第49期）・**ヌキ（第62〜64期に3度落ち、第64期で残置が確定した。再測定は提案しない）**・**オノ（第79期。空白の地図から規則で選んで両方の台で測り、帰属 +1.2〜+1.5pt で線 +1.5 に届かず採らなかった）**。
どれも測って棄却し、**定義だけを対照として残置してある**（`All` には載っていない）。
**第48期の「残り 5 / 分母 47」は古い**——第50期にソラ、第52期にカリ、第53期にトメが入った。

**通貨の厚みは駒で数える。CLAUDE.md の「毒 6書き/4読み」等は呼び出し箇所の数で、単位が違う。**
駒で数えると 毒 5/4・標 **3/2**（第52期にカリが2枚目の書き手／第53期にトメが2枚目の読み手）・痺 5(+敵3)/1・燃 **2/2**（第58期にヒヨ＝旧フイが2枚目の読み手／**第59期にゾトが2枚目の書き手**）・IdleTurn 0/3・破片 3/1・傷 4/4・
弱体 5(+敵1)/3・位置 4(+敵1)/4・死 9/5。**空白は IdleTurn の1つだけ**（書いているのは engine
なので「駒が供給できない」の意）、**飽和は 毒・痺・弱体・位置・死の5つ**。
**傷の読み手は第92期に 5 枚になった**（エグ・ナタ・ハリ・ミオ・**ノノ**）——
繕いの傷読み（`MendRule`）の採用で、**味方の傷の読み手が 1 枚（ハリ）から 2 枚になった。**

**主表の判定式を `Traits.cs` だけの grep で書くと5件落ちる。** 分かちのなまり（弱体の書き）・
引き受けの横取り（弱体の読み）・渡しの転嫁（読み＋書き）・据えの `IdleTurn` の読み・
引き受けの破片の書きは**すべて `BattleEngine.cs` にある**——駒ごとのフックでは書けない機構は
engine に窓口がある。**次に同じ表を作るときは engine 側の窓口一覧を先に固定すること。**

**切れない駒**（ある通貨の唯一の書き手か唯一の読み手）。**寄与（`ablate`）が低くても切れない。**
第48期の「6体」は**古い**——標は第52期にカリ（2枚目の書き手）・第53期にトメ（2枚目の読み手）が、
燃は第58期にヒヨ（2枚目の読み手）・**第59期にゾト（2枚目の書き手）**が入ったので、
**相互唯一のペアは1組も残っていない。**
現在も唯一なのは **シガ（痺の唯一の読み手）／ウロ（破片の唯一の読み手）の2体。**
**ボルグは第59期に唯一ではなくなった**（ゾトの破裂が着火する）。
ホタ・ヒサ・ザンも唯一ではないが、**ホタは燃の 52.3pt の帰属を単独で持つ**ので値段は別の話。

**`All` の47体は全員が `CompareBuilds()` の50行のどこかに出ている**（表D は空）。
事実上リストラされているのは**敵側の21体**（`Stages` に一度も出ていない）。
**敵側の読み手は10通貨すべてで 0**——敵は撒くだけで一度も読まない（ボス設計の論点）。

`seats2` は**駒を単位に**席を測る（第45期）。`reseat` が「この編成をどう置くか」を測るのに対し、
`seats2` は**同じ駒が編成をまたいで別の席に行くか**を測る。**盤面は1つも動かさない**
（`Traits.cs` / `UnitCatalog.cs` / `Stages` / `CompareBuilds()` に差分ゼロが受け入れ条件）。
探索は `reseat` の写しで、**検証プールに粗探索の最下位を1つ足してある**だけ
——`幅`（1位と最下位の差）を 200 seed で測るために要る。実測 8.4 秒/行・全48行で約7分。

**`reseat` の 1位 を採否の根拠にしてはいけない**（第45期の実測）。48行の実測で
**1位と最下位の差は中央値 44.4pt** ある一方、**1位と5位の差は 2.15pt**しかなく、
**その1位は別 seed 帯（200..599）では 48行中 28行で入れ替わる**。
席の値段のほとんどは「やってはいけない置き方」の回避で、良い置き方どうしは平坦な面。
**入れ替わるのは角どうしなので「次数」の割り当ては安定している**（追試一致率 98%）
——採否に使うなら1位の配置ではなく**次数**を使う。

**隣接を「隣に何人いるか」で読む機構は、配置の判断を生まない**（第45期）。
編成5枠の次数は **{2, 4} の2値しかない**ので、隣接数の単調関数は2値の選好しか返せず、
**符号が決まった時点で席が決まる**。実測でも 3値（前角/中央/後角）の最頻率は
**隣接を読む駒 85% 対 隣接も列も読まない駒 65%** で、**隣接を読む駒のほうが席が固定されている。**

    コスト型（ボルグ 巻き込み+火の粉 / リィカ 生贄 / スィド 毒漏れ）  → 角。最頻率 100%
    利得型（カド 棘守り+棘）                                → 中央。中央率 82%
    非単調（ヒサ 囃し立て＝隣で最大HPの1体を選ぶ）              → 最頻率 62%（対照と同水準）

**ロスターで隣接を非単調に読むのはヒサ1枚だけで、その1枚だけが対照と同じだけ分散した。**
n=1 なので断定はできないが、**次に隣接機構を作るなら「隣に何人いるか」ではなく
「隣に誰がいるか」を読ませること。** 経緯は design/PHASE45_ADJACENCY.md。

**新しい隣接機構は最初から2行以上に入れること。** 第41〜43期のハネ・ウケ・ワタは
**3枚とも compare に1行しか無く**、`reseat` は編成内の席しか振らないので、
**「編成によって席が変わるか」が原理的に観測できなかった**——
3期連続の「配置の判断が生まれない」は、**測っていない量についての読み**だった。

**隣接する味方「全員」を条件にする機構は、この盤面では必ず単調になる**（第46期・驕り＝棄却）。
編成5枠では**中央の隣接集合がすべての角の隣接集合の上位集合**なので
（前1={中央,後1} / 前3={中央,後3} / 後1={前1,中央} / 後3={前3,中央} ⊂ 中央={前1,前3,後1,後3}）、
「隣接全員が P」＝集合の AND ＝**最大値／最小値の判定**は**部分集合に対して必ず緩い**。
**条件の厳しさが次数の単調関数になることが盤面の形から従う。**
「隣接数が増えるほど条件が厳しい」は**単調性の向きを逆にしただけで単調性を壊していない**
——実測でも2行とも最適席は角（次数2）・上位5通りは中央0/角5・3値の最頻率 **100%**
（第45期の 隣接85% / 対照65% / ヒサ62% の中でいちばん固定されている側）。
**例外は召喚枠**（○中1={前1,後1} / ○中3={前3,後3} は角にしか隣接しない）。

**非単調にしたければ「統計量を閾値で読む」のではなく「統計量を達成した1体に効果を当てる」。**
囃し立て（ヒサ）も隣接の統計量（最大HP）を取るが、**その駒に標的を付ける**ので
当たったのが壁か脆いかで符号が変わる。驕りは最大攻撃力を**自分の攻撃力と比べた**ので、
**隣接集合が1つのスカラーに潰れ**、そのスカラーは集合について単調だった。
経緯は design/PHASE46_OVERBEAR.md。

**「自分より弱い味方に囲まれていること」を条件にしない**（第46期）。ロスターでは
**攻撃力と出力が強く相関している**（弱体・毒・支援の駒は軒並み攻2〜9）ので、
その条件を満たす編成は**出力を捨てた編成**になる——試した5通りが全部 20.0〜23.2%
（「台が死んでいる」の症状）。抜け道は攻撃力を経由しない出力（破裂・胞子・墓守）だけだった。
**条件を満たす席は勝てない席でもある**（成立率 52.3% の席 58.0% 対 成立率 0.0% の席 86.3%）。

**「隣接を読まない」は席を分散させる十分条件ではない**（第47期）。鱗（ウロ）は隣接を1つも読まないが、
3値の最頻率は **死軸台 100% / ヒビ台 60%** と2行が正反対に出た（第45期の 隣接85% / 対照65% / ヒサ62%）。
席を決めているのは隣接ではなく**列**で、ウロ1枚だけを振ると
**前列 45.9〜56.2% 対 後列 76.8〜93.3%**（次数 2/4/2/2/2 とは相関しない）。
振り回数が 1.66〜2.58 対 4.12〜4.24 で、**前列に置くと早く落ちて振れない**のが実体。
**席を分散させるのは「量を読まないこと」ではなく「行と列のどちらにも依存しないこと」。**

**「性質を切るノブ」は律速の項に付けないと強度ノブに落ちる**（第47期）。
`ScaleRule.CostPerAttack` は `0` で維持型・`1` 以上で消費型になる**性質のノブ**のつもりだったが、
掃引の全幅は 0.9pt（第41期の `ShoveRule` と同じ結末）。
理由は収支で、**支出の 83〜91% は被弾側**（攻 1.9 : 被 15.6）——攻撃の消費を 0 にしても
纏い率は 35.8 → 40.1% にしか伸びない。**ノブを付ける項を、実装前の収支計算で選ぶこと。**

**「その効果だけを 0 にできるノブ」が作れない機構では、同数値・特性なしの素体を対照に置く**（第47期）。
`ScaleRule(0)` は消費を止めるだけで供給も発揮も止めないので、
「機構が効いたのか、ただ 70/9/7 の体が入っただけか」が割れない。
**規則にノブを増やさず、駒の側で塞ぐ**（診断のローカルの `UnitDef`。`gradient` / `aim` と同じ扱い）。
実測で機構の帰属は +11.7pt / +16.2pt、体の値段は +26.0 / +31.6 で、**価値の3分の2は体**だった。

**アーマー（`StatusKeys.Armor`）は読み手が0枚だった唯一のキーで、第47期に鱗が最初の読み手になった。**
7キーの内訳は 毒 6書き/4読み・標 **3/2**（第52期にカリ／第53期にトメ）・痺 7/1・燃 1/1・IdleTurn 3/2・**破片 2/0**・傷 4/4。
供給は 砕け（ヒビ・**貫きも「範囲」に入る**）／集約（ウケ）／鱗（味方の死・+4）の3つ。
**`AcceptsSupport` を見ない**（砕けと揃える。damage 側の資源なので弱体の窓口の作法には従わない）。
**破片で受け切った被弾は `OnDamaged` を呼ばない**ので、ウロの隣に被弾強化を置くと
**3.21 回/戦ぶんの収入が消える**（アーマーが元から持つ性質だが、読み手ができて初めて編成の判断になる）。
経緯は design/PHASE47_SCALE.md。

**engine も通貨の読み手である。通貨を数えるときは駒の `Trait` と engine の窓口を両方数える**
（第50期に一覧を固定した。第48・49期の残件B）。engine 側の窓口を持つ通貨は **9 / 10**（傷だけが持たない）:

    標    BattleEngine.cs:1295  SelectTargetChain。MarkPullPercent(75)% で主目標を標持ちへ差し替える
          （第53期に**止め＝トメがこの段を 100%・決定的にする**。窓口は増やしていない）
    痺    :2269 / :137          行動順ループが手番を飛ばす ／ CanActOutOfTurn がターン外の行動を止める
    毒    :174                  TickStatuses が層の分だけ削る
          **第89期に入口が2つになった**——澱み（ミオ）が「傷を持ち毒を持たない敵」に毒 1 を置く（`IgniteRule`）
          **第90期に加算の窓口ができた**——`BattleContext.Poison`。滲み則（`SoakRule`）が
          「相手が傷を持つなら層 +1」を足す唯一の場所で、**通るのは加算の入口5箇所だけ**
          （瘴気の敵／味方漏れ・毒撃の相手／隣への漏れ・疫み）。**上書き2（ミオ）と減算2（ベニ・ヴィオ）は通さない**
    燃    :203 / :229           TickStatuses が 6 削る ／ Ignite が残ターンを設定
          **第90期に滲み則をここに置き、第91期に外した**——相手が傷を持つなら戻す先を 4 にする版は
          **1.65 回/戦 発火して紙の 2% しか払い出さない**のに `compare` の 9 行を下げていた
          （`SoakRule.Burn` は既定 `false` で対照として残置）
    破片  :1650                 ApplyDamage。HP の前に削られ、受け切ると OnDamaged を呼ばない
    IdleTurn :1519              据えの被ダメ半減の判定
    弱体  :1891                 Dull が唯一の窓口
    位置  :2100                 SwapSlots が OnMoved / OnAllyMoved を流す
    死    :1783                 OnKill → OnDeath → OnAnyDeath → OnAllyDeath の固定順
    傷    :ApplyDamage           **巻き込み則**（味方の刃が通ると傷 1。第85期に作り第88期に既定化）
          ——**第88期まで engine の窓口を持たない唯一の通貨だった。今は 10 / 10 が持つ**
          **第90期に読み手側の窓口が2つできた**（`Poison` と `Ignite` の滲み則）
          ——**engine が傷を「読む」のはこれが初めて**
          **第93期に加算の窓口ができた**——`BattleContext.Wound`。**加算6箇所だけ**を通す
          （裂き／刻み／巻き込み則／棘の傷／棘の余波／引き取りの<u>受け取る側</u>）。
          **減算3（引き取りの donor 側・縫いの塞ぎ・繕いの塞ぎ）と上書き1（断ちの 0 戻し）は通さない。**
          読み手側は `WoundDepthOf` / `IsWounded` に集約した（深手を「傷1つぶん」として読む）

**標（`Marked`）は engine の鎖の中で唯一「前列が生きている限り後列は狙われない」を破る**（第50期）。
`SelectTargetChain` の標の段は **`foes` から選んでいる（`pool` ではない）**ので、後列の標持ちは
前列を飛び越して狙われる。**執着・断ちの選好は `pool` から選ぶので破らない。**
しかも**鎖の順序は 標 → 後備え → 庇う → 殉教 → 棘守り で標がいちばん先**——
引いた瞬間に `return` するので、**標は庇い・後備え・殉教をすべて飛び越す。**
`MarkPullPercent = 75` は確率だが `marked != target` の但し書きがあるので実効は
**`1/n + (1 − 1/n) × 0.75`**（実測で味方の単体振りの 81.9〜85.8% が標持ちへ）。
**単体攻撃にしか効かない**（薙ぎ・全体・貫きは標を1ビットも見ない）。

**反撃役にとって標は「収入」でもあり「死因」でもある**（第50期・残件Aの解明）。
カドから標を外すと**寿命 +0.96T / 干渉 −2.16 回**で、
**第四波（単体攻撃ばかり・断罪なし）は −21.5pt / 第五波（断罪2体・範囲3枚）は +30.5pt**
——**符号は波の攻撃型の構成で決まる**（別 seed 帯で −17.3 / +20.0 と再現）。
**この反転はカド固有**で、カドを含まない台では一様に +25.6〜+27.2pt だった。

**「矢面に立つ」は肩代わり役と組んだ瞬間にプラスへ反転する**（第50期）。
逸らしの「自分に標を付ける」はマイナスとして設計したのに、外すと **−3.9 / −33.3pt** と弱くなった。
味方から標を外すだけでは敵の攻撃は散るだけで減らないが、1体に集めればそこを守れば済む
——実測で敵の単体振りの 73.2% がソラへ向き、その大半を巨躯（ゴルム）が肩代わりする。
**肩代わりは5種あるので、標を集める駒はその数だけ組み合わせを持つ。**

**標の符号は「読み手がいるか」で決まる**（第52期。第50期の一般形）。駆り立て（カリ）が
味方に付ける標の寄与（`標なし` 版との差）は、**仇討ち（ザン）が同席する台で +36.8pt・
同席しない台で −10.0pt**（どちらも別 seed 帯で再現）。**肩代わりに限らない**
——標を「浴びる量」に変換できる駒が同席していれば符号は正になる。
変換器は**肩代わり5種 + 仇討ちの6枚**で、**仇討ちだけが「浴びた側ではなく第三者」を出力にする。**

**標の読み手が2枚になった（第53期）。止め（トメ）は engine の標の段を 100%・決定的にする。**
書き手3（ヒサ／ソラ／カリ）・読み手2（ザン＝**味方**の標／トメ＝**敵**の標）で、
**engine に新しい窓口は1つも足していない**——実装は `SelectTargetChain` の標の段で
`PickOne(標持ち)` を `FinisherTrait.Preferred`（現在HP最大・同値のみ `PickOne`）に差し替え、
`Roll(100) < MarkPullPercent` を飛ばすだけ。倍率は `PerformAttack` が `atk` を作った直後に掛ける
（**`Trait.ModifyAttack` は対象を受け取らない**ので「相手が標を持つか」で分岐できない）。
消費（殴った敵の標を 0 に）だけが駒側の `OnAfterAttack` にある。

**対象強制を執着・断ちと同じ窓口（`pool` から選ぶ直前）に置いてはいけない機構がある**（第53期）。
`pool` は前列が生きている限り前列しか含まないので、**標のように「列を無視する」性質が主眼の機構を
そこに置くと、主眼が構造的に消える。** 標の段も `SelectTargetChain` の中にある既存の窓口なので、
**「窓口を新設しない」は守れる。どの窓口かを候補集合で選ぶこと。**
なお**責め苦（シガ）は対象を強制しない**（`OnAfterAttack` の分岐だけ）——強制の前例は
執着（ノミ）と断ち・縫いの選好（ナタ・ハリ）の2つだけ。

**後列到達の経路は4本（第53期に4本目が付いた）。4本目だけが事前に読める。**

    前から割る / 貫き / 標（engine・75%・PickOne で無作為） / **止め（100%・決定的）**

止めの発火の **49.9% / 38.0%** が「標が無ければ狙えなかった敵」で、そこに並ぶのは
**盤面ルール駒ばかり**（粛の伝令 0.66 / 渇きの祭司 1.04 / 軛の重装兵 2.60 / 告発人 0.29 回/戦）。
ソラが「現在HP最大の敵」を、止めが「標持ちのうち現在HP最大」を選ぶので**2段とも決定的**
——**プレイヤーが誰に届くかを事前に読める後列到達はこれが初めて。**

**供給が1枚しかない機構で「使うと消える」代金を作ると、代金の宛先は味方ではなく自分になる**（第53期）。
止めの消費は「標を消す ＝ engine の `MarkPullPercent` が切れる ＝ 味方の集中砲火を自分で終わらせる」
として設計したが、**味方が薙ぎばかりの台では `止めた砲火` が 0.00 回/戦になるのに代金は −5.1pt 残った**。
理由は**消費なし版で発火が 1.66 → 1.94・列越えが 0.63 → 0.81 と増える**こと
——**消費は味方の焦点を削ると同時に、自分の次の発火を削っている**（トメはソラの供給の 83〜90% を食う）。
**味方への代金は編成で消せる（薙ぎで固める）が、自己消費は消せない。**

**標は溜まらない**（第53期。遊休 0.00〜0.04T）。供給（ソラの `OnTurnStart`）は行動順ループの
外側で走り、止めは同じターンに殴るので**「供給 → 消費」のサイクルが1ターンの中で閉じる。**
第37期の断ち（傷の在庫）は「溜まる時間を作らないと溜まらない」で詰まったが、
こちらは**溜める余地が構造的に無い。**

**「1発の重さ」に課金する波は、倍率型のノブの上限を数値で決めてしまう**（第53期）。
`FinisherRule.Multiplier` の掃引は全幅 **39.1 / 32.9pt**（単調・第52期の分類の (d) 型・過去最大）だが、
**第四波だけ M3 以上で完全に頭打ち**になる（44.5 → 46.0 → 46.0 / 43.0 → 43.0 → 43.0）。
軛（`YokeTrait.Cap` = 25）が1回のダメージを切るので、攻12 では **M2 = 24 が上限の1下をすり抜ける
最大値**で M3 = 36 は 25 に切られる。**実効の上限は `Cap ÷ 素の攻撃力`（この駒で 2.08 倍）。**
**次に倍率型のノブを作るときは、この比を実装前に計算すること。**

**同じ通貨でも、陣営と手番の持ち方が違えば別の機構として並立する**（第53期。第51期の一般則の実例）。
ザン（**味方**の標・`CanActOutOfTurn` を通る＝**ターン外**）は粛に封じられ、
トメ（**敵**の標・自分の手番の `PerformAttack`）は**粛の非対象**
——第二波の勝率は **ザンの行 15.0% / 30.5% 対 トメの行 90.0% / 97.0%**（第二波でも発火 3.35 / 1.78 回/戦）。
標には**5枚が互いに干渉せずに乗っている**（ヒサ・カリが味方に書く／ザンが味方を読む／
ソラが味方を消して敵に書く／トメが敵を読んで敵を消す）——実測でも
**ザン同居 2.23・カリ同居 2.24・対照 2.30 と止めの発火が動かない。**

**席に依存しない駒でも、席が自由になるわけではない。空いた席は他人のものだから**（第53期）。
トメは隣接も自分の列も読まないのに、2行の 2値（前列/以外）は **0/5 と 4/1** で正反対に出た。
`止め` は素直（後列ほど発火 2.34 → 3.78・勝率も高い＝第47期のウロと同じ「前列だと早く落ちて振れない」）
だが、`止め改` は**発火がいちばん多い後3 が勝率は最下位**（1.92 回 / 45.9%）
——後列はドルガ（薙ぎ38）とボルグ（薙ぎ18）の指定席で、**トメを後ろに置くとその2枚が前へ出て先に死ぬ。**
第47期の「席を分散させるのは『量を読まないこと』ではなく『行と列のどちらにも依存しないこと』」に足す。

**標の代金は絶対値ではなく差分。「素体でどれだけ狙われていたか」を先に測る**（第52期）。
標の代金は engine の鎖が「前列が生きている限り後列は狙われない」を破る分だけなので
（`SelectTargetChain` の標の段は `pool` ではなく `foes` から選ぶ）、
**もともと前列にいる駒に付けた標は、ほぼ無料**である。実測で、同じ5枚・同じ機構のまま
**席だけを変えると代金の寄与が −12.5 → +36.8pt と符号ごと入れ替わる**
（ドルガを後3 → 前1へ動かすと 被弾増 +7.1 → −0.6・早逝 −2.91 → −0.40T）。
第50期の式（`p_t + (1 − p_t) × 0.75`）から被弾増 +60〜+120 を見込んで**大外しした。**

**「機構を 0 にする席」と「代金を 0 にする席」は別物**（第52期）。第49期の業改は
`confirm` の1位で引き取りが 0.00 回/戦になり測定が壊れたが、第52期の採用席は
**発火 3.40 回/戦のまま代金だけが消えている**——後者は正当な配置解。
**切り分けは発火回数**で付くので、`confirm` には `発火` と `空振り` の**両方**の列を出す
（第52期は粗探索 1位・4位を 発火 1.59〜1.60 / 空振り 3.16〜3.17 を理由に外した）。

**「効き」と「早逝」の収支は、対象の上では立たないことがある**（第52期）。
駆り立ての採用席では★対象（ドルガ）の効き +0.3・早逝 −0.40T とほぼゼロなのに、
編成全体の与ダメは +89.5 動いていて**その 76% は仇討ちのザン（+68.2）**だった。
**代金を払っている席では枠どおりに出る**（仮置きでは 効き −76.1 / 早逝 −2.91T）が、
**代金が消える席を選ぶと収支は対象からいなくなって読み手へ移る。**

**「隣に誰がいるか」を読む駒にとって、次数は供給量であって精度ではない**（第52期）。
駆り立て（隣接する `CurrentAttack` 最大の1体に効果を当てる＝ヒサ型の非単調）の
3値の最頻率は **80%**（第45期の 隣接 85% / 対照 65% / ヒサ 62%）で、**両行とも中央 0 / 5**。
カリ1枚だけを振ると**中央は発火が最多（4.04 対 3.40）なのに勝率は最良ではない**
（49.0% 対 60.9%）——次数4だと候補が4枚に増えて**狙った駒以外にも渡してしまう**。
**供給が要るなら中央、狙いを固定したいなら角。**

**順序に依存する打ち消しは「消えたか」では数えられない**（第52期）。
逸らし（ソラ）と駆り立て（カリ）はどちらも `OnTurnStart` で標を操作し、
**発火順は席番号の昇順**（engine は `ctx.AllUnits` ＝味方をスロット昇順に並べた順で回す。
**速さも乱数も入らない**）。ソラがカリより後ろなら標はその手番のうちに剥がされるが、
**`標消え` の計数は 2.36 対 2.18 で2つの席順を区別できなかった**
——ソラはどちらの席順でも「カリの次の発火」より前に1回走るため。
違いは**「その手番のあいだ標が効いたか」**で、そこは勝率にしか出ない（帰属 +4.5 対 −5.6pt）。
**窓を持つ機構の打ち消しを数えるなら「消えたか」ではなく「効く窓のあいだ立っていたか」を数える。**

**掃引の全幅が小さいことには3通りの意味がある**（第41・47・49・50期で1つずつ出た）:

    (a) ノブが機構を動かさない            第41期（比が全点 2.30 で不動）・第47期（纏い率 35.8 → 40.1%）
    (b) ノブは動かすが機構の出力が小さい  第49期（転写 0.58 → 0.00 なのに勝率 1.7pt）
    (c) ノブは動かすが2つの量が打ち消す  第50期（総量 81.9 → 95.4% と取り分の薄まりが相殺）

**全幅だけを見て「ノブを付ける項が悪い」と読んではいけない。**
切り分けは**ノブが機構の計数を動かしたか**で付ける。

**第52期に4期ぶりに 3pt を超えた**（`GoadRule.Boost` の全幅 4.8 / 6.0pt・単調で、
渡した量も ★効き も一緒に動く）。違いは**ノブが動かす量が1本しかない**こと
——第50期の `TargetCount` は「総量」と「1体あたりの取り分」という互いに逆を向く2つを
1つのノブで動かしていたが、`Boost` は見返りの側だけを動かし、**標の危険は `Boost` に依存しない。**
**系: 打ち消しを避けたければ、ノブが動かす量を1本にする**——第47期の
「ノブを付ける項を実装前の収支計算で選ぶ」に、
**「その項が他の量と連動していないことも先に確かめる」**を足す。

**`seats2` の3値（前角 / 中央 / 後角）は「列で決まる駒」の固定度を過小評価する**（第50期）。
`逸らし` のソラは上位5通りで**前角 0**（中央2 + 後角3）＝「前列以外」に完全に固定されているのに、
中央と後角が別の箱なので最頻率は 60% にしか出ない。第45期の3値は**隣接を読む駒**のための分割で、
**列で決まる駒には 2値（前列 / それ以外）を併記すること。**

**味方に載る状態異常は4種類しかない**（第49期 Phase 0-1・実測）——
毒（瘴気の味方漏れ／毒撃の隣への漏れ）・標（囃し立て）・痺（縛め／怯み・怖気・深追い／敵の断罪2体）・
燃（火の粉）。**傷は味方に載る経路が1つも無い**（裂き・刻み・断ち・縫いはすべて敵に書く）
——**この1行は第88期に終わった**（`SpillWoundRule` を既定にしたので、味方の刃6枚が味方に傷を書く。
**味方の傷の読み手は第92期にノノが2枚目になった**——ハリは「敵と味方の深いほう」を自動で選ぶので選べないが、
**ノノの繕いは必ず味方を選ぶ**ので、味方の傷の読み手としては初めて「選べる」側に立っている）。
しかも**既存 50 行で3種が同時に揃う行は 0 / 50、累積で3種に触れる行も 0 / 50**
（`累種` の全行平均 0.49 種）。**「幅を読む駒」を作る前に、盤面に幅が無いことを先に数えること。**

**engine も通貨の読み手である。** 第48期の棚卸しは「敵側の読み手は10通貨すべて 0」と数えたが、
それは**駒**の読み手で、**engine の窓口は別に4つある**——`SelectTargetCore` の標
（`MarkPullPercent` = 75・陣営を問わない）／行動順ループの痺／`TickStatuses` の毒・燃／
`ApplyDamage` の破片。**「駒の読み手が 0」と「効かない」は別。**
第49期はここで予測を外した（敵に標を付けても効かないと読んで、engine が読んでいた）。

**掃引の全幅は「ノブが悪い」と「機構が小さい」の両方で小さくなる**（第49期）。
第41期・第47期に続く3度目の全幅 2pt 未満だが、**診断が違う**——あの2回はノブが機構を
動かさなかったのに対し、第49期は閾値 2 → 4 で**転写が 0.58 → 0.00 回/戦と完全に消える**のに
勝率が 1.7pt しか動かない。**切り分けはノブが機構の計数（発火回数）を動かしたかで付く。**
**全幅だけを見て「ノブを付ける項が悪い」と読まないこと。**

**採用した配置で機構の発火回数が 0 になっていないことを毎回確認する**（第50期から標準の作法）。
`reseat` / `confirm` は勝率を上げる席を探す道具であって、機構を活かす席を探す道具ではない。
第50期の逸らしは全 10 席で 1.56〜4.61 回/戦と 0 の席が1つも無かった（外しも焦点も
隣接・列を条件にしないため）が、第49期の業改は**配置探索が引き取りを 0.00 回/戦にする席を選んだ。**

**機構が負なら、配置探索はそれを無効化する席を選ぶ**（第49期）。`reseat` / `confirm` は
勝率だけを最大化するので、マイナスの機構を持つ駒では「機構が働かない席」が最適解になる。
実測で `業改` は confirm の1位に動かした結果**引き取りが 0.00 回/戦になり、素体と 25 セル完全一致**した
（ゴウを中央に置くと囃し立ての標も火の粉の燃も最初から自分に載るので、引き取るものが残らない）。
**新機構の測定と配置の最適化を同じ実行で回すと、測っているものが消える**
——負の機構を測るときは**「機構が発火する席」での値を必ず併記すること。**

**在庫から無作為に引く機構では、寿命の短い通貨が「引き当てハズレ」になる**（第49期）。
痺は保持者の手番で消費されるので盤面に1ターンしか残らず、しかも
**引き取ると `OnTurnStart` の後の行動順ループで自分の手番が飛ぶ**（引き取りと発揮が同じ資源を奪い合う）。
痺の供給を足した台は**未達 100% / 成立率 0.0%**で、素体より −10.5pt。
**溜める機構に無作為の選択を置くなら、溜まらない通貨を供給側で先に外す。**

**「引き取り」が防御になるのは、引き取るものがダメージのときだけ**（第49期）。
標のように「狙われやすさ」を移す呪いは、引き取った瞬間に自分の寿命を縮める
——実測でゴウの継続ダメージは素体より**少なく**（早く落ちるので浴びる回数が減る）、
縮んだのは寿命のほう（2.22 → 1.65T）。**代金は継続ダメージではなく寿命だった。**

業は棄却したので `UnitCatalog.All` にも `CompareBuilds()` にも載っていない（逆位・まどろみ・
誹り・驕りと同じ扱い）。**測った2編成は診断 `scapegoat` のローカル（`SgRows()`）にある**ので、
`CompareBuilds()` を1行も動かさずに全部を測り直せる。経緯は design/PHASE49_SCAPEGOAT.md。

**`OnAllyDeath` は召喚枠（`Ephemeral`）の死も通る。** 使うときは除外するかどうかを決めて書くこと。
継ぎ接ぎ（ヴェル）は除外している（一度きりの効果を蘇生が無制限に掛け算するのを止めるため）が、
**鱗は通した**——胞子は胞子を産まないので供給は有限（ムグ1体につき最大3件）で掛け算にならない。
**実測で死軸台の獲得 18.2 のうち 7.4（40.7%）が胞子の死**なので、判断で4割動く量。

**`reseat` の採否閾値は 5.0pt**（第46期に 2.0 → 5.0 へ変更・`confirm` の `Threshold`）。
第45期の `seats2` を全48行で測ると**1位−5位の差は 中央値 2.15pt・Q3 4.65pt**で、
**+2.0pt は 26/48 行で上位帯の内部変動より小さい**（＝ほぼノイズを閾値にしていた）。作法は3段:

    (1) 現行が reseat の上位5通りに入っていれば動かさない（31/48 行はここで終わる）
    (2) 入っていない行だけ confirm（seed 200..599）で測り、5.0pt 以上のときだけ動かす
    (3) 採否は1位の配置ではなく**次数**で読む（1位は 28/48 行で入れ替わるが次数の一致率は 98%）

**`reseat` の 120通りの帯の「峰」は、機構ではなく台の配置感度を測っている**（第62期・**第58期 Q6 の訂正**）。
第58期は「帯が二峰なら配置の判断が立っている」を新機構の合否に使ったが、
**素体（同数値・特性なし）の帯を並べると峰の数は同数か素体のほうが多い**
（第62期の実測で 3/3・1/2・1/2）。**帯は「その5枚をどう置くと勝てるか」の分布**で、
そこには**機構が無くても編成そのものの配置感度がまるごと乗る。**

> **「配置の判断が生まれたか」を測るなら、席ごとの帰属（現行 − 同じ席の素体）の符号を見る。**
> 実測でこの2つは逆の答えを出す——帯が単峰の行でも、席ごとの帰属は
> **+12.2 / −2.7** や **+8.9 / −9.5** と明確に符号が割れている。
> **帯を出すときは素体の帯を必ず並べること**（第47期「素体を対照に置く」の帯版）。

**席ごとの帰属を測るときは、床と同じだけ天井も外すこと**（第63期）。
「配置の判断が生まれたか」の器具は **`帰属(席) = 現行(席) − 同じ席の素体`** で、
**測れる席 = 素体の5波平均が 40% 以上**（床の規則・第61期）。**天井の規則がまだ無い。**
第63期に足した「弱体が厚い行」は素体が **93.0〜100.0%** で、
採用席（100.0%）では帰属も版の差も**定義上 0 にしかならなかった**（第24期）。
**次に器具を使うときは `40% 以上 95% 以下` の両側で切ること。**

**器具を直すと、前の期の「割れた」が取り消されることがある**（第63期）。
第62期は4行すべてで席ごとの帰属の符号が割れたと読んだが、**床の規則を足すと1行が落ちる**
——負だった2席は素体が 22.4 / 22.9% の床で、そこの帰属は「差が無い」ではなく**「測っていない」**。
**両 seed 帯（600..999 と 0..199）で同じ判定。**
**第62期が事後に器具を差し替えて採否を出さなかったのは正しかった**
——差し替えていたら誤って採っていた。

**`Attacks == 0` は「その駒に配った量が無駄になる」の指標にならない**（第63期）。
**棘の反撃量は `ThornsTrait.OnDamaged` が自分の `CurrentAttack` で決める**ので、
**不動のカドは一度も `PerformAttack` を通らないのに弱体は満額効く。**
実測で、弱体の押し付け先がカドの席は **−1.5 / −2.2pt**、据えのバン（普通に振る駒）の席は
**+1.3 / +1.9pt** と**符号が逆になった。「振らない駒は弱体の捨て場になる」は誤り。**
第56期の但し書き（反応型は「死蔵」ではなく「振らずに干渉している」）は、
**強化側では読み違いで済むが、弱体側では符号を逆に読ませる。**

**通貨を「移す」機構は、その通貨の読み手にとっては「奪う」機構である**（第63期）。
逆しま（ウツ）は `AtkBonus` が負のとき攻撃力が下げ幅の3倍なので、**弱体がこの駒の燃料**。
横流し役をウツの隣に置くと、ウツに向いた弱体を別の駒へ回してしまう——実測で
`ウツの被弾弱体 14.0 → 0.0` になった2席だけが **−73.0 / −60.9pt**、
取り上げていない3席は **0.0 / +4.4 / 0.0**（両 seed 帯で再現）。
第43期の渡し（弱体を敵へ流す）は**味方側の読み手と同席していなかった**のでこれが見えなかった。
**新しく「移す」機構を作るときは、移す通貨の読み手が同席する行を試験行に必ず1本入れること。**

**横取り型の機構の流量は「供給の総量」ではなく「供給の宛先 × 横取り役の隣接」で決まる**（第63期）。
火選り行は味方側の弱体を 2.99 量/戦 持っているのに**1点も横取りできない**
——鈍る相手は2枚（ホタとムド）で、**ホタは宛先そのもの**（`dest == target` で何もしない）、
**ムドは横取り役の隣ではない**。**実装前に机上で出せる**（第63期はそう予測して 0.00 を当てた）。
**在庫を数えるだけでは足りない。**

**死蔵を `Attacks == 0` で数えてはいけない**（第64期・**積み残し2の訂正**）。
攻撃力を出力量に変換する経路は**ロスター全体で4本**——`PerformAttack` ／ 棘（`ThornsTrait`）／
仇討ち（`AvengeTrait`）／ 責め苦の追撃（`TormentTrait`）。`UnitTally.AttackReads` はこの4箇所だけで
加算する（**誰も読んで分岐しない**）。61行 × 5波 × seed 0..199 の実測:

    旧（`Attacks == 0`）           1.14 量/戦 = 9.0%   ← 広すぎる（棘のカドを数え過ぎる）
    参考（`Interventions == 0`）   0.62 量/戦 = 4.9%   ← 狭すぎる（破裂・生贄・吸いは固定量なのに立つ）
    **新（`AttackReads == 0`）     0.48 量/戦 = 3.8%**

カドは 0.66 → **0.18**（旧の 73% が誤検出）、ザンは 0.21 → **0.03**。
**100% 死蔵なのは ノノ・ミオ・ヒヨ の3枚だけ**（`Actions = [Skill]` で術が攻撃力を読まない）
——**これがロスターの「真の捨て場」の全部。カドは捨て場ではない。**
**第56期の但し書き（ターン外に振る駒は死蔵でなく振らずに干渉している）は半分だけ正しい**
——**ザンは正しく、ハギは誤り**（追い打ちは `ctx.PerformAttack` を通るので `Attacks == 0` は本当の不発）。

**素体を引いて機構を測る期では、`reseat` を配置の決定に使わない**（第64期）。
`reseat` は120通り＝**他の4枚も入れ替える**ので、「同じ4枚・同じ席」を前提にした器具の族が変わる。
実測で、選定規則の族（他の4枚を元のスロットに残す）が素体 54.3〜63.6%（測れる席5）なのに対し、
`reseat` 粗2位の族は**5席とも素体が床**（24.6〜28.8%）だった。
その席の帰属 +41.6pt が測っているのは配置の判断ではなく**台が生き返ったこと**である。
**`reseat` は勝つ配置を探す道具で、素体を引く器具とは目的が逆を向いている**
——第50期「勝つ席を探す道具であって機構を活かす席を探す道具ではない」の逆側。

**「もっともらしい理由で緩めた条件」を数えること**（第64期・横流しの3期目）。
第62・63期は帰属が**3行とも正**（+1.8/+12.2/+8.9 ・ +1.7/+13.6/+5.2）だったのに、
**行・枠・席の選び方を測る前の規則で固定すると 3行とも負**（−3.8/−8.8/−18.4・両 seed 帯で再現）。
緩んでいた自由度は3つ:

    どの行を試験行にするか      「供給が大きい行」→「供給が大きく、**かつ測れる席が3つ以上**」
    どの枠を差し替えるか        「寄与最小。**ただし主題が壊れるなら2番目**」→「寄与最小」
    どの席を採るか              「`reseat` 上位5の中で発火が最多」→「**差し替えた枠の席**（自由度ゼロ）」

**3つとも第62期の時点では自覚されていなかった**し、枠の2番目選択は
「クグを抜くと縛めが消える」という**正しい**理由だった。**正しい理由でも、結果を見てから緩めれば同じ。**
**3回測って通らなかった機構では、緩めた条件を1つずつ数えること。**

**主判定を新しく作った期は、その判定式が落とす行を1本、同じ表に並べる**（第64期・陰性対照）。
強化の在庫が 0 の行にヌキを置くと、**5席 × 5波 × 400 seed が素体と1セルも違わない**（幅 0.0pt）
——横流しは量を1点も増やさず行き先だけを動かすので、動かすものが無ければ何も起きない。
**「通った」だけを並べた表からは判定式の厳しさが読めない。**

**同じ規則が、移す通貨の符号で正反対に働く**（第63・64期）。逆しま（ウツ）は `AtkBonus` の
**符号だけ**を読み、負なら3倍・正なら半減する。横流し役をウツの隣に置くと:

    **弱体**を横流し → ウツから**燃料**を奪う → **−73.0 / −60.9pt**（第63期）
    **強化**を横流し → ウツから**毒**を奪う   → **+19.3 / +10.3 / +16.4pt**（第64期）

**符号は宛先がウツかどうかだけで決まる**（ウツが宛先の席だけ −7.8 / −7.1・両帯で再現）。
**「通貨を移す機構」は読み手にとって奪う機構だが、その読み手にとって通貨が資産か負債かで符号が決まる。**


**手で写した表には 29 件の誤りがあった。5期かけて手で見つけたのはそのうち 3 件で、機械は 2.5 秒で全部出した**
（第94期・棚卸し。**新しい `TraitId` ゼロ・駒ゼロ・機構ゼロ・`CompareBuilds()` / `CrossBuilds()` /
`Stages` / `UnitCatalog` は1行も触っていない・`compare` 305 セル 0 件・交差帯12行 0 件**）。
`docs/rules.md` を生成物に足し（**`docs/` は 9 → 10 ファイル**）、
`derive scan` が**走らせて観測した事実**と `TraitEntryMap` / `TraitKeyMap` を突き合わせる。
経緯は design/PHASE94_DERIVE.md。

    供給の欠落 13 ／ 読みの欠落 13 ／ 供給の過剰（中継）1 ／ `TraitKeyMap` の欠落 2 ＝ **29 件**
    Q1 第92期の欠落2・過剰1 を (T3) の前のコミットで **3/3 検出**（修正後は 0/3）
    修正後の `derive scan` は 欠落 0 ／ 過剰 0 ／ `TraitKeyMap` の差 0 枚

**以後の指示書は既定値を `docs/rules.md` から引く。手で写さない。**
`BattleEngine.Run` の引数 **30 本**が正本で、第93期がずれた2つは
`GatherRule { Enabled = True }` / `IgniteRule { Enabled = True }` と書いてある。
**指示書が挙げた 15 個に対して実際は 30 本あった**——**指示書の一覧を信用しない、が正しかった。**

**「機械に照合させる」器具そのものが、最初は表より不正確だった**（第94期）。
第一版の欠落 **105 件のうち 78 件（74%）が器具の誤り**で、
**既知の誤りを再現するテスト（Q1）が無ければ、そのまま表に書き込んでいた。**
**第88期の「新しい器具は既知の値を再現できて初めて使える」は、照合の器具にもそのまま当たる。**

**観測の印は「engine と特性の境目」を陽に決めることを要求する**（第94期）。
`ApplyDamage` の中（破片・据え・惨禍・肩代わり・巻き込み則）は **engine の機構**で、
そこで印を降ろさないと**殴っただけの駒が通貨の供給者に化ける**
（実測で吸いのゴルムが傷を 4.74 回/戦「供給」し、破片・手番・傷を 4.97 回/戦「読む」ことになっていた）。
逆に**`WoundDepthOf` / `IsWounded` は engine の内部ではなく「傷の読み手が傷を読む窓口」**なので、
そこを engine 側に倒すと**抉り・断ち・縫いの読みが1件も観測できなくなる。**
線を引いた結果、**未観測 13 件のうち 7 件が「engine が窓口を持つ通貨」に綺麗に落ちた**
（`Sluggish` の手番・`Sharer` のなまり・`Bulwark` の据え・滲み則の傷 4 枚）。

**採用したときに表を直していない機構がある**（第94期）。
**縫いの両側読み（`SutureSide.Both`・第85期に採用）は 9 期のあいだ `Reads` に載っていなかった**
——実測で味方側の糸口を 0.69 回/戦 引いている。
**機構を採用する期の受け入れ条件に「`derive scan` の欠落が 0 件」を入れること。**

**「中継」は機械的に判定できる**（第94期）。**同じ特性が同じキーについて増と減の両方を書き、
増の総量 ≤ 減の総量なら中継**——盤面の総量を増やさないので供給者ではない。
ガルドの傷は **増 0.31 ／ 減 0.31 ／ 純増 0.00 回/戦**で、
第92期が手で見つけた「ガルドの傷は中継」と**走らせた観測が同じ答えを出した。**

**味方の刃（`isFriendlyFire`）の呼び出し口は、特性に紐づくものが 8 枚 ＋ engine の中継 2**（第94期）。
第85期の「刃6」（余波・生贄・吸い・破裂・棘の巻き込み・置き去りの削り）に
**深追いの反動と転嫁の代金**を足した 8 で、**中継（巨躯・分かち）を足すと第85期の実測 10 に一致する。**

**`TraitEntryMap` の直しで動く派生値**（**数え直していない**）——
第78期の**入口**（`EntriesOf` の打ち消しが変わる）／第81期の**表D**（供給→読み の分母が増える）／
第92期の**交差の空白表**。**`TraitKeyMap` の直しは敵側2枚だけ**なので
**ロスター 51 体の `KeysOf` は動かず、第80〜83期の「独立の広さ」は動かない。**

**鱗の `(破片, 自分)` だけは観測されても直さなかった**（第94期）。
`TraitEntryMap.Supplies` の doc に明記された意図的な除外で、
**載せると自分の読みを打ち消して入口が 1 → 0 になり、第78期の「入口」の定義そのものが変わる。**
**ここだけは観測より doc の側を採る**（`derive scan` は別扱いの1行として毎回出す）。

**`docs/crossing.md` は所要時間を本文に書いているので、再生成すると必ず 1 行差分が出る**（第94期に確認）。
**`docs/rules.md` の `測った診断` の列は `Program.cs` の本文から引くので、
診断の説明文が型名に触れるだけで動く**（既定値の列は動かない＝判定には影響しない）。

**交差は3本とも引けた。引いたのは味方側の首だった —— 深手（`DeepRule`）は測って採用しなかった**
（第93期。**新しい `TraitId` ゼロ・駒ゼロ・`CompareBuilds()` / `CrossBuilds()` / `Stages` は1行も触っていない・
`docs/` は9ファイルとも再生成して差分 0 バイト**）。傷が **3** に達すると束ねて**深手**（0/1 の二値）にし、
深手の駒は**実際に行動したとき** 4 の自傷（`lethal: true`）・**上乗せ**（深手の上の傷はその場で 4 のダメージ）・
**傷の読み手から見て「傷1つぶん」**（滲み則だけ +1 ではなく +2）。既定は `false` のまま**対照として残置**。
経緯は design/PHASE93_DEEP.md。

    門（W0・理想61行）        味方 1.69 / 2.88 / 2.23 回/戦（○） 対 **敵 0.01 / 0.01 / 0.00**
    Q1-1（主判定・A = ノミ）  **0 / 6 枚**（Q2 は 2 位で通っている）              **×**
    Q1-1（参考・A = キリ）    **0 / 6 枚**（Q2 は 3 位）                          **×**
    拒否権1（61行）           29 行が −10.0pt 以上・**19 体が「壊れ」**          **×**
    拒否権2（主判定19行の第五波） 38.0 → **32.3%**（歯止め 33.2）                 **×**
    (A) 稼働率 **70.6%（過去最大）** ／ (B) 持続係数 2.45 ／ 発火 4.02 回/戦

**A（2×2 で固定する駒）は「その規則の供給の過半を持つ駒」にする**（第93期・**予測 P1 を外した原因**）。
深手の供給の内訳は **巻き込み則 8.09 回/戦（95.0%）／刻み 0.28（3.3%）／裂き 0.07（0.8%）**
——**A をノミ／キリに固定した 2×2 は、この機構の主たる供給源を1ビットも振っていない。**
だから意図した相手が並ばず、キリの表では**上位2枚（ゴルム +6.84 / ネル +3.37）が意図しない相手**で、
**意図した泥人形ムドが 50 体中の最下位（−3.91）**に来た。
**「深さ 3 を要求するのでノミだけが深手を作れる」は敵側についてだけ正しかった**
——味方側の深手を作っているのは**毎ターン味方全員を削るゴルムと、攻撃ごとに隣接を巻き込むボルグ**である。

**閾値を持つ機構は、同じ通貨の消費型の読み手と構造的に同居できない**（第93期・第39期の一般形）。
断ち（ナタ）は閾値 2 で傷を 0 に戻すので、**束ねの閾値 3 には絶対に届かない**
——ローカル台の対照で**敵側の到達が 0.00 回/戦**（他の4台は 0.22〜1.26）。
**`compare` 61 行で敵に傷を書くのはキリ・ノミの 8 行だけで、そのうち 4 行に消費型の読み手が同席している**
——これが「敵側 0.01」の実体。**第39期の「縫いと断ちは同居できない」は、
消費型の読み手と<u>閾値を持つ後発の機構すべて</u>の話に広げられる。**

**紙は下限とは限らない。自分の分母を削る機構では上限になる**（第93期・**3期続いた形が初めて破れた**）。
第85・90・91期は3期続けて「紙は線形の下限」だったが、深手の実測 ÷ 紙は **0.79**
——**自傷が `lethal: true` なので、W0 で数えた「達した駒がその後に行動した回数」に W1 では届かない。**
**紙を W0 から引くときは、その機構が自分の供給源を殺すかを先に見ること。**

**稼働率も単独では採否を決められない**（第93期・第87期の (B) に足す）。
**稼働率 70.6% は第86期以来の「35〜40% に張り付く」帯を大きく超えた過去最大**
（第87期 45.0% / 第91期 51.1〜55.9%）で、持続係数も 2.45・発火 4.02 回/戦 なのに拒否権が2本立った。
**よく走るのは「よく効く」ではない。この機構の場合、よく走るほど味方が死ぬ。**

**交差帯の行は「機構が繋ぐキーの組」で選んであって「その機構が走る台」ではない**（第93期・第92期の器具の限界）。
交差帯12行のうち**動いたのは 6 行で、符号は全部負**（強化×燃 −4.20 / 強化×被弾 −3.00 / 傷×被弾 −1.70 …）。
**第92期に「交差の空白のうち機構があるのは毒×傷の1組だけ」として組んだ `毒×傷 (ノミ×グザ×ミオ)` は
1ビットも動かなかった**——**その行に巻き込み則の書き手が1枚も居ない**ので味方側の深手が立たない。
**交差を測る器具として使うときは、その機構の供給がその行に実在するかを先に数えること。**

**手作り表のずれは5例目**（第84・85・90・92期に続く）。第93期の指示書は
**`GatherRule` と `IgniteRule` を「既定 off」と書いていた**が、どちらも第89・90期に採用済みで**既定 on**。
**実装から derive できるものは必ずずれる。**

**`ScapegoatTrait.Kinds` は 5 → 6 に動いた**（`StatusKeys.All` が 7 → 8 になったため。除外を並べる形なので自動で入る）。
**業はロスターにも敵の波にも居ないので盤面への影響は無い**が、**診断 `scapegoat` のローカル台の分母は動いた。**

**61行は軸内、交差帯は軸間。目的が違う（第92期）。拒否権の分母は今までどおり `compare` 61 行全体。**
`CrossBuilds()`（12 行）と `docs/crossing.md` を並置した。**`CompareBuilds()` は1文字も触っていない。**
交差帯は**主判定・副判定の側**で使う器具で、**壊れを見る器具ではない**（第91期 (G1)）。
**主判定19行には入れない**（`PrimaryFifthFloor` の再測定が要る）。

**「交差の空白」は「機構の空白」でもあった**（第92期）。11 キーの 55 組のうち
**繋いでいる機構が実在するのは 12 組だけ**で、**空白（61 行に 0 行）12 組のうち 11 組は機構が1本も無い。**
**機構が実在する 12 組のうち 11 組は、既に 61 行に乗っている**——空白かつ機構があるのは **毒×傷 の1組だけ。**
**「61 行には軸交差が足りない」のではなく、軸をまたぐ機構そのものが 12 本しか無い。**
**新しい交差を測りたいなら、まず交差を繋ぐ機構を作ること。台を並べるだけでは埋まらない。**
候補は **傷×移動**（`SwapSlots`）と **痺×標**（`SelectTargetChain` の標の段）の2組で、どちらも既存の窓口で書ける。

**第91期の「2軸以上を含む行は 17 / 61」は 13 / 61 に訂正**（第92期）。
第91期は痺れの書き手を「トウ・クグ・セロ・セッキ・ヒビ」・破片を「ヒビ・ウケ・ウロ」としていたが、
`SetCounter(StatusKeys.Stun, …)` の全数は `Paralyze`（トウ）／`Bind`（クグ）／`Avenge`（ザン・自分）／
`Torment`（シガ・自分）／`Overreach`（エグ・自分）／`Condemn`（敵）の6箇所で、
**セロ・セッキ・ヒビは1つも書かない。表の形（傷 × 毒/燃焼 が 0 行）は変わらない**が、
**偏りの中身は「燃焼×痺れ 6 行」ではなく「燃焼×標 4 行」。**

**`TraitEntryMap.Supplies` に2件の欠落と1件の過剰がある**（第92期に洗い直した。**表そのものは触っていない**）。
**疫み（ラウ）と澱み（ミオ）は毒を敵に置くのに `Supplies` に載っていない**（`Reads` にだけある）。
逆に**ガルドの傷は載っているが中継**（`GatherRule` は移すだけで盤面の総量を増やさない）。
第84期（カド 8→11 行）・第85期（呼び出し口 6→10）・第90期（`Where` の訂正）に続く**4例目**
——**手で作った表は、使う前に毎回洗い直すこと。**

**交差の台を並べても稼働率は上がらない。上がるのは「毎ターン撒く供給に乗った交差」だけ**（第92期）。
第91期のローカル台の稼働率 51.1 / 55.9% を交差帯 12 行は再現しなかった（**37.2 / 39.3% 対 61 行 38.1%**）。
**あの4台の共通点は瘴気（毎ターン全体に撒く）**で、交差帯にはそれに当たる行が1つしか無い。

**「1つの動作の表と裏」は、表と裏が同じ駒の上に同時に乗れるとは限らない**（第92期）。
火選り（ヒヨ）の鈍りは**燃えていない味方**に落ちるので、**弱体と燃焼は定義上その瞬間には同居しない**
——実測で出会い **0.03 回/戦・稼働率 0.6%** と 12 行中の最小。
**交差を測る前に、その2つのキーが同じ駒の上に乗れるかを確かめること。**

**席は「勝つ席」ではなく「測れる席」で選ぶ、を帯まるごとに当てた**（第92期。第50・59・64期の帯版）。
交差帯 12 行で `reseat` の1位は **5 行で情報セルを減らし、3 行で同数、増やす行は 0**。
勝率だけなら 8 行が「要差し替え」（差は最大 **35.7pt**）だが、**測るための帯では 12 行とも据え置きが正しい。**

**代金を足すと絶対値は下がるが、特異性は上がることがある**（第92期）。
ノノに塞ぎを足すと Δ相乗は 1.09 → 0.86 に下がるのに、**ノイズ床が 0.63 → 0.44 とそれ以上に下がる**ので
**Q1-1 が 1 枚 → 2 枚に増える**——**代金は意図しない相手の側の揺れも一緒に削っている**
（第88期「大きさは線にしない。並び方を見る」の実例）。

**供給の周期だけでなく、供給の「偏り」が読み手の選択に情報を足すかを見ること**（第92期）。
第85期の表D は「巻き込み則の値段は供給の周期と単調」で毎ターン全味方の吸い（ゴルム）が1位だったが、
**ノノ（繕い）の側では ゴルム +0.13（17 位）で、毎ターン<u>自分より遅い味方だけ</u>を削るナラが1位（+0.86）。**
**繕いは「誰がいちばん傷ついたか」を選ぶ機構なので、供給が一様だと読む先が動かない**
——**一様な供給は在庫を作るが、選択の情報を作らない。**

**判定の線は、既存の帯が満たしているかを先に数える**（第92期）。交差帯の Q1 の線（12 行中 10 行 ＝ 83%）は、
**`compare` 61 行自身では 36 / 61（59%）しか満たさない。**
**新しい帯に、既存の帯が満たしていない基準を課していた**（交差帯は 11 / 12 ＝ 92% で通ったので結果は変わらない）。
第59期「判定式の条件節が分母の何割を選ぶかを実装前に数える」の、**線の高さの側**。

**繕いの傷読み（`MendRule`）を採用した**（第86期に作り、**第92期に測り直して通った**）。
`MendRule.Default` = `MendSide.Wound`。ノノの `Traits` に塞ぎ（`TraitId.Seal`）を足し、説明文を直した。
**新しい `TraitId` ゼロ・新しい駒ゼロ・engine の新しい窓口ゼロ・`CompareBuilds()` の行も席も動かしていない。**
主判定は **50 体中1位が意図した相手の置き去りのナラ**（Δ相乗 +0.86pt・ノイズ床 0.44pt・2系列とも正）で、
**2枚目が焼け残りのボルグ（+0.53）**。拒否権1〜3 もすべて ○（`compare` は **10 セル / 5 行**・
−10.0pt 以上落ちた行は 0）。**第86期は紙のスループット（大きさの線）を門に置いていたので
2×2 を1戦も回さずに落ちていた**（実測 2.0%）——**門を外した第90期以降で通った2例目**
（1例目は第89期の傷の引き取り）。**紙は今後も Phase 0 で出すが、門にはしない。**

**ノノに初めてキーが立った**（第92期）。`TraitKeyMap[Mender]` は空だったので、
**第83期の「キーを1つも持たない駒は 8 / 51 体」は 7 / 51 になった**（リィカ・ドルガ・ムグ・ヴェル・ササ・ハギ・ナラ）。
**第80〜83期の派生値（独立の広さ・入口・発火口）は動く。**
第89期にミオが `{Poison}` → `{Poison, Wound}` になったのと同じ形で **2 例目**。

**棘の傷（`ThornRule`）は第84期・第88期・第92期の3回落ちた。この案は閉じたまま**（既定 `None` で残置）。
第92期の再判定は Q1-1 **0 / 5 枚**（意図した相手の最良は抉りのエグの 10 位 +0.07pt）で、
**上位3枚はどれも意図していない相手**（萎縮のクビ +0.40 / 澱みのミオ +0.35 / 砕け盾のヒビ +0.23）。
**傷の5枚の最下位が断ちのナタ（−0.24）**——第84期の
「棘が書く傷は打点の 7〜14% で、傷の読み手は加算なので誰が書いても値段は同じ」と整合する。

**燃焼側の滲みを切った（第91期）。非スタックの通貨に「深さ」を足しても、点け直しで消える。**
`SoakRule` を `(bool Poison, bool Burn)` に分け、既定を **`(Poison: true, Burn: false)`** にした。
燃焼の滲みは **1.65 回/戦 発火するのに紙の 2% しか払い出さない**のに `compare` の 9 行を下げていて、
**切ると 9 行とも第90期より前に完全に戻る**（いちばん大きいのは `鱗 (ウロ×死軸)` の第4波 68.0 → 57.5 → 68.0）。
理由は構造的で、**火の粉が毎ターン残ターンを 3 に設定し直すので、4 に伸ばした1ターンが次の一振りで消える**
（第57期の捨て率 35.5% の帰結）。**深さを足す設計は毒・傷のような「積む通貨」にしか効かない。**
**`compare` の差分は 18 セル / 11 行 → 4 セル / 2 行**になった。

**毒側は 61 行の分母でも通る**（第91期）。主判定は **A ＝ キリ・ノミ の両方**で
**50 体中1位が意図した相手の瘴気袋のグザ**（+1.25 / +0.49pt・(G3) によりフィルタ無しが主判定）。
**−10.0pt 以上落ちたのは `追撃×毒 (ハギ×グザ)` の1行だけで、その5枚のどの駒も「他の行」の平均が
−0.13 〜 +0.00pt** ＝ (G2) の分解で**制約であって壊れではない。**
**使えなくなったのは組み合わせ1つだけで、駒は1枚も使えなくなっていない。**

**「敵側で一度も発火しない」は機構の欠陥ではなく台の欠陥だった**（第91期・第90期の訂正）。
`CompareBuilds()` の外に4台組むと**敵への滲みが 1.28〜2.93 回/戦 立つ**:

    キリ×グザ（本命）       2.93 回/戦 ・ 稼働率 51.1% ・ 勝率 +0.70pt ・ **持続係数 1.11**
    キリ×グザ×ミオ（下流）  2.91      ・ 55.9%      ・ +0.70      ・ **1.34**
    ノミ×グザ（積む傷）     1.71      ・ 28.1%      ・ +0.10      ・ 0.84
    キリ×スィド（被弾時）   1.28      ・ 20.7%      ・ ±0.00      ・ 0.86

**持続係数が 1 を超えたのはこの期が初めて**（理想台では 0.88）
——**同じ機構・同じ +1 でも、払い出しの相手が何ターン生きるかで 1.5 倍動く。**
**稼働率も 51.1 / 55.9% で、第86期以来の「35〜40% に張り付く」帯を初めて明確に超えた**
——**毎ターン全体に撒く供給（瘴気）に乗ると、稼働率は波の長さに律速されない。**

**`compare` 61 行は軸をまたぐ機構を測る器具として偏っている**（第91期・**第92期の入口**）。
**2軸以上の書き手を含む行は 17 / 61（27.9%）あるが、内訳が偏っている**
——燃焼×痺れ 6 行に対し、**傷を含む交差は 傷×標 の 2 行だけ**（`逸らし改` / `止め`）で、
**滲み則が要求する 傷 × 毒/燃焼 は 0 / 61。**
**「軸交差が少ない」ではなく「軸交差の分布が偏っている」。**

**情報帯フィルタの有無は判定を変えなかった**（第91期）。Δ も床も同じ比で 1.2〜1.5 倍になるだけで、
**比を見る判定はフィルタに鈍い**（第88期 8-1 の再確認）。
**(G3) は「フィルタが判定を変えていた」の訂正ではなく「フィルタの根拠が消えていた」の訂正である。**

**紙は3期続けて線形の下限に乗った**（第85・90・91期）。**分子を「二次」と先に書いても実測は下限に落ちる**
——供給は読まれる前に決着に消える側へ倒れるから（第91期の実測 実測 ÷ 下限 = 0.88）。

**「版に依らない計数」の自己検査は、盤面が動かない台で見る**（第91期・第87期の (b) の再発）。
計数を分岐の手前に置くのは**経路の性質**であって**観測される値の性質ではない**
——滲みが1回でも走る台では観測値が 1.90% ずれる。
**陽性対照は「その機構が1回も走らない台」**（傷の書き手も巻き込み則の刃も入れない台で
計数 33,027 が3版とも厳密に一致し、`compare` も動かないことを確認した）。

**器具の規約4つ（第91期に固定した。第88・90期に3期続けて器具の穴が出たので、規約の側を直した）。**

**(G1) 壊れを見る拒否権の分母は `compare` 61 行全体。主判定19行に限らない。**
**主判定19行は「情報が取れる代表」の集合であって、「壊れが出る場所」の集合ではない**
——19行は各軸の代表を1行ずつ選んだ集合なので、**「毒の書き手 × 味方に傷を書く刃」という
<u>組み合わせ</u>の行を1つも含まない。** 第90期に `追撃×毒 (ハギ×グザ)` が
第二波で 87.5 → 29.0（−58.5pt）落ちたのに拒否権が3つとも通ったのはこれが原因。

**(G2) 拒否権3（61行版）は「壊れ」と「制約」に分ける。これが無いと (G1) が使えない。**
いずれかの波で **−10.0pt 以上**落ちた行について、
**その行に含まれる駒それぞれの「その駒を含む<u>他の</u>行」の全波平均の変化**を計算する:

    どの駒についても |平均変化| < 3.0pt   → 組み合わせ固有 ＝ **編成上の制約。拒否しない**
                                            （報告書に「この組み合わせは成立しなくなった」と明記する）
    いずれかの駒で平均変化 ≤ −3.0pt      → その駒が使えなくなっている ＝ **壊れ。拒否する**

> **駒が使えなくなるのは壊れ。組み合わせが使えなくなるのは設計。**
> **「他の行」が 0 行の駒についてはこの分解が成立しない**ので、そう書くこと。

**(G3) 情報帯フィルタは engine の規則には正当化できない。**
第88期 §2-1 はフィルタの根拠を**「A を素体にしたセルは版に依らない」**ことに置いたが、
**engine の規則では A を素体にしても規則が走る**のでその根拠が消える
（第90期の自己検査 (a) が落ちたのはこれ。**実装のバグではない**）。

> **規則が駒の特性の中にあるときだけ情報帯フィルタを当てる。**
> **engine 規則の 2×2 では、主判定はフィルタ<u>無し</u>で行い、フィルタ有りを参考として併記する。**

**(G4) 新しい判定条件・停止条件・拒否権を書くときは、その条件が
`compare` 61行・50体の組・主判定19行のどれを分母にするかを必ず書く。**
第88期（線の尺度）・第90期（紙の門）・第90期（拒否権の分母）と器具の穴が3期続いた原因は
**すべて分母の書き落とし**である。第59期の「判定式の条件節が分母の何割を選ぶかを実装前に数える」を、
**壊れを見る側にも当てること。**

**(G5) 2×2 の A は「設計上の主役」ではなく「その機構の入力を実際に供給している駒」から選ぶ**（第94期・第93期の反省）。
**Phase 0 で供給の内訳を出し、それを見てから A を決めること。**
**供給の過半を engine の規則が占めている場合、A に置ける駒は存在しない**
——そのときは 2×2 を主判定にせず、**`compare` 61 行と交差帯 12 行を主判定にする。**
第93期は深手の供給の **95.0% が巻き込み則（engine）**だったのに A をノミ・キリに固定し、
**主判定が 0/6 枚**で落ちた。**Phase 0 に内訳（8.09 / 0.28 / 0.07）が出ていたのに使わなかった。**

**(G6) 副判定を毎期出すのをやめる**（第94期）。**発火回数・持続係数・稼働率・台の乖離 は常設をやめ、
その機構に該当するときだけ出す。**

    持続係数        払い出しが持続する通貨（毒・燃焼・層）に乗る機構のときだけ
    発火回数／稼働率  読み手を足す機構のときだけ
    台の乖離        理想台とドラフト台で符号が割れたときだけ（**第92期に役目は終わっている**）

**4つとも単独では採否を決められないことが実測で確定している**（持続係数 17.97 で否決＝第87期／
稼働率 70.6% で拒否権2本＝第93期）。**出すときは必ず決着ターン数を併記する。**

**(G7) 紙のスループットを出すときは、分子について3つを先に書く**（第94期に3つ目を足した）。

    1. 線形か二次か   二次なら1ターンの丸めが2倍の誤差になる（第87期）
    2. 門ではなく出力  門は「鎖が繋がっているか」（第90期）
    3. **分母を削るか** ← 第94期に追加

**自分の分母を削る機構では、紙は下限ではなく上限になる。**
第85・90・91期は3期続けて「紙は下限」だったが、**第93期に初めて破れた**（実測 ÷ 紙 = 0.79）
——自傷で対象が倒れると、その後の払い出し機会が消えるため。

**(G8) 測定の重さを3段に分ける**（第94期）。**バランス完成段階ではないので、丙は測らない。**

    甲  engine の規則（全駒に同時にかかる）  2×2（(G5) の条件付き）＋ `compare` 61行 ＋ 交差帯12行 ＋ 自己検査フル
    乙  既存駒への1機構追加                `compare` 61行（拒否権）＋ 交差帯12行 ＋ 自己検査の必須4項目。
                                          **2×2 は (G5) の条件を満たすときだけ**
    丙  新しい駒1体                        `compare` 61行（拒否権）だけ。**入れてみる**

**採って外れたときの戻しはノブ1行＋`docs/` 再生成で済む。測って落とすほうが1〜2時間かかる。**
**自己検査の必須4項目**（これ以外は機構ごとに書く）:

    1. `compare` 305 セルが `docs/balance.md` と 0 件
    2. `docs/` 全ファイルを再生成して差分を報告する
    3. 触っていないノブの既定が動いていない（**`docs/rules.md` の差分で示す**）
    4. `ctx.PickOne` を新たに使っていない（第89期 (h)。候補2個以上で `Roll` を消費する）

**紙のスループットは門ではなく出力である（第90期から規約。第89期までの停止条件を廃止した）。**
第88期に「大きさは線にしない」と主判定を作り替えたのに、**Phase 0 の停止条件
（紙 ÷ 総被ダメージ ≥ 5%）という大きさの線を門のところに残していた。**
**線は版をまたいで持ち運べない**——第87期（攻撃側）は 出力 +8.3% に対し勝率 +1.0pt、
第89期（防御側）は **+0.82% に対し +3.4pt** で、**変換率が 30 倍違う。**

> **紙は今後も Phase 0 で必ず出すが、門にはしない。門は「鎖が繋がっているか」:**
> **(1) その通貨を持つ相手が存在する ／ (2) その相手に書き手が書く ／ (3) 書いたものが実際に払い出される。**
> **どれかが 0 なら、そこが切れている場所を書いて閉じる。**
> **2×2 は5〜6分なので、門で節約できる計算量はほとんど無い。**

**門を外した第90期の1期で、2つ通った**——(P1) 第89期の傷の引き取り（`GatherRule`）と、
本編の滲み則（`SoakRule`）。**第89期は紙 0.82% で門に弾かれていたが、実際には
縫いのハリが 50 体中1位・Δ +0.35pt で特異性の規約を通る。**

**滲み則を採用した**（第90期。**傷を持つ相手には状態異常が深く入る**）。
**第91期に通貨ごとに分け、燃焼側を切った**——`SoakRule.Default` = `(Poison: true, Burn: false)`。**新しい `TraitId` ゼロ・駒ゼロ・`CompareBuilds()` は1行も触っていない。**
engine に足したのは**毒の窓口 `ctx.Poison` 1本**（加算の入口5箇所だけを通す。上書き2＝ミオ・減算2＝ベニ／ヴィオ は通さない）と、
`Ignite` の中の **+1 が1行**。**グザ・スィド・ラウ・ボルグの4枚が、定義を1文字も変えずに傷の読み手になった。**
主判定は **A ＝ キリ・ノミ の両方で通り、どちらも 50 体中1位が意図した相手の瘴気袋のグザ**
（+1.61 / +0.59pt・ノイズ床 0.77 / 0.47pt・2系列とも正）。**拒否権1〜3 もすべて ○。**

**非スタックの通貨に「持続 +1」を足しても、供給者が毎ターン点け直していれば一度も到達しない**（第90期）。
燃焼側の滲みは **1.65 回/戦 発火するのに紙の 2%（0.1 点/戦）しか払い出さない**
——火の粉は**ボルグが殴るたび**残ターンを 3 に**設定し直す**（第57期の捨て率 35.5%）ので、
**4 に伸ばした残ターンは次の一振りで 3 に戻される。**
**滲みが払い出されるのは「供給者がもう振らない」＝決着間際のターンだけ。**
**持続系の通貨を深くする設計は、その通貨の再付与の周期を先に見ること。**
毒側は 0.88 で**線形の下限にほぼ乗る**（第85期の「紙は下限になる」が2期続けて当たった）。

**持続係数が 1 を超えるのは、盤面に新しく「層を置く」機構だけ**（第90期）。
第87期の着火は 17.97 だったが、滲みは**すでに書かれる毒に 1 を足す**ので
**1層ぶんの寿命はその毒がもともと持っていた寿命と同じ**——**増えるのは層であって時間ではない**（実測 0.88）。

**出会いの回数と値段の順位は逆になりうる**（第90期・予測 P2 を外した）。
ボルグは「傷を持つ相手に火を点けた回数」が **1.65 回/戦 でグザの 0.30 の 5.5 倍**なのに、
Δ相乗は **16 / 18 位（+0.06 / +0.00）**。**出会いを数えただけでは順位を予測できない。**

**「両陣営に等しくかかる」規則が、この盤面では片側でしか働かない**（第90期）。
理想61行では**滲みの 100% が味方側に落ちる**——**`compare` 61 行に
「敵に傷を書く駒（キリ・ノミ）と 敵に毒／火を撒く駒」が同席する行は 0 行**なのに対し、
**第85期の巻き込み則が既定なので味方の傷はいつでもある。**
**規則を対称に書いても、盤面の同席の分布が非対称なら効果は非対称になる。**
**第91期に訂正**——**これは機構の欠陥ではなく台の欠陥だった。**
`CompareBuilds()` の外に4台組むと**敵への滲みが 1.28〜2.93 回/戦 立ち、勝率も +0.70pt 動く**
（キリ×グザ 2.93 ≫ ノミ×グザ 1.71。**滲みは二値なので深さが要らない**という予測どおり）。

**拒否権の分母（主判定19行）は「組み合わせで壊れる行」を1行も含まない**（第90期。**第91期の入口**）。
滲み則の採用で `compare` は **18 セル / 11 行**動き、
**`追撃×毒 (ハギ×グザ)` の第二波は 87.5 → 29.0（−58.5pt）**なのに、
**主判定19行は 95 セルすべてが1ビットも動かず、拒否権は3つとも ○**だった。
実体は **ゴルムの吸い（毎ターン味方全員）が巻き込み則で全員に傷を書き、
その全員にグザの毒漏れが +1 ではなく +2 で入る**（毒の滲み 13.91 回/戦 ＝ 全61行平均の 48 倍）。
**主判定19行は「各軸の代表」を1行ずつ選んだ集合なので、
「毒の書き手 × 味方に傷を書く刃」という<u>組み合わせ</u>の行はそこに無い。**
**第91期に (G1)(G2) で直した**——分母を 61 行に広げ、落ちた行を「壊れ」と「制約」に分ける。
**`追撃×毒` を 61 行の分母で測り直すと 5枚の駒の「他の行」の平均は −0.13 〜 +0.00pt で、
拒否権は立たない**（駒は1枚も使えなくなっていない）。

**第85期の巻き込み則（規則）と第90期の滲み則（規則）の交わりが、駒どうしの交わりより大きく出た。**
どちらも単独では小さいのに、掛け合わせると1行を 58.5pt 動かした
——**第88期 8-3（規則どうしの AND ゲート）の負の側の実例。**

**増分尺度のノイズ床は、機構が動かすセルの数が少ないほど下がる**（第90期。第89期の定義がまだ持つ穴）。
対にした設計では真の効果がゼロなら Δ は厳密に 0 になるので、
**機構がほとんどのセルを動かさないと意図しない相手の分布が 0 に潰れる**
——(P1) の床は **0.06pt**（50 体中 40 体以上が Δ = +0.00）で、Δ +0.35pt が「床の6倍」になった。
**「床を超えた」は「大きい」ではなく「他の誰も動いていない」を意味することがある。**

**盤面ルールを 2×2 で測るときは、自己検査 (a)（A 素体のセルが版で動かない）を要求しない**（第90期）。
**駒に紐づく機構（`GatherRule`）は A がいなければ1回も走らない**のでセルが一致するが、
**engine の規則（`SoakRule`）は A を素体にしても走る**（実測で キリ 301 / ノミ 296 セルがずれる）。
**選別そのものは汚れていない**——情報帯は **W0 のセルだけ**で判定していて処置の結果では選んでおらず、
**Δ相乗 ＝ 相乗(W1) − 相乗(W0) は差の差なので `y00` / `y01` の共通の動きは打ち消える。**
**代わりに「情報帯の選別に W0 のセルしか使っていないこと」を自己検査に置く。**

**傷の引き取り（`GatherRule`）を採用した**（第89期に作り、第90期 (P1) で通った）。
ガルドが庇いのたびに隣の味方の傷をひとつ引き取る。**採用しても盤面は1セルも動かない**
——`compare` 61 行に「ガルドと味方の傷の読み手（縫いのハリ）が同席する行」が **0 行**しかないため。
**この「1セルも動かない」は第92期に終わった**——繕いの傷読み（`MendRule`）を採用してノノが
**味方の傷の2枚目の読み手**になったので、`耐久 (ガルド×ノノ)` で引き取りの下流が繋がる。
**採用時に盤面が動かない機構でも、後から読み手が増えれば動き始める。**
**あわせて引き取り先の同数のタイブレークを `ctx.PickOne` から席番号の昇順に直した**
（第89期の自己検査 (h) の訂正。**機構の変更ではない**）——直すと `compare` の Z0 対 Z1 が **0 件 / 0 行**になり、
**第89期の 17 セル / 10 行は全部が乱数のずれだったことが確定した。**

**傷は貯金ではなく脆弱性でもある**（第90期・topology の記録）。
第85期までの傷は「後で殴るための貯金」で、**持ち主が落ちれば投資が消えた**（未読 味方 84% / 敵 78%）。
滲み則の払い出しは**その場で使い切られる**ので消えない。
**ただしこの盤面では、その脆弱性は自陣にしかない**（上の「片側でしか働かない」）。

**増分尺度のノイズ床の定義（第89期から規約）。** 第88期の陰性対照（同じ版を台の抽選だけ変えて回す）は
**相乗（水準）の揺れ**を測っており、当てる先の **Δ相乗（増分）**とは尺度が1桁違った（5.61pt 対 0.21〜1.44pt）。

> **増分尺度のノイズ床 ＝ 同じ実験の中の「意図しない相手」の |Δ相乗| の 95 パーセンタイル。**
>
> **対にした設計（同じ台・同じ席・同じ戦闘 seed）では、真の効果がゼロなら Δ は厳密に 0 になる**ので、
> 「同じ版を2回」では増分の陰性対照が作れない。**実験の外から物差しを持ってこられない。**

**この定義のもとでは Q1-2（意図しない相手で床を超える体数 ≤ 3）は飾りに近い**
——床が意図しない相手の分布の 95%tile なので**構成上おおよそ 5% が必ず超える**（48 体なら 2.4 体）。
**判定の中身は Q1-1、すなわち「意図した相手が意図しない 95% の相手より上に立つか」という順位の検定である。**

**`confirm` は「今この行の席が古いか」を1つも答えない**（第89期に判明・**第88期の記述を訂正**）。
`confirm` の候補表は **`Program.cs` に焼き付けた (行名, 旧配置, 候補) の台帳**で、
**各期に提案された配置がそのまま残っている**——採用済みの行も消していない。
実際に回すと 13 行が「採用」と出るが、**現行の `CompareBuilds()` はすでにその候補の席になっている。**
**あれは過去の決定を今の盤面で測り直した記録である。**

> **生きた判定は `docs/reseat.md`（`reseat` の出力・seed 0..199）から取る**（`gather seats`）。
> 61 節のうち **38 行は作法1（現行が上位5位以内）でそこで終わり**、14 行は差が 5.0pt 未満、
> **追試にかかるのは 9 行**。seed 200..599 で測り直して 8 行を採用した（第89期）。

**席の候補は「平均1位」ではなく「情報セルを 2 以上に保つ最上位」を採る**（第59期の作法を第89期に全行へ広げた）。
`reseat` は勝つ席を探す道具であって**測れる席を探す道具ではない**（第50期）。実測で効いたのは2行:
**`逆しま改 (クビ×ウツ)` は 5.0pt を超える候補 12 通りが全部 情報セル 2 未満**（現行 1 → 0）で据え置き、
**`死軸×ホタ` は1位（+6.8pt・情報セル 1）を外して2番目（+5.8pt・情報セル 2）**を採った。

**ミオの着火（`IgniteRule`）を採用した**（第87期に測り、第88期に落ち、**第89期に別標本で再判定して通った**）。
`TableSeed` を 8,900,000（第87・88期はどちらも 8,100,000）に変えた標本で、
**裂きのキリが 50 体中 1 位・Δ +1.59pt**（増分尺度のノイズ床 0.37pt）・2系列とも正。
**`compare` は 0 / 61 行しか動かない**——ミオを含む4行はすべてグザ（瘴気）同席で、
毎ターン敵全体に毒を撒くので「傷を持ち毒を持たない敵」が存在しない（第87期の設計どおり）。
**ミオはキーを2本持つ最初の毒軸の駒**になった（`{Poison}` → `{Poison, Wound}`）。**第80〜83期の派生値は動く。**

**傷軸に足りないのは深さではなく単価だった**（第89期・**紙で止めて 2×2 を1戦も回していない**）。
廃棄聖騎士ガルドが庇いのたびに隣の味方の傷をひとつ引き取る版（`GatherRule`）を、
**源（巻き込み則の書き手6枚）→ 中継（ガルド）→ 終端（縫いのハリ）**の3段として測った。
**鎖は全部つながっている**——3段を手でそろえた台で 隣に傷のある率 **99.9%**・引き取り **1.26〜1.53 回/戦**・
**ガルド1体の傷の深さ 平均 2.69・最大 8**・ハリが読む糸口の深さ **2.53 → 3.05**・勝率 **+3.4pt**。
**それでも紙のスループットは 0.82〜0.90%（線 5%）。ドラフト台では 0.15%。**

    1回の庇いで動く傷 1 つ × PerWound 3 点 × 引き取り 1.26 回/戦 = 3.8 点
    総被ダメージ                                                 = 460 点/戦

**第84期の「傷の読み手は加算（3点/傷）なので誰が書いても値段は同じ」が、書き手ではなく<u>集め手</u>の側で出た。**
**深さは作れる。単価は動かない。** `GatherRule` は既定 `false` のまま対照として残置。経緯は design/PHASE89_GATHER.md。

**鎖の3段は5枠盤では3枠を要求する**（第89期）。ドラフト台（A 1枠 ＋ B 1枠 ＋ 埋め草3枚）で
**確実に置けるのは2枠まで**なので、終端は引かれる確率でしか入らない
——実測でハリの味方糸口は**ドラフト台 0.20 回/戦 対 3段そろえた台 4.62 回/戦（23 倍）**。
**第87期「接続子が両方とも出力ゼロの軸どうしは5枠盤では橋を架けられない」の一般形で、
こちらは出力ゼロではなく<u>枠数</u>で詰まっている。**

**発火条件に列を要求する機構は、隣接を広く読めない**（第89期）。
**庇いは前列でしか成立しない**（`SelectTargetChain`: `f.HasTrait(Guardian) && f.Row == Row.Front`）が、
**前列の2枠はどちらも次数2の角**（第45期）。**中継の発火条件と供給口の広さは両立しない**
——実測で**ガルドを中央（次数4）に置くと庇い成立が 1.53 → 0.00 回/戦**になり、引き取りが1回も走らない。

**集約の値段は「動かした回数 × 単価」より大きい。在庫は読まれるたびに効くから**（第89期・自己検査 (g) が 1.59 倍で落ちた）。
「分子は線形」と測る前に宣言して外した——**1つ集めた傷が作る深さは、その後の終端の<u>すべての</u>読みに乗る**
（ガルドが深さ 2.69 を保つと、ハリが引く 4.62 回すべてで読む深さが上がる）。
**線形の紙は下限になる。それでも 1.59 倍では線に届かない。**

**`ctx.PickOne` は「読み手がいなければ盤面は動かない」を壊す**（第89期）。
同数のタイブレークで `Roll` を1つ消費するので、**通貨を移すだけの機構でも乱数列が動く**
——実測で `compare` **17 セル / 10 行**（全部がガルドを含む行・含まない行は 0）。
**「その通貨の読み手が同席しないから `compare` は不変」という予測を立てるときは、
選択に乱数が入っていないかを先に見ること。**


**新機構の判定規約（第88期に作り替えた。以後の指示書はここを引く）。主判定は特異性、拒否権は大きさ。**
第84〜87期の否決は4期とも同じ線（**Δ相乗 ≥ +3.0pt**）で落ちたが、**その線は第80期の
「組の相乗の<u>水準</u>」の分布から引いたもので、第84期以降が測っているのは「片方の駒に規則を1本足した<u>増分</u>」**
——**水準の分布から引いた線を増分に当てていた。第84〜87期の否決のうち少なくとも一部はその線の産物である。この記述を消さない。**

    情報帯   `y00`（両方素体）と `y01`（A 素体）の**両方が 0.0%**、または**両方が 100.0%** の台を分母から外す
             **A 本物のセル（`y10` / `y11`）は選別に使わない**（処置の結果で標本を選ばないため）
             情報帯が **20 台未満**の組は「測れていない」と書き、判定に使わない
    主判定   Q1-1 意図した相手のうち**少なくとも1枚**が 2系列とも正 かつ |Δ| > ノイズ床
             Q1-2 意図しない相手で |Δ| > ノイズ床 の体数が **N3（陰性対照の偽陽性の期待数）以下**
             **大きさは記録するが線にしない**（最優先は「新しいシナジーが生まれるか」＝噛み合いが意図した場所にだけ立つか）
    副判定   Q2 意図した組の順位が上位10位以内 ／ Q3 負の側で床を超えた体数と顔ぶれ
             (A) 発火回数と**稼働率**（発火 ÷ 決着T）／ (B) 持続係数（**単独では採否を決められない**）
             (C) 台の乖離（**理想台にも情報帯を当ててから**）
    拒否権   (1) 主判定19行の第五波平均が `Baseline.PrimaryFifthFloor` を下回る
             (2) `compare` で第五波 95% 超の行が新たに2行以上 (3) 主判定行がいずれかの波で −10.0pt 以上落ちる

**ノイズ床は 3.26pt（フィルタ前）/ 5.61pt（フィルタ後）・N3 = 3**（第88期・A ＝ ミオ・seed 0..7）。
**この値をそのまま次の期に使わない**——下の 8-2 の理由で、**測り直すべき量である**。

**情報帯は分散を減らす道具ではない。「0 で薄めるのをやめる」道具である**（第88期。予測を外した）。
落ちる台は**床（4セルとも 0%）と天井（4セルとも 100%）**で、**そこでは相乗もΔも定義上ちょうど 0**
——**0 だけを大量に捨てれば残った分布の分散は上がる**（ノイズ床 3.26 → **5.61**・1.72 倍）。
**同じ理由で Δ 自身も 1.4〜1.7 倍になる**（ゴルム +5.25 → +5.74 / キリ +0.98 → +1.40）。
**分子と分母が同じ向きに動くので、「フィルタで救われるか」は Δ だけを見ても決まらない。**

**対にした設計では、陰性対照を「同じ版を2回」では作れない**（第88期 8-2。**物差しを置き換えた側で同じ間違いが再発した**）。
2×2 の Δ は**同じ台・同じ席・同じ戦闘 seed の対**なので、engine が seed 決定的である以上
**真の効果がゼロなら Δ は台ごとに厳密に 0 になる**——「効果ゼロの標本ゆらぎ」というものが存在しない。
だから台の抽選を振って対を壊すしかなく、**壊した瞬間に測る量が「相乗（水準）」へ戻る。**
実測で尺度は1桁違う——**ノイズ床 5.61pt に対し、同じ実験の中の意図しない相手の |Δ| の 95%tile は 0.21 / 1.44 / 0.32pt。**
**増分の物差しは、同じ実験の中の「効くはずのない相手」の分布から取ること。**

**2×2 の分母の厚み（床の割合）は A の体の強さの関数である**（第88期・自己検査 (d) が落ちた）。
情報帯の割合は **カド 65.2% / ハリ 70.1% / ミオ 56.5%**（床は 25.7 / 25.6 / **41.5%**）。
台の抽選は (組, 引き番号) だけで決まるので **A に依存しない**——依存するのは**その台が生き残るか**のほうで、
**攻撃しない駒（ミオ）を1枠に固定すると 5枠のうち1枠が出力ゼロになって台が床へ落ちる**。
**機構どうしを Δ の大きさで横に並べるときは、この差を先に出すこと。**

**規則どうしにも AND ゲートがある。「読み手を広げる規則」と「供給を作る規則」は必ず対で測る**（第88期 8-3）。
`SutureRule.Both`（両側読み）**単独の Δ相乗 は 50 体 × 128 台 × 4 セルすべてでちょうど 0.00**
——味方に傷が載る経路が他に1本も無いので味方側の候補が常に空で、分岐が毎回素通りする。
`SpillWoundRule`（巻き込み則）**単独は符号が負に傾く**（読み手がいないので味方の傷は代金だけ）。
**両方で +5.74。第57期の `燃焼 (ボルグ×ホタ)` の交互作用 −52.3pt と同じ形が、駒どうしではなく規則どうしのあいだで出た。**
**片方ずつ測って両方落とすのが、この形の典型的な落ち方。**

**大きさの線を外したら、量が最小の案が通り、最大の案が落ちた**（第88期）。
第87期は出力を総与ダメの 8.3% 増やし持続係数 17.97・稼働率 45.0% を出したのに落ち、
第85期は紙の下限にぴたり乗って律速（ハリの振り 2.15 回/戦）が1回も動かないまま通った。
違いは**特異性**——第85期は **50 体の1位と2位を、意図した6枚のうち供給が最も密な2枚**
（毎ターン全味方の吸い＝ゴルム +5.74 / 攻撃ごと隣接の余波＝ボルグ +3.12）**が占め、意図しない 44 体は1体も床を超えない**。
第87期は 2位・3位で、**1位は意図していない疫みのラウ**（+1.51）。
**「新しいシナジーが生まれたか」を測るなら、Δ の大きさではなく Δ の並び方を見る。**

**縫いの両側読みと巻き込み則を採用した**（第85期に測り、**第88期の規約で採否が覆った**）。
`SutureRule.Default` = `Both` ／ `SpillWoundRule.Default` = `true`（`Scope` は `All`）。
**新しい `TraitId` ゼロ・駒ゼロ・engine の新しい窓口ゼロ・`CompareBuilds()` の行も席も動かしていない。**
`docs/` で動いたのは **`刻み×縫い (ノミ×ハリ)` の1行だけ**（第3波 79.5 → 81.5 / **第4波 47.0 → 88.5** / 第5波 12.0 → 7.5・平均 67.7 → **75.5%**）。
拒否権1〜3 はすべて ○（**主判定19行にハリを含む行は 0 行**なので拒否権1・3 は構造的に立ちようがない。それを承知で記録する）。
**採用後に `reseat` を測り直すと現行席は 1位から 3.7pt 落ちる**が、**閾値 5.0pt の内側なので動かさない。**
（**第88期は `confirm` の +6.5pt を「未適用の推奨」と読んで次期の入口に置いたが、これは誤り**
——`confirm` の候補表は焼き付けた台帳で、その +6.5pt は**第85期に提案されその期に適用済み**の配置の記録だった。
第89期に訂正した。下の「`confirm` は『今この行の席が古いか』を1つも答えない」を見ること。）

**第84期（棘に傷）は3つの読みすべてで落ちた。この案は閉じる。**
**第87期（`WoundIgnite`）は §4 の規約では落ちたが、増分の物差しでは 2/2 で通る**——
**ノイズ床を測り直したら、まずこれを当て直すこと。** `IgniteRule` は既定 `false` のまま対照として残置。

**`SpillScope.Dense`（第86期 X2・密度の低い書き手を外す版）は未測定。**
第85期 表D の順位（値段は供給の周期と単調）が、外す側の候補をそのまま並べている
——**開戦時1回（生贄）・死亡時（破裂）・毎ターン遅い味方（置き去り）。**


**持続係数を 18 倍にしても勝率は +1.0pt しか動かなかった —— 天井は払い出しの形ではなく「出力 → 勝率」の変換率だった**
（第87期・**測って採用しなかった**。**新しい `TraitId` ゼロ・駒ゼロ・engine の窓口ゼロ・`UnitCatalog` の変更ゼロ・
最後の1枠は使っていない・`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件・`compare` 305 セル 0 件**）。
澱みのミオ（`AmplifierTrait`）に「毒が無くても、傷のある敵には毒が回り始める」を足す版（`IgniteRule`）を、
第81期の 2×2（**A ＝ ミオ固定**・B ＝ 残り 50 体）で測った。**ノブは対照として残置**
（オゴ・ゴウ・ヌキ・オノ・`ThornRule`・`SutureRule`・`MendRule` と同じ扱い）。経緯は design/PHASE87_IGNITE.md。

    Phase 0 停止条件1 傷由来の毒ダメ/戦 ÷ 総与ダメ/戦 ≥ 5%   **5.0%**（線ちょうど。正しく直すと 8.3%）  ○
    Phase 0 停止条件2 持続係数 > 2.0                       **8.93**（紙）／実測 **17.97**              ○
    Q1  Δ相乗(ミオ, キリ) ≥ +3.0                          **+0.98**（系列 +0.63 / +1.32・**2 / 50 位**） **×**（主判定）
    Q2  キリ > ノミ（執着は1体しか着火しない）                +0.98 > +0.32                             ○
    (A) 読み手の発火/戦（稼働率）                            **2.54（45.0%）**。ハリ 2.15 の 1.18 倍     上回った
    (C) 台の乖離（理想 ÷ ドラフト・中央値）                   **0.8×**（第85期は 30×）                   揃わなかった

**払い出しの持続係数は天井の原因ではなかった**（第87期の本体。第84〜86期の仮説の反証）。
**持続係数 ＝ 1回の発火が生む累積出力 ÷ 同じ発火が生む即時出力**（第87期に新設・以後常設。
即時出力が 0 の機構は分母に「その発火が盤面に置いた層の1ターンぶんの刻み」を使う）。
**抉り・縫い・継ぎ当ての3機構は 1.00**——どれも発火の呼び出しの中で `ApplyDamage` / `ctx.Heal` が1回走って
盤面に残るものが無いので、累積と即時が**同じ台帳**になる。着火はこれを **17.97** にしたが、勝率は動かない:

    出力の増分   実測の毒ダメ 19.4/戦 ÷ Y0 の総与ダメ 233.5/戦 = **+8.3%**
    勝率の増分   y11 が 14.4% → 15.4% = **+1.0pt**

**律速は「出力 → 勝率」の変換率**で、第24期 `yield` の「天井・床のセルでは誰に注入しても 0 に潰れる」が
新機構の採否の側で出た形。**ドラフト台の 2×2 は y00 が 0% の台が厚いので、出力を1割増やしても床から出ない。**
**5例（棘 +0.04 / ハリ +1.10 / ノノ 紙で停止 / 着火 +0.98 / オノ +1.2〜1.5）が同じ場所で止まっている以上、
疑うべきは払い出しでも読み手でも供給でもない。**

> **副判定 (B)（持続係数）は単独で採否を決められない**（第87期が反例）。
> **持続係数は「その機構が何回ぶんの出力を生むか」しか測っていない。「その出力が勝率に変わるか」は測っていない。**
> **波の長さの関数でもある**（第2波 17.52 / 第4波 30.85 / 第5波 2.84。累積は生存 L に対して **2L² − L**）
> ——**出すときは決着ターン数を必ず併記すること。**

**供給も読み手も詰まっていない**（第87期）。**着火は「着火できる敵」の 100% に届いている**
（波 × 書き手の 8 行すべてで 着火/戦 ＝ 着火できる敵の実体数/戦）。稼働率は **45.0%** で、
**第86期の 35〜40% を初めて上回った**（ハリ 2.15 の 1.18 倍・ノノ 2.10 の 1.21 倍）。**それでも足りない。**

**紙の分子が二次の量なら、1ターンの丸めが 2 倍の誤差になる**（第87期・自己検査 (h) が 1.66 で落ちた）。
累積 = Σ(1 + 4k) = **2L² − L** なので、L = 2.26 → 7.96 に対し L = 3.26 → 17.99（**2.26 倍**）。
着火の時刻をターン頭のスナップショットで見ると**1ターン遅れる**ので紙は下限になる。
**第85期は線形（回復 ＝ 回数 × 単価）だったので下限にぴたり乗った**——
**紙のスループットを線で挟むときは、丸めの誤差が分子の何乗に効くかを先に見ること。**

**「積み上げ系には上限が天井として効く」（第25期）は、積み上がる前に決着する台では成り立たない**（第87期）。
軛（第四波・単発上限 25）は毒の刻みを**1点も切らなかった**——層が 25 に届くのは着火から **7 ターン目**で、
着火後の生存は **2.26T** しかない。**上限の効き方は上限の値ではなく、上限に届くまでの時間で決まる。**

**執着は「向き」を作る**（第87期・Q2）。毎ターン的を変えるキリは着火できる敵を **1.45 体/戦**（初出T 1.01）作り、
1体に食いつくノミは **0.68 体/戦**（初出T 1.96）。**Δ相乗も +0.98 > +0.32 で予測どおり。ただし差は 0.66pt。**

**毒軸と傷軸は別の入口として並ぶ**（第87期）。グザ（瘴気）同席の `compare` 4 行は着火が **0 回**
——毎ターン敵全体に毒を撒くので「傷を持ち毒を持たない敵」が存在しない。**欠陥ではなく設計どおり。**
**ミオを含む 4 行が全部グザ同席で、キリ／ノミとの交わりは 0 行**なので、`compare` 61 行は1セルも動かない。

**下流の読み手は実在した。台が床にあるので勝率に出ない**（第87期・Q4）。理想台
`キリ×ミオ×ベニ` / `キリ×ミオ×ラウ` は着火由来の毒ダメ **72.6 / 55.8 /戦**・持続係数 **47.1 / 37.2**・
ベニの回復 2.8 → **18.5**（6.6 倍）・ラウの拡散 0.00 → **0.76** なのに、**5波とも 100/0/0/0/0**
（「台が死んでいる」）。**キリ（与ダメ常に1）＋ミオ（攻撃しない）＋ベニ／ラウ（攻2〜3）で払い出しが3枠**
——**予算は1枠ずつ返しても戻らない**（第29期）。**測れる台を先に作ってから読み手を測ること。**

**「版に依らない計数」は経路の性質であって、観測される値の性質ではない**（第87期・自己検査 (b)）。
計数を規則の分岐の**手前**に置けば同じ盤面では必ず同じ数だけ通る（コードの形から従う）が、
**盤面自体が版で分岐する**ので観測値は 1.01% ずれる。**厳密一致を要求するなら、盤面が動かない版（第86期の X1P）か
素体の台（この期の (a')）で見ること。**

**`ctx.Ignite` は燃焼の着火（メソッド）で埋まっている。** 傷口の着火の規則の窓口は **`ctx.WoundIgnite`**
——**新しい規則を通すときは、名前が engine の既存メソッドと衝突しないかを先に見ること。**


**読み手を手番の要らない駒に替えても、発火回数は 1 回も増えなかった —— 繕いに傷を読ませる案は紙で止めた**
（第86期・**紙のスループットで停止。2×2 は1戦も回していない**。**新しい `TraitId` ゼロ・駒ゼロ・`UnitCatalog` の変更ゼロ・
最後の1枠は使っていない・`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件・`compare` 305 セル 0 件**）。
継ぎ当てのノノ（`MenderTrait`）が繕う相手の傷を読む版（`MendRule`）を、第85期の巻き込み則（`SpillWoundRule`。
書き手を絞る段 `SpillScope` を追加）と対にして測る計画だったが、**Phase 0 の停止条件2つの両方で落ちた。**
**ノブは3つとも対照として残置**（オゴ・ゴウ・ヌキ・オノ・`ThornRule`・`SutureRule` と同じ扱い）。経緯は design/PHASE86_MENDER.md。

    停止条件1 傷由来の増分/戦 ÷ 総被ダメ/戦 ≥ 5%   **2.0%**（7.7 ÷ 392.8。最良のナラの行でも 3.6%）  ×
    停止条件2 ノノの発火回数/戦 > ハリの 2.15      **2.10**（**上回らないどころかわずかに下回った**）   ×

**「発火口の条件を1本減らす」は「発火回数を増やす」ではない**（第86期の本体）。
ハリは「手番がある **かつ** 殴った相手に傷がある」の積、ノノは「手番がある」だけ（`Actions = [Skill]`・繕いが手番そのもの）
なのに、発火は **2.10 対 2.15** で差が 0.05 回/戦しかない。**両方とも決着ターン数に律速されているから**である
——**稼働率（発火 ÷ 決着T）は4波とも 35〜40% に張り付き、波の長さが 4.9T → 7.0T と 1.4 倍になっても比が動かない。**
**第85期の「ハリが 2.15 回しか振れない」は、条件が揃わないからではなく手番がそれだけしか来ないからだった。**

> **副判定 (A)（読み手の発火回数）には「稼働率 ＝ 発火回数 ÷ 決着ターン数」を必ず併記する**（第86期から）。
> 回数の絶対値は台の長さに依存するが、稼働率は依存しない。

**5例のうち通ったのは第74期だけで、違いは「手番を捨てていたか」**——
ナタ（3.40T を手番ごと捨てていた → 待ち方を直して +5.15pt）だけが取り戻す余地を持っていた。
オノ（第79期）・カド（第84期）・ハリ（第85期）・ノノ（第86期）は**捨てていない。最初から手番が来ていない。**

**「既存の駒に読み手を足す」案は、その駒の現行の出力が分母の何割かを先に数える**（第86期・7-2）。
**総被ダメ 392.8 に対してノノの現行の回復は 50.2（12.8%）**なので、線 5% は「読み手1枚の出力を 40% 増やす」と同義。
傷1つ 3 点 × 深さ 1.89 × 率 64.2% では **26%** にしかならない。**紙のスループットの式に、分母に対する読み手の取り分を1行足す。**

**落ちたのは供給側ではない**（第84・85期と同じ）——味方傷 11.31 回/戦・在庫 4.05/T（第85期のカド単独より厚い）・
**患者に傷があった率 64.2%**（予測「6割以上」はゴルム 63.2% / ボルグ 76.4% で当たり、**カドだけ 42.4% で外した**）。
**書き手の順位は供給の周期と単調**（第85期 表D の再現）——
毎ターン・遅い味方（ナラ 3.6%）＞ 攻撃ごと（ボルグ 2.3%）＞ 毎ターン・全味方（ゴルム 2.2%）＞
**開戦時1回（リィカ 1.3%）**＞ 被弾ごと（カド 1.1%）＞ 死亡時（ゾト 1.0%）。

**開戦時1回の供給は在庫を作るが深くならない**（第86期の新しい形）。リィカは**率 82.1% で6枚中2位なのに、
深さ 1.30 で最小・増分は下から2番目**。第67期「開戦時の一括は粒度を作らない」の深さ側で、
**「傷があるか」と「傷が深いか」は別の量。率だけを見て供給を評価しない。**

**紙の分子を実測にするために、盤面を1ビットも動かさない計数版（X1P）を先に置く**（第86期の器具）。
**指示書の「分子の材料は全部リポジトリにある」は誤り**——第85期が持つのはハリの糸口の深さで、
**ノノの患者の傷の深さを数えた計数は1つも無かった**。`MenderTrait` に**版に依らない観測**（`wSeen`）を置き、
**巻き込み則だけを入れて読み手を入れない版**で分子を取った。
**`compare` 305 セルで X1P と X0 のずれ 0 件**——**紙の分子を取った盤面が対照と同一であることの証拠**で、これがこの期の検算そのもの。

**採用時にしか駒の `Traits` を触らない**（第86期）。塞ぎ（`TraitId.Seal`）をノノに足すのは採用時の作業なので足していない
——**`dump` の特性表は `TraitId` を全数回すので、札を1枚足しただけで `docs/units.md` が動く。**
（その結果 `check` の X1 は塞ぎが走らない＝**採用版より甘い上限**だが、その上限でも動いたのは 10 セル・5 行で、
**主判定の2行はどちらも下がった**——`耐久 (ガルド×ノノ)` 94.1 → 93.1・`燃焼 (ボルグ×ホタ)` 72.9 → 70.6。採否には使っていない。）


**紙の時点で線を挟み、測っても届かなかった —— 縫いのハリの両側読み（糸を味方にも通す）は採用しなかった**
（第85期・**測って採用しなかった**。**新しい `TraitId` ゼロ・駒ゼロ・最後の1枠は使っていない・`docs/` は8ファイルとも再生成して差分 0 バイト・
`audit` ずれ 0 件・`compare` 305 セル 0 件（W0 対 docs・W0 対 W1）**）。
ハリの糸口の候補を「殴った相手」から「殴った相手か、傷がいちばん深い味方か、深いほう」へ広げる版（`SutureRule`）を、
味方の傷の書き手（W1 ＝ 棘の巻き込み `ThornRule.Both` だけ／W2 ＝ **巻き込み則 `SpillWoundRule`**＝味方の刃 6 枚）と対にして第81期の 2×2 で測った。
**ノブは3つとも対照として残置**（オゴ・ゴウ・ヌキ・オノ・`ThornRule` と同じ扱い）。経緯は design/PHASE85_SUTURE2.md。

    停止条件 紙の回復/戦 ÷ 総被ダメ/戦 ≥ 5%   **下限 2.0% ／ 上限 6.0%**（線を挟む。実測は下限にぴたり乗った）  下限で ×
    Q1a W1: Δ相乗(カド, ハリ) ≥ +3.0        **+1.10**（系列 +0.83 / +1.37）                                   ×（主判定）
    Q1b W2: 6枚の Δ相乗 の平均 ≥ +3.0        **+1.30**（ゴルム +5.25 / ボルグ +2.54 / ナラ +0.63 / ゾト +0.17 / カド +0.12 / リィカ −0.90）  ×（主判定）
    Q4  カド × ハリ × ナタ で閾値 2 に届く     0.00 → 0.01 回/戦（敵側在庫は 0.57/T まで積む）                   動かない

**律速は供給でも在庫でも読み手の発火でもなく、読み手の手番の数だった。** 紙の式 `min(ハリの振り/戦, 供給/戦)` で振り（**2.15 回/戦**）が
供給（6.48 傷/戦）を下回り、実測の回復 6.4/戦 は下限（深さ 1）にぴたり乗った（比 0.99・自己検査 (h)）。
勝てる第3〜5波は素体でも 2.8〜3.7T で決着して傷が積まれる前に終わり、負ける第2波はカドが粛で止まってハリが 69% 死ぬ。
**第84期の「傷1つの単価が小さい」を直しても（味方側は巻き込み 5 点に傷 1 ＝ 60%）、読む側の手番は増えない**
——第75期「条件が何に接続しているかは値段を決めない」の読み手側の版。

**紙のスループットの停止条件は今期から常設で、初めて機能した。** W1・W2 は 1 戦も回さずに落とせた計算である。
**式に「深さ」を持たないと線を挟む**——下限（深さ 1）と上限（供給が全部読まれる）で挟み、実測は下限に乗った。
**次からは下限を紙の値とする**（供給は読まれる前に決着に消える側に倒れる。読まれないまま落ちた傷は味方側 84%・敵側 78%）。

**巻き込み則の値段は供給の周期と単調で、密度が薄い供給は読み手が何枚いても値段にならない**（第85期・表D）。
毎ターン・全味方（吸い・ゴルム +5.25）＞ 攻撃ごと・隣接（余波・ボルグ +2.54）＞ 毎ターン・遅い味方（削り・ナラ +0.63）＞
死亡時（破裂・ゾト +0.17）＞ **開戦時1回（生贄・リィカ −0.90）**。第67・68期「時間に分布している量」の読み手側の帰結。
**理想台では別の答えが出る**——`刻み×縫い (ノミ×ハリ)`（ゴルム同席・軛の第四波で 6〜7T 振り続ける）は W2 で第四波 **47.0 → 88.5%**。
ドラフト台の 6枚平均で落としたので採らなかったが、**ゴルム・ボルグの 2 枚は線を越えている**（§5 の分岐に「2 枚で通った」は無い）。

**「`source` が同陣営」だけでは肩代わりの中継が外れない**（第85期・器具の記録）。巨躯・分かちの中継は `source` が**元の攻撃者**で、
元の刃が味方（棘の巻き込み・吸い）なら同陣営になる。**中継を外すには段に札が要る**（`ApplyDamage` の `relayed`・`BattleEvent.Relayed`。
`burnTick` と同じ計数専用の札）。自己検査 (c)（W1 の棘の書き込み ＝ W2 の巻き込み則の書き込み）が 2,962 対 3,104 でこれを捕まえた。

**2×2 の TSV の添字（`[y11, A素体, B素体, y00]`）は A・B の割り当てで意味が変わる**（第85期）。第84期は A ＝ カドだったので
「カド素体」＝ 添字 1、この期は A ＝ ハリなので「カド素体」＝ 添字 2。器具を写したまま自己検査 (a)(b) を書いて両方落ちた（3.1pt / 59.4pt）。
**器具を写すときは、A・B が入れ替わると自己検査の添字が変わることを確かめる。**

**`isFriendlyFire: true` の呼び出し口は 10**（第85期。指示書の「6箇所」は特性側の刃の数）——刃 6（余波・生贄・吸い・破裂・棘の巻き込み・置き去りの削り）
＋ 深追いの反動（ハギ・`source` null）＋ engine の中継 3（巨躯・分かち・転嫁の代金）。
**`compare` でハリを含む行は 1 行だけ**（`刻み×縫い (ノミ×ハリ)`）で、ハリ ∩ カド は 0 行。

**供給は出た。在庫は積んだ。読み手にも届いた。それでも相乗は動かなかった —— 棘に傷を載せる案は採用しなかった**
（第84期・**測って採用しなかった**。**新しい `TraitId` ゼロ・駒ゼロ・engine の窓口ゼロ・最後の1枠は使っていない・
`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件・`compare` 305 セル 0 件**）。
棘鎧のカドの反撃（`ThornsTrait.OnDamaged`）に「刺し返した相手に傷 1」を載せる版を `ThornRule`（`Run` の引数・既定 `None`）で作り、
第81期の 2×2（カド × 残り 50 体・K 64 × 2 系列）で V0 と V1 の相乗の差を測った。**ノブは対照として残置**（オゴ・ゴウ・ヌキ・オノと同じ扱い）。
経緯は design/PHASE84_THORNWOUND.md。

    Q1 傷の5枚（キリ・ノミ・エグ・ナタ・ハリ）の Δ相乗 の平均 ≥ +3.0   **+0.04**（−0.10 / +0.00 / +0.17 / −0.02 / +0.15）  **×**（主判定）
    Q2 供給/戦: 第四波 > 第二波                                  3.67 対 0.00（軛は打点を切るが傷を切らない・粛は止める）  ○
    Q3 敵側の在庫/T（ハリ同席で 1 を超えるか）                   0.00 → 0.47・最大のターン平均 0.96                   超えない
    Q4 読み手の発火                                              抉り 0.00 → 0.47・縫い 0.08 → 0.57・断ち 0.00 → 0.10 回/戦   —

**切れたのは Q2〜Q4 のどこでもなく、傷1つの単価とそれが落ちる波の位置。** 傷の読み手は加算（3 点/傷）なので誰が書いても値段は同じで、
**裂き（打点 1）が書く傷は打点の 3 倍だが、棘（打点 22〜45）が書く傷は打点の 7〜14%**——供給側の性質（手番を使わない）は
読み手の側の値段を 1 点も変えない（第75期「条件が何に接続しているかは値段を決めない」の供給側の版）。
しかもドラフト台で供給が落ちる第3・4波は素体でも 96〜100% で勝っている波で、増えた 1 点は決着ターンの短縮に消える（第24期）。
**「被弾に乗った供給」は、その被弾で既に決着が付く台では読まれない**——第57期の燃焼（遅すぎる）と逆で、こちらは速すぎる。

**第81期の「カド × キリ +24.93」は傷の噛み合いではなかった**（第84期）。キリは書き手であって読み手ではなく、
カドが傷を書いてもキリの出力は 1 点も変わらない（与ダメ Δ +0.00）——あの相乗は「薄刃の打点 1 を棘が代わりに出す」体と枠の話。
**組の表で「既に噛んでいる」と読める組でも、通貨を経由しているかは `TraitEntryMap` の分類（共有無し）のほうが正しかった。**

**指示書の「カドを含む行 8・傷を含む行 5」は古い**——現行 61 行では **11 行・8 行**（交わりは 0 行のまま）。

**反撃が死体に向くことがある**（第84期・器具の記録）。カドの `Traits` は `ThornGuard → Thorns` の順で、
入れ替えが軋み（ヨミ）の `OnMoved` 割り込みを呼び、**その割り込みが攻撃者を倒してから棘の反応が走る**
（4,778 件中 6 件）。傷は書かれない（死体には書かない作法どおり）。**「相手が生きていたか」を Log の行名で読むと、
同名の敵が並ぶ波（第四波の重装兵 ×2）で余波の行を取り違える**——`Events` から `InstanceId` で引くこと。
**相手が既に死体で余波も出なかった反撃は Damage を 1 件も積まない**ので、発火の回数は Log から数える（8,718 対 8,715）。

**縫いの天井 1 は、供給を 40 倍にしても崩れない**（第84期・第39期の算術の追試）。ハリ同席の敵側在庫は
供給 0.08 → 3.4 回/戦で 0.00 → 0.47 /T・最大のターン平均 0.96。

**味方に傷が載っても盤面は 1 セルも動かない**（第84期 V2・25,600 + 305 セル）。味方傷 6.00 回/戦 が載って読み手 0。
「載ったが誰も読まなかった」は両側（載った回数 > 0 と セル差 0）を揃えて初めて言える。

**物差しを引き直したら、引き直した物差しのほうが再現しなかった —— 主判定は 43 / 51 で落ちた**
（第83期・調査。**新機構ゼロ・駒ゼロ・差し替えの実行ゼロ・転生の実行ゼロ・最後の1枠は使っていない・
`TraitId` ゼロ・engine の変更ゼロ・`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件・
`compare` 305 セル 0 件**）。**第82期の 2×2 のデータをそのまま使い、線だけを引き直した**
（設計目標「同じ軸で固めれば最良、というゲームにはしたくない」に合わせて、横軸を単独の帰属から相乗の広さへ）:

    広さ(A)       = |{ B : 相乗(A,B) > 2×SE }|                      有意な正の相手の数
    独立の広さ(A) = そのうち KeysOf(A) ∩ KeysOf(B) = ∅ の相手の数   ← 主判定
    残す 独立の広さ ≥ 3 ／ 転生 広さ ≥ 3 ／ 差し替え それ以外 ／ 床 単独 < −1.5 なら差し替えへ

**残す 31 / 転生 6 / 差し替え 14**（第82期は 31 / 13 / 6 / 別扱い 1）。経緯は design/PHASE83_BREADTH.md。

    Q1 器具の再現（別実行の `pairs2` と）  上位 30/30・下位 30/30・**最大差 0.0000000000pt**（163,200 セル）  ○
    Q2 第82期の3分から 15 体以上が移る     15 / 51（線ちょうど）                                          ○
    Q3 **独立2系列で群が一致する駒 45/51**  **43 / 51**（第82期の物差しでは 48 / 51）                      **×**（**主判定**）
    Q4 切れる駒                            **1 体**（縫いのハリ）／候補 14 体                              —
    Q5 共有無しの組が上位 30 に 5 組以上    13 組（**一様期待値 26.1**）                                   ○
    Q6 埋め草を変えても上位 30 が 20/30     23 / 30（**ただし駒の3分は 40 / 51**）                          ○

**数え上げ型の指標に、標本を半分に割る再現テストを当ててはいけない**（第83期）。
**「広さ」は 50 回の有意判定を数え上げた量なので標本数に依存する**——系列（64 台）は合算（128 台）より
有意の線（2SE）が厳しいので、**広さの平均が 9.8 → 7.9 / 7.2 と縮む。**
**割れた 8 体は全部が線 3 の境界**で、**床で割れた駒は 0 体。**
**第82期の「単独」は標本数に依存しない平均なので、割っても同じ器具のままだった**（r = 1.000 対 独立の広さ 0.910）。
**2系列で「有意な正」の判定が一致したのは、どちらかで立った 255 組のうち 129 組（50.6%）。**

**`TraitKeyMap` の積集合で「独立」を定義すると、1,275 組の 87.1% が独立になる**（第83期・自己検査 (a)）。
**キーを1つも持たない駒は 8 / 51 体**（リィカ・ドルガ・ムグ・ヴェル・ノノ・ササ・ハギ・ナラ）で、
**その 8 体は定義上すべての相手が独立**になるため **独立の広さ ＝ 広さ**、**上位 10 位のうち 5 体をこの 8 体が占める。**
**「軸をまたいで噛む駒を上げる」つもりの軸が、「そもそも軸に載っていない駒」を上げた**
——第72期「キーは通貨の名前であって駒の値段ではない」の分母側での再発。
**キーを持つ 43 体では 独立の広さ ÷ 広さ の中央値は 0.71。**

**下の群を作ったのは主判定ではなく床のほうだった**（第83期）。**差し替え 14 体のうち 12 体は
床（単独 < −1.5）で落ちたもので、広さの線だけなら 12 体とも「残す」**。
**広さの線だけで落ちたのは 縫いのハリと引き受けのウケの 2 体**（**51 体中 49 体が広さの線 3 を超える**）。
**ハギ 12・キリ 14・クビ 15・ヒヨ 14 は「誰とも噛まない駒」ではなく「1枚で置くと編成を弱くする駒」。**

**理想台を分母にした拒否権は、平均で見ても最悪の行で見ても 40 体以上に立つ**（第83期）。
第82期の拒否権1（理想台とドラフト台で群が割れる・**15 / 51**）を
「素体にすると `CompareBuilds()` の行が 5.0pt 以上落ちる行が1行でもある」に置き換えたら **42 / 51**
——**指示書の「実際に壊す駒でしか立たない」は否定された。**
`最大落ち` の中央値は **31.00pt** で、**線を 40pt に上げても 23 体が残る。**
**第82期の「この 86% を放置したまま拒否権1 を使い続けると差し替えは永久に実行できない」は、
器具の側を直しても解けない。分母（61 行）の側を触るしかない。**

**切れる駒は 縫いのハリ 1 体**（第83期。第82期は 0 体）。**在席行 1 行・供給するキー無し・最大落ち +3.75。**
**作ったのは新しい主判定ではなく拒否権の入れ替えのほう**——ハリは第82期も差し替え候補で、旧1 だけで止まっていた。
**同じ +3.75 が、旧1 では「働いている証拠」・新1 では「壊さない証拠」になる**（在席行が 1 行なので両者が同じ数になる）。

**盤面は現在「軸を固める」側に寄っている**（第83期・軸ゲーの監視指標。**この期から毎期出す**）。
**同キーの組は組数の 12.9% で上位 30 の 56.7% を占める（4.4 倍の濃縮）**・
**有意な正の割合 37.0% 対 17.1%**・**相乗の平均 +2.10 対 +0.24。**
**ただし共有無しの組も上位 6〜9 位を占める**——カド×ハギ +31.88 / リィカ×ヴェル +30.79 /
クビ×ハギ +29.54 / カド×キリ +24.93 で、**この4組は `CompareBuilds()` の 61 行に行として存在しない。**

**判定の線は、その線が読む分布の期待値と一緒に置くこと**（第83期）。Q5 の線（上位 30 に 5 組）は
**一様期待値 26.1 組の 5 分の1**で、**通ったこと自体には情報が無い。**
第59期「判定式の条件節が分母の何割を選ぶかを実装前に数える」の**上側の版**。

**「広さ」は台の難度の関数でもある**（第83期・埋め草の偏り）。埋め草を規則 P から無作為（規則 R）に変えると
**床が 39.1% → 57.7% に増え、有意な組が 446 → 505 に増える**。
**組の順位は残る（上位 30 の一致 23 / 30・r 0.915）が、駒の3分は 40 / 51 でしか残らない**
——**Q3（系列に割る）の 43 / 51 より悪い。物差しの安定性を問う検定は、組ではなく駒で書くこと。**
**転生 → 残す へ上がった 4 体のうち 3 体が毒の駒**（決着が延びるほど積み上がるため）。
**「規則 P は攻2〜3 を引かない」（第80期）は 5 枚選択のドラフト規則の話で、埋め草3枚の版では
攻 3 以下を 8.6% 引く**（規則 R は 17.6%）——偏りが大きいのは**延べ在席の集中のほう**（上位 10 体で 38.4%）。

**唯一の読み手であるキーを持つ駒は 3 体**（第83期・第48期の シガ＝痺・ウロ＝破片 に **ウツ＝強化** が加わる）。
**拒否権3 は `TraitEntryMap.Supplies`（書き手）しか見ないので、この3体は当たらない**
——**シガだけが差し替え候補に入っている**（床 −5.18・止めているのは拒否権2 だけ）。

**ロスターを3分したら、切れる駒は 0 体だった —— 差し替え候補は 6 体出て、6 体とも拒否権に当たった**
（第82期・調査。**新機構ゼロ・駒ゼロ・差し替えの実行ゼロ・最後の1枠は使っていない・`TraitId` ゼロ・
engine の変更ゼロ・`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件・`compare` 305 セル 0 件**）。
器具は第81期の 2×2 ひとつだけで、**在席差（第80期）は1度も使っていない**。
**単独と相乗は同じ4つの数から出る**ので器具を2つ持つ必要が無い:

    横軸  単独(A)   = y11 − y01              ← 第69期の標準器具（相方は本物のまま A だけを素体に）
    縦軸  相乗(A,B) = y11 − y10 − y01 + y00  ← 第81期の 2×2
    残す 単独 ≥ +1.5 ／ 転生 単独 < +1.5 かつ 最良の相乗 ≥ +5.0 ／ 差し替え その他

線は 単独 **+1.5**（第62期以来の帰属の閾値）・相乗 **+5.0**（第46期の配置の採否閾値）で、**測る前に固定した**。
経緯は design/PHASE82_CHECKUP.md。**残す 31 / 転生 13 / 差し替え 6 / 別扱い 1。**

    Q1 第81期の再現（別実行の `pairs2` と）  上位 30/30・下位 30/30・**台ごとの最大差 0.0000000000pt**（163,200 セル）  ○
    Q2 差し替え候補が 1〜15 体               6 体                                                          ○
    Q3 **独立2系列で群が一致する駒が 45/51**  **48 / 51**（別扱いを除くと 47/50）                          ○（**主判定**）
    Q4 拒否権が働いているか                  51 体で 15 / 38 / 1、差し替え候補で 5 / 3 / 0                 ○
    Q5 台が割れた駒                          15 / 51（うち 14 は「ドラフト台で残さない × 理想台で残す」）   —

**3分が揺れるのは線の上に乗っている駒だけである**（第82期）。Q3 で群が割れた 3 体は
**セロ +1.55 / ラウ +1.50 / ホタ +1.40 で全部が単独の線 +1.5 の ±0.2pt の内側**、
**相乗の線 +5.0 で割れた駒は 0 体**。**単独の帰属の系列間相関は r = 1.000**
（1体あたり 50 組 × 128 台 = 6,400 台・**SE の中央値 0.157pt** ＝ 相乗の 0.58pt の 4 分の1）
——**駒の値段は、組の値段よりずっと精度良く測れる。**

**理想台は 51 体中 44 体を「残す」と判定する。だから拒否権1（台が割れる）はほぼ何にでも立つ**（第82期）。
`CompareBuilds()` の 61 行は**その駒が噛み合う相方と一緒に並べてある**ので、
理想台の帰属は**在庫率を 1 に固定して単価だけを測っている**（第77期の式の片側）。
実測でドラフト台の単独と理想台の帰属の相関は **r = 0.505**、割れた 15 体のうち **14 体が同じ向き**
（ドラフト台で残さない × 理想台で残す）で、逆向きは 散開のササ 1 体だけ。
**差し替えを実行する期は、先にどちらの台を正とするかを決める必要がある**——
**この 86% を放置したまま拒否権1 を使い続けると、差し替えは永久に実行できない。**

**差し替え候補 6 体のうち 3 体は傷の読み手**（抉りのエグ −2.39 / 断ちのナタ +0.29 / 縫いのハリ +0.09）。
**4 体は `Supplies` を1つも持たない純粋な読み手**（＋継ぎ当てのノノ）——
第81期 表D の「噛むのは供給を持つ側」が、駒の側では**「供給を持たない駒が差し替え候補に集まる」**と出る。
**第73〜75期で3期かけて代金を直しても、傷という通貨が生む値段そのものが小さい。**

**「出力が遅い通貨」は転生ではなく差し替えに落ちる**（第82期。指示書の分類3つのうち転生候補に 0 体）。
**遅い通貨は単独も相乗も同じだけ小さくする**ので縦軸が +5.0 に届かない。実例が
**据えのバン**（手番の唯一の読み手・単独 +0.41・最良 +3.08）で、その手番を供給しているのは**ドルガ1枚**。

**のろまの巨兵ドルガは「切れ」と「切るな」が同時に立つ唯一の駒**（第82期）。
**単独 −13.50（素体のほうが強い）・理想台 −31.41（51 体で最下位）**なのに、
**天井 73.6% で別扱い**（`y00` > 95% の台が半数超）・**手番の唯一の書き手**（拒否権3 は 51 体でこの1体だけ）・
**主判定に在席**（拒否権2）・**有意な正の相手が 26 体でロスター最多**（最良は 鬨の号令ガン +14.48）。
のろまは「捨てた手番が号令・据えに売れる」代金なので、**買い手と組んだときだけ資産になる。**

**拒否権3（唯一の書き手）は `TraitEntryMap.Supplies` で数えるので、読み手の唯一性は入らない**（第82期）。
第48期の「切れない駒」はシガ（痺の唯一の読み手）・ウロ（破片の唯一の読み手）も数えていたが、
**書き手で数え直すと 1 体だけ**（ドルガ・手番）。**被弾は書き手 0（敵が供給）・残り9キーは 2 以上**で、
**「切ると軸が丸ごと消える」危険は手番 1 本にしか残っていない。**

**「条件が盤面に潰されている」を開戦時の盤面だけで判定しない**（第82期・予測 P3 の外れ）。
散開（ササ）は「隣に味方がいない駒の被ダメを 35% 下げる」で、規則配置 H は 5 枠を全部埋めるので
開戦時は 1 体も条件を満たさないのに、**単独は +2.61 で「残す」に入った**
——**味方が死ぬと隣接が空く**（ドラフト台は床が厚い）。**理想台の帰属が +0.12 とほぼ 0 なのは、
理想台では味方が死なないから。床が厚い台では、隣接や列の条件は戦闘の途中で満たされる。**

**床の screen はまだ無い**（第82期）。指示書は天井（`y00` > 95%）しか screen していないが、
**実測では床（`y00` = 0%）の中央値 39.1% 対 天井 8.1% で 4.8 倍厚い。**
同じ 50% の線を床に当てると**囃し立てのヒサ（51.0%）が 1 体増えるだけ**なので3分は動かないが、
**天井の厚い駒（体）と床の厚い駒（体を持たない駒）は完全に別の集団**である。
**足すなら測る前に線を決めること。**

**`checkup run` の TSV は `pairs2 tables` にそのまま渡せる**（先頭 9 + NT 列が同一・生の y を後ろに足しただけ）。
**ただし再現の確認に同じファイルを両方へ渡さないこと**——**自分自身との一致**になり、
第81期 §0-1 が潰した形（自己検査 (g)）をこちらで踏む。第82期は `pairs2 run` の全区間を別に回して突き合わせた。


**組の表は 2×2 に組み直したら使えるようになった —— 「順位が残らない」は粒度ではなく標本の作り方の問題だった**
（第81期・調査。**新機構ゼロ・駒ゼロ・最後の1枠は使っていない・`TraitId` ゼロ・engine の変更ゼロ・
`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件・`compare` 305 セル 0 件**）。
第80期の**在席差**をやめ、**同じ台（同じ埋め草3枚・同じ席・同じ戦闘 seed）の上で A と B の中身だけを4通りに振る**:

    y11 = 両方が本物 / y10 = A 本物・B 素体 / y01 = A 素体・B 本物 / y00 = 両方が素体
    相乗(A,B) = y11 − y10 − y01 + y00        ← 2×2 の交互作用

**器具は素体差し替えに統一した**（第69期の標準器具）。**在席差は表C に並べるだけで相乗の計算に使わない。**
台 = 埋め草3枚（残り 49 体・規則 P）＋ A ＋ B・席は規則配置 H・弱い波 60%・第2〜5波・seed 0..7。
**全 1,275 組 × 128 台（K 64 × 2 系列）= 21,241,600 戦。** 経緯は design/PHASE81_PAIRS2.md。

    量                              第81期（2×2）      第80期（在席差）
    SE の中央値                     **0.58pt**         2.36pt（標本の中央値 167）
    測れた組                        **1,275（全部）**   430 / 1,275
    測れる組が 0 の駒                **0 体**            13 体
    系列どうしの相関 r               **0.933**          0.601（半割）
    上位30 / 下位30 の一致           **25 / 22**        19 / 13（半割）

    Q1 SE の中央値が 1.5pt 未満          0.58                                  ○
    Q2 **独立系列で上位・下位 30 が 20/30 以上**  上位 25 / 下位 22             ○（**主判定**）
    Q3 第80期に有意な 129 組で符号一致 2/3   76 / 129（58.9%）                 **×**
    Q4 13 体すべてに測れる組              13 / 13（正の相乗を持つ駒も 13 / 13）  ○
    Q5 y00 の張り付きが 50% 以下          45.7%                                ○

**「2つの seed」は「2つの独立系列」ではない**（第81期・自己検査 (g) の実装）。
第80期の A/B 帯は**同じ 11,000 編成**を共有していたので、帯間の一致（29/30・27/30）は自分自身との一致だった。
この期は**組ごとに引き番号 0,1,2,… で埋め草を引き、3枚組が既出なら捨てて 128 通りの相異なる台を集め、
引いた順に交互へ配る**（偶数番→系列1・奇数番→系列2）。**編成の共有 0 が構成から保証される。**
**seed を定数差で2本に分ける書き方は採らない**——重複しないことが偶然任せになるうえ、
**近い seed の `Random` を2本並走させる**ことになる（第71期の相関）。台の seed 自体も SplitMix64 で混ぜる。
実測の位置関係が (g) が本物の検定であることを示す——**A/B 帯 29/30 > 独立2系列 25/30 > 半割 19/30。**

**第80期の表の空白には、表の中身より大きい組がまるごと入っていた**（第81期・表F）。
第80期が落とした 845 組のうち**この期に有意なのは 285 組（33.7%）**で、
**測れていた 430 組の 37.4% とほとんど変わらない**——空白は「そこには何も無い」ではなく「測っていない」だった。
**ロスター最大の相乗3つは第80期に1つも測れていない**——グザ×ヴィオ **+47.73**（在席 13）／
ウツ×クビ **+46.95**（92）／**ミオ×グザ +38.94（在席 0＝11,000 標本で一度も同席していない）**。
**正の相乗を1つも持たない駒は 0 体になった**——**第80期の「後備えのセッキ 1 体だけ」は撤回**（セッキは正 13 組）。

**体（ボルグ・ゴルム・ドルガ・カド）を含む組は、正の側から負の側へ移った**（第81期）。
第80期は有意な正 80 組のうち **71（89%）**が体を含み「床の非線形」と診断されていたが、
2×2 では体が4版すべてで同じ席に同じ数値で立つのでその項が定義上消える——**上位30 で 6（20%）・下位30 で 21（70%）。**
残るのは**天井の側**で、体が2枚入った台は y00 が 95.6〜99.7% と既に高く、特性を足す余地が無いので相乗が負に出る。
**Q5 の「張り付き」を作っているのは弱い埋め草ではなく、素体でも勝ってしまう体のほう**
（指示書の分岐は床を想定していたが、厚い 8 組はすべてドルガを含み y00 が天井）。

**（供給→読み）は第80期の 2 倍以上の差で正**（第81期・表D。分母 23 → 55 組）。
**+7.76 対（読み→読み）−1.59**（第80期は +3.36 対 −1.11）で、**55 組中 40 組（73%）が有意に正**。
キー別は **毒 +24.95（7組すべて有意に正）> 標 +11.79 > 弱体 +9.56 > 手番 +8.00 > 燃 +6.26 > 移動 +4.53**、
**強化だけが負（−5.29・6組中5組が有意に負）**——第63期「通貨を『移す』機構は読み手にとって『奪う』機構」の延長。
**毒の行は第80期には1つも存在しなかった**（毒の6枚がまるごと表の外だった）。

**第72期の傾きとの照合は (2枚−1枚)−(1枚−0枚) の列で見ること**（第81期・表E）。
指示書が主に据えた「傾き（1枚目と2枚目の**平均**）」との符号一致は **4 / 11** だが、
**(2枚−1枚)−(1枚−0枚) では 8 / 11 で線を越える**——第80期の但し書き（相乗は「2枚目が余分に払うか」なので
厳密にはこちらと比べるべき量）が、**器具を直すと差が開く形で効いた**。
**どちらの器具が壊れているかはこの期でも決めていない。**

**Q3（新旧の橋渡し）は落ちたが、食い違いは「両方が 0 の近く」に集中している**（第81期）。
符号一致 76 / 129 に対し、**食い違った 53 組の |2×2| の平均は 2.02**（一致した 76 組は 5.61）で、
**53 組中 30 組は |相乗| < 2pt**。一致率は在席差が大きいほど上がる（|在席差| 0〜8 で 56.2% → 8〜12 で 63.6% → 12 以上で 72.7%）。
**「同じ組に正反対の値が付いた」形の食い違いではない。どちらが正しいかは決めていない**（第62期の線）。

**長時間の診断は `run <skip> <take>` / `tables <path>` に割ること**（第81期）。
全 1,275 組を1回で回す形（約 35 分）は **28 分ぶん走ったところで OS にメモリ不足で落とされた**。
**シャードをどこで切っても結果は同じ**（台の抽選は (組, 引き番号) だけで決まり、組の間に依存が無い）。
**素体は51体ぶんを1度だけ作って使い回すこと**——呼び出しごとに `UnitDef` を作ると 65 万回ぶんの割り当てになる
（`PgWeakOf` と同じ作法）。**次期は第82期の健康診断。器具はこの期の 2×2。**

**組の表は「和より大きいか」は読めるが「どれが一番か」は読めない —— 相乗の符号は残り、順位は抽選の揺れで動く**
（第80期・調査。**新機構ゼロ・駒ゼロ・最後の1枠は使っていない・`TraitId` ゼロ・engine の変更ゼロ・
`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件・`compare` 305 セル 0 件**）。
第76〜79期のドラフト台（11,000 標本 × 弱い波 × seed 8本・規則 P・規則配置 H）を同じ抽選で再生成し、
**在席差の器具**（`単独(A) = A 在席の平均 − 不在の平均`／`組(A,B) = 両方在席 − どちらも不在`／`相乗 = 組 − 単独 − 単独`）で
1,275 組を集計した。帳簿は `PairAcc`（`Program.cs` 末尾。駒 × 駒 の n / Σy / Σy² だけで 単独/組/相乗/SE が1パスで出る）。
**帰属（素体差し替え）とは別の器具で、混ぜていない。** 経緯は design/PHASE80_PAIRS.md。

    Q1 |相乗| > 2SE の組が 50 以上        A 帯 129（雑音の期待値 20）・両帯で有意・同符号 122     ○
    Q2 上位・下位 30 が両帯で 2/3 一致      29 / 27                                                 ○（**ただし抽選の揺れを検定していない**）
    Q2' 同じ集合が半割（標本の偶奇）で一致  **19 / 13**（線 20）                                    **×**
    Q3 （供給→読み）＞（読み→読み）         +3.36 対 −1.11（両帯）                                  ○
    Q4 同キーの組と第72期の傾きの符号一致   7 / 11（線 8）                                          **×**（どちらの器具が壊れているかは決めていない）

**表を「使える」とは書いていない。次期の入口は標本の増強**（順位付きの候補は報告書 §10-3）。

**1組あたりの在席標本は 86 が上限**（11,000 × 10 ÷ 1,275 の恒等式。指示書の「1.96%・215 標本」は誤りで 0.784%）。
**線 100 で測れる組は 430 / 1,275（33.7%）**、**13 体は測れる組を1つも持たない**（ミオ 3 標本・ヒサ 13・トウ・ゾト・クグ・ガン・ラウ・
リィカ・ヴェル・グザ・ヒビ・ムグ・シオ）——**毒の6枚・標の書き手・死軸・強化の供給者がまるごと表の外**。
規則 P（攻撃力 → HP の2段）の偏りで、**表の空白はロスターの性質ではなく抽選の性質。**

**同じ編成を別の戦闘 seed で回した2帯は、編成の抽選の揺れを検定しない**（第80期）。A/B 帯は同じ 11,000 編成を共有するので
帯間の差は 0.42pt と SE（中央値 2.36pt）より一桁小さく、Q2 は必ず通る。**標本の側の揺れは半割（標本の偶奇）で見る**
（第13期 `bench` の作法の組版）——半割の相関 r = 0.601・上位 30 の一致 19 / 30・下位 13 / 30。
**ただし A 帯の上位 30 は両半分とも 30 / 30 で正、下位 30 は 30 / 30 で負**——符号は残る。

**強い体 × 弱い駒の正の相乗の半分近くは床の非線形**（第80期）。有意な正 80 組のうち **71 が4体の体**（ボルグ・ゴルム・ドルガ・カド）を含み、
**0% の標本を除いた版で残るのは 51**。弱い駒の 単独 は「引いた編成が床に落ちる」ことで大きく負に出るが、体が同席すれば台は床にいないので
機会費用が小さくなる——機構の噛み合いではなく勝率の目盛りの非線形（カド × キリ +13.9 → 0% を除くと +7.1・カド × クビ +7.2 → +0.4）。
**逆に体どうしは天井で負**（ドルガ × カド −14.0 → −2.8）。**組の表は必ず 0% を除いた版を併記する。**
0% を除いても残る組が機構の噛み合い——**ヨミ × バサ +30.0 → +25.3（移動・ロスター最大）**・ドルガ × ウツ +8.2・ゴルム × ノミ +7.9・
ボルグ × ハギ +7.6・ゴルム × カリ +7.5・ボルグ × ホタ +6.9・ソラ × トメ +4.8・ザン × ソラ +4.9。

**噛むのは供給を持つ側**（第80期・表D）。（供給→読み）23 組の平均 +3.36・正(有意) 10 / 負 1、（読み→読み）27 組は −1.11。
**傷だけが供給→読みで負**（刻み × 断ち −1.84。読み手が書き手の前提を食う・第73〜75期）、**弱体は 10 組で +0.55 とほぼ 0**
（「移す」機構は読み手から奪うので符号が打ち消す・第63期）。**第78期「入口は供給が切れうる箇所」の組版で、供給者が同席すれば正になる。**

**第72期の傾きの定数は1枚目と2枚目を平均しているので、符号が割れるキーでは組と合わない**（第80期・Q4 の落ち方）。
標は傾き −3.30 だが組の相乗は +11.3（ソラ × トメ / ザン × ソラ）——**1枚目（書き手の体）は負・2枚目が読み手なら正**（第52期の組版）。
被弾は傾き +19.36 だが同キー 32 組の相乗は −0.47 ≈ 0——**傾きの正体は1枚目で、2枚目の上積みは無い**（第69期 表E の「ほぼ線形」と整合）。
**どちらの器具が正しいかは決めていない**（指示書 §5）。

**オノは「誰とも噛まない」のではなく「体としか噛まない」**（第80期・P6）。52 体版で線 100 の相手 11 体のうち 正(有意) 3 は
ゴルム +10.2 / ドルガ +9.8 / カド +5.7 で全部が体（床の非線形）、機構の噛み合いは 0。**第79期の「軸あり ±0.00」と整合し、
`Reads` も `Supplies` も空の輪郭が表の上でそのまま見える。**

**測れているのに正の相乗を1つも持たない駒は後備えのセッキ 1 体だけ**（相手 12・正 0・負 0。供給を持たず読みは被弾のみ）。
`Supplies` を持つ駒は 51 体すべてに読み手がいる——**「その供給を読む駒が盤面にいない」駒は 0 体**。第79期 §6 の
「規則に `Supplies ≥ 1` を足す」は、供給の種類ではなく**読み手が引かれる台**を先に要求することになる。

**構想メモは `design/IDEAS_PENDING.md`**（第80期に新設。**仕様書ではなく、指示書から参照しない**）。
カドの棘 → 傷／ムドの呪い の2つを記録した。**`ENEMY_FORMATION_PLAN.md` はリポジトリに存在しない**（`TURN_SLOT_PLAN.md` と同じ）。

**輪郭どおりに作った駒は、輪郭どおりには強くならなかった —— 最後の1枠は空のまま持ち越し**
（第79期・**測って採用しなかった**。**新しい `TraitId` ゼロ・engine の変更ゼロ・`All` は 51 のまま・
`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件・`compare` 305 セル 0 件**）。
第76〜78期の実測から出た輪郭（**入口 0 ／ 特性2つ ／ 2枚目は代金 ／ 相方を要求しない ／ 数値は中央値**）に
合う「まだ無い駒」を**空白の地図から規則で選び**、既存の特性2枚だけで作った——
**首刈りのオノ**（`UnitCatalog.Ono`・処刑 `Executioner` ＋ 支援拒否 `Stoic`・薙ぎ・HP60/攻6/速7）。
定義は対照として残置してある（オゴ・ゴウ・ヌキと同じ扱い）。

    量                                   A 帯 / B 帯          線      判定
    Q1 ドラフト台の在席時帰属             +1.26 / +1.18        ≥ +1.5  ×
    Q2 理想台の帰属（単騎 / 軸あり）      +1.00 / +1.50 ・ ±0.00 / ±0.00   ≥ +1.5  ×
    Q3 相方が本体か                       軸 − 単騎 = −1.00 / −1.50 ≤ 単騎   ○（相方は本体ではない）
    Q8 台の一致                           + / + / 0（符号は割れない）        ○

**両帯で 0.1pt 以内に再現し、符号は割れず、それでも線に届かない。**
**「入口 0 のほうが強い」（第78期）は「入口 0 にすれば強い」ではない**——第78期の入口 0 群の帰属 +3.43 は
群の平均で、その中身はドルガ・カドのような**体と機構が両方大きい駒**が押し上げている。
**輪郭は必要条件の側にあって、値段を決めているのは機構の中身のほう**である。

**撃破で育つ機構は、撃破が決着の直前に集中するので育ったあとに振る手番が無い**（第79期）。
罠の行で支援拒否を外すと撃破は 0.36 → **1.31 回/戦**（処刑の +7 が 9 点ぶん載る計算）なのに、
処刑だけの寄与は **+0.62 / +0.00pt**。単騎・軸あり の行では処刑の寄与が両帯とも **±0.00**
（対照 ＝ 素体が全セル一致）。**第23期の吐き戻し（「攻撃力という遅い通貨に変換したので使う前に戦闘が終わる」）
と同じ形が、撃破という最も遅い供給で出た。**

**「代金」は弾いた量では実在したが、勝率の上では資産だった**（第79期・Q4）。罠（ゴルム・カリ・ガン・クグ・オノ中央）で
支援拒否が弾いた強化は **54.8 / 54.4 量/戦**（ロスターで最大級）なのに、外した対照より現行のほうが
**+5.5 / +4.9pt 強い**——縛め（不動）と駆り立て（標）は強化と一緒に**呪いを書く**ので、
標の読み手がいない行では受けないほうが得（第52期「標の符号は読み手がいるかで決まる」の代金側）。
**支援拒否の値段は「支援の量」ではなく「支援に同梱された呪いの量」で決まる。**

**数値を中央値にした駒は、規則配置 H が 63% を中央に置く**（第79期）。HP60・攻6 は上位2枚に入らないので
残り物の席＝中央になり、**撃破/戦は 中央 0.28 対 後列 0.51〜0.64・帰属は 中央 +0.95 対 後3 +1.78** と
機構がいちばん動かない席で測られる。**ドラフト台の帰属は「規則配置 H がその駒を置く席の値」**であって、
機構の上限ではない（ただし理想台の出発点 H・採用席のどちらでも +1.75 を超えないので、採否は動かない）。

**「相方を持たない4枚」は、字面どおりに集めると台が死ぬ**（第79期。前提の訂正）。キー 0 で死・撃破のフックを
持たない4枚（ドルガ・ナラ・ノノ・ササ）＋オノは **120 通りすべてが 100/0/0/0/0**（CLAUDE.md の「台が死んでいる」）。
第61・63期の器具の規則（**素体版の5波平均が 40〜95% の組だけを測れる行とする**）を試験行の作り方に組み込み、
キー 0 の7体から C(7,4)=35 組を全部測って**測れる5組のうち第2〜5波平均が 50% に最も近い組**を採った
（ドルガ・リィカ・ノノ・ヴェル）。**試験行を作るときは、床の検査を作る側の規則に入れること**
——`reseat` の後で気づくと「席の問題」と「台の問題」が混ざる。

**規則は1つの駒を指さなかった。3か所を固定して初めて指した**（第79期。指示書 §1-4 の「規則が1つの駒を指す」は
前提の訂正）。(1) 2枚目の決め方（代金の `TraitId` を「発火口の保持者が最少・同数なら列挙順」で選ぶ → 支援拒否）、
(2) 攻撃型の決め方（機構が燃料を自分の一振りでしか作れないときだけ薙ぎ）、(3) 保持者の数え方（`All` 51 体・
どちらの特性でも）。**しかも同点処理の第2段（攻撃型）が決定的だった**——3候補を単体に揃えていれば
`Def.Id` の辞書順で「報いのムク」（`OnAllyDamaged`・標を読まない仇討ち）が選ばれていた。
**空白の地図（分類 × 発火口 60 組のうち 50 組が空）はそのまま残る。** 経緯は design/PHASE79_LASTSLOT.md。

**「入口の数」は在庫率の代理変数ではない —— ドラフト台では入口が多いほど弱い**
（第78期・調査。**新機構ゼロ・駒ゼロ・最後の1枠は使っていない・`TraitId` ゼロ・
`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件**）。
第76期の第1識別量「**特性の総数 +1.24枚**」に第75/77期の式（帰属 = 頻度 × 単価）を当てるため、
特性の総数を **発火口 / 入口 / キー** の3つに分け直して回帰した（同じ 11,000 標本・同じ席・同じ乱数列）。
足したのは診断1本と**手で作った表2枚**（`TraitHookMap` / `TraitEntryMap`。どちらも `TraitKeyMap` と同じ場所）。

    量               Pw の R² 低下（A/B 帯）   順位   統制した比較での符号
    数値（7本）        0.3707 / 0.3699          1     —（統制した側）
    **特性の総数**     **0.0586 / 0.0600**      **2**  **正（+25.4 / +25.7pt）**
    発火口            0.0055 / 0.0055          3     —
    キー              0.0019 / 0.0017          4     —
    **入口（味方）**   **0.0010 / 0.0010**      **5**  **負（−21.0 / −21.1pt）**

**主判定 Q1 は「入口の説明力が特性の総数を上回れば在庫率」だったが、上回らないどころか最下位**
（Pw の 6 通り＝3ケース × 2帯すべてで同じ順位・**特性の総数の 59 分の1**）。
**統制した比較では符号まで逆**——**特性の総数を揃えて入口だけを振ると全7行で負**（−3.6〜−19.7pt・7,198 標本）、
**入口を揃えて特性の総数を振ると全6行で正**（+13.9〜+29.0pt・7,805 標本）。**13行で符号が1つも割れない。**

> **入口は「供給が届く口」ではなく「供給が切れうる箇所」だった。**
> 第77期の式の「在庫率」は**編成の側の量**（相方が在席しているか）で、**入口の本数は駒の側の量**である。
> **相方が保証されない台では、入口を1本増やすのは在庫を1本増やすことではなく、在庫切れの機会を1つ増やすこと。**
> **第77期の「口が2種類の通貨に開いていれば片方が欠けてももう片方で立つ」は
> <u>同じ条件に</u>2本の口が開いている場合の話で、<u>別々の条件に</u>1本ずつ開くのは逆に働く。**

**特性が多い駒ほど入口が少ない**（第78期。r = **−0.206**・編成を単位にしても −0.245）。
**2枚目の特性はたいてい「代金」か「自給の口」**であって外部供給の読み口ではない
——**第74期に切り出したマイナス4枚**（薄刃・深追い・刃待ち・塞ぎ）**がそのまま
「入口を増やさずに特性数を増やす」枚になっている。** 臆病（セロ）・執着（ノミ）も同型。
**入口 ＝ その駒の「読み」のうち、同じ駒の「書き」では満たせないものの数**で、
**場所（自分/味方/敵）まで一致しないと打ち消せない**——これが
第58期「火の粉はボルグ自身には移らない」と第77期「ヨミは自分では動かない」を同じ形で書いたもの。

**「自足」は分類として空ではない。ロスターの 61% が味方側の外部供給を1本も要らない**
（第78期・Q5）。**入口(味方) 0 が 31 体 / 51・被弾を含めても 24 体。**

    群          体数   在席時勝率（A/B 帯）   帰属の平均
    **入口 0**   31    **47.70% / 47.62%**   **+3.43 / +3.28**
    入口 ≥ 1     20    43.12% / 43.06%       +1.32 / +1.29

**入口 0 の駒のほうが強く、帰属も 2.6 倍**（両帯で再現・統制した比較と同じ向き）。
**第79期の仕様として「入口が多い駒」を作る案は、この実測でそのまま棄却された。**

**「単価は設計で動かしにくい」は1つの機構の中の話。駒をまたぐと単価は一定にならない**（第78期・Q4）。
入口1本あたりの帰属は **最小 −13.10（火選りのヒヨ）/ 中央 +1.63 / 最大 +21.51（鬨の号令ガン）pt/本**で、
**幅 34.61・符号がまたがる。** 第75期（0.175〜0.190 pt/%）と第77期（外部 3.24 / 自己 1.98）は
**どちらも1つの機構の中で測っていた。**
**変動係数を判定式に使うときは、平均が 0 から離れているかを先に見ること**（自己検査 (c)）
——実測 5.21 / 5.33 は判定線 0.5 を大きく超えるが、**平均が 1.26 pt/本 で正負にまたがるので大きさに意味が無い。**

**発火口はドラフト台で最弱・理想台で単変数最強**（第78期・Q6）。
単変数 R² は **発火口 Pw 0.044 対 理想61行 0.170（理想台では4つの最高）**、
群の説明力の順位も割れる（理想台は キー② / 特性の総数③ / 入口④ / 発火口⑤）。
**噛み合いが保証されている台でだけ「何種類の出来事に反応するか」が効く**
——**入口が負に効くのはドラフト台に固有**で、理想台では入口の説明力が **18 倍**ある。

**駒の帰属を当てる量は4つの中に無い**（第78期。第72期「キーは通貨の名前であって駒の値段ではない」の再確認）。
帰属との相関は **特性数 +0.252 / キー +0.134 / 発火口 −0.012 / 入口 −0.091**（両帯で一致）。
**特性1つあたりの帰属は 1枚 +2.17 → 2枚 +0.74 で劣加法**（4枚のカドだけ +8.19 だが **n=1 なので証拠にしない**。
在席時勝率 2/51 位・帰属 1/51 位で、**カドの入口は 0**＝棘守りが自分で移動を供給する）。

**同じ期の報告に出てくる2つの数を、判定式で突き合わせる前にどちらがどちらか確かめること**（第78期）。
第76期の **30.7pt は「組の中の勝率の標準偏差」・72pt は「上位25% − 下位25%」**で別の量なのに、
最初の実装は四分位の差を 30.7 と比べて誤って「×」を出した。
**器具は3つの数すべてを再現している**（30.72pt / +71.90 / **特性の総数 +1.24**・B 帯 30.72 / +72.24 / +1.30）。

**量の名前が向きを誤って説明していることがある**（第78期。第60期の「焚き付け」→「火選り」と同じ形）。
**外した予測3つ（P1 の符号・P2 の順位・P3 の非対称）は、すべて「入口」を
「供給が届く本数」だと思って書いたことから出ている。** 実体は
**「外から埋めてもらわないと動かない箇所の本数」**で、向きが逆だった。
**第76期の「特性の総数」はまだ分解できていない**（4つに分け直しても第1のまま・標準偏差で割った差 +0.87 / +0.91）
——残る候補は**代金の枚数**と**発火口の種類**の2つ（第78期は選んでいない）。
経緯は design/PHASE78_TRAITCOUNT.md。


**ドラフト台で勝つのは「自足している条件」ではなく「入口が広い条件」。値段ではなく在庫で勝っている**
（第77期・ヨミ V9 の再測定。**駒ゼロ・最後の1枠は使っていない・`TraitId` ゼロ・
`docs/` は8ファイルとも再生成して差分 0 バイト・`audit` ずれ 0 件**）。
`CreakRule` に供給元の選択子（`CreakSource` = `Whet` / `Bonus` / `Both`。**既定は現行のまま**）を足して、
第66期の V9（`AtkBonus`）と第67期（`WhetReceived`）を**同じ標本・同じ席・同じ乱数列**で比べた。
ドラフト台 Pw（在席 1,636 / 11,000 標本 × 弱い波 × seed 8本）の実測（A 帯 / B 帯）:

    自己供給の最良 B9（`AtkBonus` ≥ 9）   帰属 +2.11 / +2.11   薙ぎ化率 21.3 / 21.1%
    外部供給の最良 W2（`WhetReceived` ≥ 2） 帰属 +1.06 / +1.05   薙ぎ化率 11.1 / 11.0%
    **Q1 = 自己 − 外部 = +1.05 / +1.06**（両帯で符号一致・値は 0.01pt 差）

**ただし単価では外部が 1.5〜1.6 倍強い。** 強化の供給者が同席した標本（32.6%）の中では
**外部 +3.24 対 自己 +1.98**（B 帯 +3.22 対 +2.16）で、**残り 67.4% では外部の帰属が厳密に +0.00**。
期待値は **在庫率 × 単価** でぴたり出る（0.326 × 3.24 = 1.06 に対し実測 +1.06）
——**第75期の「回収額は 解除率 × 単価」と同じ形の式が、供給の側でも出た。**

**「自己供給」は自足ではない**（第77期の本体。第66期の言い回しの訂正）。
**ヨミは自分では動かない。** `AtkBonus` を上げるのは 軋み（**他人が動かす**）と `Whet`（**他人が配る**）の
2本で、どちらも他人が要る。第66期の「自分で作れる値」は**「自分の帳簿に載る値」**の意味だった。
実測で、**移動の書き手も強化の供給者もいない 742 標本の帰属は第2〜4波がちょうど +0.00**、
第五波だけ +7.58 / +6.89——**動かしているのは敵側の告発人（曝き）**である。

> **Q1 が測ったのは供給の自足ではなく「条件の入口の広さ」だった。**
> `AtkBonus` は 移動（味方3 ＋ **敵1**）＋ 強化（味方6）の口で、相方の在席率 **56.2%** / 到達率 21.7%。
> `WhetReceived` は 強化（味方6）だけの口で、在席率 33.3% / 到達率 12.3%。
> **口が2種類の通貨に開いていれば、片方が欠けてももう片方で立つ。**
> **条件は1本のキーに紐づけず、複数のキーが流れ込む器に紐づけるほうが在庫が切れにくい。**
> **これは<u>同じ条件に</u>2本の口が開いている場合の話**——**<u>別々の条件に</u>1本ずつ開くのは逆に働く**
> （第78期。入口の本数が多い駒はドラフト台で弱い。上の「入口の数」の節を見ること）。

**同じ口へ既に流れ込んでいる通貨を2回数えても何も足さない**（第77期・Q6）。
`Both`（`AtkBonus + WhetReceived`）は「自足＋外部」ではなく**「外部を二重に数えた自己」**
——`Whet` を通った量は `AtkBonus` にも入るため。差は閾値 9 で **+0.23 / +0.27**、18 で +0.11、30 で +0.04 と
**閾値が上がるほど 0 へ近づく**（重み2倍が効くのは到達の境目にいる標本だけだから）。

**味方を動かせる駒はロスターに3枚しかない**（第77期に `SwapSlots` の呼び出し全数から確定）
——**逃亡（セロ）／棘守り（カド）／喧噪（バサ）。敵側は第五波の告発人（曝き）1枚。**
突き返し（ハネ）と曝きは `HaulOutPair(Opponent)` なので**敵陣**を動かす（味方は動かない）。
**移動キーの傾きは V0 で既に +7.8**（箱＝移動キーの枚数・全 11,000 標本）と 11 キーでも大きいのに、
**書き手はこれだけ**である。

**拒否権が seed 帯に依存することがある**（第77期・Q4）。B9（＝第66期の V9）の情報セルは
**A 帯（0..199）で 移動改 4 → 3**（第五波 96.5 → **100.0**）、**第66期の B 帯（200..599）で 突き出し 4 → 3**
（99.2 → **100.0**）、**第73〜76期の B 帯（200..399）では3行とも維持**。
**落とすセルは3帯とも同じ形**——「V0 で 96〜99% だった第五波を 100.0% へ押す」で、
帯によって**ちょうど 100.0 に届くかどうか**だけが変わる。
**`0 < x < 100` の情報セルは天井の際で不安定**（第22・54期の「天井は同値塊を作る」の測定側の帰結）。

**ヨミの条件付き変質は第66・67・77期で3回落ちた。この駒は閉じる**（`CreakRule.Default` は無効のまま）。
**採否は Q4（拒否権）1つで落ちた**——Q2（薙ぎ化率 10〜90%）を通ったのは B9 だけで、
Q3（帰属 +2.11 / 理想台 +17.1 / +10.8 / +13.5・両帯で符号一致）も Q5（第五波 38.2 → 38.5%）も通っている。
**「台が割れた」とは書かない**——(1) Q1 の勝ちは +1.05pt で大きくない、
(2) **理想台も3行中2行で自己の側**（外部が勝つのは号令の鬨が第1ターンに 8.0 を一括で届ける隊列崩し1行だけ）、
(3) 割れているのは台の間ではなく **seed 帯の間**。経緯は design/PHASE77_CREAK3.md。


**ドラフト台の勝率は数値では決まらない —— 未説明の 93% は「特性の枚数」と「被弾・移動のキー」の側にある**
（第76期・調査。**新機構ゼロ・駒ゼロ・`docs/` は8ファイルとも差分 0 バイト**）。
標本ごとの勝率（第2〜5波）を 25 本の説明変数（数値7 / 形5 / 軸12 / 交互作用1）で回帰した実測（両 seed 帯）:

    台               総攻・総HP・総速(3)  **数値のみ(7)**   数値＋形(12)  全部(25)  残差の標準偏差
    ドラフト Pw       0.424 / 0.426      **0.434 / 0.435**   0.537      0.592    24.4pt（目的変数の 64%）
    ドラフト S'w      0.337 / 0.335      **0.378 / 0.374**   0.495      0.564    23.2pt
    理想61行          0.193 / 0.195      **0.337 / 0.334**   0.400      0.603    14.2pt

**判定線 0.5 に 6 セル中 6 セルで届かない。**しかも**全部を入れても残差が 64〜66%** 残る（3台 × 2帯）。
**総攻・総HP・総速 を ±5% で揃えた組の中でも、勝率は 30.7pt（台全体の 80.5%）散る**
——**勝率が 72pt 違う2群が、総攻で 0.09・総HP で 0.45・総速で 0.04 しか違わない。**

**その2群を分けている量は、大きい順に 特性の総数 +1.24枚 / キー:被弾 +0.80 / キー:移動 +0.57 / 相方あり率 +0.16。**
**「特性の枚数そのものは効かない」は誤り**（第71期「共有キー数は代理変数として壊れていた」は
**キーの数**の話で、**特性の枚数には引き継げない**）。被弾と移動が並ぶのは第69期 表E
（枚数を増やして良くなるのはこの2本だけ）と整合する。

**群を落としたときの R² の低下は、3台 × 2帯の 6 通りすべてで 軸が1位・交互作用が4位**
（軸 0.052〜0.181 は数値の 2.2〜3.5 倍。**理想61行でいちばん大きい**＝理想台がシナジーで組まれていることの証拠）。
**2位と3位（形・数値）は差 0.001 以下で順位が付かない。**

**単変数の R² だけで交互作用を語らないこと**（第76期・P4 の否定）。`総攻 × 総HP` は単変数で
**全 25 変数中の最高（0.399）**に見えるが、**総攻と総HP を先に入れると上積みは 0.0009**（S'w では 0.0001）。
積は両方と強く相関するので、単変数の順位はその相関を写しているだけ。

**「攻撃力が効く」はロスターの性質ではなくドラフト台の性質**（第76期）。総攻の単変数 R² は
**Pw 0.293 対 理想61行 0.000**（総HP は 0.171 対 0.137）——**理想台では総攻に情報が1ビットも無い。**
**n=61 に 25 本を当てた R² は自由度で調整してから台をまたいで比べること**
（生 0.603 で最高 → 調整後 0.320 で最低）。

**「攻撃力を買うと勝つ」と「攻10〜13 を5枚で −7.2」は両立する。駒は数値の束であって攻撃力ではない**
（第76期・Q4）。**傷の5枚は攻がロスター平均比 +3.8 なのに HP が −12.0**（攻7以上の23体でも HP は 66.3 なので、
**傷の5枚はその集団の中でも HP が 12.7 低い**）。百分位で見ると**攻は5枚とも 73〜92%・HP は5枚とも 8〜49%**。
**総攻・総HP・総速 を揃えて機会費用を打ち消すと、傾きは −10.4 → −7.4 としか戻らない**（B 帯 −7.3）
——**数値が説明するのは −10.4 のうち −3.0 だけ。**

**第73期の −7.2（体と枠の機会費用）は、53% が数値・47% が数値の外**（第76期・Q3。両帯で −3.4 / −3.3）。
**数値の側の正体は HP。**残る −3.4 の候補は3つ（この期は測っていない）——
**席**（規則配置 H は HP 上位2枚を前に置くので、HP が下位の駒を入れると席の割り当てが1枚ぶんずれる）／
**速さの並び**（総速を揃えても順序は揃わない）／**攻撃型**（ロスターに貫き0・全体0 なので
**単体の枚数 + 薙ぎの枚数 = 5 が恒等**＝完全共線で、総攻を動かさずに型だけが入れ替わる）。

**傾きの特性側（+3.2）は実質まるごとキリ1枚**（第76期 表E。単独 +3.24 対 5枚まとめて +3.18・両帯）
——**第75期の「残り +5.4pt の 59%（−3.2）がキリの薄刃」と独立な器具で一致した。**
**第74期の「ナタ −5.6 が最大」は採用前の値で、現行のナタの Δ は −0.37**（断ちの待ち方が払い終わっている）。
**寄与と傾きは別物**——キリの寄与は **−19.2pt**（特性を外したほうが強い）なのに傾きの Δ は +3.24。

**素体どうしは（攻・HP・速・攻撃型）が同じなら Id 以外に違いを持たない**（第76期・訂正1）。
「同じ数値の他の素体と比べる」器具は**恒等式**なので、差は必ず +0.00 になる（陰性対照にはなる）。
**素体を差し替える器具で「数値の話か」を判定したいなら、比較先ではなく統制の側でやること**
——総攻・総HP・総速 を揃えた組の中で同じ傾きを測り直せば、恒等式にならない。

**説明変数を固定する前に、その変数が定数でないか・他と恒等式で結ばれていないかを数えること**（第76期）。
**編成の枚数は3台すべてで恒等的に 5**（指示書の「4変数」は実体3変数）、
**単体の枚数と薙ぎの枚数は完全共線**（単変数 R² が小数第3位まで一致する）。
経緯は design/PHASE76_BODY.md。


**代金を条件付きにして回収できる額は「解除率 × 単価」で決まる —— 薄刃の払い方は3版とも線に届かなかった**
（第75期・**測って採用しなかった**。**駒ゼロ・設計変更ゼロ・`docs/` は1バイトも動かない**）。
第74期の残り +5.4pt の 59% を占めるキリの薄刃（`ModifyAttack => 1`＝**打点を毎振り・無条件で払う**）に、
ナタと同じ「代金を消さず払い方を変える」を当てた。版は `ThinBladeRule` を `Run` に引数で渡す
（既定は V0 ＝現行。**`compare` 305 セル 0 件・キリを含まない行は全版 0 件**）:

    版                          解除率   ドラフト台 Pw の傾きの改善（A / B 帯）  上限に対する割合
    V1 傷の無い相手には 1        37.0%   **+0.99 / +0.96**                      26%
    V3 自分より遅い相手には 1    12.7%   +0.41 / +0.43                          11%
    V2 刻める攻撃だけ 1           1.3%   +0.00 / +0.00                          0%
    薄刃なし（＝上限）           98.8%   **+3.81 / +3.67**                      100%

**線は +2.0pt。3版とも届かない。** `与ダメ/振` は**解除率の完全な線形関数**で
（V1 は 0.63×1 + 0.37×11.49 = 4.88 に対し実測 4.893）、**解除率あたりの帰属も V1 0.175 / V3 0.190 pt/% と同じ**
——**条件が何に接続しているかは値段を決めない。決めるのは条件が偽になる頻度だけ。**
**第74期のナタが上限の 62% を回収できたのは「払い方を変えたから」ではなく、
`捨てたT` が 3.40 → 0.00 ＝ 解除率が実質 100% になったから**である（今期は 37% で回収 26%）。

**上限は「その期の基準」で測り直すこと**（第75期の前提の訂正）。第74期の「薄刃 −3.2」は
**採用前の断ちの待ち方（`SeverWait.Yield`）で測った値**で、採用後の現行を基準にすると **+3.81 / +3.67**。
V0 の傾きも −15.6 ではなく **−10.4**（第74期の採用後の値と一致）。

**帰属（+6.48）と傾き（+0.99）を混同しない**（第75期）。在席時勝率は 30.7 → 37.2% と大きく動くのに、
傾きは 6 分の 1 しか動かない——**傾きは箱の平均差で、キリは 11,000 標本の 11.3% にしかいない。
軸は 5 枚で出来ているので、1 枚を直しても傾きはその持ち分しか動かない。**

**オーバーキルは盤面に届かない**（第75期。V2 が実例）。`ApplyDamage` は残HPで切り詰めない
（`Amount` は素の量・`HpAfter` が 0 止まり）ので、**1 で倒せる相手を 12 で倒す版は
`compare` が 1 セルも動かない**のに `与ダメ/戦` は 2.43 → 2.70 と増える。
**「与ダメが動いた」を「盤面が動いた」と読まないこと。**

**`Trait.ModifyAttack` を条件付きにしてはいけない。対象を見る条件は `PerformAttack` に置く**
（第75期。止め＝第53期と同じ場所・同じ理由）。`ModifyAttack` は `self` しか受け取らないうえ、
**`CurrentAttack` は駆り立ての選択・転嫁の流し先・`StatSnapshot`・棘/仇討ち/責め苦の反撃量からも読まれる**
——**動かしたいのは「この一振りの打点」であって駒の攻撃力ではない。**
薄刃を除いた素の打点は `ThinBladeTrait.RawAttack`（驕りの `AttackWithout`・第46期と同型）。**床は 1**（0 を返さない）。

**通貨は「読み手が消費するもの」であると同時に「書き手の前提条件」にもなりうる**（第75期・Q4）。
書き手（キリ）の代金を傷に紐づけると、**軸の内側で5枚が互いの値段を書き換える**
——キリと同席した標本での寄与の変化は **縫いのハリ −4.27 / −4.23**（塞ぎがキリの解除を直接打ち消す）に対し
**刻みのノミ +3.23 / +4.82**（余分に書くと解除率が上がる）で、**符号は4枚とも両 seed 帯で一致した。**
**採否とは独立に残る設計情報**——**傷を他軸へ転用するなら消費型の読み手と同席させない設計になる。**
経緯は design/PHASE75_THINBLADE.md。


**傷の代金を分離して、最大の1枚の形を変えた —— 犯人は「断てないこと」ではなく「振らないこと」だった**
（第74期。**駒ゼロ・最後の1枠は使っていない。設計変更は断ちの待ち方の1点だけ**）。
第73期に「5枚のマイナスのうち独立に外せたのは1枚（ノミの執着）だけ」で詰まったので、
残り4枚を**挙動を1ビットも変えずに**別の `TraitId` へ切り出した
（薄刃 `ThinBlade` / 深追い `Overreach` / 刃待ち `Await` / 塞ぎ `Seal`）。
**`compare` 305 セル 0 件・`wound` の 6 モードと `carry` が第73期と byte 一致。**

    −15.6  ドラフト台 Pw の傷の枚数効果
      −7.2  体と枠の機会費用（46%・傷とは無関係。第73期のまま）
     −10.5  **5枚のマイナス**（68%）  ← 第73期は「特性の側 −8.3」としか言えなかった
        −5.6 ナタの刃待ち / −3.2 キリの薄刃 / −0.8 エグの深追い / −0.1 ハリの塞ぎ /
        +0.1 ノミの執着 / −1.0 交互作用
      **+2.2  傷の機構そのもの（プラス側・正）**  ← この期に初めて分離できた

**傷の機構は正の値段を持っている。それを 5 倍近い代金が食い潰していた。**
**第73期の「特性の側 −8.3」は、代金 −10.5 と機構 +2.2 の差し引きだった。**
**5枚の順位（ナタ > キリ > エグ > ハリ > ノミ）は第73期と完全に一致する。**

**採用した設計変更は1つだけ**——`SeverRule.Default` を **`SeverWait.Swing`（V1）** にした。
**閾値未満でも普通に振り、断ちは閾値に届いた傷にしか下ろさない**（`SeverTrait.Threshold` は 2 のまま）。
**マイナスは消していない。捨てるものが「手番」から「断ちの機会」に変わっただけ**である。

    版        ナタの振/戦   断ち発火/戦   読んだ傷/発火   捨てたT   勝率    傾きの改善
    V0（旧）     0.13         0.12         1.99          3.40    39.0%   —
    **V1（採用）** **3.42**   **0.09**     2.00〜2.03    **0.00**  **56.3%**  **+5.15 / +5.16**
    V2（閾値1）   0.38        0.36         **1.02**      3.15    40.7%   +1.46 / +1.47

**V1 は断ちの発火を1つも増やしていない**（0.12 → 0.09 と**むしろ減る**）。
**増えたのは「ナタが普通に殴った回数」だけ**（0.13 → 3.42 振/戦）で、`傷/断ち` は 2.00 のまま
——**第38期の「畳んで一撃」は1ビットも壊れていない。**
**傷軸の最大の代金は「傷が読めないこと」ではなく「読めない間ずっと突っ立っていること」だった。**
`docs/balance.md` で動いたのは **`刻み×断ち (ノミ×ナタ)` の 1 行だけ**（90.5 → 95.0 / 32.0 → 83.5 / 1.5 → 22.5）。

**在庫を読む機構に閾値を置くときは、閾値に届かない間の手番を別に設計すること**（第38期への追記）。
**閾値そのものは正しく働いていた**（`傷/断ち` = 2.00）。**閾値を下げる V2 は「畳んで一撃」を壊し
（`傷/断ち` 1.02）、しかも手番は捨てたまま**——**閾値が消せるのは `待ち` だけで `放棄` は消せない。**
**`放棄`（獲物がいない）と `待ち`（傷が浅い）は別の量で、ドラフト台では 11 倍違う**（3.12 対 0.29）
——**ドラフト台のナタは在庫を待っているのではなく、一生誰も傷を書かない。**

**`Traits.cs` でマイナスを別の `TraitId` に切り出すのは、挙動を変えずにできる。**
**`ModifyAttack` / `OnKill` は丸ごと移せる。`CanAct` は `SurrendersTurn` と<u>対で</u>移すこと**
——`Trait.SurrenderedTurn` は「**`CanAct` を偽にした Trait のうち** `SurrendersTurn` が偽のものがあるか」を
見るので、**分けると捨てた手番が号令・据えに売れてマイナスが資産に化ける**（第37期の警告そのもの）。
**同じ動作の中で「前段の結果」を使う代金はフックに切り出せない**（ハリの塞ぎ）——
再導出すると **(1) 繕った後の `MostHurtAlly` が別の答えを返す**・**(2) `PickOne` が `Roll` を余分に消費する**。
**札（marker）にして計量だけできるようにすること**（引き受け＝`BearTrait` と同型）。
**マイナス側の `TraitId` にはキーを持たせない**（`TraitKeyMap` に空で登録）——
`KeysOf` は駒の `Traits` の**和**なので、プラスを残す分割なら和が動かない。

**特性を明示列挙して作る診断のローカル変種は、分割のたびに壊れる**（第74期に実際に壊れた）。
`wound` の清潔な供給ノブ `裂き二重` は `{Rend, Rend}` と書いてあったので、
薄刃を切り出した瞬間に**打点が 1 → 12 に戻って対照が汚れた**（`与ダメ/振` 1.000 → 12.120）。
**分割したら `TraitId.` を列挙している診断を全部 grep すること。**

**`docs/units.md` は `TraitId` を1つ足すだけで動く**（`dump` の特性表が `Enum.GetValues<TraitId>()` を回す）。
**「挙動を変えない変更だから `docs/` は動かない」は成り立たない**（`balance.md` は 0 件でも `units.md` に 4 行増える）。

**`M-all` が `全部素体` より良ければ、その差は「未分離の代金」ではなく「機構の値段」である**（第74期・Q2 の訂正）。
`現行 = 体 + プラス + マイナス` / `全部素体 = 体` / `M-all = 体 + プラス` なので、
**M-all − 全部素体 = プラス**。**分解の閉じ具合は `Σ(個別) ÷ M-all` で測ること**（この期は 91% / 91.5%）。
**素体どうしの差でマイナスを代用すると 1 割ほど小さく出る**（キリ +17.78 → **+19.04**）
——**代用器具は「プラスを持ったまま代金を払っている」状況を作れない。**

**同じ代金が、軸が組めている台でだけ高く付く**——ハリの塞ぎは理想台 **+5.88** 対 ドラフト台 **+0.21**
（`刻み×縫い` は在庫 0.02/T ＝傷が盤面から消える台なので、塞ぎを外すと在庫が 0.28 に戻って軸が生き返る）。
**ノミの執着だけが代金として負**（−0.56）——**執着は代金ではなく利得**で、
1体に食いつくぶん傷が積み上がってなぞりが伸びる。**傷の5枚で「1つの動作の表と裏」が成立していない唯一の枚。**

**傷軸は凍結しない**（第75期の線 +2.0 に対し +5.15）。
**第75期の上限は +5.4pt で、その 59%（−3.2）がキリの薄刃 1 枚に乗っている。**
**その薄刃は第75期に測って回収できなかった**（V1 +0.99 / +0.96・線 +2.0）——
**上限は現行基準で測り直すと +3.81 / +3.67 で、−3.2 より大きい。傷軸の代金側はここで打ち止め。**
経緯は design/PHASE74_WOUND_COST.md と design/PHASE75_THINBLADE.md。


**傷の傾き −15.6 の半分は傷と関係が無く、残り半分の 9 割は機構ではなくマイナスだった**
（第73期・傷の解剖。**新機構ゼロ・駒ゼロ**）。ドラフト台（Pw・弱い波・11,000 標本 × 5波 × seed 8本）で
**箱は元の5枚で決めたまま、傷の駒だけを素体に落として**傾きを取り直した実測（両 seed 帯）:

    −15.6  現行（第71期 表E・第72期 表D と小数第1位まで一致）
      −7.2  ← **傷の駒を全部素体にしても残る**（＝5枚の「体」と枠の機会費用。**46%**）
      −8.3  ← 特性の側（54%）
        −4.8  断ちのナタ   ← **犯人。両台・両帯で最大**
        −2.5  裂きのキリ   ← **うち −2.8 は「与ダメ常に1」だけで説明される**
        −0.4  抉りのエグ
        +0.1  縫いのハリ   ← **書込の 89% を食うのに、傾きには効かない**
        +0.8  刻みのノミ   ← **唯一、機構が正**

**5枚の順位（ナタ > キリ > エグ > ハリ > ノミ）は理想61行でも同じ**（|Δ| < 0.3 の2枚を除く）。
**第72期 §8-5 の「理想側は箱の薄さのぶんだけ測っていない」は、量については正しいが順位には当たらない。**

**在庫不足は実在するが、律速ではない**（第73期・H1）。読み手の空振り率は
**理想台 65.2 / 65.3%・ドラフト台 72〜93%**（判定線 50%）で、在庫も 40 セル中 38 セルで 1.4 未満。
**それでも清潔な供給ノブ（キリの `Traits` を `{Rend, Rend}` にした二重版）は**

    書込 2.42 → 4.89（2.02倍） / 在庫 0.44 → 0.81（1.84倍） / 維持型の出力 7.5 → 14.3（1.91倍）
    **勝率 +0.62 / +0.38 pt ・ 傾き +1.27 / +1.18（−15.6 の 8%）**

しか動かさない。**空振り率を「直すべき不足」と読まないこと。**
**読み手がいない台では、供給を増やしても1ビットも動かない**（S'w はエグを 11,000 標本に
1度も引かないので、裂き二重の傾きの Δ が **+0.00**。第57期の AND ゲートの負の側）。

**在庫を食う量と、傾きへの効きは相関しない**（第73期・H2 の否定）。
**縫い（ハリ）は書込の 89% をその場で塞いで在庫を 0.02/T まで枯らす**のに傾き寄与は **−0.10 / −0.12**、
**断ち（ナタ）は 23% しか食わない**のに **+4.78 / +4.80**。
**「消費型が維持型の前提を壊す」は盤面では起きている**（ハリ行のノミは空振り 98.7%・出力 0.1 点/戦）
**のに勝率にも傾きにも出ない**——**壊された前提の中身がそもそも小さいから。**
**「消費型が資源を食う」を代金として数えないこと。**

**ナタが強くなるのは傷が読めるからではなく、毎ターン殴るようになるからである**（第73期）。
ナタの実額は**放棄 0.53 + 待ち 1.32 = 1.85 T/戦（決着 7.0T の 26%）**で、素体に落とすとこれが 0.00 になる。
**空振り率と手番の放棄率は同じ不足の表と裏**——**ナタの空振りは 0.7% だが、
それは在庫が足りている証拠ではなく「足りない間は振らない」という設計の帰結。**
**空振りは `Attacks == 0` ではなく「振ったのに発火しなかった回数」で数えること。**

**裂きのキリは「傷を書く駒」として損をしているのではない**（第73期）。
**素体(攻12) − 素体(攻1)**（どちらも特性なし）で打点の代金だけを分離すると:

    キリの寄与（素体差し替え）  −17.91 / −17.51
    うち「与ダメ常に1」の代金   −17.78 / −17.46   ← **99% / 100%**
    差し引き＝傷を書く値段      **−0.13 / −0.05**  ← **ほぼ 0**（傾きでは +0.26 / +0.33 とわずかに正）

**`TraitCatalog.Resolve` は重複した `TraitId` を潰さない**（`ids.Select(Get).ToList()`）ので、
**同じ特性を2つ並べればフックが2回走る**——**`Traits.cs` を触らずに「フックの回数だけを
1点動かす対照」が作れる。ただし清潔になるのは `ModifyAttack` が引数を読まない特性だけ**
（裂きは二重でも打点 1 のまま＝供給だけが 2 倍。刻みの二重は**なぞりも2回走る**ので汚れる）。

**マイナスをプラスと同じ `Trait` クラスに書くと、後から代金が測れない**（第73期）。
傷の5枚でマイナスだけを落とせるのは**執着（`TraitId.Fixate`）を持つノミ 1 枚だけ**で、
残り4枚（キリの `ModifyAttack` / エグの `OnKill` / ナタの `CanAct` / ハリの塞ぎ）は
**プラスと同じクラスの中**にあるので `Traits.cs` を触らずには分離できない。
**次にマイナスを書くときは、別の `TraitId` に切り出せるかを先に見ること**——**ノミがその前例。**

**`CompareBuilds()` で傷の駒を含む行は 8 行**（第73期。指示書の「15 行」は誤りだった）。
**うち消費型（ナタ・ハリ）を含むのは 2 行**で、**傷の消費を理想台で測れる行はこれだけ。**
**傷は溜まらない**——8 行中 7 行で在庫が T3 前後を頂点に下がるか横ばいで、
単調に積み上がるのは `止め (トメ×ソラ)` の 1 行だけ（標で狙いが後列へ逸れて敵が長く生きる行）。

経緯は design/PHASE73_WOUND_ANATOMY.md。


**キーの符号を読む価値は攻撃力を数える価値より大きい。それでも枠が5つしかないので両方は買えない**
（第72期・傾きで選ぶ規則 S'）。第71期の表E の傾きを**定数として焼いて**選択の基準にした
（S' = 提示3枚のうち「持つキーの傾きの合計」が最大 → 攻撃力 → `Def.Id`。**すでに選んだ駒を見ない**）。
4規則 × 2波 × 11,000 標本 × 5波 × seed 8本 = 3,520,000 戦/帯（表E が +3,520,000）・両 seed 帯:

    P  − N = +8.57 / +20.91   攻撃力を数える価値（総攻 37.9 → 48.9）
    S  − P = −1.47 / −5.65    キーの**数**を読む（第71期。完全に再現した）
    **S' − P = +2.91 / +5.78**    キーの**符号**を読む ← **正。第2〜5波の8セルすべて・両帯**
    S' − N = **+11.48 / +26.69**  傾きで選ぶ価値（総攻 37.9 → **38.3**・ほぼ据え置き）

**S' の総攻は N とほぼ同じ**（38.3 対 37.9・攻7以上は 2.15 で4規則の最小）**なのに、
攻撃力だけを買う P より強い。第71期の「シナジーを狙うことは攻撃力を数えることより価値が低い」は
規則を直すと逆転する**——ただし**2つは同じ5枠を取り合う**ので正味は +2.9 / +5.8
（理想編成との差 43.49pt の **6.7%**）。**「まず殴れる駒を確保する」が優先なのは、
価値が大きいからではなく、どちらか一方しか買えないから。**
（**厳密な分解ではない**——S' は総HP でも N を上回る 359 対 328 ので、S' − N に「体」が混じる。
**総攻を据え置いて総HP だけを動かす対照が無い**のは積み残し。）

**ドラフト台の Q1（第2〜5波の中間帯 60% 以上）は4期目で通った**——17.9 → 57.1 → 57.1 →
**71.2 / 71.9%**（S'w）。**第71期の「選択規則を強くする方向では線に届かない」は誤りで、
強くする方向が違っていた**（共有キー数 54.6% → 傾き 71.2%）。実体は**0% の標本が 23.4 → 12.7% に半減**した
ことで、**天井は 11.6 → 8.6% と むしろ減っている**——**S' は勝てる編成を増やしたのではなく死ぬ編成を減らした。**
**第67期のヨミ V9 の再測定条件（「Q5 が通れば初めて判定できる」）はこれで満たされた**（3期連続見送りの解除）。

**キーの符号は「台の硬さ」ではなく「どの波か」で決まる**（第72期・傾きの地図）。
11キー × 8条件（4波 × 2つの波の強さ・規則 P 固定）の 88 セルのうち、
**seed 帯で符号が割れたのは 1 セルだけ**（毒·既存4 = ±0.0）で**地図はまるごと再現する**。
**集計で既存波と弱い波の符号が一致するのは 10 / 11 キー**（唯一違う毒も |傾き| ≤ 2.1 の雑音帯）。
**割れる4本は波の軸で割れ、その割れ方が両帯・両強さで再現する**:

    移動  第2波 **−**（−4.2 / −9.3）  第3〜5波 **+**（+11.9 〜 +23.5）
    燃    弱い波で 第2波 +16.9 → 第3波 +9.6 → 第4波 +2.6 → 第5波 −3.0  ← **波の番号に対して単調**
    毒・強化  波ごとに符号が交代（|傾き| ≤ 6.4 で小さい）
    被弾 は4波とも正、標・弱体・手番・破片・痺・傷 は4波とも負（**6本が一様に負**）

**第70期の「弱い波では燃が3本目になる」は、正確には「早い波では燃が正になる」だった**
——弱い波で集計が正に出るのは弱い波の第2・3波が大きいからで、**どちらの強さでも第4・5波では 0 か負。**
**傾きの表を1枚に畳むと波ごとの符号の反転が消える**——S' が使ったのは畳んだ表（弱い波の集計1本）なので、
**移動を第2波でも買い、燃を第5波でも買っている。上限はまだ上にある。**

**理想61行との相関は、規則の良し悪しを予測しない**（第72期）。
**S は r 0.205（最高）で勝率 −5.65、S' は r 0.199 で勝率 +5.78、P は r 0.150（最低）が基準。**
**「理想編成に似た駒選び」と「勝てる駒選び」は別の量**——第71期は「Q5（r）が上がったのに
Q2（勝率）が下がった」を主題の入口にしたが、**r は規則の比較には使えない。**

**傾きの表は駒の値段を当てない**（第72期）。傾き計2位のボルグ（+25.9）の寄与は **−7.6**、
傾き計が負のガン（−8.5）・ネル（−6.5）が寄与の上位に並ぶ。**キーは通貨の名前であって駒の値段ではない**
（第71期 8-2 の再確認）——**S' はキーの値段を正しく買ったが、駒の値段を当てたわけではない。**
**S' は傷の2枚を締め出す**（ナタの在席 1794 → **24**・キリ 1244 → **21**）ので、
**S' の台ではその2枚が測れなくなる**（第70期「選択を入れると台の分別は上がるが測れる駒は減る」）。

**分母の側も箱を数える**（第72期）。理想61行で枚数効果を測ると
**傷の「1枚」は 1 行・破片の「2枚」は 1 行・毒の「2枚」は 1 行**しかなく、
**両側の箱が厚いのは 被弾（2/23/24/12）と 強化（20/32/9）の2本だけ**。
そこでは**被弾が +10.6 対 +10.1 と桁まで一致し、強化は +10.2 対 −2.4 と符号が割れる。**
**第71期 §9-4 の「傷・痺 の −15 はドラフト台に固有か」には「固有ではない」と答えられる**
（傷 −3.8 対 −6.8・痺 −12.2 対 −4.9 で符号一致）**が、傷の答えは 1 行が決めている。**
第69期「精度の条件は台が床に落ちているほど楽に通る」の**分母版**。

**「壊れている兆候」を書くときは、その兆候が仕様の別の節で予期されていないかを確かめる**
（第72期・自己検査 (f) の運用で初めて出た曖昧さ）。指示書 §5 は「S' の共有キー組数が S より大きい」を
「壊れている兆候」としたが、**§2-1 は同じ現象を「規則の目的ではなく帰結」と明記していた。**
**紙の段階で点いた**（2.93 対 2.89・差 0.04 組／`check` の 5,000 標本では同値）。
実体は §2-1 の側で、**S' が買った被弾は傾き最大（+19.36）であると同時にロスター最多の保持者（10枚）**
——**集まりやすいから買ったのではなく、傾きが最大だから買ったら保持者も最多だった**
（S が集めた 傷・痺 は S' で 0.88 → 0.01 / 0.59 → 0.16 に落ちている）。

**「噛まない駒を減らす」は規則を直しても理想編成に近づかない**（第71期の再確認）。
キー無し/編成 は **S' 0.60（4規則の最小）対 理想61行 1.16**。
**S' が締め出したリィカ（寄与 +17.4）とヴェル（+14.8）は S'w の寄与で上位**——
**傾きの表はキーを持たない駒を一律 0 と評価するので、「キーを持たないが強い駒」を買う理由を持たない。**

経緯は design/PHASE72_SLOPE.md。

**噛み合わせの枚数は、噛み合わせの値段ではない**（第71期・ドラフトの選択規則を3つ振った）。
「まだ選ばれていない駒から3枚提示 → 1枚選ぶ」を5回、**選び方だけ**を
N（無作為）/ P（攻7以上が2枚未満なら攻撃力最大・以後は最大HP＝第70期と同一）/
S（**すでに選んだ駒と共有するキーの数**が最大 → 同数なら攻撃力最大）で振った実測
（11,000 標本 × 6版 × 5波 × seed 8本 = 2,640,000 戦/帯 + 表D 5,280,000 戦/帯・両 seed 帯）:

    量（既存波・平均勝率・第2〜5波）        A 帯     B 帯
    P − N（攻撃力を数える価値）           +8.57   +8.65
    **S − P（シナジーを狙う価値）**       **−1.47**  **−1.51**   ← **負**
    弱い波での S − P                      −5.65   −5.57

**S は噛み合わせを実際に増やしている**——共有キーの組数 1.59 → **2.89**（**理想61行の 2.43 を超える**）・
相方あり率 44.5% → **68.7%**（理想 58.4% を超える）・理想61行との r も 0.150 → **0.205**。
**それでも第2〜5波の8セルすべてで負**（既存波 −0.99〜−1.87 / 弱い波 −4.77〜−7.48）。
**S が P に勝つのは飽和した第一波だけ**（+1.18 / +0.64）。

**理由はキーの符号を読んでいないこと。** 表E の枚数効果の傾きは **被弾 +19.4 から 傷 −15.6 まで 35pt の幅**があり、
S はそのうち**高く付く2本（傷 +0.26枚・痺 +0.26枚）を買い増して、唯一大きく報われる被弾を 0.13枚 手放す**
（加法の目安 −8.2pt に対し実測 −5.65pt）。**共有キー数を最大化する規則の実際の選好は
「そのキーの保持者数」と「同値判定の軸」で決まる**——傷5枚・痺5枚は1枚引くと残り4枚が候補に浮上して
連鎖して集まり、**被弾は保持者 10 枚と最多なのに減る**（被弾持ちは攻2〜3 が多く、同点時の攻撃力判定で負ける）。
**次にドラフトの選択規則を作るなら、キーの数ではなくキーの符号を読ませること。**
**理想61行はキー無しの駒を 1.16 枚/編成 抱えている**（S は 0.63）——
**「噛まない駒を減らす」は理想編成の性質ではない。** 経緯は design/PHASE71_DRAFT3.md。

**近い seed の `Random` を2本並走させない**（第71期）。`Random(2_000_000 + i)`（提示）と
`Random(3_000_000 + i)`（選択）を並べたら、**無作為5枚の「攻7以上の期待枚数」が
超幾何の 2.25 に対して 2.36**（10万標本の標準誤差の **32 倍**）になった。
**.NET は互換性のために seed つき `Random` の旧アルゴリズムを使い続けている**ので、
定数差の seed を並走させると相関する。**1本の系列から順に引けば消える**（第69・70期の抽選は1本だったので無傷）。
**phase0 に超幾何との照合を置いていたから測定前に見つかった。**

**ドラフト台の Q1（第2〜5波の中間帯 60% 以上）は4期目で通った**（**第72期に更新**）——
第69期 17.9%（無作為・既存波）→ 第70期 **57.1%**（素朴 × 弱い波）→ 第71期 **57.1%**（同じ版が最大）
→ **第72期 71.2 / 71.9%（傾き志向 × 弱い波）**。
**第71期の「選択規則を強くする方向では線に届かない。残る軸は敵の攻撃力か味方の枚数」は誤り**
——届かなかったのは**強くする方向**（共有キーの数）が違っていたからで、
**キーの符号を読ませると 54.6% → 71.2% と 16.6pt 動く。**

**判定式は、それが読む分布のヒストグラムを出してから書く**（第70期。自己検査 (a') の**6例目**）。
「中間帯（5〜95%）が 60% 以上」という判定は、**天井の波を集計に混ぜた瞬間に意味が反転する**
——第一波は平均 93.5〜98.6%（=100% が 90.5〜98.0%）なので、
**「第一波しか勝てない編成」の5波集計はちょうど 1/5 = 20.0% ＝中間帯のど真ん中**に落ちる。
実測で、**第69期が第2〜5波で 18.3% と測ったのとまったく同じ標本・同じ配置の版が 95.0%** になり、
**いちばん強い版（平均 57.2%）の中間帯が 83.8% で最下位・いちばん弱い版（24.4%）が 95.0%** と
**向きまで逆になる**（天井へ抜け始めた版から中間帯は減る）。
**「中間帯」は分布の形の指標であって、強さの指標ではない。**

**同じ 1,760,000 戦から、主因の答えが物差しで割れる**（第70期・ドラフト台の 2×2）。
無作為5枚 対 3枚提示から1枚選ぶ × 5回（編成の作り方）と、既存5波 対 敵のHPを一律 60%（波）の 2×2:

    物差し              波の寄与(B−A)   編成の作り方(C−A)   主因
    5波の中間帯            −1.58           +2.04           編成の作り方
    第2〜5波の中間帯       **+29.15**      **+17.19**      **波**
    平均勝率              **+16.10**       **+7.72**       **波**

**3つのうち2つが「波」を主因にし、指示書が採った1つだけが「編成の作り方」にする**
（両 seed 帯で完全に再現）。**交互作用の符号も割れる**——中間帯では劣加法（−7.6〜−11.6）、
平均勝率では**優加法（+9.04）**。**波ごとに見れば 5波すべてで A < C < B < D** で例外が無い。

**選択ありの編成は、静的な数値の上では理想編成と見分けが付かない**（第70期）。
3枚提示から「攻7以上が2枚未満なら攻撃力最大／以後は最大HP」で選ばせると
**総攻 48.9 対 48.6（理想61行）/ 総HP 376 対 372** になるのに、**既存5波の平均勝率は 32.09% 対 67.2%。**
**差は数値ではなく噛み合わせと配置にある**——これは Phase 0 の紙の計算で**測る前に言えた**。
**相方を持つ駒の割合は 39.8% → 44.5% としか動かない**（選択規則がキーを1つも見ていないため）。

**敵のHPを下げる操作は T_kill しか縮めない**（第70期）。勝敗の第一近似は
`敵の総HP < (味方の総攻 × 味方の総HP) ÷ 敵の総攻` なので、**理想編成と同じ余裕を与える倍率は
総攻比 × 総HP比**（無作為 0.78 × 0.88 = **0.69** / 選択 1.00 × 1.01 = 1.02）。
**HP を下げても T_die は1ターンも伸びない**ので、**敵の総攻がいちばん高い第二波（84）が
全波で最も硬いまま残る**（弱い波 × 選択ありでも 39.23% で5波中の最低）。
**台を直すノブは「敵のHP」ではなく「敵の攻撃力」か「味方の枚数」の側にもう1本要る。**

**通貨の枚数効果の順位は台の難度に依存する**（第70期）。第69期の
「枚数を増やして良くなるのは移動と被弾の2本だけ」は**ロスターではなく既存波の性質**で、
**弱い波では燃が3本目になる**（0枚 56.10 → 1枚 58.02 → 2枚 66.10 → 3枚以上 72.78%・両帯で再現）。
燃焼は「遅くて時間制限のあるダメージ」（第57期）なので早い決着では不利なはずだが、
**刻み 6 × 残3ターン = 18 が HP 60% の敵に対して初めて撃破に届く**——閾値をまたいだ側の効果。
**床から離すと負のキーの傾きも開く**（`2枚 − 1枚` の幅は既存波 −1.7〜+8.5 / 弱い波 −12.5〜+14.3）
——**「差が出ない」の半分は台が床にあったことによる。**

**在席時勝率は抽選規則のバイアスを含む。駒ごとの値を比べるときは、
その値が抽選規則を経由していないかを見ること**（第70期）。
**のろまの巨兵ドルガは在席時勝率が全51駒で最高（83.55%）なのに寄与は −11.6**
——攻撃力 38 なので選択規則が優先して引き、**素体でも同じ数値を持つ**（特性を外しても体が仕事をする）。
理想61行との相関は **在席時勝率どうし 0.14〜0.16 対 寄与どうし 0.47〜0.59** と 3 倍違う。
**選択規則は実効ロスターも狭める**——延べ在席の 80% を**上位 28 駒**が占め（一様なら 41 駒）、
**澱みのミオは 11,000 標本中 3 回・囃し立てのヒサは 13 回**しか出ない
（規則が「攻撃力 → HP」の2段しか見ないので、攻2〜3 で HP も低い駒は原理的に引かれない）。
**選択を入れると台の分別は上がるが、測れる駒は減る。**

**編成の中身は紙で出せるが、勝率は出せない**（第70期）。Phase 0 の超幾何（攻7以上の期待値 2.25）と
抽選 10 万回（2.25 / 3.07・総攻 37.9 / 48.9・総HP 328 / 376）は、11,000 標本の実測と
**0.5 ポイント以内で一致した。** 第69期の P1（勝率 30〜50% と書いて実測 6.58%）とは対照的で、
**違いは予測したのが勝率か編成の中身か**である。**攻撃力 7 以上はロスターに 23 / 51 体（45.1%）。**

**特性 → 通貨のキーの対応表は `TraitKeyMap`（`BattleSim/Program.cs` の末尾）の1箇所だけ**（第70期に集約）。
第68期 `carry solo` が作り第69期 `draft` が写していたので、3つ目の写しになる前に移した
（`carry` / `draft` / `draft2` が参照する。中身は1文字も変えておらず、
**`carry solo` の出力が byte 一致することが検算**）。**`Trait` に属性は足さない**（第48期 census の作法）。

**無作為に5体を引くと、8割が「第2〜5波を1つも勝てない」編成になる**（第69期・ドラフト台）。
`UnitCatalog.All`（51体）から無作為5体 × 11,000 標本 × 5波 × seed 8 本の実測で、
**平均 6.58%・標本の 78.9% が勝率ちょうど 0%・天井の標本は 0 件**（中間帯 5〜95% は **17.9%**）。
理想編成 61 行の第2〜5波平均は **59.4%** なので **9 倍の差**——
**60 期分の採否が乗ってきた台は、この分布の上位 0.1% に相当する場所である。**
床の形は CLAUDE.md の既知の症状そのもの（`100/0/0/0/0` ＝ 第2〜5波だけ集計すると 0.0%）で、
**実装前に机上で見積もれた**——攻撃力 6 以下の駒が **28 / 51（54.9%）**あり、
5体中3体以上をそこから引く確率は超幾何で **59.5%**。
**新しい台を作るときは「その台で編成が死ぬ確率」を先に計算すること。**

**信頼区間を判定式に使うときは「分散が小さいのは測れているからか、動いていないからか」を
一緒に数える**（第69期。第68期 Q1 と同じ形の**5例目**）。ドラフト台の Q2（30駒すべてで ±1.5pt 未満）は
**通ったが、通った理由の半分は台が床に落ちていること**——標本の 78.9% でフルも素体も 0% になり、
**寄与が定義上 0・分散も 0** になる（寄与 ±0.5pt 以内の 10 体は信頼区間 ±0.05〜±0.37）。
**精度の条件は、台が床に落ちているほど楽に通る。**

**`ablate`（1枚抜き）と素体差し替えは別の器具である**（第69期）。**同じ 61 行・同じ seed 帯でも
r = 0.782・最大差 41.8pt。** 食い違いは**傷・痺の4枚**に集中する
（責め苦のシガ 素体 +2.1 対 抜き +34.2 / 抉りのエグ −0.4 対 +33.5 / 断ちのナタ **−17.4** 対 +24.4 /
縫いのハリ +3.8 対 +35.8）——**特性を外しても体としては働く駒**では、
「抜くと弱い」と「特性が効いている」がまったく別の話になる（第21期 swap の指摘の定量版）。
**断ちのナタは理想編成でも素体差し替えで −17.4pt**（＝特性を外したほうが強い）で、
**この符号は `ablate` では見えない。**
**散布を取る前に、両側の器具が揃っているかを確かめること**——第69期は器具を揃えなければ
台の差（r 0.354）を 0.21 と読んで 1.7 倍に見積もっていた。

**枚数を増やして良くなるキーは 11 本中 2 本（移動・被弾）しかない**（第69期・表E）。
無作為編成 11,000 標本を、キーを持つ駒の枚数で分けた平均勝率:

    移動  0枚 2.48% → 1枚 8.91%（+6.42）→ 2枚 18.19%（+9.29）→ 3枚以上 28.35%（+10.16）  ← **優加法**
    被弾  0枚 1.74% → 1枚 6.63%（+4.89）→ 2枚 11.51%（+4.88）→ 3枚以上 18.12%（+6.61）  ← ほぼ線形
    残り9キーは**1枚目でまず下がる**（毒 −2.39 / 傷 −2.80 / 燃 −2.85 / 強化 −0.30）

下がるのは**枠を1つ使う機会費用**（そのキーを持たない殴り役が1枚減る）で、
**移動と被弾だけがその費用を1枚目から取り返している。**
**第68期の「時間に分布する2本（被弾・強化）」とは1本しか重ならない**——強化はここでは単調に下がる。
**台で順位がいちばん上がった3枚も3枚とも被弾**（ムド 20→4位 / スィド 22→6位 / セッキ 23→8位）。

**「相方がいないほうが強い」は n = 1 の行が作った像だった可能性が高い**（第69期）。
ドラフト台で相方の有無により寄与が 1.5pt 以上動く駒は 12 体で、**12 体すべてが「相方がいないと弱い」側**
（判定式は符号を要求していない）。第68期の理想編成では正3体が出ていたが、
**ソラ・ボルグ・カドはいずれも相方なし行が 1 行しか無かった**——
ドラフト台では相方なしの標本が 174〜903 件あり、**符号は1体も割れない。**
ただし**相方なしの寄与がちょうど +0.0 になる駒が 8 体**あり、これは丸めではなく**床**である
——**Q5 の「相方は要るか」は、正確には「相方がいないと寄与が測れない」。**

**配置の価値は波ごとに符号が割れる**（第69期・両 seed 帯で再現）。規則配置
（硬い2枚を前・攻撃力の高い2枚を後）と無作為配置の差は **第3波 +1.48 / 第4波 +1.33 / 第5波 −1.02** で、
**全体の +0.52pt はその打ち消しの残りかす**（第52期の分類の (c) 型）。
**「5スロットX字は配置の判断を生んでいない」とは書けない**——
**判断は生まれているが、ひとつの規則では波ごとに符号が逆を向く。**
経緯は design/PHASE69_DRAFT.md。

**情報セル（Q4）は「機構が弱いこと」ではなく「既に勝っている波をさらに押したこと」を検出する**
（第66期・軋みが響く＝**採用しなかった**）。`AtkBonus >= 閾値` で単体を薙ぎに変える
`DisplacedTrait.ModifyPattern`（**第67期に条件を `WhetReceived` へ差し替えたので、
現行のコードは `AtkBonus` を読まない**）は、3行すべてで帰属 **+17.1 / +10.8 / +13.5pt**（両 seed 帯で幅 1.1pt 以内・
第56期以降の強化まわりで最大）を出し、Q1・Q2・Q5・Q6 を全部通したのに **Q4 で落ちた。**
落ちた変化は両帯とも同じ形——**V0 で 96.5〜99.2% だった第五波が 100.0% になった**。
**「天井は同値塊を作る」（第22・54期）が新機構の合否で実際に効いたのはこれが初めて。**

**「発火率 10〜90%」という判定は、条件と起動を区別しない**（第66期）。閾値 9 は
軋みの1回の上昇（9・突き出しなら 22）と同じなので**「一度でも動かされたら」と同義**だったのに、
薙ぎ化率は 85.9% で上限 90% の内側に収まった。分けたのは
**初到達ターン（1.55）と、その結果どこへ行ったか（天井）**のほう。
**次に閾値つきの変質を測るときは、Q1 を割合ではなく初到達ターンで書くこと。**
3版（閾値 9 / 18 / 30）すべてが **Q1 か Q4 のどちらかで落ちる**ので、選び直す抜け道は無い
——**測る前に判定を固定した効果**である。経緯は design/PHASE66_CREAK.md。

**巨躯は `DepthOf(wall) < DepthOf(target)`。吐き戻しを受けたい駒は壁の「後ろ」に置く**（第66期）。
しかも**壁を混ぜる駒（喧噪）と壁を同席させると肩代わりの機会そのものが減る**
——実測で、動かし役を1枚足した実演行のほうが `突き出し` より吐き戻しが**少ない**（0.41 対 0.92 点）。
第57期「隣接規則に乗った供給は隣接が定数なので供給も定数になる」の**深さ版**で、
こちらは定数になるのではなく**動かした結果として減る。**

**条件の粒度を決めるのは「誰が作る値か」ではなく「その値が時間に分布しているか」**
（第67期・軋みが響く2＝**採用しなかった**。第66期の一般則の訂正）。条件の出どころを
`AtkBonus` から **`UnitState.WhetReceived`（`Whet` 窓口を通って届いた累計）** へ差し替えると、
**主判定 Q3'（供給元を素体に落とすと薙ぎ化率が 1/5 以下）は比 0.00 で通り**、
第66期が唯一落ちた **Q4（情報セル）も通った**のに、**今度は Q1 で落ちた**。
落としたのは3版すべてで**同じ1行**——`隊列崩し` の `押され` は
**第1ターンにちょうど 8.0 が一括で届き、その後1点も増えない**（格子 1〜8 の到達T が全部 **1.00**、
12 以上は **0.0%**）。出どころは号令の鬨（`OnBattleStart`・全体 +4）が **2回**で、
本人ぶんの +4 と、**隣のガルド（`Stoic`）の `SupportTargets` が漏らす +4**。

    移動改（移り木・毎ターン）        薙ぎ化率 80.7 → 37.7 → 16.2%   閾値に対して単調＝条件として働く
    突き出し（吐き戻し・被弾のたび）  50.8 → 12.0 → 0.3%             同上
    **隊列崩し（号令の鬨・開戦時1回） 100.0 → 100.0 → 0.0%**         **2値。閾値が条件にならない**

**「外から来る量」を条件にしても、供給が開戦時の一括なら粒度は出ない**——第66期の
「自分で作れる値を条件に使うと閾値は起動スイッチになる」は**必要条件でしかなかった**。
**強化を条件に読む機構を作るなら、供給を「量」ではなく「刻み方」で数えること**
（第56期の経路別上位2本は 吐き戻し 48.3% と 号令開戦 27.0% で、**性質が正反対**）。
**ヨミの条件付き変質は第66・67・77期で3回落ちて閉じた。同じ駒で4回目は作らない**
（第62〜64期のヌキと同じ線）。**3回目は「台の変更に伴う基準の変更」として第67期終了時点で
条件が書かれていた再測定**（ドラフト台で自己供給が強いと実測されたら V9 を測り直す）で、
その条件が第72・76期に満たされたので回した。**採否は3回とも Q4（情報セル）で落ちている。**
経緯は design/PHASE67_CREAK2.md と design/PHASE77_CREAK3.md。

**`OnBattleStart` は `OnTurnStart` よりさらに前の席で、しかも1回きり**（第67期。第61期
「`OnTurnStart` は speed = ∞ の席」に足す）。第67期の予測は P1・P5・P6 の3つを外したが、
**外した3つはすべて同じ1行が原因**で、**「供給の総量」で順位を予測して到着時刻を数えなかった**
1つの見落としだった。**供給の順位を予測するときは、フックの位置を先に並べること。**

**`Stoic`（ガルド）の隣は強化の在庫が倍になる**（第67期）。号令は `SupportTargets` を通す
3経路の1つなので、ガルドが受け取れないぶんが隣へ漏れる。第41期の「ハネの隣をガルドで固める」
（弱体を漏らさないので得）と**同じ非対称の逆側**——**強化側でも隣が得をする。**
`Whet` の6経路は `AcceptsSupport` の扱いが3通りに割れたままで、**棚卸しはまだしていない。**

**`UnitState.WhetReceived` は盤面を1ビットも動かさない**（第67期。`compare` 305 セル 0 件）。
書くのは `Whet` 窓口の1箇所だけ（`AtkBonus` に加算するのと**同じ行・同じ条件**）で、
寿命も `AtkBonus` と同じ2箇所（`Revive` / `Engagement.CarryOver`）で 0 に戻す。
**`Dull` では減らさない**——閾値は**累積の床**であって在庫ではない。
**却下した代案**（正味 = `Whet − Dull` を読む）は、読んでいる量が「外の供給」ではなく
「弱体との差」に化けるため。**読み手は現状 `DisplacedTrait` だけで、それも `Threshold <= 0` で不活性**
——**「強化の2枚目の読み手」の席は空いたままである**（第65期 積み残し1'）。

**薙ぎ化のような「型を変える」機構では、発火率の分母（`Attacks`）自体が版で動く**（第67期）。
薙ぎが敵をまとめて倒して決着が早まるので、`振/戦` は4行すべてで減る（5.65 → 4.29 等）。
**発火率を出すときは分母の変化を必ず併記すること。**

**「外から届いて、しかも時間に分布している量」はロスターに2本しかない**（第68期・棚卸し）。
51体 × 61行 × 11キー = 3355 セルを、第67期の格子（1/2/4/6/8/12/16/24/32）で
**2値 / 連続 / 不発**に分類した実測。**連続の 313 セルのうち 被弾 199（63.6%）と
強化 64（20.4%）で 84.0%** を占め、残り9キーの合計は 50 セル（1.5%）:

    キー   連続  2値   他   不発   ← 分母は 駒 × 行 = 305
    被弾   199   18    88    0     ← **不発が 0 セルの唯一のキー**
    強化    64   24    29  188
    移動    11   25    73  196     ／ 弱体 10/28/7/260 ／ 手番 10/1/31/263
    破片     8    1     5  291     ／ 燃 4/16/45/240 ／ 毒 3/9/28/265 ／ 痺 3/0/16/286
    標       1   15     2  287     ← **標・弱体・燃・移動は 2値が連続を上回る**
    傷       0    0     0  305     ← **味方に傷が載る経路は1つも無い**（第49期の全数確認）

**第67期 7-2「開戦時一括だと粒度が出ない」は正しいが、条件はもっと厳しい**
——**粒度が出るキーそのものが2本しかない。** 経緯は design/PHASE68_CARRIERS.md。

**`OnDeath` の供給は必ず2値になる**（第68期）。破裂の着火（ゾト）は受け手側の連続が
**0 / 45 セル**で、連続が1つも無い唯一の「事象ごと」の経路。死は1回きり（蘇生を除く）なので
**時刻には分布するが量には分布しない**——第59期「一度きりの供給は蘇生役がいると
一度きりでなくなる」の粒度側の帰結。**0/1 のキー（標・痺）も同じ**で、
標の3経路のうち囃し立て・逸らしは受け手側の連続が 0。

**「載せられる駒」を数えるときは被弾を分母から外すこと**（第68期）。
表B（連続の行が2行以上ある 駒 × キー）は **53 組・33 駒**だが、
**33 駒は全員が被弾で載っている**（被弾の組数 = 駒の実数 = 33）。
被弾は不発 0 セルなので**格子に対して連続になるのは定義に近い。**
被弾を外すと 20 組——**強化 13 / 移動 3 / 手番 2 / 痺 1 / 燃 1。**
**強化の 13 駒のうち 3 枚（リィカ・セロ・ヨミ）は既に `ModifyPattern` を持ち、
1 枚（ドルガ）は既定が薙ぎ、1 枚（カド）は `振/戦 0.00` で攻撃型の変質が原理的に載らない**
——**差し引き 8 駒**（ヴェル・ムド・ノミ・ソラ・ザン・バン・ガン・セッキ）が
第65期 積み残し1'（強化は供給 16 対 読み手 1）の空席である。

**`Stoic` は減衰器ではなく「次数ぶんの増幅器」である**（第68期。第41期と第67期 7-4 の統合）。
`SupportTargets` は**割り算をしない**ので、ガルドが受け取らないぶんは**隣接それぞれが満額**受け取る。
実測で **漏れ ÷（本人ぶん × 次数）** が **0.98〜1.09** に収まるのは 27 セル中 **12 セル**で、
**その 12 セル全部が「開戦時一括の供給が `SupportTargets` を通った」行**
（弱体＝呪詛・萎縮の6セルは**きっかり 1.00**、強化＝号令を含む6行）。
**`SupportTargets` を通らない経路（縛め・移り木・火選り・突き返し・分かち）は 比 0.00〜0.51**
——そこで見える差は漏れではなく**取り合いに1枚増えたぶん**（縛め収入型で 0.51）。
**吐き戻し（被弾ごと・通るが毎回）は 0.58〜1.23 に散る**（対照が盤面を動かすぶん）。
**34 行すべてでガルドは角（次数2）**なので、**現行の盤面は増幅の半分しか使っていない**
（中央なら4倍）。

**`Stoic` は `Dull` の7経路のうち「分かちのなまり」を止めていない**（第68期）。
なまりは `Dull` で唯一の**無検査**経路なので `AcceptsSupport` を見ずガルドに直接載る
（実測で 34 行中1行だけ本人の弱体が 7.84 量/戦）。
**「誰の助けも届かない」という1文と実装が食い違っているのは、現状この1経路だけ。**

**ロスターの過半（27 / 51）は、相方のいない盤面で一度も測られていない**（第68期・表D）。
相方＝その行で同じキーの書き手／読み手である他の駒。**相方の有無で寄与が 20pt 以上動く駒が5体**
——**負（コンボ依存）**: ゾト −38.6 / ネル −28.7 / グザ −25.2 / ヒサ −18.6、
**正（相方がいないほうが強い）**: ソラ +28.9。
**ヒサの「相方なし」は +4.7pt しかない**（標の読み手がいない行では入れ得に近い。
第52期「標の符号は読み手がいるかで決まる」の寄与版）。
**現在の 61 行はすべて理想編成なので、この 27 駒についてはドラフト台を作るまで何も言えない。**

**状態異常には集約された窓口が無い。数えるなら `UnitState.SetCounter` に1箇所置く**（第68期）。
`Whet` / `Dull` / `Ignite` に相当するものが 毒・痺・標・傷・破片 には無く、
`Traits.cs` の 16 箇所から直に書かれている。**カウンタの setter に通知を1つ置けば、
経路を追加しても数え漏らさない**——第57期の `burn` が計数を書き手ごとに置いていたのの反対側。
**`AtkBonus` を 0 に戻す2箇所（`Revive` / `Engagement.CarryOver`）は `ResetAtkBonus()` を通すこと**
——通常の代入だと、負の補正を背負った駒を蘇生したときに**負 → 0 が「正の上昇」として帳簿に載る。**

**格子を全キーで共有するなら、被弾は「量」ではなく「回数」に当てる**（第68期）。
1発 10〜38 なので格子の上限 32 を1〜2発で越え、全格子点が飽和して情報を持たない。
**「到達Tが単調増加」は帯（20〜80%）の中でだけ判定する**——裾の到達Tは
「そこまで届いた試行」の偏りで前後するので、全点で見ると第67期が「連続」と読んだ形
（`突き出し`）が落ちる。

**`TURN_SLOT_PLAN.md` はリポジトリに存在しない**（第68期に確認）。第66期・第68期の指示書が
引いているが実体は0件で、第66期の報告はこの食い違いを記録していない。
資格基準は `design/PHASE66_CREAK_SPEC.md` §0-2 の4項目を使うこと。

**`ModifyPattern` に規則を渡す窓口は `UnitState.Board`**（第46期に足した盤面参照）。
`Board` が null（盤面の外で作られた `UnitState`）のときは**不活性**にすること。
`CreakRule.Threshold <= 0` は乱数も計数も盤面も1ビットも動かさない（`compare` 305 セル 0 件が検算）。

**`Trait.ModifyAttack` は `BattleContext` を受け取らない。** 盤面を読む条件（隣に誰がいるか）を
攻撃力に載せるために `UnitState.Board`（`ctx.Add` が差す盤面参照）を足した（第46期）。
**条件を `Counters` にキャッシュしないこと**——戦闘中の揺れ（隣が倒れる・隣が育つ）が消える。
**盤面の外で作られた `UnitState` では `null`** なので、読む側は「隣が1人もいない」と同じ扱いにする。

驕りは棄却したので `UnitCatalog.All` にも `CompareBuilds()` にも載っていない（逆位・まどろみ・
誹りと同じ扱い）。**測った2編成は診断 `overbear` のローカル（`ObRows()`）にある**ので、
`CompareBuilds()` を1行も動かさずに全部を測り直せる（`overbear` の §5 が `seats2` の写し）。

`layout` は「どう置くか」の粗い当たりを付ける道具で、その値で採否を決めてはいけない。
seed 50 の 720通りの最大なので上位は運で入れ替わり、狙い（ガルド前列・セッキ後列）も無視する。
`reseat` で狙いを満たす候補を含めて測り直し、`confirm` で選定に使っていない seed に当てて採否を決める。
`reseat` の第1引数はカンマ区切りの部分一致（省略で compare の全編成）。`skip` / `take` で更に切り出せる。
長時間ジョブを分割して回すためのもの（下記）。

**`reseat` の120通りが 20.0% で並んだら、それは配置の答えではなく「台が死んでいる」の合図。**
どこに置いても動かない＝1ビットも情報が出ない編成で、原因はほぼ必ず**総攻**（払い出しの駒を
積みすぎている）。第26期・第28期で連続して同じ症状が出た。**特性や数値を触る前に出力役を
入れて組み直す**——新特性の検証中にこれを取り違えると、壊れているのが機構なのか編成なのかが
決まらないまま数値をいじることになる。
**予算は1枠ずつ返しても戻らない**（第29期）——払い出し4枠のうち1枠を出力役に作り替えても
20.0〜20.3% は1ビットも動かなかった。**部分的な返済では台は生き返らない**ので、
組み直すときは払い出しの枠数そのものを減らす。

`chain`/`ablate` は勝率表（compare）が見落とす軸を測る道具。`chain` は「2枚で人並みに勝つ」編成と
「5枚が畳みかけて無双する」編成を区別する（勝率だけだと同じ100%に見える）。
`ablate` は編成から
メンバーを1体ずつ抜いて勝率の下がり方を見る道具で、差がほぼ無い・あるいはプラス（抜いた方が
強い）なら、そのメンバーは入れ得の疑いがある。`ablate` の絞り込みは `reseat` と同じ書式
（カンマ区切りの部分一致、省略で compare の全編成、全編成だと30秒前後かかる）。

`chain` の `残存`（勝った試行だけの生存数）と `全滅勝ち`（生存1体での勝率）は**勝ち方の質**で、
連鎖深度とはさらに別軸。追撃×毒 は連鎖深度2.99と高いのに勝った試行の59%が生存1体（相打ち同然）で、
逆に「単調」と評された逆しま改は連鎖深度1.17ながら残存3.9/5・全滅勝ち0%と一番きれいに勝つ。
畳みかけることと、きれいに勝つことは同じではない。

`pulse` は編成の中で**誰が仕事をしていたか**を見る。compare は編成の勝ち負けしか見ず、
ablate は1体抜いた勝率差しか見ないので、どちらも「出力で効いているのか、場を作って効いているのか」
を区別しない。`振/T`（攻撃を振った回数）と `干渉/T`（実際にダメージを通した回数）のズレが形を示す。

    振 ≒ 干渉 ≒ 1.0   自分の手番で殴るだけ。数値であって出来事ではない
    振 ≒ 0 / 干渉 大   反応型。手番を持たず、起きたことに反応して盤面を動かす（カド）
    振 大 / 干渉 ≒ 0   空振り。毎ターン振っているのに何も起きていない（クビ・ネル・ヒサ・ノノ）
    振 ≒ 0 / 干渉 ≒ 0  置物。発火条件が満たされていない

**`干渉 0` は「価値が無い」ではない。** 呪詛・萎縮・庇いはダメージを経由せずに盤面を変えるので
この列に出ない。`pulse` が測るのは**体験の密度**であって貢献度ではなく、貢献度は `ablate` の側で見る。
この表だけで駒を消すと、静かに効いている駒から先に消える。

`engage` は会戦（`EngagementEngine`）で測る。compare が各波を独立した1戦として測るのに対し、
勝った部隊は生存駒の HP・最大HPの損耗・蘇生回数・墓守の層(-1) を持ち越して次の波と戦う。
状態異常と攻撃力の一時変動は波の境界で消える。部隊列は `EnemyCatalog.Columns` の3本
（順路＝既存5波・逆順＝強い波が先頭・地点＝先頭3波）を1回の実行で全部測り、1ファイルに
節で出す。**主表は地点（3波）× 投入部隊数 1〜3**（同一編成の複製。5波の順路では全編成が
突破 0% に潰れるが、地点は部隊数を積むと突破率が 0〜100% に散る——第3期で切り替え）。
`非線形`（期待突破数(2部隊) ÷ 期待突破数(1部隊)×2）が 1.00 を超えるなら、第1部隊の削りを
第2部隊が拾えている。順路は参考で、第二波が代金になる消耗の位置を `入場戦力`（各部隊戦に
入る時点の生存数と HP割合。分母は編成全体の定義上総最大HP）で読む。逆順は `第1削り`
（勝てない編成＝特攻隊の価値）専用。突破数の表は載せない（逆順は初戦＝第五波の勝敗しか
測らず、第五波の独立勝率の測り直しにしかならないため）。
`seats` は会戦の隊列持ち越し診断。第2戦・第3戦の入場スロットが初期配置からどれだけずれているかを
編成ごとに集計する（D5「Slot は維持」が移動系編成に課す代金の可視化。同定は UnitId で行う）。

`charge` は**大技の発火率**と、チャージ化の前後を同じ実行の中で突き合わせる（第10期）。
「前」は同じ敵から `Actions` だけを剥がした複製なので、git を戻さずに前後が読める。
発火率を先に見るのは、チャージ化の最初の失敗の形が「周期が長すぎて大技が1回も出ないまま
決着し、波がただ半額になる」だから。代金や突破度より前に、**実際に何回発火したか**を確かめる。
この診断が使う台（`ChargeBench`）は測定台 113% とは別物——**測定台には全体持ちも貫き持ちも
1体もいない**ので、あの上で測るとチャージ化の前後で数字が1つも動かない。

`timing` は**味方側**の行動パターンの変種を測る（第11期）。`charge` が敵の周期を前後で比べるのに対し、
こちらは同じ敵に対して**味方の周期だけ**を差し替える。変種は `UnitCatalog` を書き換えずに
診断のローカルで組む（`gradient` / `aim` と同じやり方）ので、`UnitCatalog` は基準の形のまま。
台は2種（チャージ台と既存5波）で、片方だけでは第8期の「136% で測ると何も見えない」に嵌まる。

**この診断の要は V1 と V3 の対照。** 周期が同じ（隔ターン）で位相だけ逆なので、
「何回撃ったか」と「いつ撃ったか」を分けて読める。片方だけ（V0/V1/V2）だと、
周期を伸ばしたときに落ちたのが回数のせいか位相のせいかが決まらない。

`power` は編成の**「地力」の中身**を測る（第12期）。第4〜11期の測定は、何を作っても編成の序列が
同じ順位で出てくる壁に当たり続けた（順位相関 0.83〜1.00）。支配的な次元が1本あるのは分かっていたが、
**その1本を一度も測っていなかった。** 編成ごとに静的8種（体数・総HP・総攻・積・最薄HP・後列HP・
平均速度・範囲枚数。戦わなくても分かる）と動的7種（`UnitTally` から。新しいフィールドは足していない）を
出し、突破度との単相関で並べる。台は `timing` と同じ2種。

**この診断は何も直さない。純粋な測定で、盤面は1つも動かない。** n=31 しかないので多変量は2変数まで
（3変数以上は過学習）。相関は因果ではない——「総HPが高い編成が強い」は「総HPを上げれば強くなる」を
意味しない。読み方は README「地力の分解」を見ること。

**与ダメと撃破は受け手側（敵の tally）から取る**（第13期 Phase DA）。`TickStatuses` は
`ApplyDamage(u, poison, null)` と source を渡さないので、毒・燃焼の削りは出どころの駒の
`DamageToEnemy` にも `Kills` にも載らない——味方側から合計すると毒軸の編成の出力が構造的に
過小になる。どの経路で削っても敵の `DamageTaken` には必ず載るので、敵側から数えれば穴が塞がる。
**エンジンは触らない。読み方を変えるだけで済む。** 第12期の味方側の値も同じ実行の中で計算して
対比表に出す（別の実行から引くと、動いたのが定義のせいか実行のせいか決まらない）。
`干渉/戦` だけは味方側のまま——毒は出どころを持たないので受け手側に対応物が無く、
毒軸の `干渉/戦` は依然として過小（`docs/pulse.md` も同じ）。

**特徴量を足すときは同語反復の判定を先に通す**（第14期 Phase EA）。第13期に穴を塞いだら
第一近似が `撃破/戦` r² 0.90 になったが、**部隊の全滅＝突破なのでこれは算術**だった
（全抜きした編成の値は例外なく 13÷3 = 4.33）。基準は「**突破という結果の言い換えに
なっていないか**」の1本だけで、**「信頼できるか」を混ぜない。** 言い換えの経路は
分子経路（量そのものが突破の定義に含まれる）と分母経路（`部隊戦数 = 突破数 + 1`）の2つで、
**外すのは分子経路だけ**——比（`自傷率`・`与ダメ効率`）は分母経路ごと打ち消える。
除外後（候補13種）の第一近似は 主 `総攻` r² 0.308 / 従 `与ダメ効率` r² 0.242 で、
**台で第一近似が入れ替わる。** 判定の全表と根拠は README「同語反復を候補から外す基準」。

Phase EB は反撃軸の残差を第9期 `bill` の自傷率と突き合わせる。**新しい計測は足していない**
——`MeasureBill` と `BattleEngine.Run`（単発戦の勝率＝`compare` と同じ計算）を同じ実行の
中で呼び直すだけ。別の実行から引くと、動いたのが定義のせいか実行のせいか決まらない。

`bench` は**台をまたぐ入れ替わりが構造的か**を判定する（第13期）。台間の相関が 1.00 未満でも、
乱数のばらつきだけでそうなるので、**「どれくらいなら動いたと言えるか」の基準が先に要る。**
同じ台を seed で半分に割って（前半後半 / 偶奇の2通り）両半分の相関を取ると、それが
**同じ条件を2回測ったときの一致度＝測定の信頼性の上限**になる。台間の相関はこれと比べて読む。

台は 2×2 の格子（長さ3/5 × 主構成/従構成）を診断のローカルで組む。主↔従は格子の**対角線**で
長さと構成が同時に動いているので、そのままでは何が入れ替わりを生んだか分からない。

**長さの辺は「振ったつもり」になりやすい。** 誰も届かない波を足しても測定は1ミリも動かない——
`4波目に入った試行` の列がそれを検出する（0% に近ければ、その辺は情報を持たない）。

`wave` は**編成 × 波の交互作用**を単発戦の勝率で測る（第15期）。**主の物差しが単発戦に
変わった**ので（README 冒頭「主の物差しは単発戦」）、波の評価も突破度・代金ではなく勝率でやる
——代金は HP を持ち越す会戦でしか意味を持たない。既存5波 + 第5〜10期に `gradient` / `aim` /
`flip` / `bridge` のローカルへ散らばっていた候補波 34 を**1箇所（`WaveCatalog()`）に集めてある**。
`EnemyCatalog` には足さない（採用が決まっていない波を入れると `compare` / `dump` が動く）。

**集め方が写しであることを毎回検算する。** `MeasureCost` を同じ関数のまま呼び直して
`gradient` / `aim` / `flip` / `bridge` の 代金・向き・ターン数 と突き合わせ、ずれ 0 件を確認する。

**候補波の定義は `WaveCatalog()` の1箇所だけ。** `wave` も `dissect` も同じ関数を呼ぶ
——2つ目の診断がコピーを持った瞬間に、第15期が「1箇所に集める」ためにやった作業が消える。

**天井・床の波は評価に寄与しない。** 勝率 100.0%（天井）や 0.0%（床）で並ぶと順位が同値塊に
なり、その中の編成は区別できない。**39 波のうち寄与するのは 7 波だけ**で、候補34波では 3 本
（R8/R9/R10）しかない——**代金の帯を狙って作った波は、単発の物差しでは全部が天井に並ぶ**。
判定は3通り（(a) 半割が取れた波・勝率 / (b) 寄与する波だけ・勝率 / (c) 全波・残存度）を
必ず全部出す。**(a) は当てにならない**（同値塊のせいで `余地` が 1.0 を超える）。

Phase FB は同じ実行の中で地力の分解を単発版でやり直す。**同語反復の判定は目的変数ごとに
引き直す**——単発では分母経路が消え、`被ダメ/戦` が分子経路（味方の全滅＝敗北）に回るので
候補は 13 → 12 種。第14期の突破度の数字も同じ実行の中で `MeasurePower` を呼び直して並べる。

`dissect` は**交互作用の個別事例を解剖する**（第16期）。第15期で「交互作用は実在するが
予測できない」まで来たので、決めるのは**法則が無いのか、特徴量が悪いのか**。`power` の
15特徴量は集計量ばかりで、**プレイヤーが実際に見ている情報**（誰が誰を殴るか・貫きが後列に
届くか・範囲が何体巻き込むか・毒が乗り切る前に敵が落ちるか）を1つも含んでいない。

材料は `BattleResult.Events`（`verbose: true`）から組み直す。**`BattleCore` は触らないし、
文字列（`Log`）も解析しない。** 振りの範囲は「Attack イベントから、同じ手番の同じ actor が
出した Damage まで」で切るので、反撃（actor が違う）も毒（actor が null）も追加のフラグ無しで外れる。

**解剖するペアは探索で選ばない**（3組で固定）。毎回「入れ替わり最大」を探すと、波の定義が
動くたびに対象が入れ替わって期をまたいだ議論が繋がらない。ペアの中の編成は**順位差で
機械的に**上下2つずつ——手で選ぶと「説明が付く事例を選んだ」になる。

**解剖の前に seed による振れを確かめる。** `全滅`（削られ切った）と `打切`（30T で削り切れ
なかった）が両方 5% を超える事例は**敗因が2種類あるので1つの説明では足りない**。

Phase GB は敵側9特徴量と交互作用項10個。**総当たりで作らない**（編成8 × 敵9 = 72 通りを
全部試すと n = 217 では必ず何かが当たる）。**分散分解を先にやる**——加法モデル
`波の平均 + 編成の平均 − 全体平均` を引いた残りが交互作用成分で、これを測らずに相関だけ
出すと「積が効いた」が波の主効果を拾っているだけになる。

> **片側だけの特徴量は交互作用成分と相関が恒等的に 0**（残差は行にも列にも和が 0）。
> **波ごとの説明力も原理的に上がらない**（波の中では敵側が定数なので、交互作用項は
> 味方側の量の定数倍）。**第15期と比べる場所は「波ごと」ではなく「交互作用成分」。**

`output` は**編成の出力の実体**を、目的変数から独立に測る（第17期）。**循環に注意**——
目的変数（波ごとの勝率）と同じ戦闘から与ダメを取ると、敵を削り切ることが勝ちなので
第14期の同語反復（分子経路）にそのまま当たる。**固定の参照台で1回だけ測り、その値を
全波に対する特徴量として使う**ことで循環を切る。

参照台は既存の `EnemyCatalog` の def を並べただけの的（**新しい `UnitDef` は作らない**）。
**単一 def・単体攻撃のみ**にしてあるのは、混成や攻撃型が入ると「どの駒に当たったか」で
編成ごとに条件が変わるため。

**参照台には門がある。呪詛（ネル）が敵全体の攻撃を −6 する**ので、1体あたり攻が 6 以下の
def を並べると呪詛入りの編成には 1ダメージも通らず、**反撃も被弾強化も走らない**
（`OnDamaged` が呼ばれない）。`決着T` や `味方全滅%` からはこれが読めない——巡礼6（攻4）は
下見でいちばん要件に適って見えて、反撃軸の `手番外%` が 0.0% だった。**`手番外%` を必ず見る。**

**中立性は 2 台で確認してから使う**（性質の違う的で順位が一致しなければ、出力は単一の
特徴量にできない）。比べる相手は半割の上限で、線は第15期の裏返し——`ρ ≥ 0.90` **または**
`余地 < 0.05`（**連言の否定は選言**。ここを連言にすると第15期より厳しい線を黙って作ることになる）。

測る量は3つで、**近似は1つも使っていない**（(C) も `Events` から実測）。

    (A) 実効打点/T  参照台での総打点 ÷ 総ターン数。毒・燃焼・反撃・破裂を全部含む
    (B) 育ち        (T5累積 − T3累積)/2 ÷ T1打点。1.00 が「まったく育たない」
    (C) 手番外%     打点のうち手番の振り以外から出た割合

**(B) は決着で窓が閉じる。** 早く決着する編成の (B) は「育たなかった」ではなく
「窓が閉じた」を測るので、`到達%` と一緒に読む。**打点は `Damage` イベントから取る**
（`Status` の量は適用**前**なので、破片で吸われたぶんと食い違う。`dissect` の `毒燃/戦` は
`Status` 側で、そこだけ流儀が違う）。

`convert` は**出力が撃破に変換されるか**を、参照台を**個体HP だけ変えて**測る（第18期）。
変換率 β = `d ln((A)) ÷ d ln(個体HP)`（刻み全部を使った log-log の傾き）。

**個体HP は単独では振れない。** 総HP ＝ 個体HP × 体数 なので、体数を止めれば総HP（＝戦闘長）が動き、
総HP を止めれば体数と総攻が動く。**2つの系列は逆を向く**（ρ −0.53）ので、**どちらを主に採ったかを
必ず書く**——`convert` は「振るのは個体HP だけ」に文字どおり従う**系列P（体数6固定）**を主にし、
食い違いが体数の辺で説明が付くかを**辺（系列R・個体HP 固定で体数だけを振る）**で検算している。

**(A) はオーバーキルを含む。** `ApplyDamage` は残HPで切り詰めない（`Amount` は素の量、`HpAfter` が
0 止まり）ので、**(A) は「敵のHPに変換された量」ではなく「振り下ろした量」。** 変換率は
「硬い的で出力がどう変わるか」を測るが、**「その出力が無駄になっているか」は測っていない。**

`route` は**自傷の燃料が変換器まで届く配置**を測る（第19期）。「置き去り×被弾強化」の
メンバーを固定し、カドを中央に残したまま席だけを振った5変種を出す。`CompareBuilds()` は
触らず、変種は診断のローカルに組む（`gradient` / `aim` と同じ扱い）。

**V3 と V4 の対照が要。** 巨躯の被覆から出る方法は「壁と同じ深さか、より浅い列に立つ」しか
ないので、素朴に組むと**被覆から出ることと前列に晒されることが同じ操作に潰れる**。
V4（V3 のムド↔ヴェル）は被覆ゼロのまま変換器だけを後列に戻すので、この2つを割れる。
割らずに V1〜V3 だけを読むと「肩代わり役が自傷軸を無効化している」という誤った一般則が出る
（実際の差は燃料 +12 ではなく敵からの被弾 +66）。結論は README「自傷の燃料は変換器まで届く」。

`swap` は**同じ席で駒を入れ替えて比べる**（第21期）。ナラの回復側をノノ（もう1体の回復役）と
突き合わせるために作った。**`ablate` を使わないのが要点**——駒を1体減らすので寄与に
「5体目の体そのもの」が必ず混ざる（第20期の +19.8pt がそれで、土台で同席のゴルムは -25.5pt だった）。
同じ席に別の駒を置いた版と比べれば、差がそのまま機構の差になる。

**各群に「4体（中央 空）」を必ず入れる。** これは5体目の体の値段であると同時に、
**その台が飽和していないかの検査**になる——4体版と5体版が同じ値なら、その台では
中央の駒が何であっても結果が変わらない（＝測定にならない）。第20期・第21期で
続けて台を床と天井に落としたので、**台を作ったら先にこの列を見る。**

`wave2` は**波の中身を解剖する**（第51期）。`spread` が波を1つの箱として扱うのに対し、
**敵を1体ずつ空席にし、盤面ルールを1つ切って `compare` を回し、差分を取る**。
`Stages` は書き換えず、波の複製を診断のローカルに組む（`ObRows()` / `SgRows()` と同型）。
**波は引数で選べる**ので5波すべてに同じ解剖を回せる。**盤面は1つも動かない。**

**主要な指標は平均ではなく「100.0% の行数」。** 実測で平均と `Δ100%` は順位が違う
——第二波で平均がいちばん上がるのは中央の伝令を抜いたとき（94.54）だが、
100.0% がいちばん増えるのは前列の騎士か狙撃手を抜いたとき（+18）。
**平均で読むと関門の順位を間違える。**

**規則は2通りで外す。** (a) 規則フラグ（`HushRule` / `YokeRule` の `Active`）と
(b) 保持者を**同数値の対照駒**に差し替える（粛↔討伐隊の新兵・渇き↔巡礼騎士・
軛↔城塞の重装兵・殉教↔戦斧兵はどれも数値・型・速さが1つも違わない）。
**両者が1セルも違わないことが診断の検算**——実測で52行完全一致。

**波ルールが特定の機構を狙い撃つと、その機構を持たない編成には1セルも効かない**（第51期）。
狙い撃ちの3本（粛・渇き・断罪）はどれも非対象の行が **±0.0 が全行**で、
粛では非対象34行の平均が **86.56 → 86.56** と小数点以下まで同値だった。
**粛と渇きの違いは性質ではなく対象の広さ**で、非対象は 粛 34/52（65%）に対し 渇き 17/52（33%）。
**軛と殉教は非対象が作れない**——単発 25 超を出さない行も、単体で殴らない行もロスターに無い。
**性質が違うのではなく課金対象が普遍的なだけ。** 対象行の Δ平均は
粛 +61.53 / 軛 +26.63 / 断罪 +22.75 / 渇き +15.80 / **殉教 +1.51** で、殉教だけ桁が違う。

**同じ窓口を通る特性でも代金は3倍違う。** 粛が止めるのは `CanActOutOfTurn` を通る4本だが、
外したときの伸びは **棘 +86.30 / 仇討ち +72.75 / 軋み +28.83 / 追い打ち +28.12**。
効いているのは窓口の位置ではなく**手番の持ち方**で、棘（カド）は `Immobile` なので
粛の下では振ることも反応することもできない完全な置物になる（カドを持つ10行中7行が 0.0%）。
**断罪の課金対象は粛より狭い**——`ctx.InReaction` の中でしか発火しないので
`ctx.Reaction` に包まれる2本（棘・仇討ち）だけで、軋み（`ctx.Interrupt`）と
追い打ち（`PerformAttack` の直呼び）は `CanActOutOfTurn` を通るのに断罪は踏まない。

**情報セルの少なさは「区別していない」ではない**（第51期・第44期の裏返し）。
第二波は情報セルが全波で最少（23）なのに、**固有の勝者14 / 固有の敗者7 はどちらも全波で最多**
（第三波 4/2・第四波 3/4・第五波 1/7）。**52行のうち21行が第二波でだけ天井か床に振れている。**
天井の行のうち固有の割合も 第二波 64% 対 他波 30〜33% で高い。

**第二波の5席は全部が関門**（第51期）。どれを抜いても 100.0% が +8 以上増える。
ただし性格は同じではない——**後1の施しの司祭長だけが 0.0% を1行も救わない**（7 → 7）ので、
**天井にだけ課金していて床には一切関与していない**。床の7行は粛が単独で作っている
（粛を切ると 7 → 0）。**前1と前3の巡礼騎士2枚は52行中49行で完全一致**する重複で、
盤面としてはほぼ同じ1枚。経緯は design/PHASE51_WAVE2.md。

**第一波は勝率の物差しの上では存在していない**（第51期）。全52行 100.0%（10,400戦で1敗も無い）
に加えて、**3体のうちどれを空席にしても52行すべて 100.0% のまま**（Δ0・最大差 0.0）。
戦力を 3/5 に落としても1セルも動かない。決着Tは 2.0〜6.1 と3倍の幅で散っているので、
**情報を取り出すなら波ではなく物差しを替える余地がある**（判断は保留）。

`pace` は**物差しの側**を比べる（第54期）。`spread` が「波が互いに違うか」を勝率の上で測るのに対し、
**同じ 56 行 × 5 波を、勝率以外の4つの物差し**（決着T・残存数・被ダメ総量・与ダメ総量）**で測り直す。**
**盤面は1つも動かさない**（`Stages` も `CompareBuilds()` も読むだけ・17.7〜19.5 秒）。**`docs/` には置かない。**

**「情報が無い」と「情報を読む物差しが無い」は別**（第54期）。第一波は勝率では 56 行すべてが
100.0%（情報セル 0・第51期は「勝率の物差しの上では存在していない」と書いた）だが、
**被ダメ総量では 48 群に割れる**——しかも第2〜5波の勝率との相関は |r| ≤ 0.24 で焼き直しではない。
**波の側ではなく物差しの側を疑う手が、第22期の `spread` 以来1つも無かった。**

**物差しは「幅」ではなく「群の数」で選ぶ。** 群の定義は
「A 帯（seed 0..199）で昇順に並べ、**B 帯（200..399）でも前半と後半が完全に分離する**切れ目の数 + 1」。
**幅は seed のばらつきを含むが、群は含まない。** 実測でも決着T（幅 3.0 倍・35群）より
被ダメ総量（幅 12 倍・48群）が良く、**指示書が主題に据えた決着Tは最良ではなかった**
（決着Tは第三波の勝率と −0.48 で歯止め 0.5 に近い＝回復役の有無が両方を動かしている疑い）。
**候補を1つに絞って測ると、それが最良かどうかは判定できない。**

**飽和した波ほど勝率の取りこぼしが大きい**（第54期）。「勝率では同値だが物差しでは分かれる行」は
**第2波 38 行（56行中 68%）→ 第5波 22 行（39%）**と、波の平均勝率（68.4 → 37.0）に沿って減る。
**天井は同値塊を作り、同値塊の中身は物差しでしか見えない。**
**ただし第2〜5波で使う前に第14期の同語反復の判定を引き直すこと**——単発戦では
「味方の全滅＝敗北」なので被ダメ総量は分子経路に片足がかかる（第一波だけはそこが切れている）。
経緯は design/PHASE54_PACE.md。

`burn` は**燃焼という通貨を解剖する**（第57期・調査）。`whet` が強化の「経路」を数えたのに対し、
こちらは**書き手1枚（火の粉のボルグ）・読み手1枚（熾火のホタ）しかない通貨**が
盤面で何に繋がっているかを測る。**盤面は1つも動かさない**——足したのは
**誰も読んで分岐しない計数 9 本**（`UnitTally.Burn*`）だけで、`compare` は 280 セル 0 件で一致する。
`burn phase0` は窓口の一覧と接続の地図（**戦闘0回**）、`burn alt` は帰属の符号の別 seed 帯での追試。

**「燃えている」という事実を読んで分岐する箇所は engine 1本と駒1本の合計2本しかなく、
他の9通貨との接続は双方向とも 0**（第57期・表E の 18 セル全部）。**engine の1本は
燃焼そのものの実装**（`TickStatuses` の刻み）なので、**実質の接続点はホタ1枚。**
`ApplyDamage` の 12 段はすべて「ダメージが来た」を読んでいて「燃えている」を読んでいない
（唯一 `source == null` を見る吐き戻しも、**毒の刻みと燃焼の刻みを区別していない**）。
**現状の燃焼は「1枚の駒だけが読める、遅くて時間制限のあるダメージ」である。**
**第58期に火選り（ヒヨ）が2枚目の読み手になり、18 セルのうち 2 つ**
（`燃焼 → 強化` / `燃焼 → 弱体`）**が埋まった**（逆向きと残る7通貨は 0 のまま）。
**第59期に `死 → 燃焼` が埋まった**（ゾトの破裂＝`OnDeath` が着火する）。

**非スタックの通貨では、毎ターン撒く供給者が自分で自分の供給を捨てる**（第57期）。
火の粉は**ボルグが殴るたび**隣接味方を再着火するが、`Ignite` は残ターンを 3 に**設定**するだけなので、
**火は一度も消えない**——`速攻` の第四波は決着 16.6T の長期戦なのに `着火(味)` はちょうど 2.00 で、
残り 14.11 はすべて `再着火`（＝捨て札）に落ちている。捨て率は全体 **35.5%**・第四波 49.5%・
最長の行で **65.8%** で、**支配している変数は「供給者が何ターン振り続けたか」1本。**

**供給待ちは構造的に存在しない**（第57期。H1＝供給不足説の否定）。ホタは
**全5波・1000戦で 100% 燃えた状態で振り**、初着火は例外なく第1ターン
——ボルグ（速8）がホタ（速7）より速く、火の粉は `OnAfterAttack` なので
**ホタが最初の手番を迎える時点で既に点いている。**
ホタは第2〜5波でほぼ必ず死ぬ（96.5〜100%）のに帰属は **+78〜+96pt** ある。

**AND ゲートは加法的に分解できない**（第57期）。`燃焼 (ボルグ×ホタ)` は
火の粉 +52.7 / 熾火 +52.3 / **軸まるごと +52.7** で、**交互作用 −52.3pt**（別 seed 帯で −52.5 と再現）。
どちらか一方を素体に落とすと 20% 台（＝ `100/0/0/0/0` の「台が死んでいる」の値）へ落ち、
**両方落としてもそれ以上は落ちない。2枚のどちらにも 52.7pt の全額を帰属させるしかない。**
しかも `ablate` は 5 枠すべてが **+37pt 以上**（いちばん安い駒でも読み手の 70%）で、
**この軸の唯一の台に空き枠は 0**（ただし床で飽和しているので値は下限）。

**隣接規則に乗った供給は、隣接が定数なので供給も定数になる**（第57期）。
火の粉は巻き込み（`Splash`）と同じ `FormationRules.AreAdjacent` に乗っているが、
`着火(味)` は 8 行すべてで **2.00 に張り付く**（＝隣接する味方の枚数そのもの）。
**2.00 からずれる原因は 3 つしかない**——セロの被弾後退（+1.2〜1.5）／棘守りの入れ替え（+0.02〜0.48）／
**第五波の曝き（告発人・全行で +0.7〜1.0）**。**つまり燃焼の供給量を動かしている唯一の量は「位置」で、
それを動かしているのはボルグ自身の席ではなく移動系の駒と敵の曝きのほう**
（ボルグの席は 前3/後1/後3 の3種に散っているのに 2.00 は1ビットも動かない。**次数は8行とも 2**）。

**火の粉の符号は編成で割れる**（第57期）。ホタを含まない7行の帰属は
**資産2（速攻 +7.0 / 範囲耐性 +1.7）・代金3（毒→被弾強化 −7.3 / 縛め非収入型 −5.4 / 鱗改 −4.8）・
ゼロ2**（別 seed 帯で **6/8 行が符号一致。反転した2行はどちらも |帰属| < 1.0**
——以後 **|帰属| < 1.5pt は 0 と読む**）。正に出るのは**決着が伸びて刻みが積み上がる中間帯の波**、
負に出るのは**すでに勝てている行の第4〜5波**（味方の燃焼が生存を削る）。
**着火の 52% は味方に付いていて**（味 2.40 対 敵 2.21・40 セル中 27 セルで味方の燃ダメが敵を上回る）、
**その味方側の燃焼を読む駒は1枚も無い。**

**波ルールは燃焼に1つも課金していない**（第57期・実装前の予測が当たった）。
粛（刻みは `CanActOutOfTurn` を通らない）・渇き（`ctx.Heal` を通らない）・軛（6 < Cap 25）・
殉教（`SelectTarget` を通らない）のどれも効かず、実測でも
**燃ダメ ÷ 決着T は4種類の波ルールをまたいで 6.16〜7.61（幅 ±11%）でほぼ一定。**
波ごとの燃焼量の差はまるごと決着ターンの差で説明できる。
**この期は設計を1つも決めていない**——候補5つとその実測の根拠は design/PHASE57_BURN.md §8。


`spread` は**波の側**を集計する（第22期）。既存モードは全部「編成の側」を見ている
（どの編成が強いか）ので、**5つ並べた波が互いに違うことを測っているか**を出す道具が無かった。
`CompareBuilds()` × `Stages` を `compare` と同じ seed 帯で回すだけなので、セルは
`docs/balance.md` と一致する。**盤面は1つも動かさない。**

出す表は3つ——(1) 波ごとの飽和（平均・100%/0% の編成数・中間帯・母標準偏差）、
(2) 波間の相関、(3) **固有の勝者・敗者**（その波でだけ 100%／その波でだけ 0% の編成）。
**(3) が波の個性の実体で、ここが空の波は独立した波として存在していない。**

**「情報セルが増える」は「波が分離した」ではない**（第44期）。誹りを第二波に置くと
情報セルは 23 → 25 に増えたが、剥がれた2行（`刻み×断ち` / `刻み×縫い`）は
**そのまま第二波の固有の勝者**で、12 → 10 に減っていた。**天井を一様に削る操作は必ずこの形になる**
——情報セル +2 と 固有の勝者 −2 は**同じ変化を逆符号で数えているだけ**。
**採否は情報セルではなく固有の勝者で読むこと**——情報セルは「その波が誰かを区別しているか」を
測るが、**誰を区別しているかは測っていない。**
中間帯は **5 < x < 95 の狭義**（5.0% ちょうどは床に張り付いている側）。

**(3) は第一波を比較から外して数える**（第22期 Phase 2b で直した）。第一波はチュートリアル波
として全編成 100% を意図的に維持しているので、比較に入れると**第2〜5波の固有の勝者が
恒等的に 0** になり、中間の波を何に作り替えても動かない指標になる。第一波自身も判定しない。
**波を触る前に、判定が到達可能かを確かめること。**

**第五波は第40期に固有の勝者を初めて持った**（曝き／`Accuser`）。厳密版 0 → 1
（`突き出し (セロ×ヨミ)`）、緩い 90/10 版 0 → 3——**移動の読み手を持つ3行がそのまま並ぶ**
（隊列崩し / 突き出し / 移動改）。第五波の平均は 53.8 → 40.5 で、**歯止め 40.0 の 0.5pt 上**。
第3波×第5波 の相関は +0.58 → +0.46（狙いどおり低下）だが、**第4波×第5波 は +0.30 → +0.44 と
上がった**——軛（1発の重さ）と曝き（列条件の破壊）がどちらも積み上げ系に課金しているため。

現状値は **61編成**（第92期。繕いの傷読みを採用した後）で
**100 / 69.9 / 74.8 / 61.8 / 39.6**（**主判定19行では 100 / 73.6 / 71.1 / 65.5 / 38.0**）
——**歯止め 33.2% との余裕は +4.8pt。**
**第85・88・90・91期はここを測り直していなかった**ので、下の第74期の値からの差には
巻き込み則・縫いの両側読み・滲み則の採用ぶんが全部入っている（第92期に測り直した）。
その1つ前の記録が **61編成**（第74期。断ちの待ち方を V1 にした後）で
100 / 67.9 / 74.3 / 58.8 / **37.9**（**主判定19行では 100 / 70.4 / 71.1 / 64.7 / 38.2**）
——**動いたのは `刻み×断ち (ノミ×ナタ)` の 3 セルだけ**で、
**主判定の第五波は 38.2% のまま 1 ビットも動かない**（19行にナタを含む行が 0 行）。
**歯止め 33.2% との余裕は +5.0pt で変わらず。**
その1つ前が **61編成**（第60期。火選りを手番へ降ろした後）で
100 / 67.9 / 74.2 / 58.0 / **37.6**——**波間相関の6ペアは第59期の61行と 0.02 以内**で、
固有の勝者・敗者の分類も1件も動いていない（動いたのは火選り4行のセルだけ）。
**第60期からは第五波の判定を全行平均でやらない**（→ 下の「歯止め」）。
その1つ前が **61編成**（第59期。破裂の着火を採用し試験行2本を足した後）で
100 / 67.9 / 74.4 / 58.8 / **37.4**——**59行では 100 / 66.8 / 73.6 / 57.5 / 35.9**
（採用前の同じ59行は 100 / 66.9 / 73.6 / 57.0 / 35.9）で、波間相関の6ペアは
59行と61行で**すべて 0.04 以内**。**新2行は第五波の平均を +1.5pt 押し上げた**
——**第41期以降で行を足して第五波が上がったのは初めて。**
**ただし第59期は盤面ルールの採用期なので、既存7行のセルが動いている**
（固有の勝者は第2波 15 → 13、固有の敗者は第4波で1件入れ替え。情報セルの合計は 158 → 158 で不変）。
その1つ前が **59編成**（第58期。焚き付け3行を足した後）で 100 / 66.9 / 73.6 / 57.0 / **35.9**
——**56行では 100 / 68.4 / 73.6 / 57.0 / 37.0**（＝第53期の値と完全一致）で、波間相関の6ペアは
56行と59行で**すべて 0.04 以内**。固有の勝者・敗者も**既存行の分類は1件も動かない**
（第二波の固有の敗者に `火選り無風型 (ヒヨ×カド)`＝当時の `焚き付け無風型` が加わって 7 → 8 だけ）。
その1つ前が **56編成**（第53期。止め2行を足した後）で 100 / 68.4 / 73.6 / 57.0 / **37.0**
——**54行では 100 / 67.5 / 74.4 / 57.5 / 38.3** で、波間相関の6ペアは 54行と56行で**すべて 0.02 以内**。
固有の勝者・敗者も**既存行の分類は1件も動かない**（第五波の固有の敗者に `止め (トメ×ソラ)` が加わって 8 → 9 だけ）。
その1つ前が **54編成**（第52期。駆り立て2行を足した後）で 100 / 67.5 / 74.4 / 57.5 / **38.3**
——**52行では 100 / 67.5 / 74.3 / 58.0 / 38.4** で、波間相関の6ペアは 52行と54行で**すべて 0.04 以内**。
その1つ前が **50編成**（第47期。鱗2行を足した後）で 100 / 68.2 / 73.7 / 56.7 / **38.6**
——**48行では 100 / 66.9 / 72.8 / 55.7 / 37.8** で、波間相関の6ペアは 48行と50行で**すべて 0.03 以内**。
その1つ前が README「弱体化に戦闘中の供給を作った —— 突き返しのハネ」の `spread` の表
（第41期・**45編成**。同じ波を 44 行と 45 行の両方で測り直してある）。
その1つ前が「肩代わりに出口を付けた —— 腹・還し（採用）と、まどろみ（棄却）」（第36期・42編成）、
その前が「波に『ターン外の行動禁止』を置いたら、読み手2体が割れた」（第27期・粛の後・38編成）、
軛の節はその1つ前、渇きの節はさらに前、「波の分離度を測る物差しを作った」の表は**いちばん前の値**。
**編成の行数が世代で違う**（38 → 42 → 45 → 48 → 50 → 52 → 54 → 56 → 59 → **61**）ので、波を作り直すときは
**同じ行数で前後を測り直して**採否を決める。**計測器と測定対象を同時に動かさない。**
第41期は行を1本足しただけで**第五波の平均が 40.5 → 39.6 と歯止め 40.0 を割った**が、
**同じ第五波を 44 行で測れば 40.5 のまま**——下がったのは新行が第五波 0.0% だからで、
波は1つも動いていない。**行を足したときの平均の低下を波の難度と読み違えないこと。**

**難度の歯止めを先に決めること。** 「中間帯が増える」だけを見ると第四波の Cap は 20 が
最良に見えるが、それは 12 編成を 0% に締め出した結果の中間帯で、第五波より難しい波になる。
第25期の後の並びは 100 / 85.8 / 72.5 / 61.8 / 59.8 と**単調に落ちていた**が、
**第27期の粛でこれが崩れた**——現状（第60期・61編成）は 100 / 67.9 / 74.2 / 58.0 / 37.6 で、
**2番目の波が3番目より難しい**。

**第五波の歯止めは第60期に分母を主判定の固定行集合へ移して確定した**（第59期 9-4 の移行案への回答）。
**旧「全行平均 ≧ 40.0%」は行が増えるたびに勝手に動く量だった**——波そのものは第40期の
40.5（44編成）から1つも動いていないのに、全行平均は
第41期 39.6/45行 → 第47期 37.8/48行 → 38.6/50行 → 第53期 37.0/56行 → 第58期 35.9/59行
→ 第59期 37.4/61行 → 第60期 37.6/61行 と組成だけで上下していた。

    第五波: 主判定 38.2% / 全61行 37.6%     ← 記録の形（規約。両方を必ず併記する）
    歯止め: 主判定 33.2%                     ← 確定時（第60期）の主判定の値 − 5.0pt

**主判定は `Baseline.PrimaryRows`（BattleSim/Program.cs）に 19 行がコードとして固定してある。**
第31期の16行 + `突き出し` から、第60期に `裂き (キリ×エグ)` → `裂き×責め苦` へ差し替え、
`止め改 (トメ×薙ぎ)`（標の**敵側**の読み手）と `引き受け (ウケ×ドハ)`（`ctx.Dull` を通る行）を足した。
**`死軸×ホタ (ゾト×熾)` は保留**（第59期に作られたばかりで席も「測れる席」を消極的に選んだ状態）。
表は `spread` の §4 が毎回出す（行名が引けなければ警告が出る）。

**40.0 を据え置かなかった。** あれは 42行時代の全行平均から来た数字で、第59期の提案20行が
ちょうど 40.0 で**線上に乗った**のは偶然。**歯止めは「明らかに成立しなくなる線」であって
調整目標ではないので、線上に置くのが一番まずい。**
旧集合の値の併記（切り替わった場所が後から読めるように）: **主判定17行 41.9 / 全61行 37.4**（第59期）。
**主判定17行・19行の値は第60期の移設で1ビットも動いていない**（19行のどれもヒヨを含まないため）
——固定行集合が分母として意図どおり働いていることの実証。

**情報セルの定義が2つある。** `spread` §1 の中間帯は **`5 < x < 95` を全5波**、
§4（主判定）の情報セルは **`0 < x < 100` を第2〜5波**（第59期 9-1 に揃えた）。**別の量なので混ぜない。**
粛には軛の Cap に当たる連続量のノブが無いので**係数では緩められない**。
波を触るときは、単調性をどこまで保つかを先に決めること。

`gullet` は**巨躯の「吐き戻し」**を測る（第23期）。肩代わり4種のうち見返りを持たないのは
巨躯だけで、90% を後方全員から引き受けてそこで価値が消えていた。**見返りはゴルム自身ではなく
守った相手に返す**（`ApplyDamage` の巨躯の分岐）。版の切り替えは `ColossusRule` を
`BattleEngine.Run` に**引数で渡す**——**書き換え可能な static のノブは置かないこと。**
Trait は共有シングルトンで `layout` は並列実行するので、static だと版が他のスレッドへ漏れるし、
`Run` の「副作用も外部依存もない」もそこで壊れる。

引数無しが4版の対照（V0 現行 / V1 吐き戻し / V2 60% / V3 60%+返し）で、**V0 が
`docs/balance.md` と一致することが診断そのものの検算**。`gain` は返す効率（`DamagePerGain`）を
2/4/6/8 で振る。`log` は1戦の監査（受け手の内訳・ウツの攻撃力の推移・毒の刻みでの非発火）。

**この診断だけはログの文字列を数えている。** UI は `LogKind` を見るという規約に反して見えるが、
確かめたいのは「その行が出たか／出なかったか」そのもので、**発火しなかったことは盤面の値に
痕跡を残さない**（毒の刻みで返していないことは、立ちはだかりの回数との差でしか読めない）。

結果は README「肩代わりに見返りを付けた —— 巨躯の『吐き戻し』」。**主判定（ムドの与ダメ）は
未達で、`route` の未解決は持ち越し**——経路は通ったが、出力の総量に変換される前に戦闘が終わる。

`gullet belly` / `gullet belly4` は**腹**という通貨を測る（第36期）。腹は巨躯が肩代わりで
飲み込んだ量の残高（`ColossusTrait.BellyKey`。計上は `ApplyDamage` の巨躯の分岐で、
**吐き戻しが `blocked` を使う場所と同じ1箇所**）で、出口が2つある。

- **還し（採用済み）**: 倒れたとき腹の `RefundPercent`(25)% を生存味方へ `ctx.Heal` で分配。
  **1戦1回**（`RefundSpentKey`。腹を空にするだけでは蘇生で再発火するので印を別に立てる）
- **まどろみ（棄却・対照として残置）**: 腹が `SlumberThreshold`(60) に達した手番を失う
  → `IdleTurn` → 号令・据えが買う

`ColossusRule.Default` が **`Refund: true` / `Slumber: false`** ＝ 本採用の形。
まどろみは逆位（不採用の盤面ルール）と同じ扱いで**削除せずに残してある**。
規則は `BattleEngine.Run` に引数で渡す（static のノブは置かない）。

`belly` は Phase 0 の実測（盤面を1つも動かさない純粋な記録。N と率をここから引いた）。
`belly4` が4版の対照で、**V3 が `docs/balance.md` と一致し、ゴルムを含まない25行が
全版 ±0.0 であること**が検算（採用で既定が動いたので、**検算の相手は V0 ではなく V3**）。

**まどろみは engine 側で `IdleTurn` を立てる（`CanAct` を偽にしない）。** `CanAct` で書くと
`Trait.SurrenderedTurn` が偽になり、号令・据えが買わなくなる（不動のカド・追い打ちのハギと
同じ扱いに落ちる）。手番だけを失い、肩代わり・吐き戻し・大喰らいの吸いは止まらない
（`OnTurnStart` は行動順ループの外側）。

**還しが `ctx.Heal` を通るのは意図**——第三波（渇き）で封じられることでロスターの回復供給に
課税対象が1つ増える。**ガルドの `Stoic` も弾く**ので、ガルド行では頭数で割られた取り分が
そのまま消える（`SupportTargets` を通す吐き戻しとはここが違う）。**ウツ（逆しま）には無風**
——回復は `AtkBonus` を触らないので、第23期の吐き戻しが逆しま3行を落としたのとは逆になる。

**まどろみは測って採用しなかった。** 発火はするが売れない。理由は3段で、どれも係数では直せない:
買い手が2枚しかなく**据えは構造的に買えない**（ゴルムが速さ3＝ほぼ最後に動くので
`IdleTurn >= Turn` の窓が実質閉じている。まどろみを 0.20回/戦 足しても据えの買い取りは
1回も増えなかった）／号令は**買い手がゴルムの被覆の中にいる配置**を要求する
／売れても払い先が `SupportTargets(ゴルム)` ＝ゴルム自身で、**攻撃10の壁に +8**。
経緯と判定は README「安い手番は、売っても安い」と design/PHASE36_GOLM_BELLY.md。

**「毎ターン」→「〜したとき」の変換は供給側の作法にすぎない。** 供給が出来事になっても、
**その手番がもともと安ければ売った代金も安い。** `IdleTurn` の再販が成立するには
売り手の手番に失って惜しい価値があることと、**払い先が売り手以外であること**が要る。

**`ablate` は静的な入れ得を弾くが、「買い手が機構の働く時刻まで生きているか」は弾かない。**
第36期は試験台を一度組み直した（ガンを被覆の外に置いた版では号令の払いが 0.09回/戦しか出ず、
売却を1件も観測できなかった。被覆の中へ移して 1.78回/戦・換金率67%）。
**測る対象が勝率ではなく機構の発火であるときは、台の検査項目が第21期の飽和検査に加えて
もう1つ要る**——それは `ablate` ではなく `reseat`（配置）でしか見えない。

`yield` は**攻撃力1点が誰の手なら出力になるか**を測る（第24期）。駒1体の `AtkBonus` に
開戦時から +10 を注入し、味方全体の与ダメの増分 ÷ 注入量（`出力/点`）を出す。
「燃料 → 攻撃力 → 出力」の**前半を切り離す**道具——ムドの `Rage` が動いていることは
第23期に実測済みなので、ここで低ければ壊れているのは後半だと特定できる。
エンジンは触らない（`Materialize` → `AtkBonus` → `UnitState` 版 `Run` の3つで足りる）。

**検算は2本。** 注入なしが `Formation` 版（compare の経路）と**1試行ずつ**一致すること
（勝敗・ターン数の両方）と、**ノノの `出力/点` が 0.00** であること（`Actions = [Skill]` で
攻撃を振らないので、0 でなければ集計が間違っている）。

**天井・床のセルでは誰に注入しても 0 に潰れる。** 削り切っている波では増えた火力が
与ダメ総量ではなく決着ターンの短縮になって出るので、`出力/点` はそこでは駒ではなく台を
測っている（第21期 swap の「台が飽和していないかの検査」と同じ穴）。**順位表は2つ出す**
——`全波`（計画どおりの主指標）と `中間帯`（注入なしの勝率が 5% < x < 95% の波だけ）。
実測で 35 編成中 11 編成が中間帯を1つも持たず、行の `出力/点(全波)` は
**編成が持つ中間帯の波の数**とだけで ρ = 0.47 相関する。

結果は README「攻撃力1点は、誰の手なら出力になるか」。**§4 の仮説（`出力/点` ≒ 対象数 ×
発射回数 × 生存T）は回数の側が外れる**（`干渉/戦` との ρ は中間帯でも 0.52）が、
**`本人/点` とは 0.891 相関する**——効くのは「注入が本人の打点になるか」で、
攻撃型と手番の持ち方から机上で監査することはできない。

**第19・20・23期の「燃料が出力にならない」はこの診断で訂正された**——3期とも中間帯を
持たない編成（置き去り×被弾強化）で判定していた。ムドは中間帯なら34体中10位で、
`本人/点` との差も +0.23。**壊れていたのは変換器ではなく測定台。**

**`型(素)` 列は `Def.Pattern`。** 戦闘中の型は特性が書き換える——熾のホタ（`Pyre`）は
燃えている間だけ攻撃力4倍＋貫きで、**ロスター唯一の乗算フラグ**。強化を配る設計を触るときは、
受け手にホタがいるかを先に見ること（配った強化がそこだけ4倍になる）。

`yoke` は**第四波の軛**（1回のダメージの上限）を測る（第25期）。`gullet` と同じく規則を
`BattleEngine.Run` に**引数で渡す**（`YokeRule`。static のノブは置かない）。引数無しが5版の対照で、
**V0（中央 城塞の重装兵）が差し替え前の `docs/balance.md` と一致し、V1（軛の重装兵・規則を無効）が
V0 と1セルも違わないこと**が診断そのものの検算になる——保持者は重装兵と数値が同一なので、
規則を切れば盤面は完全に同じに戻る（**逆位はここを分けなかったせいで切り分けに追加測定が要った**）。

`sweep` は Cap を 12〜50 で振る。**上限の帯は測って決めること**——計画の 15 も手当の 20 も
「波を分離する」ではなく「波を壁にする」（0% の編成が 16 / 12）。採用値 25 の根拠は
README「波に『1発の上限』を置いたら、第四波が課税する資源を変えた」。

`log` は1戦の監査。**ここもログの文字列を数えている**（`gullet log` と同じ理由）。
**保持者の生死はターンではなくイベントの並びで割ること**——ターンで割ると、保持者が倒れた
同じターンの後続のダメージが「上限を超えた」と誤検出される（実際に踏んだ）。

`guard` は**第五波の殉教者（庇う持ち）の HP だけ**を振って、介入の試験が立つ体を探す
（第34期）。`Stages` は書き換えず変種を診断のローカルに組む（`gradient` / `aim` と同じ）。
**各HP点に「庇うだけを外した同数値の対照」が入っているのが要。** HP を上げると介入の窓が
伸びると同時に**波の総HPも増える**（355 → 448 で +26%）ので、対照を置かないと
用量反応が「介入が効いた」なのか「ただ硬くなった」なのかが決まらない
——**実測では 82% が後者**（介入の効果は全HP帯で −1.8〜−4.7 しかない）。
**HP52 の対照が第32期差し戻し後の `docs/balance.md` と一致することが診断の検算。**

`guard percent` は**介入の密度**（`MartyrRule.RedirectPercent`）を 50/75/100 で振る（第35期）。
**HP を動かさないので波の総HPは1も動かず、対照は「庇うなし・HP52」1本で足りる**
——第34期の交絡は HP が原因だった。**p75 を採用**（`MartyrTrait.DefaultPercent`）。
p50 では庇うが作った固有の敗者が 0 行で対照と区別が付かず、p75 で 裂き が 11.5 → 9.5 と
閾値10を割って**初めて介入に帰属できる行が出た**。p100 でも敗者は同じ1行きり。

**窓を持つ機構は、窓を閉じる条件が2つ以上あると片方だけ伸ばしても伸びない。**
庇いの窓は「庇い手が落ちる」と「守る相手が落ちる」の両方で閉じる（`f != target` を
満たせなくなる）。体を厚くして伸びるのは前者だけで、実測では後者が律速だった
——生存T 2.1倍に対して発火は 1.5倍で頭打ち。密度でも同じで、p を2倍にしても
**勇者候補の生存Tは 3.02 → 3.09 しか動かない**（体でも密度でも律速は動かせなかった）。
経緯は design/PHASE34_GUARDIAN_HP.md と design/PHASE35_MARTYR_PERCENT.md。

**共有する定数は、片方だけ振りたくなった時点で分ける。** `GuardianTrait.RedirectPercent` は
味方ガルドと共有だったので、そのまま振ると28行が一緒に動いて交絡が戻った。
`TraitId.Martyr` を分けたうえで**挙動は `RedirectGainTrait`（共通の基底）に寄せてある**
——`GuardianTrait` に残ったのは `RedirectPercent` と `Id` の2行だけ。
**介入の鎖に段を足しても乱数はずれない**（`PickOne` は候補0個・1個で `Roll` を消費しない）ので、
段を足す変更の受け入れ条件は「`compare` が1バイトも動かない」でよい。

`sever` は**断ち（ナタ）の発火と手番の放棄**を数える（第37期）。**ログの文字列を数えている**
（`gullet log` / `yoke log` / `hush` と同じ理由）——断ちの上乗せは与ダメの総量に溶けるし、
**振らなかったことは盤面の値に痕跡を1つも残さない**。`sever sale` は捨てた手番が
号令・据えに売れないこと（`SeverTrait.SurrendersTurn => false`）を、**買い手を揃えた
ローカルの台**で確かめる——採用した台には号令も据えも入っていないので、そこでは試されない
（第36期のまどろみと同じ穴）。**陽性対照（のろまのドルガ）を同じ台に同席させること**
——「0 件でした」は「効いている」と「台が壊れている」の区別が付かない。

**溜める機構は、溜まる時間を作らないと溜まらない。** 断ちは「溜めた傷を全部使って一撃」の
つもりで作ったが、第37期は速さ要件（書き手より遅い）と `Wounds = 1`（1ターンに傷1つ）が
同居した結果、**`傷/断ち` が 1.00 に張り付いた**。`SetCounter(Wound, 0)` を書けば
消費型になるわけではない——**消費型かどうかは供給と消費の周期差で決まる。**

**原因は速さではなかった**（第38期）。**維持読みは順序で立ち、消費読みは周期差で立つ**
——全消費の読み手が毎ターン振る限り、在庫の天井は1ターンの供給量で、速さは
「誰が先に読むか」を決めるだけで流量は変えない。系: **介入の税額は1振りに載せた在庫に
比例する**（在庫1なら殉教者の税も ±0）。ただし**比例の相手（振る回数）がゼロに近づくと
税もゼロに近づく**——第五波は待ち続けて終わる波になった（`待ち` 0.96 > `放棄` 0.60 で
発火 0.04回/戦）。

**周期は「時計」ではなく「盤面の出来事の閾値」で作る**（`SeverTrait.Threshold = 2`）。
`Actions` の `Charge` 化は採らなかった——第36期のゴルムの腹が時計型の蓄積を外したのと
同じ理由（絶対時刻の周期は蓄積を出来事から切り離す）で、加えて `Charge` の枠組みは
増幅（`[Charge, Attack(200)]` の 200%）を内蔵している。**閾値待ちは在庫の天井を閾値に
固定する**ので `傷/断ち` は閾値ちょうどになり分散を持たない（実測 10セル全部で 2.00）。
**消費型が供給源から資源を奪うのも「消費」ではなく「毎ターン振ること」の帰結**で、
閾値を入れるとノミの「なぞり」は 0.12〜0.28 → 0.98〜1.44 回/戦に戻る。
経緯は design/PHASE37_SEVER.md と design/PHASE38_SEVER_CADENCE.md。

**第74期に「待ち方」だけを差し替えた**（`SeverRule.Default` = `SeverWait.Swing`）。
**閾値（2）も `傷/断ち` = 2.00 も1ビットも変えていない**——変えたのは
**閾値に届かない間、手番ごと捨てていたのをやめて普通に殴るようにした**ことだけ。
上の第38期の「第五波は待ち続けて終わる波になった」は**その時点の記録**で、現行では成り立たない。
**閾値を下げる版（V2）は逆効果**——断ちの発火は3倍になるが `傷/断ち` が 1.02 に落ちて
「畳んで一撃」が壊れ、しかも `放棄` は消えないので傾きは +1.46 しか戻らない
（V1 は +5.15）。**閾値が消せるのは `待ち` だけで、`放棄` は消せない。**

`expose` は**引きずり出し（曝き）**を測る（第40期）。**ロスターで初めて「敵から味方へ
状態を書く」経路**——それまで敵側から味方へ届くのは断罪1本だけで、味方側に読み手
（被弾強化・逆しま・澱み喰い・軋み・責め苦・移り木）が揃っているのに供給源が無かった。
規則は `BattleEngine.Run` に**引数で渡す**（`ExposeRule`。static のノブは置かない）。
`MaxPerBattle` の**採用値は 3**で、0 なら走査ごと走らない。
**告発人（`Accuser`）は巡礼騎士（`Knight2`）と数値・型・速さが1つも違わない**ので、
規則を有効にしたまま `Knight2` に戻せば盤面が完全に元へ戻る——これが診断の検算
（実測 44行 0 件）。`ExposeRule(0)` で `compare` が差し替え前と 220 セル 0 件で一致することも別に確かめる。

**入れ替えは、押し下げた駒を次の引き出し候補にする。** 曝きは「後列で最もHPが高い駒」を
「前列で最もHPが低い枠」へ入れ替えるが、**前列が後列より硬い編成では押し下げた駒が
次の最高HP後列駒になり、規則が自己反転して振り子になる**（`逆しま` で ウツ 245 回に対し
**ガルド 240 回**——庇う手は死なず往復するだけ）。**初手の1回だけをモデル化して予測すると外す。**

**窓の条件が2つある機構は、片方を配る規則が同時にもう片方を奪う。** 後衛特化は
「`HasFallenBack` **かつ** `Row.Back`」で、曝きは前者を無償で立てるが後者を壊す
——**持っていない編成に配り、持っている編成から取り上げる**（セロを含む11行のうち
初期席が前列・中央の4行は全部増え、既に自力で後退していた7行は6行で減った）。
供給（`後退` 0.26 → 2.15 回/戦）は増えているので、律速は供給ではなく席。

`shove` は**突き返し（ハネ）**を測る（第41期）。**弱体化（`AtkBonus` を負にすること）に
初めて戦闘中の供給を作った**——それまでの供給は 呪詛の味方漏れ／萎縮（どちらも開戦時1回）と
分かちの「腕がなまる」の3つで、**分かちは代表編成でウツと同席していない**。読み手は逆しま
1枚きりで係数3倍なのに、表の上ではウツの攻撃力が定数だった。規則は `BattleEngine.Run` に
**引数で渡す**（`ShoveRule(Penalty)`。採用値 2。static のノブは置かない）。
**既定を無効にしなくてよい**——味方側の駒なので、ハネを編成に入れない限り既存 44 行は
1バイトも動かない（それ自体が回帰チェック）。

**`ShoveRule(0)` は効果Bだけを止めて効果Aは走らせる。** これが陽性対照で、
`ablate` が使えないこの台（4体版が 100/0/0/0/0 の床に落ちる）で符号を読む唯一の窓口。
**符号を測りたい効果は、その効果だけを 0 にできるノブと対にして作ること**——
1つの駒が「読み手に依存しない効果A」と「編成で符号が変わる効果B」を両方持つと、
`ablate` も `swap` も駒を単位に測るので**2つが合算されて符号が消える**。

**使われるのは「先に来る供給源」だけ。** 突き返しは1ターン1回で切ってあり、喧噪（バサ）は
`OnTurnStart` で毎ターン必ず先着するので、第五波で曝きが移動を上乗せしても
**発火は増えず空振りだけが増える**（供給元の帰属は 喧噪 11.58 / 曝き 0.12 回/戦）。
順序を決めているのは**量ではなくフックの位置**（`OnTurnStart` か `OnAfterAttack` か）。

**`reseat` が見つけた「無料の席」は、席を混ぜる駒が同席すると開幕の数ターンぶんしか効かない。**
効果Bは隣接**全員**に効くので隣接次数がそのまま値段になる（角2・中央4）はずが、
実測の比は **1.41 倍**にしかならない——喧噪が毎ターン席を混ぜて盤面の平均次数 2.4 へ回帰する。
**隣接次数は席の性質だが、席は定数ではない。**

`demo` に編成名（部分一致）を渡すと `CompareBuilds()` の編成をそのまま1戦流す（第26期に追加）。
**新しい特性が発火しているかは勝率では読めない**——勝率は「発火したが足りなかった」と
「一度も発火しなかった」を区別しないし、「狙いどおりの相手に付いた」と「別の駒に付いた」も
区別しない（ヒサの標的が計画の席でノノに付いていたのはログでしか見えなかった）。

`replay` は戦闘1戦を「台本」（初期盤面＋時間順のイベント列）として JSON で吐く。
勝率・連鎖深度が数字で答えてくれない「畳みかけて見えるか」を目で確かめるための道具で、
出力は repo に置かない（盤面を触るたび腐るし diff が読めない）。使うときにその場で吐く。

**長時間ジョブは前景で待ち切ること。** 背景に回すと、起動したコマンドが返った時点で刈られる。
`nohup` を付けても、同じターンの中で次のコマンドに移っただけでも死ぬ。
`layout` のように分割できないものは、一回の呼び出しで走り切れるかを先に確かめる。

BattleCore + BattleSim は Windows 以外でも動く（`dotnet run --project BattleSim` はどの OS でも通る）。

### バランス調整のたびにやること（CONTRIBUTING.md より）

1. 数値や特性を変える
2. `... 0 compare > docs/balance.md` で勝率を測り直す（飛ばすと勝率表が嘘になる）
3. `... 0 dump > docs/units.md` で一覧を吐き直す（飛ばすと説明文と挙動がずれる。過去3回発生）
4. `git diff docs/` で何が動いたかを確認する
5. docs/ の差分も含めてコミットし、動いた行をコミットメッセージにも書く

`docs/` の2ファイルは BattleSim の出力そのもの。**手で編集しない**（次の生成で消える）。
差分が出ないこと自体が「触ったがバランスは動いていない」という情報になるので、
変えていないと思っても必ず測り直す。

## 構成と絶対のルール

    BattleCore/     戦闘ロジック。net8.0 素のクラスライブラリ。UI を一切参照しない
    BattleSim/      コンソール総当たりシミュレータ（テスト代わり）
    PrototypeApp/   WPF (net8.0-windows)。編成を組んで結果を眺めるだけ
    GodotApp/       Godot 4 (C#) の戦闘再生装置。sln には入っておらず単独ビルド。
                    会戦の台本（Events / Openings）を再生するだけで、判定は一切しない
    docs/           BattleSim が吐く生成物 10ファイル（balance / units / chain / ablation /
                    pulse / engage / layout / reseat ／ **crossing** ／ **rules**）。手で編集しない。整合は `audit` で見る
                    ——**`crossing.md` だけは `CrossBuilds()`（交差帯）の生成物で、`audit` は見ない**
                    （`audit` は `CompareBuilds()` の行名で照合する道具なので、別の行集合には当たらない）。
                    **`rules.md` はノブの一覧**（第94期・`derive rules`）で、**編成数に依存しない**ので `audit` も見ない
    design/         設計文書（コンセプトメモ・会戦計画・指示書・測定報告）。手で編集する

**`docs/` は生成物のみ・手書き文書は `design/`。** 測定報告や指示書を `docs/` に置かない
（生成物と手書きが混ざると「手で編集しない」が守れなくなる）。第31期の報告は一度
`docs/` に置いて第32期に `design/` へ移した。

- **BattleCore に UI の参照を足さない**。`INotifyPropertyChanged` も `ObservableCollection` も不可。本番を Godot / Unity にする場合にそのまま持っていくため。
- **PrototypeApp に戦闘ルールを書かない**。ViewModel やコードビハインドにダメージ計算が漏れた瞬間に移植できなくなる。
- **`Def.Pattern` を直接読まない**。必ず `UnitState.CurrentPattern` を経由する（特性が状況でパターンを書き換えるため）。

## アーキテクチャ

### 特性 = イベントハンドラ（Traits.cs）

特性はすべて `Trait` を継承した「戦闘イベントへの反応」。`OnBattleStart` / `OnTurnStart` / `OnDamaged` / `OnDeath` / `OnMoved` などの virtual フックを上書きする。イベント駆動にしてあるので、意図していない組み合わせでも勝手に噛み合う。それが狙い。

- 追加手順: `Trait` 継承クラスを書く → `TraitId` に列挙子を足す → `TraitCatalog` の配列に登録する。
- **盤面ルール（`Inversion` / `Drought` / `Yoke` / `Hush`）だけは例外で、判定が engine 側にある。** どれも
  全員に一度にかかる盤面の状態で、駒ごとのフックでは表現できない（`ApplyDamage` が肩代わりを
  解決するのと同じ理由）。Trait 本体はログを出すだけで、**保持者がいなければ完全に不活性**
  （`compare` 差分ゼロで確認してから盤面に載せる）。
  逆位は `BattleEngine.Run` の `order` を組む直前1箇所で速さの向きを反転させる。測って
  **採用しなかった**ので `Stages` には載っていない——経緯は README「盤面ルール（逆位）は
  実在したが、波を分離しなかった」。
  渇きは `BattleContext.Heal` の入口1箇所で回復を止める。**第三波の中央に採用済み**
  （渇きの祭司。巡礼騎士と数値は同一）——経緯は README「波に『回復禁止』を置いたら、
  第三波が初めて分離した」。
  軛は `ApplyDamage` の**HP を引く直前**1箇所で1回のダメージを `YokeTrait.Cap`（25）で切る。
  **第四波の中央に採用済み**（軛の重装兵。城塞の重装兵と数値は同一）——経緯は README
  「波に『1発の上限』を置いたら、第四波が課税する資源を変えた」。
  粛は `CanActOutOfTurn` の**最後**1箇所でターン外の行動を止める。**第二波の中央に採用済み**
  （粛の伝令。討伐隊の新兵と数値は同一）——経緯は README「波に『ターン外の行動禁止』を
  置いたら、読み手2体が割れた」。
  **止まるのはこの窓口を通る4本だけ**（棘・仇討ち・軋み・追い打ち）。**肩代わり
  （庇う・分かち・巨躯・後備え・棘守り）はダメージの再分配であって行動ではない**ので通らず、
  責め苦（`OnAfterAttack`）も自分の手番の中なので通らない。**この2つを「反応する駒」と
  ひとくくりにすると設計が壊れる。**
  保持者の走査は**既存条件の後ろに置く**（`&&` の短絡。layout は数百万戦を並列で回す）。
  **入口ではなく出口で切る**のが要で、入口だと惨禍（+50%）や脆弱が上限を押し戻して
  「1発は Cap を超えない」が守られない。破片（`Armor`）は上限より前に引かれる別資源なので
  **上限の外側**で効き、肩代わりで分割された各段は別の `ApplyDamage` 呼び出しなので
  **段ごとに独立して切られる**（＝分割は上限を回避する経路。意図した帰結）。
- **「1発の重さ」に課金すると、課金されるのは「1発を育てる機構」で、大打点の駒ではない。**
  軛の予測は打点の大きさ（ドルガ38・カドの反撃）から立てて外した。実際に落ちたのは
  墓守の層（攻撃 151 → 25）と毒の刻み（52 → 25）で、**定数の大打点は引き算にしかならず、
  積み上げ系には上限が天井として効く**。反撃軸は決着が伸びるぶん振る回数が増えて逆に得をする。
- **盤面ルールを足すときは、そのルールが触るメソッドの呼び出し元を全部数える。**
  渇きは「回復役はノノ・ベニ・シオ・ナラの4種」という数え方で予測を書いて外した。
  実際に `ctx.Heal` を呼ぶのは**9経路**（第22期は7、第36期の還しで8、第39期の縫いで9）で、
  **ゴルム（吸い・還し・大喰らい）とリィカ（墓守）は説明文のどこにも回復と書いていないのに
  回復する。** 駒の説明文から数えると必ず抜ける。**第39期はこれを指示書の側で踏んだ**
  ——「回復を持たない対照」に指定された `刻み×抉り` が土台のゴルムのぶん +11.0pt 動き、
  対照を組み直す（ゴルム→ガルド）まで帰属が成立しなかった。
- **「最も傷ついた味方」の選択は `BattleContext.MostHurtAlly` の1箇所**（第39期に抽出）。
  継ぎ当て・施し・縫いの3者が同じ選択を持つ。**止まる条件は呼び出し側に残す**
  （継ぎ当ての `Hp <= 1` は自消費のための条件で、選択の一部ではない）。
- **Trait インスタンスは全ユニットで共有されるシングルトン**。インスタンスフィールドで状態を持ってはいけない。ユニットごとの状態は `UnitState.Counters`（文字列キーの int カウンタ）に置く。
- 調整用の数値は各 Trait の `public const` に置く（`BattleEngine` 側からも参照される）。

### BattleContext = 盤面への唯一の窓口（BattleEngine.cs）

- `ApplyDamage` がダメージ処理の単一窓口。敵の攻撃も味方の巻き込みも生贄もここを通るので、「被弾で強くなる」駒がどれにも等しく反応する。味方全体に効く効果（惨禍・据え・散開・萎縮・分かち）は駒の特性側ではなく `ApplyDamage` の中で解決する。
- 死亡通知の順序は固定: killer の `OnKill` → 本人の `OnDeath`（分裂など）→ 全員の `OnAnyDeath`（墓守）→ 味方の `OnAllyDeath`（蘇生）。「墓守が強化を得た後に蘇生が走る」という順序依存がある。
- 反撃は `ctx.Reaction(...)` で包む。包まないと反撃が反撃を呼んで無限に落ちる。
- ターン外の割り込み攻撃（軋み）は `ctx.Interrupt(...)` で包む。割り込みの中で起きた移動が更なる割り込みを生む再入を止める。反撃とは別の連鎖なので `Reaction` とは別フラグ。再入禁止フラグを Trait の static に置かないこと（Trait は共有シングルトンで、layout モードは戦闘を並列実行する）。
- **割り込み（庇う・後備え・標的）はすべて `SelectTarget` で働く。主目標を差し替えるだけ**なので、範囲攻撃の巻き込み（`PerformAttack` が個別に `ApplyDamage` する）には触れない。貫きは `ResolvePierce` がレーンを直接走るので標的選択自体を通らない。範囲に対処する駒は damage の層（`ApplyDamage` / `OnDamaged`）に置くこと。範囲かどうかは `source.CurrentPattern != Single` で取れるので引数を増やす必要はない（毒・燃焼は `source` が null なので自然に外れる）。
- **攻撃者側の標的選択（執着・第30期）も同じ層に置く。** 割り込み4本が「守る側」なのに対し、
  執着（`FixateTrait`・ノミ）は「殴る側」を縛る初めての例で、窓口は `SelectTargetCore` の
  **pool から無作為に選ぶ直前**1箇所。**記憶は介入の鎖を通ったあとの相手**にする
  ——鎖の前で覚えると毎ターン庇われ続けて執着が動かなくなり、後ろで覚えると
  「庇うで執着を引き剥がす」が規則ゼロで立つ。**pool membership を条件にすること**で
  「前列が生きている限り後列は狙われない」を破らせない（生存判定も兼ねる）。
  **`pattern == Single` は明示的に見る**——手前で分岐するのは貫きだけで、薙ぎ・全体は同じ経路を通る。
  `InstanceId` を持つ記憶は `OnCarryOver` で**必ず捨てる**（戦闘ごとに振り直されるので、
  持ち越すと前の戦闘の番号が次の戦闘の無関係な駒に当たる）。
- **敵から味方へ状態を書く経路は曝き（第40期）が2本目**（1本目は断罪）。**盤面ルールではない**
  ——逆位・渇き・軛・粛は両陣営に等しくかかるが、曝きは**敵陣の駒だけを動かす一方向の効果**なので、
  殉教・断罪・施しと同じ「敵側の語彙のプラス特性」として `Traits.cs` に置く（engine 側の判定はゼロ）。
  発火点は `OnAfterAttack`——ターン頭の無条件発火にすると「保持者を早く割れば止まる」勾配が立たず、
  同数値の対照に対する差分も特性1つに閉じない。**`SwapSlots` には触らない**（移動の通知・
  `HasFallenBack` の記録・`Move` イベントは既に正しく、`self.TeamId` で占有者を引くのでチーム非依存）。
  `Row.Back` に含まれる召喚枠（○後2）は**対象に含めた**（貫きのレーン経路・巨躯の被覆と同じ扱い）。
- **移動を読んで弱体化を書く経路は突き返し（第41期）が初めて。** `ShoveTrait`（ハネ）は
  `OnMoved` / `OnAllyMoved` の両方を購読し、効果A（敵陣の突き崩し・選び方は曝きと共有＝
  `BattleContext.HaulOutPair` の1箇所）と効果B（隣接する生存味方**全員**の `AtkBonus` を引く）を
  この順で実行する。**プラスとマイナスが1つの動作の表と裏**なので `TraitId` のどちらのブロックにも入らない。
  **自分から移動を起こす手段は持たせない**（供給が無ければ1回も発火しないこと自体がマイナス側の一部）。
  上限は1ターン1回で、**ターン境界でリセットせず**「最後に突き返したターン + 1」を
  `Counters` に持つ（据えの `IdleTurn` と同じ作法。engine のターンループには1行も足さない）。
  **再入ガードは `BattleContext.Shoving`**（`Reaction` / `Interrupt` と同型）——効果Aは敵陣を
  動かすので現状は再帰しないが、**敵側に突き返しを持たせた瞬間に無限再帰する**ので、
  1ターン1回の上限だけに頼らない。効果Bは `AcceptsSupport` が偽の駒（ガルドの `Stoic`）を
  **弾く。隣へ流さない**（呪詛・萎縮が `SupportTargets` を通すのとはここが違う）ので、
  「ハネの隣をガルドで固める」が正当な配置解になる——**既存駒への無料の payoff で、潰すべきバグではない。**
  **弱体化のイベントは作っていない**（第41期の判断。第42期に窓口 `ctx.Dull` は立ったが、
  `OnDebuffed` のようなフックはまだ無い——読み手が3枚以上になってから）。
- **弱体化（`AtkBonus` を負にすること）の窓口は `BattleContext.Dull` の1箇所**（第42期）。
  **`AtkBonus` を直接引かないこと。** 渇きが `ctx.Heal` の入口に1つ立っているのと同じ形で、
  集約（引き受け・`TraitId.Bear`）と転嫁（渡し・`TraitId.Relay`）の横取りがここに立っている。
  通るのは**7経路**（分かちのなまり／呪詛の敵側／呪詛の味方漏れ／突き返しのよろけ／萎縮／
  **渡しの転嫁**／**火選りの鈍り**。転嫁だけが宛先が敵側で、窓口の中から窓口を呼ぶ。
  火選り＝第58期は**状態異常を条件に宛先を選ぶ初めての弱体経路**）。
  **墓守の層の減衰は通さない**（`NecroTrait.ApplyStack` が `AtkBonus += desired - applied` を
  負で走らせるが、あれは自分で積んだ自分のボーナスの引き直しで弱体化ではない）。
  **強化の側にはまだ窓口が無い**（`AtkBonus +=` は **14箇所**が直に叩いている。
  `Traits.cs` 13 + `BattleEngine.cs` 1 で、全表は design/PHASE52_GOAD.md の Phase 0-1）ので、
  移り木の `+5` が突き返しの `−2` を打ち消す干渉は**弱体の側からしか読めない**。
  **`AcceptsSupport` の扱いは5経路で3通りに割れている**（無検査／自前で弾く／
  `SupportTargets` で隣へ流す）。**窓口では揃えていない**——揃えると既存45行が動くので、
  それは独立した作業。**窓口の統一と挙動の統一を混同しないこと**
  （代入の行だけを差し替えれば 225 セルが1件も動かない、が第42期の実測）。
- **攻撃力を上げる唯一の窓口は `BattleContext.Whet` の1箇所**（第56期）。
  **`AtkBonus` を直接足さないこと。** `Dull`（鈍らせる）の対義で、通るのは**他者強化の7経路だけ**
  ——駆り立て（カリ）／号令の鬨（ガン・開戦時）／号令の溜め（ガン・毎ターン）／縛め（クグ）／
  移り木（シオ）／**吐き戻し（ゴルム・engine 側）**／**火選り（ヒヨ・第58期。
  状態異常を条件に宛先を選ぶ初めての強化経路）**。`Dull` も engine 側の1本（分かちのなまり）を
  通しているので**対称**。強度は札（`WhetRoute`）で数えるだけで**盤面には一切影響しない**。
  **自己強化の9本は直叩きのまま残してある**（怒り・庇う／殉教・墓守2本・処刑・棘・澱み喰い・
  軋み・分かち）——**意図的な非対称**で、理由は**窓口が将来の横取りの立ち位置になる**こと
  （`Dull` の中にウケとワタが立っている）。**「自分の被弾で自分が強くなる」を他人が横取りできる
  形にしてはいけない**——9本のうち5本が「自分の受けた傷を自分の出力に変える」型である。
  **`Dull` と統合しない**（負の値を渡せる1本の関数にしない）——横取りの立ち位置が2つの意味を持ち、
  「その呼び出しで横取りが走ったか」を呼び出し側の符号から逆算する羽目になる。
  **`AcceptsSupport` の判定は窓口に入れない**（`Dull` と同じ判断。呼び出し側に残す）。
- **強化は「編成が選ぶ通貨」ではなく「ゴルムを入れると勝手に付いてくる通貨」**（第56期）。
  経路別は **吐き戻し 6.01 (48.3%) / 号令開戦 3.36 (27.0%) / 縛め 1.21 / 号令毎T 1.00 /
  駆り立て 0.45 / 移り木 0.41**（合計 12.45 量/戦。弱体は 8.35 で **1.49 倍**）。
  **強化を1点でも通す行は 34/56 で、うち21行は吐き戻し1本だけ。2経路以上を通す行は 8/34。**
  **読み手はウツ1枚**（`AtkBonus` を読むコードは3箇所だが `CurrentAttack` は全駒共通の計算、
  驕りは棄却駒）——第42期に弱体で「供給4・読み手1」を数えて読み手を足したのと同じ形が、
  **供給15（窓口6＋自己強化9）対 読み手1**とより極端に出ている。
- **「毎ターン」と「1回きり」を回数だけで比べてはいけない**（第56期）。
  号令の鬨（開戦時1回・味方全体に +4）は **3.36 量/戦**で、号令の溜め（毎ターン・+8）の
  **1.00 を3倍以上上回る**——**一度に何体へ配るか**を落とすと序列を外す
  （溜めは `SurrenderedTurn` の条件が厳しく「毎ターン」は名ばかり）。
  第36期の「『毎ターン』→『〜したとき』の変換は供給側の作法にすぎない」の裏返し。
- **配る側は「配る先が振るか」を1ビットも見ていない**（第56期）。強化総量の **6.2%** が
  **振る手段を持たない2枚**（不動のカド・`Actions=[Skill]` のノノ）に配られて消えている
  ——**カドは受け手4位（0.71/戦）でその100%が死蔵**。号令とカドは3行で同席している。
  死蔵率は全体で **9.2%**。ただし**上位は2種類に割れる**——`Attacks` は `PerformAttack` を
  通った回数なので、**ターン外に振る駒（仇討ちのザン 38.1%・追い打ちのハギ 23.8%）は
  「死蔵」ではなく「振らずに干渉している」。**
- **「過去の期の受け手の順位」は、その期に使った台の性質であって駒の性質ではない**（第56期）。
  第44期の「弱体の受け手首位はガルド 67.10」を引きずって収支の予測を外した——あれは
  **誹り（敵側・無検査経路）**の値で、**誹りは `Stages` に載っていない**。
  現行56行に残る弱体6経路は**無検査が1本も無い**ので、**ガルドは強化も弱体も 0 の完全な中立**。
  **`AcceptsSupport` の扱いは強化側のほうが揃っている**（自前で弾く3／`SupportTargets` で
  隣へ流す3／**無検査 0**。弱体側は3通りで無検査がある）。統一はしない（既存56行が動く）。
- **陽性対照のノブが盤面を動かすなら「他が動かないこと」を合格条件にしてはいけない**（第56期）。
  `ColossusRule(Regurgitate: false)` も `GoadRule(0)` も**計数ではなく盤面**を切るので、
  決着の長さと生死が変わり他の経路の回数も動く（実測で他経路の総ずれ 0.2% / 0.8%）。
  **合否は「狙った経路が 0 になったか」の1本だけで読む**——「他が動かない」を条件にすると
  **盤面を動かさないノブしか対照に使えなくなり**、それでは「窓口が呼ばれているか」しか
  確かめられず**経路の実在は確かめられない。**
- **「機構として起こりうる」と「盤面で起きている」は別**（第56期）。第52期の
  「カリはウツの呪いを『治して』殺す」は**診断 `goad` のローカルに組んだ台**での観測で、
  **号令・駆り立てとウツは現行56行で一度も同席していない**（ウツが受ける強化は
  **吐き戻し1本だけ**・0.22 量/戦、符号が正へ渡った回数は **0.002 回/戦**でほぼ安全）。
  窓口はこれを常時見せるためにある。
- **燃焼を読んで強化・弱体へ変換する経路は火選り（`FavorTrait`・ヒヨ）が初めて**（第58期。
  **第60期に改名した**——旧「焚き付けのフイ」。「焚き付け」は火を点けることを意味するが
  **この駒は火を点けない**ので、名前が能力を誤って説明していた）。
  第57期の表E（9通貨 × 双方向）は 18 セルすべて 0 で、**燃焼は盤面のどこにも繋がっていなかった。**
  **engine には規則も窓口も1つも足していない**——`Whet`（第56期）と `Dull`（第42/43期）に
  `WhetRoute.Favor` / `DullRoute.Favor` を**末尾に1つずつ**足しただけ。
  1回の発火で **プラス＝燃えている味方全員（自分を除く・位置を問わない）に
  `Whet(Gain)`／マイナス＝隣接する燃えていない味方に `Dull(Loss)`** を順に走らせる。
  **プラスを全体・マイナスを隣接にするのが要点**——逆にすると「隣に火があるか」だけの二値になり
  配置の判断が消える。**候補は両側とも自前で `AcceptsSupport` 濾しする（隣へ漏らさない）**
  ——`SupportTargets` で漏らすと**流した先が燃えているとは限らず、規則そのものが破れる。**
  **乱数を1つも引かない**（`LivingMembers` はスロット昇順の決定的なスナップショット）ので、
  **`FavorRule(0, 0)` は同数値・特性なしの素体と1セルも違わない**——これが診断の検算。
- **供給者は自分の撒いたものを持たない。「持っていない者を罰する」機構は供給者を狙い撃つ**（第58期）。
  火の粉は**隣接する味方**に燃え移るが**ボルグ自身には移らない**ので、ボルグは戦闘を通じて
  **非燃焼のまま**。火選りをその隣に置くと**毎ターン火種の腕を削る**——仮置きの3行は
  **弱体の最大の受け手がすべてボルグ**（4.26 / 4.01 / 3.83 量/戦）で、配置を直しただけで
  機構の帰属が **−18.2 → +2.1pt** 動いた。**「持っている / 持っていない」を条件にする機構は、
  供給者がどちら側に立つかを実装前に確かめること。**
- **`OnTurnStart` の機構は `OnAfterAttack` の供給に対して構造的に1ターン遅れる**（第58期）。
  ターンの順序は `TickStatuses` → `OnTurnStart` → 行動順ループで、火の粉は `OnAfterAttack`
  ——**第1ターンの発火時点では盤上の誰も燃えていない**（実測でホタの `CurrentAttack` は
  ターン頭のスナップショットで T1 が 6・T2 以降が 24）。遅れは空振りではなく**代金**として出る
  （弱体の受け手に熾のホタ 2.00 量/戦 ＝ 第1ターンの1回 × `Loss` 2 がちょうど載る）。
  **係数では詰められない。** 第57期の「ホタは 100% 燃えた状態で振る」は**行動順ループの中の話**で、
  **「稼働 100%」を別のフックから読み直すときは、フックの位置を先に確かめること。**
  **第60期に発火口を手番（`OnAction`）へ降ろしてこの代金を消した**（次の項）。
- **熾火に配った強化1点は燃えている間ちょうど 4 点になる**（第58期の乗算監査・積み残しだった）。
  `UnitState.CurrentAttack` は `Def.Attack + AtkBonus` を作ってから `ModifyAttack` を通すので、
  **強化は素の攻撃力と一緒に掛けられる。** しかも `PyreTrait.ModifyPattern` が同時に貫きへ変えるので
  **与ダメの実効は 7.4 倍**（ホタ +17.7 対 ムド +2.4 / 点）。**それでも機構の帰属は +2.1pt しか出ない**
  ——その行は第2〜4波で 95〜100% に張り付いていて、増えた出力が決着ターンの短縮に消える
  （第24期 `yield` の「天井・床のセルでは誰に注入しても 0 に潰れる」）。
  **乗算の危険は「勝率が跳ねること」ではなく「跳ねる台が出てきたときに跳ねること」**
  ——ホタ1枚に +4 を注入すると単独で 72.9% → 94.3% まで行く。
  **除外は入れなかった**（対象集合に熾火だけの例外ができ、規則が1行で説明できなくなる）。
- **2本のノブを持つ機構では、「一方を動かすと他方の計数が動くか」を掃引の表で確かめる**（第58期）。
  `FavorRule` の採用値は **`Gain = 4` / `Loss = 2`**（探索段階の初期値 2/2 から動かした。
  第25期の軛と同じ採り方）。`Gain` を 2 → 4 にしても**`撒いた`（弱体の総量）は 1 も動かない**
  ——代金の相手（**供給者と第1ターンの味方**）は構造的に非燃焼なので、
  見返りを厚くしてもその集合は縮まない。**だから片側だけを厚くするのが素直に通る。**
  第52期の「打ち消しを避けたければノブが動かす量を1本にする」の系だが**向きが違う**
  ——あちらは1本のノブが逆向きの2量を動かすのが問題で、こちらは
  **2本のノブが互いの対象集合に影響しない**ことが効いている。
- **燃焼を書く2枚目は駒ではなく既存特性への1行だった**（第59期）。破裂（`BomberTrait.OnDeath`）は
  30 期以上前から「味方も巻き込む。**これが他の駒の起点になる**」とコメントに書いてあり、
  **既存のループの中で `ctx.Ignite` を呼ぶだけ**で燃焼の2枚目の書き手になる。
  **engine に規則も窓口も足していない**——足したのは `BattleContext.Blaze` と `Run` の引数のみ。
  強度は `BlazeRule(BlazeTargets)` を `Run` に引数で渡す（**採用値 `Both` ＝ 破裂が当たった全員**。
  static のノブは置かない）。**`Ignite` は乱数を1つも引かない**ので着火を足しても乱数列は動かず、
  **ゾトを含まない52行が `None` 対 `Both` で 260 セル 0 件**になる——これが検算。
  巻き込みで倒れた相手には点かない（`Ignite` が `IsAlive` で弾く）。
- **符号の違う2つの効果を1つの動作に持つ機構は、片側だけを 0 にする対照が無いと因果が読めない**
  （第59期。第41期の突き返しの規則を**対照を作る側から**使った）。
  `BlazeTargets.Both` は「味方に在庫を作る」と「敵に打点を足す」の合成で、
  **`FoeOnly`（敵だけ・ノブではなく対照）を足すまで「勝率が上がったのは墓守の層に乗ったからか」が割れなかった。**
  実測は決定的で、**層を太らせる唯一の経路（`AllyOnly`）が唯一負ける経路**
  ——`AllyOnly` は7行すべてで負（平均 −11.5pt）なのにリィカの `AtkBonus` を +127.1 → +138.2 へ押し上げ、
  `FoeOnly` は7行すべてで正（平均 +10.8pt）なのに `AtkBonus` を +119.5 へ**下げる**。
- **判定式の条件節が分母の何割を選ぶかを、実装前に数えること**（第59期）。
  第59期の指示書は「**リィカを含む行が +5.0pt 以上上振れしたら棄却**」を拒否権にしたが、
  **ゾトを含む7行はすべてリィカを含む**ので、規則は「勝率が上がる版はすべて棄却」と同義になり
  **唯一層を太らせる版だけが生き残る**——規則が意図の逆を向いた。
  **分母が全部条件を満たすとき、「条件を満たす行が動いたら」は「どれかが動いたら」になる。**
- **「一度きりの供給」は蘇生役がいると一度きりでなくなる**（第59期）。
  破裂は `OnDeath` で発火するので**死ぬ回数がそのまま供給の回数**になり、
  継ぎ接ぎ（ヴェル）を含む5行では **1.51〜2.00 回/戦**・いない2行では**ちょうど 1.00**。
  捨て率も **37.9〜48.3% 対 0.0%** と完全に割れる。
  **供給の回数を「その機構が何回発火するか」で数えると、蘇生・召喚・分裂が入った瞬間に外れる。**
- **`reseat` / `confirm` は「勝つ席」を探す道具で、「測れる席」を探す道具ではない**（第59期）。
  試験行 `死軸×ホタ` は `reseat` の**上位6通りがすべて情報セル 1**（第2〜5波が天井に張り付く）で、
  情報セルを両 seed 帯で 2 以上に保つ最上位は**7位**、その7位と現行の差は **+4.8pt** で
  採否閾値 5.0pt に届かなかった（＝動かさない）。**勝率だけの1位は +6.8pt だが情報セル 1。**
  **新しく `CompareBuilds()` に載せる行では、席の探索に情報セルの列を出すこと**（`blaze scan`）。
  第46期が閾値を 2.0 → 5.0 に上げたのはノイズ避けだったが、**その閾値は
  「勝率を最大化する席を採り過ぎない歯止め」としても働く。**
- **盤面ルールを採用した期は「既存行のセルは不変」が原理的に成り立たない**（第59期）。
  第40〜58期の新駒の期はこれが回帰チェックそのものだったが、
  盤面ルールの採用は既存行を動かすのが中身である。代わりの検算は2本立て:
  **(a) その機構を持たない行が規則の値に対して不変**（乱数列を動かしていない）と
  **(b) 規則を無効にした版が採用前の `docs/balance.md` と完全一致**（規則の追加自体が盤面を動かしていない）。
  **(b) は既定を変える前にしか取れない。規則を足した直後に先に取ること。**
- **能動的な機構を `OnTurnStart` から手番（`OnAction`）へ降ろす窓口は既にある**（第11期 Phase BB・第60期に3枚目）。
  `Trait.OnAction` に本体を置き、`OnTurnStart` は **`ActsOnPattern` が偽のときだけ**従来どおり発火させる
  （継ぎ当て＝`MenderTrait` の形。**保持者が1枚でも分岐を残すこと**——同じ特性を共有する敵駒に
  `Actions` が無いと、無条件に移した瞬間そちらの効果だけが静かに消える）。
  駒側は `Actions = new UnitAction[] { new(ActionKind.Skill, ...) }` の**1要素だけ**にする。
  **engine には1行も足さない。** **判定材料が `UnitDef.Actions` なので、版の切り替えは規則（`Run` の引数）ではできない**
  ——診断のローカルの `UnitDef` で切り替える（`gradient` / `aim` と同じ扱い）。
- **係数が動かせない量があるときは、フックの位置を疑う**（第60期）。第58期は `Gain` を上げても
  `撒いた` が 1 も動かなかったが、**発火口を手番へ降ろすと4行とも下がった**（−1.87〜−4.18 量/戦）。
  第1ターンにちょうど載っていた **2.00 量/戦**（＝1回 × `Loss` 2）が
  **熾のホタと逸らしのソラの2枚から同時に消えた。**
  **ターンの順序（`TickStatuses` → `OnTurnStart` → 行動順ループ）に対して供給がどこにあるかで、
  機構が読める在庫が決まる。**
- **代金の大きさは「量」ではなく「誰に落ちるか」で決まる**（第60期）。`Loss` を 2 → 4 にすると
  `撒いた` は**きっちり倍**になる（0.56 → 1.16 / 6.55 → 13.03）のに、勝率への効きは
  **第58期の 6〜12 分の1**（同じ2点間の Δ が −11.9 → −1.0pt）。移設で残った受け手が
  **ボルグ**（巻き込みが本体で攻撃力への依存が薄い）に寄ったため。
  **弱体の帳簿を「撒いた総量」で読むと値段を1桁間違える**——第43期の
  「隣に置きたい駒は『弱体を受ける駒』」の逆側。
- **手番へ降ろすと止められる経路が増える、とは限らない**（第60期）。新しく効くのは痺れだけで、
  実測の供給者は**4行に 0 枚・5波に 0 枚**（`turn` の Q4 は 20 セルすべて 0.00 回/戦）。
  一方**移設は経路を1つ閉じる**——断罪（`CondemnTrait`）は殴ってきた攻撃者を罰する型なので、
  **振らなくなった駒には原理的に当たらない。**
  **交絡の勘定は増える側と減る側の両方を数えること。**
- **「盤面ルールが効かない」と「その波のセルが動かない」は別**（第60期）。粛は移設の前後で
  1ビットも変わっていない（**行動順ループは `CanActOutOfTurn` を1度も呼ばない**——
  呼び出し元は特性側の4本＝棘・仇討ち・軋み・追い打ちだけ）のに、第2波のセルは2行で動いた
  ——その駒が殴らなくなった手番が機構に変わったぶん。
  **予測は機構の側（封じる本数）で書くこと。波のセルには他の全部が入ってくる。**
- **採用で既定が動いた診断は、検算の相手が V0 から V1 へ移る**（第60期。第36期 `gullet belly4` と同型）。
  移設を採用すると `UnitCatalog` の側が V1 になるので、**診断のローカルに置くのは「移設前の姿」**であり、
  **V1 が `docs/balance.md` と一致することが診断そのものの検算**になる。
  **採用したら、その期のうちに診断の V0 を作り直すこと**——放置すると次に走らせたとき
  V0 と V1 が同じものを指す（第60期に1度そうなった）。
- **`OnTurnStart` は「全員より先」という speed = ∞ の席である**（第61期・瘴気＝**採用しなかった**）。
  能動的な機構を手番（`OnAction`）へ降ろすと、**既に手番へ降りている駒に対して速さで順序が決まる。**
  グザ（瘴気・速5）は**澱み（ミオ・速8・第11期に移設済み）に負ける**ので、
  移設すると**第1ターンにミオが増幅する毒が盤上に1層も無くなる**——増幅は「毒が無ければ
  何もしない」うえ毒は刻んでも減らないので、**その欠損は残りの全ターンに効き続ける。**
  実測で **ミオを含む4行の帰属は平均 −26.4pt・含まない4行は −2.4pt**（両 seed 帯で再現）。
  **移設の前に「その駒より速い読み手が既に手番にいるか」を数えること。**
  第60期のヒヨ（速6）が得をしたのは供給側（火の粉・`OnAfterAttack`）が**ボルグ速8**で
  移設後もヒヨより先だったからで、**相手が速い側にいた偶然である。**
- **同じターン頭に発火する機構どうしの前後は席順（スロット昇順）だけで決まり、手番へ降ろすと消える**（第61期）。
  瘴気（グザ）と澱み喰い（ヴィオ）はどちらも `OnTurnStart` で、グザが先なら撒いた毒はその場で
  吸い上げられて**味方は1点も払わない**。席を入れ替えると味方の刻みが **+11.8 / +10.7** 動くのに、
  手番へ降ろすと **+0.4 / +2.3** に潰れる（**供給を 6 割増やしても味方の負担が増えない**）。
  **判断は実在した——ただし `CompareBuilds()` の 4 行すべてが「無料の側」に置かれていた。**
- **「代金」と「資産」が同じ蛇口から出る機構では、供給を絞っても符号は割れない**（第61期・主判定の否定）。
  毒喰らい（ベニ）が読むのは「毒に侵された**敵**の数」で**味方の毒には触らない**ので、
  「ベニ行は毒漏れが減ると得」という見立ては外れた——味方の刻みも敵の刻みも回復の分母も
  **1つの `PerTurn` から出ている**ため、供給を半分にすると全部が半分になる。
  実測は**ヴィオ行 −27.6 / ベニ行 −12.7 で符号が同じ**（両帯で再現）。
  **符号を割りたければ、代金と資産に別々のノブが要る**（第41期「符号を測りたい効果は、
  その効果だけを 0 にできるノブと対にして作る」の供給側の版）。
- **「その効果だけを 0 にする素体対照」は、台を床に落とすと1ビットも出ない**（第61期）。
  ミオを同数値・特性なしに落とすと **4行中3行が 20.0%**（＝`100/0/0/0/0` の床）に並び、
  そのセルの「差 0.0」は「差が無い」ではなく**「測っていない」**。
  **対照を作る前に「その駒を抜いた台が床でないか」を測ること**——第21期 swap の飽和検査
  （4体版と5体版が同じ値なら測定にならない）の**逆側**である。
- **歯止め（第五波）は「その機構を持つ行が主判定に何行あるか」で拒否権になったりならなかったりする**（第61期）。
  瘴気は主判定19行のうち **1 行**（`毒+耐久`・第五波 4.5%）しか含まないので、
  その行が 0% まで落ちても平均の低下は **4.5 ÷ 19 = 0.24pt**——**構造的に発動しない。**
  第60期に主判定を固定行集合へ移した目的（分母が動かない）の裏返し。
  **判定式の自己検査に「その条件が動かしうる分母の割合」を数える項を足すこと**
  ——第59期（条件節が分母の全部を選ぶ）・第60期（条件が原理的に成立しない）に続く3例目。
- **`FavorRule` の `Gain` は上げなかった**（第61期・第60期の持ち越しの決着）。`(4,2)` → `(6,2)` の
  **乗算の比**（乗算持ち＝熾のホタがいる行の伸び ÷ いない行の伸び）は
  **A 帯 2.15 / B 帯 2.66**（指示書と同じ 行1÷行2 の形）・**4.73 / 6.47**（行1÷乗算なし3行の平均）で、
  **4つとも 2.0 以上。`Gain` はホタ専用のノブになっている。**
  情報セルも4行・両帯で1つも動かない（＝区別を増やさずに1行だけを持ち上げる変更）。
  次期の候補は「見返り側に**着火**（`ctx.Ignite` は乗算を通らない）の出口を作る」1本
  ——第58期 1.37 → 第60期 2.15 → 第61期 2.15/2.66 と、**比は移設で跳ね上がったまま戻っていない。**
- **強化の「行き先」を書き換える経路は横流し（`FunnelTrait`・ヌキ）が初めて**（第62〜64期・
  **3回測って採用しなかった。第64期で残置が確定し、以後この駒は提案しない**。
  第64期は行・枠・席の選び方を測る前の規則で固定して測り、**主判定 Q2''（席ごとの帰属の幅 ≥ 5.0pt）は
  3行とも通った**（8.1 / 9.1 / 19.6pt・両帯で再現）が、**Q3（帰属が正の行が1つ以上）が
  3行とも負**（−3.8 / −8.8 / −18.4）で落ちた。
  **第63期に直した器具で測り直しても採らなかった**——`FunnelRule(Slowest, Both)` の `Both` が
  第63期に足した V3＝**弱体も同じ宛先へ流す**版で、既定は `false`。
  宛先の選び方は `BattleContext.FunnelThrough` 1本に寄せてあり、**V1 と V3 で1ビットも違わない**。
  `Dull` 側の候補プールは**集約・渡しと共有**する（第43期）。経緯は design/PHASE63_FUNNEL2.md）。
  `BattleContext.Whet` の `receiver` の位置——第56期がコメントで席を空けておいた場所に、
  `Dull` の集約（ウケ）・転嫁（ワタ）と**同じ位置・同じ形**で立てた。**engine に新しい窓口はゼロ。**
  規則は「**自分と隣の味方に来た強化を、すべて自分の隣で一番遅い味方へ回す。自分は育たない**」で、
  **量は加算のまま**（減衰も倍率も無い＝**強度のノブが無い**。振るのは選択子 `FunnelRule.Slowest` だけ）。
  **1ホップで止める**のは宛先の候補から横流し役そのものを除くことで担保する。
  **候補が 0 / 1 個では `Roll` を消費しない**ので、保持者が盤上にいない行は 305 セル 0 件で不変。
  **測って採らなかった**（design/PHASE62_FUNNEL.md）——理由は主判定 Q2 の判定式が
  「帯の峰」だったこと1点で、**Q1（流量 41〜66%）・Q3（帰属 +1.8/+12.2/+8.9・両 seed 帯で再現）・
  Q4（罠の死蔵 100%）・Q6（情報セル 2/3/4）はすべて通っている。**
- **「行き先を書き換える」機構の値段は、行き先に立っている駒の性質がすべて**（第62期）。
  同じ規則・同じ5枚で**席を1つ動かすだけで帰属が 18.4pt 動く**。しかも**流量と価値が逆を向く**
  ——流量 89.9%（宛先ムド）の席が、流量 46.1%（宛先ホタ）の席に 9.5pt 負ける。

      宛先が 熾のホタ（燃焼中は乗算4倍）        → +8.9pt
      宛先が 泥人形ムド（攻3・被弾強化）        → −9.5pt
      宛先が 棘鎧のカド（不動・一度も振らない） → 回した全部が死蔵（100.0%）
      宛先が 大喰らいゴルム（吐き戻しの出どころ）→ 自己循環して −9.5pt

  第60期 13-2「弱体の帳簿を『撒いた総量』で読むと値段を1桁間違える」の**強化側の版**で、
  こちらは**同じ駒・同じ規則の中で符号が入れ替わる。**
- **「一番◯◯な隣」型の選択子は、向きが本体ではない。本体はその端に誰が立っているか**（第62期）。
  「遅い側へ流す」V1 と「速い側へ流す」V2 の対照は**行ごとに違う答えを出した**
  （+1.8→−0.4 / +12.2→−3.4 / **+8.9→+10.4**）。**V2 のほうが強い行がある**のは
  V2 の宛先がボルグ（攻18・薙ぎ）だったからで、**選択子は宛先を1体に決める関数にすぎず、
  値段はロスターの側にある。**
- **`AcceptsSupport` で落ちる駒を除いた候補集合の大きさを、実装前に数えること**（第62期）。
  ガルド（`Stoic`）は速4 でロスター3番目に遅いのに**一度も宛先にならない**。
  仮置きの1行では候補が1枚に潰れ、**V1 と V2 が同じ宛先になって選択子そのものが観測できなく**なっていた
  （`funnel phase0` の机上計算で実装前に検出した）。
- **味方から敵へ状態を移す経路は渡し（`RelayTrait`・ワタ）が初めて**（第43期）。
  第40期の曝きが作った「敵から味方へ」の逆向きで、**engine 側に足した規則はゼロ**
  ——`Dull` が最初から両陣営を通るので「横取りして流し先を敵にする」だけで済む。
  **横取りの条件は集約と同じで、候補プールも共有する**（`Dull` の候補式に
  `Bear || Relay` を書いた1箇所。優先順位を固定すると片方が構造的に飢える）。
  流し先は**敵陣で `CurrentAttack` が最も高い生存駒**を決定的に選ぶ（同値のみ `PickOne`）。
  転嫁は `Dull` の中から `Dull` を呼ぶ唯一の経路なので、**再入ガード
  （`BattleContext.Relaying`）を先に置く**——敵側に渡しを持たせた瞬間に無限往復する。
  代金は **HP**（`RelayTrait.HpCostPerDull = 2`・`ApplyDamage` を通す）。強度は
  `RelayRule(TransferPercent)` を `Run` に引数で渡す（static のノブは置かない）。
- **`TransferPercent = 0` は「横取りするが流さない」＝除去役そのもの。**
  対照であると同時に設計案で、**同じ代金の2つの版を1つのノブで比較できる**。
  実測は 51.8%（除去）vs 77.6%（転嫁）で、**「弱体を消す役」は代金に見合わない**
  ——価値のほぼ全部が「敵の攻撃力を下げる」側にある。
- **増幅の無い転嫁が、増幅のある読み手2枚より強い**（第43期）。同じ供給・同じ土台で
  読み手1枚だけを差し替えると 逆しま(×3・自分だけ) 73.1% / 引き受け(×2・自分だけ) 56.0% /
  **渡し(×1・味方全体) 77.6%**。**増幅ゼロでも、届く範囲のほうが出力になる。**
- **「隣に置きたい駒」は『弱体を受けると困る駒』ではなく『弱体を受ける駒』**（第43期）。
  なまりは**守られた駒**に乗るので、供給は攻撃力ではなく**被弾**で決まる。実測の横取りの相手は
  ガルド 35.53 / ノノ 17.06 / **ドルガ（ロスター最高攻38）4.01**。`reseat` も予測と逆に
  **上位8通り全部が中央**で、ワタを角へ動かすと横取りは 11.32 → 2.28 と**次数の比 2倍に対して 5倍**落ちる。
  **隣接次数は「隣が何人いるか」しか数えていない。効くのは「隣が殴られるか」。**
- **自弁率は「肩代わり役がいるか」ではなく「1回の代金の大きさ」で決まる**（第43期）。
  分かちは `amount * 40 / 100` の**切り捨て**なので、1回の代金が 2 なら 0 しか取らない。
  採用行（1回 2〜4）は自弁率 88〜100%、萎縮を同席させて 1回 18 にすると 48.8%。
  **代金を小口で払う機構は肩代わりに割り込まれない**——「代金を誰かに肩代わりさせる」を
  編成の選択肢にしたいなら、**1回の代金が肩代わりの割合の逆数（40% なら 3 点）を超える**必要がある。
- **「開戦時1回・全体」の供給と転嫁を同席させると盤面が飽和する**（第43期・**持ち越し**）。
  萎縮（クビ・味方1体につき 9）とワタ中央を組むと、45 点のうち **36 点を1ターン目に
  横取りして敵へ流す**（第五波なら敵の総攻の 48%）。平均勝率 99.8%。
  **`CompareBuilds()` には足していない。** 窓口を通る総量が同じでも、
  **前払いか分割払いかで意味がまったく違う。**
- **敵側から弱体を撒く経路は誹り（`SlanderTrait`・第44期）が初めて。測って採用しなかった。**
  `OnAfterAttack` で `ctx.Dull(target, Penalty, DullRoute.Slander)` を1回呼ぶだけ（engine 側の
  規則はゼロ・`SlanderRule(Penalty)` を `Run` に引数で渡す・既定 0）。保持者は
  `EnemyCatalog.Slanderer`（誹りの巡礼騎士。第二波の `KnightG` と数値・型・速さが同一）で、
  **`Stages` には載せずに定義だけ残してある**——逆位・まどろみと同じ扱いで、診断 `slander` が
  波をローカルに組んで使う。機構は完全に動く（ウケ・ワタの横取り率 100%、ワタは撒いた
  本人へ流し返す）が、**符号反転が再現しない**——第二波で上がったのは 48行中1行（+1.0pt）で、
  別 seed 帯で −2.0pt に割れた。残り21行は全部下がって符号を保つ。経緯は design/PHASE44_SLANDER.md。
- **敵側に供給源を置くと、供給量と「受け取る側の強さ」が負に相関する**（第44期）。
  味方側の供給（なまり・萎縮・突き返し）は編成の一部なので強い編成ほど多く供給するが、
  **敵側は逆**——強い編成ほど供給源を早く割るので浴びない。実測で保持者は平均 **2.70T** で落ち、
  78% の試行で決着前に死ぬ（発火は敵の振りの 12.6% 止まり）。
  **「早く割れば止まる」勾配は、供給機構に対しては勾配ではなく上限として働く。**
  第40期の曝きが機能したのは撒くものが**移動**（1回で盤面が変わる離散的な出来事）だったからで、
  **蓄積する量を敵側から撒くと、蓄積の時間を持てるのは味方が弱い試行だけになる。**
- **敵からの供給は「殴られる駒」に落ちる。読み手の席では受け取れない**（第44期）。
  第43期の「隣に置きたい駒は『弱体を受けると困る駒』ではなく『**弱体を受ける駒**』」の敵側版で、
  受け手は敵の標的選択が決めるので**肩代わり役に 73% が集中する**（ガルド 67.10 / ゴルム 44.19 /
  カド 22.18 per 戦）。**ウツ・ウケ・ワタは上位12枚に1枚も入らない。**
  届いたのは**横取りを持つ2枚（ウケ・ワタ）だけ**で、そこでは横取り率 100.0%。
  **系: 自分の身に来たぶんだけを読む型（逆しま）は、敵側の供給に対して構造的に飢える。**
- **肩代わりで育つ駒は、肩代わりで集めた弱体では沈まない**（第44期）。ガルドは誹りの最大の
  受け手（67.10/戦）なのに攻ゼロへの寄与は **+0.18** しかない——`RedirectGainTrait` が
  弱体を集めるのと同じ動作で弱体を打ち消している。見返りを持たないゴルムは +1.55 で、
  **「肩代わり役に弱体を撒く」は見返りの有無で結果が正反対になる。**
- **標（`Marked`）を操作する経路は逸らし（`DivertTrait`・ソラ）が初めて**（第50期）。
  それまで書き手は囃し立て（ヒサ）1枚で「隣接する最大HPの味方1体に**開戦時1回**」だけ、
  **消す経路は1つも無かった**（`SetCounter(StatusKeys.Marked, 0)` が grep で 0 件）。
  **engine には規則を1つも足していない**——標の窓口は元からあるので、
  **書き手（敵と自分へ付ける）と消し手（味方から外す）を駒側に作っただけ。**
  `OnTurnStart` の1回の発火で「外す → 自分に付ける → 敵の現在HP最大 `TargetCount` 体に付ける」を順に行う。
  **敵に付けた標は消さない**（外すのは味方だけ）ので**焦点は自分で溶ける**（焦点数 最大 4）。
  **味方に標が1つも無くても発火は止めない**——止めると「味方が綺麗なら何も起きない」駒になり、
  代金だけが残る局面が作れなくなる。
- **標の2枚目の書き手が駆り立て（`GoadTrait`・カリ）**（第52期）。ヒサが「隣接する最大HPの味方に
  **開戦時1回**」なのに対し、**毎ターン・隣接する `CurrentAttack` 最大の味方**に
  **標と `GoadRule.Boost` を同時に**渡す（クグの縛め＋攻撃+16 と同じ「1つの動作の表と裏」）。
  **engine には規則を1つも足していない**（計数フックだけ）。
  **前ターンの対象からは標を外すが強化は残す**——「一度渡した力は返らないが、矛先は移る」。
  記憶は `Counters["goadTarget"]` に `InstanceId + 1` で持ち、**`OnCarryOver` で必ず捨てる**
  （執着の `FixateTrait.MemoryKey` と同じ理由）。
  **候補は `AcceptsSupport` で絞る**（縛め＝`BindTrait` に揃えた。**1つの動作なので
  標と強化で候補集合を分けない**——力を渡せない相手（ガルド）は押し出しもしない）。
  **選択は `CurrentAttack`**（素の `Def.Attack` ではない）ので強化が積むと選択が固定されるが、
  **逆しま（ウツ）だけは自己修正する**（`AtkBonus` が正だと攻撃力半減）。
  **`OnTurnStart` は行動順ループの外側なので粛（`Hush`）に封じられない**
  ——`CanActOutOfTurn` を通らない（第52期 Phase 0-5 で確認）。
  **害の中に見返りを埋めたつもりが逆だった**——標の寄与 +36.8 / −10.0pt に対し
  強化の寄与は +3.3 / +4.2pt しかない（design/PHASE52_GOAD.md）。
- **敵に付いた標を読む経路は止め（`FinisherTrait`・トメ）が初めて**（第53期）。
  仇討ち（ザン）が読むのは**味方**の標なので、**第50期にソラが敵へ標を付けられるようになってから
  第52期まで、敵の標の読み手は engine だけだった。**
  **engine には規則も窓口も1つも足していない**——3つの効果を既存の3箇所に置いた:
  **対象の強制**は `SelectTargetChain` の**標の段**（`PickOne(標持ち)` を
  `FinisherTrait.Preferred`＝現在HP最大・同値のみ `PickOne` に差し替え、`Roll(100)` を飛ばす）、
  **倍率**は `PerformAttack` が `atk` を作った直後（`Trait.ModifyAttack` は**対象を受け取らない**
  ので「相手が標を持つか」で分岐できない）、**消費**だけが駒側の `OnAfterAttack`。
  **執着・断ちの窓口（`pool` から選ぶ直前）に置いてはいけない**——`pool` は前列が生きている限り
  前列しか含まないので、**列越え（この駒の主眼）が構造的に消える**。
  **止めのときは `marked == target` でも返す**（庇い・後備え・殉教を飛び越す。標が元から持つ性質）。
  強度は `FinisherRule(Multiplier)` を `Run` に引数で渡す（採用値 2。static のノブは置かない）。
  `Consume` は**ノブではなく対照**（`DivertRule.SelfMark` / `GoadRule.Mark` と同じ扱い）。
- **状態異常そのものを移す経路は業（`ScapegoatTrait`・ゴウ）が初めて**（第49期・**棄却**）。
  集約・転嫁が移すのは**弱体**（`AtkBonus`）、澱み喰いは**消すだけ**、疫みは**死体からの撒き直し**で、
  **生きている味方から状態異常のカウンタを取り上げて自分に積む経路は 0 件だった。**
  数える種類は `ScapegoatTrait.Kinds`＝`StatusKeys.All` から `Armor` と `IdleTurn` を**除いた形**で書く
  ——アーマーは damage 側のプラスの資源（数えるとヒビ1枚で稼げる抜け道）、`IdleTurn` は
  engine が痺れを振り替えて書くうえ**`0` に戻す箇所が1つも無い**（一度手番を落とせば永久に1種類を持つ）。
  **「傷は味方に載らない」を規則として焼き付けない**（除外を並べる形なのでキーが増えれば自動で数に入る）。
  **燃焼を `ctx.Ignite` に通さない**——`Ignite` は残ターンを `BurnRules.Turns`(3) に**設定**するので、
  1 を移すつもりで呼ぶと味方 −1・自分 +3 ＝**複製**になる。種類を問わず一律にカウンタを 1 だけ動かす。
  **`AcceptsSupport` を見ない**（呪いを引き取るのは支援ではない。ガルドから取り上げるのは筋が通る）。
- **状態の肩代わりは引き受け（`BearTrait`・ウケ）が1本目**（第42期）。肩代わり5種
  （庇う・分かち・巨躯・後備え・棘守り）は全部ダメージだった。横取りの実装は
  **`Dull` の中**にある——「弱体が入る**直前**に横取りする」は駒ごとのフックでは書けないので、
  `BearTrait` の本体は空の札にしてある。変換先を**アーマー**にしたのは、被ダメージを
  減算で下げると敵の一撃を下回った時点で二値化するため（`ModifyIncomingDamage` に
  書くと崖が戻る）。強度は `BearRule(ArmorPerDull)` を `Run` に引数で渡す（static のノブは置かない）。
  **横取りは隣接に限る**——「味方全体」にすると配置の判断が消え、逆しまと同居した瞬間に
  必ず集約が全部持っていく。隣接に限れば「ウツをウケの隣に置かない」が配置解として残り、
  実測でも非隣接の方が **+9.1pt** 強かった。**自分自身の弱体は横取りしない**（再帰を作らない）
  ので、中央（隣接次数4）でも被覆率は 72〜87% にしかならない。
- **「遅い通貨/速い通貨」は通貨の属性ではない**（第42期）。第23期の吐き戻しが
  「攻撃力という遅い通貨に変換したので使う前に戦闘が終わる」で終わったので、
  アーマーでも死蔵率 40〜70% を予測して **2.5% で外した**。
  **死蔵は「生成率 ≫ 消費率」のときにだけ起きる**——その駒がその通貨をどれだけの速さで
  使う立場（席）にいるかで決まる。**変換先を疑う前に流量比を数えること。**
- **攻撃者側の標的選好が2本目（断ち・第37期）。** 執着が「1体に縛る」マイナスなのに対し、
  断ち（`SeverTrait`・ナタ）は「傷がいちばん深い相手を選ぶ」プラスで、**窓口は執着の直後の同じ段。**
  `pool` は1体も足さない・引かないので中核規則は破れず、介入の鎖（後段）が選好を上書きする。
  **候補集合は `BattleContext.TargetPool` の1箇所を選好と `CanAct` が共有する**
  ——2箇所で数えると「振ると決めた手番に狙う相手がいない」が起こりうる。
  効いた手番は `pool[Roll(...)]` を**引かない**（執着と同じ。引くと乱数列がずれる）。
  **第39期に利用者が2枚になった（ナタ＝断ち / ハリ＝縫い）が、段は増やさない**
  ——選好の定義は `SeverTrait.Preferred`、利用者の判定は `SeverTrait.Prefers` の1箇所きり。
  共有しないのは閾値と手番の放棄（`CanAct`）だけで、そこは断ち固有。
- **肩代わりは価値を消さず、経路を変えるだけ**にする（巨躯の吐き戻し・第23期）。飲み込んだ分は
  **壁自身ではなく庇った相手**の攻撃力に変える（`SupportTargets` を通すので支援拒否は隣へ漏れ、
  逆しまには弱体として効く）。`source` が null の継続ダメージでは返さない（庇うと同じ除外）。
  **壁自身を育てないこと**——前列で素の被弾が膨大なので「壁だから育つ」になって機構との結び付きが切れる。
- 肩代わり（分かち・巨躯）を `ApplyDamage` に足すときは **`u != source` を必ず入れる**。自分が出どころのダメージまで肩代わりすると打ち消しになる。巨躯で実際に踏んだ（大喰らいの吸いを壁が9割引き受けて、代金が消えていた）。症状は `pulse` の `被(味)` が不自然に小さくなること。
- 破片（`StatusKeys.Armor`）は HP の前に削られるプール。**回復とは別資源**で、`ctx.Heal` が見る `AcceptsSupport` を通らないので `Stoic`（ガルド）にも届く。「1発を完全に吸う」ではなく超過分を素通りさせるプールにしてあるのは、二値にすると README の浄化と同じ「引き算は崖」の穴に落ちるため。
- 軛（盤面ルール）は `ApplyDamage` の**最後**、`target.Hp -= amount` の直前で1回のダメージを
  `YokeTrait.Cap` で切る。**新しい増減をここより後ろに足さないこと**——足した分が上限を
  押し戻して「1発は Cap を超えない」が守られなくなる。逆に、上限の外側で効かせたい資源
  （破片）は**この行より前**に置く。
- 状態異常は `StatusKeys` のカウンタで持ち、`TickStatuses` がターン開始時にまとめて処理する。新しい状態異常はキーを1つ足して `TickStatuses` に処理を書くだけでよく、特性側は「カウンタを積む」だけになる。**キーは `StatusKeys.All` にも必ず足す**（会戦が部隊戦の境界で消す一覧。漏らすとその状態異常だけが会戦を跨ぐ）。
- **`TickStatuses` に何も足さないキーもある。** 傷（`Wound`・第28期）は時間で進行せず、読み手（抉り）がいて初めて意味を持つ純粋な記録で、そこが毒との分岐点。**「状態異常＝勝手に削るもの」ではない。**
  供給は `OnAfterAttack`（**主目標のみ・攻撃1回に1度**）なので1ターン1つに構造的に限られ、伸びはターン数に対して線形になる。**非線形を止めているのは係数ではなく engine の呼び出し回数**なので、特性側で回数を数える必要はない。
  **量に比例させないこと**——ドルガの38もキリの1も等価に傷1。比例させた瞬間に「強い駒がもっと強くなる」乗算になる（README「増幅は必ず加算にする」の物理版）。
  読み手は**四役**（第39期）: 維持攻＝抉り（消費しない）／消費＝断ち（全消費・閾値2）／
  **維持防＝縫い（1つ消費して味方を回復。`ctx.Heal` を通るので渇きに課税される）**／
  供給側の自給＝刻み。**枯らすのは消費量ではなく周期**——1つしか消費しない縫いが、
  全消費の断ちより徹底して供給を刈る（毎ターン確実に1つ塞ぐため）。
  **縫いと断ちは同居できない**（塞ぎ 1/T が供給 1/T と等速で在庫の天井が 1 に固定され、
  断ちの閾値 2 に構造的に届かない＝取り合いではなく飢餓）。経緯は design/PHASE39_SUTURE.md。
- 会戦（`Engagement.cs`）は Battle を連結し、勝った側の生存駒を持ち越す。境界で `StatusKeys.All` と `AtkBonus` を一律に消し、持ち越したい状態は各特性の `Trait.OnCarryOver` が再構成する（エンジンはホワイトリストを持たない。`Counters` のキーは特性の私有物）。戦闘中に湧いた駒（胞子）は持ち越さない。判断の全文は design/ENGAGEMENT_PLAN.md。
- `LivingMembers` は必ずスナップショット（`ToList`）を返す。特性の中から召喚・蘇生が呼ばれるので、遅延評価のままだと列挙中に盤面が変わって落ちる。
- 特性の発動（`OnAfterAttack`）は攻撃1回につき1度、主目標に対してのみ。範囲攻撃のたびに複数回発動させると範囲パターンの駒が即座に壊れる。

### 決定性

`BattleEngine.Run(player, enemy, seed, verbose)` は seed 決定的で副作用も外部依存もない。行動順は速さ降順 → チーム → スロットで安定ソートしてある。BattleSim はこれを前提に seed を振って勝率を測る。`verbose: false` はログを作らないので一括シミュレーションが速い。

### 隊列と攻撃パターン（Models.cs）

スロットは9つ。**編成枠は 0-4 の5つで、プレイヤーはここにしか置けない**（0=前1・1=前3 が前列、2=中央、3=後1・4=後3 が後列）。**5-8 は召喚専用**（5=○中1・6=○中3・7=○前2・8=○後2）で、`Summon` がこの並び順に埋める。

盤面はX字で、レーンは2本。レーン0={前1,中央,○中1,後1}・レーン1={前3,中央,○中3,後3} と**奥行きが等しい**。中央は両方のレーンに属するので、スロットからレーンは単数で引けない（`LanesOf` を使う。旧 `LaneOf` は無い）。貫きはレーンを前から走り、1体貫くごとに威力が25%落ちる。○中X に召喚駒が立っていればもう1段減衰する。

隣接は**表**（`AdjacencyTable`）で持つ。幾何計算で導出しない——前1と後1は隣接するが貫き経路では間に中央が入るので、「同じ列の左右」と「同じレーンの前後」の和には分解できない。編成枠だけを見れば角4つ（前1・前3・後1・後3）は全員が次数2で等価、中央だけが次数4。薙ぎの巻き込みは別表（`SweepTargets`）で、「標的と同じ列の全員＋中列」の**非対称**な対応（前1を薙げば中央まで届くが、中央を薙いでも前列へは戻らない）。旧 `AreLateralNeighbors` / `AreDepthNeighbors` は無く、`AreSameRowPair`（横）と `IsLanePredecessor`（前）に分かれている。

**逃亡・後退の行き先は `PlayableSlotsOfRow` を使うこと。** `SlotsOfRow` は召喚枠まで返すので、空の○中1へ逃げ込んで誰も押しのけない＝逃亡が純粋な利益になる。

`AttackPattern` は Single / Sweep / Pierce / All の4つで、**増やしても4つまで**。1つ増えるたびに庇う・標的・巻き込みなど既存の全特性との相互作用を監査する必要がある。庇う・標的の介入は Single にしか効かない（薙ぎ・全体は止められず、貫きはレーン単位で解決されて割り込めない）という非対称が設計の中核。編成の定義は `Formation.Build`（名前付き引数）で書く。

配置を決めるときは人手の勘ではなく `layout` モードで測る。編成の狙い（隣接ペア・後列必須など）と探索1位が食い違ったら狙いを優先し、理由をコメントに残す。

### ログ（LogKind）

`LogLine` は `LogKind` を持ち、UI は種類で色を引くだけで文字列は一切解析しない。新しい種類を足すときは `LogKind` に列挙子を追加し、`MainWindow.xaml.cs` の `Palette` に1行足す。見せ場（`Highlight` = 破裂・覚醒）だけを浮かせ、それ以外は静かに保つ。

### 構造化イベント（BattleEvent）

`LogLine`（人が読む文字列）と対に、`BattleEvent`（機械が読む記録）が `BattleResult.Events` に入る。
戦闘画面は「誰が誰に何をしたか」を必要とするが、文字列からは復元できないので分けてある。
**文字列を解析して画面を作らないこと**（LogKind の原則と同じ）。

- 駒を指すのは `UnitState.InstanceId`（`BattleContext.Add` が振る連番）。胞子のように同じ
  `UnitDef` の駒が複数立つので、`Def.Id` では駒を指せない。増援・蘇生も必ず `Add` を通す。
- **イベントを積む処理は盤面を一切変えてはいけない。** 変えた瞬間、verbose の有無で戦闘結果が変わる。
  受け入れ確認は「`compare` の差分がゼロであること」。1ptでも動いていたら挙動を変えている。
- ログと同じく `verbose=false` では積まない（compare / layout は数百万戦を回すので確保だけで効く）。
- 見せ場は `ctx.Log(..., LogKind.Highlight)` が自動で `Highlight` イベントも流す。特性側は
  今まで通り Log を呼ぶだけでよく、演出の差し込み位置が勝手に台本へ乗る。
- 継続効果（毒・燃焼・痺れ・標的）の**残量**は `StatusSnapshot` で、ターン開始の
  `TickStatuses` 直後に1回だけ写す。カウンタは16箇所から書かれていて、書き込み側すべてに
  通知を挟むと Traits.cs を広く触ることになる（バランスが載っている場所なので触らない）。
  `Status`（そのターン働いた量）とは意味が違うので種類を分けてある。
  **ターン中に積まれたぶんは次のターンの頭まで出ない**が、効き始めるのもそのときなので揃っている。
- 攻撃力の現在値（`CurrentAttack`）も同じ場所で `StatSnapshot` として写す。積み上げ系は
  素の値から大きく離れるので（墓守は層の三角数で伸び、実測で 5 → 35 → 64）、
  素の値だけ見せると盤面で何が起きているか読めない。

## 設計判断の蓄積

**現在のバランス状況は `docs/balance.md`**（代表編成27通り × 全ステージの勝率）。数値をいじる前にまずここを見て、どの系統が壊れているかを把握する。ユニットと特性の現物一覧は `docs/units.md`。どちらも BattleSim の出力なので、コードと必ず一致している。

README.md の「調整メモ」「検証で分かったこと」「未解決の課題」に、バランス調整の理由と過去の失敗例が蓄積されている。数値や特性をいじる前に必ず読むこと（例: 増幅は必ず加算にする — 乗算にしたら毒が発散して戦闘が30ターン上限に張り付いた）。
