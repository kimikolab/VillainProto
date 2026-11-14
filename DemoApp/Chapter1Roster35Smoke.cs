using BattleCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================================
// 第475期の門 `--chapter1-roster35-smoke`（指示書 design/PHASE475_ROSTER35_SPEC.md §4・画面なし）
//
//   (a) 1-1 で呼べるのは 5 体。ハギは 1-1 ・1-2 の候補に出ず、1-3 で出る
//   (b) ウツ・ワタは 1-2 から候補に出る。1-1 では出ない
//   (c) 体験版の駒は 35 体。棚卸しの表（`--roster33-audit --roster=V475`）が 35 行で出る
//   (d) 第461〜474期の 1 章・単発は今のまま
//
// 加えて（報告だけ・線は置かない）:
//   §1 の確かめ: 5 体の表で 1-1 の呼ぶ候補が 3 枚そろうか・軸の札に出せる軸・頭の札の顔の段
//   §2-3 ハギ抜きの 1-1: 第474期の通し（北だけ ／ 南だけ ／ 2 隊に分ける × 100 seed）を、第475期の表と第474期の表で並べる
// `--chapter1-roster35-quick` は通しと棚卸しの表を省く。**規則は1つも持たない。ここには Godot の型を1つも使わない。**
// =====================================================================================

public static class Chapter1Roster35Smoke
{
    private static readonly string[] Join11 = { "継ぎ接ぎのヴェル", "分かちのドハ", "見境なしのミサ", "爆ぜるゾト", "呪詛官ネル" };
    private const int Seeds = 200;
    private const int Probe = 40;

    /// <summary>そのステージを seed <paramref name="c"/> で始めたときに、呼ぶ候補と列の先 <see cref="Probe"/> 枚に出た駒の Id。</summary>
    /// 1-2 ・1-3 は、1-1 の始めの 6 枚をそのまま持ち越して入ったことにする（単発の始めの手札は 1 章では使わない——持ち越しが手札を置き換える）。
    private static (HashSet<string> Seen, int Offers) Offered(MapStage stage, int c, int index, MapStage first)
    {
        var st = new Map11State(Chapter1.StageSeed(c, index, 0), stage.Time, stage.Portal, draft: true, stage: stage);
        if (index > 0) st.ApplyCarry(new Map11State(Chapter1.StageSeed(c, 0, 0), first.Time, first.Portal, draft: true, stage: first).TakeCarry());
        var seen = st.Offers.Select(o => o.Id).ToHashSet();
        int offers = st.Offers.Count;
        for (int k = 0; k < Probe; k++)
            if (st.ProbeOffer().Def is { } d) seen.Add(d.Id);
        return (seen, offers);
    }

    public static bool Run(Action<string> write, string demoDir, bool full = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V475;
        var ch = Chapter1.StagesFor(rule);
        RosterRule roster = ch[0].Roster;
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}・加入の表 {roster.Name}");
        UnitDef hagi = RosterRule.Find("彼岸花のハギ"), utsu = RosterRule.Find("逆しまのウツ"), wata = RosterRule.Find("渡しのワタ");

        // 候補に出た回数（ステージごと・1 章の seed 0〜199・始めの候補 ＋ 列の先 40 枚）
        var seen = new Dictionary<string, int>[3];
        var offerCounts = new List<int>[3];
        for (int i = 0; i < 3; i++)
        {
            seen[i] = new Dictionary<string, int>();
            offerCounts[i] = new List<int>();
            for (int c = 0; c < Seeds; c++)
            {
                var (ids, n) = Offered(ch[i], c, i, ch[0]);
                offerCounts[i].Add(n);
                foreach (string id in ids) seen[i][id] = seen[i].GetValueOrDefault(id) + 1;
            }
        }
        int Hit(int stage, UnitDef d) => seen[stage].GetValueOrDefault(d.Id);

        // ---- (a) 1-1 は 5 体・ハギは 1-3 から ----
        {
            var pool11 = roster.PoolFor("1-1").Select(d => d.Name).ToList();
            bool five = pool11.Count == 5 && Join11.All(pool11.Contains) && roster.JoinsAt("1-1").Select(d => d.Name).SequenceEqual(Join11);
            bool start = roster.Start.SequenceEqual(RosterRule.V471.Start);
            bool allows = !roster.Allows("1-1", hagi) && !roster.Allows("1-2", hagi) && roster.Allows("1-3", hagi);
            bool onlyPool = seen[0].Keys.All(id => roster.PoolFor("1-1").Any(d => d.Id == id));
            write($"  (a) 1-1 で呼べる {pool11.Count} 体 {string.Join("・", pool11)}・始めの 6 枚は第471期のまま {start}"
                  + $"・ハギが候補に出た配り 1-1 {Hit(0, hagi)} ／ 1-2 {Hit(1, hagi)} ／ 1-3 {Hit(2, hagi)}（各 {Seeds} 配り・始めの候補 ＋ 列の先 {Probe} 枚・1-2 ／ 1-3 は始めの 6 枚を持ち越して入る）");
            Req(five && start && allows && onlyPool && Hit(0, hagi) == 0 && Hit(1, hagi) == 0 && Hit(2, hagi) > 0,
                "(a) 1-1 で呼べるのは 5 体（ヴェル・ドハ・ミサ・ゾト・ネル）。ハギは 1-1 ・1-2 の候補に出ず、1-3 で出る");
        }

        // ---- (b) ウツ・ワタは 1-2 から ----
        {
            bool allows = new[] { utsu, wata }.All(d => !roster.Allows("1-1", d) && roster.Allows("1-2", d) && roster.Allows("1-3", d));
            int n12 = roster.PoolFor("1-2").Count, n13 = roster.PoolFor("1-3").Count, j12 = roster.JoinsAt("1-2").Count, j13 = roster.JoinsAt("1-3").Count;
            write($"  (b) ウツが候補に出た配り 1-1 {Hit(0, utsu)} ／ 1-2 {Hit(1, utsu)} ／ 1-3 {Hit(2, utsu)}・ワタ 1-1 {Hit(0, wata)} ／ 1-2 {Hit(1, wata)} ／ 1-3 {Hit(2, wata)}"
                  + $"・1-2 で加わる {j12} 体（呼べる {n12} 体）・1-3 で加わる {j13} 体（呼べる {n13} 体）");
            Req(allows && Hit(0, utsu) == 0 && Hit(0, wata) == 0 && Hit(1, utsu) > 0 && Hit(1, wata) > 0 && j12 == 16 && j13 == 8 && n12 == 21 && n13 == 29,
                "(b) ウツ・ワタは 1-2 から候補に出る。1-1 では出ない（1-2 で 16 体・1-3 で 8 体が加わる）");
        }

        // ---- (c) 35 体・棚卸しの表 ----
        {
            var units = Roster33Audit.Units(roster);
            bool count = units.Count == 35 && units.Select(d => d.Id).Distinct().Count() == 35;
            var was = Roster33Audit.Units(RosterRule.V471).Select(d => d.Id).ToHashSet();
            var added = units.Where(d => !was.Contains(d.Id)).Select(d => d.Name).ToList();
            bool diff = added.SequenceEqual(new[] { utsu.Name, wata.Name }) && was.All(id => units.Any(d => d.Id == id));
            string note = "棚卸しの表は省いた";
            bool table = true;
            if (full)
            {
                string repo = Path.GetFullPath(Path.Combine(demoDir, ".."));
                var (_, rows) = Roster33Audit.Build(_ => { }, Path.Combine(repo, "design", "plays"), rule, bots: false, weights: false, rosterRule: roster);
                var (_, rows33) = Roster33Audit.Build(_ => { }, Path.Combine(repo, "design", "plays"), ChapterRule.V470, bots: false, weights: false);
                string md = Path.Combine(repo, "design", "ROSTER35_AUDIT.md");
                int mdRows = File.Exists(md) ? File.ReadAllLines(md).Count(l => l.StartsWith("| ", StringComparison.Ordinal) && !l.StartsWith("| 駒 ", StringComparison.Ordinal) && !l.StartsWith("| 順 ", StringComparison.Ordinal) && l.Count(x => x == '|') >= 12) : -1;
                table = rows.Count == 35 && rows33.Count == 33 && mdRows == 35;
                note = $"棚卸しの表 {rows.Count} 行（第471期の表のままなら {rows33.Count} 行）・`design/ROSTER35_AUDIT.md` の駒の行 {mdRows}";
            }
            write($"  (c) 体験版の駒 {units.Count} 体（第471期の 33 体 ＋ {string.Join("・", added)}）・{note}");
            Req(count && diff && table, "(c) 体験版の駒は 35 体（33 体 ＋ ウツ・ワタ）。棚卸しの表が 35 行で出る");
        }

        // ---- (d) 第461〜474期・単発 ----
        {
            bool single = !MapStage.Stage11.Roster.On && !MapStage.Stage12.Roster.On && !MapStage.Stage13.Roster.On;
            bool okD = single;
            var parts = new List<string>();
            foreach (var (key, r) in ChapterRule.Versions.Where(v => string.CompareOrdinal(v.Key, "V461") >= 0 && string.CompareOrdinal(v.Key, "V475") < 0))
            {
                RosterRule want = key is "V469" or "V470" ? RosterRule.V469 : key is "V471" or "V474" ? RosterRule.V471 : RosterRule.Off;
                bool same = Chapter1.StagesFor(r).All(s => ReferenceEquals(s.Roster, want));
                okD &= same;
                parts.Add($"{key} {(same ? "今のまま" : "×")}");
            }
            // 表の中身（始めの 6 枚・ステージごとの駒）が第469期 ／ 第471期の値のまま
            bool v471 = RosterRule.V471.JoinsAt("1-1").Select(d => d.Name).SequenceEqual(new[] { "彼岸花のハギ", "継ぎ接ぎのヴェル", "分かちのドハ", "見境なしのミサ", "爆ぜるゾト", "呪詛官ネル" })
                        && RosterRule.V471.PoolFor("1-2").Count == 20 && RosterRule.V471.PoolFor("1-3").Count == 27
                        && RosterRule.V469.PoolFor("1-1").Count == 6 && RosterRule.V469.PoolFor("1-3").Count == 27 && Roster33Audit.Units(RosterRule.V471).Count == 33;
            // 第475期は第474期と加入の表だけが違う（盤・敵・倍率・☆・投資は同じもの）
            var s474 = Chapter1.StagesFor(ChapterRule.V474);
            bool onlyRoster = ch.Zip(s474).All(p => ReferenceEquals(p.First.Lanes, p.Second.Lanes) && p.First.FoeScale == p.Second.FoeScale && ReferenceEquals(p.First.Stars, p.Second.Stars)
                                                    && ReferenceEquals(p.First.Invest, p.Second.Invest) && p.First.Gold == p.Second.Gold && p.First.Id == p.Second.Id);
            write($"  (d) 単発の 1-1 ／ 1-2 ／ 1-3 に加入の表なし {single}・{string.Join("・", parts)}（加入の表が同じもの）・第469期 ／ 第471期の表の中身 {(v471 ? "今のまま" : "×")}・第475期は第474期と加入の表だけが違う {onlyRoster}");
            Req(okD && v471 && onlyRoster, "(d) 第461〜474期の 1 章・単発は今のまま（第475期は第474期と加入の表だけが違う）");
        }

        // ---- §1 の確かめ（報告だけ・ただし 5 体で候補が欠けたら止める） ----
        {
            for (int i = 0; i < 3; i++)
                write($"  §1 {ch[i].Id} の始めの候補の枚数: {string.Join("・", offerCounts[i].GroupBy(x => x).OrderBy(g => g.Key).Select(g => $"{g.Key} 枚 × {g.Count()} 配り"))}（候補の枠 {ch[i].Gold.Offers}）");
            Req(offerCounts[0].All(x => x == ch[0].Gold.Offers), "§1 5 体の表でも、1-1 の始めの呼ぶ候補が枠の数だけそろう（1 枚目の「手持ちと噛む駒」も引ける）");
            // 軸の札に出せる軸（始めの 6 枚だけを持って入ったとして・そのステージで呼べる駒のいる軸）
            Chapter1.Begin(0, rule);
            var open11 = Chapter1.OpenAxesOf(Map11Session.State!)!;
            Chapter1.End();
            string Axes(string stage) => string.Join("・", UnitTags.Axes.Where(a => roster.PoolFor(stage).Any(d => MapDraw.AxesOf(d).Contains(a))));
            write($"  §1 呼べる駒のいる軸: 1-1 {string.Join("・", open11)}（始めの 6 枚を持って）／ 1-2 までの表 {Axes("1-2")} ／ 1-3 までの表 {Axes("1-3")}");
            // 頭の札の顔の段
            Chapter1.Begin(0, rule);
            var groups = ch.Select(s => Chapter1.JoinGroups(s)).ToList();
            Chapter1.End();
            for (int i = 0; i < 3; i++)
                write($"  §1 {ch[i].Id} の頭の札の顔: {string.Join(" ／ ", groups[i].Select(g => $"{g.Heading ?? "（魔族・魔物の段）"} {g.Units.Count} 体: {string.Join("・", g.Units.Select(d => d.Name))}"))}");
            bool faces = groups[0].Count == 1 && groups[0][0].Units.Count == 5
                         && groups[1].Count == 2 && groups[1][0].Heading is null && groups[1][0].Units.Any(d => d.Id == utsu.Id) && groups[1][0].Units.Any(d => d.Id == wata.Id)
                         && groups[1][1].Units.Count == 3 && groups[2].Count == 1 && groups[2][0].Units.Count == 8 && groups[2][0].Units.Any(d => d.Id == hagi.Id);
            Req(faces, "§1 頭の札の顔: 1-1 は 5 体・1-2 はウツ ／ ワタが見出しの無い段（流れ着いた者は 3 体のまま）・1-3 は 8 体（ハギ入り）");
        }

        if (!full) return ok;
        int crashed = 0;
        try
        {
            write("");
            write("  §2-3 ハギ抜きの 1-1: 第474期の通しを第475期の表（1-1 で呼べるのは 5 体・ハギなし）と第474期の表（6 体・ハギあり）で");
            // 候補の先頭 2 枚にハギが入る seed の数（器具は候補の先頭から呼ぶ）
            MapStage s474 = Chapter1.StagesFor(ChapterRule.V474)[0];
            int hagiLead = Enumerable.Range(0, 100).Count(c =>
            {
                var st = new Map11State(Chapter1.StageSeed(c, 0, 0), s474.Time, s474.Portal, draft: true, stage: s474);
                return st.Offers.Take(1).Any(o => o.Id == hagi.Id);
            });
            write($"  第474期の表で、始めの候補の先頭がハギだった seed: {hagiLead} ／ 100");
            foreach (bool teamB in new[] { false, true })
            {
                Chapter1AsymSmoke.Routes(write, ch[0], "第475期の表（ハギなし・5 体）", teamB);
                Chapter1AsymSmoke.Routes(write, s474, "（比べる相手）第474期の表（ハギあり・6 体）", teamB);
            }
        }
        catch (Exception ex) { crashed++; write($"  × 通し: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        Req(crashed == 0, "器具（ハギ抜きの 1-1 の通し × 100 seed）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }
}
