using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BS = BlazeSurgeDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FF = FireFinishDiag;
using FK = FireKindleDiag;

// =====================================================================================
// foesurge —— 第257期「爆炎で敵だけの火勢を上げる（敵陣を火薬庫に）」。
// 指示書は design/PHASE257_FOE_SURGE_SPEC.md ／ 報告は design/PHASE257_FOE_SURGE.md。
// 版（§3）は札の差し替えだけで作り、`Run` の引数は増やさない。**燃焼の規則は今の規定（第256期・被弾の燃焼 H-分担）**——
// 第233〜255期の器具と違って `EmberRule.Pre256` には固定しない。
//   W0  ＝ 規定（第256期・対照）
//   W2  ＝ W0 ＋ 爆炎・敵上げ2（`BlazeFoeSurge2`・当たった敵だけ +2）
//   W4  ＝ W0 ＋ 爆炎・敵上げ満（`BlazeFoeSurgeMax`・当たった敵だけ 4 に）
//   ref ＝ W0 ＋ 第254期の爆炎・上げ2（`BlazeSurge2`・敵と味方を +2・参考）
//
//     dotnet run --project BattleSim -c Release 0 foesurge phase0            # Q0-2・Q0-3（W0 で台を数える）
//     dotnet run --project BattleSim -c Release 0 foesurge run               # 表A〜G
//     dotnet run --project BattleSim -c Release 0 foesurge check             # 自己検査（受け入れ 2〜4）
//     dotnet run --project BattleSim -c Release 0 foesurge digest <版> [path]  # 版の台本の指紋（W0 は実装の前と一致すること）
//     dotnet run --project BattleSim -c Release 0 foesurge log <版> <台> [seed] [波 0〜8] [倍率 0/1/2]
// =====================================================================================
static partial class FoeSurgeDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "digest": Digest(args.Length > 3 ? args[3] : "W0", args.Length > 4 ? args[4] : "foesurge_digest.txt"); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "W4", args.Length > 4 ? args[4] : "T3-244", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("foesurge: モードは phase0 / run / check / digest / log。");
                return;
        }
    }
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§3）——ボルグの札だけを差し替える。
    // ---------------------------------------------------------------------------------
    static UnitDef Plus(UnitDef g, params TraitId[] tr) => FC.With(g, g.Traits.Concat(tr));
    internal static readonly UnitDef BorgW2 = Plus(UnitCatalog.BorgW0, TraitId.BlazeFoeSurge2);
    internal static readonly UnitDef BorgW4 = Plus(UnitCatalog.BorgW0, TraitId.BlazeFoeSurgeMax);
    internal static readonly UnitDef BorgRef = Plus(UnitCatalog.BorgW0, TraitId.BlazeSurge2);

    internal static readonly FB.Ver[] Versions =
    {
        new("W0", "規定（第256期・対照）", UnitCatalog.BorgW0, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("W2", "W0 ＋ 爆炎・敵上げ2（当たった敵だけ火勢 +2）", BorgW2, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("W4", "W0 ＋ 爆炎・敵上げ満（当たった敵だけ火勢を 4 に）", BorgW4, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("ref", "W0 ＋ 第254期の爆炎・上げ2（敵と味方を +2・参考）", BorgRef, UnitCatalog.Hota, UnitCatalog.Hiyo),
    };
    internal static FB.Ver VerOf(string name) => Versions.First(v => v.Name == name);

    // ---------------------------------------------------------------------------------
    // 台（§5.1）——席は版に依らない。T3-255 ／ 混ぜ-255 は第255期の段2 の「H-分担選」の席そのまま。
    // ---------------------------------------------------------------------------------
    internal const string T3255Seats = "hiyo,dolga,borg,nomi,hota";
    internal const string Mix255Seats = "yomi,beni,borg,hiyo,hane";
    internal static readonly string[] Boards = { "T3-244", "T3-238", "T3-255", "混ぜ-255", "雷＋ボルグ" };
    internal static Formation BaseOf(string name) => name switch
    {
        "T3-244" => FC.T3244,
        "T3-238" => FC.T3238,
        "T3-255" => FB.Dec(T3255Seats),
        "混ぜ-255" => FB.Dec(Mix255Seats),
        "雷＋ボルグ" => FC.ThunderBorg,
        _ => throw new ArgumentException(name),
    };
    internal static Formation BoardOf(string name, FB.Ver v) => FK.Apply(BaseOf(name), v);

    static EnemyScaleRule ScOf(int w, int s) => FK.IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc;

    static void LogOne(string ver, string board, int seed, int wave, int sc)
    {
        var f = BoardOf(board, VerOf(ver));
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, FC.WaveOf(wave, ScOf(wave, sc))(), seed, verbose: true);
        Console.WriteLine($"# {ver} × {board}（{BA.SeatsNamed(f)}）× {FK.WaveNames[wave]} × {(FK.IsTarget(wave) ? "—" : BA.Scales[sc].Name)} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    // ---------------------------------------------------------------------------------
    // 測定（1戦を1回だけ回し、第246・249・252・254期の集計とこの期の集計に同じ結果を流す）
    // ---------------------------------------------------------------------------------
    internal static Dictionary<(string B, string V, int W, int S), XAgg> _x = new();
    internal static XAgg XAt(string b, string v, int w, int s) => _x[(b, v, w, FK.IsTarget(w) ? 0 : s)];
    static FB.Ver[] _vers = Versions;

    static (FK.Cell Cell, BS.SAgg S, XAgg X) Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var cs = new FC.CAgg[seeds]; var ks = new FF.KAgg[seeds]; var ms = new FK.MAgg[seeds]; var ss = new BS.SAgg[seeds]; var xs = new XAgg[seeds];
        bool target = FK.IsTarget(w);
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = FC.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true);
            var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
            var c = new FC.CAgg(); c.Take(r, p, e, target); cs[i] = c;
            var k = new FF.KAgg(); k.Take(r, p, e, target, slot0); ks[i] = k;
            var m = new FK.MAgg(); m.Take(r, p, e, target); ms[i] = m;
            var s = new BS.SAgg(); s.Take(r, p, e, target); ss[i] = s;
            var x = new XAgg(); x.Take(r, p, e, target); xs[i] = x;
        });
        var C = new FC.CAgg(); var K = new FF.KAgg(); var M = new FK.MAgg(); var S = new BS.SAgg(); var X = new XAgg();
        for (int i = 0; i < seeds; i++) { C.Merge(cs[i]); K.Merge(ks[i]); M.Merge(ms[i]); S.Merge(ss[i]); X.Merge(xs[i]); }
        return (new FK.Cell(C, K, M), S, X);
    }

    static void Setup(FB.Ver[] vers)
    {
        _vers = vers;
        FK._vers = vers; BS._vers = vers;
        FK.BoardNames = Boards;
        FK.Groups = BS.Groups;
        FK._cells = new(); BS._s = new(); _x = new();
        void Put(string b, string vn, Formation f)
        {
            for (int w = 0; w < FK.WaveNames.Length; w++)
                for (int s = 0; s < (FK.IsTarget(w) ? 1 : BA.Scales.Length); s++)
                {
                    var (cell, sa, xa) = Measure(f, w, ScOf(w, s));
                    FK._cells[(b, vn, w, s)] = cell; BS._s[(b, vn, w, s)] = sa; _x[(b, vn, w, s)] = xa;
                }
        }
        foreach (string bn in Boards) foreach (var v in vers) Put(bn, v.Name, BoardOf(bn, v));
        foreach (var (rn, rf) in FK.Refs) Put(rn, FK.RefVer, rf());
    }

    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Setup(new[] { VerOf("W0") });
        Console.WriteLine("# 第257期 Phase 0 —— W0（＝ 規定・第256期）で台を数える（敵上げの札は使わない）");
        Console.WriteLine();
        foreach (string bn in Boards) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BoardOf(bn, Versions[0]))}");
        Console.WriteLine();
        TableQ2(); FK.TableA();
        TableB(); TableC(); TableD(); BS.TableF(); BS.TableE(); TableF(); BS.TableH();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose・燃焼の規則は規定）");
    }

    static void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Setup(Versions);
        Console.Error.WriteLine($"  測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("# 第257期 —— 爆炎で敵だけの火勢を上げる（版 W0 ／ W2 ／ W4 ／ ref）");
        Console.WriteLine();
        Console.WriteLine("## 台（席は版に依らない・版はボルグの札だけを差し替える）");
        Console.WriteLine();
        foreach (string bn in Boards) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BoardOf(bn, Versions[0]))}");
        foreach (var (rn, rf) in FK.Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        foreach (var v in Versions) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        FK.TableA(); FK.TableA2();
        TableQ2(); TableB(); TableC(); TableD(); TableD2(); BS.TableF(); BS.TableE(); TableF(); BS.TableH();
        FK.TableDeath();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose・燃焼の規則は規定）");
    }

    // ---------------------------------------------------------------------------------
    // digest（版の駒を当てるだけ・燃焼の規則は規定）
    // ---------------------------------------------------------------------------------
    static void Digest(string ver, string outPath)
    {
        var v = VerOf(ver);
        var lines = new List<string>();
        foreach (string bn in Boards)
        {
            var f = BoardOf(bn, v);
            for (int w = 0; w < 9; w++)
                for (int s = 0; s < (FK.IsTarget(w) ? 1 : BA.Scales.Length); s++)
                    for (int seed = 0; seed < 6; seed++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, ScOf(w, s))(), seed, verbose: true);
                        lines.Add($"## {bn} 波{w} 倍率{s} seed{seed} won={r.PlayerWon} T={r.Turns}");
                        foreach (var l in r.Log) lines.Add(l.Text);
                        foreach (var e in r.Events)
                            lines.Add($"E {e.Kind} {e.Turn} {e.ActorId} {e.TargetId} {e.Amount} {e.HpAfter} {e.Slot} {e.Text} {e.TickIndex} {e.TickCount} {e.BrittleExtra}");
                    }
        }
        File.WriteAllLines(outPath, lines);
        Console.WriteLine($"digest {ver}: {lines.Count} 行を {outPath} に書いた");
    }
}
