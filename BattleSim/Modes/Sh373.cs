using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;
using S367 = Sh367Diag;
using S370 = Sh370Diag;
using S371 = Sh371Diag;

// =====================================================================================
// sh373 —— 第373期【軽い期】段0: ヴィオの連撃 Va を規定に ／ 本体: ヴィオのマイナス「溢れて吐く」（版 A1 ／ A2）／ 紅の台の確かめ。
// 指示書は design/PHASE373_VIO_RETCH_SPEC.md ／ 報告は design/PHASE373_VIO_RETCH.md。本体の版は規定にしない。
//
//     dotnet run --project BattleSim -c Release 0 sh373 boards               # §3-2: 4 台 × 版（規定 ／ A1 ／ A2）× 6 波（seed 0..99）・誰が何Tに倒れたか・吐いた毒の行き先・ヴィオの攻撃力 ／ 段・スィドの層 ／ 衣・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh373 seats                # §3-3: 試遊・毒の5枚の並べ替え 120 通り × 規定 ／ A1 × ボス ／ 蝕みなし ／ 近衛（seed 0..19）
//     dotnet run --project BattleSim -c Release 0 sh373 guren                # §4: 試遊・毒 紅 × 7 波（seed 0..99）＋ ポンの試遊の条件の1戦（第4波・HP 1000% ／ 攻 300%・seed 7）
//     dotnet run --project BattleSim -c Release 0 sh373 compare <A1|A2>      # `compare` 64 行 × 版（ヴィオのいる行だけを版で回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh373 check                # 自己検査
//
// 1戦の計数は第370期の器具（`Sh370Diag.One` ／ `Agg`）＋ 吐き戻しの行き先（台本の出来事から・この器具の中）。版の駒は `UnitCatalog.VioA1` ／ `VioA2`（`All` に入れない）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p373/predict.md`）。
// =====================================================================================
static partial class Sh373Diag
{
    internal const string PredictFile = ".tmp/p373/predict.md";
    const string Tag = "第373期";
    const int Seeds = 100, SeatSeeds = 20, CompareSeeds = 200, LoopT = 6;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "seats" or "guren" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh373: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "seats": Seats(); return;
            case "guren": Guren(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "A1"); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh373: モードは boards / seats / guren / compare <A1|A2> / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Nm(string id) => Short(UnitCatalog.Everyone.First(d => d.Id == id));
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    internal static readonly S370.Ver[] Vers =
    {
        new("規定（Va）", null, null, null),
        new("A1 半分を吐く", null, UnitCatalog.VioA1, null),
        new("A2 3分の1を吐く", null, UnitCatalog.VioA2, null),
    };
    internal static Formation Apply(Formation f, S370.Ver v) => S370.Apply(f, v);

    internal static (string Name, Formation F)[] BoardList() => new (string, Formation)[]
    {
        ("試遊・毒", Presets.Versions.First(r => r.Name == "試遊・毒").F),
        ("試遊・毒 席替え", Presets.Versions.First(r => r.Name == "試遊・毒 席替え").F),
        ("毒特化・集団", Row(S367.BombRow)),
        ("澱み喰い (グザ×ヴィオ)", Row("澱み喰い (グザ×ヴィオ)")),
    };
    internal static readonly I.W[] Waves = S367.PWaves;

    // ---------------------------------------------------------------------------------
    // 吐き戻しの行き先（1戦）
    // ---------------------------------------------------------------------------------
    /// <summary>吐いた層の受け手の種類: 0 毒の衣の持ち主 ／ 1 ベニ（反転）／ 2 毒に強い ／ 3 毒に強くない。</summary>
    internal static int Cat(UnitState u) => u.HasTrait(TraitId.PoisonCloak) ? 0 : u.HasTrait(TraitId.Inverse) ? 1 : u.HasTrait(TraitId.PoisonProof) ? 2 : 3;
    internal static readonly string[] CatNames = { "衣の持ち主", "ベニ", "毒に強い", "毒に強くない" };

    internal sealed class Rx
    {
        public long N, Held, Out, Lost, Dead, Resucked, TickEst, CloakSaved;
        public readonly long[] To = new long[4];
        public void Add(Rx o)
        {
            N += o.N; Held += o.Held; Out += o.Out; Lost += o.Lost; Dead += o.Dead; Resucked += o.Resucked; TickEst += o.TickEst; CloakSaved += o.CloakSaved;
            for (int i = 0; i < 4; i++) To[i] += o.To[i];
        }
    }

    internal static Rx Retch(Formation f, I.W w, int seed)
    {
        var x = new Rx { N = 1 };
        var (r, p, _) = S367.Raw(f, w, seed, verbose: true);
        var vio = p.FirstOrDefault(u => u.Def.Id == "vio");
        var sid = p.FirstOrDefault(u => u.Def.Id == "sid");
        if (r.TallyByUnit.TryGetValue("sid", out var stl) && sid is not null) x.CloakSaved = stl.Pw368CloakSaved;
        if (vio is null) return x;
        if (r.TallyByUnit.TryGetValue("vio", out var vt)) { x.Held = vt.Z373RetchHeld; x.Out = vt.Z373RetchOut; x.Lost = vt.Z373RetchLost; x.Dead = vt.Z373RetchDead; }
        var mine = p.ToDictionary(u => u.InstanceId);
        var given = new Dictionary<(int Turn, int Id), long>();
        var drained = new Dictionary<(int Turn, int Id), long>();
        var ticked = new Dictionary<(int Turn, int Id), long>();
        foreach (var e in r.Events)
        {
            if (e.Kind == BattleEventKind.StatusGain && e.PoisonRoute == PoisonRoute.Retch && e.TargetId is int gt && mine.TryGetValue(gt, out var gu))
            {
                x.To[Cat(gu)] += e.Amount;
                given[(e.Turn, gt)] = given.GetValueOrDefault((e.Turn, gt)) + e.Amount;
            }
            else if (e.Kind == BattleEventKind.StatusDrain && e.ActorId == vio.InstanceId && e.Text == StatusKeys.Poison && e.TargetId is int dt)
                drained[(e.Turn, dt)] = drained.GetValueOrDefault((e.Turn, dt)) + e.Amount;
            else if (e.Kind == BattleEventKind.Status && e.Text == "毒" && e.InverterId is null && e.TargetId is int tt && mine.ContainsKey(tt))
                ticked[(e.Turn, tt)] = ticked.GetValueOrDefault((e.Turn, tt)) + e.Amount;
        }
        foreach (var ((t, id), g) in given)
        {
            x.Resucked += Math.Min(g, drained.GetValueOrDefault((t + 1, id)));
            if (Cat(mine[id]) == 3) x.TickEst += Math.Min(g, ticked.GetValueOrDefault((t + 1, id)));
        }
        return x;
    }

    // ---------------------------------------------------------------------------------
    // §3-2 4台 × 版 × 波
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        Console.WriteLine($"# {Tag} §3-2 ヴィオのマイナス「溢れて吐く」 —— 4台 × 版 × 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。版はヴィオだけを替える。駒は段0 の後の規定（ヴィオ ＝ 第372期の Va）。1戦の計数は `sh370` と同じ。");
        Console.WriteLine();
        int nb = boards.Length, nv = Vers.Length, nw = Waves.Length;
        var grid = new S370.Agg[nb, nv, nw];
        var rx = new Rx[nb, nv, nw];
        var vmax = new long[nb, nv, nw];
        Parallel.For(0, nb * nv * nw, k =>
        {
            int bi = k / (nv * nw), vi = k / nw % nv, wi = k % nw;
            var f = Apply(boards[bi].F, Vers[vi]);
            var parts = new S370.Agg[Seeds];
            var rparts = new Rx[Seeds];
            Parallel.For(0, Seeds, s => { parts[s] = S370.One(f, Waves[wi], s); rparts[s] = Retch(f, Waves[wi], s); });
            var all = new S370.Agg(); var ra = new Rx();
            foreach (var x in parts) all.Add(x);
            foreach (var x in rparts) ra.Add(x);
            grid[bi, vi, wi] = all; rx[bi, vi, wi] = ra;
            vmax[bi, vi, wi] = parts.Max(a => Enumerable.Range(1, 8).Select(t => a.VAlive[t] > 0 ? a.VAtk[t] : 0).Max());
        });
        int bw = 0, gw = 1, tw = 2, nt = nw - 1;

        Console.WriteLine("## 1. 台 × 版 × 波（勝率・倒しT（勝った戦）・倒れた味方（1戦あたり））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => grid[bi, vi, wi].Cell))} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 誰が何Tに倒れたか（駒 倒れた戦の割合%（倒れたターンの平均））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => grid[bi, vi, wi].FellCell))} |");
        Console.WriteLine();
        Console.WriteLine("**スィドが倒れた戦の割合と倒れたターンの平均（精鋭）**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | " + string.Join(" | ", Vers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "---|")));
        string SidFell(S370.Agg a) => a.FellBy.TryGetValue("sid", out var s) ? $"{Pct(s.N, a.N)}%・T{F1((double)s.TSum / s.N)}" : "0%";
        for (int bi = 0; bi < nb; bi++)
            foreach (int wi in new[] { gw, tw })
                Console.WriteLine($"| {boards[bi].Name} | {Waves[wi].Name} | {string.Join(" | ", Enumerable.Range(0, nv).Select(vi => SidFell(grid[bi, vi, wi])))} |");
        Console.WriteLine();

        Console.WriteLine("## 3. 吐いた毒の行き先（1戦あたりの層）");
        Console.WriteLine();
        Console.WriteLine("控えた ＝ 収まらずに吐く分に回した層。吐いた先は受け手の札で分けた（衣の持ち主 ＝ スィド ／ ベニ ／ 毒に強い ＝ グザ ほか ／ 毒に強くない）。");
        Console.WriteLine("吸い直した ＝ 吐いたターンの次のターンに、ヴィオがその味方から吸った層（吐いた層を上限・見積り）。毒に強くない隣の刻み ＝ 吐いた次のターンにその味方が毒で削られた量（吐いた層を上限・見積り）。衣で減った傷 ＝ スィドの衣が切った量（規定でも数える）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 控えた | 吐いた | 消えた（隣なし ／ 倒れていた） | " + string.Join(" | ", CatNames) + " | 吸い直した | 毒に強くない隣の刻み | 衣で減った傷 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                foreach (int wi in new[] { bw, gw, tw, nt })
                {
                    var x = rx[bi, vi, wi];
                    Console.WriteLine($"| {boards[bi].Name} | {Vers[vi].Name} | {Waves[wi].Name} | {Per(x.Held, x.N)} | {Per(x.Out, x.N)} | {Per(x.Lost, x.N)} ／ {Per(x.Dead, x.N)} | {string.Join(" | ", x.To.Select(v => Per(v, x.N)))} | {Per(x.Resucked, x.N)} | {Per(x.TickEst, x.N)} | {Per(x.CloakSaved, x.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 4. ヴィオの攻撃力（ターンの頭・T1〜T6・生きていた戦の平均）と連撃に上がったターン ／ 5. スィドの層（ターンの頭・T1〜T6）");
        Console.WriteLine();
        Console.WriteLine("ヴィオの攻撃力（頭）はターンの頭の写し（そのターンの吸いの前）——段はその後の吸いで上がるので、表の数字より1ターン早く上がって見える（第372期の報告 §1 の 4）。");
        Console.WriteLine();
        foreach (int bi in new[] { 0, 1 })
            foreach (int wi in new[] { bw, nt, gw, tw })
            {
                Console.WriteLine($"### {boards[bi].Name} × {Waves[wi].Name}");
                Console.WriteLine();
                Console.WriteLine("| 版 | 勝率・倒しT | 2連撃 ／ 3連撃 | ヴィオの攻撃力（頭） | ヴィオの毒 | スィドの層（頭） | スィドの返し | 衣で減った傷（1戦） |");
                Console.WriteLine("|---|---|---|---|---|---|---|--:|");
                for (int vi = 0; vi < nv; vi++)
                {
                    var a = grid[bi, vi, wi];
                    string st = $"{(a.Stage2N == 0 ? "—" : $"T{F1((double)a.Stage2T / a.Stage2N)}（{Pct(a.Stage2N, a.N)}%）")} ／ {(a.Stage3N == 0 ? "—" : $"T{F1((double)a.Stage3T / a.Stage3N)}（{Pct(a.Stage3N, a.N)}%）")}";
                    Console.WriteLine($"| {Vers[vi].Name} | {F1(a.Win)}・{a.WinTs}T | {st} | {T6(a.VAtk, a.VAlive, a)} | {T6(a.Strike, null, a)} | {T6(a.SLayer, a.SAlive, a)} | {T6(a.Retort, null, a)} | {Per(rx[bi, vi, wi].CloakSaved, rx[bi, vi, wi].N)} |");
                }
                Console.WriteLine();
            }

        var bad = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int wi = 0; wi < nw; wi++)
            {
                var b = grid[bi, 0, wi];
                for (int vi = 1; vi < nv; vi++)
                {
                    var a = grid[bi, vi, wi]; var w = Waves[wi];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (w.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4)) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 倒しT {a.WinTs}（規定 {b.WinTs}）");
                    if (100.0 * (a.Draw - b.Draw) / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 30T の打ち切り {Pct(b.Draw, b.N)} → {Pct(a.Draw, a.N)}%");
                    if (b.Win - a.Win >= 15) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 勝率 {F1(b.Win)} → {F1(a.Win)}（参考・落ちた側）");
                }
            }
        Console.WriteLine("## 7. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満（規定で既に 4T 未満は除く）／ 30T の打ち切りが 5pt 以上増える ／ 参考: 勝率が規定から 15pt 以上落ちる）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine("ヴィオの攻撃力の最大（ターンの頭・100 戦の最大）: " + string.Join("・", Enumerable.Range(0, nv).SelectMany(vi => new[] { bw, nt }.Select(wi => $"試遊・毒 × {Vers[vi].Name} × {Waves[wi].Name} {vmax[0, vi, wi]}"))) + "。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static string T6(long[] v, long[]? alive, S370.Agg a) => string.Join(" ", Enumerable.Range(1, LoopT).Select(t => alive is null ? Per(v[t], a.N) : alive[t] == 0 ? "—" : Per(v[t], alive[t])));

    // ---------------------------------------------------------------------------------
    // §3-3 席の確かめ
    // ---------------------------------------------------------------------------------
    internal static readonly I.W[] SeatWaves = { I.Boss, S366.BossNoTax, I.Guard };

    internal static Formation Seat(int[] order, S370.Ver v)
    {
        var play = Presets.Versions.First(r => r.Name == "試遊・毒").F;
        var defs = Enumerable.Range(0, 5).Select(i => play[i]!).ToArray();
        var f = Formation.Build(front1: defs[order[0]], front3: defs[order[1]], center: defs[order[2]], back1: defs[order[3]], back3: defs[order[4]]);
        return Apply(f, v);
    }

    internal static int[]? SeatBest;

    static void Seats()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var perms = S371.Perms().ToArray();
        var vers = new[] { Vers[0], Vers[1] };
        Console.WriteLine($"# {Tag} §3-3 席の確かめ —— 試遊・毒の5枚（ガルド ／ スィド ／ グザ ／ ヴィオ ／ ドルガ）の並べ替え {perms.Length} 通り × {string.Join(" ／ ", vers.Select(v => v.Name))} × {string.Join(" ／ ", SeatWaves.Select(w => w.Name))}（seed 0..{SeatSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("並びは 前1・前3・中央・後1・後3 の順。隣接は X 字の表（前1 ↔ 中央 ／ 後1・前3 ↔ 中央 ／ 後3・中央 ↔ 四隅）。並べ方は近衛の勝率 → 蝕みなしの勇者の勝率 → 近衛の倒しT の順。");
        Console.WriteLine();
        var res = new S370.Agg[vers.Length, perms.Length, SeatWaves.Length];
        Parallel.For(0, vers.Length * perms.Length * SeatWaves.Length, k =>
        {
            int vi = k / (perms.Length * SeatWaves.Length), pi = k / SeatWaves.Length % perms.Length, wi = k % SeatWaves.Length;
            res[vi, pi, wi] = S370.Measure(Seat(perms[pi], vers[vi]), SeatWaves[wi], SeatSeeds);
        });
        string Name(int pi) => S307.Seats(Seat(perms[pi], Vers[0]));
        double TOf(S370.Agg a) => a.Wins == 0 ? 99 : (double)a.WinT / a.Wins;
        string SidT(S370.Agg a) => a.FellBy.TryGetValue("sid", out var s) ? $"{Pct(s.N, a.N)}%・T{F1((double)s.TSum / s.N)}" : "0%";
        int cur = Array.FindIndex(perms, p => p.SequenceEqual(new[] { 0, 1, 2, 3, 4 }));
        var orders = new List<int>[vers.Length];

        for (int vi = 0; vi < vers.Length; vi++)
        {
            var order = Enumerable.Range(0, perms.Length).OrderByDescending(pi => res[vi, pi, 2].Win).ThenByDescending(pi => res[vi, pi, 1].Win).ThenBy(pi => TOf(res[vi, pi, 2])).ToList();
            orders[vi] = order;
            Console.WriteLine($"## {vers[vi].Name}: 上位5並び");
            Console.WriteLine();
            Console.WriteLine("| 順位 | 並び | 近衛 | 蝕みなし | ボス | スィドが倒れた（近衛） | ヴィオの隣 |");
            Console.WriteLine("|--:|---|---|---|---|---|---|");
            foreach (var (pi, rank) in order.Take(5).Select((pi, i) => (pi, i + 1)))
            {
                var b = res[vi, pi, 0]; var ntx = res[vi, pi, 1]; var g = res[vi, pi, 2];
                Console.WriteLine($"| {rank} | {Name(pi)} | {F1(g.Win)}・{g.WinTs}T | {F1(ntx.Win)}・{ntx.WinTs}T | {F1(b.Win)}・{b.WinTs}T | {SidT(g)} | {VioNeighbors(perms[pi])} |");
            }
            int g50 = Enumerable.Range(0, perms.Length).Count(pi => res[vi, pi, 2].Win >= 50);
            int nt80 = Enumerable.Range(0, perms.Length).Count(pi => res[vi, pi, 1].Win >= 80);
            Console.WriteLine();
            Console.WriteLine($"近衛 50% 以上の並び **{g50}** ／ {perms.Length}・蝕みなし 80% 以上 {nt80}・試遊・毒の並びの順位 {order.IndexOf(cur) + 1}（近衛 {F1(res[vi, cur, 2].Win)}%・蝕みなし {F1(res[vi, cur, 1].Win)}%・ボス {F1(res[vi, cur, 0].Win)}%）。");
            Console.WriteLine($"1位の並び: {string.Join(",", perms[order[0]])} ＝ {Name(order[0])}");
            Console.WriteLine();
        }
        SeatBest = perms[orders[1][0]];

        Console.WriteLine("## ヴィオの隣の組ごと（その組の並びの平均）: 近衛の勝率 ／ スィドが倒れたターン（近衛・倒れた戦の平均）／ 蝕みなし");
        Console.WriteLine();
        Console.WriteLine("| ヴィオの隣 | 並びの数 | 近衛 規定 ／ A1 | スィドが倒れた T（近衛）規定 ／ A1 | スィドが倒れた戦（近衛）規定 ／ A1 | 蝕みなし 規定 ／ A1 |");
        Console.WriteLine("|---|--:|---|---|---|---|");
        var groups = Enumerable.Range(0, perms.Length).GroupBy(pi => VioNeighbors(perms[pi])).OrderByDescending(g => g.Average(pi => res[1, pi, 2].Win));
        foreach (var g in groups)
        {
            var ps = g.ToList();
            string ST(int vi)
            {
                long n = 0, t = 0;
                foreach (int pi in ps) if (res[vi, pi, 2].FellBy.TryGetValue("sid", out var s)) { n += s.N; t += s.TSum; }
                return n == 0 ? "—" : "T" + F1((double)t / n);
            }
            string SN(int vi) => F1(ps.Average(pi => res[vi, pi, 2].FellBy.TryGetValue("sid", out var s) ? 100.0 * s.N / res[vi, pi, 2].N : 0));
            Console.WriteLine($"| {g.Key} | {ps.Count} | {F1(ps.Average(pi => res[0, pi, 2].Win))} ／ {F1(ps.Average(pi => res[1, pi, 2].Win))} | {ST(0)} ／ {ST(1)} | {SN(0)} ／ {SN(1)}% | {F1(ps.Average(pi => res[0, pi, 1].Win))} ／ {F1(ps.Average(pi => res[1, pi, 1].Win))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>並び（試遊・毒の5枚の席順）でヴィオの隣に来る駒の名前（名前順）。</summary>
    internal static string VioNeighbors(int[] order)
    {
        var play = Presets.Versions.First(r => r.Name == "試遊・毒").F;
        int vioIx = Enumerable.Range(0, 5).First(i => play[i]!.Id == "vio");
        int slot = Array.IndexOf(order, vioIx);
        return string.Join("・", Enumerable.Range(0, 5).Where(s => s != slot && FormationRules.AreAdjacent(slot, s)).Select(s => Short(play[order[s]]!)).OrderBy(x => x));
    }

    // ---------------------------------------------------------------------------------
    // §4 紅の台
    // ---------------------------------------------------------------------------------
    internal static readonly EnemyScaleRule Strong = new(500, 300), Pon = new(1000, 300);
    internal static I.W MainW(int i, EnemyScaleRule sc, string name) => new(name, () => BattleEngine.Materialize(EnemyCatalog.Stages[i - 1].Enemy, BattleContext.EnemyTeam, sc), sc);
    internal static readonly I.W[] GurenWaves = { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5, MainW(4, Strong, "強い 第4波（HP 500% ／ 攻 300%）") };

    internal sealed class Gx
    {
        public long N, Wins, WinT, Fell, Draw, Turns, Fires, Spent, Gained, InvHealed, Retort, Mend;
        public readonly Dictionary<string, (long N, long TSum)> FellBy = new();
        public long MaxSid, MaxGuren, MaxBeni, MaxBorg, MaxFoe, MaxGurenTotal;
        public void Add(Gx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Draw += o.Draw; Turns += o.Turns; Fires += o.Fires; Spent += o.Spent; Gained += o.Gained; InvHealed += o.InvHealed; Retort += o.Retort; Mend += o.Mend;
            foreach (var (k, v) in o.FellBy) { var c = FellBy.GetValueOrDefault(k); FellBy[k] = (c.N + v.N, c.TSum + v.TSum); }
            MaxSid = Math.Max(MaxSid, o.MaxSid); MaxGuren = Math.Max(MaxGuren, o.MaxGuren); MaxBeni = Math.Max(MaxBeni, o.MaxBeni); MaxBorg = Math.Max(MaxBorg, o.MaxBorg); MaxFoe = Math.Max(MaxFoe, o.MaxFoe);
            MaxGurenTotal = Math.Max(MaxGurenTotal, o.MaxGurenTotal);
        }
    }

    /// <summary>1戦の紅の台の数。<paramref name="trace"/> を渡せばターンごとの推移（ターンの頭の写し・ターンごとに放った回数と量）を書く。</summary>
    internal static Gx GurenOne(Formation f, I.W w, int seed, List<string>? trace = null)
    {
        var x = new Gx { N = 1 };
        var (r, p, e) = S367.Raw(f, w, seed, verbose: true);
        x.Turns = r.Turns;
        if (r.PlayerWon) { x.Wins = 1; x.WinT = r.Turns; } else if (r.PlayerSurvivors > 0) x.Draw = 1;
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { x.Fell++; var c = x.FellBy.GetValueOrDefault(u.Def.Id); x.FellBy[u.Def.Id] = (c.N + 1, c.TSum + u.LastDeathTurn); }
        if (r.TallyByUnit.TryGetValue("beni", out var bt)) { x.Fires = bt.GurenFires; x.Spent = bt.GurenSpent; x.Gained = bt.GurenGained; x.InvHealed = bt.InversePoisonHealed + bt.InverseBurnHealed + bt.InverseDetonateHealed; }
        if (r.TallyByUnit.TryGetValue("sid", out var st)) x.Retort = st.Z369Retort;
        if (r.TallyByUnit.TryGetValue("borg", out var ot)) x.Mend = ot.FireMendHealed;
        int Id(string id) => p.FirstOrDefault(u => u.Def.Id == id)?.InstanceId ?? -1;
        int sid = Id("sid"), beni = Id("beni"), borg = Id("borg");
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        string psn = StatusKeys.LabelOf(StatusKeys.Poison), gl = StatusKeys.LabelOf(StatusKeys.Guren);
        var rows = new SortedDictionary<int, long[]>();   // 0 スィドの毒 1 ベニの紅 2 ベニの毒 3 ボルグの毒 4 敵の毒の最大 5 放った回数 6 放った量
        long[] Rw(int t) { if (!rows.TryGetValue(t, out var a)) rows[t] = a = new long[7]; return a; }
        foreach (var ev in r.Events)
        {
            if (ev.Kind == BattleEventKind.StatusSnapshot && ev.TargetId is int ti)
            {
                if (ev.Text == psn && ti == sid) { x.MaxSid = Math.Max(x.MaxSid, ev.Amount); Rw(ev.Turn)[0] = ev.Amount; }
                else if (ev.Text == gl && ti == beni) { x.MaxGuren = Math.Max(x.MaxGuren, ev.Amount); Rw(ev.Turn)[1] = ev.Amount; }
                else if (ev.Text == psn && ti == beni) { x.MaxBeni = Math.Max(x.MaxBeni, ev.Amount); Rw(ev.Turn)[2] = ev.Amount; }
                else if (ev.Text == psn && ti == borg) { x.MaxBorg = Math.Max(x.MaxBorg, ev.Amount); Rw(ev.Turn)[3] = ev.Amount; }
                else if (ev.Text == psn && foes.Contains(ti)) { x.MaxFoe = Math.Max(x.MaxFoe, ev.Amount); Rw(ev.Turn)[4] = Math.Max(Rw(ev.Turn)[4], ev.Amount); }
            }
            else if (ev.Kind == BattleEventKind.GurenRelease && ev.TargetId is null && ev.ActorId == beni)
            {
                Rw(ev.Turn)[5]++; Rw(ev.Turn)[6] += ev.Amount;
                x.MaxGurenTotal = Math.Max(x.MaxGurenTotal, ev.Amount);   // 放つ直前の紅の最大（手番の頭）
            }
        }
        // 戦の終わりの値も最大に入れる（最後のターンに積まれた分はターンの頭の写しに出ない）
        foreach (var u in p)
        {
            if (u.Def.Id == "sid") x.MaxSid = Math.Max(x.MaxSid, u.RawCounter(StatusKeys.Poison));
            if (u.Def.Id == "beni") { x.MaxGuren = Math.Max(x.MaxGuren, u.RawCounter(StatusKeys.Guren)); x.MaxBeni = Math.Max(x.MaxBeni, u.RawCounter(StatusKeys.Poison)); }
            if (u.Def.Id == "borg") x.MaxBorg = Math.Max(x.MaxBorg, u.RawCounter(StatusKeys.Poison));
        }
        foreach (var u in e) x.MaxFoe = Math.Max(x.MaxFoe, u.RawCounter(StatusKeys.Poison));
        if (trace is not null)
            foreach (var (t, a) in rows)
                trace.Add($"| T{t} | {a[0]} | {a[1]} | {a[2]} | {a[3]} | {a[4]} | {a[5]} | {a[6]} |");
        return x;
    }

    static void Guren()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var f = Presets.Versions.First(r => r.Name == "試遊・毒 紅").F;
        Console.WriteLine($"# {Tag} §4 紅の台の確かめ（数えるだけ）—— 試遊・毒 紅（{S307.Seats(f)}・規定の駒）× {GurenWaves.Length} 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        var res = new Gx[GurenWaves.Length];
        Parallel.For(0, GurenWaves.Length, wi =>
        {
            var parts = new Gx[Seeds];
            Parallel.For(0, Seeds, s => parts[s] = GurenOne(f, GurenWaves[wi], s));
            var all = new Gx(); foreach (var x in parts) all.Add(x); res[wi] = all;
        });
        string FellCell(Gx a) => a.FellBy.Count == 0 ? "—" : string.Join("・", a.FellBy.OrderByDescending(kv => kv.Value.N).Select(kv => $"{Nm(kv.Key)} {Pct(kv.Value.N, a.N)}%（T{F1((double)kv.Value.TSum / kv.Value.N)}）"));
        Console.WriteLine("## 1. 勝率・決着T・倒れた味方 ／ 2. 層の最大（その波の 100 戦の最大）／ 3〜4. 紅を放った回数と量・回復の量（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 倒しT ／ 決着T | 倒れた味方（1戦）| 誰が何Tに倒れたか | スィドの毒 | ベニの紅（ターンの頭）| 放つ直前の紅 | ベニの毒 | ボルグの毒 | 敵の毒 | 紅を放った回数 | 放った紅 | 溜めた紅 | 反転の回復 | スィドの返し | 火の癒し |");
        Console.WriteLine("|---|--:|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wi = 0; wi < GurenWaves.Length; wi++)
        {
            var a = res[wi];
            Console.WriteLine($"| {GurenWaves[wi].Name} | {Pct(a.Wins, a.N)} | {(a.Wins == 0 ? "—" : F1((double)a.WinT / a.Wins))} ／ {F1((double)a.Turns / a.N)} | {Per(a.Fell, a.N)} | {FellCell(a)} | {a.MaxSid} | {a.MaxGuren} | {a.MaxGurenTotal} | {a.MaxBeni} | {a.MaxBorg} | {a.MaxFoe} | {Per(a.Fires, a.N)} | {Per(a.Spent, a.N)} | {Per(a.Gained, a.N)} | {Per(a.InvHealed, a.N)} | {Per(a.Retort, a.N)} | {Per(a.Mend, a.N)} |");
        }
        Console.WriteLine();

        var pw = MainW(4, Pon, "第4波（HP 1000% ／ 攻 300%）");
        var trace = new List<string>();
        var one = GurenOne(f, pw, 7, trace);
        Console.WriteLine($"## ポンの試遊と同じ条件の1戦（{pw.Name}・seed 7）");
        Console.WriteLine();
        Console.WriteLine($"{(one.Wins > 0 ? "勝ち" : one.Draw > 0 ? "30T の打ち切り" : "負け")}・{one.Turns}T・倒れた味方 {one.Fell}（{FellCell(one)}）・紅を放った回数 {one.Fires}・放った紅 {one.Spent}・溜めた紅 {one.Gained}・反転の回復 {one.InvHealed}・スィドの返し {one.Retort}・火の癒し {one.Mend}。");
        Console.WriteLine();
        Console.WriteLine("| T | スィドの毒 | ベニの紅 | ベニの毒 | ボルグの毒 | 敵の毒の最大 | 紅を放った回数 | 放った紅 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var l in trace) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"層の最大（ターンの頭の写しと戦の終わりの値）: スィドの毒 {one.MaxSid}・ベニの紅 {one.MaxGuren}（放つ直前 {one.MaxGurenTotal}）・ベニの毒 {one.MaxBeni}・ボルグの毒 {one.MaxBorg}・敵の毒 {one.MaxFoe}。int の上限 2,147,483,647 との比 {F1(100.0 * Math.Max(one.MaxSid, Math.Max(one.MaxGuren, one.MaxFoe)) / int.MaxValue)}%。");
        Console.WriteLine();
        // あふれの見積り: 倒れない敵（HP 1,000,000% ／ 攻 300%）で 30T の打ち切りまで回す（ポンの試遊の条件の延長・数えるだけ）
        var hw = MainW(4, new EnemyScaleRule(1_000_000, 300), "第4波（HP 1,000,000% ／ 攻 300%）");
        var htr = new List<string>();
        var hard = GurenOne(f, hw, 7, htr);
        Console.WriteLine($"## あふれの見積り（{hw.Name}・seed 7・倒れない敵で 30T まで）");
        Console.WriteLine();
        Console.WriteLine($"{(hard.Wins > 0 ? "勝ち" : hard.Draw > 0 ? "30T の打ち切り" : "負け")}・{hard.Turns}T・倒れた味方 {hard.Fell}・紅を放った回数 {hard.Fires}。層の最大: スィドの毒 {hard.MaxSid}・ベニの紅 {hard.MaxGuren}・敵の毒 {hard.MaxFoe}（int の上限との比 {F1(100.0 * Math.Max(hard.MaxSid, Math.Max(hard.MaxGuren, hard.MaxFoe)) / int.MaxValue)}%・負の値 {(htr.Any(l => l.Contains("| -")) ? "あり" : "なし")}）。");
        Console.WriteLine();
        Console.WriteLine("| T | スィドの毒 | ベニの紅 | ベニの毒 | ボルグの毒 | 敵の毒の最大 | 紅を放った回数 | 放った紅 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var l in htr) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows, Func<Formation, Formation> map, Func<int, bool> only)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            if (!only(ri)) { g[ri, wi] = double.NaN; return; }
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareVer(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = ver switch { "A1" => Vers[1], "A2" => Vers[2], _ => throw new ArgumentException("sh373 compare の版は A1 / A2") };
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        Func<int, bool> only = ri => Has(rows[ri].F, "vio");
        var b = CGrid(rows, f => f, _ => true);
        var g0 = CGrid(rows, f => Apply(f, v), only);
        var g = new double[rows.Length, nw];
        int touched = 0;
        for (int r = 0; r < rows.Length; r++) { if (only(r)) touched++; for (int w = 0; w < nw; w++) g[r, w] = only(r) ? g0[r, w] : b[r, w]; }
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        Console.WriteLine($"# {Tag} `compare` {rows.Length} 行 × 版 {v.Name}（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"版で回した行 {touched}（ヴィオのいる行・{string.Join("、", Enumerable.Range(0, rows.Length).Where(only).Select(ri => rows[ri].Name))}）。");
        Console.WriteLine();
        int upR = 0, upC = 0, dnR = 0, dnC = 0;
        var lines = new List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            bool u = false, d = false;
            for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
            if (u) upR++; if (d) dnR++;
            if (u || d) lines.Add($"{rows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
        }
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        Console.WriteLine($"| {v.Name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
        Console.WriteLine();
        foreach (var l in lines) Console.WriteLine("- " + l);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
