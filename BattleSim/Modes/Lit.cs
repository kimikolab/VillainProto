using BattleCore;
using static Common;

// =====================================================================================
// lit モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "lit")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 lit
// =====================================================================================

static class LitDiag
{
// ==================================================================================
// 第113期 —— 灯の対象選択（`lit`）。**灯の対象は「最も遅い味方」のまま。濾しだけを足す。**
//
// 起点は第110期の結論——**穴は譲渡の側になかった**（`トモ×ハギ` は段1 が 0.13〜0.90 回/戦しか
// 来ない）。**「最も遅い味方」を選ぶ機構は、遅い層に集まる「手番を使わない駒」に構造的に当たる。**
// 濾しを足して、灯が無駄になる3つの形（(A) 自分の手番に振らない ／ (B) 手番を攻撃に使わない ／
// (C) 休み番がある）のうち (A)(B) だけを飛ばす版（W1）と、(C) まで飛ばす版（W2）を測る。
//
//     dotnet run --project BattleSim -c Release 0 lit phase0   # §1（(G14) の波及・(P-5)〜(P-7)・**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 lit run      # 主判定61行 × 3版 ＋ 副判定4台 ＋ 素体
//     dotnet run --project BattleSim -c Release 0 lit seat     # §3-3（採用版が決まってから）
//     dotnet run --project BattleSim -c Release 0 lit check    # §6 の自己検査
//
// **既存の診断（`tomo` / `tomo yield` を含む）は1文字も書き換えていない。**
// **`Presets.Compare` / `Presets.Cross` も1行も触っていない**（第111期に閉じたばかり）。
public static void Run(string[] args, int stageIndex)
{
    string ltMode = args.Length > 2 ? args[2] : "phase0";
    var ltCompare = CompareBuilds();
    var ltCross = CrossBuilds();
    IReadOnlyList<EnemyCatalog.Stage> ltStages = EnemyCatalog.Stages;
    const int LtSeeds = 200;        // **帯A**。`compare` と揃える（規約 (G14)）
    const int LtScan = 50;          // 粗探索。`reseat` / `layout` と揃える
    const int LtCfBase = 200;       // 追試の帯（選定に使っていない seed）
    const int LtCfSeeds = 400;
    const double LtSeatLine = 5.0;  // 第46期の採否閾値
    const string LtRowName = "灯×薙ぎ (トモ×ドルガ)";
    var ltPrim = new HashSet<string>(Baseline.PrimaryRows);

    // ------------------------------------------------------------------------------
    // 版。**濾しだけを振る。譲渡条件は V1（窓・第110期に採用）で3版とも固定**（指示書 §0-4）。
    // **`TaillightRule.Default` に頼らない**——第110期に既定が V0 → V1 へ動いたとき、
    // 頼っていた列が黙って化けた前例がある。
    // ------------------------------------------------------------------------------
    var ltVers = new (string Tag, LitFilter F)[]
    {
        ("W0 旧",     LitFilter.SupportOnly),   // 第108〜113期の既定（第114期に W2 へ移した。対照として残置）
        ("W1 手番型", LitFilter.ActingOnly),
        ("W2 現行",   LitFilter.ActingNow),     // **第114期に採った既定**
    };
    const int LtW0 = 0, LtW1 = 1, LtW2 = 2, LtPlainVer = 3;
    static TaillightRule LtRule(LitFilter f) => new(YieldMode.OwnTurnWindow, f);

    // 素体（同数値・特性なし・`Actions` なし）。第47期の作法。カタログには載せない。
    static UnitDef LtPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    UnitDef ltTomoPlain = LtPlain(UnitCatalog.Tomo);

    // --- 情報セル: `0 < x < 100` を**第2〜5波**で数える（第59期 9-1 の定義・規約 (G10)）
    static int LtInfo(double[] c)
    {
        int n = 0;
        for (int i = 1; i < c.Length; i++) if (c[i] > 0.0 && c[i] < 100.0) n++;
        return n;
    }
    static double LtAvg25(double[] c) => c.Skip(1).Average();
    static string LtN(UnitDef? d) => d?.Name ?? "-";
    static string LtSeats(Formation f) => LtN(f[0]) + "/" + LtN(f[1]) + " | " + LtN(f[2]) + " | " + LtN(f[3]) + "/" + LtN(f[4]);

    // リポジトリ直下（`audit` と同じ walk-up）。
    static string? LtRoot()
    {
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Path.GetDirectoryName(root);
        return root;
    }

    // `docs/balance.md` の現行値（**戦闘不要**）。行名 -> 5波のセル。
    static Dictionary<string, double[]> LtBalance(int waves)
    {
        var map = new Dictionary<string, double[]>(StringComparer.Ordinal);
        string? root = LtRoot();
        if (root == null) return map;
        foreach (string line in File.ReadAllLines(Path.Combine(root, "docs", "balance.md")))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            var parts = line.Split('|', StringSplitOptions.None).Select(p => p.Trim()).ToArray();
            if (parts.Length < 8) continue;
            var v = new List<double>();
            for (int i = 2; i < parts.Length - 1; i++)
                if (parts[i].EndsWith("%") && double.TryParse(parts[i].TrimEnd('%'), out double d)) v.Add(d);
            if (v.Count == waves) map[parts[1]] = v.ToArray();
        }
        return map;
    }

    // `docs/reseat.md` を読む（**戦闘不要**）。節（`## 編成名`）ごとに候補の席を返す。
    // 列は 粗順 | 狙 | 前1/前3 | 中央 | 後1/後3 | 平均 | 第1波..第5波 で、
    // **測り直しの帯は seed 0..199 ＝ 帯A**（ファイル冒頭の注記。規約 (G14) が要求する帯）。
    static Dictionary<string, List<(string Rank, bool Intent, string Seats, double[] Cells, bool Current)>>
        LtReseat(int waves)
    {
        var map = new Dictionary<string, List<(string, bool, string, double[], bool)>>(StringComparer.Ordinal);
        string? root = LtRoot();
        if (root == null) return map;
        string path = Path.Combine(root, "docs", "reseat.md");
        if (!File.Exists(path)) return map;
        string cur = "";
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.StartsWith("## "))
            {
                cur = line.Substring(3).Trim();
                if (!map.ContainsKey(cur)) map[cur] = new List<(string, bool, string, double[], bool)>();
                continue;
            }
            if (cur.Length == 0 || !line.StartsWith("| ") || !line.Contains('%')) continue;
            var parts = line.Split('|', StringSplitOptions.None).Select(p => p.Trim()).ToArray();
            if (parts.Length < 7 + waves) continue;
            var v = new List<double>();
            for (int i = 2; i < parts.Length - 1; i++)
                if (parts[i].EndsWith("%") && double.TryParse(parts[i].TrimEnd('%'), out double d)) v.Add(d);
            // 平均 ＋ 5波 の 6 つが取れる行だけを候補として拾う。
            if (v.Count != waves + 1) continue;
            map[cur].Add((parts[1], parts[2] == "○", parts[3] + " | " + parts[4] + " | " + parts[5],
                          v.Skip(1).ToArray(), parts[1].Contains('★')));
        }
        return map;
    }

    // 灯の対象を**駒の定義からだけ**予測する（(P-6)。戦闘は1回も回さない）。
    // 実装（`TaillightTrait.OnTurnStart`）と同じ順で同じ条件を当てる。
    static UnitDef? LtPredict(Formation f, LitFilter filter, bool oddTurn)
    {
        UnitDef? pick = null; int pickSlot = 0;
        foreach ((int slot, UnitDef d) in f.Occupied())
        {
            if (d.Traits.Contains(TraitId.Taillight)) continue;                 // 自分は対象外
            if (d.Traits.Any(t => TraitCatalog.Get(t).BlocksSupport)) continue; // AcceptsSupport
            if (filter != LitFilter.SupportOnly && !TaillightTrait.AttacksInOwnTurn(d)) continue;
            // W2 の (C)。**盤面を読まない予測**なので、のろまだけを偶数ターンで落とす
            // （痺れ・まどろみはこの表では扱わない——実測の側に出る）。
            if (filter == LitFilter.ActingNow && !oddTurn && d.Traits.Contains(TraitId.Sluggish)) continue;
            if (pick is null || d.Speed < pick.Speed || (d.Speed == pick.Speed && slot < pickSlot))
            { pick = d; pickSlot = slot; }
        }
        return pick;
    }

    // ------------------------------------------------------------------------------
    // 副判定の4台（第110期 §2-2 で選ばれた土台 = **ガルド / ソラ / ボルグ**）。
    // **`Presets` は1行も触らない。台は診断のローカル**（`gradient` / `aim` と同じ扱い）。
    // partner = 前1 のとき、`トモ×ドルガ` は `Presets.Compare` の `灯×薙ぎ` と**同じ5枚**。
    // **席は第111〜113期のもの（後1 トモ / 後3 ボルグ）で凍結してある**——第114期に `Presets` 側の席だけを
    // 後1 ボルグ / 後3 トモ へ差し替えたので、**この台と主判定行はもう同じ席ではない。**
    // 凍結するのは、第113期に取った3版の実測（門・帰属・対象の分布）がそのまま再現できるようにするため
    // ——**台を動かすと「濾しの効き」と「席の効き」が混ざる。**
    // ------------------------------------------------------------------------------
    var ltPartners = new (string Tag, UnitDef Def)[]
    {
        ("トモ×ドルガ", UnitCatalog.Dolga),
        ("トモ×ムド",   UnitCatalog.Mudo),
        ("トモ×ソム",   UnitCatalog.Som),
        ("トモ×ハギ",   UnitCatalog.Hagi),
    };
    Formation LtBench(UnitDef partner, UnitDef tomo) => Formation.Build(
        front1: partner, front3: UnitCatalog.Gald, center: UnitCatalog.Sora,
        back1: tomo, back3: UnitCatalog.Borg);

    // 1台 × 1波ぶんの観測。**盤面には一切影響しない**（`BattleResult` の計数を読み直すだけ）。
    LtStat LtMeasure(Formation f, LitFilter filter, EnemyCatalog.Stage stage, int seedFrom, int seeds)
    {
        var st = new LtStat();
        for (int seed = seedFrom; seed < seedFrom + seeds; seed++)
        {
            BattleResult r = BattleEngine.Run(f, stage.Enemy, seed, verbose: false, taillight: LtRule(filter));
            st.N++; if (r.PlayerWon) st.Wins++;
            st.Turns += r.Turns;
            UnitTally t = r.TallyByUnit.TryGetValue(UnitCatalog.Tomo.Id, out UnitTally? tt) ? tt : new UnitTally();
            st.Fires += t.TaillightFires; st.Switches += t.TaillightSwitches;
            st.Yields += t.TaillightYields; st.Stalls += t.TaillightYieldStalls;
            st.Doused += t.TaillightDoused; st.Lumen += t.TaillightLumen;
            st.Peak += t.TaillightPeak; st.Idle += t.TaillightIdle;
            st.NoDeath += t.TaillightNoDeath; st.NoTarget += t.TaillightNoTarget; st.Saw2 += t.TaillightSaw2;
            // 門2（条件成立）は段の和で閉じる（V0/V1 は自分の手番の側）。
            st.Gate2 += t.TaillightYields + t.TaillightNoTarget + t.TaillightBlockedHop + t.TaillightNoFoe;
            st.StallStun += t.TaillightStallStun; st.StallSlumber += t.TaillightStallSlumber;
            st.StallCanAct += t.TaillightStallCanAct;
            foreach ((int _, UnitDef d) in f.Occupied())
            {
                if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? mt)) continue;
                st.TeamDmg += mt.DamageToEnemy;
                // **譲渡の与ダメは味方全員から集める**（第110期 2-1）——`TaillightYieldDamage` は
                // `ApplyDamage` が**殴った側**の帳簿に積むので、トモの側は常に 0 になる。
                st.YieldDmg += mt.TaillightYieldDamage;
                if (mt.TaillightLitReceived > 0) LtStat.Bump(st.LitBy, d.Name, mt.TaillightLitReceived);
                if (mt.TaillightSkipStatic > 0)
                { st.SkipStatic += mt.TaillightSkipStatic; LtStat.Bump(st.SkipStaticBy, d.Name, mt.TaillightSkipStatic); }
                if (mt.TaillightSkipNow > 0)
                { st.SkipNow += mt.TaillightSkipNow; LtStat.Bump(st.SkipNowBy, d.Name, mt.TaillightSkipNow); }
            }
        }
        return st;
    }

    // ==============================================================================
    // phase0 —— 指示書 §1。**戦闘は1回も回さない。**
    //   (P-1)〜(P-4) (G14) の波及調査 ／ (P-5) 濾しに掛かる駒の全数 ／
    //   (P-6) 灯の対象の予測 ／ (P-7) 候補 0 人になる行があるか
    // ==============================================================================
    if (ltMode == "phase0")
    {
        Console.WriteLine("# 第113期 Phase 0 —— 灯の対象選択");
        Console.WriteLine();
        Console.WriteLine("**戦闘は1回も回していない。** 表P は `docs/balance.md` と `docs/reseat.md` から、"
            + "(P-5)〜(P-7) は実装（`UnitCatalog` / `TraitCatalog` / `TaillightTrait.AttacksInOwnTurn`）から derive した。");
        Console.WriteLine();
        Console.WriteLine("**帯の明示（規約 (G14)）**: `docs/balance.md` も `docs/reseat.md` の測り直しも "
            + $"**seed 0..{LtSeeds - 1} ＝ 帯A**。情報セルはこの帯で数える。");
        Console.WriteLine();

        var bal = LtBalance(ltStages.Count);
        var seat = LtReseat(ltStages.Count);
        Console.WriteLine($"`docs/balance.md` から読めた行 **{ltCompare.Count(b => bal.ContainsKey(b.Name))} / {ltCompare.Length}**"
            + $"、`docs/reseat.md` から読めた節 **{ltCompare.Count(b => seat.ContainsKey(b.Name))} / {ltCompare.Length}**。");

        // ---- (P-1)(P-2) 情報セルの分布と、1 以下の行
        var info = new List<(string Name, int Info, double[] Cells, bool Prim)>();
        foreach (var b in ltCompare)
            if (bal.TryGetValue(b.Name, out double[]? v))
                info.Add((b.Name, LtInfo(v), v, ltPrim.Contains(b.Name)));

        Console.WriteLine();
        Console.WriteLine("## 表P-1 —— 61 行の情報セル（帯A・第2〜5波）の分布");
        Console.WriteLine();
        Console.WriteLine("| 情報セル | 行数 |");
        Console.WriteLine("|--:|--:|");
        for (int k = 0; k <= ltStages.Count - 1; k++)
            Console.WriteLine($"| {k} | {info.Count(x => x.Info == k)} |");
        Console.WriteLine();
        Console.WriteLine($"合計 {info.Count} 行・情報セルの総和 **{info.Sum(x => x.Info)}**"
            + $"（平均 {info.Average(x => (double)x.Info):F2}）。");

        var thin = info.Where(x => x.Info <= 1).OrderBy(x => x.Info).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
        Console.WriteLine();
        Console.WriteLine($"## 表P-2 —— 情報セルが 1 以下の行（**{thin.Count} 行**）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 主判定 | 情報セル |" + string.Concat(ltStages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|---|:-:|--:|" + string.Concat(ltStages.Select(_ => "---:|")));
        foreach (var e in thin)
            Console.WriteLine($"| {e.Name} | {(e.Prim ? "**P**" : "")} | **{e.Info}** |"
                + string.Concat(e.Cells.Select(c => $" {c:F1}% |")));

        // ---- (P-3)(P-4) 代替席があるか
        Console.WriteLine();
        Console.WriteLine("## 表P-3 —— 代替席（`docs/reseat.md`・帯A）");
        Console.WriteLine();
        Console.WriteLine($"線は**測る前に固定してある**（指示書 §1）: **現行席との差が {LtSeatLine:F1}pt 未満で、"
            + "情報セルが 2 以上**の席が `docs/reseat.md` の候補の中にあるか。");
        Console.WriteLine();
        Console.WriteLine("> **差は第2〜5波の平均で取る**（規約 (G10)）。`docs/reseat.md` の `平均` 列は"
            + "第一波を含む5波平均なので使わない——**第一波は全行 100.0% の教習波**で、含めると差が 1/5 に希釈される。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 現行席の情報セル | 候補数 | 情報セル 2 以上 | **線を満たす** | うち狙○ | 最大の情報セル増 | 最良の候補（差） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");

        int p4 = 0, p4MaxGain = 0, p4Intent = 0;
        var p3Detail = new List<string>();
        foreach (var e in thin)
        {
            if (!seat.TryGetValue(e.Name, out var cands) || cands.Count == 0)
            { Console.WriteLine($"| {e.Name} | {e.Info} | **節が無い** | — | — | — | — |"); continue; }
            var cur = cands.FirstOrDefault(c => c.Current);
            double curAvg = cur.Cells is null ? LtAvg25(e.Cells) : LtAvg25(cur.Cells);
            int curInfo = cur.Cells is null ? e.Info : LtInfo(cur.Cells);
            var rich = cands.Where(c => !c.Current && LtInfo(c.Cells) >= 2).ToList();
            var ok = rich.Where(c => Math.Abs(LtAvg25(c.Cells) - curAvg) < LtSeatLine).ToList();
            var best = ok.OrderByDescending(c => LtInfo(c.Cells)).ThenByDescending(c => LtAvg25(c.Cells)).ToList();
            var okIntent = ok.Where(c => c.Intent).ToList();
            if (best.Count > 0) { p4++; p4MaxGain = Math.Max(p4MaxGain, LtInfo(best[0].Cells) - curInfo); }
            if (okIntent.Count > 0 && okIntent.Max(c => LtInfo(c.Cells)) > curInfo) p4Intent++;
            Console.WriteLine($"| {e.Name} | {curInfo}{(cur.Cells is null ? "（★が無い）" : "")} | {cands.Count} | {rich.Count} "
                + $"| **{ok.Count}** | {okIntent.Count} | {(best.Count == 0 ? "—" : (LtInfo(best[0].Cells) - curInfo).ToString("+0;-0;0"))} "
                + $"| {(best.Count == 0 ? "—" : $"情報セル {LtInfo(best[0].Cells)} / {LtAvg25(best[0].Cells) - curAvg:+0.0;-0.0}pt")} |");
            foreach (var c in best.Take(3))
                p3Detail.Add($"- **{e.Name}** ← 粗順 {c.Rank}（{c.Seats}）: 情報セル {curInfo} → **{LtInfo(c.Cells)}**"
                    + $"、第2〜5波平均 {curAvg:F1}% → {LtAvg25(c.Cells):F1}%（{LtAvg25(c.Cells) - curAvg:+0.0;-0.0}pt）"
                    + $"、波別 {string.Join(" / ", c.Cells.Select(x => x.ToString("F1")))}");
        }
        if (p3Detail.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("### 線を満たした候補（各行 上位3）");
            Console.WriteLine();
            foreach (string line in p3Detail) Console.WriteLine(line);
        }

        Console.WriteLine();
        Console.WriteLine($"## (P-4) 判定 —— 代替席のある行 **{p4} 行**（最大の情報セル増 {p4MaxGain:+0;-0;0}）");
        Console.WriteLine();
        Console.WriteLine($"**線（測る前に固定）: 3 行以上ならこの期を「席の期」に切り替える。** 実測 {p4} 行 → "
            + (p4 >= 3 ? "**席の期へ切り替える**" : "**記録だけして §2（灯の対象選択）へ進む**"));
        Console.WriteLine();
        Console.WriteLine($"> **(P-3) の線には狙（ガルドが前列 / セッキが後列）が入っていない。** 狙を満たす候補だけで数えると"
            + $" **{p4Intent} 行**になる——指示書 §1 の (P-3) は席の条件を「差」と「情報セル」の2つだけで書いており、"
            + $"**採る側（§6 の「情報セルの多い側を採る」）が既存の作法として狙を要求する**ことと食い違う。"
            + $"{(p4 == p4Intent ? "この期では両者が一致した。" : "**この期は一致しなかった**（" + p4 + " 対 " + p4Intent + "）ので、"
                + "線は指示書どおり狙なしで判定し、**採る側では狙を当てた**。両方を報告書に書く。")}");
        Console.WriteLine("> **線を引く条件と、採る条件は同じ集合で書くこと**——第59期（条件節が分母の何割を選ぶか）・"
            + "第107期（別の節が線を到達不能にする）に続く、**条件集合の食い違い**の例。");

        // ---- (P-5) 濾しに掛かる駒の全数
        Console.WriteLine();
        Console.WriteLine("## 表P-5 —— 濾しに掛かる駒の全数（`UnitCatalog.All` " + UnitCatalog.All.Count + " 枚）");
        Console.WriteLine();
        Console.WriteLine("**手で数えていない。** (A) は `Trait.NeverAttacksOwnTurn`、(B) は `UnitDef.Actions` に "
            + "`ActionKind.Attack` が1つも無いこと、(C) は `TraitId.Sluggish` の保持、"
            + "支援拒否は `Trait.BlocksSupport` から引いた。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 速 | 攻 | (A) 恒久的に振らない | (B) 周期に攻撃が無い | (C) 休み番 | 支援拒否 | W0 | W1 | W2 |");
        Console.WriteLine("|---|--:|--:|---|---|:-:|:-:|:-:|:-:|:-:|");
        int nA = 0, nB = 0, nC = 0, nS = 0;
        foreach (UnitDef d in UnitCatalog.All.OrderBy(x => x.Speed).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            var aTraits = d.Traits.Where(t => TraitCatalog.Get(t).NeverAttacksOwnTurn).ToList();
            bool b = d.Actions is { Count: > 0 } acts && acts.All(a => a.Kind != ActionKind.Attack);
            bool c = d.Traits.Contains(TraitId.Sluggish);
            bool stoic = d.Traits.Any(t => TraitCatalog.Get(t).BlocksSupport);
            if (aTraits.Count == 0 && !b && !c && !stoic) continue;
            if (aTraits.Count > 0) nA++;
            if (b) nB++;
            if (c) nC++;
            if (stoic) nS++;
            bool w0 = !stoic, w1 = w0 && TaillightTrait.AttacksInOwnTurn(d), w2 = w1 && !c;
            Console.WriteLine($"| {d.Name} | {d.Speed} | {d.Attack} "
                + $"| {(aTraits.Count == 0 ? "" : string.Join(" / ", aTraits.Select(t => $"`{t}`")))} "
                + $"| {(b ? "`" + string.Join(",", d.Actions!.Select(a => a.Kind)) + "`" : "")} "
                + $"| {(c ? "○" : "")} | {(stoic ? "○" : "")} "
                + $"| {(w0 ? "候補" : "**外**")} | {(w1 ? "候補" : "**外**")} | {(w2 ? "候補" : "**外**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"(A) **{nA} 枚** ／ (B) **{nB} 枚**（トモ自身を含む。トモは元から自分を対象にしない）"
            + $" ／ (C) **{nC} 枚** ／ 支援拒否 **{nS} 枚**。");
        Console.WriteLine();
        Console.WriteLine($"**W1 が新たに落とすのは {UnitCatalog.All.Count(d => !d.Traits.Any(t => TraitCatalog.Get(t).BlocksSupport) && !TaillightTrait.AttacksInOwnTurn(d))} 枚**"
            + $"（支援拒否で既に落ちている駒を除く）／ **W2 はそれに加えて {nC} 枚**（そのターンだけ）。");

        // ---- (P-6) 灯の対象の予測
        Console.WriteLine();
        Console.WriteLine("## 表P-6 —— 灯の対象の予測（**駒の定義からだけ。戦闘 0 回**）");
        Console.WriteLine();
        Console.WriteLine("§3 の実測と突き合わせる。**予測を先に書く**（第109期の反省）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席（前1/前3 \\| 中央 \\| 後1/後3） | W0 | W1 | W2 奇数T | W2 偶数T |");
        Console.WriteLine("|---|---|---|---|---|---|");
        var p6 = new List<(string Tag, Formation F)>();
        var tomoRow = ltCompare.FirstOrDefault(b => b.Name == LtRowName);
        if (tomoRow.F is not null) p6.Add(("**主判定** " + LtRowName, tomoRow.F));
        foreach (var (tag, partner) in ltPartners) p6.Add(("副 " + tag, LtBench(partner, UnitCatalog.Tomo)));
        foreach (var (tag, f) in p6)
            Console.WriteLine($"| {tag} | {LtSeats(f)} "
                + $"| {LtN(LtPredict(f, LitFilter.SupportOnly, true))} | {LtN(LtPredict(f, LitFilter.ActingOnly, true))} "
                + $"| {LtN(LtPredict(f, LitFilter.ActingNow, true))} | {LtN(LtPredict(f, LitFilter.ActingNow, false))} |");

        // ---- (P-7) 候補 0 人になる行
        Console.WriteLine();
        Console.WriteLine("## (P-7) 候補が 0 人になる行");
        Console.WriteLine();
        var withTomo = ltCompare.Concat(ltCross).Where(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Tomo)))
                                .Select(b => (b.Name, b.F))
                                .Concat(ltPartners.Select(p => ("副 " + p.Tag, LtBench(p.Def, UnitCatalog.Tomo)))).ToList();
        int zero = 0;
        Console.WriteLine("| 台 | W0 候補数 | W1 候補数 | W2 候補数（奇/偶） |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var (name, f) in withTomo)
        {
            int c0 = 0, c1 = 0, c2o = 0, c2e = 0;
            foreach ((int _, UnitDef d) in f.Occupied())
            {
                if (d.Traits.Contains(TraitId.Taillight)) continue;
                if (d.Traits.Any(t => TraitCatalog.Get(t).BlocksSupport)) continue;
                c0++;
                if (!TaillightTrait.AttacksInOwnTurn(d)) continue;
                c1++; c2o++;
                if (!d.Traits.Contains(TraitId.Sluggish)) c2e++;
            }
            if (c1 == 0 || c2e == 0) zero++;
            Console.WriteLine($"| {name} | {c0} | {c1} | {c2o} / {c2e} |");
        }
        Console.WriteLine();
        Console.WriteLine($"候補が 0 人になりうる台 **{zero} 件 / {withTomo.Count}**。");
        Console.WriteLine();
        Console.WriteLine("**0 人のときの経路は3版とも同一**——`TaillightTrait.OnTurnStart` の `pick is null` の枝は"
            + "濾しの前後で1文字も変えていないので、`Douse` して `TaillightIdle` を立てる。"
            + "**`lit check` の (c) が、全員が濾しに掛かる台を組んで実測で確かめる。**");
        return;
    }

    // 版を渡して 5 波ぶんの勝率セルを取る（`compare` と同じ帯A）。
    double[] LtCells(Formation f, LitFilter filter, int seedFrom, int seeds)
        => ltStages.Select(st =>
        {
            int w = 0;
            for (int seed = seedFrom; seed < seedFrom + seeds; seed++)
                if (BattleEngine.Run(f, st.Enemy, seed, verbose: false, taillight: LtRule(filter)).PlayerWon) w++;
            return w * 100.0 / seeds;
        }).ToArray();

    // 台 × 版 × 波の観測（`run` と `check` が共有する）。Ver 3 は素体。
    LtStat[,,] LtBenchRun(int seedFrom, int seeds)
    {
        var cell = new LtStat[ltPartners.Length, ltVers.Length + 1, ltStages.Count];
        var jobs = new List<(int P, int V, int W)>();
        for (int p = 0; p < ltPartners.Length; p++)
            for (int v = 0; v <= ltVers.Length; v++)
                for (int w = 0; w < ltStages.Count; w++) jobs.Add((p, v, w));
        Parallel.ForEach(jobs, j =>
        {
            UnitDef tomo = j.V == LtPlainVer ? ltTomoPlain : UnitCatalog.Tomo;
            LitFilter filter = j.V == LtPlainVer ? LitFilter.SupportOnly : ltVers[j.V].F;
            cell[j.P, j.V, j.W] = LtMeasure(LtBench(ltPartners[j.P].Def, tomo), filter, ltStages[j.W], seedFrom, seeds);
        });
        return cell;
    }

    // 第2〜5波（規約 (G10)）でまとめる。
    LtStat LtSum(LtStat[,,] cell, int p, int v, bool waves25)
    {
        var acc = new LtStat();
        for (int w = waves25 ? 1 : 0; w < ltStages.Count; w++) acc.AddFrom(cell[p, v, w]);
        return acc;
    }

    static string LtDist(Dictionary<string, int> bag, int battles)
    {
        if (bag.Count == 0) return "（0）";
        int tot = bag.Values.Sum();
        return string.Join(" / ", bag.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {kv.Value * 100.0 / tot:F1}%（{(battles == 0 ? 0 : (double)kv.Value / battles):F2}/戦）"));
    }

    // ==============================================================================
    // run —— 指示書 §3。主判定61行 × 3版 ＋ 副判定4台 ＋ 素体。
    // ==============================================================================
    if (ltMode == "run")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第113期 —— 灯の対象選択（`lit run`）");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{LtSeeds - 1}（**帯A**）・5波。**判定の分母は第2〜5波**（規約 (G10)）。");
        Console.WriteLine("**3版とも譲渡条件は V1（窓）で固定**——動かすのは灯の濾しだけ。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 濾し |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| W0 旧 | `AcceptsSupport` のみ（第108〜113期の既定） |");
        Console.WriteLine("| W1 手番型 | ＋ 恒久的に手番で攻撃しない駒を飛ばす（(A) 不動・追い打ち ／ (B) 周期に攻撃が無い） |");
        Console.WriteLine("| W2 現行 | W1 ＋ そのターン `CanAct` が偽の駒を飛ばす（(C) のろまの休み番）。**第114期に採った既定** |");

        LtStat[,,] cell = LtBenchRun(0, LtSeeds);

        // ---- 表A-1: 門（第2〜5波・台 × 版）
        Console.WriteLine();
        Console.WriteLine("## 表A-1 —— 門（第2〜5波・回/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 決着T | 門1 灯 | **替** | 門2 条件成立 | 門3 譲渡 | **潰れ** | 痺 | 眠 | CanAct | **潰/譲** | 譲渡の与ダメ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int p = 0; p < ltPartners.Length; p++)
            for (int v = 0; v < ltVers.Length; v++)
            {
                LtStat ss = LtSum(cell, p, v, true);
                double yields = ss.Per(ss.Yields), stalls = ss.Per(ss.Stalls);
                Console.WriteLine($"| {ltPartners[p].Tag}{(p == 0 ? "（主判定行）" : "")} | {ltVers[v].Tag} | {ss.Win:F1}% "
                    + $"| {ss.Per(ss.Turns):F2} | {ss.Per(ss.Fires):F2} | **{ss.Per(ss.Switches):F2}** | {ss.Per(ss.Gate2):F2} "
                    + $"| {yields:F2} | **{stalls:F2}** | {ss.Per(ss.StallStun):F2} | {ss.Per(ss.StallSlumber):F2} "
                    + $"| {ss.Per(ss.StallCanAct):F2} | **{(yields <= 0 ? "—" : (stalls / yields).ToString("F3"))}** "
                    + $"| {ss.Per(ss.YieldDmg):F1} |");
            }

        // ---- 表A-2: 主判定行の波別
        Console.WriteLine();
        Console.WriteLine($"## 表A-2 —— 主判定行 `{LtRowName}` の波別");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 勝率 | 決着T | 門1 灯 | 替 | 門2 | 門3 譲渡 | 潰れ | 灯の総量 | 消灯 | 到達点 | 空振り |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int v = 0; v < ltVers.Length; v++)
            for (int w = 0; w < ltStages.Count; w++)
            {
                LtStat ss = cell[0, v, w];
                Console.WriteLine($"| {ltVers[v].Tag} | {w + 1} | {ss.Win:F1}% | {ss.Per(ss.Turns):F2} | {ss.Per(ss.Fires):F2} "
                    + $"| {ss.Per(ss.Switches):F2} | {ss.Per(ss.Gate2):F2} | {ss.Per(ss.Yields):F2} | {ss.Per(ss.Stalls):F2} "
                    + $"| {ss.Per(ss.Lumen):F1} | {ss.Per(ss.Doused):F1} | {ss.Per(ss.Peak):F1} | {ss.Per(ss.Idle):F2} |");
            }

        // ---- 表B: 帰属（Q2）
        Console.WriteLine();
        Console.WriteLine("## 表B —— 帰属（Q2・第2〜5波の勝率。版 − 素体）");
        Console.WriteLine();
        Console.WriteLine("`素体` = トモを **HP" + UnitCatalog.Tomo.MaxHp + "・攻" + UnitCatalog.Tomo.Attack
            + "・速" + UnitCatalog.Tomo.Speed + "・特性なし** に差し替えた版（第47期の作法）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 素体 | W0 | W1 | W2 | 帰属 W0 | 帰属 W1 | 帰属 W2 | W1 − W0 | W2 − W1 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var attr = new double[ltPartners.Length, ltVers.Length];
        for (int p = 0; p < ltPartners.Length; p++)
        {
            double baseWin = LtSum(cell, p, LtPlainVer, true).Win;
            var w = new double[ltVers.Length];
            for (int v = 0; v < ltVers.Length; v++) { w[v] = LtSum(cell, p, v, true).Win; attr[p, v] = w[v] - baseWin; }
            Console.WriteLine($"| {ltPartners[p].Tag} | {baseWin:F1}% | {w[0]:F1}% | {w[1]:F1}% | {w[2]:F1}% "
                + $"| {attr[p, 0]:+0.0;-0.0;0.0} | {attr[p, 1]:+0.0;-0.0;0.0} | {attr[p, 2]:+0.0;-0.0;0.0} "
                + $"| {w[1] - w[0]:+0.0;-0.0;0.0} | {w[2] - w[1]:+0.0;-0.0;0.0} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **素体が床（第2〜5波が 0% 近く）の台では、帰属の差は「測っていない」**（第61期）。"
            + "第110期 §2-2 の実測では ムド 0.6 / ソム 0.0 / ハギ 0.0 で、**帯（40〜95%）に入るのはドルガ台だけ**"
            + "——だから副判定は Q4（灯の対象）を見るために置いてあり、勝率の差では読まない。");

        // ---- 表C: 対象の分布（Q4・Q6）
        Console.WriteLine();
        Console.WriteLine("## 表C —— 灯の対象の分布（Q4・Q6・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 灯/戦 | 受け手の内訳（割合・回/戦） |");
        Console.WriteLine("|---|---|--:|---|");
        for (int p = 0; p < ltPartners.Length; p++)
            for (int v = 0; v < ltVers.Length; v++)
            {
                LtStat ss = LtSum(cell, p, v, true);
                Console.WriteLine($"| {ltPartners[p].Tag} | {ltVers[v].Tag} | {ss.Per(ss.Fires):F2} | {LtDist(ss.LitBy, ss.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("### 濾された駒（実行時に数えた集合。自己検査 (b) の左辺）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 静的な濾し（W1） | 動的な濾し（W2） |");
        Console.WriteLine("|---|---|---|---|");
        for (int p = 0; p < ltPartners.Length; p++)
            for (int v = 0; v < ltVers.Length; v++)
            {
                LtStat ss = LtSum(cell, p, v, true);
                Console.WriteLine($"| {ltPartners[p].Tag} | {ltVers[v].Tag} | {LtDist(ss.SkipStaticBy, ss.N)} | {LtDist(ss.SkipNowBy, ss.N)} |");
            }

        // ---- 表D: 拒否権（Q5）
        Console.WriteLine();
        Console.WriteLine("## 表D —— 拒否権（Q5・`compare` 61 行 × 3版）");
        Console.WriteLine();
        var grid = new double[ltVers.Length][][];
        for (int v = 0; v < ltVers.Length; v++) grid[v] = new double[ltCompare.Length][];
        Parallel.For(0, ltVers.Length * ltCompare.Length, idx =>
        {
            int v = idx / ltCompare.Length, b = idx % ltCompare.Length;
            grid[v][b] = LtCells(ltCompare[b].F, ltVers[v].F, 0, LtSeeds);
        });

        var bal2 = LtBalance(ltStages.Count);
        int moved = 0, movedRows = 0, balDiff = 0;
        var movedList = new List<string>();
        for (int b = 0; b < ltCompare.Length; b++)
        {
            bool hasTomo = ltCompare[b].F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Tomo));
            int d = 0;
            for (int v = 1; v < ltVers.Length; v++)
                for (int w = 0; w < ltStages.Count; w++)
                    if (Math.Abs(grid[v][b][w] - grid[0][b][w]) > 1e-9) d++;
            if (!hasTomo && d > 0) { moved += d; movedRows++; movedList.Add(ltCompare[b].Name); }
            if (bal2.TryGetValue(ltCompare[b].Name, out double[]? o))
                for (int w = 0; w < ltStages.Count; w++)
                    if (Math.Abs(grid[0][b][w] - o[w]) > 1e-9) balDiff++;
        }
        Console.WriteLine($"**トモを含まない {ltCompare.Length - 1} 行が動いたセル: {moved} 件**"
            + $"（{movedRows} 行{(movedList.Count == 0 ? "" : ": " + string.Join(" / ", movedList))}）"
            + $" —— 1 セルでも動けば実装の事故。");
        Console.WriteLine();
        Console.WriteLine($"**自己検査 (a)**: W0 の 305 セルと `docs/balance.md` の差 **{balDiff} 件**。");
        Console.WriteLine();
        Console.WriteLine($"### 主判定行 `{LtRowName}` のセル");
        Console.WriteLine();
        int rowIdx = Array.FindIndex(ltCompare, b => b.Name == LtRowName);
        Console.WriteLine("| 版 |" + string.Concat(ltStages.Select((_, i) => $" 第{i + 1}波 |")) + " 平均(2〜5波) | 情報セル |");
        Console.WriteLine("|---|" + string.Concat(ltStages.Select(_ => "---:|")) + "---:|--:|");
        if (rowIdx >= 0)
            for (int v = 0; v < ltVers.Length; v++)
                Console.WriteLine($"| {ltVers[v].Tag} |" + string.Concat(grid[v][rowIdx].Select(c => $" {c:F1}% |"))
                    + $" {LtAvg25(grid[v][rowIdx]):F1}% | {LtInfo(grid[v][rowIdx])} |");

        // 拒否権1（主判定19行の第五波平均）と (G9) の注意
        Console.WriteLine();
        Console.WriteLine("| 版 | 主判定19行の第五波平均 | 歯止め " + Baseline.PrimaryFifthFloor.ToString("F1")
            + " との余裕 | 全61行の第五波 | 第五波 95% 超が新たに出た行 |");
        Console.WriteLine("|---|--:|--:|--:|---|");
        for (int v = 0; v < ltVers.Length; v++)
        {
            double sp = 0, sa = 0; int np = 0;
            var ceil = new List<string>();
            for (int b = 0; b < ltCompare.Length; b++)
            {
                double v5 = grid[v][b][ltStages.Count - 1];
                sa += v5;
                if (ltPrim.Contains(ltCompare[b].Name)) { sp += v5; np++; }
                bool wasCeil = grid[0][b][ltStages.Count - 1] > 95.0;
                if (v5 > 95.0 && !wasCeil) ceil.Add($"{ltCompare[b].Name}（{v5:F1}%）");
            }
            Console.WriteLine($"| {ltVers[v].Tag} | {sp / Math.Max(1, np):F1}% "
                + $"| **{sp / Math.Max(1, np) - Baseline.PrimaryFifthFloor:+0.0;-0.0}pt** | {sa / ltCompare.Length:F1}% "
                + $"| {(ceil.Count == 0 ? "0 行" : string.Join(" / ", ceil))} |");
        }

        // ---- 表E: 紙と実測
        Console.WriteLine();
        Console.WriteLine("## 表E —— 紙（§3-5）と実測");
        Console.WriteLine();
        Console.WriteLine("分子 = 譲渡/戦 × 譲られた駒の1手番の出力 ＋ 灯/戦 × `Lumen` × 受け手が振る回数。");
        Console.WriteLine("**方向だけを書く**（上限にも下限にもならない・(G7)）。"
            + "**分母を削る機構ではない**——濾しは対象を替えるだけで、盤上の駒を1枚も減らさない。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 譲渡/戦 | 潰れ/戦 | **生きた譲渡** | 譲渡の与ダメ/戦 | 灯/戦 | 替/戦 | **灯の到達点** | 味方の総与ダメ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int p = 0; p < ltPartners.Length; p++)
            for (int v = 0; v < ltVers.Length; v++)
            {
                LtStat ss = LtSum(cell, p, v, true);
                Console.WriteLine($"| {ltPartners[p].Tag} | {ltVers[v].Tag} | {ss.Per(ss.Yields):F2} | {ss.Per(ss.Stalls):F2} "
                    + $"| **{ss.Per(ss.Yields - ss.Stalls):F2}** | {ss.Per(ss.YieldDmg):F1} | {ss.Per(ss.Fires):F2} "
                    + $"| {ss.Per(ss.Switches):F2} | **{ss.Per(ss.Peak):F1}** | {ss.Per(ss.TeamDmg):F0} |");
            }

        // ---- 判定
        Console.WriteLine();
        Console.WriteLine("## 判定");
        Console.WriteLine();
        LtStat m0 = LtSum(cell, 0, LtW0, true), m1 = LtSum(cell, 0, LtW1, true), m2 = LtSum(cell, 0, LtW2, true);
        double r0 = m0.Yields <= 0 ? double.NaN : m0.Stalls / m0.Yields;
        double r1 = m1.Yields <= 0 ? double.NaN : m1.Stalls / m1.Yields;
        double r2 = m2.Yields <= 0 ? double.NaN : m2.Stalls / m2.Yields;
        Console.WriteLine("| | 問い | 線 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|---|:-:|");
        Console.WriteLine($"| Q1 | 潰れ/譲渡 が下がるか（主判定行） | W0 の 1/2 以下 "
            + $"| W0 {r0:F3} → W1 **{r1:F3}** / W2 **{r2:F3}** "
            + $"| W1 {(r1 <= r0 / 2 ? "○" : "**×**")} / W2 {(r2 <= r0 / 2 ? "○" : "**×**")} |");
        Console.WriteLine($"| Q2 | 帰属が下がらないか（主判定行） | W0 ±0 以上 "
            + $"| W0 {attr[0, LtW0]:+0.0;-0.0} → W1 **{attr[0, LtW1]:+0.0;-0.0}** / W2 **{attr[0, LtW2]:+0.0;-0.0}** "
            + $"| W1 {(attr[0, LtW1] >= attr[0, LtW0] ? "○" : "**×**")} / W2 {(attr[0, LtW2] >= attr[0, LtW0] ? "○" : "**×**")} |");
        Console.WriteLine($"| Q3 | W2 の替が W1 より増えるか | 二値 "
            + $"| W1 {m1.Per(m1.Switches):F2} → W2 **{m2.Per(m2.Switches):F2}** 回/戦 "
            + $"| {(m2.Switches > m1.Switches ? "増" : "増えない")} |");
        LtStat h0 = LtSum(cell, 3, LtW0, true), h1 = LtSum(cell, 3, LtW1, true);
        double hagi0 = h0.LitBy.TryGetValue(UnitCatalog.Hagi.Name, out int x0) ? x0 * 100.0 / Math.Max(1, h0.LitBy.Values.Sum()) : 0;
        double hagi1 = h1.LitBy.TryGetValue(UnitCatalog.Hagi.Name, out int x1) ? x1 * 100.0 / Math.Max(1, h1.LitBy.Values.Sum()) : 0;
        Console.WriteLine($"| Q4 | ハギ台で灯の対象がハギ自身でなくなるか | W1 で 0% "
            + $"| W0 {hagi0:F1}% → W1 **{hagi1:F1}%** | {(hagi1 == 0.0 ? "○" : "**×**")} |");
        Console.WriteLine($"| Q5 | トモを含まない 60 行が ±0.0 | 1 セルでも動けば事故 | 動いたセル **{moved} 件** "
            + $"| {(moved == 0 ? "○" : "**×**")} |");
        int d1 = Enumerable.Range(0, ltPartners.Length).Select(p => LtSum(cell, p, LtW1, true).LitBy.Count).Max();
        int d2 = Enumerable.Range(0, ltPartners.Length).Select(p => LtSum(cell, p, LtW2, true).LitBy.Count).Max();
        int dAll1 = Enumerable.Range(0, ltPartners.Length).SelectMany(p => LtSum(cell, p, LtW1, true).LitBy.Keys).Distinct().Count();
        int dAll2 = Enumerable.Range(0, ltPartners.Length).SelectMany(p => LtSum(cell, p, LtW2, true).LitBy.Keys).Distinct().Count();
        Console.WriteLine($"| Q6 | 対象の分布が1枚に潰れないか | 2 枚以上に散る "
            + $"| W1 台内の最大 {d1} 枚 / 全台で {dAll1} 枚 ・ W2 台内の最大 {d2} 枚 / 全台で {dAll2} 枚 "
            + $"| W1 {(d1 >= 2 ? "○" : "**×**")} / W2 {(d2 >= 2 ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒"
            + $"（副判定 {ltPartners.Length * (ltVers.Length + 1) * ltStages.Count * LtSeeds:N0} 戦 ＋ "
            + $"主判定 {ltCompare.Length * ltVers.Length * ltStages.Count * LtSeeds:N0} 戦）。");
        return;
    }


    // ==============================================================================
    // reseat —— **この期の本体**（§6 の分岐: Phase 0 の P-4 が 3 行以上だったので席の期に切り替えた）。
    //   「P-3 の代替席を帯A で追試し、差 5.0pt 未満なら情報セルの多い側を採る」
    //
    // **候補の作り方は `reseat` モードの写し**（粗探索 seed 0..49 の全120通り → 上位20 ＋ 狙上位10 ＋ 現行）
    // ——`docs/reseat.md` はまさにこの手順の出力なので、**同じ候補集合を帯A で測り直すことになる。**
    //     dotnet run --project BattleSim -c Release 0 lit reseat [絞り込み]
    // ==============================================================================
    if (ltMode == "reseat")
    {
        string ltFilter = args.Length > 3 ? args[3] : "";
        var bal = LtBalance(ltStages.Count);
        var thin = ltCompare
            .Where(b => bal.TryGetValue(b.Name, out double[]? v) && LtInfo(v) <= 1)
            .Where(b => ltFilter.Length == 0 || ltFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
            .ToList();

        Console.WriteLine("# 第113期 —— (G14) の波及（`lit reseat`）");
        Console.WriteLine();
        Console.WriteLine("**この期の本体。** Phase 0 の (P-4) が線（3 行以上）を満たしたので、"
            + "指示書 §6 の分岐どおり**席の期に切り替えた**（灯の対象選択＝W1/W2 の採否は第114期へ送る）。");
        Console.WriteLine();
        Console.WriteLine($"対象は `docs/balance.md`（**帯A** = seed 0..{LtSeeds - 1}）の情報セルが **1 以下**の行 **{thin.Count} 行**。");
        Console.WriteLine();
        Console.WriteLine("**採否の規則（測る前に固定）**:");
        Console.WriteLine();
        Console.WriteLine("    (a) 狙（ガルドが前列 / セッキが後列）を満たす        ← 既存の作法。結果を見て足した条件ではない");
        Console.WriteLine("    (b) 現行席との差（**第2〜5波の平均**・規約 (G10)）が 5.0pt 未満");
        Console.WriteLine("    (c) そのうち**情報セル（帯A・第2〜5波）が最大**の席。同値は 平均が高い側 → 粗順が小さい側");
        Console.WriteLine("    (d) **情報セルが現行より多いときだけ採る**（同数なら据え置き＝席を動かさない）");
        Console.WriteLine();
        Console.WriteLine($"帯B（seed {LtCfBase}..{LtCfBase + LtCfSeeds - 1}）は**安定の確認として併記するだけ**"
            + "——情報セルは帯A で数える（規約 (G14)）。");

        var adopted = new List<(string Name, Formation F, int InfoFrom, int InfoTo, double Delta, double DeltaB)>();
        foreach (var build in thin)
        {
            var members = build.F.Occupied().Select(x => x.Def).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in SlotAssignments(members.Count))
            {
                var f = new Formation();
                for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
                perms.Add(f);
            }
            bool Intent(Formation f)
            {
                foreach (var (slot, def) in f.Occupied())
                {
                    if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
                    if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
                }
                return true;
            }
            var scan = new int[perms.Count];
            Parallel.For(0, perms.Count, i =>
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in ltStages)
                    for (int seed = 0; seed < LtScan; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            });
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            var pool = order.Take(20).Concat(order.Where(i => Intent(perms[i])).Take(10))
                            .Append(order.First(i => SameFormation(perms[i], build.F))).Distinct().ToList();
            var cA = new double[pool.Count][];
            var cB = new double[pool.Count][];
            Parallel.For(0, pool.Count, k => cA[k] = LtCells(perms[pool[k]], LitFilter.SupportOnly, 0, LtSeeds));
            Parallel.For(0, pool.Count, k => cB[k] = LtCells(perms[pool[k]], LitFilter.SupportOnly, LtCfBase, LtCfSeeds));

            int curK = pool.FindIndex(i => SameFormation(perms[i], build.F));
            double curAvg = LtAvg25(cA[curK]);
            int curInfo = LtInfo(cA[curK]);
            var ranked = Enumerable.Range(0, pool.Count).OrderByDescending(k => LtAvg25(cA[k])).ToList();

            Console.WriteLine();
            Console.WriteLine($"## {build.Name}{(ltPrim.Contains(build.Name) ? "（**主判定19行**）" : "")}");
            Console.WriteLine();
            Console.WriteLine($"現行席の情報セル **{curInfo}**・第2〜5波平均 **{curAvg:F1}%**。候補 {pool.Count} 通り。");
            Console.WriteLine();
            Console.WriteLine("| 追順 | 粗順 | 狙 | 前1/前3 | 中央 | 後1/後3 | 平均(2〜5波) | Δ | **情報セル** |"
                + string.Concat(ltStages.Select((_, i) => $" 第{i + 1}波 |")) + $" 平均({LtCfBase}..) | 情報セル({LtCfBase}..) |");
            Console.WriteLine("|--:|--:|:-:|---|---|---|--:|--:|--:|" + string.Concat(ltStages.Select(_ => "---:|")) + "---:|--:|");
            for (int r = 0; r < ranked.Count; r++)
            {
                int k = ranked[r];
                Formation f = perms[pool[k]];
                Console.WriteLine($"| {r + 1}{(k == curK ? "★現行" : "")} | {order.IndexOf(pool[k]) + 1} | {(Intent(f) ? "○" : "×")} "
                    + $"| {LtSeats(f)} | {LtAvg25(cA[k]):F1}% | {LtAvg25(cA[k]) - curAvg:+0.0;-0.0;0.0} | **{LtInfo(cA[k])}** |"
                    + string.Concat(cA[k].Select(c => $" {c:F1}% |"))
                    + $" {LtAvg25(cB[k]):F1}% | {LtInfo(cB[k])} |");
            }

            var ok = Enumerable.Range(0, pool.Count)
                .Where(k => k != curK && Intent(perms[pool[k]]) && Math.Abs(LtAvg25(cA[k]) - curAvg) < LtSeatLine)
                .OrderByDescending(k => LtInfo(cA[k])).ThenByDescending(k => LtAvg25(cA[k]))
                .ThenBy(k => order.IndexOf(pool[k])).ToList();
            Console.WriteLine();
            if (ok.Count == 0)
            {
                Console.WriteLine($"**判定: 据え置き** —— (a)(b) を満たす候補が 0 通り"
                    + $"（狙 ○ の候補 {Enumerable.Range(0, pool.Count).Count(k => k != curK && Intent(perms[pool[k]]))} 通りのうち、"
                    + $"差 {LtSeatLine:F1}pt 未満は 0 通り）。");
                continue;
            }
            int best = ok[0];
            Formation bf = perms[pool[best]];
            bool take = LtInfo(cA[best]) > curInfo;
            Console.WriteLine($"(a)(b) を満たす候補 **{ok.Count} 通り**、そのうち情報セル最大は 追順 **{ranked.IndexOf(best) + 1} 位**"
                + $"（情報セル {curInfo} → **{LtInfo(cA[best])}**、Δ **{LtAvg25(cA[best]) - curAvg:+0.0;-0.0}pt**"
                + $"、帯B では Δ {LtAvg25(cB[best]) - LtAvg25(cB[curK]):+0.0;-0.0}pt / 情報セル {LtInfo(cB[best])}）。");
            Console.WriteLine();
            Console.WriteLine($"**判定: {(take ? "差し替え" : "据え置き")}** —— (d) 情報セルが現行より{(take ? "多い" : "多くない")}。");
            if (take)
            {
                Console.WriteLine();
                Console.WriteLine($"採る配置: `front1: {LtN(bf[0])}, front3: {LtN(bf[1])}, center: {LtN(bf[2])}, "
                    + $"back1: {LtN(bf[3])}, back3: {LtN(bf[4])}`");
                adopted.Add((build.Name, bf, curInfo, LtInfo(cA[best]), LtAvg25(cA[best]) - curAvg,
                             LtAvg25(cB[best]) - LtAvg25(cB[curK])));
            }
        }

        Console.WriteLine();
        Console.WriteLine($"## まとめ —— 差し替える行 **{adopted.Count} 行 / {thin.Count} 行**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 主判定 | 情報セル | Δ 平均(2〜5波) | Δ 帯B | 採る配置 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|---|");
        foreach (var a in adopted)
            Console.WriteLine($"| {a.Name} | {(ltPrim.Contains(a.Name) ? "**P**" : "")} | {a.InfoFrom} → **{a.InfoTo}** "
                + $"| {a.Delta:+0.0;-0.0} | {a.DeltaB:+0.0;-0.0} | {LtSeats(a.F)} |");
        Console.WriteLine();
        Console.WriteLine($"情報セルの総和は **{adopted.Sum(a => a.InfoTo - a.InfoFrom):+0;-0;0}** 増える見込み"
            + "（`compare` を測り直して確かめる）。");
        return;
    }
    // ==============================================================================
    // seat —— 指示書 §3-3。**採用する版が決まってから**測り直す
    // （第107期「機構が変わった行の席は寿命が切れる。採用と席の再判定は対で」）。
    //     dotnet run --project BattleSim -c Release 0 lit seat [w0|w1|w2]
    // ==============================================================================
    if (ltMode == "seat")
    {
        string want = args.Length > 3 ? args[3].ToLowerInvariant() : "w0";
        int sv = want == "w2" ? LtW2 : want == "w1" ? LtW1 : LtW0;
        var tomoRow = ltCompare.FirstOrDefault(b => b.Name == LtRowName);
        if (tomoRow.F is null) { Console.WriteLine($"`{LtRowName}` が `Presets.Compare` に無い。"); return; }
        var members = tomoRow.F.Occupied().Select(o => o.Def).ToList();

        Console.WriteLine($"# 第113期 §3-3 —— `{LtRowName}` の席（版: {ltVers[sv].Tag}）");
        Console.WriteLine();
        Console.WriteLine($"5枚組は据え置き（指示書 §0-4）。120 通りを粗探索 seed 0..{LtScan - 1} → "
            + $"追試 seed 0..{LtSeeds - 1}（**帯A**。情報セルはここで数える・規約 (G14)）で測り、"
            + $"別帯（{LtCfBase}..{LtCfBase + LtCfSeeds - 1}）を安定の確認に併記する。");
        Console.WriteLine();
        Console.WriteLine($"採る席は3つの条件の積: (a) 情報セル 2 以上 ／ (b) 狙（ガルドが前列） ／ "
            + $"(c) 出発点との差が **{LtSeatLine:F1}pt 以上**（第46期の採否閾値）。");

        var perms = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation();
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            perms.Add(f);
        }
        var scan = new int[perms.Count];
        Parallel.For(0, perms.Count, i =>
        {
            int wins = 0;
            foreach (EnemyCatalog.Stage st in ltStages)
                for (int seed = 0; seed < LtScan; seed++)
                    if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false, taillight: LtRule(ltVers[sv].F)).PlayerWon) wins++;
            scan[i] = wins;
        });
        var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
        bool Intent(Formation f)
        {
            foreach (var (slot, def) in f.Occupied())
            {
                if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
                if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
            }
            return true;
        }
        var pool = order.Take(20).Concat(order.Where(i => Intent(perms[i])).Take(10))
                        .Append(order.First(i => SameFormation(perms[i], tomoRow.F))).Distinct().ToList();
        var cellsA = new double[pool.Count][];
        var cellsB = new double[pool.Count][];
        Parallel.For(0, pool.Count, k => cellsA[k] = LtCells(perms[pool[k]], ltVers[sv].F, 0, LtSeeds));
        Parallel.For(0, pool.Count, k => cellsB[k] = LtCells(perms[pool[k]], ltVers[sv].F, LtCfBase, LtCfSeeds));

        var ranked = Enumerable.Range(0, pool.Count).OrderByDescending(k => LtAvg25(cellsA[k])).ToList();
        int curK = pool.FindIndex(i => SameFormation(perms[i], tomoRow.F));
        Console.WriteLine();
        Console.WriteLine($"## 追試（seed 0..{LtSeeds - 1}）—— 候補 {pool.Count} 通り");
        Console.WriteLine();
        Console.WriteLine("| 追順 | 粗順 | 狙 | 前1/前3 | 中央 | 後1/後3 | 平均(2〜5波) | 情報セル |"
            + string.Concat(ltStages.Select((_, i) => $" 第{i + 1}波 |")) + $" 平均({LtCfBase}..) | 情報セル({LtCfBase}..) |");
        Console.WriteLine("|--:|--:|:-:|---|---|---|--:|--:|" + string.Concat(ltStages.Select(_ => "---:|")) + "---:|--:|");
        for (int r = 0; r < ranked.Count; r++)
        {
            int k = ranked[r];
            Formation f = perms[pool[k]];
            Console.WriteLine($"| {r + 1}{(k == curK ? "★出発点" : "")} | {order.IndexOf(pool[k]) + 1} | {(Intent(f) ? "○" : "×")} "
                + $"| {LtSeats(f)} | {LtAvg25(cellsA[k]):F1}% | {LtInfo(cellsA[k])} |"
                + string.Concat(cellsA[k].Select(c => $" {c:F1}% |"))
                + $" {LtAvg25(cellsB[k]):F1}% | {LtInfo(cellsB[k])} |");
        }
        int pick = -1;
        foreach (int k in ranked) if (LtInfo(cellsA[k]) >= 2 && Intent(perms[pool[k]])) { pick = k; break; }
        Console.WriteLine();
        Console.WriteLine($"**出発点**: 追順 **{ranked.IndexOf(curK) + 1} 位**、平均(2〜5波) **{LtAvg25(cellsA[curK]):F1}%**、"
            + $"情報セル **{LtInfo(cellsA[curK])}**（{LtCfBase}.. 帯では {LtAvg25(cellsB[curK]):F1}% / {LtInfo(cellsB[curK])}）。");
        Console.WriteLine();
        if (pick < 0) Console.WriteLine("**判定: 据え置き** —— 狙を満たし情報セルを 2 以上に保つ席が1つも無い。");
        else if (pick == curK) Console.WriteLine("**判定: 据え置き** —— 条件を満たす最上位が出発点そのもの。");
        else
        {
            double gain = LtAvg25(cellsA[pick]) - LtAvg25(cellsA[curK]);
            double gainB = LtAvg25(cellsB[pick]) - LtAvg25(cellsB[curK]);
            Formation f = perms[pool[pick]];
            Console.WriteLine($"条件を満たす最上位は 追順 **{ranked.IndexOf(pick) + 1} 位**（{LtAvg25(cellsA[pick]):F1}% / "
                + $"情報セル {LtInfo(cellsA[pick])}）で、出発点との差は **{gain:+0.0;-0.0}pt**"
                + $"（{LtCfBase}.. 帯では {gainB:+0.0;-0.0}pt）。");
            Console.WriteLine();
            Console.WriteLine($"**判定: {(gain >= LtSeatLine ? "差し替え" : "据え置き")}** —— 閾値 {LtSeatLine:F1}pt に対して {gain:+0.0;-0.0}pt。");
            if (gain >= LtSeatLine)
                Console.WriteLine($"採る配置: `front1: {LtN(f[0])}, front3: {LtN(f[1])}, center: {LtN(f[2])}, "
                    + $"back1: {LtN(f[3])}, back3: {LtN(f[4])}`");
        }

        // --------------------------------------------------------------------------
        // 第114期 (T2) —— **採る席の優先順**（指示書 §1 で、測る前に固定した規則）。
        //
        //   線   : 狙（ガルドが前列 / セッキが後列）○ **かつ** 情報セル 2 以上
        //          ——**狙を線にも採る条件にも掛ける**（規約 (G16)。第113期に食い違った）
        //   採る : 情報セル **4 → 3** の順に、その段の中で平均が最上位。
        //          出発点との差が {LtSeatLine}pt 未満なら**情報セルの多い側**を採る
        //          （情報セル 2 未満しか無ければ採用そのものを見直す＝指示書 §5 の分岐）
        //
        // **上の第113期の判定はそのまま残してある。** あちらは「平均が最上位・差 5.0pt 以上なら
        // 差し替え」で、情報セルは 2 以上という門にしか使っていない——**規則が違うので判定も違う。**
        // 第114期は「情報を減らす変更は席の再判定と対にする」ための期なので、**情報セルが優先。**
        // --------------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 第114期 (T2) —— 採る席の優先順（**情報セルが優先**）");
        Console.WriteLine();
        var ltLine = Enumerable.Range(0, pool.Count).Where(k => LtInfo(cellsA[k]) >= 2).ToList();
        var ltLineOk = ltLine.Where(k => Intent(perms[pool[k]])).ToList();
        Console.WriteLine($"候補 {pool.Count} 通りのうち **情報セル 2 以上が {ltLine.Count} 通り**、"
            + $"そのうち**狙で落ちた席が {ltLine.Count - ltLineOk.Count} 通り**（狙 ○ は **{ltLineOk.Count} 通り**）"
            + $"——**狙は線にも採る条件にも掛かっている**（規約 (G16)・自己検査 (d)）。");
        Console.WriteLine();
        Console.WriteLine($"| 情報セル | 狙 ○ の席数 | その段の最上位（追順・平均・Δ） |");
        Console.WriteLine("|--:|--:|---|");
        for (int wantInfo = 4; wantInfo >= 2; wantInfo--)
        {
            var tier = ltLineOk.Where(k => LtInfo(cellsA[k]) == wantInfo)
                               .OrderByDescending(k => LtAvg25(cellsA[k])).ToList();
            Console.WriteLine($"| {wantInfo} | {tier.Count} | "
                + (tier.Count == 0 ? "—"
                   : $"追順 {ranked.IndexOf(tier[0]) + 1} 位・{LtAvg25(cellsA[tier[0]]):F1}%"
                     + $"・{LtAvg25(cellsA[tier[0]]) - LtAvg25(cellsA[curK]):+0.0;-0.0}pt") + " |");
        }

        int take114 = -1;
        for (int wantInfo = 4; wantInfo >= 3 && take114 < 0; wantInfo--)
        {
            var tier = ltLineOk.Where(k => LtInfo(cellsA[k]) == wantInfo)
                               .OrderByDescending(k => LtAvg25(cellsA[k])).ToList();
            foreach (int k in tier)
            {
                if (Math.Abs(LtAvg25(cellsA[k]) - LtAvg25(cellsA[curK])) >= LtSeatLine) continue;
                take114 = k; break;
            }
        }
        Console.WriteLine();
        if (take114 < 0)
            Console.WriteLine($"**判定（第114期）: 情報セル 3 以上・狙 ○・差 {LtSeatLine:F1}pt 未満の席が無い** "
                + "—— 指示書 §5 の分岐へ。");
        else if (LtInfo(cellsA[take114]) <= LtInfo(cellsA[curK]))
            Console.WriteLine($"**判定（第114期）: 据え置き** —— 採る候補の情報セル {LtInfo(cellsA[take114])} が"
                + $"出発点の {LtInfo(cellsA[curK])} を上回らない。");
        else
        {
            Formation nf = perms[pool[take114]];
            Console.WriteLine($"**判定（第114期）: 差し替え** —— 追順 **{ranked.IndexOf(take114) + 1} 位**"
                + $"（情報セル {LtInfo(cellsA[curK])} → **{LtInfo(cellsA[take114])}**、"
                + $"平均 {LtAvg25(cellsA[curK]):F1}% → **{LtAvg25(cellsA[take114]):F1}%** ＝ "
                + $"**{LtAvg25(cellsA[take114]) - LtAvg25(cellsA[curK]):+0.0;-0.0}pt**、"
                + $"帯B では {LtAvg25(cellsB[take114]) - LtAvg25(cellsB[curK]):+0.0;-0.0}pt / "
                + $"情報セル {LtInfo(cellsB[take114])}）。");
            Console.WriteLine();
            Console.WriteLine($"採る配置: `front1: {LtN(nf[0])}, front3: {LtN(nf[1])}, center: {LtN(nf[2])}, "
                + $"back1: {LtN(nf[3])}, back3: {LtN(nf[4])}`");
            Console.WriteLine();
            Console.WriteLine("落とした上位3席（表A）:");
            Console.WriteLine();
            Console.WriteLine("| 追順 | 狙 | 情報セル | 平均(2〜5波) | Δ | 落とした理由 |");
            Console.WriteLine("|--:|:-:|--:|--:|--:|---|");
            foreach (int k in ranked.Where(k => k != take114).Take(3))
                Console.WriteLine($"| {ranked.IndexOf(k) + 1} | {(Intent(perms[pool[k]]) ? "○" : "×")} "
                    + $"| {LtInfo(cellsA[k])} | {LtAvg25(cellsA[k]):F1}% "
                    + $"| {LtAvg25(cellsA[k]) - LtAvg25(cellsA[curK]):+0.0;-0.0} | "
                    + (!Intent(perms[pool[k]]) ? "狙 ×"
                       : LtInfo(cellsA[k]) < LtInfo(cellsA[take114]) ? $"情報セル {LtInfo(cellsA[k])} < {LtInfo(cellsA[take114])}"
                       : "—") + " |");
        }
        return;
    }

    // ==============================================================================
    // check —— 指示書 §5 の自己検査（必須4項目 ＋ この期固有の (a)〜(f)）。
    // ==============================================================================
    if (ltMode == "check")
    {
        Console.WriteLine("# 第113期 —— 自己検査（`lit check`）");
        Console.WriteLine();
        Console.WriteLine("## 必須4項目");
        Console.WriteLine();

        // 必須1 / (a): `compare` 305 セル。**既定の経路**（ノブを渡さない）と **W0 を明示した経路**の両方。
        var bal = LtBalance(ltStages.Count);
        int diffDefault = 0, diffW0 = 0, diffKnob = 0;
        var cellsDef = new double[ltCompare.Length][];
        var cellsW0 = new double[ltCompare.Length][];
        Parallel.For(0, ltCompare.Length, b =>
        {
            cellsDef[b] = ltStages.Select(st =>
            {
                int w = 0;
                for (int seed = 0; seed < LtSeeds; seed++)
                    if (BattleEngine.Run(ltCompare[b].F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                return w * 100.0 / LtSeeds;
            }).ToArray();
            cellsW0[b] = LtCells(ltCompare[b].F, LitFilter.SupportOnly, 0, LtSeeds);
        });
        for (int b = 0; b < ltCompare.Length; b++)
        {
            for (int w = 0; w < ltStages.Count; w++)
                if (Math.Abs(cellsDef[b][w] - cellsW0[b][w]) > 1e-9) diffKnob++;
            if (!bal.TryGetValue(ltCompare[b].Name, out double[]? o)) continue;
            for (int w = 0; w < ltStages.Count; w++)
            {
                if (Math.Abs(cellsDef[b][w] - o[w]) > 1e-9) diffDefault++;
                if (Math.Abs(cellsW0[b][w] - o[w]) > 1e-9) diffW0++;
            }
        }
        int cells = ltCompare.Length * ltStages.Count;
        // **第114期に判定を直した。** 既定が W2（`LitFilter.ActingNow`）になったので、
        // **W0 を明示した経路は `docs/balance.md` とずれるのが正しい**——ずれ幅がそのまま採用の効きで、
        // **ずれる行が `灯×薙ぎ` 1 行だけ**であることが「他の 60 行を動かしていない」の証拠になる
        // （第113期は既定＝W0 だったので、3つとも 0 件が合格だった）。
        var knobRows = Enumerable.Range(0, ltCompare.Length)
            .Where(b => ltStages.Select((_, w) => w).Any(w => Math.Abs(cellsDef[b][w] - cellsW0[b][w]) > 1e-9))
            .Select(b => ltCompare[b].Name).ToList();
        Console.WriteLine($"- **必須1 / (a)** `compare` {cells} セルと `docs/balance.md` の差: "
            + $"**既定の経路 {diffDefault} 件**（← 判定はここ） ／ W0 を明示した経路 {diffW0} 件 ／ 両経路の差 {diffKnob} 件"
            + $" → {(diffDefault == 0 ? "**○**" : "**×**")}");
        Console.WriteLine($"  - 既定（W2）と W0 でセルが動く行: **{knobRows.Count} 行**"
            + (knobRows.Count == 0 ? "" : "（" + string.Join(" / ", knobRows) + "）")
            + $" → {(knobRows.Count <= 1 ? "**○**" : "**×**")}"
            + "（**トモを含む行だけが動く**。第113期の Q5 の再確認）");
        Console.WriteLine("- **必須2** `docs/` 10ファイルの再生成は外で行う（`git diff docs/` で示す）。"
            + "**`rules.md` に `LitFilter` の行と `TaillightRule` の既定値が増えるのは想定内**（指示書 §5）。");
        Console.WriteLine($"- **必須3** 触っていないノブの既定: `TaillightRule.Default` = `{TaillightRule.Default}` ／ "
            + $"`SeverRule.Default` = `{SeverRule.Default}` ／ `EncoreRule.Default` = `{EncoreRule.Default}` ／ "
            + $"`SoakRule.Default` = `{SoakRule.Default}` ／ `CurseRule.Default` = `{CurseRule.Default}` ／ "
            + $"`MenderCostRule.Default` = `{MenderCostRule.Default}` ／ `LooseRule.Default` = `{LooseRule.Default}`");
        int pickN = 0;
        string? root = LtRoot();
        if (root is not null)
            foreach (string f in Directory.GetFiles(Path.Combine(root, "BattleCore"), "*.cs"))
                pickN += System.Text.RegularExpressions.Regex.Matches(
                    string.Join("\n", File.ReadAllLines(f).Where(l => !l.TrimStart().StartsWith("//"))), @"PickOne\(").Count;
        Console.WriteLine($"- **必須4** `BattleCore` の `PickOne(` 呼び出し **{pickN} 箇所**"
            + "（第94期は 26 箇所。**第113期は1つも足していない**——濾しは `continue` を2本足しただけ）");

        // ---- この期固有
        Console.WriteLine();
        Console.WriteLine("## この期固有の (b)〜(f)");
        Console.WriteLine();

        // (b) 濾した集合が (P-5) の一覧と一致するか。**全員が濾しに掛かる台**をローカルに組む。
        //     カド (A・不動) / ハギ (A・追い打ち) / ノノ (B・周期に攻撃が無い) / ドルガ (C・休み番)
        Formation chk1 = Formation.Build(front1: UnitCatalog.Kado, front3: UnitCatalog.Hagi,
                                         center: UnitCatalog.Lili, back1: UnitCatalog.Tomo, back3: UnitCatalog.Dolga);
        // (c) **W1 でも候補が 0 人になる台**（(C) を持つ駒も入れない）。
        Formation chk2 = Formation.Build(front1: UnitCatalog.Kado, front3: UnitCatalog.Hagi,
                                         center: UnitCatalog.Lili, back1: UnitCatalog.Tomo, back3: UnitCatalog.Mio);
        const int ChkSeeds = 40;
        var chkStat = new LtStat[2, ltVers.Length];
        for (int v = 0; v < ltVers.Length; v++)
        {
            chkStat[0, v] = new LtStat(); chkStat[1, v] = new LtStat();
            foreach (EnemyCatalog.Stage st in ltStages)
            {
                chkStat[0, v].AddFrom(LtMeasure(chk1, ltVers[v].F, st, 0, ChkSeeds));
                chkStat[1, v].AddFrom(LtMeasure(chk2, ltVers[v].F, st, 0, ChkSeeds));
            }
        }
        var predict1 = chk1.Occupied().Select(o => o.Def)
            .Where(d => !d.Traits.Contains(TraitId.Taillight) && !d.Traits.Any(t => TraitCatalog.Get(t).BlocksSupport)
                        && !TaillightTrait.AttacksInOwnTurn(d)).Select(d => d.Name).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var seen1 = chkStat[0, LtW1].SkipStaticBy.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var seenNow = chkStat[0, LtW2].SkipNowBy.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var predictNow = new List<string> { UnitCatalog.Dolga.Name };
        Console.WriteLine($"- **(b)** W1 が濾した集合（実測）= {{{string.Join(", ", seen1)}}} ／ "
            + $"(P-5) の予測 = {{{string.Join(", ", predict1)}}} → "
            + $"{(seen1.SequenceEqual(predict1) ? "**○ 一致**" : "**× ずれ**")}");
        Console.WriteLine($"  - W2 の動的な濾し（実測）= {{{string.Join(", ", seenNow)}}} ／ 予測 = "
            + $"{{{string.Join(", ", predictNow)}}} → {(seenNow.SequenceEqual(predictNow) ? "**○ 一致**" : "**× ずれ**")}"
            + "（のろまの休み番。**W0 では 0 件**: " + chkStat[0, LtW0].SkipNowBy.Count + " 件）");
        Console.WriteLine($"  - **W0 では濾しが1件も走らない**（静 {chkStat[0, LtW0].SkipStatic:F0} 件 / "
            + $"動 {chkStat[0, LtW0].SkipNow:F0} 件） → {(chkStat[0, LtW0].SkipStatic == 0 && chkStat[0, LtW0].SkipNow == 0 ? "**○**" : "**×**")}");

        Console.WriteLine();
        Console.WriteLine("- **(c)** 候補 0 人のとき 3 版とも `TaillightIdle` に落ちる（`TaillightFires` は増えない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 灯（門1） | 空振り `TaillightIdle` | 消灯 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        string[] chkName = { "カド/ハギ/ノノ/**ドルガ**（W2 の偶数Tで 0 人）", "カド/ハギ/ノノ/**ミオ**（W1 で 0 人）" };
        for (int i = 0; i < 2; i++)
            for (int v = 0; v < ltVers.Length; v++)
            {
                LtStat ss = chkStat[i, v];
                Console.WriteLine($"| {chkName[i]} | {ltVers[v].Tag} | {ss.Per(ss.Fires):F2} | **{ss.Per(ss.Idle):F2}** | {ss.Per(ss.Doused):F1} |");
            }
        bool cOk = chkStat[1, LtW1].Fires == 0 && chkStat[1, LtW1].Idle > 0
                   && chkStat[1, LtW2].Fires == 0 && chkStat[1, LtW2].Idle > 0
                   && chkStat[0, LtW2].Idle > chkStat[0, LtW1].Idle
                   && chkStat[0, LtW0].Idle > 0;
        Console.WriteLine();
        Console.WriteLine($"  → {(cOk ? "**○**" : "**×**")} —— "
            + "**ミオ台は W1/W2 で灯 0.00・空振りだけ**（候補が構造的に 0 人）／"
            + "**ドルガ台は W2 の偶数ターンだけ 0 人**になるので空振りが W1 より増える。"
            + "**W0 の空振りが 0 でないのは「味方が全滅してトモだけが残った」局面**"
            + "——同じ `pick is null` の枝で、**3版が同じ経路を通っていることの証拠**"
            + "（濾しの有無に関わらず `Douse` して `TaillightIdle` を立てる）。");

        // (d)(e) 副判定4台で確かめる
        LtStat[,,] cell = LtBenchRun(0, LtSeeds);
        var supportBlocked = new HashSet<string>(UnitCatalog.All
            .Where(d => d.Traits.Any(t => TraitCatalog.Get(t).BlocksSupport)).Select(d => d.Name), StringComparer.Ordinal);
        int badSupport = 0;
        for (int p = 0; p < ltPartners.Length; p++)
            for (int v = 0; v < ltVers.Length; v++)
                foreach (var kv in LtSum(cell, p, v, false).LitBy)
                    if (supportBlocked.Contains(kv.Key)) badSupport += kv.Value;
        Console.WriteLine();
        Console.WriteLine($"- **(d)** 灯の対象が `AcceptsSupport` 偽の駒になった回数（3版・4台・全5波）: **{badSupport} 件** "
            + $"→ {(badSupport == 0 ? "**○**" : "**×**")}（4台とも前3 はガルド＝`Stoic`）");

        Console.WriteLine();
        Console.WriteLine("- **(e)** 門1（灯）が 3 版で同数か。**決着ターン数を併記する**（規約 (G6)）");
        Console.WriteLine();
        Console.WriteLine("| 台 | W0 灯 | W1 灯 | W2 灯 | W0 決着T | W1 決着T | W2 決着T | 判定 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|:-:|");
        int eBad = 0;
        for (int p = 0; p < ltPartners.Length; p++)
        {
            var vs = Enumerable.Range(0, ltVers.Length).Select(v => LtSum(cell, p, v, true)).ToArray();
            bool same = Math.Abs(vs[0].Per(vs[0].Fires) - vs[1].Per(vs[1].Fires)) < 0.005
                     && Math.Abs(vs[0].Per(vs[0].Fires) - vs[2].Per(vs[2].Fires)) < 0.005;
            if (!same) eBad++;
            Console.WriteLine($"| {ltPartners[p].Tag} | {vs[0].Per(vs[0].Fires):F2} | {vs[1].Per(vs[1].Fires):F2} | {vs[2].Per(vs[2].Fires):F2} "
                + $"| {vs[0].Per(vs[0].Turns):F2} | {vs[1].Per(vs[1].Turns):F2} | {vs[2].Per(vs[2].Turns):F2} | {(same ? "○" : "**×**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"  → 同数でない台 **{eBad} / {ltPartners.Length}**。"
            + "**ずれるなら原因は決着ターン数**——灯はトモが生きている限り毎ターン点くので、"
            + "**濾しが盤面を動かして決着が伸び縮みすれば灯の回数も動く。**"
            + "指示書 §3-2 の「3版で同数のはず」は<b>決着が動かない</b>ことを暗黙に仮定していた。");

        Console.WriteLine();
        Console.WriteLine($"- **(f)** 情報セルを数えた帯: **seed 0..{LtSeeds - 1}（帯A）**"
            + $"——`docs/balance.md` が載る帯と同一（規約 (G14)）。追試の帯 {LtCfBase}..{LtCfBase + LtCfSeeds - 1} は"
            + "`lit seat` の右端の列にだけ使い、**情報セルの判定には使わない。**");
        return;
    }

    Console.WriteLine("mode: phase0 / reseat [絞り込み] / run / seat [w0|w1|w2] / check");
    return;
}
}
