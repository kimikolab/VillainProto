using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// chiri428 —— 第428期【軽い期・版 1 つ】導火線のチリ③: 最終形の候補 BM-emx（BM-em ＋ 刻みでは縮まない）／ ウロの退役の下見。
// 指示書は design/PHASE428_CHIRI_FINAL_SPEC.md ／ 報告は design/PHASE428_CHIRI_FINAL.md。
//
//     dotnet run --project BattleSim -c Release 0 chiri428 mixed    # §2-2（主）: 寄せ集め 6 行 × {BM-emx, BM-em, 素体} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 chiri428 boards   # §2-3（従）: 第427期の 4 台（後1）× {BM-emx, BM-em} × 4 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 chiri428 uro      # §3: ウロを引く compare ／ 交差帯の行 × {ウロ, ウロの素体} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 chiri428 log <emx|em|0> <行名の頭> <波の鍵 boss|guard|bat|s4|m1..m5> [seed]
//     dotnet run --project BattleSim -c Release 0 chiri428 check    # 自己検査
// =====================================================================================
static class Chiri428Diag
{
    const string PredictFile = ".tmp/p428/predict.md";
    internal const string PlayRow = "版・導火線 チリ";
    static readonly I.W[] Four = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    static readonly I.W[] Seven = L.Mains.Concat(new[] { I.Guard, I.Bat }).ToArray();
    /// <summary>§2-2 の主 6 行と、チリに替える席（両隣に盾役が来る席）。後ろの 3 行は第428期に選んだ（理由は報告 §1）。</summary>
    internal static readonly (string Row, int Slot)[] Main6 =
    {
        ("継ぎ当て×被弾強化", 4), ("継ぎ当て×分散回復", 2), ("耐久 (ガルド×リリ)", 2),
        ("逆しま (ネル×ウツ)", 2), ("反撃 (ヒサ×カド)", 2), ("礫 (ガレ×ヒビ×ウロ)", 3),
    };
    static readonly string[] Boards4 = { "試遊・刻み ノミ", "試遊・燃焼 ポンの台", "試遊・呪い", "試遊・標 守り型" };
    static readonly (string Key, string Label, UnitDef D)[] Vers =
        { ("emx", "BM-emx", UnitCatalog.ChiriBMemx), ("em", "BM-em", UnitCatalog.ChiriBMem), ("0", "素体", UnitCatalog.ChiriBM0) };
    static readonly (string Key, I.W W)[] WaveKeys = new[] { ("boss", I.Boss), ("guard", I.Guard), ("bat", I.Bat), ("s4", I.StrongWaves[2]) }
        .Concat(Enumerable.Range(1, 5).Select(i => ($"m{i}", L.Mains[i - 1]))).ToArray();
    static readonly string[] Shields = { "golm", "gald", "kado", "doha", "hibi" };

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;
    static Formation Board(string n) => Presets.Versions.Concat(Presets.Playtest).First(r => r.Name == n).F;
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    /// <summary>§3: ウロの素体（同じ数値・特性なし・手番の札なし）。</summary>
    static readonly UnitDef UroPlain = new()
    {
        Id = UnitCatalog.Uro.Id, Name = UnitCatalog.Uro.Name + "（素体）", MaxHp = UnitCatalog.Uro.MaxHp, Attack = UnitCatalog.Uro.Attack, Speed = UnitCatalog.Uro.Speed,
        Traits = Array.Empty<TraitId>(), PlusText = "（素体）", MinusText = "（素体）", Flavor = "",
    };

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "mixed" or "boards" or "uro" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"chiri428: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "mixed": MixedRun(); break;
            case "boards": BoardsRun(); break;
            case "uro": UroRun(); break;
            case "log": Log(A(3, "emx"), A(4, "耐久"), A(5, "bat"), int.Parse(A(6, "0"))); break;
            case "check": Check(); break;
            default: Console.WriteLine("chiri428: モードは mixed / boards / uro / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    sealed class Fx
    {
        public long N, Wins, WinT, Fell;
        public UnitTally B = new();
        public readonly Dictionary<string, long> Death = new(), DeathT = new(), Det = new(), HeldN = new(), HeldW = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; B.Add(o.B);
            foreach (var (d, od) in new[] { (Death, o.Death), (DeathT, o.DeathT), (Det, o.Det), (HeldN, o.HeldN), (HeldW, o.HeldW) })
                foreach (var (k, v) in od) d[k] = d.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => Wins == 0 ? F1(Win) : $"{F1(Win)}・{(double)WinT / Wins:0.0}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
        public double MeanFoeW => B.Bm426BlastFoe == 0 ? double.NaN : (double)B.Bm426WeightFoe / B.Bm426BlastFoe;
        public double MeanAllyW => B.Bm426BlastAlly == 0 ? double.NaN : (double)B.Bm426WeightAlly / B.Bm426BlastAlly;
        public string DetLine
        {
            get
            {
                long n = Det.Values.Sum() + B.Bm427DetBurn + B.Bm427DetPoison;
                var parts = Det.Select(kv => (kv.Key, kv.Value)).Append(("燃焼の刻み", B.Bm427DetBurn)).Append(("毒の刻み", B.Bm427DetPoison))
                    .Where(x => x.Item2 > 0).OrderByDescending(x => x.Item2).Take(4).Select(x => $"{x.Item1} {Pc(x.Item2, n)}");
                return n == 0 ? "—" : string.Join("・", parts);
            }
        }
        public string Held => HeldN.Count == 0 ? "—" : string.Join("、", HeldN.Where(kv => kv.Key != "敵").OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, N)}・{Per1(HeldW[kv.Key], kv.Value)}"));
    }

    static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, _) = I.Raw(f, w, seed);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("chiri", out var t)) a.B.Add(t);
        var allyIds = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, tt) in r.TallyByUnit)
        {
            bool ally = allyIds.Contains(id);
            string name = ally ? Short(p.First(u => u.Def.Id == id).Def) : "敵";
            if (ally && tt.Bm427Det > 0) a.Det[name] = a.Det.GetValueOrDefault(name) + tt.Bm427Det;
            if (tt.Bm426Held > 0) { a.HeldN[name] = a.HeldN.GetValueOrDefault(name) + tt.Bm426Held; a.HeldW[name] = a.HeldW.GetValueOrDefault(name) + tt.Bm426HeldWeight; }
        }
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

    static void MixedRun()
    {
        const int n = 200;
        Console.WriteLine($"# 第428期 §2-2（主）—— 寄せ集め 6 行 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..{n - 1}");
        Console.WriteLine();
        var res = new Dictionary<(string, string, string), Fx>();
        var basis = new Dictionary<(string, string), Fx>();
        foreach (var (row, slot) in Main6)
            foreach (var w in Seven)
            {
                basis[(row, w.Name)] = Measure(Row(row), w, n);
                foreach (var v in Vers) res[(row, v.Key, w.Name)] = Measure(Put(Row(row), slot, v.D), w, n);
            }
        Console.WriteLine("| 行（替えた席・チリの隣） | 版 | " + string.Join(" | ", Seven.Select(w => w.Name)) + " | 素体との差（近衛 ／ 大隊） | 元の行との差（最小） |");
        Console.WriteLine("|---|---|" + string.Concat(Seven.Select(_ => "--:|")) + "---|--:|");
        foreach (var (row, slot) in Main6)
        {
            var f0 = Row(row);
            string nb = string.Join("・", f0.Occupied().Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)).Select(o => (Shields.Contains(o.Def.Id) ? "**" : "") + Short(o.Def) + (Shields.Contains(o.Def.Id) ? "**" : "")));
            string head = $"{row}（{FormationRules.SeatNames[slot]} {Short(f0[slot]!)}・隣 {nb}）";
            Console.WriteLine($"| {head} | 元の行 | " + string.Join(" | ", Seven.Select(w => F1(basis[(row, w.Name)].Win))) + " | — | — |");
            foreach (var v in Vers)
            {
                var xs = Seven.Select(w => res[(row, v.Key, w.Name)]).ToArray();
                string d0 = v.Key == "0" ? "—" : $"{Delta(res[(row, v.Key, I.Guard.Name)].Win - res[(row, "0", I.Guard.Name)].Win)} ／ {Delta(res[(row, v.Key, I.Bat.Name)].Win - res[(row, "0", I.Bat.Name)].Win)}";
                double worst = Seven.Min(w => res[(row, v.Key, w.Name)].Win - basis[(row, w.Name)].Win);
                Console.WriteLine($"| {head} | {v.Label} | " + string.Join(" | ", xs.Select(x => F1(x.Win))) + $" | {d0} | {Delta(worst)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 爆弾（7 波の合計 ／戦・精鋭 2 波だけの値を（）に）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 仕掛けた（敵 ／ 味方）| 爆ぜた（敵側 ／ 味方側）| 誘爆 ／ 不発 | 重さ（敵側 ／ 味方側）| 向かいへ撒いた量（精鋭）| 向かいで倒れた敵 | 持ち主（味方）| 起爆の主 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|---|---|");
        foreach (var (row, _) in Main6)
            foreach (var v in Vers.Where(v => v.Key != "0"))
            {
                var a = new Fx(); foreach (var w in Seven) a.Add(res[(row, v.Key, w.Name)]);
                var e = new Fx(); e.Add(res[(row, v.Key, I.Guard.Name)]); e.Add(res[(row, v.Key, I.Bat.Name)]);
                var b = a.B;
                Console.WriteLine($"| {row} | {v.Label} | {Per(b.Bm426PlantFoe, a.N)} ／ {Per(b.Bm426PlantAlly, a.N)} | {Per(b.Bm426BlastFoe, a.N)} ／ {Per(b.Bm426BlastAlly, a.N)} | {Per(b.Bm426Chain, a.N)} ／ {Per(b.Bm426Duds, a.N)} | {F1(a.MeanFoeW)} ／ {F1(a.MeanAllyW)} | {Per1(b.Bm427MirrorDealt, a.N)}（{Per1(e.B.Bm427MirrorDealt, e.N)}）| {Per(b.Bm428MirrorKilled, a.N)} | {a.Held} | {a.DetLine} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 倒れた味方（精鋭 2 波・BM-emx ／ 素体）");
        Console.WriteLine();
        foreach (var (row, _) in Main6)
            foreach (var w in new[] { I.Guard, I.Bat })
            {
                var x = res[(row, "emx", w.Name)]; var z = res[(row, "0", w.Name)];
                Console.WriteLine($"- {row} {w.Name}: BM-emx {Per(x.Fell, x.N)}（{x.Fall}）／ 素体 {Per(z.Fell, z.N)}（{z.Fall}）");
            }
        Console.WriteLine();
    }

    static void BoardsRun()
    {
        const int n = 100;
        Console.WriteLine($"# 第428期 §2-3（従）—— 第427期の 4 台（後1）× {{BM-emx, BM-em}} × 4 波・seed 0..{n - 1}");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | BM-emx | BM-em | 差 | 敵の爆弾の重さ（emx ／ em）| 爆ぜた 敵側（emx ／ em）| 向かいへ撒いた量（emx ／ em）| 爆発で倒した敵（emx ／ em）| 起爆の主（BM-emx）|");
        Console.WriteLine("|---|---|--:|--:|--:|---|---|---|---|---|");
        foreach (var bn in Boards4)
            foreach (var w in Four)
            {
                var x = Measure(Put(Board(bn), 3, UnitCatalog.ChiriBMemx), w, n);
                var m = Measure(Put(Board(bn), 3, UnitCatalog.ChiriBMem), w, n);
                Console.WriteLine($"| {bn} | {w.Name} | {x.WT} | {m.WT} | {Delta(x.Win - m.Win)} | {F1(x.MeanFoeW)} ／ {F1(m.MeanFoeW)} | {Per(x.B.Bm426BlastFoe, n)} ／ {Per(m.B.Bm426BlastFoe, n)} | {Per1(x.B.Bm427MirrorDealt, n)} ／ {Per1(m.B.Bm427MirrorDealt, n)} | {Per(x.B.Bm426FoeKilled, n)} ／ {Per(m.B.Bm426FoeKilled, n)} | {x.DetLine} |");
            }
        Console.WriteLine();
    }

    static void UroRun()
    {
        const int n = 200;
        var rows = CompareBuilds().Select(r => (r.Name, r.F, Src: "compare")).Concat(CrossBuilds().Select(r => (r.Name, r.F, Src: "交差帯")))
            .Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Uro))).ToArray();
        Console.WriteLine($"# 第428期 §3 —— ウロ（`UnitCatalog.Uro`）を引く行 {rows.Length} 行 × {{ウロ, ウロの素体}} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..{n - 1}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 出どころ | 並び | 版 | " + string.Join(" | ", Seven.Select(w => w.Name)) + " | 素体との差（最大の落ち）|");
        Console.WriteLine("|---|---|---|---|" + string.Concat(Seven.Select(_ => "--:|")) + "--:|");
        foreach (var (name, f, src) in rows)
        {
            var g = f.Clone();
            foreach (var o in f.Occupied()) if (ReferenceEquals(o.Def, UnitCatalog.Uro)) g[o.Slot] = UroPlain;
            var a = Seven.Select(w => Measure(f, w, n).Win).ToArray();
            var b = Seven.Select(w => Measure(g, w, n).Win).ToArray();
            double drop = Enumerable.Range(0, Seven.Length).Max(i => a[i] - b[i]);
            Console.WriteLine($"| {name} | {src} | {S307.Seats(f)} | ウロ | " + string.Join(" | ", a.Select(F1)) + $" | {Delta(drop)} |");
            Console.WriteLine($"| {name} | {src} | | 素体 | " + string.Join(" | ", b.Select(F1)) + " | — |");
        }
        Console.WriteLine();
        Console.WriteLine("（「素体との差」は、ウロを素体に替えたときに最も落ちた波の差＝ウロが効いている量。正なら効いている。）");
    }

    static void Log(string vk, string rowHead, string waveKey, int seed)
    {
        var v = Vers.First(x => x.Key == vk);
        var (row, slot) = Main6.First(m => m.Row.StartsWith(rowHead, StringComparison.Ordinal));
        var w = WaveKeys.First(x => x.Key == waveKey).W;
        var (r, _, _) = I.Raw(Put(Row(row), slot, v.D), w, seed, verbose: true);
        foreach (var l in r.Log) Console.WriteLine(l.Text);
        Console.WriteLine($"→ {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
    }

    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第428期 chiri428 check");
        Console.WriteLine();
        var d = UnitCatalog.ChiriBMemx;
        Req(d.Id == "chiri" && d.Name == "導火線のチリ" && d.MaxHp == 60 && d.Attack == 3 && d.Speed == 6 && d.Flavor == "", "BM-emx は Id \"chiri\" ／ 導火線のチリ ／ HP 60 ／ 攻 3 ／ 速 6 ／ フレーバーは空");
        Req(d.Traits.OrderBy(t => t).SequenceEqual(new[] { TraitId.BombPlantFoe, TraitId.BombMirror, TraitId.BombNoTick }.OrderBy(t => t)), "札 ＝ 敵に仕掛ける ＋ 向かいへ ＋ 刻みでは縮まない（第427期の札の組み合わせだけ）");
        Req(!UnitCatalog.All.Contains(d) && !UnitCatalog.Retired.Contains(d) && UnitCatalog.All.Contains(UnitCatalog.Uro), "BM-emx は `All` ／ `Retired` の外・ウロは `All` のまま（この期は退役しない）");
        Req(Main6.All(m => CompareBuilds().Any(r => r.Name == m.Row)), "主 6 行はすべて compare の行");
        Req(Main6.All(m => Row(m.Row).Occupied().Any(o => o.Slot != m.Slot && FormationRules.AreAdjacent(m.Slot, o.Slot) && Shields.Contains(o.Def.Id))), "主 6 行のチリの隣に盾役（ゴルム ／ ガルド ／ カド ／ ドハ ／ ヒビ）が 1 体以上");
        bool tick0 = true, same = true, mir = false;
        for (int s = 0; s < 6; s++)
        {
            var f = Put(Board("試遊・燃焼 ポンの台"), 3, d);
            var (r1, _, _) = I.Raw(f, I.Guard, s, census: false);
            var (r2, _, _) = I.Raw(f, I.Guard, s, verbose: true, census: false);
            var t = r1.TallyByUnit["chiri"];
            if (t.Bm427FuseBurn + t.Bm427FusePoison > 0) tick0 = false;
            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
            var (r3, _, _) = I.Raw(Put(Row("耐久 (ガルド×リリ)"), 2, d), I.Bat, s, census: false);
            if (r3.TallyByUnit["chiri"].Bm427MirrorBlasts > 0) mir = true;
        }
        Req(tick0, "刻みでは導火線が縮まない（燃焼の台 × 近衛 × 6 seed）");
        Req(same, "verbose の有無で勝敗 ／ T が変わらない");
        Req(mir, "味方の爆弾が向かいへ爆ぜる（耐久 × 大隊 × 6 seed）");
        var row = Presets.Versions.FirstOrDefault(r => r.Name == PlayRow);
        Req(row.Name is not null && row.F.Occupied().Any(o => ReferenceEquals(o.Def, d)), $"試遊の台「{PlayRow}」は BM-emx");
        Req(!VersionsH407().Any(r => r.Name == PlayRow), "鎖の終端（`VersionsH407`）は「版・導火線 チリ」を外す");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
