using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 体験版の加入の表の門（第469期 §4・`--chapter1-roster-smoke`・指示書 design/PHASE469_DEMO_ROSTER_SPEC.md）
//
// **画面を1つも作らない。** `Chapter1`（`ChapterRule.V469`）／ `RosterRule` ／ `InvestRule` ／ `Map11State` と器具の方針（`Map12ChangeSmoke.Greedy`）だけを回す。
//
//   (a) 1 章の 1-1 の始めは決まった 6 枚（☆1）。やり直しても・1 章の seed を替えても同じ 6 枚
//   (b) 呼ぶ候補に出るのは、そのステージまでの表の駒だけ（1-1 は 6 体・1-2 は 20 体・1-3 は 27 体）。表の外の 19 体は 1 章を通して 0 回
//   (c) 呼べる駒が 2 体・0 体のときの候補の出方（ある分だけ・0 体ならリロールは押せない）
//   (d) 軸の札は、呼べる駒が 1 体以上いる軸だけ。1-1 は 1-1 の表の駒の軸の中から。出せる軸が無ければ盤・金の札で埋める
//   (e) 持ち越し・やり直し・☆・強化・盤と金の札は第468期のまま
//   (f) 単発のステージ・第460〜468期の 1 章は今のまま
//
// 加えて（報告だけ・線は置かない）: §6 1 章の通し × 100 seed（第469期 ／ 第468期 × 方針 2 つ）——通せた率・やり直し・止まったステージ・
// 各ステージの始めに 4 枚以上そろった軸の数・1-1 で呼び切った配りの割合・各ステージの終わりの「呼べる駒 あと n 体」。
// =====================================================================================

public static class Chapter1RosterSmoke
{
    public const int Seeds = 100;
    public const int RetryCap = 20;

    private readonly record struct Policy(string Name, bool Guard, bool Split, Map12ChangeSmoke.HeroPlan Plan);
    private static readonly Policy Keep1 = new("1 隊残す", true, false, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Split = new("分担＋戻り番", true, true, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Flee = new("逃げる", true, true, Map12ChangeSmoke.HeroPlan.Flee);

    private static void Play(Map11State st, Policy p)
        => Map12ChangeSmoke.Greedy(st.Seed, st.Stage, p.Guard, p.Split, false, state: st, siegeRest: true, heroPlan: p.Plan);

    private static string Names(IEnumerable<UnitDef> ds) => string.Join("・", ds.Select(d => d.Name));

    /// <summary>手持ち（隊・手札・倒れた駒）のキャラの Id。</summary>
    private static HashSet<string> Owned(Map11State st)
        => st.Squads.SelectMany(q => q.Units ?? new List<UnitState>()).Concat(st.Bench).Concat(st.FallenUnits).Select(u => u.Def.Id).ToHashSet();

    public static bool Run(Action<string> write, string demoDir, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V469;
        RosterRule roster = RosterRule.V469;
        var ch = Chapter1.StagesFor(rule);
        var start = roster.StartFor("1-1")!;
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}・加入の表 {roster.Name}");
        write($"  始めの 6 枚: {Names(start)}");
        foreach (var (stage, _) in roster.Joins)
            write($"  {stage} から呼べる駒（{roster.JoinsAt(stage).Count} 体）: {Names(roster.JoinsAt(stage))}・呼べる駒の合計 {roster.PoolFor(stage).Count} 体");
        var all33 = start.Concat(roster.PoolFor("1-3")).Select(d => d.Id).ToHashSet();
        var outside = UnitCatalog.All.Where(d => !all33.Contains(d.Id)).ToList();
        write($"  表の外（1 章では呼ぶ候補に出ない）{outside.Count} 体: {Names(outside)}");

        // ---- (a) 始めの 6 枚 ----
        {
            var startIds = start.Select(d => d.Id).ToList();
            bool okA = Chapter1.Default.Name == rule.Name && start.Count == 6;
            string Hand() => string.Join("・", Map11Session.State!.Bench.Select(u => $"{u.Def.Name}☆{Map11Session.State.StarOf(u.Def.Id).Star}"));
            bool Fixed() => Map11Session.State!.Bench.Select(u => u.Def.Id).SequenceEqual(startIds)
                            && Map11Session.State.Bench.All(u => Map11Session.State.StarOf(u.Def.Id).Star == 1)
                            && Map11Session.State.Squads.All(q => q.Units is { Count: 0 }) && Map11Session.State.SquadCap == 2 && Map11Session.State.Gold == 8;
            int same = 0;
            for (int c = 0; c < 200; c++) { Chapter1.Begin(c, rule); if (Fixed()) same++; }
            Chapter1.Begin(4691, rule);
            string h0 = Hand();
            bool f0 = Fixed();
            int seed0 = Map11Session.State!.Seed;
            Chapter1.Retry();
            bool f1 = Fixed() && Map11Session.State!.Seed != seed0;
            Chapter1.Retry();
            bool f2 = Fixed();
            okA &= same == 200 && f0 && f1 && f2;
            write($"  (a) 1 章の seed 4691 の 1-1: {h0}・隊の枠 {Map11Session.State!.SquadCap}・金 {Map11Session.State.Gold}・やり直し 2 回とも同じ 6 枚 {f1 && f2}（配りの seed は替わる）・1 章の seed 0〜199 で同じ 6 枚 {same} ／ 200・遊ぶ 1 章は第469期 {Chapter1.Default.Name == rule.Name}");
            Req(okA, "(a) 1 章の 1-1 の始めは決まった 6 枚（☆1・手札 6・隊の枠 2・金 8）。やり直しても・seed を替えても同じ 6 枚");
        }

        // ---- (b) 呼ぶ候補はそのステージまでの表の駒だけ ----
        {
            bool okB = true;
            var parts = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                MapStage stage = ch[i];
                var pool = roster.PoolFor(stage.Id).Select(d => d.Id).ToHashSet();
                var seen = new Dictionary<string, int>();
                int shown = 0;
                for (int c = 0; c < 200; c++)
                {
                    var st = new Map11State(Chapter1.StageSeed(c, i, 0), stage.Time, stage.Portal, draft: true, stage: stage);
                    void Note(IEnumerable<UnitDef> ds) { foreach (UnitDef d in ds) { seen[d.Id] = seen.GetValueOrDefault(d.Id) + 1; shown++; } }
                    Note(st.Offers);
                    // 投資あり（seed で軸を 1 つ）・候補を引く ・呼ぶ ・リロールを重ねる
                    if (c % 2 == 1) st.Invest(UnitTags.Axes[c % UnitTags.Axes.Length]);
                    Note(st.Offers);
                    for (int k = 0; k < 20; k++) { var (d, _) = st.ProbeOffer(); if (d is not null) Note(new[] { d }); }
                    st.GrantGoldForCheck(400);
                    for (int k = 0; k < 30; k++)
                    {
                        if (k % 3 == 2 ? !st.Reroll() : !st.Recruit(0)) { if (!st.Reroll()) break; }
                        Note(st.Offers);
                    }
                }
                var bad = seen.Keys.Where(id => !pool.Contains(id)).ToList();
                var never = pool.Where(id => !seen.ContainsKey(id)).ToList();
                okB &= bad.Count == 0 && never.Count == 0 && pool.Count == (i == 0 ? 6 : i == 1 ? 20 : 27);
                parts.Add($"{stage.Id} 母数 {pool.Count} 体・候補に出た {shown} 枚（{seen.Count} 体）・表の外 {bad.Count} 体{(bad.Count > 0 ? $"（{string.Join("・", bad)}）" : "")}・1 度も出ない表の駒 {never.Count}");
            }
            write($"  (b) ステージを単独で 200 配り × （始め ＋ 投資 ＋ 候補 20 枚 ＋ 呼ぶ ／ リロール 30 回）: {string.Join(" ／ ", parts)}");
            Req(okB, "(b) 呼ぶ候補に出るのは、そのステージまでの表の駒だけ（1-1 は 6 体・1-2 は 20 体・1-3 は 27 体・表の駒はどれも出る）。1 章を通して表の外が 0 回かは §6 の器具の通しで見る");
        }

        // ---- (c) 呼べる駒が 2 体 ／ 0 体 ----
        {
            Chapter1.Begin(4692, rule);
            Map11State st = Map11Session.State!;
            st.GrantGoldForCheck(200);
            Chapter1.AutoInvest();
            while (st.RosterLeft.Count > 2 && st.Recruit(0)) { }
            int left2 = st.RosterLeft.Count;
            var offers2 = st.Offers.Select(d => d.Name).ToList();
            bool reroll2 = st.CanReroll && st.Reroll();
            var offersR = st.Offers.Select(d => d.Name).ToList();
            bool okC2 = left2 == 2 && offers2.Count == 2 && offersR.Count == 2 && offers2.OrderBy(x => x).SequenceEqual(offersR.OrderBy(x => x)) && reroll2;
            while (st.Offers.Count > 0 && st.Recruit(0)) { }
            int left0 = st.RosterLeft.Count;
            bool okC0 = left0 == 0 && st.Offers.Count == 0 && !st.CanReroll && !st.Reroll() && !st.CanRecruit(0);
            write($"  (c) 1-1（1 章の seed 4692）: 呼べる駒 あと {left2} 体で候補 [{string.Join("・", offers2)}]・リロール後 [{string.Join("・", offersR)}]（ある分だけ・同じ 2 体）／ あと {left0} 体で候補 {st.Offers.Count} 枚・リロール押せる {st.CanReroll}・呼べる {st.CanRecruit(0)}・手持ち {Owned(st).Count} 体"
                  + "（画面は「呼べる駒がいない」——絵の口で見る）");
            Req(okC2 && okC0, "(c) 呼べる駒が 3 体に満たないときは候補はある分だけ・1 体も無ければ候補 0 枚でリロールも呼ぶも押せない");
        }

        // ---- (d) 軸の札 ----
        {
            bool okD = true;
            var open11 = Chapter1.OpenAxesOf(new Map11State(1, ch[0].Time, ch[0].Portal, draft: true, stage: ch[0]))!;
            var want11 = roster.JoinsAt("1-1").SelectMany(MapDraw.AxesOf).ToHashSet();
            okD &= open11.ToHashSet().SetEquals(want11);
            var seenAxes = new Dictionary<string, int>[3] { new(), new(), new() };
            int stagesN = 0, cardsBad = 0, changedAfter = 0;
            bool Fine(IReadOnlyList<string> cards, IReadOnlyCollection<string> open)
                => cards.Count == 3 && cards.Distinct().Count() == 3 && cards.Where(InvestRule.IsAxis).All(open.Contains)
                   && cards.Count(InvestRule.IsAxis) >= (open.Count > 0 ? 1 : 0) && cards.Any(x => Doctrine.Find(x) is not null);
            for (int c = 0; c < 300; c++)
            {
                // 1-1 は 1 章の口で（呼んだ後に札を読み直しても同じ 3 枚か）
                Chapter1.Begin(c, rule);
                var cards = Chapter1.InvestCards.ToList();
                stagesN++;
                if (!Fine(cards, Chapter1.OpenAxes!)) cardsBad++;
                foreach (string a in cards.Where(InvestRule.IsAxis)) seenAxes[0][a] = seenAxes[0].GetValueOrDefault(a) + 1;
                Chapter1.AutoInvest();
                Map11State st = Map11Session.State!;
                st.GrantGoldForCheck(100);
                for (int k = 0; k < 4; k++) st.Recruit(0);
                if (!Chapter1.InvestCards.SequenceEqual(cards)) changedAfter++;
                // 1-2 ／ 1-3 は持ち越しを作って入る（戦わずに次へ・Chapter1 と同じ式で札を引く——1 章の口の 1-2 ／ 1-3 は (e) と §6 の器具の通しで通る）
                Map11State.CarryPack pack = st.TakeCarry();
                for (int i = 1; i < 3; i++)
                {
                    var next = new Map11State(Chapter1.StageSeed(c, i, 0), ch[i].Time, ch[i].Portal, draft: true, stage: ch[i]);
                    next.ApplyCarry(pack);
                    var open = Chapter1.OpenAxesOf(next)!;
                    var held = pack.Invest!.Where(x => Doctrine.Find(x.Axis) is not null).Select(x => x.Axis).ToHashSet();
                    var cards2 = ch[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0), held, open);
                    stagesN++;
                    if (!Fine(cards2, open)) cardsBad++;
                    foreach (string a in cards2.Where(InvestRule.IsAxis)) seenAxes[i][a] = seenAxes[i].GetValueOrDefault(a) + 1;
                    next.Invest(cards2[InvestRule.BotPick(next, cards2)]);
                    next.GrantGoldForCheck(100);
                    for (int k = 0; k < 5; k++) next.Recruit(0);
                    pack = next.TakeCarry();
                }
            }
            okD &= cardsBad == 0 && changedAfter == 0 && seenAxes[0].Keys.All(want11.Contains);
            // 出せる軸が無いとき: 軸の枠も盤・金の札（持っている札は出さない・尽きれば枚数が減る）
            var none = ch[1].Invest.CardsFor(123, new HashSet<string>(), Array.Empty<string>());
            var none5 = ch[1].Invest.CardsFor(123, Doctrine.All.Take(5).Select(d => d.Name).ToHashSet(), Array.Empty<string>());
            bool noneOk = none.Count == 3 && none.All(x => Doctrine.Find(x) is not null) && none5.Count == 1;
            // 10 軸すべてを渡せば第468期と同じ札
            int diff468 = 0;
            for (int c = 0; c < 1000; c++)
                for (int i = 0; i < 3; i++)
                {
                    var held = Doctrine.All.Select(d => d.Name).Where((n, k) => (c + k) % 7 < i).Take(i).ToHashSet();
                    if (!ch[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0), held, UnitTags.Axes).SequenceEqual(ch[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0), held))) diff468++;
                }
            okD &= noneOk && diff468 == 0;
            write($"  (d) 1-1 の出せる軸 [{string.Join("・", open11)}]（1-1 の表の駒の軸 [{string.Join("・", UnitTags.Axes.Where(want11.Contains))}]）・札が外れた {cardsBad} ／ {stagesN}・呼んだ後に札が変わった {changedAfter}"
                  + $"・出せる軸が無いとき [{string.Join("・", none)}]（盤・金の札を 5 枚持っていれば [{string.Join("・", none5)}]）・10 軸すべてなら第468期と同じ札（違い {diff468} ／ 3000）");
            for (int i = 0; i < 3; i++)
                write($"      {ch[i].Id} の札に出た軸: {string.Join("・", UnitTags.Axes.Where(a => seenAxes[i].ContainsKey(a)).Select(a => $"{a} {seenAxes[i][a]}"))}");
            Req(okD, "(d) 軸の札は、呼べる駒（まだ持っていないもの）が 1 体以上いる軸だけ（1-1 は 1-1 の表の駒の軸から・呼んだ後も同じ 3 枚）。出せる軸が無ければ軸の枠も盤・金の札");
        }

        // ---- (e) 持ち越し・やり直し・☆・強化・盤と金の札は第468期のまま ----
        {
            var c468 = Chapter1.StagesFor(ChapterRule.V468);
            bool same = true;
            var parts = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                MapStage a = ch[i], b = c468[i];
                var checks = new (string Name, bool Ok)[]
                {
                    ("倍率", a.FoeScale == b.FoeScale), ("☆", ReferenceEquals(a.Stars, b.Stars)), ("投資", ReferenceEquals(a.Invest, b.Invest)), ("金", Equals(a.Gold, b.Gold)),
                    ("手札", Equals(a.Arrival, b.Arrival)), ("隊の枠", a.SquadCap == b.SquadCap), ("レリック", Equals(a.Relic, b.Relic)),
                    ("勇者", a.Hero.On == b.Hero.On && ReferenceEquals(a.Hero.Unit, b.Hero.Unit)), ("本陣", Equals(a.Siege, b.Siege)), ("盤", a.Board == b.Board),
                    ("表", a.Roster.On && !b.Roster.On), ("配り", !a.Deal.On && (i == 0) == b.Deal.On),
                };
                bool s = checks.All(x => x.Ok);
                same &= s;
                parts.Add($"{a.Id} {s}{(s ? "" : $"（{string.Join("・", checks.Where(x => !x.Ok).Select(x => x.Name))}）")}");
            }
            // 1 章の口で 1-1 に勝って 1-2 へ ・やり直し ・☆ ・強化
            string detail = "1-1 に勝つ 1 章が見つからない";
            bool flowOk = false;
            for (int c = 0; c < 100 && !flowOk; c++)
            {
                Chapter1.Begin(c, rule);
                Chapter1.AutoInvest();
                Map11State st = Map11Session.State!;
                Play(st, Split);
                Chapter1.Settle(st, new Dictionary<(string, TraitId), int>(), Array.Empty<Map11Session.Line>());
                if (!st.Won || !Chapter1.Next()) continue;
                Map11State s2 = Map11Session.State!;
                Map11State.CarryPack entry = Chapter1.Records[1].Entry!;
                string inText = PlayLog.PackText(s2.TakeCarry());
                bool carried = s2.CarriedIn && inText == PlayLog.PackText(entry with { Gold = entry.Gold + s2.Stage.Gold.Start }) && s2.Gold == entry.Gold + 8;
                // 強化（☆1 → ☆2・値段は第468期の式）
                s2.GrantGoldForCheck(50);
                UnitState? u = s2.Bench.FirstOrDefault(x => s2.CanEnhance(x)) ?? s2.Squads.SelectMany(q => q.Units ?? new List<UnitState>()).FirstOrDefault(x => s2.CanEnhance(x));
                StarState before = u is null ? default : s2.StarOf(u.Def.Id);
                int gold0 = s2.Gold;
                bool enh = u is not null && s2.EnhancePrice == c468[1].Stars.EnhancePrice && s2.Enhance(u) is not null && s2.Gold == gold0 - c468[1].Stars.EnhancePrice
                           && !s2.StarOf(u.Def.Id).Equals(before);
                Chapter1.Retry();
                Map11State s3 = Map11Session.State!;
                bool retry = s3.CarriedIn && PlayLog.PackText(s3.TakeCarry()) == inText && s3.Seed != s2.Seed && Chapter1.Records[1].Retries == 1;
                flowOk = carried && enh && retry;
                detail = $"1 章の seed {c}: 1-1 に勝って 1-2 へ・持ち越し {carried}（駒 {entry.Units}・金 {entry.Gold} ＋ 8）・強化 {enh}（{u?.Def.Name} {before} → {(u is null ? "" : s2.StarOf(u.Def.Id).ToString())}・値段 {s2.EnhancePrice}）・やり直すと入った時点へ {retry}";
            }
            write($"  (e) 第468期とステージの規則が同じ（敵の倍率 ・☆ ・投資と盤 ／ 金の札 ・金 ・手札 ・隊の枠 ・レリック ・勇者 ・本陣・違うのは加入の表と 1-1 の配りだけ）: {string.Join("・", parts)}");
            write($"      {detail}");
            Req(same && flowOk, "(e) 持ち越し・やり直し・☆・強化・盤と金の札は第468期のまま");
        }

        // ---- (f) 単発のステージ・第460〜468期の 1 章は今のまま ----
        {
            bool okF = MapStage.All.All(s => !s.Roster.On) && ChapterRule.Versions.Where(v => v.Key != "V469").All(v => Chapter1.StagesFor(v.Rule).All(s => !s.Roster.On));
            // 表の無い盤の母数は 52 枚・始めの手札は配りの規則のまま
            var s468 = Chapter1.StagesFor(ChapterRule.V468)[0];
            var st468 = new Map11State(77, s468.Time, s468.Portal, draft: true, stage: s468);
            bool pool52 = RosterRule.Off.PoolFor("1-1").Count == 52 && st468.RosterLeft.Count == 0 && st468.OpeningAxis is not null && st468.Bench.Count == 6;
            bool single = new[] { MapStage.Stage11, MapStage.Stage12, MapStage.Stage13 }.All(s => new Map11State(5, s.Time, s.Portal, draft: true, stage: s).RosterLeft.Count == 0);
            okF &= pool52 && single;
            write($"  (f) 単発の 1-1 ／ 1-2 ／ 1-3 と第460〜468期の 1 章に加入の表なし {okF}・第468期の 1-1 は 52 枚から配る（軸 {st468.OpeningAxis}・{Names(st468.Bench.Select(u => u.Def))}）・母数 52 {pool52}");
            write("      （単発の頭なしの門・画面の門・第460〜468期の 1 章の門・`compare` ／ `docs/` が 0 行かは回帰で見る）");
            Req(okF, "(f) 単発のステージ・第460〜468期の 1 章は今のまま");
        }
        Chapter1.End();

        int crashed = 0;
        int outsideSeen = 0;
        void Crash(string what, Exception ex) { crashed++; if (crashed <= 3) write($"  × {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        if (bots)
        {
            try { outsideSeen = BotTables(write, Crash); }
            catch (Exception ex) { Crash("§6 器具", ex); }
            Req(outsideSeen == 0, $"(b) 器具の 1 章の通しで、表の外の駒が手持ちに入った回数 {outsideSeen}（第469期の通しすべて）");
        }
        Req(crashed == 0, $"器具（{(bots ? $"1 章の通し × {Seeds} seed" : "なし")}）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }

    /// <summary>1 章の通し 1 つ（器具の方針・負ければ同じステージをやり直す）。</summary>
    private sealed class Run1
    {
        public bool Through;
        public int Stuck = -1;
        public int[] Retries = new int[3];
        public int[] Axes4 = new int[3];          // 各ステージの始め（入った時点）に 4 枚以上そろった軸の数
        public int[] LeftEnd = { -1, -1, -1 };    // 各ステージの終わり（勝った配り）の「呼べる駒 あと n 体」
        public int Deals11, Full11;              // 1-1 の配りの数 ・6 体全部を呼んだ配り
        public int Outside;                      // 表の外の駒が手持ちに入った回数
        public int Earned, Spent;
    }

    private static int Axes4(Map11State st)
        => UnitTags.Axes.Count(a => st.OwnedAxisCount(a) >= 4);

    private static Run1 Chapter(ChapterRule rule, int c, Policy[] pol)
    {
        var stages = Chapter1.StagesFor(rule);
        var res = new Run1();
        Map11State.CarryPack? pack = null;
        var allowed = rule.Roster is { On: true } r ? r.StartFor("1-1")!.Concat(r.PoolFor("1-3")).Select(d => d.Id).ToHashSet() : null;
        for (int i = 0; i < 3; i++)
        {
            bool won = false;
            IReadOnlyList<string>? cards = null;
            for (int a = 0; a <= RetryCap && !won; a++)
            {
                MapStage stage = stages[i];
                var st = new Map11State(Chapter1.StageSeed(c, i, a), stage.Time, stage.Portal, draft: true, stage: stage);
                if (pack is not null) st.ApplyCarry(pack);
                if (a == 0) res.Axes4[i] = Axes4(st);
                // 札は入った時点で決まる（Chapter1 と同じ式・やり直しでも同じ 3 枚）
                cards ??= stage.Invest.CardsFor(Chapter1.StageSeed(c, i, 0),
                    (pack?.Invest ?? Array.Empty<(string, string)>()).Where(x => x.Item1 != stage.Id && Doctrine.Find(x.Item2) is not null).Select(x => x.Item2).ToHashSet(),
                    Chapter1.OpenAxesOf(st));
                if (cards.Count > 0) st.Invest(cards[InvestRule.BotPick(st, cards)]);
                Play(st, pol[i]);
                res.Earned += st.GoldLog.Where(e => e.Amount > 0).Sum(e => e.Amount);
                res.Spent -= st.GoldLog.Where(e => e.Amount < 0).Sum(e => e.Amount);
                if (allowed is not null) res.Outside += Owned(st).Count(id => !allowed.Contains(id));
                if (i == 0) { res.Deals11++; if (stage.Roster.On ? st.RosterLeft.Count == 0 : false) res.Full11++; }
                won = st.Won;
                if (won) { pack = st.TakeCarry(); res.LeftEnd[i] = st.RosterLeft.Count; continue; }
                if (a < RetryCap) res.Retries[i]++;
            }
            if (!won) { res.Stuck = i; return res; }
        }
        res.Through = true;
        return res;
    }

    /// <summary>§6: 1 章の通し（第469期 ／ 第468期 × 方針 2 つ・報告だけ）。表の外の駒が手持ちに入った回数（第469期）を返す。</summary>
    private static int BotTables(Action<string> write, Action<string, Exception> crash)
    {
        var p2 = new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) };
        int outside = 0;
        write("");
        write($"  §6 1 章の通し（1 章の seed 0〜{Seeds - 1}・負ければ同じステージをやり直す・1 ステージ {RetryCap} 回まで・買い方は第465期と同じ・札は「手持ちの軸がいちばん多い軸、無ければ札の左から」）");
        write("  | 1 章 | 方針 | 通せた | やり直し 1-1 ／ 1-2 ／ 1-3 | 止まったステージ | 始めに 4 枚以上の軸 1-1 ／ 1-2 ／ 1-3 | 1-1 で呼び切った配り | 終わりの「あと n 体」1-1 ／ 1-2 ／ 1-3 | 金 稼いだ ／ 使った（1 章あたり） |");
        write("  |---|---|--:|---|---|---|--:|---|---|");
        foreach (ChapterRule rule in new[] { ChapterRule.V469, ChapterRule.V468 })
            foreach (var (cname, pol) in p2)
            {
                string rkey = rule.Roster is { On: true } ? "**第469期（加入の表）**" : "第468期（52 枚から）";
                var runs = new List<Run1>();
                for (int c = 0; c < Seeds; c++)
                {
                    try { runs.Add(Chapter(rule, c, pol)); }
                    catch (Exception ex) { crash($"{rkey} {cname} seed {c}", ex); }
                }
                outside += runs.Sum(r => r.Outside);
                string Avg(Func<Run1, int> f, Func<Run1, bool> where) { var xs = runs.Where(where).Select(f).ToList(); return xs.Count == 0 ? "—" : $"{xs.Average():F2}"; }
                string axes = string.Join(" ／ ", Enumerable.Range(0, 3).Select(i => Avg(r => r.Axes4[i], r => r.Stuck < 0 || r.Stuck >= i)));
                string left = rule.Roster is { On: true } ? string.Join(" ／ ", Enumerable.Range(0, 3).Select(i => Avg(r => r.LeftEnd[i], r => r.LeftEnd[i] >= 0))) : "—";
                string full = rule.Roster is { On: true } ? $"{100.0 * runs.Sum(r => r.Full11) / Math.Max(1, runs.Sum(r => r.Deals11)):F0}%（{runs.Sum(r => r.Full11)} ／ {runs.Sum(r => r.Deals11)}）" : "—";
                write($"  | {rkey} | {cname} | {runs.Count(r => r.Through)}% | {runs.Average(r => r.Retries[0]):F2} ／ {runs.Average(r => r.Retries[1]):F2} ／ {runs.Average(r => r.Retries[2]):F2}"
                      + $" | 1-1 {runs.Count(r => r.Stuck == 0)}・1-2 {runs.Count(r => r.Stuck == 1)}・1-3 {runs.Count(r => r.Stuck == 2)} | {axes} | {full} | {left} | {runs.Average(r => r.Earned):F1} ／ {runs.Average(r => r.Spent):F1} |");
            }
        write("  （「始めに 4 枚以上の軸」はそのステージに入った時点（呼ぶ前）の手持ちで数える・そのステージに入った通しだけの平均。1-1 で呼び切った ＝ 1-1 の配りの終わりに 1-1 で呼べる 6 体を全部持っていた割合（やり直しの配りも数える））");
        return outside;
    }
}
