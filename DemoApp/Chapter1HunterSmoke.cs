using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 第480期の門 `--chapter1-hunter-smoke`（指示書 design/PHASE480_STAGE13_HUNTER_SPEC.md §5・画面なし）
//
//   (a) 盤の形・前半の敵と倍率
//   (b) 門が開いた刻の終わりに勇者が門（敵の拠点のマス）に立つ。開かなければ 10 刻目。現れた刻は動かない
//   (c) 登場で目標が替わる・野営地は制圧できない・湧きが止まる・残りの敵は動かない
//   (d) 勇者は 2 歩・いちばん近い隊・戦った刻は動かない・拠点へは入らない。勇者との会戦に「引き返す」は無い
//   (e) 勝ち A（全隊が拠点）・勝ち B（勇者の HP が半分を割る）・負け（全滅）
//   (f) 第479期で直した「落ちた拠点の敵に隊が引き戻される」場面を V479 の 1-3 で踏む
//   (g) 単発・V461〜V479 は今のまま
//
// §4 の測り（`--chapter1-hunter-quick` は省く）: 前半（優勢）・勇者の重さ・撤退の方針 a〜d × 100 seed・線。
// **ここには Godot の型を1つも使わない。**
// =====================================================================================
public static class Chapter1HunterSmoke
{
    private const int Seeds = 100, WeighSeeds = 20, OddsSeeds = 30;

    /// <summary>撤退の方針（指示書 §4-3）。</summary>
    internal enum Flee { Home, Rear, Strike, Stay }

    internal static string FleeName(Flee f) => f switch
    {
        Flee.Home => "a. 全員まっすぐ帰る",
        Flee.Rear => "b. 殿を 1 隊出す",
        Flee.Strike => "c. 全隊を順に当てる",
        _ => "d. 何もしない",
    };

    internal sealed class Res
    {
        public bool Won, Driven, Escaped, Lost, Fallen, Fallback, Stalled;
        public int HeroTurn = -1, EndTurn, LostBefore, StartUnits, Survivors, Catches, Squads, Out;
        public bool DeepCaught, DeepHome, RearSent, RearDied;
        /// <summary>勇者との戦闘ごと: 削った割合（%）・入った枚数・入る前の HP（%）・勝ったか・殿か。</summary>
        public List<(double Cut, int Alive, double HpIn, bool Won, bool Rear)> Hero = new();
        /// <summary>前半の戦闘ごと: 5 枚の隊か・勝ち目（20 回のうちの勝ち・測らなければ -1）・勝ったか。</summary>
        public List<(bool Full, int Odds, bool Won, string Foe)> Front = new();
        public List<(string Foe, string Unit, int Turn, bool Rout)> Fell = new();
        public bool HeroEnteredHome;
    }

    internal sealed record Team(string Label, Map11State.CarryPack Pack);

    private static double Med(IEnumerable<double> xs) => Chapter1AsymSmoke.Med(xs);
    private static string T(double x) => double.IsNaN(x) ? "—" : x.ToString("0.#");

    /// <summary>1 章の 1-3 に規則を載せる（倍率は <paramref name="scale"/>・☆・加入の表・「窓を減らす」）。</summary>
    internal static MapStage Dress(MapStage raw, ChapterRule rule, MapFoeScale scale)
        => raw.Scaled(scale).Starred(rule.Stars ?? StarRule.Off).Rostered(rule.Roster ?? RosterRule.Off).Leaned(rule.Lean);

    // ---------------- 持ち込みの隊 ----------------

    private static readonly Dictionary<int, Map11State.CarryPack?> _botB = new();

    /// <summary>器具の組 B の 1-3 の始め（1-1 → 1-2 を器具で通した持ち越し・1-2 を落とした seed は null）。</summary>
    internal static Map11State.CarryPack? BotB(int seed)
    {
        lock (_botB)
        {
            if (_botB.TryGetValue(seed, out var hit)) return hit;
            MapStage s12 = Chapter1.StagesFor(ChapterRule.V480)[1];
            (Chapter1Stage12Smoke.Policy, int)[] tries =
            {
                (Chapter1Stage12Smoke.Policy.MidSouth, 0), (Chapter1Stage12Smoke.Policy.MidNorth, 0), (Chapter1Stage12Smoke.Policy.MidSouth, 3), (Chapter1Stage12Smoke.Policy.All, 0),
            };
            foreach (var (p, guard) in tries)
            {
                Map11State? last = null;
                var r = Chapter1Stage12Smoke.Play(s12, seed, p, guard, teamB: true, watch: st => last = st, siege: Chapter1Stage12Smoke.Siege.Hurt);
                if (r.Won && last is not null) return _botB[seed] = last.TakeCarry();
            }
            return _botB[seed] = null;
        }
    }

    /// <summary>人が組んだ隊（`design/plays` の 1-3 の始めの写し・表に無い駒のいる写しと中身が同じ写しは外す）。</summary>
    internal static List<Team> Humans(string demoDir, MapStage stage, List<string> dropped)
    {
        string root = PlayLog.DefaultRoot(demoDir);
        var pool = stage.Roster.On ? stage.Roster.PoolFor("1-3").Concat(stage.Roster.Start.Select(RosterRule.Find)).Select(d => d.Id).ToHashSet() : null;
        var list = new List<Team>();
        var seen = new HashSet<string>();
        foreach (PlayLog.Snap sn in PlayLog.List(root).OrderBy(x => x.Path, StringComparer.Ordinal))
        {
            if (sn.Stage != "1-3" || sn.Kind != "start" || sn.Pack is not { } p) continue;
            string label = $"{System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(sn.Path) ?? "")}（{sn.Rule}）";
            var units = p.Bench.Concat(p.Squads.SelectMany(q => q)).ToList();
            if (units.Count == 0) { dropped.Add($"{label}: 駒が 0 枚"); continue; }
            var outside = pool is null ? new List<string>() : units.Where(u => !pool.Contains(u.Id)).Select(u => u.Name).Distinct().ToList();
            if (outside.Count > 0) { dropped.Add($"{label}: 表に無い駒 {string.Join("・", outside)}"); continue; }
            string sig = string.Join("|", p.Squads.Select(q => string.Join(",", q.OrderBy(u => u.Slot).Select(u => $"{u.Id}{u.Star}.{u.Pips}@{u.Slot}")))) + $"#{p.Gold}";
            if (!seen.Add(sig)) { dropped.Add($"{label}: 中身が同じ写しがある（数えない）"); continue; }
            try { list.Add(new Team(label, PlayLog.ToCarry(p, stage.Stars))); }
            catch (Exception ex) { dropped.Add($"{label}: 読めない（{ex.GetType().Name}）"); }
        }
        return list;
    }

    private static double HpPct(Map11State st, int k) => st.Squads[k].Units is { } us && us.Sum(x => x.MaxHp) > 0
        ? 100.0 * us.Where(x => x.IsAlive).Sum(x => x.Hp) / us.Sum(x => x.MaxHp) : 0;

    private static bool Living(Map11State st, int i) => !st.Squads[i].Lost && st.Squads[i].Units is { } u && u.Any(x => x.IsAlive);

    internal static Map11State Setup(MapStage stage, Map11State.CarryPack pack, int seed)
    {
        var st = new Map11State(Chapter1.StageSeed(seed, 2, 0), stage.Time, stage.Portal, draft: true, stage: stage);
        st.ApplyCarry(pack);
        Chapter1Stage12Smoke.Spend(st);
        return st;
    }

    // ---------------- 1 通し ----------------

    /// <summary>
    /// 1 通し。前半: 枚数の多い順に 谷の橋 ／ 森の集落 ／ 尾根の砦跡 へ 1 隊ずつ（4 つ目からは拠点で番）。勇者が現れたら <paramref name="flee"/> のとおりに動く。戦闘は器具（台本なし）。
    /// <paramref name="odds"/> が真なら、前半の 5 枚の隊の戦闘ごとに勝ち目（同じ戦闘を 20 回）を数える。
    /// </summary>
    internal static Res Play(MapStage stage, Map11State.CarryPack pack, int seed, Flee flee, bool odds = false, Action<string>? log = null, Action<Map11State>? watch = null)
    {
        Map11State st = Setup(stage, pack, seed);
        MapGraph g = st.Graph;
        var res = new Res();
        int Op(int lane) => g.Lanes[lane].First(c => g.IsOutpost(c));
        int[] targets = { Op(1), Op(2), Op(0) };
        var order = Enumerable.Range(0, st.Squads.Length).Where(i => st.Squads[i].Units is { Count: > 0 }).OrderByDescending(i => st.Squads[i].Units!.Count(u => u.IsAlive)).ThenBy(i => i).ToList();
        res.Squads = order.Count;
        var rests = new int[st.Squads.Length];
        int deep = -1, rear = -1;
        bool routSeen = false, endedInMove = false;
        string Cell(int id) => id == g.Home ? "拠点" : id == g.Portal ? "野営地" : g[id].Name;
        int StepsHome(int i) => g.Path(st.Squads[i].Pos, st.Squads[i].Road, g.Home, -1).Count - 1;

        void TakeRelics()
        {
            while (st.RelicOffer.Count > 0)
            {
                if (!st.ChooseRelic(0)) break;
                if (st.RelicHand.Count == 0) continue;
                TraitId relic = st.RelicHand[^1];
                UnitState? holder = st.Squads.Where(q => q.Units is not null).SelectMany(q => q.Units!).FirstOrDefault(u => u.IsAlive && u.Relic is null && st.CanEquip(u));
                if (holder is not null) st.EquipRelic(holder, relic);
            }
        }

        void Fight(int squad, Map11State.Node node, bool intercept, bool caught)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            if (prep is not { } q) return;
            int alive0 = q.Players.Count(u => u.IsAlive);
            int max0 = q.Players.Sum(u => u.MaxHp);
            double hpIn = max0 == 0 ? 0 : 100.0 * q.Players.Where(u => u.IsAlive).Sum(u => u.Hp) / max0;
            int foeHp0 = node.HpNow;
            int wins = -1;
            if (odds && !node.IsHero && alive0 == 5)
            {
                wins = 0;
                for (int k = 0; k < 20; k++)
                    if (BattleEngine.Run(q.Players.Select(Map11State.CloneForOdds).ToList(), q.Enemies.Select(Map11State.CloneForOdds).ToList(), q.Seed * 31 + 1000 + k, verbose: false).PlayerWon) wins++;
            }
            var standing = q.Players.Where(u => u.IsAlive).ToList();
            bool rout = st.Rout;
            BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
            foreach (UnitState u in standing.Where(u => !u.IsAlive)) res.Fell.Add((node.Def.Name, u.Def.Name, u.LastDeathTurn, rout));
            int alive1 = q.Players.Count(u => u.IsAlive);
            st.Resolve(squad, q.Node, r.PlayerWon, intercept);
            if (node.IsHero) res.Hero.Add((100.0 * (foeHp0 - node.HpNow) / node.DefMaxHp, alive0, hpIn, r.PlayerWon, !caught));
            else if (!rout) res.Front.Add((alive0 == 5, wins, r.PlayerWon, node.Def.Name));
            if (node.IsHero && caught && squad == deep) res.DeepCaught = true;
            if (node.IsHero && squad == rear && alive1 == 0) res.RearDied = true;
            log?.Invoke($"    {st.Turn + (caught ? 0 : 1)} 刻目 ・ 隊 {squad + 1}（{Cell(node.Pos)}）対 {node.Def.Name}{(node.IsHero ? (caught ? "［追いつかれた］" : "［殿］") : intercept ? "［迎撃］" : "")}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T・{alive0} 枚 {hpIn:F0}% → {alive1} 枚"
                + (alive1 < alive0 ? $"・倒れた {string.Join("・", standing.Where(u => !u.IsAlive).Select(u => $"{u.Def.Name}（{u.LastDeathTurn}T）"))}" : "")
                + (node.IsHero ? $"・勇者の HP {node.HpNow}／{node.DefMaxHp}{(node.Retreated ? "（退いた）" : "")}" : ""));
            TakeRelics();
        }

        Dest Toward(int i, int cell)
        {
            if (cell == g.Portal || cell == g.Home) return Dest.Of(g, st.Squads[i].Pos == g.Home ? g.Lanes[1][0] : st.Squads[i].Pos);
            return Dest.Of(g, cell);
        }

        while (!st.Finished && st.Turn < 80 && !st.Fallen)
        {
            if (st.Rout && !routSeen)
            {
                routSeen = true;
                var outside = Enumerable.Range(0, st.Squads.Length).Where(i => Living(st, i) && st.Squads[i].Pos != g.Home).ToList();
                res.Out = outside.Count;
                if (outside.Count > 0) deep = outside.OrderByDescending(StepsHome).ThenBy(i => i).First();
                if (flee == Flee.Rear && outside.Count >= 2) { rear = outside.OrderBy(StepsHome).ThenBy(i => i).First(); res.RearSent = true; }
            }
            for (int i = 0; i < st.SquadCap && !st.Finished; i++)
            {
                if (!Living(st, i)) continue;
                Dest dest;
                if (!st.Rout)
                {
                    int k = order.IndexOf(i);
                    dest = k >= 0 && k < targets.Length ? Dest.Of(g, targets[k]) : Dest.Home;
                    if (rests[i] < 4 && (st.AtOutpost(i) || st.Squads[i].Pos == g.Home) && HpPct(st, i) < 60 && st.FoeFacing(i) is null && st.Rest(i)) { rests[i]++; continue; }
                }
                else
                {
                    bool strike = flee == Flee.Strike || (flee == Flee.Rear && i == rear);
                    if (flee == Flee.Stay) continue;
                    if (!strike) dest = Dest.Home;
                    else if (st.Hero is { Cleared: false } h)
                    {
                        // 拠点にいて勇者が分かれ道に立っているときは、中の道へ 1 歩出れば当たる。
                        if (st.Squads[i].Pos == g.Home && g[h.Pos].Lane < 0 && h.Pos != g.Portal)
                        {
                            if (st.Advance(i, 1) is { } met0) Fight(i, met0, false, false);
                            continue;
                        }
                        dest = Toward(i, h.Pos);
                    }
                    else continue;
                }
                if (Map11Orders.Apply(st, i, dest, Map11Orders.Plan(st, i, dest)) is { } node) Fight(i, node, false, false);
                TakeRelics();
            }
            if (st.Finished) { endedInMove = true; break; }
            if (!st.Rout) Chapter1Stage12Smoke.Spend(st);
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (st.Fallen || st.Finished) break;
                if (!e.Intercept) { Fight(e.Squad, e.Node, false, e.Node.IsHero); continue; }
                while (!st.Fallen && st.NextInterceptor(e.Node) is var j && j >= 0)
                {
                    Fight(j, e.Node, true, false);
                    if (e.Node.Cleared) break;
                }
                st.CloseIntercept(e.Node);
            }
            if (st.Hero is { Cleared: false } hh && hh.Pos == g.Home) res.HeroEnteredHome = true;
            watch?.Invoke(st);
            log?.Invoke($"  {st.Turn} 刻の終わり ・ {string.Join(" ／ ", Enumerable.Range(0, st.SquadCap).Where(i => st.Squads[i].Units is { Count: > 0 }).Select(i => $"隊 {i + 1} {Cell(st.Squads[i].Pos)} {st.Squads[i].Units!.Count(u => u.IsAlive)} 枚 {HpPct(st, i):F0}%"))}"
                + $" ・ 中間拠点 {string.Join("・", st.HeldOutposts.Select(c => g[c].Name).DefaultIfEmpty("—"))}"
                + (st.Hero is { } x ? $" ・ 勇者 {(x.Cleared ? "退いた" : Cell(x.Pos))} HP {x.HpNow}／{x.DefMaxHp}" : ""));
        }
        res.Won = st.Won; res.Driven = st.HunterDriven; res.Escaped = st.HunterEscaped; res.Lost = st.HunterLost; res.Fallen = st.Fallen; res.Stalled = st.HunterStalled;
        res.HeroTurn = st.HeroTurn; res.EndTurn = st.Turn + (endedInMove ? 1 : 0); res.Fallback = st.HeroTurn >= 0 && st.GateOpenedTurn < 0;
        res.LostBefore = res.Fell.Count(f => !f.Rout);
        res.StartUnits = st.HunterStartUnits; res.Survivors = st.HunterSurvivors; res.Catches = st.HunterCatches;
        res.DeepHome = deep >= 0 && Living(st, deep) && (st.Squads[deep].Pos == g.Home || st.HunterDriven);
        log?.Invoke($"  → {(st.HunterDriven ? "勇者を退かせた" : st.HunterEscaped ? "生還" : st.HunterLost ? "全滅" : st.Fallen ? "陥落" : "終わり")}・生還 {res.Survivors}／{res.StartUnits} 枚・金 {st.Gold}");
        return res;
    }

    // ---------------- 門 ----------------

    public static bool Run(Action<string> write, string demoDir, bool bots = true, bool sweep = false)
    {
        if (sweep) { Measure(write, ChapterRule.V480, demoDir, true); return true; }
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V480;
        var stages = Chapter1.StagesFor(rule);
        MapStage s = stages[2];
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}");

        void Win(Map11State st, int squad, Map11State.Node node, bool intercept = false)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Enemies) u.Hp = 0;
            st.Resolve(squad, node, true, intercept);
        }
        void Lose(Map11State st, int squad, Map11State.Node node)
        {
            var prep = st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Players) u.Hp = 0;
            st.Resolve(squad, node, false, false);
        }
        // 勇者の HP を最大の percent% にして、隊は生きたまま戦闘を終える（決着しなかった戦闘）。
        void Cut(Map11State st, int squad, Map11State.Node node, int percent)
        {
            var prep = st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Enemies) u.Hp = Math.Max(1, u.MaxHp * percent / 100);
            st.Resolve(squad, node, false, false);
        }
        void Walk(Map11State st, int squad, Dest dest, int steps = 30)
        {
            for (int k = 0; k < steps && !st.Finished; k++)
            {
                if (st.FoeFacing(squad) is { } here) { Win(st, squad, here); continue; }
                if (st.AtDest(squad, dest)) break;
                if (Map11Orders.Apply(st, squad, dest, Map11Orders.Plan(st, squad, dest)) is { } met) Win(st, squad, met);
            }
        }
        // 敵を 1 刻動かす。勇者との接敵は返す（精算しない）・ほかは勝たせる。
        List<Map11State.Encounter> Tick(Map11State st)
        {
            var meets = new List<Map11State.Encounter>();
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (e.Node.IsHero) { meets.Add(e); continue; }
                if (!e.Intercept) { Win(st, e.Squad, e.Node); continue; }
                if (st.NextInterceptor(e.Node) is var j && j >= 0) Win(st, j, e.Node, intercept: true);
                st.CloseIntercept(e.Node);
            }
            return meets;
        }
        Map11State Fresh(MapStage stage, params int[] keepLanes)
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            for (int r = 0; r < st.Nodes.Length; r++)
                if (!keepLanes.Contains(r))
                    foreach (var n in st.Nodes[r]) if (!n.Def.Stationary) n.Cleared = true;
            return st;
        }
        string Cell(Map11State st, int id) => id == st.Graph.Home ? "拠点" : id == st.Graph.Portal ? "野営地" : st.Graph[id].Name;
        MapStage quiet = Dress(MapStage.Stage13Ch480With(spawnEvery: 0), rule, rule.ScaleOf(2));   // 湧きの無い写し（歩く敵に邪魔されない場面用）

        // ---- (a) 盤の形・前半の敵と倍率 ----
        {
            MapGraph g = s.Board(true);
            int fort = g.Lanes[0][3], bridge = g.Lanes[1][3], village = g.Lanes[2][2], s3 = g.Lanes[2][3];
            bool shape = g.Lanes.Count == 3 && g.Lanes[0].Length == 6 && g.Lanes[1].Length == 5 && g.Lanes[2].Length == 6
                         && g.Lanes.All(l => l[0] == g.Lanes[0][0]) && g[g.Lanes[0][0]].Lane < 0
                         && g.IsOutpost(fort) && g.IsOutpost(bridge) && g.IsOutpost(village) && g.Cells.Count(c => c.Kind == MapGraph.Kind.Outpost) == 3
                         && g[fort].Neighbors.Contains(bridge) && g[bridge].Neighbors.Contains(s3) && g.Edges.Count(e => g.IsCross(e.A, e.B)) == 2
                         && g[g.Home].Name == MapGraph.Home13F480 && g[g.Portal].Name == MapGraph.Camp13F480;
            MapFoeScale sc = rule.ScaleOf(2);
            UnitDef hero = s.Hero.Unit!;
            bool rules = s.Hero.Hunter && s.Hero.Steps == 2 && s.Hero.FallbackTurn == 10 && s.Hero.RoutPercent == 50 && !s.Hero.Chaser && s.PortalGate == 2 && !s.TimeLimit && !s.Reserve.On
                         && s.EncounterRetreat && s.WipedReforms && !sc.HeroMends && !hero.Traits.Contains(TraitId.BossMend300)
                         && hero.MaxHp == MapFoeScale.Mul(EnemyCatalog.HeroMap.MaxHp, MapStage.Stage13Ch480HeroHp) && hero.Attack == MapFoeScale.Mul(EnemyCatalog.HeroMap.Attack, MapStage.Stage13Ch480HeroAtk)
                         && sc.FoePercent == ChapterRule.Stage13Ch480FoePercent && sc.HeroPercent == ChapterRule.Stage13Ch480HeroPercent;
            bool foes = s.Lanes.SelectMany(l => l).Where(n => !n.Stationary).All(n => n.Hold) && s.Lanes[1][^1].Stationary
                        && s.Lanes.SelectMany(l => l).Count(n => n.Name is "雷杖の部隊" or "槍兵の部隊") == 2;
            write($"  (a) 経路 {string.Join(" ／ ", g.Lanes.Select((l, i) => $"{g.LaneNames[i]} {l.Length - 1} マス"))}・中間拠点 {Cell0(g, fort)}・{Cell0(g, bridge)}・{Cell0(g, village)}・横の辺 {string.Join("・", g.Edges.Where(e => g.IsCross(e.A, e.B)).Select(e => $"{g[e.A].Name} ⇔ {g[e.B].Name}"))}");
            write($"      敵 {string.Join(" ／ ", s.Lanes.Select((l, i) => $"{s.LaneMark(i)}: {string.Join("・", l.Select(n => n.Name))}"))}・湧き {s.Spawn(0).Name}（{s.Portal.SpawnEvery} 刻ごと）");
            write($"      敵の倍率 ×{sc.FoePercent / 100.0:0.##}・勇者 HP {hero.MaxHp}（×{MapStage.Stage13Ch480HeroHp / 100.0:0.##}） 攻 {hero.Attack}（×{MapStage.Stage13Ch480HeroAtk / 100.0:0.##}）・{s.Hero.Steps} 歩・{s.Hero.FallbackTurn} 刻目・HP {s.Hero.RoutPercent}% で退く");
            Req(shape, "(a) 盤: 北 5 ／ 中 4 ／ 南 5 マス・中間拠点 3（北 3 ／ 中 3 ／ 南 2 マス目）・横の辺 2 本（砦跡 ⇔ 橋・橋 ⇔ 南3）");
            Req(rules && foes, "(a) 前半の敵は待ち構える（雷杖・槍兵は 1 部隊ずつ）・倍率と勇者の値は定数どおり・期限と予備隊は無い");
        }

        // ---- (b) 現れる ／ (c) 目標が替わる ----
        {
            var st = Fresh(s, 0);
            var g = st.Graph;
            int bridge = g.Lanes[1][3], village = g.Lanes[2][2];
            Walk(st, 0, Dest.Of(g, bridge));
            Walk(st, 1, Dest.Of(g, village));
            bool before = st.Hero is null && st.GateOpen && !st.Rout && !st.CanEnter(0, g.Portal);
            Tick(st);
            var h = st.Hero;
            write($"  (b) 第1隊 谷の橋・第2隊 森の集落 → 門 {(st.GateOpen ? "開" : "閉")}（刻 {st.GateOpenedTurn}）→ 勇者 {(h is null ? "なし" : Cell(st, h.Pos))}・現れた刻 {st.HeroTurn}・撤退の局面 {st.Rout}");
            Req(before && h is not null && h.Pos == g.Portal && st.HeroTurn == st.GateOpenedTurn && st.HeroTurn == 1 && st.Rout,
                "(b) 門が開いた刻の終わりに、勇者が野営地（開いた門）のマスに立つ。現れた刻は動かない。開いた刻のうちに野営地へは入れない");

            // (c) 湧き・残りの敵・制圧・金。
            int spawns0 = st.Spawns.Count(n => !n.IsHero);
            var pos0 = st.Nodes.SelectMany(r => r).Concat(st.Spawns).Where(n => !n.Cleared && !n.IsHero).Select(n => (n, n.Pos)).ToList();
            bool trade = st.CanTrade, enter = st.CanEnter(0, g.Portal) || st.CanEnter(1, g.Portal), limit = st.TimeLimitLeft is not null;
            // 2 隊とも動かさずに 2 刻（勇者は橋の隊に追いつく——精算しないで置く）。
            var m1 = Tick(st);
            int hp1 = h!.Pos;
            write($"  (c) 登場の後: 金を動かせる {trade}・野営地へ入れる {enter}・期限の札 {limit}・湧いた数 {spawns0} → {st.Spawns.Count(n => !n.IsHero)}・敵の位置は同じ {pos0.All(x => x.n.Pos == x.Pos)}・制圧 {st.Captured}");
            Req(!trade && !enter && !limit && st.Spawns.Count(n => !n.IsHero) == spawns0 && pos0.All(x => x.n.Pos == x.Pos) && !st.Captured && pos0.Count >= 3,
                "(c) 登場で撤退の局面になる: 野営地へは入れない（制圧できない）・呼ぶ ／ 呼び戻すは閉じる・湧きが止まる・残りの敵は動かない・期限は無い");
            write($"  (d) 次の刻: 勇者 野営地 → {Cell(st, hp1)}（2 歩）・接敵 {string.Join("・", m1.Select(e => $"隊 {e.Squad + 1}"))}・引き返せる {st.EncounterRetreat(0)}・引く先 {st.RetreatCellOf(0, false)}");
            Req(hp1 == bridge && m1.Count == 1 && m1[0].Squad == 0 && st.FacingHunter(0) && !st.EncounterRetreat(0) && st.RetreatCellOf(0, false) == -1 && st.Squads[0].Pos == bridge,
                "(d) 勇者は 1 刻に 2 歩でいちばん近い隊（橋）に追いつく。勇者との会戦に「引き返す」は無い");
        }

        // ---- (b) 開かなければ 10 刻目 ----
        {
            var st = Fresh(quiet);
            for (int k = 0; k < 9; k++) Tick(st);
            bool none = st.Hero is null && !st.Finished;
            Tick(st);
            write($"  (b) 隊を出さない: 9 刻目まで勇者 {(none ? "なし" : "あり")} → 10 刻目の終わりに {(st.Hero is null ? "なし" : Cell(st, st.Hero.Pos))}（現れた刻 {st.HeroTurn}）・全隊が拠点なので {(st.Won ? "その場で生還" : "続く")}");
            Req(none && st.Hero is { } h && h.Pos == st.Graph.Portal && st.HeroTurn == 10 && st.GateOpenedTurn < 0, "(b) 門が開かなければ、10 刻目の終わりに勇者が現れる");
        }

        // ---- (d) 2 歩・いちばん近い隊・殿・横の辺・拠点へは入らない ----
        {
            var st = Fresh(quiet);
            var g = st.Graph;
            int fort = g.Lanes[0][3], n4 = g.Lanes[0][4], bridge = g.Lanes[1][3], s3 = g.Lanes[2][3], village = g.Lanes[2][2];
            Walk(st, 0, Dest.Of(g, fort));
            Walk(st, 1, Dest.Of(g, village));
            Tick(st);
            var h = st.Hero!;
            var c0 = st.HunterChase();
            Tick(st);
            bool two = h.Pos == n4 && c0 is { Squad: 0, Steps: 3 };
            // 殿: 第1隊が自分から勇者のマス（北4）へ入り、倒れる。その刻の終わり、勇者は動かない。
            var met = Map11Orders.Apply(st, 0, Dest.Portal(g, 0), Map11Orders.Plan(st, 0, Dest.Portal(g, 0)));
            bool rearMeet = ReferenceEquals(met, h);
            if (rearMeet) Lose(st, 0, h);
            var mHold = Tick(st);
            bool held = h.Pos == n4 && mHold.Count == 0 && st.RearGuards.SetEquals(new[] { 0 }) && st.HunterCatches == 0;
            // 次の刻: 北4 → 砦跡 → 橋（横の辺）→ その次: 南3 → 集落（追いつく）。
            Tick(st);
            int p2 = h.Pos;
            var m3 = Tick(st);
            int p3 = h.Pos;
            write($"  (d) 第1隊 砦跡（3 歩）・第2隊 集落（4 歩）: 追う相手 隊 {(c0?.Squad ?? -1) + 1} → {Cell(st, n4)} ／ 殿（隊 1 が入る {rearMeet}）の刻は {(held ? "動かない" : "動いた")} → {Cell(st, p2)} → {Cell(st, p3)}（接敵 {m3.Count}）");
            Req(two, "(d) 勇者は 1 刻に 2 歩、いちばん近い隊を追う（野営地 → 北5 → 北4）");
            Req(rearMeet && held, "(d) 隊が自分から勇者のマスへ入った刻（殿）は、勇者は動かない");
            Req(p2 == bridge && p3 == village && m3.Count == 1 && m3[0].Squad == 1, "(d) 横の辺を使って次の隊を追う（砦跡 → 橋 → 南3 → 集落）");
            if (m3.Count == 1) Lose(st, 1, h);
            Req(st.HunterCatches == 1 && st.HunterLost && st.Finished && !st.Won, "(e) 負け: 隊に生きている駒が 1 枚もいない");

            // 同じ距離なら枚数の多い隊。
            var st2 = Fresh(quiet);
            Walk(st2, 0, Dest.Of(g, fort));
            Walk(st2, 1, Dest.Of(g, s3));
            Tick(st2);
            var even = st2.HunterChase();
            st2.Squads[0].Units![0].Hp = 0;
            var fewer = st2.HunterChase();
            write($"  (d) 砦跡と南3（どちらも 3 歩）: 同じ枚数なら 隊 {(even?.Squad ?? -1) + 1}・第1隊が 1 枚減ると 隊 {(fewer?.Squad ?? -1) + 1}");
            Req(even is { Squad: 0, Steps: 3 } && fewer is { Squad: 1, Steps: 3 }, "(d) 同じ距離なら生きている駒の多い隊（それも同じなら若い番号）");

            // 拠点へは入らない: 生きている隊が拠点にしかいなければ追う相手がいない。
            var st3 = Fresh(quiet);
            Walk(st3, 0, Dest.Of(g, bridge));
            Walk(st3, 1, Dest.Of(g, village));
            Tick(st3);
            int gold0 = st3.Gold;
            for (int k = 0; k < 8 && st3.Squads[0].Pos != g.Home; k++) st3.StepBack(0);
            bool mid = !st3.Won && st3.HunterChase() is { Squad: 1 };
            for (int k = 0; k < 8 && st3.Squads[1].Pos != g.Home; k++) st3.StepBack(1);
            write($"  (e) 2 隊とも拠点へ: 途中 勝ち {!mid}・着いた後 勝ち {st3.Won}（生還 {st3.HunterSurvivors} 枚・金 {gold0} → {st3.Gold}）・追う相手 {(st3.HunterChase() is null ? "なし" : "あり")}");
            Req(mid && st3.Won && st3.HunterEscaped && st3.Finished && st3.HunterSurvivors == 10 && st3.Gold == gold0 + 10 && st3.HunterChase() is null && st3.Hero!.Pos != g.Home,
                "(e) 勝ち A: 生きている隊がすべて拠点に入った（生還 1 枚につき ＋1）・勇者は拠点へ入らない");

            // 勝ち B: 勇者の HP が半分を割る。
            var st4 = Fresh(quiet);
            Walk(st4, 0, Dest.Of(g, bridge));
            Walk(st4, 1, Dest.Of(g, village));
            Tick(st4);
            var m = Tick(st4);
            var h4 = st4.Hero!;
            Cut(st4, m[0].Squad, h4, 60);
            bool still = !h4.Cleared && !st4.Won;
            int gold4 = st4.Gold;
            var mm = Tick(st4);
            if (mm.Count > 0) Cut(st4, mm[0].Squad, h4, 49);
            write($"  (e) 勇者の HP を 60% → {(still ? "続く" : "終わる")} ／ 49% → 退いた {h4.Retreated}・勝ち {st4.Won}・金 {gold4} → {st4.Gold}・結果の刻 {st4.HeroRoutTurn}");
            Req(still && mm.Count == 1 && h4.Cleared && h4.Retreated && st4.Won && st4.HunterDriven && st4.Finished && st4.Gold == gold4 + 20 + 10,
                "(e) 勝ち B: 勇者の HP が半分を割った刻に退く（その刻で勝ち・＋20 と生還の分）");
        }

        // ---- (f) 第479期の直し（落ちた拠点に居座る敵に隊が引き戻されない）を V479 の 1-3 で踏む ----
        {
            MapStage s479 = Chapter1.StagesFor(ChapterRule.V479)[2];
            var st = Fresh(s479, 0);
            var g = st.Graph;
            // 第1隊は北の砦の 1 つ手前（北の敵は残す）・第2隊は野営地と南の関所を取って門を開ける。勇者（第459期・追ってくる）が拠点を落とす。
            foreach (var n in st.Nodes[0]) n.Cleared = true;
            Walk(st, 0, Dest.Of(g, g.Lanes[0][3]));
            Walk(st, 1, Dest.Of(g, g.Lanes[2][3]));
            for (int k = 0; k < 6 && !st.HomeLost; k++) foreach (var e in Tick(st)) { }
            // 北の道の敵を 1 部隊、落ちた拠点に居座らせる。
            var sitter = st.Nodes[0][0];
            sitter.Cleared = false;
            sitter.Pos = g.Home;
            int cell0 = st.Squads[0].Cell;
            Dest deep = Dest.Portal(g, 0);
            var order = Map11Orders.Plan(st, 0, deep);
            var met = Map11Orders.Apply(st, 0, deep, order);
            write($"  (f) V479 の 1-3: 拠点が落ちた {st.HomeLost}・北の道の敵が拠点に居座る → 第1隊（北の砦）の命令 {order}・位置 {cell0} → {st.Squads[0].Cell}・当たった相手 {(met is null ? "なし" : met.Def.Name)}");
            Req(st.HomeLost && order == Map11Orders.Order.Advance && st.Squads[0].Cell == cell0 + 1 && !ReferenceEquals(met, sitter) && st.Squads[0].Pos != g.Home,
                "(f) 落ちた拠点に居座る敵に、道の上の隊が引き戻されない（第479期の直し・V479 の 1-3）");
        }

        // ---- (g) 単発・V461〜V479 は今のまま ----
        {
            bool others = ChapterRule.Versions.Where(v => v.Key != "V480").All(v => v.Rule.Third is null && ReferenceEquals(Chapter1.StagesFor(v.Rule)[2].Board, MapStage.Stage13.Board)
                                                                                    && !Chapter1.StagesFor(v.Rule)[2].Hero.Hunter);
            bool single = MapStage.All.All(x => !x.Hero.Hunter) && MapStage.Stage13.Hero == HeroRule.V459 with { } && MapStage.Stage13.Hero.Chaser;
            bool same12 = ReferenceEquals(Chapter1.StagesFor(rule)[0], Chapter1.StagesFor(ChapterRule.V479)[0]) && ReferenceEquals(Chapter1.StagesFor(rule)[1], Chapter1.StagesFor(ChapterRule.V479)[1]);
            write($"  (g) V480 のほかの版の 1-3 は単発の盤・追ってくる勇者のまま {others}・単発の 3 ステージに狩る者なし {single}・V480 の 1-1 ／ 1-2 は V479 と同じ写し {same12}");
            Req(others && single && same12, "(g) 単発・V461〜V479 は今のまま（V480 が替えたのは 1-3 だけ）");
        }

        if (bots) ok &= Measure(write, rule, demoDir, false);
        return ok;
    }

    private static string Cell0(MapGraph g, int id) => g[id].Name;

    // ---------------- §4 の測り ----------------

    internal sealed record Cand(string Label, int Foe, int HeroHp, int HeroAtk, int Stack = MapStage.Stage13Ch480Stack, int Steps = 2, int Fallback = 10,
                                int SpawnEvery = MapStage.Stage13Ch480SpawnEvery, int Rout = 50, int[]? Len = null, int[]? Op = null);

    /// <summary>今の定数の候補。</summary>
    internal static readonly Cand Now = new("今", ChapterRule.Stage13Ch480FoePercent, MapStage.Stage13Ch480HeroHp, MapStage.Stage13Ch480HeroAtk);

    /// <summary>振る候補（`--chapter1-hunter-sweep`・指示書 §4 の「倍率・勇者の歩数・登場の刻・盤の長さ」）。</summary>
    internal static IEnumerable<Cand> SweepCands()
    {
        const int Hp = 600, Atk = 250;
        // 1) 倍率（勇者の HP × 攻撃）
        foreach (int hp in new[] { 160, 400, 600, 800, 1000 })
            foreach (int atk in new[] { 160, 250, 400 })
                yield return new($"倍率: HP×{hp / 100.0:0.#} 攻×{atk / 100.0:0.#}", 100, hp, atk, 2);
        // 2) 勇者の歩数
        foreach (int steps in new[] { 1, 3 }) yield return new($"歩数: {steps} 歩", 100, Hp, Atk, 2, Steps: steps);
        // 3) 登場の刻（中間拠点に重ねる部隊の数で門が開く刻が動く）
        foreach (int stack in new[] { 1, 3, 4 }) yield return new($"登場: 重ね {stack}", 100, Hp, Atk, stack);
        // 4) 盤の長さ・中間拠点の位置（分かれ道を除くマスの数 北／中／南・中間拠点のマス目）
        yield return new("盤: 橋を中2へ（5/4/5・3/2/2）", 100, Hp, Atk, 2, Len: new[] { 5, 4, 5 }, Op: new[] { 3, 2, 2 });
        yield return new("盤: 中間拠点を手前に（5/4/5・2/2/2）", 100, Hp, Atk, 2, Len: new[] { 5, 4, 5 }, Op: new[] { 2, 2, 2 });
        yield return new("盤: 道を長く（7/6/7・4/3/3）", 100, Hp, Atk, 2, Len: new[] { 7, 6, 7 }, Op: new[] { 4, 3, 3 });
        yield return new("盤: 道を長く（7/6/7・4/4/3）", 100, Hp, Atk, 2, Len: new[] { 7, 6, 7 }, Op: new[] { 4, 4, 3 });
        yield return new("盤: 道を長く（8/7/8・5/4/4）・重ね 1", 100, Hp, Atk, 1, Len: new[] { 8, 7, 8 }, Op: new[] { 5, 4, 4 });
        // 5) 敵の倍率（前半）
        foreach (int foe in new[] { 80, 130, 160 }) yield return new($"前半: 敵 ×{foe / 100.0:0.#}", foe, Hp, Atk, 2);
    }

    internal static MapStage StageOf(Cand c, ChapterRule rule, int fallback = -1)
        => Dress(MapStage.Stage13Ch480With(steps: c.Steps, fallback: fallback >= 0 ? fallback : c.Fallback, routPercent: c.Rout, spawnEvery: c.SpawnEvery,
                                            heroHp: c.HeroHp, heroAtk: c.HeroAtk, stack: c.Stack, len: c.Len, op: c.Op), rule, new MapFoeScale(c.Foe, 100, HeroMends: false));

    private static bool Measure(Action<string> write, ChapterRule rule, string demoDir, bool sweep)
    {
        var dropped = new List<string>();
        MapStage now = StageOf(Now, rule);
        List<Team> humans = Humans(demoDir, now, dropped);
        var botTeams = Enumerable.Range(0, Seeds).Select(seed => (Seed: seed, Pack: BotB(seed))).Where(x => x.Pack is not null).ToList();
        write("");
        write($"## §4 の測り（{Seeds} seed）");
        write($"- 器具の組 B: 1-1 → 1-2 を器具で通して 1-3 に入れた seed {botTeams.Count}／{Seeds}");
        write($"- ポンの写し（1-3 の始め）: {humans.Count} 本 {string.Join("・", humans.Select(h => h.Label))}" + (dropped.Count > 0 ? $"（外した {dropped.Count}: {string.Join(" ／ ", dropped.Select(d => d.Split(':')[0]))}）" : ""));
        var groups = new (string Name, List<(Map11State.CarryPack Pack, int Seed)> Runs)[]
        {
            ("組 B", botTeams.Select(x => (x.Pack!, x.Seed)).ToList()),
            ("ポンの写し", humans.SelectMany(h => Enumerable.Range(0, Seeds / Math.Max(1, humans.Count)).Select(seed => (h.Pack, seed))).ToList()),
        };
        bool all = true;
        if (sweep)
        {
            // 通しの記録 1 本ずつ（組 B の先頭・ポンの写しの先頭・方針 a）と、隊ごとの勇者の重さ。
            Cand probe = new("HP×6 攻×2.5", 100, 600, 250, 2);
            foreach (var (name, runs) in groups)
            {
                if (runs.Count == 0) continue;
                write($"--- {name} の通し（{probe.Label}・方針 a・seed {runs[0].Seed}）");
                Play(StageOf(probe, rule), runs[0].Pack, runs[0].Seed, Flee.Home, log: write);
            }
            {
                // いちばん削る隊の 1 戦のログ（何が削っているか）。
                if (humans.Count == 0) return true;
                MapStage ps = StageOf(probe, rule, fallback: 1);
                Map11State pst = Setup(ps, humans[0].Pack, 0);
                pst.AdvanceFoes();
                int main = Enumerable.Range(0, pst.Squads.Length).Where(i => pst.Squads[i].Units is { Count: > 0 }).OrderByDescending(i => pst.Squads[i].Units!.Count(u => u.IsAlive)).First();
                var pp = pst.PeekSquad(main).Select(Map11State.CloneForOdds).ToList();
                var ee = pst.PeekFoe(pst.Hero!).Select(Map11State.CloneForOdds).ToList();
                BattleResult br = BattleEngine.Run(pp, ee, 7000, verbose: true);
                var dmg = br.Events.Where(e => (e.Kind == BattleEventKind.Damage || e.Kind == BattleEventKind.Status) && e.TargetId == ee[0].InstanceId).GroupBy(e => (SourceId: (e.Kind == BattleEventKind.Status ? -1 : e.ActorId ?? -2), e.Turn)).Select(gp => (gp.Key, Sum: gp.Sum(e => e.Amount), N: gp.Count())).OrderByDescending(x => x.Sum).Take(12);
                foreach (var d in dmg) write($"--- 削り: T{d.Key.Turn} 出どころ {pp.Concat(ee).FirstOrDefault(u => u.InstanceId == d.Key.SourceId)?.Def.Name ?? (d.Key.SourceId == -1 ? "継続（毒・燃焼）" : "出どころなし")} 合計 {d.Sum}（{d.N} 回）");
                write($"--- {br.Turns}T・勝ち {br.PlayerWon}");
            }
            foreach (var w in Weigh(probe, rule, humans.Select(h => h.Pack).ToList()))
                write($"--- 勇者の重さ（{probe.Label}）: {w.Names} → 勝ち {w.Wins}／{WeighSeeds}・削り 中央値 {w.Cut:0.#}% 最大 {w.Max:0.#}%");

            write("");
            write("| 候補 | 組 | 失う駒 | 現れる刻 | 優勢 | 勇者に勝つ（中央値／最大） | 削り（中央値／最大の隊／最大の 1 戦） | a 勝ち | a 生還した駒 | a 奥が追いつかれる | b 奥が生還 | b 生還した駒 | c 退かせる | d 全滅 | a の刻 | 通った線 |");
            write("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        }
        foreach (Cand c in sweep ? SweepCands() : new[] { Now })
        {
            MapStage st = StageOf(c, rule);
            if (!sweep)
            {
                write("");
                write($"### 候補「{c.Label}」: 敵 ×{c.Foe / 100.0:0.##}・勇者 HP ×{c.HeroHp / 100.0:0.##} 攻 ×{c.HeroAtk / 100.0:0.##}・中間拠点に {c.Stack} 部隊・{c.Steps} 歩・{c.Fallback} 刻目・湧き {(c.SpawnEvery > 0 ? $"{c.SpawnEvery} 刻ごと" : "なし")}・HP {c.Rout}% で退く");
            }
            foreach (var (name, runs) in groups)
            {
                if (runs.Count == 0) { write($"- {name}: 隊なし"); continue; }
                all &= Lines(write, name, st, c, rule, runs, sweep);
            }
        }
        return all;
    }

    /// <summary>1 候補 × 1 組の表と線。</summary>
    private static bool Lines(Action<string> write, string group, MapStage st, Cand c, ChapterRule rule, List<(Map11State.CarryPack Pack, int Seed)> runs, bool compact)
    {
        Action<string> detail = compact ? _ => { } : write;
        var by = new Dictionary<Flee, List<Res>>();
        foreach (Flee f in Enum.GetValues<Flee>())
        {
            var rs = new Res[runs.Count];
            System.Threading.Tasks.Parallel.For(0, runs.Count, i => rs[i] = Play(st, runs[i].Pack, runs[i].Seed, f, odds: f == Flee.Home && i < OddsSeeds));
            by[f] = rs.ToList();
        }
        var a = by[Flee.Home];
        // ---- 4-1 前半 ----
        double lostBefore = a.Average(r => (double)r.LostBefore);
        double gate = Med(a.Where(r => r.HeroTurn >= 0).Select(r => (double)r.HeroTurn));
        int fallback = a.Count(r => r.Fallback), fallen = a.Count(r => r.Fallen);
        var full = a.SelectMany(r => r.Front).Where(x => x.Full && x.Odds >= 0).ToList();
        double good = full.Count == 0 ? double.NaN : 100.0 * full.Count(x => x.Odds >= 16) / full.Count;
        var frontAll = a.SelectMany(r => r.Front).ToList();
        detail($"#### {group}（{runs.Count} 通し）");
        detail("| 4-1 前半 | 値 | 線 |");
        detail("|---|---|---|");
        detail($"| 門が開くまでに失う駒（平均） | {lostBefore:0.00} | 1 枚以下 |");
        detail($"| 勇者が現れる刻（中央値） | {T(gate)}（10 刻目で現れた {fallback}・前半に陥落 {fallen}） | 6〜8 |");
        detail($"| 5 枚の隊の戦闘が「優勢」 | {T(good)}%（{full.Count} 戦・前半の勝ち {frontAll.Count(x => x.Won)}／{frontAll.Count}） | 90% 以上 |");
        var worst = frontAll.GroupBy(x => x.Foe).Select(gp => (gp.Key, N: gp.Count(), W: gp.Count(x => x.Won))).OrderBy(x => (double)x.W / x.N).Take(3);
        detail($"- 前半で勝ちの少ない相手: {string.Join("・", worst.Select(x => $"{x.Key} {x.W}／{x.N}"))}");
        var fellBefore = a.SelectMany(r => r.Fell).Where(f => !f.Rout).GroupBy(f => (f.Unit, f.Foe)).OrderByDescending(gp => gp.Count()).Take(4);
        detail($"- 前半に倒れた駒: {string.Join("・", fellBefore.Select(gp => $"{gp.Key.Unit}（{gp.Key.Foe}・{gp.Count()} 回・平均 {gp.Average(f => f.Turn):0.#}T）").DefaultIfEmpty("なし"))}");

        // ---- 4-2 勇者の重さ（満タンの主力 5 枚 × 20 seed・隊ごと） ----
        var weigh = Weigh(c, rule, runs.Select(r => r.Pack).Distinct().Take(12).ToList());
        detail("");
        detail("| 4-2 勇者 | 値 | 線 |");
        detail("|---|---|---|");
        detail($"| 満タンの 5 枚の隊の勝ち（20 戦のうち・隊ごとの中央値 ／ 最大） | {T(Med(weigh.Select(w => (double)w.Wins)))} ／ {weigh.Select(w => w.Wins).DefaultIfEmpty(0).Max()} | 0〜1 |");
        detail($"| 1 戦で削る勇者の HP（隊ごとの中央値の中央値） | {T(Med(weigh.Select(w => w.Cut)))}% | 15〜25% |");
        detail($"| いちばん削る隊（中央値 ／ 最大の 1 戦） | {T(weigh.Select(w => w.Cut).DefaultIfEmpty(0).Max())}% ／ {T(weigh.Select(w => w.Max).DefaultIfEmpty(0).Max())}% | 30% 超は可・50% 未満 |");
        if (weigh.Count > 0)
        {
            var top = weigh.OrderByDescending(w => w.Cut).First();
            detail($"- いちばん削る隊: {top.Names}");
        }

        // ---- 4-3 後半 ----
        detail("");
        detail("| 4-3 方針 | 勝ち | 生還 | 退かせた | 全滅 | 生還した駒（中央値） | いちばん奥の隊が追いつかれた ／ 生還した | 殿が倒れた | 登場から終わりまで（中央値） |");
        detail("|---|---|---|---|---|---|---|---|---|");
        foreach (Flee f in Enum.GetValues<Flee>())
        {
            var rs = by[f].Where(r => r.HeroTurn >= 0 && !r.Fallback).ToList();
            if (rs.Count == 0) { detail($"| {FleeName(f)} | — |"); continue; }
            double P(Func<Res, bool> q) => 100.0 * rs.Count(q) / rs.Count;
            var withDeep = rs.Where(r => r.Out > 0).ToList();
            double PD(Func<Res, bool> q) => withDeep.Count == 0 ? double.NaN : 100.0 * withDeep.Count(q) / withDeep.Count;
            var sent = rs.Where(r => r.RearSent).ToList();
            detail($"| {FleeName(f)} | {P(r => r.Won):0}% | {P(r => r.Escaped):0}% | {P(r => r.Driven):0}% | {P(r => r.Lost):0}% | {T(Med(rs.Where(r => r.StartUnits > 0).Select(r => 100.0 * r.Survivors / r.StartUnits)))}% "
                + $"| {T(PD(r => r.DeepCaught))}% ／ {T(PD(r => r.DeepHome))}% | {(sent.Count == 0 ? "—" : $"{100.0 * sent.Count(r => r.RearDied) / sent.Count:0}%（出した {sent.Count}）")} | {T(Med(rs.Select(r => (double)(r.EndTurn - r.HeroTurn))))} 刻 |");
        }
        var heroFights = by[Flee.Strike].SelectMany(r => r.Hero).ToList();
        detail($"- 方針 c の勇者との戦闘 {heroFights.Count} 回: 1 戦で削る HP の中央値 {T(Med(heroFights.Select(x => x.Cut)))}%・勝ち {heroFights.Count(x => x.Won)}");
        var fellRout = by[Flee.Home].SelectMany(r => r.Fell).Where(f => f.Rout).GroupBy(f => f.Unit).OrderByDescending(gp => gp.Count()).Take(5);
        detail($"- 方針 a で勇者に倒された駒: {string.Join("・", fellRout.Select(gp => $"{gp.Key}（{gp.Count()} 回・平均 {gp.Average(f => f.Turn):0.#}T）").DefaultIfEmpty("なし"))}");
        detail($"- 勇者が拠点へ入った通し {by.Values.SelectMany(x => x).Count(r => r.HeroEnteredHome)}・打ち切り {by.Values.SelectMany(x => x).Count(r => r.Stalled)}");

        // ---- 線 ----
        List<Res> R(Flee f) => by[f].Where(r => r.HeroTurn >= 0 && !r.Fallback).ToList();
        double Pct(List<Res> rs, Func<Res, bool> q) => rs.Count == 0 ? double.NaN : 100.0 * rs.Count(q) / rs.Count;
        double Surv(Flee f) => Med(R(f).Where(r => r.StartUnits > 0).Select(r => 100.0 * r.Survivors / r.StartUnits));
        var lines = new (string Name, bool Ok)[]
        {
            ("4-1 失う駒 ≤ 1", lostBefore <= 1.0),
            ("4-1 門が開く刻 6〜8", gate >= 6 && gate <= 8),
            ("4-1 優勢 ≥ 90%", good >= 90),
            ("4-2 勝ち 0〜1／20", Med(weigh.Select(w => (double)w.Wins)) <= 1),
            ("4-2 削り 15〜25%", Med(weigh.Select(w => w.Cut)) is >= 15 and <= 25),
            ("4-2 1 隊で半分を割らない", weigh.Select(w => w.Max).DefaultIfEmpty(0).Max() < 50),
            ("4-3 a 勝ち ≥ 90%", Pct(R(Flee.Home), r => r.Won) >= 90),
            ("4-3 a 奥の隊が追いつかれる ≥ 70%", Pct(R(Flee.Home).Where(r => r.Out > 0).ToList(), r => r.DeepCaught) >= 70),
            ("4-3 a 生還 60〜80%", Surv(Flee.Home) is >= 60 and <= 80),
            ("4-3 b 奥の隊が生還 ≥ 60%", Pct(R(Flee.Rear).Where(r => r.RearSent).ToList(), r => r.DeepHome) >= 60),
            ("4-3 c 退かせる 20〜50%", Pct(R(Flee.Strike), r => r.Driven) is >= 20 and <= 50),
            ("4-3 d ほぼ全滅", Pct(R(Flee.Stay), r => r.Lost) >= 90),
            ("4-3 a がいちばん多く生還しない（殿に意味がある）", !(Surv(Flee.Home) >= Surv(Flee.Rear))),
            ("4-3 登場から終わりまで 4〜7 刻", Med(R(Flee.Home).Select(r => (double)(r.EndTurn - r.HeroTurn))) is >= 4 and <= 7),
        };
        detail("");
        detail($"- 線: {string.Join(" ／ ", lines.Select(l => $"{(l.Ok ? "○" : "×")} {l.Name}"))}");
        if (compact)
        {
            var deepRs = R(Flee.Home).Where(r => r.Out > 0).ToList();
            write($"| {c.Label}・重ね {c.Stack}・敵 ×{c.Foe / 100.0:0.##} | {group} | {lostBefore:0.00} | {T(gate)} | {T(good)}% | {T(Med(weigh.Select(w => (double)w.Wins)))}／{weigh.Select(w => w.Wins).DefaultIfEmpty(0).Max()} "
                + $"| {T(Med(weigh.Select(w => w.Cut)))}%／{T(weigh.Select(w => w.Cut).DefaultIfEmpty(0).Max())}%／{T(weigh.Select(w => w.Max).DefaultIfEmpty(0).Max())}% | {T(Pct(R(Flee.Home), r => r.Won))}% | {T(Surv(Flee.Home))}% | {T(Pct(deepRs, r => r.DeepCaught))}% "
                + $"| {T(Pct(R(Flee.Rear).Where(r => r.RearSent).ToList(), r => r.DeepHome))}% | {T(Surv(Flee.Rear))}% | {T(Pct(R(Flee.Strike), r => r.Driven))}% | {T(Pct(R(Flee.Stay), r => r.Lost))}% "
                + $"| {T(Med(R(Flee.Home).Select(r => (double)(r.EndTurn - r.HeroTurn))))} | {lines.Count(l => l.Ok)}／{lines.Length} |");
        }
        return lines.All(l => l.Ok);
    }

    /// <summary>勇者の重さ（指示書 §4-2）: 隊ごとに、1-3 の始めの主力（いちばん枚数の多い隊・満タン）を勇者に 20 回当てる。</summary>
    internal static List<(string Names, int Wins, double Cut, double Max)> Weigh(Cand c, ChapterRule rule, List<Map11State.CarryPack> packs)
    {
        MapStage stage = StageOf(c, rule, fallback: 1);
        var list = new List<(string, int, double, double)>();
        foreach (var pack in packs)
        {
            Map11State st = Setup(stage, pack, 0);
            st.AdvanceFoes();
            if (st.Hero is not { } h) continue;
            int main = Enumerable.Range(0, st.Squads.Length).Where(i => st.Squads[i].Units is { Count: > 0 }).OrderByDescending(i => st.Squads[i].Units!.Count(u => u.IsAlive)).ThenBy(i => i).DefaultIfEmpty(-1).First();
            if (main < 0) continue;
            var squad = st.PeekSquad(main).Where(u => u.IsAlive).ToList();
            var foe = st.PeekFoe(h);
            int wins = 0;
            var cuts = new List<double>();
            for (int k = 0; k < WeighSeeds; k++)
            {
                var ps = squad.Select(Map11State.CloneForOdds).ToList();
                foreach (UnitState u in ps) u.Hp = u.MaxHp;
                var es = foe.Select(Map11State.CloneForOdds).ToList();
                if (BattleEngine.Run(ps, es, 7000 + k * 13, verbose: false).PlayerWon) wins++;
                cuts.Add(100.0 * (h.DefMaxHp - es.Where(u => u.IsAlive).Sum(u => u.Hp)) / h.DefMaxHp);
            }
            list.Add((string.Join("・", squad.Select(u => u.Def.Name)), wins, Med(cuts), cuts.Max()));
        }
        return list;
    }
}
