using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 第477期の門 `--chapter1-stage12-smoke`（指示書 design/PHASE477_STAGE12_BOARD_SPEC.md §5・画面なし）
//
//   (a) 盤の形（道の長さ 4 ／ 3 ／ 6・中間拠点の位置・横の辺は 1 本）
//   (b) 中間拠点ごとの見返り（レリックは見張り台・金 ＋8 ／ ＋12）
//   (c) どの 2 つを取っても門が開く。門番の勇者・連戦は今のまま
//   (d) 予備隊が隊のいない道を通る（中 → 北 → 南 の順）。関所に隊がいれば、歩いてきた敵は関所で当たる。湧きは中の街道だけ
//   (e) 単発の 1-2・第461〜476期の 1 章は今のまま
//
// §3 の測り（`--chapter1-stage12-quick` は省く）:
//   §3-1 重さ: 1-1 から持ち越した隊（第476期の通しの終わり）で、盤の敵部隊の全部に当てる（20 seed）
//   §3-2 通し: 方針 4 つ × 留守番 0 ／ 2 ／ 3 ／ 4 枚 × 組 A ／ 組 B × 100 seed と線
// **ここには Godot の型を1つも使わない。**
// =====================================================================================
public static class Chapter1Stage12Smoke
{
    private const int Seeds = 100, WeighSeeds = 20;

    internal enum Policy { MidNorth, MidSouth, NorthSouth, All }

    internal static string PolicyName(Policy p) => p switch
    {
        Policy.MidNorth => "1 中＋北",
        Policy.MidSouth => "2 中＋南",
        Policy.NorthSouth => "3 北＋南",
        _ => "4 3 つとも",
    };

    internal sealed class Result
    {
        public bool Won, Fallen, TimeUp, Relic, HeroDown;
        public int CaptureTurn = -1, EndTurn, FallTurn = -1, Lost, Gold, HeroSquads, GateLost, Units, Squads;
        /// <summary>拠点での迎撃（1 回の到着で 1 回）と、関所で歩いてきた敵を受けた回数。</summary>
        public int HomeIntercepts, GateCatches;
        public int ReserveLane = -1;
        public List<int> SpawnLanes = new();
        public List<(string Foe, int Squad, bool Won, double Hp, int Lost, int AliveIn, double HpIn, string Where, bool Walked, bool Intercept)> Fights = new();
        public List<(string Foe, string Where, string Unit, int Turn)> Fell = new();
    }

    private static readonly string[] AttackA = { "泥人形ムド", "のろまの巨兵ドルガ", "鬨の号令ガン", "墓守リィカ", "施しのリリ" };
    private static readonly string[] GuardA = { "胞子体ムグ" };
    private static readonly string[] AttackB = { "のろまの巨兵ドルガ", "鬨の号令ガン", "墓守リィカ" };
    private static readonly string[] GuardB = { "泥人形ムド", "胞子体ムグ", "施しのリリ" };
    private static readonly Dictionary<string, int> SeatOf = new()
    {
        ["泥人形ムド"] = 0, ["のろまの巨兵ドルガ"] = 1, ["鬨の号令ガン"] = 2, ["墓守リィカ"] = 3, ["施しのリリ"] = 4, ["胞子体ムグ"] = 1,
    };

    private static MapStage Dress(MapStage raw, ChapterRule rule, int index)
        => raw.Scaled(rule.ScaleOf(index)).Starred(rule.Stars ?? StarRule.Off).Rostered(rule.Roster ?? RosterRule.Off);

    private static readonly Dictionary<(int, bool), Map11State.CarryPack?> _carry = new();

    /// <summary>1-1 の終わりの持ち越し（第476期の通し・組 A は「北だけ」・組 B は「2 隊に分ける」——どちらも 100 seed 全勝の方針）。負けた通しは null。</summary>
    internal static Map11State.CarryPack? CarryOf(int chapterSeed, bool teamB)
    {
        lock (_carry)
        {
            if (_carry.TryGetValue((chapterSeed, teamB), out var hit)) return hit;
            MapStage s11 = Dress(MapStage.Stage11Ch476, ChapterRule.V477, 0);
            Map11State? last = null;
            var r = Chapter1AsymSmoke.Play(s11, chapterSeed, teamB ? Chapter1AsymSmoke.Plan.Split : Chapter1AsymSmoke.Plan.North, watch: st => last = st, teamB: teamB);
            return _carry[(chapterSeed, teamB)] = r.Won && last is not null ? last.TakeCarry() : null;
        }
    }

    private static double HpPct(Map11State st, int k) => st.Squads[k].Units is { } us && us.Sum(x => x.MaxHp) > 0
        ? 100.0 * us.Where(x => x.IsAlive).Sum(x => x.Hp) / us.Sum(x => x.MaxHp) : 0;

    /// <summary>
    /// 持ち越しを載せて隊を組む。隊 0 ＝ 主力（5 枚）・隊 1 ＝ 別働（残り・最大 5 枚）・隊 2 ＝ 留守番（<paramref name="guard"/> 枚・0 なら置かない）。
    /// 留守番を置くなら 3 隊目の枠を買い、残りの金で呼べるだけ呼ぶ（強化はしない）。<paramref name="recruit"/> が偽なら呼ばない（重さの測り）。
    /// </summary>
    internal static Map11State Setup(MapStage stage, int chapterSeed, bool teamB, int guard, bool recruit = true, int total = 0)
    {
        var st = new Map11State(Chapter1.StageSeed(chapterSeed, 1, 0), stage.Time, stage.Portal, draft: true, stage: stage);
        if (CarryOf(chapterSeed, teamB) is { } pack) st.ApplyCarry(pack);
        for (int sq = 0; sq < st.Squads.Length; sq++)
            for (int slot = 0; slot < 5; slot++)
                if (st.UnitAt(sq, slot) is not null) st.SwapWithBench(sq, slot, -1);
        if (total <= 0) total = Math.Min(10 + guard, 12);
        if (recruit)
        {
            if (guard > 0 && st.SquadCap < 3) st.BuySlot();
            // 駒は全部で 10 枚まで呼ぶ（主力 5 ＋ 別働と留守番で 5・留守番を増やせば別働が減る）。残りの金は強化へ（`Spend`）。
            for (int n = 0; n < 20 && st.Bench.Count < total && st.Recruit(0); n++) { }
        }
        string[] attack = teamB ? AttackB : AttackA, guards = teamB ? GuardB : GuardA;
        var pool = st.Bench.Select(u => u.Def.Name).ToList();
        List<string> Take(IEnumerable<string> order, int n)
        {
            var got = new List<string>();
            foreach (string name in order) { if (got.Count >= n) break; if (pool.Remove(name)) got.Add(name); }
            return got;
        }
        int gSquad = st.SquadCap >= 3 ? 2 : 1;
        // 留守番: 守りの駒 → 呼んだ駒（後ろから）→ 残り。主力: 攻めの駒 → 呼んだ駒 → 残り。
        var g = guard > 0 ? Take(guards.Concat(pool.Where(n => !attack.Contains(n)).Reverse().ToList()).Concat(pool.ToList()), guard) : new List<string>();
        var a = Take(attack.Concat(pool.Where(n => !guards.Contains(n)).ToList()).Concat(pool.ToList()), 5);
        var b = gSquad == 1 && guard > 0 ? new List<string>() : Take(pool.ToList(), 5);
        void Seat(int sq, List<string> names)
        {
            foreach (string name in names)
            {
                int bi = st.Bench.FindIndex(u => u.Def.Name == name);
                if (bi < 0) continue;
                int slot = SeatOf.TryGetValue(name, out int pref) && st.UnitAt(sq, pref) is null ? pref
                    : new[] { 0, 1, 3, 2, 4 }.Where(s => st.UnitAt(sq, s) is null).DefaultIfEmpty(-1).First();
                if (slot >= 0) st.SwapWithBench(sq, slot, bi);
            }
        }
        Seat(0, a);
        if (b.Count > 0) Seat(1, b);
        if (g.Count > 0) Seat(gSquad, g);
        if (recruit) Spend(st);
        return st;
    }

    /// <summary>金を強化に使う（主力 → 別働 → 留守番の順に、☆の低い駒から 1 枚ずつ・拠点か取った中間拠点にいる隊の駒だけ）。</summary>
    internal static void Spend(Map11State st)
    {
        // 倒れた駒を呼び戻して、組み直せる隊の空いた席へ（主力 → 別働 → 留守番）。
        for (int n = 0; n < 10 && st.FallenUnits.Count > 0 && st.Stage.Gold.Revive > 0; n++)
        {
            int sq = -1, slot = -1;
            for (int i = 0; i < st.SquadCap && sq < 0; i++)
            {
                if (st.Squads[i].Lost || st.Squads[i].Units is not { Count: > 0 } || !st.CanReform(i)) continue;
                slot = new[] { 0, 1, 3, 2, 4 }.Where(x => st.UnitAt(i, x) is null).DefaultIfEmpty(-1).First();
                if (slot >= 0) sq = i;
            }
            int idx = Enumerable.Range(0, st.FallenUnits.Count).Where(st.CanRevive).DefaultIfEmpty(-1).First();
            if (sq < 0 || idx < 0) break;
            string id = st.FallenUnits[idx].Def.Id;
            if (!st.Revive(idx)) break;
            int bi = st.Bench.FindIndex(u => u.Def.Id == id);
            if (bi >= 0) st.SwapWithBench(sq, slot, bi);
        }
        if (!st.Stage.Stars.On || st.Stage.Stars.EnhancePrice <= 0) return;
        for (int n = 0; n < 60 && st.Gold >= st.EnhancePrice; n++)
        {
            UnitState? pick = null;
            foreach (Map11State.Squad q in st.Squads)
            {
                pick = q.Units?.Where(u => u.IsAlive && st.CanEnhance(u))
                    .OrderBy(u => st.StarOf(u.Def.Id).Star).ThenBy(u => st.StarOf(u.Def.Id).Pips).ThenBy(u => u.Slot).FirstOrDefault();
                if (pick is not null) break;
            }
            if (pick is null || st.Enhance(pick) is null) break;
        }
    }

    /// <summary>1 通し（1-1 の終わりを持ち越して 1-2 を方針どおりに・戦闘は器具）。</summary>
    internal static Result Play(MapStage stage, int chapterSeed, Policy policy, int guard, bool teamB, Action<Map11State>? watch = null, Action<string>? log = null)
    {
        Map11State st = Setup(stage, chapterSeed, teamB, guard);
        MapGraph g = st.Graph;
        var res = new Result();
        // 中間拠点は盤から引く（基準の今の 1-2 でも同じ方針を立てるため）: 北 ＝ 見張り台 ／ 中 ＝ 関所 ／ 南 ＝ 渡し場 に当たる。
        int Op(int lane) => g.Lanes[lane].First(c => g.IsOutpost(c));
        int gate = Op(1), tower = Op(0), ferry = Op(2);
        int gSquad = guard > 0 ? (st.SquadCap >= 3 ? 2 : 1) : -1;
        res.Units = st.Squads.Sum(q => q.Units?.Count ?? 0);
        res.Squads = st.Squads.Count(q => q.Units is { Count: > 0 });
        var rests = new int[st.Squads.Length];

        void TakeRelics()
        {
            while (st.RelicOffer.Count > 0)
            {
                if (!st.ChooseRelic(0)) break;
                res.Relic = true;
                if (st.RelicHand.Count == 0) continue;
                TraitId relic = st.RelicHand[^1];
                UnitState? holder = st.Squads.Where(q => q.Units is not null).SelectMany(q => q.Units!).FirstOrDefault(u => u.IsAlive && u.Relic is null && st.CanEquip(u));
                if (holder is not null) st.EquipRelic(holder, relic);
            }
        }

        void Fight(int squad, Map11State.Node node, bool intercept, bool walked)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            if (prep is not { } q) return;
            int alive0 = q.Players.Count(u => u.IsAlive);
            int max0 = q.Players.Sum(u => u.MaxHp);
            double hpIn = max0 == 0 ? 0 : 100.0 * q.Players.Where(u => u.IsAlive).Sum(u => u.Hp) / max0;
            var standing = q.Players.Where(u => u.IsAlive).ToList();
            int where = st.Squads[squad].Pos;
            BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
            string place = where == g.Home ? "拠点" : where == g.Portal ? "本陣" : g[where].Name;
            foreach (UnitState u in standing.Where(u => !u.IsAlive)) res.Fell.Add((node.Def.Name, place, u.Def.Name, u.LastDeathTurn));
            int max = q.Players.Sum(u => u.MaxHp);
            double hp = max == 0 ? 0 : 100.0 * q.Players.Where(u => u.IsAlive).Sum(u => u.Hp) / max;
            int alive1 = q.Players.Count(u => u.IsAlive);
            st.Resolve(squad, q.Node, r.PlayerWon, intercept);
            res.Fights.Add((node.Def.Name, squad, r.PlayerWon, hp, alive0 - alive1, alive0, hpIn, place, walked, intercept));
            if (walked && !intercept && where == gate) res.GateCatches++;
            log?.Invoke($"    {st.Turn + 1} 刻目 ・ 隊 {squad + 1}（{place}）対 {node.Def.Name}{(intercept ? "［迎撃］" : walked ? "［歩いてきた］" : "")}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T・{alive0} 枚 {hpIn:F0}% → {alive1} 枚 {hp:F0}%"
                + (alive1 < alive0 ? $"・倒れた {string.Join("・", standing.Where(u => !u.IsAlive).Select(u => $"{u.Def.Name}（{u.LastDeathTurn}T）"))}" : "") + (node.IsHero ? $"・勇者の残り HP {node.HpNow}／{node.DefMaxHp}" : ""));
            TakeRelics();
            // 本陣の連戦: 勝ったら同じ刻のうちに次の相手と続けて戦う。
            if (!intercept && r.PlayerWon && !st.Finished && st.SiegeNext(squad) is { } nx && !ReferenceEquals(nx, node))
            {
                // 次が勇者で、もう 1 隊がまだ本陣の手前に着いていなければ引いて待つ（傷が深いときも）。引けなければ続けて戦う。
                bool wait = nx.IsHero && squad < 2 && ((Attacker(1 - squad) && !Staged(1 - squad)) || hp < 40);
                if (wait && st.SiegeRetreat(squad)) log?.Invoke($"    {st.Turn + 1} 刻目 ・ 隊 {squad + 1} は勇者の前で引いた → {g[st.Squads[squad].Pos].Name}");
                else Fight(squad, nx, false, false);
            }
        }

        (int[] Seq, int Lane) RouteOf(int i) => i == 0
            ? policy switch
            {
                Policy.MidSouth => (new[] { gate }, 1),
                Policy.NorthSouth => (new[] { tower }, 0),
                _ => (new[] { gate, tower }, 0),
            }
            : policy == Policy.MidNorth ? (new[] { gate }, 1) : (new[] { ferry }, 2);
        bool Attacker(int i) => i < 2 && i != gSquad && !st.Squads[i].Lost && st.Squads[i].Units is { } us && us.Any(u => u.IsAlive);
        int Staging(int i) => g.Lanes[RouteOf(i).Lane][^1];
        // 門が開いたら本陣の 1 つ手前で待ち、もう 1 隊がそろってから同じ刻に入る（勇者は 2 隊で落とす相手）。期限が近ければ待たない。
        bool Staged(int i) => !Attacker(i) || st.Squads[i].Pos == g.Portal || st.Squads[i].Pos == Staging(i);

        Dest DestFor(int i)
        {
            Map11State.Squad s = st.Squads[i];
            if (s.Lost || i == gSquad || i >= 2) return Dest.Home;
            (int[] seq, int lane) = RouteOf(i);
            if (s.Pos == g.Portal) return Dest.Portal(g, s.Road >= 0 ? s.Road : lane);
            if (st.GateOpen)
            {
                // 方針 1 の別働は関所の栓——主力が本陣の手前に着くまで関所を持つ。
                if (i == 1 && policy == Policy.MidNorth && Attacker(0) && !Staged(0)) return Dest.Of(g, gate);
                if (s.Pos != Staging(i)) return Dest.Of(g, Staging(i));
                return Staged(1 - i) || st.Turn >= stage.TurnCap - 3 ? Dest.Portal(g, lane) : Dest.Of(g, Staging(i));
            }
            foreach (int cell in seq) if (!st.HeldOutposts.Contains(cell)) return Dest.Of(g, cell);
            return Dest.Of(g, seq[^1]);
        }

        while (!st.Finished && st.Turn < 40 && !st.Fallen && !st.TimeUp)
        {
            for (int i = 0; i < st.SquadCap && !st.Finished; i++)
            {
                if (st.Squads[i].Units is not { Count: > 0 }) continue;
                Dest dest = DestFor(i);
                // 取った中間拠点で傷が深ければ 1 刻休む（隊ごとに 2 回まで）。
                if (i != gSquad && rests[i] < 4 && st.AtOutpost(i) && HpPct(st, i) < (st.GateOpen ? 85 : 60) && st.FoeFacing(i) is null && st.Rest(i)) { rests[i]++; continue; }
                if (i == gSquad && HpPct(st, i) < 100 && st.Rest(i)) continue;
                if (Map11Orders.Apply(st, i, dest, Map11Orders.Plan(st, i, dest)) is { } node) Fight(i, node, false, false);
                TakeRelics();
            }
            if (st.Finished) break;
            Spend(st);
            int held = st.HeldOutposts.Count;
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (st.Fallen) break;
                if (!e.Intercept) { Fight(e.Squad, e.Node, false, true); continue; }
                res.HomeIntercepts++;
                while (!st.Fallen && st.NextInterceptor(e.Node) is var j && j >= 0)
                {
                    Fight(j, e.Node, true, true);
                    if (e.Node.Cleared) break;
                }
                st.CloseIntercept(e.Node);
            }
            if (st.Fallen && res.FallTurn < 0) res.FallTurn = st.Turn;
            if (st.HeldOutposts.Count < held) res.GateLost += held - st.HeldOutposts.Count;
            if (st.ReserveDepartTurn >= 0 && res.ReserveLane < 0 && st.Reserve is { } rv) res.ReserveLane = rv.Def.Road;
            watch?.Invoke(st);
            log?.Invoke($"  {st.Turn} 刻の終わり ・ {string.Join(" ／ ", Enumerable.Range(0, st.SquadCap).Where(i => st.Squads[i].Units is { Count: > 0 }).Select(i => $"隊 {i + 1} {(st.Squads[i].Lost ? "全滅" : st.Squads[i].Pos == g.Home ? "拠点" : st.Squads[i].Pos == g.Portal ? "本陣" : g[st.Squads[i].Pos].Name)} {st.Squads[i].Units!.Count(u => u.IsAlive)} 枚 {HpPct(st, i):F0}%"))}"
                + $" ・ 中間拠点 {string.Join("・", st.HeldOutposts.Select(c => g[c].Name).DefaultIfEmpty("—"))} ・ 門 {(st.GateOpen ? "開" : "閉")} ・ 金 {st.Gold} ・ 歩く敵 {string.Join("・", st.PlannedFoeSteps().Select(x => $"{x.Node.Def.Name}→{(x.To == g.Home ? "拠点" : g[x.To].Name)}").DefaultIfEmpty("—"))}");
        }
        log?.Invoke($"  → {(st.Won ? $"制圧（{st.CapturedTurn + 1} 刻目）" : st.Fallen ? "陥落" : st.TimeUp ? "時間切れ" : "終わり")}・金 {st.Gold}");
        res.SpawnLanes = st.Spawns.Where(n => !n.IsHero && !n.IsReserve).Select(n => n.Def.Road).ToList();
        res.Won = st.Won; res.Fallen = st.Fallen; res.TimeUp = st.TimeUp;
        res.CaptureTurn = st.Captured ? st.CapturedTurn + 1 : -1;
        res.EndTurn = st.Turn;
        res.Lost = st.Squads.Sum(q => q.Units?.Count(u => !u.IsAlive) ?? 0) + st.FallenUnits.Count;
        res.Gold = st.Gold;
        res.HeroSquads = st.HeroSquads.Count;
        res.HeroDown = st.Hero is { Cleared: true };
        return res;
    }

    public static bool Run(Action<string> write, string demoDir, bool bots = true, bool sweepOnly = false)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V477;
        MapStage s = Chapter1.StagesFor(rule)[1];
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}");

        // ---- (a) 盤の形 ----
        {
            MapGraph g = s.Board(true);
            int[] len = g.Lanes.Select(l => l.Length - 1).ToArray();
            int[] op = g.Lanes.Select(l => Array.FindIndex(l, c => g.IsOutpost(c))).ToArray();
            var cross = g.Edges.Where(e => g.IsCross(e.A, e.B)).ToList();
            int[] steps = g.Lanes.Select((l, i) => g.Path(g.Home, -1, l[op[i]], -1).Count - 1).ToArray();
            string[] names = g.Lanes.Select((l, i) => g[l[op[i]]].Name).ToArray();
            bool crossOk = cross.Count == 1 && new[] { g[cross[0].A].Name, g[cross[0].B].Name }.OrderBy(x => x).SequenceEqual(new[] { "見張り台", "関所" }.OrderBy(x => x));
            write($"  (a) 道の長さ {string.Join(" ／ ", len)}・中間拠点 {string.Join(" ／ ", names.Select((n, i) => $"{n}（{op[i]} マス目・拠点から {steps[i]} 歩）"))}・横の辺 {cross.Count} 本"
                  + $"（{string.Join("・", cross.Select(e => $"{g[e.A].Name} ⇔ {g[e.B].Name}"))}）・マス {g.Cells.Count}");
            Req(len.SequenceEqual(new[] { 4, 3, 6 }) && op.SequenceEqual(new[] { 3, 2, 4 }) && steps.SequenceEqual(new[] { 4, 3, 5 }) && crossOk
                && names.SequenceEqual(MapGraph.Outposts12F477) && g.Lanes.All(l => g[l[0]].Name == MapGraph.Fork13),
                "(a) 盤の形: 道の長さ 4 ／ 3 ／ 6・中間拠点は 北 3 ／ 中 2 ／ 南 4 マス目（拠点から 4 ／ 3 ／ 5 歩）・横の辺は 関所 ⇔ 見張り台 の 1 本");
        }

        // ---- (b) 見返り ----
        {
            var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
            MapGraph g = st.Graph;
            int gate = g.Lanes[1][2], tower = g.Lanes[0][3], ferry = g.Lanes[2][4];
            int[] gold = { st.OutpostGoldAt(tower), st.OutpostGoldAt(gate), st.OutpostGoldAt(ferry) };
            bool[] relic = { st.OutpostGivesRelic(tower), st.OutpostGivesRelic(gate), st.OutpostGivesRelic(ferry) };
            var all = Play(s, 0, Policy.All, 2, teamB: true);
            write($"  (b) 見張り台 金 ＋{gold[0]}・レリック {relic[0]} ／ 関所 金 ＋{gold[1]}・レリック {relic[1]} ／ 渡し場 金 ＋{gold[2]}・レリック {relic[2]}"
                  + $"・通し（seed 0・3 つとも）: レリックを取った {all.Relic}・勝ち {all.Won}（制圧 {all.CaptureTurn} 刻目）");
            Req(gold.SequenceEqual(new[] { MapStage.Stage12Ch477TowerGold, 8, 12 }) && relic.SequenceEqual(new[] { true, false, false }),
                "(b) 中間拠点ごとの見返り: レリックは見張り台だけ・関所 金 ＋8・渡し場 金 ＋12");
        }

        // ---- (c) 門 ----
        {
            MapGraph g0 = s.Board(true);
            int[] ops = { g0.Lanes[0][3], g0.Lanes[1][2], g0.Lanes[2][4] };
            var parts = new List<string>();
            bool pairs = true, singles = true;
            for (int a = 0; a < 3; a++)
            {
                var one = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
                one.HeldOutposts.Add(ops[a]);
                singles &= !one.GateOpen;
                for (int b = a + 1; b < 3; b++)
                {
                    var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
                    st.HeldOutposts.Add(ops[a]); st.HeldOutposts.Add(ops[b]);
                    pairs &= st.GateOpen;
                    parts.Add($"{g0[ops[a]].Name}＋{g0[ops[b]].Name} {(st.GateOpen ? "開く" : "×")}");
                }
            }
            MapStage single = MapStage.Stage12.Scaled(rule.ScaleOf(1));
            bool same = s.PortalGate == 2 && s.Hero.Gatekeeper && s.Hero.Gold == single.Hero.Gold && s.Hero.Unit?.Name == single.Hero.Unit?.Name && s.Siege == single.Siege
                        && s.WinOnCapture && s.Lanes[1][^1].Stationary && s.Lanes[1][^1].Name == single.Lanes[1][^1].Name;
            write($"  (c) {string.Join("・", parts)}・1 つだけでは開かない {singles}・門番 {s.Hero.Unit?.Name}（動かない {s.Hero.Gatekeeper}・＋{s.Hero.Gold}）・連戦 {s.Siege.On}・本陣の守り {s.Lanes[1][^1].Name}");
            Req(pairs && singles && same, "(c) どの 2 つを取っても門が開く。門番の勇者・本陣の守り・連戦は今のまま");
        }

        // ---- (d) 歩く敵 ----
        {
            var empty = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
            int none = empty.ReserveTarget();
            var ms = Play(s, 0, Policy.MidSouth, 2, teamB: true);
            var ns = Play(s, 0, Policy.NorthSouth, 2, teamB: true);
            var mn = Play(s, 0, Policy.MidNorth, 0, teamB: true);
            bool spawnMid = new[] { ms, ns, mn }.All(r => r.SpawnLanes.All(l => l == 1)) && empty.NextSpawnRoad == 1;
            // 歩くのは中の街道の先頭だけ（北と南は待ち構える）
            var steps = empty.PlannedFoeSteps();
            bool onlyMid = steps.Count == 1 && steps[0].Node.Def.Road == 1 && empty.Nodes[0].All(n => n.Def.Hold) && empty.Nodes[2].All(n => n.Def.Hold);
            int fell = -1;
            for (int k = 0; k < 8 && !empty.Fallen; k++) { empty.AdvanceFoes(); if (empty.Fallen) fell = empty.Turn; }
            write($"  (d) 予備隊の行き先: 隊がいない盤 {s.LaneNames[none]}・中＋南 {(ms.ReserveLane < 0 ? "—" : s.LaneNames[ms.ReserveLane])}・北＋南 {(ns.ReserveLane < 0 ? "—" : s.LaneNames[ns.ReserveLane])}・中＋北（別働は関所）{(mn.ReserveLane < 0 ? "—" : s.LaneNames[mn.ReserveLane])}"
                  + $"・関所で受けた回数（中＋南・seed 0）{ms.GateCatches}・拠点の迎撃（北＋南・seed 0）{ns.HomeIntercepts}・湧きは中の街道だけ {spawnMid}・1 刻目に歩く敵 {steps.Count}（中だけ {onlyMid}）・空の拠点が落ちる刻 {fell}");
            Req(none == 1 && ms.ReserveLane == 0 && ns.ReserveLane == 1 && mn.ReserveLane == 0 && ms.GateCatches >= 1 && spawnMid && onlyMid && fell >= 4,
                "(d) 予備隊は隊のいない道を 中 → 北 → 南 の順に選ぶ。関所に隊がいれば歩いてきた敵は関所で当たる。湧きは中の街道だけ・歩くのは中の部隊だけ・空の拠点も 1〜3 刻目には落ちない");
        }

        // ---- (e) ほかは今のまま ----
        {
            bool single = MapStage.Stage12.OutpostRewards is null && MapStage.Stage12.SpawnLane < 0 && MapStage.Stage12.ReserveOrder is null
                          && MapStage.Stage12.TurnCap == 12 && MapStage.Stage12.LaneNames.SequenceEqual(MapGraph.Lanes12) && MapStage.Stage12.Board(true).Cells.Count == MapGraph.Build12F444(true).Cells.Count
                          && MapStage.Stage13.OutpostRewards is null && MapStage.Stage13.SpawnLane < 0 && MapStage.Stage13.ReserveOrder is null;
            var old = ChapterRule.Versions.Where(v => v.Key != "V477").ToList();
            bool olds = old.All(v => v.Rule.Second is null && ReferenceEquals(Chapter1.StagesFor(v.Rule)[1].Board, MapStage.Stage12.Board) && Chapter1.StagesFor(v.Rule)[1].OutpostRewards is null);
            var now = Chapter1.StagesFor(rule);
            var prev = Chapter1.StagesFor(ChapterRule.V476);
            bool rest = ReferenceEquals(now[0], prev[0]) && ReferenceEquals(now[2], prev[2]) && ReferenceEquals(rule.Roster, ChapterRule.V476.Roster) && rule.Scales == ChapterRule.V476.Scales;
            write($"  (e) 単発の 1-2 ／ 1-3 {(single ? "今のまま" : "×")}・{old.Count} 版（{old[0].Key}〜{old[^1].Key}）の 1-2 は単発の盤 {olds}・V477 の 1-1 ／ 1-3 は V476 と同じもの {rest}");
            Req(single && olds && rest, "(e) 単発の 1-2・第461〜476期の 1 章は今のまま");
        }

        if (!bots) return ok;
        int crashed = 0;
        bool lines = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (!sweepOnly) try { Weigh(write, rule); }
        catch (Exception ex) { crashed++; write($"  × 重さ: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        var baseRows = new Dictionary<(bool, Policy, int), Row>();
        try
        {
            if (!sweepOnly)
            {
                Routes(write, rule, Dress(MapStage.Stage12, rule, 1), "基準 ＝ 今の 1-2（V476・単発と同じ盤 ×0.9）", full: false, table: baseRows);
                lines &= Routes(write, rule, s, "新しい盤", full: true, baseline: baseRows);
            }
        }
        catch (Exception ex) { crashed++; write($"  × 通し: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        try { Sweep(write, rule); }
        catch (Exception ex) { crashed++; write($"  × 振った表: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        write($"  （測り {sw.Elapsed.TotalSeconds:F0} 秒）");
        if (!sweepOnly)
            foreach (var (pol, gd, seed) in new[] { (Policy.MidNorth, 2, 0), (Policy.MidSouth, 2, 0), (Policy.NorthSouth, 3, 0) })
            {
                write("");
                write($"  通しの写し（組 B・{PolicyName(pol)}・留守番 {gd} 枚・1 章の seed {seed}）");
                Play(s, seed, pol, gd, teamB: true, log: write);
            }
        Req(crashed == 0, "器具（重さ・通し × 100 seed）が落ちない");
        if (!sweepOnly) write($"{(lines ? "○" : "×")} §3-2 の線{(lines ? "" : "——通っていない（門の合否には入れない・報告を見る）")}");
        return ok;
    }

    private static double Med(IEnumerable<double> xs) => Chapter1AsymSmoke.Med(xs);

    // ---------------- §3-1 重さ ----------------

    private static void Weigh(Action<string> write, ChapterRule rule)
    {
        write("");
        write($"  §3-1 重さ（1-1 から持ち越した隊・呼ばない・{rule.ScaleOf(1).Text}・1 章の seed 0〜{WeighSeeds - 1}・満タンで 1 戦）");
        write("  隊: 組 A 主力 ＝ ムド・ドルガ・ガン・リィカ・リリ ／ 組 B 主力 ＝ ドルガ・ガン・リィカ ＋ 1-1 で呼んだ 2 枚 ／ 3 枚 ＝ 組 B の守り（ムド・ムグ・リリ）");
        write("");
        write("  | 相手 | 組 A 主力 5 枚（勝ち・後の HP・失った枚数） | 組 B 主力 5 枚 | 3 枚（ムド・ムグ・リリ） |");
        write("  |---|--:|--:|--:|");
        var foes = new List<(string Label, Func<MapStage, Formation> Pick)>();
        string[] bands = { "band_fire", "band_miasma", "band_shock", "band_lance", "band_shields", "band_knights", "band_archers", "band_sellswords", "band_levy", "band_garrison" };
        MapFoeScale sc = rule.ScaleOf(1);
        string Cell(bool teamB, bool small, Formation foe)
        {
            var rows = new List<(bool Won, double Hp, int Lost)>();
            for (int c = 0; c < WeighSeeds; c++)
            {
                // 3 枚は「留守番 3 枚」の隊（組 B の守り）
                Map11State st = Setup(Dress(MapStage.Stage12Ch477, rule, 1), c, teamB: small || teamB, guard: small ? 3 : 0, recruit: false);
                List<UnitState>? us = st.Squads[small ? 1 : 0].Units;
                if (us is not { Count: > 0 }) continue;
                int alive0 = us.Count;
                BattleResult r = BattleEngine.Run(us, BattleEngine.Materialize(foe, BattleContext.EnemyTeam, Map11.EnemyScale), c, verbose: false);
                int max = us.Sum(u => u.MaxHp);
                rows.Add((r.PlayerWon, r.PlayerWon && max > 0 ? 100.0 * us.Where(u => u.IsAlive).Sum(u => u.Hp) / max : 0, alive0 - us.Count(u => u.IsAlive)));
            }
            return rows.Count == 0 ? "—" : $"{rows.Count(x => x.Won)}／{rows.Count}・{Med(rows.Select(x => x.Hp)):F0}%・{rows.Average(x => (double)x.Lost):F1}";
        }
        void Line(string label, Formation foe) => write($"  | {label} | {Cell(false, false, foe)} | {Cell(true, false, foe)} | {Cell(true, true, foe)} |");
        foreach (string band in bands)
        {
            EnemyCatalog.MapSquad m = EnemyCatalog.MapSquadOf(band);
            Line(m.Name, sc.Foe(m.Enemy));
        }
        Line("物見の兵（第一波）", sc.Foe(EnemyCatalog.Stages[0].Enemy));
        foreach ((int hp, int atk) in new[] { (130, 100), (150, 100), (150, 130), (200, 130) })
            foreach (string band in new[] { "band_fire", "band_miasma" })
            {
                MapStage v = MapStage.Stage12Ch477With(tower: band, towerHp: hp, towerAtk: atk).Scaled(sc);
                Line($"{EnemyCatalog.MapSquadOf(band).Name} 最大HP ×{hp / 100.0:0.##}・攻撃 ×{atk / 100.0:0.##}", v.Lanes[0][0].Enemy);
            }
    }

    // ---------------- §3-2 通し ----------------

    private static readonly Policy[] Policies = { Policy.MidNorth, Policy.MidSouth, Policy.NorthSouth, Policy.All };
    private static readonly int[] Guards = { 0, 2, 3, 4 };

    private sealed record Row(Policy Policy, int Guard, int Wins, int Fallen, int TimeUp, double Turn, double Lost, double Gold, double Relic, double Catch, double Home, double Hero, double GateLost, double FallTurn, double HeroDown);

    private static Row Measure(MapStage stage, Policy p, int guard, bool teamB, List<Result>? keep = null)
    {
        var rs = Enumerable.Range(0, Seeds).AsParallel().AsOrdered().Select(c => Play(stage, c, p, guard, teamB)).ToList();
        keep?.AddRange(rs);
        var won = rs.Where(r => r.Won).ToList();
        var fell = rs.Where(r => r.Fallen).ToList();
        return new Row(p, guard, won.Count, fell.Count, rs.Count(r => r.TimeUp),
            won.Count == 0 ? double.NaN : Med(won.Select(r => (double)r.CaptureTurn)),
            rs.Average(r => (double)r.Lost),
            won.Count == 0 ? double.NaN : won.Average(r => (double)r.Gold),
            100.0 * rs.Count(r => r.Relic) / rs.Count,
            rs.Average(r => (double)r.GateCatches), rs.Average(r => (double)r.HomeIntercepts),
            rs.Any(r => r.HeroSquads > 0) ? rs.Where(r => r.HeroSquads > 0).Average(r => (double)r.HeroSquads) : double.NaN,   // 勇者と戦った通しの平均
            rs.Average(r => (double)r.GateLost),
            fell.Count == 0 ? double.NaN : Med(fell.Select(r => (double)r.FallTurn)),
            100.0 * rs.Count(r => r.HeroDown) / rs.Count);
    }

    private static Policy? Dominant(IReadOnlyList<Row> rows)
    {
        foreach (Row a in rows)
        {
            bool all = true, strict = false;
            foreach (Row b in rows.Where(x => x.Policy != a.Policy))
            {
                bool relicGe = a.Relic >= b.Relic, relicGt = a.Relic > b.Relic;
                all &= a.Wins >= b.Wins && a.Turn <= b.Turn && a.Lost <= b.Lost && a.Gold >= b.Gold && relicGe;
                strict |= a.Wins > b.Wins || a.Turn < b.Turn || a.Lost < b.Lost || a.Gold > b.Gold || relicGt;
            }
            if (all && strict) return a.Policy;
        }
        return null;
    }

    private static bool Routes(Action<string> write, ChapterRule rule, MapStage s, string label, bool full, Dictionary<(bool, Policy, int), Row>? table = null, Dictionary<(bool, Policy, int), Row>? baseline = null)
    {
        bool ok = true;
        foreach (bool teamB in new[] { false, true })
        {
            var rows = new List<Row>();
            var kept = new Dictionary<(Policy, int), List<Result>>();
            foreach (Policy p in Policies)
                foreach (int gd in Guards)
                {
                    var keep = new List<Result>();
                    rows.Add(Measure(s, p, gd, teamB, keep));
                    if (table is not null) table[(teamB, p, gd)] = rows[^1];
                    kept[(p, gd)] = keep;
                }
            var probe = kept[(Policy.MidSouth, 2)];
            write("");
            write($"  §3-2 通し・{label}（1 章の seed 0〜{Seeds - 1}・期限 {s.TurnCap} 刻・{(teamB ? "組 B" : "組 A")}・1-1 の終わりを持ち越し・留守番を置くなら 3 隊目を買い、残りの金で呼ぶ・戦闘は器具）"
                  + $"・駒 {probe.Average(r => (double)r.Units):F1} 枚（留守番 2 枚の通し）");
            write("");
            write("  | 方針 | 留守番 | 勝ち | 陥落（中央の刻） | 時間切れ | 制圧の刻 | 失った枚数 | 金の余り | レリック（%） | 関所で受けた | 拠点の迎撃 | 勇者に当てた隊 | 勇者を倒した（%） | 取り返された中間拠点 |");
            write("  |---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (Row r in rows)
                write($"  | {PolicyName(r.Policy)} | {r.Guard} | {r.Wins} | {r.Fallen}{(r.Fallen > 0 ? $"（{r.FallTurn:0.#}）" : "")} | {r.TimeUp} | {(double.IsNaN(r.Turn) ? "—" : r.Turn.ToString("0.#"))} | {r.Lost:F2} | {(double.IsNaN(r.Gold) ? "—" : r.Gold.ToString("F1"))} | {r.Relic:F0} | {r.Catch:F2} | {r.Home:F2} | {(double.IsNaN(r.Hero) ? "—" : r.Hero.ToString("F2"))} | {r.HeroDown:F0} | {r.GateLost:F2} |");

            Row At(Policy p, int g) => rows.First(r => r.Policy == p && r.Guard == g);
            Policy? dom = Dominant(Policies.Select(p => At(p, 2)).ToList());
            string M(bool x) => x ? "○" : "×";
            if (baseline is null) { write(""); write($"  （基準・線は見ない）全部の列で勝つ方針 {(dom is { } dd ? PolicyName(dd) : "無い")}"); continue; }
            var two = new[] { Policy.MidNorth, Policy.MidSouth };
            Row Base(Policy p) => baseline[(teamB, p, 2)];
            bool l1 = two.All(p => At(p, 2).Wins >= Base(p).Wins);
            bool l2 = two.All(p => At(p, 2).Fallen <= 10) && At(Policy.NorthSouth, 3).Fallen <= 30;
            bool l3 = two.All(p => At(p, 2).Hero >= 1.5);
            write("");
            write($"  線（{(teamB ? "組 B・線はこちらで見る" : "組 A・並べるだけ")}）: 方針 1・2 の勝ちが基準以上（留守番 2 枚） {M(l1)}（{string.Join("・", two.Select(p => $"{At(p, 2).Wins} ／ 基準 {Base(p).Wins}"))}）"
                  + $"・留守番 2 枚で陥落 10 以下（方針 1・2） ／ 方針 3 は留守番 3 枚で 30 以下 {M(l2)}（{At(Policy.MidNorth, 2).Fallen}・{At(Policy.MidSouth, 2).Fallen} ／ {At(Policy.NorthSouth, 3).Fallen}）"
                  + $"・勇者に当てた隊が平均 1.5 以上（方針 1・2） {M(l3)}（{string.Join("・", two.Select(p => At(p, 2).Hero.ToString("F2")))}）"
                  + $"・全部の列で勝つ方針は無い {M(dom is null)}{(dom is { } d ? $"（{PolicyName(d)}）" : "")}");
            if (teamB) ok &= l1 && l2 && l3 && dom is null;

            if (!full) continue;
            write("");
            write($"  誰が何 T に倒れたか（{(teamB ? "組 B" : "組 A")}・留守番 2 枚・相手ごと・100 通しのうち倒れた通しの割合と、その戦闘の中の平均ターン）");
            write("");
            write("  | 方針 | 相手（場所） | 勝ち ／ 戦闘 | 入る前（枚数・HP の中央値） | 後の HP | 倒れた駒 |");
            write("  |---|---|--:|--:|--:|---|");
            foreach (Policy p in Policies)
            {
                var rs = kept[(p, 2)];
                foreach (var grp in rs.SelectMany(r => r.Fights).GroupBy(f => (f.Foe, f.Where)).OrderByDescending(x => x.Count()).Take(9))
                {
                    var fell = rs.SelectMany((r, i) => r.Fell.Where(x => x.Foe == grp.Key.Foe && x.Where == grp.Key.Where).Select(x => (i, x.Unit, x.Turn)))
                        .GroupBy(x => x.Unit).Select(x => (Unit: x.Key, Runs: x.Select(y => y.i).Distinct().Count(), T: x.Average(y => (double)y.Turn)))
                        .OrderByDescending(x => x.Runs).Take(5).ToList();
                    write($"  | {PolicyName(p)} | {grp.Key.Foe}（{grp.Key.Where}） | {grp.Count(f => f.Won)} ／ {grp.Count()} | {Med(grp.Select(f => (double)f.AliveIn)):0.#} 枚・{Med(grp.Select(f => f.HpIn)):F0}% | {Med(grp.Select(f => f.Hp)):F0}%"
                          + $" | {(fell.Count == 0 ? "—" : string.Join(" ／ ", fell.Select(x => $"{x.Unit} {x.Runs}%・{x.T:F1}T")))} |");
                }
            }
        }
        return ok;
    }

    // ---------------- 振った表（見返りの額・敵の重さ・期限） ----------------

    private static void Sweep(Action<string> write, ChapterRule rule)
    {
        write("");
        write("  振った表（組 B・留守番 2 枚 ｜ 留守番なしの陥落・勝ち ／ 制圧の刻 ／ 失った枚数 ／ 金の余り を 方針 1 ｜ 2 ｜ 3 ｜ 4 の順）");
        write("");
        write("  | 盤 | 1 中＋北 | 2 中＋南 | 3 北＋南 | 4 3 つとも | 留守番なしの陥落（1・2・3） | 留守番 2 枚の陥落 | 留守番 3 枚の勝ち ／ 陥落 | 勇者に当てた隊 | 勇者を倒した（%） |");
        write("  |---|---|---|---|---|---|---|---|---|---|");
        var variants = new List<(string Label, MapStage Stage)>
        {
            ("基準 ＝ 今の 1-2（V476）", MapStage.Stage12),
            ("期限 12", MapStage.Stage12Ch477With(turnCap: 12)),
            ("期限 14", MapStage.Stage12Ch477With(turnCap: 14)),
            ("期限 16", MapStage.Stage12Ch477With(turnCap: 16)),
            ("期限 18", MapStage.Stage12Ch477With(turnCap: 18)),
            ("（採らない）期限 16・勇者の最大HP と攻撃 ×0.85", MapStage.Stage12Ch477With(turnCap: 16, heroPercent: 85)),
            ("（採らない）期限 16・勇者の最大HP と攻撃 ×0.7", MapStage.Stage12Ch477With(turnCap: 16, heroPercent: 70)),
            ("（参考）期限 16・引く先は今までどおり経路の奥のマス", MapStage.Stage12Ch477With(turnCap: 16, retreatToOutpost: false)),
            ("（参考）期限 16・門番の勇者を外す", MapStage.Stage12Ch477With(turnCap: 16, hero: false)),
        };
        foreach (var (label, raw) in variants)
        {
            MapStage v = Dress(raw, rule, 1);
            var at2 = Policies.Select(p => Measure(v, p, 2, true)).ToList();
            var at0 = Policies.Take(3).Select(p => Measure(v, p, 0, true)).ToList();
            var at3 = Policies.Take(3).Select(p => Measure(v, p, 3, true)).ToList();
            string C(Row r) => $"{r.Wins} ／ {(double.IsNaN(r.Turn) ? "—" : r.Turn.ToString("0.#"))} ／ {r.Lost:F2} ／ {(double.IsNaN(r.Gold) ? "—" : r.Gold.ToString("F1"))}";
            write($"  | {label} | {string.Join(" | ", at2.Select(C))} | {string.Join("・", at0.Select(r => r.Fallen))} | {string.Join("・", at2.Take(3).Select(r => r.Fallen))} | {string.Join("・", at3.Select(r => $"{r.Wins} ／ {r.Fallen}"))} | {string.Join("・", at2.Take(3).Select(r => double.IsNaN(r.Hero) ? "—" : r.Hero.ToString("F2")))} | {string.Join("・", at2.Take(3).Select(r => r.HeroDown.ToString("F0")))} |");
        }
    }
}
