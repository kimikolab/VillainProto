using BattleCore;
using static Common;

// =====================================================================================
// shove モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "shove")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 shove
// =====================================================================================

static class ShoveDiag
{
public static void Run(string[] args, int stageIndex)
{
    var shBuilds = CompareBuilds();
    const int ShSeeds = 200;   // compare / spread / expose と同じ。balance.md と突き合わせる

    string shFilter = args.Length > 2 ? args[2] : "突き返し";
    var shTargets = shBuilds
        .Where(b => shFilter.Length == 0 || shFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    IReadOnlyList<EnemyCatalog.Stage> shStages = EnemyCatalog.Stages;

    // 供給元のラベル。**ログの文字列を数えている**（gullet log / yoke log / hush / sever と
    // 同じ理由）——移動の「出どころ」は盤面の値に痕跡を残さないし、SwapSlots は誰が呼んだかを
    // 記録していない。判定は engine ではなくここに閉じている。
    (string Mark, string Label)[] shSources =
    {
        ("が隊列をかき回した", "喧噪"),
        ("の前へ引きずり出した", "曝き"),
        ("は耐えきれず一列後ろへ下がった", "臆病"),
        ("を突き飛ばして後ろへ逃げた", "臆病"),
        ("を押しのけて前に出た", "棘守り"),
    };

    // **臆病だけはログが SwapSlots の後に出る**（CowardTrait は入れ替えてから
    // 「突き飛ばして後ろへ逃げた」を書く）。他の3つは呼ぶ前に書くので、
    // 素朴な「直前の原因行」では臆病が永久に 0 になる。
    //
    // 1ターンに突き返しが発火するのは**高々1回**（ShoveTrait の上限）なので、
    // ターンの中で「印より前の原因行」を優先し、無ければ「印より後の臆病行」を採れば
    // 曖昧さなく割れる。ターンの区切りは LogKind.Turn で取る。
    const string ShoveMarkA = "の突き返しが";      // 効果A が成立した行
    const string ShoveMarkB = "勢い余って";        // 効果B が当たった行

    // 1戦ぶんの計数。**ウツ（逆しま）の攻撃力は StatSnapshot から取る**
    // ——ログの文字列には現在値が載らないし、CurrentAttack は BattleResult に無い。
    (double Fire, double Cap, double Swap, double NoRow, double Stagger, double Block,
     double Turns, double AtkOpen, double AtkMax, double AtkLast, double Covered,
     Dictionary<string, double> Src, Dictionary<string, int> Pulled, Dictionary<string, int> Hit)
    MeasureShove(Formation f, Formation enemy, ShoveRule rule)
    {
        var src = new Dictionary<string, double>();
        var pulled = new Dictionary<string, int>();
        var hit = new Dictionary<string, int>();
        double fire = 0, cap = 0, swap = 0, norow = 0, stag = 0, block = 0, turns = 0, covered = 0;
        double open = 0, max = 0, last = 0, seen = 0;

        // 逆しま持ちの InstanceId。味方はスロット昇順に 0 から振られる（Materialize → ctx.Add）。
        int readerId = -1;
        for (int i = 0, k = 0; i < FormationRules.PlayableSlotCount; i++)
            if (f[i] is { } d) { if (d.Traits.Contains(TraitId.Perverse)) readerId = k; k++; }

        for (int seed = 0; seed < ShSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: true,
                                    null, null, null, null, null, rule);
            fire += r.ShoveFired; cap += r.ShoveCapped; swap += r.ShoveSwapped;
            norow += r.ShoveNoRow; stag += r.ShoveStaggered; block += r.ShoveBlocked;
            turns += r.Turns;

            // --- 供給元の帰属（ターンごとに1回だけ） --------------------------------------
            var lines = r.Log.ToList();
            int t0 = 0;
            while (t0 < lines.Count)
            {
                int t1 = t0 + 1;
                while (t1 < lines.Count && lines[t1].Kind != LogKind.Turn) t1++;

                int mark = -1;
                for (int i = t0; i < t1; i++)
                {
                    string tx = lines[i].Text;
                    if (tx.Contains(ShoveMarkA) || tx.Contains(ShoveMarkB)) { mark = i; break; }

                    // 効果A の入れ替え先の名前（引き出された敵）
                }
                if (mark >= 0)
                {
                    string? label = null;
                    for (int i = mark - 1; i >= t0 && label is null; i--)
                        foreach (var (mk, lb) in shSources)
                            if (lines[i].Text.Contains(mk)) { label = lb; break; }
                    if (label is null)
                        for (int i = mark + 1; i < t1 && label is null; i++)
                            if (lines[i].Text.Contains("は耐えきれず一列後ろへ下がった")
                                || lines[i].Text.Contains("を突き飛ばして後ろへ逃げた")) label = "臆病";
                    if (label is not null)
                    {
                        src[label] = src.TryGetValue(label, out double c) ? c + 1 : 1;
                        covered++;
                    }
                    else
                    {
                        src["不明"] = src.TryGetValue("不明", out double u) ? u + 1 : 1;
                    }
                }
                t0 = t1;
            }

            // --- 崩し・よろけの受け手（名前）------------------------------------------------
            foreach (LogLine l in r.Log)
            {
                if (l.Text.Contains(ShoveMarkA))
                {
                    // 「{保持者} の突き返しが {駒} を {席} の前へ突き崩した」
                    int a = l.Text.IndexOf(ShoveMarkA, StringComparison.Ordinal);
                    int b = l.Text.IndexOf(" を ", StringComparison.Ordinal);
                    if (a >= 0 && b > a)
                    {
                        string who = l.Text.Substring(a + ShoveMarkA.Length + 1, b - a - ShoveMarkA.Length - 1);
                        pulled[who] = pulled.TryGetValue(who, out int c) ? c + 1 : 1;
                    }
                }
                if (l.Text.Contains(ShoveMarkB))
                {
                    // 「勢い余って {A・B} の体勢まで崩れた（攻撃 -N）」
                    int a = l.Text.IndexOf(ShoveMarkB, StringComparison.Ordinal) + ShoveMarkB.Length;
                    int b = l.Text.IndexOf(" の体勢まで崩れた", StringComparison.Ordinal);
                    if (b > a)
                        foreach (string raw in l.Text.Substring(a, b - a).Split('・'))
                        {
                            string nm = raw.Trim();
                            hit[nm] = hit.TryGetValue(nm, out int c) ? c + 1 : 1;
                        }
                }
            }

            // --- 読み手の攻撃力（StatSnapshot。ターン頭の CurrentAttack）--------------------
            if (readerId >= 0)
            {
                var snaps = r.Events
                    .Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == readerId)
                    .Select(e => e.Amount).ToList();
                if (snaps.Count > 0)
                {
                    open += snaps[0]; max += snaps.Max(); last += snaps[^1]; seen++;
                }
            }
        }

        double n = ShSeeds, m = Math.Max(1, seen);
        foreach (string k in src.Keys.ToList()) src[k] /= n;
        return (fire / n, cap / n, swap / n, norow / n, stag / n, block / n, turns / n,
                open / m, max / m, last / m, covered / n, src, pulled, hit);
    }

    Console.WriteLine("# 突き返し（shove）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 shove [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{ShSeeds - 1}。数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`CompareBuilds()` / `Stages` / `Columns` は触っていない。");
    Console.WriteLine("既定の絞り込みは `突き返し`（引数で上書きできる）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 突き返し | 発火回数/戦（`BattleResult.ShoveFired`） |");
    Console.WriteLine("| 空振り | 1ターン1回の上限で弾かれた回数/戦（**ログを1行も出さない**ので結果から取る） |");
    Console.WriteLine("| 崩し | 効果A（敵陣の突き崩し）が成立した回数/戦 |");
    Console.WriteLine("| 列欠 | 敵の後列か前列が 0 体で効果Aだけが空振りした回数/戦 |");
    Console.WriteLine("| よろけ | 効果Bが当たった**延べ体数**/戦 |");
    Console.WriteLine("| 弾き | 効果Bが `Stoic`（ガルド）で弾かれた延べ体数/戦 |");
    Console.WriteLine("| ウツ攻 | 逆しま持ちの `CurrentAttack`（開戦時 / 最大 / 最終T）。`StatSnapshot` から |");
    Console.WriteLine("| 決着T | 決着までのターン数/戦 |");
    Console.WriteLine();
    Console.WriteLine("**`ウツ攻` の 開戦時 < 最大 がこの期の核心**（受け入れ基準2）。");
    Console.WriteLine("現行の 44 行ではウツの攻撃力は戦闘を通じて定数なので、動くこと自体が新しい挙動。");
    Console.WriteLine("`最終T` は**最後のターン頭**の値（`StatSnapshot` はターン頭にしか出ない）。");
    Console.WriteLine();

    foreach (var (bname, bf) in shTargets)
    {
        Console.WriteLine($"## {bname}");
        Console.WriteLine();

        // ---- 陽性対照 ----------------------------------------------------------------------
        Console.WriteLine("### 0. 陽性対照 `ShoveRule(0)`（受け入れ基準3の分母）");
        Console.WriteLine();
        Console.WriteLine("`Penalty = 0` は**効果Bだけを止める**（効果Aは走る）。");
        Console.WriteLine("よろけ・弾きが 0 で、ウツ攻が 開戦時 = 最大 = 最終T の定数になるはず。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 突き返し | 崩し | よろけ | 弾き | ウツ攻(開/最大/終) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int w = 0; w < shStages.Count; w++)
        {
            var z = MeasureShove(bf, shStages[w].Enemy, new ShoveRule(0));
            Console.WriteLine($"| 第{w + 1}波 | {z.Fire:0.00} | {z.Swap:0.00} | {z.Stagger:0.00} | {z.Block:0.00} "
                + $"| {z.AtkOpen:0.0} / {z.AtkMax:0.0} / {z.AtkLast:0.0} | {z.Turns:0.0} |");
        }
        Console.WriteLine();

        // ---- 本番 --------------------------------------------------------------------------
        Console.WriteLine($"### 1. 計数（`ShoveRule.Default` ＝ Penalty {ShoveRule.Default.Penalty}）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 突き返し | 空振り | 崩し | 列欠 | よろけ | 弾き | ウツ攻(開/最大/終) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");

        var srcAll = new Dictionary<string, double>();
        var pulledAll = new Dictionary<string, int>();
        var hitAll = new Dictionary<string, int>();
        double covAll = 0, fireAll = 0;

        for (int w = 0; w < shStages.Count; w++)
        {
            var z = MeasureShove(bf, shStages[w].Enemy, ShoveRule.Default);
            foreach (var kv in z.Src) srcAll[kv.Key] = srcAll.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in z.Pulled) pulledAll[kv.Key] = pulledAll.TryGetValue(kv.Key, out int c) ? c + kv.Value : kv.Value;
            foreach (var kv in z.Hit) hitAll[kv.Key] = hitAll.TryGetValue(kv.Key, out int c) ? c + kv.Value : kv.Value;
            covAll += z.Covered; fireAll += z.Fire;

            Console.WriteLine($"| 第{w + 1}波 | {z.Fire:0.00} | {z.Cap:0.00} | {z.Swap:0.00} | {z.NoRow:0.00} "
                + $"| {z.Stagger:0.00} | {z.Block:0.00} "
                + $"| {z.AtkOpen:0.0} / {z.AtkMax:0.0} / {z.AtkLast:0.0} | {z.Turns:0.0} |");
        }
        Console.WriteLine();

        Console.WriteLine("#### 供給元の内訳（発火回/戦・全波の合計）");
        Console.WriteLine();
        Console.WriteLine("**ログの文字列から割っている。** 臆病だけは入れ替えの**後**に書くので、");
        Console.WriteLine("ターンの中で「印より前の原因行」を先に見て、無ければ「印より後の臆病行」を採る。");
        Console.WriteLine("1ターンに発火は高々1回（上限）なので、この2段で曖昧さなく割れる。");
        Console.WriteLine();
        Console.WriteLine("| 供給元 | 発火回/戦（5波合計） |");
        Console.WriteLine("|---|--:|");
        foreach (var kv in srcAll.OrderByDescending(k => k.Value))
            Console.WriteLine($"| {kv.Key} | {kv.Value:0.00} |");
        Console.WriteLine($"| **帰属できた計** | **{covAll:0.00}** |");
        Console.WriteLine($"| **発火の計** | **{fireAll:0.00}** |");
        Console.WriteLine();
        Console.WriteLine($"**帰属率 {(fireAll > 0 ? covAll * 100.0 / fireAll : 0):0.0}%。**"
            + " 残りは効果A・効果Bのどちらも成立せず、ログに1行も出なかった発火"
            + "（敵の列が欠けていて、かつ隣接味方が全員 `Stoic` か不在）。");
        Console.WriteLine();

        Console.WriteLine("#### 引き出された敵（上位3体）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 回数 |");
        Console.WriteLine("|---|--:|");
        foreach (var kv in pulledAll.OrderByDescending(k => k.Value).Take(3))
            Console.WriteLine($"| {kv.Key} | {kv.Value} |");
        Console.WriteLine();

        Console.WriteLine("#### よろけの受け手（延べ）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 回数 |");
        Console.WriteLine("|---|--:|");
        foreach (var kv in hitAll.OrderByDescending(k => k.Value))
            Console.WriteLine($"| {kv.Key} | {kv.Value} |");
        Console.WriteLine();

        // ---- 掃引 --------------------------------------------------------------------------
        Console.WriteLine("### 2. 掃引（`Penalty`）");
        Console.WriteLine();
        Console.WriteLine("**各点に対照は要らない**——このノブは敵側の盤面を1も動かさない。");
        Console.WriteLine("`ShoveRule(0)` の1本を全点の基準に置く。");
        Console.WriteLine();
        Console.WriteLine("**見るのは勝率ではなく、ウツの攻撃力の到達点と、ウツ以外の駒の目減り量の比。**");
        Console.WriteLine("`利得` = ウツ攻の最大 − 開戦時（逆しまは下げ幅の3倍で読むので `Penalty×発火×3` が理論値）。");
        Console.WriteLine("`目減り` = ウツ以外の受け手が失った攻撃力の総量（`よろけ` の延べ体数 × `Penalty` から");
        Console.WriteLine("ウツ取り分を引いたもの）。");
        Console.WriteLine();

        int[] pens = { 0, 1, 2, 3 };
        Console.WriteLine("| Penalty | 平均勝率 | 突き返し/戦 | よろけ/戦 | ウツ攻(開→最大) | 利得 | ウツ以外の目減り | 比 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int pen in pens)
        {
            double wins = 0, fireS = 0, stagS = 0, openS = 0, maxS = 0, othS = 0;
            for (int w = 0; w < shStages.Count; w++)
            {
                var z = MeasureShove(bf, shStages[w].Enemy, new ShoveRule(pen));
                fireS += z.Fire; stagS += z.Stagger; openS += z.AtkOpen; maxS += z.AtkMax;
                double utsu = z.Hit.Where(k => k.Key.Contains("ウツ")).Sum(k => (double)k.Value) / ShSeeds;
                othS += (z.Stagger - utsu) * pen;
                for (int seed = 0; seed < ShSeeds; seed++)
                    if (BattleEngine.Run(bf, shStages[w].Enemy, seed, false, null, null, null, null,
                                         null, new ShoveRule(pen)).PlayerWon) wins++;
            }
            int nw = shStages.Count;
            double gain = (maxS - openS) / nw;
            double loss = othS / nw;
            Console.WriteLine($"| {pen} | {wins * 100.0 / (ShSeeds * nw):0.0}% | {fireS / nw:0.00} | {stagS / nw:0.00} "
                + $"| {openS / nw:0.0} → {maxS / nw:0.0} | {gain:+0.0;-0.0;0.0} | {loss:0.00} "
                + $"| {(loss > 0.001 ? (gain / loss).ToString("0.00") : "—")} |");
        }
        Console.WriteLine();

        // ---- 波ごとの勝率（崖の確認・受け入れ基準4）------------------------------------------
        Console.WriteLine("### 3. 波ごとの勝率（受け入れ基準4 ＝ 崖になっていないか）");
        Console.WriteLine();
        Console.WriteLine("| Penalty" + string.Concat(Enumerable.Range(1, shStages.Count).Select(i => $" | 第{i}波")) + " | 平均 |");
        Console.WriteLine("|--:" + string.Concat(shStages.Select(_ => "|--:")) + "|--:|");
        foreach (int pen in pens)
        {
            var cells = new List<double>();
            for (int w = 0; w < shStages.Count; w++)
            {
                int win = 0;
                for (int seed = 0; seed < ShSeeds; seed++)
                    if (BattleEngine.Run(bf, shStages[w].Enemy, seed, false, null, null, null, null,
                                         null, new ShoveRule(pen)).PlayerWon) win++;
                cells.Add(win * 100.0 / ShSeeds);
            }
            Console.WriteLine($"| {pen}" + string.Concat(cells.Select(c => $" | {c:0.0}%"))
                + $" | {cells.Average():0.0}% |");
        }
        Console.WriteLine();
    }

    // ---- ローカル変種（読み手の有無で符号が反転するか。§4-5）---------------------------------
    Console.WriteLine("## 変種（読み手の有無 —— `CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine("採用行の**ウツ1枚だけ**を差し替えた版を診断のローカルに組む");
    Console.WriteLine("（`gradient` / `aim` / `route` と同じ扱い）。**動く変数は中央の1枚だけ。**");
    Console.WriteLine();
    Console.WriteLine("- `V0 ウツ`   …… 採用行そのもの（読み手あり）");
    Console.WriteLine("- `V1 ノノ`   …… 読み手を回復役に差し替え（弱体化を読む駒がいない）");
    Console.WriteLine("- `V2 ムド`   …… 読み手を被弾強化に差し替え（弱体化は素の減算）");
    Console.WriteLine();
    Console.WriteLine("各版で**ハネを抜いた版**（4体）との差を取る。**ハネの寄与の符号が反転すれば、");
    Console.WriteLine("「編成によって同じ駒の符号が変わる」が実体を持つ。**");
    Console.WriteLine("4体版は `ablate` と同じく5体目の体そのものを含むので、**版どうしの差**で読むこと。");
    Console.WriteLine();

    (string Name, Formation With, Formation Without)[] shVars =
    {
        ("V0 ウツ（採用行）",
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane),
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga)),
        ("V1 ノノ（回復役）",
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Lili, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane),
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Lili, back1: UnitCatalog.Dolga)),
        ("V2 ムド（被弾強化）",
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Mudo, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane),
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Mudo, back1: UnitCatalog.Dolga)),
    };

    Console.WriteLine("| 版 | ハネ入り | ハネ抜き(4体) | ハネの寄与 |"
        + string.Concat(Enumerable.Range(1, shStages.Count).Select(i => $" 第{i}波差 |")));
    Console.WriteLine("|---|--:|--:|--:|" + string.Concat(shStages.Select(_ => "---:|")));
    foreach (var (vname, with, without) in shVars)
    {
        var dw = new List<double>();
        double aw = 0, ao = 0;
        for (int w = 0; w < shStages.Count; w++)
        {
            int a = 0, b = 0;
            for (int seed = 0; seed < ShSeeds; seed++)
            {
                if (BattleEngine.Run(with, shStages[w].Enemy, seed, false).PlayerWon) a++;
                if (BattleEngine.Run(without, shStages[w].Enemy, seed, false).PlayerWon) b++;
            }
            double pa = a * 100.0 / ShSeeds, pb = b * 100.0 / ShSeeds;
            aw += pa; ao += pb; dw.Add(pa - pb);
        }
        int nw = shStages.Count;
        Console.WriteLine($"| {vname} | {aw / nw:0.0}% | {ao / nw:0.0}% | {(aw - ao) / nw:+0.0;-0.0;0.0}pt |"
            + string.Concat(dw.Select(d => $" {d:+0.0;-0.0;0.0} |")));
    }
    Console.WriteLine();

    // ---- 符号反転の本命: 版ごとに Penalty を振る -----------------------------------------
    //
    // **`ablate` では測れない。** ハネを抜いた 4 体版はどの版でも 100/0/0/0/0 の床に落ちる
    // （第21期 swap の「4体（中央 空）」検査と同じ症状）ので、抜いた差は
    // 「5体目の体そのもの」の値段に潰れて機構の符号が読めない。
    //
    // 代わりに**同じ5体のまま `Penalty` だけを振る**。効果Bは味方の攻撃力しか触らないので、
    // **Penalty に対する勝率の傾きがそのまま「効果Bが利得か代金か」の符号**になる。
    // 読み手（ウツ）がいる版で正、いない版で負になれば、符号反転は実体を持つ。
    Console.WriteLine("### 符号反転（`Penalty` に対する勝率の傾き）");
    Console.WriteLine();
    Console.WriteLine("**`ablate` では測れない**——ハネを抜いた4体版はどの版でも床（100/0/0/0/0）に");
    Console.WriteLine("落ちるので、抜いた差が「5体目の体そのもの」の値段に潰れる");
    Console.WriteLine("（第21期 `swap` の「4体（中央 空）」検査と同じ症状）。");
    Console.WriteLine();
    Console.WriteLine("代わりに**同じ5体のまま `Penalty` だけを振る**。効果Bは味方の攻撃力しか触らないので、");
    Console.WriteLine("**`Penalty` に対する勝率の傾きがそのまま「効果Bが利得か代金か」の符号**になる。");
    Console.WriteLine();
    Console.WriteLine("| 版 | P=0 | P=1 | P=2 | P=3 | Δ(3−0) | 符号 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|:-:|");
    int[] shPens = { 0, 1, 2, 3 };
    foreach (var (vname, with, _) in shVars)
    {
        var cells = new List<double>();
        foreach (int pen in shPens)
        {
            int win = 0;
            for (int w = 0; w < shStages.Count; w++)
                for (int seed = 0; seed < ShSeeds; seed++)
                    if (BattleEngine.Run(with, shStages[w].Enemy, seed, false, null, null, null,
                                         null, null, new ShoveRule(pen)).PlayerWon) win++;
            cells.Add(win * 100.0 / (ShSeeds * shStages.Count));
        }
        double d = cells[^1] - cells[0];
        Console.WriteLine($"| {vname}" + string.Concat(cells.Select(c => $" | {c:0.0}%"))
            + $" | {d:+0.0;-0.0;0.0}pt | {(d > 0 ? "**＋**" : d < 0 ? "**−**" : "0")} |");
    }
    Console.WriteLine();

    // ---- 席と効果Bの値段（P1 の直接検算）-------------------------------------------------
    //
    // 編成5枠が埋まっていれば隣接は角=2体・中央=4体なので、効果Bの延べ体数は**ちょうど 2.0 倍**
    // になるはず——というのが机上の予測。**ただし喧噪（バサ）が毎ターン味方2体を混ぜる**ので、
    // 初期配置の次数が効くのは開幕の数ターンだけ。倍率が 2.0 から落ちるなら、
    // それは席が固定でないことの直接の証拠になる。
    //
    // 比べるのは**ハネの席だけを動かした2通り**（他4体の相対配置は変えない）。
    Console.WriteLine("### 席と効果Bの値段（P1 の直接検算）");
    Console.WriteLine();
    Console.WriteLine("編成5枠が埋まっていれば隣接は**角=2体・中央=4体**なので、効果Bの延べ体数は");
    Console.WriteLine("机上では**ちょうど 2.0 倍**になるはず。**ハネの席だけ**を動かして測る。");
    Console.WriteLine();
    Console.WriteLine("| ハネの席 | 隣接次数(初期) | 突き返し/戦 | よろけ/戦 | 弾き/戦 | よろけ÷発火 | ウツ攻(開→最大) | 平均勝率 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");

    (string Seat, int Deg, Formation F)[] shSeats =
    {
        ("後3（角）", 2, Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald,
            center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane)),
        ("中央", 4, Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald,
            center: UnitCatalog.Hane, back1: UnitCatalog.Dolga, back3: UnitCatalog.Utsu)),
    };

    foreach (var (seat, deg, sf) in shSeats)
    {
        double fi = 0, st = 0, bl = 0, op = 0, mx = 0, win = 0;
        for (int w = 0; w < shStages.Count; w++)
        {
            var z = MeasureShove(sf, shStages[w].Enemy, ShoveRule.Default);
            fi += z.Fire; st += z.Stagger; bl += z.Block; op += z.AtkOpen; mx += z.AtkMax;
            for (int seed = 0; seed < ShSeeds; seed++)
                if (BattleEngine.Run(sf, shStages[w].Enemy, seed, false).PlayerWon) win++;
        }
        int nw = shStages.Count;
        Console.WriteLine($"| {seat} | {deg} | {fi / nw:0.00} | {st / nw:0.00} | {bl / nw:0.00} "
            + $"| {st / Math.Max(0.001, fi):0.00} | {op / nw:0.0} → {mx / nw:0.0} "
            + $"| {win * 100.0 / (ShSeeds * nw):0.0}% |");
    }
    Console.WriteLine();
    Console.WriteLine("**`よろけ÷発火` が机上の次数（2 と 4）に届かない差が、席が固定でないことの代金。**");
    Console.WriteLine("喧噪は毎ターン味方2体を混ぜるので、初期配置の次数が効くのは開幕の数ターンだけ。");
    Console.WriteLine();

    // ---- シオ（移り木）との同居可否 -------------------------------------------------------
    //
    // 移り木は**動かされた味方を癒し、攻撃力を +5 する**（DrifterTrait.Gain）。
    // ウツは強化されると攻撃力が半減する（PerverseTrait: AtkBonus > 0 で baseAtk / 2）ので、
    // 喧噪が毎ターン味方2体を動かす台では**移動が起きるたびシオがウツを台無しにする**
    // 可能性がある。移動軸の既存行のうち `移動改 (バサ×ヨミ×シオ)` がシオ入りなので、
    // 同居の可否を数字で出しておく（指示書 §6-3）。
    Console.WriteLine("### シオ（移り木）との同居可否（指示書 §6-3）");
    Console.WriteLine();
    Console.WriteLine("移り木は動かされた味方の `AtkBonus` を **+5**（`DrifterTrait.Gain`）。");
    Console.WriteLine("突き返しは1ターン1回 **−Penalty**。**同じ `AtkBonus` を逆向きに奪い合う。**");
    Console.WriteLine("ウツは `AtkBonus > 0` で攻撃力が半減するので、勝つ側が符号を決める。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 平均勝率 | ウツ攻(開/最大/最終T) |");
    Console.WriteLine("|---|--:|--:|");

    Formation shioOff = Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald,
        center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane);
    Formation shioOn = Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald,
        center: UnitCatalog.Utsu, back1: UnitCatalog.Shio, back3: UnitCatalog.Hane);

    foreach (var (vn, vf) in new[] { ("シオなし（採用行）", shioOff), ("シオあり（ドルガ→シオ）", shioOn) })
    {
        double op = 0, mx = 0, la = 0, win = 0;
        for (int w = 0; w < shStages.Count; w++)
        {
            var z = MeasureShove(vf, shStages[w].Enemy, ShoveRule.Default);
            op += z.AtkOpen; mx += z.AtkMax; la += z.AtkLast;
            for (int seed = 0; seed < ShSeeds; seed++)
                if (BattleEngine.Run(vf, shStages[w].Enemy, seed, false).PlayerWon) win++;
        }
        int nw = shStages.Count;
        Console.WriteLine($"| {vn} | {win * 100.0 / (ShSeeds * nw):0.0}% "
            + $"| {op / nw:0.0} / {mx / nw:0.0} / {la / nw:0.0} |");
    }
    Console.WriteLine();
    return;
}
}
