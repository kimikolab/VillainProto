using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 1-3 の門（第458期 §1-2・`--map13-smoke`・指示書 design/PHASE458_STAGE13_SPEC.md）
//
// **画面を1つも作らない。** `MapStage.Stage13H458`（第459期から固定した第458期の 1-3）の盤と `Map11State` ／ `Map11Orders` ／ `Map11Session` だけを回す。
// **ここには Godot の型を1つも使わない**（`Map12Smoke` と同じ扱い）。
//
//   (a) 盤 14 マス ＋ 拠点 ＋ 本陣・経路 3 本・横の辺 2 本（中間拠点どうし）・分かれ道 F を通り抜けられる
//   (b) 北の砦から野営地へ横の辺で 1 歩
//   (c) 雷杖の部隊で味方に感電が付く ／ 槍兵の部隊で味方が押される
//   (d) C3 で徴募兵 3 部隊と連戦の窓が出る・引けば野営地（大群の戦闘では回復が乗らない）
//   (e) 本陣の守り（重）を抜くと ＋12
//   (f) 期限 18 で時間切れ
//
// 加えて、器具の 3 方針 × 100 seed を 1-3 と 1-2（`Stage12`）で並べる（<b>報告だけ・線は置かない</b>）。
// =====================================================================================

public static class Map13Smoke
{
    public const int Seeds = 100;

    public static bool Run(Action<string> write)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        MapStage stage = MapStage.Stage13H458;   // 第459期: 第458期の 1-3 に固定（遊ぶ 1-3 は勇者が替わった）

        void Win(Map11State st, int squad, Map11State.Node node)
        {
            var prep = st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Enemies) u.Hp = 0;
            st.Resolve(squad, node, true, false);
        }
        // 行き先へ歩かせる（当たった相手は勝たせる・連戦は続けない）。歩いたマスの名前を返す。
        List<string> Walk(Map11State st, int squad, Dest dest, int steps = 30)
        {
            var trail = new List<string>();
            for (int k = 0; k < steps && !st.Finished; k++)
            {
                if (st.FoeFacing(squad) is { } here) { Win(st, squad, here); continue; }
                if (st.AtDest(squad, dest)) break;
                Map11Orders.Order o = Map11Orders.Plan(st, squad, dest);
                Map11State.Node? met = Map11Orders.Apply(st, squad, dest, o);
                trail.Add($"{o}:{st.Graph[st.Squads[squad].Pos].Name}");
                if (met is not null) Win(st, squad, met);
            }
            return trail;
        }

        // ---- (a) 盤 ----
        {
            var g = stage.Board(true);
            var off = stage.Board(false);
            var cross = g.Edges.Where(e => g.IsCross(e.A, e.B)).ToList();
            int fork = g.Lanes[0][0];
            write($"  盤: マス {g.Cells.Count}（拠点なしの盤 {off.Cells.Count}）・経路 {g.Lanes.Count} 本 × {string.Join("/", g.Lanes.Select(l => l.Length))}（分かれ道を含む）"
                + $"・横の辺 {string.Join("・", cross.Select(e => $"{g[e.A].Name}–{g[e.B].Name}"))}");
            write($"       北: {string.Join(" → ", g.Lanes[0].Select(id => g[id].Name))} → 本陣");
            write($"       中: {string.Join(" → ", g.Lanes[1].Select(id => g[id].Name))} → 本陣");
            write($"       南: {string.Join(" → ", g.Lanes[2].Select(id => g[id].Name))} → 本陣");
            Req(off.Cells.Count == 15 && g.Cells.Count == 16 && g.HasPortal && g.Lanes.Count == 3
                && g.Lanes.Select(l => l.Length).SequenceEqual(new[] { 5, 6, 5 }) && stage.LaneCount == 3,
                "(a) 盤 14 マス（分かれ道 1 ＋ 北 4 ＋ 中 5 ＋ 南 4）＋ 拠点 ＋ 本陣・経路 3 本");
            Req(cross.Count == 2 && cross.All(e => g.IsOutpost(e.A) && g.IsOutpost(e.B))
                && g.Cells.Count(c => c.Kind == MapGraph.Kind.Outpost) == 3
                && Enumerable.Range(0, 3).All(r => g.Lanes[r][3] == g.Cells.First(c => c.Kind == MapGraph.Kind.Outpost && c.Lane == r).Id)
                && g.Cells.All(c => c.Neighbors.All(n => g[n].Neighbors.Contains(c.Id))),
                "(a) 横の辺は中間拠点どうしの 2 本（砦–野営地・野営地–関所）・中間拠点は各経路の 3 マス目・辺はすべて両方向");
            Req(g.Lanes.All(l => l[0] == fork) && g[fork].Lane < 0 && g[fork].Kind == MapGraph.Kind.Open
                && g[g.Home].Neighbors.SequenceEqual(new[] { fork })
                && Enumerable.Range(0, 3).All(r => g.Path(g.Home, -1, g.Lanes[r][3], -1).Count == 5)
                && g.Path(g.Lanes[0][3], -1, g.Portal, 0).Count == 3 && g.Path(g.Lanes[1][3], -1, g.Portal, 1).Count == 4
                && g.Path(g.Lanes[2][3], -1, g.Portal, 2).Count == 3,
                "(a) 拠点 → 分かれ道 → 3 経路・中間拠点は拠点から 4 歩・本陣まで北 2 ／ 中 3 ／ 南 2 歩");
            // 分かれ道を通り抜ける: 道の敵を抜いた盤で、拠点から野営地へ歩く（1 刻目に分かれ道に立ち、次の刻に C0 へ）。
            var st = new Map11State(0, stage.Time, stage.Portal, stage: stage);
            foreach (var lane in st.Nodes) foreach (var n in lane) if (!n.Def.Stationary) n.Cleared = true;
            var trail = Walk(st, 0, Dest.Of(st.Graph, g.Lanes[1][3]));
            write($"  拠点 → 野営地（道の敵を抜いた盤）: {string.Join(" → ", trail)}");
            Req(trail.Count == 4 && trail[0].EndsWith(MapGraph.Fork13) && st.AtDest(0, Dest.Of(st.Graph, g.Lanes[1][3])),
                "(a') 分かれ道を通り抜けて 4 刻で野営地に着く");
            // 分かれ道にいる敵は、どの経路へ向かう隊でも素通りしない（同じマスなら当たる）。
            var st2 = new Map11State(0, stage.Time, stage.Portal, stage: stage);
            st2.Nodes[0][0].Pos = fork;   // 北の先頭（弓隊）が分かれ道まで来た体
            st2.Send(0, 1);               // 第1隊を中の道へ出す（1 歩目は分かれ道）
            Map11State.Node? face = st2.FoeFacing(0);
            write($"  分かれ道に北の弓隊・第1隊を中の道へ出す → 当たる相手 {face?.Def.Name ?? "なし"}（隊の経路 {st2.Squads[0].Road}）");
            Req(face is not null && ReferenceEquals(face, st2.Nodes[0][0]), "(a'') 分かれ道では、別の経路の敵にも当たる（素通りしない）");
        }

        // ---- (b) 北の砦 → 野営地を横の辺で 1 歩 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, stage: stage);
            var g = st.Graph;
            foreach (var lane in st.Nodes) foreach (var n in lane) if (!n.Def.Stationary) n.Cleared = true;
            Walk(st, 0, Dest.Of(g, g.Lanes[0][3]));
            int before = st.Squads[0].Pos, moves = st.CrossMoves;
            Dest camp = Dest.Of(g, g.Lanes[1][3]);
            Map11Orders.Order o = Map11Orders.Plan(st, 0, camp);
            Map11Orders.Apply(st, 0, camp, o);
            write($"  (b) {g[before].Name} → {o} → {g[st.Squads[0].Pos].Name}（経路 {st.Squads[0].Road}・横の辺を通った手 {st.CrossMoves - moves}）");
            Req(g[before].Name == MapGraph.Outposts13[0] && o == Map11Orders.Order.Move && st.AtDest(0, camp) && st.Squads[0].Road == 1
                && st.CrossMoves == moves + 1,
                "(b) 北の砦から野営地へ横の辺で 1 歩（着いた隊の経路は中）");
        }

        // ---- (c) 雷杖の部隊・槍兵の部隊 ----
        {
            Formation me = Map11.Squads[0].F;
            int seed = 0;
            BattleResult Fight(string id) => BattleEngine.Run(BattleEngine.Materialize(me, BattleContext.PlayerTeam),
                BattleEngine.Materialize(EnemyCatalog.MapSquadOf(id).Enemy, BattleContext.EnemyTeam, Map11.EnemyScale), seed, verbose: true);
            BattleResult shock = Fight("band_shock");
            int marks = shock.Log.Count(l => l.Text.Contains("の雷杖が", StringComparison.Ordinal));
            write($"  (c) 雷杖の部隊 × {Map11.Squads[0].Name}（seed {seed}）: 雷杖が帯電させた {marks} 回 ・ 勝ち {shock.PlayerWon} ・ {shock.Turns} T");
            Req(marks > 0, "(c) 雷杖の部隊で味方に感電が付く");
            BattleResult lance = Fight("band_lance");
            int shoves = lance.Log.Count(l => l.Text.Contains("を後ろへ突き飛ばした", StringComparison.Ordinal));
            write($"  (c) 槍兵の部隊 × {Map11.Squads[0].Name}（seed {seed}）: 突き飛ばした {shoves} 回 ・ 勝ち {lance.PlayerWon} ・ {lance.Turns} T");
            foreach (string l in lance.Log.Select(x => x.Text).Where(t => t.Contains("突き飛ばした", StringComparison.Ordinal)).Take(3)) write($"       {l.Trim()}");
            // 押す先の表（X 字）: 前1 → 中央・前3 → 中央・中央 → 後1・後列 → 押せない。
            var probe = BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Kado, center: UnitCatalog.Hisa, back1: UnitCatalog.Hane, back3: UnitCatalog.Utsu), BattleContext.PlayerTeam);
            string table = string.Join(" ／ ", probe.Select(u => $"{FormationRules.SeatNames[u.Slot]} → {(ShoveFoeTrait.BehindOf(u) is int b && b >= 0 ? FormationRules.SeatNames[b] : "なし")}"));
            write($"       押す先: {table}");
            Req(shoves > 0, "(c) 槍兵の部隊で味方が押される（押す・代替の転倒は使っていない）");
        }

        // ---- (d) C3 の大群と連戦の窓・引けば野営地 ----
        {
            Map11Session.Reset(0, draft: true, stage: stage);
            Map11State st = Map11Session.State!;
            var g = st.Graph;
            st.UseDefaultSquads();
            int c3 = g.Lanes[1][4];
            // 中の道の物見・火の部隊を抜いた体にし、第1隊を野営地まで歩かせる。
            st.Nodes[1][0].Cleared = st.Nodes[1][1].Cleared = true;
            Walk(st, 0, Dest.Of(g, g.Lanes[1][3]));
            var horde = st.Nodes[1].Where(n => n.Def.Hold).ToList();
            write($"  (d) C3（{g[c3].Name}）の大群: {string.Join("・", horde.Select(n => $"{n.Def.Name}@{g[n.Pos].Name}"))}");
            Req(horde.Count == 3 && horde.All(n => n.Pos == c3 && n.Def.Name == EnemyCatalog.MapSquadOf("band_levy").Name),
                "(d) C3 に徴募兵 3 部隊が重なって待つ");
            // 大群は前進しない（1 刻流しても C3 のまま）・矢印にも出ない。
            bool planned = st.PlannedFoeSteps().Any(p => p.Node.Def.Hold);
            Map11Session.SetDestination(0, Dest.Of(g, c3));
            Map11Session.SetDestination(1, Dest.Home);
            var fought = new List<string>();
            string? stop = null;
            for (int guard = 0; guard < 4 && stop is null && !st.Finished; guard++)
            {
                if (!Map11Session.TurnRunning) Map11Session.BeginTurn();
                for (int step = 0; step < 20 && Map11Session.TurnRunning; step++)
                {
                    Map11Session.StepKind kind = Map11Session.StepTurn();
                    if (kind == Map11Session.StepKind.Siege) { stop = "Siege"; break; }
                    if (kind == Map11Session.StepKind.Battle)
                    {
                        Map11State.Node n = Map11Session.PendingNode!;
                        foreach (UnitState u in CampaignSession.CarriedEnemies!) u.Hp = 0;
                        CampaignSession.ClearCarriedBattle();
                        Map11Session.CompleteBattle(true);
                        fought.Add($"刻{st.Turn}:{n.Def.Name}@{g[n.Pos].Name}");
                        continue;
                    }
                    if (kind == Map11Session.StepKind.Intercept)
                    {
                        if (st.NextInterceptor(Map11Session.InterceptNode!) is var j && j >= 0 && Map11Session.ChooseInterceptor(j))
                        {
                            foreach (UnitState u in CampaignSession.CarriedEnemies!) u.Hp = 0;
                            CampaignSession.ClearCarriedBattle();
                            Map11Session.CompleteBattle(true);
                            continue;
                        }
                        break;
                    }
                    if (kind is Map11Session.StepKind.Done or Map11Session.StepKind.Fallen) break;
                }
            }
            Map11State.Node? next = Map11Session.SiegeNode;
            int left = horde.Count(n => !n.Cleared);
            write($"  (d) 当たった順: {string.Join(" → ", fought)} ／ 窓 {stop ?? "出ない"}（次の相手 {next?.Def.Name ?? "なし"}・残る大群 {left}）・大群の矢印 {planned}");
            Req(stop == "Siege" && fought.Count == 1 && fought[0].EndsWith(g[c3].Name) && next is { Def.Hold: true } && left == 2 && !planned,
                "(d) C3 で徴募兵に勝つと同じ刻に連戦の窓が出る（次は 2 部隊目・大群は動かない）");
            bool back = Map11Session.RetreatSiege();
            write($"  (d) 「引く」→ {g[st.Squads[0].Pos].Name}（引いた {back}・重なったマスで引いた回数 {st.StackRetreats}）");
            Req(back && st.Squads[0].Pos == g.Lanes[1][3] && st.StackRetreats == 1, "(d) 大群から引けば野営地へ戻る");
            Map11Session.Reset(0, draft: true, stage: stage);

            // 大群の戦闘では回復が乗らない（物見のような道の上の敵では乗る）。
            int Probe(bool hordeFight, out int before)
            {
                var s2 = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
                s2.UseDefaultSquads();
                if (hordeFight) s2.Nodes[1][0].Cleared = s2.Nodes[1][1].Cleared = true;   // 道の上の敵の版は中の道の先頭（物見）に当たる
                Map11State.Node? foe = null;
                Dest d = Dest.Of(s2.Graph, hordeFight ? s2.Graph.Lanes[1][4] : s2.Graph.Lanes[1][3]);
                for (int k = 0; k < 12 && foe is null; k++)
                {
                    if (s2.FoeFacing(0) is { } here) { foe = here; break; }
                    if (Map11Orders.Apply(s2, 0, d, Map11Orders.Plan(s2, 0, d)) is { } f) foe = f;
                }
                foreach (UnitState u in s2.Squads[0].Units!.Where(x => x.IsAlive)) u.Hp = Math.Max(1, u.MaxHp / 2);
                before = s2.Squads[0].Units!.Where(x => x.IsAlive).Sum(x => x.Hp);
                Win(s2, 0, foe!);
                return s2.Squads[0].Units!.Where(x => x.IsAlive).Sum(x => x.Hp);
            }
            int aH = Probe(true, out int bH), aR = Probe(false, out int bR);
            write($"  (d) 勝った後の第1隊の HP: 大群 {bH} → {aH}（回復なし）／ 道の上の物見 {bR} → {aR}（道中の回復 50%）");
            Req(aH == bH && aR > bR, "(d) 大群の戦闘では戦闘の後の回復が乗らない（道の上の敵では乗る）");
        }

        // ---- (e) 本陣の守り（重）を抜くと ＋12 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            var g = st.Graph;
            st.UseDefaultSquads();
            foreach (var lane in st.Nodes) foreach (var n in lane) if (!n.Def.Stationary) n.Cleared = true;
            Walk(st, 0, Dest.Of(g, g.Lanes[0][3]));
            Walk(st, 1, Dest.Of(g, g.Lanes[2][3]));
            bool gate = st.GateOpen;
            Map11State.Node garrison = st.Nodes[1].Single(n => n.Def.Stationary);
            int goldBefore = st.Gold;
            var trail = Walk(st, 0, Dest.Portal(g, 0));
            int gained = st.GoldLog.Where(e => e.Reason.StartsWith(garrison.Def.Name, StringComparison.Ordinal)).Sum(e => e.Amount);
            write($"  (e) 門 {(gate ? "開" : "閉")} → 第1隊 {string.Join(" → ", trail)} ／ {garrison.Def.Name} 撃破 {garrison.Cleared}・金 {goldBefore} → {st.Gold}（守りの分 ＋{gained}）・制圧 {st.Captured}");
            write($"       中身: {string.Join("・", garrison.Def.Enemy.Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]} {o.Def.Name}（{o.Def.MaxHp}/{o.Def.Attack}）"))}");
            Req(gate && garrison.Cleared && gained == 12 && st.KillGold(garrison) == 12, "(e) 本陣の守り（重）を抜くと ＋12");
        }

        // ---- (f) 期限 18 で時間切れ ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, stage: stage);
            int? first = st.TimeLimitLeft;
            for (int guard = 0; guard < 40 && !st.Finished; guard++)
                foreach (Map11State.Encounter e in st.AdvanceFoes())
                {
                    if (st.Finished) break;
                    if (!e.Intercept) { if (!e.Node.IsHero) Win(st, e.Squad, e.Node); continue; }
                    while (!st.Finished && st.NextInterceptor(e.Node) is var j && j >= 0)
                    {
                        var prep = st.PrepareIntercept(j, e.Node);
                        if (prep is null) break;
                        foreach (UnitState u in prep.Value.Enemies) u.Hp = 0;
                        st.Resolve(j, e.Node, true, true);
                        if (e.Node.Cleared) break;
                    }
                    st.CloseIntercept(e.Node);
                }
            write($"  (f) 拠点で待ち続けた通し: 始めの残り {first} 刻 → 刻 {st.Turn} で終わった（時間切れ {st.TimeUp}・陥落 {st.Fallen}・勝ち {st.Won}・湧いた {st.SpawnCount}）");
            Req(first == 18 && st.TimeUp && st.Turn == 18 && !st.Won && !st.Fallen, "(f) 期限 18 で時間切れ");
        }

        // ---- 報告のみ: 3 方針 × 100 seed（1-3 と 1-2 を並べる） ----
        write("");
        write("  | 盤 | 方針 | 踏破 | 時間切れ | 陥落 | 制圧の刻（中央値） | 戦闘（平均） | 横の辺を通った手（平均） | 大群で引いた（平均） | 本陣から引いた（平均） | 1 戦あたりの失った枚数 |");
        write("  |---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        int crashed = 0;
        var policies = new[] { ("1 隊残す", true, false, false), ("分担＋戻り番", true, true, false), ("籠城", true, true, true) };
        foreach (var (bname, board) in new[] { ("1-2（第457期）", MapStage.Stage12), ("1-3（第458期）", stage) })
            foreach (var (pname, guard, split, turtle) in policies)
            {
                int n = 0, won = 0, timeUp = 0, fallen = 0, cross = 0, stackBack = 0, portalBack = 0, battles = 0;
                var capTurns = new List<int>();
                var byFoe = new Dictionary<string, (int Battles, int Lost)>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    bool counted = false;
                    try
                    {
                        Map12ChangeSmoke.Greedy(seed, board, guard, split, turtle, byFoe, watch: q =>
                        {
                            if (!q.Finished || counted) return;
                            counted = true;
                            n++;
                            if (q.Won) won++;
                            if (q.TimeUp) timeUp++;
                            if (q.Fallen) fallen++;
                            if (q.Captured) capTurns.Add(q.CapturedTurn + 1);
                            cross += q.CrossMoves;
                            stackBack += q.StackRetreats;
                            portalBack += q.SiegeRetreats - q.StackRetreats;
                            battles += q.Battles;
                        }, siegeRest: true);
                    }
                    catch (Exception ex) { crashed++; if (crashed <= 3) write($"  × {bname} {pname} seed {seed}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
                }
                if (n == 0) continue;
                double R(int k) => 100.0 * k / n;
                capTurns.Sort();
                int fb = byFoe.Values.Sum(v => v.Battles), fl = byFoe.Values.Sum(v => v.Lost);
                write($"  | {bname} | {pname} | {R(won):F0} | {R(timeUp):F0} | {R(fallen):F0} | {(capTurns.Count == 0 ? "—" : capTurns[capTurns.Count / 2].ToString())} | "
                    + $"{(double)battles / n:F1} | {(double)cross / n:F2} | {(double)stackBack / n:F2} | {(double)portalBack / n:F2} | {(fb == 0 ? 0 : (double)fl / fb):F2} |");
                if (board == stage && pname == "分担＋戻り番")
                    foreach (var kv in byFoe.OrderByDescending(kv => kv.Value.Battles))
                        write($"  |   | 　└ {kv.Key} | | | | | {kv.Value.Battles} 戦 | | | | {(double)kv.Value.Lost / Math.Max(1, kv.Value.Battles):F2} |");
            }
        Req(crashed == 0, $"1-2 と 1-3 × 3 方針を {Seeds} seed ずつ回して落ちない（数字は報告のみ・線は置かない・方針は本陣と大群の合間に HP 50% 未満なら引いて休む）");
        return ok;
    }
}
