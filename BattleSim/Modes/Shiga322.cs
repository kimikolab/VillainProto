using System.Reflection;
using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S318 = Som318Diag;
using S321 = Shiga321Diag;

// =====================================================================================
// shiga322 —— 第322期「シガの鞭は感電を消さずに放電させる」（SD）と段0（シガ SA ／ ヒサ HQ-a9 の規定化）。
// 指示書は design/PHASE322_SHIGA_DISCHARGE_SPEC.md ／ 報告は design/PHASE322_SHIGA_DISCHARGE.md。
//
//     dotnet run --project BattleSim -c Release 0 shiga322 p0                 # §4 Phase 0（規定のシガ ＝ SA）: 鞭が感電した敵に入った回数・起点で弾けて消えた感電・ほかの駒の一撃が先に届いた割合・敵の痺れ・勇者の萎縮
//     dotnet run --project BattleSim -c Release 0 shiga322 boards [版,…]      # §5-2 ／ §5-5: 代表台 5 × 10 波 × 版（規定 ／ SA-SD ／ SA+ ／ SA+-SD）と対照（シガ → ドルガ）
//     dotnet run --project BattleSim -c Release 0 shiga322 compare [版,…]     # §5-3: `compare` 64 行 × 版（(G2) つき・基準は規定）
//     dotnet run --project BattleSim -c Release 0 shiga322 grid <boss|guard> <版>   # §5-4: シガのいる台の格子（第321期と同じ作り・固定枠 シガ ＋ カタ ＋ ソム・駒はいまの規定）
//     dotnet run --project BattleSim -c Release 0 shiga322 memo <台の一部> <波> <seed> <版>   # §6: 鞭 → 弾ける → 帯電の印は残る の台本の並び
//     dotnet run --project BattleSim -c Release 0 shiga322 check              # §7 自己検査
//
// 版の名前（ASCII）: base（規定 ＝ SA）／ sd（SA-SD）／ sap（SA+）／ sapsd（SA+-SD）。波: boss ／ guard ／ bat ／ nine ／ nine2 ／ 1..5。
// **予測のファイルが無ければ本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p322/predict.md`）。
// =====================================================================================
static class Shiga322Diag
{
    internal const string PredictFile = ".tmp/p322/predict.md";
    const int Seeds = 200;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"shiga322: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "compare": CompareAll(A(3, "")); return;
            case "grid": Grid(A(3, "boss"), A(4, "sd")); return;
            case "memo": Memo(A(3, "鞭"), A(4, "boss"), int.Parse(A(5, "7")), A(6, "sd")); return;
            case "check": Check(); return;
            default: Console.WriteLine("shiga322: モードは p0 / boards / compare / grid / memo / check。"); return;
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
        new("規定", "base", UnitCatalog.ShigaH325),   // 第326期: 第322〜325期の規定に固定
        new("SA-SD", "sd", UnitCatalog.ShigaSD),
        new("SA+", "sap", UnitCatalog.ShigaSAp),
        new("SA+-SD", "sapsd", UnitCatalog.ShigaSApSD),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    static Formation Apply(Formation f, UnitDef d) => S316.SwapId(f, "shiga", d);

    internal static readonly S287.Wave[] Waves = S321.Waves;
    static S287.Wave WaveOf(string n) => S321.WaveOf(n);
    static bool IsElite(S287.Wave w) => w.Name is "近衛" or "大隊" || w.Name.StartsWith("九体", StringComparison.Ordinal);

    static Formation Playtest(string n) => Pin324(Presets.Playtest.First(r => r.Name == n).F);   // 第325期: トウ ／ カタ ／ クグを第324期の規定に固定
    static Formation Row(string n) => CompareBuildsH324().First(r => r.Name == n).F;   // 第325期: トウ ／ カタ ／ クグを第324期の規定に固定

    /// <summary>§5-2 の代表台（第321期 段1 と同じ5台・駒はいまの規定）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("光の盾 鞭", Playtest("試遊・感電 光の盾 鞭")),
        ("打ち返し", Playtest("試遊・感電 打ち返し")),
        ("雷の型", Playtest("試遊・感電 雷の型")),
        ("責め苦", Row("責め苦 (トウ×シガ)")),
        ("裂き×責め苦", Row("裂き×責め苦 (キリ×エグ×シガ)")),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（第318期の `S318.Count` ＋ 第321期の `S321.CountX` ＋ 第322期の Y）
    // ---------------------------------------------------------------------------------
    internal sealed class Y
    {
        public long N, ShigaN;
        public long HitTurn, HitInt, HitBack, RootPops, Kept, Skipped, Watch, WatchOther, WatchShiga;
        public long PopsShiga, PopsOther, Roots, KeptEv;
        public long HeroN, HeroTurns, HeroDaunted, HeroDmg;
        public long FoeN, FoeNoAct, FoeStunned, FoeStunLost, FoeTurns;
        public long AllTurnMax;
        public void Merge(Y o)
        {
            N += o.N; ShigaN += o.ShigaN;
            HitTurn += o.HitTurn; HitInt += o.HitInt; HitBack += o.HitBack; RootPops += o.RootPops; Kept += o.Kept; Skipped += o.Skipped; Watch += o.Watch; WatchOther += o.WatchOther; WatchShiga += o.WatchShiga;
            PopsShiga += o.PopsShiga; PopsOther += o.PopsOther; Roots += o.Roots; KeptEv += o.KeptEv;
            HeroN += o.HeroN; HeroTurns += o.HeroTurns; HeroDaunted += o.HeroDaunted; HeroDmg += o.HeroDmg;
            FoeN += o.FoeN; FoeNoAct += o.FoeNoAct; FoeStunned += o.FoeStunned; FoeStunLost += o.FoeStunLost; FoeTurns += o.FoeTurns;
            AllTurnMax = Math.Max(AllTurnMax, o.AllTurnMax);
        }
        public long Hits => HitTurn + HitInt + HitBack;
        public long Pops => PopsShiga + PopsOther;
    }

    internal static Y CountY(List<UnitState> p, List<UnitState> e, BattleResult r)
    {
        var y = new Y { N = 1 };
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        UnitState? sh = p.FirstOrDefault(u => u.Def.Id == "shiga");
        UnitState? hero = e.FirstOrDefault(u => u.HasTrait(TraitId.BossSteadfast));
        var perTurn = new Dictionary<int, long>();
        var died = new HashSet<int>();
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.ShockSpent when ev.Team == BattleContext.EnemyTeam:
                    if (sh is not null && ev.ActorId == sh.InstanceId) y.PopsShiga++; else y.PopsOther++;
                    if (ev.Slot == 0) y.Roots++;
                    if (ev.Text == ShockSpentLabels.Kept) y.KeptEv++;
                    break;
                case BattleEventKind.Damage when ev.TargetId is int t && foes.Contains(t) && ev.ActorId is int a && mine.Contains(a):
                    perTurn[ev.Turn] = perTurn.GetValueOrDefault(ev.Turn) + ev.Amount;
                    break;
                case BattleEventKind.Damage when hero is not null && ev.ActorId == hero.InstanceId && ev.TargetId is int ht && mine.Contains(ht):
                    y.HeroDmg += ev.Amount;
                    break;
                case BattleEventKind.Death when ev.TargetId is int dt && foes.Contains(dt):
                    died.Add(dt);
                    break;
            }
        }
        y.AllTurnMax = perTurn.Count == 0 ? 0 : perTurn.Values.Max();
        var acted = r.Hands.Where(h => h.Team == BattleContext.EnemyTeam && h.Outcome != TurnOutcome.Stalled).Select(h => h.ActorId).ToHashSet();
        y.FoeN = foes.Count;
        y.FoeNoAct = died.Count(id => !acted.Contains(id));
        foreach (string id in e.Select(u => u.Def.Id).Distinct())
            if (r.TallyByUnit.TryGetValue(id, out var ft)) { y.FoeStunned += ft.ShockStunned; y.FoeStunLost += ft.StallShockStun; y.FoeTurns += ft.TurnsTaken; }
        if (hero is not null && r.TallyByUnit.TryGetValue(hero.Def.Id, out var ht2)) { y.HeroN = 1; y.HeroTurns = ht2.TurnsTaken; y.HeroDaunted = ht2.TurnsDaunted322; }
        if (sh is not null && r.TallyByUnit.TryGetValue("shiga", out var st))
        {
            y.ShigaN = 1;
            y.HitTurn = st.SdHitShockTurn; y.HitInt = st.SdHitShockInt; y.HitBack = st.SdHitShockBack;
            y.RootPops = st.SdRootPops; y.Kept = st.SdKept; y.Skipped = st.SdSkipped;
            y.Watch = st.SdWatch; y.WatchOther = st.SdWatchOther; y.WatchShiga = st.SdWatchShiga;
        }
        return y;
    }

    internal static (S318.St S, S321.X X, Y Y) Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        return (S318.Count(p, e, r), S321.CountX(p, e, r), CountY(p, e, r));
    }

    internal static (S318.St S, S321.X X, Y Y) Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var parts = new (S318.St, S321.X, Y)[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, from + i));
        var s = new S318.St(); var x = new S321.X(); var y = new Y();
        foreach (var (a, b, c) in parts) { s.Merge(a); x.Merge(b); y.Merge(c); }
        return (s, x, y);
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

    static long Fd(S318.St s, string k) => s.FoeDmg.GetValueOrDefault(k);
    static string HitLine(Y y) => $"{Per(y.HitTurn, y.N)} ／ {Per(y.HitInt, y.N)} ／ {Per(y.HitBack, y.N)}";
    static string ChainLine(Y y) => y.Roots == 0 ? "—" : (y.Pops / (double)y.Roots).ToString("F2");

    // ---------------------------------------------------------------------------------
    // §4 Phase 0（規定のシガ ＝ SA）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var cells = new List<(string B, S287.Wave W, S318.St S, S321.X X, Y Y)>();
        foreach (var (n, f) in boards) foreach (var w in Waves) { var (s, x, y) = Measure(f, w); cells.Add((n, w, s, x, y)); }
        Console.WriteLine("# 第322期 Phase 0 —— 規定のシガ（SA）の鞭と感電（seed 0..199・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("## 1. 鞭が感電した敵に入った回数と、起点で弾けて消えた感電（§4-1）");
        Console.WriteLine();
        Console.WriteLine("鞭が感電した敵に入った ＝ 当たる前に感電していた敵への鞭の当たり（主目標 ＋ 巻き込み・手番 ／ 割り込み ／ 打ち返し）。起点で弾けた ＝ 鞭の一撃が HP に届いて起点として弾けた敵（**SD で感電が残る見込み**）。"
            + "差は「当たったが弾けなかった」（破片が受け切った・回避 など）か、同じ振りの前の当たりの連鎖で先に弾けていた分。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | 鞭が感電した敵に 手番 ／ 割り込み ／ 打ち返し | 起点で弾けて消えた（SD で残る見込み） | 弾けた敵 シガ ／ ほか（連鎖の大きさ） | 割り込み |");
        Console.WriteLine("|---|---|--:|--:|---|--:|---|--:|");
        foreach (var c in cells)
            Console.WriteLine($"| {c.B} | {c.W.Name} | {F1(c.S.Win)} | {Per(c.S.Turns, c.S.N)} | {HitLine(c.Y)} | {Per(c.Y.RootPops, c.Y.N)} | {Per(c.Y.PopsShiga, c.Y.N)} ／ {Per(c.Y.PopsOther, c.Y.N)}（{ChainLine(c.Y)}） | {Per(c.X.IntFires, c.X.N)} |");
        Console.WriteLine();
        Console.WriteLine("## 2. 鞭で弾けた敵に、次に鞭が入るまでにほかの駒の一撃が先に届いた割合（§4-2）");
        Console.WriteLine();
        Console.WriteLine("見張り ＝ 鞭の一撃で起点として弾けた敵（延べ）。ほか先 ＝ その敵に、次の鞭より先にほかの駒の一撃（HP に届いた・放電 ／ 雷 ／ 刻みを除く）が届いた割合（**SD で残した感電が、ほかの駒にも使われる見込み**）。鞭先 ＝ 次の鞭が先に入った割合。残りは倒れたか戦が終わった。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 見張り | ほか先 % | 鞭先 % | 倒れた ／ 終わった % |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var c in cells)
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per(c.Y.Watch, c.Y.N)} | {Pct(c.Y.WatchOther, c.Y.Watch)} | {Pct(c.Y.WatchShiga, c.Y.Watch)} | {Pct(c.Y.Watch - c.Y.WatchOther - c.Y.WatchShiga, c.Y.Watch)} |");
        Console.WriteLine();
        Console.WriteLine("## 3. 敵の痺れ（§4-4・精鋭 ／ 九体）と勇者の萎縮（ボス）");
        Console.WriteLine();
        Console.WriteLine("痺れた ＝ 感電で新しく痺れた敵の延べ。失った手番 ＝ 感電の痺れで潰れた敵の手番（帳簿 `StallShockStun`）。動けず倒れた ＝ 1度も動けないまま倒れた敵の割合（最初からいた敵のうち）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 痺れた | 失った手番 ／ 敵の手番 | 動けず倒れた % | 1T 決着 % | 1ターンの与ダメの最大（味方全体） | 勇者の萎縮した手番 % | 勇者 → 味方 | 味方 ÷ 回復 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|");
        foreach (var c in cells.Where(c => IsElite(c.W) || c.W.Boss))
        {
            string heal = c.S.FoeHeal > 0 ? (c.S.FoeDmgAll / (double)c.S.FoeHeal).ToString("F2") : "—";
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per(c.Y.FoeStunned, c.Y.N)} | {Per(c.Y.FoeStunLost, c.Y.N)} ／ {Per(c.Y.FoeTurns, c.Y.N)} | {Pct(c.Y.FoeNoAct, c.Y.FoeN)} | {Pct(c.X.OneT, c.X.N)} | {c.Y.AllTurnMax:N0} | {Pct(c.Y.HeroDaunted, c.Y.HeroTurns)} | {Per0(c.Y.HeroDmg, c.Y.N)} | {heal} |");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5-2 ／ §5-5 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var boards = Boards();
        var res = new Dictionary<(string, string, string), (S318.St S, S321.X X, Y Y)>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v.Def), w);
        var dolga = new Dictionary<(string, string), int>();
        foreach (var (bn, bf) in boards) foreach (var w in Waves)
            if (vers.Any(v => res[(bn, v.Name, w.Name)].S.Wins * 2 >= Seeds)) dolga[(bn, w.Name)] = WinsLite(Apply(bf, UnitCatalog.Dolga), w);
        Console.WriteLine($"# 第322期 §5-2 ／ §5-5 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}");
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
                    var (s, x, _) = res[(bn, v.Name, w.Name)];
                    return $"{Per(s.WinT, s.Wins)} ／ {Per(s.LoseT, s.N - s.Wins)} ／ {Per(x.ShigaLife, x.ShigaN)}";
                })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 3. シガ（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("与ダメ（台本）は最初からいる敵への `Damage`（背いた獣を除く）。手番 ／ 割り込み ／ 打ち返しは帳簿（背いた獣を含む）。比 ＝ シガ ÷ カタ ／ シガ ÷ ソム（いなければ —）。"
            + "感電した的に ＝ 当たる前に感電していた敵への鞭の当たり（手番 ／ 割り込み ／ 打ち返し）。起点で弾けた ＝ 鞭の一撃で起点として弾けた敵。残した ＝ SD で感電を残した。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | シガ（台本） | 手番 ／ 割り込み ／ 打ち返し | 比 カタ ／ ソム | 感電した的に 手番 ／ 割り込み ／ 打ち返し | 起点で弾けた ／ 残した | 割り込み | 終わりの蓄電（最大） | 1振りの最大 | 1ターンの最大 |");
        Console.WriteLine("|---|---|---|--:|---|---|---|---|--:|---|--:|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var (s, x, y) = res[(bn, v.Name, w.Name)];
                    long sg = Fd(s, "シガ"), ka = Fd(s, "カタ"), so = Fd(s, "ソム");
                    long hand = x.Dmg - x.IntDmg - x.BackDmg;
                    string ratio = $"{(ka > 0 ? (sg / (double)ka).ToString("F2") : "—")} ／ {(so > 0 ? (sg / (double)so).ToString("F2") : "—")}";
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {Per0(sg, x.N)} | {Per0(hand, x.N)} ／ {Per0(x.IntDmg, x.N)} ／ {Per0(x.BackDmg, x.N)} | {ratio} | {HitLine(y)} | {Per(y.RootPops, y.N)} ／ {Per(y.Kept, y.N)} | {Per(x.IntFires, x.N)} | {Per(x.EndCharge, x.N)}（{x.ChargePeak}） | {x.SwMax:N0} | {x.TurnMax:N0} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 4. 起爆の効き（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("弾けた ＝ 敵の側で感電が弾けた延べ（シガ ＝ シガの一撃が起こした連鎖の中 ／ ほか）。連鎖の大きさ ＝ 弾けた延べ ÷ 起点。雷雲 ＝ 戦の終わりのカタの雷雲。光 ／ 衣 ＝ ソムの光で降らせた量 ／ 衣を足した量。"
            + "ほか先 ＝ 鞭で弾けた敵に、次の鞭より先にほかの駒の一撃が届いた割合（SD で残した感電を使う機会）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 弾けた シガ ／ ほか | 連鎖の大きさ | カタ 雷雲 ／ 与ダメ | ソム 光 ／ 衣 ／ 与ダメ | ほか先 % | 同じ振りで弾けさせなかった |");
        Console.WriteLine("|---|---|---|---|--:|---|---|--:|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var (s, _, y) = res[(bn, v.Name, w.Name)];
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {Per(y.PopsShiga, y.N)} ／ {Per(y.PopsOther, y.N)} | {ChainLine(y)} | {Per(s.KataCloud, s.KataN)} ／ {Per0(Fd(s, "カタ"), s.N)} | {Per0(s.Lights, s.N)} ／ {Per0(s.VeilAdded, s.N)} ／ {Per0(Fd(s, "ソム"), s.N)} | {Pct(y.WatchOther, y.Watch)} | {Per(y.Skipped, y.N)} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 5. ボス（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("勇者の萎縮 ＝ 勇者が萎縮したまま迎えた手番 ÷ 勇者の手番。勇者 → 味方 ＝ 勇者が味方に与えたダメージ。味方 ÷ 回復 ＝ 味方の与ダメの合計 ÷ 敵の回復の合計。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 倒しT ／ 負けT | 勇者の萎縮 % | 勇者 → 味方 | 味方 ÷ 回復 | 1ターンの与ダメの最大（味方全体） |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves.Where(w => w.Boss))
                foreach (var v in vers)
                {
                    var (s, _, y) = res[(bn, v.Name, w.Name)];
                    string heal = s.FoeHeal > 0 ? (s.FoeDmgAll / (double)s.FoeHeal).ToString("F2") : "—";
                    Console.WriteLine($"| {bn} | {v.Name} | {F1(s.Win)} | {Per(s.WinT, s.Wins)} ／ {Per(s.LoseT, s.N - s.Wins)} | {Pct(y.HeroDaunted, y.HeroTurns)} | {Per0(y.HeroDmg, y.N)} | {heal} | {y.AllTurnMax:N0} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 6. 縛りすぎの兆し（精鋭 ／ 九体・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("痺れた ＝ 感電で新しく痺れた敵の延べ。失った手番 ＝ 感電の痺れで潰れた敵の手番。動けず倒れた ＝ 1度も動けないまま倒れた敵の割合。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 痺れた | 失った手番 ／ 敵の手番 | 動けず倒れた % | 1T 決着 % | 1ターンの与ダメの最大（味方全体） |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|--:|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves.Where(IsElite))
                foreach (var v in vers)
                {
                    var (s, x, y) = res[(bn, v.Name, w.Name)];
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {F1(s.Win)} | {Per(y.FoeStunned, y.N)} | {Per(y.FoeStunLost, y.N)} ／ {Per(y.FoeTurns, y.N)} | {Pct(y.FoeNoAct, y.FoeN)} | {Pct(x.OneT, x.N)} | {y.AllTurnMax:N0} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 7. 強すぎの兆し（§5-5: 倒しT が規定の半分以下 ／ 精鋭 ／ 九体の 1T 決着 5% 以上）と格子を回す版（§5-4: 代表台で ±10pt（ボスは ±5pt または倒しT ±3T）動いた版）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var hits = new List<string>(); var strong = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var (s, x, _) = res[(bn, v.Name, w.Name)]; var (b0, _, _) = res[(bn, vers[0].Name, w.Name)];
                    double d = s.Win - b0.Win;
                    double tv = s.Wins > 0 ? (double)s.WinT / s.Wins : 0, tb = b0.Wins > 0 ? (double)b0.WinT / b0.Wins : 0;
                    double dt = s.Wins > 0 && b0.Wins > 0 ? tv - tb : 0;
                    bool hit = w.Boss ? Math.Abs(d) >= 5 || Math.Abs(dt) >= 3 : Math.Abs(d) >= 10;
                    if (hit) hits.Add($"{bn} × {w.Name}（{Sg(d)}pt{(w.Boss ? $"・倒しT {Sg(dt)}" : "")}）");
                    if (s.Wins > 0 && b0.Wins > 0 && tv * 2 <= tb) strong.Add($"{bn} × {w.Name} 倒しT {F1(tb)} → {F1(tv)}");
                    if (IsElite(w) && x.OneT * 20 >= x.N) strong.Add($"{bn} × {w.Name} 1T 決着 {Pct(x.OneT, x.N)}%");
                }
            Console.WriteLine($"- **{v.Name}**: 強すぎの兆し {(strong.Count == 0 ? "なし" : string.Join("・", strong))}。格子 {(hits.Count == 0 ? "該当なし（回さない）" : $"{hits.Count} セル → 回す。" + string.Join("・", hits))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5-3 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var rows = CompareBuildsH324().ToArray();   // 第325期: トウ ／ カタ ／ クグを第324期の規定に固定
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
        Console.WriteLine($"# 第322期 §5-3 `compare` {rows.Length} 行 × 版（seed 0..{Seeds - 1}・基準は規定のシガ ＝ SA）");
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
    // §5-4 格子（第321期と同じ作り・固定枠 シガ ＋ カタ ＋ ソム・探索枠2・席 120・駒はいまの規定）
    // ---------------------------------------------------------------------------------
    static void Grid(string waveKey, string ver)
    {
        var w = WaveOf(waveKey);
        var v = V(ver);
        var fixedU = new[] { UnitCatalog.ShigaH325, UnitCatalog.KataH324, UnitCatalog.SomH332 };   // 第326期: シガは第325期の規定
        UnitDef Now(UnitDef d) => PinTKK324(UnitCatalog.All.FirstOrDefault(x => x.Id == d.Id) ?? d);   // 第325期: トウ ／ カタ ／ クグを第324期の規定に固定
        var heal = S287.Heal.Select(d => d.Id).ToHashSet();
        var pool = S287.Pool.Select(Now).Append(UnitCatalog.TouH324).Where(d => d.Id is not ("shiga" or "kata" or "som")).GroupBy(d => d.Id).Select(g => g.First()).ToArray();
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
        Console.WriteLine($"# 第322期 §5-4 格子 —— {w.Name} × 規定 ／ {v.Name}（固定枠 シガ ＋ カタ ＋ ソム・探索枠2（候補 {pool.Length} 枚: {string.Join("・", pool.Select(Short))}・ヒーラーは 1 枚まで）{combos.Count} 組・席 120 ＝ {boards.Count:N0} 台）");
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
    // §6 台本の並び（鞭 → 弾ける → 的の帯電の印は残る）
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
        string shockLabel = StatusKeys.LabelOf(StatusKeys.Shock);
        Console.WriteLine($"# shiga322 memo —— {name} × {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("シガの `Attack` ／ `Damage`・シガが起こした `ShockSpent`（`Text` ＝ 残る は SD で感電が残った起点）・ターン頭の `StatusSnapshot`（その敵の感電の残り）。");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1, lines = 0;
        foreach (var x in r.Events)
        {
            bool show = x.Kind switch
            {
                BattleEventKind.Attack => x.ActorId == sh,
                BattleEventKind.Damage => x.ActorId == sh,
                BattleEventKind.ShockSpent => x.ActorId == sh,
                BattleEventKind.LiveWire or BattleEventKind.ShockGauge => x.ActorId == sh || x.TargetId == sh,
                BattleEventKind.StatusSnapshot => x.TargetId is int t && e.Any(u => u.InstanceId == t) && x.Text == shockLabel,
                _ => false,
            };
            if (!show) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.Reaction ? "  Reaction" : "")}");
            if (++lines >= 260) { Console.WriteLine("…（以下略）"); break; }
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // §7 自己検査
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
    static int Shock(UnitState u) => u.RawCounter(StatusKeys.Shock);
    static int Spent(BattleContext ctx, UnitState u, int from, string? text = null) =>
        ctx.Events.Skip(from).Count(x => x.Kind == BattleEventKind.ShockSpent && x.TargetId == u.InstanceId && (text is null || x.Text == text));

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# shiga322 check —— 第322期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0: 規定化
        {
            bool shiga = UnitCatalog.ShigaH325.Traits.SequenceEqual(UnitCatalog.ShigaSA.Traits)
                && UnitCatalog.ShigaH325.Traits.SequenceEqual(UnitCatalog.ShigaH321.Traits.Concat(new[] { TraitId.ChargeUncapped, TraitId.ChargeKeep }))
                && UnitCatalog.ShigaH321.Traits.SequenceEqual(UnitCatalog.ShigaH318.Traits.Append(TraitId.BetrayBackswing))
                && UnitCatalog.ShigaH325.PlusText.Contains(UnitCatalog.ShigaChargeClause322, StringComparison.Ordinal) && !UnitCatalog.ShigaH325.PlusText.Contains(UnitCatalog.ShigaChargeClause, StringComparison.Ordinal)
                && UnitCatalog.ShigaH321.PlusText.Contains(UnitCatalog.ShigaChargeClause, StringComparison.Ordinal) && UnitCatalog.ShigaH325.MinusText == UnitCatalog.ShigaH321.MinusText;
            bool hisa = UnitCatalog.Hisa.Traits.SequenceEqual(UnitCatalog.HisaHQa9.Traits)
                && UnitCatalog.HisaH321.Traits.Contains(TraitId.CommandBall) && !UnitCatalog.Hisa.Traits.Contains(TraitId.CommandBall)
                && UnitCatalog.Hisa.PlusText.Contains(UnitCatalog.HisaBallClause322, StringComparison.Ordinal) && UnitCatalog.HisaH321.PlusText.Contains(UnitCatalog.HisaBallClause, StringComparison.Ordinal)
                && UnitCatalog.HisaHQa9.Traits.SequenceEqual(UnitCatalog.HisaH321.Traits.Select(t => t == TraitId.CommandBall ? TraitId.CommandBurst3 : t).Append(TraitId.CommandBurstCap9));
            bool outside = !UnitCatalog.Everyone.Contains(UnitCatalog.ShigaH321) && !UnitCatalog.Everyone.Contains(UnitCatalog.HisaH321)
                && UnitCatalog.All.Contains(UnitCatalog.Shiga) && UnitCatalog.All.Contains(UnitCatalog.Hisa) && UnitCatalog.All.Count == 52;
            bool pin = ReferenceEquals(PinSH321(UnitCatalog.Shiga), UnitCatalog.ShigaH321) && ReferenceEquals(PinSH321(UnitCatalog.Hisa), UnitCatalog.HisaH321)
                && ReferenceEquals(PinS319(UnitCatalog.Shiga), UnitCatalog.ShigaH321) && ReferenceEquals(PinSS318(UnitCatalog.Shiga), UnitCatalog.ShigaH318)
                && ReferenceEquals(PinDS316(UnitCatalog.Shiga), UnitCatalog.ShigaSWa) && ReferenceEquals(Pin304Hisa(), UnitCatalog.HisaH303)
                && S321.Vers[0].Def == UnitCatalog.ShigaH321 && S321.Vers[1].Def == UnitCatalog.ShigaSA;
            Expect("段0: 規定のシガ ＝ SA（札は第321期の `ShigaSA` と同じ・文面の蓄電の句を差し替え）／ 旧の定義 `ShigaH321` ＝ SF-AC ＋ SG", shiga, "");
            Expect("段0: 規定のヒサ ＝ HQ-a9（札は第321期の `HisaHQa9` と同じ・文面の号令の句を差し替え）／ 旧の定義 `HisaH321` ＝ 号令の玉", hisa, "");
            Expect("段0: 旧の定義は `Everyone` の外・規定は `All` の中（52 枚のまま）・`Pin321` ／ `PinS319` が旧へ、`PinSS318` ／ `PinDS316` ／ `Pin304` がそれより前の旧へ替える・第321期の器具の基準は旧", outside && pin, $"外 {outside}・固定 {pin}");
            // 規定のシガと SA の版は同じ戦をする（札が同じ）
            var row = Row("責め苦 (トウ×シガ)");
            int diff = 0;
            for (int sd = 0; sd < 20; sd++)
                foreach (var st in EnemyCatalog.Stages)
                {
                    var a = BattleEngine.Run(row, st.Enemy, sd, verbose: false);
                    var b = BattleEngine.Run(Apply(row, UnitCatalog.ShigaSA), st.Enemy, sd, verbose: false);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                }
            Expect("段0: 規定のシガと第321期の SA は同じ戦をする（責め苦 × 5波 × seed 0..19・勝敗 ／ 決着T）", diff == 0, $"違い {diff}");
        }

        // ---- 版の定義
        {
            bool sd = UnitCatalog.ShigaSD.Traits.SequenceEqual(UnitCatalog.ShigaH325.Traits.Append(TraitId.WhipDischarge));
            bool sapsd = UnitCatalog.ShigaSApSD.Traits.SequenceEqual(UnitCatalog.ShigaSAp.Traits.Append(TraitId.WhipDischarge));
            bool text = UnitCatalog.ShigaSD.PlusText == UnitCatalog.ShigaH325.PlusText + UnitCatalog.ShigaDischargeText && UnitCatalog.ShigaSApSD.PlusText == UnitCatalog.ShigaSAp.PlusText + UnitCatalog.ShigaDischargeText;
            bool outside = Vers.Skip(1).All(v => !UnitCatalog.Everyone.Contains(v.Def)) && UnitCatalog.Everyone.All(d => !d.Traits.Contains(TraitId.WhipDischarge));
            bool stats = Vers.All(v => v.Def.Id == "shiga" && v.Def.MaxHp == 52 && v.Def.Attack == 9 && v.Def.Speed == 3);
            Expect("版: SA-SD ＝ 規定 ＋ 放電だけ ／ SA+-SD ＝ SA+ ＋ 放電だけ・文面の末尾「鞭で弾けさせた敵の雷は、消えずに残る」・`Everyone` の外・規定の駒は札を持たない・数値は規定のまま", sd && sapsd && text && outside && stats, $"SA-SD {sd}・SA+-SD {sapsd}・文面 {text}・外 {outside}・数値 {stats}");
        }

        // ---- SD: 鞭で弾けた的の感電が残る（規定では消える）・台本の札
        {
            foreach (var (def, tag, keep) in new[] { (UnitCatalog.ShigaH325, "規定", false), (UnitCatalog.ShigaSD, "SA-SD", true) })
            {
                var ctx = Ctx(Formation.Build(front1: def), Formation.Build(front1: Wall), out var p, out var e);
                var sh = p[0];
                ctx.MarkShock(e[0], e[0]);
                int from = ctx.Events.Count;
                ctx.TakeTurn(sh);
                int spent = Spent(ctx, e[0], from), kept = Spent(ctx, e[0], from, ShockSpentLabels.Kept);
                var t = Tal(ctx, "shiga");
                Expect($"{tag}: 手番の鞭が感電した敵に入って弾ける（1回）→ その敵の感電は{(keep ? "残る（`ShockSpent` の `Text` ＝ 残る）" : "消える")}",
                    spent == 1 && (Shock(e[0]) > 0) == keep && kept == (keep ? 1 : 0) && t.SdHitShockTurn == 1 && t.SdRootPops == 1 && t.SdKept == (keep ? 1 : 0),
                    $"弾けた {spent}（残る {kept}）・感電 {Shock(e[0])}・感電した的に {t.SdHitShockTurn}・起点 {t.SdRootPops}・残した {t.SdKept}");
            }
        }

        // ---- SD: 連鎖で弾けたほかの敵は消える・同じ一振りで同じ的は1回だけ
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.ShigaSD), Formation.Build(front1: Wall, center: Wall), out var p, out var e);
            var sh = p[0];
            bool adj = FormationRules.AreAdjacent(e[0], e[1]);
            ctx.MarkShock(e[0], e[0]); ctx.MarkShock(e[1], e[1]);
            int from = ctx.Events.Count;
            ctx.TakeTurn(sh);
            var t = Tal(ctx, "shiga");
            int s0 = Spent(ctx, e[0], from), s1 = Spent(ctx, e[1], from);
            Expect("SA-SD: 主目標（前1）が弾けて残り、連鎖で弾けた隣（中央）は消える。中央の放電が主目標に戻っても、同じ一振りの中ではもう弾けない（1振りにつき1回）",
                adj && Shock(e[0]) > 0 && Shock(e[1]) == 0 && s0 == 1 && s1 == 1 && t.SdKept == 1 && t.SdSkipped >= 1,
                $"隣 {adj}・前1 弾けた {s0}（感電 {Shock(e[0])}）・中央 弾けた {s1}（感電 {Shock(e[1])}）・同じ振りで弾けさせなかった {t.SdSkipped}");
        }

        // ---- SD: シガ以外の一撃では消える
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.ShigaSD, front3: UnitCatalog.Dolga), Formation.Build(front1: Wall), out var p, out var e);
            var dolga = p.First(u => u.Def.Id == "dolga");
            ctx.MarkShock(e[0], e[0]);
            int from = ctx.Events.Count;
            ctx.ApplyDamage(e[0], 5, dolga);
            Expect("SA-SD: シガ以外の駒の一撃で弾けたら、いまどおり感電は消える", Spent(ctx, e[0], from) == 1 && Shock(e[0]) == 0 && Tal(ctx, "shiga").SdKept == 0, $"感電 {Shock(e[0])}");
        }

        // ---- SD: 割り込みの中でさらに割り込まない
        {
            var ctx = Ctx(Formation.Build(back1: UnitCatalog.ShigaSD), Formation.Build(front1: Wall, front3: Wall), out var p, out var e);
            var sh = p[0];
            bool apart = !FormationRules.AreAdjacent(e[0], e[1]);
            sh.SetCounter(StoredChargeTrait.Key, 2);
            ctx.MarkShock(e[0], e[0]); ctx.MarkShock(e[1], e[1]);
            int from = ctx.Events.Count;
            Trigger.Invoke(ctx, new object?[] { e[0], null, 0, null });   // 起こし手なし（シガ自身の連鎖では割り込まない）
            var t = Tal(ctx, "shiga");
            int kept1 = Spent(ctx, e[1], from, ShockSpentLabels.Kept);
            Expect("SA-SD: 割り込みの鞭 → 巻き込んだ感電した敵が弾けて残る → 連鎖の後の口はシガ自身の連鎖なので割り込まない（割り込みは1回・入れ子 0）",
                apart && t.SwFires == 1 && kept1 == 1 && Shock(e[1]) > 0 && t.ChainOwn >= 1 && t.SwNested == 0,
                $"前1・前3 が離れている {apart}・割り込み {t.SwFires}・前3 が残った {kept1}・自分の連鎖 {t.ChainOwn}・入れ子 {t.SwNested}");
        }

        // ---- SA+-SD: 各振りで SD
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.ShigaSApSD), Formation.Build(front1: Wall), out var p, out var e);
            var sh = p[0];
            sh.SetCounter(StoredChargeTrait.Key, 4);
            ctx.MarkShock(e[0], e[0]);
            int from = ctx.Events.Count;
            ctx.TakeTurn(sh);
            var t = Tal(ctx, "shiga");
            int kept = Spent(ctx, e[0], from, ShockSpentLabels.Kept);
            Expect("SA+-SD（蓄電 4・感電した敵1体）: 5 回振り、各振りで弾けて感電が残る（残る 5 回・2倍も各振りで入る）",
                t.SaTurnSwings == 5 && kept == 5 && Shock(e[0]) > 0 && t.SdHitShockTurn == 5 && t.WhipDoubledShock == 5, $"振り {t.SaTurnSwings}・残る {kept}・感電した的に {t.SdHitShockTurn}・感電で2倍 {t.WhipDoubledShock}");
        }

        // ---- 戦の中で（代表台 × ボス・seed 0..19）: SD の保持者のいない戦は残さない ／ 版は残す・verbose の有無で勝敗が同じ
        {
            var (bn, bf) = Boards()[0];
            var w = Waves[0];
            foreach (var v in Vers)
            {
                long kept = 0, keptEv = 0; int diff = 0;
                for (int sd = 0; sd < 20; sd++)
                {
                    var f = Apply(bf, v.Def);
                    var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                    var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                    kept += r1.TallyByUnit["shiga"].SdKept;
                    keptEv += r1.Events.Count(x => x.Kind == BattleEventKind.ShockSpent && x.Text == ShockSpentLabels.Kept);
                }
                bool sdv = v.Def.Traits.Contains(TraitId.WhipDischarge);
                Expect($"{bn} × {w.Name} × {v.Name}（seed 0..19）: {(sdv ? "感電を残した起点がある（帳簿と台本が一致）" : "感電を残さない")}・verbose の有無で勝敗 ／ 決着T が同じ",
                    (sdv ? kept > 0 && kept == keptEv : kept == 0 && keptEv == 0) && diff == 0, $"残した {kept}（台本 {keptEv}）・違い {diff}");
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

    /// <summary>第304期の固定の口（`Pin304`）が規定のヒサを第303期の規定に替えるか（席0 のヒサ1枚の台で引く）。</summary>
    static UnitDef Pin304Hisa() => Pin304(Formation.Build(front1: UnitCatalog.Hisa)).Occupied().First().Def;
}
