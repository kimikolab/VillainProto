using BattleCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================================
// 盤の札・金の札の門（第468期 §4・`--chapter1-doctrine-smoke`・指示書 design/PHASE468_DOCTRINE_SPEC.md）
//
// **画面を1つも作らない。** `Chapter1`（`ChapterRule.V468`）／ `InvestRule` ／ `Doctrine` ／ `Map11State` と器具の方針（`Map12ChangeSmoke.Greedy`）・
// 敵の重さの表（`FoeWeight`）だけを回す。
//
//   (a) 3 枚が「軸 1 ＋ 盤か金 1 ＋ どれでも 1」・同じ seed なら同じ・やり直しでも同じ
//   (b) 盤・金の札は選んだ後の札に出ない。軸は重なる
//   (c) 前のめり・後詰め・要: 開戦時の席で最大HP・攻撃が表のとおり（☆の後に掛ける）。戦闘中に動かされても変わらない。戦闘の後は元の定義
//   (d) 略奪・蓄え・倹約: 金の出入りが表のとおり（抜いた金・呼ぶ・強化・売る・利子）
//   (e) 単発のステージ・第460〜467期の 1 章は今のまま
//   (f) 道具: `--foe-weight` に「守り → 勇者」の通しが出る・帯の値が直っている
//       （「写しから始める」のボタンから写しの一覧が開くかは**画面の口で見る**——絵の口 `--map11-capture=<png>,snap-button --map-chapter=N` の
//        `MAP11_CAPTURE_SNAP_BUTTON ok=`）
//
// 加えて（報告だけ・線は置かない）:
//   §3 1 章の通し × 100 seed（第468期 ／ 第467期 × 方針 2 つ・器具の選び方は「手持ちの軸、無ければ札の左から」）
//      ・1-1 で札を固定した通し（6 枚の札 ＋ 対照「1-1 は札の左の軸」）の稼ぎ・使った金・利子・通せた率
//      ・盤の札 × 手で写した写しの隊（ポンの隊）× そのステージの敵部隊の単発の戦闘（予想 2「隊の前後どちらに主力がいるかで得か損かが分かれる」）
// =====================================================================================

public static class Chapter1DoctrineSmoke
{
    public const int Seeds = 100;
    public const int RetryCap = 20;

    private readonly record struct Policy(string Name, bool Guard, bool Split, Map12ChangeSmoke.HeroPlan Plan);
    private static readonly Policy Keep1 = new("1 隊残す", true, false, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Split = new("分担＋戻り番", true, true, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Flee = new("逃げる", true, true, Map12ChangeSmoke.HeroPlan.Flee);

    private static void Play(Map11State st, Policy p)
        => Map12ChangeSmoke.Greedy(st.Seed, st.Stage, p.Guard, p.Split, false, state: st, siegeRest: true, heroPlan: p.Plan);

    private static int Mul(int x, int pct) => x == 0 || pct == 0 ? x : MapFoeScale.Mul(x, 100 + pct);

    /// <summary>持ち越しの投資の記録のうち、そのステージより前の盤・金の札。</summary>
    private static HashSet<string> HeldOf(IReadOnlyList<(string Stage, string Axis)>? log, string stage)
        => (log ?? Array.Empty<(string, string)>()).Where(x => x.Stage != stage && Doctrine.Find(x.Axis) is not null).Select(x => x.Axis).ToHashSet();

    public static bool Run(Action<string> write, string demoDir, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V468;
        var ch = Chapter1.StagesFor(rule);
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}・投資 {ch[0].Invest.Name}");
        write($"  札: {string.Join(" ／ ", Doctrine.All.Select(d => $"{d.Name}（{d.KindName}: ＋{d.Plus} ／ －{d.Minus}）"))}");

        // ---- (a) 札の組 ----
        {
            bool okA = true;
            int stagesN = 0, twoNon = 0;
            var seen = new Dictionary<string, int>();
            for (int c = 0; c < 1000; c++)
                for (int i = 0; i < 3; i++)
                {
                    // 前のステージまでに持った札を 0 〜 2 枚（1 章の seed で決める）——持った札は出ない（(b) でも見る）
                    var held = Doctrine.All.Select(d => d.Name).Where((n, k) => (c + k) % 7 < i).Take(i).ToHashSet();
                    var cards = ch[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0), held);
                    int axes = cards.Count(InvestRule.IsAxis), docs = cards.Count(x => Doctrine.Find(x) is not null);
                    okA &= cards.Count == 3 && cards.Distinct().Count() == 3 && axes >= 1 && docs >= 1 && axes + docs == 3;
                    okA &= cards.SequenceEqual(ch[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0), held));
                    stagesN++;
                    if (docs == 2) twoNon++;
                    foreach (string x in cards) seen[x] = seen.GetValueOrDefault(x) + 1;
                }
            Chapter1.Begin(4681, rule);
            var first = Chapter1.InvestCards.ToList();
            bool pending0 = Chapter1.InvestPending;
            Chapter1.PickInvest(first.FindIndex(x => Doctrine.Find(x) is not null));
            bool picked = !Chapter1.InvestPending && Map11Session.State!.InvestLog.Count == 1 && Map11Session.State.Doctrines.Count == 1;
            var after = Chapter1.InvestCards.ToList();
            Chapter1.Retry();
            var retry = Chapter1.InvestCards.ToList();
            bool pendingR = Chapter1.InvestPending && Map11Session.State!.InvestLog.Count == 0 && Map11Session.State.Doctrines.Count == 0;
            Chapter1.Begin(4681, rule);
            var again = Chapter1.InvestCards.ToList();
            Chapter1.Begin(4682, rule);
            var other = Chapter1.InvestCards.ToList();
            okA &= pending0 && picked && pendingR && first.SequenceEqual(after) && first.SequenceEqual(retry) && first.SequenceEqual(again) && first.Count == 3;
            write($"  (a) 1 章の seed 4681 の 1-1: [{string.Join("・", first)}]・選んだ後 [{string.Join("・", after)}]・やり直し [{string.Join("・", retry)}]・始め直し [{string.Join("・", again)}]・seed 4682 [{string.Join("・", other)}]"
                  + $"・選ぶ前は選んでいない {pending0}・盤 ／ 金の札を選んだ {picked}・やり直すと選び直し {pendingR}");
            write($"      1000 章 × 3 ステージ: 3 枚のうち 2 枚が軸以外 {100.0 * twoNon / stagesN:F1}%（見込み 5 ／ 14 ≒ 35.7%・持った札があると下がる）・札に出た割合 "
                  + string.Join("・", UnitTags.Axes.Concat(Doctrine.All.Select(d => d.Name)).Select(x => $"{x} {100.0 * seen.GetValueOrDefault(x) / stagesN:F1}%")));
            Req(okA, "(a) 3 枚が「軸 1 ＋ 盤か金 1 ＋ どれでも 1」（重複なし）・同じ seed なら同じ・やり直しでも同じ（やり直すと選び直す）");
        }

        // ---- (b) 盤・金の札は 2 度出ない・軸は重なる ----
        {
            bool okB = true;
            int shownHeld = 0;
            foreach (Doctrine d in Doctrine.All)
                for (int c = 0; c < 1000; c++)
                    for (int i = 1; i < 3; i++)
                        if (ch[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0), new HashSet<string> { d.Name }).Contains(d.Name)) shownHeld++;
            okB &= shownHeld == 0;
            // 1 章の口で: 1-1 で盤 ／ 金の札を選び、器具で 1-1 に勝って 1-2 へ——持ち越し・札から外れる・同じ札は選べない
            string detail = "1-1 に勝つ 1 章が見つからない";
            bool flowOk = false;
            for (int c = 0; c < 100 && !flowOk; c++)
            {
                Chapter1.Begin(c, rule);
                var cards = Chapter1.InvestCards.ToList();
                int at = cards.FindIndex(x => Doctrine.Find(x) is not null);
                string doc = cards[at];
                Chapter1.PickInvest(at);
                Map11State st = Map11Session.State!;
                Play(st, Split);
                Chapter1.Settle(st, new Dictionary<(string, TraitId), int>(), Array.Empty<Map11Session.Line>());
                if (!st.Won || !Chapter1.Next()) continue;
                Map11State s2 = Map11Session.State!;
                var cards2 = Chapter1.InvestCards.ToList();
                bool carried = s2.Doctrines.Any(x => x.Name == doc) && s2.InvestText.Contains(doc);
                bool refused = !s2.Invest(doc);
                flowOk = carried && !cards2.Contains(doc) && refused && s2.InvestPending;
                detail = $"1 章の seed {c}: 1-1 で {doc} → 1-2 の札 [{string.Join("・", cards2)}]・持ち越し {carried}（{s2.InvestText}）・同じ札は選べない {refused}";
            }
            okB &= flowOk;
            // 軸は重なる（1-1 燃焼・1-2 燃焼・1-2 で持っている盤の札があっても軸の割合は軸の回数だけで決まる）
            var s13 = new Map11State(4683, ch[2].Time, ch[2].Portal, draft: true, stage: ch[2]);
            s13.ApplyCarry(new Map11State.CarryPack(new List<Map11State.CarryUnit>(), new List<IReadOnlyList<Map11State.CarryUnit>>(), 2, 0, new List<TraitId>(),
                new Dictionary<string, StarState>(), new[] { ("1-1", "燃焼"), ("1-2", "燃焼"), ("1-2", "略奪") }));
            string text13 = s13.InvestText;
            bool stack = s13.InvestCount("燃焼") == 2 && text13 == "投資: 燃焼 ×2・略奪" && s13.Doctrines.Count == 1;
            bool burnAgain = s13.Invest("燃焼") && s13.InvestCount("燃焼") == 3;
            okB &= stack && burnAgain;
            write($"  (b) 盤 ／ 金の札を持っていると、その札は以後の札に出ない（6 枚 × 1000 章 × 1-2 ／ 1-3 で出た回数 {shownHeld}）");
            write($"      {detail}");
            write($"      1-3 に「1-1 燃焼・1-2 燃焼＋略奪」を持ち越すと {text13}（1-3 で燃焼を選ぶと ×3: {burnAgain}・割合 {s13.Stage.Invest.Percent(3)}%）");
            Req(okB, "(b) 盤・金の札は選んだ後の札に出ない（同じ札は選べない）。軸は重なる");
        }

        // ---- (c) 盤の札 ----
        {
            bool okC = true;
            // 数値の表（☆3 の素の定義・列ごと）
            UnitDef sample = UnitCatalog.All.First(d => d.MaxHp >= 80 && d.Attack >= 8);
            foreach (Doctrine d in Doctrine.All.Where(x => x.Kind == DoctrineKind.Board))
            {
                var cells = new List<string>();
                foreach (Row row in new[] { Row.Front, Row.Mid, Row.Back })
                {
                    var (h, a) = d.Of(row);
                    UnitDef x = DoctrineDefs.Of(sample, h, a);
                    bool want = x.MaxHp == Mul(sample.MaxHp, h) && x.Attack == Mul(sample.Attack, a) && (h == 0 && a == 0 ? ReferenceEquals(x, sample) : DoctrineDefs.IsCopy(x));
                    okC &= want;
                    cells.Add($"{(row == Row.Front ? "前列" : row == Row.Mid ? "中央" : "後列")} HP {sample.MaxHp}→{x.MaxHp}・攻 {sample.Attack}→{x.Attack}");
                }
                write($"  (c) {d.Name}（{sample.Name}）: {string.Join(" ／ ", cells)}");
            }
            // 全駒 × 全札 × 列: 表のとおり（☆1 ／ ☆2 の写しの後にも掛ける）
            int checkedN = 0;
            StarRule stars = ch[0].Stars;
            foreach (UnitDef u in UnitCatalog.All)
                foreach (int star in new[] { 1, 2, 3 })
                {
                    UnitDef sd = stars.Def(u, star);
                    foreach (Doctrine d in Doctrine.All.Where(x => x.Kind == DoctrineKind.Board))
                        foreach (Row row in new[] { Row.Front, Row.Mid, Row.Back })
                        {
                            var (h, a) = d.Of(row);
                            UnitDef x = DoctrineDefs.Of(sd, h, a);
                            okC &= x.MaxHp == Mul(sd.MaxHp, h) && x.Attack == Mul(sd.Attack, a) && x.Speed == sd.Speed && x.Id == sd.Id
                                   && ReferenceEquals(DoctrineDefs.BaseOf(x), sd) && x.ReapLineCapPermil == sd.ReapLineCapPermil;
                            checkedN++;
                        }
                }
            // 重ねて持つ: 前のめり ＋ 要 は足し合わせ（前列 HP +15%・中央 +30% ／ +30%・後列 HP −10% ・攻 −15%）
            var both = new Map11State(4684, ch[1].Time, ch[1].Portal, draft: true, stage: ch[1]);
            both.ApplyCarry(new Map11State.CarryPack(new List<Map11State.CarryUnit>(), new List<IReadOnlyList<Map11State.CarryUnit>>(), 2, 0, new List<TraitId>(),
                new Dictionary<string, StarState>(), new[] { ("1-1", "前のめり") }));
            both.Invest("要");
            var sp = new[] { 0, 2, 3 }.Select(both.SeatPercent).ToList();
            bool sum = sp[0] == (15, 15) && sp[1] == (30, 30) && sp[2] == (-10, -15);
            okC &= sum;
            write($"      全駒 {UnitCatalog.All.Count} 体 × ☆1〜3 × 盤の札 3 枚 × 列 3: {checkedN} 組が表のとおり（☆の写しの後に掛ける・速さ ／ 札 ／ 刈り取りの線は写しのまま）・前のめり＋要 の席 前1 {sp[0]} ／ 中央 {sp[1]} ／ 後1 {sp[2]}（足し合わせ）");

            // 本物の通しで: 開戦の直前に差し、戦闘中は動かされても変えず、後始末で元の定義へ
            int battles = 0, armedUnits = 0, wrong = 0, moved = 0, leftCopies = 0, centerHit = 0, fullN = 0, fullBack = 0;
            var lines = new List<string>();
            try
            {
                Map12ChangeSmoke.FightHook = (st, squad, node, intercept) =>
                {
                    // 差す前の HP ／ 最大HP（キャラごと・満タンの駒が戦闘で 1 も削られなければ、後始末の後も満タンの元の値に戻るかを見る）
                    var pre = (st.Squads[squad].Units ?? new List<UnitState>()).Where(u => u.IsAlive && !u.Def.Traits.Contains(TraitId.Ephemeral))
                        .GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => (g.First().Hp, g.First().MaxHp));
                    var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
                    if (prep is not { } q) return;
                    battles++;
                    var start = new Dictionary<UnitState, (int Slot, UnitDef Def, int Hp, int Max)>(ReferenceEqualityComparer.Instance);
                    foreach (UnitState u in q.Players.Where(x => x.IsAlive && !x.Def.Traits.Contains(TraitId.Ephemeral)))
                    {
                        UnitDef b = DoctrineDefs.BaseOf(u.Def);
                        var (h, a) = st.SeatPercent(u.Slot);
                        bool starOk = ReferenceEquals(b, st.Stage.Stars.Def(st.Stage.Stars.BaseOf(b), st.StarOf(b.Id).Star));
                        bool good = u.Def.MaxHp == Mul(b.MaxHp, h) && u.Def.Attack == Mul(b.Attack, a) && starOk && (h == 0 && a == 0 ? ReferenceEquals(u.Def, b) : DoctrineDefs.IsCopy(u.Def));
                        if (!good) { wrong++; if (lines.Count < 3) lines.Add($"{u.Def.Name} 席 {u.Slot} HP {u.Def.MaxHp}（元 {b.MaxHp}・{h}%）攻 {u.Def.Attack}（元 {b.Attack}・{a}%）☆の写し {starOk}"); }
                        if (DoctrineDefs.IsCopy(u.Def)) armedUnits++;
                        if (FormationRules.RowOf(u.Slot) == Row.Mid && DoctrineDefs.IsCopy(u.Def)) centerHit++;
                        start[u] = (u.Slot, u.Def, u.Hp, u.MaxHp);
                    }
                    BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
                    var full = new List<string>();
                    foreach (var (u, s0) in start)
                    {
                        if (u.Slot != s0.Slot) { moved++; if (!ReferenceEquals(u.Def, s0.Def)) wrong++; }
                        if (DoctrineDefs.IsCopy(u.Def) && u.Hp == s0.Hp && u.MaxHp == s0.Max && pre.TryGetValue(u.Def.Id, out var p0) && p0.Hp == p0.MaxHp) full.Add(u.Def.Id);
                    }
                    st.Resolve(squad, q.Node, r.PlayerWon, intercept);
                    var everyone = st.Squads.Where(x => x.Units is not null).SelectMany(x => x.Units!).Concat(st.Bench).Concat(st.FallenUnits).ToList();
                    leftCopies += everyone.Count(u => DoctrineDefs.IsCopy(u.Def));
                    foreach (string id in full)
                    {
                        fullN++;
                        UnitState? back = everyone.FirstOrDefault(u => u.Def.Id == id && u.IsAlive);
                        if (back is not null && back.Hp == pre[id].Hp && back.MaxHp == pre[id].MaxHp && !DoctrineDefs.IsCopy(back.Def)) fullBack++;
                        else if (lines.Count < 3) lines.Add($"戻らない {id}: 前 {pre[id].Hp}/{pre[id].MaxHp} → 後 {back?.Hp}/{back?.MaxHp}（定義 {back?.Def.MaxHp}）");
                    }
                };
                foreach (string[] picks in new[] { new[] { "前のめり" }, new[] { "後詰め" }, new[] { "要" }, new[] { "前のめり", "要" } })
                    for (int c = 0; c < 6; c++)
                    {
                        Chapter1.Begin(4690 + c, rule);
                        Map11State st = Map11Session.State!;
                        st.Invest(picks[0]);
                        Play(st, Split);
                        Chapter1.Settle(st, new Dictionary<(string, TraitId), int>(), Array.Empty<Map11Session.Line>());
                        if (picks.Length < 2 || !st.Won || !Chapter1.Next()) continue;
                        Map11State s2 = Map11Session.State!;
                        s2.Invest(picks[1]);
                        Play(s2, Split);
                    }
            }
            finally { Map12ChangeSmoke.FightHook = null; }
            okC &= battles > 0 && armedUnits > 0 && wrong == 0 && leftCopies == 0 && moved > 0 && centerHit > 0 && fullBack == fullN;
            write($"      器具の通し（前のめり ／ 後詰め ／ 要 ／ 前のめり＋要 × 1 章の seed 6 つ・1-1、重ねは 1-2 まで）: 戦闘 {battles}・数値を差した駒 {armedUnits}（中央 {centerHit}）・表と違う {wrong}"
                  + $"・戦闘中に席が動いた駒 {moved}（数値は開戦時のまま）・後始末の後に写しのまま残った駒 {leftCopies}・満タンで 1 も削られなかった駒 {fullN} のうち元の値に戻った {fullBack}"
                  + (lines.Count > 0 ? "・例 " + string.Join(" ／ ", lines) : ""));
            Req(okC, "(c) 前のめり・後詰め・要: 開戦時の席で最大HP・攻撃が表のとおり（☆の後に掛ける・重ねれば足し合わせ）。戦闘中に動かされても変わらない。戦闘の後は元の定義");
        }

        // ---- (d) 金の札 ----
        {
            bool okD = true;
            MapStage s12 = ch[1];
            Map11State Fresh(params string[] picks)
            {
                var st = new Map11State(4685, s12.Time, s12.Portal, draft: true, stage: s12);
                foreach (string p in picks) if (!st.Invest(p)) throw new InvalidOperationException($"{p} を選べない");
                return st;
            }
            Map11State b0 = Fresh();
            var nodes = b0.Nodes.SelectMany(r => r).ToList();
            var baseKill = nodes.Select(n => b0.KillGold(n)).ToList();
            string KillRow(Map11State st) => string.Join(",", nodes.Select(n => st.KillGold(n)).Distinct().OrderBy(x => x));
            foreach (Doctrine d in Doctrine.All.Where(x => x.Kind == DoctrineKind.Gold))
            {
                Map11State st = Fresh(d.Name);
                var kill = nodes.Select(n => st.KillGold(n)).ToList();
                bool killOk = kill.Zip(baseKill).All(p => p.First == (p.Second <= 0 ? p.Second : Math.Max(0, (d.LootPercent == 0 ? p.Second : (int)Math.Round(p.Second * (100 + d.LootPercent) / 100.0, MidpointRounding.AwayFromZero)) + d.LootFlat)));
                bool outOk = st.OutpostGold == (d.LootPercent == 0 && d.LootFlat == 0 ? s12.Gold.Outpost : Math.Max(0, (int)Math.Round(s12.Gold.Outpost * (100 + d.LootPercent) / 100.0, MidpointRounding.AwayFromZero) + d.LootFlat));
                bool recOk = st.RecruitPrice == Math.Max(1, s12.Gold.Recruit + d.RecruitDelta);
                bool enhOk = st.EnhancePrice == Math.Max(1, s12.Stars.EnhancePrice + d.EnhanceDelta);
                UnitState? benched = st.Bench.FirstOrDefault(u => u.IsAlive);
                bool sellOk = benched is null || st.SellPrice(benched) == (d.NoSell ? 0 : b0.SellPrice(b0.Bench.First(u => u.Def.Id == benched.Def.Id)));
                // 実際に払う ／ 入る: 呼ぶ・強化・売る・利子（金は絵の口と同じ口で足す）
                st.GrantGoldForCheck(25);
                string paid = "";
                int g0 = st.Gold;
                if (st.CanRecruit(0) && st.Recruit(0)) { paid += $"呼ぶ −{g0 - st.Gold}"; recOk &= g0 - st.Gold == st.RecruitPrice; }
                else paid += "呼ぶ（押せない）";
                UnitState? enh = st.Bench.FirstOrDefault(u => st.CanEnhance(u));
                if (enh is not null) { int g1 = st.Gold; st.Enhance(enh); paid += $"・強化 −{g1 - st.Gold}"; enhOk &= g1 - st.Gold == st.EnhancePrice; }
                else paid += "・強化（押せない）";
                int sb = st.Bench.FindIndex(u => u.IsAlive);
                if (sb >= 0 && st.CanSell(sb)) { int g2 = st.Gold; int want = st.SellPrice(st.Bench[sb]); st.Sell(sb); paid += $"・売る ＋{st.Gold - g2}"; sellOk &= st.Gold - g2 == want; }
                int g3 = st.Gold, logN = st.GoldLog.Count;
                st.AdvanceFoes();
                int interest = st.GoldLog.Skip(logN).Where(e => e.Reason == "蓄えの利子").Sum(e => e.Amount);
                bool intOk = interest == g3 / 10 * d.InterestPer10;
                paid += $"・作戦ターンの終わりの利子 ＋{interest}（手持ち {g3}）";
                okD &= killOk && outOk && recOk && enhOk && sellOk && intOk;
                write($"  (d) {d.Name}: 抜いた金 [{KillRow(st)}]（元 [{KillRow(b0)}]）{killOk}・中間拠点 ＋{st.OutpostGold}（元 ＋{s12.Gold.Outpost}）{outOk}・呼ぶ {st.RecruitPrice}（元 {s12.Gold.Recruit}）{recOk}"
                      + $"・強化 {st.EnhancePrice}（元 {s12.Stars.EnhancePrice}）{enhOk}・売る {(benched is null ? "—" : st.SellPrice(benched).ToString())} {sellOk}・利子 {intOk}");
                write($"      払った ／ 入った: {paid}");
            }
            // 重ねて持つ: 略奪 ＋ 倹約（呼ぶ 4 ＋1 −1 ＝ 4・抜いた金 6 → 9 → 7）
            var pt = new Map11State(4686, s12.Time, s12.Portal, draft: true, stage: s12);
            pt.ApplyCarry(new Map11State.CarryPack(new List<Map11State.CarryUnit>(), new List<IReadOnlyList<Map11State.CarryUnit>>(), 2, 0, new List<TraitId>(),
                new Dictionary<string, StarState>(), new[] { ("1-1", "略奪") }));
            pt.Invest("倹約");
            var bt = new Map11State(4686, s12.Time, s12.Portal, draft: true, stage: s12);
            var btNodes = bt.Nodes.SelectMany(r => r).ToList();
            var ptNodes = pt.Nodes.SelectMany(r => r).ToList();
            int six = btNodes.FindIndex(n => bt.KillGold(n) == 6);
            Map11State.Node? road = six >= 0 ? ptNodes[six] : null;
            bool stackOk = pt.RecruitPrice == s12.Gold.Recruit && pt.EnhancePrice == s12.Stars.EnhancePrice - 1 && road is not null && pt.KillGold(road) == 7;
            okD &= stackOk;
            write($"      略奪 ＋ 倹約: 呼ぶ {pt.RecruitPrice}・抜いた金 ＋6 の部隊 → ＋{(road is null ? "—" : pt.KillGold(road).ToString())}（×1.5 の後に −2）{stackOk}");
            Req(okD, "(d) 略奪・蓄え・倹約: 金の出入りが表のとおり（抜いた金・中間拠点・呼ぶ・強化・売る・利子・重ねて持てる）");
        }

        // ---- (e) 単発・第460〜467期 ----
        {
            bool okE = MapStage.All.All(s => !s.Invest.Doctrines);
            var parts = new List<string>();
            foreach (var (key, r) in ChapterRule.Versions)
            {
                bool docs = Chapter1.StagesFor(r).Any(s => s.Invest.Doctrines);
                okE &= docs == (key == "V468");
                parts.Add($"{key} {(docs ? "盤・金の札あり" : "なし")}");
            }
            // 第467期の札の式は今のまま（10 軸から重複なし・軸だけ）
            bool same467 = true;
            for (int c = 0; c < 300; c++)
            {
                var rng = new Random(unchecked(Chapter1.StageSeed(c, 1, 0) * 7919 + 467));
                string[] axes = UnitTags.Axes.ToArray();
                for (int i = axes.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (axes[i], axes[j]) = (axes[j], axes[i]); }
                same467 &= InvestRule.V467.CardsFor(Chapter1.StageSeed(c, 1, 0), new HashSet<string> { "略奪" }).SequenceEqual(axes.Take(3));
            }
            // 第467期の 1-2 に盤・金の札の記録を持ち越しても効かない（値段・席の数値・選べない）
            var c467 = Chapter1.StagesFor(ChapterRule.V467)[1];
            var s467 = new Map11State(4687, c467.Time, c467.Portal, draft: true, stage: c467);
            s467.ApplyCarry(new Map11State.CarryPack(new List<Map11State.CarryUnit>(), new List<IReadOnlyList<Map11State.CarryUnit>>(), 2, 0, new List<TraitId>(),
                new Dictionary<string, StarState>(), new[] { ("1-1", "略奪"), ("1-1", "前のめり") }));
            bool inert = s467.Doctrines.Count == 0 && s467.RecruitPrice == c467.Gold.Recruit && s467.EnhancePrice == c467.Stars.EnhancePrice
                         && s467.SeatPercent(0) == (0, 0) && s467.InterestNow == 0 && !s467.Invest("後詰め");
            var single = new Map11State(4687, MapStage.Stage12.Time, MapStage.Stage12.Portal, draft: true, stage: MapStage.Stage12);
            bool singleSame = !single.Invest("略奪") && single.RecruitPrice == MapStage.Stage12.Gold.Recruit && single.OutpostGold == MapStage.Stage12.Gold.Outpost;
            okE &= same467 && inert && singleSame;
            write($"  (e) 単発の 1-1 ／ 1-2 ／ 1-3 に盤・金の札なし・{string.Join("・", parts)}・第467期の札の式は今のまま {same467}"
                  + $"・第467期の 1-2 に盤 ／ 金の札の記録を持ち越しても効かず選べない {inert}・単発の 1-2 の値段は今のまま {singleSame}");
            write("      （単発の頭なしの門・画面の門・第460〜467期の 1 章の門・`compare` ／ `docs/` が 0 行かは回帰で見る）");
            Req(okE, "(e) 単発のステージ・第460〜467期の 1 章は今のまま");
        }

        // ---- (f) 道具 ----
        {
            bool okF = true;
            string repo = Path.GetFullPath(Path.Combine(demoDir, ".."));
            var bands = FoeWeight.Bands.Load(Path.Combine(repo, "design", "weights", "BANDS.md"));
            bool noCap = double.IsPositiveInfinity(bands.Foe["road"].HpHi) && double.IsPositiveInfinity(bands.Foe["horde"].HpHi)
                         && !double.IsPositiveInfinity(bands.Foe["outpost"].HpHi) && !double.IsPositiveInfinity(bands.Foe["garrison"].HpHi)
                         && FoeWeight.Verdict(bands.Foe["road"], 20, 20, 100.0) == "○" && FoeWeight.Verdict(bands.Foe["horde"], 20, 20, 100.0) == "○"
                         && FoeWeight.Verdict(bands.Foe["outpost"], 20, 20, 100.0) == "軽い";
            okF &= noCap;
            write($"  (f) 帯（{bands.Source}）: 道の部隊・湧き・予備隊 上限 {bands.Foe["road"].HpHi}・大群 上限 {bands.Foe["horde"].HpHi}（終わりの HP 100% で ○）・中間拠点 上限 {bands.Foe["outpost"].HpHi}（100% で軽い）{noCap}");
            string root = PlayLog.DefaultRoot(demoDir);
            var snaps = PlayLog.List(PlayLog.ManualDir(root));
            var squads = FoeWeight.SquadsOf(snaps, "1-2", root);
            if (squads.Count == 0) { okF = false; write("      × 手で写した写しに 1-2 の隊が無い（design/plays/manual）"); }
            else
            {
                ChapterRule r465 = ChapterRule.V465;
                var (cells, chains) = FoeWeight.Measure(squads, 1, new[] { r465.ScaleOf(1) }, new[] { 0 }, StarRule.V464, 4, true);
                var siege = chains.Where(x => x.Siege).ToList();
                var alone = chains.Where(x => !x.Siege).ToList();
                bool chainOk = siege.Count > 0 && siege.Count == alone.Count && siege.All(x => x.Banded && x.HeroKey == "hero_gate") && alone.All(x => !x.Banded);
                var table = FoeWeight.Table("1-2", cells, chains, bands, 4, "門の確かめ");
                bool printed = table.Any(l => l.Contains("守り → 勇者: ")) && table.Any(l => l.Contains("勇者だけ: ") && l.Contains("（参考）"));
                okF &= chainOk && printed;
                write($"      1-2 の勇者（門番）: 写し {siege.Count} 本で「守り → 勇者」の通し（帯を読む）と「勇者だけ」の通し（参考）が出る {chainOk}・表に出る {printed}・"
                      + string.Join(" ／ ", siege.Select(x => $"{x.Source.Split('/').Last()} 1 隊で {x.One}／4・2 隊まで {x.Two}／4（勇者だけ: {alone.First(a => a.Source == x.Source).One}／4）")));
                // 1-3（追う勇者）は今のまま（守り → 勇者の通しは出さない）
                var s13 = FoeWeight.SquadsOf(snaps, "1-3", root);
                if (s13.Count > 0)
                {
                    var (_, ch13) = FoeWeight.Measure(s13.Take(2).ToList(), 2, new[] { r465.ScaleOf(2) }, new[] { 0 }, StarRule.V464, 2, true);
                    bool chase = ch13.All(x => !x.Siege && x.Banded && x.HeroKey == "hero_chase");
                    okF &= chase;
                    write($"      1-3 の追う勇者は「勇者だけ」の通しのまま（帯を読む）{chase}");
                }
            }
            write("      「写しから始める」のボタン: 絵の口 `--map11-capture=<png>,snap-button --map-chapter=N` の `MAP11_CAPTURE_SNAP_BUTTON ok=`（画面の口で見る）");
            Req(okF, "(f) 道具: `--foe-weight` に「守り → 勇者」の通しが出る・帯の値が直っている（道の部隊・湧き・予備隊・大群は上限なし）");
        }

        int crashed = 0;
        void Crash(string what, Exception ex) { crashed++; if (crashed <= 3) write($"  × {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        if (bots)
        {
            try { BotTables(write, Crash); BoardTable(write, demoDir); }
            catch (Exception ex) { Crash("§3 器具", ex); }
        }
        Req(crashed == 0, $"器具（{(bots ? $"1 章の通し × {Seeds} seed" : "なし")}）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }

    /// <summary>1 章の通し 1 つ（器具の方針・負ければ同じステージをやり直す）。<paramref name="forced"/> は 1-1 で選ぶ札（null なら器具の選び方）。</summary>
    private sealed class Run1
    {
        public bool Through;
        public int Stuck = -1;
        public int[] Retries = new int[3];
        public int Earned, Spent, Interest, Loot, Attempts;
        public List<string> Picks = new();
    }

    private static Run1 Chapter(IReadOnlyList<MapStage> stages, int c, Policy[] pol, string? forced, bool leftAxis)
    {
        var res = new Run1();
        Map11State.CarryPack? pack = null;
        for (int i = 0; i < 3; i++)
        {
            bool won = false;
            var cards = stages[i].Invest.CardsFor(Chapter1.StageSeed(c, i, 0), HeldOf(pack?.Invest, stages[i].Id));
            for (int a = 0; a <= RetryCap && !won; a++)
            {
                MapStage stage = stages[i];
                var st = new Map11State(Chapter1.StageSeed(c, i, a), stage.Time, stage.Portal, draft: true, stage: stage);
                if (pack is not null) st.ApplyCarry(pack);
                res.Attempts++;
                if (cards.Count > 0)
                {
                    string pick = i == 0 && forced is not null ? forced
                                : i == 0 && leftAxis ? cards.First(InvestRule.IsAxis)
                                : cards[InvestRule.BotPick(st, cards)];
                    st.Invest(pick);
                    if (a == 0) res.Picks.Add(pick);
                }
                Play(st, pol[i]);
                res.Earned += st.GoldLog.Where(e => e.Amount > 0).Sum(e => e.Amount);
                res.Spent -= st.GoldLog.Where(e => e.Amount < 0).Sum(e => e.Amount);
                res.Interest += st.GoldLog.Where(e => e.Reason == "蓄えの利子").Sum(e => e.Amount);
                res.Loot += st.GoldLog.Where(e => e.Amount > 0 && (e.Reason.EndsWith("を抜いた") || e.Reason.EndsWith("を取った"))).Sum(e => e.Amount);
                won = st.Won;
                if (won) { pack = st.TakeCarry(); continue; }
                if (a < RetryCap) res.Retries[i]++;
            }
            if (!won) { res.Stuck = i; return res; }
        }
        res.Through = true;
        return res;
    }

    /// <summary>§3: 1 章の通し（第468期 ／ 第467期 × 方針 2 つ）と、1-1 で札を固定した通し（報告だけ）。</summary>
    private static void BotTables(Action<string> write, Action<string, Exception> crash)
    {
        var p2 = new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) };
        write("");
        write($"  §3 1 章の通し（1 章の seed 0〜{Seeds - 1}・負ければ同じステージをやり直す・1 ステージ {RetryCap} 回まで・買い方は第465期と同じ・札は「手持ちの軸がいちばん多い軸、無ければ札の左から」）");
        write("  | 1 章 | 方針 | 通せた | 1-1 のやり直し | 1-2 のやり直し | 1-3 のやり直し | 止まったステージ | 金 稼いだ（1 章あたり・やり直しも） | 使った | 器具が選んだ札（軸 ／ 盤 ／ 金） |");
        write("  |---|---|--:|--:|--:|--:|---|--:|--:|---|");
        foreach (ChapterRule rule in new[] { ChapterRule.V468, ChapterRule.V467 })
            foreach (var (cname, pol) in p2)
            {
                var stages = Chapter1.StagesFor(rule);
                string rkey = rule.Invest is { Doctrines: true } ? "**第468期（盤と金の札）**" : "第467期（軸だけ）";
                var runs = new List<Run1>();
                for (int c = 0; c < Seeds; c++)
                {
                    try { runs.Add(Chapter(stages, c, pol, null, false)); }
                    catch (Exception ex) { crash($"{rkey} {cname} seed {c}", ex); }
                }
                var picks = runs.SelectMany(r => r.Picks).ToList();
                string kinds = $"{picks.Count(InvestRule.IsAxis)} ／ {picks.Count(p => Doctrine.Find(p)?.Kind == DoctrineKind.Board)} ／ {picks.Count(p => Doctrine.Find(p)?.Kind == DoctrineKind.Gold)}";
                write($"  | {rkey} | {cname} | {runs.Count(r => r.Through)}% | {runs.Average(r => r.Retries[0]):F2} | {runs.Average(r => r.Retries[1]):F2} | {runs.Average(r => r.Retries[2]):F2}"
                      + $" | 1-1 {runs.Count(r => r.Stuck == 0)}・1-2 {runs.Count(r => r.Stuck == 1)}・1-3 {runs.Count(r => r.Stuck == 2)} | {runs.Average(r => r.Earned):F1} | {runs.Average(r => r.Spent):F1} | {kinds} |");
            }
        write("");
        write("  1-1 で札を固定した通し（第468期・分担＋戻り番（1-3 は逃げる）・1-2 ／ 1-3 は器具の選び方・対照は「1-1 は札の左の軸」）");
        write("  | 1-1 の札 | 通せた | やり直し（1-1 ／ 1-2 ／ 1-3） | 金 稼いだ（1 章あたり） | うち抜いた ／ 取った金 | うち利子 | 使った | 抜いた ／ 取った金（1 配りあたり） |");
        write("  |---|--:|---|--:|--:|--:|--:|--:|");
        var st468 = Chapter1.StagesFor(ChapterRule.V468);
        var pol2 = p2[1].Item2;
        foreach (string? forced in new string?[] { null }.Concat(Doctrine.All.Select(d => d.Name)))
        {
            var runs = new List<Run1>();
            for (int c = 0; c < Seeds; c++)
            {
                try { runs.Add(Chapter(st468, c, pol2, forced, forced is null)); }
                catch (Exception ex) { crash($"1-1 {forced ?? "左の軸"} seed {c}", ex); }
            }
            write($"  | {forced ?? "（対照）札の左の軸"} | {runs.Count(r => r.Through)}% | {runs.Average(r => r.Retries[0]):F2} ／ {runs.Average(r => r.Retries[1]):F2} ／ {runs.Average(r => r.Retries[2]):F2}"
                  + $" | {runs.Average(r => r.Earned):F1} | {runs.Average(r => r.Loot):F1} | {runs.Average(r => r.Interest):F1} | {runs.Average(r => r.Spent):F1} | {(double)runs.Sum(r => r.Loot) / Math.Max(1, runs.Sum(r => r.Attempts)):F1} |");
        }
        write("  （器具は札の得失を見ない——呼ぶ ／ 強化は金があるだけ使い、席は札を見ずに埋める。数字は読まない・予想 4 のとおりか見るだけ。稼いだ金はやり直しの配りも足すので、負けが多いほど増える——1 配りあたりの列で比べる）");
    }

    /// <summary>
    /// 盤の札 × 手で写した写しの隊（design/plays/manual・ポンの第461〜464期の隊）× そのステージの敵部隊（勇者を含む）の単発の戦闘 20 seed（報告だけ）。
    /// 札は開戦時の席で掛ける（作戦マップと同じ写し）。☆は写しのまま・敵の倍率は第468期の 1 章。
    /// </summary>
    private static void BoardTable(Action<string> write, string demoDir)
    {
        string root = PlayLog.DefaultRoot(demoDir);
        var snaps = PlayLog.List(PlayLog.ManualDir(root));
        const int n = 20;
        var docs = new Doctrine?[] { null }.Concat(Doctrine.All.Where(d => d.Kind == DoctrineKind.Board)).ToList();
        write("");
        write($"  盤の札 × 手で写した写しの隊（design/plays/manual の「入った時点」の隊・☆は写しのまま）× そのステージの敵部隊（勇者を含む）の単発の戦闘 seed 0〜{n - 1}");
        write("  | ステージ | 隊 | 軸 | 攻の合計 前列 ／ 中央 ／ 後列 | " + string.Join(" | ", docs.Select(d => d?.Name ?? "札なし")) + " |");
        write("  |---|---|---|---|" + string.Concat(docs.Select(_ => "--:|")));
        foreach (int index in new[] { 1, 2 })
        {
            MapStage stage = Chapter1.StagesFor(ChapterRule.V468)[index];
            string id = stage.Id;
            var squads = FoeWeight.SquadsOf(snaps, id, root);
            var foes = FoeWeight.FoesOf(MapStage.All[index].Scaled(ChapterRule.V468.ScaleOf(index)));
            MapStage scaled = MapStage.All[index].Scaled(ChapterRule.V468.ScaleOf(index));
            foreach (FoeWeight.Squad q in squads)
            {
                int Atk(Row row) => q.Pieces.Where(p => FormationRules.RowOf(p.Slot) == row).Sum(p => FoeWeight.DefOf(p, 0, StarRule.V464).Attack);
                var cells = new List<string>();
                foreach (Doctrine? d in docs)
                {
                    int wins = 0, fights = 0, lost = 0;
                    double hp = 0;
                    foreach (FoeWeight.Foe f in foes)
                        for (int seed = 0; seed < n; seed++)
                        {
                            var players = q.Pieces.Select(pc =>
                            {
                                UnitDef def = FoeWeight.DefOf(pc, 0, StarRule.V464);
                                if (d is not null) { var (h, a) = d.Of(FormationRules.RowOf(pc.Slot)); def = DoctrineDefs.Of(def, h, a); }
                                return Map11State.MakeCarried(new Map11State.CarryUnit(def, pc.Slot, pc.Relic));
                            }).OrderBy(u => u.Slot).ToList();
                            // 終わりの HP はその戦闘の隊の最大 HP（札を掛けた後）に対する割合
                            int max = players.Sum(u => u.MaxHp);
                            BattleResult r = BattleEngine.Run(players, FoeWeight.Enemies(f, scaled), seed, verbose: false);
                            fights++;
                            lost += players.Count(u => !u.IsAlive);
                            if (r.PlayerWon) { wins++; hp += 100.0 * players.Where(u => u.IsAlive).Sum(u => u.Hp) / Math.Max(1, max); }
                        }
                    cells.Add($"勝ち {100.0 * wins / fights:F0}%・倒れた {(double)lost / fights:F2}・HP {hp / fights:F0}%");
                }
                write($"  | {id} | {q.Name.Split('/').Last()} | {q.Axis} | {Atk(Row.Front)} ／ {Atk(Row.Mid)} ／ {Atk(Row.Back)} | {string.Join(" | ", cells)} |");
            }
        }
        write("  （勝ち ＝ そのステージの敵部隊すべて × 20 seed の勝ちの割合・倒れた ＝ 1 戦あたりの倒れた駒・HP ＝ 終わりの HP の平均（負けは 0・その戦闘の隊の最大 HP（札を掛けた後）に対する割合））");
    }
}
