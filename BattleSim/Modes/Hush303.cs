using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// hush303 —— 第303期「粛でヒサが完全に黙る（HV-s）の規定化 ＋ 標軸の『粛を狩る』版（Q1 ／ Q3 ／ QA）＋ 庇いの版（HC-d ／ HC-s）」。
// 指示書は design/PHASE303_HUSH_HUNT_SPEC.md ／ 報告は design/PHASE303_HUSH_HUNT.md。
//
//     dotnet run --project BattleSim -c Release 0 hush303 p0 [seeds]       # §5 Phase 0（粛の伝令の席・倒れたT・倒した駒・受けた攻撃 ／ 倒れた後に決まった割合 ／ 手番の順）
//     dotnet run --project BattleSim -c Release 0 hush303 cover [seeds]    # §4 ／ §5-4 庇いの落ちの分解（庇いなし ／ 規定 ／ 空の庇い × 4 帯）と HC-d ／ HC-s（`docs/elite.md` のヒサ在席の行）
//     dotnet run --project BattleSim -c Release 0 hush303 hunt [seeds]     # §6-3 段1-A（S ／ Q1 ／ Q3 ／ QA）× 代表台 × 全波 ＋ 第2波の粛の伝令・身振り・戻った割り込み
//     dotnet run --project BattleSim -c Release 0 hush303 wave2 [seeds]    # §6-3 標軸の行 × 第2波（S → Q1 → Q3 → QA）と、粛が止めた動作
//     dotnet run --project BattleSim -c Release 0 hush303 ver [seeds]      # 段1-B の HC-d ／ HC-s × 代表台 × 全波
//     dotnet run --project BattleSim -c Release 0 hush303 cmp [seeds]      # `compare` のヒサ在席の行 × 版（第302期の規定 ／ S ／ Q1 ／ Q3 ／ QA ／ HC-d ／ HC-s）と (G2)
//     dotnet run --project BattleSim -c Release 0 hush303 find [seeds]     # 台本の例（身振り → 仇巡り ／ 一斉射撃 → 粛が倒れる → 声が戻る）
//     dotnet run --project BattleSim -c Release 0 hush303 memo <台の一部> <1..5|guard|bat|boss> <seed> [S|Q1|Q3|QA|HCd|HCs|HCp] [最後のT]
//     dotnet run --project BattleSim -c Release 0 hush303 check            # 自己検査
// =====================================================================================
static class Hush303Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": Phase0(int.Parse(A(3, "200"))); return;
            case "cover": Cover(int.Parse(A(3, "200"))); return;
            case "hunt": Hunt(int.Parse(A(3, "200"))); return;
            case "wave2": Wave2(int.Parse(A(3, "200"))); return;
            case "ver": CoverVersions(int.Parse(A(3, "200"))); return;
            case "cmp": Cmp(int.Parse(A(3, "200"))); return;
            case "find": Find(int.Parse(A(3, "50"))); return;
            case "memo": Memo(A(3, "三人組"), A(4, "2"), int.Parse(A(5, "0")), A(6, "QA"), int.Parse(A(7, "99"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("hush303: モードは p0 / cover / hunt / wave2 / ver / cmp / find / memo / check。"); return;
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
    /// <summary>庇いなし（対照）＝ 規定から `HisaCover` を外しただけ。</summary>
    static readonly UnitDef NoCover = Def(UnitCatalog.Hisa, UnitCatalog.Hisa.Traits.Where(t => t != TraitId.HisaCover));

    internal sealed record Ver(string Key, string Name, UnitDef D);
    static readonly Ver VP = new("P302", "第302期の規定", UnitCatalog.HisaH302);
    static readonly Ver VS = new("S", "規定（HV-s）", UnitCatalog.Hisa);
    static readonly Ver VQ1 = new("Q1", "Q1 身振り +1", UnitCatalog.HisaQ1);
    static readonly Ver VQ3 = new("Q3", "Q3 身振り +3", UnitCatalog.HisaQ3);
    static readonly Ver VQA = new("QA", "QA 身振り +3 ＋ 粛を最優先", UnitCatalog.HisaQA);
    static readonly Ver VNC = new("NC", "庇いなし（対照）", NoCover);
    static readonly Ver VCp = new("HCp", "空の庇い（対照）", UnitCatalog.HisaHCp);
    static readonly Ver VCd = new("HCd", "HC-d 肩代わり役は庇わない", UnitCatalog.HisaHCd);
    static readonly Ver VCs = new("HCs", "HC-s 自分が倒れる一撃は庇わない", UnitCatalog.HisaHCs);
    static readonly Ver[] HuntVers = { VS, VQ1, VQ3, VQA };
    static Ver[] AllVers => new[] { VP, VS, VQ1, VQ3, VQA, VNC, VCp, VCd, VCs };

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
    static (string Name, Formation F)[] Boards() =>
        Presets.Playtest.Where(r => r.Name.StartsWith("試遊・標", StringComparison.Ordinal) && r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hisa))).Select(r => (r.Name, r.F))
            .Append((Econ, CompareRow(Econ)))
            .Append((Hisa301Diag.NoZanName, Hisa301Diag.NoZan)).ToArray();
    static (string Name, Formation F)[] HisaRows() => CompareBuilds().Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hisa))).ToArray();
    static (string Name, Formation F)[] MarkRows() => Boards().Concat(HisaRows().Where(r => r.Name != Econ)).ToArray();
    static Formation RowOf(string n) => n == Hisa301Diag.NoZanName ? Hisa301Diag.NoZan : n.StartsWith("試遊", StringComparison.Ordinal) ? Playtest(n) : CompareRow(n);

    // ---------------------------------------------------------------------------------
    // 1戦の集計（台本を読む・verbose）
    // ---------------------------------------------------------------------------------
    sealed class Agg
    {
        public long N, Wins, WinT, Turns, HisaDeadN, HisaDeadT, HushN, HushDeadN, HushDeadT, HushDeadWinN, HushDeadWinT, HushDeadLoseN, HushDeadLoseT, DecidedAfter, DecidedAfterWin,
            Gestures, GestureLayers, AfterVend, AfterFeather, AfterAccuse, AfterRally, AfterCover, HushAvenge, HushAccuse, HushFeather, HushCover, HushRally,
            CoverFires, CoverDied, CoverSkipShoulder, CoverSkipLethal, CoverPlacebos, CoverEstWrong, HushHitRound, HushHitVolley, HushHitOther;
        public readonly Dictionary<string, long> Killer = new(), HushHitBy = new(), Saved = new();
        public void Add(Agg o)
        {
            foreach (var f in typeof(Agg).GetFields())
                if (f.FieldType == typeof(long)) f.SetValue(this, (long)f.GetValue(this)! + (long)f.GetValue(o)!);
            foreach (var (k, v) in o.Killer) Killer[k] = Killer.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.HushHitBy) HushHitBy[k] = HushHitBy.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Saved) Saved[k] = Saved.GetValueOrDefault(k) + v;
        }
        public double P(long x) => N == 0 ? 0 : (double)x / N;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public double WinTurns => Wins == 0 ? 0 : (double)WinT / Wins;
    }

    static Agg One(Formation f, Func<List<UnitState>> enemy, int seed, bool verbose)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = enemy();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        var a = new Agg { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var hisa = p.FirstOrDefault(u => u.Def.Id == "hisa");
        if (hisa is not null && hisa.LastDeathTurn > 0) { a.HisaDeadN = 1; a.HisaDeadT = hisa.LastDeathTurn; }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!mine.Contains(id)) continue;
            a.Gestures += t.GestureFires; a.GestureLayers += t.GestureLayers;
            a.CoverFires += t.CoverFires; a.CoverDied += t.CoverDied; a.CoverSkipShoulder += t.CoverSkipShoulder; a.CoverSkipLethal += t.CoverSkipLethal;
            a.CoverPlacebos += t.CoverPlacebos; a.CoverEstWrong += t.CoverEstLethalWrong;
            if (t.CoverSaved > 0) a.Saved[id] = t.CoverSaved;
        }
        var hr = r.BoardRules.HushByRoute;
        a.HushAvenge = hr[(int)OutOfTurnRoute.Avenge][1]; a.HushAccuse = hr[(int)OutOfTurnRoute.Accuse][1]; a.HushFeather = hr[(int)OutOfTurnRoute.FeatherMark][1];
        a.HushCover = hr[(int)OutOfTurnRoute.Cover][1]; a.HushRally = hr[(int)OutOfTurnRoute.Rally][1];
        var herald = e.FirstOrDefault(u => u.Def.Traits.Contains(TraitId.Hush));
        if (herald is null) return a;
        a.HushN = 1;
        if (herald.LastDeathTurn > 0)
        {
            a.HushDeadN = 1; a.HushDeadT = herald.LastDeathTurn;
            if (r.PlayerWon) { a.HushDeadWinN = 1; a.HushDeadWinT = herald.LastDeathTurn; } else { a.HushDeadLoseN = 1; a.HushDeadLoseT = herald.LastDeathTurn; }
            if (r.Turns > herald.LastDeathTurn) { a.DecidedAfter = 1; if (r.PlayerWon) a.DecidedAfterWin = 1; }
        }
        if (!verbose) return a;
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Id);
        int zan = p.FirstOrDefault(u => u.Def.Id == "zan")?.InstanceId ?? -1, misa = p.FirstOrDefault(u => u.Def.Id == "tome")?.InstanceId ?? -1;
        bool died = false, inRound = false, inVolley = false;
        foreach (var x in r.Events)
        {
            if (x.Kind == BattleEventKind.VendettaRound && x.Text == VendettaRoundLabels.Start) { inRound = x.ActorId == zan; inVolley = false; }
            else if (x.Kind == BattleEventKind.Feather && x.Text == FeatherLabels.Volley) { inVolley = x.ActorId == misa; inRound = false; }
            else if (x.Kind == BattleEventKind.Attack && !x.Reaction && x.ActorId != zan && x.ActorId != misa) { inRound = false; inVolley = false; }
            if (!died && x.Kind == BattleEventKind.Damage && x.TargetId == herald.InstanceId && x.ActorId is int ai && names.TryGetValue(ai, out var who) && mine.Contains(who))
            {
                a.HushHitBy[who] = a.HushHitBy.GetValueOrDefault(who) + 1;
                if (ai == zan && inRound && !x.Reaction) a.HushHitRound++;
                else if (ai == misa && inVolley && !x.Reaction) a.HushHitVolley++;
                else a.HushHitOther++;
            }
            if (!died && x.Kind == BattleEventKind.Death && x.TargetId == herald.InstanceId)
            {
                died = true;
                string k = x.ActorId is int ki && names.TryGetValue(ki, out var kn) ? kn : "—";
                a.Killer[k] = a.Killer.GetValueOrDefault(k) + 1;
                continue;
            }
            if (!died) continue;
            if (x.Kind == BattleEventKind.Damage && x.Reaction && x.ActorId == zan) a.AfterVend++;
            else if (x.Kind == BattleEventKind.FeatherMark) a.AfterFeather++;
            else if (x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Accuse) a.AfterAccuse++;
            else if (x.Kind == BattleEventKind.MarkRally) a.AfterRally++;
            else if (x.Kind == BattleEventKind.Cover) a.AfterCover++;
        }
        return a;
    }

    static Agg Many(Formation f, Func<List<UnitState>> enemy, int seeds, bool verbose = false, int from = 0)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, s => parts[s] = One(f, enemy, from + s, verbose));
        var a = new Agg();
        foreach (var x in parts) a.Add(x);
        return a;
    }

    static string Pct(long x, long n) => n == 0 ? "—" : $"{100.0 * x / n:F0}%";
    static string Avg(long s, long n) => n == 0 ? "—" : $"{(double)s / n:F1}";
    static string Cell(Agg a) => a.Wins == 0 ? $"{a.Win:F1}" : $"{a.Win:F1}（{a.WinTurns:F1}）";
    static string Dict(Dictionary<string, long> d, Agg a) => d.Count == 0 ? "—" : string.Join(" ／ ", d.OrderByDescending(kv => kv.Value).Select(kv => $"{(kv.Key == "—" ? "—" : FvName(kv.Key))} {a.P(kv.Value):F2}"));

    // ---------------------------------------------------------------------------------
    // §5 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0(int seeds)
    {
        var w2 = WaveOf("2");
        var en = BattleEngine.Materialize(EnemyCatalog.Stages[1].Enemy, BattleContext.EnemyTeam);
        var herald = en.First(u => u.Def.Traits.Contains(TraitId.Hush));
        Console.WriteLine($"# 第303期 Phase 0（seed 0..{seeds - 1}・規定 S ＝ HV-s・第2波）");
        Console.WriteLine();
        Console.WriteLine("## 1. 粛の伝令");
        Console.WriteLine();
        Console.WriteLine($"席: {herald.Slot}（{FormationRules.RowOf(herald.Slot)}・`FormationShape.X`）。HP {herald.MaxHp}（倍率の後）／ 攻 {herald.Def.Attack} ／ 速 {herald.Def.Speed}。第2波の顔ぶれ: " + string.Join("・", en.Select(u => $"{u.Def.Name}（席 {u.Slot}・速 {u.Def.Speed}・HP {u.MaxHp}）")) + "。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率（倒しT） | 粛が倒れた戦 | 倒れたT 勝ち戦 ／ 負け戦 | 倒した駒（1戦） | 倒れるまでに受けた攻撃（1戦・仇巡り ／ 一斉射撃 ／ そのほか） | 受けた攻撃の駒 |");
        Console.WriteLine("|---|---|--:|---|---|---|---|");
        foreach (var (n, f) in Boards())
        {
            var a = Many(f, w2.Make, seeds, verbose: true);
            Console.WriteLine($"| {n} | {Cell(a)} | {Pct(a.HushDeadN, a.N)} | {Avg(a.HushDeadWinT, a.HushDeadWinN)}（{a.HushDeadWinN}）／ {Avg(a.HushDeadLoseT, a.HushDeadLoseN)}（{a.HushDeadLoseN}） | {Dict(a.Killer, a)} | {a.P(a.HushHitRound):F2} ／ {a.P(a.HushHitVolley):F2} ／ {a.P(a.HushHitOther):F2} | {Dict(a.HushHitBy, a)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 2. 粛の保持者が倒れた後に勝ち負けが決まった割合（第2波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 粛が倒れた戦 | うち倒れた後に決着 | 決着が後だった戦の勝率 | 粛が倒れなかった戦の勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var (n, f) in Boards())
        {
            var a = Many(f, w2.Make, seeds);
            long notDead = a.N - a.HushDeadN, notDeadWin = a.Wins - a.HushDeadWinN;
            Console.WriteLine($"| {n} | {Pct(a.HushDeadN, a.N)} | {Pct(a.DecidedAfter, a.HushDeadN)} | {Pct(a.DecidedAfterWin, a.DecidedAfter)} | {Pct(notDeadWin, notDead)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 3. 手番の順（第2波・速さ降順 → 陣営 → 席・逆位なし）");
        Console.WriteLine();
        foreach (var (n, f) in Boards())
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var all = p.Concat(en).OrderByDescending(u => u.Def.Speed).ThenBy(u => u.TeamId).ThenBy(u => u.Slot)
                       .Select(u => $"{Short(u.Def)}{(u.TeamId == BattleContext.PlayerTeam ? "" : "（敵）")} {u.Def.Speed}");
            Console.WriteLine($"- {n}: " + string.Join(" → ", all));
        }
    }

    // ---------------------------------------------------------------------------------
    // §4 庇いの落ちの分解と HC-d ／ HC-s（`docs/elite.md` の行）
    // ---------------------------------------------------------------------------------
    static Agg Elite(Formation f, EnemyWave w, int seeds, int from)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, s => parts[s] = One(f, () => BattleEngine.MaterializeEnemy(w, EnemyCatalog.EliteScale), from + s, false));
        var a = new Agg(); foreach (var x in parts) a.Add(x); return a;
    }

    static void Cover(int seeds)
    {
        var waves = new[] { ("近衛", EnemyCatalog.EliteGuardWave), ("大隊", EnemyCatalog.EliteBattalionWave) };
        var rows = HisaRows();
        Console.WriteLine($"# 第303期 §4 庇いの落ちの分解と版（`docs/elite.md` のヒサ在席の行 × 近衛 ／ 大隊）");
        Console.WriteLine();
        Console.WriteLine("NC ＝ 庇いなし（規定から `HisaCover` を外した対照）／ S ＝ 規定（庇い HC）／ HCp ＝ 空の庇い（判定と1戦1度は本物と同じ・乱数を1つ引いて一撃は元の相手が受ける）。");
        Console.WriteLine("4 帯 ＝ seed 0..199 ／ 200..399 ／ 400..599 ／ 600..799 の勝率。**S − NC が帯をまたいで同じ符号で大きければ庇いそのもの、HCp − NC（乱数を1つずらしただけ）と同じ大きさなら乱数列のずれ**と読む。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | NC（4 帯） | S（4 帯） | HCp（4 帯） | S − NC（800） | HCp − NC（800） | 庇った（S・1戦） | 空の庇い（HCp・1戦） |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|--:|");
        foreach (var (n, f) in rows)
            foreach (var (label, w) in waves)
            {
                var res = new Dictionary<string, Agg[]>();
                foreach (var v in new[] { VNC, VS, VCp }) res[v.Key] = Enumerable.Range(0, 4).Select(b => Elite(With(f, v.D), w, seeds, b * 200)).ToArray();
                double W(string k) => res[k].Average(a => a.Win);
                string Bands(string k) => string.Join(" ／ ", res[k].Select(a => $"{a.Win:F1}"));
                var s = new Agg(); foreach (var x in res["S"]) s.Add(x);
                var cp = new Agg(); foreach (var x in res["HCp"]) cp.Add(x);
                Console.WriteLine($"| {n} | {label} | {Bands("NC")} | {Bands("S")} | {Bands("HCp")} | {W("S") - W("NC"):+0.0;-0.0;0.0} | {W("HCp") - W("NC"):+0.0;-0.0;0.0} | {s.P(s.CoverFires):F2} | {cp.P(cp.CoverPlacebos):F2} |");
            }
        Console.WriteLine();
        Console.WriteLine("## HC-d ／ HC-s（seed 0..799）");
        Console.WriteLine();
        Console.WriteLine("庇った ＝ 1戦あたり・庇った一撃でヒサが倒れた割合・見送った ＝ 版の条件で庇わなかった倒れる一撃（1戦）・外れ ＝ HC-s で「倒れない」と見積もって庇ったのに倒れた回数。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 勝率 NC ／ S ／ HC-d ／ HC-s | 庇った S ／ HC-d ／ HC-s | 倒れた S ／ HC-d ／ HC-s | 見送った HC-d ／ HC-s | 外れ HC-s | 庇われた駒（S）|");
        Console.WriteLine("|---|---|---|---|---|---|--:|---|");
        foreach (var (n, f) in rows)
            foreach (var (label, w) in waves)
            {
                Agg R(UnitDef d) { var a = new Agg(); for (int b = 0; b < 4; b++) a.Add(Elite(With(f, d), w, seeds, b * 200)); return a; }
                var nc = R(NoCover); var s = R(UnitCatalog.Hisa); var cd = R(UnitCatalog.HisaHCd); var cs = R(UnitCatalog.HisaHCs);
                Console.WriteLine($"| {n} | {label} | {nc.Win:F1} ／ {s.Win:F1} ／ {cd.Win:F1} ／ {cs.Win:F1} | {s.P(s.CoverFires):F2} ／ {cd.P(cd.CoverFires):F2} ／ {cs.P(cs.CoverFires):F2} | {Pct(s.CoverDied, s.CoverFires)} ／ {Pct(cd.CoverDied, cd.CoverFires)} ／ {Pct(cs.CoverDied, cs.CoverFires)} | {cd.P(cd.CoverSkipShoulder):F2} ／ {cs.P(cs.CoverSkipLethal):F2} | {cs.CoverEstWrong} | {Dict(s.Saved, s)} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // §6 段1-A
    // ---------------------------------------------------------------------------------
    static void Hunt(int seeds)
    {
        var waves = AllWaves();
        var boards = Boards();
        var res = new Dictionary<(string, string, string), Agg>();
        foreach (var (n, f) in boards) foreach (var v in HuntVers) foreach (var w in waves) res[(n, v.Key, w.Key)] = Many(With(f, v.D), w.Make, seeds, verbose: w.Key == "2");
        Console.WriteLine($"# 第303期 段1-A —— 粛を狩る（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("版: " + string.Join(" ／ ", HuntVers.Select(v => $"{v.Key} {v.Name}")) + "。括弧は倒しT。S から ±10pt 以上動いたセルは太字。");
        Console.WriteLine();
        Console.WriteLine("## 勝率");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", waves.Length)));
        foreach (var (n, _) in boards)
            foreach (var v in HuntVers)
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", waves.Select(w =>
                {
                    var a = res[(n, v.Key, w.Key)];
                    return v != VS && Math.Abs(a.Win - res[(n, "S", w.Key)].Win) >= 10 ? $"**{Cell(a)}**" : Cell(a);
                })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 第2波（粛）の中身");
        Console.WriteLine();
        Console.WriteLine("粛が倒れたT ＝ 勝ち戦 ／ 負け戦（括弧は戦の数）・身振り ＝ 1戦の回数（付けた層）・受けた攻撃 ＝ 粛が倒れるまでに受けた攻撃（仇巡り ／ 一斉射撃 ／ そのほか）・戻った ＝ 粛が倒れた後の割り込み（仇討ち ／ 羽 ／ 指差し ／ 叫び ／ 庇い・1戦）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率（倒しT） | 粛が倒れた戦 | 粛が倒れたT | 倒した駒 | 身振り | 受けた攻撃 | 戻った | ヒサが倒れたT |");
        Console.WriteLine("|---|---|---|--:|---|---|---|---|---|---|");
        foreach (var (n, _) in boards)
            foreach (var v in HuntVers)
            {
                var a = res[(n, v.Key, "2")];
                Console.WriteLine($"| {n} | {v.Key} | {Cell(a)} | {Pct(a.HushDeadN, a.N)} | {Avg(a.HushDeadWinT, a.HushDeadWinN)}（{a.HushDeadWinN}）／ {Avg(a.HushDeadLoseT, a.HushDeadLoseN)}（{a.HushDeadLoseN}） | {Dict(a.Killer, a)} | {a.P(a.Gestures):F2}（{a.P(a.GestureLayers):F2}） | {a.P(a.HushHitRound):F2} ／ {a.P(a.HushHitVolley):F2} ／ {a.P(a.HushHitOther):F2} | {a.P(a.AfterVend):F2} ／ {a.P(a.AfterFeather):F2} ／ {a.P(a.AfterAccuse):F2} ／ {a.P(a.AfterRally):F2} ／ {a.P(a.AfterCover):F2} | {Avg(a.HisaDeadT, a.HisaDeadN)}（{Pct(a.HisaDeadN, a.N)}） |");
            }
    }

    static void Wave2(int seeds)
    {
        var w = WaveOf("2");
        Console.WriteLine($"# 第303期 標軸の行 × 第2波（粛）（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("勝率（倒しT）／ 粛が倒れた戦 と、粛が止めた動作（1戦・粛が単独の原因）: 指 ／ 叫 ／ 仇 ／ 庇 ／ 羽。");
        Console.WriteLine();
        Console.WriteLine("| 行 | P302 | S | Q1 | Q3 | QA | S で止まった（指 ／ 叫 ／ 仇 ／ 庇 ／ 羽） | QA で止まった（同） |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (var (n, f) in MarkRows())
        {
            var a = new[] { VP, VS, VQ1, VQ3, VQA }.Select(v => Many(With(f, v.D), w.Make, seeds)).ToArray();
            string Stop(Agg x) => $"{x.P(x.HushAccuse):F2} ／ {x.P(x.HushRally):F2} ／ {x.P(x.HushAvenge):F2} ／ {x.P(x.HushCover):F2} ／ {x.P(x.HushFeather):F2}";
            Console.WriteLine($"| {n} | " + string.Join(" | ", a.Select(x => $"{Cell(x)} ／ {Pct(x.HushDeadN, x.N)}")) + $" | {Stop(a[1])} | {Stop(a[4])} |");
        }
    }

    static void CoverVersions(int seeds)
    {
        var waves = AllWaves();
        var boards = Boards();
        var vers = new[] { VS, VCd, VCs };
        Console.WriteLine($"# 第303期 段1-B —— 庇いの版 × 代表台（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " | 庇った（全波の和・1戦） |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", waves.Length)) + "--:|");
        foreach (var (n, f) in boards)
        {
            var s0 = waves.Select(w => Many(With(f, VS.D), w.Make, seeds)).ToArray();
            foreach (var v in vers)
            {
                var a = v == VS ? s0 : waves.Select(w => Many(With(f, v.D), w.Make, seeds)).ToArray();
                long cov = a.Sum(x => x.CoverFires);
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", a.Select((x, i) => v != VS && Math.Abs(x.Win - s0[i].Win) >= 10 ? $"**{Cell(x)}**" : Cell(x))) + $" | {(double)cov / seeds:F2} |");
            }
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
            Parallel.For(0, seeds, s => wins[s] = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.Materialize(EnemyCatalog.Stages[ii].Enemy, BattleContext.EnemyTeam), s, verbose: false).PlayerWon);
            w[i] = 100.0 * wins.Count(x => x) / seeds;
        }
        return w;
    }

    static void Cmp(int seeds)
    {
        var rows = CompareBuilds();
        var withHisa = HisaRows();
        var vers = new[] { VP, VS, VQ1, VQ3, VQA, VCd, VCs };
        var all = new Dictionary<(string, string), double[]>();
        foreach (var v in vers) foreach (var (n, f) in withHisa) all[(n, v.Key)] = Rates(With(f, v.D), seeds);
        Console.WriteLine($"# 第303期 `compare` のヒサ在席の行 × 版（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"ヒサのいない {rows.Length - withHisa.Length} 行は版で動かない。S は P302 と、版は S と比べ、−10.0pt 以上落ちたセルを太字にする。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        string Base(string k) => k switch { "P302" => "P302", "S" => "P302", _ => "S" };
        foreach (var (n, _) in withHisa)
            foreach (var v in vers)
            {
                var a = all[(n, v.Key)]; var z = all[(n, Base(v.Key))];
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", a.Select((x, i) => v != VP && x - z[i] <= -10.0 ? $"**{x:F1}**" : $"{x:F1}")) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("## (G2)");
        Console.WriteLine();
        bool any = false;
        foreach (var v in vers.Where(v => v != VP))
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
    // 台本の例
    // ---------------------------------------------------------------------------------
    static void Find(int seeds)
    {
        Console.WriteLine($"# 第303期 台本の例（seed 0..{seeds - 1}・第2波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 台 | 身振り（1戦） | 粛が倒れた戦 | 「身振り → 仇巡り ／ 一斉射撃 → 粛が倒れる → 声が戻る」が揃った最初の seed（倒れたT） |");
        Console.WriteLine("|---|---|--:|--:|---|");
        foreach (var v in new[] { VQ1, VQ3, VQA })
            foreach (var (n, f) in Boards().Where(b => b.F.Occupied().Any(o => o.Def.Id == "zan")))
            {
                long g = 0, dead = 0; int first = -1, ft = 0;
                for (int s = 0; s < seeds; s++)
                {
                    var p = BattleEngine.Materialize(With(f, v.D), BattleContext.PlayerTeam);
                    var e = WaveOf("2").Make();
                    var r = BattleEngine.Run(p, e, s, verbose: true);
                    var ev = r.Events.ToList();
                    var h = e.First(u => u.Def.Traits.Contains(TraitId.Hush));
                    g += ev.Count(x => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Gesture);
                    if (h.LastDeathTurn > 0) dead++;
                    if (first >= 0) continue;
                    int gi = ev.FindIndex(x => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Gesture);
                    int di = ev.FindIndex(x => x.Kind == BattleEventKind.Death && x.TargetId == h.InstanceId);
                    if (gi < 0 || di < gi) continue;
                    bool hit = ev.Skip(gi).Take(di - gi).Any(x => (x.Kind == BattleEventKind.VendettaRound && x.Text == VendettaRoundLabels.Slash && x.TargetId == h.InstanceId)
                                                              || (x.Kind == BattleEventKind.Feather && x.Text == FeatherLabels.Volley));
                    bool back = ev.Skip(di).Any(x => (x.Kind == BattleEventKind.Damage && x.Reaction) || x.Kind == BattleEventKind.FeatherMark || x.Kind == BattleEventKind.MarkRally);
                    if (hit && back) { first = s; ft = ev[di].Turn; }
                }
                Console.WriteLine($"| {v.Key} | {n} | {(double)g / seeds:F2} | {100.0 * dead / seeds:F0}% | {(first < 0 ? "—" : $"seed {first}（T{ft}）")} |");
            }
    }

    static void Memo(string rowPart, string wave, int seed, string ver, int lastTurn)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(rowPart, StringComparison.Ordinal));
        var v = AllVers.First(b => b.Key == ver);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(With(f0, v.D), BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# hush303 memo —— {name} × {w.Name} × seed {seed} × {v.Key} {v.Name} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Turn > lastTurn) break;
            if (x.Kind is not (BattleEventKind.Damage or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.Feather or BattleEventKind.MarkRally
                or BattleEventKind.Death or BattleEventKind.Attack or BattleEventKind.VendettaRound or BattleEventKind.Command or BattleEventKind.Cover
                or BattleEventKind.Sealed or BattleEventKind.MarkLayer or BattleEventKind.StatusGain)) continue;
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
    static readonly MethodInfo Drain = typeof(BattleContext).GetMethod("DrainFeatherMarksPublic", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("DrainFeatherMarksPublic");
    static readonly MethodInfo SelectTarget = typeof(BattleContext).GetMethod("SelectTargetCore", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("SelectTargetCore");
    static readonly UnitDef HushDummy = new() { Id = "hushtest", Name = "粛の試験体", MaxHp = 99, Attack = 1, Speed = 1, Traits = new[] { TraitId.Hush } };
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
        Console.WriteLine("# hush303 check —— 第303期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var hisa = UnitCatalog.Hisa; var old = UnitCatalog.HisaH302;
        Expect("(a) 定義: 規定のヒサ ＝ `HisaH302` ＋ `RallyQuiet`（＝ 第302期の HV-s と同じ札）・旧は `All` ／ `Everyone` の外・数値と手番は旧のまま・マイナスの末尾に「粛の下では声が出ない」",
            hisa.Traits.SequenceEqual(old.Traits.Append(TraitId.RallyQuiet)) && hisa.Traits.SequenceEqual(UnitCatalog.HisaHVs.Traits) && hisa.PlusText == old.PlusText
            && hisa.MinusText.StartsWith(old.MinusText, StringComparison.Ordinal) && hisa.MinusText.Contains("粛の下では声が出ない") && hisa.MaxHp == old.MaxHp && hisa.Speed == old.Speed
            && UnitCatalog.All.Contains(hisa) && !UnitCatalog.Everyone.Contains(old) && UnitCatalog.HisaHK0.MinusText == old.MinusText && UnitCatalog.HisaH301.MinusText == old.MinusText);
        bool order(UnitDef d, TraitId g) { var l = d.Traits.ToList(); return l.IndexOf(g) >= 0 && l.IndexOf(g) + 1 == l.IndexOf(TraitId.CommandBall) && l.Count == hisa.Traits.Count + 1; }
        Expect("(b) 版: Q1 ／ Q3 ／ QA は身振りの札を号令の直前に挟んだだけ・HC-d ／ HC-s は末尾に札・空の庇いは `HisaCover` を差し替え・どれも `All` ／ `Everyone` の外",
            order(UnitCatalog.HisaQ1, TraitId.HushGesture1) && order(UnitCatalog.HisaQ3, TraitId.HushGesture3) && order(UnitCatalog.HisaQA, TraitId.HushGestureFocus)
            && UnitCatalog.HisaHCd.Traits.SequenceEqual(hisa.Traits.Append(TraitId.CoverSkipShoulder)) && UnitCatalog.HisaHCs.Traits.SequenceEqual(hisa.Traits.Append(TraitId.CoverSkipLethal))
            && UnitCatalog.HisaHCp.Traits.SequenceEqual(hisa.Traits.Select(t => t == TraitId.HisaCover ? TraitId.CoverPlacebo : t))
            && !new[] { UnitCatalog.HisaQ1, UnitCatalog.HisaQ3, UnitCatalog.HisaQA, UnitCatalog.HisaHCd, UnitCatalog.HisaHCs, UnitCatalog.HisaHCp }.Any(UnitCatalog.Everyone.Contains));

        // (c) 段0: 粛で叫びが出ない ／ 粛の保持者が倒れた後は出る
        (long Rally, long Hushed) Rally(UnitDef h, bool hush, bool dead)
        {
            var en = hush ? Formation.Build(front1: UnitCatalog.Gald, back3: HushDummy) : Formation.Build(front1: UnitCatalog.Gald);
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Borg, back3: h), en, out var p, out var e);
            var borg = p.First(u => u.Def.Id == UnitCatalog.Borg.Id);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 900; }
            if (dead) ctx.ApplyDamage(e.First(u => u.Def.Id == "hushtest"), 5000, borg, pattern: AttackPattern.Single);
            var g = e.First(u => u.Def.Id == UnitCatalog.Gald.Id);
            g.SetCounter(StatusKeys.Marked, 1);
            ctx.Reaction(() => ctx.ApplyDamage(g, 1, borg, pattern: AttackPattern.Single));
            return (Tal(ctx, "hisa").RallyFires, Tal(ctx, "hisa").RallyHushed);
        }
        var r0 = Rally(hisa, false, false); var r1 = Rally(hisa, true, false); var r2 = Rally(hisa, true, true); var r3 = Rally(old, true, false);
        Expect("(c) 段0: 規定のヒサは粛（保持者が生きている）で叫ばない・保持者が倒れた後は叫ぶ・旧 `HisaH302` は粛でも叫ぶ",
            r0.Rally == 1 && r1.Rally == 0 && r1.Hushed == 1 && r2.Rally == 1 && r3.Rally == 1, $"なし {r0.Rally}・粛 {r1.Rally}（黙る {r1.Hushed}）・粛の後 {r2.Rally}・旧 {r3.Rally}");

        // (d) 身振り: 粛の保持者が生きている間だけ・ヒサの手番（OnAction）・粛の下でも出る・層の数・羽は粛の下で飛ばない
        (int Layer, long Fires, long MfFoe, long MfHushed) Gesture(UnitDef h, bool hush, bool dead, bool misa = true)
        {
            var en = hush ? Formation.Build(front1: UnitCatalog.Gald, center: HushDummy) : Formation.Build(front1: UnitCatalog.Gald, center: UnitCatalog.Gald);
            var ctx = Ctx(misa ? Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Tome, back3: h) : Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Dolga, back3: h), en, out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa");
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            var target = e.First(u => u.Slot == 2);
            if (dead) ctx.ApplyDamage(target, 5000, p[0], pattern: AttackPattern.Single);
            foreach (var t in hi.Traits) t.OnAction(ctx, hi, hi.CurrentAction ?? new UnitAction(ActionKind.Skill));
            Drain.Invoke(ctx, null);
            return (target.RawCounter(StatusKeys.Marked), Tal(ctx, "hisa").GestureFires, Tal(ctx, "tome").MfShotsFoe, Tal(ctx, "tome").MfHushed);
        }
        var g1 = Gesture(UnitCatalog.HisaQ1, true, false); var g3 = Gesture(UnitCatalog.HisaQ3, true, false); var gs = Gesture(hisa, true, false);
        var gn = Gesture(UnitCatalog.HisaQ3, false, false); var gd = Gesture(UnitCatalog.HisaQ3, true, true); var gm = Gesture(UnitCatalog.HisaQ3, true, false, misa: false);
        Expect("(d) 身振り: 粛の保持者に Q1 1 層 ／ Q3 3 層（手番の動作で・粛の下でも出る）・規定は出ない・粛がいない ／ 倒れた後は出ない・ミサなしは標1つ・標撃ちの羽は粛の下で飛ばない",
            g1.Layer == 1 && g1.Fires == 1 && g3.Layer == 3 && gs.Layer == 0 && gs.Fires == 0 && gn.Fires == 0 && gd.Fires == 0 && gm.Layer == 1 && g3.MfFoe == 0 && g3.MfHushed > 0,
            $"Q1 {g1.Layer}・Q3 {g3.Layer}・規定 {gs.Fires}・粛なし {gn.Fires}・倒れた後 {gd.Fires}・ミサなし {gm.Layer}・羽 撃った {g3.MfFoe} ／ 粛で止まった {g3.MfHushed}");
        {
            // 痺れで手番が潰れたら出ない（台で: 痺れたヒサの手番に身振りが出ないこと＝出来事と痺れの順で数える）
            long bad = 0, n = 0;
            foreach (var (_, f) in Boards()) for (int s = 0; s < 10; s++)
                {
                    var p = BattleEngine.Materialize(With(f, UnitCatalog.HisaQ3), BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, WaveOf("2").Make(), s, verbose: true);
                    var hid = p.First(u => u.Def.Id == "hisa").InstanceId;
                    foreach (var x in r.Events.Where(x => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Gesture)) { n++; if (x.ActorId != hid) bad++; }
                    // 1ターンに2回出たら手番の外で出ている
                    bad += r.Events.Where(x => x.Kind == BattleEventKind.Framed && x.Text == FramedLabels.Gesture).GroupBy(x => x.Turn).Count(g => g.Count() > 1);
                }
            Expect("(d2) 身振りはヒサの手番だけ（Q3 × 代表台 × 第2波 × seed 0..9 で1ターンに1回まで）", n > 0 && bad == 0, $"身振り {n}・外れ {bad}");
        }

        // (e) QA: 仇巡り ／ 一斉射撃（炸裂の段）／ ソラの標が、層の深い敵より粛の保持者を先に選ぶ
        {
            UnitState? Pick(UnitDef h, string who)
            {
                var ctx = Ctx(Formation.Build(front1: UnitCatalog.Zan, front3: UnitCatalog.Sora, center: UnitCatalog.Tome, back3: h), Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gald, center: HushDummy), out var p, out var e);
                foreach (var u in p.Concat(e)) { u.MaxHp = 5000; u.Hp = 5000; }
                var deep = e.First(u => u.Slot == 0); var herald = e.First(u => u.Def.Id == "hushtest");
                deep.SetCounter(StatusKeys.Marked, 5); herald.SetCounter(StatusKeys.Marked, 1);
                if (who == "misa")
                {
                    var misa = p.First(u => u.Def.Id == "tome");
                    var args = new object?[] { misa, AttackPattern.Single, -1 };
                    return (UnitState?)SelectTarget.Invoke(ctx, args);
                }
                if (who == "sora")
                {
                    var sora = p.First(u => u.Def.Id == "sora");
                    herald.SetCounter(StatusKeys.Marked, 0); deep.Hp = 5000; herald.Hp = 10;
                    TraitCatalog.Get(TraitId.Divert).OnTurnStart(ctx, sora);
                    int id = sora.RawCounter(DeflectTrait.TargetKey) - 1;
                    return e.FirstOrDefault(u => u.InstanceId == id);
                }
                return null;
            }
            UnitState? zq = null, zs = null;
            {
                // ザンは台本から読む（1太刀目）
                foreach (var (d, set) in new (UnitDef, Action<UnitState?>)[] { (UnitCatalog.HisaQA, x => zq = x), (UnitCatalog.HisaQ3, x => zs = x) })
                {
                    var ctx = Ctx(Formation.Build(front1: UnitCatalog.Zan, center: UnitCatalog.Tome, back3: d), Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gald, center: HushDummy), out var p, out var e);
                    foreach (var u in p.Concat(e)) { u.MaxHp = 5000; u.Hp = 5000; }
                    var deep = e.First(u => u.Slot == 0); var herald = e.First(u => u.Def.Id == "hushtest");
                    deep.SetCounter(StatusKeys.Marked, 5); herald.SetCounter(StatusKeys.Marked, 1);
                    var zan = p.First(u => u.Def.Id == "zan");
                    ctx.TakeTurn(zan);
                    var slash = ctx.Events.FirstOrDefault(x => x.Kind == BattleEventKind.VendettaRound && x.Text == VendettaRoundLabels.Slash);
                    set(slash is null ? null : e.FirstOrDefault(u => u.InstanceId == slash.TargetId));
                }
            }
            var mq = Pick(UnitCatalog.HisaQA, "misa"); var ms = Pick(UnitCatalog.HisaQ3, "misa");
            var sq = Pick(UnitCatalog.HisaQA, "sora"); var ss = Pick(UnitCatalog.HisaQ3, "sora");
            Expect("(e) QA: 仇巡りの1太刀目 ／ ミサの炸裂の段 ／ ソラの手番の頭の標が、層の深い敵（HP の高い敵）より粛の保持者を選ぶ・Q3 は今までどおり",
                zq?.Def.Id == "hushtest" && zs?.Def.Id != "hushtest" && mq?.Def.Id == "hushtest" && ms?.Def.Id != "hushtest" && sq?.Def.Id == "hushtest" && ss?.Def.Id != "hushtest",
                $"仇巡り QA {zq?.Def.Id} ／ Q3 {zs?.Def.Id}・ミサ QA {mq?.Def.Id} ／ Q3 {ms?.Def.Id}・ソラ QA {sq?.Def.Id} ／ Q3 {ss?.Def.Id}");
        }

        // (f) HC-d ／ HC-s ／ 空の庇い
        (bool Covered, long SkipS, long SkipL, long Placebo, int AllyHp) Cov(UnitDef h, UnitDef ally, int hisaHp)
        {
            var ctx = Ctx(Formation.Build(front1: ally, front3: UnitCatalog.Borg, back3: h), Formation.Build(front1: UnitCatalog.Gald), out var p, out var e);
            var hi = p.First(u => u.Def.Id == "hisa"); var a = p.First(u => u.Def.Id == ally.Id);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            a.Hp = 20; hi.Hp = hisaHp;
            ctx.ApplyDamage(a, 40, e[0], pattern: AttackPattern.Single);
            var t = Tal(ctx, "hisa");
            return (t.CoverFires > 0, t.CoverSkipShoulder, t.CoverSkipLethal, t.CoverPlacebos, a.Hp);
        }
        var d1 = Cov(UnitCatalog.HisaHCd, UnitCatalog.Doha, 100); var d2 = Cov(UnitCatalog.HisaHCd, UnitCatalog.Dolga, 100); var d0 = Cov(hisa, UnitCatalog.Doha, 100);
        var s1 = Cov(UnitCatalog.HisaHCs, UnitCatalog.Dolga, 30); var s2 = Cov(UnitCatalog.HisaHCs, UnitCatalog.Dolga, 100); var p1 = Cov(UnitCatalog.HisaHCp, UnitCatalog.Dolga, 100);
        Expect("(f) HC-d: 分かちのドハは庇わない・ドルガは庇う（規定はドハも庇う）／ HC-s: 40 の一撃をヒサ HP 30 では庇わない・100 なら庇う ／ 空の庇い: 判定だけ通して一撃は元の相手が受ける",
            !d1.Covered && d1.SkipS == 1 && d2.Covered && d0.Covered && !s1.Covered && s1.SkipL == 1 && s2.Covered && !p1.Covered && p1.Placebo == 1 && p1.AllyHp <= 0,
            $"HC-d ドハ {d1.Covered} ／ ドルガ {d2.Covered}（規定 ドハ {d0.Covered}）・HC-s HP30 {s1.Covered} ／ HP100 {s2.Covered}・空 {p1.Placebo}（味方 HP {p1.AllyHp}）");

        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(g) `PickOne(` の出現数が第302期と同じ（BattleCore）", pick == 36, $"{pick}");
        {
            int diff = 0, n2 = 0;
            foreach (var d in new[] { hisa, UnitCatalog.HisaQ1, UnitCatalog.HisaQ3, UnitCatalog.HisaQA, UnitCatalog.HisaHCd, UnitCatalog.HisaHCs, UnitCatalog.HisaHCp })
                foreach (var (_, f) in Boards()) foreach (var w in new[] { "2", "guard", "bat", "boss" }.Select(WaveOf)) for (int s = 0; s < 5; s++)
                        {
                            var g = With(f, d);
                            var a = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                            var b = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                            n2++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                        }
            Expect("(h) verbose の有無で勝敗・決着T が同じ（規定と版 6 つ × 代表台 × 第2波 ／ 精鋭 ／ ボス × seed 0..4）", diff == 0, $"{n2} 戦・違い {diff}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
