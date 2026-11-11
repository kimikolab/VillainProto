using BattleCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================================
// 第470期の門 `--chapter1-polish-smoke`（指示書 design/PHASE470_CH1_POLISH_SPEC.md §4・画面なし）
//
//   (a) 1-1 では投資の札が出ない。1-2・1-3 の始めに出る
//   (b) 選んだ軸は以後の札に出ない（同じ軸は選べない）。軸を 2 つ取ると、それぞれ 25% で候補に寄る
//   (c) 1 章の 1-1 の先頭が 第一波／物見の兵（北）・徴募兵（南）。奥と倍率は今のまま。単発の 1-1 は今のまま
//   (d) ステージの頭の札の文が §1-4 のとおり。1-2 の顔が 2 段（流れ着いた者）
//   (e) 第460〜469期の 1 章・単発のステージは今のまま
//
// 報告だけ（線は置かない）: §3 の器具の通し（第470期 ／ 第469期 × 方針 2 つ × 1 章の seed 0〜99）と、
// §2 の調べ（第469期のポンの 1-1・1 章の seed 120065 の写しから 1 戦目 ／ 2 戦目を台本つきで回し直し、攻撃が 0 になった経路を追う）。
// `--chapter1-polish-quick` は器具の通しと §2 を省く。**規則は1つも持たない。ここには Godot の型を1つも使わない。**
// =====================================================================================

public static class Chapter1PolishSmoke
{
    public const int Seeds = 100;
    public const int RetryCap = 20;

    private readonly record struct Policy(string Name, bool Guard, bool Split, Map12ChangeSmoke.HeroPlan Plan);
    private static readonly Policy Keep1 = new("1 隊残す", true, false, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Split = new("分担＋戻り番", true, true, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Flee = new("逃げる", true, true, Map12ChangeSmoke.HeroPlan.Flee);

    private static void Play(Map11State st, Policy p)
        => Map12ChangeSmoke.Greedy(st.Seed, st.Stage, p.Guard, p.Split, false, state: st, siegeRest: true, heroPlan: p.Plan);

    /// <summary>指示書 §1-4 の文（門は Chapter1 の表を読まずに、指示書から写した文と比べる）。「／」は改行。</summary>
    private static readonly (string Id, string Heading, string Body)[] SpecTales =
    {
        ("1-1", "捨て駒の初陣", "軍が寄越したのは、使えない奴らと使えない砦。上等だ。／砦の墓地には先客がいた。墓守のエルフだ——「ここの墓は、私が守ってる。あなたたちが入るなら、それも守る」"),
        ("1-2", "砦を取れ", "砦に灯りがともると、妙な連中が流れ着くようになった。王国に居場所をなくした人間だ。魔王軍に人間？　——俺も人間だ。使える奴は使う。"),
        ("1-3", "狩る者", "本隊は撤退した。俺たちは捨て駒だった——慣れたもんだ。拾った連中まで、捨てる気は無い。"),
    };

    public static bool Run(Action<string> write, string demoDir, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V470;
        var ch = Chapter1.StagesFor(rule);
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}・投資 1-1 {ch[0].Invest.Name} ／ 1-2 {ch[1].Invest.Name} ／ 1-3 {ch[2].Invest.Name}");

        // ---- (a) 投資の札は 1-2 から ----
        {
            int none11 = 0, pend11 = 0, refused11 = 0;
            for (int c = 0; c < 200; c++)
            {
                Chapter1.Begin(c, rule);
                if (Chapter1.InvestCards.Count == 0) none11++;
                if (Chapter1.InvestPending) pend11++;
                if (!Map11Session.State!.Invest("燃焼") && !Map11Session.State.Invest(Doctrine.Forward.Name)) refused11++;
            }
            Chapter1.End();
            var counts = new int[3];
            var noCards = new int[3];
            for (int c = 0; c < 200; c++)
            {
                var run = Chapter(rule, c, new[] { Keep1, Keep1, Keep1 }, cardsOnly: true);
                for (int i = 0; i < 3; i++) { if (run.Cards[i] is { } cs) { counts[i] += cs.Count; if (cs.Count == 0) noCards[i]++; } }
            }
            bool okA = none11 == 200 && pend11 == 0 && refused11 == 200 && !ch[0].Invest.On && ch[1].Invest.On && ch[2].Invest.On
                       && counts[0] == 0 && noCards[1] == 0 && noCards[2] == 0;
            write($"  (a) 1 章の seed 0〜199: 1-1 の札 0 枚 {none11} ／ 200・選ぶ窓が出ない {200 - pend11} ／ 200・軸も盤の札も受けない {refused11} ／ 200"
                  + $"・器具の通しで札の枚数の合計 1-1 {counts[0]} ／ 1-2 {counts[1]} ／ 1-3 {counts[2]}（札が 0 枚の 1-2 ／ 1-3 は {noCards[1]} ／ {noCards[2]}）");
            Req(okA, "(a) 1-1 では投資の札が出ない（窓も出ず・選べない）。1-2・1-3 の始めに出る（1 章で 2 回）");
        }

        // ---- (b) 取った軸は出さない・2 軸で 25% ずつ ----
        {
            int runs = 0, again = 0, refused = 0, tested = 0, axisPicks = 0;
            for (int c = 0; c < 200; c++)
            {
                var run = Chapter(rule, c, new[] { Keep1, Keep1, Keep1 }, cardsOnly: true);
                runs++;
                if (run.Picked[1] is { } p12 && InvestRule.IsAxis(p12))
                {
                    axisPicks++;
                    if (run.Cards[2] is { } c13 && c13.Contains(p12)) again++;
                    if (run.RefuseTried) { tested++; if (run.Refused) refused++; }
                }
            }
            // 2 軸の投資（燃焼 ＋ 毒）で 1-3 の呼ぶ候補を 4000 枚覗く（候補は列に戻す）。
            string a1 = "燃焼", a2 = "毒";
            var s13 = ch[2];
            var st = new Map11State(4703, s13.Time, s13.Portal, draft: true, stage: s13);
            st.ApplyCarry(st.TakeCarry() with { Invest = new[] { ("1-2", a1), ("1-3", a2) } });
            int n = 4000, inv = 0, h1 = 0, h2 = 0, both = 0;
            for (int k = 0; k < n; k++)
            {
                var (d, isInv) = st.ProbeOffer();
                if (d is null || !isInv) continue;
                inv++;
                var axes = MapDraw.AxesOf(d);
                bool x1 = axes.Contains(a1), x2 = axes.Contains(a2);
                if (x1 && x2) both++; else if (x1) h1++; else if (x2) h2++;
            }
            double pInv = 100.0 * inv / n, p1 = 100.0 * (h1 + both / 2.0) / n, p2 = 100.0 * (h2 + both / 2.0) / n;
            bool share = st.Stage.Invest.Percent(2) == 50 && Math.Abs(pInv - 50) < 4 && Math.Abs(p1 - 25) < 4 && Math.Abs(p2 - 25) < 4;
            bool okB = again == 0 && tested > 0 && refused == tested && share && st.InvestText == $"投資: {a1}・{a2}";
            write($"  (b) 器具の通し 200 本: 1-2 で軸を取った {axisPicks} 本のうち、1-3 の札にその軸が出た {again} 本・1-3 で取った軸を選ぼうとして断られた {refused} ／ {tested}");
            write($"      2 軸（{a1}・{a2}）で 1-3 の候補 {n} 枚: 投資の枠から {pInv:F1}%（式 {st.Stage.Invest.Percent(2)}%）・{a1} {p1:F1}% ／ {a2} {p2:F1}%（両方の軸の駒 {both} 枚は半分ずつ）・投資の 1 行「{st.InvestText}」");
            Req(okB, "(b) 選んだ軸は以後の札に出ず、選べない。軸を 2 つ取ると、それぞれ 25% で候補に寄る（合わせて 50%・投資の 1 行は軸の名前だけ）");
        }

        // ---- (c) 1-1 の先頭 ----
        {
            var s11 = ch[0];
            string Lead(MapStage s, int lane) => s.Lanes[lane][0].Name;
            string Deep(MapStage s, int lane) => s.Lanes[lane][1].Name;
            string first = EnemyCatalog.Stages[0].Name, levy = EnemyCatalog.MapSquadOf("band_levy").Name;
            bool lead = Lead(s11, 0) == first && Lead(s11, 1) == levy;
            bool deep = Deep(s11, 0) == Deep(MapStage.Stage11Ch, 0) && Deep(s11, 1) == Deep(MapStage.Stage11Ch, 1);
            bool scale = s11.FoeScale.FoePercent == 60;
            bool spawn = Enumerable.Range(0, 2).All(r => s11.Spawn(r).Name == MapStage.Stage11Ch.Spawn(r).Name);
            bool single = Lead(MapStage.Stage11, 0) == EnemyCatalog.Vanguards[0].Name && Lead(MapStage.Stage11, 1) == EnemyCatalog.Vanguards[1].Name
                          && ReferenceEquals(MapStage.Stage11.Lanes, Map11.Roads) && ReferenceEquals(MapStage.Stage11Ch.Lanes, Map11.Roads);
            bool old = ChapterRule.Versions.Where(v => string.CompareOrdinal(v.Key, "V470") < 0)
                .All(v => Lead(Chapter1.StagesFor(v.Rule)[0], 0) == EnemyCatalog.Vanguards[0].Name && Lead(Chapter1.StagesFor(v.Rule)[0], 1) == EnemyCatalog.Vanguards[1].Name);
            write($"  (c) 1 章の 1-1: 北 {Lead(s11, 0)} → {Deep(s11, 0)} ／ 南 {Lead(s11, 1)} → {Deep(s11, 1)}・倍率 ×{s11.FoeScale.FoePercent / 100.0:F1}・湧き {s11.Spawn(0).Name} ／ {s11.Spawn(1).Name}"
                  + $"・単発の 1-1 は 北 {Lead(MapStage.Stage11, 0)} ／ 南 {Lead(MapStage.Stage11, 1)}・第461〜469期の 1 章の 1-1 も今のまま {old}");
            Req(lead && deep && scale && spawn && single && old, "(c) 1 章の 1-1 の先頭が 第一波／物見の兵（北）・徴募兵（南）。奥・湧き・倍率は今のまま。単発の 1-1 と第461〜469期の 1 章の 1-1 は今のまま");
        }

        // ---- (d) 頭の札の文と顔の段 ----
        {
            Chapter1.Begin(4704, rule);
            bool okD = true;
            var lines = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var (id, heading, body) = SpecTales[i];
                var (t, b) = Chapter1.Intro(ch[i]);
                string want = body.Replace("／", "\n") + "\n" + ch[i].FoeScale.IntroLine;
                bool same = t == $"1 章 ・ {id} {heading}" && b == want;
                var groups = Chapter1.JoinGroups(ch[i]);
                bool g = id == "1-2"
                    ? groups.Count == 2 && groups[0].Heading is null && groups[1].Heading == "流れ着いた者"
                      && groups[1].Units.Select(d => d.Name).SequenceEqual(new[] { "囃し立てのヒサ", "廃棄聖騎士ガルド", "仇討ちのザン" })
                      && groups.Sum(x => x.Units.Count) == ch[i].Roster.JoinsAt(id).Count
                    : groups.Count == 1 && groups[0].Heading is null;
                okD &= same && g;
                lines.Add($"{id} 文 {same}・顔の段 {string.Join(" ／ ", groups.Select(x => $"{x.Heading ?? "（見出しなし）"} {x.Units.Count} 体"))}");
            }
            Chapter1.End();
            write($"  (d) {string.Join("・", lines)}（画面の段は絵の口 `,card` の MAP11_CAPTURE_ROSTER の groups= で見る）");
            Req(okD, "(d) ステージの頭の札の文が §1-4 のとおり（見出し ＋ 文 ＋ 敵の強さの 1 行）。1-2 の顔が 2 段（流れ着いた者: ヒサ・ガルド・ザン）");
        }

        // ---- (e) 第460〜469期・単発は今のまま ----
        {
            bool okE = MapStage.All.All(s => !s.Invest.On);
            var parts = new List<string>();
            foreach (var (key, r) in ChapterRule.Versions.Where(v => string.CompareOrdinal(v.Key, "V470") < 0))
            {
                var ss = Chapter1.StagesFor(r);
                bool inv11 = ss[0].Invest.On == (string.CompareOrdinal(key, "V467") >= 0);
                bool once = ss.All(s => !s.Invest.Once);
                bool story = !r.Story && r.First is null;
                Chapter1.Begin(4705, r);
                bool text = Chapter1.Intro(ss[0]).Body.Contains("（仮の文）") && ss.All(s => Chapter1.JoinGroups(s).Count <= 1);
                Chapter1.End();
                okE &= inv11 && once && story && text;
                parts.Add($"{key} {(inv11 && once && story && text ? "今のまま" : "×")}");
            }
            write($"  (e) {string.Join("・", parts)}（1-1 の投資の有無・軸の重ね・1-1 の盤・仮の文・顔の段 1 つ）。単発の 1-1 ／ 1-2 ／ 1-3 は投資なし");
            write("      （単発の頭なしの門・画面の門・第460〜469期の 1 章の門・`compare` ／ `docs/` が 0 行かは回帰で見る）");
            Req(okE, "(e) 第460〜469期の 1 章・単発のステージは今のまま");
        }

        int crashed = 0;
        void Crash(string what, Exception ex) { crashed++; write($"  × 器具が落ちた: {what}: {ex.GetType().Name}: {ex.Message}"); }
        if (bots)
        {
            try { BotTables(write, Crash); } catch (Exception ex) { Crash("器具の通し", ex); }
            try { Trace(write, demoDir); } catch (Exception ex) { Crash("§2 の調べ", ex); }
        }
        Req(crashed == 0, $"器具（{(bots ? $"1 章の通し × {Seeds} seed ・§2 の調べ" : "なし")}）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }

    // ---------------- 器具の通し（§3 の予想の確かめ・報告だけ） ----------------

    private sealed class Run1
    {
        public bool Through;
        public int Stuck = -1;
        public int[] Retries = new int[3];
        public int Turn11 = -1;                          // 1-1 を制圧した作戦ターン（勝った配り）
        public IReadOnlyList<string>?[] Cards = new IReadOnlyList<string>?[3];
        public string?[] Picked = new string?[3];
        public bool RefuseTried, Refused;
        public (int Own, int All) Axis12Entry = (-1, -1), Axis12End = (-1, -1);   // 1-2 で取った軸の手持ち ／ 呼べる駒 ＋ 手持ち（1-2 に入った時点・1-2 の終わり）
        public int InvestShown12;                       // 1-2 の勝った配りで投資の枠から出た候補の枚数
    }

    private static Run1 Chapter(ChapterRule rule, int c, Policy[] pol, bool cardsOnly = false)
    {
        var stages = Chapter1.StagesFor(rule);
        var res = new Run1();
        Map11State.CarryPack? pack = null;
        for (int i = 0; i < 3; i++)
        {
            bool won = false;
            for (int a = 0; a <= RetryCap && !won; a++)
            {
                MapStage stage = stages[i];
                var st = new Map11State(Chapter1.StageSeed(c, i, a), stage.Time, stage.Portal, draft: true, stage: stage);
                if (pack is not null) st.ApplyCarry(pack);
                // 札は入った時点で決まる（Chapter1.InvestCards と同じ式・やり直しでも同じ札）
                var cards = stage.Invest.CardsFor(Chapter1.StageSeed(c, i, 0), Chapter1.HeldOf(stage, pack?.Invest ?? Array.Empty<(string, string)>()), Chapter1.OpenAxesOf(st));
                if (a == 0) res.Cards[i] = cards;
                if (a == 0 && i == 2 && rule.Invest is { Once: true } && st.InvestLog.FirstOrDefault(x => InvestRule.IsAxis(x.Axis)).Axis is { } held)
                {
                    res.RefuseTried = true;
                    res.Refused = !st.Invest(held) && st.InvestPending;   // 第470期 §1-2: 取った軸は選べない
                }
                if (cards.Count > 0) { string pick = cards[InvestRule.BotPick(st, cards)]; st.Invest(pick); res.Picked[i] = pick; }
                if (i == 1 && a == 0 && res.Picked[1] is { } p1 && InvestRule.IsAxis(p1)) res.Axis12Entry = (st.OwnedAxisCount(p1), st.AxisPieces(p1).Count);
                if (cardsOnly)
                {
                    // 札だけを見る: 遊ばずに次のステージへ（持ち越しは入った時点の写しに選んだ札を足したもの）
                    pack = st.TakeCarry();
                    won = true;
                    continue;
                }
                Play(st, pol[i]);
                won = st.Won;
                if (won)
                {
                    if (i == 0) res.Turn11 = st.CapturedTurn + 1;
                    if (i == 1)
                    {
                        if (res.Picked[1] is { } p && InvestRule.IsAxis(p)) res.Axis12End = (st.OwnedAxisCount(p), st.AxisPieces(p).Count);
                        res.InvestShown12 = st.InvestOffersShown;
                    }
                    pack = st.TakeCarry();
                    continue;
                }
                if (a < RetryCap) res.Retries[i]++;
            }
            if (!won) { res.Stuck = i; return res; }
        }
        res.Through = true;
        return res;
    }

    private static void BotTables(Action<string> write, Action<string, Exception> crash)
    {
        var p2 = new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) };
        write("");
        write($"  §3 1 章の通し（1 章の seed 0〜{Seeds - 1}・負ければ同じステージをやり直す・1 ステージ {RetryCap} 回まで・買い方は第465期と同じ・札は「手持ちの軸がいちばん多い軸、無ければ札の左から」）");
        write("  | 1 章 | 方針 | 通せた | やり直し 1-1 ／ 1-2 ／ 1-3 | 止まったステージ | 1-1 を制圧した作戦ターン（平均・中央値） | 投資の回数（軸 ／ 盤・金） | 1-2 で取った軸の手持ち ／ 呼べる駒＋手持ち（入った時点 → 1-2 の終わり） | 1-2 の終わりにその軸を全部持っていた | 1-2 の投資の枠から出た候補 |");
        write("  |---|---|--:|---|---|---|---|---|--:|--:|");
        foreach (ChapterRule rule in new[] { ChapterRule.V470, ChapterRule.V469 })
            foreach (var (cname, pol) in p2)
            {
                string rkey = rule.Name == ChapterRule.V470.Name ? "**第470期**" : "第469期";
                var runs = new List<Run1>();
                for (int c = 0; c < Seeds; c++)
                {
                    try { runs.Add(Chapter(rule, c, pol)); }
                    catch (Exception ex) { crash($"{rkey} {cname} seed {c}", ex); }
                }
                var t11 = runs.Where(r => r.Turn11 > 0).Select(r => r.Turn11).OrderBy(x => x).ToList();
                string turn = t11.Count == 0 ? "—" : $"{t11.Average():F2}（{t11[t11.Count / 2]}）";
                int axisN = runs.Sum(r => r.Picked.Count(p => p is not null && InvestRule.IsAxis(p)));
                int docN = runs.Sum(r => r.Picked.Count(p => p is not null && !InvestRule.IsAxis(p)));
                var ax = runs.Where(r => r.Axis12End.All > 0).ToList();
                string axis = ax.Count == 0 ? "—"
                    : $"{ax.Average(r => r.Axis12Entry.Own):F2} ／ {ax.Average(r => r.Axis12Entry.All):F2} → {ax.Average(r => r.Axis12End.Own):F2} ／ {ax.Average(r => r.Axis12End.All):F2}（{ax.Count} 本）";
                string full = ax.Count == 0 ? "—" : $"{100.0 * ax.Count(r => r.Axis12End.Own >= r.Axis12End.All) / ax.Count:F0}%";
                var r12 = runs.Where(r => r.Stuck < 0 || r.Stuck > 1).ToList();
                string shown = r12.Count == 0 ? "—" : $"{r12.Average(r => r.InvestShown12):F2} 枚";
                write($"  | {rkey} | {cname} | {runs.Count(r => r.Through)}% | {runs.Average(r => r.Retries[0]):F2} ／ {runs.Average(r => r.Retries[1]):F2} ／ {runs.Average(r => r.Retries[2]):F2}"
                      + $" | 1-1 {runs.Count(r => r.Stuck == 0)}・1-2 {runs.Count(r => r.Stuck == 1)}・1-3 {runs.Count(r => r.Stuck == 2)} | {turn} | {axisN} ／ {docN} | {axis} | {full} | {shown} |");
            }
        write("  （「1-2 で取った軸」は 1-2 の札で軸を選んだ通しだけ・呼べる駒＋手持ちは `Map11State.AxisPieces`（加入の表のそのステージで呼べる駒 ＋ 持っている駒）。第469期は 1-1 でも軸を取れる（1-2 で同じ軸を重ねた通しも入る））");
    }

    // ---------------- §2 の調べ（ネル・ムド ＋ リィカの泥仕合・報告だけ） ----------------

    /// <summary>
    /// 第469期のポンの 1-1（1 章の seed 120065・ステージの seed 899464）の写し `design/plays/120065/1-1_first.json` から、
    /// 1 戦目（第1隊 → 北 第二波・先遣・迎撃）と 2 戦目（第1隊 → 南 第四波・先遣・迎撃・HP は 1 戦目の終わりのまま）を
    /// 作戦マップと同じ口（`PrepareIntercept` ／ `Resolve`・戦闘の seed は `Map11.DeriveSeed(ステージの seed, 何戦目)`）で回し直し、台本から攻撃が 0 になった経路を数える。
    /// </summary>
    private static void Trace(Action<string> write, string demoDir)
    {
        string repo = Path.GetFullPath(Path.Combine(demoDir, ".."));
        string path = Path.Combine(repo, "design", "plays", "120065", "1-1_first.json");
        write("");
        if (!File.Exists(path)) { write($"  §2 写しが無い（{path}・飛ばした）"); return; }
        PlayLog.Snap snap = PlayLog.Read(path);
        ChapterRule rule = ChapterRule.ByKey(snap.Rule) ?? ChapterRule.V469;
        MapStage stage = Chapter1.StagesFor(rule)[snap.StageIndex];
        var st = new Map11State(snap.StageSeed, stage.Time, stage.Portal, draft: true, stage: stage);
        st.ApplyCarry(PlayLog.ToCarry(snap.Pack!, stage.Stars));
        write($"  §2 ポンの 1-1 の写し（{rule.Key}・1 章の seed {snap.ChapterSeed}・ステージの seed {snap.StageSeed}）: 第1隊 "
              + string.Join("・", (st.Squads[0].Units ?? new List<UnitState>()).Select(u => $"{Seat(u.Slot)} {u.Def.Name}")));
        string dumpDir = Path.Combine(repo, ".tmp", "p470");
        Directory.CreateDirectory(dumpDir);
        var recorded = snap.Kind == "end" ? snap.Battles : PlayLog.Read(Path.Combine(repo, "design", "plays", "120065", "1-1_end.json")).Battles;
        for (int b = 0; b < 2; b++)
        {
            Map11State.Node node = st.Nodes[b][0];
            var prep = st.PrepareIntercept(0, node);
            if (prep is not { } p) { write($"  §2 {b + 1} 戦目: 迎撃を組めない（飛ばした）"); continue; }
            BattleResult r = BattleEngine.Run(p.Players, p.Enemies, p.Seed, verbose: true);
            int maxHp = p.Players.Sum(u => u.MaxHp);
            double hp = 100.0 * p.Players.Where(u => u.IsAlive).Sum(u => u.Hp) / Math.Max(1, maxHp);
            st.Resolve(0, node, r.PlayerWon, intercept: true);
            var rec = recorded is { Count: > 0 } && b < recorded.Count ? recorded[b] : null;
            write($"  §2 {b + 1} 戦目 第1隊 → {node.Def.Name}（seed {p.Seed}）: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns} ターン・残り {p.Players.Count(u => u.IsAlive)} 枚・HP {hp:F1}%"
                      + (rec is null ? "" : $"（写しの記録: 残り {rec.Alive} 枚・HP {rec.Hp:F1}%・{(Math.Abs(rec.Hp - hp) < 0.05 && rec.Alive == p.Players.Count(u => u.IsAlive) ? "一致" : "不一致")}）"));
            File.WriteAllLines(Path.Combine(dumpDir, $"trace_battle{b + 1}.txt"), r.Log.Select(l => l.ToString()));
            TraceAttack(write, r, p.Players.Concat(p.Enemies).ToList());
        }
    }

    private static string Seat(int slot) => slot switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => $"席{slot}" };

    /// <summary>攻撃の写し（ターン頭の `StatSnapshot`）と、なまりの書き手（`StatusGain`）を数える。</summary>
    private static void TraceAttack(Action<string> write, BattleResult r, List<UnitState> all)
    {
        var byId = new Dictionary<int, UnitState>();
        foreach (UnitState u in all) byId[u.InstanceId] = u;
        string Name(int? id) => id is { } i && byId.TryGetValue(i, out UnitState? u) ? $"{(u.TeamId == BattleContext.EnemyTeam ? "敵" : "味方")} {u.Def.Name}" : id is null ? "盤の規則" : $"#{id}";
        var snaps = r.Events.Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId is { } sid && byId.ContainsKey(sid)).ToList();   // 駒は TargetId
        int turns = r.Turns;
        write("      攻撃の写し（ターン頭・`StatSnapshot`）: 駒ごとに T1 → …（0 になったターン）");
        foreach (var g in snaps.GroupBy(e => e.TargetId!.Value).OrderBy(g => byId.TryGetValue(g.Key, out UnitState? u) ? u.TeamId : 9).ThenBy(g => g.Key))
        {
            var seq = g.OrderBy(e => e.Turn).ToList();
            int? zero = seq.FirstOrDefault(e => e.Amount <= 0)?.Turn;
            string trail = string.Join(" ", seq.Where((e, i) => i < 12).Select(e => e.Amount.ToString()));
            write($"        {Name(g.Key)}: {trail}{(seq.Count > 12 ? " …" : "")}（素 {(byId.TryGetValue(g.Key, out UnitState? uu) ? uu.Def.Attack : 0)}・0 になったのは {(zero is { } z ? $"T{z}" : "なし")}）");
        }
        // 全員の攻撃が 0 のターン（味方 ／ 敵）
        int allZeroP = 0, allZeroE = 0;
        foreach (var t in snaps.GroupBy(e => e.Turn))
        {
            var ps = t.Where(e => byId[e.TargetId!.Value].TeamId != BattleContext.EnemyTeam).ToList();
            var es = t.Where(e => byId[e.TargetId!.Value].TeamId == BattleContext.EnemyTeam).ToList();
            if (ps.Count > 0 && ps.All(e => e.Amount <= 0)) allZeroP++;
            if (es.Count > 0 && es.All(e => e.Amount <= 0)) allZeroE++;
        }
        write($"      味方の全員が攻撃 0 のターン {allZeroP} ／ 敵の全員が攻撃 0 のターン {allZeroE}（決着 {turns} ターン）");
        var gains = r.Events.Where(e => e.Kind == BattleEventKind.StatusGain).ToList();
        write("      付いた状態（`StatusGain`・書き手 → 受け手の陣営: 回数・合計・最初のターン）:");
        foreach (var g in gains.GroupBy(e => (Actor: Name(e.ActorId), Key: e.Text ?? "", Side: e.TargetId is { } tid && byId.TryGetValue(tid, out UnitState? tu) ? (tu.TeamId == BattleContext.EnemyTeam ? "敵" : "味方") : "後から立った駒（召喚）"))
                               .OrderByDescending(g => g.Count()).Take(12))
            write($"        {g.Key.Actor} → {g.Key.Side} の {g.Key.Key}: {g.Count()} 回・計 {g.Sum(e => e.Amount)}・最初 T{g.Min(e => e.Turn)}");
        // 誰の攻撃で誰が削られたか（味方の与ダメの出どころ）
        var dmg = r.Events.Where(e => e.Kind == BattleEventKind.Damage && e.ActorId is { } a && byId.TryGetValue(a, out UnitState? au) && au.TeamId != BattleContext.EnemyTeam
                                      && e.TargetId is { } t && byId.TryGetValue(t, out UnitState? tu) && tu.TeamId == BattleContext.EnemyTeam).ToList();
        write("      味方が敵に与えたダメージ（出どころの駒: 合計・回数）: " + string.Join("・", dmg.GroupBy(e => Name(e.ActorId)).OrderByDescending(g => g.Sum(e => e.Amount)).Select(g => $"{g.Key.Replace("味方 ", "")} {g.Sum(e => e.Amount)}（{g.Count()} 回）")));
    }
}
