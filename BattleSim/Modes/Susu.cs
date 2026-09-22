using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// =====================================================================================
// susu モード（第179期） —— A群の転生 1枚目：拾い屋のスス（裂きのキリの枠）
//
// 指示書は design/PHASE179_SUSU_SPEC.md ／ 報告は design/PHASE179_SUSU.md。
//
// **線は置かない**（指示書 §0）。回帰確認（ススを含まない行が動かないこと）と、
// ススを入れた行の変化表だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 susu phase0  # 前提を実装から引き直す（戦闘は供給の実測ぶんだけ）
//     dotnet run --project BattleSim -c Release 0 susu run     # §2-2 ススを入れた行の変化表
//     dotnet run --project BattleSim -c Release 0 susu foes    # §2-3 敵の体数への反応
//     dotnet run --project BattleSim -c Release 0 susu check   # 自己検査
//
// **`Presets` は1行も触らない**（キリの2行はそのまま残る＝盤面は1ビットも動かない）ので、
// ススを載せた台は**この診断のローカル**で組む（第60期の火選りの移設と同じ扱い）。
// =====================================================================================

static class SusuDiag
{
    const int Seeds = 200;

    /// <summary>灰の規則の既定（第179期の仮置き）。</summary>
    static readonly AshRule V1 = AshRule.Default;

    /// <summary>プラスを 0 にした対照（撒いても1点も出ない）。</summary>
    static readonly AshRule NoThrow = AshRule.Default with { Multiplier = 0 };

    /// <summary>マイナスを 0 にした対照（倒れても灰が降らない）。</summary>
    static readonly AshRule NoFallout = AshRule.Default with { FalloutOnDeath = false };

    /// <summary>追補前の形（毎手番投げる）。<b>`ThrowEvery = 1` で1ビットも違わずに戻る。</b></summary>
    static readonly AshRule Every1 = AshRule.Default with { ThrowEvery = 1 };

    /// <summary>既定1 でマイナスだけを 0 にした対照。</summary>
    static readonly AshRule Every1NoFallout = Every1 with { FalloutOnDeath = false };

    /// <summary>§2-2 で見る行（指示書が名指しした6行）。</summary>
    static readonly string[] Rows =
    {
        "燃焼 (ボルグ×ホタ)",
        "火選り (ヒヨ×ホタ)",
        "反撃 (ヒサ×カド)",
        "反撃改 (ドハ×カド)",
        "毒+耐久 (ベニ×トウ)",
        "死軸×ヒヨ (ゾト×火選り)",
        // 第179期 追補: **味方由来ダメージが 61 行で最大の行**（291.1 / 戦）。
        // 代金（抱えたまま倒れる）がいちばん出やすい台として足した。
        "惨禍×死の連鎖",
    };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": StageRun(); return;
            case "foes": Foes(); return;
            case "check": Check(); return;
            default:
                Console.WriteLine("susu: モードは phase0 / run / foes / check。");
                return;
        }
    }

    // =================================================================================
    // Phase 0 —— 前提を実装から引き直す
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第179期 `susu phase0` —— 前提を実装から引き直す");
        Console.WriteLine();
        if (!ParryScan.Init()) return;

        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));

        // --- Q0-1 / Q0-2: ApplyDamage の呼び出し口を全数で引く -----------------------
        // **検索文字列は連結で組む**（この診断自身がリポジトリ内のファイル。第123期・R035）。
        string call = "Apply" + "Damage(";
        string ff = "isFriendly" + "Fire: true";

        var dot = new List<string>();
        var ally = new List<string>();
        foreach ((string file, string text) in new[] { ("BattleEngine.cs", engine), ("Traits.cs", traits) })
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i];
                if (ln.TrimStart().StartsWith("//")) continue;
                if (!ln.Contains(call)) continue;
                // 2行にまたがる呼び出しがあるので、次の行までを1つの塊として見る
                string blob = ln + (i + 1 < lines.Length ? " " + lines[i + 1].Trim() : "");
                if (Regex.IsMatch(blob, @",\s*null[,)]")) dot.Add(file + ":" + (i + 1) + "  " + ln.Trim());
                if (blob.Contains(ff)) ally.Add(file + ":" + (i + 1) + "  " + ln.Trim());
            }
        }

        Console.WriteLine("## Q0-1. 出どころの無い削り（`source` が `null`）の呼び出し口");
        Console.WriteLine();
        Console.WriteLine("**" + dot.Count + " 本**（`AshRule.CountDot` が真ならここが全部灰になる）。");
        Console.WriteLine();
        foreach (string d in dot) Console.WriteLine("- `" + d + "`");
        Console.WriteLine();

        Console.WriteLine("## Q0-2. 味方の刃として落ちる呼び出し口（`isFriendlyFire: true`）");
        Console.WriteLine();
        Console.WriteLine("**" + ally.Count + " 本**。**ただしこれは全数ではない**"
                          + "——出どころが味方なら札が無くても灰になるので（判定は `source.TeamId`）、"
                          + "棘の巻き込み・破裂・生贄・吸いのように陣営で自然に決まる経路はここに出ない。");
        Console.WriteLine();
        foreach (string a in ally) Console.WriteLine("- `" + a + "`");
        Console.WriteLine();

        // --- Q0-3: 手番に全体攻撃を撃つ既存の駒 --------------------------------------
        Console.WriteLine("## Q0-3. 手番（`Actions` に `Skill`）を持つ味方の駒");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 札 | 種別 |");
        Console.WriteLine("|---|---|---|");
        foreach (UnitDef d in UnitCatalog.Everyone)
        {
            if (d.Actions is null || d.Actions.Count == 0) continue;
            Console.WriteLine("| " + d.Name + " | " + string.Join(" / ", d.Traits) + " | "
                              + string.Join(" → ", d.Actions.Select(a => a.Kind.ToString())) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**礫（ガレ）の形をそのまま流用できる**——`[Skill]` の1要素・`OnAction` で"
                          + "`ctx.LivingMembers(ctx.Opponent(...))` を回して `ApplyDamage(pattern: All)`。"
                          + "**違うのは1点だけ**で、ススは灰が無い手番に素の一撃を振る"
                          + "（礫は `CanAct` を偽にして手番ごと捨てる）。");
        Console.WriteLine();

        // --- Q0-4: キリを外したときに動くもの ----------------------------------------
        bool kiriInAll = UnitCatalog.All.Any(d => d.Id == "kiri");
        bool kiriRetired = UnitCatalog.Retired.Any(d => d.Id == "kiri");
        bool susuInAll = UnitCatalog.All.Any(d => d.Id == "susu");
        var kiriRows = CompareBuilds().Where(b => b.F.Occupied().Any(o => o.Def.Id == "kiri")).Select(b => b.Name).ToList();
        var crossKiri = CrossBuilds().Where(b => b.F.Occupied().Any(o => o.Def.Id == "kiri")).Select(b => b.Name).ToList();

        Console.WriteLine("## Q0-4. キリを `All` から外したとき動くもの");
        Console.WriteLine();
        Console.WriteLine("- `UnitCatalog.All` の枚数: **" + UnitCatalog.All.Count + " 枚**"
                          + "（キリ在籍 " + (kiriInAll ? "○" : "**×**")
                          + " ／ スス在籍 " + (susuInAll ? "○" : "**×**")
                          + " ／ キリは `Retired` " + (kiriRetired ? "○" : "**×**") + "）");
        Console.WriteLine("- `compare` でキリを含む行: **" + kiriRows.Count + " 行**"
                          + (kiriRows.Count == 0 ? "" : "（" + string.Join(" ／ ", kiriRows) + "）"));
        Console.WriteLine("- 交差帯でキリを含む行: **" + crossKiri.Count + " 行**"
                          + (crossKiri.Count == 0 ? "" : "（" + string.Join(" ／ ", crossKiri) + "）"));
        Console.WriteLine();
        Console.WriteLine("**`Presets` からは外さない**（第139期のエグと同じ扱い）。`All` は「編成に選べる 52 枚」の"
                          + "定義であって `Presets` が参照できる集合ではないので（第108期）、"
                          + "**差し替えだけでは盤面は1ビットも動かない**——それが自己検査 (a) の中身である。");
        Console.WriteLine();

        // --- Q0-5: 味方由来のダメージを資源にする既存の札 -----------------------------
        Console.WriteLine("## Q0-5. 「味方由来のダメージ」を入力にする既存の札");
        Console.WriteLine();
        Console.WriteLine("| 札 | 何を読むか | 灰との違い |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| `Colossus`（巨躯・ゴルム） | **自分が肩代わりした量** | 自分に来た分だけ。盤面の他所の自傷は読まない |");
        Console.WriteLine("| `Shatter`（砕け・ヒビ） | **自分が浴びた範囲攻撃** | 同上。しかも敵の範囲が主な供給源 |");
        Console.WriteLine("| `Rage`（憤怒・ムド） | **自分の被弾**（出どころを問わない） | 敵からの被弾も等しく読む |");
        Console.WriteLine("| `Nourish`（糧・保持者0枚） | **徴収（`levy`）の札** | 殴った側が持っていく。陣営をまたぐ |");
        Console.WriteLine("| `SpillWound`（巻き込み則・既定オフ） | **味方の刃が通ったこと**（二値） | 量ではなく回数。第122期に降ろしてある |");
        Console.WriteLine();
        Console.WriteLine("**盤面全体の自傷を「量」で読む札は1枚も無い。** 近い3枚はどれも"
                          + "「**自分に来た分**」しか読まないので、供給の分母が自分の被弾に縛られる"
                          + "——灰はそこが違う（分母は編成が持ち込んだ自傷の総量）。");
        Console.WriteLine();

        // --- §1-1 の実測: 味方由来ダメージの供給量 -----------------------------------
        Console.WriteLine("## §1-1. 味方由来ダメージの供給量（実測・**盤面は触らない**）");
        Console.WriteLine();
        Console.WriteLine("`UnitTally.TakenFromAlly` の味方側の合計（第2〜5波の平均・seed 0..49）。"
                          + "**出どころの無い刻み（燃焼・毒）はここに出ない**"
                          + "（`TakenFromAlly` は `source` が味方の分だけを数える）ので、"
                          + "**灰の実量はこれより大きい**。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 味方由来 / 戦 |");
        Console.WriteLine("|---|--:|");
        var supply = new List<(string Name, double V)>();
        foreach ((string name, Formation f) in CompareBuilds())
        {
            double tot = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    foreach (var o in f.Occupied())
                        if (r.TallyByUnit.TryGetValue(o.Def.Id, out UnitTally? t)) tot += t.TakenFromAlly;
                    n++;
                }
            supply.Add((name, tot / n));
        }
        foreach ((string name, double v) in supply.OrderByDescending(x => x.V).Take(15))
            Console.WriteLine("| " + name + " | " + v.ToString("F1") + " |");
        Console.WriteLine();
        Console.WriteLine("- 61 行の中央値: **" + Median(supply.Select(x => x.V).ToList()).ToString("F1") + "**");
        Console.WriteLine("- 供給が 0 の行: **" + supply.Count(x => x.V < 0.05) + " / " + supply.Count + "**"
                          + "（この行では**スス は攻3 の体でしかない**）");
        Console.WriteLine();
    }

    // =================================================================================
    // §2-2 —— ススを入れた行の変化表
    // =================================================================================

    static void StageRun()
    {
        Console.WriteLine("# 第179期 `susu run` —— ススを入れた行の変化表（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("替える駒は「**その行で素体差し替えの帰属が最も低い1枚**」で機械的に選ぶ"
                          + "（`checkup ideal` と同じ定義＝第2〜5波の勝率の、素体に落としたときの下がり幅）。");
        Console.WriteLine();

        Console.WriteLine("## 表A. 替える駒の選定（素体差し替えの帰属・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 替える駒 | その駒の帰属 | 行の5枚の帰属 |");
        Console.WriteLine("|---|---|--:|---|");

        var picks = new List<(string Name, Formation F, UnitDef Drop)>();
        foreach (string row in Rows)
        {
            var hit = CompareBuilds().FirstOrDefault(b => b.Name == row);
            if (hit.F is null) { Console.WriteLine("| " + row + " | **行名が引けない** | — | — |"); continue; }
            double baseW = Avg25(hit.F, null);
            var attr = new List<(UnitDef D, double A)>();
            foreach (var o in hit.F.Occupied())
                attr.Add((o.Def, baseW - Avg25(SwapDef(hit.F, o.Def, Plain(o.Def)), null)));
            var lowest = attr.OrderBy(x => x.A).First();
            picks.Add((row, hit.F, lowest.D));
            Console.WriteLine("| " + row + " | **" + lowest.D.Name + "** | " + lowest.A.ToString("+0.0;-0.0;0.0")
                              + " | " + string.Join(" ／ ", attr.OrderBy(x => x.A)
                                    .Select(x => x.D.Name + " " + x.A.ToString("+0.0;-0.0;0.0"))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表B. 元の版 対 ススを入れた版（勝率・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**既定1** ＝ `ThrowEvery = 1`（毎手番投げる・追補前の形）／"
                          + "**既定2** ＝ `ThrowEvery = 2`（溜める → 投げるの2拍・**現在の既定**）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | Δ（元比） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f, UnitDef drop) in picks)
        {
            double[] a = Rates(f, null);
            Formation g = SwapDef(f, drop, UnitCatalog.Susu);
            double[] b1 = Rates(g, Every1), b2 = Rates(g, V1);
            Console.WriteLine("| " + name + " | 元 | " + Cells(a) + " | " + Mean25(a).ToString("F1") + " | — |");
            Console.WriteLine("| | スス（既定1） | " + Cells(b1) + " | " + Mean25(b1).ToString("F1") + " | "
                              + (Mean25(b1) - Mean25(a)).ToString("+0.0;-0.0;0.0") + " |");
            Console.WriteLine("| | **スス（既定2）** | " + Cells(b2) + " | " + Mean25(b2).ToString("F1") + " | **"
                              + (Mean25(b2) - Mean25(a)).ToString("+0.0;-0.0;0.0") + "** |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表C. ススの帳簿（第2〜5波の平均・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("**`抱えて倒れた灰`** は「倒れた瞬間に持っていた灰」、"
                          + "**`降った試行`** は「その灰が実際に隣へ降った戦の割合」"
                          + "——**隣が1人もいなければ降らない**ので、2つは一致しない。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 拍 | 溜めた灰 | 撒いた回数 | 撒いた灰 | 1回の最大 | 溜め番 | 素振り | 与えた総害 | 抱えて倒れた灰 | 降った灰 | **降った試行** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f, UnitDef drop) in picks)
        {
            Formation g = SwapDef(f, drop, UnitCatalog.Susu);
            foreach ((string tag, AshRule r) in new[] { ("既定1", Every1), ("**既定2**", V1) })
            {
                Led s = LedgerOf(g, r);
                Console.WriteLine("| " + name + " | " + tag + " | " + s.Gained.ToString("F1") + " | " + s.Fires.ToString("F2")
                                  + " | " + s.Spent.ToString("F1") + " | " + s.Peak.ToString("F1")
                                  + " | " + s.Holds.ToString("F2")
                                  + " | " + s.Dry.ToString("F2") + " | " + s.Dealt.ToString("F1")
                                  + " | " + s.AtDeath.ToString("F1") + " | " + s.FalloutAmt.ToString("F1")
                                  + " | **" + s.FalloutRate.ToString("F1") + "%** |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 表D. プラス／マイナスを1つずつ 0 にした対照");
        Console.WriteLine();
        Console.WriteLine("**`Multiplier = 0`** ＝ 溜めても撒いても1点も出ない（プラスが 0）／"
                          + "**`FalloutOnDeath = false`** ＝ 抱えて倒れても降らない（マイナスが 0）。");
        Console.WriteLine();
        Console.WriteLine("**代金 ＝ 既定 − 降らない**（負なら「降ることで損をしている」）。"
                          + "**拍ごとに別々に測る**——溜める番を挟むと在庫が乗っている時間が伸びるので、"
                          + "**代金が出るとしたらここで出る**。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 拍 | 既定 | 撒かない | 降らない | **代金** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f, UnitDef drop) in picks)
        {
            Formation g = SwapDef(f, drop, UnitCatalog.Susu);
            foreach ((string tag, AshRule r, AshRule nf) in new[]
                     { ("既定1", Every1, Every1NoFallout), ("**既定2**", V1, NoFallout) })
            {
                double d = Mean25(Rates(g, r)), o = Mean25(Rates(g, nf));
                double n = Mean25(Rates(g, r with { Multiplier = 0 }));
                Console.WriteLine("| " + name + " | " + tag + " | " + d.ToString("F1") + " | " + n.ToString("F1")
                                  + " | " + o.ToString("F1") + " | **" + (d - o).ToString("+0.0;-0.0;0.0") + "** |");
            }
        }
        Console.WriteLine();
    }

    // =================================================================================
    // §2-3 —— 敵の体数への反応
    // =================================================================================

    static void Foes()
    {
        Console.WriteLine("# 第179期 `susu foes` —— 敵の体数への反応");
        Console.WriteLine();
        Formation w1 = EnemyCatalog.Stages[0].Enemy;
        Formation vg = EnemyCatalog.Vanguards[0].Enemy;
        Formation m5 = EnemyCatalog.Stages[1].Enemy;
        Console.WriteLine("体数: 第一波 **" + w1.Occupied().Count() + "** ／ 第二波・先遣 **"
                          + vg.Occupied().Count() + "** ／ 第二波 M5 **" + m5.Occupied().Count() + "**");
        Console.WriteLine();
        Console.WriteLine("**先遣と M5 は同じ波から後1 を抜いただけ**なので、体数以外はほぼ揃っている"
                          + "（第一波だけは別の敵）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 敵 | 体数 | 溜めた灰 | 撒いた灰 | ススの与えた総害 | 総害 ÷ 撒いた灰 | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");

        foreach (string row in Rows)
        {
            var hit = CompareBuilds().FirstOrDefault(b => b.Name == row);
            if (hit.F is null) continue;
            double baseW = Avg25(hit.F, null);
            UnitDef drop = hit.F.Occupied()
                .OrderBy(o => baseW - Avg25(SwapDef(hit.F, o.Def, Plain(o.Def)), null)).First().Def;
            Formation g = SwapDef(hit.F, drop, UnitCatalog.Susu);

            foreach ((string label, Formation foe) in new[]
                     { ("第一波", w1), ("第二波・先遣", vg), ("第二波 M5", m5) })
            {
                Led s = LedgerVs(g, foe, V1);
                Console.WriteLine("| " + row + " | " + label + " | " + foe.Occupied().Count()
                                  + " | " + s.Gained.ToString("F1") + " | " + s.Spent.ToString("F1")
                                  + " | " + s.Dealt.ToString("F1") + " | "
                                  + (s.Spent < 0.05 ? "—" : (s.Dealt / s.Spent).ToString("F2"))
                                  + " | " + s.Win.ToString("F1") + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("**比が体数に比例していれば「全体攻撃として素直に効いている」**。"
                          + "比が体数より小さければ、上限（軛）か軽減か**過剰殺傷**で落ちている。");
        Console.WriteLine();
    }

    // =================================================================================
    // 自己検査
    // =================================================================================

    static void Check()
    {
        Console.WriteLine("# 第179期 `susu check` —— 自己検査");
        Console.WriteLine();

        // (a') ススを含まない行では灰が1点も溜まらない
        long stray = 0;
        foreach ((string _, Formation f) in CompareBuilds())
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 10; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, ash: V1);
                    foreach (UnitTally t in r.TallyByUnit.Values) stray += t.AshGained + t.AshSpent;
                }
        Console.WriteLine("- (a') ススを含まない 61 行で溜まった灰: **" + stray + "**（0 が正）");

        // (b') 規則を振っても、ススがいない行は1セルも動かない
        int cells = 0;
        foreach ((string _, Formation f) in CompareBuilds())
        {
            double[] x = Rates(f, V1), y = Rates(f, NoThrow), z = Rates(f, NoFallout);
            for (int i = 0; i < 5; i++)
                if (Math.Abs(x[i] - y[i]) > 0.001 || Math.Abs(x[i] - z[i]) > 0.001) cells++;
        }
        Console.WriteLine("- (b') `AshRule` を3版に振ったときに動いた `compare` のセル: **" + cells + " / 305**（0 が正）");

        // (c') 灰の収支が閉じる（溜めた ＝ 撒いた ＋ 抱えて倒れた ＋ 決着時の残り）
        var probe = CompareBuilds().First(b => b.Name == Rows[0]);
        double bw = Avg25(probe.F, null);
        UnitDef drop = probe.F.Occupied()
            .OrderBy(o => bw - Avg25(SwapDef(probe.F, o.Def, Plain(o.Def)), null)).First().Def;
        Formation g = SwapDef(probe.F, drop, UnitCatalog.Susu);
        long gained = 0, spent = 0, atDeath = 0, left = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(g, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, ash: V1);
                if (!r.TallyByUnit.TryGetValue(UnitCatalog.Susu.Id, out UnitTally? t)) continue;
                gained += t.AshGained; spent += t.AshSpent; atDeath += t.AshAtDeath;
                left += t.AshResidual;
            }
        Console.WriteLine("- (c') 灰の収支: 溜めた **" + gained + "** ＝ 撒いた " + spent
                          + " ＋ 抱えて倒れた " + atDeath + " ＋ 決着時の残り " + left
                          + " ＝ **" + (spent + atDeath + left) + "**"
                          + (gained == spent + atDeath + left ? "（一致）" : "（**閉じていない**）"));

        // (d') 新しく PickOne を使っていない
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            int i = traits.IndexOf("class Ash" + "Trait");
            int j = i < 0 ? -1 : traits.IndexOf("\npublic ", i + 10);
            string body = i < 0 ? "" : traits.Substring(i, (j < 0 ? traits.Length : j) - i);
            string pick = "Pick" + "One";
            Console.WriteLine("- (d') `AshTrait` が `" + pick + "` を呼ぶ回数: **"
                              + (body.Length - body.Replace(pick, "").Length) / pick.Length + "**（0 が正）");
        }

        // (e') ロスター
        Console.WriteLine("- (e') `All` " + UnitCatalog.All.Count + " 枚 ／ キリ在籍 "
                          + (UnitCatalog.All.Any(d => d.Id == "kiri") ? "**○（誤り）**" : "×（正）")
                          + " ／ スス在籍 " + (UnitCatalog.All.Any(d => d.Id == "susu") ? "○（正）" : "**×（誤り）**")
                          + " ／ `Retired` に キリ "
                          + (UnitCatalog.Retired.Any(d => d.Id == "kiri") ? "○（正）" : "**×（誤り）**")
                          + " ／ `ById(\"kiri\")` " + UnitCatalog.ById("kiri").Name);

        // (g') 台本に「敵全体への一撃」として載るか（`--demo-smoke` の代わり。**`Presets` を触らないため**）
        {
            var hit = CompareBuilds().First(b => b.Name == Rows[0]);
            double w = Avg25(hit.F, null);
            UnitDef dr = hit.F.Occupied()
                .OrderBy(o => w - Avg25(SwapDef(hit.F, o.Def, Plain(o.Def)), null)).First().Def;
            Formation gg = SwapDef(hit.F, dr, UnitCatalog.Susu);
            BattleResult rr = BattleEngine.Run(gg, EnemyCatalog.Stages[1].Enemy, 0, verbose: true, ash: V1);
            int all = rr.Events.Count(e => e.Kind == BattleEventKind.Damage && e.Pattern == AttackPattern.All);
            int snap = rr.Events.Count(e => e.Kind == BattleEventKind.StatusSnapshot && e.Text == "灰");
            int throws = rr.Log.Count(l => l.Text.Contains("溜めた灰を撒いた"));
            Console.WriteLine("- (g') 1戦（" + Rows[0] + " × 第二波 × seed 0）の台本: "
                              + "撒いた回数 " + throws + " ／ **全体ダメージ** " + all + " 件 ／ 灰のスナップショット "
                              + snap + " 件（3つとも 1 件以上が正）");
        }

        // (f') 状態キーの一覧
        Console.WriteLine("- (f') `StatusKeys.All` の本数: **" + StatusKeys.All.Length + "**"
                          + "（灰 " + (StatusKeys.All.Contains(StatusKeys.Ash) ? "○" : "**×**")
                          + "＝会戦の境界で消える）");
        Console.WriteLine();
    }

    // =================================================================================
    // ヘルパ
    // =================================================================================

    readonly record struct Led(double Gained, double Fires, double Spent, double Peak,
                               double Dry, double Dealt, double AtDeath, double Survivors, double Win,
                               double Holds, double FalloutAmt, double FalloutRate);

    /// <summary>ススの帳簿（第2〜5波の平均）。</summary>
    static Led LedgerOf(Formation f, AshRule r)
    {
        double g = 0, fi = 0, sp = 0, pk = 0, dr = 0, de = 0, ad = 0, sv = 0, win = 0;
        double ho = 0, fa = 0, ft = 0; int n = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, ash: r);
                n++;
                sv += res.PlayerSurvivors;
                if (res.PlayerWon) win++;
                if (!res.TallyByUnit.TryGetValue(UnitCatalog.Susu.Id, out UnitTally? t)) continue;
                g += t.AshGained; fi += t.AshFires; sp += t.AshSpent; pk += t.AshPeak;
                dr += t.AshDry; de += t.DamageToEnemy; ad += t.AshAtDeath;
                ho += t.AshHolds; fa += t.AshFalloutOut;
                if (t.AshFalloutOut > 0) ft++;          // **降った試行**（量ではなく件数）
            }
        return new Led(g / n, fi / n, sp / n, pk / n, dr / n, de / n, ad / n, sv / n, 100.0 * win / n,
                       ho / n, fa / n, 100.0 * ft / n);
    }

    /// <summary>ススの帳簿（ある敵1つだけに対して）。</summary>
    static Led LedgerVs(Formation f, Formation foe, AshRule r)
    {
        double g = 0, fi = 0, sp = 0, de = 0, win = 0; int n = 0;
        for (int seed = 0; seed < Seeds; seed++)
        {
            BattleResult res = BattleEngine.Run(f, foe, seed, verbose: false, ash: r);
            n++;
            if (res.PlayerWon) win++;
            if (!res.TallyByUnit.TryGetValue(UnitCatalog.Susu.Id, out UnitTally? t)) continue;
            g += t.AshGained; fi += t.AshFires; sp += t.AshSpent; de += t.DamageToEnemy;
        }
        return new Led(g / n, fi / n, sp / n, 0, 0, de / n, 0, 0, 100.0 * win / n, 0, 0, 0);
    }

    /// <summary>同じ数値・特性なしの素体（第69期からの器具）。</summary>
    static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name, MaxHp = d.MaxHp,
        Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
        Traits = Array.Empty<TraitId>()
    };

    static Formation SwapDef(Formation f, UnitDef from, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, from) ? to : d;
        return g;
    }

    static double[] Rates(Formation f, AshRule? r, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, ash: r).PlayerWon)
                    wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static double Avg25(Formation f, AshRule? r) => Mean25(Rates(f, r));
    static string Cells(double[] w) => string.Join(" | ", Enumerable.Range(1, 4).Select(i => w[i].ToString("F1")));

    static double Median(List<double> xs)
    {
        if (xs.Count == 0) return 0;
        xs.Sort();
        return xs.Count % 2 == 1 ? xs[xs.Count / 2] : (xs[xs.Count / 2 - 1] + xs[xs.Count / 2]) / 2.0;
    }
}
