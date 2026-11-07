using BattleCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================================
// 軸への投資の門（第467期 §5・`--chapter1-invest-smoke`・指示書 design/PHASE467_INVEST_SPEC.md）
//
// **画面を1つも作らない。** `Chapter1`（`ChapterRule.V467`）／ `InvestRule` ／ `Map11State` ／ `Map11Info.DownsOf` と器具の方針（`Map12ChangeSmoke.Greedy`）だけを回す。
//
//   (a) 各ステージの始めに 10 軸から重複なしの 3 枚。同じ seed なら同じ 3 枚。やり直しでも同じ
//   (b) 選んだ軸が持ち越され、重なる（1-1 燃焼・1-2 燃焼 → 1-3 は燃焼 ×2・50%）
//   (c) 呼ぶ候補 1000 枚を引いて、投資の枠の割合が 25% ／ 50% ／ 上限 60% に合う。2 軸なら等分
//   (d) 投資した軸の駒を全部持っていれば、今の引き方に戻る
//   (e) 強化を押した後に別の駒を押すと、入れ替わらずに選び直しになる——**画面の口で見る**
//       （絵の口 `--map11-capture=<png>,enhance-press --map-chapter-play=N --map-chapter-stop=1` の `MAP11_CAPTURE_ENHANCE_PRESS ok=`・
//        ここでは投資のある 1 章で強化の規則が今のままかだけを見る）
//   (f) 感電の隊 × 雷杖の部隊の戦闘で、記録に「味方の雷で n 枚」が出る（第465期の写しの隊で）
//   (g) 単発のステージ・第460〜465期の 1 章は今のまま
//
// 加えて（報告だけ・線は置かない）:
//   §7 1 章の通し × 100 seed × 方針 2 つ（第467期 ／ 第465期）・投資は「手持ちにいちばん多い軸（同じなら札の左から）」
//      ・各ステージの始めに 4 枚以上そろった軸の数・投資の札に燃焼が出た割合と器具が燃焼を選んだ割合
// =====================================================================================

public static class Chapter1InvestSmoke
{
    public const int Seeds = 100;
    public const int RetryCap = 20;
    public const int Probes = 1000;

    private readonly record struct Policy(string Name, bool Guard, bool Split, Map12ChangeSmoke.HeroPlan Plan);
    private static readonly Policy Keep1 = new("1 隊残す", true, false, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Split = new("分担＋戻り番", true, true, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Flee = new("逃げる", true, true, Map12ChangeSmoke.HeroPlan.Flee);

    private static void Play(Map11State st, Policy p)
        => Map12ChangeSmoke.Greedy(st.Seed, st.Stage, p.Guard, p.Split, false, state: st, siegeRest: true, heroPlan: p.Plan);

    /// <summary>空の持ち越し（駒なし・隊の枠 2・金 0）に投資の記録だけを載せたもの。</summary>
    private static Map11State.CarryPack Empty(params (string Stage, string Axis)[] invest)
        => new(new List<Map11State.CarryUnit>(), new List<IReadOnlyList<Map11State.CarryUnit>>(), 2, 0, new List<TraitId>(), new Dictionary<string, StarState>(), invest);

    /// <summary>候補を <see cref="Probes"/> 枚引いた結果（投資の枠の枚数・投資の枠から来た駒の軸の内訳・候補に出た駒の Id）。</summary>
    private static (int Invest, Dictionary<string, int> Axes, List<string> Ids) Probe(Map11State st)
    {
        int inv = 0;
        var axes = new Dictionary<string, int>();
        var ids = new List<string>();
        for (int k = 0; k < Probes; k++)
        {
            var (d, isInv) = st.ProbeOffer();
            if (d is null) continue;
            ids.Add(d.Id);
            if (!isInv) continue;
            inv++;
            foreach (string a in MapDraw.AxesOf(d)) axes[a] = axes.GetValueOrDefault(a) + 1;
        }
        return (inv, axes, ids);
    }

    private static Map11State StateAt(IReadOnlyList<MapStage> stages, int index, int seed, Map11State.CarryPack pack)
    {
        MapStage s = stages[index];
        var st = new Map11State(seed, s.Time, s.Portal, draft: true, stage: s);
        st.ApplyCarry(pack);
        return st;
    }

    public static bool Run(Action<string> write, string demoDir, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V467;
        var ch = Chapter1.StagesFor(rule);
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}・投資 {ch[0].Invest.Name}");

        // ---- (a) 札 ----
        {
            bool okA = true;
            var seen = new Dictionary<string, int>();
            int burnStages = 0, stagesN = 0;
            for (int c = 0; c < 1000; c++)
                for (int i = 0; i < 3; i++)
                {
                    var cards = ch[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0));
                    okA &= cards.Count == 3 && cards.Distinct().Count() == 3 && cards.All(a => UnitTags.Axes.Contains(a));
                    okA &= cards.SequenceEqual(ch[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0)));
                    foreach (string a in cards) seen[a] = seen.GetValueOrDefault(a) + 1;
                    stagesN++;
                    if (cards.Contains("燃焼")) burnStages++;
                }
            // 1 章の口: 始め ・やり直し ・始め直し
            Chapter1.Begin(4671, rule);
            var first = Chapter1.InvestCards.ToList();
            bool pending0 = Chapter1.InvestPending;
            Chapter1.PickInvest(0);
            bool picked = !Chapter1.InvestPending && Map11Session.State!.InvestLog.Count == 1;
            Chapter1.Retry();
            var retry = Chapter1.InvestCards.ToList();
            bool pendingR = Chapter1.InvestPending && Map11Session.State!.InvestLog.Count == 0;
            Chapter1.Begin(4671, rule);
            var again = Chapter1.InvestCards.ToList();
            Chapter1.Begin(4672, rule);
            var other = Chapter1.InvestCards.ToList();
            okA &= pending0 && picked && pendingR && first.SequenceEqual(retry) && first.SequenceEqual(again) && first.Count == 3;
            write($"  (a) 1 章の seed 4671 の 1-1: [{string.Join("・", first)}]・やり直し [{string.Join("・", retry)}]・始め直し [{string.Join("・", again)}]・seed 4672 [{string.Join("・", other)}]"
                  + $"・選ぶ前は選んでいない {pending0}・選んだ後 {picked}・やり直すと選び直し {pendingR}");
            write($"      1000 章 × 3 ステージの札に出た軸: {string.Join("・", UnitTags.Axes.Select(a => $"{a} {100.0 * seen.GetValueOrDefault(a) / stagesN:F1}%"))}（どの軸も 3 ／ 10 ＝ 30% が見込み）");
            Req(okA, "(a) 各ステージの始めに 10 軸から重複なしの 3 枚。同じ seed なら同じ 3 枚。やり直しでも同じ（やり直すと選び直す）");
        }

        // ---- (b) 持ち越しと重なり ----
        {
            bool okB = false;
            string detail = "燃焼が 1-1 ・ 1-2 の両方の札に出て、器具が 1-1 ・ 1-2 を勝つ 1 章が見つからない";
            for (int c = 0; c < 300 && !okB; c++)
            {
                if (!ch[0].Invest.CardsFor(Chapter1.StageSeed(c, 0, 0)).Contains("燃焼") || !ch[1].Invest.CardsFor(Chapter1.StageSeed(c, 1, 0)).Contains("燃焼")) continue;
                Chapter1.Begin(c, rule);
                bool won = true;
                var log = new List<string>();
                for (int i = 0; i < 2 && won; i++)
                {
                    Map11State st = Map11Session.State!;
                    var cards = Chapter1.InvestCards;
                    bool carried = st.InvestLog.Count == i && (i == 0 || st.InvestLog[0] == ("1-1", "燃焼"));
                    Chapter1.PickInvest(cards.ToList().IndexOf("燃焼"));
                    log.Add($"{st.Stage.Id} 入った時点の投資 {i} 件（持ち越し {carried}）→ 選んだ後 {st.InvestText}・割合 {st.Stage.Invest.Percent(st.InvestLog.Count)}%");
                    if (!carried) { won = false; break; }
                    Play(st, Split);
                    Chapter1.Settle(st, new Dictionary<(string, TraitId), int>(), Array.Empty<Map11Session.Line>());
                    won = st.Won && Chapter1.Next();
                }
                if (!won) { Chapter1.End(); continue; }
                Map11State s3 = Map11Session.State!;
                Map11State.CarryPack entry = Chapter1.Records[2].Entry!;
                // 写しに書いて読み戻しても同じ投資
                var pack = PlayLog.ToCarry(PlayLog.FromCarry(entry, s3.Stage.Stars), s3.Stage.Stars);
                bool round = pack.Invest is { } inv && inv.SequenceEqual(entry.Invest!);
                okB = s3.Stage.Id == "1-3" && s3.InvestCount("燃焼") == 2 && s3.InvestLog.Count == 2 && s3.Stage.Invest.Percent(s3.InvestLog.Count) == 50
                      && s3.InvestPending && round && Chapter1.Records[0].Invested == "燃焼" && Chapter1.Records[1].Invested == "燃焼";
                log.Add($"1-3 入った時点 {s3.InvestText}・割合 {s3.Stage.Invest.Percent(s3.InvestLog.Count)}%・まだ 1-3 の投資を選んでいない {s3.InvestPending}・写しに書いて読み戻しても同じ {round}");
                detail = $"1 章の seed {c}: " + string.Join(" ／ ", log);
                Chapter1.End();
            }
            write($"  (b) {detail}");
            Req(okB, "(b) 選んだ軸が持ち越され、重なる（1-1 燃焼・1-2 燃焼 → 1-3 は燃焼 ×2・50%・写しにも残る）");
        }

        // ---- (c) 割合 ----
        {
            var rows = new List<(string Name, Map11State St, int Want, Dictionary<string, double> Split)>
            {
                ("1-1 燃焼 ×1", Pick(StateAt(ch, 0, 46701, Empty()), "燃焼"), 25, new() { ["燃焼"] = 1.0 }),
                ("1-2 燃焼 ×2", Pick(StateAt(ch, 1, 46702, Empty(("1-1", "燃焼"))), "燃焼"), 50, new() { ["燃焼"] = 1.0 }),
                ("1-2 燃焼 ×1 ・感電 ×1", Pick(StateAt(ch, 1, 46703, Empty(("1-1", "燃焼"))), "感電"), 50, new() { ["燃焼"] = 0.5, ["感電"] = 0.5 }),
                ("1-3 燃焼 ×2 ・感電 ×1（上限）", Pick(StateAt(ch, 2, 46704, Empty(("1-1", "燃焼"), ("1-2", "感電"))), "燃焼"), 60, new() { ["燃焼"] = 2.0 / 3, ["感電"] = 1.0 / 3 }),
                ("1-3 燃焼 ・感電 ・毒（上限・3 軸）", Pick(StateAt(ch, 2, 46705, Empty(("1-1", "燃焼"), ("1-2", "感電"))), "毒"), 60, new() { ["燃焼"] = 1.0 / 3, ["感電"] = 1.0 / 3, ["毒"] = 1.0 / 3 }),
            };
            bool okC = true;
            foreach (var (name, st, want, split) in rows)
            {
                var (inv, axes, _) = Probe(st);
                double pct = 100.0 * inv / Probes;
                bool rowOk = Math.Abs(pct - want) <= 5.0 && st.Stage.Invest.Percent(st.InvestLog.Count) == want;
                var parts = new List<string>();
                foreach (var (axis, share) in split)
                {
                    double got = inv == 0 ? 0 : (double)axes.GetValueOrDefault(axis) / inv;
                    rowOk &= Math.Abs(got - share) <= 0.07;
                    parts.Add($"{axis} {100 * got:F1}%（見込み {100 * share:F0}%）");
                }
                okC &= rowOk;
                write($"  (c) {name}: 候補 {Probes} 枚のうち投資の枠 {inv} 枚 ＝ {pct:F1}%（見込み {want}%）・枠の中の軸 {string.Join("・", parts)} {(rowOk ? "○" : "×")}");
            }
            // 投資なし（同じ盤・投資の記録なし）は投資の枠 0 枚・乱数を引かない（候補の並びが第465期の盤と同じ）
            Map11State none = StateAt(ch, 1, 46706, Empty());
            MapStage s465 = Chapter1.StagesFor(ChapterRule.V465)[1];
            var old = new Map11State(46706, s465.Time, s465.Portal, draft: true, stage: s465);
            old.ApplyCarry(Empty());
            var (n0, _, ids0) = Probe(none);
            var (_, _, idsOld) = Probe(old);
            bool same = n0 == 0 && ids0.SequenceEqual(idsOld) && none.Offers.Select(d => d.Id).SequenceEqual(old.Offers.Select(d => d.Id));
            write($"  (c) 投資する前（1-2・記録なし）: 投資の枠 {n0} 枚・候補の並びが第465期の盤と同じ {same}");
            okC &= same;
            // 報告だけ（指示書 §4 の予想 2）: 候補 3 枚ひと組（リロール 1 回ぶん）に、狙った 1 体（熾のホタ）／ 燃焼のどれかが入る割合（投資なし ／ 燃焼 ×1 ／ ×2）
            foreach (var (label, st) in new[] { ("投資なし", StateAt(ch, 1, 46709, Empty())), ("燃焼 ×1", Pick(StateAt(ch, 1, 46709, Empty()), "燃焼")),
                                                ("燃焼 ×2", Pick(StateAt(ch, 1, 46709, Empty(("1-1", "燃焼"))), "燃焼")) })
            {
                var burnIds = InvestRule.PiecesOf("燃焼").Select(d => d.Id).ToHashSet();
                int sets = 0, hota = 0, any = 0;
                var (_, _, ids) = Probe(st);
                for (int k = 0; k + 3 <= ids.Count; k += 3)
                {
                    var trio = ids.Skip(k).Take(3).ToList();
                    sets++;
                    if (trio.Contains(UnitCatalog.Hota.Id)) hota++;
                    if (trio.Any(burnIds.Contains)) any++;
                }
                write($"      候補 3 枚ひと組（{sets} 組）・{label}: 熾のホタが入る {100.0 * hota / sets:F1}% ／ 燃焼のどれかが入る {100.0 * any / sets:F1}%");
            }
            Req(okC, $"(c) 呼ぶ候補 {Probes} 枚を引いて、投資の枠の割合が 25% ／ 50% ／ 上限 60% に合う（±5pt）。2 軸なら等分（回数で按分・±7pt）。投資する前は今の引き方");
        }

        // ---- (d) 軸の駒を全部持っていれば今の引き方 ----
        {
            var burn = InvestRule.PiecesOf("燃焼");
            var bench = burn.Select(d => new Map11State.CarryUnit(StarRule.V464.Def(d, 1), -1, null)).ToList();
            var stars = burn.ToDictionary(d => d.Id, _ => new StarState(1, 0));
            var pack = new Map11State.CarryPack(bench, new List<IReadOnlyList<Map11State.CarryUnit>>(), 2, 0, new List<TraitId>(), stars, new[] { ("1-1", "燃焼") });
            Map11State st = Pick(StateAt(ch, 1, 46707, pack), "燃焼");
            var (inv, _, ids) = Probe(st);
            int burnOffered = ids.Count(id => burn.Any(d => d.Id == id));
            // 2 枚だけ持っていない（後ろの 2 枚を外す）なら、その 2 枚だけが投資の枠で出る
            var pack4 = pack with { Bench = bench.Take(bench.Count - 2).ToList() };
            Map11State st4 = Pick(StateAt(ch, 1, 46708, pack4), "燃焼");
            var (inv4, _, ids4) = Probe(st4);
            var missing = burn.TakeLast(2).Select(d => d.Id).ToHashSet();
            bool inOffers = missing.All(id => st4.Offers.Any(o => o.Id == id));   // 残りの 2 枚がもう候補に出ていれば、引ける燃焼の駒は無い
            bool okD = inv == 0 && burnOffered == 0 && st.InvestCount("燃焼") == 2
                       && (inOffers ? inv4 == 0 : inv4 > 0) && ids4.Where(id => burn.Any(d => d.Id == id)).All(missing.Contains);
            write($"  (d) 燃焼の駒 {burn.Count} 体（{string.Join("・", burn.Select(d => d.Name))}）を全部持って燃焼 ×2: 候補 {Probes} 枚のうち投資の枠 {inv} 枚・燃焼の駒 {burnOffered} 枚"
                  + $" ／ {string.Join("・", burn.TakeLast(2).Select(d => d.Name))} だけ持っていない: 投資の枠 {inv4} 枚（{100.0 * inv4 / Probes:F1}%・いまの候補に {burn.TakeLast(2).Count(d => st4.Offers.Any(o => o.Id == d.Id))} 枚）・出た燃焼の駒はその 2 体だけ");
            Req(okD, "(d) 投資した軸の駒を全部持っていれば、今の引き方に戻る（投資の枠は 0 枚・その軸の駒は出ない）");
        }

        // ---- (e) 強化の規則（画面の押し方は絵の口で見る） ----
        {
            bool same = ChapterRule.V467.Stars == StarRule.V464 && ChapterRule.V467.Deal == DealRule.V464 && ReferenceEquals(ChapterRule.V467.Scales, ChapterRule.V465.Scales);
            write("  (e) 画面の押し方（強化の後の 1 押しは選び直し）は絵の口 `--map11-capture=<png>,enhance-press --map-chapter-play=4670 --map-chapter-stop=1` の `MAP11_CAPTURE_ENHANCE_PRESS ok=` で見る（報告 §2）");
            Req(same, "(e) 投資のある 1 章の☆ ・強化 ・1-1 の配り ・敵の倍率は第465期のまま（押し方の直しは画面だけ）");
        }

        // ---- (f) 味方の雷で倒れた ----
        {
            string root = PlayLog.DefaultRoot(demoDir);
            string dir = Path.Combine(root, "70307");
            bool okF = false;
            if (Directory.Exists(dir))
            {
                var squad = FoeWeight.SquadsOf(PlayLog.List(dir), "1-3", root).FirstOrDefault(q => q.No == 2);
                MapStage s = FoeWeight.StageAt(2, ChapterRule.V465.ScaleOf(2));
                FoeWeight.Foe? mage = FoeWeight.FoesOf(s).FirstOrDefault(f => f.Enemy.Occupied().Any(o => o.Def.Id == EnemyCatalog.ShockMage.Id));
                if (squad is not null && mage is not null)
                {
                    int wins = 0, downs = 0;
                    var by = new Dictionary<Map11Info.DownCause, int>();
                    var names = new Dictionary<string, int>();
                    for (int seed = 0; seed < 20; seed++)
                    {
                        List<UnitState> p = FoeWeight.Players(squad, 0, StarRule.V464), e = FoeWeight.Enemies(mage, s);
                        BattleResult r = BattleEngine.Run(p, e, seed, verbose: true);
                        if (r.PlayerWon) wins++;
                        foreach (var (id, cause) in Map11Info.DownsOf(r, p.Select(u => u.InstanceId).ToHashSet()))
                        {
                            downs++;
                            by[cause] = by.GetValueOrDefault(cause) + 1;
                            if (cause == Map11Info.DownCause.AllyShock) { string n = p.First(u => u.InstanceId == id).Def.Name; names[n] = names.GetValueOrDefault(n) + 1; }
                        }
                    }
                    int ally = by.GetValueOrDefault(Map11Info.DownCause.AllyShock);
                    write($"  (f) 写し 70307 の 1-3 第2隊（{string.Join("・", squad.Pieces.Select(x => x.Base.Name + "☆" + x.Star))}）× {mage.Name}（×1.6）× 20 seed: 勝ち {wins} ／ 20・倒れた駒 {downs} 枚"
                          + $"（味方の雷・粉 {ally} ・敵 {by.GetValueOrDefault(Map11Info.DownCause.Foe)} ・味方のほかの一撃 {by.GetValueOrDefault(Map11Info.DownCause.AllyOther)} ・不明 {by.GetValueOrDefault(Map11Info.DownCause.Unknown)}）");
                    write($"      味方の雷・粉で倒れた駒: {string.Join("・", names.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}"))}（第465期の観察ログ (3): 23 枚のうち粉 15 ・放電 3 ・敵 5）");
                    // 記録の口: 1 章（写しの版）の 1-3 で、その隊を雷杖の部隊に当てる（戦闘は画面と同じ `Map11Session.BeginIntercept` ／ `CompleteBattle`）
                    PlayLog.Snap snap = PlayLog.Read(Path.Combine(dir, "1-3_start.json"));
                    int shown = 0, tries = 0;
                    string line = "";
                    for (int k = 0; k < 20 && shown == 0; k++)
                    {
                        tries++;
                        snap.Attempt = k;
                        snap.StageSeed = Chapter1.StageSeed(snap.ChapterSeed, snap.StageIndex, k);
                        Map11Session.PickChapterFrom(snap, snapRule: true);
                        Map11State st = Map11Session.State!;
                        int q = Enumerable.Range(0, st.Squads.Length).FirstOrDefault(i => st.Squads[i].Units is { } us && us.Any(u => u.Def.Id == UnitCatalog.Tou.Id), -1);
                        Map11State.Node? node = st.Nodes.SelectMany(n => n).FirstOrDefault(n => n.Def.Enemy.Occupied().Any(o => o.Def.Id == EnemyCatalog.ShockMage.Id));
                        if (q < 0 || node is null || !Map11Session.BeginIntercept(q, node)) continue;
                        BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: true);
                        CampaignSession.ClearCarriedBattle();
                        Map11Session.CompleteBattle(r.PlayerWon, r);
                        Map11Session.Line l = Map11Session.BattleLog[^1];
                        shown = l.AllyShock;
                        line = $"{l.Squad} → {l.Foe} {(l.Won ? "抜いた" : "抜けず")}（残り {l.Alive}枚 ・ HP {l.HpPercent:F0}%{(l.AllyShock > 0 ? $"（味方の雷で {l.AllyShock} 枚）" : "")}）";
                    }
                    Chapter1.End();
                    write($"      記録の口（1 章・写し 70307 の 1-3 を写しの版で始め、その隊を雷杖の部隊に迎え撃たせる・{tries} 配り目）: {line}");
                    okF = ally > 0 && ally * 2 > downs && shown > 0;
                }
                else write($"  (f) 写し 70307 の 1-3 第2隊 {(squad is null ? "が無い" : "")} ／ 雷杖の部隊 {(mage is null ? "が無い" : "")}");
            }
            else write($"  (f) 写し {dir} が無い");
            Req(okF, "(f) 感電の隊 × 雷杖の部隊の戦闘で、記録に「味方の雷で n 枚」が出る（第465期の写しの隊・倒れた駒の過半が味方の雷・粉）");
        }

        // ---- (g) 単発・第460〜465期 ----
        {
            bool okG = MapStage.All.All(s => !s.Invest.On);
            var parts = new List<string>();
            foreach (var (key, r) in ChapterRule.Versions)
            {
                bool on = Chapter1.StagesFor(r).Any(s => s.Invest.On);
                okG &= on == (key == "V467");
                parts.Add($"{key} {(on ? "投資あり" : "なし")}");
            }
            Chapter1.Begin(4673, ChapterRule.V465);
            bool pending465 = Chapter1.InvestPending || Chapter1.InvestCards.Count > 0;
            bool refused = !Map11Session.State!.Invest("燃焼");
            Chapter1.End();
            var single = new Map11State(4673, MapStage.Stage12.Time, MapStage.Stage12.Portal, draft: true, stage: MapStage.Stage12);
            bool singleRefused = !single.Invest("燃焼") && !single.InvestPending && single.TakeCarry().Invest is null;
            okG &= !pending465 && refused && singleRefused && Chapter1.Default.Name == ChapterRule.V467.Name;
            write($"  (g) 単発の 1-1 ／ 1-2 ／ 1-3 は投資なし・{string.Join("・", parts)}・第465期の 1 章は札を出さず投資を受けない {!pending465 && refused}・単発の 1-2 も受けない {singleRefused}");
            write("      （単発の頭なしの門・画面の門・第460〜465期の 1 章の門・`compare` ／ `docs/` が 0 行かは回帰で見る）");
            Req(okG, "(g) 単発のステージ・第460〜465期の 1 章は今のまま（投資の札も枠も無い）");
        }

        int crashed = 0;
        void Crash(string what, Exception ex) { crashed++; if (crashed <= 3) write($"  × {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        if (bots)
        {
            try { BotTables(write, Crash); }
            catch (Exception ex) { Crash("§7 器具", ex); }
        }
        Req(crashed == 0, $"器具（{(bots ? $"1 章の通し × 2 方針 × 2 版 × {Seeds} seed" : "なし")}）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }

    private static Map11State Pick(Map11State st, string axis)
    {
        if (!st.Invest(axis)) throw new InvalidOperationException($"{st.Stage.Id} で {axis} に投資できない");
        return st;
    }

    /// <summary>持ち越しの駒のうち、同じ軸のキャラが 4 枚以上の軸の数。</summary>
    private static int Axes4(IEnumerable<UnitDef> defs)
    {
        var ids = defs.Select(d => d.Id).Distinct().ToList();
        var all = ids.Select(id => UnitCatalog.Everyone.First(d => d.Id == id)).ToList();
        return UnitTags.Axes.Count(a => all.Count(d => MapDraw.AxesOf(d).Contains(a)) >= 4);
    }

    private static IEnumerable<UnitDef> DefsOf(Map11State.CarryPack p) => p.Bench.Concat(p.Squads.SelectMany(q => q)).Select(c => c.Def);

    /// <summary>§7: 1 章の通し（第467期 ／ 第465期）× 方針 2 つ × 100 seed（第465期 §5 と同じ買い方・投資は器具の選び方）。</summary>
    private static void BotTables(Action<string> write, Action<string, Exception> crash)
    {
        write("");
        write($"  §7 1 章の通し（1 章の seed 0〜{Seeds - 1}・負ければ同じステージをやり直す・1 ステージ {RetryCap} 回まで・買い方は第465期と同じ・投資は「手持ちにいちばん多い軸（同じなら札の左から）」）");
        write($"  | 1 章 | 方針 | 通せた | 1-1 のやり直し | 1-2 のやり直し | 1-3 のやり直し | 止まったステージ | 4 枚以上の軸（1-2 の始め） | （1-3 の始め） | （1-3 の終わり） | 投資の枠から候補に出た枚数（1 章あたり） |");
        write("  |---|---|--:|--:|--:|--:|---|--:|--:|--:|--:|");
        var cardStats = new Dictionary<string, (int Stages, int BurnShown, int BurnPicked, Dictionary<string, int> Picks)>();
        foreach (ChapterRule rule in new[] { ChapterRule.V467, ChapterRule.V465 })
            foreach (var (cname, pol) in new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) })
            {
                var stages = Chapter1.StagesFor(rule);
                string rkey = rule.Invest is { On: true } ? "**第467期（投資あり）**" : "第465期（投資なし）";
                int through = 0;
                var retries = new int[3];
                var stuck = new int[3];
                var ax = new List<int>[3] { new(), new(), new() };
                var axEnd = new List<int>();
                long invShown = 0;
                var cs = (Stages: 0, BurnShown: 0, BurnPicked: 0, Picks: new Dictionary<string, int>());
                for (int c = 0; c < Seeds; c++)
                {
                    try
                    {
                        Map11State.CarryPack? pack = null;
                        bool all = true;
                        for (int i = 0; i < 3 && all; i++)
                        {
                            bool won = false;
                            var cards = stages[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0));
                            for (int a = 0; a <= RetryCap && !won; a++)
                            {
                                MapStage stage = stages[i];
                                var st = new Map11State(Chapter1.StageSeed(c, i, a), stage.Time, stage.Portal, draft: true, stage: stage);
                                if (pack is not null) st.ApplyCarry(pack);
                                if (a == 0 && i > 0) ax[i].Add(Axes4(DefsOf(st.TakeCarry())));
                                if (cards.Count > 0)
                                {
                                    string pick = cards[InvestRule.BotPick(st, cards)];
                                    st.Invest(pick);
                                    if (a == 0)
                                    {
                                        cs.Stages++;
                                        if (cards.Contains("燃焼")) cs.BurnShown++;
                                        if (pick == "燃焼") cs.BurnPicked++;
                                        cs.Picks[pick] = cs.Picks.GetValueOrDefault(pick) + 1;
                                    }
                                }
                                Play(st, pol[i]);
                                invShown += st.InvestOffersShown;
                                won = st.Won;
                                if (won) { pack = st.TakeCarry(); if (i == 2) axEnd.Add(Axes4(DefsOf(pack))); continue; }
                                if (a < RetryCap) retries[i]++;
                            }
                            if (!won) { stuck[i]++; all = false; }
                        }
                        if (all) through++;
                    }
                    catch (Exception ex) { crash($"{rkey} {cname} seed {c}", ex); }
                }
                string Avg(List<int> xs) => xs.Count == 0 ? "—" : $"{xs.Average():F2}（{xs.Count}）";
                write($"  | {rkey} | {cname} | {through}% | {(double)retries[0] / Seeds:F2} | {(double)retries[1] / Seeds:F2} | {(double)retries[2] / Seeds:F2} | 1-1 {stuck[0]}・1-2 {stuck[1]}・1-3 {stuck[2]} | {Avg(ax[1])} | {Avg(ax[2])} | {Avg(axEnd)} | {(double)invShown / Seeds:F1} |");
                if (cs.Stages > 0) cardStats[$"{rkey}|{cname}"] = cs;
            }
        write("  （4 枚以上の軸: その時点の手持ち（隊 ＋ 手札）で、同じ軸のキャラが 4 枚以上の軸の数の平均・括弧は数えた 1 章の数。投資の枠の枚数は、やり直したぶんの配りも含む）");
        write("");
        write("  投資の札と器具の選び（各ステージの最初の配り）");
        write("  | 方針 | 札を出したステージ | 札に燃焼が出た | 器具が燃焼を選んだ | 器具が選んだ軸 |");
        write("  |---|--:|--:|--:|---|");
        foreach (var (key, cs) in cardStats)
            write($"  | {key.Split('|')[1]} | {cs.Stages} | {100.0 * cs.BurnShown / cs.Stages:F1}% | {100.0 * cs.BurnPicked / cs.Stages:F1}% | {string.Join("・", cs.Picks.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}"))} |");
    }
}
