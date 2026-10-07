using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;

// checkwave poison ／ run（規定の組）—— 第264期「チェック波の採用確定」。
//   poison: `compare` の毒の行を素の 400/300 第四波に当て、7台目（毒台）を選ぶ表（選定基準: 勝率 50% 超・同率は全員生存 → 決着T の早い順）
//   run:    規定の組（ボス B3-桁・手数 W3-割合）を7台で回す。`ranks` も7台（第263期の6台の版は `ranks263`）
static partial class CheckWaveDiag
{
    /// <summary>第260〜263期の6台（各期の再現は必ずこの並びで回す）。</summary>
    internal static readonly string[] Boards6 = { "燃焼 T3-244", "燃焼 T3-255", "移動", "雷", "毒", "混ぜ-255" };
    /// <summary>
    /// 第264期の7台目（毒台の追加）。`compare` の毒の行のうち、素の 400/300 第四波で勝率が最も高い行（`checkwave poison` の表）。
    /// 旧の毒台（毒 (グザ×ミオ×ラウ)）は「土台負け」の対照として残す。
    /// </summary>
    internal const string Poison2 = "毒+ベニ+ラウ";
    internal static readonly string[] Boards7 = Boards6.Append(Poison2).ToArray();

    /// <summary>規定の組（指示書 §1.1）: 以後チェック波を物差しとして使うときはこの2版を7台で回す。</summary>
    internal const string DefaultBoss = "B3-桁", DefaultHand = "W3-割合";

    static RunSpec Spec264 => new(
        "# 第264期 —— チェック波の規定の組（ボス B3-桁 ／ 手数 W3-割合・7台・seed 0..199）",
        $"規定の組。正式な指標は連続量（倒しT・回復を上回った窓・窓あたり出力・癒し手を割ったT・純実入り）で、勝率は副。打ち切り {BossTurns} ターン。7台目は `{Poison2}`（`checkwave poison` で選んだ）。",
        new[] { CWaveOf(DefaultBoss), CWaveOf(DefaultHand) },
        "- 対照: `第四波`（倍率なし）・`W2-対照`（400/300 の素の第四波）",
        new (string, CWave)[] { (DefaultBoss, CWaveOf(DefaultBoss)), (DefaultHand, CWaveOf(DefaultHand)), ("W2-対照", CWaveOf("W2-対照")) },
        new[] { "第四波", "W2-対照", DefaultHand, DefaultBoss },
        new[] { DefaultHand }, new[] { "W2-対照", DefaultHand },
        new[] { DefaultBoss }, new[] { DefaultBoss }, Heals: true, Band: true);

    /// <summary>
    /// ほかの軸の核（毒軸の代表の台に入っていてはいけない駒）。燃焼（ボルグ・ホタ・ヒヨ）・移動（バサ・セロ・ヨミ・シオ・ハネ）・雷（シガ・ツギ・カタ）。
    /// ベニ・ミオは毒の行にも雷の台にも入る両属の駒なので数えない。
    /// </summary>
    internal static readonly HashSet<string> OtherAxisCores = new() { "borg", "hota", "hiyo", "basa", "sero", "yomi", "shio", "hane", "shiga", "tsugi", "kata" };

    static (string Name, Agg A, bool Pure) Pick(List<(string Name, Agg A, bool Pure)> scored)
        => scored.Where(x => x.Pure && x.A.Win > 50).OrderByDescending(x => x.A.Win).ThenByDescending(x => x.A.SurvPct).ThenBy(x => (double)x.A.Turns / x.A.N).FirstOrDefault();

    /// <summary>
    /// 選定を測り直して選ばれた行の名前を返す（自己検査用）。選定は第264期（規定のトウ ＝ T0）の記録なので、
    /// 第287期からは行のトウを旧の規定 `TouT0` に戻して測り直す（規定の T3 のまま測ると `毒+耐久 (ベニ×トウ)` が選ばれる・第287期の報告 §6）。
    /// </summary>
    internal static string? PickPoisonName(bool asOf264 = true)
    {
        var scored = CompareBuilds().Where(r => r.Name.Contains('毒'))
            .Select(r => (r.Name, F: asOf264 ? FvSwap(r.F, UnitCatalog.Tou, UnitCatalog.TouT0) : r.F))
            .Select(r => (r.Name, A: MeasureWave(r.F, CWaveOf("W2-対照")), Pure: r.F.Occupied().All(o => !OtherAxisCores.Contains(o.Def.Id)))).ToList();
        return Pick(scored).Name;
    }

    /// <summary>`compare` の毒の行（名前に「毒」を含む `Presets.Compare` の行）を、素の 400/300 第四波（W2-対照）に当てる。</summary>
    static void PoisonPick()
    {
        var rows = CompareBuilds().Where(r => r.Name.Contains('毒')).ToList();
        Console.WriteLine("# 第264期 毒台の選定（`compare` の毒の行 × 素の 400/300 第四波 × seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("選定基準（指示書 §1.2 ＋ 第264期の絞り込み）: **ほかの軸の核を含まない行**のうち、勝率が 50% を超え、勝率 → 全員生存 → 決着T（早い）の順で最も良い1行。");
        Console.WriteLine("ほかの軸の核 ＝ 燃焼（ボルグ・ホタ・ヒヨ）・移動（バサ・セロ・ヨミ・シオ・ハネ）・雷（シガ・ツギ・カタ）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 席 | ほかの軸の核 | 勝率 | 全員生存 | 決着T | 窓あたり出力 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|");
        var scored = new List<(string Name, Agg A, bool Pure)>();
        foreach (var (name, f) in rows)
        {
            var a = MeasureWave(f, CWaveOf("W2-対照"));
            var cores = f.Occupied().Select(o => o.Def).Where(d => OtherAxisCores.Contains(d.Id)).Select(d => d.Name).ToList();
            scored.Add((name, a, cores.Count == 0));
            Console.WriteLine($"| {name} | {BA.SeatsNamed(f)} | {(cores.Count == 0 ? "—" : string.Join("・", cores))} | {F1(a.Win)} | {F1(a.SurvPct)} | {Per(a.Turns, a.N)} | {Per(a.WinDmg, a.WinCnt)} |");
        }
        var literal = scored.Where(x => x.A.Win > 50).OrderByDescending(x => x.A.Win).ThenByDescending(x => x.A.SurvPct).ThenBy(x => (double)x.A.Turns / x.A.N).FirstOrDefault();
        Console.WriteLine();
        Console.WriteLine($"指示書の基準だけ（核の絞り込みなし）で選ぶと: {literal.Name ?? "—"}");
        var pick = Pick(scored);
        Console.WriteLine();
        Console.WriteLine(pick.Name is null ? "選べる行が無い（どの行も 50% 以下）。" : $"**選んだ行: {pick.Name}**（勝率 {F1(pick.A.Win)}・全員生存 {F1(pick.A.SurvPct)}）。器具の 7台目 `{Poison2}` と{(pick.Name == Poison2 ? "一致" : "**不一致**")}。");
    }
}
