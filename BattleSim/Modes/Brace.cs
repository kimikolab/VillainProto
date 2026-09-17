using BattleCore;
using static Common;

// =====================================================================================
// brace モード（第143期） —— ササの転生（身を固めて、はね返した分を撒く）
//
// 指示書は design/PHASE143_BRACE_SPEC.md。
//
//     dotnet run --project BattleSim -c Release 0 brace phase0   # Q0-2（敵の一撃の分布）・Q0-5・Q0-6（台の下見）
//     dotnet run --project BattleSim -c Release 0 brace scan     # 台の下見だけ（V0 の第2〜5波平均が 40〜95% か）
//     dotnet run --project BattleSim -c Release 0 brace run      # 段A〜D の測定
//     dotnet run --project BattleSim -c Release 0 brace check    # 自己検査
// =====================================================================================

static class BraceDiag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": if (arg.StartsWith("probe")) Probe(); else Scan(); return;
            case "run": Sweep(arg); return;
            case "check": Check(arg); return;
            case "log": LogOne(arg); return;
            default:
                Console.WriteLine("brace: モードは phase0 / scan / run / check。");
                return;
        }
    }

    // =================================================================================
    // 台（Q0-6）
    // =================================================================================

    /// <summary>
    /// Q0-2 の測定台。<b>前1 に「素体のササ」</b>（現行の −35% も弾きも持たない同数値の駒）を置いて、
    /// そこへ実際に届く一撃の大きさを数える。<b>盤面は1ビットも動かさない</b>——
    /// <c>UnitCatalog</c> には触らず、この駒は診断のローカルにしか存在しない。
    /// </summary>
    static UnitDef PlainSasa() => new()
    {
        Id = "sasa_plain", Name = "素体のササ", MaxHp = 96,
        Attack = UnitCatalog.Sasa.Attack, Speed = UnitCatalog.Sasa.Speed,
        Advances = false, Traits = Array.Empty<TraitId>()
    };

    /// <summary>
    /// Q0-2 の埋め草。<b>特性を1つも持たない素体</b>にする。
    ///
    /// <para><b>最初は `ネル`（呪詛官）・`ボルグ`・`ムド` を埋め草にして測って外した。</b>
    /// 呪詛は <c>ctx.Dull</c> で<b>敵の攻撃力を下げる</b>ので、第二波の巡礼騎士（攻24）が
    /// 一度も 24 で殴ってこず、最大が 18（狙撃手の貫き）に化けていた
    /// ——<b>測りたいのは「敵が振る一撃の大きさ」であって「この編成に届く一撃」ではない。</b>
    /// 埋め草が敵の数値に触ると、N の掃引点がそのまま埋め草の性能になる。</para>
    /// </summary>
    static UnitDef PlainFiller(string id, int hp, int atk) => new()
    {
        Id = id, Name = $"素体{id}", MaxHp = hp, Attack = atk, Speed = 8,
        Advances = true, Traits = Array.Empty<TraitId>()
    };

    static Formation HitRig() => Formation.Build(
        front1: PlainSasa(), front3: PlainFiller("f1", 90, 20),
        center: PlainFiller("f2", 70, 12), back1: PlainFiller("f3", 60, 12), back3: PlainFiller("f4", 60, 12));

    /// <summary>
    /// ローカル台（Q0-6）。<b><c>Presets</c> には置かない</b>（<c>compare</c> 61行を汚さない）。
    /// ササは全台とも <b>前1</b>——前1 の隣接は 中央 と 後1 の2つで、弾きはそこから席番号最小を選ぶ。
    /// </summary>
    static (string Name, Formation F)[] Rigs(UnitDef sasa) => new (string, Formation)[]
    {
        // 供給 → 出口: 切り落とし分（破片）をガレ（礫・最大保持者を砕く）とウロ（鱗・破片を纏うと貫き）が読む。
        // **ガレは中央・ウロは後1**——ササ（前1）の隣接は {中央, 後1} の2つだけなので、
        // 弾きの宛先はこの2席に限られる（席番号最小＝中央が先）。
        ("台1 出口 (ササ×ガレ×ウロ)",
            Formation.Build(front1: sasa, front3: UnitCatalog.Mug,
                            center: UnitCatalog.Gare, back1: UnitCatalog.Uro, back3: UnitCatalog.Rica)),

        // 壁だけ: 破片の読み手ゼロ・肩代わりゼロ。火力はある（台が床に落ちないように）。
        ("台2 壁だけ (ササ×読み手なし)",
            Formation.Build(front1: sasa, front3: UnitCatalog.Dolga,
                            center: UnitCatalog.Mug, back1: UnitCatalog.Nomi, back3: UnitCatalog.Rica)),

        // 肩代わりと同席: ゴルム（巨躯）がササより前で吸うと、ササには小さい一撃しか届かず
        // 切り落としが出ない（Q0-1 の「分割は上限を回避する経路」）。**噛まないことの確認。**
        ("台3 肩代わり (ササ×ゴルム)",
            Formation.Build(front1: sasa, front3: UnitCatalog.Dolga,
                            center: UnitCatalog.Golm, back1: UnitCatalog.Rica, back3: UnitCatalog.Nomi)),

        // 手番市場: 号令（ガン）が差し出された手番を買う。転倒が収入に化けるか。
        ("台4 手番市場 (ササ×ガン)",
            Formation.Build(front1: sasa, front3: UnitCatalog.Mudo,
                            center: UnitCatalog.Gan, back1: UnitCatalog.Rica, back3: UnitCatalog.Dolga)),
    };

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第143期 Phase 0 —— ササの転生（`brace phase0`）");
        Console.WriteLine();
        HitTable();
        Console.WriteLine();
        Q05();
        Console.WriteLine();
        Scan();
    }

    /// <summary>
    /// Q0-2 —— 敵の一撃の分布。<b>紙で決めない。</b>
    /// <c>BattleEvent</c>（<c>Kind == Damage</c>・<c>TargetId == 0</c> ＝ 前1）の <c>Amount</c> を数える
    /// （<c>InstanceId</c> は <c>ctx.Add</c> が味方から席番号昇順に振るので、前1 が 0）。
    ///
    /// <para><b>第四波は軛（25 上限）が先に切る</b>ので、そのままでは切られた後の値しか見えない。
    /// ササの上限は軛の<b>前</b>に置くので、見たいのは切られる前の大きさ
    /// ——<c>YokeRule(Cap, Active: false)</c> の版も併記する（<b>ノブを振っているのは測定のためだけ</b>）。</para>
    /// </summary>
    static void HitTable()
    {
        Console.WriteLine("## Q0-2 —— 前1 に実際に届く一撃の大きさ（素体のササ・seed 0..49）");
        Console.WriteLine();
        Console.WriteLine("敵からの被弾だけ（味方の巻き込みは除く）。第四波は **軛の切り落とし前**の値を見るため");
        Console.WriteLine("`YokeRule(Active: false)` の版も併記し、合算にはそちらを使う。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 軛 | 発数 | 平均 | p10 | p25 | 中央値 | p75 | p90 | 最大 | 主な値（多い順） |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|");

        var all = new Dictionary<int, List<int>>();
        for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
        {
            foreach (bool yokeOn in st == 3 ? new[] { true, false } : new[] { true })
            {
                var hits = new List<int>();
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult r = BattleEngine.Run(
                        HitRig(), EnemyCatalog.Stages[st].Enemy, seed, verbose: true,
                        yoke: new YokeRule(YokeTrait.Cap, yokeOn));
                    // `ActorId == null` は毒・燃焼の刻み（出どころの無いダメージ）。
                    // **上限が切るのは一撃**なので、刻みは分母から外す。
                    foreach (BattleEvent e in r.Events)
                        if (e.Kind == BattleEventKind.Damage && e.TargetId == 0 && e.ActorId is not null
                            && !e.FriendlyFire && e.Amount > 0)
                            hits.Add(e.Amount);
                }
                hits.Sort();
                if (st != 3 || !yokeOn) all[st] = hits;
                string top = string.Join(" / ", hits.GroupBy(x => x).OrderByDescending(g => g.Count())
                    .Take(5).Select(g => $"{g.Key}×{g.Count()}"));
                Console.WriteLine($"| 第{st + 1}波 | {(yokeOn ? "有" : "**無**")} | {hits.Count} | {(hits.Count == 0 ? 0 : hits.Average()):F1} " +
                                  $"| {P(hits, 10)} | {P(hits, 25)} | {P(hits, 50)} | {P(hits, 75)} | {P(hits, 90)} " +
                                  $"| {(hits.Count == 0 ? 0 : hits[^1])} | {top} |");
            }
        }

        // 第2〜5波を合わせた分布（規約 (G10)。第一波は分母から除外）。
        var pool = new List<int>();
        foreach (var kv in all) pool.AddRange(kv.Value);
        pool.Sort();
        Console.WriteLine();
        Console.WriteLine($"**第2〜5波の合算**（{pool.Count} 発・第四波は軛を外した版）: " +
                          $"平均 {pool.Average():F1} / p10 {P(pool, 10)} / p25 {P(pool, 25)} / 中央値 {P(pool, 50)} " +
                          $"/ p75 {P(pool, 75)} / p90 {P(pool, 90)} / 最大 {pool[^1]}");
        Console.WriteLine();
        Console.WriteLine("### N の3点（**表から引く**。最下点＝ほとんどの一撃を切る／中点＝半分／最上点＝最大の一撃しか切らない）");
        Console.WriteLine();
        Console.WriteLine("| 点 | N | 切る発数の割合 | 切り落とす総量／発 | 切り落とす総量／戦（目安） |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        double perBattle = pool.Count / 200.0;   // 4波 × 50 seed
        foreach ((string label, int n) in new[] { ("最下点", P(pool, 10)), ("中点", P(pool, 50)), ("最上点", P(pool, 90)) })
        {
            int cut = pool.Count(x => x > n);
            double lost = pool.Where(x => x > n).Sum(x => (double)(x - n)) / Math.Max(1, pool.Count);
            Console.WriteLine($"| {label} | {n} | {100.0 * cut / pool.Count:F1}% | {lost:F2} | {lost * perBattle:F1} |");
        }
    }

    static int P(List<int> xs, int pct)
        => xs.Count == 0 ? 0 : xs[Math.Min(xs.Count - 1, Math.Max(0, (int)Math.Round(pct / 100.0 * (xs.Count - 1))))];

    /// <summary>Q0-5 —— ササの現在の帳簿（<c>docs/harm.md</c> 表A と第106期の弾きの発火を走らせて引き直す）。</summary>
    static void Q05()
    {
        Console.WriteLine("## Q0-5 —— ササの現在の帳簿（走らせて引く）");
        Console.WriteLine();
        var rows = Presets.Compare.Where(b => b.F.Occupied().Any(o => o.Def.Id == "sasa")).ToList();
        Console.WriteLine($"`compare` 61 行のうちササを含むのは **{rows.Count} 行**: {string.Join(" / ", rows.Select(r => r.Name))}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第2〜5波平均 | 被弾/戦 | 生存T | 死亡率 | 弾き/戦 | 上限で弾かれ | 隣に味方なし |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach ((string name, Formation f) in rows)
        {
            long battles = 0, taken = 0, life = 0, deaths = 0, shoves = 0, capped = 0, noTarget = 0;
            double win = 0;
            for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    battles++;
                    if (r.PlayerWon) win++;
                    if (r.TallyByUnit.TryGetValue("sasa", out UnitTally? t))
                    {
                        taken += t.DamageTaken; life += t.LastActiveTurn; deaths += t.Deaths > 0 ? 1 : 0;
                        shoves += t.LooseShoves; capped += t.LooseCapped; noTarget += t.LooseNoTarget;
                    }
                }
            Console.WriteLine($"| {name} | {100.0 * win / battles:F1}% | {(double)taken / battles:F1} | {(double)life / battles:F2} " +
                              $"| {100.0 * deaths / battles:F1}% | {(double)shoves / battles:F2} | {(double)capped / battles:F2} | {(double)noTarget / battles:F2} |");
        }
    }

    /// <summary>
    /// Q0-6 の台の下見（第133期「段1 のローカル台にも床と天井の規則を当てる」）。
    /// <b>版を1つも振らない。</b> V0 ＝ 現行ササ（散開 −35% ＋ 弾き）。
    /// </summary>
    static void Scan()
    {
        Console.WriteLine("## Q0-6 —— 台の下見（**版を1つも振らない**。V0 ＝ 現行ササ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | V0 勝率（1..5波） | 第2〜5波平均 | 帯（40〜95%） |");
        Console.WriteLine("|---|---|---:|---|");
        foreach ((string name, Formation f) in Rigs(UnitCatalog.Sasa))
        {
            var w = new double[5];
            for (int st = 0; st < 5; st++)
            {
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
                w[st] = 100.0 * wins / Seeds;
            }
            double m = (w[1] + w[2] + w[3] + w[4]) / 4;
            Console.WriteLine($"| {name} | {string.Join(" / ", w.Select(x => x.ToString("F1")))} | {m:F1} | {(m >= 40 && m <= 95 ? "○" : "**×**")} |");
        }
    }

    /// <summary>
    /// 台1（出口）の埋め草の走査。<b>V0（現行ササ）の第2〜5波平均が 40〜95% に入る組を探す。</b>
    /// ガレ（後1）とウロ（中央）は固定で、前3・後3 の2枠だけを振る。
    /// </summary>
    static void Probe()
    {
        Console.WriteLine("## 台の埋め草の走査（V0 ＝ 現行ササ・seed 0..29）");
        Console.WriteLine();
        Console.WriteLine("**台を作る条件に「`V0` の第2〜5波平均が 40〜95% に入ること」を入れる**（第133期）。");
        Console.WriteLine("**情報セル（0 < x < 100 を第2〜5波で数える）が 3 以上**も課す——天井に並んだ台では版の差が出ない。");
        Console.WriteLine();

        // **肩代わり・被ダメ低減を持つ駒は候補に入れない**——ササに届く一撃を横取りするので、
        // 測りたい供給（切り落とし）が台の側の理由で消える（Q0-1 の「分割は上限を回避する経路」）。
        UnitDef[] cand = { UnitCatalog.Dolga, UnitCatalog.Borg, UnitCatalog.Mudo, UnitCatalog.Rica,
                           UnitCatalog.Hagi, UnitCatalog.Zan, UnitCatalog.Nel, UnitCatalog.Vel,
                           UnitCatalog.Nomi, UnitCatalog.Mug };

        // 台ごとに「動かさない駒」（ササは常に 前1）と、候補から埋める空き席を決める。
        (string Bed, (int Slot, UnitDef Def)[] Fixed)[] beds =
        {
            ("台1 出口 (ササ×ガレ×ウロ)", new[] { (2, UnitCatalog.Gare), (3, UnitCatalog.Uro) }),
            ("台2 壁だけ (ササ×読み手なし)", new[] { (1, UnitCatalog.Dolga) }),
            ("台3 肩代わり (ササ×ゴルム)", new[] { (2, UnitCatalog.Golm) }),
            ("台4 手番市場 (ササ×ガン)", new[] { (2, UnitCatalog.Gan) }),
        };

        foreach ((string bed, var fix) in beds)
        {
            var free = Enumerable.Range(1, 4).Where(i => !fix.Any(x => x.Slot == i)).ToArray();
            var hits = new List<(double M, string Name, double[] W, int Info)>();
            foreach (int[] pick in Pick(cand.Length, free.Length))
            {
                if (pick.Distinct().Count() != pick.Length) continue;
                var f = new Formation();
                f[0] = UnitCatalog.Sasa;
                foreach ((int slot, UnitDef d) in fix) f[slot] = d;
                for (int i = 0; i < free.Length; i++) f[free[i]] = cand[pick[i]];
                if (f.Occupied().Select(o => o.Def.Id).Distinct().Count() != 5) continue;

                var w = new double[5];
                for (int st = 0; st < 5; st++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < 30; seed++)
                        if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
                    w[st] = 100.0 * wins / 30;
                }
                double m = (w[1] + w[2] + w[3] + w[4]) / 4;
                int info = w.Skip(1).Count(x => x > 0 && x < 100);
                if (m >= 40 && m <= 95 && info >= 3)
                    hits.Add((m, string.Join(" / ", f.Occupied().Select(o => o.Def.Name)), w, info));
            }
            Console.WriteLine($"### {bed} —— 帯かつ情報セル 3 以上: **{hits.Count}** 組");
            Console.WriteLine();
            Console.WriteLine("| 組（席順） | 勝率（1..5波） | 第2〜5波平均 | 情報セル |");
            Console.WriteLine("|---|---|---:|---:|");
            foreach (var h in hits.OrderByDescending(x => x.Info).ThenBy(x => Math.Abs(x.M - 67.5)).Take(6))
                Console.WriteLine($"| {h.Name} | {string.Join(" / ", h.W.Select(x => x.ToString("F1")))} | {h.M:F1} | {h.Info} |");
            Console.WriteLine();
        }
    }

    /// <summary>n 種から k 枠への割り当ての総当たり（重複は呼び出し側が弾く）。</summary>
    static IEnumerable<int[]> Pick(int n, int k)
    {
        var idx = new int[k];
        while (true)
        {
            yield return (int[])idx.Clone();
            int i = k - 1;
            while (i >= 0 && ++idx[i] == n) { idx[i] = 0; i--; }
            if (i < 0) yield break;
        }
    }

    // =================================================================================
    // 段A〜D の測定
    // =================================================================================

    /// <summary>
    /// <b>対照（V0）＝ 転生前のササ</b>。第143期に `UnitCatalog.Sasa` の側を転生させたので、
    /// <b>診断のローカルに置くのは「転生前の姿」</b>（第60期の作法。採用で既定が動いたら
    /// 検算の相手は V0 ではなく V1 に移る）。
    /// </summary>
    static UnitDef OldSasa() => new()
    {
        Id = "sasa", Name = "散開のササ（転生前）", MaxHp = 58, Attack = 7, Speed = 12,
        Advances = false, Traits = new[] { TraitId.Loose }
    };

    sealed class Led
    {
        public long Battles, Wins, Turns;
        public long Taken, Life, Deaths, Dealt;
        public long Guards, Cuts, Refused, Given, Lost, Shoves, Capped, NoTarget, Staggers, Muted;
        public long Surrendered, Stalls, ShrapShards, ShrapDealt, ScaleWorn, ScaleAlive;
        public readonly double[] Win = new double[5];
    }

    static Led Measure(Formation f, BraceRule rule) => Measure(f, rule, null);

    /// <summary><paramref name="byWave"/> を渡すと波別の供給（切った回数・切り落とし・配った・戦数）も積む。</summary>
    static Led Measure(Formation f, BraceRule rule, long[][]? byWave)
    {
        var L = new Led();
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                  verbose: false, brace: rule);
                if (r.PlayerWon) wins++;
                if (st == 0) continue;   // 帳簿は第2〜5波（規約 (G10)）
                L.Battles++; L.Turns += r.Turns;
                if (r.PlayerWon) L.Wins++;
                L.ShrapShards += r.ShrapnelShards; L.ShrapDealt += r.ShrapnelDealt;
                L.ScaleWorn += r.ScaleWornTurns; L.ScaleAlive += r.ScaleAliveTurns;
                if (byWave is not null && r.TallyByUnit.TryGetValue("sasa", out UnitTally? bw))
                {
                    byWave[st][0] += bw.BraceCuts; byWave[st][1] += bw.BraceRefused;
                    byWave[st][2] += bw.BraceGiven; byWave[st][3]++;
                }
                if (r.TallyByUnit.TryGetValue("sasa", out UnitTally? t))
                {
                    L.Taken += t.DamageTaken; L.Life += t.LastActiveTurn;
                    L.Deaths += t.Deaths > 0 ? 1 : 0; L.Dealt += t.DamageToEnemy;
                    L.Guards += t.BraceGuards; L.Cuts += t.BraceCuts; L.Refused += t.BraceRefused;
                    L.Given += t.BraceGiven; L.Lost += t.BraceLost; L.Shoves += t.BraceShoves;
                    L.Capped += t.BraceShoveCapped; L.NoTarget += t.BraceNoTarget;
                    L.Staggers += t.BraceStaggers; L.Muted += t.BraceArmorMuted;
                    L.Shoves += t.LooseShoves; L.Capped += t.LooseCapped; L.NoTarget += t.LooseNoTarget;
                }
                // 転倒が手番市場に売れたか。**買い手が通す判定**（`TurnsSurrendered`）で見る
                // ——生の `IdleTurn` を数えると必ず「×」になる（第103期の訂正）。
                foreach (UnitTally any in r.TallyByUnit.Values)
                {
                    L.Surrendered += any.TurnsSurrendered;
                    L.Stalls += any.StallStagger;
                }
            }
            L.Win[st] = 100.0 * wins / Seeds;
        }
        return L;
    }

    static double Mean(Led L) => (L.Win[1] + L.Win[2] + L.Win[3] + L.Win[4]) / 4;

    /// <summary>段A〜D。<c>arg</c> に N の掃引点をカンマ区切りで渡せる（既定 7,12,20 ＝ Q0-2 の3点）。</summary>
    static void Sweep(string arg)
    {
        int[] ns = string.IsNullOrWhiteSpace(arg)
            ? new[] { 7, 12, 20 }
            : arg.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x.Trim())).ToArray();

        Console.WriteLine("# 第143期 段A〜D（`brace run`）");
        Console.WriteLine();
        Console.WriteLine($"台4つ × 版 × 第2〜5波 × seed 0..{Seeds - 1}。**第一波は実行して分母から除外**（規約 (G10)）。");
        Console.WriteLine($"N の掃引点は Q0-2 の表から引いた **{string.Join(" / ", ns)}**（p10 / 中央値 / p90）。");
        Console.WriteLine();

        var versions = new List<(string Tag, UnitDef Sasa, BraceRule Rule)>
        {
            ("V0 転生前", OldSasa(), BraceRule.Default),
            ("段A 上限なし", UnitCatalog.Sasa, new BraceRule(0, false, false)),
        };
        foreach (int n in ns) versions.Add(($"段B N={n}", UnitCatalog.Sasa, new BraceRule(n, false, false)));
        foreach (int n in ns) versions.Add(($"段C N={n} 配る", UnitCatalog.Sasa, new BraceRule(n, true, false)));
        foreach (int n in ns) versions.Add(($"段D N={n} 配る+転倒", UnitCatalog.Sasa, new BraceRule(n, true, true)));

        foreach ((string bedName, Formation _) in Rigs(UnitCatalog.Sasa))
        {
            Console.WriteLine($"## {bedName}");
            Console.WriteLine();
            Console.WriteLine("| 版 | 勝率 2/3/4/5波 | 第2〜5波平均 | 帰属 | 決着T | ササ生存T | 死亡率 | 被弾/戦 | 与ダメ/戦 |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
            double baseM = 0;
            var leds = new List<(string Tag, Led L)>();
            foreach ((string tag, UnitDef sasa, BraceRule rule) in versions)
            {
                Formation f = Rigs(sasa).First(x => x.Name == bedName).F;
                Led L = Measure(f, rule);
                leds.Add((tag, L));
                double m = Mean(L);
                if (tag.StartsWith("V0")) baseM = m;
                Console.WriteLine($"| {tag} | {L.Win[1]:F1} / {L.Win[2]:F1} / {L.Win[3]:F1} / {L.Win[4]:F1} | {m:F1} " +
                                  $"| {(tag.StartsWith("V0") ? "—" : (m - baseM).ToString("+0.0;-0.0"))} " +
                                  $"| {(double)L.Turns / L.Battles:F2} | {(double)L.Life / L.Battles:F2} " +
                                  $"| {100.0 * L.Deaths / L.Battles:F1}% | {(double)L.Taken / L.Battles:F1} | {(double)L.Dealt / L.Battles:F1} |");
            }
            Console.WriteLine();
            Console.WriteLine("| 版 | 身構え/戦 | 切った回数 | 切り落とし/戦 | 配った/戦 | 消えた/戦 | 弾き/戦 | 転ばせ/戦 | 破片で不発 | 差し出した手番/戦 | 転倒で潰れたT/戦 | ガレ砕き/戦 | ウロ纏い率 |");
            Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach ((string tag, Led L) in leds)
                Console.WriteLine($"| {tag} | {(double)L.Guards / L.Battles:F2} | {(double)L.Cuts / L.Battles:F2} " +
                                  $"| {(double)L.Refused / L.Battles:F1} | {(double)L.Given / L.Battles:F1} | {(double)L.Lost / L.Battles:F1} " +
                                  $"| {(double)L.Shoves / L.Battles:F2} | {(double)L.Staggers / L.Battles:F2} | {(double)L.Muted / L.Battles:F2} " +
                                  $"| {(double)L.Surrendered / L.Battles:F2} | {(double)L.Stalls / L.Battles:F2} " +
                                  $"| {(double)L.ShrapShards / L.Battles:F1} " +
                                  $"| {(L.ScaleAlive == 0 ? "—" : (100.0 * L.ScaleWorn / L.ScaleAlive).ToString("F1") + "%")} |");
            Console.WriteLine();
        }

        // 波別の供給（**予測3 を読む表**。採用候補の版だけ出す）。
        int nStar = ns[0];
        Console.WriteLine($"## 波別の供給（段C N={nStar}・**予測3 を読む表**）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 切った回数/戦 | 切り落とし/戦 | 配った/戦 |");
        Console.WriteLine("|---|---|---:|---:|---:|");
        foreach ((string bedName, Formation _) in Rigs(UnitCatalog.Sasa))
        {
            var byWave = new long[5][];
            for (int i = 0; i < 5; i++) byWave[i] = new long[4];
            Measure(Rigs(UnitCatalog.Sasa).First(x => x.Name == bedName).F,
                    new BraceRule(nStar, true, false), byWave);
            for (int st = 1; st < 5; st++)
            {
                long b = Math.Max(1, byWave[st][3]);
                Console.WriteLine($"| {bedName.Split(' ')[0]} | 第{st + 1}波 | {(double)byWave[st][0] / b:F2} " +
                                  $"| {(double)byWave[st][1] / b:F1} | {(double)byWave[st][2] / b:F1} |");
            }
        }
        Console.WriteLine();
    }

    /// <summary>1戦の監査（台名の部分一致 ＋ N ＋ seed）。</summary>
    static void LogOne(string arg)
    {
        string[] a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string bedKey = a.Length > 0 ? a[0] : "台3";
        int n = a.Length > 1 ? int.Parse(a[1]) : 7;
        int seed = a.Length > 2 ? int.Parse(a[2]) : 0;
        Formation f = Rigs(UnitCatalog.Sasa).First(x => x.Name.Contains(bedKey)).F;
        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[1].Enemy, seed, verbose: true,
                                          brace: new BraceRule(n, true, false));
        foreach (LogLine l in r.Log) Console.WriteLine(l.Text);
    }

    /// <summary>
    /// 自己検査。規約 (G8) の必須4項目 ＋ この期の (a)〜(d)。
    /// <paramref name="arg"/> に段A の <c>balance.md</c> を渡すと (a) も検査する。
    /// </summary>
    static void Check(string arg)
    {
        Console.WriteLine("# 第143期 自己検査（`brace check`）");
        Console.WriteLine();

        // 必須1: `compare` 305 セルが `docs/balance.md` と 0 件。
        var live = new List<string>();
        foreach ((string name, Formation f) in Presets.Compare)
        {
            var cells = new List<string>();
            for (int st = 0; st < 5; st++)
            {
                int w = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) w++;
                cells.Add($"{100.0 * w / 200:F1}%");
            }
            live.Add($"| {name} | {string.Join(" | ", cells)} |");
        }
        var doc = File.ReadAllLines("docs/balance.md")
                      .Where(l => l.StartsWith("| ") && l.Contains('%')).ToList();
        int bad = live.Count(l => !doc.Contains(l));
        Console.WriteLine($"- **必須1** `compare` 305 セル ↔ `docs/balance.md`: **ずれ {bad} 件**（61 行 / 305 セル）");

        // 必須4: `ctx.PickOne` を新たに使っていない（第89期 (h)。候補2個以上で `Roll` を消費する）。
        string traits = File.ReadAllText("BattleCore/Traits.cs");
        int bs = traits.IndexOf("public sealed class BraceTrait");
        int be = traits.IndexOf("public sealed class CowerTrait");
        int ss = traits.IndexOf("public static class ShoveRules");
        string body = traits[bs..be] + traits[ss..traits.IndexOf("/// 身構えの規則（第143期）", ss)];
        Console.WriteLine($"- **必須4** `BraceTrait` ＋ `ShoveRules` の `PickOne(` : "
                          + $"**{body.Split("PickOne(").Length - 1} 件**（0 が正）");

        // (a) `Cap = 0` なら `Refuse` は不活性（切り落としが1点も出ないので配るものが無い）。
        // **`Stagger` は上限ではなく弾きに付いているので `Cap` に依らない**——同じ検査で両方出す。
        int aDiff = 0, aStag = 0;
        foreach ((string _, Formation f) in Rigs(UnitCatalog.Sasa))
            for (int st = 0; st < 5; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    bool x = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                              brace: new BraceRule(0, false, false)).PlayerWon;
                    if (x != BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                              brace: new BraceRule(0, true, false)).PlayerWon) aDiff++;
                    if (x != BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                              brace: new BraceRule(0, false, true)).PlayerWon) aStag++;
                }
        Console.WriteLine($"- **(a)** `Cap = 0` で `Refuse` を振っても盤面が動かない: **ずれ {aDiff} 件**（1,000 戦・0 が正）");
        Console.WriteLine($"- **(a')** `Cap = 0` で `Stagger` を振ると盤面が動く: **ずれ {aStag} 件**"
                          + "（**0 でないのが正**。転倒は上限ではなく弾きに付いている）");

        // (b) ササを含まない行は `BraceRule` を振っても 1 セルも動かない。
        int bDiff = 0;
        foreach ((string _, Formation f) in Presets.Compare.Concat(Presets.Cross))
        {
            if (f.Occupied().Any(o => o.Def.Id == "sasa")) continue;
            for (int st = 0; st < 5; st++)
                for (int seed = 0; seed < 20; seed++)
                {
                    bool x = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon;
                    bool y = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                              brace: new BraceRule(20, true, true)).PlayerWon;
                    if (x != y) bDiff++;
                }
        }
        Console.WriteLine($"- **(b)** ササを含まない 72 行は `BraceRule` に依らない: **ずれ {bDiff} 件**");

        // (c) 帳簿が閉じる（配った ≦ 切り落とし）／(d) 敵側に転倒が立たない。
        long refused = 0, given = 0, lost = 0, foeStagger = 0, muted = 0;
        foreach ((string _, Formation f) in Rigs(UnitCatalog.Sasa))
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                                      brace: new BraceRule(7, true, true));
                    if (r.TallyByUnit.TryGetValue("sasa", out UnitTally? t))
                    {
                        refused += t.BraceRefused; given += t.BraceGiven;
                        lost += t.BraceLost; muted += t.BraceArmorMuted;
                    }
                    foreach (var kv in r.TallyByUnit)
                        if (EnemyCatalog.Stages[st].Enemy.Occupied().Any(o => o.Def.Id == kv.Key))
                            foeStagger += kv.Value.StallStagger;
                }
        Console.WriteLine($"- **(c)** 配った {given} ≦ 切り落とし {refused}: **{(given <= refused ? "OK" : "**×**")}**"
                          + $"（消えた {lost} ／ 残り {refused - given - lost}）");
        Console.WriteLine($"- **(d)** 敵側に転倒が立った回数: **{foeStagger}**（0 が正。弾きは味方しか選ばない）");
        Console.WriteLine($"- 参考: 破片が一撃を全部吸って弾きが鳴らなかった回数（Q0-1 の穴）: **{muted}**");
        Console.WriteLine();
        Console.WriteLine("**必須2**（`docs/` 全再生成の差分）と **必須3**（触っていないノブの既定）は報告書に書く。");
    }
}
