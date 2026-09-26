using BattleCore;
using static Common;

// =====================================================================================
// shock run215 / check215（第215期）—— 雷の跳ね先の同点（T0 席番号 ／ T1 行き止まりを先に）。
// 指示書は design/PHASE215_THUNDER_PATH_SPEC.md ／ 報告は design/PHASE215_THUNDER_PATH.md。
// 台と席は第214期のまま（`Tables()`・席は T0 で選ぶ）。
// =====================================================================================

static partial class ShockDiag
{
    internal static readonly UnitDef KataT1 = WithTraits(UnitCatalog.Kata, TraitId.Thunder, TraitId.ThunderLeak, TraitId.ThunderPath);

    /// <summary>
    /// 版の並び。<b>静的フィールドにしない</b>——`KataT0` は別のファイル（`Shock.Run.cs`）の静的フィールドで、
    /// 部分クラスをまたぐ静的初期化の順は決まっていないので、フィールドにすると null を掴む（R277 の第196期の形を最初の実行で踏んだ）。
    /// </summary>
    static (string Tag, UnitDef Def)[] PathVersions => new[] { ("T0", KataT0), ("T1", KataT1), ("T1′", KataT1Hop) };

    /// <summary>T1′（参考）: その先の鍵を跳ねにだけ使う。最初の一発は T0 と同じ。</summary>
    internal static readonly UnitDef KataT1Hop = WithTraits(UnitCatalog.Kata, TraitId.Thunder, TraitId.ThunderLeak, TraitId.ThunderPathHop);

    /// <summary>表E' の1セル（雷が跳ねた回のうち、全員に当たった／1体だけ取り残した・取り残した席）。</summary>
    /// <summary>
    /// 表E'' の分母を広げた版（第215期）: 帯びた敵から始まった雷（帯びた敵がいなくて1発だけの回を除く）すべてについて、
    /// 全員に当たった ／ 1体だけ取り残した ／ 2体以上取り残した ／ 1発で止まった（生きている敵が2体以上いたのに）。
    /// </summary>
    sealed class Wide { public long All, Full, One, More, Single, SingleNoCenter; }

    static (long Casts, long Full, long MissOne, Dictionary<string, long> Seats) MissTable(Formation f, Formation enF)
        => MissTableWide(f, enF, null);

    static (long Casts, long Full, long MissOne, Dictionary<string, long> Seats) MissTableWide(Formation f, Formation enF, Wide? wide)
    {
        long castsN = 0, full = 0, missOne = 0;
        var missed = new Dictionary<string, long>();
        var gate = new object();
        Parallel.For(0, MeasSeeds, s =>
        {
            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var en = BattleEngine.Materialize(enF, BattleContext.EnemyTeam);
            BattleResult r = BattleEngine.Run(pl, en, s, verbose: true);
            var byId = en.ToDictionary(u => u.InstanceId);
            var dead = new HashSet<int>();
            // 取り残した敵がその時点で状態異常を帯びていたか（ターン頭の写し ＋ その後に付いた分・感電は弾けたら外す）
            var carry = new Dictionary<int, HashSet<string>>();
            var countedLabels = ThunderTrait.CountedKeys.Select(StatusKeys.LabelOf).ToHashSet();
            long c = 0, fu = 0, mo = 0;
            var ms = new Dictionary<string, long>();
            HashSet<int>? hit = null, aliveAtStart = null;
            int maxHitKinds = 0;
            bool fromStatus = false;
            long wAll = 0, wFull = 0, wOne = 0, wMore = 0, wSingle = 0, wSingleNoC = 0;
            void Close()
            {
                if (hit is not null && aliveAtStart is not null && fromStatus)
                {
                    int left = aliveAtStart.Count(x => !hit.Contains(x));
                    wAll++;
                    if (left == 0) wFull++; else if (left == 1) wOne++; else wMore++;
                    if (hit.Count == 1 && aliveAtStart.Count >= 2)
                    {
                        wSingle++;
                        // 中央（X 字の要）が生きていなかった回（敵が X 字のときだけ意味がある）
                        if (!aliveAtStart.Any(x => byId[x].Slot == 2)) wSingleNoC++;
                    }
                }
                if (hit is null || aliveAtStart is null || hit.Count < 2) { hit = null; return; }
                c++;
                var miss = aliveAtStart.Where(x => !hit.Contains(x)).ToList();
                if (miss.Count == 0) fu++;
                else if (miss.Count == 1)
                {
                    mo++;
                    UnitState m = byId[miss[0]];
                    bool bare = !carry.TryGetValue(m.InstanceId, out var cs) || !cs.Overlaps(countedLabels);
                    int mk = cs is null ? 0 : cs.Count(countedLabels.Contains);
                    bool noFront = !aliveAtStart.Any(x => FormationRules.RowOf(byId[x].Slot) == Row.Front);
                    string seat = m.Shape.SeatName(m.Slot) + (bare ? "（帯びていない）" : noFront ? "（前列が全滅・中央から）" : mk < maxHitKinds ? "（種類で負けた）" : "（種類は同じか多い）");
                    ms[seat] = ms.GetValueOrDefault(seat) + 1;
                }
                hit = null;
            }
            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind == BattleEventKind.Death && e.TargetId is int d) dead.Add(d);
                if (e.Kind == BattleEventKind.TurnStart) carry.Clear();
                if (e.Kind == BattleEventKind.StatusSnapshot && e.TargetId is int sn && e.Text is string lb)
                    (carry.TryGetValue(sn, out var set) ? set : carry[sn] = new HashSet<string>()).Add(lb);
                if (e.Kind == BattleEventKind.StatusGain && e.TargetId is int sg && e.Text is string key)
                    (carry.TryGetValue(sg, out var set2) ? set2 : carry[sg] = new HashSet<string>()).Add(StatusKeys.LabelOf(key));
                if (e.Kind == BattleEventKind.ShockSpent && e.TargetId is int sp && carry.TryGetValue(sp, out var set3)) set3.Remove(StatusKeys.LabelOf(StatusKeys.Shock));
                if (e.Kind == BattleEventKind.Thunder)
                {
                    if (e.Slot == 1)
                    {
                        Close();
                        hit = new HashSet<int>();
                        maxHitKinds = 0;
                        fromStatus = (e.StatusRemaining ?? 0) > 0;
                        aliveAtStart = byId.Keys.Where(x => !dead.Contains(x)).ToHashSet();
                    }
                    if (e.TargetId is int t && hit is not null) { hit.Add(t); if (e.Slot > 1 && (e.StatusRemaining ?? 0) > maxHitKinds) maxHitKinds = e.StatusRemaining ?? 0; }
                }
                else if (e.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart) Close();
            }
            Close();
            lock (gate)
            {
                castsN += c; full += fu; missOne += mo;
                if (wide is not null) { wide.All += wAll; wide.Full += wFull; wide.One += wOne; wide.More += wMore; wide.Single += wSingle; wide.SingleNoCenter += wSingleNoC; }
                foreach (var (k, v) in ms) missed[k] = missed.GetValueOrDefault(k) + v;
            }
        });
        return (castsN, full, missOne, missed);
    }

    static void Run215()
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第215期 `shock run215` —— 雷の跳ね先の同点（T0 席番号 ／ T1 行き止まりを先に）");
        Console.WriteLine();
        if (PathVersions.Any(v => v.Def is null)) throw new InvalidOperationException("版の駒が null（静的初期化の順）");
        var tables = Tables();
        Console.WriteLine("台と席は第214期のまま（席は T0＝第214期の K1 で選んである）。版はどちらも K1（代金あり・刻みでは起爆しない）。");
        Console.WriteLine();

        var res = new Dictionary<(int T, FormationShape S, string V), Agg>();
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
                foreach (var (tag, def) in PathVersions)
                    res[(i, sh, tag)] = Measure(WithKata(tables[i].Seats[sh], def), Waves25);

        Console.WriteLine("## 表A. 勝率（第2〜5波 × seed 0..199）・全員生存勝ち・決着T");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | **平均** | 全員生存 | 決着T |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
                foreach (var (v, _) in PathVersions)
                {
                    Agg a = res[(i, sh, v)];
                    Console.WriteLine("| " + tables[i].Tag + " | " + ShapeName(sh) + " | " + v + " | "
                                      + string.Join(" | ", Waves25.Select(st => F1(a.WinPct(st)))) + " | **" + F1(a.Mean25) + "** | "
                                      + F1(a.AllSurvPct) + " | " + F2(a.MeanWinT) + " |");
                }
        Console.WriteLine();

        Console.WriteLine("## 表B. 雷（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 版 | 雷/戦 | 1回で当たった数の平均 | その分布 | 1発の種類の平均 | 与ダメ/戦 | 雷で倒した/戦 |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|--:|--:|");
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
                foreach (var (v, _) in PathVersions)
                {
                    Agg a = res[(i, sh, v)];
                    UnitTally k = a.P;
                    Console.WriteLine("| " + tables[i].Tag + " | " + ShapeName(sh) + " | " + v + " | " + F2(Per(k.ThunderCasts, a)) + " | "
                                      + F2((double)k.ThunderHits / Math.Max(1, k.ThunderCasts)) + " | " + Hist(k.ThunderPerCastHist, 1, 5) + " | "
                                      + F2(MeanHist(k.ThunderKindsHist)) + " | " + F1(Per(k.ThunderDealt, a)) + " | " + F2(Per(k.ThunderKills, a)) + " |");
                }
        Console.WriteLine();

        Console.WriteLine("## 表C. 敵の連鎖（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 版 | 付いた（敵） | 連鎖（敵） | 起爆（敵） | 連鎖の大きさ 平均・分布 | 放電で削った（敵） | 放電で倒れた（敵） | 味方への放電の HP |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|--:|--:|--:|");
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
                foreach (var (v, _) in PathVersions)
                {
                    Agg a = res[(i, sh, v)];
                    UnitTally p = a.P, e = a.E;
                    Console.WriteLine("| " + tables[i].Tag + " | " + ShapeName(sh) + " | " + v + " | " + F2(Per(p.ShockOnFoe, a)) + " | "
                                      + F2(Per(e.ChainRoots, a)) + " | " + F2(Per(e.ShockSpent, a)) + " | "
                                      + F2((double)e.ChainUnits / Math.Max(1, e.ChainRoots)) + " ・ " + Hist(e.ChainSizeHist, 1, 5) + " | "
                                      + F1(Per(e.DischargeTaken, a)) + " | " + F2(Per(e.DischargeDeaths, a)) + " | " + F1(Per(p.DischargeTaken, a)) + " |");
                }
        Console.WriteLine();

        Console.WriteLine("## 表E'. 雷が跳ねた回のうち「全員に当たった／1体だけ取り残した」（味方 X字の席・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 敵の陣形 | 版 | 雷（跳ねた回） | 全員に当たった | 1体だけ取り残した | 取り残した席 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|");
        foreach (int i in new[] { 0, 2 })
        {
            Formation f = tables[i].Seats[FormationShape.X];
            foreach (int st in new[] { 2, 4 })
                foreach (bool p3 in new[] { false, true })
                    foreach (var (v, def) in PathVersions)
                    {
                        Formation enF = p3 ? EnemyCatalog.Pattern3Of(st)! : EnemyCatalog.Stages[st].Enemy;
                        var (c, full, one, seats) = MissTable(WithKata(f, def), enF);
                        Console.WriteLine("| " + tables[i].Tag + " | " + (st + 1) + " | " + (p3 ? "パターン3" : "X字") + " | " + v + " | " + c + " | "
                                          + F1(100.0 * full / Math.Max(1, c)) + "% | **" + F1(100.0 * one / Math.Max(1, c)) + "%** | "
                                          + string.Join(" ／ ", seats.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + " " + kv.Value)) + " |");
                    }
        }
        Console.WriteLine();

        Console.WriteLine("## 表E''. 分母を広げた版——帯びた敵から始まった雷すべて（1発で止まった回も入れる・規約 (G12)）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 敵の陣形 | 版 | 雷 | 全員に当たった | 1体取り残した | 2体以上取り残した | 1発で止まった（生存2体以上） | うち中央（席2）が生きていない |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (int i in new[] { 0, 2 })
        {
            Formation f = tables[i].Seats[FormationShape.X];
            foreach (int st in new[] { 2, 4 })
                foreach (bool p3 in new[] { false, true })
                    foreach (var (v, def) in PathVersions)
                    {
                        Formation enF = p3 ? EnemyCatalog.Pattern3Of(st)! : EnemyCatalog.Stages[st].Enemy;
                        var w = new Wide();
                        MissTableWide(WithKata(f, def), enF, w);
                        double P(long x) => 100.0 * x / Math.Max(1, w.All);
                        Console.WriteLine("| " + tables[i].Tag + " | " + (st + 1) + " | " + (p3 ? "パターン3" : "X字") + " | " + v + " | " + w.All + " | "
                                          + F1(P(w.Full)) + "% | **" + F1(P(w.One)) + "%** | " + F1(P(w.More)) + "% | " + F1(P(w.Single)) + "% | " + (w.Single == 0 ? "—" : F1(100.0 * w.SingleNoCenter / w.Single) + "%") + " |");
                    }
        }
        Console.WriteLine();
        Console.WriteLine("所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }

    /// <summary>取り残した1回の前後を台本から出す（診断・`shock debug215`）。</summary>
    static void Debug215()
    {
        var tables = Tables();
        Formation f = WithKata(tables[2].Seats[FormationShape.X], KataT1);
        Formation enF = EnemyCatalog.Stages[4].Enemy;
        int shown = 0;
        for (int s = 0; s < MeasSeeds && shown < 3; s++)
        {
            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var en = BattleEngine.Materialize(enF, BattleContext.EnemyTeam);
            BattleResult r = BattleEngine.Run(pl, en, s, verbose: true);
            var all = pl.Concat(en).ToDictionary(u => u.InstanceId);
            var ev = r.Events;
            for (int i = 0; i < ev.Count && shown < 3; i++)
            {
                if (ev[i].Kind != BattleEventKind.Thunder || ev[i].Slot != 1) continue;
                var hits = new List<int>(); int j = i;
                for (; j < ev.Count; j++)
                {
                    if (ev[j].Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart) break;
                    if (ev[j].Kind == BattleEventKind.Thunder) { if (ev[j].Slot == 1 && j != i) break; hits.Add(ev[j].TargetId ?? -1); }
                }
                var deadBefore = ev.Take(i).Where(e => e.Kind == BattleEventKind.Death).Select(e => e.TargetId ?? -1).ToHashSet();
                var alive = en.Where(u => !deadBefore.Contains(u.InstanceId)).Select(u => u.InstanceId).ToList();
                var miss = alive.Where(x => !hits.Contains(x)).ToList();
                if (hits.Count < 2 || miss.Count != 1) continue;
                shown++;
                Console.WriteLine("## seed " + s + " T" + ev[i].Turn + " 取り残し: " + all[miss[0]].Name + "（" + all[miss[0]].Shape.SeatName(all[miss[0]].Slot) + "）");
                int ts = ev.Take(i).Select((e, k) => (e, k)).Last(x => x.e.Kind == BattleEventKind.TurnStart).k;
                for (int k = ts; k < j; k++)
                {
                    var e = ev[k];
                    if (e.Kind is BattleEventKind.StatSnapshot) continue;
                    string nm(int? id) => id is int x && all.TryGetValue(x, out var u) ? u.Name + "@" + u.Shape.SeatName(u.Slot) : "-";
                    Console.WriteLine("    " + e.Kind + " " + nm(e.ActorId) + " → " + nm(e.TargetId) + " amt=" + e.Amount + " slot=" + e.Slot + " rem=" + e.StatusRemaining + " txt=" + e.Text);
                }
            }
        }
    }

    // =================================================================================
    // check215
    // =================================================================================

    static void Check215()
    {
        Console.WriteLine("# 第215期 `shock check215` —— 自己検査");
        Console.WriteLine();
        int fails = 0;
        void Line(string name, bool ok, string detail)
        {
            if (!ok) fails++;
            Console.WriteLine("| " + name + " | " + detail + " | " + (ok ? "**○**" : "**×**") + " |");
        }
        Console.WriteLine("| 検査 | 中身 | 判定 |");
        Console.WriteLine("|---|---|---|");

        // ---- 2・3a: 実戦（verbose の有無・雷が同じ敵に2回当たらない） ----
        var tables = Tables();
        long runs = 0, mism = 0, casts = 0, dup = 0;
        var gate = new object();
        foreach (Table tb in tables)
            foreach (FormationShape sh in Shapes)
                foreach (var (_, def) in PathVersions)
                {
                    Formation f = WithKata(tb.Seats[sh], def);
                    Parallel.For(0, 4 * CheckSeeds, j =>
                    {
                        int st = 1 + j / CheckSeeds, s = j % CheckSeeds;
                        Formation en = EnemyCatalog.Stages[st].Enemy;
                        BattleResult a = BattleEngine.Run(f, en, s, verbose: false);
                        BattleResult b = BattleEngine.Run(f, en, s, verbose: true);
                        long c = 0, d = 0;
                        HashSet<int>? cast = null;
                        foreach (BattleEvent e in b.Events)
                        {
                            if (e.Kind != BattleEventKind.Thunder) continue;
                            if (e.Slot == 1) { c++; cast = new HashSet<int>(); }
                            if (cast is not null && e.TargetId is int t && !cast.Add(t)) d++;
                        }
                        lock (gate)
                        {
                            runs++; casts += c; dup += d;
                            if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns || a.PlayerSurvivors != b.PlayerSurvivors) mism++;
                        }
                    });
                }
        Line("2 verbose の有無", mism == 0, runs + " 戦（4台 × 2陣形 × T0/T1 × 第2〜5波 × seed 0.." + (CheckSeeds - 1) + "）で食い違い " + mism);
        Line("3a 雷が同じ敵に2回当たらない", dup == 0 && casts > 0, "雷 " + casts + " 回・重複 " + dup);

        // ---- 3b: 選び方そのもの（盤の外で駒を組んで Pick を直に呼ぶ） ----
        UnitState Mk(int slot, FormationShape shape)
        {
            var d = new UnitDef { Id = "d" + slot, Name = "d" + slot, MaxHp = 10, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>() };
            return new UnitState { Def = d, TeamId = BattleContext.EnemyTeam, Shape = shape, Slot = slot, Hp = 10, MaxHp = 10, Traits = Array.Empty<Trait>() };
        }
        var rng = new Random(215);
        string[] keys = ThunderTrait.CountedKeys.ToArray();
        long trials = 0, keyViol = 0, onwardViol = 0, t0Diff = 0;
        foreach (FormationShape shape in new[] { FormationShape.X, FormationShape.Spear })
            for (int n = 0; n < 5000; n++)
            {
                var slots = Enumerable.Range(0, 9).Where(s => rng.Next(100) < 70).ToList();
                if (slots.Count < 2) continue;
                var team = slots.Select(s => Mk(s, shape)).ToList();
                foreach (UnitState u in team)
                    for (int k = 0, m = rng.Next(4); k < m; k++) u.SetCounter(keys[rng.Next(keys.Length)], 1);
                var struck = team.Where(_ => rng.Next(100) < 25).ToHashSet();
                var cands = team.Where(u => !struck.Contains(u) && rng.Next(100) < 70).ToList();
                trials++;
                foreach (bool path in new[] { false, true })
                {
                    UnitState? p = ThunderTrait.Pick(cands, team, struck, path);
                    int maxK = cands.Select(ThunderTrait.KindsOf).DefaultIfEmpty(0).Max();
                    if (maxK == 0) { if (p is not null) keyViol++; continue; }
                    if (p is null || ThunderTrait.KindsOf(p) != maxK) { keyViol++; continue; }   // 第一の鍵
                    if (path)
                    {
                        var tied = cands.Where(u => ThunderTrait.KindsOf(u) == maxK).ToList();
                        int minOn = tied.Min(u => ThunderTrait.Onward(u, team, struck));
                        var best = tied.Where(u => ThunderTrait.Onward(u, team, struck) == minOn).OrderBy(u => u.Slot).First();
                        if (best != p) onwardViol++;
                    }
                    else if (p != cands.Where(u => ThunderTrait.KindsOf(u) == maxK).OrderBy(u => u.Slot).First()) t0Diff++;
                }
            }
        Line("3b 第一の鍵（種類の多い候補を後にしない）", keyViol == 0, trials + " 通り × 2版（盤の外で組んだ駒・X字とパターン3）で違反 " + keyViol);
        Line("3c T1 は種類が同じなら「その先が少ない → 席番号」", onwardViol == 0, "違反 " + onwardViol);
        Line("3d T0 は種類が同じなら席番号（第214期と同じ）", t0Diff == 0, "違反 " + t0Diff);

        // ---- 3e: X字・全員同じ種類の一筆（指示書 §2） ----
        string Walk(FormationShape shape, int[] slots, bool path, int start)
        {
            var team = slots.Select(s => Mk(s, shape)).ToList();
            foreach (UnitState u in team) u.SetCounter(StatusKeys.Poison, 1);
            var struck = new HashSet<UnitState>();
            var order = new List<string>();
            UnitState? cur = ThunderTrait.Pick(team.Where(u => FormationRules.RowOf(u.Slot) == Row.Front || u.Slot == start), team, struck, path);
            while (cur is not null)
            {
                struck.Add(cur);
                order.Add(shape.SeatName(cur.Slot));
                UnitState from = cur;
                cur = ThunderTrait.Pick(team.Where(u => !struck.Contains(u) && FormationRules.AreAdjacent(from, u)), team, struck, path);
            }
            return string.Join(" → ", order);
        }
        string xT0 = Walk(FormationShape.X, new[] { 0, 1, 2, 3, 4 }, false, -1), xT1 = Walk(FormationShape.X, new[] { 0, 1, 2, 3, 4 }, true, -1);
        Line("3e X字・全員同じ種類の一筆", xT1 == "前1 → 後1 → 中央 → 前3 → 後3", "T0: " + xT0 + " ／ T1: " + xT1);
        Console.WriteLine();
        Console.WriteLine(fails == 0 ? "SHOCK_CHECK215 ok=True" : "SHOCK_CHECK215 ok=False fails=" + fails);
    }
}
