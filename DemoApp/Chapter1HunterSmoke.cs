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
    internal enum Flee { Home, Rear, Strike, Stay, Solo, SoloRepeat, Bench, RearDeep }

    internal static string FleeName(Flee f) => f switch
    {
        Flee.Home => "a. 全員まっすぐ帰る",
        Flee.Rear => "b. 殿を 1 隊出す",
        Flee.Strike => "c. 全隊を順に当てる",
        Flee.Stay => "d. 何もしない",
        Flee.Solo => "e. 1 枚だけの隊を殿に（1 隊）",
        Flee.SoloRepeat => "e'. 1 枚だけの隊を殿に（枠のかぎり・毎刻くり返す）",
        Flee.RearDeep => "g. いちばん奥の隊を殿に（1 隊）",
        _ => "f. 中間拠点で手札に下げて拠点の隊へ組む（今の規則の穴）",
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
        /// <summary>方針 e: 手札を通して拠点の隊へ移した駒・1 枚にした隊の数・1 枚の殿が勇者と戦った回数（＝ 勇者を止めた刻）・拠点に組める隊の枠が無かった。</summary>
        public int Moved, Solos, SoloFights, SoloHolds;
        public bool NoFrame, TimeUp;
        /// <summary>追記 4: 追いつかれた隊（重ねない）・行き先が勇者のマスだったので殿に数えた戦闘・殿が足を止めた回数。</summary>
        public HashSet<int> CaughtSquads = new();
        public int Aimed, Holds;
    }

    internal sealed record Team(string Label, Map11State.CarryPack Pack);

    private static double Med(IEnumerable<double> xs) => Chapter1AsymSmoke.Med(xs);
    private static string T(double x) => double.IsNaN(x) ? "—" : x.ToString("0.#");

    /// <summary>1 章の 1-3 に規則を載せる（倍率は <paramref name="scale"/>・☆・加入の表・「窓を減らす」）。</summary>
    internal static MapStage Dress(MapStage raw, ChapterRule rule, MapFoeScale scale)
        => raw.Scaled(scale).Starred(rule.Stars ?? StarRule.Off).Rostered(rule.Roster ?? RosterRule.Off).Leaned(rule.Lean);

    // ---------------- 持ち込みの隊 ----------------

    private static readonly Dictionary<(int, bool), Map11State.CarryPack?> _bot = new();

    /// <summary>器具の組の 1-3 の始め（1-1 → 1-2 を器具で通した持ち越し・今の加入の表と金の規則・1-2 を落とした seed は null）。組 B ＝ <paramref name="teamB"/>。</summary>
    internal static Map11State.CarryPack? Bot(int seed, bool teamB)
    {
        lock (_bot)
        {
            if (_bot.TryGetValue((seed, teamB), out var hit)) return hit;
            MapStage s12 = Chapter1.StagesFor(ChapterRule.V480)[1];
            (Chapter1Stage12Smoke.Policy, int)[] tries =
            {
                (Chapter1Stage12Smoke.Policy.MidSouth, 0), (Chapter1Stage12Smoke.Policy.MidNorth, 0), (Chapter1Stage12Smoke.Policy.MidSouth, 3), (Chapter1Stage12Smoke.Policy.All, 0),
            };
            foreach (var (p, guard) in tries)
            {
                Map11State? last = null;
                var r = Chapter1Stage12Smoke.Play(s12, seed, p, guard, teamB: teamB, watch: st => last = st, siege: Chapter1Stage12Smoke.Siege.Hurt);
                if (r.Won && last is not null) return _bot[(seed, teamB)] = last.TakeCarry();
            }
            return _bot[(seed, teamB)] = null;
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

    internal static Map11State Setup(MapStage stage, Map11State.CarryPack pack, int seed, int slots = 0)
    {
        var st = new Map11State(Chapter1.StageSeed(seed, 2, 0), stage.Time, stage.Portal, draft: true, stage: stage);
        st.ApplyCarry(pack);
        // 方針 e: 1 枚だけの隊のための枠を先に買う（強化より先・買うと付く無料の 1 枚も呼ぶ）。
        for (int n = 0; n < slots && st.BuySlot(); n++) if (st.FreePicks > 0) st.Recruit(0);
        Chapter1Stage12Smoke.Spend(st);
        return st;
    }

    // ---------------- 1 通し ----------------

    /// <summary>
    /// 1 通し。前半: 枚数の多い順に 谷の橋 ／ 森の集落 ／ 尾根の砦跡 へ 1 隊ずつ（4 つ目からは拠点で番）。勇者が現れたら <paramref name="flee"/> のとおりに動く。戦闘は器具（台本なし）。
    /// <paramref name="odds"/> が真なら、前半の 5 枚の隊の戦闘ごとに勝ち目（同じ戦闘を 20 回）を数える。
    /// </summary>
    internal static Res Play(MapStage stage, Map11State.CarryPack pack, int seed, Flee flee, bool odds = false, Action<string>? log = null, Action<Map11State>? watch = null, bool soloWeak = false)
    {
        Map11State st = Setup(stage, pack, seed, flee == Flee.Solo ? 1 : flee == Flee.SoloRepeat ? 2 : 0);
        MapGraph g = st.Graph;
        var res = new Res();
        int Op(int lane) => g.Lanes[lane].First(c => g.IsOutpost(c));
        int[] targets = { Op(1), Op(2), Op(0) };
        var order = new List<int>();
        var solo = new HashSet<int>();
        int deep = -1, rear = -1;
        List<int> Order() => Enumerable.Range(0, st.Squads.Length).Where(i => !solo.Contains(i) && st.Squads[i].Units is { Count: > 0 }).OrderByDescending(i => st.Squads[i].Units!.Count(u => u.IsAlive)).ThenBy(i => i).ToList();
        var rests = new int[st.Squads.Length];
        // 方針 e ／ e': 前半のうちに 1 枚だけの隊を作って主力の後ろを付いていかせる（手札の駒から・無ければいちばん枚数の少ない隊から 1 枚）。枠が無ければ 1 つ買う。
        if (flee is Flee.Solo or Flee.SoloRepeat)
        {
            int want = flee == Flee.Solo ? 1 : 3;
            for (int n = 0; n < want; n++)
            {
                int free = Enumerable.Range(0, st.SquadCap).Where(i => st.Squads[i].Units is not { Count: > 0 } && st.CanReform(i)).DefaultIfEmpty(-1).First();
                if (free < 0) break;
                int bi = st.Bench.Select((u, k) => (u, k)).Where(x => x.u.IsAlive).OrderBy(x => soloWeak ? x.u.MaxHp : -x.u.MaxHp).Select(x => x.k).DefaultIfEmpty(-1).First();
                if (bi < 0)
                {
                    int from = Enumerable.Range(0, st.SquadCap).Where(i => !solo.Contains(i) && st.Squads[i].Units is { Count: >= 2 }).OrderBy(i => st.Squads[i].Units!.Count).DefaultIfEmpty(-1).First();
                    if (from < 0) break;
                    UnitState weak = st.Squads[from].Units!.OrderBy(u => soloWeak ? u.MaxHp : -u.MaxHp).First();
                    if (!st.SwapWithBench(from, weak.Slot, -1)) break;
                    bi = st.Bench.IndexOf(weak);
                }
                if (!st.SwapWithBench(free, 0, bi)) break;
                solo.Add(free);
            }
            log?.Invoke($"  [e] 枠 {st.SquadCap}／{st.Squads.Length}・隊 {string.Join(",", st.Squads.Select(q => q.Units?.Count.ToString() ?? "null"))}・組み直せる {string.Join(",", Enumerable.Range(0, st.Squads.Length).Select(i => st.CanReform(i)))}・手札 {st.Bench.Count}・金 {st.Gold}・1 枚の隊 {solo.Count}");
            res.Solos = solo.Count;
            if (solo.Count == 0) res.NoFrame = true;
        }
        order = Order();
        res.Squads = order.Count;
        bool routSeen = false, endedInMove = false, split = false;
        // 方針 f: 取っている中間拠点にいる隊の駒を手札に下げ、拠点にいる隊（空の枠でもよい）へ組む——今の規則では手札に場所が無いので、これで駒が拠点へ移る。
        void Split()
        {
            for (int i = 0; i < st.SquadCap; i++)
            {
                if (!Living(st, i) || !st.AtOutpost(i) || !st.CanReform(i) || st.FacingHunter(i)) continue;
                var alive = st.Squads[i].Units!.Where(u => u.IsAlive).OrderBy(u => u.MaxHp).ToList();
                var frames = Enumerable.Range(0, st.SquadCap).Where(j => j != i && st.Squads[j].Pos == g.Home && st.CanReform(j)).ToList();
                int room = frames.Sum(j => Enumerable.Range(0, 5).Count(sl => st.UnitAt(j, sl) is null));
                if (room == 0) { res.NoFrame = true; continue; }
                foreach (UnitState u in alive.Take(room))
                {
                    if (!st.SwapWithBench(i, u.Slot, -1)) break;
                    int j = frames.First(f => Enumerable.Range(0, 5).Any(sl => st.UnitAt(f, sl) is null));
                    int slot = new[] { 0, 1, 3, 2, 4 }.First(sl => st.UnitAt(j, sl) is null);
                    if (st.SwapWithBench(j, slot, st.Bench.IndexOf(u))) res.Moved++;
                }
            }
        }
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
            if (node.IsHero && caught && st.IsRearFight(squad, true)) caught = false;   // 追記 4 の (C): 行き先が勇者のマスだった隊は殿
            if (node.IsHero && caught) res.CaughtSquads.Add(squad);
            st.Resolve(squad, q.Node, r.PlayerWon, intercept, r.Turns);
            if (node.IsHero) res.Hero.Add((100.0 * (foeHp0 - node.HpNow) / node.DefMaxHp, alive0, hpIn, r.PlayerWon, !caught));
            else if (!rout) res.Front.Add((alive0 == 5, wins, r.PlayerWon, node.Def.Name));
            if (node.IsHero && caught && squad == deep) res.DeepCaught = true;
            if (node.IsHero && squad == rear && alive1 == 0) res.RearDied = true;
            if (node.IsHero && solo.Contains(squad)) { res.SoloFights++; if (!caught && r.Turns >= st.Stage.Hero.HoldTurns) res.SoloHolds++; }
            log?.Invoke($"    {st.Turn + (caught ? 0 : 1)} 刻目 ・ 隊 {squad + 1}（{Cell(node.Pos)}）対 {node.Def.Name}{(node.IsHero ? (caught ? "［追いつかれた］" : "［殿］") : intercept ? "［迎撃］" : "")}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T・{alive0} 枚 {hpIn:F0}% → {alive1} 枚"
                + (alive1 < alive0 ? $"・倒れた {string.Join("・", standing.Where(u => !u.IsAlive).Select(u => $"{u.Def.Name}（{u.LastDeathTurn}T）"))}" : "")
                + (node.IsHero ? $"・勇者の HP {node.HpNow}／{node.DefMaxHp}{(node.Retreated ? "（退いた）" : "")}" : ""));
            TakeRelics();
        }

        Dest Toward(int i, int cell)
        {
            if (cell == g.Portal) return Dest.Portal(g, st.Squads[i].Road >= 0 ? st.Squads[i].Road : 1);   // 追記 4: 行き先を勇者のマスにする（門へは入れないので手前で待つことになる）
            if (cell == g.Home) return Dest.Of(g, st.Squads[i].Pos == g.Home ? g.Lanes[1][0] : st.Squads[i].Pos);
            return Dest.Of(g, cell);
        }

        while (!st.Finished && st.Turn < 80 && !st.Fallen && !st.TimeUp)
        {
            if (st.Rout && !routSeen)
            {
                routSeen = true;
                var outside = Enumerable.Range(0, st.Squads.Length).Where(i => Living(st, i) && st.Squads[i].Pos != g.Home).ToList();
                res.Out = outside.Count;
                if (outside.Count > 0) deep = outside.OrderByDescending(StepsHome).ThenBy(i => i).First();
                if (flee == Flee.Rear && outside.Count >= 2) { rear = outside.OrderBy(StepsHome).ThenBy(i => i).First(); res.RearSent = true; }
                if (flee == Flee.RearDeep && outside.Count >= 2) { rear = deep; res.RearSent = true; }
            }
            if (st.Rout && flee == Flee.Bench) { split = true; Split(); }
            for (int i = 0; i < st.SquadCap && !st.Finished; i++)
            {
                if (!Living(st, i)) continue;
                Dest dest;
                if (!st.Rout && solo.Contains(i))
                {
                    int lead = order.Count > 0 ? order[0] : -1;
                    if (lead < 0 || st.Squads[lead].Pos == g.Home) continue;
                    int to = st.Squads[lead].Pos;
                    int next = st.Squads[i].Pos == g.Home ? g.Lanes[st.Squads[lead].Road][0] : g.NextStep(st.Squads[i].Pos, st.Squads[i].Road, to, -1);
                    bool foeThere = Enumerable.Range(0, st.Stage.LaneCount).Any(r => st.FoesOn(r).Any(n => n.Pos == next));
                    if (foeThere || st.Squads[i].Pos == to) continue;
                    dest = Dest.Of(g, to);
                    if (st.Squads[i].Pos == g.Home) { st.Advance(i, st.Squads[lead].Road); continue; }
                }
                else if (!st.Rout)
                {
                    int k = order.IndexOf(i);
                    dest = k >= 0 && k < targets.Length ? Dest.Of(g, targets[k]) : Dest.Home;
                    // 追記 4 の (イ): 門が開いたら、外にいるいちばん枚数の多い隊は野営地へ向かう（人はそうする）。
                    if (st.HunterPending && i == order.FirstOrDefault(j => Living(st, j) && st.Squads[j].Pos != g.Home, -1) && st.Squads[i].Road >= 0) dest = Dest.Portal(g, st.Squads[i].Road);
                    if (rests[i] < 4 && (st.AtOutpost(i) || st.Squads[i].Pos == g.Home) && HpPct(st, i) < 60 && st.FoeFacing(i) is null && st.Rest(i)) { rests[i]++; continue; }
                }
                else
                {
                    bool strike = flee == Flee.Strike || (flee is Flee.Rear or Flee.RearDeep && i == rear) || solo.Contains(i);
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
        res.LostBefore = res.Fell.Count(f => !f.Rout); res.TimeUp = st.TimeUp;
        res.StartUnits = st.HunterStartUnits; res.Survivors = st.HunterSurvivors; res.Catches = st.HunterCatches; res.Aimed = st.HunterAimed; res.Holds = st.HunterHolds;
        res.DeepHome = deep >= 0 && Living(st, deep) && (st.Squads[deep].Pos == g.Home || st.HunterDriven);
        log?.Invoke($"  → {(st.HunterDriven ? "勇者を退かせた" : st.HunterEscaped ? "生還" : st.HunterLost ? "全滅" : st.Fallen ? "陥落" : st.TimeUp ? "時間切れ" : "終わり")}・生還 {res.Survivors}／{res.StartUnits} 枚・金 {st.Gold}");
        return res;
    }

    // ---------------- 門 ----------------

    public static bool Run(Action<string> write, string demoDir, bool bots = true, bool sweep = false)
    {
        if (Environment.GetEnvironmentVariable("HUNTER_TRACE") is { Length: > 0 } tr)
        {
            // 追記 4 の調べる口: HUNTER_TRACE=<候補の番号>,<方針>,<seed>,<組 B なら 1> で 1 通しのログを出す。
            string[] p = tr.Split(',');
            Cand c = SweepCands().ElementAt(int.Parse(p[0]));
            int seed = int.Parse(p[2]);
            if (Bot(seed, p.Length > 3 && p[3] == "1") is { } pack) Play(StageOf(c, ChapterRule.V480), pack, seed, Enum.Parse<Flee>(p[1]), log: write);
            return true;
        }
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
        void Lose(Map11State st, int squad, Map11State.Node node, int turns = 5)
        {
            var prep = st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Players) u.Hp = 0;
            st.Resolve(squad, node, false, false, turns);
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

        // ---- (a) 盤の形・前半の敵と倍率 ----
        {
            MapGraph g = s.Board(true);
            int fort = g.Lanes[0][4], bridge = g.Lanes[1][4], village = g.Lanes[2][3], s4 = g.Lanes[2][4];
            bool shape = g.Lanes.Count == 3 && g.Lanes[0].Length == 8 && g.Lanes[1].Length == 7 && g.Lanes[2].Length == 8
                         && g.Lanes.All(l => l[0] == g.Lanes[0][0]) && g[g.Lanes[0][0]].Lane < 0
                         && g.IsOutpost(fort) && g.IsOutpost(bridge) && g.IsOutpost(village) && g.Cells.Count(c => c.Kind == MapGraph.Kind.Outpost) == 3
                         && g[fort].Neighbors.Contains(bridge) && g[bridge].Neighbors.Contains(s4) && g.Edges.Count(e => g.IsCross(e.A, e.B)) == 2
                         && g[g.Home].Name == MapGraph.Home13F480 && g[g.Portal].Name == MapGraph.Camp13F480;
            MapFoeScale sc = rule.ScaleOf(2);
            UnitDef hero = s.Hero.Unit!;
            bool rules = s.Hero.Hunter && s.Hero.Steps == 2 && s.Hero.FallbackTurn == 0 && s.Hero.RoutPercent == 50 && !s.Hero.Chaser && s.PortalGate == 2 && s.TimeLimit && s.TurnCap == 12 && s.Hero.HoldTurns == 4 && !s.Reserve.On
                         && s.Portal.SpawnEvery == 0 && s.EncounterRetreat && s.WipedReforms && !sc.HeroMends && !hero.Traits.Contains(TraitId.BossMend300)
                         && hero.MaxHp == MapFoeScale.Mul(EnemyCatalog.HeroMap.MaxHp, MapStage.Stage13Ch480HeroHp) && hero.Attack == MapFoeScale.Mul(EnemyCatalog.HeroMap.Attack, MapStage.Stage13Ch480HeroAtk)
                         && MapStage.Stage13Ch480HeroHp == 800 && MapStage.Stage13Ch480HeroAtk == 250
                         && sc.FoePercent == ChapterRule.Stage13Ch480FoePercent && sc.HeroPercent == ChapterRule.Stage13Ch480HeroPercent;
            bool foes = s.Lanes.SelectMany(l => l).Where(n => !n.Stationary).All(n => n.Hold) && s.Lanes[1][^1].Stationary
                        && s.Lanes.SelectMany(l => l).Count(n => n.Name is "雷杖の部隊" or "槍兵の部隊") == 2
                        && new[] { fort, bridge, village }.All(c => Enumerable.Range(0, 3).Sum(l => g.FoeStarts[l].Count(x => x == c)) == MapStage.Stage13Ch480Stack) && MapStage.Stage13Ch480Stack == 2;
            write($"  (a) 経路 {string.Join(" ／ ", g.Lanes.Select((l, i) => $"{g.LaneNames[i]} {l.Length - 1} マス"))}・中間拠点 {Cell0(g, fort)}・{Cell0(g, bridge)}・{Cell0(g, village)}・横の辺 {string.Join("・", g.Edges.Where(e => g.IsCross(e.A, e.B)).Select(e => $"{g[e.A].Name} ⇔ {g[e.B].Name}"))}");
            write($"      敵 {string.Join(" ／ ", s.Lanes.Select((l, i) => $"{s.LaneMark(i)}: {string.Join("・", l.Select(n => n.Name))}"))}・歩いてくる湧き {(s.Portal.SpawnEvery > 0 ? "あり" : "なし")}・期限 {s.TurnCap} 刻");
            write($"      敵の倍率 ×{sc.FoePercent / 100.0:0.##}・勇者 HP {hero.MaxHp}（×{MapStage.Stage13Ch480HeroHp / 100.0:0.##}） 攻 {hero.Attack}（×{MapStage.Stage13Ch480HeroAtk / 100.0:0.##}）・{s.Hero.Steps} 歩・HP {s.Hero.RoutPercent}% で退く");
            Req(shape, "(a) 盤: 北 7 ／ 中 6 ／ 南 7 マス・中間拠点 3（北 4 ／ 中 4 ／ 南 3 マス目）・横の辺 2 本（砦跡 ⇔ 橋・橋 ⇔ 南4）");
            Req(rules && foes, "(a) 前半の敵は待ち構える（中間拠点に 2 部隊ずつ・雷杖と槍兵は 1 部隊ずつ）・歩いてくる湧きと予備隊は無い・勇者は HP ×8 ／ 攻 ×2.5・期限 12 刻・足止めは 4 ターン以上");
        }

        // ---- (b) 現れる ／ (c) 目標が替わる ----
        {
            var st = Fresh(s, 0);
            var g = st.Graph;
            int bridge = g.Lanes[1][4], village = g.Lanes[2][3];
            Walk(st, 0, Dest.Of(g, bridge));
            Walk(st, 1, Dest.Of(g, village));
            bool before = st.Hero is null && st.GateOpen && !st.Rout && !st.CanEnter(0, g.Portal);
            Tick(st);
            var h = st.Hero;
            write($"  (b) 第1隊 谷の橋・第2隊 森の集落 → 門 {(st.GateOpen ? "開" : "閉")}（刻 {st.GateOpenedTurn}）→ 勇者 {(h is null ? "なし" : Cell(st, h.Pos))}・現れた刻 {st.HeroTurn}・撤退の局面 {st.Rout}");
            Req(before && h is not null && h.Pos == g.Portal && st.HeroTurn == st.GateOpenedTurn && st.HeroTurn == 1 && st.Rout,
                "(b) 門が開いた刻の終わりに、勇者が野営地（開いた門）のマスに立つ。現れた刻は動かない。開いた刻のうちに野営地へは入れない");

            int spawns0 = st.Spawns.Count(n => !n.IsHero);
            var pos0 = st.Nodes.SelectMany(r => r).Concat(st.Spawns).Where(n => !n.Cleared && !n.IsHero).Select(n => (n, n.Pos)).ToList();
            bool trade = st.CanTrade, enter = st.CanEnter(0, g.Portal) || st.CanEnter(1, g.Portal), limit = st.TimeLimitLeft is not null;
            var m1 = Tick(st);
            int hp1 = h!.Pos;
            var m2 = Tick(st);
            int hp2 = h.Pos;
            write($"  (c) 登場の後: 金を動かせる {trade}・野営地へ入れる {enter}・期限の札 {limit}・湧いた数 {spawns0} → {st.Spawns.Count(n => !n.IsHero)}・敵の位置は同じ {pos0.All(x => x.n.Pos == x.Pos)}・制圧 {st.Captured}・時間切れ {st.TimeUp}");
            Req(!trade && !enter && !limit && st.Spawns.Count(n => !n.IsHero) == spawns0 && pos0.All(x => x.n.Pos == x.Pos) && !st.Captured && pos0.Count >= 3 && !st.TimeUp,
                "(c) 登場で撤退の局面になる: 野営地へは入れない（制圧できない）・呼ぶ ／ 呼び戻すは閉じる・湧かない・残りの敵は動かない・期限は外れる");
            write($"  (d) 勇者 野営地 → {Cell(st, hp1)}（2 歩・接敵 {m1.Count}）→ {Cell(st, hp2)}（接敵 {string.Join("・", m2.Select(e => $"隊 {e.Squad + 1}"))}）・引き返せる {st.EncounterRetreat(0)}・引く先 {st.RetreatCellOf(0, false)}");
            Req(hp1 == g.Lanes[1][5] && m1.Count == 0 && hp2 == bridge && m2.Count == 1 && m2[0].Squad == 0 && st.FacingHunter(0) && !st.EncounterRetreat(0) && st.RetreatCellOf(0, false) == -1 && st.Squads[0].Pos == bridge,
                "(d) 勇者は 1 刻に 2 歩でいちばん近い隊（橋・3 歩）に 2 刻目で追いつく。勇者との会戦に「引き返す」は無い");
        }

        // ---- (b) 門が開かなければ時間切れ ----
        {
            var st = Fresh(s);
            for (int k = 0; k < 11; k++) Tick(st);
            bool open9 = !st.Finished && st.TimeLimitLeft == 1;
            Tick(st);
            write($"  (b) 隊を出さない: 11 刻目の終わり 続く {open9} → 12 刻目の終わり 勇者 {(st.Hero is null ? "なし" : "あり")}・時間切れ {st.TimeUp}・勝ち {st.Won}・陥落 {st.Fallen}");
            Req(open9 && st.Hero is null && st.TimeUp && st.Finished && !st.Won && !st.Fallen, "(b) 12 刻のうちに門が開かなければ時間切れで負け（勇者は現れない・拠点は落ちない）");
        }

        // ---- (d) 2 歩・いちばん近い隊・殿・横の辺・拠点へは入らない ----
        {
            var st = Fresh(s);
            var g = st.Graph;
            int fort = g.Lanes[0][4], n6 = g.Lanes[0][6], bridge = g.Lanes[1][4], s4 = g.Lanes[2][4], village = g.Lanes[2][3];
            Walk(st, 0, Dest.Of(g, fort));
            Walk(st, 1, Dest.Of(g, village));
            Tick(st);
            var h = st.Hero!;
            var c0 = st.HunterChase();
            Tick(st);
            bool two = h.Pos == n6 && c0 is { Squad: 0, Steps: 4 };
            // 殿: 第1隊が自分から勇者のマス（北6）へ入り、倒れる。その刻の終わり、勇者は動かない。
            Map11State.Node? met = null;
            for (int k = 0; k < 3 && met is null; k++) met = Map11Orders.Apply(st, 0, Dest.Portal(g, 0), Map11Orders.Plan(st, 0, Dest.Portal(g, 0)));
            bool rearMeet = ReferenceEquals(met, h);
            if (rearMeet) Lose(st, 0, h);
            var mHold = Tick(st);
            bool held = h.Pos == n6 && mHold.Count == 0 && st.RearGuards.SetEquals(new[] { 0 }) && st.HunterCatches == 0;
            // 次の刻から: 北6 → 北5 → 砦跡 ／ → 橋（横の辺）→ 南4（横の辺）／ → 集落（追いつく）。
            Tick(st);
            int p2 = h.Pos;
            Tick(st);
            int p3 = h.Pos;
            var m4 = Tick(st);
            write($"  (d) 第1隊 砦跡（4 歩）・第2隊 集落（5 歩）: 追う相手 隊 {(c0?.Squad ?? -1) + 1} → {Cell(st, n6)} ／ 殿（隊 1 が入る {rearMeet}）の刻は {(held ? "動かない" : "動いた")} → {Cell(st, p2)} → {Cell(st, p3)} → {Cell(st, h.Pos)}（接敵 {m4.Count}）");
            Req(two, "(d) 勇者は 1 刻に 2 歩、いちばん近い隊を追う（野営地 → 北7 → 北6）");
            Req(rearMeet && held && st.HunterHolds == 1, "(d) 隊が自分から勇者のマスへ入り、戦闘が 3 ターン以上続いた刻（殿）は、勇者は動かない");
            {
                // 3 ターンで終わった殿では止まらない（勇者はその刻も歩く）・撤退の局面では手札との出し入れが閉じる。
                var x = Fresh(s);
                Walk(x, 0, Dest.Of(g, fort));
                Walk(x, 1, Dest.Of(g, village));
                bool benchBefore = x.SwapWithBench(1, x.Squads[1].Units![0].Slot, -1);
                if (benchBefore) x.SwapWithBench(1, 0, x.Bench.Count - 1);
                Tick(x);
                Tick(x);
                var hx = x.Hero!;
                Map11State.Node? mx = null;
                for (int k = 0; k < 3 && mx is null; k++) mx = Map11Orders.Apply(x, 0, Dest.Portal(g, 0), Map11Orders.Plan(x, 0, Dest.Portal(g, 0)));
                if (ReferenceEquals(mx, hx)) Lose(x, 0, hx, turns: 3);
                int before = hx.Pos;
                Tick(x);
                int slot0 = x.Squads[1].Units![0].Slot;
                bool benchShut = x.BenchClosed && !x.SwapWithBench(1, slot0, -1) && x.CanReform(1) && x.Squads[1].Units!.Count == 5;
                write($"  (d) 3 ターンで終わった殿: 勇者 {Cell(x, before)} → {Cell(x, hx.Pos)}（止めた {x.HunterHolds}・止められなかった {x.HunterSlips}） ／ 手札: 前半は出し入れできる {benchBefore}・撤退の局面では閉じる {benchShut}（組み直しの窓は開く {x.CanReform(1)}）");
                Req(ReferenceEquals(mx, hx) && hx.Pos != before && x.HunterHolds == 0 && x.HunterSlips == 1 && x.RearGuards.Contains(0), "(d) 4 ターン未満で終わった戦闘では止まらない（勇者はその刻も歩く）");
                Req(benchBefore && benchShut, "(h) 撤退の局面では手札との出し入れが閉じる（中間拠点で下げて拠点で組めない・席の入れ替えと休むは残る）");
            }
            Req(p2 == fort && p3 == s4 && h.Pos == village && m4.Count == 1 && m4[0].Squad == 1, "(d) 横の辺を使って次の隊を追う（砦跡 → 橋 → 南4 → 集落）");
            // ---- 追記 4 の (C): その刻の行き先が勇者のマスだった隊は、勇者の歩みで当たっても「自分から入った」（殿）----
            {
                int n5 = g.Lanes[0][5];
                (Map11State X, Map11State.Node H, List<Map11State.Encounter> M) Meet(bool aim)
                {
                    var x = Fresh(s);
                    Walk(x, 0, Dest.Of(g, n5));
                    Walk(x, 1, Dest.Of(g, village));
                    Tick(x);   // 門が開く → 勇者が野営地に立つ（第1隊の北5 まで 3 歩）
                    var hx = x.Hero!;
                    var m = new List<Map11State.Encounter>();
                    for (int k = 0; k < 6 && m.Count == 0; k++)
                    {
                        Dest d = aim ? (hx.Pos == g.Portal ? Dest.Portal(g, 0) : Dest.Of(g, hx.Pos)) : Dest.Home;
                        Map11Orders.Apply(x, 0, d, Map11Orders.Plan(x, 0, d));
                        m = Tick(x);
                    }
                    return (x, hx, m);
                }
                var aimed = Meet(true);
                bool rearSeen = aimed.M.Count == 1 && aimed.M[0].Squad == 0 && aimed.X.IsRearFight(0, true);
                if (aimed.M.Count == 1) Lose(aimed.X, 0, aimed.H, turns: 5);
                int posA = aimed.H.Pos;
                var after = Tick(aimed.X);
                write($"  (C) 行き先が勇者のマスの隊に、勇者の番で当たった: 殿として数える {rearSeen}・足を止めた {aimed.X.HunterHolds}・追いつかれた {aimed.X.HunterCatches}・次の刻の勇者 {Cell(aimed.X, posA)} → {Cell(aimed.X, aimed.H.Pos)}");
                Req(rearSeen && aimed.X.HunterHolds == 1 && aimed.X.HunterCatches == 0 && aimed.X.HunterAimed == 1 && aimed.X.RearGuards.Contains(0) && aimed.H.Pos == posA,
                    "(C) その刻の行き先が勇者のマスだった隊は、勇者の番に当たっても殿（4 ターン以上続けば次の歩みを止める）");
                var fled = Meet(false);
                bool caughtSeen = fled.M.Count == 1 && !fled.X.IsRearFight(fled.M[0].Squad, true);
                if (fled.M.Count == 1) Lose(fled.X, fled.M[0].Squad, fled.H, turns: 5);
                int posF = fled.H.Pos;
                Tick(fled.X);
                write($"  (C) 逃げている隊が追いつかれた: 殿ではない {caughtSeen}・足を止めた {fled.X.HunterHolds}・追いつかれた {fled.X.HunterCatches}・次の刻の勇者 {Cell(fled.X, posF)} → {Cell(fled.X, fled.H.Pos)}");
                Req(caughtSeen && fled.X.HunterHolds == 0 && fled.X.HunterCatches == 1 && fled.H.Pos != posF, "(C) 逃げている隊が追いつかれた戦闘は今のまま（何ターン続いても足は止まらない）");
            }
            // ---- 追記 4 の (イ) ／ (ウ): 測りの候補の規則（遊ぶ盤は (ア) のまま）----
            {
                MapStage sDeep = StageOf(Now with { Deep = true }, rule), sSlow = StageOf(Now with { Deep = true, First = 1 }, rule);
                var x = Fresh(sDeep);
                Walk(x, 0, Dest.Of(g, bridge));
                Walk(x, 1, Dest.Of(g, village));
                Tick(x);
                bool wait = x.Hero is null && x.GateOpen && x.HunterPending && !x.Rout && x.TimeLimitLeft is null && !x.TimeUp;
                Map11Orders.Apply(x, 0, Dest.Portal(g, 1), Map11Orders.Plan(x, 0, Dest.Portal(g, 1)));
                bool deepNow = x.BeyondOutpost(x.Squads[0].Pos);
                Tick(x);
                bool came = x.Hero is { } && x.HeroTurn == x.GateOpenedTurn + 1 && x.Rout;
                var y = Fresh(sDeep);
                Walk(y, 0, Dest.Of(g, bridge));
                Walk(y, 1, Dest.Of(g, village));
                Tick(y); Tick(y); Tick(y);
                bool notYet = y.Hero is null;
                Tick(y);
                bool late = y.Hero is { } && y.HeroTurn == y.GateOpenedTurn + 3;
                var z = Fresh(sSlow);
                Walk(z, 0, Dest.Of(g, bridge));
                Walk(z, 1, Dest.Of(g, village));
                Tick(z); Tick(z); Tick(z); Tick(z);
                int next1 = z.HunterStepsNext;
                Tick(z);
                bool slow = z.Hero is { } hz && hz.Pos == g.Lanes[1][6] && next1 == 1 && z.HunterStepsNext == 2;
                write($"  (イ) 門が開いた刻は現れない {wait}・中間拠点より奥へ入った刻の終わりに現れる {deepNow && came}・誰も入らなければ 3 刻後 {notYet && late} ／ (ウ) 初めて歩く刻は 1 歩 {slow}");
                Req(wait && deepNow && came && notYet && late, "(イ) の候補: 門が開いた後、中間拠点より奥へ入った刻の終わり（3 刻たっても誰も入らなければその刻の終わり）・待つあいだ期限は外れる");
                Req(slow, "(ウ) の候補: 勇者が初めて歩く刻は 1 歩");
            }
            if (m4.Count == 1) Lose(st, 1, h);
            Req(st.HunterCatches == 1 && st.HunterLost && st.Finished && !st.Won, "(e) 負け: 隊に生きている駒が 1 枚もいない");

            // 同じ距離なら枚数の多い隊。
            var st2 = Fresh(s);
            Walk(st2, 0, Dest.Of(g, fort));
            Walk(st2, 1, Dest.Of(g, s4));
            Tick(st2);
            var even = st2.HunterChase();
            st2.Squads[0].Units![0].Hp = 0;
            var fewer = st2.HunterChase();
            write($"  (d) 砦跡と南4（どちらも 4 歩）: 同じ枚数なら 隊 {(even?.Squad ?? -1) + 1}・第1隊が 1 枚減ると 隊 {(fewer?.Squad ?? -1) + 1}");
            Req(even is { Squad: 0, Steps: 4 } && fewer is { Squad: 1, Steps: 4 }, "(d) 同じ距離なら生きている駒の多い隊（それも同じなら若い番号）");

            // 勝ち A・拠点へは入らない。
            var st3 = Fresh(s);
            Walk(st3, 0, Dest.Of(g, bridge));
            Walk(st3, 1, Dest.Of(g, village));
            Tick(st3);
            int gold0 = st3.Gold;
            for (int k = 0; k < 9 && st3.Squads[0].Pos != g.Home; k++) st3.StepBack(0);
            bool mid = !st3.Won && st3.HunterChase() is { Squad: 1 };
            for (int k = 0; k < 9 && st3.Squads[1].Pos != g.Home; k++) st3.StepBack(1);
            write($"  (e) 2 隊とも拠点へ: 途中 勝ち {!mid}・着いた後 勝ち {st3.Won}（生還 {st3.HunterSurvivors} 枚・金 {gold0} → {st3.Gold}）・追う相手 {(st3.HunterChase() is null ? "なし" : "あり")}");
            Req(mid && st3.Won && st3.HunterEscaped && st3.Finished && st3.HunterSurvivors == 10 && st3.Gold == gold0 + 10 && st3.HunterChase() is null && st3.Hero!.Pos != g.Home,
                "(e) 勝ち A: 生きている隊がすべて拠点に入った（生還 1 枚につき ＋1）・勇者は拠点へ入らない");

            // 勝ち B: 勇者の HP が半分を割る ／ 0 になっても「退いた」。
            (bool Still, bool Retreated, bool Won, int Gold, string Why) Drive(int toPercent)
            {
                var x = Fresh(s);
                Walk(x, 0, Dest.Of(g, bridge));
                Walk(x, 1, Dest.Of(g, village));
                Tick(x);
                List<Map11State.Encounter> m = new();
                for (int k = 0; k < 4 && m.Count == 0; k++) m = Tick(x);
                var hx = x.Hero!;
                Cut(x, m[0].Squad, hx, 60);
                bool still = !hx.Cleared && !x.Won;
                int gold = x.Gold;
                var mm = Tick(x);
                if (mm.Count > 0) { if (toPercent > 0) Cut(x, mm[0].Squad, hx, toPercent); else Win(x, mm[0].Squad, hx); }
                return (still, hx.Cleared && hx.Retreated, x.Won && x.HunterDriven && x.Finished, x.Gold - gold, x.GoldLog.Count > 0 ? string.Join("／", x.GoldLog.TakeLast(2).Select(l => l.Reason)) : "");
            }
            var half = Drive(49);
            var zero = Drive(0);
            write($"  (e) 勇者の HP を 60% → {(half.Still ? "続く" : "終わる")} ／ 49% → 退いた {half.Retreated}・勝ち {half.Won}・金 ＋{half.Gold} ／ 0 → 退いた {zero.Retreated}・勝ち {zero.Won}・金 ＋{zero.Gold}（金の文「{zero.Why}」）");
            Req(half.Still && half.Retreated && half.Won && half.Gold == 30, "(e) 勝ち B: 勇者の HP が半分を割った刻に退く（その刻で勝ち・＋20 と生還の分）");
            Req(zero.Retreated && zero.Won && zero.Gold == 30 && !zero.Why.Contains("抜いた") && zero.Why.Contains("退かせた"), "(e) 勇者の HP が 0 になった戦闘も「退いた」（「抜いた」の文は出ない）");
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

    internal sealed record Cand(string Label, int Foe, int HeroHp, int HeroAtk, int Stack = MapStage.Stage13Ch480Stack, int Steps = 2,
                                int Rout = 50, int[]? Len = null, int[]? Op = null, int Hold = 4, bool SoloWeak = false,
                                bool Deep = MapStage.Stage13Ch480DeepAppear, int First = MapStage.Stage13Ch480FirstSteps, bool Aim = true);

    /// <summary>今の定数の候補。</summary>
    internal static readonly Cand Now = new("今", ChapterRule.Stage13Ch480FoePercent, MapStage.Stage13Ch480HeroHp, MapStage.Stage13Ch480HeroAtk);

    /// <summary>振る候補（`--chapter1-hunter-sweep`・「今」のまわり）。</summary>
    internal static IEnumerable<Cand> SweepCands()
    {
        // 追記 4: 勇者の現れるきっかけ（殿は (C)）。
        yield return new("(ア) 門が開いた刻の終わり", Now.Foe, Now.HeroHp, Now.HeroAtk, Deep: false, First: 0);
        yield return new("(イ) 中間拠点より奥へ入った刻の終わり", Now.Foe, Now.HeroHp, Now.HeroAtk, Deep: true, First: 0);
        yield return new("(ウ) (イ) ＋ 1 刻目は 1 歩", Now.Foe, Now.HeroHp, Now.HeroAtk, Deep: true, First: 1);
    }

    internal static MapStage StageOf(Cand c, ChapterRule rule, int fallback = 0)
        => Dress(MapStage.Stage13Ch480With(steps: c.Steps, fallback: fallback, routPercent: c.Rout, holdTurns: c.Hold, heroHp: c.HeroHp, heroAtk: c.HeroAtk, stack: c.Stack, len: c.Len, op: c.Op, aim: c.Aim, deepAppear: c.Deep, firstSteps: c.First),
                 rule, new MapFoeScale(c.Foe, 100, HeroMends: false));

    private static bool Measure(Action<string> write, ChapterRule rule, string demoDir, bool sweep)
    {
        var dropped = new List<string>();
        MapStage now = StageOf(Now, rule);
        List<Team> humans = Humans(demoDir, now, dropped);
        var botB = Enumerable.Range(0, Seeds).Select(seed => (Pack: Bot(seed, true), Seed: seed)).Where(x => x.Pack is not null).Select(x => (x.Pack!, x.Seed)).ToList();
        var botA = Enumerable.Range(0, Seeds).Select(seed => (Pack: Bot(seed, false), Seed: seed)).Where(x => x.Pack is not null).Select(x => (x.Pack!, x.Seed)).ToList();
        write("");
        write($"## §4 の測り（{Seeds} seed）");
        write($"- 器具の組 B ／ 組 A（今の加入の表と金の規則で 1-1 → 1-2 を器具が通して 1-3 に入れた隊）: {botB.Count}／{Seeds} ・ {botA.Count}／{Seeds} seed");
        write($"- ポンの写し（1-3 の始め・参考）: {humans.Count} 本 {string.Join("・", humans.Select(h => h.Label))}" + (dropped.Count > 0 ? $"（外した {dropped.Count}: {string.Join(" ／ ", dropped.Select(d => d.Split(':')[0]))}）" : ""));
        // Judge: 1 ＝ 線を全部数える ／ 2 ＝ 「殿を出すと a より多く帰れる」の線だけ数える（追記 4・写し 752749）／ 0 ＝ 参考。
        var groups = new (string Name, int Judge, bool B, List<(Map11State.CarryPack Pack, int Seed)> Runs)[]
        {
            ("組 B", 1, true, botB),
            ("組 A", 1, false, botA),
            ("写し 752749", 2, false, humans.Where(h => h.Label.StartsWith("752749", StringComparison.Ordinal)).SelectMany(h => Enumerable.Range(0, Seeds).Select(seed => (h.Pack, seed))).ToList()),
            ("ポンの写し（参考）", 0, false, humans.SelectMany(h => Enumerable.Range(0, Seeds / Math.Max(1, humans.Count)).Select(seed => (h.Pack, seed))).ToList()),
        };
        bool all = true;
        if (sweep)
        {
            write("");
            write("| 候補 | 組 | 現れる刻（時間切れ） | 登場から終わりまで a ／ b ／ g（中央値） | a で 2 隊以上が追いつかれる | 生還した駒（平均） a | b 殿 1 隊（手前の隊） | g 殿 1 隊（奥の隊） | e 1 枚の殿 | 殿が足を止めた回数 b ／ g（1 通しあたり） | a 勝ち | a 奥が追いつかれる | b 奥が生還 | c 退かせる | 線 | 落ちた線 |");
            write("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        }
        else
        {
            foreach (var (name, _, _, runs) in groups)
            {
                if (runs.Count == 0) continue;
                write($"--- {name} の通し（方針 e・seed {runs[0].Seed}）");
                Play(now, runs[0].Pack, runs[0].Seed, Flee.Solo, log: write);
            }
            foreach (var w in Weigh(Now, rule, humans.Select(h => h.Pack).ToList()))
                write($"--- 勇者の重さ（ポンの写し）: {w.Names} → 勝ち {w.Wins}／{WeighSeeds}・削り 中央値 {w.Cut:0.#}% 最大 {w.Max:0.#}%");
            Stand(write, rule, now, botB.Select(x => x.Item1).ToList(), botA.Select(x => x.Item1).ToList(), humans);
        }
        foreach (Cand c in sweep ? SweepCands() : new[] { Now })
        {
            MapStage st = StageOf(c, rule);
            if (!sweep)
            {
                write("");
                write($"### 「{c.Label}」: 敵 ×{c.Foe / 100.0:0.##}・勇者 HP ×{c.HeroHp / 100.0:0.##} 攻 ×{c.HeroAtk / 100.0:0.##}・中間拠点に {c.Stack} 部隊・{c.Steps} 歩・HP {c.Rout}% で退く");
            }
            foreach (var (name, judge, isB, runs) in groups)
            {
                if (runs.Count == 0) { write($"- {name}: 隊なし"); continue; }
                var (pass, rearOk) = Lines(write, name, st, c, rule, runs, sweep, isB);
                if (judge == 1) all &= pass;
                if (judge == 2) all &= rearOk;
            }
        }
        return all;
    }

    /// <summary>1 候補 × 1 組の表と線（線はポンの直し 2026-11-17・「（組 B）」の線は <paramref name="isB"/> のときだけ数える）。</summary>
    private static (bool All, bool Rear) Lines(Action<string> write, string group, MapStage st, Cand c, ChapterRule rule, List<(Map11State.CarryPack Pack, int Seed)> runs, bool compact, bool isB)
    {
        Action<string> detail = compact ? _ => { } : write;
        var by = new Dictionary<Flee, List<Res>>();
        foreach (Flee f in Enum.GetValues<Flee>())
        {
            var rs = new Res[runs.Count];
            System.Threading.Tasks.Parallel.For(0, runs.Count, i => rs[i] = Play(st, runs[i].Pack, runs[i].Seed, f, odds: f == Flee.Home && i < OddsSeeds, soloWeak: c.SoloWeak));
            by[f] = rs.ToList();
        }
        var a = by[Flee.Home];
        double lostBefore = a.Average(r => (double)r.LostBefore);
        double gate = Med(a.Where(r => r.HeroTurn >= 0).Select(r => (double)r.HeroTurn));
        int timeUp = a.Count(r => r.TimeUp), fallen = a.Count(r => r.Fallen);
        var full = a.SelectMany(r => r.Front).Where(x => x.Full && x.Odds >= 0).ToList();
        double good = full.Count == 0 ? double.NaN : 100.0 * full.Count(x => x.Odds >= 16) / full.Count;
        var frontAll = a.SelectMany(r => r.Front).ToList();
        detail($"#### {group}（{runs.Count} 通し）");
        detail("| 前半 | 値 | 線 |");
        detail("|---|---|---|");
        detail($"| 門が開くまでに失う駒（平均） | {lostBefore:0.00} | 1 枚以下（組 B） |");
        detail($"| 勇者が現れる刻（中央値） | {T(gate)}（時間切れ {timeUp}・前半に陥落 {fallen}） | 6〜8 |");
        detail($"| 5 枚の隊の戦闘が「優勢」 | {T(good)}%（{full.Count} 戦・前半の勝ち {frontAll.Count(x => x.Won)}／{frontAll.Count}） | 90% 以上 |");
        var worst = frontAll.GroupBy(x => x.Foe).Select(gp => (gp.Key, N: gp.Count(), W: gp.Count(x => x.Won))).OrderBy(x => (double)x.W / x.N).Take(3);
        detail($"- 前半で勝ちの少ない相手: {string.Join("・", worst.Select(x => $"{x.Key} {x.W}／{x.N}"))}");
        var fellBefore = a.SelectMany(r => r.Fell).Where(f => !f.Rout).GroupBy(f => (f.Unit, f.Foe)).OrderByDescending(gp => gp.Count()).Take(4);
        detail($"- 前半に倒れた駒: {string.Join("・", fellBefore.Select(gp => $"{gp.Key.Unit}（{gp.Key.Foe}・{gp.Count()} 回・平均 {gp.Average(f => f.Turn):0.#}T）").DefaultIfEmpty("なし"))}");

        var weigh = Weigh(c, rule, runs.Select(r => r.Pack).Distinct().Take(12).ToList());
        detail("");
        detail("| 勇者 | 値 | 線 |");
        detail("|---|---|---|");
        detail($"| 満タンの 5 枚の隊の勝ち（20 戦のうち・隊ごとの中央値 ／ 最大） | {T(Med(weigh.Select(w => (double)w.Wins)))} ／ {weigh.Select(w => w.Wins).DefaultIfEmpty(0).Max()} | 0〜1（組 B） |");
        detail($"| 1 戦で削る勇者の HP（隊ごとの中央値の中央値） | {T(Med(weigh.Select(w => w.Cut)))}% | 14〜25%（組 B） |");
        detail($"| いちばん削る隊（中央値 ／ 最大の 1 戦） | {T(weigh.Select(w => w.Cut).DefaultIfEmpty(0).Max())}% ／ {T(weigh.Select(w => w.Max).DefaultIfEmpty(0).Max())}% | （線なし） |");
        if (weigh.Count > 0) detail($"- いちばん削る隊: {weigh.OrderByDescending(w => w.Cut).First().Names}");

        List<Res> R(Flee f) => by[f].Where(r => r.HeroTurn >= 0).ToList();
        double Pct(List<Res> rs, Func<Res, bool> q) => rs.Count == 0 ? double.NaN : 100.0 * rs.Count(q) / rs.Count;
        double Surv(Flee f) => Med(R(f).Where(r => r.StartUnits > 0).Select(r => 100.0 * r.Survivors / r.StartUnits));
        double SurvAvg(Flee f) => R(f).Where(r => r.StartUnits > 0).Select(r => 100.0 * r.Survivors / r.StartUnits).DefaultIfEmpty(double.NaN).Average();
        detail("");
        detail("| 方針 | 勝ち | 生還 | 退かせた | 全滅 | 生還した駒（中央値 ／ 平均） | いちばん奥の隊が追いつかれた ／ 生還した | 殿が倒れた | 登場から終わりまで（中央値） |");
        detail("|---|---|---|---|---|---|---|---|---|");
        foreach (Flee f in Enum.GetValues<Flee>())
        {
            var rs = R(f);
            if (rs.Count == 0) { detail($"| {FleeName(f)} | — |"); continue; }
            var withDeep = rs.Where(r => r.Out > 0).ToList();
            var sent = rs.Where(r => r.RearSent).ToList();
            detail($"| {FleeName(f)} | {Pct(rs, r => r.Won):0}% | {Pct(rs, r => r.Escaped):0}% | {Pct(rs, r => r.Driven):0}% | {Pct(rs, r => r.Lost):0}% | {T(Surv(f))}% ／ {T(SurvAvg(f))}% "
                + $"| {T(Pct(withDeep, r => r.DeepCaught))}% ／ {T(Pct(withDeep, r => r.DeepHome))}% | {(sent.Count == 0 ? "—" : $"{100.0 * sent.Count(r => r.RearDied) / sent.Count:0}%（出した {sent.Count}）")} | {T(Med(rs.Select(r => (double)(r.EndTurn - r.HeroTurn))))} 刻 |");
        }
        foreach (Flee f in new[] { Flee.Solo, Flee.SoloRepeat })
        {
            var rs = R(f);
            var did = rs.Where(r => r.Solos > 0).ToList();
            int solos = did.Sum(r => r.Solos);
            detail($"- {FleeName(f)}: 1 枚の隊を作れた通し {did.Count}／{rs.Count}（平均 {(did.Count == 0 ? 0 : (double)solos / did.Count):0.0} 隊）・1 枚の殿が勇者を止めた刻（自分から入った戦闘）平均 {(did.Count == 0 ? 0 : did.Average(r => (double)r.SoloHolds)):0.00}・"
                + $"1 隊あたり {(solos == 0 ? 0 : (double)did.Sum(r => r.SoloHolds) / solos):0.00} 刻・作れた通しの生還した駒 平均 {(did.Count == 0 ? double.NaN : did.Where(r => r.StartUnits > 0).Average(r => 100.0 * r.Survivors / r.StartUnits)):0.0}%・いちばん奥の隊が生還 {T(Pct(did.Where(r => r.Out > 0).ToList(), r => r.DeepHome))}%");
        }
        {
            var rs = R(Flee.Bench);
            var did = rs.Where(r => r.Moved > 0).ToList();
            detail($"- {FleeName(Flee.Bench)}: 駒を移せた通し {did.Count}／{rs.Count}（拠点に組める枠が無かった {rs.Count(r => r.NoFrame)}）・移した駒 平均 {(did.Count == 0 ? 0 : did.Average(r => (double)r.Moved)):0.0} 枚・移せた通しの生還した駒 平均 {(did.Count == 0 ? double.NaN : did.Where(r => r.StartUnits > 0).Average(r => 100.0 * r.Survivors / r.StartUnits)):0.0}%");
        }
        var heroFights = by[Flee.Strike].SelectMany(r => r.Hero).ToList();
        detail($"- 方針 c の勇者との戦闘 {heroFights.Count} 回: 1 戦で削る HP の中央値 {T(Med(heroFights.Select(x => x.Cut)))}%・勝ち {heroFights.Count(x => x.Won)}");
        var fellRout = by[Flee.Home].SelectMany(r => r.Fell).Where(f => f.Rout).GroupBy(f => f.Unit).OrderByDescending(gp => gp.Count()).Take(5);
        detail($"- 方針 a で勇者に倒された駒: {string.Join("・", fellRout.Select(gp => $"{gp.Key}（{gp.Count()} 回・平均 {gp.Average(f => f.Turn):0.#}T）").DefaultIfEmpty("なし"))}");
        detail($"- 勇者が拠点へ入った通し {by.Values.SelectMany(x => x).Count(r => r.HeroEnteredHome)}・打ち切り {by.Values.SelectMany(x => x).Count(r => r.Stalled)}");

        var deepRs = R(Flee.Home).Where(r => r.Out > 0).ToList();
        double Did(Flee f) { var d = R(f).Where(r => r.Solos > 0 && r.StartUnits > 0).ToList(); return d.Count == 0 ? double.NaN : d.Average(r => 100.0 * r.Survivors / r.StartUnits); }
        double Dur(Flee f) => Med(R(f).Select(r => (double)(r.EndTurn - r.HeroTurn)));
        double dur = Dur(Flee.Home);
        // 追記 4 の線: a で追いつかれる隊が 2 隊以上の通し ／ 殿を 1 隊出したとき（b ＝ 手前の隊・g ＝ 奥の隊のどちらか良い方）生還した駒の平均が a より多い。
        double two = Pct(R(Flee.Home), r => r.CaughtSquads.Count >= 2);
        double aAvg = SurvAvg(Flee.Home), bAvg = SurvAvg(Flee.Rear), gAvg = SurvAvg(Flee.RearDeep);
        double eAvg = Did(Flee.Solo);
        bool rearOk = new[] { bAvg, gAvg, eAvg }.Where(x => !double.IsNaN(x)).DefaultIfEmpty(double.NaN).Max() > aAvg;
        double HoldsOf(Flee f) => R(f).Count == 0 ? double.NaN : R(f).Average(r => (double)r.Holds);
        detail($"- 追記 4: a で 2 隊以上が追いつかれる {T(two)}%・生還した駒（平均） a {T(aAvg)}% ／ b {T(bAvg)}% ／ g {T(gAvg)}% ／ e {T(eAvg)}%・殿が足を止めた回数 b {HoldsOf(Flee.Rear):0.00} ／ g {HoldsOf(Flee.RearDeep):0.00}（うち行き先が勇者のマスで数えた戦闘 b {R(Flee.Rear).Sum(r => r.Aimed)} ／ g {R(Flee.RearDeep).Sum(r => r.Aimed)}）・登場から終わりまで a {T(dur)} ／ b {T(Dur(Flee.Rear))} ／ g {T(Dur(Flee.RearDeep))} 刻");
        double eSurv = Did(Flee.Solo), eRep = Did(Flee.SoloRepeat);
        var lines = new List<(string Name, bool Ok)>
        {
            ("現れる刻 6〜8", gate >= 6 && gate <= 8),
            ("優勢 ≥ 90%", good >= 90),
            ("a 勝ち ≥ 90%", Pct(R(Flee.Home), r => r.Won) >= 90),
            ("a 奥の隊が追いつかれる ≥ 70%", Pct(deepRs, r => r.DeepCaught) >= 70),
            ("b 奥の隊が生還 ≥ 60%", Pct(R(Flee.Rear).Where(r => r.RearSent).ToList(), r => r.DeepHome) >= 60),
        };
        // 追記 4 の線（勇者の現れるきっかけを選ぶ線・門の合否には数えない）。追記 5（ポンの決定）:「a で 2 隊以上が追いつかれる ≥ 50%」の線は外した
        // ——器具は 2 隊しか外へ出さないので 0% から動かない。数は表に残す。きっかけは (ア) で確定・(イ)(ウ) は測りの口。
        var extra = new List<(string Name, bool Ok)>
        {
            ("登場から終わりまで 3〜5 刻", dur is >= 3 and <= 5),
            ("殿を 1 隊出すと a より多く帰れる", rearOk),
        };
        if (isB)
        {
            lines.Add(("門までに失う駒 ≤ 1", lostBefore <= 1.0));
            lines.Add(("満タン 5 枚が勝つ 0〜1／20", Med(weigh.Select(w => (double)w.Wins)) <= 1));
            lines.Add(("1 戦で削る 14〜25%", Med(weigh.Select(w => w.Cut)) is >= 14 and <= 25));
            lines.Add(("c 退かせる 10〜50%", Pct(R(Flee.Strike), r => r.Driven) is >= 10 and <= 50));
        }
        detail("");
        detail($"- 線: {string.Join(" ／ ", lines.Select(l => $"{(l.Ok ? "○" : "×")} {l.Name}"))}");
        bool eOk = double.IsNaN(eSurv) || eSurv is >= 50 and <= 85;
        lines.Add(("e 生還した駒 50〜85%", eOk));
        detail($"- 追記 4 の線（きっかけを選ぶ線・門の合否には数えない）: {string.Join(" ／ ", extra.Select(l => $"{(l.Ok ? "○" : "×")} {l.Name}"))}");
        detail($"- 線（追記 3）: e の生還した駒（1 枚の隊を作れた通しの平均）{T(eSurv)}%・枠のかぎり {T(eRep)}% → {(eOk ? "50〜85% に入る" : "外れる")}");
        if (compact)
            write($"| {c.Label} | {group} | {T(gate)}（{timeUp}） | {T(dur)} ／ {T(Dur(Flee.Rear))} ／ {T(Dur(Flee.RearDeep))} | {T(two)}% | {T(aAvg)}% | {T(bAvg)}% | {T(gAvg)}% | {T(eSurv)}% | {HoldsOf(Flee.Rear):0.00} ／ {HoldsOf(Flee.RearDeep):0.00} "
                + $"| {T(Pct(R(Flee.Home), r => r.Won))}% | {T(Pct(deepRs, r => r.DeepCaught))}% | {T(Pct(R(Flee.Rear).Where(r => r.RearSent).ToList(), r => r.DeepHome))}% | {T(Pct(R(Flee.Strike), r => r.Driven))}% "
                + $"| {lines.Concat(extra).Count(l => l.Ok)}／{lines.Count + extra.Count}{(lines.Concat(extra).All(l => l.Ok) ? " ○" : "")} | {string.Join("・", lines.Concat(extra).Where(l => !l.Ok).Select(l => l.Name).DefaultIfEmpty("—"))} |");
        return (lines.All(l => l.Ok), rearOk);
    }

    /// <summary>勇者の重さ（指示書 §4-2）: 隊ごとに、1-3 の始めの主力（いちばん枚数の多い隊・満タン）を勇者に 20 回当てる。</summary>
    internal static List<(string Names, int Wins, double Cut, double Max)> Weigh(Cand c, ChapterRule rule, List<Map11State.CarryPack> packs)
    {
        MapStage stage = StageOf(c, rule, fallback: 1);   // 測りの口: 1 刻目の終わりに勇者を立てる（重さを見るだけ）
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

    // ---------------- 追記 2 の報告: 3 ターン立てる駒・火の隊 ----------------

    private static UnitDef WithTraits(UnitDef d, params TraitId[] add) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Traits = d.Traits.Concat(add).ToArray(),
        Pattern = d.Pattern, Advances = d.Advances, Actions = d.Actions, PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    /// <summary>勇者の駒（今の盤の定義から 1 体・満タン）。</summary>
    private static List<UnitState> HeroUnits(UnitDef hero) => BattleEngine.Materialize(Formation.Build(center: hero), BattleContext.EnemyTeam);

    /// <summary>その隊を勇者に 20 回当てて、3 ターン以上（と 4 ターン以上）続いた回数・勝ち・ターン数の中央値。</summary>
    private static (int Three, int Four, int Wins, double Turns) Last(List<UnitState> squad, UnitDef hero)
    {
        int three = 0, four = 0, wins = 0;
        var turns = new List<double>();
        for (int k = 0; k < WeighSeeds; k++)
        {
            var ps = squad.Select(Map11State.CloneForOdds).ToList();
            foreach (UnitState u in ps) u.Hp = u.MaxHp;
            BattleResult r = BattleEngine.Run(ps, HeroUnits(hero), 9000 + k * 17, verbose: false);
            if (r.Turns >= 3) three++;
            if (r.Turns >= 4) four++;
            if (r.PlayerWon) wins++;
            turns.Add(r.Turns);
        }
        return (three, four, wins, Med(turns));
    }

    internal static string Mark(int n) => n >= 16 ? "足止めできる" : n <= 7 ? "足止めできない" : "五分";

    private static void Stand(Action<string> write, ChapterRule rule, MapStage stage, List<Map11State.CarryPack> packsB, List<Map11State.CarryPack> packsA, List<Team> humans)
    {
        UnitDef hero = stage.Hero.Unit!;
        write("");
        write($"## 追記 2 の報告 —— 3 ターン立てる駒（勇者 HP {hero.MaxHp}・攻 {hero.Attack}・20 戦）");
        // 1) 1 枚の隊: 今の加入の表の全駒 × ☆1〜☆3。
        var pool = stage.Roster.Start.Select(RosterRule.Find).Concat(stage.Roster.PoolFor("1-3")).GroupBy(d => d.Id).Select(gp => gp.First()).ToList();
        write($"### 1 枚の隊（加入の表の {pool.Count} 体・席は中央）");
        write("| 駒 | ☆1 最大HP | ☆1 3 ターン以上 | ☆2 3 ターン以上 | ☆3 3 ターン以上 | ☆1 4 ターン以上 | 印（☆1） |");
        write("|---|---|---|---|---|---|---|");
        var rows = new List<(string Name, int Hp, int[] Three, int Four)>();
        foreach (UnitDef d in pool)
        {
            var three = new int[4];
            int four1 = 0, hp1 = 0;
            for (int star = 1; star <= 3; star++)
            {
                UnitDef def = stage.Stars.On ? stage.Stars.Def(d, star) : d;
                var sq = BattleEngine.Materialize(Formation.Build(center: def), BattleContext.PlayerTeam);
                var l = Last(sq, hero);
                three[star] = l.Three;
                if (star == 1) { four1 = l.Four; hp1 = def.MaxHp; }
            }
            rows.Add((d.Name, hp1, three, four1));
        }
        foreach (var r in rows.OrderByDescending(r => r.Three[1]).ThenByDescending(r => r.Three[2]).ThenByDescending(r => r.Hp))
            write($"| {r.Name} | {r.Hp} | {r.Three[1]}／20 | {r.Three[2]}／20 | {r.Three[3]}／20 | {r.Four}／20 | {Mark(r.Four)} |");
        write($"- 印は 4 ターンで数える（追記 3）: ☆1 で「足止めできる」（4 ターン以上が 16 以上）{rows.Count(r => r.Four >= 16)} 体・「五分」{rows.Count(r => r.Four is > 7 and < 16)} 体・「足止めできない」{rows.Count(r => r.Four <= 7)} 体 ／ 3 ターン以上が 16 以上: ☆1 {rows.Count(r => r.Three[1] >= 16)} 体・☆2 {rows.Count(r => r.Three[2] >= 16)} 体・☆3 {rows.Count(r => r.Three[3] >= 16)} 体");

        // 2) 2 枚・3 枚・5 枚の隊（器具の組の 1-3 の始めの隊から、席の前から k 枚）。
        write("");
        write("### 2 枚・3 枚・5 枚の隊が 3 ターン以上続く割合（1-3 の始めの隊・席の順に前から k 枚・隊 × 20 戦）");
        write("| 組 | 枚数 | 3 ターン以上 | 4 ターン以上 | 「足止めできる」隊 | 「足止めできない」隊 | ターン数（中央値） |");
        write("|---|---|---|---|---|---|---|");
        foreach (var (name, packs) in new[] { ("組 B", packsB), ("組 A", packsA), ("ポンの写し（参考）", humans.Select(h => h.Pack).ToList()) })
        {
            var squads = new List<List<UnitState>>();
            foreach (var pack in packs.Distinct().Take(20))
            {
                Map11State st = Setup(stage, pack, 0);
                squads.AddRange(Enumerable.Range(0, st.Squads.Length).Where(i => st.Squads[i].Units is { Count: > 0 }).Select(i => st.PeekSquad(i).Where(u => u.IsAlive).OrderBy(u => u.Slot).ToList()));
            }
            foreach (int k in new[] { 1, 2, 3, 5 })
            {
                var take = squads.Where(q => q.Count >= k).Select(q => q.Take(k).ToList()).ToList();
                if (take.Count == 0) continue;
                var ls = take.Select(q => Last(q, hero)).ToList();
                write($"| {name} | {k} | {100.0 * ls.Sum(l => l.Three) / (ls.Count * WeighSeeds):0}% | {100.0 * ls.Sum(l => l.Four) / (ls.Count * WeighSeeds):0}% | {ls.Count(l => l.Three >= 16)}／{ls.Count} | {ls.Count(l => l.Three <= 7)}／{ls.Count} | {T(Med(ls.Select(l => l.Turns)))} |");
            }
        }

        // 3) 火の隊（120065 の主力）: 勇者に「焼かれない」札を足したら ／ 1-2 の砦の守将に何ターンで勝つか。
        Team? fire = humans.FirstOrDefault(h => h.Label.StartsWith("120065", StringComparison.Ordinal));
        if (fire is null) return;
        Map11State fs = Setup(stage, fire.Pack, 0);
        var main = fs.PeekSquad(Enumerable.Range(0, fs.Squads.Length).Where(i => fs.Squads[i].Units is { Count: > 0 }).OrderByDescending(i => fs.Squads[i].Units!.Count).First()).Where(u => u.IsAlive).ToList();
        write("");
        write($"### 火の隊（{string.Join("・", main.Select(u => u.Def.Name))}）—— 報告だけ（この期では直さない）");
        (int Wins, double Turns, double Cut) Versus(Func<List<UnitState>> foe, int foeMax)
        {
            int wins = 0;
            var turns = new List<double>();
            var cuts = new List<double>();
            for (int k = 0; k < WeighSeeds; k++)
            {
                var ps = main.Select(Map11State.CloneForOdds).ToList();
                foreach (UnitState u in ps) u.Hp = u.MaxHp;
                var es = foe();
                BattleResult r = BattleEngine.Run(ps, es, 9500 + k * 19, verbose: false);
                if (r.PlayerWon) wins++;
                turns.Add(r.Turns);
                cuts.Add(100.0 * (foeMax - es.Where(u => u.IsAlive).Sum(u => u.Hp)) / foeMax);
            }
            return (wins, Med(turns), Med(cuts));
        }
        var plain = Versus(() => HeroUnits(hero), hero.MaxHp);
        write($"- 今の勇者（{string.Join("・", hero.Traits)}）: 勝ち {plain.Wins}／20・{T(plain.Turns)} ターン・削り {T(plain.Cut)}%");
        foreach (var (label, add) in new (string, TraitId[])[]
        {
            ("火の鎧（焼かれない ＋ 殴った相手と自分に着火 ＋ 燃えている間は被ダメ半分）", new[] { TraitId.FireArmor }),
            ("火の鎧 ＋ 半減なし（焼かれない ＋ 着火だけ）", new[] { TraitId.FireArmor, TraitId.FireArmorNoGuard }),
        })
        {
            UnitDef h2 = WithTraits(hero, add);
            var v = Versus(() => HeroUnits(h2), hero.MaxHp);
            write($"- 勇者に {label} を足す: 勝ち {v.Wins}／20・{T(v.Turns)} ターン・削り {T(v.Cut)}%");
        }
        MapStage s12 = Chapter1.StagesFor(rule)[1];
        foreach (Map11.RoadNode n in s12.Lanes.SelectMany(l => l).Where(n => n.Stationary))
        {
            int max = n.Enemy.Occupied().Sum(o => o.Item2.MaxHp);
            var v = Versus(() => BattleEngine.Materialize(n.Enemy, BattleContext.EnemyTeam), max);
            write($"- 1-2 の {n.Name}（最大HP の合計 {max}）: 勝ち {v.Wins}／20・{T(v.Turns)} ターン");
        }
    }
}

