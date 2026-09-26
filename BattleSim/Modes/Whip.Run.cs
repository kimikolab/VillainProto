using BattleCore;
using static Common;

// =====================================================================================
// whip run / check（第217期）—— 表A〜E と自己検査。指示書 §6・§7。
//
// 版は**シガの札の差し替えだけ**で作る（`Ver`）。**保持者は版の中だけ**（`UnitCatalog.Shiga` は G0 のまま）。
// W1〜W3 の席は G3 × 倍率 115 × 第2〜5波 × seed 1000..1049 で総当たりし（勝ち数最大・同値は列挙順で最初）、**全版に同じ席**を使う。
// W4 は `compare` の元の席のまま。測る: 第2〜5波 × seed 0..199（verbose: false）。
// =====================================================================================

static partial class WhipDiag
{
    const int MeasSeeds = 200, PickSeed0 = 1000, PickSeeds = 50;

    static UnitDef Ver(params TraitId[] traits)
    {
        UnitDef d = UnitCatalog.Shiga;
        return new()
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
            Pattern = d.Pattern, Actions = d.Actions, Traits = traits,
            PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
        };
    }

    /// <summary>版（指示書 §3 ＋ 参考 G3K）。<b>初めて読んだときに1度だけ作る</b>（静的初期化子にしない・R277）。</summary>
    internal static (string Tag, UnitDef Def)[] Versions => _versions ??= new[]
    {
        ("G0", UnitCatalog.Shiga),
        ("G1", Ver(TraitId.Scourge, TraitId.Shame)),
        ("G2", Ver(TraitId.Scourge, TraitId.Shame, TraitId.Lash)),
        ("G3", Ver(TraitId.Scourge, TraitId.Shame, TraitId.Lash, TraitId.LiveWire)),
        ("G3H", Ver(TraitId.Scourge, TraitId.Shame, TraitId.Lash, TraitId.LiveWire, TraitId.LiveWireGuard)),
        ("G3K", Ver(TraitId.Scourge, TraitId.Shame, TraitId.Lash, TraitId.LiveWire, TraitId.ScourgeShock)),
    };
    static (string Tag, UnitDef Def)[]? _versions;

    static UnitDef VerOf(string tag) => Versions.First(v => v.Tag == tag).Def;

    static string ShapeName(FormationShape s) => s == FormationShape.X ? "X字" : "P2";

    static bool IsFront(Formation f, int slot) => f.Shape == FormationShape.X ? slot is 0 or 1 : slot == 3;

    // =================================================================================
    // 席の総当たり（W1〜W3・G3・倍率 115）
    // =================================================================================

    internal sealed record Seats(string Tag, FormationShape Shape, Formation Best, List<(Formation F, int Score)> All);

    static readonly Dictionary<int, List<Seats>> _seats = new();

    /// <summary>
    /// 席の総当たり。<b>指示書の席は倍率 115</b>（<paramref name="scalePct"/> = 115）。115 では6つとも 200 / 200 の天井で同値が 11〜76 通りあり、
    /// 「同値は列挙順で最初」が席を決めてしまうので、<b>参考に倍率 150 で選び直した席</b>（<paramref name="scalePct"/> = 150）も並べる（第217期の判断）。
    /// </summary>
    internal static List<Seats> SeatSearch(int scalePct = 115)
    {
        if (_seats.TryGetValue(scalePct, out var cached)) return cached;
        var list = new List<Seats>();
        var boss = new BossRule(false) { Scale = scalePct == 115 ? ShockDiag.Scale115 : ShockDiag.Scale150 };
        foreach (var (tag, _, mem) in Rigs)
            foreach (FormationShape sh in ShockDiag.Shapes)
            {
                var members = mem(VerOf("G3"));
                var all = new List<Formation>();
                foreach (int[] assign in SlotAssignments(members.Count))
                {
                    var f = new Formation { Shape = sh };
                    for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
                    all.Add(f);
                }
                var score = new int[all.Count];
                Parallel.For(0, all.Count * 4, j =>
                {
                    int i = j / 4, st = 1 + j % 4;
                    Formation en = EnemyCatalog.Stages[st].Enemy;
                    int w = 0;
                    for (int s = PickSeed0; s < PickSeed0 + PickSeeds; s++)
                        if (BattleEngine.Run(all[i], en, s, verbose: false, boss: boss).PlayerWon) w++;
                    Interlocked.Add(ref score[i], w);
                });
                int best = 0;
                for (int i = 1; i < all.Count; i++) if (score[i] > score[best]) best = i;
                list.Add(new(tag, sh, all[best], all.Select((f, i) => (f, score[i])).ToList()));
            }
        return _seats[scalePct] = list;
    }

    static int ShigaSlot(Formation f) => f.Occupied().First(o => o.Def.Id == "shiga").Slot;

    static Formation WithVer(Formation f, string tag)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "shiga" ? VerOf(tag) : d;
        return g;
    }

    static string SeatsNamed(Formation f) => string.Join(" ／ ", f.Occupied().Select(o => f.Shape.FrameNames[o.Slot] + ":" + Short(o.Def)));

    // =================================================================================
    // 集計
    // =================================================================================

    internal sealed class WAgg
    {
        public readonly ShockDiag.Agg216 A = new();
        public readonly UnitTally S = new(), K = new();
        public long RunMaxSum, Run2Battles, RunMax, AllyShockLeft;
        public long Battles => A.Battles;

        public void Take(BattleResult r, int st, HashSet<string> playerIds, HashSet<string> frontIds)
        {
            A.Take(r, st, playerIds, frontIds);
            long m = 0;
            foreach (var (id, t) in r.TallyByUnit)
            {
                if (playerIds.Contains(id))
                {
                    AllyShockLeft += t.ShockLeftAlive;
                    if (id == "shiga") S.Add(t);
                    if (id == "kata") K.Add(t);
                    continue;
                }
                if (t.StallRunMax > m) m = t.StallRunMax;
            }
            RunMaxSum += m;
            if (m >= 2) Run2Battles++;
            if (m > RunMax) RunMax = m;
        }

        public void Merge(WAgg o)
        {
            A.Merge(o.A); S.Add(o.S); K.Add(o.K);
            RunMaxSum += o.RunMaxSum; Run2Battles += o.Run2Battles; AllyShockLeft += o.AllyShockLeft;
            if (o.RunMax > RunMax) RunMax = o.RunMax;
        }

        public double Per(long x) => A.Per(x);
    }

    static WAgg Measure(Formation f, EnemyScaleRule scale, int seed0 = 0, int seeds = MeasSeeds)
    {
        var playerIds = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        int[] fronts = f.Shape == FormationShape.X ? new[] { 0, 1 } : new[] { 3 };
        var frontIds = fronts.Select(i => f[i]?.Id).Where(x => x is not null).Select(x => x!).ToHashSet();
        var boss = new BossRule(false) { Scale = scale };
        var total = new WAgg();
        var gate = new object();
        Parallel.For(0, 4 * seeds, () => new WAgg(), (j, _, local) =>
        {
            int st = 1 + j / seeds, s = seed0 + j % seeds;
            local.Take(BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss), st, playerIds, frontIds);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    static double MeanHist(long[]? h)
    {
        if (h is null) return double.NaN;
        long n = h.Sum();
        return n == 0 ? double.NaN : (double)h.Select((c, i) => c * i).Sum() / n;
    }

    static string DeathLine(WAgg a, Formation f)
        => string.Join(" ／ ", f.Occupied().Select(o =>
        {
            var (fell, ts) = a.A.Fallen.GetValueOrDefault(o.Def.Id);
            return Short(o.Def) + " " + (100.0 * fell / Math.Max(1, a.Battles)).ToString("F0") + "%" + (fell > 0 ? "@" + ((double)ts / fell).ToString("F1") : "");
        }));

    // =================================================================================
    // run
    // =================================================================================

    static partial void RunImpl(string arg)
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第217期 `whip run` —— 表A〜E（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("版: " + string.Join(" ／ ", Versions.Select(v => v.Tag + " = [" + string.Join(", ", v.Def.Traits) + "]")) + "（G3K は参考・指示書に無い）");
        Console.WriteLine();
        var seats = SeatSearch(115);
        var seats150 = SeatSearch(150);
        Console.WriteLine("## 台と席（G3 × 倍率 115（指示書）／ 150（参考）× 第2〜5波 × seed " + PickSeed0 + ".." + (PickSeed0 + PickSeeds - 1) + "・勝ち数最大・同値は列挙順で最初・120 通り）");
        Console.WriteLine();
        foreach (var s in seats.Concat(seats150))
        {
            int top = s.All.Max(x => x.Score), ties = s.All.Count(x => x.Score == top);
            var tieSeats = s.All.Where(x => x.Score == top).Select(x => s.Shape.FrameNames[ShigaSlot(x.F)]).GroupBy(x => x).Select(g => g.Key + "×" + g.Count());
            Console.WriteLine("- " + s.Tag + " × " + ShapeName(s.Shape) + (seats150.Contains(s) ? "（席150）" : "（席115）") + ": " + SeatsNamed(s.Best) + "（勝ち " + top + " / 200・同値 " + ties + " 通り——同値の中のシガの席: " + string.Join("・", tieSeats) + "）");
        }
        foreach (var (name, f) in W4Rows()) Console.WriteLine("- W4 `" + name + "`: " + SeatsNamed(f) + "（元の席）");
        Console.WriteLine();
        Console.WriteLine("測る: 第2〜5波 × seed 0.." + (MeasSeeds - 1) + "（verbose: false）。倍率は敵の最大HPと攻撃力（`EnemyScaleRule`）。");
        Console.WriteLine();

        // ---- 測る ----
        var benches = new List<(string Name, Formation F)>();
        foreach (var s in seats) benches.Add((s.Tag + " " + ShapeName(s.Shape), s.Best));
        foreach (var s in seats150) benches.Add((s.Tag + " " + ShapeName(s.Shape) + " 席150", s.Best));
        foreach (var (name, f) in W4Rows()) benches.Add(("W4 " + name.Split(' ')[0], f));
        var res = new Dictionary<(int B, string V, string Sc), WAgg>();
        for (int b = 0; b < benches.Count; b++)
            foreach (var (v, _) in Versions)
                foreach (var (sc, rule) in ShockDiag.Scales216)
                    res[(b, v, sc)] = Measure(WithVer(benches[b].F, v), rule);

        // ---- 表A ----
        Console.WriteLine("## 表A. 勝率・全員生存・決着ターン・倒れた駒");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | **平均** | 全員生存 | 決着T | 倒れた割合@倒れたT（最初の1回） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|---|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, _) in Versions)
                {
                    WAgg a = res[(b, v, sc)];
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + string.Join(" | ", new[] { 1, 2, 3, 4 }.Select(w => F1(a.A.WinPct(w)))) + " | **"
                                      + F1(a.A.Mean25) + "** | " + F1(a.A.AllSurvPct) + " | " + F2(a.A.MeanWinT) + " | " + DeathLine(a, WithVer(benches[b].F, v)) + " |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表A'. 平均だけ（G0 との差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | " + string.Join(" | ", Versions.Select(v => v.Tag)) + " | G1−G0 | G2−G1 | G3−G2 | G3H−G3 | G3K−G3 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", Versions.Length + 5)));
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
            {
                double M(string v) => res[(b, v, sc)].A.Mean25;
                Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + string.Join(" | ", Versions.Select(v => F1(M(v.Tag)))) + " | "
                                  + D1(M("G1") - M("G0")) + " | " + D1(M("G2") - M("G1")) + " | " + D1(M("G3") - M("G2")) + " | " + D1(M("G3H") - M("G3")) + " | " + D1(M("G3K") - M("G3")) + " |");
            }
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B. シガの帳簿（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("怖気づいた手番 ＝ シガが感電以外の痺れで失った手番（どの版でも同じ物差し）。1振りで弾かせた ＝ シガの一撃が起こした連鎖の延べ ÷ 振り（G0 は振り＝`Attacks`）。"
                          + "2倍 ＝ 2倍で入った延べ（G0 は追い打ち ＝ 見せしめの判定が通った回数）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 与ダメ | 振り | 当たり/振り | 2倍（追い打ち） | うち感電で2倍 | 悲鳴 | うち巻き込みから | 怖気づいた手番 | 感電で怖気づかず | 電気鞭 | 移した感電 | 弾かせた連鎖 回／延べ | 1振りで弾かせた |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, _) in Versions)
                {
                    WAgg a = res[(b, v, sc)];
                    UnitTally s = a.S;
                    bool g0 = v == "G0";
                    long swings = g0 ? s.Attacks : s.WhipSwings;
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + F1(a.Per(s.DamageToEnemy)) + " | " + F2(a.Per(swings)) + " | "
                                      + (g0 ? "1.00" : F2((double)s.WhipHits / Math.Max(1, s.WhipSwings))) + " | " + F2(a.Per(g0 ? s.ShameFires : s.WhipDoubled)) + " | "
                                      + F2(a.Per(s.WhipDoubledShock)) + " | " + F2(a.Per(s.ShameFires)) + " | " + F2(a.Per(s.WhipScreamSplash)) + " | "
                                      + F2(a.Per(s.StallStun - s.StallShockStun)) + " | " + F2(a.Per(s.WhipWiredSpared)) + " | " + F2(a.Per(s.WiredSwings)) + " | " + F2(a.Per(s.WiredMarked)) + " | "
                                      + F2(a.Per(s.ShockTriggered)) + "／" + F2(a.Per(s.ShockTriggeredUnits)) + " | " + F2((double)s.ShockTriggeredUnits / Math.Max(1, swings)) + " |");
                }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. シガ自身の感電（1戦あたり）と味方に残った感電");
        Console.WriteLine();
        Console.WriteLine("もらった ＝ 新しく感電した回数（ほぼカタの代金）。使い切った ＝ 電気鞭。弾けた ＝ シガの感電が起爆した回数（敵に殴られたか隣の放電）。"
                          + "痺れた ＝ そのうち感電で痺れて失った手番。味方に残った ＝ 決着時に生きている味方に残っていた感電。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | もらった | 使い切った | 弾けた | 感電の痺れで失った手番 | カタが味方に付けた | 味方に残った | 味方全体が感電の痺れで失った手番 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
        {
            if (benches[b].Name.StartsWith("W4")) continue;
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, _) in Versions)
                {
                    WAgg a = res[(b, v, sc)];
                    UnitTally s = a.S;
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + F2(a.Per(s.ShockReceived)) + " | " + F2(a.Per(s.WiredSwings)) + " | "
                                      + F2(a.Per(s.ShockSpent)) + " | " + F2(a.Per(s.StallShockStun)) + " | " + F2(a.Per(a.K.ShockOnAlly)) + " | " + F2(a.Per(a.AllyShockLeft)) + " | "
                                      + F2(a.Per(a.A.P.StallShockStun)) + " |");
                }
        }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. 敵の手番（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("失った手番の出どころ: 感電 ＝ 感電の痺れ ／ 竦み ＝ 見せしめの悲鳴 ／ ほか痺れ ＝ トウの粉など感電以外の痺れ ／ 組み付き ＝ クグ。"
                          + "続けて失った ＝ 同じ敵が自分の手番を続けて失った数（戦ごとの最大を平均）。2以上 ＝ そういう敵が1体でもいた戦の割合。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 失った手番 計 | 感電 | 竦み | ほか痺れ | 組み付き | ハメ防止で痺れず | 続けて失った（戦ごとの最大の平均） | 2以上の戦 | 最大 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, _) in Versions)
                {
                    WAgg a = res[(b, v, sc)];
                    UnitTally e = a.A.E;
                    long shock = e.StallShockStun, cow = e.StallCowed, other = e.StallStun - e.StallShockStun, grap = e.StallGrappled;
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + F2(a.Per(shock + cow + other + grap)) + " | " + F2(a.Per(shock)) + " | " + F2(a.Per(cow)) + " | "
                                      + F2(a.Per(other)) + " | " + F2(a.Per(grap)) + " | " + F2(a.Per(e.ShockStunGuarded)) + " | " + F2((double)a.RunMaxSum / Math.Max(1, a.Battles)) + " | "
                                      + F1(100.0 * a.Run2Battles / Math.Max(1, a.Battles)) + "% | " + a.RunMax + " |");
                }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E. 席（W1〜W3・G3）");
        Console.WriteLine();
        Console.WriteLine("シガの席ごとに、その席でいちばん勝った並び（**倍率 150 の総当たりの点**——115 は天井で同値ばかりなので）を G3 で測り直す。前列 ＝ X字の前1・前3／P2 の前衛。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | シガの席 | 前列 | 選んだ点 | 115 平均 | 150 平均 | 150 全員生存 | シガの感電 もらった／弾けた／痺れで失った手番（150） | シガが倒れた（150） | 並び |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|---|--:|---|");
        foreach (var s in seats150)
        {
            foreach (int frame in new[] { 0, 1, 2, 3, 4 })
            {
                var cand = s.All.Where(x => ShigaSlot(x.F) == frame).OrderByDescending(x => x.Score).First();
                WAgg a115 = Measure(cand.F, ShockDiag.Scale115), a150 = Measure(cand.F, ShockDiag.Scale150);
                var (fell, _) = a150.A.Fallen.GetValueOrDefault("shiga");
                bool chosen = ReferenceEquals(cand.F, s.Best);
                bool chosen115 = ShigaSlot(seats.First(x => x.Tag == s.Tag && x.Shape == s.Shape).Best) == frame;
                Console.WriteLine("| " + s.Tag + " | " + ShapeName(s.Shape) + " | " + s.Shape.FrameNames[frame] + (chosen ? " **（席150）**" : "") + (chosen115 ? " **（席115）**" : "") + " | " + (IsFront(cand.F, frame) ? "前列" : "—") + " | "
                                  + cand.Score + " | " + F1(a115.A.Mean25) + " | " + F1(a150.A.Mean25) + " | " + F1(a150.A.AllSurvPct) + " | "
                                  + F2(a150.Per(a150.S.ShockReceived)) + "／" + F2(a150.Per(a150.S.ShockSpent)) + "／" + F2(a150.Per(a150.S.StallShockStun)) + " | "
                                  + F1(100.0 * fell / Math.Max(1, a150.Battles)) + "% | " + SeatsNamed(cand.F) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }

    static string D1(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;±0.0");
}

static partial class WhipDiag
{
    /// <summary>1戦のログ（`whip log "<台 陣形> <版> <波 0始まり> <seed> [倍率]"`・例 `W1 X字 G3 2 0 150`）。</summary>
    static partial void LogImpl(string arg)
    {
        string[] a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string rig = a.Length > 0 ? a[0] : "W1", shape = a.Length > 1 ? a[1] : "X字", v = a.Length > 2 ? a[2] : "G3";
        int st = a.Length > 3 ? int.Parse(a[3]) : 2, seed = a.Length > 4 ? int.Parse(a[4]) : 0;
        EnemyScaleRule sc = a.Length > 5 && a[5] == "150" ? ShockDiag.Scale150 : ShockDiag.Scale115;
        var s = SeatSearch().First(x => x.Tag == rig && ShapeName(x.Shape) == shape);
        Formation f = WithVer(s.Best, v);
        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: true, boss: new BossRule(false) { Scale = sc });
        Console.WriteLine("# " + rig + " " + shape + " " + v + " 第" + (st + 1) + "波 seed " + seed + " —— " + SeatsNamed(f) + " → " + (r.PlayerWon ? "勝ち" : "負け") + " " + r.Turns + "T");
        foreach (LogLine l in r.Log) Console.WriteLine(l.ToString());
    }
}
