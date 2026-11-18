using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Policy = Chapter1Stage12Smoke.Policy;
using Result = Chapter1Stage12Smoke.Result;
using Siege = Chapter1Stage12Smoke.Siege;

// =====================================================================================
// 第478期の門 `--chapter1-nohero-smoke`（指示書 design/PHASE478_STAGE12_NOHERO_SPEC.md §5・画面なし）
//
//   (a) 1 章（V478）の 1-2 で、門が開いても勇者が出ない。本陣は 守り → 砦の守将 の連戦
//   (b) 守将を抜いた刻に制圧で勝ち。踏破の窓に出す予告の 1 行がある
//   (c) 1-3 の追う勇者は今のまま
//   (d) 単発・V461〜V477 は今のまま
//
// §3 の測り（`--chapter1-nohero-quick` は省く）:
//   §3-1 守将の重さ: 候補 × 満タンの 5 枚 ／ 本陣の守りを抜いた直後の隊 ／ 3 枚（20 seed）
//   §3-2 通し: 方針 1〜3 × 留守番 × 組 B ／ ポンの写し × 100 seed と線。候補と期限を振った表
// 器具は第477期の門（`Chapter1Stage12Smoke`）のものを使う。**ここには Godot の型を1つも使わない。**
// =====================================================================================
public static class Chapter1NoheroSmoke
{
    private const int Seeds = 100, WeighSeeds = 20;
    private static readonly Policy[] Three = { Policy.MidNorth, Policy.MidSouth, Policy.NorthSouth };

    private static double Med(IEnumerable<double> xs) => Chapter1AsymSmoke.Med(xs);
    private static MapStage Dress(MapStage raw, ChapterRule rule) => Chapter1Stage12Smoke.Dress(raw, rule, 1);

    /// <summary>守将の候補（今ある部隊 × その区画だけの倍率）。</summary>
    private static readonly (string Label, string Band, int Hp, int Atk)[] Keepers =
    {
        ("本陣の守り（重）", "band_garrison_heavy", 100, 100),
        ("本陣の守り（重）最大HP ×0.8", "band_garrison_heavy", 80, 100),
        ("本陣の守り（重）×0.8 ／ ×0.8", "band_garrison_heavy", 80, 80),
        ("本陣の守り（聖騎士長が芯）最大HP ×1.3", "band_garrison", 130, 100),
        ("本陣の守り（聖騎士長が芯）×1.5 ／ ×1.2", "band_garrison", 150, 120),
        ("盾の列 ×2 ／ ×1.5", "band_shields", 200, 150),
        ("盾の列 ×2.5 ／ ×2", "band_shields", 250, 200),
        ("騎士の小隊 ×1.5 ／ ×1.2", "band_knights", 150, 120),
    };

    public static bool Run(Action<string> write, string demoDir, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V478;
        var stages = Chapter1.StagesFor(rule);
        MapStage s = stages[1];
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}");

        // ---- (a) 勇者が出ない・本陣は 守り → 守将 ----
        Result seen = new();
        Map11State? end = null;
        {
            bool heroSeen = false;
            var order = new List<string>();
            foreach (int seed in Enumerable.Range(0, 10))
            {
                var r = Chapter1Stage12Smoke.Play(s, seed, Policy.MidSouth, 2, teamB: true, watch: st => { heroSeen |= st.Hero is not null; end = st; }, siege: Siege.Continue);
                if (order.Count == 0) order = r.Fights.Where(f => f.Where == "本陣").Select(f => f.Foe).ToList();
                if (r.Won && !seen.Won) seen = r;
            }
            var mid = s.Lanes[1];
            bool shape = !s.Hero.On && mid.Count == 4 && mid[2].Stationary && mid[2].Name == "本陣の守り" && mid[3].Stationary && mid[3].Name == MapStage.KeeperName
                         && mid[3].Gold == MapStage.Stage12Ch478KeeperGold && s.Siege.On && s.PortalGate == 2 && s.WinOnCapture;
            int g = order.IndexOf("本陣の守り"), k = order.IndexOf(MapStage.KeeperName);
            write($"  (a) 勇者の規則 {(s.Hero.On ? "あり" : "なし")}・通し 10 本で勇者が立った {heroSeen}・中の経路の区画 {string.Join(" → ", mid.Select(n => n.Name + (n.Stationary ? "（動かない）" : "")))}"
                  + $"・守将の賞金 ＋{mid[3].Gold}・本陣での戦闘の順（seed 0）{string.Join(" → ", order.DefaultIfEmpty("—"))}");
            Req(shape && !heroSeen && g >= 0 && k > g, "(a) 門が開いても勇者が出ない。本陣は 本陣の守り → 砦の守将 の連戦");
        }

        // ---- (b) 守将を抜いた刻に制圧 ----
        {
            bool sameTurn = false;
            int turn = -1;
            foreach (int seed in Enumerable.Range(0, 20))
            {
                Map11State? last = null;
                int keeperTurn = -1;
                var r = Chapter1Stage12Smoke.Play(s, seed, Policy.MidSouth, 2, teamB: true, watch: st => last = st, siege: Siege.Continue,
                    log: line => { if (line.Contains($"対 {MapStage.KeeperName}") && line.Contains("勝ち")) keeperTurn = int.Parse(line.TrimStart().Split(' ')[0]); });
                if (!r.Won || keeperTurn < 0) continue;
                sameTurn = r.CaptureTurn == keeperTurn;
                turn = keeperTurn;
                break;
            }
            write($"  (b) 守将を抜いた刻 {turn}・その刻に制圧で勝ち {sameTurn}・踏破の窓の 1 行「{s.WinLine}」");
            Req(sameTurn && s.WinLine.Length > 0 && MapStage.Stage12Ch477.WinLine.Length == 0 && MapStage.Stage12.WinLine.Length == 0, "(b) 守将を抜いた刻に制圧で勝ち。踏破の窓に予告の 1 行（この版だけ）");
        }

        // ---- (c) 1-3 は今のまま ----
        {
            var prev = Chapter1.StagesFor(ChapterRule.V477);
            bool same = ReferenceEquals(stages[2], prev[2]) && stages[2].Hero.Chaser && ReferenceEquals(stages[0], prev[0]);
            write($"  (c) 1-3 は V477 と同じもの {ReferenceEquals(stages[2], prev[2])}・追ってくる勇者 {stages[2].Hero.Chaser}（{stages[2].Hero.Unit?.Name}）・1-1 も同じもの {ReferenceEquals(stages[0], prev[0])}");
            Req(same, "(c) 1-3 の追う勇者（と 1-1）は今のまま");
        }

        // ---- (d) 単発・ほかの版は今のまま ----
        {
            bool single = MapStage.Stage12.Hero.Gatekeeper && MapStage.Stage12.Lanes[1].Count == 3 && MapStage.Stage13.Hero.Chaser;
            MapStage s477 = Chapter1.StagesFor(ChapterRule.V477)[1];
            bool v477 = s477.Hero.Gatekeeper && s477.Lanes[1].Count == 3 && s477.TurnCap == MapStage.Stage12Ch477TurnCap && s477.Notes[0].Contains("門番");
            var old = ChapterRule.Versions.Where(v => v.Key != "V477" && v.Key != "V478" && v.Key != "V479" && v.Key != "V480").ToList();
            bool olds = old.All(v => v.Rule.Second is null && Chapter1.StagesFor(v.Rule)[1].Hero.Gatekeeper);
            bool rest = ReferenceEquals(rule.Roster, ChapterRule.V477.Roster) && rule.Scales == ChapterRule.V477.Scales
                        && s.OutpostRewards!.SequenceEqual(s477.OutpostRewards!) && s.Board == s477.Board && s.Lanes[0][0].Name == s477.Lanes[0][0].Name;
            write($"  (d) 単発の 1-2（門番の勇者）／ 1-3 {(single ? "今のまま" : "×")}・V477 の 1-2 は門番の勇者のまま {v477}・{old.Count} 版（{old[0].Key}〜{old[^1].Key}）は単発の盤 {olds}・V478 の盤 ／ 見返り ／ 加入の表 ／ 倍率は V477 と同じ {rest}");
            Req(single && v477 && olds && rest, "(d) 単発・V461〜V477 は今のまま");
        }

        if (!bots) return ok;
        int crashed = 0;
        bool lines = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { Weigh(write, rule, demoDir); }
        catch (Exception ex) { crashed++; write($"  × 重さ: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        try { lines = Routes(write, rule, demoDir, s); }
        catch (Exception ex) { crashed++; write($"  × 通し: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        try { Sweep(write, rule, demoDir); }
        catch (Exception ex) { crashed++; write($"  × 振った表: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        write($"  （測り {sw.Elapsed.TotalSeconds:F0} 秒）");
        foreach (var (pol, gd) in new[] { (Policy.MidNorth, 2), (Policy.MidSouth, 2), (Policy.NorthSouth, 3) })
        {
            write("");
            write($"  通しの写し（組 B・{Chapter1Stage12Smoke.PolicyName(pol)}・留守番 {gd} 枚・1 章の seed 0・傷んでいれば引く）");
            Chapter1Stage12Smoke.Play(s, 0, pol, gd, teamB: true, log: write, siege: Siege.Hurt);
        }
        Req(crashed == 0, "器具（重さ・通し × 100 seed）が落ちない");
        write($"{(lines ? "○" : "×")} §3 の線{(lines ? "" : "——通っていない（門の合否には入れない・報告を見る）")}");
        return ok;
    }

    // ---------------- §3-1 守将の重さ ----------------

    private static void Weigh(Action<string> write, ChapterRule rule, string demoDir)
    {
        MapStage s = Dress(MapStage.Stage12Ch478, rule);
        Formation garrison = s.Lanes[1][2].Enemy;
        var humans = Chapter1Stage12Smoke.Humans(demoDir, s, new List<string>());
        write("");
        write($"  §3-1 守将の重さ（{rule.ScaleOf(1).Text}・seed 0〜{WeighSeeds - 1}・「守りの後」は同じ隊が本陣の守りを抜いたそのままの姿で続けて戦う）");
        write("  隊: 組 B 主力 ＝ ドルガ・ガン・リィカ ＋ 1-1 で呼んだ 2 枚（呼ばない・強化なし）／ 3 枚 ＝ ムド・ムグ・リリ ／ 写し ＝ `design/plays` の 1-2 の始めのいちばん枚数の多い隊");
        write("  線: 満タンの 5 枚 20／20・終わりの HP 40〜65%・1 枚は倒れる ／ 守りの後 8〜14／20 ／ 3 枚 5／20 以下");
        write("");
        write("  | 守将の候補 | 組 B 主力・満タン（勝ち・後の HP・失った枚数） | 組 B 主力・守りの後（入る前の枚数・HP → 勝ち） | 3 枚 | 写しの主力・満タン | 写しの主力・守りの後 | 倒れた駒（組 B・満タン） | 線 |");
        write("  |---|--:|--:|--:|--:|--:|---|:-:|");

        List<UnitState>? Squad(int c, bool small) => Chapter1Stage12Smoke.Setup(s, c, teamB: true, guard: small ? 3 : 0, recruit: false).Squads[small ? 1 : 0].Units;
        List<UnitState>? HumanSquad(int k)
        {
            var st = new Map11State(Chapter1.StageSeed(k, 1, 0), s.Time, s.Portal, draft: true, stage: s);
            st.ApplyCarry(humans[k % humans.Count].Pack);
            return st.Squads.Where(q => q.Units is { Count: > 0 }).OrderByDescending(q => q.Units!.Count).FirstOrDefault()?.Units;
        }
        (int Wins, int N, double Hp, double Lost, double AliveIn, double HpIn, List<(string, int)> Fell) Cell(Func<int, List<UnitState>?> pick, Formation foe, bool after)
        {
            var rows = new List<(bool Won, double Hp, int Lost, int AliveIn, double HpIn)>();
            var fell = new List<(string, int)>();
            for (int c = 0; c < WeighSeeds; c++)
            {
                List<UnitState>? us = pick(c);
                if (us is not { Count: > 0 }) continue;
                if (after)
                {
                    BattleResult g = BattleEngine.Run(us, BattleEngine.Materialize(garrison, BattleContext.EnemyTeam, Map11.EnemyScale), c * 7 + 1, verbose: false);
                    if (!g.PlayerWon) { rows.Add((false, 0, us.Count, 0, 0)); continue; }
                    us = EngagementEngine.CrossBoundary(us.Where(u => u.IsAlive).ToList());
                }
                int alive0 = us.Count(u => u.IsAlive), max0 = us.Sum(u => u.MaxHp);
                double hpIn = max0 == 0 ? 0 : 100.0 * us.Where(u => u.IsAlive).Sum(u => u.Hp) / max0;
                var standing = us.Where(u => u.IsAlive).ToList();
                BattleResult r = BattleEngine.Run(us, BattleEngine.Materialize(foe, BattleContext.EnemyTeam, Map11.EnemyScale), c, verbose: false);
                int max = us.Sum(u => u.MaxHp);
                foreach (UnitState u in standing.Where(u => !u.IsAlive)) fell.Add((u.Def.Name, u.LastDeathTurn));
                rows.Add((r.PlayerWon, r.PlayerWon && max > 0 ? 100.0 * us.Where(u => u.IsAlive).Sum(u => u.Hp) / max : 0, alive0 - us.Count(u => u.IsAlive), alive0, hpIn));
            }
            if (rows.Count == 0) return (0, 0, 0, 0, 0, 0, fell);
            return (rows.Count(x => x.Won), rows.Count, Med(rows.Select(x => x.Hp)), rows.Average(x => (double)x.Lost), Med(rows.Select(x => (double)x.AliveIn)), Med(rows.Select(x => x.HpIn)), fell);
        }
        foreach (var (label, band, hp, atk) in Keepers)
        {
            Formation foe = Dress(MapStage.Stage12Ch478With(band, hp, atk), rule).Lanes[1][3].Enemy;
            var full = Cell(c => Squad(c, false), foe, false);
            var aft = Cell(c => Squad(c, false), foe, true);
            var small = Cell(c => Squad(c, true), foe, false);
            string hFull = "—", hAft = "—";
            if (humans.Count > 0)
            {
                var a = Cell(HumanSquad, foe, false); var b = Cell(HumanSquad, foe, true);
                hFull = $"{a.Wins}／{a.N}・{a.Hp:F0}%・{a.Lost:F1}";
                hAft = $"{b.AliveIn:0.#} 枚 {b.HpIn:F0}% → {b.Wins}／{b.N}";
            }
            string who = full.Fell.Count == 0 ? "—" : string.Join(" ／ ", full.Fell.GroupBy(x => x.Item1).OrderByDescending(x => x.Count()).Take(4)
                .Select(x => $"{x.Key} {x.Count()}／{full.N}・{x.Average(y => (double)y.Item2):F1}T"));
            bool l1 = full.Wins == full.N && full.Hp >= 40 && full.Hp <= 65 && full.Lost >= 1, l2 = aft.Wins >= 8 && aft.Wins <= 14, l3 = small.Wins <= 5;
            write($"  | {label} | {full.Wins}／{full.N}・{full.Hp:F0}%・{full.Lost:F1} | {aft.AliveIn:0.#} 枚 {aft.HpIn:F0}% → {aft.Wins}／{aft.N} | {small.Wins}／{small.N} | {hFull} | {hAft} | {who} | {(l1 ? "○" : "×")}{(l2 ? "○" : "×")}{(l3 ? "○" : "×")} |");
        }
        write("");
        write($"  使った写し {humans.Count} 本: {string.Join("・", humans.Select(h => h.Label).DefaultIfEmpty("—"))}");
    }

    // ---------------- §3-2 通し ----------------

    private sealed record Row(int Wins, int Fallen, int TimeUp, double Turn, double Lost, double Gold, int ContLost, int RetreatWon, int Retreats, int KeeperWins, int KeeperFights, List<Result> Runs);

    private static Row Sum(List<Result> rs)
    {
        var won = rs.Where(r => r.Won).ToList();
        return new Row(won.Count * 100 / Math.Max(1, rs.Count) * 1, rs.Count(r => r.Fallen) * 100 / Math.Max(1, rs.Count), rs.Count(r => r.TimeUp) * 100 / Math.Max(1, rs.Count),
            won.Count == 0 ? double.NaN : Med(won.Select(r => (double)r.CaptureTurn)), rs.Average(r => (double)r.Lost), won.Count == 0 ? double.NaN : won.Average(r => (double)r.Gold),
            rs.Count(r => r.ContinuedLost > 0) * 100 / Math.Max(1, rs.Count), rs.Count(r => r.Won && r.RetreatWon) * 100 / Math.Max(1, rs.Count), rs.Count(r => r.KeeperRetreats > 0) * 100 / Math.Max(1, rs.Count),
            rs.Sum(r => r.KeeperWins), rs.Sum(r => r.KeeperFights), rs);
    }

    private static Row Bot(MapStage s, Policy p, int guard, Siege siege)
        => Sum(Enumerable.Range(0, Seeds).AsParallel().AsOrdered().Select(c => Chapter1Stage12Smoke.Play(s, c, p, guard, teamB: true, siege: siege)).ToList());

    private static Row Human(MapStage s, List<Chapter1Stage12Smoke.Human> hs, Policy p, int guard, Siege siege)
        => Sum(hs.SelectMany(h => Enumerable.Range(0, Seeds).AsParallel().AsOrdered().Select(c => Chapter1Stage12Smoke.PlayHuman(s, h, c, p, guard, siege: siege))).ToList());

    private static string T(double x) => double.IsNaN(x) ? "—" : x.ToString("0.#");

    private static bool Routes(Action<string> write, ChapterRule rule, string demoDir, MapStage s)
    {
        var dropped = new List<string>();
        var humans = Chapter1Stage12Smoke.Humans(demoDir, s, dropped);
        var pon = humans.Where(h => h.Label.StartsWith("771715", StringComparison.Ordinal)).ToList();
        write("");
        write($"  §3-2 通し（遊ぶ盤 ＝ 守将 {s.Lanes[1][3].Name}・期限 {s.TurnCap}・1 章の seed 0〜{Seeds - 1}・勝ち ／ 陥落 ／ 時間切れ は %）");
        write($"  持ち込み: 組 B（器具・1-1 の終わりを持ち越し）／ 写し {humans.Count} 本（今の表で使えるもの: {string.Join("・", humans.Select(h => h.Label).DefaultIfEmpty("—"))}）／ ポンの写し ＝ 771715（第477期の試遊）{pon.Count} 本");
        write("  守将の前: 続ける ＝ 必ず続けて戦う ／ 引く ＝ 4 枚以下か HP 70% 未満なら引いて中間拠点で休む");
        write("");
        write("  | 持ち込み | 方針 | 留守番 | 守将の前 | 勝ち | 陥落 | 時間切れ | 制圧の刻（中央値） | 失った枚数 | 金の余り | 守将との戦闘（勝ち ／ 回数） | 続けて戦って負けた通し | 引いた通し | 引いた隊が守将を抜いて勝った通し |");
        write("  |---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var table = new Dictionary<(string, Policy, int, Siege), Row>();
        void Line(string who, Policy p, int guard, Siege sg, Row r)
        {
            table[(who, p, guard, sg)] = r;
            write($"  | {who} | {Chapter1Stage12Smoke.PolicyName(p)} | {guard} | {(sg == Siege.Hurt ? "引く" : "続ける")} | {r.Wins} | {r.Fallen} | {r.TimeUp} | {T(r.Turn)} | {r.Lost:F2} | {T(r.Gold)} | {r.KeeperWins} ／ {r.KeeperFights} | {r.ContLost} | {r.Retreats} | {r.RetreatWon} |");
        }
        foreach (Siege sg in new[] { Siege.Hurt, Siege.Continue })
            foreach (Policy p in Three)
                foreach (int gd in new[] { 0, 2, 3, 4 })
                {
                    if (sg == Siege.Continue && gd != 2) continue;
                    Line("組 B", p, gd, sg, Bot(s, p, gd, sg));
                }
        if (humans.Count > 0)
            foreach (Siege sg in new[] { Siege.Hurt, Siege.Continue })
                foreach (Policy p in Three)
                    Line($"写し {humans.Count} 本", p, 2, sg, Human(s, humans, p, 2, sg));
        if (pon.Count > 0)
            foreach (Policy p in Three)
                Line("ポンの写し", p, 2, Siege.Hurt, Human(s, pon, p, 2, Siege.Hurt));

        Row B(Policy p, int g, Siege sg = Siege.Hurt) => table[("組 B", p, g, sg)];
        string M(bool x) => x ? "○" : "×";
        var two = new[] { Policy.MidNorth, Policy.MidSouth };
        bool l1 = two.All(p => B(p, 2).Wins >= 65);
        bool l1h = pon.Count > 0 && two.All(p => table[("ポンの写し", p, 2, Siege.Hurt)].Wins >= 85);
        bool l2 = B(Policy.NorthSouth, 3).Wins >= 50;
        bool l3 = two.All(p => !double.IsNaN(B(p, 2).Turn) && B(p, 2).Turn <= s.TurnCap - 3);
        int contLost = two.Max(p => B(p, 2, Siege.Continue).ContLost), retWon = two.Max(p => B(p, 2).RetreatWon);
        bool l4 = two.All(p => B(p, 2, Siege.Continue).ContLost >= 10) && two.All(p => B(p, 2).RetreatWon >= 10);
        bool l5 = B(Policy.NorthSouth, 0).Fallen >= 20 && B(Policy.NorthSouth, 3).Fallen <= 10;
        bool dom = Three.Any(a => Three.Where(b => b != a).All(b => B(a, 2).Wins >= B(b, 2).Wins && B(a, 2).Turn <= B(b, 2).Turn && B(a, 2).Lost <= B(b, 2).Lost && B(a, 2).Gold >= B(b, 2).Gold));
        write("");
        write($"  線 1 方針 1・2 の勝ちが 65% 以上（組 B・留守番 2 枚・上の端なし） {M(l1)}（{string.Join("・", two.Select(p => B(p, 2).Wins))}）／ ポンの写しでは 85% 以上 {M(l1h)}（{(pon.Count == 0 ? "写しが無い" : string.Join("・", two.Select(p => table[("ポンの写し", p, 2, Siege.Hurt)].Wins)))}）");
        write($"  線 2 方針 3 は留守番 3 枚で 50% 以上 {M(l2)}（{B(Policy.NorthSouth, 3).Wins}）");
        write($"  線 3 制圧の刻の中央値が期限の 3 刻以上手前（{s.TurnCap - 3} 刻目まで） {M(l3)}（{string.Join("・", two.Select(p => T(B(p, 2).Turn)))}）");
        write($"  線 4 「続けて戦って負けた」通し（続ける器具）と「引いてから勝った」通し（引く器具）がどちらも 10% 以上 {M(l4)}（続けて負け {string.Join("・", two.Select(p => B(p, 2, Siege.Continue).ContLost))} ／ 引いて勝ち {string.Join("・", two.Select(p => B(p, 2).RetreatWon))}）");
        write($"  線 5 留守番（方針 3 だけ・ポンの決定 2026-11-16）: なし ＝ 陥落 20% 以上・3 枚 ＝ 陥落 10% 以下 {M(l5)}（なし {B(Policy.NorthSouth, 0).Fallen} ／ 3 枚 {B(Policy.NorthSouth, 3).Fallen}）"
              + $"・参考: 方針 1・2 の陥落 なし {string.Join("・", two.Select(p => B(p, 0).Fallen))} ／ 2 枚 {string.Join("・", two.Select(p => B(p, 2).Fallen))}・勝ち 2 → 3 → 4 枚 {string.Join("・", two.Select(p => $"{B(p, 2).Wins}→{B(p, 3).Wins}→{B(p, 4).Wins}"))}");
        write($"  線 6 どれか 1 つの方針が全部の列で勝っていない {M(!dom)}");
        foreach (Policy p in Three)
        {
            var late = B(p, 2).Runs.Where(r => r.TimeUp).ToList();
            write($"  時間切れの通し（組 B・{Chapter1Stage12Smoke.PolicyName(p)}・留守番 2 枚・引く器具）{late.Count} 本: 中間拠点の戦闘（見張り台 ／ 渡し場）で負けた {late.Count(r => r.Fights.Any(f => !f.Won && !f.Intercept && (f.Where == "見張り台" || f.Where == "渡し場")))} 本"
                  + $"・本陣で負けた {late.Count(r => r.Fights.Any(f => !f.Won && f.Where == "本陣"))} 本・1 度も負けていない {late.Count(r => r.Fights.All(f => f.Won))} 本・終わりの刻の中央値 {(late.Count == 0 ? "—" : T(Med(late.Select(r => (double)r.EndTurn))))}");
        }

        write("");
        write("  誰が何 T に倒れたか（組 B・留守番 2 枚・引く器具・相手ごと・倒れた通しの割合と、その戦闘の中の平均ターン）");
        write("");
        write("  | 方針 | 相手（場所） | 勝ち ／ 戦闘 | 入る前（枚数・HP の中央値） | 後の HP | 倒れた駒 |");
        write("  |---|---|--:|--:|--:|---|");
        void Fell(string who, Policy p, Row row)
        {
            var rs = row.Runs;
            foreach (var grp in rs.SelectMany(r => r.Fights).GroupBy(f => (f.Foe, f.Where)).OrderByDescending(x => x.Count()).Take(8))
            {
                var fell = rs.SelectMany((r, i) => r.Fell.Where(x => x.Foe == grp.Key.Foe && x.Where == grp.Key.Where).Select(x => (i, x.Unit, x.Turn)))
                    .GroupBy(x => x.Unit).Select(x => (Unit: x.Key, Runs: x.Select(y => y.i).Distinct().Count(), T: x.Average(y => (double)y.Turn)))
                    .OrderByDescending(x => x.Runs).Take(5).ToList();
                write($"  | {who}{Chapter1Stage12Smoke.PolicyName(p)} | {grp.Key.Foe}（{grp.Key.Where}） | {grp.Count(f => f.Won)} ／ {grp.Count()} | {Med(grp.Select(f => (double)f.AliveIn)):0.#} 枚・{Med(grp.Select(f => f.HpIn)):F0}% | {Med(grp.Select(f => f.Hp)):F0}%"
                      + $" | {(fell.Count == 0 ? "—" : string.Join(" ／ ", fell.Select(x => $"{x.Unit} {100.0 * x.Runs / rs.Count:F0}%・{x.T:F1}T")))} |");
            }
        }
        foreach (Policy p in Three) Fell("", p, B(p, 2));
        if (pon.Count > 0) foreach (Policy p in Three) Fell("ポンの写し・", p, table[("ポンの写し", p, 2, Siege.Hurt)]);
        return l1 && l1h && l2 && l3 && l4 && l5 && !dom;
    }

    // ---------------- 守将の候補と期限を振った表 ----------------

    private static void Sweep(Action<string> write, ChapterRule rule, string demoDir)
    {
        MapStage basis = Dress(MapStage.Stage12Ch478, rule);
        var humans = Chapter1Stage12Smoke.Humans(demoDir, basis, new List<string>());
        var pon = humans.Where(h => h.Label.StartsWith("771715", StringComparison.Ordinal)).ToList();
        write("");
        write("  振った表（留守番 2 枚・「勝ち ／ 制圧の刻の中央値」を 方針 1 ｜ 2 ｜ 3 の順・引く ＝ 引く器具・続ける ＝ 続ける器具）");
        write("");
        write("  | 守将 | 期限 | 組 B・引く | 組 B・続ける | 続けて負けた通し（1・2） | 引いて勝った通し（1・2） | 方針 3 留守番 3 枚の勝ち | ポンの写し・引く | 留守番なしの陥落（1・2・3） |");
        write("  |---|--:|---|---|--:|--:|--:|---|--:|");
        foreach (var (label, band, hp, atk) in Keepers)
            foreach (int cap in new[] { 14, 16 })
            {
                MapStage v = Dress(MapStage.Stage12Ch478With(band, hp, atk, cap), rule);
                var hurt = Three.Select(p => Bot(v, p, 2, Siege.Hurt)).ToList();
                var cont = Three.Select(p => Bot(v, p, 2, Siege.Continue)).ToList();
                var none = Three.Select(p => Bot(v, p, 0, Siege.Hurt)).ToList();
                Row ns3 = Bot(v, Policy.NorthSouth, 3, Siege.Hurt);
                string C(Row r) => $"{r.Wins} ／ {T(r.Turn)}";
                string ponCell = pon.Count == 0 ? "—" : string.Join(" ｜ ", Three.Select(p => C(Human(v, pon, p, 2, Siege.Hurt))));
                write($"  | {label} | {cap} | {string.Join(" ｜ ", hurt.Select(C))} | {string.Join(" ｜ ", cont.Select(C))} | {cont[0].ContLost}・{cont[1].ContLost} | {hurt[0].RetreatWon}・{hurt[1].RetreatWon} | {ns3.Wins} | {ponCell} | {string.Join("・", none.Select(r => r.Fallen))} |");
            }
    }
}
