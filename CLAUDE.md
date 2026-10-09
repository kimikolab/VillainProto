# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**原則（第259期）: 毎セッション必ず要るもの以外はここに置かない。履歴は `design/` へ、本文は索引で引く。**
上限は **500 行 / 50 KB**（`audit` が門にする）。期が `CLAUDE.md` に書けるのは
**(a) 変わった規則 と (b)「現状値」ブロックの差し替え** の2つだけ。
期の一行は `design/PHASE_INDEX.md` の末尾へ、測定の中身・予測の○×・踏んだ穴は `design/PHASEnnn_*.md` と `design/LESSONS_*.md` へ、
engine の窓口に足したものは `design/ENGINE_HOOKS.md` へ、新しいコマンドは `design/COMMANDS.md` へ書く。
**「n本目」「第nn期に足した」をここに書かない**——数は生成物（`docs/rules.md` / `docs/watch.md` / `docs/units.md`）で引く（R151）。

## 概要

「捨てられた駒に役割を与えて噛み合わせる」編成が面白いかどうかだけを確かめる実験装置（オートバトラーのプロトタイプ）。グラフィックや演出は対象外。コメント・ユニット名・ログはすべて日本語で書かれており、追加するコードもそれに合わせる。

## 構成と絶対のルール

    BattleCore/     戦闘ロジック。net8.0 素のクラスライブラリ。UI を一切参照しない
    BattleSim/      コンソール総当たりシミュレータ（テスト代わり）。
                    新しいモードは `Program.cs` ではなく `BattleSim/Modes/<名前>.cs` に
                    `static class <名前>Diag { public static void Run(string[] args, int stageIndex) }` で足し、
                    `Program.cs` に書くのは振り分けの1行だけ。共有ヘルパは `BattleSim/Common.cs`（`using static Common;`）
    PrototypeApp/   WPF (net8.0-windows)。編成を組んで結果を眺めるだけ
    GodotApp/       Godot 4 (C#) の戦闘再生装置。sln には入っておらず単独ビルド。台本を再生するだけで判定はしない。
                    会戦（`EngagementEngine`）を目で見られるのはここだけ
    DemoApp/        Godot 4 (C#) のデモ。sln には入っておらず単独ビルド。編成 → 戦闘の 2.5D 再生と、
                    検証用マップ 1-1（`Map11*.cs`・編成画面の「作戦マップへ」）。**`BattleCore` をそのまま参照し、写しを持たない。**
                    進行の規則は `Map11State` の1箇所で、画面（`Map11Main`）は判定を1つも持たない。
                    `Map11.cs` / `Map11Orders.cs` には Godot の型が1つも入っていない（頭なしの門 `--map11-verify` が同じクラスを回す）。
                    再生側が知らない `BattleEventKind` は素通りする。経緯は `design/DEMOAPP_HISTORY.md`
    docs/           BattleSim が吐く生成物（balance / units / chain / ablation / pulse / engage / layout / reseat /
                    crossing / rules / watch / quality / stock / harm / roster_audit / elite）。**手で編集しない。** 整合は `audit` で見る。
                    `units.md` の列は末尾側に足す（`checkup check` が 2〜4 列目を位置で読む）。
                    `balance.md` に節を足してはいけない（行を位置で読む自己検査が壊れる——だから `quality.md` は別ファイル）。
                    ファイルごとの由来は `design/DEMOAPP_HISTORY.md`
    design/         設計文書（コンセプトメモ・指示書・測定報告・則の本文・履歴）。手で編集する

**`docs/` は生成物のみ・手書き文書は `design/`。** 測定報告や指示書を `docs/` に置かない。

- **BattleCore に UI の参照を足さない**。`INotifyPropertyChanged` も `ObservableCollection` も不可。本番を Godot / Unity にする場合にそのまま持っていくため。
- **PrototypeApp / DemoApp に戦闘ルールを書かない**。ViewModel やコードビハインドにダメージ計算が漏れた瞬間に移植できなくなる。
- **`Def.Pattern` を直接読まない**。必ず `UnitState.CurrentPattern` を経由する（特性が状況でパターンを書き換えるため）。

## アーキテクチャ

**窓口ごとの「第nnn期に何を足したか」は `design/ENGINE_HOOKS.md`**（この節から逐語で移した）。ここには窓口の名前と不変条件だけを置く。
窓口の本数・札の保持者の数はそこにも書かない——`docs/rules.md`（ノブ）・`docs/units.md`（札と保持者）・`docs/watch.md`（出来事の窓口）で引く。

### 特性 = イベントハンドラ（Traits.cs）

特性はすべて `Trait` を継承した「戦闘イベントへの反応」。`OnBattleStart` / `OnTurnStart` / `OnDamaged` / `OnDeath` / `OnMoved` などの virtual フックを上書きする。イベント駆動にしてあるので、意図していない組み合わせでも勝手に噛み合う。それが狙い。

- 追加手順: `Trait` 継承クラスを書く → `TraitId` に列挙子を足す → `TraitCatalog` の配列に登録する。
- **Trait インスタンスは全ユニットで共有されるシングルトン**。インスタンスフィールドで状態を持ってはいけない。ユニットごとの状態は `UnitState.Counters`（文字列キーの int カウンタ）に置く。再入禁止フラグも Trait の static に置かない（`layout` は戦闘を並列実行する）。
- 調整用の数値は各 Trait の `public const` に置く。**版の切り替えは駒の `Traits` の配列を差し替える**（マイナスは別の `TraitId` に切り出す。`Run` の引数は増やさない——増やすと `docs/rules.md` の既定値の列が動く）。
- `InstanceId` を `Counters` に持つ記憶は `OnCarryOver` で**必ず捨てる**（戦闘ごとに振り直される）。
- 能動的な機構は手番（`OnAction`・`Actions = [Skill]`）に置く。`OnTurnStart` は「全員より先」という speed = ∞ の席で、粛（`Hush`）にも封じられない。**手番で撃つが相手がいなければ殴る**は `OnAction` の中で `ctx.PerformAttack` を直に呼ぶ形（`CanAct` を偽にすると `IdleTurn` が立ち、号令・据えが無償で買い取る）。
- **レリック（第270期・第273期に機構として採用）は編成の枠に付ける札**（`Formation.SetRelic`・1枠1枚・札は `RelicCatalog` だけ）。**札を足す・作り替えるときは `design/RELIC_MEASURES.md` の物差しで測る**（まとめは `design/RELIC_SUMMARY.md`）。`Materialize` が `UnitState.Traits` の**末尾**に足し、**`Def.Traits` には入れない**（説明文・ロスター・転生の評価は素の駒のまま）。素の札と同じ札は積まない。**レリックの測定結果を転生の評価に書き戻さない。**
- **手番に何発振るかを書き換える窓口は `Trait.ModifyHitCount` の1本**（問うのは `SwingTurn` の1箇所＝手番の中だけ。反撃・割り込み・追い打ち・再行動は1発のまま）。上限は特性の側で掛ける。
- **盤面ルール（`Inversion` / `Drought` / `Yoke` / `Hush`）だけは例外で、判定が engine 側にある**（全員に一度にかかる状態は駒ごとのフックで書けない）。Trait 本体はログを出すだけで、**保持者がいなければ完全に不活性**（`compare` 差分ゼロで確認してから盤面に載せる）。保持者の走査は既存条件の後ろ（`&&` の短絡）に置く。**止めるのは入口ではなく出口**（入口だと惨禍や脆弱が上限を押し戻す）。
- 盤面ルールを足すときは、**そのルールが触るメソッドの呼び出し元を全部数える**。駒の説明文から数えると必ず抜ける（ゴルムもリィカも「回復」と書いていないのに `ctx.Heal` を呼ぶ）。**窓口を1本足す作業には、その本数を数えている文の一覧を添える**——いまは `docs/rules.md` / `docs/watch.md` の再生成がその一覧。
- **「1発の重さ」に課金すると、課金されるのは「1発を育てる機構」で、大打点の駒ではない。** 定数の大打点は引き算にしかならず、積み上げ系には上限が天井として効く。
- 符号の違う2つの効果を1つの動作に持つ機構は、**片側だけを 0 にする対照（ノブではなく対照の札）を必ず対にして作る**。

### BattleContext = 盤面への唯一の窓口（BattleEngine.cs）

- **`ApplyDamage` がダメージ処理の単一窓口。** 敵の攻撃も味方の巻き込みも生贄もここを通るので、「被弾で強くなる」駒がどれにも等しく反応する。味方全体に効く効果（惨禍・据え・散開・萎縮・分かち）は駒の特性側ではなく `ApplyDamage` の中で解決する。
  **段の並び**: 入口の族（回避・逸らし・棘守りの上限・駒の被ダメ修正・惨禍・荷・敵の標 +50%・燃焼の脆さ）→ 軽減の族（据え・散開・萎縮・矢面・層・火の鎧）→ 肩代わり（巨躯・分かち）→ 破片 → 身構えの上限 → **軛（`YokeTrait.Cap`・`target.Hp -=` の直前）** → HP を引く → `OnDamaged` → 死亡処理 → 感電の起爆。
  **軛より後ろに新しい増減を足さない**（上限が守られなくなる）。上限の外側で効かせたい資源（破片）はこの行より前。肩代わりで分割された各段は別の `ApplyDamage` 呼び出しなので**段ごとに独立して切られる**（意図した帰結）。
  肩代わりを足すときは **`u != source` を必ず入れる**（自分が出どころのダメージまで肩代わりすると打ち消しになる）。
  「攻撃によるダメージ」と「コスト徴収」は `levy`、中継は `relayed`、刻みは `burnTick`、呪いの共有は `hexShare` の札で分ける。**肩代わりの中継は被弾の燃焼・回避の対象外**（中継は元の削りの一部）。
- **死亡通知の順序は固定**: killer の `OnKill` → 本人の `OnDeath`（分裂など）→ 全員の `OnAnyDeath`（墓守）→ 味方の `OnAllyDeath`（蘇生）。順序依存がある。連鎖する死（放電・澱みの爆発）は**幅優先**で、1つの駒は1回の連鎖で1回しか爆ぜない。
- **ターン外の行動は3つの包みで再入を止める**: 反撃は `ctx.Reaction(...)`、割り込みは `ctx.Interrupt(...)`、移動の読み手は `ctx.Shoving`。別の連鎖なので別フラグ。**粛（`Hush`）が止めるのは `CanActOutOfTurn` を通る経路だけ**で、肩代わり（ダメージの再分配）と `OnAfterAttack`（自分の手番の中）は通らない。この2つを「反応する駒」とひとくくりにすると設計が壊れる。経路は `OutOfTurnRoute` の列挙で数える。
- **標的選択の介入（庇う・後備え・標・殉教・棘守り・挑発）はすべて `SelectTarget` で主目標を差し替えるだけ**なので、範囲攻撃の巻き込み（`PerformAttack` が個別に `ApplyDamage` する）には触れない。貫きは `ResolvePierce` がレーンを直接走るので標的選択自体を通らない。範囲に対処する駒は damage の層（`ApplyDamage` / `OnDamaged`）に置く。範囲かどうかは `source.CurrentPattern != Single` で取れる。
  攻撃者側の選好（執着・断ち・見せしめ・挑発）は `SelectTargetCore` の **pool から無作為に選ぶ直前**の1段。**pool 自体は1体も足さず引かず**（「前列が生きている限り後列は狙われない」を破らせない）、効いた手番は `Roll` を引かない。候補集合は `BattleContext.TargetPool` の1箇所を選好と `CanAct` が共有する。
- **「最も傷ついた味方」の選択は `BattleContext.MostHurtAlly` の1箇所。** 止まる条件は呼び出し側に残す。失った HP の量で選ぶと固定量の回復は最大HPの大きい壁に吸われる（割合で選ぶ）。
- **弱体化（`AtkBonus` を負にする）の窓口は `BattleContext.Dull`、他者強化は `BattleContext.Whet` の1箇所ずつ。直接足し引きしない。** 窓口は横取り（集約・転嫁・横流し）の立ち位置で、`Dull` と `Whet` は統合しない（符号で意味が変わる）。**自己強化（自分の被弾を自分の出力に変える型）と持ち替え（尾灯の消灯・リリの強弱の移し）は意図的に直叩き**——他人が横取りできる形にしてはいけない。`AcceptsSupport` の判定は窓口に入れず呼び出し側に残す。
- **状態異常は `StatusKeys` のカウンタ**で持ち、`TickStatuses` がターン開始時にまとめて処理する。新しいキーは **`StatusKeys.All` にも必ず足す**（会戦が部隊戦の境界で消す一覧。漏らすとその状態だけが会戦を跨ぐ）。`TickStatuses` に何も足さないキー（傷・標・感電）もある——「状態異常＝勝手に削るもの」ではない。私有の帳簿（火勢・紅蓮・腕の累計など）は `All` に入れない私有キーにし、`OnCarryOver` で自分で消す。
- **燃焼の刻みは `BurnTickOnce`、毒は `PoisonTickOnce`、起爆は `Detonate` の1箇所**（濃縮の印・火勢の回数・被弾の燃焼はこれらを呼び直す）。熾のホタ・火の鎧・火の癒しの「焼かれない」の枝 → ベニの反転（`InverseHeal`）→ 火の変換 の順で、**切るのは `ApplyDamage` の中ではなく呼び出しの手前**（中で 0 にすると「浴びた量」を読む札が 0 で発火する）。
- 回復は `BattleContext.Heal` の入口1箇所（渇き・支援拒否 `Stoic`・反転の裏がここに立つ。`HealOutcome` を返す）。破片（`StatusKeys.Armor`）は HP の前に削られる別資源で、`Heal` を通らないので `Stoic` にも届く。**破片の減りは `UnitState.SetCounter` の1点**が `NoteArmorLost` へ流す。
- 1回の呼び出しにだけ効く札（`_deflectFrom` / `_shockNext` / `_forcedTarget` など）は**本体の最初の行で読んで消す**（引数を足さない）。保持者がいない戦は `_xxxLive` の比較1つで全部抜ける——**新しい窓口は必ずこの形**（layout は数百万戦を並列で回す）。
- **`ctx.PickOne` を新たに使わない**（候補2個以上で `Roll` を消費し、乱数列がずれて `compare` 全セルが動く）。決定的な選び方（席番号・HP 割合）で書き、乱数を引かないことを自己検査に入れる。
- **`LivingMembers` は必ずスナップショット（`ToList`）を返す**（特性の中から召喚・蘇生が呼ばれる）。
- **特性の発動（`OnAfterAttack`）は攻撃1回につき1度、主目標に対してのみ。** 範囲攻撃のたびに複数回発動させると範囲パターンの駒が即座に壊れる。
- 会戦（`Engagement.cs`）は Battle を連結し、勝った側の生存駒を持ち越す。境界で `StatusKeys.All` と `AtkBonus` を一律に消し、持ち越したい状態は各特性の `Trait.OnCarryOver` が再構成する（エンジンはホワイトリストを持たない）。**この engine の負けは全滅である**（「負けたら退く」は無い）。
- **敵の数値の倍率（`EnemyScaleRule`・採用値 115 / 115）** は `BattleEngine.Materialize` と敵が呼んだ `Summon` の2口だけで掛かる（`UnitDef.WithStats` の写しを `Def` に差す・`AtkBonus` には掛けない・`BossRule.Scale` に相乗り）。**作戦マップ（`Map11.EnemyScale = None`）には掛けない。** `EnemyCatalog` の数値は素の値のまま。

### 決定性

`BattleEngine.Run(player, enemy, seed, verbose)` は seed 決定的で副作用も外部依存もない。行動順は速さ降順 → チーム → スロットで安定ソートしてある。BattleSim はこれを前提に seed を振って勝率を測る。`verbose: false` はログを作らないので一括シミュレーションが速い。

### 隊列と攻撃パターン（Models.cs）

スロットは9つ。**編成枠は 0-4 の5つで、プレイヤーはここにしか置けない**（0=前1・1=前3 が前列、2=中央、3=後1・4=後3 が後列）。**5-8 は召喚専用**（5=○中1・6=○中3・7=○前2・8=○後2）で、`Summon` がこの並び順に埋める。
敵は `EnemyWave` / `BattleEngine.MaterializeEnemy` で9席に直接立てる（陣形は X 字のまま。会戦と診断の大半は敵を `Formation` でしか受け取らない）。

盤面はX字で、レーンは2本。中央は両方のレーンに属するので、スロットからレーンは単数で引けない（`LanesOf` を使う）。貫きはレーンを前から走り、1体貫くごとに威力が25%落ちる。
隣接は**表**（`AdjacencyTable`）で持つ。幾何計算で導出しない。薙ぎの巻き込みは別表（`SweepTargets`）で、「標的と同じ列の全員＋中列」の**非対称**な対応。
**逃亡・後退の行き先は `PlayableSlotsOfRow` を使うこと**（`SlotsOfRow` は召喚枠まで返すので、空の○中1へ逃げ込むと逃亡が純粋な利益になる）。

**陣形は隊ごとに1つ（`FormationShape`）。** 9マスは陣形に依らず同じで、変わるのは「どの5マスに編成が立つか」と隣接・薙ぎ・貫き・召喚枠の表だけ。列（前・中・後）は `RowOf` の幾何なので陣形は持たない。
**X 字（`FormationShape.X`）は `FormationRules` の静的な表をそのまま引く。** パターン2（`Diamond`・前衛1枚）とパターン3（`Spear`）は一般規則（`BuildGrid`）から表を作り、貫きは「撃つ敵のいるレーン側」（`PierceRule.Facing`・乱数を引かない）。
engine は駒を受け取る版（`FormationRules.AreAdjacent(UnitState, UnitState)` / `u.Shape.SweepTargets` など）を使う。**席番号の版は X 字の表**で、`BattleSim` の診断と `DemoApp` の画面はこちら。
枠の表示名は `FormationShape.FrameNames`（表示だけ・読んで分岐する規則は 0 件）。

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

## 規約

**器具の規約 (G1)〜(G17)。以後の指示書はここを引く。** ここには規則だけを置く——経緯と実例つきの全文（第285期まで `CLAUDE.md` にあった節を逐語で移した）は `design/CONVENTIONS_G.md`。

- **(G1)** 壊れを見る拒否権の分母は `compare` 全行（規約を作ったときは 61 行）。主判定19行に限らない——19行は軸の代表で、組み合わせの行を含まない。
- **(G2)** 拒否権3（全行版）は「壊れ」と「制約」に分ける。いずれかの波で **−10.0pt 以上**落ちた行について、その行の駒それぞれの「その駒を含む<u>他の</u>行」の全波平均の変化を出す。どの駒も |変化| < 3.0pt → **編成上の制約**（拒否しない。報告書に「この組み合わせは成立しなくなった」と書く）／ いずれかの駒で ≤ −3.0pt → **壊れ**（拒否する）。**駒が使えなくなるのは壊れ。組み合わせが使えなくなるのは設計。** 「他の行」が 0 行の駒は分解が成立しないと書く。
- **(G3)** 情報帯フィルタは**規則が駒の特性の中にあるときだけ**当てる。engine 規則の 2×2 は主判定をフィルタ無しで行い、フィルタ有りを参考に併記する。
- **(G4)** 新しい判定条件・停止条件・拒否権には、分母（`compare` 全行・50体の組・主判定19行のどれか）を必ず書く。
- **(G5)** 2×2 の A は「その機構の入力を実際に供給している駒」から選ぶ。Phase 0 で供給の内訳を出してから決める。供給の過半が engine の規則なら 2×2 を主判定にせず、`compare` 全行と交差帯 12 行を主判定にする。
- **(G6)** 副判定は常設しない。持続係数は払い出しが持続する通貨（毒・燃焼・層）に乗る機構、発火回数／稼働率は読み手を足す機構、台の乖離は理想台とドラフト台で符号が割れたときだけ出す。どれも単独では採否を決められない。出すときは決着ターン数を併記する。
- **(G7)** 紙のスループットを出すときは、分子について先に書く: 1. 線形か二次か ／ 2. 門ではなく出力 ／ 3. 分母を削るか（自分の分母を削る機構では紙は下限ではなく上限になる）。
- **(G8)** 測定の重さは3段。**甲** engine の規則 ＝ 2×2（(G5) の条件付き）＋ `compare` 全行 ＋ 交差帯12行 ＋ 自己検査フル ／ **乙** 既存駒への1機構追加 ＝ `compare` 全行（拒否権）＋ 交差帯12行 ＋ 必須4項目（2×2 は (G5) の条件を満たすときだけ）／ **丙** 新しい駒1体 ＝ `compare` 全行（拒否権）だけ・入れてみる。バランス完成段階ではないので丙は測らない。採って外れたときの戻しはノブ1行＋`docs/` 再生成で済む。
  **自己検査の必須4項目**: 1. `compare` 全セルが `docs/balance.md` と 0 件 ／ 2. `docs/` 全ファイルを再生成して差分を報告する ／ 3. 触っていないノブの既定が動いていない（`docs/rules.md` の差分で示す）／ 4. `ctx.PickOne` を新たに使っていない。
- **(G9)** 「`compare` で第五波 95% 超の行が新たに2行以上」は拒否ではなく**注意**（情報セルが減ったら報告書に記録する。それだけで機構を落とさない）。残す拒否権は **(1) 主判定19行の第五波平均が歯止めを下回る** と **(3) 主判定行がいずれかの波で −10.0pt 以上落ちる**、および (G1)(G2) の壊れ。拒否権の番号は文書でずれる（(G1)(G2) は壊れを見る側を「拒否権3（61行版）」と呼ぶ）。
- **(G10)** 判定の分母は**第2〜5波**（地点なら第2〜3波）。第一波は教習波なので分母に入れない（勝率・残存・圧勝率・全滅勝ちのすべて。参考の併記は可）。分母は事前に固定し、結果を見てから変えない。
- **紙は門ではなく出力**（第90期から規約）。Phase 0 で必ず出すが門にしない。門は「鎖が繋がっているか」: (1) その通貨を持つ相手が存在する ／ (2) その相手に書き手が書く ／ (3) 書いたものが実際に払い出される。どれかが 0 なら切れている場所を書いて閉じる。
- **(G11)** 判定の線を引く前に、対照側の実測値を見る（対照が下限・上限に張り付いていると線が自動で通る）。
- **(G12)** 割合を版をまたいで比べるときは、分母の変化を先に出す。
- **(G13)** 同値塊がある分布に順位相関を当てない。同値塊の割合を必ず併記し、割合が高いときは順位相関を判定に使わない。
- **(G14)** 情報セルは `compare` が使う帯（seed 0..199）で数える（席の追試は帯B でよい）。第89期以降に席を選んだ行への波及調査は未了。
- **(G15)** 取り込み・引用は行番号ではなく構造（見出し・箇条書きの先頭）で切り、取り込んだ文字列が原文に含まれることを機械で確かめる。
- **(G16)** 線を引く条件と採る条件は同じ集合で書く。向きは「線は緩く、採用は厳しい」。線を満たすが採る条件で落ちた数も出力に書く（効かなかったことも記録する）。
- **(G17)** `UnitCatalog.All` ／ `Retired` ／ `Presets` に触る期は、受け入れ条件に `sweep`（全診断の exit 検査）を入れる。`All` から外すのは「`Retired` へ移す」の1行で、辞書のキーや `Id` の引きは `Everyone`（`All ∪ Retired`）。**日中は `sweep fast`・full は夜間（`sweep nightly`）、朝にサマリを確認。差分があれば翌朝の最初の仕事は原因特定（`git bisect`）**（`design/SWEEP_NIGHTLY.md`）。sweep が回すのは現役網（`design/SWEEP_ROSTER.md`・加点方式）だけで、新しい器具は入れると決めたときだけ `SweepDiag.Active` に足す。合格は異常終了 0 本かつ上限 0 本（上限 150 秒）。**凍結庫**（`design/SWEEP_FREEZER.md`）の本を使う期は Phase 0 で `sweep thaw <絞り込み>` を1回走らせ、封印（`design/freezer/seals.tsv`）と照合する——不一致なら `git bisect`。

## 新機構の判定規約

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
             (2) **第100期に「注意」へ降ろした**（→ (G9)）。`compare` で第五波 95% 超の行が
                 新たに2行以上でも**拒否しない。記録するだけ**
             (3) 主判定行がいずれかの波で −10.0pt 以上落ちる

**ノイズ床は 3.26pt（フィルタ前）/ 5.61pt（フィルタ後）・N3 = 3**（第88期・A ＝ ミオ・seed 0..7）。
**この値をそのまま次の期に使わない**——下の 8-2 の理由で、**測り直すべき量である**。

## 指示書を書くときに当てる一般則

**本文と索引は `design/RULES_*.md`**（`RULES_001_097.md` / `RULES_098_173.md` / `RULES_174_242.md` / `RULES_243_.md`・索引の表は `design/RULES_INDEX.md`）。
**ID（`R001`〜）で grep すること。ID は永続で、欠番になっても再利用しない。**
新しい則は `RULES_243_.md` の末尾に次の ID（`R392` から）で本文を書き、`RULES_INDEX.md` に1行足す。既存の則が再発したときは新しい ID を作らず、本文側に `#### R0nn の再発（第nn期）` の段落を足して索引の `期` 欄に期番号を足す。**`CLAUDE.md` に本文を書かない。**

## 現状値

**期ごとの報告は `design/HISTORY_PHASES.md`**（この節から逐語で移した。以後の期はそちらの冒頭に足し、ここのブロックは差し替える）。

**最後に動かした期: 第306期**（**本編 第2波を HCD15 に規定化——巡礼騎士は斬り返し（`KnightGR`）・粛の伝令は 15 回止めたら沈黙が砕ける（`HusherHD15`）**。報告は `design/PHASE306_HUSH_REGULATE.md`）。第305期の中間の版 HCD15 ／ HCD20 の測定は `design/PHASE305_HUSH_CRACK.md`。第304期のヒサの庇い HC-s の規定化と粛の作り直しの版（HB ／ HD10 ／ HD20 ／ HC ／ HCD）は `design/PHASE304_HUSH_REWORK.md`。第303期の叫びも粛で止める ／ 身振りと庇いの版は `design/PHASE303_HUSH_HUNT.md`。第302期の指差しを粛で止める ／ 号令の玉 ／ 庇いの規定化は `design/PHASE302_HISA_REGULATE.md`。第301期のソラ ／ ザン ／ ヒサの小修正と号令 ／ 庇いの版は `design/PHASE301_HISA_COMMAND.md`。第300期のザン ZM-a ／ 矢面は羽も半分は `design/PHASE300_ROUND_GUARD.md`。第299期のミサ MF-b ／ ザン ZN-b の規定化・ヒサが自分も癒す・仇巡りの版 ZM-a ／ ZM-1 の測定は `design/PHASE299_ZAN_ROUND.md`。第298期のドハ DH-a（なまりなし）の規定化・戦績の帰属のずれ 8 群・ミサ MF ／ ザン ZN の版は `design/PHASE298_MARK_FEATHER.md`。第297期のドハの版 DH-a ／ DH-b ／ DH-t と叫びの回復(与) の帰属は `design/PHASE297_DOHA_SHARE.md`。第296期のヒサ HK-b の規定化と試遊・標の3行は `design/PHASE296_HISA_RALLY_PLAYTEST.md`。第295期のソラ SR-b の規定化とヒサの HK-a ／ HK-b の測定は `design/PHASE295_MARK_HEAL.md`。第294期のクグ KW-a ／ シガ SW-a の規定化と守りの版（ヒサ HS ／ ソラ SR ／ ソム SM）は `design/PHASE294_GUARD.md`。第293期のカタ KR-∞ の規定化と網 ／ 連鎖の鞭の版は `design/PHASE293_SHOCK_WEB.md`。第292期のクグの糸玉（盤面の駒の列の外の置物 `BattleContext.SilkBalls`）は `design/PHASE292_KUGU_SILKBALL.md`。第291期のクグ KG-b の規定化と試遊の準備は `design/PHASE291_PLAYTEST_PREP.md`（台本は `design/PHASE291_CODEX_MEMO.md`）。第290期のシガ SI-b ／ カタ KR-b の規定化とクグの糸の測定は `design/PHASE290_SHOCK_SQUAD.md`、第289期のシガの割り込み・カタの雷雲・ベニ×トウの切り分けは `design/PHASE289_SHOCK_ATTACKERS.md`、第288期のシガの蓄電の版の測定は `design/PHASE288_SHIGA_CHARGE.md`、第287期のトウの規定化（T3）と感電軸の台探索は `design/PHASE287_SHOCK_AXIS_GRID.md`、第286期のミサ M-b の規定化とトウ T3 の測定は `design/PHASE286_TOU_SPREAD.md`、第285期の改名・羽・`CLAUDE.md` の圧縮は `design/PHASE285_MISA_FEATHERS.md`、第284期の精鋭波は `design/PHASE284_ELITE_WAVE.md`、第283期のボスの標台（標軸の格子）は `design/PHASE283_BOSS_MARK_SQUAD.md`、第279期のトウの転生と凍結庫は `design/PHASE279_TOU_REBIRTH.md`、第280期の sweep 現役網は `design/PHASE280_SWEEP_REBUILD.md`。転生段の棚卸しは `design/PHASE275_ROSTER_INVENTORY.md`、レリック段のまとめは `design/RELIC_SUMMARY.md`。
**改名の対応（第285期）**: 止めのトメ → 見境なしのミサ（`Id = "tome"`・識別子 `UnitCatalog.Tome` ほかは据え置き）／ 行名 `止め (トメ×ソラ)` → `見境 (ミサ×ソラ)`・`止め改 (トメ×薙ぎ)` → `見境改 (ミサ×薙ぎ)`・`標経済 (ヒサ×ザン×トメ)` → `標経済 (ヒサ×ザン×ミサ)` ／ 精鋭の波 `精鋭・五` → `近衛`・`精鋭・九` → `大隊`。過去の `design/` は旧名のまま（名前で引く器具は `Common.UnitRenames`）。
**最後に `compare` が動いた期: 第306期**（第2波の列だけ・64 行すべてが第305期の HCD15 と一致: `標経済` 0.5 → 61.0・`継ぎ当て×被弾強化` 53.5 → 99.5 など・(G2) の対象なし）。**旧の第2波は `EnemyCatalog.Wave2H305`**（`Stages` の外）で、第305期までの第2波を測った器具（`doha297` の波・`hush303` ／ `hush304` ／ `hush305`）は `Common.StagesH305` で固定する。第304期の `compare` は段0 の HC-s で3セル（`反撃改3` 第3波 99.0 → 74.5 など）。第303期の規定のヒサは `HisaH303`（`Common.Pin304`・第303期の版 Q1 ／ Q3 ／ QA ／ HC-d ／ HC-s ／ 空の庇いもここから作る）。粛の版は第2波の敵の札（`EnemyCatalog.HusherHB` ／ `HusherHD10` ／ `HusherHD15` ／ `HusherHD20` ／ `KnightGR`）で、第306期に `HusherHD15` ／ `KnightGR` を規定の第2波に入れた。第302期の規定のヒサは `HisaH302`（`Common.Pin303`・第302期の版 HV-s ／ HB もここから作る）。第301期の規定のヒサは `HisaH301`（`Common.Pin302`・第301期の版 HL ／ HC もここから作る）。第300期の規定のソラは `SoraSRs`・ザンは `ZanZMa`・ヒサは `HisaHKf`（`Common.Pin301`・`Pin299` ／ `Pin300` もソラを `SoraSRs` に）。第299期の規定のザンは `ZanZNb`・ヒサは `HisaHKs`（`Common.Pin300` がこの2枚を固定する）。過去の器具のミサは `TomeMb`（第298期までは `Tome` の別名・いまは明示の定義）、ザンは `ZanZN0`、第296〜298期の規定のヒサは `HisaHKb`（`Common.Pin299` がこの3枚をまとめて固定する）。過去の器具のドハは `DohaD0`。過去の器具のヒサは `HisaHK0`、ヒーラーの一覧は `Boss283Diag.HealPool295`（規定のヒサを数えない）に固定してある。ソラは `SoraSR0`、シガは `ShigaG3K` ／ `ShigaSGa` ／ `ShigaSIb`、カタは `KataS3` ／ `KataKRb`、トウは `TouT0`、クグは `KuguKG0` ／ `KuguKGb`（第294期の固定は `Kugu292Diag.Pin294`・第296期にヒサも足した）。

    編成:       64 行（`CompareBuilds()`・第281期に 63 → 64）＋ 交差帯 12 行（`CrossBuilds()`）
    全64行:     100 / 90.7 / 89.3 / 83.3 / 83.3     （第1〜5波の平均勝率・seed 0..199・`spread` §4・第306期。第304〜305期は第2波 89.0）
    主判定19行: 100 / 86.3 / 89.1 / 82.2 / 81.6     （第306期・`spread` §4。第304〜305期は第2波 85.7）
    歯止め:     主判定の第五波 33.2%                 ← 余裕 +48.4pt
    情報セル:   全64行 77 / 主判定 27                （第2〜5波の 0 < x < 100 のセル数・第306期に第2波で 75 → 77）
    ロスター:   52 枚（上限 52・第103期に確定）。残り枠 0。入れ替えは 4 度（ハリ→トモ・エグ→ガレ・キリ→スス・ナタ→カタ）
    敵の倍率:   115 / 115（第187期）

**規則（動かさない）**

- **第五波の歯止めは `Baseline.PrimaryRows`（BattleSim/Program.cs）の 19 行で読む。** 全行平均は行が増えるたびに勝手に動く量なので歯止めにしない。記録は「主判定 / 全61行」の両方を必ず併記する（`spread` の §4 が出す）。
- **情報セルの定義が2つある。** `spread` §1 の中間帯は `5 < x < 95` を全5波、§4（主判定）の情報セルは `0 < x < 100` を第2〜5波。別の量なので混ぜない。情報セルは `compare` が使う帯（seed 0..199）で数える（(G14)）。
- **波を作り直すときは、同じ行数で前後を測り直して採否を決める。計測器と測定対象を同時に動かさない。** 行を足したときの平均の低下を波の難度と読み違えないこと。
- **判定に使う分母は第2〜5波**（(G10)）。第一波は全行が 100% 勝つ教習波。
- **`UnitCatalog.All` は「編成に選べる 52 枚」の定義であって `Presets` が参照できる集合ではない。** 外した駒は `Retired` へ移し、辞書のキーと `Id` の引きは `Everyone`（`All ∪ Retired`）を使う。`All` / `Retired` / `Presets` に触る期は `sweep` を受け入れ条件に入れる（(G17)）。測って棄却した駒の定義は対照として残置する（`All` にも `Presets` にも入れない）。
- **駒を転生させるときは旧の駒を `UnitCatalog.<名前><版>` に残し、その駒を使う過去の器具を旧に固定する**（台本の指紋 `shockdigest` / 各 `digest` が規定化の前後で全行一致するのが門）。
- **採用で既定が動いたら、その期のうちに診断の対照（V0）を作り直す**（放置すると V0 と V1 が同じものを指す）。
- **軸テコ入れの期は `docs/elite.md`（精鋭波・素の構成 × HP 1000% ／ 攻 300%・波ルールなし）の該当行を卒業指標として見る**（第284期 A4）。精鋭は評価軸で本編の波ではない——`Stages` / `TestStages` に載せない。

## コマンド

テストプロジェクトは無い。検証はすべて BattleSim の実行結果で行う。
**全診断モードの一覧は `design/COMMANDS.md`**（この節から逐語で移した。`sweep` はその表を読んで走らせる本を組むので、新しいモードはそこに足す）。

    dotnet build                                                        # 全体ビルド（WPF を含むので Windows のみ）
    dotnet run --project BattleSim -c Release 0 compare > docs/balance.md          # 代表編成 × 全ステージの勝率（毎期）
    dotnet run --project BattleSim -c Release 0 compare quality > docs/quality.md  # 勝ち方の質
    dotnet run --project BattleSim -c Release 0 dump > docs/units.md               # ユニット・特性・ステージ一覧（毎期）
    dotnet run --project BattleSim -c Release 0 audit                              # docs/ の整合 ＋ CLAUDE.md の行数の門（戦闘0回）
    dotnet run --project BattleSim -c Release 0 chain > docs/chain.md              # 連鎖の深さ（最大同時撃破・決着T）
    dotnet run --project BattleSim -c Release 0 derive rules > docs/rules.md       # ノブ一覧（CLAUDE.md を書き終えた後に最後に回す）
    dotnet run --project BattleSim -c Release 0 reseat [絞り込み] / confirm        # 席の測り直しと別 seed の追試（採否は confirm で）
    dotnet run --project BattleSim -c Release 0 sweep fast / nightly               # 現役網の exit 検査（第280期: fast は日中・nightly は夜間で朝のサマリ。`full` / `roster` / `check` / 凍結庫の `frozen` / `thaw` は COMMANDS.md）
    dotnet run --project BattleSim -c Release 0 sweep list                         # 走らせる一覧だけ（戦闘0回）
    dotnet run --project BattleSim -c Release <n> demo "編成名" [seed]             # 1戦の詳細ログ
    dotnet run --project BattleSim -c Release <n> replay "編成名" <seed>           # 1戦を再生用JSON（台本）で吐く
    Godot_console.exe --path DemoApp --headless -- --map11-verify[=N]             # 作戦マップの進行を頭なしで N seed 回す（第168期の5量と差 0.0pt が門）
    Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast --demo-preset=<行名> --demo-stage=<n> --demo-seed=<n>
                                                                                   # 単発の戦闘を頭なしで再生。合否は `DEMO_SMOKE_COMPLETE` の行で読む

`docs/` の全再生成は約 5 分（`roster audit` だけ 130 秒・毎回は回さない）。
DemoApp の頭なしの門（`--map11-flow-smoke` など）は**終了コードが 0 にならない**ので、合否は `*_COMPLETE` の行で読む。

### バランス調整のたびにやること（CONTRIBUTING.md より）

1. 数値や特性を変える
2. `... 0 compare > docs/balance.md` で勝率を測り直す（飛ばすと勝率表が嘘になる）
3. `... 0 dump > docs/units.md` で一覧を吐き直す（飛ばすと説明文と挙動がずれる。過去3回発生）
4. `git diff docs/` で何が動いたかを確認する
5. docs/ の差分も含めてコミットし、動いた行をコミットメッセージにも書く

`docs/` の2ファイルは BattleSim の出力そのもの。**手で編集しない**（次の生成で消える）。
差分が出ないこと自体が「触ったがバランスは動いていない」という情報になるので、
変えていないと思っても必ず測り直す。

## 設計判断の蓄積

**現在のバランス状況は `docs/balance.md`**（代表編成27通り × 全ステージの勝率）。数値をいじる前にまずここを見て、どの系統が壊れているかを把握する。ユニットと特性の現物一覧は `docs/units.md`。どちらも BattleSim の出力なので、コードと必ず一致している。

README.md の「調整メモ」「検証で分かったこと」「未解決の課題」に、バランス調整の理由と過去の失敗例が蓄積されている。数値や特性をいじる前に必ず読むこと（例: 増幅は必ず加算にする — 乗算にしたら毒が発散して戦闘が30ターン上限に張り付いた）。

## 知見の索引

**各期の実測と経緯は `design/LESSONS_*.md` にある**（第112期に `CLAUDE.md` から逐語で移した）。
**則は移していない**——規約は「規約」節、毎期当てる則は「指示書を書くときに当てる一般則」節にそのまま残っている。

| ファイル | 帯 | 何が確定した帯か／いつ読むか |
|---|---|---|
| `design/LESSONS_001_040.md` | 第1〜40期 | 診断の作り方と波ルール・傷軸の立ち上げ・弱体の窓口。**器具そのものを疑うとき** |
| `design/LESSONS_041_060.md` | 第41〜60期 | 隣接と席・通貨の棚卸し・標の書き手と読み手・強化と燃焼の窓口。**駒や機構を1つ足すとき** |
| `design/LESSONS_061_080.md` | 第61〜80期 | 素体対照・情報帯・ドラフト台・2×2 の相乗・傷軸の代金の分解。**帰属の測り方を決めるとき** |
| `design/LESSONS_081_100.md` | 第81〜100期 | 規約 (G1)〜(G10) が出た帯。巻き込み則・滲み則・呪い則を採り、棘の傷・深手・呪いを落とした。**採否の線を引くとき** |
| `design/LESSONS_101_.md` | 第101期〜 | 会戦の境界・手番という通貨・再行動・背かれ・尾灯・段（格上げ）・**害の帳簿**。**いま盤面がどうなっているかを知るとき**。以後の期はここに追記する |

### 主題別・期別

**主題 → 期 の表と、期ごとの一行（第84期以降）は `design/PHASE_INDEX.md`**（この節から逐語で移した。以後の期はその末尾に足す）。
**第1〜83期は各 `LESSONS_*.md` の冒頭の目次から引く**（期 → 診断名の表を各ファイルが持っている）。
