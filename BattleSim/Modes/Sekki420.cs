using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// sekki420 —— 第420期 本体【軽い期・版だけ】人呑みのセッキの呑みを手番の軸へ（SW-n 一日遅れて吐き出す ／ SW-nd 消化する・対照に SW-a）。
// 指示書は design/PHASE420_SEKKI_DIGEST_SPEC.md ／ 報告は design/PHASE420_SEKKI_DIGEST.md。台は第419期（`sekki419`）と同じ 3 台。
//
//     dotnet run --project BattleSim -c Release 0 sekki420 boards   # §3-2: 台 3 つ × {規定, SW-a, SW-n, SW-nd} × 4 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 sekki420 mixed    # §3-3: `compare` のセッキを含む行 × {規定, SW-a, SW-n, SW-nd} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 sekki420 log <0|a|n|nd> <台の鍵 ／ row:行名の頭> <波の鍵> [seed]
//     dotnet run --project BattleSim -c Release 0 sekki420 check    # 自己検査
//
// **予測のファイルが無ければ boards ／ mixed を走らせない**（R397・`.tmp/p420/predict.md`）。
// =====================================================================================
static class Sekki420Diag
{
    const string PredictFile = ".tmp/p420/predict.md";
    const int Seeds = 100;

    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Sekki), ("a", "SW-a", UnitCatalog.SekkiSWa), ("n", "SW-n", UnitCatalog.SekkiSWn), ("nd", "SW-nd", UnitCatalog.SekkiSWnd),
    };

    /// <summary>台（第419期と同じ）。鍵 ／ 名前 ／ 並び（セッキを差す）。</summary>
    static readonly (string Key, string Name, Func<UnitDef, Formation> F)[] Boards =
    {
        ("wrap", "試遊・手番 包み", s => Presets.SlimeLamp396(s)),
        ("core4", "試遊・手番 芯4枚", s => Presets.FourCore397(sekki: s)),
        ("curse", "試遊・呪い（クビ → セッキ）", s => Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Mudo, center: UnitCatalog.Nel, back1: s, back3: UnitCatalog.Utsu)),
    };
    static readonly (string Key, I.W W)[] WaveKeys = { ("boss", I.Boss), ("guard", I.Guard), ("bat", I.Bat), ("s4", I.StrongWaves[2]) };

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");

    static Formation Swap(Formation f, UnitDef sekki)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "sekki") g[o.Slot] = sekki;
        return g;
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class Fx
    {
        public long N, Wins, WinT, Turns, Fell, FirstFellT, FirstFellN;
        public long WrapHits, Bursts, RallyAmt;
        public long Gulps, GulpAmt, GulpSpent, BlockTurns, GulpedLaterFell, GulpedUnits;
        public long Lost419, Lost420, RallyRead, LampRead, Rest;           // 失った手番（第419期の数え ／ ゼリーの中）・号令 ／ 灯が読んだ・休み番のドルガを呑んだ
        public long HeldSpits, DigestSpits, DigestHeal, SelfTurns, SelfDigest, SelfHeal, HeldAtEnd;
        public readonly Dictionary<string, long> Death = new(), DeathT = new(), GulpWho = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Turns += o.Turns; Fell += o.Fell; FirstFellT += o.FirstFellT; FirstFellN += o.FirstFellN;
            WrapHits += o.WrapHits; Bursts += o.Bursts; RallyAmt += o.RallyAmt;
            Gulps += o.Gulps; GulpAmt += o.GulpAmt; GulpSpent += o.GulpSpent; BlockTurns += o.BlockTurns; GulpedLaterFell += o.GulpedLaterFell; GulpedUnits += o.GulpedUnits;
            Lost419 += o.Lost419; Lost420 += o.Lost420; RallyRead += o.RallyRead; LampRead += o.LampRead; Rest += o.Rest;
            HeldSpits += o.HeldSpits; DigestSpits += o.DigestSpits; DigestHeal += o.DigestHeal; SelfTurns += o.SelfTurns; SelfDigest += o.SelfDigest; SelfHeal += o.SelfHeal; HeldAtEnd += o.HeldAtEnd;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.GulpWho) GulpWho[k] = GulpWho.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => $"{F1(Win)}・{(Wins == 0 ? "—" : ((double)WinT / Wins).ToString("0.0"))}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
        public static string Who(Dictionary<string, long> d, long n) => d.Count == 0 ? "—" : string.Join("、", d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} {Per(kv.Value, n)}"));
        /// <summary>号令 ／ 灯 ／ 休みが呑まれた仲間を読んだ回数（第419期の数え ＋ 第420期の数え）。</summary>
        public long Reads => RallyRead + LampRead;
        public long LostAll => Lost419 + Lost420;
    }

    static Fx One(Formation f, I.W w, int seed, bool verbose = false)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: verbose);
        var a = new Fx { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var fell = p.Where(u => u.LastDeathTurn > 0).ToList();
        a.Fell = fell.Count;
        if (fell.Count > 0) { a.FirstFellN = 1; a.FirstFellT = fell.Min(u => u.LastDeathTurn); }
        foreach (var u in fell) { string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        a.RallyAmt = r.WhetByRoute[(int)WhetRoute.RallyTurn];
        foreach (var u in p)
        {
            if (!r.TallyByUnit.TryGetValue(u.Def.Id, out var t)) continue;
            a.Lost419 += t.Sw419GulpedLost; a.Lost420 += t.Sw420Lost;
            a.RallyRead += t.Sw419RallyRead + t.Sw420RallyRead; a.LampRead += t.Sw419LampRead + t.Sw420LampRead;
            if (t.Sw419Gulped > 0)
            {
                string k = Short(u.Def); a.GulpWho[k] = a.GulpWho.GetValueOrDefault(k) + t.Sw419Gulped; a.GulpedUnits++;
                if (u.LastDeathTurn > 0) a.GulpedLaterFell++;
            }
        }
        if (r.TallyByUnit.TryGetValue("sekki", out var s0))
        {
            a.WrapHits = s0.R396WrapHits; a.Bursts = s0.R396Bursts;
            a.Gulps = s0.Sw419Gulps; a.GulpAmt = s0.Sw419GulpAmt; a.GulpSpent = s0.Sw419GulpSpent; a.BlockTurns = s0.Sw419WrapBlockedTurns; a.Rest = s0.Sw419GulpRest;
            a.HeldSpits = s0.Sw420HeldSpits; a.DigestSpits = s0.Sw420DigestSpits; a.DigestHeal = s0.Sw420DigestHeal;
            a.SelfTurns = s0.Sw420SelfDigestTurns; a.SelfDigest = s0.Sw420SelfDigest; a.SelfHeal = s0.Sw420SelfHeal; a.HeldAtEnd = s0.Sw420HeldAtEnd;
        }
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n = Seeds, int from = 0, bool verbose = false)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, from + s, verbose));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sekki420: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "boards": BoardsMode(); break;
            case "mixed": Mixed(); break;
            case "log": Log(A(3, "n"), A(4, "core4"), A(5, "guard"), int.Parse(A(6, "0"))); return;
            case "check": Check(); break;
            default: Console.WriteLine("sekki420: モードは boards / mixed / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 台 × 版 × 4 波
    // ---------------------------------------------------------------------------------
    static void BoardsMode()
    {
        Console.WriteLine($"# 第420期 §4 —— 台 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", WaveKeys.Select(w => w.W.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        var res = new Dictionary<(string, string, string), Fx>();
        foreach (var (bk, _, mk) in Boards)
            foreach (var (wk, w) in WaveKeys)
                foreach (var v in Vers)
                    res[(bk, wk, v.Key)] = Measure(mk(v.D), w);
        foreach (var (bk, name, mk) in Boards)
        {
            Console.WriteLine($"## {name}（{S307.Seats(mk(UnitCatalog.Sekki))}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 勝率・倒しT | 規定との差 | SW-a との差 | 倒れた ／戦 | 最初に倒れたT | 倒れた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|");
            foreach (var (wk, w) in WaveKeys)
                foreach (var v in Vers)
                {
                    var a = res[(bk, wk, v.Key)];
                    string d0 = v.Key == "0" ? "—" : Delta(a.Win - res[(bk, wk, "0")].Win);
                    string da = v.Key is "0" or "a" ? "—" : Delta(a.Win - res[(bk, wk, "a")].Win);
                    Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {d0} | {da} | {Per(a.Fell, a.N)} | {Per1(a.FirstFellT, a.FirstFellN)} | {a.Fall} |");
                }
            Console.WriteLine();
            Console.WriteLine("呑み: 呑んだ ／戦・誰を（回 ／戦）・後で倒れた（呑まれた駒のうち、その戦で倒れた）・失った手番 ／戦（第419期の数え ＋ ゼリーの中）・**号令 ／ 灯が呑まれた仲間を読んだ ／戦**・休み番のドルガ ／戦・回数切れ ／戦・包めなかったターン ／戦・包んだ ／戦・弾き返し ／戦・号令の +8 ／戦。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 呑んだ ／戦 | 誰を | 後で倒れた | 失った手番 | 号令 ／ 灯 | 休み番ドルガ | 回数切れ | 包めなかったT | 包んだ ／戦 | 弾き返し ／戦 | 号令の +8 ／戦 |");
            Console.WriteLine("|---|---|--:|---|--:|--:|---|--:|--:|--:|--:|--:|--:|");
            foreach (var (wk, w) in WaveKeys)
                foreach (var v in Vers)
                {
                    var a = res[(bk, wk, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per(a.Gulps, a.N)} | {Fx.Who(a.GulpWho, a.N)} | {Pc(a.GulpedLaterFell, a.GulpedUnits)} | {Per(a.LostAll, a.N)} | "
                        + $"{Per(a.RallyRead, a.N)} ／ {Per(a.LampRead, a.N)} | {Per(a.Rest, a.N)} | {Per(a.GulpSpent, a.N)} | {Per(a.BlockTurns, a.N)} | {Per1(a.WrapHits, a.N)} | {Per(a.Bursts, a.N)} | {Per(a.RallyAmt / 8, a.N)} |");
                }
            Console.WriteLine();
            Console.WriteLine("吐き出し（SW-n ／ SW-nd）: 預かって次のターンの終わりに吐き出した ／戦・消化で早く吐き出した ／戦（早く返した割合）・決着のときにまだ中 ／戦・消化で仲間を癒した ／戦・自分を消化した手番 ／戦・消した溜め ／戦・自分が癒えた ／戦。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 預かって吐き出した | 消化で吐き出した | 早く返した割合 | 決着時に中 | 消化で癒した | 自分を消化した手番 | 消した溜め | 自分が癒えた |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (var (wk, w) in WaveKeys)
                foreach (var v in Vers.Where(v => v.Key is "n" or "nd"))
                {
                    var a = res[(bk, wk, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per(a.HeldSpits, a.N)} | {Per(a.DigestSpits, a.N)} | {Pc(a.DigestSpits, a.HeldSpits + a.DigestSpits)} | {Per(a.HeldAtEnd, a.N)} | "
                        + $"{Per1(a.DigestHeal, a.N)} | {Per(a.SelfTurns, a.N)} | {Per1(a.SelfDigest, a.N)} | {Per1(a.SelfHeal, a.N)} |");
                }
            Console.WriteLine();
        }
        Console.WriteLine("## まとめ（4 波の平均勝率・規定との差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Vers.Select(v => v.Label)) + " | 号令 ／ 灯が読んだ ／戦（4 波・SW-a ／ SW-n ／ SW-nd）| 失った手番 ／戦（同）|");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "--:|")) + "---|---|");
        foreach (var (bk, name, _) in Boards)
        {
            double b0 = WaveKeys.Average(w => res[(bk, w.Key, "0")].Win);
            Fx Sum(string k) { var s = new Fx(); foreach (var (wk, _) in WaveKeys) s.Add(res[(bk, wk, k)]); return s; }
            var sa = Sum("a"); var sn = Sum("n"); var snd = Sum("nd");
            Console.WriteLine($"| {name} | " + string.Join(" | ", Vers.Select(v => { double x = WaveKeys.Average(w => res[(bk, w.Key, v.Key)].Win); return v.Key == "0" ? F1(x) : $"{F1(x)}（{Delta(x - b0)}）"; }))
                + $" | {Per(sa.Reads, sa.N)} ／ {Per(sn.Reads, sn.N)} ／ {Per(snd.Reads, snd.N)} | {Per(sa.LostAll, sa.N)} ／ {Per(sn.LostAll, sn.N)} ／ {Per(snd.LostAll, snd.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("§5-2: 呪いの台の上がり幅 ÷ 芯4枚の上がり幅（規定比・4 波平均）");
        Console.WriteLine();
        foreach (var v in Vers.Skip(1))
        {
            double up(string bk) => WaveKeys.Average(w => res[(bk, w.Key, v.Key)].Win) - WaveKeys.Average(w => res[(bk, w.Key, "0")].Win);
            double c = up("curse"), k = up("core4");
            Console.WriteLine($"- {v.Label}: 呪い {Delta(c)} ／ 芯4枚 {Delta(k)}（呪い {(c < k ? "<" : "≥")} 芯4枚）");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3-3: 寄せ集め（`compare` のセッキを含む行）
    // ---------------------------------------------------------------------------------
    static void Mixed()
    {
        var rows = CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "sekki")).ToArray();
        var waves = L.Mains.Append(I.Guard).Append(I.Bat).ToArray();
        Console.WriteLine($"# 第420期 §3-3 —— `compare` のセッキを含む行（{rows.Length} 行）× {{規定, SW-a, SW-n, SW-nd}} × 本編 第1〜5波 ＋ 近衛 ／ 大隊・seed 0..199");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " | 第2〜5波の平均 | 呑んだ ／戦（本編 5 波）| 失った手番 ／戦（本編 5 波）|");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|--:|--:|");
        foreach (var (name, f) in rows)
        {
            Fx[] b = waves.Select(w => Measure(f, w, 200)).ToArray();
            double m0 = Enumerable.Range(1, 4).Average(i => b[i].Win);
            Console.WriteLine($"| {name} | 規定 | " + string.Join(" | ", b.Select(a => F1(a.Win))) + $" | {F1(m0)} | — | — |");
            foreach (var v in Vers.Skip(1))
            {
                Fx[] x = waves.Select(w => Measure(Swap(f, v.D), w, 200)).ToArray();
                double m1 = Enumerable.Range(1, 4).Average(i => x[i].Win);
                long g = x.Take(5).Sum(a => a.Gulps), lost = x.Take(5).Sum(a => a.LostAll);
                Console.WriteLine($"| {name} | {v.Label} | " + string.Join(" | ", x.Select((a, i) => $"{F1(a.Win)}（{Delta(a.Win - b[i].Win)}）")) + $" | {F1(m1)}（{Delta(m1 - m0)}）| {Per(g, 1000)} | {Per(lost, 1000)} |");
            }
        }
        Console.WriteLine();
    }

    static void Log(string vk, string bk, string wk, int seed)
    {
        var vd = Vers.First(v => v.Key == vk).D;
        var (bname, f) = bk.StartsWith("row:") ? CompareBuilds().Where(r => r.Name.StartsWith(bk[4..])).Select(r => (r.Name, Swap(r.F, vd))).First() : (Boards.First(b => b.Key == bk).Name, Boards.First(b => b.Key == bk).F(vd));
        var w = wk.StartsWith("m") ? L.Mains[int.Parse(wk[1..]) - 1] : WaveKeys.First(x => x.Key == wk).W;
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {bname} × {w.Name} × {Vers.First(v => v.Key == vk).Label}・seed {seed}: PlayerWon={r.PlayerWon}・T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(new string(' ', l.Indent * 2) + l.Text);
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int ok = 0, ng = 0;
        void Ok(string what, bool c) { Console.WriteLine($"- {(c ? "○" : "×")} {what}"); if (c) ok++; else ng++; }
        Console.WriteLine("# sekki420 check");
        Console.WriteLine();
        var sk = UnitCatalog.Sekki;
        foreach (var v in Vers.Skip(2))
            Ok($"(1) {v.Label}: Id = sekki・数値は規定のまま・規定の札が先頭に並ぶ・`All` ／ `Retired` に入っていない",
                v.D.Id == "sekki" && v.D.MaxHp == sk.MaxHp && v.D.Attack == sk.Attack && v.D.Speed == sk.Speed
                && v.D.Traits.Take(sk.Traits.Count).SequenceEqual(sk.Traits) && !UnitCatalog.All.Contains(v.D) && !UnitCatalog.Retired.Contains(v.D));
        Ok("(1) SW-n ＝ 規定 ＋ SlimeGulpAlly ＋ SlimeGulpLate ／ SW-nd ＝ SW-n ＋ SlimeDigest・手番は [Skill「消化する」]",
            UnitCatalog.SekkiSWn.Traits.SequenceEqual(sk.Traits.Append(TraitId.SlimeGulpAlly).Append(TraitId.SlimeGulpLate))
            && UnitCatalog.SekkiSWnd.Traits.SequenceEqual(UnitCatalog.SekkiSWn.Traits.Append(TraitId.SlimeDigest))
            && UnitCatalog.SekkiSWnd.Actions.Count == 1 && UnitCatalog.SekkiSWnd.Actions[0].Kind == ActionKind.Skill && UnitCatalog.SekkiSWnd.Actions[0].Label == "消化する");
        Ok("(1) 文面: SW-nd のマイナスから「ぷるぷる」を外した・SW-n は「次のターンの終わり」",
            !UnitCatalog.SekkiSWnd.MinusText.Contains("ぷるぷる") && UnitCatalog.SekkiSWn.MinusText.Contains("次のターンの終わり") && UnitCatalog.SekkiSWn.MinusText.Contains("ぷるぷる"));
        Ok("(1) 新しい札の保持者は `All` に 0 枚", !UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.SlimeGulpLate) || d.Traits.Contains(TraitId.SlimeDigest)));
        // (2) SW-a は第419期と同じ（対照）
        var core = Boards.First(b => b.Key == "core4").F;
        var s4a = Measure(core(UnitCatalog.SekkiSWa), I.StrongWaves[2], 100);
        Ok($"(2) SW-a（対照）× 芯4枚 × 強4 は第419期と同じ 77.0（{F1(s4a.Win)}）", Math.Abs(s4a.Win - 77.0) < 0.05);
        // (3) SW-n: 呑んだ仲間は 2 ターン中にいて、手番を失い、吐き出される
        var n = Measure(core(UnitCatalog.SekkiSWn), I.Bat, 20);
        Ok($"(3) SW-n × 芯4枚 × 大隊: 呑んだ {n.Gulps}・預かって吐き出した {n.HeldSpits} ＋ 決着時に中 {n.HeldAtEnd} ＝ 呑んだ（消化はしない）", n.Gulps > 0 && n.HeldSpits + n.HeldAtEnd == n.Gulps && n.DigestSpits == 0);
        Ok($"(3) SW-n はゼリーの中で手番を失う（{n.Lost420} 回 ／ 20 戦）", n.Lost420 > 0);
        // (4) SW-nd: 消化か預かりのどちらかで必ず出る・自分の消化が鳴る
        var nd = Measure(core(UnitCatalog.SekkiSWnd), I.Bat, 20);
        Ok($"(4) SW-nd × 芯4枚 × 大隊: 呑んだ {nd.Gulps} ＝ 消化 {nd.DigestSpits} ＋ 預かり {nd.HeldSpits} ＋ 決着時に中 {nd.HeldAtEnd}", nd.Gulps == nd.DigestSpits + nd.HeldSpits + nd.HeldAtEnd);
        var ndg = Measure(core(UnitCatalog.SekkiSWnd), I.Boss, 20);   // 精鋭の近衛 ／ 大隊は範囲が無く溜めが 0 のまま（消す溜めが無い）——範囲のあるボスで見る
        Ok($"(4) SW-nd は自分を消化する手番がある（ボス {ndg.SelfTurns} 回 ／ 20 戦）", ndg.SelfTurns > 0);
        // (5) 決定的・verbose の有無
        bool same = Enumerable.Range(0, 5).All(s => { var x = One(core(UnitCatalog.SekkiSWnd), I.Bat, s); var y = One(core(UnitCatalog.SekkiSWnd), I.Bat, s, verbose: true); return x.Wins == y.Wins && x.Turns == y.Turns && x.Gulps == y.Gulps && x.DigestSpits == y.DigestSpits; });
        Ok("(5) SW-nd × 大隊 × seed 0..4 を verbose の有無で回して同じ", same);
        // (6) 試遊の台
        var row = Presets.Versions.FirstOrDefault(r => r.Name == "版・呑み セッキ");
        Ok("(6) 「版・呑み セッキ」は `Presets.Versions` の末尾・並びは 芯4枚（セッキだけ版）", row.Name is not null && Presets.Versions[^1].Name == "版・呑み セッキ"
            && row.F.Occupied().Count() == 5 && row.F.Occupied().All(o => o.Def.Id == "sekki" ? Vers.Skip(1).Any(v => v.D == o.Def) : Presets.FourCore397()[o.Slot] == o.Def));
        Ok("(6) 鎖の終端（`VersionsH407`）は「版・呑み セッキ」を外す", !VersionsH407().Any(r => r.Name == "版・呑み セッキ"));
        // (7) 乱数を引かない
        string src = File.ReadAllText("BattleCore/BattleEngine.cs");
        int i0 = src.IndexOf("// ---- 第420期（セッキの版 SW-n ／ SW-nd）", StringComparison.Ordinal);
        int i1 = src.IndexOf("public void SlimeDigest420", StringComparison.Ordinal);
        int i2 = i1 < 0 ? -1 : src.IndexOf("\n    }\n", i1, StringComparison.Ordinal);
        Ok("(7) 第420期の engine の段は `PickOne` ／ `Roll` を使わない", i0 > 0 && i2 > i0 && !src[i0..i2].Contains("PickOne") && !src[i0..i2].Contains("Roll("));
        Console.WriteLine();
        Console.WriteLine($"○ {ok} ／ × {ng}");
        if (ng > 0) Environment.ExitCode = 1;
    }
}
