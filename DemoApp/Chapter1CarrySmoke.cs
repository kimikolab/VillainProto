using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 1 章の持ち越しの門（第461期 §3・`--chapter1-carry-smoke`・指示書 design/PHASE461_CARRYOVER_SPEC.md）
//
// **画面を1つも作らない。** `Chapter1`（`ChapterRule.V461`）／ `Map11Session` ／ `Map11State` と器具の方針（`Map12ChangeSmoke.Greedy`）だけを回す。
//
//   (a) 1 章の 1-1: 手札 6・隊の枠 2・金 8・レリック 0・隊は空（単発の 1-1 は手札 15・隊 3・金なしのまま）
//   (b) 1 章の 1-1 で金が入る（抜く ＋6 など・1-2 と同じ値）
//   (c) 1-1 → 1-2: 手札・組んだ隊（席も）・隊の枠・金（余り ＋8）・レリックが持ち越される・全快・死者も戻る・隊はすべて拠点
//   (d) 1-2 → 1-3: (c) と同じ・レリックは持たせた駒に付いたまま
//   (e) 1-2 で負けて「やり直す」: 1-2 に入った時点の編成・金・レリック・枠に戻る・戦闘の seed は新しい
//   (f) 持ち越した駒と同じキャラは、呼ぶ候補に出ない
//   (g) 1 章の結果に、各ステージの終わりの手札・隊の枠・金・レリックが出る
//
// 加えて（報告だけ・線は置かない）: 1 章の通し × 100 seed × 方針 2 つ（持ち越しあり ／ 第460期の持ち越し無しを並べる）・
// ステージの始めの手札・隊の枠・金・レリック（平均と最大）・1-3 の終わりの手札と駒。
// =====================================================================================

public static class Chapter1CarrySmoke
{
    public const int Seeds = 100;
    public const int RetryCap = 20;

    private readonly record struct Policy(string Name, bool Guard, bool Split, Map12ChangeSmoke.HeroPlan Plan);
    private static readonly Policy Keep1 = new("1 隊残す", true, false, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Split = new("分担＋戻り番", true, true, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Flee = new("逃げる", true, true, Map12ChangeSmoke.HeroPlan.Flee);

    private static Map11State Play(Map11State st, Policy p)
    {
        Map12ChangeSmoke.Greedy(st.Seed, st.Stage, p.Guard, p.Split, false, state: st, siegeRest: true, heroPlan: p.Plan);
        return st;
    }

    /// <summary>盤の中身の写し（比べるため）: 手札・隊ごとの駒と席とレリック・隊の枠・金・手持ちのレリック。</summary>
    private static string Snap(Map11State st)
        => $"手札[{string.Join(",", st.Bench.Select(u => u.Def.Id + (u.Relic is { } r ? "◆" + r : "")).OrderBy(x => x, StringComparer.Ordinal))}]"
           + $" 隊[{string.Join(" | ", st.Squads.Select(q => string.Join(",", (q.Units ?? new List<UnitState>()).OrderBy(u => u.Slot).Select(u => $"{u.Slot}:{u.Def.Id}{(u.Relic is { } r ? "◆" + r : "")}"))))}]"
           + $" 枠{st.SquadCap} 金{st.Gold} 手持ち[{string.Join(",", st.RelicHand)}]";

    private static string SnapPack(Map11State.CarryPack p, int goldAdd)
        => $"手札[{string.Join(",", p.Bench.Select(u => u.Def.Id + (u.Relic is { } r ? "◆" + r : "")).OrderBy(x => x, StringComparer.Ordinal))}]"
           + $" 隊[{string.Join(" | ", p.Squads.Select(s => string.Join(",", s.OrderBy(u => u.Slot).Select(u => $"{u.Slot}:{u.Def.Id}{(u.Relic is { } r ? "◆" + r : "")}"))))}]"
           + $" 枠{p.SquadCap} 金{p.Gold + goldAdd} 手持ち[{string.Join(",", p.RelicHand)}]";

    public static bool Run(Action<string> write, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        var noRelics = new Dictionary<(string Unit, TraitId Relic), int>();
        var noLog = new List<Map11Session.Line>();
        var lose = new Action<Map11State, int, Map11State.Node, bool>((st, squad, node, intercept) =>
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            if (prep is not { } q) return;
            foreach (UnitState u in q.Players) u.Hp = 0;
            st.Resolve(squad, q.Node, false, intercept);
        });

        const int chapterSeed = 4610;
        Chapter1.Begin(chapterSeed, ChapterRule.V461);
        var plan = new[] { Split, Split, Flee };

        // ---- (a) 1 章の 1-1 ----
        {
            Map11State st = Map11Session.State!;
            var single = new Map11State(0, MapStage.Stage11.Time, MapStage.Stage11.Portal, draft: true, stage: MapStage.Stage11);
            write($"  (a) 1 章の 1-1（{st.Stage.Title}・seed {st.Seed}）: 手札 {st.Bench.Count}（{string.Join("・", st.Bench.Select(u => u.Def.Name))}）・隊の枠 {st.SquadCap} ／ {st.Squads.Length}・金 {st.Gold}・レリック {st.RelicHand.Count}・隊の駒 {st.Squads.Sum(q => q.Units?.Count ?? 0)}"
                + $" ／ 単発の 1-1: 手札 {single.Bench.Count}・隊の枠 {single.SquadCap}・金 {single.Gold}");
            Req(ReferenceEquals(st.Stage, MapStage.Stage11Ch) && st.Bench.Count == 6 && st.SquadCap == 2 && st.Gold == 8 && st.RelicHand.Count == 0
                && st.Squads.All(q => q.Units is { Count: 0 }) && single.Bench.Count == 15 && single.SquadCap == 3 && single.Gold == 0 && !single.Stage.Gold.On,
                "(a) 1 章の 1-1 は手札 6・隊の枠 2・金 8・レリック 0・隊は空。単発の 1-1 は手札 15・隊 3・金なしのまま");
        }

        // ---- (b)(c) 1-1 を遊んで 1-2 へ ----
        Map11State.CarryPack? endOf11 = null;
        int attempts11 = 0;
        {
            for (int guard = 0; guard < RetryCap && Chapter1.Index == 0; guard++)
            {
                Map11State st = Map11Session.State!;
                Play(st, plan[0]);
                attempts11++;
                Chapter1.Settle(st, noRelics, noLog);
                if (attempts11 == 1 || st.Won)
                {
                    var earned = st.GoldLog.Where(e => e.Amount > 0).ToList();
                    write($"  (b) 1-1（{(st.Won ? "勝ち" : "負け")}・seed {st.Seed}）の金: 稼いだ {earned.Sum(e => e.Amount)}（{string.Join("・", earned.Take(6).Select(e => $"＋{e.Amount} {e.Reason}"))}{(earned.Count > 6 ? "…" : "")}）・使った {-st.GoldLog.Where(e => e.Amount < 0).Sum(e => e.Amount)}・余り {st.Gold}");
                }
                if (st.Won) { endOf11 = Chapter1.Records[0].EndPack; Chapter1.Next(); }
                else Chapter1.Retry();
            }
            Req(Chapter1.Records[0].Earned > 0 && Chapter1.Records[0].Stage.Gold.Kill == MapStage.Stage12.Gold.Kill, $"(b) 1 章の 1-1 で金が入る（抜く ＋{MapStage.Stage12.Gold.Kill}・1-2 と同じ値）");
        }

        bool CarriedOk(Map11State st, Map11State.CarryPack p, string label)
        {
            string want = SnapPack(p, st.Stage.Gold.Start), got = Snap(st);
            bool full = st.Squads.SelectMany(q => q.Units ?? new()).Concat(st.Bench).All(u => u.Hp == u.MaxHp && u.IsAlive);
            bool home = st.Squads.All(q => q.Pos == st.Graph.Home && !q.Lost);
            int units = st.Bench.Count + st.Squads.Sum(q => q.Units?.Count ?? 0);
            write($"  {label}: 持ち越し {p.Units} 枚（手札 {p.Bench.Count}・隊 {string.Join("/", p.Squads.Select(s => s.Count))}）・枠 {p.SquadCap}・金 {p.Gold} ＋{st.Stage.Gold.Start} → {st.Gold}・レリック {p.Relics}"
                + $" ／ 入った盤 {units} 枚・全快 {full}・すべて拠点 {home}・写しと一致 {want == got}");
            if (want != got) write($"       want {want}\n       got  {got}");
            return want == got && full && home && units == p.Units && st.CarriedIn;
        }

        // ---- (c) 1-1 → 1-2 の持ち越し ／ (f) 呼ぶ候補 ／ (e) 1-2 で負けてやり直す ----
        Map11State.CarryPack? endOf12 = null;
        {
            Map11State st = Map11Session.State!;
            Req(endOf11 is not null && Chapter1.Index == 1 && CarriedOk(st, endOf11, "(c) 1-1 → 1-2"),
                "(c) 1-1 → 1-2: 手札・組んだ隊（席も）・隊の枠・金（余り ＋8）・レリックが持ち越され、全快・死者も戻る・隊はすべて拠点");
            var owned = st.Bench.Concat(st.Squads.SelectMany(q => q.Units ?? new())).Select(u => u.Def.Id).ToHashSet();
            bool offersOk = st.Offers.All(d => !owned.Contains(d.Id));
            write($"  (f) 1-2 の呼ぶ候補: {string.Join("・", st.Offers.Select(d => d.Name))}（持ち越した駒 {owned.Count} 枚と重なる候補 {st.Offers.Count(d => owned.Contains(d.Id))}）");

            // (e) 1 度目の 1-2 は器具の戦闘を「いつも負ける」にして落とし、やり直す。
            Map11State.CarryPack entry = Chapter1.Records[1].Entry!;
            int seedFirst = st.Seed;
            Map12ChangeSmoke.FightHook = lose;
            try { Play(st, plan[1]); }
            finally { Map12ChangeSmoke.FightHook = null; }
            Chapter1.Settle(st, noRelics, noLog);
            bool lost = st.Finished && !st.Won;
            Chapter1.Retry();
            Map11State again = Map11Session.State!;
            string wantE = SnapPack(entry, again.Stage.Gold.Start), gotE = Snap(again);
            write($"  (e) 1-2 で負けた（勝ち {st.Won}・刻 {st.Turn}・金 {st.Gold}）→ やり直す: seed {seedFirst} → {again.Seed}・入った時点の写しと一致 {wantE == gotE}・やり直し {Chapter1.Records[1].Retries} 回");
            Req(lost && wantE == gotE && again.Seed != seedFirst && again.Seed == Chapter1.StageSeed(chapterSeed, 1, 1) && again.Turn == 0 && Chapter1.Records[1].Retries == 1,
                "(e) 1-2 で負けて「やり直す」と、1-2 に入った時点の編成・金・レリック・枠に戻る（戦闘の seed は新しい）");

            // 1-2 を勝つまで回す。
            for (int guard = 0; guard < RetryCap && Chapter1.Index == 1; guard++)
            {
                Map11State s2 = Map11Session.State!;
                var own2 = s2.Bench.Concat(s2.Squads.SelectMany(q => q.Units ?? new())).Select(u => u.Def.Id).ToHashSet();
                offersOk &= s2.Offers.All(d => !own2.Contains(d.Id));
                Play(s2, plan[1]);
                Chapter1.Settle(s2, noRelics, noLog);
                if (s2.Won) { endOf12 = Chapter1.Records[1].EndPack; Chapter1.Next(); } else Chapter1.Retry();
            }
            Req(offersOk, "(f) 持ち越した駒と同じキャラは、呼ぶ候補に出ない（1-2 の配りごとに確かめた）");
        }

        // ---- (d) 1-2 → 1-3 ----
        {
            Map11State st = Map11Session.State!;
            bool relicOn = endOf12 is not null && st.Squads.SelectMany(q => q.Units ?? new()).Concat(st.Bench)
                .Count(u => u.Relic is not null) == endOf12.Bench.Concat(endOf12.Squads.SelectMany(s => s)).Count(c => c.Relic is not null);
            int equipped = endOf12?.Bench.Concat(endOf12.Squads.SelectMany(s => s)).Count(c => c.Relic is not null) ?? 0;
            Req(endOf12 is not null && Chapter1.Index == 2 && CarriedOk(st, endOf12, "(d) 1-2 → 1-3") && relicOn,
                $"(d) 1-2 → 1-3: (c) と同じ・レリックは持たせた駒に付いたまま（持たせた札 {equipped} 枚）");
            var owned = st.Bench.Concat(st.Squads.SelectMany(q => q.Units ?? new())).Select(u => u.Def.Id).ToHashSet();
            Req(st.Offers.All(d => !owned.Contains(d.Id)), $"(f) 1-3 でも持ち越した駒は候補に出ない（候補 {string.Join("・", st.Offers.Select(d => d.Name))}）");
            for (int guard = 0; guard < RetryCap && Chapter1.Index == 2 && !Chapter1.Over; guard++)
            {
                Map11State s3 = Map11Session.State!;
                Play(s3, plan[2]);
                Chapter1.Settle(s3, noRelics, noLog);
                if (s3.Won) Chapter1.Next(); else Chapter1.Retry();
            }
        }

        // ---- (g) 1 章の結果 ----
        {
            var lines = Chapter1.ResultLines();
            foreach (string l in lines) write($"  (g) {l}");
            Req(Chapter1.Over && lines.Count == 4 && lines.Take(3).All(l => l.Contains(" ・ 終わり 手札 ", StringComparison.Ordinal) && l.Contains("・隊の枠 ", StringComparison.Ordinal)
                    && l.Contains("・金 ", StringComparison.Ordinal) && l.Contains("・レリック ", StringComparison.Ordinal)),
                "(g) 1 章の結果に、各ステージの終わりの手札・隊の枠・金・レリックが出る");
            Chapter1.End();
        }

        if (!bots) return ok;
        int crashed = 0;
        void Crash(string what, Exception ex) { crashed++; if (crashed <= 3) write($"  × {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

        // ---- 報告のみ: 1 章の通し（持ち越しあり ／ 第460期の持ち越し無し）----
        write("");
        write($"  | 1 章 | 方針 | 通せた | 1-1 のやり直し（平均） | 1-2 のやり直し（平均） | 1-3 のやり直し（平均） | 止まったステージ（やり直し {RetryCap} 回を超えた） |");
        write("  |---|---|--:|--:|--:|--:|---|");
        var starts = new Dictionary<string, List<(int Hand, int Units, int Cap, int Gold, int Relics)>>();
        var ends13 = new Dictionary<string, List<(int Hand, int Units)>>();
        foreach (ChapterRule rule in new[] { ChapterRule.V461, ChapterRule.V460 })
            foreach (var (cname, pol) in new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) })
            {
                var stages = Chapter1.StagesFor(rule);
                int through = 0;
                var retries = new int[3];
                var stuck = new int[3];
                string key = $"{(rule.Carry ? "持ち越し" : "第460期")}|{cname}";
                starts[key] = new();
                ends13[key] = new();
                for (int c = 0; c < Seeds; c++)
                {
                    try
                    {
                        Map11State.CarryPack? pack = null;
                        bool all = true;
                        for (int i = 0; i < 3 && all; i++)
                        {
                            bool won = false;
                            for (int a = 0; a <= RetryCap && !won; a++)
                            {
                                MapStage stage = stages[i];
                                var st = new Map11State(Chapter1.StageSeed(c, i, a), stage.Time, stage.Portal, draft: true, stage: stage);
                                if (rule.Carry && pack is not null) st.ApplyCarry(pack);
                                if (a == 0)
                                {
                                    int units = st.Bench.Count + st.Squads.Sum(q => q.Units?.Count ?? 0);
                                    int relics = st.RelicHand.Count + st.Bench.Concat(st.Squads.SelectMany(q => q.Units ?? new())).Count(u => u.Relic is not null);
                                    while (starts[key].Count <= (c * 3 + i)) starts[key].Add(default);
                                    starts[key][c * 3 + i] = (st.Bench.Count, units, st.SquadCap, st.Gold, relics);
                                }
                                Play(st, pol[i]);
                                won = st.Won;
                                if (won)
                                {
                                    var end = st.TakeCarry();
                                    pack = end;
                                    if (i == 2) ends13[key].Add((end.Bench.Count, end.Units));
                                }
                                else if (a < RetryCap) retries[i]++;
                            }
                            if (!won) { stuck[i]++; all = false; }
                        }
                        if (all) through++;
                    }
                    catch (Exception ex) { Crash($"{key} seed {c}", ex); }
                }
                write($"  | {(rule.Carry ? "**持ち越し（第461期）**" : "第460期（持ち越し無し）")} | {cname} | {through}% | {(double)retries[0] / Seeds:F2} | {(double)retries[1] / Seeds:F2} | {(double)retries[2] / Seeds:F2} | 1-1 {stuck[0]}・1-2 {stuck[1]}・1-3 {stuck[2]} |");
            }

        write("");
        write("  | 1 章 | 方針 | ステージ | 始めの手札（平均 ／ 最大） | 始めの駒（平均 ／ 最大） | 隊の枠（平均 ／ 最大） | 金（平均 ／ 最大） | レリック（平均 ／ 最大） | 着いた通し |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (key, list) in starts)
            for (int i = 0; i < 3; i++)
            {
                var xs = list.Where((_, k) => k % 3 == i).Where(x => x != default).ToList();
                if (xs.Count == 0) continue;
                string Am(Func<(int Hand, int Units, int Cap, int Gold, int Relics), int> f) => $"{xs.Average(f):F1} ／ {xs.Max(f)}";
                var parts = key.Split('|');
                write($"  | {parts[0]} | {parts[1]} | {Chapter1.StagesFor(ChapterRule.V461)[i].Id} | {Am(x => x.Hand)} | {Am(x => x.Units)} | {Am(x => x.Cap)} | {Am(x => x.Gold)} | {Am(x => x.Relics)} | {xs.Count} |");
            }
        write("");
        write("  | 1 章 | 方針 | 1-3 の終わりの手札（平均 ／ 最大） | 1-3 の終わりの駒（平均 ／ 最大） | 1-3 に勝った通し |");
        write("  |---|---|--:|--:|--:|");
        foreach (var (key, list) in ends13)
        {
            var parts = key.Split('|');
            if (list.Count == 0) { write($"  | {parts[0]} | {parts[1]} | — | — | 0 |"); continue; }
            write($"  | {parts[0]} | {parts[1]} | {list.Average(x => x.Hand):F1} ／ {list.Max(x => x.Hand)} | {list.Average(x => x.Units):F1} ／ {list.Max(x => x.Units)} | {list.Count} |");
        }
        Req(crashed == 0, $"器具（1 章の通し・持ち越しあり ／ 無し × 2 方針 × {Seeds} seed）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }
}
