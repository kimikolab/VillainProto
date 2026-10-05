using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using CW = CheckWaveDiag;

// =====================================================================================
// nomi277 —— 第277期「ノミの転生（豆鉄砲: 攻撃力を連撃の回数に変える）」。
// 指示書は design/PHASE277_NOMI_REBIRTH_SPEC.md ／ 報告は design/PHASE277_NOMI_REBIRTH.md。
//
//     dotnet run --project BattleSim -c Release 0 nomi277 run        # ノミ在席の `compare` 6 行・交差帯 2 行・燃焼の検証行 × 版 N0 ／ N1 ／ N2 × 本編第2〜5波・ボス規定形・チェック波（B3 ／ W3）× seed 0..199
//     dotnet run --project BattleSim -c Release 0 nomi277 bandb      # 帯B（seed 200..599）の追試: 9 台 × 版 × 本編第2〜5波の勝率
//     dotnet run --project BattleSim -c Release 0 nomi277 seat       # 燃焼×ノミの行の席: ボルグ入りの `compare` 行の火の駒以外の各枠に N0 ／ N1 を差して本編第2〜5波（情報セル・被弾の燃焼）
//     dotnet run --project BattleSim -c Release 0 nomi277 check      # 自己検査（弾数・1発の打点・刻みの回数・再行動の上限・N0 の写しが規定と台本一致）
//     dotnet run --project BattleSim -c Release 0 nomi277 log <行の番号 0..8> <版 N0|N1|N2> <波 2..5|ボス|B3|W3> [seed]   # 1戦のログ
// =====================================================================================
static class Nomi277Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "run";
        switch (mode)
        {
            case "run": RunAll(); return;
            case "check": Check(); return;
            case "bandb": BandB(); return;
            case "seat": Seat(); return;
            case "log": LogOne(args.Length > 3 ? int.Parse(args[3]) : 0, args.Length > 4 ? args[4] : "N1", args.Length > 5 ? args[5] : "3", args.Length > 6 ? int.Parse(args[6]) : 0); return;
            default: Console.WriteLine("nomi277: モードは run / check / log。"); return;
        }
    }

    const int Seeds = 200;

    internal static readonly (string Name, string What, UnitDef Nomi)[] Vers =
    {
        ("N0", "旧ノミ（対照・第276期までの規定）", UnitCatalog.NomiN0),
        ("N1", "豆鉄砲（1 点 × 攻撃力）・刻みは一振りに1回（第277期に規定）", UnitCatalog.NomiN1),
        ("N2", "豆鉄砲・1発ごとに刻む（傷の機関銃）", UnitCatalog.NomiN2),
    };

    /// <summary>燃焼の検証行: `燃焼 (ボルグ×ホタ)` の後1 ムド → ノミ。</summary>
    static Formation BurnRow(UnitDef nomi) => FvSwap(BA.RefBurn, UnitCatalog.Mudo, nomi);

    /// <summary>台（規定のノミで定義し、版はノミだけを差し替える）。群 ＝ compare ／ 交差帯 ／ 検証。</summary>
    static readonly (string Name, string Group, Func<UnitDef, Formation> Make)[] Boards = BuildBoards();
    static (string, string, Func<UnitDef, Formation>)[] BuildBoards()
    {
        var l = new List<(string, string, Func<UnitDef, Formation>)>();
        // 第277期の追記で `compare` 63 行目（燃焼×刻み）が立ったが、それは行8（検証）と同じ台なので compare 群からは外す（行の番号を測定のときのまま保つ）。
        foreach (var (n, f) in CompareBuilds().Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Nomi)) && !r.Name.StartsWith("燃焼×刻み")))
            l.Add((n, "compare", d => FvSwap(f, UnitCatalog.Nomi, d)));
        foreach (var (n, f) in CrossBuilds().Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Nomi))))
            l.Add((n, "交差帯", d => FvSwap(f, UnitCatalog.Nomi, d)));
        l.Add(("燃焼＋ノミ（燃焼の行の後1 ムド → ノミ）", "検証", BurnRow));
        return l.ToArray();
    }

    static readonly (string Name, string Group, Func<List<UnitState>> Make)[] Waves = BuildWaves();
    static (string, string, Func<List<UnitState>>)[] BuildWaves()
    {
        var l = new List<(string, string, Func<List<UnitState>>)>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
        {
            int ww = w;
            l.Add(($"第{w + 1}波", "本編", () => BattleEngine.Materialize(EnemyCatalog.Stages[ww].Enemy, BattleContext.EnemyTeam)));
        }
        l.Add(("ボス", "ボス", () => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None)));
        l.Add(("B3", "B3", CW.CheckWave(CW.CWaveOf(CW.DefaultBoss))));
        l.Add(("W3", "W3", CW.CheckWave(CW.CWaveOf(CW.DefaultHand))));
        return l.ToArray();
    }

    sealed class Agg
    {
        public long N, Wins, WinT, Surv, Dealt20;
        public long Volleys, Shots, Attacks, Encore, EncoreCap, Carved, NomiHit, NomiHitAmt, NomiBig, NomiBigAmt;
        public long BurnHitFires, BurnHitDmg, YokeCutN, YokeCutAmt, MartyrGain, MartyrFires, Switches, ReaderDealt, Turns;

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Surv += o.Surv; Dealt20 += o.Dealt20;
            Volleys += o.Volleys; Shots += o.Shots; Attacks += o.Attacks; Encore += o.Encore; EncoreCap += o.EncoreCap; Carved += o.Carved;
            NomiHit += o.NomiHit; NomiHitAmt += o.NomiHitAmt; NomiBig += o.NomiBig; NomiBigAmt += o.NomiBigAmt;
            BurnHitFires += o.BurnHitFires; BurnHitDmg += o.BurnHitDmg; YokeCutN += o.YokeCutN; YokeCutAmt += o.YokeCutAmt;
            MartyrGain += o.MartyrGain; MartyrFires += o.MartyrFires; Switches += o.Switches; ReaderDealt += o.ReaderDealt; Turns += o.Turns;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++;
            Turns += r.Turns;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) Surv++; }
            var foeIds = e.Select(u => u.InstanceId).ToHashSet();
            foreach (var ev in r.Events.Where(ev => ev.Kind == BattleEventKind.Summon && ev.Team == BattleContext.EnemyTeam))
                if (ev.TargetId is int t) foeIds.Add(t);
            if (r.PlayerWon)
                WinT += r.Events.Where(ev => ev.Kind == BattleEventKind.Death && ev.TargetId is int d && foeIds.Contains(d)).Select(ev => ev.Turn).DefaultIfEmpty(r.Turns).Max();
            int nomi = p.First(u => u.Def.Id == UnitCatalog.Nomi.Id).InstanceId;
            var egu = p.FirstOrDefault(u => u.Def.Id == UnitCatalog.Egu.Id)?.InstanceId;
            int last = -1;
            foreach (var ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.Damage && ev.TargetId is int t && foeIds.Contains(t))
                {
                    if (ev.Turn <= CW.BossTurns) Dealt20 += ev.Amount;
                    if (ev.ActorId == nomi)
                    {
                        // 1発の打点（N1/N2 は 1・破片に吸われた発は Damage を積まない）と、2 以上（なぞり・脆さ・N0 の素の一撃）を分ける。
                        NomiHit++; NomiHitAmt += ev.Amount;
                        if (ev.Amount > PelletTrait.ShotDamage) { NomiBig++; NomiBigAmt += ev.Amount; }
                        if (last >= 0 && last != t) Switches++;
                        last = t;
                    }
                    if (egu is int g && ev.ActorId == g) ReaderDealt += ev.Amount;
                }
                if (ev.Kind == BattleEventKind.Status && ev.ActorId == nomi && ev.Text == "燃焼") BurnHitDmg += ev.Amount;
                if (ev.Kind == BattleEventKind.Sealed && ev.Text == SealedLabels.Yoke && ev.ActorId == nomi) { YokeCutN++; YokeCutAmt += ev.Amount; }
            }
            if (r.TallyByUnit.TryGetValue(UnitCatalog.Nomi.Id, out var nt))
            {
                Volleys += nt.PelletVolleys; Shots += nt.PelletShots; Attacks += nt.Attacks;
                Encore += nt.EncoreFires; EncoreCap += nt.PelletEncoreCapped;
                Carved += nt.WoundWritesByRoute is { } wr ? wr[(int)WoundRoute.Carve] : 0;
            }
            if (r.BurnHit is { } bh && bh.ChanceBy.TryGetValue($"0:{UnitCatalog.Nomi.Id}", out var c)) BurnHitFires += c[0];
            foreach (var (id, t) in r.TallyByUnit)
                if (e.Any(u => u.Def.Id == id)) { MartyrGain += t.RedirectGain; MartyrFires += t.RedirectGainFires; }
        }

        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
    }

    static Agg Measure(Formation f, Func<List<UnitState>> make)
    {
        var parts = new Agg[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var (r, p, e) = CW.Fight(f, make, i, verbose: true);
            var a = new Agg(); a.Take(r, p, e); parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string WinT(Agg a) => a.Wins == 0 ? "—" : ((double)a.WinT / a.Wins).ToString("F2");

    static void RunAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = new Dictionary<(int, string, string), Agg>();
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers)
            {
                var f = Boards[bi].Make(v.Nomi);
                foreach (var w in Waves) res[(bi, v.Name, w.Name)] = Measure(f, w.Make);
            }

        Console.WriteLine("# 第277期 ノミの転生 —— ノミ在席の行 × 版 × 波（seed 0..199）");
        Console.WriteLine();
        for (int bi = 0; bi < Boards.Length; bi++)
            Console.WriteLine($"- 行{bi}（{Boards[bi].Group}）{Boards[bi].Name} ＝ " + BA.SeatsNamed(Boards[bi].Make(UnitCatalog.Nomi)));
        foreach (var v in Vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine("- 波: 本編第2〜5波（`compare` と同じ口・倍率 115/115）／ ボス ＝ 規定形（倍率なし）／ B3 ＝ チェック波 B3-桁 ／ W3 ＝ チェック波 W3-割合");
        Console.WriteLine();
        string head = "| 行 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " | 第2〜5波 平均 | N0 差 |";
        string sep = "|---|---|" + string.Concat(Waves.Select(_ => "--:|")) + "--:|--:|";

        Console.WriteLine("## 表1 勝率（%）");
        Console.WriteLine();
        Console.WriteLine(head); Console.WriteLine(sep);
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            double m0 = Waves.Where(w => w.Group == "本編").Select(w => res[(bi, "N0", w.Name)].Win).Average();
            foreach (var v in Vers)
            {
                double main = Waves.Where(w => w.Group == "本編").Select(w => res[(bi, v.Name, w.Name)].Win).Average();
                string d = v.Name == "N0" ? "" : (main - m0).ToString("+0.0;-0.0;0.0");
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", Waves.Select(w => F1(res[(bi, v.Name, w.Name)].Win))) + $" | {F1(main)} | {d} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 表2 倒しT（勝った戦で最後の敵が倒れたターンの平均）と 敵へ入れた HP（1〜20 ターン・全員の合計・1戦平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name + " 倒しT")) + " | ボス HP | B3 HP | W3 HP |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")) + "--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers)
            {
                string hp(string w) { var a = res[(bi, v.Name, w)]; return ((double)a.Dealt20 / a.N).ToString("F0"); }
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", Waves.Select(w => WinT(res[(bi, v.Name, w.Name)]))) + $" | {hp("ボス")} | {hp("B3")} | {hp("W3")} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 表3 機構の実測（1戦平均・群ごと）");
        Console.WriteLine();
        Console.WriteLine("振 ＝ 一振りの数（豆鉄砲）／ 発 ＝ 撃った発 ／ 攻 ＝ `PerformAttack` の回数（N0 はこれが振り）／ 再 ＝ もう一度動いた ／ 止 ＝ 一振りの2体目以降で止めた再行動 ／ 刻 ＝ 刻んだ傷 ／ ");
        Console.WriteLine("当 ＝ ノミの直撃の件数（打点の合計）／ 大 ＝ うち 2 以上（なぞり・N0 の素の一撃）／ 燃 ＝ ノミが起こした被弾の燃焼（そのダメージ）／ 軛 ＝ ノミの一撃が軛に切られた件数（切られた量）／ 殉 ＝ 敵の肩代わりの成長（回数）／ 切替 ＝ ノミの直撃の的が変わった回数 ／ エグ ＝ エグの与ダメ ／ T ＝ 決着T");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 群 | 振 | 発 | 攻 | 再 | 止 | 刻 | 当（量） | 大（量） | 燃（量） | 軛（量） | 殉（回） | 切替 | エグ | T |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
            foreach (var v in Vers)
                foreach (string g in new[] { "本編", "ボス", "B3", "W3" })
                {
                    var a = new Agg();
                    foreach (var w in Waves.Where(w => w.Group == g)) a.Merge(res[(bi, v.Name, w.Name)]);
                    Console.WriteLine($"| {bi} | {v.Name} | {g} | {Per(a.Volleys, a.N)} | {Per(a.Shots, a.N)} | {Per(a.Attacks, a.N)} | {Per(a.Encore, a.N)} | {Per(a.EncoreCap, a.N)} | {Per(a.Carved, a.N)} | {Per(a.NomiHit, a.N)}（{Per(a.NomiHitAmt, a.N)}）| {Per(a.NomiBig, a.N)}（{Per(a.NomiBigAmt, a.N)}）| {Per(a.BurnHitFires, a.N)}（{Per(a.BurnHitDmg, a.N)}）| {Per(a.YokeCutN, a.N)}（{Per(a.YokeCutAmt, a.N)}）| {Per(a.MartyrGain, a.N)}（{Per(a.MartyrFires, a.N)}）| {Per(a.Switches, a.N)} | {Per(a.ReaderDealt, a.N)} | {Per(a.Turns, a.N)} |");
                }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Check()
    {
        int bad = 0;
        void Ok(string what, bool ok) { Console.WriteLine($"{(ok ? "○" : "×")} {what}"); if (!ok) bad++; }

        // 第277期の追記で規定のノミを N1 にした——(a)(b)(c) は「規定 ＝ N1」を確かめる形に改めた（規定化の前は「規定 ＝ N0・保持者 0 枚」だった）。
        Ok("(a) 豆鉄砲・一振り1回の刻みの保持者は `UnitCatalog.Everyone` に規定のノミ1枚だけ",
           UnitCatalog.Everyone.Where(d => d.Traits.Contains(TraitId.Pellet) || d.Traits.Contains(TraitId.CarveOnce)).SequenceEqual(new[] { UnitCatalog.Nomi }));
        Ok("(b) N1 は規定のノミと定義が同じ（体・札・文）",
           UnitCatalog.NomiN1.MaxHp == UnitCatalog.Nomi.MaxHp && UnitCatalog.NomiN1.Attack == UnitCatalog.Nomi.Attack && UnitCatalog.NomiN1.Speed == UnitCatalog.Nomi.Speed
           && UnitCatalog.NomiN1.Traits.SequenceEqual(UnitCatalog.Nomi.Traits) && UnitCatalog.NomiN1.PlusText == UnitCatalog.Nomi.PlusText);

        // (c) N0 の写しで組んだ台と規定のノミの台の台本が一致（全台 × 本編第2〜5波 × seed 0..49）。
        bool same = true;
        foreach (var b in Boards)
            foreach (var w in Waves.Where(w => w.Group == "本編"))
                for (int s = 0; s < 50 && same; s++)
                {
                    var x = CW.Dig(CW.Fight(b.Make(UnitCatalog.Nomi), w.Make, s).R);
                    var y = CW.Dig(CW.Fight(b.Make(UnitCatalog.NomiN1), w.Make, s).R);
                    same &= x.SequenceEqual(y);
                }
        Ok("(c) N1 の写しの台本が規定のノミと一致（9 台 × 本編第2〜5波 × seed 0..49）", same);

        // (d)〜(h) N1 ／ N2 の1発の形。
        foreach (var v in Vers.Skip(1))
        {
            long volleys = 0, shots = 0, carve = 0, encore = 0, hits = 0, big = 0;
            foreach (var b in Boards)
                foreach (var w in Waves.Where(w => w.Group == "本編"))
                    for (int s = 0; s < 20; s++)
                    {
                        var (r, p, _) = CW.Fight(b.Make(v.Nomi), w.Make, s);
                        int id = p.First(u => u.Def.Id == UnitCatalog.Nomi.Id).InstanceId;
                        var t = r.TallyByUnit[UnitCatalog.Nomi.Id];
                        volleys += t.PelletVolleys; shots += t.PelletShots; encore += t.EncoreFires;
                        carve += t.WoundWritesByRoute is { } wr ? wr[(int)WoundRoute.Carve] : 0;
                        foreach (var ev in r.Events.Where(ev => ev.Kind == BattleEventKind.Damage && ev.ActorId == id))
                        {
                            hits++;
                            if (ev.Amount > PelletTrait.ShotDamage) big++;
                        }
                    }
            Ok($"(d) {v.Name}: 1振りあたりの発 ＝ {(double)shots / Math.Max(1, volleys):F2}（攻撃力 10 前後・強化と呪いで動く）", volleys > 0 && shots >= volleys * 5);
            Ok($"(e) {v.Name}: 再行動 {encore} ≦ 一振り {volleys}（1振り1回）", encore <= volleys);
            if (v.Name == "N1")
                Ok($"(f) N1: 刻んだ傷 {carve} ≦ 一振り {volleys}（刻みは一振りに1回）", carve <= volleys);
            else
                Ok($"(f) N2: 刻んだ傷 {carve} ＞ 一振り {volleys}（1発ごとに刻む）", carve > volleys);
            Ok($"(g) {v.Name}: 直撃 {hits} 件のうち 2 以上は {big} 件（なぞり・脆さ）", hits > big);
        }

        // (h) 乱数: 豆鉄砲の一振りは乱数を引かない——同じ seed の N1 を2度回して台本が一致（決定性）。
        bool det = true;
        foreach (var b in Boards.Take(3))
            for (int s = 0; s < 20; s++)
                det &= CW.Dig(CW.Fight(b.Make(UnitCatalog.NomiN1), Waves[2].Make, s).R).SequenceEqual(CW.Dig(CW.Fight(b.Make(UnitCatalog.NomiN1), Waves[2].Make, s).R));
        Ok("(h) N1 は seed 決定的（同じ seed の2戦の台本が一致）", det);

        // (i) verbose の有無で結果が変わらない（イベントを積む処理が盤面を変えていない）。
        bool vb = true;
        foreach (var b in Boards)
            foreach (var v in Vers.Skip(1))
                for (int s = 0; s < 20; s++)
                {
                    var a = CW.Fight(b.Make(v.Nomi), Waves[1].Make, s, verbose: true).R;
                    var c = CW.Fight(b.Make(v.Nomi), Waves[1].Make, s, verbose: false).R;
                    vb &= a.PlayerWon == c.PlayerWon && a.Turns == c.Turns;
                }
        Ok("(i) verbose の有無で勝敗と決着T が一致（N1 ／ N2 × 9 台 × 第3波 × seed 0..19）", vb);

        Console.WriteLine(bad == 0 ? "自己検査: すべて ○" : $"自己検査: × が {bad} 件");
    }

    /// <summary>帯B（seed 200..599）の追試: 9 台 × 版 × 本編第2〜5波の勝率（帯A の N0 差がノイズでないかを見る・verbose なし）。</summary>
    static void BandB()
    {
        Console.WriteLine("| 行 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | N0 差 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            double m0 = 0;
            foreach (var v in Vers)
            {
                var f = Boards[bi].Make(v.Nomi);
                var cells = new double[4];
                for (int w = 1; w <= 4; w++)
                {
                    int wins = 0;
                    Parallel.For(200, 600, s => { if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, s, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                    cells[w - 1] = 100.0 * wins / 400;
                }
                double m = cells.Average();
                if (v.Name == "N0") m0 = m;
                Console.WriteLine($"| {bi} | {v.Name} | " + string.Join(" | ", cells.Select(F1)) + $" | {F1(m)} | {(v.Name == "N0" ? "" : (m - m0).ToString("+0.0;-0.0;0.0"))} |");
            }
        }
    }

    /// <summary>
    /// 燃焼×ノミの行の席（`compare` に足す行を選ぶ・第277期の追記）。ボルグ（火の書き手）を含む `compare` 行の、火の駒（ボルグ・ホタ・ヒヨ）とノミ以外の各枠を N0 ／ N1 に差し替え、
    /// 本編第2〜5波 × seed 0..199（`compare` と同じ帯・verbose なし）の勝率・情報セル（0 &lt; x &lt; 100）・ノミが起こした被弾の燃焼（1戦平均）を並べる。
    /// </summary>
    static void Seat()
    {
        var fire = new HashSet<string> { UnitCatalog.Borg.Id, UnitCatalog.Hota.Id, UnitCatalog.Hiyo.Id, UnitCatalog.Nomi.Id };
        string[] seatName = { "前1", "前3", "中央", "後1", "後3" };
        var rows = new List<(string Base, string Seat, string Out, double[] W0, double[] W1, double B0, double B1)>();
        foreach (var (name, f) in CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Borg.Id)))
            foreach ((int slot, UnitDef d) in f.Occupied())
            {
                if (slot > 4 || fire.Contains(d.Id)) continue;
                (double[] W, double B) M(UnitDef nomi)
                {
                    var g = FvSwap(f, d, nomi);
                    var w = new double[4]; long burn = 0;
                    for (int wi = 1; wi <= 4; wi++)
                    {
                        int wins = 0; long b = 0;
                        Parallel.For(0, Seeds, sd =>
                        {
                            var r = BattleEngine.Run(g, EnemyCatalog.Stages[wi].Enemy, sd, verbose: false);
                            if (r.PlayerWon) Interlocked.Increment(ref wins);
                            if (r.BurnHit is { } bh && bh.ChanceBy.TryGetValue($"0:{UnitCatalog.Nomi.Id}", out var c)) Interlocked.Add(ref b, c[0]);
                        });
                        w[wi - 1] = 100.0 * wins / Seeds; burn += b;
                    }
                    return (w, (double)burn / (4 * Seeds));
                }
                var (w0, b0) = M(UnitCatalog.NomiN0);
                var (w1, b1) = M(UnitCatalog.NomiN1);
                rows.Add((name, seatName[slot], d.Name, w0, w1, b0, b1));
            }
        static int Info(double[] w) => w.Count(x => x > 0 && x < 100);
        Console.WriteLine("| 元の行 | 席 | 抜いた駒 | N0 第2〜5波 | N1 第2〜5波 | 情報セル N0 ／ N1 | 平均 N0 ／ N1 | 被弾の燃焼（ノミ）N0 ／ N1 |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|");
        foreach (var r in rows.OrderByDescending(r => Info(r.W1)).ThenByDescending(r => r.B1))
            Console.WriteLine($"| {r.Base} | {r.Seat} | {r.Out} | {string.Join(" / ", r.W0.Select(F1))} | {string.Join(" / ", r.W1.Select(F1))} | {Info(r.W0)} ／ {Info(r.W1)} | {F1(r.W0.Average())} ／ {F1(r.W1.Average())} | {r.B0:F2} ／ {r.B1:F2} |");
    }

    static void LogOne(int board, string ver, string wave, int seed)
    {
        var v = Vers.First(x => x.Name == ver);
        var w = Waves.First(x => x.Name == (int.TryParse(wave, out int n) ? $"第{n}波" : wave));
        var f = Boards[board].Make(v.Nomi);
        var (r, _, _) = CW.Fight(f, w.Make, seed);
        Console.WriteLine($"# 行{board} {Boards[board].Name}（{BA.SeatsNamed(f)}）× {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
