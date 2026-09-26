using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// =====================================================================================
// shock モード（第214期） —— 新しい状態異常「感電」と、雷のカタ（＋ススを血詠みのアカへ）
//
// 指示書は design/PHASE214_KATA_SHOCK_SPEC.md ／ 報告は design/PHASE214_KATA_SHOCK.md。
// **線は置かない**（採否はポンが遊んで決める）。
//
//     dotnet run --project BattleSim -c Release 0 shock phase0   # Q0-2〜Q0-7（台の枠の選定だけ戦闘を回す）
//     dotnet run --project BattleSim -c Release 0 shock run      # 表A〜E（席の総当たり ＋ 4台 × 4版 × 2陣形）
//     dotnet run --project BattleSim -c Release 0 shock check [第213期のbalance.md]   # 自己検査（受け入れ 1〜4）
//
// 台は `Presets` に足さない（この診断のローカル）。
// =====================================================================================

static partial class ShockDiag
{
    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(arg); return;
            default:
                Console.WriteLine("shock: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunImpl(string arg);
    static partial void CheckImpl(string arg);

    // =================================================================================
    // 台の元の行と「残す駒」（**測る前に固定**・指示書 §6）
    // =================================================================================

    /// <summary>
    /// 台ごとの元の行と、差し替えない駒（Id）。<b>残す駒 ＝ 数える状態異常の書き手 ＋ 台の狙いの駒</b>。
    /// 残りの駒のうち、素体に替えたときの帰属（元 − 素体版・第2〜5波 × seed 1000..1199）が最も低い1枚
    /// （台4 は2枚）をカタ（台4 はアカとカタ）に替える。台1 だけは指示書が顔ぶれを名指ししている（ラウの枠）。
    /// </summary>
    internal static readonly (string Tag, string Row, string[] Keep, int Replace)[] BaseRows =
    {
        ("台1 毒パ",   "毒+ベニ+ラウ",            new[] { "gald", "sid", "guza", "beni" }, 1),
        ("台2 燃焼パ", "燃焼 (ボルグ×ホタ)",      new[] { "borg", "lili", "mudo" }, 1),          // 書き手: 燃焼・聖痕・呪い
        ("台3 起爆役", "毒→被弾強化 (グザ×ムド)", new[] { "guza", "mudo", "borg" }, 1),          // 書き手: 毒・呪い ／ 薙ぎ: ボルグ
        ("台4 血",     "惨禍×死の連鎖",           new[] { "kado", "rica", "zoto" }, 2),          // 惨禍・墓守 ／ 書き手: 破裂の着火
    };

    const int SelSeed0 = 1000, SelSeeds = 200;

    static Formation RowOf(string name) => CompareBuilds().First(r => r.Name == name).F;

    static double Mean25Sel(Formation f)
    {
        int wins = 0;
        for (int st = 1; st < 5; st++)
            for (int s = SelSeed0; s < SelSeed0 + SelSeeds; s++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: false).PlayerWon) wins++;
        return 100.0 * wins / (4 * SelSeeds);
    }

    internal static UnitDef PlainOf(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name, MaxHp = d.MaxHp,
        Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
        Traits = Array.Empty<TraitId>()
    };

    internal static Formation SwapDef(Formation f, UnitDef from, UnitDef to)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, from) ? to : d;
        return g;
    }

    /// <summary>台の枠の選定（台1 は名指し・台2〜4 は帰属の低い順）。返すのは替える駒の一覧。</summary>
    internal static List<(UnitDef Def, double Attr)> ReplacedOf(int i, bool print)
    {
        var (tag, row, keep, n) = BaseRows[i];
        Formation f = RowOf(row);
        if (i == 0)
        {
            UnitDef rau = f.Occupied().Select(o => o.Def).First(d => d.Id == "rau");
            return new() { (rau, double.NaN) };
        }
        double baseW = Mean25Sel(f);
        var cand = f.Occupied().Select(o => o.Def).Where(d => !keep.Contains(d.Id))
            .Select(d => (Def: d, Attr: baseW - Mean25Sel(SwapDef(f, d, PlainOf(d))))).ToList();
        if (print)
        {
            Console.WriteLine("- " + tag + "（元 `" + row + "`・第2〜5波 × seed " + SelSeed0 + ".." + (SelSeed0 + SelSeeds - 1) + " の元 " + baseW.ToString("F1") + "%）: "
                              + string.Join(" ／ ", cand.Select(c => c.Def.Name + " " + c.Attr.ToString("+0.0;-0.0;0.0"))));
        }
        return cand.OrderBy(c => c.Attr).ThenBy(c => Array.IndexOf(f.Occupied().Select(o => o.Def).ToArray(), c.Def)).Take(n).ToList();
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static string FindRoot()
    {
        string d = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(d, "CLAUDE.md"))) d = Path.GetDirectoryName(d) ?? throw new InvalidOperationException("CLAUDE.md が見つからない");
        return d;
    }

    /// <summary>Traits.cs を「クラス名 → 本文」に割る（次の `public sealed class` までを1つの本文とみなす）。</summary>
    static Dictionary<string, string> ClassBodies(string src)
    {
        var map = new Dictionary<string, string>();
        var ms = Regex.Matches(src, @"(?m)^public (?:sealed |abstract )?class (\w+)");
        for (int i = 0; i < ms.Count; i++)
        {
            int start = ms[i].Index, end = i + 1 < ms.Count ? ms[i + 1].Index : src.Length;
            map[ms[i].Groups[1].Value] = src.Substring(start, end - start);
        }
        return map;
    }

    static TraitId? IdOf(string body)
    {
        var m = Regex.Match(body, @"Id\s*=>\s*TraitId\.(\w+)");
        return m.Success && Enum.TryParse(m.Groups[1].Value, out TraitId t) ? t : null;
    }

    static void Phase0()
    {
        Console.WriteLine("# 第214期 `shock phase0` —— Q0-2〜Q0-7");
        Console.WriteLine();
        string root = FindRoot();
        string traits = File.ReadAllText(Path.Combine(root, "BattleCore", "Traits.cs"));
        string engine = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
        var bodies = ClassBodies(traits);

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 敵の隣接（X 字・パターン3）");
        Console.WriteLine();
        foreach ((string label, FormationShape shape, Formation? f) in new (string, FormationShape, Formation?)[]
                 {
                     ("X 字（第一〜五波の元の波）", FormationShape.X, EnemyCatalog.Stages[4].Enemy),
                     ("パターン3（第五波の写し）", FormationShape.Spear, EnemyCatalog.Pattern3Copies[1].Enemy),
                 })
        {
            Console.WriteLine("### " + label);
            Console.WriteLine();
            Console.WriteLine("| 席 | 盤の席 | 編成の席か | 隣（盤の席） | 隣の数（編成の席だけ） |");
            Console.WriteLine("|---|--:|---|---|--:|");
            for (int s = 0; s < 9; s++)
            {
                var adj = Enumerable.Range(0, 9).Where(b => shape.AreAdjacent(s, b)).ToList();
                bool play = !shape.IsSummonSlot(s);
                int playAdj = adj.Count(b => !shape.IsSummonSlot(b));
                Console.WriteLine("| " + shape.SeatName(s) + " | " + s + " | " + (play ? "○" : "召喚") + " | "
                                  + string.Join(" ", adj.Select(b => shape.SeatName(b))) + " | " + playAdj + " |");
            }
            Console.WriteLine();
            if (f is not null)
            {
                Console.WriteLine("第五波（" + label + "）の実際の5体の隣: "
                                  + string.Join(" ／ ", f.Occupied().Select(o => o.Def.Name + "（" + shape.SeatName(shape.PlayableSlots[o.Slot]) + "）=" + f.Occupied().Count(p => p.Slot != o.Slot && shape.AreAdjacent(shape.PlayableSlots[o.Slot], shape.PlayableSlots[p.Slot])))));
                Console.WriteLine();
            }
        }

        // 敵の駒の札のうち隣接を読むもの（Traits.cs の走査）
        var adjReaders = bodies.Where(kv => kv.Value.Contains("AreAdjacent(")).Select(kv => IdOf(kv.Value)).Where(t => t is not null).Select(t => t!.Value).ToHashSet();
        if (adjReaders.Count == 0) throw new InvalidOperationException("R034: 隣接を読む札の走査が空");
        var enemyTraits = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied()).Concat(EnemyCatalog.Pattern3Copies.SelectMany(s => s.Enemy.Occupied()))
            .SelectMany(o => o.Def.Traits).ToHashSet();
        var hit = enemyTraits.Where(adjReaders.Contains).ToList();
        Console.WriteLine("- 隣接を読む札（`Traits.cs` で `AreAdjacent(` を含むクラス）: " + adjReaders.Count + " 枚");
        Console.WriteLine("- **そのうち `Stages` ／ パターン3の写しの敵が持つ札: " + hit.Count + " 枚**" + (hit.Count > 0 ? "（" + string.Join("・", hit) + "）" : "（**敵側で隣接を読むのは感電が初めて**）"));
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 数える状態異常（`StatusKeys.All` の 23 本を1本ずつ）");
        Console.WriteLine();
        Console.WriteLine("`derive scan` の観測は `UnitTally.CarryKeys`（11 本）しか引けないので、"
                          + "**書き込みの場所を `Traits.cs` / `BattleEngine.cs` から走査**して、書く札と保持者（`UnitCatalog.All`）を並べる。");
        Console.WriteLine();
        var holders = UnitCatalog.All.SelectMany(d => d.Traits.Select(t => (t, d))).GroupBy(x => x.t).ToDictionary(g => g.Key, g => g.Select(x => x.d.Name).Distinct().ToList());
        var writeTokens = new Dictionary<string, string[]>
        {
            [StatusKeys.Poison] = new[] { "ctx.Poison(", "SetCounter(StatusKeys.Poison" },
            [StatusKeys.Burn] = new[] { "ctx.Ignite(", "SetCounter(StatusKeys.Burn" },
            [StatusKeys.Wound] = new[] { "ctx.Wound(", "SetCounter(StatusKeys.Wound" },
            [StatusKeys.Concentrated] = new[] { "MarkConcentrated(" },
            [StatusKeys.Numbed] = new[] { "MarkNumbed(" },
        };
        var counted = new HashSet<string>(ShockKinds.Counted);
        Console.WriteLine("| キー | 表示 | 書く札（保持者・**減算や消去を含む**） | 数える | 理由 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (string key in StatusKeys.All)
        {
            string name = typeof(StatusKeys).GetFields().First(fi => fi.IsLiteral && (string?)fi.GetRawConstantValue() == key).Name;
            string[] tokens = writeTokens.TryGetValue(key, out var tk) ? tk : new[] { "SetCounter(StatusKeys." + name };
            var writers = bodies.Where(kv => tokens.Any(t => kv.Value.Contains(t))).Select(kv => IdOf(kv.Value)).Where(t => t is not null).Select(t => t!.Value).Distinct()
                .Select(t => t + (holders.TryGetValue(t, out var hs) ? "（" + string.Join("・", hs) + "）" : "（保持者 0）")).ToList();
            bool eng = tokens.Any(t => engine.Contains(t));
            Console.WriteLine("| `" + name + "` | " + StatusKeys.LabelOf(key) + " | " + (writers.Count == 0 ? "—" : string.Join(" / ", writers)) + (eng ? " ＋ engine" : "")
                              + " | " + (counted.Contains(key) ? "**○**" : "×") + " | " + ShockKinds.Why(key) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("| `Shock`（第214期に足す） | 雷 | 雷のカタ | **○** | 新しい状態異常（雷で付く） |");
        Console.WriteLine();
        Console.WriteLine("- 数える: **" + ShockKinds.Counted.Length + " 本**（新しい `Shock` を含む）");
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 撃破の読み手");
        Console.WriteLine();
        var onKill = bodies.Where(kv => kv.Value.Contains("override void OnKill")).Select(kv => IdOf(kv.Value)).Where(t => t is not null).Select(t => t!.Value).ToList();
        var enemyHold = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied()).SelectMany(o => o.Def.Traits.Select(t => (t, o.Def.Name))).ToList();
        foreach (TraitId t in onKill)
        {
            var hs = holders.TryGetValue(t, out var h) ? h : new List<string>();
            var es = enemyHold.Where(x => x.t == t).Select(x => x.Name).Distinct().ToList();
            Console.WriteLine("- `OnKill`: `" + t + "` —— 味方 " + (hs.Count == 0 ? "0 枚" : string.Join("・", hs)) + " ／ 敵 " + (es.Count == 0 ? "0 体" : string.Join("・", es)));
        }
        int hd = engine.IndexOf("private void HandleDeath(", StringComparison.Ordinal);
        string hdBody = engine.Substring(hd, engine.IndexOf("void NoteAlone()", hd, StringComparison.Ordinal) - hd);
        Console.WriteLine("- `HandleDeath` の中で `killer` を読む行: " + Regex.Matches(hdBody, @"killer").Count + " 箇所（`Kills` の加算・`Death` の書き手・標の帳簿・背かれの計数・混乱の計数・`OnKill`）");
        var anyDeath = bodies.Where(kv => kv.Value.Contains("override void OnAnyDeath")).Select(kv => IdOf(kv.Value)).Where(t => t is not null).Select(t => t!.Value).ToList();
        Console.WriteLine("- `OnAnyDeath`（**撃破者を読まない**）: " + string.Join("・", anyDeath));
        Console.WriteLine();

        // ---------------- Q0-5 ----------------
        Console.WriteLine("## Q0-5 ベニの反転の口");
        Console.WriteLine();
        Console.WriteLine("- 入口は `InvertsTick(u)`（同じ陣営の生きている反転の保持者が隣にいれば返す）→ `InverseHeal(ベニ, u, 量, kind, 文言)`。呼び口は "
                          + Count(engine, "InverseHeal(") + " 箇所（定義を含む）");
        Console.WriteLine("- `kind` は帳簿の添字（0 毒 ／ 1 火 ／ それ以外 起爆）。放電は **3** を足して別に数える");
        Console.WriteLine();

        // ---------------- Q0-6 ----------------
        Console.WriteLine("## Q0-6 アカ（ススの名前と「灰」の出る場所）");
        Console.WriteLine();
        foreach (string dir in new[] { "BattleCore", "BattleSim", "docs", "DemoApp" })
        {
            var files = Directory.EnumerateFiles(Path.Combine(root, dir), "*.*", SearchOption.AllDirectories)
                .Where(p => (p.EndsWith(".cs") || p.EndsWith(".md") || p.EndsWith(".tscn")) && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !p.Contains(".godot"));
            int nName = 0, nAsh = 0; var where = new List<string>();
            foreach (string p in files)
            {
                string s = File.ReadAllText(p);
                int a = Count(s, "拾い屋のスス"), b = Count(s, "灰");
                if (a + b == 0) continue;
                nName += a; nAsh += b;
                where.Add(Path.GetFileName(p) + "(" + a + "/" + b + ")");
            }
            Console.WriteLine("- `" + dir + "/`: 名前 " + nName + " ／ 「灰」 " + nAsh + " —— " + string.Join(" ", where.Take(40)) + (where.Count > 40 ? " …" : ""));
        }
        Console.WriteLine();

        // ---------------- Q0-7 ----------------
        Console.WriteLine("## Q0-7 台（元の行と替える枠）");
        Console.WriteLine();
        for (int i = 0; i < BaseRows.Length; i++)
        {
            var rep = ReplacedOf(i, print: true);
            Formation f = RowOf(BaseRows[i].Row);
            Console.WriteLine("  - 替える: " + string.Join("・", rep.Select(r => r.Def.Name)) + " ／ 残る: "
                              + string.Join("・", f.Occupied().Select(o => o.Def).Where(d => !rep.Any(r => ReferenceEquals(r.Def, d))).Select(d => d.Name)));
        }
        Console.WriteLine();
    }

    static int Count(string hay, string needle)
    {
        int n = 0, i = 0;
        while ((i = hay.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}

/// <summary>
/// 雷の1発の重さに数える状態異常（第214期 Q0-3）。<b>engine の判定（`ThunderTrait.KindsOf`）はこちらを読まない</b>
/// ——表の説明と突き合わせるための写し。一致は自己検査で見る。
/// </summary>
static class ShockKinds
{
    /// <summary>Phase 0 で決めた一覧（実装前に固定）。<c>"shock"</c> は第214期に足すキー（`StatusKeys.Shock`）。</summary>
    internal static readonly string[] Counted =
    {
        StatusKeys.Poison, StatusKeys.Marked, StatusKeys.Stun, StatusKeys.Burn, StatusKeys.Wound, StatusKeys.Deep,
        StatusKeys.Curse, StatusKeys.Stagger, StatusKeys.Confused, StatusKeys.Grappled, StatusKeys.Cowed,
        StatusKeys.Daunted, StatusKeys.Concentrated, StatusKeys.Numbed, StatusKeys.Stigma, "shock",
    };

    internal static string Why(string key) => key switch
    {
        StatusKeys.Poison => "敵に書く（瘴気・吐く・疫み・紅蓮）",
        StatusKeys.Marked => "敵の標は §1 の +50% を受ける弱体（守りの標は味方側にしか付かない）",
        StatusKeys.Stun => "敵に書く（大縛り・痺れ粉ほか）",
        StatusKeys.Burn => "敵に書く（火の粉・破裂・紅蓮）",
        StatusKeys.IdleTurn => "engine の帳簿（手番を失った印）。状態ではない",
        StatusKeys.Armor => "味方側の資源",
        StatusKeys.Wound => "敵に書く（刻み・薄刃ほか）",
        StatusKeys.Deep => "傷の束ね（敵にも付く）",
        StatusKeys.Curse => "敵に書く（ムド・ネル）",
        StatusKeys.Stagger => "敵に書く（突風・バネ）",
        StatusKeys.Confused => "敵に書く（喧噪）",
        StatusKeys.Ward => "味方側の資源（預かり）",
        StatusKeys.Debt => "味方側の代金（前借り）",
        StatusKeys.Ash => "保持者自身の在庫",
        StatusKeys.Grappled => "敵に書く（組み付き）",
        StatusKeys.Cowed => "敵に書く（見せしめ）",
        StatusKeys.Footing => "味方側の資源（据えの層）",
        StatusKeys.Daunted => "敵に書く（萎縮）",
        StatusKeys.Concentrated => "敵に書く（濃縮の印）",
        StatusKeys.Numbed => "敵に書く（痺れ毒）",
        StatusKeys.Guren => "保持者自身の在庫（紅蓮）",
        StatusKeys.Stigma => "敵に書く（聖痕）",
        StatusKeys.Plank => "味方側の資源（板の印）",
        _ => "?"
    };
}
