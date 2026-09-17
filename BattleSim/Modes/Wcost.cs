using BattleCore;
using static Common;

// =====================================================================================
// wcost モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "wcost")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 wcost
// =====================================================================================

static class WcostDiag
{
// wcost モード（第74期）: **傷の代金を分離する（器具の完成）＋ 断ちの待ち方。**
//
// 第73期は「5枚のマイナスのうち独立に外せたのは1枚（ノミの執着）だけ」で詰まった
// ——残り4枚のマイナスはプラスとまったく同じ `Trait` クラスの中にあり、`Traits.cs` を
// 触らずに落とせるのは「別の `TraitId` になっているマイナス」だけだったため。
//
// この期は残り4枚を**挙動を1ビットも変えずに**切り出した（薄刃 / 深追い / 刃待ち / 塞ぎ）。
// 切り出したマイナスは**既定で全員に付いたまま**で、`UnitDef.Traits` に2つ並ぶだけ
// ——受け入れ基準は `compare` が `docs/balance.md` と 305 セル 0 件で一致すること。
//
// **台・標本・seed 帯・箱の作り方はすべて第73期 `wound` の写し**（1文字も変えていない）。
// 変えると「値が動いたのは器具か機構か」が決まらなくなる。
//
//     dotnet run --project BattleSim -c Release 0 wcost phase0       # 分割の一覧と紙の計算（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 wcost check        # 受け入れ基準1の陰性対照
//     dotnet run --project BattleSim -c Release 0 wcost              # 理想台（A 帯 seed 0..199）
//     dotnet run --project BattleSim -c Release 0 wcost alt          # 同じ表を B 帯（seed 200..399）
//     dotnet run --project BattleSim -c Release 0 wcost draft [alt]  # ドラフト台 Pw（A 帯 seed 0..7 / B 帯 200..207）
public static void Run(string[] args, int stageIndex)
{
    string wcArg = args.Length > 2 ? args[2] : "";
    string wcArg2 = args.Length > 3 ? args[3] : "";
    var wcSw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> wcStages = EnemyCatalog.Stages;
    int wcW = wcStages.Count;
    var wcRoster = UnitCatalog.All.ToArray();
    int wcRN = wcRoster.Length;
    var wcKeyOf = wcRoster.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);
    const int WcKW = UnitTally.CarryWound;

    const int WcSeeds = 200;                 // 理想台の seed 本数（`compare` / 第73期と同じ）
    int wcSeed0 = wcArg == "alt" ? 200 : 0;

    // ---- 傷の5枚と、第74期に切り出したマイナス（**順序は指示書 §2 の M1〜M5**）--------------
    var wcFive = new (UnitDef Def, string Role, string Minus, TraitId Cut, string Site)[]
    {
        (UnitCatalog.Kiri, "書き手",           "与ダメは常に1",              TraitId.ThinBlade, "`ThinBladeTrait.ModifyAttack`"),
        (UnitCatalog.Egu,  "維持読み（攻）",   "倒すと次の手番を失う",        TraitId.Overreach, "`OverreachTrait.OnKill`"),
        (UnitCatalog.Nata, "消費読み",         "傷が2つ開くまで手番を捨てる", TraitId.Await,     "`AwaitTrait.CanAct` ＋ `SurrendersTurn`"),
        (UnitCatalog.Hari, "消費読み（防）",   "繕うたび敵の傷が1つ塞がる",   TraitId.Seal,      "**札** `SealTrait`（本体は `SutureTrait`）"),
        (UnitCatalog.Nomi, "書き手＋維持読み", "執着",                        TraitId.Fixate,    "`FixateTrait`（第73期からの前例）"),
    };
    var wcIds = wcFive.Select(x => x.Def.Id).ToArray();
    var wcIdSet = wcIds.ToHashSet();

    // ---- 変種（診断のローカル。`UnitCatalog` は1行も触らない）--------------------------------
    UnitDef WcPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    // **マイナスを1つだけ落とした版**（プラスはそのまま）。第74期の分割が無ければ作れない。
    UnitDef WcNoMinus(int k)
    {
        var x = wcFive[k];
        return new UnitDef
        {
            Id = x.Def.Id + "_m", Name = x.Def.Name + "・代金なし",
            MaxHp = x.Def.MaxHp, Attack = x.Def.Attack, Speed = x.Def.Speed,
            Traits = x.Def.Traits.Where(t => t != x.Cut).ToArray(), Pattern = x.Def.Pattern
        };
    }

    Formation WcSub(Formation f, IReadOnlyDictionary<string, UnitDef> map)
    {
        Formation g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (map.TryGetValue(d.Id, out UnitDef? nd)) g[slot] = nd;
        return g;
    }

    // ---- 版（**理想台とドラフト台で同じ並び**）-----------------------------------------------
    // 0 現行 / 1..5 M1〜M5 / 6 M-all / 7 傷の駒を全部素体 / 8 V1（待ち方）/ 9 V2（閾値）
    const int WcV = 10;
    string[] wcVName = new string[WcV];
    wcVName[0] = "**V0 採用前（手番を捨てる）**";
    for (int k = 0; k < 5; k++) wcVName[1 + k] = $"M{k + 1} 代金なし:{wcFive[k].Def.Name}";
    wcVName[6] = "**M-all（5つ全部）**";
    wcVName[7] = "傷の駒を全部素体";
    wcVName[8] = "**V1 待ち方（手番を捨てない）＝第74期に採用**";
    wcVName[9] = "V2 閾値 2 → 1（待ち方は V0 のまま）";

    // 版 → 規則。**`SeverRule.Default` を使わずに全部を明示する**（第74期に採用で既定が動いたため。
    // 第60期の「採用したら診断の V0 を作り直すこと」の形）——こう書いておくと、
    // **既定がどちらであってもこの表の数字が動かない**ので、第73期・第74期の報告と直接比べられる。
    // **版 0〜7（現行・M1〜M5・M-all・全部素体）は V0 の規則で回す。**
    SeverRule WcRuleOf(int v) => v == 8 ? new SeverRule(SeverWait.Swing, SeverTrait.Threshold)
                               : v == 9 ? new SeverRule(SeverWait.Yield, 1)
                               : new SeverRule(SeverWait.Yield, SeverTrait.Threshold);

    // 版 → 差し替え表（規則版は差し替えなし）
    Dictionary<string, UnitDef>? WcMapOf(int v, ISet<string> ids)
    {
        if (v == 0 || v >= 8) return null;
        var map = new Dictionary<string, UnitDef>();
        if (v >= 1 && v <= 5)
        {
            if (ids.Contains(wcIds[v - 1])) map[wcIds[v - 1]] = WcNoMinus(v - 1);
        }
        else if (v == 6)
        {
            for (int k = 0; k < 5; k++) if (ids.Contains(wcIds[k])) map[wcIds[k]] = WcNoMinus(k);
        }
        else if (v == 7)
        {
            for (int k = 0; k < 5; k++) if (ids.Contains(wcIds[k])) map[wcIds[k]] = WcPlain(wcFive[k].Def);
        }
        return map.Count > 0 ? map : null;
    }

    // 版が「その編成で意味を持つか」。**持たない版は現行と同じ数字になるので表から外す**
    bool WcHas(int v, ISet<string> ids)
        => v == 0 ? true
         : v >= 1 && v <= 5 ? ids.Contains(wcIds[v - 1])
         : v == 6 || v == 7 ? wcIds.Any(ids.Contains)
         : ids.Contains(UnitCatalog.Nata.Id);      // V1 / V2 は刃待ちの保持者がいる編成だけ

    // ---- 統計の道具（第69〜73期の写し。**1文字も変えていない**）------------------------------
    string WcP1(double x) => double.IsNaN(x) ? "—" : (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    string WcP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");

    double WcSlope(IReadOnlyList<double> rate, IReadOnlyList<int> box)
    {
        var bk = new List<double>[4];
        for (int j = 0; j < 4; j++) bk[j] = new List<double>();
        for (int i = 0; i < rate.Count; i++) bk[Math.Min(3, box[i])].Add(rate[i]);
        double d10 = (bk[0].Count > 0 && bk[1].Count > 0) ? bk[1].Average() - bk[0].Average() : double.NaN;
        double d21 = (bk[1].Count > 0 && bk[2].Count > 0) ? bk[2].Average() - bk[1].Average() : double.NaN;
        return double.IsNaN(d10) ? d21 : double.IsNaN(d21) ? d10 : (d10 + d21) / 2;
    }
    double[] WcBoxMeans(IReadOnlyList<double> rate, IReadOnlyList<int> box)
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
    static int WcNum(string t, string prefix)
    {
        int a = t.IndexOf(prefix, StringComparison.Ordinal);
        if (a < 0) return 0;
        a += prefix.Length;
        int b = a;
        while (b < t.Length && char.IsDigit(t[b])) b++;
        return b > a ? int.Parse(t[a..b]) : 0;
    }

    // 1戦ぶんの計数（**盤面を1ビットも触らない**。ログと `Events` を読むだけ）。第73期の写し。
    // 添字: 0 裂きの書 / 1 刻みの書 / 2,3 抉りの発火・読んだ傷 / 4,5 なぞり / 6,7 断ち / 8,9 縫い /
    //       10 放棄T / 11 待ちT / 12 深追い / 13 戦数 / 14 在庫Σ / 15 ターンΣ / 16 味方の席の傷 /
    //       17..21 5枚の振り / WcC 勝数
    const int WcC = 22;
    void WcCount(BattleResult res, int pcount, UnitDef?[] ob, double[] a)
    {
        a[WcC] += res.PlayerWon ? 1 : 0;
        a[13]++;
        a[15] += res.Turns;
        for (int k = 0; k < 5; k++)
            if (ob[k] is UnitDef d && res.TallyByUnit.TryGetValue(d.Id, out UnitTally? tl))
                a[17 + k] += tl.Attacks;

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
        foreach (LogLine l in res.Log)
        {
            string t = l.Text;
            if (l.Kind == LogKind.Turn) { turn++; continue; }
            if (nKiri != null && t.Contains(nKiri + " の刃が") && t.Contains("に傷を残した")) { a[0]++; continue; }
            if (nNomi != null && t.Contains(nNomi + " の鑿が")) { a[1]++; continue; }
            if (nEgu != null && t.Contains("の傷をこじ開ける") && t.Contains(nEgu + " が "))
            { a[2]++; a[3] += WcNum(t, "（傷 "); continue; }
            if (nNomi != null && t.Contains("の古い傷をなぞる") && t.Contains(nNomi + " が "))
            { a[4]++; a[5] += WcNum(t, "（傷 "); continue; }
            if (nNata != null && t.Contains("の傷をまとめて断つ") && t.Contains(nNata + " が "))
            { a[6]++; a[7] += WcNum(t, "（傷 "); continue; }
            if (nHari != null && t.Contains("の傷口から糸を引き") && t.Contains(nHari + " が "))
            { a[8]++; a[9] += WcNum(t, "（傷 "); continue; }
            // 放棄・待ちは**1ターンに1回だけ**（`CanAct` は `Trait.SurrenderedTurn` からも呼ばれる）
            if (nNata != null && t.Contains(nNata + " は閉じた肌に刃を下ろさない"))
            { if (turn != lastIdle) { a[10]++; lastIdle = turn; } continue; }
            if (nNata != null && t.Contains(nNata + " は傷がまだ浅いと刃を上げない"))
            { if (turn != lastWait) { a[11]++; lastWait = turn; } continue; }
            if (nEgu != null && t.Contains(nEgu + " は ") && t.Contains("の裂け目に踏み込みすぎた")) { a[12]++; continue; }
        }
    }

    // ---- ドラフト台の器具（第69〜73期の写し。**1文字も変えていない**）------------------------
    const int WcOfferSeed = 2_000_000;
    const int WcN = 11000, WcM = 8;
    const int WcStrong = 7;
    const int WcWeakPct = 60;
    int wcBand = wcArg2 == "alt" ? 200 : 0;

    var wcWeakCache = new Dictionary<string, UnitDef>();
    UnitDef WcWeakOf(UnitDef d)
    {
        if (wcWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * WcWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        wcWeakCache[d.Id] = w;
        return w;
    }
    var wcWeak = wcStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = WcWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    // 規則 P（素朴・第70〜73期と同一）。**この期は Pw だけ**——S'w は傷の駒を締め出す（第73期 0-5）。
    UnitDef[] WcTeam(int i)
    {
        var rng = new Random(WcOfferSeed + i);
        var idx = new int[wcRN];
        for (int k = 0; k < wcRN; k++) idx[k] = k;
        int remain = wcRN, strong = 0;
        var picked = new UnitDef[5];
        for (int r = 0; r < 5; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = wcRoster[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= WcStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(wcRoster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int[] WcSeats(UnitDef[] u)
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
    Formation WcDraftForm(UnitDef[] u, int[] seats, IReadOnlyDictionary<string, UnitDef>? map)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
            f[seats[k]] = (map is not null && map.TryGetValue(u[k].Id, out UnitDef? nd)) ? nd : u[k];
        return f;
    }
    int WcWoundCount(IEnumerable<UnitDef> team)
        => team.Count(x => wcKeyOf.TryGetValue(x.Id, out int[]? k) && k.Contains(WcKW));

    var wcAllRows = CompareBuilds();
    var wcRows = wcAllRows.Where(b => b.F.Occupied().Any(o => wcIdSet.Contains(o.Def.Id))).ToArray();

    UnitDef?[] WcBoard(Formation g)
    {
        var arr = new UnitDef?[5];
        foreach ((int _, UnitDef d) in g.Occupied())
            for (int k = 0; k < 5; k++)
                if (d.Id == wcIds[k] || d.Id.StartsWith(wcIds[k] + "_", StringComparison.Ordinal)) arr[k] = d;
        return arr;
    }

    // =====================================================================================
    // phase0: 分割の一覧と紙の計算（**戦闘を1回も回さない**。抽選だけは回す）
    // =====================================================================================
    if (wcArg == "phase0")
    {
        Console.WriteLine("# 第74期 Phase 0 —— 分割の一覧と、測る前の紙の計算");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `dotnet run --project BattleSim -c Release 0 wcost phase0`");
        Console.WriteLine();
        Console.WriteLine("## 0-1. 表A —— 切り出したマイナス4枚（**§1 の器具**）");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | プラス（残す） | マイナス（切り出す） | 新しい `TraitId` | 窓口 | 分離の質 |");
        Console.WriteLine("|--:|---|---|---|---|---|---|");
        string[] wcQuality =
        {
            "**フック**（`ModifyAttack` を丸ごと移した）",
            "**フック**（`OnKill` を丸ごと移した）",
            "**フック**（`CanAct` ＋ `SurrendersTurn` を対で移した）",
            "**札**（フックには切り出せない。`SealTrait` の doc を参照）",
            "**もとから別**（第73期の前例）",
        };
        for (int k = 0; k < 5; k++)
            Console.WriteLine($"| {k + 1} | {wcFive[k].Def.Name} | `{wcFive[k].Def.Traits[0]}` | {wcFive[k].Minus} "
                              + $"| `{wcFive[k].Cut}` | {wcFive[k].Site} | {wcQuality[k]} |");
        Console.WriteLine();
        Console.WriteLine("**切り出したマイナスは既定で全員に付いたまま**——`UnitDef.Traits` に2つ並ぶだけで、");
        Console.WriteLine("盤面は1ビットも変わらない（受け入れ基準は `wcost check` と `docs/` の diff）。");
        Console.WriteLine();

        Console.WriteLine("## 0-2. `Traits` とキーの前後（**キーの和が変わっていないこと**）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | `Traits`（現在） | `TraitKeyMap.KeysOf` | 代金なし版の `Traits` |");
        Console.WriteLine("|---|---|---|---|");
        for (int k = 0; k < 5; k++)
        {
            UnitDef d = wcFive[k].Def, m = WcNoMinus(k);
            Console.WriteLine($"| {d.Name} | {string.Join(", ", d.Traits.Select(t => $"`{t}`"))} "
                              + $"| {string.Join(", ", TraitKeyMap.KeysOf(d).Select(x => $"`{UnitTally.CarryKeys[x]}`"))} "
                              + $"| {string.Join(", ", m.Traits.Select(t => $"`{t}`"))} |");
        }
        Console.WriteLine();
        Console.WriteLine("**マイナス側の `TraitId` にはキーを持たせていない**（`TraitKeyMap` に空で登録）。");
        Console.WriteLine("`KeysOf` は駒の `Traits` の**和**を取るので、プラス側を残した分割では和が動かない");
        Console.WriteLine("——マイナスにキーを足すと第68期以降の「駒で数える」が変わってしまう。");
        Console.WriteLine();

        Console.WriteLine("## 0-3. 到達可能な上限（**指示書 §0-1 の再掲**）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 値 | 出どころ |");
        Console.WriteLine("|---|--:|---|");
        Console.WriteLine("| ドラフト台 Pw の傷の枚数効果 | **−15.6** | 第71期 表E / 第72期 表D / 第73期 §4-1 |");
        Console.WriteLine("| うち体と枠の機会費用（傷の駒を全部素体） | −7.2 | 第73期 表F |");
        Console.WriteLine("| **うち特性の側 ＝ この期の上限** | **+8.3** | 同上 |");
        Console.WriteLine("| 主判定（Q4）の線 | +2.0 | 上限の **24%** |");
        Console.WriteLine();

        Console.WriteLine("## 0-4. 理想 61 行のうち傷の駒を含む行");
        Console.WriteLine();
        Console.WriteLine("| # | 行 | 傷の駒 | 枚数 | **刃待ち（ナタ）** |");
        Console.WriteLine("|--:|---|---|--:|:-:|");
        for (int r = 0; r < wcRows.Length; r++)
        {
            var mem = wcRows[r].F.Occupied().Select(o => o.Def).Where(d => wcIdSet.Contains(d.Id)).ToArray();
            Console.WriteLine($"| {r + 1} | {wcRows[r].Name} | {string.Join("・", mem.Select(d => d.Name))} | {mem.Length} "
                              + $"| {(mem.Any(d => d.Id == UnitCatalog.Nata.Id) ? "**○**" : "×")} |");
        }
        var wcBox0 = wcAllRows.Select(b => Math.Min(3, WcWoundCount(b.F.Occupied().Select(o => o.Def)))).ToArray();
        Console.WriteLine();
        Console.WriteLine($"箱: **0枚 {wcBox0.Count(x => x == 0)} / 1枚 {wcBox0.Count(x => x == 1)} / "
                          + $"2枚 {wcBox0.Count(x => x == 2)} / 3+ {wcBox0.Count(x => x == 3)}**（第72期 表D・第73期と一致）。");
        Console.WriteLine();
        Console.WriteLine("> **V1（待ち方）を理想台で測れる行は 1 行しか無い**——刃待ちの保持者はナタ 1 枚で、");
        Console.WriteLine("> `CompareBuilds()` でナタが出る行は `刻み×断ち (ノミ×ナタ)` だけ。**主判定はドラフト台で書く。**");
        Console.WriteLine();

        // ---- 0-5. 主判定19行と歯止め（**V1 が動かしうる分母**。第61期の自己検査 (a)）----
        var wcPrimary = new HashSet<string>(Baseline.PrimaryRows);
        var wcPrimRows = wcAllRows.Where(b => wcPrimary.Contains(b.Name)).ToArray();
        var wcPrimWound = wcPrimRows.Where(b => b.F.Occupied().Any(o => wcIdSet.Contains(o.Def.Id))).ToArray();
        var wcPrimNata = wcPrimRows.Where(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Nata.Id)).ToArray();
        Console.WriteLine("## 0-5. 歯止め（第五波）の分母 —— **V1 が動かしうる行は何行か**");
        Console.WriteLine();
        Console.WriteLine("| 量 | 値 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine($"| 主判定 | {Baseline.PrimaryRows.Length} 行（`Baseline.PrimaryRows`） |");
        Console.WriteLine($"| うち傷の駒を含む | **{wcPrimWound.Length} 行**（{string.Join(" / ", wcPrimWound.Select(b => b.Name))}） |");
        Console.WriteLine($"| **うち刃待ち（ナタ）を含む** | **{wcPrimNata.Length} 行** |");
        Console.WriteLine($"| 歯止め | 第五波 {Baseline.PrimaryFifthFloor:F1}% |");
        Console.WriteLine();
        if (wcPrimNata.Length == 0)
        {
            Console.WriteLine("> **V1 は主判定19行を1行も動かさない。歯止めは構造的に発動しない**");
            Console.WriteLine("> ——第61期（瘴気）の自己検査 (a) と同じ形で、**測る前に紙で出る**。");
            Console.WriteLine();
        }

        // ---- 0-6. 紙の計算（ドラフト台の在席。抽選だけ）----
        Console.WriteLine("## 0-6. 紙の計算 —— ドラフト台 Pw に刃待ちは何回入るか（**抽選だけ。戦闘0回**）");
        Console.WriteLine();
        const int WcSim = 100000;
        var wcSeat = new int[5];
        int wcAnyW = 0, wcNata = 0;
        for (int i = 0; i < WcSim; i++)
        {
            var ids = WcTeam(i).Select(x => x.Id).ToHashSet();
            bool any = false;
            for (int k = 0; k < 5; k++) if (ids.Contains(wcIds[k])) { wcSeat[k]++; any = true; }
            if (any) wcAnyW++;
            if (ids.Contains(UnitCatalog.Nata.Id)) wcNata++;
        }
        Console.WriteLine("| 駒 | " + string.Join(" | ", wcFive.Select(x => x.Def.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", 5)));
        Console.WriteLine($"| 在席（{WcSim:N0} 標本） | " + string.Join(" | ", wcSeat.Select(c => $"{c}")) + " |");
        Console.WriteLine();
        Console.WriteLine($"傷 ≥1 の標本 **{100.0 * wcAnyW / WcSim:F1}%** / **ナタ在席 {100.0 * wcNata / WcSim:F1}%**"
                          + $"（11,000 標本なら約 {WcN * wcNata / WcSim:N0} 件が V1 で動く）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {wcSw.Elapsed.TotalSeconds:F1} 秒（**戦闘 0 回**）。");
        return;
    }

    // =====================================================================================
    // check: 受け入れ基準1の陰性対照（**分割が挙動を変えていないこと**）
    // =====================================================================================
    if (wcArg == "check")
    {
        Console.WriteLine("# 第74期 —— 陰性対照（受け入れ基準1・Q1）");
        Console.WriteLine();
        Console.WriteLine("## (1) 分割の形");
        Console.WriteLine();
        Console.WriteLine("| 確認 | 結果 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine($"| `UnitCatalog.All` の体数 | {wcRN} |");
        Console.WriteLine($"| 傷キーの保持者 | {wcRoster.Count(u => wcKeyOf[u.Id].Contains(WcKW))} |");
        Console.WriteLine($"| 5枚が切り出したマイナスを持っていること | {wcFive.Count(x => x.Def.Traits.Contains(x.Cut))} / 5 |");
        int wcKeyDiff = 0;
        for (int k = 0; k < 5; k++)
            if (!TraitKeyMap.KeysOf(wcFive[k].Def).SequenceEqual(TraitKeyMap.KeysOf(WcNoMinus(k)))) wcKeyDiff++;
        Console.WriteLine($"| **マイナス側の `TraitId` がキーを持たないこと**（外しても `KeysOf` が同じ） | **{wcKeyDiff} 件の食い違い** |");
        Console.WriteLine($"| 変種の `Id` が `All` に無いこと | "
                          + $"{Enumerable.Range(0, 5).Count(k => wcRoster.Any(u => u.Id == WcNoMinus(k).Id || u.Id == WcPlain(wcFive[k].Def).Id))} 件の衝突 |");
        Console.WriteLine($"| `SeverRule.Default` | `{SeverRule.Default.Wait}` / 閾値 {SeverRule.Default.Threshold}"
                          + $"（`SeverTrait.Threshold` = {SeverTrait.Threshold}） |");
        Console.WriteLine();

        Console.WriteLine("## (2) `compare` の全セル（**305 セル**）を `docs/balance.md` と突き合わせる");
        Console.WriteLine();
        string? wcRoot = Directory.GetCurrentDirectory();
        while (wcRoot != null && !File.Exists(Path.Combine(wcRoot, "docs", "balance.md")))
            wcRoot = Path.GetDirectoryName(wcRoot);
        if (wcRoot == null) Console.WriteLine("`docs/` が見つからない（リポジトリの外から実行している）。");
        else
        {
            var wcNow = new double[wcAllRows.Length][];
            Parallel.For(0, wcAllRows.Length, ri =>
            {
                var a = new double[wcW];
                for (int w = 0; w < wcW; w++)
                {
                    int win = 0;
                    for (int seed = 0; seed < WcSeeds; seed++)
                        if (BattleEngine.Run(wcAllRows[ri].F, wcStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                    a[w] = win * 100.0 / WcSeeds;
                }
                wcNow[ri] = a;
            });
            var wcV0 = new double[wcAllRows.Length][];
            Parallel.For(0, wcAllRows.Length, ri =>
            {
                var a = new double[wcW];
                for (int w = 0; w < wcW; w++)
                {
                    int win = 0;
                    for (int seed = 0; seed < WcSeeds; seed++)
                        if (BattleEngine.Run(wcAllRows[ri].F, wcStages[w].Enemy, seed, verbose: false,
                                             sever: new SeverRule(SeverWait.Yield, SeverTrait.Threshold)).PlayerWon) win++;
                    a[w] = win * 100.0 / WcSeeds;
                }
                wcV0[ri] = a;
            });
            string[] bl = File.ReadAllLines(Path.Combine(wcRoot, "docs", "balance.md"));
            int cells = 0, bad = 0, badV0 = 0, matched = 0;
            var moved = new List<string>();
            for (int ri = 0; ri < wcAllRows.Length; ri++)
            {
                string? line = bl.FirstOrDefault(l => l.StartsWith("| " + wcAllRows[ri].Name + " |", StringComparison.Ordinal));
                if (line == null) continue;
                matched++;
                var cols = line.Split('|').Select(c => c.Trim()).ToArray();
                bool any = false;
                for (int w = 0; w < wcW; w++)
                {
                    cells++;
                    bool ok = double.TryParse(cols[2 + w].TrimEnd('%'), out double doc);
                    if (!ok || Math.Abs(doc - wcNow[ri][w]) > 1e-9) bad++;
                    if (!ok || Math.Abs(doc - wcV0[ri][w]) > 1e-9) { badV0++; any = true; }
                }
                if (any) moved.Add(wcAllRows[ri].Name);
            }
            Console.WriteLine("| 量 | 値 |");
            Console.WriteLine("|---|--:|");
            Console.WriteLine($"| 突き合わせた行 | {matched} / {wcAllRows.Length} |");
            Console.WriteLine($"| 突き合わせたセル | {cells} |");
            Console.WriteLine($"| **現在の既定（`SeverRule.Default`）との食い違い** | **{bad} 件** |");
            Console.WriteLine($"| V0（第74期の採用前）との食い違い | {badV0} 件 |");
            Console.WriteLine();
            Console.WriteLine("**上段が 0 件なら `docs/balance.md` はコードと一致している。**");
            Console.WriteLine($"下段は**第74期の採用（V1）が動かしたセル**——動いた行は "
                              + $"{(moved.Count == 0 ? "**無し**" : $"**{moved.Count} 行**（{string.Join(" / ", moved)}）")}。");
            Console.WriteLine("**傷の駒を含まない行が1つも出ないこと**が Q7 の陰性対照。");
        }
        Console.WriteLine();

        Console.WriteLine("## (3) 代金なし版が「その1点だけ」を動かすこと（1戦の監査・50 seed × 5波）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 版 | 振/戦 | 与ダメ/戦 | **与ダメ/振** | 書込/戦 | 深追い/戦 | 放棄+待ちT/戦 | 断ち発火/戦 | 縫い発火/戦 | 在庫/T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int k = 0; k < 5; k++)
        {
            var row = wcRows.FirstOrDefault(b => b.F.Occupied().Any(o => o.Def.Id == wcIds[k]));
            if (row.F is null) continue;
            foreach ((string lab, UnitDef d) in new[] { ("現行", wcFive[k].Def), ("代金なし", WcNoMinus(k)) })
            {
                Formation f = WcSub(row.F, new Dictionary<string, UnitDef> { [wcIds[k]] = d });
                UnitDef?[] ob = WcBoard(f);
                var a = new double[WcC + 1];
                double dmg = 0;
                for (int w = 0; w < wcW; w++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult res = BattleEngine.Run(f, wcStages[w].Enemy, seed, verbose: true);
                        WcCount(res, f.Count, ob, a);
                        if (res.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) dmg += t.DamageToEnemy;
                    }
                double n = a[13], sw = a[17 + k];
                Console.WriteLine($"| {(lab == "現行" ? wcFive[k].Def.Name : "")} | {lab} | {sw / n:F2} | {dmg / n:F2} "
                                  + $"| **{dmg / Math.Max(1, sw):F3}** | {(a[0] + a[1]) / n:F2} | {a[12] / n:F2} "
                                  + $"| {(a[10] + a[11]) / n:F2} | {a[6] / n:F2} | {a[8] / n:F2} | {a[14] / Math.Max(1, a[15]):F2} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("**薄刃を外すと `与ダメ/振` だけが 1.000 から素の攻撃力へ動き、書込は動かない**——");
        Console.WriteLine("第73期が `素体(攻12) − 素体(攻1)` で代用したものが、ここで**本物の分割**になっている。");
        Console.WriteLine();
        Console.WriteLine($"所要 {wcSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // 主表（理想台）
    // =====================================================================================
    if (wcArg.Length == 0 || wcArg == "alt")
    {
        string wcBandName = wcArg == "alt" ? "B" : "A";

        var wcVers = new (int V, Formation F, UnitDef?[] OnBoard)[wcRows.Length][];
        for (int r = 0; r < wcRows.Length; r++)
        {
            var ids = wcRows[r].F.Occupied().Select(o => o.Def.Id).ToHashSet();
            var list = new List<(int, Formation, UnitDef?[])>();
            for (int v = 0; v < WcV; v++)
            {
                if (!WcHas(v, ids)) continue;
                Dictionary<string, UnitDef>? map = WcMapOf(v, ids);
                Formation f = map is null ? wcRows[r].F : WcSub(wcRows[r].F, map);
                list.Add((v, f, WcBoard(f)));
            }
            wcVers[r] = list.ToArray();
        }

        var wcAcc = new double[wcRows.Length][][][];
        for (int r = 0; r < wcRows.Length; r++)
        {
            wcAcc[r] = new double[wcVers[r].Length][][];
            for (int v = 0; v < wcVers[r].Length; v++)
            {
                wcAcc[r][v] = new double[wcW][];
                for (int w = 0; w < wcW; w++) wcAcc[r][v][w] = new double[WcC + 1];
            }
        }

        Console.Error.Write($"{wcBandName}帯 理想台: ");
        var wcJobs = new List<(int R, int Vi)>();
        for (int r = 0; r < wcRows.Length; r++)
            for (int v = 0; v < wcVers[r].Length; v++) wcJobs.Add((r, v));
        Parallel.ForEach(wcJobs, job =>
        {
            (int r, int vi) = job;
            (int v, Formation f, UnitDef?[] ob) = wcVers[r][vi];
            SeverRule rule = WcRuleOf(v);
            int pcount = f.Count;
            for (int w = 0; w < wcW; w++)
                for (int seed = wcSeed0; seed < wcSeed0 + WcSeeds; seed++)
                    WcCount(BattleEngine.Run(f, wcStages[w].Enemy, seed, verbose: true, sever: rule),
                            pcount, ob, wcAcc[r][vi][w]);
            Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        var wcBase = new double[wcAllRows.Length][];
        Console.Error.Write($"{wcBandName}帯 61行: ");
        Parallel.For(0, wcAllRows.Length, ri =>
        {
            var a = new double[wcW];
            for (int w = 0; w < wcW; w++)
            {
                int win = 0;
                for (int seed = wcSeed0; seed < wcSeed0 + WcSeeds; seed++)
                    if (BattleEngine.Run(wcAllRows[ri].F, wcStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                a[w] = win * 100.0 / WcSeeds;
            }
            wcBase[ri] = a;
            Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        int WcIdxOf(int r, int v) => Array.FindIndex(wcVers[r], x => x.V == v);
        double WcRate(int r, int vi, int w) => wcAcc[r][vi][w][WcC] * 100.0 / WcSeeds;
        double WcRate25(int r, int vi) => Enumerable.Range(1, wcW - 1).Average(w => WcRate(r, vi, w));
        double[] WcSum(int r, int vi)
        {
            var a = new double[WcC + 1];
            for (int w = 0; w < wcW; w++) for (int c = 0; c <= WcC; c++) a[c] += wcAcc[r][vi][w][c];
            return a;
        }

        Console.WriteLine($"# 第74期 —— 傷の代金を分離する（理想台・{wcBandName} 帯 seed {wcSeed0}..{wcSeed0 + WcSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 wcost{(wcArg == "alt" ? " alt" : "")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"理想 {wcAllRows.Length} 行のうち**傷の駒を含む {wcRows.Length} 行**（行は選んでいない）× 版 "
                          + $"{wcJobs.Count / (double)wcRows.Length:F1}（平均）× {wcW} 波 × seed {WcSeeds} 本。");
        Console.WriteLine("**台・標本・seed 帯・箱の作り方は第73期の写し**（1文字も変えていない）。");
        Console.WriteLine();
        Console.WriteLine("**版 0〜7 は V0（＝第74期の採用前の待ち方）の規則で回してある**——第73期の表と直接比べるため。");
        Console.WriteLine("**採用後の現行は V1（版 8）。**規則は版ごとに明示していて `SeverRule.Default` を読んでいない。");
        Console.WriteLine();

        int wcVmis = 0, wcAllyW = 0;
        for (int r = 0; r < wcRows.Length; r++)
        {
            int bi = Array.FindIndex(wcAllRows, b => b.Name == wcRows[r].Name);
            for (int w = 0; w < wcW; w++)
                if (Math.Abs(WcRate(r, 0, w) - wcBase[bi][w]) > 1e-9) wcVmis++;
            for (int v = 0; v < wcVers[r].Length; v++) wcAllyW += (int)WcSum(r, v)[16];
        }
        Console.WriteLine($"**検算: `verbose` 版と非 `verbose` 版の勝率の食い違い {wcVmis} 件 / {wcRows.Length * wcW}**"
                          + "（イベントを積む処理が盤面を動かしていないこと）。");
        Console.WriteLine($"**陰性対照: 味方の席に載った傷 {wcAllyW} 件**"
                          + $"（{wcJobs.Count * wcW * WcSeeds:N0} 戦のターン頭スナップショット全数）。");
        Console.WriteLine();

        // ---- 表D（理想側）----
        Console.WriteLine("## 表D（理想側）—— 5つのマイナスを1つずつ外す");
        Console.WriteLine();
        Console.WriteLine("`Δ` は V0（第74期の採用前）との差（第2〜5波の平均勝率）。**同じ5枚・同じ席で、代金1つだけを落とした版。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第2〜5波 | **Δ** | 5波 | 書込/戦 | 在庫/T | 断ち発火 | 放棄+待ちT | 縫い発火 | 深追い |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int r = 0; r < wcRows.Length; r++)
        {
            double b25 = WcRate25(r, 0);
            for (int vi = 0; vi < wcVers[r].Length; vi++)
            {
                double[] a = WcSum(r, vi);
                double n = a[13];
                Console.WriteLine($"| {(vi == 0 ? wcRows[r].Name : "")} | {wcVName[wcVers[r][vi].V]} | {WcRate25(r, vi):F2}% "
                                  + $"| {(vi == 0 ? "—" : WcP2(WcRate25(r, vi) - b25))} "
                                  + $"| {Enumerable.Range(0, wcW).Average(w => WcRate(r, vi, w)):F2}% "
                                  + $"| {(a[0] + a[1]) / n:F2} | {a[14] / Math.Max(1, a[15]):F2} | {a[6] / n:F2} "
                                  + $"| {(a[10] + a[11]) / n:F2} | {a[8] / n:F2} | {a[12] / n:F2} |");
            }
        }
        Console.WriteLine();

        // ---- 表F（理想側）----
        Console.WriteLine("## 表F（理想側）—— 傷の枚数効果の傾き（**箱は元の5枚で決める**）");
        Console.WriteLine();
        var wcBox = wcAllRows.Select(b => Math.Min(3, WcWoundCount(b.F.Occupied().Select(o => o.Def)))).ToArray();
        double[] WcIdealRates(int v, int wave)
        {
            var rate = new double[wcAllRows.Length];
            for (int i = 0; i < wcAllRows.Length; i++)
                rate[i] = wave < 0 ? Enumerable.Range(1, wcW - 1).Average(w => wcBase[i][w]) : wcBase[i][wave];
            for (int r = 0; r < wcRows.Length; r++)
            {
                int vi = WcIdxOf(r, v);
                if (vi < 0) continue;
                int bi = Array.FindIndex(wcAllRows, b => b.Name == wcRows[r].Name);
                rate[bi] = wave < 0 ? WcRate25(r, vi) : WcRate(r, vi, wave);
            }
            return rate;
        }
        Console.WriteLine("| 版 | 0枚 | 1枚 | 2枚 | 第2波 | 第3波 | 第4波 | 第5波 | **集計（第2〜5波）** | Δ（V0比） | 動いた行 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        double wcSlope0 = double.NaN;
        var wcIdealSlope = new double[WcV];
        for (int v = 0; v < WcV; v++) wcIdealSlope[v] = double.NaN;
        for (int v = 0; v < WcV; v++)
        {
            int moved = Enumerable.Range(0, wcRows.Length).Count(r => WcIdxOf(r, v) >= 0);
            if (moved == 0 && v != 0) continue;
            var rate = WcIdealRates(v, -1);
            double[] bm = WcBoxMeans(rate, wcBox);
            double sl = WcSlope(rate, wcBox);
            wcIdealSlope[v] = sl;
            if (v == 0) wcSlope0 = sl;
            Console.WriteLine($"| {wcVName[v]} | {bm[0]:F1}% | {bm[1]:F1}% | {bm[2]:F1}% "
                              + string.Concat(Enumerable.Range(1, wcW - 1).Select(w => $"| {WcP1(WcSlope(WcIdealRates(v, w), wcBox))} "))
                              + $"| **{WcP1(sl)}** | {(v == 0 ? "—" : WcP2(sl - wcSlope0))} | {moved} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**箱の大きさ: 0枚 {wcBox.Count(x => x == 0)} 行 / 1枚 {wcBox.Count(x => x == 1)} 行 / "
                          + $"2枚 {wcBox.Count(x => x == 2)} 行 / 3+ {wcBox.Count(x => x == 3)} 行**"
                          + "（第72期 表D・第73期と一致）。**1枚の箱は1行しか無い。**");
        Console.WriteLine();

        // ---- Q2 分解が閉じたか ----
        Console.WriteLine("### Q2 —— 分解は閉じたか（`M-all` と `全部素体`）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 傾き | Δ（V0比） |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 現行 | {WcP1(wcIdealSlope[0])} | — |");
        Console.WriteLine($"| **M-all（5つのマイナス全部）** | {WcP1(wcIdealSlope[6])} | {WcP2(wcIdealSlope[6] - wcSlope0)} |");
        Console.WriteLine($"| 傷の駒を全部素体 | {WcP1(wcIdealSlope[7])} | {WcP2(wcIdealSlope[7] - wcSlope0)} |");
        Console.WriteLine($"| **差（M-all − 全部素体）** | **{WcP2(wcIdealSlope[6] - wcIdealSlope[7])}** | 線 1.5pt |");
        Console.WriteLine();

        // ---- Q4 / Q5 待ち方（ナタの行）----
        Console.WriteLine("## Q4 / Q5 —— 断ちの待ち方（**理想台はナタの行が1つしか無い**）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第2〜5波 | **Δ** | 決着T | ナタの振/戦 | 断ち発火/戦 | **放棄T** | **待ちT** | 読んだ傷/発火 | 在庫/T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int r = 0; r < wcRows.Length; r++)
        {
            if (WcIdxOf(r, 8) < 0) continue;
            double b25 = WcRate25(r, 0);
            foreach (int v in new[] { 0, 3, 8, 9 })
            {
                int vi = WcIdxOf(r, v);
                if (vi < 0) continue;
                double[] a = WcSum(r, vi);
                double n = a[13];
                Console.WriteLine($"| {(v == 0 ? wcRows[r].Name : "")} | {wcVName[v]} | {WcRate25(r, vi):F2}% "
                                  + $"| {(v == 0 ? "—" : WcP2(WcRate25(r, vi) - b25))} | {a[15] / n:F2} "
                                  + $"| {a[19] / n:F2} | {a[6] / n:F2} | {a[10] / n:F2} | {a[11] / n:F2} "
                                  + $"| {(a[6] > 0 ? $"{a[7] / a[6]:F2}" : "—")} | {a[14] / Math.Max(1, a[15]):F2} |");
            }
        }
        Console.WriteLine();

        // ---- Q6 情報セル ----
        Console.WriteLine("## Q6 —— 情報セル（傷の 8 行 × 第2〜5波・`0 < x < 100`）と歯止め");
        Console.WriteLine();
        Console.WriteLine("| 版 | 情報セル | 天井(100.0%) | 床(0.0%) | 動いた行 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int v = 0; v < WcV; v++)
        {
            int info = 0, ceil = 0, floor = 0, moved = 0;
            for (int r = 0; r < wcRows.Length; r++)
            {
                int vi = WcIdxOf(r, v);
                if (vi < 0) vi = 0; else if (v != 0) moved++;
                for (int w = 1; w < wcW; w++)
                {
                    double x = WcRate(r, vi, w);
                    if (x >= 100.0) ceil++; else if (x <= 0.0) floor++; else info++;
                }
            }
            if (v != 0 && moved == 0) continue;
            Console.WriteLine($"| {wcVName[v]} | **{info}** | {ceil} | {floor} | {(v == 0 ? wcRows.Length : moved)} |");
        }
        Console.WriteLine();
        var wcPrimIdx = Baseline.PrimaryRows.Select(nm => Array.FindIndex(wcAllRows, b => b.Name == nm))
                                            .Where(i => i >= 0).ToArray();
        double wcFifth = wcPrimIdx.Select(i => wcBase[i][wcW - 1]).Average();
        var wcPrimNata2 = wcPrimIdx.Where(i => wcAllRows[i].F.Occupied().Any(o => o.Def.Id == UnitCatalog.Nata.Id)).ToArray();
        Console.WriteLine($"**歯止め**: 主判定 {wcPrimIdx.Length} 行の第五波平均 **{wcFifth:F1}%**、"
                          + $"線 {Baseline.PrimaryFifthFloor:F1}% との余裕 **{wcFifth - Baseline.PrimaryFifthFloor:F1}pt**。");
        Console.WriteLine($"**うち刃待ち（ナタ）を含む行は {wcPrimNata2.Length} 行**"
                          + $"——**V1 が動かしうる分母は {wcPrimNata2.Length} / {wcPrimIdx.Length}**"
                          + $"（{(wcPrimNata2.Length == 0 ? "**歯止めは構造的に発動しない**。第61期の自己検査 (a) と同じ形" : "実効レンジを併記すること")}）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {wcSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // draft: ドラフト台 Pw（規則 P × 弱い波。**第73期の写し**）
    // =====================================================================================
    if (wcArg == "draft")
    {
        string wcDName = wcBand == 0 ? "A" : "B";
        var wcDWin = new int[WcN][];
        var wcDBox = new int[WcN];
        var wcDHas = new bool[WcN][];

        int wcDone = 0;
        Console.Error.Write($"{wcDName}帯 Pw: ");
        Parallel.For(0, WcN, i =>
        {
            UnitDef[] team = WcTeam(i);
            int[] seats = WcSeats(team);
            var ids = team.Select(x => x.Id).ToHashSet();
            var has = new bool[WcV];
            for (int v = 0; v < WcV; v++) has[v] = WcHas(v, ids);
            var win = new int[WcV * wcW];
            for (int v = 0; v < WcV; v++)
            {
                if (!has[v]) continue;
                Formation f = WcDraftForm(team, seats, WcMapOf(v, ids));
                SeverRule rule = WcRuleOf(v);
                for (int w = 0; w < wcW; w++)
                    for (int seed = wcBand; seed < wcBand + WcM; seed++)
                        if (BattleEngine.Run(f, wcWeak[w].Enemy, seed, verbose: false, sever: rule).PlayerWon)
                            win[v * wcW + w]++;
            }
            wcDWin[i] = win; wcDHas[i] = has;
            wcDBox[i] = Math.Min(3, WcWoundCount(team));
            int c = Interlocked.Increment(ref wcDone);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        double WcDRate(int i, int v, int w)
            => (wcDHas[i][v] ? wcDWin[i][v * wcW + w] : wcDWin[i][w]) * 100.0 / WcM;
        double[] WcDRates(int v, int wave)
            => Enumerable.Range(0, WcN).Select(i => wave < 0
                ? Enumerable.Range(1, wcW - 1).Average(w => WcDRate(i, v, w))
                : WcDRate(i, v, wave)).ToArray();

        Console.WriteLine($"# 第74期 —— 傷の代金を分離する・ドラフト台 Pw（{wcDName} 帯 seed {wcBand}..{wcBand + WcM - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 wcost draft{(wcBand == 0 ? "" : " alt")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"標本 **{WcN:N0}** × 版 {WcV} × {wcW} 波 × seed **{WcM}** 本（規則 P・弱い波 {WcWeakPct}%）。");
        Console.WriteLine("**箱（傷の枚数）は必ず元の5枚で決める。判定は第2〜5波。**");
        Console.WriteLine();
        Console.WriteLine("**版 0〜7 は V0（＝第74期の採用前の待ち方）の規則で回してある**——第73期の表と直接比べるため。");
        Console.WriteLine("**採用後の現行は V1（版 8）。**規則は版ごとに明示していて `SeverRule.Default` を読んでいない。");
        Console.WriteLine();
        Console.WriteLine($"箱: **0枚 {wcDBox.Count(x => x == 0):N0} / 1枚 {wcDBox.Count(x => x == 1):N0} / "
                          + $"2枚 {wcDBox.Count(x => x == 2):N0} / 3+ {wcDBox.Count(x => x == 3):N0}**"
                          + "（第73期 §4-1 の 5299 / 4706 / 945 / 50 と突き合わせる）");
        Console.WriteLine();
        Console.Write("在席: ");
        Console.WriteLine(string.Join(" / ", Enumerable.Range(0, 5).Select(k =>
            $"{wcFive[k].Def.Name} {Enumerable.Range(0, WcN).Count(i => wcDHas[i][1 + k]):N0}")));
        Console.WriteLine();

        Console.WriteLine("## 表F（ドラフト側）—— 傾きの分解");
        Console.WriteLine();
        Console.WriteLine("| 版 | 0枚 | 1枚 | 2枚 | 3+ | 第2波 | 第3波 | 第4波 | 第5波 | **集計** | Δ（V0比） | 動いた標本 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var wcDSlope = new double[WcV];
        double wcDSlope0 = double.NaN;
        for (int v = 0; v < WcV; v++)
        {
            var rate = WcDRates(v, -1);
            double[] bm = WcBoxMeans(rate, wcDBox);
            double sl = WcSlope(rate, wcDBox);
            wcDSlope[v] = sl;
            if (v == 0) wcDSlope0 = sl;
            int moved = Enumerable.Range(0, WcN).Count(i => wcDHas[i][v]);
            Console.WriteLine($"| {wcVName[v]} | {bm[0]:F2}% | {bm[1]:F2}% | {bm[2]:F2}% | {(double.IsNaN(bm[3]) ? "—" : $"{bm[3]:F2}%")} "
                              + string.Concat(Enumerable.Range(1, wcW - 1).Select(w => $"| {WcP1(WcSlope(WcDRates(v, w), wcDBox))} "))
                              + $"| **{WcP1(sl)}** | {(v == 0 ? "—" : WcP2(sl - wcDSlope0))} | {(v == 0 ? WcN : moved):N0} |");
        }
        Console.WriteLine();

        Console.WriteLine("### Q2 —— 分解は閉じたか");
        Console.WriteLine();
        Console.WriteLine("| 量 | 傾き | Δ（V0比） |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 現行 | {WcP1(wcDSlope[0])} | — |");
        Console.WriteLine($"| M1〜M5 の Δ の単純和 | — | {WcP2(Enumerable.Range(1, 5).Sum(v => wcDSlope[v] - wcDSlope0))} |");
        Console.WriteLine($"| **M-all** | {WcP1(wcDSlope[6])} | **{WcP2(wcDSlope[6] - wcDSlope0)}** |");
        Console.WriteLine($"| 傷の駒を全部素体 | {WcP1(wcDSlope[7])} | {WcP2(wcDSlope[7] - wcDSlope0)} |");
        Console.WriteLine($"| **差（M-all − 全部素体）** | **{WcP2(wcDSlope[6] - wcDSlope[7])}** | 線 1.5pt |");
        Console.WriteLine();

        Console.WriteLine("## 表D（ドラフト側）—— 代金の帰属（**在席した標本だけで比べる**）");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | マイナス | 在席 | 在席時の勝率（V0） | 同（代金なし） | **帰属** | **傾きの変化** |");
        Console.WriteLine("|--:|---|---|--:|--:|--:|--:|--:|");
        for (int k = 0; k < 5; k++)
        {
            var idx = Enumerable.Range(0, WcN).Where(i => wcDHas[i][1 + k]).ToArray();
            if (idx.Length == 0) { Console.WriteLine($"| M{k + 1} | {wcFive[k].Def.Name} | {wcFive[k].Minus} | 0 | — | — | — | — |"); continue; }
            double a = idx.Average(i => Enumerable.Range(1, wcW - 1).Average(w => WcDRate(i, 0, w)));
            double b = idx.Average(i => Enumerable.Range(1, wcW - 1).Average(w => WcDRate(i, 1 + k, w)));
            Console.WriteLine($"| M{k + 1} | {wcFive[k].Def.Name} | {wcFive[k].Minus} | {idx.Length:N0} | {a:F2}% | {b:F2}% "
                              + $"| **{WcP2(b - a)}** | **{WcP2(wcDSlope[1 + k] - wcDSlope0)}** |");
        }
        Console.WriteLine();
        Console.WriteLine("**帰属が正 ＝「その代金を外すと強くなる」**（＝実在する代金）。");
        Console.WriteLine();

        Console.WriteLine("## Q4 / Q5 —— 断ちの待ち方（**主判定**）");
        Console.WriteLine();
        {
            var idx = Enumerable.Range(0, WcN).Where(i => wcDHas[i][8]).ToArray();
            Console.WriteLine("| 版 | 在席時の勝率 | Δ（V0比） | **傾き** | **傾きの改善** | 上限 +8.3 に対する割合 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|");
            double a0 = idx.Average(i => Enumerable.Range(1, wcW - 1).Average(w => WcDRate(i, 0, w)));
            foreach (int v in new[] { 0, 3, 8, 9 })
            {
                double a = idx.Average(i => Enumerable.Range(1, wcW - 1).Average(w => WcDRate(i, v, w)));
                double d = wcDSlope[v] - wcDSlope0;
                Console.WriteLine($"| {wcVName[v]} | {a:F2}% | {(v == 0 ? "—" : WcP2(a - a0))} | {WcP1(wcDSlope[v])} "
                                  + $"| {(v == 0 ? "—" : WcP2(d))} | {(v == 0 ? "—" : $"{100 * d / 8.3:F0}%")} |");
            }
            Console.WriteLine();
            Console.WriteLine($"分母（ナタが在席した標本）= **{idx.Length:N0} / {WcN:N0}**。**線は傾きの改善 +2.0pt。**");
        }
        Console.WriteLine();

        // ---- 待ち方の機構（小標本・verbose）----
        Console.WriteLine("## 表G（ドラフト側）—— 待ち方が何を動かしたか（**ナタを含む先頭 300 標本 × 弱い波 × seed 2 本**）");
        Console.WriteLine();
        var wcSample = new List<int>();
        for (int i = 0; i < WcN && wcSample.Count < 300; i++) if (wcDHas[i][8]) wcSample.Add(i);
        int[] wcGVer = { 0, 8, 9 };
        var wcGAcc = new double[wcGVer.Length][];
        for (int g = 0; g < wcGVer.Length; g++)
        {
            var part = new double[wcSample.Count][];
            int gi = g;
            Parallel.For(0, wcSample.Count, si =>
            {
                int i = wcSample[si];
                UnitDef[] team = WcTeam(i);
                int[] seats = WcSeats(team);
                Formation f = WcDraftForm(team, seats, null);
                UnitDef?[] ob = WcBoard(f);
                var a = new double[WcC + 1];
                for (int w = 0; w < wcW; w++)
                    for (int seed = wcBand; seed < wcBand + 2; seed++)
                        WcCount(BattleEngine.Run(f, wcWeak[w].Enemy, seed, verbose: true, sever: WcRuleOf(wcGVer[gi])),
                                f.Count, ob, a);
                part[si] = a;
            });
            wcGAcc[g] = new double[WcC + 1];
            for (int si = 0; si < wcSample.Count; si++)
                for (int c = 0; c <= WcC; c++) wcGAcc[g][c] += part[si][c];
        }
        Console.WriteLine("| 版 | 決着T | ナタの振/戦 | **断ち発火/戦** | 読んだ傷/発火 | **放棄T** | **待ちT** | 捨てたT合計 | 在庫/T | 書込/戦 | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int g = 0; g < wcGVer.Length; g++)
        {
            double[] a = wcGAcc[g];
            double n = a[13];
            Console.WriteLine($"| {wcVName[wcGVer[g]]} | {a[15] / n:F2} | {a[19] / n:F2} | **{a[6] / n:F2}** "
                              + $"| {(a[6] > 0 ? $"{a[7] / a[6]:F2}" : "—")} | {a[10] / n:F2} | {a[11] / n:F2} "
                              + $"| **{(a[10] + a[11]) / n:F2}** | {a[14] / Math.Max(1, a[15]):F2} | {(a[0] + a[1]) / n:F2} "
                              + $"| {100 * a[WcC] / n:F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {wcSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("wcost: 引数は phase0 / check / （無し）/ alt / draft [alt]。");
    return;
}
}
