using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using T323 = Tou323Diag;

// =====================================================================================
// tou325 —— 第325期「トウ E3 の規定化 ＋ 召喚獣の名前 ＋ 試遊の台（粉 隣あり ／ 隣なし）」。測定の期ではない（版は作らない）。
// 指示書は design/PHASE325_TOU_SETTLE_SPEC.md ／ 報告は design/PHASE325_TOU_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 tou325 cand     # §4: 試遊の2台の候補（トウ・カタ・クグ ＋ 2枚 × 席の2組）を近衛 ／ 大隊 ／ ボスで測る（seed 0..39）
//     dotnet run --project BattleSim -c Release 0 tou325 boards   # §4: 足した2台 × 波（seed 0..199）——勝率と、伝染 ／ 返す ／ 味方の濃さ ／ 焼の数
//     dotnet run --project BattleSim -c Release 0 tou325 guide    # §5: 試遊の感電の台 × 波（規定 ／ 第324期の規定・seed 0..199）——手引きの数字
//     dotnet run --project BattleSim -c Release 0 tou325 check    # 自己検査（規定 ＝ E3・旧の規定の固定・召喚獣の名前・足した2台）
//
// 駒・札・数値は1つも変えない（器具だけ）。台は `Presets.Playtest` を引くだけで写しを持たない（`cand` の候補だけはこの器具の中で組む）。
// =====================================================================================
static class Tou325Diag
{
    const int Seeds = 200;
    internal const string Near = "試遊・感電 粉 隣あり";
    internal const string Far = "試遊・感電 粉 隣なし";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        switch (mode)
        {
            case "cand": Cand(); return;
            case "boards": Boards(); return;
            case "guide": Guide(); return;
            case "check": Check(); return;
            default: Console.WriteLine("tou325: モードは cand / boards / guide / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    static bool Adj(Formation f, string a, string b)
    {
        var sa = f.Occupied().Where(o => o.Def.Id == a).Select(o => o.Slot).ToList();
        var sb = f.Occupied().Where(o => o.Def.Id == b).Select(o => o.Slot).ToList();
        return sa.Any(x => sb.Any(y => FormationRules.AreAdjacent(x, y)));
    }

    /// <summary>トウの隣にカタ ／ クグが何体いるか（0 ＝ 隣なし・2 ＝ 隣あり）。</summary>
    static int NearCount(Formation f) => (Adj(f, "tou", "kata") ? 1 : 0) + (Adj(f, "tou", "kugu") ? 1 : 0);

    static S287.Wave W(string n) => T323.Waves.First(w => w.Name == n);
    static readonly string[] CandWaves = { "近衛", "大隊", "ボス" };

    static (int Wins, long WinT) Lite(Formation f, S287.Wave w, int n)
    {
        int wins = 0; long t = 0;
        Parallel.For(0, n, sd =>
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
            if (r.PlayerWon) { Interlocked.Increment(ref wins); Interlocked.Add(ref t, r.Turns); }
        });
        return (wins, t);
    }

    // ---------------------------------------------------------------------------------
    // §4 候補: 駒が同じ5体で、トウの隣にカタとクグがいる席（2）と、どちらもいない席（0）
    // ---------------------------------------------------------------------------------
    static void Cand()
    {
        const int n = 40;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var partners = new[] { UnitCatalog.Kubi, UnitCatalog.Som, UnitCatalog.ShigaH325, UnitCatalog.Hagi, UnitCatalog.Tsugi, UnitCatalog.Gan, UnitCatalog.Sora, UnitCatalog.Gald, UnitCatalog.Beni, UnitCatalog.Doha };
        var pairs = new List<(UnitDef A, UnitDef B)>();
        for (int i = 0; i < partners.Length; i++) for (int j = i + 1; j < partners.Length; j++) pairs.Add((partners[i], partners[j]));
        var waves = CandWaves.Select(W).ToArray();
        var rows = new List<(string Pair, Formation N, int[] Nw, double NbT, Formation F, int[] Fw, double FbT, int[] On, int[] Of)>();
        foreach (var (a, b) in pairs)
        {
            var five = new[] { UnitCatalog.TouH326, UnitCatalog.Kata, UnitCatalog.KuguH327, a, b };   // 第327期: トウを第325〜326期の規定に固定・第328期: クグも（電池の前）
            var seats = B283.Perms(five).Select(B283.Seat).Where(f => NearCount(f) is 0 or 2).ToList();
            var res = new (int[] W, double BossT)[seats.Count];
            for (int s = 0; s < seats.Count; s++)
            {
                var wins = new int[waves.Length]; double bt = 0;
                for (int k = 0; k < waves.Length; k++)
                {
                    var (wn, t) = Lite(seats[s], waves[k], n);
                    wins[k] = wn;
                    if (waves[k].Boss) bt = wn == 0 ? 99 : (double)t / wn;
                }
                res[s] = (wins, bt);
            }
            // 席の選び方（決めたこと）: クラスごとに 3 波の勝ちの合計がいちばん多い席・同じならボスの倒しT が短い席・同じなら席の名前の順
            (Formation F, int[] W, double T) Best(int cls)
            {
                var c = Enumerable.Range(0, seats.Count).Where(s => NearCount(seats[s]) == cls)
                    .OrderByDescending(s => res[s].W.Sum()).ThenBy(s => res[s].BossT).ThenBy(s => Seats(seats[s]), StringComparer.Ordinal).First();
                return (seats[c], res[c].W, res[c].BossT);
            }
            var bn = Best(2); var bf = Best(0);
            // 同じ2席を第324期の規定（トウ ／ カタ ／ クグが旧）で回す——席そのものの差と、E3 が足した差を分けるため
            int[] Old(Formation f) => waves.Select(w => Lite(Pin324(f), w, n).Wins).ToArray();
            rows.Add(($"{Short(a)} ＋ {Short(b)}", bn.F, bn.W, bn.T, bf.F, bf.W, bf.T, Old(bn.F), Old(bf.F)));
        }
        Console.WriteLine($"# 第325期 §4 試遊の2台の候補（トウ・カタ・クグ ＋ 2枚・seed 0..{n - 1}・各組 {CandWaves.Length} 波）");
        Console.WriteLine();
        Console.WriteLine("隣あり ＝ トウの隣にカタとクグの両方がいる席・隣なし ＝ どちらもいない席。各クラスで 3 波の勝ちの合計がいちばん多い席（同じならボスの倒しT が短い席）。");
        Console.WriteLine("差 ＝ 隣あり − 隣なし（勝率 pt）・ボスT ＝ ボスの倒しT（隣あり ／ 隣なし）。第324期 ＝ 同じ2席を第324期の規定で回した差（席そのものの差）。E3 の差 ＝ 差 − 第324期の差（近衛 ＋ 大隊 ＋ ボス）。");
        Console.WriteLine("並びは「E3 の差」の大きい順（同じなら「差の合計 ＋ ボスの倒しT の差 × 5」）。");
        Console.WriteLine();
        Console.WriteLine("| 組 | 隣あり の席 | 隣なし の席 | 近衛 あり ／ なし | 大隊 あり ／ なし | ボス あり ／ なし | ボスT あり ／ なし | 精鋭の差 | ボスの差 | 第324期の差 近衛 ／ 大隊 ／ ボス | E3 の差 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|--:|---|--:|");
        double P(int w) => 100.0 * w / n;
        double Gap(int[] a, int[] b) => Enumerable.Range(0, 3).Sum(k => P(a[k]) - P(b[k]));
        foreach (var r in rows.OrderByDescending(r => Gap(r.Nw, r.Fw) - Gap(r.On, r.Of)).ThenByDescending(r => Gap(r.Nw, r.Fw) + 5 * (Math.Min(r.FbT, 40) - Math.Min(r.NbT, 40))))
        {
            string T(double t) => t >= 99 ? "—" : t.ToString("F1");
            Console.WriteLine($"| {r.Pair} | {Seats(r.N)} | {Seats(r.F)} | {F1(P(r.Nw[0]))} ／ {F1(P(r.Fw[0]))} | {F1(P(r.Nw[1]))} ／ {F1(P(r.Fw[1]))} | {F1(P(r.Nw[2]))} ／ {F1(P(r.Fw[2]))} | {T(r.NbT)} ／ {T(r.FbT)} | {F1(P(r.Nw[0]) + P(r.Nw[1]) - P(r.Fw[0]) - P(r.Fw[1]))} | {F1(P(r.Nw[2]) - P(r.Fw[2]))} | {F1(P(r.On[0]) - P(r.Of[0]))} ／ {F1(P(r.On[1]) - P(r.Of[1]))} ／ {F1(P(r.On[2]) - P(r.Of[2]))} | {F1(Gap(r.Nw, r.Fw) - Gap(r.On, r.Of))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §4 足した2台 × 波（seed 0..199）
    // ---------------------------------------------------------------------------------
    static (string Name, Formation F)[] Added() => new[] { Near, Far }.Select(n => (n, Presets.Playtest.First(r => r.Name == n).F)).ToArray();

    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Added().Select(a => (a.Name, F: Pin325(a.F))).ToArray();   // 第327期: トウ（とシガ）を第325期の規定に固定
        Console.WriteLine("# 第325期 §4 足した試遊の2台（規定 ＝ E3・seed 0..199・1戦あたり）");
        Console.WriteLine();
        foreach (var (n, f) in boards)
            Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）・トウ×カタ {(Adj(f, "tou", "kata") ? "隣" : "離れ")}・トウ×クグ {(Adj(f, "tou", "kugu") ? "隣" : "離れ")}");
        Console.WriteLine();
        Console.WriteLine("伝染 ＝ 伝染で新しく帯電した敵。返した ＝ カタが雷で移した濃さ（量）＋ クグが糸で敵へ流した濃さ。味方の濃さ ＝ 漏れの粉で味方に付いた濃さの延べ ／ そのうち味方に残った分（付いた − カタが雷で移した・クグの分は糸で直に敵へ流れるので味方に付かない）／ 濃さ ≧1 のまま弾けた味方（重い放電 ＋ 焼の元）。焼 ＝ 濃い雷が弾けた本人を焼いた量（敵 ／ 味方）。");
        Console.WriteLine("決着T ＝ 勝った戦の倒しT ／ 負けた戦の負けT。第324期 ＝ 同じ台を第324期の規定（`PlaytestH324`・トウ ／ カタ ／ クグが旧）で回した勝率。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | 勝率 | 第324期 | 決着T 勝ち ／ 負け | 伝染で帯電した敵 | 返した カタ ／ クグ | 味方の濃さ 付いた ／ 残った ／ 濃いまま弾けた | 焼 敵 ／ 味方 | 味方の弾け | 倒れた味方 |");
        Console.WriteLine("|---|---|--:|--:|---|--:|---|---|---|--:|--:|");
        var old = PlaytestH324.ToDictionary(r => r.Name, r => r.F);
        foreach (var w in T323.Waves)
            foreach (var (n, f) in boards)
            {
                var z = T323.Measure(f, w, Seeds);
                Console.WriteLine($"| {w.Name} | {n.Replace("試遊・感電 粉 ", "")} | {F1(z.Win)} | {F1(100.0 * Lite(old[n], w, Seeds).Wins / Seeds)} | {Per(z.WinT, z.Wins)} ／ {Per(z.LoseT, z.N - z.Wins)} | {Per(z.T.TpNewFoe, z.N)} | {Per(z.K.TdKataMoved, z.N)} ／ {Per(z.G.TdKuguFlowed, z.N)} | {Per(z.T.TdGainAlly, z.N)} ／ {Per(z.T.TdGainAlly - z.K.TdKataMoved, z.N)} ／ {Per(z.T.TdPopAlly, z.N)} | {Per(z.T.TdBurnFoe, z.N)} ／ {Per(z.T.TdBurnAlly, z.N)} | {Per(z.AllyPopsEv, z.N)} | {Per(z.AllyDeaths, z.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5 手引き: 試遊の感電の台 × 波 —— 規定（E3）と第324期の規定（`PlaytestH324`）の勝率・決着T
    // ---------------------------------------------------------------------------------
    static void Guide()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var now = PlaytestH325.Where(r => r.Name.StartsWith("試遊・感電", StringComparison.Ordinal)).ToArray();   // 第326期: シガを第325期の規定に固定
        var old = PlaytestH324.ToDictionary(r => r.Name, r => r.F);
        Console.WriteLine("# 第325期 §5 試遊の感電の台 × 波（seed 0..199）—— 規定（E3）と第324期の規定");
        Console.WriteLine();
        Console.WriteLine("セル ＝ 規定の勝率（第324期の規定との差）／ 勝った戦の決着T。トウのいない台は第324期と同じ（返す働きは濃さの保持者がいないと何も起きない）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | トウ | " + string.Join(" | ", T323.Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(T323.Waves.Select(_ => "---|")));
        foreach (var (n, f) in now)
        {
            bool tou = f.Occupied().Any(o => o.Def.Id == "tou");
            var cells = T323.Waves.Select(w =>
            {
                var (a, ta) = Lite(f, w, Seeds);
                var (b, _) = Lite(old[n], w, Seeds);
                double d = 100.0 * (a - b) / Seeds;
                return $"{F1(100.0 * a / Seeds)}（{(d == 0 ? "±0" : d.ToString("+0.0;-0.0"))}）／ {(a == 0 ? "—" : ((double)ta / a).ToString("F1"))}";
            });
            Console.WriteLine($"| {n.Replace("試遊・感電 ", "")} | {(tou ? (NearCount(f) == 2 ? "隣にカタ ／ クグ" : NearCount(f) == 1 ? "隣に1体" : "隣なし") : "—")} | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int bad = 0;
        void Ok(string what, bool ok) { Console.WriteLine($"{(ok ? "○" : "×")} {what}"); if (!ok) bad++; }
        UnitDef T = UnitCatalog.TouH326, K = UnitCatalog.Kata, G = UnitCatalog.KuguH327;   // 第327期: 第325期の規定のトウ（DR-a の前）・第328期: クグも（電池の前）
        bool Body(UnitDef a, UnitDef b) => a.Id == b.Id && a.Name == b.Name && a.MaxHp == b.MaxHp && a.Attack == b.Attack && a.Speed == b.Speed && a.Advances == b.Advances && a.Pattern == b.Pattern && a.Flavor == b.Flavor;
        Console.WriteLine("# 第325期 tou325 check");
        Console.WriteLine();
        Ok("(a) 規定のトウ ／ カタ ／ クグの札 ＝ 第324期の E3（`TouTDbBTPe` ／ `KataTDR` ／ `KuguTDR`）・体は第324期までの規定と同じ",
            T.Traits.SequenceEqual(UnitCatalog.TouTDbBTPe.Traits) && K.Traits.SequenceEqual(UnitCatalog.KataTDR.Traits) && G.Traits.SequenceEqual(UnitCatalog.KuguTDR.Traits)
            && Body(T, UnitCatalog.TouH324) && Body(K, UnitCatalog.KataH324) && Body(G, UnitCatalog.KuguH324)
            && T.Traits.SequenceEqual(UnitCatalog.TouH324.Traits.Concat(new[] { TraitId.PowderDense, TraitId.PowderDenseKeep, TraitId.PowderDenseBurn, TraitId.PowderContagion, TraitId.PowderMarkFoeOnly })));
        Ok("(b) 文面: トウ ＝ 第324期の叩き台 ＋ 痺れ ／ 萎縮の句（末尾）・MinusText は重ねた版と同じ・カタ ／ クグ ＝ 旧の規定の文面 ＋ 第323期の叩き台",
            T.PlusText == UnitCatalog.TouStackText + UnitCatalog.TouStunDauntText && T.PlusText.Contains("痺れ", StringComparison.Ordinal) && T.PlusText.EndsWith(UnitCatalog.ShockDauntText, StringComparison.Ordinal)
            && !UnitCatalog.TouStackText.Contains("痺れ", StringComparison.Ordinal) && T.MinusText == UnitCatalog.TouTDbBTPe.MinusText
            && K.PlusText == UnitCatalog.KataH324.PlusText + "。身に積もった粉も、雷に乗せて落とす" && G.PlusText == UnitCatalog.KuguH324.PlusText + "。身に積もった粉も、糸を伝って流れていく"
            && K.MinusText == UnitCatalog.KataH324.MinusText && G.MinusText == UnitCatalog.KuguH324.MinusText);
        Ok("(c) 旧の規定 `TouH324` ／ `KataH324` ／ `KuguH324` は `All` ／ `Retired` の外・`All` は規定の3体を持ち 52 枚・第323〜324期の版は旧の規定から作る",
            !new[] { UnitCatalog.TouH324, UnitCatalog.KataH324, UnitCatalog.KuguH324 }.Any(UnitCatalog.Everyone.Contains) && new[] { UnitCatalog.Tou, K, UnitCatalog.Kugu }.All(UnitCatalog.All.Contains) && UnitCatalog.All.Count == 52   // 第327期: `All` は規定（DR-a）のトウ
            && UnitCatalog.TouTPe.Traits.SequenceEqual(UnitCatalog.TouH324.Traits.Concat(new[] { TraitId.PowderContagion, TraitId.PowderMarkFoeOnly }))
            && UnitCatalog.KataTDR.Traits.SequenceEqual(UnitCatalog.KataH324.Traits.Append(TraitId.ThunderPowderReturn)) && UnitCatalog.KuguTDR.Traits.SequenceEqual(UnitCatalog.KuguH324.Traits.Append(TraitId.ThreadPowderFlow))
            && ReferenceEquals(UnitCatalog.KuguKWa, UnitCatalog.KuguH324) && UnitCatalog.KataLR.Traits.SequenceEqual(UnitCatalog.KataH324.Traits.Append(TraitId.LightningRod)));
        Ok("(d) 固定の口: `PinTKK324` が3体とも旧へ・古い口（`PinS317` ／ `PinSS318` ／ `PinSH321` ／ `Pin316`）の最後も旧へ・`PinKataTou311` は旧の規定（H324）からも第311期へ",
            new[] { T, K, G }.Select(PinTKK324).SequenceEqual(new[] { UnitCatalog.TouH324, UnitCatalog.KataH324, UnitCatalog.KuguH324 })
            && new Func<UnitDef, UnitDef>[] { PinS317, PinSS318, PinSH321, PinS319, PinDS316 }.All(p => ReferenceEquals(p(T), UnitCatalog.TouH324) && ReferenceEquals(p(K), UnitCatalog.KataH324) && ReferenceEquals(p(G), UnitCatalog.KuguH324))
            && ReferenceEquals(PinKataTou311(UnitCatalog.KataH324), UnitCatalog.KataH311) && ReferenceEquals(PinKataTou311(UnitCatalog.TouH324), UnitCatalog.TouH311) && ReferenceEquals(PinKataTou311(G), UnitCatalog.KuguH324)
            && ReferenceEquals(Kugu292Diag.Pin294(Formation.Build(front1: G, front3: T)).Occupied().First().Def, UnitCatalog.KuguKGb)
            && ReferenceEquals(Kugu292Diag.Pin294(Formation.Build(front1: UnitCatalog.KuguH324, front3: T))[1], UnitCatalog.TouH324));
        var texts = UnitCatalog.Everyone.Append(UnitCatalog.Fodder).SelectMany(d => new[] { d.PlusText, d.MinusText }).Where(s => s is not null).ToList();
        Ok("(e) 召喚獣の名前: `Fodder` の画面の名前 ＝ 「背いた召喚獣」・`Id` は \"fodder\" のまま・規定の駒の PlusText ／ MinusText に「召喚」の付かない「獣」が無い",
            UnitCatalog.Fodder.Name == "背いた召喚獣" && UnitCatalog.Fodder.Id == "fodder"
            && texts.All(s => !System.Text.RegularExpressions.Regex.IsMatch(s, "(?<!召喚)獣"))
            && UnitCatalog.Shiga.PlusText.Contains("背いた召喚獣の雷", StringComparison.Ordinal) && UnitCatalog.Som.MinusText.Contains("召喚獣は投げた雷", StringComparison.Ordinal));
        var pl = Presets.Playtest;
        var added = Added();
        string Ids(Formation f) => string.Join(",", f.Occupied().Select(o => o.Def.Id).OrderBy(x => x, StringComparer.Ordinal));
        Ok("(f) 試遊の台: 末尾に2台（全17台）・前の15台の名前は第320期と同じ並び・2台は同じ5体（規定の駒）で、隣あり ＝ トウの隣にカタとクグ・隣なし ＝ どちらもいない・DemoApp の `--demo-preset=` の部分一致で自分に当たる",
            pl.Length == 17 && pl[^2].Name == Near && pl[^1].Name == Far
            && pl.Take(15).Select(r => r.Name).SequenceEqual(new[] { "試遊・感電 火の型", "試遊・感電 雷の型", "試遊・感電 糸", "試遊・標 ボス台", "試遊・標 道中", "試遊・標 循環", "試遊・標 三人組", "試遊・標 守り型",
                "試遊・感電 光の盾", "試遊・感電 光の盾 重", "試遊・感電 光の盾 鞭", "試遊・感電 光の盾 重 鞭", "試遊・感電 打ち返し", "試遊・感電 追い打ち", "試遊・感電 瓦礫" })
            && Ids(added[0].F) == Ids(added[1].F) && added[0].F.Occupied().Count() == 5 && added.All(a => a.F.Occupied().All(o => UnitCatalog.All.Contains(o.Def)))
            && NearCount(added[0].F) == 2 && NearCount(added[1].F) == 0
            && added.All(a => Presets.Compare.Concat(Presets.Cross).Concat(pl).First(r => r.Name.Contains(a.Name, StringComparison.Ordinal)).Name == a.Name));
        bool same = true;
        foreach (var (_, f) in added)
            foreach (var w in new[] { W("ボス"), W("近衛") })
                for (int sd = 0; sd < 4; sd++)
                {
                    var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                    same &= r0.PlayerWon == r1.PlayerWon && r0.Turns == r1.Turns;
                }
        Ok("(g) 足した2台 × ボス ／ 近衛 × seed 0..3: verbose の有無で勝敗 ／ 決着T が同じ", same);
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○" : $"× が {bad} 件");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
