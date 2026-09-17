using BattleCore;
using static Common;

// =====================================================================================
// census モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "census")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 census
// =====================================================================================

static class CensusDiag
{
// census モード: 棚卸し（第48期）。**駒と通貨の対応表を作るための素材だけを機械的に出す。**
//
// ロスターの上限を52体と決めたので、新規追加の合否テストに「どの通貨の空白を埋めるか」を
// 足す必要が出た。その判断に要る員数（分母）と、`CompareBuilds()` の走査結果を出す。
//
// **盤面は1つも動かさない。** 戦闘を1回も回さないので所要は1秒未満。
// `Traits.cs` / `UnitCatalog.cs` / `Stages` / `CompareBuilds()` には1行も触れていない。
//
// **通貨の書き手/読み手の判定はここでは行わない**（grep と目視で報告書に手で書く。§3）。
// ここが出すのは員数・compare の出現数・特性の保持者一覧＝**判定の分母**だけ。
// Trait に属性を足して自動判定させる案は採らない——判定の根拠が
// 「誰かが属性を正しく付けたか」に化けて、grep で検算できなくなる。
//
// **出力は docs/ に置かない**（標準出力で読むだけ）。
//
//     dotnet run --project BattleSim -c Release 0 census
public static void Run(string[] args, int stageIndex)
{
    const int RosterCap = 52;   // トランプ1組（ジョーカーを除く）

    // 反射で拾うのは「定義された `UnitDef`」——`All` に載っていない残置駒
    // （第44期の誹り・第46期の驕り）を数えるには、リストではなくフィールドを見るしかない。
    static (string Field, UnitDef Def)[] CsDefs(Type t) =>
        t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
         .Where(f => f.FieldType == typeof(UnitDef))
         .Select(f => (f.Name, (UnitDef)f.GetValue(null)!))
         .ToArray();

    static string CsTraits(UnitDef d)
        => d.Traits is { Count: > 0 } tr ? string.Join(", ", tr.Select(t => t.ToString())) : "—";

    var csAllyDefs = CsDefs(typeof(UnitCatalog));
    var csEnemyDefs = CsDefs(typeof(EnemyCatalog));
    var csRoster = UnitCatalog.All;
    var csRosterIds = csRoster.Select(u => u.Id).ToHashSet();
    var csOrphans = csAllyDefs.Where(d => !csRosterIds.Contains(d.Def.Id)).ToArray();

    var csBuilds = CompareBuilds();
    var csStages = EnemyCatalog.Stages;

    Console.WriteLine("# 棚卸し —— 駒と通貨の対応表の素材（第48期 census）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 census` の出力。**docs/ には置かない。**");
    Console.WriteLine("戦闘を1回も回していない（盤面は1つも動かない）。");
    Console.WriteLine();

    // --- Phase 0-1: 員数 ---------------------------------------------------------------------
    Console.WriteLine("## Phase 0-1: `UnitDef` の員数");
    Console.WriteLine();
    Console.WriteLine("| 区分 | 数 |");
    Console.WriteLine("|---|--:|");
    Console.WriteLine($"| 味方 `UnitDef` の定義 | {csAllyDefs.Length} |");
    Console.WriteLine($"| うち `UnitCatalog.All` に登録 | {csRoster.Count} |");
    Console.WriteLine($"| うち `All` に載っていない | {csOrphans.Length} |");
    Console.WriteLine($"| 敵 `UnitDef` の定義 | {csEnemyDefs.Length} |");
    Console.WriteLine();
    Console.WriteLine("**`All` に載っていない味方の駒**（52 の分母に数えるかどうかの論点）:");
    Console.WriteLine();
    Console.WriteLine("| フィールド | Id | 名前 | 特性 |");
    Console.WriteLine("|---|---|---|---|");
    foreach (var (field, d) in csOrphans)
        Console.WriteLine($"| `{field}` | `{d.Id}` | {d.Name} | {CsTraits(d)} |");
    Console.WriteLine();
    Console.WriteLine($"残り枠 = {RosterCap} − {csRoster.Count}（`All` のみ）= **{RosterCap - csRoster.Count}** ／ "
        + $"{RosterCap} − {csAllyDefs.Length}（定義すべて）= **{RosterCap - csAllyDefs.Length}**");
    Console.WriteLine();

    // --- Phase 0-2: StatusKeys ---------------------------------------------------------------
    Console.WriteLine("## Phase 0-2: `StatusKeys.All`");
    Console.WriteLine();
    Console.WriteLine($"要素数 **{StatusKeys.All.Length}**。主表の 1〜{StatusKeys.All.Length} はこれで確定する。");
    Console.WriteLine();
    Console.WriteLine("| # | 値 |");
    Console.WriteLine("|--:|---|");
    for (int i = 0; i < StatusKeys.All.Length; i++)
        Console.WriteLine($"| {i + 1} | `{StatusKeys.All[i]}` |");
    Console.WriteLine();

    // --- Phase 0-3: CompareBuilds の走査 ------------------------------------------------------
    var csRows = new Dictionary<string, List<string>>();
    foreach (var b in csBuilds)
        foreach (var (_, d) in b.F.Occupied())
        {
            if (!csRows.TryGetValue(d.Id, out var lst)) csRows[d.Id] = lst = new List<string>();
            if (!lst.Contains(b.Name)) lst.Add(b.Name);
        }

    int CsCount(string id) => csRows.TryGetValue(id, out var l) ? l.Count : 0;

    Console.WriteLine("## Phase 0-3: `CompareBuilds()` の走査");
    Console.WriteLine();
    Console.WriteLine($"行数 **{csBuilds.Length}**。同じ編成に同じ駒は2枚入らないので、出現数＝行数。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | Id | 行数 | 出ている編成 |");
    Console.WriteLine("|---|---|--:|---|");
    foreach (var u in csRoster.OrderByDescending(u => CsCount(u.Id)).ThenBy(u => u.Id))
        Console.WriteLine($"| {u.Name} | `{u.Id}` | {CsCount(u.Id)} | "
            + (CsCount(u.Id) == 0 ? "**—（一度も出ていない）**" : string.Join(" / ", csRows[u.Id])) + " |");
    Console.WriteLine();

    var csUnused = csRoster.Where(u => CsCount(u.Id) == 0).ToArray();
    Console.WriteLine($"**一度も compare に載っていない駒: {csUnused.Length} 体**（表D の母集団）");
    Console.WriteLine();
    Console.WriteLine("| 駒 | Id | 特性 |");
    Console.WriteLine("|---|---|---|");
    foreach (var u in csUnused)
        Console.WriteLine($"| {u.Name} | `{u.Id}` | {CsTraits(u)} |");
    Console.WriteLine();
    Console.WriteLine("`All` に載っていない駒（Phase 0-1）は定義上ここに含まれない——");
    Console.WriteLine("**「ロスターに載っていない」と「ロスターに載っているのに使われていない」は別の話**なので分けて数える。");
    Console.WriteLine();

    // --- Phase 0-4: 回帰チェックの分母 --------------------------------------------------------
    Console.WriteLine("## Phase 0-4: `docs/balance.md` の分母");
    Console.WriteLine();
    Console.WriteLine($"編成 **{csBuilds.Length}** 行 × 波 **{csStages.Count}** = **{csBuilds.Length * csStages.Count} セル**。");
    Console.WriteLine("§4-1 の回帰チェック（`compare` の食い違い0件）はこの分母で数える。");
    Console.WriteLine();

    // --- 付表1: 駒 × 特性（表B の素材） -------------------------------------------------------
    Console.WriteLine("## 付表1: 駒 × 特性（表B の索引）");
    Console.WriteLine();
    Console.WriteLine("**特性は通貨ではない。** この表は「どの Trait クラスを grep すればよいか」の索引にすぎず、");
    Console.WriteLine("通貨の書き手/読み手はここからは決まらない（1つの特性が複数の通貨を書く場合がある）。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | Id | 陣営 | `All` | 特性 | compare 行数 |");
    Console.WriteLine("|---|---|---|---|---|--:|");
    foreach (var (_, d) in csAllyDefs.OrderBy(x => x.Def.Id))
        Console.WriteLine($"| {d.Name} | `{d.Id}` | 味方 | {(csRosterIds.Contains(d.Id) ? "○" : "**×**")} "
            + $"| {CsTraits(d)} | {CsCount(d.Id)} |");
    foreach (var (_, d) in csEnemyDefs.OrderBy(x => x.Def.Id))
        Console.WriteLine($"| {d.Name} | `{d.Id}` | 敵 | — | {CsTraits(d)} | — |");
    Console.WriteLine();

    // --- 付表2: 特性 → 保持者（表C の素材） -------------------------------------------------
    Console.WriteLine("## 付表2: 特性 → 保持者（表C の素材）");
    Console.WriteLine();
    Console.WriteLine("**唯一性の判定そのものではない**（唯一性は通貨について問うもので、特性についてではない）。");
    Console.WriteLine("保持者が1体の特性は、その駒を切ると**その特性の実装が誰にも使われなくなる**という別の意味を持つ。");
    Console.WriteLine();
    Console.WriteLine("| 特性 | 味方の保持者 | 敵の保持者 | 味方数 | 敵数 |");
    Console.WriteLine("|---|---|---|--:|--:|");
    foreach (TraitId t in Enum.GetValues<TraitId>())
    {
        var a = csAllyDefs.Where(x => x.Def.Traits is { } tr && tr.Contains(t)).Select(x => x.Def.Name).ToArray();
        var e = csEnemyDefs.Where(x => x.Def.Traits is { } tr && tr.Contains(t)).Select(x => x.Def.Name).ToArray();
        Console.WriteLine($"| {t} | {(a.Length == 0 ? "—" : string.Join(" / ", a))} "
            + $"| {(e.Length == 0 ? "—" : string.Join(" / ", e))} | {a.Length} | {e.Length} |");
    }
    Console.WriteLine();

    // --- 付表3: 敵の駒 × Stages ---------------------------------------------------------------
    var csInStage = new Dictionary<string, List<string>>();
    foreach (var st in csStages)
        foreach (var (_, d) in st.Enemy.Occupied())
        {
            if (!csInStage.TryGetValue(d.Id, out var lst)) csInStage[d.Id] = lst = new List<string>();
            if (!lst.Contains(st.Name)) lst.Add(st.Name);
        }

    Console.WriteLine("## 付表3: 敵の駒 × `Stages`");
    Console.WriteLine();
    Console.WriteLine("敵側の「事実上すでにリストラされている駒」。味方の表D と対になる。");
    Console.WriteLine("`Columns` は `Stages` の並べ替えなので、ここに出ない駒はどの部隊列にも出ない。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | Id | 波数 | 出ている波 |");
    Console.WriteLine("|---|---|--:|---|");
    foreach (var (_, d) in csEnemyDefs
        .OrderByDescending(x => csInStage.TryGetValue(x.Def.Id, out var l) ? l.Count : 0)
        .ThenBy(x => x.Def.Id))
    {
        var l = csInStage.TryGetValue(d.Id, out var v) ? v : new List<string>();
        Console.WriteLine($"| {d.Name} | `{d.Id}` | {l.Count} | {(l.Count == 0 ? "**—**" : string.Join(" / ", l))} |");
    }
    Console.WriteLine();

    return;
}
}
