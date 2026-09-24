using BattleCore;
using static Common;

// =====================================================================================
// sid モード（第196期）の本体 —— 吐く相手を散らす（S）と、序盤の生存の手当て（S+反・S+HP）
//
//     dotnet run --project BattleSim -c Release 0 sid run196     # 主表（前列スィド＋中央ベニ）・参考表・在席行
//     dotnet run --project BattleSim -c Release 0 sid ledger196  # 帳簿（印の数のターン別・既に印・減らした量・倒れとベニの回復）
//     dotnet run --project BattleSim -c Release 0 sid check196 [第195期のbalance.md]  # 自己検査
//
// **版は診断のローカルに写した `UnitDef`**（第195期と同じ作法）。規定の版（`UnitCatalog.Sid`）は S。
// =====================================================================================

static partial class SidDiag
{
    /// <summary>第195期のスィド（散らさない吐き＝`SpewFixed`）。</summary>
    static readonly UnitDef Sid195 = Clone(UnitCatalog.Sid, new[] { TraitId.SpewFixed, TraitId.Venom, TraitId.Numb }, UnitCatalog.Sid.Actions);
    /// <summary>S+反（散らし＋殴られたときの毒 +8＝`VenomHeavy`）。</summary>
    static readonly UnitDef SidRe = Clone(UnitCatalog.Sid, new[] { TraitId.Spew, TraitId.VenomHeavy, TraitId.Numb }, UnitCatalog.Sid.Actions);
    /// <summary>S+HP（散らし＋最大HP <see cref="UnitCatalog.SidHardyHp"/>）。</summary>
    static readonly UnitDef SidHp = new()
    {
        Id = "sid", Name = UnitCatalog.Sid.Name, MaxHp = UnitCatalog.SidHardyHp, Attack = UnitCatalog.Sid.Attack, Speed = UnitCatalog.Sid.Speed,
        Pattern = UnitCatalog.Sid.Pattern, Advances = UnitCatalog.Sid.Advances, Actions = UnitCatalog.Sid.Actions,
        Traits = UnitCatalog.Sid.Traits, PlusText = UnitCatalog.Sid.PlusText, MinusText = UnitCatalog.Sid.MinusText, Flavor = UnitCatalog.Sid.Flavor,
    };

    /// <summary>
    /// **プロパティにしてある**——partial クラスの静的フィールドの初期化順はファイルをまたいで決まらないので、
    /// 配列にすると `SidNew` / `SidPlainG`（`Sid.Run.cs`）がまだ null のまま詰まる（1度踏んだ：ガルドの席が空席になっていた）。
    /// </summary>
    static (string Tag, UnitDef D)[] Versions196 => new[] { ("S", SidNew), ("S+反", SidRe), ("S+HP", SidHp), ("素体", SidPlainG) };

    /// <summary>§4.1 のベニ中央: ベニがいれば中央の駒と入れ替え、いなければ中央の駒をベニに替える。</summary>
    static Formation BeniCenter(Formation f)
    {
        if (Has(f, "beni")) return SwapSeats(f, "beni", f[2]!.Id);
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = slot == 2 ? UnitCatalog.Beni : d;
        return g;
    }

    static void RunMore196(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run196": Run196(); handled = true; return;
            case "ledger196": Ledger196(); handled = true; return;
            case "check196": Check196(arg); handled = true; return;
        }
    }

    // =================================================================================
    // run196
    // =================================================================================

    static void Run196()
    {
        Console.WriteLine("# 第196期 `sid run196` —— 前列スィド＋中央ベニ（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("seed 0..199・平均は第2〜5波。**S** ＝ 散らし（規定の版）／ **S+反** ＝ 散らし＋殴られたときの毒 +8 ／ **S+HP** ＝ 散らし＋HP " + UnitCatalog.SidHardyHp
                          + " ／ **195** ＝ 第195期のスィド（散らさない）／ **素体** ＝ スィドと同じ数値・特性なし。");
        Console.WriteLine();

        Console.WriteLine("## 表A. 主表 —— ガルドの席にスィド・ベニを中央へ（§4.1・**測る前に固定**）");
        Console.WriteLine();
        Console.WriteLine("**元** ＝ `compare` の行のまま（ガルドあり・第195期のスィド）／ **元'** ＝ ガルドあり・ベニ中央（参考・R303）。"
                          + "穴 ＝ 元 − 版。`倒れ` はガルドの席のスィドが倒れた戦の割合、`≤2T` は全戦のうち2ターン目までに倒れた割合。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 中央 | 元 | 元' | 195 | **S** | S+反 | S+HP | 素体 | 穴 S ／ S+反 ／ S+HP | 倒れ 195 ／ S ／ S+反 ／ S+HP | ≤2T 195 ／ S ／ S+反 ／ S+HP | S の波別 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|---|---|---|---|");
        var sum = new Dictionary<string, (double Dead, double Early, int N, double W)>();
        var stallNote = new List<string>();
        foreach (var (name, f) in MainRows())
        {
            Formation bc = BeniCenter(f);
            M mo2 = Measure(BeniCenter(Apply(f, Sid195)));
            double o = Mean25(Rates(Apply(f, Sid195))), o2 = Mean25(mo2.W);
            stallNote.Add(name + " " + mo2.Stall.Skip(1).Sum() + " / 800");
            var m = new Dictionary<string, M> { ["195"] = Measure(GaldTo(bc, Sid195)) };
            foreach (var (tag, d) in Versions196) m[tag] = Measure(GaldTo(bc, d));
            foreach (var (k, v) in m)
            {
                var s = sum.GetValueOrDefault(k);
                sum[k] = (s.Dead + v.SidDeaths, s.Early + v.SidEarly, s.N + v.N, s.W + Mean25(v.W));
            }
            string W(string k) => Mean25(m[k].W).ToString("F1");
            string Dd(string k) => P(m[k].SidDeaths, m[k].N);
            string Ee(string k) => P(m[k].SidEarly, m[k].N);
            Console.WriteLine("| " + name + " | " + (Has(f, "beni") ? "ベニ ↔ " + f[2]!.Name : f[2]!.Name + " → ベニ") + " | " + o.ToString("F1") + " | " + o2.ToString("F1") + " | "
                              + W("195") + " | **" + W("S") + "** | " + W("S+反") + " | " + W("S+HP") + " | " + W("素体") + " | "
                              + D(o - Mean25(m["S"].W)) + " ／ " + D(o - Mean25(m["S+反"].W)) + " ／ " + D(o - Mean25(m["S+HP"].W)) + " | "
                              + Dd("195") + " ／ " + Dd("S") + " ／ " + Dd("S+反") + " ／ " + Dd("S+HP") + " | "
                              + Ee("195") + " ／ " + Ee("S") + " ／ " + Ee("S+反") + " ／ " + Ee("S+HP") + " | " + Cells(m["S"].W) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("7 行の平均: " + string.Join(" ／ ", new[] { "195", "S", "S+反", "S+HP", "素体" }.Where(sum.ContainsKey)
            .Select(k => k + " 勝率 " + (sum[k].W / 7).ToString("F1") + "・倒れ " + P(sum[k].Dead, sum[k].N) + "・≤2T " + P(sum[k].Early, sum[k].N))));
        Console.WriteLine();
        Console.WriteLine("元' の 30 ターン上限（膠着・第2〜5波）: " + string.Join(" ／ ", stallNote));
        Console.WriteLine();

        Console.WriteLine("## 表B. 参考 —— 第195期の形のまま（ベニを動かさない・§4.2）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 元 | 195 | **S** | S−195 | 倒れ 195 → S | ≤2T 195 → S | 減らした量/戦 195 → S | 新しい印/戦（吐き） 195 → S |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|---|---|---|");
        foreach (var (name, f) in MainRows())
        {
            M a = Measure(GaldTo(f, Sid195)), b = Measure(GaldTo(f, SidNew));
            double o = Mean25(Rates(Apply(f, Sid195)));
            Console.WriteLine("| " + name + " | " + o.ToString("F1") + " | " + Mean25(a.W).ToString("F1") + " | **" + Mean25(b.W).ToString("F1") + "** | " + D(Mean25(b.W) - Mean25(a.W)) + " | "
                              + P(a.SidDeaths, a.N) + " → " + P(b.SidDeaths, b.N) + " | " + P(a.SidEarly, a.N) + " → " + P(b.SidEarly, b.N) + " | "
                              + Per(a.Cut, a.N) + " → " + Per(b.Cut, b.N) + " | " + Per(a.SpewMarks, a.N, "F2") + " → " + Per(b.SpewMarks, b.N, "F2") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表B'. S のスィドが何で削られたか（第195期の形 → ベニ中央・1戦あたり・害の帳簿）");
        Console.WriteLine();
        var rts = Enumerable.Range(0, DamageRoutes.Names.Length).ToList();
        Console.WriteLine("| 行 | 倒れ | 受けた総量 | " + string.Join(" | ", rts.Select(i => DamageRoutes.Names[i])) + " | 隊の被ダメ |");
        Console.WriteLine("|---|---|---|" + string.Concat(rts.Select(_ => "---|")) + "---|");
        foreach (var (name, f) in MainRows())
        {
            M a = Measure(GaldTo(f, SidNew)), b = Measure(GaldTo(BeniCenter(f), SidNew));
            Console.WriteLine("| " + name + " | " + P(a.SidDeaths, a.N) + " → " + P(b.SidDeaths, b.N) + " | " + Per(a.SidTaken, a.N) + " → " + Per(b.SidTaken, b.N) + " | "
                              + string.Join(" | ", rts.Select(i => Per(a.SidHarm[i], a.N) + " → " + Per(b.SidHarm[i], b.N))) + " | "
                              + Per(a.TeamTakenFoe, a.N, "F0") + " → " + Per(b.TeamTakenFoe, b.N, "F0") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表C. `compare` のスィドの在席行（§4.3・規定の版の差分）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 195 | **S** | S−195 | 195 の波別 | S の波別 |");
        Console.WriteLine("|---|--:|--:|--:|---|---|");
        foreach (var (name, f) in CompareBuilds())
        {
            if (!Has(f, "sid")) continue;
            double[] a = Rates(Apply(f, Sid195)), b = Rates(f);
            Console.WriteLine("| " + name + " | " + Mean25(a).ToString("F1") + " | **" + Mean25(b).ToString("F1") + "** | " + D(Mean25(b) - Mean25(a)) + " | " + Cells(a) + " | " + Cells(b) + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ledger196 —— 台本（verbose）から
    // =================================================================================

    sealed class L196
    {
        public int N;
        public double[] MarkedAt = new double[7]; public int[] ReachedAt = new int[7];   // 1..6 ターン目の終わりに生きていて印のある敵
        public double AllTurnSum; public int AllN;
        public double SpewActs, SpewOn, Cut, SidDead, SidEarly, SidTurnSum, BeniHeal, BeniHealDead;
        public int BeniDeadN;
    }

    static L196 LedgerV(Formation f)
    {
        var players = f.Occupied().ToList();
        int sid = players.FindIndex(o => o.Def.Id == "sid");
        int beni = players.FindIndex(o => o.Def.Id == "beni");
        var l = new L196();
        object gate = new();
        for (int st = 1; st < 5; st++)
        {
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            int nFoe = enemy.Occupied().Count();
            Parallel.For(0, Seeds, seed =>
            {
                BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true);
                bool IsFoe(int id) => id >= players.Count;   // 敵の編成と召喚（召喚は敵陣が呼ぶ）
                var marked = new HashSet<int>(); var dead = new HashSet<int>();
                var byTurn = new int[7]; int allTurn = 0, sidDeath = 0; long heal = 0, healBefore = 0;
                int turn = 1;
                void Close(int upto)
                {
                    for (; turn < upto && turn <= 6; turn++) byTurn[turn] = marked.Count(x => !dead.Contains(x));
                }
                foreach (BattleEvent e in r.Events)
                {
                    if (e.Turn > turn) Close(e.Turn);
                    if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Numbed && e.TargetId is int t && IsFoe(t))
                    {
                        marked.Add(t);
                        if (allTurn == 0)
                        {
                            // 生きている敵（開幕の敵のうち死んでいない者・召喚は数えない）が全員印つきか
                            bool all = Enumerable.Range(players.Count, nFoe).All(x => dead.Contains(x) || marked.Contains(x));
                            if (all) allTurn = e.Turn;
                        }
                    }
                    if (e.Kind == BattleEventKind.Death && e.TargetId is int dt)
                    {
                        dead.Add(dt);
                        if (dt == sid && sidDeath == 0) { sidDeath = e.Turn; healBefore = heal; }
                    }
                    if (e.Kind == BattleEventKind.Heal && e.TargetId == sid && beni >= 0 && e.ActorId == beni) heal += e.Amount;
                }
                Close(7);
                r.TallyByUnit.TryGetValue("sid", out UnitTally? s);
                long cut = r.TallyByUnit.Where(kv => !players.Any(o => o.Def.Id == kv.Key)).Sum(kv => kv.Value.NumbedCut);
                lock (gate)
                {
                    l.N++;
                    for (int k = 1; k <= 6; k++) if (r.Turns >= k) { l.MarkedAt[k] += byTurn[k]; l.ReachedAt[k]++; }
                    if (allTurn > 0) { l.AllTurnSum += allTurn; l.AllN++; }
                    if (s is not null) { l.SpewActs += s.SpewActs; l.SpewOn += s.SpewOnMarked; }
                    l.Cut += cut;
                    if (sidDeath > 0) { l.SidDead++; l.SidTurnSum += sidDeath; if (sidDeath <= 2) l.SidEarly++; l.BeniHealDead += healBefore; l.BeniDeadN++; }
                    l.BeniHeal += heal;
                }
            });
        }
        return l;
    }

    static void Ledger196()
    {
        Console.WriteLine("# 第196期 `sid ledger196` —— 帳簿（1戦あたり・第2〜5波・seed 0..199・台本を読む）");
        Console.WriteLine();
        Console.WriteLine("`印 nT` は n ターン目の終わりに生きていて印のある敵の数（そのターンまで続いた戦の平均）。`全員` は開幕の敵が（倒れた者を除いて）全員印つきになったターン"
                          + "（届いた戦だけ・括弧は届いた割合）。`既に印` は吐いた相手が既に印を持っていた割合。`ベニの回復` はベニの反転でスィドが癒えた量"
                          + "（全戦の平均 ／ 倒れた戦で倒れる前まで）。");
        Console.WriteLine();
        var forms = new List<(string Name, Formation F)>();
        foreach (var (name, f) in MainRows())
        {
            Formation bc = BeniCenter(f);
            forms.Add((name + "・ベニ中央・195", GaldTo(bc, Sid195)));
            forms.Add((name + "・ベニ中央・**S**", GaldTo(bc, SidNew)));
            forms.Add((name + "・ベニ中央・S+反", GaldTo(bc, SidRe)));
            forms.Add((name + "・ベニ中央・S+HP", GaldTo(bc, SidHp)));
            forms.Add((name + "・第195期の形・195", GaldTo(f, Sid195)));
            forms.Add((name + "・第195期の形・S", GaldTo(f, SidNew)));
        }
        Console.WriteLine("| 行・形・版 | 印 1T | 2T | 3T | 4T | 5T | 全員 | 既に印 | 減らした量 | 倒れ | ≤2T | 倒れたT | ベニの回復 全戦 ／ 倒れる前 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (name, f) in forms)
        {
            L196 l = LedgerV(f);
            string At(int k) => l.ReachedAt[k] == 0 ? "—" : (l.MarkedAt[k] / l.ReachedAt[k]).ToString("F2");
            Console.WriteLine("| " + name + " | " + At(1) + " | " + At(2) + " | " + At(3) + " | " + At(4) + " | " + At(5) + " | "
                              + (l.AllN == 0 ? "—" : (l.AllTurnSum / l.AllN).ToString("F2") + "（" + (100.0 * l.AllN / l.N).ToString("F0") + "%）") + " | "
                              + (l.SpewActs == 0 ? "—" : (100.0 * l.SpewOn / l.SpewActs).ToString("F0") + "%") + " | " + (l.Cut / l.N).ToString("F1") + " | "
                              + (100.0 * l.SidDead / l.N).ToString("F1") + "% | " + (100.0 * l.SidEarly / l.N).ToString("F1") + "% | "
                              + (l.SidDead == 0 ? "—" : (l.SidTurnSum / l.SidDead).ToString("F2")) + " | "
                              + (l.BeniHeal / l.N).ToString("F1") + " ／ " + (l.BeniDeadN == 0 ? "—" : (l.BeniHealDead / l.BeniDeadN).ToString("F1")) + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // check196
    // =================================================================================

    static void Check196(string arg)
    {
        Console.WriteLine("# 第196期 `sid check196` —— 自己検査");
        Console.WriteLine();
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        if (!File.Exists(path)) { Console.WriteLine("**`" + path + "` が無い。止める。**"); return; }
        var want = ReadBalance(path);
        int diffA = 0, seenA = 0, diffB = 0, seenB = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            if (!want.TryGetValue(name, out double[]? w)) { diffB += 5; continue; }
            double[] r195 = Rates(Apply(f, Sid195));
            for (int i = 0; i < 5; i++) { seenB++; if (Math.Abs(r195[i] - w[i]) > 0.001) diffB++; }
            if (Has(f, "sid")) continue;
            double[] r = Rates(f);
            for (int i = 0; i < 5; i++) { seenA++; if (Math.Abs(r[i] - w[i]) > 0.001) diffA++; }
        }
        Console.WriteLine("- (a) スィドを含まない `compare` 行のセルが `" + path + "` とずれた数: **" + diffA + " / " + seenA + "**（0 が正・第195期の表を渡すこと）");
        Console.WriteLine("- (b) スィドを第195期の版（`SpewFixed`）に戻した `compare` の全セルが `" + path + "` とずれた数: **" + diffB + " / " + seenB + "**（0 が正）");

        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string b = Body(traits, "internal static UnitState? " + "PickTarget(", "internal static void " + "Act(")
                     + Body(traits, "class " + "SpewFixedTrait", "class " + "VenomHeavyTrait")
                     + Body(traits, "class " + "VenomHeavyTrait", "\n}\n");
            string pick = "Pick" + "One", roll = "Roll" + "(", shuf = "Shuffl" + "e";
            int Count(string x) => (b.Length - b.Replace(x, "").Length) / x.Length;
            Console.WriteLine("- (c) 散らしの選び方・`SpewFixed`・`VenomHeavy` の本体が `" + pick + "` / `Roll` / `" + shuf + "` を呼ぶ回数: **"
                              + (Count(pick) + Count(roll) + Count(shuf)) + "**（0 が正・走査した本体 " + b.Length + " 字。空なら止める）");
        }

        // (d) 散らしの規則: 吐いた相手が既に印を持っていたのは、生きている敵が全員印つきのときだけ
        {
            long spews = 0, onMarked = 0, bad = 0, notTop = 0;
            object gate = new();
            foreach (var (_, f0) in MainRows())
            {
                Formation f = GaldTo(BeniCenter(f0), SidNew);
                int np = f.Occupied().Count();
                for (int st = 1; st < 5; st++)
                {
                    Formation enemy = EnemyCatalog.Stages[st].Enemy;
                    int nf = enemy.Occupied().Count();
                    Parallel.For(0, 20, seed =>
                    {
                        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true);
                        var marked = new HashSet<int>(); var dead = new HashSet<int>();
                        var seen = Enumerable.Range(np, nf).ToHashSet();   // 開幕の敵（召喚は数えない）
                        long a = 0, o = 0, x = 0;
                        foreach (BattleEvent e in r.Events)
                        {
                            if (e.Kind == BattleEventKind.Death && e.TargetId is int d) dead.Add(d);
                            if (e.Kind == BattleEventKind.StatusGain && e.TargetId is int tt)
                            {
                                if (e.Text == StatusKeys.Poison && e.PoisonRoute == PoisonRoute.Spew)
                                {
                                    a++;
                                    if (marked.Contains(tt))
                                    {
                                        o++;
                                        // 生きていて印の無い敵が、この時点で1体でも見えていたら規則違反
                                        if (seen.Any(u => !dead.Contains(u) && !marked.Contains(u))) x++;
                                    }
                                }
                                else if (e.Text == StatusKeys.Numbed) marked.Add(tt);
                            }
                        }
                        lock (gate) { spews += a; onMarked += o; bad += x; }
                    });
                }
            }
            _ = notTop;
            Console.WriteLine("- (d) 主表の S（ベニ中央）× 第2〜5波 × seed 0..19 の台本: 吐き " + spews + " 回・うち既に印のある敵へ " + onMarked
                              + " 回——**印の無い生きた敵（開幕の敵）がいたのに印のある敵へ吐いた回数 " + bad + "**（0 が正）");
        }

        Console.WriteLine("- (e) `UnitCatalog.All` の保持者: `Spew` **" + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Spew)) + "** ／ `SpewFixed` **"
                          + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.SpewFixed)) + "** ／ `VenomHeavy` **" + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.VenomHeavy))
                          + "**（1 / 0 / 0 が正）。`SpewTrait.SpewSpreads` = " + SpewTrait.SpewSpreads + "・スィドの最大HP " + UnitCatalog.Sid.MaxHp + "（84 が正）");
        Console.WriteLine();
    }
}
