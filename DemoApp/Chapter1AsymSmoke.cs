using BattleCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================================
// 第474期の門 `--chapter1-asym-smoke`（指示書 design/PHASE474_STAGE11_ASYM_SPEC.md §4・画面なし）
//
//   (a) 1 章（V474）の 1-1 の盤: 北 2 マス・南 4 マス。敵の置き場が §1 のとおり
//   (b) 湧いた部隊が北を先に通る（拠点まで 3 歩）。南は 5 歩
//   (c) 行き先までの作戦ターン数（盤の最短路の歩数・画面の `TurnsToDest` と同じ式）が、長さの違う道で正しい
//   (d) 単発の 1-1・第461〜471期の 1 章は今のまま
//
// 加えて（報告だけ・線は置かない）:
//   §1 の重さの調べ: 北の 2 マス目（峠の関）の候補（雑兵の部隊 × その区画だけの倍率）× 始めの 6 枚の 5 隊（`design/weights/p471_start6`）
//   §1 の通し: 北だけ ／ 南だけ ／ 2 隊に分ける ／（参考）拠点で迎え撃ってから出る × 100 seed
// `--chapter1-asym-quick` は報告だけの 2 つを省く。**規則は1つも持たない。ここには Godot の型を1つも使わない。**
// =====================================================================================

public static class Chapter1AsymSmoke
{
    private const int Seeds = 100;
    private const int WeighSeeds = 20;

    public static bool Run(Action<string> write, string demoDir, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V474;
        MapStage s = Chapter1.StagesFor(rule)[0];
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}");

        // ---- (a) 盤と敵の置き場 ----
        {
            var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
            MapGraph g = st.Graph;
            bool lens = g.Lanes.Count == 2 && g.Lanes[0].Length == 2 && g.Lanes[1].Length == 4;
            bool plain = g.Cells.All(c => c.Kind != MapGraph.Kind.Outpost) && !g.Edges.Any(e => g.IsCross(e.A, e.B)) && s.PortalGate == 0;
            string[] want = { EnemyCatalog.Stages[0].Name, Band(MapStage.Stage11Ch474GateBand), Band("band_levy"), Band("band_knights") };
            string[] got = { st.Nodes[0][0].Def.Name, st.Nodes[0][1].Def.Name, st.Nodes[1][0].Def.Name, st.Nodes[1][1].Def.Name };
            int[] at = { g.IndexOn(0, st.Nodes[0][0].Pos), g.IndexOn(0, st.Nodes[0][1].Pos), g.IndexOn(1, st.Nodes[1][0].Pos), g.IndexOn(1, st.Nodes[1][1].Pos) };
            bool places = at.SequenceEqual(new[] { 0, 1, 0, 2 });
            bool same = s.FoeScale.FoePercent == 60 && s.Spawn(0).Name == Band("band_levy") && s.Spawn(1).Name == Band("band_sellswords")
                        && s.Gold.Start == 8 && s.Arrival.Start == 6 && st.Bench.Count == 6;
            write($"  (a) 経路 {string.Join(" ／ ", g.Lanes.Select((l, i) => $"{g.LaneNames[i]} {l.Length} マス"))}・北 {got[0]}（{at[0] + 1} マス目）→ {got[1]}（{at[1] + 1} マス目）／ 南 {got[2]}（{at[2] + 1} マス目）→ {got[3]}（{at[3] + 1} マス目）"
                  + $"・横の辺 ／ 中間拠点 ／ 門 なし {plain}・倍率 ×{s.FoeScale.FoePercent / 100.0:F1}・湧き {s.Spawn(0).Name}・{s.Spawn(1).Name}・金 {s.Gold.Start}・手札 {st.Bench.Count}・峠の関 {Band(MapStage.Stage11Ch474GateBand)}（その区画だけ 最大HP {MapStage.Stage11Ch474GateHp}% ／ 攻撃 {MapStage.Stage11Ch474GateAtk}%）");
            // ポンの決定（2026-11-14）: 関は動かない・制圧したら勝ち・関は 250% ／ 200%・抜くと金 +6（ほかの道の部隊も +6 のまま）
            Map11State.Node gate = st.Nodes[0][1];
            bool decided = gate.Def.Hold && !st.Nodes[0][0].Def.Hold && st.Nodes[1].All(n => !n.Def.Hold) && s.WinOnCapture
                           && MapStage.Stage11Ch474GateHp == 250 && MapStage.Stage11Ch474GateAtk == 200
                           && st.KillGold(gate) == 6 && st.KillGold(st.Nodes[0][0]) == 6 && st.TurnsToHome(gate) is null;
            write($"      峠の関: 動かない {gate.Def.Hold}・拠点まで あと {(st.TurnsToHome(gate) is { } eta ? eta.ToString() : "—（歩かない）")}・抜くと金 +{st.KillGold(gate)}（道の部隊 +{st.KillGold(st.Nodes[0][0])}）・制圧したら勝ち {s.WinOnCapture}");
            Req(lens && plain && want.SequenceEqual(got) && places && same && decided,
                "(a) 1 章（V474）の 1-1: 北 2 マス（物見の兵 → 盾の列）・南 4 マス（徴募兵 → 空 → 騎士の小隊 → 空）・倍率 ／ 湧き ／ 金 ／ 手札は第471期のまま・峠の関は動かず 250% ／ 200%・抜くと金 +6・制圧したら勝ち");
        }

        // ---- (b) 湧きは北が先・拠点まで 3 歩 ／ 5 歩 ----
        {
            var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
            MapGraph g = st.Graph;
            int north = g.Path(g.Portal, 0, g.Home, -1).Count - 1, south = g.Path(g.Portal, 1, g.Home, -1).Count - 1;
            // 通しを 1 本回して、最初に湧いた 2 部隊の経路と「拠点まで あと n」を湧いた刻に読む。
            var seen = new List<(int Road, int? Eta, int Turn)>();
            int gateAt = -1, northSpawnAt = -1;
            Play(s, 0, Plan.Hold, watch: q =>
            {
                foreach (Map11State.Node sp in q.Spawns.Skip(seen.Count).ToList()) seen.Add((sp.Def.Road, q.TurnsToHome(sp), q.Turn));
                gateAt = q.Graph.IndexOn(0, q.Nodes[0][1].Pos);
                if (q.Spawns.FirstOrDefault(x => x.Def.Road == 0 && !x.Cleared) is { } ns) northSpawnAt = q.Graph.IndexOn(0, ns.Pos);
            }, endless: 20);
            string spawned = seen.Count == 0 ? "湧かなかった" : string.Join("・", seen.Take(2).Select(x => $"{g.LaneNames[x.Road]}（作戦ターン {x.Turn + 1}・拠点まで あと {x.Eta}）"));
            write($"  (b) 敵の拠点から拠点まで: 北 {north} 歩 ／ 南 {south} 歩・最初の湧き {spawned}・誰も出ない 20 刻の後: 峠の関は北の {gateAt + 1} マス目のまま・北の湧きは {(northSpawnAt >= g.Lanes[0].Length ? "敵の拠点で詰まっている" : $"北の {northSpawnAt + 1} マス目")}");
            Req(north == 3 && south == 5 && seen.Count >= 2 && seen[0].Road == 0 && seen[0].Eta == 3 && seen[1].Road == 1 && seen[1].Eta == 5 && gateAt == 1 && northSpawnAt > gateAt,
                "(b) 湧いた部隊は北が先（拠点まで 3 歩）。南は 5 歩。関は動かず、抜くまで北の湧きは関の奥で詰まる");
        }

        // ---- (c) 行き先までの作戦ターン数 ----
        {
            var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
            MapGraph g = st.Graph;
            int Steps(int from, int via, Dest d) => g.Path(from, via, d.Id, d.Lane).Count - 1;
            var fromHome = new List<string>();
            bool okC = true;
            for (int lane = 0; lane < 2; lane++)
            {
                for (int k = 0; k < g.Lanes[lane].Length; k++)
                {
                    int n = Steps(g.Home, -1, Dest.Of(g, g.Lanes[lane][k]));
                    okC &= n == k + 1;
                    fromHome.Add($"{g[g.Lanes[lane][k]].Name} {n}");
                }
                int p = Steps(g.Home, -1, Dest.Portal(g, lane));
                okC &= p == g.Lanes[lane].Length + 1;
                fromHome.Add($"{Map11.PortalName}（{s.LaneMark(lane)}）{p}");
            }
            // 道をまたぐ（北の 2 マス目 → 南の 3 マス目は拠点を通って 5 歩）・敵の拠点は経由しない
            int cross = Steps(g.Lanes[0][1], 0, Dest.Of(g, g.Lanes[1][2]));
            int back = Steps(g.Lanes[1][3], 1, Dest.Home);
            okC &= cross == 5 && back == 4;
            write($"  (c) 拠点から: {string.Join("・", fromHome)} ／ 北の 2 マス目 → 南の 3 マス目 {cross}（拠点を通る）／ 南の 4 マス目 → 拠点 {back}");
            Req(okC, "(c) 行き先までの作戦ターン数（盤の最短路の歩数）が、長さの違う道で正しい（北 1・2・敵の拠点 3 ／ 南 1〜4・敵の拠点 5）");
        }

        // ---- (d) 単発・第461〜471期 ----
        {
            string Line(MapStage x) => $"{string.Join(" ／ ", x.Lanes.Select(l => string.Join("→", l.Select(n => n.Name))))} ／ 湧き {x.Spawn(0).Name}・{x.Spawn(1).Name}";
            bool Shape(MapStage x) { MapGraph g = x.Board(true); return g.Lanes.Count == 2 && g.Lanes.All(l => l.Length == Map11.RoadCells) && g.Cells.Count == 8; }
            bool single = ReferenceEquals(MapStage.Stage11.Lanes, Map11.Roads) && Shape(MapStage.Stage11);
            bool okD = single;
            var parts = new List<string>();
            foreach (var (key, r) in ChapterRule.Versions.Where(v => string.CompareOrdinal(v.Key, "V461") >= 0 && string.CompareOrdinal(v.Key, "V474") < 0))
            {
                MapStage s11 = Chapter1.StagesFor(r)[0];
                MapStage want = key == "V471" ? MapStage.Stage11Ch471 : key == "V470" ? MapStage.Stage11Ch470 : MapStage.Stage11Ch;
                bool same = Shape(s11) && Line(s11) == Line(want);
                okD &= same;
                parts.Add($"{key} {(same ? "今のまま" : "×")}");
            }
            bool others = Chapter1.StagesFor(rule).Skip(1).Zip(Chapter1.StagesFor(ChapterRule.V471).Skip(1)).All(p => ReferenceEquals(p.First, p.Second));
            write($"  (d) 単発の 1-1 {(single ? "今のまま" : "×")}・{string.Join("・", parts)}（盤は 3 マス × 2 本・敵と湧きの名前）・V474 の 1-2 ／ 1-3 は V471 と同じもの {others}");
            Req(okD && others, "(d) 単発の 1-1・第461〜471期の 1 章は今のまま（V474 の 1-2 ／ 1-3 も第471期と同じ）");
        }

        if (!bots) return ok;
        int crashed = 0;
        try { Weigh(write, demoDir, rule); }
        catch (Exception ex) { crashed++; write($"  × 重さの調べ: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        try
        {
            MapStage old = Chapter1.StagesFor(ChapterRule.V471)[0];
            Routes(write, s); Routes(write, s, teamB: true);
            // 比べる相手: 最初に測った「関も歩く ／ 全部抜いて勝ち」の盤（関の重さは今と同じ）と、第471期の盤
            MapStage walk = MapStage.Stage11Ch474With(MapStage.Stage11Ch474GateBand, MapStage.Stage11Ch474GateHp, MapStage.Stage11Ch474GateAtk, gateHolds: false, winOnCapture: false)
                .Scaled(rule.ScaleOf(0)).Starred(rule.Stars ?? StarRule.Off).Rostered(rule.Roster ?? RosterRule.Off);
            Routes(write, walk, "（比べる相手）関も歩く ／ 全部抜いて勝ち"); Routes(write, walk, "（比べる相手）関も歩く ／ 全部抜いて勝ち", teamB: true);
            Routes(write, old, "（比べる相手）第471期の盤・3 マス × 2 本"); Routes(write, old, "（比べる相手）第471期の盤・3 マス × 2 本", teamB: true);
        }
        catch (Exception ex) { crashed++; write($"  × 通し: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        Req(crashed == 0, "器具（重さの調べ・通し × 100 seed）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }

    private static string Band(string id) => EnemyCatalog.MapSquadOf(id).Name;

    internal static double Med(IEnumerable<double> xs)
    {
        var o = xs.OrderBy(x => x).ToList();
        return o.Count == 0 ? double.NaN : o.Count % 2 == 1 ? o[o.Count / 2] : (o[o.Count / 2 - 1] + o[o.Count / 2]) / 2;
    }

    // ---------------- §1 の重さの調べ（峠の関の候補） ----------------

    /// <summary>候補の部隊（波ルールなしの雑兵・`EnemyCatalog.MapSquads`）。本陣の守り 2 つは 1-2 ／ 1-3 の本陣の相手なので外した。</summary>
    private static readonly string[] GateBands = { "band_shields", "band_knights", "band_lance", "band_miasma", "band_archers", "band_sellswords", "band_fire" };
    private static readonly int[] GateHps = { 100, 200, 250, 300, 350, 400 };
    private static readonly int[] GateAtks = { 100, 150, 175, 200, 225, 250 };

    /// <summary>
    /// 北の 2 マス目の候補 × 始めの 6 枚の 5 隊（5 枚 × 3・3 枚 × 2・全員☆1・×0.6）。指示書 §1 の帯:
    /// 5 枚の隊 20／20・終わりの HP 55〜75%・決着 6 ターン以上 ／ 3 枚の隊 8／20 以下。
    /// </summary>
    private static void Weigh(Action<string> write, string demoDir, ChapterRule rule)
    {
        string repo = Path.GetFullPath(Path.Combine(demoDir, ".."));
        string root = Path.Combine(repo, "design", "weights", "p471_start6");
        var squads = FoeWeight.SquadsOf(PlayLog.List(root), "1-1", root);
        var big = squads.Where(q => q.Pieces.Count == 5).ToList();
        var small = squads.Where(q => q.Pieces.Count < 5).ToList();
        MapFoeScale sc = rule.ScaleOf(0);
        StarRule stars = rule.Stars ?? StarRule.V464;
        write("");
        write($"  §1 重さの調べ: 峠の関（北の 2 マス目）の候補 × 始めの 6 枚の隊（5 枚 {big.Count}・3 枚 {small.Count}・全員☆1・{sc.Text}・seed {WeighSeeds}）");
        write("  帯: 5 枚の隊 20／20・終わりの HP 55〜75%・決着 6 ターン以上 ／ 3 枚の隊 8／20 以下");
        write("");
        write("  | 部隊 | その区画の 最大HP ／ 攻撃 | 5 枚の隊（勝ち・終わりの HP・決着T） | 3 枚の隊（勝ち・終わりの HP） | 帯 |");
        write("  |---|--:|---|---|---|");
        foreach (string band in GateBands)
            foreach (int atk in GateAtks)
            foreach (int pct in GateHps)
            {
                MapStage stage = MapStage.Stage11Ch474With(band, pct, atk).Scaled(sc);
                var foe = new FoeWeight.Foe(FoeWeight.Kind.Road, stage.Lanes[0][1].Name, stage.Lanes[0][1].Enemy);
                (int Wins, double Hp, double Turns) One(FoeWeight.Squad q)
                {
                    var fs = Enumerable.Range(0, WeighSeeds).Select(seed => FoeWeight.Run(FoeWeight.Players(q, 1, stars), FoeWeight.Enemies(foe, stage), seed)).ToList();
                    return (fs.Count(f => f.Won), Med(fs.Select(f => f.Hp)), Med(fs.Select(f => (double)f.Turns)));
                }
                var b = big.Select(One).ToList();
                var m = small.Select(One).ToList();
                bool bigOk = b.All(x => x.Wins == WeighSeeds && x.Hp >= 55 && x.Hp <= 75 && x.Turns >= 6);
                // 「3 枚の隊は 8／20 以下」の線は外した（ポン 2026-11-14）——3 枚の隊の数は並べるだけ。
                bool smallOk = true;
                string why = bigOk && smallOk ? "○"
                    : string.Join("・", new[]
                    {
                        b.Any(x => x.Wins < WeighSeeds) ? "5 枚が負ける" : null,
                        b.Any(x => x.Hp > 75) ? "5 枚に軽い" : null,
                        b.All(x => x.Wins == WeighSeeds) && b.Any(x => x.Hp < 55) ? "5 枚に重い" : null,
                        b.Any(x => x.Turns < 6) ? "決着が早い" : null,
                        smallOk ? null : "3 枚で抜ける",
                    }.Where(x => x is not null));
                write($"  | {foe.Name} | {pct}% ／ {atk}% | {string.Join(" ／ ", b.Select(x => $"{x.Wins}／{WeighSeeds}・{x.Hp:F0}%・{x.Turns:0.#}T"))} | {string.Join(" ／ ", m.Select(x => $"{x.Wins}／{WeighSeeds}・{x.Hp:F0}%"))} | {why} |");
            }
    }

    // ---------------- §1 の通し（北だけ ／ 南だけ ／ 2 隊に分ける） ----------------

    internal enum Plan { North, South, Split, Turtle, Hold }

    internal static string PlanName(Plan p) => p switch
    {
        Plan.North => "北だけ（主力 5 枚が北・残りは拠点で番）",
        Plan.South => "南だけ（主力 5 枚が南・残りは拠点で番）",
        Plan.Split => "2 隊に分ける（主力 5 枚が北・3 枚が南・拠点は空）",
        _ => "（参考）拠点で迎え撃ってから出る（道の敵が尽きたら主力が北へ）",
    };

    internal sealed class Result
    {
        public bool Won, Fallen;
        public int CaptureTurn = -1, EndTurn, Lost, Battles, Spawns, Recruits;
        public bool MainLost, SmallLost, GateCleared;
        /// <summary>終わりの金（持ち越す余り）。</summary>
        public int Gold;
        /// <summary>相手の名前 → その戦闘の後の隊の HP（%）と失った枚数（隊ごと）。</summary>
        public List<(string Foe, int Squad, bool Won, double Hp, int Lost, int Turns, bool AtHome)> Fights = new();
        /// <summary>第476期の測りが読む列（<see cref="Fights"/> と同じ順）: 入る前の隊の HP（%）・生きている枚数・相手が湧いた部隊か。</summary>
        public List<(double HpIn, int AliveIn, bool Spawn)> Ins = new();
    }

    /// <summary>主力の席（`design/weights/p471_start6` の第1隊と同じ）。</summary>
    private static readonly (string Name, int Slot)[] MainSeats =
        { ("泥人形ムド", 0), ("のろまの巨兵ドルガ", 1), ("鬨の号令ガン", 2), ("墓守リィカ", 3), ("施しのリリ", 4) };

    /// <summary>組 B（`design/weights/p471_start6` の第4隊 ／ 第5隊）: 攻めの 3 枚に呼んだ 2 枚を足して主力に、守りの 3 枚を 3 枚の隊に。</summary>
    private static readonly (string Name, int Slot)[] MainSeatsB = { ("のろまの巨兵ドルガ", 0), ("鬨の号令ガン", 2), ("墓守リィカ", 3) };
    private static readonly (string Name, int Slot)[] SmallSeatsB = { ("泥人形ムド", 0), ("胞子体ムグ", 1), ("施しのリリ", 4) };

    private static double HpPct(Map11State st, int k) => st.Squads[k].Units is { } us && us.Sum(x => x.MaxHp) > 0
        ? 100.0 * us.Where(x => x.IsAlive).Sum(x => x.Hp) / us.Sum(x => x.MaxHp) : 0;

    /// <summary>
    /// 1 通し。始めの 6 枚から主力 5 枚（第1隊）を組み、金 8 で 2 枚呼んで残りの 1 枚と 3 枚の隊（第2隊）にする。
    /// 行き先は方針のとおり——制圧の後は盤に残った敵部隊のいちばん近いものへ。戦闘は器具（台本なし）。
    /// </summary>
    internal static Result Play(MapStage stage, int chapterSeed, Plan plan, Action<Map11State>? watch = null, int endless = 0, bool teamB = false)
    {
        var st = new Map11State(Chapter1.StageSeed(chapterSeed, 0, 0), stage.Time, stage.Portal, draft: true, stage: stage);
        MapGraph g = st.Graph;
        var res = new Result();
        void Seat(int squad, (string Name, int Slot)[] seats)
        {
            foreach (var (name, slot) in seats)
            {
                int b = st.Bench.FindIndex(u => u.Def.Name == name);
                if (b >= 0) st.SwapWithBench(squad, slot, b);
            }
        }
        // 組 A: 主力 ＝ 始めの 5 枚・3 枚の隊 ＝ ムグ ＋ 呼んだ 2 枚 ／ 組 B: 主力 ＝ 攻めの 3 枚 ＋ 呼んだ 2 枚・3 枚の隊 ＝ 守りの 3 枚（ムド・ムグ・リリ）
        Seat(0, teamB ? MainSeatsB : MainSeats);
        if (teamB) Seat(1, SmallSeatsB);
        while (st.Recruit(0)) res.Recruits++;
        foreach (int slot in new[] { 0, 1, 3, 2, 4 })
            if (st.Bench.Count > 0 && st.UnitAt(teamB ? 0 : 1, slot) is null) st.SwapWithBench(teamB ? 0 : 1, slot, 0);

        void Fight(int squad, Map11State.Node node, bool intercept)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            if (prep is not { } q) return;
            int alive0 = q.Players.Count(u => u.IsAlive);
            int max0 = q.Players.Sum(u => u.MaxHp);
            res.Ins.Add((max0 == 0 ? 0 : 100.0 * q.Players.Where(u => u.IsAlive).Sum(u => u.Hp) / max0, alive0, node.FromPortal));
            BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
            // 戦闘の直後（道中の回復が乗る前）の HP。
            int max = q.Players.Sum(u => u.MaxHp);
            double hp = max == 0 ? 0 : 100.0 * q.Players.Where(u => u.IsAlive).Sum(u => u.Hp) / max;
            int alive1 = q.Players.Count(u => u.IsAlive);
            st.Resolve(squad, q.Node, r.PlayerWon, intercept);
            res.Fights.Add((node.Def.Name, squad, r.PlayerWon, hp, alive0 - alive1, r.Turns, intercept));
        }

        Dest Nearest(int i)
        {
            Map11State.Squad q = st.Squads[i];
            Map11State.Node? best = null;
            int bestLen = int.MaxValue;
            for (int road = 0; road < g.Lanes.Count; road++)
                foreach (Map11State.Node n in st.FoesOn(road))
                {
                    if (n.Pos == g.Home) continue;
                    int len = g.Path(q.Pos, q.Road, n.Pos, n.Pos == g.Portal ? n.Def.Road : -1).Count;
                    if (len > 0 && len < bestLen) { bestLen = len; best = n; }
                }
            return best is null ? Dest.Home : best.Pos == g.Portal ? Dest.Portal(g, best.Def.Road) : Dest.Of(g, best.Pos);
        }

        Dest DestFor(int i)
        {
            if (st.Squads[i].Lost) return Dest.Home;
            bool roadFoes = Enumerable.Range(0, g.Lanes.Count).Any(r => st.NextNode(r) is not null);
            if (plan == Plan.Hold) return Dest.Home;
            if (i == 0)
            {
                if (st.Captured) return Nearest(0);
                if (plan == Plan.Turtle && roadFoes) return Dest.Home;
                return Dest.Portal(g, plan == Plan.South ? 1 : 0);
            }
            if (plan == Plan.Split) return st.Captured ? Nearest(i) : st.NextNode(1) is not null || st.FoesOn(1).Any() ? Dest.Portal(g, 1) : Dest.Home;
            return Dest.Home;
        }

        int cap = endless > 0 ? endless : 40;
        while ((endless > 0 || !st.Finished) && st.Turn < cap && !st.Fallen)
        {
            for (int i = 0; i < st.SquadCap && !st.Finished; i++)
            {
                Dest dest = DestFor(i);
                if (Map11Orders.Apply(st, i, dest, Map11Orders.Plan(st, i, dest)) is { } node) Fight(i, node, false);
            }
            if (st.Finished && endless == 0) break;
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (st.Fallen) break;
                if (!e.Intercept) { Fight(e.Squad, e.Node, false); continue; }
                while (!st.Fallen && st.NextInterceptor(e.Node) is var j && j >= 0)
                {
                    Fight(j, e.Node, true);
                    if (e.Node.Cleared) break;
                }
                st.CloseIntercept(e.Node);
            }
            watch?.Invoke(st);
        }
        res.Won = st.Won; res.Fallen = st.Fallen;
        res.CaptureTurn = st.Captured ? st.CapturedTurn + 1 : -1;
        res.EndTurn = st.Turn; res.Battles = st.Battles; res.Spawns = st.SpawnCount;
        res.MainLost = st.Squads[0].Lost; res.SmallLost = st.Squads[1].Lost;
        res.Lost = st.Squads.Take(st.SquadCap).Sum(q => q.Units?.Count(u => !u.IsAlive) ?? 0) + st.FallenUnits.Count;
        res.Gold = st.Gold;
        res.GateCleared = st.Nodes[0].Length > 1 && st.Nodes[0][1].Cleared;
        return res;
    }

    internal static void Routes(Action<string> write, MapStage stage, string label = "第474期の盤", bool teamB = false)
    {
        write("");
        write($"  §1 通し・{label}（1 章の seed 0〜{Seeds - 1}・始めの 6 枚 ＋ 金 8 で 2 枚呼ぶ・{(teamB ? "組 B: 主力 ＝ ドルガ・ガン・リィカ ＋ 呼んだ 2 枚 ／ 3 枚の隊 ＝ ムド・ムグ・リリ" : "組 A: 主力 ＝ ムド・ドルガ・ガン・リィカ・リリ ／ 3 枚の隊 ＝ ムグ ＋ 呼んだ 2 枚")}・戦闘は器具）");
        write("");
        write("  | 方針 | 勝ち | 陥落 | 制圧した作戦ターン（中央値・範囲） | 終わった作戦ターン（中央値） | 失った枚数（平均） | 主力が全滅 | 3 枚の隊が全滅 | 戦闘（平均） | 湧き（平均） | 金の余り（勝った通しの平均） | 関を抜いた |");
        write("  |---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var all = new Dictionary<Plan, List<Result>>();
        foreach (Plan plan in new[] { Plan.North, Plan.South, Plan.Split, Plan.Turtle })
        {
            var rs = Enumerable.Range(0, Seeds).Select(c => Play(stage, c, plan, teamB: teamB)).ToList();
            all[plan] = rs;
            var won = rs.Where(r => r.Won).ToList();
            var caps = rs.Where(r => r.CaptureTurn > 0).Select(r => (double)r.CaptureTurn).ToList();
            write($"  | {PlanName(plan)} | {won.Count} | {rs.Count(r => r.Fallen)} | {(caps.Count == 0 ? "—" : $"{Med(caps):0.#}（{caps.Min():0}〜{caps.Max():0}）")} | {(won.Count == 0 ? "—" : $"{Med(won.Select(r => (double)r.EndTurn)):0.#}")}"
                  + $" | {rs.Average(r => r.Lost):F2} | {rs.Count(r => r.MainLost)} | {rs.Count(r => r.SmallLost)} | {rs.Average(r => r.Battles):F1} | {rs.Average(r => r.Spawns):F1}"
                  + $" | {(won.Count == 0 ? "—" : $"{won.Average(r => r.Gold):F1}")} | {rs.Count(r => r.GateCleared)} |");
        }
        write("");
        write("  相手ごと（勝ち ／ 戦闘・その戦闘の後の隊の HP の中央値・失った枚数の平均・決着T の中央値）");
        write("");
        write("  | 方針 | 隊 | 相手 | どこで | 勝ち ／ 戦闘 | 後の HP | 失った枚数 | 決着T |");
        write("  |---|---|---|---|--:|--:|--:|--:|");
        foreach (var (plan, rs) in all)
            foreach (var grp in rs.SelectMany(r => r.Fights).GroupBy(f => (f.Squad, f.Foe, f.AtHome)).OrderBy(x => x.Key.Squad).ThenBy(x => x.Key.Foe, StringComparer.Ordinal).ThenBy(x => x.Key.AtHome))
                write($"  | {PlanName(plan).Split('（')[0]}{(plan == Plan.Turtle ? "（迎え撃つ）" : "")} | {(grp.Key.Squad == 0 ? "主力 5 枚" : "3 枚の隊")} | {grp.Key.Foe} | {(grp.Key.AtHome ? "拠点（迎撃）" : "道の上")}"
                      + $" | {grp.Count(f => f.Won)} ／ {grp.Count()} | {Med(grp.Select(f => f.Hp)):F0}% | {grp.Average(f => (double)f.Lost):F2} | {Med(grp.Select(f => (double)f.Turns)):0.#} |");
    }
}
