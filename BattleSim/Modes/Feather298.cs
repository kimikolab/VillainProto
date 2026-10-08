using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// feather298 —— 第298期「ドハ DH-a の規定化（なまりを外す）＋ 戦績の帰属の修正 ＋ ミサの羽とザンの濡れ衣」。
// 指示書は design/PHASE298_MARK_FEATHER_SPEC.md ／ 報告は design/PHASE298_MARK_FEATHER.md。
//
//     dotnet run --project BattleSim -c Release 0 feather298 doha            # 段0-1: `compare` 64 行 ＋ 交差帯 12 行 × ドハ（旧 `DohaD0` ／ 規定）・(G2) の分解・弱体の供給源・doha297 の代表台（DH-a なまりあり ／ 規定）
//     dotnet run --project BattleSim -c Release 0 feather298 attr            # 段0-2: 帰属のずれ 8 群（直す前に別の駒に入っていた量 → 本当の出どころ）
//     dotnet run --project BattleSim -c Release 0 feather298 p0              # Phase 0: 標の書き込み（書き手ごと・新しい標 ／ 層）・羽の見込み・同士討ちの出どころ
//     dotnet run --project BattleSim -c Release 0 feather298 seat            # §6-2: 濡れ衣編成（ザン・ミサ・ヒサ・ボルグ・カド）の席（120 通り）
//     dotnet run --project BattleSim -c Release 0 feather298 rates [a|b] [seeds]   # §6: 代表台 × 波 × 組（C0〜C4・C3 ／ C4 のミサは引数）＋ 対照
//     dotnet run --project BattleSim -c Release 0 feather298 compare [a|b]   # §6-2: `compare` 64 行 × 組（ミサ ／ ザン在席の行・C4 はボルグ ／ カド在席の行も）・(G2)
//     dotnet run --project BattleSim -c Release 0 feather298 grid <guard|bat|boss> <組> [seed]   # §6-2: 固定枠 ヒサ ＋ ザン ＋ ミサ・探索枠2 × 席 120・C0 と組で同じ台
//     dotnet run --project BattleSim -c Release 0 feather298 memo <台の一部> <boss|guard|bat|1..5> <seed> <組>   # 台本の並び
//     dotnet run --project BattleSim -c Release 0 feather298 check           # 自己検査
// =====================================================================================
static class Feather298Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "rates";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "doha": Doha(); return;
            case "attr": Attr(); return;
            case "p0": P0(); return;
            case "seat": Seat(); return;
            case "rates": Rates(A(3, "a"), int.Parse(A(4, "200"))); return;
            case "compare": CompareRows(A(3, "a")); return;
            case "grid": Grid(A(3, "guard"), A(4, "C3"), int.Parse(A(5, "10")), A(6, "a")); return;
            case "memo": Memo(A(3, "守り型"), A(4, "guard"), int.Parse(A(5, "0")), A(6, "C1"), A(7, "a")); return;
            case "check": Check(); return;
            default: Console.WriteLine("feather298: モードは doha / attr / p0 / seat / rates / compare / grid / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 共通
    // ---------------------------------------------------------------------------------
    static Doha297Diag.Wave[] Waves() => Doha297Diag.Waves();
    static Doha297Diag.Wave WaveOf(string k) => Waves().First(w => w.Key == k);
    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static string Seats(Formation f) => string.Join("・", Enumerable.Range(0, 5).Select(i => f[i] is { } d ? Short(d) : "—"));
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation CompareRow(string n) => CompareBuilds().First(r => r.Name == n).F;

    /// <summary>濡れ衣編成（§6-2・`seat` で決めた席）: 前1 ザン ／ 前3 ボルグ ／ 中央 カド ／ 後1 ヒサ ／ 後3 ミサ。席 120 通りの勝率はすべて同じ（C0 ＝ C4 ＝ 85.7）なので、C4 の濡れ衣が最も多い席（2.12 回 ／ 戦）を採った（決めたこと）。</summary>
    internal static Formation FrameSquad => Formation.Build(front1: UnitCatalog.Zan, front3: UnitCatalog.Borg, center: UnitCatalog.Kado, back1: UnitCatalog.Hisa, back3: UnitCatalog.Tome);

    internal static (string Name, Formation F)[] Boards() => new[]
    {
        ("試遊・標 循環", Playtest("試遊・標 循環")),
        ("試遊・標 三人組", Playtest("試遊・標 三人組")),
        ("試遊・標 守り型", Playtest("試遊・標 守り型")),
        ("標経済 (ヒサ×ザン×ミサ)", CompareRow("標経済 (ヒサ×ザン×ミサ)")),
        ("見境 (ミサ×ソラ)", CompareRow("見境 (ミサ×ソラ)")),
        ("見境改 (ミサ×薙ぎ)", CompareRow("見境改 (ミサ×薙ぎ)")),
        ("濡れ衣編成（ザン・ミサ・ヒサ・ボルグ・カド）", FrameSquad),
    };

    internal sealed record Combo(string Tag, UnitDef Misa, UnitDef Zan);
    internal static Combo[] Combos(string best)
    {
        UnitDef mf = best == "b" ? UnitCatalog.MisaMFb : UnitCatalog.MisaMFa;
        string m = best == "b" ? "MF-b" : "MF-a";
        return new[]
        {
            new Combo("C0", UnitCatalog.Tome, UnitCatalog.Zan),
            new Combo("C1", UnitCatalog.MisaMFa, UnitCatalog.Zan),
            new Combo("C2", UnitCatalog.MisaMFb, UnitCatalog.Zan),
            new Combo($"C3（{m}＋ZN-a）", mf, UnitCatalog.ZanZNa),
            new Combo($"C4（{m}＋ZN-b）", mf, UnitCatalog.ZanZNb),
        };
    }
    static Combo ComboOf(string tag, string best) => Combos(best).First(c => c.Tag.StartsWith(tag, StringComparison.Ordinal));
    internal static Formation Apply(Formation f, Combo c) => FvSwap(FvSwap(f, UnitCatalog.Tome, c.Misa), UnitCatalog.Zan, c.Zan);

    // ---------------------------------------------------------------------------------
    // 1戦の集計
    // ---------------------------------------------------------------------------------
    sealed class Agg
    {
        public long N, Wins, WinT, FirstDeathN, FirstDeathT, FfDeaths, MfFoe, MfAlly, MfDealtFoe, MfDealtAlly, MfBefore, MfChain, MfHushed, HandShots, Sprays,
                    Vend, Frames, FrameDealt, VendDealt, Recoil, Rally, RallyHealed, Accuse, SoraHits, SoraTaken, SoraPresent, SoraDeathN, SoraDeathT, Turns;
        public void Add(Agg o)
        {
            foreach (var f in typeof(Agg).GetFields()) if (f.FieldType == typeof(long)) f.SetValue(this, (long)f.GetValue(this)! + (long)f.GetValue(o)!);
        }
        public double P(long x) => N == 0 ? 0 : (double)x / N;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
    }

    static Agg One(Formation f, Func<List<UnitState>> enemy, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, enemy(), seed, verbose: false);
        var a = new Agg { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var dead = p.Where(u => u.LastDeathTurn > 0).Select(u => u.LastDeathTurn).ToList();
        if (dead.Count > 0) { a.FirstDeathN = 1; a.FirstDeathT = dead.Min(); }
        var sora = p.FirstOrDefault(u => u.Def.Id == "sora");
        if (sora is not null) { a.SoraPresent = 1; if (sora.LastDeathTurn > 0) { a.SoraDeathN = 1; a.SoraDeathT = sora.LastDeathTurn; } }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!mine.Contains(id)) continue;
            a.FfDeaths += t.MfAllyKills + t.SprayAllyKills;
            a.MfFoe += t.MfShotsFoe; a.MfAlly += t.MfShotsAlly; a.MfDealtFoe += t.MfDealtFoe; a.MfDealtAlly += t.MfDealtAlly; a.MfBefore += t.MfBeforeFirstTurn;
            a.MfChain += t.MfChainSkipped; a.MfHushed += t.MfHushed; a.HandShots += t.FeatherShots; a.Sprays += t.FeatherSprayed + (t.FeatherShots == 0 ? t.SprayFoe + t.SprayAlly : 0);
            a.Vend += t.VendettaFires; a.Frames += t.FrameVendettas; a.FrameDealt += t.FrameDealt; a.VendDealt += t.VendettaDealt;
            a.Rally += t.RallyFires; a.RallyHealed += t.RallyHealed; a.Accuse += t.FrameAccuses;
            if (id == "sora") { a.SoraHits += t.MfTakenHits; a.SoraTaken += t.MfTaken; }
        }
        return a;
    }

    static Agg Many(Formation f, Func<List<UnitState>> enemy, int seeds, int seed0 = 0)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, s => parts[s] = One(f, enemy, seed0 + s));
        var a = new Agg();
        foreach (var x in parts) a.Add(x);
        return a;
    }

    /// <summary>`compare` と同じ勝率（本編の波・seed 0..199）。</summary>
    static double[] CompareRates(Formation f)
    {
        var w = new double[EnemyCatalog.Stages.Count];
        for (int i = 0; i < w.Length; i++)
        {
            int ii = i, wins = 0;
            var res = new bool[200];
            Parallel.For(0, 200, s => res[s] = BattleEngine.Run(f, EnemyCatalog.Stages[ii].Enemy, s, verbose: false).PlayerWon);
            wins = res.Count(x => x);
            w[i] = 100.0 * wins / 200;
        }
        return w;
    }

    static string Row(double[] r) => string.Join(" / ", r.Select(x => x.ToString("F1")));

    // ---------------------------------------------------------------------------------
    // 段0-1: ドハ
    // ---------------------------------------------------------------------------------
    static void Doha()
    {
        Console.WriteLine("# 第298期 段0-1 —— ドハ ＝ DH-a（なまりを外す）");
        Console.WriteLine();
        var rows = CompareBuilds().Select(r => (r.Name, F: r.F, Cross: false)).Concat(CrossBuilds().Select(r => (r.Name, F: r.F, Cross: true))).ToArray();
        // 旧 ＝ 規定のドハを `DohaD0` に（ドハのいない行は同じ）
        var oldR = new Dictionary<string, double[]>(); var newR = new Dictionary<string, double[]>();
        foreach (var (n, f, _) in rows) { newR[n] = CompareRates(f); oldR[n] = CompareRates(FvSwap(f, UnitCatalog.Doha, UnitCatalog.DohaD0)); }
        Console.WriteLine("## 動いた行（`compare` 64 行 ＋ 交差帯 12 行・seed 0..199・第1〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帯 | 旧（`DohaD0`） | 規定（DH-a・なまりなし） | 最大の差（第2〜5波） |");
        Console.WriteLine("|---|---|---|---|--:|");
        int moved = 0;
        foreach (var (n, f, cross) in rows)
        {
            var o = oldR[n]; var x = newR[n];
            if (o.SequenceEqual(x)) continue;
            moved++;
            double d = Enumerable.Range(1, 4).Select(i => x[i] - o[i]).OrderByDescending(Math.Abs).First();
            Console.WriteLine($"| {n} | {(cross ? "交差帯" : "compare")} | {Row(o)} | {Row(x)} | {d:+0.0;-0.0} |");
        }
        Console.WriteLine();
        Console.WriteLine($"動いた行 {moved}（ドハのいない行は旧と規定が同じ定義なので動かない）。");
        Console.WriteLine();
        var allOld = rows.Where(r => !r.Cross).Select(r => oldR[r.Name]).ToArray(); var allNew = rows.Where(r => !r.Cross).Select(r => newR[r.Name]).ToArray();
        Console.WriteLine("全64行の平均（第1〜5波）: 旧 " + string.Join(" / ", Enumerable.Range(0, 5).Select(i => allOld.Average(r => r[i]).ToString("F1")))
            + " → 規定 " + string.Join(" / ", Enumerable.Range(0, 5).Select(i => allNew.Average(r => r[i]).ToString("F1"))));
        Console.WriteLine();

        // (G2): いずれかの波で −10.0pt 以上落ちた行
        Console.WriteLine("## (G2) の判定（分母は `compare` 全行 ＋ 交差帯）");
        Console.WriteLine();
        var dropped = rows.Where(r => Enumerable.Range(0, 5).Any(i => newR[r.Name][i] - oldR[r.Name][i] <= -10.0)).ToList();
        if (dropped.Count == 0) Console.WriteLine("いずれかの波で −10.0pt 以上落ちた行は **0 行**。(G2) の分解の対象が無い（壊れ ／ 制約のどちらでもない）。");
        foreach (var (n, f, _) in dropped)
        {
            Console.WriteLine($"### {n}");
            Console.WriteLine();
            Console.WriteLine("| 駒 | その駒を含む他の行 | 全波平均の変化 | 判定 |");
            Console.WriteLine("|---|--:|--:|---|");
            foreach (var (_, d) in f.Occupied())
            {
                var others = rows.Where(r => r.Name != n && r.F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToList();
                if (others.Count == 0) { Console.WriteLine($"| {Short(d)} | 0 | — | 分解が成立しない |"); continue; }
                double ch = others.Average(r => newR[r.Name].Average() - oldR[r.Name].Average());
                Console.WriteLine($"| {Short(d)} | {others.Count} | {ch:+0.00;-0.00} | {(ch <= -3.0 ? "**壊れ**" : Math.Abs(ch) < 3.0 ? "制約" : "—")} |");
            }
            Console.WriteLine();
        }

        // 弱体の軸の行: 弱体の供給源（`DullByRoute`・1戦あたり・第2〜5波 × seed 0..49）
        Console.WriteLine("## 弱体の軸の行の供給源（`DullByRoute` の1戦あたり・両陣営・第2〜5波 × seed 0..49）");
        Console.WriteLine();
        Console.WriteLine("経路: " + string.Join("・", DullRoutes.Names.Select((x, i) => $"{i} {x}")));
        Console.WriteLine();
        Console.WriteLine("| 行 | ドハ | 第2〜5波 勝率 | " + string.Join(" | ", DullRoutes.Names) + " |");
        Console.WriteLine("|---|---|--:|" + string.Concat(DullRoutes.Names.Select(_ => "--:|")));
        string[] axis = { "分かち×逆しま (ドハ×ウツ)", "引き受け (ウケ×ドハ)", "渡し (ワタ×ドハ)", "弱体×被弾 (ドハ×ワタ×ヒビ)", "弱体×破片 (ウケ×ネル×ウロ)" };
        foreach (var n in axis)
        {
            var f = rows.First(r => r.Name == n).F;
            foreach (var (tag, g) in new[] { ("旧", FvSwap(f, UnitCatalog.Doha, UnitCatalog.DohaD0)), ("規定", f) })
            {
                var sum = new double[DullRoutes.Count]; int wins = 0, cnt = 0;
                for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
                    for (int s = 0; s < 50; s++)
                    {
                        var r = BattleEngine.Run(g, EnemyCatalog.Stages[w].Enemy, s, verbose: false);
                        cnt++; if (r.PlayerWon) wins++;
                        for (int k = 0; k < sum.Length; k++) sum[k] += r.DullByRoute[k];
                    }
                Console.WriteLine($"| {n} | {tag} | {100.0 * wins / cnt:F1} | " + string.Join(" | ", sum.Select(x => (x / cnt).ToString("F1"))) + " |");
            }
        }
        Console.WriteLine();
        // 弱体を読む駒の、ドハのいない行
        Console.WriteLine("## 弱体を読む駒（ウツ ／ ウケ ／ ワタ ／ クビ）の行（ドハがいる行 ／ いない行）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 行 | ドハ | 旧 | 規定 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var d in new[] { UnitCatalog.Utsu, UnitCatalog.Uke, UnitCatalog.Wata, UnitCatalog.Kubi })
            foreach (var (n, f, _) in rows.Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, d))))
                Console.WriteLine($"| {Short(d)} | {n} | {(f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Doha)) ? "○" : "—")} | {Row(oldR[n])} | {Row(newR[n])} |");
        Console.WriteLine();

        // 参考: doha297 の代表台 × DH-a（なまりあり）／ 規定（なまりなし）
        Console.WriteLine("## 参考: 第297期の代表台（`doha297 rates` の 10 台）× DH-a（なまりあり・`DohaDHa`）／ 規定（なまりなし）× seed 0..199");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 旧（`DohaD0`） | DH-a（なまりあり） | 規定（なまりなし） | 規定 − DH-a |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var (name, f) in Doha297Diag.Boards())
            foreach (var w in Waves().Where(w => w.Key != "1"))
            {
                double o = Many(f, w.Make, 200).Win, a = Many(FvSwap(f, UnitCatalog.DohaD0, UnitCatalog.DohaDHa), w.Make, 200).Win, nw = Many(FvSwap(f, UnitCatalog.DohaD0, UnitCatalog.Doha), w.Make, 200).Win;
                if (o == a && a == nw) continue;
                Console.WriteLine($"| {name} | {w.Name} | {o:F1} | {a:F1} | {nw:F1} | {nw - a:+0.0;-0.0} |");
            }
        Console.WriteLine();
        Console.WriteLine("（3つとも同じ値のセルは省いた。）");
    }

    // ---------------------------------------------------------------------------------
    // 段0-2: 帰属のずれ 8 群
    // ---------------------------------------------------------------------------------
    static readonly string[] Groups = { "", "反転（`InverseHeal` ／ `InverseSip`）", "火の変換（`FireConvert`）", "癒しの灯（`MendGlow`）", "火の癒し（`FireHeal`）", "耐火の枝（`Ember.TickHeal`）", "分かちのなまり", "橋（板 ／ 口づけ）", "くべられる火（`FeedAtk`）" };

    static UnitDef With(UnitDef d, TraitId t) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances, Pattern = d.Pattern,
        Traits = d.Traits.Append(t).ToArray(), Actions = d.Actions, PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    static (string Name, Formation F)[] AttrBoards()
    {
        var l = new List<(string, Formation)>();
        l.AddRange(CompareBuilds()); l.AddRange(CrossBuilds()); l.AddRange(Presets.Playtest);
        // 旧のドハ（なまりあり）の行
        foreach (var (n, f) in CompareBuilds().Concat(CrossBuilds()))
            if (f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Doha))) l.Add(($"{n}（旧のドハ）", FvSwap(f, UnitCatalog.Doha, UnitCatalog.DohaD0)));
        // 癒しの灯（保持者 0 枚）: 火選りの行のヒヨに札を足す
        foreach (var (n, f) in CompareBuilds().Where(r => r.Name.StartsWith("火選り", StringComparison.Ordinal)))
            l.Add(($"{n}（ヒヨ ＋ 癒しの灯）", FvSwap(f, UnitCatalog.Hiyo, With(UnitCatalog.Hiyo, TraitId.MendGlow))));
        // 橋（HS-c・保持者は版だけ）: 試遊・標 ボス台のヒサ → HS-c・バン → ツギ ／ リリ
        var bossRow = Playtest("試遊・標 ボス台");
        l.Add(("標 ボス台（ヒサ → HS-c・バン → ツギ）", FvSwap(FvSwap(bossRow, UnitCatalog.Hisa, UnitCatalog.HisaHSc), UnitCatalog.Ban, UnitCatalog.Tsugi)));
        l.Add(("標 ボス台（ヒサ → HS-c・バン → リリ）", FvSwap(FvSwap(bossRow, UnitCatalog.Hisa, UnitCatalog.HisaHSc), UnitCatalog.Ban, UnitCatalog.Lili)));
        return l.ToArray();
    }

    static void Attr()
    {
        Console.WriteLine("# 第298期 段0-2 —— 戦績の帰属のずれ 8 群");
        Console.WriteLine();
        Console.WriteLine("台: `compare` 64 行 ＋ 交差帯 12 行 ＋ 試遊 8 行 ＋ 旧のドハ（なまりあり）の行 ＋ 火選りの行のヒヨに癒しの灯 ＋ 標 ボス台に橋（HS-c）の 2 台。波: 本編 第2〜5波 ／ 近衛 ／ 大隊 ／ ボス規定形 × seed 0..9。");
        Console.WriteLine("**直す前に別の駒に入っていた量** ＝ 包む前の印が本当の出どころ以外（別の駒か誰でもない）を指していた量（`AttrFixed`）。量は回復なら回復(与)、なまり ／ くべられる火は攻撃力、橋は HP ＋ 破片。");
        Console.WriteLine();
        var boards = AttrBoards();
        var waves = Waves().Where(w => w.Key != "1").ToArray();
        var total = new long[9]; var fixd = new long[9]; var none = new long[9]; var battles = new long[9];
        var stolen = Enumerable.Range(0, 9).Select(_ => new Dictionary<string, long>()).ToArray();
        var src = Enumerable.Range(0, 9).Select(_ => new Dictionary<string, long>()).ToArray();
        var best = new (long Amt, string Where, string From, string To)[9];
        var lk = new object();
        Parallel.ForEach(boards.SelectMany(b => waves.SelectMany(w => Enumerable.Range(0, 10).Select(s => (b, w, s)))), job =>
        {
            var (b, w, s) = job;
            var p = BattleEngine.Materialize(b.F, BattleContext.PlayerTeam);
            var e = w.Make();
            var r = BattleEngine.Run(p, e, s, verbose: false);
            var names = p.Concat(e).GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => (g.First().TeamId == BattleContext.PlayerTeam ? "" : "敵 ") + Short(g.First().Def));
            lock (lk)
            {
                for (int g = 1; g <= 8; g++)
                {
                    long bf = 0; string to = "", from = "";
                    foreach (var (id, t) in r.TallyByUnit)
                    {
                        if (t.AttrTotal is { } tt) total[g] += tt[g];
                        if (t.AttrFixed is { } tf && tf[g] > 0) { fixd[g] += tf[g]; bf += tf[g]; to = names.GetValueOrDefault(id, id); src[g][to] = src[g].GetValueOrDefault(to) + tf[g]; }
                        if (t.AttrFromNone is { } tn) none[g] += tn[g];
                        if (t.AttrStolen is { } ts && ts[g] > 0) { string nm = names.GetValueOrDefault(id, id); stolen[g][nm] = stolen[g].GetValueOrDefault(nm) + ts[g]; from += (from == "" ? "" : "・") + $"{nm} {ts[g]}"; }
                    }
                    if (bf > 0) battles[g]++;
                    if (bf > best[g].Amt) best[g] = (bf, $"{b.Name} × {w.Name} × seed {s}", from == "" ? "誰でもない" : from + (none[g] > 0 ? "" : ""), to);
                }
            }
        });
        Console.WriteLine("| 群 | 中身 | 出した量の合計 | うち直す前は別の駒 ／ 誰でもない | 直す前に入っていた駒（上位） | 本当の出どころ（上位） | 起きた戦 | 1戦の例（最大）: 直す前 → 本当の出どころ |");
        Console.WriteLine("|--:|---|--:|---|---|---|--:|---|");
        string Top(Dictionary<string, long> d) => d.Count == 0 ? "—" : string.Join("・", d.OrderByDescending(x => x.Value).Take(4).Select(x => $"{x.Key} {x.Value}"));
        for (int g = 1; g <= 8; g++)
            Console.WriteLine($"| {g} | {Groups[g]} | {total[g]} | {fixd[g] - none[g]} ／ {none[g]} | {Top(stolen[g])} | {Top(src[g])} | {battles[g]} | "
                + (best[g].Amt == 0 ? "—" : $"{best[g].Where}: {best[g].From}（誰でもない ＝ 残り）→ {best[g].To} {best[g].Amt}") + " |");
        Console.WriteLine();
        Console.WriteLine($"戦の数: {boards.Length} 台 × {waves.Length} 波 × 10 seed ＝ {boards.Length * waves.Length * 10}。");
    }

    // ---------------------------------------------------------------------------------
    // Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        Console.WriteLine("# 第298期 Phase 0 —— 標の書き込みと羽の見込み（規定の駒・C0）");
        Console.WriteLine();
        var boards = Boards().Take(4).ToArray();
        var waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(WaveOf).ToArray();
        Console.WriteLine("## §5-1 標の書き込み（1ターンあたり・書き手ごと・seed 0..49）");
        Console.WriteLine();
        Console.WriteLine("書き手 ＝ 第94期の印（その標を書いた特性の持ち主）。「敵に新」＝ 標の無かった敵に新しく ／「敵に層」＝ 層の追加 ／「味方に新」＝ 味方（自分を含む）に新しく。");
        Console.WriteLine("**見込み**: MF-a の羽 ≈ 新しい標（敵 ＋ 味方・ミサ自身を除く）／ MF-b ≈ MF-a ＋ 敵の層。味方への羽 ≈ 味方への新しい標（ミサ自身を除く）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | 書き手ごと（敵に新 ／ 敵に層 ／ 味方に新・1ターンあたり） | 見込み MF-a ／ MF-b（1戦） | 味方への羽（1戦） | 実測 C1 ／ C2（1戦・敵 ＋ 味方） |");
        Console.WriteLine("|---|---|--:|--:|---|---|--:|---|");
        foreach (var (name, f) in boards)
            foreach (var w in waves)
            {
                long n = 0, turns = 0, wins = 0, fresh = 0, layer = 0, allyFresh = 0, misaSelf = 0;
                var by = new Dictionary<string, (long F, long L, long A)>();
                for (int s = 0; s < 50; s++)
                {
                    var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: false);
                    n++; turns += r.Turns; if (r.PlayerWon) wins++;
                    var mine = p.ToDictionary(u => u.Def.Id, u => Short(u.Def));
                    foreach (var (id, t) in r.TallyByUnit)
                    {
                        string nm = mine.TryGetValue(id, out var m) ? m : "敵";
                        var c = by.GetValueOrDefault(nm);
                        bool enemy = !mine.ContainsKey(id);
                        // 敵の書き手が書いた「味方に」は、敵陣営の中への標（プレイヤーから見れば敵への標）
                        by[nm] = (c.F + t.MarkWriteFoeFresh, c.L + t.MarkWriteFoeLayer, c.A + t.MarkWriteAllyFresh);
                        if (!enemy) { fresh += t.MarkWriteFoeFresh + t.MarkWriteAllyFresh; layer += t.MarkWriteFoeLayer; allyFresh += t.MarkWriteAllyFresh; }
                        else { fresh += t.MarkWriteFoeFresh + t.MarkWriteAllyFresh; allyFresh += t.MarkWriteFoeFresh; }
                        if (id == "tome") misaSelf += t.MarkWriteNoOwner;
                    }
                }
                var c1 = Many(Apply(f, ComboOf("C1", "a")), w.Make, 50); var c2 = Many(Apply(f, ComboOf("C2", "a")), w.Make, 50);
                string Per(long x) => turns == 0 ? "0" : ((double)x / turns).ToString("F2");
                Console.WriteLine($"| {name} | {w.Name} | {100.0 * wins / n:F0} | {(double)turns / n:F1} | "
                    + string.Join("・", by.Where(x => x.Value.F + x.Value.L + x.Value.A > 0).OrderByDescending(x => x.Value.F + x.Value.L + x.Value.A).Select(x => $"{x.Key} {Per(x.Value.F)} ／ {Per(x.Value.L)} ／ {Per(x.Value.A)}"))
                    + $" | {(double)fresh / n:F1} ／ {(double)(fresh + layer) / n:F1} | {(double)allyFresh / n:F1} | {c1.P(c1.MfFoe + c1.MfAlly):F1} ／ {c2.P(c2.MfFoe + c2.MfAlly):F1}（味方 {c1.P(c1.MfAlly):F1} ／ {c2.P(c2.MfAlly):F1}） |");
            }
        Console.WriteLine();

        Console.WriteLine("## §5-2 味方への羽と ZN-a の濡れ衣（C1 ／ C3 の実測・1戦あたり・seed 0..49）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | C1 味方への羽 | うちソラ | C3 濡れ衣の仇討ち | C3 ヒサの指差し |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var (name, f) in boards)
            foreach (var w in waves)
            {
                var c1 = Many(Apply(f, ComboOf("C1", "a")), w.Make, 50); var c3 = Many(Apply(f, ComboOf("C3", "a")), w.Make, 50);
                Console.WriteLine($"| {name} | {w.Name} | {c1.P(c1.MfAlly):F2} | {c1.P(c1.SoraHits):F2} | {c3.P(c3.Frames):F2} | {c3.P(c3.Accuse):F2} |");
            }
        Console.WriteLine();

        Console.WriteLine("## §5-4 ZN-b の対象（標の付いた味方への同士討ち・`compare` 64 行 × 第2〜5波 × seed 0..49・撃った駒ごと）");
        Console.WriteLine();
        var ff = new Dictionary<string, (long N, HashSet<string> Rows)>();
        long battles = 0;
        foreach (var (n, f) in CompareBuilds())
            for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
                for (int s = 0; s < 50; s++)
                {
                    var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, BattleEngine.Materialize(EnemyCatalog.Stages[w].Enemy, BattleContext.EnemyTeam), s, verbose: false);
                    battles++;
                    foreach (var u in p.GroupBy(u => u.Def.Id).Select(g => g.First()))
                        if (r.TallyByUnit.TryGetValue(u.Def.Id, out var t) && t.FfOnMarked > 0)
                        {
                            var c = ff.GetValueOrDefault(Short(u.Def), (0, new HashSet<string>()));
                            c.Rows.Add(n);
                            ff[Short(u.Def)] = (c.N + t.FfOnMarked, c.Rows);
                        }
                }
        Console.WriteLine("| 撃った駒 | 回数（全戦の合計） | 行 |");
        Console.WriteLine("|---|--:|---|");
        foreach (var (k, v) in ff.OrderByDescending(x => x.Value.N)) Console.WriteLine($"| {k} | {v.N} | {string.Join(" ／ ", v.Rows)} |");
        Console.WriteLine();
        Console.WriteLine($"戦の数 {battles}。");
    }

    // ---------------------------------------------------------------------------------
    // 濡れ衣編成の席
    // ---------------------------------------------------------------------------------
    static void Seat()
    {
        var five = new[] { UnitCatalog.Zan, UnitCatalog.Tome, UnitCatalog.Hisa, UnitCatalog.Borg, UnitCatalog.Kado };
        var waves = Waves().Where(w => w.Key is "2" or "3" or "4" or "5" or "guard" or "bat" or "boss").ToArray();
        var c0 = Combos("a")[0]; var c4 = Combos("a")[4];
        const int seeds = 20;
        Console.WriteLine($"# 第298期 濡れ衣編成の席 —— ザン・ミサ・ヒサ・ボルグ・カド × 席 120 × C0 ／ C4（MF-a ＋ ZN-b）× 第2〜5波 ＋ 精鋭 ＋ ボス × seed 0..{seeds - 1}");
        Console.WriteLine();
        var res = new System.Collections.Concurrent.ConcurrentBag<(int[] Perm, double C0, double C4, double Frames)>();
        Parallel.ForEach(Perms(5), perm =>
        {
            var g = new Formation();
            for (int i = 0; i < 5; i++) g[perm[i]] = five[i];
            double W(Combo c) => waves.Average(w => Many(Apply(g, c), w.Make, seeds).Win);
            double fr = waves.Average(w => { var a = Many(Apply(g, c4), w.Make, seeds); return a.P(a.Frames); });
            res.Add((perm, W(c0), W(c4), fr));
        });
        Console.WriteLine("同点は C4 の濡れ衣の仇討ち（1戦あたり・波の平均）の多い順（決めたこと: 勝率で席が割れないので、濡れ衣が最も出る席を採る）。");
        Console.WriteLine();
        Console.WriteLine("| C4 の平均 | C0 の平均 | C4 の濡れ衣（1戦） | 席（前1・前3・中央・後1・後3） |");
        Console.WriteLine("|--:|--:|--:|---|");
        foreach (var r in res.OrderByDescending(r => r.C4).ThenByDescending(r => r.Frames).ThenByDescending(r => r.C4 - r.C0).Take(10))
        {
            var g = new Formation();
            for (int i = 0; i < 5; i++) g[r.Perm[i]] = five[i];
            Console.WriteLine($"| {r.C4:F1} | {r.C0:F1} | {r.Frames:F2} | {Seats(g)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"席 120 通りのうち C4 の平均が最大の席の数: {res.Count(r => r.C4 == res.Max(x => x.C4))}");
    }

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
        return Rec(0).ToList();
    }

    // ---------------------------------------------------------------------------------
    // §6: 代表台 × 波 × 組
    // ---------------------------------------------------------------------------------
    static void Rates(string best, int seeds)
    {
        var combos = Combos(best);
        var waves = Waves();
        Console.WriteLine($"# 第298期 代表台 × 波 × 組（seed 0..{seeds - 1}・C3 ／ C4 のミサは {(best == "b" ? "MF-b" : "MF-a")}）");
        Console.WriteLine();
        Console.WriteLine("勝率（%）。**判定の分母は第2〜5波**（第1波は参考）。「よかった方」の線（§4-3）＝ 第2〜5波 ＋ 精鋭 ＋ ボスの平均。");
        Console.WriteLine();
        var lineAvg = combos.ToDictionary(c => c.Tag, _ => new List<double>());
        foreach (var (name, f) in Boards())
        {
            Console.WriteLine($"## {name}（{Seats(f)}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | " + string.Join(" | ", combos.Select(c => c.Tag)) + " |");
            Console.WriteLine("|---|" + string.Concat(combos.Select(_ => "--:|")));
            var detail = new List<string>();
            foreach (var w in waves)
            {
                var res = combos.Select(c => Many(Apply(f, c), w.Make, seeds)).ToArray();
                Console.WriteLine($"| {w.Name} | " + string.Join(" | ", res.Select(a => a.Win.ToString("F1"))) + " |");
                if (w.Key != "1") foreach (var (c, a) in combos.Zip(res)) lineAvg[c.Tag].Add(a.Win);
                foreach (var (c, a) in combos.Zip(res))
                    detail.Add($"| {w.Name} | {c.Tag} | {a.Win:F1} | {(a.Wins == 0 ? "—" : $"{(double)a.WinT / a.Wins:F1}")} | {(a.FirstDeathN == 0 ? "—" : $"{(double)a.FirstDeathT / a.FirstDeathN:F1}")}（{100.0 * a.FirstDeathN / a.N:F0}%） | {a.P(a.FfDeaths):F2} | "
                        + $"{a.P(a.HandShots):F1} ／ {a.P(a.MfFoe):F1} ／ {a.P(a.MfAlly):F1}（手番より前 {a.P(a.MfBefore):F1}） | {a.P(a.MfDealtFoe):F0} ／ {a.P(a.MfDealtAlly):F0} | {a.P(a.Vend):F1}（濡れ衣 {a.P(a.Frames):F1}・{a.P(a.FrameDealt):F0}） | "
                        + $"{a.P(a.Rally):F1} ／ {a.P(a.RallyHealed):F0} | {(a.SoraPresent == 0 ? "—" : $"{a.P(a.SoraHits):F1} ／ {a.P(a.SoraTaken):F0}・{(a.SoraDeathN == 0 ? "—" : $"{(double)a.SoraDeathT / a.SoraDeathN:F1}")}（{100.0 * a.SoraDeathN / a.N:F0}%）")} |");
            }
            Console.WriteLine();
            Console.WriteLine("<details><summary>組ごとの中身（1戦あたり）</summary>");
            Console.WriteLine();
            Console.WriteLine("| 波 | 組 | 勝率 | 決着T | 味方が初めて倒れたT（割合） | 同士討ちで倒れた味方 | 羽: 手番 ／ 標で敵へ ／ 標で味方へ | 標の羽の量 敵 ／ 味方 | ザンの仇討ち（濡れ衣・量） | 叫び ／ 回復量 | ソラ: 撃たれた羽 ／ 量・倒れたT |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|---|---|---|");
            foreach (var l in detail) Console.WriteLine(l);
            Console.WriteLine();
            Console.WriteLine("</details>");
            Console.WriteLine();
            // 対照（R383）: 固定枠の駒それぞれ → ドルガ（C0 と C1 と C3）
            Console.WriteLine("対照（R383・ドルガ）: " + string.Join(" ／ ", new[] { UnitCatalog.Sora, UnitCatalog.Tome, UnitCatalog.Zan, UnitCatalog.Hisa }
                .Where(d => f.Occupied().Any(o => ReferenceEquals(o.Def, d)))
                .Select(d => $"{Short(d)} → ドルガ: " + string.Join("・", new[] { combos[0], combos[1], combos[3] }.Select(c =>
                    $"{c.Tag.Split('（')[0]} {waves.Where(w => w.Key is "guard" or "bat" or "boss").Select(w => Many(FvSwap(Apply(f, c), d == UnitCatalog.Tome ? c.Misa : d == UnitCatalog.Zan ? c.Zan : d, UnitCatalog.Dolga), w.Make, seeds).Win.ToString("F0")).Aggregate((x, y) => x + "/" + y)}")))));
            Console.WriteLine("（近衛 ／ 大隊 ／ ボスの勝率）");
            Console.WriteLine();
        }
        Console.WriteLine("## 「よかった方」の線（代表台 7 台 × 第2〜5波 ＋ 精鋭 ＋ ボス の平均）");
        Console.WriteLine();
        foreach (var c in combos) Console.WriteLine($"- {c.Tag}: {lineAvg[c.Tag].Average():F2}");
    }

    // ---------------------------------------------------------------------------------
    // §6-2: `compare` の行
    // ---------------------------------------------------------------------------------
    static void CompareRows(string best)
    {
        var combos = Combos(best);
        Console.WriteLine($"# 第298期 `compare` 64 行 × 組（seed 0..199・第1〜5波）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var baseR = rows.ToDictionary(r => r.Name, r => CompareRates(r.F));
        foreach (var c in combos.Skip(1))
        {
            bool zb = c.Zan == UnitCatalog.ZanZNb;
            Console.WriteLine($"## {c.Tag}");
            Console.WriteLine();
            Console.WriteLine("| 行 | C0 | 組 | 最大の差（第2〜5波） |");
            Console.WriteLine("|---|---|---|--:|");
            var newR = new Dictionary<string, double[]>();
            foreach (var (n, f) in rows)
            {
                var g = Apply(f, c);
                newR[n] = g.Occupied().SequenceEqual(f.Occupied()) ? baseR[n] : CompareRates(g);
                if (newR[n].SequenceEqual(baseR[n])) continue;
                double d = Enumerable.Range(1, 4).Select(i => newR[n][i] - baseR[n][i]).OrderByDescending(Math.Abs).First();
                Console.WriteLine($"| {n} | {Row(baseR[n])} | {Row(newR[n])} | {d:+0.0;-0.0} |");
            }
            Console.WriteLine();
            Console.WriteLine("全64行の平均: C0 " + string.Join(" / ", Enumerable.Range(0, 5).Select(i => baseR.Values.Average(r => r[i]).ToString("F1"))) + " → 組 " + string.Join(" / ", Enumerable.Range(0, 5).Select(i => newR.Values.Average(r => r[i]).ToString("F1"))));
            var primary = Baseline.PrimaryRows;
            Console.WriteLine($"主判定19行の第五波: C0 {primary.Average(n => baseR[n][4]):F1} → 組 {primary.Average(n => newR[n][4]):F1}");
            var dropped = rows.Where(r => Enumerable.Range(1, 4).Any(i => newR[r.Name][i] - baseR[r.Name][i] <= -10.0)).ToList();
            Console.WriteLine($"いずれかの波（第2〜5波）で −10.0pt 以上落ちた行: {(dropped.Count == 0 ? "0 行" : string.Join(" ／ ", dropped.Select(r => r.Name)))}");
            foreach (var (n, f) in dropped)
            {
                Console.WriteLine();
                Console.WriteLine($"(G2) {n}:");
                foreach (var (_, d) in f.Occupied())
                {
                    var others = rows.Where(r => r.Name != n && r.F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToList();
                    if (others.Count == 0) { Console.WriteLine($"- {Short(d)}: 他の行 0（分解が成立しない）"); continue; }
                    double ch = others.Average(r => newR[r.Name].Skip(1).Average() - baseR[r.Name].Skip(1).Average());
                    Console.WriteLine($"- {Short(d)}: 他の行 {others.Count}・第2〜5波平均の変化 {ch:+0.00;-0.00} → {(ch <= -3.0 ? "**壊れ**" : Math.Abs(ch) < 3.0 ? "制約" : "—")}");
                }
            }
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    // 格子: 固定枠 ヒサ ＋ ザン ＋ ミサ・探索枠2 × 席 120
    // ---------------------------------------------------------------------------------
    static void Grid(string waveKey, string comboTag, int seeds, string best)
    {
        var w = WaveOf(waveKey);
        var c0 = Combos(best)[0]; var cv = ComboOf(comboTag, best);
        var fixedDefs = new[] { UnitCatalog.Hisa, UnitCatalog.Zan, UnitCatalog.Tome };
        var pool = UnitCatalog.All.Where(d => !fixedDefs.Contains(d)).ToArray();
        var perms = Perms(5).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第298期 格子 —— {w.Name} × C0 ／ {cv.Tag}（固定枠 ヒサ ＋ ザン ＋ ミサ・探索枠2 × 席 120・seed 0..{seeds - 1}）");
        Console.WriteLine();
        var rows = new System.Collections.Concurrent.ConcurrentBag<(string A, string B, double Base, double Ver, double SoraCtl)>();
        var pairs = new List<(UnitDef, UnitDef)>();
        for (int i = 0; i < pool.Length; i++) for (int j = i + 1; j < pool.Length; j++) pairs.Add((pool[i], pool[j]));
        Parallel.ForEach(pairs, pr =>
        {
            var five = new[] { UnitCatalog.Hisa, UnitCatalog.Zan, UnitCatalog.Tome, pr.Item1, pr.Item2 };
            double Best(Combo c, out int[] seat)
            {
                int bw = -1; seat = perms[0];
                foreach (var perm in perms)
                {
                    var g = new Formation();
                    for (int s = 0; s < 5; s++) g[perm[s]] = five[s];
                    var f = Apply(g, c);
                    int k = 0;
                    for (int sd = 0; sd < seeds; sd++) { if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) k++; else if (k + (seeds - sd - 1) <= bw) break; }
                    if (k > bw) { bw = k; seat = perm; }
                    if (bw == seeds) break;
                }
                return 100.0 * bw / seeds;
            }
            double b = Best(c0, out _), v = Best(cv, out var sv);
            if (Math.Max(b, v) < 50) return;
            double soraCtl = -1;
            if (ReferenceEquals(pr.Item1, UnitCatalog.Sora) || ReferenceEquals(pr.Item2, UnitCatalog.Sora))
            {
                var g = new Formation();
                for (int s = 0; s < 5; s++) g[sv[s]] = ReferenceEquals(five[s], UnitCatalog.Sora) ? UnitCatalog.Dolga : five[s];
                var f = Apply(g, cv);
                int k = 0;
                for (int sd = 0; sd < seeds; sd++) if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) k++;
                soraCtl = 100.0 * k / seeds;
            }
            rows.Add((Short(pr.Item1), Short(pr.Item2), b, v, soraCtl));
        });
        var all = rows.ToList();
        Console.WriteLine($"組 {pairs.Count}・所要 {sw.Elapsed.TotalSeconds:F0} 秒。50% 以上の台: C0 {all.Count(r => r.Base >= 50)} ／ {cv.Tag} {all.Count(r => r.Ver >= 50)}。版 − C0 の平均 {(all.Count == 0 ? 0 : all.Average(r => r.Ver - r.Base)):+0.0;-0.0}・+10pt 以上 {all.Count(r => r.Ver - r.Base >= 10)} ／ −10pt 以下 {all.Count(r => r.Ver - r.Base <= -10)}。");
        Console.WriteLine();
        int soraBase = all.Count(r => r.Base >= 50 && (r.A == "ソラ" || r.B == "ソラ")), soraVer = all.Count(r => r.Ver >= 50 && (r.A == "ソラ" || r.B == "ソラ"));
        Console.WriteLine($"**ソラが自由枠に入る 50% 以上の台**: C0 {soraBase} → {cv.Tag} {soraVer}（ソラ → ドルガで半分以下になる台 {all.Count(r => r.SoraCtl >= 0 && r.Ver >= 50 && r.SoraCtl <= r.Ver / 2)}）");
        Console.WriteLine();
        string Kinds(Func<(string A, string B, double Base, double Ver, double SoraCtl), bool> q) =>
            string.Join("・", all.Where(q).SelectMany(r => new[] { r.A, r.B }).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(15).Select(g => $"{g.Key} {g.Count()}"));
        Console.WriteLine($"自由枠（C0 で 50% 以上）: {Kinds(r => r.Base >= 50)}");
        Console.WriteLine();
        Console.WriteLine($"自由枠（{cv.Tag} で 50% 以上）: {Kinds(r => r.Ver >= 50)}");
        Console.WriteLine();
        Console.WriteLine($"自由枠（版が +10pt 以上）: {Kinds(r => r.Ver - r.Base >= 10)}");
        Console.WriteLine();
        Console.WriteLine("| C0 | 版 | ソラ → ドルガ | 自由枠 |");
        Console.WriteLine("|--:|--:|--:|---|");
        foreach (var r in all.OrderByDescending(r => r.Ver - r.Base).ThenByDescending(r => r.Ver).Take(12)) Console.WriteLine($"| {r.Base:F0} | {r.Ver:F0} | {(r.SoraCtl < 0 ? "—" : r.SoraCtl.ToString("F0"))} | {r.A}・{r.B} |");
    }

    // ---------------------------------------------------------------------------------
    // 台本
    // ---------------------------------------------------------------------------------
    static void Memo(string boardPart, string wave, int seed, string comboTag, string best)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var c = ComboOf(comboTag, best);
        var f = Apply(f0, c);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# feather298 memo —— {name} × {c.Tag} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Kind is not (BattleEventKind.Damage or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.Feather or BattleEventKind.MarkRally or BattleEventKind.Death or BattleEventKind.Attack)) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.PartnerId is int pa ? $"  Partner={N(pa)}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}{(x.Relayed ? "  relayed" : "")}");
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
    static readonly MethodInfo Drain = typeof(BattleContext).GetMethod("DrainFeatherMarksPublic", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("DrainFeatherMarksPublic");
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();

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
        Console.WriteLine("# feather298 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var d = UnitCatalog.Doha; var d0 = UnitCatalog.DohaD0;
        Expect("(a) 段0-1: 規定のドハ ＝ 旧（`DohaD0`）＋ `ShareBack` ＋ `SharerNoDull`・数値は旧のまま・文面とフレーバーは指示書のまま・旧は `All` ／ `Everyone` の外・版（DH-a ／ b ／ t）は旧から作る",
            d.Traits.SequenceEqual(d0.Traits.Append(TraitId.ShareBack).Append(TraitId.SharerNoDull)) && d.MaxHp == d0.MaxHp && d.Attack == d0.Attack && d.Speed == d0.Speed && d.Id == d0.Id
            && d.PlusText == "味方が受けるダメージの4割を肩代わりする（薙ぎでも全体でも効く・味方の破片が受け止めた残りから取る）。引き受けた痛みは、その相手の力に変えて返す"
            && d.MinusText == "自分は強くならず、味方が多いほど早く尽きる" && d.Flavor == "頼まれてもいない痛みを喰う。礼を言われたことは、一度もない。"
            && UnitCatalog.All.Contains(d) && !UnitCatalog.Everyone.Contains(d0)
            && UnitCatalog.DohaDHa.Traits.SequenceEqual(d0.Traits.Append(TraitId.ShareBack)) && SharerTrait.DullDivisor == 4);
        {
            // (b) 規定のドハはなまりを持たない ／ 旧と DH-a は持つ（ドルガへ 30 の一撃でドルガの攻撃力）
            int Dull(UnitDef dv)
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Tome, center: dv, back1: UnitCatalog.Kata, back3: UnitCatalog.Gald), Formation.Build(front1: UnitCatalog.Dolga), out var p, out var e);
                var dol = p.First(u => u.Def.Id == "dolga");
                ctx.ApplyDamage(dol, 30, e[0]);
                return ctx.DullByRoute[(int)DullRoute.Sharer];
            }
            int n0 = Dull(d0), na = Dull(UnitCatalog.DohaDHa), nn = Dull(d);
            Expect("(b) 段0-1: なまり（`DullRoute.Sharer`）が旧 ／ DH-a で出て、規定では 0", n0 > 0 && na == n0 && nn == 0, $"旧 {n0} ／ DH-a {na} ／ 規定 {nn}");
        }
        // (c) 段0-2: 直した群の量が本当の出どころに入る（なまり・橋・くべられる火・反転の4群を盤面を直に組んで）
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Tome, center: d0, back1: UnitCatalog.Kata, back3: UnitCatalog.Gald), Formation.Build(front1: UnitCatalog.Dolga), out var p, out var e);
            var dol = p.First(u => u.Def.Id == "dolga");
            // 敵のフックの印の中で起きた分かちのなまり（印 ＝ 敵のドルガ）
            var prev = ctx.BeginTrait(TraitId.Thorns, e[0]);
            ctx.ApplyDamage(dol, 30, e[0]);
            ctx.EndTrait(prev);
            var td = Tal(ctx, "doha"); var te = Tal(ctx, e[0].Def.Id);
            Expect("(c) 段0-2 群6: 別の駒の印の中のなまりは、印を戻した後に ドハ（本当の出どころ）へ・別の駒の側に「直す前に入っていた量」",
                (td.AttrFixed?[6] ?? 0) > 0 && (td.AttrTotal?[6] ?? 0) == (td.AttrFixed?[6] ?? 0) && (te.AttrStolen?[6] ?? 0) == (td.AttrFixed?[6] ?? 0) && ctx.Mark.Owner == null,
                $"ドハ {td.AttrFixed?[6]} ／ 敵のドルガ {te.AttrStolen?[6]}");
        }
        {
            // 群1: ベニの反転（毒の刻み）。ベニ隣の毒の駒の刻みが回復に。回復(与) がベニに入る
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Mio, center: UnitCatalog.Dolga, back1: UnitCatalog.Kata, back3: UnitCatalog.Gald), Formation.Build(front1: UnitCatalog.Dolga), out var p, out var e);
            var dol = p.First(u => u.Def.Id == "dolga");
            dol.Hp = dol.MaxHp - 20;
            dol.SetCounter(StatusKeys.Poison, 3);
            ctx.TickStatuses();
            var tb = Tal(ctx, "beni");
            Expect("(c2) 段0-2 群1: 反転の回復(与) がベニに入る（毒の刻み・印の無い枠）", tb.HealOutInTurn + tb.HealOutOffTurn > 0 && (tb.AttrFromNone?[1] ?? 0) > 0,
                $"ベニの回復(与) {tb.HealOutInTurn + tb.HealOutOffTurn}・誰でもないから {tb.AttrFromNone?[1]}");
        }
        // (d) 段0-2 の門: 全群の「直した量」が戦の中で 0 でない群がある（attr の要約・試遊 8 行 × 近衛 × seed 0..4）
        {
            var hit = new bool[9];
            foreach (var (n, f) in AttrBoards())
                foreach (var w in new[] { WaveOf("guard"), WaveOf("5") })
                    for (int s = 0; s < 3; s++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                        foreach (var t in r.TallyByUnit.Values) if (t.AttrFixed is { } a) for (int g = 1; g <= 8; g++) if (a[g] > 0) hit[g] = true;
                    }
            Expect("(d) 段0-2: 直した量が 0 でない群（群5 耐火の枝はノブ既定 0 で不活性）", hit[1] && hit[2] && hit[4] && hit[6] && hit[8] && !hit[5],
                string.Join(" ", Enumerable.Range(1, 8).Select(g => $"{g}:{(hit[g] ? "○" : "—")}")));
        }

        // MF ／ ZN（盤面を直に組む）: 前1 ドルガ ／ 前3 ザン ／ 中央 ミサ ／ 後1 カタ ／ 後3 ヒサ
        Formation Sq(UnitDef misa, UnitDef zan, UnitDef hisa) => Formation.Build(front1: UnitCatalog.Dolga, front3: zan, center: misa, back1: UnitCatalog.Kata, back3: hisa);
        var foeF = Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Gald);
        {
            var ctx = Ctx(Sq(UnitCatalog.MisaMFa, UnitCatalog.Zan, UnitCatalog.Hisa), foeF, out var p, out var e);
            var misa = p.First(u => u.Def.Id == "tome");
            int stock0 = FeathersTrait.Count(misa);
            int h0 = e[0].Hp;
            ctx.LayerMark(e[0], p.First(u => u.Def.Id == "zan"));   // 敵に新しい標（在庫 +1・羽が1発）
            Drain.Invoke(ctx, null);
            int afterNew = FeathersTrait.Count(misa), hit1 = h0 - e[0].Hp;
            ctx.LayerMark(e[0], p.First(u => u.Def.Id == "zan"));   // 層の追加（MF-a は飛ばない）
            Drain.Invoke(ctx, null);
            var tm = Tal(ctx, "tome");
            Expect("(e) MF-a: 新しい標で羽が1発（在庫は +1 のまま・減らない）・層の追加では飛ばない", hit1 > 0 && afterNew == stock0 + 1 && tm.MfShotsFoe == 1 && FeathersTrait.Count(misa) == stock0 + 2 && tm.MfQueuedLayer == 0,
                $"在庫 {stock0} → {afterNew} → {FeathersTrait.Count(misa)}・羽 {tm.MfShotsFoe}・削り {hit1}");
        }
        {
            var ctx = Ctx(Sq(UnitCatalog.MisaMFb, UnitCatalog.Zan, UnitCatalog.Hisa), foeF, out var p, out var e);
            var zan = p.First(u => u.Def.Id == "zan");
            ctx.LayerMark(e[0], zan); Drain.Invoke(ctx, null);
            ctx.LayerMark(e[0], zan); Drain.Invoke(ctx, null);
            var tm = Tal(ctx, "tome");
            Expect("(f) MF-b: 層の追加でも飛ぶ（新しい標 1 ＋ 層 1 ＝ 2 発）", tm.MfShotsFoe == 2 && tm.MfFresh == 1 && tm.MfLayer == 1, $"{tm.MfShotsFoe}（新 {tm.MfFresh} ／ 層 {tm.MfLayer}）");
        }
        {
            // (g) 味方への羽は同士討ち: 矢面の標（ヒサの記憶 ＋ 標）の付いた味方へ・半減は掛からない（量 ＝ 攻 × 倍率）
            var ctx = Ctx(Sq(UnitCatalog.MisaMFa, UnitCatalog.Zan, UnitCatalog.Hisa), foeF, out var p, out var e);
            var misa = p.First(u => u.Def.Id == "tome"); var hisa = p.First(u => u.Def.Id == "hisa"); var dol = p.First(u => u.Def.Id == "dolga");
            hisa.SetCounter(BeckonTrait.TargetKey, dol.InstanceId + 1);
            int h0 = dol.Hp;
            dol.SetCounter(StatusKeys.Marked, 1);
            Drain.Invoke(ctx, null);
            int lost = h0 - dol.Hp, want = misa.CurrentAttack * FinisherRule.Default.Multiplier;
            var tm = Tal(ctx, "tome");
            Expect("(g) 味方への羽は同士討ち（矢面の半減が掛からない・量 ＝ 攻 × 倍率）", tm.MfShotsAlly == 1 && lost == want, $"削り {lost}（攻 {misa.CurrentAttack} × {FinisherRule.Default.Multiplier}）");
            // (h) 再入しない: 羽の発射の中で書かれた標は控えない。ZN-a でない ザンなので、ここではミサの羽の中で ZN が出ないことも見る
            Expect("(h) 規定のザンは同士討ちで仇討ちしない（ZN の札なし）", Tal(ctx, "zan").VendettaFires == 0 && Tal(ctx, "zan").FrameVendettas == 0);
        }
        {
            // (i) ZN-a: ミサの羽が標の付いた味方に当たる → ヒサが指差し → ザンが仇討ち（その中で書かれた敵の標は羽を呼ばない）
            var ctx = Ctx(Sq(UnitCatalog.MisaMFb, UnitCatalog.ZanZNa, UnitCatalog.Hisa), foeF, out var p, out var e);
            var dol = p.First(u => u.Def.Id == "dolga");
            e[1].SetCounter(StatusKeys.Marked, 1);   // 敵に層 → ガルドが深い（指差しの先）
            Drain.Invoke(ctx, null);
            var tz0 = Tal(ctx, "zan").FrameVendettas; var tm0 = Tal(ctx, "tome").MfShotsFoe;
            dol.SetCounter(StatusKeys.Marked, 1);
            Drain.Invoke(ctx, null);
            var tz = Tal(ctx, "zan"); var tm = Tal(ctx, "tome"); var th = Tal(ctx, "hisa");
            Expect("(i) ZN-a（ミサは MF-b）: ミサの羽が標の付いた味方へ → ヒサの指差し → ザンの濡れ衣の仇討ち 1 回・仇討ちが足した敵の層は羽を呼ばない（連鎖の中で控えない）",
                tz.FrameVendettas - tz0 == 1 && th.FrameAccuses == 1 && tm.MfShotsAlly == 1 && tm.MfChainSkipped >= 1 && tm.MfShotsFoe == tm0,
                $"濡れ衣 {tz.FrameVendettas - tz0}・指差し {th.FrameAccuses}・味方への羽 {tm.MfShotsAlly}・敵への羽 {tm.MfShotsFoe - tm0}・控えなかった {tm.MfChainSkipped}");
        }
        {
            // (j) ZN-a はヒサがいなければ出ない
            var ctx = Ctx(Sq(UnitCatalog.MisaMFa, UnitCatalog.ZanZNa, UnitCatalog.Dolga), foeF, out var p, out var e);
            var kata = p.First(u => u.Def.Id == "kata");
            kata.SetCounter(StatusKeys.Marked, 1);
            Drain.Invoke(ctx, null);
            var tz = Tal(ctx, "zan");
            Expect("(j) ZN-a はヒサがいなければ出ない", Tal(ctx, "tome").MfShotsAlly == 1 && tz.FrameVendettas == 0 && tz.FrameNoAccuser == 1);
        }
        {
            // (k) ZN-a はミサの羽以外の同士討ちでは出ない ／ ZN-b は出る（カタの同士討ちを直に）
            int F(UnitDef zan)
            {
                var ctx = Ctx(Sq(UnitCatalog.Tome, zan, UnitCatalog.Hisa), foeF, out var p, out var e);
                var dol = p.First(u => u.Def.Id == "dolga"); var kata = p.First(u => u.Def.Id == "kata");
                dol.SetCounter(StatusKeys.Marked, 1);
                ctx.ApplyDamage(dol, 5, kata, isFriendlyFire: true);
                return (int)Tal(ctx, "zan").FrameVendettas;
            }
            Expect("(k) ZN-a はミサ以外の同士討ちでは出ない ／ ZN-b は出る", F(UnitCatalog.ZanZNa) == 0 && F(UnitCatalog.ZanZNb) == 1);
        }
        {
            // (l) ヒサの指差しは乱数を引かない: 層 → 攻撃力 → 席
            var ctx = Ctx(Sq(UnitCatalog.Tome, UnitCatalog.ZanZNa, UnitCatalog.Hisa), Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Gald, center: UnitCatalog.Kado), out var p, out var e);
            var zan = p.First(u => u.Def.Id == "zan");
            var top = e.OrderByDescending(u => u.CurrentAttack).ThenBy(u => u.Slot).First();
            bool a1 = ctx.FramePick(zan) == top;
            var g = e.First(u => u != top); g.SetCounter(StatusKeys.Marked, 2);
            bool a2 = ctx.FramePick(zan) == g;
            Expect("(l) ヒサの指差し: 標の層 → 攻撃力 → 席番号（乱数を引かない）", a1 && a2);
        }
        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(m) `PickOne(` の出現数が第297期と同じ（BattleCore）", pick == 36, $"{pick}");
        // (n) verbose の有無で勝敗・決着T が同じ（C1〜C4 × 代表台 × 試遊の波 × seed 0..9）
        int diff = 0, n2 = 0;
        foreach (var c in Combos("a").Skip(1)) foreach (var (_, f) in Boards()) foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf)) for (int s = 0; s < 10; s++)
                    {
                        var g = Apply(f, c);
                        var a = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                        var b = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                        n2++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                    }
        Expect("(n) verbose の有無で勝敗・決着T が同じ（C1〜C4 × 代表台 7 × 試遊の波 × seed 0..9）", diff == 0, $"{n2} 戦・違い {diff}");
        // (o) 出来事: C1 で `FeatherMark`（敵 ／ 味方）、C3 で `Framed`（2種）
        long Ev(Combo c, BattleEventKind k, string t) { long x = 0; for (int s = 0; s < 10; s++) x += BattleEngine.Run(BattleEngine.Materialize(Apply(Playtest("試遊・標 循環"), c), BattleContext.PlayerTeam), WaveOf("guard").Make(), s, verbose: true).Events.Count(e => e.Kind == k && e.Text == t); return x; }
        var cs = Combos("a");
        long f1 = Ev(cs[1], BattleEventKind.FeatherMark, FeatherMarkLabels.Foe), f2 = Ev(cs[1], BattleEventKind.FeatherMark, FeatherMarkLabels.Ally), f3 = Ev(cs[3], BattleEventKind.Framed, FramedLabels.Accuse), f4 = Ev(cs[3], BattleEventKind.Framed, FramedLabels.Vendetta), f0 = Ev(cs[0], BattleEventKind.FeatherMark, FeatherMarkLabels.Foe);
        Expect("(o) 出来事: C0 は `FeatherMark` 0・C1 に敵 ／ 味方の `FeatherMark`・C3 に `Framed`（あいつがやった ＝ 濡れ衣）（循環 × 近衛 × seed 0..9）", f0 == 0 && f1 > 0 && f2 > 0 && f3 > 0 && f3 == f4, $"{f0} ／ {f1} ／ {f2} ／ {f3} ／ {f4}");
        // (p) 版の駒は `All` ／ `Everyone` ／ `Presets` の外・文面
        var vers = new[] { UnitCatalog.MisaMFa, UnitCatalog.MisaMFb, UnitCatalog.ZanZNa, UnitCatalog.ZanZNb };
        Expect("(p) 版の札と文面（ミサ: プラス末尾「指差されたものは、全部撃つ」・マイナス末尾「味方が指差されても、撃つ」／ ザン: プラス末尾）・`All` ／ `Everyone` ／ `Presets` の外",
            UnitCatalog.MisaMFa.Traits.SequenceEqual(UnitCatalog.Tome.Traits.Append(TraitId.FeatherMark)) && UnitCatalog.MisaMFb.Traits.SequenceEqual(UnitCatalog.Tome.Traits.Append(TraitId.FeatherMarkLayer))
            && UnitCatalog.ZanZNa.Traits.SequenceEqual(UnitCatalog.Zan.Traits.Append(TraitId.VendettaFrame)) && UnitCatalog.ZanZNb.Traits.SequenceEqual(UnitCatalog.Zan.Traits.Append(TraitId.VendettaFrameAll))
            && UnitCatalog.MisaMFa.PlusText.EndsWith("。指差されたものは、全部撃つ", StringComparison.Ordinal) && UnitCatalog.MisaMFa.MinusText.EndsWith("。味方が指差されても、撃つ", StringComparison.Ordinal)
            && UnitCatalog.ZanZNa.PlusText.EndsWith("。仲間を撃った者が誰であれ、指差された敵を斬る", StringComparison.Ordinal) && UnitCatalog.ZanZNa.MinusText == UnitCatalog.Zan.MinusText
            && vers.All(v => !UnitCatalog.Everyone.Contains(v)) && Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).All(r => r.F.Occupied().All(o => !vers.Contains(o.Def))));
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
