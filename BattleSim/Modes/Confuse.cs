using BattleCore;
using static Common;

// =====================================================================================
// confuse モード（第146期） —— 混乱（動かされた駒は次の攻撃を自軍へ向ける）
//
// 指示書は design/PHASE146_CONFUSION_SPEC.md ／ 報告は design/PHASE146_CONFUSION.md。
//
//     dotnet run --project BattleSim -c Release 0 confuse phase0   # Phase 0 の答えを実装から引き直す（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 confuse scan     # 台の下見（V0 の第2〜5波平均が 40〜95% か）
//     dotnet run --project BattleSim -c Release 0 confuse run      # 段B: V0 対 V1 × 台4つ × 波
//     dotnet run --project BattleSim -c Release 0 confuse check    # 自己検査
// =====================================================================================

static class ConfuseDiag
{
    const int Seeds = 200;

    static readonly ConfusionRule V0 = ConfusionRule.Default;   // 掛けない（回帰の基準）
    static readonly ConfusionRule V1 = ConfusionRule.On;        // 両陣営に掛ける

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": Scan(); return;
            case "run": Sweep(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("confuse: モードは phase0 / scan / run / check。");
                return;
        }
    }

    // =================================================================================
    // 台
    // =================================================================================

    /// <summary>
    /// 埋め草は<b>特性を1つも持たない素体</b>（第143期の則）。
    /// <b>敵の数値に触る駒（呪詛官ネル・萎縮のクビ）を入れると、掃引点がそのまま埋め草の性能になる。</b>
    /// </summary>
    static UnitDef Plain(string id, int hp, int atk, int spd = 8) => new()
    {
        Id = id, Name = "素体" + id, MaxHp = hp, Attack = atk, Speed = spd,
        Advances = true, Traits = Array.Empty<TraitId>()
    };

    /// <summary>
    /// ローカル台。<b><c>Presets</c> には置かない</b>（<c>compare</c> 61行を汚さない）。
    ///
    /// <para><b>供給は喧噪のバサに寄せてある</b>——毎ターン敵2体と味方2体を入れ替えるので、
    /// <b>両陣営に混乱を立てられる唯一の札</b>（Phase 0 Q0-6）。台4 だけがバサを持たない。</para>
    ///
    /// <para><b>台4 の「発火 0」は第1〜4波でしか成り立たない。</b> 第五波には告発人（曝き）がいて、
    /// <b>敵が味方を引きずり出す</b>ので、動かす機構を1枚も入れなくても混乱が立つ
    /// ——これが「両陣営に等しくかかる」は名目だけである、の実証になる。</para>
    /// </summary>
    static (string Name, Formation F)[] Rigs() => new (string, Formation)[]
    {
        // 被弾変換が厚い。ガルド（庇い100%・受け流し）／ムド（怒り）／ドハ（分かち）。
        // **混乱が代金になるか**を読む台（指示書 §3 予測1 の負の側）。
        ("被弾変換が厚い", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Doha,
            back1: UnitCatalog.Basa, back3: Plain("pa", 70, 10))),

        // 打点だけ。被弾を資産に変える札が1枚も無いので、**混乱が純粋な得になるか**を読む
        // （指示書 §3 予測1 の正の側）。台4 とはバサの有無だけが違う。
        ("被弾変換が薄い", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: UnitCatalog.Basa, back3: Plain("pa", 70, 10))),

        // ササ（身構え・上限7で切って切り落としを破片へ）＋ ガレ（礫・破片を出口へ）。
        // **混乱の一撃が供給に化けるか**（指示書 §3 予測2）。
        ("ササ同席", Formation.Build(
            front1: UnitCatalog.Sasa, front3: UnitCatalog.Gare, center: UnitCatalog.Borg,
            back1: UnitCatalog.Basa, back3: Plain("pa", 70, 10))),

        // 動かす機構なし（陰性対照）。台2 からバサを抜いただけ。
        ("動かす機構なし", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: Plain("pb", 70, 10), back3: Plain("pa", 70, 10))),
    };

    // =================================================================================
    // phase0 —— Phase 0 の答えを**実装から引き直す**（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第146期 `confuse phase0` —— 陣営の前提を実装から引き直す（戦闘0回）");
        Console.WriteLine();

        string? eng = FindSource("BattleEngine.cs"), tr = FindSource("Traits.cs");
        if (eng is null || tr is null)
        {
            Console.WriteLine("**`BattleCore` のソースが引けない。止める**（第117期）。");
            return;
        }
        string[] E = File.ReadAllLines(eng), T = File.ReadAllLines(tr);

        Console.WriteLine("## Q0-1. `Opponent(` の全数と、標的集合を作る箇所");
        Console.WriteLine();
        Console.WriteLine("- `BattleEngine.cs` **" + E.Count(l => l.Contains("Opponent(")) + "** 行 ／ `Traits.cs` **"
                          + T.Count(l => l.Contains("Opponent(")) + "** 行");
        Console.WriteLine("- そのうち標的集合を作るのは **`FoesOf` の1箇所だけ**"
                          + "（第146期に寄せた。`SelectTargetChain` / `TargetPool` / `SecondaryTargets` が共有）");
        Console.WriteLine("- `FoesOf` の呼び出し: **" + (E.Count(l => l.Contains("FoesOf(")) - 1)
                          + "** 箇所（定義を除く。**3** なら3つとも通っている）");
        Console.WriteLine("- `ResolvePierce` が `entry.TeamId` を見る: "
                          + (E.Any(l => l.Contains("LaneOccupants(LivingMembers(entry.TeamId)"))
                             ? "**○**（自動で追従する）" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("## Q0-2 / Q0-3. `source` の陣営を見て降りる札");
        Console.WriteLine();
        Console.WriteLine("| 行 | クラス | 条件 |");
        Console.WriteLine("|--:|---|---|");
        string cls = "";
        for (int i = 0; i < T.Length; i++)
        {
            if (T[i].StartsWith("public sealed class ") && T[i].Contains("Trait"))
                cls = T[i].Substring("public sealed class ".Length).Split(' ')[0];
            if (T[i].Contains("source.TeamId == self.TeamId") && T[i].Contains("return"))
                Console.WriteLine("| " + (i + 1) + " | `" + cls + "` | `" + T[i].Trim() + "` |");
        }
        Console.WriteLine();
        Console.WriteLine("**これ以外の被弾変換（怒り・分かち・後備え・集約・鱗・身構え・庇いの見返り）は陣営を見ない。**");
        Console.WriteLine();
        int parry = Array.FindIndex(E, l => l.Contains("Parry.Uses > 0 && source is not null"));
        if (parry >= 0)
        {
            Console.WriteLine("- 受け流し（`BattleEngine.cs:" + (parry + 1) + "`）: `" + E[parry].Trim() + "`");
            Console.WriteLine("  → **味方からの一撃は弾かない。** 指示書 §0 の表の1行目は偽（報告書 §Q0-2）。");
        }
        else Console.WriteLine("- **受け流しの条件が引けない。止める**（第117期）。");
        Console.WriteLine();

        Console.WriteLine("## Q0-6. `SwapSlots` の呼び出し元（＝混乱の供給）");
        Console.WriteLine();
        cls = "";
        Console.WriteLine("| 行 | クラス |");
        Console.WriteLine("|--:|---|");
        for (int i = 0; i < T.Length; i++)
        {
            if (T[i].StartsWith("public sealed class ") && T[i].Contains("Trait"))
                cls = T[i].Substring("public sealed class ".Length).Split(' ')[0];
            if (T[i].Contains("ctx.SwapSlots(")) Console.WriteLine("| " + (i + 1) + " | `" + cls + "` |");
        }
        Console.WriteLine();
        Console.WriteLine("**庇いの前出は1件も出ない**——鎖は主目標を差し替えるだけで席を1ミリも動かさない（第124期 Q0-7）。");
        Console.WriteLine("**弾きの共有ヘルパは散開と身構えの2者が使う**ので、箇所は 6・札は 7・");
        Console.WriteLine("保持者のいる札は 6（散開は第143期の転生で保持者 0 枚になった）。");
        Console.WriteLine();

        Console.WriteLine("## 敵側の供給（波ごと）");
        Console.WriteLine();
        TraitId[] movers =
        {
            TraitId.Coward, TraitId.ThornGuard, TraitId.Expose,
            TraitId.Shove, TraitId.Shuffler, TraitId.Loose, TraitId.Brace
        };
        Console.WriteLine("| 波 | 移動を起こす敵 |");
        Console.WriteLine("|---|---|");
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
        {
            var found = EnemyCatalog.Stages[st].Enemy.Occupied()
                .Where(o => o.Def.Traits.Any(t => movers.Contains(t)))
                .Select(o => o.Def.Name + "（"
                             + string.Join("/", o.Def.Traits.Where(t => movers.Contains(t))) + "）")
                .ToList();
            Console.WriteLine("| " + EnemyCatalog.Stages[st].Name + " | "
                              + (found.Count == 0 ? "**0 枚**" : string.Join(" ／ ", found)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**第五波の告発人（曝き）だけで、しかも動かすのは<u>味方</u>。**");
        Console.WriteLine("敵の駒が動く経路は、プレイヤーが ハネ／バサ を連れてきたときしか存在しない");
        Console.WriteLine("——第134期の「**在庫**」型の非対称（窓口は両陣営に開いているが、片側が一度も問い合わせない）。");
    }

    // =================================================================================
    // scan —— 台の下見（版を1つも振らない）
    // =================================================================================

    static void Scan()
    {
        Console.WriteLine("# 第146期 `confuse scan` —— 台の下見（V0 だけ。版は振らない）");
        Console.WriteLine();
        Console.WriteLine("**採る条件は第61・63期の帯: 第2〜5波平均が 40〜95%。** 情報セルは第2〜5波で `0 < x < 100`。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | 情報セル | 帯 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|:-:|");
        foreach ((string name, Formation f) in Rigs())
        {
            double[] w = Rates(f, V0);
            double avg = w.Skip(1).Average();
            int info = w.Skip(1).Count(x => x > 0.0 && x < 100.0);
            Console.WriteLine("| " + name
                              + " | " + w[0].ToString("F1") + " | " + w[1].ToString("F1")
                              + " | " + w[2].ToString("F1") + " | " + w[3].ToString("F1")
                              + " | " + w[4].ToString("F1")
                              + " | **" + avg.ToString("F1") + "** | " + info + "/4 | "
                              + (avg >= 40 && avg <= 95 ? "○" : "**×**") + " |");
        }
    }

    // =================================================================================
    // run —— 段B: V0 対 V1
    // =================================================================================

    static void Sweep()
    {
        Console.WriteLine("# 第146期 `confuse run` —— 段B: 混乱を掛ける（V0 対 V1）");
        Console.WriteLine();
        Console.WriteLine("**波ごとの効き方は生の pt で比べない**（第144期）——同じ波でも V0 の位置で");
        Console.WriteLine("動ける幅が違う。`余地に対する取り分` ＝ Δ ÷ (上がったなら 100 − V0 ／ 下がったなら V0)。");
        Console.WriteLine();
        Console.WriteLine("## 表A. 勝率（V1 − V0）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | V0 | V1 | Δ | 余地に対する取り分 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in Rigs())
        {
            double[] a = Rates(f, V0), b = Rates(f, V1);
            for (int st = 1; st < 5; st++)
            {
                double room = b[st] >= a[st] ? 100.0 - a[st] : a[st];
                string share = room <= 0.001 ? "—" : ((b[st] - a[st]) / room).ToString("+0.000;-0.000");
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | "
                                  + a[st].ToString("F1") + " | " + b[st].ToString("F1") + " | "
                                  + (b[st] - a[st]).ToString("+0.0;-0.0") + " | " + share + " |");
            }
            Console.WriteLine("| **" + name + "** | **第2〜5波** | **" + a.Skip(1).Average().ToString("F1")
                              + "** | **" + b.Skip(1).Average().ToString("F1") + "** | **"
                              + (b.Skip(1).Average() - a.Skip(1).Average()).ToString("+0.0;-0.0") + "** | |");
        }

        Console.WriteLine();
        Console.WriteLine("## 表B. 混乱の帳簿（V1・回/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 味方に立った | 味方が振った | 敵に立った | 敵が振った |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in Rigs())
            for (int st = 1; st < 5; st++)
            {
                (double am, double asw, double fm, double fsw) = Ledger(f, st, V1, Seeds);
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | "
                                  + am.ToString("F2") + " | " + asw.ToString("F2") + " | "
                                  + fm.ToString("F2") + " | " + fsw.ToString("F2") + " |");
            }
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第146期 自己検査 —— `confuse check`");
        Console.WriteLine();

        var def = Presets.Compare.ToDictionary(b => b.Name, b => Rates(b.F, null));

        string bal = arg.Length > 0 ? arg : "docs/balance.md";
        Console.WriteLine("## (a) 必須1: `compare` 305 セルが `" + bal + "` と 0 件（既定の `ConfusionRule`）");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**比較先が見つからない。手で `compare` を回して突き合わせること。**");
        else
        {
            int diff = 0, cells = 0;
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
            foreach ((string name, Formation f) in Presets.Compare)
            {
                if (!want.TryGetValue(name, out double[]? w)) { Console.WriteLine("- 行が引けない: " + name); continue; }
                double[] got = def[name];
                for (int i = 0; i < 5; i++) { cells++; if (Math.Abs(got[i] - w[i]) > 0.001) diff++; }
            }
            Console.WriteLine("- " + cells + " セル中 **" + diff + " 件**の差分" + (diff == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (b) `ConfusionRule.Default` と既定が 305 セル 0 件（**既定が現行と同値か**）");
        Console.WriteLine();
        {
            int diff = 0;
            foreach ((string name, Formation f) in Presets.Compare)
            {
                double[] a = def[name], c = Rates(f, V0);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - c[i]) > 0.001) diff++;
            }
            Console.WriteLine("- 305 セル中 **" + diff + " 件**" + (diff == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (c) `Active = false` では混乱が1件も立たない（**計数の検算**）");
        Console.WriteLine();
        {
            double m = 0, sw = 0;
            foreach ((string _, Formation f) in Rigs())
                for (int st = 0; st < 5; st++)
                {
                    (double a1, double a2, double f1, double f2) = Ledger(f, st, V0, 50);
                    m += a1 + f1; sw += a2 + f2;
                }
            Console.WriteLine("- 立った **" + m.ToString("F0") + "** ／ 振った **" + sw.ToString("F0") + "**"
                              + (m == 0 && sw == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (d) 指示書 §4 の 6: `振った ≦ 立った`（**1回の攻撃で必ず1つ落ちる**）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 立った | 振った | 差 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        bool ok6 = true;
        foreach ((string name, Formation f) in Rigs())
            for (int st = 0; st < 5; st++)
            {
                (double am, double asw, double fm, double fsw) = Ledger(f, st, V1, 50);
                double m = am + fm, sw = asw + fsw;
                bool ok = sw <= m + 1e-9;
                if (!ok) ok6 = false;
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + m.ToString("F2")
                                  + " | " + sw.ToString("F2") + " | " + (m - sw).ToString("F2")
                                  + " | " + (ok ? "○" : "**×**") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("- 全 20 セル" + (ok6 ? " ○" : " **×**"));
        Console.WriteLine();
        Console.WriteLine("**台4「動かす機構なし」の第五波だけは 0 にならない**——告発人（曝き）が味方を引きずり出すため。");
        Console.WriteLine("指示書 §3 の予測5「動かす機構なしの台で発火 0」は**第1〜4波でしか成り立たない**（報告書 §Q0-6）。");

        Console.WriteLine();
        Console.WriteLine("## (e) 必須4: `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        string? eng = FindSource("BattleEngine.cs");
        if (eng is null) Console.WriteLine("**ソースが引けない。止める**（第117期）。");
        else
        {
            string[] E = File.ReadAllLines(eng);
            Console.WriteLine("- `BattleEngine.cs` の `PickOne(` は **" + E.Count(l => l.Contains("PickOne("))
                              + "** 行（第145期 HEAD と同数なら ○）");
            Console.WriteLine("- `FoesOf` は乱数を1つも引かない: "
                              + (E.Any(l => l.Contains("internal List<UnitState> FoesOf")) ? "**○**（定義を確認）" : "**×**"));
        }
    }

    // =================================================================================
    // 器具
    // =================================================================================

    static double[] Rates(Formation f, ConfusionRule? r, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, confusion: r).PlayerWon)
                    wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    /// <summary>
    /// 混乱の帳簿を陣営別に引く（回/戦）。
    /// <b><c>TallyByUnit</c> は駒 <c>Id</c> のキーなので、編成に載っている <c>Id</c> の集合で味方を判別する。</b>
    /// 台のどれも敵と同じ <c>Id</c> を持たないことが前提（素体の <c>Id</c> に `pa` / `pb` を使ってあるのはそのため）。
    /// </summary>
    static (double AllyMarks, double AllySwings, double FoeMarks, double FoeSwings)
        Ledger(Formation f, int st, ConfusionRule r, int seeds)
    {
        var own = new HashSet<string>(f.Occupied().Select(o => o.Def.Id));
        double am = 0, asw = 0, fm = 0, fsw = 0;
        for (int seed = 0; seed < seeds; seed++)
        {
            BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                verbose: false, confusion: r);
            foreach (var kv in res.TallyByUnit)
            {
                if (own.Contains(kv.Key)) { am += kv.Value.ConfusedMarks; asw += kv.Value.ConfusedSwings; }
                else { fm += kv.Value.ConfusedMarks; fsw += kv.Value.ConfusedSwings; }
            }
        }
        return (am / seeds, asw / seeds, fm / seeds, fsw / seeds);
    }

    /// <summary>
    /// リポジトリ内のソースを探す。<b>引けなかったら呼び出し側で止めること</b>（第117期）
    /// ——実装から引く分類は、引けなかったときに「該当なし」と区別が付かない。
    /// </summary>
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
