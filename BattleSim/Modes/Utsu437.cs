using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// =====================================================================================
// utsu437 —— 第437期【軽い期】段0: ウツの U-d2 を規定に ／ 本体: クビとウツを隣にした「試遊・呪い」の席を探す。
// 指示書は design/PHASE437_UTSU_DAUNT_SETTLE_SPEC.md ／ 報告は design/PHASE437_UTSU_DAUNT_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 utsu437 seats    # §2: 「試遊・呪い」の 5 枚の 120 並び × {規定 U-d2, 旧 UtsuH435} × ボス ／ 近衛 ／ 大隊 ／ 本編 第4・5波・seed 0..19
//                                                                 #     → U-d2 の上位 10 ＋ 隣り合う上位 5 ＋ 今の試遊・呪い を seed 0..99 で回し直す
//     dotnet run --project BattleSim -c Release 0 utsu437 verify   # 段0: 規定のウツ ＝ 第436期の U-d2（`UtsuUd2`）と全戦一致（第436期の 4 台 × 6 波 × seed 0..99）
//     dotnet run --project BattleSim -c Release 0 utsu437 check    # 自己検査
//
// **予測のファイルが無ければ seats を走らせない**（R397・`.tmp/p437/predict.md`）。
// =====================================================================================
static class Utsu437Diag
{
    const string PredictFile = ".tmp/p437/predict.md";
    const int Coarse = 20, Fine = 100;
    internal const string PlayRow = "試遊・呪い 萎縮";
    static readonly I.W[] Waves = { I.Boss, I.Guard, I.Bat, I.M4, I.M5 };
    static readonly (string Key, string Label, UnitDef D)[] Vers = { ("d2", "規定 U-d2", UnitCatalog.Utsu), ("old", "旧 UtsuH435", UnitCatalog.UtsuH435) };

    /// <summary>今の「試遊・呪い」（前1 ワタ ／ 前3 ムド ／ 中央 ネル ／ 後1 クビ ／ 後3 ウツ）。</summary>
    internal static Formation Current => Presets.CurseCenterNel();
    static readonly UnitDef[] Five = { UnitCatalog.Wata, UnitCatalog.Mudo, UnitCatalog.Nel, UnitCatalog.Kubi, UnitCatalog.Utsu };

    /// <summary>5 枚の全並び（席 0..4 に置く駒の添字の順列・120 通り）。</summary>
    internal static List<int[]> Perms()
    {
        var all = new List<int[]>();
        void Go(int[] p, int k)
        {
            if (k == p.Length) { all.Add((int[])p.Clone()); return; }
            for (int i = k; i < p.Length; i++) { (p[k], p[i]) = (p[i], p[k]); Go(p, k + 1); (p[k], p[i]) = (p[i], p[k]); }
        }
        Go(new[] { 0, 1, 2, 3, 4 }, 0);
        return all.OrderBy(p => string.Concat(p)).ToList();
    }
    internal static Formation Build(int[] p, UnitDef utsu)
    {
        var g = Current.Clone();
        for (int s = 0; s < 5; s++) g[s] = Five[p[s]].Id == "utsu" ? utsu : Five[p[s]];
        return g;
    }
    internal static bool Adjacent(Formation f)
    {
        int k = f.Occupied().First(o => o.Def.Id == "kubi").Slot, u = f.Occupied().First(o => o.Def.Id == "utsu").Slot;
        return FormationRules.AreAdjacent(k, u);
    }
    static bool SameSeats(Formation a, Formation b) => Enumerable.Range(0, 5).All(i => a[i]?.Id == b[i]?.Id);

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");

    sealed class Fx
    {
        public long N, Wins, WinT, LeakHitters, Volleys, UtsuDied, UtsuDeathT, DauntSwings;
        public void Add(Fx o) { N += o.N; Wins += o.Wins; WinT += o.WinT; LeakHitters += o.LeakHitters; Volleys += o.Volleys; UtsuDied += o.UtsuDied; UtsuDeathT += o.UtsuDeathT; DauntSwings += o.DauntSwings; }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public double T => Wins == 0 ? 99 : (double)WinT / Wins;
        public string WT => Wins == 0 ? F1(Win) : $"{F1(Win)}・{T:0.0}T";
    }

    static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, _) = I.Raw(f, w, seed);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var id in new[] { "wata", "mudo" })
            if (r.TallyByUnit.TryGetValue(id, out var t) && t.Dn436From is { } d) a.LeakHitters += d[0];
        if (r.TallyByUnit.TryGetValue("utsu", out var ut)) { a.Volleys = ut.Ud436Volleys; a.DauntSwings = ut.DauntedSwings; }
        var u = p.FirstOrDefault(x => x.Def.Id == "utsu");
        if (u is not null && u.LastDeathTurn > 0) { a.UtsuDied = 1; a.UtsuDeathT = u.LastDeathTurn; }
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    sealed record Row(int[] P, Formation F, Fx[] W)
    {
        public double Key1 => W[0].Win + W[1].Win + W[2].Win;
        public double BossT => W[0].T;
        public double EliteT => (W[1].T + W[2].T) / 2;
    }
    static Row MeasureRow(int[] p, UnitDef utsu, int n) { var f = Build(p, utsu); return new Row(p, f, Waves.Select(w => Measure(f, w, n)).ToArray()); }
    static IEnumerable<Row> Rank(IEnumerable<Row> rows) => rows.OrderByDescending(r => r.Key1).ThenBy(r => r.BossT).ThenBy(r => r.EliteT).ThenBy(r => string.Concat(r.P), StringComparer.Ordinal);

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode == "seats" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"utsu437: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "seats": Seats(); break;
            case "verify": Verify(); break;
            case "check": Check(); break;
            default: Console.WriteLine("utsu437: モードは seats / verify / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Seats()
    {
        var perms = Perms();
        int[] cur = perms.First(p => SameSeats(Build(p, UnitCatalog.Utsu), Current));
        var coarse = Vers.ToDictionary(v => v.Key, v => Rank(perms.Select(p => MeasureRow(p, v.D, Coarse))).ToList());
        var d2 = coarse["d2"];
        var top10 = d2.Take(10).Select(r => r.P).ToList();
        var adj5 = d2.Where(r => Adjacent(r.F)).Take(5).Select(r => r.P).ToList();
        var pick = top10.Concat(adj5).Append(cur).Distinct(new SeqEq()).ToList();
        var fine = Vers.ToDictionary(v => v.Key, v => pick.Select(p => MeasureRow(p, v.D, Fine)).ToList());
        var fineD2 = Rank(fine["d2"]).ToList();
        Row Old(int[] p) => fine["old"].First(r => r.P.SequenceEqual(p));
        int RankOf(List<Row> list, int[] p) => list.FindIndex(r => r.P.SequenceEqual(p)) + 1;

        Console.WriteLine($"# 第437期 —— 「試遊・呪い」の 5 枚の席（120 並び × 2 種のウツ・粗 seed 0..{Coarse - 1} → 細 seed 0..{Fine - 1}）");
        Console.WriteLine();
        Console.WriteLine($"- 並べ方: ボス ＋ 近衛 ＋ 大隊 の勝率の和 → ボスの倒しT → 精鋭の倒しT。席は 前1・前3・中央・後1・後3。");
        Console.WriteLine($"- クビとウツが隣り合う並び: {perms.Count(p => Adjacent(Build(p, UnitCatalog.Utsu)))} ／ {perms.Count}");
        Console.WriteLine($"- 粗の上位 10 に入った隣り合う並び: U-d2 {d2.Take(10).Count(r => Adjacent(r.F))} 本 ／ 旧 {coarse["old"].Take(10).Count(r => Adjacent(r.F))} 本");
        Console.WriteLine($"- 今の試遊・呪い（{S307.Seats(Current)}）の粗の順位: U-d2 {RankOf(d2, cur)} 位 ／ 旧 {RankOf(coarse["old"], cur)} 位（同点を含む並べ方の順）");
        Console.WriteLine();

        Console.WriteLine("## 表1 細（seed 0..99）: U-d2 の上位 10 ＋ 隣り合う上位 5 ＋ 今の試遊・呪い");
        Console.WriteLine();
        Console.WriteLine("| 細の順位 | 並び | 印 | 隣 | ボス | 近衛 | 大隊 | 第4波 | 第5波 | 漏れがワタ ／ ムドに乗った ／ 戦 | 萎縮の一振り ／ 戦 | ウツが倒れた（割合・平均T） | 旧のウツ ボス ／ 近衛 ／ 大隊（差） |");
        Console.WriteLine("|--:|---|---|:-:|---|---|---|---|---|--:|--:|---|---|");
        foreach (var (r, i) in fineD2.Select((r, i) => (r, i)))
        {
            var o = Old(r.P);
            string mark = string.Join("", new[] { top10.Any(p => p.SequenceEqual(r.P)) ? "上位" : "", adj5.Any(p => p.SequenceEqual(r.P)) ? "隣" : "", r.P.SequenceEqual(cur) ? "今" : "" }.Where(x => x != ""));
            long n = r.W.Sum(x => x.N);
            string diff = string.Join(" ／ ", Enumerable.Range(0, 3).Select(k => $"{F1(o.W[k].Win)}（{Delta(o.W[k].Win - r.W[k].Win)}）"));
            Console.WriteLine($"| {i + 1} | {S307.Seats(r.F)} | {mark} | {(Adjacent(r.F) ? "○" : "—")} | {string.Join(" | ", r.W.Select(x => x.WT))} | {Per1(r.W.Sum(x => x.LeakHitters), n)} | {Per1(r.W.Sum(x => x.Volleys), n)} | {Pc(r.W.Sum(x => x.UtsuDied), n)}・T{Per1(r.W.Sum(x => x.UtsuDeathT), r.W.Sum(x => x.UtsuDied))} | {diff} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表2 粗（seed 0..19）の上位 15（U-d2 ／ 旧）");
        Console.WriteLine();
        foreach (var v in Vers)
        {
            Console.WriteLine($"### {v.Label}");
            Console.WriteLine();
            Console.WriteLine("| 順位 | 並び | 隣 | ボス | 近衛 | 大隊 | 第4波 | 第5波 |");
            Console.WriteLine("|--:|---|:-:|---|---|---|---|---|");
            foreach (var (r, i) in coarse[v.Key].Take(15).Select((r, i) => (r, i)))
                Console.WriteLine($"| {i + 1} | {S307.Seats(r.F)} | {(Adjacent(r.F) ? "○" : "—")} | {string.Join(" | ", r.W.Select(x => x.WT))} |");
            Console.WriteLine();
        }

        // §4 の線
        var bestAdj = fineD2.First(r => Adjacent(r.F));
        var now = fine["d2"].First(r => r.P.SequenceEqual(cur));
        var bo = Old(bestAdj.P);
        bool l1 = Enumerable.Range(0, 3).All(k => bestAdj.W[k].Win >= now.W[k].Win - 5) && bestAdj.BossT <= now.BossT;
        Console.WriteLine("## §4 の線");
        Console.WriteLine();
        Console.WriteLine($"- 隣り合う 1 位（細）: {S307.Seats(bestAdj.F)}——ボス {bestAdj.W[0].WT} ／ 近衛 {bestAdj.W[1].WT} ／ 大隊 {bestAdj.W[2].WT}");
        Console.WriteLine($"- 今の試遊・呪い（細）: ボス {now.W[0].WT} ／ 近衛 {now.W[1].WT} ／ 大隊 {now.W[2].WT}");
        Console.WriteLine($"- 線1 ボス ／ 近衛 ／ 大隊 が −5 以内・ボスの倒しT が同じか短い: {(l1 ? "通る" : "通らない")}（差 {string.Join(" ／ ", Enumerable.Range(0, 3).Select(k => Delta(bestAdj.W[k].Win - now.W[k].Win)))}・ボスT {bestAdj.BossT:0.0} vs {now.BossT:0.0}）");
        Console.WriteLine($"- 線2 旧のウツで同じ並び: ボス {bo.W[0].WT} ／ 近衛 {bo.W[1].WT} ／ 大隊 {bo.W[2].WT}（U-d2 との差 {string.Join(" ／ ", Enumerable.Range(0, 3).Select(k => Delta(bo.W[k].Win - bestAdj.W[k].Win)))}）");
        Console.WriteLine();
    }

    sealed class SeqEq : IEqualityComparer<int[]>
    {
        public bool Equals(int[]? a, int[]? b) => a!.SequenceEqual(b!);
        public int GetHashCode(int[] a) => a.Aggregate(17, (h, x) => h * 31 + x);
    }

    static void Verify()
    {
        bool ok = true;
        void Req(bool c, string what) { Console.WriteLine($"{(c ? "○" : "×")} {what}"); ok &= c; }
        long n = 0, bad = 0;
        I.W[] ws = { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5 };
        foreach (var b in Utsu436Diag.Boards())
            foreach (var w in ws)
            {
                var res = new bool[Fine];
                Parallel.For(0, Fine, s =>
                {
                    var (r0, _, _) = I.Raw(Utsu436Diag.Swap(b.F, UnitCatalog.Utsu), w, s);
                    var (r1, _, _) = I.Raw(Utsu436Diag.Swap(b.F, UnitCatalog.UtsuUd2), w, s);
                    var t0 = r0.TallyByUnit.GetValueOrDefault("utsu") ?? new UnitTally(); var t1 = r1.TallyByUnit.GetValueOrDefault("utsu") ?? new UnitTally();
                    res[s] = r0.PlayerWon == r1.PlayerWon && r0.Turns == r1.Turns && t0.Ud436Swings == t1.Ud436Swings && t0.Ud436Added == t1.Ud436Added && t0.Ud436Volleys == t1.Ud436Volleys;
                });
                n += Fine; bad += res.Count(x => !x);
            }
        Req(bad == 0, $"規定のウツ ＝ U-d2（`UtsuUd2`）: 第436期の 4 台 × 6 波 × seed 0..{Fine - 1} の {n} 戦で勝敗 ／ T ／ 萎縮の一撃 ／ 倍の量 ／ 一振りが一致（不一致 {bad}）");
        Console.WriteLine(ok ? "utsu437 verify: すべて ○" : "utsu437 verify: × あり");
        if (!ok) Environment.ExitCode = 1;
    }

    static void Check()
    {
        bool ok = true;
        void Req(bool c, string what) { Console.WriteLine($"{(c ? "○" : "×")} {what}"); ok &= c; }
        var u = UnitCatalog.Utsu; var h = UnitCatalog.UtsuH435;
        Req(u.Traits.SequenceEqual(h.Traits.Append(TraitId.UtsuDauntVolley)) && u.MaxHp == h.MaxHp && u.Attack == h.Attack && u.Speed == h.Speed && u.MinusText == h.MinusText, "規定のウツ ＝ 旧 `UtsuH435` ＋ `UtsuDauntVolley`（HP ／ 攻 ／ 速 ／ マイナスは同じ）");
        Req(u.Traits.SequenceEqual(UnitCatalog.UtsuUd2.Traits) && u.PlusText == UnitCatalog.UtsuUd2.PlusText, "規定のウツの札と文面 ＝ 第436期の U-d2");
        Req(h.Traits.SequenceEqual(new[] { TraitId.Perverse, TraitId.PerverseOver }), "旧 ＝ 逆しま ＋ 上限超え（第354〜436期の規定）");
        Req(UnitCatalog.All.Contains(u) && !UnitCatalog.Everyone.Contains(h), "`All` は規定のウツ・旧は `All` ／ `Retired` の外");
        Req(UnitCatalog.UtsuUd1.Traits.SequenceEqual(h.Traits.Append(TraitId.UtsuDauntDouble)) && UnitCatalog.UtsuUd3.Traits.SequenceEqual(h.Traits.Append(TraitId.UtsuDauntCurse)) && UnitCatalog.UtsuU.Traits.SequenceEqual(h.Traits.Append(TraitId.UtsuScar)), "版 U-d1 ／ U-d3 ／ 爪痕の土台は旧");
        Req(UnitCatalog.Everyone.Where(d => d.Traits.Contains(TraitId.UtsuDauntVolley)).SequenceEqual(new[] { u }), "`UtsuDauntVolley` の保持者は規定のウツだけ（`All` ／ `Retired`）");
        Req(!Presets.Versions.Any(r => r.Name == "版・萎縮 ウツ"), "「版・萎縮 ウツ」は `Presets.Versions` から外した");
        Req(ReferenceEquals(PinU435(u), h) && ReferenceEquals(PinU435(UnitCatalog.Kubi), UnitCatalog.Kubi), "`PinU435` は規定のウツだけを旧へ");
        var perms = Perms();
        Req(perms.Count == 120 && perms.Select(p => string.Concat(p)).Distinct().Count() == 120, "5 枚の全並び 120 通り");
        Req(perms.Count(p => SameSeats(Build(p, u), Current)) == 1 && !Adjacent(Current), "今の試遊・呪いは 120 通りの 1 つ・クビとウツは隣でない");
        Req(perms.Count(p => Adjacent(Build(p, u))) == 72, "クビとウツが隣り合う並びは 72 通り（どちらかが中央 48 ＋ 同じレーンの前後 24）");
        var play = Presets.Versions.FirstOrDefault(r => r.Name == PlayRow).F;
        if (play is not null)
            Req(Adjacent(play) && play.Occupied().Select(o => o.Def).OrderBy(d => d.Id, StringComparer.Ordinal).SequenceEqual(Five.OrderBy(d => d.Id, StringComparer.Ordinal)) && !VersionsH407().Any(r => r.Name == PlayRow),
                $"試遊の台「{PlayRow}」は同じ 5 枚・クビとウツが隣・鎖の終端 VersionsH407 が外す");
        else Console.WriteLine($"— 試遊の台「{PlayRow}」は置いていない（§4 の線を通らなかった）");
        Console.WriteLine(ok ? "utsu437 check: すべて ○" : "utsu437 check: × あり");
        if (!ok) Environment.ExitCode = 1;
    }
}
