using BattleCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

// =====================================================================================
// 敵の重さの表（第466期 §3・`--foe-weight`・指示書 design/PHASE466_TUNING_TOOLS_SPEC.md）
//
// **代表の隊（遊びの写しの隊）× そのステージの敵部隊 1 つずつ × 倍率 × ☆** を単発の戦闘で回して表にする。
// 駒は作戦マップと同じ式で立てる（`Map11State.MakeCarried`・敵は `Map11.EnemyScale` ＋ 勇者の回復の写し `MapFoeScale.MendHero`）。
// 勇者は「1 隊ずつ順に当てる」の通しも出す（勇者の HP は持ち越す）。狙いの帯は `design/weights/BANDS.md` の表を読む（ポンが直す）。
// 第468期 §2: 本陣の守りの後ろに立つ勇者（1-2 の門番）は「守り → 勇者」を同じ隊で続けて戦う通し（`SiegeChain`）で勇者の帯を読む。
// 帯の上限は「なし」と書ける（その帯では「軽い」を外れとしない・1 章の道の部隊・湧き・予備隊・大群）。
// 出すのは `design/weights/<日付>_<ステージ>.md`（`docs/` には載せない）。**規則は1つも持たない・盤は1つも動かさない。**
// **ここには Godot の型を1つも使わない。**
// =====================================================================================

public static class FoeWeight
{
    public enum Kind { Road, Outpost, Horde, Spawn, Reserve, Garrison, Hero }

    public static string KindName(Kind k) => k switch
    {
        Kind.Road => "道の部隊", Kind.Outpost => "中間拠点の部隊", Kind.Horde => "大群の 1 部隊", Kind.Spawn => "湧き",
        Kind.Reserve => "予備隊", Kind.Garrison => "本陣の守り", _ => "勇者",
    };

    /// <summary>敵部隊 1 つ（盤に出るもの）。</summary>
    public sealed record Foe(Kind Kind, string Name, Formation Enemy);

    /// <summary>駒 1 枚（☆を掛ける前の定義・写しの☆・席・札）。</summary>
    public sealed record Piece(UnitDef Base, int Star, int Slot, TraitId? Relic);

    /// <summary>代表の隊 1 つ（写しの名前・隊の番号（1 から））。</summary>
    public sealed record Squad(string Source, int No, IReadOnlyList<Piece> Pieces)
    {
        public string Name => $"{Source} 第{No}隊";
        /// <summary>軸（2 枚以上いる軸・多い順）。</summary>
        public string Axis
        {
            get
            {
                var axes = Pieces.SelectMany(p => p.Base.Tags.Where(t => UnitTags.Axes.Contains(t))).GroupBy(t => t)
                    .Where(g => g.Count() >= 2).OrderByDescending(g => g.Count()).ThenBy(g => Array.IndexOf(UnitTags.Axes, g.Key)).Select(g => $"{g.Key} {g.Count()}").ToList();
                return axes.Count == 0 ? "—" : string.Join("・", axes);
            }
        }
    }

    /// <summary>1 戦の結果（終わりの HP は隊の最大 HP に対する割合・負けは 0）。</summary>
    public readonly record struct Fight(bool Won, double Hp, int Lost, int Turns);

    // ---------------- 狙いの帯（design/weights/BANDS.md） ----------------

    /// <summary>勇者以外の帯（勝ち <c>MinWins</c> ／ 20 以上・終わりの HP の中央値が <c>HpLo</c>〜<c>HpHi</c>%）。</summary>
    public sealed record Band(string Key, int MinWins, double HpLo, double HpHi);
    /// <summary>勇者の帯（主力の 1 隊で落ちた seed が <c>OneLo</c>〜<c>OneHi</c> ／ 20・強い順に 2 隊までで落ちた seed が <c>TwoMin</c> ／ 20 以上）。</summary>
    public sealed record HeroBand(string Key, int OneLo, int OneHi, int TwoMin);

    /// <summary>帯の表（読めなければ指示書 §3-3 の仮の値）。</summary>
    public sealed class Bands
    {
        public Dictionary<string, Band> Foe { get; } = new()
        {
            ["road"] = new("road", 20, 70, double.PositiveInfinity),     // 第468期: 上限なし（ポン 2026-11-07「1 章の雑魚は軸がそろえば軽くてよい」）
            ["outpost"] = new("outpost", 18, 55, 85),
            ["horde"] = new("horde", 20, 80, double.PositiveInfinity),   // 第468期: 上限なし
            ["garrison"] = new("garrison", 15, 40, 75),
        };
        public Dictionary<string, HeroBand> Hero { get; } = new()
        {
            ["hero_gate"] = new("hero_gate", 0, 4, 16),
            ["hero_chase"] = new("hero_chase", 7, 13, 16),
        };
        public string Source { get; set; } = "指示書 §3-3 の仮の値（BANDS.md が読めなかった）";

        public static string KeyOf(Kind k) => k switch
        {
            Kind.Outpost => "outpost", Kind.Horde => "horde", Kind.Garrison => "garrison", Kind.Hero => "hero", _ => "road",
        };

        /// <summary>
        /// `BANDS.md` を読む。行の 1 列目が `` `road` `` のようにキーを持つ表の行だけを読み、ほかの行は飛ばす
        /// （勇者以外: キー | 相手 | 勝ち（以上・／20） | HP 下限 | HP 上限 ／ 勇者: キー | 相手 | 1 隊で落ちる（下限） | 1 隊で落ちる（上限） | 2 隊までで落ちる（下限））。
        /// </summary>
        public static Bands Load(string path)
        {
            var b = new Bands();
            if (!File.Exists(path)) return b;
            int read = 0;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (!line.StartsWith("|")) continue;
                var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                if (cells.Length < 5 || !cells[0].StartsWith("`")) continue;
                string key = cells[0].Trim('`');
                // 第468期: 「なし」「—」は上限なし（その帯では「軽い」を外れとしない）。
                static double D(string s) => s.Trim() is "なし" or "—" or "-" ? double.PositiveInfinity
                    : double.Parse(s.Replace("%", "").Replace("／20", "").Trim(), CultureInfo.InvariantCulture);
                if (b.Foe.ContainsKey(key)) { b.Foe[key] = new Band(key, (int)D(cells[2]), D(cells[3]), D(cells[4])); read++; }
                else if (b.Hero.ContainsKey(key) && cells.Length >= 5) { b.Hero[key] = new HeroBand(key, (int)D(cells[2]), (int)D(cells[3]), (int)D(cells[4])); read++; }
            }
            b.Source = $"{Path.GetFileName(path)}（{read} 行を読んだ）";
            return b;
        }
    }

    /// <summary>帯の判定（勝ちと HP の中央値）。勝ちが足りないか HP が下限より下なら「重い」・上限より上なら「軽い」。</summary>
    public static string Verdict(Band b, int wins, int n, double medHp)
    {
        if (wins * 20 < b.MinWins * n || medHp < b.HpLo) return "重い";
        if (medHp > b.HpHi) return "軽い";
        return "○";
    }

    /// <summary>勇者の帯の判定（主力の 1 隊で落ちた数・2 隊までで落ちた数）。</summary>
    public static string Verdict(HeroBand b, int one, int two, int n)
    {
        if (one * 20 > b.OneHi * n) return "軽い";
        if (one * 20 < b.OneLo * n || two * 20 < b.TwoMin * n) return "重い";
        return "○";
    }

    // ---------------- 敵部隊 ----------------

    /// <summary>
    /// そのステージの盤に出る敵部隊（道の部隊・中間拠点の部隊・大群の 1 部隊・湧き・予備隊・本陣の守り・勇者）。
    /// 種類は区画の最初のマスで決める（中間拠点のマスに立つ区画のうち経路の奥のもの ＝ 中間拠点の部隊・敵の拠点で動かない ＝ 本陣の守り・重なって動かない ＝ 大群）。
    /// 中間拠点のマスに重なって立つ手前の区画（1-2 の中の道の「第一波 ／ 物見の兵」）は道の部隊。同じ種類・同じ名前は 1 つにまとめる。
    /// </summary>
    public static List<Foe> FoesOf(MapStage s)
    {
        var st = new Map11State(0, s.Time, s.Portal, draft: true, stage: s);
        var list = new List<Foe>();
        void Add(Kind k, string name, Formation f) { if (!list.Any(x => x.Kind == k && x.Name == name)) list.Add(new Foe(k, name, f)); }
        foreach (Map11State.Node[] road in st.Nodes)
            foreach (Map11State.Node n in road)
            {
                bool deepest = !road.Any(o => o != n && o.Pos == n.Pos && !o.Def.Hold && o.Def.Index > n.Def.Index);
                Kind k = n.Def.Hold ? Kind.Horde
                       : st.Graph.IsOutpost(n.Pos) && deepest ? Kind.Outpost
                       : n.Def.Stationary && n.Pos == st.Graph.Portal ? Kind.Garrison
                       : Kind.Road;
                Add(k, n.Def.Name, n.Def.Enemy);
            }
        for (int r = 0; r < s.LaneCount; r++) { var sp = s.Spawn(r); Add(Kind.Spawn, sp.Name, sp.Enemy); }
        if (s.Reserve.Enemy is { } re) Add(Kind.Reserve, s.Reserve.Name, re);
        if (s.Hero.Unit is { } hu) Add(Kind.Hero, hu.Name, Formation.Build(center: hu));
        return list.OrderBy(f => f.Kind).ToList();
    }

    // ---------------- 戦闘 ----------------

    /// <summary>☆<paramref name="star"/>（0 ＝ 写しのまま）の駒の定義。☆3 は今の定義そのもの。</summary>
    public static UnitDef DefOf(Piece p, int star, StarRule stars)
    {
        int s = star == 0 ? p.Star : star;
        return s >= stars.Max || !stars.On ? p.Base : stars.Def(p.Base, s);
    }

    /// <summary>隊を立てる（作戦マップと同じ式・席の順）。</summary>
    public static List<UnitState> Players(Squad q, int star, StarRule stars, bool relics = true)
        => q.Pieces.Select(p => Map11State.MakeCarried(new Map11State.CarryUnit(DefOf(p, star, stars), p.Slot, relics ? p.Relic : null)))
            .OrderBy(u => u.Slot).ToList();

    /// <summary>敵部隊を立てる（作戦マップの `FoeUnits` と同じ式・勇者は倍率の盤の回復の写し）。</summary>
    public static List<UnitState> Enemies(Foe f, MapStage s)
    {
        List<UnitState> us = BattleEngine.Materialize(f.Enemy, BattleContext.EnemyTeam, Map11.EnemyScale);
        return f.Kind == Kind.Hero ? s.FoeScale.MendHero(us) : us;
    }

    /// <summary>1 戦（敵は満タンから）。</summary>
    public static Fight Run(List<UnitState> players, List<UnitState> enemies, int seed)
    {
        int max = players.Sum(u => u.MaxHp);
        BattleResult r = BattleEngine.Run(players, enemies, seed, verbose: false);
        double hp = !r.PlayerWon || max == 0 ? 0 : 100.0 * players.Where(u => u.IsAlive).Sum(u => u.Hp) / max;
        return new Fight(r.PlayerWon, hp, players.Count(u => !u.IsAlive), r.Turns);
    }

    /// <summary>
    /// 勇者に隊を 1 つずつ順に当てる通し（勇者の HP は持ち越す・戦闘の境界は作戦マップと同じ `EngagementEngine.CrossBoundary`）。
    /// 返すのは倒した隊の番号（1 から・倒せなければ 0）。
    /// </summary>
    public static int HeroChain(IReadOnlyList<Squad> order, Foe hero, MapStage s, int star, StarRule stars, int seed, bool relics = true)
    {
        List<UnitState> h = Enemies(hero, s);
        for (int k = 0; k < order.Count; k++)
        {
            List<UnitState> p = Players(order[k], star, stars, relics);
            BattleEngine.Run(p, h, Map11.DeriveSeed(seed, k), verbose: false);
            var alive = h.Where(u => u.IsAlive).ToList();
            if (alive.Count == 0) return k + 1;
            h = EngagementEngine.CrossBoundary(alive);
        }
        return 0;
    }

    /// <summary>
    /// 本陣の守り → 勇者の通し（第468期 §2・ポン 2026-11-07「連戦で読む」）。<paramref name="line"/>（守り…・勇者の順）を隊 1 つずつ順に当てる。
    /// <b>勝った隊は、その戦闘の終わりの HP のまま次の相手に当たる</b>（作戦マップの本陣の連戦と同じく回復なし・境界は `EngagementEngine.CrossBoundary`）。
    /// 負けた隊はそこで終わり、残った敵の HP は次の隊へ持ち越す。返すのは勇者を倒した隊の番号（1 から・倒せなければ 0）。
    /// </summary>
    public static int SiegeChain(IReadOnlyList<Squad> order, IReadOnlyList<Foe> line, MapStage s, int star, StarRule stars, int seed, bool relics = true)
    {
        int at = 0;
        List<UnitState> e = Enemies(line[0], s);
        for (int k = 0; k < order.Count; k++)
        {
            List<UnitState> p = Players(order[k], star, stars, relics);
            for (int b = 0; ; b++)
            {
                BattleEngine.Run(p, e, Map11.DeriveSeed(seed, k * 16 + b), verbose: false);
                var aliveE = e.Where(u => u.IsAlive).ToList();
                if (aliveE.Count > 0) { e = EngagementEngine.CrossBoundary(aliveE); break; }
                if (line[at].Kind == Kind.Hero) return k + 1;
                at++;
                e = Enemies(line[at], s);
                var aliveP = p.Where(u => u.IsAlive).ToList();
                if (aliveP.Count == 0) break;
                p = EngagementEngine.CrossBoundary(aliveP);
            }
        }
        return 0;
    }

    // ---------------- 写しから代表の隊 ----------------

    /// <summary>
    /// 写しの隊（1 枚以上いる隊）を代表の隊にする。同じ 1 章・同じステージ・同じフォルダに「1 刻目の前」があればそれを、無ければ「入った時点」を使う。
    /// </summary>
    public static List<Squad> SquadsOf(IEnumerable<PlayLog.Snap> snaps, string stage, string root)
    {
        var picked = snaps.Where(x => x.Stage == stage && x.Pack is not null && (x.Kind == "start" || x.Kind == "first"))
            .GroupBy(x => (Path.GetDirectoryName(x.Path), x.ChapterSeed))
            .Select(g => g.OrderBy(x => x.Kind == "first" ? 0 : 1).First())
            .OrderBy(x => x.Path, StringComparer.Ordinal);
        var list = new List<Squad>();
        foreach (PlayLog.Snap sn in picked)
        {
            string name = Path.GetRelativePath(root, sn.Path).Replace('\\', '/');
            if (name.EndsWith(".json")) name = name[..^5];
            for (int i = 0; i < sn.Pack!.Squads.Count; i++)
            {
                var us = sn.Pack.Squads[i];
                if (us.Count == 0) continue;
                var pieces = us.Select(u => new Piece(PlayLog.BaseDef(u), Math.Max(1, u.Star), u.Slot, PlayLog.RelicOf(u.Relic))).ToList();
                list.Add(new Squad(name, i + 1, pieces));
            }
        }
        return list;
    }

    // ---------------- 表 ----------------

    public static readonly int[] AllStars = { 0, 1, 2, 3 };
    public static string StarName(int s) => s == 0 ? "写しのまま" : $"全員☆{s}";
    private static string StarsOf(Squad q, int star) => star == 0
        ? string.Join("", Enumerable.Range(1, 3).Select(s => (s, n: q.Pieces.Count(p => Math.Min(3, p.Star) == s))).Where(x => x.n > 0).Select(x => $"☆{x.s}×{x.n}"))
        : $"☆{star}×{q.Pieces.Count}";

    private static double Med(IEnumerable<double> xs)
    {
        var o = xs.OrderBy(x => x).ToList();
        return o.Count == 0 ? double.NaN : o.Count % 2 == 1 ? o[o.Count / 2] : (o[o.Count / 2 - 1] + o[o.Count / 2]) / 2;
    }

    /// <summary>1 組（隊 × 敵部隊 × 倍率 × ☆）の結果。</summary>
    public sealed record Cell(Squad Squad, int Star, Foe Foe, MapFoeScale Scale, IReadOnlyList<Fight> Fights)
    {
        public int Wins => Fights.Count(f => f.Won);
        public double MedHp => Med(Fights.Select(f => f.Hp));
        public double MinHp => Fights.Min(f => f.Hp);
        public double MaxHp => Fights.Max(f => f.Hp);
        public double Lost => Fights.Average(f => f.Lost);
        public double MedTurns => Med(Fights.Select(f => (double)f.Turns));
    }

    /// <summary>
    /// 勇者の通し 1 組（写し × ☆ × 倍率）。<c>KilledAt</c> は seed ごとの倒した隊の番号（0 ＝ 倒せず）。
    /// 第468期: <c>Siege</c> は「守り → 勇者」の通し（同じ隊で続けて戦う）・<c>Banded</c> が偽の通しは参考（帯を読まない——その写しは守り → 勇者の通しで読む）。
    /// </summary>
    public sealed record Chain(string Source, int Star, MapFoeScale Scale, IReadOnlyList<Squad> Order, IReadOnlyList<int> KilledAt, string HeroKey,
                               bool Siege = false, bool Banded = true)
    {
        public int One => KilledAt.Count(k => k == 1);
        public int Two => KilledAt.Count(k => k is 1 or 2);
        public int Never => KilledAt.Count(k => k == 0);
    }

    /// <summary>倍率の盤（素のステージに倍率を掛けた写し・勇者の回復は <paramref name="scale"/> のとおり）。</summary>
    public static MapStage StageAt(int index, MapFoeScale scale) => MapStage.All[index].Scaled(scale);

    /// <summary>1 ステージぶんを回す（表の材料）。</summary>
    public static (List<Cell> Cells, List<Chain> Chains) Measure(IReadOnlyList<Squad> squads, int stageIndex, IReadOnlyList<MapFoeScale> scales,
                                                                 IReadOnlyList<int> stars, StarRule starRule, int seeds, bool relics)
    {
        var cells = new List<Cell>();
        var chains = new List<Chain>();
        foreach (MapFoeScale sc in scales)
        {
            MapStage s = StageAt(stageIndex, sc);
            var foes = FoesOf(s);
            foreach (int star in stars)
            {
                foreach (Squad q in squads)
                    foreach (Foe f in foes)
                    {
                        var fights = new List<Fight>();
                        for (int seed = 0; seed < seeds; seed++) fights.Add(Run(Players(q, star, starRule, relics), Enemies(f, s), seed));
                        cells.Add(new Cell(q, star, f, sc, fights));
                    }
                if (foes.FirstOrDefault(f => f.Kind == Kind.Hero) is { } hero)
                    foreach (var g in squads.GroupBy(q => q.Source))
                    {
                        // 主力 ＝ 勇者に 1 隊で当てたときにいちばん強い隊（勝ち → 終わりの HP の中央値）。通しはその強い順。
                        var order = g.OrderByDescending(q => cells.First(c => c.Squad == q && c.Star == star && c.Foe == hero && c.Scale == sc).Wins)
                                     .ThenByDescending(q => cells.First(c => c.Squad == q && c.Star == star && c.Foe == hero && c.Scale == sc).MedHp)
                                     .ThenBy(q => q.No).ToList();
                        var killed = Enumerable.Range(0, seeds).Select(seed => HeroChain(order, hero, s, star, starRule, seed, relics)).ToList();
                        // 第468期 §2: 守りの後ろに立つ勇者（追う勇者でない）は「守り → 勇者」の通しで帯を読む（勇者だけの通しは参考に残す）。
                        var guards = s.Hero.Chaser ? new List<Foe>() : foes.Where(f => f.Kind == Kind.Garrison).ToList();
                        string key = s.Hero.Chaser ? "hero_chase" : "hero_gate";
                        chains.Add(new Chain(g.Key, star, sc, order, killed, key, Banded: guards.Count == 0));
                        if (guards.Count > 0)
                        {
                            var line = guards.Append(hero).ToList();
                            var sieged = Enumerable.Range(0, seeds).Select(seed => SiegeChain(order, line, s, star, starRule, seed, relics)).ToList();
                            chains.Add(new Chain(g.Key, star, sc, order, sieged, key, Siege: true));
                        }
                    }
            }
        }
        return (cells, chains);
    }

    private static string Pct(double x) => double.IsNaN(x) ? "—" : $"{x:F0}%";

    /// <summary>1 ステージぶんの表（Markdown）。</summary>
    public static List<string> Table(string stageId, List<Cell> cells, List<Chain> chains, Bands bands, int seeds, string head)
    {
        var o = new List<string>();
        string V(Cell c) => c.Foe.Kind == Kind.Hero ? "（下の通し）" : Verdict(bands.Foe[Bands.KeyOf(c.Foe.Kind)], c.Wins, seeds, c.MedHp);
        string H(Chain ch) => ch.Banded ? Verdict(bands.Hero[ch.HeroKey], ch.One, ch.Two, seeds) : "（参考）";
        string Row(Cell c) => $"| {c.Squad.Name} | {c.Squad.Axis} | {StarName(c.Star)}（{StarsOf(c.Squad, c.Star)}） | {c.Foe.Name}（{KindName(c.Foe.Kind)}） | {c.Scale.Text}{(c.Scale.HeroMends ? "" : "・勇者の回復なし")}"
                             + $" | {c.Wins}／{seeds} | {Pct(c.MedHp)}（{Pct(c.MinHp)}〜{Pct(c.MaxHp)}） | {c.Lost:F2} | {V(c)} |";
        string ChainRow(Chain ch)
        {
            var got = ch.KilledAt.Where(k => k > 0).Select(k => (double)k).ToList();
            return $"| {ch.Source} | {StarName(ch.Star)} | {ch.Scale.Text}{(ch.Scale.HeroMends ? "" : "・勇者の回復なし")} | {(ch.Siege ? "守り → 勇者: " : "勇者だけ: ")}{string.Join(" → ", ch.Order.Select(q => $"第{q.No}隊"))}"
                   + $" | {ch.One}／{seeds} | {ch.Two}／{seeds} | {(got.Count == 0 ? "—" : $"{Med(got):0.#}")} | {ch.Never}／{seeds} | {H(ch)} |";
        }
        const string CellHead = "| 隊 | 軸 | ☆ | 敵部隊 | 倍率 | 勝ち | 終わりの HP（中央値・範囲） | 失った枚数 | 帯 |";
        const string CellRule = "|---|---|---|---|---|--:|--:|--:|---|";
        const string ChainHead = "| 写し | ☆ | 倍率 | 当てた順（主力から） | 主力の 1 隊で落ちた | 2 隊までで落ちた | 倒した隊（中央値） | 倒せなかった | 帯 |";
        const string ChainRule = "|---|---|---|---|--:|--:|--:|--:|---|";

        o.Add($"# 敵の重さの表 —— {stageId}");
        o.Add("");
        o.Add(head);
        o.Add("");
        // ---- 帯から外れた組（まず読むところ） ----
        var off = cells.Where(c => c.Foe.Kind != Kind.Hero && V(c) != "○").ToList();
        var offChains = chains.Where(ch => ch.Banded && H(ch) != "○").ToList();
        o.Add($"## 帯から外れた組（{off.Count} ／ {cells.Count(c => c.Foe.Kind != Kind.Hero)} 組・勇者の通し {offChains.Count} ／ {chains.Count(ch => ch.Banded)}）");
        o.Add("");
        o.Add(CellHead);
        o.Add(CellRule);
        foreach (Cell c in off.OrderBy(c => V(c) == "重い" ? 0 : 1).ThenBy(c => c.Foe.Kind).ThenBy(c => c.Foe.Name, StringComparer.Ordinal).ThenBy(c => c.Star).ThenBy(c => c.Squad.Name, StringComparer.Ordinal))
            o.Add(Row(c));
        if (chains.Count > 0)
        {
            o.Add("");
            o.Add(ChainHead);
            o.Add(ChainRule);
            foreach (Chain ch in offChains) o.Add(ChainRow(ch));
        }
        // ---- 帯の数え ----
        o.Add("");
        o.Add("## 帯の数え（敵部隊の種類 × ☆・○ ／ 重い ／ 軽い）");
        o.Add("");
        var starsUsed = cells.Select(c => c.Star).Distinct().OrderBy(x => x).ToList();
        o.Add("| 倍率 | 敵部隊の種類 | " + string.Join(" | ", starsUsed.Select(StarName)) + " |");
        o.Add("|---|---|" + string.Concat(starsUsed.Select(_ => "---|")));
        foreach (var gs in cells.Where(c => c.Foe.Kind != Kind.Hero).GroupBy(c => (c.Scale, c.Foe.Kind)).OrderBy(g => g.Key.Scale.FoePercent).ThenBy(g => g.Key.Kind))
            o.Add($"| {gs.Key.Scale.Text} | {KindName(gs.Key.Kind)} | " + string.Join(" | ", starsUsed.Select(s =>
            {
                var xs = gs.Where(c => c.Star == s).Select(V).ToList();
                return $"{xs.Count(v => v == "○")} ／ {xs.Count(v => v == "重い")} ／ {xs.Count(v => v == "軽い")}";
            })) + " |");
        // ---- 勇者の通し ----
        if (chains.Count > 0)
        {
            o.Add("");
            o.Add("## 勇者に 1 隊ずつ順に当てる（勇者の HP は持ち越す）");
            o.Add("");
            o.Add("主力 ＝ 勇者に 1 隊で当てたときにいちばん強い隊（勝ち → 終わりの HP）。当てる順はその強い順。「倒した隊」は倒した seed の中央値（何隊目で落ちたか）。");
            o.Add("");
            o.Add("「守り → 勇者」は本陣の守りに勝った隊がその終わりの HP のまま勇者に当たる通し（回復なし・負けた隊の相手の HP は次の隊へ）。守りの後ろに立つ勇者の帯はこの通しで読み、「勇者だけ」の通しは（参考）。");
            o.Add("");
            o.Add(ChainHead);
            o.Add(ChainRule);
            foreach (Chain ch in chains) o.Add(ChainRow(ch));
        }
        // ---- 全部 ----
        o.Add("");
        o.Add("## すべての組");
        foreach (var g in cells.GroupBy(c => (c.Scale, c.Star)).OrderBy(g => g.Key.Scale.FoePercent).ThenBy(g => g.Key.Star))
        {
            o.Add("");
            o.Add($"### {g.Key.Scale.Text} ・ {StarName(g.Key.Star)}");
            o.Add("");
            o.Add(CellHead);
            o.Add(CellRule);
            foreach (Cell c in g.OrderBy(c => c.Squad.Name, StringComparer.Ordinal).ThenBy(c => c.Foe.Kind)) o.Add(Row(c));
        }
        return o;
    }

    // ---------------- 口（`--foe-weight`） ----------------

    /// <summary>
    /// `--foe-weight [--plays=<dir>] [--stage=1-2,1-3] [--scale=1.0,1.3,1.6] [--rule=V465] [--seeds=20] [--stars=as,1,2,3] [--no-relics] [--from=<文字>] [--out=<dir>] [--bands=<path>]`。
    /// 既定は design/plays の写し全部・写しのある 1-2 ／ 1-3（1-1 も写しがあれば）・今の 1 章の倍率・seed 20・☆は 4 通り。
    /// </summary>
    public static bool Main(Action<string> write, IReadOnlyList<string> args, string demoAppDir)
    {
        string? Opt(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?[(name.Length + 3)..];
        string repo = Path.GetFullPath(Path.Combine(demoAppDir, ".."));
        // 相対パスはリポジトリの根から（Godot の作業フォルダは DemoApp なので）。
        string root = Path.GetFullPath(Opt("plays") ?? PlayLog.DefaultRoot(demoAppDir), repo);
        string outDir = Path.GetFullPath(Opt("out") ?? Path.Combine(repo, "design", "weights"), repo);
        Bands bands = Bands.Load(Path.GetFullPath(Opt("bands") ?? Path.Combine(repo, "design", "weights", "BANDS.md"), repo));
        ChapterRule rule = Opt("rule") is { } rk ? ChapterRule.ByKey(rk) ?? throw new ArgumentException($"版が無い: {rk}") : Chapter1.Default;
        int seeds = Opt("seeds") is { } sd ? int.Parse(sd, CultureInfo.InvariantCulture) : 20;
        bool relics = !args.Contains("--no-relics");
        var stars = (Opt("stars") ?? "as,1,2,3").Split(',').Select(x => x.Trim() == "as" ? 0 : int.Parse(x, CultureInfo.InvariantCulture)).ToList();
        var snaps = PlayLog.List(root).Where(s => Opt("from") is not { } f || s.Path.Replace('\\', '/').Contains(f, StringComparison.Ordinal) || s.ChapterSeed.ToString() == f).ToList();
        var stageIds = Opt("stage") is { } st ? st.Split(',').Select(x => x.Trim()).ToList()
                     : MapStage.All.Select(s => s.Id).Where(id => snaps.Any(x => x.Stage == id)).ToList();
        StarRule starRule = rule.Stars is { On: true } sr ? sr : StarRule.V464;
        write($"FOE_WEIGHT 写し {snaps.Count} 本（{root}）・ステージ {string.Join(" ／ ", stageIds)}・版 {rule.Name}・seed {seeds}・☆ {string.Join(" ／ ", stars.Select(StarName))}・帯 {bands.Source}");
        Directory.CreateDirectory(outDir);
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        foreach (string id in stageIds)
        {
            int index = MapStage.All.ToList().FindIndex(s => s.Id == id);
            if (index < 0) { write($"FOE_WEIGHT × ステージが無い: {id}"); return false; }
            var squads = SquadsOf(snaps, id, root);
            if (squads.Count == 0) { write($"FOE_WEIGHT {id}: 写しに隊が無い（飛ばした）"); continue; }
            MapFoeScale baseScale = rule.ScaleOf(index);
            var scales = Opt("scale") is { } sc
                ? sc.Split(',').Select(x => (int)Math.Round(double.Parse(x, CultureInfo.InvariantCulture) * 100)).Select(p => new MapFoeScale(p, p, baseScale.HeroMends)).ToList()
                : new List<MapFoeScale> { baseScale };
            var (cells, chains) = Measure(squads, index, scales, stars, starRule, seeds, relics);
            string head = $"`--foe-weight` の出力（{DateTime.Now:yyyy-MM-dd HH:mm}）。手で編集しない（次の実行で上書きされる）。"
                          + $"\n\n- 版: {rule.Name}（☆の表 {starRule.Name}）・倍率 {string.Join(" ／ ", scales.Select(s => s.Text + (s.HeroMends ? "" : "（勇者の回復なし）")))}"
                          + $"\n- 代表の隊: {squads.Count} 隊（{string.Join("・", squads.Select(q => q.Source).Distinct())}）——写しの「1 刻目の前」、無ければ「入った時点」の隊"
                          + $"\n- 単発の戦闘・敵は満タンから・seed 0〜{seeds - 1}・レリック{(relics ? "は写しのまま" : "なし")}・帯 {bands.Source}"
                          + "\n- 終わりの HP は隊の最大 HP に対する割合（負けは 0%）・失った枚数は倒れた駒の平均";
            var lines = Table(id, cells, chains, bands, seeds, head);
            string path = Path.Combine(outDir, $"{date}_{id}.md");
            File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            int off = cells.Count(c => c.Foe.Kind != Kind.Hero && Verdict(bands.Foe[Bands.KeyOf(c.Foe.Kind)], c.Wins, seeds, c.MedHp) != "○");
            write($"FOE_WEIGHT {id}: 隊 {squads.Count} × 敵部隊 {cells.Select(c => (c.Foe.Kind, c.Foe.Name)).Distinct().Count()} × 倍率 {scales.Count} × ☆ {stars.Count}・帯から外れた組 {off} ／ {cells.Count(c => c.Foe.Kind != Kind.Hero)}・勇者の通し {chains.Count} → {path}");
        }
        return true;
    }
}
