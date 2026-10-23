using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S379 = Sh379Diag;

// =====================================================================================
// sh381 —— 第381期【軽い期】ヴェルの「魂を分ける」（版 S2 ／ S2n ／ S10）。規定は1つも変えない。
// 指示書は design/PHASE381_VEL_SOUL_SPEC.md、報告は design/PHASE381_VEL_SOUL.md。
//
//     dotnet run --project BattleSim -c Release 0 sh381 dead      # §3-1 の 1: 死の台（第380期の上位 10 ＋ 死の連鎖）× ヴェル 4 列 × 8 物差し（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh381 heal      # §3-1 の 2: 上位 3 台の耐久役 → ヒサ ／ シオ ／ ベニ（9 台）× ヴェル 4 列 × 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh381 outside   # §3-1 の 3: 死の外の4台で1枚をヴェルに替えた最良（seed 0..19 で選び、最良だけ seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh381 compare   # §3-3 の 7: `compare` 64 行 × 版（S2 ／ S2n ／ S10・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh381 check     # 自己検査
//
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p381/predict.md`）。替えた台は器具の中だけ（試遊の台は「版・S2 ／ S10 死 探索1位」の2台）。
// =====================================================================================
static partial class Sh381Diag
{
    internal const string PredictFile = ".tmp/p381/predict.md";
    const string Tag = "第381期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "dead" or "heal" or "outside" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh381: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "dead": Dead(); return;
            case "heal": Heal(); return;
            case "outside": Outside(); return;
            case "compare": CompareVer(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh381: モードは dead / heal / outside / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long Xv(S377.B b, string k) => b.X.GetValueOrDefault(k);

    // ---------------------------------------------------------------------------------
    // 版と台
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] VelOf = { UnitCatalog.VelH381, UnitCatalog.VelS2, UnitCatalog.VelS2n, UnitCatalog.VelS10 };   // 第382期: 「規定」の列は第381期までの規定（`VelH381`）に固定
    internal static readonly string[] VerNames = { "規定", "S2", "S2n", "S10" };
    internal static Formation Apply(Formation f, int vi) => vi == 0 ? Pin381(f) : I.Swap(f, "vel", VelOf[vi]);

    /// <summary>並び（前1・前3・中央・後1・後3 の短い名前を「・」で区切る）から台を組む（規定の駒）。</summary>
    internal static Formation Seat(string s)
    {
        var f = new Formation();
        var n = s.Split('・');
        for (int i = 0; i < 5; i++) f[i] = I.ByShort(n[i]);
        return f;
    }
    /// <summary>第380期の席の探索の上位 10 台（本体・`design/PHASE380_DEATH_SEATS.md` §3-1）。1 位は「試遊・死 探索1位」（`Presets.DeathSeek380`）。</summary>
    internal static readonly (string Name, string Seats)[] Top10 =
    {
        ("1 ボルグ＋ヒヨ", "リィカ・ヒヨ・ボルグ・ヴェル・ムグ"),
        ("2 スィド＋ドハ", "スィド・ドハ・ヴェル・リィカ・ムグ"),
        ("3 ゴルム＋スィド", "スィド・ヴェル・ゴルム・リィカ・ムグ"),
        ("4 スィド＋アカ", "スィド・ムグ・アカ・リィカ・ヴェル"),
        ("5 スィド＋クビ", "スィド・クビ・ムグ・リィカ・ヴェル"),
        ("6 スィド＋バン", "バン・スィド・ムグ・ヴェル・リィカ"),
        ("7 ガルド＋スィド", "スィド・ムグ・ガルド・リィカ・ヴェル"),
        ("8 スィド＋ソラ", "スィド・ソラ・ムグ・リィカ・ヴェル"),
        ("9 スィド＋カド", "ムグ・カド・スィド・ヴェル・リィカ"),
        ("10 ゴルム＋ソラ", "ムグ・ゴルム・ヴェル・ソラ・リィカ"),
    };
    internal static (string Name, Formation F)[] DeadBoards() =>
        Top10.Select(t => (t.Name, Seat(t.Seats))).Append(("死の連鎖 (リィカ軸)", Presets.DeathChain377())).ToArray();
    /// <summary>上位 3 台の2枠の耐久役（決めたこと: 1 位はボルグ（燃える盾・HP 100）／ 2 位はドハ ／ 3 位はゴルム）。</summary>
    internal static readonly string[] TankOfTop3 = { "ボルグ", "ドハ", "ゴルム" };
    internal static readonly string[] HealerSub = { "ヒサ", "シオ", "ベニ" };
    internal static (string Name, Formation F)[] HealBoards()
    {
        var res = new List<(string, Formation)>();
        for (int i = 0; i < 3; i++)
            foreach (var h in HealerSub)
                res.Add(($"{Top10[i].Name} の {TankOfTop3[i]} → {h}", Seat(Top10[i].Seats.Replace(TankOfTop3[i], h))));
        return res.ToArray();
    }
    internal static readonly string[] OutsideNames = { "試遊・標 守り型", "試遊・燃焼 ポンの台", "試遊・呪い", "試遊・破片" };
    internal static Formation Named(string n) => Presets.Playtest.Concat(VersionsH386()).First(r => r.Name == n).F;
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] Key4 = { 0, 1, 2, 5 };   // ボス ／ 近衛 ／ 大隊 ／ 本編 第5波

    // ---------------------------------------------------------------------------------
    // この期の計数（`Sh377Diag.One` の `extra`）
    // ---------------------------------------------------------------------------------
    /// <summary>
    /// 第379期の計数（層の出どころ・胞子・勇者の HP など）に、起き上がりを足す: 起き上がった回数（駒ごと・T）／ 本当の死（駒ごとの最初の T）／ 縫い付け ／ 層の出どころの「編成」を
    /// 本当の死と起き上がる死に分けた数 ／ 亡者を起き上がる死から起きたものと本当の死から起きたものに分けた数 ／ 1戦の起き上がり 10 回超。
    /// 起き上がる死は「`Death` の後に同じ駒の `Revive`（`Text` ＝ 起き上がり）が来る死」。
    /// </summary>
    internal static void Extra(BattleResult r, List<UnitState> p, List<UnitState> e, S377.B b)
    {
        S379.Extra(r, p, e, b);
        var x = b.X;
        var form = p.ToDictionary(u => u.InstanceId);
        var evs = r.Events;
        var lastDeath = new Dictionary<int, int>(); var riseIdx = new HashSet<int>();
        for (int i = 0; i < evs.Count; i++)
        {
            var ev = evs[i];
            if (ev.Kind == BattleEventKind.Death && ev.TargetId is int d) lastDeath[d] = i;
            else if (ev.Kind == BattleEventKind.Revive && ev.Text == BattleContext.SoulRiseText && ev.TargetId is int rv && lastDeath.TryGetValue(rv, out int di)) riseIdx.Add(di);
        }
        int ricaId = p.FirstOrDefault(u => u.Def.Id == "rica")?.InstanceId ?? -1;
        bool ricaAlive = ricaId >= 0, curRise = false;
        int rises = 0; var realSeen = new HashSet<int>(); var riseSeen = new HashSet<int>();
        for (int i = 0; i < evs.Count; i++)
        {
            var ev = evs[i];
            if (ev.Kind == BattleEventKind.Revive && ev.TargetId == ricaId) ricaAlive = true;
            else if (ev.Kind == BattleEventKind.Summon && ev.Team == BattleContext.PlayerTeam && ev.Text == UnitCatalog.Wight.Name)
            { string k = curRise ? "wRise" : "wReal"; x[k] = x.GetValueOrDefault(k) + 1; }
            else if (ev.Kind == BattleEventKind.Death && ev.TargetId is int dt)
            {
                bool rise = riseIdx.Contains(i); curRise = rise;
                if (form.TryGetValue(dt, out var u))
                {
                    string nm = S307.Short(u.Def);
                    if (rise)
                    {
                        rises++;
                        x[$"ri:{nm}"] = x.GetValueOrDefault($"ri:{nm}") + 1; x[$"riT:{nm}"] = x.GetValueOrDefault($"riT:{nm}") + ev.Turn;
                        if (riseSeen.Add(dt)) { x[$"ri1:{nm}"] = x.GetValueOrDefault($"ri1:{nm}") + 1; x[$"ri1T:{nm}"] = x.GetValueOrDefault($"ri1T:{nm}") + ev.Turn; }
                    }
                    else
                    {
                        x["realDeaths"] = x.GetValueOrDefault("realDeaths") + 1;
                        if (realSeen.Add(dt)) { x[$"rd:{nm}"] = x.GetValueOrDefault($"rd:{nm}") + 1; x[$"rdT:{nm}"] = x.GetValueOrDefault($"rdT:{nm}") + ev.Turn; }
                    }
                    if (dt != ricaId && ricaAlive) { string k = rise ? "srcRise" : "srcReal"; x[k] = x.GetValueOrDefault(k) + 1; }
                }
                if (dt == ricaId && !rise) ricaAlive = false;
            }
        }
        x["rise"] = rises;
        if (rises > 10) x["rise10"] = 1;
        if (r.TallyByUnit.TryGetValue("vel", out var vt)) { x["sewn"] = vt.R381Sewn; x["sewSwing"] = vt.R381SewSwing; x["split"] = vt.R381Split; }
    }

    static S377.B M(Formation f, I.W[] ws, int seeds) => S377.Measure(f, ws, seeds, Extra);

    /// <summary>台 × 列 × 8 物差し（seed 0..<paramref name="seeds"/>-1）。</summary>
    static S377.B[,,] Grid((string Name, Formation F)[] boards, int seeds)
    {
        int nb = boards.Length, nv = VelOf.Length, nw = W8.Length;
        var g = new S377.B[nb, nv, nw];
        Parallel.For(0, nb * nv * nw, k => { int bi = k / (nv * nw), vi = k / nw % nv, wi = k % nw; g[bi, vi, wi] = M(Apply(boards[bi].F, vi), W8[wi].Ws, seeds); });
        return g;
    }

    static void CellTable(string title, (string Name, Formation F)[] boards, S377.B[,,] g, Func<S377.B, string> cell, int[]? ws = null)
    {
        ws ??= Enumerable.Range(0, W8.Length).ToArray();
        Console.WriteLine(title);
        Console.WriteLine();
        Console.WriteLine("| 台 | ヴェル | " + string.Join(" | ", ws.Select(w => W8[w].Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < VelOf.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {string.Join(" | ", ws.Select(w => cell(g[bi, vi, w])))} |");
        Console.WriteLine();
    }

    /// <summary>誰が何Tに倒れたか（本当の死: 最初に倒れた戦の割合%（T）／ 起き上がり: 1戦の回数（最初の T））。</summary>
    static string FellSplit(S377.B b, Formation f)
    {
        var parts = new List<string>();
        foreach ((int _, UnitDef d) in f.Occupied())
        {
            string nm = S307.Short(d);
            long rd = Xv(b, $"rd:{nm}"), ri = Xv(b, $"ri:{nm}"), ri1 = Xv(b, $"ri1:{nm}");
            if (rd == 0 && ri == 0) continue;
            string s = nm;
            if (rd > 0) s += $" 死 {Pct(rd, b.N)}%（T{Per(Xv(b, $"rdT:{nm}"), rd)}）";
            if (ri > 0) s += $" 起 {Per(ri, b.N)}（T{Per(Xv(b, $"ri1T:{nm}"), ri1)}）";
            parts.Add(s);
        }
        return parts.Count == 0 ? "—" : string.Join("・", parts);
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 1 死の台
    // ---------------------------------------------------------------------------------
    static void Dead()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = DeadBoards();
        var g = Grid(boards, 100);
        Console.WriteLine($"# {Tag} §3-1 の 1 死の台 —— 第380期の上位 10 台 ＋ 死の連鎖 × ヴェル（{string.Join(" ／ ", VerNames)}）× 8 物差し（seed 0..99）");
        Console.WriteLine();
        Console.WriteLine("1 位（ボルグ＋ヒヨ）は「試遊・死 探索1位」と同じ台。セル: 勝率・決着T・倒れた編成の駒（起き上がる死も1回と数える）。");
        Console.WriteLine();
        CellTable("## 1. 勝率・決着T・倒れた数", boards, g, x => x.Cell);
        double Mean4(int bi, int vi) => Key4.Average(w => g[bi, vi, w].Win);
        Console.WriteLine("4 波（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波）の平均（規定 → S2 ／ S2n ／ S10）: " + string.Join("・", Enumerable.Range(0, boards.Length).Select(bi => $"{boards[bi].Name} {F1(Mean4(bi, 0))} → {F1(Mean4(bi, 1))} ／ {F1(Mean4(bi, 2))} ／ {F1(Mean4(bi, 3))}")) + "。");
        Console.WriteLine();
        Console.WriteLine("強い条件の平均（11 台）: " + string.Join(" ／ ", Enumerable.Range(0, VelOf.Length).Select(vi => $"{VerNames[vi]} {F1(Enumerable.Range(0, boards.Length).Average(bi => g[bi, vi, 7].Win))}")) + "。");
        Console.WriteLine();

        int[] rep = { 0, 1, 2, 3, 10 };   // 代表台: 1〜4 位 ＋ 死の連鎖
        Console.WriteLine("## 1'. 代表台の誰が何Tに倒れたか（「死」＝ 本当の死: 最初に倒れた戦の割合%（T）／「起」＝ 起き上がり: 1戦の回数（最初の T））");
        Console.WriteLine();
        foreach (int bi in rep)
            for (int vi = 0; vi < VelOf.Length; vi++)
                Console.WriteLine($"- **{boards[bi].Name} × {VerNames[vi]}**: " + string.Join(" ／ ", new[] { 0, 1, 2, 5 }.Select(w => $"{W8[w].Name} {FellSplit(g[bi, vi, w], Apply(boards[bi].F, vi))}")));
        Console.WriteLine();

        Console.WriteLine("## 2. 起き上がり（1戦あたり: 起き上がり ／ 縫い付けた手番 ／ 縫えずに殴った手番 ／ 本当の死 ／ 起き上がりの平均T）");
        Console.WriteLine();
        CellTable("", rep.Select(bi => boards[bi]).ToArray(), Sub(g, rep), x => $"{Per(Xv(x, "rise"), x.N)} ／ {Per(Xv(x, "sewn"), x.N)} ／ {Per(Xv(x, "sewSwing"), x.N)} ／ {Per(Xv(x, "realDeaths"), x.N)} ／ T{RiseT(x)}", Key4);

        Console.WriteLine("## 3. リィカ（ボス）: 層 T1〜T8 ／ 9 層に届いた戦%・T ／ 層の出どころ（本当の死 ／ 起き上がる死 ／ 胞子 ／ 亡者）／ 亡者（起き上がる死から ／ 本当の死から）");
        Console.WriteLine();
        foreach (int bi in rep)
            for (int vi = 0; vi < VelOf.Length; vi++)
            {
                var x = g[bi, vi, 0];
                Console.WriteLine($"- **{boards[bi].Name} × {VerNames[vi]}**: 層 [{x.Layers(8)}] ／ 9 層 {Pct(Xv(x, "l9"), x.N)}%・T{Per(Xv(x, "l9T"), Xv(x, "l9"))} ／ 出どころ {Per(Xv(x, "srcReal"), x.N)} ／ {Per(Xv(x, "srcRise"), x.N)} ／ {Per(Xv(x, "srcSpore"), x.N)} ／ {Per(Xv(x, "srcWight"), x.N)} ／ 亡者 {Per(Xv(x, "wRise"), x.N)} ／ {Per(Xv(x, "wReal"), x.N)}");
            }
        Console.WriteLine();
        Console.WriteLine("同じ物差しを近衛 ／ 大隊で（層 T1〜T6 ／ 出どころ）:");
        Console.WriteLine();
        foreach (int bi in rep)
            for (int vi = 0; vi < VelOf.Length; vi++)
                Console.WriteLine($"- {boards[bi].Name} × {VerNames[vi]}: " + string.Join(" ／ ", new[] { 1, 2 }.Select(w => { var x = g[bi, vi, w]; return $"{W8[w].Name} 層 [{x.Layers(6)}]・出どころ {Per(Xv(x, "srcReal"), x.N)} ／ {Per(Xv(x, "srcRise"), x.N)} ／ {Per(Xv(x, "srcSpore"), x.N)} ／ {Per(Xv(x, "srcWight"), x.N)}"; })));
        Console.WriteLine();

        Console.WriteLine("## 4. ボス: 勇者の HP（ターンの頭・回復の後・T1〜T8）／ 倒した戦%・T ／ 隊の1ターンの最大の与ダメ");
        Console.WriteLine();
        foreach (int bi in rep)
            for (int vi = 0; vi < VelOf.Length; vi++)
            {
                var x = g[bi, vi, 0];
                string hp = string.Join(" ", Enumerable.Range(1, 8).Select(t => Xv(x, $"heroHpN{t}") == 0 ? "—" : F0((double)Xv(x, $"heroHp{t}") / Xv(x, $"heroHpN{t}"))));
                Console.WriteLine($"- **{boards[bi].Name} × {VerNames[vi]}**: HP [{hp}] ／ 倒した {Pct(Xv(x, "heroKill"), x.N)}%・T{Per(Xv(x, "heroKillT"), Xv(x, "heroKill"))} ／ 最大 {F0((double)Xv(x, "teamMax") / x.N)}");
            }
        Console.WriteLine();

        Signs(boards, g);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static string RiseT(S377.B x)
    {
        long n = 0, ts = 0;
        foreach (var (k, v) in x.X) { if (k.StartsWith("ri:")) n += v; else if (k.StartsWith("riT:")) ts += v; }
        return Per(ts, n);
    }
    static S377.B[,,] Sub(S377.B[,,] g, int[] rows)
    {
        var s = new S377.B[rows.Length, g.GetLength(1), g.GetLength(2)];
        for (int i = 0; i < rows.Length; i++) for (int v = 0; v < g.GetLength(1); v++) for (int w = 0; w < g.GetLength(2); w++) s[i, v, w] = g[rows[i], v, w];
        return s;
    }

    /// <summary>§3-3 の 8 壊れの兆し。</summary>
    static void Signs((string Name, Formation F)[] boards, S377.B[,,] g)
    {
        Console.WriteLine("## 8. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満の勝ち ／ 30T の打ち切りが規定より 5pt 以上増えた ／ 層 15 ／ 20 超 ／ 1戦の起き上がり 10 回超 ／ int のあふれ）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 1; vi < VelOf.Length; vi++)
            {
                string at = $"{boards[bi].Name} × {VerNames[vi]}";
                foreach (int wi in new[] { 1, 2 }) { var x = g[bi, vi, wi]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[wi].Name}: 1T 決着 {Pct(x.Win1T, x.N)}%"); }
                if (g[bi, vi, 0].BossFast > 0) signs.Add($"{at}: ボス 4T 未満の勝ち {Pct(g[bi, vi, 0].BossFast, g[bi, vi, 0].N)}%");
                for (int wi = 0; wi < W8.Length; wi++)
                {
                    var x = g[bi, vi, wi]; var x0 = g[bi, 0, wi];
                    double d = 100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N;
                    if (d >= 5) signs.Add($"{at} × {W8[wi].Name}: 30T の打ち切り {Pct(x0.Cap, x0.N)} → {Pct(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {W8[wi].Name}: あふれの兆し {x.Overflow} 件");
                }
                foreach (int wi in Key4)
                {
                    var x = g[bi, vi, wi];
                    if (Xv(x, "peak15") > 0) signs.Add($"{at} × {W8[wi].Name}: 層 15 超 {Pct(Xv(x, "peak15"), x.N)}%（20 超 {Pct(Xv(x, "peak20"), x.N)}%・規定 {Pct(Xv(g[bi, 0, wi], "peak15"), g[bi, 0, wi].N)}% ／ {Pct(Xv(g[bi, 0, wi], "peak20"), g[bi, 0, wi].N)}%）");
                    if (Xv(x, "rise10") > 0) signs.Add($"{at} × {W8[wi].Name}: 起き上がり 10 回超 {Pct(Xv(x, "rise10"), x.N)}%");
                }
            }
        if (signs.Count == 0) Console.WriteLine("- 無い。");
        foreach (var s in signs) Console.WriteLine("- " + s);
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 2 ヒーラー同席
    // ---------------------------------------------------------------------------------
    static void Heal()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = HealBoards();
        var g = Grid(boards, 100);
        Console.WriteLine($"# {Tag} §3-1 の 2 ヴェルとヒーラーが同席する台 —— 第380期の上位 3 台の耐久役（{string.Join(" ／ ", TankOfTop3)}）→ {string.Join(" ／ ", HealerSub)}（並びは元のまま・9 台）× ヴェル 4 列 × 8 物差し（seed 0..99）");
        Console.WriteLine();
        Console.WriteLine("規定のヴェルの列は「ヒーラー2枚」（規定のヴェルはヒーラー）の参考。版のヴェルはヒーラーではないので、版の列は「ヒーラー1枚 ＋ ヴェル」。");
        Console.WriteLine();
        CellTable("## 1. 勝率・決着T・倒れた数", boards, g, x => x.Cell);
        Console.WriteLine("4 波（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波）の平均（規定 → S2 ／ S2n ／ S10）・強い条件: ");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
            Console.WriteLine($"- {boards[bi].Name}: 4 波 {string.Join(" ／ ", Enumerable.Range(0, VelOf.Length).Select(vi => F1(Key4.Average(w => g[bi, vi, w].Win))))}・強 {string.Join(" ／ ", Enumerable.Range(0, VelOf.Length).Select(vi => F1(g[bi, vi, 7].Win)))}");
        Console.WriteLine();
        Console.WriteLine("## 2. 誰が何Tに倒れたか（ボス ／ 近衛 ／ 大隊・「死」＝ 本当の死 ／「起」＝ 起き上がり）");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < VelOf.Length; vi++)
                Console.WriteLine($"- {boards[bi].Name} × {VerNames[vi]}: " + string.Join(" ／ ", new[] { 0, 1, 2 }.Select(w => $"{W8[w].Name} {FellSplit(g[bi, vi, w], Apply(boards[bi].F, vi))}")));
        Console.WriteLine();
        Signs(boards, g);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 3 死の外
    // ---------------------------------------------------------------------------------
    static (double Mean, double Deaths) Quick(Formation f)
    {
        double win = 0; long deaths = 0, n = 0;
        foreach (int wi in Key4)
        {
            int w = 0;
            foreach (var wave in W8[wi].Ws)
                for (int s = 0; s < 20; s++)
                {
                    var (r, p, _) = I.Raw(f, wave, s, census: false);
                    if (r.PlayerWon) w++;
                    deaths += p.Count(u => u.LastDeathTurn > 0); n++;
                }
            win += 100.0 * w / (20 * W8[wi].Ws.Length);
        }
        return (win / Key4.Length, (double)deaths / n);
    }

    static void Outside()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3-1 の 3 死の外 —— {string.Join(" ／ ", OutsideNames)} で、ヴェル以外の1枚をヴェルに替えた台の最良（替える1枚 × 5 通り・seed 0..19 の4波で選び、最良だけ seed 0..99 × 8 物差し）");
        Console.WriteLine();
        Console.WriteLine("最良の選び方: ボス ／ 近衛 ／ 大隊 ／ 本編 第5波の勝率の平均（同点なら倒れた数の少ない方）。セル: 勝率・決着T・倒れた数（括弧は元の台との差 pt）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | ヴェル | 替えた1枚（席）| 選びの4波 | " + string.Join(" | ", W8.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|---|--:|" + string.Concat(W8.Select(_ => "---|")));
        foreach (var name in OutsideNames)
        {
            var f0 = Named(name);
            var orig = new S377.B[W8.Length];
            Parallel.For(0, W8.Length, wi => orig[wi] = M(f0, W8[wi].Ws, 100));
            var q0 = Quick(f0);
            Console.WriteLine($"| **{name}**（{S307.Seats(f0)}）| 元の台 | — | {F1(q0.Mean)} | {string.Join(" | ", orig.Select(x => x.Cell))} |");
            for (int vi = 0; vi < VelOf.Length; vi++)
            {
                var cands = f0.Occupied().Where(o => o.Def.Id != "vel").Select(o => (o.Slot, o.Def, F: Put(f0, o.Slot, VelOf[vi]))).ToArray();
                var qs = new (double Mean, double Deaths)[cands.Length];
                Parallel.For(0, cands.Length, k => qs[k] = Quick(cands[k].F));
                int best = Enumerable.Range(0, cands.Length).OrderByDescending(k => qs[k].Mean).ThenBy(k => qs[k].Deaths).First();
                var bf = cands[best].F;
                var res = new S377.B[W8.Length];
                Parallel.For(0, W8.Length, wi => res[wi] = M(bf, W8[wi].Ws, 100));
                string all5 = string.Join("・", Enumerable.Range(0, cands.Length).Select(k => $"{S307.Short(cands[k].Def)} {F1(qs[k].Mean)}"));
                Console.WriteLine($"| | {VerNames[vi]} | {S307.Short(cands[best].Def)}（席 {cands[best].Slot}）| {F1(qs[best].Mean)} | {string.Join(" | ", Enumerable.Range(0, W8.Length).Select(wi => $"{res[wi].Cell}（{(res[wi].Win - orig[wi].Win >= 0 ? "+" : "")}{F1(res[wi].Win - orig[wi].Win)}）"))} |");
                Console.Error.WriteLine($"{name} × {VerNames[vi]}: 5 通り {all5}");
                for (int wi = 0; wi < W8.Length; wi++)
                    if (res[wi].Cap > 0 || orig[wi].Cap > 0) _outsideLog.Add($"- {name} × {VerNames[vi]} × {W8[wi].Name}: 30T の打ち切り 元の台 {Pct(orig[wi].Cap, orig[wi].N)}% → {Pct(res[wi].Cap, res[wi].N)}%");
                _outsideLog.Add($"- {name} × {VerNames[vi]}: 替える1枚ごとの選びの4波 {all5}");
            }
        }
        Console.WriteLine();
        Console.WriteLine("替える1枚ごとの選びの4波（seed 0..19）と 30T の打ち切り（seed 0..99・元の台か最良の台で 0 でないもの）:");
        Console.WriteLine();
        foreach (var l in _outsideLog) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
    static readonly List<string> _outsideLog = new();

    // ---------------------------------------------------------------------------------
    // §3-3 の 7 `compare`
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw, wins = 0;
            for (int s = 0; s < 200; s++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / 200;
        });
        return g;
    }

    static void CompareVer()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin381(r.F))).ToArray();   // 第382期: 基準は第381期までの規定
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = CGrid(rows);
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        var all = Enumerable.Range(0, rows.Length);
        bool HasVel(Formation f) => f.Occupied().Any(o => o.Def.Id == "vel");
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(all.Average(r => x[r, w]))));
        int Over95(double[,] x) => all.Count(r => x[r, last] > 95.0);
        int Info(double[,] x, IEnumerable<int> rs) => rs.Sum(r => Enumerable.Range(1, nw - 1).Count(w => x[r, w] > 0 && x[r, w] < 100));
        Console.WriteLine($"# {Tag} §3-3 の 7 `compare` 64 行 × ヴェルの版（seed 0..199・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"ヴェルのいる行: {string.Join("、", rows.Where(r => HasVel(r.F)).Select(r => r.Name))}。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 動いた行 ／ セル（上がり ／ 落ち）| ヴェルのいない行で動いたセル | 主判定19行の第五波 | 全行の第1〜5波の平均 | 第五波 95% 超の行 | 情報セル（全行 ／ 主判定） |");
        Console.WriteLine("|---|---|--:|--:|---|--:|---|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} | {Over95(b)} | {Info(b, all)} ／ {Info(b, pr)} |");
        var detail = new List<string>();
        for (int vi = 1; vi < VelOf.Length; vi++)
        {
            var vr = rows.Select(r => (r.Name, F: Apply(r.F, vi))).ToArray();
            var g = CGrid(vr);
            int up = 0, dn = 0, moved = 0, novel = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                var cells = Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).ToList();
                if (!HasVel(rows[r].F)) { novel += cells.Count + (g[r, 0] != b[r, 0] ? 1 : 0); continue; }
                up += cells.Count(w => g[r, w] > b[r, w]); dn += cells.Count(w => g[r, w] < b[r, w]);
                if (cells.Count > 0) { moved++; detail.Add($"- {VerNames[vi]} {rows[r].Name}: " + string.Join("・", cells.Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}"))); }
            }
            Console.WriteLine($"| {VerNames[vi]} | {moved} ／ {up + dn}（{up} ／ {dn}）| {novel} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} | {Over95(g)} | {Info(g, all)} ／ {Info(g, pr)} |");
            if (novel > 0) Console.WriteLine($"| **止める** | ヴェルのいない行が {novel} セル動いた | | | | | |");
        }
        Console.WriteLine();
        foreach (var l in detail) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
