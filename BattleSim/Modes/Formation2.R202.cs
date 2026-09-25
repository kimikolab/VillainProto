using BattleCore;
using static Common;

// form2 run202 / check202（第202期）。Phase 0 は `Formation2.P202.cs`。
//
// 列の意味:
//   P2・201   ＝ 第201期の貫き（`FormationShape.Diamond201`・多い方・同数なら 1-2）× スィド S で選び直した値（第201期の再現）
//   P2・202S  ＝ 新しい貫き（`FormationShape.Diamond`・向かいの敵）× スィド S ——**貫きだけの効き**
//   P2・202   ＝ 新しい貫き × 規定のスィド（S+反）——**第202期の規定**
//   X・201    ＝ 第201期の報告の値（X 字は変えていないので回し直さない・指示書 §4.2）

static partial class Formation2Diag
{
    /// <summary>第201期の報告の X 字の列（表A の X・選 ／ 表B の X・ガルド・選）。<b>回し直さない</b>（指示書 §4.2）。</summary>
    static readonly Dictionary<string, double> X201 = new()
    {
        ["毒 (グザ×ミオ×ラウ)"] = 96.9, ["毒+耐久 (ベニ×トウ)"] = 100.0, ["毒+ベニ+ラウ"] = 93.9, ["毒爆弾 (ラウ×ヴィオ)"] = 79.6,
        ["毒→被弾強化 (グザ×ムド)"] = 47.1, ["澱み喰い (グザ×ヴィオ)"] = 92.5, ["追撃×毒 (ハギ×グザ)"] = 96.0,
        ["反撃 (ヒサ×カド)"] = 99.4, ["燃焼 (ボルグ×ホタ)"] = 97.9, ["礫 (ガレ×ヒビ×ウロ)"] = 89.9,
        ["移動改 (バサ×ヨミ×シオ)"] = 97.2, ["責め苦 (トウ×シガ)"] = 99.4, ["駆り立て (カリ×ドルガ)"] = 99.9,
    };

    static Formation WithShape(Formation f, FormationShape shape)
    {
        var g = new Formation { Shape = shape };
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d;
        return g;
    }

    /// <summary>上下の入れ替え（中衛・上 ↔ 中衛・下 ／ X 字は前1↔前3・後1↔後3）。</summary>
    static Formation Mirror(Formation f)
    {
        var g = new Formation { Shape = f.Shape };
        for (int i = 0; i < 5; i++) g[MirrorSlot(f.Shape == FormationShape.X ? FormationShape.X : FormationShape.Diamond, i)] = f[i];
        return g;
    }

    /// <summary>帳簿（第2〜5波 × 測る帯・1戦あたり）。</summary>
    sealed class PL
    {
        public long N; public long[] Chose = new long[6]; public long Ties, Fallbacks;
        public long HitUp, HitDown, PierceSwings;   // 中衛・上 ／ 中衛・下（開戦時の席）に貫きが当たった段数
        public double[] W = new double[5];
    }

    static PL Ledger202(Formation f)
    {
        var p = new PL(); object gate = new();
        for (int st = 0; st < 5; st++)
        {
            int wins = 0; Formation en = EnemyCatalog.Stages[st].Enemy; bool led = st >= 1;
            Parallel.For(MeasSeed0, MeasSeed0 + MeasSeeds, seed =>
            {
                List<UnitState> pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                List<UnitState> en2 = BattleEngine.Materialize(en, BattleContext.EnemyTeam);
                var plStart = pl.Select(u => (U: u, u.Slot)).ToList();
                BattleResult r = BattleEngine.Run(pl, en2, seed, verbose: led);
                if (r.PlayerWon) Interlocked.Increment(ref wins);
                if (!led) return;
                int up = f.Shape.PlayableSlots[0], down = f.Shape.PlayableSlots[4];
                var startSlot = plStart.ToDictionary(t => t.U.InstanceId, t => t.Slot);
                var foeIds = en2.Select(u => u.InstanceId).ToHashSet();
                long hu = 0, hd = 0, sw = 0;
                bool curFoePierce = false;
                foreach (BattleEvent e in r.Events)
                {
                    if (e.Kind == BattleEventKind.Attack) { curFoePierce = e.Pattern == AttackPattern.Pierce && e.ActorId is int a && foeIds.Contains(a); if (curFoePierce) sw++; }
                    else if (e.Kind == BattleEventKind.TurnStart) curFoePierce = false;
                    else if (e.Kind == BattleEventKind.Damage && curFoePierce && e.Pattern == AttackPattern.Pierce && !e.FriendlyFire && !e.Relayed
                             && e.TargetId is int t && startSlot.TryGetValue(t, out int ss))
                    { if (ss == up) hu++; else if (ss == down) hd++; }
                }
                lock (gate)
                {
                    p.N++; for (int i = 0; i < 6; i++) p.Chose[i] += r.PierceChose[i];
                    p.Ties += r.PierceTies; p.Fallbacks += r.PierceFallbacks; p.HitUp += hu; p.HitDown += hd; p.PierceSwings += sw;
                }
            });
            p.W[st] = 100.0 * wins / MeasSeeds;
        }
        return p;
    }

    static partial void Run202Impl(string arg)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第202期 `form2 run202` —— パターン2の列を新しい貫きで選び直す");
        Console.WriteLine();
        Console.WriteLine("席の選び方は第201期と同じ（**選ぶ** 120 通り × 第2〜5波 × seed " + PickSeed0 + ".." + (PickSeed0 + PickSeeds - 1)
                          + " ／ **測る** seed " + MeasSeed0 + ".." + (MeasSeed0 + MeasSeeds - 1) + "・平均は第2〜5波）。");
        Console.WriteLine("`P2・201` ＝ 第201期の貫き（多い方・同数なら 1-2）× スィド S ／ `P2・202S` ＝ 新しい貫き × スィド S（**貫きだけの効き**）／ "
                          + "**`P2・202`** ＝ 新しい貫き × 規定のスィド（S+反）／ `X・201` ＝ 第201期の報告の値（回し直さない）。");
        Console.WriteLine("`上下` ＝ 最良の並び − その並びの中衛・上と中衛・下を入れ替えた並び（測る帯）。");
        Console.WriteLine();

        var mirrorRows = new List<(string Row, string Col, double Best, double Mir)>();
        var ledgerRows = new List<(string Row, string Col, Formation F)>();
        var bestRows = new List<(string Row, string Col, Formation F)>();

        (double Best, double Mir) MirrorOf(Formation f)
        {
            double b = Mean25(MeasB(f, false).W), m = Mean25(MeasB(Mirror(f), false).W);
            return (b, m);
        }

        // ---------------- 表A ----------------
        Console.WriteLine("## 表A —— 毒パ（第201期と同じ7行・パターン2の列）");
        Console.WriteLine();
        Console.WriteLine("`X・202` ＝ X 字を**規定のスィド（S+反）で選び直した**値（X 字の貫きは変えていないので、X・201 との差はスィドの版だけ）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | X・201 | X・202 | P2・201 | P2・202S | **P2・202** | P2・202 − X・202 | P2・202 − P2・201 | 前衛 201 → 202 | 上下 201 | 上下 202S | **上下 202** | 上下 X（S） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|--:|--:|--:|--:|");
        double[] sA = new double[5]; int nA = 0;
        foreach (var (name, _, _, s) in SidDiag.MainForms197())
        {
            var memS = Members(s);                                                      // スィド S
            var memR = memS.Select(u => ReferenceEquals(u, SidDiag.SidS) ? UnitCatalog.Sid : u).ToList();   // 規定（S+反）
            Pick p201 = Select(memS, FormationShape.Diamond201), p202s = Select(memS, FormationShape.Diamond), p202 = Select(memR, FormationShape.Diamond);
            Pick px = Select(memS, FormationShape.X), pxr = Select(memR, FormationShape.X);
            double x202 = Mean25(MeasB(pxr.Best, false).W);
            var m201 = MirrorOf(p201.Best); var m202s = MirrorOf(p202s.Best); var m202 = MirrorOf(p202.Best); var mx = MirrorOf(px.Best);
            mirrorRows.Add((name, "201", m201.Best, m201.Mir)); mirrorRows.Add((name, "202", m202.Best, m202.Mir)); mirrorRows.Add((name, "X", mx.Best, mx.Mir));
            ledgerRows.Add((name, "P2・201", p201.Best)); ledgerRows.Add((name, "P2・202", p202.Best));
            bestRows.Add((name, "P2・202", p202.Best));
            double x201 = X201[name];
            bool broken = name.StartsWith("追撃×毒");
            if (!broken) { sA[0] += x201; sA[1] += x202; sA[2] += m201.Best; sA[3] += m202s.Best; sA[4] += m202.Best; nA++; }
            Console.WriteLine("| " + name + (broken ? "（**読まない**）" : "") + " | " + x201.ToString("F1") + " | " + x202.ToString("F1") + " | " + m201.Best.ToString("F1") + " | " + m202s.Best.ToString("F1")
                              + " | **" + m202.Best.ToString("F1") + "** | " + Dp(m202.Best - x202) + " | " + Dp(m202.Best - m201.Best) + " | "
                              + p201.Best[3]!.Name + " → " + p202.Best[3]!.Name + " | " + Dp(m201.Best - m201.Mir) + " | " + Dp(m202s.Best - m202s.Mir)
                              + " | **" + Dp(m202.Best - m202.Mir) + "** | " + Dp(mx.Best - mx.Mir) + " |");
        }
        Console.WriteLine("| **平均（`追撃×毒` を除く " + nA + " 行）** | " + string.Join(" | ", sA.Select((v, i) => (i == 4 ? "**" : "") + (v / nA).ToString("F1") + (i == 4 ? "**" : "")))
                          + " | " + Dp((sA[4] - sA[1]) / nA) + " | " + Dp((sA[4] - sA[2]) / nA) + " | | | | | |");
        Console.WriteLine();

        // ---------------- 表B ----------------
        Console.WriteLine("## 表B —— タンクの土俵（第201期と同じ6行・パターン2の列）");
        Console.WriteLine();
        Console.WriteLine("スィドの列は 201 が S・202 が規定（S+反）。括弧は 202 でタンクが座った席。");
        Console.WriteLine();
        (string Tag, string Id, UnitDef S, UnitDef R)[] tanks =
        {
            ("ガルド", "gald", UnitCatalog.Gald, UnitCatalog.Gald), ("ゴルム", "golm", UnitCatalog.Golm, UnitCatalog.Golm),
            ("ササ", "sasa", UnitCatalog.Sasa, UnitCatalog.Sasa), ("スィド", "sid", SidDiag.SidS, UnitCatalog.Sid),
        };
        Console.WriteLine("| 行 | X・ガルド・201 | " + string.Join(" | ", tanks.Select(t => t.Tag + " 201 → **202**（席）")) + " | 上下 201 | 上下 202 | 上下 X（ガルド） |");
        Console.WriteLine("|---|--:|" + string.Concat(tanks.Select(_ => "---|")) + "---|---|--:|");
        var sB = new double[2, 4]; int nB = 0;
        foreach (string row in TankRows)
        {
            Formation x = CompareBuilds().First(r => r.Name == row).F;
            UnitDef gald = x.Occupied().First(o => o.Def.Id == "gald").Def;
            var mem = Members(x);
            var cells = new List<string>(); var up201 = new List<string>(); var up202 = new List<string>();
            for (int k = 0; k < tanks.Length; k++)
            {
                var (tag, _, dS, dR) = tanks[k];
                var memS = mem.Select(u => ReferenceEquals(u, gald) ? dS : u).ToList();
                var memR = mem.Select(u => ReferenceEquals(u, gald) ? dR : u).ToList();
                Pick a = Select(memS, FormationShape.Diamond201), b = Select(memR, FormationShape.Diamond);
                var ma = MirrorOf(a.Best); var mb = MirrorOf(b.Best);
                mirrorRows.Add((row + "・" + tag, "201", ma.Best, ma.Mir)); mirrorRows.Add((row + "・" + tag, "202", mb.Best, mb.Mir));
                ledgerRows.Add((row + "・" + tag, "P2・201", a.Best)); ledgerRows.Add((row + "・" + tag, "P2・202", b.Best));
                bestRows.Add((row, "P2・" + tag + "・202", b.Best));
                sB[0, k] += ma.Best; sB[1, k] += mb.Best;
                cells.Add(ma.Best.ToString("F1") + " → **" + mb.Best.ToString("F1") + "**（" + SeatOf(b.Best, u => ReferenceEquals(u, dR)) + "）");
                up201.Add(tag + " " + Dp(ma.Best - ma.Mir)); up202.Add(tag + " " + Dp(mb.Best - mb.Mir));
            }
            Pick px = Select(mem, FormationShape.X);
            var mx = MirrorOf(px.Best);
            mirrorRows.Add((row + "・ガルド", "X", mx.Best, mx.Mir));
            nB++;
            Console.WriteLine("| " + row + " | " + X201[row].ToString("F1") + " | " + string.Join(" | ", cells) + " | " + string.Join(" ", up201) + " | " + string.Join(" ", up202) + " | " + Dp(mx.Best - mx.Mir) + " |");
        }
        Console.WriteLine("| **平均** | " + (TankRows.Average(r => X201[r])).ToString("F1") + " | "
                          + string.Join(" | ", Enumerable.Range(0, 4).Select(k => (sB[0, k] / nB).ToString("F1") + " → **" + (sB[1, k] / nB).ToString("F1") + "**")) + " | | | |");
        Console.WriteLine();

        // ---------------- 上下の偏り ----------------
        Console.WriteLine("## 上下の偏り（最良の並び − 上下を入れ替えた並び・測る帯）");
        Console.WriteLine();
        foreach (string col in new[] { "201", "202", "X" })
        {
            var d = mirrorRows.Where(m => m.Col == col).Select(m => (m.Row, D: m.Best - m.Mir)).ToList();
            var abs = d.Select(t => Math.Abs(t.D)).OrderBy(v => v).ToList();
            Console.WriteLine("- **" + (col == "X" ? "X 字" : "P2・" + col) + "**（" + d.Count + " 並び）: |差| の中央値 " + abs[abs.Count / 2].ToString("F1")
                              + " ／ 平均 " + abs.Average().ToString("F1") + " ／ 最大 " + abs.Max().ToString("F1") + "（" + d.First(t => Math.Abs(t.D) == abs.Max()).Row + "）"
                              + " ／ 5pt を超える並び " + abs.Count(v => v > 5.0) + " ／ 10pt を超える並び " + abs.Count(v => v > 10.0));
        }
        Console.WriteLine();
        Console.WriteLine("| 並び | 上下 201 | 上下 202 | 縮んだ |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var g in mirrorRows.Where(m => m.Col != "X").GroupBy(m => m.Row))
        {
            var a = g.First(m => m.Col == "201"); var b = g.First(m => m.Col == "202");
            double da = a.Best - a.Mir, db = b.Best - b.Mir;
            Console.WriteLine("| " + g.Key + " | " + Dp(da) + " | " + Dp(db) + " | " + Dp(Math.Abs(da) - Math.Abs(db)) + " |");
        }
        Console.WriteLine();

        // ---------------- 帳簿 ----------------
        Console.WriteLine("## 帳簿（最良の並びの戦・第2〜5波 × 測る帯・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("撃つ敵のレーン別に選んだ経路（1レーン側 → 1-2 ／ 3レーン側 → 2-3 ／ 2レーンは多い方・同数は交互）。"
                          + "`反対側` は選んだ経路に生きている駒がいなくて反対へ回った回数。`上` ／ `下` ＝ 開戦時に中衛・上 ／ 中衛・下 にいた駒に敵の貫きが当たった段数。"
                          + "**第201期の貫き（P2・201）は選んだ経路を帳簿に付けない**（上・下の段数だけ）。");
        Console.WriteLine();
        Console.WriteLine("| 並び | 列 | 敵の貫き | 1レーン側 1-2 / 2-3 | 2レーン 1-2 / 2-3 | 3レーン側 1-2 / 2-3 | 同数（交互） | 反対側 | 上 | 下 |");
        Console.WriteLine("|---|---|--:|---|---|---|--:|--:|--:|--:|");
        var tot = new Dictionary<string, PL>();
        foreach (var (row, col, f) in ledgerRows)
        {
            PL q = Ledger202(f);
            double n = q.N;
            if (!tot.TryGetValue(col, out PL? t)) tot[col] = t = new PL();
            t.N += q.N; for (int i = 0; i < 6; i++) t.Chose[i] += q.Chose[i]; t.Ties += q.Ties; t.Fallbacks += q.Fallbacks; t.HitUp += q.HitUp; t.HitDown += q.HitDown; t.PierceSwings += q.PierceSwings;
            Console.WriteLine("| " + row + " | " + col + " | " + Av(q.PierceSwings, n) + " | " + Av(q.Chose[0], n) + " / " + Av(q.Chose[1], n) + " | " + Av(q.Chose[2], n) + " / " + Av(q.Chose[3], n)
                              + " | " + Av(q.Chose[4], n) + " / " + Av(q.Chose[5], n) + " | " + Av(q.Ties, n) + " | " + Av(q.Fallbacks, n) + " | " + Av(q.HitUp, n) + " | " + Av(q.HitDown, n) + " |");
        }
        foreach (var (col, t) in tot)
            Console.WriteLine("| **合計** | " + col + " | " + Av(t.PierceSwings, t.N) + " | " + Av(t.Chose[0], t.N) + " / " + Av(t.Chose[1], t.N) + " | " + Av(t.Chose[2], t.N) + " / " + Av(t.Chose[3], t.N)
                              + " | " + Av(t.Chose[4], t.N) + " / " + Av(t.Chose[5], t.N) + " | " + Av(t.Ties, t.N) + " | " + Av(t.Fallbacks, t.N) + " | " + Av(t.HitUp, t.N) + " | " + Av(t.HitDown, t.N) + " |");
        Console.WriteLine();
        if (tot.TryGetValue("P2・202", out PL? z))
        {
            long all = z.Chose.Sum();
            Console.WriteLine("- P2・202: 撃つ敵のレーンどおり（1レーン側 → 1-2・3レーン側 → 2-3）の割合 **" + Pc(z.Chose[0] + z.Chose[5], z.Chose[0] + z.Chose[1] + z.Chose[4] + z.Chose[5])
                              + "**（外れは全部 `反対側`）／ 反対側 " + Pc(z.Fallbacks, all) + " ／ 同数で交互 " + Pc(z.Ties, all));
        }
        foreach (string col in new[] { "P2・201", "P2・202" })
            if (tot.TryGetValue(col, out PL? t))
                Console.WriteLine("- " + col + ": 上 : 下 ＝ " + Av(t.HitUp, t.N) + " : " + Av(t.HitDown, t.N) + "（上の割合 " + Pc(t.HitUp, t.HitUp + t.HitDown) + "）");
        Console.WriteLine();

        // ---------------- 敵の貫きの波別 ----------------
        Console.WriteLine("## 最良の並び（P2・202・ポンが遊ぶときの初期配置）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 列 | 並び |");
        Console.WriteLine("|---|---|---|");
        foreach (var (row, col, f) in bestRows) Console.WriteLine("| " + row + " | " + col + " | " + SeatsNamed(f) + " |");
        Console.WriteLine();
        Console.WriteLine("所要 " + sw.Elapsed.TotalSeconds.ToString("F0") + " 秒");
    }

    // =================================================================================
    // check202
    // =================================================================================

    static List<string> CompareLines(Func<Formation, Formation> map, Func<Formation, bool>? only = null)
    {
        var got = new List<string>();
        foreach (var (name, f0) in CompareBuilds())
        {
            if (only is not null && !only(f0)) { got.Add(""); continue; }
            Formation f = map(f0);
            var w = new double[5];
            for (int st = 0; st < 5; st++)
            {
                int wins = 0; Formation en = EnemyCatalog.Stages[st].Enemy;
                Parallel.For(0, 200, s => { if (BattleEngine.Run(f, en, s, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                w[st] = wins / 2.0;
            }
            got.Add("| " + name + " | " + string.Join(" | ", w.Select(x => x.ToString("F1") + "%")) + " |");
        }
        return got;
    }

    static partial void Check202Impl(string arg)
    {
        Console.WriteLine("# 第202期 `form2 check202` —— 自己検査");
        Console.WriteLine();
        bool ok = true;
        string root = FindRoot();

        // (1) compare
        Console.WriteLine("## (1) `compare`: スィドを含まない行が 0 セル差分・S に戻すと第201期と一致");
        Console.WriteLine();
        string path = string.IsNullOrWhiteSpace(arg) ? "" : arg.Trim();
        if (path.Length > 0 && File.Exists(path))
        {
            var want = File.ReadAllLines(path).Where(l => l.StartsWith("| ") && l.Contains('%')).ToList();
            var now = CompareLines(f => f);
            var back = CompareLines(f => ReplaceRef(f, UnitCatalog.Sid, SidDiag.SidS));
            int n = 0, dNo = 0, dBack = 0, cells = 0;
            var moved = new List<string>();
            for (int i = 0; i < Math.Min(want.Count, now.Count); i++)
            {
                bool sid = CompareBuilds()[i].F.Occupied().Any(o => o.Def.Id == "sid");
                if (want[i].Trim() != back[i].Trim()) dBack++;
                if (sid) { if (want[i].Trim() != now[i].Trim()) moved.Add(now[i]); continue; }
                n++; if (want[i].Trim() != now[i].Trim()) dNo++;
            }
            cells = 5 * n;
            bool c1 = dNo == 0 && dBack == 0 && want.Count == now.Count;
            ok &= c1;
            Console.WriteLine("- スィドを含まない " + n + " 行（" + cells + " セル）: 差分 **" + dNo + "** 行 → " + (dNo == 0 ? "○" : "**×**"));
            Console.WriteLine("- スィドを S（`Venom`）に戻した " + back.Count + " 行: `" + path + "` と差分 **" + dBack + "** 行 → " + (dBack == 0 ? "○" : "**×**"));
            Console.WriteLine("- 動いたスィドの在席行: " + (moved.Count == 0 ? "なし" : string.Join(" ／ ", moved)));
        }
        else Console.WriteLine("- 第201期の `balance.md` を引数で渡すこと（飛ばす）");
        Console.WriteLine();

        // (2) 乱数
        Console.WriteLine("## (2) 新しい処理が乱数を引かない");
        Console.WriteLine();
        string eng = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
        int a0 = eng.IndexOf("private int " + "PickPierceLane("), a1 = a0 < 0 ? -1 : eng.IndexOf("private readonly Dictionary<int, int> _pierce" + "Alt", a0);
        string body = a0 < 0 || a1 < 0 ? "" : eng.Substring(a0, a1 - a0);
        int rolls = Count(body, "Roll(") + Count(body, "Pick" + "One(") + Count(body, "Random");
        bool r2 = body.Length > 0 && rolls == 0;
        ok &= r2;
        Console.WriteLine("- `PickPierceLane` の本文の `Roll(` / `PickOne(` / `Random`: " + rolls + " → " + (r2 ? "○" : "**×**"));
        Formation probe = Formation.BuildDiamond(UnitCatalog.Hisa, UnitCatalog.Nono, UnitCatalog.Beni, UnitCatalog.Gald, UnitCatalog.Kado);
        bool det = Enumerable.Range(0, 20).All(s =>
        {
            var x1 = BattleEngine.Run(probe, EnemyCatalog.Stages[4].Enemy, s, verbose: true);
            var x2 = BattleEngine.Run(probe, EnemyCatalog.Stages[4].Enemy, s, verbose: true);
            return x1.PlayerWon == x2.PlayerWon && x1.Turns == x2.Turns && x1.Events.Count == x2.Events.Count;
        });
        ok &= det;
        Console.WriteLine("- パターン2の戦を同じ seed で2回（20 seed）: " + (det ? "○ 一致" : "**× ずれた**"));
        Console.WriteLine();

        // (3) 台本で突き合わせる
        Console.WriteLine("## (3) パターン2の貫きが規則どおりに選ばれている（台本で突き合わせ）");
        Console.WriteLine();
        Console.WriteLine("台本から撃つ敵の席（開幕 → `Move` / `Summon`）と味方の生存・席（`Move` / `Death` / `Revive`）を追い、`Attack.PierceLane` を規則から引き直した経路と比べる。"
                          + "分母は第2〜5波 × seed 0..199 × 表A（スィド S）・表B（ガルド）の行を `ToDiamond` で並べたもの。");
        Console.WriteLine();
        var forms = new List<Formation>();
        foreach (var (_, _, _, s) in SidDiag.MainForms197()) forms.Add(WithShape(ToDiamond(s, SidDiag.SidS, s.Occupied().First(o => o.Def.Id == "beni").Def, new[] { 0, 4, 1 }), FormationShape.Diamond));
        foreach (string row in TankRows)
        {
            Formation x = CompareBuilds().First(r => r.Name == row).F;
            forms.Add(ToDiamond(x, x.Occupied().First(o => o.Def.Id == "gald").Def, null, new[] { 0, 2, 4, 1 }));
        }
        long total = 0, miss = 0, fb = 0, tie = 0, tieBad = 0, byLane1 = 0, byLane2 = 0, byLane3 = 0, unknown = 0;
        object gate = new();
        foreach (Formation f in forms)
            for (int st = 1; st < 5; st++)
            {
                Formation enemy = EnemyCatalog.Stages[st].Enemy;
                Parallel.For(0, 200, seed =>
                {
                    List<UnitState> pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    List<UnitState> en = BattleEngine.Materialize(enemy, BattleContext.EnemyTeam);
                    // InstanceId は Run の中で振られるので、開幕の席は参照で取っておいて後で引く
                    var enStart = en.Select(u => (U: u, u.Slot)).ToList();
                    var plStart = pl.Select(u => (U: u, u.Slot)).ToList();
                    var alt = new Dictionary<int, int>();
                    BattleResult r = BattleEngine.Run(pl, en, seed, verbose: true);
                    var foeSlot = enStart.ToDictionary(t => t.U.InstanceId, t => t.Slot);
                    var mine = plStart.ToDictionary(t => t.U.InstanceId, t => t.Slot);   // 生きている味方の席
                    long t0 = 0, m0 = 0, f0 = 0, ti = 0, tb = 0, l1 = 0, l2 = 0, l3 = 0, un = 0;
                    int Occ(int lane) => mine.Values.Count(s => FormationShape.Diamond.LanePath(lane).Contains(s));
                    foreach (BattleEvent e in r.Events)
                    {
                        switch (e.Kind)
                        {
                            case BattleEventKind.Summon when e.TargetId is int sid:
                                if (e.Team == BattleContext.PlayerTeam) mine[sid] = e.Slot; else foeSlot[sid] = e.Slot; break;
                            case BattleEventKind.Move when e.TargetId is int mid:
                                if (mine.ContainsKey(mid)) mine[mid] = e.Slot; else if (foeSlot.ContainsKey(mid)) foeSlot[mid] = e.Slot; break;
                            case BattleEventKind.Death when e.TargetId is int did: mine.Remove(did); break;
                            case BattleEventKind.Revive when e.TargetId is int rid && pl.Any(u => u.InstanceId == rid): mine[rid] = e.Slot; break;
                            case BattleEventKind.Attack when e.PierceLane is int lane && e.ActorId is int aid:
                                if (!foeSlot.TryGetValue(aid, out int aslot)) { un++; break; }
                                t0++;
                                int gl = FormationShape.GridLane(aslot);
                                if (gl == 1) l1++; else if (gl == 2) l2++; else l3++;
                                int o0 = Occ(0), o1 = Occ(1);
                                int want;
                                if (gl == 1) want = 0;
                                else if (gl == 3) want = 1;
                                else if (o0 != o1) want = o0 > o1 ? 0 : 1;
                                else { ti++; int k = alt.GetValueOrDefault(aid); want = k % 2; alt[aid] = k + 1; }
                                int wantOcc = want == 0 ? o0 : o1;
                                if (lane != want)
                                {
                                    if (wantOcc == 0) f0++;   // 反対側へ回った（選んだ経路が空）
                                    else { m0++; if (gl == 2 && o0 == o1) tb++; }
                                }
                                break;
                        }
                    }
                    lock (gate) { total += t0; miss += m0; fb += f0; tie += ti; tieBad += tb; byLane1 += l1; byLane2 += l2; byLane3 += l3; unknown += un; }
                });
            }
        bool c3 = total > 0 && miss == 0;
        ok &= c3;
        Console.WriteLine("- パターン2への敵の貫き " + total + " 回（撃つ敵のレーン 1 / 2 / 3 ＝ " + byLane1 + " / " + byLane2 + " / " + byLane3 + "）");
        Console.WriteLine("- 規則から引き直した経路と違う: **" + miss + "**（うち2レーンの同数・交互 " + tieBad + "）／ 反対側へ回った " + fb + " ／ 同数で交互 " + tie
                          + " ／ 撃つ敵の席が追えない " + unknown + " → " + (c3 ? "○" : "**×**"));
        Console.WriteLine();

        // (4) 表示名
        Console.WriteLine("## (4) 席札の名前（`FormationShape.SeatName`・表示だけ）");
        Console.WriteLine();
        bool n4 = Enumerable.Range(0, 5).All(i => FormationShape.Diamond.SeatName(FormationShape.Diamond.PlayableSlots[i]) == FormationShape.Diamond.FrameNames[i])
                  && Enumerable.Range(0, 9).All(s => FormationShape.X.SeatName(s) == FormationRules.SeatNames[s])
                  && FormationShape.Diamond.SummonSlots.All(s => FormationShape.Diamond.SeatName(s) == FormationRules.SeatNames[s]);
        ok &= n4;
        Console.WriteLine("- パターン2の編成の席 → " + string.Join("・", FormationShape.Diamond.PlayableSlots.Select(FormationShape.Diamond.SeatName))
                          + " ／ X 字は `SeatNames` と全席一致 ／ 召喚枠は `SeatNames` → " + (n4 ? "○" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("**総合: " + (ok ? "ok=True" : "ok=False") + "**");
    }
}
