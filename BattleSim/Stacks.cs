using System.Text.RegularExpressions;
using BattleCore;

// =====================================================================================
// stacks モード（第134期） —— 重ね掛けの実測と、盤面ルールの対称性
//
// **この期は測定だけである。機構を1つも足さない。採否も無い。**
// 足したのは計数だけで、`compare` の 305 セルは `docs/balance.md` と 0 件差分で終わる。
//
// 測るもの1（段1） —— **燃焼の重ね掛け**
//   `BattleContext.Ignite` は残ターンを 3 に**上書きする（加算しない）**ので、
//   同じ駒に何度点けても燃焼は濃くならない。`BurnRules.Damage` のコメントは既に
//   「**1ターンあたりの固定ダメージ。層に依存しない。**」と書いていて、
//   **「層」という語が前提として置かれているのに実装が無い。**
//   第133期の実測（前列規則のせいでボルグは同じ敵を殴り続ける）は、
//   **体数は増えないが重ね掛けは起きている**ことを示している。**それが何回かを誰も数えていない。**
//
// 測るもの2（段2） —— **盤面ルールの対称性**（第132期の最大の発見の続き）
//   軛は「両陣営に等しくかかる」のに、第四波の敵の打点が全部 25 以下なので、実測は
//   **敵 7.68回/174.3点 対 味方 0.07回/0.4点 ＝ 実質プレイヤー専用の税**だった。
//   **対称性は規則ではなく数値で決まっている。** 残り3つ（渇き・粛・逆位）は未確認。
//
//     dotnet run --project BattleSim -c Release 0 stacks phase0  # Q0-1〜Q0-8（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 stacks burn    # 段1（重ね掛けの実測）
//     dotnet run --project BattleSim -c Release 0 stacks rules   # 段2（盤面ルールの対称性）
//     dotnet run --project BattleSim -c Release 0 stacks check [採用前のbalance.md]
// =====================================================================================

/// <summary>第134期の走査。<b>すべて実装から引く</b>。<b>走査が空なら止める</b>（第117期）。</summary>
static class StScan
{
    public static string Root { get; } = FindRoot();

    static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "BattleCore", "Traits.cs")))
            d = d.Parent;
        return d?.FullName ?? "";
    }

    public static string Read(string rel)
    {
        if (Root.Length == 0) return "";
        string p = Path.Combine(Root, rel);
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    /// <summary>件数を出力し、0 件なら偽を返す（呼び出し側が止める）。</summary>
    public static bool Guard(string what, int count)
    {
        Console.WriteLine($"走査 `{what}`: **{count} 件**"
            + (count == 0 ? " —— **0 件。異常として止める**（第117期）" : ""));
        return count > 0;
    }

    /// <summary>直前の宣言から型名を引く。<b>`class` だけを見ない</b>（第130期）。</summary>
    public static string TypeAt(string src, int index)
    {
        var ms = Regex.Matches(src[..index],
            @"\b(?:class|record\s+struct|record|struct|interface)\s+(\w+)");
        return ms.Count == 0 ? "?" : ms[^1].Groups[1].Value;
    }

    /// <summary>その位置を含むメソッド（<c>public override ...</c>）の名前を引く。</summary>
    public static string MemberAt(string src, int index)
    {
        var ms = Regex.Matches(src[..index], @"\b(?:public|private|internal|protected)[^\n]*?\b(\w+)\s*\(");
        return ms.Count == 0 ? "?" : ms[^1].Groups[1].Value;
    }
}

static class StacksDiag
{
    const int Seeds = 200;

    /// <summary>判定は第2〜5波（規約 (G10)。第一波は全行 100% の教習波なので分母に入れない）。</summary>
    static readonly int[] JudgeWaves = { 1, 2, 3, 4 };

    const int Foe = 0, Ally = 1;
    static readonly string[] SideName = { "敵", "味方" };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); break;
            case "burn": Stage1(); break;
            case "check": Check(arg); break;
            default:
                Console.WriteLine("使い方: stacks [phase0|burn|check]");
                break;
        }
    }

    // =================================================================================
    // Phase 0 —— 実装から引く。**戦闘0回。**
    // =================================================================================

    static void Phase0()
    {
        string engine = StScan.Read("BattleCore/BattleEngine.cs");
        string traits = StScan.Read("BattleCore/Traits.cs");
        string catalog = StScan.Read("BattleCore/UnitCatalog.cs");
        if (engine.Length == 0 || traits.Length == 0 || catalog.Length == 0)
        {
            Console.WriteLine("**実装の読み出しが空。止める**（第117期）。");
            return;
        }

        Console.WriteLine("# 第134期 Phase 0 —— 実装から引く（戦闘0回）");
        Console.WriteLine();

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1 —— `Ignite` の `relit` の用途を全件");
        Console.WriteLine();
        // **走査対象は `Ignite` の本体だけ**に切る（第123期 —— 走査する側のコードが
        // 自分で当たると「静かに違う表を作る」）。第134期に足した帳簿の側は下で別に数える。
        int ia = engine.IndexOf("public void Ignite(UnitState target", StringComparison.Ordinal);
        int ib = ia >= 0 ? engine.IndexOf("public const string DullKey", ia, StringComparison.Ordinal) : -1;
        string ignite = ia >= 0 && ib > ia ? engine[ia..ib] : "";
        if (!StScan.Guard("`Ignite` の本体（文字数）", ignite.Length)) return;
        var relit = Regex.Matches(ignite, @"\brelit\b");
        if (!StScan.Guard("`relit` の出現（`Ignite` の本体）", relit.Count)) return;
        Console.WriteLine();
        Console.WriteLine("| # | 行 | 何をしているか |");
        Console.WriteLine("|---|---|---|");
        int ri = 0;
        foreach (Match m in relit)
        {
            int line = engine[..(ia + m.Index)].Count(c => c == '\n') + 1;
            string text = LineAt(ignite, m.Index).Trim();
            string what = text.Contains("bool relit", StringComparison.Ordinal) ? "**判定そのもの**（既に燃えているか）"
                : text.StartsWith("if (relit)", StringComparison.Ordinal) ? "計数の分岐（`BurnRelit` / `BurnLit`）"
                : text.Contains("Log(", StringComparison.Ordinal) ? "ログの文言の切り替え"
                : text.Contains("NoteIgnite", StringComparison.Ordinal) ? "**第134期 段1 の帳簿**（計数専用）"
                : "その他";
            Console.WriteLine($"| {++ri} | `BattleEngine.cs:{line}` `{Trim(text, 62)}` | {what} |");
        }
        Console.WriteLine();
        Console.WriteLine("**盤面の分岐には1度も使われていない**——`relit` が決めるのは"
            + "(a) どちらの計数を回すか と (b) ログの文言だけで、`SetCounter` に渡す値は"
            + "`relit` に依らず `BurnRules.Turns`（＋滲み則の +1）である。"
            + "**計数を足す場所はここでよい。**");
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2 —— `StatusKeys.Burn` のカウンタの意味");
        Console.WriteLine();
        var sets = Regex.Matches(engine + traits, @"SetCounter\(\s*StatusKeys\.Burn\s*,\s*([^)]+)\)");
        if (!StScan.Guard("`SetCounter(StatusKeys.Burn, ...)` の全件", sets.Count)) return;
        Console.WriteLine();
        foreach (Match m in sets)
            Console.WriteLine($"- 書く値: `{m.Groups[1].Value.Trim()}`");
        Console.WriteLine();
        Console.WriteLine($"- `BurnRules.Turns` = **{BurnRules.Turns}** / `BurnRules.Damage` = **{BurnRules.Damage}**");
        Console.WriteLine();
        Console.WriteLine("**カウンタは残ターン数で、量ではない。** 書き込みは2箇所"
            + "（`Ignite` の上書きと、`TickStatuses` の `left - 1`）だけで、**加算する箇所が1つも無い。**");
        Console.WriteLine();
        Console.WriteLine("> **第135期の材料**: 層を作るなら `Burn` は残ターンのままにして、"
            + "**層は別キー**にするしかない。同じキーに載せると「残ターン 3」と「3層」が"
            + "区別できず、`TickStatuses` の `left - 1` が層を削ることになる。"
            + "**この期では足さない。**");
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3 —— 燃焼の書き手の全件（陣営別）");
        Console.WriteLine();
        var calls = Regex.Matches(traits, @"ctx\.Ignite\(([^;]*)\);");
        if (!StScan.Guard("`ctx.Ignite(` の呼び出し（`Traits.cs`）", calls.Count)) return;
        Console.WriteLine();
        Console.WriteLine("| # | 特性 | フック | 向き |");
        Console.WriteLine("|---|---|---|---|");
        int ci = 0;
        foreach (Match m in calls)
        {
            string arg0 = m.Groups[1].Value;
            bool friendly = arg0.Contains("friendly: true", StringComparison.Ordinal);
            Console.WriteLine($"| {++ci} | `{StScan.TypeAt(traits, m.Index)}` | `{StScan.MemberAt(traits, m.Index)}` "
                + $"| {(friendly ? "**味方へ**" : "敵へ（主目標）")} |");
        }
        Console.WriteLine();

        // 保持者を陣営別に数える
        var holdersAlly = UnitCatalog.All
            .Where(d => d.Traits.Any(t => t is TraitId.Cinder or TraitId.Bomber or TraitId.Pyre or TraitId.Favor))
            .ToList();
        var stageDefs = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def)).Distinct().ToList();
        var holdersFoe = stageDefs
            .Where(d => d.Traits.Any(t => t is TraitId.Cinder or TraitId.Bomber or TraitId.Pyre or TraitId.Favor))
            .ToList();
        Console.WriteLine($"- 燃焼に関わる札（`Cinder` / `Bomber` / `Pyre` / `Favor`）を持つ**味方の駒**: "
            + $"**{holdersAlly.Count} 枚** —— {string.Join(" / ", holdersAlly.Select(d => d.Name))}");
        Console.WriteLine($"- 同・**敵の駒**（`EnemyCatalog.Stages` に立つ {stageDefs.Count} 種のうち）: "
            + $"**{holdersFoe.Count} 枚**"
            + (holdersFoe.Count == 0 ? " —— **敵側に着火する駒は1枚もいない**" : ": " + string.Join(" / ", holdersFoe.Select(d => d.Name))));
        Console.WriteLine();
        Console.WriteLine("**燃焼は味方専用の通貨である**——供給が味方にしか無いので、"
            + "「敵に点いた火」も「味方に点いた火」も**すべて味方の駒が書いたもの**になる。"
            + "段1 の陣営別は「誰が書いたか」ではなく**「どちらに溜まったか」**を測っている。");
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4 —— 渇き・粛・逆位の判定位置");
        Console.WriteLine();
        Console.WriteLine("| ルール | engine のどこで効くか | 陣営別に数えられるか |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine($"| 軛（`Yoke`） | `ApplyDamage` の `target.Hp -= amount` の直前 | **済**（第132期・`YokeLedger`） |");
        Console.WriteLine($"| 渇き（`Drought`） | `Heal` の入口（`DroughtBinding` で早期 return） | **○**（止められた駒の `TeamId`） |");
        Console.WriteLine($"| 粛（`Hush`） | `CanActOutOfTurn` の最後 | **○**（振ろうとした駒の `TeamId`） |");
        Console.WriteLine($"| 逆位（`Inversion`） | `Run` の `order` を組む直前（速さの向き） | **×**（順序は陣営に属さない） |");
        Console.WriteLine();
        int inv = Regex.Matches(engine, @"TraitId\.Inversion").Count;
        if (!StScan.Guard("`TraitId.Inversion` の出現（`BattleEngine.cs`）", inv)) return;
        Console.WriteLine();
        Console.WriteLine("**逆位だけは `yoke map` と同じ形に載らない**——軛・渇き・粛は"
            + "「誰の何を止めたか」を数えられるが、**行動順は盤面全体の1つの性質**で、"
            + "「味方の順序を何回入れ替えたか」は課税の量にならない。");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5 —— 各ルールの保持者と、出る波");
        Console.WriteLine();
        Console.WriteLine("| 波 | 軛 | 渇き | 粛 | 逆位 |");
        Console.WriteLine("|---|---|---|---|---|");
        var totals = new int[4];
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
        {
            var defs = EnemyCatalog.Stages[w].Enemy.Occupied().Select(o => o.Def).ToList();
            int[] n =
            {
                defs.Count(d => d.Traits.Contains(TraitId.Yoke)),
                defs.Count(d => d.Traits.Contains(TraitId.Drought)),
                defs.Count(d => d.Traits.Contains(TraitId.Hush)),
                defs.Count(d => d.Traits.Contains(TraitId.Inversion)),
            };
            for (int i = 0; i < 4; i++) totals[i] += n[i];
            Console.WriteLine($"| {EnemyCatalog.Stages[w].Name} | {n[0]} | {n[1]} | {n[2]} | {n[3]} |");
        }
        Console.WriteLine($"| **合計** | **{totals[0]}** | **{totals[1]}** | **{totals[2]}** | **{totals[3]}** |");
        Console.WriteLine();
        int allyRule = UnitCatalog.All.Count(d => d.Traits.Any(t =>
            t is TraitId.Yoke or TraitId.Drought or TraitId.Hush or TraitId.Inversion));
        Console.WriteLine($"- 盤面ルールの札を持つ**味方の駒**: **{allyRule} 枚**"
            + (allyRule == 0 ? " —— **0 枚。盤面ルールは敵しか持たない**" : ""));
        Console.WriteLine();
        Console.WriteLine(totals[3] == 0
            ? "> **逆位（`Inversion`）の保持者は `Stages` に 0 体**（`Inverter` / `Reverser` はどちらも"
              + "宣言されているだけで波に立っていない）。**測る対象から外す**（予測 P5 ○）。"
            : "> **逆位の保持者が波に立っている。P5 は外れ。**");
        Console.WriteLine();

        // ---- Q0-6 ----
        Console.WriteLine("## Q0-6 —— 渇きが封じた回復量を数えられるか");
        Console.WriteLine();
        var healCalls = Regex.Matches(traits, @"ctx\.Heal\(");
        if (!StScan.Guard("`ctx.Heal(` の呼び出し（`Traits.cs`）", healCalls.Count)) return;
        int engineHeal = Regex.Matches(engine, @"\bHeal\(").Count;
        Console.WriteLine($"- `BattleEngine.cs` 側の `Heal(` の出現: **{engineHeal} 件**（宣言と内部呼び出しを含む）");
        Console.WriteLine();
        Console.WriteLine($"> **`CLAUDE.md` は第39期以来「`ctx.Heal` を呼ぶのは9経路」と書いているが、"
            + $"走査では {healCalls.Count} 経路ある。** 第39期（7→8→9）の数え方のまま"
            + "更新されていない——**Q0-7 の「4本だけ」と同じ形の取り残し**で、"
            + "どちらも**規則を足したのに説明文が増えていない。**");
        Console.WriteLine();
        Console.WriteLine("**回復の単一窓口は `BattleContext.Heal` で、渇きはその入口1箇所で止めている。**"
            + "止めた量の計数は**第133期まで 0 件**だったので、第134期 段2 で `NoteDroughtBlocked` を足した"
            + "（**誰も読んで分岐しない**）。");
        Console.WriteLine();
        Console.WriteLine("> **要求量と実効量を分けて数える。** 満タンの駒への回復は渇きが無くても1点も入らない"
            + "（`Heal` は `Hp == before` で抜ける）ので、**要求量だけを数えると「封じられた量」を上振れさせる。**");
        Console.WriteLine();

        // ---- Q0-7 ----
        Console.WriteLine("## Q0-7 —— 粛が止めたターン外の行動を数えられるか");
        Console.WriteLine();
        var outOfTurn = Regex.Matches(traits, @"ctx\.CanActOutOfTurn\(");
        if (!StScan.Guard("`ctx.CanActOutOfTurn(` の呼び出し（`Traits.cs`）", outOfTurn.Count)) return;
        Console.WriteLine();
        Console.WriteLine("| # | 特性 | フック | 保持者（味方） | 保持者（敵・`Stages`） |");
        Console.WriteLine("|---|---|---|---|---|");
        var routeTraits = new (string Cls, TraitId Id)[]
        {
            ("ThornsTrait", TraitId.Thorns), ("AvengeTrait", TraitId.Avenge),
            ("DisplacedTrait", TraitId.Displaced), ("PursuerTrait", TraitId.Pursuer),
            ("TaillightTrait", TraitId.Taillight),
        };
        int oi = 0;
        foreach (Match m in outOfTurn)
        {
            string cls = StScan.TypeAt(traits, m.Index);
            var hit = routeTraits.FirstOrDefault(r => r.Cls == cls);
            int a = hit.Cls is null ? -1 : UnitCatalog.All.Count(d => d.Traits.Contains(hit.Id));
            int f = hit.Cls is null ? -1 : stageDefs.Count(d => d.Traits.Contains(hit.Id));
            Console.WriteLine($"| {++oi} | `{cls}` | `{StScan.MemberAt(traits, m.Index)}` "
                + $"| {(a < 0 ? "?" : a + " 枚")} | {(f < 0 ? "?" : f + " 枚")} |");
        }
        Console.WriteLine();
        int foeRoutes = routeTraits.Sum(r => stageDefs.Count(d => d.Traits.Contains(r.Id)));
        Console.WriteLine($"- **敵側にこの {outOfTurn.Count} 本を持つ駒: {foeRoutes} 枚**"
            + (foeRoutes == 0
                ? " —— **粛は在庫の側で非対称。実効は 味方 100% / 敵 0%**（予測 P4）"
                : ""));
        Console.WriteLine();
        Console.WriteLine($"> **`CLAUDE.md` は「止まるのはこの窓口を通る4本だけ」と書いているが、走査では {outOfTurn.Count} 本ある**"
            + " ——**第110期の譲渡（尾灯・`TaillightTrait`）が5本目**で、"
            + "第27期以来の一文が**24期ぶんのあいだ更新されていなかった。**"
            + "第122期「規則を降ろすと、その規則を前提に書かれた説明文が静かに嘘になる」の**逆側**"
            + "（規則を足したのに説明文が増えていない）。");
        Console.WriteLine();

        // ---- Q0-8 ----
        Console.WriteLine("## Q0-8 —— 過去に重ね掛けを数えた期が無いか");
        Console.WriteLine();
        string design = Path.Combine(StScan.Root, "design");
        var files = Directory.Exists(design) ? Directory.GetFiles(design, "*.md") : Array.Empty<string>();
        if (!StScan.Guard("`design/*.md`", files.Length)) return;
        var hits2 = new List<string>();
        foreach (string f in files)
        {
            if (Path.GetFileName(f).StartsWith("PHASE134", StringComparison.Ordinal)) continue;
            string body = File.ReadAllText(f);
            int n = Regex.Matches(body, "重ね掛け|点け直|BurnRelit|煽られた").Count;
            if (n > 0) hits2.Add($"  - `design/{Path.GetFileName(f)}` **{n} 件**");
        }
        Console.WriteLine($"- 「重ね掛け」「点け直」「`BurnRelit`」「煽られた」を含む報告書: **{hits2.Count} 本**");
        foreach (string h in hits2) Console.WriteLine(h);
        Console.WriteLine();
        Console.WriteLine("**第57期が `BurnLit` / `BurnRelit` の総数を数えている**（受け手の `UnitTally`）。"
            + "**区間ごとの分布を数えた期は 0 本**——「何回煽られたか」の総数はあるが、"
            + "「**1回燃えてから消えるまでに何回煽られたか**」は誰も出していない。"
            + "**層が積むかどうかはその分布でしか決まらない。**");
        Console.WriteLine();
    }

    static string LineAt(string s, int i)
    {
        int a = s.LastIndexOf('\n', i) + 1;
        int b = s.IndexOf('\n', i);
        return b < 0 ? s[a..] : s[a..b];
    }

    static string Trim(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    // =================================================================================
    // 段1 —— 重ね掛けの実測
    // =================================================================================

    sealed class BurnAcc
    {
        public readonly long[] Lit = new long[2], Relit = new long[2];
        public readonly long[] Episodes = new long[2], RelitSum = new long[2];
        public readonly long[] Max = new long[2];
        public readonly long[][] Hist = { new long[BattleContext.BurnHistBuckets], new long[BattleContext.BurnHistBuckets] };
        public readonly long[] Expired = new long[2], Death = new long[2], Alive = new long[2];
        public readonly Dictionary<string, (long Lit, long Relit)> By = new();
        public readonly Dictionary<string, (long Lit, long Relit)> On = new();
        public int Battles;

        public void Add(BurnLedger b)
        {
            Battles++;
            for (int s = 0; s < 2; s++)
            {
                Lit[s] += b.Lit[s]; Relit[s] += b.Relit[s];
                Episodes[s] += b.Episodes[s]; RelitSum[s] += b.RelitSum[s];
                if (b.RelitMax[s] > Max[s]) Max[s] = b.RelitMax[s];
                Expired[s] += b.EndExpired[s]; Death[s] += b.EndDeath[s]; Alive[s] += b.EndAlive[s];
                for (int k = 0; k < BattleContext.BurnHistBuckets; k++) Hist[s][k] += b.Hist[s][k];
            }
            foreach (var kv in b.By)
            {
                By.TryGetValue(kv.Key, out var a);
                By[kv.Key] = (a.Lit + kv.Value.Lit, a.Relit + kv.Value.Relit);
            }
            foreach (var kv in b.On)
            {
                On.TryGetValue(kv.Key, out var a);
                On[kv.Key] = (a.Lit + kv.Value.Lit, a.Relit + kv.Value.Relit);
            }
        }

        public long Fires => Lit.Sum() + Relit.Sum();
        public long EpisodesAll => Episodes.Sum();
        public double Mean(int s) => Episodes[s] > 0 ? (double)RelitSum[s] / Episodes[s] : 0.0;
        public double MeanAll => EpisodesAll > 0 ? (double)RelitSum.Sum() / EpisodesAll : 0.0;
    }

    /// <summary>燃焼を含む行（ボルグ＝火の粉 ／ ゾト＝破裂の着火）。</summary>
    static bool BurnRow(Formation f)
        => f.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Cinder) || o.Def.Traits.Contains(TraitId.Bomber));

    static void Stage1()
    {
        Console.WriteLine("# 第134期 段1 —— 燃焼の重ね掛けの実測");
        Console.WriteLine();
        Console.WriteLine("**区間の定義**: 火が点いていない駒に点いた瞬間に開き、"
            + "(a) 残ターンが 0 まで落ちた（**燃え尽き**）／(b) 燃えたまま倒れた（**死**）／"
            + "(c) 燃えたまま決着した（**決着**）のどれかで閉じる。"
            + "**1区間の「点け直し回数」は、その区間が開いてから閉じるまでに走った再付与の回数**"
            + "（0 なら一度も煽られずに燃え尽きた）。**戦闘単位ではなく区間単位。**");
        Console.WriteLine();
        Console.WriteLine($"分母: `compare` の**燃焼を含む行** × 第2〜5波（規約 (G10)）× seed 0..{Seeds - 1}。");
        Console.WriteLine();

        var rows = Presets.Compare.Where(r => BurnRow(r.F)).ToArray();
        if (!StScan.Guard("`compare` の燃焼を含む行", rows.Length)) return;
        Console.WriteLine();

        var all = new BurnAcc();
        var byWave = new BurnAcc[EnemyCatalog.Stages.Count];
        for (int w = 0; w < byWave.Length; w++) byWave[w] = new BurnAcc();
        var byRow = new Dictionary<string, BurnAcc>();

        foreach ((string name, Formation f) in rows)
        {
            var acc = byRow[name] = new BurnAcc();
            foreach (int w in JudgeWaves)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false);
                    acc.Add(r.Burns); all.Add(r.Burns); byWave[w].Add(r.Burns);
                }
        }

        // ---- 表A ----
        Console.WriteLine("## 表A —— 着火の総回数と重ね掛け（陣営別・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 区分 | 点いた（敵） | 煽った（敵） | 点いた（味方） | 煽った（味方） | 重ね掛け率 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        WriteA("**通算**", all);
        foreach (int w in JudgeWaves) WriteA(EnemyCatalog.Stages[w].Name, byWave[w]);
        Console.WriteLine();
        Console.WriteLine($"**重ね掛け率 ＝ 煽った ÷（点いた ＋ 煽った）** —— 捨てられている供給の割合。"
            + $"通算 **{Pct(all.Relit.Sum(), all.Fires)}**。");
        Console.WriteLine();

        // ---- 表B（P1 の主判定） ----
        Console.WriteLine("## 表B —— 区間ごとの点け直し回数の分布（**P1 の主判定**）");
        Console.WriteLine();
        Console.WriteLine("| 陣営 | 区間 | 平均 | 最大 | 0回 | 1回 | 2回 | 3回 | 4回 | 5回以上 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
        for (int s = 0; s < 2; s++) WriteB(SideName[s], all, s);
        Console.WriteLine($"| **両陣営** | {all.EpisodesAll} | **{all.MeanAll:F2}** | {all.Max.Max()} | "
            + string.Join(" | ", Enumerable.Range(0, BattleContext.BurnHistBuckets)
                .Select(k => Pct(all.Hist[0][k] + all.Hist[1][k], all.EpisodesAll))) + " |");
        Console.WriteLine();
        Console.WriteLine($"**P1 の線は「1区間あたり平均 2 回以上」**（1.5 未満なら層にしても積まない）。"
            + $"実測は **{all.MeanAll:F2} 回**（敵 {all.Mean(Foe):F2} ／ 味方 {all.Mean(Ally):F2}）"
            + $" —— **P1 {(all.MeanAll >= 2.0 ? "○" : "×")}**"
            + (all.MeanAll < 1.5 ? "（**1.5 未満。層は第135期の候補から落ちる**）" : "")
            + ".");
        Console.WriteLine();
        Console.WriteLine($"**P2 の線は「味方の重ね掛け > 敵の重ね掛け」。** "
            + $"1区間あたりでは 味方 {all.Mean(Ally):F2} 対 敵 {all.Mean(Foe):F2}、"
            + $"総回数では 味方 {Per(all.Relit[Ally], all.Battles)} 対 敵 {Per(all.Relit[Foe], all.Battles)} 回/戦"
            + $" —— **P2 {(all.Mean(Ally) > all.Mean(Foe) ? "○" : "×")}**（1区間あたりで判定）。");
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C —— 区間の閉じ方（陣営別・割合）");
        Console.WriteLine();
        Console.WriteLine("| 陣営 | 区間 | 燃え尽き | 燃えたまま死 | 燃えたまま決着 |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int s = 0; s < 2; s++)
            Console.WriteLine($"| {SideName[s]} | {all.Episodes[s]} | {Pct(all.Expired[s], all.Episodes[s])} "
                + $"| {Pct(all.Death[s], all.Episodes[s])} | {Pct(all.Alive[s], all.Episodes[s])} |");
        Console.WriteLine();
        Console.WriteLine("**「燃えたまま死」は火が仕事を終える前に相手が落ちた区間**——"
            + "第133期の「**火を増やす動作と火を消す動作が同じ一振りの中にある**」がここに出る。");
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D —— 駒別（上位10枚・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("### 点けた側（`ctx.Ignite` に付け手が渡ったぶん）");
        Console.WriteLine();
        WriteUnits(all.By, all.Battles, "点けた", "煽った");
        Console.WriteLine("### 点けられた側");
        Console.WriteLine();
        WriteUnits(all.On, all.Battles, "点いた", "煽られた");

        // ---- 表E ----
        Console.WriteLine("## 表E —— 行別（燃焼を含む行だけ）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 着火/戦 | 煽り/戦 | 重ね掛け率 | 区間/戦 | 平均点け直し | 最大 | 味方の区間の割合 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach ((string name, _) in rows)
        {
            BurnAcc a = byRow[name];
            Console.WriteLine($"| {name} | {Per(a.Lit.Sum(), a.Battles)} | {Per(a.Relit.Sum(), a.Battles)} "
                + $"| {Pct(a.Relit.Sum(), a.Fires)} | {Per(a.EpisodesAll, a.Battles)} | **{a.MeanAll:F2}** "
                + $"| {a.Max.Max()} | {Pct(a.Episodes[Ally], a.EpisodesAll)} |");
        }
        Console.WriteLine();
    }

    static void WriteA(string label, BurnAcc a)
        => Console.WriteLine($"| {label} | {Per(a.Lit[Foe], a.Battles)} | {Per(a.Relit[Foe], a.Battles)} "
            + $"| {Per(a.Lit[Ally], a.Battles)} | {Per(a.Relit[Ally], a.Battles)} | {Pct(a.Relit.Sum(), a.Fires)} |");

    static void WriteB(string label, BurnAcc a, int s)
        => Console.WriteLine($"| {label} | {a.Episodes[s]} | **{a.Mean(s):F2}** | {a.Max[s]} | "
            + string.Join(" | ", Enumerable.Range(0, BattleContext.BurnHistBuckets)
                .Select(k => Pct(a.Hist[s][k], a.Episodes[s]))) + " |");

    static void WriteUnits(Dictionary<string, (long Lit, long Relit)> d, int battles, string c1, string c2)
    {
        Console.WriteLine($"| 駒 | {c1}/戦 | {c2}/戦 | 重ね掛け率 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var kv in d.OrderByDescending(x => x.Value.Lit + x.Value.Relit).Take(10))
            Console.WriteLine($"| {NameOf(kv.Key)} | {Per(kv.Value.Lit, battles)} | {Per(kv.Value.Relit, battles)} "
                + $"| {Pct(kv.Value.Relit, kv.Value.Lit + kv.Value.Relit)} |");
        Console.WriteLine();
    }

    static string NameOf(string id)
    {
        UnitDef? d = UnitCatalog.All.FirstOrDefault(x => x.Id == id)
                     ?? EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def))
                                           .FirstOrDefault(x => x.Id == id);
        return d is null ? $"`{id}`" : $"{d.Name}（`{id}`）";
    }

    static string Per(long n, int battles) => battles == 0 ? "—" : $"{(double)n / battles:F2}";
    static string Pct(long n, long d) => d == 0 ? "—" : $"{n * 100.0 / d:F1}%";

    // =================================================================================
    // 自己検査
    // =================================================================================

    static void Check(string before)
    {
        Console.WriteLine("# 第134期 —— 自己検査");
        Console.WriteLine();
        string path = before.Length > 0 ? before : Path.Combine(StScan.Root, "docs", "balance.md");

        Console.WriteLine("## A1 / 必須1 —— `compare` 305 セルが `docs/balance.md` と 0 件（規約 (G8)）");
        Console.WriteLine();
        CompareToFile(path);

        string engine = StScan.Read("BattleCore/BattleEngine.cs");
        string traits = StScan.Read("BattleCore/Traits.cs");
        if (engine.Length == 0 || traits.Length == 0)
        {
            Console.WriteLine("**実装の読み出しが空。止める**（第117期）。");
            return;
        }

        Console.WriteLine("## A2 —— engine に規則も窓口も 0 本（足したのは計数だけ）");
        Console.WriteLine();
        var notes = Regex.Matches(engine, @"\b(NoteIgnite|CloseBurnEpisode|CloseBurnLedger)\b");
        if (!StScan.Guard("第134期に足した計数の呼び出し・宣言", notes.Count)) return;
        Console.WriteLine();
        foreach (var g in notes.Select(m => m.Value).GroupBy(x => x).OrderBy(g => g.Key))
            Console.WriteLine($"- `{g.Key}` **{g.Count()} 件**（宣言 1 ＋ 呼び出し {g.Count() - 1}）");
        Console.WriteLine();
        // **帳簿の外**で帳簿の値を読んでいる箇所を数える。**第134期のブロック自身は除く**
        // ——`NoteRuleHolders` は `RuleFallTurn` を二重計上しないために自分で読む（計数の内輪）。
        // 第123期「走査対象が走査する側のコード自身なら、当たるのは自分」の engine 側の版。
        string outside = engine.Replace(NewCode(engine), "");
        int branch = Regex.Matches(outside,
            @"\b(BurnLitSide|BurnRelitSide|BurnEpisodes|BurnRelitSum|BurnHist|BurnEndExpired)\b").Count;
        int built = Regex.Matches(engine[engine.IndexOf("Burns = new BurnLedger", StringComparison.Ordinal)..],
            @"\b(BurnLitSide|BurnRelitSide|BurnEpisodes|BurnRelitSum|BurnHist|BurnEndExpired)\b").Count;
        Console.WriteLine($"第134期のブロックの**外**で帳簿の配列を読んでいる箇所: **{branch} 件**"
            + $"（うち `Run` の末尾で `BattleResult` へ写している {built} 件） —— "
            + (branch == built ? "**A2 ○**（写す以外に読む箇所が 1 つも無い ＝ どの規則も帳簿を読まない）" : "**A2 ×**"));
        Console.WriteLine();
        int traitRef = Regex.Matches(traits, @"\b(BurnLedger|BoardRuleLedger|NoteIgnite)\b").Count;
        Console.WriteLine($"`Traits.cs` からの帳簿の参照: **{traitRef} 件** —— "
            + (traitRef == 0 ? "**○**（特性側は1つも読まない）" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("## A4 —— 陣営別に出ているか");
        Console.WriteLine();
        int sides = Regex.Matches(engine, @"SideOf\(").Count;
        Console.WriteLine($"`SideOf(` の出現 **{sides} 件** —— 帳簿の陣営の添字はすべてこの1箇所から引いている"
            + $"（0 = 敵 / 1 = 味方。第132期の `YokeLedger` と同じ向き） —— **{(sides > 1 ? "A4 ○" : "A4 ×")}**");
        Console.WriteLine();

        Console.WriteLine("## 必須4 —— `ctx.PickOne` を新たに使っていない（規約 (G8)）");
        Console.WriteLine();
        // **検索文字列は連結で組む**（第123期）——この走査が読むのは `BattleCore` 側だが、
        // 第132期に「走査対象の本文に型名を書くと自己参照ができる」を踏んでいるので作法を揃える。
        string needle = "Pick" + "One";
        string roll = "Roll" + "(";
        string added = NewCode(engine);
        if (!StScan.Guard("第134期に足した engine のブロック（文字数）", added.Length)) return;
        int pick = Regex.Matches(added, needle).Count;
        int rolls = Regex.Matches(added, Regex.Escape(roll)).Count;
        Console.WriteLine($"第134期に足した engine のブロックの中の `{needle}` **{pick} 件** ／ "
            + $"`{roll}` **{rolls} 件** —— "
            + (pick == 0 && rolls == 0 ? "**○**（乱数を1つも引かない）" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("## A10 —— `DemoApp` に差分が無い");
        Console.WriteLine();
        int demo = 0;
        string dd = Path.Combine(StScan.Root, "DemoApp");
        if (Directory.Exists(dd))
            foreach (string f in Directory.GetFiles(dd, "*.cs", SearchOption.AllDirectories))
                demo += Regex.Matches(File.ReadAllText(f),
                    "BurnLedger").Count;
        Console.WriteLine($"`DemoApp` の第134期の型・窓口の出現 **{demo} 件** —— {(demo == 0 ? "**A10 ○**" : "**A10 ×**")}");
        Console.WriteLine();

        Console.WriteLine("## 帳簿が閉じるか（段1 の検算）");
        Console.WriteLine();
        long open = 0, closed = 0, lit = 0;
        var rows = Presets.Compare.Where(r => BurnRow(r.F)).ToArray();
        foreach ((_, Formation f) in rows)
            foreach (int w in JudgeWaves)
                for (int seed = 0; seed < 40; seed++)
                {
                    BurnLedger b = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false).Burns;
                    lit += b.Lit.Sum();
                    closed += b.Episodes.Sum();
                    open += b.Relit.Sum();
                }
        Console.WriteLine($"- 点いた回数（＝開いた区間の数） **{lit}** / 閉じた区間の数 **{closed}** —— "
            + (lit == closed ? "**○**（開いた区間はすべて閉じている）" : "**×**"));
        Console.WriteLine($"- 煽った回数 **{open}**（区間の点け直し回数の総和と一致するはず）");
        Console.WriteLine();
    }

    /// <summary>第134期に足したブロックだけを切り出す（`PickOne` の走査用）。</summary>
    static string NewCode(string engine)
    {
        int a = engine.IndexOf("第134期 段1・段2 —— 重ね掛けと盤面ルール", StringComparison.Ordinal);
        int b = engine.IndexOf("上限がいま効いているか", StringComparison.Ordinal);
        return a >= 0 && b > a ? engine[a..b] : "";
    }

    static void CompareToFile(string path)
    {
        if (!File.Exists(path)) { Console.WriteLine($"`{path}` が無い。**止める**。"); return; }
        var want = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal) || !line.Contains('%')) continue;
            var c = line.Split('|').Select(x => x.Trim()).ToList();
            if (c.Count < 2 + EnemyCatalog.Stages.Count) continue;
            var v = new double[EnemyCatalog.Stages.Count];
            bool ok = true;
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                if (!double.TryParse(c[2 + w].TrimEnd('%'), out v[w])) ok = false;
            if (ok) want[c[1]] = v;
        }
        int diff = 0, cells = 0;
        var moved = new List<string>();
        foreach ((string name, Formation f) in Presets.Compare)
        {
            if (!want.TryGetValue(name, out double[]? exp)) continue;
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                cells++;
                double got = wins * 100.0 / Seeds;
                if (Math.Abs(got - exp[w]) > 1e-9)
                {
                    diff++;
                    moved.Add($"  - {name} 第{w + 1}波: {exp[w]:F1}% → {got:F1}%（{got - exp[w]:+0.0;-0.0}pt）");
                }
            }
        }
        Console.WriteLine($"現行（既定）対 `{Path.GetFileName(path)}`: **{cells} セル中 {diff} 件のずれ** —— "
            + (diff == 0 ? "**○**" : "**×**"));
        foreach (string m in moved) Console.WriteLine(m);
        Console.WriteLine();
    }
}
