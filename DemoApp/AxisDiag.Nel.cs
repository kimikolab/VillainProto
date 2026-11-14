using BattleCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// =====================================================================================
// 第473期 ネルの漏れの測り（`--nel-leak`・指示書 design/PHASE473_NEL_LEAK_SPEC.md §1-1 ／ §2）。報告だけ・盤は動かさない。
//
// 呪いの代表の隊（第472期の `Representative`）と、ポンの写しの呪いの隊（`FromPlays`・2 隊まで）を、
// 1 章（第471期）の 1-2 ／ 1-3 の雑兵の全部と勇者に、☆1 ／ ☆2 ／ ☆3 × seed 20 で当てる。☆の表を替えて並べる:
//   今（第464期の☆）／ Phase 0 の対照 3 つ（開戦の漏れなし ／ 呪うたびの漏れなし ／ 漏れなし・ネルの看板の数字を 0 にした☆）／ 3 版（NL-f ／ NL-s ／ NL-1）。
// 1 戦は `AxisDiag.Run`（台本の写しで読む・第472期と同じ数え方）。
// =====================================================================================

public static partial class AxisDiag
{
    /// <summary>ネルの看板の数字だけを替えた☆の表（Phase 0 の対照・この道具の中だけ）。</summary>
    private static StarRule NelOnly(string what, int?[] start, int?[] turn) => new()
    {
        On = true, Name = $"対照（{what}）", Percent = StarRule.V464.Percent, Need = StarRule.V464.Need, EnhancePrice = StarRule.V464.EnhancePrice,
        Signs = new Dictionary<string, StarSign>(StarRule.V464.Signs) { [UnitCatalog.Nel.Id] = new StarSign(what, new int?[] { null, null, null, null }) { LeakStart = start, LeakTurn = turn } },
    };

    private sealed record NelCase(string Key, string Label, StarRule Stars, bool Version);

    private static IReadOnlyList<NelCase> NelCases()
    {
        int?[] zero = { null, 0, 0, 0 }, none = { null, null, null, null };
        return new[]
        {
            new NelCase("now", "今（第464期の☆）", StarRule.V464, false),
            new NelCase("noStart", "開戦の漏れなし", NelOnly("開戦の漏れなし", zero, none), false),
            new NelCase("noTurn", "呪うたびの漏れなし", NelOnly("呪うたびの漏れなし", none, zero), false),
            new NelCase("noLeak", "漏れなし", NelOnly("漏れなし", zero, zero), false),
            new NelCase("NL-f", "NL-f（漏れの床 攻撃の半分）", StarRule.V473f, true),
            new NelCase("NL-s", "NL-s（漏れに☆の倍率）", StarRule.V473s, true),
            new NelCase("NL-1", "NL-1（☆1 は開戦の漏れなし）", StarRule.V4731, true),
        };
    }

    private sealed record NelCell(AxisSquad Q, int Star, NelCase C, string Stage, FoeWeight.Foe Foe, List<Fight> Fights);

    /// <summary>
    /// `--nel-leak [--quick] [--seeds=20] [--out=<path>]`。`--quick` は seed 2・☆1 だけ・代表の隊だけ（1 分以内）。
    /// 出力は既定で `design/weights/nel_leak.md`（quick は `.tmp/p473/nel_leak_quick.md`）。
    /// </summary>
    public static bool NelMain(Action<string> write, IReadOnlyList<string> args, string demoAppDir)
    {
        string? Opt(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?[(name.Length + 3)..];
        bool quick = args.Contains("--quick") || args.Contains("--nel-leak-quick");
        string repo = Path.GetFullPath(Path.Combine(demoAppDir, ".."));
        string root = Path.Combine(repo, "design", "plays");
        string outPath = Path.GetFullPath(Opt("out") ?? (quick ? Path.Combine(repo, ".tmp", "p473", "nel_leak_quick.md") : Path.Combine(repo, "design", "weights", "nel_leak.md")), repo);
        int seeds = Opt("seeds") is { } sd ? int.Parse(sd, CultureInfo.InvariantCulture) : quick ? 2 : 20;
        int[] stars = quick ? new[] { 1 } : new[] { 1, 2, 3 };
        ChapterRule rule = ChapterRule.V471;   // 敵は第471期の 1 章（1-2 ×0.9 ／ 1-3 ×1.6・勇者の回復なし）
        var t0 = DateTime.Now;

        var units = Roster33Audit.Units(RosterRule.V471);
        var squads = new List<AxisSquad> { Representative("呪い", units) };
        if (!quick)
        {
            var snaps = PlayLog.List(root).Where(s => s.Pack is not null).ToList();
            var plays = new[] { "1-1", "1-2", "1-3" }.SelectMany(id => FoeWeight.SquadsOf(snaps, id, root)).ToList();
            squads.AddRange(FromPlays("呪い", plays, 2));
        }
        var chapter = Chapter1.StagesFor(rule);
        var foes = new[] { (Id: "1-2", S: chapter[1]), (Id: "1-3", S: chapter[2]) }
            .Select(x => (x.Id, x.S, Foes: FoeWeight.FoesOf(x.S).Where(f => f.Kind != FoeWeight.Kind.Garrison).GroupBy(f => f.Name).Select(g => g.First()).ToList())).ToList();
        var cases = NelCases();
        write($"NEL_LEAK 隊 {squads.Count}・☆ {string.Join("/", stars)}・☆の表 {cases.Count}・seed {seeds}");

        var cells = new List<NelCell>();
        int fights = 0;
        foreach (AxisSquad q in squads)
            foreach (int star in stars)
                foreach (NelCase c in cases)
                    foreach (var (id, s, fs) in foes)
                        foreach (FoeWeight.Foe f in fs)
                        {
                            var list = new List<Fight>();
                            for (int seed = 0; seed < seeds; seed++)
                            {
                                list.Add(Run("呪い", FoeWeight.Players(q.Squad, star, c.Stars, relics: false), FoeWeight.Enemies(f, s), seed));
                                fights++;
                            }
                            cells.Add(new NelCell(q, star, c, id, f, list));
                        }
        write($"NEL_LEAK {fights} 戦（{(DateTime.Now - t0).TotalSeconds:F0} 秒）");

        var lines = NelLines(squads, stars, cases, cells, seeds, rule, quick);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllLines(outPath, lines, new System.Text.UTF8Encoding(false));
        foreach (string l in lines.Where(l => l.StartsWith("NEL_", StringComparison.Ordinal))) write(l);
        write($"NEL_LEAK 表 {outPath}（{(DateTime.Now - t0).TotalSeconds:F0} 秒）");
        return true;
    }

    private static bool HasNel(AxisSquad q) => q.Squad.Pieces.Any(p => p.Base.Id == UnitCatalog.Nel.Id);

    private static List<string> NelLines(List<AxisSquad> squads, int[] stars, IReadOnlyList<NelCase> cases, List<NelCell> cells, int seeds, ChapterRule rule, bool quick)
    {
        var L = new List<string>
        {
            "# 第473期 ネルの漏れの測り（`--nel-leak`・生成物・手で直さない）",
            "",
            $"- 敵: {rule.Name} の 1-2 ／ 1-3 の雑兵の全部（同じ名前の部隊は 1 つ）と勇者（1-2 門番 ／ 1-3 追う勇者）・seed 0〜{seeds - 1}・レリックなし・全員同じ☆{(quick ? "・**quick**" : "")}",
            "- 1 戦の読み方は第472期の `--axis-diag` と同じ（`design/weights/axis_diag.md` の頭の注意）。**味方の攻撃の下がり** ＝ ターン頭の写しの（素の攻撃 − 今の攻撃）の和 ÷ 素の攻撃の和（呼んだ駒を除く）",
            "- **弱体の量**は窓口（`BattleContext.Dull`）を通った量の 1 戦あたり: 開戦の漏れ（呪詛漏れ）／ 呪うたびの漏れ（呪いの漏れ）／ 味方へのそれ以外（泥散り・突き返し・誹りほか）",
            "",
            "## 隊",
            "",
        };
        foreach (AxisSquad q in squads) L.Add($"- {(q.Representative ? "代表" : q.Squad.Source)}{(HasNel(q) ? "" : "（**ネルがいない**——漏れの版は効かない）")}: {string.Join(" ／ ", q.How)}");

        IEnumerable<Fight> Of(AxisSquad q, int star, string key, Func<FoeWeight.Foe, bool> pick, string? stage = null)
            => cells.Where(c => c.Q == q && c.Star == star && c.C.Key == key && pick(c.Foe) && (stage is null || c.Stage == stage)).SelectMany(c => c.Fights);
        static bool G(FoeWeight.Foe f) => Grunt(f);
        static bool H(FoeWeight.Foe f) => f.Kind == FoeWeight.Kind.Hero;
        string Hero(AxisSquad q, int star, string key, string stage)
        {
            var l = Of(q, star, key, H, stage).ToList();
            return l.Count == 0 ? "—" : $"{l.Count(f => f.Won)}／{l.Count}";
        }
        int HeroWins(AxisSquad q, int star, string key, string stage) => Of(q, star, key, H, stage).Count(f => f.Won);

        // ---- Phase 0 ----
        L.Add("");
        L.Add("## 1. Phase 0 —— 味方の攻撃の下がりを経路に分ける（雑兵の全部）");
        L.Add("");
        L.Add("対照は**ネルの看板の数字だけ**を 0 にした☆（開戦の呪詛の漏れ −5 ／ 呪うたびの隣への漏れ −2）。「ネルの分」＝ 今 − 漏れなし、「開戦の分」＝ 今 − 開戦の漏れなし、「呪うたびの分」＝ 今 − 呪うたびの漏れなし（2 つの分の和は、重なりのぶんネルの分と一致しない）。");
        L.Add("");
        L.Add("| 隊 | ☆ | 下がり 今 | 開戦の漏れなし | 呪うたびの漏れなし | 漏れなし | ネルの分 | うち開戦 | うち呪うたび | 量 開戦の漏れ | 量 呪うたびの漏れ | 量 それ以外 |");
        L.Add("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (AxisSquad q in squads)
            foreach (int star in stars)
            {
                Agg now = Sum(Of(q, star, "now", G)), ns = Sum(Of(q, star, "noStart", G)), nt = Sum(Of(q, star, "noTurn", G)), nl = Sum(Of(q, star, "noLeak", G));
                var fs = Of(q, star, "now", G).ToList();
                double n = Math.Max(1, fs.Count);
                L.Add($"| {(q.Representative ? "代表" : q.Squad.Source)} | ☆{star} | {P(now.AllyDrop)} | {P(ns.AllyDrop)} | {P(nt.AllyDrop)} | {P(nl.AllyDrop)} | {Pt(now.AllyDrop - nl.AllyDrop)} | {Pt(now.AllyDrop - ns.AllyDrop)} | {Pt(now.AllyDrop - nt.AllyDrop)} | {F1(fs.Sum(f => f.LeakStart) / n)} | {F1(fs.Sum(f => f.LeakTurn) / n)} | {F1(fs.Sum(f => f.AllyDullOther) / n)} |");
            }

        // ---- 誰が殴っているか ----
        L.Add("");
        L.Add("### 1-2. 漏れを軽くすると誰の殴りが減るか（敵の HP を減らした量・1 戦あたり・☆1）");
        L.Add("");
        L.Add("| 隊 | 相手 | 駒 | 今 | 開戦の漏れなし | 漏れなし | NL-f | NL-s | NL-1 |");
        L.Add("|---|---|---|--:|--:|--:|--:|--:|--:|");
        string[] showKeys = { "now", "noStart", "noLeak", "NL-f", "NL-s", "NL-1" };
        foreach (AxisSquad q in squads.Where(HasNel))
            foreach (var (label, pick, stage) in new (string, Func<FoeWeight.Foe, bool>, string?)[] { ("雑兵", G, null), ("1-3 勇者", H, "1-3") })
                foreach (FoeWeight.Piece pc in q.Squad.Pieces.OrderBy(x => x.Slot))
                {
                    string Cell(string key)
                    {
                        var l = Of(q, 1, key, pick, stage).ToList();
                        return l.Count == 0 ? "—" : F1(l.Sum(f => f.DealBy.GetValueOrDefault(pc.Base.Id)) / l.Count);
                    }
                    L.Add($"| {(q.Representative ? "代表" : q.Squad.Source)} | {label} | {SeatName(pc.Slot)} {pc.Base.Name} | {string.Join(" | ", showKeys.Select(Cell))} |");
                }

        // ---- 3 版 ----
        L.Add("");
        L.Add("## 2. 3 版（雑兵の全部 ／ 勇者）");
        L.Add("");
        L.Add("| 隊 | ☆ | 版 | 雑兵の勝ち | 終わりの HP | 決着T | 味方の下がり | 敵の下がり | 勇者 1-2 | 勇者 1-3 |");
        L.Add("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (AxisSquad q in squads)
            foreach (int star in stars)
                foreach (NelCase c in cases.Where(c => c.Key == "now" || c.Version))
                {
                    Agg a = Sum(Of(q, star, c.Key, G));
                    L.Add($"| {(q.Representative ? "代表" : q.Squad.Source)} | ☆{star} | {c.Label} | {P(a.Win)} | {P(a.Hp)} | {F1(a.Turns)} | {P(a.AllyDrop)} | {P(a.FoeDrop)} | {Hero(q, star, c.Key, "1-2")} | {Hero(q, star, c.Key, "1-3")} |");
                }

        // ---- 線 ----
        L.Add("");
        L.Add("## 3. 選ぶ線（指示書 §2・ネルのいる隊）");
        L.Add("");
        L.Add("(1) ☆1 の味方の下がり 50% 以下 ／ (2) 1-3 の勇者に ☆1 で 10／20 以上 ／ (3) 雑兵の勝ちが今より下がらず、終わりの HP が今より 1.0pt を超えて下がらない（☆1 ／ ☆2 ／ ☆3 とも）／ (4) ☆3 の味方の下がりが 0 にならない。");
        L.Add("");
        L.Add("| 版 | 隊 | (1) ☆1 の下がり | (2) 1-3 勇者 ☆1 | (3) 雑兵（☆ごとの勝ち ／ HP の差） | (4) ☆3 の下がり | 判定 |");
        L.Add("|---|---|--:|--:|---|--:|:-:|");
        var nelSquads = squads.Where(HasNel).ToList();
        foreach (NelCase c in cases.Where(c => c.Version))
        {
            bool all = true;
            foreach (AxisSquad q in nelSquads)
            {
                Agg s1 = Sum(Of(q, 1, c.Key, G));
                bool l1 = s1.AllyDrop <= 50.0;
                int hw = HeroWins(q, 1, c.Key, "1-3");
                int hn = Of(q, 1, c.Key, H, "1-3").Count();
                bool l2 = hn > 0 && hw * 20 >= 10 * hn;
                var parts = new List<string>();
                bool l3 = true;
                foreach (int star in stars)
                {
                    Agg v = Sum(Of(q, star, c.Key, G)), now = Sum(Of(q, star, "now", G));
                    bool ok = v.Win >= now.Win && v.Hp >= now.Hp - 1.0;
                    l3 &= ok;
                    parts.Add($"☆{star} {P(v.Win)}（今 {P(now.Win)}）／ {Pt(v.Hp - now.Hp)}{(ok ? "" : " ×")}");
                }
                bool l4 = !stars.Contains(3) || Sum(Of(q, 3, c.Key, G)).AllyDrop > 0.0;
                bool pass = l1 && l2 && l3 && l4;
                all &= pass;
                L.Add($"| {c.Label} | {(q.Representative ? "代表" : q.Squad.Source)} | {P(s1.AllyDrop)} {(l1 ? "○" : "×")} | {hw}／{hn} {(l2 ? "○" : "×")} | {string.Join("・", parts)} {(l3 ? "○" : "×")} | {(stars.Contains(3) ? P(Sum(Of(q, 3, c.Key, G)).AllyDrop) : "—")} {(l4 ? "○" : "×")} | {(pass ? "○" : "×")} |");
            }
            L.Add($"NEL_LINES {c.Key} {(all ? "PASS" : "FAIL")}");
        }
        L.Add("");
        L.Add("（`NEL_LINES` の行は門ではなく、報告が読むための印）");
        return L;
    }

    private static string Pt(double x) => double.IsNaN(x) ? "—" : $"{(x >= 0 ? "+" : "")}{x:F1}pt";
}
