using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 1 章の門（第460期 §2・`--chapter1-smoke`・指示書 design/PHASE460_CHAPTER1_SPEC.md）
//
// **第461期から第460期の 1 章（`ChapterRule.V460`・持ち越し無し）に固定。** **画面を1つも作らない。** `Chapter1` ／ `Map11Session` ／ `Map11State` ／ `Map11Orders` と器具の方針（`Map12ChangeSmoke.Greedy`）だけを回す。
// **ここには Godot の型を1つも使わない**。
//
//   A (a) 札の回り方 1-1 → 1-2 → 1-3 → 1 章 → 1-1 ／ (b) 1 章で勝つと次のステージが単発と同じ始め方で始まる
//     (c) 1 章の各ステージの通しが、同じ seed の単発の通しと行単位で一致 ／ (d) 負けると「やり直す」（新しい配り・回数 ＋1）と「やめる」
//     (e) 1-3 の後に 1 章の結果（3 行 ＋ 合計・勇者の列）
//   B (f) 先頭でない敵のいるマスへ横の辺で入ると戦闘（1-3 と 1-2）／ (g) 同じマスに 2 部隊以上なら連戦の窓（1-3）
//     (h) 横の辺の直しは遊ぶ 1-2 ／ 1-3 だけ（固定した版は旧のまま——出力の 0 行は報告の回帰で見る）
//
// 加えて（報告だけ・線は置かない）: B の前後の 1-2 ／ 1-3 の方針の表・1 章の通し（方針 2 つ × 100 seed）・勇者が取っている中間拠点を通った回数。
// =====================================================================================

public static class Chapter1Smoke
{
    public const int Seeds = 100;
    /// <summary>器具の 1 章の通しで、1 ステージのやり直しの上限（これを超えたら通せなかったと数える）。</summary>
    public const int RetryCap = 20;

    /// <summary>器具の方針（1 章の通しでステージごとに替える）。</summary>
    private readonly record struct Policy(string Name, bool Guard, bool Split, bool Turtle, Map12ChangeSmoke.HeroPlan Plan);
    private static readonly Policy Keep1 = new("1 隊残す", true, false, false, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Split = new("分担＋戻り番", true, true, false, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Turtle = new("籠城", true, true, true, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Guard = new("守る", true, true, false, Map12ChangeSmoke.HeroPlan.Guard);
    private static readonly Policy Flee = new("逃げる", true, true, false, Map12ChangeSmoke.HeroPlan.Flee);

    private static Map11State Play(int seed, MapStage stage, Policy p, Map11State? state = null, List<string>? trace = null)
    {
        Map11State? last = null;
        Map12ChangeSmoke.Greedy(seed, stage, p.Guard, p.Split, p.Turtle, state: state, siegeRest: true, heroPlan: p.Plan,
            watch: q =>
            {
                last = q;
                trace?.Add($"T{q.Turn} 戦{q.Battles} 金{q.Gold} 隊[{string.Join(",", q.Squads.Select(s => $"{s.Pos}:{s.Units?.Count(u => u.IsAlive) ?? -1}"))}]"
                           + $" 敵{q.Nodes.Sum(r => r.Count(n => !n.Cleared)) + q.Spawns.Count(n => !n.Cleared)} 終{q.Finished}/{q.Won}");
            });
        return last ?? state!;
    }

    public static bool Run(Action<string> write, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        var noRelics = new Dictionary<(string Unit, TraitId Relic), int>();
        var noLog = new List<Map11Session.Line>();

        // ---- (a) 札の回り方 ----
        {
            if (Map11Session.ChapterPicked) Map11Session.CyclePick();
            Map11Session.Chosen = MapStage.Stage11;
            var seen = new List<string> { Map11Session.PickText };
            for (int k = 0; k < 4; k++) { Map11Session.CyclePick(); seen.Add(Map11Session.PickText); }
            write($"  (a) {string.Join(" → ", seen)}");
            Req(seen.SequenceEqual(new[] { "マップ 1-1", "マップ 1-2", "マップ 1-3", "1 章", "マップ 1-1" }) && !Map11Session.ChapterPicked,
                "(a) 札の回り方が 1-1 → 1-2 → 1-3 → 1 章 → 1-1");
        }

        // ---- (b)(c)(e) 1 章を最後まで（器具の方針で・負ければやり直す）----
        {
            const int chapterSeed = 4600;
            Chapter1.Begin(chapterSeed, ChapterRule.V460);   // 第461期: 第460期の 1 章（持ち越し無し）に固定
            var plan = new[] { Split, Split, Flee };
            bool startsOk = true, tracesOk = true;
            var startLines = new List<string>();
            int attempts = 0;
            for (int guard = 0; guard < 3 * RetryCap && Chapter1.Active && !Chapter1.Over; guard++)
            {
                Map11State st = Map11Session.State!;
                MapStage stage = Chapter1.CurrentStage;
                int seed = Chapter1.Records[Chapter1.Index].Seed;
                // (b) 始め方は単発と同じ（全快・手札・金・レリック・隊の枠）。
                var fresh = new Map11State(seed, stage.Time, stage.Portal, draft: true, stage: stage);
                string Snap(Map11State s) => $"手札 {s.Bench.Count}（{string.Join("・", s.Bench.Select(u => u.Def.Id))}）・金 {s.Gold}・レリック {s.RelicHand.Count}・隊の枠 {s.SquadCap}"
                    + $"・傷 {s.Bench.Count(u => u.Hp < u.MaxHp)}";
                bool same = Snap(st) == Snap(fresh) && ReferenceEquals(st.Stage, stage) && st.Seed == seed && st.Turn == 0 && st.Battles == 0;
                bool rule = !stage.Gold.On || (st.Bench.Count == stage.Arrival.Start && st.Gold == stage.Gold.Start && st.RelicHand.Count == 0 && st.SquadCap == 2);
                startsOk &= same && rule;
                if (Chapter1.Attempt == 0) startLines.Add($"{stage.Id}（seed {seed}）: {Snap(st)}");
                // (c) 1 章の通しと、同じ seed の単発の通しが行単位で一致。
                var tChapter = new List<string>();
                var tSingle = new List<string>();
                Play(seed, stage, plan[Chapter1.Index], state: st, trace: tChapter);
                Play(seed, stage, plan[Chapter1.Index], trace: tSingle);
                tracesOk &= tChapter.SequenceEqual(tSingle) && tChapter.Count > 0;
                attempts++;
                Chapter1.Settle(st, noRelics, noLog);
                if (st.Won) Chapter1.Next(); else Chapter1.Retry();
            }
            foreach (string l in startLines) write($"  (b) {l}");
            Req(startsOk, "(b) 1 章のステージは単発と同じ始め方（全快・手札は配り直し・金とレリックは無し・1-2 ／ 1-3 は手札 10 枚・隊の枠 2）");
            write($"  (c) 1 章の通し {attempts} 回（やり直しを含む）を、同じ seed の単発の通しと 1 刻ずつ突き合わせた");
            Req(tracesOk, "(c) 1 章の各ステージの通しが、同じ seed の単発の通しと行単位で一致する");
            var lines = Chapter1.ResultLines();
            foreach (string l in lines) write($"  (e) {l}");
            var r = Chapter1.Records;
            bool heroCol = r[0].Hero == "—" && r[1].Hero.StartsWith("倒した", StringComparison.Ordinal)
                && (r[2].Hero.StartsWith("倒した", StringComparison.Ordinal) || r[2].Hero.StartsWith("逃げ切った（拠点", StringComparison.Ordinal)
                    || r[2].Hero.StartsWith("拠点で迎え撃って倒した", StringComparison.Ordinal));
            Req(Chapter1.Over && lines.Count == 4 && r.All(x => x.Won) && lines[^1].StartsWith("合計 ・ 制圧 3 ／ 3", StringComparison.Ordinal) && heroCol,
                "(e) 1-3 の後に 1 章の結果（3 行 ＋ 合計）・勇者の列は 1-1「—」／ 1-2「倒した」／ 1-3「倒した」「逃げ切った（拠点を落とされた ／ 落とされなかった）」「拠点で迎え撃って倒した」のどれか");
            Chapter1.End();
        }

        // ---- (d) 負けると「やり直す」「やめる」 ----
        {
            Chapter1.Begin(4601, ChapterRule.V460);
            Map11State first = Map11Session.State!;
            int seed0 = Chapter1.Records[0].Seed;
            string bench0 = string.Join("・", first.Bench.Select(u => u.Def.Id));
            // 器具の戦闘を「いつも負ける」にして 1-1 を終わらせる。
            Map12ChangeSmoke.FightHook = (st, squad, node, intercept) =>
            {
                var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
                if (prep is not { } q) return;
                foreach (UnitState u in q.Players) u.Hp = 0;
                st.Resolve(squad, q.Node, false, intercept);
            };
            try { Play(seed0, MapStage.Stage11, Split, state: first); }
            finally { Map12ChangeSmoke.FightHook = null; }
            Chapter1.Settle(first, noRelics, noLog);
            bool lost = first.Finished && !first.Won;
            Chapter1.Retry();
            Map11State second = Map11Session.State!;
            string bench1 = string.Join("・", second.Bench.Select(u => u.Def.Id));
            write($"  (d) 1-1 で負けた（勝ち {first.Won}・陥落 {first.Fallen}・刻 {first.Turn}）→ やり直す: 同じステージ {ReferenceEquals(second.Stage, MapStage.Stage11)}"
                + $"・seed {seed0} → {second.Seed}・配り {(bench0 == bench1 ? "同じ" : "違う")}・やり直し {Chapter1.Records[0].Retries} 回");
            Req(lost && ReferenceEquals(second.Stage, MapStage.Stage11) && second.Seed != seed0 && second.Seed == Chapter1.StageSeed(4601, 0, 1)
                && Chapter1.Records[0].Retries == 1 && second.Turn == 0 && !Chapter1.Over,
                "(d) 負けて「やり直す」と、同じステージが新しい配り（新しい seed）で始まり、やり直しの回数が 1 増える");
            Chapter1.Quit();
            var lines = Chapter1.ResultLines();
            write($"  (d) 「やめる」→ {lines[0]} ／ {lines[^1]}");
            Req(Chapter1.Over && Chapter1.Records[0].Quit && lines[0].Contains("やめた", StringComparison.Ordinal) && lines[0].Contains("やり直し 1 回", StringComparison.Ordinal)
                && lines[1].Contains("— ・ やり直し 0 回", StringComparison.Ordinal),
                "(d) 「やめる」で 1 章の結果へ（1-1 は「やめた」・やり直し 1 回・1-2 ／ 1-3 は遊んでいない）");
            Chapter1.End();
        }

        // ---- (f)(g) 横の辺の穴 ----
        {
            // 1-3: 物見がまだ中の道の手前にいるうちに、北の砦から横の辺で野営地（火の魔術師の部隊）へ。
            (Map11State st, Map11State.Node? met, bool held) Cross13(MapStage stage, bool stack)
            {
                var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
                st.UseDefaultSquads();
                var g = st.Graph;
                foreach (var n in st.Nodes[0]) if (!n.Def.Stationary) n.Cleared = true;
                for (int k = 0; k < 10 && !st.AtDest(0, Dest.Of(g, g.Lanes[0][3])); k++)
                    Map11Orders.Apply(st, 0, Dest.Of(g, g.Lanes[0][3]), Map11Orders.Plan(st, 0, Dest.Of(g, g.Lanes[0][3])));
                int camp = g.Lanes[1][3];
                if (stack) st.Nodes[1].First(n => n.Def.Hold).Pos = camp;   // 大群の 1 部隊も野営地に重ねる
                Dest d = Dest.Of(g, camp);
                Map11State.Node? m = Map11Orders.Apply(st, 0, d, Map11Orders.Plan(st, 0, d));
                return (st, m, st.HeldOutposts.Contains(camp));
            }
            var (n13, m13, _) = Cross13(MapStage.Stage13, false);
            var (o13, mo13, _) = Cross13(MapStage.Stage13H459, false);
            var g13 = n13.Graph;
            string front = n13.FrontFoe(1) is { } f ? $"{f.Def.Name}@{g13[f.Pos].Name}" : "なし";
            write($"  (f) 1-3: 北の砦 → 横の辺 → 野営地（中の道の先頭は {front}）: 遊ぶ版 → {(m13 is null ? "素通り" : $"{m13.Def.Name} と戦闘")}"
                + $" ／ 第459期の版 → {(mo13 is null ? "素通り" : mo13.Def.Name)}（計数: 当たった {n13.CrossContacts}・素通り {o13.CrossSkips}）");
            bool after13 = m13 is not null && m13.Def.Name == EnemyCatalog.MapSquadOf("band_fire").Name;
            if (m13 is not null)
            {
                var prep = n13.Prepare(0, m13);
                foreach (UnitState u in prep!.Value.Enemies) u.Hp = 0;
                n13.Resolve(0, m13, true);
            }
            write($"  (f) 1-3: 勝った後 野営地は味方 {n13.HeldOutposts.Contains(g13.Lanes[1][3])}");
            Req(after13 && mo13 is null && n13.CrossContacts == 1 && o13.CrossSkips == 1 && n13.HeldOutposts.Contains(g13.Lanes[1][3]),
                "(f) 1-3: 先頭でない敵（火の魔術師の部隊）のいるマスへ横の辺で入ると戦闘になり、勝てば野営地を取れる（第459期の版は素通り）");

            // 1-2: 北の 1 戦目の地点 → 横の辺 → 中の 1 戦目の地点。中の道の 2 部隊目をそこに置き、先頭は手前（0）に置く。
            (Map11State st, Map11State.Node? met) Cross12(MapStage stage)
            {
                var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
                st.UseDefaultSquads();
                var g = st.Graph;
                foreach (var n in st.Nodes[0]) if (!n.Def.Stationary) n.Cleared = true;
                var mid = st.FoesOn(1).Where(n => !n.Def.Stationary).ToList();
                mid[0].Pos = g.Lanes[1][0];
                mid[1].Pos = g.Lanes[1][1];
                Dest a = Dest.Of(g, g.Lanes[0][1]);
                for (int k = 0; k < 6 && !st.AtDest(0, a); k++) Map11Orders.Apply(st, 0, a, Map11Orders.Plan(st, 0, a));
                Dest b = Dest.Of(g, g.Lanes[1][1]);
                return (st, Map11Orders.Apply(st, 0, b, Map11Orders.Plan(st, 0, b)));
            }
            var (n12, m12) = Cross12(MapStage.Stage12);
            var (o12, mo12) = Cross12(MapStage.Stage12H459);
            write($"  (f) 1-2: 北の道 2 マス目 → 横の辺 → 中の道 2 マス目（先頭は中の道 1 マス目）: 遊ぶ版 → {(m12 is null ? "素通り" : $"{m12.Def.Name} と戦闘")}"
                + $" ／ 第459期の版 → {(mo12 is null ? "素通り" : mo12.Def.Name)}（横の辺を通った {n12.CrossMoves}）");
            Req(m12 is not null && mo12 is null && n12.CrossMoves == 1, "(f) 1-2: 同じく、先頭でない敵のいるマスへ横の辺で入ると戦闘になる（第459期の版は素通り）");

            // (g) 1-3: 野営地に 2 部隊（火の魔術師の部隊 ＋ 大群の 1 部隊）→ 1 部隊目に勝つと連戦の窓。
            var (s13, first, _) = Cross13(MapStage.Stage13, true);
            if (first is not null)
            {
                var prep = s13.Prepare(0, first);
                foreach (UnitState u in prep!.Value.Enemies) u.Hp = 0;
                s13.Resolve(0, first, true);
            }
            Map11State.Node? nextFoe = s13.SiegeNext(0);
            write($"  (g) 1-3: 野営地に 2 部隊 → 1 部隊目 {first?.Def.Name ?? "なし"} に勝つ → 連戦の窓の次の相手 {nextFoe?.Def.Name ?? "なし"}");
            Req(first is not null && nextFoe is not null && !ReferenceEquals(first, nextFoe), "(g) 1-3: 同じマスに 2 部隊以上いれば、1 部隊目に勝つと連戦の窓が出る（次の相手は同じマスの 2 部隊目）");
        }

        // ---- (h) 直しは遊ぶ 1-2 ／ 1-3 だけ ----
        {
            var pinned = new[] { MapStage.Stage11, MapStage.Stage12H456, MapStage.Stage12H459, MapStage.Stage13H458, MapStage.Stage13H459 };
            write($"  (h) 横の辺の直し: 遊ぶ 1-2 {MapStage.Stage12.CrossFacing}・遊ぶ 1-3 {MapStage.Stage13.CrossFacing} ／ 固定した版 {string.Join("・", pinned.Select(s => $"{s.Id}{(ReferenceEquals(s, MapStage.Stage11) ? "" : "旧")} {s.CrossFacing}"))}");
            Req(MapStage.Stage12.CrossFacing && MapStage.Stage13.CrossFacing && pinned.All(s => !s.CrossFacing),
                "(h) 横の辺の直しは遊ぶ 1-2 ／ 1-3 だけ（1-1 と固定した版は旧のまま・出力が前と 0 行かは報告の回帰で見る）");
        }

        if (!bots) return ok;
        int crashed = 0;
        void Crash(string what, Exception ex) { crashed++; if (crashed <= 3) write($"  × {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

        // ---- 報告のみ: B の前後の方針の表 ----
        write("");
        write("  | 盤 | 方針 | 勝ち | 時間切れ | 陥落 | 全滅 | 拠点が落ちた | 勇者と戦った（平均） | 勇者を倒した | 制圧の作戦ターン（中央値） | 戦闘（平均） | 横の辺を通った（平均） | 横の辺で先頭でない敵に当たった（平均） | 敵がいるのに素通り（平均） |");
        write("  |---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var all5 = new[] { Keep1, Split, Turtle, Guard, Flee };
        foreach (var (bname, board) in new[] { ("1-2 第459期", MapStage.Stage12H459), ("1-2 遊ぶ版", MapStage.Stage12), ("1-3 第459期", MapStage.Stage13H459), ("1-3 遊ぶ版", MapStage.Stage13) })
            foreach (Policy p in all5)
            {
                if (!board.Hero.Chaser && p.Plan != Map12ChangeSmoke.HeroPlan.None) continue;
                int n = 0, won = 0, timeUp = 0, fallen = 0, wiped = 0, homeLost = 0, heroFights = 0, heroDown = 0, battles = 0, cross = 0, contacts = 0, skips = 0;
                var capTurns = new List<int>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    try
                    {
                        Map11State q = Play(seed, board, p);
                        n++;
                        if (q.Won) won++; else if (q.Fallen) fallen++; else if (q.TimeUp) timeUp++; else if (q.NoSquadsLeft) wiped++;
                        if (q.HomeLost) homeLost++;
                        heroFights += q.HeroBattles;
                        if (q.Hero is { Cleared: true }) heroDown++;
                        if (q.Captured) capTurns.Add(q.CapturedTurn + 1);
                        battles += q.Battles;
                        cross += q.CrossMoves;
                        contacts += q.CrossContacts;
                        skips += q.CrossSkips;
                    }
                    catch (Exception ex) { Crash($"{bname} {p.Name} seed {seed}", ex); }
                }
                if (n == 0) continue;
                double R(int k) => 100.0 * k / n;
                capTurns.Sort();
                write($"  | {bname} | {p.Name} | {R(won):F0} | {R(timeUp):F0} | {R(fallen):F0} | {R(wiped):F0} | {R(homeLost):F0} | {(double)heroFights / n:F2} | {R(heroDown):F0} | "
                    + $"{(capTurns.Count == 0 ? "—" : capTurns[capTurns.Count / 2].ToString())} | {(double)battles / n:F1} | {(double)cross / n:F2} | {(double)contacts / n:F2} | {(double)skips / n:F2} |");
            }

        // ---- 報告のみ: B で結果が変わった通し（どこで分かれたか・最初の 2 通しまで）----
        write("");
        foreach (var (bname, oldB, newB, p) in new[] { ("1-2", MapStage.Stage12H459, MapStage.Stage12, Split), ("1-3", MapStage.Stage13H459, MapStage.Stage13, Keep1) })
        {
            List<string> Fights(int seed, MapStage board)
            {
                var log = new List<string>();
                Map12ChangeSmoke.FightHook = (st, squad, node, intercept) =>
                {
                    Map11State.Squad s = st.Squads[squad];
                    Map11State.Node? front = node.Def.Road >= 0 ? st.FrontFoe(node.Def.Road) : null;
                    log.Add($"T{st.Turn + 1} {s.Label}@{st.Graph[s.Pos].Name} → {node.Def.Name}@{(node.Pos == st.Graph.Home ? "拠点" : node.Pos == st.Graph.Portal ? "本陣" : st.Graph[node.Pos].Name)}"
                            + $"{(intercept ? "【迎撃】" : "")}{(front is not null && !ReferenceEquals(front, node) ? $"（先頭は {front.Def.Name}@{(front.Pos == st.Graph.Portal ? "本陣" : st.Graph[front.Pos].Name)}）" : "")}");
                    var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
                    if (prep is not { } q) return;
                    BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
                    st.Resolve(squad, q.Node, r.PlayerWon, intercept);
                };
                try { Play(seed, board, p); }
                finally { Map12ChangeSmoke.FightHook = null; }
                return log;
            }
            int shown = 0, differ = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                try
                {
                    var a = Fights(seed, oldB);
                    var b = Fights(seed, newB);
                    if (a.SequenceEqual(b)) continue;
                    differ++;
                    if (shown >= 2) continue;
                    shown++;
                    int k = 0;
                    while (k < a.Count && k < b.Count && a[k] == b[k]) k++;
                    write($"  {bname} {p.Name} seed {seed}: {k + 1} 戦目で分かれた ／ 第459期の版 {(k < a.Count ? a[k] : "（戦闘なし）")} ／ 遊ぶ版 {(k < b.Count ? b[k] : "（戦闘なし）")}");
                }
                catch (Exception ex) { Crash($"分かれた通し {bname} seed {seed}", ex); }
            }
            write($"  {bname} {p.Name}: 戦闘の並びが変わった通し {differ} ／ {Seeds}");
        }

        // ---- 報告のみ: 1 章の通し（方針 2 つ × 100 seed）----
        write("");
        write($"  | 1 章の方針 | 通せた | 1-1 のやり直し（平均） | 1-2 のやり直し（平均） | 1-3 のやり直し（平均） | 止まったステージ（やり直し {RetryCap} 回を超えた） |");
        write("  |---|--:|--:|--:|--:|---|");
        foreach (var (cname, plan) in new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) })
        {
            int through = 0;
            var retries = new int[3];
            var stuck = new int[3];
            for (int c = 0; c < Seeds; c++)
            {
                try
                {
                    bool all = true;
                    for (int i = 0; i < 3 && all; i++)
                    {
                        MapStage stage = Chapter1.StagesFor(ChapterRule.V460)[i];
                        bool won = false;
                        for (int a = 0; a <= RetryCap && !won; a++)
                        {
                            won = Play(Chapter1.StageSeed(c, i, a), stage, plan[i]).Won;
                            if (!won && a < RetryCap) retries[i]++;
                        }
                        if (!won) { stuck[i]++; all = false; }
                    }
                    if (all) through++;
                }
                catch (Exception ex) { Crash($"1 章 {cname} seed {c}", ex); }
            }
            write($"  | {cname} | {through}% | {(double)retries[0] / Seeds:F2} | {(double)retries[1] / Seeds:F2} | {(double)retries[2] / Seeds:F2} | 1-1 {stuck[0]}・1-2 {stuck[1]}・1-3 {stuck[2]} |");
        }

        // ---- 報告のみ: 勇者が取っている中間拠点を通ったら（ポンの問い 7・規則は変えない）----
        write("");
        write("  | 1-3 遊ぶ版の方針 | 勇者が取っている中間拠点を通った（平均） | 通った通し | 取り返していたら門が閉じた通し |");
        write("  |---|--:|--:|--:|");
        foreach (Policy p in all5)
        {
            int n = 0, over = 0, overRuns = 0, close = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                try
                {
                    Map11State q = Play(seed, MapStage.Stage13, p);
                    n++;
                    over += q.HeroOverHeld;
                    if (q.HeroOverHeld > 0) overRuns++;
                    if (q.HeroWouldCloseGate) close++;
                }
                catch (Exception ex) { Crash($"問い7 {p.Name} seed {seed}", ex); }
            }
            if (n == 0) continue;
            write($"  | {p.Name} | {(double)over / n:F2} | {100.0 * overRuns / n:F0}% | {100.0 * close / n:F0}% |");
        }
        Req(crashed == 0, $"器具（B の前後の表・1 章の通し・勇者と中間拠点）を {Seeds} seed ずつ回して落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }
}
