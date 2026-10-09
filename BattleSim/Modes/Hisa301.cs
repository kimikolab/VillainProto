using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// hisa301 —— 第301期「標軸の仕上げ（ソラ・ザン・ヒサの小修正）＋ ヒサの号令（過剰回復を層に換える）と庇い」。
// 指示書は design/PHASE301_HISA_COMMAND_SPEC.md ／ 報告は design/PHASE301_HISA_COMMAND.md。
//
//     dotnet run --project BattleSim -c Release 0 hisa301 seat [seeds]     # §2-4 ザンのいない台の席（120 通り・段0 の後の規定）
//     dotnet run --project BattleSim -c Release 0 hisa301 p0 [seeds]       # §4 Phase 0（溢れ ／ 倒れる一撃 ／ ソラの剥がし ／ 同士討ち ／ 層の膨張）
//     dotnet run --project BattleSim -c Release 0 hisa301 stage [seeds]    # §2-4 段0 の参考の数字（第300期の規定 → 段0-1 → 段0-2 → 段0-3）
//     dotnet run --project BattleSim -c Release 0 hisa301 ver [seeds]      # §5 段1 の版（K0〜K5）× 代表台 × 波
//     dotnet run --project BattleSim -c Release 0 hisa301 four [seeds]     # §5-3 手番版の「4段」（号令 → 羽の割り込み → ミサの一斉射撃 → ザンの仇巡り）
//     dotnet run --project BattleSim -c Release 0 hisa301 cmp [seeds]      # §5-2 `compare` 64 行 × 組（ヒサ在席の行・(G2)）
//     dotnet run --project BattleSim -c Release 0 hisa301 find [seeds]     # 台本の例（号令 ／ 庇い ／ ヒサ単独の指差し）
//     dotnet run --project BattleSim -c Release 0 hisa301 memo <台の一部> <1..5|guard|bat|boss> <seed> [版 K0..K5] [最後のT]
//     dotnet run --project BattleSim -c Release 0 hisa301 check            # 自己検査
// =====================================================================================
static class Hisa301Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "seat": Seat(int.Parse(A(3, "50"))); return;
            case "p0": Phase0(int.Parse(A(3, "200"))); return;
            case "stage": Stage(int.Parse(A(3, "200"))); return;
            case "ver": Versions(int.Parse(A(3, "200"))); return;
            case "four": Four(int.Parse(A(3, "50"))); return;
            case "cmp": Cmp(int.Parse(A(3, "200"))); return;
            case "find": Find(int.Parse(A(3, "50"))); return;
            case "memo": Memo(A(3, "循環"), A(4, "boss"), int.Parse(A(5, "0")), A(6, "K2"), int.Parse(A(7, "99"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("hisa301: モードは seat / p0 / stage / ver / four / cmp / find / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 共通
    // ---------------------------------------------------------------------------------
    static Doha297Diag.Wave[] Waves() => Doha297Diag.Waves();
    static Doha297Diag.Wave WaveOf(string k) => Waves().First(w => w.Key == k);
    static Doha297Diag.Wave[] MainWaves() => new[] { "2", "3", "4", "5", "guard", "bat", "boss" }.Select(WaveOf).ToArray();
    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation CompareRow(string n) => CompareBuilds().First(r => r.Name == n).F;
    static Formation Map(Formation f, params (UnitDef From, UnitDef To)[] m)
    {
        var g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied())
            foreach (var (a, b) in m) if (ReferenceEquals(d, a)) { g[slot] = b; break; }
        return g;
    }

    /// <summary>
    /// §2-4 ザンのいない台（ヒサ・ミサ ＋ 同士討ちを出す駒 ボルグ ／ カド ＋ ゴルム）。席は `hisa301 seat`（段0 の後の規定・seed 0..49・120 通り）で決めた（報告 §2-4）——
    /// 勝率の得点（第2〜5波 ＋ 精鋭 ＋ ボスの平均）は上位が 85.7 で同値の塊になったので、同値の中は「ヒサ単独の指差しが多い順」で割った（この台の狙いが指差しなので）。
    /// 前1 カド ／ 前3 ボルグ ／ 中央 ゴルム ／ 後1 ヒサ ／ 後3 ミサ。
    /// </summary>
    internal static Formation NoZan => Formation.Build(front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Golm, back1: UnitCatalog.Hisa, back3: UnitCatalog.Tome);
    internal const string NoZanName = "ザンなし (ヒサ×ミサ×ボルグ×カド)";
    static readonly UnitDef[] NoZanFive = { UnitCatalog.Hisa, UnitCatalog.Tome, UnitCatalog.Borg, UnitCatalog.Kado, UnitCatalog.Golm };

    /// <summary>§5-2 代表台（試遊・標の5行 ＋ 標経済 ＋ ザンのいない台）。</summary>
    static (string Name, Formation F)[] Boards() =>
        Presets.Playtest.Where(r => r.Name.StartsWith("試遊・標", StringComparison.Ordinal)).Select(r => (r.Name, r.F))
            .Append(("標経済 (ヒサ×ザン×ミサ)", CompareRow("標経済 (ヒサ×ザン×ミサ)")))
            .Append((NoZanName, NoZan)).ToArray();

    /// <summary>§2-4 段0 の参考の台（循環 ／ 三人組 ／ 守り型 ／ 標経済 ／ ザンのいない台）。</summary>
    static (string Name, Formation F)[] StageBoards() => Boards().Where(b => !b.Name.Contains("ボス台") && !b.Name.Contains("道中")).ToArray();

    /// <summary>段0 の段: 0 第300期の規定（`Pin301`）／ 1 ＋ソラ（矢面は剥がさない）／ 2 ＋ザン（標を付けてから斬る）／ 3 ＋ヒサ（あいつがやった！）＝ 今の規定。</summary>
    static readonly string[] StageNames = { "第300期の規定", "＋ソラ 矢面は剥がさない（段0-1）", "＋ザン 標を付けてから斬る（段0-2）", "＋ヒサ あいつがやった！（段0-3）" };
    static Formation St(Formation f, int k) => k switch
    {
        0 => Pin301(f),
        1 => Map(f, (UnitCatalog.Zan, UnitCatalog.ZanZMa), (UnitCatalog.Hisa, UnitCatalog.HisaHKf)),
        2 => Map(f, (UnitCatalog.Hisa, UnitCatalog.HisaHKf)),
        _ => f,
    };

    /// <summary>§3-3 の組。K5 は K1〜K3 で代表台がいちばんよかった号令 ＋ 庇い（`ver` が決める）。</summary>
    internal sealed record Ver(string Key, string Name, UnitDef D);
    internal static readonly Ver[] Base =
    {
        new("K0", "規定", UnitCatalog.Hisa), new("K1", "HL-i", UnitCatalog.HisaHLi), new("K2", "HL-t3", UnitCatalog.HisaHLt3),
        new("K3", "HL-t8", UnitCatalog.HisaHLt8), new("K4", "HC", UnitCatalog.HisaHC),
    };
    static Ver K5Of(string hl) => hl switch
    {
        "K1" => new("K5", "HL-i ＋ HC", UnitCatalog.HisaHCi),
        "K2" => new("K5", "HL-t3 ＋ HC", UnitCatalog.HisaHCt3),
        _ => new("K5", "HL-t8 ＋ HC", UnitCatalog.HisaHCt8),
    };
    static Formation With(Formation f, UnitDef hisa) => FvSwap(f, UnitCatalog.Hisa, hisa);

    // ---------------------------------------------------------------------------------
    // 1戦の集計
    // ---------------------------------------------------------------------------------
    sealed class Agg
    {
        public long N, Wins, WinT, Turns, FirstDeathN, FirstDeathT, HisaDeathN, HisaDeathT,
            Feathers, Volleys, VolleyShots, RoundSlashes, RoundCapped, RoundTurns, Overflow, CmdFires, CmdLayers, CmdCapped, CmdTurns, CmdFeathers, CmdNoTarget, CmdNowTurns,
            CoverFires, CoverBattles, CoverDied, CoverSurvived, CoverRaw, CoverHushed, CoverBlocked, PeakSum, FeatherMaxSum, PressureHits, PressureCap,
            AccuseZan, AccuseSolo, FrameAccuses, FrameVend, VendFresh, VendFreshShouts, GuardHit, Stripped, StripBeckon, KeptBeckon, StripOther,
            Lethal, LethalHisa, LethalBattles, MfFoe, MfAlly;
        public long PeakMax, FeatherMax;
        public long[] OverByTurn = new long[21];
        public long[] CmdHist = new long[9];
        public void Add(Agg o)
        {
            foreach (var f in typeof(Agg).GetFields())
            {
                if (f.FieldType != typeof(long)) continue;
                if (f.Name is "PeakMax" or "FeatherMax") f.SetValue(this, Math.Max((long)f.GetValue(this)!, (long)f.GetValue(o)!));
                else f.SetValue(this, (long)f.GetValue(this)! + (long)f.GetValue(o)!);
            }
            for (int i = 0; i < OverByTurn.Length; i++) OverByTurn[i] += o.OverByTurn[i];
            for (int i = 0; i < CmdHist.Length; i++) CmdHist[i] += o.CmdHist[i];
        }
        public double P(long x) => N == 0 ? 0 : (double)x / N;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public double WinTurns => Wins == 0 ? 0 : (double)WinT / Wins;
    }

    static Agg One(Formation f, Func<List<UnitState>> enemy, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = enemy();
        var r = BattleEngine.Run(p, e, seed, verbose: false);
        var a = new Agg { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var dead = p.Where(u => u.LastDeathTurn > 0).Select(u => u.LastDeathTurn).ToList();
        if (dead.Count > 0) { a.FirstDeathN = 1; a.FirstDeathT = dead.Min(); }
        var hisa = p.FirstOrDefault(u => u.Def.Id == "hisa");
        if (hisa is not null && hisa.LastDeathTurn > 0) { a.HisaDeathN = 1; a.HisaDeathT = hisa.LastDeathTurn; }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!mine.Contains(id))
            {
                if (t.MarkPeak > a.PeakMax) a.PeakMax = t.MarkPeak;
                continue;
            }
            a.Feathers += t.FeatherChased + t.FeatherSprayed + t.MfShotsFoe + t.MfShotsAlly;
            a.MfFoe += t.MfShotsFoe; a.MfAlly += t.MfShotsAlly;
            a.Volleys += t.FeatherVolleys; a.VolleyShots += t.FeatherShots;
            if (t.FeatherMax > a.FeatherMax) a.FeatherMax = t.FeatherMax;
            a.RoundSlashes += t.RoundSlashes; a.RoundCapped += t.RoundCapped; a.RoundTurns += t.RoundTurns;
            a.Overflow += t.RallyOverflow; a.CmdFires += t.CommandFires; a.CmdLayers += t.CommandLayers; a.CmdCapped += t.CommandCapped; a.CmdTurns += t.CommandTurns;
            a.CmdFeathers += t.CommandFeathers; a.CmdNoTarget += t.CommandNoTarget; a.CmdNowTurns += t.CommandNowTurns;
            if (t.RallyOverByTurn is { } ob) for (int i = 0; i < ob.Length; i++) a.OverByTurn[i] += ob[i];
            if (t.CommandTurnHist is { } ch) for (int i = 0; i < ch.Length; i++) a.CmdHist[i] += ch[i];
            a.CoverFires += t.CoverFires; a.CoverDied += t.CoverDied; a.CoverRaw += t.CoverRaw; a.CoverHushed += t.CoverHushed; a.CoverBlocked += t.CoverBlocked;
            a.PressureHits += t.PressureHits; if (t.PressureHitsByLayer is { } ph) a.PressureCap += ph[3];
            a.AccuseZan += t.AccuseWithZan; a.AccuseSolo += t.AccuseSolo; a.FrameAccuses += t.FrameAccuses; a.FrameVend += t.FrameVendettas;
            a.VendFresh += t.VendettaFresh; a.VendFreshShouts += t.VendettaFreshShouts;
            a.GuardHit += t.BeckonGuardHitN; a.Stripped += t.BeckonStrippedHits;
            a.StripBeckon += t.DivertStripBeckon; a.KeptBeckon += t.DivertKeptBeckon; a.StripOther += t.DivertStripOther;
            a.Lethal += t.LethalOnAlly; a.LethalHisa += t.LethalHisaAlive;
        }
        if (a.CoverFires > 0) a.CoverBattles = 1;
        foreach (var u in p) if (r.TallyByUnit.TryGetValue(u.Def.Id, out var ut) && ut.CoverSaved > 0 && u.LastDeathTurn == 0) a.CoverSurvived++;
        if (a.Lethal > 0) a.LethalBattles = 1;
        a.PeakSum = a.PeakMax;
        a.FeatherMaxSum = a.FeatherMax;
        return a;
    }

    static Agg Many(Formation f, Func<List<UnitState>> enemy, int seeds, int from = 0)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, s => parts[s] = One(f, enemy, from + s));
        var a = new Agg();
        foreach (var x in parts) a.Add(x);
        return a;
    }

    static string Avg(long sum, long n) => n == 0 ? "—" : $"{(double)sum / n:F1}";
    static string Pct(long x, long n) => n == 0 ? "—" : $"{100.0 * x / n:F0}%";
    static string FirstDeath(Agg a) => a.FirstDeathN == 0 ? "—" : $"{(double)a.FirstDeathT / a.FirstDeathN:F1}（{100.0 * a.FirstDeathN / a.N:F0}%）";
    static string HisaDeath(Agg a) => a.HisaDeathN == 0 ? "—" : $"{(double)a.HisaDeathT / a.HisaDeathN:F1}（{100.0 * a.HisaDeathN / a.N:F0}%）";
    static string Cell(Agg a) => a.Wins == 0 ? $"{a.Win:F1}" : $"{a.Win:F1}（{a.WinTurns:F1}）";

    // ---------------------------------------------------------------------------------
    // §2-4 ザンのいない台の席
    // ---------------------------------------------------------------------------------
    static IEnumerable<int[]> Perms(int n)
    {
        var a = Enumerable.Range(0, n).ToArray();
        IEnumerable<int[]> Rec(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var x in Rec(k + 1)) yield return x;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Rec(0);
    }

    static void Seat(int seeds)
    {
        var waves = MainWaves();
        var rows = new List<(string Seats, double Score, double Elite, double Acc, Formation F)>();
        foreach (var perm in Perms(5).ToList())
        {
            var g = new Formation();
            for (int s = 0; s < 5; s++) g[s] = NoZanFive[perm[s]];
            var aggs = waves.Select(w => Many(g, w.Make, seeds)).ToArray();
            double score = aggs.Average(x => x.Win);
            double elite = (aggs[4].Win + aggs[5].Win) / 2;
            double acc = aggs.Sum(x => x.P(x.AccuseSolo));
            rows.Add((string.Join("・", Enumerable.Range(0, 5).Select(i => Short(g[i]!))), score, elite, acc, g));
        }
        Console.WriteLine($"# hisa301 seat —— ザンのいない台の席（ヒサ・ミサ・ボルグ・カド・ゴルム・段0 の後の規定・seed 0..{seeds - 1}・120 通り）");
        Console.WriteLine();
        Console.WriteLine("得点 ＝ 第2〜5波 ＋ 近衛 ＋ 大隊 ＋ ボスの勝率の平均。席の順は 前1・前3・中央・後1・後3。指差し ＝ ヒサ単独の「あいつがやった！」（1戦あたり・7 波の和）。");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 席 | 得点 | 精鋭の平均 | 指差し |");
        Console.WriteLine("|--:|---|--:|--:|--:|");
        int k = 0;
        foreach (var r in rows.OrderByDescending(r => r.Score).ThenByDescending(r => r.Acc).Take(10))
            Console.WriteLine($"| {++k} | {r.Seats} | {r.Score:F1} | {r.Elite:F1} | {r.Acc:F2} |");
        var cur = rows.First(r => r.F.Occupied().SequenceEqual(NoZan.Occupied()));
        Console.WriteLine();
        Console.WriteLine($"いま `NoZan` に置いている席: {cur.Seats}（得点 {cur.Score:F1} ／ 指差し {cur.Acc:F2}・順位 {rows.OrderByDescending(r => r.Score).ThenByDescending(r => r.Acc).ToList().IndexOf(cur) + 1}）");
    }

    // ---------------------------------------------------------------------------------
    // §4 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0(int seeds)
    {
        var waves = MainWaves();
        Console.WriteLine($"# 第301期 Phase 0（seed 0..{seeds - 1}・1戦あたり）");
        Console.WriteLine();

        // 1. 溢れ
        Console.WriteLine("## 1. 叫びの溢れ（段0 の後の規定）と号令の層の見込み");
        Console.WriteLine();
        Console.WriteLine("溢れ ＝ 叫びの回復のうち満タンで入らなかった量（攻撃した味方が満タンのときの 0 回復を含む）。1T ＝ 決着T で割った量。");
        Console.WriteLine("見込みは溢れをターンごとに溜め、HL-i ＝ そのターンに 20 ごと・2 層まで、HL-t ＝ 次のターンの頭（ヒサは速さ 10）に 20 ごと・3 ／ 8 層まで刻むと置いた紙の計算（決着の後のターンは数えない）。張り付き ＝ 上限で止まったターン ÷ 刻んだターン。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 溢れ（1戦）| 溢れ（1T）| 層の見込み HL-i（張り付き）| HL-t3（張り付き）| HL-t8（張り付き）|");
        Console.WriteLine("|---|---|--:|--:|---|---|---|");
        var p0Boards = Boards().Where(b => b.Name.StartsWith("試遊・標") || b.Name.StartsWith("標経済")).ToArray();
        foreach (var (n, f) in p0Boards)
            foreach (var w in waves)
            {
                var parts = new Agg[seeds];
                Parallel.For(0, seeds, s => parts[s] = One(f, w.Make, s));
                var a = new Agg(); foreach (var x in parts) a.Add(x);
                (double L, double C) Sim(Func<Agg, (long Layers, long Turns, long Capped)> sim)
                {
                    long l = 0, t = 0, c = 0;
                    foreach (var x in parts) { var (ll, tt, cc) = sim(x); l += ll; t += tt; c += cc; }
                    return ((double)l / seeds, t == 0 ? 0 : 100.0 * c / t);
                }
                (long, long, long) Now(Agg x)
                {
                    long pool = 0, l = 0, t = 0, c = 0;
                    for (int i = 0; i <= Math.Min(20, x.Turns); i++)
                    {
                        pool += x.OverByTurn[i];
                        long k = pool / 20; if (k <= 0) continue;
                        long d = Math.Min(2, k); l += d; t++; if (k > 2) c++; pool -= d * 20;
                    }
                    return (l, t, c);
                }
                Func<Agg, (long, long, long)> Turn(int cap) => x =>
                {
                    long pool = 0, l = 0, t = 0, c = 0;
                    for (int i = 0; i <= Math.Min(20, x.Turns); i++)
                    {
                        long k = pool / 20;
                        if (k > 0) { long d = Math.Min(cap, k); l += d; t++; if (k > cap) c++; pool -= d * 20; }
                        pool += x.OverByTurn[i];
                    }
                    return (l, t, c);
                };
                var ni = Sim(Now); var t3 = Sim(Turn(3)); var t8 = Sim(Turn(8));
                Console.WriteLine($"| {n} | {w.Name} | {a.P(a.Overflow):F1} | {(a.Turns == 0 ? 0 : (double)a.Overflow / a.Turns):F1} | {ni.L:F2}（{ni.C:F0}%）| {t3.L:F2}（{t3.C:F0}%）| {t8.L:F2}（{t8.C:F0}%）|");
            }
        Console.WriteLine();

        // 2. 倒れる一撃
        Console.WriteLine("## 2. 倒れる一撃（ヒサ以外の味方・敵の攻撃・段0 の後の規定）");
        Console.WriteLine();
        Console.WriteLine("数えるのは HP を引く直前の量（破片・軛・猶予の後）で HP 以上になった敵の攻撃の一撃。ヒサが生きていた ＝ その時点で矢面の保持者が生きていた（庇いの発火の見込み）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 1戦あたり | 出た戦 | ヒサが生きていた | 味方が初めて倒れたT |");
        Console.WriteLine("|---|---|--:|--:|--:|---|");
        foreach (var (n, f) in Boards())
            foreach (var w in waves)
            {
                var a = Many(f, w.Make, seeds);
                Console.WriteLine($"| {n} | {w.Name} | {a.P(a.Lethal):F2} | {Pct(a.LethalBattles, a.N)} | {Pct(a.LethalHisa, a.Lethal)} | {FirstDeath(a)} |");
            }
        Console.WriteLine();

        // 3. ソラの剥がし
        Console.WriteLine("## 3. ソラが剥がしていた標の内訳（第300期の規定・段0-1 の前）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 矢面の標 | それ以外 | 剥がした中の矢面 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (var (n, f) in Boards().Where(b => b.F.Occupied().Any(o => o.Def.Id == "sora")))
            foreach (var w in waves)
            {
                var a = Many(St(f, 0), w.Make, seeds);
                Console.WriteLine($"| {n} | {w.Name} | {a.P(a.StripBeckon):F2} | {a.P(a.StripOther):F2} | {Pct(a.StripBeckon, a.StripBeckon + a.StripOther)} |");
            }
        Console.WriteLine();

        // 4. 同士討ちが標の付いた味方に当たる
        Console.WriteLine("## 4. 味方による同士討ちが標の付いた味方に当たった回数（段0-3 の「あいつがやった！」の出番・段0 の後の規定）");
        Console.WriteLine();
        Console.WriteLine("数えるのは「あいつがやった！」で敵に標を付けた数（徴収 ／ 中継 ／ 本人の一撃・反撃の連鎖の中を除く・指差す敵がいれば必ず付く）。第300期の規定（ザンの札の中の指差し）の数を並べる。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | ザンがいる | ザンがいない | 第300期の指差し |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (var (n, f) in Boards())
            foreach (var w in waves)
            {
                var a = Many(f, w.Make, seeds); var o = Many(St(f, 0), w.Make, seeds);
                Console.WriteLine($"| {n} | {w.Name} | {a.P(a.AccuseZan):F2} | {a.P(a.AccuseSolo):F2} | {o.P(o.FrameAccuses):F2} |");
            }
        Console.WriteLine();

        // 5. 層の膨張
        Console.WriteLine("## 5. 層の膨張（段0 の後の規定・号令なし）");
        Console.WriteLine();
        Console.WriteLine("層の最大 ＝ 敵ごとの標の層の最大（1戦の最大の平均 ／ 全戦の最大）。在庫 ＝ ミサの羽の最大（同）。一斉射撃 ＝ ミサの手番の羽の枚数の平均。仇巡り ＝ 1手番の太刀の平均（上限 8 に張り付いた割合）。見切り ＝ 標の敵の一撃のうち層 3 以上（上限 45%）の割合。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 層の最大 | ミサの在庫 | 一斉射撃 | 仇巡り（張り付き）| 見切りの上限 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|");
        foreach (var (n, f) in Boards())
            foreach (var w in waves)
            {
                var a = Many(f, w.Make, seeds);
                Console.WriteLine($"| {n} | {w.Name} | {a.P(a.PeakSum):F1} ／ {a.PeakMax} | {a.P(a.FeatherMaxSum):F1} ／ {a.FeatherMax} | {Avg(a.VolleyShots, a.Volleys)} | {Avg(a.RoundSlashes, a.RoundTurns)}（{Pct(a.RoundCapped, a.RoundTurns)}）| {Pct(a.PressureCap, a.PressureHits)} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // §2-4 段0 の参考の数字
    // ---------------------------------------------------------------------------------
    static void Stage(int seeds)
    {
        var waves = MainWaves();
        var boards = StageBoards();
        var res = new Dictionary<(string, int, string), Agg>();
        foreach (var (n, f) in boards) for (int k = 0; k < StageNames.Length; k++) foreach (var w in waves) res[(n, k, w.Key)] = Many(St(f, k), w.Make, seeds);

        Console.WriteLine($"# 第301期 段0 の参考の数字（seed 0..{seeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("段: " + string.Join(" → ", StageNames.Select((s, i) => $"S{i} {s}")) + "。括弧は倒しT（勝った戦の決着T）。");
        Console.WriteLine();
        Console.WriteLine("## 勝率");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | S0 | S1 | S2 | S3 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (n, _) in boards)
            foreach (var w in waves)
                Console.WriteLine($"| {n} | {w.Name} | " + string.Join(" | ", Enumerable.Range(0, 4).Select(k => Cell(res[(n, k, w.Key)]))) + " |");
        Console.WriteLine();

        Console.WriteLine("## 段0-1: 矢面の標が敵の一撃の時点で残っていた割合（ソラのいる台）");
        Console.WriteLine();
        Console.WriteLine("分母 ＝ ヒサの矢面の記憶が指す味方が敵の攻撃を受けた回数（標が残っていた ＋ 剥がされていた）。ボス・勇者（規定形）の行がボスの一撃。剥がした ／ 残した ＝ ソラの逸らしが矢面の標を剥がした ／ 剥がさずに残した回数（1戦あたり）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 残っていた S0 → S1 | 敵の一撃（矢面）S0 → S1 | 剥がした S0 → S1 | 残した S1 | 剥がしたそれ以外 S0 → S1 |");
        Console.WriteLine("|---|---|---|---|---|--:|---|");
        foreach (var (n, _) in boards.Where(b => b.F.Occupied().Any(o => o.Def.Id == "sora")))
            foreach (var w in waves)
            {
                var a = res[(n, 0, w.Key)]; var b = res[(n, 1, w.Key)];
                Console.WriteLine($"| {n} | {w.Name} | {Pct(a.GuardHit, a.GuardHit + a.Stripped)} → {Pct(b.GuardHit, b.GuardHit + b.Stripped)} | {a.P(a.GuardHit + a.Stripped):F2} → {b.P(b.GuardHit + b.Stripped):F2} | {a.P(a.StripBeckon):F2} → {b.P(b.StripBeckon):F2} | {b.P(b.KeptBeckon):F2} | {a.P(a.StripOther):F2} → {b.P(b.StripOther):F2} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 段0-2: 初回の仇討ち（標の無い仇への仇討ち）で叫びが出た数（ザンのいる台）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 初回の仇討ち S1 → S2 | うち叫びが出た S1 → S2 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (n, _) in boards.Where(b => b.F.Occupied().Any(o => o.Def.Id == "zan")))
            foreach (var w in waves)
            {
                var a = res[(n, 1, w.Key)]; var b = res[(n, 2, w.Key)];
                Console.WriteLine($"| {n} | {w.Name} | {a.P(a.VendFresh):F2} → {b.P(b.VendFresh):F2} | {a.P(a.VendFreshShouts):F2}（{Pct(a.VendFreshShouts, a.VendFresh)}）→ {b.P(b.VendFreshShouts):F2}（{Pct(b.VendFreshShouts, b.VendFresh)}）|");
            }
        Console.WriteLine();

        Console.WriteLine("## 段0-3: ヒサの「あいつがやった！」（敵に標を付けた数・ザンがいる ／ いない）");
        Console.WriteLine();
        Console.WriteLine("S2 はザンの札の中の指差し（標を付けない・ザンがいなければ出ない）。S3 はヒサの札（標を付ける）。濡れ衣 ＝ ザンの濡れ衣の仇討ち。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 指差し S2 | 指差し S3（ザンがいる ／ いない）| 濡れ衣 S2 → S3 | 味方への標撃ち（ミサ）S2 → S3 |");
        Console.WriteLine("|---|---|--:|---|---|---|");
        foreach (var (n, _) in boards)
            foreach (var w in waves)
            {
                var a = res[(n, 2, w.Key)]; var b = res[(n, 3, w.Key)];
                Console.WriteLine($"| {n} | {w.Name} | {a.P(a.FrameAccuses):F2} | {b.P(b.AccuseZan):F2} ／ {b.P(b.AccuseSolo):F2} | {a.P(a.FrameVend):F2} → {b.P(b.FrameVend):F2} | {a.P(a.MfAlly):F2} → {b.P(b.MfAlly):F2} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // §5 段1 の版
    // ---------------------------------------------------------------------------------
    static double Score(Dictionary<(string, string, string), Agg> res, string k, (string Name, Formation F)[] boards, Doha297Diag.Wave[] waves)
        => boards.SelectMany(b => waves.Select(w => res[(b.Name, k, w.Key)].Win)).Average();

    /// <summary>K0〜K5 を代表台 × 波で回す（K5 は K1〜K3 の得点で決める）。</summary>
    static (Dictionary<(string, string, string), Agg> Res, Ver[] Vers, string Best) RunVersions(int seeds, Doha297Diag.Wave[] waves)
    {
        var boards = Boards();
        var res = new Dictionary<(string, string, string), Agg>();
        foreach (var v in Base) foreach (var (n, f) in boards) foreach (var w in waves) res[(n, v.Key, w.Key)] = Many(With(f, v.D), w.Make, seeds);
        var main = MainWaves();
        string best = new[] { "K1", "K2", "K3" }.OrderByDescending(k => Score(res, k, boards, main)).First();
        var k5 = K5Of(best);
        foreach (var (n, f) in boards) foreach (var w in waves) res[(n, "K5", w.Key)] = Many(With(f, k5.D), w.Make, seeds);
        return (res, Base.Append(k5).ToArray(), best);
    }

    static void Versions(int seeds)
    {
        var waves = Enumerable.Range(1, EnemyCatalog.Stages.Count).Select(i => WaveOf(i.ToString())).Concat(new[] { "guard", "bat", "boss" }.Select(WaveOf)).ToArray();
        var main = MainWaves();
        var boards = Boards();
        var (res, vers, best) = RunVersions(seeds, waves);

        Console.WriteLine($"# 第301期 段1 —— ヒサの版（seed 0..{seeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("組: " + string.Join(" ／ ", vers.Select(v => $"{v.Key} {v.Name}")) + "。");
        Console.WriteLine();
        Console.WriteLine("## 得点（代表台 7 × 第2〜5波 ＋ 近衛 ＋ 大隊 ＋ ボスの勝率の平均・第298期と同じ作り）");
        Console.WriteLine();
        Console.WriteLine("| 組 | 得点 | 精鋭の平均 | ボス |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var v in vers)
        {
            double el = boards.SelectMany(b => new[] { "guard", "bat" }.Select(k => res[(b.Name, v.Key, k)].Win)).Average();
            double bo = boards.Average(b => res[(b.Name, v.Key, "boss")].Win);
            Console.WriteLine($"| {v.Key} {v.Name} | {Score(res, v.Key, boards, main):F2} | {el:F1} | {bo:F1} |");
        }
        Console.WriteLine();
        Console.WriteLine($"K5 の号令 ＝ K1〜K3 で得点がいちばん高い **{best}**（{vers.First(v => v.Key == best).Name}）。");
        Console.WriteLine();

        Console.WriteLine("## 勝率（括弧は倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 組 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", waves.Length)));
        foreach (var (n, _) in boards)
            foreach (var v in vers)
            {
                var b0 = waves.Select(w => res[(n, "K0", w.Key)]).ToArray();
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", waves.Select((w, i) =>
                {
                    var a = res[(n, v.Key, w.Key)];
                    string c = Cell(a);
                    return v.Key != "K0" && Math.Abs(a.Win - b0[i].Win) >= 10 ? $"**{c}**" : c;
                })) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 共通（第2〜5波 ＋ 近衛 ＋ 大隊 ＋ ボスの和・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("味方が初めて倒れたT ／ ヒサが倒れたT の括弧は倒れた戦の割合。羽 ＝ ミサの羽の総数（手番の羽 ＋ 乱射 ＋ 標撃ち）・一斉射撃 ＝ ミサの手番の羽の枚数の平均。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 組 | 決着T | 味方が初めて倒れたT | ヒサが倒れたT | 羽 | 一斉射撃 | 仇巡りの太刀 |");
        Console.WriteLine("|---|---|--:|---|---|--:|--:|--:|");
        Agg Sum(string n, string k) { var a = new Agg(); foreach (var w in main) a.Add(res[(n, k, w.Key)]); return a; }
        foreach (var (n, _) in boards)
            foreach (var v in vers)
            {
                var a = Sum(n, v.Key);
                Console.WriteLine($"| {n} | {v.Key} | {a.P(a.Turns):F2} | {FirstDeath(a)} | {HisaDeath(a)} | {a.P(a.Feathers):F1} | {Avg(a.VolleyShots, a.Volleys)} | {a.P(a.RoundSlashes):F2} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 号令（K1〜K3 ／ K5・波ごと）");
        Console.WriteLine();
        Console.WriteLine("層 ＝ 号令で刻んだ層（1戦）・1回 ＝ 1回の号令の層・張り付き ＝ 上限で止まったターン ÷ 号令が出たターン（HL-i は 1ターン 2 層で溢れが残った・HL-t は 1手番の上限で溢れが残った）。羽 ＝ 号令の層が呼んだミサの羽。層の最大 ＝ 敵の層の最大（1戦の最大の平均 ／ 全戦の最大）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 組 | 溢れ | 層 | 1回 | 張り付き | 羽 | 層の最大 | ミサの在庫 | 仇巡り（張り付き）| 見切りの上限 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|---|---|--:|");
        foreach (var (n, _) in boards)
            foreach (var w in main)
                foreach (var v in vers.Where(v => v.Key is "K0" or "K1" or "K2" or "K3" || (v.Key == "K5")))
                {
                    var a = res[(n, v.Key, w.Key)];
                    Console.WriteLine($"| {n} | {w.Name} | {v.Key} | {a.P(a.Overflow):F1} | {a.P(a.CmdLayers):F2} | {Avg(a.CmdLayers, a.CmdFires)} | {Pct(a.CmdCapped, a.CmdNowTurns > 0 ? a.CmdNowTurns : a.CmdFires)} | {a.P(a.CmdFeathers):F2} | {a.P(a.PeakSum):F1} ／ {a.PeakMax} | {a.P(a.FeatherMaxSum):F1} ／ {a.FeatherMax} | {Avg(a.RoundSlashes, a.RoundTurns)}（{Pct(a.RoundCapped, a.RoundTurns)}）| {Pct(a.PressureCap, a.PressureHits)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 庇い（K4 ／ K5・波ごと）");
        Console.WriteLine();
        Console.WriteLine("発火 ＝ 1戦あたり（出た戦の割合）・倒れた ＝ 庇った一撃でヒサが倒れた割合・生き延びた ＝ 庇われた味方がその戦の終わりまで生きていた割合・量 ＝ 元の一撃の平均・粛 ／ 痺れ ＝ 庇えなかった倒れる一撃（1戦あたり）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 組 | 勝率 K0 → | 発火 | 倒れた | 生き延びた | 量 | 粛 ／ 痺れ | ヒサが倒れたT K0 → |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|---|---|");
        foreach (var (n, _) in boards)
            foreach (var w in main)
                foreach (var v in vers.Where(v => v.Key is "K4" or "K5"))
                {
                    var a = res[(n, v.Key, w.Key)]; var z = res[(n, "K0", w.Key)];
                    Console.WriteLine($"| {n} | {w.Name} | {v.Key} | {z.Win:F1} → {a.Win:F1} | {a.P(a.CoverFires):F2}（{Pct(a.CoverBattles, a.N)}）| {Pct(a.CoverDied, a.CoverFires)} | {Pct(a.CoverSurvived, a.CoverFires)} | {Avg(a.CoverRaw, a.CoverFires)} | {a.P(a.CoverHushed):F2} ／ {a.P(a.CoverBlocked):F2} | {HisaDeath(z)} → {HisaDeath(a)} |");
                }
    }

    // ---------------------------------------------------------------------------------
    // §5-3 手番版の「4段」
    // ---------------------------------------------------------------------------------
    /// <summary>1戦の台本から「ヒサの号令 → 羽の割り込み → ミサの一斉射撃 → ザンの仇巡り」が同じターンにこの順で揃ったターンの数を数える。</summary>
    internal static int FourCount(BattleResult r, List<UnitState> p, out int commandTurns)
    {
        var hisa = p.FirstOrDefault(u => u.Def.Id == "hisa"); var misa = p.FirstOrDefault(u => u.Def.Id == "tome"); var zan = p.FirstOrDefault(u => u.Def.Id == "zan");
        commandTurns = 0;
        if (hisa is null || misa is null) return 0;
        int n = 0;
        foreach (var g in r.Events.Select((x, i) => (x, i)).GroupBy(t => t.x.Turn))
        {
            var ev = g.ToList();
            int c = ev.FindIndex(t => t.x.Kind == BattleEventKind.Command && t.x.ActorId == hisa.InstanceId);
            if (c < 0) continue;
            commandTurns++;
            int f = ev.FindIndex(c + 1, t => t.x.Kind == BattleEventKind.FeatherMark && t.x.ActorId == misa.InstanceId && t.x.Text == FeatherMarkLabels.Foe);
            if (f < 0) continue;
            int v = ev.FindIndex(f + 1, t => t.x.Kind == BattleEventKind.Feather && t.x.Text == FeatherLabels.Volley && t.x.ActorId == misa.InstanceId);
            if (v < 0) continue;
            if (zan is null) continue;
            int z = ev.FindIndex(v + 1, t => t.x.Kind == BattleEventKind.VendettaRound && t.x.Text == VendettaRoundLabels.Start && t.x.ActorId == zan.InstanceId);
            if (z >= 0) n++;
        }
        return n;
    }

    static void Four(int seeds)
    {
        var waves = MainWaves();
        Console.WriteLine($"# 第301期 §5-3 手番版の「4段」（seed 0..{seeds - 1}・verbose の台本で数える）");
        Console.WriteLine();
        Console.WriteLine("1ターンの中で「ヒサの号令（`Command`）→ その後にミサの羽の割り込み（`FeatherMark`「敵」）→ その後にミサの一斉射撃（`Feather`「連射」）→ その後にザンの仇巡り（`VendettaRound`「仇巡り」）」の順で揃ったターン。");
        Console.WriteLine("号令T ＝ 号令が出たターン（1戦あたり）・4段 ＝ 揃ったターン（1戦あたり）／ 揃った戦の割合。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 組 | 号令T | 4段 | 揃った戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|");
        var vers = new[] { Base[1], Base[2], Base[3] };
        foreach (var (n, f) in Boards().Where(b => b.F.Occupied().Any(o => o.Def.Id == "tome")))
            foreach (var w in waves)
                foreach (var v in vers)
                {
                    var four = new int[seeds]; var ct = new int[seeds];
                    Parallel.For(0, seeds, s =>
                    {
                        var p = BattleEngine.Materialize(With(f, v.D), BattleContext.PlayerTeam);
                        var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                        four[s] = FourCount(r, p, out ct[s]);
                    });
                    Console.WriteLine($"| {n} | {w.Name} | {v.Key} {v.Name} | {ct.Average():F2} | {four.Average():F2} | {100.0 * four.Count(x => x > 0) / seeds:F0}% |");
                }
    }

    // ---------------------------------------------------------------------------------
    // §5-2 compare 64 行 × 組
    // ---------------------------------------------------------------------------------
    static double[] Rates(Formation f, int seeds)
    {
        var w = new double[EnemyCatalog.Stages.Count];
        for (int i = 0; i < w.Length; i++)
        {
            int ii = i; int k = 0;
            var wins = new bool[seeds];
            Parallel.For(0, seeds, s => wins[s] = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.Materialize(EnemyCatalog.Stages[ii].Enemy, BattleContext.EnemyTeam), s, verbose: false).PlayerWon);
            k = wins.Count(x => x);
            w[i] = 100.0 * k / seeds;
        }
        return w;
    }

    static void Cmp(int seeds)
    {
        var rows = CompareBuilds();
        var withHisa = rows.Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hisa))).ToArray();
        var main = MainWaves();
        // K5 の号令は代表台で決める（`ver` と同じ）——ここでは得点だけ取り直す。
        var (res, vers, best) = RunVersions(Math.Min(seeds, 200), main);
        Console.WriteLine($"# 第301期 §5-2 `compare` 64 行 × 組（ヒサ在席の行・seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"ヒサのいない {rows.Length - withHisa.Length} 行は組で動かない（ヒサの札しか替えない）。K5 ＝ {K5Of(best).Name}。−10.0pt 以上落ちたセルは太字。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 組 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        var all = new Dictionary<(string, string), double[]>();
        foreach (var v in vers)
            foreach (var (n, f) in rows)
                all[(n, v.Key)] = withHisa.Any(h => h.Name == n) || v.Key == "K0" ? Rates(With(f, v.D), seeds) : null!;
        foreach (var (n, _) in withHisa)
            foreach (var v in vers)
            {
                var a = all[(n, v.Key)]; var z = all[(n, "K0")];
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", a.Select((x, i) => v.Key != "K0" && x - z[i] <= -10.0 ? $"**{x:F1}**" : $"{x:F1}")) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("## (G2)（−10.0pt 以上落ちた行の駒ごとの「その駒を含む他の行」の全波平均の変化）");
        Console.WriteLine();
        bool any = false;
        foreach (var v in vers.Where(v => v.Key != "K0"))
            foreach (var (n, f) in withHisa)
            {
                var a = all[(n, v.Key)]; var z = all[(n, "K0")];
                if (!a.Select((x, i) => x - z[i]).Any(d => d <= -10.0)) continue;
                any = true;
                var parts = new List<string>();
                foreach (var d in f.Occupied().Select(o => o.Def).Distinct())
                {
                    var others = rows.Where(r => r.Name != n && r.F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToArray();
                    if (others.Length == 0) { parts.Add($"{Short(d)}: 他の行 0（分解が成立しない）"); continue; }
                    double delta = others.Average(o => (all[(o.Name, v.Key)] ?? all[(o.Name, "K0")]).Average() - all[(o.Name, "K0")].Average());
                    parts.Add($"{Short(d)}: {delta:+0.0;-0.0;0.0}pt（{others.Length} 行）");
                }
                Console.WriteLine($"- {v.Key} {n}: " + string.Join(" ／ ", parts));
            }
        if (!any) Console.WriteLine("−10.0pt 以上落ちた行は無い（(G2) の分解の対象なし）。");
    }

    // ---------------------------------------------------------------------------------
    // 台本の例
    // ---------------------------------------------------------------------------------
    sealed record Show(string Label, string Row, string Wave, UnitDef Hisa, Func<BattleEvent, List<UnitState>, bool> Hit);

    static Show[] Shows() => new Show[]
    {
        new("ヒサ単独の「あいつがやった！」（`Framed`「あいつがやった」・ザンなし）", NoZanName, "guard", UnitCatalog.Hisa, (x, _) => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Accuse),
        new("あいつがやった！ → 濡れ衣（ザンあり）", "試遊・標 循環", "guard", UnitCatalog.Hisa, (x, _) => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Vendetta),
        new("号令・即時（`Command`「即時」・HL-i）", "試遊・標 循環", "boss", UnitCatalog.HisaHLi, (x, _) => x.Kind == BattleEventKind.Command && x.Text == CommandLabels.Now),
        new("号令・手番（`Command`「手番」・HL-t3）", "試遊・標 循環", "boss", UnitCatalog.HisaHLt3, (x, _) => x.Kind == BattleEventKind.Command && x.Text == CommandLabels.Turn),
        new("号令・手番（`Command`「手番」・HL-t8）", "試遊・標 循環", "boss", UnitCatalog.HisaHLt8, (x, _) => x.Kind == BattleEventKind.Command && x.Text == CommandLabels.Turn),
        new("庇い（`Cover`・HC）", "試遊・標 三人組", "bat", UnitCatalog.HisaHC, (x, _) => x.Kind == BattleEventKind.Cover),
    };

    static Formation RowOf(string n) => n == NoZanName ? NoZan : n.StartsWith("試遊", StringComparison.Ordinal) ? Playtest(n) : CompareRow(n);

    static void Find(int seeds)
    {
        Console.WriteLine($"# 第301期 台本の例（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 見せ場 | 台 × 波 | 1戦あたりの件数 | 出た戦 | seed 0 の件数（最初の T） | 最初に出る seed（最初の T） |");
        Console.WriteLine("|---|---|--:|--:|---|---|");
        foreach (var s in Shows())
        {
            var f = With(RowOf(s.Row), s.Hisa); var w = WaveOf(s.Wave);
            var cnt = new int[seeds]; var first = new int[seeds];
            Parallel.For(0, seeds, sd =>
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, w.Make(), sd, verbose: true);
                var hits = r.Events.Where(x => s.Hit(x, p)).ToList();
                cnt[sd] = hits.Count; first[sd] = hits.Count > 0 ? hits[0].Turn : -1;
            });
            int fs = Array.FindIndex(cnt, c => c > 0);
            Console.WriteLine($"| {s.Label} | {s.Row} × {w.Name} | {cnt.Average():F2} | {cnt.Count(c => c > 0)} / {seeds} | {cnt[0]}{(cnt[0] > 0 ? $"（T{first[0]}）" : "")} | {(fs < 0 ? "—" : $"seed {fs}（T{first[fs]}）")} |");
        }
        // 4段が揃う最初の seed（HL-t3 ／ HL-t8・循環 × ボス）
        foreach (var v in new[] { Base[2], Base[3] })
        {
            var f = With(Playtest("試遊・標 循環"), v.D); var w = WaveOf("boss");
            for (int sd = 0; sd < seeds; sd++)
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, w.Make(), sd, verbose: true);
                if (FourCount(r, p, out _) > 0) { Console.WriteLine(); Console.WriteLine($"4段（{v.Name}・試遊・標 循環 × ボス）が最初に揃う seed: {sd}"); break; }
            }
        }
    }

    static void Memo(string rowPart, string wave, int seed, string ver, int lastTurn)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(rowPart, StringComparison.Ordinal));
        var v = ver == "K5" ? K5Of("K2") : Base.First(b => b.Key == ver);
        var f = With(f0, v.D);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# hisa301 memo —— {name} × {w.Name} × seed {seed} × {v.Key} {v.Name} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Turn > lastTurn) break;
            if (x.Kind is not (BattleEventKind.Damage or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.Feather or BattleEventKind.MarkRally
                or BattleEventKind.Death or BattleEventKind.Attack or BattleEventKind.VendettaRound or BattleEventKind.Command or BattleEventKind.Cover
                or BattleEventKind.MarkLayer or BattleEventKind.StatusGain or BattleEventKind.BeckonFeather)) continue;
            if (x.Kind == BattleEventKind.StatusGain && x.Text != StatusKeys.Marked) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.PartnerId is int pa ? $"  Partner={N(pa)}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
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
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit");
    static readonly MethodInfo RallyHealM = typeof(BattleContext).GetMethod("RallyHeal", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("RallyHeal");
    static readonly MethodInfo Drain = typeof(BattleContext).GetMethod("DrainFeatherMarksPublic", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("DrainFeatherMarksPublic");
    static readonly UnitDef HushDummy = new() { Id = "hushtest", Name = "粛の試験体", MaxHp = 99, Attack = 1, Speed = 1, Traits = new[] { TraitId.Hush } };
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();
    static UnitTally TalOf(BattleContext ctx, UnitState u)
    {
        var d = (Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!;
        if (!d.TryGetValue(u.Def.Id, out var t)) d[u.Def.Id] = t = new UnitTally();
        return t;
    }

    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnProp.SetValue(ctx, 1);
        return ctx;
    }

    static int Count(string path, string needle)
    {
        string s = File.ReadAllText(path);
        int c = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { c++; i += needle.Length; }
        return c;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# hisa301 check —— 第301期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        var sora = UnitCatalog.Sora; var zan = UnitCatalog.Zan; var hisa = UnitCatalog.Hisa;
        Expect("(a) 定義: ソラ ＝ `SoraSRs` ＋ `DivertKeepBeckon`・ザン ＝ `ZanZMa` ＋ `VendettaMarkFirst`・ヒサ ＝ `HisaHKf` ＋ `FrameAccuse`・旧はすべて `All` ／ `Everyone` の外・数値と手番は旧のまま",
            sora.Traits.SequenceEqual(UnitCatalog.SoraSRs.Traits.Append(TraitId.DivertKeepBeckon)) && sora.PlusText == UnitCatalog.SoraSRs.PlusText
            && zan.Traits.SequenceEqual(UnitCatalog.ZanZMa.Traits.Append(TraitId.VendettaMarkFirst)) && zan.MinusText == UnitCatalog.ZanZMa.MinusText && zan.MaxHp == 56 && zan.Attack == 10 && zan.Speed == 5
            && zan.PlusText.Contains("ヒサが指差した敵を斬る") && zan.PlusText.Contains("仇として指差し")
            && hisa.Traits.SequenceEqual(UnitCatalog.HisaHKf.Traits.Append(TraitId.FrameAccuse)) && hisa.PlusText.StartsWith(UnitCatalog.HisaHKf.PlusText, StringComparison.Ordinal)
            && hisa.PlusText.Contains("あいつがやった！") && hisa.MinusText == UnitCatalog.HisaHKf.MinusText && hisa.MaxHp == 44 && hisa.Speed == 10 && hisa.Actions!.SequenceEqual(UnitCatalog.HisaHKf.Actions!)
            && UnitCatalog.HisaHKf.Traits.SequenceEqual(UnitCatalog.HisaHKs.Traits.Append(TraitId.BeckonFeather))
            && new[] { sora, zan, hisa }.All(UnitCatalog.All.Contains) && !new[] { UnitCatalog.SoraSRs, UnitCatalog.ZanZMa, UnitCatalog.HisaHKf }.Any(UnitCatalog.Everyone.Contains));
        Expect("(b) 段1 の版は規定のヒサの末尾に札を足しただけ・`All` ／ `Everyone` の外・文面は末尾に足しただけ",
            UnitCatalog.HisaHLi.Traits.SequenceEqual(hisa.Traits.Append(TraitId.CommandNow)) && UnitCatalog.HisaHLt3.Traits.SequenceEqual(hisa.Traits.Append(TraitId.CommandTurn3))
            && UnitCatalog.HisaHLt8.Traits.SequenceEqual(hisa.Traits.Append(TraitId.CommandTurn8)) && UnitCatalog.HisaHC.Traits.SequenceEqual(hisa.Traits.Append(TraitId.HisaCover))
            && UnitCatalog.HisaHCi.Traits.SequenceEqual(hisa.Traits.Append(TraitId.CommandNow).Append(TraitId.HisaCover))
            && new[] { UnitCatalog.HisaHLi, UnitCatalog.HisaHLt3, UnitCatalog.HisaHLt8, UnitCatalog.HisaHC, UnitCatalog.HisaHCi, UnitCatalog.HisaHCt3, UnitCatalog.HisaHCt8 }.All(d => !UnitCatalog.Everyone.Contains(d) && d.PlusText.StartsWith(hisa.PlusText, StringComparison.Ordinal) && d.MaxHp == hisa.MaxHp)
            && CommandTrait.Every == 20 && CommandTrait.NowCap == 2);

        // (c) 段0-1: ソラは矢面の標を剥がさない（ほかの味方の標は剥がす）
        (bool Beckon, bool Other) Strip(UnitDef s)
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Golm, center: UnitCatalog.Tome, back1: s, back3: UnitCatalog.Hisa), Formation.Build(front1: UnitCatalog.Gald), out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa"); var dolga = p.First(u => u.Def.Id == UnitCatalog.Dolga.Id); var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id);
            var so = p.First(u => u.Def.Id == "sora");
            hi.SetCounter(BeckonTrait.TargetKey, dolga.InstanceId + 1);
            dolga.SetCounter(StatusKeys.Marked, 1); golm.SetCounter(StatusKeys.Marked, 1);
            TraitCatalog.Get(TraitId.Divert).OnTurnStart(ctx, so);
            return (dolga.RawCounter(StatusKeys.Marked) > 0, golm.RawCounter(StatusKeys.Marked) > 0);
        }
        var s0 = Strip(UnitCatalog.SoraSRs); var s1 = Strip(sora);
        Expect("(c) 段0-1: 規定のソラは矢面の標を剥がさない・ほかの味方の標は剥がす（旧 `SoraSRs` はどちらも剥がす）", !s0.Beckon && !s0.Other && s1.Beckon && !s1.Other, $"旧 矢面 {s0.Beckon} ／ ほか {s0.Other}・規定 矢面 {s1.Beckon} ／ ほか {s1.Other}");

        // (d) 段0-2: 初回の仇討ちで叫びが出る（旧 `ZanZMa` は出ない）
        (long Shouts, long Fresh, int Layer) FirstVend(UnitDef z)
        {
            var ctx = Ctx(Formation.Build(front1: z, front3: UnitCatalog.Golm, back3: UnitCatalog.Hisa), Formation.Build(front1: UnitCatalog.Gald), out var p, out var e);
            var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id); var zz = p.First(u => u.Def.Id == "zan");
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 900; }
            golm.SetCounter(StatusKeys.Marked, 1);
            ctx.ApplyDamage(golm, 10, e[0], pattern: AttackPattern.Single);
            return (Tal(ctx, "zan").VendettaFreshShouts, Tal(ctx, "zan").VendettaFresh, e[0].RawCounter(StatusKeys.Marked));
        }
        var v0 = FirstVend(UnitCatalog.ZanZMa); var v1 = FirstVend(zan);
        Expect("(d) 段0-2: 初回の仇討ち（標の無い敵）で叫びが出る（旧 `ZanZMa` は出ない）・どちらも仇に標が付く", v0.Fresh == 1 && v0.Shouts == 0 && v1.Fresh == 1 && v1.Shouts == 1 && v0.Layer > 0 && v1.Layer > 0,
            $"旧 初回 {v0.Fresh} ／ 叫び {v0.Shouts}・規定 初回 {v1.Fresh} ／ 叫び {v1.Shouts}");

        // (e) 段0-3: ザンがいなくてもヒサが敵に標を付ける・敵の攻撃では出ない・旧 `HisaHKf` は付けない
        (int Mark, long Solo) Accuse(UnitDef h, bool friendly)
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Borg, center: UnitCatalog.Dolga, back3: h), Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gald), out var p, out var e);
            var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id); var borg = p.First(u => u.Def.Id == UnitCatalog.Borg.Id);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 900; }
            golm.SetCounter(StatusKeys.Marked, 1);
            ctx.ApplyDamage(golm, 10, friendly ? borg : e[0], isFriendlyFire: friendly, pattern: AttackPattern.Single);
            return (e.Sum(x => x.RawCounter(StatusKeys.Marked)), Tal(ctx, "hisa").AccuseSolo);
        }
        var a0 = Accuse(UnitCatalog.HisaHKf, true); var a1 = Accuse(hisa, true); var a2 = Accuse(hisa, false);
        Expect("(e) 段0-3: ザンがいなくても、味方の攻撃で標の味方が撃たれたらヒサが敵に標を付ける・敵の攻撃では出ない・旧 `HisaHKf` は付けない", a0.Mark == 0 && a1.Mark == 1 && a1.Solo == 1 && a2.Mark == 0 && a2.Solo == 0,
            $"旧 {a0.Mark}・規定 {a1.Mark}（単独 {a1.Solo}）・敵の攻撃 {a2.Mark}");

        // (f) 号令: 溢れだけを数える（満タンで入らなかった量）
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Tome, back3: UnitCatalog.HisaHLt3), Formation.Build(front1: UnitCatalog.Gald), out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa"); var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id);
            golm.MaxHp = 100; golm.Hp = 95;
            var ht = TalOf(ctx, hi);
            RallyHealM.Invoke(ctx, new object[] { hi, golm, 12, golm, 2, ht, true });
            int pool1 = hi.RawCounter(CommandTrait.PoolKey);
            golm.Hp = 50;
            RallyHealM.Invoke(ctx, new object[] { hi, golm, 12, golm, 2, ht, true });
            Expect("(f) 号令: 溢れは満タンで入らなかった量だけ（95/100 に 12 → 7・50/100 に 12 → 0）", pool1 == 7 && hi.RawCounter(CommandTrait.PoolKey) == 7 && ht.RallyOverflow == 7, $"{pool1} → {hi.RawCounter(CommandTrait.PoolKey)}・帳簿 {ht.RallyOverflow}");
        }

        // (g) 号令（手番）: 20 ごとに1層・上限で止まる・端数の持ち越し・層1つにつき羽1発
        (int Layers, int Pool, long Feathers, long MfFoe) Turn(UnitDef h, int pool, bool withMisa = true)
        {
            var ctx = Ctx(withMisa ? Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Tome, back3: h) : Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Dolga, back3: h),
                Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gald), out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa");
            foreach (var u in p.Concat(e)) { u.MaxHp = 5000; u.Hp = 5000; }
            e[0].SetCounter(StatusKeys.Marked, 1);
            hi.SetCounter(CommandTrait.PoolKey, pool);
            ctx.CommandTurnFire(hi, CommandTrait.TurnCap(hi));
            int left = hi.RawCounter(CommandTrait.PoolKey);   // 羽を撃つ前に読む（羽の割り込みの叫びの溢れがまた溜まる）
            int layers = e.Sum(x => x.RawCounter(StatusKeys.Marked)) - 1;
            Drain.Invoke(ctx, null);
            return (layers, left, withMisa ? Tal(ctx, "tome").CommandFeathers : 0, withMisa ? Tal(ctx, "tome").MfShotsFoe : 0);
        }
        var t1 = Turn(UnitCatalog.HisaHLt3, 65); var t2 = Turn(UnitCatalog.HisaHLt3, 200); var t3 = Turn(UnitCatalog.HisaHLt8, 200); var t4 = Turn(UnitCatalog.HisaHLt8, 19);
        Expect("(g) 号令（手番）: 20 ごとに1層（65 → 3 層・端数 5 を持ち越す）・上限で止まる（HL-t3 200 → 3 層・140 残る ／ HL-t8 200 → 8 層・40 残る）・19 では刻まない・層1つにつき羽1発",
            t1.Layers == 3 && t1.Pool == 5 && t2.Layers == 3 && t2.Pool == 140 && t3.Layers == 8 && t3.Pool == 40 && t4.Layers == 0 && t4.Pool == 19
            && t1.Feathers == 3 && t1.MfFoe == 3 + 1 && t3.Feathers == 8 && t3.MfFoe == 8 + 1,
            $"層 {t1.Layers}／{t2.Layers}／{t3.Layers}／{t4.Layers}・残り {t1.Pool}／{t2.Pool}／{t3.Pool}／{t4.Pool}・号令の羽 {t1.Feathers}／{t3.Feathers}（撃った {t1.MfFoe}／{t3.MfFoe}——準備で付けた標の1発を含む）");

        // (h) 号令（即時）: 1ターンに 2 層まで（台本で数える）・号令の層は溢れ 20 ごと
        {
            long over = 0, viol = 0, cmds = 0, mism = 0;
            var f = With(Playtest("試遊・標 循環"), UnitCatalog.HisaHLi);
            foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf))
                for (int s = 0; s < 10; s++)
                {
                    var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    foreach (var g in r.Events.Where(x => x.Kind == BattleEventKind.Command).GroupBy(x => x.Turn))
                    {
                        cmds += g.Count();
                        if (g.Sum(x => x.Amount) > CommandTrait.NowCap) viol++;
                        foreach (var x in g) if (x.Slot != x.Amount * CommandTrait.Every) mism++;
                    }
                    over += r.TallyByUnit.TryGetValue("hisa", out var ht) ? ht.RallyOverflow : 0;
                }
            Expect("(h) 号令（即時）: 1ターンに 2 層まで・使った溢れ ＝ 層 × 20（循環 × 精鋭 ／ ボス × seed 0..9）", cmds > 0 && viol == 0 && mism == 0, $"号令 {cmds}・超え {viol}・量の外れ {mism}・溢れ {over}");
        }

        // (i) 庇い: 敵の攻撃だけ・1戦1度・ヒサの自分への標に羽が飛ばない・粛で止まる
        (bool Covered, int AllyHp, int HisaMark, long FeatherAlly, long Skipped, long Used) Cover(bool friendly, bool second, bool hush)
        {
            var en = hush ? Formation.Build(front1: UnitCatalog.Gald, back3: HushDummy) : Formation.Build(front1: UnitCatalog.Gald);
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Borg, center: UnitCatalog.Tome, back3: UnitCatalog.HisaHC), en, out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa"); var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id); var borg = p.First(u => u.Def.Id == UnitCatalog.Borg.Id);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            golm.Hp = 20;
            if (second) hi.SetCounter(CoverTrait.UsedKey, 1);
            ctx.ApplyDamage(golm, 40, friendly ? borg : e[0], isFriendlyFire: friendly, pattern: AttackPattern.Single);
            Drain.Invoke(ctx, null);
            var ht = Tal(ctx, "hisa");
            return (ht.CoverFires > 0, golm.Hp, hi.RawCounter(StatusKeys.Marked), Tal(ctx, "tome").MfShotsAlly, Tal(ctx, "hisa").CoverFeatherSkipped, hi.RawCounter(CoverTrait.UsedKey));
        }
        var c1 = Cover(false, false, false); var c2 = Cover(true, false, false); var c3 = Cover(false, true, false);
        Expect("(i) 庇い: 敵の倒れる一撃をヒサが受ける（味方は無傷・ヒサに標・ミサの羽は飛ばない）・同士討ちでは庇わない・2度目は庇わない",
            c1.Covered && c1.AllyHp == 20 && c1.HisaMark == 1 && c1.FeatherAlly == 0 && c1.Skipped == 1 && c1.Used == 1 && !c2.Covered && c2.AllyHp <= 0 && !c3.Covered && c3.AllyHp <= 0,
            $"敵 庇った {c1.Covered}（味方 HP {c1.AllyHp}・標 {c1.HisaMark}・羽 {c1.FeatherAlly}・止めた {c1.Skipped}）・同士討ち {c2.Covered}・2度目 {c3.Covered}");
        var c4 = Cover(false, false, true);
        Expect("(j) 庇い: 粛（保持者が生きている）で止まる", !c4.Covered && c4.AllyHp <= 0, $"庇った {c4.Covered}");
        {
            long maxPer = 0, battles = 0;
            foreach (var w in MainWaves()) for (int s = 0; s < 20; s++)
                {
                    var r = BattleEngine.Run(BattleEngine.Materialize(With(Playtest("試遊・標 三人組"), UnitCatalog.HisaHC), BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                    long c = r.TallyByUnit.TryGetValue("hisa", out var ht) ? ht.CoverFires : 0;
                    maxPer = Math.Max(maxPer, c); if (c > 0) battles++;
                }
            Expect("(k) 庇い: 1戦に1度（三人組 × 7 波 × seed 0..19）", maxPer == 1 && battles > 0, $"1戦の最大 {maxPer}・出た戦 {battles}");
        }

        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(l) `PickOne(` の出現数が第300期と同じ（BattleCore）", pick == 36, $"{pick}");

        // (m) verbose の有無で勝敗・決着T が同じ（K0〜K4 と K5 の3つ × 代表台 × 試遊の波 × seed 0..4）
        {
            int diff = 0, n2 = 0;
            var vs = Base.Select(b => b.D).Concat(new[] { UnitCatalog.HisaHCi, UnitCatalog.HisaHCt3, UnitCatalog.HisaHCt8 });
            foreach (var d in vs) foreach (var (_, f) in Boards()) foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf)) for (int s = 0; s < 5; s++)
                        {
                            var g = With(f, d);
                            var a = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                            var b = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                            n2++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                        }
            Expect("(m) verbose の有無で勝敗・決着T が同じ（K0〜K4 ＋ K5 の候補3つ × 代表台 7 × 試遊の波 × seed 0..4）", diff == 0, $"{n2} 戦・違い {diff}");
        }

        // (n) 出来事: 号令・庇い・ヒサ単独の指差しが台本に出る（規定 K0 では号令 ／ 庇いは 0）
        long Ev(Formation f, string wave, BattleEventKind k, string? text = null) { long x = 0; for (int s = 0; s < 10; s++) x += BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), WaveOf(wave).Make(), s, verbose: true).Events.Count(e => e.Kind == k && (text is null || e.Text == text)); return x; }
        var cyc = Playtest("試遊・標 循環");
        long cm0 = Ev(cyc, "boss", BattleEventKind.Command), cm1 = Ev(With(cyc, UnitCatalog.HisaHLt3), "boss", BattleEventKind.Command, CommandLabels.Turn), cm2 = Ev(With(cyc, UnitCatalog.HisaHLi), "boss", BattleEventKind.Command, CommandLabels.Now);
        long cv0 = Ev(Playtest("試遊・標 三人組"), "bat", BattleEventKind.Cover), cv1 = Ev(With(Playtest("試遊・標 三人組"), UnitCatalog.HisaHC), "bat", BattleEventKind.Cover);
        long ac0 = Ev(St(NoZan, 2), "guard", BattleEventKind.Framed, FramedLabels.Accuse), ac1 = Ev(NoZan, "guard", BattleEventKind.Framed, FramedLabels.Accuse);
        Expect("(n) 出来事: 規定に `Command` ／ `Cover` は 0・HL-t3 ／ HL-i ／ HC で出る・ザンのいない台の「あいつがやった」は 段0-3 の前 0 → 後 出る（seed 0..9）",
            cm0 == 0 && cm1 > 0 && cm2 > 0 && cv0 == 0 && cv1 > 0 && ac0 == 0 && ac1 > 0, $"号令 {cm0} ／ 手番 {cm1} ／ 即時 {cm2}・庇い {cv0} → {cv1}・単独の指差し {ac0} → {ac1}");

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
