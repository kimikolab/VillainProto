using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// chiri429 —— 第429期【規定化の期】導火線のチリ ＝ 第428期の BM-emx を `All` に・鱗のウロを退役・爆弾の表示専用の出来事。
// 指示書は design/PHASE429_CHIRI_SETTLE_SPEC.md ／ 報告は design/PHASE429_CHIRI_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 chiri429 verify   # §1-8: 「試遊・導火線 チリ」と耐久（中央）× 7 波・seed 0..199 を規定のチリと BM-emx で回し、全戦一致を確かめる
//     dotnet run --project BattleSim -c Release 0 chiri429 events   # §2: 台本の爆弾の出来事の件数と計数（UnitTally）の突き合わせ・verbose の有無
//     dotnet run --project BattleSim -c Release 0 chiri429 check    # 自己検査
// =====================================================================================
static class Chiri429Diag
{
    internal const string PlayRow = "試遊・導火線 チリ";
    static readonly I.W[] Seven = L.Mains.Concat(new[] { I.Guard, I.Bat }).ToArray();
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    static Formation Play => Presets.Versions.First(r => r.Name == PlayRow).F;
    static Formation Swap(Formation f, UnitDef from, UnitDef to)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (ReferenceEquals(o.Def, from)) g[o.Slot] = to;
        return g;
    }
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    static string F1(double x) => x.ToString("F1");

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "verify": Verify(); break;
            case "events": Events(); break;
            case "check": Check(); break;
            default: Console.WriteLine("chiri429: モードは verify / events / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>規定のチリの台と、同じ並びで BM-emx に替えた台を同じ seed で回し、勝敗 ／ T ／ 爆弾の計数が全戦一致するか。</summary>
    static (double Win, bool Same) Pair(Formation def, I.W w, int n = 200)
    {
        var emx = Swap(def, UnitCatalog.Chiri, UnitCatalog.ChiriBMemx);
        var won = new bool[n]; var ok = new bool[n];
        Parallel.For(0, n, s =>
        {
            var (a, _, _) = I.Raw(def, w, s, census: false);
            var (b, _, _) = I.Raw(emx, w, s, census: false);
            var ta = a.TallyByUnit.GetValueOrDefault("chiri"); var tb = b.TallyByUnit.GetValueOrDefault("chiri");
            won[s] = a.PlayerWon;
            ok[s] = a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && (ta?.Bm426BlastFoe ?? -1) == (tb?.Bm426BlastFoe ?? -1)
                    && (ta?.Bm427MirrorDealt ?? -1) == (tb?.Bm427MirrorDealt ?? -1);
        });
        return (100.0 * won.Count(x => x) / n, ok.All(x => x));
    }

    static void Verify()
    {
        Console.WriteLine("# 第429期 §1-8 —— 規定のチリ と 第428期の BM-emx の突き合わせ（7 波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Seven.Select(w => w.Name)) + " | BM-emx と全戦一致 |");
        Console.WriteLine("|---|" + string.Concat(Seven.Select(_ => "--:|")) + ":-:|");
        foreach (var (name, f) in new[] { (PlayRow, Play), ("耐久 (ガルド×リリ)・中央", Put(Row("耐久 (ガルド×リリ)"), 2, UnitCatalog.Chiri)) })
        {
            var rs = Seven.Select(w => Pair(f, w)).ToArray();
            Console.WriteLine($"| {name} | " + string.Join(" | ", rs.Select(r => F1(r.Win))) + $" | {(rs.All(r => r.Same) ? "○" : "**×**")} |");
        }
        Console.WriteLine();
        Console.WriteLine("第428期 §3-1 の値: 礫（ウロの席）× BM-emx ＝ 本編 100 ×5 ／ 近衛 93.0 ／ 大隊 30.0・耐久 × BM-emx ＝ 本編 100 ×5 ／ 近衛 100 ／ 大隊 100。");
    }

    /// <summary>§2: 1 戦の台本の爆弾の出来事を数え、計数と突き合わせる。</summary>
    static (int Plant, int Fuse, int Blast, int Mirror, int Dud, UnitTally T) Count(Formation f, I.W w, int seed)
    {
        var (r, _, _) = I.Raw(f, w, seed, verbose: true, census: false);
        int C(string t) => r.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.Text == t);
        return (C(BattleContext.BombPlantText), C(BattleContext.BombFuseText), C(BattleContext.BombBlastText), C(BattleContext.BombMirrorText), C(BattleContext.BombDudText), r.TallyByUnit["chiri"]);
    }

    static void Events()
    {
        Console.WriteLine("# 第429期 §2 —— 爆弾の表示専用の出来事");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | seed | bomb_plant ／ 仕掛けた | bomb_blast＋bomb_mirror ／ 爆ぜた | bomb_mirror ／ 向かいへ | bomb_dud ／ 不発 | bomb_fuse |");
        Console.WriteLine("|---|---|--:|---|---|---|---|--:|");
        foreach (var w in new[] { I.Guard, I.Bat, I.Boss })
            foreach (int s in new[] { 0, 1, 2 })
            {
                var c = Count(Play, w, s); var t = c.T;
                long blasts = t.Bm426BlastFoe + t.Bm426BlastAlly;
                Console.WriteLine($"| {PlayRow} | {w.Name} | {s} | {c.Plant} ／ {t.Bm426PlantFoe + t.Bm426PlantAlly} | {c.Blast + c.Mirror} ／ {blasts} | {c.Mirror} ／ {t.Bm427MirrorBlasts} | {c.Dud} ／ {t.Bm426Duds} | {c.Fuse} |");
            }
        Console.WriteLine();
        bool same = true;
        foreach (var w in Seven)
            for (int s = 0; s < 20; s++)
            {
                var (a, _, _) = I.Raw(Play, w, s, verbose: false, census: false);
                var (b, _, _) = I.Raw(Play, w, s, verbose: true, census: false);
                if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) same = false;
            }
        Console.WriteLine($"verbose の有無で勝敗 ／ T が変わらない（{PlayRow} × 7 波 × seed 0..19）: {(same ? "○" : "**×**")}");
    }

    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第429期 chiri429 check");
        Console.WriteLine();
        var c = UnitCatalog.Chiri; var x = UnitCatalog.ChiriBMemx;
        Req(c.Traits.SequenceEqual(x.Traits) && !ReferenceEquals(c, x), "規定のチリと BM-emx は同じ札の別の定義");
        Req(c.Id == "chiri" && c.Name == "導火線のチリ" && c.MaxHp == 60 && c.Attack == 3 && c.Speed == 6 && c.Flavor == "" && c.PlusText == x.PlusText && c.MinusText == x.MinusText, "Id ／ 名前 ／ HP 60 ／ 攻 3 ／ 速 6 ／ 文面は BM-emx と同じ・フレーバーは空");
        Req(c.Actions is { Count: 1 } a && a[0].Kind == ActionKind.Skill && a[0].Label == "仕掛ける", "手番は [Skill「仕掛ける」]");
        Req(UnitCatalog.All.Count == 52 && UnitCatalog.All.Contains(c) && !UnitCatalog.All.Contains(UnitCatalog.Uro), "`All` は 52 枚・チリが入りウロが抜けた");
        Req(UnitCatalog.Retired.LastOrDefault() == UnitCatalog.Uro && ReferenceEquals(UnitCatalog.UroH428, UnitCatalog.Uro), "ウロは `Retired` の末尾・`UroH428` は同じ定義");
        int uroAll = UnitCatalog.All.ToList().IndexOf(c);
        Req(uroAll == 46, "チリはウロのいた位置（`All` の 47 枚目）");
        var uroRows = CompareBuilds().Concat(CrossBuilds()).Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Uro))).Select(r => r.Name).ToArray();
        Req(uroRows.Length == 5, $"ウロを使う compare ／ 交差帯の行は旧のウロのまま 5 行（{string.Join("・", uroRows)}）");
        Req(!CompareBuilds().Concat(CrossBuilds()).Any(r => r.F.Occupied().Any(o => o.Def.Id == "chiri")), "compare ／ 交差帯にチリの行は無い");
        Req(Presets.Versions.Any(r => r.Name == PlayRow && r.F.Occupied().Any(o => ReferenceEquals(o.Def, c))) && !Presets.Versions.Any(r => r.Name == "版・導火線 チリ"), "「試遊・導火線 チリ」は規定のチリ・「版・導火線 チリ」は無い");
        Req(!VersionsH407().Any(r => r.Name == PlayRow), "鎖の終端（`VersionsH407`）は「試遊・導火線 チリ」を外す");
        Req(UnitCatalog.Everyone.Count(u => u.Id == "uro") == 1 && UnitCatalog.Everyone.Count(u => u.Id == "chiri") == 1, "`Everyone` にウロとチリが 1 枚ずつ");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
