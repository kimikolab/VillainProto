using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using BA = BurnAuditDiag;

// =====================================================================================
// shiga288 —— 第288期「シガの蓄電（感電軸のアタッカー化）」。
// 指示書は design/PHASE288_SHIGA_CHARGE_SPEC.md ／ 報告は design/PHASE288_SHIGA_CHARGE.md。
//
//     dotnet run --project BattleSim -c Release 0 shiga288 p0            # Phase 0: 蓄電の供給源・シガの感電が手番の前に失われる経路・振り数・0 → 4 の手番（規定のシガ・計数だけ）
//     dotnet run --project BattleSim -c Release 0 shiga288 compare       # `compare` 64 行 × 版（規定 ／ SG-a ／ SG-b ／ SG-c）× 5 波（seed 0..199・シガ在席の行だけ差し替える）
//     dotnet run --project BattleSim -c Release 0 shiga288 boards        # 代表台 × 版（seed 0..199・verbose）と対照（トウ ／ シガ ／ カタ → ドルガ）
//     dotnet run --project BattleSim -c Release 0 shiga288 grid <ボス|近衛|大隊> <規定|SG-a|SG-b|SG-c>   # 第287期の格子（固定枠 トウ ＋ シガの版）
//     dotnet run --project BattleSim -c Release 0 shiga288 check         # 自己検査
//     dotnet run --project BattleSim -c Release 0 shiga288 log <波> <版> <席の並び（短い名前を ・ で5つ）> [seed]
//
// 第288期の規定は G3K（第289期から `UnitCatalog.ShigaG3K`・規定のシガは SG-a ＝ `ShigaSGa`）。版の名前「G3K」は第288期の報告の「規定」の列と同じもの。
// =====================================================================================
static class Shiga288Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        switch (mode)
        {
            case "p0": P0(); return;
            case "compare": CompareAll(); return;
            case "boards": BoardsAll(); return;
            case "grid": Grid(args.Length > 3 ? args[3] : "ボス", args.Length > 4 ? args[4] : "G3K"); return;
            case "check": Check(); return;
            case "log": LogOne(args.Length > 3 ? args[3] : "ボス", args.Length > 4 ? args[4] : "SG-b", args.Length > 5 ? args[5] : "", args.Length > 6 ? int.Parse(args[6]) : 0); return;
            default: Console.WriteLine("shiga288: モードは p0 / compare / boards / grid / check / log。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・波・台（測る前に固定・指示書 §2 ／ §4-2）
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef Def)[] Vers =
    {
        ("G3K", UnitCatalog.ShigaG3K), ("SG-a", UnitCatalog.ShigaSGa), ("SG-b", UnitCatalog.ShigaSGb), ("SG-c", UnitCatalog.ShigaSGc), ("SG-c′", UnitCatalog.ShigaSGcAny),
    };
    /// <summary>版と波は ASCII の別名でも引ける（シェルを跨ぐ起動で日本語の引数が化けないように）: def ／ a ／ b ／ c ／ cp、boss ／ guard ／ bat。</summary>
    static string VerName(string n) => n switch { "def" => "G3K", "g3k" => "G3K", "a" => "SG-a", "b" => "SG-b", "c" => "SG-c", "cp" => "SG-c′", _ => n };
    static UnitDef VerOf(string n) => Vers.First(v => v.Name == VerName(n)).Def;

    static S287.Wave WaveOf(string n) => S287.Waves.First(w => w.Name == (n switch { "boss" => "ボス", "guard" => "近衛", "bat" => "大隊", _ => n }));
    const int Seeds = 200, CutSeeds = 20;

    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static UnitDef ByShort(string n) => UnitCatalog.All.First(d => Short(d) == n);
    static UnitDef[] Order(string s) => s.Split('・', StringSplitOptions.RemoveEmptyEntries).Select(ByShort).ToArray();
    static string OrderName(UnitDef[] o) => string.Join("・", o.Select(Short));
    /// <summary>第290期: 名前で引く台のカタ（規定は第290期から KR-b）を旧の規定 `KataS3` に固定する（第288期の台を再現するため）。</summary>
    static Formation Seat(UnitDef[] o) => FvSwap(FvSwap(B283.Seat(o), UnitCatalog.Kata, UnitCatalog.KataS3), UnitCatalog.Kugu, UnitCatalog.KuguKG0);   // 第291期: クグも旧の規定へ
    /// <summary>第290期: `compare` の行のカタを旧の規定 `KataS3` に固定した行（シガは版で差し替えるので触らない）。</summary>
    static (string Name, Formation F)[] Rows288() => CompareBuilds().Select(r => (r.Name, FvSwap(FvSwap(r.F, UnitCatalog.Kata, UnitCatalog.KataS3), UnitCatalog.Kugu, UnitCatalog.KuguKG0))).ToArray();
    static Formation SwapShiga(Formation f, UnitDef ver) => ReferenceEquals(ver, UnitCatalog.Shiga) ? f : FvSwap(f, UnitCatalog.Shiga, ver);

    /// <summary>
    /// 代表台（指示書 §4-2）: 第287期 R4 の 7 台 ＋ `責め苦` ／ `感電` の2行（3 波）＋ カタをシガの隣に置いた台（近衛 ／ 大隊 各1台）。
    /// カタの台の作り方（測る前に固定）: 第287期の各波の上位1位の台（シガ・ガルド・ベニ・トウ・ソラ）で、探索枠3 のうち第287期 R5 の到達寄与が最も低い1枚をカタに替え、
    /// その席がシガの隣でなければ、シガの隣の席にいる探索枠の駒（中央のベニ）と席を入れ替える。
    /// 近衛: ソラ（3.2%）→ カタ・後3 は前1 の隣でない → 中央のベニと入れ替え ＝ シガ・ガルド・カタ・トウ・ベニ。
    /// 大隊: ガルド（0.2%）→ カタ・前3 は前1 の隣でない → 中央のベニと入れ替え ＝ シガ・ベニ・カタ・トウ・ソラ。
    /// </summary>
    internal static (string Name, string Wave, Func<Formation> Make, bool Kata)[] Boards()
    {
        Formation Row(string n) => Rows288().First(r => r.Name == n).F;
        var l = new List<(string, string, Func<Formation>, bool)>
        {
            ("R4 ボス1", "ボス", () => Seat(Order("シガ・トウ・ベニ・クビ・バン")), false),
            ("R4 近衛1", "近衛", () => Seat(Order("シガ・ガルド・ベニ・トウ・ソラ")), false),
            ("R4 近衛2", "近衛", () => Seat(Order("ソラ・シガ・ベニ・ドハ・トウ")), false),
            ("R4 近衛3", "近衛", () => Seat(Order("ガルド・トウ・ベニ・クグ・シガ")), false),
            ("R4 大隊1", "大隊", () => Seat(Order("シガ・ガルド・ベニ・トウ・ソラ")), false),
            ("R4 大隊2", "大隊", () => Seat(Order("トウ・ソラ・ベニ・シガ・ドハ")), false),
            ("R4 大隊3", "大隊", () => Seat(Order("シガ・ヒサ・クビ・ベニ・トウ")), false),
        };
        foreach (var w in new[] { "ボス", "近衛", "大隊" })
        {
            l.Add(($"責め苦 × {w}", w, () => Row("責め苦 (トウ×シガ)"), false));
            l.Add(($"感電 × {w}", w, () => Row("感電 (シガ×カタ×ソム)"), true));
        }
        l.Add(("カタ隣 近衛", "近衛", () => Seat(Order("シガ・ガルド・カタ・トウ・ベニ")), true));
        l.Add(("カタ隣 大隊", "大隊", () => Seat(Order("シガ・ベニ・カタ・トウ・ソラ")), true));
        return l.ToArray();
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Deep
    {
        public long N, Wins, WinT, Swings, Cower, Wired, ChargeEnd, FullN, FullT, Bolts, BoltDry, BoltHits, BoltLone, WhipBase, WhipBonus, BoltNom, BoltLoneNom, BoltDealt, BoltKills;
        public long[] Src = new long[5];
        public long Recv, Popped, PoppedChain, FoeDis, AllyDis, ShigaDied, ShigaDeathT, ShigaDmg, KataThunder;
        // 手数の判定材料（シガが倒れなかった戦）
        public long AliveN, AliveSwings, AliveShigaDmg, AliveWonN, AliveWonTeamDmg, AliveWonShigaDmg, AliveLostN, AliveLostRemain;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Swings += o.Swings; Cower += o.Cower; Wired += o.Wired; ChargeEnd += o.ChargeEnd; FullN += o.FullN; FullT += o.FullT;
            Bolts += o.Bolts; BoltDry += o.BoltDry; BoltHits += o.BoltHits; BoltLone += o.BoltLone; WhipBase += o.WhipBase; WhipBonus += o.WhipBonus;
            BoltNom += o.BoltNom; BoltLoneNom += o.BoltLoneNom; BoltDealt += o.BoltDealt; BoltKills += o.BoltKills;
            for (int i = 0; i < Src.Length; i++) Src[i] += o.Src[i];
            Recv += o.Recv; Popped += o.Popped; PoppedChain += o.PoppedChain; FoeDis += o.FoeDis; AllyDis += o.AllyDis; ShigaDied += o.ShigaDied; ShigaDeathT += o.ShigaDeathT;
            ShigaDmg += o.ShigaDmg; KataThunder += o.KataThunder;
            AliveN += o.AliveN; AliveSwings += o.AliveSwings; AliveShigaDmg += o.AliveShigaDmg; AliveWonN += o.AliveWonN; AliveWonTeamDmg += o.AliveWonTeamDmg;
            AliveWonShigaDmg += o.AliveWonShigaDmg; AliveLostN += o.AliveLostN; AliveLostRemain += o.AliveLostRemain;
        }
    }

    internal static Deep FightDeep(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1 };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        long teamDmg = 0;
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (mine.Contains(id)) { d.AllyDis += t.DischargeTaken; teamDmg += t.DamageToEnemy; }
            else d.FoeDis += t.DischargeTaken;
        }
        UnitState? shiga = p.FirstOrDefault(u => u.Def.Id == UnitCatalog.Shiga.Id);
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Shiga.Id, out var st))
        {
            d.Swings = st.WhipSwings; d.Cower = st.WhipCowered; d.Wired = st.WiredSwings;
            d.Bolts = st.BoltCasts; d.BoltDry = st.BoltDry; d.BoltHits = st.BoltHits; d.BoltLone = st.BoltLoneHits;
            d.WhipBase = st.WhipBase; d.WhipBonus = st.WhipBonus; d.BoltNom = st.BoltNominal; d.BoltLoneNom = st.BoltLoneNominal; d.BoltDealt = st.BoltDealt; d.BoltKills = st.BoltKills;
            if (st.ChargeFulls > 0) { d.FullN = 1; d.FullT = st.ChargeFullTurn; }
            d.Recv = st.ShockReceived; d.Popped = st.ShockSpent; d.ShigaDmg = st.DamageToEnemy;
        }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.KataS3.Id, out var kt)) d.KataThunder = kt.ThunderDealt;
        if (shiga is not null)
        {
            d.ChargeEnd = shiga.RawCounter(StoredChargeTrait.Key);
            var inst = p.ToDictionary(u => u.InstanceId);
            foreach (var ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.StatusGain && ev.TargetId == shiga.InstanceId && ev.Text == StatusKeys.Shock)
                {
                    // 供給源（規定のシガでも数える・感電が新しく付いた回数）
                    int src = 4;
                    if (ev.ActorId is int a && inst.TryGetValue(a, out var wr) && wr != shiga)
                        src = wr.Def.Id == "tou" ? 0 : wr.Def.Id == "kata" ? 1 : wr.Def.Id == "som" ? 2 : 3;
                    d.Src[src]++;
                }
                else if (ev.Kind == BattleEventKind.ShockSpent && ev.TargetId == shiga.InstanceId && ev.Slot > 0) d.PoppedChain++;
                else if (ev.Kind == BattleEventKind.Death && ev.TargetId == shiga.InstanceId && d.ShigaDied == 0) { d.ShigaDied = 1; d.ShigaDeathT = ev.Turn; }
            }
            if (d.ShigaDied == 0)
            {
                d.AliveN = 1; d.AliveSwings = d.Swings; d.AliveShigaDmg = d.ShigaDmg;
                if (r.PlayerWon) { d.AliveWonN = 1; d.AliveWonTeamDmg = teamDmg; d.AliveWonShigaDmg = d.ShigaDmg; }
                else { d.AliveLostN = 1; d.AliveLostRemain = e.Where(u => u.IsAlive).Sum(u => (long)u.Hp); }
            }
        }
        return d;
    }

    internal static Deep MeasureDeep(Formation f, S287.Wave w, int from, int n)
    {
        var parts = new Deep[n];
        Parallel.For(0, n, i => parts[i] = FightDeep(f, w, from + i));
        var all = new Deep();
        foreach (var d in parts) all.Merge(d);
        return all;
    }

    static bool FightLite(Formation f, S287.Wave w, int seed, out int turns)
    {
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: false);
        turns = r.Turns;
        return r.PlayerWon;
    }

    static int Wins(Formation f, S287.Wave w, int from, int n, out long winT)
    {
        int wins = 0; winT = 0;
        for (int s = from; s < from + n; s++) if (FightLite(f, w, s, out int t)) { wins++; winT += t; }
        return wins;
    }

    static int WinsPar(Formation f, S287.Wave w, int n)
    {
        int wins = 0;
        Parallel.For(0, n, s => { if (FightLite(f, w, s, out _)) Interlocked.Increment(ref wins); });
        return wins;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static readonly string[] SrcNames = { "トウ", "カタ", "ソム", "ほかの味方", "自分・敵" };

    // ---------------------------------------------------------------------------------
    // Phase 0（規定のシガ・計数だけ）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第288期 Phase 0 —— 規定のシガの感電の出入りと振り数（seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("付 ＝ シガに新しく感電が付いた回数（1戦・書き手ごと）／ 弾 ＝ シガの感電が弾けた（被弾 ＋ 味方の放電の連鎖）／ 連 ＝ そのうち味方の放電の連鎖で弾けた ／ 電 ＝ 電気鞭で使った ／ "
                          + "失 ＝ 弾 ÷ 付（付いた感電が手番の前に失われた割合）／ 振 ＝ 鞭を振った手番 ／ 怖 ＝ 怖気づいた ／ 4 付 ＝ 付いた感電の累計が 4 に届いた手番（届いた戦の平均・届いた戦の割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 倒しT | 付（トウ ／ カタ ／ ソム ／ ほか ／ 自敵） | 付 ÷ T | 弾（連） | 電 | 失 | 振 | 怖 | 4 付のT（割合） | シガが倒れたT（割合） |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in Boards())
        {
            var w = WaveOf(b.Wave);
            var f = SwapShiga(b.Make(), UnitCatalog.ShigaG3K);   // 第289期: Phase 0 は第288期の規定（G3K）で数える
            // 0 → 4 の手番は「付いた感電の累計」で数える（規定のシガには蓄電が無い・上限を掛けない）。
            var parts = new (Deep D, int T4, int Turns)[Seeds];
            Parallel.For(0, Seeds, i =>
            {
                var d = FightDeep(f, w, i);
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, w.Make(), i, verbose: true);
                var sh = p.First(u => u.Def.Id == UnitCatalog.Shiga.Id);
                int cum = 0, t4 = 0;
                foreach (var ev in r.Events)
                    if (ev.Kind == BattleEventKind.StatusGain && ev.TargetId == sh.InstanceId && ev.Text == StatusKeys.Shock && ++cum == 4) { t4 = ev.Turn; break; }
                parts[i] = (d, t4, r.Turns);
            });
            var a = new Deep();
            foreach (var x in parts) a.Merge(x.D);
            long recv = a.Src.Sum();
            long turns = parts.Sum(x => (long)(x.D.ShigaDied == 1 ? x.D.ShigaDeathT : x.Turns));
            var t4s = parts.Where(x => x.T4 > 0).ToList();
            Console.WriteLine($"| {b.Name} | {b.Wave} | {F1(a.Win)} | {Per(a.WinT, a.Wins)} | {Per(recv, a.N)}（{string.Join(" ／ ", a.Src.Select(s => Per(s, a.N)))}） | {Per(recv, turns)} | "
                + $"{Per(a.Popped, a.N)}（{Per(a.PoppedChain, a.N)}） | {Per(a.Wired, a.N)} | {(recv == 0 ? "—" : (100.0 * a.Popped / recv).ToString("F1") + "%")} | {Per(a.Swings, a.N)} | {Per(a.Cower, a.N)} | "
                + $"{(t4s.Count == 0 ? "—" : t4s.Average(x => x.T4).ToString("F1"))}（{F1(100.0 * t4s.Count / Seeds)}%） | {Per1(a.ShigaDeathT, a.ShigaDied)}（{F1(100.0 * a.ShigaDied / a.N)}%） |");
        }
        Console.WriteLine();
        Console.WriteLine("付 ÷ T ＝ シガが生きていたターンあたりの付（倒れた戦は倒れたT まで）。供給源ごとの 0 → 4 の手番の見込み ＝ 4 ÷（その書き手の付 ÷ T）。");
        Console.WriteLine();
        foreach (var b in Boards()) Console.WriteLine($"- {b.Name}: {BA.SeatsNamed(b.Make())}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` 64 行 × 版
    // ---------------------------------------------------------------------------------
    static bool HasShiga(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Shiga));

    static double[,] CompareGrid(UnitDef ver)
    {
        var rows = Rows288();
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            var f = SwapShiga(rows[ri].F, ver);
            int wins = 0;
            for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / Seeds;
        });
        return g;
    }

    static void CompareAll()
    {
        var rows = Rows288();
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.ToDictionary(v => v.Name, v => CompareGrid(v.Def));
        var basis = grids["G3K"];
        Console.WriteLine("# 第288期 `compare` 64 行 × シガの版（seed 0..199・シガ在席の行だけ `UnitCatalog.Shiga` を版に差し替える）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 規定との差 | 最大の落ち（波） |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|");
        for (int ri = 0; ri < rows.Length; ri++)
        {
            if (!HasShiga(rows[ri].F)) continue;
            double m0 = Enumerable.Range(1, nw - 1).Average(w => basis[ri, w]);
            foreach (var v in Vers)
            {
                var g = grids[v.Name];
                double m = Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
                double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - basis[ri, w]);
                Console.WriteLine($"| {rows[ri].Name} | {v.Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w]))) + $" | {F1(m)} | "
                    + $"{(v.Name == "G3K" ? "" : (m - m0).ToString("+0.0;-0.0;0.0"))} | {(v.Name == "G3K" ? "" : drop.ToString("+0.0;-0.0;0.0"))} |");
            }
        }
        Console.WriteLine();
        foreach (var v in Vers.Skip(1))
        {
            int bad = 0, cells = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (HasShiga(rows[ri].F)) continue;
                for (int w = 0; w < nw; w++) { cells++; if (grids[v.Name][ri, w] != basis[ri, w]) bad++; }
            }
            Console.WriteLine($"- シガのいない行のずれ（{v.Name} − 規定）: {cells} セル中 **{bad} 件**");
        }
        var prim = Baseline.PrimaryRows.Select(n => Array.FindIndex(rows, r => r.Name == n)).ToArray();
        Console.WriteLine();
        Console.WriteLine($"| 版 | 全64行 第1〜5波 | 主判定19行 第1〜5波 | 主判定の第五波 − 歯止め（{Baseline.PrimaryFifthFloor}%） | 情報セル（全行 ／ 主判定・第2〜5波の 0 < x < 100） | −10pt 以上落ちた行（波） |");
        Console.WriteLine("|---|---|---|--:|--:|---|");
        foreach (var v in Vers)
        {
            var g = grids[v.Name];
            int info = 0, infoP = 0;
            for (int ri = 0; ri < rows.Length; ri++)
                for (int w = 1; w < nw; w++)
                    if (g[ri, w] > 0 && g[ri, w] < 100) { info++; if (prim.Contains(ri)) infoP++; }
            var drops = new List<string>();
            for (int ri = 0; ri < rows.Length; ri++)
                for (int w = 0; w < nw; w++)
                    if (g[ri, w] - basis[ri, w] <= -10.0) drops.Add($"{rows[ri].Name} 第{w + 1}波 {basis[ri, w]:F1} → {g[ri, w]:F1}");
            Console.WriteLine($"| {v.Name} | " + string.Join(" / ", Enumerable.Range(0, nw).Select(w => F1(Enumerable.Range(0, rows.Length).Average(ri => g[ri, w]))))
                + " | " + string.Join(" / ", Enumerable.Range(0, nw).Select(w => F1(prim.Average(ri => g[ri, w]))))
                + $" | {(prim.Average(ri => g[ri, nw - 1]) - Baseline.PrimaryFifthFloor):+0.0;-0.0} | {info} ／ {infoP} | {(drops.Count == 0 ? "なし" : string.Join("・", drops))} |");
        }
    }

    // ---------------------------------------------------------------------------------
    // 代表台 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bs = Boards();
        var res = new Dictionary<(int, string), (Deep D, int Tou, int Shi, int Kata)>();
        for (int bi = 0; bi < bs.Length; bi++)
            foreach (var v in Vers)
            {
                var w = WaveOf(bs[bi].Wave);
                var f = SwapShiga(bs[bi].Make(), v.Def);
                var d = MeasureDeep(f, w, 0, Seeds);
                int dt = WinsPar(FvSwap(f, UnitCatalog.Tou, UnitCatalog.Dolga), w, Seeds);
                int ds = WinsPar(FvSwap(f, v.Def, UnitCatalog.Dolga), w, Seeds);
                int dk = bs[bi].Kata ? WinsPar(FvSwap(f, UnitCatalog.KataS3, UnitCatalog.Dolga), w, Seeds) : -1;
                res[(bi, v.Name)] = (d, dt, ds, dk);
            }

        Console.WriteLine("# 第288期 代表台 × シガの版（seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("## 表1 勝率・振り数・蓄電・雷霆・対照");
        Console.WriteLine();
        Console.WriteLine("振 ＝ 鞭を振った手番（1戦）／ 怖 ＝ 怖気づいて失った手番 ／ 蓄末 ＝ 戦の終わりの蓄電 ／ 満T ＝ 初めて蓄電 4 に達したT（達した戦の割合）／ 霆 ＝ 雷霆の手番（うち空振り）／ 霆当 ＝ 雷霆の追加を当てた延べ（うち孤立）／ "
                          + "対照 ＝ その駒 → ドルガ の勝率（同じ席・同じ seed）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 倒しT | 振 | 怖 | 蓄末 | 満T（割合） | 霆（空） | 霆当（孤） | 放電 敵 ／ 味方 | シガが倒れたT（割合） | トウ → ドルガ | シガ → ドルガ | カタ → ドルガ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < bs.Length; bi++)
            foreach (var v in Vers)
            {
                var (a, dt, ds, dk) = res[(bi, v.Name)];
                Console.WriteLine($"| {bs[bi].Name} | {bs[bi].Wave} | {v.Name} | {F1(a.Win)} | {Per(a.WinT, a.Wins)} | {Per(a.Swings, a.N)} | {Per(a.Cower, a.N)} | {Per(a.ChargeEnd, a.N)} | "
                    + $"{Per1(a.FullT, a.FullN)}（{F1(100.0 * a.FullN / a.N)}%） | {Per(a.Bolts, a.N)}（{Per(a.BoltDry, a.N)}） | {Per(a.BoltHits, a.N)}（{Per(a.BoltLone, a.N)}） | "
                    + $"{Per1(a.FoeDis, a.N)} ／ {Per1(a.AllyDis, a.N)} | {Per1(a.ShigaDeathT, a.ShigaDied)}（{F1(100.0 * a.ShigaDied / a.N)}%） | {F1(100.0 * dt / Seeds)} | {F1(100.0 * ds / Seeds)} | {(dk < 0 ? "—" : F1(100.0 * dk / Seeds))} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表2 シガの与ダメの内訳（1戦・名目）と蓄電の供給源");
        Console.WriteLine();
        Console.WriteLine("鞭 ＝ 鞭の名目（2倍の前・当たる駒ごと・蓄電の上乗せを含む）／ 2倍 ＝ 2倍で足した名目 ／ 霆 ＝ 雷霆の追加の名目（孤立の上乗せを除く）／ 孤 ＝ 孤立の上乗せ ／ 霆減 ＝ 雷霆で実際に減った HP（うち倒した）／ "
                          + "与 ＝ シガが敵に与えた HP（`DamageToEnemy`）／ 付 ＝ シガに新しく付いた感電（トウ ／ カタ ／ ソム ／ ほか ／ 自敵）／ カタの雷 ＝ カタの雷が減らした HP");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 鞭 | 2倍 | 霆 | 孤 | 霆減（倒） | 与 | 付（トウ ／ カタ ／ ソム ／ ほか ／ 自敵） | カタの雷 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|--:|");
        for (int bi = 0; bi < bs.Length; bi++)
            foreach (var v in Vers)
            {
                var a = res[(bi, v.Name)].D;
                Console.WriteLine($"| {bs[bi].Name} | {v.Name} | {Per1(a.WhipBase, a.N)} | {Per1(a.WhipBonus, a.N)} | {Per1(a.BoltNom - a.BoltLoneNom, a.N)} | {Per1(a.BoltLoneNom, a.N)} | "
                    + $"{Per1(a.BoltDealt, a.N)}（{Per(a.BoltKills, a.N)}） | {Per1(a.ShigaDmg, a.N)} | {Per(a.Src.Sum(), a.N)}（{string.Join(" ／ ", a.Src.Select(s => Per(s, a.N)))}） | {(bs[bi].Kata ? Per1(a.KataThunder, a.N) : "—")} |");
            }
        Console.WriteLine();
        Console.WriteLine("（規定のシガの 鞭 ／ 2倍 ／ 霆 の列は計数の口が蓄電の保持者だけなので 0。規定の与ダメは 与 の列で読む。）");
        Console.WriteLine();

        Console.WriteLine("## 表3 手数の判定材料（シガが倒れなかった戦）");
        Console.WriteLine();
        Console.WriteLine("1振り ＝ シガの与ダメ ÷ 振り数 ／ 勝った戦の比 ＝ シガの与ダメ ÷ 味方の与ダメ合計 ／ 負けた戦の残り ＝ 決着時に生きていた敵の HP の合計 ／ あと何振り ＝ 残り ÷ 1振り");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倒れなかった戦 | 振（1戦） | 1振り | 勝った戦（シガの与 ÷ 味方の与） | 負けた戦 | 負けた戦の残り HP | あと何振り |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < bs.Length; bi++)
            foreach (var v in Vers)
            {
                var a = res[(bi, v.Name)].D;
                double per = a.AliveSwings == 0 ? double.NaN : (double)a.AliveShigaDmg / a.AliveSwings;
                double remain = a.AliveLostN == 0 ? double.NaN : (double)a.AliveLostRemain / a.AliveLostN;
                Console.WriteLine($"| {bs[bi].Name} | {v.Name} | {a.AliveN} | {Per(a.AliveSwings, a.AliveN)} | {F1(per)} | {a.AliveWonN}（{(a.AliveWonTeamDmg == 0 ? "—" : (100.0 * a.AliveWonShigaDmg / a.AliveWonTeamDmg).ToString("F1") + "%")}） | "
                    + $"{a.AliveLostN} | {F1(remain)} | {(double.IsNaN(per) || double.IsNaN(remain) || per <= 0 ? "—" : (remain / per).ToString("F1"))} |");
            }
        Console.WriteLine();
        foreach (var b in bs) Console.WriteLine($"- {b.Name}: {BA.SeatsNamed(b.Make())}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 格子（第287期の器具の流用・固定枠 トウ ＋ シガの版）
    // ---------------------------------------------------------------------------------
    static int CutWins(S287.Wave w) => w.Boss ? 1 : 5;
    static bool Reached(S287.Wave w, int wins, int dolgaWins) => w.Boss ? wins > 0 && dolgaWins == 0 : wins * 2 >= Seeds && dolgaWins * 2 <= wins;
    static bool WinLine(S287.Wave w, int wins) => w.Boss ? wins > 0 : wins * 2 >= Seeds;
    /// <summary>その駒が要る ＝ その駒 → ドルガで勝率が半分以下（ボスは 0）に落ちる（第287期の「届いた」と同じ線）。</summary>
    static bool Needed(S287.Wave w, int wins, int dolgaWins) => w.Boss ? dolgaWins == 0 : dolgaWins * 2 <= wins;

    sealed record BoardRes(UnitDef[] Order, int Wins, long WinT, int DolgaTou, int DolgaShiga, bool Reach);

    static void Grid(string waveName, string verName)
    {
        var w = WaveOf(waveName);
        var ver = VerOf(verName);
        verName = VerName(verName);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fixedU = new[] { UnitCatalog.Tou, ver };
        var lu = S287.Lineups();
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedU.Concat(t).ToArray()));
        var res = new BoardRes?[boards.Count];
        int cutPass = 0, winPass = 0;
        Parallel.For(0, boards.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var f = Seat(boards[i]);
            int cw = Wins(f, w, 0, CutSeeds, out _);
            if (cw < CutWins(w)) return;
            Interlocked.Increment(ref cutPass);
            int wins = Wins(f, w, 0, Seeds, out long wt);
            int dg = -1, ds = -1;
            if (WinLine(w, wins))
            {
                Interlocked.Increment(ref winPass);
                dg = Wins(FvSwap(f, UnitCatalog.Tou, UnitCatalog.Dolga), w, 0, Seeds, out _);
                ds = Wins(FvSwap(f, ver, UnitCatalog.Dolga), w, 0, Seeds, out _);
            }
            res[i] = new BoardRes(boards[i], wins, wt, dg, ds, dg >= 0 && Reached(w, wins, dg));
        });
        var all = res.Where(r => r is not null).Select(r => r!).ToList();
        var reached = all.Where(r => r.Reach).ToList();
        var heal = S287.Heal;
        bool IsHeal(UnitDef d) => heal.Contains(d);
        string Key(UnitDef[] o) => string.Join(",", o.Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal));

        Console.WriteLine($"# 第288期 格子 × {w.Name} × シガ {verName}");
        Console.WriteLine();
        Console.WriteLine($"固定枠 トウ（規定 T3）＋ シガ（{verName}）／ 探索枠3（第287期と同じ候補 {S287.Pool.Length} 枚・ヒーラー ≦ 1）× 席 120 ＝ {boards.Count:N0} 台。足切り seed 0..{CutSeeds - 1}（{CutWins(w)} 勝以上）→ seed 0..{Seeds - 1} → ドルガ対照（トウ → ドルガ ／ シガ → ドルガ・同じ席・同じ seed）");
        Console.WriteLine($"届いた ＝ {(w.Boss ? "勝率が 0% を離れ、トウ → ドルガが 0%" : "勝率 50% 以上、かつトウ → ドルガがその半分以下")}（第287期と同じ）。シガが要る ＝ シガ → ドルガが{(w.Boss ? " 0%" : "その半分以下")}");
        Console.WriteLine();
        Console.WriteLine("## 表1 段ごとの台数");
        Console.WriteLine();
        Console.WriteLine("| 段 | 台 | 組 | うちヒーラー 0 枚の台 ／ 組 |");
        Console.WriteLine("|---|--:|--:|--:|");
        void RowOf(string label, IEnumerable<UnitDef[]> os)
        {
            var l = os.ToList();
            var h0 = l.Where(o => !o.Any(IsHeal)).ToList();
            Console.WriteLine($"| {label} | {l.Count:N0} | {l.Select(Key).Distinct().Count()} | {h0.Count:N0} ／ {h0.Select(Key).Distinct().Count()} |");
        }
        RowOf("全体", boards);
        RowOf("足切りを通った", all.Select(r => r.Order));
        RowOf(w.Boss ? "勝率 ＞ 0%" : "勝率 ≧ 50%", all.Where(r => r.DolgaTou >= 0).Select(r => r.Order));
        RowOf("**届いた**（トウ → ドルガで落ちる）", reached.Select(r => r.Order));
        RowOf("届いた台のうち **シガが要る**（シガ → ドルガでも落ちる）", reached.Where(r => Needed(w, r.Wins, r.DolgaShiga)).Select(r => r.Order));
        RowOf("勝率の線を越えた台のうち シガが要る（届いたかを問わない）", all.Where(r => r.DolgaShiga >= 0 && Needed(w, r.Wins, r.DolgaShiga)).Select(r => r.Order));
        RowOf("勝率の線は越えたがトウ → ドルガで落ちない", all.Where(r => r.DolgaTou >= 0 && !r.Reach).Select(r => r.Order));
        Console.WriteLine();

        Console.WriteLine("## 表2 駒別の到達寄与（届いた台にその駒が入っていた数 ÷ その駒を含む台）");
        Console.WriteLine();
        int reachedN = reached.Count;
        int touNeed = reached.Count(r => Needed(w, r.Wins, r.DolgaTou)), shiNeed = reached.Count(r => Needed(w, r.Wins, r.DolgaShiga));
        Console.WriteLine($"固定枠の2枚は全台に入っているので、**届いた台のうちその駒 → ドルガで落ちる台の割合**を出す: トウ {touNeed:N0} ／ {reachedN:N0}（定義上 100%）・**シガ {shiNeed:N0} ／ {reachedN:N0}（{(reachedN == 0 ? "—" : (100.0 * shiNeed / reachedN).ToString("F1") + "%")}）**");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 種 | 届いた台 | 届いた組 | 全体の台（分母） | 割合 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var d in S287.Pool)
        {
            string kind = S287.ShockPool.Contains(d) ? "感電" : IsHeal(d) ? "ヒーラー" : "寿命側";
            var mine = reached.Where(r => r.Order.Contains(d)).ToList();
            int denom = boards.Count(o => o.Contains(d));
            Console.WriteLine($"| {(d == UnitCatalog.KataS3 ? "**" + d.Name + "**" : d.Name)} | {kind} | {mine.Count:N0} | {mine.Select(r => Key(r.Order)).Distinct().Count()} | {denom:N0} | {(denom == 0 ? "—" : (100.0 * mine.Count / denom).ToString("F1") + "%")} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表3 上位の台（届いた台・勝率 → 倒しT の順・上位 15）");
        Console.WriteLine();
        Console.WriteLine("| # | 席（前1・前3・中央・後1・後3） | ヒーラー | 勝率 | 倒しT | トウ → ドルガ | シガ → ドルガ |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|");
        var top = reached.OrderByDescending(r => r.Wins).ThenBy(r => r.Wins == 0 ? double.MaxValue : (double)r.WinT / r.Wins).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal).ToList();
        for (int i = 0; i < Math.Min(15, top.Count); i++)
        {
            var r = top[i];
            Console.WriteLine($"| {i + 1} | {OrderName(r.Order)} | {r.Order.Count(IsHeal)} | {F1(100.0 * r.Wins / Seeds)} | {(r.Wins == 0 ? "—" : ((double)r.WinT / r.Wins).ToString("F2"))} | {F1(100.0 * r.DolgaTou / Seeds)} | {F1(100.0 * r.DolgaShiga / Seeds)} |");
        }
        Console.WriteLine();

        // 中身: ボスは届いた台すべて（少ない）、精鋭は上位3（組が重ならないように）。届いた台が無ければ勝率の上位。
        var reps = new List<BoardRes>();
        if (w.Boss) reps.AddRange(top);
        else foreach (var r in top) { if (reps.Any(x => Key(x.Order) == Key(r.Order))) continue; reps.Add(r); if (reps.Count == 3) break; }
        if (reps.Count == 0)
        {
            foreach (var r in all.OrderByDescending(r => r.Wins).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal))
            { if (reps.Any(x => Key(x.Order) == Key(r.Order))) continue; reps.Add(r); if (reps.Count == 3) break; }
            Console.WriteLine("**届いた台は 0。** 下の中身は足切りを通った台の勝率の上位（届かない理由を読むため）。");
            Console.WriteLine();
        }
        Console.WriteLine($"## 表4 中身（{(w.Boss ? "届いた台すべて" : "上位3台")}・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 倒しT | 振 | 怖 | 霆（空） | 霆当（孤） | 霆減 | シガの与 | 放電 敵 ／ 味方 | シガが倒れたT（割合） | 感電で届いた |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        int shockReach = 0;
        foreach (var r in reps)
        {
            var d = MeasureDeep(Seat(r.Order), w, 0, Seeds);
            // 感電で届いた（測る前に固定）: 届いた台で、シガが要り、かつ雷霆が勇者（敵）に当たっている（1戦 0.5 回以上）——ベニの毒と火の台（第287期）と分ける。
            bool viaShock = r.Reach && Needed(w, r.Wins, r.DolgaShiga) && d.BoltHits * 2 >= d.N;
            if (viaShock) shockReach++;
            Console.WriteLine($"| {OrderName(r.Order)} | {F1(d.Win)} | {Per(d.WinT, d.Wins)} | {Per(d.Swings, d.N)} | {Per(d.Cower, d.N)} | {Per(d.Bolts, d.N)}（{Per(d.BoltDry, d.N)}） | {Per(d.BoltHits, d.N)}（{Per(d.BoltLone, d.N)}） | "
                + $"{Per1(d.BoltDealt, d.N)} | {Per1(d.ShigaDmg, d.N)} | {Per1(d.FoeDis, d.N)} ／ {Per1(d.AllyDis, d.N)} | {Per1(d.ShigaDeathT, d.ShigaDied)}（{F1(100.0 * d.ShigaDied / d.N)}%） | {(viaShock ? "**○**" : "")} |");
        }
        Console.WriteLine();
        if (w.Boss) Console.WriteLine($"**感電で届いた台（届いた ∧ シガが要る ∧ 雷霆が 1戦 0.5 回以上当たる）: {shockReach} 台**");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒（足切り {boards.Count:N0} 台 → 本段 {cutPass:N0} 台 → ドルガ対照 {winPass:N0} 台 × 2）。");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int fail = 0;
        void Expect(string what, bool ok, string detail = "")
        {
            Console.WriteLine($"- {(ok ? "○" : "×")} {what}{(detail.Length > 0 ? "（" + detail + "）" : "")}");
            if (!ok) fail++;
        }
        Console.WriteLine("# shiga288 check");
        Console.WriteLine();
        var g3k = new[] { TraitId.Scourge, TraitId.Shame, TraitId.Lash, TraitId.LiveWire, TraitId.ScourgeShock };
        Expect("(a) 旧の規定 `ShigaG3K` は G3K のまま（蓄電の札を持たない）・規定（第289期から）は SG-a の上に足した版（第290期から SI-b）・版は G3K の札の末尾に足しただけ",
            UnitCatalog.ShigaG3K.Traits.SequenceEqual(g3k) && UnitCatalog.Shiga.Traits.Take(6).SequenceEqual(UnitCatalog.ShigaSGa.Traits)
            && UnitCatalog.ShigaSGa.Traits.SequenceEqual(g3k.Append(TraitId.StoredCharge))
            && UnitCatalog.ShigaSGb.Traits.SequenceEqual(g3k.Append(TraitId.StoredCharge).Append(TraitId.Thunderclap))
            && UnitCatalog.ShigaSGc.Traits.SequenceEqual(g3k.Append(TraitId.StoredCharge).Append(TraitId.Thunderclap).Append(TraitId.ThunderclapLone))
            && UnitCatalog.ShigaSGcAny.Traits.SequenceEqual(UnitCatalog.ShigaSGc.Traits.Append(TraitId.ThunderclapAny)));
        Expect("(b) 版は体・Id・マイナス・フレーバーが G3K と同じ・SG-a（規定）のほかは `All` ／ `Retired` に入っていない",
            Vers.Skip(1).All(v => v.Def.Id == "shiga" && v.Def.MaxHp == 52 && v.Def.Attack == 9 && v.Def.Speed == 3 && v.Def.Pattern == AttackPattern.Sweep
                && v.Def.MinusText == UnitCatalog.ShigaG3K.MinusText && v.Def.Flavor == UnitCatalog.ShigaG3K.Flavor && !UnitCatalog.Everyone.Contains(v.Def)));   // 第290期: SG-a も規定でなくなった
        Expect("(c) 蓄電の保持者は `All` に規定のシガ1枚・雷霆の札の保持者は 0 枚",
            UnitCatalog.All.Count(u => u.Traits.Contains(TraitId.StoredCharge)) == 1 && UnitCatalog.Shiga.Traits.Contains(TraitId.StoredCharge)
            && !UnitCatalog.All.Any(u => u.Traits.Any(t => t is TraitId.Thunderclap or TraitId.ThunderclapLone or TraitId.ThunderclapAny)));

        // (d)〜(h): 責め苦 × 近衛 ／ 大隊 ／ ボス と 感電 × 近衛 × seed 0..39 で、版ごとの帳簿を突き合わせる
        var rows = new[] { ("責め苦 (トウ×シガ)", "近衛"), ("責め苦 (トウ×シガ)", "大隊"), ("責め苦 (トウ×シガ)", "ボス"), ("感電 (シガ×カタ×ソム)", "近衛"), ("感電 (シガ×カタ×ソム)", "ボス") };
        long badCap = 0, badGain = 0, aBolt = 0, bBoltAtFull = 0, bBolts = 0, bReset = 0, muted = 0, onShocked = 0, anyCasts = 0, anyDry = 0, loneB = 0, loneCOnBoss = 0, boltHitsCBoss = 0, wiredKeep = 0, wiredSeen = 0, defaultTally = 0;
        foreach (var (rn, wn) in rows)
        {
            var w = WaveOf(wn);
            var f0 = Rows288().First(r => r.Name == rn).F;
            foreach (var v in Vers)
                for (int s = 0; s < 40; s++)
                {
                    var p = BattleEngine.Materialize(SwapShiga(f0, v.Def), BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    var sh = p.First(u => u.Def.Id == "shiga");
                    var t = r.TallyByUnit["shiga"];
                    if (v.Name == "G3K") { if (t.ChargeGains + t.ChargeCapped + t.BoltCasts + t.WhipBase != 0) defaultTally++; continue; }
                    // 蓄電の増減をログから追う（増える ＝ シガへの感電の付与・減る ＝ 雷霆だけ）
                    int charge = 0, maxSeen = 0;
                    foreach (var ev in r.Events)
                        if (ev.Kind == BattleEventKind.StatusGain && ev.TargetId == sh.InstanceId && ev.Text == StatusKeys.Shock) { charge = Math.Min(StoredChargeTrait.Cap, charge + 1); maxSeen = Math.Max(maxSeen, charge); }
                    if (sh.RawCounter(StoredChargeTrait.Key) > StoredChargeTrait.Cap || maxSeen > StoredChargeTrait.Cap) badCap++;
                    if (t.ChargeGains + t.ChargeCapped != t.ShockReceived) badGain++;
                    if (v.Name == "SG-a") { aBolt += t.BoltCasts; if (sh.RawCounter(StoredChargeTrait.Key) != Math.Min(StoredChargeTrait.Cap, (int)t.ShockReceived)) badGain++; }
                    else
                    {
                        bBolts += t.BoltCasts;
                        // 蓄電の帳簿: 増えた数 − 雷霆ごとの 4 ＝ 終わりの蓄電（雷霆は蓄電 4 のときだけ撃ち、撃てば 0）
                        if (t.ChargeGains - StoredChargeTrait.Cap * t.BoltCasts == sh.RawCounter(StoredChargeTrait.Key)) bBoltAtFull += t.BoltCasts; else bReset++;
                        muted += t.BoltMuted; onShocked += t.BoltOnShocked;
                        if (v.Name == "SG-b") loneB += t.BoltLoneHits;
                        if (v.Name == "SG-c′" && t.BoltCasts > 0) { anyCasts += t.BoltCasts; anyDry += t.BoltDry; }
                        if (v.Name == "SG-c" && w.Boss) { loneCOnBoss += t.BoltLoneHits; boltHitsCBoss += t.BoltHits; }
                    }
                    // 電気鞭で感電を消しても蓄電は減らない: 電気鞭を振った戦で、雷霆が 0 なら 終わりの蓄電 ＝ min(4, 付いた数)
                    if (t.WiredSwings > 0 && t.BoltCasts == 0) { wiredSeen++; if (sh.RawCounter(StoredChargeTrait.Key) == Math.Min(StoredChargeTrait.Cap, (int)t.ShockReceived)) wiredKeep++; }
                }
        }
        Expect("(d) G3K のシガの帳簿は蓄電・雷霆・鞭の名目がすべて 0（計数の口は保持者だけ）", defaultTally == 0, $"{defaultTally} 戦");
        Expect("(e) 蓄電は上限 4 を超えない・増えた数 ＋ 上限で溜まらなかった数 ＝ シガに新しく付いた感電（書き手を問わない）", badCap == 0 && badGain == 0, $"上限超え {badCap} ／ 帳簿ずれ {badGain}");
        Expect("(f) SG-a は雷霆を撃たない・SG-b ／ SG-c の雷霆は蓄電 4 のときだけで、撃てば 0（帳簿: 増えた数 − 4 × 雷霆 ＝ 終わりの蓄電）", aBolt == 0 && bReset == 0 && bBolts > 0, $"SG-a 雷霆 {aBolt} ／ SG-b・c 雷霆 {bBolts}・帳簿ずれ {bReset}");
        Expect("(g) 電気鞭で自分の感電を消しても蓄電は減らない（電気鞭を振り雷霆の無い戦で 終わりの蓄電 ＝ min(4, 付いた数)）", wiredSeen > 0 && wiredKeep == wiredSeen, $"{wiredKeep} ／ {wiredSeen} 戦");
        Expect("(h) 雷霆の一撃は感電を起爆しない（感電している駒に当てた雷霆の後も感電が残る）", onShocked > 0 && muted == onShocked, $"{muted} ／ {onShocked}");
        Expect("(i) 孤立の2倍は SG-c だけ・ボスでは SG-c の雷霆の当たりがすべて孤立", loneB == 0 && boltHitsCBoss > 0 && loneCOnBoss == boltHitsCBoss, $"SG-b 孤立 {loneB} ／ SG-c ボス {loneCOnBoss} ／ {boltHitsCBoss}");
        Expect("(i′) 参考 SG-c′ の雷霆は感電を問わず当たった敵を打つ（空振りは当たった敵が全員倒れていたときだけ・SG-c より少ない）", anyCasts > 0 && anyDry * 10 <= anyCasts, $"雷霆 {anyCasts} ／ 空振り {anyDry}");
        // (j) 軽い口と詳しい口の勝敗が一致（版 × 3 波 × 2 台 × seed 0..19）
        int bad = 0;
        foreach (var v in Vers)
            foreach (var w in S287.Waves)
                foreach (var o in new[] { "シガ・ガルド・ベニ・トウ・ソラ", "シガ・トウ・ベニ・クビ・バン" })
                    for (int s = 0; s < 20; s++)
                    {
                        var f = SwapShiga(Seat(Order(o)), v.Def);
                        bool a = FightLite(f, w, s, out int ta);
                        var d = FightDeep(f, w, s);
                        if (a != (d.Wins == 1) || (a && ta != d.WinT)) bad++;
                    }
        Expect("(j) 軽い口（verbose なし）と詳しい口（verbose）で勝敗と決着T が一致（4 版）", bad == 0, $"{bad} 件");
        // (k) 決定性
        int nd = 0;
        foreach (var v in Vers.Skip(1))
        {
            var f = SwapShiga(Seat(Order("シガ・ガルド・ベニ・トウ・ソラ")), v.Def);
            for (int s = 0; s < 20; s++) if (FightLite(f, S287.Waves[1], s, out int t1) != FightLite(f, S287.Waves[1], s, out int t2) || t1 != t2) nd++;
        }
        Expect("(k) seed 決定的", nd == 0, $"{nd} 件");
        // (l) 新しい器具・新しい札は `ctx.PickOne` を使わない（ソースの走査）
        string root = Directory.GetCurrentDirectory();
        string tr = File.ReadAllText(Path.Combine(root, "BattleCore", "Traits.cs"));
        int i0 = tr.IndexOf("public sealed class StoredChargeTrait", StringComparison.Ordinal), i1 = tr.IndexOf("public sealed class ThunderclapLoneTrait", StringComparison.Ordinal);
        string en = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
        int j0 = en.IndexOf("void GainCharge(", StringComparison.Ordinal), j1 = en.IndexOf("/// <summary>帯電の粉が新しく感電を付けた", StringComparison.Ordinal);
        bool scanned = i0 > 0 && i1 > i0 && j0 > 0 && j1 > j0;
        Expect("(l) 蓄電・雷霆の本体は `PickOne` ／ `Roll` を呼ばない（ソースの走査）",
            scanned && !tr[i0..i1].Contains("PickOne") && !tr[i0..i1].Contains("Roll(") && !en[j0..j1].Contains("PickOne") && !en[j0..j1].Contains("Roll("));
        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "すべて ○。" : $"× が {fail} 件。");
        Environment.ExitCode = fail == 0 ? 0 : 1;
    }

    static void LogOne(string wave, string ver, string order, int seed)
    {
        var w = WaveOf(wave);
        var f = SwapShiga(Seat(Order(order)), VerOf(ver));
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# shiga288 log —— {BA.SeatsNamed(f)} × シガ {ver} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
