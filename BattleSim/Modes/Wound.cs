using BattleCore;
using static Common;

// =====================================================================================
// wound モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "wound")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 wound
// =====================================================================================

static class WoundDiag
{
// =========================================================================================
// wound モード（第73期・調査）: **傷は「使われていない」のか、「使うと損」なのか。**
//
// 第57期（燃焼）・第65期（強化）と同じ形の解剖で、**この期は新しい機構を1つも作らない。**
// `Traits.cs` / `BattleEngine.cs` / `UnitCatalog` / `EnemyCatalog.Stages` / `CompareBuilds()` は
// **1行も動かさない**。変種はすべて診断のローカルの `UnitDef`（`gradient` / `aim` / `route` と同じ扱い）。
//
// 計数は**ログの文字列**から取る（`gullet log` / `yoke log` / `hush` / `sever` / `suture` と同じ理由）
// ——「振ったが在庫が 0 で読めなかった」は盤面の値に痕跡を1つも残さない。
// 在庫（ターン頭の傷の総数）だけは `Events` の `StatusSnapshot` から取る。
//
// 立てた仮説（**測る前に指示書 §0-2 で固定**。測って否定してよい）:
//   H1 未接続説 —— 傷は書かれる量が足りない。読み手はいるが在庫が無いので発火しない
//   H2 消費競合説 —— 在庫はあるが、消費型（断ち・縫い）が維持型（抉り・刻み）の前提を壊す
//   H3 代金説 —— 5枚のマイナスが重すぎる。傾き −15 は軸の失敗ではなくマイナスの合計
//
//     dotnet run --project BattleSim -c Release 0 wound phase0     # 窓口一覧と紙の計算（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 wound            # 理想台の主表（A 帯 seed 0..199）
//     dotnet run --project BattleSim -c Release 0 wound alt        # 同じ表を B 帯（seed 200..399）
//     dotnet run --project BattleSim -c Release 0 wound draft      # ドラフト台（A 帯 seed 0..7）
//     dotnet run --project BattleSim -c Release 0 wound draft alt  # 同じ表を B 帯（seed 200..207）
//     dotnet run --project BattleSim -c Release 0 wound check      # 陰性対照（Q6）
public static void Run(string[] args, int stageIndex)
{
    string wdArg = args.Length > 2 ? args[2] : "";
    string wdArg2 = args.Length > 3 ? args[3] : "";
    var wdSw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> wdStages = EnemyCatalog.Stages;
    int wdW = wdStages.Count;
    var wdRoster = UnitCatalog.All.ToArray();
    int wdRN = wdRoster.Length;
    var wdKeyOf = wdRoster.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);
    const int WdKW = UnitTally.CarryWound;

    const int WdSeeds = 200;                 // 理想台の seed 本数（`compare` と同じ）
    int wdSeed0 = wdArg == "alt" ? 200 : 0;

    // ---- 傷の5枚（**役は Phase 0-1 の窓口一覧から引く。役割名で数えない**）------------------
    var wdFive = new (UnitDef Def, string Role, string Minus, bool StockDep, string Site)[]
    {
        (UnitCatalog.Kiri, "書き手",           "与ダメは常に1",                    false, "`RendTrait.ModifyAttack` Traits.cs:3619"),
        (UnitCatalog.Nomi, "書き手＋維持読み", "執着（倒れるまで他を見ない）",      false, "`FixateTrait` Traits.cs:4049 / engine 1718"),
        (UnitCatalog.Egu,  "維持読み（攻）",   "倒すと次の手番を失う",              false, "`GougeTrait.OnKill` Traits.cs:3683"),
        (UnitCatalog.Nata, "消費読み",         "最深の傷が2に届くまで手番を捨てる", true,  "`SeverTrait.CanAct` Traits.cs:3901"),
        (UnitCatalog.Hari, "消費読み（防）",   "繕うたび敵の傷が1つ塞がる",         true,  "`SutureTrait` Traits.cs:4005"),
    };
    var wdIds = wdFive.Select(x => x.Def.Id).ToArray();
    var wdIdSet = wdIds.ToHashSet();
    var wdConsumers = new[] { UnitCatalog.Nata.Id, UnitCatalog.Hari.Id };

    // ---- 変種（診断のローカル。`UnitCatalog` は1行も触らない）--------------------------------
    UnitDef WdPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    UnitDef WdPlainAtk(UnitDef d, int atk) => new()
    {
        Id = d.Id + "_plain" + atk, Name = $"素体の{d.Name}(攻{atk})",
        MaxHp = d.MaxHp, Attack = atk, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    UnitDef WdRetrait(UnitDef d, string tag, params TraitId[] tr) => new()
    {
        Id = d.Id + "_" + tag, Name = d.Name + "・" + tag,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = tr, Pattern = d.Pattern
    };

    // **`TraitCatalog.Resolve` は重複を潰さない**（`ids.Select(Get).ToList()`）ので、
    // 同じ `TraitId` を2つ並べると `OnAfterAttack` が2回走る＝傷が1振りで2つ入る。
    // **裂きは `ModifyAttack` が引数を読まずに 1 を返す**ので、二重にしても打点は 1 のまま
    // ——**供給だけを1点動かす完全にクリーンなノブ**（`check` の (3) で監査する）。
    // 刻みの二重は**なぞりも二重に走る**ので汚れている（表C に明記する）。
    // **`TraitId.ThinBlade` を必ず並べること**（第74期）。第74期に「与ダメ常に1」を
    // `RendTrait` から `ThinBladeTrait` へ切り出したので、`{ Rend, Rend }` だけにすると
    // **打点が 1 から素の 12 に戻って対照が汚れる**（供給だけでなく出力も動く）。
    // **特性を明示列挙して作る診断のローカル変種は、分割のたびに見直すこと。**
    UnitDef wdKiriDbl = WdRetrait(UnitCatalog.Kiri, "裂き二重", TraitId.Rend, TraitId.Rend, TraitId.ThinBlade);
    UnitDef wdNomiDbl = WdRetrait(UnitCatalog.Nomi, "刻み二重", TraitId.Carve, TraitId.Carve, TraitId.Fixate);
    UnitDef wdNomiNoFix = WdRetrait(UnitCatalog.Nomi, "執着なし", TraitId.Carve);

    Formation WdSub(Formation f, IReadOnlyDictionary<string, UnitDef> map)
    {
        Formation g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (map.TryGetValue(d.Id, out UnitDef? nd)) g[slot] = nd;
        return g;
    }
    Formation WdSub1(Formation f, string id, UnitDef with)
        => WdSub(f, new Dictionary<string, UnitDef> { [id] = with });

    // ---- 統計の道具（第69〜72期の写し）-------------------------------------------------------
    string WdP1(double x) => double.IsNaN(x) ? "—" : (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    string WdP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");

    // 傾き = （1枚 − 0枚）と（2枚 − 1枚）の平均。**第71期 表E・第72期 表D と同じ定義。**
    // 箱は 0 / 1 / 2 / 3+ の4つで、**傾きが読むのは前3つだけ**（3+ は表示のみ）。
    double WdSlope(IReadOnlyList<double> rate, IReadOnlyList<int> box)
    {
        var bk = new List<double>[4];
        for (int j = 0; j < 4; j++) bk[j] = new List<double>();
        for (int i = 0; i < rate.Count; i++) bk[Math.Min(3, box[i])].Add(rate[i]);
        double d10 = (bk[0].Count > 0 && bk[1].Count > 0) ? bk[1].Average() - bk[0].Average() : double.NaN;
        double d21 = (bk[1].Count > 0 && bk[2].Count > 0) ? bk[2].Average() - bk[1].Average() : double.NaN;
        return double.IsNaN(d10) ? d21 : double.IsNaN(d21) ? d10 : (d10 + d21) / 2;
    }
    double[] WdBoxMeans(IReadOnlyList<double> rate, IReadOnlyList<int> box)
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

    // 「（傷 N」の N を読む。書式は裂き・刻み・抉り・断ち・縫いで共通。
    // **`sever` の `WoundOf` は「（傷 N）」（後ろが空白でない）を読めない**ので、
    // ここは数字を直接走査する形に書き直してある。
    static int WdNum(string t, string prefix)
    {
        int a = t.IndexOf(prefix, StringComparison.Ordinal);
        if (a < 0) return 0;
        a += prefix.Length;
        int b = a;
        while (b < t.Length && char.IsDigit(t[b])) b++;
        return b > a ? int.Parse(t[a..b]) : 0;
    }

    // 1戦ぶんの計数。**盤面を1ビットも触らない**（ログと `Events` を読むだけ）。
    // 添字: 0 裂きの書 / 1 刻みの書 / 2,3 抉りの発火・読んだ傷 / 4,5 なぞり / 6,7 断ち /
    //       8,9 縫い / 10 放棄T / 11 待ちT / 12 深追い / 13 戦数 / 14 在庫Σ / 15 ターンΣ /
    //       16 在庫maxΣ / 17 **味方の席に載った傷** / 18..22 5枚の振り /
    //       24..33 第1〜10ターン頭の在庫Σ / 34..43 そのターンに到達した戦数 / WdC 勝数
    const int WdC = 44;
    const int WdTMax = 10;
    void WdCount(BattleResult res, int pcount, UnitDef?[] ob, double[] a)
    {
        a[WdC] += res.PlayerWon ? 1 : 0;
        a[13]++;
        a[15] += res.Turns;
        for (int k = 0; k < 5; k++)
            if (ob[k] is UnitDef d && res.TallyByUnit.TryGetValue(d.Id, out UnitTally? tl))
                a[18 + k] += tl.Attacks;

        // 在庫（ターン頭の傷の総数）。**味方の席（InstanceId < 味方の初期人数）は別に数える**
        // ——「味方に載る経路が1つも無い」の実測の陰性対照。
        var stock = new Dictionary<int, int>();
        foreach (BattleEvent e in res.Events)
        {
            if (e.Kind != BattleEventKind.StatusSnapshot || e.Text != "傷") continue;
            if (e.TargetId is int tid && tid < pcount) { a[17] += e.Amount; continue; }
            stock[e.Turn] = stock.GetValueOrDefault(e.Turn) + e.Amount;
        }
        a[14] += stock.Values.Sum();
        a[16] += stock.Count > 0 ? stock.Values.Max() : 0;
        // 在庫の時間推移。**分母は「そのターンに到達した戦」**（決着した戦を 0 で埋めない）。
        for (int t = 1; t <= WdTMax && t <= res.Turns; t++)
        { a[24 + t - 1] += stock.GetValueOrDefault(t); a[34 + t - 1]++; }

        string? nKiri = ob[0]?.Name, nNomi = ob[1]?.Name, nEgu = ob[2]?.Name,
                nNata = ob[3]?.Name, nHari = ob[4]?.Name;
        int turn = 0, lastIdle = -1, lastWait = -1;
        foreach (LogLine l in res.Log)
        {
            string t = l.Text;
            if (l.Kind == LogKind.Turn) { turn++; continue; }
            if (nKiri != null && t.Contains(nKiri + " の刃が") && t.Contains("に傷を残した")) { a[0]++; continue; }
            if (nNomi != null && t.Contains(nNomi + " の鑿が")) { a[1]++; continue; }
            if (nEgu != null && t.Contains("の傷をこじ開ける") && t.Contains(nEgu + " が "))
            { a[2]++; a[3] += WdNum(t, "（傷 "); continue; }
            if (nNomi != null && t.Contains("の古い傷をなぞる") && t.Contains(nNomi + " が "))
            { a[4]++; a[5] += WdNum(t, "（傷 "); continue; }
            if (nNata != null && t.Contains("の傷をまとめて断つ") && t.Contains(nNata + " が "))
            { a[6]++; a[7] += WdNum(t, "（傷 "); continue; }
            if (nHari != null && t.Contains("の傷口から糸を引き") && t.Contains(nHari + " が "))
            { a[8]++; a[9] += WdNum(t, "（傷 "); continue; }
            // 放棄・待ちは**1ターンに1回だけ**（`CanAct` は `Trait.SurrenderedTurn` からも呼ばれる。第38期）
            if (nNata != null && t.Contains(nNata + " は閉じた肌に刃を下ろさない"))
            { if (turn != lastIdle) { a[10]++; lastIdle = turn; } continue; }
            if (nNata != null && t.Contains(nNata + " は傷がまだ浅いと刃を上げない"))
            { if (turn != lastWait) { a[11]++; lastWait = turn; } continue; }
            if (nEgu != null && t.Contains(nEgu + " は ") && t.Contains("の裂け目に踏み込みすぎた")) { a[12]++; continue; }
        }
    }
    // =====================================================================================
    // ドラフト台の器具（第69〜72期の写し。**1文字も変えていない**）
    // =====================================================================================
    const int WdOfferSeed = 2_000_000;       // 提示・選択の系列（第70〜72期と同じ1本）
    const int WdN = 11000, WdM = 8;          // 標本数 / 1標本あたりの戦闘 seed 数
    const int WdStrong = 7;                  // 規則 P が使う「殴れる駒」の線
    const int WdWeakPct = 60;                // 弱い波: 敵の MaxHp をこの %（切り捨て）
    int wdBand = wdArg2 == "alt" ? 200 : 0;

    // 第71期 表E（Pw・A 帯）の傾き。**規則 S' の唯一の判断材料**（第72期 `D4Slope100` の写し）。
    int[] wdSlope100 = { -90, -650, 190, 653, -1256, -330, -796, -1557, -759, 1936, 981 };
    var wdSlopeOf = wdRoster.ToDictionary(u => u.Id, u => wdKeyOf[u.Id].Sum(k => wdSlope100[k]));

    var wdWeakCache = new Dictionary<string, UnitDef>();
    UnitDef WdWeakOf(UnitDef d)
    {
        if (wdWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * WdWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        wdWeakCache[d.Id] = w;
        return w;
    }
    var wdWeak = wdStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = WdWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    // 規則 P（素朴・第70〜72期と同一）と S'（傾き志向・第72期）。**rule: 1 = P / 3 = S'**
    UnitDef[] WdTeam(int rule, int i)
    {
        var rng = new Random(WdOfferSeed + i);
        var idx = new int[wdRN];
        for (int k = 0; k < wdRN; k++) idx[k] = k;
        int remain = wdRN, strong = 0;
        var picked = new UnitDef[5];
        for (int r = 0; r < 5; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = wdRoster[idx[t]];
            }
            UnitDef sel = rule == 1
                ? (strong < 2
                    ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                    : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First())
                : offer.OrderByDescending(x => wdSlopeOf[x.Id]).ThenByDescending(x => x.Attack)
                       .ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= WdStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(wdRoster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    // 規則配置 H（第69期 §1-3 の写し）。**席は必ず元の5枚から決める**
    // ——素体は `Id` が変わるので、差し替えてから並べるとタイブレークが動く（第72期 `D4Form` と同じ作法）。
    int[] WdSeats(UnitDef[] u)
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
    Formation WdDraftForm(UnitDef[] u, int[] seats, IReadOnlyDictionary<string, UnitDef>? map)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
            f[seats[k]] = (map is not null && map.TryGetValue(u[k].Id, out UnitDef? nd)) ? nd : u[k];
        return f;
    }
    int WdWoundCount(IEnumerable<UnitDef> team)
        => team.Count(x => wdKeyOf.TryGetValue(x.Id, out int[]? k) && k.Contains(WdKW));
    // 理想61行のうち「傷の駒を含む行」。**行は選ばない**（含む行を全部）。
    var wdAllRows = CompareBuilds();
    var wdRows = wdAllRows.Where(b => b.F.Occupied().Any(o => wdIdSet.Contains(o.Def.Id))).ToArray();

    // =====================================================================================
    // phase0: 窓口一覧と紙の計算（**戦闘を1回も回さない**）
    // =====================================================================================
    if (wdArg == "phase0")
    {
        Console.WriteLine("# 第73期 Phase 0 —— 測る前に、コードと紙で");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `dotnet run --project BattleSim -c Release 0 wound phase0`");
        Console.WriteLine();

        // ---- 0-1. 窓口一覧（call-site。役割名で数えない）----
        Console.WriteLine("## 0-1. 表A —— 傷（`StatusKeys.Wound`）の窓口一覧（**call-site grep**）");
        Console.WriteLine();
        Console.WriteLine("| # | 分類 | 駒 | 窓口（ファイル:行） | 何をするか |");
        Console.WriteLine("|--:|---|---|---|---|");
        Console.WriteLine("| 1 | **書き手** | 裂きのキリ | `RendTrait.OnAfterAttack` Traits.cs:3633 | `SetCounter(Wound, w+1)`。主目標のみ・攻撃1回に1度・死体には刻まない |");
        Console.WriteLine("| 2 | **書き手** | 刻みのノミ | `CarveTrait.OnAfterAttack` Traits.cs:3742 | 同上（**読んだ後に**書く。順序が仕様） |");
        Console.WriteLine("| 3 | 維持型の読み手 | 抉りのエグ | `GougeTrait.OnAfterAttack` Traits.cs:3668 | 傷1つにつき +3（加算）。**消費しない** |");
        Console.WriteLine("| 4 | 維持型の読み手 | 刻みのノミ | `CarveTrait.OnAfterAttack` Traits.cs:3730 | 傷1つにつき +2（加算）。**消費しない** |");
        Console.WriteLine("| 5 | **消費型の読み手** | 断ちのナタ | `SeverTrait.OnAfterAttack` Traits.cs:3927 / **消費 3936** | 傷1つにつき +5 →**全部 0 に戻す** |");
        Console.WriteLine("| 6 | **消費型の読み手** | 縫いのハリ | `SutureTrait.OnAfterAttack` Traits.cs:3991 / **消費 4005** | 傷1つにつき味方を +3 回復 →**1つ塞ぐ** |");
        Console.WriteLine("| 7 | 読み（発火条件） | 断ちのナタ | `SeverTrait.DeepestWound` Traits.cs:3847 → `CanAct` 3901 | 最深の傷が `Threshold`(2) 未満なら**振らない** |");
        Console.WriteLine("| 8 | 読み（標的選好） | ナタ・ハリ | `SeverTrait.Preferred` Traits.cs:3863 ← **engine 1735** | `SelectTargetChain` の執着と同じ段 |");
        Console.WriteLine("| 9 | 帳簿（**盤面を動かさない**） | — | `BattleEngine.NoteStatusGain` 1238 | `CarryCount[傷]` に 1 を足すだけ（第68期） |");
        Console.WriteLine("| 10 | 表示（**盤面を動かさない**） | — | `BattleEngine.StatusLabels` 1614 / `StatusKeys` 44・51・66 | 台本と会戦の消去一覧 |");
        Console.WriteLine();
        Console.WriteLine("**engine は傷を1箇所も読まない**（9・10 は帳簿と表示で、分岐に使われていない）。");
        Console.WriteLine("第50期に固定した「engine の窓口を持つ通貨は 9 / 10。**傷だけが持たない**」は現在も正しい。");
        Console.WriteLine();
        Console.WriteLine($"**書き手 2 / 維持型の読み手 2 / 消費型の読み手 2**（駒で数えると 5 枚。ノミが書きと読みを兼ねる）。");
        Console.WriteLine();

        // ---- 0-2. 味方に載る経路（コードで確認）----
        Console.WriteLine("## 0-2. 「味方に載る経路が1つも無い」をコードで確認する");
        Console.WriteLine();
        Console.WriteLine("| 確認 | 結果 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 書き手が書く相手 | `OnAfterAttack(ctx, self, **target**, dealt)` の `target` **だけ**。両方とも `target.SetCounter` 以外に `Wound` を書く行が無い |");
        Console.WriteLine("| 範囲の巻き込み | engine は `OnAfterAttack` を**主目標に1度しか呼ばない**（BattleEngine.cs:2058）ので、薙ぎ・貫きでも副次目標に傷は乗らない |");
        Console.WriteLine("| 味方への再分配 | 分かち・巨躯・庇う・後備え・棘守りはすべて `ApplyDamage` の層で、**傷のカウンタに触らない**（`Wound` の grep は Traits.cs の6箇所と engine の帳簿2箇所だけ） |");
        Console.WriteLine("| 状態の移し替え | 業（`ScapegoatTrait`）は `UnitCatalog.All` に載っていない（第49期に棄却） |");
        var wdFoeWound = new[] { TraitId.Rend, TraitId.Carve, TraitId.Gouge, TraitId.Sever, TraitId.Suture };
        var wdFoeHas = wdStages.SelectMany(st => st.Enemy.Occupied()).Select(o => o.Def)
                               .Where(d => d.Traits.Any(t => wdFoeWound.Contains(t))).Select(d => d.Name).Distinct().ToArray();
        Console.WriteLine($"| 敵側の保持者 | **{wdFoeHas.Length} 体**（`Stages` の全敵を走査）——敵は傷を書きも読みもしない |");
        Console.WriteLine();
        Console.WriteLine("**実測の陰性対照は主表で取る**（`StatusSnapshot` の傷が味方の `InstanceId` に1件も出ないこと）。");
        Console.WriteLine();

        // ---- 0-3. 5枚のマイナスと在庫依存 ----
        Console.WriteLine("## 0-3. 5枚のマイナスは「傷の在庫」に依存するか（**H3 の分母**）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 攻 | HP | 速 | 役 | マイナス | 窓口 | **在庫依存** | この期で分離できるか |");
        Console.WriteLine("|---|--:|--:|--:|---|---|---|:-:|---|");
        Console.WriteLine($"| {UnitCatalog.Kiri.Name} | {UnitCatalog.Kiri.Attack} | {UnitCatalog.Kiri.MaxHp} | {UnitCatalog.Kiri.Speed} | 書き手 | 与ダメは常に1 | `RendTrait.ModifyAttack` 3619 | × | **できない**（同じ `Trait`）。代金は**素体(攻12) − 素体(攻1)** で測る |");
        Console.WriteLine($"| {UnitCatalog.Nomi.Name} | {UnitCatalog.Nomi.Attack} | {UnitCatalog.Nomi.MaxHp} | {UnitCatalog.Nomi.Speed} | 書き＋維持読み | 執着 | **`TraitId.Fixate`（別の特性）** | × | **できる**（`Traits = {{Carve}}` の版） |");
        Console.WriteLine($"| {UnitCatalog.Egu.Name} | {UnitCatalog.Egu.Attack} | {UnitCatalog.Egu.MaxHp} | {UnitCatalog.Egu.Speed} | 維持読み | 倒すと次の手番を失う | `GougeTrait.OnKill` 3683 | ×（撃破に依存） | **できない**。**回数**を出す |");
        Console.WriteLine($"| {UnitCatalog.Nata.Name} | {UnitCatalog.Nata.Attack} | {UnitCatalog.Nata.MaxHp} | {UnitCatalog.Nata.Speed} | 消費読み | 傷が2つ開くまで手番を捨てる | `SeverTrait.CanAct` 3901 | **○** | **できない**（`Threshold` は `const`）。**回数**を出す |");
        Console.WriteLine($"| {UnitCatalog.Hari.Name} | {UnitCatalog.Hari.Attack} | {UnitCatalog.Hari.MaxHp} | {UnitCatalog.Hari.Speed} | 消費読み（防） | 繕うたび敵の傷が1つ塞がる | `SutureTrait` 4005 | **○** | **できない**。**塞いだ数**を出す |");
        Console.WriteLine();
        Console.WriteLine("> **指示書 §2-3 の「5枚それぞれ、マイナスだけを無効にした版」は、5枚中1枚（ノミ）でしか作れない。**");
        Console.WriteLine("> 残り4枚のマイナスは**プラスと同じ `Trait` クラスの中**にあり、");
        Console.WriteLine("> `Traits.cs` を触らずに落とせるのは「別の `TraitId` になっているマイナス」だけである。");
        Console.WriteLine("> **在庫依存のマイナスは2枚（ナタ・ハリ）で、どちらも消費型の読み手**——");
        Console.WriteLine("> **H2 と H3 はこの2枚の上で同じものを指している。**");
        Console.WriteLine();

        // ---- 0-4. 理想61行の傷の駒 ----
        Console.WriteLine($"## 0-4. 理想 {wdAllRows.Length} 行のうち傷の駒を含む行（**指示書の「15 行」は誤り**）");
        Console.WriteLine();
        Console.WriteLine("| # | 行 | 傷の駒 | 枚数 | 書き手 | 維持読み | 消費読み | 5枚以外の同席 |");
        Console.WriteLine("|--:|---|---|--:|--:|--:|--:|---|");
        for (int r = 0; r < wdRows.Length; r++)
        {
            var defs = wdRows[r].F.Occupied().Select(o => o.Def).ToArray();
            var mine = defs.Where(d => wdIdSet.Contains(d.Id)).ToArray();
            int wr = mine.Count(d => d.Id == UnitCatalog.Kiri.Id || d.Id == UnitCatalog.Nomi.Id);
            int keep = mine.Count(d => d.Id == UnitCatalog.Egu.Id || d.Id == UnitCatalog.Nomi.Id);
            int cons = mine.Count(d => wdConsumers.Contains(d.Id));
            Console.WriteLine($"| {r + 1} | {wdRows[r].Name} | {string.Join("・", mine.Select(d => d.Name))} | {mine.Length} "
                              + $"| {wr} | {keep} | {cons} | {string.Join("・", defs.Where(d => !wdIdSet.Contains(d.Id)).Select(d => d.Name))} |");
        }
        Console.WriteLine();
        var wdBox61 = wdAllRows.Select(b => Math.Min(3, WdWoundCount(b.F.Occupied().Select(o => o.Def)))).ToArray();
        Console.WriteLine($"**傷の駒を含む行は {wdRows.Length} 行**（指示書 §0-1 は「15 行」と書いている）。"
                          + $"枚数の箱は **0枚 {wdBox61.Count(x => x == 0)} / 1枚 {wdBox61.Count(x => x == 1)} / "
                          + $"2枚 {wdBox61.Count(x => x == 2)} / 3+ {wdBox61.Count(x => x == 3)}**"
                          + "——**第72期 表D の 53 / 1 / 7 / 0 と一致する**（器具の検算）。");
        Console.WriteLine();
        Console.WriteLine("**消費型（ナタ・ハリ）を含む行は "
                          + $"{wdRows.Count(b => b.F.Occupied().Any(o => wdConsumers.Contains(o.Def.Id)))} 行しか無い**"
                          + "——**H2（消費競合）を理想台で測れる行はこれだけ。**");
        Console.WriteLine();

        // ---- 0-5. 紙の計算（超幾何と抽選）----
        double Comb(int n, int r2)
        {
            if (r2 < 0 || r2 > n) return 0;
            double v = 1;
            for (int k = 0; k < r2; k++) v = v * (n - k) / (k + 1);
            return v;
        }
        Console.WriteLine("## 0-5. 紙の計算 —— ドラフト台に傷の駒は何枚入るか（**戦闘0回**）");
        Console.WriteLine();
        int wdHold = wdRoster.Count(u => wdKeyOf[u.Id].Contains(WdKW));
        Console.WriteLine($"傷キーの保持者は **{wdHold} / {wdRN} 体**（{wdHold * 100.0 / wdRN:F1}%）。無作為5枚なら超幾何:");
        Console.WriteLine();
        Console.WriteLine("| 枚数 | 0 | 1 | 2 | 3 | 4 | 5 | 期待値 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        Console.Write("| 超幾何（無作為） |");
        double wdEv = 0;
        for (int k = 0; k <= 5; k++)
        {
            double pk = Comb(wdHold, k) * Comb(wdRN - wdHold, 5 - k) / Comb(wdRN, 5);
            wdEv += k * pk;
            Console.Write($" {pk * 100:F1}% |");
        }
        Console.WriteLine($" **{wdEv:F2}** |");

        const int WdSim = 100000;
        var wdDist = new int[2][]; var wdConsDist = new int[2][];
        var wdBoth = new int[2]; var wdKeepOnly = new int[2];
        var wdSeat = new int[2][];
        for (int q = 0; q < 2; q++) { wdDist[q] = new int[6]; wdConsDist[q] = new int[6]; wdSeat[q] = new int[5]; }
        for (int i = 0; i < WdSim; i++)
            for (int q = 0; q < 2; q++)
            {
                var u = WdTeam(q == 0 ? 1 : 3, i);
                int n = WdWoundCount(u);
                int c = u.Count(x => wdConsumers.Contains(x.Id));
                wdDist[q][n]++; wdConsDist[q][c]++;
                if (n >= 1 && c >= 1) wdBoth[q]++;
                if (n >= 1 && c == 0) wdKeepOnly[q]++;
                for (int k = 0; k < 5; k++)
                {
                    int at = Array.IndexOf(wdIds, u[k].Id);
                    if (at >= 0) wdSeat[q][at]++;
                }
            }
        for (int q = 0; q < 2; q++)
        {
            Console.Write($"| 規則 {(q == 0 ? "P（素朴）" : "S'（傾き志向）")} |");
            for (int k = 0; k <= 5; k++) Console.Write($" {wdDist[q][k] * 100.0 / WdSim:F1}% |");
            Console.WriteLine($" **{wdDist[q].Select((c, k) => (double)c * k).Sum() / WdSim:F2}** |");
        }
        Console.WriteLine();
        Console.WriteLine("| 規則 | 傷 ≥1 の標本 | うち**消費型も同席**（H2 が成立しうる） | 維持型だけ | 消費型の期待枚数 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int q = 0; q < 2; q++)
            Console.WriteLine($"| {(q == 0 ? "P" : "S'")} | {(WdSim - wdDist[q][0]) * 100.0 / WdSim:F1}% "
                              + $"| **{wdBoth[q] * 100.0 / WdSim:F1}%** | {wdKeepOnly[q] * 100.0 / WdSim:F1}% "
                              + $"| {wdConsDist[q].Select((c, k) => (double)c * k).Sum() / WdSim:F3} |");
        Console.WriteLine();
        Console.WriteLine($"| 規則 | {string.Join(" | ", wdIds.Select((id, k) => wdFive[k].Def.Name))} |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", wdIds.Length)));
        for (int q = 0; q < 2; q++)
            Console.WriteLine($"| {(q == 0 ? "P" : "S'")} の在席（{WdSim:N0} 標本） | "
                              + string.Join(" | ", wdSeat[q].Select(c => $"{c}")) + " |");
        Console.WriteLine();
        Console.WriteLine("> **規則 S' は傷の駒を締め出す**（第72期の申し送り）。**S' の台では傷が測れない**ので、");
        Console.WriteLine("> **主の台は Pw（素朴 × 弱い波・傾き −15.57 の出どころ）にし、S'w は指示書どおり併記する。**");
        Console.WriteLine();

        // ---- 0-6. docs/ の現在値 ----
        string? wdRoot = Directory.GetCurrentDirectory();
        while (wdRoot != null && !File.Exists(Path.Combine(wdRoot, "docs", "balance.md")))
            wdRoot = Path.GetDirectoryName(wdRoot);
        Console.WriteLine("## 0-6. `docs/balance.md` の現在値（**この期の差分は 0 行**）");
        Console.WriteLine();
        if (wdRoot == null) Console.WriteLine("`docs/` が見つからない（リポジトリの外から実行している）。");
        else
        {
            string[] bl = File.ReadAllLines(Path.Combine(wdRoot, "docs", "balance.md"));
            int rows = bl.Count(l => l.StartsWith("| ") && !l.StartsWith("|---") && !l.Contains("編成"));
            Console.WriteLine($"- 行数 {bl.Length} / 表の行 {rows} / 編成 {wdAllRows.Length} × 波 {wdW} = **{wdAllRows.Length * wdW} セル**");
            foreach (string l in bl.Where(l => wdRows.Any(r => l.Contains(r.Name))))
                Console.WriteLine(l);
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {wdSw.Elapsed.TotalSeconds:F1} 秒（**戦闘 0 回**）。");
        return;
    }
    // =====================================================================================
    // 主表（理想台。表B〜F の理想側）
    // =====================================================================================
    if (wdArg.Length == 0 || wdArg == "alt")
    {
        string wdBandName = wdArg == "alt" ? "B" : "A";

        // ---- 行ごとの版（**行は選ばない・席は動かさない・枠は差し替えない**）----------------
        (string Key, string Label, Formation F, UnitDef?[] OnBoard)[] WdMakeVersions(Formation f)
        {
            var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
            var list = new List<(string, string, Formation, UnitDef?[])>();
            UnitDef?[] Board(Formation g)
            {
                var arr = new UnitDef?[5];
                foreach ((int _, UnitDef d) in g.Occupied())
                    for (int k = 0; k < 5; k++)
                        if (d.Id == wdIds[k] || d.Id.StartsWith(wdIds[k] + "_", StringComparison.Ordinal)) arr[k] = d;
                return arr;
            }
            void Add(string key, string label, Formation g) => list.Add((key, label, g, Board(g)));

            Add("full", "現行", f);
            for (int k = 0; k < 5; k++)
                if (ids.Contains(wdIds[k]))
                    Add("p" + wdIds[k], "素体:" + wdFive[k].Def.Name, WdSub1(f, wdIds[k], WdPlain(wdFive[k].Def)));
            var allMap = new Dictionary<string, UnitDef>();
            foreach (var x in wdFive) if (ids.Contains(x.Def.Id)) allMap[x.Def.Id] = WdPlain(x.Def);
            if (allMap.Count >= 1) Add("pAll", "傷の駒を全部素体", WdSub(f, allMap));
            var consMap = new Dictionary<string, UnitDef>();
            foreach (string cid in wdConsumers)
                if (ids.Contains(cid)) consMap[cid] = WdPlain(wdFive.First(x => x.Def.Id == cid).Def);
            if (consMap.Count >= 1) Add("noCons", "**消費型だけ素体**（H2）", WdSub(f, consMap));
            if (ids.Contains(UnitCatalog.Nomi.Id))
            {
                Add("nofix", "**執着を外す**（H3）", WdSub1(f, UnitCatalog.Nomi.Id, wdNomiNoFix));
                Add("dblNomi", "**刻み二重**（H1・汚れあり）", WdSub1(f, UnitCatalog.Nomi.Id, wdNomiDbl));
            }
            if (ids.Contains(UnitCatalog.Kiri.Id))
            {
                Add("kiri1", "**素体:キリ(攻1)**（H3）", WdSub1(f, UnitCatalog.Kiri.Id, WdPlainAtk(UnitCatalog.Kiri, 1)));
                Add("dblKiri", "**裂き二重**（H1・清潔）", WdSub1(f, UnitCatalog.Kiri.Id, wdKiriDbl));
            }
            return list.ToArray();
        }

        var wdVers = wdRows.Select(b => WdMakeVersions(b.F)).ToArray();

        // [行][版][波] → 計数（0..WdC-1）と 勝数（WdC）
        var wdAcc = new double[wdRows.Length][][][];
        for (int r = 0; r < wdRows.Length; r++)
        {
            wdAcc[r] = new double[wdVers[r].Length][][];
            for (int v = 0; v < wdVers[r].Length; v++)
            {
                wdAcc[r][v] = new double[wdW][];
                for (int w = 0; w < wdW; w++) wdAcc[r][v][w] = new double[WdC + 1];
            }
        }

        Console.Error.Write($"{wdBandName}帯 理想台: ");
        var wdJobs = new List<(int R, int V)>();
        for (int r = 0; r < wdRows.Length; r++)
            for (int v = 0; v < wdVers[r].Length; v++) wdJobs.Add((r, v));
        Parallel.ForEach(wdJobs, job =>
        {
            (int r, int v) = job;
            (string key, string label, Formation f, UnitDef?[] ob) = wdVers[r][v];
            int pcount = f.Count;
            for (int w = 0; w < wdW; w++)
                for (int seed = wdSeed0; seed < wdSeed0 + WdSeeds; seed++)
                    WdCount(BattleEngine.Run(f, wdStages[w].Enemy, seed, verbose: true), pcount, ob, wdAcc[r][v][w]);
            Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        // ---- 理想61行のベースライン（**非 verbose**。傾きの 0枚 の箱に要る）--------------------
        var wdBase = new double[wdAllRows.Length][];
        Console.Error.Write($"{wdBandName}帯 61行: ");
        Parallel.For(0, wdAllRows.Length, ri =>
        {
            var a = new double[wdW];
            for (int w = 0; w < wdW; w++)
            {
                int win = 0;
                for (int seed = wdSeed0; seed < wdSeed0 + WdSeeds; seed++)
                    if (BattleEngine.Run(wdAllRows[ri].F, wdStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                a[w] = win * 100.0 / WdSeeds;
            }
            wdBase[ri] = a;
            Console.Error.Write(".");
        });
        Console.Error.WriteLine();

        double WdRate(int r, int v, int w) => wdAcc[r][v][w][WdC] * 100.0 / WdSeeds;
        double WdRate25(int r, int v) => Enumerable.Range(1, wdW - 1).Average(w => WdRate(r, v, w));
        int WdVerOf(int r, string key) => Array.FindIndex(wdVers[r], x => x.Key == key);
        double[] WdSum(int r, int v)   // 5波を合算した計数
        {
            var a = new double[WdC + 1];
            for (int w = 0; w < wdW; w++) for (int c = 0; c <= WdC; c++) a[c] += wdAcc[r][v][w][c];
            return a;
        }

        Console.WriteLine($"# 第73期 —— 傷は使われていないのか、使うと損なのか（理想台・{wdBandName} 帯 seed {wdSeed0}..{wdSeed0 + WdSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 wound{(wdArg == "alt" ? " alt" : "")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"理想 {wdAllRows.Length} 行のうち**傷の駒を含む {wdRows.Length} 行**（行は選んでいない）× 版 "
                          + $"{wdJobs.Count / (double)wdRows.Length:F1}（平均）× {wdW} 波 × seed {WdSeeds} 本。");
        Console.WriteLine("**計数は `verbose` のログと `Events` から取る。盤面は1つも動かしていない。**");
        Console.WriteLine();

        // 検算: verbose の勝率が非 verbose と一致するか
        int wdVmis = 0;
        for (int r = 0; r < wdRows.Length; r++)
        {
            int bi = Array.FindIndex(wdAllRows, b => b.Name == wdRows[r].Name);
            for (int w = 0; w < wdW; w++)
                if (Math.Abs(WdRate(r, 0, w) - wdBase[bi][w]) > 1e-9) wdVmis++;
        }
        Console.WriteLine($"**検算: `verbose` 版と非 `verbose` 版の勝率の食い違い {wdVmis} 件 / {wdRows.Length * wdW}**"
                          + "（イベントを積む処理が盤面を動かしていないこと）。");
        int wdAllyW = 0;
        for (int r = 0; r < wdRows.Length; r++)
            for (int v = 0; v < wdVers[r].Length; v++) wdAllyW += (int)WdSum(r, v)[17];
        Console.WriteLine($"**陰性対照: 味方の席に載った傷 {wdAllyW} 件**"
                          + $"（{wdJobs.Count * wdW * WdSeeds:N0} 戦のターン頭スナップショット全数）。");
        Console.WriteLine();

        // ---- 表B-1 供給と在庫 ----
        Console.WriteLine("## 表B-1 —— 供給と在庫（**現行のみ**・行 × 波）");
        Console.WriteLine();
        Console.WriteLine("`在庫/T` はターン頭の敵側の傷の総数の平均、`在庫max` は1戦の最大の平均。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 勝率 | 決着T | 裂きの書 | 刻みの書 | **書込/戦** | **在庫/T** | 在庫max | 消費/戦 | **書込に対する消費** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int r = 0; r < wdRows.Length; r++)
            for (int w = 0; w < wdW; w++)
            {
                double[] a = wdAcc[r][0][w];
                double n = a[13];
                double write = (a[0] + a[1]) / n;
                double cons = (a[7] + a[8]) / n;          // 断ちは全消費（Σ傷）・縫いは1つずつ（発火回数）
                Console.WriteLine($"| {(w == 0 ? wdRows[r].Name : "")} | 第{w + 1}波 | {WdRate(r, 0, w):F1}% | {a[15] / n:F1} "
                                  + $"| {a[0] / n:F2} | {a[1] / n:F2} | **{write:F2}** | **{a[14] / Math.Max(1, a[15]):F2}** "
                                  + $"| {a[16] / n:F2} | {cons:F2} | {(write > 0 ? $"{100 * cons / write:F1}%" : "—")} |");
            }
        Console.WriteLine();

        // ---- 表B-1' 在庫の時間推移 ----
        Console.WriteLine("### 表B-1' —— 在庫の時間推移（**分母はそのターンに到達した戦**）");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, WdTMax).Select(t => $"T{t}")) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", WdTMax)));
        for (int r = 0; r < wdRows.Length; r++)
        {
            double[] a = WdSum(r, 0);
            Console.WriteLine($"| {wdRows[r].Name} | " + string.Join(" | ", Enumerable.Range(0, WdTMax)
                .Select(t => a[34 + t] > 0 ? $"{a[24 + t] / a[34 + t]:F2}" : "—")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**傷は溜まらない。** 供給は1振り1つ、読み手は毎ターン振る——第53期の標と同じく");
        Console.WriteLine("「供給 → 消費」が1ターンの中で閉じる形になっているかを、この行で読む。");
        Console.WriteLine();

        // ---- 表B-2 読みと空振り（Q1）----
        Console.WriteLine("## 表B-2 —— 読み手の発火と空振り（**主判定 Q1**・5波を合算）");
        Console.WriteLine();
        Console.WriteLine("`空振り率` = 1 − 発火 ÷ 振り。**ナタは振らなかった手番（放棄・待ち）を分母に含めない**");
        Console.WriteLine("——「振ったのに在庫が無かった」と「そもそも振らなかった」は別の量。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 読み手 | 型 | 振/戦 | 発火/戦 | **空振り率** | 読んだ傷/発火 | 出力/戦 | 放棄T/戦 | 待ちT/戦 | 深追い/戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        var wdWhiff = new List<(string Row, string Unit, double Sw, double Fire)>();
        for (int r = 0; r < wdRows.Length; r++)
        {
            double[] a = WdSum(r, 0);
            double n = a[13];
            var readers = new (int Unit, string Type, int Fire, int Sum, double Out)[]
            {
                (2, "維持（攻）", 2, 3, GougeTrait.PerWound * a[3] / n),
                (1, "維持（攻）", 4, 5, CarveTrait.PerWound * a[5] / n),
                (3, "**消費**",   6, 7, SeverTrait.PerWound * a[7] / n),
                (4, "**消費（防）**", 8, 9, SutureTrait.PerWound * a[9] / n),
            };
            foreach ((int u, string ty, int fi, int su, double outp) in readers)
            {
                if (wdVers[r][0].OnBoard[u] is null) continue;
                double sw = a[18 + u] / n, fire = a[fi] / n;
                wdWhiff.Add((wdRows[r].Name, wdFive[u].Def.Name, sw, fire));
                Console.WriteLine($"| {wdRows[r].Name} | {wdFive[u].Def.Name} | {ty} | {sw:F2} | {fire:F2} "
                                  + $"| **{(sw > 0 ? $"{100 * (1 - fire / sw):F1}%" : "—")}** "
                                  + $"| {(a[fi] > 0 ? $"{a[su] / a[fi]:F2}" : "—")} | {outp:F1} "
                                  + $"| {(u == 3 ? $"{a[10] / n:F2}" : "—")} | {(u == 3 ? $"{a[11] / n:F2}" : "—")} "
                                  + $"| {(u == 2 ? $"{a[12] / n:F2}" : "—")} |");
            }
        }
        Console.WriteLine();
        {
            double swAll = wdWhiff.Sum(x => x.Sw), fiAll = wdWhiff.Sum(x => x.Fire);
            double whiff = 100 * (1 - fiAll / swAll);
            int hi = wdWhiff.Count(x => x.Sw > 0 && 1 - x.Fire / x.Sw >= 0.50);
            int lo = wdWhiff.Count(x => x.Sw > 0 && 1 - x.Fire / x.Sw < 0.10);
            Console.WriteLine($"**Q1（H1 未接続説）: 全読み手の空振り率 {whiff:F1}%**"
                              + $"（≥50% の 行×読み手 {hi} / {wdWhiff.Count}・<10% は {lo}）→ "
                              + $"**{(whiff >= 50 ? "H1 成立" : whiff < 10 ? "**H1 は否定される**" : "どちらの線にも掛からない")}**。");
        }
        Console.WriteLine();

        // ---- 表C 対照3種 ----
        Console.WriteLine("## 表C —— 対照3種（消費型を落とす / 代金を外す / 書き手を増やす）");
        Console.WriteLine();
        Console.WriteLine("`Δ` は現行との差（第2〜5波の平均勝率）。**同じ5枚・同じ席で、1点だけ変えた版。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第2〜5波 | **Δ** | 5波 | 書込/戦 | 在庫/T | **維持型の出力/戦** | 消費/戦 | 断ち発火 | 放棄+待ちT |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int r = 0; r < wdRows.Length; r++)
        {
            double b25 = WdRate25(r, 0);
            for (int v = 0; v < wdVers[r].Length; v++)
            {
                double[] a = WdSum(r, v);
                double n = a[13];
                double keepOut = (GougeTrait.PerWound * a[3] + CarveTrait.PerWound * a[5]) / n;
                Console.WriteLine($"| {(v == 0 ? wdRows[r].Name : "")} | {wdVers[r][v].Label} | {WdRate25(r, v):F2}% "
                                  + $"| {(v == 0 ? "—" : WdP2(WdRate25(r, v) - b25))} "
                                  + $"| {Enumerable.Range(0, wdW).Average(w => WdRate(r, v, w)):F2}% "
                                  + $"| {(a[0] + a[1]) / n:F2} | {a[14] / Math.Max(1, a[15]):F2} | **{keepOut:F1}** "
                                  + $"| {(a[7] + a[8]) / n:F2} | {a[6] / n:F2} | {(a[10] + a[11]) / n:F2} |");
            }
        }
        Console.WriteLine();

        // Q2（主判定）
        Console.WriteLine("### Q2（H2 消費競合説・**主判定**）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 維持型の出力/戦（現行） | 同（消費型を素体に） | **倍率** | 在庫/T（現行→対照） | Δ勝率 | 線 1.5 倍 |");
        Console.WriteLine("|---|--:|--:|--:|---|--:|:-:|");
        int wdQ2ok = 0, wdQ2n = 0;
        for (int r = 0; r < wdRows.Length; r++)
        {
            int vi = WdVerOf(r, "noCons");
            if (vi < 0) continue;
            wdQ2n++;
            double[] a0 = WdSum(r, 0), a1 = WdSum(r, vi);
            double o0 = (GougeTrait.PerWound * a0[3] + CarveTrait.PerWound * a0[5]) / a0[13];
            double o1 = (GougeTrait.PerWound * a1[3] + CarveTrait.PerWound * a1[5]) / a1[13];
            double d = WdRate25(r, vi) - WdRate25(r, 0);
            bool ok = o0 > 0 && o1 / o0 >= 1.5;
            if (ok) wdQ2ok++;
            Console.WriteLine($"| {wdRows[r].Name} | {o0:F1} | {o1:F1} | **{(o0 > 0 ? $"{o1 / o0:F2}" : "—")}** "
                              + $"| {a0[14] / Math.Max(1, a0[15]):F2} → {a1[14] / Math.Max(1, a1[15]):F2} | {WdP2(d)} | {(ok ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**維持型の出力が 1.5 倍以上になった行: {wdQ2ok} / {wdQ2n}**"
                          + "（傾きの向きは表F で読む。**両方満たして初めて H2 成立**）。");
        Console.WriteLine();

        // ---- 表D 代金の実額 ----
        Console.WriteLine("## 表D —— 代金の実額（H3 の分解）");
        Console.WriteLine();
        Console.WriteLine("**分離できるのは2枚だけ**（Phase 0-3）。残り3枚は**回数**を出す。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | マイナス | 分離 | 器具 | 行 | **帰属（Δ勝率・第2〜5波）** | 実額（1戦あたり） |");
        Console.WriteLine("|---|---|:-:|---|---|--:|---|");
        for (int r = 0; r < wdRows.Length; r++)
        {
            int a12 = WdVerOf(r, "p" + UnitCatalog.Kiri.Id), a1 = WdVerOf(r, "kiri1");
            if (a12 < 0 || a1 < 0) continue;
            Console.WriteLine($"| {UnitCatalog.Kiri.Name} | 与ダメ常に1 | ○ | 素体(攻12) − 素体(攻1) | {wdRows[r].Name} "
                              + $"| **{WdP2(WdRate25(r, a12) - WdRate25(r, a1))}** | 打点 {UnitCatalog.Kiri.Attack} → 1 |");
        }
        for (int r = 0; r < wdRows.Length; r++)
        {
            int vi = WdVerOf(r, "nofix");
            if (vi < 0) continue;
            Console.WriteLine($"| {UnitCatalog.Nomi.Name} | 執着 | ○ | 執着なし − 現行 | {wdRows[r].Name} "
                              + $"| **{WdP2(WdRate25(r, vi) - WdRate25(r, 0))}** | — |");
        }
        for (int r = 0; r < wdRows.Length; r++)
        {
            double[] a = WdSum(r, 0);
            double n = a[13];
            if (wdVers[r][0].OnBoard[2] is not null)
                Console.WriteLine($"| {UnitCatalog.Egu.Name} | 倒すと次の手番を失う | × | — | {wdRows[r].Name} | — "
                                  + $"| **深追い {a[12] / n:F2} 回**（＝失った手番） |");
            if (wdVers[r][0].OnBoard[3] is not null)
                Console.WriteLine($"| {UnitCatalog.Nata.Name} | 傷が2つ開くまで手番を捨てる | × | — | {wdRows[r].Name} | — "
                                  + $"| **放棄 {a[10] / n:F2} + 待ち {a[11] / n:F2} = {(a[10] + a[11]) / n:F2} T**（決着 {a[15] / n:F1}T の {100 * (a[10] + a[11]) / a[15]:F0}%） |");
            if (wdVers[r][0].OnBoard[4] is not null)
                Console.WriteLine($"| {UnitCatalog.Hari.Name} | 繕うたび敵の傷が塞がる | × | — | {wdRows[r].Name} | — "
                                  + $"| **塞いだ傷 {a[8] / n:F2}**（書込 {(a[0] + a[1]) / n:F2} の {100 * a[8] / (a[0] + a[1]):F0}%） |");
        }
        Console.WriteLine();

        // ---- 表E 犯人 ----
        Console.WriteLine("## 表E —— 犯人（1枚ずつ素体にしたときの Δ）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 役 | 出た行 | 素体にしたときの Δ勝率（第2〜5波・行ごと） | 平均 |");
        Console.WriteLine("|---|---|--:|---|--:|");
        var wdCulprit = new double[5];
        for (int k = 0; k < 5; k++)
        {
            var xs = new List<(string Row, double D)>();
            for (int r = 0; r < wdRows.Length; r++)
            {
                int vi = WdVerOf(r, "p" + wdIds[k]);
                if (vi >= 0) xs.Add((wdRows[r].Name, WdRate25(r, vi) - WdRate25(r, 0)));
            }
            wdCulprit[k] = xs.Count > 0 ? xs.Average(x => x.D) : double.NaN;
            Console.WriteLine($"| {wdFive[k].Def.Name} | {wdFive[k].Role} | {xs.Count} "
                              + $"| {string.Join(" / ", xs.Select(x => $"{x.Row} {WdP1(x.D)}"))} "
                              + $"| **{WdP2(wdCulprit[k])}** |");
        }
        Console.WriteLine();
        Console.WriteLine("**Δ が正 ＝ 「その特性を外したほうが強い」**（＝マイナスが正味で勝っている駒）。");
        Console.WriteLine();

        // ---- 表F 台の比較（理想側）----
        Console.WriteLine("## 表F（理想側）—— 傷の枚数効果の傾き（**箱は元の5枚で決める**）");
        Console.WriteLine();
        var wdBox = wdAllRows.Select(b => Math.Min(3, WdWoundCount(b.F.Occupied().Select(o => o.Def)))).ToArray();
        double[] WdIdealRates(string verKey, int wave)
        {
            var rate = new double[wdAllRows.Length];
            for (int i = 0; i < wdAllRows.Length; i++)
                rate[i] = wave < 0 ? Enumerable.Range(1, wdW - 1).Average(w => wdBase[i][w]) : wdBase[i][wave];
            for (int r = 0; r < wdRows.Length; r++)
            {
                int vi = WdVerOf(r, verKey);
                if (vi < 0) continue;
                int bi = Array.FindIndex(wdAllRows, b => b.Name == wdRows[r].Name);
                rate[bi] = wave < 0 ? WdRate25(r, vi) : WdRate(r, vi, wave);
            }
            return rate;
        }
        var wdVerKeys = new List<(string Key, string Label)>
        {
            ("full", "**現行**"), ("pAll", "傷の駒を全部素体"), ("noCons", "消費型だけ素体"),
        };
        for (int k = 0; k < 5; k++) wdVerKeys.Add(("p" + wdIds[k], "素体:" + wdFive[k].Def.Name));
        wdVerKeys.Add(("nofix", "執着を外す"));
        wdVerKeys.Add(("dblKiri", "裂き二重（供給+1）"));
        wdVerKeys.Add(("dblNomi", "刻み二重（供給+1）"));
        wdVerKeys.Add(("kiri1", "素体:キリ(攻1)"));

        Console.WriteLine("| 版 | 0枚 | 1枚 | 2枚 | 3+ | 第2波 | 第3波 | 第4波 | 第5波 | **集計（第2〜5波）** | Δ（現行比） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        double wdSlopeFull = double.NaN;
        foreach ((string key, string label) in wdVerKeys)
        {
            var rate = WdIdealRates(key, -1);
            double[] bm = WdBoxMeans(rate, wdBox);
            double sl = WdSlope(rate, wdBox);
            if (key == "full") wdSlopeFull = sl;
            Console.WriteLine($"| {label} | {bm[0]:F1}% | {bm[1]:F1}% | {bm[2]:F1}% | {(double.IsNaN(bm[3]) ? "—" : $"{bm[3]:F1}%")} "
                              + string.Concat(Enumerable.Range(1, wdW - 1).Select(w => $"| {WdP1(WdSlope(WdIdealRates(key, w), wdBox))} "))
                              + $"| **{WdP1(sl)}** | {(key == "full" ? "—" : WdP2(sl - wdSlopeFull))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**箱の大きさ: 0枚 {wdBox.Count(x => x == 0)} 行 / 1枚 {wdBox.Count(x => x == 1)} 行 / "
                          + $"2枚 {wdBox.Count(x => x == 2)} 行 / 3+ {wdBox.Count(x => x == 3)} 行。**");
        Console.WriteLine("**1枚の箱は1行しか無い**（第72期 §8-5 の「傷の答えは 1 行が決めている」）——"
                          + "**理想台の傾きは (2枚 − 1枚) の項もその1行に寄りかかっている。**");
        Console.WriteLine();
        Console.WriteLine($"所要 {wdSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }
    // =====================================================================================
    // draft: ドラフト台（規則 Pw と S'w・**弱い波**）
    // =====================================================================================
    if (wdArg == "draft")
    {
        string wdDName = wdBand == 0 ? "A" : "B";
        // 0 full / 1 pAll / 2 noCons / 3..7 p<駒> / 8 素体キリ(攻1) / 9 執着なし / 10 裂き二重 / 11 刻み二重
        const int VD = 12;
        string[] wdDVer = new string[VD];
        wdDVer[0] = "**現行**"; wdDVer[1] = "傷の駒を全部素体"; wdDVer[2] = "消費型だけ素体";
        for (int k = 0; k < 5; k++) wdDVer[3 + k] = "素体:" + wdFive[k].Def.Name;
        wdDVer[8] = "**素体:キリ(攻1)**（H3）"; wdDVer[9] = "**執着を外す**（H3）";
        wdDVer[10] = "**裂き二重**（H1・清潔）"; wdDVer[11] = "**刻み二重**（H1・汚れあり）";

        int[] wdRules = { 1, 3 };
        string[] wdRuleName = { "Pw（素朴 × 弱い波）", "S'w（傾き志向 × 弱い波）" };
        var wdDWin = new int[2][][];
        var wdDBox = new int[2][];
        var wdDHas = new bool[2][][];
        for (int q = 0; q < 2; q++) { wdDWin[q] = new int[WdN][]; wdDBox[q] = new int[WdN]; wdDHas[q] = new bool[WdN][]; }

        for (int q = 0; q < 2; q++)
        {
            int done = 0;
            Console.Error.Write($"{wdDName}帯 {wdRuleName[q]}: ");
            Parallel.For(0, WdN, i =>
            {
                UnitDef[] team = WdTeam(wdRules[q], i);
                int[] seats = WdSeats(team);
                var ids = team.Select(x => x.Id).ToHashSet();
                var has = new bool[VD];
                has[0] = true;
                for (int k = 0; k < 5; k++) has[3 + k] = ids.Contains(wdIds[k]);
                has[1] = Enumerable.Range(0, 5).Any(k => has[3 + k]);
                has[2] = wdConsumers.Any(ids.Contains);
                has[8] = has[10] = ids.Contains(UnitCatalog.Kiri.Id);
                has[9] = has[11] = ids.Contains(UnitCatalog.Nomi.Id);
                var win = new int[VD * wdW];
                for (int v = 0; v < VD; v++)
                {
                    if (!has[v]) continue;
                    Dictionary<string, UnitDef>? map = null;
                    if (v == 1)
                    {
                        map = new Dictionary<string, UnitDef>();
                        foreach (var x in wdFive) if (ids.Contains(x.Def.Id)) map[x.Def.Id] = WdPlain(x.Def);
                    }
                    else if (v == 2)
                    {
                        map = new Dictionary<string, UnitDef>();
                        foreach (string cid in wdConsumers)
                            if (ids.Contains(cid)) map[cid] = WdPlain(wdFive.First(x => x.Def.Id == cid).Def);
                    }
                    else if (v >= 3 && v <= 7)
                        map = new Dictionary<string, UnitDef> { [wdIds[v - 3]] = WdPlain(wdFive[v - 3].Def) };
                    else if (v == 8)
                        map = new Dictionary<string, UnitDef> { [UnitCatalog.Kiri.Id] = WdPlainAtk(UnitCatalog.Kiri, 1) };
                    else if (v == 9)
                        map = new Dictionary<string, UnitDef> { [UnitCatalog.Nomi.Id] = wdNomiNoFix };
                    else if (v == 10)
                        map = new Dictionary<string, UnitDef> { [UnitCatalog.Kiri.Id] = wdKiriDbl };
                    else if (v == 11)
                        map = new Dictionary<string, UnitDef> { [UnitCatalog.Nomi.Id] = wdNomiDbl };

                    Formation f = WdDraftForm(team, seats, map);
                    for (int w = 0; w < wdW; w++)
                        for (int seed = wdBand; seed < wdBand + WdM; seed++)
                            if (BattleEngine.Run(f, wdWeak[w].Enemy, seed, verbose: false).PlayerWon)
                                win[v * wdW + w]++;
                }
                wdDWin[q][i] = win; wdDHas[q][i] = has;
                wdDBox[q][i] = Math.Min(3, WdWoundCount(team));
                int c = Interlocked.Increment(ref done);
                if (c % 1000 == 0) Console.Error.Write(".");
            });
            Console.Error.WriteLine();
        }

        double WdDRate(int q, int i, int v, int w)
            => (wdDHas[q][i][v] ? wdDWin[q][i][v * wdW + w] : wdDWin[q][i][w]) * 100.0 / WdM;
        double[] WdDRates(int q, int v, int wave)
            => Enumerable.Range(0, WdN).Select(i => wave < 0
                ? Enumerable.Range(1, wdW - 1).Average(w => WdDRate(q, i, v, w))
                : WdDRate(q, i, v, wave)).ToArray();

        Console.WriteLine($"# 第73期 —— 傷の解剖・ドラフト台（{wdDName} 帯 seed {wdBand}..{wdBand + WdM - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 wound draft{(wdBand == 0 ? "" : " alt")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"標本 **{WdN:N0}** × 規則 2（P / S'）× 版 {VD} × {wdW} 波 × seed **{WdM}** 本。");
        Console.WriteLine($"波は**弱い波のみ**（敵の `MaxHp` を {WdWeakPct}%・第70〜72期の写し）。**判定は第2〜5波。**");
        Console.WriteLine("**箱（傷の枚数）は必ず元の5枚で決める**——素体に落とすとキーが消えるので、");
        Console.WriteLine("版で箱が動くと「同じ標本の版どうし」を比べられなくなる。");
        Console.WriteLine();

        for (int q = 0; q < 2; q++)
        {
            Console.WriteLine($"## 規則 {wdRuleName[q]}");
            Console.WriteLine();
            Console.WriteLine($"箱: **0枚 {wdDBox[q].Count(x => x == 0):N0} / 1枚 {wdDBox[q].Count(x => x == 1):N0} / "
                              + $"2枚 {wdDBox[q].Count(x => x == 2):N0} / 3+ {wdDBox[q].Count(x => x == 3):N0}**");
            Console.WriteLine();
            Console.Write("在席: ");
            Console.WriteLine(string.Join(" / ", Enumerable.Range(0, 5).Select(k =>
                $"{wdFive[k].Def.Name} {Enumerable.Range(0, WdN).Count(i => wdDHas[q][i][3 + k]):N0}")));
            Console.WriteLine();
            Console.WriteLine("| 版 | 0枚 | 1枚 | 2枚 | 3+ | 第2波 | 第3波 | 第4波 | 第5波 | **集計** | Δ（現行比） | 動いた標本 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            double sl0 = double.NaN;
            for (int v = 0; v < VD; v++)
            {
                var rate = WdDRates(q, v, -1);
                double[] bm = WdBoxMeans(rate, wdDBox[q]);
                double sl = WdSlope(rate, wdDBox[q]);
                if (v == 0) sl0 = sl;
                int moved = Enumerable.Range(0, WdN).Count(i => wdDHas[q][i][v]);
                Console.WriteLine($"| {wdDVer[v]} | {bm[0]:F2}% | {bm[1]:F2}% | {bm[2]:F2}% | {(double.IsNaN(bm[3]) ? "—" : $"{bm[3]:F2}%")} "
                                  + string.Concat(Enumerable.Range(1, wdW - 1).Select(w => $"| {WdP1(WdSlope(WdDRates(q, v, w), wdDBox[q]))} "))
                                  + $"| **{WdP1(sl)}** | {(v == 0 ? "—" : WdP2(sl - sl0))} | {(v == 0 ? WdN : moved):N0} |");
            }
            Console.WriteLine();
            Console.WriteLine("| 分離できた代金 | 在席 | 器具 | **帰属（Δ勝率・第2〜5波）** | **傾きの変化** |");
            Console.WriteLine("|---|--:|---|--:|--:|");
            {
                var ik = Enumerable.Range(0, WdN).Where(i => wdDHas[q][i][8]).ToArray();
                double sl3 = WdSlope(WdDRates(q, 3, -1), wdDBox[q]), sl8 = WdSlope(WdDRates(q, 8, -1), wdDBox[q]);
                if (ik.Length > 0)
                    Console.WriteLine($"| キリ「与ダメ常に1」 | {ik.Length:N0} | 素体(攻12) − 素体(攻1) "
                        + $"| **{WdP2(ik.Average(i => Enumerable.Range(1, wdW - 1).Average(w => WdDRate(q, i, 3, w)))
                                    - ik.Average(i => Enumerable.Range(1, wdW - 1).Average(w => WdDRate(q, i, 8, w))))}** "
                        + $"| {WdP2(sl3 - sl8)} |");
                var inm = Enumerable.Range(0, WdN).Where(i => wdDHas[q][i][9]).ToArray();
                double sl0b = WdSlope(WdDRates(q, 0, -1), wdDBox[q]), sl9 = WdSlope(WdDRates(q, 9, -1), wdDBox[q]);
                if (inm.Length > 0)
                    Console.WriteLine($"| ノミ「執着」 | {inm.Length:N0} | 執着なし − 現行 "
                        + $"| **{WdP2(inm.Average(i => Enumerable.Range(1, wdW - 1).Average(w => WdDRate(q, i, 9, w)))
                                    - inm.Average(i => Enumerable.Range(1, wdW - 1).Average(w => WdDRate(q, i, 0, w))))}** "
                        + $"| {WdP2(sl9 - sl0b)} |");
            }
            Console.WriteLine();
            Console.WriteLine("| 駒 | 在席 | 在席時の勝率（現行） | 同（その1枚を素体に） | **寄与** |");
            Console.WriteLine("|---|--:|--:|--:|--:|");
            for (int k = 0; k < 5; k++)
            {
                var idx = Enumerable.Range(0, WdN).Where(i => wdDHas[q][i][3 + k]).ToArray();
                if (idx.Length == 0) { Console.WriteLine($"| {wdFive[k].Def.Name} | 0 | — | — | — |"); continue; }
                double a = idx.Average(i => Enumerable.Range(1, wdW - 1).Average(w => WdDRate(q, i, 0, w)));
                double b = idx.Average(i => Enumerable.Range(1, wdW - 1).Average(w => WdDRate(q, i, 3 + k, w)));
                Console.WriteLine($"| {wdFive[k].Def.Name} | {idx.Length:N0} | {a:F2}% | {b:F2}% | **{WdP2(a - b)}** |");
            }
            Console.WriteLine();
        }

        // ---- ドラフト台の在庫の収支（小標本・verbose）----
        Console.WriteLine("## 表B（ドラフト側）—— 在庫の収支（**規則 Pw・傷を含む先頭 300 標本 × 弱い波 × seed 2 本**）");
        Console.WriteLine();
        var wdSample = new List<int>();
        for (int i = 0; i < WdN && wdSample.Count < 300; i++) if (wdDBox[0][i] >= 1) wdSample.Add(i);
        var wdDAcc = new double[wdSample.Count][];
        Parallel.For(0, wdSample.Count, si =>
        {
            int i = wdSample[si];
            UnitDef[] team = WdTeam(1, i);
            int[] seats = WdSeats(team);
            Formation f = WdDraftForm(team, seats, null);
            var ob = new UnitDef?[5];
            foreach ((int _, UnitDef d) in f.Occupied())
                for (int k = 0; k < 5; k++) if (d.Id == wdIds[k]) ob[k] = d;
            var a = new double[WdC + 1];
            for (int w = 0; w < wdW; w++)
                for (int seed = wdBand; seed < wdBand + 2; seed++)
                    WdCount(BattleEngine.Run(f, wdWeak[w].Enemy, seed, verbose: true), f.Count, ob, a);
            wdDAcc[si] = a;
        });
        {
            var wdS = new double[WdC + 1];
            for (int si = 0; si < wdSample.Count; si++) for (int c = 0; c <= WdC; c++) wdS[c] += wdDAcc[si][c];
            double n = wdS[13];
            Console.WriteLine("| 量 | 値 |");
            Console.WriteLine("|---|--:|");
            Console.WriteLine($"| 標本 / 戦 | {wdSample.Count} / {n:N0} |");
            Console.WriteLine($"| 決着T | {wdS[15] / n:F2} |");
            Console.WriteLine($"| **書込/戦**（裂き {wdS[0] / n:F2} + 刻み {wdS[1] / n:F2}） | **{(wdS[0] + wdS[1]) / n:F2}** |");
            Console.WriteLine($"| **在庫/T** | **{wdS[14] / Math.Max(1, wdS[15]):F2}** |");
            Console.WriteLine($"| 在庫max/戦 | {wdS[16] / n:F2} |");
            Console.WriteLine($"| 消費/戦（断ち {wdS[7] / n:F2} + 縫い {wdS[8] / n:F2}） | {(wdS[7] + wdS[8]) / n:F2} |");
            Console.WriteLine($"| **味方の席に載った傷** | **{wdS[17]:F0}** |");
            Console.WriteLine();
            Console.WriteLine("| 読み手 | 振/戦 | 発火/戦 | **空振り率** | 読んだ傷/発火 |");
            Console.WriteLine("|---|--:|--:|--:|--:|");
            var rd = new (int U, int F, int S)[] { (2, 2, 3), (1, 4, 5), (3, 6, 7), (4, 8, 9) };
            foreach ((int u, int fi, int su) in rd)
            {
                double sw = wdS[18 + u] / n, fire = wdS[fi] / n;
                Console.WriteLine($"| {wdFive[u].Def.Name} | {sw:F3} | {fire:F3} "
                                  + $"| **{(sw > 0 ? $"{100 * (1 - fire / sw):F1}%" : "—")}** | {(wdS[fi] > 0 ? $"{wdS[su] / wdS[fi]:F2}" : "—")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {wdSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check: 陰性対照（Q6）
    // =====================================================================================
    if (wdArg == "check")
    {
        Console.WriteLine("# 第73期 —— 陰性対照（Q6）");
        Console.WriteLine();
        Console.WriteLine("## (1) 変種は診断のローカルにしか無い");
        Console.WriteLine();
        Console.WriteLine("| 確認 | 結果 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine($"| `UnitCatalog.All` の体数 | {wdRN} |");
        Console.WriteLine($"| うち傷キーの保持者 | {wdRoster.Count(u => wdKeyOf[u.Id].Contains(WdKW))} |");
        Console.WriteLine($"| 変種の `Id` が `All` に無いこと | "
                          + $"{(new[] { WdPlain(UnitCatalog.Kiri), WdPlainAtk(UnitCatalog.Kiri, 1), wdKiriDbl, wdNomiDbl, wdNomiNoFix }.Count(d => wdRoster.Any(u => u.Id == d.Id)))} 件の衝突 |");
        Console.WriteLine($"| 素体が特性を持たないこと | {wdFive.Sum(x => WdPlain(x.Def).Traits.Count)} 件 |");
        Console.WriteLine($"| 素体の数値が同じであること | "
                          + $"{wdFive.Count(x => WdPlain(x.Def).MaxHp != x.Def.MaxHp || WdPlain(x.Def).Attack != x.Def.Attack || WdPlain(x.Def).Speed != x.Def.Speed)} 件の食い違い |");
        Console.WriteLine();

        Console.WriteLine("## (2) 裂き二重は**供給だけ**を動かす（1戦の監査）");
        Console.WriteLine();
        Console.WriteLine("`TraitCatalog.Resolve` が重複を潰さないので `OnAfterAttack` が2回走る。");
        Console.WriteLine("**`RendTrait.ModifyAttack` は引数を読まずに 1 を返す**ので、二重にしても打点は 1 のまま。");
        Console.WriteLine();
        var wdAudRow = wdRows.First(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Kiri.Id));
        Console.WriteLine("| 版 | キリの振/戦 | 書込/戦 | **書込/振** | キリの与ダメ/戦 | **与ダメ/振** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach ((string lab, UnitDef d) in new[] { ("現行", UnitCatalog.Kiri), ("裂き二重", wdKiriDbl) })
        {
            Formation f = WdSub1(wdAudRow.F, UnitCatalog.Kiri.Id, d);
            double sw = 0, wr = 0, dmg = 0, n = 0;
            for (int w = 0; w < wdW; w++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult res = BattleEngine.Run(f, wdStages[w].Enemy, seed, verbose: true);
                    n++;
                    if (res.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) { sw += t.Attacks; dmg += t.DamageToEnemy; }
                    foreach (LogLine l in res.Log)
                        if (l.Text.Contains(d.Name + " の刃が") && l.Text.Contains("に傷を残した")) wr++;
                }
            Console.WriteLine($"| {lab} | {sw / n:F2} | {wr / n:F2} | **{wr / Math.Max(1, sw):F3}** | {dmg / n:F2} | **{dmg / Math.Max(1, sw):F3}** |");
        }
        Console.WriteLine();
        Console.WriteLine("**書込/振 が 1.000 → 2.000 で、与ダメ/振 が 1.000 のまま**なら、このノブは供給だけを動かしている。");
        Console.WriteLine();

        Console.WriteLine("## (3) 刻み二重は**汚れている**（なぞりも二重に走る）");
        Console.WriteLine();
        var wdAudRow2 = wdRows.First(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Nomi.Id));
        Console.WriteLine("| 版 | ノミの振/戦 | 書込/戦 | なぞり発火/戦 | なぞりの上乗せ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach ((string lab, UnitDef d) in new[] { ("現行", UnitCatalog.Nomi), ("刻み二重", wdNomiDbl) })
        {
            Formation f = WdSub1(wdAudRow2.F, UnitCatalog.Nomi.Id, d);
            double sw = 0, wr = 0, fire = 0, gain = 0, n = 0;
            for (int w = 0; w < wdW; w++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult res = BattleEngine.Run(f, wdStages[w].Enemy, seed, verbose: true);
                    n++;
                    if (res.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) sw += t.Attacks;
                    foreach (LogLine l in res.Log)
                    {
                        if (l.Text.Contains(d.Name + " の鑿が")) wr++;
                        else if (l.Text.Contains(d.Name + " が ") && l.Text.Contains("の古い傷をなぞる"))
                        { fire++; gain += CarveTrait.PerWound * WdNum(l.Text, "（傷 "); }
                    }
                }
            Console.WriteLine($"| {lab} | {sw / n:F2} | {wr / n:F2} | {fire / n:F2} | {gain / n:F1} |");
        }
        Console.WriteLine();
        Console.WriteLine("**刻みの二重は出力も増やす**ので、H1（供給を増やせば傾きが動くか）の清潔な対照は**裂き二重だけ**。");
        Console.WriteLine();

        Console.WriteLine("## (4) 盤面");
        Console.WriteLine();
        Console.WriteLine("`wound` は `BattleEngine.Run` を読むだけで、`Traits.cs` / `BattleEngine.cs` / `UnitCatalog` /");
        Console.WriteLine("`EnemyCatalog.Stages` / `CompareBuilds()` を1行も動かしていない。");
        Console.WriteLine("`compare` / `dump` の再生成と `docs/` の diff は別に取る。");
        Console.WriteLine();
        Console.WriteLine($"所要 {wdSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("wound: 引数は phase0 / （無し）/ alt / draft [alt] / check。");
    return;
}
}
