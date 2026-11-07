using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 1 章の勇者の回復を外す門（第465期 §3・`--chapter1-nomend-smoke`・指示書 design/PHASE465_HERO_NOMEND_SPEC.md）
//
// **画面を1つも作らない。** `Chapter1`（`ChapterRule.V465`）／ `MapFoeScale` ／ `Map11State` と器具の方針（`Map12ChangeSmoke.Greedy`）だけを回す。
//
//   (a) 1 章の 1-2 ・ 1-3 の勇者は回復の札を持たない。HP ・攻撃は倍率どおり（1-2 2160 ／ 11 ・ 1-3 3840 ／ 19）
//   (b) 1 章の勇者の HP は戦闘をまたいで持ち越され、戦闘の中で増えない（1 隊を当てて負けた後、勇者の HP は減ったまま）
//   (c) 1-3 の敵の倍率が ×1.6（勇者も）
//   (d) 単発の 1-2 ／ 1-3 の勇者は回復 300 を持つ・第460〜464期の 1 章は今のまま
//   (e) 勇者の説明文に回復の行が無い（1 章だけ）
//
// 加えて（報告だけ・線は置かない）:
//   §5 1 章の通し × 100 seed × 方針 2 つ（第465期 ／ 第464期）・勇者との戦闘（数・負け・倒すまでに当てた隊・1 戦目の後の HP）・戦闘の終わりの HP
// =====================================================================================

public static class Chapter1NomendSmoke
{
    public const int Seeds = 100;
    public const int RetryCap = 20;

    private readonly record struct Policy(string Name, bool Guard, bool Split, Map12ChangeSmoke.HeroPlan Plan);
    private static readonly Policy Keep1 = new("1 隊残す", true, false, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Split = new("分担＋戻り番", true, true, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Flee = new("逃げる", true, true, Map12ChangeSmoke.HeroPlan.Flee);

    private static void Play(Map11State st, Policy p)
        => Map12ChangeSmoke.Greedy(st.Seed, st.Stage, p.Guard, p.Split, false, state: st, siegeRest: true, heroPlan: p.Plan);

    /// <summary>盤に出る敵の部隊の一覧（経路の区画・湧き・予備隊・勇者）。</summary>
    private static List<Formation> Foes(MapStage s)
    {
        var list = new List<Formation>();
        foreach (var lane in s.Lanes) foreach (Map11.RoadNode n in lane) list.Add(n.Enemy);
        for (int r = 0; r < s.LaneCount; r++) list.Add(s.Spawn(r).Enemy);
        if (s.Reserve.Enemy is { } re) list.Add(re);
        if (s.Hero.Unit is { } hu) list.Add(Formation.Build(center: hu));
        return list;
    }

    /// <summary>そのステージの勇者を戦闘に立てた姿（`Map11State` と同じ口: 定義の写し ＋ `MendHero`）。</summary>
    private static UnitState HeroUnit(MapStage s)
        => s.FoeScale.MendHero(BattleEngine.Materialize(Formation.Build(center: s.Hero.Unit!), BattleContext.EnemyTeam, EnemyScaleRule.None))[0];

    private static int MendOf(UnitState u) => u.Traits.OfType<BossMendTrait>().Select(m => m.Fixed).DefaultIfEmpty(0).Max();

    /// <summary>勇者との 1 戦（測るためだけ）。</summary>
    private readonly record struct HeroFight(Map11State State, int Stage, int Before, int After, int Max, bool Won, int HeroHeals, bool StartMatches);

    public static bool Run(Action<string> write, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V465;
        var ch = Chapter1.StagesFor(rule);
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}");
        write($"  倍率 {string.Join(" ／ ", ch.Select(s => $"{s.Id} {s.FoeScale.Text}{(s.FoeScale.HeroMends ? "" : "（勇者の回復なし）")}"))}");

        // ---- (a)(e) 1 章の勇者 ----
        {
            bool okA = true, okE = true;
            var lines = new List<string>();
            foreach (var (i, hp, atk) in new[] { (1, 2160, 11), (2, 3840, 19) })
            {
                MapStage s = ch[i];
                UnitDef d = s.Hero.Unit!;
                UnitState u = HeroUnit(s);
                okA &= !d.Traits.Contains(TraitId.BossMend300) && d.MaxHp == hp && d.Attack == atk && MendOf(u) == 0 && !u.Traits.Any(t => t is BossMendTrait)
                       && d.Traits.Contains(TraitId.BossRise11) && d.Traits.Contains(TraitId.BossSteadfast) && d.Speed == EnemyCatalog.HeroMap.Speed
                       && s.Hero.Unit!.Id == EnemyCatalog.HeroMap.Id;
                okE &= !d.PlusText.Contains("癒える") && d.PlusText.Contains("傷は戦いをまたいで残る");
                lines.Add($"{s.Id} HP {d.MaxHp} 攻 {d.Attack} 札 [{string.Join("・", d.Traits)}]「{d.PlusText}」");
            }
            foreach (string l in lines) write($"  (a) {l}");
            Req(okA, "(a) 1 章の 1-2 ・ 1-3 の勇者は回復の札を持たない（ほかの札はそのまま）。HP ・攻撃は倍率どおり（1-2 2160 ／ 11 ・ 1-3 3840 ／ 19）");

            // (e) は (d) の単発の文と並べて下で判定する
            var single = MapStage.Stage12.Hero.Unit!;
            write($"  (e) 単発の 1-2 の勇者「{single.PlusText}」／ 第464期の 1 章の 1-2「{Chapter1.StagesFor(ChapterRule.V464)[1].Hero.Unit!.PlusText}」");
            Req(okE && single.PlusText.Contains("300 癒える") && Chapter1.StagesFor(ChapterRule.V464)[1].Hero.Unit!.PlusText.Contains("270 癒える"),
                "(e) 勇者の説明文に回復の行が無い（1 章だけ・単発は「300 癒える」・第464期の 1 章は「270 癒える」のまま）");
        }

        // ---- (b) 勇者の HP は持ち越され、戦闘の中で増えない ----
        {
            var fights = new List<HeroFight>();
            var fights464 = new List<HeroFight>();
            List<HeroFight> sink = fights;
            Map12ChangeSmoke.FightHook = (s, squad, node, intercept) =>
            {
                var prep = intercept ? s.PrepareIntercept(squad, node) : s.Prepare(squad, node);
                if (prep is not { } q) return;
                int before = node.HpNow;
                UnitState? h = node.IsHero ? q.Enemies.FirstOrDefault(u => u.Def.Id == EnemyCatalog.HeroMap.Id) : null;
                int startHp = h?.Hp ?? before;
                BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: node.IsHero);
                int heals = h is null ? 0 : r.Events.Count(e => e.Kind == BattleEventKind.Heal && e.TargetId == h.InstanceId && e.Amount > 0);
                bool start = h is null || startHp == before;
                s.Resolve(squad, q.Node, r.PlayerWon, intercept);
                if (node.IsHero) sink.Add(new HeroFight(s, 1, before, node.HpNow, node.DefMaxHp, r.PlayerWon, heals, start));
            };
            try
            {
                foreach (var (stages, list) in new[] { (ch, fights), (Chapter1.StagesFor(ChapterRule.V464), fights464) })
                {
                    sink = list;
                    for (int c = 0; c < 40; c++)
                    {
                        MapStage s = stages[1];
                        var st = new Map11State(Chapter1.StageSeed(c, 1, 0), s.Time, s.Portal, draft: true, stage: s);
                        Play(st, Keep1);
                    }
                }
            }
            finally { Map12ChangeSmoke.FightHook = null; }
            // 同じ配りの続けての 2 戦: 2 戦目の始めの HP ＝ 1 戦目の終わりの HP
            bool chained = true;
            int pairs = 0;
            foreach (var g in fights.GroupBy(f => f.State))
            {
                var xs = g.ToList();
                for (int k = 1; k < xs.Count; k++) { pairs++; chained &= xs[k].Before == xs[k - 1].After; }
            }
            var lost = fights.Where(f => !f.Won).ToList();
            bool dented = lost.Any(f => f.After < f.Max);
            bool noHeal = fights.All(f => f.HeroHeals == 0 && f.After <= f.Before);
            int heal464 = fights464.Sum(f => f.HeroHeals);
            int up464 = fights464.Count(f => f.After > f.Before - 1 && !f.Won);
            HeroFight? ex = lost.Where(f => f.After < f.Max).Select(f => (HeroFight?)f).FirstOrDefault();
            write($"  (b) 第465期の 1 章の 1-2 × 40 配り（1 隊残す）: 勇者との戦闘 {fights.Count}（負け {lost.Count}）・勇者が戦闘の中で癒えた回数 {fights.Sum(f => f.HeroHeals)}"
                  + $"・負けた戦闘の後に HP が減ったまま {lost.Count(f => f.After < f.Max)} ／ {lost.Count}・続けての戦闘 {pairs} 組で始めの HP ＝ 前の終わりの HP {chained}");
            if (ex is { } e) write($"      例: 勇者 HP {e.Before} → {e.After}（最大 {e.Max}）の負け");
            write($"      第464期の 1 章の 1-2 × 40 配り: 勇者との戦闘 {fights464.Count}（負け {fights464.Count(f => !f.Won)}）・勇者が戦闘の中で癒えた回数 {heal464}・負けて HP が減らなかった戦闘 {up464}");
            Req(fights.Count > 0 && lost.Count > 0 && dented && noHeal && chained && pairs > 0 && fights.All(f => f.StartMatches) && heal464 > 0,
                "(b) 1 章の勇者の HP は戦闘をまたいで持ち越され、戦闘の中で増えない（負けた後も減ったまま・第464期は戦闘の中で癒える）");
        }

        // ---- (c) 1-3 ×1.6 ----
        {
            MapStage plain = MapStage.Stage13, s = ch[2];
            var a = Foes(plain); var b = Foes(s);
            bool okC = a.Count == b.Count && s.FoeScale == new MapFoeScale(160, 160, HeroMends: false);
            int n = 0;
            for (int k = 0; k < Math.Min(a.Count, b.Count); k++)
                foreach (var (x, y) in a[k].Occupied().Zip(b[k].Occupied()))
                {
                    n++;
                    okC &= y.Def.MaxHp == MapFoeScale.Mul(x.Def.MaxHp, 160) && y.Def.Attack == MapFoeScale.Mul(x.Def.Attack, 160) && y.Def.Speed == x.Def.Speed;
                }
            bool others = ch[0].FoeScale.FoePercent == 60 && ch[1].FoeScale.FoePercent == 90 && ch[1].FoeScale.HeroPercent == 90;
            write($"  (c) 1-3 の敵 {n} 体・{s.FoeScale.BarText}・勇者 HP {s.Hero.Unit!.MaxHp}・1-1 ×{ch[0].FoeScale.FoePercent / 100.0:0.0#} ・1-2 ×{ch[1].FoeScale.FoePercent / 100.0:0.0#} は第464期のまま {others}");
            Req(okC && n > 0 && others, "(c) 1-3 の敵の倍率が ×1.6（勇者も・最大HP と攻撃だけ）。1-1 ×0.6 ・1-2 ×0.9 は今のまま");
        }

        // ---- (d) 単発・第460〜464期 ----
        {
            var parts = new List<string>();
            bool okD = true;
            foreach (MapStage s in new[] { MapStage.Stage12, MapStage.Stage13 })
            {
                int m = MendOf(HeroUnit(s));
                okD &= m == BossMendTrait.Fixed300 && s.FoeScale.HeroMends;
                parts.Add($"単発 {s.Id} 回復 {m}");
            }
            foreach (var (r, want12, want13) in new[] { (ChapterRule.V462, 390, 480), (ChapterRule.V463, 390, 480), (ChapterRule.V464, 270, 390) })
            {
                var st = Chapter1.StagesFor(r);
                int m12 = MendOf(HeroUnit(st[1])), m13 = MendOf(HeroUnit(st[2]));
                okD &= m12 == want12 && m13 == want13 && st.All(x => x.FoeScale.HeroMends);
                parts.Add($"{r.Name.Split('（')[0]} 1-2 {m12} ／ 1-3 {m13}");
            }
            foreach (ChapterRule r in new[] { ChapterRule.V460, ChapterRule.V461 })
                foreach (MapStage s in Chapter1.StagesFor(r).Skip(1)) { okD &= MendOf(HeroUnit(s)) == BossMendTrait.Fixed300; }
            parts.Add("第460 ／ 461期 300");
            okD &= ChapterRule.V464.Stars == StarRule.V464 && ChapterRule.V465.Stars == StarRule.V464 && ChapterRule.V465.Deal == DealRule.V464;
            write($"  (d) 勇者の回復: {string.Join("・", parts)}・第465期は☆・強化・配りが第464期と同じ規則 {ChapterRule.V465.Stars == StarRule.V464 && ChapterRule.V465.Deal == DealRule.V464}");
            write("      （単発の頭なしの門・画面の門・第460〜464期の 1 章の門・`compare` ／ `docs/` が 0 行かは回帰で見る）");
            Req(okD, "(d) 単発の 1-2 ／ 1-3 の勇者は回復 300 を持つ・第460〜464期の 1 章は今のまま");
        }

        int crashed = 0;
        void Crash(string what, Exception ex) { crashed++; if (crashed <= 3) write($"  × {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        if (bots)
        {
            try { BotTables(write, Crash); }
            catch (Exception ex) { Crash("§5 器具", ex); }
        }
        Req(crashed == 0, $"器具（{(bots ? $"1 章の通し × 2 方針 × {Seeds} seed" : "なし")}）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }

    private static double Med(IEnumerable<double> xs) { var o = xs.OrderBy(x => x).ToList(); return o.Count == 0 ? double.NaN : o.Count % 2 == 1 ? o[o.Count / 2] : (o[o.Count / 2 - 1] + o[o.Count / 2]) / 2; }

    private enum Cause { Fallen, TimeUp, Wiped, Stalled, Other }

    /// <summary>§5: 1 章の通し（第465期 ／ 第464期）× 方針 2 つ × 100 seed（第464期 §5 と同じ買い方）。</summary>
    private static void BotTables(Action<string> write, Action<string, Exception> crash)
    {
        string curKey = "";
        int curStage = 0;
        var battles = new List<(string Key, int Stage, bool Won, bool Hero, double Hp)>();
        var heroFights = new List<(string Key, int Stage, Map11State State, int Before, int After, int Max, bool Won)>();
        Map12ChangeSmoke.FightHook = (s, squad, node, intercept) =>
        {
            var prep = intercept ? s.PrepareIntercept(squad, node) : s.Prepare(squad, node);
            if (prep is not { } q) return;
            int before = node.HpNow;
            BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
            int max = q.Players.Sum(u => u.MaxHp);
            double hp = max == 0 ? 0 : 100.0 * q.Players.Where(u => u.IsAlive).Sum(u => u.Hp) / max;
            battles.Add((curKey, curStage, r.PlayerWon, node.IsHero, hp));
            s.Resolve(squad, q.Node, r.PlayerWon, intercept);
            if (node.IsHero) heroFights.Add((curKey, curStage, s, before, node.HpNow, node.DefMaxHp, r.PlayerWon));
        };
        var causes = new Dictionary<string, int[,]>();
        var ends = new Dictionary<string, List<(int Stage, int S1, int S2, int S3, int Gold)>>();
        try
        {
            write("");
            write($"  §5 1 章の通し（1 章の seed 0〜{Seeds - 1}・負ければ同じステージをやり直す・1 ステージ {RetryCap} 回まで・買い方は第464期と同じ——穴を埋めてから前の席から強化）");
            write($"  | 1 章 | 方針 | 通せた | 1-1 のやり直し（平均） | 1-2 のやり直し（平均） | 1-3 のやり直し（平均） | 止まったステージ（やり直し {RetryCap} 回を超えた） |");
            write("  |---|---|--:|--:|--:|--:|---|");
            foreach (ChapterRule rule in new[] { ChapterRule.V465, ChapterRule.V464 })
                foreach (var (cname, pol) in new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) })
                {
                    var stages = Chapter1.StagesFor(rule);
                    string key = $"{(rule.ScaleOf(1).HeroMends ? "第464期（回復あり・1-3 ×1.3）" : "**第465期（回復なし・1-3 ×1.6）**")}|{cname}";
                    curKey = key;
                    causes[key] = new int[3, 5];
                    ends[key] = new();
                    int through = 0;
                    var retries = new int[3];
                    var stuck = new int[3];
                    for (int c = 0; c < Seeds; c++)
                    {
                        try
                        {
                            Map11State.CarryPack? pack = null;
                            bool all = true;
                            for (int i = 0; i < 3 && all; i++)
                            {
                                curStage = i;
                                bool won = false;
                                for (int a = 0; a <= RetryCap && !won; a++)
                                {
                                    MapStage stage = stages[i];
                                    var st = new Map11State(Chapter1.StageSeed(c, i, a), stage.Time, stage.Portal, draft: true, stage: stage);
                                    if (pack is not null) st.ApplyCarry(pack);
                                    Play(st, pol[i]);
                                    won = st.Won;
                                    if (won)
                                    {
                                        pack = st.TakeCarry();
                                        var units = pack.Bench.Concat(pack.Squads.SelectMany(q => q)).ToList();
                                        int Count(int s) => units.Count(u => (pack.Stars is { } ps && ps.TryGetValue(u.Def.Id, out StarState x) ? x.Star : 3) == s);
                                        ends[key].Add((i, Count(1), Count(2), Count(3), pack.Gold));
                                        continue;
                                    }
                                    Cause last = st.Fallen ? Cause.Fallen : st.TimeUp ? Cause.TimeUp : st.HeroStalled ? Cause.Stalled
                                               : st.Squads.All(q => q.Lost || q.Units is null || !q.Units.Any(u => u.IsAlive)) ? Cause.Wiped : Cause.Other;
                                    causes[key][i, (int)last]++;
                                    if (a < RetryCap) retries[i]++;
                                }
                                if (!won) { stuck[i]++; all = false; }
                            }
                            if (all) through++;
                        }
                        catch (Exception ex) { crash($"{key} seed {c}", ex); }
                    }
                    var parts = key.Split('|');
                    write($"  | {parts[0]} | {parts[1]} | {through}% | {(double)retries[0] / Seeds:F2} | {(double)retries[1] / Seeds:F2} | {(double)retries[2] / Seeds:F2} | 1-1 {stuck[0]}・1-2 {stuck[1]}・1-3 {stuck[2]} |");
                }
        }
        finally { Map12ChangeSmoke.FightHook = null; }

        string Id(int i) => Chapter1.StagesFor(ChapterRule.V465)[i].Id;

        write("");
        write("  負けた配りの切り分け（やり直したぶんも止まったぶんも数える）");
        write("  | 1 章 | 方針 | ステージ | 負けた配り | 陥落 | 時間切れ | 全滅 | 勇者の打ち切り | その他 |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (key, arr) in causes)
        {
            var parts = key.Split('|');
            for (int i = 0; i < 3; i++)
                write($"  | {parts[0]} | {parts[1]} | {Id(i)} | {Enumerable.Range(0, 5).Sum(k => arr[i, k])} | {arr[i, 0]} | {arr[i, 1]} | {arr[i, 2]} | {arr[i, 3]} | {arr[i, 4]} |");
        }

        write("");
        write("  勇者との戦闘（すべての配り）: 「倒すまでに当てた隊」は勇者を倒した配りで、倒した 1 戦を含む勇者との戦闘の数（平均）。「1 戦目の後の勇者の HP」は勇者と 1 戦以上した配りの 1 戦目の終わり（最大HP に対する割合・中央値・倒した 1 戦目は 0%）");
        write("  | 1 章 | 方針 | ステージ | 勇者との戦闘 | 負け | 勇者を倒した配り | 倒すまでに当てた隊（平均） | 1 戦目の後の勇者の HP（中央値） | 1 戦目で倒した配り |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (string key in causes.Keys)
        {
            var parts = key.Split('|');
            for (int i = 1; i < 3; i++)
            {
                var hs = heroFights.Where(h => h.Key == key && h.Stage == i).ToList();
                var byState = hs.GroupBy(h => h.State).Select(g => g.ToList()).ToList();
                var killed = byState.Where(g => g.Any(h => h.Won)).ToList();
                double per = killed.Count == 0 ? double.NaN : killed.Average(g => g.FindIndex(h => h.Won) + 1);
                double first = Med(byState.Select(g => 100.0 * g[0].After / Math.Max(1, g[0].Max)));
                write($"  | {parts[0]} | {parts[1]} | {Id(i)} | {hs.Count} | {hs.Count(h => !h.Won)} | {killed.Count} | {per:F2} | {first:F0}% | {byState.Count(g => g[0].Won)} |");
            }
        }

        write("");
        write("  戦闘の終わりの HP（すべての配り・やり直しを含む・中央値）／ ステージの終わり（勝った配り）の☆と金の余り（平均）");
        write("  | 1 章 | 方針 | ステージ | 戦闘 | 終わりの HP | 勝った戦闘の HP | 隊を失った戦闘 | 勝った配り | ☆1 | ☆2 | ☆3 | 金の余り |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string key in causes.Keys)
            for (int i = 0; i < 3; i++)
            {
                var bs = battles.Where(b => b.Key == key && b.Stage == i).ToList();
                var xs = ends[key].Where(e => e.Stage == i).ToList();
                string Am(Func<(int Stage, int S1, int S2, int S3, int Gold), int> f) => xs.Count == 0 ? "—" : $"{xs.Average(f):F1}";
                var parts = key.Split('|');
                write($"  | {parts[0]} | {parts[1]} | {Id(i)} | {bs.Count} | {Med(bs.Select(b => b.Hp)):F0}% | {Med(bs.Where(b => b.Won).Select(b => b.Hp)):F0}% | {bs.Count(b => !b.Won)} | {xs.Count} | {Am(x => x.S1)} | {Am(x => x.S2)} | {Am(x => x.S3)} | {Am(x => x.Gold)} |");
            }
    }
}
