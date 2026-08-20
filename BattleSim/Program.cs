using BattleCore;

// 総当たりシミュレータ。WPF を通さず戦闘ロジックだけを叩く。
// 手動プレイでは見つからない「強すぎる組み合わせ」と「死に駒」を機械的に洗い出す。

int stageIndex = args.Length > 0 && int.TryParse(args[0], out int s) ? s : 1;
string focusId = args.Length > 1 ? args[1] : "";
EnemyCatalog.Stage stage = EnemyCatalog.Stages[stageIndex];

// compare / dump は docs/ に貼る Markdown をそのまま吐くので、
// 「対象ステージ」の見出しはこの2モードの分岐を抜けた後で出す。
// （compare はステージ引数を無視して全ステージを回すため、内容としても誤りになる）

// compare モード: 代表的な編成を全ステージで比較する。
// 総当たりは駒が増えるほど爆発するので、系統ごとの当たり外れはこちらで見る。
if (focusId == "compare")
{
    var builds = new (string Name, Formation F)[]
    {
        ("速攻 (ボルグ×ムド)",   Formation.Build(front1: UnitCatalog.Borg, front2: UnitCatalog.Mudo, front3: UnitCatalog.Gald, mid: UnitCatalog.Sero, back1: UnitCatalog.Nel)),
        ("死の連鎖 (リィカ軸)",  Formation.Build(front1: UnitCatalog.Golm, front2: UnitCatalog.Zoto, front3: UnitCatalog.Mug, mid: UnitCatalog.Vel, back1: UnitCatalog.Rica)),
        ("毒 (グザ×ミオ×ラウ)", Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Guza, front3: UnitCatalog.Sid, mid: UnitCatalog.Mio, back1: UnitCatalog.Rau)),
        ("毒+耐久 (ベニ×トウ)",  Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Guza, front3: UnitCatalog.Tou, mid: UnitCatalog.Mio, back1: UnitCatalog.Beni)),
        ("反撃 (ヒサ×カド)",     Formation.Build(front1: UnitCatalog.Hisa, front2: UnitCatalog.Kado, front3: UnitCatalog.Gald, mid: UnitCatalog.Nono, back1: UnitCatalog.Nel)),
        ("惨禍×被弾強化",        Formation.Build(front1: UnitCatalog.Kado, front2: UnitCatalog.Mudo, front3: UnitCatalog.Hisa, mid: UnitCatalog.Nono, back1: UnitCatalog.Sero)),
        ("惨禍×死の連鎖",        Formation.Build(front1: UnitCatalog.Kado, front2: UnitCatalog.Zoto, front3: UnitCatalog.Golm, mid: UnitCatalog.Vel, back1: UnitCatalog.Rica)),
        ("耐久 (ガルド×ノノ)",   Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Golm, front3: UnitCatalog.Dolga, mid: UnitCatalog.Nono, back1: UnitCatalog.Sero)),
        ("溜め (ガン×ドルガ×カド)", Formation.Build(front1: UnitCatalog.Kado, front2: UnitCatalog.Dolga, front3: UnitCatalog.Gald, mid: UnitCatalog.Gan, back1: UnitCatalog.Hisa)),
        ("毒→被弾強化 (グザ×ムド)", Formation.Build(front1: UnitCatalog.Borg, front2: UnitCatalog.Mudo, front3: UnitCatalog.Gald, mid: UnitCatalog.Guza, back1: UnitCatalog.Sero)),
        ("澱み喰い (グザ×ヴィオ)", Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Vio, front3: UnitCatalog.Guza, mid: UnitCatalog.Sid, back1: UnitCatalog.Mio)),
        ("隊列崩し (バサ×ヨミ×セロ)", Formation.Build(front1: UnitCatalog.Yomi, front2: UnitCatalog.Gald, front3: UnitCatalog.Basa, mid: UnitCatalog.Sero, back1: UnitCatalog.Gan)),
        ("突き出し (セロ×ヨミ)",  Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Sero, front3: UnitCatalog.Golm, mid: UnitCatalog.Yomi, back1: UnitCatalog.Nel)),
        ("溜め改 (クグ×バン×ガン)", Formation.Build(front1: UnitCatalog.Dolga, front2: UnitCatalog.Kado, front3: UnitCatalog.Ban, mid: UnitCatalog.Gan, back1: UnitCatalog.Kugu)),
        ("移動改 (バサ×ヨミ×シオ)", Formation.Build(front1: UnitCatalog.Yomi, front2: UnitCatalog.Gald, front3: UnitCatalog.Basa, mid: UnitCatalog.Shio, back1: UnitCatalog.Sero)),
        ("逆しま (ネル×ウツ)",   Formation.Build(front1: UnitCatalog.Utsu, front2: UnitCatalog.Gald, front3: UnitCatalog.Golm, mid: UnitCatalog.Nel, back1: UnitCatalog.Sero)),
        ("逆しま改 (クビ×ウツ)", Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Utsu, front3: UnitCatalog.Golm, mid: UnitCatalog.Nel, back1: UnitCatalog.Kubi)),
        ("反撃改 (ドハ×カド)",   Formation.Build(front1: UnitCatalog.Hisa, front2: UnitCatalog.Kado, front3: UnitCatalog.Doha, mid: UnitCatalog.Nono, back1: UnitCatalog.Nel)),
        ("反撃改2 (ガン×カド)",  Formation.Build(front1: UnitCatalog.Kado, front2: UnitCatalog.Hisa, front3: UnitCatalog.Ban, mid: UnitCatalog.Gan, back1: UnitCatalog.Doha)),
        ("反撃改3 (カド×ハギ)",  Formation.Build(front1: UnitCatalog.Kado, front2: UnitCatalog.Hisa, front3: UnitCatalog.Gald, mid: UnitCatalog.Gan, back1: UnitCatalog.Hagi)),
        ("追撃×毒 (ハギ×グザ)",  Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Guza, front3: UnitCatalog.Golm, mid: UnitCatalog.Mio, back1: UnitCatalog.Hagi)),
        ("追撃×死 (ハギ×リィカ)", Formation.Build(front1: UnitCatalog.Golm, front2: UnitCatalog.Zoto, front3: UnitCatalog.Mug, mid: UnitCatalog.Rica, back1: UnitCatalog.Hagi)),
        ("移動改2 (ササ×ヨミ)",  Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald, mid: UnitCatalog.Basa, back1: UnitCatalog.Sasa)),
        ("散開耐久 (ササ×ドハ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, mid: UnitCatalog.Doha, back1: UnitCatalog.Sasa)),
        ("死の連鎖+後備え", Formation.Build(front1: UnitCatalog.Golm, front2: UnitCatalog.Zoto, front3: UnitCatalog.Vel, mid: UnitCatalog.Sekki, back1: UnitCatalog.Rica)),
        ("後衛特化+後備え", Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Golm, front3: UnitCatalog.Dolga, mid: UnitCatalog.Sekki, back1: UnitCatalog.Sero)),
        ("逆しま+後備え",   Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Golm, front3: UnitCatalog.Kubi, mid: UnitCatalog.Sekki, back1: UnitCatalog.Utsu))
    };

    const int CompareSeeds = 200;

    // そのまま docs/balance.md になるので、見出しと注意書きもここで吐く。
    // 手で足した文章はリダイレクトのたびに消えるため、文書の体裁ごと生成物にする。
    Console.WriteLine("# 勝率表");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 compare > docs/balance.md` の出力。手で編集しない。");
    Console.WriteLine($"代表編成 × 全ステージ、seed 0..{CompareSeeds - 1} の {CompareSeeds} 試行。");
    Console.WriteLine();

    // 列はステージ数から作るので、ステージを足しても勝手に増える。
    // 固定幅で揃えるのはやめた。全角の編成名では桁が合わない（`,-24` は表示幅ではなく文字数を数える）。
    Console.WriteLine("| 編成 |" + string.Concat(EnemyCatalog.Stages.Select((st, i) => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(EnemyCatalog.Stages.Select(_ => "---:|")));
    foreach (var (name, f) in builds)
    {
        var cells = new List<string>();
        foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
        {
            int wins = 0;
            for (int seed = 0; seed < CompareSeeds; seed++)
                if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
            cells.Add($" {wins * 100.0 / CompareSeeds:F1}% |");
        }
        Console.WriteLine($"| {name} |" + string.Concat(cells));
    }
    return;
}

// dump モード: カタログから資料を吐く。手書きの一覧とコードがずれないようにするため。
if (focusId == "dump")
{
    static string Pat(AttackPattern p) => p switch
    {
        AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き",
        AttackPattern.All => "全体", _ => "単体"
    };
    Console.WriteLine("# ユニット・特性・ステージ一覧");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 dump > docs/units.md` の出力。手で編集しない。");
    Console.WriteLine();
    Console.WriteLine("## ユニット");
    Console.WriteLine();
    Console.WriteLine("| 名前 | HP | 攻 | 速 | 型 | プラス | マイナス | 由来 |");
    Console.WriteLine("|---|---:|---:|---:|---|---|---|---|");
    foreach (UnitDef u in UnitCatalog.All.Where(u => u.Id != "spore"))
        Console.WriteLine($"| **{u.Name}** | {u.MaxHp} | {u.Attack} | {u.Speed} | {Pat(u.Pattern)} | {u.PlusText} | {u.MinusText} | {u.Flavor} |");

    Console.WriteLine();
    Console.WriteLine("## 特性");
    Console.WriteLine();
    Console.WriteLine("| 特性 | 保持者 |");
    Console.WriteLine("|---|---|");
    foreach (TraitId id in Enum.GetValues<TraitId>())
    {
        var owners = UnitCatalog.All.Where(u => u.Traits.Contains(id)).Select(u => u.Name).ToList();
        Console.WriteLine($"| `{id}` | {(owners.Count == 0 ? "-" : string.Join("、", owners))} |");
    }

    Console.WriteLine();
    Console.WriteLine("## ステージ");
    Console.WriteLine();
    foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
    {
        var e = st.Enemy.Occupied().Select(x => $"{x.Def.Name}(HP{x.Def.MaxHp}/攻{x.Def.Attack}/{Pat(x.Def.Pattern)})");
        Console.WriteLine($"- **{st.Name}**: {string.Join("、", e)}");
    }
    return;
}

Console.WriteLine($"対象ステージ: {stage.Name}\n");

// demo モード: 特定の編成のログだけを見る
if (focusId == "demo")
{
    var build = Formation.Build(
        front1: UnitCatalog.Kado,   // 反撃。範囲で返す
        front2: UnitCatalog.Hisa,   // 標的を付けてカドに殴らせる
        front3: UnitCatalog.Gald,   // 壁
        mid:    UnitCatalog.Gan,    // 号令。動かないカドの攻撃を積む
        back1:  UnitCatalog.Hagi    // 追い打ち。誰かが倒すと割り込む
    );
    BattleResult demo = BattleEngine.Run(build, stage.Enemy, seed: 7, verbose: true);
    foreach (LogLine line in demo.Log) Console.WriteLine(line);
    Console.WriteLine($"結果: {(demo.PlayerWon ? "勝利" : "敗北")} / {demo.Turns}ターン");
    return;
}

const int SeedsPerFormation = 20;

var units = UnitCatalog.All.Where(u => u.Id != "spore").ToList();
var records = new List<(double WinRate, UnitDef?[] Slots)>();

foreach (var combo in Combinations(units, 4))
{
    if (focusId.Length > 0 && combo.All(u => u.Id != focusId)) continue;

    foreach (var slots in SlotPermutations(combo))
    {
        var f = new Formation();
        for (int i = 0; i < FormationRules.TotalSlots; i++) f[i] = slots[i];

        int wins = 0;
        for (int seed = 0; seed < SeedsPerFormation; seed++)
            if (BattleEngine.Run(f, stage.Enemy, seed, verbose: false).PlayerWon)
                wins++;

        records.Add((wins / (double)SeedsPerFormation, slots));
    }
}

Console.WriteLine($"検証した編成: {records.Count} 通り × {SeedsPerFormation} 回\n");

Console.WriteLine("--- 勝率の高い編成 TOP 10 ---");
foreach (var r in records.OrderByDescending(r => r.WinRate).Take(10))
    Console.WriteLine($"  {r.WinRate,6:P0}  {Describe(r.Slots)}");

Console.WriteLine("\n--- ユニット別 平均勝率 ---");
double overall = records.Average(r => r.WinRate);
foreach (UnitDef u in units)
{
    var with = records.Where(r => r.Slots.Any(x => x?.Id == u.Id)).ToList();
    if (with.Count == 0) continue;
    double avg = with.Average(r => r.WinRate);
    double best = with.Max(r => r.WinRate);
    string flag = avg < overall - 0.05 ? "  ← 平均以下" : "";
    Console.WriteLine($"  {u.Name,-16} 平均 {avg,6:P1} / 最高 {best,6:P0}{flag}");
}
Console.WriteLine($"  （全体平均 {overall:P1}）");

Console.WriteLine("\n--- ペア相性 TOP 10 ---");
var pairs = new List<(string Key, double Avg, int N)>();
for (int i = 0; i < units.Count; i++)
for (int j = i + 1; j < units.Count; j++)
{
    var with = records.Where(r =>
        r.Slots.Any(x => x?.Id == units[i].Id) &&
        r.Slots.Any(x => x?.Id == units[j].Id)).ToList();
    if (with.Count == 0) continue;
    pairs.Add(($"{units[i].Name} + {units[j].Name}", with.Average(r => r.WinRate), with.Count));
}
foreach (var p in pairs.OrderByDescending(p => p.Avg).Take(10))
    Console.WriteLine($"  {p.Avg,6:P1}  {p.Key}");

Console.WriteLine("\n--- ペア相性 WORST 5 ---");
foreach (var p in pairs.OrderBy(p => p.Avg).Take(5))
    Console.WriteLine($"  {p.Avg,6:P1}  {p.Key}");

return;

static string Describe(UnitDef?[] slots)
{
    string Row(Row r) => string.Join("/", FormationRules.SlotsOfRow(r)
        .Select(i => slots[i]?.Name ?? "空"));
    return $"前[{Row(BattleCore.Row.Front)}] 中[{Row(BattleCore.Row.Mid)}] 後[{Row(BattleCore.Row.Back)}]";
}

static IEnumerable<List<T>> Combinations<T>(IReadOnlyList<T> source, int k)
{
    var idx = new int[k];
    for (int i = 0; i < k; i++) idx[i] = i;
    while (true)
    {
        yield return idx.Select(i => source[i]).ToList();
        int pos = k - 1;
        while (pos >= 0 && idx[pos] == source.Count - k + pos) pos--;
        if (pos < 0) yield break;
        idx[pos]++;
        for (int i = pos + 1; i < k; i++) idx[i] = idx[i - 1] + 1;
    }
}

static IEnumerable<UnitDef?[]> SlotPermutations(List<UnitDef> members)
{
    int blanks = FormationRules.TotalSlots - members.Count;

    foreach (var order in Permute(members))
        foreach (var empty in Combinations(Enumerable.Range(0, FormationRules.TotalSlots).ToList(), blanks))
        {
            var skip = empty.ToHashSet();
            var slots = new UnitDef?[FormationRules.TotalSlots];
            int m = 0;
            for (int i = 0; i < FormationRules.TotalSlots; i++)
                slots[i] = skip.Contains(i) ? null : order[m++];
            yield return slots;
        }
}

static IEnumerable<List<T>> Permute<T>(List<T> items)
{
    if (items.Count <= 1) { yield return new List<T>(items); yield break; }
    for (int i = 0; i < items.Count; i++)
    {
        var rest = new List<T>(items);
        T head = rest[i];
        rest.RemoveAt(i);
        foreach (var p in Permute(rest)) { p.Insert(0, head); yield return p; }
    }
}
