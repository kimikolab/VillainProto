using BattleCore;
using static Common;

// =====================================================================================
// divert モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "divert")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 divert
// =====================================================================================

static class DivertDiag
{
// divert モード: 逸らし（第50期）。**標（`Marked`）を操作可能にする。**
//
// 標は engine が常時読んでいる強い通貨（`MarkPullPercent` = 75）なのに、
// 盤面での操作手段が存在しなかった——書き手はヒサ1枚・開戦時1回・選択の余地ゼロ、
// **消す経路は1つも無い**（第49期 Phase 0-3）。
//
// `CompareBuilds()` / `Stages` / `Columns` は触らない（phase0 は BattleCore にも触らない）。
//
//     dotnet run --project BattleSim -c Release 0 divert phase0   # 実装前の地図（§2 Phase 0）
public static void Phase0(string[] args, int stageIndex)
{
    var dvBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> dvStages = EnemyCatalog.Stages;
    const int DvScan = 100;   // verbose=true で回すので compare の 200 とは分ける

    Console.WriteLine("# 逸らし Phase 0 —— 標という通貨の地図（第50期 §2）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 divert phase0` の出力。");
    Console.WriteLine("**盤面は1つも動かない。** `BattleCore` には1文字も足していない状態で走らせる測定で、");
    Console.WriteLine("`docs/` には置かない。");
    Console.WriteLine();

    // --- 0-1. engine 側の窓口一覧 -------------------------------------------------------------
    // 第48期・第49期の残件B。**2度当たっているのでここで固定する。**
    // 表は grep と目視で作る（census と同じ作法——Trait に属性を足すと判定の根拠が
    // 「誰かが属性を正しく付けたか」に化けて grep で検算できなくなる）。
    Console.WriteLine("## 0-1. 通貨の読み手 —— **駒の `Trait`** と **engine の窓口** を分けて数える");
    Console.WriteLine();
    Console.WriteLine("第48期の棚卸しは**駒**の読み手だけを数えていた。第49期はそれを根拠に");
    Console.WriteLine("「敵に標を付けても読み手がいないので効かない」と予測して外した——**engine が読んでいた**。");
    Console.WriteLine("**通貨の読み手は「駒の `Trait`」と「engine の窓口」の和である。**");
    Console.WriteLine();
    Console.WriteLine("| 通貨 | engine の窓口（ファイル:行） | 何をするか | 駒の読み手 |");
    Console.WriteLine("|---|---|---|---|");
    Console.WriteLine("| **標** `Marked` | `BattleEngine.cs:1152` | **`SelectTargetCore` の鎖。`MarkPullPercent`(75)% で主目標を標持ちへ差し替える** | 仇討ち（ザン）1枚 |");
    Console.WriteLine("| **痺** `Stun` | `BattleEngine.cs:2269` | 行動順ループ。手番を飛ばして `IdleTurn` へ振り替える | 責め苦（シガ）1枚 |");
    Console.WriteLine("| | `BattleEngine.cs:137` | `CanActOutOfTurn`。ターン外の行動（棘・仇討ち・軋み・追い打ち）を止める | |");
    Console.WriteLine("| **毒** `Poison` | `BattleEngine.cs:174` | `TickStatuses`。層の分だけ削る（毒喰らいがいれば味方は2倍） | 澱み／疫み／毒喰らい／澱み喰い 4枚 |");
    Console.WriteLine("| **燃** `Burn` | `BattleEngine.cs:203` | `TickStatuses`。6 削って残ターンを1減らす | 熾火（ホタ）1枚 |");
    Console.WriteLine("| | `BattleEngine.cs:229` | `Ignite`。再付与は残ターンの**設定**（加算ではない） | |");
    Console.WriteLine("| **破片** `Armor` | `BattleEngine.cs:1650` | `ApplyDamage`。HP の前に削られる。**受け切ると `OnDamaged` を呼ばない** | 鱗（ウロ）1枚 |");
    Console.WriteLine("| **`IdleTurn`** | `BattleEngine.cs:1519` | 据え（バン）の被ダメ半減の判定 | 責め苦／号令 2枚 |");
    Console.WriteLine("| **傷** `Wound` | **無し** | engine は1箇所も読まない（`TickStatuses` に何も足していない） | 抉り／断ち／縫い／刻み 4枚 |");
    Console.WriteLine("| **弱体** | `BattleEngine.cs:1891` | `Dull` が唯一の窓口。集約・転嫁の横取りがここに立つ | 逆しま／引き受け／渡し 3枚 |");
    Console.WriteLine("| **位置** | `BattleEngine.cs:2100` | `SwapSlots`。`OnMoved` / `OnAllyMoved` を流す | 軋み／移り木／後衛特化／突き返し 4枚 |");
    Console.WriteLine("| **死** | `BattleEngine.cs:1783` | `OnKill` → `OnDeath` → `OnAnyDeath` → `OnAllyDeath` の固定順 | 墓守／分裂／破裂／蘇生／鱗 5枚 |");
    Console.WriteLine();
    Console.WriteLine("> **engine の窓口を持つ通貨は 9 / 10**（傷だけが持たない）。");
    Console.WriteLine("> **標は「駒の読み手1枚・engine の窓口1つ」で、engine 側のほうが強い**");
    Console.WriteLine("> ——ザンは標を持つ味方が殴られたときにしか動かないが、engine の窓口は");
    Console.WriteLine("> **すべての単体攻撃**で評価される。");
    Console.WriteLine();

    // --- 0-2. MarkPullPercent の実装 ----------------------------------------------------------
    Console.WriteLine("## 0-2. `MarkPullPercent` の実装（`BattleEngine.cs:1152`）");
    Console.WriteLine();
    Console.WriteLine("```csharp");
    Console.WriteLine("UnitState? marked = PickOne(foes.Where(f => f.Counter(StatusKeys.Marked) > 0).ToList());");
    Console.WriteLine("if (marked is not null && marked != target && Roll(100) < MarkPullPercent)");
    Console.WriteLine("{ ... return marked; }");
    Console.WriteLine("```");
    Console.WriteLine();
    Console.WriteLine("読み取れること（実装から。**この4点がこの期の設計の前提**）:");
    Console.WriteLine();
    Console.WriteLine("1. **`75` は確率であって重みではない。** ただし「既に主目標が標持ちなら引かない」");
    Console.WriteLine("   （`marked != target`）ので、**実効の被狙撃率は 75% より高い**:");
    Console.WriteLine();
    Console.WriteLine("       標持ちが pool（＝生存する最前列）にいる: 1/n + (1 − 1/n) × 0.75");
    Console.WriteLine("       標持ちが pool にいない（後列など）:        0 + 1 × 0.75 = 0.75");
    Console.WriteLine();
    Console.WriteLine("2. **`foes` から選んでいる（`pool` ではない）。** つまり標は");
    Console.WriteLine("   **「前列が生きている限り後列は狙われない」という盤面の中核規則を破る**");
    Console.WriteLine("   ——後列の標持ちは前列を飛び越して狙われる。ロスターで**標だけが持つ性質**");
    Console.WriteLine("   （執着・断ちの選好は `pool` から選ぶので破らない）。");
    Console.WriteLine();
    Console.WriteLine("3. **標持ちが複数いると `PickOne` で1体に絞ってから 75% を引く。**");
    Console.WriteLine("   引きは1回しか起きないが、**標持ちが増えると `p_t`（無作為の主目標が既に標持ちである確率）が上がる**ので、");
    Console.WriteLine("   標の集合が集める総量は `p_t + (1 − p_t) × 0.75` で**増える**。1体あたりの取り分は逆に薄まる。");
    Console.WriteLine("   ⇒ **`TargetCount` は「集中」と「被覆」を取り替えるノブ**であって、総量のノブでも純粋な分散のノブでもない。");
    Console.WriteLine();
    Console.WriteLine("4. **鎖の順序は 標 → 後備え → 庇う → 殉教 → 棘守り。標がいちばん先。**");
    Console.WriteLine("   標が引いた瞬間に `return` するので、**標は庇い・後備え・殉教をすべて飛び越す。**");
    Console.WriteLine("   ⇒ **敵に標を付けると、敵の殉教者（第五波・庇う75%）を無視して狙い撃てる。**");
    Console.WriteLine();
    Console.WriteLine("5. **単体攻撃にしか効かない**（`pattern != Single` は手前で return）。");
    Console.WriteLine("   薙ぎ・全体・貫きは標を1ビットも見ない。");
    Console.WriteLine();
    Console.WriteLine("6. **両陣営で対称。** `foes = LivingMembers(Opponent(attacker.TeamId))` なので");
    Console.WriteLine("   陣営に依存する分岐が1つも無い。**敵に標を付ければ味方の単体攻撃が引かれるはず**");
    Console.WriteLine("   （実装からはそう読める。実測は §6 の受け入れ基準3で確かめる）。");
    Console.WriteLine();

    // --- 0-3. 標を消す経路 --------------------------------------------------------------------
    Console.WriteLine("## 0-3. 標を消す経路は 1 つも無い（再確認）");
    Console.WriteLine();
    Console.WriteLine("`StatusKeys.Marked` の全出現は **5 箇所**:");
    Console.WriteLine();
    Console.WriteLine("| 場所 | 何をするか |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| `Traits.cs:1374` | **唯一の書き手**（囃し立て・`SetCounter(Marked, 1)`） |");
    Console.WriteLine("| `Traits.cs:2637` | 仇討ち（ザン）が読む |");
    Console.WriteLine("| `BattleEngine.cs:1152` | engine の窓口が読む |");
    Console.WriteLine("| `BattleEngine.cs:1157` | 第49期の計数（帰属用。分岐しない） |");
    Console.WriteLine("| `BattleEngine.cs:995` | スナップショットの表示名 |");
    Console.WriteLine();
    Console.WriteLine("**`SetCounter(StatusKeys.Marked, 0)` は grep で 0 件。**");
    Console.WriteLine("会戦の境界では `StatusKeys.All` の一律掃除で消えるが、**1戦の中では永続。**");
    Console.WriteLine("⇒ **この期で初めて「消す」が実装される。**");
    Console.WriteLine();

    // --- 0-5. 敵側で標を持つ駒 ----------------------------------------------------------------
    Console.WriteLine("## 0-5. 敵側で標を持ちうる駒（＝敵に標が付く経路）");
    Console.WriteLine();
    {
        var markers = new List<string>();
        foreach (UnitDef d in EnemyCatalog.Stages.SelectMany(st => st.Enemy.Occupied()).Select(o => o.Def).Distinct())
            if (d.Traits.Contains(TraitId.Marker)) markers.Add(d.Name);
        Console.WriteLine($"`Stages` に出る敵で囃し立て（`Marker`）を持つ駒: **{markers.Count} 体**"
            + (markers.Count == 0 ? "" : $"（{string.Join(" / ", markers)}）"));
    }
    Console.WriteLine();
    Console.WriteLine("**敵に標が付く経路は現状ゼロ。** ⇒ `MarkPullPercent` の味方側の動作は");
    Console.WriteLine("**一度も実行されたことがない**（第49期の業が 0.30 回/戦だけ通したのが唯一）。");
    Console.WriteLine("受け入れ基準3（敵の標が機能するか）はここが根拠。");
    Console.WriteLine();

    // --- 0-4. 標の現在値と、カドを含む行 -------------------------------------------------------
    // **ログの文字列を数えている**（gullet log / yoke log / hush / sever と同じ理由）——
    // 標が引いたことは盤面の値に痕跡を残さない（誰が狙われたかは HP の差にしか出ない）。
    Console.WriteLine("## 0-4. 標の現在値 —— 引きの回数と、カドを含む行の第四波・第五波");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{DvScan - 1} × 全5波。`引き` は「敵は … に気を取られた」の行数/戦");
    Console.WriteLine("（**ログの文字列を数えている**——標が引いたことは盤面の値に痕跡を残さない）。");
    Console.WriteLine("`敵の単体振り` は**敵側だけ**が単体で振った回数/戦——標が付いているのは味方だけなので、");
    Console.WriteLine("味方の振りは標の窓口を1度も通らない。");
    Console.WriteLine();
    Console.WriteLine("> **`引き` は「標が主目標を差し替えた回数」であって「標持ちが狙われた回数」ではない。**");
    Console.WriteLine("> `marked != target` のときしかログを出さないので、**たまたま標持ちが先に選ばれた回**は");
    Console.WriteLine("> 数に入らない。実効の被狙撃率は `引き率` より 1/n（n = pool の大きさ）ぶん高い。");
    Console.WriteLine("> **差し替えた回数のほうが「標が盤面を動かした量」としては正しい**（帰属に使うのはこちら）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | ヒサ | カド | 引き | 敵の単体振り | 引き率 | カド寿命 | カド干渉 | 第4波 | 第5波 |");
    Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
    var dvMarkRows = new List<(string Name, bool Hisa, bool Kado, double Pull, double Rate)>();
    foreach (var b in dvBuilds)
    {
        var ids = b.F.Occupied().Select(o => o.Def.Id).ToHashSet();
        bool hasHisa = ids.Contains("hisa"), hasKado = ids.Contains("kado");
        if (!hasHisa && !hasKado) continue;

        double pull = 0, singles = 0, life = 0, inter = 0; int battles = 0;
        var wins = new double[dvStages.Count];
        for (int w = 0; w < dvStages.Count; w++)
        {
            int win = 0;
            for (int seed = 0; seed < DvScan; seed++)
            {
                BattleResult r = BattleEngine.Run(b.F, dvStages[w].Enemy, seed, verbose: true);
                if (r.PlayerWon) win++;
                battles++;
                pull += r.Log.Count(l => l.Text.Contains("気を取られた"));
                // **分母は敵側の単体振りだけ。** 標が付いているのは味方だけなので、
                // 味方の振りは標の窓口を1度も通らない（`foes` に標持ちがいない）。
                // 味方の InstanceId は 0..N-1（Run が味方 → 敵の順で Add する）＋味方側の Summon。
                var mine = new HashSet<int>(Enumerable.Range(0, b.F.Occupied().Count()));
                foreach (BattleEvent e in r.Events)
                    if (e.Kind == BattleEventKind.Summon && e.Team == BattleContext.PlayerTeam
                        && e.TargetId is int sid) mine.Add(sid);
                singles += r.Events.Count(e => e.Kind == BattleEventKind.Attack
                                               && e.Pattern == AttackPattern.Single
                                               && e.ActorId is int aid && !mine.Contains(aid));
                if (hasKado && r.TallyByUnit.TryGetValue("kado", out UnitTally? t))
                { life += t.LastActiveTurn; inter += t.Interventions; }
            }
            wins[w] = win * 100.0 / DvScan;
        }
        double n = battles;
        Console.WriteLine($"| {b.Name} | {(hasHisa ? "●" : "")} | {(hasKado ? "●" : "")} "
            + $"| {pull / n:0.00} | {singles / n:0.00} | {(singles > 0 ? $"{pull * 100 / singles:0.0}%" : "—")} "
            + $"| {(hasKado ? $"{life / n:0.00}" : "—")} | {(hasKado ? $"{inter / n:0.00}" : "—")} "
            + $"| {wins[3]:0.0}% | {wins[4]:0.0}% |");
        dvMarkRows.Add((b.Name, hasHisa, hasKado, pull / n, singles > 0 ? pull * 100 / singles : 0));
        Console.Out.Flush();
    }
    Console.WriteLine();
    {
        var withHisa = dvMarkRows.Where(r => r.Hisa).ToList();
        var noHisa = dvMarkRows.Where(r => !r.Hisa).ToList();
        Console.WriteLine($"- ヒサを含む行（{withHisa.Count}）の引き: 平均 **{withHisa.Average(r => r.Pull):0.00} 回/戦**"
            + $"・引き率 平均 **{withHisa.Average(r => r.Rate):0.0}%**");
        Console.WriteLine($"- ヒサを含まない行（{noHisa.Count}）の引き: 平均 **{(noHisa.Count == 0 ? 0 : noHisa.Average(r => r.Pull)):0.00} 回/戦**"
            + "（標の書き手がいないので 0 のはず）");
    }
    Console.WriteLine();

    // --- 0-6 / 0-7 ----------------------------------------------------------------------------
    Console.WriteLine("## 0-6. `docs/balance.md` の分母");
    Console.WriteLine();
    Console.WriteLine($"編成 **{dvBuilds.Length}** 行 × 波 **{dvStages.Count}** = **{dvBuilds.Length * dvStages.Count} セル**。");
    Console.WriteLine();
    Console.WriteLine("## 0-7. 残り枠");
    Console.WriteLine();
    Console.WriteLine($"`UnitCatalog.All` は **{UnitCatalog.All.Count}** 体。上限 52 に対して残り "
        + $"**{52 - UnitCatalog.All.Count}**（第49期は棄却・残置なので消費していない）。");
    Console.WriteLine($"**この期で1枚使うと残り {52 - UnitCatalog.All.Count - 1} になる。**");
    Console.WriteLine();
    return;
}

// divert probe: 採用する2行を選ぶための候補探索（第50期 §3-4）。
// **`CompareBuilds()` は触らない**（候補はここでローカルに組む。`gradient` / `aim` と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 divert probe
public static void Probe(string[] args, int stageIndex)
{
    IReadOnlyList<EnemyCatalog.Stage> pbStages = EnemyCatalog.Stages;
    const int PbSeeds = 200;

    UnitDef PbPlain = new()
    {
        Id = "sora_plain", Name = "素体のソラ", MaxHp = UnitCatalog.Sora.MaxHp,
        Attack = UnitCatalog.Sora.Attack, Speed = UnitCatalog.Sora.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Sora.Pattern
    };
    Formation PbSwap(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Sora) ? PbPlain : d;
        return g;
    }

    var pbCand = new (string Name, Formation F)[]
    {
        // --- カド入り（残件Aの再現台の候補）---
        ("P1 反撃のノノ枠 (カド/ガルド/ヒサ/ソラ/ネル)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Nel)),
        ("P2 反撃のネル枠 (カド/ガルド/ヒサ/ノノ/ソラ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Lili, back3: UnitCatalog.Sora)),
        ("P3 反撃改のドハ枠 (カド/ガルド/ヒサ/ソラ/ノノ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Lili)),
        ("P4 第49期の土台 (カド/ボルグ/ヒサ/ソラ/ガルド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Gald)),
        ("P5 溜めのガン枠 (カド/ドルガ/ヒサ/ソラ/ガルド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Dolga)),
        // 第49期の業の行と**席まで同じ**（ゴウ→ソラ）。ヒサは後1で、隣接は カド(96) と 中央。
        // **中央をグザ(58)にしてあるのでヒサは必ずカドに標を付ける**——残件Aの再現条件そのもの。
        ("P6 第49期の業の席 (カド/ボルグ/グザ/ヒサ/ソラ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Guza,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Sora)),
        // 同上でソラを中央に（第49期の採用席）。**ヒサの隣接最大HPが カド96 と ソラ96 で同値**に
        // なるので `PickOne` が 50/50 に割る——再現条件としては汚れるが、席の対照として測る。
        ("P7 第49期の採用席 (カド/ボルグ/ソラ/ヒサ/グザ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Sora,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Guza)),
        // カドを含むが**ヒサを含まない**（外す対象が無い＝焦点と代金だけ）。
        ("P8 カド入りヒサ無し (カド/ガルド/ノノ/ソラ/ネル)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Lili,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Nel)),
        // --- カド無し・ヒサ入り（外しは走るが反撃役がいない）---
        ("Q1 ヒサ入り (ドルガ/ガルド/ヒサ/ソラ/ノノ)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Lili)),
        ("Q2 ヒサ入り (ドルガ/ガルド/ヒサ/ソラ/ザン)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Zan)),
        ("Q3 ヒサ入り (ボルグ/ガルド/ヒサ/ソラ/ドルガ)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Dolga)),
        // --- カド無し・ヒサ無し（焦点と代金だけが残る）---
        ("R1 刻みのヴェル枠 (エグ/ゴルム/ノミ/ドルガ/ソラ)", Formation.Build(
            front1: UnitCatalog.Egu, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi,
            back1: UnitCatalog.Dolga, back3: UnitCatalog.Sora)),
        ("R2 耐久のセロ枠 (ガルド/ドルガ/ノノ/ソラ/ゴルム)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Lili,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Golm)),
        ("R3 逆しま改のセッキ枠 (ガルド/ゴルム/ウツ/ソラ/クビ)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Golm, center: UnitCatalog.Utsu,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Kubi)),
        ("R4 死の連鎖のヴェル枠 (ゾト/ムグ/ゴルム/リィカ/ソラ)", Formation.Build(
            front1: UnitCatalog.Zoto, front3: UnitCatalog.Mug, center: UnitCatalog.Golm,
            back1: UnitCatalog.Rica, back3: UnitCatalog.Sora)),
        ("R5 後衛特化のセロ枠 (ガルド/ドルガ/ゴルム/セッキ/ソラ)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Golm,
            back1: UnitCatalog.Sekki, back3: UnitCatalog.Sora)),
    };

    Console.WriteLine("# 逸らし: 候補台の探索（`CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{PbSeeds - 1}。`帰属` は ソラ版 − 素体版（同数値・特性なし）。");
    Console.WriteLine("**第四波が − / 第五波が + なら残件Aの符号反転が再現している。**");
    Console.WriteLine();
    Console.WriteLine("| 候補 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 発火 | 外し | 焦点数 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var c in pbCand)
    {
        var wins = new double[pbStages.Count];
        var pw = new double[pbStages.Count];
        double fires = 0, strips = 0, markedSum = 0; int battles = 0;
        for (int w = 0; w < pbStages.Count; w++)
        {
            int a = 0, b = 0;
            for (int seed = 0; seed < PbSeeds; seed++)
            {
                var r = BattleEngine.Run(c.F, pbStages[w].Enemy, seed, false);
                if (r.PlayerWon) a++;
                fires += r.DivertFires; strips += r.DivertStrips;
                markedSum += r.DivertFires > 0 ? (double)r.DivertMarkedFoeSum / r.DivertFires : 0;
                battles++;
                if (BattleEngine.Run(PbSwap(c.F), pbStages[w].Enemy, seed, false).PlayerWon) b++;
            }
            wins[w] = a * 100.0 / PbSeeds;
            pw[w] = b * 100.0 / PbSeeds;
        }
        Console.WriteLine($"| {c.Name} | ソラ |" + string.Concat(wins.Select(x => $" {x:0.0}% |"))
            + $" {wins.Average():0.0}% | {fires / battles:0.00} | {strips / battles:0.00} | {markedSum / battles:0.00} |");
        Console.WriteLine($"| | 素体 |" + string.Concat(pw.Select(x => $" {x:0.0}% |"))
            + $" {pw.Average():0.0}% | — | — | — |");
        Console.WriteLine($"| | **帰属** |"
            + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $" {wins[i] - pw[i]:+0.0;-0.0;0.0} |"))
            + $" **{wins.Average() - pw.Average():+0.0;-0.0;0.0}** | | | |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}

// divert モード（本体）: 逸らし（第50期）。**出力は `docs/` に置かない。**
// `Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（カドの有無で分けた対）。
//
//     dotnet run --project BattleSim -c Release 0 divert [絞り込み]
//     dotnet run --project BattleSim -c Release 0 divert sweep    # TargetCount の掃引だけ
//     dotnet run --project BattleSim -c Release 0 divert seats    # 席の分散だけ
//     dotnet run --project BattleSim -c Release 0 divert confirm  # 配置の追試（seed 200..599）
//     dotnet run --project BattleSim -c Release 0 divert alt      # 機構の帰属を別 seed 帯で追試
public static void Run(string[] args, int stageIndex)
{
    var dvBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> dvStages = EnemyCatalog.Stages;
    const int DvSeeds = 200;   // compare / spread / scale / scapegoat と揃える
    const int DvMain = 1;      // 主表に使う TargetCount（探索段階の初期値）
    int[] dvCounts = { 1, 2, 3 };

    string dvMode = args.Length > 2 ? args[2] : "";

    static bool DvHasSora(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Sora));

    var dvTargets = dvBuilds.Where(b => DvHasSora(b.F)).ToArray();
    if (dvMode.Length > 0 && dvMode != "sweep" && dvMode != "seats"
        && dvMode != "confirm" && dvMode != "alt")
        dvTargets = dvTargets.Where(b => dvMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // **素体の対照（対照1）。** ソラと数値・型・速さが1つも違わず、特性だけを持たない駒。
    // **`DivertRule` を弱める形の対照は使わない**（第47期 `ScaleRule(0)` の失敗）——
    // `TargetCount` を下げても外しと自分への標は止まらないので、
    // 「機構が効いたのか、ただ 96/6/8 の体が入ったのか」が割れない。
    UnitDef DvPlainDef = new()
    {
        Id = "sora_plain", Name = "素体のソラ", MaxHp = UnitCatalog.Sora.MaxHp,
        Attack = UnitCatalog.Sora.Attack, Speed = UnitCatalog.Sora.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Sora.Pattern
    };
    Formation DvPlain(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Sora) ? DvPlainDef : d;
        return g;
    }
    // ソラを外した4体版（対照3）。**第21期の飽和検査**も兼ねる。
    static Formation DvWithoutSora(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Sora)) g[slot] = d;
        return g;
    }
    // ソラの席だけを振った5変種。
    static Formation DvSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Sora))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Sora;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    DvStat MeasureDv(Formation f, Formation enemy, DivertRule rule, string who)
    {
        var z = new DvStat();
        for (int seed = 0; seed < DvSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                        null, null, null, null, null, null, null, null, null, null, null, null,
                        rule with { Audit = true });
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Fires += r.DivertFires; z.Strips += r.DivertStrips;
            z.Focus += r.DivertFocus; z.FocusFresh += r.DivertFocusFresh;
            z.MarkedFoe += r.DivertFires > 0 ? (double)r.DivertMarkedFoeSum / r.DivertFires : 0;
            if (r.DivertMarkedFoeMax > z.MarkedFoeMax) z.MarkedFoeMax = r.DivertMarkedFoeMax;
            z.AllySingles += r.DivertAllySingles; z.AllyOnMarked += r.DivertAllyOnMarked;
            z.FoeSingles += r.DivertFoeSingles; z.FoeOnMarked += r.DivertFoeOnMarked;
            z.AllyPulls += r.DivertAllyPulls; z.FoePulls += r.DivertFoePulls;
            foreach ((string k, int v) in r.DivertStripFrom)
                z.StripFrom[k] = z.StripFrom.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.DivertFocusTo)
                z.FocusTo[k] = z.FocusTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.DivertKillTurnByFoe)
                z.KillTurn[k] = z.KillTurn.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.DivertKillCountByFoe)
                z.KillCount[k] = z.KillCount.TryGetValue(k, out double a) ? a + v : v;
            if (r.TallyByUnit.TryGetValue(who, out UnitTally? tw))
            { z.SelfTaken += tw.DamageTaken; z.Life += tw.LastActiveTurn; }
            if (r.TallyByUnit.TryGetValue("kado", out UnitTally? tk))
            { z.KadoLife += tk.LastActiveTurn; z.KadoInter += tk.Interventions; z.HasKado = true; }
        }
        double n = DvSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.Fires /= n; z.Strips /= n; z.Focus /= n; z.FocusFresh /= n; z.MarkedFoe /= n;
        z.AllySingles /= n; z.AllyOnMarked /= n; z.FoeSingles /= n; z.FoeOnMarked /= n;
        z.AllyPulls /= n; z.FoePulls /= n;
        z.SelfTaken /= n; z.Life /= n; z.KadoLife /= n; z.KadoInter /= n;
        foreach (string k in z.StripFrom.Keys.ToList()) z.StripFrom[k] /= n;
        foreach (string k in z.FocusTo.Keys.ToList()) z.FocusTo[k] /= n;
        return z;
    }

    (double[] Wins, DvStat Z) DvAll(Formation f, DivertRule rule, string who)
    {
        var wins = new double[dvStages.Count];
        var acc = new DvStat();
        for (int w = 0; w < dvStages.Count; w++)
        {
            var z = MeasureDv(f, dvStages[w].Enemy, rule, who);
            wins[w] = z.Win;
            acc.Turns += z.Turns; acc.Fires += z.Fires; acc.Strips += z.Strips;
            acc.Focus += z.Focus; acc.FocusFresh += z.FocusFresh; acc.MarkedFoe += z.MarkedFoe;
            acc.AllySingles += z.AllySingles; acc.AllyOnMarked += z.AllyOnMarked;
            acc.FoeSingles += z.FoeSingles; acc.FoeOnMarked += z.FoeOnMarked;
            acc.AllyPulls += z.AllyPulls; acc.FoePulls += z.FoePulls;
            acc.SelfTaken += z.SelfTaken; acc.Life += z.Life;
            acc.KadoLife += z.KadoLife; acc.KadoInter += z.KadoInter; acc.HasKado |= z.HasKado;
            if (z.MarkedFoeMax > acc.MarkedFoeMax) acc.MarkedFoeMax = z.MarkedFoeMax;
            foreach ((string k, double v) in z.StripFrom)
                acc.StripFrom[k] = acc.StripFrom.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.FocusTo)
                acc.FocusTo[k] = acc.FocusTo.TryGetValue(k, out double a) ? a + v : v;
        }
        double m = dvStages.Count;
        acc.Win = wins.Average();
        acc.Turns /= m; acc.Fires /= m; acc.Strips /= m; acc.Focus /= m; acc.FocusFresh /= m;
        acc.MarkedFoe /= m; acc.AllySingles /= m; acc.AllyOnMarked /= m;
        acc.FoeSingles /= m; acc.FoeOnMarked /= m; acc.AllyPulls /= m; acc.FoePulls /= m;
        acc.SelfTaken /= m; acc.Life /= m; acc.KadoLife /= m; acc.KadoInter /= m;
        foreach (string k in acc.StripFrom.Keys.ToList()) acc.StripFrom[k] /= m;
        foreach (string k in acc.FocusTo.Keys.ToList()) acc.FocusTo[k] /= m;
        return (wins, acc);
    }

    double[] DvWins(Formation f)
    {
        var v = new double[dvStages.Count];
        for (int w = 0; w < dvStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < DvSeeds; seed++)
                if (BattleEngine.Run(f, dvStages[w].Enemy, seed, false).PlayerWon) wins++;
            v[w] = wins * 100.0 / DvSeeds;
        }
        return v;
    }

    static string DvCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));
    static string DvTop(Dictionary<string, double> d, int n = 3)
    {
        var parts = d.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(n)
            .Select(x => $"{x.Key} {x.Value:0.00}").ToList();
        return parts.Count == 0 ? "—" : string.Join(" / ", parts);
    }

    Console.WriteLine("# 逸らし（divert）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 divert [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{DvSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（カドの有無で分けた対）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 発火 | 逸らしの発火回数/戦。**0 になっていないことが受け入れ基準4**（配置探索が機構を無効化していないか） |");
    Console.WriteLine("| 外し | 味方から外した標の回数/戦・**外した相手の内訳** |");
    Console.WriteLine("| 焦点 | 敵に付けた回数/戦（`新規` は新しく標が付いた回数）・**付けた敵の内訳** |");
    Console.WriteLine("| 焦点数 | 発火時点で**標を持っている敵の数**の平均/最大。**敵の標は消えないので焦点は自分で溶ける** |");
    Console.WriteLine("| **焦点の効き** | **味方の単体振りのうち標持ちの敵に当たった割合**（`引き` は engine の鎖が実際に差し替えた回数） |");
    Console.WriteLine("| **代金の効き** | **敵の単体振りのうち標持ちの味方に当たった割合**（同上） |");
    Console.WriteLine("| 自傷 / 寿命 | ソラが受けた被弾/戦 ／ ソラが最後に盤上にいたターン |");
    Console.WriteLine("| カド | カドの生存ターンと干渉回数（＝反撃を含む「盤面を動かした回数」）。カド入り台のみ |");
    Console.WriteLine();
    Console.WriteLine("> **「焦点」と「焦点の効き」は別の列。** 標を付けた回数は成果ではない");
    Console.WriteLine("> ——味方がそちらを殴らなければ意味がない。**撃破順（§2）が本命の指標。**");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2・3）--------------------------------------------------------
    if (dvMode.Length == 0)
    {
        Console.WriteLine("## 0. 検算");
        Console.WriteLine();
        var plain = dvBuilds.Where(b => !DvHasSora(b.F)).ToArray();
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < dvStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < DvSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, dvStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null, null,
                            new DivertRule(1)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, dvStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null, null,
                            new DivertRule(5, false)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（ソラを含まない {plain.Length} 行が `DivertRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {dvStages.Count} 波・`(1, true)` 対 `(5, false)`）");

        int aCells = 0, aDiff = 0;
        foreach (var b in dvBuilds)
            for (int w = 0; w < dvStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < DvSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, dvStages[w].Enemy, seed, false).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, dvStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null, null,
                            new DivertRule(DivertRule.Default.TargetCount, true, true)).PlayerWon) c++;
                }
                aCells++;
                if (a != c) aDiff++;
            }
        Console.WriteLine($"- **監査は盤面を動かさない**（`Audit` の有無で `compare` が変わらない）: "
            + $"**{aCells} セル中 {aDiff} 件の食い違い**（{dvBuilds.Length} 行 × {dvStages.Count} 波）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  行を足す前に `compare` の全文で確認済み（**250 セル中 0 件**）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 ------------------------------------------------------------------------------
    if (dvMode.Length == 0 || (dvMode != "sweep" && dvMode != "seats"
                               && dvMode != "confirm" && dvMode != "alt"))
    {
        Console.WriteLine($"## 1. 主表（`TargetCount = {DvMain}` と陽性対照3本）");
        Console.WriteLine();
        Console.WriteLine("`素体` = ソラと**数値・型・速さが1つも違わず特性だけを持たない駒**（対照1）。");
        Console.WriteLine("**これが機構の帰属を取る唯一の窓口**——`TargetCount` を下げても外しと自分への標は止まらない。");
        Console.WriteLine("`代金なし` = `DivertRule.SelfMark = false`（対照2）。**自分に標を付けない版**で、");
        Console.WriteLine("「味方の標を外す」＋「敵に焦点を作る」だけが残る——**代金の分離**。");
        Console.WriteLine("`4体` = ソラを外した4体版（対照3。**第21期の飽和検査**も兼ねる）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var (wins, _) = DvAll(b.F, new DivertRule(DvMain), "sora");
            var (nc, _) = DvAll(b.F, new DivertRule(DvMain, false), "sora");
            var pw = DvWins(DvPlain(b.F));
            var fw = DvWins(DvWithoutSora(b.F));
            Console.WriteLine($"| {b.Name} | **T{DvMain}** |{DvCells(wins)} {wins.Average():0.0}% |");
            Console.WriteLine($"| | 代金なし（自分に標を付けない） |{DvCells(nc)} {nc.Average():0.0}% |");
            Console.WriteLine($"| | 素体（特性なし・同数値） |{DvCells(pw)} {pw.Average():0.0}% |");
            Console.WriteLine($"| | 4体（ソラ抜き） |{DvCells(fw)} {fw.Average():0.0}% |");
            Console.WriteLine($"| | **機構の帰属（T{DvMain} − 素体）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - pw[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - pw.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **代金の値段（T{DvMain} − 代金なし）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - nc[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - nc.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | 体の値段（素体 − 4体） | "
                + string.Concat(Enumerable.Range(0, pw.Length).Select(i => $"{pw[i] - fw[i]:+0.0;-0.0;0.0} |"))
                + $" **{pw.Average() - fw.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 機構の計数（`TargetCount = {DvMain}`・5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | **発火** | 外し | 外した相手 | 焦点(新規) | 付けた敵 | 焦点数(平均/最大) | **効き・味方** | 引き | **効き・敵** | 引き | 自傷 | 寿命 |");
        Console.WriteLine("|---|--:|--:|---|--:|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var (_, z) = DvAll(b.F, new DivertRule(DvMain), "sora");
            Console.WriteLine($"| {b.Name} | **{z.Fires:0.00}** | {z.Strips:0.00} | {DvTop(z.StripFrom)} "
                + $"| {z.Focus:0.00} ({z.FocusFresh:0.00}) | {DvTop(z.FocusTo)} "
                + $"| {z.MarkedFoe:0.00} / {z.MarkedFoeMax:0} "
                + $"| **{(z.AllySingles > 0 ? $"{z.AllyOnMarked * 100 / z.AllySingles:0.0}%" : "—")}** | {z.AllyPulls:0.00} "
                + $"| **{(z.FoeSingles > 0 ? $"{z.FoeOnMarked * 100 / z.FoeSingles:0.0}%" : "—")}** | {z.FoePulls:0.00} "
                + $"| {z.SelfTaken:0.0} | {z.Life:0.00} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // カドの再現（残件A）
        Console.WriteLine("### カドの生存と干渉（残件Aの再現・カド入り台のみ）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | カド寿命 | カド干渉 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var (wins, z) = DvAll(b.F, new DivertRule(DvMain), "sora");
            if (!z.HasKado) continue;
            var (pw2, pz) = DvAll(DvPlain(b.F), new DivertRule(DvMain), "sora_plain");
            Console.WriteLine($"| {b.Name} | T{DvMain} | {z.KadoLife:0.00} | {z.KadoInter:0.00} | {wins[3]:0.0}% | {wins[4]:0.0}% |");
            Console.WriteLine($"| | 素体 | {pz.KadoLife:0.00} | {pz.KadoInter:0.00} | {pw2[3]:0.0}% | {pw2[4]:0.0}% |");
            Console.WriteLine($"| | **差** | **{z.KadoLife - pz.KadoLife:+0.00;-0.00;0.00}** "
                + $"| **{z.KadoInter - pz.KadoInter:+0.00;-0.00;0.00}** "
                + $"| **{wins[3] - pw2[3]:+0.0;-0.0;0.0}** | **{wins[4] - pw2[4]:+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 2. 撃破順（本命の指標）------------------------------------------------------------
        Console.WriteLine("## 2. 撃破順（**本命の指標**）—— 敵の駒ごとの撃破ターン");
        Console.WriteLine();
        Console.WriteLine("**焦点の狙いは「硬い敵を先に割る」ことなので、対照と比べて撃破ターンが");
        Console.WriteLine("早まっているかで効いたかどうかが決まる。** `撃破率` は倒せた試行の割合。");
        Console.WriteLine("**標に依存しない切り方**で数えているので素体とそのまま引き算できる。");
        Console.WriteLine();
        foreach (var b in dvTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 敵 | HP | 撃破T(ソラ) | 撃破率(ソラ) | 撃破T(素体) | 撃破率(素体) | **撃破Tの差** |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < dvStages.Count; w++)
            {
                var z = MeasureDv(b.F, dvStages[w].Enemy, new DivertRule(DvMain), "sora");
                var pz = MeasureDv(DvPlain(b.F), dvStages[w].Enemy, new DivertRule(DvMain), "sora_plain");
                foreach ((int slot, UnitDef d) in dvStages[w].Enemy.Occupied())
                {
                    double ct = z.KillCount.TryGetValue(d.Id, out double c1) ? c1 : 0;
                    double tt = z.KillTurn.TryGetValue(d.Id, out double t1) ? t1 : 0;
                    double pc = pz.KillCount.TryGetValue(d.Id, out double c2) ? c2 : 0;
                    double pt = pz.KillTurn.TryGetValue(d.Id, out double t2) ? t2 : 0;
                    // 同じ def が同じ波に複数いる行があるので、体数で割って1体あたりに直す
                    int copies = dvStages[w].Enemy.Occupied().Count(o => o.Def.Id == d.Id);
                    if (dvStages[w].Enemy.Occupied().First(o => o.Def.Id == d.Id).Slot != slot) continue;
                    double a = ct > 0 ? tt / ct : 0, p = pc > 0 ? pt / pc : 0;
                    Console.WriteLine($"| {dvStages[w].Name} | {d.Name}{(copies > 1 ? $" ×{copies}" : "")} | {d.MaxHp} "
                        + $"| {(ct > 0 ? $"{a:0.00}" : "—")} | {ct * 100 / (DvSeeds * copies):0.0}% "
                        + $"| {(pc > 0 ? $"{p:0.00}" : "—")} | {pc * 100 / (DvSeeds * copies):0.0}% "
                        + $"| {(ct > 0 && pc > 0 ? $"**{a - p:+0.00;-0.00;0.00}**" : "—")} |");
                }
                Console.Out.Flush();
            }
            Console.WriteLine();
        }

        // --- 3. 波ごとの内訳 -------------------------------------------------------------------
        Console.WriteLine($"## 3. 波ごとの内訳（`TargetCount = {DvMain}`）");
        Console.WriteLine();
        foreach (var b in dvTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 発火 | 外し | 焦点(新規) | 付けた敵 | 焦点数 | 効き・味方 | 効き・敵 | 自傷 | 寿命 | 決着T |");
            Console.WriteLine("|---|--:|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < dvStages.Count; w++)
            {
                var z = MeasureDv(b.F, dvStages[w].Enemy, new DivertRule(DvMain), "sora");
                Console.WriteLine($"| {dvStages[w].Name} | {z.Win:0.0}% | {z.Fires:0.00} | {z.Strips:0.00} "
                    + $"| {z.Focus:0.00} ({z.FocusFresh:0.00}) | {DvTop(z.FocusTo, 2)} | {z.MarkedFoe:0.00} "
                    + $"| {(z.AllySingles > 0 ? $"{z.AllyOnMarked * 100 / z.AllySingles:0.0}%" : "—")} "
                    + $"| {(z.FoeSingles > 0 ? $"{z.FoeOnMarked * 100 / z.FoeSingles:0.0}%" : "—")} "
                    + $"| {z.SelfTaken:0.0} | {z.Life:0.00} | {z.Turns:0.0} |");
                Console.Out.Flush();
            }
            Console.WriteLine();
        }
    }

    // --- 4. 掃引 ------------------------------------------------------------------------------
    if (dvMode.Length == 0 || dvMode == "sweep")
    {
        Console.WriteLine("## 4. 掃引（`TargetCount` 1 / 2 / 3）");
        Console.WriteLine();
        Console.WriteLine("**`TargetCount` は「集中」と「被覆」を取り替えるノブ。** 標持ちが増えると");
        Console.WriteLine("`p_t`（無作為の主目標が既に標持ちである確率）が上がるので**総量は増える**が、");
        Console.WriteLine("1体あたりの取り分は薄まる。**勝率が動かないのは2つが打ち消し合うから**で、");
        Console.WriteLine("ノブが機構を動かしていないからではない（`効き・味方` の列で確かめる）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | TC | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 焦点数 | 効き・味方 | 効き・敵 | 自傷 | 寿命 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            foreach (int tc in dvCounts)
            {
                var (wins, z) = DvAll(b.F, new DivertRule(tc), "sora");
                Console.WriteLine($"| {b.Name} | {tc} | **{wins.Average():0.0}%** |{DvCells(wins)} "
                    + $"{z.MarkedFoe:0.00} "
                    + $"| {(z.AllySingles > 0 ? $"{z.AllyOnMarked * 100 / z.AllySingles:0.0}%" : "—")} "
                    + $"| {(z.FoeSingles > 0 ? $"{z.FoeOnMarked * 100 / z.FoeSingles:0.0}%" : "—")} "
                    + $"| {z.SelfTaken:0.0} | {z.Life:0.00} |");
                Console.Out.Flush();
            }
            var pw = DvWins(DvPlain(b.F));
            Console.WriteLine($"| {b.Name} | 素体 | **{pw.Average():0.0}%** |{DvCells(pw)} — | — | — | — | — |");
        }
        Console.WriteLine();
        foreach (var b in dvTargets)
        {
            var vals = dvCounts.Select(tc => DvAll(b.F, new DivertRule(tc), "sora").Wins.Average()).ToList();
            Console.WriteLine($"- **{b.Name} の掃引の全幅: {vals.Max() - vals.Min():0.0}pt**"
                + $"（{string.Join(" / ", dvCounts.Zip(vals, (t, v) => $"TC{t} {v:0.0}%"))}）");
        }
        Console.WriteLine();
    }

    // --- 5. 別 seed の追試 --------------------------------------------------------------------
    if (dvMode == "alt")
    {
        const int AltFrom = 200, AltTo = 600;
        Console.WriteLine("## 5. 別 seed の追試（seed 200..599・機構の帰属）");
        Console.WriteLine();
        Console.WriteLine("**受け入れ基準6（残件Aの符号反転の再現）はここで判定する。**");
        Console.WriteLine("第49期の実測は 第四波 −18.0 / −23.0pt・第五波 +22.0 / +19.0pt だった。");
        Console.WriteLine();
        double[] Cells(Formation f, DivertRule? rule)
        {
            var v = new double[dvStages.Count];
            for (int w = 0; w < dvStages.Count; w++)
            {
                int wins = 0;
                for (int seed = AltFrom; seed < AltTo; seed++)
                    if (BattleEngine.Run(f, dvStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null, null,
                            rule).PlayerWon) wins++;
                v[w] = wins * 100.0 / (AltTo - AltFrom);
            }
            return v;
        }
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var t = Cells(b.F, new DivertRule(DvMain));
            var pl = Cells(DvPlain(b.F), null);
            Console.WriteLine($"| {b.Name} | T{DvMain} |{DvCells(t)} {t.Average():0.0}% |");
            Console.WriteLine($"| | 素体 |{DvCells(pl)} {pl.Average():0.0}% |");
            Console.WriteLine($"| | **帰属（追試）** | "
                + string.Concat(Enumerable.Range(0, t.Length).Select(i => $"{t[i] - pl[i]:+0.0;-0.0;0.0} |"))
                + $" **{t.Average() - pl.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    // --- 6. 配置の追試 ------------------------------------------------------------------------
    if (dvMode == "confirm")
    {
        const int CfFrom = 200, CfTo = 600;
        Console.WriteLine("## 6. 配置の追試（`confirm`・seed 200..599）");
        Console.WriteLine();
        Console.WriteLine("**選定に使っていない seed 帯**で測り直す。採否閾値は **5.0pt**（第46期）。");
        Console.WriteLine("**1位の配置ではなく次数で読む**（第45期の残件 D）。");
        Console.WriteLine("**`発火` の列を必ず見る**——配置探索が機構を無効化する席を選んでいたら");
        Console.WriteLine("そこは採用しない（第49期の業改が引き取り 0.00 回/戦になった件・§1 の作法）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | ソラの席 | 次数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 現行との差 | 発火 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var members = b.F.Occupied().Select(x => x.Def).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in SlotAssignments(members.Count))
            {
                var g = new Formation();
                for (int m = 0; m < members.Count; m++) g[assign[m]] = members[m];
                perms.Add(g);
            }
            var scan = new int[perms.Count];
            for (int i = 0; i < perms.Count; i++)
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in dvStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));

            (double[] Cells, double Fires) Measure(Formation f)
            {
                var v = new double[dvStages.Count];
                double fires = 0; int n = 0;
                for (int w = 0; w < dvStages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = CfFrom; seed < CfTo; seed++)
                    {
                        var r = BattleEngine.Run(f, dvStages[w].Enemy, seed, false);
                        if (r.PlayerWon) wins++;
                        fires += r.DivertFires; n++;
                    }
                    v[w] = wins * 100.0 / (CfTo - CfFrom);
                }
                return (v, fires / n);
            }
            int Seat(Formation f)
            {
                foreach ((int slot, UnitDef d) in f.Occupied())
                    if (ReferenceEquals(d, UnitCatalog.Sora)) return slot;
                return -1;
            }
            int Deg(int slot)
            {
                int n = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(slot, i)) n++;
                return n;
            }

            var cur = Measure(b.F);
            Console.WriteLine($"| {b.Name} | **現行** | {FormationRules.SeatNames[Seat(b.F)]} | {Deg(Seat(b.F))} "
                + $"|{DvCells(cur.Cells)} **{cur.Cells.Average():0.0}%** | — | {cur.Fires:0.00} |");
            Console.Out.Flush();
            foreach (int idx in order.Take(5))
            {
                if (idx == curIdx) continue;
                var v = Measure(perms[idx]);
                int seat = Seat(perms[idx]);
                Console.WriteLine($"| {b.Name} | 粗探索 {order.IndexOf(idx) + 1}位 | {FormationRules.SeatNames[seat]} | {Deg(seat)} "
                    + $"|{DvCells(v.Cells)} {v.Cells.Average():0.0}% "
                    + $"| **{v.Cells.Average() - cur.Cells.Average():+0.0;-0.0;0.0}pt** | {v.Fires:0.00} |");
                Console.WriteLine($"|   ↳ 配置 | {string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} | | | | | | | | | | |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        return;
    }

    // --- 7. 席の分散 --------------------------------------------------------------------------
    if (dvMode.Length == 0 || dvMode == "seats")
    {
        Console.WriteLine("## 7. 席の分散（`seats2` の写し・受け入れ基準12）");
        Console.WriteLine();
        Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 + 最下位 を seed 0..199 で測り直し。");
        Console.WriteLine("**ソラは隣接も列も読まない**（外しは味方全員・焦点は敵全員から選ぶ）が、");
        Console.WriteLine("**自分が矢面に立つ**ので列（前後）は効くはず——第47期の鱗と同じ形になるかを見る。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（3値） | 最頻率 | 幅 | 現行の順位 | 1位との差 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var members = b.F.Occupied().Select(x => x.Def).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in SlotAssignments(members.Count))
            {
                var g = new Formation();
                for (int m = 0; m < members.Count; m++) g[assign[m]] = members[m];
                perms.Add(g);
            }
            var scan = new int[perms.Count];
            for (int i = 0; i < perms.Count; i++)
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in dvStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));
            var pool = order.Take(20).Append(curIdx).Append(order[^1]).Distinct().ToList();

            double Avg(Formation f)
            {
                double avg = 0;
                foreach (EnemyCatalog.Stage st in dvStages)
                {
                    int wins = 0;
                    for (int seed = 0; seed < DvSeeds; seed++)
                        if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    avg += wins * 100.0 / DvSeeds;
                }
                return avg / dvStages.Count;
            }

            var verified = pool.Select(i => (Idx: i, Avg: Avg(perms[i]))).OrderByDescending(x => x.Avg).ToList();
            double width = verified[0].Avg - verified[^1].Avg;
            var top5 = verified.Take(5).ToList();
            int curRank = verified.FindIndex(x => x.Idx == curIdx) + 1;
            double curGap = verified[0].Avg - verified.First(x => x.Idx == curIdx).Avg;

            foreach (UnitDef d in members)
            {
                int bestSlot = -1;
                foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                    if (ReferenceEquals(dd, d)) bestSlot = slot;
                int mid = 0, fcorner = 0, bcorner = 0;
                foreach (var v in top5)
                    foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                        if (ReferenceEquals(dd, d))
                        {
                            int deg2 = 0;
                            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                                if (FormationRules.AreAdjacent(slot, i)) deg2++;
                            if (deg2 == 4) mid++;
                            else if (FormationRules.RowOf(slot) == Row.Front) fcorner++;
                            else bcorner++;
                        }
                int bdeg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
                int top = Math.Max(mid, Math.Max(fcorner, bcorner));
                Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                    + $"| 前角{fcorner} / 中央{mid} / 後角{bcorner} | {top * 100 / 5}% | {width:0.0}pt "
                    + $"| {curRank} / {verified.Count} | {curGap:0.0}pt |");
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine("### ソラ1枚だけを振った5変種（他の4枚は元の相対順のまま詰める）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ソラの席 | 次数 | 列 | 発火 | 外し | 焦点数 | 効き・味方 | 効き・敵 | 自傷 | 寿命 | 平均勝率 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                Formation g = DvSeat(b.F, seat);
                int deg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(seat, i)) deg++;
                var (wins, z) = DvAll(g, new DivertRule(DvMain), "sora");
                Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {deg} | {FormationRules.RowOf(seat)} "
                    + $"| {z.Fires:0.00} | {z.Strips:0.00} | {z.MarkedFoe:0.00} "
                    + $"| {(z.AllySingles > 0 ? $"{z.AllyOnMarked * 100 / z.AllySingles:0.0}%" : "—")} "
                    + $"| {(z.FoeSingles > 0 ? $"{z.FoeOnMarked * 100 / z.FoeSingles:0.0}%" : "—")} "
                    + $"| {z.SelfTaken:0.0} | {z.Life:0.00} | {wins.Average():0.0}% |");
                Console.Out.Flush();
            }
        Console.WriteLine();
    }

    return;
}
}
