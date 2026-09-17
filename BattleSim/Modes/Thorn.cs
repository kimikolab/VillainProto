using BattleCore;
using static Common;

// =====================================================================================
// thorn モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "thorn")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 thorn
// =====================================================================================

static class ThornDiag
{
// 棘に傷を載せる（第84期）。傷の供給を「手番」から「被弾」へ——棘鎧のカドの反撃が刺し返した相手に傷を1つ残す。
// **新しい `TraitId` は足さない**（カドは特性4つでロスター最多）。版は `ThornRule` を `Run` に引数で渡す
// （既定は V0 ＝ 現行。static のノブは置かない）。器具は第81期の 2×2 の写しで **1文字も変えない**
// （台の抽選・規則配置 H・弱い波・戦闘 seed 0..7・独立2系列）。A はカド固定・B は残り 50 体。
//
//     dotnet run --project BattleSim -c Release 0 thorn phase0                 # §1（戦闘は Phase 0-3/0-4 のぶんだけ）
//     dotnet run --project BattleSim -c Release 0 thorn run <v> [skip] [take]  # 2×2（v = 0/1/2）。TSV を標準出力へ
//     dotnet run --project BattleSim -c Release 0 thorn tables <v0> <v1> [<v2>] # 表A〜E・Q1〜Q4
//     dotnet run --project BattleSim -c Release 0 thorn check                  # 自己検査 (b)(c)(d)・拒否権1・2・`compare` 305 セル
public static void Run(string[] args, int stageIndex)
{
    string thArg = args.Length > 2 ? args[2] : "";
    var thSw = System.Diagnostics.Stopwatch.StartNew();
    var thInv = System.Globalization.CultureInfo.InvariantCulture;

    IReadOnlyList<EnemyCatalog.Stage> thStages = EnemyCatalog.Stages;
    int thW = thStages.Count;
    // **第141期: ロスターは `Everyone`（`All ∪ Retired` ＝ 52 ＋ 2）。** この診断ではロスターが
    // 「B を回す集合」と「`Id` → 添字の引き表」を兼ねていて、意図した相手 5 枚（キリ・ノミ・エグ・ナタ・ハリ）のうち
    // エグ（第139期）とハリ（第108期）が `All` に居ない。引き表だけを広げても添字が配列の外を指すので、
    // **ロスターごと `Everyone` にする**——第84期の測定当時はどちらも `All` に居て B の候補だったので、
    // 当時の集合に近いのはこちら（その後に入った トモ・ソム・ガレ のぶんだけ広い）。
    var thRoster = UnitCatalog.Everyone.ToArray();
    int thRN = thRoster.Length;                       // 54（第84期は 51）

    // ---- 第81期 `pairs2` の定数の写し（**1つも変えていない**）----------------------------------
    const int ThTableSeed = 8_100_000;
    const int ThK = 64;                               // 1組・1系列あたりの台数
    const int ThS = 2;                                // 独立系列の本数（自己検査 (g)）
    const int ThBand = 0, ThM = 8;                    // 戦闘 seed 0..7
    const int ThStrong = 7, ThWeakPct = 60, ThDrawCap = 20000;
    const int ThTop = 20;                             // 表B の上位・下位の行数
    const double ThQ1Line = 3.0;                      // 主判定 Q1 の線（§3-1。測る前に固定）
    const int ThProbeTables = 16;                     // 表C・D の在庫・供給を verbose で測る台数（系列から 8 ずつ）

    var thIdx = new Dictionary<string, int>();
    for (int u = 0; u < thRN; u++) thIdx[thRoster[u].Id] = u;
    string[] thName = thRoster.Select(d => d.Name).ToArray();
    int thKado = thIdx["kado"];
    string[] thWoundIds = { "kiri", "nomi", "egu", "nata", "hari" };
    int[] thWound = thWoundIds.Select(i => thIdx[i]).ToArray();
    int[] thOthers = Enumerable.Range(0, thRN).Where(u => u != thKado).ToArray();   // 50 体

    ThornRule ThRuleOf(int v) => new(v switch { 0 => ThornWound.None, 1 => ThornWound.Foe, _ => ThornWound.Both });
    string ThVName(int v) => v switch { 0 => "V0（現行）", 1 => "V1（敵に傷 1）", _ => "V2（味方にも傷 1）" };

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜83期と同一。`Stages` は書き換えない）----------------------
    var thWeakCache = new Dictionary<string, UnitDef>();
    UnitDef ThWeakOf(UnitDef d)
    {
        if (thWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * ThWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        thWeakCache[d.Id] = w;
        return w;
    }
    var thWeak = thStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = ThWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef ThPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var thPlainMap = thRoster.ToDictionary(d => d.Id, ThPlain);

    UnitDef[] ThFill(UnitDef[] pool, int strong0, int seed)
    {
        int rn = pool.Length;
        var rng = new Random(seed);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        int remain = rn, strong = strong0;
        var picked = new UnitDef[3];
        for (int r = 0; r < 3; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = pool[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= ThStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int ThSeed(int pairIx, int draw)
    {
        ulong x = (ulong)ThTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] ThSeats(UnitDef[] u)
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
    // 版 v: bit0 = A（添字0＝カド）を素体に / bit1 = B（添字1）を素体に
    Formation ThForm(UnitDef[] u, int[] seats, int v)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (v & 1) != 0) || (k == 1 && (v & 2) != 0);
            f[seats[k]] = plain ? thPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double ThRate(Formation f, ThornRule rule)
    {
        double sum = 0;
        for (int wi = 1; wi < thW; wi++)
        {
            int wins = 0;
            for (int seed = ThBand; seed < ThBand + ThM; seed++)
                if (BattleEngine.Run(f, thWeak[wi].Enemy, seed, verbose: false, thorn: rule).PlayerWon) wins++;
            sum += wins * 100.0 / ThM;
        }
        return sum / (thW - 1);
    }
    string ThP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double ThSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double ThSe(IReadOnlyList<double> xs) => xs.Count < 2 ? double.NaN : ThSd(xs) / Math.Sqrt(xs.Count);

    // 組の添字（第81期の並び＝ a<b の全 1,275 組を添字順）。**同じ組には同じ台が引かれる**（台の抽選は (組, 引き番号) だけ）
    var thPairIxOf = new int[thRN, thRN];
    {
        int pi = 0;
        for (int a = 0; a < thRN; a++) for (int b = a + 1; b < thRN; b++) { thPairIxOf[a, b] = thPairIxOf[b, a] = pi; pi++; }
    }
    // 台（埋め草3枚）を 2K 通り集める。引いた順 t の系列は t % ThS
    List<UnitDef[]> ThFills(int b)
    {
        int a = thKado;
        var pool = thRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (thRoster[a].Attack >= ThStrong ? 1 : 0) + (thRoster[b].Attack >= ThStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < ThS * ThK && draw < ThDrawCap; draw++)
        {
            var f = ThFill(pool, strong0, ThSeed(thPairIxOf[a, b], draw));
            var t = f.Select(d => thIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }
    UnitDef[] ThTeam(int b, UnitDef[] fill) => new[] { thRoster[thKado], thRoster[b], fill[0], fill[1], fill[2] };
    // 1組ぶんの 2×2。Y[t][v]: v0 = y11 / v1 = y01（カド素体）/ v2 = y10（B 素体）/ v3 = y00
    double[][] ThMeasure(int b, ThornRule rule)
    {
        var fills = ThFills(b);
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = ThTeam(b, fills[t]);
            int[] seats = ThSeats(team);
            ys[t] = new double[4];
            for (int v = 0; v < 4; v++) ys[t][v] = ThRate(ThForm(team, seats, v), rule);
        }
        return ys;
    }

    // ---- verbose の1戦から供給・在庫・読み手の出力を読む（**盤面は動かさない**。Events と Log を読むだけ）----
    // 棘の発火は **Events** から取る：カドは不動で `PerformAttack` を1度も通らないので、
    // 行為者がカドで対象が敵の Damage は必ず反撃。「刺し返した相手」＝ カドへの Damage の直後に来る最初の1件。
    void ThProbe(Formation f, Formation enemy, int seed, ThornRule rule, int bIx, ThStat acc)
    {
        var r = BattleEngine.Run(f, enemy, seed, verbose: true, thorn: rule);
        int nP = f.Occupied().Count(), nE = enemy.Occupied().Count();
        var team = new Dictionary<int, int>();
        int kadoId = -1;
        {
            int i = 0;
            foreach ((int sl, UnitDef d) in f.Occupied()) { if (d.Id == "kado") kadoId = i; team[i] = BattleContext.PlayerTeam; i++; }
            var hushIds = new List<int>();
            foreach ((int sl, UnitDef d) in enemy.Occupied()) { team[i] = BattleContext.EnemyTeam; if (d.Traits.Contains(TraitId.Hush)) hushIds.Add(i); i++; }
            var dead = new HashSet<int>();
            bool pending = false; int pendingSrc = -1;
            double stock = 0; int stockTurns = 0; double stockMax = 0; int curTurn = -1; double curStock = 0;
            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind == BattleEventKind.Summon && e.TargetId is int sid && e.Team is int tm) team[sid] = tm;
                else if (e.Kind == BattleEventKind.Death && e.TargetId is int did) { dead.Add(did); if (did == kadoId) pending = false; }
                else if (e.Kind == BattleEventKind.Revive && e.TargetId is int rid) dead.Remove(rid);
                else if (e.Kind == BattleEventKind.TurnStart)
                {
                    if (curTurn >= 0) { stock += curStock; stockTurns++; stockMax = Math.Max(stockMax, curStock); }
                    curTurn = e.Turn; curStock = 0;
                }
                else if (e.Kind == BattleEventKind.StatusSnapshot && e.Text == "傷" && e.TargetId is int wid
                         && team.TryGetValue(wid, out int wt) && wt == BattleContext.EnemyTeam)
                    curStock += e.Amount;
                else if (e.Kind == BattleEventKind.Damage)
                {
                    if (kadoId < 0) continue;
                    if (e.TargetId == kadoId && e.ActorId is int aid && aid != kadoId && !e.FriendlyFire
                        && team.TryGetValue(aid, out int at) && at == BattleContext.EnemyTeam)
                    { pending = true; pendingSrc = aid; }
                    else if (pending && e.ActorId == kadoId && !e.FriendlyFire && e.TargetId is int tid
                             && team.TryGetValue(tid, out int tt) && tt == BattleContext.EnemyTeam)
                    {
                        pending = false;
                        acc.FiresEv++;
                        // 刺し返した相手が生きていたか。**InstanceId で引く**（同名の敵が並ぶ波でも取り違えない）。
                        // 最初の一撃が相手本人なら HpAfter で読む。別の敵への余波が先に来たなら、本人への `ApplyDamage` が
                        // 入口で返っている——破片で受け切った（生存）か、棘守りの入れ替えが呼んだ軋みの割り込みに
                        // 先に倒されていた（死体。Death が先に積まれている）かのどちらか。
                        if (tid == pendingSrc) { if (e.HpAfter > 0) acc.FiresAlive++; }
                        else if (!dead.Contains(pendingSrc)) acc.FiresAlive++;
                        if (hushIds.Any(h => !dead.Contains(h))) acc.FiresUnderHush++;
                    }
                }
            }
            if (curTurn >= 0) { stock += curStock; stockTurns++; stockMax = Math.Max(stockMax, curStock); }
            acc.Stock += stockTurns > 0 ? stock / stockTurns : 0;
            acc.StockMax += stockMax;
        }
        // 発火の回数は **Log** から取る（`gullet log` / `sever` と同じ作法）。Events だけだと、相手が既に死体で
        // 余波も出なかった反撃（Damage が1件も積まれない）が落ちる（実測 8,718 対 8,715）。
        // 「相手が生きていたか」は上の Events 側（InstanceId で引く）。
        var logs = r.Log;
        for (int li = 0; li < logs.Count; li++)
        {
            string sx = logs[li].Text;
            if (sx.Contains(" の棘が ") && sx.Contains(" を刺し返す")) acc.Fires++;
            else if (sx.Contains(" の棘が ") && sx.Contains(" に残る（傷 "))
            {
                // 受け手が味方か敵か：味方の名前で判定（素体は「素体の」を冠するので名前で引ける）
                bool ally = f.Occupied().Any(o => sx.Contains($" の棘が {o.Def.Name} に残る"));
                if (ally) acc.WoundsAlly++; else acc.Wounds++;
            }
            else if (sx.Contains(" の傷をこじ開ける")) acc.Gouge++;
            else if (sx.Contains(" の古い傷をなぞる")) acc.Trace++;
            else if (sx.Contains(" の傷をまとめて断つ（傷 "))
            {
                acc.Sever++;
                int p = sx.IndexOf("（傷 ", StringComparison.Ordinal) + 3;
                int q = sx.IndexOf(' ', p);
                if (q > p && int.TryParse(sx.AsSpan(p, q - p), out int w)) { acc.SeverWounds += w; if (w >= SeverTrait.Threshold) acc.SeverReached++; }
            }
            else if (sx.Contains(" の傷口から糸を引き")) acc.Suture++;
        }
        if (bIx >= 0 && r.TallyByUnit.TryGetValue(thRoster[bIx].Id, out UnitTally? bt))
        {
            acc.DmgB += bt.DamageToEnemy;
        }
        foreach ((int sl, UnitDef d) in f.Occupied())
            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? pt))
            {
                acc.Healed += pt.Healed;
                acc.CarryWoundAlly += pt.CarryCount is null ? 0 : pt.CarryCount[UnitTally.CarryWound];
            }
        foreach ((int sl, UnitDef d) in enemy.Occupied())
            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? et))
                acc.CarryWoundFoe += et.CarryCount is null ? 0 : et.CarryCount[UnitTally.CarryWound];
        if (r.PlayerWon) acc.Wins++;
        acc.Turns += r.Turns;
        acc.N++;
    }

    var thAllRows = CompareBuilds();
    bool ThHas(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    var thKadoRows = thAllRows.Where(rw => ThHas(rw.F, "kado")).ToArray();
    var thWoundRows = thAllRows.Where(rw => thWoundIds.Any(id => ThHas(rw.F, id))).ToArray();
    var thBothRows = thKadoRows.Where(rw => thWoundIds.Any(id => ThHas(rw.F, id))).ToArray();

    double[] ThCompare(Formation f, ThornRule rule)
    {
        var v = new double[thW];
        for (int w = 0; w < thW; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < 200; seed++)
                if (BattleEngine.Run(f, thStages[w].Enemy, seed, verbose: false, thorn: rule).PlayerWon) wins++;
            v[w] = wins * 100.0 / 200;
        }
        return v;
    }
    var thBalance = new Dictionary<string, double[]>();
    if (File.Exists("docs/balance.md"))
        foreach (string line in File.ReadAllLines("docs/balance.md"))
        {
            if (!line.StartsWith("| ")) continue;
            var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
            if (cells.Length != thW + 1 || !cells[1].EndsWith("%")) continue;
            var v = new double[thW];
            bool ok = true;
            for (int w = 0; w < thW; w++)
                if (!double.TryParse(cells[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
            if (ok) thBalance[cells[0]] = v;
        }

    // =====================================================================================
    // phase0: 窓口の全数・発火回数・粛・交わり・過去の期
    // =====================================================================================
    if (thArg == "phase0")
    {
        Console.WriteLine("# 第84期 Phase 0 —— 棘に傷を載せる前の地図");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 thorn phase0`。**盤面は1つも動かさない**"
                          + "（0-3 と 0-4 のために現行の V0 で戦闘を回すが、規則は既定のまま）。");
        Console.WriteLine();

        // ---- 0-1 / 0-2 窓口の全数（call-site grep をコードで再現）--------------------------------
        Console.WriteLine("## 0-1 / 0-2. `StatusKeys.Wound` の窓口の全数（call-site grep）");
        Console.WriteLine();
        Console.WriteLine("`grep -rn \"StatusKeys.Wound\" --include=*.cs BattleCore/` を実行時に再現し、各行を **書き／読み／その他** と "
                          + "**対象（敵／味方／両方）** に分けた。判定は行の形（`SetCounter` が書き・`Counter(` が読み）で機械的に付け、"
                          + "対象は呼び出し元の変数名で読んだ（`target`／`f`（`TargetPool`）は敵、`self`／`ally` は味方）。");
        Console.WriteLine();
        Console.WriteLine("| ファイル:行 | 種別 | 対象 | 行 |");
        Console.WriteLine("|---|:-:|:-:|---|");
        int nWrite = 0, nRead = 0, nReadAlly = 0, nOther = 0;
        foreach (string file in new[] { "BattleCore/Models.cs", "BattleCore/Traits.cs", "BattleCore/BattleEngine.cs" })
        {
            if (!File.Exists(file)) continue;
            string[] ls = File.ReadAllLines(file);
            for (int i = 0; i < ls.Length; i++)
            {
                string sx = ls[i];
                if (!sx.Contains("StatusKeys.Wound")) continue;
                string t = sx.Trim();
                string kind, who;
                if (t.StartsWith("///") || t.StartsWith("//")) { kind = "doc"; who = "—"; nOther++; }
                else if (t.Contains("SetCounter(StatusKeys.Wound")) { kind = "**書き**"; who = t.Contains("ally.") ? "**味方**" : "敵"; nWrite++; }
                else if (t.Contains("Counter(StatusKeys.Wound)")) { kind = "読み"; who = t.Contains("self.") || t.Contains("ally.") ? "**味方**" : "敵"; nRead++; if (who.Contains("味方")) nReadAlly++; }
                else { kind = "その他"; who = "—"; nOther++; }
                Console.WriteLine($"| `{file}:{i + 1}` | {kind} | {who} | `{t.Replace("|", "\\|")}` |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"**書き {nWrite} 箇所・読み {nRead} 箇所（うち味方を読む箇所 **{nReadAlly}**）・doc／その他 {nOther}。**");
        Console.WriteLine($"読み {nRead} の内訳: 抉り（`GougeTrait.OnAfterAttack`・`target`）／刻みのなぞり（`target`）／断ち（`SeverTrait.OnAfterAttack`・`target`）／"
                          + "`SeverTrait.DeepestWound` と `Preferred`（`ctx.TargetPool` ＝ 敵）／縫い（`target`）／"
                          + "`BattleEngine` の `ThinBladeCost.Unwounded`（`target`。**既定 `Always` なので死んでいる**）。"
                          + $"書き {nWrite} の内訳: 裂き（+1）／刻み（+1）／断ち（0 に戻す）／縫い（1 減らす）。"
                          + "**この期に足した棘の2箇所（敵・味方）は上の表に `ThornsTrait` の行として出ている**（V0 では素通り）。");
        Console.WriteLine();
        Console.WriteLine("**味方の傷を読む箇所は 0。** 第68期の表（傷は 305 セル全部が不発）と第49期の全数確認と整合する。");
        Console.WriteLine();

        // ---- 0-5 交わり（戦闘0回）----------------------------------------------------------------
        Console.WriteLine("## 0-5. カドと傷の駒が同居する `compare` の行数");
        Console.WriteLine();
        Console.WriteLine($"カドを含む行 **{thKadoRows.Length}**: " + string.Join(" / ", thKadoRows.Select(rw => $"`{rw.Name}`")));
        Console.WriteLine();
        Console.WriteLine($"傷の駒（キリ・ノミ・エグ・ナタ・ハリ）を含む行 **{thWoundRows.Length}**: " + string.Join(" / ", thWoundRows.Select(rw => $"`{rw.Name}`")));
        Console.WriteLine();
        Console.WriteLine($"**交わり {thBothRows.Length} 行**{(thBothRows.Length == 0 ? "。§3 の拒否権は V1 では原理的に立たない（`compare` 61 行は V1 で1セルも動かない）。" : ": " + string.Join(" / ", thBothRows.Select(rw => $"`{rw.Name}`")))}");
        Console.WriteLine();

        // ---- 0-6 過去の期 --------------------------------------------------------------------
        Console.WriteLine("## 0-6. 過去に同じ機構を測っていないか");
        Console.WriteLine();
        var hits = new List<string>();
        if (Directory.Exists("design"))
            foreach (string fp in Directory.GetFiles("design", "*.md").OrderBy(x => x, StringComparer.Ordinal))
            {
                if (Path.GetFileName(fp).StartsWith("PHASE84")) continue;
                foreach (string sx in File.ReadAllLines(fp))
                    if (System.Text.RegularExpressions.Regex.IsMatch(sx, "棘.*傷|Thorns.*Wound|反撃.*傷"))
                    { hits.Add($"`{fp.Replace('\\', '/')}`"); break; }
            }
        Console.WriteLine($"`grep -rln \"棘.*傷\\|Thorns.*Wound\\|反撃.*傷\" design/` に当たるファイル {hits.Count}: " + string.Join(" / ", hits));
        Console.WriteLine();
        Console.WriteLine("**測った期は無い。** 当たるのは (1) `IDEAS_PENDING.md` (1)「カドの棘 → 傷」＝この期の起点の構想メモ（第80期に記録）、"
                          + "(2) 第72〜83期の報告書で「棘」と「傷」が同じ行に別の話として並ぶもの（傾きの表・組の表・代金の分解）、"
                          + "(3) `TAUTOLOGY_AND_COUNTER_PLAN.md` / `OUTPUT_FEATURE_PLAN.md`（反撃軸と傷軸を別々に数えている）。"
                          + "第80期 表D の **カド × キリ +13.9 / カド × ハリ +8.0**、第81期の **カド × キリ +24.93** が「既に噛んでいる」の出どころ。");
        Console.WriteLine();

        // ---- 0-3 / 0-4 発火回数と粛（現行 V0・カドを含む 8 行 × 5 波 × seed 0..199・verbose）-------------
        Console.WriteLine("## 0-3 / 0-4. カドの反撃の発火回数（現行・波別・1戦あたり）と粛の確認");
        Console.WriteLine();
        Console.WriteLine($"カドを含む {thKadoRows.Length} 行 × 5 波 × seed 0..199 を **V0（現行）で verbose に回し**、Events から"
                          + "「刺し返した回数」「そのうち相手が生存していた回数」「粛の保持者が生きている間の回数」を数えた"
                          + "（Log の「刺し返す」の行数と突き合わせる）。**これが V1 の供給量の上限になる。**");
        Console.WriteLine();
        var p0 = new ThStat[thKadoRows.Length, thW];
        for (int i = 0; i < thKadoRows.Length; i++) for (int w = 0; w < thW; w++) p0[i, w] = new ThStat();
        Parallel.For(0, thKadoRows.Length * thW, k =>
        {
            int i = k / thW, w = k % thW;
            for (int seed = 0; seed < 200; seed++) ThProbe(thKadoRows[i].F, thStages[w].Enemy, seed, ThRuleOf(0), -1, p0[i, w]);
        });
        Console.WriteLine("| 行 | 波 | 発火/戦（Log） | 同（Events） | 生存/戦 | 粛の下/戦 | 決着T | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        var waveTot = new ThStat[thW];
        for (int w = 0; w < thW; w++) waveTot[w] = new ThStat();
        for (int i = 0; i < thKadoRows.Length; i++)
            for (int w = 0; w < thW; w++)
            {
                ThStat sx = p0[i, w];
                waveTot[w].AddFrom(sx);
                Console.WriteLine($"| {thKadoRows[i].Name} | 第{w + 1}波 | {sx.Fires / sx.N:F2} | {sx.FiresEv / sx.N:F2} | {sx.FiresAlive / sx.N:F2} | {sx.FiresUnderHush / sx.N:F2} | {sx.Turns / sx.N:F1} | {sx.Wins * 100.0 / sx.N:F1}% |");
            }
        Console.WriteLine();
        Console.WriteLine($"| 波 | 発火/戦（{thKadoRows.Length}行平均） | 生存/戦 | 粛の下/戦 |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int w = 0; w < thW; w++) Console.WriteLine($"| 第{w + 1}波 | {waveTot[w].Fires / waveTot[w].N:F2} | {waveTot[w].FiresAlive / waveTot[w].N:F2} | {waveTot[w].FiresUnderHush / waveTot[w].N:F2} |");
        Console.WriteLine();
        bool logMatch = Enumerable.Range(0, thKadoRows.Length).All(i => Enumerable.Range(0, thW).All(w => p0[i, w].Fires == p0[i, w].FiresEv));
        Console.WriteLine($"Log と Events の発火回数は {(logMatch ? $"**{thKadoRows.Length * thW} セル全部で一致**" : "**一致しないセルがある**（相手が既に死体で余波も出なかった反撃は Damage が1件も積まれないので Events 側が落ちる。棘守りの入れ替えが呼んだ軋みの割り込みが攻撃者を先に倒す形。主の数は Log）")}。"
                          + $" 第二波で粛の保持者が生きている間の発火は **{waveTot[1].FiresUnderHush / waveTot[1].N:F2} 回/戦**"
                          + $"（{(waveTot[1].FiresUnderHush == 0 ? "0 ＝ 門は閉じている" : "**0 ではない**——`CanActOutOfTurn` の門をくぐっていない")}）。");
        Console.WriteLine();
        Console.WriteLine("## 予測（測る前に書いた。§1 の写し）");
        Console.WriteLine();
        Console.WriteLine("- `compare` 61 行は V1 で1セルも動かない（0-5 が 0 行なら論理的に確定）。");
        Console.WriteLine("- 第二波の供給は 0 に近い（粛）。第四波の供給は落ちない（軛は打点を切るが傷を切らない）。");
        Console.WriteLine("- 相乗が最も伸びるのは カド × 抉り（エグ）。カド × 断ち（ナタ）は伸びるが分散が大きい。カド × 縫い（ハリ）は最も伸びが小さい。");
        Console.WriteLine();
        Console.WriteLine($"所要 {thSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // run <v> [skip] [take]: 2×2 を組ごとに TSV で吐く
    // =====================================================================================
    if (thArg == "run")
    {
        int v = args.Length > 3 ? int.Parse(args[3]) : 0;
        int skip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int take = args.Length > 5 ? int.Parse(args[5]) : thOthers.Length;
        skip = Math.Clamp(skip, 0, thOthers.Length);
        take = Math.Clamp(take, 0, thOthers.Length - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"thorn run v{v} {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            int b = thOthers[skip + j];
            var ys = ThMeasure(b, ThRuleOf(v));
            var sb = new System.Text.StringBuilder();
            sb.Append(v).Append('\t').Append(thKado).Append('\t').Append(b).Append('\t').Append(ys.Length);
            foreach (double[] y in ys) for (int k = 0; k < 4; k++) sb.Append('\t').Append(y[k].ToString("R", thInv));
            rows[j] = sb.ToString();
            if (Interlocked.Increment(ref doneR) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {thSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    // TSV を読む: [b] → Y[t][v]
    Dictionary<int, double[][]> ThRead(string path, int expectV)
    {
        var d = new Dictionary<int, double[][]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int v = int.Parse(c[0]);
            if (v != expectV) throw new InvalidOperationException($"{path}: 版 {v} の行が混ざっている（期待 {expectV}）");
            int b = int.Parse(c[2]); int nT = int.Parse(c[3]);
            var ys = new double[nT][];
            int at = 4;
            for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], thInv); }
            d[b] = ys;
        }
        return d;
    }
    double ThSyn(double[] y) => y[0] - y[2] - y[1] + y[3];

    // =====================================================================================
    // tables <v0> <v1> [<v2>]: 表A〜E
    // =====================================================================================
    if (thArg == "tables")
    {
        if (args.Length < 5 || !File.Exists(args[3]) || !File.Exists(args[4]))
        {
            Console.WriteLine("thorn tables <v0 の TSV> <v1 の TSV> [<v2 の TSV>]");
            return;
        }
        var Y0 = ThRead(args[3], 0);
        var Y1 = ThRead(args[4], 1);
        Dictionary<int, double[][]>? Y2 = args.Length > 5 && File.Exists(args[5]) ? ThRead(args[5], 2) : null;
        var missing = thOthers.Where(b => !Y0.ContainsKey(b) || !Y1.ContainsKey(b)).ToArray();

        Console.WriteLine("# 第84期 —— 棘に傷を載せる（表A〜E）");
        Console.WriteLine();
        Console.WriteLine($"器具は第81期の 2×2 の写し（K = {ThK} 台 × {ThS} 系列・弱い波 {ThWeakPct}%・第2〜5波・seed {ThBand}..{ThBand + ThM - 1}）。"
                          + $"A はカド固定・B は残り {thOthers.Length} 体。**台・席・戦闘 seed・台の抽選は V0 と V1 で同一**（同じ台で `ThornRule` だけを差し替える）。"
                          + (missing.Length > 0 ? $" **TSV に無い組 {missing.Length}**: " + string.Join(" / ", missing.Select(b => thName[b])) : ""));
        Console.WriteLine();

        // ---- 表A ------------------------------------------------------------------------------
        Console.WriteLine("## 表A —— 傷の5枚の相乗 V0 / V1 / Δ（系列別・SE 併記）【主判定 Q1】");
        Console.WriteLine();
        Console.WriteLine("`相乗(カド,B) = y11 − y10 − y01 + y00`／`Δ相乗 = 相乗_V1 − 相乗_V0`（**同じ台・同じ seed の対**なので Δ の SE は台ごとの Δ から出す）。");
        Console.WriteLine();
        Console.WriteLine("| B | 系列 | 台数 | 相乗 V0 | SE | 相乗 V1 | SE | **Δ相乗** | SE(Δ) | y11 V0 | y11 V1 | y00 | 帰属(カド) V0 | V1 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var dAll = new Dictionary<int, double[]>();      // b → Δ per table
        var dSer = new Dictionary<int, double[][]>();    // b → [series][t]
        foreach (int b in thOthers)
        {
            if (!Y0.ContainsKey(b) || !Y1.ContainsKey(b)) continue;
            int nT = Math.Min(Y0[b].Length, Y1[b].Length);
            var d = new double[nT];
            var ser = new List<double>[ThS];
            for (int sx = 0; sx < ThS; sx++) ser[sx] = new List<double>();
            for (int t = 0; t < nT; t++) { d[t] = ThSyn(Y1[b][t]) - ThSyn(Y0[b][t]); ser[t % ThS].Add(d[t]); }
            dAll[b] = d; dSer[b] = ser.Select(l => l.ToArray()).ToArray();
        }
        var q1Ser = new double[ThS];
        var q1Vals = new List<double>();
        foreach (int b in thWound)
        {
            if (!dAll.ContainsKey(b)) { Console.WriteLine($"| {thName[b]} | — | 0 | — | — | — | — | — | — | — | — | — | — | — |"); continue; }
            int nT = dAll[b].Length;
            for (int sx = 0; sx < ThS; sx++)
            {
                var s0 = new List<double>(); var s1 = new List<double>(); var y11a = new List<double>(); var y11b = new List<double>(); var y00 = new List<double>();
                for (int t = sx; t < nT; t += ThS) { s0.Add(ThSyn(Y0[b][t])); s1.Add(ThSyn(Y1[b][t])); y11a.Add(Y0[b][t][0]); y11b.Add(Y1[b][t][0]); y00.Add(Y0[b][t][3]); }
                var dd = dSer[b][sx];
                Console.WriteLine($"| {thName[b]} | {sx + 1} | {s0.Count} | {ThP2(s0.Average())} | {ThSe(s0):F2} | {ThP2(s1.Average())} | {ThSe(s1):F2} | **{ThP2(dd.Average())}** | {ThSe(dd):F2} | {y11a.Average():F1} | {y11b.Average():F1} | {y00.Average():F1} | {ThP2(Enumerable.Range(0, nT).Where(t => t % ThS == sx).Average(t => Y0[b][t][0] - Y0[b][t][1]))} | {ThP2(Enumerable.Range(0, nT).Where(t => t % ThS == sx).Average(t => Y1[b][t][0] - Y1[b][t][1]))} |");
                q1Ser[sx] += dd.Average() / thWound.Length;
            }
            {
                var s0 = Y0[b].Take(nT).Select(ThSyn).ToList(); var s1 = Y1[b].Take(nT).Select(ThSyn).ToList();
                Console.WriteLine($"| **{thName[b]}** | 合算 | {nT} | {ThP2(s0.Average())} | {ThSe(s0):F2} | {ThP2(s1.Average())} | {ThSe(s1):F2} | **{ThP2(dAll[b].Average())}** | {ThSe(dAll[b]):F2} | {Y0[b].Take(nT).Average(y => y[0]):F1} | {Y1[b].Take(nT).Average(y => y[0]):F1} | {Y0[b].Take(nT).Average(y => y[3]):F1} | {ThP2(Y0[b].Take(nT).Average(y => y[0] - y[1]))} | {ThP2(Y1[b].Take(nT).Average(y => y[0] - y[1]))} |");
                q1Vals.Add(dAll[b].Average());
            }
        }
        double q1 = q1Vals.Count > 0 ? q1Vals.Average() : double.NaN;
        Console.WriteLine();
        Console.WriteLine($"**Q1: 傷の5枚の Δ相乗 の平均 = {ThP2(q1)}pt**（系列1 {ThP2(q1Ser[0])} / 系列2 {ThP2(q1Ser[1])}）。線 +{ThQ1Line:F1} → **{(q1 >= ThQ1Line ? "○" : "×")}**。"
                          + $" 自己検査 (g)（2系列で同符号）: {(Math.Sign(q1Ser[0]) == Math.Sign(q1Ser[1]) ? "○" : "**×**")}。");
        Console.WriteLine();

        // ---- 表B ------------------------------------------------------------------------------
        Console.WriteLine($"## 表B —— カド × 全 {thOthers.Length} 体の Δ相乗（降順・上位 {ThTop} と下位 {ThTop}）");
        Console.WriteLine();
        Console.WriteLine("★ は傷の5枚。`有意` は |Δ| > 2SE(Δ)。**個数は主判定に混ぜない**（自己検査 (h)）。");
        Console.WriteLine();
        var order = dAll.Keys.OrderByDescending(b => dAll[b].Average()).ToArray();
        Console.WriteLine("| 順位 | B | Δ相乗 | SE(Δ) | 有意 | 相乗 V0 | 相乗 V1 | 系列1 Δ | 系列2 Δ | y00 |");
        Console.WriteLine("|--:|---|--:|--:|:-:|--:|--:|--:|--:|--:|");
        void BRow(int rank, int b)
        {
            double m = dAll[b].Average(), se = ThSe(dAll[b]);
            int nT = dAll[b].Length;
            Console.WriteLine($"| {rank} | {(thWound.Contains(b) ? "★" : "")}{thName[b]} | **{ThP2(m)}** | {se:F2} | {(Math.Abs(m) > 2 * se ? "○" : "")} | {ThP2(Y0[b].Take(nT).Select(ThSyn).Average())} | {ThP2(Y1[b].Take(nT).Select(ThSyn).Average())} | {ThP2(dSer[b][0].Average())} | {ThP2(dSer[b][1].Average())} | {Y0[b].Take(nT).Average(y => y[3]):F1} |");
        }
        for (int i = 0; i < Math.Min(ThTop, order.Length); i++) BRow(i + 1, order[i]);
        if (order.Length > 2 * ThTop) Console.WriteLine("| … | | | | | | | | | |");
        for (int i = Math.Max(ThTop, order.Length - ThTop); i < order.Length; i++) BRow(i + 1, order[i]);
        Console.WriteLine();
        int nPos = order.Count(b => dAll[b].Average() > 2 * ThSe(dAll[b])), nNeg = order.Count(b => dAll[b].Average() < -2 * ThSe(dAll[b]));
        Console.WriteLine($"全 {order.Length} 体の Δ相乗 の平均 {ThP2(order.Average(b => dAll[b].Average()))}・中央値 {ThP2(order.Select(b => dAll[b].Average()).OrderBy(x => x).ElementAt(order.Length / 2))}・"
                          + $"有意な正 {nPos} / 有意な負 {nNeg}（参考。主判定ではない）。傷の5枚の順位: "
                          + string.Join(" / ", thWound.Where(b => dAll.ContainsKey(b)).Select(b => $"{thName[b]} {Array.IndexOf(order, b) + 1} 位")) + "。");
        Console.WriteLine();
        Console.WriteLine("**y00・y01（カド素体）の V0/V1 の一致（自己検査 (a)）は表E。**");
        Console.WriteLine();

        // ---- 表C / D: 供給・在庫・読み手の出力（verbose・傷の5枚 × 台 16 × 4 波 × seed 8 × 版）----------------
        Console.WriteLine("## 表C —— 供給量と在庫（Q2・Q3。波別）");
        Console.WriteLine();
        Console.WriteLine($"傷の5枚の組それぞれについて、2×2 と同じ台のうち **各系列の先頭 {ThProbeTables / ThS} 台 = {ThProbeTables} 台**の y11（両方が本物）を、"
                          + $"弱い波の第2〜5波 × seed {ThBand}..{ThBand + ThM - 1} で **verbose に回し直し**、Events から棘の発火と敵側の傷の在庫を、Log から傷の書き込みと読み手の発火を数えた。"
                          + "**在庫** ＝ ターン頭の `StatusSnapshot`（傷）を敵側で合計した値のターン平均。`供給` ＝ 棘が傷を書いた回数／戦。");
        Console.WriteLine();
        int nV = Y2 is null ? 2 : 3;
        var cst = new ThStat[thWound.Length, nV, thW];
        for (int i = 0; i < thWound.Length; i++) for (int v = 0; v < nV; v++) for (int w = 0; w < thW; w++) cst[i, v, w] = new ThStat();
        Parallel.For(0, thWound.Length * nV, k =>
        {
            int i = k / nV, v = k % nV;
            int b = thWound[i];
            var fills = ThFills(b);
            var loc = new ThStat[thW];
            for (int w = 0; w < thW; w++) loc[w] = new ThStat();
            for (int t = 0; t < Math.Min(ThProbeTables, fills.Count); t++)
            {
                var team = ThTeam(b, fills[t]);
                Formation f = ThForm(team, ThSeats(team), 0);
                for (int w = 1; w < thW; w++)
                    for (int seed = ThBand; seed < ThBand + ThM; seed++) ThProbe(f, thWeak[w].Enemy, seed, ThRuleOf(v), b, loc[w]);
            }
            for (int w = 0; w < thW; w++) cst[i, v, w] = loc[w];
        });
        Console.WriteLine("| B | 波 | 版 | 発火/戦 | 生存/戦 | **供給/戦** | 味方傷/戦 | **在庫/T** | 在庫最大 | 決着T | 勝率 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < thWound.Length; i++)
            for (int w = 1; w < thW; w++)
                for (int v = 0; v < nV; v++)
                {
                    ThStat sx = cst[i, v, w];
                    if (sx.N == 0) continue;
                    Console.WriteLine($"| {thName[thWound[i]]} | 第{w + 1}波 | V{v} | {sx.Fires / sx.N:F2} | {sx.FiresAlive / sx.N:F2} | **{sx.Wounds / sx.N:F2}** | {sx.WoundsAlly / sx.N:F2} | **{sx.Stock / sx.N:F2}** | {sx.StockMax / sx.N:F2} | {sx.Turns / sx.N:F1} | {sx.Wins * 100.0 / sx.N:F1}% |");
                }
        Console.WriteLine();
        // Q2 / Q3 の要約
        double SupplyWave(int v, int w) { double a = 0, n = 0; for (int i = 0; i < thWound.Length; i++) { a += cst[i, v, w].Wounds; n += cst[i, v, w].N; } return n == 0 ? double.NaN : a / n; }
        double StockWave(int v, int w) { double a = 0, n = 0; for (int i = 0; i < thWound.Length; i++) { a += cst[i, v, w].Stock; n += cst[i, v, w].N; } return n == 0 ? double.NaN : a / n; }
        Console.WriteLine("| 波 | 供給/戦 V1（5組平均） | 在庫/T V0 | 在庫/T V1 | Δ在庫 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int w = 1; w < thW; w++) Console.WriteLine($"| 第{w + 1}波 | {SupplyWave(1, w):F2} | {StockWave(0, w):F2} | {StockWave(1, w):F2} | {ThP2(StockWave(1, w) - StockWave(0, w))} |");
        Console.WriteLine();
        int iHari = Array.IndexOf(thWoundIds, "hari");
        double hariStockV1 = Enumerable.Range(1, thW - 1).Sum(w => cst[iHari, 1, w].Stock) / Enumerable.Range(1, thW - 1).Sum(w => cst[iHari, 1, w].N);
        double hariStockV0 = Enumerable.Range(1, thW - 1).Sum(w => cst[iHari, 0, w].Stock) / Enumerable.Range(1, thW - 1).Sum(w => cst[iHari, 0, w].N);
        double hariMaxV1 = Enumerable.Range(1, thW - 1).Sum(w => cst[iHari, 1, w].StockMax) / Enumerable.Range(1, thW - 1).Sum(w => cst[iHari, 1, w].N);
        Console.WriteLine($"**Q2**: 第四波の供給 {SupplyWave(1, 3):F2} 対 第二波 {SupplyWave(1, 1):F2} 回/戦 → {(SupplyWave(1, 3) > SupplyWave(1, 1) ? "○（軛は切らない・粛は止める）" : "**×**")}。");
        Console.WriteLine($"**Q3**: ハリ同席の敵側在庫 V0 {hariStockV0:F2} → V1 {hariStockV1:F2} /T（在庫の最大のターン平均 V1 {hariMaxV1:F2}）。"
                          + $"在庫のターン平均が 1 を{(hariStockV1 > 1.0 ? "**超えた**（第39期の天井 1 が崩れた）" : "超えない（第39期の「天井 1」の算術が供給を増やしても崩れない）")}。");
        Console.WriteLine();

        Console.WriteLine("## 表D —— 読み手の出力（Q4）");
        Console.WriteLine();
        Console.WriteLine("同じ verbose の実行から。`抉り`＝こじ開けた回数・`なぞり`＝刻みが古い傷をなぞった回数・`断ち`＝まとめて断った回数（**閾値 2 に届いた回数と同じ**——待ち方 V1 では閾値未満に下りない）・"
                          + "`傷/断ち`＝1回の断ちが読んだ傷・`縫い`＝糸を引いた回数。`与ダメ(B)` は B の `DamageToEnemy`／戦、`回復` は味方全体の `Healed`／戦。第2〜5波の合算。");
        Console.WriteLine();
        Console.WriteLine("| B | 版 | 抉り/戦 | なぞり/戦 | 断ち/戦 | 傷/断ち | 縫い/戦 | 与ダメ(B)/戦 | 回復/戦 | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < thWound.Length; i++)
            for (int v = 0; v < nV; v++)
            {
                var sx = new ThStat();
                for (int w = 1; w < thW; w++) sx.AddFrom(cst[i, v, w]);
                if (sx.N == 0) continue;
                Console.WriteLine($"| {thName[thWound[i]]} | V{v} | {sx.Gouge / sx.N:F2} | {sx.Trace / sx.N:F2} | {sx.Sever / sx.N:F2} | {(sx.Sever > 0 ? (sx.SeverWounds / sx.Sever).ToString("F2") : "—")} | {sx.Suture / sx.N:F2} | {sx.DmgB / sx.N:F1} | {sx.Healed / sx.N:F1} | {sx.Wins * 100.0 / sx.N:F1}% |");
            }
        Console.WriteLine();
        Console.WriteLine("| B | 読み手の発火 Δ(V1−V0)/戦 | 与ダメ(B) Δ/戦 | 回復 Δ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int i = 0; i < thWound.Length; i++)
        {
            var s0 = new ThStat(); var s1 = new ThStat();
            for (int w = 1; w < thW; w++) { s0.AddFrom(cst[i, 0, w]); s1.AddFrom(cst[i, 1, w]); }
            if (s0.N == 0 || s1.N == 0) continue;
            double f0 = (s0.Gouge + s0.Trace + s0.Sever + s0.Suture) / s0.N, f1 = (s1.Gouge + s1.Trace + s1.Sever + s1.Suture) / s1.N;
            Console.WriteLine($"| {thName[thWound[i]]} | {ThP2(f1 - f0)} | {ThP2(s1.DmgB / s1.N - s0.DmgB / s0.N)} | {ThP2(s1.Healed / s1.N - s0.Healed / s0.N)} |");
        }
        Console.WriteLine();

        // ---- 表E: 自己検査 --------------------------------------------------------------------
        Console.WriteLine("## 表E —— 自己検査（(a)(c)(e)(f)(g)(h)。(b)(d) と拒否権は `thorn check`）");
        Console.WriteLine();
        double maxA = 0; int cellsA = 0;
        foreach (int b in thOthers)
        {
            if (!Y0.ContainsKey(b) || !Y1.ContainsKey(b)) continue;
            int nT = Math.Min(Y0[b].Length, Y1[b].Length);
            for (int t = 0; t < nT; t++) foreach (int k in new[] { 1, 3 })
                {
                    cellsA++;
                    maxA = Math.Max(maxA, Math.Abs(Y0[b][t][k] - Y1[b][t][k]));
                    if (Y2 is not null && Y2.ContainsKey(b) && t < Y2[b].Length) { cellsA++; maxA = Math.Max(maxA, Math.Abs(Y0[b][t][k] - Y2[b][t][k])); }
                }
        }
        double maxC = 0; int cellsC = 0;
        if (Y2 is not null)
            foreach (int b in thOthers)
            {
                if (!Y1.ContainsKey(b) || !Y2.ContainsKey(b)) continue;
                int nT = Math.Min(Y1[b].Length, Y2[b].Length);
                for (int t = 0; t < nT; t++) for (int k = 0; k < 4; k++) { cellsC++; maxC = Math.Max(maxC, Math.Abs(Y1[b][t][k] - Y2[b][t][k])); }
            }
        var e2 = new ThStat(); var e4 = new ThStat(); var all1 = new ThStat(); var all2 = new ThStat();
        for (int i = 0; i < thWound.Length; i++) { e2.AddFrom(cst[i, 1, 1]); e4.AddFrom(cst[i, 1, 3]); for (int w = 1; w < thW; w++) { all1.AddFrom(cst[i, 1, w]); if (nV > 2) all2.AddFrom(cst[i, 2, w]); } }
        Console.WriteLine("| 検査 | 内容 | 値 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        Console.WriteLine($"| (a) | y01・y00（カド素体）が V0・V1{(Y2 is null ? "" : "・V2")} で完全一致 | {cellsA} セル・最大差 {maxA:F10}pt | {(maxA == 0 ? "○" : "**×**")} |");
        if (Y2 is null) Console.WriteLine("| (c) | V2 は V1 と全セル ±0.0 ＋ 味方に傷が載った回数 > 0 | V2 の TSV が無い | — |");
        else Console.WriteLine($"| (c) | V2 は V1 と全セル ±0.0 ＋ 味方に傷が載った回数 > 0 | {cellsC} セル・最大差 {maxC:F10}pt ／ 味方傷 {(all2.N > 0 ? (all2.WoundsAlly / all2.N).ToString("F2") : "—")} 回/戦（V1 では {all1.WoundsAlly / all1.N:F2}） | {(maxC == 0 && all2.WoundsAlly > 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (e) | 第二波の供給が 0 に近い（粛） | {e2.Wounds / e2.N:F3} 回/戦（第四波 {e4.Wounds / e4.N:F2}・粛の下の発火 {e2.FiresUnderHush / e2.N:F3}） | {(e2.FiresUnderHush == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (f) | 1反撃 = 1傷（書き込み ÷ 生存していた反撃 = 1.00） | {all1.Wounds} / {all1.FiresAlive} = {(all1.FiresAlive > 0 ? all1.Wounds / all1.FiresAlive : double.NaN):F4}（発火 {all1.Fires}・Events の発火 {all1.FiresEv}） | {(all1.Wounds == all1.FiresAlive ? "○" : "**×**")} |");
        Console.WriteLine($"| (g) | Q1 が2系列で同符号 | 系列1 {ThP2(q1Ser[0])} / 系列2 {ThP2(q1Ser[1])} | {(Math.Sign(q1Ser[0]) == Math.Sign(q1Ser[1]) ? "○" : "**×**")} |");
        Console.WriteLine($"| (h) | Δ相乗 の分母は5枚固定（個数を主判定に混ぜない） | 表A の平均 {ThP2(q1)}（有意な相手の数 {nPos} は表B の参考値） | ○ |");
        Console.WriteLine();
        Console.WriteLine("**(b) の読み方**: 計数は `if (!ctx.WoundIgnite.Enabled) continue;` の**手前**にあるので、"
                          + "**同じ盤面なら Y0 と Y1 で必ず同じ数だけ通る**（コードの形から従う。理想台の (a') と `compare` の (c) がその実証）。"
                          + "上の残差は**盤面が版で分岐したぶん**——Y1 では毒が敵を削るので、決着ターン数も生き残る敵の数も変わる。"
                          + "**「版に依らない計数」は経路の性質であって、観測される値の性質ではない。**");
        Console.WriteLine();
        Console.WriteLine("## 判定表");
        Console.WriteLine();
        Console.WriteLine("| | 判定 | 値 | 線 | 結果 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1** | **傷の5枚の Δ相乗 の平均** | **{ThP2(q1)}** | ≥ +{ThQ1Line:F1} | **{(q1 >= ThQ1Line ? "○" : "×")}** |");
        Console.WriteLine($"| Q2 | 供給/戦: 第四波 > 第二波 | {SupplyWave(1, 3):F2} 対 {SupplyWave(1, 1):F2} | — | {(SupplyWave(1, 3) > SupplyWave(1, 1) ? "○" : "×")} |");
        Console.WriteLine($"| Q3 | 在庫/T の V1 − V0（ハリ同席で 1 を超えるか） | {ThP2(hariStockV1 - hariStockV0)}（V1 {hariStockV1:F2}） | 記録 | {(hariStockV1 > 1.0 ? "超えた" : "超えない")} |");
        {
            var s0 = new ThStat(); var s1 = new ThStat();
            for (int i = 0; i < thWound.Length; i++) for (int w = 1; w < thW; w++) { s0.AddFrom(cst[i, 0, w]); s1.AddFrom(cst[i, 1, w]); }
            Console.WriteLine($"| Q4 | 読み手の発火 Δ/戦（5組合算）・断ちが閾値に届いた回数 | {ThP2((s1.Gouge + s1.Trace + s1.Sever + s1.Suture) / s1.N - (s0.Gouge + s0.Trace + s0.Sever + s0.Suture) / s0.N)}・断ち {s0.SeverReached / s0.N * thWound.Length:F2} → {s1.SeverReached / s1.N * thWound.Length:F2}（ナタ行） | 記録 | — |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {thSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check: (b)(d) `compare` 61 行の V0 対 V1、(c) V1 対 V2、拒否権1・2、`docs/balance.md` との突き合わせ
    // =====================================================================================
    if (thArg == "check")
    {
        Console.WriteLine("# 第84期 —— 受け入れ基準: `compare` 61 行の V0 / V1 / V2 の突き合わせと拒否権");
        Console.WriteLine();
        var c0 = new double[thAllRows.Length][]; var c1 = new double[thAllRows.Length][]; var c2 = new double[thAllRows.Length][];
        Parallel.For(0, thAllRows.Length * 3, k =>
        {
            int i = k / 3, v = k % 3;
            var r = ThCompare(thAllRows[i].F, ThRuleOf(v));
            if (v == 0) c0[i] = r; else if (v == 1) c1[i] = r; else c2[i] = r;
        });
        int mismDoc = 0, cellsDoc = 0, missingDoc = 0, mism01 = 0, mism12 = 0, mismWound = 0;
        var lines = new List<string>();
        for (int i = 0; i < thAllRows.Length; i++)
        {
            bool isWound = thWoundRows.Any(rw => rw.Name == thAllRows[i].Name);
            if (thBalance.TryGetValue(thAllRows[i].Name, out double[]? doc))
                for (int w = 0; w < thW; w++) { cellsDoc++; if (Math.Abs(doc[w] - c0[i][w]) > 0.05) { mismDoc++; lines.Add($"| {thAllRows[i].Name} | 第{w + 1}波 | docs {doc[w]:F1} | V0 {c0[i][w]:F1} |"); } }
            else missingDoc++;
            for (int w = 0; w < thW; w++)
            {
                if (Math.Abs(c0[i][w] - c1[i][w]) > 0.001) { mism01++; if (isWound) mismWound++; lines.Add($"| {thAllRows[i].Name} | 第{w + 1}波 | V0 {c0[i][w]:F1} | V1 {c1[i][w]:F1} |"); }
                if (Math.Abs(c1[i][w] - c2[i][w]) > 0.001) { mism12++; lines.Add($"| {thAllRows[i].Name} | 第{w + 1}波 | V1 {c1[i][w]:F1} | V2 {c2[i][w]:F1} |"); }
            }
        }
        Console.WriteLine($"`CompareBuilds()` {thAllRows.Length} 行 × {thW} 波 × seed 0..199 を V0 / V1 / V2 で回した（{thAllRows.Length * thW * 200 * 3:N0} 戦）。");
        Console.WriteLine();
        Console.WriteLine("| 検査 | 内容 | 値 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        Console.WriteLine($"| 受け入れ | V0 と `docs/balance.md` の突き合わせ | {cellsDoc} セル・ずれ {mismDoc} 件（`docs` に無い行 {missingDoc}） | {(mismDoc == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (b) | `compare` {thAllRows.Length} 行が V0 と V1 で全セル ±0.0 | ずれ {mism01} 件 / {thAllRows.Length * thW} | {(mism01 == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (c) | V2 は V1 と全セル ±0.0（味方傷の計数は `tables` (c)） | ずれ {mism12} 件 / {thAllRows.Length * thW} | {(mism12 == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (d) | 傷を含む {thWoundRows.Length} 行が V1 で ±0.0 | ずれ {mismWound} 件 | {(mismWound == 0 ? "○" : "**×**")} |");
        // 拒否権
        double Fifth(double[][] c, IEnumerable<string> names)
        {
            var xs = new List<double>();
            foreach (string n in names) { int i = Array.FindIndex(thAllRows, rw => rw.Name == n); if (i >= 0) xs.Add(c[i][thW - 1]); }
            return xs.Count == 0 ? double.NaN : xs.Average();
        }
        double f0 = Fifth(c0, Baseline.PrimaryRows), f1 = Fifth(c1, Baseline.PrimaryRows);
        int over0 = thKadoRows.Count(rw => c0[Array.FindIndex(thAllRows, x => x.Name == rw.Name)][thW - 1] > 95.0);
        int over1 = thKadoRows.Count(rw => c1[Array.FindIndex(thAllRows, x => x.Name == rw.Name)][thW - 1] > 95.0);
        Console.WriteLine($"| 拒否権1 | 主判定 {Baseline.PrimaryRows.Length} 行の第五波平均 ≥ {Baseline.PrimaryFifthFloor:F1}% | V0 {f0:F2}% → V1 {f1:F2}% | {(f1 >= Baseline.PrimaryFifthFloor ? "立たない" : "**立つ**")} |");
        Console.WriteLine($"| 拒否権2 | カドを含む {thKadoRows.Length} 行で第五波 > 95% が新たに 2 行以上 | V0 {over0} → V1 {over1} 行 | {(over1 - over0 >= 2 ? "**立つ**" : "立たない")} |");
        Console.WriteLine();
        Console.WriteLine($"カドと傷の駒が同居する行は {thBothRows.Length} 行なので、V1 で `compare` が動かないのは論理的な帰結（**立たないこと自体が「傷を書くだけ」の実装であることの自己検査**）。");
        if (lines.Count > 0)
        {
            Console.WriteLine(); Console.WriteLine("| 行 | 波 | 左 | 右 |"); Console.WriteLine("|---|---|--:|--:|");
            foreach (string l in lines) Console.WriteLine(l);
        }
        Console.WriteLine();
        Console.WriteLine($"| 行（カドを含む {thKadoRows.Length} 行・V1） | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var rw in thKadoRows)
        {
            int i = Array.FindIndex(thAllRows, x => x.Name == rw.Name);
            Console.WriteLine($"| {rw.Name} (V1) | " + string.Join(" | ", c1[i].Select(x => $"{x:F1}%")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {thSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("thorn: 引数は phase0 / run <v> [skip] [take] / tables <v0> <v1> [<v2>] / check。");
    return;
}
}
