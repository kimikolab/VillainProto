using BattleCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

// =====================================================================================
// 33 体の棚卸しの表（第471期 §2・`--roster33-audit`・指示書 design/PHASE471_STAGE11_SPEC.md）
//
// 体験版の 33 体（加入の表 `RosterRule.V471` の始めの 6 枚 ＋ 1-3 までに呼べる 27 体）を 1 枚の表にする（`design/ROSTER33_AUDIT.md`）:
//   駒・軸・役割（`UnitDef.Tags`）／ 遊びでの使われ方（ポンの写しのステージの終わりに 隊 ／ 余り ／ 持っていない）／
//   器具での使われ方（1 章の器具の通しで同じ数）／ 単体の重さ（写しの隊から 1 枚抜いたときの終わりの HP の差）／
//   マイナス（関係の表の損・両方の札 ＋ 位置を問わない札）と受け手の候補（`Presets.Compare` の同じ行に載る 33 体の駒）／ ☆1 での問題（過去の報告から手で写した）。
// 最後に「浮いている見込みの駒」を上から並べる（直す案は書かない）。
// **規則は1つも持たない・盤は1つも動かさない。ここには Godot の型を1つも使わない。**
// =====================================================================================

public static class Roster33Audit
{
    /// <summary>1 体ぶんの行。</summary>
    public sealed class Row
    {
        public required UnitDef Def { get; init; }
        public int PlaySquad, PlayBench, PlayNone;
        public int BotSquad, BotBench, BotNone;
        public List<double> Weight { get; } = new();
        public List<string> Minus { get; } = new();
        public List<string> Mates { get; } = new();
        public string Star1 = "";
        public int PlayOwned => PlaySquad + PlayBench;
        public int BotOwned => BotSquad + BotBench;
        public double? PlayBenchShare => PlayOwned == 0 ? null : (double)PlayBench / PlayOwned;
        public double? BotBenchShare => BotOwned == 0 ? null : (double)BotBench / BotOwned;
        public double? WeightAvg => Weight.Count == 0 ? null : Weight.Average();
    }

    /// <summary>☆1 で困ることが分かっている札（過去の報告から手で写した・出どころつき）。</summary>
    public static readonly IReadOnlyDictionary<string, string> Star1Notes = new Dictionary<string, string>
    {
        ["nel"] = "呪詛が味方に漏れ（開戦 −5）・隣へ滲む（毎ターン −2）——☆1 の味方（攻撃 2〜4）は開戦で攻撃 0（第470期 §4）",
        ["tou"] = "粉が隣へ漏れて帯電——雷杖の部隊が相手だと☆1 の感電の隊が味方の放電で自爆（第465期・第467期「味方の雷で n 枚」）",
        ["mudo"] = "泥が味方にもかかる（攻撃 −3・ネルと組むと味方の攻撃 0 が続く）（第470期 §4）",
        ["hagi"] = "刈り取りの線の上限が☆1 で 25%——勇者を 4 割まで削っても刈れない（第463・464期・☆で伸ばす設計）",
    };

    /// <summary>33 体（始めの 6 枚 ＋ 表の並び）。</summary>
    public static IReadOnlyList<UnitDef> Units(RosterRule roster)
        => roster.StartFor(roster.StartStage)!.Concat(roster.Joins.SelectMany(j => j.Units).Select(RosterRule.Find)).ToList();

    /// <summary>
    /// 表を作る。<paramref name="bots"/> が偽なら器具の通しを省く・<paramref name="weights"/> が偽なら単体の重さを省く（門 (d) の速い版）。
    /// 返すのは md の行と 33 行ぶんの集計。
    /// </summary>
    public static (List<string> Lines, List<Row> Rows) Build(Action<string> write, string playsRoot, ChapterRule botRule, int botSeeds = 100, bool bots = true, bool weights = true, int weightSeeds = 8, RosterRule? rosterRule = null)
    {
        RosterRule roster = rosterRule ?? RosterRule.V471;   // 第475期: `--roster=V475` で 35 体（既定は第471期の 33 体のまま）
        var units = Units(roster);
        var rows = units.Select(d => new Row { Def = d }).ToList();
        var byId = rows.ToDictionary(r => r.Def.Id);

        // ---- 遊びでの使われ方 ----
        var snaps = PlayLog.List(playsRoot).Where(s => s.Pack is not null).ToList();
        var ends = Observations(snaps);
        foreach (PlayLog.Snap s in ends)
        {
            var squad = s.Pack!.Squads.SelectMany(q => q).Select(u => u.Id).ToHashSet();
            var bench = s.Pack.Bench.Select(u => u.Id).ToHashSet();
            foreach (Row r in rows)
            {
                if (squad.Contains(r.Def.Id)) r.PlaySquad++;
                else if (bench.Contains(r.Def.Id)) r.PlayBench++;
                else r.PlayNone++;
            }
        }
        write($"ROSTER33 写し {snaps.Count} 本のうち、ステージの終わり {ends.Count} 本（_end ＋ 前のステージの _end が無い通しの次の _start）");

        // ---- 器具での使われ方 ----
        int botEnds = 0;
        if (bots)
        {
            foreach (bool split in new[] { false, true })
                for (int c = 0; c < botSeeds; c++)
                {
                    foreach (var (sq, bn) in BotEnds(botRule, c, split))
                    {
                        botEnds++;
                        foreach (Row r in rows)
                        {
                            if (sq.Contains(r.Def.Id)) r.BotSquad++;
                            else if (bn.Contains(r.Def.Id)) r.BotBench++;
                            else r.BotNone++;
                        }
                    }
                }
            write($"ROSTER33 器具 {botRule.Key} × 2 方針 × {botSeeds} seed・勝ったステージの終わり {botEnds} 回");
        }

        // ---- 単体の重さ ----
        int weightSquads = 0;
        if (weights)
        {
            foreach (PlayLog.Snap s in snaps.Where(x => x.Kind is "first" or "start")
                         .GroupBy(x => (Path.GetDirectoryName(x.Path), x.ChapterSeed, x.Stage))
                         .Select(g => g.OrderBy(x => x.Kind == "first" ? 0 : 1).First()))
            {
                ChapterRule rule = ChapterRule.ByKey(s.Rule) ?? ChapterRule.V471;   // 第473期: 版の無い写しは第471期で（遊ぶ 1 章を替えても表が動かない）
                int index = Math.Clamp(s.StageIndex, 0, 2);
                MapStage stage = FoeWeight.StageAt(index, rule.ScaleOf(index), rule.Carry ? rule.First : null);
                StarRule stars = rule.Stars is { On: true } sr ? sr : StarRule.V464;
                var foes = FoeWeight.FoesOf(stage).Where(f => f.Kind != FoeWeight.Kind.Hero).ToList();
                for (int i = 0; i < s.Pack!.Squads.Count; i++)
                {
                    var us = s.Pack.Squads[i];
                    if (us.Count < 2) continue;
                    var pieces = us.Select(u => new FoeWeight.Piece(PlayLog.BaseDef(u), Math.Max(1, u.Star), u.Slot, PlayLog.RelicOf(u.Relic))).ToList();
                    var full = new FoeWeight.Squad("w", i + 1, pieces);
                    double hpFull = MeanHp(full, foes, stage, stars, weightSeeds);
                    weightSquads++;
                    foreach (FoeWeight.Piece p in pieces)
                    {
                        if (!byId.TryGetValue(p.Base.Id, out Row? r)) continue;
                        var without = new FoeWeight.Squad("w", i + 1, pieces.Where(x => x != p).ToList());
                        r.Weight.Add(hpFull - MeanHp(without, foes, stage, stars, weightSeeds));
                    }
                }
            }
            write($"ROSTER33 単体の重さ: 写しの隊 {weightSquads} 隊 × 1 枚ずつ抜く × その盤の敵部隊（勇者を除く）× seed {weightSeeds}");
        }

        // ---- マイナスと受け手の候補 ----
        var ids33 = rows.Select(r => r.Def.Id).ToHashSet();
        var compareRows = Presets.Compare.Select(c => c.F.Occupied().Select(o => o.Def.Id).ToHashSet()).ToList();
        var globalWhy = Map11Relations.Unresolved;
        foreach (Row r in rows)
        {
            foreach (TraitId t in r.Def.Traits)
            {
                if (Map11Relations.SignOf(t) is Map11Relations.LinkSign sign && sign != Map11Relations.LinkSign.Gain)
                    r.Minus.Add($"{Map11Relations.WordOf(t)}（{Map11Relations.LabelOf(sign)}）");
                // 位置を問わない札のうち、味方に「漏れる」もの（呪詛の漏れ）だけをマイナスに数える（配る・下げる被ダメージは得）。
                else if (globalWhy.TryGetValue(t, out string? why) && why.Contains(Map11Relations.GlobalMark, StringComparison.Ordinal) && why.Contains("漏れ", StringComparison.Ordinal))
                    r.Minus.Add($"{why.Split('（')[0]}（損・位置を問わない）");
            }
            var mates = compareRows.Where(x => x.Contains(r.Def.Id)).SelectMany(x => x).Where(id => id != r.Def.Id && ids33.Contains(id)).Distinct();
            r.Mates.AddRange(mates.Select(id => byId[id].Def.Name).OrderBy(n => n, StringComparer.Ordinal));
            r.Star1 = Star1Notes.TryGetValue(r.Def.Id, out string? note) ? note : "";
        }

        return (Lines(rows, ends.Count, botEnds, bots, weights, weightSquads, botRule, weightSeeds, roster), rows);
    }

    /// <summary>ステージの終わり: `_end` の写しと、同じ通し（フォルダ ・1 章の seed）に前のステージの `_end` が無いときの次のステージの `_start`（持ち越しそのもの）。</summary>
    public static List<PlayLog.Snap> Observations(IReadOnlyList<PlayLog.Snap> snaps)
    {
        var ends = snaps.Where(s => s.Kind == "end").ToList();
        var endKeys = ends.Select(s => (Path.GetDirectoryName(s.Path), s.ChapterSeed, s.StageIndex)).ToHashSet();
        var starts = snaps.Where(s => s.Kind == "start" && s.StageIndex >= 1 && !endKeys.Contains((Path.GetDirectoryName(s.Path), s.ChapterSeed, s.StageIndex - 1)));
        return ends.Concat(starts).OrderBy(s => s.Path, StringComparer.Ordinal).ToList();
    }

    private static double MeanHp(FoeWeight.Squad q, List<FoeWeight.Foe> foes, MapStage stage, StarRule stars, int seeds)
    {
        double sum = 0;
        int n = 0;
        foreach (FoeWeight.Foe f in foes)
            for (int seed = 0; seed < seeds; seed++)
            {
                sum += FoeWeight.Run(FoeWeight.Players(q, 0, stars), FoeWeight.Enemies(f, stage), seed).Hp;
                n++;
            }
        return n == 0 ? 0 : sum / n;   // `Fight.Hp` は始めから %（負けは 0）
    }

    /// <summary>器具の 1 章の通し 1 本の、勝ったステージの終わりごとの（隊の Id ・手札の Id）。方針は第470期の §3 と同じ 2 つ（1 隊残す ／ 分担＋戻り番・1-3 は逃げる）。</summary>
    private static IEnumerable<(HashSet<string> Squad, HashSet<string> Bench)> BotEnds(ChapterRule rule, int c, bool split)
    {
        var stages = Chapter1.StagesFor(rule);
        Map11State.CarryPack? pack = null;
        var result = new List<(HashSet<string>, HashSet<string>)>();
        for (int i = 0; i < 3; i++)
        {
            bool won = false;
            for (int a = 0; a <= 20 && !won; a++)
            {
                MapStage stage = stages[i];
                var st = new Map11State(Chapter1.StageSeed(c, i, a), stage.Time, stage.Portal, draft: true, stage: stage);
                if (pack is not null) st.ApplyCarry(pack);
                var cards = stage.Invest.CardsFor(Chapter1.StageSeed(c, i, 0), Chapter1.HeldOf(stage, pack?.Invest ?? Array.Empty<(string, string)>()), Chapter1.OpenAxesOf(st));
                if (cards.Count > 0) st.Invest(cards[InvestRule.BotPick(st, cards)]);
                Map12ChangeSmoke.Greedy(st.Seed, st.Stage, true, split, false, state: st, siegeRest: true,
                    heroPlan: split && i == 2 ? Map12ChangeSmoke.HeroPlan.Flee : Map12ChangeSmoke.HeroPlan.None);
                won = st.Won;
                if (!won) continue;
                pack = st.TakeCarry();
                result.Add((pack.Squads.SelectMany(q => q).Select(u => u.Def.Id).ToHashSet(), pack.Bench.Select(u => u.Def.Id).ToHashSet()));
            }
            if (!won) break;
        }
        return result;
    }

    private static string Pct(double? x) => x is { } v ? $"{100 * v:F0}%" : "—";

    private static List<string> Lines(List<Row> rows, int playEnds, int botEnds, bool bots, bool weights, int weightSquads, ChapterRule botRule, int weightSeeds, RosterRule roster)
    {
        // 第475期: 第471期の表（33 体）なら頭の文は今のまま。ほかの表は体数と表の名前を出す。
        bool v471 = ReferenceEquals(roster, RosterRule.V471);
        int n = rows.Count;
        string key = ReferenceEquals(roster, RosterRule.V475) ? "V475" : ReferenceEquals(roster, RosterRule.V469) ? "V469" : "V471";
        var L = new List<string>
        {
            $"# {n} 体の棚卸し（体験版の加入の表）",
            "",
            v471 ? $"`--roster33-audit` の出力（{DateTime.Now:yyyy-MM-dd HH:mm}）。手で編集しない（次の実行で上書きされる）。指示書 `design/PHASE471_STAGE11_SPEC.md` §2。**調べるだけ——直す案は書かない。**"
                 : $"`--roster33-audit --roster={key}` の出力（{DateTime.Now:yyyy-MM-dd HH:mm}）。手で編集しない（次の実行で上書きされる）。表は第471期の棚卸し（`design/PHASE471_STAGE11_SPEC.md` §2）と同じ作り。**調べるだけ——直す案は書かない。**",
            "",
            $"- 駒: `RosterRule.{key}` の始めの 6 枚 ＋ 1-1 ／ 1-2 ／ 1-3 で呼べる {n - 6} 体（表の並び）。軸・役割は `UnitDef.Tags`（`docs/tags.md` と同じ表）",
            $"- 遊び: ポンの写し（`design/plays/` の全部・手で写した分も）の**ステージの終わり {playEnds} 本**——`_end` の写しと、同じ通しに前のステージの `_end` が無いときの次のステージの `_start`（持ち越しそのもの）。隊 ／ 余り（手札）／ 持っていない の回数",
            bots ? $"- 器具: {botRule.Name} の 1 章の通し（1 隊残す ／ 分担＋戻り番・1-3 は逃げる × seed 0〜99・負ければやり直し 20 回まで）の**勝ったステージの終わり {botEnds} 回**" : "- 器具: 省いた",
            weights ? $"- 単体の重さ: ポンの写しの隊（`_first`、無ければ `_start`・{weightSquads} 隊）から**その 1 枚を抜いた隊**と元の隊を、その盤の敵部隊（勇者を除く）× seed 0〜{weightSeeds - 1} に当てた終わりの HP（隊の最大 HP に対する %）の平均の差（pt・正なら入れたほうが残る）。写しの隊に出てこない駒は「—」" : "- 単体の重さ: 省いた",
            "  - 戦闘中に味方の最大 HP を変える駒（ヴェルの魂を分ける＝隣の最大 HP が半分）は、終わりの HP の割合が下がって見える（負に出やすい）。隊の数が 1〜3 の駒は揺れが大きい",
            $"- マイナス: 関係の表（`Map11Relations`）の損 ／ 両方の札と、位置を問わない札。**受け手の候補** は `Presets.Compare`（`compare` の行）でその駒と同じ行に載る {n} 体の駒——**マイナスを得に変えるかは機械では数えていない（手で確認）**",
            "- ☆1 での問題: 第463〜470期の報告から手で写した（`Roster33Audit.Star1Notes`）",
            "",
            "| 駒 | 軸 | 役割 | 遊び 隊 ／ 余り ／ 無し | 余りの割合 | 器具 隊 ／ 余り ／ 無し | 余りの割合 | 単体の重さ（pt・隊の数） | マイナス | 受け手の候補（compare の同じ行） | ☆1 での問題 |",
            "|---|---|---|---|--:|---|--:|---|---|---|---|",
        };
        foreach (Row r in rows)
        {
            string axes = string.Join("・", r.Def.Tags.Where(t => UnitTags.Axes.Contains(t)));
            string role = string.Join("・", r.Def.Tags.Where(t => UnitTags.Roles.Contains(t)));
            string w = r.WeightAvg is { } wv ? $"{wv.ToString("+0.0;−0.0;0.0", CultureInfo.InvariantCulture)}（{r.Weight.Count}）" : "—";
            string minus = r.Minus.Count == 0 ? "なし" : string.Join("・", r.Minus.Distinct());
            string mates = r.Mates.Count == 0 ? "0 体" : $"{r.Mates.Count} 体（{string.Join("・", r.Mates)}）";
            L.Add($"| {r.Def.Name} | {axes} | {role} | {r.PlaySquad} ／ {r.PlayBench} ／ {r.PlayNone} | {Pct(r.PlayBenchShare)} | "
                  + (bots ? $"{r.BotSquad} ／ {r.BotBench} ／ {r.BotNone} | {Pct(r.BotBenchShare)}" : "— | —")
                  + $" | {w} | {minus} | {mates} | {(r.Star1.Length == 0 ? "" : r.Star1)} |");
        }
        L.Add("");
        L.Add("## 浮いている見込みの駒（上から・直す案は書かない）");
        L.Add("");
        L.Add("点 ＝ 余りの割合（遊び）＋ 余りの割合（器具）＋ 受け手の候補が 0〜1 体なら 0.5 ＋ 単体の重さが +2pt 未満なら 0.5（「—」は 0.25）。持っていた回数が遊び ・器具とも 0 の駒は点を付けない（使われ方が分からない）。");
        L.Add("");
        L.Add("| 順 | 駒 | 点 | 理由 |");
        L.Add("|--:|---|--:|---|");
        int k = 0;
        foreach (var (r, score, why) in Floating(rows).Take(8))
            L.Add($"| {++k} | {r.Def.Name} | {score:F2} | {why} |");
        return L;
    }

    /// <summary>浮いている見込みの順（点の高い順）。</summary>
    public static IEnumerable<(Row Row, double Score, string Why)> Floating(IEnumerable<Row> rows)
    {
        foreach (Row r in rows.Where(r => r.PlayOwned + r.BotOwned > 0)
                     .Select(r => r)
                     .OrderByDescending(Score).ThenBy(r => r.Def.Name, StringComparer.Ordinal))
        {
            var why = new List<string>();
            if (r.PlayBenchShare is { } pb) why.Add($"遊びで余り {Pct(pb)}（{r.PlayBench} ／ {r.PlayOwned}）");
            else why.Add("遊びで持っていたことが無い");
            if (r.BotBenchShare is { } bb) why.Add($"器具で余り {Pct(bb)}（{r.BotBench} ／ {r.BotOwned}）");
            why.Add($"受け手の候補 {r.Mates.Count} 体");
            why.Add(r.WeightAvg is { } w ? $"単体の重さ {w.ToString("+0.0;−0.0;0.0", CultureInfo.InvariantCulture)}pt" : "写しの隊に出てこない");
            if (r.Star1.Length > 0) why.Add("☆1 での問題あり");
            yield return (r, Score(r), string.Join("・", why));
        }
    }

    private static double Score(Row r)
        => (r.PlayBenchShare ?? 0) + (r.BotBenchShare ?? 0) + (r.Mates.Count <= 1 ? 0.5 : 0) + (r.WeightAvg is { } w ? (w < 2 ? 0.5 : 0) : 0.25);

    /// <summary>
    /// 口 `--roster33-audit [--plays=<dir>] [--out=<path>] [--rule=V470] [--seeds=100] [--no-bots] [--no-weights] [--roster=V475]`。
    /// 第475期: `--roster=V475` は体験版の 35 体の表（出力の既定は `design/ROSTER35_AUDIT.md`・器具の版は `--rule=V475` で合わせる）。
    /// </summary>
    public static bool Main(Action<string> write, IReadOnlyList<string> args, string demoAppDir)
    {
        string? Opt(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?[(name.Length + 3)..];
        string repo = Path.GetFullPath(Path.Combine(demoAppDir, ".."));
        string root = Path.GetFullPath(Opt("plays") ?? Path.Combine(repo, "design", "plays"), repo);
        RosterRule roster = Opt("roster") is { } rok ? RosterRule.ByKey(rok) ?? throw new ArgumentException($"加入の表が無い: {rok}") : RosterRule.V471;
        int want = Units(roster).Count;
        string outPath = Path.GetFullPath(Opt("out") ?? Path.Combine(repo, "design", $"ROSTER{want}_AUDIT.md"), repo);
        ChapterRule rule = Opt("rule") is { } rk ? ChapterRule.ByKey(rk) ?? throw new ArgumentException($"版が無い: {rk}") : ChapterRule.V470;
        int seeds = Opt("seeds") is { } sd ? int.Parse(sd, CultureInfo.InvariantCulture) : 100;
        var (lines, rows) = Build(write, root, rule, seeds, bots: !args.Contains("--no-bots"), weights: !args.Contains("--no-weights"), rosterRule: roster);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        write($"ROSTER33 {rows.Count} 行 → {outPath}");
        return rows.Count == want;
    }
}
