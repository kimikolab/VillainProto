using BattleCore;

// 総当たりシミュレータ。WPF を通さず戦闘ロジックだけを叩く。
// 手動プレイでは見つからない「強すぎる組み合わせ」と「死に駒」を機械的に洗い出す。

int stageIndex = args.Length > 0 && int.TryParse(args[0], out int s) ? s : 1;
string focusId = args.Length > 1 ? args[1] : "";

// compare / dump / layout は docs/ に貼れる Markdown をそのまま吐くので、
// 「対象ステージ」の見出しと stageIndex の解決はこの3モードの分岐を抜けた後で行う。
// （3モードともステージ引数を無視して全ステージを回すため、内容としても誤りになる）

// compare モード: 代表的な編成を全ステージで比較する。
// 総当たりは駒が増えるほど爆発するので、系統ごとの当たり外れはこちらで見る。
if (focusId == "compare")
{
    var builds = CompareBuilds();

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

// layout モード: compare の各編成についてメンバー固定のまま6スロットへの全配置を試し、
// 全ステージ平均勝率で並べる。「この編成をどう置くか」を人手の勘で決めないための道具。
// 編成名が示す狙い（隣接ペア・後列必須など）との突き合わせは人がやる。上位だけでなく
// 現行配置の順位も出すのはそのため。
if (focusId == "layout")
{
    var builds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> stages = EnemyCatalog.Stages;
    const int LayoutSeeds = 50;
    const int TopN = 5;

    // ジョブ表は「編成の並び順 → 配置の辞書式昇順」で逐次構築する。
    // 各ジョブは results[自分の添字] にしか書かないので、回収に同期は要らず、
    // 出力はスレッドのスケジューリングに依存しない（同じ引数なら必ず同じ出力になる）。
    var jobs = new List<(int BuildIdx, int PermIdx, Formation F)>();
    for (int b = 0; b < builds.Length; b++)
    {
        var members = builds[b].F.Occupied().Select(x => x.Def).ToList();
        int permIdx = 0;
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation();
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            jobs.Add((b, permIdx++, f));
        }
    }

    // BattleEngine.Run は seed 決定的な純関数（副作用・外部依存なし）なので配置単位の並列は安全。
    var results = new int[jobs.Count][];
    Parallel.For(0, jobs.Count, i =>
    {
        var wins = new int[stages.Count];
        for (int st = 0; st < stages.Count; st++)
            for (int seed = 0; seed < LayoutSeeds; seed++)
                if (BattleEngine.Run(jobs[i].F, stages[st].Enemy, seed, verbose: false).PlayerWon)
                    wins[st]++;
        results[i] = wins;
    });

    Console.WriteLine("# 配置探索");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 layout` の出力。");
    Console.WriteLine($"compare の各編成をメンバー固定で全配置（5体=720通り / 4体=360通り）に展開し、");
    Console.WriteLine($"全{stages.Count}ステージ × seed 0..{LayoutSeeds - 1} の平均勝率で並べた上位{TopN}件と現行配置。");
    Console.WriteLine($"検証した配置: {jobs.Count:N0} 通り（{(long)jobs.Count * stages.Count * LayoutSeeds:N0} 戦）");

    for (int b = 0; b < builds.Length; b++)
    {
        int bb = b;
        var ranked = Enumerable.Range(0, jobs.Count)
            .Where(i => jobs[i].BuildIdx == bb)
            .OrderByDescending(i => results[i].Sum())
            .ThenBy(i => jobs[i].PermIdx)   // 同点は配置の辞書式で若い方（決定的タイブレーク）
            .ToList();

        Console.WriteLine();
        Console.WriteLine($"## {builds[b].Name}");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 前1/前2/前3 | 中 | 後1/後2 | 平均 |"
            + string.Concat(stages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|--:|---|---|---|--:|" + string.Concat(stages.Select(_ => "---:|")));
        for (int rank = 0; rank < TopN && rank < ranked.Count; rank++)
            Console.WriteLine(LayoutRow($"{rank + 1}", jobs[ranked[rank]].F, results[ranked[rank]], LayoutSeeds));

        int cur = ranked.FindIndex(i => SameFormation(jobs[i].F, builds[bb].F));
        Console.WriteLine(LayoutRow($"現行({cur + 1}位)", jobs[ranked[cur]].F, results[ranked[cur]], LayoutSeeds));
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

EnemyCatalog.Stage stage = EnemyCatalog.Stages[stageIndex];
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

// compare / layout が共有する代表編成。系統ごとの当たり外れを見るための固定リスト。
//
// 配置は layout モード（6枠化後・全配置総当たり）の結果から、編成の狙いを壊さない最良を採った。
// 守った制約: ガルドは前列（庇うは前列でしか発動しない）/ セッキは後列（後備えは後列でしか
// 発動しない）/ セロは原則後列（狙撃化）/ ヒサの隣接で最大HPがカドになること（標的が逸れる）。
static (string Name, Formation F)[] CompareBuilds() => new (string, Formation)[]
{
    // 中衛ボルグの巻き込みが縦隣接の後2ムドを育てる。ボルグ自身も中衛で守られ、セロは後1で狙撃化
    ("速攻 (ボルグ×ムド)",   Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Nel, mid: UnitCatalog.Borg, back1: UnitCatalog.Sero, back2: UnitCatalog.Mudo)),
    // 脆いムグ・ゾトを前で死なせて連鎖を起こす。中衛ゴルムの吸いが隣のゾトを破裂まで運ぶ（layout 1位）
    ("死の連鎖 (リィカ軸)",  Formation.Build(front1: UnitCatalog.Mug, front2: UnitCatalog.Zoto, mid: UnitCatalog.Golm, back1: UnitCatalog.Rica, back2: UnitCatalog.Vel)),
    // スィドは前3の孤立席で毒漏れを消す。ガルドが前1で庇う（layout 1位）
    ("毒 (グザ×ミオ×ラウ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sid, mid: UnitCatalog.Guza, back1: UnitCatalog.Mio, back2: UnitCatalog.Rau)),
    // 支援2枚を後列に下げ、痺れ粉は守られる中衛から撒く（layout 1位）
    ("毒+耐久 (ベニ×トウ)",  Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Guza, mid: UnitCatalog.Tou, back1: UnitCatalog.Mio, back2: UnitCatalog.Beni)),
    // ヒサの隣接はカドだけ（後1↔後2）。ガルド(HP100>カド96)を隣に置くと標的が逸れる（layout 1位）
    ("反撃 (ヒサ×カド)",     Formation.Build(front2: UnitCatalog.Nono, front3: UnitCatalog.Gald, mid: UnitCatalog.Nel, back1: UnitCatalog.Hisa, back2: UnitCatalog.Kado)),
    // ヒサ(前1)の隣接はカドとムドでカドが最大HP。惨禍の中でセロとムドを後列に逃がす（layout 1位）
    ("惨禍×被弾強化",        Formation.Build(front1: UnitCatalog.Hisa, front2: UnitCatalog.Kado, mid: UnitCatalog.Nono, back1: UnitCatalog.Mudo, back2: UnitCatalog.Sero)),
    // 中衛リィカの生贄が隣のカドとゾトを削り、惨禍込みの死の密度で墓守が積む（layout 1位）
    ("惨禍×死の連鎖",        Formation.Build(front2: UnitCatalog.Kado, front3: UnitCatalog.Golm, mid: UnitCatalog.Rica, back1: UnitCatalog.Vel, back2: UnitCatalog.Zoto)),
    // ガルドは前列でないと庇えない。中衛に隠す配置(96.8%)が上だが庇いが死ぬので採らない（layout 5位）
    ("耐久 (ガルド×ノノ)",   Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald, mid: UnitCatalog.Golm, back1: UnitCatalog.Dolga, back2: UnitCatalog.Sero)),
    // 動かないカドを前1に置き、ヒサは後1から深さ隣接でカドだけを指す（layout 1位）
    ("溜め (ガン×ドルガ×カド)", Formation.Build(front1: UnitCatalog.Kado, front2: UnitCatalog.Gald, front3: UnitCatalog.Dolga, mid: UnitCatalog.Gan, back1: UnitCatalog.Hisa)),
    // 狙いはグザの瘴気（味方全体に毒）でムドを育てる形なので位置は不問。ムドは中衛で守る（layout 1位）
    ("毒→被弾強化 (グザ×ムド)", Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Guza, front3: UnitCatalog.Borg, mid: UnitCatalog.Mudo, back1: UnitCatalog.Sero)),
    // ヴィオの吸い上げは全体対象で位置不問。スィドの毒漏れはむしろ燃料なので孤立させない（layout 1位）
    ("澱み喰い (グザ×ヴィオ)", Formation.Build(front1: UnitCatalog.Sid, front3: UnitCatalog.Gald, mid: UnitCatalog.Guza, back1: UnitCatalog.Mio, back2: UnitCatalog.Vio)),
    // バサの入れ替えが全員を動かすので後列ヨミでも育つ。セロは後1で狙撃化（layout 1位）
    ("隊列崩し (バサ×ヨミ×セロ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Basa, mid: UnitCatalog.Gan, back1: UnitCatalog.Sero, back2: UnitCatalog.Yomi)),
    // セロが前3から中のヨミへ逃げ込み、ヨミを前へ突き出す(+22)。セロ後置き型(76%)は狙いが死ぬ（layout 98位）
    ("突き出し (セロ×ヨミ)",  Formation.Build(front1: UnitCatalog.Golm, front2: UnitCatalog.Gald, front3: UnitCatalog.Sero, mid: UnitCatalog.Yomi, back1: UnitCatalog.Nel)),
    // 溜め役3体を敵から遠い後列と中衛へ。カドとクグの前列が受け止める（layout 1位）
    ("溜め改 (クグ×バン×ガン)", Formation.Build(front1: UnitCatalog.Kado, front2: UnitCatalog.Kugu, mid: UnitCatalog.Ban, back1: UnitCatalog.Dolga, back2: UnitCatalog.Gan)),
    // セロの逃亡もバサの入れ替えも全部シオとヨミの燃料になる（layout 1位）
    ("移動改 (バサ×ヨミ×シオ)", Formation.Build(front1: UnitCatalog.Sero, front2: UnitCatalog.Gald, front3: UnitCatalog.Shio, mid: UnitCatalog.Basa, back1: UnitCatalog.Yomi)),
    // 呪詛は全体に漏れるのでウツの位置は不問。ガルドを中衛に隠す配置(99%)は庇いが死ぬので採らない（layout 29位）
    ("逆しま (ネル×ウツ)",   Formation.Build(front2: UnitCatalog.Golm, front3: UnitCatalog.Gald, mid: UnitCatalog.Nel, back1: UnitCatalog.Utsu, back2: UnitCatalog.Sero)),
    // 萎縮も呪詛も全体に効く。ウツだけ守られる中衛に置き、ガルドは前列で庇う（layout 52位）
    ("逆しま改 (クビ×ウツ)", Formation.Build(front1: UnitCatalog.Nel, front2: UnitCatalog.Golm, front3: UnitCatalog.Gald, mid: UnitCatalog.Utsu, back1: UnitCatalog.Kubi)),
    // 旧配置がそのまま全配置1位。ヒサの隣接（カド・ネル）で最大HPはカド（layout 1位）
    ("反撃改 (ドハ×カド)",   Formation.Build(front1: UnitCatalog.Hisa, front2: UnitCatalog.Kado, front3: UnitCatalog.Doha, mid: UnitCatalog.Nono, back1: UnitCatalog.Nel)),
    // ヒサ(前3)の横はカドだけ＝標的が確実にカドへ。溜め役のガンとドハは奥（layout 1位）
    ("反撃改2 (ガン×カド)",  Formation.Build(front1: UnitCatalog.Ban, front2: UnitCatalog.Kado, front3: UnitCatalog.Hisa, mid: UnitCatalog.Gan, back1: UnitCatalog.Doha)),
    // ヒサ(前1)の隣接は後1のカドだけ。ガルドは前3で庇い、ハギは後2で延命（layout 4位、Gald前列の最良）
    ("反撃改3 (カド×ハギ)",  Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Gald, mid: UnitCatalog.Gan, back1: UnitCatalog.Kado, back2: UnitCatalog.Hagi)),
    // ハギは守られる中衛から追い打つ（位置不問）。前列3枚が受け、ミオは後1（layout: ガルド前列の最良）
    ("追撃×毒 (ハギ×グザ)",  Formation.Build(front1: UnitCatalog.Guza, front2: UnitCatalog.Gald, front3: UnitCatalog.Golm, mid: UnitCatalog.Hagi, back1: UnitCatalog.Mio)),
    // 死の連鎖にハギを足した形。ムグは後2で最後に死んで胞子を残す（layout 1位）
    ("追撃×死 (ハギ×リィカ)", Formation.Build(front1: UnitCatalog.Hagi, front2: UnitCatalog.Zoto, mid: UnitCatalog.Golm, back1: UnitCatalog.Rica, back2: UnitCatalog.Mug)),
    // 空き枠で孤立を作る散開の幾何。ササ(前1)とガルド(前3)が孤立して-35%を受ける（layout 1位）
    ("移動改2 (ササ×ヨミ)",  Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Gald, mid: UnitCatalog.Basa, back2: UnitCatalog.Yomi)),
    // 同じく孤立の幾何。ドハの分かちは全体に効くので中衛でよい（layout 1位）
    ("散開耐久 (ササ×ドハ)", Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Gald, mid: UnitCatalog.Doha, back2: UnitCatalog.Dolga)),
    // セッキは後列でないと庇えない。中衛セッキの探索1位は特性が死ぬので採らない（layout 3位）
    ("死の連鎖+後備え", Formation.Build(front1: UnitCatalog.Zoto, front2: UnitCatalog.Golm, mid: UnitCatalog.Rica, back1: UnitCatalog.Sekki, back2: UnitCatalog.Vel)),
    // セロとセッキが後列に並ぶ。セッキが貫き以外の後列狙いを45%で肩代わりして狙撃を守る（layout 2位）
    ("後衛特化+後備え", Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Golm, mid: UnitCatalog.Dolga, back1: UnitCatalog.Sekki, back2: UnitCatalog.Sero)),
    // ウツとセッキが後列、クビは守られる中衛。萎縮の被ダメ減とセッキの肩代わりが重なる（layout 1位）
    ("逆しま+後備え",   Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Golm, mid: UnitCatalog.Kubi, back1: UnitCatalog.Utsu, back2: UnitCatalog.Sekki))
};

// メンバーをスロット 0..5 へ重複なく割り当てる全順列を、
// 割り当てタプルの辞書式昇順で列挙する（各深さでスロットを昇順に試すため）。
// layout モードの決定性（同点タイブレーク＝列挙順の若い方）はこの順序に依存している。
static IEnumerable<int[]> SlotAssignments(int memberCount)
{
    var assign = new int[memberCount];
    var used = new bool[FormationRules.TotalSlots];
    return Rec(0);

    IEnumerable<int[]> Rec(int depth)
    {
        if (depth == memberCount) { yield return (int[])assign.Clone(); yield break; }
        for (int slot = 0; slot < FormationRules.TotalSlots; slot++)
        {
            if (used[slot]) continue;
            used[slot] = true;
            assign[depth] = slot;
            foreach (int[] a in Rec(depth + 1)) yield return a;
            used[slot] = false;
        }
    }
}

static bool SameFormation(Formation a, Formation b)
{
    for (int i = 0; i < FormationRules.TotalSlots; i++)
        if (!ReferenceEquals(a[i], b[i])) return false;
    return true;
}

static string LayoutRow(string rank, Formation f, int[] wins, int seeds)
{
    static string N(UnitDef? d) => d?.Name ?? "−";
    double avg = wins.Sum() * 100.0 / (wins.Length * seeds);
    string cells = string.Concat(wins.Select(w => $" {w * 100.0 / seeds:F1}% |"));
    return $"| {rank} | {N(f[0])}/{N(f[1])}/{N(f[2])} | {N(f[3])} | {N(f[4])}/{N(f[5])} | {avg:F1}% |{cells}";
}
