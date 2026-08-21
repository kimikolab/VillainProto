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
    dotnet run --project BattleSim -c Release 0 reseat [skip] [take] > docs/reseat.md  # 配置候補を seed 200 で測り直す
    dotnet run --project BattleSim -c Release 0 confirm     # 差し替え候補を別 seed で追試する
    dotnet run --project BattleSim -c Release <n> demo      # 固定編成1戦の詳細ログを表示

`layout` は「どう置くか」の粗い当たりを付ける道具で、その値で採否を決めてはいけない。
seed 50 の 720通りの最大なので上位は運で入れ替わり、狙い（ガルド前列・セッキ後列）も無視する。
`reseat` で狙いを満たす候補を含めて測り直し、`confirm` で選定に使っていない seed に当てて採否を決める。
`reseat` は `skip` / `take` で対象を切り出せる。長時間ジョブを分割して回すためのもの（下記）。

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
    docs/           BattleSim が吐く生成物（balance.md / units.md）。手で編集しない

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
- 状態異常は `StatusKeys` のカウンタで持ち、`TickStatuses` がターン開始時にまとめて処理する。新しい状態異常はキーを1つ足して `TickStatuses` に処理を書くだけでよく、特性側は「カウンタを積む」だけになる。
- `LivingMembers` は必ずスナップショット（`ToList`）を返す。特性の中から召喚・蘇生が呼ばれるので、遅延評価のままだと列挙中に盤面が変わって落ちる。
- 特性の発動（`OnAfterAttack`）は攻撃1回につき1度、主目標に対してのみ。範囲攻撃のたびに複数回発動させると範囲パターンの駒が即座に壊れる。

### 決定性

`BattleEngine.Run(player, enemy, seed, verbose)` は seed 決定的で副作用も外部依存もない。行動順は速さ降順 → チーム → スロットで安定ソートしてある。BattleSim はこれを前提に seed を振って勝率を測る。`verbose: false` はログを作らないので一括シミュレーションが速い。

### 隊列と攻撃パターン（Models.cs）

スロットは6つ（0-2 が前列、3 が中衛、4-5 が後列）。レーンは3本で、レーン0={前1,後1}・レーン1={前2,中,後2}・レーン2={前3}と奥行きが違う。貫きはレーンを前から走り、1体貫くごとに威力が25%落ちる。`AttackPattern` は Single / Sweep / Pierce / All の4つで、**増やしても4つまで**。1つ増えるたびに庇う・標的・巻き込みなど既存の全特性との相互作用を監査する必要がある。庇う・標的の介入は Single にしか効かない（薙ぎ・全体は止められず、貫きはレーン単位で解決されて割り込めない）という非対称が設計の中核。編成の定義は `Formation.Build`（名前付き引数）で書く。

配置を決めるときは人手の勘ではなく `layout` モードで測る。編成の狙い（隣接ペア・後列必須など）と探索1位が食い違ったら狙いを優先し、理由をコメントに残す。

### ログ（LogKind）

`LogLine` は `LogKind` を持ち、UI は種類で色を引くだけで文字列は一切解析しない。新しい種類を足すときは `LogKind` に列挙子を追加し、`MainWindow.xaml.cs` の `Palette` に1行足す。見せ場（`Highlight` = 破裂・覚醒）だけを浮かせ、それ以外は静かに保つ。

## 設計判断の蓄積

**現在のバランス状況は `docs/balance.md`**（代表編成27通り × 全ステージの勝率）。数値をいじる前にまずここを見て、どの系統が壊れているかを把握する。ユニットと特性の現物一覧は `docs/units.md`。どちらも BattleSim の出力なので、コードと必ず一致している。

README.md の「調整メモ」「検証で分かったこと」「未解決の課題」に、バランス調整の理由と過去の失敗例が蓄積されている。数値や特性をいじる前に必ず読むこと（例: 増幅は必ず加算にする — 乗算にしたら毒が発散して戦闘が30ターン上限に張り付いた）。
