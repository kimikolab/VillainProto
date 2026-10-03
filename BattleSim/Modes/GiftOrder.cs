using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FK = FireKindleDiag;

// =====================================================================================
// giftorder —— 第253期「ボルグの育ちを規定に・ギフトの渡す順（ボルグ → ホタ）」。
// 指示書は design/PHASE253_GIFT_ORDER_SPEC.md ／ 報告は design/PHASE253_GIFT_ORDER.md。
// 版（§3）は札の差し替えだけ。台・波・倍率・集計は第252期（`firekindle`）の器具をそのまま使う（`FireKindleDiag` の表を呼ぶ）。
//   N0 ＝ 規定（第251期・対照・`UnitCatalog.BorgM0` / `HiyoM0` に固定）
//   N1 ＝ N0 ＋ 守るほど燃え上がる（B1）＋ 開幕の火勢（B2）＋ 溜め火（O1）＋ 渡す火（H1）
//   N2 ＝ N1 ＋ 渡す順（`GiftOrder`・2体のときボルグ → ホタ）
//   ref ＝ 第252期 M1+（参考・N1 との差は放熱で育つ（B3）だけ）
//
//     dotnet run --project BattleSim -c Release 0 giftorder phase0          # N0 ／ N1 ／ ref で2体のギフトの並びを数える（予測の材料）
//     dotnet run --project BattleSim -c Release 0 giftorder run             # 表A〜G と線（§4）
//     dotnet run --project BattleSim -c Release 0 giftorder check           # 自己検査（受け入れ 1・2・4）
//     dotnet run --project BattleSim -c Release 0 giftorder digest <版> [path]  # 版の台本の指紋（`firekindle digest` と同じ形式・N0 は第251期の規定と一致すること）
//     dotnet run --project BattleSim -c Release 0 giftorder log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]
// =====================================================================================
static partial class GiftOrderDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        _args = args;
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "digest": Digest(args.Length > 3 ? args[3] : "N0", args.Length > 4 ? args[4] : "giftorder_digest.txt"); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "N2", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("giftorder: モードは phase0 / run / check / digest / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§3）——駒は旧（第251期の規定）に固定して札を足す。
    // ---------------------------------------------------------------------------------
    static UnitDef Plus(UnitDef g, params TraitId[] tr) => FC.With(g, g.Traits.Concat(tr));
    internal static readonly UnitDef BorgN1 = Plus(UnitCatalog.BorgM0, TraitId.KindleGuard, TraitId.BlazeHoard, TraitId.KindleOpen);
    internal static readonly UnitDef HiyoN1 = Plus(UnitCatalog.HiyoM0, TraitId.GiftHoard);
    internal static readonly UnitDef HiyoN2 = Plus(HiyoN1, TraitId.GiftOrder);
    /// <summary>第252期 M1+（`FireKindleDiag` の駒をそのまま・旧の駒に固定済み）。</summary>
    internal static readonly UnitDef BorgRef = FK.BorgM1P;

    internal static readonly FB.Ver[] Versions =
    {
        new("N0", "規定（第251期・対照）", UnitCatalog.BorgM0, UnitCatalog.Hota, UnitCatalog.HiyoM0),
        new("N1", "N0 ＋ 守るほど燃え上がる ＋ 開幕の火勢 ＋ 溜め火 ＋ 渡す火", BorgN1, UnitCatalog.Hota, HiyoN1),
        new("N2", "N1 ＋ 渡す順（2体のギフトはボルグ → ホタ）", BorgN1, UnitCatalog.Hota, HiyoN2),
        new("ref", "第252期 M1+（N1 ＋ 放熱で育つ）", BorgRef, UnitCatalog.Hota, FK.HiyoH1),
    };
    internal static FB.Ver VerOf(string name) => Versions.First(v => v.Name == name);

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var f = FK.Apply(FB.Dec(seats), VerOf(ver));
        var (r, _, _) = FC.Fight(f, wave, FK.IsTarget(wave) ? EnemyScaleRule.None : BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {FK.WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    /// <summary>波のまとまり（表の列）: 九体の波（新兵・農兵）× 200/200・400/300 を主に。</summary>
    static readonly (string Name, (int W, int S)[] Cells)[] Groups =
    {
        ("九/新兵 400/300", new[] { (BA.MainWave, 1) }),
        ("九/新兵 200/200", new[] { (BA.MainWave, 0) }),
        ("九/農兵 400/300", new[] { (BA.MainWave + 1, 1) }),
        ("九/農兵 200/200", new[] { (BA.MainWave + 1, 0) }),
        ("本編 400/300", new[] { (0, 1), (1, 1), (2, 1), (3, 1) }),
        ("重い波 400/300", new[] { (FC.WaveHeavy, 1) }),
        ("的・一（8T）", new[] { (FC.WaveTarget1, 0) }),
        ("的・九（8T）", new[] { (FC.WaveTarget9, 0) }),
    };
    /// <summary>線 2 のセル（九体の波 × 200/200・400/300）。</summary>
    internal static readonly (int W, int S)[] NineCells = { (BA.MainWave, 0), (BA.MainWave, 1), (BA.MainWave + 1, 0), (BA.MainWave + 1, 1) };
    internal static readonly string[] LineBoards = { "T3-244", "T3-238" };

    static void Setup(FB.Ver[] vers, bool refs)
    {
        FK._vers = vers;
        FK.BoardNames = FK.FixedBoards;
        FK.Groups = Groups;
        FK._cells = new();
        foreach (string bn in FK.BoardNames) foreach (var v in vers) FK.Put(bn, v.Name, FK.BoardOf(bn, v, null));
        if (refs) foreach (var (rn, rf) in FK.Refs) FK.Put(rn, FK.RefVer, rf());
        // 表C・D は台本を読み直す（`FireKindleDiag.Measure` の集計は2体の並びを持たない）
        _order = new();
        foreach (string bn in FK.BoardNames)
            foreach (var v in vers)
                for (int w = 0; w < FK.WaveNames.Length; w++)
                    for (int s = 0; s < (FK.IsTarget(w) ? 1 : BA.Scales.Length); s++)
                        _order[(bn, v.Name, w, s)] = MeasureOrder(FK.BoardOf(bn, v, null), w, FK.IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc);
    }

    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = new[] { VerOf("N0"), VerOf("N1"), VerOf("ref") };
        Setup(vers, false);
        Console.WriteLine("# 第253期 Phase 0 —— N0 ／ N1 ／ ref で、2体のギフトの並びと爆炎の後の焼き尽くすを数える（渡す順の札は使わない）");
        Console.WriteLine();
        foreach (string bn in FK.BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(FK.BoardOf(bn, vers[0], null))}");
        Console.WriteLine();
        TableC(); TableD();
        FK.TableB(); FK.TableG();
        TableLine(vers.Select(v => v.Name).ToArray());
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    static void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Setup(Versions, true);
        Console.Error.WriteLine($"  測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("# 第253期 —— ボルグの育ちを規定に・ギフトの渡す順（版 N0〜N2 ＋ ref）");
        Console.WriteLine();
        Console.WriteLine("## 台（席は版に依らない・版はボルグ・ヒヨの札だけを差し替える）");
        Console.WriteLine();
        foreach (string bn in FK.BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(FK.BoardOf(bn, Versions[0], null))}");
        foreach (var (rn, rf) in FK.Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        foreach (var v in Versions) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        TableLine(Versions.Select(v => v.Name).ToArray());
        FK.TableA(); FK.TableA2();
        Console.WriteLine("（以下の表B は第252期の表B と同じ集計。列は九体の波を主にした）");
        Console.WriteLine();
        FK.TableB();
        TableC(); TableD();
        Console.WriteLine("（表E ＝ 第252期の表E。表F ＝ 第252期の表G（ホタの焼き尽くす）。ギフトの相手の内訳は第252期の表D）");
        Console.WriteLine();
        FK.TableE(); FK.TableG(); FK.TableD(); FK.TableC();
        FK.TableDeath();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    // ---------------------------------------------------------------------------------
    // 線（§4）
    // ---------------------------------------------------------------------------------
    internal static double MainWin(string b, string v) => FK.MainWaves.Average(w => FK.WinP(FK.At(b, v, w, 1).C));
    internal static long NineBlazes(string b, string v) => NineCells.Sum(c => FK.At(b, v, c.W, c.S).M.Blazes);
    internal static long NineN(string b, string v) => NineCells.Sum(c => FK.At(b, v, c.W, c.S).M.N);

    static void TableLine(string[] vers)
    {
        Console.WriteLine("## 表G —— 線（§4）の数字");
        Console.WriteLine();
        Console.WriteLine("線1 ＝ 本編の第2〜5波 × 400/300 の勝率の平均が N0 より 1.0pt 以上下がらない。線2 ＝ 九体の波（新兵・農兵）× 200/200・400/300 の4セルの爆炎の合計（/戦）が N0 より多い。**どちらも T3-244 と T3-238 の両方で**。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 本編 400/300 勝率（N0 との差） | 線1 | 九体 4セルの爆炎/戦（合計 回） | 線2 |");
        Console.WriteLine("|---|---|---|:-:|---|:-:|");
        foreach (string b in LineBoards)
        {
            double w0 = MainWin(b, "N0"); long z0 = NineBlazes(b, "N0");
            foreach (string v in vers)
            {
                double wv = MainWin(b, v); long zv = NineBlazes(b, v);
                bool l1 = wv - w0 > -1.0 - 1e-9, l2 = zv > z0;
                Console.WriteLine($"| {b} | {v} | {wv:F2}（{wv - w0:+0.00;-0.00;0.00}） | {(v == "N0" ? "—" : l1 ? "○" : "×")} | {FK.Per(zv, NineN(b, v))}（{zv}） | {(v == "N0" ? "—" : l2 ? "○" : "×")} |");
            }
        }
        Console.WriteLine();
        foreach (string v in vers.Where(x => x is "N1" or "N2"))
        {
            bool l1 = LineBoards.All(b => MainWin(b, v) - MainWin(b, "N0") > -1.0 - 1e-9);
            bool l2 = LineBoards.All(b => NineBlazes(b, v) > NineBlazes(b, "N0"));
            Console.WriteLine($"- {v}: 線1 {(l1 ? "○" : "×")} ／ 線2 {(l2 ? "○" : "×")}");
        }
        Console.WriteLine();
    }
}
