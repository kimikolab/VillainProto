using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Policy = Chapter1Stage12Smoke.Policy;
using Result = Chapter1Stage12Smoke.Result;
using Siege = Chapter1Stage12Smoke.Siege;

// =====================================================================================
// 第479期の門 `--chapter1-keep-smoke`（指示書 design/PHASE479_LESS_WINDOWS_SPEC.md §6・§7・画面なし）
//
//   (a) V479 の本陣の中身と重さ（本陣の守り → 砦の守将・その区画だけの倍率）
//   (b) 連戦で引いた隊の行き先が「引いた先のマス」になる（V478 の盤では今までどおり残る）
//   (c) V461〜V478・単発は今のまま
//   (d) 遭遇で引き返せる（第479期の追記・V479 の 3 ステージだけ・`Map11Session.DeclineBattle`）
//
// §6 の測り（`--chapter1-keep-quick` は省く）: 守将 ／ 本陣の守りの候補 × ポンの写しの隊（満タン ／ 守りを抜いた直後）と通し × 100 seed・線。
// 器具は第477期の門（`Chapter1Stage12Smoke`）のものを使う。**ここには Godot の型を1つも使わない。**
// =====================================================================================
public static class Chapter1KeepSmoke
{
    private const int Seeds = 100, WeighSeeds = 20;
    private static readonly Policy[] Two = { Policy.MidNorth, Policy.MidSouth };
    /// <summary>ポンの試遊の写し（章の seed・第477期 ／ 第478期）。</summary>
    private static readonly string[] PonPlays = { "771715", "767134" };

    private static double Med(IEnumerable<double> xs) => Chapter1AsymSmoke.Med(xs);
    private static MapStage Dress(MapStage raw, ChapterRule rule) => Chapter1Stage12Smoke.Dress(raw, rule, 1);

    /// <summary>候補（守将の倍率・本陣の守りの部隊と倍率）。先頭が今（第478期）。<b>軽い順</b>に並べる（通る候補のうち先頭を採る）。</summary>
    internal static readonly (string Label, int KHp, int KAtk, string Garrison, int GHp, int GAtk)[] Candidates =
    {
        ("今（守将 ×0.8／×0.8・守り ×1.0）", 80, 80, "band_garrison", 100, 100),
        ("守将 ×0.8／×0.8・守り 最大HP ×1.3", 80, 80, "band_garrison", 130, 100),
        ("守将 ×1.0／×0.8", 100, 80, "band_garrison", 100, 100),
        ("守将 ×1.0／×1.0", 100, 100, "band_garrison", 100, 100),
        ("守将 ×0.8／×0.8・守り ＝ 騎士の小隊", 80, 80, "band_knights", 100, 100),
        ("守将 ×0.8／×0.8・守り ＝ 騎士の小隊 最大HP ×1.3", 80, 80, "band_knights", 130, 100),
        ("守将 ×1.0／×1.0・守り 最大HP ×1.3", 100, 100, "band_garrison", 130, 100),
        ("守将 ×1.2／×1.0", 120, 100, "band_garrison", 100, 100),
        ("守将 ×1.0／×1.0・守り ＝ 騎士の小隊", 100, 100, "band_knights", 100, 100),
        ("守将 ×1.2／×1.0・守り 最大HP ×1.3", 120, 100, "band_garrison", 130, 100),
        // ---- ここから下は指示書 §6 の候補の外（参考・採らない——通るものがあれば報告に書く） ----
        ("参考: 守将 ×1.2／×1.2", 120, 120, "band_garrison", 100, 100),
        ("参考: 守将 ×1.5／×1.0", 150, 100, "band_garrison", 100, 100),
        ("参考: 守将 ×1.5／×1.2", 150, 120, "band_garrison", 100, 100),
        ("参考: 守将 ×1.5／×1.5", 150, 150, "band_garrison", 100, 100),
        ("参考: 守将 ×2.0／×1.0", 200, 100, "band_garrison", 100, 100),
        ("参考: 守将 ×1.0／×1.0・守り 攻撃 ×1.3", 100, 100, "band_garrison", 100, 130),
        ("参考: 守将 ×1.0／×1.0・守り ×1.3／×1.3", 100, 100, "band_garrison", 130, 130),
        ("参考: 守将 ×1.2／×1.2・守り ×1.3／×1.3", 120, 120, "band_garrison", 130, 130),
        ("参考: 守将 ×1.0／×1.0・守り ＝ 本陣の守り（重）×0.8／×0.8", 100, 100, "band_garrison_heavy", 80, 80),
    };
    /// <summary>指示書 §6 の候補の数（これより後ろは参考・採らない）。</summary>
    private const int InSpec = 10;

    private static MapStage Cand(int i)
    {
        var c = Candidates[i];
        return MapStage.Stage12Ch479With(c.KHp, c.KAtk, c.Garrison, c.GHp, c.GAtk);
    }

    public static bool Run(Action<string> write, string demoDir, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V479;
        var stages = Chapter1.StagesFor(rule);
        MapStage s = stages[1];
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}");

        // ---- (a) 本陣の中身と重さ ----
        {
            var mid = s.Lanes[1];
            MapStage raw = MapStage.Stage12Ch479, raw478 = MapStage.Stage12Ch478;
            string Sum(Formation f) => string.Join("・", f.Occupied().Select(x => $"{x.Item2.Name} {x.Item2.MaxHp}/{x.Item2.Attack}"));
            Formation g0 = EnemyCatalog.MapSquadOf(MapStage.Stage12Ch479GarrisonBand).Enemy, k0 = EnemyCatalog.MapSquadOf(MapStage.Stage12Ch478KeeperBand).Enemy;
            bool Mul(Formation got, Formation from, int hp, int atk)
                => got.Occupied().Zip(from.Occupied()).All(z => z.First.Item2.MaxHp == MapFoeScale.Mul(z.Second.Item2.MaxHp, hp) && z.First.Item2.Attack == MapFoeScale.Mul(z.Second.Item2.Attack, atk));
            bool shape = !s.Hero.On && mid.Count == 4 && mid[2].Stationary && mid[2].Name == "本陣の守り" && mid[3].Stationary && mid[3].Name == MapStage.KeeperName
                         && mid[3].Gold == MapStage.Stage12Ch478KeeperGold && s.TurnCap == MapStage.Stage12Ch478TurnCap && s.SiegeRetreatHolds && s.WinLine.Length == 0 && s.Notes.Count == 1;
            bool weight = Mul(raw.Lanes[1][2].Enemy, g0, MapStage.Stage12Ch479GarrisonHp, MapStage.Stage12Ch479GarrisonAtk)
                          && Mul(raw.Lanes[1][3].Enemy, k0, MapStage.Stage12Ch479KeeperHp, MapStage.Stage12Ch479KeeperAtk);
            bool rest = raw.Board == raw478.Board && raw.OutpostRewards!.SequenceEqual(raw478.OutpostRewards!) && ReferenceEquals(raw.Lanes[0][0].Enemy, raw478.Lanes[0][0].Enemy)
                        && ReferenceEquals(raw.Lanes[2][1].Enemy, raw478.Lanes[2][1].Enemy) && ReferenceEquals(raw.Lanes[1][1].Enemy, raw478.Lanes[1][1].Enemy);
            write($"  (a) 中の経路 {string.Join(" → ", mid.Select(n => n.Name + (n.Stationary ? "（動かない）" : "")))}・期限 {s.TurnCap}・引いた隊は待つ {s.SiegeRetreatHolds}・頭の札の行 {s.Notes.Count}・予告の 1 行「{s.WinLine}」");
            write($"      本陣の守り ＝ {EnemyCatalog.MapSquadOf(MapStage.Stage12Ch479GarrisonBand).Name} ×{MapStage.Stage12Ch479GarrisonHp / 100.0:0.##}／×{MapStage.Stage12Ch479GarrisonAtk / 100.0:0.##}（盤の上: {Sum(mid[2].Enemy)}）");
            write($"      砦の守将 ＝ {EnemyCatalog.MapSquadOf(MapStage.Stage12Ch478KeeperBand).Name} ×{MapStage.Stage12Ch479KeeperHp / 100.0:0.##}／×{MapStage.Stage12Ch479KeeperAtk / 100.0:0.##}（盤の上: {Sum(mid[3].Enemy)}）");
            Req(shape && weight && rest, "(a) V479 の本陣は 本陣の守り → 砦の守将（値は定数どおり）・盤 ／ 見返り ／ ほかの敵は V478 と同じ");
        }

        // ---- (b) 引いた隊の行き先 ----
        {
            (bool Back, bool Holds, string Order, string Where) Try(MapStage stage)
            {
                Map11Session.Reset(0, draft: true, stage: stage);
                Map11State st = Map11Session.State!;
                st.UseDefaultSquads();
                MapGraph g = st.Graph;
                Map11Session.SetDestination(0, Dest.Portal(g, 1));
                if (st.Squads.Length > 1 && st.Squads[1].Units is not null) Map11Session.SetDestination(1, Dest.Deep(g, 2));
                bool siege = false;
                for (int turn = 0; turn < 20 && !siege && !st.Finished; turn++)
                {
                    if (!Map11Session.TurnRunning) Map11Session.BeginTurn();
                    for (int step = 0; step < 30 && Map11Session.TurnRunning && !siege; step++)
                    {
                        Map11Session.StepKind kind = Map11Session.StepTurn();
                        if (kind == Map11Session.StepKind.Siege) { siege = true; break; }
                        if (kind == Map11Session.StepKind.Intercept)
                        {
                            if (!(st.NextInterceptor(Map11Session.InterceptNode!) is var j && j >= 0 && Map11Session.ChooseInterceptor(j))) break;
                            kind = Map11Session.StepKind.Battle;
                        }
                        if (kind == Map11Session.StepKind.Battle)
                        {
                            foreach (UnitState u in CampaignSession.CarriedEnemies!) u.Hp = 0;
                            CampaignSession.ClearCarriedBattle();
                            Map11Session.CompleteBattle(true);
                            continue;
                        }
                        if (kind is Map11Session.StepKind.Done or Map11Session.StepKind.Fallen) break;
                    }
                }
                if (!siege) { Map11Session.Reset(0, draft: true, stage: stage); return (false, false, "連戦の合間に着かない", "—"); }
                int sq = Map11Session.SiegeSquad;
                bool back = Map11Session.RetreatSiege();
                int pos = st.Squads[sq].Pos;
                bool holds = Map11Session.Destinations[sq].Id == pos && !Map11Session.Destinations[sq].IsPortal;
                // 作戦ターンの続きを流し切ってから、次の刻の命令を読む。
                for (int step = 0; step < 30 && Map11Session.TurnRunning; step++)
                {
                    Map11Session.StepKind kind = Map11Session.StepTurn();
                    if (kind == Map11Session.StepKind.Battle)
                    {
                        foreach (UnitState u in CampaignSession.CarriedEnemies!) u.Hp = 0;
                        CampaignSession.ClearCarriedBattle();
                        Map11Session.CompleteBattle(true);
                    }
                    else if (kind != Map11Session.StepKind.Siege && kind != Map11Session.StepKind.Intercept) break;
                    else break;
                }
                string order = Map11Session.OrderText(sq), where = g[pos].Name;
                bool stays = st.Squads[sq].Pos == pos;
                Map11Session.Reset(0, draft: true, stage: stage);
                return (back, holds && stays, order, where);
            }
            var now = Try(s);
            var old = Try(Chapter1.StagesFor(ChapterRule.V478)[1]);
            write($"  (b) V479: 引いた {now.Back} → {now.Where}・行き先は引いた先 {now.Holds}・次の刻の命令「{now.Order}」 ／ V478: 引いた {old.Back} → {old.Where}・行き先は引いた先 {old.Holds}・命令「{old.Order}」");
            Req(now.Back && now.Holds && old.Back && !old.Holds, "(b) 連戦で引いた隊の行き先は引いた先のマス（V478 の盤では敵の拠点のまま）");
        }

        // ---- (d) 遭遇で引き返す（第479期の追記）----
        {
            (bool Met, bool Back, string From, string To, bool Holds, bool FoeStays, bool Cleared, int Battles) Try(MapStage stage)
            {
                Map11Session.Reset(0, draft: true, stage: stage);
                Map11State st = Map11Session.State!;
                st.UseDefaultSquads();
                MapGraph g = st.Graph;
                Map11Session.SetDestination(0, Dest.Deep(g, 1));
                bool met = false;
                for (int turn = 0; turn < 6 && !met && !st.Finished; turn++)
                {
                    if (!Map11Session.TurnRunning) Map11Session.BeginTurn();
                    for (int step = 0; step < 30 && Map11Session.TurnRunning && !met; step++)
                    {
                        Map11Session.StepKind kind = Map11Session.StepTurn();
                        if (kind == Map11Session.StepKind.Battle && Map11Session.PendingSquad == 0 && !Map11Session.PendingIntercept) { met = true; break; }
                        if (kind is Map11Session.StepKind.Done or Map11Session.StepKind.Fallen) break;
                        if (kind == Map11Session.StepKind.Battle)
                        {
                            foreach (UnitState u in CampaignSession.CarriedEnemies!) u.Hp = 0;
                            CampaignSession.ClearCarriedBattle();
                            Map11Session.CompleteBattle(true);
                        }
                        else break;
                    }
                }
                if (!met) { Map11Session.Reset(0, draft: true, stage: stage); return (false, false, "—", "—", false, false, false, 0); }
                Map11State.Node foe = Map11Session.PendingNode!;
                int from = st.Squads[0].Pos, foeAt = foe.Pos, battles = st.Battles;
                bool back = Map11Session.DeclineBattle();
                int to = st.Squads[0].Pos;
                bool holds = back && !Map11Session.HasPendingBattle && Map11Session.Destinations[0].Id == to;
                var r = (true, back, g[from].Name, g[to].Name, holds, foe.Pos == foeAt && !foe.Cleared, foe.Cleared, st.Battles - battles);
                Map11Session.Reset(0, draft: true, stage: stage);
                return r;
            }
            var now = Try(s);
            var old = Try(Chapter1.StagesFor(ChapterRule.V478)[1]);
            bool flags = stages.All(x => x.EncounterRetreat && x.SiegeRetreatHolds) && Chapter1.StagesFor(ChapterRule.V478).All(x => !x.EncounterRetreat) && !MapStage.Stage12.EncounterRetreat && !MapStage.Stage11.EncounterRetreat;
            write($"  (d) V479: 遭遇した {now.Met}（{now.From}）→ 引き返した {now.Back} → {now.To}・行き先は戻った先 {now.Holds}・敵はそのまま {now.FoeStays}・戦闘は数えない {now.Battles == 0} ／ V478: 引き返せる {old.Back}"
                  + $"・V479 の 3 ステージとも 引き返せる ／ 引いた隊は待つ・ほかの版と単発は引き返せない {flags}");
            Req(now.Met && now.Back && now.Holds && now.FoeStays && now.Battles == 0 && now.From != now.To && old.Met && !old.Back && flags,
                "(d) 遭遇で引き返すと 1 マス拠点寄りへ戻ってそこで待つ・敵はそのまま（V479 の 3 ステージだけ）");
        }

        // ---- (c) ほかの版・単発は今のまま ----
        {
            var prev = Chapter1.StagesFor(ChapterRule.V478);
            MapStage s478 = prev[1];
            bool v478 = !s478.Hero.On && s478.WinLine.Length > 0 && s478.Notes.Count == 4 && !s478.SiegeRetreatHolds && s478.TurnCap == MapStage.Stage12Ch478TurnCap
                        && ReferenceEquals(MapStage.Stage12Ch478.Lanes[1][2].Enemy, EnemyCatalog.MapSquadOf("band_garrison").Enemy);
            bool single = MapStage.Stage12.Hero.Gatekeeper && MapStage.Stage12.Lanes[1].Count == 3 && MapStage.Stage13.Hero.Chaser && !MapStage.Stage12.SiegeRetreatHolds && !MapStage.Stage13.SiegeRetreatHolds;
            var old = ChapterRule.Versions.Where(v => v.Key is not ("V477" or "V478" or "V479")).ToList();
            bool olds = old.All(v => v.Rule.Second is null && Chapter1.StagesFor(v.Rule)[1].Hero.Gatekeeper);
            bool third = stages[2].Hero.Chaser && stages[2].Board == prev[2].Board && stages[2].LaneCount == prev[2].LaneCount;
            bool first = stages[0].Board == prev[0].Board && stages[0].Lanes.SelectMany(l => l).Select(n => n.Enemy).SequenceEqual(prev[0].Lanes.SelectMany(l => l).Select(n => n.Enemy));
            bool same = ReferenceEquals(rule.Roster, ChapterRule.V478.Roster) && rule.Scales == ChapterRule.V478.Scales;
            write($"  (c) V478 の 1-2 は今のまま {v478}（予告の 1 行・頭の札 4 行・守り ＝ 本陣の守り ×1.0）・単発の 1-2 ／ 1-3 {(single ? "今のまま" : "×")}・{old.Count} 版（{old[0].Key}〜{old[^1].Key}）は単発の盤 {olds}"
                  + $"・V479 の 1-1 ／ 1-3 の盤と敵は V478 と同じ {first} ／ {third}・加入の表 ／ 倍率は同じ {same}");
            Req(v478 && single && olds && third && first && same, "(c) V461〜V478・単発は今のまま");
        }

        if (!bots) return ok;
        int crashed = 0;
        int pick = -1;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { pick = Measure(write, rule, demoDir); }
        catch (Exception ex) { crashed++; write($"  × 測り: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        write($"  （測り {sw.Elapsed.TotalSeconds:F0} 秒）");
        Req(crashed == 0, "器具（重さ・通し × 100 seed）が落ちない");
        write(pick < 0 ? "× §6 の線——通る候補が無い（門の合否には入れない・報告を見る）" : $"○ §6 の線——通る候補のうちいちばん軽いもの: {Candidates[pick].Label}");
        return ok;
    }

    // ---------------- §6 の測り ----------------

    private sealed record Row(int Wins, int TimeUp, double Turn, double Lost, int ContLost, int RetreatWon, int Retreats, List<Result> Runs);

    private static Row Sum(List<Result> rs)
    {
        var won = rs.Where(r => r.Won).ToList();
        int n = Math.Max(1, rs.Count);
        return new Row(won.Count * 100 / n, rs.Count(r => r.TimeUp) * 100 / n, won.Count == 0 ? double.NaN : Med(won.Select(r => (double)r.CaptureTurn)), rs.Average(r => (double)r.Lost),
            rs.Count(r => r.ContinuedLost > 0) * 100 / n, rs.Count(r => r.Won && r.RetreatWon) * 100 / n, rs.Count(r => r.KeeperRetreats > 0) * 100 / n, rs);
    }

    private static Row Bot(MapStage s, Policy p, int guard, Siege siege)
        => Sum(Enumerable.Range(0, Seeds).AsParallel().AsOrdered().Select(c => Chapter1Stage12Smoke.Play(s, c, p, guard, teamB: true, siege: siege)).ToList());

    private static Row Human(MapStage s, List<Chapter1Stage12Smoke.Human> hs, Policy p, int guard, Siege siege)
        => Sum(hs.SelectMany(h => Enumerable.Range(0, Seeds).AsParallel().AsOrdered().Select(c => Chapter1Stage12Smoke.PlayHuman(s, h, c, p, guard, siege: siege))).ToList());

    private static string T(double x) => double.IsNaN(x) ? "—" : x.ToString("0.#");

    private static int Measure(Action<string> write, ChapterRule rule, string demoDir)
    {
        MapStage basis = Dress(Cand(0), rule);
        var dropped = new List<string>();
        var humans = Chapter1Stage12Smoke.Humans(demoDir, basis, dropped);
        var pon = humans.Where(h => PonPlays.Any(k => h.Label.StartsWith(k + "/", StringComparison.Ordinal))).ToList();
        write("");
        write($"  §6 の測り（{rule.ScaleOf(1).Text}・期限 {basis.TurnCap}）");
        write($"  ポンの写し {pon.Count} 本: {string.Join("・", pon.Select(h => h.Label).DefaultIfEmpty("—"))}（1-2 の始めのいちばん枚数の多い隊・seed 0〜{WeighSeeds - 1} ずつ）");
        write("  部隊の中身（素の値）: " + string.Join(" ／ ", new[] { "band_garrison", "band_knights", "band_garrison_heavy" }
            .Select(id => $"{EnemyCatalog.MapSquadOf(id).Name} ＝ {string.Join("・", EnemyCatalog.MapSquadOf(id).Enemy.Occupied().Select(x => $"{x.Item2.Name} {x.Item2.MaxHp}/{x.Item2.Attack}"))}")));
        write("  線: A 写しの隊・満タンで守将 90% 以上（18／20）・終わりの HP 30〜50% ／ B 写しの隊・守りを抜いた直後に続けて守将 40〜60%（8〜12／20）");
        write("      C 通し 方針 1・2 の勝ち（組 B・留守番 2 枚・引く器具）60% 以上 ／ D 「引いてから勝った」通しが方針 1・2 とも 30% 以上");
        write("      E 引く器具の勝ち − 続ける器具の勝ち が 10pt 以上（方針 1・2 とも）／ F 制圧の刻の中央値が 13 刻目まで");
        write("");
        write("  | # | 候補 | A 満タンで守将（勝ち・後の HP・失った枚数） | B 守りの後（入る前の枚数・HP → 守将に勝ち） | 守りに負けた | C 勝ち 引く（1・2） | 勝ち 続ける（1・2） | D 引いて勝った（1・2） | 引いた通し（1・2） | 続けて負けた（1・2） | F 制圧の刻（1・2） | 時間切れ（1・2） | ポンの写しの通し 引く（1・2） | 線 A B C D E F |");
        write("  |--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|:-:|");

        List<UnitState>? HumanSquad(MapStage s, int k)
        {
            var st = new Map11State(Chapter1.StageSeed(k, 1, 0), s.Time, s.Portal, draft: true, stage: s);
            st.ApplyCarry(pon[k % pon.Count].Pack);
            return st.Squads.Where(q => q.Units is { Count: > 0 }).OrderByDescending(q => q.Units!.Count).FirstOrDefault()?.Units;
        }
        int pick = -1;
        var fellAll = new List<string>();
        for (int i = 0; i < Candidates.Length; i++)
        {
            MapStage s = Dress(Cand(i), rule);
            Formation garrison = s.Lanes[1][2].Enemy, keeper = s.Lanes[1][3].Enemy;
            int n = pon.Count * WeighSeeds;
            int fullWins = 0, aftWins = 0, aftN = 0, gLost = 0;
            var fullHp = new List<double>(); var fullLost = new List<double>(); var inAlive = new List<double>(); var inHp = new List<double>();
            var fell = new List<(string, int)>();
            for (int k = 0; k < n; k++)
            {
                // 満タンで守将
                if (HumanSquad(s, k) is { Count: > 0 } us)
                {
                    BattleResult r = BattleEngine.Run(us, BattleEngine.Materialize(keeper, BattleContext.EnemyTeam, Map11.EnemyScale), k, verbose: false);
                    int max = us.Sum(u => u.MaxHp);
                    if (r.PlayerWon) { fullWins++; fullHp.Add(100.0 * us.Where(u => u.IsAlive).Sum(u => u.Hp) / Math.Max(1, max)); }
                    fullLost.Add(us.Count(u => !u.IsAlive));
                    foreach (UnitState u in us.Where(u => !u.IsAlive)) fell.Add((u.Def.Name, u.LastDeathTurn));
                }
                // 守りを抜いた直後に続けて守将
                if (HumanSquad(s, k) is { Count: > 0 } vs)
                {
                    BattleResult g = BattleEngine.Run(vs, BattleEngine.Materialize(garrison, BattleContext.EnemyTeam, Map11.EnemyScale), k * 7 + 1, verbose: false);
                    aftN++;
                    if (!g.PlayerWon) { gLost++; continue; }
                    int max = vs.Sum(u => u.MaxHp);
                    vs = EngagementEngine.CrossBoundary(vs.Where(u => u.IsAlive).ToList());
                    inAlive.Add(vs.Count); inHp.Add(100.0 * vs.Sum(u => u.Hp) / Math.Max(1, max));
                    if (BattleEngine.Run(vs, BattleEngine.Materialize(keeper, BattleContext.EnemyTeam, Map11.EnemyScale), k, verbose: false).PlayerWon) aftWins++;
                }
            }
            double a = n == 0 ? 0 : 100.0 * fullWins / n, b = aftN == 0 ? 0 : 100.0 * aftWins / aftN, hp = fullHp.Count == 0 ? 0 : Med(fullHp);
            var hurt = Two.Select(p => Bot(s, p, 2, Siege.Hurt)).ToList();
            var cont = Two.Select(p => Bot(s, p, 2, Siege.Continue)).ToList();
            var ponRun = pon.Count == 0 ? null : Two.Select(p => Human(s, pon, p, 2, Siege.Hurt)).ToList();
            bool lA = a >= 90 && hp >= 30 && hp <= 50, lB = b >= 40 && b <= 60, lC = hurt.All(r => r.Wins >= 60), lD = hurt.All(r => r.RetreatWon >= 30);
            bool lE = hurt.Zip(cont).All(z => z.First.Wins - z.Second.Wins >= 10), lF = hurt.All(r => !double.IsNaN(r.Turn) && r.Turn <= basis.TurnCap - 3);
            string M(bool x) => x ? "○" : "×";
            string P(List<Row> rs, Func<Row, string> f) => string.Join("・", rs.Select(f));
            write($"  | {i} | {Candidates[i].Label} | {fullWins}／{n}（{a:F0}%）・{hp:F0}%・{(fullLost.Count == 0 ? 0 : fullLost.Average()):F1} | {(inAlive.Count == 0 ? "—" : $"{Med(inAlive):0.#} 枚 {Med(inHp):F0}%")} → {aftWins}／{aftN}（{b:F0}%） | {gLost} | {P(hurt, r => r.Wins.ToString())} | {P(cont, r => r.Wins.ToString())}"
                  + $" | {P(hurt, r => r.RetreatWon.ToString())} | {P(hurt, r => r.Retreats.ToString())} | {P(cont, r => r.ContLost.ToString())} | {P(hurt, r => T(r.Turn))} | {P(hurt, r => r.TimeUp.ToString())} | {(ponRun is null ? "—" : P(ponRun, r => $"{r.Wins}（{T(r.Turn)}）"))} | {M(lA)}{M(lB)}{M(lC)}{M(lD)}{M(lE)}{M(lF)} |");
            fellAll.Add($"  | {i} | {(fell.Count == 0 ? "—" : string.Join(" ／ ", fell.GroupBy(x => x.Item1).OrderByDescending(x => x.Count()).Take(5).Select(x => $"{x.Key} {100.0 * x.Count() / Math.Max(1, n):F0}%・{x.Average(y => (double)y.Item2):F1}T")))} |");
            if (pick < 0 && i < InSpec && lA && lB && lC && lD && lE && lF) pick = i;
        }
        write("");
        write("  誰が何 T に倒れたか（ポンの写しの隊・満タンで守将・倒れた戦闘の割合と平均ターン）");
        write("");
        write("  | # | 倒れた駒 |");
        write("  |--:|---|");
        foreach (string l in fellAll) write(l);
        if (dropped.Count > 0) { write(""); write($"  使わなかった写し {dropped.Count} 本"); }
        return pick;
    }
}
