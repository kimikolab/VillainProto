using BattleCore;
using static Common;

// =====================================================================================
// funnel モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "funnel")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 funnel
// =====================================================================================

static class FunnelDiag
{
// funnel モード: 横流し（ヌキ）の最後の測定（第62 → 63 → 64期）。
//
// **第62期**は主判定の判定式が問いを測っていなかった（`reseat` の帯の峰は**台の配置感度**）。
// **第63期**は器具を直して測り直し、主判定 Q2'（測れる席で符号が割れる）が 3行中2行で落ちた。
// **第64期はこの駒を測る最後の期**——通らなければ残置で確定し、以後この駒の再測定は提案しない。
//
// 第64期で直したもの:
//   (a) **器具を両側にする**。測れる席 = 素体の5波平均が **40.0% 以上 95.0% 以下**
//       （第63期は床だけ塞いでいて、天井の行が「測れる席5」と数えられていた）
//   (b) **死蔵の定義を直す**。`Attacks == 0` は棘（カド）を死蔵に数えて**符号を逆に読ませる**
//       ——`UnitTally.AttackReads`（攻撃力を出力に変換した回数）で数える
//   (c) **試験行の選び方を結果を見る前に規則で固定する**（`funnel pick`）
//   (d) **主判定を「幅」に変える**。測れる席の帰属の最大 − 最小 ≥ 5.0pt（第46期の配置の採否閾値）
//
// **新しい機構は無い。** V3（弱体も流す）は残置のまま測らない（最後の1回に変数を2つ持ち込まない）。
// **`reseat` は新しい試験行にだけ 1回ずつ。** `CompareBuilds()` / `Stages` / `UnitCatalog.All` は不変。
//
//     dotnet run --project BattleSim -c Release 0 funnel [phase0|pick|scan|why|alt|絞り込み]
public static void Run(string[] args, int stageIndex)
{
    IReadOnlyList<EnemyCatalog.Stage> fnStages = EnemyCatalog.Stages;
    const int FnFrom = 1000, FnTo = 1400;  // **第64期の主帯。** 0..199 / 200..599 / 600..999 は既に使った
    const int AltFrom = 0, AltTo = 200;    // 再現帯
    const double FnFloor = 40.0;           // 器具の下側（第63期）
    const double FnCeil = 95.0;            // 器具の上側（**第64期に追加**）
    const double FnSplit = 1.5;            // 副判定「割れる」の閾値
    const double FnWidth = 5.0;            // **主判定 Q2'' の閾値**（第46期の配置の採否閾値と同じ数字）
    string fnMode = args.Length > 2 ? args[2] : "";

    FunnelRule FnV1 = new(Slowest: true, Both: false);   // 強化だけ（この期に測る唯一の版）

    UnitDef FnPlainDef = new()
    {
        Id = "nuki_plain", Name = "素体のヌキ", MaxHp = UnitCatalog.Nuki.MaxHp,
        Attack = UnitCatalog.Nuki.Attack, Speed = UnitCatalog.Nuki.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Nuki.Pattern
    };

    Formation FnBuild(params (int Slot, UnitDef Def)[] xs)
    {
        var g = new Formation();
        foreach ((int s, UnitDef d) in xs) g[s] = d;
        return g;
    }
    static Formation FnSwapS(Formation f, UnitDef from, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, from) ? to : d;
        return g;
    }
    Formation FnPlain(Formation f) => FnSwapS(f, UnitCatalog.Nuki, FnPlainDef);
    static Formation FnDrop(Formation f, UnitDef d)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef x) in f.Occupied())
            if (!ReferenceEquals(x, d)) g[slot] = x;
        return g;
    }
    static Formation FnSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Item2, UnitCatalog.Nuki))
                      .Select(o => o.Item2).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Nuki;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    // --- 試験行。**§1-6 の選定規則（`funnel pick`）で確定したもの。**
    //     規則: ablate 寄与最小の1枚をヌキに差し替え、**素体の5波平均が 40.0〜95.0% に入る席が
    //     3つ以上ある行**を候補とし、その中から**味方側の強化の総量が大きい順に3行**。
    (string Name, int Seat, Formation F)[] FnRows() => new (string, int, Formation)[]
    {
        // **席は選定規則が決めた形そのまま**（`ablate` 寄与最小の1枚を「その席で」ヌキに差し替え、
        // 他の4枚は元のスロットのまま）。**`reseat` は使わない**——120通りの入れ替えは
        // 他の4枚も動かすので、選定規則が保証した「測れる席が3つ以上」が壊れる（§4 の実測）。
        // **採る席＝差し替えた枠の席。選ぶ自由度がゼロなのが要点。**
        //
        // 1 `縛め収入型 (クグ×バン×ガン)`（強化 63.93 量/戦・1位）の **縛めのクグ（前3）** → ヌキ。
        //   第62・63期はここでバンを差し替えていた（クグを抜くと縛めが消えるから）が、
        //   第64期の規則は**寄与最小の1枚**と決めてあるのでクグを抜く。
        ("横流し×号令 (ヌキ×ガン)", 1,
            FnBuild((0, UnitCatalog.Gald), (1, UnitCatalog.Nuki), (2, UnitCatalog.Gan),
                    (3, UnitCatalog.Ban), (4, UnitCatalog.Dolga))),
        // 2 `死軸×ヒヨ (ゾト×火選り)`（38.58・2位）の **火選りのヒヨ（後1）** → ヌキ。
        ("横流し×死軸 (ヌキ×ゾト)", 3,
            FnBuild((0, UnitCatalog.Golm), (1, UnitCatalog.Zoto), (2, UnitCatalog.Vel),
                    (3, UnitCatalog.Nuki), (4, UnitCatalog.Rica))),
        // 3 `刻み (ノミ単騎)`（35.15・3位）の **刻みのノミ（中央）** → ヌキ。
        ("横流し×刻み台 (ヌキ×ガン×ゴルム)", 2,
            FnBuild((0, UnitCatalog.Golm), (1, UnitCatalog.Gan), (2, UnitCatalog.Nuki),
                    (3, UnitCatalog.Vel), (4, UnitCatalog.Dolga))),
        // 4 罠行（**新**）。宛先は §1-3 の**真の捨て場**＝継ぎ当てのノノ。
        //   `反撃改2 (ガン×カド)` の ヒサ → ヌキ ／ カド → ノノ。**手で組む**（勝率で採る行ではない）。
        //   ヌキ 前1 の隣は 中央ノノ(速6) と 後1ガン(速9) なので**宛先は必ずノノ**。
        ("横流し罠 (ヌキ×ノノ)", 0,
            FnBuild((0, UnitCatalog.Nuki), (1, UnitCatalog.Doha), (2, UnitCatalog.Lili),
                    (3, UnitCatalog.Gan), (4, UnitCatalog.Ban))),
        // 5 罠行（**第63期の版・カドが宛先**）。**死蔵の新旧の差を1行で見せるためだけに並べる。**
        //   第63期はこれを「100% 死蔵の罠」と読んだが、カドは棘で攻撃力を読むので捨て場ではない。
        ("横流し罠 旧 (ヌキ×カド)", 0,
            FnBuild((0, UnitCatalog.Nuki), (1, UnitCatalog.Doha), (2, UnitCatalog.Kado),
                    (3, UnitCatalog.Gan), (4, UnitCatalog.Ban))),
        // 6 **陰性対照**（自己検査 (b)）。`反撃改 (ドハ×カド)` の ヒサ（前1） → ヌキ。
        //   選定規則を満たす（測れる席5）が**味方側の強化が 0.00 量/戦**の行。
        //   横流しは量を1点も増やさず行き先だけを動かすので、**在庫が無ければ幅は 0 に潰れるはず**
        //   ——**Q2'' が「何でも通る判定式」ではないことの実測。採否には使わない。**
        ("横流し陰性対照 (ヌキ×強化0)", 0,
            FnBuild((0, UnitCatalog.Nuki), (1, UnitCatalog.Kado), (2, UnitCatalog.Doha),
                    (3, UnitCatalog.Nel), (4, UnitCatalog.Lili))),
        // 7 **Q5 用**（採否には使わない）。`逆しま (ネル×ウツ)` の 逃亡兵セロ（中央） → ヌキ。
        //   選定規則を満たす（測れる席4・強化 15.87）が**総量順で試験行1〜3 には入らない**行。
        //   **ウツ（逆しま）は正の `AtkBonus` で攻撃力が半減する**ので、
        //   ウツに向いた強化を横流しが**奪う席**はウツにとって得、**回す席**は損になるはず。
        ("横流し×逆しま (ヌキ×ウツ)", 2,
            FnBuild((0, UnitCatalog.Golm), (1, UnitCatalog.Gald), (2, UnitCatalog.Nuki),
                    (3, UnitCatalog.Nel), (4, UnitCatalog.Utsu))),
    };

    var fnAll = FnRows();
    var fnTargets = fnAll;
    string[] fnKnown = { "phase0", "pick", "reseat", "alt" };
    if (fnMode.Length > 0 && !fnKnown.Contains(fnMode))
        fnTargets = fnAll.Where(b => fnMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();
    string FnLabel(string id)
    {
        foreach (UnitDef d in UnitCatalog.All) if (d.Id == id) return d.Name;
        if (id == UnitCatalog.Nuki.Id) return UnitCatalog.Nuki.Name;
        if (id == FnPlainDef.Id) return FnPlainDef.Name;
        foreach (EnemyCatalog.Stage st in fnStages)
            foreach ((int _, UnitDef d) in st.Enemy.Occupied()) if (d.Id == id) return d.Name;
        return id;
    }
    static int FnDeg(int slot)
    {
        int n = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
            if (FormationRules.AreAdjacent(slot, i)) n++;
        return n;
    }
    static List<UnitDef> FnCands(Formation f)
    {
        int ns = -1;
        foreach ((int sl, UnitDef d) in f.Occupied())
            if (ReferenceEquals(d, UnitCatalog.Nuki)) ns = sl;
        if (ns < 0) return new List<UnitDef>();
        return f.Occupied()
            .Where(o => o.Item1 != ns
                        && !o.Item2.Traits.Contains(TraitId.Stoic)
                        && !o.Item2.Traits.Contains(TraitId.Funnel)
                        && FormationRules.AreAdjacent(ns, o.Item1))
            .Select(o => o.Item2).ToList();
    }
    static UnitDef? FnDest(Formation f)
    {
        var c = FnCands(f);
        if (c.Count == 0) return null;
        int want = c.Min(u => u.Speed);
        return c.First(u => u.Speed == want);
    }
    static string FnSeats(Formation f)
        => string.Join(" / ", f.Occupied().OrderBy(o => o.Item1)
            .Select(o => $"{FormationRules.SeatNames[o.Item1]}:{o.Item2.Name}"));
    // **真の捨て場**（第64期 §1-3）: 出力経路が攻撃力を1度も読まない駒。
    // 攻撃力を出力に変換する経路はロスター全体で4本（`PerformAttack` / 棘 / 仇討ち / 責め苦の追撃）。
    static bool FnIsDump(UnitDef d)
        => d.Actions is not null && d.Actions.Count > 0
           && d.Actions.All(a => a.Kind != ActionKind.Attack)
           && !d.Traits.Contains(TraitId.Thorns)
           && !d.Traits.Contains(TraitId.Avenge)
           && !d.Traits.Contains(TraitId.Torment);

    // ==========================================================================================
    // 計測器
    // ==========================================================================================
    FlStat MeasureFn(Formation f, Formation enemy, FunnelRule? rule, int from, int to)
    {
        var z = new FlStat();
        for (int seed = from; seed < to; seed++)
        {
            BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false, funnel: rule);
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.WhetTotal += r.WhetTotal;
            z.DullTotal += r.DullTotal;
            z.Taken += r.FunnelTaken;
            z.Dead += r.FunnelDead;
            z.DeadNew += r.FunnelDeadNew;
            z.DullTaken += r.FunnelDullTaken;
            z.DullDead += r.FunnelDullDead;
            z.Perverse += r.WhetToPerverse;
            z.Flips += r.WhetPerverseFlips;
            for (int i = 0; i < WhetRoutes.Count; i++)
            {
                z.Route[i] += r.WhetByRoute[i];
                z.TakenRoute[i] += r.FunnelByRoute[i];
            }
            foreach ((string k, int v) in r.FunnelTo)
                z.To[k] = z.To.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.FunnelFrom)
                z.DullFrom[k] = z.DullFrom.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string id, UnitTally t) in r.TallyByUnit)
            {
                if (!z.Got.ContainsKey(id)) { z.Got[id] = 0; z.Atk[id] = 0; z.Lost[id] = 0; }
                z.Got[id] += t.Whetted;
                z.Atk[id] += t.Attacks;
                z.Lost[id] += t.Dulled;
                if (t.Whetted > 0 && t.Attacks == 0) z.Hoard += t.Whetted;
                if (t.Whetted > 0 && t.AttackReads == 0) z.HoardNew += t.Whetted;
            }
        }
        double n = Math.Max(1, to - from);
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.WhetTotal /= n; z.DullTotal /= n; z.Taken /= n; z.Dead /= n; z.DeadNew /= n;
        z.DullTaken /= n; z.DullDead /= n; z.Hoard /= n; z.HoardNew /= n;
        z.Perverse /= n; z.Flips /= n;
        for (int i = 0; i < WhetRoutes.Count; i++) { z.Route[i] /= n; z.TakenRoute[i] /= n; }
        foreach (string k in z.To.Keys.ToList()) z.To[k] /= n;
        foreach (string k in z.DullFrom.Keys.ToList()) z.DullFrom[k] /= n;
        foreach (string k in z.Got.Keys.ToList()) { z.Got[k] /= n; z.Atk[k] /= n; z.Lost[k] /= n; }
        return z;
    }

    (double[] Wins, FlStat Z) FnAll(Formation f, FunnelRule? rule, int from, int to)
    {
        var wins = new double[fnStages.Count];
        var acc = new FlStat();
        for (int w = 0; w < fnStages.Count; w++)
        {
            FlStat z = MeasureFn(f, fnStages[w].Enemy, rule, from, to);
            wins[w] = z.Win;
            acc.Turns += z.Turns; acc.WhetTotal += z.WhetTotal; acc.DullTotal += z.DullTotal;
            acc.Taken += z.Taken; acc.Dead += z.Dead; acc.DeadNew += z.DeadNew;
            acc.DullTaken += z.DullTaken; acc.DullDead += z.DullDead;
            acc.Hoard += z.Hoard; acc.HoardNew += z.HoardNew;
            acc.Perverse += z.Perverse; acc.Flips += z.Flips;
            for (int i = 0; i < WhetRoutes.Count; i++)
            { acc.Route[i] += z.Route[i]; acc.TakenRoute[i] += z.TakenRoute[i]; }
            foreach ((string k, double v) in z.To)
                acc.To[k] = acc.To.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.DullFrom)
                acc.DullFrom[k] = acc.DullFrom.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.Got)
            {
                if (!acc.Got.ContainsKey(k)) { acc.Got[k] = 0; acc.Atk[k] = 0; acc.Lost[k] = 0; }
                acc.Got[k] += v; acc.Atk[k] += z.Atk[k]; acc.Lost[k] += z.Lost[k];
            }
        }
        double m = fnStages.Count;
        acc.Win = wins.Average();
        acc.Turns /= m; acc.WhetTotal /= m; acc.DullTotal /= m; acc.Taken /= m;
        acc.Dead /= m; acc.DeadNew /= m; acc.DullTaken /= m; acc.DullDead /= m;
        acc.Hoard /= m; acc.HoardNew /= m; acc.Perverse /= m; acc.Flips /= m;
        for (int i = 0; i < WhetRoutes.Count; i++) { acc.Route[i] /= m; acc.TakenRoute[i] /= m; }
        foreach (string k in acc.To.Keys.ToList()) acc.To[k] /= m;
        foreach (string k in acc.DullFrom.Keys.ToList()) acc.DullFrom[k] /= m;
        foreach (string k in acc.Got.Keys.ToList())
        { acc.Got[k] /= m; acc.Atk[k] /= m; acc.Lost[k] /= m; }
        return (wins, acc);
    }

    // 勝率だけを速く測る（スキャン用）。
    double FnWin(Formation f, int from, int to)
    {
        int wins = 0, n = 0;
        foreach (EnemyCatalog.Stage st in fnStages)
            for (int seed = from; seed < to; seed++)
            {
                n++;
                if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
            }
        return wins * 100.0 / Math.Max(1, n);
    }

    static string FnCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));
    static int FnInfo(double[] w)
        => Enumerable.Range(1, w.Length - 1).Count(i => w[i] > 0.0 && w[i] < 100.0);
    string FnTop(Dictionary<string, double> d, int n = 3)
    {
        var parts = d.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(n)
            .Select(x => $"{FnLabel(x.Key)} {x.Value:0.00}").ToList();
        return parts.Count == 0 ? "—" : string.Join(" / ", parts);
    }

    // ==========================================================================================
    // Phase 0（1-1〜1-5・1-8）。**席ごとの素体は測らない**（それは `funnel pick`）。
    // ==========================================================================================
    if (fnMode == "phase0")
    {
        Console.WriteLine("# 横流し Phase 0（第64期）—— 器具・死蔵・捨て場・在庫・読み手");
        Console.WriteLine();
        Console.WriteLine("## 0. 試験行（3行目は `funnel pick` の結果で確定する）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ヌキの席 | 並び |");
        Console.WriteLine("|---|---|---|");
        foreach (var b in fnAll)
            Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[b.Seat]} | {FnSeats(b.F)} |");
        Console.WriteLine();


        Console.WriteLine("## 1. 器具（**両側にした**）");
        Console.WriteLine();
        Console.WriteLine("    帰属(席) = 現行(席) − 同じ席の素体");
        Console.WriteLine($"    測れる席 = 素体の5波平均が **{FnFloor:0.0}% 以上 {FnCeil:0.0}% 以下**");
        Console.WriteLine($"    幅       = 測れる席の帰属の 最大 − 最小（**主判定 Q2''**・閾値 {FnWidth:0.0}pt）");
        Console.WriteLine();
        Console.WriteLine("**第63期は床（40% 未満）しか塞いでいなかった**ので、素体が 93〜100% の行が");
        Console.WriteLine("「測れる席5」と数えられ、帰属も版の差も定義上 0 に潰れていた（第63期 §11-4）。");
        Console.WriteLine();

        Console.WriteLine("## 2. 真の捨て場（**出力経路が攻撃力を1度も読まない駒**）");
        Console.WriteLine();
        Console.WriteLine("攻撃力を出力量に変換する経路はロスター全体で **4本**——");
        Console.WriteLine("`PerformAttack` ／ 棘（`ThornsTrait`）／ 仇討ち（`AvengeTrait`）／ 責め苦の追撃（`TormentTrait`）。");
        Console.WriteLine("**このどれも通らない駒だけが「配った強化が確実に無駄になる」宛先**である。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 行動 | 攻撃力を読む経路 | 捨て場か |");
        Console.WriteLine("|---|---|---|:-:|");
        foreach (UnitDef d in UnitCatalog.All)
        {
            bool noAtk = d.Actions is not null && d.Actions.Count > 0
                         && d.Actions.All(a => a.Kind != ActionKind.Attack);
            if (!noAtk) continue;   // 攻撃する駒は必ず読む
            var reads = new List<string>();
            if (d.Traits.Contains(TraitId.Thorns)) reads.Add("棘の反撃");
            if (d.Traits.Contains(TraitId.Avenge)) reads.Add("仇討ち");
            if (d.Traits.Contains(TraitId.Torment)) reads.Add("責め苦の追撃");
            Console.WriteLine($"| {d.Name} | {string.Join("/", d.Actions!.Select(a => a.Kind.ToString()))} | "
                + (reads.Count == 0 ? "**無し**" : string.Join(" / ", reads))
                + $" | {(FnIsDump(d) ? "**○**" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**捨て場は {UnitCatalog.All.Count(FnIsDump)} 枚**"
            + $"（{string.Join(" ・ ", UnitCatalog.All.Where(FnIsDump).Select(d => d.Name))}）。");
        Console.WriteLine("**棘鎧のカドは捨て場ではない**——`PerformAttack` を1度も通らないが、");
        Console.WriteLine("反撃量を自分の `CurrentAttack` で決めるので強化は満額効く（第63期 §11-2 の実測）。");
        Console.WriteLine();

        Console.WriteLine("## 3. 強化の在庫（味方側・行ごと）と、逆しま（ウツ）の同席");
        Console.WriteLine();
        Console.WriteLine($"seed {FnFrom}..{FnTo - 1}。**味方側が受け取った強化**（`UnitTally.Whetted` を味方の `Def.Id` で絞る）。");
        Console.WriteLine();
        var p0Builds = CompareBuilds();
        var p0Rows = new (string Name, double Supply, bool Utsu)[p0Builds.Length];
        Parallel.For(0, p0Builds.Length, i =>
        {
            (string nm, Formation f) = p0Builds[i];
            var mine = new HashSet<string>(f.Occupied().Select(o => o.Item2.Id));
            var (_, z) = FnAll(f, null, FnFrom, FnTo);
            double sup = z.Got.Where(kv => mine.Contains(kv.Key)).Sum(kv => kv.Value);
            p0Rows[i] = (nm, sup, f.Occupied().Any(o => ReferenceEquals(o.Item2, UnitCatalog.Utsu)));
        });
        Console.WriteLine("| 編成 | 味方の強化/戦 | ウツ同席 |");
        Console.WriteLine("|---|--:|:-:|");
        foreach (var r in p0Rows.OrderByDescending(x => x.Supply).Take(15))
            Console.WriteLine($"| {r.Name} | **{r.Supply:0.00}** | {(r.Utsu ? "**あり**" : "—")} |");
        Console.WriteLine();
        Console.WriteLine($"強化を1点でも受ける行は **{p0Rows.Count(x => x.Supply > 0)} / {p0Rows.Length}**。");
        Console.WriteLine();
        Console.WriteLine("### 逆しま（ウツ）を含む行（**Q5 の候補**・第63期 §11-3）");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 味方の強化/戦 |");
        Console.WriteLine("|---|--:|");
        foreach (var r in p0Rows.Where(x => x.Utsu).OrderByDescending(x => x.Supply))
            Console.WriteLine($"| {r.Name} | {r.Supply:0.00} |");
        Console.WriteLine();

        Console.WriteLine("## 4. 主判定19行との重なり");
        Console.WriteLine();
        int inPrimary = Baseline.PrimaryRows.Count(n => fnAll.Any(b => b.Name == n));
        Console.WriteLine($"- **主判定 {Baseline.PrimaryRows.Length} 行のうち試験行と同名の行: {inPrimary}**"
            + " → **Q8（歯止め）は構造的に発動しない。記録のみ。**");
        Console.WriteLine();
        return;
    }

    // ==========================================================================================
    // pick: 選定規則のスキャン（§1-6）。**結果を見る前に規則を固定してある。**
    // ==========================================================================================
    if (fnMode == "pick")
    {
        Console.WriteLine("# 試験行の選定（第64期 §1-6・**規則は結果を見る前に固定**）");
        Console.WriteLine();
        Console.WriteLine("> 候補 = ヌキを含まない61行のうち、`ablate` 寄与最小の1枚をヌキに差し替えたとき、");
        Console.WriteLine($"> **素体の5波平均が {FnFloor:0.0}〜{FnCeil:0.0}% に入る席が3つ以上ある行**");
        Console.WriteLine($"> （席ごとの素体は seed {FnFrom}..{FnTo - 1}。ヌキの席を5通り振り、他の4枚は固定）。");
        Console.WriteLine("> その中から**味方側の強化の総量が大きい順に3行**を試験行1〜3とする。");
        Console.WriteLine();
        var pkBuilds = CompareBuilds();
        var pk = new (string Name, string Swap, double Supply, double[] Plain, int Ok)[pkBuilds.Length];
        Parallel.For(0, pkBuilds.Length, i =>
        {
            (string nm, Formation f) = pkBuilds[i];
            var members = f.Occupied().Select(o => o.Item2).ToList();
            // **寄与最小の1枚** = 抜いた後の勝率がいちばん高い1枚（`ablate` と同じ定義）。
            UnitDef? worst = null;
            double top = double.NegativeInfinity;
            foreach (UnitDef d in members)
            {
                double w = FnWin(FnDrop(f, d), FnFrom, FnTo);
                if (w > top) { top = w; worst = d; }
            }
            Formation baseF = FnSwapS(f, worst!, UnitCatalog.Nuki);
            var plain = new double[FormationRules.PlayableSlotCount];
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
                plain[seat] = FnWin(FnPlain(FnSeat(baseF, seat)), FnFrom, FnTo);
            var mine = new HashSet<string>(f.Occupied().Select(o => o.Item2.Id));
            var (_, z) = FnAll(f, null, FnFrom, FnTo);
            double sup = z.Got.Where(kv => mine.Contains(kv.Key)).Sum(kv => kv.Value);
            pk[i] = (nm, worst!.Name, sup,
                     plain, plain.Count(x => x >= FnFloor && x <= FnCeil));
        });
        Console.WriteLine("## 候補（測れる席が3つ以上・強化の総量順）");
        Console.WriteLine();
        Console.WriteLine("| 編成 | ヌキに差し替えた枠 | 味方の強化/戦 | 前1 | 前3 | 中央 | 後1 | 後3 | 測れる席 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in pk.Where(x => x.Ok >= 3).OrderByDescending(x => x.Supply))
            Console.WriteLine($"| {r.Name} | {r.Swap} | **{r.Supply:0.00}** | "
                + string.Join(" | ", r.Plain.Select(x => x >= FnFloor && x <= FnCeil
                    ? $"{x:0.0}" : $"~~{x:0.0}~~"))
                + $" | **{r.Ok}** |");
        Console.WriteLine();
        Console.WriteLine($"候補は **{pk.Count(x => x.Ok >= 3)} / {pk.Length}** 行。");
        Console.WriteLine();
        Console.WriteLine("## 落ちた行のうち、強化の総量が上位のもの（**規則が効いていることの確認**）");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 味方の強化/戦 | 前1 | 前3 | 中央 | 後1 | 後3 | 測れる席 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in pk.Where(x => x.Ok < 3).OrderByDescending(x => x.Supply).Take(10))
            Console.WriteLine($"| {r.Name} | {r.Supply:0.00} | "
                + string.Join(" | ", r.Plain.Select(x => x >= FnFloor && x <= FnCeil
                    ? $"{x:0.0}" : $"~~{x:0.0}~~"))
                + $" | {r.Ok} |");
        Console.WriteLine();
        Console.WriteLine("`~~取り消し~~` は床（40% 未満）か天井（95% 超）。");
        Console.WriteLine();
        return;
    }

    // ==========================================================================================
    // reseat: 新しい試験行3本の席を決める（**1回だけ**・罠行と陰性対照は手で組んであるので対象外）。
    //   粗探索は seed 1000..1049（主帯の頭）、追試は seed 1000..1399。
    //   **採るのは「上位5通りのうち横流しがいちばん多い席」**（第53期の作法）——
    //   `reseat` は勝つ席を探す道具であって機構を活かす席を探す道具ではない（第50期）。
    // ==========================================================================================
    if (fnMode == "reseat")
    {
        Console.WriteLine("# 新しい試験行の配置（第64期・**1行につき1回**）");
        Console.WriteLine();
        Console.WriteLine($"粗探索 seed {FnFrom}..{FnFrom + 49}（120通り）→ 上位5通りを seed {FnFrom}..{FnTo - 1} で追試。");
        Console.WriteLine("**採るのは上位5のうち横流しがいちばん多い席**（第53期の作法。勝率だけの1位を採ると Q1 の分子が消える）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | ヌキの席 | 次数 | 宛先 | 粗探索 | 追試 | 横流し | 割合 | 並び |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|---|");
        foreach (var b in fnTargets.Where(x => !x.Name.Contains("罠") && !x.Name.Contains("対照")))
        {
            var members = b.F.Occupied().Select(o => o.Item2).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in SlotAssignments(members.Count))
            {
                var g = new Formation();
                for (int m = 0; m < members.Count; m++) g[assign[m]] = members[m];
                perms.Add(g);
            }
            var scan = new int[perms.Count];
            Parallel.For(0, perms.Count, i =>
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in fnStages)
                    for (int sd = FnFrom; sd < FnFrom + 50; sd++)
                        if (BattleEngine.Run(perms[i], st.Enemy, sd, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            });
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));
            Console.WriteLine($"| {b.Name} | 仮置き（{order.IndexOf(curIdx) + 1}/{perms.Count}位） | "
                + $"{FormationRules.SeatNames[b.Seat]} | {FnDeg(b.Seat)} | {FnDest(b.F)?.Name ?? "—"} | "
                + $"{scan[curIdx] * 100.0 / (fnStages.Count * 50):0.0}% | — | — | — | {FnSeats(b.F)} |");
            for (int r = 0; r < 5; r++)
            {
                Formation f = perms[order[r]];
                int ns = -1;
                foreach ((int sl, UnitDef d) in f.Occupied())
                    if (ReferenceEquals(d, UnitCatalog.Nuki)) ns = sl;
                var (w, z) = FnAll(f, FnV1, FnFrom, FnTo);
                Console.WriteLine($"| {b.Name} | 粗{r + 1}位 | {FormationRules.SeatNames[ns]} | {FnDeg(ns)} "
                    + $"| {FnDest(f)?.Name ?? "**—**"} | {scan[order[r]] * 100.0 / (fnStages.Count * 50):0.0}% "
                    + $"| {w.Average():0.0}% | {z.Taken:0.00} "
                    + $"| **{(z.WhetTotal > 0 ? z.Taken * 100 / z.WhetTotal : 0):0.0}%** | {FnSeats(f)} |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        return;
    }

    // ==========================================================================================
    // 主表: 席ごとの帰属と幅（**Q2'' の器具**）
    // ==========================================================================================
    int band0 = fnMode == "alt" ? AltFrom : FnFrom;
    int band1 = fnMode == "alt" ? AltTo : FnTo;

    Console.WriteLine("# 横流しの最後の測定（funnel・第64期）");
    Console.WriteLine();
    Console.WriteLine($"seed {band0}..{band1 - 1}。**docs/ には置かない。**");
    Console.WriteLine();
    Console.WriteLine("    帰属(席) = 現行(席) − 同じ席の素体");
    Console.WriteLine($"    測れる席 = 素体の5波平均が {FnFloor:0.0}% 以上 {FnCeil:0.0}% 以下（**両側**）");
    Console.WriteLine($"    幅（Q2''・主判定） = 測れる席の帰属の 最大 − 最小 ≥ {FnWidth:0.0}pt");
    Console.WriteLine($"    符号（Q2副・記録のみ） = 測れる席に +{FnSplit:0.0} 以上と −{FnSplit:0.0} 以下が両方ある");
    Console.WriteLine();

    Console.WriteLine("## 1. 席ごとの帰属");
    Console.WriteLine();
    Console.WriteLine("| 行 | 席 | 次数 | 宛先 | 素体 | V1 | **帰属** | 測 |");
    Console.WriteLine("|---|---|--:|---|--:|--:|--:|:-:|");
    var fnAttr = new Dictionary<string, List<(int Seat, double A, bool Ok)>>();
    foreach (var b in fnTargets)
    {
        var list = new List<(int, double, bool)>();
        for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
        {
            Formation g = FnSeat(b.F, seat);
            var (wp, _) = FnAll(FnPlain(g), null, band0, band1);
            var (w1, _) = FnAll(g, FnV1, band0, band1);
            double p = wp.Average(), a = w1.Average() - p;
            bool ok = p >= FnFloor && p <= FnCeil;
            list.Add((seat, a, ok));
            Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]}"
                + (seat == b.Seat ? "**◀採用**" : "") + $" | {FnDeg(seat)} "
                + $"| {FnDest(g)?.Name ?? "**—**"} | {p:0.0}% | {w1.Average():0.0}% "
                + $"| **{a:+0.0;-0.0;0.0}** | "
                + (ok ? "○" : p < FnFloor ? "**床**" : "**天井**") + " |");
            Console.Out.Flush();
        }
        fnAttr[b.Name] = list;
    }
    Console.WriteLine();

    Console.WriteLine("## 2. Q2''（幅・**主判定**）と Q2副（符号）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 測れる席 | 帰属 | **幅** | **Q2''** | 符号 | Q2副 |");
    Console.WriteLine("|---|--:|---|--:|:-:|---|:-:|");
    foreach ((string nm, var list) in fnAttr)
    {
        var ok = list.Where(x => x.Ok).ToList();
        double width = ok.Count == 0 ? 0 : ok.Max(x => x.A) - ok.Min(x => x.A);
        bool split = ok.Any(x => x.A >= FnSplit) && ok.Any(x => x.A <= -FnSplit);
        string f = ok.Count == 0 ? "—" : string.Join(" / ", ok.Select(x => $"{x.A:+0.0;-0.0;0.0}"));
        Console.WriteLine($"| {nm} | {ok.Count} | {f} | **{width:0.0}pt** | "
            + (ok.Count < 2 ? "判定不能" : width >= FnWidth ? "**○**" : "×")
            + $" | {(split ? "割れる" : "割れない")} | "
            + (ok.Count < 2 ? "判定不能" : split ? "○" : "×") + " |");
    }
    Console.WriteLine();
    if (fnMode == "alt") return;

    Console.WriteLine("## 3. 採った席の中身（**Q1 / Q4 / Q6**）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 情報 | 強化総量 | 横流し | 割合 | 死蔵(旧) | **死蔵(新)** |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var b in fnTargets)
    {
        Formation g = FnSeat(b.F, b.Seat);
        foreach ((string nm, FunnelRule? rule, Formation f) in new (string, FunnelRule?, Formation)[]
                 { ("V1", FnV1, g), ("素体", null, FnPlain(g)) })
        {
            var (w, z) = FnAll(f, rule, FnFrom, FnTo);
            Console.WriteLine($"| {b.Name} | {nm} |{FnCells(w)} {w.Average():0.0}% | {FnInfo(w)} "
                + $"| {z.WhetTotal:0.00} | {z.Taken:0.00} "
                + $"| {(z.WhetTotal > 0 ? z.Taken * 100 / z.WhetTotal : 0):0.0}% "
                + $"| {z.Dead:0.00} | **{z.DeadNew:0.00}** |");
            Console.Out.Flush();
        }
    }
    Console.WriteLine();
    Console.WriteLine("> **死蔵(新)** は宛先が `AttackReads == 0`（攻撃力を出力に1度も変換しなかった）だった量。");
    Console.WriteLine("> **死蔵(旧)** は `Attacks == 0`（`PerformAttack` を通らなかった）で、棘のような反応型を数え過ぎる。");
    Console.WriteLine();

    Console.WriteLine("### 回した先と、取り上げた相手（採った席）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 取り上げた相手 | 回した先 | 逆しまへ | 符号反転 |");
    Console.WriteLine("|---|---|---|--:|--:|");
    foreach (var b in fnTargets)
    {
        var (_, z) = FnAll(FnSeat(b.F, b.Seat), FnV1, FnFrom, FnTo);
        Console.WriteLine($"| {b.Name} | {FnTop(z.DullFrom)} | {FnTop(z.To)} "
            + $"| {z.Perverse:0.00} | {z.Flips:0.000} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}
}
