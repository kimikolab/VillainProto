using BattleCore;
using static Common;

// =====================================================================================
// kata モード（第188期） —— ナタの枠に新駒「触媒のカタ」（起爆）
//
// 指示書は design/PHASE188_KATA_SPEC.md ／ 報告は design/PHASE188_KATA.md。
//
// **線は置かない**（指示書 §0）。回帰確認（カタを含まない行が動かないこと）と、
// 診断台3つのカタあり／なし・帳簿だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 kata phase0  # Q0-1 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 kata run     # 診断台3つ × カタあり／素体 ＋ 帳簿 ＋ 残存・全滅勝ち
//     dotnet run --project BattleSim -c Release 0 kata rows    # 表E（事後）compare の毒・燃焼の 27 行で1枚をカタに替える
//     dotnet run --project BattleSim -c Release 0 kata check [balance.md]  # 自己検査
//
// **`Presets` は1行も触らない**（ナタの1行はそのまま残る＝盤面は1ビットも動かない）ので、
// カタを載せた台は**この診断のローカル**で組む（第179期のススと同じ扱い）。
// =====================================================================================

static class KataDiag
{
    const int Seeds = 200;

    /// <summary>Q0-1 で名指しする軸の駒（指示書の並び）。</summary>
    static readonly UnitDef[] AxisUnits =
    {
        UnitCatalog.Guza, UnitCatalog.Sid, UnitCatalog.Mio, UnitCatalog.Rau, UnitCatalog.Vio,
        UnitCatalog.Beni, UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo, UnitCatalog.Zoto,
        UnitCatalog.Rica, UnitCatalog.Susu,
    };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunBenches(); return;
            case "check": Check(arg); return;
            case "rows": Rows(); return;
            default:
                Console.WriteLine("kata: モードは phase0 / run / rows / check。");
                return;
        }
    }

    // =================================================================================
    // Phase 0 —— Q0-1 の数え物（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第188期 `kata phase0` —— Q0-1（戦闘0回）");
        Console.WriteLine();
        var rows = CompareBuilds();
        Console.WriteLine("`compare` " + rows.Length + " 行。");
        Console.WriteLine();

        Console.WriteLine("## ナタを含む行");
        Console.WriteLine();
        foreach (var (name, f) in rows)
            if (f.Occupied().Any(o => o.Def.Id == "nata")) Console.WriteLine("- " + name);
        Console.WriteLine();

        Console.WriteLine("## 軸の駒を含む行（駒ごと）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 行数 | 行 |");
        Console.WriteLine("|---|--:|---|");
        foreach (UnitDef d in AxisUnits)
        {
            var hit = rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == d.Id)).Select(r => r.Name).ToList();
            Console.WriteLine("| " + d.Name + " | " + hit.Count + " | " + string.Join(" ／ ", hit) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 毒の書き手と燃焼の書き手が同席する行");
        Console.WriteLine();
        string[] poison = { "guza", "sid", "mio", "rau", "vio" };   // 毒を書く（ミオは増幅・ヴィオは吐き戻し）
        string[] burn = { "borg", "zoto" };                          // 燃焼を書く（ボルグの火の粉・ゾトの破裂）
        foreach (var (name, f) in rows)
        {
            var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
            if (ids.Overlaps(poison) && ids.Overlaps(burn)) Console.WriteLine("- " + name);
        }
        Console.WriteLine();
    }

    // =================================================================================
    // 診断台（Phase 0 で測る前に固定した3つ・`Presets` には足さない）
    // =================================================================================

    static readonly (string Name, Formation F)[] Benches =
    {
        ("毒の台", Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Vio, center: UnitCatalog.Guza,
                                  back1: UnitCatalog.Mio, back3: UnitCatalog.KataOld)),
        ("毒×燃焼の台", Formation.Build(front1: UnitCatalog.Borg, front3: UnitCatalog.Hota, center: UnitCatalog.Guza,
                                      back1: UnitCatalog.Mio, back3: UnitCatalog.KataOld)),
        ("リィカの台", Formation.Build(front1: UnitCatalog.Mug, front3: UnitCatalog.Zoto, center: UnitCatalog.Rica,
                                     back1: UnitCatalog.Guza, back3: UnitCatalog.KataOld)),
    };

    /// <summary>逆流（代金）の札を外したカタ＝`yP`。**Id は同じ**（帳簿を同じキーで引く）。この診断のローカルだけ。</summary>
    static readonly UnitDef KataNoBackfire = new()
    {
        Id = UnitCatalog.KataOld.Id, Name = UnitCatalog.KataOld.Name, MaxHp = UnitCatalog.KataOld.MaxHp,
        Attack = UnitCatalog.KataOld.Attack, Speed = UnitCatalog.KataOld.Speed, Advances = UnitCatalog.KataOld.Advances,
        Actions = UnitCatalog.KataOld.Actions, Traits = new[] { TraitId.Catalyst },
    };

    static IEnumerable<(string Tag, Formation F)> Versions(Formation f) => new[]
    {
        ("素体", SwapDef(f, UnitCatalog.KataOld, Plain(UnitCatalog.KataOld))),
        ("**カタ**", f),
        ("逆流なし", SwapDef(f, UnitCatalog.KataOld, KataNoBackfire)),
    };

    static void RunBenches()
    {
        Console.WriteLine("# 第188期 `kata run` —— 診断台3つ × カタあり／素体（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**素体** ＝ カタと同じ数値・特性なし（攻3 で毎手番殴る）。"
                          + "**逆流なし** ＝ 代金の札（`Backfire`）を外した版（`yP`・起爆は敵にだけ効く）。"
                          + "seed 0..199。平均と帳簿は第2〜5波（規約 (G10)）。");
        Console.WriteLine();

        Console.WriteLine("## 表A. 勝率");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | Δ（素体比） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in Benches)
        {
            double basis = 0; bool first = true;
            foreach (var (tag, g) in Versions(f))
            {
                double[] w = Rates(g);
                if (first) basis = Mean25(w);
                Console.WriteLine("| " + (first ? name : "") + " | " + tag + " | " + Cells5(w) + " | " + Mean25(w).ToString("F1")
                                  + " | " + (first ? "—" : (Mean25(w) - basis).ToString("+0.0;-0.0;0.0")) + " |");
                first = false;
            }
        }
        Console.WriteLine();
        Console.WriteLine("**代金 ＝ カタ − 逆流なし**（負なら「味方にも弾けること」で損をしている）。");
        Console.WriteLine();

        Console.WriteLine("## 表B. 起爆の帳簿（1戦あたり・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("名目は倍を含む量、実額は実際に削れた HP（過剰分・破片・軛を除く）。"
                          + "**倍の上乗せ**は名目のうち両方持ちの倍で足した分。**軛超過**は軛が効いている間に上限 25 を超えた毒の段の数。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 起爆 | 空振り | 両方持ち（延べ） | 敵・毒 名目 | 敵・燃焼 名目 | 倍の上乗せ | **敵 実額** | 味方 名目 | **味方 実額** | 倒した敵 | 倒れた味方 | 軛超過 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in Benches)
            foreach (var (tag, g) in Versions(f).Skip(1))
            {
                Led l = LedgerOf(g, 1, 4);
                Console.WriteLine("| " + name + " | " + tag + " | " + l.Fires.ToString("F2") + " | " + l.Dry.ToString("F2") + " | "
                                  + l.Dual.ToString("F2") + " | " + l.Poison.ToString("F1") + " | " + l.Burn.ToString("F1") + " | "
                                  + l.Extra.ToString("F1") + " | **" + l.FoeDealt.ToString("F1") + "** | "
                                  + l.AllyNom.ToString("F1") + " | **" + l.AllyDealt.ToString("F1") + "** | "
                                  + l.FoeKills.ToString("F2") + " | " + l.AllyKills.ToString("F2") + " | " + l.YokeCut.ToString("F2") + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表C. 波ごとの起爆（カタ・敵 実額 ／ 倍の上乗せ ／ 味方 実額 ／ 軛超過）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 第2波 | 第3波 | 第4波（軛） | 第5波 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (name, f) in Benches)
        {
            var cells = Enumerable.Range(1, 4).Select(st =>
            {
                Led l = LedgerOf(f, st, st);
                return l.FoeDealt.ToString("F1") + " ／ " + l.Extra.ToString("F1") + " ／ " + l.AllyDealt.ToString("F1")
                       + " ／ " + l.YokeCut.ToString("F2");
            });
            Console.WriteLine("| " + name + " | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表D. 味方の損と墓守の層・勝ち方（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`味方の死` は出撃5枚の `Deaths` の合計／戦（胞子は数えない）。"
                          + "`層の最大` は墓守リィカの層の最大値の平均（リィカのいない台は —）。"
                          + "`残存` は**勝った試行だけ**の生存数、`全滅勝ち` は勝った試行のうち生存1体の割合"
                          + "（`chain` と同じ定義。ただし分母は第2〜5波）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 味方の死 | 層の最大 | 決着T（勝ち） | 残存（勝ち） | 全滅勝ち |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in Benches)
            foreach (var (tag, g) in Versions(f))
            {
                Qual q = QualOf(g);
                Console.WriteLine("| " + name + " | " + tag + " | " + q.AllyDeaths.ToString("F2") + " | "
                                  + (q.HasRica ? q.NecroPeak.ToString("F2") : "—") + " | " + q.WinT.ToString("F2") + " | "
                                  + q.Survivors.ToString("F2") + "/5 | " + q.Wipe.ToString("F0") + "% |");
            }
        Console.WriteLine();
    }

    // =================================================================================
    // 表E（**事後に足した**）—— `compare` の毒・燃焼の行で1枚をカタに替える
    // =================================================================================

    /// <summary>
    /// 診断台3つは素体が床（0〜0.4%）に張り付いた（R146）ので、<b>床でない台</b>として足した。
    /// <b>予測の判定には使わない</b>（予測は診断台で宣言した）。
    /// 行は「毒か燃焼の書き手（グザ・スィド・ミオ・ラウ・ヴィオ・ボルグ・ゾト）を含む `compare` 行」、
    /// 替える駒は「その行で<b>書き手以外</b>のうち素体差し替えの帰属が最も低い1枚」（第179期 `susu run` の規則から書き手を除いた）。
    /// </summary>
    static void Rows()
    {
        Console.WriteLine("# 第188期 `kata rows` —— `compare` の毒・燃焼の行で1枚をカタに替える（**事後の追加・予測の判定に使わない**）");
        Console.WriteLine();
        Console.WriteLine("行 ＝ 毒か燃焼の書き手（グザ・スィド・ミオ・ラウ・ヴィオ・ボルグ・ゾト）を含む `compare` 行。"
                          + "替える駒 ＝ その行で**書き手以外のうち**素体差し替えの帰属が最も低い1枚（第179期 `susu run` の規則から書き手を除いた。"
                          + "除かないと 27 行中 14 行で書き手そのものが抜けて起爆の燃料が 0 になった）。"
                          + "勝率は第2〜5波の平均・seed 0..199。");
        Console.WriteLine();
        Console.WriteLine("**Δ元** ＝ カタ − 元の行（駒1枚を替えた損得の全部）／ **Δ素体** ＝ カタ − 同じ席の素体のカタ（起爆の機構だけの値）"
                          + "／ **代金** ＝ カタ − 逆流なし。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 替えた駒（帰属） | 元 | 素体 | カタ | Δ元 | **Δ素体** | 逆流なし | 代金 | 起爆/戦 | 敵 実額 | 倍の上乗せ | 味方 実額 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        string[] writers = { "guza", "sid", "mio", "rau", "vio", "borg", "zoto" };
        var deltas = new List<double>(); var costs = new List<double>(); var mech = new List<double>();
        foreach (var (name, f) in CompareBuilds())
        {
            if (!f.Occupied().Any(o => writers.Contains(o.Def.Id))) continue;
            double baseW = Mean25(Rates(f));
            var attr = f.Occupied().Select(o => (o.Def, A: baseW - Mean25(Rates(SwapDef(f, o.Def, Plain(o.Def)))))).ToList();
            // **書き手は替える候補から外す**——外さないと 27 行中 14 行で書き手そのもの（ボルグ・ゾト・スィド）が抜けて
            // 起爆の燃料が 0 になった（初回の実行・R016）。
            var low = attr.Where(x => !writers.Contains(x.Def.Id)).OrderBy(x => x.A).First();
            Formation g = SwapDef(f, low.Def, UnitCatalog.KataOld);
            Formation y = SwapDef(f, low.Def, KataNoBackfire);
            double k = Mean25(Rates(g)), yp = Mean25(Rates(y));
            double pl = Mean25(Rates(SwapDef(f, low.Def, Plain(UnitCatalog.KataOld))));
            mech.Add(k - pl);
            Led l = LedgerOf(g, 1, 4);
            deltas.Add(k - baseW); costs.Add(k - yp);
            Console.WriteLine("| " + name + " | " + low.Def.Name + "（" + low.A.ToString("+0.0;-0.0;0.0") + "） | "
                              + baseW.ToString("F1") + " | " + pl.ToString("F1") + " | " + k.ToString("F1") + " | " + (k - baseW).ToString("+0.0;-0.0;0.0")
                              + " | **" + (k - pl).ToString("+0.0;-0.0;0.0") + "** | "
                              + yp.ToString("F1") + " | " + (k - yp).ToString("+0.0;-0.0;0.0") + " | " + l.Fires.ToString("F2") + " | "
                              + l.FoeDealt.ToString("F1") + " | " + l.Extra.ToString("F1") + " | " + l.AllyDealt.ToString("F1") + " |");
        }
        Console.WriteLine();
        if (deltas.Count > 0)
            Console.WriteLine("- " + deltas.Count + " 行 ／ Δ元 の平均 **" + deltas.Average().ToString("+0.0;-0.0;0.0") + "**"
                              + "（正 " + deltas.Count(d => d > 0.05) + " ／ 負 " + deltas.Count(d => d < -0.05) + "）"
                              + " ／ Δ素体 の平均 **" + mech.Average().ToString("+0.0;-0.0;0.0") + "**"
                              + "（正 " + mech.Count(d => d > 0.05) + " ／ 負 " + mech.Count(d => d < -0.05) + "）"
                              + " ／ 代金（カタ − 逆流なし）の平均 **" + costs.Average().ToString("+0.0;-0.0;0.0") + "**");
        Console.WriteLine();
    }

    // =================================================================================
    // 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第188期 `kata check` —— 自己検査");
        Console.WriteLine();

        // (a) compare 305 セル
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        if (File.Exists(path))
        {
            var want = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(path))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                string[] c = line.Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length < 8) continue;
                want[c[1]] = c.Skip(2).Take(5).Select(x => double.Parse(x.TrimEnd('%'))).ToArray();
            }
            int diff = 0, seen = 0;
            foreach (var (name, f) in CompareBuilds())
            {
                if (!want.TryGetValue(name, out double[]? w)) { diff += 5; continue; }
                double[] r = Rates(f);
                for (int i = 0; i < 5; i++) { seen++; if (Math.Abs(r[i] - w[i]) > 0.001) diff++; }
            }
            Console.WriteLine("- (a) `compare` のセルが `" + path + "` とずれた数: **" + diff + " / " + seen + "**（0 が正）");
        }

        // (b) カタを含まない行では起爆が1度も起きない
        long stray = 0;
        foreach (var (_, f) in CompareBuilds())
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 10; seed++)
                    foreach (UnitTally t in BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).TallyByUnit.Values)
                        stray += t.DetonateFires + t.DetonateDry;
        Console.WriteLine("- (b) カタを含まない 61 行で起きた起爆: **" + stray + "**（0 が正）");

        // (c) 逆流なしは味方を1点も弾けさせない
        long allyNom = 0, foeNom = 0;
        foreach (var (_, f) in Benches)
        {
            Formation g = SwapDef(f, UnitCatalog.KataOld, KataNoBackfire);
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    var r = BattleEngine.Run(g, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    if (r.TallyByUnit.TryGetValue(UnitCatalog.KataOld.Id, out UnitTally? t))
                    { allyNom += t.DetonateAllyNominal; foeNom += t.DetonatePoisonNominal + t.DetonateBurnNominal; }
                }
        }
        Console.WriteLine("- (c) 逆流なしの味方への名目: **" + allyNom + "**（0 が正）／ 敵への名目 " + foeNom + "（正が正）");

        // (d) 乱数を引かない
        if (ParryScan.Init())
        {
            string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            static string Body(string src, string head, string end)
            {
                int i = src.IndexOf(head);
                if (i < 0) return "";
                int j = src.IndexOf(end, i + head.Length);
                return src.Substring(i, (j < 0 ? src.Length : j) - i);
            }
            string b = Body(engine, "public bool " + "Detonate(", "static void " + "DetonateHit")
                     + Body(traits, "class " + "CatalystTrait", "class " + "BackfireTrait");
            string pick = "Pick" + "One", roll = "Roll" + "(", shuf = "Shuffl" + "ed";
            int Count(string x) => (b.Length - b.Replace(x, "").Length) / x.Length;
            Console.WriteLine("- (d) 起爆の本体が `" + pick + "` / `Roll` / `" + shuf + "` を呼ぶ回数: **"
                              + (Count(pick) + Count(roll) + Count(shuf)) + "**（0 が正・走査した本体 " + b.Length + " 字。空なら止める）");
        }

        // (e) ロスター
        Console.WriteLine("- (e) `All` " + UnitCatalog.All.Count + " 枚 ／ ナタ在籍 "
                          + (UnitCatalog.All.Any(d => d.Id == "nata") ? "**○（誤り）**" : "×（正）")
                          + " ／ カタ在籍 " + (UnitCatalog.All.Any(d => d.Id == "kata") ? "○（正）" : "**×（誤り）**")
                          + " ／ `Retired` に ナタ " + (UnitCatalog.Retired.Any(d => d.Id == "nata") ? "○（正）" : "**×（誤り）**")
                          + " ／ `ById(\"nata\")` " + UnitCatalog.ById("nata").Name
                          + " ／ `compare` にナタ " + CompareBuilds().Count(r => r.F.Occupied().Any(o => o.Def.Id == "nata")) + " 行"
                          + "・カタ " + CompareBuilds().Count(r => r.F.Occupied().Any(o => o.Def.Id == "kata")) + " 行");

        // (f) 台本に起爆が載るか
        {
            var f = Benches[1].F;
            int skills = 0, statusByKata = 0, linkedDamage = 0, seedUsed = -1;
            for (int seed = 0; seed < 20 && skills == 0; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[1].Enemy, seed, verbose: true);
                var kata = r.Events.FirstOrDefault(e => e.Kind == BattleEventKind.Skill && e.Text == "起爆");
                if (kata is null) continue;
                skills = r.Events.Count(e => e.Kind == BattleEventKind.Skill && e.Text == "起爆");
                statusByKata = r.Events.Count(e => e.Kind == BattleEventKind.Status && e.ActorId == kata.ActorId);
                // 再生側は `Status` の直後の出どころ無しの `Damage` を紐づける（`IndexStatusDamageEvents`）
                for (int i = 0; i + 1 < r.Events.Count; i++)
                    if (r.Events[i].Kind == BattleEventKind.Status && r.Events[i].ActorId == kata.ActorId
                        && r.Events[i + 1].Kind == BattleEventKind.Damage && r.Events[i + 1].ActorId is null) linkedDamage++;
                seedUsed = seed;
            }
            Console.WriteLine("- (f) 1戦（" + Benches[1].Name + " × 第二波 × seed " + seedUsed + "）の台本: `Skill`「起爆」 "
                              + skills + " 件 ／ カタが書き手の `Status` " + statusByKata + " 件 ／ その直後の出どころ無し `Damage` "
                              + linkedDamage + " 件（3つとも 1 件以上が正）");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ヘルパ
    // =================================================================================

    readonly record struct Led(double Fires, double Dry, double Dual, double Poison, double Burn, double Extra,
                               double FoeDealt, double AllyNom, double AllyDealt, double FoeKills, double AllyKills,
                               double YokeCut);

    static Led LedgerOf(Formation f, int stFrom, int stTo)
    {
        double fi = 0, dr = 0, du = 0, po = 0, bu = 0, ex = 0, fd = 0, an = 0, ad = 0, fk = 0, ak = 0, yc = 0; int n = 0;
        for (int st = stFrom; st <= stTo; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                n++;
                if (!r.TallyByUnit.TryGetValue(UnitCatalog.KataOld.Id, out UnitTally? t)) continue;
                fi += t.DetonateFires; dr += t.DetonateDry; du += t.DetonateDualTargets;
                po += t.DetonatePoisonNominal; bu += t.DetonateBurnNominal; ex += t.DetonateDualExtra;
                fd += t.DetonateFoeDealt; an += t.DetonateAllyNominal; ad += t.DetonateAllyDealt;
                fk += t.DetonateFoeKills; ak += t.DetonateAllyKills; yc += t.DetonateYokeCut;
            }
        return new Led(fi / n, dr / n, du / n, po / n, bu / n, ex / n, fd / n, an / n, ad / n, fk / n, ak / n, yc / n);
    }

    readonly record struct Qual(double AllyDeaths, double NecroPeak, bool HasRica, double WinT, double Survivors, double Wipe);

    static Qual QualOf(Formation f)
    {
        var ids = f.Occupied().Select(o => o.Def.Id).Distinct().ToList();
        bool rica = ids.Contains(UnitCatalog.Rica.Id);
        double deaths = 0, peak = 0, winT = 0, surv = 0, wipe = 0; int n = 0, wins = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                n++;
                foreach (string id in ids)
                    if (r.TallyByUnit.TryGetValue(id, out UnitTally? t)) deaths += t.Deaths;
                if (rica && r.TallyByUnit.TryGetValue(UnitCatalog.Rica.Id, out UnitTally? rt)) peak += rt.NecroPeak;
                if (r.PlayerWon)
                {
                    wins++; winT += r.Turns; surv += r.PlayerSurvivors;
                    if (r.PlayerSurvivors == 1) wipe++;
                }
            }
        return new Qual(deaths / n, peak / n, rica, wins == 0 ? 0 : winT / wins, wins == 0 ? 0 : surv / wins,
                        wins == 0 ? 0 : 100.0 * wipe / wins);
    }

    /// <summary>同じ数値・特性なしの素体（第69期からの器具）。`Actions` を持たないので毎手番殴る。</summary>
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

    static double[] Rates(Formation f)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
            w[st] = 100.0 * wins / Seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells5(double[] w) => string.Join(" | ", w.Select(x => x.ToString("F1")));
}
