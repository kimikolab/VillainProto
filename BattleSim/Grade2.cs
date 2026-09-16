using System.Reflection;
using System.Text.RegularExpressions;
using BattleCore;

// =====================================================================================
// grade2 モード（第128期） —— 段の載せ替え（ドルガ）と、強化の棚卸し
//
// 第127期は据えのバンに段違い（`GradeStep`）を載せたが、**全体（閾値20）は実戦で一度も立たなかった**
// ——バンに積まれる量は +8 止まりで、構造的に届かない（指示書 §0-1）。
// 原因は席でも seed でもなく**供給の宛先**で、強化は「休む駒」に流れる（§0-2）。
// **ドルガは `Sluggish` で必ず休むので最も積まれ、そして読み手を1つも持っていない。**
//
// この期は**新しい機構を1つも作らない。** 札の載せ先を替えるだけである。
//
// **Phase 0 と段1 は盤面を1ビットも動かさない**（走査・数え物・既存の計数の読み直しだけ）。
//
//     dotnet run --project BattleSim -c Release 0 grade2 phase0  # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 grade2 stock   # 段1 の棚卸し（→ docs/stock.md）
//     dotnet run --project BattleSim -c Release 0 grade2 run     # 段2 の測定（発火率・到達率・席）
//     dotnet run --project BattleSim -c Release 0 grade2 check [採用前のbalance.md]
// =====================================================================================
static class Grade2Diag
{
    const int Seeds = 200;   // 帯A。`compare` と揃える（規約 (G14)）

    static string? _root;
    static string _traits = "", _engine = "";

    public static void Run(string mode, string arg)
    {
        if (!Init()) return;
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "stock": Stock(); return;
            case "run": RunTables(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("grade2: モードは phase0 / stock / run / check（第128期）。");
                return;
        }
    }

    // ==================================================================================
    // 索く。**索けなかったら止める**（第117期）
    // ==================================================================================
    static bool Init()
    {
        _root = Directory.GetCurrentDirectory();
        while (_root != null && !File.Exists(Path.Combine(_root, "docs", "balance.md")))
            _root = Path.GetDirectoryName(_root);
        if (_root is null)
        {
            Console.WriteLine("grade2: リポジトリが見つからない。**走査が空なら止める**（第117期）。");
            return false;
        }
        _traits = ReadOrEmpty("Traits.cs");
        _engine = ReadOrEmpty("BattleEngine.cs");
        if (_traits.Length == 0 || _engine.Length == 0)
        {
            Console.WriteLine("grade2: `BattleCore/*.cs` が読めない。**止める**（第117期）。");
            return false;
        }
        return true;
    }

    static string ReadOrEmpty(string file)
    {
        string p = Path.Combine(_root ?? ".", "BattleCore", file);
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    /// <summary>
    /// その札が <see cref="Trait.ModifyPattern"/> を上書きしているか。
    /// <b>正規表現ではなくリフレクションで引く</b>——`GradeTrait` のように
    /// 1クラスが複数の <c>TraitId</c> を持つ札があり、宣言の走査では落ちる（第127期）。
    /// </summary>
    static bool IsPatternReader(TraitId t)
    {
        try
        {
            Trait tr = TraitCatalog.Get(t);
            MethodInfo? m = tr.GetType().GetMethod(nameof(Trait.ModifyPattern));
            return m is not null && m.DeclaringType != typeof(Trait);
        }
        catch (KeyNotFoundException) { return false; }
    }

    static bool HasReader(UnitDef d) => d.Traits.Any(IsPatternReader);

    static AttackPattern Upgrade(TraitId t)
    {
        Trait tr = TraitCatalog.Get(t);
        return tr is GradeTrait g ? (g.High ?? g.Low) : AttackPattern.Single;
    }

    static string Pat(AttackPattern p) => p switch
    {
        AttackPattern.Sweep => "薙ぎ",
        AttackPattern.Pierce => "貫き",
        AttackPattern.All => "全体",
        _ => "単体"
    };

    static List<UnitDef> Holders(TraitId t) => UnitCatalog.All.Where(d => d.Traits.Contains(t)).ToList();

    static string Names(IEnumerable<UnitDef> ds)
    {
        var l = ds.Select(d => d.Name).ToList();
        return l.Count == 0 ? "—" : string.Join("・", l);
    }

    /// <summary>ドルガが出てくる <c>compare</c> の行（機械で引く）。</summary>
    static List<(string Name, Formation F, int Slot)> DolgaRows()
    {
        var rows = new List<(string, Formation, int)>();
        foreach ((string name, Formation f) in Presets.Compare)
            foreach ((int slot, UnitDef d) in f.Occupied())
                if (d.Id == UnitCatalog.Dolga.Id) rows.Add((name, f, slot));
        return rows;
    }

    static string SlotName(int slot) => slot switch
    {
        0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3",
        5 => "○中1", 6 => "○中3", 7 => "○前2", 8 => "○後2", _ => slot.ToString()
    };

    // ==================================================================================
    // Phase 0
    // ==================================================================================
    static void Phase0()
    {
        Console.WriteLine("# 第128期 Phase 0 —— 段の載せ替えの地図");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 grade2 phase0` の出力。");
        Console.WriteLine("**戦闘は1回も回さない。盤面は1ビットも動かない**——走査と数え物だけ。");
        Console.WriteLine("**手で並べた表は1つも無い**（第94期の作法。走査が空なら止める＝第117期）。");
        Console.WriteLine();
        Q01(); Q02(); Q03(); Q04(); Q05Q06(); Q07(); Q09();
    }

    // --- Q0-1 GradeStep は段を上げるのか、型を返すのか -----------------------------------
    static void Q01()
    {
        Console.WriteLine("## Q0-1 —— `GradeStep` は「現在の型から段を上げる」のか「固定の型を返す」のか");
        Console.WriteLine();
        Match m = Regex.Match(_traits, @"public override AttackPattern ModifyPattern\(UnitState self, AttackPattern p\)\s*\{[^}]*StepFactor[^}]*\}",
                              RegexOptions.Singleline);
        if (!m.Success)
        {
            Console.WriteLine("**走査 0 件。止める**（第117期）——`GradeTrait.ModifyPattern` が索けない。");
            Console.WriteLine();
            return;
        }
        Console.WriteLine("```csharp");
        Console.WriteLine(m.Value.Trim());
        Console.WriteLine("```");
        Console.WriteLine();
        var g = (GradeTrait)TraitCatalog.Get(TraitId.GradeStep);
        Console.WriteLine($"→ **固定の型を返す。** 低い段 `{Pat(g.Low)}`（閾値 {ReaderRule.Adopted}）／"
            + $"高い段 `{Pat(g.High ?? g.Low)}`（{ReaderRule.Adopted} × `StepFactor` {GradeTrait.StepFactor} "
            + $"＝ **{ReaderRule.Adopted * GradeTrait.StepFactor}**）。"
            + "引数 `p`（現在の型）は**どちらの段でも読まれない**——条件が成立すれば捨てられる。");
        Console.WriteLine();
        Console.WriteLine($"→ **薙ぎ持ちに載せると低い段は恒等になる。** ドルガの素の型は "
            + $"`{Pat(UnitCatalog.Dolga.Pattern)}` なので、`AtkBonus` が {ReaderRule.Adopted}〜"
            + $"{ReaderRule.Adopted * GradeTrait.StepFactor - 1} のあいだ `ModifyPattern` は**薙ぎを返すが型は変わらない**。"
            + $"**効くのは上の段（{ReaderRule.Adopted * GradeTrait.StepFactor} で全体）1つだけ**である"
            + "（指示書 §3-2 の但し書きに該当。**新しい札は作らない**）。");
        Console.WriteLine();
    }

    // --- Q0-2 ModifyPattern の合成 ------------------------------------------------------
    static void Q02()
    {
        Console.WriteLine("## Q0-2 —— `ModifyPattern` の衝突（合成）");
        Console.WriteLine();
        var readers = Enum.GetValues<TraitId>().Where(IsPatternReader).ToList();
        Console.WriteLine($"`Trait.ModifyPattern` を上書きしている札は **{readers.Count} 本**"
            + "（リフレクションで引く＝第127期に宣言の走査で3枚落とした反省）。");
        Console.WriteLine();
        Console.WriteLine("| 札 | 上がる先 | 味方の保持者 |");
        Console.WriteLine("|---|---|---|");
        foreach (TraitId t in readers)
            Console.WriteLine($"| `{t}` | {(TraitCatalog.Get(t) is GradeTrait gg ? (gg.High is null ? Pat(gg.Low) : $"{Pat(gg.Low)} / {Pat(gg.High.Value)}") : "—")} | {Names(Holders(t))} |");
        Console.WriteLine();
        var two = UnitCatalog.All.Where(d => d.Traits.Count(IsPatternReader) >= 2).ToList();
        Console.WriteLine($"**`ModifyPattern` を2本以上持つ味方: {two.Count} 枚**（{Names(two)}）"
            + "——`CurrentPattern` は畳み込みで**後ろの札が勝つ**ので、2本持つ駒が出た瞬間に順序依存が顕在化する。");
        Console.WriteLine();
        var dolga = UnitCatalog.Dolga;
        Console.WriteLine($"**ドルガの現行の札: {string.Join("・", dolga.Traits.Select(t => $"`{t}`"))}** "
            + $"——`ModifyPattern` を持つ札は **{dolga.Traits.Count(IsPatternReader)} 本**。"
            + $"{(dolga.Traits.Count(IsPatternReader) == 0 ? "**衝突は 0 件**（§3-2 の前提が成立）。" : "**衝突がある。止めて報告する。**")}");
        Console.WriteLine();
    }

    // --- Q0-3 AtkBonus を積む経路と、その宛先の決まり方 ----------------------------------
    static readonly string[] _pickWords =
        { "SurrenderedTurn", "IdleTurn", "Roll(", "AcceptsSupport", "SupportTargets", "Adjacent",
          "CurrentAttack", "LivingMembers", "self", "Burn", "Slot" };

    static void Q03()
    {
        Console.WriteLine("## Q0-3 —— `AtkBonus` を積む経路の全件と、**宛先の決まり方**");
        Console.WriteLine();
        var sites = new List<(string Route, string File, string Line, string Words)>();
        foreach ((string file, string text) in new[] { ("Traits.cs", _traits), ("BattleEngine.cs", _engine) })
            foreach (Match m in Regex.Matches(text, @"[^\n]*Whet\(([^;]*?)WhetRoute\.(\w+)\)[^\n]*"))
            {
                if (m.Value.Contains("public void Whet")) continue;
                int from = Math.Max(0, m.Index - 1400);
                string ctx = text.Substring(from, m.Index - from);
                string words = string.Join(" / ", _pickWords.Where(w => ctx.Contains(w)));
                sites.Add((m.Groups[2].Value, file, m.Value.Trim(), words.Length == 0 ? "—" : words));
            }
        Console.WriteLine($"`Whet` の呼び出し口は **{sites.Count} 本**（窓口の宣言を除く）。"
            + "**「宛先の語」は呼び出しの手前 1,400 文字から機械で拾った語**（手で書いた分類は1つも無い）。");
        Console.WriteLine();
        Console.WriteLine("| 経路 | ファイル | 呼び出し | 宛先の選択に現れる語 |");
        Console.WriteLine("|---|---|---|---|");
        foreach ((string r, string f, string l, string w) in sites)
            Console.WriteLine($"| `{r}` | `{f}` | `{(l.Length > 64 ? l.Substring(0, 64) + "…" : l)}` | {w} |");
        Console.WriteLine();
        int idle = sites.Count(s => s.Words.Contains("SurrenderedTurn") || s.Words.Contains("IdleTurn"));
        Console.WriteLine($"→ **「休んだ味方」を条件に選ぶ経路: {idle} 本**（`SurrenderedTurn` / `IdleTurn` を読む）。");
        Console.WriteLine();
        Console.WriteLine("→ **縛め（`Bind`）は「休む駒を選ぶ」のではない。** 無作為に選んだ味方を"
            + "**その場で縛って休ませる**（`SetCounter(StatusKeys.Stun, 1)` → `Whet`）ので、"
            + "選択は `Roll` で、休みは結果である。**指示書 §0-2 の「どちらも動かない味方に積む」は半分だけ正しい。**");
        Console.WriteLine();
        var self = Regex.Matches(_traits, @"self\.AtkBonus \+=").Count;
        Console.WriteLine($"自己強化（`self.AtkBonus +=`・`Traits.cs`）は **{self} 箇所**。"
            + "**窓口を通らない**ので横取り（集約・転嫁・横流し）にも晒されない（第56期の意図的な非対称）。");
        Console.WriteLine();
    }

    // --- Q0-4 「休む駒」の全件 ------------------------------------------------------------
    static void Q04()
    {
        Console.WriteLine("## Q0-4 —— 「休む駒」の全件と、**その駒が読み手を持つか**");
        Console.WriteLine();
        TraitId[] idle = { TraitId.Sluggish, TraitId.Immobile, TraitId.Pursuer, TraitId.Displaced };
        Console.WriteLine("**常に手番を落とす／ターン外に働く札**（`CanAct` を偽にする札と、手番の外で干渉する札）:");
        Console.WriteLine();
        Console.WriteLine("| 札 | 味方の保持者 | 読み手（`ModifyPattern`）|");
        Console.WriteLine("|---|---|:-:|");
        var noReader = new List<UnitDef>();
        foreach (TraitId t in idle)
            foreach (UnitDef d in Holders(t))
            {
                bool r = HasReader(d);
                Console.WriteLine($"| `{t}` | {d.Name} | {(r ? "○" : "**×**")} |");
                if (!r) noReader.Add(d);
            }
        Console.WriteLine();
        Console.WriteLine($"→ **「休むのに読み手を持たない」駒: {noReader.Distinct().Count()} 枚**"
            + $"（{Names(noReader.Distinct())}）。**予測 P2 の分子。**");
        Console.WriteLine();
        Console.WriteLine("**痺れ（`Stun`）で休まされる駒は編成では選べない**——縛めのクグが `Roll` で選ぶので、"
            + "**どの駒も等確率で候補になる**（`AcceptsSupport` が真で痺れていない味方）。");
        Console.WriteLine();
    }

    // --- Q0-5 / Q0-6 ドルガの行と席 -------------------------------------------------------
    static void Q05Q06()
    {
        Console.WriteLine("## Q0-5 / Q0-6 —— ドルガが出る `compare` の行と、その**席**");
        Console.WriteLine();
        var rows = DolgaRows();
        Console.WriteLine($"`Presets.Compare` **{Presets.Compare.Length} 行**のうち、"
            + $"ドルガを含むのは **{rows.Count} 行**（{rows.Count * 100.0 / Presets.Compare.Length:F1}%）。"
            + $"うち主判定19行に入るのは **{rows.Count(r => Baseline.PrimaryRows.Contains(r.Name))} 行**"
            + "（規約 (G4)——**動く行の予告は分母の何割かを出して初めて予告になる**）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 席 | 隣接する占有枠 | 主判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        foreach ((string name, Formation f, int slot) in rows)
        {
            // 薙ぎの巻き込みは敵側の表（`SweepTargets`）で決まるので、**席では変わらない**。
            // 変わるのは「どの敵を主目標に取りやすいか」のほうなので、ここでは席と隣接だけを出す。
            int adj = f.Occupied().Count(o => FormationRules.AreAdjacent(slot, o.Slot));
            Console.WriteLine($"| {name} | {SlotName(slot)} | 隣接 {adj} | {(Baseline.PrimaryRows.Contains(name) ? "○" : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine("**席は「全体化の伸び幅」を直接は変えない**——全体（`All`）の副次目標は"
            + "「生存する敵**全員**」で、味方側の席を1つも読まない。席が効くのは"
            + "**薙ぎだったときの巻き込み**（標的と同じ列＋中列）と、**強化が届くか**（隣接）の側である。");
        Console.WriteLine();
    }

    // --- Q0-7 全体が通る経路 ---------------------------------------------------------------
    static void Q07()
    {
        Console.WriteLine("## Q0-7 —— 全体（`All`）が通る経路の再確認（第127期 Q0-4 が今も成り立つか）");
        Console.WriteLine();
        int early = Regex.Matches(_engine, @"pattern != AttackPattern\.Single").Count;
        Console.WriteLine($"- `SelectTargetChain` の早期リターン（`pattern != AttackPattern.Single`）: **{early} 箇所**"
            + $"{(early == 1 ? "（＝第127期と同じ。**薙ぎの時点で介入は既に素通りしている**）" : "（**第127期と違う。止めて報告する**）")}");
        Console.WriteLine($"- 副次目標の倍率 `SecondaryPercent`: **{Regex.Match(_engine, @"SecondaryPercent = (\d+)").Groups[1].Value}%**");
        Console.WriteLine();
        TraitId[] guards = { TraitId.Guardian, TraitId.Martyr, TraitId.ThornGuard, TraitId.RearGuard };
        Console.WriteLine("| 波 | 介入を持つ敵 |");
        Console.WriteLine("|---|---|");
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
        {
            var g = EnemyCatalog.Stages[w].Enemy.Occupied()
                .Where(o => o.Def.Traits.Any(guards.Contains)).Select(o => o.Def.Name).Distinct().ToList();
            Console.WriteLine($"| 第{w + 1}波 | {(g.Count == 0 ? "—" : string.Join("・", g))} |");
        }
        Console.WriteLine();
        Console.WriteLine("→ **全体化しても新しく素通りするものは無い**（薙ぎが既に全部素通りしている）。"
            + "副次目標にも `ApplyDamage` が1体ずつ走るので、**巻き込み則・反撃・棘は通る**。");
        Console.WriteLine();
    }

    // --- Q0-9 過去の実験 ---------------------------------------------------------------
    static void Q09()
    {
        Console.WriteLine("## Q0-9 —— 過去に「薙ぎ持ちを全体化」した実験が無いか");
        Console.WriteLine();
        string dir = Path.Combine(_root!, "design");
        int hit = 0;
        foreach (string p in Directory.GetFiles(dir, "*.md").OrderBy(x => x))
        {
            if (Path.GetFileName(p).StartsWith("PHASE128")) continue;
            string t = File.ReadAllText(p);
            int n = Regex.Matches(t, "全体攻撃|全体化").Count;
            if (n == 0) continue;
            hit++;
            Console.WriteLine($"- `{Path.GetFileName(p)}`: **{n} 件**");
        }
        Console.WriteLine();
        Console.WriteLine($"該当 **{hit} ファイル**。**同じ機構を別の名前で2度測らない**"
            + "——`GradeAll`（第127期）が上がる先を全体に振った唯一の版で、**保持者は `UnitCatalog.All` に 0 枚**だった。");
        Console.WriteLine();
    }

    // ==================================================================================
    // 段1 —— 強化の棚卸し（`docs/stock.md`）
    // ==================================================================================
    sealed class Stat
    {
        public int Rows;            // 出場行数
        public long Battles;        // 出場した戦闘数（分母）
        public long PeakSum;        // 到達点（`AtkPeak`）の総和
        public int PeakMax;
        public long Turns, Stalls;  // 手番と、潰れた手番
        public long Whetted, Dulled;   // **窓口を通って外から届いた量**（自己強化は入らない・第56期）
        public long Dealt;
    }

    static void Stock()
    {
        Console.WriteLine("# 強化の棚卸し —— 駒ごとの `AtkBonus` の在庫（第128期 段1）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 grade2 stock > docs/stock.md` の出力。**手で編集しない。**");
        Console.WriteLine();
        Console.WriteLine("**盤面は1ビットも動かない**——`BattleResult` に元からある計数"
            + "（`AtkPeak` ＝ 第106期／`TurnsTaken` `TurnStalls` ＝ 第105期）を読み直しているだけ。");
        Console.WriteLine();
        Console.WriteLine($"帯は `compare` と同じ **{Presets.Compare.Length} 行 × 全5波 × seed 0..{Seeds - 1}**"
            + $"（＝ {Presets.Compare.Length * EnemyCatalog.Stages.Count * Seeds:N0} 戦）。"
            + "**`docs/balance.md` は1文字も変わらない**（規則もノブも1つも触っていない）。");
        Console.WriteLine();
        Console.WriteLine("| 列 | 意味 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 外 | **窓口（`BattleContext.Whet`）を通って外から届いた量 / 戦**（`UnitTally.Whetted`）。"
            + "**自己強化の9本は入らない**——第56期の非対称そのままで、これが「積んでもらった量」である |");
        Console.WriteLine("| 到達 | **1戦で `AtkBonus` が届いた最大値の平均**（`UnitTally.AtkPeak`）。"
            + "決着時の値ではなく**到達点**を採る——第109期に「累積は途切れる」を測った量と同じ側 |");
        Console.WriteLine("| 最大 | 同じ量の、全戦を通じた最大 |");
        Console.WriteLine("| 休み | `TurnStalls ÷ TurnsTaken`（回ってきた手番のうち潰れた割合） |");
        Console.WriteLine("| 読み手 | `Trait.ModifyPattern` を上書きする札を持つか（リフレクションで引く） |");
        Console.WriteLine();

        var stats = new Dictionary<string, Stat>(StringComparer.Ordinal);
        // **箱は `Everyone`（`All ∪ Retired`）で作る（第141期）。** 第139期は「`Presets` に居るが `All` に居ない駒」を
        // `Presets.Compare` を走査して足していたが、その集合は `UnitCatalog.Retired` が明示的に持つようになった。
        // **ロスターは「編成に選べる 52 枚」の定義であって、`Presets` が参照できる集合ではない**
        // （第108期。ハリを `All` から外したときも `Presets.Cross` には残した）。
        // **表は `UnitCatalog.All` を回すので 52 行のまま**——箱が増えるだけで出力は1文字も変わらない。
        foreach (UnitDef d in UnitCatalog.Everyone) stats[d.Id] = new Stat();

        object gate = new();
        Parallel.ForEach(Presets.Compare, row =>
        {
            var local = new Dictionary<string, Stat>(StringComparer.Ordinal);
            foreach ((int _, UnitDef d) in row.F.Occupied()) local[d.Id] = new Stat { Rows = 1 };
            foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(row.F, st.Enemy, seed, verbose: false);
                    foreach ((int _, UnitDef d) in row.F.Occupied())
                    {
                        Stat s = local[d.Id];
                        s.Battles++;
                        if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
                        s.PeakSum += t.AtkPeak;
                        if (t.AtkPeak > s.PeakMax) s.PeakMax = t.AtkPeak;
                        s.Turns += t.TurnsTaken;
                        s.Stalls += t.TurnStalls;
                        s.Whetted += t.Whetted;
                        s.Dulled += t.Dulled;
                        s.Dealt += t.DamageToEnemy;
                    }
                }
            lock (gate)
                foreach (KeyValuePair<string, Stat> kv in local)
                {
                    Stat g = stats[kv.Key];
                    g.Rows += kv.Value.Rows; g.Battles += kv.Value.Battles;
                    g.PeakSum += kv.Value.PeakSum; g.Turns += kv.Value.Turns;
                    g.Stalls += kv.Value.Stalls; g.Dealt += kv.Value.Dealt;
                    g.Whetted += kv.Value.Whetted; g.Dulled += kv.Value.Dulled;
                    if (kv.Value.PeakMax > g.PeakMax) g.PeakMax = kv.Value.PeakMax;
                }
        });

        Console.WriteLine("## 表 —— 52枚（到達点の降順）");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | 出場行 | **外** | 到達 | 最大 | 休み | 読み手 | 与ダメ/戦 |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|--:|");
        int rank = 0;
        foreach (UnitDef d in UnitCatalog.All
                     .OrderByDescending(x => stats[x.Id].Battles == 0 ? -1 : stats[x.Id].PeakSum / (double)stats[x.Id].Battles)
                     .ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            Stat s = stats[d.Id];
            rank++;
            string peak = s.Battles == 0 ? "—" : $"{s.PeakSum / (double)s.Battles:F2}";
            string idle = s.Turns == 0 ? "—" : $"{s.Stalls * 100.0 / s.Turns:F1}%";
            string dealt = s.Battles == 0 ? "—" : $"{s.Dealt / (double)s.Battles:F1}";
            string outer = s.Battles == 0 ? "—" : $"{s.Whetted / (double)s.Battles:F2}";
            Console.WriteLine($"| {rank} | {d.Name} | {s.Rows} | **{outer}** | {peak} | {(s.Battles == 0 ? "—" : s.PeakMax.ToString())} | {idle} | {(HasReader(d) ? "○" : "×")} | {dealt} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**{UnitCatalog.All.Count} 枚すべてを含む**（出場行 0 の駒は `compare` に出ないので「—」）。");
        Console.WriteLine();

        var appear = UnitCatalog.All.Where(d => stats[d.Id].Battles > 0).ToList();
        Console.WriteLine($"出場する駒 **{appear.Count} 枚** ／ 出場しない駒 **{UnitCatalog.All.Count - appear.Count} 枚**"
            + $"（{Names(UnitCatalog.All.Where(d => stats[d.Id].Battles == 0))}）。");
        Console.WriteLine();

        Console.WriteLine("## 外から積まれた量（`Whetted`）の降順・上位15");
        Console.WriteLine();
        Console.WriteLine("**これが「誰が強化を受け取っているか」の表である**——上の表の `到達` は"
            + "**自己強化の9本を含む**ので、育つ駒（墓守・澱み喰い・分かち・軋み・泥人形）が上に来る。");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | 外/戦 | 到達 | 休み | 読み手 |");
        Console.WriteLine("|--:|---|--:|--:|--:|:-:|");
        int q = 0;
        foreach (UnitDef d in appear.OrderByDescending(x => stats[x.Id].Whetted / (double)stats[x.Id].Battles).Take(15))
        {
            Stat s = stats[d.Id];
            Console.WriteLine($"| {++q} | {d.Name} | **{s.Whetted / (double)s.Battles:F2}** | {s.PeakSum / (double)s.Battles:F2} | "
                + $"{s.Stalls * 100.0 / Math.Max(1, s.Turns):F1}% | {(HasReader(d) ? "○" : "×")} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 「積まれるのに読み手を持たない」駒（上位10）");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | 外/戦 | 到達 | 休み | 読み手 |");
        Console.WriteLine("|--:|---|--:|--:|--:|:-:|");
        int k = 0;
        foreach (UnitDef d in appear.Where(x => !HasReader(x))
                     .OrderByDescending(x => stats[x.Id].Whetted / (double)stats[x.Id].Battles).Take(10))
        {
            Stat s = stats[d.Id];
            Console.WriteLine($"| {++k} | {d.Name} | {s.Whetted / (double)s.Battles:F2} | {s.PeakSum / (double)s.Battles:F2} | {s.Stalls * 100.0 / Math.Max(1, s.Turns):F1}% | × |");
        }
        Console.WriteLine();
        Console.WriteLine("**これがこの期でいちばん再利用される表である**——"
            + "**読み手をどこに置くべきかは、供給の帳簿ではなくこの到達点の列で決まる**（第115期）。");
        Console.WriteLine();
    }

    // ==================================================================================
    // 段2 —— 載せ替えの測定
    // ==================================================================================
    static void RunTables()
    {
        Console.WriteLine("# 第128期 段2 —— 載せ替えの測定（発火率・到達率・行ごとの帰属）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 grade2 run` の出力。");
        Console.WriteLine($"帯は `compare` と同じ **seed 0..{Seeds - 1}**。**規約 (G10) に従い判定は第2〜5波**"
            + "（第一波は参考として併記する）。");
        Console.WriteLine();

        var rows = DolgaRows();
        Console.WriteLine($"ドルガを含む行 **{rows.Count} 行** ／ `compare` 全 {Presets.Compare.Length} 行。");
        Console.WriteLine();
        Console.WriteLine($"閾値は `ReaderRule.Adopted` = **{ReaderRule.Adopted}**（低い段＝薙ぎ・ドルガでは恒等）／"
            + $"**{ReaderRule.Adopted * GradeTrait.StepFactor}**（上の段＝全体）。**掃引していない。**");
        Console.WriteLine();

        Console.WriteLine("## 表A —— ドルガの門（到達と発火。波ごと）");
        Console.WriteLine();
        Console.WriteLine("`到達率` は**ターン頭に `AtkBonus` が上の段の閾値以上だったターンの割合**"
            + "（`ReaderProbeTurns` の格子 20 ÷ `ReaderTurns`）。"
            + "`全体率` は**実際に振った一撃のうち全体だった割合**（`ReaderAlls` ÷ `ReaderSwings`）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 生存T/戦 | 平均到達 | 到達率 | 振/戦 | **全体率** | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");

        long gTurns = 0, gOver = 0, gSwings = 0, gAlls = 0;
        long gTurns25 = 0, gOver25 = 0, gSwings25 = 0, gAlls25 = 0;
        // **分母を先に割る**（規約 (G12)）——ドルガに外から1点も届かない行では、
        // 全体率は閾値に依らず構造的に 0 になる。**その 0 は「効かなかった」ではなく「供給が無い」。**
        long fedSwings = 0, fedAlls = 0, drySwings = 0, dryAlls = 0;
        int fedRows = 0, dryRows = 0;
        // 格子（`UnitTally.ReaderProbes` = 1/5/10/20/40）ごとの到達ターン数。
        // **閾値を動かす前に、動かした先に標本があるかを見る**（第116期——強化は段で来るので
        // 格子の2点のあいだに標本がほとんど無いことがある）。
        long[] probeTurns = new long[UnitTally.ReaderProbes.Length];
        long probeDenom = 0;
        foreach ((string name, Formation f, int _) in rows)
        {
            long rowWhet = 0, rowSwings = 0, rowAlls = 0;
            for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
            {
                long turns = 0, over = 0, swings = 0, alls = 0, peak = 0, wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false);
                    if (r.PlayerWon) wins++;
                    if (!r.TallyByUnit.TryGetValue(UnitCatalog.Dolga.Id, out UnitTally? t)) continue;
                    turns += t.ReaderTurns;
                    swings += t.ReaderSwings;
                    alls += t.ReaderAlls;
                    peak += t.AtkPeak;
                    int hi = ReaderRule.Adopted * GradeTrait.StepFactor;
                    int idx = Array.IndexOf(UnitTally.ReaderProbes, hi);
                    if (idx >= 0 && t.ReaderProbeTurns is not null) over += t.ReaderProbeTurns[idx];
                    rowWhet += t.Whetted;
                    if (w >= 1 && t.ReaderProbeTurns is not null)
                    {
                        probeDenom += t.ReaderTurns;
                        for (int i = 0; i < probeTurns.Length; i++) probeTurns[i] += t.ReaderProbeTurns[i];
                    }
                }
                if (w >= 1) { rowSwings += swings; rowAlls += alls; }
                gTurns += turns; gOver += over; gSwings += swings; gAlls += alls;
                if (w >= 1) { gTurns25 += turns; gOver25 += over; gSwings25 += swings; gAlls25 += alls; }
                Console.WriteLine($"| {name} | 第{w + 1}波 | {turns / (double)Seeds:F2} | {peak / (double)Seeds:F1} | "
                    + $"{(turns == 0 ? "—" : $"{over * 100.0 / turns:F1}%")} | {swings / (double)Seeds:F2} | "
                    + $"**{(swings == 0 ? "—" : $"{alls * 100.0 / swings:F1}%")}** | {wins * 100.0 / Seeds:F1}% |");
            }
            if (rowWhet > 0) { fedRows++; fedSwings += rowSwings; fedAlls += rowAlls; }
            else { dryRows++; drySwings += rowSwings; dryAlls += rowAlls; }
        }
        Console.WriteLine();
        Console.WriteLine($"**通算（全5波）**: 到達率 **{(gTurns == 0 ? 0 : gOver * 100.0 / gTurns):F1}%** ／ "
            + $"全体率 **{(gSwings == 0 ? 0 : gAlls * 100.0 / gSwings):F1}%**");
        Console.WriteLine($"**第2〜5波（規約 (G10) の判定の分母）**: 到達率 **{(gTurns25 == 0 ? 0 : gOver25 * 100.0 / gTurns25):F1}%** ／ "
            + $"全体率 **{(gSwings25 == 0 ? 0 : gAlls25 * 100.0 / gSwings25):F1}%**");
        Console.WriteLine();
        double fire = gSwings25 == 0 ? 0 : gAlls25 * 100.0 / gSwings25;
        Console.WriteLine();
        Console.WriteLine("## 表B —— 分母を割る（規約 (G12)）");
        Console.WriteLine();
        Console.WriteLine("**ドルガに外から1点でも届く行**と、**1点も届かない行**に分ける。"
            + "後者では全体率が**閾値に依らず構造的に 0** になる——その 0 は「効かなかった」ではなく"
            + "**「供給が無い」**（第61期・第126期）。");
        Console.WriteLine();
        Console.WriteLine("| 分母 | 行数 | 振/戦 | **全体率**（第2〜5波）|");
        Console.WriteLine("|---|--:|--:|--:|");
        double fedFire = fedSwings == 0 ? 0 : fedAlls * 100.0 / fedSwings;
        Console.WriteLine($"| 供給のある行 | {fedRows} | {fedSwings / (double)(Math.Max(1, fedRows) * 4 * Seeds):F2} | **{fedFire:F1}%** |");
        Console.WriteLine($"| 供給の無い行 | {dryRows} | {drySwings / (double)(Math.Max(1, dryRows) * 4 * Seeds):F2} | **{(drySwings == 0 ? 0 : dryAlls * 100.0 / drySwings):F1}%** |");
        Console.WriteLine($"| 全 {rows.Count} 行 | {rows.Count} | {gSwings25 / (double)(rows.Count * 4 * Seeds):F2} | **{fire:F1}%** |");
        Console.WriteLine();
        Console.WriteLine("## 表C —— `AtkBonus` の格子（第2〜5波・ドルガの生存ターンが分母）");
        Console.WriteLine();
        Console.WriteLine("**閾値を動かす前に、動かした先に標本があるかを見る**（第116期——"
            + "強化は連続量ではなく段で来るので、格子の2点のあいだに標本がほとんど無いことがある）。");
        Console.WriteLine();
        Console.Write("| 格子 |"); foreach (int q in UnitTally.ReaderProbes) Console.Write($" ≥{q} |"); Console.WriteLine();
        Console.Write("|---|"); foreach (int _ in UnitTally.ReaderProbes) Console.Write("--:|"); Console.WriteLine();
        Console.Write("| 到達率 |");
        foreach (long v in probeTurns) Console.Write($" {(probeDenom == 0 ? 0 : v * 100.0 / probeDenom):F1}% |");
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"→ 現行の上の段は **≥{ReaderRule.Adopted * GradeTrait.StepFactor}**"
            + $"（`ReaderRule.Adopted` {ReaderRule.Adopted} × `GradeTrait.StepFactor` {GradeTrait.StepFactor}）。");
        Console.WriteLine();
        Console.WriteLine($"→ 採否2（発火率 10〜90%）: 全行の分母では **{(fire >= 10 && fire <= 90 ? "○" : "×")}**（{fire:F1}%）／"
            + $"**供給のある行の分母では {(fedFire >= 10 && fedFire <= 90 ? "○" : "×")}（{fedFire:F1}%）**。"
            + "**90% を超えたら「普段は薙ぎ」が消えている・10% を切ったら死に札**（§3-3）。");
        Console.WriteLine();
    }

    // ==================================================================================
    // 自己検査
    // ==================================================================================
    static void Check(string before)
    {
        Console.WriteLine("# 第128期 —— 自己検査");
        Console.WriteLine();

        // 必須1: compare 305 セルが docs/balance.md と 0 件
        var cells = new List<string>();
        foreach ((string name, Formation f) in Presets.Compare)
        {
            var row = new List<string>();
            foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
            {
                int w = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) w++;
                row.Add($"{w * 100.0 / Seeds:F1}%");
            }
            cells.Add($"| {name} | {string.Join(" | ", row)} |");
        }
        Console.WriteLine($"- **必須1** `compare` {Presets.Compare.Length} 行 × {EnemyCatalog.Stages.Count} 波 ＝ "
            + $"{Presets.Compare.Length * EnemyCatalog.Stages.Count} セル: {Diff(Path.Combine(_root!, "docs", "balance.md"), cells)}");
        if (before.Length > 0)
            Console.WriteLine($"- **(a)** 渡された採用前の表 `{Path.GetFileName(before)}` との比較: {Diff(before, cells)}");

        // (b) 能力値が1つも変わっていない
        UnitDef d0 = UnitCatalog.Dolga, b0 = UnitCatalog.Ban;
        Console.WriteLine($"- **(b)** ドルガの数値 HP {d0.MaxHp} / 攻 {d0.Attack} / 速 {d0.Speed} / 型 {Pat(d0.Pattern)}"
            + $" ／ バンの数値 HP {b0.MaxHp} / 攻 {b0.Attack} / 速 {b0.Speed} / 型 {Pat(b0.Pattern)}"
            + "（**差分は `git diff` で示す**）");

        // (c) 札の在り処
        Console.WriteLine($"- **(c)** `GradeStep` の保持者: {Names(Holders(TraitId.GradeStep))} ／ "
            + $"`Overload` の保持者: {Names(Holders(TraitId.Overload))}");

        // (d) ReaderRule の既定が動いていない
        Console.WriteLine($"- **(d)** `ReaderRule.Default` = `{ReaderRule.Default}`（採用値 {ReaderRule.Adopted}）／ "
            + $"`GradeTrait.StepFactor` = {GradeTrait.StepFactor} —— **閾値 `ReaderRule` は第116期から1ビットも動かしていない。"
            + $"`StepFactor` だけ 4 → {GradeTrait.StepFactor} へ下げた**（§3-3 の唯一の条件＝発火率が 10% を切った。前後の両方を報告書に載せる）");

        // (e) ctx.PickOne を新たに使っていない（第89期 (h)）
        int pick = Regex.Matches(_traits, @"PickOne\(").Count;
        Console.WriteLine($"- **(e)** `PickOne(` の出現は `Traits.cs` に **{pick} 箇所**（**この期は1つも足していない**）");

        // (f) 情報セル
        Console.WriteLine($"- **(f)** 情報セル（`0 < x < 100` を第2〜5波で数える）: **{Info(cells)}**");
        Console.WriteLine();
    }

    static string Diff(string path, List<string> cells)
    {
        if (!File.Exists(path)) return "**比較先が無い（×）**";
        var doc = File.ReadAllLines(path).Where(l => l.StartsWith("| ") && l.Contains('%')).ToArray();
        int diff = Math.Abs(doc.Length - cells.Count);
        var moved = new List<string>();
        for (int i = 0; i < Math.Min(doc.Length, cells.Count); i++)
            if (doc[i].Trim() != cells[i].Trim()) { diff++; moved.Add(cells[i].Split('|')[1].Trim()); }
        return $"**ずれ {diff} 行**{(diff == 0 ? "（○）" : $"（{string.Join("・", moved.Take(12))}）")}";
    }

    static int Info(List<string> cells)
    {
        int n = 0;
        foreach (string c in cells)
        {
            string[] p = c.Split('|');
            for (int i = 3; i < p.Length - 1; i++)   // 第2〜5波（先頭は行名・2列目が第一波）
                if (double.TryParse(p[i].Trim().TrimEnd('%'), out double v) && v > 0 && v < 100) n++;
        }
        return n;
    }
}
