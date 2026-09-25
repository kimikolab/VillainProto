using BattleCore;
using static Common;

// =====================================================================================
// seats2 モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "seats2")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 seats2
// =====================================================================================

static class Seats2Diag
{
// 隣接という条件がこの盤面で「席の値段」になっているかを調べる（第45期）。
//
// **新しい機構は1つも作らない。** `Traits.cs` / `UnitCatalog.cs` / `Stages` /
// `CompareBuilds()` は1行も触らず、既存の `reseat` の探索ロジックを写して
// **駒ごとに「どの席に置かれたか」**を集計するだけ。
//
// **既存の `seats` / `reseat` は書き換えていない**（別モードにしてある）。
//
// 問いは3つ。
//   Q1 席に値段が付いているか（`幅` ＝ 1位と最下位の勝率差）
//   Q2 **その値段は編成によって変わるか**（`席の分散`。これが本題）
//   Q3 原因は特性の設計か、盤面の形状か（**隣接も列も読まない駒**を対照に置いて切り分ける）
//
// サブモード:
//   seats2 degree           次数分布（Phase 0-2）と 角の対称性（Phase 0-3）。探索しない
//   seats2 list             対象・対照の選定（Phase 0-1 / 0-4）。戦闘を1回も回さない
//   seats2 [skip] [take]    探索本体。**行単位で切り出せる**（長時間ジョブなので分割する）
//
// **`reseat` との差は1点だけ**——検証プール（上位20 + 狙い上位10 + 現行）に
// **粗探索の最下位を1つ足してある**。`幅` を「120通りの1位と最下位の差」として
// 200 seed で測るために要る（`reseat` のプールは上位に偏っているので、
// そのままだと幅が過小になる）。**探索・検証の seed 帯とプールの作り方は写しのまま。**
public static void Run(string[] args, int stageIndex)
{
    var s2Builds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> s2Stages = EnemyCatalog.Stages;
    const int S2Scan = 50;     // 粗探索。reseat / layout と揃える
    const int S2Verify = 200;  // 測り直し。compare と揃える
    const int S2TopOverall = 20;
    const int S2TopConstrained = 10;

    string s2Mode = args.Length > 2 ? args[2] : "";

    // 編成5枠だけを見た隣接次数（召喚枠を除く）。角4つが2・中央が4。
    static int S2Degree(int slot)
    {
        int n = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
            if (FormationRules.AreAdjacent(slot, i)) n++;
        return n;
    }

    // 鏡像写像。0↔1 / 3↔4（中央は不動点）。召喚枠は編成には現れない。
    static int S2Mirror(int slot) => slot switch { 0 => 1, 1 => 0, 3 => 4, 4 => 3, _ => slot };

    // --- 隣接を読む特性 / 列を読む特性 ------------------------------------------------------
    // **grep から機械的に作った一覧**（Traits.cs と BattleEngine.cs を AreAdjacent /
    // AreSameRowPair / IsLanePredecessor / DepthOf / Row. / SlotsOfRow で走査）。
    // 瘴気（Miasma）は**入っていない**——味方漏れは隣接ではなく味方全体（Traits.cs:2024）。
    // 呪詛漏れ・萎縮・号令も入っていない——あれらは味方全体に配り、
    // **隣接を読むのは拡散側（ガルドの Stoic / BattleContext.SupportTargets）**。
    var s2Adj = new HashSet<TraitId>
    {
        TraitId.Splash,      // 巻き込み（ボルグ）: コスト
        TraitId.Cinder,      // 火の粉（ボルグ）: コスト
        TraitId.Sacrifice,   // 生贄（リィカ）: コスト
        TraitId.Venom,       // 毒漏れ（旧スィド・対照）: コスト
        TraitId.VenomHeavy,  // 毒漏れ（スィド・第202期から）: コスト
        TraitId.Thorns,      // 棘（カド）: コスト＋利得
        TraitId.ThornGuard,  // 棘守り（カド）: 利得。AreSameRowPair / IsLanePredecessor
        TraitId.Marker,      // 囃し立て（ヒサ）: 利得
        TraitId.Shove,       // 突き返し（ハネ）: コスト
        TraitId.Bear,        // 集約（ウケ）: 利得。判定は BattleEngine.Dull
        TraitId.Relay,       // 渡し（ワタ）: 利得。判定は BattleEngine.Dull
        TraitId.Overbear,    // 驕り（オゴ）: コスト＋利得。**ロスターで2枚目の非単調な読み手**（第46期）
                             // ——削る量は隣接数に比例（単調なコスト）だが、条件は隣接全員への AND
                             // なので「隣が誰か」で成立時刻が変わる
        TraitId.Goad,        // 駆り立て（カリ）: コスト＋利得。**ロスターで3枚目の非単調な読み手**（第52期）
                             // ——隣接する生存味方の CurrentAttack の最大を取り、**その1体に効果を当てる**
                             // （囃し立てと同型で、驕りのように1つのスカラーへ潰さない）
        TraitId.Stoic,       // 支援拒否（ガルド）: 中立。SupportTargets が隣へ流す
        TraitId.Loose,       // 散開（ササ）: 利得。**第106期に被弾クロックの弾きが付いた**（−35% の側は据え置き）
    };

    // 列（Row / DepthOf）を読む、あるいは席を書き換える特性。**対象でも対照でもない**
    // ——席に依存はするが「隣接」ではないので、混ぜると Q3 の切り分けが壊れる。
    var s2Row = new HashSet<TraitId>
    {
        TraitId.Coward, TraitId.Sniper,   // 臆病・後衛特化（セロ）
        TraitId.Colossus,                 // 巨躯（ゴルム）: DepthOf
        TraitId.Guardian,                 // 庇う（ガルド）: Row.Front
        TraitId.RearGuard,                // 後備え（セッキ）: Row.Back
        TraitId.Displaced,                // 軋み（ヨミ）: DepthOf
        TraitId.Shuffler,                 // 喧噪（バサ）: 席を書き換える
    };

    string S2Class(UnitDef d) =>
        d.Traits.Any(s2Adj.Contains) ? "隣接"
        : d.Traits.Any(s2Row.Contains) ? "列"
        : "無";

    // --- degree: Phase 0-2 と 0-3 -----------------------------------------------------------
    if (s2Mode == "degree")
    {
        Console.WriteLine("# 席の値段（seats2 degree）—— 次数分布と角の対称性");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 seats2 degree` の出力。");
        Console.WriteLine("**docs/ には置かない**（標準出力で読むだけ）。");
        Console.WriteLine();
        Console.WriteLine("## 1. 次数分布（`FormationRules.AreAdjacent` の表から。戦闘は回さない）");
        Console.WriteLine();
        Console.WriteLine("| 席 | 編成5枠のみ | 召喚枠込み |");
        Console.WriteLine("|---|--:|--:|");
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
        {
            int all = 0;
            for (int j = 0; j < FormationRules.TotalSlots; j++)
                if (FormationRules.AreAdjacent(i, j)) all++;
            Console.WriteLine($"| {i} {FormationRules.SeatNames[i]} | {S2Degree(i)} | {all} |");
        }
        Console.WriteLine();
        var degs = Enumerable.Range(0, FormationRules.PlayableSlotCount).Select(S2Degree).Distinct().OrderBy(x => x);
        Console.WriteLine($"**次数の取りうる値: {{{string.Join(", ", degs)}}}（{degs.Count()} 種類）**");
        Console.WriteLine();

        Console.WriteLine("## 2. 角の対称性（現行の配置 vs その鏡像）");
        Console.WriteLine();
        Console.WriteLine("鏡像写像は 0↔1 / 3↔4（中央は不動点）。**盤面のグラフとしては自己同型**なので、");
        Console.WriteLine("エンジンが完全に対称なら差は 0 になるはず。**タイブレークは乱数化済み**だが、");
        Console.WriteLine("README「まだ残っている非対称（未解決）」のとおり完全な同値ではない。");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{S2Verify - 1}（選定帯）と seed 200..599（別帯）の両方で測る。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 移動する駒 | 平均差(0..199) | 最大波差 | 平均差(200..599) | 最大波差 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|");

        var mvIds = new HashSet<TraitId> { TraitId.Coward, TraitId.Shuffler, TraitId.Displaced, TraitId.ThornGuard };
        double sumA = 0, sumB = 0, maxA = 0, maxB = 0;
        double sumMv = 0, sumNo = 0; int nMv = 0, nNo = 0;

        foreach (var b in s2Builds)
        {
            var mir = new Formation();
            foreach ((int slot, UnitDef d) in b.F.Occupied()) mir[S2Mirror(slot)] = d;
            bool moves = b.F.Occupied().Any(o => o.Def.Traits.Any(mvIds.Contains));

            double[] Run(int seed0, int n)
            {
                var diff = new double[s2Stages.Count];
                for (int w = 0; w < s2Stages.Count; w++)
                {
                    int a = 0, c = 0;
                    for (int seed = seed0; seed < seed0 + n; seed++)
                    {
                        if (BattleEngine.Run(b.F, s2Stages[w].Enemy, seed, false).PlayerWon) a++;
                        if (BattleEngine.Run(mir, s2Stages[w].Enemy, seed, false).PlayerWon) c++;
                    }
                    diff[w] = Math.Abs(a - c) * 100.0 / n;
                }
                return diff;
            }

            double[] dA = Run(0, S2Verify), dB = Run(200, 400);
            double avgA = dA.Average(), avgB = dB.Average();
            sumA += avgA; sumB += avgB;
            if (dA.Max() > maxA) maxA = dA.Max();
            if (dB.Max() > maxB) maxB = dB.Max();
            if (moves) { sumMv += avgA; nMv++; } else { sumNo += avgA; nNo++; }

            Console.WriteLine($"| {b.Name} | {(moves ? "○" : "")} | {avgA:0.00} | {dA.Max():0.0} "
                + $"| {avgB:0.00} | {dB.Max():0.0} |");
            Console.Out.Flush();
        }
        int n2 = s2Builds.Length;
        Console.WriteLine();
        Console.WriteLine($"**全 {n2} 行の平均差: {sumA / n2:0.00}pt（0..199） / {sumB / n2:0.00}pt（200..599）**"
            + $"。波ごとの最大差 {maxA:0.0}pt / {maxB:0.0}pt。");
        Console.WriteLine($"席を動かす駒あり **{nMv} 行**: {sumMv / Math.Max(1, nMv):0.00}pt ／ "
            + $"なし **{nNo} 行**: {sumNo / Math.Max(1, nNo):0.00}pt。");
        Console.WriteLine();
        return;
    }

    // --- list: Phase 0-1 / 0-4 の選定 -------------------------------------------------------
    if (s2Mode == "list")
    {
        Console.WriteLine("# 席の値段（seats2 list）—— 対象と対照の選定");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 seats2 list` の出力。**戦闘は1回も回さない。**");
        Console.WriteLine();
        Console.WriteLine("`隣接` = `AreAdjacent` / `AreSameRowPair` / `IsLanePredecessor` を読む特性を持つ駒。");
        Console.WriteLine("`列` = `Row` / `DepthOf` を読む、または席を書き換える駒（**対象でも対照でもない**）。");
        Console.WriteLine("`無` = どちらも読まない駒（**対照の母集団**）。");
        Console.WriteLine();
        Console.WriteLine($"**2行以上に出ていない駒は調査から外す**（同じ駒が複数の編成に出ていることが Q2 の前提）。");
        Console.WriteLine();

        var rowsOf = new Dictionary<string, List<string>>();
        var defOf = new Dictionary<string, UnitDef>();
        foreach (var b in s2Builds)
            foreach ((int _, UnitDef d) in b.F.Occupied())
            {
                if (!rowsOf.TryGetValue(d.Name, out var l)) rowsOf[d.Name] = l = new List<string>();
                l.Add(b.Name); defOf[d.Name] = d;
            }

        foreach (string cls in new[] { "隣接", "列", "無" })
        {
            var members = rowsOf.Keys.Where(k => S2Class(defOf[k]) == cls)
                .OrderByDescending(k => rowsOf[k].Count).ThenBy(k => k).ToList();
            Console.WriteLine($"## 分類 `{cls}`（{members.Count} 枚）");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 行数 | 調査対象 | 特性 |");
            Console.WriteLine("|---|--:|:-:|---|");
            foreach (string k in members)
                Console.WriteLine($"| {k} | {rowsOf[k].Count} | {(rowsOf[k].Count >= 2 ? "○" : "**外す**")} "
                    + $"| {string.Join(" / ", defOf[k].Traits)} |");
            Console.WriteLine();
        }

        // ロスターにいるが compare に1行も出ていない駒（＝行数0）
        var absent = UnitCatalog.All.Where(d => !rowsOf.ContainsKey(d.Name)).ToList();
        Console.WriteLine($"## `CompareBuilds()` に1行も出ていない駒（{absent.Count} 枚）");
        Console.WriteLine();
        foreach (var d in absent)
            Console.WriteLine($"- {d.Name}（{S2Class(d)}） — {string.Join(" / ", d.Traits)}");
        Console.WriteLine();
        return;
    }

    // --- 探索本体 ---------------------------------------------------------------------------
    int s2Skip = args.Length > 2 && int.TryParse(args[2], out int sk2) ? sk2 : 0;
    int s2Take = args.Length > 3 && int.TryParse(args[3], out int tk2) ? tk2 : s2Builds.Length;
    var s2Targets = s2Builds.Skip(s2Skip).Take(s2Take).ToArray();

    Console.WriteLine($"# 席の値段（seats2 {s2Skip} {s2Take}）");
    Console.WriteLine();
    Console.WriteLine($"粗探索 seed 0..{S2Scan - 1} の全 120 通り → 検証 seed 0..{S2Verify - 1}。");
    Console.WriteLine("**検証プールは `reseat` の写し（上位20 + 狙い上位10 + 現行）に");
    Console.WriteLine("粗探索の最下位を1つ足したもの**——`幅` を 120 通りの1位と最下位の差として測るため。");
    Console.WriteLine();
    Console.WriteLine("`#ROW` / `#MEM` 行は集計用の機械可読出力（タブ区切り）。");
    Console.WriteLine();

    foreach (var (name, bf) in s2Targets)
    {
        var members = bf.Occupied().Select(x => x.Def).ToList();

        var perms = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation();
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            perms.Add(f);
        }

        var scan = new int[perms.Count];
        for (int i = 0; i < perms.Count; i++)
        {
            int wins = 0;
            foreach (EnemyCatalog.Stage st in s2Stages)
                for (int seed = 0; seed < S2Scan; seed++)
                    if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
            scan[i] = wins;
        }

        var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
        int curIdx = order.First(i => SameFormation(perms[i], bf));

        // reseat の写し + 粗探索の最下位（幅を測るために足した1件）
        var pool = order.Take(S2TopOverall)
            .Concat(order.Where(i => S2MeetsIntent(perms[i])).Take(S2TopConstrained))
            .Append(curIdx)
            .Append(order[^1])
            .Distinct().ToList();

        double S2Avg(Formation f, int seed0, int n)
        {
            double avg = 0;
            foreach (EnemyCatalog.Stage st in s2Stages)
            {
                int wins = 0;
                for (int seed = seed0; seed < seed0 + n; seed++)
                    if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                avg += wins * 100.0 / n;
            }
            return avg / s2Stages.Count;
        }

        var verified = pool.Select(i => (Idx: i, Avg: S2Avg(perms[i], 0, S2Verify)))
            .OrderByDescending(x => x.Avg).ToList();

        // **別 seed 帯での測り直し（200..599 の 400 試行）。** 上位5通りだけを測り直して
        // 1位が入れ替わるかを見る——「最適席」が seed のばらつきで決まっているなら、
        // 席の分散を数えても分散を数えたことにならない。
        var reTop = verified.Take(5).Select(v => (v.Idx, Avg: S2Avg(perms[v.Idx], 200, 400)))
            .OrderByDescending(x => x.Avg).ToList();

        double width = verified[0].Avg - verified[^1].Avg;
        int curRank = verified.FindIndex(v => v.Idx == curIdx) + 1;
        var top5 = verified.Take(5).ToList();

        Console.WriteLine($"## {name}");
        Console.WriteLine();
        Console.WriteLine($"幅 **{width:F1}pt**（1位 {verified[0].Avg:F1}% ／ 最下位 {verified[^1].Avg:F1}%）"
            + $"・現行は検証 {curRank}/{verified.Count} 位（粗 {order.IndexOf(curIdx) + 1}/120 位）"
            + $"・追試（200..599）で1位が {(reTop[0].Idx == verified[0].Idx ? "**保つ**" : "**入れ替わる**")}");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 分類 | 最適席 | 次数 | 上位5の席（中央/角） | 現行席 | 追試の最適席 |");
        Console.WriteLine("|---|:-:|---|--:|---|---|---|");

        double curAvg = verified.First(v => v.Idx == curIdx).Avg;
        double fifthAvg = verified[Math.Min(4, verified.Count - 1)].Avg;
        Console.WriteLine($"#ROW\t{name}\t{width:0.000}\t{verified[0].Avg:0.000}\t{verified[^1].Avg:0.000}"
            + $"\t{order.IndexOf(curIdx) + 1}\t{curRank}\t{verified.Count}\t{curAvg:0.000}\t{fifthAvg:0.000}"
            + $"\t{(reTop[0].Idx == verified[0].Idx ? 1 : 0)}");

        foreach (UnitDef d in members)
        {
            int bestSlot = -1;
            foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                if (ReferenceEquals(dd, d)) bestSlot = slot;
            int curSlot = -1;
            foreach ((int slot, UnitDef dd) in bf.Occupied())
                if (ReferenceEquals(dd, d)) curSlot = slot;

            int mid = 0, corner = 0;
            foreach (var v in top5)
                foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                    if (ReferenceEquals(dd, d)) { if (S2Degree(slot) == 4) mid++; else corner++; }

            // 追試（200..599）で1位になった配置でのこの駒の席
            int reSlot = -1;
            foreach ((int slot, UnitDef dd) in perms[reTop[0].Idx].Occupied())
                if (ReferenceEquals(dd, d)) reSlot = slot;

            Console.WriteLine($"| {d.Name} | {S2Class(d)} | {FormationRules.SeatNames[bestSlot]} "
                + $"| {S2Degree(bestSlot)} | 中央{mid} / 角{corner} | {FormationRules.SeatNames[curSlot]} "
                + $"| {FormationRules.SeatNames[reSlot]} |");
            Console.WriteLine($"#MEM\t{name}\t{d.Name}\t{S2Class(d)}\t{bestSlot}\t{S2Degree(bestSlot)}"
                + $"\t{mid}\t{corner}\t{curSlot}\t{width:0.000}\t{reSlot}\t{S2Degree(reSlot)}");
        }
        Console.WriteLine();
        Console.Out.Flush();
    }
    return;

    // reseat と同じ「狙い」（ガルドは前列 / セッキは後列）。**写しのまま**。
    static bool S2MeetsIntent(Formation f)
    {
        foreach (var (slot, def) in f.Occupied())
        {
            if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
            if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
        }
        return true;
    }
}
}
