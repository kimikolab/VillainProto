using BattleCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================================
// 第471期の門 `--chapter1-stage11-smoke`（指示書 design/PHASE471_STAGE11_SPEC.md §4・画面なし）
//
//   (a) 始めの 6 枚が リィカ・ムグ・ドルガ・ガン・リリ・ムド。ネルは 1-1 で呼べる・ガンは呼ぶ候補に出ない
//   (b) 1 章の 1-1 の敵: 北 物見の兵 → 盾の列 ／ 南 徴募兵 → 騎士の小隊 ／ 湧き 徴募兵・傭兵崩れ。波ルールを持つ駒が 1 体もいない。倍率 ×0.6
//       （南の奥は指示書では弓隊——`--foe-weight` で線に届かず騎士の小隊に替えた・報告 §2）
//   (c) 単発の 1-1・第461〜470期の 1 章は今のまま
//   (d) 棚卸しの表（`Roster33Audit`）が 33 行で出る（写しを 1 本足して回すと数が動く）
// `--chapter1-stage11-quick` は (d) を省く。**規則は1つも持たない。ここには Godot の型を1つも使わない。**
// =====================================================================================

public static class Chapter1Stage11Smoke
{
    private static readonly string[] Start6 = { "墓守リィカ", "胞子体ムグ", "のろまの巨兵ドルガ", "鬨の号令ガン", "施しのリリ", "泥人形ムド" };

    public static bool Run(Action<string> write, string demoDir, bool audit = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        ChapterRule rule = ChapterRule.V471;
        var ch = Chapter1.StagesFor(rule);
        write($"  1 章の規則 {rule.Name}・遊ぶ 1 章 {Chapter1.Default.Name}・加入の表 {ch[0].Roster.Name}");

        // ---- (a) 始めの 6 枚 ----
        {
            UnitDef gan = RosterRule.Find("鬨の号令ガン"), nel = RosterRule.Find("呪詛官ネル");
            int same = 0, ganOffered = 0, nelLeft = 0;
            for (int c = 0; c < 200; c++)
            {
                Chapter1.Begin(c, rule);
                Map11State st = Map11Session.State!;
                if (st.Bench.Select(u => u.Def.Name).SequenceEqual(Start6) && st.Bench.All(u => st.StarOf(u.Def.Id).Star == 1)) same++;
                if (st.RosterLeft.Any(d => d.Id == nel.Id)) nelLeft++;
                for (int k = 0; k < 12; k++)
                {
                    if (st.Offers.Any(o => o.Id == gan.Id)) ganOffered++;
                    var (d, _) = st.ProbeOffer();
                    if (d?.Id == gan.Id) ganOffered++;
                }
            }
            Chapter1.End();
            var pool11 = ch[0].Roster.PoolFor("1-1").Select(d => d.Name).ToList();
            bool okA = same == 200 && nelLeft == 200 && ganOffered == 0 && pool11.Contains(nel.Name) && !pool11.Contains(gan.Name)
                       && ch.All(s => !s.Roster.Allows(s.Id, gan));
            write($"  (a) 1 章の seed 0〜199 の 1-1 の始め: {string.Join("・", Start6)}（☆1）{same} ／ 200・ネルが「呼べる駒」にいる {nelLeft} ／ 200・ガンが呼ぶ候補に出た {ganOffered} 回（1 配りにつき候補 ＋ 列の 12 枚）"
                  + $"・1-1 で呼べる 6 体 {string.Join("・", pool11)}");
            Req(okA, "(a) 始めの 6 枚が リィカ・ムグ・ドルガ・ガン・リリ・ムド（☆1）。ネルは 1-1 で呼べる・ガンは呼ぶ候補に出ない（1-2 ／ 1-3 でも）");
        }

        // ---- (b) 1 章の 1-1 の敵 ----
        {
            MapStage s = ch[0];
            string[] want = { EnemyCatalog.Stages[0].Name, Band("band_shields"), Band("band_levy"), Band("band_knights") };
            string[] got = { s.Lanes[0][0].Name, s.Lanes[0][1].Name, s.Lanes[1][0].Name, s.Lanes[1][1].Name };
            string[] spawnWant = { Band("band_levy"), Band("band_sellswords") };
            string[] spawnGot = { s.Spawn(0).Name, s.Spawn(1).Name };
            var formations = s.Lanes.SelectMany(l => l).Select(n => (n.Name, n.Enemy)).Concat(new[] { (s.Spawn(0).Name, s.Spawn(0).Enemy), (s.Spawn(1).Name, s.Spawn(1).Enemy) }).ToList();
            var ruled = formations.Where(f => Ruled(f.Enemy)).Select(f => f.Name).ToList();
            bool okB = want.SequenceEqual(got) && spawnWant.SequenceEqual(spawnGot) && ruled.Count == 0 && s.FoeScale.FoePercent == 60 && s.Lanes.Length == 2 && s.Lanes.All(l => l.Count == 2);
            write($"  (b) 北 {got[0]} → {got[1]} ／ 南 {got[2]} → {got[3]} ／ 湧き {spawnGot[0]}・{spawnGot[1]}・倍率 ×{s.FoeScale.FoePercent / 100.0:F1}・波ルールを持つ部隊 {ruled.Count}（{(ruled.Count == 0 ? "なし" : string.Join("・", ruled))}）");
            Req(okB, "(b) 1 章の 1-1: 北 物見の兵 → 盾の列 ／ 南 徴募兵 → 騎士の小隊 ／ 湧き 徴募兵・傭兵崩れ・波ルール（粛・渇き・軛・逆位・断罪）を持つ駒が 1 体もいない・×0.6");
        }

        // ---- (c) 単発・第461〜470期 ----
        {
            string Line(MapStage s) => $"{s.Lanes[0][0].Name}→{s.Lanes[0][1].Name} ／ {s.Lanes[1][0].Name}→{s.Lanes[1][1].Name} ／ 湧き {s.Spawn(0).Name}・{s.Spawn(1).Name}";
            string old = Line(MapStage.Stage11Ch), p470 = Line(MapStage.Stage11Ch470);
            bool single = ReferenceEquals(MapStage.Stage11.Lanes, Map11.Roads) && Line(MapStage.Stage11) == old;
            bool okC = single;
            var parts = new List<string>();
            foreach (var (key, r) in ChapterRule.Versions.Where(v => string.CompareOrdinal(v.Key, "V461") >= 0 && string.CompareOrdinal(v.Key, "V471") < 0))
            {
                MapStage s11 = Chapter1.StagesFor(r)[0];
                bool lanes = Line(s11) == (key == "V470" ? p470 : old);
                bool roster = !s11.Roster.On || s11.Roster.StartFor("1-1")!.Any(d => d.Name == "呪詛官ネル");
                okC &= lanes && roster;
                parts.Add($"{key} {(lanes && roster ? "今のまま" : "×")}");
            }
            write($"  (c) 単発の 1-1: {Line(MapStage.Stage11)}・{string.Join("・", parts)}（1-1 の敵と湧き・始めの 6 枚のネル）");
            Req(okC, "(c) 単発の 1-1・第461〜470期の 1 章は今のまま");
        }

        // ---- (d) 棚卸しの表 ----
        if (audit)
        {
            string repo = Path.GetFullPath(Path.Combine(demoDir, ".."));
            string plays = Path.Combine(repo, "design", "plays");
            var (_, rows) = Roster33Audit.Build(write, plays, ChapterRule.V470, bots: false, weights: false);
            // 写しを 1 本足す（ステージの終わりの写しを別のフォルダへ写す）——遊びの数が動くか
            string tmp = Path.Combine(repo, ".tmp", "p471", "smoke_plays");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            CopyDir(plays, tmp);
            PlayLog.Snap end = PlayLog.List(plays, "end").OrderBy(x => x.Path, StringComparer.Ordinal).First();
            string extra = Path.Combine(tmp, "_smoke_extra", Path.GetFileName(end.Path));
            Directory.CreateDirectory(Path.GetDirectoryName(extra)!);
            File.Copy(end.Path, extra);
            var (_, rows2) = Roster33Audit.Build(write, tmp, ChapterRule.V470, bots: false, weights: false);
            int obs1 = rows.Sum(r => r.PlaySquad + r.PlayBench + r.PlayNone) / Math.Max(1, rows.Count);
            int obs2 = rows2.Sum(r => r.PlaySquad + r.PlayBench + r.PlayNone) / Math.Max(1, rows2.Count);
            bool moved = obs2 == obs1 + 1 && rows.Zip(rows2).Any(p => p.First.PlaySquad != p.Second.PlaySquad || p.First.PlayBench != p.Second.PlayBench);
            string md = Path.Combine(repo, "design", "ROSTER33_AUDIT.md");
            int mdRows = File.Exists(md) ? File.ReadAllLines(md).Count(l => l.StartsWith("| ", StringComparison.Ordinal) && !l.StartsWith("| 駒 ", StringComparison.Ordinal) && !l.StartsWith("| 順 ", StringComparison.Ordinal) && l.Count(ch => ch == '|') >= 12) : -1;
            Directory.Delete(tmp, true);
            write($"  (d) 33 体 {rows.Count} 行・ステージの終わり {obs1} 本 → 写しを 1 本足すと {obs2} 本（隊 ／ 余りの数が動いた {moved}）・`design/ROSTER33_AUDIT.md` の駒の行 {mdRows}");
            Req(rows.Count == 33 && rows2.Count == 33 && moved && mdRows == 33, "(d) 棚卸しの表が 33 行で出る（写しを 1 本足して回すと数が動く）");
        }
        return ok;
    }

    private static string Band(string id) => EnemyCatalog.MapSquadOf(id).Name;

    /// <summary>波ルール（粛・渇き・軛・逆位・断罪）を持つ駒がいるか（`Map11.RuleLineOf` と同じ札）。</summary>
    private static bool Ruled(Formation f)
        => f.Occupied().Any(o => o.Def.Traits.Any(t => t is TraitId.Hush or TraitId.Drought or TraitId.Yoke or TraitId.Inversion or TraitId.Condemn));

    private static void CopyDir(string from, string to)
    {
        foreach (string dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(from, to));
        Directory.CreateDirectory(to);
        foreach (string f in Directory.EnumerateFiles(from, "*.json", SearchOption.AllDirectories))
            File.Copy(f, f.Replace(from, to), true);
    }
}
