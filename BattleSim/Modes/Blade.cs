using BattleCore;
using static Common;

// =====================================================================================
// blade モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "blade")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 blade
// =====================================================================================

static class BladeDiag
{
// blade モード（第75期）: **薄刃の払い方。傷軸の代金の最後の大物。**
//
// 第74期の3段分解の残りは +5.4pt で、その 59%（−3.2）が**キリの薄刃 1 枚**に乗っている。
// 薄刃（`ThinBladeTrait.ModifyAttack => 1`）は**打点という一番高い通貨を、常時・無条件で払っている。**
//
// **代金を消さない。払い方を変える**（第74期のナタと同じ方針。あれは +5.15pt を回収した）。
// 版は `ThinBladeRule` を `Run` に引数で渡して切り替える（static のノブは置かない）。
// **既定は V0（常に 1）**なので、引数なしの `compare` は `docs/balance.md` と 305 セル 0 件で一致する。
//
// **台・標本・seed 帯・箱の作り方はすべて第73期 `wound` / 第74期 `wcost` の写し**（1文字も変えていない）。
// 変えると「値が動いたのは器具か機構か」が決まらなくなる。
//
//     dotnet run --project BattleSim -c Release 0 blade phase0       # 窓口の一覧と紙の計算（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 blade check        # 受け入れ基準1・2の陰性対照
//     dotnet run --project BattleSim -c Release 0 blade              # 理想台（A 帯 seed 0..199）
//     dotnet run --project BattleSim -c Release 0 blade alt          # 同じ表を B 帯（seed 200..399）
//     dotnet run --project BattleSim -c Release 0 blade draft [alt]  # ドラフト台 Pw（**主判定**。A 帯 / B 帯）
public static void Run(string[] args, int stageIndex)
{
    string blArg = args.Length > 2 ? args[2] : "";
    string blArg2 = args.Length > 3 ? args[3] : "";
    var blSw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> blStages = EnemyCatalog.Stages;
    int blW = blStages.Count;
    var blRoster = UnitCatalog.All.ToArray();
    int blRN = blRoster.Length;
    var blKeyOf = blRoster.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);
    const int BlKW = UnitTally.CarryWound;

    const int BlSeeds = 200;                 // 理想台の seed 本数（`compare` / 第73期 / 第74期と同じ）
    int blSeed0 = blArg == "alt" ? 200 : 0;

    // ---- 傷の5枚（**第74期 `wcost` の写し。順序も同じ**）--------------------------------------
    var blFive = new (UnitDef Def, string Role, string Minus, TraitId Cut)[]
    {
        (UnitCatalog.Kiri, "書き手",           "与ダメは常に1",              TraitId.ThinBlade),
        (UnitCatalog.Egu,  "維持読み（攻）",   "倒すと次の手番を失う",        TraitId.Overreach),
        (UnitCatalog.Nata, "消費読み",         "傷が2つ開くまで手番を捨てる", TraitId.Await),
        (UnitCatalog.Hari, "消費読み（防）",   "繕うたび敵の傷が1つ塞がる",   TraitId.Seal),
        (UnitCatalog.Nomi, "書き手＋維持読み", "執着",                        TraitId.Fixate),
    };
    var blIds = blFive.Select(x => x.Def.Id).ToArray();
    var blIdSet = blIds.ToHashSet();

    // ---- 変種（診断のローカル。`UnitCatalog` は1行も触らない）--------------------------------
    UnitDef BlPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    UnitDef BlNoMinus(int k)
    {
        var x = blFive[k];
        return new UnitDef
        {
            Id = x.Def.Id + "_m", Name = x.Def.Name + "・代金なし",
            MaxHp = x.Def.MaxHp, Attack = x.Def.Attack, Speed = x.Def.Speed,
            Traits = x.Def.Traits.Where(t => t != x.Cut).ToArray(), Pattern = x.Def.Pattern
        };
    }

    Formation BlSub(Formation f, IReadOnlyDictionary<string, UnitDef> map)
    {
        Formation g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (map.TryGetValue(d.Id, out UnitDef? nd)) g[slot] = nd;
        return g;
    }

    // ---- 版（**理想台とドラフト台で同じ並び**）-----------------------------------------------
    //  0 V0 現行（常に 1） / 1 V1 傷の無い相手には 1 / 2 V2 刻める攻撃だけ 1 / 3 V3 自分より遅い相手には 1
    //  4 薄刃なし（第74期の M1。**到達可能上限 +3.2 の出どころ**） / 5 キリを素体に（第73期の器具）
    //  6..9  エグ・ナタ・ハリ・ノミを素体に（V0 の規則）  ← Q4 の分母
    // 10..13 同（**V1 の規則**）                          ← Q4: V1 で寄与がどう動くか
    const int BlV = 14;
    string[] blVName = new string[BlV];
    blVName[0] = "**V0 現行（常に 1）**";
    blVName[1] = "**V1 傷の無い相手には 1**";
    blVName[2] = "V2 刻める攻撃だけ 1";
    blVName[3] = "V3 自分より遅い相手には 1";
    blVName[4] = "薄刃なし（第74期 M1・上限）";
    blVName[5] = "キリを素体に";
    for (int k = 1; k < 5; k++)
    {
        blVName[5 + k] = $"素体:{blFive[k].Def.Name}（V0）";
        blVName[9 + k] = $"素体:{blFive[k].Def.Name}（**V1**）";
    }

    // 版 → 規則。**`ThinBladeRule.Default` を読まずに全部を明示する**（第74期の作法）
    // ——こう書いておくと、既定がどちらであってもこの表の数字が動かない。
    ThinBladeRule BlRuleOf(int v) => v switch
    {
        1 => new ThinBladeRule(ThinBladeCost.Unwounded),
        2 => new ThinBladeRule(ThinBladeCost.Carving),
        3 => new ThinBladeRule(ThinBladeCost.Slower),
        >= 10 => new ThinBladeRule(ThinBladeCost.Unwounded),
        _ => new ThinBladeRule(ThinBladeCost.Always),
    };

    // 版 → 差し替え表（規則だけの版は差し替えなし）
    Dictionary<string, UnitDef>? BlMapOf(int v, ISet<string> ids)
    {
        var map = new Dictionary<string, UnitDef>();
        if (v == 4 && ids.Contains(blIds[0])) map[blIds[0]] = BlNoMinus(0);
        else if (v == 5 && ids.Contains(blIds[0])) map[blIds[0]] = BlPlain(blFive[0].Def);
        else if (v >= 6 && v <= 9 && ids.Contains(blIds[v - 5])) map[blIds[v - 5]] = BlPlain(blFive[v - 5].Def);
        else if (v >= 10 && ids.Contains(blIds[v - 9])) map[blIds[v - 9]] = BlPlain(blFive[v - 9].Def);
        return map.Count > 0 ? map : null;
    }

    // 版が「その編成で意味を持つか」。**持たない版は V0 と同じ数字になるので回さない**
    //（規則の版はキリが在席しないと1ビットも動かない——`check` の陰性対照がそれを実測する）。
    bool BlHas(int v, ISet<string> ids)
        => v == 0 ? true
         : v <= 5 ? ids.Contains(blIds[0])
         : v <= 9 ? ids.Contains(blIds[v - 5])
         : ids.Contains(blIds[0]) && ids.Contains(blIds[v - 9]);

    // ---- 統計の道具（第69〜74期の写し。**1文字も変えていない**）------------------------------
    string BlP1(double x) => double.IsNaN(x) ? "—" : (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    string BlP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");

    double BlSlope(IReadOnlyList<double> rate, IReadOnlyList<int> box)
    {
        var bk = new List<double>[4];
        for (int j = 0; j < 4; j++) bk[j] = new List<double>();
        for (int i = 0; i < rate.Count; i++) bk[Math.Min(3, box[i])].Add(rate[i]);
        double d10 = (bk[0].Count > 0 && bk[1].Count > 0) ? bk[1].Average() - bk[0].Average() : double.NaN;
        double d21 = (bk[1].Count > 0 && bk[2].Count > 0) ? bk[2].Average() - bk[1].Average() : double.NaN;
        return double.IsNaN(d10) ? d21 : double.IsNaN(d21) ? d10 : (d10 + d21) / 2;
    }
    double[] BlBoxMeans(IReadOnlyList<double> rate, IReadOnlyList<int> box)
    {
        var r = new double[4];
        for (int j = 0; j < 4; j++)
        {
            var xs = new List<double>();
            for (int i = 0; i < rate.Count; i++) if (Math.Min(3, box[i]) == j) xs.Add(rate[i]);
            r[j] = xs.Count > 0 ? xs.Average() : double.NaN;
        }
        return r;
    }
    static int BlNum(string t, string prefix)
    {
        int a = t.IndexOf(prefix, StringComparison.Ordinal);
        if (a < 0) return 0;
        a += prefix.Length;
        int b = a;
        while (b < t.Length && char.IsDigit(t[b])) b++;
        return b > a ? int.Parse(t[a..b]) : 0;
    }

    // 1戦ぶんの計数（**盤面を1ビットも触らない**。ログと `Events` を読むだけ）。第74期の写し ＋ キリの4本。
    // 添字: 0 裂きの書 / 1 刻みの書 / 2,3 抉りの発火・読んだ傷 / 4,5 なぞり / 6,7 断ち / 8,9 縫い /
    //       10 放棄T / 11 待ちT / 12 深追い / 13 戦数 / 14 在庫Σ / 15 ターンΣ / 16 味方の席の傷 /
    //       17..21 5枚の振り / **22 キリの攻撃回数（ログ）/ 23 キリの打点Σ / 24 素の打点で振った回数 /
    //       25 直前と同じ相手を殴った回数 / 26 キリの与ダメ** / BlC 勝数
    const int BlC = 27;
    void BlCount(BattleResult res, int pcount, UnitDef?[] ob, double[] a)
    {
        a[BlC] += res.PlayerWon ? 1 : 0;
        a[13]++;
        a[15] += res.Turns;
        for (int k = 0; k < 5; k++)
            if (ob[k] is UnitDef d && res.TallyByUnit.TryGetValue(d.Id, out UnitTally? tl))
            {
                a[17 + k] += tl.Attacks;
                if (k == 0) a[26] += tl.DamageToEnemy;
            }

        var stock = new Dictionary<int, int>();
        foreach (BattleEvent e in res.Events)
        {
            if (e.Kind != BattleEventKind.StatusSnapshot || e.Text != "傷") continue;
            if (e.TargetId is int tid && tid < pcount) { a[16] += e.Amount; continue; }
            stock[e.Turn] = stock.GetValueOrDefault(e.Turn) + e.Amount;
        }
        a[14] += stock.Values.Sum();

        string? nKiri = ob[0]?.Name, nEgu = ob[1]?.Name, nNata = ob[2]?.Name,
                nHari = ob[3]?.Name, nNomi = ob[4]?.Name;
        int turn = 0, lastIdle = -1, lastWait = -1;
        string prevTarget = "";
        foreach (LogLine l in res.Log)
        {
            string t = l.Text;
            if (l.Kind == LogKind.Turn) { turn++; continue; }
            // キリの一振り（**打点はログからしか読めない**——`atk` は tally に載らない）。
            if (nKiri != null && t.Contains(nKiri + " → ", StringComparison.Ordinal)
                              && t.Contains(" (攻撃 ", StringComparison.Ordinal))
            {
                int p = t.IndexOf(nKiri + " → ", StringComparison.Ordinal) + nKiri.Length + 3;
                int q = t.IndexOf(" (攻撃 ", StringComparison.Ordinal);
                string tgt = q > p ? t[p..q] : "";
                int amt = BlNum(t, " (攻撃 ");
                a[22]++; a[23] += amt;
                if (amt > 1) a[24]++;
                if (tgt.Length > 0 && tgt == prevTarget) a[25]++;
                prevTarget = tgt;
                continue;
            }
            if (nKiri != null && t.Contains(nKiri + " の刃が") && t.Contains("に傷を残した")) { a[0]++; continue; }
            if (nNomi != null && t.Contains(nNomi + " の鑿が")) { a[1]++; continue; }
            if (nEgu != null && t.Contains("の傷をこじ開ける") && t.Contains(nEgu + " が "))
            { a[2]++; a[3] += BlNum(t, "（傷 "); continue; }
            if (nNomi != null && t.Contains("の古い傷をなぞる") && t.Contains(nNomi + " が "))
            { a[4]++; a[5] += BlNum(t, "（傷 "); continue; }
            if (nNata != null && t.Contains("の傷をまとめて断つ") && t.Contains(nNata + " が "))
            { a[6]++; a[7] += BlNum(t, "（傷 "); continue; }
            if (nHari != null && t.Contains("の傷口から糸を引き") && t.Contains(nHari + " が "))
            { a[8]++; a[9] += BlNum(t, "（傷 "); continue; }
            if (nNata != null && t.Contains(nNata + " は閉じた肌に刃を下ろさない"))
            { if (turn != lastIdle) { a[10]++; lastIdle = turn; } continue; }
            if (nNata != null && t.Contains(nNata + " は傷がまだ浅いと刃を上げない"))
            { if (turn != lastWait) { a[11]++; lastWait = turn; } continue; }
            if (nEgu != null && t.Contains(nEgu + " は ") && t.Contains("の裂け目に踏み込みすぎた")) { a[12]++; continue; }
        }
    }

    // ---- ドラフト台の器具（第69〜74期の写し。**1文字も変えていない**）------------------------
    const int BlOfferSeed = 2_000_000;
    const int BlN = 11000, BlM = 8;
    const int BlStrong = 7;
    const int BlWeakPct = 60;
    int blBand = blArg2 == "alt" ? 200 : 0;

    var blWeakCache = new Dictionary<string, UnitDef>();
    UnitDef BlWeakOf(UnitDef d)
    {
        if (blWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * BlWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        blWeakCache[d.Id] = w;
        return w;
    }
    var blWeak = blStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = BlWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    // 規則 P（素朴・第70〜74期と同一）。**この期も Pw だけ**——傾き志向の S'w は傷の駒を締め出す（第73期）。
    UnitDef[] BlTeam(int i)
    {
        var rng = new Random(BlOfferSeed + i);
        var idx = new int[blRN];
        for (int k = 0; k < blRN; k++) idx[k] = k;
        int remain = blRN, strong = 0;
        var picked = new UnitDef[5];
        for (int r = 0; r < 5; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = blRoster[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= BlStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(blRoster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int[] BlSeats(UnitDef[] u)
    {
        var all5 = new[] { 0, 1, 2, 3, 4 };
        var front = all5.OrderByDescending(k => u[k].MaxHp)
                        .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        var rest = all5.Where(k => k != front[0] && k != front[1]).ToArray();
        var back = rest.OrderByDescending(k => u[k].Attack)
                       .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        int center = rest.Single(k => k != back[0] && k != back[1]);
        var r = new int[5];
        r[front[0]] = 0; r[front[1]] = 1; r[center] = 2; r[back[0]] = 3; r[back[1]] = 4;
        return r;
    }
    Formation BlDraftForm(UnitDef[] u, int[] seats, IReadOnlyDictionary<string, UnitDef>? map)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
            f[seats[k]] = (map is not null && map.TryGetValue(u[k].Id, out UnitDef? nd)) ? nd : u[k];
        return f;
    }
    int BlWoundCount(IEnumerable<UnitDef> team)
        => team.Count(x => blKeyOf.TryGetValue(x.Id, out int[]? k) && k.Contains(BlKW));

    var blAllRows = CompareBuilds();
    var blRows = blAllRows.Where(b => b.F.Occupied().Any(o => blIdSet.Contains(o.Def.Id))).ToArray();

    UnitDef?[] BlBoard(Formation g)
    {
        var arr = new UnitDef?[5];
        foreach ((int _, UnitDef d) in g.Occupied())
            for (int k = 0; k < 5; k++)
                if (d.Id == blIds[k] || d.Id.StartsWith(blIds[k] + "_", StringComparison.Ordinal)) arr[k] = d;
        return arr;
    }

    // =====================================================================================
    // phase0: 窓口の一覧と紙の計算（**戦闘を1回も回さない**。抽選だけは回す）
    // =====================================================================================
    if (blArg == "phase0")
    {
        Console.WriteLine("# 第75期 Phase 0 —— 薄刃の窓口と、測る前の紙の計算");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `dotnet run --project BattleSim -c Release 0 blade phase0`");
        Console.WriteLine();

        Console.WriteLine("## 0-1. 薄刃はどこで呼ばれるか（**call-site の全数**）");
        Console.WriteLine();
        Console.WriteLine("| 段 | 場所 | 通るか | 備考 |");
        Console.WriteLine("|---|---|:-:|---|");
        Console.WriteLine("| `ModifyAttack` の呼び出し | `UnitState.CurrentAttack` の getter **1箇所だけ** | ○ | `Def.Attack + AtkBonus` を作ってから `Traits` の順に通す |");
        Console.WriteLine("| 攻撃力 → 出力の経路 | `PerformAttack` | ○ | キリが通る唯一の出力経路 |");
        Console.WriteLine("| 同 | 棘（`ThornsTrait`）/ 仇討ち（`AvengeTrait`）/ 責め苦の追撃（`TormentTrait`） | × | **キリはどれも持たない**（第64期の4経路） |");
        Console.WriteLine("| `PerformAttack` の呼び出し元 | 行動順ループ（`BattleEngine.Run`） | ○ | `Actions` を持たない駒はここしか通らない |");
        Console.WriteLine("| 同 | `Actions` の `Attack`（`act.AttackPercent`） | × | **キリは `Actions` を持たない**（`attackPercent` は常に 100） |");
        Console.WriteLine("| 同 | 軋み（`DisplacedTrait` の `ctx.Interrupt`）/ 追い打ち（`PursuitTrait`） | × | **どちらも保持者自身しか振らせない**（ヨミ / ハギ） |");
        Console.WriteLine("| `CurrentAttack` を読む他の全員 | 駆り立ての選択・転嫁の流し先・`StatSnapshot`・墓守 ほか | ○ | **版によらず 1 のまま**（`ModifyAttack` は常に 1 を返す） |");
        Console.WriteLine();
        Console.WriteLine("> **払い直しは `PerformAttack` が `atk` を作った直後の1箇所**（止め＝第53期と同じ場所・同じ理由）。");
        Console.WriteLine("> `Trait.ModifyAttack` は `self` しか受け取らないので、対象を見る条件はそこには書けない。");
        Console.WriteLine();

        Console.WriteLine("## 0-2. 版（**測る前に固定**）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 条件（**真なら 1・`atk` を読まない**） | 狙い |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| **V0** | 常に真 | 基準（現行） |");
        Console.WriteLine("| **V1** | `target.Counter(Wound) <= 0` | **自分で開けた傷に自分で入る** |");
        Console.WriteLine("| **V2** | `!(破片なし かつ Hp <= atk)` | 刻める攻撃だけ 1。**条件の効きを分ける対照** |");
        Console.WriteLine("| **V3** | `target.Def.Speed < actor.Def.Speed` | 代金を「誰を殴るか」に紐づける |");
        Console.WriteLine();

        // ---- 0-3. 敵の速さ（V3 の紙の予測）----
        Console.WriteLine("## 0-3. 紙の計算 (1) —— V3 で素の打点が出る相手は何体か（**キリは速 "
                          + $"{UnitCatalog.Kiri.Speed}**）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵 | キリより速い（素の打点） | 同速（同・境界） | キリより遅い（1 のまま） |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        int blFast = 0, blTie = 0, blSlow = 0;
        for (int w = 0; w < blW; w++)
        {
            var es = blStages[w].Enemy.Occupied().Select(o => o.Def).ToArray();
            int f = es.Count(d => d.Speed > UnitCatalog.Kiri.Speed);
            int e2 = es.Count(d => d.Speed == UnitCatalog.Kiri.Speed);
            int s2 = es.Count(d => d.Speed < UnitCatalog.Kiri.Speed);
            blFast += f; blTie += e2; blSlow += s2;
            Console.WriteLine($"| {blStages[w].Name} | {es.Length} | {f} | {e2} | {s2} |");
        }
        Console.WriteLine($"| **計** | **{blFast + blTie + blSlow}** | **{blFast}** | **{blTie}** | **{blSlow}** |");
        Console.WriteLine();
        Console.WriteLine($"**素の打点が出る相手は {blFast + blTie} / {blFast + blTie + blSlow} 体**"
                          + $"（{100.0 * (blFast + blTie) / Math.Max(1, blFast + blTie + blSlow):F1}%）。"
                          + "**V3 は V1 より小さい**（P4）——ただし前列から割る順序があるので、この割合がそのまま出るわけではない。");
        Console.WriteLine();

        Console.WriteLine("## 0-4. 紙の計算 (2) —— V1 は1戦に何回「素の打点」になるか");
        Console.WriteLine();
        Console.WriteLine("キリは**速 12・単体**で、`SelectTargetCore` は前列が生きている限り前列の pool から選ぶ。");
        Console.WriteLine("**執着も断ちの選好も持たない**ので毎ターン `PickOne(pool)` を引き直す");
        Console.WriteLine("——つまり**同じ相手を殴り続ける保証は無く、pool の大きさぶんだけ散る。**");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵 | 前列（初期の pool） | 同じ相手を2手番続けて引く確率（1/pool） |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int w = 0; w < blW; w++)
        {
            var occ = blStages[w].Enemy.Occupied().ToArray();
            int front = occ.Count(o => FormationRules.RowOf(o.Slot) == Row.Front);
            int mid = occ.Count(o => FormationRules.RowOf(o.Slot) == Row.Mid);
            int pool = front > 0 ? front : mid > 0 ? mid : occ.Length;
            Console.WriteLine($"| {blStages[w].Name} | {occ.Length} | {front} | {100.0 / pool:F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine("> **一方で傷は消えない**（`TickStatuses` に何も足していない・第28期）ので、");
        Console.WriteLine("> **キリが一度でも触った相手は以後ずっと「傷のある相手」**である。");
        Console.WriteLine("> 素の打点になる割合は「同じ相手を続けて引く確率」ではなく");
        Console.WriteLine("> **「pool の中で既に触った相手の割合」**で、ターンが進むほど 1 に近づく");
        Console.WriteLine("> ——**P1（与ダメが大きく増える）の根拠はここ。**");
        Console.WriteLine();

        // ---- 0-5. 理想61行とキリ ----
        Console.WriteLine("## 0-5. 理想 61 行のうちキリを含む行と、主判定19行・歯止めの実効レンジ");
        Console.WriteLine();
        var blKiriRows = blAllRows.Where(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Kiri.Id)).ToArray();
        Console.WriteLine("| # | 行 | 傷の駒 | 枚数 |");
        Console.WriteLine("|--:|---|---|--:|");
        for (int r = 0; r < blKiriRows.Length; r++)
        {
            var mem = blKiriRows[r].F.Occupied().Select(o => o.Def).Where(d => blIdSet.Contains(d.Id)).ToArray();
            Console.WriteLine($"| {r + 1} | {blKiriRows[r].Name} | {string.Join("・", mem.Select(d => d.Name))} | {mem.Length} |");
        }
        Console.WriteLine();
        var blPrimary = new HashSet<string>(Baseline.PrimaryRows);
        var blPrimRows = blAllRows.Where(b => blPrimary.Contains(b.Name)).ToArray();
        var blPrimKiri = blPrimRows.Where(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Kiri.Id)).ToArray();
        Console.WriteLine("| 量 | 値 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine($"| 主判定 | {Baseline.PrimaryRows.Length} 行（`Baseline.PrimaryRows`） |");
        Console.WriteLine($"| **うちキリを含む** | **{blPrimKiri.Length} 行**（{string.Join(" / ", blPrimKiri.Select(b => b.Name))}） |");
        Console.WriteLine($"| 歯止め | 第五波 {Baseline.PrimaryFifthFloor:F1}% |");
        Console.WriteLine($"| **実効レンジ（自己検査 (c)）** | その 1 行が 0% まで落ちても平均は "
                          + $"**その行の第五波 ÷ {Baseline.PrimaryRows.Length}** しか下がらない |");
        Console.WriteLine();
        Console.WriteLine("> **第74期のナタ（主判定 0 行＝構造的に発動しない）とは違い、キリは主判定に 1 行ある。**");
        Console.WriteLine("> **歯止めは発動しうる**——ただし動かせる分母は 1/19 なので、実効レンジは主表で数字にして併記する。");
        Console.WriteLine();

        // ---- 0-6. ドラフト台の在席 ----
        Console.WriteLine("## 0-6. 紙の計算 (3) —— ドラフト台 Pw にキリは何回入るか（**抽選だけ。戦闘0回**）");
        Console.WriteLine();
        const int BlSim = 100000;
        var blSeat = new int[5];
        var blJoint = new int[5];
        int blAnyW = 0;
        for (int i = 0; i < BlSim; i++)
        {
            var ids = BlTeam(i).Select(x => x.Id).ToHashSet();
            bool any = false;
            for (int k = 0; k < 5; k++) if (ids.Contains(blIds[k])) { blSeat[k]++; any = true; }
            if (any) blAnyW++;
            if (ids.Contains(blIds[0]))
                for (int k = 1; k < 5; k++) if (ids.Contains(blIds[k])) blJoint[k]++;
        }
        Console.WriteLine("| 駒 | " + string.Join(" | ", blFive.Select(x => x.Def.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", 5)));
        Console.WriteLine($"| 在席（{BlSim:N0} 標本） | " + string.Join(" | ", blSeat.Select(c => $"{c}")) + " |");
        Console.WriteLine("| **キリと同席** | — | " + string.Join(" | ", Enumerable.Range(1, 4).Select(k => $"{blJoint[k]}")) + " |");
        Console.WriteLine();
        Console.WriteLine($"傷 ≥1 の標本 **{100.0 * blAnyW / BlSim:F1}%** / **キリ在席 {100.0 * blSeat[0] / BlSim:F1}%**"
                          + $"（11,000 標本なら約 {BlN * blSeat[0] / BlSim:N0} 件が V1 で動く）。");
        Console.WriteLine($"**Q4（軸の内側）の分母は 11,000 標本で約 "
                          + string.Join(" / ", Enumerable.Range(1, 4).Select(k => $"{blFive[k].Def.Name} {BlN * blJoint[k] / BlSim:N0}"))
                          + " 件**——**小さい。Q4 は採否に使わない**（指示書 §3）。");
        Console.WriteLine();

        Console.WriteLine("## 0-7. 到達可能な上限（**指示書 §0-1 の再掲**）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 値 | 出どころ |");
        Console.WriteLine("|---|--:|---|");
        Console.WriteLine("| ドラフト台 Pw の傷の枚数効果 | **−15.6** | 第71期 表E / 第72期 表D / 第73期 |");
        Console.WriteLine("| うち体と枠の機会費用 | −7.2 | 第73期 表F |");
        Console.WriteLine("| うち5枚のマイナス | −10.5 | 第74期 |");
        Console.WriteLine("| **うち薄刃 ＝ この期の上限** | **+3.2** | 第74期 M1 |");
        Console.WriteLine("| 主判定（Q1）の線 | +2.0 | 上限の **63%** |");
        Console.WriteLine();
        Console.WriteLine($"所要 {blSw.Elapsed.TotalSeconds:F1} 秒（**戦闘 0 回**）。");
        return;
    }
    // =====================================================================================
    // check: 受け入れ基準1・2の陰性対照
    // =====================================================================================
    if (blArg == "check")
    {
        Console.WriteLine("# 第75期 —— 陰性対照（受け入れ基準1・2 ＝ Q7）");
        Console.WriteLine();
        Console.WriteLine("## (1) 規則の形");
        Console.WriteLine();
        Console.WriteLine("| 確認 | 結果 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine($"| `UnitCatalog.All` の体数 | {blRN} |");
        Console.WriteLine($"| 傷キーの保持者 | {blRoster.Count(u => blKeyOf[u.Id].Contains(BlKW))} |");
        Console.WriteLine($"| `ThinBladeRule.Default` | `{ThinBladeRule.Default.Cost}`（＝ V0・常に 1） |");
        Console.WriteLine($"| キリが薄刃を持っていること | {(UnitCatalog.Kiri.Traits.Contains(TraitId.ThinBlade) ? "○" : "×")} |");
        Console.WriteLine($"| **薄刃の保持者**（ロスター全体） | {blRoster.Count(u => u.Traits.Contains(TraitId.ThinBlade))} 体"
                          + $"（{string.Join(" / ", blRoster.Where(u => u.Traits.Contains(TraitId.ThinBlade)).Select(u => u.Name))}） |");
        Console.WriteLine();

        Console.WriteLine("## (2) `compare` の全セル（**305 セル**）—— 引数なし／版ごと");
        Console.WriteLine();
        string? blRoot = Directory.GetCurrentDirectory();
        while (blRoot != null && !File.Exists(Path.Combine(blRoot, "docs", "balance.md")))
            blRoot = Path.GetDirectoryName(blRoot);

        double[][] BlAllRates(int v)
        {
            var res = new double[blAllRows.Length][];
            Parallel.For(0, blAllRows.Length, ri =>
            {
                var a = new double[blW];
                for (int w = 0; w < blW; w++)
                {
                    int win = 0;
                    for (int seed = 0; seed < BlSeeds; seed++)
                        if (v < 0
                            ? BattleEngine.Run(blAllRows[ri].F, blStages[w].Enemy, seed, verbose: false).PlayerWon
                            : BattleEngine.Run(blAllRows[ri].F, blStages[w].Enemy, seed, verbose: false,
                                               thinBlade: BlRuleOf(v)).PlayerWon) win++;
                    a[w] = win * 100.0 / BlSeeds;
                }
                res[ri] = a;
            });
            return res;
        }

        var blNow = BlAllRates(-1);
        if (blRoot == null) Console.WriteLine("`docs/` が見つからない（リポジトリの外から実行している）。");
        else
        {
            string[] bl = File.ReadAllLines(Path.Combine(blRoot, "docs", "balance.md"));
            int cells = 0, bad = 0, matched = 0;
            for (int ri = 0; ri < blAllRows.Length; ri++)
            {
                string? line = bl.FirstOrDefault(l => l.StartsWith("| " + blAllRows[ri].Name + " |", StringComparison.Ordinal));
                if (line == null) continue;
                matched++;
                var cols = line.Split('|').Select(c => c.Trim()).ToArray();
                for (int w = 0; w < blW; w++)
                {
                    cells++;
                    bool ok = double.TryParse(cols[2 + w].TrimEnd('%'), out double doc);
                    if (!ok || Math.Abs(doc - blNow[ri][w]) > 1e-9) bad++;
                }
            }
            Console.WriteLine("| 量 | 値 |");
            Console.WriteLine("|---|--:|");
            Console.WriteLine($"| 突き合わせた行 | {matched} / {blAllRows.Length} |");
            Console.WriteLine($"| 突き合わせたセル | {cells} |");
            Console.WriteLine($"| **`docs/balance.md` との食い違い（引数なし）** | **{bad} 件** |");
            Console.WriteLine();
        }

        Console.WriteLine("**版ごとの差分**（`docs/` ではなく引数なしの実測と突き合わせる）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 動いたセル | 動いた行 | **キリを含まない行で動いたもの** |");
        Console.WriteLine("|---|--:|---|--:|");
        for (int v = 0; v <= 3; v++)
        {
            var r = v == 0 ? blNow : BlAllRates(v);
            int diff = 0, badRow = 0;
            var moved = new List<string>();
            for (int ri = 0; ri < blAllRows.Length; ri++)
            {
                bool any = false;
                for (int w = 0; w < blW; w++)
                    if (Math.Abs(r[ri][w] - blNow[ri][w]) > 1e-9) { diff++; any = true; }
                if (!any) continue;
                moved.Add(blAllRows[ri].Name);
                if (!blAllRows[ri].F.Occupied().Any(o => o.Def.Id == UnitCatalog.Kiri.Id)) badRow++;
            }
            Console.WriteLine($"| {blVName[v]} | {diff} | {(moved.Count == 0 ? "**無し**" : string.Join(" / ", moved))} | **{badRow}** |");
        }
        Console.WriteLine();
        Console.WriteLine("**V0 の行が 0 セルであること**が「規則を足しただけでは盤面が動かない」の検算");
        Console.WriteLine("（`ThinBladeRule.Always` は `PerformAttack` の比較1つで抜ける）。");
        Console.WriteLine("**右端の列が全部 0 であること**が受け入れ基準2（Q7）。");
        Console.WriteLine();

        Console.WriteLine("## (3) 版が「この一振りの打点」だけを動かすこと（1戦の監査・50 seed × 5波・`裂き (キリ×エグ)`）");
        Console.WriteLine();
        {
            var row = blRows.FirstOrDefault(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Kiri.Id));
            if (row.F is not null)
            {
                Console.WriteLine("| 版 | キリの振/戦 | **素の打点で振った割合** | 与ダメ/戦 | **与ダメ/振** | 書込/戦 | 在庫/T | 決着T | 勝率 |");
                Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
                foreach (int v in new[] { 0, 1, 2, 3, 4, 5 })
                {
                    var ids = row.F.Occupied().Select(o => o.Def.Id).ToHashSet();
                    Dictionary<string, UnitDef>? map = BlMapOf(v, ids);
                    Formation f = map is null ? row.F : BlSub(row.F, map);
                    UnitDef?[] ob = BlBoard(f);
                    var a = new double[BlC + 1];
                    for (int w = 0; w < blW; w++)
                        for (int seed = 0; seed < 50; seed++)
                            BlCount(BattleEngine.Run(f, blStages[w].Enemy, seed, verbose: true,
                                                     thinBlade: BlRuleOf(v)), f.Count, ob, a);
                    double n = a[13], sw = a[22];
                    Console.WriteLine($"| {blVName[v]} | {sw / n:F2} | {(sw > 0 ? $"{100 * a[24] / sw:F1}%" : "—")} "
                                      + $"| {a[26] / n:F2} | **{a[26] / Math.Max(1, sw):F3}** | {a[0] / n:F2} "
                                      + $"| {a[14] / Math.Max(1, a[15]):F2} | {a[15] / n:F2} | {100 * a[BlC] / n:F1}% |");
                }
                Console.WriteLine();
                Console.WriteLine("**V0 の `与ダメ/振` が 1.000 であること**が薄刃が効いていることの検算");
                Console.WriteLine("（`キリを素体に` は攻12・特性なし＝上限側の対照）。");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {blSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // 主表（理想台）
    // =====================================================================================
    if (blArg.Length == 0 || blArg == "alt")
    {
        string blBandName = blArg == "alt" ? "B" : "A";

        var blVers = new (int V, Formation F, UnitDef?[] OnBoard)[blRows.Length][];
        for (int r = 0; r < blRows.Length; r++)
        {
            var ids = blRows[r].F.Occupied().Select(o => o.Def.Id).ToHashSet();
            var list = new List<(int, Formation, UnitDef?[])>();
            for (int v = 0; v < BlV; v++)
            {
                if (!BlHas(v, ids)) continue;
                Dictionary<string, UnitDef>? map = BlMapOf(v, ids);
                Formation f = map is null ? blRows[r].F : BlSub(blRows[r].F, map);
                list.Add((v, f, BlBoard(f)));
            }
            blVers[r] = list.ToArray();
        }

        var blAcc = new double[blRows.Length][][][];
        for (int r = 0; r < blRows.Length; r++)
        {
            blAcc[r] = new double[blVers[r].Length][][];
            for (int v = 0; v < blVers[r].Length; v++)
            {
                blAcc[r][v] = new double[blW][];
                for (int w = 0; w < blW; w++) blAcc[r][v][w] = new double[BlC + 1];
            }
        }

        Console.Error.Write($"{blBandName}帯 理想台: ");
        var blJobs = new List<(int R, int Vi)>();
        for (int r = 0; r < blRows.Length; r++)
            for (int v = 0; v < blVers[r].Length; v++) blJobs.Add((r, v));
        Parallel.ForEach(blJobs, job =>
        {
            (int r, int vi) = job;
            (int v, Formation f, UnitDef?[] ob) = blVers[r][vi];
            ThinBladeRule rule = BlRuleOf(v);
            int pcount = f.Count;
            for (int w = 0; w < blW; w++)
                for (int seed = blSeed0; seed < blSeed0 + BlSeeds; seed++)
                    BlCount(BattleEngine.Run(f, blStages[w].Enemy, seed, verbose: true, thinBlade: rule),
                            pcount, ob, blAcc[r][vi][w]);
            Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        // 61 行を版ごとに（歯止め・情報セル・傾きの分母）。**V0〜V3 の4版だけ。**
        var blBase = new double[4][][];
        for (int v = 0; v < 4; v++)
        {
            var arr = new double[blAllRows.Length][];
            int vv = v;
            Console.Error.Write($"{blBandName}帯 61行 V{vv}: ");
            Parallel.For(0, blAllRows.Length, ri =>
            {
                var a = new double[blW];
                for (int w = 0; w < blW; w++)
                {
                    int win = 0;
                    for (int seed = blSeed0; seed < blSeed0 + BlSeeds; seed++)
                        if (BattleEngine.Run(blAllRows[ri].F, blStages[w].Enemy, seed, verbose: false,
                                             thinBlade: BlRuleOf(vv)).PlayerWon) win++;
                    a[w] = win * 100.0 / BlSeeds;
                }
                arr[ri] = a;
                Console.Error.Write(".");
            });
            Console.Error.WriteLine();
            blBase[v] = arr;
        }

        int BlIdxOf(int r, int v) => Array.FindIndex(blVers[r], x => x.V == v);
        double BlRate(int r, int vi, int w) => blAcc[r][vi][w][BlC] * 100.0 / BlSeeds;
        double BlRate25(int r, int vi) => Enumerable.Range(1, blW - 1).Average(w => BlRate(r, vi, w));
        double[] BlSum(int r, int vi)
        {
            var a = new double[BlC + 1];
            for (int w = 0; w < blW; w++) for (int c = 0; c <= BlC; c++) a[c] += blAcc[r][vi][w][c];
            return a;
        }

        Console.WriteLine($"# 第75期 —— 薄刃の払い方（理想台・{blBandName} 帯 seed {blSeed0}..{blSeed0 + BlSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 blade{(blArg == "alt" ? " alt" : "")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"理想 {blAllRows.Length} 行のうち**傷の駒を含む {blRows.Length} 行**（行は選んでいない）× 版 "
                          + $"{blJobs.Count / (double)blRows.Length:F1}（平均）× {blW} 波 × seed {BlSeeds} 本。");
        Console.WriteLine("**台・標本・seed 帯・箱の作り方は第73期・第74期の写し**（1文字も変えていない）。");
        Console.WriteLine();

        int blVmis = 0, blAllyW = 0;
        for (int r = 0; r < blRows.Length; r++)
        {
            int bi = Array.FindIndex(blAllRows, b => b.Name == blRows[r].Name);
            for (int w = 0; w < blW; w++)
                if (Math.Abs(BlRate(r, 0, w) - blBase[0][bi][w]) > 1e-9) blVmis++;
            for (int v = 0; v < blVers[r].Length; v++) blAllyW += (int)BlSum(r, v)[16];
        }
        Console.WriteLine($"**検算: `verbose` 版と非 `verbose` 版の勝率の食い違い {blVmis} 件 / {blRows.Length * blW}**"
                          + "（イベントを積む処理が盤面を動かしていないこと）。");
        Console.WriteLine($"**陰性対照: 味方の席に載った傷 {blAllyW} 件**"
                          + $"（{blJobs.Count * blW * BlSeeds:N0} 戦のターン頭スナップショット全数）。");
        Console.WriteLine();

        Console.WriteLine("## 表A（理想側）—— 版 × 行");
        Console.WriteLine();
        Console.WriteLine("`Δ` は V0 との差（第2〜5波の平均勝率）。**キリを含む行だけが動く。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第2〜5波 | **Δ** | 5波 | キリの振/戦 | **素の打点%** | キリの与ダメ/戦 | 書込/戦 | 在庫/T | 決着T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int r = 0; r < blRows.Length; r++)
        {
            double b25 = BlRate25(r, 0);
            for (int vi = 0; vi < blVers[r].Length; vi++)
            {
                int v = blVers[r][vi].V;
                if (v > 5) continue;
                double[] a = BlSum(r, vi);
                double n = a[13], sw = a[22];
                Console.WriteLine($"| {(vi == 0 ? blRows[r].Name : "")} | {blVName[v]} | {BlRate25(r, vi):F2}% "
                                  + $"| {(v == 0 ? "—" : BlP2(BlRate25(r, vi) - b25))} "
                                  + $"| {Enumerable.Range(0, blW).Average(w => BlRate(r, vi, w)):F2}% "
                                  + $"| {sw / n:F2} | {(sw > 0 ? $"{100 * a[24] / sw:F1}%" : "—")} | {a[26] / n:F2} "
                                  + $"| {(a[0] + a[1]) / n:F2} | {a[14] / Math.Max(1, a[15]):F2} | {a[15] / n:F2} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 表B（理想側）—— 傷の枚数効果の傾き（**箱は元の5枚で決める**）");
        Console.WriteLine();
        var blBox = blAllRows.Select(b => Math.Min(3, BlWoundCount(b.F.Occupied().Select(o => o.Def)))).ToArray();
        double[] BlIdealRates(int v, int wave)
        {
            int src = v <= 3 ? v : 0;
            var rate = new double[blAllRows.Length];
            for (int i = 0; i < blAllRows.Length; i++)
                rate[i] = wave < 0 ? Enumerable.Range(1, blW - 1).Average(w => blBase[src][i][w]) : blBase[src][i][wave];
            if (v <= 3) return rate;
            for (int r = 0; r < blRows.Length; r++)
            {
                int vi = BlIdxOf(r, v);
                if (vi < 0) continue;
                int bi = Array.FindIndex(blAllRows, b => b.Name == blRows[r].Name);
                rate[bi] = wave < 0 ? BlRate25(r, vi) : BlRate(r, vi, wave);
            }
            return rate;
        }
        Console.WriteLine("| 版 | 0枚 | 1枚 | 2枚 | 第2波 | 第3波 | 第4波 | 第5波 | **集計（第2〜5波）** | Δ（V0比） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        double blSlope0 = double.NaN;
        var blIdealSlope = new double[BlV];
        for (int v = 0; v < BlV; v++) blIdealSlope[v] = double.NaN;
        for (int v = 0; v <= 5; v++)
        {
            var rate = BlIdealRates(v, -1);
            double[] bm = BlBoxMeans(rate, blBox);
            double sl = BlSlope(rate, blBox);
            blIdealSlope[v] = sl;
            if (v == 0) blSlope0 = sl;
            Console.WriteLine($"| {blVName[v]} | {bm[0]:F1}% | {bm[1]:F1}% | {bm[2]:F1}% "
                              + string.Concat(Enumerable.Range(1, blW - 1).Select(w => $"| {BlP1(BlSlope(BlIdealRates(v, w), blBox))} "))
                              + $"| **{BlP1(sl)}** | {(v == 0 ? "—" : BlP2(sl - blSlope0))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**箱の大きさ: 0枚 {blBox.Count(x => x == 0)} 行 / 1枚 {blBox.Count(x => x == 1)} 行 / "
                          + $"2枚 {blBox.Count(x => x == 2)} 行 / 3+ {blBox.Count(x => x == 3)} 行**"
                          + "（第72期 表D・第73期・第74期と一致）。**1枚の箱は1行しか無い。**");
        Console.WriteLine();

        Console.WriteLine("## Q4（理想側）—— 軸の内側の符号（**キリと同席する読み手だけ**）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 読み手 | 寄与（V0） | 寄与（V1） | **差** |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        int blQ4rows = 0;
        for (int r = 0; r < blRows.Length; r++)
            for (int k = 1; k < 5; k++)
            {
                int v0 = BlIdxOf(r, 5 + k), v1 = BlIdxOf(r, 9 + k);
                if (v0 < 0 || v1 < 0) continue;
                blQ4rows++;
                double c0 = BlRate25(r, BlIdxOf(r, 0)) - BlRate25(r, v0);
                double c1 = BlRate25(r, BlIdxOf(r, 1)) - BlRate25(r, v1);
                Console.WriteLine($"| {blRows[r].Name} | {blFive[k].Def.Name} | {BlP2(c0)} | {BlP2(c1)} | **{BlP2(c1 - c0)}** |");
            }
        if (blQ4rows == 0) Console.WriteLine("| — | — | — | — | — |");
        Console.WriteLine();
        Console.WriteLine("**寄与 ＝ 現行 − その駒を素体にした版**（第69期の標準器具）。");
        Console.WriteLine("**理想台でキリと同席する傷の読み手はエグだけ**——ナタ・ハリ・ノミとは 61 行のどこでも同席しない。");
        Console.WriteLine("**Q4 の本体はドラフト台の側。**");
        Console.WriteLine();

        Console.WriteLine("## Q5 / Q6 —— 情報セルと歯止め");
        Console.WriteLine();
        Console.WriteLine("| 版 | 傷8行の情報セル | 天井 | 床 | **61行の情報セル** | **主判定19行の第五波** | 歯止め 33.2% との余裕 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        var blPrimIdx = Baseline.PrimaryRows.Select(nm => Array.FindIndex(blAllRows, b => b.Name == nm))
                                            .Where(i => i >= 0).ToArray();
        for (int v = 0; v <= 3; v++)
        {
            int info = 0, ceil = 0, floor = 0;
            for (int r = 0; r < blRows.Length; r++)
            {
                int vi = BlIdxOf(r, v);
                if (vi < 0) vi = 0;
                for (int w = 1; w < blW; w++)
                {
                    double x = BlRate(r, vi, w);
                    if (x >= 100.0) ceil++; else if (x <= 0.0) floor++; else info++;
                }
            }
            int info61 = 0;
            for (int i = 0; i < blAllRows.Length; i++)
                for (int w = 1; w < blW; w++)
                    if (blBase[v][i][w] > 0.0 && blBase[v][i][w] < 100.0) info61++;
            double fifth = blPrimIdx.Select(i => blBase[v][i][blW - 1]).Average();
            Console.WriteLine($"| {blVName[v]} | **{info}** | {ceil} | {floor} | {info61} | **{fifth:F1}%** "
                              + $"| {BlP1(fifth - Baseline.PrimaryFifthFloor)} |");
        }
        Console.WriteLine();
        {
            var kiriPrim = blPrimIdx.Where(i => blAllRows[i].F.Occupied().Any(o => o.Def.Id == UnitCatalog.Kiri.Id)).ToArray();
            Console.WriteLine($"**主判定 {blPrimIdx.Length} 行のうちキリを含むのは {kiriPrim.Length} 行**"
                              + $"（{string.Join(" / ", kiriPrim.Select(i => blAllRows[i].Name))}）。");
            foreach (int i in kiriPrim)
                Console.WriteLine($"その行の第五波: " + string.Join(" / ", Enumerable.Range(0, 4).Select(v => $"V{v} {blBase[v][i][blW - 1]:F1}%"))
                                  + $"。**実効レンジ = 最大 {blBase[0][i][blW - 1] / blPrimIdx.Length:F2}pt**"
                                  + "（その行が 0% まで落ちた場合の平均の低下・自己検査 (c)）。");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {blSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // draft: ドラフト台 Pw（規則 P × 弱い波。**主判定**。第73期・第74期の写し）
    // =====================================================================================
    if (blArg == "draft")
    {
        string blDName = blBand == 0 ? "A" : "B";
        var blDWin = new int[BlN][];
        var blDBox = new int[BlN];
        var blDHas = new bool[BlN][];

        int blDone = 0;
        Console.Error.Write($"{blDName}帯 Pw: ");
        Parallel.For(0, BlN, i =>
        {
            UnitDef[] team = BlTeam(i);
            int[] seats = BlSeats(team);
            var ids = team.Select(x => x.Id).ToHashSet();
            var has = new bool[BlV];
            for (int v = 0; v < BlV; v++) has[v] = BlHas(v, ids);
            var win = new int[BlV * blW];
            for (int v = 0; v < BlV; v++)
            {
                if (!has[v]) continue;
                Formation f = BlDraftForm(team, seats, BlMapOf(v, ids));
                ThinBladeRule rule = BlRuleOf(v);
                for (int w = 0; w < blW; w++)
                    for (int seed = blBand; seed < blBand + BlM; seed++)
                        if (BattleEngine.Run(f, blWeak[w].Enemy, seed, verbose: false, thinBlade: rule).PlayerWon)
                            win[v * blW + w]++;
            }
            blDWin[i] = win; blDHas[i] = has;
            blDBox[i] = Math.Min(3, BlWoundCount(team));
            int c = Interlocked.Increment(ref blDone);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        double BlDRate(int i, int v, int w)
            => (blDHas[i][v] ? blDWin[i][v * blW + w] : blDWin[i][w]) * 100.0 / BlM;
        double[] BlDRates(int v, int wave)
            => Enumerable.Range(0, BlN).Select(i => wave < 0
                ? Enumerable.Range(1, blW - 1).Average(w => BlDRate(i, v, w))
                : BlDRate(i, v, wave)).ToArray();

        Console.WriteLine($"# 第75期 —— 薄刃の払い方・ドラフト台 Pw（{blDName} 帯 seed {blBand}..{blBand + BlM - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 blade draft{(blBand == 0 ? "" : " alt")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"標本 **{BlN:N0}** × 版 {BlV} × {blW} 波 × seed **{BlM}** 本（規則 P・弱い波 {BlWeakPct}%）。");
        Console.WriteLine("**箱（傷の枚数）は必ず元の5枚で決める。判定は第2〜5波。**");
        Console.WriteLine();
        Console.WriteLine($"箱: **0枚 {blDBox.Count(x => x == 0):N0} / 1枚 {blDBox.Count(x => x == 1):N0} / "
                          + $"2枚 {blDBox.Count(x => x == 2):N0} / 3+ {blDBox.Count(x => x == 3):N0}**"
                          + "（第73期の 5299 / 4706 / 945 / 50 と突き合わせる）");
        Console.WriteLine();
        Console.Write("在席: ");
        Console.WriteLine(string.Join(" / ", Enumerable.Range(0, 5).Select(k => k == 0
            ? $"{blFive[0].Def.Name} {Enumerable.Range(0, BlN).Count(i => blDHas[i][1]):N0}"
            : $"{blFive[k].Def.Name}×キリ {Enumerable.Range(0, BlN).Count(i => blDHas[i][9 + k]):N0}")));
        Console.WriteLine();

        Console.WriteLine("## 表C —— 傾きの分解（**主判定 Q1**）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 0枚 | 1枚 | 2枚 | 3+ | 第2波 | 第3波 | 第4波 | 第5波 | **集計** | **傾きの改善** | 上限 +3.2 に対する割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var blDSlope = new double[BlV];
        double blDSlope0 = double.NaN;
        for (int v = 0; v <= 5; v++)
        {
            var rate = BlDRates(v, -1);
            double[] bm = BlBoxMeans(rate, blDBox);
            double sl = BlSlope(rate, blDBox);
            blDSlope[v] = sl;
            if (v == 0) blDSlope0 = sl;
            double d = sl - blDSlope0;
            Console.WriteLine($"| {blVName[v]} | {bm[0]:F2}% | {bm[1]:F2}% | {bm[2]:F2}% | {(double.IsNaN(bm[3]) ? "—" : $"{bm[3]:F2}%")} "
                              + string.Concat(Enumerable.Range(1, blW - 1).Select(w => $"| {BlP1(BlSlope(BlDRates(v, w), blDBox))} "))
                              + $"| **{BlP1(sl)}** | {(v == 0 ? "—" : BlP2(d))} | {(v == 0 ? "—" : $"{100 * d / 3.2:F0}%")} |");
        }
        Console.WriteLine();
        Console.WriteLine("**線は傾きの改善 +2.0pt（上限 +3.2 の 63%）。上限を超えたら代金が実質消えている（＝採らない）。**");
        Console.WriteLine();

        Console.WriteLine("## 表D —— 帰属（**キリが在席した標本だけで比べる**）");
        Console.WriteLine();
        {
            var idx = Enumerable.Range(0, BlN).Where(i => blDHas[i][1]).ToArray();
            double a0 = idx.Average(i => Enumerable.Range(1, blW - 1).Average(w => BlDRate(i, 0, w)));
            Console.WriteLine("| 版 | 在席時の勝率 | **帰属（V0比）** | 傾き | 傾きの改善 |");
            Console.WriteLine("|---|--:|--:|--:|--:|");
            for (int v = 0; v <= 5; v++)
            {
                double a = idx.Average(i => Enumerable.Range(1, blW - 1).Average(w => BlDRate(i, v, w)));
                Console.WriteLine($"| {blVName[v]} | {a:F2}% | {(v == 0 ? "—" : BlP2(a - a0))} | {BlP1(blDSlope[v])} "
                                  + $"| {(v == 0 ? "—" : BlP2(blDSlope[v] - blDSlope0))} |");
            }
            Console.WriteLine();
            Console.WriteLine($"分母（キリが在席した標本）= **{idx.Length:N0} / {BlN:N0}**。");
        }
        Console.WriteLine();

        Console.WriteLine("## 表E —— Q4 軸の内側の符号（**キリと同席した標本だけ**・採否には使わない）");
        Console.WriteLine();
        Console.WriteLine("| 読み手 | 分母 | 寄与（V0） | 寄与（V1） | **差** |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int k = 1; k < 5; k++)
        {
            var idx = Enumerable.Range(0, BlN).Where(i => blDHas[i][9 + k]).ToArray();
            if (idx.Length == 0) { Console.WriteLine($"| {blFive[k].Def.Name} | 0 | — | — | — |"); continue; }
            double b0 = idx.Average(i => Enumerable.Range(1, blW - 1).Average(w => BlDRate(i, 0, w)));
            double p0 = idx.Average(i => Enumerable.Range(1, blW - 1).Average(w => BlDRate(i, 5 + k, w)));
            double b1 = idx.Average(i => Enumerable.Range(1, blW - 1).Average(w => BlDRate(i, 1, w)));
            double p1 = idx.Average(i => Enumerable.Range(1, blW - 1).Average(w => BlDRate(i, 9 + k, w)));
            Console.WriteLine($"| {blFive[k].Def.Name} | {idx.Length:N0} | {BlP2(b0 - p0)} | {BlP2(b1 - p1)} | **{BlP2((b1 - p1) - (b0 - p0))}** |");
        }
        Console.WriteLine();
        Console.WriteLine("**寄与 ＝ 現行 − その駒を素体にした版**（第69期の標準器具）を、V0 と V1 の両方で取った。");
        Console.WriteLine("**差が負 ＝ キリを直すとその読み手の値段が下がる**（軸の内側で符号が割れている）。");
        Console.WriteLine();

        Console.WriteLine("## 表F —— 版が何を動かしたか（**キリを含む先頭 300 標本 × 弱い波 × seed 2 本**）");
        Console.WriteLine();
        var blSample = new List<int>();
        for (int i = 0; i < BlN && blSample.Count < 300; i++) if (blDHas[i][1]) blSample.Add(i);
        int[] blGVer = { 0, 1, 2, 3, 4 };
        var blGAcc = new double[blGVer.Length][];
        for (int g = 0; g < blGVer.Length; g++)
        {
            var part = new double[blSample.Count][];
            int gi = g;
            Parallel.For(0, blSample.Count, si =>
            {
                int i = blSample[si];
                UnitDef[] team = BlTeam(i);
                int[] seats = BlSeats(team);
                var ids = team.Select(x => x.Id).ToHashSet();
                Formation f = BlDraftForm(team, seats, BlMapOf(blGVer[gi], ids));
                UnitDef?[] ob = BlBoard(f);
                var a = new double[BlC + 1];
                for (int w = 0; w < blW; w++)
                    for (int seed = blBand; seed < blBand + 2; seed++)
                        BlCount(BattleEngine.Run(f, blWeak[w].Enemy, seed, verbose: true, thinBlade: BlRuleOf(blGVer[gi])),
                                f.Count, ob, a);
                part[si] = a;
            });
            blGAcc[g] = new double[BlC + 1];
            for (int si = 0; si < blSample.Count; si++)
                for (int c = 0; c <= BlC; c++) blGAcc[g][c] += part[si][c];
        }
        Console.WriteLine("| 版 | 決着T | キリの振/戦 | **素の打点%** | **与ダメ/振** | キリの与ダメ/戦 | 連続同一標的% | 書込/戦 | **在庫/T** | 抉り発火 | 断ち発火 | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int g = 0; g < blGVer.Length; g++)
        {
            double[] a = blGAcc[g];
            double n = a[13], sw = a[22];
            Console.WriteLine($"| {blVName[blGVer[g]]} | {a[15] / n:F2} | {sw / n:F2} | {(sw > 0 ? $"{100 * a[24] / sw:F1}%" : "—")} "
                              + $"| **{a[26] / Math.Max(1, sw):F3}** | {a[26] / n:F2} | {(sw > 0 ? $"{100 * a[25] / sw:F1}%" : "—")} "
                              + $"| {(a[0] + a[1]) / n:F2} | **{a[14] / Math.Max(1, a[15]):F2}** | {a[2] / n:F2} | {a[6] / n:F2} "
                              + $"| {100 * a[BlC] / n:F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine("**在庫（P6）**: V1 でキリが早く倒すなら、刻む前に相手が落ちて在庫は減るはず。");
        Console.WriteLine("**連続同一標的%** は「直前と同じ相手を殴った割合」——V1 の条件が成立する経路の実測。");
        Console.WriteLine();
        Console.WriteLine($"所要 {blSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("blade: 引数は phase0 / check / （無し）/ alt / draft [alt]。");
    return;
}
}
