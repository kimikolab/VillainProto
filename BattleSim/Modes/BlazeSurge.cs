using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FK = FireKindleDiag;
using GO = GiftOrderDiag;

// =====================================================================================
// blazesurge —— 第254期「爆炎で火勢を一気に上げる（ヒヨとホタに見せ場を回す）」。
// 指示書は design/PHASE254_BLAZE_SURGE_SPEC.md ／ 報告は design/PHASE254_BLAZE_SURGE.md。
// 版（§3）は札の差し替えだけで作り、`Run` の引数は増やさない。台・波・倍率は第253期（`giftorder`）と同じ。
//   V0 ＝ 規定（第253期・対照）
//   V2 ＝ V0 ＋ 爆炎・上げ2（`BlazeSurge2`・当たった敵と味方の火勢 +2）
//   V4 ＝ V0 ＋ 爆炎・上げ満（`BlazeSurgeMax`・当たった敵と味方の火勢を 4 に）
//
//     dotnet run --project BattleSim -c Release 0 blazesurge phase0          # Q0-1〜Q0-3（V0 で台を数える・予測の材料）
//     dotnet run --project BattleSim -c Release 0 blazesurge run             # 表A〜H
//     dotnet run --project BattleSim -c Release 0 blazesurge check           # 自己検査（受け入れ 1〜4）
//     dotnet run --project BattleSim -c Release 0 blazesurge digest <版> [path]  # 版の台本の指紋（`giftorder digest` と同じ形式・V0 は第253期の規定と一致すること）
//     dotnet run --project BattleSim -c Release 0 blazesurge log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]
// =====================================================================================
static partial class BlazeSurgeDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "digest": Digest(args.Length > 3 ? args[3] : "V0", args.Length > 4 ? args[4] : "blazesurge_digest.txt"); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "V4", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("blazesurge: モードは phase0 / run / check / digest / log。");
                return;
        }
    }
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§3）——駒は今の規定（第253期）に札を足す。
    // ---------------------------------------------------------------------------------
    static UnitDef Plus(UnitDef g, params TraitId[] tr) => FC.With(g, g.Traits.Concat(tr));
    internal static readonly UnitDef BorgV2 = Plus(UnitCatalog.Borg, TraitId.BlazeSurge2);
    internal static readonly UnitDef BorgV4 = Plus(UnitCatalog.Borg, TraitId.BlazeSurgeMax);

    internal static readonly FB.Ver[] Versions =
    {
        new("V0", "規定（第253期・対照）", UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("V2", "V0 ＋ 爆炎・上げ2（当たった敵と味方の火勢 +2）", BorgV2, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("V4", "V0 ＋ 爆炎・上げ満（当たった敵と味方の火勢を 4 に）", BorgV4, UnitCatalog.Hota, UnitCatalog.Hiyo),
    };
    internal static FB.Ver VerOf(string name) => Versions.First(v => v.Name == name);

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var f = FK.Apply(FB.Dec(seats), VerOf(ver));
        var (r, _, _) = FC.Fight(f, wave, FK.IsTarget(wave) ? EnemyScaleRule.None : BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {FK.WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    /// <summary>波のまとまり（表の列）。</summary>
    internal static readonly (string Name, (int W, int S)[] Cells)[] Groups =
    {
        ("九/新兵 400/300", new[] { (BA.MainWave, 1) }),
        ("九/新兵 200/200", new[] { (BA.MainWave, 0) }),
        ("九/農兵 400/300", new[] { (BA.MainWave + 1, 1) }),
        ("本編 400/300", new[] { (0, 1), (1, 1), (2, 1), (3, 1) }),
        ("本編 200/200", new[] { (0, 0), (1, 0), (2, 0), (3, 0) }),
        ("重い波 400/300", new[] { (FC.WaveHeavy, 1) }),
        ("的・一（8T）", new[] { (FC.WaveTarget1, 0) }),
        ("的・九（8T）", new[] { (FC.WaveTarget9, 0) }),
    };

    internal static Dictionary<(string B, string V, int W, int S), SAgg> _s = new();
    internal static Dictionary<(string B, string V, int W, int S), GO.OAgg> _o = new();
    static FB.Ver[] _vers = Versions;
    internal static SAgg SAt(string b, string v, int w, int s) => _s[(b, v, w, FK.IsTarget(w) ? 0 : s)];

    static void Setup(FB.Ver[] vers, bool refs)
    {
        _vers = vers;
        FK._vers = vers;
        FK.BoardNames = FK.FixedBoards;
        FK.Groups = Groups;
        FK._cells = new();
        _s = new(); _o = new();
        foreach (string bn in FK.BoardNames)
            foreach (var v in vers) PutAll(bn, v.Name, FK.BoardOf(bn, v, null));
        if (refs) foreach (var (rn, rf) in FK.Refs) PutAll(rn, FK.RefVer, rf());
    }

    static void PutAll(string b, string vn, Formation f)
    {
        for (int w = 0; w < FK.WaveNames.Length; w++)
            for (int s = 0; s < (FK.IsTarget(w) ? 1 : BA.Scales.Length); s++)
            {
                var (cell, sa, oa) = Measure(f, w, FK.IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc);
                FK._cells[(b, vn, w, s)] = cell; _s[(b, vn, w, s)] = sa; _o[(b, vn, w, s)] = oa;
            }
    }

    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = new[] { VerOf("V0") };
        Setup(vers, false);
        Console.WriteLine("# 第254期 Phase 0 —— V0（＝ 規定）で、爆炎の瞬間の火勢とギフトの中の順を数える（上げの札は使わない）");
        Console.WriteLine();
        foreach (string bn in FK.BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(FK.BoardOf(bn, vers[0], null))}");
        Console.WriteLine();
        TableQ02(); TableF(); TableB(); TableC(); TableD(); TableE(); TableG(); TableH();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    static void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Setup(Versions, true);
        Console.Error.WriteLine($"  測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("# 第254期 —— 爆炎で火勢を一気に上げる（版 V0 ／ V2 ／ V4）");
        Console.WriteLine();
        Console.WriteLine("## 台（席は版に依らない・版はボルグの札だけを差し替える）");
        Console.WriteLine();
        foreach (string bn in FK.BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(FK.BoardOf(bn, Versions[0], null))}");
        foreach (var (rn, rf) in FK.Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        foreach (var v in Versions) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        FK.TableA(); FK.TableA2();
        TableQ02(); TableB(); TableC(); TableD(); TableE(); TableF(); TableG(); TableH();
        Console.WriteLine("（以下は第252・253期と同じ集計: 爆炎の回数（第252期の表B）・ボルグの火勢（表C）・ギフトの相手（表D）・回復（表E）・ホタ（表G））");
        Console.WriteLine();
        FK.TableB(); FK.TableC(); FK.TableD(); FK.TableE(); FK.TableG();
        FK.TableDeath();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    // ---------------------------------------------------------------------------------
    // digest（`giftorder digest` と同じ形式・同じ台と波。版の駒を当てるだけ）
    // ---------------------------------------------------------------------------------
    static void Digest(string ver, string outPath)
    {
        var v = VerOf(ver);
        var boards = new (string Name, Formation F)[]
        {
            ("T3-244", FK.Apply(FC.T3244, v)), ("T3-238", FK.Apply(FC.T3238, v)), ("雷＋ボルグ", FK.Apply(FC.ThunderBorg, v)),
        };
        var scales = new[] { new EnemyScaleRule(200, 200), new EnemyScaleRule(400, 300), new EnemyScaleRule(115, 115) };
        var lines = new List<string>();
        foreach (var (bn, f) in boards)
            for (int w = 0; w < 9; w++)
                for (int s = 0; s < (w >= 7 ? 1 : scales.Length); s++)
                    for (int seed = 0; seed < 6; seed++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, w >= 7 ? EnemyScaleRule.None : scales[s])(), seed, verbose: true);
                        lines.Add($"## {bn} 波{w} 倍率{s} seed{seed} won={r.PlayerWon} T={r.Turns}");
                        foreach (var l in r.Log) lines.Add(l.Text);
                        foreach (var e in r.Events)
                            lines.Add($"E {e.Kind} {e.Turn} {e.ActorId} {e.TargetId} {e.Amount} {e.HpAfter} {e.Slot} {e.Text} {e.TickIndex} {e.TickCount} {e.BrittleExtra}");
                    }
        File.WriteAllLines(outPath, lines);
        Console.WriteLine($"digest {ver}: {lines.Count} 行を {outPath} に書いた");
    }
}
