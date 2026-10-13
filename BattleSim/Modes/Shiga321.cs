using System.Reflection;
using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S318 = Som318Diag;

// =====================================================================================
// shiga321 —— 第321期 段1「シガの蓄電が育つ」（SA ／ SA+ ／ SA+k）。
// 指示書は design/PHASE321_SHIGA_CHARGE_HISA_BURST_SPEC.md ／ 報告は design/PHASE321_SHIGA_CHARGE_HISA_BURST.md。
//
//     dotnet run --project BattleSim -c Release 0 shiga321 p0                 # §1-3 Phase 0（規定のシガ）: 蓄電の付き方 ／ 使い方 ／ ターンごとの値・鞭の内訳・割り込みが蓄電不足で起きなかった回数
//     dotnet run --project BattleSim -c Release 0 shiga321 boards [版,…]      # §1-5 ／ §1-6: 代表台 5 × 10 波 × 版（規定 ／ SA ／ SA+ ／ SA+k）と対照（シガ → ドルガ）
//     dotnet run --project BattleSim -c Release 0 shiga321 compare [版,…]     # §1-7: `compare` 64 行 × 版（(G2) つき・基準は規定）
//     dotnet run --project BattleSim -c Release 0 shiga321 grid <boss|guard> <版>   # §1-7: シガのいる台の格子（第316期と同じ作り・固定枠 シガ ＋ カタ ＋ ソム・駒はいまの規定）
//     dotnet run --project BattleSim -c Release 0 shiga321 memo <台の一部> <波> <seed> <版>   # §3: 連撃の台本の並び（シガの Attack ／ Damage ／ ゲージ）
//     dotnet run --project BattleSim -c Release 0 shiga321 check              # §4 自己検査
//
// 版の名前（ASCII）: base（規定）／ sa ／ sap（SA+）／ sapk（SA+k）。波: boss ／ guard ／ bat ／ nine ／ nine2 ／ 1..5。
// **予測のファイルが無ければ本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p321/predict.md`）。
// =====================================================================================
static class Shiga321Diag
{
    internal const string PredictFile = ".tmp/p321/predict.md";
    const int Seeds = 200;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"shiga321: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "compare": CompareAll(A(3, "")); return;
            case "grid": Grid(A(3, "boss"), A(4, "sap")); return;
            case "memo": Memo(A(3, "鞭"), A(4, "boss"), int.Parse(A(5, "7")), A(6, "sap")); return;
            case "check": Check(); return;
            default: Console.WriteLine("shiga321: モードは p0 / boards / compare / grid / memo / check。"); return;
        }
    }

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef Def);
    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.Shiga),
        new("SA", "sa", UnitCatalog.ShigaSA),
        new("SA+", "sap", UnitCatalog.ShigaSAp),
        new("SA+k", "sapk", UnitCatalog.ShigaSApk),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    static Formation Apply(Formation f, UnitDef d) => S316.SwapId(f, "shiga", d);

    /// <summary>波（第310期からの 10 波: ボス ／ 近衛 ／ 大隊 ／ 九体 新兵 ／ 九体 農兵 ／ 本編の5波）。</summary>
    internal static readonly S287.Wave[] Waves = S318.Waves;
    internal static S287.Wave WaveOf(string n) => n switch { "nine" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };
    static bool IsElite(S287.Wave w) => w.Name is "近衛" or "大隊" || w.Name.StartsWith("九体", StringComparison.Ordinal);

    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;

    /// <summary>§1-5 の代表台（いまの規定の駒・席は出典のまま）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("光の盾 鞭", Playtest("試遊・感電 光の盾 鞭")),
        ("打ち返し", Playtest("試遊・感電 打ち返し")),
        ("雷の型", Playtest("試遊・感電 雷の型")),
        ("責め苦", Row("責め苦 (トウ×シガ)")),
        ("裂き×責め苦", Row("裂き×責め苦 (キリ×エグ×シガ)")),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（第318期の計数 `S318.Count` ＋ シガの蓄電・鞭）
    // ---------------------------------------------------------------------------------
    static readonly int[] Marks = { 5, 10, 15, 20 };

    internal sealed class X
    {
        public long N, ShigaN, ShigaLife, OneT;
        public long Dmg, IntDmg, BackDmg, TurnSw, IntSw, BackSw, LaterSw, SwAsked, NoCharge, IntFires, Spent, EndCharge, GainsAll, Capped;
        public long SwMax, ChargePeak, TurnMax;
        public long[] ChargeAt = new long[4], ChargeAtN = new long[4];
        public Dictionary<string, long> GainBy = new();
        public void Merge(X o)
        {
            N += o.N; ShigaN += o.ShigaN; ShigaLife += o.ShigaLife; OneT += o.OneT;
            Dmg += o.Dmg; IntDmg += o.IntDmg; BackDmg += o.BackDmg; TurnSw += o.TurnSw; IntSw += o.IntSw; BackSw += o.BackSw; LaterSw += o.LaterSw;
            SwAsked += o.SwAsked; NoCharge += o.NoCharge; IntFires += o.IntFires; Spent += o.Spent; EndCharge += o.EndCharge; GainsAll += o.GainsAll; Capped += o.Capped;
            SwMax = Math.Max(SwMax, o.SwMax); ChargePeak = Math.Max(ChargePeak, o.ChargePeak); TurnMax = Math.Max(TurnMax, o.TurnMax);
            for (int i = 0; i < 4; i++) { ChargeAt[i] += o.ChargeAt[i]; ChargeAtN[i] += o.ChargeAtN[i]; }
            foreach (var (k, v) in o.GainBy) GainBy[k] = GainBy.GetValueOrDefault(k) + v;
        }
    }

    internal static (S318.St S, X X) Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        return (S318.Count(p, e, r), CountX(p, e, r));
    }

    internal static X CountX(List<UnitState> p, List<UnitState> e, BattleResult r)
    {
        var x = new X { N = 1 };
        if (r.PlayerWon && r.Turns <= 1) x.OneT = 1;
        UnitState? sh = p.FirstOrDefault(u => u.Def.Id == "shiga");
        if (sh is null) return x;
        x.ShigaN = 1;
        var mine = p.ToDictionary(u => u.InstanceId, u => u);
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        int charge = 0, life = r.Turns;
        var perTurn = new Dictionary<int, long>();
        int mark = 0;
        void Snap(int uptoTurn)
        {
            while (mark < Marks.Length && Marks[mark] <= uptoTurn)
            {
                if (r.Turns >= Marks[mark]) { x.ChargeAt[mark] += charge; x.ChargeAtN[mark]++; }
                mark++;
            }
        }
        foreach (var ev in r.Events)
        {
            Snap(ev.Turn - 1);   // そのターンの終わりの値 ＝ 次のターンの最初の出来事の手前
            switch (ev.Kind)
            {
                case BattleEventKind.ShockGauge when ev.TargetId == sh.InstanceId && ev.Text is ShockGaugeLabels.ChargeGain or ShockGaugeLabels.ChargeSpent or ShockGaugeLabels.ChargeDrained:
                    if (ev.Text == ShockGaugeLabels.ChargeGain)
                    {
                        x.GainsAll++;
                        string k = ev.ActorId is int a && mine.TryGetValue(a, out var au) ? (a == sh.InstanceId ? "自分" : Short(au.Def)) : ev.ActorId is int b && foes.Contains(b) ? "敵" : "背いた獣";
                        x.GainBy[k] = x.GainBy.GetValueOrDefault(k) + 1;
                    }
                    else if (ev.Text == ShockGaugeLabels.ChargeSpent) x.Spent++;
                    charge = ev.Amount;
                    break;
                case BattleEventKind.Damage when ev.ActorId == sh.InstanceId && ev.TargetId is int t && !mine.ContainsKey(t):
                    perTurn[ev.Turn] = perTurn.GetValueOrDefault(ev.Turn) + ev.Amount;
                    break;
                case BattleEventKind.Death when ev.TargetId == sh.InstanceId:
                    life = Math.Min(life, ev.Turn);
                    break;
            }
        }
        Snap(int.MaxValue - 1);
        x.EndCharge = charge;
        x.ShigaLife = life;
        x.TurnMax = perTurn.Count == 0 ? 0 : perTurn.Values.Max();
        if (r.TallyByUnit.TryGetValue("shiga", out var st))
        {
            x.Dmg = st.DamageToEnemy; x.IntDmg = st.SwWhipDealt;
            x.TurnSw = st.SaTurnSwings; x.IntSw = st.SaIntSwings; x.BackSw = st.SaBackSwings; x.LaterSw = st.SaLaterSwings;
            x.SwAsked = st.SwAsked; x.NoCharge = st.SwNoCharge; x.IntFires = st.SwFires; x.SwMax = st.SaSwingMax; x.ChargePeak = st.SaChargePeak; x.Capped = st.ChargeCapped;
        }
        if (r.TallyByUnit.TryGetValue("som", out var mt)) x.BackDmg = mt.SgDealt;
        return x;
    }

    internal static (S318.St S, X X) Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var parts = new (S318.St, X)[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, from + i));
        var s = new S318.St(); var x = new X();
        foreach (var (a, b) in parts) { s.Merge(a); x.Merge(b); }
        return (s, x);
    }

    static int WinsLite(Formation f, S287.Wave w, int n = Seeds)
    {
        int wins = 0;
        Parallel.For(0, n, sd =>
        {
            if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) Interlocked.Increment(ref wins);
        });
        return wins;
    }

    static string GainLine(X x)
    {
        if (x.GainsAll == 0) return "—";
        return string.Join("・", x.GainBy.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {Per(k.Value, x.N)}"));
    }

    static string ChargeLine(X x) => string.Join(" ／ ", Enumerable.Range(0, 4).Select(i => x.ChargeAtN[i] == 0 ? "—" : Per(x.ChargeAt[i], x.ChargeAtN[i])));

    static long Fd(S318.St s, string k) => s.FoeDmg.GetValueOrDefault(k);

    // ---------------------------------------------------------------------------------
    // §1-3 Phase 0（規定のシガ）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var cells = new List<(string B, S287.Wave W, S318.St S, X X)>();
        foreach (var (n, f) in boards) foreach (var w in Waves) { var (s, x) = Measure(f, w); cells.Add((n, w, s, x)); }
        Console.WriteLine("# 第321期 段1 Phase 0 —— 規定のシガ（SF-AC ＋ SG）の蓄電と鞭（seed 0..199・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("## 1. 蓄電の推移");
        Console.WriteLine();
        Console.WriteLine("付いた（書き手別）は `ShockGauge 蓄電・増えた` の書き手（獣 ＝ ソムの背いた獣の雷・打ち返しの雷を含む）。上限 4 で溜まらなかった新しい感電は「上限で捨てた」。T5 ／ T10 ／ T15 ／ T20 はそのターンの終わりの蓄電（そのターンまで続いた戦だけの平均）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | 付いた（書き手別） | 上限で捨てた | 使った | 終わりの蓄電 | T5 ／ T10 ／ T15 ／ T20 | 増えた総数 = SA の終わりの見込み |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|---|--:|");
        foreach (var c in cells)
            Console.WriteLine($"| {c.B} | {c.W.Name} | {F1(c.S.Win)} | {Per(c.S.Turns, c.S.N)} | {GainLine(c.X)} | {Per(c.X.Capped, c.X.N)} | {Per(c.X.Spent, c.X.N)} | {Per(c.X.EndCharge, c.X.N)} | {ChargeLine(c.X)} | {Per(c.X.GainsAll + c.X.Capped, c.X.N)} |");
        Console.WriteLine();
        Console.WriteLine("「SA の終わりの見込み」＝ 規定で付いた数 ＋ 上限で捨てた数（割り込みで使わず・上限なしなら全部残る。SA で鞭が強くなって戦が短くなる分は入れていない）。");
        Console.WriteLine();
        Console.WriteLine("## 2. シガの鞭の内訳（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("与ダメは帳簿の `DamageToEnemy`（背いた獣への分を含む）。手番 ＝ 全体 − 割り込み − 打ち返し。1振りは振り数で割った平均。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 振り 手番 ／ 割り込み ／ 打ち返し | 与ダメ 手番 ／ 割り込み ／ 打ち返し | 1振り 手番 ／ 割り込み ／ 打ち返し | 割り込みを問うた ／ 蓄電不足 ／ 起きた | 1振りの最大 | 1ターンの最大 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|");
        foreach (var c in cells)
        {
            var x = c.X;
            long hand = x.Dmg - x.IntDmg - x.BackDmg;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per(x.TurnSw, x.N)} ／ {Per(x.IntSw, x.N)} ／ {Per(x.BackSw, x.N)} | {Per0(hand, x.N)} ／ {Per0(x.IntDmg, x.N)} ／ {Per0(x.BackDmg, x.N)} | {Per(hand, x.TurnSw)} ／ {Per(x.IntDmg, x.IntSw)} ／ {Per(x.BackDmg, x.BackSw)} | {Per(x.SwAsked, x.N)} ／ {Per(x.NoCharge, x.N)} ／ {Per(x.IntFires, x.N)} | {x.SwMax:N0} | {x.TurnMax:N0} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 3. 攻撃回数の口（`Trait.ModifyHitCount`）は割り込み ／ 打ち返しに届くか");
        Console.WriteLine();
        Console.WriteLine("届かない。`ModifyHitCount` を問うのは `SwingTurnHits`（手番の中）の1箇所だけで、割り込み（`ShockWhip`）と打ち返し（`Backswing`）は `Interrupt` の中で `PerformAttack` を1回直に呼ぶ（engine の注記「反撃・割り込み・追い打ち・再行動は1発のまま」）。");
        Console.WriteLine("→ 版では engine に `FlurryHits`（1 ＋ 蓄電（4 まで））を足し、割り込み ／ 打ち返しの中で `PerformAttack` を続けて呼ぶ（手番の外の連鎖の二乗化は、割り込みの中の割り込みが入れ子で止まる今の規則で抑える）。");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §1-5 ／ §1-6 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var boards = Boards();
        var res = new Dictionary<(string, string, string), (S318.St S, X X)>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v.Def), w);
        // 対照（勝率 50% 以上のセル: シガ → ドルガ）
        var dolga = new Dictionary<(string, string), int>();
        foreach (var (bn, bf) in boards) foreach (var w in Waves)
            if (vers.Any(v => res[(bn, v.Name, w.Name)].S.Wins * 2 >= Seeds)) dolga[(bn, w.Name)] = WinsLite(Apply(bf, UnitCatalog.Dolga), w);
        Console.WriteLine($"# 第321期 §1-5 ／ §1-6 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("## 1. 勝率（括弧は規定との差）と対照（シガ → ドルガ・どれかの版で 50% 以上のセルだけ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var (bn, _) in boards)
        {
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    double a = res[(bn, v.Name, w.Name)].S.Win, b0 = res[(bn, vers[0].Name, w.Name)].S.Win;
                    return v == vers[0] || a == b0 ? F1(a) : $"{F1(a)}（{Sg(a - b0)}）";
                })) + " |");
            Console.WriteLine($"| {bn} | 対照 ドルガ | " + string.Join(" | ", Waves.Select(w => dolga.TryGetValue((bn, w.Name), out int d) ? F1(100.0 * d / Seeds) : "—")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 2. 倒しT ／ 負けT ／ シガの寿命（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var (s, x) = res[(bn, v.Name, w.Name)];
                    return $"{Per(s.WinT, s.Wins)} ／ {Per(s.LoseT, s.N - s.Wins)} ／ {Per(x.ShigaLife, x.ShigaN)}";
                })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 3. シガ（1戦あたり）・ほかの駒との比・ボスの火力");
        Console.WriteLine();
        Console.WriteLine("与ダメ（台本）は最初からいる敵への `Damage`（背いた獣を除く）。手番 ／ 割り込み ／ 打ち返しは帳簿（背いた獣を含む）。比 ＝ シガ ÷ カタ ／ シガ ÷ ソム（いなければ —）。味方 ÷ 回復 ＝ 味方の与ダメの合計 ÷ 敵の回復の合計（回復が 0 なら —）。");
        Console.WriteLine("1T 決着 ＝ 1ターンで勝った戦の割合（精鋭 ／ 九体の強すぎの兆し）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | シガ（台本） | 手番 ／ 割り込み ／ 打ち返し | 振り 手番 ／ 割り込み ／ 打ち返し（2振り目以降） | 比 カタ ／ ソム | 終わりの蓄電（最大） | 蓄電 T5 ／ T10 ／ T15 ／ T20 | 1振りの最大 | 1ターンの最大 | 割り込み | 味方 ÷ 回復 | 1T 決着 |");
        Console.WriteLine("|---|---|---|--:|---|---|---|---|---|--:|--:|--:|--:|--:|");
        foreach (var (bn, bf) in boards)
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var (s, x) = res[(bn, v.Name, w.Name)];
                    long sg = Fd(s, "シガ"), ka = Fd(s, "カタ"), so = Fd(s, "ソム");
                    long hand = x.Dmg - x.IntDmg - x.BackDmg;
                    string ratio = $"{(ka > 0 ? (sg / (double)ka).ToString("F2") : "—")} ／ {(so > 0 ? (sg / (double)so).ToString("F2") : "—")}";
                    string heal = s.FoeHeal > 0 ? (s.FoeDmgAll / (double)s.FoeHeal).ToString("F2") : "—";
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {Per0(sg, x.N)} | {Per0(hand, x.N)} ／ {Per0(x.IntDmg, x.N)} ／ {Per0(x.BackDmg, x.N)} | {Per(x.TurnSw, x.N)} ／ {Per(x.IntSw, x.N)} ／ {Per(x.BackSw, x.N)}（{Per(x.LaterSw, x.N)}） | {ratio} | {Per(x.EndCharge, x.N)}（{x.ChargePeak}） | {ChargeLine(x)} | {x.SwMax:N0} | {x.TurnMax:N0} | {Per(x.IntFires, x.N)} | {heal} | {(IsElite(w) ? Pct(x.OneT, x.N) : "—")} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 4. 格子を回す版（§1-7: 代表台で ±10pt（ボスは ±5pt または倒しT ±3T）動いた版）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var hits = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var (s, _) = res[(bn, v.Name, w.Name)]; var (b0, _) = res[(bn, vers[0].Name, w.Name)];
                    double d = s.Win - b0.Win;
                    double dt = s.Wins > 0 && b0.Wins > 0 ? (double)s.WinT / s.Wins - (double)b0.WinT / b0.Wins : 0;
                    bool hit = w.Boss ? Math.Abs(d) >= 5 || Math.Abs(dt) >= 3 : Math.Abs(d) >= 10;
                    if (hit) hits.Add($"{bn} × {w.Name}（{Sg(d)}pt{(w.Boss ? $"・倒しT {Sg(dt)}" : "")}）");
                }
            Console.WriteLine($"- **{v.Name}**: {(hits.Count == 0 ? "該当なし（格子は回さない）" : $"{hits.Count} セル → 格子を回す。" + string.Join("・", hits))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §1-7 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        double[,] Grid(UnitDef d)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, d);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }
        var b = Grid(vers[0].Def);
        Console.WriteLine($"# 第321期 §1-7 `compare` {rows.Length} 行 × 版（seed 0..{Seeds - 1}・基準は規定のシガ）");
        Console.WriteLine();
        Console.WriteLine($"シガのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "shiga")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi].Def), "shiga");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §1-7 格子（第316期と同じ作り・固定枠 シガ ＋ カタ ＋ ソム・探索枠2・席 120・駒はいまの規定）
    // ---------------------------------------------------------------------------------
    static void Grid(string waveKey, string ver)
    {
        var w = WaveOf(waveKey);
        var v = V(ver);
        var fixedU = new[] { UnitCatalog.Shiga, UnitCatalog.Kata, UnitCatalog.Som };
        UnitDef Now(UnitDef d) => UnitCatalog.All.FirstOrDefault(x => x.Id == d.Id) ?? d;
        var heal = S287.Heal.Select(d => d.Id).ToHashSet();
        var pool = S287.Pool.Select(Now).Append(UnitCatalog.Tou).Where(d => d.Id is not ("shiga" or "kata" or "som")).GroupBy(d => d.Id).Select(g => g.First()).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var combos = new List<UnitDef[]>();
        for (int i = 0; i < pool.Length; i++) for (int j = i + 1; j < pool.Length; j++)
            if (!(heal.Contains(pool[i].Id) && heal.Contains(pool[j].Id))) combos.Add(new[] { pool[i], pool[j] });
        var boards = new List<UnitDef[]>();
        foreach (var c in combos) boards.AddRange(B283.Perms(fixedU.Concat(c).ToArray()));
        const int cut = 20;
        var res = new (int B, int V, int Db, int Dv, long Tb, long Tv)?[boards.Count];
        (int W, long T) Wins(Formation f, int n)
        {
            int c = 0; long t = 0;
            for (int sd = 0; sd < n; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                if (r.PlayerWon) { c++; t += r.Turns; }
            }
            return (c, t);
        }
        Parallel.For(0, boards.Count, i =>
        {
            var f0 = B283.Seat(boards[i]);
            var fv = Apply(f0, v.Def);
            if (Wins(f0, cut).W < 5 && Wins(fv, cut).W < 5) return;
            var (wb, tb) = Wins(f0, Seeds); var (wv, tv) = Wins(fv, Seeds);
            int db = wb * 2 >= Seeds ? Wins(Apply(f0, UnitCatalog.Dolga), Seeds).W : -1;
            int dv = wv * 2 >= Seeds ? (db >= 0 ? db : Wins(Apply(f0, UnitCatalog.Dolga), Seeds).W) : -1;
            res[i] = (wb, wv, db, dv, tb, tv);
        });
        var all = res.Where(r => r is not null).Select(r => r!.Value).ToList();
        bool Half(int x) => x * 2 >= Seeds;
        bool Need(int x, int d) => Half(x) && d >= 0 && d * 2 <= x;
        Console.WriteLine($"# 第321期 §1-7 格子 —— {w.Name} × 規定 ／ {v.Name}（固定枠 シガ ＋ カタ ＋ ソム・探索枠2（候補 {pool.Length} 枚: {string.Join("・", pool.Select(Short))}・ヒーラーは 1 枚まで）{combos.Count} 組・席 120 ＝ {boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{cut - 1}（どちらかで 5 勝以上・{all.Count:N0} 台）→ seed 0..{Seeds - 1}。**シガが要る台** ＝ 50% 以上 かつ シガ → ドルガで半分以下。所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
        Console.WriteLine();
        long sb = all.Sum(r => (long)r.B), sv = all.Sum(r => (long)r.V);
        Console.WriteLine($"| 段 | 規定 | {v.Name} |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 勝ちの延べ（届いた量） | {sb:N0} | {sv:N0} |");
        Console.WriteLine($"| 勝率 ≧ 50% | {all.Count(r => Half(r.B)):N0} | {all.Count(r => Half(r.V)):N0} |");
        Console.WriteLine($"| **シガが要る** | {all.Count(r => Need(r.B, r.Db)):N0} | {all.Count(r => Need(r.V, r.Dv)):N0} |");
        Console.WriteLine($"| 勝率 ≧ 90% | {all.Count(r => r.B * 10 >= Seeds * 9):N0} | {all.Count(r => r.V * 10 >= Seeds * 9):N0} |");
        Console.WriteLine($"| 倒しT（勝ちの平均） | {Per(all.Sum(r => r.Tb), sb)} | {Per(all.Sum(r => r.Tv), sv)} |");
        Console.WriteLine($"| 版 − 規定 の平均（pt） | — | {(all.Count == 0 ? 0 : all.Average(r => 100.0 * (r.V - r.B) / Seeds)):+0.0;-0.0} |");
        Console.WriteLine($"| 版が +10pt 以上 ／ −10pt 以下 | — | {all.Count(r => r.V - r.B >= Seeds / 10):N0} ／ {all.Count(r => r.V - r.B <= -Seeds / 10):N0} |");
    }

    // ---------------------------------------------------------------------------------
    // §3 台本の並び
    // ---------------------------------------------------------------------------------
    static void Memo(string boardPart, string wave, int seed, string ver)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var v = V(ver);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(Apply(f0, v.Def), BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        int sh = p.First(u => u.Def.Id == "shiga").InstanceId;
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"獣#{i}") : "—";
        Console.WriteLine($"# shiga321 memo —— {name} × {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1, lines = 0;
        foreach (var x in r.Events)
        {
            bool mineEv = x.ActorId == sh || x.TargetId == sh;
            if (!mineEv) continue;
            if (x.Kind is not (BattleEventKind.Attack or BattleEventKind.Damage or BattleEventKind.ShockGauge or BattleEventKind.LiveWire or BattleEventKind.BeastThrow)) continue;
            if (x.Kind == BattleEventKind.Damage && x.ActorId != sh) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.Reaction ? "  Reaction" : "")}");
            if (++lines >= 260) { Console.WriteLine("…（以下略）"); break; }
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // §4 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly PropertyInfo TurnProp = typeof(BattleContext).GetProperty("Turn") ?? throw new InvalidOperationException("Turn が見つからない");
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit が見つからない");
    static readonly MethodInfo Trigger = typeof(BattleContext).GetMethod("ShockTrigger", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("ShockTrigger が見つからない");
    static readonly MethodInfo BackswingM = typeof(BattleContext).GetMethod("Backswing", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Backswing が見つからない");
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();

    static BattleContext Ctx(Formation pl, Formation foes, out List<UnitState> p, out List<UnitState> e, bool verbose = true, int seed = 0)
    {
        var ctx = new BattleContext(seed, verbose);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(foes, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnProp.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Wall => UnitCatalog.Dolga.WithStats(5000, 1);

    static int HitsOf(UnitState u) { int h = 1; foreach (var t in u.Traits) h = t.ModifyHitCount(u, h); return h; }

    /// <summary>シガの1回の動作（手番 ／ 割り込み ／ 打ち返し）で積まれた出来事から、シガの `Attack` の数と電気鞭（`LiveWire`）の数を数える。</summary>
    static (int Atk, int Wire, int Flurry) Count(BattleContext ctx, UnitState sh, int from)
    {
        int a = 0, wi = 0, fl = 0;
        for (int i = from; i < ctx.Events.Count; i++)
        {
            var x = ctx.Events[i];
            if (x.ActorId != sh.InstanceId) continue;
            if (x.Kind == BattleEventKind.Attack) a++;
            else if (x.Kind == BattleEventKind.LiveWire) wi++;
            else if (x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.Flurry) fl++;
        }
        return (a, wi, fl);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# shiga321 check —— 第321期 段1の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 定義
        {
            bool sa = UnitCatalog.ShigaSA.Traits.SequenceEqual(UnitCatalog.Shiga.Traits.Concat(new[] { TraitId.ChargeUncapped, TraitId.ChargeKeep }));
            bool sap = UnitCatalog.ShigaSAp.Traits.SequenceEqual(UnitCatalog.Shiga.Traits.Concat(new[] { TraitId.ChargeUncapped, TraitId.ChargeKeep, TraitId.ChargeFlurry }));
            bool sapk = UnitCatalog.ShigaSApk.Traits.SequenceEqual(UnitCatalog.Shiga.Traits.Concat(new[] { TraitId.ChargeUncapped, TraitId.ChargeFlurry }));
            bool stats = Vers.All(v => v.Def.Id == "shiga" && v.Def.MaxHp == 52 && v.Def.Attack == 9 && v.Def.Speed == 3);
            bool outside = Vers.Skip(1).All(v => !UnitCatalog.All.Contains(v.Def) && !UnitCatalog.Retired.Contains(v.Def));
            bool texts = Vers.Skip(1).All(v => v.Def.PlusText != UnitCatalog.Shiga.PlusText && v.Def.MinusText == UnitCatalog.Shiga.MinusText);
            bool none = UnitCatalog.All.All(d => !d.Traits.Contains(TraitId.ChargeUncapped) && !d.Traits.Contains(TraitId.ChargeKeep) && !d.Traits.Contains(TraitId.ChargeFlurry));
            Expect("定義: SA ＝ 規定 ＋ 上限なし ＋ 使わない ／ SA+ ＝ ＋ 回数に ／ SA+k ＝ 上限なし ＋ 回数に・数値は規定のまま・`All` ／ `Retired` の外・文面の差し替え", sa && sap && sapk && stats && outside && texts, $"SA {sa}・SA+ {sap}・SA+k {sapk}・数値 {stats}・外 {outside}・文面 {texts}");
            Expect("規定の駒（`All`）は新しい札を1枚も持たない（規定は動かさない）", none, "");
        }

        // ---- 攻撃力と回数（蓄電の値ごと）
        {
            var rows = new List<string>(); bool ok = true;
            foreach (var (def, tag) in new[] { (UnitCatalog.Shiga, "規定"), (UnitCatalog.ShigaSA, "SA"), (UnitCatalog.ShigaSAp, "SA+"), (UnitCatalog.ShigaSApk, "SA+k") })
                foreach (int c in new[] { 0, 1, 4, 5, 9 })
                {
                    var u = BattleEngine.Materialize(Formation.Build(front1: def), BattleContext.PlayerTeam)[0];
                    u.SetCounter(StoredChargeTrait.Key, c);
                    int atk = u.CurrentAttack, hits = HitsOf(u);
                    bool flurry = def.Traits.Contains(TraitId.ChargeFlurry);
                    int wantAtk = 9 + 3 * (flurry ? Math.Max(0, c - 4) : c), wantHits = flurry ? 1 + Math.Min(4, c) : 1;
                    if (atk != wantAtk || hits != wantHits) ok = false;
                    if (c is 4 or 9) rows.Add($"{tag} 蓄電{c}: 攻 {atk}・{hits} 回");
                }
            Expect("SA は攻撃力 +3 × 蓄電（上限なし）・SA+ ／ SA+k は蓄電 1〜4 で振る回数 2〜5・5 個目から攻撃力 +3（蓄電 0 ／ 1 ／ 4 ／ 5 ／ 9）", ok, string.Join("・", rows));
        }

        // ---- 上限なしと割り込みで使わない
        {
            foreach (var (def, tag, cap) in new[] { (UnitCatalog.Shiga, "規定", true), (UnitCatalog.ShigaSA, "SA", false) })
            {
                var ctx = Ctx(Formation.Build(front1: def), Formation.Build(front1: Wall), out var p, out var e);
                var sh = p[0];
                for (int i = 0; i < 7; i++) { ctx.MarkShock(sh, e[0]); sh.SetCounter(StatusKeys.Shock, 0); }
                int c = StoredChargeTrait.Of(sh);
                Expect($"{tag}: 感電が7回新しく付くと蓄電 {(cap ? "4（上限・3回は捨てる）" : "7（上限なし）")}", cap ? c == 4 && Tal(ctx, "shiga").ChargeCapped == 3 : c == 7 && Tal(ctx, "shiga").ChargeCapped == 0, $"蓄電 {c}・捨てた {Tal(ctx, "shiga").ChargeCapped}");
            }
            foreach (var (def, tag, spend) in new[] { (UnitCatalog.Shiga, "規定", true), (UnitCatalog.ShigaSA, "SA", false), (UnitCatalog.ShigaSAp, "SA+", false), (UnitCatalog.ShigaSApk, "SA+k", true) })
            {
                var ctx = Ctx(Formation.Build(back1: def), Formation.Build(front1: Wall, front3: Wall, center: Wall), out var p, out var e);
                var sh = p[0];
                sh.SetCounter(StoredChargeTrait.Key, 3);
                int c0 = StoredChargeTrait.Of(sh);
                ctx.MarkShock(e[0], sh);
                int from = ctx.Events.Count;
                Trigger.Invoke(ctx, new object?[] { e[0], null, 0, null });   // 起こし手なし（シガ自身の連鎖では割り込まない）
                var (atk, _, fl) = Count(ctx, sh, from);
                int c1 = StoredChargeTrait.Of(sh);
                bool flurry = def.Traits.Contains(TraitId.ChargeFlurry);
                int want = flurry ? 1 + Math.Min(4, c0) : 1;
                Expect($"{tag}: 敵の感電が弾けた直後の割り込みで {want} 回振り、蓄電は {(spend ? "1 減る" : "減らない")}（蓄電 3 から）", Tal(ctx, "shiga").SwFires == 1 && atk == want && c1 == (spend ? c0 - 1 : c0) && fl == (flurry ? want : 0),
                    $"割り込み {Tal(ctx, "shiga").SwFires}・振り {atk}（連撃の札 {fl}）・蓄電 {c0} → {c1}");
            }
        }

        // ---- 手番: SA+ の回数・電気鞭は1回目だけ・怖気の判定は1回
        {
            foreach (int c in new[] { 1, 2, 3, 4 })
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.ShigaSAp), Formation.Build(front1: Wall, front3: Wall, center: Wall), out var p, out var e, seed: c);
                var sh = p[0];
                sh.SetCounter(StoredChargeTrait.Key, c);
                ctx.MarkShock(sh, e[0]);
                int c0 = StoredChargeTrait.Of(sh);
                int from = ctx.Events.Count;
                ctx.TakeTurn(sh);
                var (atk, wire, fl) = Count(ctx, sh, from);
                var t = Tal(ctx, "shiga");
                Expect($"SA+ 手番（蓄電 {c0}・帯電して振り始め）: {1 + Math.Min(4, c0)} 回振る・電気鞭は1回目だけ・2振り目以降は {Math.Min(4, c0)} 回", atk == 1 + Math.Min(4, c0) && wire == 1 && t.SaLaterSwings == Math.Min(4, c0) && t.SaTurnSwings == atk && fl == atk,
                    $"振り {atk}・電気鞭 {wire}・2振り目以降 {t.SaLaterSwings}・連撃の札 {fl}");
            }
            // 怖気: 帯電していない・動ける敵 → 1振り目で怖気（1回）・2振り目以降は数えない
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.ShigaSAp), Formation.Build(front1: Wall, front3: Wall), out var p, out var e, seed: 3);
                var sh = p[0];
                sh.SetCounter(StoredChargeTrait.Key, 4);
                ctx.TakeTurn(sh);
                var t = Tal(ctx, "shiga");
                Expect("SA+ 手番（蓄電 4・帯電していない・動ける敵）: 5 回振り、怖気は1回目の1回だけ（怖気づきうる振り 1）", t.SaTurnSwings == 5 && t.WhipCowered == 1 && t.FearSwings == 1 && sh.RawCounter(StatusKeys.Stun) > 0,
                    $"振り {t.SaTurnSwings}・怖気 {t.WhipCowered}・怖気づきうる振り {t.FearSwings}");
            }
            // 主目標が倒れたら残りは今の標的選びで次の的へ
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.ShigaSAp), Formation.Build(front1: UnitCatalog.Dolga.WithStats(1, 1), front3: Wall), out var p, out var e, seed: 1);
                var sh = p[0];
                sh.SetCounter(StoredChargeTrait.Key, 4);
                ctx.MarkShock(sh, e[1]);
                ctx.TakeTurn(sh);
                var atkTargets = ctx.Events.Where(x => x.Kind == BattleEventKind.Attack && x.ActorId == sh.InstanceId).Select(x => x.TargetId).ToList();
                Expect("SA+ 手番: 主目標が1振り目で倒れたら、残りは次の的へ振る（手番の連撃と同じ）", !e[0].IsAlive && atkTargets.Count == 5 && atkTargets.Skip(1).All(t => t == e[1].InstanceId), $"的 {string.Join(",", atkTargets)}");
            }
        }

        // ---- 打ち返しにも同じ回数
        {
            foreach (var (def, tag) in new[] { (UnitCatalog.Shiga, "規定"), (UnitCatalog.ShigaSAp, "SA+") })
            {
                var ctx = Ctx(Formation.Build(front1: def, back3: UnitCatalog.Som), Formation.Build(front1: Wall, front3: Wall, center: Wall), out var p, out var e);
                var sh = p.First(u => u.Def.Id == "shiga"); var som = p.First(u => u.Def.Id == "som");
                sh.SetCounter(StoredChargeTrait.Key, 2);
                int from = ctx.Events.Count;
                BackswingM.Invoke(ctx, new object[] { som, e[0], sh });
                var (atk, _, _) = Count(ctx, sh, from);
                int want = def.Traits.Contains(TraitId.ChargeFlurry) ? 3 : 1;
                Expect($"{tag}: 打ち返しの鞭も同じ回数（蓄電 2 → {want} 回）・蓄電は使わない", atk == want && Tal(ctx, "shiga").SaBackSwings == want && StoredChargeTrait.Of(sh) == 2, $"振り {atk}・打ち返しの振り {Tal(ctx, "shiga").SaBackSwings}");
            }
        }

        // ---- 戦の中で（代表台 × ボス・seed 0..19）: 規定は上限 4 ／ SA 系は上限を越える ／ SA ／ SA+ は使わない ／ SA+k は使う・verbose の有無で勝敗が同じ
        {
            var (bn, bf) = Boards()[0];
            var w = Waves[0];
            foreach (var v in Vers)
            {
                long peak = 0, spent = 0, capped = 0; int diff = 0;
                for (int sd = 0; sd < 20; sd++)
                {
                    var f = Apply(bf, v.Def);
                    var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                    var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                    var t = r1.TallyByUnit["shiga"];
                    peak = Math.Max(peak, t.SaChargePeak); capped += t.ChargeCapped;
                    spent += r1.Events.Count(x => x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.ChargeSpent);
                }
                bool keep = v.Def.Traits.Contains(TraitId.ChargeKeep), unc = v.Def.Traits.Contains(TraitId.ChargeUncapped);
                Expect($"{bn} × {w.Name} × {v.Name}（seed 0..19）: {(unc ? "上限で捨てない" : "蓄電の最大 ≦ 4")}{(unc && keep ? "・蓄電の最大 > 4" : "")}・{(keep ? "使わない" : "割り込みで使う")}・verbose の有無で勝敗 ／ 決着T が同じ",
                    (unc ? capped == 0 && (!keep || peak > 4) : peak <= 4) && (keep ? spent == 0 : spent > 0) && diff == 0, $"最大 {peak}・捨てた {capped}・使った {spent}・違い {diff}");
            }
        }

        // ---- 乱数を新たに引いていない（`PickOne` の呼び出し数が HEAD と同じ）
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show HEAD:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string s = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return s; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が HEAD と同じ）", engine == he && traits == ht, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
