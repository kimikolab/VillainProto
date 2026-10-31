using BattleCore;
using static Common;
using I = Inv348Diag;
using V = Inv414Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// sekki421 —— 第421期 Phase 0: セッキ入りの台の横並び ／ 段0【規定化】: セッキ ＝ 第420期の SW-n（旧は `SekkiH420`）。
// 指示書は design/PHASE421_SEKKI_SETTLE_SPEC.md ／ 報告は design/PHASE421_SEKKI_SETTLE.md。横並びは `inv414` の代表台 20 × 物差し 13 波を借りる。
//
//     dotnet run --project BattleSim -c Release 0 sekki421 p0       # Phase 0: 代表台 20 ＋ セッキ入りの台 3 × {旧の規定, SW-n} × 13 波・seed 0..99（止める線の判定つき）
//     dotnet run --project BattleSim -c Release 0 sekki421 settle   # 段0 の確かめ: 試遊・手番 芯4枚 ／ 試遊・呪い（クビ → セッキ）× 4 波・seed 0..99 が SW-n（`SekkiSWn`）と同じ
//     dotnet run --project BattleSim -c Release 0 sekki421 nomi     # 次の下見: ノミを含む compare ／ elite の行 × 4 波・ノミの与ダメ ／ 連打 ／ 再行動・ノミ → ミサの勝率
//     dotnet run --project BattleSim -c Release 0 sekki421 check    # 自己検査
// =====================================================================================
static class Sekki421Diag
{
    const int Seeds = 100;
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");
    static readonly I.W[] Four = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };

    static Formation Swap(Formation f, UnitDef sekki)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "sekki") g[o.Slot] = sekki;
        return g;
    }
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;

    /// <summary>セッキ入りの台 3 つ（並び ＝ セッキを差す関数）。</summary>
    static readonly (string Name, Func<UnitDef, Formation> F)[] SekkiBoards =
    {
        ("試遊・手番 芯4枚", s => Presets.FourCore397(sekki: s)),
        ("試遊・呪い（クビ → セッキ）", s => Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Mudo, center: UnitCatalog.Nel, back1: s, back3: UnitCatalog.Utsu)),
        ("逆しま+後備え", s => Swap(Row("逆しま+後備え"), s)),
    };

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "p0": Phase0(); break;
            case "settle": Settle(); break;
            case "nomi": Nomi(); break;
            case "check": Check(); break;
            default: Console.WriteLine("sekki421: モードは p0 / settle / nomi / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // Phase 0: 横並び
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        // 旧の規定 ＝ 第420期までの規定（段0 の後に回しても同じものを測るよう、旧は `SekkiH420` があればそれ、無ければ今の規定）
        UnitDef oldSekki = OldSekki();
        var boards = V.Boards();
        var res = boards.ToDictionary(b => b.Name, b => I.AxisWaves.ToDictionary(w => w.Name, w => I.Measure(b.F, w)));
        var val = boards.ToDictionary(b => b.Name, b => V.Vals(res[b.Name]));
        var axes = boards.GroupBy(b => b.Axis).ToDictionary(g => g.Key, g => Enumerable.Range(0, V.Cols.Length).Select(i => g.Max(b => val[b.Name][i])).ToArray());
        double Avg4(Dictionary<string, I.St> r) => Four.Average(w => r[w.Name].Win);

        Console.WriteLine($"# 第421期 Phase 0 —— 代表台 {boards.Length}（`inv414` の並び・いまの規定）＋ セッキ入りの台 {SekkiBoards.Length} × {{旧の規定, SW-n}} × 物差し {I.AxisWaves.Length} 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine($"旧の規定 ＝ `{(ReferenceEquals(oldSekki, UnitCatalog.Sekki) ? "UnitCatalog.Sekki" : "UnitCatalog.SekkiH420")}`（S2h ＋ B ＋ K1）。SW-n ＝ `UnitCatalog.SekkiSWn`。");
        Console.WriteLine("順位は、軸 10 本の1枚（代表台2つの良いほう・デバフ軸を含む）＋ その行・同じ値は上の順位（`inv414` §1-5 の形）。単独 1 位 ＝ 10 本のどれよりも良い。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", V.Cols) + " | 4 波平均 | 1位の数（単独）|");
        Console.WriteLine("|---|---|" + string.Concat(V.Cols.Select(_ => "--:|")) + "--:|--:|");
        var solo = new Dictionary<(string, string), int>();
        var avg = new Dictionary<(string, string), double>();
        foreach (var (name, mk) in SekkiBoards)
            foreach (var (lab, d) in new[] { ("旧の規定", oldSekki), ("SW-n", UnitCatalog.SekkiSWn) })
            {
                var r = I.AxisWaves.ToDictionary(w => w.Name, w => I.Measure(mk(d), w));
                var v = V.Vals(r);
                var ranks = Enumerable.Range(0, V.Cols.Length).Select(i => 1 + axes.Values.Count(x => x[i] > v[i])).ToArray();
                int s1 = Enumerable.Range(0, V.Cols.Length).Count(i => axes.Values.All(x => x[i] < v[i]));
                solo[(name, lab)] = s1; avg[(name, lab)] = Avg4(r);
                Console.WriteLine($"| {name} | {lab} | " + string.Join(" | ", Enumerable.Range(0, V.Cols.Length).Select(i => $"{V.Fmt(i, v[i])}（{ranks[i]}）")) + $" | {F1(Avg4(r))} | {ranks.Count(x => x == 1)}（{s1}）|");
            }
        Console.WriteLine();
        Console.WriteLine("軸ごとの1枚（代表台2つの良いほう）と、4 波平均（ボス ／ 近衛 ／ 大隊 ／ 強 第4波）の良いほう:");
        Console.WriteLine();
        Console.WriteLine("| 軸 | " + string.Join(" | ", V.Cols) + " | 4 波平均 |");
        Console.WriteLine("|---|" + string.Concat(V.Cols.Select(_ => "--:|")) + "--:|");
        var axisAvg = boards.GroupBy(b => b.Axis).ToDictionary(g => g.Key, g => g.Max(b => Avg4(res[b.Name])));
        foreach (var (a, x) in axes) Console.WriteLine($"| {a} | " + string.Join(" | ", Enumerable.Range(0, V.Cols.Length).Select(i => V.Fmt(i, x[i]))) + $" | {F1(axisAvg[a])} |");
        Console.WriteLine();
        Console.WriteLine("デバフ軸の台（第414期 §1-2 の値と並べる）: " + string.Join("・", boards.Where(b => b.Axis == "デバフ").Select(b => $"{b.Name} 4 波平均 {F1(Avg4(res[b.Name]))}")) + "。");
        Console.WriteLine();
        double best = axisAvg.Values.Max();
        string curse = SekkiBoards[1].Name;
        int soloN = solo[(curse, "SW-n")];
        double over = avg[(curse, "SW-n")] - best;
        Console.WriteLine("## 止める線");
        Console.WriteLine();
        Console.WriteLine($"- 「{curse}」SW-n が単独 1 位の物差し: **{soloN}**（線 ＝ 2 以上で止める）");
        Console.WriteLine($"- 「{curse}」SW-n の 4 波平均 {F1(avg[(curse, "SW-n")])} − 全軸の最良 {F1(best)}（{axisAvg.First(kv => kv.Value == best).Key}）＝ **{Delta(over)}**（線 ＝ +5.0 以上で止める）");
        Console.WriteLine($"- SW-n にしたとき単独 1 位が増える物差しの数（3 台の和）: " + string.Join("・", SekkiBoards.Select(b => $"{b.Name} {solo[(b.Name, "旧の規定")]} → {solo[(b.Name, "SW-n")]}")));
        Console.WriteLine();
        Console.WriteLine(soloN >= 2 || over >= 5.0 ? "**止める線に当たった → 規定化せず報告。**" : "**止める線に当たらない → 段0 へ。**");
        Console.WriteLine();
    }

    static UnitDef OldSekki()
    {
        var f = typeof(UnitCatalog).GetField("SekkiH420");
        return f?.GetValue(null) as UnitDef ?? UnitCatalog.Sekki;
    }

    // ---------------------------------------------------------------------------------
    // 段0 の確かめ
    // ---------------------------------------------------------------------------------
    static void Settle()
    {
        Console.WriteLine("# 第421期 段0 の確かめ —— 規定のセッキ × SW-n（`SekkiSWn`・同じ札の別の定義）× 4 波・seed 0..99");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | SW-n | 一致 |");
        Console.WriteLine("|---|---|---|---|:-:|");
        foreach (var (name, mk) in SekkiBoards.Take(2))
            foreach (var w in Four)
            {
                var a = L.Measure(mk(UnitCatalog.Sekki), w, Seeds);
                var b = L.Measure(mk(UnitCatalog.SekkiSWn), w, Seeds);
                bool same = a.Wins == b.Wins && a.WinT == b.WinT && a.Turns == b.Turns && a.Fell == b.Fell;
                Console.WriteLine($"| {name} | {w.Name} | {a.WT} | {b.WT} | {(same ? "○" : "**×**")} |");
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // 次の下見: ノミ
    // ---------------------------------------------------------------------------------
    static void Nomi()
    {
        var rows = CompareBuilds().Concat(CrossBuilds()).Where(r => r.F.Occupied().Any(o => o.Def.Id == "nomi")).ToArray();
        Console.WriteLine($"# 第421期 §4 の下見 —— ノミを含む `compare` ／ 交差帯の行（{rows.Length} 行）× 4 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("与ダメ ＝ ノミが敵に与えた量 ／戦。一振り ＝ 豆鉄砲の一振りの数 ／戦（`PelletVolleys`・再行動の一振りも1つ）。発 ／ 一振り ＝ 撃った発の数 ÷ 一振り（`PelletShots`）。再行動 ＝ 「刻んだ相手が倒れるともう一度動く」が鳴った回数 ／戦（`EncoreFires`）。ミサ ＝ 同じ台でノミを規定のミサ（見境なし）に替えた勝率。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 勝率 | ノミの与ダメ ／戦 | 一振り ／戦 | 発 ／ 一振り | 再行動 ／戦 | ミサに替えた勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in rows)
            foreach (var w in Four)
            {
                long n = 0, wins = 0, dmg = 0, turns = 0, swings = 0, re = 0, winsM = 0;
                var g = f.Clone();
                foreach (var o in f.Occupied()) if (o.Def.Id == "nomi") g[o.Slot] = UnitCatalog.Tome;
                var parts = new (long Wins, long Dmg, long Turns, long Swings, long Re, long WinsM)[Seeds];
                Parallel.For(0, Seeds, s =>
                {
                    var (r, _, _) = I.Raw(f, w, s, verbose: false);
                    var (rm, _, _) = I.Raw(g, w, s, verbose: false);
                    r.TallyByUnit.TryGetValue("nomi", out var t);
                    parts[s] = (r.PlayerWon ? 1 : 0, t?.DamageToEnemy ?? 0, t?.PelletVolleys ?? 0, t?.PelletShots ?? 0, t?.EncoreFires ?? 0, rm.PlayerWon ? 1 : 0);
                });
                foreach (var p in parts) { n++; wins += p.Wins; dmg += p.Dmg; turns += p.Turns; swings += p.Swings; re += p.Re; winsM += p.WinsM; }
                Console.WriteLine($"| {name} | {w.Name} | {F1(100.0 * wins / n)} | {Per1(dmg, n)} | {Per(turns, n)} | {Per(swings, turns)} | {Per(re, n)} | {F1(100.0 * winsM / n)} |");
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第421期 sekki421 check");
        Console.WriteLine();
        var sk = UnitCatalog.Sekki; var swn = UnitCatalog.SekkiSWn;
        Req(V.Boards().Length == 20, "代表台は `inv414` の 20 台");
        Req(SekkiBoards.All(b => b.F(sk).Occupied().Count(o => o.Def.Id == "sekki") == 1), "セッキ入りの台 3 つにセッキが 1 枚ずつ");
        var old = OldSekki();
        if (!ReferenceEquals(old, sk))
        {
            Req(sk.Traits.SequenceEqual(swn.Traits) && sk.PlusText == swn.PlusText && sk.MinusText == swn.MinusText, "規定のセッキ ＝ SW-n（札 ／ 文面）");
            Req(sk.Traits.SequenceEqual(old.Traits.Append(TraitId.SlimeGulpAlly).Append(TraitId.SlimeGulpLate)), "規定 ＝ 旧（`SekkiH420`）＋ SlimeGulpAlly ＋ SlimeGulpLate");
            Req(sk.Flavor == old.Flavor && sk.Name == old.Name && sk.MaxHp == 70 && sk.Attack == 2 && sk.Speed == 2, "名前 ／ フレーバー ／ HP 70 ／ 攻 2 ／ 速 2 は旧のまま");
            Req(UnitCatalog.All.Contains(sk) && !UnitCatalog.All.Contains(old), "`All` のセッキは規定（旧は入らない）");
            Req(ReferenceEquals(PinS420(sk), old) && ReferenceEquals(PinS420(UnitCatalog.Mudo), UnitCatalog.Mudo), "`PinS420` は規定のセッキだけを旧へ");
            Req(!Presets.Versions.Any(r => r.Name == "版・呑み セッキ"), "「版・呑み セッキ」は外した");
        }
        else Req(sk.Traits.All(t => t != TraitId.SlimeGulpLate), "（段0 の前）規定のセッキは呑まない");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
