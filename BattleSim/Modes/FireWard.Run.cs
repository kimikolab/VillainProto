using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BG = BorgGuardDiag;

// fireward run —— 段1 のまとめ・段2（上位の台を全ての波と倍率で）・表A〜H。
static partial class FireWardDiag
{
    static string SN(string id) { var n = UnitCatalog.ById(id).Name; int k = n.LastIndexOf('の'); return k >= 0 && k < n.Length - 1 ? n[(k + 1)..] : n; }
    static string PairName(Pick p) => string.Join("＋", p.Partners.Select(SN));
    static string ScoreText(Score4 s) => $"{s.S.Sv}/{BA.PickSeeds}・400/300 {Math.Max(0, s.Sv4)}/{BA.PickSeeds}・落 {s.S.Fell}";
    static string SeatText(string enc) => string.Join(" ／ ", enc.Split(',').Select((id, i) => $"{FormationRules.SeatNames[i]} {SN(id)}"));
    static string SeatOf(string enc, string id) { int i = Array.IndexOf(enc.Split(','), id); return i < 0 ? "—" : FormationRules.SeatNames[i]; }
    static string SeatOfF(Formation f, string id) { foreach (var (s, d) in f.Occupied()) if (d.Id == id) return FormationRules.SeatNames[s]; return "—"; }
    static bool HiyoBack(string enc) { int i = Array.IndexOf(enc.Split(','), "hiyo"); return i is 3 or 4; }
    static string Pct(long a, long b) => b == 0 ? "—" : BA.F1(100.0 * a / b) + "%";

    sealed record Board(string Ver, string Kind, string Label, Formation F, Score4? S);

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var t3 = new Dictionary<string, List<Pick>>();
        var secs = new Dictionary<string, double>();
        foreach (var v in Versions) { t3[v.Name] = Ranked(PicksOf(v, out double a)); secs[v.Name] = a; }
        var roles = BA.ObserveRoles(BA.Candidates);

        Console.WriteLine("# 第238期 段1・段2・表A〜H");
        Console.WriteLine();
        Console.WriteLine($"段1: 九 / 新兵 × 200/200 × seed {BA.PickSeed0}..{BA.PickSeed0 + BA.PickSeeds - 1}（{BA.PickSeeds} 本）・版ごとに T3 1,081 組 × 席 120。所要 "
            + string.Join("・", Versions.Select(v => $"{v.Name} {secs[v.Name] / 60:F1} 分")) + "。");
        Console.WriteLine("並び: **200/200 の全員生存 → 400/300 の全員生存（同じ seed・200/200 の最良と同値の席だけに回す）→ 落ちた駒（200/200）→ 決着T → 組の列挙順**。T3′ は T3 の表からシオ・ササを含む組を除いたもの、T3-1 はシオ＋ササ。段2・表は seed 0..199。");
        Console.WriteLine();

        // 段1 のまとめ
        Console.WriteLine("## 段1 のまとめ（総当たりの1位・ボルグとヒヨの席・ボルグが前列にいる席の最良）");
        Console.WriteLine();
        Console.WriteLine("| 版 | T3 の1位 | ボルグ | ヒヨ | T3 上位10 の前列 | T3′ の1位 | ボルグ | ヒヨ | T3′ 上位10 の前列 | T3′ 1位の前列の最良 | T3-1（順位） | T3 1位と同値 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var v in Versions)
        {
            var all = t3[v.Name]; var p3 = all.Where(IsT3P).ToList(); var t31 = all.First(IsT31);
            Console.WriteLine($"| {v.Name} | {PairName(all[0])} {ScoreText(all[0].BestS)} | {SeatOf(all[0].Best, "borg")} | {SeatOf(all[0].Best, "hiyo")} | {all.Take(10).Count(p => p.BestIsFront)} | "
                + $"{PairName(p3[0])} {ScoreText(p3[0].BestS)} | {SeatOf(p3[0].Best, "borg")} | {SeatOf(p3[0].Best, "hiyo")} | {p3.Take(10).Count(p => p.BestIsFront)} | "
                + $"{(p3[0].BestIsFront ? "（1位と同じ）" : $"{ScoreText(p3[0].FrontS)}・{SeatOf(p3[0].Front, "borg")}")} | {ScoreText(t31.BestS)}（{all.IndexOf(t31) + 1} 位） | {all.Count(p => p.BestS.Key == all[0].BestS.Key)} |");
        }
        Console.WriteLine();
        int hiyoBack = Versions.Count(v => HiyoBack(t3[v.Name][0].Best)), hiyoBackP = Versions.Count(v => HiyoBack(t3[v.Name].Where(IsT3P).First().Best));
        Console.WriteLine($"ヒヨが後列（後1 ／ 後3）: T3 の1位 {hiyoBack}/{Versions.Length} 版・T3′ の1位 {hiyoBackP}/{Versions.Length} 版。"
            + $" 上位10 のヒヨの席（全版の T3 ＋ T3′）: " + string.Join("・", Versions.SelectMany(v => t3[v.Name].Take(10).Concat(t3[v.Name].Where(IsT3P).Take(10)))
                .GroupBy(p => SeatOf(p.Best, "hiyo")).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")) + "。");
        Console.WriteLine();

        foreach (var v in Versions)
        {
            Console.WriteLine($"## 段1 {v.Name}（{v.What}）");
            Console.WriteLine();
            foreach (var (title, list) in new[] { ("T3", t3[v.Name]), ("T3′（シオ・ササ抜き）", t3[v.Name].Where(IsT3P).ToList()) })
            {
                Console.WriteLine($"### {title} の上位 10");
                Console.WriteLine();
                Console.WriteLine("| 順 | 相方 | 役割 | 1位の席 | 1位 | ボルグ前列の最良 |");
                Console.WriteLine("|---|---|---|---|---|---|");
                for (int i = 0; i < Math.Min(10, list.Count); i++)
                {
                    var p = list[i];
                    Console.WriteLine($"| {i + 1} | {PairName(p)} | {string.Join(" ／ ", p.Partners.Select(id => BA.RoleText(roles[id].Roles)))} | {SeatText(p.Best)} | {ScoreText(p.BestS)} | "
                        + $"{(p.BestIsFront ? "（1位と同じ）" : $"{ScoreText(p.FrontS)}（{SeatOf(p.Front, "borg")}）")} |");
                }
                Console.WriteLine();
                Console.WriteLine($"全 {list.Count} 組の 1位の席のうちボルグが前列 {list.Count(p => p.BestIsFront)} 組・上位 10 では {list.Take(10).Count(p => p.BestIsFront)}。"
                    + $" 200/200 で {BA.PickSeeds}/{BA.PickSeeds} の組 {list.Count(p => p.BestS.S.Sv == BA.PickSeeds)}・そのうち 400/300 も {BA.PickSeeds}/{BA.PickSeeds} {list.Count(p => p.BestS.S.Sv == BA.PickSeeds && p.BestS.Sv4 == BA.PickSeeds)}。"
                    + $" 1位と同値の組 {list.Count(p => p.BestS.Key == list[0].BestS.Key)}。");
                Console.WriteLine();
            }
        }

        // 表G
        Console.WriteLine("## 表G 相方の役割（上位 10 組のうち、その役割の駒を1枚でも含む組の数・第233期と同じ機械の分け方）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 台 | " + string.Join(" | ", BA.RoleNames) + " | シオ | ササ | ベニ | ゴルム |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", BA.RoleNames.Length + 4)));
        foreach (var v in Versions)
            foreach (var (tn, list) in new[] { ("T3", t3[v.Name]), ("T3′", t3[v.Name].Where(IsT3P).ToList()) })
            {
                var top = list.Take(10).ToList();
                Console.WriteLine($"| {v.Name} | {tn} | " + string.Join(" | ", BA.RoleNames.Select((_, r) => top.Count(p => p.Partners.Any(id => roles[id].Roles[r])).ToString()))
                    + $" | {top.Count(p => p.Partners.Contains("shio"))} | {top.Count(p => p.Partners.Contains("sasa"))} | {top.Count(p => p.Partners.Contains("beni"))} | {top.Count(p => p.Partners.Contains("golm"))} |");
            }
        Console.WriteLine();

        // 段2 の台
        var boards = new List<Board>();
        foreach (var v in Versions)
        {
            var all = t3[v.Name]; var p3 = all.Where(IsT3P).ToList(); var t31 = all.First(IsT31);
            void AddB(string kind, string label, Pick p, bool front)
            {
                string enc = front ? p.Front : p.Best;
                if (boards.Any(b => b.Ver == v.Name && Enc(b.F) == enc)) return;
                boards.Add(new Board(v.Name, kind, label, Dec(enc, v), front ? p.FrontS : p.BestS));
            }
            for (int i = 0; i < Math.Min(5, all.Count); i++) AddB("T3", $"T3-{i + 1} {PairName(all[i])}", all[i], false);
            for (int i = 0; i < Math.Min(5, p3.Count); i++) AddB("T3′", $"T3′-{i + 1} {PairName(p3[i])}", p3[i], false);
            AddB("T3-1", "T3-1 シオ＋ササ", t31, false);
            if (!all[0].BestIsFront) AddB("T3", $"T3-1位 {PairName(all[0])}・前列", all[0], true);
            if (!p3[0].BestIsFront) AddB("T3′", $"T3′-1位 {PairName(p3[0])}・前列", p3[0], true);
            if (v.Hiyo == UnitCatalog.Hiyo) boards.Add(new Board(v.Name, "雷置換", "雷 前1 シガ → ボルグ", BF_ThunderSwap(v.Borg), null));
        }
        boards.Add(new Board("参考", "参考", "雷（ポンの席）", BA.RefThunder, null));
        boards.Add(new Board("参考", "参考", "移動（第232期の規定）", BA.RefMove, null));

        var res = new Dictionary<(int B, int W, int S), ZAgg>();
        for (int bi = 0; bi < boards.Count; bi++)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    res[(bi, w, s)] = Measure(boards[bi].F, w, BA.Scales[s].Sc);
        ZAgg Group(int bi, int s, params int[] ws) { var a = new ZAgg(); foreach (int w in ws) a.Merge(res[(bi, w, s)]); return a; }
        int[] Main = { 0, 1, 2, 3 };
        const int MW = BA.MainWave;
        double MainAvg(int bi, int s) => Main.Average(w => res[(bi, w, s)].A.Surv);

        Console.WriteLine("## 段2 の台（席）");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | 席 | 段1 |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
            Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {BA.SeatsNamed(boards[bi].F)} | {(boards[bi].S is { } s ? ScoreText(s) : "—")} |");
        Console.WriteLine();

        // 表A
        Console.WriteLine("## 表A 全員生存 ／ 勝率（seed 0..199）・ボルグとヒヨの席");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | ボルグ | ヒヨ | 九/新兵 200/200 | 九/新兵 400/300 | 九/新兵 115/115 | 九/農兵 200/200 | 本編 第2〜5波 200/200 の平均 | 本編 400/300 の平均 | 本編 115/115 の平均 | 決着T（主） | 落ちた駒/戦（主） | 落ちた駒（400/300） |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            string C(int w, int s) { var a = res[(bi, w, s)].A; return $"{BA.F1(a.Surv)} ／ {BA.F1(a.Win)}"; }
            string M(int s) { var cs = Main.Select(w => res[(bi, w, s)].A).ToList(); return $"{BA.F1(cs.Average(a => a.Surv))} ／ {BA.F1(cs.Average(a => a.Win))}"; }
            var m = res[(bi, MW, 0)].A; var m4 = res[(bi, MW, 1)].A;
            Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {SeatOfF(boards[bi].F, "borg")} | {SeatOfF(boards[bi].F, "hiyo")} | {C(MW, 0)} | {C(MW, 1)} | {C(MW, 2)} | {C(5, 0)} | {M(0)} | {M(1)} | {M(2)} | {BA.F2(m.WinT)} | {BA.F2(m.Per(m.FellSum))} | "
                + string.Join("・", m4.Fell.OrderByDescending(kv => kv.Value).Select(kv => $"{SN(kv.Key)} {BA.F1(100.0 * kv.Value / m4.N)}%")) + " |");
        }
        Console.WriteLine();

        // 表A'' 版ごとの最良
        Console.WriteLine("### 表A'' 版ごとの最良（段2）");
        Console.WriteLine();
        Console.WriteLine("主判定（九/新兵 200/200）の最良と、負荷試験（九/新兵 400/300）の最良を別々に選ぶ。括弧は同じ台の 400/300 ／ 本編 200/200 の平均 ／ 本編 400/300 の平均。");
        Console.WriteLine();
        Console.WriteLine("| 版 | T3 の最良（200/200） | T3 の最良（400/300） | T3′ の最良（200/200） | T3′ の最良（400/300） | 雷 前1 シガ → ボルグ |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var v in Versions)
        {
            string BestOf(Func<Board, bool> pred, int s)
            {
                var ix = Enumerable.Range(0, boards.Count).Where(i => boards[i].Ver == v.Name && pred(boards[i])).ToList();
                if (ix.Count == 0) return "—";
                int b = ix.OrderByDescending(i => res[(i, MW, s)].A.Surv).ThenByDescending(i => res[(i, MW, 1 - s)].A.Surv).ThenBy(i => i).First();
                return $"{BA.F1(res[(b, MW, s)].A.Surv)}（{BA.F1(res[(b, MW, 1)].A.Surv)} ／ {BA.F1(MainAvg(b, 0))} ／ {BA.F1(MainAvg(b, 1))}）#{b + 1}・ボルグ{SeatOfF(boards[b].F, "borg")}";
            }
            Console.WriteLine($"| {v.Name} | {BestOf(b => b.Kind is "T3" or "T3-1", 0)} | {BestOf(b => b.Kind is "T3" or "T3-1" or "T3′", 1)} | {BestOf(b => b.Kind == "T3′", 0)} | {BestOf(b => b.Kind == "T3′", 1)} | {BestOf(b => b.Kind == "雷置換", 0)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 表A''' 同じ席で版を並べる（T3 と T3′ の A の上位の席を、全版の駒で測り直す・九/新兵）");
        Console.WriteLine();
        var fixedSeats = new List<(string Label, string Enc)>();
        foreach (var (lab, list) in new[] { ("T3", t3["A"]), ("T3′", t3["A"].Where(IsT3P).ToList()) })
            for (int i = 0; i < 3; i++) fixedSeats.Add(($"A の {lab}-{i + 1} {PairName(list[i])}（{SeatText(list[i].Best)}）", list[i].Best));
        foreach (var v in new[] { "A+D2+V1" })
        {
            var pl = t3[v].Where(IsT3P).ToList();
            fixedSeats.Add(($"{v} の T3′-1 {PairName(pl[0])}（{SeatText(pl[0].Best)}）", pl[0].Best));
        }
        Console.WriteLine("| 席 | " + string.Join(" | ", Versions.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", Versions.Length)));
        var fixedRes = new Dictionary<(int, string, int), ZAgg>();
        for (int k = 0; k < fixedSeats.Count; k++)
            foreach (var v in Versions)
                foreach (int s in new[] { 0, 1 })
                    fixedRes[(k, v.Name, s)] = Measure(Dec(fixedSeats[k].Enc, v), MW, BA.Scales[s].Sc);
        for (int k = 0; k < fixedSeats.Count; k++)
            Console.WriteLine($"| {fixedSeats[k].Label} | " + string.Join(" | ", Versions.Select(v => $"{BA.F1(fixedRes[(k, v.Name, 0)].A.Surv)} ／ {BA.F1(fixedRes[(k, v.Name, 1)].A.Surv)}")) + " |");
        Console.WriteLine();
        Console.WriteLine("（各セル 200/200 ／ 400/300 の全員生存）");
        Console.WriteLine();
        Console.WriteLine("### 表A'''' 同じ席の燃える巻き込み（九/新兵 200/200・/戦）: 普通に受けた巻き込み ／ 変換で癒えた ／ 相方の倒れ ／ 盾の配りで切った");
        Console.WriteLine();
        Console.WriteLine("| 席 | " + string.Join(" | ", Versions.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", Versions.Length)));
        for (int k = 0; k < fixedSeats.Count; k++)
            Console.WriteLine($"| {fixedSeats[k].Label} | " + string.Join(" | ", Versions.Select(v =>
            {
                var z = fixedRes[(k, v.Name, 0)]; var a = z.A;
                return $"{BA.F1(a.Per(z.Y.SplashTaken))} ／ {BA.F1(a.Per(z.ConvSplashHealed + z.ConvTickHealed))} ／ {BA.F2(a.Per(z.FellBy.GetValueOrDefault("相方")))} ／ {BA.F1(a.Per(z.WardSaved))}";
            })) + " |");
        Console.WriteLine();

        // 表B
        Console.WriteLine("## 表B 死因・誰が倒れたか（/戦）・検算（死因の合計 ＝ 倒れた駒）");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MW }), ("九 / 新兵 × 400/300", 1, new[] { MW }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 版 | 台 | 倒れた/戦 | ボルグ | ホタ | ヒヨ | 相方 | 敵の攻撃 | 巻き込み 物理 | 巻き込み 燃焼 | 味方のその他 | 燃焼の刻み | 毒の刻み | その他・不明 | 相方の死因（敵／巻き込み／刻み） | 検算 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                if (boards[bi].Kind == "参考") continue;
                var z = Group(bi, s, ws); var y = z.Y; var a = z.A;
                bool splitOk = y.SplashPhysDeaths + y.SplashBurnDeaths == a.Cause[BA.CBorgSplash];
                bool roleOk = z.FellBy.Values.Sum() == a.DeathEvents && z.CauseByRole.Values.Sum(x => x.Sum()) == a.DeathEvents;
                string chk = a.Cause.Sum() == a.DeathEvents && a.DeathEvents == a.FellSum && splitOk && roleOk ? "一致" : $"**不一致 {a.Cause.Sum()}/{a.DeathEvents}/{(splitOk ? "" : "巻き込み")}{(roleOk ? "" : "役")}**";
                string P(long x) => x == 0 ? "0" : BA.F2(a.Per(x));
                var pc = z.CauseByRole.GetValueOrDefault("相方") ?? new long[BA.Causes.Length];
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {BA.F2(a.Per(a.DeathEvents))} | " + string.Join(" | ", ZAgg.Roles.Select(r => P(z.FellBy.GetValueOrDefault(r))))
                    + $" | {P(a.Cause.Take(5).Sum())} | {P(y.SplashPhysDeaths)} | {P(y.SplashBurnDeaths)} | {P(a.Cause[6])} | {P(a.Cause[BA.CBurnTick])} | {P(a.Cause[BA.CPoisonTick])} | {P(a.Cause[BA.CUnknown])} | "
                    + $"{P(pc.Take(5).Sum())}／{P(pc[BA.CBorgSplash])}／{P(pc[BA.CBurnTick])} | {chk} |");
            }
            Console.WriteLine();
        }

        // 表C
        Console.WriteLine("## 表C 盾の配り・火の変換（/戦）");
        Console.WriteLine();
        Console.WriteLine("盾の配り ＝ 半分にした一撃の数 ／ 切った量（受け手ごと）。火の変換 ＝ 名目 → 癒えた（刻み・起爆 ／ 燃える巻き込み）。優先順位 ＝ ヒヨ（V）がいるときに燃えている味方の燃焼ダメージがどの規則に回ったか（名目）。");
        Console.WriteLine("燃える巻き込みの「普通に受けた」＝ 焼かれず・反転も変換もされずに HP を削った量。");
        Console.WriteLine();
        foreach (var (gname, s, ws) in new[] { ("九 / 新兵 × 200/200", 0, new[] { MW }), ("九 / 新兵 × 400/300", 1, new[] { MW }), ("本編 第2〜5波 × 200/200", 0, Main) })
        {
            Console.WriteLine($"### {gname}");
            Console.WriteLine();
            Console.WriteLine("| # | 版 | 台 | 盾の配り 回 ／ 切った | 受け手ごと | 変換 刻み 名目 → 癒えた | 変換 巻き込み 名目 → 癒えた | 優先 焼かれない ／ 火の癒し ／ ベニ ／ 変換 | 燃える巻き込み 名目 ／ 普通に受けた | 燃焼の刻み（ボルグ以外が受けた） | 火の鎧の半減 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
            for (int bi = 0; bi < boards.Count; bi++)
            {
                if (boards[bi].Kind == "参考") continue;
                var z = Group(bi, s, ws); var a = z.A;
                string F(long x) => BA.F1(a.Per(x));
                long burnTick = a.Taken.Where(kv => kv.Key != UnitCatalog.Borg.Name).Sum(kv => kv.Value[3]);
                Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {BA.F2(a.Per(z.WardHits))} ／ {F(z.WardSaved)} | "
                    + (z.WardTakenBy.Count == 0 ? "—" : string.Join("・", z.WardTakenBy.OrderByDescending(kv => kv.Value).Select(kv => $"{SN(kv.Key)} {F(kv.Value)}"))) + " | "
                    + $"{F(z.ConvTickNom)} → {F(z.ConvTickHealed)} | {F(z.ConvSplashNom)} → {F(z.ConvSplashHealed)} | {F(z.PrecPyre)} ／ {F(z.PrecMend)} ／ {F(z.PrecBeni)} ／ {F(z.PrecV)} | "
                    + $"{F(z.Y.SplashNominal)} ／ {F(z.Y.SplashTaken)} | {F(burnTick)} | {F(z.Y.X.Saved)} |");
            }
            Console.WriteLine();
        }

        // 表D
        Console.WriteLine("## 表D 燃えている味方（ターン頭・ボルグが生きているターンで割る）");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | 九/新兵 200/200 燃える味方/T ／ うちボルグの隣 | 400/300 | 本編 200/200 | ボルグが燃えていた（主） |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            if (boards[bi].Kind == "参考") continue;
            string D(ZAgg z) => z.CensusTurns == 0 ? "—" : $"{BA.F2((double)z.BurnAllies / z.CensusTurns)} ／ {BA.F2((double)z.BurnAdj / z.CensusTurns)}";
            var z0 = res[(bi, MW, 0)];
            Console.WriteLine($"| {bi + 1} | {boards[bi].Ver} | {boards[bi].Label} | {D(z0)} | {D(res[(bi, MW, 1)])} | {D(Group(bi, 0, Main))} | {Pct(z0.Y.X.BorgBurnTurns, z0.Y.X.BorgAliveTurns)} |");
        }
        Console.WriteLine();

        // 表H: V の a ／ b × 本編の第三波
        Console.WriteLine("## 表H 火の変換と渇き（V を持つ版の台 × 本編の第三波・a ＝ 渇きを素通り ／ b ＝ 渇きに封じられる）");
        Console.WriteLine();
        Console.WriteLine("| # | 版 | 台 | 倍率 | a 全員生存 ／ 勝率 | b 全員生存 ／ 勝率 | 変換で癒えた a ／ b | b で渇きに止まった回/戦 | 倒れた/戦 a ／ b |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Count; bi++)
        {
            var v = Versions.FirstOrDefault(x => x.Name == boards[bi].Ver);
            if (v is null || v.Hiyo == UnitCatalog.Hiyo || boards[bi].Kind is not ("T3" or "T3′")) continue;
            var fb = Dec(Enc(boards[bi].F), v.Borg, DryOf(v.Hiyo));
            foreach (int s in new[] { 0, 1, 2 })
            {
                var za = res[(bi, 1, s)]; var zb = Measure(fb, 1, BA.Scales[s].Sc);
                Console.WriteLine($"| {bi + 1} | {v.Name} | {boards[bi].Label} | {BA.Scales[s].Name} | {BA.F1(za.A.Surv)} ／ {BA.F1(za.A.Win)} | {BA.F1(zb.A.Surv)} ／ {BA.F1(zb.A.Win)} | "
                    + $"{BA.F1(za.A.Per(za.ConvTickHealed + za.ConvSplashHealed))} ／ {BA.F1(zb.A.Per(zb.ConvTickHealed + zb.ConvSplashHealed))} | {BA.F2(zb.A.Per(zb.ConvDry))} | {BA.F2(za.A.Per(za.A.DeathEvents))} ／ {BA.F2(zb.A.Per(zb.A.DeathEvents))} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒（段1 を除く）");
    }

    static Formation BF_ThunderSwap(UnitDef borg) => BorgFrontDiag.ThunderSwap(borg, true);
}
