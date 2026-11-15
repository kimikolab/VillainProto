using BattleCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Plan = Chapter1AsymSmoke.Plan;

// =====================================================================================
// 第476期の門 `--chapter1-reason-smoke`（指示書 design/PHASE476_STAGE11_REASON_SPEC.md §5・画面なし）
//
//   (a) 1-1 の金: 雑兵 ＋4・関 ＋14・本陣の守り ＋4
//   (b) 始めの 4 部隊は動かない。湧きは 6 刻目から歩く。1 刻目に拠点を空にしても陥落しない
//   (c) 本陣に守りがいて、勝つと制圧で勝ち
//   (d) 関の名前が「峠の関」。頭の札の 3 行
//   (e) 1-2・1-3・単発・第461〜475期の 1 章は今のまま
//
// 加えて §3 の測り（`--chapter1-reason-quick` は省く）:
//   §3-1 重さ: 本陣の守り（関を抜いた後の主力）・6 刻目の湧き（拠点に残した 3 枚の隊 ／ 1 枚）
//   §3-2 通し: 北だけ ／ 南だけ ／ 2 隊に分ける × 組 A ／ 組 B × 100 seed と線・賞金 12／14／16 × 湧き 5／6／7 刻目の表
// **規則は1つも持たない。ここには Godot の型を1つも使わない。**
// =====================================================================================

public static class Chapter1ReasonSmoke
{
    private const int Seeds = 100;
    private const int WeighSeeds = 20;

    public static bool Run(Action<string> write, string demoDir, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V476;
        MapStage s = Chapter1.StagesFor(rule)[0];
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}");

        // ---- (a) 金 ----
        {
            var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
            var all = st.Nodes.SelectMany(r => r).ToList();
            string line = string.Join("・", all.Select(n => $"{n.Def.Name} ＋{st.KillGold(n)}"));
            int[] want = { 4, 14, 4, 4, 4 };
            write($"  (a) {line}（合計 {all.Sum(n => st.KillGold(n))}・道の 4 部隊 {all.Where(n => !n.Def.Stationary).Sum(n => st.KillGold(n))}）・始めの金 {st.Gold}");
            Req(all.Select(n => st.KillGold(n)).SequenceEqual(want) && st.Gold == 8, "(a) 1-1 の金: 雑兵（物見の兵・徴募兵・騎士の小隊）＋4・峠の関 ＋14・本陣の守り ＋4");
        }

        // ---- (b) 待ち構える・湧きは 6 刻目から歩く・空の拠点は 1 刻目に落ちない ----
        {
            var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);   // 隊を 1 つも組まない ＝ 拠点は空
            MapGraph g = st.Graph;
            var road = st.Nodes.SelectMany(r => r).Where(n => !n.Def.Stationary).ToList();
            int[] at0 = road.Select(n => n.Pos).ToArray();
            bool holds = road.Count == 4 && road.All(n => n.Def.Hold && st.TurnsToHome(n) is null) && st.PlannedFoeSteps().Count == 0;
            int firstSpawnTurn = -1, fellAt = -1, cooldown0 = st.PortalCooldown;
            var trace = new List<string>();
            bool still = true;
            for (int k = 0; k < 12 && !st.Fallen; k++)
            {
                st.AdvanceFoes();
                still &= road.Select(n => n.Pos).SequenceEqual(at0);
                if (firstSpawnTurn < 0 && st.SpawnCount > 0) firstSpawnTurn = st.Turn;
                if (st.Spawns.FirstOrDefault() is { } sp) trace.Add(sp.Pos == g.Portal ? "本陣" : sp.Pos == g.Home ? "拠点" : g[sp.Pos].Name);
                if (st.Fallen) fellAt = st.Turn;
            }
            write($"  (b) 始めの 4 部隊: 動かない {holds}・12 刻のあいだ位置は同じ {still}・湧くまで あと {cooldown0}（{cooldown0 + 1} 刻目）・最初の湧き 作戦ターン {firstSpawnTurn + 1}（{st.Spawns.FirstOrDefault()?.Def.Name}・{g.LaneNames[st.Spawns.FirstOrDefault()?.Def.Road ?? 0]}）"
                  + $"・歩み {string.Join(" → ", trace)}・空の拠点が落ちた作戦ターン {(fellAt < 0 ? "—" : (fellAt + 1).ToString())}");
            Req(holds && still && cooldown0 == 5 && firstSpawnTurn == 5 && fellAt == 8 && s.SpawnsPassHolds && s.WarnEmptyHome
                && s.Spawn(0).Name == Band(MapStage.Stage11Ch476SpawnBand) && s.Spawn(1).Name == Band("band_sellswords"),
                "(b) 始めの 4 部隊は動かない。湧きは 6 刻目から歩く（関の脇を抜けて 3 歩で拠点）。拠点を空にしても 1 刻目には陥落しない（落ちるのは湧きが着く 9 刻目）");
        }

        // ---- (c) 本陣の守り ----
        {
            var st0 = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
            Map11State.Node? gar = st0.Nodes.SelectMany(r => r).FirstOrDefault(n => n.Def.Stationary);
            bool placed = gar is not null && gar.Pos == st0.Graph.Portal && ReferenceEquals(st0.PortalFoe(), gar) && s.WinOnCapture;
            var north = Chapter1AsymSmoke.Play(s, 0, Plan.North);
            var south = Chapter1AsymSmoke.Play(s, 0, Plan.South);
            bool fought(Chapter1AsymSmoke.Result r) => r.Fights.Count > 0 && r.Fights[^1].Foe == gar?.Def.Name && r.Fights[^1].Won && r.Won && r.CaptureTurn == r.EndTurn + 1;
            write($"  (c) 本陣の守り: {gar?.Def.Name}（{string.Join("・", gar?.Def.Enemy.Occupied().Select(o => o.Def.Name) ?? Array.Empty<string>())}）・敵の拠点のマス {placed}"
                  + $"・北だけ（seed 0）: {string.Join(" → ", north.Fights.Select(f => f.Foe + (f.Won ? "○" : "×")))}・制圧 {north.CaptureTurn} 刻目・勝ち {north.Won}"
                  + $" ／ 南だけ: {string.Join(" → ", south.Fights.Select(f => f.Foe + (f.Won ? "○" : "×")))}・制圧 {south.CaptureTurn} 刻目・勝ち {south.Won}");
            Req(placed && fought(north) && fought(south), "(c) 本陣に守りがいて（北回りでも南回りでも最後に 1 戦）、勝つとその刻に制圧で勝ち");
        }

        // ---- (d) 名前と頭の札 ----
        {
            var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
            Map11State.Node gate = st.Nodes[0][1];
            string body = Chapter1.Intro(s).Body;
            string[] want =
            {
                "敵の本陣を制圧したら勝ち。",
                "北は近道だが峠の関が硬い（抜けば賞金）。南は回り道だが軽い。",
                "6 刻目から、敵の本陣から部隊が歩いてくる。拠点を空にしないこと。",
            };
            write($"  (d) 北の 2 マス目の名前「{gate.Def.Name}」・札「{gate.Def.Tag}」・{gate.Def.Note}・北の 1 マス目「{st.Nodes[0][0].Def.Name}」・頭の札:");
            foreach (string l in body.Split('\n')) write($"        {l}");
            Req(gate.Def.Name == "峠の関" && gate.Def.Tag == "硬い" && gate.Def.Note.Contains("×2.5") && gate.Def.Note.Contains("×2") && gate.Def.Note.Contains("盾の列")
                && st.Nodes[0][0].Def.Name == "物見の兵" && want.All(body.Contains),
                "(d) 関の名前が「峠の関」（硬い・最大HP ×2.5・攻撃 ×2・中身は盾の列）。「第一波」は「物見の兵」。頭の札に 3 行");
        }

        // ---- (e) ほかは今のまま ----
        {
            string Line(MapStage x) => $"{string.Join(" ／ ", x.Lanes.Select(l => string.Join("→", l.Select(n => $"{n.Name}{(n.Hold ? "(待)" : "")}{(n.Gold >= 0 ? $"+{n.Gold}" : "")}"))))} ／ 湧き {x.Spawn(0).Name}・{x.Spawn(1).Name} ／ {x.Portal}・最初 {x.Portal.FirstSpawn} ／ 抜け {x.SpawnsPassHolds} ／ 行 {x.Notes.Count}";
            bool single = ReferenceEquals(MapStage.Stage11.Lanes, Map11.Roads) && MapStage.Stage11.Portal.FirstSpawn == 0 && !MapStage.Stage11.SpawnsPassHolds && MapStage.Stage11.Notes.Count == 0;
            bool okE = single;
            var parts = new List<string>();
            foreach (var (key, r) in ChapterRule.Versions.Where(v => string.CompareOrdinal(v.Key, "V461") >= 0 && string.CompareOrdinal(v.Key, "V476") < 0))
            {
                MapStage s11 = Chapter1.StagesFor(r)[0];
                MapStage want = key is "V474" or "V475" ? MapStage.Stage11Ch474 : key == "V471" ? MapStage.Stage11Ch471 : key == "V470" ? MapStage.Stage11Ch470 : MapStage.Stage11Ch;
                bool same = Line(s11) == Line(want.Scaled(r.ScaleOf(0))) && s11.Lanes.SelectMany(l => l).All(n => n.Gold < 0 && n.Tag == "" && n.Note == "")
                            && s11.Portal.FirstSpawn == 0 && !s11.SpawnsPassHolds && !s11.WarnEmptyHome && s11.Notes.Count == 0;
                okE &= same;
                parts.Add($"{key} {(same ? "今のまま" : "×")}");
            }
            // 第474期の盤の関は今も ＋6・歩く 3 部隊は歩く
            {
                MapStage s474 = Chapter1.StagesFor(ChapterRule.V475)[0];
                var st = new Map11State(0, s474.Time, s474.Portal, draft: true, stage: s474);
                bool old = st.KillGold(st.Nodes[0][1]) == 6 && st.KillGold(st.Nodes[0][0]) == 6 && st.PlannedFoeSteps().Count == 2 && st.PortalCooldown == 8
                           && st.Nodes[0][1].Def.Name == EnemyCatalog.MapSquadOf(MapStage.Stage11Ch474GateBand).Name;
                okE &= old;
                parts.Add($"V475 の 1-1: 関 ＋{st.KillGold(st.Nodes[0][1])}・次の刻に歩く敵 {st.PlannedFoeSteps().Count}・湧くまで {st.PortalCooldown} {(old ? "今のまま" : "×")}");
            }
            bool others = Chapter1.StagesFor(rule).Skip(1).Zip(Chapter1.StagesFor(ChapterRule.V475).Skip(1)).All(p => ReferenceEquals(p.First, p.Second));
            bool roster = ReferenceEquals(rule.Roster, ChapterRule.V475.Roster) && rule.Scales == ChapterRule.V475.Scales;
            write($"  (e) 単発の 1-1 {(single ? "今のまま" : "×")}・{string.Join("・", parts)}・V476 の 1-2 ／ 1-3 は V475 と同じもの {others}・加入の表と倍率は V475 と同じ {roster}");
            Req(okE && others && roster, "(e) 1-2・1-3・単発・第461〜475期の 1 章は今のまま");
        }

        // ---- 不具合 3: 1 章の手札で「既定の組み方で 2 隊を組む」 ----
        {
            var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
            int fill = st.DefaultSquadsFilled;
            bool done = false;
            string err = "";
            try { done = st.UseDefaultSquads(); } catch (Exception ex) { err = ex.GetType().Name; }
            string seats = string.Join(" ／ ", st.Squads.Take(2).Select(q => string.Join("・", (q.Units ?? new List<UnitState>()).OrderBy(u => u.Slot).Select(u => $"{u.Def.Name}@{u.Slot}"))));
            write($"  不具合 3: 既定の組み方で {fill} 隊を組む → {(err.Length > 0 ? "落ちた " + err : done.ToString())}・{seats}・控え {st.Bench.Count}");
            Req(done && fill == 2 && st.Squads[0].Units is { Count: 5 } && st.Squads[1].Units is { Count: 1 } && st.Bench.Count == 0 && st.CanSend(0) && st.CanSend(1),
                "「既定の組み方で 2 隊を組む」が 1 章の手札でも落ちず、主力 5 枚 ＋ 1 枚に配る（第475期までは各隊 1 枚で止まっていた）");
        }

        if (!bots) return ok;
        int crashed = 0;
        bool lines = true;
        try { lines &= Weigh(write, demoDir, rule, s); }
        catch (Exception ex) { crashed++; write($"  × 重さ: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        try { lines &= Routes(write, rule, s); }
        catch (Exception ex) { crashed++; write($"  × 通し: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        Req(crashed == 0, "器具（重さ・通し × 100 seed）が落ちない");
        write($"{(lines ? "○" : "×")} §3 の線（重さ 2 本・通し 3 本・どれか 1 つの方針が全部の列で勝っていない）{(lines ? "" : "——通っていない（門の合否には入れない・報告を見る）")}");
        return ok;
    }

    private static string Band(string id) => EnemyCatalog.MapSquadOf(id).Name;
    private static double Med(IEnumerable<double> xs) => Chapter1AsymSmoke.Med(xs);

    private static MapStage Dress(MapStage raw, ChapterRule rule)
        => raw.Scaled(rule.ScaleOf(0)).Starred(rule.Stars ?? StarRule.Off).Rostered(rule.Roster ?? RosterRule.Off);

    // ---------------- §3-1 重さ ----------------

    private static bool Weigh(Action<string> write, string demoDir, ChapterRule rule, MapStage s)
    {
        string repo = Path.GetFullPath(Path.Combine(demoDir, ".."));
        string root = Path.Combine(repo, "design", "weights", "p471_start6");
        var squads = FoeWeight.SquadsOf(PlayLog.List(root), "1-1", root);
        var small = squads.Where(q => q.Pieces.Count < 5).ToList();
        StarRule stars = rule.Stars ?? StarRule.V464;
        bool ok = true;
        write("");
        write($"  §3-1 重さ（始めの 6 枚・全員☆1・{rule.ScaleOf(0).Text}・seed 0〜{WeighSeeds - 1}）");
        var big = squads.Where(q => q.Pieces.Count == 5).ToList();
        write("");
        write("  本陣の守り: 関を抜いた後の主力（4 枚・HP 60%）——5 枚の隊から 1 枚を外し（5 通り）、残りの 4 枚を最大HP の 60% にして当てる。線: どの外し方でも 18／20 以上");
        write("");
        write("  | 本陣の守り | 5 枚の隊 | 外した 1 枚ごとの勝ち（／20） | 後の HP（中央値の幅） | 線 |");
        write("  |---|---|---|--:|---|");
        foreach (string band in new[] { "band_levy", "band_sellswords" })
        {
            MapStage stage = Dress(MapStage.Stage11Ch476With(garrison: band), rule);
            var foe = new FoeWeight.Foe(FoeWeight.Kind.Garrison, stage.Lanes[1][^1].Name, stage.Lanes[1][^1].Enemy);
            bool chosen = band == MapStage.Stage11Ch476GarrisonBand;
            foreach (FoeWeight.Squad q in big)
            {
                var cells = new List<(string Out, int Wins, double Hp)>();
                foreach (FoeWeight.Piece drop in q.Pieces)
                {
                    var four = new FoeWeight.Squad(q.Source, q.No, q.Pieces.Where(p => !ReferenceEquals(p, drop)).ToList());
                    var fs = Enumerable.Range(0, WeighSeeds).Select(seed =>
                    {
                        List<UnitState> us = FoeWeight.Players(four, 1, stars);
                        foreach (UnitState u in us) u.Hp = Math.Max(1, (int)Math.Round(u.MaxHp * 0.6));
                        // 後の HP は満タンの最大HP に対する割合（`FoeWeight.Run` と同じ式）
                        return FoeWeight.Run(us, FoeWeight.Enemies(foe, stage), seed);
                    }).ToList();
                    cells.Add((drop.Base.Name, fs.Count(f => f.Won), Med(fs.Select(f => f.Hp))));
                }
                bool pass = cells.All(c => c.Wins >= 18);
                if (chosen) ok &= pass;
                write($"  | {Band(band)}{(chosen ? "（採る）" : "")} | {q.Name}（{string.Join("・", q.Pieces.Select(p => p.Base.Name))}） | {string.Join("・", cells.Select(c => $"{c.Out}抜き {c.Wins}"))} | {cells.Min(c => c.Hp):F0}〜{cells.Max(c => c.Hp):F0}% | {(pass ? "○" : "×")} |");
            }
        }
        write("");
        write("  （並べるだけ）通し「北だけ」の seed 0〜19 で、物見の兵 → 峠の関を抜いて本陣に入った主力そのもの");
        write("");
        write("  | 本陣の守り | 組 | 勝ち ／ 入った通し | 入る前の主力（生きている枚数の中央値・HP の中央値） | 後の HP（中央値） | 失った枚数（平均） |");
        write("  |---|---|--:|--:|--:|--:|");
        foreach (string band in new[] { "band_levy", "band_sellswords" })
            foreach (bool teamB in new[] { false, true })
            {
                MapStage stage = Dress(MapStage.Stage11Ch476With(garrison: band), rule);
                string name = stage.Lanes[1][^1].Name;
                var rows = new List<(bool Won, double HpIn, int AliveIn, double Hp, int Lost)>();
                for (int c = 0; c < WeighSeeds; c++)
                {
                    var r = Chapter1AsymSmoke.Play(stage, c, Plan.North, teamB: teamB);
                    for (int i = 0; i < r.Fights.Count; i++)
                        if (r.Fights[i].Foe == name && r.Fights[i].Squad == 0) rows.Add((r.Fights[i].Won, r.Ins[i].HpIn, r.Ins[i].AliveIn, r.Fights[i].Hp, r.Fights[i].Lost));
                }
                write($"  | {Band(band)}{(band == MapStage.Stage11Ch476GarrisonBand ? "（採る）" : "")} | {(teamB ? "B" : "A")} | {rows.Count(x => x.Won)} ／ {rows.Count} | {(rows.Count == 0 ? "—" : $"{Med(rows.Select(x => (double)x.AliveIn)):0.#} 枚・{Med(rows.Select(x => x.HpIn)):F0}%")}"
                      + $" | {(rows.Count == 0 ? "—" : $"{Med(rows.Select(x => x.Hp)):F0}%")} | {(rows.Count == 0 ? "—" : $"{rows.Average(x => (double)x.Lost):F2}")} |");
            }

        write("");
        write("  6 刻目の湧き（北の道）: 拠点に残した隊。線: 3 枚の隊 15／20 以上・1 枚では負ける（10／20 以下）");
        write("");
        var singles = squads.SelectMany(q => q.Pieces).GroupBy(p => p.Base.Name).Select(g => g.First()).ToList();
        write($"  | 湧く部隊 | {string.Join(" | ", small.Select(q => "3 枚: " + string.Join("・", q.Pieces.Select(p => p.Base.Name))))} | {string.Join(" | ", singles.Select(p => "1 枚: " + p.Base.Name))} | 線 |");
        write($"  |---|{string.Concat(small.Select(_ => "--:|"))}{string.Concat(singles.Select(_ => "--:|"))}---|");
        string now = s.Spawn(0).Name;
        foreach (string band in new[] { "band_levy", "band_sellswords", "band_knights", "band_archers", "band_shields" })
        {
            EnemyCatalog.MapSquad m = EnemyCatalog.MapSquadOf(band);
            var spawn = new FoeWeight.Foe(FoeWeight.Kind.Spawn, m.Name, rule.ScaleOf(0).Foe(m.Enemy));
            (int Wins, double Hp) One(FoeWeight.Squad q)
            {
                var fs = Enumerable.Range(0, WeighSeeds).Select(seed => FoeWeight.Run(FoeWeight.Players(q, 1, stars), FoeWeight.Enemies(spawn, s), seed)).ToList();
                return (fs.Count(f => f.Won), Med(fs.Select(f => f.Hp)));
            }
            var three = small.Select(One).ToList();
            var one = singles.Select(p => One(new FoeWeight.Squad("1 枚", 1, new[] { p }))).ToList();
            bool pass = three.All(x => x.Wins >= 15) && one.All(x => x.Wins <= WeighSeeds / 2);
            if (m.Name == now) ok &= pass;
            write($"  | {m.Name}{(m.Name == now ? "（今）" : "")} | {string.Join(" | ", three.Select(x => $"{x.Wins}・{x.Hp:F0}%"))} | {string.Join(" | ", one.Select(x => x.Wins.ToString()))} | {(pass ? "○" : "×")} |");
        }
        return ok;
    }

    // ---------------- §3-2 通し ----------------

    private static readonly Plan[] Plans = { Plan.North, Plan.South, Plan.Split };

    private sealed record Row(Plan Plan, int Wins, int Fallen, double Turn, double Lost, double Gold, double SpawnFights, int SpawnLosses, int MainLost);

    private static List<Row> Measure(MapStage stage, bool teamB)
    {
        var rows = new List<Row>();
        foreach (Plan plan in Plans)
        {
            var rs = Enumerable.Range(0, Seeds).Select(c => Chapter1AsymSmoke.Play(stage, c, plan, teamB: teamB)).ToList();
            var won = rs.Where(r => r.Won).ToList();
            rows.Add(new Row(plan, won.Count, rs.Count(r => r.Fallen),
                won.Count == 0 ? double.NaN : Med(won.Select(r => (double)r.CaptureTurn)),
                rs.Average(r => (double)r.Lost),
                won.Count == 0 ? double.NaN : won.Average(r => (double)r.Gold),
                rs.Average(r => (double)r.Ins.Count(i => i.Spawn)),
                rs.Sum(r => Enumerable.Range(0, r.Fights.Count).Count(i => r.Ins[i].Spawn && !r.Fights[i].Won)),
                rs.Count(r => r.MainLost)));
        }
        return rows;
    }

    /// <summary>どれか 1 つの方針が全部の列（勝ち・制圧の刻・失った枚数・金の余り）でほかの 2 つ以上に並ぶか勝ち、どれかで厳密に勝つ。</summary>
    private static Plan? Dominant(List<Row> rows)
    {
        foreach (Row a in rows)
        {
            bool all = true, strict = false;
            foreach (Row b in rows.Where(x => x.Plan != a.Plan))
            {
                all &= a.Wins >= b.Wins && a.Turn <= b.Turn && a.Lost <= b.Lost && a.Gold >= b.Gold;
                strict |= a.Wins > b.Wins || a.Turn < b.Turn || a.Lost < b.Lost || a.Gold > b.Gold;
            }
            if (all && strict) return a.Plan;
        }
        return null;
    }

    private static string Short(Plan p) => p switch { Plan.North => "北だけ", Plan.South => "南だけ", _ => "2 隊に分ける" };

    private static bool Routes(Action<string> write, ChapterRule rule, MapStage s)
    {
        bool ok = true;
        foreach (bool teamB in new[] { false, true })
        {
            var rows = Measure(s, teamB);
            write("");
            write($"  §3-2 通し・第476期の盤（1 章の seed 0〜{Seeds - 1}・35 体の表・始めの 6 枚 ＋ 金 8 で 2 枚呼ぶ・{(teamB ? "組 B: 主力 ＝ ドルガ・ガン・リィカ ＋ 呼んだ 2 枚 ／ 3 枚の隊 ＝ ムド・ムグ・リリ" : "組 A: 主力 ＝ ムド・ドルガ・ガン・リィカ・リリ ／ 3 枚の隊 ＝ ムグ ＋ 呼んだ 2 枚")}・戦闘は器具）");
            write("");
            write("  | 方針 | 勝ち | 陥落 | 制圧の刻（勝った通しの中央値） | 失った枚数（平均） | 金の余り（勝った通しの平均） | 湧きと戦った回数（平均） | 湧きに負けた戦闘 | 主力が全滅 |");
            write("  |---|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (Row r in rows)
                write($"  | {Chapter1AsymSmoke.PlanName(r.Plan)} | {r.Wins} | {r.Fallen} | {r.Turn:0.#} | {r.Lost:F2} | {r.Gold:F1} | {r.SpawnFights:F2} | {r.SpawnLosses} | {r.MainLost} |");
            Row n = rows[0], so = rows[1];
            Plan? dom = Dominant(rows);
            bool l1 = !teamB || rows.All(r => r.Wins >= 85);
            bool l2 = n.Gold - so.Gold >= 8, l3 = n.Lost - so.Lost >= 1, l4 = dom is null;
            write("");
            write($"  線（組 {(teamB ? "B" : "A")}）: {(teamB ? $"どの方針も勝ち 85 以上 {(l1 ? "○" : "×")}（{string.Join("・", rows.Select(r => r.Wins))}）・" : "")}北だけは南だけより金の余りが 8 以上多い {(l2 ? "○" : "×")}（{n.Gold - so.Gold:+0.0;-0.0}）"
                  + $"・失った枚数が 1 枚以上多い {(l3 ? "○" : "×")}（{n.Lost - so.Lost:+0.00;-0.00}）・全部の列で勝つ方針は無い {(l4 ? "○" : $"×（{Short(dom!.Value)}）")}");
            // 誰が何ターン目に倒れたか（方針 × 相手 × 駒・倒れた通しの割合と、その戦闘の中の平均ターン）
            write("");
            write($"  誰が何 T に倒れたか（組 {(teamB ? "B" : "A")}・{Seeds} 通しのうち倒れた通しの割合・その戦闘の中の平均ターン）");
            write("");
            write("  | 方針 | 相手 | 隊 | 戦闘（勝ち ／ 数） | 倒れた駒（割合・平均 T） |");
            write("  |---|---|---|--:|---|");
            foreach (Plan plan in Plans)
            {
                var rs = Enumerable.Range(0, Seeds).Select(c => Chapter1AsymSmoke.Play(s, c, plan, teamB: teamB)).ToList();
                foreach (var g in rs.SelectMany(r => r.Fights).GroupBy(f => (f.Foe, f.Squad)).OrderBy(g => g.Key.Squad).ThenBy(g => g.Key.Foe, StringComparer.Ordinal))
                {
                    var fell = rs.SelectMany(r => r.Fell).Where(f => f.Foe == g.Key.Foe && f.Squad == g.Key.Squad).GroupBy(f => f.Unit)
                        .OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {100.0 * x.Count() / Seeds:F0}%・{x.Average(f => (double)f.Turn):F1}T").ToList();
                    write($"  | {Short(plan)} | {g.Key.Foe} | {(g.Key.Squad == 0 ? "主力" : "3 枚の隊")} | {g.Count(f => f.Won)} ／ {g.Count()} | {(fell.Count == 0 ? "—" : string.Join(" ／ ", fell))} |");
                }
            }
            // 線は組 B で見る（指示書 §3-2）。組 A は並べるだけ——ただし「全部の列で勝つ方針」はどちらの組でも止める理由。
            if (teamB) ok &= l1 && l2 && l3;
            ok &= l4;
        }

        // 賞金 12／14／16 × 最初の湧き 5／6／7 刻目（組 B・指示書 §3-2 の「振って測った表」）
        write("");
        write("  賞金の額 × 最初の湧きの刻（組 B・勝ち ／ 制圧の刻 ／ 失った枚数 ／ 金の余り を 北だけ ｜ 南だけ ｜ 2 隊に分ける の順）");
        write("");
        write("  | 峠の関の賞金 | 最初の湧き | 北だけ | 南だけ | 2 隊に分ける | 北 − 南（金 ／ 失った枚数） | 全部の列で勝つ方針 |");
        write("  |--:|--:|---|---|---|--:|---|");
        foreach (int gold in new[] { 12, 14, 16 })
            foreach (int first in new[] { 4, 5, 6 })
            {
                var rows = Measure(Dress(MapStage.Stage11Ch476With(gateGold: gold, firstSpawn: first), rule), teamB: true);
                string Cell(Row r) => $"{r.Wins} ／ {r.Turn:0.#} ／ {r.Lost:F2} ／ {r.Gold:F1}";
                bool now = gold == MapStage.Stage11Ch476GateGold && first == MapStage.Stage11Ch476FirstSpawn;
                write($"  | ＋{gold}{(now ? "（今）" : "")} | {first + 1} 刻目 | {Cell(rows[0])} | {Cell(rows[1])} | {Cell(rows[2])} | {rows[0].Gold - rows[1].Gold:+0.0;-0.0} ／ {rows[0].Lost - rows[1].Lost:+0.00;-0.00} | {(Dominant(rows) is { } d ? Short(d) : "なし")} |");
            }

        // 比べる相手: 湧きが待ち構える敵の奥で詰まる盤（第458期からの規則）・第474期の盤
        write("");
        write("  比べる相手（組 B）");
        write("");
        write("  | 盤 | 方針 | 勝ち | 陥落 | 制圧の刻 | 失った枚数 | 金の余り | 湧きと戦った回数 |");
        write("  |---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (label, stage) in new[]
                 {
                     ("湧きは待ち構える敵の奥で詰まる（ほかは今と同じ）", Dress(MapStage.Stage11Ch476With(spawnsPass: false), rule)),
                     ("本陣の守りなし（ほかは今と同じ）", Dress(MapStage.Stage11Ch476With(garrison: null), rule)),
                     ("北の湧きは徴募兵（第471期のまま・ほかは今と同じ）", Dress(MapStage.Stage11Ch476With(spawn: "band_levy"), rule)),
                     ("第474期の盤（V475）", Chapter1.StagesFor(ChapterRule.V475)[0]),
                 })
            foreach (Row r in Measure(stage, teamB: true))
                write($"  | {label} | {Short(r.Plan)} | {r.Wins} | {r.Fallen} | {r.Turn:0.#} | {r.Lost:F2} | {r.Gold:F1} | {r.SpawnFights:F2} |");
        return ok;
    }
}
