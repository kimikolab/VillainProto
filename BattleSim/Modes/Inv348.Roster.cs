using System.Reflection;
using BattleCore;
using static Common;

// inv348 §2 —— 駒の表（立ち絵の無い 16 枚）と資源の地図。
static partial class Inv348Diag
{
    /// <summary>ポンの一覧（指示書 §0・立ち絵の無い 16 枚）。</summary>
    internal static readonly string[] NoArt = { "ムグ", "ラウ", "グザ", "ヴィオ", "バン", "クビ", "ハギ", "セッキ", "ヒビ", "ガレ", "ノミ", "トモ", "ウケ", "ワタ", "ウロ", "カリ" };
    internal static UnitDef ByShort(string n) => UnitCatalog.All.First(d => Short(d) == n);
    const int CompareSeeds = 200, MapSeeds = 20;

    static (string Name, Formation F)[] AllRows() => CompareBuilds().Concat(CrossBuilds()).Select(r => (Name: r.Name, F: Pin349(r.F))).ToArray();   // 第350期: 燃焼の3枚は第349期までの規定に固定

    /// <summary>`compare` と同じ帯で1行 × 第1〜5波の勝率（<paramref name="sc"/> が null なら本編の倍率）。</summary>
    static double[] Grid(Formation f, int seeds, EnemyScaleRule? sc = null)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[nw];
        Parallel.For(0, nw, wi =>
        {
            int wins = 0;
            for (int s = 0; s < seeds; s++)
            {
                bool won = sc is EnemyScaleRule x
                    ? BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.Materialize(EnemyCatalog.Stages[wi].Enemy, BattleContext.EnemyTeam, x), s, verbose: false, boss: new BossRule(false) { Scale = x }).PlayerWon
                    : BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon;
                if (won) wins++;
            }
            g[wi] = 100.0 * wins / seeds;
        });
        return g;
    }
    static double Mean25(double[] g) => g.Skip(1).Take(4).Average();

    // ---- 手番の中身（札の上書きを反射で読む）----
    static bool Overrides(Trait t, string name) =>
        t.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public).Concat(t.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(p => p.GetMethod!))
            .Any(m => m is not null && (m.Name == name || m.Name == "get_" + name) && m.DeclaringType != typeof(Trait) && m.GetBaseDefinition().DeclaringType == typeof(Trait));
    static string TurnKind(UnitDef d)
    {
        var ts = d.Traits.Select(TraitCatalog.Get).ToArray();
        var parts = new List<string>();
        if (ts.Any(t => t.NeverAttacksOwnTurn)) parts.Add("自分では攻撃しない");
        if (d.Actions is { Count: > 0 } acts) parts.Add("術（" + string.Join("・", acts.Select(a => a.Label)) + "）");
        if (ts.Any(t => Overrides(t, nameof(Trait.OnAction))) && d.Actions is null) parts.Add("手番の札");
        if (ts.Any(t => Overrides(t, nameof(Trait.CanAct)))) parts.Add("動けない手番がある");
        if (ts.Any(t => Overrides(t, nameof(Trait.ModifyPattern)) || Overrides(t, nameof(Trait.ModifyHitCount)) || Overrides(t, nameof(Trait.ModifyAttack)))) parts.Add("攻撃が変わる");
        return parts.Count == 0 ? "素の通常攻撃" : string.Join("・", parts);
    }
    /// <summary>出来事の窓口のうち、資源の地図に載せるもの（読み手 ＝ その札を持つ駒）。</summary>
    static readonly (string Res, string[] Hooks)[] HookRes =
    {
        ("死", new[] { nameof(Trait.OnAllyDeath), nameof(Trait.OnAnyDeath), nameof(Trait.OnKill) }),
        ("自分の死", new[] { nameof(Trait.OnDeath) }),
        ("被弾", new[] { nameof(Trait.OnDamaged), nameof(Trait.OnAllyDamaged) }),
        ("移動", new[] { nameof(Trait.OnMoved), nameof(Trait.OnAllyMoved) }),
    };
    static string[] HookReads(UnitDef d) => HookRes.Where(h => d.Traits.Select(TraitCatalog.Get).Any(t => h.Hooks.Any(m => Overrides(t, m)))).Select(h => h.Res).ToArray();

    // ---- 資源の地図の集計 ----
    sealed class MapAgg
    {
        public long N;
        public readonly Dictionary<(string Unit, string Key), long[]> Out = new();   // 味方の駒 → [自分 回, 量, 味方 回, 量, 敵 回, 量]
        public readonly Dictionary<(string Unit, string Key), long> Reads = new();   // 味方の駒が読んだ（`Counter` の読み・第94期の印）
        public readonly Dictionary<(string Wave, string Enemy, string Key), long[]> FoeOnAlly = new();   // 味方に書く敵 → [回, 量]
        public readonly Dictionary<string, long[]> AllyIn = new();   // 味方が浴びた量の出どころ → key → [自分, 味方, 敵, なし]（回）
        public readonly Dictionary<(string Unit, string Key), long[]> CarryOut = new();   // 強化 ／ 弱体（窓口）→ [味方 量, 敵 量]
        public readonly Dictionary<(string Wave, string Enemy), long[]> FoeDull = new();   // 味方に弱体を書いた敵 → [回, 量]
        public void Merge(MapAgg o)
        {
            N += o.N;
            foreach (var (k, v) in o.Out) { if (!Out.TryGetValue(k, out var a)) Out[k] = a = new long[6]; for (int i = 0; i < 6; i++) a[i] += v[i]; }
            foreach (var (k, v) in o.Reads) Reads[k] = Reads.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.FoeOnAlly) { if (!FoeOnAlly.TryGetValue(k, out var a)) FoeOnAlly[k] = a = new long[2]; a[0] += v[0]; a[1] += v[1]; }
            foreach (var (k, v) in o.AllyIn) { if (!AllyIn.TryGetValue(k, out var a)) AllyIn[k] = a = new long[4]; for (int i = 0; i < 4; i++) a[i] += v[i]; }
            foreach (var (k, v) in o.CarryOut) { if (!CarryOut.TryGetValue(k, out var a)) CarryOut[k] = a = new long[2]; a[0] += v[0]; a[1] += v[1]; }
            foreach (var (k, v) in o.FoeDull) { if (!FoeDull.TryGetValue(k, out var a)) FoeDull[k] = a = new long[2]; a[0] += v[0]; a[1] += v[1]; }
        }
    }

    static MapAgg MapFight(Formation f, W w, int seed)
    {
        var m = new MapAgg { N = 1 };
        var reads = m.Reads; var carry = m.CarryOut; var foeDull = m.FoeDull;
        void Probe(TraitId trait, UnitState owner, UnitState target, string key, int delta)
        {
            if (owner.TeamId != BattleContext.PlayerTeam)
            {
                if (delta > 0 && key == "弱体" && target.TeamId == BattleContext.PlayerTeam)
                {
                    var fk = (w.Name, owner.Def.Id);
                    if (!foeDull.TryGetValue(fk, out var fa)) foeDull[fk] = fa = new long[2];
                    fa[0]++; fa[1] += delta;
                    EnemyNames.TryAdd(owner.Def.Id, owner.Def.Name);
                }
                return;
            }
            if (delta == 0) { var k = (owner.Def.Id, key); reads[k] = reads.GetValueOrDefault(k) + 1; return; }
            if (delta > 0 && key is "強化" or "弱体")
            {
                var k = (owner.Def.Id, key);
                if (!carry.TryGetValue(k, out var a)) carry[k] = a = new long[2];
                a[target.TeamId == owner.TeamId ? 0 : 1] += delta;
            }
        }
        var (r, p, e) = Raw(f, w, seed, probe: Probe);
        var pid = p.Select(u => u.Def.Id).ToHashSet();
        var eid = e.Select(u => u.Def.Id).ToHashSet();
        foreach (var u in e) EnemyNames.TryAdd(u.Def.Id, u.Def.Name);
        int nk = StatusKeys.All.Length;
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (pid.Contains(id) && t.Inv348Out is long[] o)
                for (int k = 0; k < nk; k++)
                {
                    if (o[k * 8] + o[k * 8 + 2] + o[k * 8 + 4] == 0) continue;
                    var key = (id, StatusKeys.All[k]);
                    if (!m.Out.TryGetValue(key, out var a)) m.Out[key] = a = new long[6];
                    for (int i = 0; i < 6; i++) a[i] += o[k * 8 + i];
                }
            if (eid.Contains(id) && !pid.Contains(id) && t.Inv348Out is long[] eo)
                for (int k = 0; k < nk; k++)
                {
                    if (eo[k * 8 + 4] == 0) continue;
                    var key = (w.Name, id, StatusKeys.All[k]);
                    if (!m.FoeOnAlly.TryGetValue(key, out var a)) m.FoeOnAlly[key] = a = new long[2];
                    a[0] += eo[k * 8 + 4]; a[1] += eo[k * 8 + 5];
                }
            if (pid.Contains(id) && t.Inv348In is long[] inn)
                for (int k = 0; k < nk; k++)
                {
                    if (!m.AllyIn.TryGetValue(StatusKeys.All[k], out var a)) m.AllyIn[StatusKeys.All[k]] = a = new long[4];
                    for (int rel = 0; rel < 4; rel++) a[rel] += inn[k * 8 + rel * 2];
                }
        }
        return m;
    }

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> EnemyNames = new();
    static string Name(string id) => UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id) is { } d ? Short(d) : EnemyNames.TryGetValue(id, out var n) ? n : id;
    static string KeyName(string key) => StatusKeys.All.Contains(key) ? StatusKeys.LabelOf(key) : key;

    static void Roster()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var units = NoArt.Select(ByShort).ToArray();
        var rows = AllRows();
        int nCompare = CompareBuilds().Length;
        Console.WriteLine($"# {Tag} §2 駒の表（立ち絵の無い {units.Length} 枚）と資源の地図");
        Console.WriteLine();
        Console.WriteLine($"行 ＝ `compare` {nCompare} 行 ＋ 交差帯 {rows.Length - nCompare} 行（在席は両方で数え、在席行の平均は第2〜5波・seed 0..{CompareSeeds - 1}）。");
        Console.WriteLine();

        // ---- 勝率表（本物 ／ 素体）----
        var real = new Dictionary<string, double[]>();
        foreach (var (n, f) in rows) real[n] = Grid(f, CompareSeeds);
        var bossOf = new Dictionary<string, double>();
        var boardsForBoss = rows.Concat(Presets.Playtest.Select(r => (Name: r.Name, F: Pin349(r.F)))).Concat(VersionsH356.Where(r => r.Name.StartsWith("試遊・", StringComparison.Ordinal)).Select(r => (Name: r.Name, F: Pin349(r.F))))
            .Where(r => units.Any(u => r.F.Occupied().Any(o => o.Def.Id == u.Id))).GroupBy(r => r.Name).Select(g => g.First()).ToArray();
        foreach (var (n, f) in boardsForBoss) bossOf[n] = Measure(f, Boss).Win;

        Console.WriteLine("## 1. 駒の表（測った列）");
        Console.WriteLine();
        Console.WriteLine("帰属 ＝ 在席行で**その駒だけ**を素体（同数値・特性なし・`checkup ideal` の作り）に落とした差の平均（第2〜5波・seed 0..199）。"
            + "（強）＝ 同じことを本編 第2〜5波 × HP 500% ／ 攻 300% で（seed 0..99・R365 で本編が全波 100% の駒のため）。固有の勝者 ＝ 第2〜5波のうち1つの波でだけ 100% の在席行の数。"
            + "ボス ＝ その駒がいる台（`compare` ／ 交差帯 ／ 試遊の台）のボスの勝率の最高（seed 0..99・その台）と 50% 以上の台の数。チェック波 ＝ `checkwave` の7台にいるか。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 手番の中身 | 在席（compare ＋ 交差帯）| 在席行の平均 | 帰属 | 帰属（強） | 固有の勝者 | ボス（最高の台 ／ 50%+ の台数） | チェック波 | よく組む相手（同席の上位3） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|---|---|");
        var cw = CheckWaveDiag.Boards7.Select(CheckWaveDiag.BoardOf).ToArray();
        foreach (var u in units)
        {
            var mine = rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == u.Id)).ToArray();
            int inCmp = mine.Count(r => CompareBuilds().Any(c => c.Name == r.Name));
            double avg = mine.Length == 0 ? double.NaN : mine.Average(r => Mean25(real[r.Name]));
            var dl = new List<double>(); var dls = new List<double>();
            foreach (var (n, f) in mine)
            {
                var pf = Swap(f, u.Id, Plain(u));
                dl.Add(Mean25(real[n]) - Mean25(Grid(pf, CompareSeeds)));
                dls.Add(Mean25(Grid(f, Seeds, Strong)) - Mean25(Grid(pf, Seeds, Strong)));
            }
            bool allFull = mine.Length > 0 && mine.All(r => real[r.Name].Skip(1).All(x => x >= 100.0));
            int sole = mine.Count(r => real[r.Name].Skip(1).Count(x => x >= 100.0) == 1);
            var bb = boardsForBoss.Where(r => r.F.Occupied().Any(o => o.Def.Id == u.Id)).Select(r => (r.Name, B: bossOf[r.Name])).OrderByDescending(x => x.B).ToArray();
            string boss = bb.Length == 0 ? "—" : $"{F1(bb[0].B)}%（{bb[0].Name}）／ {bb.Count(x => x.B >= 50)}";
            bool inCw = cw.Any(f => f.Occupied().Any(o => o.Def.Id == u.Id));
            var mates = mine.SelectMany(r => r.F.Occupied().Select(o => o.Def).Where(d => d.Id != u.Id)).GroupBy(d => d.Id)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(3).Select(g => $"{Short(g.First())} {g.Count()}");
            Console.WriteLine($"| {Short(u)}（{u.Name}） | {TurnKind(u)} | {mine.Length}（{inCmp} ＋ {mine.Length - inCmp}） | {(double.IsNaN(avg) ? "—" : F1(avg))} | "
                + $"{(mine.Length == 0 ? "—" : Sg(dl.Average()))}{(allFull ? "（全波 100%・測れない）" : "")} | {(mine.Length == 0 ? "—" : Sg(dls.Average()))} | {sole} | {boss} | {(inCw ? "○" : "—")} | {string.Join("・", mates)} |");
        }
        Console.WriteLine();
        Console.WriteLine("在席行の一覧:");
        Console.WriteLine();
        foreach (var u in units)
        {
            var mine = rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == u.Id)).Select(r => $"{r.Name}（{F1(Mean25(real[r.Name]))}）");
            Console.WriteLine($"- **{Short(u)}**: {string.Join("・", mine)}");
        }
        Console.WriteLine();

        // ---- 資源 ----
        W[] mapW = new[] { M2, Main(3, EnemyScaleRule.Default, "本編 "), M4, M5, Boss, Guard, Bat };
        var map = new MapAgg();
        var lockObj = new object();
        foreach (var (n, f) in rows)
            foreach (var w in mapW)
                Parallel.For(0, MapSeeds, s => { var m = MapFight(f, w, s); lock (lockObj) map.Merge(m); });
        long nB = map.N;
        Console.WriteLine($"## 2. 読み書きする資源（16 枚・1戦あたり・行 {rows.Length} × 波 {mapW.Length}（本編 第2〜5波 ／ ボス ／ 近衛 ／ 大隊）× seed 0..{MapSeeds - 1}・延べ {nB:N0} 戦）");
        Console.WriteLine();
        Console.WriteLine("書く ＝ 状態キーが増えた量（第348期の帳簿 `Inv348Out`・書き手は第94期の印）を **敵へ ／ 味方へ ／ 自分へ**（回）。弱体 ／ 強化 ＝ 窓口（`Dull` ／ `Whet`）の量（敵へ ／ 味方へ）。"
            + "読む ＝ 札が状態キーを読んだ回数（`Counter` の読み）。出来事 ＝ 札が持つ窓口（死 ＝ 味方 ／ 誰かの死・撃破 ／ 被弾 ／ 移動）。1戦あたりは**その駒が在席した戦**で割る。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席した戦 | 書く（敵へ ／ 味方へ ／ 自分へ・回 ／戦） | 弱体 ／ 強化（敵へ ／ 味方へ・量 ／戦） | 読む（回 ／戦・多い順） | 出来事の読み手 |");
        Console.WriteLine("|---|--:|---|---|---|---|");
        var presence = rows.SelectMany(r => r.F.Occupied().Select(o => o.Def.Id).Distinct()).GroupBy(x => x).ToDictionary(g => g.Key, g => (long)g.Count() * mapW.Length * MapSeeds);
        foreach (var u in units)
        {
            long nu = presence.GetValueOrDefault(u.Id);
            string writes = string.Join("・", map.Out.Where(kv => kv.Key.Unit == u.Id).OrderByDescending(kv => kv.Value[0] + kv.Value[2] + kv.Value[4])
                .Select(kv => $"{KeyName(kv.Key.Key)} {Per2(kv.Value[4], nu)} ／ {Per2(kv.Value[2], nu)} ／ {Per2(kv.Value[0], nu)}"));
            string carry = string.Join("・", map.CarryOut.Where(kv => kv.Key.Unit == u.Id).Select(kv => $"{kv.Key.Key} {Per(kv.Value[1], nu)} ／ {Per(kv.Value[0], nu)}"));
            string reads = string.Join("・", map.Reads.Where(kv => kv.Key.Unit == u.Id && StatusKeys.All.Contains(kv.Key.Key)).OrderByDescending(kv => kv.Value).Take(6)
                .Select(kv => $"{KeyName(kv.Key.Key)} {Per(kv.Value, nu)}"));
            Console.WriteLine($"| {Short(u)} | {nu:N0} | {(writes == "" ? "—" : writes)} | {(carry == "" ? "—" : carry)} | {(reads == "" ? "—" : reads)} | {string.Join("・", HookReads(u)) switch { "" => "—", var x => x }} |");
        }
        Console.WriteLine();

        // ---- 資源の地図 ----
        Console.WriteLine("## 3. 資源の地図（状態キーごと・味方の駒の全員・`compare` ＋ 交差帯の行）");
        Console.WriteLine();
        Console.WriteLine("書き手（敵へ）／ 書き手（味方へ・漏れも含む）＝ 1戦あたり 0.05 回以上書いた駒（その駒が在席した戦で割る・**太字**は立ち絵の無い 16 枚）。読み手 ＝ そのキーを札が読んだ駒（1戦 0.05 回以上）。"
            + "味方が浴びた出どころ ＝ 味方の駒に増えた回数の内訳（全戦で割る）: 味方から ／ 敵から ／ 書き手なし（engine の規則）。味方に書く敵 ＝ 敵の駒ごと（波・回 ／戦）。");
        Console.WriteLine();
        Console.WriteLine("| キー | 書き手（敵へ） | 書き手（味方へ） | 読み手 | 味方が浴びた（味方から ／ 敵から ／ なし・回 ／戦） | 味方に書く敵（波: 駒 回 ／戦） |");
        Console.WriteLine("|---|---|---|---|---|---|");
        var noArtIds = units.Select(u => u.Id).ToHashSet();
        string Nm(string id) => noArtIds.Contains(id) ? $"**{Name(id)}**" : Name(id);
        foreach (var key in StatusKeys.All)
        {
            string W(int rel) => string.Join("・", map.Out.Where(kv => kv.Key.Key == key).Select(kv => (kv.Key.Unit, v: (double)kv.Value[rel] / Math.Max(1, presence.GetValueOrDefault(kv.Key.Unit))))
                .Where(x => x.v >= 0.05).OrderByDescending(x => x.v).Select(x => $"{Nm(x.Unit)} {x.v:F2}"));
            string rd = string.Join("・", map.Reads.Where(kv => kv.Key.Key == key).Select(kv => (kv.Key.Unit, v: (double)kv.Value / Math.Max(1, presence.GetValueOrDefault(kv.Key.Unit))))
                .Where(x => x.v >= 0.05).OrderByDescending(x => x.v).Select(x => $"{Nm(x.Unit)} {x.v:F1}"));
            var ai = map.AllyIn.GetValueOrDefault(key) ?? new long[4];
            string foe = string.Join("・", map.FoeOnAlly.Where(kv => kv.Key.Key == key).GroupBy(kv => kv.Key.Wave)
                .Select(g => $"{g.Key}: " + string.Join("、", g.OrderByDescending(kv => kv.Value[0]).Select(kv => $"{Name(kv.Key.Enemy)} {(double)kv.Value[0] / (rows.Length * MapSeeds):F2}"))));
            if (W(4) == "" && W(2) == "" && rd == "" && ai.Sum() == 0) continue;
            Console.WriteLine($"| {KeyName(key)} | {(W(4) == "" ? "—" : W(4))} | {(W(2) == "" ? "—" : W(2))} | {(rd == "" ? "—" : rd)} | {Per2(ai[1], nB)} ／ {Per2(ai[2], nB)} ／ {Per2(ai[3], nB)} | {(foe == "" ? "—" : foe)} |");
        }
        // 窓口の通貨（弱体 ／ 強化）
        foreach (var ck in new[] { "弱体", "強化" })
        {
            string W(int i) => string.Join("・", map.CarryOut.Where(kv => kv.Key.Key == ck).Select(kv => (kv.Key.Unit, v: (double)kv.Value[i] / Math.Max(1, presence.GetValueOrDefault(kv.Key.Unit))))
                .Where(x => x.v >= 0.5).OrderByDescending(x => x.v).Select(x => $"{Nm(x.Unit)} {x.v:F1}"));
            string fd = ck != "弱体" ? "—" : string.Join("・", map.FoeDull.GroupBy(kv => kv.Key.Wave).Select(g => $"{g.Key}: " + string.Join("、", g.OrderByDescending(kv => kv.Value[1]).Select(kv => $"{Name(kv.Key.Enemy)} {(double)kv.Value[0] / (rows.Length * MapSeeds):F2} 回 ／ {(double)kv.Value[1] / (rows.Length * MapSeeds):F1}"))));
            string fdAll = ck != "弱体" ? "—" : $"敵から {(double)map.FoeDull.Values.Sum(v => v[1]) / nB:F2}（量 ／戦）";
            Console.WriteLine($"| {ck}（窓口・量） | {(W(1) == "" ? "—" : W(1))} | {(W(0) == "" ? "—" : W(0))} | （窓口で横取りする札） | {fdAll} | {(fd == "" ? "—" : fd)} |");
        }
        Console.WriteLine();
        Console.WriteLine("出来事の読み手（札の窓口・`compare` の行の駒を含む 52 枚）:");
        Console.WriteLine();
        foreach (var (res, _) in HookRes)
        {
            var holders = UnitCatalog.All.Where(d => HookReads(d).Contains(res)).Select(d => noArtIds.Contains(d.Id) ? $"**{Short(d)}**" : Short(d));
            Console.WriteLine($"- **{res}**: {string.Join("・", holders)}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
