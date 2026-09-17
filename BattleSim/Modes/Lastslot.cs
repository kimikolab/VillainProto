using BattleCore;
using static Common;

// =====================================================================================
// lastslot モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "lastslot")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 lastslot
// =====================================================================================

static class LastslotDiag
{
// 最後の1枠（52枚目）——空白の地図から規則で駒を1つ選び、両方の台で測る（第79期）。
// **仕様は8期分の実測から出ている**（design/PHASE79_LASTSLOT_SPEC.md §0-1）:
// 入口 0 ／ 特性2つ ／ 2枚目は代金または自給の口 ／ 相方を要求しない ／ 数値で強くしない。
// 候補駒は `UnitCatalog.Ono`（**`All` には載せていない**。診断が 52 体のロスターをローカルに組む）。
//
//     dotnet run --project BattleSim -c Release 0 lastslot phase0  # 空白の地図・候補の選定・数値の決め方（戦闘は在席時勝率と理想台の帰属のぶんだけ）
//     dotnet run --project BattleSim -c Release 0 lastslot         # 主表（A 帯: ドラフト seed 0..7 / 理想 0..199）
//     dotnet run --project BattleSim -c Release 0 lastslot alt     # B 帯（ドラフト 200..207 / 理想 200..399）
//     dotnet run --project BattleSim -c Release 0 lastslot seats   # 単騎・軸あり の配置（reseat 120通り → confirm seed 200..599・発火の列つき）
//     dotnet run --project BattleSim -c Release 0 lastslot check   # 受け入れ基準: `compare` 305 セルの突き合わせ（`All` は動かしていない）
public static void Run(string[] args, int stageIndex)
{
    string lsArg = args.Length > 2 ? args[2] : "";
    var lsSw = System.Diagnostics.Stopwatch.StartNew();

    IReadOnlyList<EnemyCatalog.Stage> lsStages = EnemyCatalog.Stages;
    int lsW = lsStages.Count;
    var lsRoster51 = UnitCatalog.All.ToArray();
    UnitDef lsNew = UnitCatalog.Ono;
    var lsRoster52 = lsRoster51.Concat(new[] { lsNew }).ToArray();

    // ---- ドラフト台は第76〜78期と同一（指示書 §2-3。定数も抽選も規則配置も1文字も変えていない）----
    const int LsOfferSeed = 2_000_000;
    const int LsN = 11000, LsM = 8;
    const int LsStrong = 7, LsWeakPct = 60;
    const int LsSeedsIdeal = 200;
    int lsBand = lsArg == "alt" ? 200 : 0;
    string lsBandName = lsBand == 0 ? "A" : "B";

    var lsWeakCache = new Dictionary<string, UnitDef>();
    UnitDef LsWeakOf(UnitDef d)
    {
        if (lsWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * LsWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        lsWeakCache[d.Id] = w;
        return w;
    }
    var lsWeak = lsStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = LsWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    // 素体（同数値・特性なし）。帰属の標準器具（第69期）。Id を変えるので帳簿の鍵も変わる。
    UnitDef LsPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    // 対照: 支援拒否だけを外した版（罠の代金を数えるため。Id は同じ＝帳簿の鍵が同じ）。
    UnitDef lsNoStoic = new()
    {
        Id = lsNew.Id, Name = lsNew.Name + "（支援拒否なし）",
        MaxHp = lsNew.MaxHp, Attack = lsNew.Attack, Speed = lsNew.Speed,
        Traits = new[] { TraitId.Executioner }, Pattern = lsNew.Pattern
    };

    // 規則 P（第70〜78期の写し）。ロスターだけを引数にした。
    UnitDef[] LsTeam(UnitDef[] roster, int i)
    {
        int rn = roster.Length;
        var rng = new Random(LsOfferSeed + i);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        int remain = rn, strong = 0;
        var picked = new UnitDef[5];
        for (int r = 0; r < 5; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = roster[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= LsStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(roster[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    // 規則配置 H（第69期以来の写し）: HP 上位2枚を前・攻 上位2枚を後・残りが中央。
    int[] LsSeats(UnitDef[] u)
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
    Formation LsForm(UnitDef[] u, int[] seats, Func<UnitDef, UnitDef>? sub)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = sub is null ? u[k] : sub(u[k]);
        return f;
    }
    Formation LsFormH(UnitDef[] u) => LsForm(u, LsSeats(u), null);
    Formation LsSub(Formation f, UnitDef from, UnitDef to)
    {
        var g = new Formation();
        foreach ((int sl, UnitDef d) in f.Occupied()) g[sl] = ReferenceEquals(d, from) ? to : d;
        return g;
    }

    string LsP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double LsSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    int LsDeg(int slot)
    {
        int n = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
            if (FormationRules.AreAdjacent(slot, i)) n++;
        return n;
    }
    int LsSeatOf(Formation f, UnitDef d)
    {
        foreach ((int slot, UnitDef x) in f.Occupied()) if (x.Id == d.Id) return slot;
        return -1;
    }
    string LsPlace(Formation f) => string.Join(" / ", f.Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"));

    var lsAllRows = CompareBuilds();

    // ---- docs/balance.md の現在値（戦闘0回で読む）------------------------------------------------
    var lsBalance = new Dictionary<string, double[]>();
    if (File.Exists("docs/balance.md"))
        foreach (string line in File.ReadAllLines("docs/balance.md"))
        {
            if (!line.StartsWith("| ")) continue;
            var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
            if (cells.Length != lsW + 1 || !cells[1].EndsWith("%")) continue;
            var v = new double[lsW];
            bool ok = true;
            for (int w = 0; w < lsW; w++)
                if (!double.TryParse(cells[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
            if (ok) lsBalance[cells[0]] = v;
        }
    double LsPrimaryW5()
    {
        var xs = Baseline.PrimaryRows.Where(lsBalance.ContainsKey).Select(n => lsBalance[n][lsW - 1]).ToList();
        return xs.Count == 0 ? double.NaN : xs.Average();
    }

    // ---- 試験行（理想台・§2-2）。**既存行は書き換えない。3 行とも新設。** ----------------------
    // **最初の規則（「キー 0 で死・撃破のフックを持たない4枚」＝ドルガ・ナラ・ノノ・ササ）は床に落ちた**
    // ——`lastslot seats` で 120 通りすべてが 100/0/0/0/0（CLAUDE.md「台が死んでいる」の合図）。
    // 床では帰属が定義上 0 で「測っていない」になる（第61期）ので、第61・63期の器具の規則を
    // 試験行の作り方に組み込み、**帰属を測る前に**固定し直した:
    //
    //   測れる行 ＝ 素体版（オノを同数値・特性なしに落とした版）の **5波平均が 40% 以上 95% 以下**
    //             （seed 0..49・規則配置 H。第63期の「床と同じだけ天井も外す」）
    //
    // 単騎:  新駒 ＋ **新駒の出力（撃破・自分の攻撃力）を読まない**4枚。プールは「キー 0 で、
    //        敵の撃破を読まない駒」＝ ドルガ・リィカ・ムグ・ナラ・ノノ・ササ・ヴェル の7体（追い打ちのハギは
    //        味方の撃破で割り込むので外す）。C(7,4)=35 組を全部測り、**測れる組のうち素体版の第2〜5波平均が
    //        50% に最も近い組**（＝情報がいちばん多い台）を採る。オノにキーは無いので、第68期の意味の
    //        「相方」はどの4枚でも存在しない——プールの制限は「4枚どうしの噛み合いで台を作らない」ため。
    // 軸あり: 単騎の4枚のうち**攻撃力が最も低い1枚**を追い打ちのハギに差し替える（1枚だけ動かす・自由度ゼロ）。
    // 罠:    オノを**中央**（次数最大）に固定し、味方へ強化か回復を書く駒（`Supplies` に強化@味方を持つか、
    //        `ctx.Heal` の呼び出し元の保持者）11 体から C(11,4)=330 組を全部測り、**測れる組のうち
    //        対照（支援拒否なし）でオノが受け取る強化＋回復が最大の組**＝代金が最大の編成を採る。
    //        測れる組が無ければ受け取り最大の組を採り、床であることを報告する。
    // 配置:  単騎・軸あり は規則配置 H を出発点に `lastslot seats` で reseat → confirm（閾値 5.0pt）。
    var lsSoloPool = new[] { UnitCatalog.Dolga, UnitCatalog.Rica, UnitCatalog.Mug, UnitCatalog.Nara, UnitCatalog.Nono, UnitCatalog.Sasa, UnitCatalog.Vel };
    var lsTrapPool = new[] { UnitCatalog.Golm, UnitCatalog.Gan, UnitCatalog.Kugu, UnitCatalog.Shio, UnitCatalog.Kari, UnitCatalog.Hiyo,
                             UnitCatalog.Nono, UnitCatalog.Rica, UnitCatalog.Hari, UnitCatalog.Beni, UnitCatalog.Nara };
    const int LsPickSeeds = 50;
    IEnumerable<UnitDef[]> LsCombos4(UnitDef[] pool)
    {
        for (int a = 0; a < pool.Length; a++)
            for (int b = a + 1; b < pool.Length; b++)
                for (int c = b + 1; c < pool.Length; c++)
                    for (int d = c + 1; d < pool.Length; d++)
                        yield return new[] { pool[a], pool[b], pool[c], pool[d] };
    }
    (double Avg5, double Avg25) LsQuick(Formation f)
    {
        double sum5 = 0, sum25 = 0;
        for (int w = 0; w < lsW; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < LsPickSeeds; seed++)
                if (BattleEngine.Run(f, lsStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
            double r = wins * 100.0 / LsPickSeeds;
            sum5 += r; if (w >= 1) sum25 += r;
        }
        return (sum5 / lsW, sum25 / (lsW - 1));
    }
    double LsReceived(Formation f)
    {
        double got = 0; int n = 0;
        for (int w = 0; w < lsW; w++)
            for (int seed = 0; seed < LsPickSeeds; seed++)
            {
                var r = BattleEngine.Run(f, lsStages[w].Enemy, seed, verbose: false);
                if (r.TallyByUnit.TryGetValue(lsNew.Id, out UnitTally? t)) got += t.Whetted + t.Healed;
                n++;
            }
        return got / n;
    }
    // 単騎の候補（35 組）
    var lsSoloCands = LsCombos4(lsSoloPool).Select(four =>
    {
        var team = new[] { lsNew }.Concat(four).ToArray();
        Formation f = LsFormH(team);
        var q = LsQuick(LsSub(f, lsNew, LsPlain(lsNew)));
        return (Four: four, F: f, Plain5: q.Avg5, Plain25: q.Avg25, Ok: q.Avg5 >= 40.0 && q.Avg5 <= 95.0);
    }).ToArray();
    var lsSoloPick = lsSoloCands.Where(c => c.Ok).OrderBy(c => Math.Abs(c.Plain25 - 50.0))
                                .ThenBy(c => string.Join(",", c.Four.Select(d => d.Id)), StringComparer.Ordinal).FirstOrDefault();
    bool lsSoloOk = lsSoloPick.F is not null;
    if (!lsSoloOk) lsSoloPick = lsSoloCands.OrderByDescending(c => c.Plain5).First();
    var lsSoloTeam = new[] { lsNew }.Concat(lsSoloPick.Four).ToArray();
    // 軸あり: 単騎の4枚のうち攻撃力最小をハギへ
    UnitDef lsSwapped = lsSoloPick.Four.OrderBy(d => d.Attack).ThenBy(d => d.Id, StringComparer.Ordinal).First();
    var lsAxisTeam = lsSoloTeam.Select(d => ReferenceEquals(d, lsSwapped) ? UnitCatalog.Hagi : d).ToArray();
    // 罠の候補（330 組・オノ中央固定）
    Formation LsTrapForm(UnitDef[] four)
    {
        var front = four.OrderByDescending(d => d.MaxHp).ThenBy(d => d.Id, StringComparer.Ordinal).Take(2).ToArray();
        var rest = four.Where(d => !front.Contains(d)).OrderByDescending(d => d.Attack).ThenBy(d => d.Id, StringComparer.Ordinal).ToArray();
        return Formation.Build(front1: front[0], front3: front[1], center: lsNew, back1: rest[0], back3: rest[1]);
    }
    var lsTrapCands = LsCombos4(lsTrapPool).Select(four =>
    {
        Formation f = LsTrapForm(four);
        var q = LsQuick(LsSub(f, lsNew, LsPlain(lsNew)));
        double got = LsReceived(LsSub(f, lsNew, lsNoStoic));
        return (Four: four, F: f, Plain5: q.Avg5, Plain25: q.Avg25, Ok: q.Avg5 >= 40.0 && q.Avg5 <= 95.0, Got: got);
    }).ToArray();
    var lsTrapPick = lsTrapCands.Where(c => c.Ok).OrderByDescending(c => c.Got)
                                .ThenBy(c => string.Join(",", c.Four.Select(d => d.Id)), StringComparer.Ordinal).FirstOrDefault();
    bool lsTrapOk = lsTrapPick.F is not null;
    if (!lsTrapOk) lsTrapPick = lsTrapCands.OrderByDescending(c => c.Got).First();

    Formation lsSolo = LsFormH(lsSoloTeam);
    Formation lsAxis = LsFormH(lsAxisTeam);
    // 採用席（`lastslot seats`）。規則配置 H（オノ後1・リィカ中央）は粗探索 31位 / 37位で上位5に入らず、
    // confirm（seed 200..599）で 単騎 +36.8pt / 軸あり +31.7pt（閾値 5.0pt）。上位5通りは**全部オノが前角（次数2）・
    // ドルガ中央**で、次数の読みも一致する。撃破/戦は 0.42 → 0.21 / 0.35 → 0.11 と減るが 0 ではない（第50期の歯止めは通る）。
    // **選定が変わって5枚の顔ぶれが違えば H に戻る**（下の一致検査）。
    if (lsSoloTeam.Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "dolga", "nono", "ono", "rica", "vel" }))
        lsSolo = Formation.Build(front1: lsNew, front3: UnitCatalog.Nono, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Rica);
    if (lsAxisTeam.Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "dolga", "hagi", "ono", "rica", "vel" }))
        lsAxis = Formation.Build(front1: UnitCatalog.Hagi, front3: lsNew, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Rica);
    Formation lsTrap = lsTrapPick.F;
    var lsRows = new (string Name, Formation F)[]
    {
        ("単騎 (オノ×相方なし4)", lsSolo),
        ("軸あり (オノ×ハギ)", lsAxis),
        ("罠 (オノ中央×支援4)", lsTrap),
    };
    void LsPrintPick()
    {
        Console.WriteLine("### 試験行の選定（seed 0..49・規則配置 H・素体版の床と天井で切る）");
        Console.WriteLine();
        Console.WriteLine($"単騎: 35 組のうち測れる組（素体版の5波平均 40〜95%）は **{lsSoloCands.Count(c => c.Ok)} 組**。"
                          + (lsSoloOk ? "" : "**測れる組が無い（全部床か天井）。5波平均が最大の組を仮に採り、床であることを報告する。**"));
        Console.WriteLine();
        Console.WriteLine("| 順位 | 4枚 | 素体版 5波平均 | 素体版 第2〜5波 | 測れる |");
        Console.WriteLine("|--:|---|--:|--:|:-:|");
        int rank = 0;
        foreach (var c in lsSoloCands.OrderBy(c => c.Ok ? 0 : 1).ThenBy(c => Math.Abs(c.Plain25 - 50.0)).Take(8))
            Console.WriteLine($"| {++rank} | {string.Join("・", c.Four.Select(d => d.Name))} | {c.Plain5:F1}% | {c.Plain25:F1}% | {(c.Ok ? "○" : "×")} |");
        Console.WriteLine($"| … | 床（5波平均 < 40%）の組 | {lsSoloCands.Count(c => c.Plain5 < 40.0)} 組 | | |");
        Console.WriteLine();
        Console.WriteLine($"→ **単騎 ＝ オノ ＋ {string.Join("・", lsSoloPick.Four.Select(d => d.Name))}**（素体版 {lsSoloPick.Plain5:F1}% / 第2〜5波 {lsSoloPick.Plain25:F1}%）。"
                          + $" 軸あり ＝ そのうち攻撃力最小の **{lsSwapped.Name}（攻{lsSwapped.Attack}）** を追い打ちのハギへ。");
        Console.WriteLine();
        Console.WriteLine($"罠: 330 組のうち測れる組は **{lsTrapCands.Count(c => c.Ok)} 組**。"
                          + (lsTrapOk ? "" : "**測れる組が無い。受け取り最大の組を仮に採り、床であることを報告する。**"));
        Console.WriteLine();
        Console.WriteLine("| 順位 | 4枚（オノは中央） | 対照で受け取る強化＋回復/戦 | 素体版 5波平均 | 測れる |");
        Console.WriteLine("|--:|---|--:|--:|:-:|");
        rank = 0;
        foreach (var c in lsTrapCands.OrderBy(c => c.Ok ? 0 : 1).ThenByDescending(c => c.Got).Take(8))
            Console.WriteLine($"| {++rank} | {string.Join("・", c.Four.Select(d => d.Name))} | {c.Got:F2} | {c.Plain5:F1}% | {(c.Ok ? "○" : "×")} |");
        Console.WriteLine();
        Console.WriteLine($"→ **罠 ＝ {LsPlace(lsTrap)}**（対照で受け取る量 {lsTrapPick.Got:F2}/戦・素体版 {lsTrapPick.Plain5:F1}%）。");
        Console.WriteLine();
        foreach (var r in lsRows) Console.WriteLine($"- **{r.Name}**: {LsPlace(r.F)}");
        Console.WriteLine();
    }

    // 理想台の1行を測る。帳簿はオノのぶんだけ。
    (double[] Cells, double Kills, double Whetted, double Healed, int Info) LsMeasureRow(Formation f, int from, int seeds)
    {
        var v = new double[lsW];
        double kills = 0, whet = 0, heal = 0; int n = 0;
        for (int w = 0; w < lsW; w++)
        {
            int wins = 0;
            for (int seed = from; seed < from + seeds; seed++)
            {
                var r = BattleEngine.Run(f, lsStages[w].Enemy, seed, verbose: false);
                if (r.PlayerWon) wins++;
                if (r.TallyByUnit.TryGetValue(lsNew.Id, out UnitTally? t))
                { kills += t.Kills; whet += t.Whetted; heal += t.Healed; }
                n++;
            }
            v[w] = wins * 100.0 / seeds;
        }
        int info = 0;
        for (int w = 1; w < lsW; w++) if (v[w] > 0.0 && v[w] < 100.0) info++;
        return (v, kills / n, whet / n, heal / n, info);
    }
    string LsCells(double[] v) => string.Concat(v.Select(x => $" {x:F1}% |"));
    double LsAvg25(double[] v) => v.Skip(1).Average();
    string PatName(AttackPattern p) => p switch { AttackPattern.Single => "単体", AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き", _ => "全体" };

    // =====================================================================================
    // phase0: 空白を数える（§1）。在席時勝率と理想台の帰属のぶんだけ戦闘を回す（それ以外は紙）。
    // =====================================================================================
    if (lsArg == "phase0")
    {
        Console.WriteLine("# 第79期 Phase 0 —— 空白を数える（最後の1枠）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 lastslot phase0` の出力。**`docs/` には置かない。**");
        Console.WriteLine("**紙の計算が主**。戦闘を回すのは §1-1 の「在席時勝率（Pw・51 体）」と「理想台の帰属（61 行・素体差し替え）」の2列だけ"
                          + "（どちらも第78期 `traits` の表D と同じ器具・同じ標本・同じ seed 帯 A）。");
        Console.WriteLine();

        // ---- 在席時勝率（Pw・51 体）と理想台の帰属 ------------------------------------------
        var inSum = new Dictionary<string, double>(); var inCnt = new Dictionary<string, int>();
        {
            var rate = new double[LsN]; var mem = new UnitDef[LsN][];
            Console.Error.Write("phase0 Pw(51): ");
            int done = 0;
            Parallel.For(0, LsN, i =>
            {
                UnitDef[] team = LsTeam(lsRoster51, i);
                Formation f = LsFormH(team);
                double sum = 0;
                for (int wi = 1; wi < lsW; wi++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < LsM; seed++)
                        if (BattleEngine.Run(f, lsWeak[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
                    sum += wins * 100.0 / LsM;
                }
                rate[i] = sum / (lsW - 1); mem[i] = team;
                int c = Interlocked.Increment(ref done);
                if (c % 1000 == 0) Console.Error.Write(".");
            });
            Console.Error.WriteLine();
            for (int i = 0; i < LsN; i++)
                foreach (UnitDef d in mem[i])
                {
                    inSum[d.Id] = inSum.GetValueOrDefault(d.Id) + rate[i];
                    inCnt[d.Id] = inCnt.GetValueOrDefault(d.Id) + 1;
                }
        }
        var idSum = new Dictionary<string, double>(); var idCnt = new Dictionary<string, int>();
        {
            int n = lsAllRows.Length;
            var win = new double[n][]; var mem = new UnitDef[n][];
            Console.Error.Write("phase0 理想61行: ");
            Parallel.For(0, n, i =>
            {
                var occ = lsAllRows[i].F.Occupied().ToArray();
                var team = occ.Select(o => o.Def).ToArray();
                var w = new double[6];
                for (int v = 0; v < 6; v++)
                {
                    if (v - 1 >= team.Length) { w[v] = double.NaN; continue; }
                    var f = new Formation();
                    for (int k = 0; k < occ.Length; k++)
                        f[occ[k].Slot] = k == v - 1 ? LsPlain(occ[k].Def) : occ[k].Def;
                    double sum = 0;
                    for (int wi = 1; wi < lsW; wi++)
                    {
                        int wins = 0;
                        for (int seed = 0; seed < LsSeedsIdeal; seed++)
                            if (BattleEngine.Run(f, lsStages[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
                        sum += wins * 100.0 / LsSeedsIdeal;
                    }
                    w[v] = sum / (lsW - 1);
                }
                win[i] = w; mem[i] = team;
            });
            Console.Error.WriteLine();
            for (int i = 0; i < n; i++)
                for (int k = 0; k < mem[i].Length && k < 5; k++)
                {
                    if (double.IsNaN(win[i][k + 1])) continue;
                    string id = mem[i][k].Id;
                    idSum[id] = idSum.GetValueOrDefault(id) + (win[i][0] - win[i][k + 1]);
                    idCnt[id] = idCnt.GetValueOrDefault(id) + 1;
                }
        }

        // ---- §1-1: 入口 0 の駒 --------------------------------------------------------------
        var zero = lsRoster51.Where(d => TraitEntryMap.EntriesOf(d, false).Length == 0).ToArray();
        Console.WriteLine($"## 1-1. 入口 0 の駒（`TraitEntryMap.EntriesOf(u, withFoe: false)` が空・**{zero.Length} 体 / {lsRoster51.Length}**）");
        Console.WriteLine();
        Console.WriteLine($"指示書の当たり 31 体: {(zero.Length == 31 ? "○" : "**×**")}。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 攻/HP/速 | 特性 | 特性数 | 発火口 | キー | 攻撃型 | 在席時勝率（Pw） | 理想台の帰属 | 在席（理想） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|:-:|--:|--:|--:|");
        foreach (UnitDef d in zero.OrderByDescending(u => u.Traits.Count).ThenBy(u => u.Id, StringComparer.Ordinal))
            Console.WriteLine($"| {d.Name} | {d.Attack}/{d.MaxHp}/{d.Speed} | {string.Join("・", d.Traits.Select(t => t.ToString()))} "
                              + $"| {d.Traits.Count} | {TraitHookMap.HooksOf(d).Length} | {TraitKeyMap.KeysOf(d).Length} | {PatName(d.Pattern)} "
                              + $"| {(inCnt.ContainsKey(d.Id) ? (inSum[d.Id] / inCnt[d.Id]).ToString("F2") + "%" : "—")} "
                              + $"| {(idCnt.ContainsKey(d.Id) ? LsP2(idSum[d.Id] / idCnt[d.Id]) : "—")} | {idCnt.GetValueOrDefault(d.Id)} 行 |");
        Console.WriteLine();
        {
            var z = zero.Where(d => inCnt.ContainsKey(d.Id)).ToArray();
            var h = lsRoster51.Where(d => TraitEntryMap.EntriesOf(d, false).Length >= 1 && inCnt.ContainsKey(d.Id)).ToArray();
            Console.WriteLine($"入口 0 の在席時勝率の平均 **{z.Average(d => inSum[d.Id] / inCnt[d.Id]):F2}%**（第78期 47.70）／ "
                              + $"入口 ≥ 1 は {h.Average(d => inSum[d.Id] / inCnt[d.Id]):F2}%（第78期 43.12）。");
            Console.WriteLine($"入口 0 の理想台の帰属の平均 **{LsP2(zero.Where(d => idCnt.ContainsKey(d.Id)).Average(d => idSum[d.Id] / idCnt[d.Id]))}**"
                              + $"（理想61行に出ている {zero.Count(d => idCnt.ContainsKey(d.Id))} 体）。");
        }
        Console.WriteLine();

        // ---- §1-2: 特性2つの駒の2枚目 --------------------------------------------------------
        // **手で作った表**。分類は3つ——代金 / 自給の口（2枚目の供給が1枚目の読みを満たす）/ 別の出力。
        var second = new Dictionary<string, (TraitId First, TraitId Second, string Class, string Why)>
        {
            ["borg"]  = (TraitId.Cinder,    TraitId.Splash,    "代金",   "巻き込み＝同じ一振りが自分の両隣の味方にも当たる"),
            ["gald"]  = (TraitId.Guardian,  TraitId.Stoic,     "代金",   "支援拒否＝回復も強化も受け付けない"),
            ["golm"]  = (TraitId.Colossus,  TraitId.Drain,     "代金",   "大食い＝毎ターン味方から吸う"),
            ["hibi"]  = (TraitId.Shatter,   TraitId.Frail,     "代金",   "脆弱＝受けるダメージが増える"),
            ["kiri"]  = (TraitId.Rend,      TraitId.ThinBlade, "代金",   "薄刃＝与ダメ常に1（第74期に切り出した）"),
            ["nomi"]  = (TraitId.Carve,     TraitId.Fixate,    "代金",   "執着＝1体に縛られる"),
            ["rica"]  = (TraitId.Necro,     TraitId.Sacrifice, "代金",   "生贄＝開戦時に味方全体を削る"),
            ["sekki"] = (TraitId.Rage,      TraitId.RearGuard, "自給の口", "後備えが後列への攻撃を自分へ引き寄せ、被弾強化がそれを読む"),
            ["sero"]  = (TraitId.Sniper,    TraitId.Coward,    "自給の口", "臆病が移動@自分を書き、後衛特化が読む（`TraitEntryMap` の形式判定で打ち消し）"),
        };
        var two = zero.Where(d => d.Traits.Count == 2).OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();
        Console.WriteLine($"## 1-2. 入口 0 で特性2つの駒（**{two.Length} 体**）と、2枚目の分類");
        Console.WriteLine();
        Console.WriteLine("**分類は手で付けた**（3つ: 代金 / 自給の口＝2枚目の供給が1枚目の読みを満たす / 別の出力）。"
                          + "1枚目＝出力側のプラス特性、2枚目＝もう一方。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 1枚目（出力） | 1枚目の発火口 | 2枚目 | 2枚目の発火口 | 分類 | 理由 |");
        Console.WriteLine("|---|---|---|---|---|:-:|---|");
        foreach (UnitDef d in two)
        {
            if (!second.TryGetValue(d.Id, out var sc)) { Console.WriteLine($"| {d.Name} | — | — | — | — | **未分類** | 表に無い（**×**） |"); continue; }
            Console.WriteLine($"| {d.Name} | {sc.First} | {string.Join("・", TraitHookMap.TraitHooks[sc.First])} | {sc.Second} "
                              + $"| {string.Join("・", TraitHookMap.TraitHooks[sc.Second])} | **{sc.Class}** | {sc.Why} |");
        }
        Console.WriteLine();
        Console.WriteLine($"分類の内訳: 代金 **{two.Count(d => second.TryGetValue(d.Id, out var sc) && sc.Class == "代金")}** / "
                          + $"自給の口 **{two.Count(d => second.TryGetValue(d.Id, out var sc) && sc.Class == "自給の口")}** / "
                          + $"別の出力 **{two.Count(d => second.TryGetValue(d.Id, out var sc) && sc.Class == "別の出力")}**。");
        Console.WriteLine();
        Console.WriteLine("**前提の訂正**: 指示書 §1-2 は「第74期に切り出した4枚（ThinBlade / Overreach / Await / Seal）がここに入るはず」と書くが、"
                          + "**入るのは薄刃（キリ）1枚だけ**。深追い（エグ）・刃待ち（ナタ）・塞ぎ（ハリ）の保持者は"
                          + "**傷の読み手なので入口 1**（第78期 表A）で、入口 0 の集合に入らない。");
        Console.WriteLine("棘鎧のカドは特性4つなので「特性2つ」の抜き出しには入らない（棘守り＋棘＋不動＝代金＋惨禍＝自給の口 の混成）。");
        Console.WriteLine();

        // ---- §1-3: 空白の地図 ----------------------------------------------------------------
        var hooks = TraitHookMap.TraitHooks.Values.SelectMany(h => h).Distinct().ToArray();
        int HolderCount(string hook) => lsRoster51.Count(d => TraitHookMap.HooksOf(d).Contains(hook));
        hooks = hooks.OrderBy(HolderCount).ThenBy(h => h, StringComparer.Ordinal).ToArray();
        string[] classes = { "代金", "自給の口", "別の出力" };
        Console.WriteLine("## 1-3. 空白の地図（2枚目の分類 × 1枚目の発火口）");
        Console.WriteLine();
        Console.WriteLine("セル＝その分類の2枚目を持ち、1枚目がその発火口を持つ駒（§1-2 の9体から）。"
                          + "**列は「同じ発火口を持つ既存駒（`All` 51 体・どちらの特性でも）」の少ない順**（同点処理に使う量）。");
        Console.WriteLine();
        Console.WriteLine("| 発火口 | 保持者（51体） | 代金 | 自給の口 | 別の出力 |");
        Console.WriteLine("|---|--:|---|---|---|");
        var empties = new List<(string Hook, string Class, int Holders)>();
        foreach (string h in hooks)
        {
            var cell = new string[3];
            for (int c = 0; c < 3; c++)
            {
                var us = two.Where(d => second.TryGetValue(d.Id, out var sc) && sc.Class == classes[c]
                                        && TraitHookMap.TraitHooks[sc.First].Contains(h))
                            .Select(d => d.Name).ToArray();
                cell[c] = us.Length == 0 ? "**空**" : string.Join("・", us);
                if (us.Length == 0) empties.Add((h, classes[c], HolderCount(h)));
            }
            Console.WriteLine($"| `{h}` | {HolderCount(h)} | {cell[0]} | {cell[1]} | {cell[2]} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**空白は {empties.Count} 組 / {hooks.Length * 3}**（P1 の当たり「5 組以上」: {(empties.Count >= 5 ? "○" : "**×**")}）。"
                          + " 埋まっているのは " + (hooks.Length * 3 - empties.Count) + " 組。");
        Console.WriteLine();

        // ---- §1-4: 候補の選び方（**測る前に固定**）--------------------------------------------
        Console.WriteLine("## 1-4. 候補の選定（規則は指示書 §1-4。適用の仕方をここで固定する）");
        Console.WriteLine();
        Console.WriteLine("(i) 入口 0（`TraitEntryMap.EntriesOf` が空）／(ii) 特性2つ・2枚目は代金か自給の口／"
                          + "(iii) 発火口の組（駒の特性の和）が既存 51 体のどれとも一致しない／(iv) engine に新しい窓口を要求しない。");
        Console.WriteLine();
        Console.WriteLine("**空白の全部が候補になるわけではない**——1枚目に「入口 0 のプラス特性」を書ける発火口だけが候補になる。"
                          + " 発火口ごとの可否（手で判定。理由つき）:");
        Console.WriteLine();
        Console.WriteLine("| 発火口 | 保持者 | 入口 0 のプラスを書けるか | 理由 |");
        Console.WriteLine("|---|--:|:-:|---|");
        var feas = new (string Hook, string Holders, bool Ok, string Why)[]
        {
            ("OnKill",               HolderCount("OnKill").ToString(),               true,  "撃破はキーに無い＝入口 0。既存の `Executioner`（処刑・敵側の語彙）をそのまま再利用できる。味方側のプラスとしては 0 枚"),
            ("OnAllyDamaged",        HolderCount("OnAllyDamaged").ToString(),        true,  "味方の被弾＝敵が供給（入口(味)に数えない）。標を読まない仇討ち＝「隣の味方が殴られたら殴った相手に割り込んで反撃」。**新しい TraitId が要る**"),
            ("ModifyIncomingDamage", HolderCount("ModifyIncomingDamage").ToString(), true,  "自分の HP だけを条件に被ダメを下げる（例: 半分以下で半減）。**新しい TraitId が要る**"),
            ("CanReact",             HolderCount("CanReact").ToString(),             false, "反応を「止める」だけの窓口（のろま）。プラスは書けない"),
            ("BlocksSupport",        HolderCount("BlocksSupport").ToString(),        false, "支援を「断る」だけの窓口（支援拒否）。プラスは書けない"),
            ("OnMoved / OnAllyMoved", "2 / 2", false, "移動を読む＝入口 1。自給（臆病）で打ち消す形はセロ（後衛特化＋臆病）が既にあり、保持者も 2 で最少ではない"),
            ("OnAllyDeath / OnAnyDeath / OnDeath", "2 / 3 / 3", false, "死はキーに無いので入口 0 で書けるが、保持者 2〜3 で最少ではない（規則の第1段で落ちる）"),
        };
        foreach (var f in feas) Console.WriteLine($"| `{f.Hook}` | {f.Holders} | {(f.Ok ? "○" : "×")} | {f.Why} |");
        Console.WriteLine();
        Console.WriteLine("**2枚目の決め方**（指示書は「代金または自給の口」としか言わないので、ここで固定する）:");
        Console.WriteLine("自給の口 ＝「2枚目の `Supplies` が1枚目の `Reads` を満たす」。**撃破・味方の被弾・自分の HP はどれもキーに無いので、"
                          + "3候補とも自給の口は定義上作れず、2枚目は代金で確定。** 代金は既存のマイナス側 `TraitId` から、"
                          + "**(a) 1枚目と発火口が重ならず (b) 組が既存駒と一致せず (c) その特性の発火口（`engine` を除く）の保持者が最も少ないもの。"
                          + "同数なら `TraitId` の列挙順**。→ 保持者 1 で並ぶのは 支援拒否（`BlocksSupport`＝ガルド）・のろま（`CanReact`＝ドルガ）・"
                          + "脆弱（`ModifyIncomingDamage`＝ヒビ）で、**列挙順で支援拒否**。");
        Console.WriteLine();
        Console.WriteLine("**攻撃型の決め方**: **機構が燃料を自分の一振りでしか作れないときだけ薙ぎ、それ以外は単体（既定）**。"
                          + " 処刑の燃料（撃破）は攻6 の単体ではほぼ出ないので、届く相手を3体にする薙ぎが燃料の自給そのもの。"
                          + " 報い・痩せ我慢は一振りに依存しないので単体。");
        Console.WriteLine();
        var cands = new (string Id, string Name, string Hook, string First, bool NewTrait, string Second, string Pattern, string[] Hooks)[]
        {
            ("ono",  "首刈りのオノ",     "OnKill",               "Executioner（既存・再利用）", false, "Stoic", "薙ぎ", new[] { "BlocksSupport", "OnKill", "engine" }),
            ("muku", "報いのムク",       "OnAllyDamaged",        "報い（新規）",               true,  "Stoic", "単体", new[] { "BlocksSupport", "OnAllyDamaged", "engine" }),
            ("yase", "痩せ我慢のヤセ",   "ModifyIncomingDamage", "痩せ我慢（新規）",           true,  "Stoic", "単体", new[] { "BlocksSupport", "ModifyIncomingDamage", "engine" }),
        };
        var hookSets = lsRoster51.Select(d => string.Join("+", TraitHookMap.HooksOf(d))).ToHashSet();
        Console.WriteLine("| 候補 | 1枚目の発火口 | 保持者 | 1枚目 | 2枚目 | 攻撃型 | (i) 入口 | (ii) | (iii) 組が一意 | (iv) engine |");
        Console.WriteLine("|---|---|--:|---|---|:-:|:-:|:-:|:-:|:-:|");
        foreach (var c in cands)
        {
            string set = string.Join("+", c.Hooks.OrderBy(x => x, StringComparer.Ordinal));
            Console.WriteLine($"| {c.Name}（`{c.Id}`） | `{c.Hook}` | {HolderCount(c.Hook)} | {c.First} | {c.Second}（代金） | {c.Pattern} "
                              + $"| 0 | ○ | {(hookSets.Contains(set) ? "**×**" : "○")} | {(c.NewTrait ? "○（フックは既存・`TraitId` は新規）" : "○（既存の特性2枚）")} |");
        }
        Console.WriteLine();
        int minHold = cands.Min(c => HolderCount(c.Hook));
        var best = cands.OrderBy(c => HolderCount(c.Hook)).ThenBy(c => c.Pattern == "単体" ? 1 : 0).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Console.WriteLine($"**規則の適用**: 保持者の最少は {minHold} で {cands.Count(c => HolderCount(c.Hook) == minHold)} 候補が並ぶ → "
                          + $"攻撃型が単体でないものは **{string.Join("・", best.Where(c => HolderCount(c.Hook) == minHold && c.Pattern != "単体").Select(c => c.Name))}** "
                          + $"→ **選定: {best[0].Name}（`{best[0].Id}`）**。（P2 の当たり「候補 1〜3」: {(cands.Length >= 1 && cands.Length <= 3 ? "○" : "**×**")}・実測 {cands.Length}）");
        Console.WriteLine();
        Console.WriteLine("**もし攻撃型を3候補とも単体に揃えていたら `Def.Id` の辞書順で `muku`（報い）が選ばれていた**——"
                          + "同点処理の第2段（攻撃型）が決定的だったことを記録しておく。薙ぎを付けた理由は上の「攻撃型の決め方」。");
        Console.WriteLine();

        // 実体の検査（選んだ駒は `UnitCatalog.Ono` として既に定義してある）。
        Console.WriteLine("### 選んだ駒の実体（`UnitCatalog.Ono`・**`All` には載せていない**）");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 値 | 検査 |");
        Console.WriteLine("|---|---|:-:|");
        Console.WriteLine($"| 特性 | {string.Join("・", lsNew.Traits)} | {(lsNew.Traits.Count == 2 ? "○" : "**×**")} |");
        Console.WriteLine($"| 入口（味方） | {TraitEntryMap.EntriesOf(lsNew, false).Length} | {(TraitEntryMap.EntriesOf(lsNew, false).Length == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| 入口（敵含む） | {TraitEntryMap.EntriesOf(lsNew, true).Length} | — |");
        Console.WriteLine($"| 発火口 | {string.Join("・", TraitHookMap.HooksOf(lsNew))} | {(hookSets.Contains(string.Join("+", TraitHookMap.HooksOf(lsNew))) ? "**×（既存と一致）**" : "○（一意）")} |");
        Console.WriteLine($"| キー | {TraitKeyMap.KeysOf(lsNew).Length} | — |");
        Console.WriteLine($"| 攻撃型 | {PatName(lsNew.Pattern)} | — |");
        Console.WriteLine();

        // ---- §1-5: 数値 -----------------------------------------------------------------------
        double Med(IEnumerable<int> xs) { var a = xs.OrderBy(x => x).ToArray(); return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2.0; }
        double mHp = Med(lsRoster51.Select(d => d.MaxHp)), mAtk = Med(lsRoster51.Select(d => d.Attack)), mSpd = Med(lsRoster51.Select(d => d.Speed));
        Console.WriteLine("## 1-5. 数値（規則で決める）");
        Console.WriteLine();
        Console.WriteLine($"ロスター 51 体の中央値: **HP {mHp:F0} / 攻 {mAtk:F0} / 速 {mSpd:F0}**（指示書の 60 / 6 / 7: {(mHp == 60 && mAtk == 6 && mSpd == 7 ? "○" : "**×**")}）。");
        Console.WriteLine($"新駒: HP {lsNew.MaxHp} / 攻 {lsNew.Attack} / 速 {lsNew.Speed} — HP・速は中央値、攻は「6 以下」を**中央値ちょうど**で固定"
                          + $"（{(lsNew.MaxHp == mHp && lsNew.Speed == mSpd && lsNew.Attack <= mAtk ? "○" : "**×**")}）。");
        Console.WriteLine();

        // ---- §1-6 / §1-7 ------------------------------------------------------------------------
        int overlap = Baseline.PrimaryRows.Count(n => lsAllRows.Any(r => r.Name == n && r.F.Occupied().Any(o => o.Def.Id == lsNew.Id)));
        Console.WriteLine("## 1-6 / 1-7. 主判定19行との重なりと現在値");
        Console.WriteLine();
        Console.WriteLine($"主判定 {Baseline.PrimaryRows.Length} 行のうち新駒を含む行: **{overlap}**（Q6 の実効レンジは **0**）。");
        Console.WriteLine($"主判定19行の第五波平均（`docs/balance.md`）: **{LsPrimaryW5():F1}%**・歯止め 33.2% との余裕 **{LsPrimaryW5() - 33.2:+0.0;-0.0}pt**。");
        Console.WriteLine($"`docs/balance.md` の現在値: **{lsBalance.Count} 行 × {lsW} 波 = {lsBalance.Count * lsW} セル**（指示書の 305: {(lsBalance.Count * lsW == 305 ? "○" : "**×**")}）。");
        Console.WriteLine($"試験行3本の名前は `CompareBuilds()` に無い: {(lsRows.All(r => !lsBalance.ContainsKey(r.Name)) ? "○" : "**×**")}。");
        Console.WriteLine();
        LsPrintPick();

        Console.WriteLine("## 予測（**測る前に書いた**）");
        Console.WriteLine();
        Console.WriteLine("| # | 量 | 予測 |");
        Console.WriteLine("|--:|---|---|");
        Console.WriteLine("| P1 | §1-3 の空白 | **5 組以上** |");
        Console.WriteLine("| P2 | 候補の数 | **1〜3** |");
        Console.WriteLine("| P3 | ドラフト台の帰属 | **+1.5〜+5pt**（入口 0 の駒の平均 +3.43 に近い） |");
        Console.WriteLine("| P4 | 理想台 | **試験行でのみ動く。既存61行は 0 件** |");
        Console.WriteLine("| P5 | 情報セル | 試験行で **2 以上** |");
        Console.WriteLine("| P6 | 台の一致 | **両方の台で符号が一致する** |");
        Console.WriteLine();
        Console.WriteLine("## 判定基準（**測る前に固定**・指示書 §3 の写し）");
        Console.WriteLine();
        Console.WriteLine("Q1 ドラフト台の在席時帰属 ≥ +1.5pt・両帯で符号一致／Q2 単騎・軸あり の帰属 ≥ +1.5pt・両帯で符号一致／"
                          + "Q3 帰属(軸あり) − 帰属(単騎) ≤ 帰属(単騎)／Q4 罠で代金が実在（回数と実額）／Q5 単騎・軸あり が第2〜5波のうち 2 つ以上で開区間／"
                          + "Q6 歯止め（実効レンジ 0）／Q7 `All` に入れない版で `compare` 305 セル 0 件／Q8 Q1 と Q2 の符号一致。**採る条件は Q1・Q2・Q3・Q5・Q6・Q8 の全部。**");
        Console.WriteLine();
        Console.WriteLine($"所要 {lsSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // seats: 単騎・軸あり の配置探索（reseat 120通り → confirm seed 200..599）。発火（撃破）の列つき。
    // =====================================================================================
    if (lsArg == "seats")
    {
        const int CfFrom = 200, CfTo = 600;
        Console.WriteLine("# 第79期 —— 単騎・軸あり の配置（reseat → confirm）");
        Console.WriteLine();
        Console.WriteLine("粗探索は 120 通り × 5 波 × seed 0..49、追試は seed 200..599。採否閾値 **5.0pt**（第46期）。"
                          + "**`reseat` は席を決めるためだけに使う**（帰属の器具ではない・第64期）。**発火（撃破/戦）が 0 の席は採らない**（第50期）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | オノの席 | 次数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 現行との差 | 撃破/戦 | 情報セル |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in lsRows.Take(2))
        {
            var members = b.F.Occupied().Select(x => x.Def).ToList();
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
                foreach (EnemyCatalog.Stage st in lsStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            });
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));
            var cur = LsMeasureRow(b.F, CfFrom, CfTo - CfFrom);
            int cs = LsSeatOf(b.F, lsNew);
            Console.WriteLine($"| {b.Name} | **現行** | {FormationRules.SeatNames[cs]} | {LsDeg(cs)} |{LsCells(cur.Cells)} **{cur.Cells.Average():0.0}%** | — | {cur.Kills:0.00} | {cur.Info} |");
            Console.WriteLine($"|   ↳ 配置 | {LsPlace(b.F)} | | | | | | | | | | | |");
            Console.Out.Flush();
            foreach (int idx in order.Take(5))
            {
                if (idx == curIdx) continue;
                var v = LsMeasureRow(perms[idx], CfFrom, CfTo - CfFrom);
                int seat = LsSeatOf(perms[idx], lsNew);
                Console.WriteLine($"| {b.Name} | 粗探索 {order.IndexOf(idx) + 1}位 | {FormationRules.SeatNames[seat]} | {LsDeg(seat)} |{LsCells(v.Cells)} {v.Cells.Average():0.0}% "
                                  + $"| **{v.Cells.Average() - cur.Cells.Average():+0.0;-0.0;0.0}pt** | {v.Kills:0.00} | {v.Info} |");
                Console.WriteLine($"|   ↳ 配置 | {LsPlace(perms[idx])} | | | | | | | | | | | |");
                Console.Out.Flush();
            }
            Console.WriteLine($"|   ↳ 現行の順位 | **粗探索 {order.IndexOf(curIdx) + 1}位 / {perms.Count}通り** | | | | | | | | | | | |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {lsSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check: `compare` 305 セルの突き合わせ（新駒は `All` に入れていないので、動く理由が無い）。
    // =====================================================================================
    if (lsArg == "check")
    {
        Console.WriteLine("# 第79期 —— 受け入れ基準（Q7）: `compare` 305 セルの突き合わせ");
        Console.WriteLine();
        int mism = 0, cellsN = 0, missing = 0;
        var lines = new List<string>();
        Parallel.For(0, lsAllRows.Length, i =>
        {
            var v = new double[lsW];
            for (int w = 0; w < lsW; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(lsAllRows[i].F, lsStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                v[w] = wins * 100.0 / 200;
            }
            lock (lines)
            {
                if (!lsBalance.TryGetValue(lsAllRows[i].Name, out double[]? doc)) { missing++; return; }
                for (int w = 0; w < lsW; w++)
                {
                    cellsN++;
                    if (Math.Abs(doc[w] - v[w]) > 0.05) { mism++; lines.Add($"| {lsAllRows[i].Name} | 第{w + 1}波 | {doc[w]:F1} | {v[w]:F1} |"); }
                }
            }
        });
        Console.WriteLine($"`CompareBuilds()` {lsAllRows.Length} 行 × {lsW} 波 × seed 0..199 を回し直して `docs/balance.md` と突き合わせた: "
                          + $"**{cellsN} セル・ずれ {mism} 件**（`docs/balance.md` に無い行 {missing}）。");
        if (lines.Count > 0)
        {
            Console.WriteLine(); Console.WriteLine("| 行 | 波 | docs | 今 |"); Console.WriteLine("|---|---|--:|--:|");
            foreach (string l in lines) Console.WriteLine(l);
        }
        Console.WriteLine();
        Console.WriteLine($"`UnitCatalog.All` は {UnitCatalog.All.Count} 体（新駒を含まない: {(UnitCatalog.All.All(d => d.Id != lsNew.Id) ? "○" : "**×**")}）。"
                          + " `compare` は `All` を読まない（行は `CompareBuilds()` に明示）ので、新駒を `All` に入れてもここは動かない"
                          + "——**動くのはドラフト台の抽選だけ**（主表 §1 の「52版・素体」列がその分離）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {lsSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    if (lsArg != "" && lsArg != "alt")
    {
        Console.WriteLine("lastslot: 引数は phase0 / seats / check / （無し）/ alt。");
        return;
    }

    // =====================================================================================
    // 主表: ドラフト台（51版 / 52版・現行 / 52版・素体）と理想台（試験行3本 × 3版）
    // =====================================================================================
    Console.WriteLine($"# 第79期 —— 最後の1枠（首刈りのオノ）・{lsBandName} 帯");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 lastslot{(lsBand == 0 ? "" : " alt")}` の出力。**`docs/` には置かない。**");
    Console.WriteLine();

    // ---- ドラフト台 ----------------------------------------------------------------------------
    var r51 = new double[LsN];
    var r52 = new double[LsN]; var r52p = new double[LsN]; var has52 = new bool[LsN];
    var w52 = new double[LsN][]; var w52p = new double[LsN][];
    var kills52 = new double[LsN]; var seat52 = new int[LsN];
    {
        int done = 0;
        Console.Error.Write($"{lsBandName}帯 51版: ");
        Parallel.For(0, LsN, i =>
        {
            Formation f = LsFormH(LsTeam(lsRoster51, i));
            double sum = 0;
            for (int wi = 1; wi < lsW; wi++)
            {
                int wins = 0;
                for (int seed = lsBand; seed < lsBand + LsM; seed++)
                    if (BattleEngine.Run(f, lsWeak[wi].Enemy, seed, verbose: false).PlayerWon) wins++;
                sum += wins * 100.0 / LsM;
            }
            r51[i] = sum / (lsW - 1);
            int c = Interlocked.Increment(ref done);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        done = 0;
        Console.Error.Write($"{lsBandName}帯 52版: ");
        Parallel.For(0, LsN, i =>
        {
            UnitDef[] team = LsTeam(lsRoster52, i);
            int[] seats = LsSeats(team);
            bool has = team.Any(d => ReferenceEquals(d, lsNew));
            has52[i] = has;
            seat52[i] = has ? seats[Array.FindIndex(team, d => ReferenceEquals(d, lsNew))] : -1;
            var wv = new double[lsW]; var wvp = new double[lsW];
            double kills = 0; int nb = 0;
            for (int v = 0; v < (has ? 2 : 1); v++)
            {
                Formation f = LsForm(team, seats, v == 0 ? null : d => ReferenceEquals(d, lsNew) ? LsPlain(d) : d);
                for (int wi = 1; wi < lsW; wi++)
                {
                    int wins = 0;
                    for (int seed = lsBand; seed < lsBand + LsM; seed++)
                    {
                        var r = BattleEngine.Run(f, lsWeak[wi].Enemy, seed, verbose: false);
                        if (r.PlayerWon) wins++;
                        if (v == 0 && has && r.TallyByUnit.TryGetValue(lsNew.Id, out UnitTally? t)) { kills += t.Kills; nb++; }
                    }
                    (v == 0 ? wv : wvp)[wi] = wins * 100.0 / LsM;
                }
            }
            r52[i] = wv.Skip(1).Average(); w52[i] = wv;
            r52p[i] = has ? wvp.Skip(1).Average() : double.NaN; w52p[i] = wvp;
            kills52[i] = has && nb > 0 ? kills / nb : double.NaN;
            int c = Interlocked.Increment(ref done);
            if (c % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
    }
    var inIdx = Enumerable.Range(0, LsN).Where(i => has52[i]).ToArray();
    double inRate = inIdx.Average(i => r52[i]);
    double inPlain = inIdx.Average(i => r52p[i]);
    var attr = inIdx.Select(i => r52[i] - r52p[i]).ToArray();
    double attrMean = attr.Average(), attrSe = LsSd(attr) / Math.Sqrt(attr.Length);
    // 52版・素体 ＝ 新駒を含む標本だけ素体に落とした全体（抽選の変化ぶん＝「体」だけが入った 52 体のロスター）
    double all52plain = Enumerable.Range(0, LsN).Average(i => has52[i] ? r52p[i] : r52[i]);

    Console.WriteLine("## 1. ドラフト台（11,000 標本 × 弱い波 × seed 8 本・規則配置 H・Pw）");
    Console.WriteLine();
    Console.WriteLine("**51 → 52 の分母の変化を分離する**（自己検査 (d)）: `52版・素体` は新駒を含む標本だけを素体に落とした全体で、"
                      + "`51版` との差が**抽選の変化ぶん**（同じ数値の体が 52 枚目に入った影響）、`52版・現行` との差が**機構の帰属**。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 標本 | 平均勝率（第2〜5波） | 0% の標本 | 100% の標本 | 中間帯 5〜95% |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    void DraftLine(string name, Func<int, double> v)
    {
        var xs = Enumerable.Range(0, LsN).Select(v).ToArray();
        Console.WriteLine($"| {name} | {xs.Length:N0} | {xs.Average():F2}% | {xs.Count(x => x <= 0):N0} ({100.0 * xs.Count(x => x <= 0) / xs.Length:F1}%) "
                          + $"| {xs.Count(x => x >= 100):N0} | {100.0 * xs.Count(x => x > 5 && x < 95) / xs.Length:F1}% |");
    }
    DraftLine("51版（現行ロスター）", i => r51[i]);
    DraftLine("52版・素体", i => has52[i] ? r52p[i] : r52[i]);
    DraftLine("**52版・現行**", i => r52[i]);
    Console.WriteLine();
    Console.WriteLine($"抽選の変化ぶん（52版・素体 − 51版）: **{LsP2(all52plain - r51.Average())}pt**／"
                      + $"機構の帰属（52版・現行 − 52版・素体・全標本平均）: **{LsP2(r52.Average() - all52plain)}pt**"
                      + $"（＝在席率 {100.0 * inIdx.Length / LsN:F1}% × 在席時帰属 {LsP2(attrMean)}）。");
    Console.WriteLine();
    Console.WriteLine("### Q1 —— 在席時の帰属（素体差し替え・第69期の標準器具）");
    Console.WriteLine();
    Console.WriteLine("| 量 | 値 |");
    Console.WriteLine("|---|--:|");
    Console.WriteLine($"| 在席した標本 | {inIdx.Length:N0} / {LsN:N0}（{100.0 * inIdx.Length / LsN:F1}%・一様なら {100.0 * 5 / 52:F1}%） |");
    Console.WriteLine($"| 在席時勝率（現行） | **{inRate:F2}%** |");
    Console.WriteLine($"| 在席時勝率（素体） | {inPlain:F2}% |");
    Console.WriteLine($"| **在席時の帰属** | **{LsP2(attrMean)}pt**（標準誤差 ±{attrSe:F2}） |");
    Console.WriteLine($"| 帰属が正の標本 / 負の標本 / 0 | {attr.Count(x => x > 0):N0} / {attr.Count(x => x < 0):N0} / {attr.Count(x => x == 0):N0} |");
    Console.WriteLine($"| 撃破/戦（在席時・現行） | {inIdx.Where(i => !double.IsNaN(kills52[i])).Average(i => kills52[i]):F2} |");
    Console.WriteLine($"| 撃破が 0 の標本 | {100.0 * inIdx.Count(i => !double.IsNaN(kills52[i]) && kills52[i] == 0) / inIdx.Length:F1}% |");
    Console.WriteLine("| 第78期の入口 0 の平均 | 在席時勝率 47.70% / 帰属 +3.43（P3 の帯 +1.5〜+5） |");
    Console.WriteLine();
    Console.WriteLine("波ごとの在席時帰属:");
    Console.WriteLine();
    Console.WriteLine("| 波 | 現行 | 素体 | 帰属 |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int wi = 1; wi < lsW; wi++)
        Console.WriteLine($"| {lsStages[wi].Name} | {inIdx.Average(i => w52[i][wi]):F2}% | {inIdx.Average(i => w52p[i][wi]):F2}% | {LsP2(inIdx.Average(i => w52[i][wi] - w52p[i][wi]))} |");
    Console.WriteLine();
    Console.WriteLine("席ごとの在席時帰属（規則配置 H が新駒をどこへ置いたか）:");
    Console.WriteLine();
    Console.WriteLine("| 席 | 標本 | 現行 | 帰属 | 撃破/戦 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int st = 0; st < 5; st++)
    {
        var ii = inIdx.Where(i => seat52[i] == st).ToArray();
        if (ii.Length == 0) { Console.WriteLine($"| {FormationRules.SeatNames[st]} | 0 | — | — | — |"); continue; }
        var kk = ii.Where(i => !double.IsNaN(kills52[i])).ToArray();
        Console.WriteLine($"| {FormationRules.SeatNames[st]} | {ii.Length:N0} | {ii.Average(i => r52[i]):F2}% | {LsP2(ii.Average(i => r52[i] - r52p[i]))} | {(kk.Length == 0 ? "—" : kk.Average(i => kills52[i]).ToString("F2"))} |");
    }
    Console.WriteLine();

    // ---- 理想台 ----------------------------------------------------------------------------------
    Console.WriteLine($"## 2. 理想台（試験行3本 × 3版 × 5波 × seed {lsBand}..{lsBand + LsSeedsIdeal - 1}）");
    Console.WriteLine();
    Console.WriteLine("版: **現行**（処刑＋支援拒否）／**素体**（同数値・特性なし・薙ぎ）／**対照**（支援拒否だけを外した版＝罠の代金を数えるため）。"
                      + " 帳簿（撃破・受けた強化・受けた回復）はオノのぶんだけ。**帰属 ＝ 現行 − 素体（第2〜5波の平均）**。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均(2〜5) | 帰属 | 撃破/戦 | 受けた強化/戦 | 受けた回復/戦 | 情報セル |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var idealAttr = new double[lsRows.Length];
    var idealInfo = new int[lsRows.Length];
    double trapCur = 0, trapCtl = 0, trapWhet = 0, trapHeal = 0;
    for (int ri = 0; ri < lsRows.Length; ri++)
    {
        var (name, f) = lsRows[ri];
        var cur = LsMeasureRow(f, lsBand, LsSeedsIdeal);
        var pla = LsMeasureRow(LsSub(f, lsNew, LsPlain(lsNew)), lsBand, LsSeedsIdeal);
        var ctl = LsMeasureRow(LsSub(f, lsNew, lsNoStoic), lsBand, LsSeedsIdeal);
        idealAttr[ri] = LsAvg25(cur.Cells) - LsAvg25(pla.Cells);
        idealInfo[ri] = cur.Info;
        Console.WriteLine($"| **{name}** | 現行 |{LsCells(cur.Cells)} **{LsAvg25(cur.Cells):F1}%** | **{LsP2(idealAttr[ri])}** | {cur.Kills:F2} | {cur.Whetted:F2} | {cur.Healed:F2} | {cur.Info} |");
        Console.WriteLine($"| | 素体 |{LsCells(pla.Cells)} {LsAvg25(pla.Cells):F1}% | — | — | — | — | {pla.Info} |");
        Console.WriteLine($"| | 対照（支援拒否なし） |{LsCells(ctl.Cells)} {LsAvg25(ctl.Cells):F1}% | {LsP2(LsAvg25(ctl.Cells) - LsAvg25(pla.Cells))} | {ctl.Kills:F2} | {ctl.Whetted:F2} | {ctl.Healed:F2} | {ctl.Info} |");
        Console.WriteLine($"|   ↳ 配置 | {LsPlace(f)} | | | | | | | | | | | |");
        if (ri == 2) { trapCur = LsAvg25(cur.Cells); trapCtl = LsAvg25(ctl.Cells); trapWhet = ctl.Whetted; trapHeal = ctl.Healed; }
        Console.Out.Flush();
    }
    Console.WriteLine();
    // 参考: 出発点 H（オノ後1・リィカ中央）の同じ3版。**採否には使わない**——採用席は `reseat`→`confirm` の
    // 「勝つ席」で、撃破/戦がいちばん多い席ではない（第59期「勝つ席と測れる席は別」）。両方を並べて読めるようにする。
    Console.WriteLine("### 参考 —— 出発点 H（規則配置）での同じ3版（採否には使わない）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均(2〜5) | 帰属 | 撃破/戦 | 受けた強化/戦 | 受けた回復/戦 | 情報セル |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var (name, f) in new[] { ("単騎・H", LsFormH(lsSoloTeam)), ("軸あり・H", LsFormH(lsAxisTeam)) })
    {
        var cur = LsMeasureRow(f, lsBand, LsSeedsIdeal);
        var pla = LsMeasureRow(LsSub(f, lsNew, LsPlain(lsNew)), lsBand, LsSeedsIdeal);
        var ctl = LsMeasureRow(LsSub(f, lsNew, lsNoStoic), lsBand, LsSeedsIdeal);
        Console.WriteLine($"| **{name}** | 現行 |{LsCells(cur.Cells)} **{LsAvg25(cur.Cells):F1}%** | **{LsP2(LsAvg25(cur.Cells) - LsAvg25(pla.Cells))}** | {cur.Kills:F2} | {cur.Whetted:F2} | {cur.Healed:F2} | {cur.Info} |");
        Console.WriteLine($"| | 素体 |{LsCells(pla.Cells)} {LsAvg25(pla.Cells):F1}% | — | — | — | — | {pla.Info} |");
        Console.WriteLine($"| | 対照（支援拒否なし） |{LsCells(ctl.Cells)} {LsAvg25(ctl.Cells):F1}% | {LsP2(LsAvg25(ctl.Cells) - LsAvg25(pla.Cells))} | {ctl.Kills:F2} | {ctl.Whetted:F2} | {ctl.Healed:F2} | {ctl.Info} |");
        Console.WriteLine($"|   ↳ 配置 | {LsPlace(f)} | | | | | | | | | | | |");
    }
    Console.WriteLine();
    Console.WriteLine("### Q4 —— 罠（支援拒否の代金は実在するか）");
    Console.WriteLine();
    Console.WriteLine($"対照（支援拒否なし）でオノが受け取った強化 **{trapWhet:F2} 量/戦**・回復 **{trapHeal:F2} 点/戦** が、現行では**全部 0**（支援拒否が弾いた量）。");
    Console.WriteLine($"代金の実額（勝率）: 現行 {trapCur:F1}% − 対照 {trapCtl:F1}% = **{LsP2(trapCur - trapCtl)}pt**"
                      + "（負なら代金が効いている。|1.5| 未満は 0 と読む・第57期）。");
    Console.WriteLine();

    // ---- 判定 -----------------------------------------------------------------------------------
    double q6 = LsPrimaryW5();
    bool q1 = attrMean >= 1.5;
    bool q2 = idealAttr[0] >= 1.5 && idealAttr[1] >= 1.5;
    bool q3 = (idealAttr[1] - idealAttr[0]) <= idealAttr[0];
    bool q5 = idealInfo[0] >= 2 && idealInfo[1] >= 2;
    bool q6ok = q6 >= 33.2;
    // 台が割れた ＝ 符号が逆（ドラフトで正・理想で負、またはその逆）。0 は割れではない。
    bool q8 = Math.Sign(attrMean) * Math.Sign(idealAttr[0]) >= 0 && Math.Sign(attrMean) * Math.Sign(idealAttr[1]) >= 0;
    string Sg(double x) => x > 0.005 ? "+" : x < -0.005 ? "−" : "0";
    Console.WriteLine($"## 3. 判定（{lsBandName} 帯。**両帯で符号一致が要る判定は、もう一方の帯の出力と突き合わせて報告書で確定する**）");
    Console.WriteLine();
    Console.WriteLine("| # | 問い | この帯の値 | 判定 |");
    Console.WriteLine("|--:|---|---|:-:|");
    Console.WriteLine($"| Q1 | ドラフト台の在席時帰属 ≥ +1.5 | {LsP2(attrMean)}pt | {(q1 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q2 | 理想台の帰属（単騎 / 軸あり）≥ +1.5 | {LsP2(idealAttr[0])} / {LsP2(idealAttr[1])} | {(q2 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q3 | 帰属(軸) − 帰属(単騎) ≤ 帰属(単騎) | {LsP2(idealAttr[1] - idealAttr[0])} ≤ {LsP2(idealAttr[0])} | {(q3 ? "○" : "**×（相方が本体）**")} |");
    Console.WriteLine($"| Q4 | 罠の代金（採否には使わない） | 弾いた強化 {trapWhet:F2} / 回復 {trapHeal:F2}・実額 {LsP2(trapCur - trapCtl)}pt | {(trapWhet + trapHeal > 0 ? "実在" : "**不発**")} |");
    Console.WriteLine($"| Q5 | 情報セル（単騎 / 軸あり / 罠） | {idealInfo[0]} / {idealInfo[1]} / {idealInfo[2]} | {(q5 ? "○" : "**×**")} |");
    Console.WriteLine($"| Q6 | 歯止め（主判定19行の第五波） | {q6:F1}%・余裕 {q6 - 33.2:+0.0;-0.0}pt（実効レンジ 0） | {(q6ok ? "○" : "**×**")} |");
    Console.WriteLine("| Q7 | 無関係 | `lastslot check` で別に確認 | — |");
    Console.WriteLine($"| Q8 | 台の一致（Q1 と Q2 の符号） | {Sg(attrMean)} / {Sg(idealAttr[0])} / {Sg(idealAttr[1])} | {(q8 ? "○" : "**×（台が割れた）**")} |");
    Console.WriteLine();
    Console.WriteLine($"所要 {lsSw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}
}
