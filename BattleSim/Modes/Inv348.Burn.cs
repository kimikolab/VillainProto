using BattleCore;
using static Common;
using FA = FireAtkDiag;

// inv348 §4 —— 燃焼の内訳（ボルグが死なない理由 ／ ホタの火力の出どころ）。札を1枚ずつ外す（`ablation` の作法・規定の札の配列から1枚抜くだけ）。
static partial class Inv348Diag
{
    static readonly string[] RouteNames = Enum.GetNames(typeof(DamageRoute));

    static Dictionary<TraitId, string>? _traitJa;
    /// <summary>札の日本語名（`TraitId` の列挙子の行末コメントの頭・「（」か「:」の手前）。読めなければ列挙子の名前。</summary>
    internal static string TraitJa(TraitId t)
    {
        if (_traitJa is null)
        {
            var d = new Dictionary<TraitId, string>();
            try
            {
                foreach (var line in File.ReadLines("BattleCore/Traits.cs"))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*([A-Z][A-Za-z0-9]*)\s*,\s*//\s*([^（(:：]+)");
                    if (m.Success && Enum.TryParse<TraitId>(m.Groups[1].Value, out var id) && !d.ContainsKey(id)) d[id] = m.Groups[2].Value.Trim();
                    if (line.StartsWith("}", StringComparison.Ordinal) && d.Count > 0) break;
                }
            }
            catch (IOException) { }
            _traitJa = d;
        }
        return _traitJa.TryGetValue(t, out var n) ? n : t.ToString();
    }

    /// <summary>§4 の台（燃焼の代表台・§3 と同じ2台）。</summary>
    static Board[] BurnBoards() => Boards().Where(b => b.Axis == "燃焼").ToArray();

    sealed record Abl(string Name, Func<Formation, Formation> Map);

    static Abl[] BorgAbl()
    {
        var list = new List<Abl> { new("規定", f => f) };
        foreach (var t in UnitCatalog.BorgH349.Traits)
            list.Add(new($"ボルグ −{TraitJa(t)}（{t}）", f => Swap(f, "borg", Without(UnitCatalog.BorgH349, t))));
        list.Add(new("ヒヨ → 素体", f => Swap(f, "hiyo", Plain(UnitCatalog.HiyoH349))));
        list.Add(new("ヒヨ −火の変換（FireConvert）", f => Swap(f, "hiyo", Without(UnitCatalog.HiyoH349, TraitId.FireConvert))));
        return list.ToArray();
    }

    static Abl[] HotaAbl()
    {
        var list = new List<Abl> { new("規定", f => f) };
        foreach (var t in UnitCatalog.HotaH349.Traits)
            list.Add(new($"ホタ −{TraitJa(t)}（{t}）", f => Swap(f, "hota", Without(UnitCatalog.HotaH349, t))));
        list.Add(new("ヒヨ −贔屓（Favor ＋ FavorLevel）", f => Swap(f, "hiyo", Without(UnitCatalog.HiyoH349, TraitId.Favor, TraitId.FavorLevel))));
        list.Add(new("ホタ −あぶれた火 −くべられる火 ／ ヒヨ −贔屓（育ち3つとも）", f => Swap(Swap(f, "hota", Without(UnitCatalog.HotaH349, TraitId.PyreOverflow, TraitId.PyreFed)), "hiyo", Without(UnitCatalog.HiyoH349, TraitId.Favor, TraitId.FavorLevel))));
        return list.ToArray();
    }

    static double AliveTOf(St s, string id) => s.N == 0 ? 0 : (double)s.AliveT.GetValueOrDefault(id) / s.N;
    static double FellPct(St s, string id) => s.N == 0 ? 0 : 100.0 * s.Deaths.GetValueOrDefault(id) / s.N;

    static void Burn()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BurnBoards();
        W[] bw = { Boss, Bat, M5, StrongWaves[3] };
        Console.WriteLine($"# {Tag} §4 燃焼の内訳 —— 台 {boards.Length}（{string.Join(" ／ ", boards.Select(b => b.Name))}）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();

        // ---- 4-0: 誰が倒れているか ----
        Console.WriteLine("## 4-0. 規定の台で誰が倒れているか（倒れた割合 % ／ 生きていた T の平均）");
        Console.WriteLine();
        foreach (var b in boards)
        {
            var ids = b.F.Occupied().Select(o => o.Def).ToArray();
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率・T | " + string.Join(" | ", ids.Select(Short)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(ids.Select(_ => "--:|")));
            foreach (var w in bw)
            {
                var s = Measure(b.F, w);
                Console.WriteLine($"| {w.Name} | {F1(s.Win)}・{s.WinTs}T | " + string.Join(" | ", ids.Select(d => $"{F1(FellPct(s, d.Id))}% ／ {F1(AliveTOf(s, d.Id))}T")) + " |");
            }
            Console.WriteLine();
        }

        // ---- 4-1: ボルグの札を1枚ずつ外す ----
        Console.WriteLine("## 4-1. ボルグが死なない理由 —— ボルグの札を1枚ずつ外したときの、ボルグの生きていた T ／ 倒れた割合 ／ 台の勝率（・決着T）");
        Console.WriteLine();
        Console.WriteLine("生きていた T ＝ 倒れた戦は倒れた T・生き残った戦は決着 T（30T 打ち切りを含む）。規定との差を（）に。陣営の規則の札（燃え広がりの上限・敵の火勢など）もボルグが持っているので、外すと燃焼の規則そのものが抜ける。");
        Console.WriteLine();
        var abl = BorgAbl();
        foreach (var b in boards)
        {
            bool hasHiyo = b.F.Occupied().Any(o => o.Def.Id == "hiyo");
            var res = new Dictionary<(string, string), St>();
            foreach (var a in abl)
            {
                if (a.Name.StartsWith("ヒヨ", StringComparison.Ordinal) && !hasHiyo) continue;
                foreach (var w in bw) res[(a.Name, w.Name)] = Measure(a.Map(b.F), w);
            }
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 外した札 | " + string.Join(" | ", bw.Select(w => $"{w.Name} 生T ／ 倒% ／ 勝率・T")) + " |");
            Console.WriteLine("|---|" + string.Concat(bw.Select(_ => "---|")));
            foreach (var a in abl)
            {
                if (!res.ContainsKey((a.Name, bw[0].Name))) continue;
                Console.WriteLine($"| {a.Name} | " + string.Join(" | ", bw.Select(w =>
                {
                    var s = res[(a.Name, w.Name)]; var z = res[("規定", w.Name)];
                    double t = AliveTOf(s, "borg"), t0 = AliveTOf(z, "borg"), f = FellPct(s, "borg"), f0 = FellPct(z, "borg");
                    string d(double x, double x0) => a.Name == "規定" || Math.Abs(x - x0) < 0.05 ? "" : $"（{Sg(x - x0)}）";
                    return $"{F1(t)}{d(t, t0)} ／ {F1(f)}{d(f, f0)} ／ {F1(s.Win)}{d(s.Win, z.Win)}・{s.WinTs}T";
                })) + " |");
            }
            Console.WriteLine();

            // 受けた量の内訳（規定）
            Console.WriteLine($"#### {b.Name} —— ボルグが受けた量の内訳（規定・1戦あたり）");
            Console.WriteLine();
            Console.WriteLine("受けた ＝ HP が減った量（`DamageTaken`）・経路は害の帳簿（`HarmAmount`）。減らした ＝ 火の鎧（燃えていて半分）／ 盾の配りで切ってもらった量（受け手の側）。"
                + "戻した ＝ 回復で実際に増えた HP（火の癒し ／ 焼き返し（燃えている敵を斬って癒える）／ そのほか＝ヒヨの火の変換・ヒサの声援など）。焼け残り ＝ 倒れる一撃を HP1 で止めた回数（札をボルグが持っていなければ 0）。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 受けた | 経路（多い順） | 火の鎧で減らした（回） | 盾の配りで減らしてもらった | （参考）盾の配りで味方から減らした | 戻した 計 | 火の癒し | 焼き返し | そのほか | 焼け残り | ヒヨの火の変換（全員へ・癒えた） |");
            Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (var w in bw)
            {
                var s = res[("規定", w.Name)]; var t = s.T("borg"); long X(string id, int i) => s.Xv(id, i);
                string routes = t.HarmAmount is null ? "—" : string.Join("・", Enumerable.Range(0, t.HarmAmount.Length).Where(i => t.HarmAmount[i] > 0)
                    .OrderByDescending(i => t.HarmAmount[i]).Take(4).Select(i => $"{RouteNames[i]} {Per(t.HarmAmount[i], s.N)}"));
                long other = t.Healed - X("borg", Xi.MendHealed) - X("borg", Xi.FeedHealed);
                Console.WriteLine($"| {w.Name} | {Per(t.DamageTaken, s.N)} | {routes} | {Per(X("borg", Xi.ArmorSaved), s.N)}（{Per(X("borg", Xi.ArmorHits), s.N)}） | {Per(X("borg", Xi.WardTaken), s.N)} | {Per(X("borg", Xi.WardSaved), s.N)} | {Per(t.Healed, s.N)} | "
                    + $"{Per(X("borg", Xi.MendHealed), s.N)} | {Per(X("borg", Xi.FeedHealed), s.N)} | {Per(other, s.N)} | {Per2(X("borg", Xi.Smolder), s.N)} | {(hasHiyo ? Per(X("hiyo", Xi.ConvTick) + X("hiyo", Xi.ConvSplash), s.N) : "—")} |");
            }
            Console.WriteLine();
        }

        // ---- 4-2: ホタ ----
        W[] hw = { Boss, T1, Bat };
        Console.WriteLine("## 4-2. ホタの火力の出どころ —— 手番の種類ごとの与ダメ（規定・台本を読む）");
        Console.WriteLine();
        Console.WriteLine("手番の種類は `fireatk` の表D と同じ（段0〜臨界 ／ 焼き尽くす ／ 残り火）。「手番の外」＝ ホタの与ダメ（`DamageToEnemy`）− 手番の中の与ダメ（被弾の燃焼の起爆・燃えさし・燃焼の刻みのうちホタに付いた分など）。的・一は 1〜8 ターン目だけ。");
        Console.WriteLine();
        foreach (var b in boards)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | ホタの与ダメ ／戦 | " + string.Join(" | ", FA.HandKinds) + " | 手番の外 | 攻撃力の育ち（贔屓 ／ あぶれた火 ／ くべられる火・量 ／戦） |");
            Console.WriteLine("|---|--:|" + string.Concat(FA.HandKinds.Select(_ => "---|")) + "--:|---|");
            foreach (var w in hw)
            {
                var parts = new (FA.AAgg A, long Total)[Seeds];
                Parallel.For(0, Seeds, i =>
                {
                    var (r, p, e) = Raw(b.F, w, i, verbose: true);
                    var ag = new FA.AAgg();
                    ag.Take(r, p, e, w.Target);
                    long tot = 0;
                    if (r.TallyByUnit.TryGetValue("hota", out var t))
                        tot = w.Target ? (t.BossDmgByTurn is int[] d ? d.Skip(1).Take(TargetTurns).Sum() : 0) : t.DamageToEnemy;
                    parts[i] = (ag, tot);
                });
                var all = new FA.AAgg(); long total = 0;
                foreach (var (a, t) in parts) { all.Merge(a); total += t; }
                long inHand = all.HandTotal.Sum();
                Console.WriteLine($"| {w.Name} | {Per(total, all.N)} | " + string.Join(" | ", Enumerable.Range(0, FA.HandKinds.Length).Select(k =>
                    all.HandN[k] == 0 ? "—" : $"{Per(all.HandTotal[k], all.N)}（{Per2(all.HandN[k], all.N)} 手）")) + $" | {Per(total - inHand, all.N)} | "
                    + string.Join(" ／ ", Enumerable.Range(0, 3).Select(s => Per(Enumerable.Range(0, 5).Sum(o => all.SrcAmt[s, o]), all.N))) + " |");
            }
            Console.WriteLine();
        }

        Console.WriteLine("## 4-2b. ホタの札を1枚ずつ外したときの、ホタの与ダメ ／ 台の倒しT（ボス）・勝率");
        Console.WriteLine();
        Console.WriteLine("ホタの与ダメ ＝ 1戦あたり（的・一は 1〜8 ターン目）。規定との差を（）に（与ダメは %）。");
        Console.WriteLine();
        var habl = HotaAbl();
        foreach (var b in boards)
        {
            bool hasHiyo = b.F.Occupied().Any(o => o.Def.Id == "hiyo");
            var res = new Dictionary<(string, string), St>();
            foreach (var a in habl)
            {
                if (a.Name.Contains("ヒヨ") && !hasHiyo) continue;
                foreach (var w in hw) res[(a.Name, w.Name)] = Measure(a.Map(b.F), w);
            }
            double HotaDmg(St s, W w)
            {
                if (!w.Target) return (double)s.T("hota").DamageToEnemy / s.N;
                return (double)s.Xv("hota", Xi.Dmg8) / s.N;
            }
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 外した札 | " + string.Join(" | ", hw.Select(w => w.Target ? $"{w.Name} ホタ 8T ／ 台 8T" : $"{w.Name} ホタ ／ 勝率・T")) + " |");
            Console.WriteLine("|---|" + string.Concat(hw.Select(_ => "---|")));
            foreach (var a in habl)
            {
                if (!res.ContainsKey((a.Name, hw[0].Name))) continue;
                Console.WriteLine($"| {a.Name} | " + string.Join(" | ", hw.Select(w =>
                {
                    var s = res[(a.Name, w.Name)]; var z = res[("規定", w.Name)];
                    double h = HotaDmg(s, w), h0 = HotaDmg(z, w);
                    string dh = a.Name == "規定" || h0 == 0 ? "" : $"（{Sg(100.0 * (h - h0) / h0)}%）";
                    if (w.Target) return $"{h:F0}{dh} ／ {Per(s.Dmg8, s.N)}";
                    string dw = a.Name == "規定" || Math.Abs(s.Win - z.Win) < 0.05 ? "" : $"（{Sg(s.Win - z.Win)}）";
                    return $"{h:F0}{dh} ／ {F1(s.Win)}{dw}・{s.WinTs}T";
                })) + " |");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
