using BattleCore;
using static Common;

// =====================================================================================
// 第139期 段0 —— **トップレベル文をやめて、本体を大きなスタックのスレッドで走らせる**。
//
// 以前はここから 68,000 行がトップレベル文だった。C# はそれを1つの `<Main>$` に畳むので、
// **全モードの局所変数が1つのスタックフレームに同居する**——実測では
// `<Main>$` のフレームだけで既定 1MB のスタックをほぼ食い尽くしていて、
// **`Enumerable.Sum` の1呼び出しでオーバーフローする**ところまで来ていた。
//
// 第138期は同じ症状を「`BattleEngine.Run` の引数を2本増やしたから」と読んだが、
// **引数は最後の一押しで、原因ではない**——第139期に `eaec7e6` をクリーンな
// `git worketree` に出して測ると、**引数を1本も足していない HEAD で `compare` が落ちる**
// （23 行目 `反撃改3 (カド×ハギ)`・exit 127・`docs/balance.md` が途中で壊れる）。
// `compare quality` と `chain` は `<Main>$` の中の集計そのもので落ちていた。
//
// **中身は1行も動かしていない。** `class` と `Body(string[] args)` で囲っただけで、
// 局所変数もローカル関数も並びもそのまま（トップレベル文はもともと `<Main>$` の本体なので、
// **生成されるコードはほぼ同じ**）。**盤面・乱数・順序には1ビットも触っていない。**
//
// **採らなかった形**: `compare` の分岐だけをローカル関数に切り出して渡す版は、
// **本体が参照するトップレベルの局所変数がすべて表示クラスへ巻き上げられ、
// Roslyn がメモリ 52GB を掴んでビルドが終わらなくなった**（実測）。
// **囲うなら全部**——部分的に囲うとクロージャになる。
//
// **構造的な直し（`Run` の 49 本の引数を1つの束に畳む・モードを別クラスへ割る）はこの期ではやらない。**
// 呼び出し口が数百あり、「計測器と測定対象を同時に動かさない」に正面から反する。
// =====================================================================================
internal static class Prog
{
    internal static void Body(string[] args)
    {

// 総当たりシミュレータ。WPF を通さず戦闘ロジックだけを叩く。
// 手動プレイでは見つからない「強すぎる組み合わせ」と「死に駒」を機械的に洗い出す。

int stageIndex = args.Length > 0 && int.TryParse(args[0], out int s) ? s : 1;
string focusId = args.Length > 1 ? args[1] : "";

// compare / dump / layout は docs/ に貼れる Markdown をそのまま吐くので、
// 「対象ステージ」の見出しと stageIndex の解決はこの3モードの分岐を抜けた後で行う。
// （3モードともステージ引数を無視して全ステージを回すため、内容としても誤りになる）

// audit モード: docs/ の生成物が現行の編成数と整合しているかを判定する（第55期）。
//
// 生成物は「作った時点の編成数」で固まるので、CompareBuilds() に行を足すたびに
// 測り直さないと静かに腐る。第54期に docs/chain.md が 35 行（当時の現行は 56 行）と
// 判明したのが発端で、20期以上前の編成表を根拠に判断しかけた。
//
// **戦闘を1回も回さない。docs/ に何も書かない**（生成物を増やすと腐るものが増える）。
// 判定は「現行の編成名がその生成物に1つ残らず現れるか」の1本だけ——行数を直に比べると
// 表の書式を変えるたびに閾値が嘘になるが、名前の有無は書式に依存しない。
//
//     dotnet run --project BattleSim -c Release 0 audit
if (focusId == "audit")
{
    string[] names = CompareBuilds().Select(b => b.Name).ToArray();

    // リポジトリ直下から実行するのが既定だが、どこから叩かれても docs/ を見つけられるようにする。
    string? root = Directory.GetCurrentDirectory();
    while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
        root = Path.GetDirectoryName(root);
    if (root == null)
    {
        Console.WriteLine("docs/ が見つからない（リポジトリの外から実行している）。");
        return;
    }
    string docs = Path.Combine(root, "docs");

    // Depends: 行数が編成数に依存するか。Sections: `## ` の見出しが編成名か。
    var targets = new (string File, string Cmd, bool Depends, bool Sections)[]
    {
        ("balance.md",  "compare", true,  false),
        ("quality.md",  "compare quality", true, false),   // 第126期 段1
        ("units.md",    "dump",    false, false),
        ("chain.md",    "chain",   true,  false),
        ("ablation.md", "ablate",  true,  true),
        ("pulse.md",    "pulse",   true,  true),
        ("engage.md",   "engage",  true,  false),
        ("layout.md",   "layout",  true,  true),
        ("reseat.md",   "reseat",  true,  true),
    };

    Console.WriteLine($"現行の編成数: {names.Length}");
    Console.WriteLine();
    Console.WriteLine("| ファイル | 生成コマンド | 行数 | 編成数依存 | 現れた編成 | 節(編成/他) | 判定 |");
    Console.WriteLine("|---|---|--:|:-:|--:|--:|:-:|");

    var stale = new List<(string File, string[] Missing)>();
    foreach ((string file, string cmd, bool depends, bool sections) in targets)
    {
        string path = Path.Combine(docs, file);
        if (!File.Exists(path))
        {
            Console.WriteLine($"| `{file}` | `{cmd}` | — | {(depends ? "○" : "×")} | — | — | **欠落** |");
            stale.Add((file, names));
            continue;
        }

        string[] lines = File.ReadAllLines(path);
        string text = string.Join("\n", lines);
        string[] missing = depends ? names.Where(n => !text.Contains(n)).ToArray() : Array.Empty<string>();

        string secCol = "—";
        if (sections)
        {
            var heads = lines.Where(l => l.StartsWith("## ")).Select(l => l.Substring(3).Trim()).ToArray();
            // ablate は見出しに「（フル編成 43.9%）」を足すので、完全一致では引けない。
            int known = heads.Count(h => names.Any(n => h.StartsWith(n)));
            secCol = $"{known}/{heads.Length - known}";
        }

        string hit = depends ? $"{names.Length - missing.Length}/{names.Length}" : "—";
        Console.WriteLine($"| `{file}` | `{cmd}` | {lines.Length} | {(depends ? "○" : "×")} | {hit} | {secCol} "
                          + $"| {(missing.Length == 0 ? "OK" : "**ずれ**")} |");
        if (missing.Length > 0) stale.Add((file, missing));
    }

    Console.WriteLine();
    Console.WriteLine($"ずれているファイル {stale.Count} 件");
    foreach ((string file, string[] missing) in stale)
    {
        Console.WriteLine();
        Console.WriteLine($"## {file} — {missing.Length} 編成が現れない");
        foreach (string n in missing) Console.WriteLine($"- {n}");
    }
    return;
}

if (focusId == "derive") { DeriveDiag.Run(args, stageIndex); return; }

if (focusId == "curse") { CurseDiag.Run(args, stageIndex); return; }

if (focusId == "hex") { HexDiag.Run(args, stageIndex); return; }

if (focusId == "encore") { EncoreDiag.Run(args, stageIndex); return; }

if (focusId == "betray") { BetrayDiag.Run(args, stageIndex); return; }

if (focusId == "pulse")
{
    var all = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> stages = EnemyCatalog.Stages;
    const int PulseSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    Console.WriteLine("# 活動量");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 pulse > docs/pulse.md` の出力。手で編集しない。");
    Console.WriteLine($"代表編成 × 全ステージ、seed 0..{PulseSeeds - 1}。数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("**`振/T` と `干渉/T` のズレが体験の密度を測っている。** どちらも1ターンあたりの回数。");
    Console.WriteLine();
    Console.WriteLine("- `振/T` は攻撃を振った回数（`PerformAttack` を通った回数）");
    Console.WriteLine("- `干渉/T` は実際にダメージを通した回数。攻撃・反撃・破裂・毒のどれでも、");
    Console.WriteLine("  その駒が起点になって盤面が動いた回数");
    Console.WriteLine();
    Console.WriteLine("| 形 | 読み方 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 振 ≒ 干渉 ≒ 1.0 | 自分の手番で殴るだけ。数値であって出来事ではない |");
    Console.WriteLine("| 振 ≒ 0 / 干渉 大 | **反応型。** 手番を持たず、起きたことに反応して盤面を動かす |");
    Console.WriteLine("| 振 大 / 干渉 ≒ 0 | **空振り。** 毎ターン振っているのに何も起きていない |");
    Console.WriteLine("| 振 ≒ 0 / 干渉 ≒ 0 | 置物。発火条件が満たされていない |");
    Console.WriteLine();
    Console.WriteLine("分母は**戦闘の全ターン数**（その駒が生きていたターン数ではない）ので、");
    Console.WriteLine("早く落ちる駒は下がる。`落ちた` 列と合わせて読む。");
    Console.WriteLine();
    Console.WriteLine("> **`干渉 0` は「価値が無い」ではない。** 呪詛（ネル）・萎縮（クビ）・庇い（ガルド）は");
    Console.WriteLine("> ダメージを経由せずに盤面を変えるので、この列には最初から出ない。");
    Console.WriteLine("> ここで測れるのは**体験の密度**であって貢献度ではない。");
    Console.WriteLine("> 貢献度は `ablate`（抜いたときの勝率差）の側で見ること。");
    Console.WriteLine("> この表だけを見て駒を消すと、静かに効いている駒から先に消える。");
    Console.WriteLine();
    Console.WriteLine("`与ダメ(味)` は味方に与えたダメージ。破裂・生贄・吸いはここに出る。");
    Console.WriteLine("敵味方を混ぜて数えると、**味方を削ることで仕事をする駒が出力の大きい優等生に見える**。");
    Console.WriteLine("`被(味)` は受けたダメージのうち味方由来のぶん。ここが `被ダメ` の過半を占める駒は、");
    Console.WriteLine("敵ではなく編成に殺されている。");

    // === 第140期 —— 集計だけを行ごとに並列化した。印字は従来どおり直列に `targets` の順 ===
    //
    // 集計の辞書（`sum`）は行の私物で、行をまたいで触らない。
    // 表は `formation.Occupied()` の順に引くので、辞書の列挙順には依存しない。
    var puSum = new Dictionary<string, UnitTally>[targets.Length];
    var puBattles = new int[targets.Length];
    var puTurns = new int[targets.Length];
    Parallel.For(0, targets.Length, ti =>
    {
        // 駒ごとに全戦闘の集計を足し込む。Def.Id で引くので、胞子のような増援もまとまる。
        Formation pf = targets[ti].F;
        var s = new Dictionary<string, UnitTally>();
        int b = 0, tt = 0;
        foreach (EnemyCatalog.Stage st in stages)
            for (int seed = 0; seed < PulseSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(pf, st.Enemy, seed, verbose: false);
                b++;
                tt += r.Turns;
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (!s.TryGetValue(id, out UnitTally? acc)) s[id] = acc = new UnitTally();
                    acc.Add(t);
                }
            }
        puSum[ti] = s; puBattles[ti] = b; puTurns[ti] = tt;
    });

    for (int ti = 0; ti < targets.Length; ti++)
    {
        (string name, Formation formation) = targets[ti];
        var sum = puSum[ti];
        int battles = puBattles[ti], totalTurns = puTurns[ti];

        Console.WriteLine();
        Console.WriteLine($"## {name}");
        Console.WriteLine();
        Console.WriteLine($"{battles} 戦 / 平均 {(double)totalTurns / battles:F1} ターン");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 振/T | 干渉/T | 与ダメ(敵) | 与ダメ(味) | 被ダメ | 被(味) | 撃破 | 落ちた |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");

        // 味方の駒だけを、編成の並び順で出す。敵は編成ごとに変わらないので混ぜない。
        foreach ((int _, UnitDef def) in formation.Occupied())
        {
            UnitTally t = sum.TryGetValue(def.Id, out UnitTally? x) ? x : new UnitTally();
            double swings = totalTurns == 0 ? 0 : (double)t.Attacks / totalTurns;
            double acts = totalTurns == 0 ? 0 : (double)t.Interventions / totalTurns;
            Console.WriteLine(
                $"| {def.Name} | {swings:F2} | {acts:F2} | {(double)t.DamageToEnemy / battles:F0} "
                + $"| {(double)t.DamageToAlly / battles:F0} | {(double)t.DamageTaken / battles:F0} "
                + $"| {(double)t.TakenFromAlly / battles:F0} | {(double)t.Kills / battles:F2} "
                + $"| {(double)t.Deaths / battles:F2} |");
        }
        Console.Out.Flush();
    }
    return;
}

// route モード: 自傷の燃料が変換器まで届く配置は、勝率で競争力を持つか（第19期）。
//
// 「置き去り×被弾強化」の採用配置（reseat 1位）では、ナラの削りがムド（被弾強化）に
// 届く前にゴルムの巨躯へ 90% 吸われる。ApplyDamage の巨躯の分岐は
// **DepthOf(壁の列) < DepthOf(標的の列)** を満たす壁だけを働かせるので、
// ムドを前列へ上げてゴルムと同じ列に並べれば被覆から外れる（同じ列は守らない）。
// 巨躯は庇う・分かちと違って**肩代わりの見返りが無い**（ColossusTrait は Percent だけ）ので、
// 吸われた燃料はどの変換器にも届かず消える。
//
// **メンバーは固定で、動かすのは席だけ。** どの変種もカドを中央に残すので、
// 採用時の +48.5pt の主因（棘守りの反応先が5枠になる件）は全変種で共通＝変数から外れる。
//
// **CompareBuilds() を触らない**（変種はここでローカルに組む。gradient / aim と同じ扱い）。
// docs/ の差分ゼロが受け入れ条件なので、この診断は標準出力で読むだけで docs/ には置かない。
//
//     dotnet run --project BattleSim -c Release 0 route
if (focusId == "route")
{
    IReadOnlyList<EnemyCatalog.Stage> routeStages = EnemyCatalog.Stages;
    const int RouteSeeds = 200;   // compare / pulse と同じ

    var variants = new (string Name, string Note, Formation F)[]
    {
        ("V0 採用済み", "ゴルム前1。ムドは後1で被覆下",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nara,
                            center: UnitCatalog.Kado, back1: UnitCatalog.Mudo, back3: UnitCatalog.Vel)),
        ("V1 ムドを前3へ", "ゴルムと同列。同じ列は守らないので削りが満額届く",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Mudo,
                            center: UnitCatalog.Kado, back1: UnitCatalog.Nara, back3: UnitCatalog.Vel)),
        ("V2 ムドを前1へ", "V1 の前1/前3 入れ替え。席バイアスの確認",
            Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Golm,
                            center: UnitCatalog.Kado, back1: UnitCatalog.Nara, back3: UnitCatalog.Vel)),
        ("V3 ゴルムを後1へ", "巨躯の被覆ゼロ。前列の壁も消えるので上限側の参考値",
            Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Nara,
                            center: UnitCatalog.Kado, back1: UnitCatalog.Golm, back3: UnitCatalog.Vel)),
        // V3 のムドとヴェルを入れ替えただけ。**巨躯は同じ列を守らない**ので、
        // ゴルム後1・ムド後3 でも被覆はゼロのまま——V3 と V4 の差は「ムドが前列にいるか」だけになる。
        // V0〜V3 では「被覆から出ること」と「前列に晒されること」が同じ操作に潰れていて、
        // 勝率差のどこまでが燃料の経路でどこからが露出かが決まらない。この1本がそれを割る。
        ("V4 ムドを後3へ", "V3 のムド↔ヴェル。被覆ゼロのまま、ムドだけ後列に戻す",
            Formation.Build(front1: UnitCatalog.Vel, front3: UnitCatalog.Nara,
                            center: UnitCatalog.Kado, back1: UnitCatalog.Golm, back3: UnitCatalog.Mudo)),
    };

    Console.WriteLine("# 自傷の燃料は変換器まで届くか（route）");
    Console.WriteLine();
    Console.WriteLine($"「置き去り×被弾強化」のメンバー固定・席だけを振った4変種 × 全ステージ、seed 0..{RouteSeeds - 1}。");
    Console.WriteLine("数字は**1戦あたりの平均**（pulse と同じ規約）。診断用なので docs/ には置かない。");
    Console.WriteLine();
    Console.WriteLine("**先に `ムド 被(味)` を見ること。** V0 と V1 でここが跳ねていなければ");
    Console.WriteLine("変種そのものが効いていない（巨躯の判定の読み違い）ので、勝率を読む意味がない。");
    Console.WriteLine();
    Console.WriteLine("**交絡**: V1〜V3 ではナラが後列へ移るので、巨躯や敵の標的選択の都合で生存が伸びうる。");
    Console.WriteLine("ナラの生存ターン数はそのまま効果の総量なので `ナラ 最終T` を併記してある。");

    var rows = new List<(string Name, string Note, double Avg, double[] PerStage,
                         double NaraToAlly, double MudoTakenAlly, double MudoTaken, double MudoDmg,
                         double MudoDeaths, double GolmTakenAlly, double GolmDeaths,
                         double NaraLast, double Turns)>();

    foreach (var (vname, note, f) in variants)
    {
        var sum = new Dictionary<string, UnitTally>();
        var perStage = new double[routeStages.Count];
        long turnSum = 0, naraLastSum = 0;
        int battles = 0;

        for (int si = 0; si < routeStages.Count; si++)
        {
            int wins = 0;
            for (int seed = 0; seed < RouteSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, routeStages[si].Enemy, seed, verbose: false);
                if (r.PlayerWon) wins++;
                battles++;
                turnSum += r.Turns;
                // LastActiveTurn は UnitTally.Add が Max を取る（ターン番号は足しても意味を持たない）ので、
                // 1戦あたりの平均が欲しいここでは戦闘ごとに自前で足す。
                if (r.TallyByUnit.TryGetValue(UnitCatalog.Nara.Id, out UnitTally? nt))
                    naraLastSum += nt.LastActiveTurn;
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (!sum.TryGetValue(id, out UnitTally? acc)) sum[id] = acc = new UnitTally();
                    acc.Add(t);
                }
            }
            perStage[si] = wins * 100.0 / RouteSeeds;
        }

        UnitTally Tally(string id) => sum.TryGetValue(id, out UnitTally? x) ? x : new UnitTally();
        UnitTally mudo = Tally(UnitCatalog.Mudo.Id), golm = Tally(UnitCatalog.Golm.Id);
        UnitTally nara = Tally(UnitCatalog.Nara.Id);

        rows.Add((vname, note, perStage.Average(), perStage,
                  (double)nara.DamageToAlly / battles,
                  (double)mudo.TakenFromAlly / battles, (double)mudo.DamageTaken / battles,
                  (double)mudo.DamageToEnemy / battles, (double)mudo.Deaths / battles,
                  (double)golm.TakenFromAlly / battles, (double)golm.Deaths / battles,
                  (double)naraLastSum / battles, (double)turnSum / battles));
        Console.Out.Flush();
    }

    double baseAvg = rows[0].Avg;

    Console.WriteLine();
    Console.WriteLine("## 勝率");
    Console.WriteLine();
    Console.WriteLine("| 変種 | 平均 | V0差 |" + string.Concat(routeStages.Select((_, i) => $" 第{i + 1}波 |")) + " 席 |");
    Console.WriteLine("|---|--:|--:|" + string.Concat(routeStages.Select(_ => "---:|")) + "---|");
    foreach (var r in rows)
    {
        var f = variants.First(v => v.Name == r.Name).F;
        string seats = $"{f[0]?.Name}/{f[1]?.Name} - {f[2]?.Name} - {f[3]?.Name}/{f[4]?.Name}";
        Console.WriteLine($"| {r.Name} | {r.Avg:F1}% | {(r.Avg - baseAvg):+0.0;-0.0}pt |"
            + string.Concat(r.PerStage.Select(x => $" {x:F1}% |")) + $" {seats} |");
    }

    Console.WriteLine();
    Console.WriteLine("## 燃料の行き先");
    Console.WriteLine();
    Console.WriteLine("`ナラ 削り` は肩代わりされたぶんも含む発生量（source はナラのまま）なので、");
    Console.WriteLine("規則が変種で動いていないことの検算になる。**その内訳がどこへ行ったか**が下の2列。");
    Console.WriteLine("`ムド 被ダメ` は敵味方を問わない総被弾で、`被(味)` との差が敵から受けたぶん。");
    Console.WriteLine();
    Console.WriteLine("| 変種 | ナラ 削り | ムド 被(味) | ムド 被ダメ | ムド 与ダメ(敵) | ムド 落ちた | ゴルム 被(味) | ゴルム 落ちた | ナラ 最終T | 決着T |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var r in rows)
        Console.WriteLine($"| {r.Name} | {r.NaraToAlly:F0} | {r.MudoTakenAlly:F0} | {r.MudoTaken:F0} "
            + $"| {r.MudoDmg:F0} | {r.MudoDeaths:F2} "
            + $"| {r.GolmTakenAlly:F0} | {r.GolmDeaths:F2} | {r.NaraLast:F1} | {r.Turns:F1} |");

    Console.WriteLine();
    foreach (var r in rows) Console.WriteLine($"- **{r.Name}**: {r.Note}");
    return;
}

// swap モード: ナラの**回復側**は成立するか（第21期）。
//
// 削り側は3編成で不活性と出た（route の配置・第20期の変換器の型・台の飽和を潰しても動かない）。
// 原因は minus-trait-design-notes §4 が先に書いていたとおりで、**ロスターは自傷ダメージで
// 既に飽和している**——ナラの削りは8番目の供給源になっただけで、変換器に余地が無い。
// 対して回復側は一度も測れていない（速攻＝床に張り付き / 被弾強化・死の連鎖＝ヴェルが同速）。
//
// **ablate は使わない。** 駒を1体減らすので寄与に「5体目の体そのもの」が必ず混ざる
// （第20期の +19.8pt がそれで、同席のゴルムは -25.5pt だった）。**同じ席にノノを置いた版と
// 比べれば、差がそのまま機構の差になる。** ノノは支払い方だけが違う回復役で、
// 回復14/ターン・自分のHPを同量・最も傷ついた味方1体（ナラは 5/ターン・遅い味方を削る・
// 自分より速い味方全員）。
//
// 台は2つ。**S1 はナラに不利な割れ方**（削り3 / 回復1）で、勝てば強い証拠になるが
// 負けても否定材料にはならない。**S2 が主判定**（削り2 / 回復2）。
// S2 だけ「4体（中央 空）」も測る——**5体目の体そのものの値段**で、差を読むときの下駄になる。
//
// **CompareBuilds() / Stages / Columns を触らない**ので docs/ の差分はゼロ。
//
//     dotnet run --project BattleSim -c Release 0 swap
if (focusId == "swap")
{
    IReadOnlyList<EnemyCatalog.Stage> swapStages = EnemyCatalog.Stages;
    const int SwapSeeds = 200;   // compare / pulse / route と同じ

    var cases = new (string Group, string Name, Formation F)[]
    {
        ("S1 耐久（削り3 / 回復1・ナラに不利）", "ノノ（土台）",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Sero, back1: UnitCatalog.Nono, back3: UnitCatalog.Dolga)),
        ("S1 耐久（削り3 / 回復1・ナラに不利）", "ナラ",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Sero, back1: UnitCatalog.Nara, back3: UnitCatalog.Dolga)),

        ("S2 守り（削り2 / 回復2・主判定）", "ノノ",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Nono, back1: UnitCatalog.Tou, back3: UnitCatalog.Kugu)),
        ("S2 守り（削り2 / 回復2・主判定）", "ナラ",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Nara, back1: UnitCatalog.Tou, back3: UnitCatalog.Kugu)),
        ("S2 守り（削り2 / 回復2・主判定）", "4体（中央 空）",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald,
                            back1: UnitCatalog.Tou, back3: UnitCatalog.Kugu)),

        // S3/S4 は S2 が床（全版 100/0/0/0/0）に落ちたので足した差し替え台。
        // 割れ方は S2 と同じ 削り2 / 回復2 のまま、**出力を持つ駒に入れ替えて余地を作る**
        // （S2 は ゴルム+ガルド+トウ+クグ で与ダメ合計 ~120 しかなく、第二波以降を削り切れない）。
        // セロは前1 に置くこと——狙撃化には戦闘中に後退した実績が要るので、後列始まりでは発火しない。
        ("S3 攻め（削り2 / 回復2・ゴルム軸）", "ノノ",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Golm,
                            center: UnitCatalog.Nono, back1: UnitCatalog.Dolga, back3: UnitCatalog.Sasa)),
        ("S3 攻め（削り2 / 回復2・ゴルム軸）", "ナラ",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Golm,
                            center: UnitCatalog.Nara, back1: UnitCatalog.Dolga, back3: UnitCatalog.Sasa)),
        ("S3 攻め（削り2 / 回復2・ゴルム軸）", "4体（中央 空）",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Golm,
                            back1: UnitCatalog.Dolga, back3: UnitCatalog.Sasa)),

        ("S4 攻め（削り2 / 回復2・ガルド軸）", "ノノ",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Nono, back1: UnitCatalog.Dolga, back3: UnitCatalog.Sasa)),
        ("S4 攻め（削り2 / 回復2・ガルド軸）", "ナラ",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Nara, back1: UnitCatalog.Dolga, back3: UnitCatalog.Sasa)),
        ("S4 攻め（削り2 / 回復2・ガルド軸）", "4体（中央 空）",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Gald,
                            back1: UnitCatalog.Dolga, back3: UnitCatalog.Sasa)),

        // S5 は S4 と同じメンバーを、**compare へ採用した席**（reseat 1位）で測り直したもの。
        // S4 の席（中央ナラ）と採用席では波別の形がまるで違うので（S4 100/34/4/34/0 に対し
        // 採用席は 100/69/74.5/3.5/3.0）、**採用の根拠にした「波ごとの振れ」が
        // 採用した席でも立っているかは、測らないと分からない。** 入れ替える枠は後1。
        // 割れ方は S4 と同じ 削り2（ガルド4・ドルガ6）／回復2（ササ12・セロ12）。
        ("S5 分散回復（採用席・削り2 / 回復2）", "ノノ",
            Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Sero, back1: UnitCatalog.Nono, back3: UnitCatalog.Dolga)),
        ("S5 分散回復（採用席・削り2 / 回復2）", "ナラ",
            Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Sero, back1: UnitCatalog.Nara, back3: UnitCatalog.Dolga)),
        ("S5 分散回復（採用席・削り2 / 回復2）", "4体（後1 空）",
            Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Sero, back3: UnitCatalog.Dolga)),
    };

    Console.WriteLine("# ナラの回復側は成立するか（swap）");
    Console.WriteLine();
    Console.WriteLine($"同じ席でナラとノノを入れ替えて比べる。全ステージ、seed 0..{SwapSeeds - 1}。");
    Console.WriteLine("数字は**1戦あたりの平均**（pulse / route と同じ規約）。診断用なので docs/ には置かない。");
    Console.WriteLine();
    Console.WriteLine("**`ablate` を使わないのは、駒を1体減らすと寄与に「5体目の体そのもの」が混ざるため。**");
    Console.WriteLine("S2 の `4体（中央 空）` がその体の値段で、ナラ版・ノノ版との差を読むときの下駄になる。");

    var results = new List<(string Group, string Name, Formation F, double Avg, double[] PerStage,
                            Dictionary<string, UnitTally> Sum, int Battles, double Turns)>();

    foreach (var (group, cname, f) in cases)
    {
        var sum = new Dictionary<string, UnitTally>();
        var perStage = new double[swapStages.Count];
        long turnSum = 0;
        int battles = 0;

        for (int si = 0; si < swapStages.Count; si++)
        {
            int wins = 0;
            for (int seed = 0; seed < SwapSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, swapStages[si].Enemy, seed, verbose: false);
                if (r.PlayerWon) wins++;
                battles++;
                turnSum += r.Turns;
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (!sum.TryGetValue(id, out UnitTally? acc)) sum[id] = acc = new UnitTally();
                    acc.Add(t);
                }
            }
            perStage[si] = wins * 100.0 / SwapSeeds;
        }

        results.Add((group, cname, f, perStage.Average(), perStage, sum, battles,
                     (double)turnSum / battles));
        Console.Out.Flush();
    }

    foreach (string group in cases.Select(c => c.Group).Distinct())
    {
        var rows = results.Where(r => r.Group == group).ToList();
        double baseAvg = rows[0].Avg;   // 各群の先頭（ノノ版）を基準にする

        Console.WriteLine();
        Console.WriteLine($"## {group}");
        Console.WriteLine();
        Console.WriteLine("| 版 | 平均 | ノノ差 |" + string.Concat(swapStages.Select((_, i) => $" 第{i + 1}波 |"))
            + " 回復役 与ダメ(味) | ゴルム 被(味) | 決着T |");
        Console.WriteLine("|---|--:|--:|" + string.Concat(swapStages.Select(_ => "---:|")) + "---:|---:|---:|");
        foreach (var r in rows)
        {
            // 回復役の削り総量。ノノ版は 0（継ぎ当ては味方を削らない）、4体版は該当なし。
            UnitTally? healer = r.Name.Contains("ナラ") ? Get(r.Sum, UnitCatalog.Nara.Id)
                              : r.Name.Contains("ノノ") ? Get(r.Sum, UnitCatalog.Nono.Id)
                              : null;
            string toAlly = healer is null ? "−" : $"{(double)healer.DamageToAlly / r.Battles:F0}";
            UnitTally golm = Get(r.Sum, UnitCatalog.Golm.Id);
            Console.WriteLine($"| {r.Name} | {r.Avg:F1}% | {(r.Avg - baseAvg):+0.0;-0.0}pt |"
                + string.Concat(r.PerStage.Select(x => $" {x:F1}% |"))
                + $" {toAlly} | {(double)golm.TakenFromAlly / r.Battles:F0} | {r.Turns:F1} |");
        }

        Console.WriteLine();
        Console.WriteLine("| 版 | 駒 | 与ダメ(敵) | 回復された | 被(味) | 落ちた |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var r in rows)
            foreach ((int _, UnitDef def) in r.F.Occupied())
            {
                UnitTally t = Get(r.Sum, def.Id);
                Console.WriteLine($"| {r.Name} | {def.Name} | {(double)t.DamageToEnemy / r.Battles:F0} "
                    + $"| {(double)t.Healed / r.Battles:F0} | {(double)t.TakenFromAlly / r.Battles:F0} "
                    + $"| {(double)t.Deaths / r.Battles:F2} |");
            }
    }

    Console.WriteLine();
    Console.WriteLine("`回復された` は ctx.Heal が実際に動かした HP（上限で切られた分は入らない）。");
    Console.WriteLine("**ガルドは Stoic なので常に 0**——回復も強化も受け付けない。");
    Console.WriteLine();
    Console.WriteLine("**S1 の交絡**: セロは `Coward`（3分の1＝14 削られると後退）＋ `Sniper`");
    Console.WriteLine("（後退してから後列にいると攻撃力2倍＋貫き化）。**ナラの回復が後退を遅らせ、");
    Console.WriteLine("狙撃化を抑えうる**ので、S1 でナラ版が負けても `セロ 与ダメ(敵)` が下がっていれば");
    Console.WriteLine("それは回復が損に働いたぶんで、機構の否定材料にはしない。");
    return;

    static UnitTally Get(Dictionary<string, UnitTally> sum, string id)
        => sum.TryGetValue(id, out UnitTally? x) ? x : new UnitTally();
}

if (focusId == "gullet") { GulletDiag.Run(args, stageIndex); return; }

if (focusId == "yoke") { YokeDiag.Run(args, stageIndex); return; }

if (focusId == "hush") { HushDiag.Run(args, stageIndex); return; }

if (focusId == "sever") { SeverDiag.Run(args, stageIndex); return; }

if (focusId == "suture") { SutureDiag.Run(args, stageIndex); return; }

if (focusId == "expose") { ExposeDiag.Run(args, stageIndex); return; }

if (focusId == "shove") { ShoveDiag.Run(args, stageIndex); return; }

if (focusId == "dull") { DullDiag.Run(args, stageIndex); return; }

if (focusId == "relay") { RelayModeDiag.Run(args, stageIndex); return; }

if (focusId == "slander") { SlanderDiag.Run(args, stageIndex); return; }

if (focusId == "overbear") { OverbearDiag.Run(args, stageIndex); return; }

if (focusId == "scale") { ScaleDiag.Run(args, stageIndex); return; }

if (focusId == "scapegoat" && (args.Length > 2 ? args[2] : "") == "phase0") { ScapegoatDiag.Phase0(args, stageIndex); return; }

if (focusId == "scapegoat") { ScapegoatDiag.Run(args, stageIndex); return; }

if (focusId == "divert" && (args.Length > 2 ? args[2] : "") == "phase0") { DivertDiag.Phase0(args, stageIndex); return; }

if (focusId == "divert" && (args.Length > 2 ? args[2] : "") == "probe") { DivertDiag.Probe(args, stageIndex); return; }

if (focusId == "divert") { DivertDiag.Run(args, stageIndex); return; }

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
if (focusId == "census")
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

if (focusId == "guard") { GuardDiag.Run(args, stageIndex); return; }

if (focusId == "whet") { WhetDiag.Run(args, stageIndex); return; }

if (focusId == "creak") { CreakDiag.Run(args, stageIndex); return; }


if (focusId == "carry") { CarryDiag.Run(args, stageIndex); return; }

if (focusId == "draft") { DraftDiag.Run(args, stageIndex); return; }

if (focusId == "draft2") { Draft2Diag.Run(args, stageIndex); return; }

if (focusId == "draft3") { Draft3Diag.Run(args, stageIndex); return; }

if (focusId == "slope") { SlopeDiag.Run(args, stageIndex); return; }


if (focusId == "wound") { WoundDiag.Run(args, stageIndex); return; }

if (focusId == "wcost") { WcostDiag.Run(args, stageIndex); return; }

if (focusId == "blade") { BladeDiag.Run(args, stageIndex); return; }

if (focusId == "body") { BodyDiag.Run(args, stageIndex); return; }

if (focusId == "traits") { TraitsDiag.Run(args, stageIndex); return; }

if (focusId == "pairs") { PairsDiag.Run(args, stageIndex); return; }

if (focusId == "pairs2") { Pairs2Diag.Run(args, stageIndex); return; }

if (focusId == "checkup") { CheckupDiag.Run(args, stageIndex); return; }

if (focusId == "breadth") { BreadthDiag.Run(args, stageIndex); return; }


if (focusId == "thorn") { ThornDiag.Run(args, stageIndex); return; }


if (focusId == "suture2") { Suture2Diag.Run(args, stageIndex); return; }

if (focusId == "mender") { MenderDiag.Run(args, stageIndex); return; }

if (focusId == "blaze2") { Blaze2Diag.Run(args, stageIndex); return; }

if (focusId == "gauge") { GaugeDiag.Run(args, stageIndex); return; }


if (focusId == "gather") { GatherDiag.Run(args, stageIndex); return; }
if (focusId == "deep") { DeepDiag.Run(args, stageIndex); return; }
if (focusId == "soak") { SoakDiag.Run(args, stageIndex); return; }
if (focusId == "lastslot") { LastslotDiag.Run(args, stageIndex); return; }

if (focusId == "creak3") { Creak3Diag.Run(args, stageIndex); return; }

if (focusId == "spend") { SpendDiag.Run(args, stageIndex); return; }

if (focusId == "burn") { BurnDiag.Run(args, stageIndex); return; }

if (focusId == "pace") { PaceDiag.Run(args, stageIndex); return; }

if (focusId == "tempo") { TempoDiag.Run(args, stageIndex); return; }

if (focusId == "hold") { HoldDiag.Run(args, stageIndex); return; }



if (focusId == "taillight") { TaillightDiag.Run(args, stageIndex); return; }

if (focusId == "tomo" && args.Length > 2 && args[2] == "yield") { TomoDiag.Yield(args, stageIndex); return; }

if (focusId == "tomo") { TomoDiag.Run(args, stageIndex); return; }

if (focusId == "hold2") { Hold2Diag.Run(args, stageIndex); return; }

if (focusId == "spread") { SpreadDiag.Run(args, stageIndex); return; }

if (focusId == "favor" && (args.Length > 2 ? args[2] : "") == "phase0") { FavorDiag.Phase0(args, stageIndex); return; }

if (focusId == "favor") { FavorDiag.Run(args, stageIndex); return; }

// turn モード: 火選りを**手番へ降ろす**（第60期）。
//
// 第58期 9-2 の実測が出発点。ターンの順序は `TickStatuses` → `OnTurnStart` → 行動順ループで、
// 火の粉は `OnAfterAttack` ——つまり **`OnTurnStart` の機構は供給に対して構造的に1ターン遅れる**。
// 第1ターンの発火時点では盤上の誰も燃えておらず、**強化するはずの熾のホタをそのターンだけ鈍らせていた**
// （弱体の受け手に 2.00 量/戦 ＝ 第1ターンの1回 × `Loss` 2 がちょうど載っていた）。
// **係数では詰められない**（`Gain` を上げてもこの1回は消えない）。
//
// **engine には何も足さない。** `FavorTrait` に `OnAction` と `ActsOnPattern` の分岐を置き
// （継ぎ当て＝`MenderTrait` の形の踏襲）、版の切り替えは**駒の側**でやる
// ——`ActsOnPattern` は `UnitDef.Actions` を読むので、規則（`Run` の引数）では切り替えられない。
// 診断のローカルに `Actions = [Skill]` を持つヒヨの複製を組む（`gradient` / `aim` と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 turn phase0  # 実装前の地図（止めうる経路・粛・速さ・現在の出力）
//     dotnet run --project BattleSim -c Release 0 turn          # 主表 V0/V1/V2/V3 × 4行 × 5波 と Q1〜Q6
//     dotnet run --project BattleSim -c Release 0 turn sweep    # Gain/Loss の掃引 4点
//     dotnet run --project BattleSim -c Release 0 turn alt      # 帰属の符号を別 seed 帯（200..599）で追試


if (focusId == "turn" && (args.Length > 2 ? args[2] : "") == "phase0") { TurnDiag.Phase0(args, stageIndex); return; }


if (focusId == "turn") { TurnDiag.Run(args, stageIndex); return; }

// miasma モード: 瘴気（グザ）を手番へ降ろす（第61期）。
//
// **移設が動かす一番大きい量は「ターン頭の発火順」である。** ターンの順序は
// `TickStatuses` → `OnTurnStart`（**席順の昇順**）→ 行動順ループなので、
// `OnTurnStart` に置いた機構どうしの前後は**席の番号だけ**で決まる。瘴気と澱み喰い
// （ヴィオ）はどちらもターン頭で、グザがヴィオより前の席なら撒いた毒はその場で
// 吸い上げられて**味方は1点も払わない**——「毒の代金を誰が払うか」が席の左右で
// 切り替わる隠れた判断になっていた。手番（速5）へ降ろすと必ずヴィオが先になる。
//
// **版の切り替えは駒の側で行う**（`ActsOnPattern` は `UnitDef.Actions` を読むので
// 規則では切り替わらない）。診断のローカルに `UnitDef` を置き、`UnitCatalog` は触らない
// ——`gradient` / `aim` / `turn` と同じ扱い。
//
//     dotnet run --project BattleSim -c Release 0 miasma phase0  # 実装前の地図（発火順・出力・分母）
//     dotnet run --project BattleSim -c Release 0 miasma         # 主表 V0/V1/V2/V3 × 8行 × 5波 と Q1〜Q7
//     dotnet run --project BattleSim -c Release 0 miasma seat    # 席の入れ替えの追試（Q1 の直接の証拠）
//     dotnet run --project BattleSim -c Release 0 miasma alt     # 帰属の符号を別 seed 帯（200..599）で追試


if (focusId == "miasma" && (args.Length > 2 ? args[2] : "") == "phase0") { MiasmaDiag.Phase0(args, stageIndex); return; }

if (focusId == "miasma") { MiasmaDiag.Run(args, stageIndex); return; }

if (focusId == "blaze" && (args.Length > 2 ? args[2] : "") == "phase0") { BlazeDiag.Phase0(args, stageIndex); return; }

if (focusId == "blaze") { BlazeDiag.Run(args, stageIndex); return; }

if (focusId == "funnel") { FunnelDiag.Run(args, stageIndex); return; }

if (focusId == "yield") { YieldDiag.Run(args, stageIndex); return; }

// replay モード: 1戦ぶんの台本を JSON で吐く。戦闘画面（ビューア）が読む。
//
// BattleEngine.Run は seed 決定的な純関数で戦闘を丸ごと計算し切るので、
// ビューアはシミュレーションを持たず、この列を再生するだけでよい。
// ここが JSON を吐く唯一の場所。docs/ と違って生成物を repo に置かない
// （盤面が変わるたび腐るし、diff が読めない）。
//
//     dotnet run --project BattleSim -c Release <stage> replay [編成の部分一致] [seed]
if (focusId == "replay")
{
    string want = args.Length > 2 ? args[2] : "";
    int replaySeed = args.Length > 3 && int.TryParse(args[3], out int rs) ? rs : 0;

    var (buildName, playerF) = CompareBuilds()
        .FirstOrDefault(b => want.Length == 0 || b.Name.Contains(want));
    if (playerF is null)
    {
        Console.Error.WriteLine($"編成が見つからない: {want}");
        return;
    }

    EnemyCatalog.Stage st = EnemyCatalog.Stages[stageIndex];
    BattleResult res = BattleEngine.Run(playerF, st.Enemy, replaySeed, verbose: true);

    // 初期盤面は Run の前の状態が要るが、Run は編成を書き換えないので
    // ここで Formation から組み直せる。InstanceId は Deploy の順（味方→敵、スロット昇順）で
    // 振られるので、同じ順で数えれば一致する。
    var roster = new List<object>();
    int id = 0;
    foreach (var (team, f) in new[] { (0, playerF), (1, st.Enemy) })
        foreach (var (slot, def) in f.Occupied())
            roster.Add(new
            {
                id = id++,
                team,
                slot,
                name = def.Name,
                maxHp = def.MaxHp,
                attack = def.Attack,
                speed = def.Speed,
                pattern = def.Pattern.ToString(),
                plus = def.PlusText,
                minus = def.MinusText
            });

    // 増援・蘇生で後から出る駒は roster に無いので、ビューアは Summon イベントで足す。
    // その駒の見た目に要る情報をイベント側からは引けないため、カタログ全体も併せて渡す。
    var catalog = UnitCatalog.All.ToDictionary(
        u => u.Name,
        u => (object)new { maxHp = u.MaxHp, attack = u.Attack, pattern = u.Pattern.ToString() });

    var payload = new
    {
        build = buildName,
        stage = st.Name,
        stageIndex,
        seed = replaySeed,
        playerWon = res.PlayerWon,
        turns = res.Turns,
        maxChain = res.MaxEnemyKillsInOneTurn,
        roster,
        catalog,
        events = res.Events.Select(e => new
        {
            kind = e.Kind.ToString(),
            turn = e.Turn,
            actor = e.ActorId,
            target = e.TargetId,
            amount = e.Amount,
            hpAfter = e.HpAfter,
            friendly = e.FriendlyFire,
            slot = e.Slot,
            team = e.Team,
            pattern = e.Pattern?.ToString(),
            text = e.Text
        }).ToList()
    };

    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload,
        new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    return;
}

if (focusId == "goad") { GoadDiag.Run(args, stageIndex); return; }

if (focusId == "finisher") { FinisherDiag.Run(args, stageIndex); return; }

if (focusId == "wave2") { Wave2Diag.Run(args, stageIndex); return; }

if (focusId == "ledger") { LedgerDiag.Run(args, stageIndex); return; }

if (focusId == "lit") { LitDiag.Run(args, stageIndex); return; }

if (focusId == "reader") { ReaderDiag.Run(args, stageIndex); return; }

if (focusId == "offturn") { OffturnDiag.Run(args, stageIndex); return; }

if (focusId == "watch") { WatchDiag.Run(args, stageIndex); return; }

if (focusId == "compare") { CompareDiag.Run(args, stageIndex); return; }

if (focusId == "engage") { EngageDiag.Run(args, stageIndex); return; }

// seats モード: 会戦の隊列持ち越し診断。第2戦・第3戦の入場スロットが初期配置から
// どれだけずれているかを測る（第3期 Phase H。仮説 (i)「D5 の Slot 持ち越しが移動系の
// 隊列を壊している」の切り分け）。診断用で docs/ には置かない（標準出力で読むだけ）。
//
// 会戦を跨いだ駒の同定は UnitId で行う。Slot はまさに今動いている量なので同定キーに使えない
// （BattleOpening の (TeamId, Slot) 同定は再生側の話。ここは味方限定＋重複ガード付き）。
//
//     dotnet run --project BattleSim -c Release 0 seats [絞り込み]
if (focusId == "seats")
{
    var all = CompareBuilds();
    const int SeatSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 列は Name で引く（Columns の並び順に依存しない）。診断は順路のみ——知りたいのは
    // 「第2戦の開始時点でどこに居るか」で、どの列に当てても D5 の挙動は同じ。
    IReadOnlyList<Formation> route = EnemyCatalog.Columns.First(c => c.Name == "順路").Squads;
    string[] seatName = FormationRules.SeatNames;

    Console.WriteLine($"# 会戦の隊列持ち越し診断（順路・seed 0..{SeatSeeds - 1} の {SeatSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("一致 = 生存駒がすべて自分の初期スロットに居る試行（死んだ駒の枠が空くことはずれに数えない）。");
    Console.WriteLine("後退済み = 第2戦の入場時点で HasFallenBack が立っている駒の平均数（判断 D6 で会戦を跨いで維持される）。");

    foreach (var (name, f) in targets)
    {
        // 同定不能ガード: 味方編成内に同じ UnitId の駒が複数あると UnitId で駒を同定できない。
        // 現在の31編成に重複は無いが、増えたときに黙って嘘の集計を出さないための番犬（作業ルール7）。
        var seats0 = f.Occupied().ToList();
        if (seats0.GroupBy(x => x.Def.Id).Any(g => g.Count() > 1))
        {
            Console.WriteLine();
            Console.WriteLine(name);
            Console.WriteLine("  同定不能（UnitId 重複）: 集計から除外");
            continue;
        }
        var home = seats0.ToDictionary(x => x.Def.Id, x => x.Slot);

        // 添字はそのまま Battle 番号（1=第2戦, 2=第3戦）。第1戦は初期配置そのものなので測らない。
        var reached = new int[3];
        var match = new int[3];
        var patterns = new Dictionary<string, int>[] { new(), new(), new() };
        double fbSum = 0, aliveSum = 0; // 第2戦の入場時のみ（§2.4）

        for (int seed = 0; seed < SeatSeeds; seed++)
        {
            // Openings は verbose:true のときだけ入る（このモードが engage より遅い理由）
            EngagementResult r = EngagementEngine.Run(new[] { f }, route, seed, verbose: true);
            for (int b = 1; b <= 2 && b < r.Openings.Count; b++)
            {
                // 味方1部隊なので Openings[b] の存在＝第 b+1 戦に到達（負けた時点で会戦が終わる）。
                // 持ち越されるのは生存駒だけなので、死んだ駒はここに現れない。
                var mine = r.Openings[b]
                    .Where(o => o.TeamId == BattleContext.PlayerTeam)
                    .OrderBy(o => o.Slot).ToList();
                reached[b]++;
                if (mine.All(o => home[o.UnitId] == o.Slot)) match[b]++;
                // 最頻パターン: スロット昇順の表示文字列をそのままキーにする
                // （味方は UnitId・Slot とも一意なので昇順整列が正準形になる）
                string pat = string.Join(" ", mine.Select(o => $"{seatName[o.Slot]}={o.Name}"));
                patterns[b][pat] = patterns[b].GetValueOrDefault(pat) + 1;
                if (b == 1)
                {
                    fbSum += mine.Count(o => o.HasFallenBack);
                    aliveSum += mine.Count;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(name);
        Console.WriteLine("  初期配置    : "
            + string.Join(" ", seats0.Select(x => $"{seatName[x.Slot]}={x.Def.Name}")));
        for (int b = 1; b <= 2; b++)
        {
            string label = b == 1 ? "第2戦の入場" : "第3戦の入場";
            if (reached[b] == 0)
            {
                Console.WriteLine($"  {label} : 到達 0/{SeatSeeds}");
                continue;
            }
            // 同数タイは Ordinal 順で先頭を取る（実行のたびに最頻が入れ替わらないように）
            var top = patterns[b].OrderByDescending(kv => kv.Value)
                                 .ThenBy(kv => kv.Key, StringComparer.Ordinal).First();
            // 一致/ずれの分母は到達試行数。200 未満なら到達数を前置する
            // （一致・ずれ・未到達の3値を同じ /200 に混ぜると読めない）
            string reach = reached[b] < SeatSeeds ? $"到達 {reached[b]}/{SeatSeeds}  " : "";
            Console.WriteLine($"  {label} : {reach}一致 {match[b]}/{reached[b]}  ずれ {reached[b] - match[b]}/{reached[b]}"
                + $"   最頻: {top.Key} （{top.Value}/{reached[b]}）");
        }
        if (reached[1] > 0)
            Console.WriteLine($"  第2戦の入場で後退済み: {fbSum / reached[1]:F1}体/{aliveSum / reached[1]:F1}体");
        Console.Out.Flush();
    }
    return;
}


if (focusId == "boss") { BossDiag.Run(args, stageIndex); return; }

// =====================================================================================
// tank モード（第118期） —— 時間を買う機構（自己回復タンク）を作って測る
//
// **本体は `BattleSim/Tank.cs`**（診断を別ファイルに置いた最初の例）。
// このファイルの top-level statements は 67,000 行が**全部で1つのメソッド**で、
// Release のビルドに 4 分かかる（実測）。診断1本ぶんのローカルとクロージャをそこへ足す理由が無いので、
// **クロージャを1つも作らない形**（static メソッドと static フィールドだけ）で外に出した。
// **ここは振り分けの数行だけ。** `derive rules` の走査もこの形に対応させてある（第118期）。
//
//     dotnet run --project BattleSim -c Release 0 tank phase0   # 表P（経路表・§1-B の再測定）
//     dotnet run --project BattleSim -c Release 0 tank run      # 表A〜E
//     dotnet run --project BattleSim -c Release 0 tank check    # 自己検査
if (focusId == "tank")
{
    TankDiag.Run(args.Length > 2 ? args[2] : "phase0");
    return;
}

// wound2 モード: 傷という通貨の棚卸し（第120期・**測定だけ**）。中身は `Wound2.cs`。
// **engine に規則は1本も足していない**——在庫の走査・消滅の帳簿・実際に減った HP の計数だけ。
//
//     dotnet run --project BattleSim -c Release 0 wound2 phase0 / run / check
//
// 第121期: 版の引数を1つ足した（**既存3モードの呼び出しは1文字も変えない**）。
//
//     dotnet run --project BattleSim -c Release 0 wound2 spill phase0 / run / check
if (focusId == "wound2")
{
    Wound2Diag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "phase0",
                   args.Length > 4 ? args[4] : "");
    return;
}

// time モード: 軸が回る前に落ちる問題（第126期）。中身は `Time.cs`。
// **Phase 0 は盤面を1ビットも動かさない**——読むのは計数
// （`UnitTally.LastActiveTurn` / `Deaths` / `DamageTaken` / `DamageToEnemy`）だけ。
// `TankDiag` / `Wound2Diag` と同じく、**ここは振り分けの数行だけ**（Release のビルド時間）。
//
//     dotnet run --project BattleSim -c Release 0 time phase0   # Q0-1〜Q0-8
if (focusId == "time")
{
    TimeDiag.Run(args.Length > 2 ? args[2] : "phase0");
    return;
}

// grade モード: 段（格上げ）を作る期（第127期）。中身は `Grade.cs`。
// **Phase 0 は盤面を1ビットも動かさない**（走査と数え物だけ）。段1 も台は診断のローカルで、
// `Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` は1文字も触らない。
// `TankDiag` / `Wound2Diag` / `TimeDiag` と同じく**ここは振り分けの数行だけ**（Release のビルド時間）。
//
//     dotnet run --project BattleSim -c Release 0 grade phase0   # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 grade run      # 段1（V0〜V4 × 台2つ）
if (focusId == "grade")
{
    GradeDiag.Run(args.Length > 2 ? args[2] : "phase0");
    return;
}

// grade2 モード: 段の載せ替え（第128期）。中身は `Grade2.cs`。
// **Phase 0 と段1 は盤面を1ビットも動かさない**（走査・数え物・既存の計数の読み直しだけ）。
//
//     dotnet run --project BattleSim -c Release 0 grade2 phase0  # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 grade2 stock   # 段1 の棚卸し（→ docs/stock.md）
//     dotnet run --project BattleSim -c Release 0 grade2 run     # 段2 の測定（発火率・到達率）
//     dotnet run --project BattleSim -c Release 0 grade2 check [採用前のbalance.md]
if (focusId == "grade2")
{
    Grade2Diag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// survive モード: 指標の直しと、「起動まで守れるか」（第129期）。中身は `Survive.cs`。
// **Phase 0 と段2 は盤面を1ビットも動かさない**——器具（`TraitId.Undying`）の保持者は
// `UnitCatalog.All` に1枚もおらず、台は診断のローカルで `Presets` を1文字も触らない。
// `TankDiag` / `Wound2Diag` / `TimeDiag` / `GradeDiag` と同じく**ここは振り分けの数行だけ**。
//
//     dotnet run --project BattleSim -c Release 0 survive phase0  # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 survive run     # 段2（延命台）
//     dotnet run --project BattleSim -c Release 0 survive check [採用前のbalance.md]
if (focusId == "survive")
{
    SurviveDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// ember モード: 火を配る（第130期）。中身は `Relay.cs`。
// **Phase 0 は盤面を1ビットも動かさない**（走査と数え物だけ）。段1 は `EmberRule` で版を振る
// ——`EmberRule.Off` は第129期までの盤面と 305 セル 0 件で一致する（`Ignite` は乱数を引かない）。
// `SurviveDiag` / `Grade2Diag` と同じく**ここは振り分けの数行だけ**。
//
//     dotnet run --project BattleSim -c Release 0 ember phase0  # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 ember run     # 段1（主判定 P1 ＋ 盤面）
//     dotnet run --project BattleSim -c Release 0 ember check [採用前のbalance.md]
if (focusId == "ember")
{
    RelayDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// wildfire モード（第133期） —— 撒いた火を読む（ボルグ）。
// **敵が燃えていることを読む駒が1枚も無い**という穴を埋める。実装は `BattleSim/Wildfire.cs`。
if (focusId == "wildfire")
{
    WildfireDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// stacks モード（第134期） —— 重ね掛けの実測と、盤面ルールの対称性。
// **測定だけの期。機構を1つも足さない。** 実装は `BattleSim/Stacks.cs`。
//
//     dotnet run --project BattleSim -c Release 0 stacks phase0  # Q0-1〜Q0-8（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 stacks burn    # 段1（重ね掛けの実測）
//     dotnet run --project BattleSim -c Release 0 stacks rules   # 段2（盤面ルールの対称性）
//     dotnet run --project BattleSim -c Release 0 stacks check [採用前のbalance.md]
if (focusId == "stacks")
{
    StacksDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// parry モード: ガルドは何で死んでいるか（と、受け流し）（第135期）。中身は `Parry.cs`。
// **段1 は測るだけ**——engine に足したのは計数のノブだけで、既定では配列を1本も確保しない。
// **ここで規則の型名を書かない**——`derive rules` は直前のクラス宣言でファイルとモードを結ぶので、
// 振り分けのコメントに型名を書くと `利用者` 列が1つ手前のモード（`ember`）にも付く（第129期）。
// `Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` は1文字も触らない。
// `SurviveDiag` / `RelayDiag` と同じく**ここは振り分けの数行だけ**（Release のビルド時間）。
//
//     dotnet run --project BattleSim -c Release 0 parry phase0            # Q0-1〜Q0-10
//     dotnet run --project BattleSim -c Release 0 parry harm > docs/harm.md  # 段1 の生成物
//     dotnet run --project BattleSim -c Release 0 parry check [採用前のbalance.md]
if (focusId == "parry")
{
    ParryDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// wall モード（第136期） —— ガルドを壁にする（確実な庇い・受け流し・中継）。本体は `Wall.cs`。
// **ここは振り分けの数行だけ**（Release のビルド時間）。**規則の型名をここに書かない**（第129期）。
//
//     dotnet run --project BattleSim -c Release 0 wall phase0            # Q0-1〜Q0-10
//     dotnet run --project BattleSim -c Release 0 wall n                 # 段2 の N の決め方（§5-2）
//     dotnet run --project BattleSim -c Release 0 wall run [段1のbalance.md]  # 段2/段3 の版 × 群A/B/C
//     dotnet run --project BattleSim -c Release 0 wall check [段1のbalance.md]  # 自己検査
if (focusId == "wall")
{
    WallDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// shard モード（第137期） —— 砕けの鍵を自前にする（ヒビ）。本体は `Shard.cs`。
// **ここは振り分けの数行だけ**。**規則の型名をここに書かない**（第129期）。
//
//     dotnet run --project BattleSim -c Release 0 shard phase0   # Q0-1 の実測（現行の帳簿）
//     dotnet run --project BattleSim -c Release 0 shard scan     # 段1 の台の下見（床と天井）
//     dotnet run --project BattleSim -c Release 0 shard run      # 掰引（3点）× 台 × 既存５行
//     dotnet run --project BattleSim -c Release 0 shard check docs/balance.md   # 自己検査
if (focusId == "shard")
{
    ShardDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// sweep モード（第141期） —— 全診断の exit 検査。本体は `Sweep.cs`。
// `CLAUDE.md` のコマンド表を自分で読み、引数の穴の無い本を1本ずつ上限つきの子プロセスで走らせる。
// **合格 = 異常終了 0 本**。毎期は回さない（80 分前後）——`UnitCatalog.All` ／ `Retired` ／ `Presets` に触る期の受け入れ条件。
//
//     dotnet run --project BattleSim -c Release 0 sweep [上限秒] [絞り込み]   # 全部（上限 90 秒）
//     dotnet run --project BattleSim -c Release 0 sweep list                  # 一覧だけ（戦闘0回）
if (focusId == "sweep")
{
    SweepDiag.Run(args.Skip(2).ToArray());
    return;
}

// handoff モード: 会戦の交代の実態を計測する（第4期 Phase K）。「部隊を1つ足すと突破数の
// 増分が編成によらずほぼ +1.00」の原因を、仮説 P（第1部隊が敵をほとんど削らずに全滅し、
// 第2部隊は仕切り直しで1波抜くだけ＝拾えていない）と仮説 Q（拾えてはいるが、第2部隊の
// 担当が重い側の波なので無傷スタートの有利と相殺して +1 に見える）に切り分ける。
// 診断用で docs/ には置かない（seats と同じ扱い。標準出力で読むだけ）。
//
// 列は順路（5波）。地点（3波）は3部隊でほぼ全編成 100% に飽和していて勾配が見えない。
//
// 判定の中心は対照実験（2-3）:「無傷の1部隊が第 i 波から始めたら何波抜けるか」を接尾列
// 順路[i..5] で測る。第2部隊は「敵が削れた第 i 波」から始まるので、実績（2-2 の
// 部隊2が抜いた波数）が対照の [i..5] を明確に上回るなら Q、ほぼ等しい・下回るなら P。
//
//     dotnet run --project BattleSim -c Release 0 handoff [絞り込み]
if (focusId == "handoff")
{
    var all = CompareBuilds();
    const int HandoffSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 列は Name で引く（Columns の並び順に依存しない）
    IReadOnlyList<Formation> route = EnemyCatalog.Columns.First(c => c.Name == "順路").Squads;
    int waves = route.Count;

    // 敵の残りの分母（体数・HP とも）は列の定義から取った不変値。SquadEntry.DefMaxHpSum を
    // 分母にする案は却下——あれは「その戦闘に入った駒」だけの合計で、死んだ駒が分子と分母から
    // 一緒に抜けるため、3体倒して2体だけ全快で残した部隊が HP 100% に化け、第1部隊の削りが
    // 見えなくなる（SquadEntry の doc と engage の分母の判断と同じ。この診断はまさに削りを
    // 測るものなので、ここを間違えると仮説 P を機械的に棄却してしまう）。
    int[] enemyDefTotal = route.Select(e => e.Occupied().Sum(x => x.Def.MaxHp)).ToArray();
    int[] enemyDefCount = route.Select(e => e.Occupied().Count()).ToArray();

    // 対照実験（2-3）の接尾列は診断モードのローカル変数で組む。EnemyCatalog.Columns には
    // 足さない（公開する列の集合を診断で汚さない）。[1..5] は順路そのものなので、その
    // 期待突破数が docs/engage.md の順路×1部隊と完全一致することが組み方の検算になる。
    var suffixes = Enumerable.Range(0, waves)
        .Select(skip => (IReadOnlyList<Formation>)route.Skip(skip).ToList())
        .ToArray();

    Console.WriteLine($"# 会戦の交代診断（順路・seed 0..{HandoffSeeds - 1} の {HandoffSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("交代 = 味方2部隊の走行で第1部隊が尽き、第2部隊が入場した最初の Battle。");
    Console.WriteLine("`Pairings` の PlayerSquad の変化で特定し、その Battle の敵の入場戦力を台帳に取る。");
    Console.WriteLine("交代せずに終わった試行（第1部隊が最終波との相打ちで会戦を終えた等）は分母から外す。");
    Console.WriteLine("`抜いた波数` の分母は全試行。敵の残りの分母（体数・HP%）はその波の定義上の値。");

    var controlRows = new List<string>();   // 2-3 の表は最後にまとめて出す

    foreach (var (name, f) in targets)
    {
        // --- 2-1 交代台帳 / 2-2 部隊別の内訳（味方2部隊 × 順路の同じ走行から取る） ---
        Formation[] two = { f, f };
        int handoffs = 0;                       // 交代が起きた試行数
        var handoffWave = new int[waves];       // 交代時に敵が居た波（1回目の交代のみ）
        double aliveAcc = 0, defCountAcc = 0;   // 交代時の敵の残り体数と定義体数
        long hpAcc = 0, hpDenomAcc = 0;         // 交代時の敵の残り HP と定義上総最大HP
        var clearedBy = new int[2, waves];      // [部隊, 波] → その部隊がその波を抜いた試行数

        for (int seed = 0; seed < HandoffSeeds; seed++)
        {
            EngagementResult r = EngagementEngine.Run(two, route, seed, verbose: false);

            // 交代の特定: PlayerSquad が 0 でなくなった最初の Battle。2部隊なので交代は
            // 高々1回だが、仕様（3部隊でも1回目だけ数える）を形にして First で取る。
            int hb = -1;
            for (int b = 0; b < r.Pairings.Count; b++)
                if (r.Pairings[b].PlayerSquad != 0) { hb = b; break; }
            if (hb >= 0)
            {
                handoffs++;
                int ei = r.Pairings[hb].EnemySquad;
                handoffWave[ei]++;
                aliveAcc += r.EnemyEntries[hb].Alive;
                defCountAcc += enemyDefCount[ei];
                hpAcc += r.EnemyEntries[hb].HpSum;
                hpDenomAcc += enemyDefTotal[ei];
            }

            // 部隊別の内訳: Battle b で敵部隊を抜いたかは次の Battle の EnemySquad が
            // +1 されているかで分かる。最終 Battle だけは次が無いので、抜いた部隊の
            // 累計（EnemySquadsCleared）と突き合わせる（各 Battle で抜けるのは高々1部隊）。
            for (int b = 0; b < r.Pairings.Count; b++)
            {
                var (pSquad, eSquad) = r.Pairings[b];
                bool clearedHere = b + 1 < r.Pairings.Count
                    ? r.Pairings[b + 1].EnemySquad == eSquad + 1
                    : r.EnemySquadsCleared == eSquad + 1;
                if (clearedHere) clearedBy[pSquad, eSquad]++;
            }
        }

        Console.WriteLine();
        Console.WriteLine(name);
        Console.WriteLine($"  交代の発生: {handoffs}/{HandoffSeeds}（交代せず終わった試行 {HandoffSeeds - handoffs}）");
        if (handoffs > 0)
        {
            Console.WriteLine("  交代時に敵が居た波: " + string.Join(" / ",
                Enumerable.Range(0, waves).Select(w => $"第{w + 1}波 {handoffWave[w]}")));
            Console.WriteLine($"  交代時の敵の残り: {aliveAcc / handoffs:F1}体 / {defCountAcc / handoffs:F1}体、"
                + $"HP {hpAcc * 100.0 / hpDenomAcc:F0}%（定義上の総最大HPに対する割合）");
        }
        for (int p = 0; p < 2; p++)
        {
            double cleared = Enumerable.Range(0, waves).Sum(w => clearedBy[p, w]);
            Console.WriteLine($"  部隊{p + 1}が抜いた波数: {cleared / HandoffSeeds:F2}（" + string.Join(" / ",
                Enumerable.Range(0, waves).Select(w => $"第{w + 1}波 {clearedBy[p, w]}")) + "）");
        }
        Console.Out.Flush();

        // --- 2-3 対照実験（無傷の1部隊 × 接尾列） ---
        var cells = suffixes.Select(col =>
        {
            double clearedSum = 0;
            for (int seed = 0; seed < HandoffSeeds; seed++)
                clearedSum += EngagementEngine.Run(new[] { f }, col, seed, verbose: false)
                    .EnemySquadsCleared;
            return clearedSum / HandoffSeeds;
        }).ToArray();
        controlRows.Add($"| {name} |" + string.Concat(cells.Select(c => $" {c:F2} |")));
    }

    Console.WriteLine();
    Console.WriteLine("## 対照実験: 無傷の1部隊が第 i 波から始めたときの期待突破数（接尾列）");
    Console.WriteLine();
    Console.WriteLine("[1..5] は順路そのもの。docs/engage.md の順路×期待突破数(1) と完全一致するはず");
    Console.WriteLine("（接尾列の組み方の検算。一致しなければこの表は読めない）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, waves).Select(i => $" [{i}..{waves}] |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, waves).Select(_ => "--:|")));
    foreach (string row in controlRows) Console.WriteLine(row);
    return;
}

// cost モード: 波の「代金」を測る診断（第5期 Phase M）。
// compare / engage は「勝てるか」しか測っておらず、「いくら払ったか」の列が無い。
// 無傷の1部隊がその波「だけ」と戦ったとき（単独列 [i..i] 相当。会戦として組む必要は
// 無いので BattleEngine.Run を直接呼ぶ）、勝った試行に何が残るか（残体数・残HP%）を測り、
// 代金 = 100% − 残HP% と読む。負けた試行は代金が定義できないので集計から外す（勝率を併記）。
//
// 波間の差は代金の平均で、編成間の差は代金の標準偏差で見る。標準偏差が一律に小さいなら
// 「どの波もどの編成にも同じ値段」で、波をいくら安くしても投入部隊数の配分判断を生まない
// （第5期 §0。勾配のある部隊列を設計する動機の裏付けを取る診断）。
// 診断用で docs/ には置かない（seats / handoff と同じ扱い。標準出力で読むだけ）。
//
//     dotnet run --project BattleSim -c Release 0 cost [絞り込み]
if (focusId == "cost")
{
    var all = CompareBuilds();
    const int CostSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    var waves = EnemyCatalog.Stages
        .Select((st, i) => (Name: $"第{i + 1}波", Enemy: st.Enemy))
        .ToList();

    Console.WriteLine($"# 波の代金診断（単独戦・seed 0..{CostSeeds - 1} の {CostSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("無傷の1部隊が各波「だけ」と戦ったときの勝率と、勝った試行の残存（体数・HP%）。");
    Console.WriteLine("**代金 = 100% − 残HP%**。残HP% の分母は編成の定義上の総最大HP");
    Console.WriteLine("（engage の入場戦力と同じ判断。生存駒だけを分母にすると全快1体が 100% に化ける）。");
    Console.WriteLine();
    Console.WriteLine("検算: 第1波の残HP% は docs/engage.md 順路の「第2戦の入場戦力」とおおむね一致するはず");
    Console.WriteLine("（順路の第1戦は第1波単独と同じ状況で、境界の CarryOver は HP に触らない。");
    Console.WriteLine("大きくずれたら CarryOver が残存に何かしている——止まって報告する。第5期 §2-2）。");
    Console.WriteLine();
    EmitCostTables(targets, waves, CostSeeds);
    return;
}

if (focusId == "gradient") { GradientDiag.Run(args, stageIndex); return; }

// aim モード: 安い波の「代金の向き」を測る診断（第6期 Phase P）。
// gradient で分かったのは「代金の平均は狙い帯に乗せられるが、向き（どの型の編成に安いか）は
// 作れない」こと——第1波候補の 単体 − 範囲 は +3.1pt / +3.1pt / +2.4pt で、編成間の
// ばらつき（SD 9.4pt）に埋もれている。原因の仮説は方向の違う2つで、どちらが正しいかで
// 作るべき波が正反対になる:
//   H1（戦闘が短すぎる）: 範囲の利得は「敵を早く減らして被弾を減らす」複利なので、
//                          減らした状態で経過するターンが多いほど効く。総HP 150 では
//                          2〜3ターンで終わって複利が効く前に決着する → 総HPを上げる
//   H2（1体あたりの価値が低すぎる）: 1キルの価値 = その駒の攻撃力 × 残りターン数。
//                          農兵の攻8 では範囲で3体倒しても 24/T しか減らない → 攻撃を上げる
// 物差しは cost / gradient と同じ（勝った試行の残HP% → 代金 = 100% − 残HP%）で、
// 媒介変数として決着ターン数を足す（H1 は「ターン数が伸びれば向きが出る」と言っているので、
// ターン数を測らないと H1 の検証にならない）。
//
// **成功条件: 単体 − 範囲 が編成間の標準偏差と同程度以上（およそ 8pt 以上）。**
// 第5期の +3.1pt は「無い」と判定した水準なので、そこを明確に超えたときだけ「向きが出た」
// と言う。H2 には罠がある——1体あたりの攻撃を上げると波が「安く」なくなるが、
// この診断では代金の帯（20〜30%）より向きを優先する（向きが作れると分かってから体数を
// 減らして帯に戻せばよい。逆は不可能）。
//
// 打点の基準について（指示書 §2-1 の「一撃圏」）: docs/pulse.md から実測した1振りあたりの
// 打点は 中央値 10.6 / 四分位 4.4〜20.4 / 上位1割 51.1 / 最大 90.1 で、一撃圏は編成ごとに
// 1〜3発に振れて一意に決まらない。そこで H2 は個体HPを 16 / 24 / 32 と振った3案を並べ、
// どこから向きが出るかを測定で決める（推測で1点に決めない。第6期 §4-7 の停止条件）。
//
// 候補波は gradient と同じくこのモードのローカル変数で組む（Stages / Columns には足さない）。
// 診断用で docs/ には置かない（seats / handoff / cost / gradient と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 aim [絞り込み]
if (focusId == "aim")
{
    var all = CompareBuilds();
    const int AimSeeds = 200;   // gradient と同じ。対照（農兵候補）の検算が成立する条件

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // --- 候補波（第1波の位置だけ。第2波・第3波は第5期のまま触らない） ---
    // 制約は第5期と同じ（1波6体まで / 貫き1枚まで / 全体1枚まで / AttackPattern を増やさない）。
    // 加えて第6期は**新候補に範囲持ちの敵を入れない**——敵側の攻撃型は測定の交絡になる
    // （1c で斧を入れたのは第5期の判断。今回は向きを測るのが目的なので敵は単体で揃える）。
    // 配置は前1→前3→中央→後1→後3 の順に詰める（農兵候補と同じ規則）。
    //
    // 対照3案（1a/1b/1c）は gradient の w1 をそのまま写したもの。値が動いたら測り方が
    // 変わった証拠なので、先へ進まずに止まる（第6期 §2-5 の検算）。
    var cand = new (string Name, Formation Enemy)[]
    {
        ("1a 農兵5（対照）", Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("1b 農兵5（対照）", Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("1c 農兵5+斧（対照）", Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Axeman, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),

        // H1 系: 高HP低攻。体数で総HPを積んで戦闘を伸ばす。H1a→H1c は体数だけの差で、
        // 総HP 270 / 225 / 180 と落ちる（H1c は農兵5と総HPが同じで総攻だけ半分の対照）。
        ("H1a 人足5", Formation.Build(front1: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer, center: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer, back3: EnemyCatalog.Laborer)),
        ("H1b 人足5", Formation.Build(front1: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer, center: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer, back3: EnemyCatalog.Laborer)),
        ("H1c 人足4", Formation.Build(front1: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer, center: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer)),

        // H2 系: 低HP高攻。H2a/H2b/H2c は体数5・総攻 80/T を固定して**個体HPだけ**を
        // 16 / 24 / 32 と振った軸（実測打点中央値の 2 / 3 / 4 発圏）。向きが出るとしたら
        // 「範囲で1手に複数落ちる」HP から出るはずで、その閾値を測定で挟む形。
        // H2d は体数を4に減らした案——向きが出たときに「体数を減らして代金の帯へ戻せるか」
        // （第6期 §3.3-3）を同じ実行で読むために置く。
        ("H2a 裸5(16)", Formation.Build(front1: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare, center: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare, back3: EnemyCatalog.ZealotBare)),
        ("H2b 革5(24)", Formation.Build(front1: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather, center: EnemyCatalog.ZealotLeather, back1: EnemyCatalog.ZealotLeather, back3: EnemyCatalog.ZealotLeather)),
        ("H2c 鎖5(32)", Formation.Build(front1: EnemyCatalog.ZealotMail, front3: EnemyCatalog.ZealotMail, center: EnemyCatalog.ZealotMail, back1: EnemyCatalog.ZealotMail, back3: EnemyCatalog.ZealotMail)),
        ("H2d 革4(24)", Formation.Build(front1: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather, center: EnemyCatalog.ZealotLeather, back1: EnemyCatalog.ZealotLeather)),

        // 中間点: 総HP × 1体あたり攻撃 の2軸で4点目を取る（低HP低攻=農兵 / 高HP低攻=H1 /
        // 低HP高攻=H2 / 中間=これ）。向きが軸のどちら側から出るかを単調性で読むための点。
        ("M1 傭兵5", Formation.Build(front1: EnemyCatalog.Drifter, front3: EnemyCatalog.Drifter, center: EnemyCatalog.Drifter, back1: EnemyCatalog.Drifter, back3: EnemyCatalog.Drifter)),
    };

    Console.WriteLine($"# 安い波の候補診断・代金の向き（seed 0..{AimSeeds - 1} の {AimSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("第1波の位置の候補を、cost / gradient と同じ物差し（勝った試行の残HP% →");
    Console.WriteLine("**代金 = 100% − 残HP%**）で測り、媒介変数として決着ターン数を足したもの。");
    Console.WriteLine();
    Console.WriteLine("**成功条件: `単体 − 範囲` が編成間の標準偏差と同程度以上（およそ 8pt 以上）。**");
    Console.WriteLine("第5期の農兵は +3.1pt で「向きは無い」と判定した水準（1a/1b +3.1pt / 1c +2.4pt）。");
    Console.WriteLine();
    Console.WriteLine("候補波の中身（HP/攻/速/型/配置）:");
    Console.WriteLine();
    foreach (var (name, enemy) in cand)
    {
        var members = enemy.Occupied().Select(x =>
        {
            string pat = x.Def.Pattern switch
            {
                AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き",
                AttackPattern.All => "全体", _ => "単体"
            };
            string[] seat = FormationRules.SeatNames;
            return $"{seat[x.Slot]}={x.Def.Name}({x.Def.MaxHp}/{x.Def.Attack}/速{x.Def.Speed}/{pat})";
        });
        Console.WriteLine($"- **{name}**: {string.Join("、", members)}");
    }
    Console.WriteLine();

    var cells = EmitCostTables(targets, cand, AimSeeds);

    // --- 候補まとめ（第6期 §2-3 の表そのもの） ---
    // 代金の平均・SD は EmitCostTables と同じ集計（勝率 > 0% の編成だけ・母標準偏差）。
    // 単体 − 範囲 は HasAoe による静的区分で、**第5期から定義を変えていない**
    // （+3.1pt と直接比べられることが表の意味なので、ここを触ったら比較が壊れる）。
    // 平均ターン数は勝った試行だけの平均を、さらに編成間で平均したもの。
    Console.WriteLine();
    Console.WriteLine("### 候補まとめ（総HP × 1体あたり攻撃 の2軸と、向き・ターン数）");
    Console.WriteLine();
    Console.WriteLine("| 候補 | 総HP | 総攻/T | 代金平均 | 代金SD | 単体−範囲 | 平均ターン数 | 勝率0%の編成数 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    var split = new double[cand.Length];
    for (int w = 0; w < cand.Length; w++)
    {
        int hp = cand[w].Enemy.Occupied().Sum(x => x.Def.MaxHp);
        int atk = cand[w].Enemy.Occupied().Sum(x => x.Def.Attack);

        var live = Enumerable.Range(0, targets.Length).Where(t => cells[t, w].Wins > 0).ToArray();
        double Cost(int t) => (1 - cells[t, w].AvgHpPct) * 100;
        double mean = live.Average(Cost);
        double sd = Math.Sqrt(live.Average(t => (Cost(t) - mean) * (Cost(t) - mean)));
        double turns = live.Average(t => cells[t, w].AvgTurns);

        var groups = live.GroupBy(t => HasAoe(targets[t].F))
            .ToDictionary(g => g.Key, g => g.Average(Cost));
        double aoe = groups.TryGetValue(true, out double a) ? a : double.NaN;
        double single = groups.TryGetValue(false, out double b) ? b : double.NaN;
        split[w] = single - aoe;

        Console.WriteLine($"| {cand[w].Name} | {hp} | {atk} | {mean:F1}% | {sd:F1}pt "
            + $"| {split[w]:+0.0;-0.0}pt | {turns:F1} | {targets.Length - live.Length} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    int nAoe = targets.Count(t => HasAoe(t.F));
    Console.WriteLine($"範囲持ち {nAoe} 編成 / 単体のみ {targets.Length - nAoe} 編成"
        + "（代金は各群とも勝率 > 0% の編成だけで平均）");
    Console.WriteLine();

    const double AimThreshold = 8.0;
    var won = Enumerable.Range(0, cand.Length).Where(w => split[w] >= AimThreshold).ToArray();
    Console.WriteLine(won.Length == 0
        ? $"**判定: 向きは出ていない。** `単体−範囲` が {AimThreshold:F0}pt 以上の候補は無い。"
        : $"**判定: 向きが出た候補がある** — {string.Join(" / ", won.Select(w => cand[w].Name))}");

    // --- 範囲持ち枚数での単調性（第6期 §2-4） ---
    // HasAoe の二値区分は粗い（薙ぎを1枚持つだけで範囲側に入る）。向きが出た候補について、
    // 枚数 0 / 1 / 2以上 で代金が単調に下がるなら本物、1枚と2枚で差が無いなら区分の副作用を疑う。
    Console.WriteLine();
    if (won.Length == 0)
    {
        Console.WriteLine("### 範囲持ち枚数での単調性");
        Console.WriteLine();
        Console.WriteLine("向きが出た候補が無いので省略（枚数で割っても二値区分より細かい情報は出ない）。");
    }
    else
    {
        Console.WriteLine("### 範囲持ち枚数での単調性（向きが出た候補のみ）");
        Console.WriteLine();
        Console.WriteLine("| 候補 | 0枚 | 1枚 | 2枚以上 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (int w in won)
        {
            var by = Enumerable.Range(0, targets.Length).Where(t => cells[t, w].Wins > 0)
                .GroupBy(t => Math.Min(2, AoeCount(targets[t].F)))
                .ToDictionary(g => g.Key,
                    g => (Cost: g.Average(t => (1 - cells[t, w].AvgHpPct) * 100), N: g.Count()));
            string Cell(int k) => by.TryGetValue(k, out var v) ? $"{v.Cost:F1}%（{v.N}編成）" : "—";
            Console.WriteLine($"| {cand[w].Name} | {Cell(0)} | {Cell(1)} | {Cell(2)} |");
        }
    }
    return;
}

if (focusId == "flip") { FlipDiag.Run(args, stageIndex); return; }

if (focusId == "bridge") { BridgeDiag.Run(args, stageIndex); return; }

// bill モード: 代金を「自傷分」と「被弾分」に分解する診断（第9期 Phase X）。
//
// cost / gradient / aim / flip / bridge が測ってきた代金は「失った HP の割合」という
// 一つの数字で、内訳が無い。第5期に目視で見えた「自傷の固定費」（死の連鎖系・惨禍系が
// 第1波でも代金 50% 付近、逆しま系・移動系は 13〜22%）が本当に自傷なのかは、
// 編成名から見た印象でしか裏付けられていない。ここを数字にする。
//
//     失ったHP  =  敵由来の被ダメ  +  味方由来の被ダメ  −  回復  +  残差
//
// - 味方由来 = 自傷分（UnitTally.TakenFromAlly。破裂・生贄・吸いはここに出る）
// - 敵由来   = DamageTaken − TakenFromAlly
// - 回復     = UnitTally.Healed（第9期に足した。実際に増えた分だけ）
// - 残差     = 上記3つを通らずに HP が動いた分と、過剰殺傷（HP が 0 未満に沈む分）
//
// **残差は誤差ではなく検出器。** 大きければ代金の一部が tally の外で動いているという
// ことで、分解そのものが信用できない（第9期 §2-2）。目安として定義上の総最大HPの 5%。
//
// 台は第8期の 113% 列（反転列(低)）。**合計代金 113% が結果の敏感な唯一の帯**で、
// 136% で測ると全編成が潰れて何も見えない（第6〜8期の結論。第9期 §0）。
// 味方1部隊で会戦を回すので、単発戦の cost と違って**自傷が部隊戦ごとに積み上がるか**も見える。
// 診断用で docs/ には置かない（seats / handoff / cost / gradient / aim / flip と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 bill [絞り込み]
if (focusId == "bill")
{
    var all = CompareBuilds();
    const int BillSeeds = 200;   // cost / gradient / aim / flip / bridge と同じ

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    Formation[] column = BenchColumn113();

    Console.WriteLine($"# 代金の分解（測定台 113% = 反転列(低)・味方1部隊・seed 0..{BillSeeds - 1}）");
    Console.WriteLine();
    Console.WriteLine("列は 第8期の 反転列(低)（H2a 裸5 / 2b 騎士混成 / 巡礼5）。合計代金 113% の測定台。");
    Console.WriteLine("**失ったHP = 敵由来 + 自傷分 − 回復 + 残差**、分母はすべて編成の定義上の総最大HP。");
    Console.WriteLine("`代金合計` は会戦を終えた時点で失っていた HP の割合（勝敗を問わず全試行の平均。");
    Console.WriteLine("cost の代金が「勝った試行だけ」なのと違う——負けた試行を外すと自傷型が");
    Console.WriteLine("いちばん払っている場面が丸ごと分母から消える）。");
    Console.WriteLine();
    Console.WriteLine("**`自傷率` = 自傷分 ÷ (敵由来 + 自傷分)**。払った HP のうち何割を自分で削ったか。");
    Console.WriteLine("Phase Y で編成を群分けする連続量はこれ。");
    Console.WriteLine();

    // 敵と味方で Def.Id が衝突していると、Def.Id で引く tally が敵の被弾を味方に混ぜてしまう。
    // 起きていないはずだが、起きたら分解が黙って壊れるので毎回確かめる。
    var enemyIds = column.SelectMany(w => w.Occupied().Select(x => x.Def.Id)).ToHashSet();
    var clash = targets.SelectMany(t => t.F.Occupied().Select(x => x.Def.Id)).Where(enemyIds.Contains).Distinct().ToArray();
    Console.WriteLine(clash.Length == 0
        ? "**検算（ID の衝突）: 味方と敵で重複する Def.Id は無い**（tally は Def.Id で引くので必須）。"
        : "**衝突あり: " + string.Join(" / ", clash) + " — 分解が敵の被弾を混ぜている。読んではいけない。**");
    Console.WriteLine();

    var rows = targets.Select(t => (t.Name, t.F, Bill: MeasureBill(t.F, column, BillSeeds)))
        .OrderByDescending(x => x.Bill.SelfHarmRate)
        .ToArray();

    Console.WriteLine("## 分解（自傷率の降順）");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 範 | 代金合計 | 敵由来 | 自傷分 | 回復 | 残差 | 自傷率 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|");
    foreach (var (name, f, b) in rows)
        Console.WriteLine($"| {name} | {(HasAoe(f) ? "○" : "")} | {b.Lost:F1}% | {b.Enemy:F1}% "
            + $"| {b.Ally:F1}% | {b.Heal:F1}% | {b.Residual:+0.0;-0.0}% | {b.SelfHarmRate * 100:F1}% |");
    Console.Out.Flush();

    // --- 残差（分解が信用できるか） ---
    var worst = rows.OrderByDescending(x => Math.Abs(x.Bill.Residual)).First();
    Console.WriteLine();
    Console.WriteLine($"**残差の絶対値の最大 = {Math.Abs(worst.Bill.Residual):F1}%（{worst.Name}）。**"
        + $" 全編成の平均 {rows.Average(x => x.Bill.Residual):+0.0;-0.0}%、"
        + $"5% を超えた編成 {rows.Count(x => Math.Abs(x.Bill.Residual) > 5)}/{rows.Length}。");
    Console.WriteLine();
    Console.WriteLine("残差の出どころ（ApplyDamage / Heal を通らずに HP が動く経路）は3つ:");
    Console.WriteLine("過剰殺傷（HP が 0 未満に沈んだ分。tally は振り切った量を数え、失った HP は 0 で止まる → **マイナス**）、");
    Console.WriteLine("継ぎ当て（ミオ）の自己出血（`self.Hp -= amount` が直接 HP を引く → **プラス**）、");
    Console.WriteLine("蘇生と継ぎ接ぎ（`Revive` の HP 付与と、縫った側の最大HP半減に伴う切り詰め）。");

    // --- 自傷率の分布 ---
    var sh = rows.Select(x => x.Bill.SelfHarmRate * 100).OrderBy(x => x).ToArray();
    double shMean = sh.Average();
    double shSd = Math.Sqrt(sh.Average(x => (x - shMean) * (x - shMean)));
    Console.WriteLine();
    Console.WriteLine("## 自傷率の分布");
    Console.WriteLine();
    Console.WriteLine($"編成数 {sh.Length} / 最小 {sh[0]:F1}% / 中央 {sh[sh.Length / 2]:F1}% / 最大 {sh[^1]:F1}% "
        + $"/ 平均 {shMean:F1}% / 標準偏差 {shSd:F1}pt");
    Console.WriteLine();
    Console.WriteLine("三分位の境目: "
        + $"下位1/3 ≤ {sh[sh.Length / 3]:F1}% < 中位1/3 ≤ {sh[sh.Length * 2 / 3]:F1}% < 上位1/3");
    Console.WriteLine();
    Console.WriteLine("**ほぼ一様（標準偏差が小さい）なら Phase Y の群分けは意味を持たない**（第9期 §5-7）。");

    // --- 部隊戦ごとの積み上がり ---
    // HP は会戦を跨ぐ唯一の持ち越し資源なので、自傷が毎戦繰り返されるなら自傷型は
    // 単発戦では成立しても会戦では二重に課金される。cost は単発戦しか測っていない（§2-4）。
    Console.WriteLine();
    Console.WriteLine("## 自傷分の部隊戦ごとの推移（到達した試行だけの平均・到達率併記）");
    Console.WriteLine();
    Console.WriteLine("分母は定義上の総最大HP。第2戦・第3戦は**そこまで生き延びた試行だけ**の平均なので、");
    Console.WriteLine("到達率が低い編成の値は少数の試行から出ている（到達率が 0% なら `—`）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 自傷率 | 第1戦 自傷 | 第2戦 自傷（到達率） | 第3戦 自傷（到達率） | 第1戦 敵由来 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    foreach (var (name, _, b) in rows)
    {
        string Cell(int i) => b.Reached[i] == 0
            ? "—"
            : $"{b.AllyByBattle[i]:F1}%（{b.Reached[i] * 100.0 / BillSeeds:F0}%）";
        Console.WriteLine($"| {name} | {b.SelfHarmRate * 100:F1}% | {(b.Reached[0] == 0 ? "—" : $"{b.AllyByBattle[0]:F1}%")} "
            + $"| {Cell(1)} | {Cell(2)} | {(b.Reached[0] == 0 ? "—" : $"{b.EnemyByBattle[0]:F1}%")} |");
    }
    Console.Out.Flush();

    // --- 単発戦との突き合わせ（cost 側の物差しと繋がっているか） ---
    // 会戦の第1戦は「無傷の1部隊が第1波だけと戦う」状況そのものなので、cost の代金と
    // 揃うはず。ただし seed は揃わない（会戦は DeriveSeed で seed*1000003 に散らす）ので
    // 一致ではなく近似——大きくずれたら物差しが繋がっていない。
    Console.WriteLine();
    Console.WriteLine("## 検算: 第1戦の代金 と cost の代金（別 seed の同じ状況）");
    Console.WriteLine();
    Console.WriteLine("`第1戦の代金` は会戦の第1戦を終えた時点で失っていた HP（勝った試行だけ）。");
    Console.WriteLine("`cost の代金` は同じ波の単独戦を seed 0..199 で測ったもの（100% − 残HP%）。");
    Console.WriteLine("**seed が違うので一致はしない。**数 pt のずれは試行の散らばり、大きなずれは物差しのずれ。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 第1戦の代金 | cost の代金 | 差 |");
    Console.WriteLine("|---|--:|--:|--:|");
    double maxGapCost = 0;
    foreach (var (name, f, b) in rows)
    {
        var m = MeasureCost(f, column[0], BillSeeds);
        if (b.WonFirst == 0 || m.Wins == 0) { Console.WriteLine($"| {name} | — | — | — |"); continue; }
        double a = b.FirstWinCost, c = (1 - m.AvgHpPct) * 100;
        maxGapCost = Math.Max(maxGapCost, Math.Abs(a - c));
        Console.WriteLine($"| {name} | {a:F1}% | {c:F1}% | {a - c:+0.0;-0.0}pt |");
    }
    Console.WriteLine();
    Console.WriteLine($"**差の絶対値の最大 = {maxGapCost:F1}pt。**");
    return;
}

if (focusId == "charge") { ChargeDiag.Run(args, stageIndex); return; }

if (focusId == "timing") { TimingDiag.Run(args, stageIndex); return; }

if (focusId == "power") { PowerDiag.Run(args, stageIndex); return; }

if (focusId == "bench") { BenchDiag.Run(args, stageIndex); return; }

if (focusId == "wave") { WaveDiag.Run(args, stageIndex); return; }

if (focusId == "dissect") { DissectDiag.Run(args, stageIndex); return; }

if (focusId == "output") { OutputDiag.Run(args, stageIndex); return; }

if (focusId == "convert") { ConvertDiag.Run(args, stageIndex); return; }

if (focusId == "chain")
{
    var builds = CompareBuilds();
    const int ChainSeeds = 200;

    Console.WriteLine("# 連鎖の深さ");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 chain > docs/chain.md` の出力。手で編集しない。");
    Console.WriteLine($"代表編成 × 全ステージ、seed 0..{ChainSeeds - 1} の {ChainSeeds} 試行。全ステージ通算。");
    Console.WriteLine("`連鎖深度`は1ターンで味方が倒した敵の数の最大値（全試行平均 / 最大値）。");
    Console.WriteLine("`決着T`は勝利した試行だけの平均ターン数（短いほど速攻で畳んでいる）。");
    Console.WriteLine();
    Console.WriteLine("`残存`は**勝った試行だけ**の生存数（平均 / 出撃数）。**勝ち方の質**を測る列で、");
    Console.WriteLine("低いほど「なんとか勝った」になる。勝率が同じでも、5体残して勝つ編成と");
    Console.WriteLine("1体残して勝つ編成は別物だが、勝率表では区別がつかない。");
    Console.WriteLine("`全滅勝ち`は生存1体での勝率（勝った試行のうち何%がぎりぎりだったか）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 勝率 | 連鎖深度(平均) | 連鎖深度(最大) | 決着T(勝利時平均) | 残存 | 全滅勝ち |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");

    // === 第140期 —— 行（編成）ごとに並列化した。**1行の中は従来どおり波→seed の順**なので、
    // 浮動小数の合算順序も変わらない。印字は控えて後で直列に出す。 ===
    var chRow = new string[builds.Length];
    Parallel.For(0, builds.Length, bi =>
    {
        (string name, Formation f) = builds[bi];
        int wins = 0, trials = 0;
        double killSum = 0;
        int killMax = 0;
        double turnSumOnWin = 0;
        double survSumOnWin = 0;
        int narrowWins = 0;
        int party = f.Occupied().Count();

        foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
        {
            for (int seed = 0; seed < ChainSeeds; seed++)
            {
                var r = BattleEngine.Run(f, st.Enemy, seed, verbose: false);
                trials++;
                killSum += r.MaxEnemyKillsInOneTurn;
                if (r.MaxEnemyKillsInOneTurn > killMax) killMax = r.MaxEnemyKillsInOneTurn;
                if (r.PlayerWon)
                {
                    wins++;
                    turnSumOnWin += r.Turns;
                    survSumOnWin += r.PlayerSurvivors;
                    if (r.PlayerSurvivors <= 1) narrowWins++;
                }
            }
        }

        double winRate = wins * 100.0 / trials;
        double killAvg = killSum / trials;
        double turnAvgOnWin = wins > 0 ? turnSumOnWin / wins : 0;
        double survAvg = wins > 0 ? survSumOnWin / wins : 0;
        double narrow = wins > 0 ? narrowWins * 100.0 / wins : 0;
        chRow[bi] = $"| {name} | {winRate:F1}% | {killAvg:F2} | {killMax} | {turnAvgOnWin:F1} "
            + $"| {survAvg:F1}/{party} | {narrow:F0}% |";
    });
    foreach (string row in chRow) Console.WriteLine(row);
    return;
}

if (focusId == "run") { RunDiag.Run(args, stageIndex); return; }

if (focusId == "choice") { ChoiceDiag.Run(args, stageIndex); return; }

if (focusId == "recover") { RecoverDiag.Run(args, stageIndex); return; }

if (focusId == "ablate") { AblateDiag.Run(args, stageIndex); return; }

if (focusId == "confirm") { ConfirmDiag.Run(args, stageIndex); return; }

if (focusId == "seats2") { Seats2Diag.Run(args, stageIndex); return; }

if (focusId == "reseat")
{
    var all = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> stages = EnemyCatalog.Stages;
    const int ScanSeeds = 50;    // 候補を絞るための粗い探索。layout と揃える
    const int VerifySeeds = 200; // 採否を決める測り直し。compare と揃える
    const int TopOverall = 20;
    const int TopConstrained = 10;

    // 対象は既定では compare の全編成。args[2] にカンマ区切りの部分一致を渡すと絞れる。
    // 固定リストにしていた頃は「いつ作ったリストか」が読めず、盤面や波を変えたあとも
    // 古い顔ぶれのまま回してしまう。絞り込みは呼び出し側で明示する。
    string filter = args.Length > 2 ? args[2] : "";
    var targets = all.Select(b => b.Name)
        .Where(n => filter.Length == 0
                    || filter.Split(',').Any(k => n.Contains(k.Trim())))
        .ToArray();

    // 長時間ジョブは前景で待ち切るしかない（背景に回すと起動元のコマンド終了で刈られる）。
    // 一回の呼び出しに収まる分だけを回せるよう、対象を切り出せるようにしてある。
    int skip = args.Length > 3 && int.TryParse(args[3], out int sk) ? sk : 0;
    int take = args.Length > 4 && int.TryParse(args[4], out int tk) ? tk : targets.Length;
    targets = targets.Skip(skip).Take(take).ToArray();

    Console.WriteLine("# 配置の測り直し");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{ScanSeeds - 1} の全配置探索で候補を絞り、seed 0..{VerifySeeds - 1} で測り直した。");
    Console.WriteLine("`狙`列: ガルドが前列 / セッキが後列 を満たすか（その駒を含む編成のみ）。");

    // === 第140期 —— 戦闘を2つの平坦な並列パスに畳んだ（器具の期・値は1ビットも変えない） ===
    //
    // **`perms` / `order` / `pool` / `verified` の作り方は1文字も変えていない。**
    // 変えたのは「どの順で戦闘を走らせるか」だけで、`BattleEngine.Run` は seed 決定的な純関数
    // （副作用も外部依存もない）なので、どのスレッドで走らせても同じ seed は同じ結果を返す。
    //
    // **各ジョブは自分の添字にしか書かない**（共有の `List` に `Add` しない）ので回収に同期は要らず、
    // **印字は従来どおり直列に、`targets` の順・`pool` の順で行う**——出力はスレッドの
    // スケジューリングに依存しない。**平均も `double[]` の添字順に足す**ので浮動小数も同一。
    //
    // 行ごとに `Parallel.For` を 61 回立てるのではなく**全行ぶんを1つの平坦なジョブ表に畳む**のは、
    // 1行あたり 120 ジョブでは末尾でコアが余るため（`layout` の粗探索と同じ形）。
    var rsPerms = new List<Formation>[targets.Length];
    for (int t = 0; t < targets.Length; t++)
    {
        var members = all.First(b => b.Name == targets[t]).F.Occupied().Select(x => x.Def).ToList();
        var ps = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation();
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            ps.Add(f);
        }
        rsPerms[t] = ps;
    }

    // (a) 粗探索（seed 0..ScanSeeds-1 の全配置）。
    var rsScan = new int[targets.Length][];
    var rsScanJobs = new List<(int T, int I)>();
    for (int t = 0; t < targets.Length; t++)
    {
        rsScan[t] = new int[rsPerms[t].Count];
        for (int i = 0; i < rsPerms[t].Count; i++) rsScanJobs.Add((t, i));
    }
    Parallel.For(0, rsScanJobs.Count, j =>
    {
        var (t, i) = rsScanJobs[j];
        int wins = 0;
        foreach (EnemyCatalog.Stage st in stages)
            for (int seed = 0; seed < ScanSeeds; seed++)
                if (BattleEngine.Run(rsPerms[t][i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
        rsScan[t][i] = wins;
    });

    // (b) 候補の選び方は従来と同一。**戦闘を1回も回さない**ので直列のまま。
    var rsOrder = new List<int>[targets.Length];
    var rsPool = new List<int>[targets.Length];
    for (int t = 0; t < targets.Length; t++)
    {
        var perms = rsPerms[t];
        var scan = rsScan[t];
        var build = all.First(b => b.Name == targets[t]);
        rsOrder[t] = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
        rsPool[t] = rsOrder[t].Take(TopOverall)
            .Concat(rsOrder[t].Where(i => MeetsIntent(perms[i])).Take(TopConstrained))
            .Append(rsOrder[t].First(i => SameFormation(perms[i], build.F)))
            .Distinct().ToList();
    }

    // (c) 測り直し（seed 0..VerifySeeds-1）。**波までジョブに割る**ので粒度が揃う。
    var rsCells = new double[targets.Length][][];
    var rsVerJobs = new List<(int T, int P, int St)>();
    for (int t = 0; t < targets.Length; t++)
    {
        rsCells[t] = new double[rsPool[t].Count][];
        for (int p = 0; p < rsPool[t].Count; p++)
        {
            rsCells[t][p] = new double[stages.Count];
            for (int st = 0; st < stages.Count; st++) rsVerJobs.Add((t, p, st));
        }
    }
    Parallel.For(0, rsVerJobs.Count, j =>
    {
        var (t, p, st) = rsVerJobs[j];
        int wins = 0;
        for (int seed = 0; seed < VerifySeeds; seed++)
            if (BattleEngine.Run(rsPerms[t][rsPool[t][p]], stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
        rsCells[t][p][st] = wins * 100.0 / VerifySeeds;
    });

    // (d) 印字。**従来の foreach の中身をそのまま持ってきてある。**
    for (int t = 0; t < targets.Length; t++)
    {
        string name = targets[t];
        var build = all.First(b => b.Name == name);
        var perms = rsPerms[t];
        var order = rsOrder[t];

        // `pool.Select(...).OrderByDescending(x => x.Avg)` と同じ——LINQ の OrderBy は安定ソートなので、
        // 同値は `pool` の順（＝従来と同じ順）で残る。
        var verified = Enumerable.Range(0, rsPool[t].Count)
            .Select(p => (Idx: rsPool[t][p], Cells: rsCells[t][p], Avg: rsCells[t][p].Average()))
            .OrderByDescending(x => x.Avg).ToList();

        Console.WriteLine();
        Console.WriteLine($"## {name}");
        Console.WriteLine();
        Console.WriteLine("| 粗順 | 狙 | 前1/前3 | 中央 | 後1/後3 | 平均 |"
            + string.Concat(stages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|--:|:-:|---|---|---|--:|" + string.Concat(stages.Select(_ => "---:|")));
        foreach (var v in verified)
        {
            Formation f = perms[v.Idx];
            static string N(UnitDef? d) => d?.Name ?? "−";
            bool isCur = SameFormation(f, build.F);
            string rank = $"{order.IndexOf(v.Idx) + 1}" + (isCur ? "★現行" : "");
            Console.WriteLine($"| {rank} | {(MeetsIntent(f) ? "○" : "×")} | {N(f[0])}/{N(f[1])} | {N(f[2])} "
                + $"| {N(f[3])}/{N(f[4])} | {v.Avg:F1}% |" + string.Concat(v.Cells.Select(c => $" {c:F1}% |")));
        }
        Console.Out.Flush();
    }

    bool MeetsIntent(Formation f)
    {
        foreach (var (slot, def) in f.Occupied())
        {
            if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
            if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
        }
        return true;
    }
    return;
}

if (focusId == "layout") { LayoutDiag.Run(args, stageIndex); return; }

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
    // 行動列は「説明文と挙動のズレ」を防ぐための列（過去4回発生）。Actions を持たない駒は
    // 空欄——味方は全員そちらなので、この表の見た目は第9期までと変わらない。
    static string Acts(UnitDef u) => u.Actions is null
        ? ""
        : string.Join(" → ", u.Actions.Select(a => a.Kind switch
        {
            ActionKind.Charge => a.Label ?? "溜め",
            ActionKind.Skill => a.Label ?? "術",
            _ => a.AttackPercent == 100 ? "攻撃" : $"攻撃×{a.AttackPercent}%"
        }));

    // 踏込 列は**表示専用**（第131期）。engine に射程という軸は無く、この札を読んで分岐する
    // 規則は 0 件——`DemoApp` が「敵の前まで出て振るか、その場から振るか」を選ぶためだけにある。
    // **列は末尾側に足すこと**——`checkup check` の (a) は `docs/units.md` の
    // 2〜4 列目（HP / 攻 / 速）を位置で読むので、前に挟むとその自己検査が壊れる。
    static string Adv(UnitDef u) => u.Advances ? "踏込" : "据置";

    Console.WriteLine("| 名前 | HP | 攻 | 速 | 型 | 踏込 | 行動 | プラス | マイナス | 由来 |");
    Console.WriteLine("|---|---:|---:|---:|---|---|---|---|---|---|");
    foreach (UnitDef u in UnitCatalog.All.Where(u => u.Id != "spore"))
        Console.WriteLine($"| **{u.Name}** | {u.MaxHp} | {u.Attack} | {u.Speed} | {Pat(u.Pattern)} | {Adv(u)} | {Acts(u)} | {u.PlusText} | {u.MinusText} | {u.Flavor} |");

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
        var e = st.Enemy.Occupied().Select(x =>
            $"{x.Def.Name}(HP{x.Def.MaxHp}/攻{x.Def.Attack}/{Pat(x.Def.Pattern)}/{Adv(x.Def)}"
            + (x.Def.Actions is null ? "" : $"/{Acts(x.Def)}") + ")");
        Console.WriteLine($"- **{st.Name}**: {string.Join("、", e)}");
    }
    return;
}

EnemyCatalog.Stage stage = EnemyCatalog.Stages[stageIndex];
Console.WriteLine($"対象ステージ: {stage.Name}\n");

// demo モード: 特定の編成のログだけを見る
// ptrace モード: 毒軸の立ち上がりを見る。層は減衰しないので累積ダメージは時間の二乗で効く。
// 「間に合っていないのか、そもそも足りないのか」を切り分けるための道具。
// 各ターンの敵の総層数・敵の残数・味方の残数を並べ、決着ターンと突き合わせる。
// life モード: 注目する駒の**寿命**を測る（第19期）。
//
// 第五波でカドを含む編成が低いことは分かっているが、**反撃が出すぎているのか、
// そもそも出せていないのかが分かっていない。** 早期に落ちているなら原因は回数ではなく寿命で、
// 回数制は逆方向の修正になる。切り分けてから設計に進むための測定。
//
// **測定だけで、盤面は1つも動かしていない**（`UnitTally.LastActiveTurn` は書くだけで、
// 誰も読んで分岐しない。`compare` は ±0.0）。
//
//     dotnet run --project BattleSim -c Release 0 life [絞り込み] [駒Id]
if (focusId == "life")
{
    const int LifeSeeds = 200;   // compare / power / bench と同じ

    var all = CompareBuilds();
    string filter = args.Length > 2 ? args[2] : "";
    string unitId = args.Length > 3 ? args[3] : "kado";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    IReadOnlyList<EnemyCatalog.Stage> stages = EnemyCatalog.Stages;

    // 1編成 × 1波 の集計。注目駒（focus）と、基準線用の「最も早く落ちた駒」を同時に取る。
    //
    // 初落 の同値は**同着として全員に数える**ので、注目駒の 初落% と基準線は排他ではない。
    // 基準線側の 干渉 も、同着した駒の平均を取る（1体に絞ると席順で選ぶことになる）。
    (double Live, double End, double FirstPct, double DeadPct, double Intv, double MinLive, double MinIntv, bool Has)
        Measure(Formation f, Formation enemy, string id)
    {
        var allyIds = f.Occupied().Select(x => x.Def.Id).ToHashSet();
        bool has = allyIds.Contains(id);

        double live = 0, end = 0, first = 0, dead = 0, intv = 0, minLive = 0, minIntv = 0;
        for (int seed = 0; seed < LifeSeeds; seed++)
        {
            BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
            end += r.Turns;

            int lo = int.MaxValue;
            foreach (string a in allyIds)
                if (r.TallyByUnit.TryGetValue(a, out UnitTally? t)) lo = Math.Min(lo, t.LastActiveTurn);

            // 最も早く落ちた駒（同着は平均）
            int tie = 0; double tieIntv = 0;
            foreach (string a in allyIds)
                if (r.TallyByUnit.TryGetValue(a, out UnitTally? t) && t.LastActiveTurn == lo)
                { tie++; tieIntv += t.Interventions; }
            minLive += lo;
            minIntv += tie > 0 ? tieIntv / tie : 0;

            if (has && r.TallyByUnit.TryGetValue(id, out UnitTally? ft))
            {
                live += ft.LastActiveTurn;
                intv += ft.Interventions;
                if (ft.LastActiveTurn == lo) first++;
                if (ft.Deaths > 0) dead++;
            }
        }

        return (live / LifeSeeds, end / LifeSeeds, 100.0 * first / LifeSeeds, 100.0 * dead / LifeSeeds,
                intv / LifeSeeds, minLive / LifeSeeds, minIntv / LifeSeeds, has);
    }

    string Row(string head, double live, double end, double pct, double deadPct, double intv)
        => $"| {head} | {live:F2} | {end:F2} | {live / end:F2} | {pct:F1}% | {deadPct:F1}% | "
           + $"{(live > 0 ? intv / live : 0):F2} |";

    Console.WriteLine($"# 寿命を測る — {unitId} の稼働率（第19期）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 × 全{stages.Count}波、seed 0..{LifeSeeds - 1}。`UnitTally.LastActiveTurn` を読むだけの測定。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 定義 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 生存T | `LastActiveTurn` の平均（倒れたターン。生き残れば決着ターン） |");
    Console.WriteLine("| 決着T | その試行の決着ターンの平均 |");
    Console.WriteLine("| 稼働率 | 生存T ÷ 決着T |");
    Console.WriteLine("| 初落% | 味方の中でこの駒の `LastActiveTurn` が最小だった試行の割合（同値は同着） |");
    Console.WriteLine("| 落% | この駒が実際に倒れた試行の割合 |");
    Console.WriteLine();
    Console.WriteLine("**全員が生き残った試行では全員が同着**になるので、`初落%` は `落%` と一緒に読む");
    Console.WriteLine("（`落% ≒ 0` なのに `初落% ≒ 100%` なら、それは「誰も落ちていない」を意味する）。");
    Console.WriteLine("| 干渉/T | `Interventions` ÷ 生存T |");
    Console.WriteLine();
    Console.WriteLine($"`AbsorbCap` = {ThornGuardTrait.AbsorbCap}");
    Console.WriteLine();

    var withFocus = targets.Where(b => b.F.Occupied().Any(x => x.Def.Id == unitId)).ToArray();
    var without = targets.Where(b => b.F.Occupied().All(x => x.Def.Id != unitId)).ToArray();

    Console.WriteLine($"## 1. {unitId} を含む {withFocus.Length} 編成");
    Console.WriteLine();
    foreach ((string name, Formation f) in withFocus)
    {
        Console.WriteLine($"### {name}");
        Console.WriteLine();
        Console.WriteLine("| 波 | 生存T | 決着T | 稼働率 | 初落% | 落% | 干渉/T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int st = 0; st < stages.Count; st++)
        {
            var m = Measure(f, stages[st].Enemy, unitId);
            Console.WriteLine(Row(stages[st].Name, m.Live, m.End, m.FirstPct, m.DeadPct, m.Intv));
        }
        Console.WriteLine();
    }

    Console.WriteLine($"## 2. 基準線: {unitId} を含まない {without.Length} 編成");
    Console.WriteLine();
    Console.WriteLine("**その編成で最も早く落ちた駒**（試行ごとに選び直す。同着は平均）についての同じ列。");
    Console.WriteLine("`初落%` は定義上 100% なので出さない。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 波 | 生存T | 決着T | 稼働率 | 干渉/T |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|");
    var byWave = new List<double>[stages.Count];
    for (int st = 0; st < stages.Count; st++) byWave[st] = new List<double>();
    foreach ((string name, Formation f) in without)
        for (int st = 0; st < stages.Count; st++)
        {
            var m = Measure(f, stages[st].Enemy, unitId);
            double rate = m.MinLive / m.End;
            byWave[st].Add(rate);
            Console.WriteLine($"| {name} | {stages[st].Name} | {m.MinLive:F2} | {m.End:F2} | {rate:F2} | "
                              + $"{(m.MinLive > 0 ? m.MinIntv / m.MinLive : 0):F2} |");
        }

    Console.WriteLine();
    Console.WriteLine("### 稼働率の分布（波ごと・最も早く落ちた駒）");
    Console.WriteLine();
    Console.WriteLine("| 波 | 最小 | 中央 | 最大 | 平均 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int st = 0; st < stages.Count; st++)
    {
        var v = byWave[st].OrderBy(x => x).ToArray();
        if (v.Length == 0) continue;
        double med = v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
        Console.WriteLine($"| {stages[st].Name} | {v[0]:F2} | {med:F2} | {v[^1]:F2} | {v.Average():F2} |");
    }

    // 注目駒を含む編成の側も、波ごとにまとめる（第一波と第五波の対比を1つの表で読むため）
    Console.WriteLine();
    Console.WriteLine($"## 3. {unitId} の波ごとの平均（含む {withFocus.Length} 編成の平均）");
    Console.WriteLine();
    Console.WriteLine("| 波 | 生存T | 決着T | 稼働率 | 初落% | 落% | 干渉/T |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    for (int st = 0; st < stages.Count; st++)
    {
        double live = 0, end = 0, pct = 0, dpct = 0, intv = 0;
        foreach ((_, Formation f) in withFocus)
        {
            var m = Measure(f, stages[st].Enemy, unitId);
            live += m.Live; end += m.End; pct += m.FirstPct; dpct += m.DeadPct; intv += m.Intv;
        }
        int n = Math.Max(1, withFocus.Length);
        Console.WriteLine(Row(stages[st].Name, live / n, end / n, pct / n, dpct / n, intv / n));
    }
    return;
}

if (focusId == "cross") { CrossDiag.Run(args, stageIndex); return; }


if (focusId == "ptrace")
{
    string want = args.Length > 2 ? args[2] : "毒 (グザ";
    var builds = CompareBuilds();
    var (name, f) = builds.First(b => b.Name.Contains(want));

    Console.WriteLine($"# 毒の立ち上がり: {name}");
    for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
    {
        Console.WriteLine();
        Console.WriteLine($"## {EnemyCatalog.Stages[st].Name}");
        Console.WriteLine();
        Console.WriteLine("| ターン | 敵の総層数 | 敵残 | 味方残 | 味方の総層数 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|");

        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed: 0, verbose: true);
        var enemyNames = EnemyCatalog.Stages[st].Enemy.Occupied().Select(x => x.Def.Name).ToHashSet();
        var allyNames = f.Occupied().Select(x => x.Def.Name).ToHashSet();

        int turn = 0, ep = 0, ap = 0;
        var deadE = new HashSet<string>();
        var deadA = new HashSet<string>();
        int nE = EnemyCatalog.Stages[st].Enemy.Count, nA = f.Count;

        void Flush()
        {
            if (turn > 0)
                Console.WriteLine($"| {turn} | {ep} | {nE - deadE.Count} | {nA - deadA.Count} | {ap} |");
        }

        foreach (LogLine line in r.Log)
        {
            string ln = line.ToString();
            if (ln.Contains("--- ターン ")) { Flush(); turn++; ep = 0; ap = 0; continue; }
            if (ln.Contains("は毒に蝕まれている"))
            {
                int a = ln.IndexOf('（'), b = ln.IndexOf('）');
                if (a >= 0 && b > a && int.TryParse(ln[(a + 1)..b], out int n))
                {
                    if (enemyNames.Any(e => ln.Contains(e + " は毒"))) ep += n;
                    else if (allyNames.Any(e => ln.Contains(e + " は毒"))) ap += n;
                }
                continue;
            }
            if (ln.Contains("倒れた") || ln.Contains("死亡"))
            {
                foreach (string e in enemyNames) if (ln.Contains(e)) deadE.Add(e);
                foreach (string e in allyNames) if (ln.Contains(e)) deadA.Add(e);
            }
        }
        Flush();
        Console.WriteLine();
        Console.WriteLine($"結果: {(r.PlayerWon ? "勝利" : "敗北")} / {r.Turns}ターン");
    }
    return;
}

if (focusId == "demo")
{
    // 第3引数に編成名の部分一致を渡すと、compare の編成をそのまま1戦ぶん詳細ログで流す
    // （`... <n> demo "仇討ち"`）。省略時は従来どおり下の固定編成。
    // 新しい特性が**実際に発火しているか**はログの並びでしか読めない——勝率は
    // 「発火したが足りなかった」と「一度も発火しなかった」を区別しない。
    string demoWant = args.Length > 2 ? args[2] : "";
    int demoSeed = args.Length > 3 && int.TryParse(args[3], out int ds) ? ds : 7;
    if (demoWant.Length > 0)
    {
        var (demoName, demoF) = CompareBuilds().FirstOrDefault(b => b.Name.Contains(demoWant));
        if (demoF is null) { Console.Error.WriteLine($"編成が見つからない: {demoWant}"); return; }
        BattleResult picked = BattleEngine.Run(demoF, stage.Enemy, demoSeed, verbose: true);
        Console.WriteLine($"# {demoName} / 第{stageIndex + 1}波 / seed {demoSeed}");
        foreach (LogLine line in picked.Log) Console.WriteLine(line);
        Console.WriteLine($"結果: {(picked.PlayerWon ? "勝利" : "敗北")} / {picked.Turns}ターン");
        return;
    }

    var build = Formation.Build(
        front1: UnitCatalog.Kado,   // 反撃。範囲で返す
        front3: UnitCatalog.Hisa,   // 標的を付けてカドに殴らせる
        center: UnitCatalog.Gald,   // 壁。中央は前列が割れるまで単体攻撃が届かない席
        back1:  UnitCatalog.Hagi,   // 追い打ち。誰かが倒すと割り込む
        back3:  UnitCatalog.Gan     // 号令。動かないカドの攻撃を積む
    );
    BattleResult demo = BattleEngine.Run(build, stage.Enemy, demoSeed, verbose: true);
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
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++) f[i] = slots[i];

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











// ---- 波の代金診断（第5期 cost / gradient が共有） ----
























    }
}





/// <summary>
/// 参照台1つ × 編成1つぶんの出力（第17期 Phase HA）。<see cref="MeasureOutput"/> が埋める。
///
/// **seed ごとの生の値を持っているのが要点。** 半割（測定の信頼性の上限）を
/// 同じ計測から取り出すために要る——2回走らせると、半割の値そのものに実行間の
/// ばらつきが乗る（第13期 <c>bench</c> と同じ作法）。
///
/// **どの列も盤面には一切影響しない。** verbose=true の <c>Events</c> を読み直しているだけ。
/// </summary>
sealed class OutputTrace
{
    /// <summary>立ち上がりを見る範囲。T1 / T3 / T5 を取るので 5 で足りる。</summary>
    public const int Ramp = 5;

    public required int Seeds { get; init; }

    /// <summary>seed ごとの、敵に通した総打点（直接 + 毒燃。敵同士の巻き込みは除く）。</summary>
    public required double[] Damage { get; init; }
    /// <summary>seed ごとのターン数。</summary>
    public required double[] Turns { get; init; }
    /// <summary>seed ごとの T1..T5 の**累積**打点。決着後は増えないので、そのまま頭打ちになる。</summary>
    public required double[][] Cum { get; init; }

    /// <summary>手番の振りに帰属した打点の合計（全 seed）。</summary>
    public required double Swing { get; init; }
    /// <summary>出どころのある打点の合計（振り + 反撃・破裂・追い打ち・生贄）。</summary>
    public required double Direct { get; init; }
    /// <summary>毒・燃焼の打点の合計。<c>ApplyDamage</c> が source を渡さないので出どころが無い。</summary>
    public required double Dot { get; init; }

    /// <summary>ターン数が <see cref="Ramp"/> 未満だった試行数。**(B) が測れているかの検定。**</summary>
    public required int Short { get; init; }
    public required int AllyWipe { get; init; }
    public required int FoeWipe { get; init; }

    /// <summary>検算用。敵の tally から数えた同じ量（第13期の受け手側測定）。</summary>
    public required double TallyDamage { get; init; }
    /// <summary>
    /// 敵の撃破数の合計（全 seed）。**受け手側から数える**——毒・燃焼の削りは出どころを
    /// 持たないので、味方側の <c>Kills</c> には載らない（第13期 Phase DA）。
    /// 第18期が「出力が撃破に変換されているか」を読むために足した列で、
    /// **第17期の (A)(B)(C) はこの列を一切見ない**（`output` の出力は1文字も動かない）。
    /// </summary>
    public required long Kills { get; init; }

    /// <summary>
    /// オーバーキルの合計（全 seed）。<c>ApplyDamage</c> は残HPで切り詰めないので、
    /// <c>Damage</c> イベントの <c>Amount</c> には超過分が入っている
    /// ——**(A) は「敵のHPに変換された量」ではなく「振り下ろした量」を測っている。**
    /// 1体ごとに「通した合計 − 最大HP」で数える（総打点 − 敵の総HP では、生き残った駒の
    /// ぶんまで引いてしまう）。
    /// </summary>
    public required double Overkill { get; init; }

    /// <summary>検算用。敵同士の巻き込み。参照台は単一 def の単体攻撃なので 0 のはず。</summary>
    public required long FoeFromAlly { get; init; }

    /// <summary>
    /// **(A) 実効打点/ターン。** seed の部分集合で取れるようにしてあるのは半割のため。
    /// 平均の平均ではなく**総打点 ÷ 総ターン数**（試行ごとの長さが違うので、
    /// 比の平均を取ると短い試行に重みが寄る）。
    /// </summary>
    public double Rate(Func<int, bool> take)
    {
        double d = 0, t = 0;
        for (int s = 0; s < Seeds; s++) if (take(s)) { d += Damage[s]; t += Turns[s]; }
        return t <= 0 ? double.NaN : d / t;
    }

    /// <summary>(A) 全 seed 版。</summary>
    public double RateAll => Rate(_ => true);

    /// <summary>T 番目（1 起点）までの累積打点の試行平均。</summary>
    public double CumAt(int turn) => Cum.Average(c => c[turn - 1]);

    /// <summary>
    /// **(B) 立ち上がりの傾き。** `(T5 − T3) ÷ 2` は T4〜T5 の1ターンあたり打点、
    /// `T1` は初手の1ターンあたり打点。**その比**なので 1.0 が「まったく育たない」。
    /// 甲群（出力が時間で育つ）は 1 を大きく超え、乙群（一撃圏に縛られる）は 1 付近になるはず。
    /// </summary>
    public double Ramp15 => CumAt(1) <= 0 ? double.NaN : ((CumAt(5) - CumAt(3)) / 2) / CumAt(1);

    /// <summary>
    /// **(C) 手番外率（%）。** 打点のうち手番の振り以外（反撃・破裂・追い打ち・生贄・毒燃）
    /// から出たぶん。**近似ではなく実測**——計画書 §4-1 は `総攻 × 手番数` を引く近似を
    /// 示していたが、`Events` から振りの範囲を切れるので引き算の近似は要らない。
    /// </summary>
    public double OffTurnPct => Direct + Dot <= 0 ? double.NaN : (Direct + Dot - Swing) * 100.0 / (Direct + Dot);

    /// <summary>
    /// (C) の直接ダメージだけ版。**`dissect` の `振に帰属%` の裏返し**（100 − あれ）なので、
    /// 第16期の「溜め改 S4 で 78%」と直接突き合わせられる。
    /// </summary>
    public double OffTurnDirectPct => Direct <= 0 ? double.NaN : (Direct - Swing) * 100.0 / Direct;

    /// <summary>打点の内訳（%）。振り / 手番外の直接 / 毒燃。</summary>
    public double SwingPct => Direct + Dot <= 0 ? double.NaN : Swing * 100.0 / (Direct + Dot);
    public double ReactPct => Direct + Dot <= 0 ? double.NaN : (Direct - Swing) * 100.0 / (Direct + Dot);
    public double DotPct => Direct + Dot <= 0 ? double.NaN : Dot * 100.0 / (Direct + Dot);
}

/// <summary>
/// 1事例（編成 × 波）ぶんの解剖材料（第16期 Phase GA）。<see cref="MeasureTrace"/> が埋める。
///
/// **タプルではなく型にしてあるのは列が 25 本あるから。** 名前付きタプルでも書けるが、
/// 25 要素の型注釈が呼び出し側と関数側の2箇所に写ることになり、片方だけ直す事故が起きる。
///
/// **どの列も盤面には一切影響しない。** verbose=true の <c>Events</c> を読み直しているだけで、
/// <c>BattleCore</c> には1文字も足していない（第16期 §1「やらないこと」）。
/// </summary>
sealed class WaveTrace
{
    /// <summary>ターン推移を出す範囲。既存5波の決着はほぼ 3〜9T なので 12 で足りる。</summary>
    public const int Profile = 12;

    public required int Seeds { get; init; }
    public required int Party { get; init; }
    public required int Foes { get; init; }
    public required int FoeHpTotal { get; init; }
    public required int AllyHpTotal { get; init; }

    // --- 結末 ---
    public required double WinRate { get; init; }
    /// <summary>30T 打ち切りでの敗北率。**全滅とは中身が違う**（削り切れなかった側）。</summary>
    public required double DrawRate { get; init; }
    /// <summary>全滅での敗北率（削られ切った側）。</summary>
    public required double WipeRate { get; init; }
    public required double TurnsWin { get; init; }
    public required double TurnsWinSd { get; init; }
    public required double TurnsLose { get; init; }
    public required double AliveOnWin { get; init; }

    // --- 味方の出力 ---
    /// <summary>1戦あたり味方が振った回数（Attack イベント）。反撃はここを通らない。</summary>
    public required double AllySwings { get; init; }
    /// <summary>1振りで実際に削った敵の数。**範囲が何体を巻き込んだか**の実測。</summary>
    public required double HitsPerSwing { get; init; }
    /// <summary>1振りの主目標への打点。一撃圏の分母になる。</summary>
    public required double PrimaryDmg { get; init; }
    /// <summary>
    /// 敵に通した直接ダメージのうち、**手番の振りに帰属したぶん**の割合。
    /// 残りは手番外（反撃・破裂・追い打ち・生贄）から来ている。`pulse` の
    /// 「振 ≒ 0 / 干渉 大 = 反応型」を、量の側で見た列。
    /// </summary>
    public required double SwingShare { get; init; }
    /// <summary>1戦あたり敵に通した直接ダメージ（毒・燃焼を含まない）。</summary>
    public required double DirectToFoe { get; init; }
    /// <summary>1戦あたり敵に通した毒・燃焼のダメージ。出どころを持たないので直接とは分ける。</summary>
    public required double DotToFoe { get; init; }
    public required double FoeDeaths { get; init; }
    /// <summary>敵1体を落とすのに振った回数。**一撃圏の実測版。**</summary>
    public required double SwingsPerKill { get; init; }
    /// <summary>(直接 + 毒燃) ÷ 敵の総HP。1.00 を超えたぶんが過剰殺傷と敵の回復。</summary>
    public required double ShaveRatio { get; init; }

    // --- 敵の出力 ---
    public required double FoeSwings { get; init; }
    public required double AllyTaken { get; init; }
    /// <summary>味方の被ダメのうち後列（slot 4/5）が受けた割合。貫きが後列に届いたかを見る。</summary>
    public required double BackShare { get; init; }
    public required double DotToAlly { get; init; }

    // --- 毒 ---
    /// <summary>敵に乗った毒の総段数のピーク（試行平均）。</summary>
    public required double PoisonPeak { get; init; }
    public required double PoisonPeakTurn { get; init; }
    /// <summary>敵が落ちた時点で乗ったままだった毒の段数の合計。**乗り切る前に落ちた量。**</summary>
    public required double PoisonWasted { get; init; }

    // --- 推移（ターン開始時点の平均生存数。決着後は決着時の盤面で埋める） ---
    public required double[] AllyAlive { get; init; }
    public required double[] FoeAlive { get; init; }
}

/// <summary>
/// 業（第49期）の1測定ぶんの集計。<b>タプルではなく型にしてあるのは列が 20 本あるから</b>
/// （名前付きタプルでも書けるが、20 要素の型注釈が呼び出し側と関数側の2箇所に写る）。
/// <c>WaveTrace</c>（第16期）と同じ理由。
///
/// <b>どの列も盤面には一切影響しない。</b> <c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class SgStat
{
    public double Win, Turns;
    public double Takes, Missed, Full;
    public double KindAvg, KindMax, Met, First, Never;
    public double Swings, Fired;
    public double FoeDot, FoeSkips, MarkPulls;
    public double SelfDot, SelfSkips, AllyDot, AllySkips, Life;
    public Dictionary<string, double> TakeByKind = new();
    public Dictionary<string, double> WriteByKind = new();
    public Dictionary<string, double> TakeFrom = new();
}

/// <summary>
/// 逸らし（第50期）の1測定ぶんの集計。<b>タプルではなく型にしてあるのは列が 20 本あるから</b>
/// （<c>WaveTrace</c> / <c>SgStat</c> と同じ理由）。
/// <b>どの列も盤面には一切影響しない。</b> <c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class GdStat
{
    public double Win, Turns;
    public double Fires, Idle, Given, Switches, MarkLost, ToPerverse;
    public Dictionary<string, double> TargetTo = new();
    public Dictionary<string, double> Dmg = new();
    public Dictionary<string, double> Taken = new();
    public Dictionary<string, double> Last = new();
    public Dictionary<string, double> Deaths = new();
}

// **主判定の固定行集合（19行）と歯止めの線。第60期に確定した**（第59期 9-4 の移行案への回答）。
//
// **歯止めは全61行の平均ではなくこの集合の上で測る。** 第41〜59期の「第五波が歯止めを割った」は
// すべて**分母の話**で、波そのものは第40期（曝きの採用）から1つも動いていない
// ——実測でも第58〜60期に行を5本足す間、この17〜19行の第五波平均は 1ビットも動いていない。
//
// 中身は第31期の16行 + `突き出し`（17行）から、第60期に**1行を差し替え・2行を足した**もの:
//   差し替え `裂き (キリ×エグ)` → `裂き×責め苦 (キリ×エグ×シガ)`（情報セル 2 → 3）
//   追加     `止め改 (トメ×薙ぎ)`   ——標の**敵側**の読み手（第53期）。#4 は味方側で無代表だった
//   追加     `引き受け (ウケ×ドハ)` ——`ctx.Dull` を通る行が主判定に1つも無かった（第42期）
// **`死軸×ホタ (ゾト×熾)` は保留のまま第111期に `Presets.Compare` から落ちた**
// ——`後衛特化+後備え`（#16）との r が **+1.0000**・max|Δ| **0.5pt** で 61 行でいちばん冗長で、
// しかも情報セルが 1（第89期 (P2) の席の差し替えで 3 → 1 に落ちていた）。**主判定には一度も入っていない。**
// 代わりに入ったのは `灯×薙ぎ (トモ×ドルガ)` だが、**これも主判定には入れていない**
// （主判定の集合を動かすと歯止め 33.2 の測り直しが要る。第111期 §8）。
//
// **行名で引いている。** `CompareBuilds()` の行名を変えたらここも直すこと
// （`spread` の §4 が見つからない行を警告として出す）。
/// <summary>
/// 第68期（carry）の集計セル。<b>行 × 駒</b> ごとに、5波 × seed 帯ぶんを積む。
/// <c>BattleResult</c> の計数を読み直しているだけで、<b>盤面には一切影響しない</b>。
/// </summary>
sealed class CyCell
{
    public int Trials;
    public long Attacks, AtkGain, Taken, Deaths;

    /// <summary>キーごとの届いた累計と回数（<see cref="UnitTally.CarryKeys"/> の並び）。</summary>
    public readonly long[] Amount, Count;

    /// <summary>格子ごとの到達した試行数と、その到達ターンの和。添字は <c>キー * 格子数 + 格子</c>。</summary>
    public readonly int[] ProbeN;
    public readonly double[] ProbeT;

    public CyCell(int keys, int probes)
    {
        Amount = new long[keys];
        Count = new long[keys];
        ProbeN = new int[keys * probes];
        ProbeT = new double[keys * probes];
    }
}

/// <summary>
/// 第116期（`reader load`）の観測。<b>どの列も盤面には一切影響しない</b>
/// ——<see cref="BattleResult"/> の計数を読み直しているだけ。
/// </summary>
sealed class LdStat
{
    public int N, FirstN;
    public double Win, Turns, Reach, Alive, Over, OverSwung, Sweeps, Splash, Swings, Bonus, BonusMax, First, Dmg, Kills;
    public double[] Probe = new double[UnitTally.ReaderProbes.Length];
}

static class Baseline
{
    public static readonly string[] PrimaryRows =
    {
        "隊列崩し (バサ×ヨミ×セロ)",          // 移動
        "燃焼 (ボルグ×ホタ)",                  // 燃焼（ボルグの毎ターン供給）
        "縛め収入型 (クグ×バン×ガン)",        // 縛め
        "仇討ち×砕け (ヒビ×ザン)",            // 標（味方側の読み手）
        "刻み×抉り (ノミ×エグ)",              // 傷（ノミ入口）
        "裂き×責め苦 (キリ×エグ×シガ)",      // 傷（キリ入口）**第60期に差し替え**
        "耐久 (ガルド×ノノ)",                  // 耐久（第36期の申し送りは第59期に解消）
        "溜め改 (クグ×バン×ガン)",            // 溜め
        "逆しま (ネル×ウツ)",                  // 逆しま
        "追撃×据え (ハギ×バン)",              // 追撃
        "置き去り×分散回復",                   // 置き去り／回復
        "毒+耐久 (ベニ×トウ)",                 // 毒
        "速攻 (ボルグ×ムド)",                  // 速攻
        "反撃改2 (ガン×カド)",                 // カウンター
        "惨禍×死の連鎖",                       // 死（番人）
        "後衛特化+後備え",                     // 後備え（番人。情報セルでは測らない）
        "突き出し (セロ×ヨミ)",                // 移動（予備）
        "止め改 (トメ×薙ぎ)",                  // 標（敵側の読み手）**第60期に追加**
        "引き受け (ウケ×ドハ)",                // 弱体の窓口（`ctx.Dull` の横取り）**第60期に追加**
    };

    /// <summary>
    /// 第五波の歯止め。<b>確定時（第60期）の主判定19行の第五波平均 38.2% − 5.0pt。</b>
    /// **旧値の 40.0 を据え置かなかった**のは、あれが 42行時代の**全行平均**から来た数字で、
    /// 第59期の提案20行がちょうど 40.0 で**線上に乗った**のが偶然だったため。
    /// **線は「明らかに成立しなくなる線」であって調整目標ではない**ので、線上に置くのが一番まずい。
    /// </summary>
    public const double PrimaryFifthFloor = 33.2;
}

/// <summary>
/// 瘴気の移設（第61期）の計数。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class MsStat
{
    public double Win, Turns;
    public double Fires, ToFoe, ToAlly, BiteAlly, BiteFoe, TicksAlly, TicksFoe;
    public double Swings, Dmg, Life, TeamDmg, TeamTaken;
    public double VioDmg, BeniHeal;
}

sealed class FvStat
{
    public double Win, Turns;
    public double Fires, Idle, Whetted, Dulled, Given, Taken, ToPyre;
    public double Swings, Dmg, Life, TeamDmg;
    public Dictionary<string, double> WhetTo = new();
    public Dictionary<string, double> DullTo = new();
}

/// <summary>
/// 横流し（第62期）の計数。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class FlStat
{
    public double Win, Turns;
    public double WhetTotal, DullTotal, Taken, Dead, DeadNew, DullTaken, DullDead;
    public double Hoard, HoardNew, Perverse, Flips;
    public double[] Route = new double[WhetRoutes.Count];
    public double[] TakenRoute = new double[WhetRoutes.Count];
    public double[] DullRoute = new double[DullRoutes.Count];
    public double[] DullTakenRoute = new double[DullRoutes.Count];
    public Dictionary<string, double> To = new();
    public Dictionary<string, double> DullTo = new();
    public Dictionary<string, double> DullFrom = new();
    public Dictionary<string, double> Got = new();
    public Dictionary<string, double> Atk = new();
    public Dictionary<string, double> Lost = new();
}

sealed class KdStat
{
    public double Win, Turns;
    public double Fires, Idle, Whetted, Dulled, Given, Taken, ToPyre, Hoard;
    public double WhetTotal, DullTotal;
    public Dictionary<string, double> WhetTo = new();
    public Dictionary<string, double> DullTo = new();
    public Dictionary<string, double> Dmg = new();
    public Dictionary<string, double> Taken2 = new();
    public Dictionary<string, double> Last = new();
    public Dictionary<string, double> Atk = new();
    public Dictionary<string, double> Got = new();
}

// 第59期 blaze。**陣営は敵側の `Def.Id` 集合で割る**——胞子のように戦闘中に湧いた味方も
// 味方に入る（`TallyByUnit` は `Def.Id` で引くので、`InstanceId` の範囲では割れない）。
sealed class BzStat
{
    public double Win, Turns;
    public double Lit, Relit, LitAlly;
    public double BurnDmgA, BurnDmgF, BurnDeathA, BurnDeathF;
    public double Deaths, RicaAtk;
    public Dictionary<string, double> Atk = new();
    public Dictionary<string, double> BurnAtk = new();
    public Dictionary<string, double> Dmg = new();
}

sealed class FnStat
{
    public double Win, Turns;
    public double Fires, Idle, Cross, Consumed, Kills;
    public double WaitSum, WaitCount, AllySingles, Starved;
    public double Supply, SupplyFresh, DvSingles, DvOnMarked;
    public Dictionary<string, double> TargetTo = new();
    public Dictionary<string, double> Dmg = new();
    public Dictionary<string, double> Taken = new();
    public Dictionary<string, double> Last = new();
    public Dictionary<string, double> Deaths = new();
}

sealed class DvStat
{
    public double Win, Turns;
    public double Fires, Strips, Focus, FocusFresh, MarkedFoe;
    public int MarkedFoeMax;
    public double AllySingles, AllyOnMarked, FoeSingles, FoeOnMarked, AllyPulls, FoePulls;
    public double SelfTaken, Life, KadoLife, KadoInter;
    public bool HasKado;
    public Dictionary<string, double> StripFrom = new();
    public Dictionary<string, double> FocusTo = new();
    public Dictionary<string, double> KillTurn = new();
    public Dictionary<string, double> KillCount = new();
}

/// <summary>
/// ドラフト台（第69期）の1帯ぶんの走査結果。<b>盤面には一切影響しない</b>
/// ——<c>BattleEngine.Run</c> の勝敗を数え直しているだけ。
/// </summary>
sealed class DfResult
{
    public required int N { get; init; }
    public required int M { get; init; }
    public required int W { get; init; }
    public required int Band { get; init; }

    /// <summary>[標本][版 * 波数 + 波] の勝数。版 0 = R（無作為）/ 版 1 = H（規則）。</summary>
    public required int[][] Full { get; init; }

    /// <summary>[標本][(版 * 在席対象駒数 + 添字) * 波数 + 波] の勝数（素体に差し替えた版）。</summary>
    public required int[][] Plain { get; init; }

    /// <summary>[標本] その標本に居る対象駒の添字（<c>dfTargets</c> の並び）。</summary>
    public required int[][] Present { get; init; }

    /// <summary>[標本][0..4] 抽選された5体（<c>UnitCatalog.All</c> の添字。抽選順）。</summary>
    public required int[][] Members { get; init; }
}

/// <summary>
/// 特性 → 通貨のキー（<see cref="UnitTally.CarryKeys"/> の添字）の対応表。
///
/// <para><b>出典は <c>BattleCore/Traits.cs</c> の grep</b>——<c>StatusKeys.*</c> の
/// <c>SetCounter</c> / <c>Counter</c> ／ <c>ctx.Whet</c> ／ <c>ctx.Dull</c> ／ <c>ctx.Ignite</c> ／
/// <c>ctx.SwapSlots</c> ／ <c>OnMoved</c> / <c>OnAllyMoved</c> / <c>OnDamaged</c> の override。
/// <b>engine 側の窓口は駒に属さないのでここには入らない</b>（第50期の窓口一覧の裏返し）。</para>
///
/// <para><b><c>Trait</c> に属性を足さない</b>——判定の根拠が「誰かが属性を正しく付けたか」に
/// 化けて grep で検算できなくなる（第48期 census の作法）。</para>
///
/// <para><b>1箇所に集めてある。</b> 第68期 <c>carry solo</c> が作り、第69期 <c>draft</c> が写し、
/// 第70期 <c>draft2</c> で3つ目の写しになるところだった——<b>2つ目の診断がコピーを持った瞬間に
/// 「1箇所に集める」が消える</b>（CLAUDE.md の <c>WaveCatalog()</c> の申し送りと同じ理由）。
/// <b>移しただけで中身は1文字も変えていない</b>（<c>carry solo</c> の出力が byte 一致することが検算）。</para>
/// </summary>
/// <summary>
/// 第80期の器具 —— <b>在席差</b>の帳簿（駒1枚と駒の組）。
///
/// <para><b>帰属（素体差し替え）ではない。</b>標本ごとの勝率 y を、その標本に在席した駒の組み合わせで
/// 4群（両方在席 / A だけ / B だけ / どちらも不在）に分けた平均の差で読む（指示書 §0-2）:</para>
///
/// <para><c>単独(A) = mean(A 在席) − mean(A 不在)</c>／
/// <c>組(A,B) = mean(両方在席) − mean(どちらも不在)</c>／
/// <c>相乗(A,B) = 組 − 単独(A) − 単独(B)</c></para>
///
/// <para>標準誤差は、相乗を4群の平均の線形結合として書き、群ごとの標本分散から合成する
/// （群の間は互いに素なので共分散は 0）。溜めるのは 駒 × 駒 の <c>n / Σy / Σy²</c> だけで、
/// 「A だけ」「どちらも不在」の群は駒ごと・全体の累計から引き算で作る。</para>
/// </summary>
/// <summary>
/// 棘の傷（第84期）の計数。<b>どの列も盤面には一切影響しない</b>——verbose の `Events` と `Log` を読み直しているだけ。
/// </summary>
sealed class ThStat
{
    public double N, Wins, Turns;
    public double Fires, FiresEv, FiresAlive, FiresUnderHush;    // 棘の発火（Log）／同（Events）／相手が生きていた（Events・InstanceId）／粛の保持者が生きている間（Events）
    public double Wounds, WoundsAlly;                           // 棘が傷を書いた回数（敵／味方）
    public double CarryWoundFoe, CarryWoundAlly;                // `CarryCount[CarryWound]` の合計（敵側／味方側。書き手を問わない）
    public double Stock, StockMax;                              // 敵側の傷の在庫のターン平均／最大
    public double Gouge, Trace, Sever, SeverWounds, SeverReached, Suture;
    public double DmgB, Healed;
    public void AddFrom(ThStat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        Fires += o.Fires; FiresEv += o.FiresEv; FiresAlive += o.FiresAlive; FiresUnderHush += o.FiresUnderHush;
        Wounds += o.Wounds; WoundsAlly += o.WoundsAlly; CarryWoundFoe += o.CarryWoundFoe; CarryWoundAlly += o.CarryWoundAlly;
        Stock += o.Stock; StockMax += o.StockMax;
        Gouge += o.Gouge; Trace += o.Trace; Sever += o.Sever; SeverWounds += o.SeverWounds; SeverReached += o.SeverReached; Suture += o.Suture;
        DmgB += o.DmgB; Healed += o.Healed;
    }
}

sealed class PairAcc
{
    readonly int _u;
    public int N;
    public double S, Q;
    public readonly int[] NA;
    public readonly double[] SA, QA;
    public readonly int[,] N11;
    public readonly double[,] S11, Q11;

    public PairAcc(int units)
    {
        _u = units;
        NA = new int[units]; SA = new double[units]; QA = new double[units];
        N11 = new int[units, units]; S11 = new double[units, units]; Q11 = new double[units, units];
    }

    /// <summary>1標本を積む。<paramref name="team"/> は駒の添字（重複なし）。</summary>
    public void Add(IReadOnlyList<int> team, double y)
    {
        N++; S += y; Q += y * y;
        for (int i = 0; i < team.Count; i++)
        {
            int a = team[i];
            NA[a]++; SA[a] += y; QA[a] += y * y;
            for (int j = i + 1; j < team.Count; j++)
            {
                int b = team[j];
                N11[a, b]++; N11[b, a]++;
                S11[a, b] += y; S11[b, a] += y;
                Q11[a, b] += y * y; Q11[b, a] += y * y;
            }
        }
    }

    public double MeanIn(int a) => NA[a] == 0 ? double.NaN : SA[a] / NA[a];
    public double MeanOut(int a) => N - NA[a] == 0 ? double.NaN : (S - SA[a]) / (N - NA[a]);
    public double Solo(int a) => MeanIn(a) - MeanOut(a);

    static double Var(int n, double s, double q) => n < 2 ? 0.0 : Math.Max(0.0, (q - s * s / n) / (n - 1));

    public readonly record struct Stat(int N11, double SoloA, double SoloB, double Pair, double Syn, double Se);

    /// <summary>組 (a, b) の統計。両方在席が 0 標本なら NaN。</summary>
    public Stat Of(int a, int b)
    {
        int n11 = N11[a, b];
        if (n11 == 0) return new Stat(0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        double s11 = S11[a, b], q11 = Q11[a, b];
        int n10 = NA[a] - n11, n01 = NA[b] - n11, n00 = N - NA[a] - NA[b] + n11;
        double s10 = SA[a] - s11, s01 = SA[b] - s11, s00 = S - SA[a] - SA[b] + s11;
        double q10 = QA[a] - q11, q01 = QA[b] - q11, q00 = Q - QA[a] - QA[b] + q11;
        double m11 = s11 / n11, m00 = n00 == 0 ? double.NaN : s00 / n00;
        double soloA = Solo(a), soloB = Solo(b);
        double pair = m11 - m00;
        double syn = pair - soloA - soloB;
        // 相乗 = c11·m11 + c10·m10 + c01·m01 + c00·m00
        int nA = NA[a], nB = NA[b], oA = N - nA, oB = N - nB;
        double c11 = 1.0 - (double)n11 / nA - (double)n11 / nB;
        double c10 = -(double)n10 / nA + (double)n10 / oB;
        double c01 = -(double)n01 / nB + (double)n01 / oA;
        double c00 = -1.0 + (double)n00 / oA + (double)n00 / oB;
        double v = 0;
        if (n11 > 0) v += c11 * c11 * Var(n11, s11, q11) / n11;
        if (n10 > 0) v += c10 * c10 * Var(n10, s10, q10) / n10;
        if (n01 > 0) v += c01 * c01 * Var(n01, s01, q01) / n01;
        if (n00 > 0) v += c00 * c00 * Var(n00, s00, q00) / n00;
        return new Stat(n11, soloA, soloB, pair, syn, Math.Sqrt(v));
    }
}

static class TraitKeyMap
{
    public static readonly Dictionary<TraitId, int[]> TraitKeys = new()
    {
        // 強化
        [TraitId.Rally]      = new[] { UnitTally.CarryWhet, UnitTally.CarryIdle },
        [TraitId.Bind]       = new[] { UnitTally.CarryWhet, UnitTally.CarryStun },
        [TraitId.Drifter]    = new[] { UnitTally.CarryWhet, UnitTally.CarryMove },
        [TraitId.Goad]       = new[] { UnitTally.CarryWhet, UnitTally.CarryMark },
        [TraitId.Favor]      = new[] { UnitTally.CarryWhet, UnitTally.CarryDull, UnitTally.CarryBurn },
        [TraitId.Colossus]   = new[] { UnitTally.CarryWhet, UnitTally.CarryHit },
        [TraitId.Perverse]   = new[] { UnitTally.CarryWhet, UnitTally.CarryDull },
        [TraitId.Funnel]     = new[] { UnitTally.CarryWhet, UnitTally.CarryDull },
        // 弱体
        [TraitId.Curse]      = new[] { UnitTally.CarryDull },
        [TraitId.Cower]      = new[] { UnitTally.CarryDull },
        [TraitId.Shove]      = new[] { UnitTally.CarryDull, UnitTally.CarryMove },
        [TraitId.Bear]       = new[] { UnitTally.CarryDull, UnitTally.CarryArmor },
        [TraitId.Relay]      = new[] { UnitTally.CarryDull },
        [TraitId.Sharer]     = new[] { UnitTally.CarryDull, UnitTally.CarryHit },
        // 毒
        [TraitId.Miasma]     = new[] { UnitTally.CarryPoison },
        [TraitId.Venom]      = new[] { UnitTally.CarryPoison, UnitTally.CarryHit },
        [TraitId.Amplifier]  = new[] { UnitTally.CarryPoison, UnitTally.CarryWound },   // 第89期 (P1) の採用で 2 本目
        [TraitId.Contagion]  = new[] { UnitTally.CarryPoison },
        [TraitId.Devour]     = new[] { UnitTally.CarryPoison },
        [TraitId.Blightfed]  = new[] { UnitTally.CarryPoison },
        // 燃焼
        [TraitId.Cinder]     = new[] { UnitTally.CarryBurn },
        [TraitId.Bomber]     = new[] { UnitTally.CarryBurn },
        [TraitId.Pyre]       = new[] { UnitTally.CarryBurn },
        // 痺れ
        [TraitId.Paralyze]   = new[] { UnitTally.CarryStun },
        [TraitId.Torment]    = new[] { UnitTally.CarryStun, UnitTally.CarryIdle },
        [TraitId.Gouge]      = new[] { UnitTally.CarryStun, UnitTally.CarryWound },
        [TraitId.Avenge]     = new[] { UnitTally.CarryStun, UnitTally.CarryMark, UnitTally.CarryHit },
        // 標
        [TraitId.Marker]     = new[] { UnitTally.CarryMark },
        [TraitId.Divert]     = new[] { UnitTally.CarryMark },
        [TraitId.Finisher]   = new[] { UnitTally.CarryMark },
        // 破片
        [TraitId.Shatter]    = new[] { UnitTally.CarryArmor, UnitTally.CarryHit },
        [TraitId.Scale]      = new[] { UnitTally.CarryArmor },
        // 傷
        [TraitId.Rend]       = new[] { UnitTally.CarryWound },
        [TraitId.Carve]      = new[] { UnitTally.CarryWound },
        [TraitId.Sever]      = new[] { UnitTally.CarryWound },
        [TraitId.Suture]     = new[] { UnitTally.CarryWound },
        // 第74期に切り出したマイナス4枚は**キーを持たない**（意図的）。
        // この表は「駒 → キー」を Trait 経由で作る道具で、`KeysOf` は駒の Traits の**和**を取る。
        // 分割はプラス側の TraitId をそのまま残しているので、**和は1ビットも変わらない**
        // ——マイナス側にキーを足すと第68期以降のキーの数え方（駒で数える）が動いてしまう。
        // 抉りの `CarryStun` を `Overreach` へ移さないのも同じ理由（駒の側の和が答え）。
        [TraitId.ThinBlade]  = Array.Empty<int>(),
        [TraitId.Overreach]  = Array.Empty<int>(),
        [TraitId.Await]      = Array.Empty<int>(),
        [TraitId.Seal]       = Array.Empty<int>(),
        // 手番
        [TraitId.Bulwark]    = new[] { UnitTally.CarryIdle },
        // 繕いの傷読み（第92期に採用）。**これでノノに初めてキーが立つ**
        // ——第83期の「キーを1つも持たない駒は 8 / 51 体」は **7 / 51** になった（第80〜83期の派生値は動く）。
        [TraitId.Mender]     = new[] { UnitTally.CarryWound },
        // 被弾（damage の層に立つ読み手・書き手）
        [TraitId.Rage]       = new[] { UnitTally.CarryHit },
        [TraitId.Thorns]     = new[] { UnitTally.CarryHit },
        [TraitId.Guardian]   = new[] { UnitTally.CarryHit, UnitTally.CarryWound },   // 傷の引き取り（第90期に採用）
        [TraitId.RearGuard]  = new[] { UnitTally.CarryHit },
        [TraitId.Splash]     = new[] { UnitTally.CarryHit },
        // 移動
        [TraitId.Shuffler]   = new[] { UnitTally.CarryMove },
        [TraitId.Coward]     = new[] { UnitTally.CarryMove },
        [TraitId.ThornGuard] = new[] { UnitTally.CarryMove, UnitTally.CarryHit },
        [TraitId.Displaced]  = new[] { UnitTally.CarryMove },
        // 第106期。散開（ササ）が被弾のたびに隣の味方を弾くようになったので、移動の書き手になった
        // （`derive scan` の観測 0.05 回/戦）。**第78期の入口・発火口と第80〜83期の独立の広さは動く。**
        [TraitId.Loose]      = new[] { UnitTally.CarryMove },
        // 第79期の候補駒（首刈りのオノ）が使う2枚。撃破は 11 本のキーに無く、支援拒否は通貨を書きも読みもしない。
        // **どちらも空**で、`KeysOf` の和は動かない（ガルド＝Guardian・Stoic の値が変わらないことが検算）。
        [TraitId.Executioner] = Array.Empty<int>(),
        [TraitId.Stoic]       = Array.Empty<int>(),
        // 第108期の尾灯（トモ）。**強化の書き手**（`ctx.Whet` の `WhetRoute.Taillight`）。
        // **消灯は `ctx.Dull` を通さない**ので弱体のキーは立たない——素の攻撃力より弱くはしていないし、
        // 集約・転嫁の横取りに晒すつもりも無い（`TaillightTrait` の doc）。
        // **手番の譲渡もキーを持たない**——`IdleTurn` は「差し出した手番」の記録で、
        // 譲渡は engine の `TakeTurn` を1回余分に呼ぶだけで counter を1つも書かない。
        [TraitId.Taillight]   = new[] { UnitTally.CarryWhet },
        // 第103期の背かれ（ソム）。**撃破は 11 本のキーに無い**ので空
        // ——餌を敵陣に置くのは「体を1つ増やす」であって、通貨を1つも書かない
        // （`derive scan` の観測でも `Betrayed` は 0 件）。
        [TraitId.Betrayed]    = Array.Empty<int>(),
        // 敵側の2枚（第94期 (T2) の観測で出た欠落）。**`UnitCatalog.All` の 51 体は1枚も持たない**ので、
        // 第80〜83期のロスター側の派生値は動かない（`KeysOf` が変わるのは敵の駒だけ）。
        [TraitId.Condemn]     = new[] { UnitTally.CarryStun },     // 観測（断罪）
        [TraitId.Expose]      = new[] { UnitTally.CarryMove },     // 観測（曝き）
    };

    /// <summary>その駒が書き手または読み手になっているキーの一覧（重複なし・昇順）。</summary>
    public static int[] KeysOf(UnitDef d)
        => d.Traits.SelectMany(t => TraitKeys.TryGetValue(t, out int[]? k) ? k : Array.Empty<int>())
                   .Distinct().OrderBy(x => x).ToArray();
}

/// <summary>
/// 第78期の器具その1 —— <b>発火口</b>（その特性が反応するイベントの種類）。
///
/// <para><b>リフレクションで数えない。</b>この表は手で作ってここに置く（指示書 §2-1）。
/// 理由は第70期の集約と同じ——写しを増やさないため。<see cref="TraitKeyMap"/> と同じ場所に置いてある。</para>
///
/// <para><b>engine は擬似フック。</b>駒ごとのフックでは書けない機構は
/// <c>BattleEngine.cs</c> に窓口を持つ（CLAUDE.md の「engine も通貨の読み手である」）。
/// 札（<see cref="TraitId.Bear"/> / <see cref="TraitId.Relay"/> / <see cref="TraitId.Funnel"/> /
/// <see cref="TraitId.Seal"/> / <see cref="TraitId.RearGuard"/>）は本体が全部 engine にあるので、
/// これを数えないと<b>発火口 0 の機構</b>が出てしまう。<b>数えるのはその特性自身の挙動が
/// engine にある場合だけ</b>——標を読む <c>SelectTargetChain</c> のような「通貨の窓口」は、
/// 書き手（<see cref="TraitId.Marker"/>）の発火口ではないので数えない。</para>
///
/// <para><b><see cref="Trait.OnCarryOver"/> は数えない。</b>会戦の境界でしか呼ばれず、
/// この期が測るのは単発戦なので<b>原理的に 0 回</b>（判定式の自己検査 (b)）。</para>
/// </summary>
static class TraitHookMap
{
    public const string Engine = "engine";

    /// <summary>特性 → 発火口の一覧（<c>OnCarryOver</c> を除く）。<b>手で作った表。</b></summary>
    public static readonly Dictionary<TraitId, string[]> TraitHooks = new()
    {
        // --- マイナス側 ---
        [TraitId.Splash]      = new[] { "OnAfterAttack" },
        [TraitId.Coward]      = new[] { "OnTurnStart" },
        [TraitId.Stoic]       = new[] { "BlocksSupport", Engine },              // SupportTargets
        [TraitId.Sacrifice]   = new[] { "OnBattleStart" },
        [TraitId.Drain]       = new[] { "OnTurnStart" },
        [TraitId.Sluggish]    = new[] { "CanAct", "CanReact" },
        [TraitId.Splitter]    = new[] { "OnDeath" },
        [TraitId.Bomber]      = new[] { "OnDeath" },
        [TraitId.Frail]       = new[] { "ModifyIncomingDamage" },
        [TraitId.Fixate]      = new[] { Engine },                              // SelectTargetCore
        // --- プラス側 ---
        [TraitId.Rage]        = new[] { "OnDamaged" },
        [TraitId.Sniper]      = new[] { "ModifyAttack", "ModifyPattern" },
        [TraitId.Curse]       = new[] { "OnBattleStart" },
        [TraitId.Guardian]    = new[] { "OnDamaged", Engine },                 // SelectTargetChain
        [TraitId.Martyr]      = new[] { "OnDamaged", Engine },
        [TraitId.Necro]       = new[] { "OnTurnStart", "ModifyPattern", "OnAnyDeath" },
        [TraitId.Colossus]    = new[] { "OnDeath", Engine },                   // ApplyDamage の巨躯
        [TraitId.Executioner] = new[] { "OnKill" },
        [TraitId.Reviver]     = new[] { "OnAllyDeath" },
        [TraitId.Ephemeral]   = Array.Empty<string>(),                         // 旗。誰も反応しない
        [TraitId.Betrayed]    = new[] { "OnTurnStart" },                       // 第103期
        [TraitId.Venom]       = new[] { "OnDamaged" },
        [TraitId.Thorns]      = new[] { "OnDamaged" },
        [TraitId.Marker]      = new[] { "OnBattleStart" },
        [TraitId.Mender]      = new[] { "OnTurnStart", "OnAction" },
        [TraitId.Amplifier]   = new[] { "OnTurnStart", "OnAction" },
        [TraitId.Contagion]   = new[] { "OnAnyDeath" },
        [TraitId.Miasma]      = new[] { "OnTurnStart", "OnAction" },
        [TraitId.Immobile]    = new[] { "CanAct", "SurrendersTurn" },
        [TraitId.Havoc]       = new[] { "OnBattleStart", Engine },             // ApplyDamage
        [TraitId.Paralyze]    = new[] { "OnAfterAttack" },
        [TraitId.Devour]      = new[] { "OnTurnStart", Engine },               // TickStatuses
        [TraitId.Rally]       = new[] { "OnBattleStart", "OnTurnStart" },
        [TraitId.Blightfed]   = new[] { "OnTurnStart" },
        [TraitId.Displaced]   = new[] { "ModifyPattern", "OnMoved", Engine },  // 型の差し替え / NoteCreak
        [TraitId.Shuffler]    = new[] { "OnTurnStart" },
        [TraitId.Bind]        = new[] { "OnBattleStart", "OnTurnStart" },
        [TraitId.Bulwark]     = new[] { "OnBattleStart", Engine },             // ApplyDamage の据え
        [TraitId.Drifter]     = new[] { "OnAllyMoved" },
        [TraitId.Perverse]    = new[] { "ModifyAttack" },
        [TraitId.Sharer]      = new[] { "OnDamaged", Engine },                 // ApplyDamage の分かち
        [TraitId.Loose]       = new[] { "OnBattleStart", "OnDamaged", Engine },   // 第106期に OnDamaged（弾き）が付いた
        [TraitId.Cower]       = new[] { "OnBattleStart", Engine },
        [TraitId.Pursuer]     = new[] { "OnTurnStart", "CanAct", "OnAnyDeath", "SurrendersTurn" },
        [TraitId.RearGuard]   = new[] { Engine },                              // 札。本体は SelectTargetChain
        [TraitId.Cinder]      = new[] { "OnAfterAttack" },
        [TraitId.Pyre]        = new[] { "ModifyAttack", "ModifyPattern" },
        [TraitId.Condemn]     = new[] { "OnDamaged" },
        [TraitId.Shatter]     = new[] { "OnDamaged" },
        [TraitId.ThornGuard]  = new[] { "OnAction", "OnDamaged", Engine },
        [TraitId.Carve]       = new[] { "OnAfterAttack" },
        [TraitId.Forsake]     = new[] { "OnBattleStart", "OnTurnStart" },
        [TraitId.Torment]     = new[] { "OnAfterAttack" },
        [TraitId.Avenge]      = new[] { "OnDamaged", "OnAllyDamaged" },
        [TraitId.Rend]        = new[] { "OnAfterAttack" },
        [TraitId.Gouge]       = new[] { "OnAfterAttack" },
        [TraitId.Sever]       = new[] { "OnAfterAttack", Engine },             // 選好（SeverTrait.Prefers）
        [TraitId.Suture]      = new[] { "OnAfterAttack", Engine },
        [TraitId.Alms]        = new[] { "OnTurnStart" },
        [TraitId.Expose]      = new[] { "OnAfterAttack" },
        [TraitId.Slander]     = new[] { "OnAfterAttack" },
        // --- プラスとマイナスが1つの動作の表と裏 ---
        [TraitId.Shove]       = new[] { "OnMoved", "OnAllyMoved" },
        [TraitId.Bear]        = new[] { Engine },                              // 札。本体は Dull
        [TraitId.Relay]       = new[] { Engine },                              // 札。本体は Dull
        [TraitId.Overbear]    = new[] { "OnTurnStart", "ModifyAttack", "OnAfterAttack" },
        [TraitId.Scale]       = new[] { "OnTurnStart", "ModifyPattern", "OnAfterAttack", "OnAllyDeath", Engine },
        [TraitId.Scapegoat]   = new[] { "OnTurnStart", "OnAfterAttack" },
        [TraitId.Divert]      = new[] { "OnTurnStart" },
        [TraitId.Goad]        = new[] { "OnTurnStart" },
        [TraitId.Finisher]    = new[] { "OnAfterAttack", Engine },             // 標の段 ＋ 倍率
        [TraitId.Favor]       = new[] { "OnTurnStart", "OnAction" },
        [TraitId.Funnel]      = new[] { Engine },                              // 札。本体は Whet / Dull
        [TraitId.Hex]         = new[] { "OnDamaged" },                        // 第96期。共有の段は engine 側
        [TraitId.Taillight]   = new[] { "OnTurnStart", "OnAction", "OnAnyDeath" },   // 第108期
        // --- 第74期に切り出したマイナス ---
        [TraitId.ThinBlade]   = new[] { "ModifyAttack", Engine },              // PerformAttack の条件版
        [TraitId.Overreach]   = new[] { "OnKill" },
        [TraitId.Await]       = new[] { "CanAct", "SurrendersTurn" },
        [TraitId.Seal]        = new[] { Engine },                              // 札。本体は SutureTrait の中
        // --- 盤面ルール ---
        [TraitId.Inversion]   = new[] { "OnBattleStart", "OnDeath", Engine },
        [TraitId.Drought]     = new[] { "OnBattleStart", "OnDeath", Engine },
        [TraitId.Yoke]        = new[] { "OnBattleStart", "OnDeath", Engine },
        [TraitId.Hush]        = new[] { "OnBattleStart", "OnDeath", Engine },
    };

    /// <summary>その駒の発火口の一覧（特性の和・重複なし）。</summary>
    public static string[] HooksOf(UnitDef d)
        => d.Traits.SelectMany(t => TraitHooks.TryGetValue(t, out string[]? h) ? h : Array.Empty<string>())
                   .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
}

/// <summary>
/// 第78期の器具その2 —— <b>入口</b>（その駒の出力を成立させる<b>外部の供給</b>の本数）。
///
/// <para>定義は第77期に揃える（指示書 §2-1）——ヨミの <c>AtkBonus</c> は
/// <b>移動（軋み）＋ 強化（Whet）の2本</b>、<c>WhetReceived</c> は<b>強化だけの1本</b>。
/// 一般形にすると:</para>
///
/// <para><b>入口 ＝ その駒の「読み」のうち、同じ駒の「書き」では満たせないものの数。</b>
/// キーは <see cref="UnitTally.CarryKeys"/> の 11 本。<b>場所（scope）まで一致しないと打ち消せない</b>
/// ——これが第58期の「供給者は自分の撒いたものを持たない」（火の粉はボルグ自身には移らない）と
/// 第77期の「ヨミは自分では動かない」を同じ形で書いたもの。</para>
///
/// <para><b>被弾（<see cref="UnitTally.CarryHit"/>）だけは敵が供給する。</b>
/// 主の量は<b>味方側の入口</b>（＝標本ごとに在席が揺れるもの・第77期の在庫率）で数え、
/// 被弾を含めた版は別列で併記する。</para>
///
/// <para><b>死・撃破は 11 本のキーに無い</b>ので、墓守・継ぎ接ぎ・処刑・追い打ちの入口は 0 と数える
/// （表A の脚注に明記する）。<b><see cref="TraitId.Scale"/> の自給は打ち消さない</b>
/// ——鱗は自分で破片を作るが、その供給は<b>味方の死</b>という外部の事象に縛られている。</para>
/// </summary>
static class TraitEntryMap
{
    /// <summary>通貨が乗っている場所。<c>Any</c> は自分でも味方でもよい。</summary>
    public enum Where { Self, Ally, Foe, Any }

    /// <summary>その特性の出力が要求する「外から来る通貨」。</summary>
    public static readonly Dictionary<TraitId, (int Key, Where W)[]> Reads = new()
    {
        [TraitId.Rally]      = new[] { (UnitTally.CarryIdle, Where.Ally) },
        [TraitId.Bulwark]    = new[] { (UnitTally.CarryIdle, Where.Ally) },
        [TraitId.Drifter]    = new[] { (UnitTally.CarryMove, Where.Ally) },
        [TraitId.Displaced]  = new[] { (UnitTally.CarryMove, Where.Self) },
        [TraitId.Shove]      = new[] { (UnitTally.CarryMove, Where.Any) },
        [TraitId.Sniper]     = new[] { (UnitTally.CarryMove, Where.Self) },
        [TraitId.Perverse]   = new[] { (UnitTally.CarryWhet, Where.Self), (UnitTally.CarryDull, Where.Self) },
        [TraitId.Funnel]     = new[] { (UnitTally.CarryWhet, Where.Any) },
        [TraitId.Bear]       = new[] { (UnitTally.CarryDull, Where.Ally) },
        [TraitId.Relay]      = new[] { (UnitTally.CarryDull, Where.Ally) },
        // 第94期 (T2) の観測で足した分は、行末に「観測」と書いてある。
        // **走らせて観測した事実**（`derive scan`）で、手で読んで足したものは1件も無い。
        [TraitId.Divert]     = new[] { (UnitTally.CarryMark, Where.Self), (UnitTally.CarryMark, Where.Ally),
                                       (UnitTally.CarryMark, Where.Foe) },                  // 観測
        [TraitId.Goad]       = new[] { (UnitTally.CarryMark, Where.Ally) },                 // 観測
        [TraitId.Carve]      = new[] { (UnitTally.CarryWound, Where.Foe) },                 // 観測（なぞり）
        [TraitId.Bind]       = new[] { (UnitTally.CarryStun, Where.Foe),
                                       (UnitTally.CarryStun, Where.Ally) },                 // 観測（既に縛られているかを見る）
        [TraitId.Amplifier]  = new[] { (UnitTally.CarryPoison, Where.Foe), (UnitTally.CarryWound, Where.Foe) },
        // 繕いの傷読み（第92期に採用）。患者は必ず同陣営（`MostHurtAlly`）なので `Where.Ally`。
        [TraitId.Mender]     = new[] { (UnitTally.CarryWound, Where.Ally) },
        [TraitId.Devour]     = new[] { (UnitTally.CarryPoison, Where.Foe) },
        [TraitId.Contagion]  = new[] { (UnitTally.CarryPoison, Where.Foe), (UnitTally.CarryWound, Where.Any),
                                       (UnitTally.CarryPoison, Where.Ally) },               // 観測（味方の死体からも撒く）
        // 滲み則（第90期に採用）。**engine の規則なので `TraitId` は増えていない**が、
        // この4枚は定義を1文字も変えずに傷の読み手になった。
        // **`Where.Any`**——指示書 §2-4 は `Where.Foe` と書いていたが、理想61行の実測では
        // **滲みの 100% が味方側に落ちる**（`soak ideal` の Q2）ので `Foe` は事実に反する。
        [TraitId.Miasma]     = new[] { (UnitTally.CarryWound, Where.Any) },
        [TraitId.Venom]      = new[] { (UnitTally.CarryWound, Where.Any),
                                       (UnitTally.CarryPoison, Where.Foe) },                // 観測
        // **火の粉（`Cinder`）は第91期に外した**——燃焼は非スタックなので深さを足しても点け直しで消える。
        [TraitId.Blightfed]  = new[] { (UnitTally.CarryPoison, Where.Ally) },
        [TraitId.Pyre]       = new[] { (UnitTally.CarryBurn, Where.Self) },
        [TraitId.Favor]      = new[] { (UnitTally.CarryBurn, Where.Ally) },
        [TraitId.Torment]    = new[] { (UnitTally.CarryStun, Where.Foe),
                                       (UnitTally.CarryIdle, Where.Foe) },                  // 観測
        [TraitId.Avenge]     = new[] { (UnitTally.CarryMark, Where.Ally), (UnitTally.CarryHit, Where.Ally) },
        [TraitId.Finisher]   = new[] { (UnitTally.CarryMark, Where.Foe) },
        [TraitId.Scale]      = new[] { (UnitTally.CarryArmor, Where.Self) },
        [TraitId.Gouge]      = new[] { (UnitTally.CarryWound, Where.Foe) },
        [TraitId.Sever]      = new[] { (UnitTally.CarryWound, Where.Foe) },
        // 縫いの両側読み（第85期に採用した `SutureSide.Both`）が**表に反映されていなかった**。
        // 第94期 (T2) の観測で 0.69 回/戦（味方側の糸口）が出た。
        [TraitId.Suture]     = new[] { (UnitTally.CarryWound, Where.Foe),
                                       (UnitTally.CarryWound, Where.Ally) },                // 観測
        // 処刑（Executioner）は撃破を読むが撃破は 11 本のキーに無いので入口 0、支援拒否（Stoic）は読みを持たない。
        // **第79期の候補駒（オノ）はこの2枚だけなので、この表には載らない＝入口 0 が正しい答え**（第78期 (b) の注意）。
        // 被弾（敵が供給する）
        [TraitId.Rage]       = new[] { (UnitTally.CarryHit, Where.Self) },
        [TraitId.Thorns]     = new[] { (UnitTally.CarryHit, Where.Self) },
        [TraitId.Shatter]    = new[] { (UnitTally.CarryHit, Where.Self),
                                       (UnitTally.CarryArmor, Where.Ally) },                // 観測
        [TraitId.Condemn]    = new[] { (UnitTally.CarryHit, Where.Self) },
        [TraitId.Frail]      = new[] { (UnitTally.CarryHit, Where.Self) },
        // 傷の引き取り（第90期に採用）は**供給ではなく中継**だった（第94期 (T2)。下の `Supplies` の注）。
        // 引き取りは「隣の味方に傷があること」を要求するので、**読みの側にだけ立つ**。
        [TraitId.Guardian]   = new[] { (UnitTally.CarryHit, Where.Ally),
                                       (UnitTally.CarryWound, Where.Ally) },                // 観測
        [TraitId.Martyr]     = new[] { (UnitTally.CarryHit, Where.Ally) },
        [TraitId.RearGuard]  = new[] { (UnitTally.CarryHit, Where.Ally) },
        [TraitId.ThornGuard] = new[] { (UnitTally.CarryHit, Where.Ally) },
        [TraitId.Colossus]   = new[] { (UnitTally.CarryHit, Where.Ally) },
        [TraitId.Sharer]     = new[] { (UnitTally.CarryHit, Where.Ally) },
        // 業（`All` に載っていない棄却駒。参考のために置いてある）
        [TraitId.Scapegoat]  = new[] { (UnitTally.CarryPoison, Where.Ally), (UnitTally.CarryStun, Where.Ally),
                                       (UnitTally.CarryMark, Where.Ally), (UnitTally.CarryBurn, Where.Ally) },
    };

    /// <summary>
    /// その特性が供給できる通貨と、その置き場所。
    ///
    /// <para><b>鱗（<see cref="TraitId.Scale"/>）だけは載せていない</b>（上の doc）。
    /// 第94期 (T2) の観測は <c>(破片, 自分) 0.14 回/戦</c> を出すが、<b>これは意図的な除外である</b>
    /// ——鱗は自分で破片を作るが、その供給は<b>味方の死</b>という外部の事象に縛られているので、
    /// 自分の読み <c>(破片, 自分)</c> を打ち消させない。<b>載せると入口が 1 → 0 になり、
    /// 第78期の「入口」の定義が変わる。</b> ここだけは観測より doc の側を採る。</para>
    ///
    /// <para><b>それ以外の欠落 13 件・過剰 1 件は、第94期 (T2) の観測どおりに直した</b>
    /// （行末に「観測」と書いてある）。</para>
    /// </summary>
    public static readonly Dictionary<TraitId, (int Key, Where W)[]> Supplies = new()
    {
        [TraitId.Carve]      = new[] { (UnitTally.CarryWound, Where.Foe) },
        [TraitId.Rend]       = new[] { (UnitTally.CarryWound, Where.Foe) },
        // **傷の引き取り（`GatherRule`）はここから外した**（第94期 (T2)）。
        // ガルドは隣の味方から傷を「移す」だけで**盤面の総量を1つも増やさない**
        // ——`derive scan` の実測で **増 0.31 ／ 減 0.31 ／ 純増 0.00 回/戦**。
        // 第92期が「ガルドの傷は中継であって供給ではない」と書いて別の期に送った1件で、
        // **走らせた観測がそのまま同じ答えを出した**（Q1）。
        // 要求の側（隣に傷があること）は `Reads` に立ててある。
        [TraitId.Venom]      = new[] { (UnitTally.CarryPoison, Where.Foe),
                                       (UnitTally.CarryPoison, Where.Ally) },               // 観測（毒撃の隣への漏れ）
        [TraitId.Miasma]     = new[] { (UnitTally.CarryPoison, Where.Foe), (UnitTally.CarryPoison, Where.Ally),
                                       (UnitTally.CarryPoison, Where.Self) },               // 観測
        // **疫み（ラウ）と澱み（ミオ）が載っていなかった**（第92期が見つけて別の期に送った2件）。
        // 疫みは死体の毒を撒き直し、澱みは第89期の `IgniteRule` で
        // 「傷を持ち毒を持たない敵」に毒 1 を置く。**どちらも敵に毒を置いている。**
        [TraitId.Contagion]  = new[] { (UnitTally.CarryPoison, Where.Foe) },                // 観測
        [TraitId.Amplifier]  = new[] { (UnitTally.CarryPoison, Where.Foe) },                // 観測
        // 火の粉は自分には移らない（第58期）。だから熾火の (燃, Self) を打ち消さない。
        [TraitId.Cinder]     = new[] { (UnitTally.CarryBurn, Where.Foe), (UnitTally.CarryBurn, Where.Ally) },
        [TraitId.Bomber]     = new[] { (UnitTally.CarryBurn, Where.Foe), (UnitTally.CarryBurn, Where.Ally) },
        [TraitId.Paralyze]   = new[] { (UnitTally.CarryStun, Where.Foe) },
        [TraitId.Torment]    = new[] { (UnitTally.CarryStun, Where.Self) },
        [TraitId.Avenge]     = new[] { (UnitTally.CarryStun, Where.Self) },                 // 観測（ターン外の行動の代金）
        [TraitId.Condemn]    = new[] { (UnitTally.CarryStun, Where.Foe) },                  // 観測（敵側の断罪）
        [TraitId.Marker]     = new[] { (UnitTally.CarryMark, Where.Ally) },
        [TraitId.Goad]       = new[] { (UnitTally.CarryMark, Where.Ally), (UnitTally.CarryWhet, Where.Ally) },
        [TraitId.Divert]     = new[] { (UnitTally.CarryMark, Where.Foe), (UnitTally.CarryMark, Where.Self) },
        [TraitId.Rally]      = new[] { (UnitTally.CarryWhet, Where.Ally),
                                       (UnitTally.CarryWhet, Where.Self) },                 // 観測（号令は自分にも乗る）
        // **大縛り（`BindEnemy`）が載っていなかった**——開戦時に最速の敵を確定で縛る。
        [TraitId.Bind]       = new[] { (UnitTally.CarryWhet, Where.Ally), (UnitTally.CarryStun, Where.Ally),
                                       (UnitTally.CarryStun, Where.Foe) },                  // 観測
        [TraitId.Drifter]    = new[] { (UnitTally.CarryWhet, Where.Ally) },
        // 第108期の尾灯。**灯は必ず味方1体**（自分は対象外）。
        // 消灯は総量を減らすが `Dull` を通さないので「中継」の判定（増と減の両方を書く）には
        // かからない——`derive scan` の中継判定は `NoteCarry` / `SetCounter` の観測から作るので、
        // <c>AtkBonus</c> を直に引く消灯はどちらの側にも現れない。
        [TraitId.Taillight]  = new[] { (UnitTally.CarryWhet, Where.Ally) },
        [TraitId.Favor]      = new[] { (UnitTally.CarryWhet, Where.Ally), (UnitTally.CarryDull, Where.Ally) },
        [TraitId.Colossus]   = new[] { (UnitTally.CarryWhet, Where.Ally),
                                       (UnitTally.CarryWhet, Where.Self) },                 // 観測（吐き戻しは壁自身にも返る）
        [TraitId.Funnel]     = new[] { (UnitTally.CarryWhet, Where.Ally) },
        [TraitId.Curse]      = new[] { (UnitTally.CarryDull, Where.Foe), (UnitTally.CarryDull, Where.Ally),
                                       (UnitTally.CarryDull, Where.Self) },                 // 観測
        [TraitId.Cower]      = new[] { (UnitTally.CarryDull, Where.Ally),
                                       (UnitTally.CarryDull, Where.Self) },                 // 観測
        [TraitId.Relay]      = new[] { (UnitTally.CarryDull, Where.Foe) },                  // 観測（転嫁の流し先）
        [TraitId.Sharer]     = new[] { (UnitTally.CarryDull, Where.Ally) },
        [TraitId.Slander]    = new[] { (UnitTally.CarryDull, Where.Foe) },
        [TraitId.Shove]      = new[] { (UnitTally.CarryDull, Where.Ally), (UnitTally.CarryMove, Where.Foe) },
        [TraitId.Bear]       = new[] { (UnitTally.CarryArmor, Where.Self) },
        [TraitId.Shatter]    = new[] { (UnitTally.CarryArmor, Where.Ally) },
        [TraitId.Shuffler]   = new[] { (UnitTally.CarryMove, Where.Ally) },
        [TraitId.Coward]     = new[] { (UnitTally.CarryMove, Where.Self),
                                       (UnitTally.CarryMove, Where.Ally) },                 // 観測（押しのけた側も動く）
        [TraitId.ThornGuard] = new[] { (UnitTally.CarryMove, Where.Self), (UnitTally.CarryMove, Where.Ally) },
        // 第106期。弾かれた側と、席を明け渡した側の両方が動く（`SwapSlots` が2体を通知する）。
        [TraitId.Loose]      = new[] { (UnitTally.CarryMove, Where.Ally) },
        [TraitId.Expose]     = new[] { (UnitTally.CarryMove, Where.Foe) },
        // のろまは SurrendersTurn を偽にしないので、捨てた手番が号令・据えに売れる。
        // 不動・刃待ち・追い打ちは偽にするので供給しない（第74期の警告）。
        [TraitId.Sluggish]   = new[] { (UnitTally.CarryIdle, Where.Self) },
    };

    static bool Covers(Where supply, Where read)
        => supply == read || supply == Where.Any || read == Where.Any;

    /// <summary>その駒の入口（キーの一覧・重複なし）。<paramref name="withFoe"/> が偽なら被弾を外す。</summary>
    public static int[] EntriesOf(UnitDef d, bool withFoe)
    {
        var sup = d.Traits.SelectMany(t => Supplies.TryGetValue(t, out var s) ? s : Array.Empty<(int Key, Where W)>())
                          .ToArray();
        var need = new List<int>();
        foreach (TraitId t in d.Traits)
        {
            if (!Reads.TryGetValue(t, out var rs)) continue;
            foreach ((int key, Where w) in rs)
            {
                if (!withFoe && key == UnitTally.CarryHit) continue;
                if (sup.Any(x => x.Key == key && Covers(x.W, w))) continue;
                need.Add(key);
            }
        }
        return need.Distinct().OrderBy(x => x).ToArray();
    }
}

/// <summary>第87期 `blaze2` の集計（診断専用。盤面には一切影響しない）。</summary>
sealed class Bz2Stat
{
    public double N, Wins, Turns;
    public double AmpFires, AmpThickened, AmpIgnitable, AmpIgnitableBodies, AmpIgnited, AmpIgniteAmount;
    public double AmpIgniteWoundBefore, AmpIgniteWoundAfter, AmpIgnitePoisonAfter;
    public double FirstIgnitableSum, FirstIgnitableN, FirstIgniteSum, FirstIgniteN;
    public double IgnitePoison, IgniteTicks, IgniteLogs, FoeAmpIgnited;
    public double PaperRaw, PaperCapped, PaperBodies, PaperTurns;       // 紙（§1-1。Y0 の Events から組む）
    public double FoeTaken, AllyTaken, CarryWoundFoe, CarryPoisonFoe;
    public double GougeFires, GougeOut, SutureFires, SutureHealed, MendFires, MendHealed;   // 持続係数の検算（第84〜86期）
    public double BeniHealed, RauSpread;                                // 下流の読み手（Q4）
    public void AddFrom(Bz2Stat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        AmpFires += o.AmpFires; AmpThickened += o.AmpThickened; AmpIgnitable += o.AmpIgnitable;
        AmpIgnitableBodies += o.AmpIgnitableBodies; AmpIgnited += o.AmpIgnited; AmpIgniteAmount += o.AmpIgniteAmount;
        AmpIgniteWoundBefore += o.AmpIgniteWoundBefore; AmpIgniteWoundAfter += o.AmpIgniteWoundAfter;
        AmpIgnitePoisonAfter += o.AmpIgnitePoisonAfter;
        FirstIgnitableSum += o.FirstIgnitableSum; FirstIgnitableN += o.FirstIgnitableN;
        FirstIgniteSum += o.FirstIgniteSum; FirstIgniteN += o.FirstIgniteN;
        IgnitePoison += o.IgnitePoison; IgniteTicks += o.IgniteTicks; IgniteLogs += o.IgniteLogs; FoeAmpIgnited += o.FoeAmpIgnited;
        PaperRaw += o.PaperRaw; PaperCapped += o.PaperCapped; PaperBodies += o.PaperBodies; PaperTurns += o.PaperTurns;
        FoeTaken += o.FoeTaken; AllyTaken += o.AllyTaken; CarryWoundFoe += o.CarryWoundFoe; CarryPoisonFoe += o.CarryPoisonFoe;
        GougeFires += o.GougeFires; GougeOut += o.GougeOut;
        SutureFires += o.SutureFires; SutureHealed += o.SutureHealed;
        MendFires += o.MendFires; MendHealed += o.MendHealed;
        BeniHealed += o.BeniHealed; RauSpread += o.RauSpread;
    }
}

/// <summary>第86期 `mender` の集計（診断専用。盤面には一切影響しない）。</summary>
sealed class MdStat
{
    public double N, Wins, Turns;
    public double SpillWounds, SpillHits;                        // 巻き込み則の書き込み（全）／味方の刃の着弾（生存・Events）
    public double[] SpillByWriter = new double[6];               // 巻き込み則の書き手別（mdWriterIds の並び）
    public double StockAlly, StockAllyMax, StockFoe, AllyWoundTurns;
    public double MendFires, MendSeen, MendDepth, MendDry, MendHealed, MendPaid, MendFoePatient;
    public double NonoLife, NonoDeaths, GaldPatient;
    public double FoeMendFires, FoeMendSeen;
    public double TeamHealed, TeamTaken;
    public double CarryWoundAlly, CarryWoundFoe, DeathWoundAlly, DeathWoundFoe, EndWoundAlly, EndWoundFoe;
    public void AddFrom(MdStat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        SpillWounds += o.SpillWounds; SpillHits += o.SpillHits;
        for (int k = 0; k < SpillByWriter.Length; k++) SpillByWriter[k] += o.SpillByWriter[k];
        StockAlly += o.StockAlly; StockAllyMax += o.StockAllyMax; StockFoe += o.StockFoe; AllyWoundTurns += o.AllyWoundTurns;
        MendFires += o.MendFires; MendSeen += o.MendSeen; MendDepth += o.MendDepth; MendDry += o.MendDry;
        MendHealed += o.MendHealed; MendPaid += o.MendPaid; MendFoePatient += o.MendFoePatient;
        NonoLife += o.NonoLife; NonoDeaths += o.NonoDeaths; GaldPatient += o.GaldPatient;
        FoeMendFires += o.FoeMendFires; FoeMendSeen += o.FoeMendSeen;
        TeamHealed += o.TeamHealed; TeamTaken += o.TeamTaken;
        CarryWoundAlly += o.CarryWoundAlly; CarryWoundFoe += o.CarryWoundFoe;
        DeathWoundAlly += o.DeathWoundAlly; DeathWoundFoe += o.DeathWoundFoe;
        EndWoundAlly += o.EndWoundAlly; EndWoundFoe += o.EndWoundFoe;
    }
}

sealed class SuStat
{
    public double N, Wins, Turns;
    public double KadoFires, KadoAdjacent;                       // 棘の発火（Log）／カドの隣接数（開戦時の席。1戦ごとに加算）
    public double ThornWoundAlly, ThornWoundFoe;                 // 棘が傷を書いた回数（味方／敵。W1 の供給）
    public double SpillWounds, SpillHits, KadoSpill;             // 巻き込み則の書き込み（全）／味方の刃の着弾（生存・Events）／カドが巻き込み則で書いた回数
    public double[] SpillByWriter = new double[6];               // 巻き込み則の書き手別（suWriterIds の並び）
    public double StockAlly, StockAllyMax, StockFoe, StockFoeMax, AllyWoundTurns;   // 在庫（ターン平均／最大）と味方側に傷があったターン数
    public double SutureFoe, SutureAlly, SutureDry, SutureHealed, HariAttacks, HariDeaths, KadoDeaths;
    public double TeamHealed, TeamTaken;
    public double CarryWoundAlly, CarryWoundFoe, DeathWoundAlly, DeathWoundFoe, EndWoundAlly, EndWoundFoe;
    public double Sever, SeverWounds, SeverReached;
    public void AddFrom(SuStat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        KadoFires += o.KadoFires; KadoAdjacent += o.KadoAdjacent;
        ThornWoundAlly += o.ThornWoundAlly; ThornWoundFoe += o.ThornWoundFoe;
        SpillWounds += o.SpillWounds; SpillHits += o.SpillHits; KadoSpill += o.KadoSpill;
        for (int k = 0; k < SpillByWriter.Length; k++) SpillByWriter[k] += o.SpillByWriter[k];
        StockAlly += o.StockAlly; StockAllyMax += o.StockAllyMax; StockFoe += o.StockFoe; StockFoeMax += o.StockFoeMax; AllyWoundTurns += o.AllyWoundTurns;
        SutureFoe += o.SutureFoe; SutureAlly += o.SutureAlly; SutureDry += o.SutureDry; SutureHealed += o.SutureHealed; HariAttacks += o.HariAttacks; HariDeaths += o.HariDeaths; KadoDeaths += o.KadoDeaths;
        TeamHealed += o.TeamHealed; TeamTaken += o.TeamTaken;
        CarryWoundAlly += o.CarryWoundAlly; CarryWoundFoe += o.CarryWoundFoe; DeathWoundAlly += o.DeathWoundAlly; DeathWoundFoe += o.DeathWoundFoe; EndWoundAlly += o.EndWoundAlly; EndWoundFoe += o.EndWoundFoe;
        Sever += o.Sever; SeverWounds += o.SeverWounds; SeverReached += o.SeverReached;
    }
}

/// <summary>
/// 灯の対象選択（第113期・<c>lit</c>）の集計。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class LtStat
{
    public int N, Wins;
    public double Turns;
    public double Fires, Switches, Yields, Stalls, Gate2, Doused, Lumen, Peak, Idle;
    public double StallStun, StallSlumber, StallCanAct;
    public double YieldDmg, TeamDmg, NoDeath, NoTarget, Saw2;
    public double SkipStatic, SkipNow;
    /// <summary>灯の受け手（駒名 → 灯した回数）。</summary>
    public Dictionary<string, int> LitBy = new(StringComparer.Ordinal);
    /// <summary>濾された駒（駒名 → 回数）。<c>SkipStaticBy</c> は W1 の静的な濾し、<c>SkipNowBy</c> は W2。</summary>
    public Dictionary<string, int> SkipStaticBy = new(StringComparer.Ordinal);
    public Dictionary<string, int> SkipNowBy = new(StringComparer.Ordinal);

    public double Win => N == 0 ? 0 : Wins * 100.0 / N;
    public double Per(double x) => N == 0 ? 0 : x / N;

    public static void Bump(Dictionary<string, int> bag, string key, int n)
        => bag[key] = bag.TryGetValue(key, out int had) ? had + n : n;

    public void AddFrom(LtStat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        Fires += o.Fires; Switches += o.Switches; Yields += o.Yields; Stalls += o.Stalls;
        Gate2 += o.Gate2; Doused += o.Doused; Lumen += o.Lumen; Peak += o.Peak; Idle += o.Idle;
        StallStun += o.StallStun; StallSlumber += o.StallSlumber; StallCanAct += o.StallCanAct;
        YieldDmg += o.YieldDmg; TeamDmg += o.TeamDmg;
        NoDeath += o.NoDeath; NoTarget += o.NoTarget; Saw2 += o.Saw2;
        SkipStatic += o.SkipStatic; SkipNow += o.SkipNow;
        foreach (var kv in o.LitBy) Bump(LitBy, kv.Key, kv.Value);
        foreach (var kv in o.SkipStaticBy) Bump(SkipStaticBy, kv.Key, kv.Value);
        foreach (var kv in o.SkipNowBy) Bump(SkipNowBy, kv.Key, kv.Value);
    }
}

/// <summary>
/// 積み過ぎ（第115期・<c>reader</c>）の1戦ぶんの観測。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class RdRow
{
    public int Bench, Ver, Wave, Turns;
    public bool Won;
    /// <summary>読み手が生きていたターン数（門1 の分母）と、閾値以上だったターン数（分子）。</summary>
    public int Alive, Over, FirstOver;
    /// <summary><c>PerformAttack</c> を通った総回数 ／ そのうち薙ぎ（門2）／ 閾値以上で振ったターン数。</summary>
    public int Swings, Sweeps, OverSwung;
    public int BonusSum, BonusMax;
    public int[] Probe = System.Array.Empty<int>();
    public int Dmg, TeamDmg, Whet;
}

/// <summary>
/// ボスの土台（第117期・<c>boss</c>）の1戦ぶんの観測。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数（<c>BossRule</c>）を読み直しているだけ。
/// </summary>
sealed class BsRow
{
    /// <summary>決着ターン（<c>BattleResult.Turns</c>）と勝敗。</summary>
    public int Turns;
    public bool Won;
    /// <summary>味方が最後にターン頭で生きていたターン（＝生存T）。</summary>
    public int PartyAlive;
    /// <summary>育つ側の生きていたターン数 ／ 1度でも振ったターン数（<b>空振り = 差</b>）。</summary>
    public int GrowAlive, GrowSwing, GrowLastAlive, GrowDmg;
    /// <summary>育つ側の <c>CurrentAttack</c>（開戦・前半末・終端・最大）。<b>生の <c>AtkBonus</c> ではない。</b></summary>
    public int Atk1, Atk3, AtkEnd, AtkMax;
    /// <summary>味方の与ダメ（前半3T ／ 後半3T ／ 総計）。</summary>
    public int DmgEarly, DmgLate, DmgTotal;
    /// <summary>
    /// <b>育つ側だけ</b>の与ダメ（前半3T ／ 後半3T）。味方全体の傾きには
    /// 「他の駒が倒れた」が混ざるので、育ちの本人だけを切り出した分子を別に持つ。
    /// </summary>
    public int GrowEarly, GrowLate;
    /// <summary>前半と後半が重ならない戦か（<c>Turns &gt;= 6</c>）。<b>傾きの分母はこれが真の戦だけ。</b></summary>
    public bool Ok;
}

/// <summary>
/// 第125期 —— `offturn` の走査子。<b>実装から引く表の共通部分</b>を1箇所に置く。
///
/// <para>メソッド1本ぶんの本文を<b>波括弧を数えて</b>切り出す（行番号では切らない・規約 (G15)）。
/// <b>走査が空なら呼び出し側が止める。</b></para>
/// </summary>
static class OffturnScan
{
    /// <summary>
    /// <b>第124期の観察ログとまったく同じ6行・同じ波・同じ seed</b>
    /// （<c>design/PHASE124_WATCH_LOG.md</c> の見出しから採った。第123期とも同一）。
    /// <c>Stage</c> は 0 始まり（第1波 = 0）。<b>行名は `Presets` と完全一致で照合する。</b>
    /// </summary>
    public static readonly (string Name, int Stage, int Seed)[] WatchRows =
    {
        ("死軸×ヒヨ (ゾト×火選り)",   3, 1),
        ("隊列崩し (バサ×ヨミ×セロ)", 1, 3),
        ("置き去り×分散回復",         1, 0),
        ("逆しま (ネル×ウツ)",        4, 15),
        ("止め改 (トメ×薙ぎ)",        3, 6),
        ("刻み×抉り (ノミ×エグ)",     4, 0),
    };

    /// <summary>署名から始まるメソッド1本ぶんの本文（波括弧を数える）。引けなければ空文字。</summary>
    public static string Body(string src, string signature)
    {
        int a = src.IndexOf(signature, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = src.IndexOf('{', a);
        if (b < 0) return "";
        int depth = 0;
        for (int i = b; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}' && --depth == 0) return src.Substring(b, i - b + 1);
        }
        return "";
    }

    /// <summary>その位置を含む <c>class Xxx : Trait</c> の名前（直前の宣言）。</summary>
    public static string EnclosingTrait(string src, int at)
    {
        string name = "—";
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(src, @"class\s+(\w+)\s*:\s*Trait"))
        {
            if (m.Index > at) break;
            name = m.Groups[1].Value;
        }
        return name;
    }

    /// <summary>
    /// <c>DemoApp/Main.cs</c> の <c>ApplyEvent</c> から、出来事の種類ごとの間（秒・速度 ×1）を引く。
    /// <b>条件付きの間も上から数える</b>——旧版と新版を<b>同じ規則</b>で数えるので比較は等質になる。
    /// <c>raw: true</c> の待ち（一時停止のポーリング）は <c>ApplyEvent</c> の外なので入らない。
    /// </summary>
    /// <b>無条件の間（<c>Map</c>）と条件付きの間（<c>Cond</c>）を分ける。</b>
    /// 条件付きは「その種類の出来事1件あたり必ず掛かる時間」ではないので、
    /// <b>総尺の重み付けに使うと上限しか出せない</b>——旧版にも新版にもあるので、
    /// <b>同じ規則で分けて、総尺は無条件のぶんだけで出す</b>（条件付きは件数を別に書く）。
    /// 判定は「その文（直前の <c>;</c> / <c>{</c> / <c>}</c> から <c>Delay</c> まで）に
    /// <c>if (</c> が含まれるか」。
    public static (Dictionary<string, double> Map, Dictionary<string, double> Cond, int Sites, int CondSites)
        Beats(string demoSrc)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        var cond = new Dictionary<string, double>(StringComparer.Ordinal);
        int sites = 0, condSites = 0;
        int a = demoSrc.IndexOf("private async Task " + "ApplyEvent", StringComparison.Ordinal);
        if (a < 0) return (map, cond, 0, 0);
        int b = demoSrc.IndexOf('{', a);
        if (b < 0) return (map, cond, 0, 0);
        int depth = 0, end = demoSrc.Length;
        for (int i = b; i < demoSrc.Length; i++)
        {
            if (demoSrc[i] == '{') depth++;
            else if (demoSrc[i] == '}' && --depth == 0) { end = i; break; }
        }
        string body = demoSrc.Substring(b, end - b);
        var marks = System.Text.RegularExpressions.Regex
            .Matches(body, @"case BattleEventKind\.(\w+)").ToList();
        for (int i = 0; i < marks.Count; i++)
        {
            int from = marks[i].Index;
            int to = i + 1 < marks.Count ? marks[i + 1].Index : body.Length;
            string span = body.Substring(from, to - from);
            double sum = 0, guarded = 0;
            foreach (System.Text.RegularExpressions.Match d in System.Text.RegularExpressions.Regex
                     .Matches(span, @"Delay\(\s*([0-9]*\.?[0-9]+)\s*\)"))
            {
                double v = double.Parse(d.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                int st = span.LastIndexOfAny(new[] { ';', '{', '}' }, d.Index);
                string stmt = span.Substring(st + 1, d.Index - st - 1);
                if (stmt.Contains("if (", StringComparison.Ordinal)) { guarded += v; condSites++; }
                else { sum += v; sites++; }
            }
            string kind = marks[i].Groups[1].Value;
            map[kind] = map.GetValueOrDefault(kind) + sum;
            cond[kind] = cond.GetValueOrDefault(kind) + guarded;
        }
        return (map, cond, sites, condSites);
    }
}
