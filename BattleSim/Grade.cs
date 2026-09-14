using System.Text.RegularExpressions;
using BattleCore;

// =====================================================================================
// grade モード（第127期） —— 段（格上げ）を作る
//
// 起点は3期分の言葉が一致していること:
//
//   「ちまちま通常攻撃で1体ずつ削る絵面」（第126期のあと）／「デバフを貰いながら1体ずつ倒していく
//   のは爽快感に欠ける」（第124期）／「薙ぎやゾトの全体攻撃は**一斉に入った**ほうが爽快感ある」（第123期）
//
// **機構は新規ではない。** 積み過ぎ（`OverloadTrait`・第116期）が既に
// 「`AtkBonus >= 閾値` なら 単体 → 薙ぎ」をやっている。足りないのは**段の数**（薙ぎの上が無い）で、
// この診断は**読み手・供給・閾値を固定したまま上がる先だけを振る。**
//
// **Phase 0 は盤面を1ビットも動かさない**（走査と数え物だけ）。
// **段1 も `Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` を1文字も触らない**——
// 台は診断のローカル（第115・117・118・126期と同じ形）。
//
// `TankDiag` / `Wound2Diag` / `TimeDiag` と同じく **`Program.cs` には振り分けの数行だけ**置く
// （あちらは 69,000 行が全部で1つのメソッドで Release のビルドに 4 分半かかる）。
// **クロージャを1つも作らない形**（static メソッドと static フィールドだけ）で外に置く。
//
//     dotnet run --project BattleSim -c Release 0 grade phase0   # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 grade run      # 段1（V0〜V4 × 台）
// =====================================================================================
static class GradeDiag
{
    const int Seeds = 200;                         // 帯A。`compare` と揃える（規約 (G14)）
    static readonly int[] Waves = { 1, 2, 3, 4 };  // 第2〜5波（規約 (G10)）

    static string? _root;
    static string _traits = "", _engine = "", _models = "";

    /// <summary><c>TraitId</c> → クラス本体（<c>Traits.cs</c> から索いたもの）。<b>空なら止める</b>。</summary>
    static readonly Dictionary<TraitId, string> _body = new();
    /// <summary><c>TraitId</c> → クラス名。</summary>
    static readonly Dictionary<TraitId, string> _cls = new();

    public static void Run(string mode)
    {
        if (!Init()) return;
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunTables(); return;
            default:
                Console.WriteLine("grade: モードは phase0 / run（第127期）。");
                return;
        }
    }

    // ==================================================================================
    // 索く。**手で並べた分類は1件も無い**（第94期の作法）。
    // **索けなかったら止める**（第117期の器具の事故——走査が空でも分類はすべて偽を返す）。
    // ==================================================================================
    static bool Init()
    {
        _root = Directory.GetCurrentDirectory();
        while (_root != null && !File.Exists(Path.Combine(_root, "docs", "balance.md")))
            _root = Path.GetDirectoryName(_root);

        if (_root is null)
        {
            Console.WriteLine("grade: リポジトリが見つからない（直下から実行すること）。**走査が空なら止める**（第117期）。");
            return false;
        }

        _traits = ReadOrEmpty("Traits.cs");
        _engine = ReadOrEmpty("BattleEngine.cs");
        _models = ReadOrEmpty("Models.cs");
        if (_traits.Length == 0 || _engine.Length == 0 || _models.Length == 0)
        {
            Console.WriteLine("grade: `BattleCore/*.cs` が読めない。**止める**（第117期）。");
            return false;
        }

        var decl = Regex.Matches(_traits, @"public (?:sealed |abstract )?class (\w+Trait)(?:\s*:\s*(\w+))?");
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        var baseOf = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < decl.Count; i++)
        {
            int st = decl[i].Index;
            int en = i + 1 < decl.Count ? decl[i + 1].Index : _traits.Length;
            byName[decl[i].Groups[1].Value] = _traits.Substring(st, en - st);
            if (decl[i].Groups[2].Success) baseOf[decl[i].Groups[1].Value] = decl[i].Groups[2].Value;
        }
        foreach (KeyValuePair<string, string> kv in byName)
        {
            string body = kv.Value;
            if (baseOf.TryGetValue(kv.Key, out string? b) && byName.TryGetValue(b, out string? bb)) body += bb;
            // `GradeTrait` のように1クラスで複数の `TraitId` を持つ札があるので、
            // **Id は宣言（`TraitId Id => TraitId.X`）からも、登録（`new XTrait(TraitId.X`）からも引く。**
            foreach (Match m in Regex.Matches(body, @"TraitId Id => TraitId\.(\w+)"))
                Bind(m.Groups[1].Value, kv.Key, body);
            foreach (Match m in Regex.Matches(_traits, @"new " + kv.Key + @"\(TraitId\.(\w+)"))
                Bind(m.Groups[1].Value, kv.Key, body);
        }
        if (_body.Count == 0)
        {
            Console.WriteLine("grade: `Traits.cs` の走査が **0 件**。止める（第117期）。");
            return false;
        }
        return true;
    }

    static void Bind(string idText, string cls, string body)
    {
        if (!Enum.TryParse(idText, out TraitId tid)) return;
        _body[tid] = body;
        _cls[tid] = cls;
    }

    static string ReadOrEmpty(string file)
    {
        string p = Path.Combine(_root ?? ".", "BattleCore", file);
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    /// <summary><c>TraitCatalog</c> に登録されているか（<c>Get</c> は未登録だと投げる）。</summary>
    static bool Registered(TraitId t)
    {
        try { return TraitCatalog.Get(t) is not null; }
        catch (KeyNotFoundException) { return false; }
    }

    /// <summary>その札を持つ味方（<c>UnitCatalog.All</c>）。</summary>
    static List<UnitDef> AllyHolders(TraitId t)
        => UnitCatalog.All.Where(d => d.Traits.Contains(t)).ToList();

    /// <summary>その札を持つ敵（<c>EnemyCatalog.Stages</c> に実際に出てくる駒だけ）。</summary>
    static List<UnitDef> FoeHolders(TraitId t)
    {
        var seen = new Dictionary<string, UnitDef>(StringComparer.Ordinal);
        foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
            foreach ((int _, UnitDef d) in st.Enemy.Occupied())
                if (d.Traits.Contains(t)) seen[d.Id] = d;
        return seen.Values.ToList();
    }

    static string Names(IEnumerable<UnitDef> ds)
    {
        var l = ds.Select(d => d.Name).ToList();
        return l.Count == 0 ? "—" : string.Join("・", l);
    }

    static string PatternLabel(AttackPattern p) => p switch
    {
        AttackPattern.Sweep => "薙ぎ",
        AttackPattern.Pierce => "貫き",
        AttackPattern.All => "全体",
        _ => "単体"
    };

    // ==================================================================================
    // Phase 0
    // ==================================================================================
    static void Phase0()
    {
        Console.WriteLine("# 第127期 Phase 0 —— 段（格上げ）の地図");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 grade phase0` の出力。");
        Console.WriteLine("**戦闘は1回も回さない。盤面は1ビットも動かない**——走査と数え物だけ。");
        Console.WriteLine("**手で並べた表は1つも無い**（第94期の作法。走査が空なら止める＝第117期）。");
        Console.WriteLine();

        Q01();
        Q02();
        Q03();
        Q04();
        Q05();
        Q06();
        Q07Q08();
        Q09();
    }

    // --- Q0-1 格上げを持つ駒の全件 ------------------------------------------------------
    static readonly List<(TraitId Id, string Cls, string Cond, string To)> _grades = new();

    static void Q01()
    {
        Console.WriteLine("## Q0-1 —— 格上げ（`ModifyPattern`）を持つ札の全件");
        Console.WriteLine();

        _grades.Clear();
        foreach (KeyValuePair<TraitId, string> kv in _body.OrderBy(k => (int)k.Key))
        {
            if (!kv.Value.Contains("override AttackPattern ModifyPattern", StringComparison.Ordinal)) continue;
            if (!Registered(kv.Key)) continue;

            // 本体から「上がる先」を引く（`AttackPattern.X` の出現）。**手で書かない。**
            int at = kv.Value.IndexOf("override AttackPattern ModifyPattern", StringComparison.Ordinal);
            string tail = kv.Value.Substring(at);
            int end = tail.IndexOf("\n    public ", StringComparison.Ordinal);
            if (end > 0) tail = tail.Substring(0, end);
            var tos = new List<string>();
            foreach (Match m in Regex.Matches(tail, @"AttackPattern\.(Sweep|Pierce|All|Single)"))
                if (!tos.Contains(m.Groups[1].Value)) tos.Add(m.Groups[1].Value);
            // 型をフィールドで持つ札（この期の `GradeTrait`）は本体にリテラルが無い。
            // **走査が空なら止める**のではなく、**登録済みのインスタンスから引く**
            // ——「引けなかった」と「該当なし」を区別する（第117期）。
            if (tos.Count == 0 && TraitCatalog.Get(kv.Key) is GradeTrait gt)
            {
                tos.Add(gt.Low.ToString());
                if (gt.High is not null) tos.Add(gt.High.Value.ToString());
            }
            // 条件は本体を1行に畳んで出す（長ければ切る）。
            string cond = Regex.Replace(tail, @"\s+", " ");
            cond = cond.Length > 140 ? cond.Substring(0, 140) + " …" : cond;
            _grades.Add((kv.Key, _cls[kv.Key], cond,
                tos.Count == 0 ? "—" : string.Join(" / ", tos.Select(x => PatternLabel(Enum.Parse<AttackPattern>(x))))));
        }

        Console.WriteLine($"`Traits.cs` から索いた札 **{_body.Count} 本**のうち、"
            + $"`override AttackPattern ModifyPattern` を実装しているのは **{_grades.Count} 本**。");
        Console.WriteLine();
        Console.WriteLine("| 札 | クラス | 上がる先 | 味方の保持者 | 敵の保持者 | 条件（本体から機械で切り出し） |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach ((TraitId id, string cls, string cond, string to) in _grades)
            Console.WriteLine($"| `{id}` | `{cls}` | **{to}** | {Names(AllyHolders(id))} | {Names(FoeHolders(id))} "
                + $"| `{cond.Replace("|", "\\|")}` |");
        Console.WriteLine();

        // §0-4 が下読みで挙げた5枚との照合（第98期に墓守が落ちていた形の再発防止）。
        var named = new[] { "据えのバン", "熾のホタ", "尾灯のトモ", "逃亡兵セロ", "墓守リィカ" };
        var machine = new List<string>();
        foreach ((TraitId id, string _, string _, string _) in _grades)
            foreach (UnitDef d in AllyHolders(id))
                if (!machine.Contains(d.Name)) machine.Add(d.Name);

        Console.WriteLine("### 指示書 §0-4 の下読み（5枚）との照合");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 指示書 | 機械（`ModifyPattern` を持つ札の保持者か） | 一致 |");
        Console.WriteLine("|---|:-:|:-:|:-:|");
        int agree = 0, total = 0;
        foreach (string n in named.Concat(machine).Distinct())
        {
            bool want = named.Contains(n), got = machine.Contains(n);
            total++;
            if (want == got) agree++;
            Console.WriteLine($"| {n} | {(want ? "○" : "—")} | {(got ? "○" : "—")} | {(want == got ? "○" : "**×**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"一致 **{agree} / {total}**。"
            + "**食い違った行は機械のほうを採る**（第98期にこの一覧から墓守リィカが落ちていた）。");
        Console.WriteLine();
    }

    // --- Q0-2 攻撃型の在庫 --------------------------------------------------------------
    static void Q02()
    {
        Console.WriteLine("## Q0-2 —— 攻撃型の在庫（味方 / 敵）");
        Console.WriteLine();
        Console.WriteLine("`常時` は `UnitDef.Pattern` そのもの。"
            + "`条件付き` は Q0-1 の札で**到達できる型**（保持者ごとに数える）。");
        Console.WriteLine();

        Console.WriteLine("| 型 | 味方・常時 | 味方・条件付きで到達 | 敵・常時 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (AttackPattern p in new[] { AttackPattern.Single, AttackPattern.Sweep,
                                            AttackPattern.Pierce, AttackPattern.All })
        {
            int ally = UnitCatalog.All.Count(d => d.Pattern == p);
            int reach = 0;
            foreach (UnitDef d in UnitCatalog.All)
            {
                bool hit = false;
                foreach ((TraitId id, string _, string _, string to) in _grades)
                    if (d.Traits.Contains(id) && to.Contains(PatternLabel(p), StringComparison.Ordinal)) hit = true;
                if (hit) reach++;
            }
            var foes = new Dictionary<string, UnitDef>(StringComparer.Ordinal);
            foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
                foreach ((int _, UnitDef d) in st.Enemy.Occupied())
                    if (d.Pattern == p) foes[d.Id] = d;
            Console.WriteLine($"| {PatternLabel(p)} | {ally} | {reach} | {foes.Count} |");
        }
        Console.WriteLine();
        Console.WriteLine($"味方の総数 **{UnitCatalog.All.Count} 枚**。");
        Console.WriteLine();

        foreach (AttackPattern p in new[] { AttackPattern.Sweep, AttackPattern.Pierce, AttackPattern.All })
        {
            var ally = UnitCatalog.All.Where(d => d.Pattern == p).ToList();
            Console.WriteLine($"- 味方の常時 **{PatternLabel(p)}**: {Names(ally)}");
        }
        Console.WriteLine();
    }

    // --- Q0-3 合成規則 ------------------------------------------------------------------
    static void Q03()
    {
        Console.WriteLine("## Q0-3 —— `ModifyPattern` の合成規則（実装から読む）");
        Console.WriteLine();

        int at = _models.IndexOf("public AttackPattern CurrentPattern", StringComparison.Ordinal);
        if (at < 0)
        {
            Console.WriteLine("**`CurrentPattern` が見つからない。止める**（第117期）。");
            Console.WriteLine();
            return;
        }
        string src = _models.Substring(at, Math.Min(700, _models.Length - at));
        int end = src.IndexOf("\n    }", StringComparison.Ordinal);
        if (end > 0) src = src.Substring(0, end + 6);
        Console.WriteLine("```csharp");
        Console.WriteLine(src.TrimEnd());
        Console.WriteLine("```");
        Console.WriteLine();
        Console.WriteLine("→ **畳み込み（fold）で、`Traits` の並び順の最後の1本が勝つ。**"
            + " 各実装は条件が成立すれば `p` を**無視して**自分の型を返すので、"
            + "**「段の高いほう」ではなく「後ろの札」が勝つ＝順序依存**である。");
        Console.WriteLine();

        int both = 0;
        var who = new List<string>();
        foreach (UnitDef d in UnitCatalog.All)
        {
            int n = _grades.Count(g => d.Traits.Contains(g.Id));
            if (n >= 2) { both++; who.Add(d.Name); }
        }
        Console.WriteLine($"→ **`ModifyPattern` を2本以上持つ味方は {both} 枚**"
            + (both == 0 ? "（＝現行の盤面では順序依存が一度も顕在化しない）。" : $"（{string.Join("・", who)}）。"));
        Console.WriteLine();
    }

    // --- Q0-4 全体が通る経路 -------------------------------------------------------------
    static void Q04()
    {
        Console.WriteLine("## Q0-4 —— 味方が `AttackPattern.All` を持ったときに通る経路");
        Console.WriteLine();

        int chain = _engine.IndexOf("private UnitState? SelectTargetChain", StringComparison.Ordinal);
        int branch = chain < 0 ? -1
            : _engine.IndexOf("if (pattern != AttackPattern.Single)", chain, StringComparison.Ordinal);
        Console.WriteLine($"`SelectTargetChain` の位置 **{(chain < 0 ? "見つからない" : Line(chain))}** ／ "
            + $"`pattern != AttackPattern.Single` の早期リターン **{(branch < 0 ? "見つからない" : Line(branch))}**。");
        if (chain < 0 || branch < 0)
        {
            Console.WriteLine();
            Console.WriteLine("**走査が空。止める**（第117期）。");
            Console.WriteLine();
            return;
        }
        Console.WriteLine();
        Console.WriteLine("介入の段（`InterceptLabels.All` の"
            + $" **{InterceptLabels.All.Length} 本**）が、その分岐の**前**にあるか**後**にあるか:");
        Console.WriteLine();
        Console.WriteLine("| 段 | `EmitIntercept` の位置 | 単体 | 薙ぎ・全体 |");
        Console.WriteLine("|---|---|:-:|:-:|");
        foreach (string lab in InterceptLabels.All)
        {
            string needle = "InterceptLabels." + LabelField(lab);
            var hits = new List<int>();
            for (int i = _engine.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = _engine.IndexOf(needle, i + 1, StringComparison.Ordinal))
            {
                int em = _engine.LastIndexOf("EmitIntercept", i, StringComparison.Ordinal);
                if (em >= 0 && i - em < 80) hits.Add(i);
            }
            bool beforeAny = hits.Any(h => h < branch);
            bool afterAll = hits.Count > 0 && hits.All(h => h > branch);
            Console.WriteLine($"| {lab} | {(hits.Count == 0 ? "—" : string.Join(" / ", hits.Select(Line)))} "
                + $"| {(hits.Count > 0 ? "**○**" : "—")} "
                + $"| {(beforeAny ? "**○**" : afterAll ? "**× 素通り**" : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **早期リターンより後ろにある段は、薙ぎ・全体では1度も評価されない。**"
            + " これは実装の穴ではなく設計の中核（`CLAUDE.md`「庇う・標的の介入は Single にしか効かない」）で、"
            + "**全体化した駒は敵の防御機構（庇い・殉教・棘守り・標）を丸ごと無効化する**。");
        Console.WriteLine();

        int sec = _engine.IndexOf("AttackPattern.All => foes", StringComparison.Ordinal);
        Console.WriteLine($"- `SecondaryTargets` の `All` の行: "
            + (sec < 0 ? "**見つからない**"
                       : $"`AttackPattern.All => foes`（{Line(sec)}）＝生存する敵**全員**が副次目標。"
                         + $"倍率は `SecondaryPercent` = **{BattleContext.SecondaryPercent}%**"));
        Console.WriteLine("- 副次目標にも `ApplyDamage` が1体ずつ走る（＝巻き込み則・反撃・棘は**通る**）。");
        Console.WriteLine("- 特性の発動（`OnAfterAttack`）は**主目標に対して1度だけ**（範囲でも増えない）。");
        Console.WriteLine();

        Console.WriteLine("### 素通りさせると効かなくなる敵側の機構（波ごとの在庫）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 介入を持つ敵（`Guardian`/`Martyr`/`ThornGuard`/`RearGuard`） |");
        Console.WriteLine("|---|---|");
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
        {
            var g = EnemyCatalog.Stages[w].Enemy.Occupied()
                .Where(x => x.Def.Traits.Any(t => t is TraitId.Guardian or TraitId.Martyr
                                                    or TraitId.ThornGuard or TraitId.RearGuard))
                .Select(x => x.Def.Name).ToList();
            Console.WriteLine($"| 第{w + 1}波 | {(g.Count == 0 ? "—" : string.Join("・", g))} |");
        }
        Console.WriteLine();
    }

    static string LabelField(string lab) => lab switch
    {
        "標的" => "Mark",
        "後備え" => "RearGuard",
        "庇う" => "Guardian",
        "殉教" => "Martyr",
        _ => "ThornGuard"
    };

    static string Line(int index)
    {
        int n = 1;
        for (int i = 0; i < index && i < _engine.Length; i++) if (_engine[i] == '\n') n++;
        return "L" + n;
    }

    // --- Q0-5 状態キーの供給と読み手 -----------------------------------------------------
    static void Q05()
    {
        Console.WriteLine("## Q0-5 —— 状態キーの書き手と読み手（`StatusKeys.All`）");
        Console.WriteLine();
        Console.WriteLine("書き手 = `SetCounter(StatusKeys.X` を含む札（＋その鍵の窓口 `ctx.Poison` / `ctx.Ignite`）。");
        Console.WriteLine("読み手 = `Counter(StatusKeys.X` / `RawCounter(StatusKeys.X` を**読む**札（`SetCounter` を除く）。");
        Console.WriteLine("**engine 側（`BattleEngine.cs`）は駒に帰属しないので別列にする**（第120期）。");
        Console.WriteLine();
        Console.WriteLine("| 鍵 | 書き手の札 | 書き手の味方 | 読み手の札 | **読み手の味方** | engine 書 | engine 読 |");
        Console.WriteLine("|---|--:|---|--:|---|--:|--:|");
        foreach (string key in StatusKeys.All)
        {
            string k = KeyField(key);
            var w = new List<TraitId>();
            var r = new List<TraitId>();
            foreach (KeyValuePair<TraitId, string> kv in _body)
            {
                if (!Registered(kv.Key)) continue;
                if (Writes(kv.Value, k, key)) w.Add(kv.Key);
                if (Reads(kv.Value, k)) r.Add(kv.Key);
            }
            var wu = new List<string>();
            foreach (TraitId t in w) foreach (UnitDef d in AllyHolders(t)) if (!wu.Contains(d.Name)) wu.Add(d.Name);
            var ru = new List<string>();
            foreach (TraitId t in r) foreach (UnitDef d in AllyHolders(t)) if (!ru.Contains(d.Name)) ru.Add(d.Name);

            int ew = Regex.Matches(_engine, @"SetCounter\(\s*StatusKeys\." + k).Count;
            int er = Regex.Matches(_engine, @"(?<!Set)(?:Raw)?Counter\(\s*StatusKeys\." + k).Count;
            Console.WriteLine($"| {StatusKeys.LabelOf(key)} (`{key}`) | {w.Count} "
                + $"| {(wu.Count == 0 ? "—" : string.Join("・", wu))} | {r.Count} "
                + $"| {(ru.Count == 0 ? "**— 0 枚**" : string.Join("・", ru))} | {ew} | {er} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **この表が §8-1（支援・耐久の格上げ）の材料**である。"
            + "「条件を読んで段を上げる」の**条件**に使えるのは、**読み手の側が薄い鍵**"
            + "——書かれているのに誰も読んでいない鍵ほど、新しい読み手を足す余地がある。");
        Console.WriteLine();
    }

    static bool Writes(string body, string k, string key)
    {
        if (Regex.IsMatch(body, @"SetCounter\(\s*StatusKeys\." + k)) return true;
        if (key == StatusKeys.Burn && body.Contains("ctx.Ignite(", StringComparison.Ordinal)) return true;
        if (key == StatusKeys.Poison && body.Contains("ctx.Poison(", StringComparison.Ordinal)) return true;
        return false;
    }

    static bool Reads(string body, string k)
        => Regex.IsMatch(body, @"(?<!Set)(?:Raw)?Counter\(\s*StatusKeys\." + k);

    static string KeyField(string key) => key switch
    {
        StatusKeys.Poison => "Poison",
        StatusKeys.Marked => "Marked",
        StatusKeys.Stun => "Stun",
        StatusKeys.Burn => "Burn",
        StatusKeys.IdleTurn => "IdleTurn",
        StatusKeys.Armor => "Armor",
        StatusKeys.Wound => "Wound",
        StatusKeys.Deep => "Deep",
        _ => "Curse"
    };

    // --- Q0-6 AtkBonus の供給者 ----------------------------------------------------------
    static void Q06()
    {
        Console.WriteLine("## Q0-6 —— `AtkBonus` を**正に**動かす経路の全件");
        Console.WriteLine();

        int direct = Regex.Matches(_traits, @"AtkBonus \+=").Count;
        int directE = Regex.Matches(_engine, @"AtkBonus \+=").Count;
        Console.WriteLine($"直叩き（`AtkBonus +=`）: `Traits.cs` **{direct}** ／ `BattleEngine.cs` **{directE}**"
            + $"（合計 **{direct + directE}**）。");
        Console.WriteLine($"窓口（`BattleContext.Whet`）の経路の札 `WhetRoute`: **{WhetRoutes.Count} 本**。");
        Console.WriteLine();
        Console.WriteLine("| 経路 | `WhetRoute.X` の出現（`Traits.cs` / `BattleEngine.cs`） | 味方の供給者 |");
        Console.WriteLine("|---|--:|---|");
        foreach (string name in Enum.GetNames<WhetRoute>())
        {
            if (name == "Other") continue;
            int t = Regex.Matches(_traits, @"WhetRoute\." + name + @"\b").Count;
            int e = Regex.Matches(_engine, @"WhetRoute\." + name + @"\b").Count;
            var holders = new List<string>();
            foreach (KeyValuePair<TraitId, string> kv in _body)
            {
                if (!Registered(kv.Key)) continue;
                if (!Regex.IsMatch(kv.Value, @"WhetRoute\." + name + @"\b")) continue;
                foreach (UnitDef d in AllyHolders(kv.Key)) if (!holders.Contains(d.Name)) holders.Add(d.Name);
            }
            Console.WriteLine($"| `{name}` | {t} / {e} "
                + $"| {(holders.Count == 0 ? "— (engine 側 / 保持者なし)" : string.Join("・", holders))} |");
        }
        Console.WriteLine();

        var selfOnly = new List<string>();
        foreach (KeyValuePair<TraitId, string> kv in _body)
        {
            if (!Registered(kv.Key)) continue;
            if (!kv.Value.Contains("self.AtkBonus +=", StringComparison.Ordinal)) continue;
            foreach (UnitDef d in AllyHolders(kv.Key)) if (!selfOnly.Contains(d.Name)) selfOnly.Add(d.Name);
        }
        Console.WriteLine($"**自己強化（`self.AtkBonus +=`）を持つ味方: {selfOnly.Count} 枚** — "
            + (selfOnly.Count == 0 ? "—" : string.Join("・", selfOnly)));
        Console.WriteLine();

        var readers = _body.Keys
            .Where(t => Registered(t)
                        && _body[t].Contains("self.AtkBonus", StringComparison.Ordinal)
                        && !_body[t].Contains("self.AtkBonus +=", StringComparison.Ordinal))
            .ToList();
        Console.WriteLine($"**`AtkBonus` を読む札: {readers.Count} 本** — "
            + string.Join("・", readers.Select(t => $"`{t}`（味方 {Names(AllyHolders(t))}）")));
        Console.WriteLine();
        Console.WriteLine($"閾値 `ReaderRule.Adopted` = **{ReaderRule.Adopted}**。"
            + " 第116期の知見「**閾値の当落は量ではなく席（その隣が読み手か）で決まる**」がそのまま効く"
            + "——供給の帳簿（誰が何点撒くか）を見ても、読み手に何点届くかは分からない（第115期）。");
        Console.WriteLine();
    }

    // --- Q0-7 / Q0-8 ---------------------------------------------------------------------
    static void Q07Q08()
    {
        Console.WriteLine("## Q0-7 —— `ReaderRule`（閾値を版で差し替える窓口）");
        Console.WriteLine();
        Console.WriteLine($"`ReaderRule.Default.Threshold` = **{ReaderRule.Default.Threshold}**"
            + $" ／ `ReaderRule.Adopted` = **{ReaderRule.Adopted}**。");
        Console.WriteLine($"`Run` の引数として残っているか: "
            + $"**{(_engine.Contains("ReaderRule? reader = null", StringComparison.Ordinal) ? "○" : "×")}**"
            + "（＝段1 は閾値を版で差し替えられる。**ただし掃引はしない**＝第115期と同じ二値の読み方）。");
        Console.WriteLine();

        Console.WriteLine("## Q0-8 —— 第126期・第118期の残置（保持者 0 の対照）");
        Console.WriteLine();
        Console.WriteLine("| 札 | 登録 | 味方の保持者 | 敵の保持者 |");
        Console.WriteLine("|---|:-:|--:|--:|");
        foreach (TraitId t in new[] { TraitId.Regen, TraitId.Nourish, TraitId.Reprieve, TraitId.Tempered,
                                      TraitId.GradePierce, TraitId.GradeAll, TraitId.GradeStep })
            Console.WriteLine($"| `{t}` | {(Registered(t) ? "○" : "×")} | {AllyHolders(t).Count} | {FoeHolders(t).Count} |");
        Console.WriteLine();
        Console.WriteLine("→ **`Reprieve` / `Tempered` はこの期では触らない。**"
            + " この期に足した `Grade*` の3本も**保持者 0**（台は診断のローカル）。");
        Console.WriteLine();
    }

    // --- Q0-9 過去に測っていないか --------------------------------------------------------
    static void Q09()
    {
        Console.WriteLine("## Q0-9 —— 過去に攻撃型の格上げを測っていないか");
        Console.WriteLine();
        string dir = Path.Combine(_root ?? ".", "design");
        if (!Directory.Exists(dir))
        {
            Console.WriteLine("**`design/` が見つからない。止める。**");
            Console.WriteLine();
            return;
        }
        // **自分の期の文書は分母から外す**（第123期の自己参照——指示書そのものがこの語を含む）。
        string[] needles = { "ModifyPattern", "薙ぎになる", "格上げ" };
        var hits = new List<(string File, int[] N)>();
        foreach (string f in Directory.GetFiles(dir, "*.md").OrderBy(x => x, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(f);
            if (name.StartsWith("PHASE127", StringComparison.Ordinal)) continue;
            string src = File.ReadAllText(f);
            var n = needles.Select(x => Regex.Matches(src, Regex.Escape(x)).Count).ToArray();
            if (n.Sum() > 0) hits.Add((name, n));
        }
        Console.WriteLine($"`design/*.md`（**自分の期 `PHASE127*` を除く**）のうち該当 **{hits.Count} 件**。");
        Console.WriteLine();
        Console.WriteLine("| ファイル | `ModifyPattern` | 「薙ぎになる」 | 「格上げ」 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach ((string f, int[] n) in hits) Console.WriteLine($"| `{f}` | {n[0]} | {n[1]} | {n[2]} |");
        Console.WriteLine();
        Console.WriteLine(hits.Count == 0
            ? "→ **0 件。走査が空なので止める**（語が変わった可能性がある）。"
            : "→ **同じ機構を別の名前で2度測らない。** 第66・67・77期（軋み＝`Creak`）と"
              + "第115・116期（積み過ぎ＝`Overload`）が本体で、**どちらも上がる先は薙ぎ1段だけ**。"
              + "**上がる先を振った期があるかは、この表の「格上げ」列で見る。**");
        Console.WriteLine();
    }

    // ==================================================================================
    // 段1 —— 段をローカル台で測る
    //
    // **台は「条件が確実に供給される台」にする**（指示書 §3-2。第126期の案Cが
    // 「`AtkBonus` が一度も動かない台」で落ちた穴を二度踏まない）。
    // 供給は第116期に到達率 100.0% が実測されている顔ぶれ（号令のガン ＋ `Stoic` のガルド ＋
    // 縛めのクグ）をそのまま使い、**読み手の席（後1）の札だけを版で振る。**
    // **`Presets` は1行も触らない**——同じ5枚を診断のローカルで組み直している。
    // ==================================================================================
    enum Ver { V0, V1, V2, V3, V4 }

    static readonly (Ver V, string Name, TraitId? Grade, string Note)[] Versions =
    {
        (Ver.V0, "V0 対照", null,                "格上げなし（札を1本も足さない）"),
        (Ver.V1, "V1 薙ぎ", TraitId.Overload,    "**現行の積み過ぎそのもの**（器具の再現性の確認を兼ねる）"),
        (Ver.V2, "V2 貫き", TraitId.GradePierce, "単体 → 貫き"),
        (Ver.V3, "V3 全体", TraitId.GradeAll,    "単体 → 全体（**介入を素通りする**）"),
        (Ver.V4, "V4 段",   TraitId.GradeStep,   "閾値で薙ぎ・その " + GradeTrait.StepFactor + " 倍で全体"),
    };

    sealed class Agg
    {
        public int N, Wins, Blow, Perfect, Narrow;
        public double TurnSum, SurvSum, SurvAll;
        public double DealtAlly, DealtReader, AtkMax;
        public long OverTurns, AliveTurns;      // 閾値を越えていたターン / 生きていたターン
        public int FoeIntercepts, ReaderKills, ReaderSwings;
    }

    static void One(Formation f, Formation enemy, int seed, string readerId, int baseAtk, Agg a)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false, boss: new BossRule(true));
        a.N++;
        a.TurnSum += Math.Max(1, r.Turns);
        if (r.PlayerWon)
        {
            a.Wins++;
            a.SurvSum += r.PlayerSurvivors;
            a.SurvAll += r.PlayerSurvivors;
            if (r.PlayerSurvivors >= 4) a.Blow++;
            if (r.PlayerSurvivors >= f.Occupied().Count()) a.Perfect++;
            if (r.PlayerSurvivors <= 1) a.Narrow++;
        }

        foreach ((int _, UnitDef d) in f.Occupied())
            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) a.DealtAlly += t.DamageToEnemy;

        // 敵側の介入（標的・後備え・庇う・殉教・棘守り）。**verbose に依らない計数**（第125期）。
        foreach ((int _, UnitDef d) in enemy.Occupied())
            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? ft)) a.FoeIntercepts += ft.Intercepts;

        if (!r.TallyByUnit.TryGetValue(readerId, out UnitTally? rt)) return;
        a.DealtReader += rt.DamageToEnemy;
        a.ReaderKills += rt.Kills;
        a.ReaderSwings += rt.Attacks;
        int[] atk = rt.BossAtkByTurn ?? Array.Empty<int>();
        int mx = 0;
        for (int t = 1; t <= r.Turns && t < atk.Length; t++)
        {
            if (atk[t] <= 0) continue;                 // 倒れた後のターンは数えない
            if (atk[t] > mx) mx = atk[t];
            a.AliveTurns++;
            if (atk[t] - baseAtk >= ReaderRule.Adopted) a.OverTurns++;   // **到着**（第115期）
        }
        a.AtkMax += mx;
    }

    /// <summary>読み手の札を差し替えた版（<b>数値は1つも変えない</b>）。</summary>
    static UnitDef Reader(UnitDef d, TraitId? grade) => new()
    {
        Id = d.Id + "_" + (grade?.ToString() ?? "plain"),
        Name = d.Name,
        MaxHp = d.MaxHp,
        Attack = d.Attack,
        Speed = d.Speed,
        Pattern = d.Pattern,
        // **既存の格上げの札（積み過ぎ）を必ず外してから**足す——外さないと V0 が対照にならない。
        Traits = d.Traits.Where(t => t != TraitId.Overload)
                         .Concat(grade is null ? Array.Empty<TraitId>() : new[] { grade.Value }).ToArray(),
        Actions = d.Actions,
        PlusText = d.PlusText,
        MinusText = d.MinusText,
        Flavor = d.Flavor,
    };

    static void RunTables()
    {
        Console.WriteLine("# 第127期 段1 —— 段（格上げ）をローカル台で測る");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 grade run` の出力。");
        Console.WriteLine("**`Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` は1文字も触っていない**"
            + "——台は診断のローカル（第115・117・118・126期と同じ形）。");
        Console.WriteLine();
        Console.WriteLine("**台は「条件が確実に供給される台」**（§3-2）。供給は第116期に到達率 100.0% が"
            + "実測されている顔ぶれ（号令のガン ＋ `Stoic` のガルド ＋ 縛めのクグ）で、**全版で同一**。"
            + "**読み手の席（後1）の札だけを振る。**");
        Console.WriteLine();
        Console.WriteLine("| 版 | 中身 |");
        Console.WriteLine("|---|---|");
        foreach ((Ver _, string name, TraitId? g, string note) in Versions)
            Console.WriteLine($"| {name} | {note}{(g is null ? "" : $"（札 `{g}`）")} |");
        Console.WriteLine();
        Console.WriteLine($"閾値は全版で `ReaderRule.Adopted` = **{ReaderRule.Adopted}** に固定（**掃引しない**＝第115期）。"
            + "**代金は1つも付けていない**（受け入れ条件 A5・第118/126期の教訓）。");
        Console.WriteLine();

        // --- 台（3つ）。読み手の駒だけを替えて、段の値段が読み手に依るかを見る --------------
        //
        // **台3 は供給を抜いた陰性対照**（P3・受け入れ条件 A4）。号令・縛めを外し、
        // `Whet` を1本も持たない4枚で囲む——**読み手は同じ札を持っているのに閾値に届かない**ので、
        // 「効かなかった」ではなく**「測っていない」**が出るはずである（第61期）。
        var benches = new (string Name, UnitDef Reader, bool Supply)[]
        {
            ("台1 読み手 = 据えのバン", UnitCatalog.Ban, true),
            ("台2 読み手 = 泥人形ムド", UnitCatalog.Mudo, true),
            ("台3 供給なし（陰性対照・読み手 = 据えのバン）", UnitCatalog.Ban, false),
        };

        var cell = new Dictionary<(int, Ver), Agg>();
        var cellW = new Dictionary<(int, Ver, int), Agg>();
        for (int bi = 0; bi < benches.Length; bi++)
            foreach ((Ver v, string _, TraitId? g, string _) in Versions)
            {
                UnitDef rd = Reader(benches[bi].Reader, g);
                Formation f = benches[bi].Supply
                    ? Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Kugu,
                                      center: UnitCatalog.Gan, back1: rd, back3: UnitCatalog.Dolga)
                    : Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Borg,
                                      center: UnitCatalog.Dolga, back1: rd, back3: UnitCatalog.Hagi);
                var a = new Agg();
                foreach (int w in Waves)
                {
                    var aw = new Agg();
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        One(f, EnemyCatalog.Stages[w].Enemy, seed, rd.Id, benches[bi].Reader.Attack, a);
                        One(f, EnemyCatalog.Stages[w].Enemy, seed, rd.Id, benches[bi].Reader.Attack, aw);
                    }
                    cellW[(bi, v, w)] = aw;
                }
                cell[(bi, v)] = a;
            }

        // --- 表A: 門（供給は届いているか）-------------------------------------------------
        Console.WriteLine($"## 表A —— 門: 条件（`AtkBonus >= {ReaderRule.Adopted}`）は実際に供給されているか");
        Console.WriteLine();
        Console.WriteLine("**`発火率` = 閾値を越えていたターン ÷ 読み手が生きていたターン**"
            + "（**到達＝最大ではなく到着＝そのターン効いていたか**・第115期）。");
        Console.WriteLine("**0.0% の版があれば「測定不能」と明記して、その版の数値を判定に使わない**（受け入れ条件 A4）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 素の攻 | 到達攻 | **発火率** | 測定 |");
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        for (int bi = 0; bi < benches.Length; bi++)
            foreach ((Ver v, string name, TraitId? _, string _) in Versions)
            {
                Agg a = cell[(bi, v)];
                double pct = a.AliveTurns == 0 ? 0 : a.OverTurns * 100.0 / a.AliveTurns;
                Console.WriteLine($"| {benches[bi].Name} | {name} | {benches[bi].Reader.Attack} "
                    + $"| {a.AtkMax / a.N:F1} | **{pct:F1}%** | {(pct <= 0 ? "**測定不能**" : "○")} |");
            }
        Console.WriteLine();

        // --- 表B: 版ごとの盤面 -------------------------------------------------------------
        Console.WriteLine($"## 表B —— 版ごとの盤面（第2〜5波 × seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("`読み手の与ダメ` はその駒だけ・`味方の与ダメ` は5枚の合計（どちらも1戦あたり）。");
        Console.WriteLine("`敵の介入` は敵側の `UnitTally.Intercepts` の合計（標的・後備え・庇う・殉教・棘守り）。");
        Console.WriteLine("**`Intercepts` は verbose に依らない計数**（第125期 段1）。");
        Console.WriteLine("`残存度`（第82期）は**全試行**が分母（負けは 0）——`残存` と違って**版をまたいでも分母が動かない**（規約 (G12)）。");
        Console.WriteLine();
        for (int bi = 0; bi < benches.Length; bi++)
        {
            Console.WriteLine($"### {benches[bi].Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | 発火率 | 振/戦 | 読み手の与ダメ | Δ | 撃破/戦 | 味方の与ダメ | 決着T | 勝率 | Δ勝率 "
                + "| 残存 | 残存度 | 圧勝率 | 完全勝利 | 全滅勝ち | **敵の介入/戦** |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            Agg z = cell[(bi, Ver.V0)];
            double zDealt = z.DealtReader / z.N, zWin = z.Wins * 100.0 / z.N;
            foreach ((Ver v, string name, TraitId? _, string _) in Versions)
            {
                Agg a = cell[(bi, v)];
                double pct = a.AliveTurns == 0 ? 0 : a.OverTurns * 100.0 / a.AliveTurns;
                double win = a.Wins * 100.0 / a.N;
                Console.WriteLine($"| {name} | {pct:F1}% | {a.ReaderSwings / (double)a.N:F2} "
                    + $"| **{a.DealtReader / a.N:F1}** "
                    + $"| {(v == Ver.V0 ? "—" : (a.DealtReader / a.N - zDealt).ToString("+0.0;-0.0;0.0"))} "
                    + $"| {a.ReaderKills / (double)a.N:F2} | {a.DealtAlly / a.N:F1} | {a.TurnSum / a.N:F2} "
                    + $"| **{win:F1}%** | {(v == Ver.V0 ? "—" : (win - zWin).ToString("+0.0;-0.0;0.0"))} "
                    + (a.Wins == 0
                        ? "| — | — | — | — | — "
                        : $"| {a.SurvSum / a.Wins:F2} | {a.SurvAll / a.N:F2} | {a.Blow * 100.0 / a.Wins:F1}% "
                          + $"| **{a.Perfect * 100.0 / a.Wins:F1}%** | {a.Narrow * 100.0 / a.Wins:F1}% ")
                    + $"| **{a.FoeIntercepts / (double)a.N:F2}** |");
            }
            Console.WriteLine();
        }

        // --- 表C: 段の値段（P1 / P2 の判定）------------------------------------------------
        Console.WriteLine("## 表C —— 段の値段（P1・P2 の判定）");
        Console.WriteLine();
        Console.WriteLine("`介入の残存率` は V0 の `敵の介入/戦` を 100% としたときの割合"
            + "（**素通りした量ではなく、盤面から消えた介入の割合**）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 段 | Δ読み手の与ダメ | Δ味方の与ダメ | Δ勝率 | Δ敵の介入/戦 | 介入の残存率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < benches.Length; bi++)
        {
            Agg z = cell[(bi, Ver.V0)];
            double zd = z.DealtReader / z.N, zt = z.DealtAlly / z.N,
                   zw = z.Wins * 100.0 / z.N, zi = z.FoeIntercepts / (double)z.N;
            foreach (Ver v in new[] { Ver.V1, Ver.V2, Ver.V3, Ver.V4 })
            {
                Agg a = cell[(bi, v)];
                double ai = a.FoeIntercepts / (double)a.N;
                Console.WriteLine($"| {benches[bi].Name} | {Versions.First(x => x.V == v).Name} "
                    + $"| {a.DealtReader / a.N - zd:+0.0;-0.0;0.0} "
                    + $"| {a.DealtAlly / a.N - zt:+0.0;-0.0;0.0} "
                    + $"| {a.Wins * 100.0 / a.N - zw:+0.0;-0.0;0.0} "
                    + $"| {ai - zi:+0.00;-0.00;0.00} "
                    + $"| {(zi <= 0 ? "—" : (ai * 100.0 / zi).ToString("F1") + "%")} |");
            }
        }
        Console.WriteLine();
        // --- 表D: 波別の介入（A7 の本体）---------------------------------------------------
        //
        // **介入を持つ敵がいるのは第五波（殉教者）だけ**（Phase 0 の Q0-4）。
        // 通算で見ると他の3波に薄められるので、**A7 は波別で見る。**
        Console.WriteLine("## 表D —— 波別の `敵の介入/戦`（A7 の本体）");
        Console.WriteLine();
        Console.WriteLine("**介入を持つ敵がいるのは第五波（殉教者）だけ**（Phase 0 の Q0-4）。"
            + "通算で見ると他の3波に薄められるので、**素通りは波別で見る。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 |" + string.Concat(Waves.Select(w => $" 第{w + 1}波 |")) + " 第五波の勝率 |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---:|")) + "---:|");
        for (int bi = 0; bi < benches.Length; bi++)
            foreach ((Ver v, string name, TraitId? _, string _) in Versions)
            {
                var cells = new List<string>();
                foreach (int w in Waves)
                {
                    Agg aw = cellW[(bi, v, w)];
                    cells.Add($" {aw.FoeIntercepts / (double)aw.N:F2} |");
                }
                Agg last = cellW[(bi, v, Waves[^1])];
                Console.WriteLine($"| {benches[bi].Name} | {name} |" + string.Concat(cells)
                    + $" {last.Wins * 100.0 / last.N:F1}% |");
            }
        Console.WriteLine();

        Console.WriteLine("> **A7（全体が介入を素通りする）は、V3 の `敵の介入/戦` が V0・V1 より下がることで見る。**"
            + " ただし**介入は敵が味方の単体攻撃に対して出すもの**なので、"
            + "読み手1枚を全体化しても**残り4枚の単体攻撃に対する介入は残る**"
            + "——この列が 0 にならないことは素通りしていない証拠ではない。");
        Console.WriteLine();
    }
}
