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
    dotnet run --project BattleSim -c Release 0 layout      # 代表編成の全配置総当たり（並列・決定的、1コアで約19分）
    dotnet run --project BattleSim -c Release 0 reseat [絞り込み] [skip] [take]  # 配置候補を seed 200 で測り直す
    dotnet run --project BattleSim -c Release 0 confirm     # 差し替え候補を別 seed で追試する
    dotnet run --project BattleSim -c Release 0 chain > docs/chain.md    # 勝率だけでは見えない「連鎖の深さ」（最大同時撃破数・決着ターン数）
    dotnet run --project BattleSim -c Release 0 ablate [絞り込み] > docs/ablation.md  # 編成から1体ずつ抜いた勝率変化（入れ得の検出）
    dotnet run --project BattleSim -c Release 0 pulse [絞り込み] > docs/pulse.md      # 駒ごとの活動量（振/干渉）と与被ダメージの内訳
    dotnet run --project BattleSim -c Release 0 engage [絞り込み] > docs/engage.md    # 会戦（部隊列3本: 順路・逆順・地点）の突破分布・入場戦力
    dotnet run --project BattleSim -c Release 0 engage2 [絞り込み]  # 同一編成2部隊の会戦（診断用。docs/ に置かない）
    dotnet run --project BattleSim -c Release 0 seats [絞り込み]    # 会戦の隊列持ち越し診断（診断用。docs/ に置かない）
    dotnet run --project BattleSim -c Release <n> demo      # 固定編成1戦の詳細ログを表示
    dotnet run --project BattleSim -c Release <n> replay "編成名" <seed>  # 1戦を再生用JSON（台本）で吐く

`layout` は「どう置くか」の粗い当たりを付ける道具で、その値で採否を決めてはいけない。
seed 50 の 720通りの最大なので上位は運で入れ替わり、狙い（ガルド前列・セッキ後列）も無視する。
`reseat` で狙いを満たす候補を含めて測り直し、`confirm` で選定に使っていない seed に当てて採否を決める。
`reseat` の第1引数はカンマ区切りの部分一致（省略で compare の全編成）。`skip` / `take` で更に切り出せる。
長時間ジョブを分割して回すためのもの（下記）。

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

`engage` は会戦（`EngagementEngine`）で測る。compare が5波を独立した5戦として測るのに対し、
勝った部隊は生存駒の HP・最大HPの損耗・蘇生回数・墓守の層(-1) を持ち越して次の波と戦う。
状態異常と攻撃力の一時変動は波の境界で消える。**`突破率` と `独立積`（独立勝率の積）の差が
会戦導入の効き目そのもの。** 部隊列は `EnemyCatalog.Columns` の3本（順路＝既存5波・
逆順＝強い波が先頭・地点＝先頭3波）を1回の実行で全部測り、1ファイルに列ごとの節で出す。
`第1削り` は勝てない編成の価値（特攻隊）を測る列で、**逆順で読む**（順路は第一波が
全編成必勝で一律 100% になり無情報）。`入場戦力` は各部隊戦に入る時点の生存数と HP割合
（分母は編成全体の定義上総最大HP）で、壁がどの戦いに、どんな消耗で立っているかを示す。
`engage2` は同一編成を2部隊にした会戦で、突破数の非線形性（第2部隊が削り残しを拾えるか）を見る。
`seats` は会戦の隊列持ち越し診断。第2戦・第3戦の入場スロットが初期配置からどれだけずれているかを
編成ごとに集計する（D5「Slot は維持」が移動系編成に課す代金の可視化。同定は UnitId で行う）。

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
    docs/           BattleSim が吐く生成物（balance.md / units.md）。手で編集しない
    design/         設計文書（コンセプトメモ・会戦計画）。生成物ではないので手で編集する

- **BattleCore に UI の参照を足さない**。`INotifyPropertyChanged` も `ObservableCollection` も不可。本番を Godot / Unity にする場合にそのまま持っていくため。
- **PrototypeApp に戦闘ルールを書かない**。ViewModel やコードビハインドにダメージ計算が漏れた瞬間に移植できなくなる。
- **`Def.Pattern` を直接読まない**。必ず `UnitState.CurrentPattern` を経由する（特性が状況でパターンを書き換えるため）。

## アーキテクチャ

### 特性 = イベントハンドラ（Traits.cs）

特性はすべて `Trait` を継承した「戦闘イベントへの反応」。`OnBattleStart` / `OnTurnStart` / `OnDamaged` / `OnDeath` / `OnMoved` などの virtual フックを上書きする。イベント駆動にしてあるので、意図していない組み合わせでも勝手に噛み合う。それが狙い。

- 追加手順: `Trait` 継承クラスを書く → `TraitId` に列挙子を足す → `TraitCatalog` の配列に登録する。
- **Trait インスタンスは全ユニットで共有されるシングルトン**。インスタンスフィールドで状態を持ってはいけない。ユニットごとの状態は `UnitState.Counters`（文字列キーの int カウンタ）に置く。
- 調整用の数値は各 Trait の `public const` に置く（`BattleEngine` 側からも参照される）。

### BattleContext = 盤面への唯一の窓口（BattleEngine.cs）

- `ApplyDamage` がダメージ処理の単一窓口。敵の攻撃も味方の巻き込みも生贄もここを通るので、「被弾で強くなる」駒がどれにも等しく反応する。味方全体に効く効果（惨禍・据え・散開・萎縮・分かち）は駒の特性側ではなく `ApplyDamage` の中で解決する。
- 死亡通知の順序は固定: killer の `OnKill` → 本人の `OnDeath`（分裂など）→ 全員の `OnAnyDeath`（墓守）→ 味方の `OnAllyDeath`（蘇生）。「墓守が強化を得た後に蘇生が走る」という順序依存がある。
- 反撃は `ctx.Reaction(...)` で包む。包まないと反撃が反撃を呼んで無限に落ちる。
- ターン外の割り込み攻撃（軋み）は `ctx.Interrupt(...)` で包む。割り込みの中で起きた移動が更なる割り込みを生む再入を止める。反撃とは別の連鎖なので `Reaction` とは別フラグ。再入禁止フラグを Trait の static に置かないこと（Trait は共有シングルトンで、layout モードは戦闘を並列実行する）。
- **割り込み（庇う・後備え・標的）はすべて `SelectTarget` で働く。主目標を差し替えるだけ**なので、範囲攻撃の巻き込み（`PerformAttack` が個別に `ApplyDamage` する）には触れない。貫きは `ResolvePierce` がレーンを直接走るので標的選択自体を通らない。範囲に対処する駒は damage の層（`ApplyDamage` / `OnDamaged`）に置くこと。範囲かどうかは `source.CurrentPattern != Single` で取れるので引数を増やす必要はない（毒・燃焼は `source` が null なので自然に外れる）。
- 肩代わり（分かち・巨躯）を `ApplyDamage` に足すときは **`u != source` を必ず入れる**。自分が出どころのダメージまで肩代わりすると打ち消しになる。巨躯で実際に踏んだ（大喰らいの吸いを壁が9割引き受けて、代金が消えていた）。症状は `pulse` の `被(味)` が不自然に小さくなること。
- 破片（`StatusKeys.Armor`）は HP の前に削られるプール。**回復とは別資源**で、`ctx.Heal` が見る `AcceptsSupport` を通らないので `Stoic`（ガルド）にも届く。「1発を完全に吸う」ではなく超過分を素通りさせるプールにしてあるのは、二値にすると README の浄化と同じ「引き算は崖」の穴に落ちるため。
- 状態異常は `StatusKeys` のカウンタで持ち、`TickStatuses` がターン開始時にまとめて処理する。新しい状態異常はキーを1つ足して `TickStatuses` に処理を書くだけでよく、特性側は「カウンタを積む」だけになる。**キーは `StatusKeys.All` にも必ず足す**（会戦が部隊戦の境界で消す一覧。漏らすとその状態異常だけが会戦を跨ぐ）。
- 会戦（`Engagement.cs`）は Battle を連結し、勝った側の生存駒を持ち越す。境界で `StatusKeys.All` と `AtkBonus` を一律に消し、持ち越したい状態は各特性の `Trait.OnCarryOver` が再構成する（エンジンはホワイトリストを持たない。`Counters` のキーは特性の私有物）。戦闘中に湧いた駒（胞子）は持ち越さない。判断の全文は design/ENGAGEMENT_PLAN.md。
- `LivingMembers` は必ずスナップショット（`ToList`）を返す。特性の中から召喚・蘇生が呼ばれるので、遅延評価のままだと列挙中に盤面が変わって落ちる。
- 特性の発動（`OnAfterAttack`）は攻撃1回につき1度、主目標に対してのみ。範囲攻撃のたびに複数回発動させると範囲パターンの駒が即座に壊れる。

### 決定性

`BattleEngine.Run(player, enemy, seed, verbose)` は seed 決定的で副作用も外部依存もない。行動順は速さ降順 → チーム → スロットで安定ソートしてある。BattleSim はこれを前提に seed を振って勝率を測る。`verbose: false` はログを作らないので一括シミュレーションが速い。

### 隊列と攻撃パターン（Models.cs）

スロットは6つ（0-2 が前列、3 が中衛、4-5 が後列）。レーンは3本で、レーン0={前1,後1}・レーン1={前2,中,後2}・レーン2={前3}と奥行きが違う。貫きはレーンを前から走り、1体貫くごとに威力が25%落ちる。`AttackPattern` は Single / Sweep / Pierce / All の4つで、**増やしても4つまで**。1つ増えるたびに庇う・標的・巻き込みなど既存の全特性との相互作用を監査する必要がある。庇う・標的の介入は Single にしか効かない（薙ぎ・全体は止められず、貫きはレーン単位で解決されて割り込めない）という非対称が設計の中核。編成の定義は `Formation.Build`（名前付き引数）で書く。

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
