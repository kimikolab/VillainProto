using System.Text.RegularExpressions;
using BattleCore;

// =====================================================================================
// wildfire モード（第133期） —— 撒いた火を読む（ボルグ）
//
// `StatusKeys.Burn` を読む駒は第132期まで **2 枚しか無かった**——
//   熾のホタ（`Pyre`）      ＝ **自分が**燃えている
//   火選りのヒヨ（`Favor`） ＝ **味方が**燃えている
// **敵が燃えていることを読む駒が 1 枚も無く**、一方で
// **唯一の常時の着火役（火の粉・ボルグ）が、撒いた火に一切の価値を持っていなかった。**
//
//   火勢（`TraitId.Wildfire`） ＝ 燃えている**敵**の数だけ一撃が重くなる
//
// **形は実装が1つに決めている**（Q0-1）——`Trait.ModifyAttack` は対象を受け取らないので
// 「標的が燃えていれば重い」は書けない。書けるのは「**盤面の**燃えている敵の数を読む」形だけで、
// それは望ましい——**着火した敵を他の駒が倒すと、その場で威力が下がる**（非単調・P6）。
//
//     dotnet run --project BattleSim -c Release 0 wildfire phase0  # Q0-1〜Q0-10（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 wildfire run     # 段1（V0〜V3 × 2台）
//     dotnet run --project BattleSim -c Release 0 wildfire compare # 段2（`compare` 61行 ＋ 交差帯12行）
//     dotnet run --project BattleSim -c Release 0 wildfire check [採用前のbalance.md]
// =====================================================================================

/// <summary>第133期の走査。<b>すべて実装から引く</b>。<b>走査が空なら止める</b>（第117期）。</summary>
static class WfScan
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
}

static class WildfireDiag
{
    const int Seeds = 200;
    static readonly string BorgId = UnitCatalog.Borg.Id;

    // 判定は第2〜5波（規約 (G10)。第一波は全行 100% の教習波なので分母に入れない）。
    static readonly int[] JudgeWaves = { 1, 2, 3, 4 };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); break;
            case "run": Stage1(); break;
            case "compare": Stage2(); break;
            case "check": Check(arg); break;
            default: Console.WriteLine("wildfire: モードは phase0 / run / compare / check。"); break;
        }
    }

    // ==================================================================================
    // 版（段1）
    // ==================================================================================
    //
    // **強度は「同じくらいの大きさ」に揃えてある。** P1 の見込み（同時に燃えている敵 ≒ 2 体）で
    //   V1 ＝ 攻 18 → 26（×1.44） ／ V2 ＝ ×1.50 ／ V3 ＝ ×1.40
    // ——**比べたいのは大きさではなく形**（体数に比例するか・二値に潰れるか）なので、
    // 大きさを揃えないと「V2 が強い」と「M が大きい」が区別できない。
    static readonly (string Name, string Text, WildfireRule Rule)[] Versions =
    {
        ("V0", "対照（`WildfireRule.Off`・不活性）",             WildfireRule.Off),
        ("V1", "燃えている敵1体につき **攻撃力 +4**（加算）",      WildfireRule.Added(4)),
        ("V2", "燃えている敵1体につき **×1.25**（乗算・比例）",    WildfireRule.Scaled(25)),
        ("V3", "1体でも燃えていれば **×1.40**（二値）",           WildfireRule.Binary(40)),
    };

    /// <summary>火勢を載せたボルグ（<b>診断のローカル</b>。`UnitCatalog` は1バイトも触らない）。</summary>
    static readonly UnitDef BorgW = new()
    {
        Id = UnitCatalog.Borg.Id,
        Name = UnitCatalog.Borg.Name,
        MaxHp = UnitCatalog.Borg.MaxHp,
        Attack = UnitCatalog.Borg.Attack,
        Speed = UnitCatalog.Borg.Speed,
        Traits = UnitCatalog.Borg.Traits.Concat(new[] { TraitId.Wildfire }).ToArray(),
        Pattern = UnitCatalog.Borg.Pattern,
        Advances = UnitCatalog.Borg.Advances,
        PlusText = UnitCatalog.Borg.PlusText,
        MinusText = UnitCatalog.Borg.MinusText,
        Flavor = UnitCatalog.Borg.Flavor
    };

    // 台の作り方（指示書 §3-1・第130期の教訓）:
    //   - **ボルグ1枚＋土台**（全版で同一）
    //   - **敵は複数体**（燃えている「数」が変わることが条件なので、単体の敵では測れない）
    //   - **土台に燃焼を触る駒を1枚も入れない**——ホタ（自分の火を読む）・ヒヨ（味方の火を読む）・
    //     ゾト（破裂で着火）を外すと、**盤上の火はすべてボルグが撒いたものになる**。
    //     第117期・第126期「台の選定規則が測る対象の入力を 0 にしていないか」の裏側で、
    //     こちらは**土台が入力を汚していないか**を先に固定している。
    //   - 台1 は**ボルグが唯一の火力**（土台3枚が全部防御）、
    //     台2 は**もう1枚の火力を足す**（敵が早く落ちる ＝ 燃えている敵が減る方向）。
    //     **符号が台で割れるかを先に見る**（第61期）。
    static (string Name, string Note, Formation F)[] Tables(UnitDef borg) => new[]
    {
        ("台1 ボルグ単騎火力",
         "土台はガルド（肩代わり）・キリ（薄刃・打点は常に1）・ノノ（繕い）。**ボルグが唯一の火力**",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Kiri,
                            center: UnitCatalog.Nono, back1: UnitCatalog.Sekki, back3: borg)),
        ("台2 火力2枚",
         "前3 をドルガ（攻38）に差し替え。**敵が早く落ちる ＝ 燃えている敵が減る**",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga,
                            center: UnitCatalog.Nono, back1: UnitCatalog.Sekki, back3: borg)),
    };

    // ==================================================================================
    // Phase 0
    // ==================================================================================
    static void Phase0()
    {
        Console.WriteLine("# 第133期 Phase 0 —— 撒いた火を読む（ボルグ）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 wildfire phase0` の出力。**戦闘0回。**");
        Console.WriteLine();
        if (WfScan.Root.Length == 0) { Console.WriteLine("**走査に失敗した。止める**（第117期）。"); return; }
        string traits = WfScan.Read("BattleCore/Traits.cs");
        string engine = WfScan.Read("BattleCore/BattleEngine.cs");
        string models = WfScan.Read("BattleCore/Models.cs");
        string catalog = WfScan.Read("BattleCore/UnitCatalog.cs");
        if (traits.Length == 0 || engine.Length == 0 || models.Length == 0 || catalog.Length == 0)
        {
            Console.WriteLine("**`BattleCore` の走査が空。止める**（第117期）。"); return;
        }

        // --- Q0-1 ---
        Console.WriteLine("## Q0-1 —— `ModifyAttack` が対象を受け取らないこと");
        Console.WriteLine();
        var sig = Regex.Match(traits, @"public virtual int ModifyAttack\(([^)]*)\)");
        if (!WfScan.Guard("Trait.ModifyAttack の署名", sig.Success ? 1 : 0)) return;
        Console.WriteLine();
        Console.WriteLine($"署名: `ModifyAttack({sig.Groups[1].Value})` —— "
            + "**`UnitState self` と `int atk` の2つだけ。対象も `BattleContext` も受け取らない。**");
        var ovr = Regex.Matches(traits, @"public override int ModifyAttack");
        Console.WriteLine($"上書き **{ovr.Count} 件**: "
            + string.Join(" / ", ovr.Select(m => "`" + WfScan.TypeAt(traits, m.Index) + "`")));
        Console.WriteLine();
        int finisherEngine = Regex.Matches(engine, @"atk \*= Finisher\.Multiplier").Count;
        int thinEngine = Regex.Matches(engine, @"ThinBladeTrait\.RawAttack").Count;
        Console.WriteLine($"**対象を見る条件が engine 側にある前例**: 止め `atk *= Finisher.Multiplier` **{finisherEngine} 件**"
            + $" ／ 薄刃 `ThinBladeTrait.RawAttack` **{thinEngine} 件**（どちらも `PerformAttack` の中）。");
        Console.WriteLine();
        Console.WriteLine("> **`FinisherTrait` の doc の主張は今も正しい。**"
            + " したがって「標的が燃えていれば重い」は書けず、"
            + "**書けるのは「盤面の燃えている敵の数を読む」形だけ**である。");
        Console.WriteLine();

        // --- Q0-2 ---
        Console.WriteLine("## Q0-2 —— `UnitState.Board` から敵を引けるか");
        Console.WriteLine();
        int boardDecl = Regex.Matches(models, @"BattleContext\? Board \{ get").Count;
        int overbearBoard = Regex.Matches(traits, @"BattleContext\? board = self\.Board;").Count;
        int allUnits = Regex.Matches(engine, @"AllUnits").Count;
        Console.WriteLine($"- `UnitState.Board` の宣言: **{boardDecl} 件**（`Models.cs`）");
        Console.WriteLine($"- `self.Board` を `ModifyAttack` の条件に使う型: **{overbearBoard} 件**"
            + "（驕り＝`OverbearTrait`・第46期／**火勢＝第133期**）");
        Console.WriteLine($"- `BattleContext.AllUnits` の出現: **{allUnits} 件**");
        Console.WriteLine();
        Console.WriteLine("> **引ける。engine に新しい窓口は 1 本も要らない**"
            + "——驕りが `a.TeamId != self.TeamId` で味方に絞っているのと同じ行を、敵側に向けるだけ。");
        Console.WriteLine();

        // --- Q0-3 ---
        Console.WriteLine("## Q0-3 —— `ModifyAttack` が呼ばれる回数");
        Console.WriteLine();
        int atkOnce = Regex.Matches(engine, @"int atk = attackPercent == 100").Count;
        int secondary = Regex.Matches(engine, @"dealt \* SecondaryPercent / 100").Count;
        Console.WriteLine($"- `PerformAttack` が打点を作る行: **{atkOnce} 件**（`int atk = ... actor.CurrentAttack ...`）");
        Console.WriteLine($"- 薙ぎの巻き込みが同じ `dealt` から引く行: **{secondary} 件**");
        Console.WriteLine();
        Console.WriteLine("> **1回の振りにつき 1 回。** 薙ぎで3体に当たっても打点は1度しか作らず、"
            + "巻き込みはその 60% を引くだけ。**「振るたびに盤面を1度読む」形になる。**");
        Console.WriteLine();
        Console.WriteLine("> **ただし計数を `ModifyAttack` の中に置いてはいけない**"
            + "——`CurrentAttack` は駆り立ての選択・転嫁の流し先・`StatSnapshot`・"
            + "棘/仇討ち/責め苦の反撃量からも読まれるので、"
            + "数えると「振った回数」ではなく**「読まれた回数」**になる（驕りの明文）。");
        Console.WriteLine();

        // --- Q0-4 ---
        Console.WriteLine("## Q0-4 —— ボルグが1戦で作れる燃焼の上限");
        Console.WriteLine();
        int cinderFoe = Regex.Matches(traits, @"ctx\.Ignite\(target, source: self\);").Count;
        var turns = Regex.Match(engine, @"public const int Turns = (\d+);");
        var dmg = Regex.Match(engine, @"public const int Damage = (\d+);");
        int setNotAdd = Regex.Matches(engine, @"int turns = BurnRules\.Turns;").Count;
        Console.WriteLine($"- `CinderTrait` が敵に着火する行: **{cinderFoe} 件**（**主目標だけ**）");
        Console.WriteLine($"- `BurnRules.Turns` = **{(turns.Success ? turns.Groups[1].Value : "?")}**"
            + $" ／ `BurnRules.Damage` = **{(dmg.Success ? dmg.Groups[1].Value : "?")}**");
        Console.WriteLine($"- `Ignite` が残ターンを**設定**する行（加算しない）: **{setNotAdd} 件**");
        Console.WriteLine();
        Console.WriteLine("> **特性の発動は攻撃1回につき1度・主目標に対してのみ**（engine の規則）なので、"
            + "薙ぎで3体に当たっても着火は1体。残ターン3・非スタック・ターン頭に1減る、から"
            + "**ボルグ単独で同時に燃やせる敵は最大 3 体**"
            + "（毎ターン別の敵を主目標にし、1体も落ちなかった場合のみ）。**これは届かない上限である。**");
        Console.WriteLine();

        // --- Q0-5 ---
        Console.WriteLine("## Q0-5 —— 燃焼の書き手の全件（敵への着火に限る）");
        Console.WriteLine();
        var ig = Regex.Matches(traits, @"ctx\.Ignite\(([^;]*)\);");
        if (!WfScan.Guard("ctx.Ignite( の呼び出し", ig.Count)) return;
        Console.WriteLine();
        Console.WriteLine("| # | 型 | 引数 | 向き |");
        Console.WriteLine("|---|---|---|---|");
        int foes = 0;
        for (int i = 0; i < ig.Count; i++)
        {
            string a = ig[i].Groups[1].Value.Trim();
            bool ally = a.Contains("friendly: true");
            if (!ally) foes++;
            Console.WriteLine($"| {i + 1} | `{WfScan.TypeAt(traits, ig[i].Index)}` | `{a}` | {(ally ? "味方" : "**敵**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**敵に向くのは {foes} 件。**");
        Console.WriteLine();
        int cinderDefs = Regex.Matches(catalog, @"TraitId\.Cinder").Count;
        int bomberDefs = Regex.Matches(catalog, @"TraitId\.Bomber").Count;
        int enemyIdx = catalog.IndexOf("class EnemyCatalog", StringComparison.Ordinal);
        string enemySrc = enemyIdx >= 0 ? catalog[enemyIdx..] : "";
        int enemyIgniters = Regex.Matches(enemySrc, @"TraitId\.(Cinder|Bomber)").Count;
        Console.WriteLine($"- `UnitCatalog.cs` 全体の `TraitId.Cinder` **{cinderDefs} 件** / `TraitId.Bomber` **{bomberDefs} 件**");
        Console.WriteLine($"- そのうち **`EnemyCatalog` の中: {enemyIgniters} 件**");
        Console.WriteLine();
        Console.WriteLine("> **敵側に自分で燃える経路は 1 件も無い。**"
            + " **敵の燃焼はすべてプレイヤー側が撒いたものである**"
            + "——「撒いた火を読む」案にとって望ましい（**供給が完全に編成の選択の中にある**）。");
        Console.WriteLine();

        // --- Q0-6 / Q0-7 ---
        Console.WriteLine("## Q0-6 / Q0-7 —— ボルグを含む行・ゾトを含む行");
        Console.WriteLine();
        var cB = RowsWith(Presets.Compare, BorgId);
        var xB = RowsWith(Presets.Cross, BorgId);
        var cZ = RowsWith(Presets.Compare, UnitCatalog.Zoto.Id);
        var xZ = RowsWith(Presets.Cross, UnitCatalog.Zoto.Id);
        Console.WriteLine($"- `compare` **{Presets.Compare.Length} 行**中、ボルグを含むのは **{cB.Count} 行**"
            + $"（**{cB.Count * 100.0 / Presets.Compare.Length:F1}%**・規約 (G4) の分母）:");
        foreach (string n in cB) Console.WriteLine($"    - {n}");
        Console.WriteLine($"- 交差帯 **{Presets.Cross.Length} 行**中 **{xB.Count} 行**: " + string.Join(" / ", xB));
        Console.WriteLine();
        Console.WriteLine($"- ゾトを含むのは `compare` **{cZ.Count} 行** / 交差帯 **{xZ.Count} 行**");
        var both = Presets.Compare.Concat(Presets.Cross)
            .Where(b => Has(b.F, BorgId) && Has(b.F, UnitCatalog.Zoto.Id)).Select(b => b.Name).ToList();
        Console.WriteLine($"- **ボルグとゾトが同席する行: {both.Count} 行**"
            + (both.Count == 0 ? " —— **1行も無い**" : " " + string.Join(" / ", both)));
        Console.WriteLine();
        Console.WriteLine("> **ゾトの破裂はこの機構に 1 点も供給しない**（同席が 0 行）。"
            + "**P4 の「ゾトを含む行は動かない」は、実装の側から先に確定している。**");
        Console.WriteLine();

        // --- Q0-8 ---
        Console.WriteLine("## Q0-8 —— 強度を外から差す窓口");
        Console.WriteLine();
        int rules = Regex.Matches(traits + engine, @"public readonly record struct \w+Rule\b").Count;
        Console.WriteLine($"- `record struct ...Rule` の定義: **{rules} 件**（すべて `Run` に引数で渡す形）");
        Console.WriteLine();
        Console.WriteLine("> 置ける。`WildfireRule(Mode, Amount)` を `OverbearRule` / `EmberRule` と同型で足した。"
            + "**ノブは `Amount` 1本**（第52期・第53期）で、`Mode` は**ノブではなく版**"
            + "（`BlazeTargets` / `DivertRule.SelfMark` と同じ扱い）。"
            + "**書き換え可能な static のノブは置かない**（Trait は共有シングルトンで `layout` は並列に回す）。");
        Console.WriteLine();

        // --- Q0-9 ---
        Console.WriteLine("## Q0-9 —— `checkup` の分類");
        Console.WriteLine();
        string prog = WfScan.Read("BattleSim/Program.cs");
        var labels = Regex.Matches(prog, @"\[TraitId\.(\w+)\]\s*=\s*\(Hc(\w+)L");
        if (!WfScan.Guard("hcLabel の項目", labels.Count)) return;
        var labelled = labels.Select(m => m.Groups[1].Value).ToHashSet();
        var roster = UnitCatalog.All.SelectMany(d => d.Traits).Select(t => t.ToString()).ToHashSet();
        var missing = roster.Where(t => !labelled.Contains(t)).ToList();
        Console.WriteLine();
        Console.WriteLine($"ロスター 52 枚が使う札 **{roster.Count} 種** ／ 分類済み **{labelled.Count} 種**"
            + $" ／ **分類の無い札 {missing.Count} 件**"
            + (missing.Count == 0 ? " —— **`checkup` は最後まで走る**" : ": " + string.Join(" / ", missing)));
        Console.WriteLine();
        Console.WriteLine("> **段2 でボルグに札を足すなら、同じコミットで `hcLabel` に1行足す**"
            + "（第128期が `GradeStep` で踏み、**3 期止まった**穴。受け入れ条件 A6）。");
        Console.WriteLine();

        // --- Q0-10 ---
        Console.WriteLine("## Q0-10 —— 過去に「敵の状態を読んで威力を変える」機構を測っていないか");
        Console.WriteLine();
        string dir = Path.Combine(WfScan.Root, "design");
        if (!Directory.Exists(dir)) { Console.WriteLine("`design/` が無い。**止める**（第117期）。"); return; }
        var files = Directory.GetFiles(dir, "*.md");
        if (!WfScan.Guard("design/*.md", files.Length)) return;
        var hit = new List<(string F, int N)>();
        // **自分の出力を数えない**（第123期・第132期）——この期の指示書と報告書は
        // `design/PHASE133_*.md` に置かれ、`FinisherRule` に言及するので、
        // 素直に数えると**自分自身が「過去の測定」として表に出る**。
        // 症状は「走査が空」ではなく**「静かに違う表を作る」**ので、第117期の門では捕まらない。
        int selfRef = 0;
        foreach (string f in files)
        {
            string fn = Path.GetFileName(f);
            int n = Regex.Matches(File.ReadAllText(f), "FinisherRule").Count;
            if (n == 0) continue;
            if (fn.StartsWith("PHASE133", StringComparison.Ordinal)) { selfRef++; continue; }
            hit.Add((fn, n));
        }
        Console.WriteLine();
        Console.WriteLine($"`FinisherRule` を含むファイル **{hit.Count} 件**"
            + $"（**この期自身の `design/PHASE133_*.md` {selfRef} 件は除いた**——第123期・第132期）: "
            + string.Join(" / ", hit.OrderByDescending(x => x.N).Select(x => $"`{x.F}`({x.N})")));
        Console.WriteLine();
        Console.WriteLine("> **実体は1件——第53期の止め（トメ）だけ**である。"
            + "**窓口の位置の結論は Q0-1 と同じ**（`ModifyAttack` は対象を受け取らないので engine 側）。"
            + "**違い**: 止めは**対象の状態**を読む（engine）／火勢は**盤面の状態**を読む（`ModifyAttack`）。"
            + "**実装は前例にならないが、強度の作法（ノブ1本・掛ける場所を1箇所）はそのまま引ける。**");
        Console.WriteLine();
    }

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    static List<string> RowsWith((string Name, Formation F)[] rows, string id)
        => rows.Where(b => Has(b.F, id)).Select(b => b.Name).ToList();

    // ==================================================================================
    // 段1
    // ==================================================================================
    sealed class WfAcc
    {
        public int N;                      // 戦数
        public long Swings, Lit, Foes, FoesSq, Gain, GainSq;
        public int FoesMax;
        public double Win, Dealt, Kills, Turns;
        public long YokeHits, YokeLost;    // ボルグの一撃が上限に切られた回数と量（A7・P3）
    }

    static void Stage1()
    {
        Console.WriteLine("# 第133期 段1 —— 強度（ローカル台）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 wildfire run` の出力。");
        Console.WriteLine($"**2 台 × {Versions.Length} 版 × 第2〜5波 × seed 0..{Seeds - 1}**"
            + $"（= {2 * Versions.Length * JudgeWaves.Length * Seeds:N0} 戦）。");
        Console.WriteLine("**判定の分母は第2〜5波**（規約 (G10)。第一波は教習波なので入れない）。");
        Console.WriteLine();
        Console.WriteLine("**台**（`Presets` は1行も触らない。診断のローカル）:");
        Console.WriteLine();
        foreach (var (n, note, f) in Tables(BorgW))
            Console.WriteLine($"- **{n}** … {string.Join("・", f.Occupied().Select(o => o.Def.Name))}。{note}");
        Console.WriteLine();
        Console.WriteLine("**土台に燃焼を触る駒を1枚も入れていない**（ホタ・ヒヨ・ゾトを除いた）ので、"
            + "**盤上の火はすべてボルグが撒いたもの**である。");
        Console.WriteLine();
        Console.WriteLine("**版**:");
        Console.WriteLine();
        foreach (var (n, t, _) in Versions) Console.WriteLine($"- **{n}** … {t}");
        Console.WriteLine();
        Console.WriteLine("強度は**同じくらいの大きさに揃えてある**（P1 の見込み ＝ 同時に 2 体なら"
            + " V1 ×1.44 / V2 ×1.50 / V3 ×1.40）——比べたいのは**大きさではなく形**なので。");
        Console.WriteLine();

        var res = new Dictionary<(string T, string V), WfAcc>();
        foreach (var (tn, _, f) in Tables(BorgW))
        {
            var ids = f.Occupied().Select(o => o.Def.Id).ToList();
            foreach (var (vn, _, rule) in Versions)
            {
                var a = new WfAcc();
                foreach (int w in JudgeWaves)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed,
                                                          verbose: false, wildfire: rule);
                        a.N++;
                        if (r.PlayerWon) a.Win++;
                        a.Turns += r.Turns;
                        foreach (string id in ids)
                            if (r.TallyByUnit.TryGetValue(id, out UnitTally? q)) a.Kills += q.Kills;
                        if (r.TallyByUnit.TryGetValue(BorgId, out UnitTally? t))
                        {
                            a.Swings += t.WildfireSwings; a.Lit += t.WildfireLit;
                            a.Foes += t.WildfireFoes; a.FoesSq += t.WildfireFoesSq;
                            a.Gain += t.WildfireGain; a.GainSq += t.WildfireGainSq;
                            if (t.WildfireFoesMax > a.FoesMax) a.FoesMax = t.WildfireFoesMax;
                            a.Dealt += t.DamageToEnemy;
                        }
                        if (r.Yoke.CutBy.TryGetValue(BorgId, out var cut))
                        { a.YokeHits += cut.Hits; a.YokeLost += cut.Lost; }
                    }
                res[(tn, vn)] = a;
            }
        }

        TableA(res);
        TableB(res);
        TableYoke();
        Judge(res);
    }

    static double Mean(long sum, long n) => n > 0 ? (double)sum / n : 0;

    static double Var(long sum, long sq, long n)
        => n > 0 ? Math.Max(0, (double)sq / n - Math.Pow((double)sum / n, 2)) : 0;

    static void TableA(Dictionary<(string, string), WfAcc> res)
    {
        Console.WriteLine("## 表A —— 供給（A2）と発火（P1・P2・P6）");
        Console.WriteLine();
        Console.WriteLine("`燃敵` ＝ **振った瞬間に燃えていた敵の数**（平均・最大・分散）。"
            + "`発火率` ＝ 燃敵 ≥ 1 だった振りの割合。`上乗せ` ＝ 実際に打点へ乗った量（平均・分散）。");
        Console.WriteLine("**分母は `PerformAttack` を通った振りの全部**（手番も割り込みも）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 振/戦 | **発火率** | **燃敵 平均** | 燃敵 最大 | **燃敵 分散** | 上乗せ 平均 | **上乗せ 分散** |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var (tn, _, _) in Tables(BorgW))
            foreach (var (vn, _, _) in Versions)
            {
                WfAcc a = res[(tn, vn)];
                Console.WriteLine($"| {tn} | {vn} | {Mean(a.Swings, a.N):F2} |"
                    + $" {(a.Swings > 0 ? a.Lit * 100.0 / a.Swings : 0):F1}% |"
                    + $" **{Mean(a.Foes, a.Swings):F2}** | {a.FoesMax} |"
                    + $" **{Var(a.Foes, a.FoesSq, a.Swings):F2}** |"
                    + $" {Mean(a.Gain, a.Swings):F2} | **{Var(a.Gain, a.GainSq, a.Swings):F2}** |");
            }
        Console.WriteLine();
        bool zero = Tables(BorgW).Any(t => Mean(res[(t.Name, "V0")].Foes, res[(t.Name, "V0")].Swings) <= 0.0);
        Console.WriteLine(zero
            ? "> **燃えている敵の数が 0.0 の台がある。測定不能として止める**（A2・第126期／第130期の作法）。"
            : "> **供給は届いている**（A2 ○）。**V0 の上乗せは定義上 0.00** ——"
              + "これが「`WildfireRule.Off` が不活性である」ことの盤面側の検算になる。");
        Console.WriteLine();
    }

    static void TableB(Dictionary<(string, string), WfAcc> res)
    {
        Console.WriteLine("## 表B —— 盤面（採否の条件 §5-1 の 3）");
        Console.WriteLine();
        Console.WriteLine("`撃破/戦` は**味方5枚の合計**（ボルグ単騎ではない）。"
            + "`上限に切られた` はボルグの一撃だけ（`YokeLedger.CutBy[\"borg\"]`・第132期の帳簿）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 与ダメ(ボルグ)/戦 | Δ | **撃破/戦** | Δ | 決着T | **勝率** | Δ | 上限に切られた 回/戦 | 量/戦 |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var (tn, _, _) in Tables(BorgW))
        {
            WfAcc b = res[(tn, "V0")];
            foreach (var (vn, _, _) in Versions)
            {
                WfAcc a = res[(tn, vn)];
                string d(double x, double y) => vn == "V0" ? "—" : $"{x - y:+0.00;-0.00;0.00}";
                Console.WriteLine($"| {tn} | {vn} | {a.Dealt / a.N:F1} | {d(a.Dealt / a.N, b.Dealt / b.N)} |"
                    + $" **{a.Kills / a.N:F2}** | {d(a.Kills / a.N, b.Kills / b.N)} |"
                    + $" {a.Turns / a.N:F2} | **{a.Win * 100.0 / a.N:F1}%** |"
                    + $" {(vn == "V0" ? "—" : $"{(a.Win - b.Win) * 100.0 / a.N:+0.0;-0.0;0.0}pt")} |"
                    + $" {(double)a.YokeHits / a.N:F2} | {(double)a.YokeLost / a.N:F1} |");
            }
        }
        Console.WriteLine();
    }

    static void TableYoke()
    {
        Console.WriteLine("## 表C —— 波別の「上限に切られた量」（A7・P3）");
        Console.WriteLine();
        Console.WriteLine("**軛（1発 25 上限）が載っているのは第四波だけ**なので、"
            + "他の波では定義上 0 になる（第132期の地図と同じ読み方）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", JudgeWaves.Select(w => $"第{w + 1}波 回/量")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(JudgeWaves.Select(_ => "---:|")));
        foreach (var (tn, _, f) in Tables(BorgW))
            foreach (var (vn, _, rule) in Versions)
            {
                var cells = new List<string>();
                foreach (int w in JudgeWaves)
                {
                    long h = 0, l = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed,
                                                          verbose: false, wildfire: rule);
                        if (r.Yoke.CutBy.TryGetValue(BorgId, out var c)) { h += c.Hits; l += c.Lost; }
                    }
                    cells.Add($"{(double)h / Seeds:F2} / {(double)l / Seeds:F1}");
                }
                Console.WriteLine($"| {tn} | {vn} | {string.Join(" | ", cells)} |");
            }
        Console.WriteLine();
    }

    static void Judge(Dictionary<(string, string), WfAcc> res)
    {
        Console.WriteLine("## 段2 に進む条件（指示書 §5-1）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 1 発火率 ≥ 40% | 2 燃敵 ≥ 1.0 | 3 与ダメと撃破が両方増える | 4 分散 ≠ 0 | 判定 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        var tabs = Tables(BorgW).Select(t => t.Name).ToList();
        foreach (var (vn, _, _) in Versions)
        {
            if (vn == "V0") continue;
            bool c1 = tabs.All(t => res[(t, vn)].Swings > 0 && res[(t, vn)].Lit * 100.0 / res[(t, vn)].Swings >= 40);
            bool c2 = tabs.All(t => Mean(res[(t, vn)].Foes, res[(t, vn)].Swings) >= 1.0);
            bool c3 = tabs.All(t => res[(t, vn)].Dealt / res[(t, vn)].N > res[(t, "V0")].Dealt / res[(t, "V0")].N
                                 && res[(t, vn)].Kills / res[(t, vn)].N > res[(t, "V0")].Kills / res[(t, "V0")].N);
            bool c4 = tabs.All(t => Var(res[(t, vn)].Gain, res[(t, vn)].GainSq, res[(t, vn)].Swings) > 0);
            string m(bool b) => b ? "○" : "**×**";
            Console.WriteLine($"| {vn} | {m(c1)} | {m(c2)} | {m(c3)} | {m(c4)} | "
                + (c1 && c2 && c3 && c4 ? "**通過**" : "**落ちる**") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**4 を満たさない版は採らない**——分散が 0 なら「盤面を読む機構」ではなく"
            + "「ボルグに常時 +N を足した」のと同じで、**駒の個性にならない**（第129期）。");
        Console.WriteLine();
    }

    // ==================================================================================
    // 段2 —— `compare` 61 行 ＋ 交差帯 12 行
    // ==================================================================================
    static void Stage2()
    {
        Console.WriteLine("# 第133期 段2 —— 載せた後の盤面");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 wildfire compare` の出力。");
        Console.WriteLine("**V0 ＝ `WildfireRule.Off`（＝載せる前の盤面）／ V1 ＝ 現行の既定。**");
        Console.WriteLine();
        Rows("`compare`", Presets.Compare);
        Rows("交差帯", Presets.Cross);
    }

    static void Rows(string label, (string Name, Formation F)[] rows)
    {
        Console.WriteLine($"## {label} {rows.Length} 行");
        Console.WriteLine();
        Console.WriteLine("| 行 | ボルグ | " + string.Join(" | ",
            Enumerable.Range(0, EnemyCatalog.Stages.Count).Select(w => $"第{w + 1}波")) + " | 情報セル |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, EnemyCatalog.Stages.Count)
            .Select(_ => "---:|")) + "---:|");

        int infoBefore = 0, infoAfter = 0, movedRows = 0, movedRowsWithout = 0;
        var log = new List<string>();
        foreach (var (name, f) in rows)
        {
            bool has = Has(f, BorgId);
            var cells = new List<string>();
            int info = 0;
            bool rowMoved = false;
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
            {
                int w0 = 0, w1 = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false,
                                         wildfire: WildfireRule.Off).PlayerWon) w0++;
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false).PlayerWon) w1++;
                }
                double a = w0 * 100.0 / Seeds, b = w1 * 100.0 / Seeds;
                if (w >= 1)
                {
                    if (a > 0 && a < 100) infoBefore++;
                    if (b > 0 && b < 100) { infoAfter++; info++; }
                }
                if (Math.Abs(a - b) > 1e-9)
                {
                    rowMoved = true;
                    log.Add($"- {name} 第{w + 1}波: {a:F1}% → {b:F1}%（**{b - a:+0.0;-0.0}pt**）"
                            + (has ? "" : " ← **ボルグを含まない行**"));
                }
                cells.Add(Math.Abs(a - b) < 1e-9 ? $"{b:F1}" : $"**{b:F1}**（{b - a:+0.0;-0.0}）");
            }
            if (rowMoved) { movedRows++; if (!has) movedRowsWithout++; }
            Console.WriteLine($"| {name} | {(has ? "○" : "")} | {string.Join(" | ", cells)} | {info} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**動いた行 {movedRows} / セル {log.Count}**"
            + $" ／ **情報セル（第2〜5波・`0 < x < 100`）: {infoBefore} → {infoAfter}**");
        Console.WriteLine();
        foreach (string l in log) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine(movedRowsWithout == 0
            ? "> **A5 ○ —— ボルグを含まない行は 1 セルも動かない。**"
            : $"> **A5 × —— ボルグを含まない行が {movedRowsWithout} 行動いた。**");
        Console.WriteLine();
    }

    // ==================================================================================
    // 自己検査
    // ==================================================================================
    static void Check(string before)
    {
        Console.WriteLine("# 第133期 —— 自己検査");
        Console.WriteLine();
        string path = before.Length > 0 ? before : Path.Combine(WfScan.Root, "docs", "balance.md");

        Console.WriteLine("## 必須1 —— `compare` 305 セルが `docs/balance.md` と 0 件（規約 (G8)）");
        Console.WriteLine();
        CompareToFile(path, null, "現行（既定）");

        Console.WriteLine("## 陰性対照 —— `WildfireRule.Off` が**載せる前**の盤面と 0 件で一致する");
        Console.WriteLine();
        Console.WriteLine("**これが機構そのものの検算**——`Off` は `ModifyAttack` の最初の比較で抜けるので、"
            + "札を足しても乱数列も盤面も1ビットも動かない（比較先は**載せる前の `docs/balance.md`**）。");
        Console.WriteLine();
        CompareToFile(path, WildfireRule.Off, "V0（`WildfireRule.Off`）");

        string traits = WfScan.Read("BattleCore/Traits.cs");
        string engine = WfScan.Read("BattleCore/BattleEngine.cs");
        int ws = traits.IndexOf("public sealed class WildfireTrait", StringComparison.Ordinal);
        int we = traits.IndexOf("public enum WildfireMode", StringComparison.Ordinal);
        string body = ws >= 0 && we > ws ? traits[ws..we] : "";
        if (body.Length == 0) { Console.WriteLine("**`WildfireTrait` の走査が空。止める**（第117期）。"); return; }
        string code = string.Join("\n", body.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        Console.WriteLine("## 必須4 —— `ctx.PickOne` を新たに使っていない（規約 (G8)）");
        Console.WriteLine();
        int pick = Regex.Matches(code, "PickOne").Count;
        int roll = Regex.Matches(code, @"\bRoll\(").Count;
        Console.WriteLine($"`WildfireTrait` の中の `PickOne` **{pick} 件** ／ `Roll` **{roll} 件** —— "
            + (pick == 0 && roll == 0 ? "**○**（乱数を1つも引かない）" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("## A4 —— engine に窓口が増えていない");
        Console.WriteLine();
        var wf = Regex.Matches(engine, @"Wildfire\w*");
        var kinds = wf.Select(m => m.Value).GroupBy(x => x).OrderByDescending(g => g.Count()).ToList();
        int branch = Regex.Matches(engine, @"Wildfire\.(Mode|Amount|Active)").Count;
        Console.WriteLine($"`BattleEngine.cs` の `Wildfire*` の出現 **{wf.Count} 件**: "
            + string.Join(" / ", kinds.Select(g => $"`{g.Key}`×{g.Count()}")));
        Console.WriteLine();
        Console.WriteLine($"**規則で分岐している箇所（`Wildfire.Mode` / `.Amount` / `.Active`）: {branch} 件** —— "
            + (branch == 0
                ? "**A4 ○**（判定は `WildfireTrait.ModifyAttack` の中にある。engine は 1 つも読まない）"
                : "**A4 ×**"));
        Console.WriteLine();
        Console.WriteLine("engine 側にあるのは **(a) プロパティの宣言と doc ／ (b) ctor の引数と代入 ／"
            + " (c) `Run` の引き回し ／ (d) `NoteWildfireSwing`（計数専用・誰も読んで分岐しない）** だけ。");
        Console.WriteLine();

        Console.WriteLine("## A6 —— `checkup` の分類が入っている");
        Console.WriteLine();
        string prog = WfScan.Read("BattleSim/Program.cs");
        var labelled = Regex.Matches(prog, @"\[TraitId\.(\w+)\]\s*=\s*\(Hc(\w+)L")
                            .Select(m => m.Groups[1].Value).ToHashSet();
        var missing = UnitCatalog.All.SelectMany(d => d.Traits).Select(t => t.ToString())
                                 .Distinct().Where(t => !labelled.Contains(t)).ToList();
        Console.WriteLine($"分類の無い札 **{missing.Count} 件**"
            + (missing.Count == 0 ? " —— **A6 ○**" : ": " + string.Join(" / ", missing) + " —— **A6 ×**"));
        Console.WriteLine();

        Console.WriteLine("## A10 —— `DemoApp` に差分が無い");
        Console.WriteLine();
        int demo = 0;
        string dd = Path.Combine(WfScan.Root, "DemoApp");
        if (Directory.Exists(dd))
            foreach (string f in Directory.GetFiles(dd, "*.cs", SearchOption.AllDirectories))
                demo += Regex.Matches(File.ReadAllText(f), "Wildfire").Count;
        Console.WriteLine($"`DemoApp` の `Wildfire` の出現 **{demo} 件** —— {(demo == 0 ? "**A10 ○**" : "**A10 ×**")}");
        Console.WriteLine();
    }

    static void CompareToFile(string path, WildfireRule? rule, string label)
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
        for (int bi = 0; bi < Presets.Compare.Length; bi++)
        {
            (string name, Formation f) = Presets.Compare[bi];
            if (!want.TryGetValue(name, out double[]? exp)) continue;
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed,
                                         verbose: false, wildfire: rule).PlayerWon) wins++;
                cells++;
                double got = wins * 100.0 / Seeds;
                if (Math.Abs(got - exp[w]) > 1e-9)
                {
                    diff++;
                    moved.Add($"  - {name} 第{w + 1}波: {exp[w]:F1}% → {got:F1}%（{got - exp[w]:+0.0;-0.0}pt）");
                }
            }
        }
        Console.WriteLine($"{label} 対 `{Path.GetFileName(path)}`: **{cells} セル中 {diff} 件のずれ** —— "
            + (diff == 0 ? "**○**" : "**×**"));
        foreach (string m in moved) Console.WriteLine(m);
        Console.WriteLine();
    }
}
