using System.Reflection;
using System.Text.RegularExpressions;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// =====================================================================================
// inv414 —— 第414期【数えるだけの期・Phase 0 だけ】デバフ軸の立て直しの後の軸の横並び（突出していないかの確認）。
// 指示書は design/PHASE414_AXES_RECHECK_SPEC.md ／ 報告は design/PHASE414_AXES_RECHECK.md。**規則を1つも変えない。版も並べない。**
//
//     dotnet run --project BattleSim -c Release 0 inv414 q1    # 軸の横並び（第348期 §3 の形・いまの規定・デバフの行は「試遊・呪い」「試遊・カーズ 呪い」）＋ 第348期の表との差 ＋ デバフの行の順位
//     dotnet run --project BattleSim -c Release 0 inv414 q2    # 「試遊・呪い」× 4 波: 誰の与ダメが何割か（直 ／ 鎖）・ムドの M-u 由来・ウツ ／ ムドを素体に替えた台
//     dotnet run --project BattleSim -c Release 0 inv414 q3    # 速攻 (ボルグ×ムド) × 近衛 ／ 大隊のムド・各軸の代表台の1枚をムドに替えた台 × 4 波
//     dotnet run --project BattleSim -c Release 0 inv414 q4    # カーズ台との差・カーズが受けた量 ／ 倒れたT・ワタの死と倒しT・保留在庫の一覧
//     dotnet run --project BattleSim -c Release 0 inv414 all   # q1〜q4
//
// 1戦は `inv348` の `Raw`（計数の規則あり）。代表台は `inv348` の並びと同じで、**古い期への固定（`Pin349`）を外した今の規定**。
// =====================================================================================
static class Inv414Diag
{
    const int Seeds = 100, TT = 10;
    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static readonly I.W[] Four = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "all";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "q1": Q1(); break;
            case "q2": Q2(); break;
            case "q3": Q3(); break;
            case "q4": Q4(); break;
            case "all": Q1(); Q2(); Q3(); Q4(); break;
            default: Console.WriteLine("inv414: モードは q1 / q2 / q3 / q4 / all。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 代表台（inv348 の並び・今の規定）
    // ---------------------------------------------------------------------------------
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;
    static Formation Play(string n) => Presets.Playtest.Concat(Presets.Versions).First(r => r.Name == n).F;
    internal static I.Board[] Boards() => I.Boards().Where(b => b.Axis != "デバフ").Select(b => new I.Board(b.Axis, b.Name,
            b.Name == "移動 (チェック波の台)" ? I.MoveCheck : CompareBuilds().Concat(CrossBuilds()).Any(r => r.Name == b.Name) ? Row(b.Name) : Play(b.Name)))
        .Append(new I.Board("デバフ", "試遊・呪い", Play("試遊・呪い")))
        .Append(new I.Board("デバフ", "試遊・カーズ 呪い", Play("試遊・カーズ 呪い"))).ToArray();
    static readonly string[] AxisOrder = I.AxisOrder;

    /// <summary>第348期以降に規定を作り替えた駒（`UnitCatalog` に `{名前}H{期}`（期 ≥ 348）の旧が残っている駒）。</summary>
    static string ChangedSince348(UnitDef d)
    {
        var fields = typeof(UnitCatalog).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(UnitDef)).ToArray();
        var own = fields.FirstOrDefault(f => ReferenceEquals(f.GetValue(null), d))?.Name;
        if (own is null) return "";
        var hs = fields.Select(f => Regex.Match(f.Name, "^" + Regex.Escape(own) + @"H(\d{3})$")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).Where(n => n >= 348).OrderBy(n => n).ToArray();
        return hs.Length == 0 ? "" : $"{Short(d)}（旧 {string.Join(" ／ ", hs.Select(n => $"H{n}"))}）";
    }

    // ---------------------------------------------------------------------------------
    // Q1 軸の横並び
    // ---------------------------------------------------------------------------------
    internal static readonly string[] Cols = { "ボス 勝率", "ボス 倒しT", "近衛", "大隊", "本編2", "本編4", "本編5", "九体", "的・一 8T", "的・九 8T", "強 2〜5" };
    internal static double[] Vals(Dictionary<string, I.St> r) => new[]
    {
        r[I.Boss.Name].Win, r[I.Boss.Name].Wins == 0 ? double.NegativeInfinity : -(double)r[I.Boss.Name].WinT / r[I.Boss.Name].Wins,
        r[I.Guard.Name].Win, r[I.Bat.Name].Win, r[I.M2.Name].Win, r[I.M4.Name].Win, r[I.M5.Name].Win, r[I.Nine.Name].Win,
        (double)r[I.T1.Name].Dmg8 / r[I.T1.Name].N, (double)r[I.T9.Name].Dmg8 / r[I.T9.Name].N, I.StrongWaves.Average(w => r[w.Name].Win),
    };
    internal static string Fmt(int i, double v) => i == 1 ? (double.IsNegativeInfinity(v) ? "—" : F1(-v)) : i is 8 or 9 ? v.ToString("N0") : F1(v);
    static string Norm(string s) => s.Replace("★", "").Replace("**", "").Replace(",", "").Trim();

    static void Q1()
    {
        var boards = Boards();
        var res = boards.ToDictionary(b => b.Name, b => I.AxisWaves.ToDictionary(w => w.Name, w => I.Measure(b.F, w)));
        Console.WriteLine($"# 第414期 Q1 軸の横並び —— 代表台 {boards.Length} × 物差し {I.AxisWaves.Length} 波・seed 0..{Seeds - 1}（いまの規定・`inv348` の並びから古い期への固定を外した）");
        Console.WriteLine();
        Console.WriteLine("台の並び（前1・前3・中央・後1・後3）と、第348期の後に規定を作り替えた駒:");
        Console.WriteLine();
        foreach (var b in boards)
        {
            string ch = string.Join("・", b.F.Occupied().Select(o => ChangedSince348(o.Def)).Where(s => s != ""));
            Console.WriteLine($"- **{b.Axis}** {b.Name}: {S307.Seats(b.F)}{(ch == "" ? "" : $"——作り替え {ch}")}");
        }
        Console.WriteLine();
        var val = boards.ToDictionary(b => b.Name, b => Vals(res[b.Name]));
        var axisVal = AxisOrder.ToDictionary(a => a, a => Enumerable.Range(0, Cols.Length).Select(i => boards.Where(b => b.Axis == a).Max(b => val[b.Name][i])).ToArray());
        string Star(string a, int i) { double v = axisVal[a][i]; return v == AxisOrder.Max(x => axisVal[x][i]) && !double.IsNegativeInfinity(v) ? " ★" : ""; }

        Console.WriteLine("## 1-1. 攻め（軸ごとの1枚 ＝ 代表台2つの良いほう・★ ＝ 1位・第348期 §3-1 と同じ形）");
        Console.WriteLine();
        Console.WriteLine("| 軸 | " + string.Join(" | ", Cols) + " |");
        Console.WriteLine("|---|" + string.Concat(Cols.Select(_ => "--:|")));
        foreach (var a in AxisOrder) Console.WriteLine($"| {a} | " + string.Join(" | ", Enumerable.Range(0, Cols.Length).Select(i => Fmt(i, axisVal[a][i]) + Star(a, i))) + " |");
        Console.WriteLine();

        Console.WriteLine("## 1-2. 台ごと（2台とも）");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 台 | " + string.Join(" | ", Cols) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Cols.Select(_ => "--:|")));
        foreach (var b in boards) Console.WriteLine($"| {b.Axis} | {b.Name} | " + string.Join(" | ", Enumerable.Range(0, Cols.Length).Select(i => Fmt(i, val[b.Name][i]))) + " |");
        Console.WriteLine();
        Console.WriteLine("精鋭 ／ 九体の決着T（勝った戦の平均・台ごと）: " + string.Join("・", boards.Select(b => $"{b.Name} {res[b.Name][I.Guard.Name].WinTs} ／ {res[b.Name][I.Bat.Name].WinTs} ／ {res[b.Name][I.Nine.Name].WinTs}T")) + "。");
        Console.WriteLine();

        I.W[] defW = { I.Boss, I.Bat, I.M5 };
        var fellAxis = defW.ToDictionary(w => w.Name, w => AxisOrder.ToDictionary(a => a, a => boards.Where(b => b.Axis == a).Min(b => (double)res[b.Name][w.Name].AllyFell / res[b.Name][w.Name].N)));
        Console.WriteLine("## 1-3. 守り（倒れた味方の数 ／戦・軸ごとの少ないほう・★ ＝ 最少・第348期 §3-2 と同じ形）");
        Console.WriteLine();
        Console.WriteLine("| 軸 | ボス | 大隊 | 本編 第5波 | ボスの最初に倒れた T（倒れた戦の割合・台ごと） |");
        Console.WriteLine("|---|--:|--:|--:|---|");
        foreach (var a in AxisOrder)
        {
            var cells = defW.Select(w => { double f = fellAxis[w.Name][a]; return f.ToString("F2") + (f == fellAxis[w.Name].Values.Min() ? " ★" : ""); });
            string first = string.Join(" ／ ", boards.Where(b => b.Axis == a).Select(b => { var s = res[b.Name][I.Boss.Name]; return $"{Per(s.FirstFellSum, s.FirstFellN)}T（{Pct(s.FirstFellN, s.N)}%）"; }));
            Console.WriteLine($"| {a} | {string.Join(" | ", cells)} | {first} |");
        }
        Console.WriteLine();

        // 第348期の表との差
        var old = Read348();
        Console.WriteLine("## 1-4. 第348期の表（§3-1 攻め ／ §3-2 守り）との差（動いたセルだけ・旧 → 今）");
        Console.WriteLine();
        int moved = 0;
        foreach (var a in AxisOrder)
        {
            var diffs = new List<string>();
            if (old.Attack.TryGetValue(a, out var oa))
                for (int i = 0; i < Cols.Length && i < oa.Length; i++)
                {
                    string now = Fmt(i, axisVal[a][i]);
                    if (Norm(oa[i]) != Norm(now)) diffs.Add($"{Cols[i]} {Norm(oa[i])} → {now}");
                }
            if (old.Defense.TryGetValue(a, out var od))
                for (int i = 0; i < defW.Length && i < od.Length; i++)
                {
                    string now = fellAxis[defW[i].Name][a].ToString("F2");
                    if (Norm(od[i]) != now) diffs.Add($"倒れた（{defW[i].Name}）{Norm(od[i])} → {now}");
                }
            moved += diffs.Count;
            Console.WriteLine($"- **{a}**: {(diffs.Count == 0 ? "動かない" : string.Join("・", diffs))}");
        }
        Console.WriteLine();
        Console.WriteLine($"動いたセル 計 {moved}（攻め 11 ＋ 守り 3 の 14 列 × 10 軸）。");
        Console.WriteLine();

        // ムドのいる台（ムドの規定化の分を切り分ける）
        var withMudo = boards.Where(x => x.Axis != "デバフ" && x.F.Occupied().Any(o => o.Def.Id == "mudo")).ToArray();
        if (withMudo.Length > 0)
        {
            Console.WriteLine("## 1-4b. ムドのいる他の軸の台を、ムドだけ旧（`MudoH412`・M1＋M2）に戻した値（ムドの規定化の分の切り分け）");
            Console.WriteLine();
            Console.WriteLine("| 台 | ムド | " + string.Join(" | ", Cols) + " |");
            Console.WriteLine("|---|---|" + string.Concat(Cols.Select(_ => "--:|")));
            foreach (var x in withMudo)
            {
                var g = x.F.Clone();
                foreach (var o in x.F.Occupied()) if (o.Def.Id == "mudo") g[o.Slot] = UnitCatalog.MudoH412;
                var vo = Vals(I.AxisWaves.ToDictionary(w => w.Name, w => I.Measure(g, w)));
                Console.WriteLine($"| {x.Name} | いま | " + string.Join(" | ", Enumerable.Range(0, Cols.Length).Select(i => Fmt(i, val[x.Name][i]))) + " |");
                Console.WriteLine($"| {x.Name} | 旧 | " + string.Join(" | ", Enumerable.Range(0, Cols.Length).Select(i => Fmt(i, vo[i]))) + " |");
            }
            Console.WriteLine();
        }

        // デバフの行の順位
        var others = boards.Where(b => b.Axis != "デバフ").GroupBy(b => b.Axis).ToDictionary(g => g.Key, g => Enumerable.Range(0, Cols.Length).Select(i => g.Max(b => val[b.Name][i])).ToArray());
        Console.WriteLine($"## 1-5. 物差しごとの順位（ほかの軸 {others.Count} 本の1枚 ＋ その行・同じ値は上の順位・`db351 axes` の形）");
        Console.WriteLine();
        Console.WriteLine("| デバフの行 | " + string.Join(" | ", Cols) + " | 1位の数 |");
        Console.WriteLine("|---|" + string.Concat(Cols.Select(_ => "--:|")) + "--:|");
        foreach (var b in boards.Where(b => b.Axis == "デバフ"))
        {
            var v = val[b.Name];
            var ranks = Enumerable.Range(0, Cols.Length).Select(i => 1 + others.Values.Count(x => x[i] > v[i])).ToArray();
            int solo = Enumerable.Range(0, Cols.Length).Count(i => others.Values.All(x => x[i] < v[i]));
            Console.WriteLine($"| {b.Name} | {string.Join(" | ", ranks)} | {ranks.Count(r => r == 1)}（単独 {solo}） |");
        }
        Console.WriteLine();
    }

    static (Dictionary<string, string[]> Attack, Dictionary<string, string[]> Defense) Read348()
    {
        var attack = new Dictionary<string, string[]>(); var defense = new Dictionary<string, string[]>();
        string path = Path.Combine("design", "PHASE348_AXIS_INVENTORY.md");
        if (!File.Exists(path)) return (attack, defense);
        var lines = File.ReadAllLines(path);
        int a = Array.FindIndex(lines, l => l.StartsWith("### 3-1.")), d = Array.FindIndex(lines, l => l.StartsWith("### 3-2.")), e = Array.FindIndex(lines, l => l.StartsWith("## 4."));
        foreach (var (from, to, dict, n) in new[] { (a, d, attack, 11), (d, e, defense, 3) })
            for (int k = from; k >= 0 && k < to; k++)
            {
                var c = lines[k].Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length < n + 3 || !AxisOrder.Contains(c[1])) continue;
                dict[c[1]] = c.Skip(2).Take(n).ToArray();
            }
        return (attack, defense);
    }

    // ---------------------------------------------------------------------------------
    // Q2 デバフ軸の速さの中身
    // ---------------------------------------------------------------------------------
    sealed class Dx
    {
        public long N, Wins, WinT;
        public readonly Dictionary<string, long> Dir = new(), Share = new(), Scar = new();
        public long MDmg, MFuel, MCurse, MStrike;
        public void Add(Dx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; MDmg += o.MDmg; MFuel += o.MFuel; MCurse += o.MCurse; MStrike += o.MStrike;
            foreach (var (k, v) in o.Dir) Dir[k] = Dir.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Share) Share[k] = Share.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Scar) Scar[k] = Scar.GetValueOrDefault(k) + v;
        }
        public string WT => $"{F1(N == 0 ? 0 : 100.0 * Wins / N)}・{(Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0"))}T";
    }
    static Dx DmgOne(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Dx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var mine = p.ToDictionary(u => u.InstanceId, u => Short(u.Def));
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team != BattleContext.PlayerTeam) foes.Add(sid);
        foreach (var x in r.Events)
        {
            if (x.Kind == BattleEventKind.Damage && x.Amount > 0 && x.TargetId is int ti && foes.Contains(ti) && x.ActorId is int ai && mine.TryGetValue(ai, out var who))
            {
                if (x.ShareFromId is null) a.Dir[who] = a.Dir.GetValueOrDefault(who) + x.Amount; else a.Share[who] = a.Share.GetValueOrDefault(who) + x.Amount;
            }
            if (x.Kind == BattleEventKind.Scar && x.ActorId is int si && mine.TryGetValue(si, out var sw)) a.Scar[sw] = a.Scar.GetValueOrDefault(sw) + x.Amount;
        }
        if (r.TallyByUnit.TryGetValue("mudo", out var tm)) { a.MDmg = tm.DamageToEnemy; a.MFuel = tm.EruptFuel; a.MCurse = tm.Db354CurseRage; a.MStrike = tm.Mu412Rage; }
        return a;
    }
    static Dx DmgMeasure(Formation f, I.W w)
    {
        var parts = new Dx[Seeds];
        Parallel.For(0, Seeds, s => parts[s] = DmgOne(f, w, s));
        var all = new Dx(); foreach (var x in parts) all.Add(x); return all;
    }
    static Formation SwapId(Formation f, string id, UnitDef to)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == id) g[o.Slot] = to;
        return g;
    }
    static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + Short(d), MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances, Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };

    static void Q2()
    {
        var b = Play("試遊・呪い");
        var old = SwapId(b, "mudo", UnitCatalog.MudoH412);
        Console.WriteLine($"# 第414期 Q2 デバフ軸の速さの中身 —— 「試遊・呪い」（{S307.Seats(b)}）× {string.Join(" ／ ", Four.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("与ダメ ＝ 台本の Damage（味方 → 敵）。直 ＝ 共有でない一撃・鎖 ＝ 呪いの共有で届いた分（`ShareFromId` あり・書き手は元の一撃を打った駒）。"
            + "旧 ＝ 同じ台のムドだけ規定化の前（`MudoH412`・M1＋M2）。削った最大HP ＝ 台本の Scar（第407期の指示書が引いた「ウツ 2,430 ／ ムド 537」は第356期のボスの削った最大HPで、与ダメではない）。");
        Console.WriteLine();
        string[] who = { "ウツ", "ムド", "ネル", "クビ", "ワタ" };
        foreach (var (label, f) in new[] { ("いま（M-ud）", b), ("旧のムド（M1＋M2）", old) })
        {
            Console.WriteLine($"## 2-1. 与ダメの割合（戦あたり・直 ＋ 鎖 ＝ 計・割合）—— {label}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率・倒しT | " + string.Join(" | ", who) + " | 全員 |");
            Console.WriteLine("|---|---|" + string.Concat(who.Select(_ => "---|")) + "--:|");
            foreach (var w in Four)
            {
                var a = DmgMeasure(f, w);
                long all = a.Dir.Values.Sum() + a.Share.Values.Sum();
                Console.WriteLine($"| {w.Name} | {a.WT} | " + string.Join(" | ", who.Select(k =>
                {
                    long d = a.Dir.GetValueOrDefault(k), s = a.Share.GetValueOrDefault(k);
                    return $"{Per(d, a.N)} ＋ {Per(s, a.N)}（{Pct(d + s, all)}%）";
                })) + $" | {Per(all, a.N)} |");
            }
            Console.WriteLine();
            Console.WriteLine($"削った最大HP（戦あたり・{label}）: " + string.Join("・", Four.Select(w => { var a = DmgMeasure(f, w); return $"{w.Name} " + string.Join(" ／ ", who.Where(k => a.Scar.GetValueOrDefault(k) > 0).Select(k => $"{k} {Per(a.Scar[k], a.N)}")); })) + "。");
            Console.WriteLine();
        }

        Console.WriteLine("## 2-2. ムドの与ダメのうち M-u（呪いの一撃からの怒り）由来の分（怒りの内訳 殴られて ／ 呪いから ／ 一撃から の比で按分・出力であって門ではない）");
        Console.WriteLine();
        Console.WriteLine("| 波 | ムドの与ダメ | 怒り 殴られて ／ 呪いから ／ 一撃から | 一撃から由来の与ダメ（割合） |");
        Console.WriteLine("|---|--:|---|---|");
        foreach (var w in Four)
        {
            var a = DmgMeasure(b, w);
            long tot = a.MFuel + a.MCurse + a.MStrike;
            double share = tot == 0 ? 0 : (double)a.MStrike / tot;
            Console.WriteLine($"| {w.Name} | {Per(a.MDmg, a.N)} | {Per(a.MFuel, a.N)} ／ {Per(a.MCurse, a.N)} ／ {Per(a.MStrike, a.N)} | {F1(a.MDmg * share / a.N)}（{F1(100 * share)}%） |");
        }
        Console.WriteLine();

        Console.WriteLine("## 2-3. ムドが一人で勝つか —— ウツ ／ ムドを素体（同じ HP ／ 攻 ／ 速 ／ 型・札なし）に替えた台（勝率・倒しT ／ 倒れた味方 ／戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Four.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Four.Select(_ => "---|")));
        foreach (var (label, f) in new[] { ("いま", b), ("ウツを素体に", SwapId(b, "utsu", Plain(UnitCatalog.Utsu))), ("ムドを素体に", SwapId(b, "mudo", Plain(UnitCatalog.Mudo))), ("両方を素体に", SwapId(SwapId(b, "utsu", Plain(UnitCatalog.Utsu)), "mudo", Plain(UnitCatalog.Mudo))) })
            Console.WriteLine($"| {label} | " + string.Join(" | ", Four.Select(w => { var s = I.Measure(f, w); return $"{F1(s.Win)}・{s.WinTs}T ／ {Per2(s.AllyFell, s.N)}"; })) + " |");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Q3 他の軸の台にムドを入れる
    // ---------------------------------------------------------------------------------
    static void Q3()
    {
        Console.WriteLine("# 第414期 Q3 他の軸の台にムドを入れると何が起きるか");
        Console.WriteLine();
        var fast = Row("速攻 (ボルグ×ムド)");
        Console.WriteLine($"## 3-1. 速攻 (ボルグ×ムド)（{S307.Seats(fast)}・呪いの書き手はネル1枚）× 近衛 ／ 大隊・seed 0..{Seeds - 1}（いま ／ 旧のムド M1＋M2）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 勝率・倒しT | 暴発 回 ／戦 | 怒り 殴られて ／ 呪いから ／ 一撃から | ムドの与ダメ | ムドが倒れた割合・T |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|");
        foreach (var (label, f) in new[] { ("いま", fast), ("旧のムド", SwapId(fast, "mudo", UnitCatalog.MudoH412)) })
            foreach (var w in new[] { I.Guard, I.Bat })
            {
                // `UnitTally.Add` は暴発の欄を足さない（第348期）ので、1戦ごとの帳簿から直に数える
                var parts = new long[Seeds][];
                Parallel.For(0, Seeds, seed =>
                {
                    var (r, p, _) = I.Raw(f, w, seed);
                    var m = p.First(u => u.Def.Id == "mudo");
                    var t = r.TallyByUnit["mudo"];
                    parts[seed] = new long[] { r.PlayerWon ? 1 : 0, r.PlayerWon ? r.Turns : 0, t.EruptFires, t.EruptFuel, t.Db354CurseRage, t.Mu412Rage, t.DamageToEnemy, m.LastDeathTurn > 0 ? 1 : 0, m.LastDeathTurn };
                });
                long Sum(int k) => parts.Sum(x => x[k]);
                Console.WriteLine($"| {label} | {w.Name} | {Pct(Sum(0), Seeds)}・{Per(Sum(1), Sum(0))}T | {Per(Sum(2), Seeds)} | {Per(Sum(3), Seeds)} ／ {Per(Sum(4), Seeds)} ／ {Per(Sum(5), Seeds)} | {Per(Sum(6), Seeds)} | {Pct(Sum(7), Seeds)}%・T{Per(Sum(8), Sum(7))} |");
            }
        Console.WriteLine();

        Console.WriteLine($"## 3-2. 各軸の代表台（1台目）の1枚をムドに替えた台 × {string.Join(" ／ ", Four.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("席はその駒の席のまま。5 席それぞれを替えた台を測り、4 波の平均勝率が最も上がった席を載せる（同じなら倒しT の和が小さい方）。ムドが元からいる台はその席を除く。");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 台 | いま（4 波の勝率・平均） | 最良の替え（外した駒） | 替えた後（4 波の勝率・平均） | 差 | 上がった席の数 ／ 替えた席 |");
        Console.WriteLine("|---|---|---|---|---|--:|---|");
        int up = 0;
        foreach (var g in Boards().Where(b => b.Axis != "デバフ").GroupBy(b => b.Axis))
        {
            var b = g.First();
            double Mean(St4 s) => s.Win.Average();
            var now = M4(b.F);
            var tries = b.F.Occupied().Where(o => o.Def.Id != "mudo").Select(o =>
            {
                var f = b.F.Clone(); f[o.Slot] = UnitCatalog.Mudo;
                return (Out: o.Def, S: M4(f));
            }).ToList();
            var best = tries.OrderByDescending(t => Mean(t.S)).ThenBy(t => t.S.T.Sum()).First();
            int better = tries.Count(t => Mean(t.S) > Mean(now) + 1e-9);
            double d = Mean(best.S) - Mean(now);
            if (d > 1e-9) up++;
            Console.WriteLine($"| {b.Axis} | {b.Name} | {string.Join(" ／ ", now.Win.Select(F1))}（{F1(Mean(now))}） | {Short(best.Out)} | {string.Join(" ／ ", best.S.Win.Select(F1))}（{F1(Mean(best.S))}） | {d:+0.0;-0.0;0.0} | {better} ／ {tries.Count} |");
        }
        Console.WriteLine();
        Console.WriteLine($"ムドを入れて 4 波の平均勝率が上がる軸: **{up}** ／ 9。");
        Console.WriteLine();
    }
    sealed record St4(double[] Win, double[] T);
    static St4 M4(Formation f)
    {
        var s = Four.Select(w => I.Measure(f, w)).ToArray();
        return new St4(s.Select(x => x.Win).ToArray(), s.Select(x => x.Wins == 0 ? 30.0 : (double)x.WinT / x.Wins).ToArray());
    }

    // ---------------------------------------------------------------------------------
    // Q4 カーズとウケの材料
    // ---------------------------------------------------------------------------------
    sealed class Wx { public long N, Wins, WinT, WataDied, WataDeathT, KarzTaken, KarzDied, KarzDeathT, WonDiedN, WonDiedT, WonAliveN, WonAliveT; }
    static void Q4()
    {
        Console.WriteLine("# 第414期 Q4 カーズとウケの材料");
        Console.WriteLine();
        var play = Play("試遊・呪い"); var karz = Play("試遊・カーズ 呪い");
        Console.WriteLine($"## 4-1. 「試遊・呪い」（カーズ無し）と「試遊・カーズ 呪い」× 4 波・seed 0..{Seeds - 1}（勝率・倒しT ／ 倒れた味方 ／戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Four.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Four.Select(_ => "---|")));
        foreach (var (label, f) in new[] { ("試遊・呪い", play), ("試遊・カーズ 呪い", karz) })
            Console.WriteLine($"| {label} | " + string.Join(" | ", Four.Select(w => { var s = I.Measure(f, w); return $"{F1(s.Win)}・{s.WinTs}T ／ {Per2(s.AllyFell, s.N)}"; })) + " |");
        Console.WriteLine();
        Console.WriteLine("カーズ台のカーズ（戦あたり）: " + string.Join("・", Four.Select(w =>
        {
            var s = I.Measure(karz, w); var t = s.T("kari"); long died = s.Deaths.GetValueOrDefault("kari");
            return $"{w.Name} 受けた量 {Per(t.DamageTaken, s.N)}・倒れた {Pct(died, s.N)}%・T{Per(s.DeathT.GetValueOrDefault("kari"), died)}・印 {Per(t.K409Marks, s.N)}";
        })) + "。");
        Console.WriteLine();

        Console.WriteLine($"## 4-2. ワタの死（「試遊・呪い」× 4 波・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率・倒しT | ワタが倒れた割合・T | ワタが倒れた戦の倒しT（勝った戦）| 倒れなかった戦の倒しT（勝った戦） |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var w in Four)
        {
            var parts = new Wx[Seeds];
            Parallel.For(0, Seeds, seed =>
            {
                var (r, p, _) = I.Raw(play, w, seed);
                var wata = p.First(u => u.Def.Id == "wata");
                var x = new Wx { N = 1 };
                if (r.PlayerWon) { x.Wins = 1; x.WinT = r.Turns; }
                if (wata.LastDeathTurn > 0) { x.WataDied = 1; x.WataDeathT = wata.LastDeathTurn; if (r.PlayerWon) { x.WonDiedN = 1; x.WonDiedT = r.Turns; } }
                else if (r.PlayerWon) { x.WonAliveN = 1; x.WonAliveT = r.Turns; }
                parts[seed] = x;
            });
            var a = new Wx();
            foreach (var x in parts) { a.N += x.N; a.Wins += x.Wins; a.WinT += x.WinT; a.WataDied += x.WataDied; a.WataDeathT += x.WataDeathT; a.WonDiedN += x.WonDiedN; a.WonDiedT += x.WonDiedT; a.WonAliveN += x.WonAliveN; a.WonAliveT += x.WonAliveT; }
            Console.WriteLine($"| {w.Name} | {Pct(a.Wins, a.N)}・{Per(a.WinT, a.Wins)}T | {Pct(a.WataDied, a.N)}%・T{Per(a.WataDeathT, a.WataDied)} | {Per(a.WonDiedT, a.WonDiedN)}T（{a.WonDiedN} 戦） | {Per(a.WonAliveT, a.WonAliveN)}T（{a.WonAliveN} 戦） |");
        }
        Console.WriteLine();

        Console.WriteLine("## 4-3. 保留在庫（`All` の外・保持者 0 枚・定義は残す）");
        Console.WriteLine();
        Console.WriteLine("| 定義名 | 駒 | 期 | 一言 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (field, phase, note) in Reserve)
        {
            var d = (UnitDef?)typeof(UnitCatalog).GetField(field, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            string traits = d is null ? "**定義が無い**" : string.Join(" ＋ ", d.Traits.Skip(Math.Max(0, d.Traits.Count - 2)));
            Console.WriteLine($"| `{field}` | {(d is null ? "—" : Short(d))}（{traits}） | {phase} | {note} |");
        }
        Console.WriteLine();
    }
    static readonly (string Field, string Phase, string Note)[] Reserve =
    {
        ("KariKk", "第408期", "K-k 刻む: 呪われた敵を累計 ÷ 4 発で刻む（刻むたび自分の攻撃 −1）"),
        ("KariKd", "第408期", "K-d 駆り立てる: 呪われた後列の敵を前へ引きずり出し、殴るたび −4・的をカリに固定"),
        ("KariKd1", "第409期", "K-d1 往復止め: K-d に「同じ敵は一度だけ」"),
        ("KariKz62", "第410期", "KZ-62: 規定のカーズの札で HP 62（旧のカリの HP）"),
        ("WataWu", "第413期", "W-u 刃を呼ぶ: 押し付けた手番に、最も呪われた味方がその敵を殴る（鳴るのはボスの T2・T3 だけ）"),
        ("WataWm", "第413期", "W-m 泥を呼ぶ: 押し付けた手番に、ムドが溜めた怒りを吐く（倒しT は動かない）"),
        ("UkeUKr", "第351期", "UK-r 奪う: ウケの転生（背負うを外し、殴った呪われた敵から力を奪う `Plunder`）。ウケ本体は第385期にロスターから外れた"),
        ("UkeUKc", "第353期", "UK-c 奪う・累計: UK-r の作り直し（敵の弱体の累計を読む `PlunderCum`）"),
        ("MudoH412", "第413期", "第355〜412期の規定のムド（M1＋M2）。封印した 42 本を起こすときに使う"),
    };
}
