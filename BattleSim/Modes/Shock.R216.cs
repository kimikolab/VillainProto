using BattleCore;
using static Common;

// =====================================================================================
// shock run216 / check216（第216期）—— 表A〜G と自己検査。指示書 §5・§6。
//
//     dotnet run --project BattleSim -c Release 0 shock run216        # 表A〜F（台A・B・C ＋ 台1・台3）
//     dotnet run --project BattleSim -c Release 0 shock run216 g      # 表G（雷の編成の2枠・15通り）
//     dotnet run --project BattleSim -c Release 0 shock check216      # 自己検査（受け入れ 2・3・6）
//
// 版はベニ（O0〜O4）とカタ（S0〜S3）の札の差し替えだけで作る（`WithTraits`）。**保持者は版の中だけ。**
// 台C の席は指示書どおり O2・S0（倍率 115）で総当たりして全版に使う。台1・台3 は第214期の台と席のまま。
// =====================================================================================

static partial class ShockDiag
{
    static UnitDef Plus(UnitDef d, TraitId? extra) => extra is TraitId t ? WithTraits(d, d.Traits.Append(t).ToArray()) : d;

    /// <summary>
    /// ベニの版（第216期 §1）。<b>初めて読んだときに1度だけ作る</b>——静的フィールドの初期化子にしない（R277）が、
    /// 読むたびに作り直してもいけない（席を選ぶときに差し込んだ版を <c>SwapDef</c>（参照の一致）で元に戻せなくなる。1度踏んだ）。
    /// </summary>
    static (string Tag, UnitDef Def)[] BeniVersions => _beniVersions ??= new[]
    {
        ("O0", UnitCatalog.Beni), ("O1", Plus(UnitCatalog.Beni, TraitId.GurenOpening)), ("O2", Plus(UnitCatalog.Beni, TraitId.GurenOpeningAll)),
        ("O3", Plus(UnitCatalog.Beni, TraitId.GurenOpeningAll3)), ("O4", Plus(UnitCatalog.Beni, TraitId.GurenOpeningBurn)),
    };

    static (string Tag, UnitDef Def)[]? _beniVersions, _kataVersions;

    /// <summary>カタの版（第216期 §2）。作り方は <see cref="BeniVersions"/> と同じ。</summary>
    static (string Tag, UnitDef Def)[] KataVersions => _kataVersions ??= new[]
    {
        ("S0", UnitCatalog.Kata), ("S1", Plus(UnitCatalog.Kata, TraitId.ShockStun)), ("S2", Plus(UnitCatalog.Kata, TraitId.ShockStunAll)),
        ("S3", Plus(UnitCatalog.Kata, TraitId.ShockStunHalf)),
    };

    static UnitDef BeniOf(string o) => BeniVersions.First(v => v.Tag == o).Def;
    static UnitDef KataOf(string sv) => KataVersions.First(v => v.Tag == sv).Def;

    /// <summary>台の中のベニ・カタ（規定の定義）を版に差し替える。</summary>
    static Formation Versioned(Formation f, string o, string sv)
        => SwapDef(SwapDef(f, UnitCatalog.Beni, BeniOf(o)), UnitCatalog.Kata, KataOf(sv));

    internal sealed record Bench216(string Tag, Formation F, bool Opening);

    static List<Bench216>? _benches216;

    /// <summary>台A・B・C（X字・P2）・台1・台3（X字・P2）。台C の席は O2・S0 × 倍率 115 で選ぶ。</summary>
    internal static List<Bench216> Benches216()
    {
        if (_benches216 is not null) return _benches216;
        var list = new List<Bench216>
        {
            new("台A", TableA216(UnitCatalog.Beni, UnitCatalog.Kata), true),
            new("台B", TableB216(UnitCatalog.Beni, UnitCatalog.Kata), true),
        };
        foreach (FormationShape sh in Shapes)
        {
            var members = TableCMembers.Select(d => d == UnitCatalog.Beni ? BeniOf("O2") : d).ToList();
            Formation picked = PickSeats216(members, sh, Scale115);
            list.Add(new("台C", SwapDef(picked, BeniOf("O2"), UnitCatalog.Beni), true));
        }
        var tables = Tables();
        foreach (int i in new[] { 0, 2 })
            foreach (FormationShape sh in Shapes)
                list.Add(new(tables[i].Tag.Split(' ')[0], tables[i].Seats[sh], false));
        return _benches216 = list;
    }

    static string BenchName(Bench216 b) => b.Tag + " " + ShapeName(b.F.Shape);

    // =================================================================================
    // run216
    // =================================================================================

    static partial void Run216(string arg)
    {
        if (arg.Trim() == "g") { RunG216(); return; }
        if (arg.Trim() == "debug") { Debug216(); return; }
        var t0 = DateTime.Now;
        Console.WriteLine("# 第216期 `shock run216` —— 表A〜F（**線は置かない**）");
        Console.WriteLine();
        if (BeniVersions.Any(v => v.Def is null) || KataVersions.Any(v => v.Def is null)) throw new InvalidOperationException("版の駒が null");
        var benches = Benches216();
        Console.WriteLine("## 台と席");
        Console.WriteLine();
        foreach (var b in benches) Console.WriteLine("- " + BenchName(b) + ": " + SeatsNamed(b.F) + (b.Tag == "台C" ? "（O2・S0 × 倍率 115 で総当たり）" : ""));
        Console.WriteLine();
        Console.WriteLine("測る: 第2〜5波 × seed 0..199（verbose: false）。倍率は敵の最大HPと攻撃力（`EnemyScaleRule`）。");
        Console.WriteLine();

        var res = new Dictionary<(int B, string O, string S, string Sc), Agg216>();
        Agg216 Get(int bi, string o, string sv, string sc)
        {
            if (!res.TryGetValue((bi, o, sv, sc), out var a))
                res[(bi, o, sv, sc)] = a = Measure216(Versioned(benches[bi].F, o, sv), Scales216.First(x => x.Tag == sc).Rule);
            return a;
        }
        var openB = Enumerable.Range(0, benches.Count).Where(i => benches[i].Opening).ToList();

        // ---- 表A ----
        Console.WriteLine("## 表A. 開戦の撒き（S0）");
        Console.WriteLine();
        HeaderA();
        foreach (int bi in openB)
            foreach (var (sc, _) in Scales216)
                foreach (var (o, _) in BeniVersions)
                    RowA(benches[bi], o, "S0", sc, Get(bi, o, "S0", sc));
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B. 感電で痺れる（O0）");
        Console.WriteLine();
        HeaderA();
        for (int bi = 0; bi < benches.Count; bi++)
            foreach (var (sc, _) in Scales216)
                foreach (var (sv, _) in KataVersions)
                    RowA(benches[bi], "O0", sv, sc, Get(bi, "O0", sv, sc));
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. 組み合わせ（勝率・全員生存）");
        Console.WriteLine();
        string[] os = { "O0", "O2", "O4" }, ss = { "S0", "S2" };
        Console.WriteLine("| 台 | 倍率 | " + string.Join(" | ", os.SelectMany(o => ss.Select(sv => o + "・" + sv))) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", os.Length * ss.Length)));
        foreach (int bi in openB)
            foreach (var (sc, _) in Scales216)
                Console.WriteLine("| " + BenchName(benches[bi]) + " | " + sc + " | "
                                  + string.Join(" | ", os.SelectMany(o => ss.Select(sv => { var a = Get(bi, o, sv, sc); return F1(a.Mean25) + " ／ " + F1(a.AllSurvPct); }))) + " |");
        Console.WriteLine();
        Console.WriteLine("（各セル: 勝率（第2〜5波の平均） ／ 全員生存勝ち）");
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. 雷の帳簿（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 1T目の雷/戦 | 1T目の当たり/雷 | 1T目の1発の種類 | 雷/戦 | 当たり/雷 | 1発の種類 | 雷の与ダメ | 放電で削った 敵 | 放電で削った 味方 | 反転で癒えた放電 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int bi in openB)
            foreach (var (sc, _) in Scales216)
                foreach (var (o, sv) in os.SelectMany(o => ss.Select(sv => (o, sv))).Concat(new[] { ("O1", "S0"), ("O3", "S0") }).Distinct())
                {
                    Agg216 a = Get(bi, o, sv, sc);
                    UnitTally k = a.P;
                    Console.WriteLine("| " + BenchName(benches[bi]) + " | " + sc + " | " + o + "・" + sv + " | " + F2(a.Per(k.ThunderCastsT1)) + " | "
                                      + F2((double)k.ThunderHitsT1 / Math.Max(1, k.ThunderCastsT1)) + " | " + F2((double)k.ThunderKindsT1 / Math.Max(1, k.ThunderHitsT1)) + " | "
                                      + F2(a.Per(k.ThunderCasts)) + " | " + F2((double)k.ThunderHits / Math.Max(1, k.ThunderCasts)) + " | " + F2(MeanHist(k.ThunderKindsHist)) + " | "
                                      + F1(a.Per(k.ThunderDealt)) + " | " + F1(a.Per(a.E.DischargeTaken)) + " | " + F1(a.Per(a.P.DischargeTaken)) + " | "
                                      + F1(a.Per(a.P.DischargeInvertedIn)) + " |");
                }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E. 痺れの帳簿（1戦あたり・O0）");
        Console.WriteLine();
        Console.WriteLine("痺れた ＝ 感電で新しく痺れた駒の延べ（既に痺れていた・倒れていた・S3 で外れた は別に数える）。失った手番 ＝ 痺れで失った通常の手番。"
                          + "「ほか」＝ 感電以外の痺れ（トウほか）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 痺れた 敵／味方 | 既に痺れていた 敵／味方 | 倒れていた | S3で外れた | 失った手番（感電）敵／味方 | 失った手番（ほか）敵／味方 | 付いた痺れ（ほか）敵／味方 | 連鎖 敵／味方 | 弾けた 敵／味方 |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|---|---|---|---|---|");
        for (int bi = 0; bi < benches.Count; bi++)
            foreach (var (sc, _) in Scales216)
                foreach (var (sv, _) in KataVersions)
                {
                    Agg216 a = Get(bi, "O0", sv, sc);
                    UnitTally e = a.E, p = a.P;
                    long carryE = e.CarryAmount?[UnitTally.CarryStun] ?? 0, carryP = p.CarryAmount?[UnitTally.CarryStun] ?? 0;
                    Console.WriteLine("| " + BenchName(benches[bi]) + " | " + sc + " | " + sv + " | "
                                      + F2(a.Per(e.ShockStunned)) + "／" + F2(a.Per(p.ShockStunned)) + " | "
                                      + F2(a.Per(e.ShockStunAlready)) + "／" + F2(a.Per(p.ShockStunAlready)) + " | "
                                      + F2(a.Per(e.ShockStunDead + p.ShockStunDead)) + " | " + F2(a.Per(e.ShockStunMissed + p.ShockStunMissed)) + " | "
                                      + F2(a.Per(e.StallShockStun)) + "／" + F2(a.Per(p.StallShockStun)) + " | "
                                      + F2(a.Per(e.StallStun - e.StallShockStun)) + "／" + F2(a.Per(p.StallStun - p.StallShockStun)) + " | "
                                      + F2(a.Per(carryE - e.ShockStunned)) + "／" + F2(a.Per(carryP - p.ShockStunned)) + " | "
                                      + F2(a.Per(e.ChainRoots)) + "／" + F2(a.Per(p.ChainRoots)) + " | "
                                      + F2(a.Per(e.ShockSpent)) + "／" + F2(a.Per(p.ShockSpent)) + " |");
                }
        Console.WriteLine();

        // ---- 表F ----
        Console.WriteLine("## 表F. 開戦の毒の帳簿（1戦あたり・S0）");
        Console.WriteLine();
        Console.WriteLine("刻みは**開戦の撒きの層の分だけ**（`min(撒いた層, 今の層)`・濃縮の印の2回目以降も含む・名目）。癒えた ＝ 反転で実際に HP が増えた分（按分）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 撒いた層 敵／味方 | 味方の刻み | うち反転で回復に | うち実際に癒えた | 結界の外で削られた | 敵の刻み（削った名目） | 紅蓮を放った/戦 | ベニが倒れた戦 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int bi in openB)
            foreach (var (sc, _) in Scales216)
                foreach (var (o, _) in BeniVersions)
                {
                    Agg216 a = Get(bi, o, "S0", sc);
                    UnitTally p = a.P, e = a.E;
                    long allyTick = p.OpeningTickInverted + p.OpeningTickBitten;
                    var (bf, _) = a.Fallen.GetValueOrDefault("beni");
                    Console.WriteLine("| " + BenchName(benches[bi]) + " | " + sc + " | " + o + " | " + F2(a.Per(p.OpeningFoeLayers)) + "／" + F2(a.Per(p.OpeningAllyLayers)) + " | "
                                      + F2(a.Per(allyTick)) + " | " + F2(a.Per(p.OpeningTickInverted)) + " | " + F2(a.Per(p.OpeningHealed)) + " | "
                                      + F2(a.Per(p.OpeningTickBitten)) + " | " + F2(a.Per(e.OpeningTickBitten + e.OpeningTickInverted)) + " | "
                                      + F2(a.Per(p.GurenFires)) + " | " + F1(100.0 * bf / Math.Max(1, a.Battles)) + "% |");
                }
        Console.WriteLine();
        Console.WriteLine("所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }

    /// <summary>診断: 台A の1ターン目の雷が帯びていた状態（O0 と O3・第2波 seed 0..2）。</summary>
    static void Debug216()
    {
        foreach (string o in new[] { "O0", "O3" })
            for (int s = 0; s < 3; s++)
            {
                BattleResult r = BattleEngine.Run(Versioned(Benches216()[0].F, o, "S0"), EnemyCatalog.Stages[1].Enemy, s, verbose: true);
                Console.WriteLine(o + " seed " + s + ": " + string.Join(" ／ ", r.Events.Where(e => e.Kind == BattleEventKind.Thunder && e.Turn == 1)
                    .Select(e => e.TargetId + "[" + e.Text + "]")));
            }
    }

    static void HeaderA()
    {
        Console.WriteLine("| 台 | 倍率 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | **平均** | 全員生存 | 第2波の全員生存 | 決着T | 前衛が倒れた戦 | うち1T目まで | 倒れた割合@倒れたT（最初の1回） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
    }

    static void RowA(Bench216 b, string o, string sv, string sc, Agg216 a)
    {
        string tag = o + "・" + sv;
        Console.WriteLine("| " + BenchName(b) + " | " + sc + " | " + tag + " | " + string.Join(" | ", Waves25.Select(w => F1(a.WinPct(w)))) + " | **" + F1(a.Mean25) + "** | "
                          + F1(a.AllSurvPct) + " | " + F1(a.AllSurvAt(1)) + " | " + F2(a.MeanWinT) + " | "
                          + F1(100.0 * a.FrontFell / a.Battles) + "% | " + F1(100.0 * a.FrontT1 / a.Battles) + "% | " + DeathLine(a, b.F) + " |");
    }

    // =================================================================================
    // 表G（§5.3）: ベニ・ミオ・カタ ＋ 2枠
    // =================================================================================

    static void RunG216()
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第216期 `shock run216 g` —— 表G. 雷の編成の2枠の比較（**線は置かない**）");
        Console.WriteLine();
        UnitDef[] extra = { UnitCatalog.Guza, UnitCatalog.Sid, UnitCatalog.Kubi, UnitCatalog.Tou, UnitCatalog.Nel, UnitCatalog.Kugu };
        var pairs = new List<(UnitDef A, UnitDef B)>();
        for (int i = 0; i < extra.Length; i++) for (int j = i + 1; j < extra.Length; j++) pairs.Add((extra[i], extra[j]));
        Console.WriteLine("席: 陣形ごとに 120 通りを**候補（O2・S2）× 倍率 115** で総当たり（第2〜5波 × seed " + PickSeed0 + ".." + (PickSeed0 + PickSeeds - 1)
                          + "・勝ち数最大・同値は列挙順で最初）。**同じ席を全部の設定に使う。** 測る: 第2〜5波 × seed 0..199。");
        Console.WriteLine();
        var rows = new List<(string Name, FormationShape Sh, Formation Seats, Dictionary<string, Agg216> A)>();
        foreach (var (pa, pb) in pairs)
            foreach (FormationShape sh in Shapes)
            {
                var members = new List<UnitDef> { BeniOf("O2"), UnitCatalog.Mio, KataOf("S2"), pa, pb };
                Formation cand = PickSeats216(members, sh, Scale115);
                Formation seats = SwapDef(SwapDef(cand, BeniOf("O2"), UnitCatalog.Beni), KataOf("S2"), UnitCatalog.Kata);
                var d = new Dictionary<string, Agg216>();
                foreach (var (sc, rule) in Scales216)
                {
                    d["現行 " + sc] = Measure216(seats, rule);
                    d["候補 " + sc] = Measure216(Versioned(seats, "O2", "S2"), rule);
                }
                rows.Add((pa.Name.Split('の').Last() + "・" + pb.Name.Split('の').Last(), sh, seats, d));
            }

        string[] cols = { "現行 115", "候補 115", "現行 150", "候補 150" };
        Console.WriteLine("| 2枠 | 陣形 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", cols.Length)));
        foreach (var r in rows)
            Console.WriteLine("| " + r.Name + " | " + ShapeName(r.Sh) + " | " + string.Join(" | ", cols.Select(c => F1(r.A[c].Mean25) + " ／ " + F1(r.A[c].AllSurvPct))) + " |");
        Console.WriteLine();
        Console.WriteLine("（各セル: 勝率（第2〜5波の平均） ／ 全員生存勝ち・現行 ＝ O0・S0 ／ 候補 ＝ O2・S2）");
        Console.WriteLine();
        foreach (string c in cols)
        {
            Console.WriteLine("### 全員生存の上位5つ（" + c + "）");
            Console.WriteLine();
            foreach (var r in rows.OrderByDescending(r => r.A[c].AllSurvPct).ThenByDescending(r => r.A[c].Mean25).Take(5))
                Console.WriteLine("- " + r.Name + " × " + ShapeName(r.Sh) + ": 全員生存 " + F1(r.A[c].AllSurvPct) + " ／ 勝率 " + F1(r.A[c].Mean25) + " ——" + SeatsNamed(r.Seats));
            Console.WriteLine();
        }
        Console.WriteLine("所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }

    // =================================================================================
    // check216（受け入れ 2・3・6）
    // =================================================================================

    static partial void Check216(string arg)
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第216期 `shock check216` —— 自己検査");
        Console.WriteLine();
        var benches = Benches216();
        bool allOk = true;
        void Verdict(string name, bool ok, string detail)
        {
            allOk &= ok;
            Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + name + ": " + detail);
        }

        // (2) verbose の有無で勝敗・決着ターンが同じ（全版・S3 も）。
        long cells = 0, mism = 0;
        var combos = BeniVersions.Select(v => (v.Tag, "S0")).Concat(KataVersions.Skip(1).Select(v => ("O0", v.Tag))).Append(("O2", "S2")).Append(("O4", "S3")).ToList();
        var gate = new object();
        foreach (var b in benches)
            foreach (var (o, sv) in combos)
            {
                if (!b.Opening && o != "O0") continue;
                Formation f = Versioned(b.F, o, sv);
                Parallel.For(0, 4 * 50, j =>
                {
                    int st = 1 + j / 50, s = j % 50;
                    BattleResult q = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: false);
                    BattleResult v = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: true);
                    bool same = q.PlayerWon == v.PlayerWon && q.Turns == v.Turns && q.PlayerSurvivors == v.PlayerSurvivors;
                    lock (gate) { cells++; if (!same) mism++; }
                });
            }
        Verdict("(2) verbose の有無", mism == 0, cells + " 戦で勝敗・決着ターン・生存数の食い違い " + mism);

        // 台本で見る検査（台A・B・C × 版 × 第2〜5波 × seed 0..49）。
        long openFires = 0, openBattles = 0, openBadCount = 0, allyOpenO14 = 0, openEvtBeforeTurn = 0, openEvtLate = 0;
        long stunEvt = 0, stunDead = 0, stunNotAfterSpent = 0, s1Chains = 0, s1ChainMulti = 0, s1NotRoot = 0, stunTargetMismatch = 0;
        long stunGainNoStunEvt = 0;
        foreach (var b in benches.Where(x => x.Opening))
            foreach (var (o, sv) in new[] { ("O1", "S0"), ("O2", "S1"), ("O3", "S2"), ("O4", "S3"), ("O1", "S1") })
            {
                Formation f = Versioned(b.F, o, sv);
                for (int st = 1; st < 5; st++)
                    for (int s = 0; s < 50; s++)
                    {
                        var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                        var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
                        BattleResult r = BattleEngine.Run(pl, en, s, verbose: true);
                        var allyIds = pl.Select(u => u.InstanceId).ToHashSet();
                        int beniId = pl.First(u => u.Def.Id == "beni").InstanceId;
                        openBattles++;
                        long fires = r.TallyByUnit.TryGetValue("beni", out var bt) ? bt.OpeningFires : 0;
                        openFires += fires;
                        if (fires != 1) openBadCount++;
                        bool turnStarted = false;
                        var dead = new HashSet<int>();
                        var evs = r.Events;
                        int chainStart = -1, chainStuns = 0;
                        int? rootId = null;
                        for (int i = 0; i < evs.Count; i++)
                        {
                            BattleEvent e = evs[i];
                            if (e.Kind == BattleEventKind.TurnStart) turnStarted = true;
                            if (e.Kind == BattleEventKind.Death && e.TargetId is int d) dead.Add(d);
                            if (e.Kind == BattleEventKind.StatusGain && e.PoisonRoute == PoisonRoute.Opening)
                            {
                                if (turnStarted) openEvtLate++; else openEvtBeforeTurn++;
                                if ((o == "O1" || o == "O4") && e.TargetId is int tg && allyIds.Contains(tg)) allyOpenO14++;
                                if (e.ActorId != beniId) openEvtLate++;
                            }
                            if (e.Kind == BattleEventKind.ShockSpent && e.Slot == 0)
                            {
                                if (chainStart >= 0 && sv == "S1") { s1Chains++; if (chainStuns > 1) s1ChainMulti++; }
                                chainStart = i; chainStuns = 0; rootId = e.TargetId;
                            }
                            if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Stun)
                            {
                                stunEvt++;
                                chainStuns++;
                                if (e.TargetId is int tg2 && dead.Contains(tg2)) stunDead++;
                                BattleEvent prev = evs[i - 1];
                                if (prev.Kind != BattleEventKind.ShockSpent) stunNotAfterSpent++;
                                else if (prev.TargetId != e.TargetId) stunTargetMismatch++;
                                if (prev.Kind == BattleEventKind.ShockSpent && prev.HpAfter == 0) stunDead++;
                                if (sv == "S1" && e.TargetId != rootId) s1NotRoot++;
                                // 付いた瞬間の痺れの出来事（Stun・Struck）が直後にあること（既存の表示）
                                if (i + 1 >= evs.Count || evs[i + 1].Kind != BattleEventKind.Stun || evs[i + 1].TargetId != e.TargetId) stunGainNoStunEvt++;
                            }
                        }
                        if (chainStart >= 0 && sv == "S1") { s1Chains++; if (chainStuns > 1) s1ChainMulti++; }
                    }
            }
        Verdict("(3a) 開戦の撒きは1戦に1回", openBadCount == 0, openBattles + " 戦で撒いた回数の合計 " + openFires + "・1回でない戦 " + openBadCount);
        Verdict("(3b) O1・O4 で味方に毒が入らない", allyOpenO14 == 0, "味方への開戦の毒 " + allyOpenO14 + " 件");
        Verdict("(6a) 開戦の撒きは1ターン目の前・ベニが書き手", openEvtLate == 0, "開戦の毒の StatusGain " + openEvtBeforeTurn + " 件（ターン開始の後・ベニ以外 " + openEvtLate + "）");
        Verdict("(3c) 倒れた駒に痺れを付けない", stunDead == 0, "感電の痺れ " + stunEvt + " 件のうち倒れた駒 " + stunDead);
        Verdict("(3d) S1 は連鎖1回に1体だけ・起点だけ", s1ChainMulti == 0 && s1NotRoot == 0, "S1 の連鎖 " + s1Chains + " 回で2体以上 " + s1ChainMulti + "・起点でない " + s1NotRoot);
        Verdict("(6b) 感電の痺れは ShockSpent の直後の StatusGain（stun）", stunNotAfterSpent == 0 && stunTargetMismatch == 0,
                "直前が ShockSpent でない " + stunNotAfterSpent + "・相手が違う " + stunTargetMismatch + "（続く Stun の出来事が無い " + stunGainNoStunEvt + "）");

        // (3a') 会戦: 開戦の撒きは戦ごとに1回（台A・O2 を順路5 の第2〜5波に当てる）。
        long engBattles = 0, engBad = 0;
        Formation fa = Versioned(benches[0].F, "O2", "S2");
        var squads = Enumerable.Range(1, 4).Select(st => EnemyCatalog.Stages[st].Enemy).ToList();
        for (int s = 0; s < 100; s++)
        {
            EngagementResult er = EngagementEngine.Run(new[] { fa }, squads, s, verbose: false);
            foreach (BattleResult br in er.Battles)
            {
                if (!br.TallyByUnit.TryGetValue("beni", out var bt)) continue;
                engBattles++;
                if (bt.OpeningFires != 1 && bt.Deaths == 0) engBad++;
                if (bt.OpeningFires > 1) engBad++;
            }
        }
        Verdict("(3a') 会戦では戦ごとに1回", engBad == 0, "ベニが出た戦 " + engBattles + " で1回でない戦 " + engBad);

        // (0) 台の駒は規定の定義（版の差し込みが残っていない）。
        int leftover = benches.Sum(b => b.F.Occupied().Count(o => !UnitCatalog.Everyone.Any(u => ReferenceEquals(u, o.Def))));
        Verdict("(0) 台に版の定義が残っていない", leftover == 0, leftover + " 枠");

        // (S0・O0 は保持者 0) 札の保持者が UnitCatalog.All に居ない。
        var cards = new[] { TraitId.GurenOpening, TraitId.GurenOpeningAll, TraitId.GurenOpeningAll3, TraitId.GurenOpeningBurn, TraitId.ShockStun, TraitId.ShockStunAll, TraitId.ShockStunHalf };
        int holders = UnitCatalog.Everyone.Count(u => u.Traits.Any(cards.Contains));
        Verdict("(1') 版の札の保持者はロスターに 0 枚", holders == 0, holders + " 枚");

        Console.WriteLine();
        Console.WriteLine("SHOCK216_CHECK " + (allOk ? "ok=True" : "ok=False") + " 所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }
}
