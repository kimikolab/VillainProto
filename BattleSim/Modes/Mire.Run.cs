using BattleCore;
using static Common;

// =====================================================================================
// mire run（第218期）—— 表A〜F。指示書 §6。
//
// 版は**ミオの札の差し替えだけ**で作る（`Ver`）。**保持者は版の中だけ**（`UnitCatalog.Mio` は M0 のまま）。
// M台1〜M台4 の席は M4 × 倍率 150 × 第2〜5波 × seed 1000..1049 で総当たりし（勝ち数最大・同値は列挙順で最初）、**全版に同じ席**を使う。
// M台3・M台4 はポンの席（第216期の台A・台B）も並べる。M台5 は `compare` の元の席のまま。
// 測る: 第2〜5波 × seed 0..199（verbose: false）× 倍率 115 と 150。
// =====================================================================================

static partial class MireDiag
{
    const int MeasSeeds = 200, PickSeed0 = 1000, PickSeeds = 50;

    static UnitDef Ver(params TraitId[] extra)
    {
        UnitDef d = UnitCatalog.Mio;
        return new()
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
            Pattern = d.Pattern, Actions = d.Actions, Traits = new[] { TraitId.Concentrate, TraitId.ConcentrateLeak }.Concat(extra).ToArray(),
            PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
        };
    }

    /// <summary>版（指示書 §3）。<b>初めて読んだときに1度だけ作る</b>（静的初期化子にしない・R277）。</summary>
    internal static (string Tag, UnitDef Def)[] Versions => _versions ??= new[]
    {
        ("M0", Ver()),
        ("M1", Ver(TraitId.MireSlam)),
        ("M2", Ver(TraitId.MireSlam, TraitId.MireConduct)),
        ("M3", Ver(TraitId.MireSlam, TraitId.MireConduct, TraitId.MireDull)),
        ("M3x", Ver(TraitId.MireSlam, TraitId.MireConduct, TraitId.MireDullAll)),
        ("M4", Ver(TraitId.MireSlam, TraitId.MireConduct, TraitId.MireDull, TraitId.MireCarry)),
        ("M5", Ver(TraitId.MireSlam, TraitId.MireConduct, TraitId.MireDull, TraitId.MireCarry, TraitId.MireHandoff)),
    };
    static (string Tag, UnitDef Def)[]? _versions;

    internal static UnitDef VerOf(string tag) => Versions.First(v => v.Tag == tag).Def;

    // =================================================================================
    // 席の総当たり（M台1〜M台4・M4・倍率 150）
    // =================================================================================

    internal sealed record Seats(string Tag, FormationShape Shape, Formation Best, int Top, int Ties);

    static List<Seats>? _seats;

    internal static List<Seats> SeatSearch()
    {
        if (_seats is not null) return _seats;
        var list = new List<Seats>();
        var boss = new BossRule(false) { Scale = ShockDiag.Scale150 };
        foreach (var (tag, _, mem) in Rigs)
            foreach (FormationShape sh in ShockDiag.Shapes)
            {
                var members = mem(VerOf("M4"));
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
                list.Add(new(tag, sh, all[best], score[best], score.Count(x => x == score[best])));
            }
        return _seats = list;
    }

    /// <summary>測る台（M台1〜M台4 × 陣形 ＋ ポンの席 ＋ M台5 の行）。ミオは M0 の駒で入れておき、版で差し替える。</summary>
    internal static List<(string Name, Formation F, bool Rig5)> Benches()
    {
        var b = new List<(string, Formation, bool)>();
        foreach (var s in SeatSearch()) b.Add((s.Tag + " " + ShapeName(s.Shape), WithMio(s.Best, UnitCatalog.Mio), false));
        b.Add(("M台3 ポンX", PonX(UnitCatalog.Mio), false));
        b.Add(("M台4 ポンP2", PonP2(UnitCatalog.Mio), false));
        foreach (var (name, f) in M5Rows()) b.Add(("M台5 " + name.Split(' ')[0], f, true));
        return b;
    }

    // =================================================================================
    // 集計
    // =================================================================================

    internal sealed class MAgg
    {
        public readonly ShockDiag.Agg216 A = new();
        public readonly UnitTally M = new(), K = new();
        public long Battles => A.Battles;
        public double Per(long x) => A.Per(x);

        public void Take(BattleResult r, int st, HashSet<string> playerIds, HashSet<string> frontIds)
        {
            A.Take(r, st, playerIds, frontIds);
            if (r.TallyByUnit.TryGetValue("mio", out var m)) M.Add(m);
            if (r.TallyByUnit.TryGetValue("kata", out var k)) K.Add(k);
        }

        public void Merge(MAgg o) { A.Merge(o.A); M.Add(o.M); K.Add(o.K); }
    }

    internal static MAgg Measure(Formation f, EnemyScaleRule scale, int seed0 = 0, int seeds = MeasSeeds)
    {
        var playerIds = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        int[] fronts = f.Shape == FormationShape.X ? new[] { 0, 1 } : new[] { 3 };
        var frontIds = fronts.Select(i => f[i]?.Id).Where(x => x is not null).Select(x => x!).ToHashSet();
        var boss = new BossRule(false) { Scale = scale };
        var total = new MAgg();
        var gate = new object();
        Parallel.For(0, 4 * seeds, () => new MAgg(), (j, _, local) =>
        {
            int st = 1 + j / seeds, s = seed0 + j % seeds;
            local.Take(BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss), st, playerIds, frontIds);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    static string D1(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;±0.0");

    static string DeathLine(MAgg a, Formation f)
        => string.Join(" ／ ", f.Occupied().Select(o =>
        {
            var (fell, ts) = a.A.Fallen.GetValueOrDefault(o.Def.Id);
            return Short(o.Def) + " " + (100.0 * fell / Math.Max(1, a.Battles)).ToString("F0") + "%" + (fell > 0 ? "@" + ((double)ts / fell).ToString("F1") : "");
        }));

    /// <summary>第2波の味方の被ダメ（敵が出どころ）を1ターン目とそれ以外に分ける（台本・倍率 115・seed 0..199）。P4 用。</summary>
    static (double T1, double All) Wave2AllyTaken(Formation f)
    {
        long t1 = 0, all = 0;
        var gate = new object();
        Parallel.For(0, MeasSeeds, s =>
        {
            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var en = BattleEngine.Materialize(EnemyCatalog.Stages[1].Enemy, BattleContext.EnemyTeam, EnemyScaleRule.Adopted);
            BattleResult r = BattleEngine.Run(pl, en, s, verbose: true);
            var ally = pl.Select(u => u.InstanceId).ToHashSet();
            var foe = en.Select(u => u.InstanceId).ToHashSet();
            long a1 = 0, aa = 0;
            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind != BattleEventKind.Damage || e.TargetId is not int t || !ally.Contains(t) || e.ActorId is not int a || !foe.Contains(a)) continue;
                aa += e.Amount;
                if (e.Turn == 1) a1 += e.Amount;
            }
            lock (gate) { t1 += a1; all += aa; }
        });
        return ((double)t1 / MeasSeeds, (double)all / MeasSeeds);
    }

    // =================================================================================
    // run
    // =================================================================================

    static partial void RunImpl(string arg)
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第218期 `mire run` —— 表A〜F（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("版: " + string.Join(" ／ ", Versions.Select(v => v.Tag + " = [" + string.Join(", ", v.Def.Traits) + "]")));
        Console.WriteLine();
        Console.WriteLine("## 台と席（M4 × 倍率 150 × 第2〜5波 × seed " + PickSeed0 + ".." + (PickSeed0 + PickSeeds - 1) + "・勝ち数最大・同値は列挙順で最初・120 通り）");
        Console.WriteLine();
        foreach (var s in SeatSearch())
            Console.WriteLine("- " + s.Tag + " × " + ShapeName(s.Shape) + ": " + SeatsNamed(s.Best) + "（勝ち " + s.Top + " / 200・同値 " + s.Ties + " 通り）");
        Console.WriteLine("- ポンの席: X字 " + SeatsNamed(PonX(UnitCatalog.Mio)) + " ／ P2 " + SeatsNamed(PonP2(UnitCatalog.Mio)));
        foreach (var (name, f) in M5Rows()) Console.WriteLine("- M台5 `" + name + "`: " + SeatsNamed(f) + "（元の席）");
        Console.WriteLine();
        Console.WriteLine("測る: 第2〜5波 × seed 0.." + (MeasSeeds - 1) + "（verbose: false）。倍率は敵の最大HPと攻撃力（`EnemyScaleRule`）。");
        Console.WriteLine();

        var benches = Benches();
        var res = new Dictionary<(int B, string V, string Sc), MAgg>();
        for (int b = 0; b < benches.Count; b++)
            foreach (var (v, def) in Versions)
                foreach (var (sc, rule) in ShockDiag.Scales216)
                    res[(b, v, sc)] = Measure(WithMio(benches[b].F, def), rule);
        string[] vs = Versions.Select(v => v.Tag).ToArray();

        // ---- 表A ----
        Console.WriteLine("## 表A. 勝率・全員生存・決着ターン・倒れた駒");
        Console.WriteLine();
        Console.WriteLine("### 表A'. 平均（第2〜5波）と差");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | " + string.Join(" | ", vs) + " | M1−M0 | M2−M1 | M3−M2 | M3x−M3 | M4−M3 | M5−M4 | M4−M0 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", vs.Length + 7)));
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
            {
                double M(string v) => res[(b, v, sc)].A.Mean25;
                Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + string.Join(" | ", vs.Select(v => F1(M(v)))) + " | "
                                  + D1(M("M1") - M("M0")) + " | " + D1(M("M2") - M("M1")) + " | " + D1(M("M3") - M("M2")) + " | " + D1(M("M3x") - M("M3")) + " | "
                                  + D1(M("M4") - M("M3")) + " | " + D1(M("M5") - M("M4")) + " | " + D1(M("M4") - M("M0")) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("### 表A''. 全員生存（第2〜5波・勝った戦のうち出撃した駒が1枚も倒れなかった割合 ÷ 全戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | " + string.Join(" | ", vs) + " | M4−M0 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", vs.Length + 1)));
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
            {
                double S(string v) => res[(b, v, sc)].A.AllSurvPct;
                Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + string.Join(" | ", vs.Select(v => F1(S(v)))) + " | " + D1(S("M4") - S("M0")) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("### 表A（全部）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | **平均** | 全員生存 | 決着T | 倒れた割合@倒れたT（最初の1回） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|---|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, def) in Versions)
                {
                    MAgg a = res[(b, v, sc)];
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + string.Join(" | ", new[] { 1, 2, 3, 4 }.Select(w => F1(a.A.WinPct(w)))) + " | **"
                                      + F1(a.A.Mean25) + "** | " + F1(a.A.AllSurvPct) + " | " + F2(a.A.MeanWinT) + " | " + DeathLine(a, WithMio(benches[b].F, def)) + " |");
                }
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B. ミオの帳簿（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("手番 ＝ 濃縮の手番。叩きつけ（うち感電の相手）。弾いた ＝ その一撃で感電が弾けた（叩きつけ／通電）。通電 ＝ 中心の外へ走った回数・届いた数（中心を含む平均）・"
                          + "敵の中央（X字）が空いてから走った割合。連鎖 ＝ ミオの一撃が起こした連鎖の回数／延べの駒数。撃破 ＝ ミオの `Kills`。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 手番 | 叩きつけ | うち感電の相手 | 弾いた 叩き／通電 | 通電 | 届いた数 | 中央が空いてから % | 連鎖 回／延べ | 叩きつけの与ダメ | 撃破 | ミオが倒れた % |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|--:|--:|--:|---|--:|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, _) in Versions)
                {
                    if (v == "M0") continue;
                    MAgg a = res[(b, v, sc)];
                    UnitTally m = a.M;
                    var (fell, _) = a.A.Fallen.GetValueOrDefault("mio");
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + F2(a.Per(m.ConcFires)) + " | " + F2(a.Per(m.MireSlams)) + " | " + F2(a.Per(m.MireSlamOnShocked)) + " | "
                                      + F2(a.Per(m.MireSlamPops)) + "／" + F2(a.Per(m.MireConductPops)) + " | " + F2(a.Per(m.MireConducts)) + " | "
                                      + (m.MireConducts == 0 ? "—" : F2(1.0 + (double)m.MireConductHits / m.MireConducts)) + " | " + P1(m.MireConductAfterCenter, m.MireConducts) + " | "
                                      + F2(a.Per(m.ShockTriggered)) + "／" + F2(a.Per(m.ShockTriggeredUnits)) + " | " + F1(a.Per(m.MireSlamDealt)) + " | " + F2(a.Per(m.Kills)) + " | "
                                      + F1(100.0 * fell / Math.Max(1, a.Battles)) + " |");
                }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. 痺れの時機（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("ミオが弾かせて痺れた敵のうち、**そのターンの手番をまだ終えていなかった**敵（＝動く前に止めた）。比べる相手は、同じ戦で**だれの一撃であれ**感電で痺れた敵の同じ割合。"
                          + "味方 ＝ ミオの一撃の連鎖で痺れた味方（連鎖は弾けた駒と同じ陣営の隣へしか走らないので 0 のはず）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | ミオで痺れた敵 | うち動く前 % | ミオで痺れた味方 | 敵が感電で痺れた（全部） | うち動く前 % | 敵が感電の痺れで失った手番 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, _) in Versions)
                {
                    MAgg a = res[(b, v, sc)];
                    UnitTally m = a.M, e = a.A.E;
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + F2(a.Per(m.MireStunned)) + " | " + P1(m.MireStunnedEarly, m.MireStunned) + " | " + F2(a.Per(m.MireStunnedAlly)) + " | "
                                      + F2(a.Per(e.ShockStunned)) + " | " + P1(e.ShockStunnedEarly, e.ShockStunned) + " | " + F2(a.Per(e.StallShockStun)) + " |");
                }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. 澱みのデバフ（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("軽くした量 ＝ 印のある駒の与ダメから引いた量（出どころの側で数える）。敵の内訳は 攻撃／放電（**敵どうしの放電** ＝ こちらの損）。味方の内訳は 攻撃／雷／放電／叩きつけ（M3x だけ）。"
                          + "味方の被ダメ ＝ 味方の `DamageTaken` の合計。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 敵: 軽くした 攻撃 | 敵: 敵どうしの放電 | 味方: 軽くした 攻撃／雷／放電／叩き | 味方の被ダメ | M2 比 |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, _) in Versions)
                {
                    if (v is "M0" or "M1") continue;
                    MAgg a = res[(b, v, sc)];
                    long[] er = a.A.E.MireDulledByRoute ?? new long[4], pr = a.A.P.MireDulledByRoute ?? new long[4];
                    double taken = a.Per(a.A.P.DamageTaken), taken2 = res[(b, "M2", sc)].Per(res[(b, "M2", sc)].A.P.DamageTaken);
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + F1(a.Per(er[0])) + " | " + F1(a.Per(er[2])) + " | "
                                      + string.Join("／", pr.Select(x => F1(a.Per(x)))) + " | " + F1(taken) + " | " + D1(100.0 * (taken - taken2) / Math.Max(1e-9, taken2)) + "% |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表D'. 第2波の味方の被ダメ（敵が出どころ・台本・倍率 115・seed 0..199）——1ターン目とそれ以外（P4）");
        Console.WriteLine();
        Console.WriteLine("| 台 | M2 1ターン目 | M3 1ターン目 | 差 | M2 全体 | M3 全体 | 差 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
        {
            var (a1, aa) = Wave2AllyTaken(WithMio(benches[b].F, VerOf("M2")));
            var (c1, ca) = Wave2AllyTaken(WithMio(benches[b].F, VerOf("M3")));
            Console.WriteLine("| " + benches[b].Name + " | " + F1(a1) + " | " + F1(c1) + " | " + D1(100.0 * (c1 - a1) / Math.Max(1e-9, a1)) + "% | " + F1(aa) + " | " + F1(ca) + " | " + D1(100.0 * (ca - aa) / Math.Max(1e-9, aa)) + "% |");
        }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E. 印（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("置いた ＝ 中心＋周り（敵）。漏れ ＝ ミオの隣の味方。運んだ ＝ 放電で運んだ（うち味方へ）。移った ＝ 倒れた敵から移した印（回数）。消えた ＝ 倒れた駒が持っていた印（移した分は含まない）。"
                          + "一から ＝ 中心の印が 0 から付いた手番の割合。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 置いた | 漏れ | 運んだ（味方へ） | 移った（回） | 移せず消えた | 消えた 敵／味方 | 一から % |");
        Console.WriteLine("|---|---|---|--:|--:|---|---|--:|---|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, _) in Versions)
                {
                    MAgg a = res[(b, v, sc)];
                    UnitTally m = a.M;
                    long placed = m.ConcMarkCenter + m.ConcMarkAround, acts = m.ConcFires - m.ConcDry;
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + F2(a.Per(placed)) + " | " + F2(a.Per(m.ConcMarkAlly)) + " | "
                                      + F2(a.Per(m.MireCarried)) + "（" + F2(a.Per(m.MireCarriedAlly)) + "） | " + F2(a.Per(m.MireHandedOff)) + "（" + F2(a.Per(m.MireHandoffs)) + "） | "
                                      + F2(a.Per(m.MireHandoffLost)) + " | " + F2(a.Per(a.A.E.ConcMarksAtDeath - m.MireHandedOff)) + "／" + F2(a.Per(a.A.P.ConcMarksAtDeath)) + " | " + P1(m.ConcCenterFresh, acts) + " |");
                }
        Console.WriteLine();

        // ---- 表F ----
        Console.WriteLine("## 表F. 印による追加の刻み（名目・毒と燃焼・1戦あたり・ターン別）");
        Console.WriteLine();
        Console.WriteLine("敵が受けた追加の刻み（印の2回目以降）をターン別に（全戦で割る——短い戦は後ろのターンを持たない）。味方 ＝ 味方が受けた追加の刻み（全ターン）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | T1 | T2 | T3 | T4 | T5 | T6+ | 敵 計 | 味方 計 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var (sc, _) in ShockDiag.Scales216)
                foreach (var (v, _) in Versions)
                {
                    MAgg a = res[(b, v, sc)];
                    long[] h = a.A.E.ConcExtraByTurn ?? new long[8];
                    long[] hp = a.A.P.ConcExtraByTurn ?? new long[8];
                    string Cell(int t) => F1(a.Per(t < 6 ? h[t] : h.Skip(6).Sum()));
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + v + " | " + string.Join(" | ", Enumerable.Range(1, 6).Select(Cell)) + " | "
                                      + F1(a.Per(h.Sum())) + " | " + F1(a.Per(hp.Sum())) + " |");
                }
        Console.WriteLine();
        Console.WriteLine("所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }
}
