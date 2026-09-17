using BattleCore;
using static Common;

// =====================================================================================
// derange モード（第147期） —— バサの転倒を**混乱**に置き換える
//
// 指示書は design/PHASE147_CONFUSE_SPEC.md ／ 報告は design/PHASE147_CONFUSE.md。
//
// **対照 V0 は「転倒版」**（＝`ShufflerRule.Default`）。第143期（ササの転生）の作法で、
// この期の問いは「置き換えて良くなるか」であって「足して良くなるか」ではない。
//
//     dotnet run --project BattleSim -c Release 0 derange phase0   # 前提を実装から引き直す（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 derange scan     # 台の下見（V0 だけ）
//     dotnet run --project BattleSim -c Release 0 derange run      # 段A: 転倒 → 混乱（絞りなし）
//     dotnet run --project BattleSim -c Release 0 derange knob     # 段B / 段B': 確率と回数制を同じ帯で
//     dotnet run --project BattleSim -c Release 0 derange compare  # 拒否権（compare 61行 ＋ 交差帯12行）
//     dotnet run --project BattleSim -c Release 0 derange check    # 自己検査
// =====================================================================================

static class DerangeDiag
{
    const int Seeds = 200;

    /// <summary>
    /// 対照。<b>転倒版</b>（第144期の既定）。
    /// <b>第147期 段C で既定が混乱へ移ったので、V0 は `ShufflerRule.Default` ではなく
    /// `Stagger144` を名指しする</b>——採用で既定が動いた診断は検算の相手が移る（第60期）。
    /// </summary>
    static readonly ShufflerRule V0 = ShufflerRule.Stagger144;

    /// <summary>段A。転倒を混乱に置き換える（絞りなし）。</summary>
    static readonly ShufflerRule V1 = new(true, ShuffleStagger.Confuse);

    /// <summary>
    /// 段C の採用値。<b>回数制（保持者1体・1戦あたり3回まで）。</b>
    /// 段B / 段B' を同じ帯で比べた結果、<b>同じ供給量なら回数制のほうが帰属が 5.6〜8.8pt 高い</b>。
    /// <b>第147期 段C でこれが `ShufflerRule.Default` になった</b>（自己検査 (b) が同値を確かめる）。
    /// </summary>
    static readonly ShufflerRule VC = new(true, ShuffleStagger.Confuse, ConfuseUses: 3);

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": Scan(); return;
            case "run": Stage(); return;
            case "knob": Knobs(); return;
            case "compare": CompareRows(arg.StartsWith("a") ? "a" : "c"); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("derange: モードは phase0 / scan / run / knob / compare / check。");
                return;
        }
    }

    // =================================================================================
    // 台 —— **第146期の4台をそのまま使う**（V0 が1ビットも同じなので下見が流用できる）
    // =================================================================================

    /// <summary>埋め草は<b>特性を1つも持たない素体</b>（第143期の則）。</summary>
    static UnitDef Plain(string id, int hp, int atk, int spd = 8) => new()
    {
        Id = id, Name = "素体" + id, MaxHp = hp, Attack = atk, Speed = spd,
        Advances = true, Traits = Array.Empty<TraitId>()
    };

    static UnitDef Fill(string id) => Plain(id, 110, 34);

    static (string Name, Formation F)[] Rigs() => new (string, Formation)[]
    {
        // 被弾変換が厚い。**混乱が餌を減らす代金になるか**（予測2）。
        ("被弾変換が厚い", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Doha,
            back1: UnitCatalog.Basa, back3: Fill("pa"))),

        // 打点だけ。**混乱が純粋な得になるか**（予測1）。
        ("被弾変換が薄い", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: UnitCatalog.Basa, back3: Fill("pa"))),

        // ササ ＋ ガレ。**混乱は敵→敵なので供給に化けないはず**（予測3）。
        ("ササ同席", Formation.Build(
            front1: UnitCatalog.Sasa, front3: UnitCatalog.Gare, center: UnitCatalog.Borg,
            back1: UnitCatalog.Basa, back3: Fill("pa"))),

        // 陰性対照。**バサを同数値・特性なしの素体に落としただけ**（HP56 / 攻7 / 速8）。
        // 版を振っても 1 ビットも動かないことが特異性の検算（予測7）。
        ("動かす機構なし", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: Plain("pb", 56, 7), back3: Fill("pa"))),
    };

    // =================================================================================
    // phase0 —— 前提を実装から引き直す（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第147期 `derange phase0` —— 前提を実装から引き直す（戦闘0回）");
        Console.WriteLine();

        string? eng = FindSource("BattleEngine.cs"), tr = FindSource("Traits.cs");
        if (eng is null || tr is null)
        {
            Console.WriteLine("**`BattleCore` のソースが引けない。止める**（第117期）。");
            return;
        }
        string[] E = File.ReadAllLines(eng), T = File.ReadAllLines(tr);

        Console.WriteLine("## Q0-1. ノブの形");
        Console.WriteLine();
        Console.WriteLine("- `ShuffleStagger` の枝: **" + string.Join(" / ", Enum.GetNames(typeof(ShuffleStagger))) + "**");
        Console.WriteLine("- `ShufflerRule` の既定: `" + ShufflerRule.Default + "`");
        Console.WriteLine("- `ConfusionRule` の既定: `" + ConfusionRule.Default + "`（波ルール版・第146期の残置）");
        Console.WriteLine();

        Console.WriteLine("## Q0-2. 混乱を立てる箇所と読む箇所");
        Console.WriteLine();
        Console.WriteLine("| 役 | 場所 |");
        Console.WriteLine("|---|---|");
        for (int i = 0; i < T.Length; i++)
            if (T[i].Contains("SetCounter(StatusKeys.Confused"))
                Console.WriteLine("| 立てる（喧噪） | `Traits.cs:" + (i + 1) + "` |");
        for (int i = 0; i < E.Length; i++)
        {
            if (E[i].Contains("SetCounter(StatusKeys.Confused, 1)"))
                Console.WriteLine("| 立てる（波ルール版） | `BattleEngine.cs:" + (i + 1) + "` |");
            if (E[i].Contains("ConfusionLive &&") || E[i].Contains("!ConfusionLive ||"))
                Console.WriteLine("| 読む | `BattleEngine.cs:" + (i + 1) + "` |");
        }
        Console.WriteLine();
        Console.WriteLine("**読む側は `Confusion.Active` を見ていない**"
                          + "——ノブは「誰が立てるか」を決め、読む側はキーが立っていれば従う。");
        Console.WriteLine();

        Console.WriteLine("## Q0-3. 混乱の下流（陣営を見ない札）");
        Console.WriteLine();
        int exe = Array.FindIndex(T, l => l.Contains("class ExecutionerTrait"));
        int necro = Array.FindIndex(T, l => l.Contains("if (dead.TeamId != self.TeamId)"));
        int mar = Array.FindIndex(E, l => l.Contains("f.HasTrait(TraitId.Martyr)"));
        Console.WriteLine("| 札 | 場所 | 陣営を見るか |");
        Console.WriteLine("|---|---|:-:|");
        Console.WriteLine("| 処刑（`ExecutionerTrait.OnKill`） | `Traits.cs:" + (exe + 1) + "` | **見ない** |");
        Console.WriteLine("| 殉教（鎖の4段目） | `BattleEngine.cs:" + (mar + 1) + "` | **見ない**（`foes` は攻撃者の相手集合） |");
        Console.WriteLine("| 墓守（`NecroTrait.OnAnyDeath`） | `Traits.cs:" + (necro + 1) + "` | **見る**（層は積まれない） |");
        Console.WriteLine();

        Console.WriteLine("## Q0-4. 表示");
        Console.WriteLine();
        bool kind = Enum.GetNames(typeof(BattleEventKind)).Contains("Confused");
        Console.WriteLine("- `BattleEventKind` に `Confused` があるか: " + (kind ? "**○**" : "**×**"));
        Console.WriteLine("- `ConfusedLabels.All`: " + string.Join(" / ", ConfusedLabels.All));
        Console.WriteLine("- **キーの定数名と列挙の名前が一致**（盲点表の「専用の種類」の列が機械で照合する）: "
                          + (kind ? "**○**" : "**×**"));
    }

    // =================================================================================
    // scan —— 台の下見（版を振らない）
    // =================================================================================

    static void Scan()
    {
        Console.WriteLine("# 第147期 `derange scan` —— 台の下見（V0 ＝ 転倒版だけ）");
        Console.WriteLine();
        Console.WriteLine("**採る条件は第61・63期の帯: 第2〜5波平均が 40〜95%。** 情報セルは第2〜5波で `0 < x < 100`。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | 情報セル | 帯 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|:-:|");
        foreach ((string name, Formation f) in Rigs())
        {
            double[] w = Rates(f, V0);
            double avg = w.Skip(1).Average();
            Console.WriteLine("| " + name + " | " + string.Join(" | ", w.Select(x => x.ToString("F1")))
                              + " | **" + avg.ToString("F1") + "** | "
                              + w.Skip(1).Count(x => x > 0.0 && x < 100.0) + "/4 | "
                              + (avg >= 40 && avg <= 95 ? "○" : "**×**") + " |");
        }
    }

    // =================================================================================
    // run —— 段A: 転倒 → 混乱（絞りなし）
    // =================================================================================

    static void Stage()
    {
        Console.WriteLine("# 第147期 `derange run` —— 段A: 転倒を混乱に置き換える（絞りなし）");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ 転倒版**（`ShufflerRule.Default`）。第143期の作法で、差し替え前を対照に取る。");
        Console.WriteLine("**波ごとの効き方は生の pt で比べない**（第144期）——`余地に対する取り分` ＝");
        Console.WriteLine("Δ ÷ (上がったなら 100 − V0 ／ 下がったなら V0)。");
        Console.WriteLine();

        Console.WriteLine("## 表A. 勝率（段A − V0）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | V0 転倒 | 段A 混乱 | Δ | 余地に対する取り分 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in Rigs())
        {
            double[] a = Rates(f, V0), b = Rates(f, V1);
            for (int st = 1; st < 5; st++)
            {
                double room = b[st] >= a[st] ? 100.0 - a[st] : a[st];
                string share = room <= 0.001 ? "—" : ((b[st] - a[st]) / room).ToString("+0.000;-0.000");
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + a[st].ToString("F1")
                                  + " | " + b[st].ToString("F1") + " | "
                                  + (b[st] - a[st]).ToString("+0.0;-0.0") + " | " + share + " |");
            }
            Console.WriteLine("| **" + name + "** | **第2〜5波** | **" + a.Skip(1).Average().ToString("F1")
                              + "** | **" + b.Skip(1).Average().ToString("F1") + "** | **"
                              + (b.Skip(1).Average() - a.Skip(1).Average()).ToString("+0.0;-0.0") + "** | |");
        }

        Console.WriteLine();
        Console.WriteLine("## 表B. 混乱の帳簿（段A・回/戦）");
        Console.WriteLine();
        Console.WriteLine("**`前へ出た` は喧噪が前へ出した敵の延べ体数**（`ShuffleAdvanced`）、");
        Console.WriteLine("**`立てた` は実際に混乱を立てた回数**（`ShuffleConfuses`。差は「既に混乱していた」ぶん）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 前へ出た | 立てた | 敵に立った | 敵が振った | **味方に立った** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in Rigs())
            for (int st = 1; st < 5; st++)
            {
                Led l = Ledger(f, st, V1, Seeds);
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | "
                                  + l.Advanced.ToString("F2") + " | " + l.Confuses.ToString("F2") + " | "
                                  + l.FoeMarks.ToString("F2") + " | " + l.FoeSwings.ToString("F2") + " | "
                                  + l.AllyMarks.ToString("F2") + " |");
            }

        Console.WriteLine();
        Console.WriteLine("## 表C. 混乱の下流（段A・回/戦）—— 予測4・予測5");
        Console.WriteLine();
        Console.WriteLine("**処刑も殉教も陣営を見ない**ので、混乱は敵側の札に直撃する（Q0-3）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 同士討ちの撃破 | うち処刑が積まれた | 混乱を庇った（敵の介入） |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach ((string name, Formation f) in Rigs())
            for (int st = 1; st < 5; st++)
            {
                Led l = Ledger(f, st, V1, Seeds);
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | "
                                  + l.Kills.ToString("F2") + " | " + l.ExecGain.ToString("F2") + " | "
                                  + l.Guards.ToString("F2") + " |");
            }
    }

    // =================================================================================
    // knob —— 段B（確率）と 段B'（回数制）を**同じ帯・同じ台・同じ seed で**
    // =================================================================================

    static (string Label, ShufflerRule R)[] Versions() => new (string, ShufflerRule)[]
    {
        ("V0 転倒", V0),
        ("段A 100%", V1),
        ("段B 50%", new ShufflerRule(true, ShuffleStagger.Confuse, ConfusePercent: 50)),
        ("段B 25%", new ShufflerRule(true, ShuffleStagger.Confuse, ConfusePercent: 25)),
        ("段B' 3回", new ShufflerRule(true, ShuffleStagger.Confuse, ConfuseUses: 3)),
        ("段B' 1回", new ShufflerRule(true, ShuffleStagger.Confuse, ConfuseUses: 1)),
    };

    static void Knobs()
    {
        Console.WriteLine("# 第147期 `derange knob` —— 段B（確率）と 段B'（回数制）");
        Console.WriteLine();
        Console.WriteLine("**片方だけ条件を変えない。** 同じ台・同じ seed・同じ波で、絞りの軸だけを振る。");
        Console.WriteLine("第146期は「確率は効かない」を**両陣営版の1点**で結論したが、");
        Console.WriteLine("この期は供給が細い（前へ出る敵は毎ターン高々1体）ので、測って決める。");
        Console.WriteLine();

        var versions = Versions();

        Console.WriteLine("## 表D. 第2〜5波平均（括弧は帰属 ＝ 版 − V0）");
        Console.WriteLine();
        Console.Write("| 台 |");
        foreach ((string lab, _) in versions) Console.Write(" " + lab + " |");
        Console.WriteLine();
        Console.Write("|---|");
        foreach (var _ in versions) Console.Write("--:|");
        Console.WriteLine();
        foreach ((string name, Formation f) in Rigs())
        {
            double baseAvg = Rates(f, V0).Skip(1).Average();
            Console.Write("| " + name + " |");
            foreach ((string lab, ShufflerRule r) in versions)
            {
                double v = Rates(f, r).Skip(1).Average();
                Console.Write(" " + v.ToString("F1")
                              + (lab.StartsWith("V0") ? "" : " (" + (v - baseAvg).ToString("+0.0;-0.0") + ")") + " |");
            }
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("## 表E. 供給（喧噪が混乱を立てた回数／戦・第2〜5波の平均）");
        Console.WriteLine();
        Console.Write("| 台 |");
        foreach ((string lab, _) in versions) Console.Write(" " + lab + " |");
        Console.WriteLine();
        Console.Write("|---|");
        foreach (var _ in versions) Console.Write("--:|");
        Console.WriteLine();
        foreach ((string name, Formation f) in Rigs())
        {
            Console.Write("| " + name + " |");
            foreach ((_, ShufflerRule r) in versions)
            {
                double c = 0;
                for (int st = 1; st < 5; st++) c += Ledger(f, st, r, Seeds).Confuses;
                Console.Write(" " + (c / 4).ToString("F2") + " |");
            }
            Console.WriteLine();
        }
    }

    // =================================================================================
    // compare —— 拒否権
    // =================================================================================

    static void CompareRows(string which)
    {
        string lab = which == "a" ? "段A 混乱 100%" : "段C 回数制 3回";
        Console.WriteLine("# 第147期 `derange compare " + which + "` —— 拒否権（`compare` 61 行 ＋ 交差帯 12 行）");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ 転倒版**（現行の盤面そのもの）。比べる版は **" + lab + "**。");
        Console.WriteLine();

        var rows = new List<(string Name, double[] A, double[] B)>();
        foreach ((string name, Formation f) in CompareBuilds())
            rows.Add((name, Rates(f, V0), Rates(f, which == "a" ? V1 : VC)));

        int moved = rows.Count(r => Enumerable.Range(0, 5).Any(i => Math.Abs(r.A[i] - r.B[i]) > 0.001));
        Console.WriteLine("## 表F. 特異性（バサを含まない行が ±0.0 か）");
        Console.WriteLine();
        Console.WriteLine("- 動いた行: **" + moved + " / " + rows.Count + "**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach ((string name, double[] a, double[] b) in rows)
            if (Enumerable.Range(0, 5).Any(i => Math.Abs(a[i] - b[i]) > 0.001))
                Console.WriteLine("| " + name + " | "
                                  + string.Join(" | ", Enumerable.Range(0, 5)
                                      .Select(i => (b[i] - a[i]).ToString("+0.0;-0.0;0.0"))) + " |");
        Console.WriteLine();

        int cross = 0, crossRows = 0;
        var crossNames = new List<string>();
        foreach ((string cname, Formation f) in CrossBuilds())
        {
            double[] a = Rates(f, V0), b = Rates(f, which == "a" ? V1 : VC);
            int n = Enumerable.Range(0, 5).Count(i => Math.Abs(a[i] - b[i]) > 0.001);
            cross += n;
            if (n > 0) { crossRows++; crossNames.Add(cname + "（" + n + " セル）"); }
        }
        Console.WriteLine("- 交差帯 12 行 / 60 セルで動いたセル: **" + cross + "**（" + crossRows + " 行）");
        Console.WriteLine("  - " + (crossNames.Count == 0 ? "—" : string.Join(" ／ ", crossNames)));
        Console.WriteLine();

        Console.WriteLine("## 表G. 拒否権1（主判定19行の第五波平均）");
        Console.WriteLine();
        var pri = rows.Where(r => Baseline.PrimaryRows.Contains(r.Name)).ToList();
        double pa5 = pri.Average(r => r.A[4]), pb5 = pri.Average(r => r.B[4]);
        double aAll = rows.Average(r => r.A.Skip(1).Average()), bAll = rows.Average(r => r.B.Skip(1).Average());
        Console.WriteLine("| | V0 | " + lab + " | Δ |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine("| 全61行・第2〜5波 | " + aAll.ToString("F1") + " | " + bAll.ToString("F1")
                          + " | " + (bAll - aAll).ToString("+0.0;-0.0") + " |");
        Console.WriteLine("| **主判定" + pri.Count + "行・第五波** | **" + pa5.ToString("F1") + "** | **"
                          + pb5.ToString("F1") + "** | **" + (pb5 - pa5).ToString("+0.0;-0.0") + "** |");
        Console.WriteLine();
        Console.WriteLine("**拒否権1（歯止め " + Baseline.PrimaryFifthFloor.ToString("F1") + "%）: "
                          + (pb5 < Baseline.PrimaryFifthFloor ? "発動" : "通る") + "**");
        Console.WriteLine();

        Console.WriteLine("## 表H. 拒否権3（いずれかの波で −10.0pt 以上）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | V0 | " + lab + " | Δ | 主判定 |");
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        int veto = 0;
        foreach ((string name, double[] a, double[] b) in rows)
            for (int st = 0; st < 5; st++)
                if (b[st] - a[st] <= -10.0)
                {
                    veto++;
                    Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + a[st].ToString("F1") + " | "
                                      + b[st].ToString("F1") + " | " + (b[st] - a[st]).ToString("+0.0;-0.0")
                                      + " | " + (Baseline.PrimaryRows.Contains(name) ? "**○**" : "—") + " |");
                }
        Console.WriteLine();
        Console.WriteLine("- **" + veto + " セル**が −10.0pt 以上");
        Console.WriteLine();

        Console.WriteLine("## 表I. 情報セル（第2〜5波で `0 < x < 100`）");
        Console.WriteLine();
        Console.WriteLine("| | V0 | " + lab + " |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine("| 全61行 | " + rows.Sum(r => r.A.Skip(1).Count(x => x > 0 && x < 100))
                          + " | " + rows.Sum(r => r.B.Skip(1).Count(x => x > 0 && x < 100)) + " |");
        Console.WriteLine("| 主判定" + pri.Count + "行 | " + pri.Sum(r => r.A.Skip(1).Count(x => x > 0 && x < 100))
                          + " | " + pri.Sum(r => r.B.Skip(1).Count(x => x > 0 && x < 100)) + " |");
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第147期 自己検査 —— `derange check`");
        Console.WriteLine();

        string bal = arg.Length > 0 ? arg : "docs/balance.md";
        Console.WriteLine("## (a) 必須1: `compare` 305 セルが `" + bal + "` と 0 件");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**比較先が見つからない。手で `compare` を回して突き合わせること。**");
        else
        {
            var want = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(bal))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                string[] c = line.Split('|', StringSplitOptions.TrimEntries);
                if (c.Length < 7) continue;
                var v = new List<double>();
                for (int i = 2; i <= 6; i++)
                    if (double.TryParse(c[i].TrimEnd('%'), out double d)) v.Add(d);
                if (v.Count == 5) want[c[1]] = v.ToArray();
            }
            int diff = 0, cells = 0;
            foreach ((string name, Formation f) in CompareBuilds())
            {
                if (!want.TryGetValue(name, out double[]? w)) { Console.WriteLine("- 行が引けない: " + name); continue; }
                double[] got = Rates(f, null);
                for (int i = 0; i < 5; i++) { cells++; if (Math.Abs(got[i] - w[i]) > 0.001) diff++; }
            }
            Console.WriteLine("- " + cells + " セル中 **" + diff + " 件**" + (diff == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (b) **採用版（段C）が既定と 305 セル 0 件**（第60期: 既定が動いた診断は検算の相手が移る）");
        Console.WriteLine();
        Console.WriteLine("- `ShufflerRule.Default` = `" + ShufflerRule.Default + "`");
        Console.WriteLine("- 段C の値 = `" + VC + "`");
        {
            int diff = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                double[] a = Rates(f, null), b = Rates(f, VC);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - b[i]) > 0.001) diff++;
            }
            Console.WriteLine("- 305 セル中 **" + diff + " 件**" + (diff == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (c) 転倒版では混乱が1件も立たない（**陰性対照**）");
        Console.WriteLine();
        {
            double m = 0, c = 0;
            foreach ((string _, Formation f) in Rigs())
                for (int st = 0; st < 5; st++)
                {
                    Led l = Ledger(f, st, V0, 50);
                    m += l.AllyMarks + l.FoeMarks; c += l.Confuses;
                }
            Console.WriteLine("- 立った **" + m.ToString("F0") + "** ／ 喧噪が立てた **" + c.ToString("F0") + "**"
                              + (m == 0 && c == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (d) §4-2: `立てた ≦ 前へ出た` ／ `振った ≦ 立った` ／ **味方側の混乱が 0**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 前へ出た | 立てた | 敵に立った | 敵が振った | 味方に立った | 判定 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|:-:|");
        bool ok = true;
        foreach ((string name, Formation f) in Rigs())
            for (int st = 0; st < 5; st++)
            {
                Led l = Ledger(f, st, V1, 50);
                bool o = l.Confuses <= l.Advanced + 1e-9
                         && l.FoeSwings <= l.FoeMarks + 1e-9
                         && l.AllyMarks <= 1e-9;
                if (!o) ok = false;
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + l.Advanced.ToString("F2")
                                  + " | " + l.Confuses.ToString("F2") + " | " + l.FoeMarks.ToString("F2")
                                  + " | " + l.FoeSwings.ToString("F2") + " | " + l.AllyMarks.ToString("F2")
                                  + " | " + (o ? "○" : "**×**") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("- 全 20 セル" + (ok ? " ○" : " **×**"));
        Console.WriteLine();
        Console.WriteLine("**味方側の混乱が 0 なのは、この期はバサの中だけで立てるため**"
                          + "——第146期の波ルール版（`SwapSlots` の通知）とはここが違う。");

        Console.WriteLine();
        Console.WriteLine("## (e) 在庫（`ConfuseUses`）が上限どおり効く");
        Console.WriteLine();
        Console.WriteLine("| 台 | 上限 | 立てた/戦（第2〜5波平均） | 判定 |");
        Console.WriteLine("|---|--:|--:|:-:|");
        bool okUses = true;
        foreach ((string name, Formation f) in Rigs())
            foreach (int n in new[] { 1, 3 })
            {
                var r = new ShufflerRule(true, ShuffleStagger.Confuse, ConfuseUses: n);
                double c = 0;
                for (int st = 1; st < 5; st++) c += Ledger(f, st, r, 50).Confuses;
                c /= 4;
                bool o = c <= n + 1e-9;
                if (!o) okUses = false;
                Console.WriteLine("| " + name + " | " + n + " | " + c.ToString("F2") + " | " + (o ? "○" : "**×**") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("- " + (okUses ? "○" : "**×**") + "（上限は**保持者1体・1戦あたり**なので、平均は上限以下になる）");

        Console.WriteLine();
        Console.WriteLine("## (f) 必須4: `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        string? eng = FindSource("BattleEngine.cs");
        string? tr = FindSource("Traits.cs");
        if (eng is null || tr is null) Console.WriteLine("**ソースが引けない。止める**（第117期）。");
        else
        {
            Console.WriteLine("- `BattleEngine.cs` の `PickOne(` は **"
                              + File.ReadAllLines(eng).Count(l => l.Contains("PickOne(")) + "** 行");
            Console.WriteLine("- `Traits.cs` の `PickOne(` は **"
                              + File.ReadAllLines(tr).Count(l => l.Contains("PickOne(")) + "** 行");
            Console.WriteLine("- 混乱が引く乱数は `ConfusePercent < 100` のときの `Roll(100)` 1本だけ: "
                              + (File.ReadAllLines(tr).Any(l => l.Contains("c.Shuffler.ConfusePercent < 100"))
                                 ? "**○**" : "**×**"));
        }
    }

    // =================================================================================
    // 器具
    // =================================================================================

    static double[] Rates(Formation f, ShufflerRule? r, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, shuffler: r).PlayerWon)
                    wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    readonly record struct Led(double Advanced, double Confuses, double AllyMarks, double FoeMarks,
                               double FoeSwings, double Kills, double ExecGain, double Guards);

    /// <summary>
    /// 混乱の帳簿を陣営別に引く（回/戦）。
    /// <b><c>TallyByUnit</c> は駒 <c>Id</c> のキー</b>なので、編成に載っている <c>Id</c> の集合で味方を判別する
    /// （台のどれも敵と同じ <c>Id</c> を持たないことが前提。素体の <c>Id</c> に `pa` / `pb` を使ってある）。
    /// </summary>
    static Led Ledger(Formation f, int st, ShufflerRule r, int seeds)
    {
        var own = new HashSet<string>(f.Occupied().Select(o => o.Def.Id));
        double adv = 0, con = 0, am = 0, fm = 0, fsw = 0, kill = 0, exe = 0, grd = 0;
        for (int seed = 0; seed < seeds; seed++)
        {
            BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                verbose: false, shuffler: r);
            foreach (var kv in res.TallyByUnit)
            {
                UnitTally t = kv.Value;
                adv += t.ShuffleAdvanced; con += t.ShuffleConfuses;
                if (own.Contains(kv.Key)) am += t.ConfusedMarks;
                else
                {
                    fm += t.ConfusedMarks; fsw += t.ConfusedSwings;
                    kill += t.ConfusedKills; exe += t.ConfusedExecGain; grd += t.ConfusedGuards;
                }
            }
        }
        return new Led(adv / seeds, con / seeds, am / seeds, fm / seeds, fsw / seeds,
                       kill / seeds, exe / seeds, grd / seeds);
    }

    /// <summary>リポジトリ内のソースを探す。<b>引けなかったら呼び出し側で止めること</b>（第117期）。</summary>
    static string? FindSource(string leaf)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            string p = Path.Combine(dir.FullName, "BattleCore", leaf);
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
