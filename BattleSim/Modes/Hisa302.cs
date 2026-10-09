using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// hisa302 —— 第302期「ヒサの仕上げ（粛で指差しを止める・号令 HL-t3 ＋ 溜まり 60・庇い HC）の規定化」と、段1 の版（HV-s ／ HB-t ／ HB-p）。
// 指示書は design/PHASE302_HISA_REGULATE_SPEC.md ／ 報告は design/PHASE302_HISA_REGULATE.md。
//
//     dotnet run --project BattleSim -c Release 0 hisa302 p0 [seeds]       # §4 Phase 0（粛が止める動作 ／ 第2波の指差しと羽 ／ 号令の溜まり ／ 鼓舞が乗る攻撃）
//     dotnet run --project BattleSim -c Release 0 hisa302 stage [seeds]    # §5-2 段0 の段ごと（S0 第301期の規定 → S1 → S2 → S3）と HV-s × 代表台 × 全波 ＋ 上限なし（K2）と上限 60
//     dotnet run --project BattleSim -c Release 0 hisa302 wave2 [seeds]    # §5-3 標軸の行 × 第2波（S0 → S1 → S3 → HV-s）と、何が止まったか
//     dotnet run --project BattleSim -c Release 0 hisa302 ver [seeds]      # §3 段1 の HB-t ／ HB-p（規定 S3 と比べる）× 代表台 × 全波 ＋ 鼓舞の内訳 ＋ 攻撃力の最大
//     dotnet run --project BattleSim -c Release 0 hisa302 cmp [seeds]      # `compare` のヒサ在席の行 × 版（S3 ／ HV-s ／ HB-t ／ HB-p）と (G2)
//     dotnet run --project BattleSim -c Release 0 hisa302 find [seeds]     # 台本の例（玉 ／ 黙る ／ 鼓舞）
//     dotnet run --project BattleSim -c Release 0 hisa302 elite [seeds]    # `docs/elite.md` のヒサ在席の行 × 精鋭（S2 → S3・庇いで動いた行の内訳）
//     dotnet run --project BattleSim -c Release 0 hisa302 memo <台の一部> <1..5|guard|bat|boss> <seed> [S3|HVs|HBt|HBp] [最後のT]
//     dotnet run --project BattleSim -c Release 0 hisa302 check            # 自己検査
// =====================================================================================
static class Hisa302Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": Phase0(int.Parse(A(3, "200"))); return;
            case "stage": Stage(int.Parse(A(3, "200"))); return;
            case "wave2": Wave2(int.Parse(A(3, "200"))); return;
            case "ver": Versions(int.Parse(A(3, "200"))); return;
            case "cmp": Cmp(int.Parse(A(3, "200"))); return;
            case "find": Find(int.Parse(A(3, "50"))); return;
            case "elite": EliteCover(int.Parse(A(3, "200"))); return;
            case "memo": Memo(A(3, "循環"), A(4, "boss"), int.Parse(A(5, "0")), A(6, "S3"), int.Parse(A(7, "99"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("hisa302: モードは p0 / stage / wave2 / ver / cmp / find / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版と台
    // ---------------------------------------------------------------------------------
    static UnitDef Def(UnitDef b, IEnumerable<TraitId> traits) => new()
    {
        Id = b.Id, Name = b.Name, MaxHp = b.MaxHp, Attack = b.Attack, Speed = b.Speed, Advances = b.Advances, Pattern = b.Pattern,
        Traits = traits.ToArray(), Actions = b.Actions, PlusText = b.PlusText, MinusText = b.MinusText, Flavor = b.Flavor,
    };

    /// <summary>S1 ＝ 第301期の規定の「あいつがやった！」を粛 ／ 痺れで止める札に差し替えただけ（段0-1）。</summary>
    static readonly UnitDef S1 = Def(UnitCatalog.HisaH301, UnitCatalog.HisaH301.Traits.Select(t => t == TraitId.FrameAccuse ? TraitId.FrameAccuseQuiet : t));
    /// <summary>S2 ＝ S1 ＋ 号令の玉（HL-t3 ＋ 溜まり 60・段0-2）。</summary>
    static readonly UnitDef S2 = Def(S1, S1.Traits.Append(TraitId.CommandBall));
    /// <summary>K2 ＝ S1 ＋ 上限なしの HL-t3（第301期の K2 の号令）。上限 60 の効き目の対照（精鋭 ／ ボスに粛は無いので、S1 の差し替えは効かない）。</summary>
    static readonly UnitDef K2u = Def(S1, S1.Traits.Append(TraitId.CommandTurn3));

    internal sealed record Ver(string Key, string Name, UnitDef D);
    static readonly Ver V0 = new("S0", "第301期の規定", UnitCatalog.HisaH301);
    static readonly Ver V1 = new("S1", "＋粛で黙る（段0-1）", S1);
    static readonly Ver V2 = new("S2", "＋玉（段0-2）", S2);
    static readonly Ver V3 = new("S3", "＋庇い（段0-3）＝ 規定", UnitCatalog.Hisa);
    static readonly Ver VK2 = new("K2", "上限なし（第301期の K2）", K2u);
    static readonly Ver VHV = new("HVs", "HV-s（叫びも粛で黙る）", UnitCatalog.HisaHVs);
    static readonly Ver VBt = new("HBt", "HB-t（重い玉＋鼓舞・次の手番まで）", UnitCatalog.HisaHBt);
    static readonly Ver VBp = new("HBp", "HB-p（重い玉＋鼓舞・戦の終わりまで）", UnitCatalog.HisaHBp);
    static readonly Ver[] StageVers = { V0, V1, V2, V3, VHV };

    /// <summary>第303期: ヒサの規定が動いたので、この器具の台は第302期の規定のヒサ（`HisaH302`）に固定する（`Common.Pin303`・版の駒は替えない）。</summary>
    static List<UnitState> Mat(Formation f) => BattleEngine.Materialize(Pin303(f), BattleContext.PlayerTeam);
    static Doha297Diag.Wave[] Waves() => Doha297Diag.Waves();
    static Doha297Diag.Wave WaveOf(string k) => Waves().First(w => w.Key == k);
    static Doha297Diag.Wave[] AllWaves() => Enumerable.Range(1, EnemyCatalog.Stages.Count).Select(i => WaveOf(i.ToString())).Concat(new[] { "guard", "bat", "boss" }.Select(WaveOf)).ToArray();
    static Doha297Diag.Wave[] MainWaves() => new[] { "2", "3", "4", "5", "guard", "bat", "boss" }.Select(WaveOf).ToArray();
    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation CompareRow(string n) => CompareBuilds().First(r => r.Name == n).F;
    static Formation With(Formation f, UnitDef hisa)
    {
        var g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Hisa)) g[slot] = hisa;
        return g;
    }
    const string Econ = "標経済 (ヒサ×ザン×ミサ)";

    /// <summary>代表台（試遊・標のうちヒサのいる4行 ＋ 標経済 ＋ ザンのいない台）。道中（見境改）はヒサがいないので外す。</summary>
    static (string Name, Formation F)[] Boards() =>
        Presets.Playtest.Where(r => r.Name.StartsWith("試遊・標", StringComparison.Ordinal) && r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hisa))).Select(r => (r.Name, r.F))
            .Append((Econ, CompareRow(Econ)))
            .Append((Hisa301Diag.NoZanName, Hisa301Diag.NoZan)).ToArray();

    /// <summary>標軸の行（代表台 ＋ `compare` のヒサ在席の行）。</summary>
    static (string Name, Formation F)[] MarkRows() => Boards()
        .Concat(CompareBuilds().Where(r => r.Name != Econ && r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hisa))).Select(r => (r.Name, r.F))).ToArray();

    // ---------------------------------------------------------------------------------
    // 1戦の集計
    // ---------------------------------------------------------------------------------
    sealed class Agg
    {
        public long N, Wins, WinT, Turns, AccuseSil, AccuseSilHush, Accuses, AccuseFeathers, RallyFires, RallyHushed, RallyBlocked, CoverFires, CoverHushed, CoverBlocked,
            CmdLayers, CmdFires, CmdCapped, CmdTurns, CmdDropped, CmdDropEvents, CmdPoolLow, CmdFeathers, RouseFires, RouseGiven, MfHushed, Overflow, Feathers, RoundSlashes,
            HushAvenge, HushAccuse, HushFeather, HushCover, HushRally, DivertStrips, ShareHits, AtkMax, AtkMaxN;
        public long PeakAtkMax;
        public long[] BallHist = new long[4];
        public readonly Dictionary<string, long> Reads = new(), RousedReads = new(), RousedBonus = new();
        public void Add(Agg o)
        {
            foreach (var f in typeof(Agg).GetFields())
            {
                if (f.FieldType != typeof(long)) continue;
                if (f.Name == "PeakAtkMax") f.SetValue(this, Math.Max((long)f.GetValue(this)!, (long)f.GetValue(o)!));
                else f.SetValue(this, (long)f.GetValue(this)! + (long)f.GetValue(o)!);
            }
            for (int i = 0; i < BallHist.Length; i++) BallHist[i] += o.BallHist[i];
            foreach (var (k, v) in o.Reads) Reads[k] = Reads.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.RousedReads) RousedReads[k] = RousedReads.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.RousedBonus) RousedBonus[k] = RousedBonus.GetValueOrDefault(k) + v;
        }
        public double P(long x) => N == 0 ? 0 : (double)x / N;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public double WinTurns => Wins == 0 ? 0 : (double)WinT / Wins;
        public long CoverBattlesN;
        public void CoverBattles(IEnumerable<Agg> parts) => CoverBattlesN = parts.Count(x => x.CoverFires > 0);
    }

    static Agg One(Formation f, Func<List<UnitState>> enemy, int seed, bool verbose = false)
    {
        var p = Mat(f);
        var r = BattleEngine.Run(p, enemy(), seed, verbose: verbose);
        var a = new Agg { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!mine.Contains(id)) continue;
            a.AccuseSil += t.AccuseSilenced; a.AccuseSilHush += t.AccuseSilencedHush; a.Accuses += t.FrameAccuses; a.AccuseFeathers += t.AccuseFeathers;
            a.RallyFires += t.RallyFires; a.RallyHushed += t.RallyHushed; a.RallyBlocked += t.RallyBlocked;
            a.CoverFires += t.CoverFires; a.CoverHushed += t.CoverHushed; a.CoverBlocked += t.CoverBlocked;
            a.CmdLayers += t.CommandLayers; a.CmdFires += t.CommandFires; a.CmdCapped += t.CommandCapped; a.CmdTurns += t.CommandTurns; a.CmdDropped += t.CommandDropped;
            a.CmdDropEvents += t.CommandDropEvents; a.CmdPoolLow += t.CommandPoolLow; a.CmdFeathers += t.CommandFeathers; a.RouseFires += t.RouseFires; a.RouseGiven += t.RouseGiven;
            a.MfHushed += t.MfHushed; a.Overflow += t.RallyOverflow; a.Feathers += t.FeatherChased + t.FeatherSprayed + t.MfShotsFoe + t.MfShotsAlly; a.RoundSlashes += t.RoundSlashes;
            a.DivertStrips += t.DivertStripBeckon + t.DivertStripOther; a.ShareHits += t.ShareTakenHits;
            if (t.RousePeakAtk > a.PeakAtkMax) a.PeakAtkMax = t.RousePeakAtk;
            if (t.CommandBallHist is { } bh) for (int i = 0; i < 4; i++) a.BallHist[i] += bh[i];
            if (t.AttackReads > 0) a.Reads[id] = t.AttackReads;
            if (t.RousedReads > 0) { a.RousedReads[id] = t.RousedReads; a.RousedBonus[id] = t.RousedBonus; }
        }
        var hr = r.BoardRules.HushByRoute;
        a.HushAvenge = hr[(int)OutOfTurnRoute.Avenge][1]; a.HushAccuse = hr[(int)OutOfTurnRoute.Accuse][1]; a.HushFeather = hr[(int)OutOfTurnRoute.FeatherMark][1];
        a.HushCover = hr[(int)OutOfTurnRoute.Cover][1]; a.HushRally = hr[(int)OutOfTurnRoute.Rally][1];
        if (verbose)
        {
            var ids = p.Select(u => u.InstanceId).ToHashSet();
            long mx = 0;
            foreach (var e in r.Events) if (e.Kind == BattleEventKind.StatSnapshot && e.TargetId is int ti && ids.Contains(ti) && e.Amount > mx) mx = e.Amount;
            a.AtkMax = mx; a.AtkMaxN = 1;
        }
        return a;
    }

    static Agg Many(Formation f, Func<List<UnitState>> enemy, int seeds, bool verbose = false)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, s => parts[s] = One(f, enemy, s, verbose));
        var a = new Agg();
        foreach (var x in parts) a.Add(x);
        return a;
    }

    static string Pct(long x, long n) => n == 0 ? "—" : $"{100.0 * x / n:F0}%";
    static string Cell(Agg a) => a.Wins == 0 ? $"{a.Win:F1}" : $"{a.Win:F1}（{a.WinTurns:F1}）";
    static string Hist(long[] h) { long n = h.Sum(); return n == 0 ? "—" : string.Join(" ／ ", h.Select(x => $"{100.0 * x / n:F0}")); }

    // ---------------------------------------------------------------------------------
    // §4 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0(int seeds)
    {
        var w2 = WaveOf("2");
        var boards = Boards();
        Console.WriteLine($"# 第302期 Phase 0（seed 0..{seeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("## 1. 粛（第2波）が止める標軸の動作（S3 ＝ 規定・HV-s の叫びは版）");
        Console.WriteLine();
        Console.WriteLine("粛が**単独の原因で**止めた回数（`BoardRules.HushByRoute` の味方の側）と、止まらない動作の回数（1戦あたり）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 仇討ち（止）| 指差し（止）| 羽の標撃ち（止）| 庇い（止）| 叫び（出た）| 叫び HV-s（止）| 号令（出た）| 逸らしの剥がし（出た）| 肩代わり（ドハ・出た）|");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (n, f) in boards)
        {
            var a = Many(f, w2.Make, seeds); var h = Many(With(f, UnitCatalog.HisaHVs), w2.Make, seeds);
            Console.WriteLine($"| {n} | {a.Win:F1} | {a.P(a.HushAvenge):F2} | {a.P(a.HushAccuse):F2} | {a.P(a.HushFeather):F2} | {a.P(a.HushCover):F2} | {a.P(a.RallyFires):F2} | {h.P(h.HushRally):F2} | {a.P(a.CmdFires):F2} | {a.P(a.DivertStrips):F2} | {a.P(a.ShareHits):F2} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 2. 第2波（粛）: 指差しから付いた標と、そこから飛んだ羽（S0 第301期の規定 → S1 粛で黙る）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 S0 → S1 | 指差し（標）S0 → S1 | 羽（指差しの標から）S0 → S1 | 黙った S1（うち粛）|");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (n, f) in boards)
        {
            var a = Many(With(f, V0.D), w2.Make, seeds); var b = Many(With(f, V1.D), w2.Make, seeds);
            Console.WriteLine($"| {n} | {a.Win:F1} → {b.Win:F1} | {a.P(a.Accuses):F2} → {b.P(b.Accuses):F2} | {a.P(a.AccuseFeathers):F2} → {b.P(b.AccuseFeathers):F2} | {b.P(b.AccuseSil):F2}（{b.P(b.AccuseSilHush):F2}）|");
        }
        Console.WriteLine();

        Console.WriteLine("## 3. 号令の溜まり（精鋭 ／ ボス・本編第3〜5波）");
        Console.WriteLine();
        Console.WriteLine("捨てた ＝ 上限 60 を超えて捨てた溢れ（S3・1戦）／ 満杯でなかった ＝ ヒサの手番の時点で溜まりが 60 未満だった手番の割合（K2 ＝ 上限なし・S3 ＝ 上限 60）／ 玉の分布 ＝ 1手番に使った玉 0 ／ 1 ／ 2 ／ 3 の割合（%）。HB の見込み ＝ HB-t（溢れ 40 で玉1つ・120 まで）の同じ量。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 溢れ（S3）| 捨てた（S3）| 満杯でなかった K2 ／ S3 | 玉の分布 S3 | HB-t の満杯でなかった（120 未満）| 玉の分布 HB-t |");
        Console.WriteLine("|---|---|--:|--:|---|---|--:|---|");
        foreach (var (n, f) in boards)
            foreach (var w in new[] { "3", "4", "5", "guard", "bat", "boss" }.Select(WaveOf))
            {
                var a = Many(f, w.Make, seeds); var k = Many(With(f, K2u), w.Make, seeds); var b = Many(With(f, UnitCatalog.HisaHBt), w.Make, seeds);
                Console.WriteLine($"| {n} | {w.Name} | {a.P(a.Overflow):F1} | {a.P(a.CmdDropped):F1} | {Pct(k.CmdPoolLow, k.CmdTurns)} ／ {Pct(a.CmdPoolLow, a.CmdTurns)} | {Hist(a.BallHist)} | {Pct(b.CmdPoolLow, b.CmdTurns)} | {Hist(b.BallHist)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 4. 攻撃力 +5 が乗る攻撃（HB-t・精鋭 ／ ボス・駒別）");
        Console.WriteLine();
        Console.WriteLine("読み ＝ 攻撃力を出力に変えた回数（`NoteAttackRead`・1戦）／ 鼓舞が乗った ＝ そのうち鼓舞が乗っていた回数 ／ 平均の鼓舞。攻撃力を読まない固定の量（刻み・徴収など）はここに出ない。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 駒 | 読み | 鼓舞が乗った | 平均の鼓舞 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (var (n, f) in boards)
        {
            var a = new Agg();
            foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf)) a.Add(Many(With(f, UnitCatalog.HisaHBt), w.Make, seeds));
            foreach (var (id, reads) in a.Reads.OrderByDescending(kv => kv.Value))
            {
                long rr = a.RousedReads.GetValueOrDefault(id), rb = a.RousedBonus.GetValueOrDefault(id);
                Console.WriteLine($"| {n} | {FvName(id)} | {a.P(reads):F2} | {a.P(rr):F2}（{Pct(rr, reads)}）| {(rr == 0 ? "—" : $"{(double)rb / rr:F1}")} |");
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // §5-2 段0 の段ごと と HV-s
    // ---------------------------------------------------------------------------------
    static void Stage(int seeds)
    {
        var waves = AllWaves();
        var boards = Boards();
        var vers = StageVers.Append(VK2).ToArray();
        var res = new Dictionary<(string, string, string), Agg>();
        foreach (var (n, f) in boards) foreach (var v in vers) foreach (var w in waves) res[(n, v.Key, w.Key)] = Many(With(f, v.D), w.Make, seeds);

        Console.WriteLine($"# 第302期 段0 の段ごと と HV-s（seed 0..{seeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("版: " + string.Join(" ／ ", vers.Select(v => $"{v.Key} {v.Name}")) + "。括弧は倒しT。前の版から ±10pt 以上動いたセルは太字。");
        Console.WriteLine();
        Console.WriteLine("## 勝率");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", waves.Length)));
        foreach (var (n, _) in boards)
            for (int i = 0; i < StageVers.Length; i++)
            {
                var v = StageVers[i]; var prev = v == VHV ? V3 : i > 0 ? StageVers[i - 1] : null;
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", waves.Select(w =>
                {
                    var a = res[(n, v.Key, w.Key)];
                    return prev is not null && Math.Abs(a.Win - res[(n, prev.Key, w.Key)].Win) >= 10 ? $"**{Cell(a)}**" : Cell(a);
                })) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 段0-2: 上限なし（K2）と上限 60（S2）——精鋭 ／ ボス");
        Console.WriteLine();
        Console.WriteLine("層 ＝ 号令で刻んだ層（1戦）・張り付き ＝ 1手番の上限（3 層）で止まった手番 ÷ 号令の手番・捨てた ＝ 上限 60 で捨てた溢れ（1戦）・満杯でなかった ＝ 手番の時点で溜まりが 60 未満。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率（倒しT）K2 → S2 | 層 K2 → S2 | 張り付き K2 → S2 | 捨てた S2 | 満杯でなかった K2 → S2 | 羽（号令）K2 → S2 |");
        Console.WriteLine("|---|---|---|---|---|--:|---|---|");
        foreach (var (n, _) in boards)
            foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf))
            {
                var k = res[(n, "K2", w.Key)]; var a = res[(n, "S2", w.Key)];
                Console.WriteLine($"| {n} | {w.Name} | {Cell(k)} → {Cell(a)} | {k.P(k.CmdLayers):F2} → {a.P(a.CmdLayers):F2} | {Pct(k.CmdCapped, k.CmdFires)} → {Pct(a.CmdCapped, a.CmdFires)} | {a.P(a.CmdDropped):F1} | {Pct(k.CmdPoolLow, k.CmdTurns)} → {Pct(a.CmdPoolLow, a.CmdTurns)} | {k.P(k.CmdFeathers):F2} → {a.P(a.CmdFeathers):F2} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 段0-3: 庇い（S3・出た波だけ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 S2 → S3 | 庇った | 粛 ／ 痺れで庇えなかった |");
        Console.WriteLine("|---|---|---|--:|---|");
        foreach (var (n, _) in boards)
            foreach (var w in waves)
            {
                var a = res[(n, "S3", w.Key)]; var z = res[(n, "S2", w.Key)];
                if (a.CoverFires == 0 && a.CoverHushed == 0 && a.CoverBlocked == 0) continue;
                Console.WriteLine($"| {n} | {w.Name} | {z.Win:F1} → {a.Win:F1} | {a.P(a.CoverFires):F2} | {a.P(a.CoverHushed):F2} ／ {a.P(a.CoverBlocked):F2} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // §5-3 第2波の報告
    // ---------------------------------------------------------------------------------
    static void Wave2(int seeds)
    {
        var w = WaveOf("2");
        var vers = new[] { V0, V1, V3, VHV };
        Console.WriteLine($"# 第302期 §5-3 標軸の行 × 第2波（粛）（seed 0..{seeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("勝率（倒しT）と、粛が止めた動作（1戦あたり・粛が単独の原因）: 指 ＝ 指差し（あいつがやった！）／ 叫 ＝ 叫び（HV-s だけ）／ 仇 ＝ ザンの仇討ち ／ 庇 ＝ 庇い ／ 羽 ＝ ミサの羽の標撃ち。");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", vers.Select(v => v.Key)) + " | S1 で止まった（指 ／ 仇 ／ 羽）| S3 で止まった（指 ／ 仇 ／ 庇 ／ 羽）| HV-s で止まった（指 ／ 叫 ／ 仇 ／ 庇 ／ 羽）|");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", vers.Length)) + "---|---|---|");
        foreach (var (n, f) in MarkRows())
        {
            var a = vers.Select(v => Many(With(f, v.D), w.Make, seeds)).ToArray();
            string Stop(Agg x, bool cover, bool rally) => $"{x.P(x.HushAccuse):F2}" + (rally ? $" ／ {x.P(x.HushRally):F2}" : "") + $" ／ {x.P(x.HushAvenge):F2}" + (cover ? $" ／ {x.P(x.HushCover):F2}" : "") + $" ／ {x.P(x.HushFeather):F2}";
            Console.WriteLine($"| {n} | " + string.Join(" | ", a.Select(Cell)) + $" | {Stop(a[1], false, false)} | {Stop(a[2], true, false)} | {Stop(a[3], true, true)} |");
        }
        Console.WriteLine();
        Console.WriteLine("S0 の止まった（参考・指差しは止まらない）: 仇 ／ 羽");
        Console.WriteLine();
        foreach (var (n, f) in MarkRows())
        {
            var a = Many(With(f, V0.D), w.Make, seeds);
            Console.WriteLine($"- {n}: 仇 {a.P(a.HushAvenge):F2} ／ 羽 {a.P(a.HushFeather):F2}・指差し（出た）{a.P(a.Accuses):F2}・叫び（出た）{a.P(a.RallyFires):F2}");
        }
    }

    // ---------------------------------------------------------------------------------
    // §3 段1 HB-t ／ HB-p
    // ---------------------------------------------------------------------------------
    static void Versions(int seeds)
    {
        var waves = AllWaves();
        var main = MainWaves();
        var boards = Boards();
        var vers = new[] { V3, VBt, VBp };
        var res = new Dictionary<(string, string, string), Agg>();
        foreach (var (n, f) in boards) foreach (var v in vers) foreach (var w in waves) res[(n, v.Key, w.Key)] = Many(With(f, v.D), w.Make, seeds);
        int vs = Math.Min(seeds, 50);
        var mx = new Dictionary<(string, string, string), Agg>();
        foreach (var (n, f) in boards) foreach (var v in vers) foreach (var w in main) mx[(n, v.Key, w.Key)] = Many(With(f, v.D), w.Make, vs, verbose: true);

        Console.WriteLine($"# 第302期 段1 —— 号令に鼓舞を足す版（seed 0..{seeds - 1}・攻撃力の最大は seed 0..{vs - 1} の台本）");
        Console.WriteLine();
        Console.WriteLine("版: " + string.Join(" ／ ", vers.Select(v => $"{v.Key} {v.Name}")) + "。");
        Console.WriteLine();
        Console.WriteLine("## 得点（代表台 × 第2〜5波 ＋ 近衛 ＋ 大隊 ＋ ボスの勝率の平均・第301期と同じ作り）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 得点 | 精鋭の平均 | ボスの平均 | 精鋭の決着T | ボスの決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var v in vers)
        {
            double sc = boards.SelectMany(b => main.Select(w => res[(b.Name, v.Key, w.Key)].Win)).Average();
            double el = boards.SelectMany(b => new[] { "guard", "bat" }.Select(k => res[(b.Name, v.Key, k)].Win)).Average();
            double bo = boards.Average(b => res[(b.Name, v.Key, "boss")].Win);
            double elT = boards.SelectMany(b => new[] { "guard", "bat" }.Select(k => res[(b.Name, v.Key, k)])).Average(a => a.P(a.Turns));
            double boT = boards.Select(b => res[(b.Name, v.Key, "boss")]).Average(a => a.P(a.Turns));
            Console.WriteLine($"| {v.Key} {v.Name} | {sc:F2} | {el:F1} | {bo:F1} | {elT:F2} | {boT:F2} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 勝率（括弧は倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", waves.Length)));
        foreach (var (n, _) in boards)
            foreach (var v in vers)
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", waves.Select(w =>
                {
                    var a = res[(n, v.Key, w.Key)];
                    return v != V3 && Math.Abs(a.Win - res[(n, "S3", w.Key)].Win) >= 10 ? $"**{Cell(a)}**" : Cell(a);
                })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 号令と鼓舞（精鋭 ／ ボス）");
        Console.WriteLine();
        Console.WriteLine("層 ＝ 号令で刻んだ層（1戦）・玉の分布 ＝ 1手番に使った玉 0 ／ 1 ／ 2 ／ 3 の割合（%）・鼓舞 ＝ 鼓舞の回数（1戦）・攻撃力の最大 ＝ 味方のターンの頭の攻撃力の最大（台本・1戦の最大の平均）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 決着T | 層 | 玉の分布 | 鼓舞 | 攻撃力の最大 |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|--:|");
        foreach (var (n, _) in boards)
            foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf))
                foreach (var v in vers)
                {
                    var a = res[(n, v.Key, w.Key)]; var m = mx[(n, v.Key, w.Key)];
                    Console.WriteLine($"| {n} | {w.Name} | {v.Key} | {a.P(a.Turns):F2} | {a.P(a.CmdLayers):F2} | {Hist(a.BallHist)} | {a.P(a.RouseFires):F2} | {(m.AtkMaxN == 0 ? "—" : $"{(double)m.AtkMax / m.AtkMaxN:F1}")} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 鼓舞が乗った攻撃の内訳（駒別・第2〜5波 ＋ 精鋭 ＋ ボスの和・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 内訳（駒 回数（平均の鼓舞））|");
        Console.WriteLine("|---|---|---|");
        foreach (var (n, _) in boards)
            foreach (var v in new[] { VBt, VBp })
            {
                var a = new Agg(); foreach (var w in main) a.Add(res[(n, v.Key, w.Key)]);
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" ／ ", a.RousedReads.OrderByDescending(kv => kv.Value).Select(kv => $"{FvName(kv.Key)} {a.P(kv.Value):F2}（{(double)a.RousedBonus[kv.Key] / kv.Value:F1}）")) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 攻撃力の最大（全戦の最大・第2〜5波 ＋ 精鋭 ＋ ボス）");
        Console.WriteLine();
        Console.WriteLine("| 台 | S3 | HB-t | HB-p |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var (n, f) in boards)
        {
            long Max(string k)
            {
                long best = 0;
                foreach (var w in main)
                    for (int s = 0; s < vs; s++)
                    {
                        var p = Mat(With(f, vers.First(v => v.Key == k).D));
                        var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                        var ids = p.Select(u => u.InstanceId).ToHashSet();
                        foreach (var e in r.Events) if (e.Kind == BattleEventKind.StatSnapshot && e.TargetId is int ti && ids.Contains(ti) && e.Amount > best) best = e.Amount;
                    }
                return best;
            }
            Console.WriteLine($"| {n} | {Max("S3")} | {Max("HBt")} | {Max("HBp")} |");
        }
    }

    // ---------------------------------------------------------------------------------
    // compare のヒサ在席の行 × 版
    // ---------------------------------------------------------------------------------
    static double[] Rates(Formation f, int seeds)
    {
        var w = new double[EnemyCatalog.Stages.Count];
        for (int i = 0; i < w.Length; i++)
        {
            int ii = i;
            var wins = new bool[seeds];
            Parallel.For(0, seeds, s => wins[s] = BattleEngine.Run(Mat(f), BattleEngine.Materialize(EnemyCatalog.Stages[ii].Enemy, BattleContext.EnemyTeam), s, verbose: false).PlayerWon);
            w[i] = 100.0 * wins.Count(x => x) / seeds;
        }
        return w;
    }

    static void Cmp(int seeds)
    {
        var rows = CompareBuilds();
        var withHisa = rows.Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hisa))).ToArray();
        var vers = new[] { V0, V1, V2, V3, VHV, VBt, VBp };
        var all = new Dictionary<(string, string), double[]>();
        foreach (var v in vers) foreach (var (n, f) in withHisa) all[(n, v.Key)] = Rates(With(f, v.D), seeds);
        Console.WriteLine($"# 第302期 `compare` のヒサ在席の行 × 版（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"ヒサのいない {rows.Length - withHisa.Length} 行は版で動かない。段0 の S1〜S3 は前の段と、HV-s ／ HB-t ／ HB-p は S3 と比べ、−10.0pt 以上落ちたセルを太字にする。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        string Base(string k) => k switch { "S1" => "S0", "S2" => "S1", "S3" => "S2", "S0" => "S0", _ => "S3" };
        foreach (var (n, _) in withHisa)
            foreach (var v in vers)
            {
                var a = all[(n, v.Key)]; var z = all[(n, Base(v.Key))];
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", a.Select((x, i) => v != V0 && x - z[i] <= -10.0 ? $"**{x:F1}**" : $"{x:F1}")) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("## (G2)（−10.0pt 以上落ちた行の駒ごとの「その駒を含む他の行」の全波平均の変化）");
        Console.WriteLine();
        Console.WriteLine("ヒサの版はヒサの札しか替えないので、ヒサを含まない行は動かない（0.0pt）。ヒサを含む他の行は上の表の同じ版で読む。");
        Console.WriteLine();
        bool any = false;
        foreach (var v in vers.Where(v => v != V0))
            foreach (var (n, f) in withHisa)
            {
                var a = all[(n, v.Key)]; var z = all[(n, Base(v.Key))];
                if (!a.Select((x, i) => x - z[i]).Any(d => d <= -10.0)) continue;
                any = true;
                var parts = new List<string>();
                foreach (var d in f.Occupied().Select(o => o.Def).Distinct())
                {
                    var others = rows.Where(r => r.Name != n && r.F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToArray();
                    if (others.Length == 0) { parts.Add($"{Short(d)}: 他の行 0（分解が成立しない）"); continue; }
                    double delta = others.Average(o => withHisa.Any(h => h.Name == o.Name) ? all[(o.Name, v.Key)].Average() - all[(o.Name, Base(v.Key))].Average() : 0.0);
                    parts.Add($"{Short(d)}: {delta:+0.0;-0.0;0.0}pt（{others.Length} 行）");
                }
                Console.WriteLine($"- {v.Key} {n}: " + string.Join(" ／ ", parts));
            }
        if (!any) Console.WriteLine("−10.0pt 以上落ちた行は無い（(G2) の分解の対象なし）。");
    }

    // ---------------------------------------------------------------------------------
    // `docs/elite.md` のヒサ在席の行 × 精鋭（段0-3 の庇い）
    // ---------------------------------------------------------------------------------
    static void EliteCover(int seeds)
    {
        var rows = CompareBuilds().Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hisa))).ToArray();
        Console.WriteLine($"# 第302期 `docs/elite.md` のヒサ在席の行 × 精鋭（S2 → S3・seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("庇った ＝ 1戦あたり（出た戦）・ヒサが倒れた ＝ 庇った一撃で ／ その戦の中で（S2 → S3）・庇われた駒 ＝ 庇われた回数の内訳。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 勝率（決着T）S2 → S3 | 庇った | 庇った一撃で倒れた | ヒサが倒れた戦 S2 → S3 | 叫び S2 → S3 | 庇われた駒 |");
        Console.WriteLine("|---|---|---|---|--:|---|---|---|");
        foreach (var (n, f) in rows)
            foreach (var (label, w) in new[] { ("近衛", EnemyCatalog.EliteGuardWave), ("大隊", EnemyCatalog.EliteBattalionWave) })
            {
                Agg Run(UnitDef d, out long hisaDead, out Dictionary<string, long> saved, out long coverDied)
                {
                    var parts = new Agg[seeds]; var hd = new long[seeds]; var sv = new Dictionary<string, long>[seeds]; var cd = new long[seeds];
                    Parallel.For(0, seeds, s =>
                    {
                        var p = Mat(With(f, d));
                        var r = BattleEngine.Run(p, BattleEngine.MaterializeEnemy(w, EnemyCatalog.EliteScale), s, verbose: false);
                        var a = new Agg { N = 1, Turns = r.Turns };
                        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
                        sv[s] = new();
                        foreach (var (id, t) in r.TallyByUnit)
                        {
                            a.CoverFires += t.CoverFires; a.RallyFires += t.RallyFires; cd[s] += t.CoverDied;
                            if (t.CoverSaved > 0) sv[s][id] = t.CoverSaved;
                        }
                        hd[s] = p.Any(u => u.Def.Id == "hisa" && u.LastDeathTurn > 0) ? 1 : 0;
                        parts[s] = a;
                    });
                    var acc = new Agg(); foreach (var x in parts) acc.Add(x);
                    hisaDead = hd.Sum(); coverDied = cd.Sum();
                    saved = new(); foreach (var d2 in sv) foreach (var (k, v) in d2) saved[k] = saved.GetValueOrDefault(k) + v;
                    acc.CoverBattles(parts);
                    return acc;
                }
                var a2 = Run(S2, out var hd2, out _, out _); var a3 = Run(UnitCatalog.Hisa, out var hd3, out var sv3, out var cd3);
                Console.WriteLine($"| {n} | {label} | {Cell(a2)} → {Cell(a3)} | {a3.P(a3.CoverFires):F2}（{Pct(a3.CoverBattlesN, a3.N)}）| {Pct(cd3, a3.CoverFires)} | {Pct(hd2, a2.N)} → {Pct(hd3, a3.N)} | {a2.P(a2.RallyFires):F1} → {a3.P(a3.RallyFires):F1} | "
                    + string.Join(" ／ ", sv3.OrderByDescending(kv => kv.Value).Select(kv => $"{FvName(kv.Key)} {kv.Value}")) + " |");
            }
    }

    // ---------------------------------------------------------------------------------
    // 台本の例
    // ---------------------------------------------------------------------------------
    sealed record Show(string Label, string Row, string Wave, UnitDef Hisa, Func<BattleEvent, bool> Hit);

    static Show[] Shows() => new Show[]
    {
        new("玉が溜まる（`CommandBall`「溜まる」）", "試遊・標 循環", "boss", UnitCatalog.Hisa, x => x.Kind == BattleEventKind.CommandBall && x.Text == CommandBallLabels.Gain),
        new("玉を使う（`CommandBall`「使う」）", "試遊・標 循環", "boss", UnitCatalog.Hisa, x => x.Kind == BattleEventKind.CommandBall && x.Text == CommandBallLabels.Use),
        new("満杯で捨てる（`CommandBall`「捨てる」）", "試遊・標 循環", "boss", UnitCatalog.Hisa, x => x.Kind == BattleEventKind.CommandBall && x.Text == CommandBallLabels.Spill),
        new("号令（`Command`「手番」）", "試遊・標 循環", "boss", UnitCatalog.Hisa, x => x.Kind == BattleEventKind.Command),
        new("粛で黙る（`Framed`「黙る」）", "試遊・標 三人組", "2", UnitCatalog.Hisa, x => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Silenced),
        new("粛で黙る（`Framed`「黙る」）", Econ, "2", UnitCatalog.Hisa, x => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Silenced),
        new("庇い（`Cover`）", "試遊・標 三人組", "bat", UnitCatalog.Hisa, x => x.Kind == BattleEventKind.Cover),
        new("鼓舞（`Rouse`「次の手番まで」・HB-t）", "試遊・標 循環", "boss", UnitCatalog.HisaHBt, x => x.Kind == BattleEventKind.Rouse && x.Text == RouseLabels.Until),
        new("鼓舞が解ける（`Rouse`「解ける」・HB-t）", "試遊・標 循環", "boss", UnitCatalog.HisaHBt, x => x.Kind == BattleEventKind.Rouse && x.Text == RouseLabels.End),
        new("鼓舞（`Rouse`「戦の終わりまで」・HB-p）", "試遊・標 循環", "boss", UnitCatalog.HisaHBp, x => x.Kind == BattleEventKind.Rouse && x.Text == RouseLabels.Stay),
    };

    static Formation RowOf(string n) => n == Hisa301Diag.NoZanName ? Hisa301Diag.NoZan : n.StartsWith("試遊", StringComparison.Ordinal) ? Playtest(n) : CompareRow(n);

    static void Find(int seeds)
    {
        Console.WriteLine($"# 第302期 台本の例（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 見せ場 | 台 × 波 | 1戦あたりの件数 | 出た戦 | seed 0 の件数（最初の T） | 最初に出る seed（最初の T） |");
        Console.WriteLine("|---|---|--:|--:|---|---|");
        foreach (var s in Shows())
        {
            var f = With(RowOf(s.Row), s.Hisa); var w = WaveOf(s.Wave);
            var cnt = new int[seeds]; var first = new int[seeds];
            Parallel.For(0, seeds, sd =>
            {
                var r = BattleEngine.Run(Mat(f), w.Make(), sd, verbose: true);
                var hits = r.Events.Where(s.Hit).ToList();
                cnt[sd] = hits.Count; first[sd] = hits.Count > 0 ? hits[0].Turn : -1;
            });
            int fs = Array.FindIndex(cnt, c => c > 0);
            Console.WriteLine($"| {s.Label} | {s.Row} × {w.Name} | {cnt.Average():F2} | {cnt.Count(c => c > 0)} / {seeds} | {cnt[0]}{(cnt[0] > 0 ? $"（T{first[0]}）" : "")} | {(fs < 0 ? "—" : $"seed {fs}（T{first[fs]}）")} |");
        }
    }

    static void Memo(string rowPart, string wave, int seed, string ver, int lastTurn)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(rowPart, StringComparison.Ordinal));
        var v = new[] { V0, V1, V2, V3, VHV, VBt, VBp }.First(b => b.Key == ver);
        var w = WaveOf(wave);
        var p = Mat(With(f0, v.D));
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# hisa302 memo —— {name} × {w.Name} × seed {seed} × {v.Key} {v.Name} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Turn > lastTurn) break;
            if (x.Kind is not (BattleEventKind.Damage or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.Feather or BattleEventKind.MarkRally
                or BattleEventKind.Death or BattleEventKind.Attack or BattleEventKind.VendettaRound or BattleEventKind.Command or BattleEventKind.Cover
                or BattleEventKind.CommandBall or BattleEventKind.Rouse or BattleEventKind.Sealed or BattleEventKind.StatusGain)) continue;
            if (x.Kind == BattleEventKind.StatusGain && x.Text != StatusKeys.Marked) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.PartnerId is int pa ? $"  Partner={N(pa)}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}");
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
    static readonly MethodInfo RallyHealM = typeof(BattleContext).GetMethod("RallyHeal", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("RallyHeal");
    static readonly MethodInfo Drain = typeof(BattleContext).GetMethod("DrainFeatherMarksPublic", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("DrainFeatherMarksPublic");
    static readonly UnitDef HushDummy = new() { Id = "hushtest", Name = "粛の試験体", MaxHp = 99, Attack = 1, Speed = 1, Traits = new[] { TraitId.Hush } };
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();
    static UnitTally TalOf(BattleContext ctx, UnitState u)
    {
        var d = (Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!;
        if (!d.TryGetValue(u.Def.Id, out var t)) d[u.Def.Id] = t = new UnitTally();
        return t;
    }

    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = Mat(pl);
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
        Console.WriteLine("# hisa302 check —— 第302期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        var hisa = UnitCatalog.HisaH302; var old = UnitCatalog.HisaH301;   // 第303期: ヒサは第302期の規定に固定
        Expect("(a) 定義: 規定のヒサ ＝ `HisaH301` の `FrameAccuse` を `FrameAccuseQuiet` に差し替え ＋ `CommandBall` ＋ `HisaCover`・旧は `All` ／ `Everyone` の外・数値と手番は旧のまま・文面は末尾に足しただけ",
            hisa.Traits.SequenceEqual(old.Traits.Select(t => t == TraitId.FrameAccuse ? TraitId.FrameAccuseQuiet : t).Append(TraitId.CommandBall).Append(TraitId.HisaCover))
            && old.Traits.SequenceEqual(UnitCatalog.HisaHKf.Traits.Append(TraitId.FrameAccuse)) && hisa.PlusText.StartsWith(old.PlusText, StringComparison.Ordinal)
            && hisa.PlusText.Contains("玉") && hisa.PlusText.Contains("仲間の前に立つ") && hisa.MinusText == old.MinusText && hisa.MaxHp == old.MaxHp && hisa.Speed == old.Speed && hisa.Attack == old.Attack
            && hisa.Actions!.SequenceEqual(old.Actions!) && UnitCatalog.All.Contains(UnitCatalog.Hisa) && !UnitCatalog.Everyone.Contains(old)
            && UnitCatalog.HisaHLt3.Traits.SequenceEqual(old.Traits.Append(TraitId.CommandTurn3)) && UnitCatalog.HisaHC.Traits.SequenceEqual(old.Traits.Append(TraitId.HisaCover)));
        Expect("(b) 段1 の版: HV-s ＝ 規定 ＋ `RallyQuiet`・HB-t ／ HB-p ＝ 規定の `CommandBall` を `CommandRouse` ／ `CommandRouseStay` に差し替え・`All` ／ `Everyone` の外・定数（20 ／ 3 ／ 40 ／ +5）",
            UnitCatalog.HisaHVs.Traits.SequenceEqual(hisa.Traits.Append(TraitId.RallyQuiet))
            && UnitCatalog.HisaHBt.Traits.SequenceEqual(hisa.Traits.Select(t => t == TraitId.CommandBall ? TraitId.CommandRouse : t))
            && UnitCatalog.HisaHBp.Traits.SequenceEqual(hisa.Traits.Select(t => t == TraitId.CommandBall ? TraitId.CommandRouseStay : t))
            && !new[] { UnitCatalog.HisaHVs, UnitCatalog.HisaHBt, UnitCatalog.HisaHBp }.Any(UnitCatalog.Everyone.Contains)
            && CommandTrait.Every == 20 && CommandTrait.BallCap == 3 && CommandTrait.HeavyEvery == 40 && CommandTrait.RousePerBall == 5
            && CommandTrait.PoolCapOf(Mat(Formation.Build(back3: hisa))[0]) == 60
            && CommandTrait.PoolCapOf(Mat(Formation.Build(back3: UnitCatalog.HisaHBt))[0]) == 120
            && CommandTrait.PoolCapOf(Mat(Formation.Build(back3: UnitCatalog.HisaHLt3))[0]) == 0);

        // (c) 段0-1: 粛（保持者が生きている）／ 痺れで指差しが出ない・叫びは出る ／ 粛の保持者が倒れた後は指差しが出る
        (int Mark, long Sil, long Rally) Accuse(UnitDef h, bool hush, bool stun, bool hushDead)
        {
            var en = hush ? Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gald, back3: HushDummy) : Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gald);
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Borg, center: UnitCatalog.Dolga, back3: h), en, out var p, out var e);
            var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id); var borg = p.First(u => u.Def.Id == UnitCatalog.Borg.Id); var hi = p.First(u => u.Def.Id == "hisa");
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 900; }
            if (hushDead) { var d = e.First(u => u.Def.Id == "hushtest"); ctx.ApplyDamage(d, 5000, golm, pattern: AttackPattern.Single); }
            if (stun) hi.SetCounter(StatusKeys.Stun, 1);
            golm.SetCounter(StatusKeys.Marked, 1);
            ctx.ApplyDamage(golm, 10, borg, isFriendlyFire: true, pattern: AttackPattern.Single);
            int marks = e.Where(u => u.Def.Id != "hushtest").Sum(x => x.RawCounter(StatusKeys.Marked));
            // 叫び: 標の敵を撃つ（反撃の枠 ＝ 攻撃のひとまとまり）
            var g = e.First(u => u.Def.Id == UnitCatalog.Gald.Id);
            if (g.RawCounter(StatusKeys.Marked) <= 0) g.SetCounter(StatusKeys.Marked, 1);
            ctx.Reaction(() => ctx.ApplyDamage(g, 1, borg, pattern: AttackPattern.Single));
            return (marks, Tal(ctx, "hisa").AccuseSilenced, Tal(ctx, "hisa").RallyFires);
        }
        var c0 = Accuse(hisa, false, false, false); var c1 = Accuse(hisa, true, false, false); var c2 = Accuse(hisa, false, true, false); var c3 = Accuse(hisa, true, false, true);
        var c4 = Accuse(old, true, false, false); var c5 = Accuse(UnitCatalog.HisaHVs, true, false, false);
        Expect("(c) 段0-1: 粛（保持者が生きている）／ 痺れで指差しが出ない・叫びは出る・粛の保持者が倒れた後は出る・旧 `HisaH301` は粛でも出る・HV-s は叫びも出ない",
            c0.Mark == 1 && c0.Sil == 0 && c1.Mark == 0 && c1.Sil == 1 && c1.Rally == 1 && c2.Mark == 0 && c2.Sil == 1 && c3.Mark == 1 && c3.Sil == 0 && c4.Mark == 1 && c4.Sil == 0 && c5.Mark == 0 && c5.Rally == 0,
            $"なし {c0.Mark}・粛 {c1.Mark}（黙る {c1.Sil}・叫び {c1.Rally}）・痺れ {c2.Mark}・粛の後 {c3.Mark}・旧 {c4.Mark}・HV-s 叫び {c5.Rally}");

        // (d) 段0-2: 溜まりは 60 を超えない・超えた分は捨てる・手番の後の端数は持ち越す・1手番 3 層
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Tome, back3: hisa), Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gald), out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa"); var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id);
            foreach (var u in p.Concat(e)) { u.MaxHp = 5000; u.Hp = 5000; }
            var ht = TalOf(ctx, hi);
            hi.SetCounter(CommandTrait.PoolKey, 50);
            RallyHealM.Invoke(ctx, new object[] { hi, golm, 30, golm, 5, ht, true });   // 満タンに 30 → 溢れ 30・60 で頭打ち・20 を捨てる
            int pool1 = hi.RawCounter(CommandTrait.PoolKey); long drop1 = ht.CommandDropped, dropEv1 = ht.CommandDropEvents;
            e[0].SetCounter(StatusKeys.Marked, 1);
            hi.SetCounter(CommandTrait.PoolKey, 59);
            ctx.CommandTurnFire(hi, CommandTrait.TurnCap(hi));
            int left = hi.RawCounter(CommandTrait.PoolKey); int layers = e.Sum(x => x.RawCounter(StatusKeys.Marked)) - 1;
            hi.SetCounter(CommandTrait.PoolKey, 60);
            ctx.CommandTurnFire(hi, CommandTrait.TurnCap(hi));
            int left2 = hi.RawCounter(CommandTrait.PoolKey); int layers2 = e.Sum(x => x.RawCounter(StatusKeys.Marked)) - 1 - layers;
            Drain.Invoke(ctx, null);
            // 上限なし（第301期の HL-t3）は 60 を超えて溜まる
            var ctx2 = Ctx(Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Tome, back3: UnitCatalog.HisaHLt3), Formation.Build(front1: UnitCatalog.Gald), out var p2, out _);
            var hi2 = p2.First(u => u.Def.Id == "hisa"); var golm2 = p2.First(u => u.Def.Id == UnitCatalog.Golm.Id);
            golm2.MaxHp = 100; golm2.Hp = 100; hi2.SetCounter(CommandTrait.PoolKey, 50);
            RallyHealM.Invoke(ctx2, new object[] { hi2, golm2, 30, golm2, 5, TalOf(ctx2, hi2), true });
            Expect("(d) 段0-2: 溜まりは 60 で頭打ち（50 ＋ 30 → 60・20 を捨てる）・59 → 2 層 ＋ 端数 19 を持ち越す・60 → 3 層・上限なし（HL-t3）は 80",
                pool1 == 60 && drop1 == 20 && dropEv1 == 1 && layers == 2 && left == 19 && layers2 == 3 && left2 == 0 && hi2.RawCounter(CommandTrait.PoolKey) == 80,
                $"溜まり {pool1}・捨てた {drop1}・59 → 層 {layers} ／ 残り {left}・60 → 層 {layers2} ／ 残り {left2}・上限なし {hi2.RawCounter(CommandTrait.PoolKey)}");
        }
        {
            long over = 0, maxPool = 0, battles = 0;
            foreach (var (n, f) in Boards()) foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf)) for (int s = 0; s < 5; s++)
                    {
                        var r = BattleEngine.Run(Mat(f), w.Make(), s, verbose: true);
                        battles++;
                        foreach (var x in r.Events.Where(x => x.Kind == BattleEventKind.CommandBall)) { if (x.Slot > maxPool) maxPool = x.Slot; if (x.Amount > 3) over++; }
                        foreach (var x in r.Events.Where(x => x.Kind == BattleEventKind.Command)) if (x.Amount > 3) over++;
                    }
            Expect("(d2) 段0-2: 台本で溜まりが 60 を超えない・玉は 3 つまで・1手番 3 層まで（代表台 × 精鋭 ／ ボス × seed 0..4）", maxPool <= 60 && over == 0 && maxPool > 0, $"{battles} 戦・溜まりの最大 {maxPool}・超え {over}");
        }

        // (e) 段0-3: 第301期の (i) ／ (j) ／ (k) が規定で通る
        (bool Covered, int AllyHp, int HisaMark, long FeatherAlly, long Skipped) Cover(bool friendly, bool second, bool hush)
        {
            var en = hush ? Formation.Build(front1: UnitCatalog.Gald, back3: HushDummy) : Formation.Build(front1: UnitCatalog.Gald);
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Borg, center: UnitCatalog.Tome, back3: hisa), en, out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa"); var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id); var borg = p.First(u => u.Def.Id == UnitCatalog.Borg.Id);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            golm.Hp = 20;
            if (second) hi.SetCounter(CoverTrait.UsedKey, 1);
            ctx.ApplyDamage(golm, 40, friendly ? borg : e[0], isFriendlyFire: friendly, pattern: AttackPattern.Single);
            Drain.Invoke(ctx, null);
            return (Tal(ctx, "hisa").CoverFires > 0, golm.Hp, hi.RawCounter(StatusKeys.Marked), Tal(ctx, "tome").MfShotsAlly, Tal(ctx, "hisa").CoverFeatherSkipped);
        }
        var k1 = Cover(false, false, false); var k2 = Cover(true, false, false); var k3 = Cover(false, true, false); var k4 = Cover(false, false, true);
        Expect("(e) 段0-3（第301期の (i) ／ (j)）: 敵の倒れる一撃を規定のヒサが受ける（味方は無傷・ヒサに標・羽は飛ばない）・同士討ち ／ 2度目は庇わない・粛で止まる",
            k1.Covered && k1.AllyHp == 20 && k1.HisaMark == 1 && k1.FeatherAlly == 0 && k1.Skipped == 1 && !k2.Covered && !k3.Covered && !k4.Covered && k4.AllyHp <= 0,
            $"敵 {k1.Covered}（味方 HP {k1.AllyHp}・標 {k1.HisaMark}・羽 {k1.FeatherAlly}）・同士討ち {k2.Covered}・2度目 {k3.Covered}・粛 {k4.Covered}");
        {
            long maxPer = 0, battles = 0;
            foreach (var w in MainWaves()) for (int s = 0; s < 20; s++)
                {
                    var r = BattleEngine.Run(Mat(Playtest("試遊・標 三人組")), w.Make(), s, verbose: false);
                    long c = r.TallyByUnit.TryGetValue("hisa", out var ht) ? ht.CoverFires : 0;
                    maxPer = Math.Max(maxPer, c); if (c > 0) battles++;
                }
            Expect("(e2) 段0-3（第301期の (k)）: 1戦に1度（規定の三人組 × 7 波 × seed 0..19）", maxPer == 1 && battles > 0, $"1戦の最大 {maxPer}・出た戦 {battles}");
        }

        // (f) HB-t ／ HB-p: 玉を全部使う・層 ＋ 鼓舞・期限・重ね
        (int Layers1, int Atk1, int Atk2, int Pool, int Layers0, int Atk0) Rouse(UnitDef h, bool misa)
        {
            var ctx = Ctx(misa ? Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Tome, back3: h) : Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Dolga, back3: h),
                Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gald), out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa"); var golm = p.First(u => u.Def.Id == UnitCatalog.Golm.Id);
            foreach (var u in p.Concat(e)) { u.MaxHp = 5000; u.Hp = 5000; }
            int a0 = golm.CurrentAttack;
            e[0].SetCounter(StatusKeys.Marked, 1);
            hi.SetCounter(CommandTrait.PoolKey, 130);
            ctx.CommandTurnFire(hi, CommandTrait.TurnCap(hi));
            int l1 = e.Sum(x => x.RawCounter(StatusKeys.Marked)) - 1; int atk1 = golm.CurrentAttack - a0; int pool = hi.RawCounter(CommandTrait.PoolKey);
            hi.SetCounter(CommandTrait.PoolKey, 40);
            ctx.CommandTurnFire(hi, CommandTrait.TurnCap(hi));
            int atk2 = golm.CurrentAttack - a0;
            hi.SetCounter(CommandTrait.PoolKey, 0);
            ctx.CommandTurnFire(hi, CommandTrait.TurnCap(hi));
            int atk0 = golm.CurrentAttack - a0;
            Drain.Invoke(ctx, null);
            return (l1, atk1, atk2, pool, e.Sum(x => x.RawCounter(StatusKeys.Marked)) - 1 - l1, atk0);
        }
        var rt = Rouse(UnitCatalog.HisaHBt, true); var rp = Rouse(UnitCatalog.HisaHBp, true); var rn = Rouse(UnitCatalog.HisaHBt, false);
        Expect("(f) HB-t: 130 → 玉 3（端数 10）・層 3・攻撃 +15 → 次の手番（玉 1）で +5 に上書き → 玉 0 の手番で解ける ／ HB-p: +15 → +20 → +20 ／ ミサなし（標の敵）: 層 0 でも玉を使って +15",
            rt.Layers1 == 3 && rt.Atk1 == 15 && rt.Pool == 10 && rt.Atk2 == 5 && rt.Atk0 == 0 && rp.Atk1 == 15 && rp.Atk2 == 20 && rp.Atk0 == 20 && rn.Layers1 == 0 && rn.Atk1 == 15 && rn.Pool == 10,
            $"HB-t 層 {rt.Layers1}・攻 +{rt.Atk1} → +{rt.Atk2} → +{rt.Atk0}・残り {rt.Pool} ／ HB-p +{rp.Atk1} → +{rp.Atk2} → +{rp.Atk0} ／ ミサなし 層 {rn.Layers1}・+{rn.Atk1}");

        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(g) `PickOne(` の出現数が第301期と同じ（BattleCore）", pick == 36, $"{pick}");

        {
            int diff = 0, n2 = 0;
            foreach (var d in new[] { hisa, UnitCatalog.HisaHVs, UnitCatalog.HisaHBt, UnitCatalog.HisaHBp })
                foreach (var (_, f) in Boards()) foreach (var w in new[] { "2", "guard", "bat", "boss" }.Select(WaveOf)) for (int s = 0; s < 5; s++)
                        {
                            var g = With(f, d);
                            var a = BattleEngine.Run(Mat(g), w.Make(), s, verbose: false);
                            var b = BattleEngine.Run(Mat(g), w.Make(), s, verbose: true);
                            n2++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                        }
            Expect("(h) verbose の有無で勝敗・決着T が同じ（規定 ／ HV-s ／ HB-t ／ HB-p × 代表台 × 第2波 ／ 精鋭 ／ ボス × seed 0..4）", diff == 0, $"{n2} 戦・違い {diff}");
        }

        long Ev(Formation f, string wave, Func<BattleEvent, bool> hit) { long x = 0; for (int s = 0; s < 10; s++) x += BattleEngine.Run(Mat(f), WaveOf(wave).Make(), s, verbose: true).Events.Count(hit); return x; }
        var cyc = Playtest("試遊・標 循環");
        long gain = Ev(cyc, "boss", x => x.Kind == BattleEventKind.CommandBall && x.Text == CommandBallLabels.Gain), use = Ev(cyc, "boss", x => x.Kind == BattleEventKind.CommandBall && x.Text == CommandBallLabels.Use),
             spill = Ev(cyc, "boss", x => x.Kind == BattleEventKind.CommandBall && x.Text == CommandBallLabels.Spill), oldBall = Ev(With(cyc, UnitCatalog.HisaHLt3), "boss", x => x.Kind == BattleEventKind.CommandBall),
             sil = Ev(CompareRow(Econ), "2", x => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Silenced) + Ev(Playtest("試遊・標 三人組"), "2", x => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Silenced),
             silOld = Ev(With(CompareRow(Econ), old), "2", x => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Silenced),
             ru = Ev(With(cyc, UnitCatalog.HisaHBt), "boss", x => x.Kind == BattleEventKind.Rouse && x.Text == RouseLabels.Until), re = Ev(With(cyc, UnitCatalog.HisaHBt), "boss", x => x.Kind == BattleEventKind.Rouse && x.Text == RouseLabels.End),
             rs = Ev(With(cyc, UnitCatalog.HisaHBp), "boss", x => x.Kind == BattleEventKind.Rouse && x.Text == RouseLabels.Stay), r0 = Ev(cyc, "boss", x => x.Kind == BattleEventKind.Rouse);
        Expect("(i) 出来事: 規定で玉（溜まる ／ 使う ／ 捨てる）が出る・上限なしの HL-t3 には出ない・第2波で「黙る」が出る（旧は 0）・鼓舞は HB-t（次の手番まで ／ 解ける）／ HB-p（戦の終わりまで）だけ（seed 0..9）",
            gain > 0 && use > 0 && spill > 0 && oldBall == 0 && sil > 0 && silOld == 0 && ru > 0 && re > 0 && rs > 0 && r0 == 0,
            $"溜まる {gain} ／ 使う {use} ／ 捨てる {spill}・HL-t3 {oldBall}・黙る {sil}（旧 {silOld}）・鼓舞 {ru} ／ {re} ／ {rs}（規定 {r0}）");
        {
            // 鼓舞の出来事は号令の直後・羽より前
            long bad = 0, n3 = 0;
            for (int s = 0; s < 10; s++)
            {
                var r = BattleEngine.Run(Mat(With(cyc, UnitCatalog.HisaHBt)), WaveOf("boss").Make(), s, verbose: true);
                var ev = r.Events.ToList();
                for (int i = 0; i < ev.Count; i++)
                {
                    if (ev[i].Kind != BattleEventKind.Rouse || ev[i].Text != RouseLabels.Until) continue;
                    n3++;
                    int c = ev.FindLastIndex(i, x => x.Kind == BattleEventKind.Command);
                    int fm = ev.FindIndex(c < 0 ? 0 : c, x => x.Kind == BattleEventKind.FeatherMark);
                    if (c < 0 || (fm >= 0 && fm < i && ev[fm].Turn == ev[i].Turn)) bad++;
                }
            }
            Expect("(j) 台本の順: 号令 → 鼓舞 → 羽（HB-t × 循環 × ボス × seed 0..9）", n3 > 0 && bad == 0, $"鼓舞 {n3}・順の崩れ {bad}");
        }
        Expect("(k) 手番の外の経路の名前の数 ＝ 列挙の数（`OutOfTurnRoutes.Names`・第301期の庇いの名前の抜けを直した）", OutOfTurnRoutes.Count == Enum.GetValues<OutOfTurnRoute>().Length
            && OutOfTurnRoutes.Names[(int)OutOfTurnRoute.Cover] == "庇い" && OutOfTurnRoutes.Names[(int)OutOfTurnRoute.Accuse] == "指差し" && OutOfTurnRoutes.Names[(int)OutOfTurnRoute.Rally] == "叫び",
            $"{OutOfTurnRoutes.Count}");

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
