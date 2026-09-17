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

// hush モード: **第二波の粛**（第27期）。
//
// 第二波は情報セル13・100%が22本で全5波中いちばん弱い検出器で、課金する資源は
// 後列到達力の1本だけだった。そこに「**ターン外の行動**」という2本目の軸を足す。
// **第四波と正反対の極**を狙っている——第四波は1発の重さに課金する（＝手数が有利）ので、
// ターン外の手数に課金するこの波とは逆を向く。第四波との相関が高ければ設計が失敗している。
//
// 規則は `HushRule` で `BattleEngine.Run` に**引数で渡す**（`gullet` / `yoke` と同じ。
// 書き換え可能な static のノブは置かない——Trait は共有シングルトンで layout は並列実行する）。
//
// **検算は3本。**
//   1. V0（中央 討伐隊の新兵）が差し替え前の `docs/balance.md` と一致する
//   2. V1（粛の伝令・規則を無効）が V0 と1セルも違わない
//      ——保持者は新兵と数値が同一なので、規則を切れば盤面は完全に同じに戻る
//      （**逆位はここを分けなかったせいで切り分けに追加測定が要った**）
//   3. 第二波以外は全版 ±0.0（保持者が不在なら規則は完全に不活性）
//
// 機構の確認は「経路ごとの発火数」でやる（主表の前の節）。**ここが要**——
// 粛が止めるのは `CanActOutOfTurn` を通る4本（棘・仇討ち・軋み・追い打ち）だけで、
// **肩代わりと責め苦は無風でなければならない**。勝率は「発火したが足りなかった」と
// 「一度も発火しなかった」を区別しないので、勝率だけ見ていてはこれが読めない。
//
// docs/ には置かない（診断用）。
//
//     dotnet run --project BattleSim -c Release 0 hush [log]
if (focusId == "hush")
{
    string hushMode = args.Length > 2 ? args[2] : "";
    var hushBuilds = CompareBuilds();
    const int HushSeeds = 200;   // compare / spread / yoke と同じ。balance.md と突き合わせる
    const int Wave2 = 1;         // 第二波（0 起点）

    // 第二波の中央だけを差し替えた版を診断のローカルで組む（gradient / aim / yoke と同じ扱い）。
    // 残り4枠も他の4波も EnemyCatalog のまま——**動く変数は中央の1枚と規則の有無だけ。**
    Formation Wave2With(UnitDef center) => Formation.Build(
        front1: EnemyCatalog.KnightG, front3: EnemyCatalog.KnightG, center: center,
        back1: EnemyCatalog.Almoner, back3: EnemyCatalog.ArcherG);

    Formation wave2 = Wave2With(EnemyCatalog.Husher);

    // 第二波の敵の Def.Id。tally を敵味方に割るのに使う（味方の召喚駒まで正しく味方側に落ちる）。
    var wave2EnemyIds = new HashSet<string>(new[]
    {
        EnemyCatalog.KnightG.Id, EnemyCatalog.RecruitG.Id, EnemyCatalog.Husher.Id,
        EnemyCatalog.Almoner.Id, EnemyCatalog.ArcherG.Id
    });

    // 粛が触る4経路と、**触ってはいけない**2経路。ログの文字列で数える。
    // （`gullet log` / `yoke log` と同じ理由——「その行が出たか／出なかったか」そのものを
    //  見たいので盤面の値では代用できない。発火しなかったことは値に痕跡を残さない）
    var paths = new (string Label, string Build, string Mark, bool ShouldStop)[]
    {
        ("棘 (カド)",       "反撃 (ヒサ×カド)",          "の棘が",                       true),
        ("仇討ち (ザン)",   "仇討ち (ヒサ×ザン)",        "の仇を討つ",                   true),
        ("軋み (ヨミ)",     "隊列崩し (バサ×ヨミ×セロ)", "はよろけた勢いのまま振り抜く", true),
        ("追い打ち (ハギ)", "追撃×毒 (ハギ×グザ)",       "が倒れた隙に踏み込む",         true),
        ("責め苦 (シガ)",   "責め苦 (トウ×シガ)",        "に追い打ちを重ねる",           false),
        ("巨躯 (ゴルム)",   "死の連鎖 (リィカ軸)",       "立ちはだかる",                 false),
    };

    // ---- log: 1戦の監査 -------------------------------------------------------------------
    if (hushMode == "log")
    {
        Console.WriteLine("# 粛の監査（hush log）");
        Console.WriteLine();
        Console.WriteLine("計画 §5 の `demo` 相当を1戦ずつ確かめる。規則は既定（`HushRule.Default`）。");
        Console.WriteLine();
        Console.WriteLine("**ここもログの文字列を数えている。** UI は `LogKind` を見るという規約に");
        Console.WriteLine("反して見えるが、確かめたいのは「その行が出たか／出なかったか」そのもので、");
        Console.WriteLine("**発火しなかったことは盤面の値に痕跡を残さない**（`gullet log` と同じ理由）。");
        Console.WriteLine();
        Console.WriteLine("**保持者の生死はログの行の並びで割る**（保持者の `OnDeath` が出す");
        Console.WriteLine("「ターン外の行動が戻った」の行より前か後か）。ターンで割ると、保持者が倒れた");
        Console.WriteLine("同じターンの後続の割り込みを誤検出する（`yoke log` で実際に踏んだ）。");

        foreach (var (label, buildKey, mark, shouldStop) in paths)
        {
            var (name, f) = hushBuilds.First(b => b.Name.Contains(buildKey));

            Console.WriteLine();
            Console.WriteLine($"## {label} — 粛は{(shouldStop ? "**止める**" : "**止めない**")}");
            Console.WriteLine();
            Console.WriteLine(shouldStop
                ? "`CanActOutOfTurn` を通る経路。保持者が生きている間は 0 回でなければならない。"
                : "**この窓口を通らない**経路（"
                  + (mark == "立ちはだかる"
                     ? "肩代わりは**ダメージの再分配であって行動ではない**"
                     : "責め苦は `OnAfterAttack` ＝**自分の手番の中**")
                  + "）。粛の下でも普通に働く。");

            // **探す事例は経路の種類で違う。**
            // 「止める」経路は〈保持者が倒れ、かつ mark がその後に出る〉——止まったことと
            // 倒せば戻ることを1戦で同時に見せる。「止めない」経路は〈保持者の生存中に mark が出る〉
            // ——こちらは倒す必要がなく、むしろ生存中に出ることが証拠になる。
            // 見つからなければ走査全体の集計を書く（1戦だけ見て「出なかった」と書くと、
            // **一度も発火しなかったのか、条件を満たす seed が無かったのか**が分からない）。
            const int Scan = 60;
            int shown = 0, deaths = 0;
            double sumOn = 0, sumAlive = 0, sumOff = 0;
            for (int seed = 0; seed < Scan; seed++)
            {
                var on = BattleEngine.Run(f, wave2, seed, verbose: true)
                                    .Log.Select(l => l.Text).ToList();
                int deathAt = on.FindIndex(t => t.Contains("ターン外の行動が戻った"));
                int before = on.Take(deathAt < 0 ? on.Count : deathAt).Count(t => t.Contains(mark));
                int after = deathAt < 0 ? 0 : on.Skip(deathAt).Count(t => t.Contains(mark));

                if (deathAt >= 0) deaths++;
                sumOn += before + after;
                sumAlive += before;
                sumOff += BattleEngine.Run(f, wave2, seed, verbose: true, null, null,
                                           new HushRule(Active: false))
                                      .Log.Count(l => l.Text.Contains(mark));

                if (shown > 0) continue;
                if (shouldStop ? (deathAt < 0 || after == 0) : before == 0) continue;

                // 同じ seed を「規則だけ切った版」でも回して、発火数の素の量を出す。
                int plain = BattleEngine.Run(f, wave2, seed, verbose: true, null, null,
                                             new HushRule(Active: false))
                                        .Log.Count(l => l.Text.Contains(mark));

                Console.WriteLine();
                Console.WriteLine($"{name} / 第二波 / seed {seed}");
                Console.WriteLine();
                Console.WriteLine($"- `{mark}` の発火: **保持者の生存中 {before} 回 / 撃破後 {after} 回**"
                    + $"（規則を切ると同じ seed で {plain} 回）");
                Console.WriteLine($"- 保持者が倒れた行: {deathAt} 行目");
                if (shouldStop && before != 0)
                    Console.WriteLine($"- **受け入れ不合格: 生存中に {before} 回出ている**");

                Console.WriteLine();
                Console.WriteLine("```");
                // 「止める」側は保持者の死の周りを、「止めない」側は最初の発火の周りを抜く。
                int at = shouldStop ? deathAt : on.FindIndex(t => t.Contains(mark));
                for (int i = Math.Max(0, at - 1); i < Math.Min(on.Count, at + 6); i++)
                    Console.WriteLine(on[i]);
                Console.WriteLine("```");
                shown++;
            }
            Console.WriteLine();
            Console.WriteLine($"seed 0..{Scan - 1} の集計: 保持者が倒れた戦 **{deaths} / {Scan}** ／ "
                + $"`{mark}` の発火 規則なし **{sumOff / Scan:F2}/戦** → 規則あり **{sumOn / Scan:F2}/戦**"
                + $"（うち保持者の生存中 **{sumAlive / Scan:F2}/戦**）。");
            if (shown == 0)
            {
                Console.WriteLine();
                Console.WriteLine($"（{(shouldStop ? "「保持者が倒れ、その後に発火する」" : "「保持者の生存中に発火する」")}"
                    + "事例は 0 件だったので抜粋なし。**集計の行で読むこと**——"
                    + "抜粋が無いのは発火しなかったからとは限らない。）");
            }
        }
        return;
    }

    // ---- 版の並び ------------------------------------------------------------------------
    var hVersions = new (string Name, string Note, UnitDef Center, HushRule Rule)[]
    {
        ("V0 現行",   "中央 討伐隊の新兵（粛なし）＝**差し替え前の盤面**",
            EnemyCatalog.RecruitG, HushRule.Default),
        ("V1 壁のみ", "中央 粛の伝令・**規則は無効**（数値は V0 と同一）",
            EnemyCatalog.Husher, new HushRule(Active: false)),
        ("V2 粛",     "中央 粛の伝令・規則あり（**本命**）",
            EnemyCatalog.Husher, HushRule.Default),
    };

    int hnv = hVersions.Length, hnb = hushBuilds.Length, hnw = EnemyCatalog.Stages.Count;

    var hboard = new Formation[hnv][];
    for (int v = 0; v < hnv; v++)
    {
        hboard[v] = new Formation[hnw];
        for (int w = 0; w < hnw; w++)
            hboard[v][w] = w == Wave2 ? Wave2With(hVersions[v].Center) : EnemyCatalog.Stages[w].Enemy;
    }

    var hrate = new double[hnv][][];        // hrate[版][編成][波] = 勝率(%)
    var hFoe = new double[hnv][];           // 第二波・1戦あたりの「敵が受けたダメージ」＝味方の出力
    var hAlly = new double[hnv][];          // 第二波・1戦あたりの「味方が受けたダメージ」
    var hTurns = new double[hnv][];         // 第二波・決着ターン

    for (int v = 0; v < hnv; v++)
    {
        hrate[v] = new double[hnb][];
        hFoe[v] = new double[hnb];
        hAlly[v] = new double[hnb];
        hTurns[v] = new double[hnb];

        for (int b = 0; b < hnb; b++)
        {
            hrate[v][b] = new double[hnw];
            for (int w = 0; w < hnw; w++)
            {
                int wins = 0;
                long foe = 0, ally = 0, turns = 0;
                for (int seed = 0; seed < HushSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(hushBuilds[b].F, hboard[v][w], seed,
                                                      verbose: false, null, null, hVersions[v].Rule);
                    if (r.PlayerWon) wins++;
                    if (w != Wave2) continue;

                    turns += r.Turns;
                    // **与ダメは受け手側から取る**（第13期 Phase DA）。毒・燃焼は source が
                    // null なので味方側から合計すると毒軸の出力が構造的に過小になる。
                    foreach ((string id, UnitTally t) in r.TallyByUnit)
                        if (wave2EnemyIds.Contains(id)) foe += t.DamageTaken;
                        else ally += t.DamageTaken;
                }
                hrate[v][b][w] = wins * 100.0 / HushSeeds;
                if (w != Wave2) continue;
                hFoe[v][b] = (double)foe / HushSeeds;
                hAlly[v][b] = (double)ally / HushSeeds;
                hTurns[v][b] = (double)turns / HushSeeds;
            }
        }
        Console.Error.WriteLine($"  {hVersions[v].Name} 完了");
    }

    Console.WriteLine("# 第二波の粛（hush）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 {hnb} × 全 {hnw} 波 × {hnv} 版、seed 0..{HushSeeds - 1}。"
        + "診断用なので docs/ には置かない。");
    Console.WriteLine();
    foreach (var vv in hVersions) Console.WriteLine($"- **{vv.Name}**: {vv.Note}");

    // --- 検算 1 --------------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 検算 1: V0 × 全編成");
    Console.WriteLine();
    Console.WriteLine("**このセルは差し替え前の `docs/balance.md`（`git show HEAD:docs/balance.md`）と");
    Console.WriteLine("一致しなければならない。** ずれていたら診断の組み方（seed 帯・台・編成リスト）が");
    Console.WriteLine("balance.md と揃っていない。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, hnw).Select(i => $" 第{i}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, hnw).Select(_ => "---:|")));
    for (int b = 0; b < hnb; b++)
        Console.WriteLine($"| {hushBuilds[b].Name} |" + string.Concat(hrate[0][b].Select(x => $" {x:F1}% |")));

    // --- 検算 2 --------------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 検算 2: V1（壁のみ）= V0");
    Console.WriteLine();
    Console.WriteLine("粛の伝令は討伐隊の新兵と**数値が1つも違わない**ので、規則を切れば盤面は完全に同じになる。");
    Console.WriteLine("**ここが 0 件でなければ、差し替えが数値も動かしている**（逆位の失敗の直接の原因）。");
    Console.WriteLine();
    var h1stray = new List<string>();
    for (int b = 0; b < hnb; b++)
        for (int w = 0; w < hnw; w++)
            if (Math.Abs(hrate[1][b][w] - hrate[0][b][w]) > 1e-9)
                h1stray.Add($"{hushBuilds[b].Name} / 第{w + 1}波: {hrate[0][b][w]:F1}% → {hrate[1][b][w]:F1}%");
    Console.WriteLine($"{hnb} 編成 × {hnw} 波 = {hnb * hnw} セル中、**食い違い {h1stray.Count} 件**。");
    foreach (string x in h1stray.Take(40)) Console.WriteLine($"- {x}");

    // --- 検算 3 --------------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 検算 3: 第一・三・四・五波は全版 ±0.0");
    Console.WriteLine();
    Console.WriteLine("粛の保持者は第二波にしかいないので、他の4波は1セルも動いてはいけない（計画 §5）。");
    Console.WriteLine("**動いていたら規則が保持者の不在下でも効いている。**");
    Console.WriteLine();
    var hOther = new List<string>();
    for (int v = 1; v < hnv; v++)
        for (int b = 0; b < hnb; b++)
            for (int w = 0; w < hnw; w++)
                if (w != Wave2 && Math.Abs(hrate[v][b][w] - hrate[0][b][w]) > 1e-9)
                    hOther.Add($"{hushBuilds[b].Name} / {hVersions[v].Name} / 第{w + 1}波: "
                               + $"{hrate[0][b][w]:F1}% → {hrate[v][b][w]:F1}%");
    Console.WriteLine($"{hnb} 編成 × {hnv - 1} 版 × {hnw - 1} 波 = {hnb * (hnv - 1) * (hnw - 1)} セル中、"
                      + $"**食い違い {hOther.Count} 件**。");
    foreach (string x in hOther.Take(40)) Console.WriteLine($"- {x}");

    // --- 機構: 経路ごとの発火数 -----------------------------------------------------------
    //
    // **主表より先に置く。** 勝率は「発火したが足りなかった」と「一度も発火しなかった」を
    // 区別しないので、勝率が動いたことは機構が動いた証拠にならない（第26期の教訓）。
    Console.WriteLine();
    Console.WriteLine("## 機構: 窓口を通る4経路と、通らない2経路");
    Console.WriteLine();
    Console.WriteLine("第二波・seed 0..49 の**ログの行数**を版ごとに数える。粛が止めるのは");
    Console.WriteLine("`CanActOutOfTurn` を通る4本だけで、**肩代わりと責め苦は無風でなければならない**。");
    Console.WriteLine("`V2/戦` が 0 でないのは保持者を倒した後の発火（規則は倒したその場から外れる）。");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 窓口 | 編成 | ログ行 | V1/戦 | V2/戦 | 保持者の生存中/戦 |");
    Console.WriteLine("|---|:-:|---|---|--:|--:|--:|");
    const int PathSeeds = 50;
    foreach (var (label, buildKey, mark, shouldStop) in paths)
    {
        var (name, f) = hushBuilds.First(b => b.Name.Contains(buildKey));
        double v1 = 0, v2 = 0, alive = 0;
        for (int seed = 0; seed < PathSeeds; seed++)
        {
            v1 += BattleEngine.Run(f, wave2, seed, verbose: true, null, null, new HushRule(Active: false))
                              .Log.Count(l => l.Text.Contains(mark));
            var on = BattleEngine.Run(f, wave2, seed, verbose: true).Log.Select(l => l.Text).ToList();
            v2 += on.Count(t => t.Contains(mark));
            int deathAt = on.FindIndex(t => t.Contains("ターン外の行動が戻った"));
            alive += on.Take(deathAt < 0 ? on.Count : deathAt).Count(t => t.Contains(mark));
        }
        Console.WriteLine($"| {label} | {(shouldStop ? "通る" : "通らない")} | {name} | `{mark}` "
            + $"| {v1 / PathSeeds:F2} | {v2 / PathSeeds:F2} | {alive / PathSeeds:F2} |");
    }
    Console.WriteLine();
    Console.WriteLine("**受け入れ条件**: 「通る」4本の `保持者の生存中/戦` が **0.00**、");
    Console.WriteLine("「通らない」2本の `V1/戦` と `V2/戦` が**一致**すること。");

    // --- 主表: 第二波 ---------------------------------------------------------------------
    bool HMid(int b) => hrate[0][b][Wave2] > 5.0 && hrate[0][b][Wave2] < 95.0;

    Console.WriteLine();
    Console.WriteLine("## 主表: 第二波の勝率 × 各版");
    Console.WriteLine();
    Console.WriteLine("`中` は **V0 の第二波が中間帯（5% < x < 95%）にある編成**＝この台で増分が読める編成");
    Console.WriteLine("（第24期 yield の教訓。飽和したセルでは誰に何をしても 0 に潰れる）。");
    Console.WriteLine("第二波は V0 で 100% が多数なので、ほとんどの行は「落ちたかどうか」だけを見る。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 中 |" + string.Concat(hVersions.Select(v => $" {v.Name} |")) + " V2−V0 |");
    Console.WriteLine("|---|:-:|" + string.Concat(hVersions.Select(_ => "---:|")) + "---:|");
    foreach (int b in Enumerable.Range(0, hnb).OrderBy(b => hrate[2][b][Wave2] - hrate[0][b][Wave2]))
        Console.WriteLine($"| {hushBuilds[b].Name} | {(HMid(b) ? "●" : "")} |"
            + string.Concat(Enumerable.Range(0, hnv).Select(v => $" {hrate[v][b][Wave2]:F1}% |"))
            + $" {hrate[2][b][Wave2] - hrate[0][b][Wave2]:+0.0;-0.0} |");

    // --- 第26期の読み手2体 -----------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 第26期の3編成（計画 §5 でいちばん見たい1点）");
    Console.WriteLine();
    Console.WriteLine("**ザン（仇討ち）は窓口を通るので課金され、シガ（責め苦）は自分の手番内なので無風。**");
    Console.WriteLine("同じフェーズで作った読み手2体が、敵側ルール1つで割れるか。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | V0 | V2 | 差 | 敵被ダメ/戦 V1→V2 | 決着T V1→V2 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    foreach (string key in new[] { "責め苦", "仇討ち (", "仇討ち×砕け" })
    {
        int b = Enumerable.Range(0, hnb).First(i => hushBuilds[i].Name.Contains(key));
        Console.WriteLine($"| {hushBuilds[b].Name} | {hrate[0][b][Wave2]:F1} | {hrate[2][b][Wave2]:F1} "
            + $"| {hrate[2][b][Wave2] - hrate[0][b][Wave2]:+0.0;-0.0} "
            + $"| {hFoe[1][b]:F0} → {hFoe[2][b]:F0} ({hFoe[2][b] - hFoe[1][b]:+0;-0}) "
            + $"| {hTurns[1][b]:F1} → {hTurns[2][b]:F1} |");
    }

    // --- 判定 -----------------------------------------------------------------------------
    List<string> HLosers(int v, int w) => Enumerable.Range(0, hnb)
        .Where(b => hrate[v][b][w] <= 0.0
                    && Enumerable.Range(1, hnw - 1).All(o => o == w || hrate[v][b][o] > 0.0))
        .Select(b => hushBuilds[b].Name).ToList();
    List<string> HWinners(int v, int w) => Enumerable.Range(0, hnb)
        .Where(b => hrate[v][b][w] >= 100.0
                    && Enumerable.Range(1, hnw - 1).All(o => o == w || hrate[v][b][o] < 100.0))
        .Select(b => hushBuilds[b].Name).ToList();

    Console.WriteLine();
    Console.WriteLine("## 判定（計画 §5）");
    Console.WriteLine();
    Console.WriteLine("`spread` の (1)(2)(3) を第二波について版ごとに引き直したもの。");
    Console.WriteLine("**固有の敗者/勝者は第一波を比較から外して数える**（第22期 Phase 2b）。");
    Console.WriteLine("**第4波との相関を必ず見る**——正反対の極を狙っているので、高ければ設計が失敗している。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 平均 | 100%の編成 | 0%の編成 | 中間帯 | 標準偏差 | 固有の敗者 | 固有の勝者 | 第4波との相関 | 第5波との相関 | 第2〜4波すべて100% |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int v = 0; v < hnv; v++)
    {
        double[] col = Enumerable.Range(0, hnb).Select(b => hrate[v][b][Wave2]).ToArray();
        double mean = col.Average();
        double sd = Math.Sqrt(col.Select(x => (x - mean) * (x - mean)).Sum() / col.Length);
        double c4 = HCorr(Enumerable.Range(0, hnb).Select(b => hrate[v][b][3]).ToArray(), col);
        double c5 = HCorr(Enumerable.Range(0, hnb).Select(b => hrate[v][b][4]).ToArray(), col);
        int allTop = Enumerable.Range(0, hnb)
            .Count(b => Enumerable.Range(1, 3).All(w => hrate[v][b][w] >= 100.0));
        Console.WriteLine($"| {hVersions[v].Name} | {mean:F1} | {col.Count(x => x >= 100.0)} / {hnb} "
            + $"| {col.Count(x => x <= 0.0)} | {col.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} "
            + $"| {HLosers(v, Wave2).Count} | {HWinners(v, Wave2).Count} "
            + $"| {(double.IsNaN(c4) ? "—" : $"{c4:+0.00;-0.00}")} "
            + $"| {(double.IsNaN(c5) ? "—" : $"{c5:+0.00;-0.00}")} | {allTop} / {hnb} |");
    }
    Console.WriteLine();
    for (int v = 0; v < hnv; v++)
    {
        var lose = HLosers(v, Wave2);
        var win = HWinners(v, Wave2);
        Console.WriteLine($"- **{hVersions[v].Name}** 固有の敗者 ({lose.Count}): "
            + (lose.Count == 0 ? "**なし**" : string.Join(" / ", lose))
            + $" ／ 固有の勝者 ({win.Count}): " + (win.Count == 0 ? "なし" : string.Join(" / ", win)));
    }

    // --- 動いた編成 -----------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 動いた編成（V2−V0 の順）");
    Console.WriteLine();
    Console.WriteLine("計画 §3 の予測（4経路を持つ編成だけが課金される）と突き合わせる列。");
    Console.WriteLine("**`中` が付いていない行の 0.0 は「無風」ではなく「読めない」**——飽和したセルなので。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 中 | V0 | V2 | 差 | 敵被ダメ/戦 V1→V2 | 味方被ダメ/戦 V1→V2 | 決着T V1→V2 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|");
    foreach (int b in Enumerable.Range(0, hnb).OrderBy(b => hrate[2][b][Wave2] - hrate[0][b][Wave2]))
        Console.WriteLine($"| {hushBuilds[b].Name} | {(HMid(b) ? "●" : "")} | {hrate[0][b][Wave2]:F1} "
            + $"| {hrate[2][b][Wave2]:F1} | {hrate[2][b][Wave2] - hrate[0][b][Wave2]:+0.0;-0.0} "
            + $"| {hFoe[1][b]:F0} → {hFoe[2][b]:F0} ({hFoe[2][b] - hFoe[1][b]:+0;-0}) "
            + $"| {hAlly[1][b]:F0} → {hAlly[2][b]:F0} ({hAlly[2][b] - hAlly[1][b]:+0;-0}) "
            + $"| {hTurns[1][b]:F1} → {hTurns[2][b]:F1} |");

    Console.WriteLine();
    Console.WriteLine("`敵被ダメ/戦` は**受け手側から数えた味方の出力**（第13期 Phase DA）。");
    Console.WriteLine("V1 → V2 の減りが、そのまま**黙らされたターン外の打点**。");
    return;

    // ピアソン相関。片方の分散が 0 なら定義できないので NaN を返す（呼び出し側で — に置く）。
    static double HCorr(double[] a, double[] b)
    {
        double ma = a.Average(), mb = b.Average();
        double num = 0, da = 0, db = 0;
        for (int i = 0; i < a.Length; i++)
        {
            num += (a[i] - ma) * (b[i] - mb);
            da += (a[i] - ma) * (a[i] - ma);
            db += (b[i] - mb) * (b[i] - mb);
        }
        return da <= 0 || db <= 0 ? double.NaN : num / Math.Sqrt(da * db);
    }
}

// spread モード: **波の側**の分離度を測る（第22期 Phase 1）。
//
// 既存モードは全部「編成の側」を見ている（どの編成が強いか）。ここで見たいのは逆で、
// **5つ並べた波が、互いに違うことを測っているか**。第19〜21期は3期続けて土台が
// 飽和して止まった（19: 第1〜3波 100% / 20: 全5波 100.0% / 21: 全版 100/0/0/0/0）。
// 新しい機構を測る台が無いという同じ壁なので、波を触る前に**まず物差しを作って
// 現状値を固定する**。これが無いと「作り直して良くなったか」が主観になる。
//
// 出す表は3つ。
//
//   1. 波ごとの飽和   平均・100%の編成数・0%の編成数・中間帯の数・標準偏差。
//                     100% と 0% で埋まった波は、その編成たちを区別していない
//   2. 波間の相関     別の波として並べているのに同じことを測っていないか。
//                     第一波は全編成 100% で分散 0 なので相関は定義できない（—）
//   3. 固有の勝者・敗者  その波でだけ 100%（他では 100% 未満）／その波でだけ 0%（他では 0% 超）
//                     の編成。**これが波の個性の実体**で、ここが空の波は独立していない。
//                     **第一波は比較対象から外す**——全編成 100% を意図して維持している波なので、
//                     比較に入れると第2〜5波の固有の勝者が恒等的に 0 になる
//
// 中間帯は **5 < x < 95 の狭義**。境界を含めると 5.0% ちょうどの編成（速攻の第二波）が
// 「分離できている」側に入るが、あれは床に張り付いている。
// 「分散」列は**母標準偏差**（勝率と同じ pt 単位で読めるようにするため。分散だと pt² になる）。
//
// **docs/ には出さない**（診断用）。ただしこの3つの表は README に貼って残す
// ——作り直しの前後で比べる基準値になる。
//
// 殉教者の体の用量反応（第34期）。第五波の前1（殉教者）の **HP だけ**を振って、
// 介入の試験が立つ最小の体を探す。
//
// **UnitCatalog は触らない。** 変種は診断のローカルに組む（gradient / aim / timing と同じ扱い）
// ——`Stages` を書き換えると compare / dump が動いてしまい、掃引と本測定が混ざる。
// 動かすのは HP の1変数のみ（攻11・速5・薙ぎ・Guardian・前1の席はすべて据え置き）。
//
// 掃引点は**新しい数値を発明せず、既存の敵の体から借りる**:
//   52  = 現行（戦斧兵 axeman_v と同値）
//   71  = 第五波の中央（巡礼騎士 knight_v）の体。52 と 90 の中点
//   90  = 第五波の前3（勇者候補 hero_v）の体
//   145 = ロスター最重（城塞の重装兵 warden / 軛の重装兵 yoker）。上端の当たり所
//
// 第五波だけを測る（他の波には殉教者が出ないので測る意味が無い）。ただし
// **固有の敗者の判定には第2〜4波が要る**ので、そこは1回だけ測って全HP点で使い回す
// （殉教者がいないので HP を振っても1セルも動かない）。
//
// 機構の指標（肩代わりの発火・殉教者の最終攻・生存ターン）は verbose のログ行を数える
// ——`gullet log` / `yoke log` / `hush` と同じ理由で、**発火しなかったことは盤面の値に
// 痕跡を残さない**（庇いは標的を差し替えるだけで、tally には「逸れた」痕跡が残らない）。
//
// docs/ には置かない（診断用）。
//
//     dotnet run --project BattleSim -c Release 0 guard
// sever モード（第37期・使い捨ての診断）: 断ち（ナタ）の発火と手番の放棄を数える。
//
// **ここもログの文字列を数えている**（`gullet log` / `yoke log` / `hush` と同じ理由）。
// 断ちの上乗せは `ApplyDamage` を1回通るだけなので与ダメの総量に溶けてしまうし、
// **振らなかったこと（手番の放棄）は盤面の値に痕跡を1つも残さない**——
// 「その行が出たか／何回出たか」を数える以外に発火を捕まえる方法が無い。
//
// **`docs/` には置かない。** 標準出力で読むだけ。
//
//     dotnet run --project BattleSim -c Release 0 sever [絞り込み]
if (focusId == "sever")
{
    var sevBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> sevStages = EnemyCatalog.Stages;
    const int SevSeeds = 50;
    string sevSub = args.Length > 2 ? args[2] : "";
    string nata = UnitCatalog.Nata.Name;

    // ---- sale: 捨てた手番は売り物になっていないか（1-1 (c) の受け入れ）------------------
    //
    // **第37期の2台には号令も据えも入っていない**ので、`SurrendersTurn => false` は
    // 本編の測定では一度も試されない。第36期の教訓（買い手を持たない台では機構の発火を
    // 1件も観測できない）と同じ穴なので、**買い手を揃えた台を診断のローカルに組んで**測る。
    //
    // 台は「供給源が1枚も無い」形——ナタは毎ターン振れないので、`SurrendersTurn` が
    // true なら号令（次のターン 攻撃+8）と据え（そのターン 被ダメ-50%）の**無償の収入源**になる。
    // **陽性対照はドルガ**（のろま。`SurrendersTurn` は true なので買われるはず）で、
    // 同じ台の同じ号令が働いていることをここで確かめる——これが 0 なら台が壊れている。
    //
    //     dotnet run --project BattleSim -c Release 0 sever sale
    if (sevSub == "sale")
    {
        var sale = Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Dolga,
                                   center: UnitCatalog.Gan, back1: UnitCatalog.Ban,
                                   back3: UnitCatalog.Nata);
        string gan = UnitCatalog.Gan.Name, dolga = UnitCatalog.Dolga.Name;

        Console.WriteLine("# 断ちの捨てた手番は売れるか（第37期・診断。docs/ には置かない）");
        Console.WriteLine();
        Console.WriteLine("台は **供給源ゼロ**（ゴルム／ドルガ／ガン＝号令／バン＝据え／ナタ）。");
        Console.WriteLine("ナタは傷持ちを一度も狙えないので毎ターン手番を捨てる。");
        Console.WriteLine($"`SurrendersTurn` が true なら、この台でナタは毎ターン 攻撃+{RallyTrait.Gain} と");
        Console.WriteLine($"被ダメ-{BulwarkTrait.ReductionPercent}% を無償で受け取る。**陽性対照はドルガ**（のろま＝true）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | ナタの放棄/戦 | 号令→ナタ | 据え→ナタ | 号令→ドルガ（陽性対照） | 据え→ドルガ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        for (int w = 0; w < sevStages.Count; w++)
        {
            double idle = 0, rn = 0, bn = 0, rd = 0, bd = 0, n = 0;
            for (int seed = 0; seed < SevSeeds; seed++)
            {
                BattleResult res = BattleEngine.Run(sale, sevStages[w].Enemy, seed, verbose: true);
                n++;
                int turn = 0, last = -1;
                foreach (LogLine l in res.Log)
                {
                    string t = l.Text;
                    if (l.Kind == LogKind.Turn) { turn++; continue; }
                    // 捨てた手番はどちらの理由でも1つ（第38期で待ちが2種に割れた）。
                    // この台には供給源が1枚も無いので実際に出るのは「閉じた肌」だけだが、
                    // 数えたいのは**失った手番の数**なので両方を拾う。
                    if (t.Contains($"{nata} は閉じた肌に刃を下ろさない")
                        || t.Contains($"{nata} は傷がまだ浅いと刃を上げない"))
                    { if (turn != last) { idle++; last = turn; } continue; }
                    if (t.Contains($"{gan} の号令で {nata} の溜めが乗った")) rn++;
                    else if (t.Contains($"{gan} の号令で {dolga} の溜めが乗った")) rd++;
                    else if (t.Contains($"据えが差し出した {nata} の被弾を")) bn++;
                    else if (t.Contains($"据えが差し出した {dolga} の被弾を")) bd++;
                }
            }
            Console.WriteLine($"| 第{w + 1}波 | {idle / n:0.00} | {rn / n:0.00} | {bn / n:0.00} | {rd / n:0.00} | {bd / n:0.00} |");
        }
        Console.WriteLine();
        Console.WriteLine("ナタ側が 0 / ドルガ側が正なら、**同じ号令・同じ据えが働いている台で");
        Console.WriteLine("ナタの手番だけが売り物になっていない**＝ 1-1 (c) が効いている。");
        return;
    }

    // ---- reach: 到達可能性（第38期 Phase 0。閾値を決める前に数える）---------------------
    //
    // **問い: 現行の 刻み×断ち の盤面で、1体の敵の傷は 3 まで積み得るか。**
    //
    // 新ルール（閾値待ち）ではナタが待つ間ノミが書き続けるので、近似は
    // 「ノミが同一の生存敵に刻んだ回数（＝その敵が抱える傷の深さ）の1戦あたり最大値」。
    // **現行の盤面ではナタが断つたびに傷が 0 に戻る**ので、そのリセットを無視して数える
    // ＝ 閾値を入れた後の在庫の下限の近似になる（待つぶん敵は長く生きるので、実際は増える側）。
    //
    // **ログではなく `Events` から数える。** 敵は同じ def が複数立つ波があり
    // （名前が衝突する）、文字列では「同一の敵」を指せない。`InstanceId` は
    // Deploy の順（味方スロット昇順 → 敵スロット昇順）で振られるので、ノミの席から引ける。
    //
    // **第38期の Phase 0 の値は閾値を入れる前に測った**（`SeverTrait.Threshold` 導入前）。
    // いま走らせると閾値待ちの入った盤面を測るので数字は一致しない——ゲートの記録は
    // design/PHASE38_SEVER_CADENCE.md 側にある。導入後に走らせると
    // 「待たせたぶん在庫が実際に伸びたか」の事後確認になる（別の問い）。
    //
    //     dotnet run --project BattleSim -c Release 0 sever reach
    if (sevSub == "reach")
    {
        var reachRow = sevBuilds.First(x => x.Name.Contains("刻み×断ち"));
        // ノミの InstanceId ＝ 味方をスロット昇順に並べたときの位置（ctx.Add の順）。
        int nomiId = reachRow.F.Occupied().Select((x, i) => (x.Def.Id, i))
                             .First(t => t.Id == UnitCatalog.Nomi.Id).i;

        Console.WriteLine("# 傷の到達可能性（第38期 Phase 0・診断。docs/ には置かない）");
        Console.WriteLine();
        Console.WriteLine($"台は `{reachRow.Name}`。seed 0..{SevSeeds - 1} × 全波。");
        Console.WriteLine("**ナタの消費を無視して**、ノミが同一の生存敵に刻んだ回数を数えた");
        Console.WriteLine("（敵が倒れたらその敵の計数は 0 に戻す）。1戦あたりの最大値の分布。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 中央値 | 平均 | 最大 | ≥2 の戦 | ≥3 の戦 | ≥4 の戦 | ノミの振/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        var reachMed = new double[sevStages.Count];
        for (int w = 0; w < sevStages.Count; w++)
        {
            var peaks = new List<int>();
            double swings = 0;
            for (int seed = 0; seed < SevSeeds; seed++)
            {
                BattleResult res = BattleEngine.Run(reachRow.F, sevStages[w].Enemy, seed, verbose: true);
                var depth = new Dictionary<int, int>();
                int peak = 0;
                foreach (BattleEvent e in res.Events)
                {
                    if (e.Kind == BattleEventKind.Attack && e.ActorId == nomiId && e.TargetId is { } t)
                    {
                        // 刻みは主目標にだけ・攻撃1回に1度。死体には刻まないので、
                        // この直後に Death が来たら下の分岐が 0 に戻す（順序は 攻撃 → ダメージ → 死亡）。
                        swings++;
                        depth[t] = depth.GetValueOrDefault(t) + 1;
                        if (depth[t] > peak) peak = depth[t];
                    }
                    else if (e.Kind == BattleEventKind.Death && e.TargetId is { } d)
                    {
                        depth[d] = 0;
                    }
                }
                peaks.Add(peak);
            }
            peaks.Sort();
            double med = peaks.Count % 2 == 1
                ? peaks[peaks.Count / 2]
                : (peaks[peaks.Count / 2 - 1] + peaks[peaks.Count / 2]) / 2.0;
            reachMed[w] = med;
            Console.WriteLine($"| 第{w + 1}波 | {med:0.0} | {peaks.Average():0.00} | {peaks.Max()} | "
                + $"{peaks.Count(p => p >= 2)} | {peaks.Count(p => p >= 3)} | {peaks.Count(p => p >= 4)} | "
                + $"{swings / SevSeeds:0.00} |");
        }
        Console.WriteLine();
        int ge3 = 0, ge2 = 0;
        for (int w = 1; w < sevStages.Count; w++) { if (reachMed[w] >= 3) ge3++; if (reachMed[w] >= 2) ge2++; }
        Console.WriteLine($"**第2〜5波のうち 中央値 ≥3 は {ge3} 波 / ≥2 は {ge2} 波。**");
        Console.WriteLine(ge3 >= 3 ? "→ `Threshold = 3` で Phase 1 へ。"
            : ge2 >= 3 ? "→ 中央値 3 の波が過半に届かない。**`Threshold = 2` に落として** Phase 1 へ（事前承認済みのフォールバック）。"
            : "→ 中央値が 2 にも届かない波が過半。**実装せず報告で止める。**");
        return;
    }

    string sevFilter = args.Length > 2 && args[2].Length > 0
        ? args[2] : "断ち,裂き (キリ×エグ),刻み×抉り";

    // 第37期に compare から落とした対照（`断ち (キリ×ナタ)`）を**診断のローカルに組む**
    // （`gradient` / `aim` / `route` と同じ扱い）。**`CompareBuilds()` には戻さない**
    // ——戻すと `docs/balance.md` の行が増えて「既存42行 ±0.0」の分母が動く。
    //
    // 配置は `confirm` の `picks` に残してある旧配置と同じ。閾値待ち（第38期）が
    // **供給の細い台に何をするか**の無料の対照で、キリは1ターンに傷を1つ撒くだけなので
    // 「同じ相手に2つ目が乗る」機会そのものが構造的に少ない。
    (string Name, Formation F) sevControl = ("断ち (キリ×ナタ)", Formation.Build(
        front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga,
        back1: UnitCatalog.Vel, back3: UnitCatalog.Nata));
    var sevRows = sevBuilds
        .Select(x => (Name: x.Name, F: x.F))
        .Append(sevControl)
        .Where(x => sevFilter.Split(',').Any(k => x.Name.Contains(k.Trim())))
        .ToList();

    string nomi = UnitCatalog.Nomi.Name;
    string egu = UnitCatalog.Egu.Name;

    // 「（傷 w → +x）」の w を取り出す。書式は裂き・抉り・刻み・断ちで共通。
    static int WoundOf(string text)
    {
        int a = text.IndexOf("（傷 ", StringComparison.Ordinal);
        if (a < 0) return 0;
        a += 3;
        int b = text.IndexOf(' ', a);
        return b > a && int.TryParse(text[a..b], out int w) ? w : 0;
    }

    // 0 発火 / 1 消費傷の総和 / 2 放棄ターン（獲物なし）/ 3 ナタの振り / 4 逸れた振り
    // 5 空振り（振ったが断てなかった）/ 6 軛で切られた発火 / 7 w>=6 の発火
    // 8 逸れた振りの基礎打点 / 9 ノミのなぞり発火 / 10 ノミのなぞり上乗せ総量
    // 11 エグのこじ開け発火 / 12 エグの上乗せ総量 / 13 戦数
    // 14 待ちターン（傷はあるが Threshold に届かない。第38期）
    //
    // **14 と 2 を分けるのが第38期の主眼**——どちらも「振らなかった手番」だが、
    // 2 は供給が止まっている（書き手が落ちた／まだ誰も刻んでいない）、
    // 14 は在庫が積み上がっている最中。合算すると周期が立ったのか供給が枯れたのかが決まらない。
    var acc = new Dictionary<(int Row, int Wave), double[]>();

    for (int r = 0; r < sevRows.Count; r++)
        for (int w = 0; w < sevStages.Count; w++)
        {
            var a = new double[15];
            for (int seed = 0; seed < SevSeeds; seed++)
            {
                BattleResult res = BattleEngine.Run(sevRows[r].F, sevStages[w].Enemy, seed, verbose: true);
                a[13]++;

                int turn = 0, lastIdleTurn = -1, lastWaitTurn = -1;
                string intended = "", swungAt = "";
                bool swinging = false, fired = false, cutPending = false;
                int swungAtk = 0;

                void CloseSwing()
                {
                    if (!swinging) return;
                    if (!fired) a[5]++;
                    if (intended.Length > 0 && swungAt.Length > 0 && intended != swungAt)
                    {
                        a[4]++;
                        a[8] += swungAtk;
                    }
                    swinging = false; fired = false; intended = ""; swungAt = ""; swungAtk = 0;
                }

                foreach (LogLine l in res.Log)
                {
                    string t = l.Text;
                    if (l.Kind == LogKind.Turn) { turn++; CloseSwing(); continue; }

                    if (t.Contains($"{nata} は閉じた肌に刃を下ろさない"))
                    {
                        // **1ターンに1回だけ数える。** CanAct は Trait.SurrenderedTurn からも
                        // 呼ばれるので（据えの判定。のろまの「まだ動き出せない」と同じ既存の作法）、
                        // 同じ手番に2行出ることがある。数えたいのは失った手番の数。
                        if (turn != lastIdleTurn) { a[2]++; lastIdleTurn = turn; }
                        continue;
                    }

                    // 待ち（浅い）。**放棄と分けて数える**（上の但し書き）。
                    // 1ターンに1回だけ数えるのは放棄と同じ理由（CanAct が2回呼ばれうる）。
                    if (t.Contains($"{nata} は傷がまだ浅いと刃を上げない"))
                    {
                        if (turn != lastWaitTurn) { a[14]++; lastWaitTurn = turn; }
                        continue;
                    }

                    if (t.Contains($"{nata} は ") && t.Contains(" の傷口を見定めた"))
                    {
                        int p1 = t.IndexOf($"{nata} は ", StringComparison.Ordinal) + nata.Length + 3;
                        int p2 = t.IndexOf(" の傷口を見定めた", StringComparison.Ordinal);
                        intended = p2 > p1 ? t[p1..p2] : "";
                        continue;
                    }

                    if (t.Contains($"{nata} → "))
                    {
                        CloseSwing();
                        swinging = true;
                        a[3]++;
                        int p1 = t.IndexOf($"{nata} → ", StringComparison.Ordinal) + nata.Length + 3;
                        int p2 = t.IndexOf(" (攻撃", p1, StringComparison.Ordinal);
                        swungAt = p2 > p1 ? t[p1..p2] : "";
                        int q = t.IndexOf("(攻撃 ", StringComparison.Ordinal);
                        if (q >= 0)
                        {
                            q += 4;
                            int q2 = t.IndexOfAny(new[] { ')', ' ' }, q);
                            if (q2 > q) int.TryParse(t[q..q2], out swungAtk);
                        }
                        continue;
                    }

                    if (t.Contains($"{nata} が ") && t.Contains("の傷をまとめて断つ"))
                    {
                        int wd = WoundOf(t);
                        a[0]++; a[1] += wd; fired = true;
                        if (wd >= SeverTrait.PerWound + 1) a[7]++;
                        cutPending = true;
                        continue;
                    }

                    // 軛の行が断ちの直後に出たら、その発火が切られている
                    if (cutPending && t.Contains("軛が") && t.Contains("に切った")) { a[6]++; cutPending = false; continue; }
                    cutPending = false;

                    if (t.Contains($"{nomi} が ") && t.Contains("の古い傷をなぞる"))
                    { a[9]++; a[10] += CarveTrait.PerWound * WoundOf(t); continue; }

                    if (t.Contains($"{egu} が ") && t.Contains("の傷をこじ開ける"))
                    { a[11]++; a[12] += GougeTrait.PerWound * WoundOf(t); continue; }
                }
                CloseSwing();
            }
            acc[(r, w)] = a;
        }

    Console.WriteLine("# 断ち（第37期・診断。docs/ には置かない）");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{SevSeeds - 1} × 全波、verbose のログ行を数えた。数字は**1戦あたり**。");
    Console.WriteLine();
    Console.WriteLine($"閾値は `SeverTrait.Threshold` = {SeverTrait.Threshold}（第38期）。");
    Console.WriteLine();
    Console.WriteLine("- `振` ナタが攻撃を振った回数");
    Console.WriteLine("- `放棄` 傷持ちが1体も狙えず手番を捨てた回数（供給が止まっている）");
    Console.WriteLine($"- `待ち` 傷はあるが最深が {SeverTrait.Threshold} に届かず捨てた回数（在庫を積んでいる最中）");
    Console.WriteLine("- `断ち` 上乗せが発火した回数 / `傷/断ち` 1発でまとめて断った傷の平均数");
    Console.WriteLine("- `逸れ` 見定めた相手と実際に殴った相手が違った振り（介入の鎖が上書きした）");
    Console.WriteLine("- `空振` 振ったが断てなかった回数（逸れの多くはここに落ちる）");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 波 | 振 | 放棄 | 待ち | 断ち | 傷/断ち | 上乗せ | 逸れ | 空振 | 軛切 | w≥6 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int r = 0; r < sevRows.Count; r++)
        for (int w = 0; w < sevStages.Count; w++)
        {
            double[] a = acc[(r, w)];
            double n = a[13];
            string per = a[0] > 0 ? $"{a[1] / a[0]:0.00}" : "—";
            Console.WriteLine($"| {sevRows[r].Name} | 第{w + 1}波 | {a[3] / n:0.00} | {a[2] / n:0.00} | "
                + $"{a[14] / n:0.00} | "
                + $"{a[0] / n:0.00} | {per} | {SeverTrait.PerWound * a[1] / n:0.0} | "
                + $"{a[4] / n:0.00} | {a[5] / n:0.00} | {a[6] / n:0.00} | {a[7] / n:0.00} |");
        }

    Console.WriteLine();
    Console.WriteLine("## 資源の取り合い（ノミの「なぞり」・エグの「こじ開け」）");
    Console.WriteLine();
    Console.WriteLine("同じ傷を誰が読んだか。**ナタが断つと傷は 0 に戻る**ので、");
    Console.WriteLine("同じ台にナタを入れた版のなぞり／こじ開けは削られるはず。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 波 | なぞり回 | なぞり量 | こじ開け回 | こじ開け量 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|");
    for (int r = 0; r < sevRows.Count; r++)
        for (int w = 0; w < sevStages.Count; w++)
        {
            double[] a = acc[(r, w)];
            double n = a[13];
            Console.WriteLine($"| {sevRows[r].Name} | 第{w + 1}波 | {a[9] / n:0.00} | {a[10] / n:0.0} | "
                + $"{a[11] / n:0.00} | {a[12] / n:0.0} |");
        }

    Console.WriteLine();
    Console.WriteLine("## 第五波（殉教者 p=75）の介入");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 振 | 逸れ | 逸れ率 | 逸れが殉教者へ渡した基礎打点/戦 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int r = 0; r < sevRows.Count; r++)
    {
        double[] a = acc[(r, sevStages.Count - 1)];
        double n = a[13];
        string rate = a[3] > 0 ? $"{100.0 * a[4] / a[3]:0.0}%" : "—";
        Console.WriteLine($"| {sevRows[r].Name} | {a[3] / n:0.00} | {a[4] / n:0.00} | {rate} | {a[8] / n:0.0} |");
    }
    return;
}

// suture モード（第39期・使い捨ての診断）: 縫い（ハリ）の繕いと塞ぎを数え、
// **第三波の値が渇きのせいであることを同数値対照で証明する。**
//
// **ここもログの文字列を数えている**（`gullet log` / `yoke log` / `hush` / `sever` と同じ理由）。
// 繕いは `ctx.Heal` を1回通るだけなので回復の総量に溶けるし、**渇きに封じられた繕いは
// 盤面の値に痕跡を1つも残さない**（`Heal` が入口で return するので tally も Events も動かない）。
// 「その行が出たか／何回出たか」を数える以外に発火を捕まえる方法が無い。
//
// **`docs/` には置かない。** 標準出力で読むだけ。
//
//     dotnet run --project BattleSim -c Release 0 suture [絞り込み]
if (focusId == "suture")
{
    var sutBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> sutStages = EnemyCatalog.Stages;
    const int SutSeeds = 50;      // 機構の指標（verbose のログ行）を数える本数。sever と揃える
    const int SutRateSeeds = 200; // 勝率を測り直す本数。compare / spread と揃える（セルを突き合わせる）

    string hari = UnitCatalog.Hari.Name;
    string nomi = UnitCatalog.Nomi.Name;
    string droughter = EnemyCatalog.Droughter.Name;

    // 第39期に compare から落とした対照（`裂き×縫い (キリ×ハリ)`）を**診断のローカルに組む**
    // （`gradient` / `aim` / `route` / `sever` と同じ扱い）。**`CompareBuilds()` には戻さない**
    // ——戻すと `docs/balance.md` の行が増えて「既存43行 ±0.0」の分母が動く。
    // 配置は confirm で据え置きになった仮置き（reseat 1位は -2.2pt で不採用）。
    (string Name, Formation F) sutControl = ("裂き×縫い (キリ×ハリ)", Formation.Build(
        front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga,
        back1: UnitCatalog.Vel, back3: UnitCatalog.Hari));

    string sutFilter = args.Length > 2 && args[2].Length > 0 ? args[2] : "縫い,刻み×抉り";
    var sutRows = sutBuilds
        .Select(x => (Name: x.Name, F: x.F))
        .Append(sutControl)
        .Where(x => sutFilter.Split(',').Any(k => x.Name.Contains(k.Trim())))
        .ToList();

    // 「（傷 w → +x、傷 y へ）」の w を取り出す。書式の頭は裂き・抉り・刻み・断ちと共通。
    static int SutWoundOf(string text)
    {
        int a = text.IndexOf("（傷 ", StringComparison.Ordinal);
        if (a < 0) return 0;
        a += 3;
        int b = text.IndexOf(' ', a);
        return b > a && int.TryParse(text[a..b], out int w) ? w : 0;
    }

    //  0 繕いの発火 / 1 読んだ傷の総和 / 2 ハリの振り / 3 見定め / 4 逸れた振り
    //  5 空振り（振ったが繕えなかった）/ 6 封じられた発火（渇きの保持者が生きている間）
    //  7 封じられた繕い量 / 8 解禁後の発火 / 9 解禁後の繕い量 / 10 祭司を割った戦
    // 11 ノミのなぞり発火 / 12 なぞり上乗せ総量 / 13 戦数 / 14 塞ぎ（傷を1つ減らした回数）
    //
    // **6 と 8 を分けるのが第39期の主眼**——どちらも同じ発火だが、6 は `ctx.Heal` が
    // 入口で return して1点も届いていない。合算すると「繕いが細い」のか「封じられた」のかが決まらない。
    // **塞ぎ（14）は 6 でも走る**ので必ず発火数と一致する（一致しなければ実装が親切をしている）。
    var sacc = new Dictionary<(int Row, int Wave), double[]>();

    for (int r = 0; r < sutRows.Count; r++)
        for (int w = 0; w < sutStages.Count; w++)
        {
            var a = new double[15];
            for (int seed = 0; seed < SutSeeds; seed++)
            {
                BattleResult res = BattleEngine.Run(sutRows[r].F, sutStages[w].Enemy, seed, verbose: true);
                a[13]++;

                // 渇きの保持者が生きているか。**波に祭司がいなければ最初から解禁**。
                bool droughtAlive = sutStages[w].Enemy.Occupied()
                    .Any(x => x.Def.Id == EnemyCatalog.Droughter.Id);
                bool sawPriestDeath = false;

                string intended = "", swungAt = "";
                bool swinging = false, fired = false;

                void CloseSwing()
                {
                    if (!swinging) return;
                    if (!fired) a[5]++;
                    if (intended.Length > 0 && swungAt.Length > 0 && intended != swungAt) a[4]++;
                    swinging = false; fired = false; intended = ""; swungAt = "";
                }

                foreach (LogLine l in res.Log)
                {
                    string t = l.Text;
                    if (l.Kind == LogKind.Turn) { CloseSwing(); continue; }

                    // 祭司の死。**名前で引ける**（渇きの祭司は波に1体きりで、他の def と名前が衝突しない）。
                    if (l.Kind == LogKind.Death && t.Contains($"{droughter} は倒れた"))
                    { droughtAlive = false; sawPriestDeath = true; continue; }

                    if (t.Contains($"{hari} は ") && t.Contains(" の傷口を見定めた"))
                    {
                        a[3]++;
                        int p1 = t.IndexOf($"{hari} は ", StringComparison.Ordinal) + hari.Length + 3;
                        int p2 = t.IndexOf(" の傷口を見定めた", StringComparison.Ordinal);
                        intended = p2 > p1 ? t[p1..p2] : "";
                        continue;
                    }

                    if (t.Contains($"{hari} → "))
                    {
                        CloseSwing();
                        swinging = true;
                        a[2]++;
                        int p1 = t.IndexOf($"{hari} → ", StringComparison.Ordinal) + hari.Length + 3;
                        int p2 = t.IndexOf(" (攻撃", p1, StringComparison.Ordinal);
                        swungAt = p2 > p1 ? t[p1..p2] : "";
                        continue;
                    }

                    if (t.Contains($"{hari} が ") && t.Contains("の傷口から糸を引き"))
                    {
                        int wd = SutWoundOf(t);
                        a[0]++; a[1] += wd; a[14]++; fired = true;
                        if (droughtAlive) { a[6]++; a[7] += SutureTrait.PerWound * wd; }
                        else { a[8]++; a[9] += SutureTrait.PerWound * wd; }
                        continue;
                    }

                    if (t.Contains($"{nomi} が ") && t.Contains("の古い傷をなぞる"))
                    { a[11]++; a[12] += CarveTrait.PerWound * SutWoundOf(t); continue; }
                }
                CloseSwing();
                if (sawPriestDeath) a[10]++;
            }
            sacc[(r, w)] = a;
        }

    Console.WriteLine("# 縫い（第39期・診断。docs/ には置かない）");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{SutSeeds - 1} × 全波、verbose のログ行を数えた。数字は**1戦あたり**。");
    Console.WriteLine($"繕い量は `SutureTrait.PerWound`({SutureTrait.PerWound}) × 傷の**名目値**");
    Console.WriteLine("（HP上限で切られた分を含む。封じられた分は1点も届いていない）。");
    Console.WriteLine();
    Console.WriteLine("- `振` ハリが攻撃を振った回数 / `見定` 傷選好が働いた回数（傷持ちが狙えた手番）");
    Console.WriteLine("- `繕い` 発火した回数 / `傷/繕い` 1回で読んだ傷の平均（**塞ぎは常に1つ**）");
    Console.WriteLine("- `塞ぎ` 傷を1つ減らした回数。**発火数と必ず一致する**（渇き下でも走るのが仕様）");
    Console.WriteLine("- `逸れ` 見定めた相手と実際に殴った相手が違った振り（介入の鎖が上書きした）");
    Console.WriteLine("- `空振` 振ったが繕えなかった回数（傷が無い相手を殴った／患者がいない）");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 波 | 振 | 見定 | 繕い | 傷/繕い | 塞ぎ | 繕い量(名目) | 逸れ | 空振 | なぞり回 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int r = 0; r < sutRows.Count; r++)
        for (int w = 0; w < sutStages.Count; w++)
        {
            double[] a = sacc[(r, w)];
            double n = a[13];
            string per = a[0] > 0 ? $"{a[1] / a[0]:0.00}" : "—";
            Console.WriteLine($"| {sutRows[r].Name} | 第{w + 1}波 | {a[2] / n:0.00} | {a[3] / n:0.00} | "
                + $"{a[0] / n:0.00} | {per} | {a[14] / n:0.00} | {SutureTrait.PerWound * a[1] / n:0.0} | "
                + $"{a[4] / n:0.00} | {a[5] / n:0.00} | {a[11] / n:0.00} |");
        }

    Console.WriteLine();
    Console.WriteLine("## 第三波（渇き）の封じ");
    Console.WriteLine();
    Console.WriteLine("`封じ` は渇きの祭司が生きている間に出た繕い（**1点も届いていない**）。");
    Console.WriteLine("`解禁` は祭司を割った後の繕い。**塞ぎは封じの側でも走っている**ので、");
    Console.WriteLine("この波はハリの編成に**二重に**課金する（回復の封じ ＋ 傷という資源の目減り）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 繕い/戦 | 封じ/戦 | 封じ量 | 解禁/戦 | 解禁量 | 封じ率 | 祭司を割った戦 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    for (int r = 0; r < sutRows.Count; r++)
    {
        double[] a = sacc[(r, 2)];
        double n = a[13];
        string rate = a[0] > 0 ? $"{100.0 * a[6] / a[0]:0.0}%" : "—";
        Console.WriteLine($"| {sutRows[r].Name} | {a[0] / n:0.00} | {a[6] / n:0.00} | {a[7] / n:0.0} | "
            + $"{a[8] / n:0.00} | {a[9] / n:0.0} | {rate} | {100.0 * a[10] / n:0.0}% |");
    }

    Console.WriteLine();
    Console.WriteLine("## 第五波（殉教者 p=75）の介入");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 振 | 見定 | 逸れ | 逸れ率（見定めた振りのうち） |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int r = 0; r < sutRows.Count; r++)
    {
        double[] a = sacc[(r, sutStages.Count - 1)];
        double n = a[13];
        string rate = a[3] > 0 ? $"{100.0 * a[4] / a[3]:0.0}%" : "—";
        Console.WriteLine($"| {sutRows[r].Name} | {a[2] / n:0.00} | {a[3] / n:0.00} | {a[4] / n:0.00} | {rate} |");
    }

    // ---- 渇きの帰属（同数値対照）------------------------------------------------------
    //
    // **第三波の値が渇きのせいであることを、対照で証明する。** 渇きの祭司（Droughter）と
    // 巡礼騎士（Knight）は HP・攻・速さ・型が同一で、違いは盤面ルールを1つ持つかだけ
    // ——差し替えで動いた分は**渇きの税額そのもの**になる（第34期の交絡＝HP を動かして
    // しまう罠を、同数値の対照で構造的に避ける）。
    //
    // **対照行（回復を持たない `刻み×抉り`）が ±0.0 であることが診断の検算。**
    // ここが動いたら、差し替え自体が盤面を変えている（＝帰属に使えない測定）。
    Console.WriteLine();
    Console.WriteLine("## 渇きの帰属（同数値対照・第三波）");
    Console.WriteLine();
    Console.WriteLine("第三波の中央を **渇きの祭司 ↔ 巡礼騎士** に差し替えた（HP・攻・速さ・型は同一）。");
    Console.WriteLine($"seed 0..{SutRateSeeds - 1}（compare と同じ帯）。`渇きあり` は `docs/balance.md` の第三波と一致するはず。");
    Console.WriteLine();
    Console.WriteLine("**前提の訂正（第39期）**: 指示書は対照に `刻み×抉り` を指定していたが、");
    Console.WriteLine("**この行は回復を持っている**——ゴルムの吸い（`DrainTrait`）と巨躯の還し（`ColossusTrait`）が");
    Console.WriteLine("どちらも `ctx.Heal` を通る（README「駒の説明文から数えると必ず抜ける」の再演）。");
    Console.WriteLine("傷軸の5行はすべて土台にゴルムを持つので、**傷軸の中に回復ゼロの行は1つも無い。**");
    Console.WriteLine("そこで検算用に **`対照 (回復ゼロ)`** を診断のローカルに組んだ——`刻み×抉り` の");
    Console.WriteLine("ゴルムをガルド（`Guardian`+`Stoic`。回復経路なし）に差し替えただけの版で、");
    Console.WriteLine("**この行が ±0.0 であることが「差し替え自体は盤面を変えていない」の証明。**");
    Console.WriteLine("`刻み×抉り` の側は**土台（ゴルム）が払っている税額**として読む。");
    Console.WriteLine();
    Console.WriteLine("`回復回` は渇きなし版で実際に通った `Heal` の回数（1戦あたり）。**0 なら渇きは無風のはず。**");
    Console.WriteLine();

    Formation stage3 = sutStages[2].Enemy;
    var stage3NoDrought = new Formation();
    for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
    {
        UnitDef? d = stage3[i];
        stage3NoDrought[i] = d is null ? null
            : d.Id == EnemyCatalog.Droughter.Id ? EnemyCatalog.Knight : d;
    }

    // 回復経路ゼロの検算行。**ゴルム（吸い＋還し）だけを抜いてある**ので、
    // 渇きが触れる窓口が1つも無い＝差し替えは1試行も動かせない。
    var sutAttrib = sutRows.Append((Name: "対照 (回復ゼロ)", F: Formation.Build(
        front1: UnitCatalog.Egu, front3: UnitCatalog.Gald, center: UnitCatalog.Nomi,
        back1: UnitCatalog.Dolga, back3: UnitCatalog.Vel))).ToList();

    Console.WriteLine("| 編成 | 渇きあり | 渇きなし（巡礼騎士） | 税額 | 回復回（渇きなし） |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (var row in sutAttrib)
    {
        double with = 0, without = 0, heals = 0;
        for (int seed = 0; seed < SutRateSeeds; seed++)
        {
            if (BattleEngine.Run(row.F, stage3, seed, verbose: false).PlayerWon) with++;
            BattleResult free = BattleEngine.Run(row.F, stage3NoDrought, seed, verbose: true);
            if (free.PlayerWon) without++;
            heals += free.Events.Count(e => e.Kind == BattleEventKind.Heal);
        }
        double a = with * 100.0 / SutRateSeeds, b = without * 100.0 / SutRateSeeds;
        Console.WriteLine($"| {row.Name} | {a:0.0}% | {b:0.0}% | {b - a:+0.0;-0.0}pt | {heals / SutRateSeeds:0.00} |");
    }
    return;
}

if (focusId == "expose")
{
    var exBuilds = CompareBuilds();
    const int ExSeeds = 200;   // compare / spread / yoke / hush と同じ。balance.md と突き合わせる

    string exFilter = args.Length > 2 ? args[2] : "";
    var exTargets = exBuilds
        .Where(b => exFilter.Length == 0 || exFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 第五波の中央だけを差し替えた版を診断のローカルで組む（gradient / aim / yoke / hush と同じ扱い）。
    // 残り4枠は EnemyCatalog のまま——**動く変数は中央の1枚と規則の有無だけ。**
    Formation Wave5With(UnitDef center) => Formation.Build(
        front1: EnemyCatalog.Martyr, front3: EnemyCatalog.Hero2, center: center,
        back1: EnemyCatalog.Seer, back3: EnemyCatalog.Lancer);

    Formation wave5Accuser = Wave5With(EnemyCatalog.Accuser);   // 告発人（曝き持ち）
    Formation wave5Knight = Wave5With(EnemyCatalog.Knight2);    // 同数値の対照（巡礼騎士）

    // 上限は「無制限」も測る。ExposeRule は int なので実質の無限として大きい値を置く
    // （1戦 30 ターン上限・保持者1体なので 999 は到達しない）。
    const int Unlimited = 999;

    // --- 1戦から計数を取り出す ------------------------------------------------------------
    //
    // **数え方は3系統に分けてある。**
    //   (a) ログの文字列 …… 出された駒の名前・軋み・移り木。「その行が出たか」そのものを見る
    //       （gullet log / yoke log / hush / sever と同じ理由）
    //   (b) Move イベント …… 後退・後衛特化・戻り。**行（Row）はログの文字列に載っていない**ので
    //       こちらは席の履歴から組む。HasFallenBack の判定式は SwapSlots のものと同じ
    //       （DepthOf(新) > DepthOf(旧)）で、エンジンは1行も触っていない
    //   (c) BattleResult の counter …… 曝きと空振り。**空振りはログを1行も出さない**
    //       （出すと「何も起きていない」がログの主役になる）ので文字列にも盤面にも痕跡が残らない
    //
    // 味方の InstanceId は「スロット昇順の並び」で 0 から振られる（Materialize → ctx.Add）。
    // 敵は味方の後ろに続くので、味方側は 0..(体数-1) で引ける。召喚駒はそれより後ろの番号になる。
    (double Fire, double Miss, double Back, double Sniper, double Return,
     double Displace, double Drift, double Turns, Dictionary<string, int> Pulled) MeasureExpose(
        Formation f, Formation enemy, ExposeRule rule)
    {
        var pulled = new Dictionary<string, int>();
        double fire = 0, miss = 0, back = 0, sniper = 0, ret = 0, disp = 0, drift = 0, turns = 0;

        // 後衛特化（セロ）の席番号。編成に居なければ -1
        int sniperId = -1;
        for (int i = 0, k = 0; i < FormationRules.PlayableSlotCount; i++)
            if (f[i] is { } d) { if (d.Traits.Contains(TraitId.Sniper)) sniperId = k; k++; }

        for (int seed = 0; seed < ExSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: true,
                                    null, null, null, null, rule);
            fire += r.ExposeCount;
            miss += r.ExposeMissed;
            turns += r.Turns;

            foreach (LogLine l in r.Log)
            {
                if (l.Text.Contains("の前へ引きずり出した"))
                {
                    // 「{保持者} が {駒} を {席} の前へ引きずり出した」
                    int a = l.Text.IndexOf(" が ", StringComparison.Ordinal);
                    int b = l.Text.IndexOf(" を ", StringComparison.Ordinal);
                    if (a >= 0 && b > a)
                    {
                        string who = l.Text.Substring(a + 3, b - a - 3);
                        pulled[who] = pulled.TryGetValue(who, out int c) ? c + 1 : 1;
                    }
                }
                if (l.Text.Contains("はよろけた勢いのまま振り抜く")) disp++;
                if (l.Text.Contains("を拾い上げた")) drift++;
            }

            // --- 席の履歴（味方側だけ）。初期配置は Formation から直に引ける -----------------
            var slot = new Dictionary<int, int>();
            int id = 0;
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                if (f[i] is not null) slot[id++] = i;
            int allyCount = id;

            var fell = new HashSet<int>();      // HasFallenBack が立った駒
            var forward = new HashSet<int>();   // 後列から前列へ出された駒（戻りの母数）

            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind == BattleEventKind.TurnStart)
                {
                    // ターン頭に「後退済み かつ 後列」を満たしていたら1つ数える
                    if (sniperId >= 0 && fell.Contains(sniperId)
                        && slot.TryGetValue(sniperId, out int ss)
                        && FormationRules.RowOf(ss) == Row.Back) sniper++;
                    continue;
                }
                if (e.Kind != BattleEventKind.Move) continue;
                if (e.TargetId is not { } tid || tid >= allyCount) continue;   // 召喚駒・敵は数えない

                Row from = slot.TryGetValue(tid, out int old) ? FormationRules.RowOf(old) : Row.Front;
                Row to = FormationRules.RowOf(e.Slot);
                slot[tid] = e.Slot;

                // SwapSlots と同じ式。**より深い列へ動いたときだけ**印が立つ
                if (FormationRules.DepthOf(to) > FormationRules.DepthOf(from) && fell.Add(tid)) back++;

                // 後列 → 前列（＝引きずり出された側）。
                // **喧噪（バサ）でも起きうる**ので、帰属は対照との差で取ること
                if (from == Row.Back && to == Row.Front) forward.Add(tid);
                else if (to == Row.Back && forward.Remove(tid)) ret++;   // 味方の手で後列へ戻った
            }
        }

        return (fire / ExSeeds, miss / ExSeeds, back / ExSeeds, sniper / ExSeeds,
                ret / ExSeeds, disp / ExSeeds, drift / ExSeeds, turns / ExSeeds, pulled);
    }

    Console.WriteLine("# 引きずり出し（曝き / expose）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 expose [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。第五波 × seed 0..{ExSeeds - 1}。");
    Console.WriteLine("数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`CompareBuilds()` / `Stages` / `Columns` は触っていない。第五波の中央だけを");
    Console.WriteLine("診断のローカルで差し替えている（`gradient` / `aim` / `yoke` / `hush` と同じ扱い）。");
    Console.WriteLine();

    // ---- 基準1・2: 対照が成立しているか ---------------------------------------------------
    Console.WriteLine("## 0. 対照の成立（受け入れ基準 1・2）");
    Console.WriteLine();
    Console.WriteLine("**告発人（曝き持ち・規則 0）** と **巡礼騎士（規則 有効）** の第五波が、");
    Console.WriteLine("全行で一致しなければならない。一致すれば「差分は規則だけに閉じている」");
    Console.WriteLine("＝ 同数値の対照が成立している。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 告発人 規則0 | 巡礼騎士 規則∞ | 一致 |");
    Console.WriteLine("|---|--:|--:|:--:|");
    int exMismatch = 0;
    foreach (var (name, f) in exTargets)
    {
        int w0 = 0, w1 = 0;
        for (int seed = 0; seed < ExSeeds; seed++)
        {
            if (BattleEngine.Run(f, wave5Accuser, seed, false, null, null, null, null,
                                 new ExposeRule(0)).PlayerWon) w0++;
            if (BattleEngine.Run(f, wave5Knight, seed, false, null, null, null, null,
                                 new ExposeRule(Unlimited)).PlayerWon) w1++;
        }
        bool ok = w0 == w1;
        if (!ok) exMismatch++;
        Console.WriteLine($"| {name} | {w0 * 100.0 / ExSeeds:0.0}% | {w1 * 100.0 / ExSeeds:0.0}% | {(ok ? "○" : "**×**")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**食い違い {exMismatch} 件 / {exTargets.Length} 行**"
        + (exMismatch == 0 ? "。対照は成立している。" : "。**対照が壊れている。**"));
    Console.WriteLine();

    // ---- 計数（対照 vs 有効） --------------------------------------------------------------
    Console.WriteLine("## 1. 計数（陽性対照 `ExposeRule(0)` と 有効時）");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 曝き | 引きずり出しの発火回数/戦（`BattleResult.ExposeCount`） |");
    Console.WriteLine("| 空振り | 後列または前列が 0 体で何もしなかった回数/戦（**ログを1行も出さない**ので結果から取る） |");
    Console.WriteLine("| 戻り | 引き出された駒（後列→前列に動いた味方）が後列へ戻った回数/戦 |");
    Console.WriteLine("| 後退 | `HasFallenBack` が新たに立った回数/戦（Move イベントから。式は `SwapSlots` と同じ） |");
    Console.WriteLine("| 軋み | ヨミの `OnMoved` 起点の割り込み回数/戦（「よろけた勢いのまま振り抜く」） |");
    Console.WriteLine("| 移り木 | シオの `OnAllyMoved` 起点の回復回数/戦（「拾い上げた」） |");
    Console.WriteLine("| 後衛特化 | セロが「後退済み かつ 後列」を満たしていたターン数/戦 |");
    Console.WriteLine("| 決着T | 決着までのターン数/戦 |");
    Console.WriteLine();
    Console.WriteLine("**`後衛特化` はターン数なので戦闘の長さで割ること。** 勝ち方が速くなれば");
    Console.WriteLine("窓が開いたままでも数が減る（第17期 (B)「育ち」が決着で窓が閉じるのと同じ穴）。");
    Console.WriteLine("`決着T` を並べてあるのはそのため——読むのは `後衛特化 ÷ 決着T`。");
    Console.WriteLine();
    Console.WriteLine("**`戻り` は喧噪（バサ）でも立つ**（後列→前列の移動を起こすもう1つの経路）ので、");
    Console.WriteLine("帰属は必ず対照との差で取ること。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 曝き | 空振り | 戻り | 後退(対照→有効) | 軋み(対照→有効) | 移り木(対照→有効) | 後衛特化(対照→有効) | 後衛特化/T | 決着T(対照→有効) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

    var pulledAll = new Dictionary<string, int>();
    double sumFire = 0, sumMiss = 0, sumRet = 0, ctrlFire = 0;
    double sumBack0 = 0, sumBack1 = 0, sumSnp0 = 0, sumSnp1 = 0;
    double sumDsp0 = 0, sumDsp1 = 0, sumDrf0 = 0, sumDrf1 = 0, sumT0 = 0, sumT1 = 0;

    foreach (var (name, f) in exTargets)
    {
        var off = MeasureExpose(f, wave5Accuser, new ExposeRule(0));
        var on = MeasureExpose(f, wave5Accuser, new ExposeRule(Unlimited));

        foreach (var kv in on.Pulled)
            pulledAll[kv.Key] = pulledAll.TryGetValue(kv.Key, out int c) ? c + kv.Value : kv.Value;

        ctrlFire += off.Fire;
        sumFire += on.Fire; sumMiss += on.Miss; sumRet += on.Return - off.Return;
        sumBack0 += off.Back; sumBack1 += on.Back;
        sumSnp0 += off.Sniper; sumSnp1 += on.Sniper;
        sumDsp0 += off.Displace; sumDsp1 += on.Displace;
        sumDrf0 += off.Drift; sumDrf1 += on.Drift;
        sumT0 += off.Turns; sumT1 += on.Turns;

        Console.WriteLine($"| {name} | {on.Fire:0.00} | {on.Miss:0.00} | {on.Return - off.Return:+0.00;-0.00;0.00} "
            + $"| {off.Back:0.00} → {on.Back:0.00} | {off.Displace:0.00} → {on.Displace:0.00} "
            + $"| {off.Drift:0.00} → {on.Drift:0.00} | {off.Sniper:0.00} → {on.Sniper:0.00} "
            + $"| {off.Sniper / Math.Max(1, off.Turns):0.00} → {on.Sniper / Math.Max(1, on.Turns):0.00} "
            + $"| {off.Turns:0.0} → {on.Turns:0.0} |");
    }

    int n = Math.Max(1, exTargets.Length);
    Console.WriteLine($"| **平均** | **{sumFire / n:0.00}** | **{sumMiss / n:0.00}** | **{sumRet / n:+0.00;-0.00;0.00}** "
        + $"| **{sumBack0 / n:0.00} → {sumBack1 / n:0.00}** | **{sumDsp0 / n:0.00} → {sumDsp1 / n:0.00}** "
        + $"| **{sumDrf0 / n:0.00} → {sumDrf1 / n:0.00}** | **{sumSnp0 / n:0.00} → {sumSnp1 / n:0.00}** "
        + $"| **{sumSnp0 / Math.Max(1, sumT0):0.00} → {sumSnp1 / Math.Max(1, sumT1):0.00}** "
        + $"| **{sumT0 / n:0.0} → {sumT1 / n:0.0}** |");
    Console.WriteLine();
    Console.WriteLine($"**陽性対照**: `ExposeRule(0)` での 曝き = **{ctrlFire / n:0.00} 回/戦**"
        + (ctrlFire == 0 ? "（0.00 なので有効時の数字を読んでよい）" : "（**0 でない。計数が壊れている**）"));
    Console.WriteLine();

    Console.WriteLine("### 出された駒（上位）");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 回数 |");
    Console.WriteLine("|---|--:|");
    foreach (var kv in pulledAll.OrderByDescending(k => k.Value).Take(10))
        Console.WriteLine($"| {kv.Key} | {kv.Value} |");
    Console.WriteLine();

    // ---- 掃引 -----------------------------------------------------------------------------
    Console.WriteLine("## 2. 掃引（`MaxPerBattle`）");
    Console.WriteLine();
    Console.WriteLine("**各点に対照は要らない**——このノブは盤面の総HPを1も動かさない（席を入れ替えるだけで");
    Console.WriteLine("HP も攻撃力も1点も変わらない）。`ExposeRule(0)` の1本だけを全点の基準に置く。");
    Console.WriteLine();

    int[] caps = { 0, 1, 2, 3, Unlimited };
    var capWins = new Dictionary<int, int[]>();
    foreach (int cap in caps) capWins[cap] = new int[exTargets.Length];

    for (int i = 0; i < exTargets.Length; i++)
        foreach (int cap in caps)
            for (int seed = 0; seed < ExSeeds; seed++)
                if (BattleEngine.Run(exTargets[i].F, wave5Accuser, seed, false, null, null, null,
                                     null, new ExposeRule(cap)).PlayerWon) capWins[cap][i]++;

    Console.WriteLine("| 編成 | 0（対照） | 1 | 2 | 3 | 無制限 | Δ(無制限−対照) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    for (int i = 0; i < exTargets.Length; i++)
    {
        double b = capWins[0][i] * 100.0 / ExSeeds, u = capWins[Unlimited][i] * 100.0 / ExSeeds;
        Console.WriteLine($"| {exTargets[i].Name} "
            + string.Join("", caps.Select(c => $"| {capWins[c][i] * 100.0 / ExSeeds:0.0}% "))
            + $"| {u - b:+0.0;-0.0;0.0}pt |");
    }
    Console.WriteLine("| **平均** "
        + string.Join("", caps.Select(c => $"| **{capWins[c].Sum() * 100.0 / (ExSeeds * n):0.0}%** "))
        + $"| **{(capWins[Unlimited].Sum() - capWins[0].Sum()) * 100.0 / (ExSeeds * n):+0.0;-0.0;0.0}pt** |");
    Console.WriteLine();

    foreach (int cap in caps)
    {
        var v = Enumerable.Range(0, exTargets.Length).Select(i => capWins[cap][i] * 100.0 / ExSeeds).ToList();
        double mean = v.Average();
        double sd = Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / v.Count);
        Console.WriteLine($"- 上限 {(cap == Unlimited ? "無制限" : cap.ToString())}: "
            + $"平均 {mean:0.0} / SD {sd:0.0} / 100% {v.Count(x => x >= 100)} 行 / 0% {v.Count(x => x <= 0)} 行 "
            + $"/ 中間帯(0,100) {v.Count(x => x > 0 && x < 100)} 行");
    }
    Console.WriteLine();

    // ---- 符号反転 -------------------------------------------------------------------------
    Console.WriteLine("## 3. 符号反転（採否の判断材料 §5-5）");
    Console.WriteLine();
    Console.WriteLine("同じ規則で第五波の勝率が**上がる行と下がる行が両方存在するか**。");
    Console.WriteLine();
    var deltas = Enumerable.Range(0, exTargets.Length)
        .Select(i => (Name: exTargets[i].Name,
                      D: (capWins[Unlimited][i] - capWins[0][i]) * 100.0 / ExSeeds))
        .OrderByDescending(x => x.D).ToList();
    Console.WriteLine($"上がった行 **{deltas.Count(x => x.D > 0)}** / 下がった行 **{deltas.Count(x => x.D < 0)}** "
        + $"/ ±0.0 の行 **{deltas.Count(x => x.D == 0)}**");
    Console.WriteLine();
    Console.WriteLine("| 得をした行 | Δ |   | 損をした行 | Δ |");
    Console.WriteLine("|---|--:|---|---|--:|");
    for (int i = 0; i < Math.Min(6, deltas.Count / 2); i++)
    {
        var up = deltas[i];
        var dn = deltas[deltas.Count - 1 - i];
        Console.WriteLine($"| {up.Name} | {up.D:+0.0;-0.0;0.0}pt |   | {dn.Name} | {dn.D:+0.0;-0.0;0.0}pt |");
    }
    Console.WriteLine();
    return;
}

if (focusId == "shove")
{
    var shBuilds = CompareBuilds();
    const int ShSeeds = 200;   // compare / spread / expose と同じ。balance.md と突き合わせる

    string shFilter = args.Length > 2 ? args[2] : "突き返し";
    var shTargets = shBuilds
        .Where(b => shFilter.Length == 0 || shFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    IReadOnlyList<EnemyCatalog.Stage> shStages = EnemyCatalog.Stages;

    // 供給元のラベル。**ログの文字列を数えている**（gullet log / yoke log / hush / sever と
    // 同じ理由）——移動の「出どころ」は盤面の値に痕跡を残さないし、SwapSlots は誰が呼んだかを
    // 記録していない。判定は engine ではなくここに閉じている。
    (string Mark, string Label)[] shSources =
    {
        ("が隊列をかき回した", "喧噪"),
        ("の前へ引きずり出した", "曝き"),
        ("は耐えきれず一列後ろへ下がった", "臆病"),
        ("を突き飛ばして後ろへ逃げた", "臆病"),
        ("を押しのけて前に出た", "棘守り"),
    };

    // **臆病だけはログが SwapSlots の後に出る**（CowardTrait は入れ替えてから
    // 「突き飛ばして後ろへ逃げた」を書く）。他の3つは呼ぶ前に書くので、
    // 素朴な「直前の原因行」では臆病が永久に 0 になる。
    //
    // 1ターンに突き返しが発火するのは**高々1回**（ShoveTrait の上限）なので、
    // ターンの中で「印より前の原因行」を優先し、無ければ「印より後の臆病行」を採れば
    // 曖昧さなく割れる。ターンの区切りは LogKind.Turn で取る。
    const string ShoveMarkA = "の突き返しが";      // 効果A が成立した行
    const string ShoveMarkB = "勢い余って";        // 効果B が当たった行

    // 1戦ぶんの計数。**ウツ（逆しま）の攻撃力は StatSnapshot から取る**
    // ——ログの文字列には現在値が載らないし、CurrentAttack は BattleResult に無い。
    (double Fire, double Cap, double Swap, double NoRow, double Stagger, double Block,
     double Turns, double AtkOpen, double AtkMax, double AtkLast, double Covered,
     Dictionary<string, double> Src, Dictionary<string, int> Pulled, Dictionary<string, int> Hit)
    MeasureShove(Formation f, Formation enemy, ShoveRule rule)
    {
        var src = new Dictionary<string, double>();
        var pulled = new Dictionary<string, int>();
        var hit = new Dictionary<string, int>();
        double fire = 0, cap = 0, swap = 0, norow = 0, stag = 0, block = 0, turns = 0, covered = 0;
        double open = 0, max = 0, last = 0, seen = 0;

        // 逆しま持ちの InstanceId。味方はスロット昇順に 0 から振られる（Materialize → ctx.Add）。
        int readerId = -1;
        for (int i = 0, k = 0; i < FormationRules.PlayableSlotCount; i++)
            if (f[i] is { } d) { if (d.Traits.Contains(TraitId.Perverse)) readerId = k; k++; }

        for (int seed = 0; seed < ShSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: true,
                                    null, null, null, null, null, rule);
            fire += r.ShoveFired; cap += r.ShoveCapped; swap += r.ShoveSwapped;
            norow += r.ShoveNoRow; stag += r.ShoveStaggered; block += r.ShoveBlocked;
            turns += r.Turns;

            // --- 供給元の帰属（ターンごとに1回だけ） --------------------------------------
            var lines = r.Log.ToList();
            int t0 = 0;
            while (t0 < lines.Count)
            {
                int t1 = t0 + 1;
                while (t1 < lines.Count && lines[t1].Kind != LogKind.Turn) t1++;

                int mark = -1;
                for (int i = t0; i < t1; i++)
                {
                    string tx = lines[i].Text;
                    if (tx.Contains(ShoveMarkA) || tx.Contains(ShoveMarkB)) { mark = i; break; }

                    // 効果A の入れ替え先の名前（引き出された敵）
                }
                if (mark >= 0)
                {
                    string? label = null;
                    for (int i = mark - 1; i >= t0 && label is null; i--)
                        foreach (var (mk, lb) in shSources)
                            if (lines[i].Text.Contains(mk)) { label = lb; break; }
                    if (label is null)
                        for (int i = mark + 1; i < t1 && label is null; i++)
                            if (lines[i].Text.Contains("は耐えきれず一列後ろへ下がった")
                                || lines[i].Text.Contains("を突き飛ばして後ろへ逃げた")) label = "臆病";
                    if (label is not null)
                    {
                        src[label] = src.TryGetValue(label, out double c) ? c + 1 : 1;
                        covered++;
                    }
                    else
                    {
                        src["不明"] = src.TryGetValue("不明", out double u) ? u + 1 : 1;
                    }
                }
                t0 = t1;
            }

            // --- 崩し・よろけの受け手（名前）------------------------------------------------
            foreach (LogLine l in r.Log)
            {
                if (l.Text.Contains(ShoveMarkA))
                {
                    // 「{保持者} の突き返しが {駒} を {席} の前へ突き崩した」
                    int a = l.Text.IndexOf(ShoveMarkA, StringComparison.Ordinal);
                    int b = l.Text.IndexOf(" を ", StringComparison.Ordinal);
                    if (a >= 0 && b > a)
                    {
                        string who = l.Text.Substring(a + ShoveMarkA.Length + 1, b - a - ShoveMarkA.Length - 1);
                        pulled[who] = pulled.TryGetValue(who, out int c) ? c + 1 : 1;
                    }
                }
                if (l.Text.Contains(ShoveMarkB))
                {
                    // 「勢い余って {A・B} の体勢まで崩れた（攻撃 -N）」
                    int a = l.Text.IndexOf(ShoveMarkB, StringComparison.Ordinal) + ShoveMarkB.Length;
                    int b = l.Text.IndexOf(" の体勢まで崩れた", StringComparison.Ordinal);
                    if (b > a)
                        foreach (string raw in l.Text.Substring(a, b - a).Split('・'))
                        {
                            string nm = raw.Trim();
                            hit[nm] = hit.TryGetValue(nm, out int c) ? c + 1 : 1;
                        }
                }
            }

            // --- 読み手の攻撃力（StatSnapshot。ターン頭の CurrentAttack）--------------------
            if (readerId >= 0)
            {
                var snaps = r.Events
                    .Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == readerId)
                    .Select(e => e.Amount).ToList();
                if (snaps.Count > 0)
                {
                    open += snaps[0]; max += snaps.Max(); last += snaps[^1]; seen++;
                }
            }
        }

        double n = ShSeeds, m = Math.Max(1, seen);
        foreach (string k in src.Keys.ToList()) src[k] /= n;
        return (fire / n, cap / n, swap / n, norow / n, stag / n, block / n, turns / n,
                open / m, max / m, last / m, covered / n, src, pulled, hit);
    }

    Console.WriteLine("# 突き返し（shove）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 shove [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{ShSeeds - 1}。数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`CompareBuilds()` / `Stages` / `Columns` は触っていない。");
    Console.WriteLine("既定の絞り込みは `突き返し`（引数で上書きできる）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 突き返し | 発火回数/戦（`BattleResult.ShoveFired`） |");
    Console.WriteLine("| 空振り | 1ターン1回の上限で弾かれた回数/戦（**ログを1行も出さない**ので結果から取る） |");
    Console.WriteLine("| 崩し | 効果A（敵陣の突き崩し）が成立した回数/戦 |");
    Console.WriteLine("| 列欠 | 敵の後列か前列が 0 体で効果Aだけが空振りした回数/戦 |");
    Console.WriteLine("| よろけ | 効果Bが当たった**延べ体数**/戦 |");
    Console.WriteLine("| 弾き | 効果Bが `Stoic`（ガルド）で弾かれた延べ体数/戦 |");
    Console.WriteLine("| ウツ攻 | 逆しま持ちの `CurrentAttack`（開戦時 / 最大 / 最終T）。`StatSnapshot` から |");
    Console.WriteLine("| 決着T | 決着までのターン数/戦 |");
    Console.WriteLine();
    Console.WriteLine("**`ウツ攻` の 開戦時 < 最大 がこの期の核心**（受け入れ基準2）。");
    Console.WriteLine("現行の 44 行ではウツの攻撃力は戦闘を通じて定数なので、動くこと自体が新しい挙動。");
    Console.WriteLine("`最終T` は**最後のターン頭**の値（`StatSnapshot` はターン頭にしか出ない）。");
    Console.WriteLine();

    foreach (var (bname, bf) in shTargets)
    {
        Console.WriteLine($"## {bname}");
        Console.WriteLine();

        // ---- 陽性対照 ----------------------------------------------------------------------
        Console.WriteLine("### 0. 陽性対照 `ShoveRule(0)`（受け入れ基準3の分母）");
        Console.WriteLine();
        Console.WriteLine("`Penalty = 0` は**効果Bだけを止める**（効果Aは走る）。");
        Console.WriteLine("よろけ・弾きが 0 で、ウツ攻が 開戦時 = 最大 = 最終T の定数になるはず。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 突き返し | 崩し | よろけ | 弾き | ウツ攻(開/最大/終) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int w = 0; w < shStages.Count; w++)
        {
            var z = MeasureShove(bf, shStages[w].Enemy, new ShoveRule(0));
            Console.WriteLine($"| 第{w + 1}波 | {z.Fire:0.00} | {z.Swap:0.00} | {z.Stagger:0.00} | {z.Block:0.00} "
                + $"| {z.AtkOpen:0.0} / {z.AtkMax:0.0} / {z.AtkLast:0.0} | {z.Turns:0.0} |");
        }
        Console.WriteLine();

        // ---- 本番 --------------------------------------------------------------------------
        Console.WriteLine($"### 1. 計数（`ShoveRule.Default` ＝ Penalty {ShoveRule.Default.Penalty}）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 突き返し | 空振り | 崩し | 列欠 | よろけ | 弾き | ウツ攻(開/最大/終) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");

        var srcAll = new Dictionary<string, double>();
        var pulledAll = new Dictionary<string, int>();
        var hitAll = new Dictionary<string, int>();
        double covAll = 0, fireAll = 0;

        for (int w = 0; w < shStages.Count; w++)
        {
            var z = MeasureShove(bf, shStages[w].Enemy, ShoveRule.Default);
            foreach (var kv in z.Src) srcAll[kv.Key] = srcAll.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in z.Pulled) pulledAll[kv.Key] = pulledAll.TryGetValue(kv.Key, out int c) ? c + kv.Value : kv.Value;
            foreach (var kv in z.Hit) hitAll[kv.Key] = hitAll.TryGetValue(kv.Key, out int c) ? c + kv.Value : kv.Value;
            covAll += z.Covered; fireAll += z.Fire;

            Console.WriteLine($"| 第{w + 1}波 | {z.Fire:0.00} | {z.Cap:0.00} | {z.Swap:0.00} | {z.NoRow:0.00} "
                + $"| {z.Stagger:0.00} | {z.Block:0.00} "
                + $"| {z.AtkOpen:0.0} / {z.AtkMax:0.0} / {z.AtkLast:0.0} | {z.Turns:0.0} |");
        }
        Console.WriteLine();

        Console.WriteLine("#### 供給元の内訳（発火回/戦・全波の合計）");
        Console.WriteLine();
        Console.WriteLine("**ログの文字列から割っている。** 臆病だけは入れ替えの**後**に書くので、");
        Console.WriteLine("ターンの中で「印より前の原因行」を先に見て、無ければ「印より後の臆病行」を採る。");
        Console.WriteLine("1ターンに発火は高々1回（上限）なので、この2段で曖昧さなく割れる。");
        Console.WriteLine();
        Console.WriteLine("| 供給元 | 発火回/戦（5波合計） |");
        Console.WriteLine("|---|--:|");
        foreach (var kv in srcAll.OrderByDescending(k => k.Value))
            Console.WriteLine($"| {kv.Key} | {kv.Value:0.00} |");
        Console.WriteLine($"| **帰属できた計** | **{covAll:0.00}** |");
        Console.WriteLine($"| **発火の計** | **{fireAll:0.00}** |");
        Console.WriteLine();
        Console.WriteLine($"**帰属率 {(fireAll > 0 ? covAll * 100.0 / fireAll : 0):0.0}%。**"
            + " 残りは効果A・効果Bのどちらも成立せず、ログに1行も出なかった発火"
            + "（敵の列が欠けていて、かつ隣接味方が全員 `Stoic` か不在）。");
        Console.WriteLine();

        Console.WriteLine("#### 引き出された敵（上位3体）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 回数 |");
        Console.WriteLine("|---|--:|");
        foreach (var kv in pulledAll.OrderByDescending(k => k.Value).Take(3))
            Console.WriteLine($"| {kv.Key} | {kv.Value} |");
        Console.WriteLine();

        Console.WriteLine("#### よろけの受け手（延べ）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 回数 |");
        Console.WriteLine("|---|--:|");
        foreach (var kv in hitAll.OrderByDescending(k => k.Value))
            Console.WriteLine($"| {kv.Key} | {kv.Value} |");
        Console.WriteLine();

        // ---- 掃引 --------------------------------------------------------------------------
        Console.WriteLine("### 2. 掃引（`Penalty`）");
        Console.WriteLine();
        Console.WriteLine("**各点に対照は要らない**——このノブは敵側の盤面を1も動かさない。");
        Console.WriteLine("`ShoveRule(0)` の1本を全点の基準に置く。");
        Console.WriteLine();
        Console.WriteLine("**見るのは勝率ではなく、ウツの攻撃力の到達点と、ウツ以外の駒の目減り量の比。**");
        Console.WriteLine("`利得` = ウツ攻の最大 − 開戦時（逆しまは下げ幅の3倍で読むので `Penalty×発火×3` が理論値）。");
        Console.WriteLine("`目減り` = ウツ以外の受け手が失った攻撃力の総量（`よろけ` の延べ体数 × `Penalty` から");
        Console.WriteLine("ウツ取り分を引いたもの）。");
        Console.WriteLine();

        int[] pens = { 0, 1, 2, 3 };
        Console.WriteLine("| Penalty | 平均勝率 | 突き返し/戦 | よろけ/戦 | ウツ攻(開→最大) | 利得 | ウツ以外の目減り | 比 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int pen in pens)
        {
            double wins = 0, fireS = 0, stagS = 0, openS = 0, maxS = 0, othS = 0;
            for (int w = 0; w < shStages.Count; w++)
            {
                var z = MeasureShove(bf, shStages[w].Enemy, new ShoveRule(pen));
                fireS += z.Fire; stagS += z.Stagger; openS += z.AtkOpen; maxS += z.AtkMax;
                double utsu = z.Hit.Where(k => k.Key.Contains("ウツ")).Sum(k => (double)k.Value) / ShSeeds;
                othS += (z.Stagger - utsu) * pen;
                for (int seed = 0; seed < ShSeeds; seed++)
                    if (BattleEngine.Run(bf, shStages[w].Enemy, seed, false, null, null, null, null,
                                         null, new ShoveRule(pen)).PlayerWon) wins++;
            }
            int nw = shStages.Count;
            double gain = (maxS - openS) / nw;
            double loss = othS / nw;
            Console.WriteLine($"| {pen} | {wins * 100.0 / (ShSeeds * nw):0.0}% | {fireS / nw:0.00} | {stagS / nw:0.00} "
                + $"| {openS / nw:0.0} → {maxS / nw:0.0} | {gain:+0.0;-0.0;0.0} | {loss:0.00} "
                + $"| {(loss > 0.001 ? (gain / loss).ToString("0.00") : "—")} |");
        }
        Console.WriteLine();

        // ---- 波ごとの勝率（崖の確認・受け入れ基準4）------------------------------------------
        Console.WriteLine("### 3. 波ごとの勝率（受け入れ基準4 ＝ 崖になっていないか）");
        Console.WriteLine();
        Console.WriteLine("| Penalty" + string.Concat(Enumerable.Range(1, shStages.Count).Select(i => $" | 第{i}波")) + " | 平均 |");
        Console.WriteLine("|--:" + string.Concat(shStages.Select(_ => "|--:")) + "|--:|");
        foreach (int pen in pens)
        {
            var cells = new List<double>();
            for (int w = 0; w < shStages.Count; w++)
            {
                int win = 0;
                for (int seed = 0; seed < ShSeeds; seed++)
                    if (BattleEngine.Run(bf, shStages[w].Enemy, seed, false, null, null, null, null,
                                         null, new ShoveRule(pen)).PlayerWon) win++;
                cells.Add(win * 100.0 / ShSeeds);
            }
            Console.WriteLine($"| {pen}" + string.Concat(cells.Select(c => $" | {c:0.0}%"))
                + $" | {cells.Average():0.0}% |");
        }
        Console.WriteLine();
    }

    // ---- ローカル変種（読み手の有無で符号が反転するか。§4-5）---------------------------------
    Console.WriteLine("## 変種（読み手の有無 —— `CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine("採用行の**ウツ1枚だけ**を差し替えた版を診断のローカルに組む");
    Console.WriteLine("（`gradient` / `aim` / `route` と同じ扱い）。**動く変数は中央の1枚だけ。**");
    Console.WriteLine();
    Console.WriteLine("- `V0 ウツ`   …… 採用行そのもの（読み手あり）");
    Console.WriteLine("- `V1 ノノ`   …… 読み手を回復役に差し替え（弱体化を読む駒がいない）");
    Console.WriteLine("- `V2 ムド`   …… 読み手を被弾強化に差し替え（弱体化は素の減算）");
    Console.WriteLine();
    Console.WriteLine("各版で**ハネを抜いた版**（4体）との差を取る。**ハネの寄与の符号が反転すれば、");
    Console.WriteLine("「編成によって同じ駒の符号が変わる」が実体を持つ。**");
    Console.WriteLine("4体版は `ablate` と同じく5体目の体そのものを含むので、**版どうしの差**で読むこと。");
    Console.WriteLine();

    (string Name, Formation With, Formation Without)[] shVars =
    {
        ("V0 ウツ（採用行）",
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane),
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga)),
        ("V1 ノノ（回復役）",
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Nono, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane),
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Nono, back1: UnitCatalog.Dolga)),
        ("V2 ムド（被弾強化）",
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Mudo, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane),
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Mudo, back1: UnitCatalog.Dolga)),
    };

    Console.WriteLine("| 版 | ハネ入り | ハネ抜き(4体) | ハネの寄与 |"
        + string.Concat(Enumerable.Range(1, shStages.Count).Select(i => $" 第{i}波差 |")));
    Console.WriteLine("|---|--:|--:|--:|" + string.Concat(shStages.Select(_ => "---:|")));
    foreach (var (vname, with, without) in shVars)
    {
        var dw = new List<double>();
        double aw = 0, ao = 0;
        for (int w = 0; w < shStages.Count; w++)
        {
            int a = 0, b = 0;
            for (int seed = 0; seed < ShSeeds; seed++)
            {
                if (BattleEngine.Run(with, shStages[w].Enemy, seed, false).PlayerWon) a++;
                if (BattleEngine.Run(without, shStages[w].Enemy, seed, false).PlayerWon) b++;
            }
            double pa = a * 100.0 / ShSeeds, pb = b * 100.0 / ShSeeds;
            aw += pa; ao += pb; dw.Add(pa - pb);
        }
        int nw = shStages.Count;
        Console.WriteLine($"| {vname} | {aw / nw:0.0}% | {ao / nw:0.0}% | {(aw - ao) / nw:+0.0;-0.0;0.0}pt |"
            + string.Concat(dw.Select(d => $" {d:+0.0;-0.0;0.0} |")));
    }
    Console.WriteLine();

    // ---- 符号反転の本命: 版ごとに Penalty を振る -----------------------------------------
    //
    // **`ablate` では測れない。** ハネを抜いた 4 体版はどの版でも 100/0/0/0/0 の床に落ちる
    // （第21期 swap の「4体（中央 空）」検査と同じ症状）ので、抜いた差は
    // 「5体目の体そのもの」の値段に潰れて機構の符号が読めない。
    //
    // 代わりに**同じ5体のまま `Penalty` だけを振る**。効果Bは味方の攻撃力しか触らないので、
    // **Penalty に対する勝率の傾きがそのまま「効果Bが利得か代金か」の符号**になる。
    // 読み手（ウツ）がいる版で正、いない版で負になれば、符号反転は実体を持つ。
    Console.WriteLine("### 符号反転（`Penalty` に対する勝率の傾き）");
    Console.WriteLine();
    Console.WriteLine("**`ablate` では測れない**——ハネを抜いた4体版はどの版でも床（100/0/0/0/0）に");
    Console.WriteLine("落ちるので、抜いた差が「5体目の体そのもの」の値段に潰れる");
    Console.WriteLine("（第21期 `swap` の「4体（中央 空）」検査と同じ症状）。");
    Console.WriteLine();
    Console.WriteLine("代わりに**同じ5体のまま `Penalty` だけを振る**。効果Bは味方の攻撃力しか触らないので、");
    Console.WriteLine("**`Penalty` に対する勝率の傾きがそのまま「効果Bが利得か代金か」の符号**になる。");
    Console.WriteLine();
    Console.WriteLine("| 版 | P=0 | P=1 | P=2 | P=3 | Δ(3−0) | 符号 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|:-:|");
    int[] shPens = { 0, 1, 2, 3 };
    foreach (var (vname, with, _) in shVars)
    {
        var cells = new List<double>();
        foreach (int pen in shPens)
        {
            int win = 0;
            for (int w = 0; w < shStages.Count; w++)
                for (int seed = 0; seed < ShSeeds; seed++)
                    if (BattleEngine.Run(with, shStages[w].Enemy, seed, false, null, null, null,
                                         null, null, new ShoveRule(pen)).PlayerWon) win++;
            cells.Add(win * 100.0 / (ShSeeds * shStages.Count));
        }
        double d = cells[^1] - cells[0];
        Console.WriteLine($"| {vname}" + string.Concat(cells.Select(c => $" | {c:0.0}%"))
            + $" | {d:+0.0;-0.0;0.0}pt | {(d > 0 ? "**＋**" : d < 0 ? "**−**" : "0")} |");
    }
    Console.WriteLine();

    // ---- 席と効果Bの値段（P1 の直接検算）-------------------------------------------------
    //
    // 編成5枠が埋まっていれば隣接は角=2体・中央=4体なので、効果Bの延べ体数は**ちょうど 2.0 倍**
    // になるはず——というのが机上の予測。**ただし喧噪（バサ）が毎ターン味方2体を混ぜる**ので、
    // 初期配置の次数が効くのは開幕の数ターンだけ。倍率が 2.0 から落ちるなら、
    // それは席が固定でないことの直接の証拠になる。
    //
    // 比べるのは**ハネの席だけを動かした2通り**（他4体の相対配置は変えない）。
    Console.WriteLine("### 席と効果Bの値段（P1 の直接検算）");
    Console.WriteLine();
    Console.WriteLine("編成5枠が埋まっていれば隣接は**角=2体・中央=4体**なので、効果Bの延べ体数は");
    Console.WriteLine("机上では**ちょうど 2.0 倍**になるはず。**ハネの席だけ**を動かして測る。");
    Console.WriteLine();
    Console.WriteLine("| ハネの席 | 隣接次数(初期) | 突き返し/戦 | よろけ/戦 | 弾き/戦 | よろけ÷発火 | ウツ攻(開→最大) | 平均勝率 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");

    (string Seat, int Deg, Formation F)[] shSeats =
    {
        ("後3（角）", 2, Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald,
            center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane)),
        ("中央", 4, Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald,
            center: UnitCatalog.Hane, back1: UnitCatalog.Dolga, back3: UnitCatalog.Utsu)),
    };

    foreach (var (seat, deg, sf) in shSeats)
    {
        double fi = 0, st = 0, bl = 0, op = 0, mx = 0, win = 0;
        for (int w = 0; w < shStages.Count; w++)
        {
            var z = MeasureShove(sf, shStages[w].Enemy, ShoveRule.Default);
            fi += z.Fire; st += z.Stagger; bl += z.Block; op += z.AtkOpen; mx += z.AtkMax;
            for (int seed = 0; seed < ShSeeds; seed++)
                if (BattleEngine.Run(sf, shStages[w].Enemy, seed, false).PlayerWon) win++;
        }
        int nw = shStages.Count;
        Console.WriteLine($"| {seat} | {deg} | {fi / nw:0.00} | {st / nw:0.00} | {bl / nw:0.00} "
            + $"| {st / Math.Max(0.001, fi):0.00} | {op / nw:0.0} → {mx / nw:0.0} "
            + $"| {win * 100.0 / (ShSeeds * nw):0.0}% |");
    }
    Console.WriteLine();
    Console.WriteLine("**`よろけ÷発火` が机上の次数（2 と 4）に届かない差が、席が固定でないことの代金。**");
    Console.WriteLine("喧噪は毎ターン味方2体を混ぜるので、初期配置の次数が効くのは開幕の数ターンだけ。");
    Console.WriteLine();

    // ---- シオ（移り木）との同居可否 -------------------------------------------------------
    //
    // 移り木は**動かされた味方を癒し、攻撃力を +5 する**（DrifterTrait.Gain）。
    // ウツは強化されると攻撃力が半減する（PerverseTrait: AtkBonus > 0 で baseAtk / 2）ので、
    // 喧噪が毎ターン味方2体を動かす台では**移動が起きるたびシオがウツを台無しにする**
    // 可能性がある。移動軸の既存行のうち `移動改 (バサ×ヨミ×シオ)` がシオ入りなので、
    // 同居の可否を数字で出しておく（指示書 §6-3）。
    Console.WriteLine("### シオ（移り木）との同居可否（指示書 §6-3）");
    Console.WriteLine();
    Console.WriteLine("移り木は動かされた味方の `AtkBonus` を **+5**（`DrifterTrait.Gain`）。");
    Console.WriteLine("突き返しは1ターン1回 **−Penalty**。**同じ `AtkBonus` を逆向きに奪い合う。**");
    Console.WriteLine("ウツは `AtkBonus > 0` で攻撃力が半減するので、勝つ側が符号を決める。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 平均勝率 | ウツ攻(開/最大/最終T) |");
    Console.WriteLine("|---|--:|--:|");

    Formation shioOff = Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald,
        center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane);
    Formation shioOn = Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald,
        center: UnitCatalog.Utsu, back1: UnitCatalog.Shio, back3: UnitCatalog.Hane);

    foreach (var (vn, vf) in new[] { ("シオなし（採用行）", shioOff), ("シオあり（ドルガ→シオ）", shioOn) })
    {
        double op = 0, mx = 0, la = 0, win = 0;
        for (int w = 0; w < shStages.Count; w++)
        {
            var z = MeasureShove(vf, shStages[w].Enemy, ShoveRule.Default);
            op += z.AtkOpen; mx += z.AtkMax; la += z.AtkLast;
            for (int seed = 0; seed < ShSeeds; seed++)
                if (BattleEngine.Run(vf, shStages[w].Enemy, seed, false).PlayerWon) win++;
        }
        int nw = shStages.Count;
        Console.WriteLine($"| {vn} | {win * 100.0 / (ShSeeds * nw):0.0}% "
            + $"| {op / nw:0.0} / {mx / nw:0.0} / {la / nw:0.0} |");
    }
    Console.WriteLine();
    return;
}

// 弱体を通貨として測る（第42期）。窓口 BattleContext.Dull を通った量を経路別に数え、
// 集約（引き受け・ウケ）がそれをどれだけ横取りしてアーマーに変えたかを出す。
//
// **経路別はログではなく結果（BattleResult.DullByRoute）から取る。** 開戦時1回の3経路
// （呪詛の敵側・呪詛の味方漏れ・萎縮）はログを**1行にまとめて**出すので、
// 文字列からは延べ体数が復元できない（gullet log / yoke log / sever がログを数えたのは、
// あちらが「その行が出たか出なかったか」そのものを見ていたから）。
//
// **「鎧」と「死蔵」を分けて数える。** 生成量だけを見ると第23期の巨躯の吐き戻し
// （経路は通ったが、攻撃力という遅い通貨に変換したので使う前に戦闘が終わる）と
// 同じ穴に落ちる。アーマーも遅い通貨かもしれない。
if (focusId == "dull")
{
    var duBuilds = CompareBuilds();
    const int DuSeeds = 200;   // compare / spread / shove と同じ。balance.md と突き合わせる

    string duFilter = args.Length > 2 ? args[2] : "分かち×逆しま,引き受け";
    var duTargets = duBuilds
        .Where(b => duFilter.Length == 0 || duFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    IReadOnlyList<EnemyCatalog.Stage> duStages = EnemyCatalog.Stages;

    // 味方の InstanceId はスロット昇順に 0 から振られる（Materialize → ctx.Add）。
    static int IdOf(Formation f, TraitId t)
    {
        for (int i = 0, k = 0; i < FormationRules.PlayableSlotCount; i++)
            if (f[i] is { } d) { if (d.Traits.Contains(t)) return k; k++; }
        return -1;
    }

    (double Total, double[] Route, double Taken, double Passed, double Armor, double Soaked,
     double UtsuOpen, double UtsuMax, double UtsuLast, double UkeOpen, double UkeLast,
     double Turns, double Win, Dictionary<string, double> From)
    MeasureDull(Formation f, Formation enemy, BearRule rule)
    {
        var route = new double[DullRoutes.Count];
        var from = new Dictionary<string, double>();
        double total = 0, taken = 0, passed = 0, armor = 0, soaked = 0, turns = 0, win = 0;
        double uo = 0, um = 0, ul = 0, useen = 0, ko = 0, kl = 0, kseen = 0;

        int utsuId = IdOf(f, TraitId.Perverse);
        int ukeId = IdOf(f, TraitId.Bear);

        for (int seed = 0; seed < DuSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: true,
                                    null, null, null, null, null, null, rule);
            total += r.DullTotal;
            for (int i = 0; i < route.Length; i++) route[i] += r.DullByRoute[i];
            taken += r.BearTaken; passed += r.BearPassed;
            armor += r.BearArmor; soaked += r.BearSoaked;
            turns += r.Turns; if (r.PlayerWon) win++;
            foreach (var kv in r.BearFrom)
                from[kv.Key] = from.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

            if (utsuId >= 0)
            {
                var snaps = r.Events.Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == utsuId)
                                    .Select(e => e.Amount).ToList();
                if (snaps.Count > 0) { uo += snaps[0]; um += snaps.Max(); ul += snaps[^1]; useen++; }
            }
            if (ukeId >= 0)
            {
                var snaps = r.Events.Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == ukeId)
                                    .Select(e => e.Amount).ToList();
                if (snaps.Count > 0) { ko += snaps[0]; kl += snaps[^1]; kseen++; }
            }
        }

        double n = DuSeeds, mu = Math.Max(1, useen), mk = Math.Max(1, kseen);
        for (int i = 0; i < route.Length; i++) route[i] /= n;
        foreach (string k in from.Keys.ToList()) from[k] /= n;
        return (total / n, route, taken / n, passed / n, armor / n, soaked / n,
                uo / mu, um / mu, ul / mu, ko / mk, kl / mk, turns / n, win * 100.0 / n, from);
    }

    Console.WriteLine("# 弱体の通貨（dull）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 dull [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{DuSeeds - 1}。数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`CompareBuilds()` / `Stages` / `Columns` は触っていない。");
    Console.WriteLine("既定の絞り込みは `分かち×逆しま,引き受け`（引数で上書きできる）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 弱体総量 | 窓口 `Dull` を通った総量/戦（**両陣営**。呪詛の敵側を含む） |");
    Console.WriteLine("| 経路別 | なまり / 呪詛敵 / 呪詛漏れ / 突き返し / 萎縮 の内訳 |");
    Console.WriteLine("| 横取り | 集約役が引き受けた量/戦 |");
    Console.WriteLine("| 素通り | 横取りされずにそのまま入った量/戦（隣に集約役がいない） |");
    Console.WriteLine("| 鎧 | 生成したアーマー量/戦 と、**実際にダメージを吸った量**/戦 |");
    Console.WriteLine("| 死蔵 | 生成したのに使われずに終わった量/戦（= 生成 − 吸った） |");
    Console.WriteLine("| ウツ攻 | 逆しま持ちの `CurrentAttack`（開戦時 / 最大 / 最終T）。`StatSnapshot` から |");
    Console.WriteLine("| ウケ攻 | 集約持ちの `CurrentAttack`（開戦時 / 最終T） |");
    Console.WriteLine();
    Console.WriteLine("**生成量ではなく吸った量で判断する**（第23期の吐き戻しと同じ穴を避けるため）。");
    Console.WriteLine();

    // ---- 検算: ウケ抜きの行は BearRule に対して不変か（受け入れ基準3）-----------------------
    Console.WriteLine("## 0. 検算 —— ウケを含まない行は `BearRule` に対して不変か（受け入れ基準3）");
    Console.WriteLine();
    Console.WriteLine("集約役が盤上にいなければ横取りは1回も走らないので、`ArmorPerDull` を");
    Console.WriteLine("どう振っても勝率は1セルも動かないはず。**分母はセル数**。");
    Console.WriteLine();
    {
        var plain = duBuilds.Where(b => !Enumerable.Range(0, FormationRules.PlayableSlotCount)
                                        .Any(i => b.F[i]?.Id == "uke")).ToArray();
        int cells = 0, diff = 0;
        foreach (var (_, bf) in plain)
            for (int w = 0; w < duStages.Count; w++)
            {
                int a = 0, b3 = 0;
                for (int seed = 0; seed < DuSeeds; seed++)
                {
                    if (BattleEngine.Run(bf, duStages[w].Enemy, seed, false, null, null, null, null,
                                         null, null, new BearRule(0)).PlayerWon) a++;
                    if (BattleEngine.Run(bf, duStages[w].Enemy, seed, false, null, null, null, null,
                                         null, null, new BearRule(3)).PlayerWon) b3++;
                }
                cells++; if (a != b3) diff++;
            }
        Console.WriteLine($"`BearRule(0)` と `BearRule(3)` の突き合わせ: **{cells} セル中 {diff} 件の食い違い**"
            + $"（ウケを含まない {plain.Length} 行 × {duStages.Count} 波）。");
    }
    Console.WriteLine();

    foreach (var (bname, bf) in duTargets)
    {
        Console.WriteLine($"## {bname}");
        Console.WriteLine();

        bool hasUke = Enumerable.Range(0, FormationRules.PlayableSlotCount)
                                .Any(i => bf[i]?.Id == "uke");

        Console.WriteLine($"### 1. 計数（`BearRule.Default` ＝ ArmorPerDull {BearRule.Default.ArmorPerDull}）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 弱体総量 | なまり | 呪詛敵 | 呪詛漏れ | 突き返し | 萎縮 | 横取り | 素通り | 鎧(生成/吸) | 死蔵 | ウツ攻(開/最大/終) | ウケ攻(開/終) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

        var fromAll = new Dictionary<string, double>();
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(bf, duStages[w].Enemy, BearRule.Default);
            foreach (var kv in z.From)
                fromAll[kv.Key] = fromAll.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            Console.WriteLine($"| 第{w + 1}波 | {z.Win:0.0}% | {z.Total:0.00} "
                + $"| {z.Route[(int)DullRoute.Sharer]:0.00} | {z.Route[(int)DullRoute.CurseEnemy]:0.00} "
                + $"| {z.Route[(int)DullRoute.CurseLeak]:0.00} | {z.Route[(int)DullRoute.Shove]:0.00} "
                + $"| {z.Route[(int)DullRoute.Cower]:0.00} "
                + $"| {z.Taken:0.00} | {z.Passed:0.00} | {z.Armor:0.00} / {z.Soaked:0.00} "
                + $"| {z.Armor - z.Soaked:0.00} "
                + $"| {z.UtsuOpen:0.0} / {z.UtsuMax:0.0} / {z.UtsuLast:0.0} "
                + $"| {z.UkeOpen:0.0} / {z.UkeLast:0.0} | {z.Turns:0.0} |");
        }
        Console.WriteLine();

        if (hasUke)
        {
            Console.WriteLine("#### 横取りの相手（量/戦・全波の合計）");
            Console.WriteLine();
            Console.WriteLine("**なまりは「守られた駒」に乗る**ので、ここに出るのは");
            Console.WriteLine("「ウケの隣にいる駒」ではなく「ウケの隣で**殴られた**駒」。");
            Console.WriteLine();
            Console.WriteLine("| 取られた相手 | 量/戦（5波合計） |");
            Console.WriteLine("|---|--:|");
            foreach (var kv in fromAll.OrderByDescending(k => k.Value))
                Console.WriteLine($"| {kv.Key} | {kv.Value:0.00} |");
            Console.WriteLine();
        }

        // ---- 陽性対照 ----------------------------------------------------------------------
        Console.WriteLine("### 2. 陽性対照 `BearRule(0)`（横取りはするが変換しない）");
        Console.WriteLine();
        Console.WriteLine("`ArmorPerDull = 0` は**変換だけを止める**（横取りは走る）。");
        Console.WriteLine("横取り・素通りは既定と同じで、鎧が 0 になるはず。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 横取り | 素通り | 鎧(生成/吸) | ウケ攻(開/終) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(bf, duStages[w].Enemy, new BearRule(0));
            Console.WriteLine($"| 第{w + 1}波 | {z.Win:0.0}% | {z.Taken:0.00} | {z.Passed:0.00} "
                + $"| {z.Armor:0.00} / {z.Soaked:0.00} | {z.UkeOpen:0.0} / {z.UkeLast:0.0} | {z.Turns:0.0} |");
        }
        Console.WriteLine();

        // ---- 掃引 --------------------------------------------------------------------------
        Console.WriteLine("### 3. 掃引（`ArmorPerDull`）");
        Console.WriteLine();
        Console.WriteLine("**見るのは勝率ではなく、生成したアーマーのうち実際に吸われた割合**（`吸率`）。");
        Console.WriteLine("第41期の掃引は「比が全点で 2.30 で動かない＝ノブは強度しか変えず性質を変えない」を");
        Console.WriteLine("示した。全点で吸率が同じなら、`ArmorPerDull` も強度ノブでしかない。");
        Console.WriteLine();
        Console.WriteLine("| ArmorPerDull | 平均勝率 | 横取り/戦 | 鎧 生成/戦 | 鎧 吸/戦 | 死蔵/戦 | 吸率 | ウケ攻(開→終) |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int apd in new[] { 0, 1, 2, 3 })
        {
            double win = 0, tk = 0, ar = 0, so = 0, ko = 0, kl = 0;
            for (int w = 0; w < duStages.Count; w++)
            {
                var z = MeasureDull(bf, duStages[w].Enemy, new BearRule(apd));
                win += z.Win; tk += z.Taken; ar += z.Armor; so += z.Soaked; ko += z.UkeOpen; kl += z.UkeLast;
            }
            int nw = duStages.Count;
            Console.WriteLine($"| {apd} | {win / nw:0.0}% | {tk / nw:0.00} | {ar / nw:0.00} | {so / nw:0.00} "
                + $"| {(ar - so) / nw:0.00} | {(ar > 0.001 ? (so * 100.0 / ar).ToString("0.0") + "%" : "—")} "
                + $"| {ko / nw:0.0} → {kl / nw:0.0} |");
        }
        Console.WriteLine();

        // ---- 波ごとの勝率（崖の確認・受け入れ基準5）------------------------------------------
        Console.WriteLine("### 4. 波ごとの勝率（受け入れ基準5 ＝ 崖になっていないか）");
        Console.WriteLine();
        Console.WriteLine("| ArmorPerDull" + string.Concat(Enumerable.Range(1, duStages.Count).Select(i => $" | 第{i}波")) + " | 平均 |");
        Console.WriteLine("|--:" + string.Concat(duStages.Select(_ => "|--:")) + "|--:|");
        foreach (int apd in new[] { 0, 1, 2, 3 })
        {
            var cells = new List<double>();
            for (int w = 0; w < duStages.Count; w++)
            {
                int win = 0;
                for (int seed = 0; seed < DuSeeds; seed++)
                    if (BattleEngine.Run(bf, duStages[w].Enemy, seed, false, null, null, null, null,
                                         null, null, new BearRule(apd)).PlayerWon) win++;
                cells.Add(win * 100.0 / DuSeeds);
            }
            Console.WriteLine($"| {apd}" + string.Concat(cells.Select(c => $" | {c:0.0}%"))
                + $" | {cells.Average():0.0}% |");
        }
        Console.WriteLine();
    }

    // ---- ウケ抜きの対照（陽性対照その2）------------------------------------------------------
    Console.WriteLine("## 変種（`CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine("採用行の**1枚だけ**を差し替えた版を診断のローカルに組む");
    Console.WriteLine("（`gradient` / `aim` / `route` と同じ扱い）。");
    Console.WriteLine();

    Console.WriteLine("### A. ウケ抜き（4体）—— 陽性対照その2");
    Console.WriteLine();
    Console.WriteLine("**4体版が5体版と同じ値なら、その台は飽和していて測定になっていない**");
    Console.WriteLine("（第21期 `swap` の検査）。ここでは中央を空けたぶんの体の値段も混ざるので、");
    Console.WriteLine("**符号ではなく「動くかどうか」だけを読む**。");
    Console.WriteLine();

    Formation ukeRow = Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
        center: UnitCatalog.Uke, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    Formation ukeGone = Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
        back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);

    Console.WriteLine("| 版 | 平均勝率" + string.Concat(Enumerable.Range(1, duStages.Count).Select(i => $" | 第{i}波")) + " | なまり/戦 | 横取り/戦 |");
    Console.WriteLine("|---|--:" + string.Concat(duStages.Select(_ => "|--:")) + "|--:|--:|");
    foreach (var (vn, vf) in new[] { ("ウケあり（採用行）", ukeRow), ("ウケ抜き（4体・中央 空）", ukeGone) })
    {
        var cells = new List<double>();
        double sh = 0, tk = 0;
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(vf, duStages[w].Enemy, BearRule.Default);
            cells.Add(z.Win); sh += z.Route[(int)DullRoute.Sharer]; tk += z.Taken;
        }
        Console.WriteLine($"| {vn} | {cells.Average():0.0}%" + string.Concat(cells.Select(c => $" | {c:0.0}%"))
            + $" | {sh:0.00} | {tk:0.00} |");
    }
    Console.WriteLine();

    // ---- 排他（受け入れ基準7）----------------------------------------------------------------
    Console.WriteLine("### B. 横取りの排他（受け入れ基準7）");
    Console.WriteLine();
    Console.WriteLine("**ウケの席は固定（前1）で、ウツの席だけを動かす。** 前1の隣接は");
    Console.WriteLine("`{中央, 後1}` なので、ウツを中央に置けば隣接・前3に置けば非隣接になる");
    Console.WriteLine("（角どうし＝前1と前3は隣接していない。`AdjacencyTable` 参照）。");
    Console.WriteLine("**動く変数はウツとガルドの入れ替え1つだけ。**");
    Console.WriteLine();
    Console.WriteLine("ウツの攻撃力の到達点が隣接版で下がれば、**同じ供給を2枚の読み手が配置で分け合う**が実体を持つ。");
    Console.WriteLine();

    Formation adj = Formation.Build(front1: UnitCatalog.Uke, front3: UnitCatalog.Gald,
        center: UnitCatalog.Utsu, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    Formation far = Formation.Build(front1: UnitCatalog.Uke, front3: UnitCatalog.Utsu,
        center: UnitCatalog.Gald, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);

    Console.WriteLine("| 版 | 平均勝率 | なまり/戦 | 横取り/戦 | 素通り/戦 | 鎧(生成/吸) | ウツ攻(開/最大/終) | ウケ攻(開/終) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var (vn, vf) in new[] { ("隣接（ウケ前1 / ウツ中央）", adj), ("非隣接（ウケ前1 / ウツ前3）", far) })
    {
        double win = 0, sh = 0, tk = 0, ps = 0, ar = 0, so = 0, uo = 0, um = 0, ul = 0, ko = 0, kl = 0;
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(vf, duStages[w].Enemy, BearRule.Default);
            win += z.Win; sh += z.Route[(int)DullRoute.Sharer]; tk += z.Taken; ps += z.Passed;
            ar += z.Armor; so += z.Soaked; uo += z.UtsuOpen; um += z.UtsuMax; ul += z.UtsuLast;
            ko += z.UkeOpen; kl += z.UkeLast;
        }
        int nw = duStages.Count;
        Console.WriteLine($"| {vn} | {win / nw:0.0}% | {sh:0.00} | {tk:0.00} | {ps:0.00} "
            + $"| {ar:0.00} / {so:0.00} | {uo / nw:0.0} / {um / nw:0.0} / {ul / nw:0.0} "
            + $"| {ko / nw:0.0} / {kl / nw:0.0} |");
    }
    Console.WriteLine();

    // ---- 移り木との同居（§6-6）---------------------------------------------------------------
    Console.WriteLine("### C. 移り木（シオ）との同居 —— 同じ窓口の逆向き");
    Console.WriteLine();
    Console.WriteLine("第41期の実測では、シオの `+5`（動かされた味方を強化）が突き返しの `−2` を");
    Console.WriteLine("打ち消してウツの `AtkBonus` を正へ振り、逆しまの半減側に落とした。");
    Console.WriteLine("**`Dull` の窓口ができたことで、この干渉は「同じ窓口の逆向き」として初めて計測できる**");
    Console.WriteLine("——強化はまだ窓口を持たないので、読めるのは弱体の側の量だけ。");
    Console.WriteLine();
    Console.WriteLine("台（分かち×逆しま）のノノをシオに差し替える。移動の供給は第五波の曝き（告発人）。");
    Console.WriteLine();

    Formation shioOff = Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald,
        center: UnitCatalog.Nono, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    Formation shioOn = Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald,
        center: UnitCatalog.Shio, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);

    Console.WriteLine("| 版 | 波 | 勝率 | なまり/戦 | ウツ攻(開/最大/終) |");
    Console.WriteLine("|---|---|--:|--:|--:|");
    foreach (var (vn, vf) in new[] { ("シオなし（台）", shioOff), ("シオあり（ノノ→シオ）", shioOn) })
        for (int w = 0; w < duStages.Count; w++)
        {
            var z = MeasureDull(vf, duStages[w].Enemy, BearRule.Default);
            Console.WriteLine($"| {vn} | 第{w + 1}波 | {z.Win:0.0}% | {z.Route[(int)DullRoute.Sharer]:0.00} "
                + $"| {z.UtsuOpen:0.0} / {z.UtsuMax:0.0} / {z.UtsuLast:0.0} |");
        }
    Console.WriteLine();
    return;
}

// 渡し（転嫁）を測る（第43期）。窓口 BattleContext.Dull の中で味方から敵へ移った量を数え、
// **流した量ではなく「味方の被ダメージがいくら減ったか」**で読む。
//
// **「渡し」と「効き」を分けて数えるのが要。** 第42期が「生成したアーマー」ではなく
// 「実際に吸った量」で判断して死蔵率 2.5% を出したのと同じ理由で、流した量は成果ではない
// ——敵の攻撃力を下げても、その敵が既に死んでいたり、もともと殴らない駒だったりすれば
// 効いていない。効きの分母は**敵が味方に与えたダメージ**（敵 tally の DamageToEnemy）で、
// 転嫁を止めた同じ台（RelayRule(0)）との差で取る。
//
// **「自弁率」も必須。** 代金は ApplyDamage を通すので肩代わり5種が割り込む。
// 「横取り量 × 2 を払った」ことにはならない。
if (focusId == "relay")
{
    var reBuilds = CompareBuilds();
    const int ReSeeds = 200;   // compare / spread / shove / dull と同じ。balance.md と突き合わせる

    // 第2引数に `kubi` を渡すと変種Cだけを回す（主表と検算は 47行×5波×200seed×2版 で重い）。
    bool reKubiOnly = args.Length > 2 && args[2] == "kubi";
    string reFilter = args.Length > 2 && !reKubiOnly ? args[2] : "渡し";
    var reTargets = reBuilds
        .Where(b => reFilter.Length == 0 || reFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    IReadOnlyList<EnemyCatalog.Stage> reStages = EnemyCatalog.Stages;

    // 敵が味方に与えたダメージ。**敵側の tally から取る**（第13期 Phase DA と同じ理由）。
    // 味方側の DamageTaken から引くと、渡しの代金（source が null の自傷）が混ざる
    // ——あれは TakenFromAlly にも載らない（ApplyDamage は source が null だと
    // 味方由来の印を立てない）ので、味方側からは分離できない。
    static int EnemyOutput(BattleResult r, Formation enemy)
    {
        int sum = 0;
        foreach ((int _, UnitDef d) in enemy.Occupied())
            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) sum += t.DamageToEnemy;
        return sum;
    }

    (double Taken, double Passed, double Sent, double Cost, double SelfPaid,
     double MaxSent, double Zeroed, double Foe, double Death, double Died,
     double Turns, double Win, Dictionary<string, double> From, Dictionary<string, double> To)
    MeasureRelay(Formation f, Formation enemy, RelayRule rule)
    {
        var from = new Dictionary<string, double>();
        var to = new Dictionary<string, double>();
        double taken = 0, passed = 0, sent = 0, cost = 0, self = 0, maxSent = 0, zero = 0;
        double foe = 0, death = 0, died = 0, turns = 0, win = 0;

        for (int seed = 0; seed < ReSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                                    null, null, null, null, null, null, null, rule);
            taken += r.RelayTaken; passed += r.BearPassed; sent += r.RelaySent;
            cost += r.RelayCost; self += r.RelaySelfPaid; zero += r.RelayZeroed;
            if (r.RelayMaxSent > maxSent) maxSent = r.RelayMaxSent;
            foe += EnemyOutput(r, enemy);
            turns += r.Turns; if (r.PlayerWon) win++;
            foreach (var kv in r.RelayFrom)
                from[kv.Key] = from.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in r.RelayTo)
                to[kv.Key] = to.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

            // 早逝: ワタが倒れた試行の、倒れたターン（UnitTally.LastActiveTurn は死亡時に
            // その手番のターンで上書きされる）。倒れなかった試行は分母に入れない。
            if (r.TallyByUnit.TryGetValue(UnitCatalog.Wata.Id, out UnitTally? wt) && wt.Deaths > 0)
            {
                died++; death += wt.LastActiveTurn;
            }
        }

        double n = ReSeeds, md = Math.Max(1, died);
        foreach (string k in from.Keys.ToList()) from[k] /= n;
        foreach (string k in to.Keys.ToList()) to[k] /= n;
        return (taken / n, passed / n, sent / n, cost / n, self / n, maxSent, zero / n,
                foe / n, death / md, died / n, turns / n, win * 100.0 / n, from, to);
    }

    Console.WriteLine("# 転嫁（relay）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 relay [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{ReSeeds - 1}。数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`CompareBuilds()` / `Stages` / `Columns` は触っていない。");
    Console.WriteLine("既定の絞り込みは `渡し`（引数で上書きできる）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 横取り | ワタが引き受けた量/戦（`RelayTaken`） |");
    Console.WriteLine("| 素通り | 隣接に横取り役がいなくて素通りした量/戦（`BearPassed`） |");
    Console.WriteLine("| 渡し | 敵へ流した量/戦（`RelaySent`） |");
    Console.WriteLine("| 最大流入 | **1回の `Dull` で流した最大量**（全 seed の最大。崖の検算） |");
    Console.WriteLine("| 攻ゼロ | 転嫁で敵の `CurrentAttack` が 0 になった回数/戦（**崖の検算**） |");
    Console.WriteLine("| 代金 | `ApplyDamage` へ渡した総量/戦（= 横取り × 2） |");
    Console.WriteLine("| 自弁 | そのうち**ワタ自身の身に落ちた量**/戦。`自弁率` = 自弁 ÷ 代金 |");
    Console.WriteLine("| 敵与ダメ | **敵が味方に与えたダメージ**/戦（敵 tally の `DamageToEnemy` の和） |");
    Console.WriteLine("| 早逝 | ワタが倒れたターン（倒れた試行の平均）と、倒れた試行の割合 |");
    Console.WriteLine();
    Console.WriteLine("**流した量は成果ではない。** 採否は「敵与ダメ」が対照よりいくら減ったか（＝効き）で読む。");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準2）------------------------------------------------------
    if (reKubiOnly) { reTargets = Array.Empty<(string Name, Formation F)>(); goto reVariants; }
    Console.WriteLine("## 0. 検算 —— ワタを含まない行は `RelayRule` に対して不変か（受け入れ基準2）");
    Console.WriteLine();
    Console.WriteLine("渡し役が盤上にいなければ横取りは1回も走らないので、`TransferPercent` を");
    Console.WriteLine("どう振っても勝率は1セルも動かないはず。**分母はセル数**。");
    Console.WriteLine();
    {
        int cells = 0, diff = 0, rows = 0;
        foreach (var b in reBuilds)
        {
            if (b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Wata.Id)) continue;
            rows++;
            for (int w = 0; w < reStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < ReSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, reStages[w].Enemy, seed, false,
                                         null, null, null, null, null, null, null, new RelayRule(0)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, reStages[w].Enemy, seed, false,
                                         null, null, null, null, null, null, null, new RelayRule(100)).PlayerWon) c++;
                }
                cells++; if (a != c) diff++;
            }
        }
        Console.WriteLine($"`RelayRule(0)` と `RelayRule(100)` の突き合わせ: **{cells} セル中 {diff} 件の食い違い**"
            + $"（ワタを含まない {rows} 行 × {reStages.Count} 波）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    foreach (var b in reTargets)
    {
        Console.WriteLine($"## {b.Name}");
        Console.WriteLine();
        Console.WriteLine("### 1. 計数（`RelayRule.Default` ＝ TransferPercent 100）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 横取り | 素通り | 渡し | 最大流入 | 攻ゼロ | 代金 | 自弁 | 自弁率 | 敵与ダメ | 早逝(T/率) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

        var full = new (double Taken, double Passed, double Sent, double Cost, double SelfPaid,
                        double MaxSent, double Zeroed, double Foe, double Death, double Died,
                        double Turns, double Win, Dictionary<string, double> From,
                        Dictionary<string, double> To)[reStages.Count];

        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(b.F, reStages[w].Enemy, RelayRule.Default);
            full[w] = z;
            Console.WriteLine($"| 第{w + 1}波 | {z.Win:0.0}% | {z.Taken:0.00} | {z.Passed:0.00} | {z.Sent:0.00} "
                + $"| {z.MaxSent:0} | {z.Zeroed:0.00} | {z.Cost:0.00} | {z.SelfPaid:0.00} "
                + $"| {(z.Cost > 0 ? $"{z.SelfPaid * 100 / z.Cost:0.0}%" : "—")} | {z.Foe:0.0} "
                + $"| {z.Death:0.0} / {z.Died * 100:0.0}% | {z.Turns:0.0} |");
            Console.Out.Flush();
        }

        Console.WriteLine();
        Console.WriteLine("#### 横取りの相手（量/戦・全波の合計）");
        Console.WriteLine();
        Console.WriteLine("**なまりは「守られた駒」に乗る**ので、ここに出るのは");
        Console.WriteLine("「ワタの隣にいる駒」ではなく「ワタの隣で**殴られた**駒」。");
        Console.WriteLine();
        Console.WriteLine("| 取られた相手 | 量/戦（5波合計） |");
        Console.WriteLine("|---|--:|");
        var fromAll = new Dictionary<string, double>();
        foreach (var z in full)
            foreach (var kv in z.From)
                fromAll[kv.Key] = fromAll.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
        foreach (var kv in fromAll.OrderByDescending(k => k.Value))
            Console.WriteLine($"| {kv.Key} | {kv.Value:0.00} |");

        Console.WriteLine();
        Console.WriteLine("#### 流し先（量/戦・波ごと）");
        Console.WriteLine();
        Console.WriteLine("**最高攻撃力の生存駒を決定的に選ぶ**ので、上から均されて対象が移る。");
        Console.WriteLine("1体に集中していたら自己分散が働いていない（＝崖の予兆）。");
        Console.WriteLine();
        for (int w = 0; w < reStages.Count; w++)
        {
            if (full[w].To.Count == 0) { Console.WriteLine($"- **第{w + 1}波**: （転嫁なし）"); continue; }
            Console.WriteLine($"- **第{w + 1}波**: "
                + string.Join(" / ", full[w].To.OrderByDescending(k => k.Value)
                                              .Select(kv => $"{kv.Key} {kv.Value:0.0}")));
        }
        Console.WriteLine();

        // --- 2. 陽性対照 -------------------------------------------------------------
        Console.WriteLine("### 2. 陽性対照 `RelayRule(0)`（横取りするが流さない＝除去役）");
        Console.WriteLine();
        Console.WriteLine("`TransferPercent = 0` は**転嫁だけを止める**（横取りも代金もそのまま走る）。");
        Console.WriteLine("**弱体はそこで消滅する**ので、これは除去役そのもの。");
        Console.WriteLine("`効き` は同じ台の転嫁ありとの敵与ダメの差（**正なら転嫁が敵の出力を削っている**）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 横取り | 渡し | 代金 | 自弁率 | 敵与ダメ | **効き** | 早逝(T/率) | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(b.F, reStages[w].Enemy, new RelayRule(0));
            Console.WriteLine($"| 第{w + 1}波 | {z.Win:0.0}% | {z.Taken:0.00} | {z.Sent:0.00} | {z.Cost:0.00} "
                + $"| {(z.Cost > 0 ? $"{z.SelfPaid * 100 / z.Cost:0.0}%" : "—")} | {z.Foe:0.0} "
                + $"| **{z.Foe - full[w].Foe:+0.0;-0.0}** | {z.Death:0.0} / {z.Died * 100:0.0}% | {z.Turns:0.0} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 3. 掃引 -----------------------------------------------------------------
        Console.WriteLine("### 3. 掃引（`TransferPercent` 0 / 50 / 100）");
        Console.WriteLine();
        Console.WriteLine("**0（除去）が 100（転嫁）と同等以上なら、転嫁という機構は要らない**（受け入れ基準10）。");
        Console.WriteLine("代金は3点とも同じなので、差は「流した先で何が起きたか」だけ。");
        Console.WriteLine();
        Console.WriteLine("| TransferPercent | 平均勝率 | 横取り/戦 | 渡し/戦 | 代金/戦 | 自弁率 | 攻ゼロ/戦 | 敵与ダメ/戦 | 早逝率 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        int[] tps = { 0, 50, 100 };
        var sweep = new double[tps.Length][];
        for (int i = 0; i < tps.Length; i++)
        {
            double win = 0, tk = 0, sn = 0, cs = 0, sp = 0, zr = 0, fo = 0, dd = 0;
            var per = new double[reStages.Count];
            for (int w = 0; w < reStages.Count; w++)
            {
                var z = MeasureRelay(b.F, reStages[w].Enemy, new RelayRule(tps[i]));
                per[w] = z.Win;
                win += z.Win; tk += z.Taken; sn += z.Sent; cs += z.Cost; sp += z.SelfPaid;
                zr += z.Zeroed; fo += z.Foe; dd += z.Died;
            }
            sweep[i] = per;
            int n = reStages.Count;
            Console.WriteLine($"| {tps[i]} | {win / n:0.0}% | {tk / n:0.00} | {sn / n:0.00} | {cs / n:0.00} "
                + $"| {(cs > 0 ? $"{sp * 100 / cs:0.0}%" : "—")} | {zr / n:0.00} | {fo / n:0.0} | {dd * 100 / n:0.0}% |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine("### 4. 波ごとの勝率（受け入れ基準5 ＝ 崖になっていないか）");
        Console.WriteLine();
        Console.WriteLine("| TransferPercent | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < tps.Length; i++)
            Console.WriteLine($"| {tps[i]} | " + string.Join(" | ", sweep[i].Select(v => $"{v:0.0}%"))
                + $" | {sweep[i].Average():0.0}% |");
        Console.WriteLine();
    }

    // --- 5. 変種 ---------------------------------------------------------------------
reVariants:
    Console.WriteLine("## 変種（`CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine("採用行の**席か1枚だけ**を差し替えた版を診断のローカルに組む");
    Console.WriteLine("（`gradient` / `aim` / `route` / `dull` と同じ扱い）。");
    Console.WriteLine();

    if (reKubiOnly) goto reKubi;
    Console.WriteLine("### A. ワタ抜き（4体）—— 陽性対照その2");
    Console.WriteLine();
    Console.WriteLine("**4体版が5体版と同じ値なら、その台は飽和していて測定になっていない**");
    Console.WriteLine("（第21期 `swap` の検査）。中央を空けたぶんの体の値段も混ざるので、");
    Console.WriteLine("**符号ではなく「動くかどうか」だけを読む**。");
    Console.WriteLine();

    var baseF = Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
                                center: UnitCatalog.Wata, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    var noWata = Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
                                 back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);

    Console.WriteLine("| 版 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 横取り/戦 | 渡し/戦 | 敵与ダメ/戦 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var (vn, vf) in new[] { ("ワタあり（採用行）", baseF), ("ワタ抜き（4体・中央 空）", noWata) })
    {
        double tk = 0, sn = 0, fo = 0;
        var per = new double[reStages.Count];
        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(vf, reStages[w].Enemy, RelayRule.Default);
            per[w] = z.Win; tk += z.Taken; sn += z.Sent; fo += z.Foe;
        }
        int n = reStages.Count;
        Console.WriteLine($"| {vn} | {per.Average():0.0}% | " + string.Join(" | ", per.Select(v => $"{v:0.0}%"))
            + $" | {tk / n:0.00} | {sn / n:0.00} | {fo / n:0.0} |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    // --- B. 隣接が値段として機能するか（受け入れ基準8）--------------------------------
    Console.WriteLine("### B. 名指しした駒（ドルガ）を隣に置くか（受け入れ基準8）");
    Console.WriteLine();
    Console.WriteLine("**ワタの席は固定（前1）で、ドルガの席だけを動かす。** 前1の隣接は");
    Console.WriteLine("`{中央, 後1}` なので、ドルガを後1に置けば隣接・後3に置けば非隣接になる");
    Console.WriteLine("（`AdjacencyTable` 参照）。**動く変数はドルガとドハの入れ替え1つだけ。**");
    Console.WriteLine();
    Console.WriteLine("ドルガはロスター最高攻撃力（38）で、しかも**2ターンに1回しか動けない**");
    Console.WriteLine("＝1回の振りの価値が2倍。**「隣に置く価値のある駒」の条件に最も近い**");
    Console.WriteLine("——第42期の集約はこれを先に決めていなかったので、隣接がコストにしかならなかった。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 横取り/戦 | 渡し/戦 | 敵与ダメ/戦 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var adj = Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Gald,
                              center: UnitCatalog.Nono, back1: UnitCatalog.Dolga, back3: UnitCatalog.Doha);
    var far = Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Gald,
                              center: UnitCatalog.Nono, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    foreach (var (vn, vf) in new[] { ("隣接（ワタ前1 / ドルガ後1）", adj), ("非隣接（ワタ前1 / ドルガ後3）", far) })
    {
        double tk = 0, sn = 0, fo = 0;
        var per = new double[reStages.Count];
        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(vf, reStages[w].Enemy, RelayRule.Default);
            per[w] = z.Win; tk += z.Taken; sn += z.Sent; fo += z.Foe;
        }
        int n = reStages.Count;
        Console.WriteLine($"| {vn} | {per.Average():0.0}% | " + string.Join(" | ", per.Select(v => $"{v:0.0}%"))
            + $" | {tk / n:0.00} | {sn / n:0.00} | {fo / n:0.0} |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    // --- C. 萎縮（クビ）との同居（予測4）---------------------------------------------
reKubi:
    Console.WriteLine("### C. 萎縮（クビ）との同居 —— 開戦時1回・1体につき 9（予測4）");
    Console.WriteLine();
    Console.WriteLine("採用行のノノをクビに差し替える。萎縮は**開戦時1回・味方1体につき 9**なので、");
    Console.WriteLine("中央（隣接次数4）のワタは1ターン目に 4体ぶん = 36 を横取りし、代金は 72。");
    Console.WriteLine("**HP84 に対して 86%。** 角（次数2）なら 18 / 代金 36。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 横取り/戦 | 代金/戦 | 自弁率 | 渡し/戦 | 攻ゼロ/戦 | 早逝(T/率) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var kubiMid = Formation.Build(front1: UnitCatalog.Kubi, front3: UnitCatalog.Gald,
                                  center: UnitCatalog.Wata, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    var kubiCorner = Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Gald,
                                     center: UnitCatalog.Kubi, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga);
    foreach (var (vn, vf) in new[] { ("クビ同居（ワタ中央・次数4）", kubiMid), ("クビ同居（ワタ前1・次数2）", kubiCorner) })
    {
        double tk = 0, cs = 0, sp = 0, sn = 0, zr = 0, de = 0, dd = 0;
        var per = new double[reStages.Count];
        for (int w = 0; w < reStages.Count; w++)
        {
            var z = MeasureRelay(vf, reStages[w].Enemy, RelayRule.Default);
            per[w] = z.Win; tk += z.Taken; cs += z.Cost; sp += z.SelfPaid;
            sn += z.Sent; zr += z.Zeroed; de += z.Death; dd += z.Died;
        }
        int n = reStages.Count;
        Console.WriteLine($"| {vn} | {per.Average():0.0}% | " + string.Join(" | ", per.Select(v => $"{v:0.0}%"))
            + $" | {tk / n:0.00} | {cs / n:0.00} "
            + $"| {(cs > 0 ? $"{sp * 100 / cs:0.0}%" : "—")} | {sn / n:0.00} | {zr / n:0.00} "
            + $"| {de / n:0.0} / {dd * 100 / n:0.0}% |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}

// 誹り（第44期）を測る。**敵から味方へ弱体を撒く初めての経路**で、窓口は第42期の
// BattleContext.Dull。撒いた量そのものではなく、**そのうち何割が読み手（ウケ＝アーマー化 /
// ワタ＝敵へ転嫁）に届いたか**で読む——第42期が「生成したアーマー」ではなく「実際に吸った量」で、
// 第43期が「流した量」ではなく「効き」で判断したのと同じ理由。**撒いた量は成果ではない。**
//
// **「早逝」が必須。** 保持者は前列（第二波・前3）なので早く落ちる。供給の総量は
// 保持者の生存ターン数で決まるので、これを見ないと「弱すぎる」と「早く死んでいる」が切り分かない。
//
// **陽性対照は SlanderRule(0)。** 誹りが 0.00 になること自体が計数の検算で、
// 同時にその行の「差し替え前の値」（＝Phase 0 の基準）になる。
if (focusId == "slander")
{
    var slBuilds = CompareBuilds();
    const int SlSeeds = 200;   // compare / spread / shove / dull / relay と同じ。balance.md と突き合わせる
    const int SlWave = 1;      // 第二波（0 起算）。誹りの保持者はここにしかいない

    string slFilter = args.Length > 2 ? args[2] : "";
    var slTargets = slBuilds
        .Where(b => slFilter.Length == 0 || slFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // **波は診断のローカルに組む**（gradient / aim / guard と同じ扱い）。
    // 誹りは測って採用しなかったので `Stages` には載っていない——載せ替えずに測れるようにしてある。
    //
    // 差し替え版（前3 = Slanderer）と、差し替え前（前3 = KnightG）。
    // **2体は数値・型・速さが1つも違わない**ので、差分は誹り1つに閉じる。
    Formation SlSlanderWave2() => Formation.Build(
        front1: EnemyCatalog.KnightG, front3: EnemyCatalog.Slanderer, center: EnemyCatalog.Husher,
        back1: EnemyCatalog.Almoner, back3: EnemyCatalog.ArcherG);

    Formation SlPlainWave2() => Formation.Build(
        front1: EnemyCatalog.KnightG, front3: EnemyCatalog.KnightG, center: EnemyCatalog.Husher,
        back1: EnemyCatalog.Almoner, back3: EnemyCatalog.ArcherG);

    Formation slWave2 = SlSlanderWave2();

    // 弱体の読み手（ウツ＝逆しま / ウケ＝集約 / ワタ＝渡し）を1枚でも含む行か。
    static bool SlHasReader(Formation f) => f.Occupied().Any(o =>
        o.Def.Traits.Contains(TraitId.Perverse) || o.Def.Traits.Contains(TraitId.Bear)
        || o.Def.Traits.Contains(TraitId.Relay));

    int slRouteIdx = (int)DullRoute.Slander;

    (double Win, double Fired, double Total, double Taken, double Passed, double Zeroed,
     double Death, double Died, double Turns, double Foe, double Swings,
     Dictionary<string, double> To, Dictionary<string, double> Zero, Dictionary<string, double> RelayTo)
    MeasureSlander(Formation f, Formation enemy, SlanderRule rule, int seed0 = 0, int seedN = SlSeeds)
    {
        var to = new Dictionary<string, double>();
        var zero = new Dictionary<string, double>();
        var rto = new Dictionary<string, double>();
        double win = 0, fired = 0, total = 0, taken = 0, passed = 0, zeroed = 0;
        double death = 0, died = 0, turns = 0, foe = 0, swings = 0;

        for (int seed = seed0; seed < seed0 + seedN; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                                    null, null, null, null, null, null, null, null, rule);
            if (r.PlayerWon) win++;
            fired += r.SlanderFired; total += r.SlanderTotal;
            taken += r.DullTakenByRoute[slRouteIdx];
            passed += r.DullByRoute[slRouteIdx] - r.DullTakenByRoute[slRouteIdx];
            zeroed += r.DullZeroed; turns += r.Turns;

            foreach (var kv in r.SlanderTo)
                to[kv.Key] = to.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in r.DullZeroedWho)
                zero[kv.Key] = zero.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in r.RelayTo)
                rto[kv.Key] = rto.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

            // 敵が味方に与えたダメージと、敵が振った回数（第二波の攻撃回数＝誹りの発火の母数）
            foreach ((int _, UnitDef d) in enemy.Occupied())
                if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t))
                { foe += t.DamageToEnemy; swings += t.Attacks; }

            // 早逝: 誹りの保持者が倒れた試行の、倒れたターン（UnitTally.LastActiveTurn は
            // 死亡時にその手番のターンで上書きされる）。倒れなかった試行は分母に入れない。
            if (r.TallyByUnit.TryGetValue(EnemyCatalog.Slanderer.Id, out UnitTally? st) && st.Deaths > 0)
            { died++; death += st.LastActiveTurn; }
        }

        double n = seedN, md = Math.Max(1, died);
        foreach (string k in to.Keys.ToList()) to[k] /= n;
        foreach (string k in zero.Keys.ToList()) zero[k] /= n;
        foreach (string k in rto.Keys.ToList()) rto[k] /= n;
        return (win * 100 / n, fired / n, total / n, taken / n, passed / n, zeroed / n,
                death / md, died / n, turns / n, foe / n, swings / n, to, zero, rto);
    }

    // 逆しま持ちの CurrentAttack（StatSnapshot。ターン頭の値）。**verbose が要るので別立て**。
    (double Open, double Max, double Last, double Seen) MeasureUtsu(Formation f, Formation enemy, SlanderRule rule)
    {
        int readerId = -1;
        for (int i = 0, k = 0; i < FormationRules.PlayableSlotCount; i++)
            if (f[i] is { } d) { if (d.Traits.Contains(TraitId.Perverse)) readerId = k; k++; }
        if (readerId < 0) return (0, 0, 0, 0);

        double open = 0, max = 0, last = 0, seen = 0;
        for (int seed = 0; seed < SlSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: true,
                                    null, null, null, null, null, null, null, null, rule);
            var snaps = r.Events
                .Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == readerId)
                .Select(e => e.Amount).ToList();
            if (snaps.Count > 0) { open += snaps[0]; max += snaps.Max(); last += snaps[^1]; seen++; }
        }
        double m = Math.Max(1, seen);
        return (open / m, max / m, last / m, seen);
    }

    Console.WriteLine("# 誹り（slander）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 slander [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{SlSeeds - 1}。数字は**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`CompareBuilds()` / `Stages` / `Columns` は触っていない。");
    Console.WriteLine("**測るのは第二波だけ**——誹りの保持者（誹りの巡礼騎士・前3）はそこにしかいない。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 誹り | 発火回数/戦（`SlanderFired`）と撒いた総量/戦（`SlanderTotal`） |");
    Console.WriteLine("| 横取り | ウケ／ワタが横取りした量/戦（`DullTakenByRoute[Slander]`）＝**敵の供給が資産に変わった量** |");
    Console.WriteLine("| 素通り | 読み手に届かずただの損になった量/戦（`DullByRoute[Slander]` − 横取り） |");
    Console.WriteLine("| 攻ゼロ | 味方の `CurrentAttack` が 0 になった回数/戦（**崖の検算**・全経路） |");
    Console.WriteLine("| 早逝 | 誹りの保持者が倒れたターン（倒れた試行の平均）と、倒れた試行の割合 |");
    Console.WriteLine("| 敵振り | 敵5体が攻撃を振った回数/戦（`UnitTally.Attacks` の和）＝**誹りの発火の母数** |");
    Console.WriteLine();
    Console.WriteLine("**撒いた量は成果ではない。** 採否は「横取り ÷（横取り + 素通り）」で読む。");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）--------------------------------------------------------
    Console.WriteLine("## 0. 検算 —— 差分は規則だけに閉じているか");
    Console.WriteLine();
    {
        Formation plain = SlPlainWave2();
        int cells = 0, diffA = 0, diffB = 0;
        foreach (var b in slBuilds)
        {
            int a = 0, c = 0, d = 0;
            for (int seed = 0; seed < SlSeeds; seed++)
            {
                // A: 誹りの保持者を置いた波 × SlanderRule(0)
                if (BattleEngine.Run(b.F, slWave2, seed, false,
                        null, null, null, null, null, null, null, null, new SlanderRule(0)).PlayerWon) a++;
                // B: 前3 を KnightG に戻した波 ×
                //    SlanderRule(0)（＝出荷している Stages[1] そのもの）
                if (BattleEngine.Run(b.F, EnemyCatalog.Stages[SlWave].Enemy, seed, false,
                        null, null, null, null, null, null, null, null, new SlanderRule(0)).PlayerWon) c++;
                // C: 前3 を KnightG に戻した波 × SlanderRule(3)（規則を有効にしても保持者がいない）
                if (BattleEngine.Run(b.F, plain, seed, false,
                        null, null, null, null, null, null, null, null, new SlanderRule(3)).PlayerWon) d++;
            }
            cells++;
            if (a != c) diffA++;
            if (c != d) diffB++;
        }
        Console.WriteLine($"- **基準1**（保持者を置いた波 × `SlanderRule(0)` ⇔ 出荷している `Stages[1]`）: "
            + $"**{cells} セル中 {diffA} 件の食い違い**（{cells} 行 × 第二波）");
        Console.WriteLine($"- **基準2**（`KnightG` に戻すと規則を有効にしても元へ戻る）: **{cells} セル中 {diffB} 件の食い違い**");
        Console.WriteLine();
        Console.Out.Flush();
    }

    int[] slPens = { 0, 1, 2, 3 };
    const int SlMain = 2;   // 主表に使う Penalty

    // --- 1. 陽性対照と主表 ------------------------------------------------------------------
    Console.WriteLine($"## 1. 全 {slBuilds.Length} 行 × 第二波（陽性対照 `SlanderRule(0)` と 主表 `Penalty = {SlMain}`）");
    Console.WriteLine();
    Console.WriteLine("`読` = 弱体の読み手（ウツ／ウケ／ワタ）を持つ行。");
    Console.WriteLine();
    Console.WriteLine($"| 行 | 読 | 勝率(P0) | 勝率(P{SlMain}) | 差 | 誹り | 総量 | 横取り | 素通り | 攻ゼロ(P0→P{SlMain}) | 早逝(T/率) | 敵振り | 決着T(P0→P{SlMain}) |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

    var slAggTo = new Dictionary<string, double>();
    var slAggZero = new Dictionary<string, double>();
    var slAggZero0 = new Dictionary<string, double>();
    double slPassReader = 0, slPassPlain = 0; int slNReader = 0, slNPlain = 0;
    double slDeltaReader = 0, slDeltaPlain = 0;

    foreach (var b in slBuilds)
    {
        var z0 = MeasureSlander(b.F, slWave2, new SlanderRule(0));
        var z = MeasureSlander(b.F, slWave2, new SlanderRule(SlMain));
        bool reader = SlHasReader(b.F);

        foreach (var kv in z.To)
            slAggTo[kv.Key] = slAggTo.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
        foreach (var kv in z.Zero)
            slAggZero[kv.Key] = slAggZero.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

        foreach (var kv in z0.Zero)
            slAggZero0[kv.Key] = slAggZero0.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;

        if (reader) { slPassReader += z.Passed; slNReader++; slDeltaReader += z.Win - z0.Win; }
        else { slPassPlain += z.Passed; slNPlain++; slDeltaPlain += z.Win - z0.Win; }

        Console.WriteLine($"| {b.Name} | {(reader ? "○" : "")} | {z0.Win:0.0}% | {z.Win:0.0}% "
            + $"| {z.Win - z0.Win:+0.0;-0.0;±0.0} | {z.Fired:0.00} | {z.Total:0.0} | {z.Taken:0.00} "
            + $"| {z.Passed:0.00} | {z0.Zeroed:0.00} → {z.Zeroed:0.00} | {z.Death:0.0} / {z.Died * 100:0.0}% "
            + $"| {z0.Swings:0.0} | {z0.Turns:0.0} → {z.Turns:0.0} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine($"読み手あり **{slNReader} 行**: 平均 {slDeltaReader / Math.Max(1, slNReader):+0.00;-0.00;±0.00} pt"
        + $" / 素通り {slPassReader / Math.Max(1, slNReader):0.00} per 戦");
    Console.WriteLine($"読み手なし **{slNPlain} 行**: 平均 {slDeltaPlain / Math.Max(1, slNPlain):+0.00;-0.00;±0.00} pt"
        + $" / **目減り（素通り）{slPassPlain / Math.Max(1, slNPlain):0.00} per 戦**");
    Console.WriteLine();

    // --- 2. 誰が誹られたか ------------------------------------------------------------------
    Console.WriteLine($"## 2. 誰が誹られたか（`Penalty = {SlMain}` ・全 {slBuilds.Length} 行の合計 / 戦）");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 量/戦（全行合計） |");
    Console.WriteLine("|---|--:|");
    foreach (var kv in slAggTo.OrderByDescending(k => k.Value).Take(12))
        Console.WriteLine($"| {kv.Key} | {kv.Value:0.00} |");
    Console.WriteLine();

    Console.WriteLine("### 攻ゼロの内訳（崖の検算・受け入れ基準4）");
    Console.WriteLine();
    Console.WriteLine("**帰属は P0 との差で取る**——`攻ゼロ` は窓口を通る全経路を数えるので、");
    Console.WriteLine("呪詛・萎縮・なまりのぶんが P0 の側に既に載っている。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 素の攻 | P0 | P" + SlMain + " | 差（＝誹りに帰属） |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    if (slAggZero.Count == 0) Console.WriteLine("| （0 件） | — | 0.00 | 0.00 | ±0.00 |");
    foreach (var kv in slAggZero.OrderByDescending(k => k.Value - (slAggZero0.TryGetValue(k.Key, out double q) ? q : 0)).Take(15))
    {
        var def = UnitCatalog.All.FirstOrDefault(u => u.Name == kv.Key);
        double b0 = slAggZero0.TryGetValue(kv.Key, out double p) ? p : 0;
        Console.WriteLine($"| {kv.Key} | {(def is null ? "—" : def.Attack.ToString())} "
            + $"| {b0:0.00} | {kv.Value:0.00} | {kv.Value - b0:+0.00;-0.00;±0.00} |");
    }
    Console.WriteLine();

    // --- 3. 読み手を持つ行の詳細（ウツ攻 / ワタの流し先）-------------------------------------
    Console.WriteLine("## 3. 弱体の読み手を持つ行の詳細");
    Console.WriteLine();
    Console.WriteLine("`ウツ攻` は逆しま持ちの `CurrentAttack`（開戦時 / 最大 / 最終T）。`StatSnapshot` から。");
    Console.WriteLine("**開戦時 < 最大 が「敵の供給が読み手に届いた」の直接の証拠。**");
    Console.WriteLine();
    Console.WriteLine($"| 行 | 勝率(P0→P{SlMain}) | 誹り | 横取り | 素通り | 横取り率 | ウツ攻(P0) | ウツ攻(P{SlMain}) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    var slRelayNotes = new List<string>();
    foreach (var b in slBuilds.Where(x => SlHasReader(x.F)))
    {
        var z0 = MeasureSlander(b.F, slWave2, new SlanderRule(0));
        var z = MeasureSlander(b.F, slWave2, new SlanderRule(SlMain));
        var u0 = MeasureUtsu(b.F, slWave2, new SlanderRule(0));
        var u1 = MeasureUtsu(b.F, slWave2, new SlanderRule(SlMain));
        double got = z.Taken + z.Passed;
        Console.WriteLine($"| {b.Name} | {z0.Win:0.0}% → {z.Win:0.0}% | {z.Fired:0.00} | {z.Taken:0.00} "
            + $"| {z.Passed:0.00} | {(got > 0 ? $"{z.Taken * 100 / got:0.0}%" : "—")} "
            + $"| {(u0.Seen > 0 ? $"{u0.Open:0.0} / {u0.Max:0.0} / {u0.Last:0.0}" : "—")} "
            + $"| {(u1.Seen > 0 ? $"{u1.Open:0.0} / {u1.Max:0.0} / {u1.Last:0.0}" : "—")} |");
        Console.Out.Flush();

        if (z.RelayTo.Count > 0 || z0.RelayTo.Count > 0)
        {
            slRelayNotes.Add($"- **{b.Name}** P0 の流し先: "
                + (z0.RelayTo.Count == 0 ? "（なし）" : string.Join(" / ", z0.RelayTo.OrderByDescending(k => k.Value)
                    .Select(k => $"{k.Key} {k.Value:0.00}"))));
            slRelayNotes.Add($"- **{b.Name}** P{SlMain} の流し先: "
                + (z.RelayTo.Count == 0 ? "（なし）" : string.Join(" / ", z.RelayTo.OrderByDescending(k => k.Value)
                    .Select(k => $"{k.Key} {k.Value:0.00}"))));
        }
    }
    Console.WriteLine();
    if (slRelayNotes.Count > 0)
    {
        Console.WriteLine("### ワタの流し先（**撒いた呪いが本人に返るか**）");
        Console.WriteLine();
        Console.WriteLine("第二波の最高攻撃力は `KnightG` / `Slanderer` の 24 なので、");
        Console.WriteLine("**誹りの保持者自身に返る可能性がある**（設計としては望ましい）。");
        Console.WriteLine();
        foreach (string note in slRelayNotes) Console.WriteLine(note);
        Console.WriteLine();
    }

    // --- 4. 掃引 ----------------------------------------------------------------------------
    Console.WriteLine("## 4. 掃引（`Penalty` 0 / 1 / 2 / 3）");
    Console.WriteLine();
    Console.WriteLine("**見るのは勝率ではなく「横取り／素通り」の比と、第二波の情報セル。**");
    Console.WriteLine("第41期の掃引は「比が全点で 2.30 で動かない＝ノブは強度しか変えず性質を変えない」を示した。");
    Console.WriteLine("同じことが起きるかを先に疑う。");
    Console.WriteLine();
    Console.WriteLine("| Penalty | 平均(全行) | 主判定17行 | 情報セル | 100%行 | 0%行 | 誹り | 総量 | 横取り | 素通り | 横取り率 | 攻ゼロ |");
    Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

    // 主判定17行（design/HANDOFF_NEXT_SESSION.md §4-4）。行集合を固定した歯止めの分母。
    string[] slMainKeys =
    {
        "隊列崩し (バサ×ヨミ×セロ)", "燃焼 (ボルグ×ホタ)", "縛め収入型 (クグ×バン×ガン)",
        "仇討ち×砕け (ヒビ×ザン)", "刻み×抉り (ノミ×エグ)", "裂き (キリ×エグ)",
        "耐久 (ガルド×ノノ)", "溜め改 (クグ×バン×ガン)", "逆しま (ネル×ウツ)",
        "追撃×据え (ハギ×バン)", "置き去り×分散回復", "毒+耐久 (ベニ×トウ)",
        "速攻 (ボルグ×ムド)", "反撃改2 (ガン×カド)", "惨禍×死の連鎖",
        "後衛特化+後備え", "突き出し (セロ×ヨミ)"
    };

    var slSweep = new Dictionary<int, Dictionary<string, double>>();
    foreach (int pen in slPens)
    {
        var wins = new Dictionary<string, double>();
        double fired = 0, total = 0, taken = 0, passed = 0, zeroed = 0;
        foreach (var b in slBuilds)
        {
            var z = MeasureSlander(b.F, slWave2, new SlanderRule(pen));
            wins[b.Name] = z.Win;
            fired += z.Fired; total += z.Total; taken += z.Taken; passed += z.Passed; zeroed += z.Zeroed;
        }
        slSweep[pen] = wins;
        double n = slBuilds.Length;
        var col = wins.Values.ToList();
        double got = taken + passed;
        Console.WriteLine($"| {pen} | {col.Average():0.00} "
            + $"| {slMainKeys.Average(k => wins[k]):0.00} "
            + $"| {col.Count(x => x > 0 && x < 100)} | {col.Count(x => x >= 100)} | {col.Count(x => x <= 0)} "
            + $"| {fired / n:0.00} | {total / n:0.0} | {taken / n:0.00} | {passed / n:0.00} "
            + $"| {(got > 0 ? $"{taken * 100 / got:0.0}%" : "—")} | {zeroed / n:0.00} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine("**歯止め**: 主判定17行の第二波は `Penalty = 0` の値から 10pt 以上落ちてはいけない。");
    Console.WriteLine();

    // --- 5. 符号反転（受け入れ基準6）--------------------------------------------------------
    Console.WriteLine("## 5. 符号反転（受け入れ基準6・**この期の主眼**）");
    Console.WriteLine();
    Console.WriteLine($"`Penalty = {SlMain}` で第二波の勝率が動いた行。**上がる行と下がる行が両方あること**が条件。");
    Console.WriteLine();
    Console.WriteLine($"| 行 | 読 | P0 | P{SlMain} | 差 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|");
    var slMoved = slBuilds
        .Select(b => (b.Name, Reader: SlHasReader(b.F), A: slSweep[0][b.Name], B: slSweep[SlMain][b.Name]))
        .Where(x => Math.Abs(x.B - x.A) > 0.001)
        .OrderByDescending(x => x.B - x.A).ToList();
    foreach (var x in slMoved)
        Console.WriteLine($"| {x.Name} | {(x.Reader ? "○" : "")} | {x.A:0.0}% | {x.B:0.0}% | {x.B - x.A:+0.0;-0.0;±0.0} |");
    Console.WriteLine();
    Console.WriteLine($"上がった行 **{slMoved.Count(x => x.B > x.A)}** / 下がった行 **{slMoved.Count(x => x.B < x.A)}**"
        + $" / 動かなかった行 **{slBuilds.Length - slMoved.Count}**。");
    Console.WriteLine();


    // --- 6. 別 seed での追試（confirm）------------------------------------------------------
    // **選定に使っていない seed に当てて採否を決める**（CLAUDE.md の reseat → confirm と同じ作法）。
    // 対象は「読み手を持つ7行」と「主表で 1.0pt 以上動いた行」。+1.0pt が 200 試行のうち2件でしかない
    // 以上、seed 帯を変えても符号が保つかを見ないと符号反転を主張できない。
    Console.WriteLine("## 6. 別 seed での追試（seed 200..599 の 400 試行）");
    Console.WriteLine();
    Console.WriteLine("**選定に使っていない seed 帯**。主表（seed 0..199）で1セルでも動いた行と、");
    Console.WriteLine("読み手を持つ7行が対象。**符号が保つかどうかだけを見る。**");
    Console.WriteLine();
    Console.WriteLine($"| 行 | 読 | 主表 P0→P{SlMain} | 主表 差 | 追試 P0→P{SlMain} | 追試 差 | 符号 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|:-:|");
    foreach (var b in slBuilds)
    {
        bool reader = SlHasReader(b.F);
        double a = slSweep[0][b.Name], c = slSweep[SlMain][b.Name];
        if (!reader && Math.Abs(c - a) < 0.001) continue;
        var q0 = MeasureSlander(b.F, slWave2, new SlanderRule(0), 200, 400);
        var q1 = MeasureSlander(b.F, slWave2, new SlanderRule(SlMain), 200, 400);
        double d1 = c - a, d2 = q1.Win - q0.Win;
        string sign = Math.Abs(d1) < 0.001 && Math.Abs(d2) < 0.001 ? "—"
            : (d1 > 0) == (d2 > 0) && Math.Abs(d2) > 0.001 ? "保つ" : "**割れる**";
        Console.WriteLine($"| {b.Name} | {(reader ? "○" : "")} | {a:0.0}% → {c:0.0}% | {d1:+0.0;-0.0;±0.0} "
            + $"| {q0.Win:0.0}% → {q1.Win:0.0}% | {d2:+0.0;-0.0;±0.0} | {sign} |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    if (slTargets.Length > 0 && slFilter.Length > 0)
    {
        Console.WriteLine($"## 7. 絞り込み `{slFilter}` の掃引（第二波）");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", slPens.Select(p => $"P{p}")) + " |");
        Console.WriteLine("|---|" + string.Join("|", slPens.Select(_ => "--:")) + "|");
        foreach (var b in slTargets)
            Console.WriteLine($"| {b.Name} | " + string.Join(" | ", slPens.Select(p => $"{slSweep[p][b.Name]:0.0}%")) + " |");
        Console.WriteLine();
    }
    return;
}

// overbear モード: 驕り（第46期）。**隣接を「量」ではなく「誰がいるか」で読む駒**を測る。
//
// 第45期の結論は「隣接を単調な量（隣に何人いるか）で読む駒は席が定数になる」で、
// 反例はロスターに1枚しかない非単調な読み手（囃し立てのヒサ）だけだった（n=1）。
// オゴは条件を隣接する味方**全員**への AND にするので、
//
//     隣接数が増えるほど条件が厳しくなる（中央は削る量が多い代わりに成立が遅い）
//     さらに「隣が誰か」で成立時刻が変わる（ドルガ38 の隣なら13ターン、ムグ6 の隣なら即座）
//
// **この期の核心は「成立時刻」**で、席（隣接数）と相方（隣の攻撃力）の両方で変わるはず。
// 変わらなければ非単調にした意味が無い。
//
// **陽性対照を2本置く**: OverbearRule(0)（削らない＝条件が永遠に成立しない＝プラス側だけを切る）と、
// オゴを外した4体版（＝第21期の飽和検査も兼ねる）。
// **発火しなかったことは盤面の値に痕跡を残さない**ので、計数は BattleResult の
// Overbear*（verbose に依存しない）から取る。
//
//     dotnet run --project BattleSim -c Release 0 overbear [絞り込み]
if (focusId == "overbear")
{
    var obBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> obStages = EnemyCatalog.Stages;
    const int ObSeeds = 200;   // compare / spread / shove / dull / relay / slander と揃える
    const int ObMain = 2;      // 主表に使う Drain（採用値）
    int[] obDrains = { 0, 1, 2, 3 };
    int obRouteIdx = (int)DullRoute.Overbear;

    string obFilter = args.Length > 2 ? args[2] : "";

    static bool ObHasOgo(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Ogo));

    // **測定した2編成は診断のローカルに持つ**（gradient / aim / guard / slander と同じ扱い）。
    // 第46期は驕りを**採用しなかった**ので `CompareBuilds()` には残していない
    // ——測ったときは 50 行に足して回し、棄却と同時に外した。ここに残しておけば、
    // `CompareBuilds()` を1行も動かさずに全部を測り直せる。
    //
    // 2行は「条件が早く成立する台」と「遅く成立する台」の対。律速は隣でいちばん攻撃力の高い1枚。
    //
    //   早い台 = 死の連鎖行の ゴルム(10)→ウケ・ヴェル(6)→オゴ。開幕の隣の最高は 6 で**どの席でも成立する**
    //   遅い台 = 分かち×逆しま行の ノノ(3) → オゴ。**ドルガ 38 と ウツ（削るほど育つ）が隣にいると成立しない**
    //
    // **早い台は「攻撃力の低い駒だけ」では組めなかった**（測った5通りが全部 20.0〜23.2% ＝
    // CLAUDE.md の「台が死んでいる」）——ロスターでは弱体・毒・支援の駒が軒並み攻2〜9 で、
    // 攻撃力と出力が強く相関している。抜け道は**攻撃力を経由しない出力**で、
    // 死の連鎖（破裂・胞子・墓守）がそれに当たる。
    static (string Name, Formation F)[] ObRows() => new (string, Formation)[]
    {
        ("驕り (オゴ×ウケ)", Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Mug,
                                        center: UnitCatalog.Uke, back1: UnitCatalog.Rica,
                                        back3: UnitCatalog.Ogo)),
        ("驕り改 (オゴ×ウツ)", Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald,
                                          center: UnitCatalog.Ogo, back1: UnitCatalog.Doha,
                                          back3: UnitCatalog.Dolga)),
    };

    var obTargets = ObRows();
    if (obFilter.Length > 0)
        obTargets = obTargets.Where(b => obFilter.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // オゴを外した同じ編成（4体版）。**第21期の飽和検査を兼ねる**
    // ——4体版と5体版が同じ値なら、その台では中央の駒が何であっても結果が変わらない。
    static Formation ObWithoutOgo(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Ogo)) g[slot] = d;
        return g;
    }

    // オゴの席だけを振った5変種。**他の4枚は元の相対順のまま**空いた席へ詰める
    // （reseat の120通りと違い、動かすのはオゴ1枚だけ＝席の効果を1変数に閉じる）。
    static Formation ObSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Ogo))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Ogo;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    (double Win, double Fired, double Total, double Taken, double Passed, double Zeroed,
     double MetRate, double First, double Never, double Doubled, double Swings,
     double Back, double BackHits, double Turns,
     Dictionary<string, double> To, Dictionary<string, double> Zero)
    MeasureOverbear(Formation f, Formation enemy, OverbearRule rule, int seed0 = 0, int seedN = ObSeeds)
    {
        var to = new Dictionary<string, double>();
        var zero = new Dictionary<string, double>();
        double win = 0, fired = 0, total = 0, taken = 0, passed = 0, zeroed = 0;
        double met = 0, obTurns = 0, first = 0, firstN = 0, never = 0;
        double doubled = 0, swings = 0, back = 0, backHits = 0, turns = 0;

        for (int seed = seed0; seed < seed0 + seedN; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                                    null, null, null, null, null, null, null, null, null, rule);
            if (r.PlayerWon) win++;
            fired += r.OverbearFired; total += r.OverbearTotal;
            taken += r.DullTakenByRoute[obRouteIdx];
            passed += r.DullByRoute[obRouteIdx] - r.DullTakenByRoute[obRouteIdx];
            zeroed += r.DullZeroed;
            met += r.OverbearMetTurns; obTurns += r.OverbearTurns;
            if (r.OverbearFirstTurn > 0) { first += r.OverbearFirstTurn; firstN++; } else never++;
            doubled += r.OverbearDoubled; swings += r.OverbearSwings;
            back += r.OverbearBackfire; backHits += r.OverbearBackfireHits;
            turns += r.Turns;

            foreach (var kv in r.OverbearTo)
                to[kv.Key] = to.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in r.DullZeroedWho)
                zero[kv.Key] = zero.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
        }

        double n = seedN;
        foreach (string k in to.Keys.ToList()) to[k] /= n;
        foreach (string k in zero.Keys.ToList()) zero[k] /= n;
        return (win * 100 / n, fired / n, total / n, taken / n, passed / n, zeroed / n,
                obTurns > 0 ? met * 100 / obTurns : 0, firstN > 0 ? first / firstN : 0, never * 100 / n,
                doubled / n, swings / n, back / n, backHits / n, turns / n, to, zero);
    }

    // 全波を通した集計（機構の量は波で平均する。勝率だけは波ごとに出す）
    (double[] Wins, double Fired, double Total, double Taken, double Passed, double Zeroed,
     double MetRate, double First, double Never, double Doubled, double Swings,
     double Back, double BackHits, Dictionary<string, double> To, Dictionary<string, double> Zero)
    MeasureAll(Formation f, OverbearRule rule)
    {
        var wins = new double[obStages.Count];
        var to = new Dictionary<string, double>();
        var zero = new Dictionary<string, double>();
        double fired = 0, total = 0, taken = 0, passed = 0, zeroed = 0, met = 0, first = 0, firstN = 0;
        double never = 0, doubled = 0, swings = 0, back = 0, backHits = 0;
        for (int w = 0; w < obStages.Count; w++)
        {
            var z = MeasureOverbear(f, obStages[w].Enemy, rule);
            wins[w] = z.Win;
            fired += z.Fired; total += z.Total; taken += z.Taken; passed += z.Passed; zeroed += z.Zeroed;
            met += z.MetRate; never += z.Never; doubled += z.Doubled; swings += z.Swings;
            back += z.Back; backHits += z.BackHits;
            if (z.First > 0) { first += z.First; firstN++; }
            foreach (var kv in z.To) to[kv.Key] = to.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in z.Zero) zero[kv.Key] = zero.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
        }
        double m = obStages.Count;
        foreach (string k in to.Keys.ToList()) to[k] /= m;
        foreach (string k in zero.Keys.ToList()) zero[k] /= m;
        return (wins, fired / m, total / m, taken / m, passed / m, zeroed / m, met / m,
                firstN > 0 ? first / firstN : 0, never / m, doubled / m, swings / m,
                back / m, backHits / m, to, zero);
    }

    static string ObCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));

    Console.WriteLine("# 驕り（overbear）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 overbear [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{ObSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（第45期の残件 C）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 削り | 撒いた総量/戦（`OverbearTotal`）と延べ体数（`OverbearFired`） |");
    Console.WriteLine("| 成立率 | 条件を満たしていたターン数 ÷ 保持者がターン頭を迎えた回数（`MetTurns/Turns`） |");
    Console.WriteLine("| 成立T | **条件が初めて成立したターン**（成立した試行だけの平均）。`未成立` が成立しなかった試行の割合 |");
    Console.WriteLine("| 2倍 | 2倍が乗った攻撃回数/戦（`OverbearDoubled`）と振った回数（`OverbearSwings`） |");
    Console.WriteLine("| 横取り | ウケ／ワタが横取りした量/戦（`DullTakenByRoute[Overbear]`）＝**削りが資産に変わった量** |");
    Console.WriteLine("| 素通り | 読み手に届かずただの損になった量/戦 |");
    Console.WriteLine("| 逆行 | **削ったのに相手の `CurrentAttack` が上がった量**/戦（逆しまの自己矛盾） |");
    Console.WriteLine("| 攻ゼロ | 味方の `CurrentAttack` が 0 になった回数/戦（**崖の検算**・全経路） |");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）--------------------------------------------------------
    Console.WriteLine("## 0. 検算 —— 差分は驕りだけに閉じているか（受け入れ基準1・2）");
    Console.WriteLine();
    {
        var plain = obBuilds.Where(b => !ObHasOgo(b.F)).ToArray();   // 現状は 48 行すべて
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < obStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < ObSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, obStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, new OverbearRule(0)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, obStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, new OverbearRule(9)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（オゴを含まない {plain.Length} 行が `OverbearRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {obStages.Count} 波・`Drain` 0 対 9）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  行を足す前に `compare` の全文 diff で確認する（**240 セル中 0 件**）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 ----------------------------------------------------------------------------
    Console.WriteLine($"## 1. 主表（`Drain = {ObMain}` と 陽性対照2本）");
    Console.WriteLine();
    Console.WriteLine("`D0` = `OverbearRule(0)`（削らない＝条件が永遠に成立しない＝**プラス側だけを切る**）。");
    Console.WriteLine("`4体` = オゴを外した同じ編成（**第21期の飽和検査**も兼ねる。4体版と5体版が同値なら台が測定になっていない）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    var obMain = new Dictionary<string, (double[] Wins, double Fired, double Total, double Taken,
        double Passed, double Zeroed, double MetRate, double First, double Never, double Doubled,
        double Swings, double Back, double BackHits, Dictionary<string, double> To,
        Dictionary<string, double> Zero)>();
    var obZero = new Dictionary<string, (double[] Wins, double Fired, double Total, double Taken,
        double Passed, double Zeroed, double MetRate, double First, double Never, double Doubled,
        double Swings, double Back, double BackHits, Dictionary<string, double> To,
        Dictionary<string, double> Zero)>();

    foreach (var b in obTargets)
    {
        var z = MeasureAll(b.F, new OverbearRule(ObMain));
        var z0 = MeasureAll(b.F, new OverbearRule(0));
        obMain[b.Name] = z;
        obZero[b.Name] = z0;
        var four = new double[obStages.Count];
        for (int w = 0; w < obStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < ObSeeds; seed++)
                if (BattleEngine.Run(ObWithoutOgo(b.F), obStages[w].Enemy, seed, false).PlayerWon) wins++;
            four[w] = wins * 100.0 / ObSeeds;
        }
        Console.WriteLine($"| {b.Name} | **D{ObMain}** |{ObCells(z.Wins)} {z.Wins.Average():0.0}% |");
        Console.WriteLine($"| | D0 |{ObCells(z0.Wins)} {z0.Wins.Average():0.0}% |");
        Console.WriteLine($"| | 4体 |{ObCells(four)} {four.Average():0.0}% |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    Console.WriteLine($"### 機構の計数（`Drain` = {ObMain}・5波の平均）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 削り(量/体) | 成立率 | 成立T | 未成立 | 2倍/振り | 横取り | 素通り | 横取り率 | 逆行(量/回) | 攻ゼロ |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var b in obTargets)
    {
        var z = obMain[b.Name];
        double got = z.Taken + z.Passed;
        Console.WriteLine($"| {b.Name} | {z.Total:0.0} / {z.Fired:0.0} | {z.MetRate:0.0}% "
            + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
            + $"| {z.Doubled:0.00} / {z.Swings:0.00} | {z.Taken:0.00} | {z.Passed:0.00} "
            + $"| {(got > 0 ? $"{z.Taken * 100 / got:0.0}%" : "—")} "
            + $"| {z.Back:0.0} / {z.BackHits:0.00} | {z.Zeroed:0.00} |");
    }
    Console.WriteLine();

    // --- 2. 削った相手と攻ゼロの内訳 --------------------------------------------------------
    Console.WriteLine($"## 2. 誰を削ったか / 誰が攻ゼロになったか（`Drain = {ObMain}`）");
    Console.WriteLine();
    foreach (var b in obTargets)
    {
        var z = obMain[b.Name];
        var z0 = obZero[b.Name];
        Console.WriteLine($"### {b.Name}");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 素の攻 | 削られた量/戦 |");
        Console.WriteLine("|---|--:|--:|");
        foreach (var kv in z.To.OrderByDescending(k => k.Value))
        {
            var def = UnitCatalog.All.FirstOrDefault(u => u.Name == kv.Key);
            Console.WriteLine($"| {kv.Key} | {(def is null ? "—" : def.Attack.ToString())} | {kv.Value:0.00} |");
        }
        Console.WriteLine();
        Console.WriteLine("**攻ゼロの内訳（崖の検算・受け入れ基準4）。** 帰属は D0 との差で取る");
        Console.WriteLine("——`攻ゼロ` は窓口を通る全経路を数えるので、なまり・呪詛・萎縮のぶんが D0 側に載っている。");
        Console.WriteLine();
        Console.WriteLine($"| 駒 | 素の攻 | D0 | D{ObMain} | 差（＝驕りに帰属） |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        if (z.Zero.Count == 0 && z0.Zero.Count == 0) Console.WriteLine("| （0 件） | — | 0.00 | 0.00 | ±0.00 |");
        foreach (var kv in z.Zero.OrderByDescending(k => k.Value))
        {
            var def = UnitCatalog.All.FirstOrDefault(u => u.Name == kv.Key);
            double b0 = z0.Zero.TryGetValue(kv.Key, out double q) ? q : 0;
            Console.WriteLine($"| {kv.Key} | {(def is null ? "—" : def.Attack.ToString())} | {b0:0.00} "
                + $"| {kv.Value:0.00} | {kv.Value - b0:+0.00;-0.00;±0.00} |");
        }
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 3. 成立時刻 × 席（**この期の核心**）------------------------------------------------
    Console.WriteLine("## 3. 成立時刻 × 席（**この期の核心**）");
    Console.WriteLine();
    Console.WriteLine("**オゴ1枚だけを動かす。** 他の4枚は元の相対順のまま空いた席へ詰める");
    Console.WriteLine("（`reseat` の120通りと違い、動かすのは1枚だけ＝席の効果を1変数に閉じる）。");
    Console.WriteLine();
    Console.WriteLine("**席と相方の両方で成立時刻が動かなければ、非単調にした意味が無い。**");
    Console.WriteLine();
    Console.WriteLine("`勝率(D0)` は同じ席配置で `OverbearRule(0)`（削らない＝条件が成立しない）を回した対照。");
    Console.WriteLine("**オゴを動かすと他の4枚の席も動く**ので、対照を置かないと席の差が機構の差かどうか決まらない。");
    Console.WriteLine();
    foreach (var b in obTargets)
    {
        Console.WriteLine($"### {b.Name}");
        Console.WriteLine();
        Console.WriteLine("| オゴの席 | 次数 | 隣接する駒（開幕） | 成立率 | 成立T | 未成立 | 2倍/振り | 削り | 横取り | 逆行 | 勝率(D0) | 勝率(D2) | 差 |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
        {
            Formation g = ObSeat(b.F, seat);
            int deg = 0;
            var names = new List<string>();
            foreach ((int slot, UnitDef d) in g.Occupied())
                if (slot != seat && FormationRules.AreAdjacent(seat, slot)) { deg++; names.Add($"{d.Name}({d.Attack})"); }
            var z = MeasureAll(g, new OverbearRule(ObMain));
            var zc = MeasureAll(g, new OverbearRule(0));   // 席の効果から機構の効果を割る対照
            Console.WriteLine($"| {FormationRules.SeatNames[seat]} | {deg} | {string.Join("・", names)} "
                + $"| {z.MetRate:0.0}% | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                + $"| {z.Doubled:0.00} / {z.Swings:0.00} | {z.Total:0.0} | {z.Taken:0.00} | {z.Back:0.0} "
                + $"| {zc.Wins.Average():0.0}% | {z.Wins.Average():0.0}% "
                + $"| {z.Wins.Average() - zc.Wins.Average():+0.0;-0.0;±0.0} |");
            Console.Out.Flush();
        }
        Console.WriteLine();
    }

    // --- 4. 掃引 ----------------------------------------------------------------------------
    Console.WriteLine("## 4. 掃引（`Drain` 0 / 1 / 2 / 3）");
    Console.WriteLine();
    Console.WriteLine("**見るのは勝率ではなく「成立時刻」と「席」。** `Drain` は成立時刻を直接動かすノブなので、");
    Console.WriteLine("席の選び方も動くはず。動かなければ非単調化が効いていない証拠になる。");
    Console.WriteLine();
    Console.WriteLine("`最適席` は §3 の5変種のうち平均勝率が最大の席（**1位の値ではなく次数を読むこと**・第45期の残件 D）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | Drain | 平均勝率 | 成立率 | 成立T | 未成立 | 2倍/振り | 削り | 横取り | 素通り | 逆行 | 攻ゼロ | 最適席(次数) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
    foreach (var b in obTargets)
    {
        foreach (int d in obDrains)
        {
            var z = MeasureAll(b.F, new OverbearRule(d));
            int best = -1; double bestAvg = double.MinValue;
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                var q = MeasureAll(ObSeat(b.F, seat), new OverbearRule(d));
                double avg = q.Wins.Average();
                if (avg > bestAvg) { bestAvg = avg; best = seat; }
            }
            int deg = 0;
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                if (FormationRules.AreAdjacent(best, i)) deg++;
            Console.WriteLine($"| {b.Name} | {d} | {z.Wins.Average():0.0}% | {z.MetRate:0.0}% "
                + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% | {z.Doubled:0.00} / {z.Swings:0.00} "
                + $"| {z.Total:0.0} | {z.Taken:0.00} | {z.Passed:0.00} | {z.Back:0.0} | {z.Zeroed:0.00} "
                + $"| {FormationRules.SeatNames[best]}({deg}) {bestAvg:0.0}% |");
            Console.Out.Flush();
        }
    }
    Console.WriteLine();

    // --- 5. 席の分散（seats2 の写し・**受け入れ基準6**）--------------------------------------
    // `seats2` は `CompareBuilds()` の行しか見ないので、2行を外した後は同じ数字を出せない。
    // **探索と検証の作り方は `seats2` の写し**（粗探索 seed 0..49 の全120通り →
    // 上位20 + 現行 を seed 0..199 で測り直し）で、ここでローカルの2編成に対して回す。
    Console.WriteLine("## 5. 席の分散（`seats2` の写し・**受け入れ基準6・この期の主眼**）");
    Console.WriteLine();
    Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 を seed 0..199 で測り直し。");
    Console.WriteLine("**採否に使うのは1位の配置ではなく次数**（第45期の残件 D。1位は別 seed 帯で 48行中28行で");
    Console.WriteLine("入れ替わるが、次数の追試一致率は 98%）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（中央/角） | 幅 |");
    Console.WriteLine("|---|---|---|--:|---|--:|");
    foreach (var b in obTargets)
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
        for (int i = 0; i < perms.Count; i++)
        {
            int wins = 0;
            foreach (EnemyCatalog.Stage st in obStages)
                for (int seed = 0; seed < 50; seed++)
                    if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
            scan[i] = wins;
        }
        var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
        int curIdx = order.First(i => SameFormation(perms[i], b.F));
        var pool = order.Take(20).Append(curIdx).Append(order[^1]).Distinct().ToList();

        double Avg(Formation f)
        {
            double avg = 0;
            foreach (EnemyCatalog.Stage st in obStages)
            {
                int wins = 0;
                for (int seed = 0; seed < ObSeeds; seed++)
                    if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                avg += wins * 100.0 / ObSeeds;
            }
            return avg / obStages.Count;
        }

        var verified = pool.Select(i => (Idx: i, Avg: Avg(perms[i]))).OrderByDescending(x => x.Avg).ToList();
        double width = verified[0].Avg - verified[^1].Avg;
        var top5 = verified.Take(5).ToList();

        foreach (UnitDef d in members)
        {
            int bestSlot = -1;
            foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                if (ReferenceEquals(dd, d)) bestSlot = slot;
            int mid = 0, corner = 0;
            foreach (var v in top5)
                foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                    if (ReferenceEquals(dd, d))
                    {
                        int deg2 = 0;
                        for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                            if (FormationRules.AreAdjacent(slot, i)) deg2++;
                        if (deg2 == 4) mid++; else corner++;
                    }
            int bdeg = 0;
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
            Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                + $"| 中央{mid} / 角{corner} | {width:0.0}pt |");
        }
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}

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

// pace モード: **勝率以外の物差しで、波から情報を取り出せるか**（第54期・調査）。
//
// `compare` の5波のうち第一波は 56 行すべてが 100.0% で、情報セルが 0 のまま遊んでいる
// （第51期の実測では、3体のうちどれを空席にしても 56 行すべて 100.0% のまま）。
// しかし決着ターンは 2.0〜6.1 と3倍の幅で散っている——**勝敗という物差しでは何も
// 測れていないが、盤面では明確な差が出ている。**
//
// ここでやるのは物差しの比較だけで、**波は1体も触らない**（`Stages` 差分ゼロ）。
// 既存の `chain` / `pulse` も決着T・残存・被ダメを出しているが、どちらも
// **全ステージ通算**（か駒ごと）なので、第一波の値が通算の中に埋もれて取り出せない。
// この診断の要は「**波ごとに**、複数の物差しを、同じ実行の中で並べる」ことにある。
//
// 群の数の定義は第54期の指示書で測る前に固定した（design/PHASE54_PACE.md §1-1）:
//   A 帯（seed 0..199・compare と同じ）の値で昇順に並べ、位置 k に切れ目を入れるのは
//   **B 帯（seed 200..399・選定に使っていない）でも前半と後半が完全に分離する**ときだけ。
//   群 = 切れ目 + 1。隣接ペアだけの比較にすると1組の逆転で群が増減して不安定になる
//   （第45期の「1位は 28/48 行で入れ替わる」と同じ罠）。
//
// 強化の分布（第56期）。**窓口 `BattleContext.Whet` を通った量だけ**を経路別・受け手別に数える。
//
// 第42期に弱体は `ctx.Dull` へ統一されたが、強化側は15箇所が `AtkBonus` を直に叩いていた
// （第52期 Phase 0-1 の持ち越し）。窓口ができたので、**強化の総量と経路別の内訳が初めて測れる。**
// 窓口そのものは挙動不変（`compare` が 280 セル 0 件で一致）なので、**この診断がこの期の成果。**
//
// **通っているのは他者強化の6本だけ。** 自己強化の9本（怒り・庇う／殉教・墓守2本・処刑・棘・
// 澱み喰い・軋み・分かち）は直叩きのまま残してあるので、ここの「総量」は
// **`AtkBonus` の総流量ではなく「他者から受け取った量」**である。収支も同じ意味。
//
// **渡した量は成果ではない**（第42期以降の作法）。`死蔵`（受け取ったのに一度も振らなかった駒への
// 付与量）を必ず分けて出す。`逆しま` は**受け取ったことがそのまま害になった量**。
//
// **陽性対照**（§7）: 既存のノブで経路を1本ずつ殺し、`whet` がその欠落を検出することを確かめる
// ——**合否は「狙った経路が 0 になったか」の1本だけ。** ノブは計数ではなく盤面を切るので、
// 他の経路がわずかに動くのは故障ではない（決着の長さと生死が変わる）
// ——`ColossusRule(Regurgitate: false)` が吐き戻しを、`GoadRule(0)` が駆り立てを 0 にする。
// 「0 件でした」が検出器の故障と区別できないので、**新しいノブは足さず既存のノブでやる。**
//
//     dotnet run --project BattleSim -c Release 0 whet [絞り込み]
if (focusId == "whet")
{
    var whBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> whStages = EnemyCatalog.Stages;
    const int WhSeeds = 200;   // compare / spread / dull と同じ帯

    string whFilter = args.Length > 2 ? args[2] : "";
    var whTargets = whBuilds
        .Where(b => whFilter.Length == 0 || whFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 駒名の引き当て。TallyByUnit は両陣営を Def.Id で混ぜて持つので、敵側の名前も要る
    // （弱体は呪詛・転嫁で敵にも載る）。召喚（胞子）は Occupied に出ないので、
    // 見つからなければ Id をそのまま出す（**握り潰さない**）。
    var whName = new Dictionary<string, string>();
    foreach (UnitDef d in UnitCatalog.All) whName[d.Id] = d.Name;
    var whEnemyIds = new HashSet<string>();
    foreach (EnemyCatalog.Stage st in whStages)
        foreach ((int _, UnitDef d) in st.Enemy.Occupied()) { whName[d.Id] = d.Name; whEnemyIds.Add(d.Id); }
    string WhLabel(string id) => whName.TryGetValue(id, out string? n) ? n : id;

    // 1戦ぶんを足し込む器。**すべて「延べ量 ÷ 戦数」で割って出す**（量/戦の作法）。
    var whRoute = new double[WhetRoutes.Count];
    double whTotal = 0, whDull = 0, whPerv = 0, whFlip = 0;
    double whHoard = 0;                                    // 死蔵(旧): 受け取ったが Attacks == 0 の駒への付与量
    double whHoardInt = 0;                                 // 参考: Interventions == 0 の駒への付与量
    double whHoardNew = 0;                                 // **死蔵(新)**: AttackReads == 0 の駒への付与量
    var whHoardNewWho = new Dictionary<string, double>();  // 新定義の内訳
    var whGot = new Dictionary<string, double>();          // 受け手ごとの Whetted
    var whLost = new Dictionary<string, double>();         // 受け手ごとの Dulled
    var whHoardWho = new Dictionary<string, double>();     // 死蔵の内訳
    var whRowRoute = new Dictionary<string, double[]>();   // 行ごとの経路別（供給源の地図）
    int whBattles = 0;

    var whSw = System.Diagnostics.Stopwatch.StartNew();
    foreach ((string whRowName, Formation whF) in whTargets)
    {
        var rowRoute = new double[WhetRoutes.Count];
        foreach (EnemyCatalog.Stage st in whStages)
        {
            for (int seed = 0; seed < WhSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(whF, st.Enemy, seed, verbose: false);
                whBattles++;

                whTotal += r.WhetTotal;
                whDull += r.DullTotal;
                whPerv += r.WhetToPerverse;
                whFlip += r.WhetPerverseFlips;
                for (int i = 0; i < WhetRoutes.Count; i++)
                {
                    whRoute[i] += r.WhetByRoute[i];
                    rowRoute[i] += r.WhetByRoute[i];
                }

                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (t.Whetted > 0)
                    {
                        whGot[id] = whGot.TryGetValue(id, out double g) ? g + t.Whetted : t.Whetted;
                        // 死蔵。**振った回数が 0 の駒**への付与量。反撃（棘）は Attacks を通らないので
                        // 「反応型」も死蔵に数えてしまう——内訳を出して読む側で切り分ける。
                        if (t.Attacks == 0)
                        {
                            whHoard += t.Whetted;
                            whHoardWho[id] = whHoardWho.TryGetValue(id, out double h) ? h + t.Whetted : t.Whetted;
                        }
                        // **死蔵の新定義（第64期）。** `Attacks` は `PerformAttack` を通った回数なので、
                        // 棘（カド）のように **`PerformAttack` を通らずに自分の `CurrentAttack` を
                        // 打点に変える駒**を「死蔵」に数えてしまう（第63期 §11-2 で符号を逆に読んだ）。
                        // `AttackReads` は攻撃力を出力に変換した回数そのものなので、そこが塞がる。
                        if (t.Interventions == 0) whHoardInt += t.Whetted;
                        if (t.AttackReads == 0)
                        {
                            whHoardNew += t.Whetted;
                            whHoardNewWho[id] = whHoardNewWho.TryGetValue(id, out double h2)
                                ? h2 + t.Whetted : t.Whetted;
                        }
                    }
                    if (t.Dulled > 0)
                        whLost[id] = whLost.TryGetValue(id, out double d) ? d + t.Dulled : t.Dulled;
                }
            }
        }
        whRowRoute[whRowName] = rowRoute;
    }
    whSw.Stop();

    double whN = Math.Max(1, whBattles);

    Console.WriteLine("# 強化の分布 —— 窓口 `Whet`（第56期）");
    Console.WriteLine();
    Console.WriteLine($"対象 **{whTargets.Length} 編成** × 全 {whStages.Count} 波 × seed 0..{WhSeeds - 1} = **{whBattles} 戦**"
                      + $"（{whSw.Elapsed.TotalSeconds:F1} 秒）。");
    Console.WriteLine();
    Console.WriteLine("**通っているのは他者強化の6経路だけ。** 自己強化の9本（怒り・庇う／殉教・墓守2本・処刑・");
    Console.WriteLine("棘・澱み喰い・軋み・分かち）は `AtkBonus` を直に叩いたまま残してあるので、");
    Console.WriteLine("下の「総量」は `AtkBonus` の総流量ではなく **「他者から受け取った量」** である。");
    Console.WriteLine();

    // ---- 1. 経路別 -------------------------------------------------------
    Console.WriteLine("## 1. 経路別の強化総量");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 量/戦 | 占有率 | 出た編成 |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int i = 0; i < WhetRoutes.Count; i++)
    {
        int rows = whRowRoute.Count(kv => kv.Value[i] > 0);
        Console.WriteLine($"| {WhetRoutes.Names[i]} | {whRoute[i] / whN:F2} | "
                          + $"{(whTotal > 0 ? whRoute[i] * 100.0 / whTotal : 0):F1}% | {rows} |");
    }
    Console.WriteLine($"| **合計** | **{whTotal / whN:F2}** | 100.0% | {whRowRoute.Count(kv => kv.Value.Sum() > 0)} |");
    Console.WriteLine();
    Console.WriteLine($"参考: 弱体総量（`Dull`）は **{whDull / whN:F2}** /戦。"
                      + $"強化 ÷ 弱体 = **{(whDull > 0 ? whTotal / whDull : 0):F2} 倍**。");
    Console.WriteLine();
    Console.WriteLine("> `その他` が 0 でないなら、札を付け忘れた `Whet` の呼び出しがある（受け入れ基準・指示書 §6-5）。");
    Console.WriteLine();

    // ---- 2. 受け手 -------------------------------------------------------
    Console.WriteLine("## 2. 受け手（強化を受け取った駒・上位10）");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 受けた強化/戦 | 占有率 | 陣営 |");
    Console.WriteLine("|---|--:|--:|:-:|");
    foreach ((string id, double v) in whGot.OrderByDescending(kv => kv.Value).Take(10))
        Console.WriteLine($"| {WhLabel(id)} | {v / whN:F2} | {(whTotal > 0 ? v * 100.0 / whTotal : 0):F1}% | "
                          + (whEnemyIds.Contains(id) ? "敵" : "味方") + " |");
    Console.WriteLine();
    Console.WriteLine($"受け手の延べ種類数 **{whGot.Count}**。");
    Console.WriteLine();

    // ---- 3. 収支 ---------------------------------------------------------
    Console.WriteLine("## 3. 収支（`Whet - Dull` の正味）");
    Console.WriteLine();
    Console.WriteLine("**他者から受け取った正味**であって `AtkBonus` の総収支ではない（自己強化9本は窓口を通らない）。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 強化/戦 | 弱体/戦 | 正味 | 陣営 |");
    Console.WriteLine("|---|--:|--:|--:|:-:|");
    var whAll = new HashSet<string>(whGot.Keys);
    whAll.UnionWith(whLost.Keys);
    foreach (string id in whAll
                 .OrderByDescending(x => Math.Abs((whGot.TryGetValue(x, out double a) ? a : 0)
                                                  - (whLost.TryGetValue(x, out double b) ? b : 0)))
                 .Take(15))
    {
        double g = whGot.TryGetValue(id, out double gv) ? gv : 0;
        double d = whLost.TryGetValue(id, out double dv) ? dv : 0;
        Console.WriteLine($"| {WhLabel(id)} | {g / whN:F2} | {d / whN:F2} | **{(g - d) / whN:+0.00;-0.00;0.00}** | "
                          + (whEnemyIds.Contains(id) ? "敵" : "味方") + " |");
    }
    Console.WriteLine();

    // ---- 4. 死蔵 ---------------------------------------------------------
    Console.WriteLine("## 4. 死蔵（配った強化が出力にならなかった量）");
    Console.WriteLine();
    Console.WriteLine("**第64期に定義を直した**（第63期 §11-2）。3つの数え方を併記する。");
    Console.WriteLine();
    Console.WriteLine("| 定義 | 数え方 | 死蔵量/戦 | 強化総量に占める割合 |");
    Console.WriteLine("|---|---|--:|--:|");
    Console.WriteLine($"| 旧（第56期） | `Attacks == 0`（`PerformAttack` を通らなかった） | {whHoard / whN:F2} | "
                      + $"**{(whTotal > 0 ? whHoard * 100.0 / whTotal : 0):F1}%** |");
    Console.WriteLine($"| 参考 | `Interventions == 0`（ダメージの出どころにならなかった） | {whHoardInt / whN:F2} | "
                      + $"{(whTotal > 0 ? whHoardInt * 100.0 / whTotal : 0):F1}% |");
    Console.WriteLine($"| **新（第64期）** | **`AttackReads == 0`（攻撃力を出力に1度も変換しなかった）** | "
                      + $"**{whHoardNew / whN:F2}** | **{(whTotal > 0 ? whHoardNew * 100.0 / whTotal : 0):F1}%** |");
    Console.WriteLine();
    Console.WriteLine("> **旧定義は広すぎ、`Interventions` は狭すぎる。** 棘（カド）は `PerformAttack` を1度も");
    Console.WriteLine("> 通らないが反撃量を自分の `CurrentAttack` で決めるので**強化は満額効く**（旧定義は死蔵に数える）。");
    Console.WriteLine("> 逆に破裂・生贄・吸いは**固定量**で攻撃力を1ビットも読まないのに `Interventions` は立つ。");
    Console.WriteLine("> **`AttackReads` は攻撃力を出力量に変換した回数そのもの**で、加算箇所は4つだけ");
    Console.WriteLine("> （`PerformAttack` / 棘 / 仇討ち / 責め苦の追撃）。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 死蔵(旧)/戦 | 死蔵(新)/戦 | その駒が受けた総量 | 新定義の割合 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    var whHoardAll = new HashSet<string>(whHoardWho.Keys);
    whHoardAll.UnionWith(whHoardNewWho.Keys);
    foreach (string id in whHoardAll
                 .OrderByDescending(x => Math.Max(whHoardWho.TryGetValue(x, out double a) ? a : 0,
                                                  whHoardNewWho.TryGetValue(x, out double b) ? b : 0))
                 .Take(12))
    {
        double o = whHoardWho.TryGetValue(id, out double ov) ? ov : 0;
        double nv = whHoardNewWho.TryGetValue(id, out double nvv) ? nvv : 0;
        double got = whGot.TryGetValue(id, out double g) ? g : 0;
        Console.WriteLine($"| {WhLabel(id)} | {o / whN:F2} | **{nv / whN:F2}** | {got / whN:F2} | "
                          + $"{(got > 0 ? nv * 100.0 / got : 0):F1}% |");
    }
    Console.WriteLine();

    // ---- 5. 逆しま -------------------------------------------------------
    Console.WriteLine("## 5. 逆しま（強化が害になった量）");
    Console.WriteLine();
    Console.WriteLine($"ウツが受けた強化 **{whPerv / whN:F2}** /戦 ＝ 強化総量の **{(whTotal > 0 ? whPerv * 100.0 / whTotal : 0):F1}%**。");
    Console.WriteLine($"そのうち **符号が正へ渡った（半減側へ落ちた）回数 {whFlip / whN:F3} 回/戦**。");
    Console.WriteLine();
    Console.WriteLine("> `AtkBonus` が正だと攻撃力が半減し、負だと下げ幅の3倍になる（`PerverseTrait`）。");
    Console.WriteLine("> **量ではなく符号が読まれる**ので、「落ちた回数」のほうが本体。");
    Console.WriteLine();

    // ---- 6. 行ごとの供給源 -----------------------------------------------
    Console.WriteLine("## 6. 行ごとの供給源（強化を持つ編成だけ）");
    Console.WriteLine();
    Console.WriteLine("| 編成 | " + string.Join(" | ", WhetRoutes.Names.Skip(1)) + " | 合計/戦 |");
    Console.WriteLine("|---" + string.Concat(Enumerable.Repeat("|--:", WhetRoutes.Count)) + "|");
    double whPerRow = whStages.Count * WhSeeds;
    foreach ((string rn, double[] rr) in whRowRoute.Where(kv => kv.Value.Sum() > 0)
                                                   .OrderByDescending(kv => kv.Value.Sum()))
        Console.WriteLine($"| {rn} | "
                          + string.Join(" | ", rr.Skip(1).Select(v => $"{v / whPerRow:F1}"))
                          + $" | **{rr.Sum() / whPerRow:F1}** |");
    Console.WriteLine();
    Console.WriteLine($"強化を1点でも通す行は **{whRowRoute.Count(kv => kv.Value.Sum() > 0)} / {whTargets.Length}**。");
    Console.WriteLine();

    // ---- 7. 陽性対照 -----------------------------------------------------
    // **「0 件でした」は検出器の故障と区別が付かない**（第37期の作法）。
    // 既存のノブで経路を1本ずつ殺し、その経路だけが 0 になり他が動かないことを確かめる。
    // **新しいノブは足さない。**
    Console.WriteLine("## 7. 陽性対照（経路を1本ずつ窓口から外す）");
    Console.WriteLine();
    Console.WriteLine("既存のノブで経路を殺し、**狙った経路が 0 になる**ことを確かめる（合否はこの1本）。");
    Console.WriteLine("`ColossusRule(Regurgitate: false)` が吐き戻しを、`GoadRule(0)` が駆り立てを 0 にする。");
    Console.WriteLine();

    double[] WhMeasure((string Name, Formation F)[] rows, ColossusRule? col, GoadRule? goad)
    {
        var acc = new double[WhetRoutes.Count];
        int n = 0;
        foreach ((string _, Formation f) in rows)
            foreach (EnemyCatalog.Stage st in whStages)
                for (int seed = 0; seed < WhSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, st.Enemy, seed, verbose: false,
                                                      colossus: col, goad: goad);
                    n++;
                    for (int i = 0; i < WhetRoutes.Count; i++) acc[i] += r.WhetByRoute[i];
                }
        for (int i = 0; i < WhetRoutes.Count; i++) acc[i] /= Math.Max(1, n);
        return acc;
    }

    var whProbes = new (string Label, int Route, TraitId Need, ColossusRule? Col, GoadRule? Goad)[]
    {
        ("吐き戻しを切る (`Regurgitate: false`)", (int)WhetRoute.Regurgitate, TraitId.Colossus,
         ColossusRule.Default with { Regurgitate = false }, null),
        ("駆り立てを切る (`GoadRule(0)`)", (int)WhetRoute.Goad, TraitId.Goad,
         null, new GoadRule(0)),
    };

    foreach ((string label, int route, TraitId need, ColossusRule? col, GoadRule? goad) in whProbes)
    {
        // 対照の台は「その経路を実際に持っている行」だけに絞る（持たない行を混ぜると分母で薄まる）。
        var rows = whTargets.Where(t => t.F.Occupied().Any(o => o.Item2.Traits.Contains(need))).ToArray();
        Console.WriteLine($"### {label}");
        Console.WriteLine();
        if (rows.Length == 0)
        {
            Console.WriteLine("**対象の行が絞り込みに入っていない**（対照が立たない）。絞り込みを外して回すこと。");
            Console.WriteLine();
            continue;
        }
        double[] before = WhMeasure(rows, null, null);
        double[] after = WhMeasure(rows, col, goad);

        Console.WriteLine($"対象 {rows.Length} 行。");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 切る前 | 切った後 | 差 |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int i = 0; i < WhetRoutes.Count; i++)
        {
            if (before[i] == 0 && after[i] == 0) continue;
            Console.WriteLine($"| {WhetRoutes.Names[i]}{(i == route ? " ←狙い" : "")} | "
                              + $"{before[i]:F2} | {after[i]:F2} | {after[i] - before[i]:+0.00;-0.00;0.00} |");
        }
        // **合否は「狙った経路が 0 になったか」の1本だけ。**
        // 他の経路のずれは検出器の故障ではない——ノブは計数ではなく**盤面**を切るので
        // （`Regurgitate: false` は攻撃力が返らない盤面・`GoadRule(0)` は押し出しの無い盤面）、
        // 決着ターン数と生死が変わり、ターンあたりで走る他の経路の回数も動く。
        // **「他が動かないこと」を合格条件にすると、盤面を動かさないノブしか対照に使えなくなる。**
        bool killed = before[route] > 0 && after[route] == 0;
        double drift = Enumerable.Range(0, WhetRoutes.Count)
                                 .Where(i => i != route)
                                 .Sum(i => Math.Abs(after[i] - before[i]));
        Console.WriteLine();
        Console.WriteLine($"狙った経路: {before[route]:F2} → **{after[route]:F2}**（切る前が 0 でないこと・切った後が 0 であること）");
        Console.WriteLine($"→ 検出器は **{(killed ? "機能している" : "疑わしい（読む前に原因を潰すこと）")}**");
        Console.WriteLine();
        double rest = before.Sum() - before[route];
        double driftPct = rest > 0 ? drift * 100.0 / rest : 0;
        Console.WriteLine($"参考: 他経路の総ずれ **{drift:F2}**/戦（他経路の合計の {driftPct:F1}%）。"
                          + "**これは故障ではない**——ノブは計数ではなく盤面を切るので、");
        Console.WriteLine("決着の長さと生死が変わり、他の経路が走る回数も動く。合否は狙った経路の1本だけで読む。");
        Console.WriteLine();
    }

    return;
}

// 軋みが響く（第66期 → **第67期に条件の出どころを差し替えた**）。
// **駒は作らない。ロスターの最後の1枠は使わない。**
//
// 第66期は `AtkBonus` を読んで単体 → 薙ぎにしたが、閾値 9 はヨミ自身の上昇（軋み 9 / 突き出し 22）
// だけで満たされ、**条件ではなく起動スイッチ**になっていた（到達時点の内訳 軋み 21.8 対 窓口 8.0）。
// 第67期は条件を **`UnitState.WhetReceived`（`Whet` 窓口を通って届いた累計）** に差し替える。
// **軋み自身の上昇は条件に入らない**ので、これで初めて「強化の2枚目の読み手」になる
// （第65期 積み残し1': 供給 16 対 読み手 1）。
//
// 自由度は構造的に消してある（第64期の教訓・自己検査 (e)）:
//   行は4本に固定（ヨミを含む全3行 ＋ 実演行 `軋み×吐き戻し`。**測定前の宣言**）／
//   席は動かさない（`reseat` は帰属の器具ではない）／攻撃型は薙ぎで固定（第66期と同じ）／
//   閾値は `UnitTally.CreakWhetProbes` の格子から3点、採り方は §2-2 で固定
//   （Q1 を満たす版のうち帰属が最大・同点は高い方）。
//
//     dotnet run --project BattleSim -c Release 0 creak         # 主表（A 帯 seed 0..199）
//     dotnet run --project BattleSim -c Release 0 creak phase0  # Phase 0（V0 の `WhetReceived` の分布）
//     dotnet run --project BattleSim -c Release 0 creak alt     # 再現帯（seed 200..599）
if (focusId == "creak")
{
    string ckArg = args.Length > 2 ? args[2] : "";
    bool ckAlt = ckArg == "alt";
    int ckSeed0 = ckAlt ? 200 : 0;
    int ckSeedN = ckAlt ? 400 : 200;
    IReadOnlyList<EnemyCatalog.Stage> ckStages = EnemyCatalog.Stages;
    var ckAll = CompareBuilds().ToArray();

    bool CkHasYomi((string Name, Formation F) row)
        => row.F.Occupied().Any(o => o.Item2.Id == UnitCatalog.Yomi.Id);

    var ckRows = ckAll.Where(CkHasYomi).ToArray();
    var ckOther = ckAll.Where(b => !CkHasYomi(b)).ToArray();

    // 実演行（第66期 §1-5）。**第67期は「供給がある台」が主題なので採否にも使う**（指示書 §2-3・測定前の宣言）。
    // 配置は手で固定し `reseat` しない。巨躯は `DepthOf(wall) < DepthOf(target)`＝**ゴルムより後ろ**を庇うので、
    // 吐き戻しを受けるにはヨミが後ろでなければならない（第66期の前提の訂正）。
    var ckDemo = ("軋み×吐き戻し (ヨミ×ゴルム)",
        Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald, center: UnitCatalog.Basa,
                        back1: UnitCatalog.Yomi, back3: UnitCatalog.Sero));

    // Q3ダッシュの器具（指示書 §1-4）。**ゴルムだけを同数値・特性なしの素体に落とす。他の4枚と席は動かさない。**
    // 規則にノブを増やさず駒の側で塞ぐ（第47期・`gradient` / `aim` と同じ扱い）。
    var ckPlainGolm = new UnitDef
    {
        Id = "golm_plain", Name = "素体（ゴルム同数値）",
        MaxHp = 150, Attack = 10, Speed = 3, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = ""
    };
    var ckDemoPlain = ("軋み×吐き戻し（ゴルム素体）",
        Formation.Build(front1: ckPlainGolm, front3: UnitCatalog.Gald, center: UnitCatalog.Basa,
                        back1: UnitCatalog.Yomi, back3: UnitCatalog.Sero));

    // 版と閾値（§2-2・**測る前に固定**）。Phase 0-3 の分布から `CreakWhetProbes` の格子上で3点を採る。
    int[] ckVer = { 0, 2, 6, 12 };
    string[] ckVerName = { "V0", "Va", "Vb", "Vc" };

    // 1行ぶんの器。**勝率と計数を同じ走査で取る。**
    (double[] Win, double Sweeps, double Swings, double WhetMax,
     double SelfGain, double WhetGain, double RegurgGain,
     double[] ProbeT, double[] ProbeN)
    CkRun(Formation f, int threshold)
    {
        int np = UnitTally.CreakWhetProbes.Length;
        var win = new double[ckStages.Count];
        double sweeps = 0, swings = 0, wmax = 0, sg = 0, wg = 0, rg = 0;
        var pt = new double[np]; var pn = new double[np];

        for (int w = 0; w < ckStages.Count; w++)
        {
            int wins = 0;
            for (int seed = ckSeed0; seed < ckSeed0 + ckSeedN; seed++)
            {
                BattleResult r = BattleEngine.Run(f, ckStages[w].Enemy, seed, verbose: false,
                                                  creak: new CreakRule(threshold));
                if (r.PlayerWon) wins++;
                if (!r.TallyByUnit.TryGetValue(UnitCatalog.Yomi.Id, out UnitTally? t)) continue;
                sweeps += t.CreakSweeps; swings += t.CreakSwings; wmax += t.CreakWhetMax;
                sg += t.CreakSelfGain; wg += t.CreakWhetGain; rg += t.CreakRegurgGain;
                if (t.CreakWhetProbeTurn is null) continue;
                for (int i = 0; i < np; i++)
                {
                    if (t.CreakWhetProbeTurn[i] == 0) continue;
                    pn[i]++; pt[i] += t.CreakWhetProbeTurn[i];
                }
            }
            win[w] = wins * 100.0 / ckSeedN;
        }
        int n = ckStages.Count * ckSeedN;
        return (win, sweeps / n, swings / n, wmax / n, sg / n, wg / n, rg / n, pt, pn);
    }

    var ckSw = System.Diagnostics.Stopwatch.StartNew();

    // ---- phase0: V0 の `WhetReceived` の分布と、判定式の実効レンジ ----------------
    if (ckArg == "phase0")
    {
        Console.WriteLine("# 軋みが響く（第67期） —— Phase 0（**規則は無効のまま測る**）");
        Console.WriteLine();
        Console.WriteLine($"seed {ckSeed0}..{ckSeed0 + ckSeedN - 1} × 5 波。**盤面は1つも動かない**"
                          + "（`CreakRule` 無効＝`ModifyPattern` が素通り）。");
        Console.WriteLine();
        Console.WriteLine("## 0-3. ヨミの `WhetReceived` の分布（現行・V0）");
        Console.WriteLine();
        Console.WriteLine("`押され` は1戦あたり窓口経由で届いた累計の平均、`うち吐` はそのうち吐き戻し、");
        Console.WriteLine("`軋み` は自前の上昇（**条件に入らない**）。各列は 5 波 × 試行のうちその点へ届いた割合で、");
        Console.WriteLine("次の行が届いた試行だけの平均ターン。**閾値の候補はこの格子から採る。**");
        Console.WriteLine();
        Console.Write("| 行 | 振/戦 | 押され | うち吐 | 軋み |");
        foreach (int q in UnitTally.CreakWhetProbes) Console.Write($" {q} |");
        Console.WriteLine();
        Console.Write("|---|--:|--:|--:|--:|");
        foreach (int _ in UnitTally.CreakWhetProbes) Console.Write("--:|");
        Console.WriteLine();
        double ckDen = ckStages.Count * ckSeedN;
        foreach ((string rn, Formation f) in ckRows.Append(ckDemo).Append(ckDemoPlain))
        {
            var g = CkRun(f, 0);
            Console.Write($"| {rn} | {g.Swings:F2} | {g.WhetGain:F1} | {g.RegurgGain:F1} | {g.SelfGain:F1} |");
            for (int i = 0; i < UnitTally.CreakWhetProbes.Length; i++)
                Console.Write($" {g.ProbeN[i] * 100.0 / ckDen:F1}% |");
            Console.WriteLine();
            Console.Write("|  |  |  |  | 到達T |");
            for (int i = 0; i < UnitTally.CreakWhetProbes.Length; i++)
                Console.Write(g.ProbeN[i] > 0 ? $" {g.ProbeT[i] / g.ProbeN[i]:F2} |" : " — |");
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine("## 0-5. 主判定19行との重なりと、Q6 の実効レンジ（自己検査 (c)）");
        Console.WriteLine();
        var ckPrimary0 = new HashSet<string>(Baseline.PrimaryRows);
        var ckIn0 = ckRows.Where(b => ckPrimary0.Contains(b.Name)).Select(b => b.Name).ToArray();
        Console.WriteLine($"ヨミを含む行は **{ckRows.Length} 行**、うち主判定 {Baseline.PrimaryRows.Length} 行に "
                          + $"**{ckIn0.Length} 行**（{string.Join(" / ", ckIn0)}）。");
        Console.WriteLine();
        double[] ckFifth0 = Baseline.PrimaryRows
            .Select(n => Array.FindIndex(ckAll, b => b.Name == n)).Where(i => i >= 0)
            .Select(i => CkRun(ckAll[i].Item2, 0).Win[4]).ToArray();
        double ckAvg0 = ckFifth0.Average();
        double ckSwing0 = ckIn0.Select(n => CkRun(ckAll[Array.FindIndex(ckAll, b => b.Name == n)].Item2, 0).Win[4]).Sum()
                          / ckFifth0.Length;
        Console.WriteLine($"主判定の第五波平均（現行）= **{ckAvg0:F1}%**、歯止め {Baseline.PrimaryFifthFloor:F1}% との余裕 "
                          + $"**{ckAvg0 - Baseline.PrimaryFifthFloor:F1}pt**。");
        Console.WriteLine($"重なる {ckIn0.Length} 行が第五波で 0.0% まで落ちても平均の低下は最大 **{ckSwing0:F2}pt**"
                          + "——**実効レンジがこの幅**。");
        Console.WriteLine();
        Console.WriteLine("## 0-6. 陰性対照の分母");
        Console.WriteLine();
        Console.WriteLine($"ヨミを含まない **{ckOther.Length} 行**（{ckOther.Length} × 5 波 = **{ckOther.Length * 5} セル**）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {ckSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ---- 主表 -------------------------------------------------------------------
    Console.WriteLine($"# 軋みが響く（第67期） —— 主表{(ckAlt ? "・再現帯" : "")}");
    Console.WriteLine();
    Console.WriteLine($"seed {ckSeed0}..{ckSeed0 + ckSeedN - 1} × 5 波。帰属 = V_n − V0。");
    Console.WriteLine("**席は現行のまま（`reseat` しない）／行は4本に固定（測定前の宣言）。**");
    Console.WriteLine($"閾値は `WhetReceived`（窓口経由の累計）で **{ckVer[1]} / {ckVer[2]} / {ckVer[3]}**。");
    Console.WriteLine();

    var ckMain = ckRows.Append(ckDemo).ToArray();
    var ckBase = new Dictionary<string, double[]>();
    foreach ((string rn, Formation f) in ckMain.Append(ckDemoPlain)) ckBase[rn] = CkRun(f, 0).Win;

    Console.WriteLine("## 1. 行 × 版（波ごとの勝率・平均・帰属・薙ぎ化率）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 帰属 | 薙ぎ化率 | 振/戦 | 押され | 初到達T |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var ckAttr = new Dictionary<string, double[]>();
    var ckRate = new Dictionary<string, double[]>();
    foreach ((string rn, Formation f) in ckMain)
    {
        var attr = new double[ckVer.Length];
        var rate = new double[ckVer.Length];
        for (int v = 0; v < ckVer.Length; v++)
        {
            var g = CkRun(f, ckVer[v]);
            double avg = g.Win.Average();
            attr[v] = avg - ckBase[rn].Average();
            rate[v] = g.Swings > 0 ? g.Sweeps * 100.0 / g.Swings : 0;
            int pi = Array.IndexOf(UnitTally.CreakWhetProbes, ckVer[v]);
            string ft = pi >= 0 && g.ProbeN[pi] > 0 ? (g.ProbeT[pi] / g.ProbeN[pi]).ToString("F2") : "—";
            Console.WriteLine($"| {(v == 0 ? rn : "")} | {ckVerName[v]} | "
                              + string.Join(" | ", g.Win.Select(x => $"{x:F1}%"))
                              + $" | {avg:F1}% | {(v == 0 ? "—" : $"{attr[v]:+0.0;-0.0}")} "
                              + $"| {rate[v]:F1}% | {g.Swings:F2} | {g.WhetGain:F1} | {ft} |");
        }
        ckAttr[rn] = attr; ckRate[rn] = rate;
    }
    Console.WriteLine();

    // ---- Q1 → 閾値の採用 ---------------------------------------------------------
    Console.WriteLine("## 2. Q1（条件は条件か）と閾値の採用");
    Console.WriteLine();
    Console.WriteLine("**採る版は「Q1 を満たす版のうち帰属が最大のもの。同点は高い閾値」**（§2-2・測る前に固定）。");
    Console.WriteLine("Q1 = 採る版で、**4行中3行**の薙ぎ化率が **10% 以上 90% 以下**。**90% 超が1行でもあれば×。**");
    Console.WriteLine();
    Console.WriteLine("| 版 | 閾値 | 薙ぎ化率（4行） | 帯内 | 90%超 | Q1 | 帰属（4行平均） |");
    Console.WriteLine("|---|--:|---|--:|--:|---|--:|");
    int ckPick = -1; double ckPickAttr = double.NegativeInfinity;
    for (int v = 1; v < ckVer.Length; v++)
    {
        var rr = ckMain.Select(b => ckRate[b.Item1][v]).ToArray();
        int inBand = rr.Count(x => x >= 10.0 && x <= 90.0);
        int over = rr.Count(x => x > 90.0);
        bool q1 = inBand >= 3 && over == 0;
        double aa = ckMain.Select(b => ckAttr[b.Item1][v]).Average();
        Console.WriteLine($"| {ckVerName[v]} | {ckVer[v]} | {string.Join(" / ", rr.Select(x => $"{x:F1}%"))} "
                          + $"| {inBand} | {over} | {(q1 ? "○" : "×")} | {aa:+0.0;-0.0} |");
        if (q1 && aa >= ckPickAttr) { ckPickAttr = aa; ckPick = v; }
    }
    Console.WriteLine();
    if (ckPick < 0) Console.WriteLine("**Q1 を満たす版が1つも無い → 採らない（残置）。**");
    else Console.WriteLine($"→ 採る版は **{ckVerName[ckPick]}（Threshold = {ckVer[ckPick]}）**。");
    Console.WriteLine();

    // ---- Q3ダッシュ（主判定・読み手か） ---------------------------------------------
    Console.WriteLine("## 3. Q3'（**主判定**: 外の供給を読んでいるか）");
    Console.WriteLine();
    Console.WriteLine("`軋み×吐き戻し` のゴルムだけを同数値・特性なしの素体に落とす（他の4枚と席は動かさない）。");
    Console.WriteLine("**薙ぎ化率が 1/5 以下に落ちること。**落ちなければ条件は外の供給を読んでいない。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 閾値 | 現行の薙ぎ化率 | 素体の薙ぎ化率 | 比 | 押され（現行→素体） | 1/5 以下 |");
    Console.WriteLine("|---|--:|--:|--:|--:|---|---|");
    for (int v = 1; v < ckVer.Length; v++)
    {
        var gn = CkRun(ckDemo.Item2, ckVer[v]);
        var gp = CkRun(ckDemoPlain.Item2, ckVer[v]);
        double rn2 = gn.Swings > 0 ? gn.Sweeps * 100.0 / gn.Swings : 0;
        double rp = gp.Swings > 0 ? gp.Sweeps * 100.0 / gp.Swings : 0;
        double ratio = rn2 > 0 ? rp / rn2 : 0;
        Console.WriteLine($"| {ckVerName[v]} | {ckVer[v]} | {rn2:F1}% | {rp:F1}% | {ratio:F2} "
                          + $"| {gn.WhetGain:F1} → {gp.WhetGain:F1} | {(rn2 > 0 && ratio <= 0.2 ? "○" : "×")} |");
    }
    Console.WriteLine();

    // ---- Q2 ------------------------------------------------------------------------
    if (ckPick >= 0)
    {
        Console.WriteLine("## 4. Q2（帰属）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 帰属 | ≥ +1.5pt | ≥ −3.0pt |");
        Console.WriteLine("|---|--:|---|---|");
        foreach ((string rn, Formation _) in ckMain)
        {
            double a = ckAttr[rn][ckPick];
            Console.WriteLine($"| {rn} | {a:+0.0;-0.0} | {(a >= 1.5 ? "○" : "×")} | {(a >= -3.0 ? "○" : "**×**")} |");
        }
        int okN = ckMain.Count(b => ckAttr[b.Item1][ckPick] >= 1.5);
        bool floorOk = ckMain.All(b => ckAttr[b.Item1][ckPick] >= -3.0);
        Console.WriteLine();
        Console.WriteLine($"**Q2 = {(okN >= 2 && floorOk ? "○" : "×")}**（+1.5pt 以上が {okN} / 4 行"
                          + $"・下限を割った行 {ckMain.Count(b => ckAttr[b.Item1][ckPick] < -3.0)}）。");
        Console.WriteLine();
    }

    // ---- Q4 / Q5 / Q6 / Q7 ---------------------------------------------------------
    Console.WriteLine("## 5. Q4（情報セル）・Q5（陰性対照）・Q6（歯止め）・Q7（絵）");
    Console.WriteLine();
    int InfoCells(double[] w) => w.Skip(1).Count(x => x > 0.0 && x < 100.0);
    Console.WriteLine("| 行 | 情報セル V0 | " + string.Join(" | ", ckVerName.Skip(1).Select(n => $"情報セル {n}")) + " |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach ((string rn, Formation f) in ckMain)
        Console.WriteLine($"| {rn} | {InfoCells(ckBase[rn])} | "
                          + string.Join(" | ", ckVer.Skip(1).Select(t => InfoCells(CkRun(f, t).Win).ToString())) + " |");
    Console.WriteLine();

    int ckDiff = 0;
    foreach ((string rn, Formation f) in ckOther)
    {
        double[] b0 = CkRun(f, 0).Win;
        foreach (int t in ckVer.Skip(1))
        {
            double[] bt = CkRun(f, t).Win;
            for (int w = 0; w < b0.Length; w++) if (Math.Abs(b0[w] - bt[w]) > 1e-9) ckDiff++;
        }
    }
    Console.WriteLine($"**Q5**: ヨミを含まない {ckOther.Length} 行 × 5 波 = {ckOther.Length * 5} セルを"
                      + $"3つの閾値で照合 → **ずれ {ckDiff} 件**。");
    Console.WriteLine();

    var ckPrimIdx = Baseline.PrimaryRows.Select(n => Array.FindIndex(ckAll, b => b.Name == n))
                                        .Where(i => i >= 0).ToArray();
    foreach (int v in (ckPick < 0 ? new[] { 0 } : new[] { 0, ckPick }))
    {
        double avg = ckPrimIdx.Select(i => CkRun(ckAll[i].Item2, ckVer[v]).Win[4]).Average();
        Console.WriteLine($"**Q6**: 主判定 {ckPrimIdx.Length} 行の第五波平均（{ckVerName[v]}）= **{avg:F1}%**"
                          + $"、歯止め {Baseline.PrimaryFifthFloor:F1}% との余裕 **{avg - Baseline.PrimaryFifthFloor:F1}pt**。");
    }
    Console.WriteLine();
    Console.WriteLine("**Q7**（絵）: 初到達Tは §1 の最右列。第66期の 1.55〜2.55 より遅いことが条件。");
    Console.WriteLine();
    Console.WriteLine($"所要 {ckSw.Elapsed.TotalSeconds:F1} 秒。");
    return;
}


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

// 強化の使い道の解剖（第65期・調査）。**この期は新しい機構を1つも作らない。駒も作らない。**
// `Traits.cs` の挙動 / `UnitCatalog.All` / `Stages` / `CompareBuilds()` は1行も動かさず、
// 足したのは **(a) 誰も読んで分岐しない計数**（到着ターン・使用・経路別の受け手）と
// **(b) 窓口 `Whet` の加算だけを経路ごとに落とすノブ `WhetMask`**（既定は空＝現行）。
//
// 主題は「**強化の供給の偏りは、編成・配置の判断を減らしているか**」。
// 積み残し1（第56期）は「供給の 47.1% が吐き戻し1本＝編成が選べる供給が無い」として立ち、
// **「要検討」のまま3期（62〜64）を「行き先」の駒に使って3回落ちた。**
// 偏りが問題だと確かめた数字は1つも無い——ここで測る。
//
// 立てた仮説（測って否定してよい。**測る前に指示書 §0-3 で固定**）:
//   H1 偏り説 —— 吐き戻しが半分を占めるので編成が強化について判断する余地が無い
//   H2 無価値説 —— **吐き戻しの帰属は ≈ 0**（第23期の再現）。偏りは価値の無い通貨の帳簿上の偏り
//   H3 遅さ説 —— 律速は偏りではなく**到着の遅さ**。受け手が振る前に戦闘が終わる
//
// **自由度 (e) を構造的に消してある**（第64期の教訓）:
//   行は選ばない（その経路の供給者を含む**全行**）／席は動かさない（`reseat` は帰属の器具ではない）／
//   枠は差し替えない（**落とすのは窓口の加算1行だけ**で、供給者の他の特性は残る）。
//
//     dotnet run --project BattleSim -c Release 0 spend        # 主表（A 帯 seed 0..199）
//     dotnet run --project BattleSim -c Release 0 spend map    # 判断の地図（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 spend alt    # 再現帯（seed 200..599）
if (focusId == "spend")
{
    var spBuilds = CompareBuilds().ToArray();
    IReadOnlyList<EnemyCatalog.Stage> spStages = EnemyCatalog.Stages;
    string spArg = args.Length > 2 ? args[2] : "";
    bool spAlt = spArg == "alt";
    int spSeed0 = spAlt ? 200 : 0;
    int spSeedN = spAlt ? 400 : 200;

    // 経路の台帳。**「編成が動かせるか」は測る前にコードを読んで固定した**（指示書 §1-3）。
    // Q3（H1 の判定）はこの分類の上で読むので、**実測を見てから書き換えないこと。**
    var spRoutes = new (WhetRoute Route, TraitId Need, string Supplier, string Dest, string Decided, string Lever)[]
    {
        (WhetRoute.Goad,         TraitId.Goad,     "カリ",   "隣接する `CurrentAttack` 最大の味方1体",
         "カリの隣に誰を置くか", "配置"),
        (WhetRoute.RallyOpening, TraitId.Rally,    "ガン",   "味方全体（自分以外・`SupportTargets`）",
         "—（無条件）", "動かせない"),
        (WhetRoute.RallyTurn,    TraitId.Rally,    "ガン",   "手番を差し出した味方（`SupportTargets`）",
         "`IdleTurn` の供給（痺れ・据え・不動）", "編成"),
        (WhetRoute.Bind,         TraitId.Bind,     "クグ",   "`AcceptsSupport` を通り痺れていない味方から**無作為**",
         "候補集合の枚数だけ（`Roll`）", "編成（弱い）"),
        (WhetRoute.Drifter,      TraitId.Drifter,  "シオ",   "動かされた味方",
         "移動の供給（バサ・ハネ・曝き）", "編成"),
        (WhetRoute.Regurgitate,  TraitId.Colossus, "ゴルム", "ゴルムが庇った相手（`SupportTargets`）",
         "巨躯の被覆（誰がゴルムより後ろにいるか）", "配置"),
        (WhetRoute.Favor,        TraitId.Favor,    "ヒヨ",   "燃えている味方全員（自分を除く・**位置を問わない**）",
         "燃焼の供給（ボルグの隣・破裂）", "配置（間接）"),
    };

    var spName = new Dictionary<string, string>();
    foreach (UnitDef d in UnitCatalog.All) spName[d.Id] = d.Name;
    foreach (EnemyCatalog.Stage st in spStages)
        foreach ((int _, UnitDef d) in st.Enemy.Occupied()) spName[d.Id] = d.Name;
    string SpLabel(string id) => spName.TryGetValue(id, out string? n) ? n : id;

    bool SpHas((string Name, Formation F) row, TraitId t)
        => row.F.Occupied().Any(o => o.Item2.Traits.Contains(t));

    // ---- map: 判断の地図。**戦闘を1回も回さない。** -----------------------------
    if (spArg == "map")
    {
        Console.WriteLine("# 強化の使い道 —— 判断の地図（第65期 Phase 0。**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("## 0-2. 出力経路（強化1点が「使われる」ための分母）");
        Console.WriteLine();
        Console.WriteLine("`CurrentAttack` を自分の出力量に変換する箇所は**ロスター全体で4つ**"
                          + "（`BattleContext.NoteAttackRead` の呼び出し元）。");
        Console.WriteLine();
        Console.WriteLine("| # | 場所 | 型 | `CurrentAttack` の読み | 手番 |");
        Console.WriteLine("|--:|---|---|---|---|");
        Console.WriteLine("| 1 | `BattleEngine.cs:1815` `PerformAttack` | 攻撃（単体・薙ぎ・貫き・全体） | "
                          + "1回（`atk` を作る所。倍率＝止めはこの後） | 自分の手番 |");
        Console.WriteLine("| 2 | `Traits.cs:1221` 棘 `ThornsTrait.OnDamaged` | 反撃（範囲） | "
                          + "1回（`CurrentAttack * Multiplier`） | **ターン外**（`CanActOutOfTurn`） |");
        Console.WriteLine("| 3 | `Traits.cs:3453` 仇討ち `AvengeTrait.OnAllyDamaged` | 反撃（単体） | "
                          + "1回（`Max(1, CurrentAttack)`） | **ターン外**（`CanActOutOfTurn`） |");
        Console.WriteLine("| 4 | `Traits.cs:3504` 責め苦の追撃 `TormentTrait.OnAfterAttack` | 追撃（単体） | "
                          + "1回（`Max(1, CurrentAttack)`） | 自分の手番の中 |");
        Console.WriteLine();
        Console.WriteLine("> **乗算はこの4本の外側にある。** `UnitState.CurrentAttack` は "
                          + "`Def.Attack + AtkBonus` を作ってから `Trait.ModifyAttack` を通すので、");
        Console.WriteLine("> **熾火（ホタ・燃焼中は4倍＋貫き）に配った強化1点は 4 点になる**（第58期の乗算監査）。");
        Console.WriteLine("> 逆に `Actions = [Skill]` の3枚（ノノ・ミオ・ヒヨ）は**4本のどれも通らない**"
                          + "＝真の捨て場（第64期）。");
        Console.WriteLine();

        Console.WriteLine("## 0-3. 経路ごとの「行き先の決まり方」");
        Console.WriteLine();
        Console.WriteLine("**Q3（H1 の判定）はこの分類の上で読む。実測を見てから書き換えない。**");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 供給者 | 行き先 | 決めているもの | 編成が動かせるか |");
        Console.WriteLine("|---|:-:|---|---|:-:|");
        foreach ((WhetRoute r, TraitId _, string sup, string dest, string dec, string lev) in spRoutes)
            Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | {sup} | {dest} | {dec} | **{lev}** |");
        Console.WriteLine();
        Console.WriteLine("> **縛めの「?」を埋めた**（指示書 §1-3）——`BindTrait.BindAlly` は");
        Console.WriteLine("> `AcceptsSupport` を通り痺れていない味方から `ctx.Roll` で**無作為に**選ぶ。");
        Console.WriteLine("> 席も攻撃力も隣接も1ビットも読まないので、**配置では動かせない**"
                          + "（動かせるのは候補集合の枚数だけ）。");
        Console.WriteLine();

        Console.WriteLine("## 0-5 / 0-6. 各経路の供給者を含む行（陰性対照の分母）");
        Console.WriteLine();
        var spPrimary = new HashSet<string>(Baseline.PrimaryRows);
        Console.WriteLine("| 経路 | 供給者を含む行 | 含まない行（陰性対照） | 主判定19行のうち |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach ((WhetRoute r, TraitId need, string _, string __, string ___, string ____) in spRoutes)
        {
            int with = spBuilds.Count(b => SpHas(b, need));
            int prim = spBuilds.Count(b => SpHas(b, need) && spPrimary.Contains(b.Name));
            Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | {with} | {spBuilds.Length - with} | {prim} |");
        }
        Console.WriteLine();
        Console.WriteLine($"全 **{spBuilds.Length} 行**（`docs/balance.md` = {spBuilds.Length} × 5 波 = "
                          + $"**{spBuilds.Length * 5} セル**）。");
        Console.WriteLine();
        return;
    }

    // ---- 測定 -------------------------------------------------------------
    // 1戦ぶんの器。**行 × 波の勝率**と、経路別の量・到着・使用・受け手を同じ走査で取る。
    double[] SpWin(Formation f, WhetMask? mask, double[]? route, double[]? turnSum,
                   double[]? firstSum, double[]? firstCnt, double[]? used,
                   Dictionary<string, double>[]? to, Dictionary<string, double>? got,
                   Dictionary<string, double>? hoardNew, double[]? battleTurns)
    {
        var win = new double[spStages.Count];
        for (int w = 0; w < spStages.Count; w++)
        {
            int wins = 0;
            for (int seed = spSeed0; seed < spSeed0 + spSeedN; seed++)
            {
                BattleResult r = BattleEngine.Run(f, spStages[w].Enemy, seed, verbose: false,
                                                  whetMask: mask);
                if (r.PlayerWon) wins++;
                if (route is null) continue;

                if (battleTurns is not null) battleTurns[0] += r.Turns;
                for (int i = 0; i < WhetRoutes.Count; i++)
                {
                    route[i] += r.WhetByRoute[i];
                    turnSum![i] += r.WhetTurnSumByRoute[i];
                    firstSum![i] += r.WhetFirstTurnSumByRoute[i];
                    firstCnt![i] += r.WhetFirstTurnCountByRoute[i];
                    used![i] += r.WhetUsedByRoute[i];
                    foreach ((string id, int v) in r.WhetToByRoute[i])
                        to![i][id] = to[i].TryGetValue(id, out double had) ? had + v : v;
                }
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (t.Whetted <= 0) continue;
                    got![id] = got.TryGetValue(id, out double g) ? g + t.Whetted : t.Whetted;
                    if (t.AttackReads == 0)
                        hoardNew![id] = hoardNew.TryGetValue(id, out double h) ? h + t.Whetted : t.Whetted;
                }
            }
            win[w] = wins * 100.0 / spSeedN;
        }
        return win;
    }

    var spSw = System.Diagnostics.Stopwatch.StartNew();

    // 現行（`WhetMask` 空）。**ここが 305 セルの `compare` と一致する**（受け入れ基準1）。
    var spBase = new Dictionary<string, double[]>();
    var spRouteAmt = new double[WhetRoutes.Count];
    var spTurnSum = new double[WhetRoutes.Count];
    var spFirstSum = new double[WhetRoutes.Count];
    var spFirstCnt = new double[WhetRoutes.Count];
    var spUsed = new double[WhetRoutes.Count];
    var spTo = Enumerable.Range(0, WhetRoutes.Count).Select(_ => new Dictionary<string, double>()).ToArray();
    var spGot = new Dictionary<string, double>();
    var spHoard = new Dictionary<string, double>();
    var spTurns = new double[1];
    var spRowRoute = new Dictionary<string, double[]>();
    var spRowHoard = new Dictionary<string, double>();

    foreach ((string rn, Formation f) in spBuilds)
    {
        var rr = new double[WhetRoutes.Count];
        var rh = new Dictionary<string, double>();
        var rowTo = Enumerable.Range(0, WhetRoutes.Count).Select(_ => new Dictionary<string, double>()).ToArray();
        spBase[rn] = SpWin(f, null, rr, spTurnSum, spFirstSum, spFirstCnt, spUsed, rowTo, spGot, rh, spTurns);
        for (int i = 0; i < WhetRoutes.Count; i++)
        {
            spRouteAmt[i] += rr[i];
            foreach ((string id, double v) in rowTo[i])
                spTo[i][id] = spTo[i].TryGetValue(id, out double had) ? had + v : v;
        }
        spRowRoute[rn] = rr;
        spRowHoard[rn] = rh.Values.Sum();
        foreach ((string id, double v) in rh)
            spHoard[id] = spHoard.TryGetValue(id, out double h) ? h + v : v;
    }

    // 経路を1本ずつ落とす。**全 61 行**（陰性対照＝供給者を含まない行も同じ表で出す）。
    var spDrop = new Dictionary<(WhetRoute, string), double[]>();
    foreach ((WhetRoute r, TraitId _, string __, string ___, string ____, string _____) in spRoutes)
        foreach ((string rn, Formation f) in spBuilds)
            spDrop[(r, rn)] = SpWin(f, WhetMask.Of(r), null, null, null, null, null, null, null, null, null);

    spSw.Stop();

    int spBattles = spBuilds.Length * spStages.Count * spSeedN * (1 + spRoutes.Length);
    double spN = spBuilds.Length * spStages.Count * (double)spSeedN;
    double Mean(double[] w) => w.Average();

    Console.WriteLine("# 強化は何に使われているのか（第65期・調査）");
    Console.WriteLine();
    Console.WriteLine($"全 **{spBuilds.Length} 行** × {spStages.Count} 波 × seed {spSeed0}..{spSeed0 + spSeedN - 1}"
                      + $"、現行 + 7 経路落とし = **{spBattles} 戦**（{spSw.Elapsed.TotalSeconds:F1} 秒）。");
    Console.WriteLine($"帯は **{(spAlt ? "B（再現・200..599）" : "A（主・0..199）")}**。");
    Console.WriteLine();
    Console.WriteLine("**行を選ばない・席を動かさない・枠を差し替えない**（第64期の自由度 (e) を構造的に消してある）。");
    Console.WriteLine("落とすのは **`BattleContext.Whet` の `AtkBonus +=` の1行だけ**で、");
    Console.WriteLine("縛めの痺れ・移り木の回復・吐き戻しのログ・乱数の消費は1ビットも動かない。");
    Console.WriteLine();

    // ---- 表A -------------------------------------------------------------
    Console.WriteLine("## 表A. 経路の帰属（現行 − その経路を落とした版）");
    Console.WriteLine();
    Console.WriteLine("帰属は**5波平均の勝率(pt)**。**`|帰属| < 1.5pt` は 0 と読む**（第57期の線）。");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 供給量/戦 | 占有率 | 該当行 | 帰属の平均 | 正の行 | \\|帰属\\| ≥ 1.5 の行 | 陰性対照（最大\\|Δ\\|） |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    double spTotal = spRouteAmt.Sum();
    var spAttr = new Dictionary<WhetRoute, List<(string Row, double D)>>();
    foreach ((WhetRoute r, TraitId need, string _, string __, string ___, string ____) in spRoutes)
    {
        var with = new List<(string, double)>();
        double negMax = 0;
        foreach ((string rn, Formation f) in spBuilds)
        {
            double d = Mean(spBase[rn]) - Mean(spDrop[(r, rn)]);
            if (SpHas((rn, f), need)) with.Add((rn, d));
            else negMax = Math.Max(negMax, Math.Abs(d));
        }
        spAttr[r] = with;
        Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | {spRouteAmt[(int)r] / spN:F2} | "
                          + $"{(spTotal > 0 ? spRouteAmt[(int)r] * 100.0 / spTotal : 0):F1}% | {with.Count} | "
                          + $"**{with.Average(x => x.Item2):+0.0;-0.0;0.0}** | "
                          + $"{with.Count(x => x.Item2 > 0)} | {with.Count(x => Math.Abs(x.Item2) >= 1.5)} | "
                          + $"{negMax:F1} |");
    }
    Console.WriteLine($"| **合計** | **{spTotal / spN:F2}** | 100.0% | — | — | — | — | — |");
    Console.WriteLine();

    Console.WriteLine("### 表A-2. 行ごとの帰属（該当行を全部・選んでいない）");
    Console.WriteLine();
    foreach ((WhetRoute r, TraitId _, string __, string ___, string ____, string _____) in spRoutes)
    {
        Console.WriteLine($"**{WhetRoutes.Names[(int)r]}**（{spAttr[r].Count} 行）: "
                          + string.Join(" / ", spAttr[r].OrderByDescending(x => x.D)
                                .Select(x => $"{x.Row} {x.D:+0.0;-0.0;0.0}")));
        Console.WriteLine();
    }

    // ---- 表B -------------------------------------------------------------
    Console.WriteLine("## 表B. 到着と使用");
    Console.WriteLine();
    Console.WriteLine("**使用率** = 受け取った**後**に受け手が `AttackReads` を1度でも通した量の割合。");
    Console.WriteLine("死蔵（`AttackReads == 0`・第64期）より厳しい——最後の一撃の後に届いた強化は死蔵に数えられない。");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 供給量/戦 | 到着の平均T | 初到着の平均T | 使用率 | 到着T ÷ 決着T |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    double spAvgTurns = spTurns[0] / spN;
    foreach ((WhetRoute r, TraitId _, string __, string ___, string ____, string _____) in spRoutes)
    {
        int i = (int)r;
        double amt = spRouteAmt[i];
        double arr = amt > 0 ? spTurnSum[i] / amt : 0;
        double first = spFirstCnt[i] > 0 ? spFirstSum[i] / spFirstCnt[i] : 0;
        Console.WriteLine($"| {WhetRoutes.Names[i]} | {amt / spN:F2} | {arr:F2} | {first:F2} | "
                          + $"**{(amt > 0 ? spUsed[i] * 100.0 / amt : 0):F1}%** | "
                          + $"{(spAvgTurns > 0 ? arr / spAvgTurns : 0):F2} |");
    }
    Console.WriteLine();
    Console.WriteLine($"参考: 全 {spBuilds.Length} 行 × 5 波の**決着ターンの平均は {spAvgTurns:F2}T**。");
    Console.WriteLine("開戦時（`OnBattleStart`）の到着はターン **0**。");
    Console.WriteLine();

    // ---- 表C -------------------------------------------------------------
    Console.WriteLine("## 表C. 判断の地図（Phase 0-3 の分類 + 実測）");
    Console.WriteLine();
    Console.WriteLine("**分類は測る前に固定した**（`spend map`）。**実測を見てから書き換えていない。**");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 決めているもの | 編成が動かせるか | 帰属の平均 | 受け手の種類数 | 最大の受け手 |");
    Console.WriteLine("|---|---|:-:|--:|--:|---|");
    foreach ((WhetRoute r, TraitId _, string __, string ___, string dec, string lev) in spRoutes)
    {
        int i = (int)r;
        var top = spTo[i].OrderByDescending(kv => kv.Value).FirstOrDefault();
        double sum = spTo[i].Values.Sum();
        Console.WriteLine($"| {WhetRoutes.Names[i]} | {dec} | **{lev}** | "
                          + $"{spAttr[r].Average(x => x.D):+0.0;-0.0;0.0} | {spTo[i].Count} | "
                          + (top.Key is null ? "—"
                             : $"{SpLabel(top.Key)} {(sum > 0 ? top.Value * 100.0 / sum : 0):F0}%") + " |");
    }
    Console.WriteLine();
    Console.WriteLine("### Q3 の判定（群ごとの帰属の平均）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 経路 | 帰属の平均（経路の単純平均） |");
    Console.WriteLine("|---|---|--:|");
    foreach (string lev in new[] { "配置", "配置（間接）", "編成", "編成（弱い）", "動かせない" })
    {
        var rs = spRoutes.Where(x => x.Lever == lev).ToArray();
        if (rs.Length == 0) continue;
        Console.WriteLine($"| {lev} | {string.Join(" / ", rs.Select(x => WhetRoutes.Names[(int)x.Route]))} | "
                          + $"**{rs.Average(x => spAttr[x.Route].Average(y => y.D)):+0.0;-0.0;0.0}** |");
    }
    Console.WriteLine();

    // ---- 表D -------------------------------------------------------------
    Console.WriteLine("## 表D. 死蔵（新定義・`AttackReads == 0`）の行ごと分布");
    Console.WriteLine();
    double spHoardAll = spHoard.Values.Sum();
    Console.WriteLine($"死蔵総量 **{spHoardAll / spN:F2}** /戦 ＝ 強化総量の "
                      + $"**{(spTotal > 0 ? spHoardAll * 100.0 / spTotal : 0):F1}%**。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 死蔵(新)/戦 | 受けた総量/戦 | 死蔵率 | 真の捨て場か |");
    Console.WriteLine("|---|--:|--:|--:|:-:|");
    var spDump = new[] { "nono", "mio", "hiyo" };
    foreach ((string id, double v) in spHoard.OrderByDescending(kv => kv.Value).Take(10))
    {
        double g = spGot.TryGetValue(id, out double gv) ? gv : 0;
        Console.WriteLine($"| {SpLabel(id)} | **{v / spN:F2}** | {g / spN:F2} | "
                          + $"{(g > 0 ? v * 100.0 / g : 0):F1}% | {(spDump.Contains(id) ? "○" : "")} |");
    }
    Console.WriteLine();
    Console.WriteLine("捨て場3枚（ノノ・ミオ・ヒヨ）が受けた量: "
                      + string.Join(" / ", spDump.Select(id =>
                          $"{SpLabel(id)} {(spGot.TryGetValue(id, out double g) ? g / spN : 0):F2}"))
                      + " /戦。");
    Console.WriteLine();
    Console.WriteLine("死蔵が多い行（上位8・量/戦）: "
                      + string.Join(" / ", spRowHoard.OrderByDescending(kv => kv.Value).Take(8)
                          .Select(kv => $"{kv.Key} {kv.Value / (spStages.Count * (double)spSeedN):F2}")));
    Console.WriteLine();

    // ---- 表E -------------------------------------------------------------
    Console.WriteLine("## 表E. 乗算・反撃への流入");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 読み方 | 受けた強化/戦 | 実効 | その駒を含む行 |");
    Console.WriteLine("|---|---|--:|---|--:|");
    var spReaders = new (string Id, string How, string Eff)[]
    {
        ("hota", "熾火（燃焼中は攻撃力4倍＋貫き）", "**×4**（第58期の乗算監査。与ダメの実効は 7.4 倍）"),
        ("kado", "棘（`PerformAttack` を通らず反撃量を `CurrentAttack` で決める）", "×1（反撃・範囲）"),
        ("zan",  "仇討ち（ターン外・`CurrentAttack` をそのまま）", "×1（ターン外）"),
        ("shiga", "責め苦の追撃（自分の手番の中）", "×1（追撃）"),
    };
    foreach ((string id, string how, string eff) in spReaders)
    {
        double g = spGot.TryGetValue(id, out double gv) ? gv : 0;
        int rows = spBuilds.Count(b => b.F.Occupied().Any(o => o.Item2.Id == id));
        Console.WriteLine($"| {SpLabel(id)} | {how} | {g / spN:F2} | {eff} | {rows} |");
    }
    Console.WriteLine();
    Console.WriteLine("乗算持ち（ホタ）を含む行と含まない行の帰属:");
    Console.WriteLine();
    Console.WriteLine("| 経路 | ホタを含む行 | 含まない行 |");
    Console.WriteLine("|---|--:|--:|");
    foreach ((WhetRoute r, TraitId _, string __, string ___, string ____, string _____) in spRoutes)
    {
        var withH = spAttr[r].Where(x => spBuilds.First(b => b.Name == x.Row).F.Occupied()
                                          .Any(o => o.Item2.Id == "hota")).ToArray();
        var noH = spAttr[r].Where(x => !spBuilds.First(b => b.Name == x.Row).F.Occupied()
                                        .Any(o => o.Item2.Id == "hota")).ToArray();
        Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | "
                          + (withH.Length > 0 ? $"{withH.Average(x => x.D):+0.0;-0.0;0.0}（{withH.Length} 行）" : "—")
                          + " | "
                          + (noH.Length > 0 ? $"{noH.Average(x => x.D):+0.0;-0.0;0.0}（{noH.Length} 行）" : "—")
                          + " |");
    }
    Console.WriteLine();

    // ---- 陰性対照 ---------------------------------------------------------
    Console.WriteLine("## Q6. 陰性対照（供給者を含まない行が ±0.0 であること）");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 対照の行数 | 最大 \\|Δ\\| | 0 でない行 |");
    Console.WriteLine("|---|--:|--:|--:|");
    foreach ((WhetRoute r, TraitId need, string _, string __, string ___, string ____) in spRoutes)
    {
        var neg = spBuilds.Where(b => !SpHas(b, need))
                          .Select(b => Math.Abs(Mean(spBase[b.Name]) - Mean(spDrop[(r, b.Name)]))).ToArray();
        Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | {neg.Length} | {neg.Max():F1} | "
                          + $"{neg.Count(x => x > 0.0001)} |");
    }
    Console.WriteLine();

    return;
}

if (focusId == "burn") { BurnDiag.Run(args, stageIndex); return; }

//     dotnet run --project BattleSim -c Release 0 pace
if (focusId == "pace")
{
    var pcBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> pcStages = EnemyCatalog.Stages;
    const int PcSeedsA = 200;   // 0..199。compare と同じ帯（勝率が balance.md と一致することの検算に使う）
    const int PcSeedsB = 200;   // 200..399。選定に使っていない帯（群の定義がここを使う）
    int pcNb = pcBuilds.Length, pcNw = pcStages.Count;

    string[] pcMeasure = { "決着T", "残存数", "被ダメ総量", "与ダメ総量" };
    const int PcNm = 4;

    // pcVal[帯][物差し][波][編成] = その帯の平均値。
    var pcVal = new double[2][][][];
    for (int band = 0; band < 2; band++)
    {
        pcVal[band] = new double[PcNm][][];
        for (int m = 0; m < PcNm; m++)
        {
            pcVal[band][m] = new double[pcNw][];
            for (int w = 0; w < pcNw; w++) pcVal[band][m][w] = new double[pcNb];
        }
    }
    // 勝率は A 帯だけ（compare と同じ計算なので docs/balance.md と一致する）。
    var pcRate = new double[pcNw][];
    for (int w = 0; w < pcNw; w++) pcRate[w] = new double[pcNb];

    // 味方／敵の切り分けは def.Id でやる（TallyByUnit は両陣営を Def.Id で混ぜて持つ）。
    // **同じ Id が両陣営に出ると集計が壊れる**ので、衝突を数えて検算に出す。
    int pcCollisions = 0;

    var pcSw = System.Diagnostics.Stopwatch.StartNew();
    for (int w = 0; w < pcNw; w++)
    {
        var pcEnemyIds = new HashSet<string>(pcStages[w].Enemy.Occupied().Select(o => o.Item2.Id));
        for (int b = 0; b < pcNb; b++)
        {
            foreach ((int _, UnitDef def) in pcBuilds[b].F.Occupied())
                if (pcEnemyIds.Contains(def.Id)) pcCollisions++;

            for (int band = 0; band < 2; band++)
            {
                int from = band == 0 ? 0 : PcSeedsA;
                int n = band == 0 ? PcSeedsA : PcSeedsB;
                double turns = 0, alive = 0, taken = 0, dealt = 0;
                int wins = 0;
                for (int seed = from; seed < from + n; seed++)
                {
                    BattleResult r = BattleEngine.Run(pcBuilds[b].F, pcStages[w].Enemy, seed, verbose: false);
                    if (r.PlayerWon) wins++;
                    turns += r.Turns;
                    alive += r.PlayerSurvivors;
                    // 受け手側から数える（第13期 Phase DA）。毒・燃焼は source が null なので
                    // 出どころ側からは数えられないが、受け手の DamageTaken には必ず載る。
                    foreach ((string id, UnitTally t) in r.TallyByUnit)
                    {
                        if (pcEnemyIds.Contains(id)) dealt += t.DamageTaken;
                        else taken += t.DamageTaken;   // 味方の召喚（胞子）もこちらに入る
                    }
                }
                pcVal[band][0][w][b] = turns / n;
                pcVal[band][1][w][b] = alive / n;
                pcVal[band][2][w][b] = taken / n;
                pcVal[band][3][w][b] = dealt / n;
                if (band == 0) pcRate[w][b] = wins * 100.0 / n;
            }
        }
    }
    pcSw.Stop();

    Console.WriteLine("# 物差しの比較（pace・第54期）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 {pcNb} × 全 {pcNw} 波 × 2帯（A: seed 0..{PcSeedsA - 1} / B: seed {PcSeedsA}..{PcSeedsA + PcSeedsB - 1}）。");
    Console.WriteLine($"計 {pcNb * pcNw * (PcSeedsA + PcSeedsB):N0} 戦 / 所要 {pcSw.Elapsed.TotalSeconds:F1} 秒。");
    Console.WriteLine();
    Console.WriteLine("**盤面は1つも動かない。** `Stages` も `CompareBuilds()` も読むだけ。");
    Console.WriteLine("A 帯の勝率は `compare` と同じ計算なので `docs/balance.md` と一致する（下の検算）。");
    Console.WriteLine();
    Console.WriteLine($"- **検算1**: 味方と敵で Def.Id が衝突した件数 = **{pcCollisions}**（0 でなければ味方/敵の切り分けが壊れている）");
    Console.WriteLine($"- **検算2**: A 帯・第一波で 100.0% の行数 = **{pcRate[0].Count(x => x >= 100.0)} / {pcNb}**");
    Console.Write("- **検算3**: A 帯の波ごとの平均勝率 = ");
    Console.WriteLine(string.Join(" / ", Enumerable.Range(0, pcNw).Select(w => $"{pcRate[w].Average():F1}")));
    Console.WriteLine();

    // ---- 表A: 第一波の分離度 ----------------------------------------------------------
    Console.WriteLine("## 表A: 物差しごとの分離度（第一波）");
    Console.WriteLine();
    Console.WriteLine("`群` は §1-1 の定義（A 帯で昇順に並べ、B 帯でも前後が完全分離する切れ目の数 + 1）。");
    Console.WriteLine("**第一波の勝率は全行 100.0% で分散 0 なので、相関は第2〜5波の勝率に対して取る。**");
    Console.WriteLine("第一波の物差しが後ろの波の勝率を予測するなら、それは既存の情報の焼き直しである。");
    Console.WriteLine();
    Console.Write("| 物差し | 最小 | 最大 | 中央値 | 標準偏差 | **群** |");
    for (int w = 1; w < pcNw; w++) Console.Write($" r(第{w + 1}波) |");
    Console.WriteLine(" **&#124;r&#124;最大** |");
    Console.Write("|---|--:|--:|--:|--:|--:|");
    for (int w = 1; w < pcNw; w++) Console.Write("--:|");
    Console.WriteLine("--:|");
    for (int m = 0; m < PcNm; m++)
    {
        double[] pa = pcVal[0][m][0], pb = pcVal[1][m][0];
        double mean = pa.Average();
        double sd = Math.Sqrt(pa.Select(x => (x - mean) * (x - mean)).Sum() / pa.Length);
        Console.Write($"| {pcMeasure[m]} | {pa.Min():F2} | {pa.Max():F2} | {PcMedian(pa):F2} | {sd:F2} | **{PcGroups(pa, pb)}** |");
        double rmax = 0;
        for (int w = 1; w < pcNw; w++)
        {
            double r = PcCorr(pa, pcRate[w]);
            rmax = Math.Max(rmax, double.IsNaN(r) ? 0 : Math.Abs(r));
            Console.Write(double.IsNaN(r) ? " — |" : $" {r:+0.00;-0.00} |");
        }
        Console.WriteLine($" **{rmax:F2}** |");
    }
    Console.WriteLine();

    // ---- 表B: 全波の分離度 ------------------------------------------------------------
    Console.WriteLine("## 表B: 全波での分離度");
    Console.WriteLine();
    Console.WriteLine("`勝率群` は勝率そのものを同じ定義で群に割ったもの（比較の基準線）。");
    Console.WriteLine("`分離行` = **勝率が完全に同値の塊（サイズ2以上）が、その物差しの群では2つ以上に割れている**とき、");
    Console.WriteLine("その塊に属する行数。**勝率が拾えていない情報の量**を行数で数えている。");
    Console.WriteLine();
    Console.Write("| 波 | 勝率平均 | 勝率群 |");
    for (int m = 0; m < PcNm; m++) Console.Write($" {pcMeasure[m]}(群/分離行) |");
    Console.WriteLine();
    Console.Write("|---|--:|--:|");
    for (int m = 0; m < PcNm; m++) Console.Write("--:|");
    Console.WriteLine();
    for (int w = 0; w < pcNw; w++)
    {
        Console.Write($"| 第{w + 1}波 | {pcRate[w].Average():F1} | {PcGroups(pcRate[w], pcRate[w])} |");
        for (int m = 0; m < PcNm; m++)
            Console.Write($" {PcGroups(pcVal[0][m][w], pcVal[1][m][w])} / **{PcSplitRows(pcRate[w], pcVal[0][m][w], pcVal[1][m][w])}** |");
        Console.WriteLine();
    }
    Console.WriteLine();
    Console.WriteLine("> `勝率群` の B 帯は A 帯と同じ列を渡している（勝率は同じ帯で測った値どうしを");
    Console.WriteLine("> 比べても意味がないため）。**基準線であって、他の列と同じ厳しさでは無い**");
    Console.WriteLine("> ——勝率群は「A 帯で値が違えば必ず切れる」ので上振れする。");
    Console.WriteLine();

    // ---- 表C: 第一波の決着ターンの両端 --------------------------------------------------
    Console.WriteLine("## 表C: 第一波で決着ターンが最も速い5行と最も遅い5行");
    Console.WriteLine();
    Console.WriteLine("`総攻` は編成の素の攻撃力合計、`範囲` は単体型でない駒の数、`最速` は速さの最大値。");
    Console.WriteLine("敵は 前1 討伐隊の新兵(45/11/6) / 前3 戦斧兵(55/12/5・薙ぎ) / 中央 討伐隊の新兵(45/11/6)。総HP 145。");
    Console.WriteLine();
    // 何が速さを決めているか。静的特徴量（第12期 power の写し。新しい量は作らない）との単相関。
    double[] pcAtk = pcBuilds.Select(x => (double)x.F.Occupied().Sum(o => o.Item2.Attack)).ToArray();
    double[] pcArea = pcBuilds.Select(x => (double)x.F.Occupied().Count(o => o.Item2.Pattern != AttackPattern.Single)).ToArray();
    double[] pcSpd = pcBuilds.Select(x => (double)x.F.Occupied().Max(o => o.Item2.Speed)).ToArray();
    double[] pcHp = pcBuilds.Select(x => (double)x.F.Occupied().Sum(o => o.Item2.MaxHp)).ToArray();
    Console.WriteLine("**第一波の決着Tと静的特徴量の単相関（56行）:** "
        + $"総攻 {PcCorr(pcVal[0][0][0], pcAtk):+0.00;-0.00} / 範囲枚数 {PcCorr(pcVal[0][0][0], pcArea):+0.00;-0.00} / "
        + $"最速 {PcCorr(pcVal[0][0][0], pcSpd):+0.00;-0.00} / 総HP {PcCorr(pcVal[0][0][0], pcHp):+0.00;-0.00}");
    Console.WriteLine();
    Console.WriteLine("**物差しどうしの相関（第一波・56行）:**");
    Console.WriteLine();
    Console.Write("| |");
    for (int m = 0; m < PcNm; m++) Console.Write($" {pcMeasure[m]} |");
    Console.WriteLine();
    Console.Write("|---|");
    for (int m = 0; m < PcNm; m++) Console.Write("--:|");
    Console.WriteLine();
    for (int m = 0; m < PcNm; m++)
    {
        Console.Write($"| {pcMeasure[m]} |");
        for (int m2 = 0; m2 < PcNm; m2++) Console.Write($" {PcCorr(pcVal[0][m][0], pcVal[0][m2][0]):+0.00;-0.00} |");
        Console.WriteLine();
    }
    Console.WriteLine();

    var pcOrder = Enumerable.Range(0, pcNb).OrderBy(b => pcVal[0][0][0][b]).ToArray();
    Console.WriteLine("| | 編成 | 決着T | 残存 | 被ダメ | 与ダメ | 総攻 | 範囲 | 最速 | 中身 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|---|");
    for (int i = 0; i < 5; i++) PcRow("**速**", pcOrder[i]);
    Console.WriteLine("| … | | | | | | | | | |");
    for (int i = 5; i >= 1; i--) PcRow("**遅**", pcOrder[pcNb - i]);
    Console.WriteLine();

    void PcRow(string tag, int b)
    {
        UnitDef[] occ = pcBuilds[b].F.Occupied().Select(o => o.Item2).ToArray();
        int atk = occ.Sum(d => d.Attack);
        int area = occ.Count(d => d.Pattern != AttackPattern.Single);
        int spd = occ.Length == 0 ? 0 : occ.Max(d => d.Speed);
        Console.WriteLine($"| {tag} | {pcBuilds[b].Name} | {pcVal[0][0][0][b]:F2} | {pcVal[0][1][0][b]:F2} "
            + $"| {pcVal[0][2][0][b]:F0} | {pcVal[0][3][0][b]:F0} | {atk} | {area} | {spd} "
            + $"| {string.Join(" ", occ.Select(d => $"{d.Name}({d.Attack}/{d.Speed})"))} |");
    }
    return;

    // 中央値。
    static double PcMedian(double[] v)
    {
        double[] s = v.OrderBy(x => x).ToArray();
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2.0;
    }

    // 群の数（§1-1 の定義）。A で昇順に並べ、B でも前半と後半が完全に分離する位置だけを切れ目にする。
    static int PcGroups(double[] a, double[] b)
    {
        int n = a.Length;
        if (n == 0) return 0;
        int[] perm = Enumerable.Range(0, n).OrderBy(i => a[i]).ThenBy(i => i).ToArray();
        // 接尾辞の最小値を先に作る（O(n)）。
        var sufMin = new double[n + 1];
        sufMin[n] = double.PositiveInfinity;
        for (int k = n - 1; k >= 0; k--) sufMin[k] = Math.Min(sufMin[k + 1], b[perm[k]]);
        int cuts = 0;
        double preMax = double.NegativeInfinity;
        for (int k = 0; k < n - 1; k++)
        {
            preMax = Math.Max(preMax, b[perm[k]]);
            if (preMax < sufMin[k + 1]) cuts++;
        }
        return cuts + 1;
    }

    // 「勝率では同値だが物差しでは分かれる行」の数（§1-2 の定義）。
    static int PcSplitRows(double[] rate, double[] a, double[] b)
    {
        int n = rate.Length;
        // 物差しの群番号を各行に振る（PcGroups と同じ切り方）。
        int[] perm = Enumerable.Range(0, n).OrderBy(i => a[i]).ThenBy(i => i).ToArray();
        var sufMin = new double[n + 1];
        sufMin[n] = double.PositiveInfinity;
        for (int k = n - 1; k >= 0; k--) sufMin[k] = Math.Min(sufMin[k + 1], b[perm[k]]);
        var gid = new int[n];
        int g = 0;
        double preMax = double.NegativeInfinity;
        for (int k = 0; k < n; k++)
        {
            gid[perm[k]] = g;
            preMax = Math.Max(preMax, b[perm[k]]);
            if (k < n - 1 && preMax < sufMin[k + 1]) g++;
        }
        int rows = 0;
        foreach (IGrouping<double, int> cls in Enumerable.Range(0, n).GroupBy(i => rate[i]))
        {
            int[] idx = cls.ToArray();
            if (idx.Length < 2) continue;
            if (idx.Select(i => gid[i]).Distinct().Count() >= 2) rows += idx.Length;
        }
        return rows;
    }

    // ピアソン相関。片方の分散が 0 なら定義できないので NaN。
    static double PcCorr(double[] a, double[] b)
    {
        double ma = a.Average(), mb = b.Average();
        double num = 0, da = 0, db = 0;
        for (int i = 0; i < a.Length; i++)
        {
            num += (a[i] - ma) * (b[i] - mb);
            da += (a[i] - ma) * (a[i] - ma);
            db += (b[i] - mb) * (b[i] - mb);
        }
        return da == 0 || db == 0 ? double.NaN : num / Math.Sqrt(da * db);
    }
}

// 第105期 —— 手番の値段を測る（調査）。
//
// **盤面は1ビットも動かない。** 駒・特性・数値・engine の判定・境界の規則に触っていない
// （足したのは観測専用の計数と、`TakeTurn` に被せた枠だけ。受け入れ条件は `compare` 305 セル 0 件）。
//
// **診断の名前が `turn` ではないのは、`turn` が第60期（火選りを手番へ降ろす）で埋まっているから。**
// 指示書 §4 は「新しい診断 `turn` を1本足す」と書いているが、そこは
// 「既存の診断は1文字も書き換えない」と両立しない。名前だけを `tempo` に替えた。
//
//     dotnet run --project BattleSim -c Release 0 tempo phase0   # §2（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 tempo run      # 表B（駒ごとの生の表）
//     dotnet run --project BattleSim -c Release 0 tempo tables   # 表A〜D
//     dotnet run --project BattleSim -c Release 0 tempo check    # 表E（自己検査 (a)〜(f)）
if (focusId == "tempo")
{
    string tpMode = args.Length > 2 ? args[2] : "tables";
    var tpCompare = CompareBuilds();
    var tpCross = CrossBuilds();
    var tpBuilds = tpCompare.Concat(tpCross).ToArray();
    IReadOnlyList<EnemyCatalog.Stage> tpStages = EnemyCatalog.Stages;
    const int TpSeeds = 200;   // compare と同じ帯

    var tpName = UnitCatalog.All.ToDictionary(d => d.Id, d => d.Name);

    if (tpMode == "phase0")
    {
        Console.WriteLine("# 手番の値段 —— 実装前の地図（第105期 Phase 0）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `docs/` には置かない。");
        Console.WriteLine();

        Console.WriteLine("## 0-1. 既存の計数で何が取れたか（足したものだけを列挙する）");
        Console.WriteLine();
        Console.WriteLine("`UnitTally` に**手番そのものを数えた列は1本も無かった**。");
        Console.WriteLine("近いものは3本あるが、どれも手番の代理にならない:");
        Console.WriteLine();
        Console.WriteLine("| 既存の列 | 何を数えているか | 手番の代理にならない理由 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| `Attacks` | `PerformAttack` を通った回数 | 術・溜め・潰れた手番が落ちる。反撃・割り込みが混じる |");
        Console.WriteLine("| `Charges` | 溜めた回数 | 溜めだけ |");
        Console.WriteLine("| `Slumbers` | まどろんだ回数 | 潰れ方の1つだけ |");
        Console.WriteLine();
        Console.WriteLine("**足したのは `UnitTally` の 17 本と ctx 側の総計 13 本。**");
        Console.WriteLine("どれも誰も読んで分岐しない・`verbose` 非依存・**版に依らない**");
        Console.WriteLine("（規則の分岐より手前、または規則を1つも見ない場所に置いた）。");
        Console.WriteLine();
        Console.WriteLine("    TurnsTaken / TurnAttacks / TurnSkills / TurnCharges / TurnStalls");
        Console.WriteLine("    StallStun / StallSlumber / StallImmobile / StallCanAct / TurnsSurrendered");
        Console.WriteLine("    DmgOutInTurn / DmgOutOffTurn / HealOutInTurn / HealOutOffTurn");
        Console.WriteLine("    StatusOutInTurn / StatusOutOffTurn");
        Console.WriteLine();

        Console.WriteLine("## 0-2. 手番の中と外をどこで切ったか（**境界の定義**）");
        Console.WriteLine();
        Console.WriteLine("定義は `BattleContext.InOwnTurn` の**1箇所だけ**。**3つの積**:");
        Console.WriteLine();
        Console.WriteLine("1. `TakeTurn` の枠の中であること（`TurnActor` が立っている）");
        Console.WriteLine("2. **出どころがその枠の主であること**");
        Console.WriteLine("3. `InReaction` / `InInterrupt` の中でないこと");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 中／外 | 理由 |");
        Console.WriteLine("|---|:-:|---|");
        Console.WriteLine("| 自分の手番の通常攻撃・術・溜め | 中 | (1)(2)(3) すべて |");
        Console.WriteLine("| 責め苦の追撃（`OnAfterAttack`） | 中 | 自分の手番の中で自分が出どころ |");
        Console.WriteLine("| 棘の反撃（`OnDamaged`） | 外 | 殴った側の枠の中だが**出どころが違う**（(2) で外れる） |");
        Console.WriteLine("| `OnTurnStart`（瘴気・逸らし・駆り立て・吸い） | 外 | 行動順ループの**外側**（(1) で外れる） |");
        Console.WriteLine("| 仇討ち・軋み・追い打ち | 外 | (2) と (3) の両方で外れる |");
        Console.WriteLine("| `OnDeath` / `OnAnyDeath` / `OnAllyDeath` | 外 | 出どころが違う |");
        Console.WriteLine("| **再行動（第104期）で振った分** | **中** | `TakeTurn` を丸ごと渡しているので枠が立つ |");
        Console.WriteLine();
        Console.WriteLine("**`OnTurnStart` が「外」なのは意図した線**（第61期「`OnTurnStart` は speed = ∞ の席」）。");
        Console.WriteLine("あそこは手番の順序の外にあるので、手番の値段とは別の通貨で払われている。");
        Console.WriteLine();

        Console.WriteLine("## 0-3. 帰属できない出力（**誰のものでもない出力**）");
        Console.WriteLine();
        Console.WriteLine("| 通貨 | 帰属の手掛かり | 帰属できない経路 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 与ダメージ | `ApplyDamage` の `source` | **毒・燃焼の刻み**（`source` が null。第13期） |");
        Console.WriteLine("| 回復 | 第94期 (T2) の印（`Mark.Owner`） | `Heal` は源を引数で受け取らない |");
        Console.WriteLine("| 状態異常 | 同上（`NoteCarry` の中で読む） | engine が書く `IdleTurn`・境界の一括操作 |");
        Console.WriteLine();
        Console.WriteLine("3つとも `None` の桶へ入れて、`In + Off + None == All` を自己検査 (d) で突き合わせる。");
        Console.WriteLine();

        Console.WriteLine("## 0-4. 再行動が手番数に入ること");
        Console.WriteLine();
        Console.WriteLine("`EncoreRule` は `NoteEncore` の中から `TakeTurn` を呼ぶので、**枠がもう一度立つ**");
        Console.WriteLine("——`TurnsTaken` は自動で +1 される。**入れ子になる**（`HandleDeath` は");
        Console.WriteLine("誰かの `TakeTurn` の中から呼ばれる）ので、`TurnActor` は1本の変数ではなく");
        Console.WriteLine("**退避して戻す**形で持つ。自己検査 (c) が `TurnLoopCalls + EncoreFired` と突き合わせる。");
        Console.WriteLine();

        Console.WriteLine("## 0-5. `SurrenderedTurn` の読み方（第103期の訂正）");
        Console.WriteLine();
        Console.WriteLine("engine は `CanAct` が偽の駒にも **`IdleTurn` を無条件に立てる**ので、");
        Console.WriteLine("生の counter を見ると必ず「売れた」になる。数えるのは**買い手が通す判定**");
        Console.WriteLine("`Trait.SurrenderedTurn` のほう。**潰れた手番についてだけ1回**問う。");
        Console.WriteLine();
        Console.WriteLine("問い合わせが観測を汚さないように、印を落としてログを黙らせてから呼んでいる");
        Console.WriteLine("——`CanAct` は Sluggish / Sever がログを出し、Sever は counter を読む。");
        Console.WriteLine("**乱数は1つも引かない**（`CanAct` の4つの実装のどれも `Roll` を呼ばない）。");
        Console.WriteLine();

        Console.WriteLine("## 0-6. §1-4 の数え直し（**戦闘0回**）");
        Console.WriteLine();
        var tpAll = UnitCatalog.All;
        var tpSkillOnly = tpAll.Where(d => d.Actions is { Count: > 0 }
                                        && d.Actions.All(a => a.Kind == ActionKind.Skill)).ToArray();
        var tpNoActions = tpAll.Where(d => d.Actions is null || d.Actions.Count == 0).ToArray();
        var tpHasActions = tpAll.Where(d => d.Actions is { Count: > 0 }).ToArray();
        Console.WriteLine($"ロスターは **{tpAll.Count} 枚**（第103期に 52 枚で確定）。");
        Console.WriteLine();
        Console.WriteLine($"- `Actions` を持つ駒: **{tpHasActions.Length} 枚** — {string.Join(" / ", tpHasActions.Select(d => d.Name))}");
        Console.WriteLine($"- 術しか撃たない駒: **{tpSkillOnly.Length} 枚** — {string.Join(" / ", tpSkillOnly.Select(d => d.Name))}");
        Console.WriteLine($"- `Actions` を持たない駒: **{tpNoActions.Length} 枚**（{tpNoActions.Length * 100.0 / tpAll.Count:0.0}%）");
        Console.WriteLine();
        Console.WriteLine("**`TURN_SLOT_PLAN` の「46体中30体」は再現しない**——分母も分子も動いている。");
        Console.WriteLine("しかも「素の通常攻撃しか振らない」は `Actions` の有無だけでは決まらない");
        Console.WriteLine("——**不動のカドは `Actions = [Skill]` を持っている**（`Immobile` が拒むのは");
        Console.WriteLine("`ActionKind.Attack` だけなので、カドの手番は潰れずに術として通る）。**実測は表D で出す。**");
        Console.WriteLine();

        Console.WriteLine("## 0-7. 予測（**測る前に書く**）");
        Console.WriteLine();
        Console.WriteLine("1. **カドの手番外の割合は 100%、ドルガは 0%**（§1-3 の対照）。");
        Console.WriteLine("2. **ロスターの過半は手番型**（`Actions` を持たない駒が圧倒的多数）。");
        Console.WriteLine("3. **1手番あたりの与ダメージはドルガ（攻38）とキリ（攻1）で 38 倍開く。ノミは 10〜21。**");
        Console.WriteLine("4. **稼働率は 35〜40% 帯に集中する**（第86期）。**再行動で刻み手だけが上に外れる。**");
        Console.WriteLine();
        Console.WriteLine("**予測4 は測る前から怪しい**——第86期の 35〜40% は「**特性の発火** ÷ 決着T」で、");
        Console.WriteLine("この期が測るのは「**手番** ÷ 決着T」。生き残った駒は毎ターン手番を持つので上に寄るはず。");
        Console.WriteLine("**判定は指示書どおり出したうえで、ノノ・ハリの実測を併記する**（第86期の分子はこの2枚）。");
        return;
    }

    // ---------------------------------------------------------------------------------
    // 測定（73行 × 5波 × seed 0..199 = 73,000 戦）
    // ---------------------------------------------------------------------------------
    const int TpN = 21;
    const int TpTurns = 0, TpAtkT = 1, TpSkill = 2, TpCharge = 3, TpStall = 4,
              TpStun = 5, TpSlumber = 6, TpImmobile = 7, TpCanAct = 8, TpSold = 9,
              TpDmgIn = 10, TpDmgOff = 11, TpHealIn = 12, TpHealOff = 13,
              TpStatIn = 14, TpStatOff = 15, TpBattles = 16, TpSettle = 17,
              TpAttacks = 18, TpDmgEnemy = 19, TpEncore = 20;

    // tpAcc[0] = 第2〜5波（判定の分母・規約 (G10)）／ tpAcc[1] = 第一波（参考）
    var tpAcc = new Dictionary<string, long[]>[2];
    for (int i = 0; i < 2; i++) tpAcc[i] = new Dictionary<string, long[]>();

    long[] TpGet(int band, string id)
    {
        if (!tpAcc[band].TryGetValue(id, out long[]? a)) tpAcc[band][id] = a = new long[TpN];
        return a;
    }

    long gLoop = 0, gEncore = 0, gTurnsAll = 0;
    long gDmgAll = 0, gDmgIn = 0, gDmgOff = 0, gDmgNone = 0;
    long gHealAll = 0, gHealIn = 0, gHealOff = 0, gHealNone = 0;
    long gStatAll = 0, gStatIn = 0, gStatOff = 0, gStatNone = 0;
    int tpCollisions = 0, tpBattlesRun = 0;

    var tpSw = System.Diagnostics.Stopwatch.StartNew();
    for (int w = 0; w < tpStages.Count; w++)
    {
        int wband = w == 0 ? 1 : 0;
        var tpEnemyIds = new HashSet<string>(tpStages[w].Enemy.Occupied().Select(o => o.Item2.Id));
        for (int b = 0; b < tpBuilds.Length; b++)
        {
            // 味方側の駒だけを引く（`TallyByUnit` は両陣営を `Def.Id` で混ぜて持つ）。
            // 同じ Id が両陣営に出ると集計が壊れるので、衝突を数えて検算に出す。
            var tpMine = tpBuilds[b].F.Occupied().Select(o => o.Item2.Id).Distinct().ToArray();
            foreach (string id in tpMine) if (tpEnemyIds.Contains(id)) tpCollisions++;

            for (int seed = 0; seed < TpSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(tpBuilds[b].F, tpStages[w].Enemy, seed, verbose: false);
                tpBattlesRun++;
                gLoop += r.TurnLoopCalls; gEncore += r.EncoreFired;
                foreach (UnitTally t0 in r.TallyByUnit.Values) gTurnsAll += t0.TurnsTaken;
                gDmgAll += r.TurnDmgAll; gDmgIn += r.TurnDmgIn; gDmgOff += r.TurnDmgOff; gDmgNone += r.TurnDmgNone;
                gHealAll += r.TurnHealAll; gHealIn += r.TurnHealIn; gHealOff += r.TurnHealOff; gHealNone += r.TurnHealNone;
                gStatAll += r.TurnStatusAll; gStatIn += r.TurnStatusIn; gStatOff += r.TurnStatusOff; gStatNone += r.TurnStatusNone;

                foreach (string id in tpMine)
                {
                    if (tpEnemyIds.Contains(id)) continue;
                    long[] a = TpGet(wband, id);
                    a[TpBattles]++; a[TpSettle] += r.Turns;
                    if (!r.TallyByUnit.TryGetValue(id, out UnitTally? t)) continue;
                    a[TpTurns] += t.TurnsTaken; a[TpAtkT] += t.TurnAttacks;
                    a[TpSkill] += t.TurnSkills; a[TpCharge] += t.TurnCharges; a[TpStall] += t.TurnStalls;
                    a[TpStun] += t.StallStun; a[TpSlumber] += t.StallSlumber;
                    a[TpImmobile] += t.StallImmobile; a[TpCanAct] += t.StallCanAct;
                    a[TpSold] += t.TurnsSurrendered;
                    a[TpDmgIn] += t.DmgOutInTurn; a[TpDmgOff] += t.DmgOutOffTurn;
                    a[TpHealIn] += t.HealOutInTurn; a[TpHealOff] += t.HealOutOffTurn;
                    a[TpStatIn] += t.StatusOutInTurn; a[TpStatOff] += t.StatusOutOffTurn;
                    a[TpAttacks] += t.Attacks; a[TpDmgEnemy] += t.DamageToEnemy;
                    a[TpEncore] += t.EncoreFires;
                }
            }
        }
    }
    tpSw.Stop();

    double TpPer(long[] a, int i) => a[TpBattles] == 0 ? 0 : (double)a[i] / a[TpBattles];
    double TpUsed(long[] a) => a[TpAtkT] + a[TpSkill] + a[TpCharge];
    // 稼働率 ＝ **使えた手番** ÷ 決着ターン数（潰れた手番は稼働ではない）。
    double TpUtil(long[] a) => a[TpSettle] == 0 ? 0 : TpUsed(a) / a[TpSettle];
    // 手番外の割合。分母が 0 なら NaN（「その通貨を1点も出していない」）。
    double TpOff(long[] a, int inI, int offI)
    {
        double d = a[inI] + a[offI];
        return d == 0 ? double.NaN : a[offI] / d;
    }
    string TpPct(double v) => double.IsNaN(v) ? "—" : $"{v * 100:0.0}%";

    var tpIds = tpAcc[0].Keys.OrderBy(k => k).ToArray();
    string TpNm(string id) => tpName.TryGetValue(id, out string? n) ? n : id;

    // 分類（§1-3 の線: 手番外の割合が 50% を超えたら手番外型）。
    // **通貨ごとに割れる駒は「混合」**。分母が 0 の通貨は判定に使わない。
    (string Kind, string Detail) TpClass(long[] a)
    {
        var lab = new[] { "与ダメ", "回復", "状態" };
        var offs = new[] { TpOff(a, TpDmgIn, TpDmgOff), TpOff(a, TpHealIn, TpHealOff), TpOff(a, TpStatIn, TpStatOff) };
        var live = Enumerable.Range(0, 3).Where(i => !double.IsNaN(offs[i])).ToArray();
        if (live.Length == 0) return ("出力なし", "3通貨とも 0");
        string detail = string.Join(" / ", live.Select(i => $"{lab[i]} {TpPct(offs[i])}"));
        bool anyOff = live.Any(i => offs[i] > 0.5), anyIn = live.Any(i => offs[i] <= 0.5);
        return (anyOff && anyIn ? "混合" : anyOff ? "手番外型" : "手番型", detail);
    }

    void TpTableB()
    {
        Console.WriteLine("## 表B. 駒ごとの手番（第2〜5波・73行 × 4波 × seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("`手番/戦` は**回ってきた**手番（再行動を含む）。`使/戦` はそのうち使えた手番");
        Console.WriteLine("（通常攻撃 ＋ 術 ＋ 溜め）。`稼働` ＝ `使/戦` ÷ 決着T。");
        Console.WriteLine("`内ダメ/手番` は §1-2 の「1手番あたりの出力」＝**手番内の与ダメ ÷ 回ってきた手番**、");
        Console.WriteLine("`外ダメ/戦` は**手番を1つも使わずに出した与ダメ**。`外%` は手番外の割合。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席戦 | 手番/戦 | 使/戦 | 稼働 | 攻 | 術 | 溜 | 潰 | 売 | 再行動 | 内ダメ/手番 | 外ダメ/戦 | 打点/振 | 与ダメ外% | 回復外% | 状態外% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        // 並びは §1-2 の「1手番あたりの出力」＝**手番内の与ダメ ÷ 手番数**の降順。
        foreach (string id in tpIds.OrderByDescending(k =>
                 {
                     long[] a = tpAcc[0][k];
                     return a[TpTurns] == 0 ? -1 : (double)a[TpDmgIn] / a[TpTurns];
                 }))
        {
            long[] a = tpAcc[0][id];
            double used = TpUsed(a);
            double perTurn = a[TpTurns] == 0 ? 0 : (double)a[TpDmgIn] / a[TpTurns];
            double offPer = a[TpBattles] == 0 ? 0 : (double)a[TpDmgOff] / a[TpBattles];
            double perSwing = a[TpAttacks] == 0 ? 0 : (double)a[TpDmgEnemy] / a[TpAttacks];
            Console.WriteLine($"| {TpNm(id)} | {a[TpBattles]} | {TpPer(a, TpTurns):0.00} | {used / Math.Max(1, a[TpBattles]):0.00} "
                + $"| {TpUtil(a):0.00} | {TpPer(a, TpAtkT):0.00} | {TpPer(a, TpSkill):0.00} | {TpPer(a, TpCharge):0.00} "
                + $"| {TpPer(a, TpStall):0.00} | {TpPer(a, TpSold):0.00} | {TpPer(a, TpEncore):0.00} "
                + $"| {perTurn:0.0} | {offPer:0.0} | {perSwing:0.0} "
                + $"| {TpPct(TpOff(a, TpDmgIn, TpDmgOff))} | {TpPct(TpOff(a, TpHealIn, TpHealOff))} | {TpPct(TpOff(a, TpStatIn, TpStatOff))} |");
        }
        Console.WriteLine();
    }

    if (tpMode == "run")
    {
        Console.WriteLine("# 手番の値段 —— 駒ごとの表（第105期 `tempo run`）");
        Console.WriteLine();
        Console.WriteLine($"73行（`compare` {tpCompare.Length} ＋ 交差帯 {tpCross.Length}） × 5波 × seed 0..{TpSeeds - 1} = **{tpBattlesRun:#,0} 戦**（{tpSw.Elapsed.TotalSeconds:0.0} 秒）。");
        Console.WriteLine();
        TpTableB();
        return;
    }

    if (tpMode == "check")
    {
        Console.WriteLine("# 手番の値段 —— 自己検査（第105期 `tempo check`）");
        Console.WriteLine();
        Console.WriteLine($"分母: **{tpBattlesRun:#,0} 戦**（{tpSw.Elapsed.TotalSeconds:0.0} 秒）。");
        Console.WriteLine();
        Console.WriteLine("| 検査 | 内容 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine("| (a) | `compare` 305 セルが `docs/balance.md` と 0 件 | **別コマンド**（`0 compare` の diff） | — |");
        // (b) 版に依らないこと。**盤面が動かない台**で見る（第91期 (e) の直し方）——
        // 刻み手（キリ・ノミ）を含まない行では再行動が原理的に発火せず、ソムはどの行にも居ないので、
        // `EncoreRule` / `BetrayRule` を切っても盤面は1ビットも動かない。そこで計数が一致すること。
        {
            var tpQuiet = tpBuilds.Where(x => !x.F.Occupied().Any(o =>
                o.Item2.Id == "kiri" || o.Item2.Id == "nomi" || o.Item2.Id == "som")).ToArray();
            long[] v0 = new long[TpN], v1 = new long[TpN];
            long l0 = 0, l1 = 0;
            int nRow = Math.Min(8, tpQuiet.Length), nSeed = 20;
            for (int w = 0; w < tpStages.Count; w++)
                for (int b2 = 0; b2 < nRow; b2++)
                    for (int seed = 0; seed < nSeed; seed++)
                        for (int ver = 0; ver < 2; ver++)
                        {
                            BattleResult rr = ver == 0
                                ? BattleEngine.Run(tpQuiet[b2].F, tpStages[w].Enemy, seed, verbose: false)
                                : BattleEngine.Run(tpQuiet[b2].F, tpStages[w].Enemy, seed, verbose: false,
                                                   betray: new BetrayRule(false, false),
                                                   encore: new EncoreRule(false));
                            long[] v = ver == 0 ? v0 : v1;
                            if (ver == 0) l0 += rr.TurnLoopCalls; else l1 += rr.TurnLoopCalls;
                            foreach (UnitTally t in rr.TallyByUnit.Values)
                            {
                                v[TpTurns] += t.TurnsTaken; v[TpAtkT] += t.TurnAttacks;
                                v[TpSkill] += t.TurnSkills; v[TpCharge] += t.TurnCharges;
                                v[TpStall] += t.TurnStalls; v[TpStun] += t.StallStun;
                                v[TpSlumber] += t.StallSlumber; v[TpImmobile] += t.StallImmobile;
                                v[TpCanAct] += t.StallCanAct; v[TpSold] += t.TurnsSurrendered;
                                v[TpDmgIn] += t.DmgOutInTurn; v[TpDmgOff] += t.DmgOutOffTurn;
                                v[TpHealIn] += t.HealOutInTurn; v[TpHealOff] += t.HealOutOffTurn;
                                v[TpStatIn] += t.StatusOutInTurn; v[TpStatOff] += t.StatusOutOffTurn;
                            }
                        }
            int diff = Enumerable.Range(0, TpN).Count(i => v0[i] != v1[i]) + (l0 == l1 ? 0 : 1);
            Console.WriteLine($"| (b) | 足した計数が版に依らない（刻み手を含まない {nRow} 行 × 5波 × {nSeed} seed で "
                + $"`EncoreRule` / `BetrayRule` を切る） | 食い違った列 **{diff}** 件（延べ手番 {v0[TpTurns]:#,0}） | {(diff == 0 ? "○" : "×")} |");
        }
        bool okC = gTurnsAll == gLoop + gEncore;
        Console.WriteLine($"| (c) | 手番数の合計 ＝ 行動順ループ ＋ 再行動 | {gTurnsAll:#,0} ＝ {gLoop:#,0} ＋ {gEncore:#,0} = {gLoop + gEncore:#,0} | {(okC ? "○" : "×")} |");
        bool okD1 = gDmgIn + gDmgOff + gDmgNone == gDmgAll;
        bool okD2 = gHealIn + gHealOff + gHealNone == gHealAll;
        bool okD3 = gStatIn + gStatOff + gStatNone == gStatAll;
        Console.WriteLine($"| (d) 与ダメ | 中 ＋ 外 ＋ 誰のものでもない ＝ 全部 | {gDmgIn:#,0} ＋ {gDmgOff:#,0} ＋ {gDmgNone:#,0} = {gDmgAll:#,0} | {(okD1 ? "○" : "×")} |");
        Console.WriteLine($"| (d) 回復 | 同上 | {gHealIn:#,0} ＋ {gHealOff:#,0} ＋ {gHealNone:#,0} = {gHealAll:#,0} | {(okD2 ? "○" : "×")} |");
        Console.WriteLine($"| (d) 状態 | 同上（回数） | {gStatIn:#,0} ＋ {gStatOff:#,0} ＋ {gStatNone:#,0} = {gStatAll:#,0} | {(okD3 ? "○" : "×")} |");
        Console.WriteLine("| (e) | `docs/` 10ファイルの差分 | **別コマンド** | — |");
        Console.WriteLine("| (f) | `ctx.PickOne` の箇所数 | **別コマンド**（grep） | — |");
        Console.WriteLine($"| 追加 | 味方と敵で `Def.Id` が衝突した件数 | {tpCollisions} | {(tpCollisions == 0 ? "○" : "×")} |");
        Console.WriteLine();
        Console.WriteLine("## 誰のものでもない出力の割合");
        Console.WriteLine();
        Console.WriteLine("| 通貨 | 手番の中 | 手番の外 | 誰のものでもない |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| 与ダメージ | {gDmgIn * 100.0 / Math.Max(1, gDmgAll):0.0}% | {gDmgOff * 100.0 / Math.Max(1, gDmgAll):0.0}% | {gDmgNone * 100.0 / Math.Max(1, gDmgAll):0.0}% |");
        Console.WriteLine($"| 回復 | {gHealIn * 100.0 / Math.Max(1, gHealAll):0.0}% | {gHealOff * 100.0 / Math.Max(1, gHealAll):0.0}% | {gHealNone * 100.0 / Math.Max(1, gHealAll):0.0}% |");
        Console.WriteLine($"| 状態異常（回数） | {gStatIn * 100.0 / Math.Max(1, gStatAll):0.0}% | {gStatOff * 100.0 / Math.Max(1, gStatAll):0.0}% | {gStatNone * 100.0 / Math.Max(1, gStatAll):0.0}% |");
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表A〜D
    // ---------------------------------------------------------------------------------
    Console.WriteLine("# 手番の値段（第105期 `tempo tables`）");
    Console.WriteLine();
    Console.WriteLine($"73行（`compare` {tpCompare.Length} ＋ 交差帯 {tpCross.Length}） × 5波 × seed 0..{TpSeeds - 1} = **{tpBattlesRun:#,0} 戦**（{tpSw.Elapsed.TotalSeconds:0.0} 秒）。");
    Console.WriteLine("**判定の分母は第2〜5波**（規約 (G10)）。第一波は参考として併記する。");
    Console.WriteLine();

    Console.WriteLine("## 表A. Q1 —— 既知の値の再現");
    Console.WriteLine();
    long[] TpA(string id) => tpAcc[0].TryGetValue(id, out long[]? a) ? a : new long[TpN];
    long[] aNomi = TpA("nomi"), aKiri = TpA("kiri"), aHagi = TpA("hagi"), aKado = TpA("kado");
    long[] aNono = TpA("nono"), aHari = TpA("hari");

    double nomiSwing = aNomi[TpAttacks] == 0 ? 0 : (double)aNomi[TpDmgEnemy] / aNomi[TpAttacks];
    double kiriSwing = aKiri[TpAttacks] == 0 ? 0 : (double)aKiri[TpDmgEnemy] / aKiri[TpAttacks];
    double hagiUsed = TpUsed(aHagi);
    bool q1a = nomiSwing >= 10 && nomiSwing <= 21;
    bool q1b = Math.Abs(kiriSwing - 1.0) < 0.005;
    bool q1c = hagiUsed == 0;
    bool q1d = aKado[TpTurns] > 0 && aKado[TpStall] == aKado[TpTurns] && aKado[TpImmobile] > 0;
    double utilNono = TpUtil(aNono), utilHari = TpUtil(aHari);
    bool q1e = utilNono >= 0.35 && utilNono <= 0.40 && utilHari >= 0.35 && utilHari <= 0.40;

    Console.WriteLine("| # | 既知の値 | 出どころ | 実測 | 判定 |");
    Console.WriteLine("|---|---|---|--:|:-:|");
    Console.WriteLine($"| 1 | ノミの打点/振が 10〜21 | 第104期 | **{nomiSwing:0.0}** | {(q1a ? "○" : "×")} |");
    Console.WriteLine($"| 2 | キリの打点/振が常に 1 | `ThinBlade` | **{kiriSwing:0.00}** | {(q1b ? "○" : "×")} |");
    Console.WriteLine($"| 3 | ハギの手番数が 0 | `CanAct => false` | 使えた手番 **{hagiUsed / Math.Max(1, aHagi[TpBattles]):0.00}**/戦（回ってきた手番は {TpPer(aHagi, TpTurns):0.00}） | {(q1c ? "○" : "×")} |");
    Console.WriteLine($"| 4 | カドの手番が全部潰れる | `Immobile` | 潰れ **{aKado[TpStall]:#,0}** / 手番 **{aKado[TpTurns]:#,0}**（うち不動 {aKado[TpImmobile]:#,0}・痺れ {aKado[TpStun]:#,0}） | {(q1d ? "○" : "×")} |");
    Console.WriteLine($"| 5 | 稼働率が 35〜40% 帯 | 第86期（分子はノノ・ハリ） | ノノ **{utilNono * 100:0.0}%** / ハリ **{utilHari * 100:0.0}%** | {(q1e ? "○" : "×")} |");
    Console.WriteLine();
    Console.WriteLine("### 稼働率の分布（ロスター全体・第2〜5波）");
    Console.WriteLine();
    var tpUtils = tpIds.Select(k => TpUtil(tpAcc[0][k])).Where(v => v > 0).OrderBy(v => v).ToArray();
    if (tpUtils.Length > 0)
    {
        double med = tpUtils[tpUtils.Length / 2];
        int inBand = tpUtils.Count(v => v >= 0.35 && v <= 0.40);
        Console.WriteLine($"最小 {tpUtils[0] * 100:0.0}% ／ 中央 {med * 100:0.0}% ／ 最大 {tpUtils[^1] * 100:0.0}%。");
        Console.WriteLine($"**35〜40% に入る駒は {inBand} / {tpUtils.Length} 枚。**");
    }
    Console.WriteLine();

    TpTableB();

    Console.WriteLine("## 表C. 分類（手番型 / 手番外型 / 混合）");
    Console.WriteLine();
    Console.WriteLine("線は §1-3 のとおり **手番外の割合 50%**。**通貨ごとに割れる駒は「混合」**。");
    Console.WriteLine();
    var tpKinds = tpIds.ToDictionary(k => k, k => TpClass(tpAcc[0][k]));
    foreach (string kind in new[] { "手番外型", "混合", "手番型", "出力なし" })
    {
        var members = tpIds.Where(k => tpKinds[k].Kind == kind).ToArray();
        Console.WriteLine($"### {kind}（{members.Length} 枚）");
        Console.WriteLine();
        if (members.Length == 0) { Console.WriteLine("（0 枚）"); Console.WriteLine(); continue; }
        Console.WriteLine("| 駒 | 内訳（手番外の割合） |");
        Console.WriteLine("|---|---|");
        foreach (string k in members) Console.WriteLine($"| {TpNm(k)} | {tpKinds[k].Detail} |");
        Console.WriteLine();
    }

    Console.WriteLine("### Q2 —— 両端");
    Console.WriteLine();
    double kadoOff = TpOff(aKado, TpDmgIn, TpDmgOff);
    long[] aDolga = TpA("dolga");
    double dolgaOff = TpOff(aDolga, TpDmgIn, TpDmgOff);
    bool q2 = kadoOff == 1.0 && dolgaOff == 0.0;
    Console.WriteLine($"- カド（`Immobile`）の与ダメの手番外の割合: **{TpPct(kadoOff)}**（期待 100%）");
    Console.WriteLine($"- ドルガ（素の通常攻撃のみ）: **{TpPct(dolgaOff)}**（期待 0%）");
    Console.WriteLine($"- **判定: {(q2 ? "○" : "×")}**");
    Console.WriteLine();

    Console.WriteLine("### Q3 —— 第103期の順位を説明するか");
    Console.WriteLine();
    Console.WriteLine("第103期は「餌の代金は手番。**上位3枚は手番で殴らない駒**（ハギ・カド・ヒヨ）」だった。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 第103期の Δ相乗の順位 | この器具の分類 | 期待 | 判定 |");
    Console.WriteLine("|---|--:|---|---|:-:|");
    var q3Rows = new (string Id, string Rank, string Want)[]
    {
        ("hagi", "1 / 51", "手番外型"), ("kado", "2 / 51", "手番外型"), ("hiyo", "3 / 51", "手番外型"),
        ("borg", "32 / 51", "手番型"), ("dolga", "46 / 51", "手番型"),
    };
    int q3ok = 0;
    foreach ((string id, string rank, string want) in q3Rows)
    {
        string got = tpKinds.TryGetValue(id, out var kk) ? kk.Kind : "（在席なし）";
        bool ok = got == want;
        if (ok) q3ok++;
        Console.WriteLine($"| {TpNm(id)} | {rank} | **{got}** | {want} | {(ok ? "○" : "×")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**{q3ok} / {q3Rows.Length} 一致。**");
    Console.WriteLine();

    Console.WriteLine("## 表D. 手番の内訳の分布と §1-4 の数え直し");
    Console.WriteLine();
    long sTurns = tpIds.Sum(k => tpAcc[0][k][TpTurns]);
    long sAtk = tpIds.Sum(k => tpAcc[0][k][TpAtkT]), sSk = tpIds.Sum(k => tpAcc[0][k][TpSkill]);
    long sCh = tpIds.Sum(k => tpAcc[0][k][TpCharge]), sSt = tpIds.Sum(k => tpAcc[0][k][TpStall]);
    long sSold = tpIds.Sum(k => tpAcc[0][k][TpSold]);
    long sStun = tpIds.Sum(k => tpAcc[0][k][TpStun]), sSlm = tpIds.Sum(k => tpAcc[0][k][TpSlumber]);
    long sImm = tpIds.Sum(k => tpAcc[0][k][TpImmobile]), sCan = tpIds.Sum(k => tpAcc[0][k][TpCanAct]);
    Console.WriteLine("| 内訳 | 延べ | 割合 |");
    Console.WriteLine("|---|--:|--:|");
    Console.WriteLine($"| 通常攻撃 | {sAtk:#,0} | {sAtk * 100.0 / Math.Max(1, sTurns):0.0}% |");
    Console.WriteLine($"| 術 | {sSk:#,0} | {sSk * 100.0 / Math.Max(1, sTurns):0.0}% |");
    Console.WriteLine($"| 溜め | {sCh:#,0} | {sCh * 100.0 / Math.Max(1, sTurns):0.0}% |");
    Console.WriteLine($"| **潰れた** | {sSt:#,0} | {sSt * 100.0 / Math.Max(1, sTurns):0.0}% |");
    Console.WriteLine($"| （うち痺れ） | {sStun:#,0} | 潰れの {sStun * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine($"| （うちまどろみ） | {sSlm:#,0} | 潰れの {sSlm * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine($"| （うち不動） | {sImm:#,0} | 潰れの {sImm * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine($"| （うちその他の `CanAct` 偽） | {sCan:#,0} | 潰れの {sCan * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine($"| **売れた手番** | {sSold:#,0} | 潰れの {sSold * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine();

    var tpAll2 = UnitCatalog.All;
    var tpNever = tpIds.Where(k => TpUsed(tpAcc[0][k]) == 0).Select(TpNm).ToArray();
    var tpAllStall = tpIds.Where(k => tpAcc[0][k][TpTurns] > 0 && tpAcc[0][k][TpStall] == tpAcc[0][k][TpTurns]).Select(TpNm).ToArray();
    var tpSkillDefs = tpAll2.Where(d => d.Actions is { Count: > 0 } && d.Actions.All(a => a.Kind == ActionKind.Skill)).ToArray();
    var tpPlain = tpIds.Where(k =>
    {
        long[] a = tpAcc[0][k];
        return a[TpAtkT] > 0 && a[TpSkill] == 0 && a[TpCharge] == 0;
    }).ToArray();
    Console.WriteLine($"- **手番を1度も使えない駒**: {tpNever.Length} 枚 — {(tpNever.Length == 0 ? "—" : string.Join(" / ", tpNever))}");
    Console.WriteLine($"- **手番の全部が潰れる駒**: {tpAllStall.Length} 枚 — {(tpAllStall.Length == 0 ? "—" : string.Join(" / ", tpAllStall))}");
    Console.WriteLine($"- **術しか撃たない駒**（定義から）: {tpSkillDefs.Length} 枚 — {string.Join(" / ", tpSkillDefs.Select(d => d.Name))}");
    Console.WriteLine($"- **実測で通常攻撃しか振らない駒**: {tpPlain.Length} / {tpIds.Length} 枚（在席した駒が分母）");
    Console.WriteLine($"- 73行に在席した駒: **{tpIds.Length} / {tpAll2.Count} 枚**");
    Console.WriteLine();

    Console.WriteLine("## 参考. 第一波（**判定に使わない**・規約 (G10)）");
    Console.WriteLine();
    long w1Turns = tpAcc[1].Values.Sum(a => a[TpTurns]), w1St = tpAcc[1].Values.Sum(a => a[TpStall]);
    Console.WriteLine($"延べ手番 {w1Turns:#,0}・潰れた手番 {w1St:#,0}（{w1St * 100.0 / Math.Max(1, w1Turns):0.0}%）。");
    Console.WriteLine($"第2〜5波の潰れ率は {sSt * 100.0 / Math.Max(1, sTurns):0.0}%。");
    Console.WriteLine();
    return;
}

if (focusId == "hold") { HoldDiag.Run(args, stageIndex); return; }



//     dotnet run --project BattleSim -c Release 0 spread
// ==================================================================================
// 第107期 —— 保留を閉じる（席・ムド・ハリ）。指示書は design/PHASE107_HOLD2_SPEC.md。
//
// **既存の診断は1文字も書き換えていない。** (S1)(S2)(S3) をモードで分けてある
// （報告書とコミットも別々にする——`docs/balance.md` の差分がどれの帰結か読めなくなるため）。
// ==================================================================================
// 第108期 —— 尾灯（`TaillightTrait`）の受け入れ確認。**測定ではない。**
//
// 指示書 §4 の「必要なら最小の確認用モードを1つ足す」。**第109期の `tomo`（測定）とは別物**で、
// ここがやるのは自己検査 (a)〜(g) だけ——トモは `Presets.Compare` にも `Presets.Cross` にも
// 入っていない（受け入れ 3）ので、**盤面に出す唯一の場所がこの診断のローカル台になる**。
//
// 台は診断のローカルに組む（`gradient` / `aim` / `route` / `sever` と同じ扱い）。
// **`Presets` も `UnitCatalog.All` も1行も読み替えない。**
//
// **ログの文字列を数えている。** UI は `LogKind` を見るという規約に反して見えるが、
// 確かめたいのは「灯がどの駒に何点載っているか」の推移そのもので、
// **消えたことは盤面の値に痕跡を残さない**（`gullet log` / `yoke log` / `sever` と同じ理由）。
//
//     dotnet run --project BattleSim -c Release 0 taillight
// ==================================================================================
if (focusId == "taillight")
{
    const int TlSeeds = 40;
    IReadOnlyList<EnemyCatalog.Stage> tlStages = EnemyCatalog.Stages;

    static Formation TlF(params UnitDef[] m)
    {
        var f = new Formation();
        for (int i = 0; i < m.Length; i++) f[i] = m[i];
        return f;
    }

    // 台。**速さの並びを手で決めてある**——「自分を除いて最も遅い味方」の答えが台ごとに一意に決まる。
    var tlBenches = new (string Name, Formation F, string Expect)[]
    {
        // 速さ: ゴルム3 < ドルガ6 < ノミ7 < キリ12。トモ（3）は自分なので対象外。
        ("(b) 一意", TlF(UnitCatalog.Golm, UnitCatalog.Dolga, UnitCatalog.Tomo, UnitCatalog.Nomi, UnitCatalog.Kiri),
            UnitCatalog.Golm.Name),
        // 速さ: バン2 = セッキ2 < ドルガ6 < ノミ7。**同速は席番号の昇順**なので前1のバン。
        ("(b) 同速→席番号", TlF(UnitCatalog.Ban, UnitCatalog.Sekki, UnitCatalog.Tomo, UnitCatalog.Dolga, UnitCatalog.Nomi),
            UnitCatalog.Ban.Name),
        // 速さ: ガルド4 < ザン5 < ノミ7 < キリ12。**ガルドは支援拒否**なので飛ばしてザンへ。
        ("(b) 支援拒否を飛ばす", TlF(UnitCatalog.Gald, UnitCatalog.Zan, UnitCatalog.Tomo, UnitCatalog.Nomi, UnitCatalog.Kiri),
            UnitCatalog.Zan.Name),
        // トモ2枚。**互いに譲り合っても無限に往復しない**ことの確認（1ホップ）。
        ("(f) トモ2枚", TlF(UnitCatalog.Tomo, UnitCatalog.Tomo, UnitCatalog.Golm, UnitCatalog.Dolga, UnitCatalog.Nomi),
            UnitCatalog.Golm.Name),
    };

    Console.WriteLine("# 第108期 —— 尾灯（`TaillightTrait`）の受け入れ確認");
    Console.WriteLine();
    Console.WriteLine("**測定ではない**（第109期に `tomo` を作る）。トモは `Presets.Compare` にも "
        + "`Presets.Cross` にも入っていないので、盤面に出す唯一の場所がこの診断のローカル台。");
    Console.WriteLine();
    Console.WriteLine("台は 5波 × seed 0.." + (TlSeeds - 1) + "。灯 = " + TaillightTrait.Lumen + "。");
    Console.WriteLine();

    // ------------------------------------------------------------------------------
    // ログを再生して「いま誰に何点の灯が載っているか」を追う。
    // 灯る:  「… が ○○ に灯をともした（攻撃 +5 → n）」
    // 消える:「○○ の灯が消えた（攻撃 -m）」
    // 譲る:  「… は前へ出ず、灯した ○○ に道を譲る」
    // ------------------------------------------------------------------------------
    var tlRows = new List<(string Bench, int Wave, int Seed, int Turns,
                           int Lit, int Doused, int Switches, int Yields, int Hop,
                           int MaxLampsAtOnce, int BadTarget, int BadDouse, int BadYieldPerTurn,
                           int Fires, int Idle, int NoDeath, int NoTarget, int NetLeft)>();

    foreach (var (name, form, expect) in tlBenches)
        for (int w = 0; w < tlStages.Count; w++)
            for (int seed = 0; seed < TlSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(form, tlStages[w].Enemy, seed, verbose: true);

                // **判定は「トモ1枚あたり1ターン1回」**。トモが2枚の台では 2 回が正しい
                // （それぞれが自分の手番で1回ずつ譲る。1ホップが止めるのは<b>入れ子</b>のほう）。
                int tomoCount = form.Occupied().Count(o => ReferenceEquals(o.Def, UnitCatalog.Tomo));

                var lamp = new Dictionary<string, int>();   // 駒名 → いま載っている灯
                int maxAtOnce = 0, badTarget = 0, badDouse = 0, badYield = 0;
                int lit = 0, doused = 0, turn = 0, yieldsThisTurn = 0, badYieldTurns = 0;

                foreach (LogLine line in r.Log)
                {
                    string t = line.Text;
                    if (line.Kind == LogKind.Turn)
                    {
                        if (yieldsThisTurn > tomoCount) badYieldTurns++;
                        yieldsThisTurn = 0;
                        turn++;
                        continue;
                    }

                    int a = t.IndexOf(" に灯をともした", StringComparison.Ordinal);
                    if (a >= 0)
                    {
                        int b = t.IndexOf(" が ", StringComparison.Ordinal);
                        string who = t.Substring(b + 3, a - b - 3);
                        // (b) 台ごとに答えは一意。**まだ誰も倒れていないあいだだけ**判定する
                        //     （味方が倒れると「最も遅い生存味方」が変わるのは仕様どおりなので）。
                        if (lamp.Count == 0 && lit == 0 && who != expect) badTarget++;
                        lamp[who] = lamp.TryGetValue(who, out int had) ? had + TaillightTrait.Lumen
                                                                      : TaillightTrait.Lumen;
                        lit += TaillightTrait.Lumen;
                        maxAtOnce = Math.Max(maxAtOnce, lamp.Count(kv => kv.Value > 0));
                        continue;
                    }

                    int c = t.IndexOf(" の灯が消えた（攻撃 -", StringComparison.Ordinal);
                    if (c >= 0)
                    {
                        // **`ctx.Log` は先頭の空白を落として `LogLine.Indent` に分けて持つ**
                        // （BattleEngine.Log）ので、駒名は行頭から始まる。
                        string who = t.Substring(0, c);
                        int amt = int.Parse(new string(t.Substring(c).Where(char.IsAsciiDigit).ToArray()));
                        // (c) 消える量は、その駒に載っていた累計とちょうど同じ
                        if (!lamp.TryGetValue(who, out int had2) || had2 != amt) badDouse++;
                        lamp[who] = 0;
                        doused += amt;
                        continue;
                    }

                    if (t.Contains("は前へ出ず、灯した", StringComparison.Ordinal)) yieldsThisTurn++;
                }
                if (yieldsThisTurn > tomoCount) badYieldTurns++;
                badYield = badYieldTurns;

                int sw = 0, yl = 0, hop = 0, fires = 0, idle = 0, nod = 0, not_ = 0;
                foreach (var kv in r.TallyByUnit)
                {
                    sw += kv.Value.TaillightSwitches; yl += kv.Value.TaillightYields;
                    hop += kv.Value.TaillightBlockedHop; fires += kv.Value.TaillightFires;
                    idle += kv.Value.TaillightIdle; nod += kv.Value.TaillightNoDeath;
                    not_ += kv.Value.TaillightNoTarget;
                }

                tlRows.Add((name, w + 1, seed, r.Turns, lit, doused, sw, yl, hop,
                            maxAtOnce, badTarget, badDouse, badYield, fires, idle, nod, not_,
                            lamp.Values.Sum()));
            }

    Console.WriteLine("## 表A —— 台ごとの集計（1戦あたり）");
    Console.WriteLine();
    Console.WriteLine("| 台 | 戦 | 決着T | 灯/戦 | 消/戦 | 替/戦 | 譲/戦 | 空振り/戦 | 敵未撃破の手番/戦 | 同時に灯る最大 | 1ホップ | 消の照合ずれ | 収支ずれ |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var g in tlRows.GroupBy(x => x.Bench))
    {
        double n = g.Count();
        Console.WriteLine($"| {g.Key} | {n:F0} | {g.Average(x => x.Turns):F1} | {g.Sum(x => x.Lit) / n:F1} "
            + $"| {g.Sum(x => x.Doused) / n:F1} | {g.Sum(x => x.Switches) / n:F2} | {g.Sum(x => x.Yields) / n:F2} "
            + $"| {g.Sum(x => x.Idle) / n:F2} | {g.Sum(x => x.NoDeath) / n:F2} "
            + $"| **{g.Max(x => x.MaxLampsAtOnce)}** | {g.Sum(x => x.Hop)} "
            + $"| {g.Sum(x => x.BadDouse)} | {g.Count(x => x.Lit - x.Doused != x.NetLeft)} |");
    }
    Console.WriteLine();

    // ------------------------------------------------------------------------------
    // 自己検査
    // ------------------------------------------------------------------------------
    var tlChecks = new List<(string Tag, string What, string Got, bool Ok)>();

    // (a) ハリと縫いの実装が残っている
    bool hariAlive = UnitCatalog.Hari.Id == "hari"
                     && UnitCatalog.Hari.Traits.Contains(TraitId.Suture)
                     && TraitCatalog.Get(TraitId.Suture) is SutureTrait;
    string hariWhere = UnitCatalog.All.Any(u => ReferenceEquals(u, UnitCatalog.Hari)) ? "`All` に居る" : "`All` から外れている";
    tlChecks.Add(("(a)", "`UnitCatalog.Hari` と `SutureTrait` / `SutureRule` / `SutureFireRule` が残置されている",
        $"{UnitCatalog.Hari.Name}（{hariWhere}）・既定 {SutureRule.Default} / {SutureFireRule.Default}", hariAlive));

    // (b) 対象は自分を除く最も遅い味方（同速は席番号昇順・支援拒否は飛ばす）
    foreach (var g in tlRows.GroupBy(x => x.Bench).Where(g => g.Key.StartsWith("(b)", StringComparison.Ordinal)))
        tlChecks.Add(("(b)", g.Key + " の初灯が期待どおり",
            "ずれ " + g.Sum(x => x.BadTarget) + " 件 / 灯 " + g.Sum(x => x.Fires) + " 回",
            g.Sum(x => x.BadTarget) == 0));

    // (c)(c') は**トモ1枚の台でだけ判定する**。
    // この照合は駒名をキーにした帳簿なので、**同名のトモが2枚いると2つの灯を分離できない**
    // ——2枚とも同じ「最も遅い味方」を照らすと lamp[その味方] に両方の灯が混ざり、
    // 片方が消したときの量（自分のぶんだけ）と食い違う。**器具の限界であって盤面の不整合ではない**
    // （下の表A で (f) の台にだけ ずれが出ていることがその証拠）。
    var tlSolo = tlRows.Where(x => !x.Bench.StartsWith("(f)", StringComparison.Ordinal)).ToList();

    // (c) 消える量は、その駒に載っていた累計とちょうど同じ
    tlChecks.Add(("(c)", "対象が変わったとき、前の灯が**ちょうど載っていた量だけ**消える（トモ1枚の台）",
        "ずれ " + tlSolo.Sum(x => x.BadDouse) + " 件 / 消灯 " + tlSolo.Count(x => x.Doused > 0) + " 戦"
        + "（トモ2枚の台は同名で分離できないので除外。ずれ " + tlRows.Sum(x => x.BadDouse) + " 件）",
        tlSolo.Sum(x => x.BadDouse) == 0));

    // (c') 収支: 載った総量 − 消した総量 = 戦闘終了時に残っている灯
    int tlBadNet = tlSolo.Count(x => x.Lit - x.Doused != x.NetLeft);
    tlChecks.Add(("(c')", "灯った総量 − 消した総量 ＝ 終了時に残っている灯（収支が閉じる・トモ1枚の台）",
        "ずれ " + tlBadNet + " 戦 / " + tlSolo.Count + " 戦", tlBadNet == 0));

    // (d) 灯は1体にしか灯らない
    // **トモ1枚につき灯は1つ**。2枚の台で 2 体に灯るのは規則どおり（それぞれが1体を照らす）。
    int tlMax1 = tlRows.Where(x => !x.Bench.StartsWith("(f)", StringComparison.Ordinal))
                       .Max(x => x.MaxLampsAtOnce);
    int tlMax2 = tlRows.Where(x => x.Bench.StartsWith("(f)", StringComparison.Ordinal))
                       .Max(x => x.MaxLampsAtOnce);
    tlChecks.Add(("(d)", "同時に灯が載っている駒が**トモの枚数まで**（1枚の台で 1 体・2枚の台で 2 体）",
        "1枚の台 最大 " + tlMax1 + " 体 / 2枚の台 最大 " + tlMax2 + " 体（全 " + tlRows.Count + " 戦）",
        tlMax1 <= 1 && tlMax2 <= 2));

    // (e) 手番の譲渡は1ターン1回以下
    tlChecks.Add(("(e)", "手番の譲渡が**トモ1枚あたり1ターン1回以下**",
        "違反 " + tlRows.Sum(x => x.BadYieldPerTurn) + " ターン / 譲渡 " + tlRows.Sum(x => x.Yields) + " 回",
        tlRows.Sum(x => x.BadYieldPerTurn) == 0));

    // (f) 1ホップ（トモ2枚でも往復しない）
    var tlTwo = tlRows.Where(x => x.Bench.StartsWith("(f)", StringComparison.Ordinal)).ToList();
    tlChecks.Add(("(f)", "トモ2枚の台が**全戦とも完走する**（無限に譲り合わない）",
        tlTwo.Count + " 戦とも決着（平均 " + (tlTwo.Count > 0 ? tlTwo.Average(x => x.Turns) : 0).ToString("F1")
        + "T）・1ホップで止めた回数 " + tlTwo.Sum(x => x.Hop), tlTwo.Count > 0 && tlTwo.All(x => x.Turns > 0)));

    // (g) ctx.PickOne を新たに使っていない（第89期 (h)。候補2個以上で Roll を消費する）
    static int TlCount(string hay, string needle)
    {
        int n = 0;
        for (int i = hay.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = hay.IndexOf(needle, i + 1, StringComparison.Ordinal)) n++;
        return n;
    }
    string? tlRoot = Directory.GetCurrentDirectory();
    while (tlRoot is not null && !File.Exists(Path.Combine(tlRoot, "CLAUDE.md")))
        tlRoot = Directory.GetParent(tlRoot)?.FullName;
    int tlPick = 0, tlPickInTrait = 0;
    if (tlRoot is not null)
    {
        foreach (string f in new[] { Path.Combine(tlRoot, "BattleCore", "Traits.cs"),
                                     Path.Combine(tlRoot, "BattleCore", "BattleEngine.cs") })
            if (File.Exists(f))
                tlPick += TlCount(File.ReadAllText(f), "PickOne(");

        string tf = Path.Combine(tlRoot, "BattleCore", "Traits.cs");
        if (File.Exists(tf))
        {
            string src = File.ReadAllText(tf);
            int i = src.IndexOf("public sealed class TaillightTrait", StringComparison.Ordinal);
            if (i >= 0)
            {
                int j = src.IndexOf("public sealed class", i + 20, StringComparison.Ordinal);
                if (j < 0) j = src.IndexOf("public readonly record struct", i, StringComparison.Ordinal);
                if (j < 0) j = src.Length;
                tlPickInTrait = TlCount(src.Substring(i, j - i), "PickOne(");
            }
        }
    }
    tlChecks.Add(("(g)", "`PickOne(` の素の出現数が **26** のまま（第94期以降不変）で、"
        + "**`TaillightTrait` の中は 0**",
        tlPick + " 箇所（うち `TaillightTrait` の中 " + tlPickInTrait + "）",
        tlPick == 26 && tlPickInTrait == 0));

    Console.WriteLine("## 表B —— 自己検査");
    Console.WriteLine();
    Console.WriteLine("| | 内容 | 実測 | 判定 |");
    Console.WriteLine("|---|---|---|:-:|");
    foreach (var (tag, what, got, ok) in tlChecks)
        Console.WriteLine($"| **{tag}** | {what} | {got} | {(ok ? "○" : "**×**")} |");
    Console.WriteLine();
    Console.WriteLine($"**{tlChecks.Count(x => x.Ok)} / {tlChecks.Count} 件が ○。**");
    Console.WriteLine();

    // ------------------------------------------------------------------------------
    // 1戦の監査（読んで確かめるための生ログ）
    // ------------------------------------------------------------------------------
    // 灯が**移る**戦を選ぶ（対象の死 → 消灯 → 次へ、が1本の中で読める戦）。
    var tlPick2 = tlRows.Where(x => x.Bench == "(b) 一意" && x.Switches > 0 && x.Yields > 0)
                        .OrderBy(x => x.Wave).ThenBy(x => x.Seed).FirstOrDefault();
    Console.WriteLine("## 表C —— 1戦の監査（`(b) 一意`・第" + Math.Max(1, tlPick2.Wave) + "波・seed " + tlPick2.Seed + "）");
    Console.WriteLine();
    Console.WriteLine("**灯が移る戦**（対象が倒れる → 消灯 → 次に遅い味方へ）を選んである。");
    Console.WriteLine();
    Console.WriteLine("```");
    BattleResult tlAudit = BattleEngine.Run(tlBenches[0].F,
        tlStages[Math.Max(0, tlPick2.Wave - 1)].Enemy, tlPick2.Seed, verbose: true);
    foreach (LogLine line in tlAudit.Log)
        if (line.Kind == LogKind.Turn || line.Text.Contains("灯", StringComparison.Ordinal)
            || line.Text.Contains("道を譲る", StringComparison.Ordinal)
            || line.Text.Contains("倒れた", StringComparison.Ordinal))
            Console.WriteLine(line.Text);
    Console.WriteLine("```");
    Console.WriteLine();
    return;
}

if (focusId == "tomo" && args.Length > 2 && args[2] == "yield") { TomoDiag.Yield(args, stageIndex); return; }

if (focusId == "tomo") { TomoDiag.Run(args, stageIndex); return; }

if (focusId == "hold2") { Hold2Diag.Run(args, stageIndex); return; }

if (focusId == "spread")
{
    var spreadBuilds = CompareBuilds();
    // 第3引数に除外語（カンマ区切りの部分一致）を渡すと、その行を外して測る。
    // **行を足した期に「同じ行数で前後を測り直す」ためだけの窓口**（CLAUDE.md の
    // 「計測器と測定対象を同時に動かさない」）。省略すれば従来どおり全行。
    string spreadDrop = args.Length > 2 ? args[2] : "";
    if (spreadDrop.Length > 0)
        spreadBuilds = spreadBuilds
            .Where(b => !spreadDrop.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();
    IReadOnlyList<EnemyCatalog.Stage> spreadStages = EnemyCatalog.Stages;
    const int SpreadSeeds = 200;   // compare と同じ。数字を突き合わせるので変えない

    int nb = spreadBuilds.Length, nw = spreadStages.Count;

    // rate[波][編成] = 勝率(%)。compare と同じ計算（同じ seed 帯・同じ Run）なので
    // docs/balance.md の表とセルが一致する。ずれたらどちらかの集計が間違っている。
    var rate = new double[nw][];
    for (int w = 0; w < nw; w++)
    {
        rate[w] = new double[nb];
        for (int b = 0; b < nb; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < SpreadSeeds; seed++)
                if (BattleEngine.Run(spreadBuilds[b].F, spreadStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
            rate[w][b] = wins * 100.0 / SpreadSeeds;
        }
    }

    Console.WriteLine("# 波の分離度（spread）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 {nb} × 全 {nw} 波、seed 0..{SpreadSeeds - 1} の {SpreadSeeds} 試行。");
    Console.WriteLine("compare と同じ計算なので、セルは docs/balance.md と一致する。");
    Console.WriteLine();

    Console.WriteLine("## 1. 波ごとの飽和");
    Console.WriteLine();
    Console.WriteLine("| 波 | 平均 | 100%の編成 | 0%の編成 | 中間帯(5〜95%) | 標準偏差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    var sd = new double[nw];
    for (int w = 0; w < nw; w++)
    {
        double[] v = rate[w];
        double mean = v.Average();
        sd[w] = Math.Sqrt(v.Select(x => (x - mean) * (x - mean)).Sum() / v.Length);
        int top = v.Count(x => x >= 100.0), bottom = v.Count(x => x <= 0.0);
        int mid = v.Count(x => x > 5.0 && x < 95.0);
        Console.WriteLine($"| 第{w + 1}波 | {mean:F1} | {top} / {nb} | {bottom} | {mid} | {sd[w]:F1} |");
    }
    Console.WriteLine();
    int allTop = Enumerable.Range(0, nb).Count(b => Enumerable.Range(1, Math.Max(0, nw - 2)).All(w => rate[w][b] >= 100.0));
    Console.WriteLine($"第2〜{nw - 1}波すべて 100% の編成: **{allTop} / {nb}**"
                    + "（この編成たちにとって、中間の波は存在しないのと同じ）");
    Console.WriteLine();

    Console.WriteLine("## 2. 波間の相関");
    Console.WriteLine();
    Console.WriteLine("編成ごとの勝率を波の間で相関させる。高いほど「同じ資源に課金している」。");
    Console.WriteLine("分散 0 の波（全編成が同じ勝率）は相関が定義できないので `—`。");
    Console.WriteLine();
    Console.WriteLine("| |" + string.Concat(Enumerable.Range(1, nw - 1).Select(w => $" 第{w + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, nw - 1).Select(_ => "--:|")));
    for (int i = 0; i < nw - 1; i++)
    {
        var cells = new List<string>();
        for (int j = 1; j < nw; j++)
        {
            if (j <= i) { cells.Add(" |"); continue; }   // 下三角は空欄（対称なので上だけ出す）
            double r = Corr(rate[i], rate[j]);
            cells.Add(double.IsNaN(r) ? " — |" : $" {r:+0.00;-0.00} |");
        }
        Console.WriteLine($"| **第{i + 1}波** |" + string.Concat(cells));
    }
    Console.WriteLine();

    Console.WriteLine("## 3. 固有の勝者・敗者");
    Console.WriteLine();
    Console.WriteLine("**固有の勝者** = その波でだけ 100%（他のどの波でも 100% 未満）の編成。");
    Console.WriteLine("**固有の敗者** = その波でだけ 0%（他のどの波でも 0% 超）の編成。");
    Console.WriteLine("両方とも空の波は、独立した波として存在していない。");
    Console.WriteLine();
    Console.WriteLine("**第一波は比較から外してある。** チュートリアル波として全編成 100% を意図的に");
    Console.WriteLine("維持しているので、比較に入れると第2〜5波の固有の勝者が**恒等的に 0** になる");
    Console.WriteLine("——第三波を何に作り替えても動かない指標だった。第一波自身も判定しない。");
    Console.WriteLine();
    for (int w = 0; w < nw; w++)
    {
        Console.WriteLine($"### 第{w + 1}波");
        Console.WriteLine();
        if (w == 0)
        {
            // 第一波は全編成 100%。「他のどの波でも 100% 未満」を要求する判定の比較対象に
            // 入れると、第2〜5波の固有の勝者が恒等的に 0 になる（第20期 逆位の副産物）。
            // ここを直さずに波を作り替えると、主判定が到達不能なまま前後比較をすることになる。
            Console.WriteLine("- （比較対象外。全編成 100% のチュートリアル波）");
            Console.WriteLine();
            continue;
        }

        var winners = new List<string>();
        var losers = new List<string>();
        for (int b = 0; b < nb; b++)
        {
            bool onlyTop = rate[w][b] >= 100.0
                        && Enumerable.Range(1, nw - 1).All(o => o == w || rate[o][b] < 100.0);
            bool onlyBottom = rate[w][b] <= 0.0
                           && Enumerable.Range(1, nw - 1).All(o => o == w || rate[o][b] > 0.0);
            if (onlyTop) winners.Add(spreadBuilds[b].Name);
            if (onlyBottom) losers.Add(spreadBuilds[b].Name);
        }
        Console.WriteLine($"- 固有の勝者 ({winners.Count}): " + (winners.Count == 0 ? "**なし**" : string.Join(" / ", winners)));
        Console.WriteLine($"- 固有の敗者 ({losers.Count}): " + (losers.Count == 0 ? "**なし**" : string.Join(" / ", losers)));
        Console.WriteLine();
    }

    // ---- 4. 主判定の固定行集合（第60期に確定）---------------------------------------
    Console.WriteLine($"## 4. 主判定 {Baseline.PrimaryRows.Length} 行（歯止めはこの集合の上で測る）");
    Console.WriteLine();
    Console.WriteLine("**全行平均は行を足すたびに勝手に動く量である。** 第41〜59期の「歯止めを割った」は");
    Console.WriteLine("すべて分母の話で、波そのものは第40期から1つも動いていない（第59期 9-4 → 第60期に確定）。");
    Console.WriteLine("主判定は**軸の被覆**で選んだ固定集合なので、新機構の行を足しても分母が動かない。");
    Console.WriteLine();
    Console.WriteLine("**情報セルの定義はここだけ第59期 9-1 に揃えてある**——");
    Console.WriteLine("`0 < x < 100` を**第2〜5波**で数える（§1 の中間帯は `5 < x < 95`。別の量なので混ぜない）。");
    Console.WriteLine();
    var primary = Baseline.PrimaryRows
        .Select(n => Array.FindIndex(spreadBuilds, b => b.Name == n))
        .Where(i => i >= 0).ToArray();
    var missing = Baseline.PrimaryRows.Where(n => !spreadBuilds.Any(b => b.Name == n)).ToArray();
    if (missing.Length > 0)
    {
        Console.WriteLine($"**警告: 主判定行のうち {missing.Length} 行が見つからない** —— "
                        + string.Join(" / ", missing));
        Console.WriteLine("（`CompareBuilds()` の行名を変えたら `Baseline.PrimaryRows` も直すこと）");
        Console.WriteLine();
    }
    int Info59(int b) => Enumerable.Range(1, nw - 1).Count(w => rate[w][b] > 0.0 && rate[w][b] < 100.0);
    Console.WriteLine("| # | 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 情報セル |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|");
    for (int k = 0; k < primary.Length; k++)
    {
        int b = primary[k];
        Console.WriteLine($"| {k + 1} | {spreadBuilds[b].Name} "
                        + string.Concat(Enumerable.Range(0, nw).Select(w => $"| {rate[w][b]:0.0}% "))
                        + $"| **{Info59(b)}** |");
    }
    Console.WriteLine();
    string Fold(string tag, IEnumerable<int> idx)
    {
        int[] a = idx.ToArray();
        int info = a.Sum(Info59);
        return $"| {tag} " + string.Concat(Enumerable.Range(0, nw)
                 .Select(w => $"| {a.Average(b => rate[w][b]):0.0}% "))
             + $"| {info} / {info / (double)a.Length:0.00} |";
    }
    Console.WriteLine("| 分母 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 情報セル 合計 / 平均 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    Console.WriteLine(Fold($"**主判定 {primary.Length} 行**", primary));
    Console.WriteLine(Fold($"全 {nb} 行", Enumerable.Range(0, nb)));
    Console.WriteLine();
    double fifth = primary.Average(b => rate[nw - 1][b]);
    Console.WriteLine($"**第五波: 主判定 {fifth:0.0}% / 全 {nb} 行 {rate[nw - 1].Average():0.0}%**");
    Console.WriteLine($"**歯止め: 主判定 {Baseline.PrimaryFifthFloor:0.0}%** "
                    + $"——確定時（第60期）の主判定の値 − 5.0pt。現在の余裕は "
                    + $"**{fifth - Baseline.PrimaryFifthFloor:+0.0;-0.0;0.0}pt**。");
    Console.WriteLine();
    Console.WriteLine("**線は「明らかに成立しなくなる線」であって調整目標ではない。**");
    Console.WriteLine("第59期の提案20行がちょうど 40.0 で線上に乗ったのは偶然で、**線上に置くのが一番まずい**");
    Console.WriteLine("——だから確定時の値そのものではなく **−5.0pt** に置いてある。");
    Console.WriteLine();

    return;

    // ピアソン相関。片方の分散が 0 なら定義できないので NaN を返す（呼び出し側で — に置く）。
    static double Corr(double[] a, double[] b)
    {
        double ma = a.Average(), mb = b.Average();
        double num = 0, da = 0, db = 0;
        for (int i = 0; i < a.Length; i++)
        {
            num += (a[i] - ma) * (b[i] - mb);
            da += (a[i] - ma) * (a[i] - ma);
            db += (b[i] - mb) * (b[i] - mb);
        }
        return da <= 0 || db <= 0 ? double.NaN : num / Math.Sqrt(da * db);
    }
}

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


if (focusId == "turn" && (args.Length > 2 ? args[2] : "") == "phase0")
{
    var fpBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> fpStages = EnemyCatalog.Stages;
    const int FpSeeds = 200;   // compare / spread / whet / burn / favor と同じ帯

    var fpRows = fpBuilds.Where(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hiyo))).ToArray();

    Console.WriteLine("# 火選りを手番へ降ろす前の地図（第60期 Phase 0）");
    Console.WriteLine();
    Console.WriteLine("**盤面は1つも動かさない。** 0-1〜0-3 と 0-5 は戦闘0回、0-4 と 0-6 は既存の経路を読むだけ。");
    Console.WriteLine();

    // ---- 0-5 を先に出す（以降の表の分母になる）---------------------------------------
    Console.WriteLine("## 0-5. ヒヨを含む行（数え直し）");
    Console.WriteLine();
    Console.WriteLine($"`CompareBuilds()` は **{fpBuilds.Count()} 行**。うちヒヨを含むのは **{fpRows.Length} 行**。");
    Console.WriteLine();
    Console.WriteLine("| # | 行 | 前1 | 前3 | 中央 | 後1 | 後3 | ヒヨの席 | 次数 |");
    Console.WriteLine("|--:|---|---|---|---|---|---|:-:|--:|");
    for (int i = 0; i < fpRows.Length; i++)
    {
        Formation f = fpRows[i].F;
        string Cell(int s) { UnitDef? d = f[s]; return d is null ? "—" : d.Name; }
        int fs = f.Occupied().First(o => ReferenceEquals(o.Def, UnitCatalog.Hiyo)).Slot;
        int deg = Enumerable.Range(0, FormationRules.PlayableSlotCount)
                            .Count(s => s != fs && f[s] is not null && FormationRules.AreAdjacent(fs, s));
        Console.WriteLine($"| {i + 1} | {fpRows[i].Name} | {Cell(0)} | {Cell(1)} | {Cell(2)} | {Cell(3)} | {Cell(4)} "
                        + $"| **{FormationRules.SeatNames[fs]}** | {deg} |");
    }
    Console.WriteLine();

    // ---- 0-1. 止めうる経路 -----------------------------------------------------------
    Console.WriteLine("## 0-1. 移設後に火選りを止めうる経路（call-site の全数）");
    Console.WriteLine();
    Console.WriteLine("`BattleEngine` の行動順ループで手番が飛ぶ・行動が変わる箇所を上から全部挙げる。");
    Console.WriteLine();
    Console.WriteLine("| # | 経路 | 実装 | ヒヨに効くか |");
    Console.WriteLine("|--:|---|---|---|");
    Console.WriteLine("| 1 | 痺れ（`StatusKeys.Stun`） | 行動順ループの先頭で `continue` | **効く**（下で数える） |");
    Console.WriteLine("| 2 | まどろみ | `ctx.Colossus.Slumber && HasTrait(Colossus)` | **効かない**（規則の既定が `false`・保持者はゴルムだけ） |");
    Console.WriteLine("| 3 | `CanAct` 偽 | `actor.Traits.All(t => t.CanAct(...))` | **効かない**（上書きは のろま／不動／断ち／追い打ちの4本で、ヒヨは1つも持たない） |");
    Console.WriteLine("| 4 | `ActionIndex` の周期 | `Actions` の要素を順に消費 | **効かない**（`[Skill]` の1要素。空回りしない） |");
    Console.WriteLine("| 5 | 死亡 | `IsAlive` | 効く（移設前も同じ） |");
    Console.WriteLine();
    Console.WriteLine("**痺れの書き手は3本**（`SetCounter(StatusKeys.Stun, 1)` の全数から自傷を除いた）。");
    Console.WriteLine();
    Console.WriteLine("| 書き手 | 特性 | 誰に付くか | 移設後のヒヨに届くか |");
    Console.WriteLine("|---|---|---|---|");
    Console.WriteLine("| 縛め（クグ） | `BindTrait` | **味方**を無作為に1体（第2ターン以降） | 届く |");
    Console.WriteLine("| 痺れ | `ParalyzeTrait` | 殴った**相手** | 届く（敵側の保持者がヒヨを殴ったとき） |");
    Console.WriteLine("| 断罪 | `CondemnTrait` | 殴ってきた**攻撃者** | **移設で届かなくなる**（ヒヨは `PerformAttack` を通らなくなる） |");
    Console.WriteLine();
    Console.WriteLine("**行ごとの供給者（味方）**と**波ごとの供給者（敵）**を数える。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 味方の縛め | 味方の痺れ | 味方の断罪 |");
    Console.WriteLine("|---|--:|--:|--:|");
    foreach (var b in fpRows)
        Console.WriteLine($"| {b.Name} | {FvCountTrait(b.F, TraitId.Bind)} | {FvCountTrait(b.F, TraitId.Paralyze)} | {FvCountTrait(b.F, TraitId.Condemn)} |");
    Console.WriteLine();
    Console.WriteLine("| 波 | 敵の痺れ | 敵の断罪 | 保持者 |");
    Console.WriteLine("|---|--:|--:|---|");
    for (int w = 0; w < fpStages.Count; w++)
    {
        Formation e = fpStages[w].Enemy;
        var who = e.Occupied()
            .Where(o => o.Def.Traits.Contains(TraitId.Paralyze) || o.Def.Traits.Contains(TraitId.Condemn))
            .Select(o => o.Def.Name + (o.Def.Traits.Contains(TraitId.Paralyze) ? "（痺れ）" : "（断罪）"))
            .ToList();
        Console.WriteLine($"| 第{w + 1}波 | {FvCountTrait(e, TraitId.Paralyze)} | {FvCountTrait(e, TraitId.Condemn)} | "
                        + (who.Count == 0 ? "—" : string.Join(" / ", who)) + " |");
    }
    Console.WriteLine();

    // ---- 0-2. 粛との関係 -------------------------------------------------------------
    Console.WriteLine("## 0-2. 粛（`Hush`）との関係（**予測**）");
    Console.WriteLine();
    Console.WriteLine("粛は `BattleContext.CanActOutOfTurn` の最後1箇所でターン外の行動を止める。");
    Console.WriteLine("**行動順ループは `CanActOutOfTurn` を1度も呼ばない**（呼び出し元は特性側の4本＝棘・仇討ち・軋み・追い打ちだけ）。");
    Console.WriteLine("`OnTurnStart` も行動順ループの外側なので、こちらも通らない。");
    Console.WriteLine();
    Console.WriteLine("> **予測: 移設の前後で粛の効きは変わらない。第2波は動かない。**");
    Console.WriteLine("> `火選り無風型` の第2波 0.0% は不動のカドが粛の下で置物になる行の性質で、火選りとは無関係。");
    Console.WriteLine();

    // ---- 0-3. 速さの前後関係 ---------------------------------------------------------
    Console.WriteLine("## 0-3. 速さの前後関係（ヒヨは速6）");
    Console.WriteLine();
    Console.WriteLine("行動順は速さ降順 → チーム → スロット。**ヒヨより速い味方は、ヒヨが配る前にその手番を終えている。**");
    Console.WriteLine();
    Console.WriteLine("| 行 | ヒヨより速い味方 | 同速 | ヒヨより遅い味方 |");
    Console.WriteLine("|---|---|---|---|");
    foreach (var b in fpRows)
    {
        var others = b.F.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Hiyo)).Select(o => o.Def).ToList();
        string Grp(Func<UnitDef, bool> p)
        {
            var l = others.Where(p).Select(d => $"{d.Name}({d.Speed})").ToList();
            return l.Count == 0 ? "—" : string.Join(" / ", l);
        }
        Console.WriteLine($"| {b.Name} | {Grp(d => d.Speed > UnitCatalog.Hiyo.Speed)} | {Grp(d => d.Speed == UnitCatalog.Hiyo.Speed)} | {Grp(d => d.Speed < UnitCatalog.Hiyo.Speed)} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**ボルグ速{UnitCatalog.Borg.Speed} > ヒヨ速{UnitCatalog.Hiyo.Speed}** ——移設すれば、ヒヨの番が回る時点で火は既に点いている（これが移設の狙い）。");
    Console.WriteLine($"**ホタ速{UnitCatalog.Hota.Speed} > ヒヨ速{UnitCatalog.Hiyo.Speed}** ——**ホタはその手番に配られた強化を、そのターンには使えない。**");
    Console.WriteLine();
    Console.WriteLine("> **予測（P6・新しく生まれる遅れ）: 第58期の遅れは消えるのではなく形が変わる。**");
    Console.WriteLine("> 移設前は `OnTurnStart` がホタの手番より前なので、配った強化は**そのターンから**乗っていた。");
    Console.WriteLine("> 移設後はヒヨ（速6）がホタ（速7）より後に動くので、**乗るのは次のターンから**になる。");
    Console.WriteLine("> **「第1ターンだけ鈍らせる」（1回）は消えるが、「1ターン遅れて乗る」（毎ターン）が新しく生まれる。**");
    Console.WriteLine();

    // ---- 0-4. ヒヨの現在の出力 -------------------------------------------------------
    Console.WriteLine("## 0-4. ヒヨの現在の出力（移設で捨てる量）");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{FpSeeds - 1}。`振り` は `PerformAttack` を通った回数、`与ダメ` は敵に通した量。");
    Console.WriteLine("`空振り` は `FavorIdle`（盤上に燃えている味方が1体もいなかった手番）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 波 | 勝率 | 決着T | ヒヨの振り/戦 | ヒヨの与ダメ/戦 | 発火/戦 | 空振り/戦 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    double swSum = 0, dmgSum = 0; int nCell = 0;
    foreach (var b in fpRows)
    {
        double swAll = 0, dmAll = 0, fiAll = 0, idAll = 0;
        for (int w = 0; w < fpStages.Count; w++)
        {
            double win = 0, turns = 0, sw = 0, dm = 0, fi = 0, id = 0;
            for (int seed = 0; seed < FpSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(b.F, fpStages[w].Enemy, seed, verbose: false);
                if (r.PlayerWon) win++;
                turns += r.Turns; fi += r.FavorFires; id += r.FavorIdle;
                if (r.TallyByUnit.TryGetValue("hiyo", out UnitTally? t))
                { sw += t.Attacks; dm += t.DamageToEnemy; }
            }
            double n = FpSeeds;
            Console.WriteLine($"| {b.Name} | 第{w + 1}波 | {win * 100 / n:0.0}% | {turns / n:0.0} "
                            + $"| {sw / n:0.00} | {dm / n:0.0} | {fi / n:0.00} | {id / n:0.00} |");
            swAll += sw / n; dmAll += dm / n; fiAll += fi / n; idAll += id / n;
            swSum += sw / n; dmgSum += dm / n; nCell++;
        }
        Console.WriteLine($"| | **5波平均** | | | **{swAll / 5:0.00}** | **{dmAll / 5:0.0}** | **{fiAll / 5:0.00}** | **{idAll / 5:0.00}** |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine($"**移設で捨てる量は 20 セルの平均で 振り {swSum / nCell:0.00} 回/戦・与ダメ {dmgSum / nCell:0.0}/戦。**");
    Console.WriteLine();

    // ---- 0-6. 現在値 -----------------------------------------------------------------
    Console.WriteLine("## 0-6. `docs/balance.md` の現在値（4行 × 5波）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 情報セル |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var b in fpRows)
    {
        var v = new double[fpStages.Count];
        for (int w = 0; w < fpStages.Count; w++)
        {
            int win = 0;
            for (int seed = 0; seed < FpSeeds; seed++)
                if (BattleEngine.Run(b.F, fpStages[w].Enemy, seed, false).PlayerWon) win++;
            v[w] = win * 100.0 / FpSeeds;
        }
        int info = v.Count(x => x > 5.0 && x < 95.0);   // 第22期の狭義の中間帯
        Console.WriteLine($"| {b.Name} " + string.Concat(v.Select(x => $"| {x:0.0}% "))
                        + $"| {v.Average():0.0}% | **{info}** |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine("**情報セルは狭義の中間帯 5.0 < x < 95.0**（第22期）。Q5 はこの列で判定する。");
    Console.WriteLine();
    return;
}


if (focusId == "turn")
{
    var fvBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> fvStages = EnemyCatalog.Stages;
    string fvMode = args.Length > 2 ? args[2] : "";
    bool fvAlt = fvMode == "alt";
    int fvFrom = fvAlt ? 200 : 0;              // alt は別 seed 帯（200..599）
    int fvSeeds = fvAlt ? 400 : 200;           // それ以外は compare と同じ帯

    var fvRows = fvBuilds.Where(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hiyo))).ToArray();

    UnitDef fvTs = FvTurnStartDef(), fvPlainAct = FvPlainActDef(), fvPlain = FvPlainDef();
    Formation FvV0(Formation f) => FvSwap(f, UnitCatalog.Hiyo, fvTs);   // 移設前（ターン頭）
    Formation FvV1(Formation f) => f;                                    // 移設後（＝現行の盤面）
    Formation FvV2(Formation f) => FvSwap(f, UnitCatalog.Hiyo, fvPlainAct);
    Formation FvV3(Formation f) => FvSwap(f, UnitCatalog.Hiyo, fvPlain);

    // 版ごとの計数。ヒヨの id は版で変わるので、駒側の集計は「ヒヨの席にいる駒」で引く。
    var fvIds = new[] { "hiyo", "hiyo_ts", "hiyo_plain_act", "hiyo_plain" };

    FvStat FvMeasure(Formation f, Formation enemy, FavorRule? rule)
    {
        // 味方の総与ダメは**味方の `Def.Id` 集合**で割る（`TallyByUnit` は `Def.Id` で引くので
        // `InstanceId` の範囲では割れない。第59期 `BzStat` と同じ作法）。
        var mine = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        var z = new FvStat();
        for (int seed = fvFrom; seed < fvFrom + fvSeeds; seed++)
        {
            BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false, favor: rule);
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Fires += r.FavorFires; z.Idle += r.FavorIdle;
            z.Whetted += r.FavorWhetted; z.Dulled += r.FavorDulled;
            z.Given += r.FavorGiven; z.Taken += r.FavorTaken; z.ToPyre += r.FavorToPyre;
            foreach ((string k, int v) in r.FavorDullTo)
                z.DullTo[k] = z.DullTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.FavorWhetTo)
                z.WhetTo[k] = z.WhetTo.TryGetValue(k, out double a) ? a + v : v;
            foreach (string id in fvIds)
                if (r.TallyByUnit.TryGetValue(id, out UnitTally? t))
                { z.Swings += t.Attacks; z.Dmg += t.DamageToEnemy; z.Life += t.LastActiveTurn; }
            z.TeamDmg += r.TallyByUnit.Where(kv => mine.Contains(kv.Key)).Sum(kv => kv.Value.DamageToEnemy);
        }
        double n = fvSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n; z.Fires /= n; z.Idle /= n;
        z.Whetted /= n; z.Dulled /= n; z.Given /= n; z.Taken /= n; z.ToPyre /= n;
        z.Swings /= n; z.Dmg /= n; z.Life /= n; z.TeamDmg /= n;
        foreach (string k in z.DullTo.Keys.ToList()) z.DullTo[k] /= n;
        foreach (string k in z.WhetTo.Keys.ToList()) z.WhetTo[k] /= n;
        return z;
    }
    double[] FvWins(Formation f, FavorRule? rule)
    {
        var v = new double[fvStages.Count];
        for (int w = 0; w < fvStages.Count; w++)
        {
            int win = 0;
            for (int seed = fvFrom; seed < fvFrom + fvSeeds; seed++)
                if (BattleEngine.Run(f, fvStages[w].Enemy, seed, false, favor: rule).PlayerWon) win++;
            v[w] = win * 100.0 / fvSeeds;
        }
        return v;
    }
    static int FvInfo(double[] v) => v.Count(x => x > 5.0 && x < 95.0);
    static string FvCells(double[] v) => string.Concat(v.Select(x => $"| {x:0.0}% "));

    // ---- 係数の確定（第61期 §4）--------------------------------------------------------
    //
    // **乗算の比**を両 seed 帯で取る。1帯の比で桁の判断をしない（指示書 §4）。
    //
    //     比 = （乗算持ち＝熾のホタがいる行の伸び） ÷ （いない行の伸び）
    //
    // 比が 2.0 以上なら `Gain` は**ホタ専用のノブ**と判定して上げない。
    if (fvMode == "ratio")
    {
        Console.WriteLine("# `FavorRule` の係数の確定（第61期 §4）");
        Console.WriteLine();
        Console.WriteLine("`(4, 2)`（現行）と `(6, 2)` を火選り4行で比べ、**乗算の比**を両 seed 帯で取る。");
        Console.WriteLine("**乗算持ち（熾のホタ・`PyreTrait`）がいるのは `火選り (ヒヨ×ホタ)` の1行だけ。**");
        Console.WriteLine();
        var bands = new (string Tag, int From, int Seeds)[] { ("A 帯 0..199", 0, 200), ("B 帯 200..599", 200, 400) };
        var rows4 = fvBuilds.Where(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hiyo))).ToArray();
        foreach ((string tag, int from, int seeds) in bands)
        {
            double[] WinsIn(Formation f, FavorRule? rule)
            {
                var v = new double[fvStages.Count];
                for (int w = 0; w < fvStages.Count; w++)
                {
                    int win = 0;
                    for (int seed = from; seed < from + seeds; seed++)
                        if (BattleEngine.Run(f, fvStages[w].Enemy, seed, false, favor: rule).PlayerWon) win++;
                    v[w] = win * 100.0 / seeds;
                }
                return v;
            }
            Console.WriteLine($"## {tag}");
            Console.WriteLine();
            Console.WriteLine("| 行 | 乗算 | (4,2) | (6,2) | 伸び | 情報セル (4,2) → (6,2) |");
            Console.WriteLine("|---|:-:|--:|--:|--:|--:|");
            double pyre = 0; var plainGains = new List<double>();
            foreach (var b in rows4)
            {
                bool hasPyre = b.F.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Pyre));
                double[] a = WinsIn(b.F, new FavorRule(4, 2));
                double[] c = WinsIn(b.F, new FavorRule(6, 2));
                double gain = c.Average() - a.Average();
                Console.WriteLine($"| {b.Name} | {(hasPyre ? "**あり**" : "—")} | {a.Average():0.0}% | {c.Average():0.0}% "
                                + $"| **{gain:+0.0;-0.0;0.0}** | {FvInfo(a)} → {FvInfo(c)} |");
                if (hasPyre) pyre = gain; else plainGains.Add(gain);
                Console.Out.Flush();
            }
            Console.WriteLine();
            double row2 = plainGains.Count > 0 ? plainGains[0] : 0;
            double mean = plainGains.Count > 0 ? plainGains.Average() : 0;
            Console.WriteLine($"- **行1 ÷ 行2（指示書と同じ形）: {pyre:0.0} ÷ {row2:0.0} = "
                            + $"{(Math.Abs(row2) > 0.05 ? (pyre / row2).ToString("0.00") : "—（分母が 0）")}**");
            Console.WriteLine($"- **行1 ÷ 乗算なし3行の平均: {pyre:0.0} ÷ {mean:0.0} = "
                            + $"{(Math.Abs(mean) > 0.05 ? (pyre / mean).ToString("0.00") : "—（分母が 0）")}**");
            Console.WriteLine();
        }
        Console.WriteLine("**判定**: 比が 2.0 未満なら `(6,2)` を採る。2.0 以上なら `Gain` はホタ専用のノブと判定して上げない。");
        Console.WriteLine();
        return;
    }

    // ---- 掃引だけ --------------------------------------------------------------------
    if (fvMode == "sweep")
    {
        Console.WriteLine("# 移設後の掃引（第60期 §4）");
        Console.WriteLine();
        Console.WriteLine("**4点だけ。** 移設で代金の相手の集合が変わっている可能性があるので、");
        Console.WriteLine("`(4, 4)` が現行値 `(4, 2)` を上回るかどうかが「集合が縮んだ」ことの独立な検算になる。");
        Console.WriteLine("**各点の対照は V2（素体）で足りる**（体の値段はノブで動かない）。");
        Console.WriteLine();
        var pts = new (string Tag, int G, int L)[] { ("現行 (4/2)", 4, 2), ("a (4/4)", 4, 4), ("b (6/2)", 6, 2), ("c (2/2)", 2, 2) };
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 素体との差 | 配った | 撒いた | 情報セル |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in fvRows)
        {
            double[] pv = FvWins(FvV2(b.F), null);
            Console.WriteLine($"| {b.Name} | V2 素体（手番） {FvCells(pv)}| **{pv.Average():0.0}%** | — | — | — | {FvInfo(pv)} |");
            foreach (var p in pts)
            {
                var rule = new FavorRule(p.G, p.L);
                double[] v = FvWins(FvV1(b.F), rule);
                double give = 0, take = 0;
                for (int w = 0; w < fvStages.Count; w++)
                {
                    FvStat z = FvMeasure(FvV1(b.F), fvStages[w].Enemy, rule);
                    give += z.Given / fvStages.Count; take += z.Taken / fvStages.Count;
                }
                Console.WriteLine($"| | {p.Tag} {FvCells(v)}| **{v.Average():0.0}%** | {v.Average() - pv.Average():+0.0;-0.0;0.0} "
                                + $"| {give:0.00} | {take:0.00} | {FvInfo(v)} |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        return;
    }

    // ---- 主表 ------------------------------------------------------------------------
    Console.WriteLine(fvAlt ? "# 移設の主表（別 seed 帯 200..599 の追試）" : "# 移設の主表（第60期）");
    Console.WriteLine();
    Console.WriteLine($"seed {fvFrom}..{fvFrom + fvSeeds - 1}。**V1（移設）は第60期に採用したので `UnitCatalog.Hiyo` そのもの**——");
    Console.WriteLine("**V1 の5波が `docs/balance.md` と一致することがこの診断の検算**（第36期 `gullet belly4` と同じ形）。");
    Console.WriteLine("V0 = 移設前（`OnTurnStart`）／V2 = 移設 + 素体（同数値・特性なし。**機構の帰属**）／");
    Console.WriteLine("V3 = 移設前の素体（`V0 − V3` が第58期の機構の帰属）。**V0 / V2 / V3 は診断のローカルの `UnitDef`。**");
    Console.WriteLine();

    Console.WriteLine("## 1. 勝率（4行 × 5波）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 情報セル | V1−V0 | V1−V2 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var attr = new List<(string Name, double A, double B, int I0, int I1)>();
    foreach (var b in fvRows)
    {
        double[] v0 = FvWins(FvV0(b.F), null);
        double[] v1 = FvWins(FvV1(b.F), null);
        double[] v2 = FvWins(FvV2(b.F), null);
        double[] v3 = FvWins(FvV3(b.F), null);
        Console.WriteLine($"| {b.Name} | V0 移設前 {FvCells(v0)}| **{v0.Average():0.0}%** | {FvInfo(v0)} | — | — |");
        Console.WriteLine($"| | **V1 移設（現行）** {FvCells(v1)}| **{v1.Average():0.0}%** | **{FvInfo(v1)}** "
                        + $"| **{v1.Average() - v0.Average():+0.0;-0.0;0.0}** | **{v1.Average() - v2.Average():+0.0;-0.0;0.0}** |");
        Console.WriteLine($"| | V2 素体（手番） {FvCells(v2)}| **{v2.Average():0.0}%** | {FvInfo(v2)} | — | — |");
        Console.WriteLine($"| | V3 素体（移設前） {FvCells(v3)}| **{v3.Average():0.0}%** | {FvInfo(v3)} "
                        + $"| （V0−V3 = {v0.Average() - v3.Average():+0.0;-0.0;0.0}） | |");
        attr.Add((b.Name, v1.Average() - v0.Average(), v1.Average() - v2.Average(), FvInfo(v0), FvInfo(v1)));
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine($"**帰属（V1 − V0）の4行平均: {attr.Average(a => a.A):+0.0;-0.0;0.0}pt** "
                    + $"／ **機構の帰属（V1 − V2）の4行平均: {attr.Average(a => a.B):+0.0;-0.0;0.0}pt**");
    Console.WriteLine($"**情報セルの合計: V0 {attr.Sum(a => a.I0)} → V1 {attr.Sum(a => a.I1)}**");
    Console.WriteLine();

    // ---- 計数 ------------------------------------------------------------------------
    Console.WriteLine("## 2. 計数（5波の平均）");
    Console.WriteLine();
    Console.WriteLine("`発火` は強化か弱体を1体でも配った手番、`空振り` は盤上に燃えている味方が1体もいなかった手番。");
    Console.WriteLine("`燃体` / `非燃体` は延べ体数、`配った` / `撒いた` は量。`熾火へ` は乗算持ちへ配った量。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 発火 | 空振り | 燃体 | 非燃体 | 配った | 撒いた | 熾火へ | ヒヨ振り | ヒヨ与ダメ | 味方総与ダメ | ヒヨ寿命T |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var q2 = new List<(string Name, double T0, double T1, double G0, double G1)>();
    foreach (var b in fvRows)
    {
        var acc = new Dictionary<string, FvStat>();
        foreach ((string tag, Formation f) in new[] { ("V0 移設前", FvV0(b.F)), ("V1 移設", FvV1(b.F)) })
        {
            var agg = new FvStat();
            for (int w = 0; w < fvStages.Count; w++)
            {
                FvStat z = FvMeasure(f, fvStages[w].Enemy, null);
                agg.Fires += z.Fires / 5; agg.Idle += z.Idle / 5; agg.Whetted += z.Whetted / 5;
                agg.Dulled += z.Dulled / 5; agg.Given += z.Given / 5; agg.Taken += z.Taken / 5;
                agg.ToPyre += z.ToPyre / 5; agg.Swings += z.Swings / 5; agg.Dmg += z.Dmg / 5;
                agg.TeamDmg += z.TeamDmg / 5; agg.Life += z.Life / 5;
                foreach ((string k, double v) in z.DullTo)
                    agg.DullTo[k] = agg.DullTo.TryGetValue(k, out double a) ? a + v / 5 : v / 5;
                foreach ((string k, double v) in z.WhetTo)
                    agg.WhetTo[k] = agg.WhetTo.TryGetValue(k, out double a) ? a + v / 5 : v / 5;
            }
            acc[tag] = agg;
            Console.WriteLine($"| {(tag.StartsWith("V0") ? b.Name : "")} | {tag} | {agg.Fires:0.00} | {agg.Idle:0.00} "
                            + $"| {agg.Whetted:0.00} | {agg.Dulled:0.00} | {agg.Given:0.00} | {agg.Taken:0.00} | {agg.ToPyre:0.00} "
                            + $"| {agg.Swings:0.00} | {agg.Dmg:0.0} | {agg.TeamDmg:0.0} | {agg.Life:0.0} |");
            Console.Out.Flush();
        }
        q2.Add((b.Name, acc["V0 移設前"].Taken, acc["V1 移設"].Taken, acc["V0 移設前"].Given, acc["V1 移設"].Given));

        // 受け手の内訳（Q1）
        Console.WriteLine($"| | **弱体の受け手 V0** | " + FvTop(acc["V0 移設前"].DullTo) + " |");
        Console.WriteLine($"| | **弱体の受け手 V1** | " + FvTop(acc["V1 移設"].DullTo) + " |");
        Console.WriteLine($"| | 強化の受け手 V0 | " + FvTop(acc["V0 移設前"].WhetTo) + " |");
        Console.WriteLine($"| | 強化の受け手 V1 | " + FvTop(acc["V1 移設"].WhetTo) + " |");
    }
    Console.WriteLine();
    Console.WriteLine("**Q2（`撒いた` の増減）**");
    Console.WriteLine();
    Console.WriteLine("| 行 | 撒いた V0 | 撒いた V1 | 差 | 配った V0 | 配った V1 | 差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    foreach (var q in q2)
        Console.WriteLine($"| {q.Name} | {q.T0:0.00} | {q.T1:0.00} | **{q.T1 - q.T0:+0.00;-0.00;0.00}** "
                        + $"| {q.G0:0.00} | {q.G1:0.00} | **{q.G1 - q.G0:+0.00;-0.00;0.00}** |");
    Console.WriteLine();

    // ---- Q4: 止まった手番 -------------------------------------------------------------
    Console.WriteLine("## 3. Q4 —— 移設で止まった手番（verbose・seed の先頭 50 本）");
    Console.WriteLine();
    Console.WriteLine("**痺れだけを数える。** 手番が飛ぶ経路は Phase 0-1 の5本で、ヒヨに効きうるのは痺れと死亡だけ。");
    Console.WriteLine("死亡と「決着して行動順ループが `break` した」は移設の前後で同じだけあるので、");
    Console.WriteLine("**新しい交絡になりうるのは痺れ1本**——`StatusSnapshot` の `痺` を数える");
    Console.WriteLine("（痺れは付いた次のターンの頭に立ち、その手番の先頭で消費される）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 波 | 痺れ V0/戦 | 痺れ V1/戦 | 生存T V1 | 撃った V1 | 差（死亡＋決着） |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
    double stun0All = 0, stun1All = 0;
    foreach (var b in fvRows)
    {
        for (int w = 0; w < fvStages.Count; w++)
        {
            double st0 = 0, st1 = 0, alive = 0, cast = 0;
            for (int seed = fvFrom; seed < fvFrom + 50; seed++)
            {
                var p0 = BattleEngine.Materialize(FvV0(b.F), BattleContext.PlayerTeam);
                int i0 = p0.FindIndex(u => u.Def.Id == "hiyo_ts");
                BattleResult r0 = BattleEngine.Run(p0, BattleEngine.Materialize(fvStages[w].Enemy, BattleContext.EnemyTeam), seed, verbose: true);
                st0 += r0.Events.Count(e => e.Kind == BattleEventKind.StatusSnapshot && e.TargetId == i0 && e.Text == "痺");

                var p1 = BattleEngine.Materialize(FvV1(b.F), BattleContext.PlayerTeam);
                int i1 = p1.FindIndex(u => u.Def.Id == "hiyo");
                BattleResult r1 = BattleEngine.Run(p1, BattleEngine.Materialize(fvStages[w].Enemy, BattleContext.EnemyTeam), seed, verbose: true);
                st1 += r1.Events.Count(e => e.Kind == BattleEventKind.StatusSnapshot && e.TargetId == i1 && e.Text == "痺");
                alive += r1.Events.Count(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == i1);
                cast += r1.Events.Count(e => e.Kind == BattleEventKind.Skill && e.ActorId == i1);
            }
            Console.WriteLine($"| {(w == 0 ? b.Name : "")} | 第{w + 1}波 | {st0 / 50:0.00} | **{st1 / 50:0.00}** "
                            + $"| {alive / 50:0.00} | {cast / 50:0.00} | {(alive - cast) / 50:0.00} |");
            stun0All += st0 / 50; stun1All += st1 / 50;
            Console.Out.Flush();
        }
    }
    Console.WriteLine();
    Console.WriteLine($"**20 セルの合計で痺れは V0 {stun0All:0.00} 回/戦 → V1 {stun1All:0.00} 回/戦。**");
    Console.WriteLine("`差（死亡＋決着）` は移設に固有ではない——ヒヨが自分の手番の前に落ちたか、");
    Console.WriteLine("その手番が回る前に決着して `order` のループが `break` した回数。");
    Console.WriteLine();
    return;
}

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

// yield モード: 攻撃力1点は、誰の手なら出力になるか（第24期）。
//
// 「燃料が出力にならない」が3回続いている。**毎回、原因の候補を1つ潰して外している。**
// 第19期 route（燃料が変換器に届いていない → 届けたら `ムド 与ダメ` は 39 → 32 と下がった）、
// 第20期（変換器が積み上げ型だから遅い → 即時払いに繋いでも1ミリも動かない）、
// 第23期 gullet（変換器に燃料が足りない → 吐き戻しで足しても 41 → 42）。
//
// 23期で形は見えた。**ムドの攻撃力は素3から18まで伸びていて、1ターンあたりの出力も +11%
// 出ている。** それでも総量が動かないのは決着が 3.9 → 3.6 と短くなるからで、火力の増分が
// 「与ダメ総量」ではなく「決着ターンの短縮」として出ている。**そして戦闘を長くする方向は
// 第6期 `aim` で否定済み**（`単体−範囲` とターン数の相関 r = −0.97。長さは向きを作らず消す）。
//
// 残っている仮説は1つ。**攻撃力を出力に変える効率が、駒によって桁で違う。**
// `gullet` の予測を外したのも同じ場所だった——ウツという例外にだけ注目して、
// **強化の価値は受け手の攻撃型と手番数に比例する**という当たり前を見ていなかった
// （実際に伸びたのは セロ＝狙撃で攻撃力2倍 +24.4 と ドルガ＝攻38・薙ぎ +14.7 で、
// 伸びなかったのは リィカ攻5・ヴェル攻6 の編成。受け手の質が全部だった）。
//
// **注入テスト。** 駒1体の `AtkBonus` に開戦時から `Inject` を足して、味方全体の与ダメが
// どれだけ増えるかを見る。これで「燃料 → 攻撃力 → 出力」の**前半を切り離せる**——
// ムドの `Rage` が動いていることは実測済みなので、**壊れているのは後半だと特定できる。**
//
// **測っているのは上限。** 実際の積み上げは戦闘の後半にしか効かないが、注入は開戦時から
// 全ターン効く。**ここで低い駒は、積み上げ経由ではもっと低い。**
//
// **天井・床のセルでは誰に注入しても 0 に潰れる。** 敵を既に削り切っている波では、
// 増えた火力は与ダメ総量ではなく決着ターンの短縮になって出る（第23期のムドと同じ形）。
// これは駒の性質ではなく台の性質なので、**波ごとに 中間帯（5% < 注入なしの勝率 < 95%）を
// 切り出した列を必ず併記する**（第15期「天井・床の波は評価に寄与しない」/ 第21期 swap の
// 「台が飽和していないかの検査」と同じ線）。順位表は2つ出す。
//
// エンジンは触らない。`Materialize`（public）→ `AtkBonus`（set 可）→ `UnitState` 版 `Run` の
// 3つで診断側だけで完結する。**`Formation` 版の `Run` は `Materialize` を内側で呼ぶので
// 注入する隙が無い。** 敵側は毎回作り直す（`UnitState` を使い回すと前の戦闘の状態が残る）。
//
// **`CompareBuilds()` / `Stages` / ロスターは一切触らない。** docs/ には出さない（診断用）。
//
//     dotnet run --project BattleSim -c Release 0 yield [絞り込み]
if (focusId == "yield")
{
    var yieldBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> yStages = EnemyCatalog.Stages;
    const int YieldSeeds = 200;   // compare / pulse / route と同じ
    const int Inject = 10;        // 注入量。ノイズより十分大きく、駒を別物にしない帯

    string filter = args.Length > 2 ? args[2] : "";
    var targets = yieldBuilds
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    if (targets.Length == 0)
    {
        Console.WriteLine($"絞り込み「{filter}」に一致する編成が無い。");
        return;
    }

    // 味方全体の与ダメは**受け手側＝敵の tally** から取る（第13期 Phase DA）。
    // TickStatuses が `ApplyDamage(u, poison, null)` と source を渡さないので、毒・燃焼の削りは
    // 味方側の DamageToEnemy に載らない。注入で毒軸の駒の出力が動いたときに味方側から数えると
    // 構造的に過小に出る。`TakenFromAlly` を引くのは敵同士の巻き込みを手柄にしないため。
    var foeIdsByStage = yStages.Select(st => st.Enemy.Occupied().Select(x => x.Def.Id).ToHashSet()).ToArray();
    var allFoeIds = foeIdsByStage.SelectMany(x => x).ToHashSet();
    var clash = targets.SelectMany(t => t.F.Occupied().Select(x => x.Def.Id))
                       .Where(allFoeIds.Contains).Distinct().ToArray();

    var members = targets.Select(t => t.F.Occupied().Select(x => x.Def).ToArray()).ToArray();

    // ジョブ表: 編成ごとに「注入なし（J=-1）」+「メンバー1体ずつ」。
    // 各ジョブは自分の添字にしか書かないので回収に同期は要らず、出力はスレッドの
    // スケジューリングに依存しない（layout と同じ作法。Run は seed 決定的な純関数）。
    var jobs = new List<(int B, int J)>();
    var baseIdx = new int[targets.Length];
    for (int b = 0; b < targets.Length; b++)
    {
        baseIdx[b] = jobs.Count;
        jobs.Add((b, -1));
        for (int m = 0; m < members[b].Length; m++) jobs.Add((b, m));
    }

    // 集計はすべて**波ごと**に持つ。中間帯の切り出しがセル単位（編成 × 波）だから。
    var res = new (int[] Wins, long[] Turns, long[] Output, long[] Kills, long[] Self)[jobs.Count];
    var baseTally = new UnitTally[targets.Length][][];   // [編成][駒][波]
    var baseMismatch = new int[targets.Length];          // Formation 版とのずれ（0 のはず）
    var baseWinsF = new int[targets.Length][];           // Formation 版の勝ち数（= compare の値）

    Parallel.For(0, jobs.Count, i =>
    {
        (int b, int j) = jobs[i];
        Formation f = targets[b].F;
        string? injectId = j < 0 ? null : members[b][j].Id;
        int n = yStages.Count;

        var wins = new int[n];
        var turns = new long[n];
        var output = new long[n];
        var kills = new long[n];
        var self = new long[n];
        int[]? winsF = null;
        int mismatch = 0;
        UnitTally[][]? perMember = null;
        if (j < 0)
        {
            winsF = new int[n];
            perMember = new UnitTally[members[b].Length][];
            for (int m = 0; m < perMember.Length; m++)
            {
                perMember[m] = new UnitTally[n];
                for (int st = 0; st < n; st++) perMember[m][st] = new UnitTally();
            }
        }

        for (int st = 0; st < n; st++)
        {
            HashSet<string> foeIds = foeIdsByStage[st];
            for (int seed = 0; seed < YieldSeeds; seed++)
            {
                // 注入版は Materialize してから AtkBonus を触り、UnitState 版の Run へ渡す。
                // AtkBonus を直に足すので支援拒否（ガルド）にも通る——測っているのは
                // 「攻撃力が乗ったら出力になるか」で、乗せる経路の可否ではない。
                var player = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                if (injectId is not null)
                    foreach (UnitState u in player.Where(u => u.Def.Id == injectId)) u.AtkBonus += Inject;
                var foe = BattleEngine.Materialize(yStages[st].Enemy, BattleContext.EnemyTeam);
                BattleResult r = BattleEngine.Run(player, foe, seed, verbose: false);

                if (r.PlayerWon) wins[st]++;
                turns[st] += r.Turns;
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (!foeIds.Contains(id)) continue;
                    output[st] += t.DamageTaken - t.TakenFromAlly;
                    kills[st] += t.Deaths;
                }
                if (injectId is not null && r.TallyByUnit.TryGetValue(injectId, out UnitTally? sx))
                    self[st] += sx.DamageToEnemy;

                if (perMember is null) continue;

                for (int m = 0; m < perMember.Length; m++)
                    if (r.TallyByUnit.TryGetValue(members[b][m].Id, out UnitTally? mt)) perMember[m][st].Add(mt);

                // 検算: Formation 版（compare がそのまま通る経路）と1試行ずつ突き合わせる。
                // 勝敗もターン数も一致していなければ Materialize の使い方が違う。
                BattleResult chk = BattleEngine.Run(f, yStages[st].Enemy, seed, verbose: false);
                if (chk.PlayerWon) winsF![st]++;
                if (chk.PlayerWon != r.PlayerWon || chk.Turns != r.Turns) mismatch++;
            }
        }

        res[i] = (wins, turns, output, kills, self);
        if (perMember is not null)
        {
            baseTally[b] = perMember;
            baseMismatch[b] = mismatch;
            baseWinsF[b] = winsF!;
        }
    });

    // --- 派生値。ここから先は測定を1回もしない ---

    // 中間帯 = 注入なしの勝率が 5% < x < 95% の（編成 × 波）セル。狭義（第22期 spread と同じ線）。
    // 天井・床のセルでは誰に注入しても与ダメ総量が動かないので、駒の質を測っていない。
    var midStages = new List<int>[targets.Length];
    for (int b = 0; b < targets.Length; b++)
        midStages[b] = Enumerable.Range(0, yStages.Count)
            .Where(st => baseWinsF[b][st] * 100.0 / YieldSeeds is > 5.0 and < 95.0)
            .ToList();

    // 指定した波だけを足して1戦あたりに直す。全波と中間帯で同じ関数を通すので定義がずれない。
    (double Yield, double Self, double Win, double Kill, double Turn, double Swing, double Act, double Death)
        Agg(int b, int m, IReadOnlyList<int> sts)
    {
        var bs = res[baseIdx[b]];
        var inj = res[baseIdx[b] + 1 + m];
        double battles = sts.Count * (double)YieldSeeds;
        long dOut = 0, dSelf = 0, dKill = 0, dTurn = 0, dWin = 0;
        long swing = 0, act = 0, death = 0, baseSelf = 0;
        foreach (int st in sts)
        {
            dOut += inj.Output[st] - bs.Output[st];
            dKill += inj.Kills[st] - bs.Kills[st];
            dTurn += inj.Turns[st] - bs.Turns[st];
            dWin += inj.Wins[st] - bs.Wins[st];
            dSelf += inj.Self[st];
            baseSelf += baseTally[b][m][st].DamageToEnemy;
            swing += baseTally[b][m][st].Attacks;
            act += baseTally[b][m][st].Interventions;
            death += baseTally[b][m][st].Deaths;
        }
        return (dOut / battles / Inject, (dSelf - baseSelf) / battles / Inject,
                dWin * 100.0 / battles, dKill / battles, dTurn / battles,
                swing / battles, act / battles, death / battles);
    }

    var allStages = Enumerable.Range(0, yStages.Count).ToList();
    var rows = new List<(string Build, UnitDef Def, int Slot, int MidCells,
                         (double Yield, double Self, double Win, double Kill, double Turn,
                          double Swing, double Act, double Death) All,
                         (double Yield, double Self, double Win, double Kill, double Turn,
                          double Swing, double Act, double Death) Mid)>();
    for (int b = 0; b < targets.Length; b++)
    {
        var slots = targets[b].F.Occupied().Select(x => x.Slot).ToArray();
        for (int m = 0; m < members[b].Length; m++)
            rows.Add((targets[b].Name, members[b][m], slots[m], midStages[b].Count,
                      Agg(b, m, allStages),
                      midStages[b].Count == 0 ? default : Agg(b, m, midStages[b])));
    }

    string Pat(UnitDef d) => d.Pattern switch
    {
        AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き",
        AttackPattern.All => "全体", _ => "単体"
    };
    string Acts(UnitDef d) => d.Actions is null || d.Actions.Count == 0
        ? "毎T攻"
        : string.Concat(d.Actions.Select(a => a.Kind switch
        {
            ActionKind.Charge => "溜", ActionKind.Skill => "技", _ => "攻"
        }));
    // 出力/点 を左右しそうな特性だけを短い札にする（全部並べると読めない）。
    (TraitId Id, string Tag)[] tagTable =
    {
        (TraitId.Immobile, "不動"), (TraitId.Sluggish, "のろま"), (TraitId.Sniper, "狙撃"),
        (TraitId.Perverse, "逆しま"), (TraitId.Thorns, "棘"), (TraitId.ThornGuard, "棘守り"),
        (TraitId.Pursuer, "追打"), (TraitId.Venom, "毒撃"), (TraitId.Miasma, "瘴気"),
        (TraitId.Cinder, "火粉"), (TraitId.Mender, "繕い"), (TraitId.Coward, "臆病"),
        (TraitId.Bomber, "自爆"), (TraitId.Stoic, "支援拒否"), (TraitId.Rage, "被弾強化"),
    };
    string Tags(UnitDef d)
        => string.Join("/", tagTable.Where(x => d.Traits.Contains(x.Id)).Select(x => x.Tag));

    int battlesPerJob = yStages.Count * YieldSeeds;

    Console.WriteLine("# 攻撃力1点は、誰の手なら出力になるか（yield）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 yield` の出力。診断用なので docs/ には置かない。");
    Console.WriteLine($"代表編成 {targets.Length} × 全{yStages.Count}ステージ、seed 0..{YieldSeeds - 1}。");
    Console.WriteLine($"編成ごとに「注入なし + メンバー1体ずつ」の {jobs.Count} 通りを回した"
        + $"（検算のぶんを含めて {(long)(jobs.Count + targets.Length) * battlesPerJob:N0} 戦）。");
    Console.WriteLine();
    Console.WriteLine($"**注入**: 駒1体の `AtkBonus` に開戦時から **+{Inject}**。積み上げ（`Rage`）と違って");
    Console.WriteLine("全ターン効くので、**測っているのはその駒が受け取れる強化の上限。**");
    Console.WriteLine();
    Console.WriteLine($"**主指標は `出力/点`** =（注入版の味方全体与ダメ − 注入なし）÷ {Inject}。**1戦あたり**。");
    Console.WriteLine("与ダメは**受け手側＝敵の `DamageTaken` − `TakenFromAlly`**（第13期 Phase DA。");
    Console.WriteLine("毒・燃焼は出どころの駒に載らないので、味方側から数えると毒軸が構造的に過小になる）。");
    Console.WriteLine();
    Console.WriteLine("> **天井・床のセルでは誰に注入しても 0 に潰れる。** 敵を既に削り切っている波では、");
    Console.WriteLine("> 増えた火力は与ダメ総量ではなく**決着ターンの短縮**になって出る（第23期のムドと同じ形）。");
    Console.WriteLine("> **`中間帯`（注入なしの勝率が 5% < x < 95% の波だけを足した列）を必ず併読すること。**");
    Console.WriteLine("> 順位表は2つ出す——`全波` は計画どおりの主指標、`中間帯` は駒が実際に試された場所。");
    Console.WriteLine();
    Console.WriteLine("> **`出力/点` はオーバーキルを含む**（第18期）。`ApplyDamage` は残HPで切り詰めないので、");
    Console.WriteLine("> これは「敵のHPに変換された量」ではなく「振り下ろした量」。**`撃破差` を必ず併読すること**");
    Console.WriteLine("> ——出力だけ増えて撃破が動かない駒は、増えたぶんが過剰殺傷に消えている。");
    Console.WriteLine();
    Console.WriteLine("> **勝率差ではなく `出力/点` で読む。** 火力の増分は総量ではなく決着の短縮として");
    Console.WriteLine("> 出ることがあるので（第23期）、勝率と総量は別々に動く。両方出すが主は `出力/点`。");

    Console.WriteLine();
    Console.WriteLine("## 検算");
    Console.WriteLine();
    int mismatchTotal = baseMismatch.Sum();
    Console.WriteLine("- **注入なしが `Formation` 版（compare の経路）と一致するか**: "
        + $"ずれ {mismatchTotal} 件 / {(long)targets.Length * battlesPerJob:N0} 戦"
        + (mismatchTotal == 0 ? "（勝敗・ターン数ともに完全一致）"
                              : " ← **一致しない。Materialize の使い方が違う**"));
    Console.WriteLine($"- **味方と敵の `Def.Id` 衝突**: {clash.Length} 件"
        + (clash.Length == 0 ? "（受け手側から与ダメを取る前提が成立）"
                             : $" ← **{string.Join(", ", clash)}。敵側の集計に味方が混ざる**"));
    var nono = rows.Where(r => r.Def.Id == UnitCatalog.Nono.Id).ToArray();
    Console.WriteLine(nono.Length == 0
        ? "- **ノノの `出力/点`**: 対象編成にいないので測れていない（絞り込みを外すと出る）"
        : $"- **ノノの `出力/点`**: {nono.Average(r => r.All.Yield):F3}（{nono.Length} 編成）"
          + (Math.Abs(nono.Average(r => r.All.Yield)) < 1e-9
              ? " ← 0.000。`Actions = [Skill]` で攻撃を振らないので当然で、**このモードの動作確認**"
              : " ← **0 でない。攻撃を振らない駒に出力が出ている＝集計が間違っている**"));
    int midCellTotal = midStages.Sum(x => x.Count);
    Console.WriteLine($"- **中間帯のセル**: {midCellTotal} / {targets.Length * yStages.Count}"
        + $"（{midCellTotal * 100.0 / (targets.Length * yStages.Count):F0}%）。"
        + $"中間帯を1つも持たない編成 {midStages.Count(x => x.Count == 0)} 件"
        + "（その編成の行は `中間帯` 側で測れていない）");

    // --- 台の飽和。駒の順位を読む前にここを見る（第21期 swap の作法） ---
    Console.WriteLine();
    Console.WriteLine("## 台の飽和");
    Console.WriteLine();
    Console.WriteLine("**駒の順位より先にここを見る。** 注入なしの勝率が天井（≥95%）か床（≤5%）の波では、");
    Console.WriteLine("増やした火力が与ダメ総量に変換されない——`出力/点` はその波では駒ではなく台を測っている。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 中間帯の波 |" + string.Concat(yStages.Select((_, i) => $" 第{i + 1}波 |"))
        + " 出力/点(全波) | 出力/点(中間帯) |");
    Console.WriteLine("|---|--:|" + string.Concat(yStages.Select(_ => "---:|")) + "---:|---:|");
    for (int b = 0; b < targets.Length; b++)
    {
        var mine = rows.Where(r => r.Build == targets[b].Name).ToArray();
        Console.WriteLine($"| {targets[b].Name} | {midStages[b].Count} |"
            + string.Concat(Enumerable.Range(0, yStages.Count)
                .Select(st => $" {baseWinsF[b][st] * 100.0 / YieldSeeds:F1}% |"))
            + $" {mine.Average(r => r.All.Yield):F2} |"
            + (midStages[b].Count == 0 ? " — |" : $" {mine.Average(r => r.Mid.Yield):F2} |"));
    }
    (double rSat, double rhoSat, int nSat) = Correlate(
        rows.Select(r => (double)r.MidCells).ToArray(), rows.Select(r => r.All.Yield).ToArray());
    Console.WriteLine();
    Console.WriteLine($"行の `出力/点(全波)` と **その編成が持つ中間帯の波の数** の相関: "
        + $"r = {rSat:F3} / ρ = {rhoSat:F3}（n = {nSat}）。");
    Console.WriteLine("**駒の性質を1つも含まない量がこれだけ効く。** 全波の順位表はこの分だけ台に汚染されている。");

    // --- ロスター順位。これが成果物 ---
    var roster = rows.GroupBy(r => r.Def.Id)
        .Select(g => (Def: g.First().Def, N: g.Count(),
                      Yield: g.Average(r => r.All.Yield),
                      Min: g.Min(r => r.All.Yield), Max: g.Max(r => r.All.Yield),
                      Self: g.Average(r => r.All.Self), Win: g.Average(r => r.All.Win),
                      Kill: g.Average(r => r.All.Kill), Turn: g.Average(r => r.All.Turn),
                      Swing: g.Average(r => r.All.Swing), Act: g.Average(r => r.All.Act),
                      Death: g.Average(r => r.All.Death),
                      MidN: g.Count(r => r.MidCells > 0),
                      MidYield: g.Any(r => r.MidCells > 0)
                          ? g.Where(r => r.MidCells > 0).Average(r => r.Mid.Yield) : double.NaN,
                      MidSelf: g.Any(r => r.MidCells > 0)
                          ? g.Where(r => r.MidCells > 0).Average(r => r.Mid.Self) : double.NaN,
                      MidKill: g.Any(r => r.MidCells > 0)
                          ? g.Where(r => r.MidCells > 0).Average(r => r.Mid.Kill) : double.NaN,
                      MidWin: g.Any(r => r.MidCells > 0)
                          ? g.Where(r => r.MidCells > 0).Average(r => r.Mid.Win) : double.NaN,
                      MidTurn: g.Any(r => r.MidCells > 0)
                          ? g.Where(r => r.MidCells > 0).Average(r => r.Mid.Turn) : double.NaN,
                      MidAct: g.Any(r => r.MidCells > 0)
                          ? g.Where(r => r.MidCells > 0).Average(r => r.Mid.Act) : double.NaN))
        .ToArray();

    string F2(double v) => double.IsNaN(v) ? "—" : v.ToString("F2");

    Console.WriteLine();
    Console.WriteLine("## ロスター順位 A（全波。計画どおりの主指標）");
    Console.WriteLine();
    Console.WriteLine("複数の編成に出る駒は編成をまたいで平均した。**`最小`〜`最大` の開きが編成依存の大きさ**");
    Console.WriteLine("——ここが平均と同じ桁で開いている駒は、駒の性質ではなく編成の事情を測っている。");
    Console.WriteLine();
    Console.WriteLine("`本人/点` は注入した駒自身の `DamageToEnemy` の差（味方側の定義。毒・燃焼は載らない）。");
    Console.WriteLine("`出力/点` との差が**その駒を強化したときに他の駒から出たぶん**（連鎖・場の効果）。");
    Console.WriteLine();
    Console.WriteLine("`型(素)` は `Def.Pattern`。**戦闘中の型は特性が書き換える**ので、この列だけを見ると");
    Console.WriteLine("読み違える——熾のホタは燃えている間だけ 貫き ＋ 攻撃力4倍（`PyreTrait`）になる。");
    Console.WriteLine();
    Console.WriteLine("| 順 | 駒 | 編成 | 出力/点 | 最小 | 最大 | 中間帯 | 本人/点 | 撃破差 | 勝率差 | 決着T差 | 振/戦 | 干渉/戦 | 攻 | 型(素) | 手番 | 落ちた | 注記 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|--:|---|");
    var rankA = roster.OrderByDescending(x => x.Yield).ThenBy(x => x.Def.Id).ToArray();
    for (int i = 0; i < rankA.Length; i++)
    {
        var x = rankA[i];
        Console.WriteLine($"| {i + 1} | {x.Def.Name} | {x.N} | **{x.Yield:F2}** | {x.Min:F2} | {x.Max:F2} "
            + $"| {F2(x.MidYield)} | {x.Self:F2} | {x.Kill:+0.000;-0.000;0.000} | {x.Win:+0.0;-0.0;0.0}pt "
            + $"| {x.Turn:+0.00;-0.00;0.00} | {x.Swing:F2} | {x.Act:F2} | {x.Def.Attack} | {Pat(x.Def)} "
            + $"| {Acts(x.Def)} | {x.Death:F2} | {Tags(x.Def)} |");
    }

    Console.WriteLine();
    Console.WriteLine("## ロスター順位 B（中間帯だけ。駒が実際に試された場所）");
    Console.WriteLine();
    Console.WriteLine("天井・床の波を落として測り直した順位。`編成` は中間帯を持つ編成の数で、");
    Console.WriteLine("**ここが 0 の駒はこの表に出ない**（測れていない、が正しい報告）。");
    Console.WriteLine();
    Console.WriteLine("| 順 | 駒 | 編成 | 出力/点 | 全波 | 本人/点 | 撃破差 | 勝率差 | 決着T差 | 干渉/戦 | 攻 | 型(素) | 手番 | 注記 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|---|");
    var rankB = roster.Where(x => x.MidN > 0).OrderByDescending(x => x.MidYield).ThenBy(x => x.Def.Id).ToArray();
    for (int i = 0; i < rankB.Length; i++)
    {
        var x = rankB[i];
        Console.WriteLine($"| {i + 1} | {x.Def.Name} | {x.MidN} | **{x.MidYield:F2}** | {x.Yield:F2} "
            + $"| {F2(x.MidSelf)} | {x.MidKill:+0.000;-0.000;0.000} | {x.MidWin:+0.0;-0.0;0.0}pt "
            + $"| {x.MidTurn:+0.00;-0.00;0.00} | {F2(x.MidAct)} | {x.Def.Attack} | {Pat(x.Def)} "
            + $"| {Acts(x.Def)} | {Tags(x.Def)} |");
    }
    var both = roster.Where(x => x.MidN > 0).ToArray();
    (double rAB, double rhoAB, int nAB) = Correlate(both.Select(x => x.Yield).ToArray(),
                                                    both.Select(x => x.MidYield).ToArray());
    Console.WriteLine();
    Console.WriteLine($"A と B の一致度: r = {rAB:F3} / ρ = {rhoAB:F3}（n = {nAB}）。"
        + "**ここが低ければ、全波の順位は台の飽和を測っている。**");

    // --- 仮説の検定（計画 §4）---
    // 出力/点 ≒（1発が当たる対象数）×（手番あたりの発射回数）× 生存ターン数 で説明できるか。
    // 右辺は既に測れている——**`干渉/戦` がその実測値**（その駒が起点になってダメージを通した
    // 回数で、対象数も発射回数も生存ターン数も掛かった後の数字）。`振/戦` は手番の振りだけを
    // 数えるので、反撃・追い打ち・範囲の巻き込みが落ちる。2つ並べれば落ちる分の効きが読める。
    //
    // **判定は3通りを必ず全部出す**（第15期の作法）。逆しま（ウツ）を外した版を出すのは、
    // 計画が測る前に「負になるはず」と書いていた**唯一の例外**だから——後から外すのではなく、
    // 予測に入っていた駒を予測どおり別扱いにするだけ。
    var midRows = rows.Where(r => r.MidCells > 0).ToArray();
    var midNoPerv = midRows.Where(r => !r.Def.Traits.Contains(TraitId.Perverse)).ToArray();

    (double R, double Rho)[] Set(IReadOnlyList<(string Build, UnitDef Def, int Slot, int MidCells,
        (double Yield, double Self, double Win, double Kill, double Turn, double Swing, double Act, double Death) All,
        (double Yield, double Self, double Win, double Kill, double Turn, double Swing, double Act, double Death) Mid)> src,
        bool mid)
    {
        double[] y = src.Select(r => mid ? r.Mid.Yield : r.All.Yield).ToArray();
        var acts = src.Select(r => mid ? r.Mid.Act : r.All.Act).ToArray();
        var swings = src.Select(r => mid ? r.Mid.Swing : r.All.Swing).ToArray();
        var atks = src.Select(r => (double)r.Def.Attack).ToArray();
        (double r1, double p1, int _) = Correlate(acts, y);
        (double r2, double p2, int _) = Correlate(swings, y);
        (double r3, double p3, int _) = Correlate(atks, y);
        return new[] { (r1, p1), (r2, p2), (r3, p3) };
    }

    var setAll = Set(rows, mid: false);
    var setMid = Set(midRows, mid: true);
    var setMidNp = Set(midNoPerv, mid: true);

    Console.WriteLine();
    Console.WriteLine("## 仮説の検定");
    Console.WriteLine();
    Console.WriteLine("**`出力/点` ≒（1発が当たる対象数）×（手番あたりの発射回数）× 生存ターン数** で説明できるか。");
    Console.WriteLine("右辺は既に測れている——**`干渉/戦` がその実測値**（その駒が起点になって");
    Console.WriteLine("ダメージを通した回数。対象数も発射回数も生存ターン数も掛かった後の数字）。");
    Console.WriteLine("`振/戦` は手番の振りだけなので、反撃・追い打ち・範囲の巻き込みが落ちる。");
    Console.WriteLine();
    Console.WriteLine("行は 駒 × 編成。説明変数はすべて**注入なし**の値。**3通りを全部出す**——");
    Console.WriteLine($"(a) 全波 {rows.Count} 行 / (b) 中間帯 {midRows.Length} 行 / "
        + $"(c) 中間帯から逆しまを除いた {midNoPerv.Length} 行。");
    Console.WriteLine("(c) を出すのは、計画が**測る前に**「逆しまは負になるはず」と書いていた唯一の例外だから");
    Console.WriteLine("（後から外れ値を外すのではなく、予測に入っていた駒を予測どおり別扱いにする）。");
    Console.WriteLine();
    Console.WriteLine("| 説明変数 | (a) r | (a) ρ | (b) r | (b) ρ | (c) r | (c) ρ |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    string[] names = { "`干渉/戦`", "`振/戦`", "`Def.Attack`" };
    for (int k = 0; k < names.Length; k++)
        Console.WriteLine($"| {names[k]} | {setAll[k].R:F3} | {setAll[k].Rho:F3} "
            + $"| {setMid[k].R:F3} | {setMid[k].Rho:F3} | {setMidNp[k].R:F3} | {setMidNp[k].Rho:F3} |");
    Console.WriteLine();
    Console.WriteLine("説明できるなら、**残りのロスターは測らずに監査できる**——攻撃型と手番の持ち方を");
    Console.WriteLine("見れば、その駒が強化を受け取れるかどうかが分かることになる。");
    Console.WriteLine();
    Console.WriteLine("`倍率` = `出力/点` ÷ `干渉/戦`。1回の干渉あたり、注入1点が何倍になって出たか。");
    Console.WriteLine("素朴には「素の殴りだけで 1.0 前後、狙撃（攻撃力2倍）や棘（攻撃力の2倍で反撃）は上」");
    Console.WriteLine("だが、**実測はほとんどの駒が 1.0 未満**——出力は敵の総HPで頭打ちになるので、");
    Console.WriteLine("増えたぶんは過剰殺傷と決着の短縮に消える（干渉の回数が多い駒ほど下がる）。");
    Console.WriteLine();
    Console.WriteLine("**1.0 を超えるのは毒・燃焼軸に偏るが、これは効率ではなく分母の穴。** 毒・燃焼の刻みは");
    Console.WriteLine("`source` を持たないので `干渉/戦` に載らず（`docs/pulse.md` と同じ過小）、分母だけが小さい。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 出力/点(中間帯) | 干渉/戦(中間帯) | 倍率 | 出力/点(全波) | 干渉/戦(全波) | 倍率 | 型(素) | 注記 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|---|");
    foreach (var x in rankA)
        Console.WriteLine($"| {x.Def.Name} | {F2(x.MidYield)} | {F2(x.MidAct)} | "
            + (double.IsNaN(x.MidAct) || x.MidAct < 0.05 ? "—" : $"{x.MidYield / x.MidAct:F2}")
            + $" | {x.Yield:F2} | {x.Act:F2} | "
            + (x.Act < 0.05 ? "—" : $"{x.Yield / x.Act:F2}")
            + $" | {Pat(x.Def)} | {Tags(x.Def)} |");

    // --- 編成別の内訳 ---
    Console.WriteLine();
    Console.WriteLine("## 編成別");
    Console.WriteLine();
    Console.WriteLine("見出しの `勝率` は注入なしの平均（`docs/balance.md` と同じ計算・同じ seed 帯）。");
    Console.WriteLine("`中間帯` が空の編成では、下の数字は**駒ではなく台の天井**を測っている。");
    for (int b = 0; b < targets.Length; b++)
    {
        var bs = res[baseIdx[b]];
        Console.WriteLine();
        Console.WriteLine($"### {targets[b].Name}");
        Console.WriteLine();
        Console.WriteLine($"注入なし: 勝率 {baseWinsF[b].Sum() * 100.0 / battlesPerJob:F1}% / "
            + $"味方全体の与ダメ {bs.Output.Sum() / (double)battlesPerJob:F0} / "
            + $"撃破 {bs.Kills.Sum() / (double)battlesPerJob:F2} / "
            + $"決着 {bs.Turns.Sum() / (double)battlesPerJob:F1}T / "
            + $"中間帯 {(midStages[b].Count == 0 ? "なし" : string.Join("・", midStages[b].Select(st => $"第{st + 1}波")))}");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 席 | 出力/点 | 中間帯 | 本人/点 | 撃破差 | 勝率差 | 決着T差 | 振/戦 | 干渉/戦 | 落ちた |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in rows.Where(r => r.Build == targets[b].Name))
            Console.WriteLine($"| {r.Def.Name} | {FormationRules.SeatNames[r.Slot]} | **{r.All.Yield:F2}** "
                + $"| {(r.MidCells == 0 ? "—" : r.Mid.Yield.ToString("F2"))} | {r.All.Self:F2} "
                + $"| {r.All.Kill:+0.000;-0.000;0.000} | {r.All.Win:+0.0;-0.0;0.0}pt | {r.All.Turn:+0.00;-0.00;0.00} "
                + $"| {r.All.Swing:F2} | {r.All.Act:F2} | {r.All.Death:F2} |");
        Console.Out.Flush();
    }
    return;
}

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

if (focusId == "wave2")
{
    const int W2Seeds = 200;        // compare / pulse と同じ
    const int W2ProbeSeeds = 20;    // 表C の課金対象の判定（verbose が要る）だけこの本数

    int w2Want = args.Length > 2 && int.TryParse(args[2], out int w2Arg) ? w2Arg : 2;
    int w2Idx = Math.Clamp(w2Want, 1, EnemyCatalog.Stages.Count) - 1;

    var w2Builds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> w2Stages = EnemyCatalog.Stages;

    // --- 機械的な判定 --------------------------------------------------------------------------
    // **役割名で数えない**（第32期の「単一窓口は call-site grep で数える」の適用）。
    // どれも窓口の呼び出し元をそのまま写しただけで、この配列以外に判定は置いていない。

    // ターン外の行動 = BattleContext.CanActOutOfTurn を通る4本（Traits.cs 側の呼び出し元）。
    // 肩代わり（庇う・分かち・巨躯・後備え・棘守り）はダメージの再分配であって行動ではないので入らない。
    var w2OutOfTurn = new[] { TraitId.Thorns, TraitId.Avenge, TraitId.Displaced, TraitId.Pursuer };

    // 回復 = ctx.Heal の呼び出し元9経路（CLAUDE.md の「駒の説明文から数えると必ず抜ける」）。
    // 巨躯（吸い・還し）と墓守は説明文のどこにも回復と書いていないが回復する。
    var w2Healers = new[]
    {
        TraitId.Drain, TraitId.Necro, TraitId.Colossus, TraitId.Mender, TraitId.Alms,
        TraitId.Suture, TraitId.Devour, TraitId.Drifter, TraitId.Forsake
    };

    // 後列に届く手段（指示書 §2 表B の「貫き・標・毒・範囲」）。
    //   範囲/貫き = Def.Pattern が Single 以外、または ModifyPattern を上書きする特性
    //   毒        = 敵に毒を積む特性（澱み喰い・毒喰らいは読み手なので入らない）
    //   標        = 敵に標を付ける特性。囃し立て（Marker）は**味方**に付けるので入らない
    var w2Repattern = new[] { TraitId.Sniper, TraitId.Necro, TraitId.Scale, TraitId.Pyre };
    var w2Poison = new[] { TraitId.Venom, TraitId.Amplifier, TraitId.Contagion, TraitId.Miasma };
    var w2Mark = new[] { TraitId.Divert };

    bool W2HasAny(Formation f, TraitId[] ids)
        => f.Occupied().Any(o => o.Def.Traits.Any(ids.Contains));
    int W2CountHas(Formation f, TraitId[] ids)
        => f.Occupied().Count(o => o.Def.Traits.Any(ids.Contains));
    bool W2Reaches(Formation f)
        => f.Occupied().Any(o => o.Def.Pattern != AttackPattern.Single)
           || W2HasAny(f, w2Repattern) || W2HasAny(f, w2Poison) || W2HasAny(f, w2Mark);
    int W2TotalAttack(Formation f) => f.Occupied().Sum(o => o.Def.Attack);

    // --- 条件を組む ----------------------------------------------------------------------------
    // 同数値の対照駒。粛・渇き・軛・殉教はどれも素の駒と数値・型・速さが1つも違わない
    // ——差し替えれば盤面が完全に元へ戻る（第40期の告発人と同じ形）。
    var w2Controls = new Dictionary<string, (UnitDef Def, string Note)>
    {
        ["husher"] = (EnemyCatalog.Recruit, "討伐隊の新兵 45/11/6"),
        ["droughter"] = (EnemyCatalog.Knight, "巡礼騎士 75/15/7"),
        ["yoker"] = (EnemyCatalog.Warden, "城塞の重装兵 145/12/3"),
        ["axeman_g"] = (EnemyCatalog.Axeman2, "戦斧兵 52/11/5 薙ぎ"),
    };

    Formation W2Swap(Formation f, int slot, UnitDef? def)
    {
        Formation c = f.Clone();
        c[slot] = def;
        return c;
    }

    // 波ごとの「規則を切った版」。保持者を同数値の対照駒に差し替える。
    (Formation Enemy, string Label)? W2RuleOff(int idx)
    {
        Formation e = w2Stages[idx].Enemy;
        foreach ((int slot, UnitDef def) in e.Occupied())
            if (w2Controls.TryGetValue(def.Id, out var ctl))
                return (W2Swap(e, slot, ctl.Def),
                        FormationRules.SeatNames[slot] + " " + def.Name + " → " + ctl.Def.Name
                        + "（" + ctl.Note + "・同数値）");
        return null;
    }

    Formation w2Base = w2Stages[w2Idx].Enemy;

    var w2Conds = new List<(string Id, string Name, Formation Enemy, HushRule? Hush, YokeRule? Yoke)>
    {
        ("0", "基準（現行のまま）", w2Base, null, null)
    };

    // 条件1: 規則を無効にする。フラグのある規則はフラグ側を主に採り、対照駒側は検算 1' に回す。
    var w2Off = W2RuleOff(w2Idx);
    bool w2HasHush = w2Base.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Hush));
    bool w2HasYoke = w2Base.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Yoke));
    if (w2HasHush)
        w2Conds.Add(("1", "規則を無効（HushRule(Active:false)・盤面は現行のまま）",
                     w2Base, new HushRule(Active: false), null));
    else if (w2HasYoke)
        w2Conds.Add(("1", "規則を無効（YokeRule(Active:false)・盤面は現行のまま）",
                     w2Base, null, new YokeRule(YokeTrait.Cap, Active: false)));
    if (w2Off is { } off)
        w2Conds.Add((w2HasHush || w2HasYoke ? "1b" : "1",
                     "規則の保持者を同数値の対照に差し替え（" + off.Label + "）",
                     off.Enemy, null, null));

    // 条件2〜: 敵を1体ずつ空席にする。埋めない（埋めると2変数が動く）。
    int w2Seat = 2;
    foreach ((int slot, UnitDef def) in w2Base.Occupied())
    {
        w2Conds.Add((w2Seat.ToString(),
                     FormationRules.SeatNames[slot] + " " + def.Name + " を抜く（空席）",
                     W2Swap(w2Base, slot, null), null, null));
        w2Seat++;
    }

    // --- 測る ----------------------------------------------------------------------------------
    var w2Sw = new System.Diagnostics.Stopwatch();

    // 基準は全5波を回す（0-1 の分布と、docs/balance.md との突き合わせを兼ねる）。
    w2Sw.Restart();
    var w2Grid = new double[w2Builds.Length][];
    var w2Turns = new double[w2Builds.Length];
    var w2BackDmg = new double[w2Builds.Length];
    var w2BackKill = new double[w2Builds.Length];
    string[] w2BackIds = w2Base.Occupied()
        .Where(o => FormationRules.RowOf(o.Slot) == Row.Back)
        .Select(o => o.Def.Id).Distinct().ToArray();
    for (int b = 0; b < w2Builds.Length; b++)
    {
        Formation f = w2Builds[b].F;
        w2Grid[b] = new double[w2Stages.Count];
        for (int st = 0; st < w2Stages.Count; st++)
        {
            int wins = 0, turns = 0, backDmg = 0, backKill = 0;
            for (int seed = 0; seed < W2Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, w2Stages[st].Enemy, seed, verbose: false);
                if (r.PlayerWon) wins++;
                if (st != w2Idx) continue;
                turns += r.Turns;
                foreach (string id in w2BackIds)
                    if (r.TallyByUnit.TryGetValue(id, out UnitTally? t))
                    {
                        backDmg += t.DamageTaken;
                        backKill += t.Deaths;
                    }
            }
            w2Grid[b][st] = wins * 100.0 / W2Seeds;
            if (st == w2Idx)
            {
                w2Turns[b] = (double)turns / W2Seeds;
                w2BackDmg[b] = (double)backDmg / W2Seeds;
                w2BackKill[b] = (double)backKill / W2Seeds;
            }
        }
    }
    double w2GridSec = w2Sw.Elapsed.TotalSeconds;

    // 条件ごとの対象波の勝率
    var w2Cells = new double[w2Conds.Count][];
    var w2Secs = new double[w2Conds.Count];
    for (int c = 0; c < w2Conds.Count; c++)
    {
        w2Sw.Restart();
        var row = new double[w2Builds.Length];
        for (int b = 0; b < w2Builds.Length; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < W2Seeds; seed++)
                if (BattleEngine.Run(w2Builds[b].F, w2Conds[c].Enemy, seed, verbose: false,
                                     yoke: w2Conds[c].Yoke, hush: w2Conds[c].Hush).PlayerWon) wins++;
            row[b] = wins * 100.0 / W2Seeds;
        }
        w2Cells[c] = row;
        w2Secs[c] = w2Sw.Elapsed.TotalSeconds;
    }

    // --- 出力 ----------------------------------------------------------------------------------
    Console.WriteLine("# 波の解剖: 第" + (w2Idx + 1) + "波（" + w2Stages[w2Idx].Name + "）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 wave2 " + (w2Idx + 1) + "` の出力。");
    Console.WriteLine($"`CompareBuilds()` の {w2Builds.Length} 行 × seed 0..{W2Seeds - 1}。");
    Console.WriteLine("`Traits.cs` / `UnitCatalog.cs` / `Stages` / `CompareBuilds()` は1行も触っていない。");
    Console.WriteLine("**診断用なので `docs/` には置かない。**");

    // --- 0-1 ---
    Console.WriteLine();
    Console.WriteLine("## 0-1. 分布（基準・全5波）");
    Console.WriteLine();
    Console.WriteLine("| 波 | 平均 | 0.0% | 100.0% | 中間（情報セル） |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int st = 0; st < w2Stages.Count; st++)
    {
        var v = w2Grid.Select(r => r[st]).ToArray();
        Console.WriteLine($"| 第{st + 1}波 | {v.Average():F2} | {v.Count(x => x == 0.0)} "
                          + $"| {v.Count(x => x == 100.0)} | {v.Count(x => x > 0.0 && x < 100.0)} |");
    }

    Console.WriteLine();
    Console.WriteLine("### `docs/balance.md` との突き合わせ用（compare と同じ書式）");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(w2Stages.Select((_, i) => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(w2Stages.Select(_ => "---:|")));
    for (int b = 0; b < w2Builds.Length; b++)
        Console.WriteLine($"| {w2Builds[b].Name} |"
                          + string.Concat(w2Grid[b].Select(x => $" {x:F1}% |")));

    // --- 0-2 / 0-3 ---
    Console.WriteLine();
    Console.WriteLine("## 0-2 / 0-3. 全5波の敵と波ルール");
    Console.WriteLine();
    Console.WriteLine("| 波 | 席 | 駒 | HP | 攻 | 速 | 型 | 特性 | 行動 |");
    Console.WriteLine("|---|---|---|--:|--:|--:|---|---|---|");
    for (int st = 0; st < w2Stages.Count; st++)
        foreach ((int slot, UnitDef def) in w2Stages[st].Enemy.Occupied())
        {
            string acts = def.Actions is { } a
                ? string.Join(" → ", a.Select(x => x.Kind == ActionKind.Charge
                      ? "溜め" : x.Kind.ToString() + (x.AttackPercent == 100 ? "" : "(" + x.AttackPercent + "%)")))
                : "—";
            Console.WriteLine($"| 第{st + 1}波 | {FormationRules.SeatNames[slot]} | {def.Name} "
                + $"| {def.MaxHp} | {def.Attack} | {def.Speed} | {def.Pattern} "
                + $"| {(def.Traits.Count == 0 ? "—" : string.Join(" / ", def.Traits))} | {acts} |");
        }

    // --- 0-4 ---
    Console.WriteLine();
    Console.WriteLine("## 0-4. 第" + (w2Idx + 1) + "波の 100.0% 群 と 0.0% 群");
    Console.WriteLine();
    Console.WriteLine("`外` = ターン外の行動を持つ駒の枚数（棘・仇討ち・軋み・追い打ち）。");
    Console.WriteLine("`届` = 後列に届く手段（貫き・標・毒・範囲）を持つか。`癒` = 回復経路を持つか。");
    Console.WriteLine("`後列与ダメ` / `後列撃破` はこの波の後列2体に通した量（1戦あたり・実測）。");
    foreach (var (grpName, want) in new[] { ("100.0% 群", 100.0), ("0.0% 群", 0.0) })
    {
        Console.WriteLine();
        Console.WriteLine("### " + grpName);
        Console.WriteLine();
        Console.WriteLine("| 編成 | 駒 | 外 | 届 | 癒 | 総攻 | 決着T | 後列与ダメ | 後列撃破 |");
        Console.WriteLine("|---|---|--:|:-:|:-:|--:|--:|--:|--:|");
        for (int b = 0; b < w2Builds.Length; b++)
        {
            if (w2Grid[b][w2Idx] != want) continue;
            Formation f = w2Builds[b].F;
            Console.WriteLine($"| {w2Builds[b].Name} "
                + $"| {string.Join("・", f.Occupied().Select(o => o.Def.Name))} "
                + $"| {W2CountHas(f, w2OutOfTurn)} | {(W2Reaches(f) ? "○" : "—")} "
                + $"| {(W2HasAny(f, w2Healers) ? "○" : "—")} | {W2TotalAttack(f)} "
                + $"| {w2Turns[b]:F1} | {w2BackDmg[b]:F0} | {w2BackKill[b]:F2} |");
        }
    }

    // --- 表A ---
    Console.WriteLine();
    Console.WriteLine("## 表A. 関門ごとの寄与");
    Console.WriteLine();
    Console.WriteLine("**主要な指標は `100.0%` の列。** 平均ではない。");
    Console.WriteLine("`Δ100%` は基準からの増減で、**外して 100.0% が増えるならその関門は天井を押さえている。**");
    Console.WriteLine();
    Console.WriteLine("| 条件 | 内容 | 平均 | 100.0% | Δ100% | 0.0% | 中間 | 最大差 | 動いた行 | 秒 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int c = 0; c < w2Conds.Count; c++)
    {
        double[] v = w2Cells[c], b0 = w2Cells[0];
        double maxDiff = 0; int moved = 0;
        for (int b = 0; b < v.Length; b++)
        {
            double d = v[b] - b0[b];
            if (Math.Abs(d) > Math.Abs(maxDiff)) maxDiff = d;
            if (d != 0) moved++;
        }
        Console.WriteLine($"| {w2Conds[c].Id} | {w2Conds[c].Name} | {v.Average():F2} "
            + $"| {v.Count(x => x == 100.0)} | {v.Count(x => x == 100.0) - b0.Count(x => x == 100.0):+0;-0;0} "
            + $"| {v.Count(x => x == 0.0)} | {v.Count(x => x > 0 && x < 100)} "
            + $"| {maxDiff:+0.0;-0.0;0.0} | {moved} | {w2Secs[c]:F1} |");
    }

    // --- 表B ---
    Console.WriteLine();
    Console.WriteLine("## 表B. 100% 群 と 0% 群 の性格");
    Console.WriteLine();
    var w2Hi = Enumerable.Range(0, w2Builds.Length).Where(b => w2Grid[b][w2Idx] == 100.0).ToArray();
    var w2Lo = Enumerable.Range(0, w2Builds.Length).Where(b => w2Grid[b][w2Idx] == 0.0).ToArray();
    var w2Mid = Enumerable.Range(0, w2Builds.Length)
                          .Where(b => w2Grid[b][w2Idx] > 0 && w2Grid[b][w2Idx] < 100).ToArray();
    string W2Cmp(double hi, double lo) =>
        lo == 0 && hi == 0 ? "—" : lo == 0 ? "∞" : hi == 0 ? "0.00倍" : $"{hi / lo:F2}倍";
    var w2Items = new (string Name, Func<int, double> V)[]
    {
        ("ターン外の行動を持つ駒の平均枚数", b => W2CountHas(w2Builds[b].F, w2OutOfTurn)),
        ("後列に届く手段を持つ行の割合(%)", b => W2Reaches(w2Builds[b].F) ? 100 : 0),
        ("回復を持つ行の割合(%)", b => W2HasAny(w2Builds[b].F, w2Healers) ? 100 : 0),
        ("平均の総攻撃力", b => W2TotalAttack(w2Builds[b].F)),
        ("決着ターンの平均", b => w2Turns[b]),
        ("（実測）後列2体への与ダメ/戦", b => w2BackDmg[b]),
        ("（実測）後列2体の撃破/戦", b => w2BackKill[b]),
    };
    Console.WriteLine($"| 項目 | 100%群({w2Hi.Length}行) | 0%群({w2Lo.Length}行) | 比 | 中間帯({w2Mid.Length}行) |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (var (nm, val) in w2Items)
    {
        double hi = w2Hi.Length == 0 ? 0 : w2Hi.Average(val);
        double lo = w2Lo.Length == 0 ? 0 : w2Lo.Average(val);
        double md = w2Mid.Length == 0 ? 0 : w2Mid.Average(val);
        Console.WriteLine($"| {nm} | {hi:F2} | {lo:F2} | {W2Cmp(hi, lo)} | {md:F2} |");
    }

    // --- 表C ---
    // 各波の規則を同数値の対照で外し、**課金対象を持つ行と持たない行に分けて**変化を見る。
    // 「課金対象を持たない編成にも効くか」がこの表の問い（指示書 §0 の仮説の検証）。
    Console.WriteLine();
    Console.WriteLine("## 表C. 波ルールの性格の比較（全波を実測）");
    Console.WriteLine();
    Console.WriteLine("**規則の外し方は全波で同じ**——保持者を**同数値・特性なしの対照駒**に差し替える");
    Console.WriteLine("（粛・渇き・軛・殉教はどれも素の駒と数値・型・速さが1つも違わない）。");
    Console.WriteLine("`課金対象` の判定は窓口の呼び出し元から機械的に引く（役割名で数えない）:");
    Console.WriteLine();
    Console.WriteLine("- 粛（第二波）= ターン外の行動を持つ駒 ≥1（`CanActOutOfTurn` を通る4本）");
    Console.WriteLine("- 渇き（第三波）= 回復経路の特性 ≥1（`ctx.Heal` の呼び出し元9経路）");
    Console.WriteLine($"- 軛（第四波）= 規則を外した盤面で単発 > {YokeTrait.Cap} を敵に通す行（seed 0..{W2ProbeSeeds - 1} の実測）");
    Console.WriteLine("- 殉教（第五波）= 規則を外した盤面で単体攻撃を敵に振る行（同上。庇うは Single にしか割り込まない）");
    Console.WriteLine();
    Console.WriteLine("| 波 | ルール | 対照 | 課金対象 | Δ平均(対象) | Δ平均(非対象) | 非対象で動いた行 | 非対象の最大差 | 課金対象を持たない編成にも効くか |");
    Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|");
    for (int st = 0; st < w2Stages.Count; st++)
    {
        var offC = W2RuleOff(st);
        if (offC is not { } o)
        {
            Console.WriteLine($"| 第{st + 1}波 | なし | — | — | — | — | — | — | 規則を持たない |");
            continue;
        }
        Formation offEnemy = o.Enemy;
        UnitDef holder = w2Stages[st].Enemy.Occupied()
                            .First(x => w2Controls.ContainsKey(x.Def.Id)).Def;

        // 課金対象の判定
        var taxed = new bool[w2Builds.Length];
        bool probe = holder.Traits.Contains(TraitId.Yoke) || holder.Traits.Contains(TraitId.Martyr);
        for (int b = 0; b < w2Builds.Length; b++)
        {
            Formation f = w2Builds[b].F;
            if (holder.Traits.Contains(TraitId.Hush)) taxed[b] = W2CountHas(f, w2OutOfTurn) > 0;
            else if (holder.Traits.Contains(TraitId.Drought)) taxed[b] = W2HasAny(f, w2Healers);
            else if (probe)
            {
                int lo = f.Count, hi = lo + offEnemy.Count;   // 敵の InstanceId の範囲（Add は味方→敵の順）
                bool hit = false;
                for (int seed = 0; seed < W2ProbeSeeds && !hit; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, offEnemy, seed, verbose: true);
                    hit = holder.Traits.Contains(TraitId.Yoke)
                        ? r.Events.Any(e => e.Kind == BattleEventKind.Damage && !e.FriendlyFire
                                            && e.TargetId >= lo && e.TargetId < hi
                                            && e.Amount > YokeTrait.Cap)
                        : r.Events.Any(e => e.Kind == BattleEventKind.Attack
                                            && e.Pattern == AttackPattern.Single
                                            && e.TargetId >= lo && e.TargetId < hi);
                }
                taxed[b] = hit;
            }
        }

        // 基準と対照
        double sumT = 0, sumN = 0, maxN = 0; int nT = 0, nN = 0, movedN = 0;
        for (int b = 0; b < w2Builds.Length; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < W2Seeds; seed++)
                if (BattleEngine.Run(w2Builds[b].F, offEnemy, seed, verbose: false).PlayerWon) wins++;
            double d = wins * 100.0 / W2Seeds - w2Grid[b][st];
            if (taxed[b]) { sumT += d; nT++; }
            else
            {
                sumN += d; nN++;
                if (d != 0) movedN++;
                if (Math.Abs(d) > Math.Abs(maxN)) maxN = d;
            }
        }
        string verdict = nN == 0 ? "非対象が0行（判定不能）"
                       : movedN == 0 ? "**効かない**（非対象は全行 ±0.0）"
                       : $"効く（非対象 {movedN}/{nN} 行が動く）";
        Console.WriteLine($"| 第{st + 1}波 | {holder.Name} | {w2Controls[holder.Id].Def.Name} "
            + $"| {nT}/{w2Builds.Length} | {(nT == 0 ? 0 : sumT / nT):+0.00;-0.00;0.00} "
            + $"| {(nN == 0 ? 0 : sumN / nN):+0.00;-0.00;0.00} | {movedN} "
            + $"| {maxN:+0.0;-0.0;0.0} | {verdict} |");
        Console.Out.Flush();
    }

    // 断罪（第五波）だけは catalog に同数値の対照が無いので、ここでローカルに作る
    // （`gradient` / `aim` / `route` と同じ扱い。`UnitCatalog` には戻さない）。
    // 数値・型・速さは Hero2 / Seer と1つも違わず、差分は Condemn を落としただけ。
    //
    // **課金対象は粛と同じではない。** 断罪は `ctx.InReaction` の中でしか発火しないので、
    // `ctx.Reaction` で包まれる2本（棘・仇討ち）だけが対象——軋みは `ctx.Interrupt`、
    // 追い打ちは `ctx.PerformAttack` の直呼びなので、`CanActOutOfTurn` は通るが断罪は踏まない。
    var w2Reactors = new[] { TraitId.Thorns, TraitId.Avenge };
    var w2PlainHero = new UnitDef
    {
        Id = "hero_v_plain", Name = "勇者候補（断罪なし）", MaxHp = 90, Attack = 20, Speed = 14,
        Traits = Array.Empty<TraitId>()
    };
    var w2PlainSeer = new UnitDef
    {
        Id = "seer_plain", Name = "審問官（断罪なし）", MaxHp = 76, Attack = 12, Speed = 10,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.All
    };
    {
        const int W2Fifth = 4;
        Formation cond = w2Stages[W2Fifth].Enemy.Clone();
        foreach ((int slot, UnitDef def) in w2Stages[W2Fifth].Enemy.Occupied())
        {
            if (def.Id == "hero_v") cond[slot] = w2PlainHero;
            if (def.Id == "seer") cond[slot] = w2PlainSeer;
        }
        double sumT = 0, sumN = 0, maxN = 0; int nT = 0, nN = 0, movedN = 0;
        for (int b = 0; b < w2Builds.Length; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < W2Seeds; seed++)
                if (BattleEngine.Run(w2Builds[b].F, cond, seed, verbose: false).PlayerWon) wins++;
            double d = wins * 100.0 / W2Seeds - w2Grid[b][W2Fifth];
            if (W2HasAny(w2Builds[b].F, w2Reactors)) { sumT += d; nT++; }
            else
            {
                sumN += d; nN++;
                if (d != 0) movedN++;
                if (Math.Abs(d) > Math.Abs(maxN)) maxN = d;
            }
        }
        string verdict = movedN == 0 ? "**効かない**（非対象は全行 ±0.0）"
                                     : $"効く（非対象 {movedN}/{nN} 行が動く）";
        Console.WriteLine($"| 第5波 | 断罪（勇者候補・審問官） | 同数値の断罪なし2体 "
            + $"| {nT}/{w2Builds.Length} | {(nT == 0 ? 0 : sumT / nT):+0.00;-0.00;0.00} "
            + $"| {(nN == 0 ? 0 : sumN / nN):+0.00;-0.00;0.00} | {movedN} "
            + $"| {maxN:+0.0;-0.0;0.0} | {verdict} |");
        Console.WriteLine();
        Console.WriteLine("> 第5波の行は2本ある——**殉教（庇う）と断罪は課金対象が違う。**");
        Console.WriteLine("> 断罪の対象は `ctx.Reaction` に包まれる2本（棘・仇討ち）だけで、");
        Console.WriteLine("> 軋み（`ctx.Interrupt`）と追い打ち（`PerformAttack` の直呼び）は `CanActOutOfTurn` を");
        Console.WriteLine("> 通るのに断罪は踏まない。**粛の4本より狭い。**");
    }

    // --- 付録 ---
    Console.WriteLine();
    Console.WriteLine("## 付録: 条件別の全行（対象波の勝率）");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(w2Conds.Select(c => $" {c.Id} |")));
    Console.WriteLine("|---|" + string.Concat(w2Conds.Select(_ => "---:|")));
    for (int b = 0; b < w2Builds.Length; b++)
        Console.WriteLine($"| {w2Builds[b].Name} |"
            + string.Concat(w2Cells.Select(r => $" {r[b]:F1} |")));

    Console.WriteLine();
    Console.WriteLine($"基準の全5波グリッド {w2GridSec:F1} 秒 / 条件ごとの所要は表A の `秒` 列。");
    return;
}

if (focusId == "ledger") { LedgerDiag.Run(args, stageIndex); return; }

if (focusId == "lit") { LitDiag.Run(args, stageIndex); return; }

if (focusId == "reader") { ReaderDiag.Run(args, stageIndex); return; }

if (focusId == "offturn") { OffturnDiag.Run(args, stageIndex); return; }

if (focusId == "watch") { WatchDiag.Run(args, stageIndex); return; }

// compare モード: 代表的な編成を全ステージで比較する。
// 総当たりは駒が増えるほど爆発するので、系統ごとの当たり外れはこちらで見る。
if (focusId == "compare")
{
    var builds = CompareBuilds();

    const int CompareSeeds = 200;

    // 第126期 段1 —— 勝ち方の質（残存・圧勝率・完全勝利・全滅勝ち）。
    //
    // **`docs/balance.md` には1文字も足さない。別の生成物（`docs/quality.md`）にする。**
    // 指示書 §4-1 は「`compare` の出力に足す」と書いているが、**同じ節が挙げている理由
    // （(G8) の検算が使えなくなる）が、足した瞬間に現実になる**——`docs/balance.md` を読む
    // 自己検査は「`| ` で始まり `%` を含む行」を**位置で**突き合わせる版（`curse check`）と、
    // **行名をキーに 2..6 列目を数値として読む**版（`encore check`）の2種類があり、
    // 勝ち方の質の表は**どちらにも引っかかる**（実測で 61 行 / 293 件のずれが出た）。
    // **指示書の字と、指示書が挙げた理由が食い違っている場所なので、理由のほうを採る。**
    //
    // **戦闘は1回も増えない**——勝率表と同じ `BattleResult` から読むだけ。
    //
    //     dotnet run --project BattleSim -c Release 0 compare > docs/balance.md          # 勝率表（不変）
    //     dotnet run --project BattleSim -c Release 0 compare quality > docs/quality.md  # 勝ち方の質
    bool cqQuality = args.Length > 2 && args[2] == "quality";

    // そのまま docs/balance.md になるので、見出しと注意書きもここで吐く。
    // 手で足した文章はリダイレクトのたびに消えるため、文書の体裁ごと生成物にする。
    if (!cqQuality)
    {
        Console.WriteLine("# 勝率表");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 compare > docs/balance.md` の出力。手で編集しない。");
        Console.WriteLine($"代表編成 × 全ステージ、seed 0..{CompareSeeds - 1} の {CompareSeeds} 試行。");
        Console.WriteLine();

        // 列はステージ数から作るので、ステージを足しても勝手に増える。
        // 固定幅で揃えるのはやめた。全角の編成名では桁が合わない（`,-24` は表示幅ではなく文字数を数える）。
        Console.WriteLine("| 編成 |" + string.Concat(EnemyCatalog.Stages.Select((st, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|---|" + string.Concat(EnemyCatalog.Stages.Select(_ => "---:|")));
    }

    var qWins = new int[builds.Length];
    var qSurv = new double[builds.Length];
    var qBlow = new int[builds.Length];      // 生存 >= 4（`run` の `圧勝率` と同じ定義）
    var qPerf = new int[builds.Length];      // 生存 >= 出撃数（**この期の新しい列**）
    var qNarrow = new int[builds.Length];    // 生存 <= 1
    var qParty = new int[builds.Length];

    // 第127期 段0-2 —— **波別の完全勝利率**。
    // 通算だけだと「第三波だけを見た人には 76.1% がその戦の確率に読める」（指示書 §3-1）。
    // **戦闘は1回も増えない**（同じ `BattleResult` から波ごとに落とすだけ）。
    var qWinsW = new int[builds.Length, EnemyCatalog.Stages.Count];
    var qPerfW = new int[builds.Length, EnemyCatalog.Stages.Count];

    // 第129期 段1 —— **無傷勝利 / 実質無傷勝利 / 決着ターンの分布**。
    //
    // **現行の `完全勝利` は緩い。** 判定式は `PlayerSurvivors >= 出撃数` だが、
    // `PlayerSurvivors` は `ctx.LivingMembers(PlayerTeam).Count()` なので**戦闘中に湧いた駒
    // （胞子・餌）も数える**——出撃5枚のうち3枚が落ちて胞子が3体湧いた戦も通る。
    // 分母も「勝った試行」だけなので、負けた試行が1つも効かない。
    //
    // **ここは `BattleResult.PlayerStarterFallen`（出撃した駒だけを個体の同一性で見る）を読む。**
    // **戦闘は1回も増えていない**——上の表とまったく同じ `BattleResult` から落とすだけ。
    var qTrials = new int[builds.Length];                          // 分母（第2〜5波の**全試行**）
    var qClean = new int[builds.Length];                           // 無傷勝利
    var qClean2 = new int[builds.Length];                          // 実質無傷勝利
    var qTrialsW = new int[builds.Length, EnemyCatalog.Stages.Count];
    var qCleanW = new int[builds.Length, EnemyCatalog.Stages.Count];
    var qClean2W = new int[builds.Length, EnemyCatalog.Stages.Count];
    var qPerfDirty = new int[builds.Length];   // 現行の「完全勝利」のうち**出撃駒が欠けている**試行
    var qOver = new int[builds.Length];        // `PlayerSurvivors` > 出撃数（現行の判定式を通せる形）
    var qSummonAlive = new int[builds.Length]; // `PlayerSurvivors` ≠ 出撃数 − 欠けた数（湧いた駒が残った戦）
    var qExcused = new HashSet<string>[builds.Length];             // 免除する駒（`OnDeath` の保持者）
    var qTurnAll = new List<int>[builds.Length];                   // 決着T（第2〜5波・全試行）
    var qTurnW = new List<int>[builds.Length][];

    // === 第140期 —— 行（編成）ごとに並列化した（器具の期・値は1ビットも変えない） ===
    //
    // **この表の集計は、どの行も自分の `[bi]` の枠にしか書かない**——`qWins[bi]` も
    // `qTurnAll[bi]` も行の私物なので、行をまたいだ競合が原理的に起きない。
    // **波（`w`）では割らない**——`qWins[bi] += ...` のように波をまたいで足す量があるので、
    // 同じ行の2つの波を別スレッドに置くと壊れる。**割ってよい軸は行だけ。**
    //
    // 印字は従来どおり直列に `builds` の順で行う（行の文字列を控えて後でまとめて出す）。
    var cqRow = new string[builds.Length];
    Parallel.For(0, builds.Length, bi =>
    {
        (string name, Formation f) = builds[bi];
        qParty[bi] = f.Occupied().Count();
        qExcused[bi] = SurviveScan.ExcusedIds(f);
        qTurnAll[bi] = new List<int>();
        qTurnW[bi] = new List<int>[EnemyCatalog.Stages.Count];
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++) qTurnW[bi][w] = new List<int>();
        var cells = new List<string>();
        for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
        {
            EnemyCatalog.Stage st = EnemyCatalog.Stages[w];
            int wins = 0;
            for (int seed = 0; seed < CompareSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, st.Enemy, seed, verbose: false);

                // --- 第129期 段1 ——「勝ったか」より前に**全試行**を分母に取る ---------------
                // **第一波は実行して除外**（規約 (G10)。ここも同じ分母の切り方にそろえる）。
                if (w > 0)
                {
                    qTrials[bi]++; qTrialsW[bi, w]++;
                    qTurnAll[bi].Add(r.Turns); qTurnW[bi][w].Add(r.Turns);
                    if (r.PlayerSurvivors > qParty[bi]) qOver[bi]++;
                    // **湧いた駒が数えられている証拠は「> 出撃数」ではなく「≠ 出撃数 − 欠けた数」**
                    // ——前者は「欠けた数 ≦ 湧いた数」まで要求するので、1枚欠けて1体湧いた戦を数え落とす。
                    if (r.PlayerSurvivors != qParty[bi] - r.PlayerStarterFallen.Count) qSummonAlive[bi]++;
                    if (r.PlayerWon)
                    {
                        bool clean = r.PlayerStarterFallen.Count == 0;
                        if (clean) { qClean[bi]++; qCleanW[bi, w]++; }
                        // **免除するのは `OnDeath` を上書きする札の保持者だけ**
                        // （`OnAnyDeath` / `OnAllyDeath` は「他人の死を読む側」なので混ぜない
                        //   ——混ぜるとリィカ・ラウ・ハギの死軸が丸ごと免除される）。
                        if (r.PlayerStarterFallen.All(id => qExcused[bi].Contains(id)))
                        { qClean2[bi]++; qClean2W[bi, w]++; }
                        if (r.PlayerSurvivors >= qParty[bi] && !clean) qPerfDirty[bi]++;
                    }
                }

                if (!r.PlayerWon) continue;
                wins++;
                if (w == 0) continue;
                qWins[bi]++;
                qSurv[bi] += r.PlayerSurvivors;
                if (r.PlayerSurvivors >= 4) qBlow[bi]++;
                if (r.PlayerSurvivors >= qParty[bi]) qPerf[bi]++;
                if (r.PlayerSurvivors <= 1) qNarrow[bi]++;
                qWinsW[bi, w]++;
                if (r.PlayerSurvivors >= qParty[bi]) qPerfW[bi, w]++;
            }
            cells.Add($" {wins * 100.0 / CompareSeeds:F1}% |");
        }
        cqRow[bi] = $"| {name} |" + string.Concat(cells);
    });

    if (!cqQuality)
    {
        foreach (string row in cqRow) Console.WriteLine(row);
        return;
    }

    Console.WriteLine("# 勝ち方の質");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 compare quality > docs/quality.md` の出力。手で編集しない。");
    Console.WriteLine($"代表編成 × 全ステージ、seed 0..{CompareSeeds - 1} の {CompareSeeds} 試行"
        + "（**勝率表と同じ戦・同じ帯**。戦闘は1回も増えていない）。");
    Console.WriteLine();
    Console.WriteLine("**`docs/balance.md` とは別のファイルにしてある。** 勝率表に節を足すと、"
        + "`docs/balance.md` を読む自己検査（`curse check` は `| ` で始まり `%` を含む行を位置で、"
        + "`encore check` は行名をキーに 2..6 列目を数値として読む）が両方とも壊れる"
        + "——実測で 61 行 / 293 件のずれが出た。規約 (G8) の必須1 を守るほうを採った。");
    Console.WriteLine();
    Console.WriteLine("## 勝ち方の質（第2〜5波）");
    Console.WriteLine();
    Console.WriteLine("**分母は「第2〜5波で勝った試行」**（規約 (G10)。第一波は全行必勝の教習波なので入れない）。");
    Console.WriteLine("定義は `run` の `RunSolo` から写した——`残存` は勝った試行の平均生存数、");
    Console.WriteLine("`圧勝率` は生存4体以上、`全滅勝ち` は生存1体以下の割合。");
    Console.WriteLine();
    Console.WriteLine("`完全勝利` は**この期に足した列**で、**生存数 ≧ 出撃数**（＝1体も失わずに勝った割合）。");
    Console.WriteLine("`圧勝率`（4体以上）とは別の量である——`PlayerSurvivors` は戦闘中に湧いた駒");
    Console.WriteLine("（胞子・餌）も数えるので、`==` ではなく `>=` で書いてある。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 出撃 | 勝った試行 | 残存 | 圧勝率 | **完全勝利** | 全滅勝ち |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    for (int bi = 0; bi < builds.Length; bi++)
    {
        int n = qWins[bi];
        Console.WriteLine($"| {builds[bi].Name} | {qParty[bi]} | {n} "
            + (n == 0
                ? "| — | — | — | — |"
                : $"| {qSurv[bi] / n:F2} | {qBlow[bi] * 100.0 / n:F1}% "
                  + $"| **{qPerf[bi] * 100.0 / n:F1}%** | {qNarrow[bi] * 100.0 / n:F1}% |"));
    }

    int tw = qWins.Sum();
    if (tw > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"**全 {builds.Length} 行の通算**（勝った試行 {tw}）: "
            + $"残存 **{qSurv.Sum() / tw:F2}** ／ 圧勝率 **{qBlow.Sum() * 100.0 / tw:F1}%** ／ "
            + $"**完全勝利 {qPerf.Sum() * 100.0 / tw:F1}%** ／ 全滅勝ち **{qNarrow.Sum() * 100.0 / tw:F1}%**。");
    }

    // --- 第127期 段0-2 —— 波別の完全勝利率 -------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 波別の完全勝利率（第127期 段0-2）");
    Console.WriteLine();
    Console.WriteLine("上の表の `完全勝利` は**第2〜5波の通算**なので、**1つの波だけを見たときの確率ではない**。");
    Console.WriteLine("分母は**その波で勝った試行**（波ごとに違う）。`—` はその波で1度も勝っていない行。");
    Console.WriteLine("**戦闘は1回も増えていない**——上の表とまったく同じ `BattleResult` から波ごとに落としただけ。");
    Console.WriteLine("**第一波は判定に使わない**（規約 (G10)。全行必勝の教習波）ので列にも出さない。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
        .Select(i => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
        .Select(_ => "---:|")));
    for (int bi = 0; bi < builds.Length; bi++)
    {
        var cells = new List<string>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
            cells.Add(qWinsW[bi, w] == 0
                ? " — |"
                : $" {qPerfW[bi, w] * 100.0 / qWinsW[bi, w]:F1}% |");
        Console.WriteLine($"| {builds[bi].Name} |" + string.Concat(cells));
    }
    Console.WriteLine();
    for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
    {
        int ww = 0, pp = 0;
        for (int bi = 0; bi < builds.Length; bi++) { ww += qWinsW[bi, w]; pp += qPerfW[bi, w]; }
        Console.WriteLine($"- **第{w + 1}波の通算**: 勝った試行 {ww} ／ 完全勝利 "
            + (ww == 0 ? "—" : $"**{pp * 100.0 / ww:F1}%**"));
    }

    // --- 第129期 段1 —— 無傷勝利 / 実質無傷勝利 --------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 無傷勝利と実質無傷勝利（第129期 段1）");
    Console.WriteLine();
    Console.WriteLine("**上の `完全勝利` は緩い。** 判定式は `PlayerSurvivors >= 出撃数` だが、");
    Console.WriteLine("`PlayerSurvivors` は `ctx.LivingMembers(PlayerTeam).Count()` なので"
        + "**戦闘中に湧いた駒（胞子・餌）も数える**。");
    Console.WriteLine("分母も「勝った試行」だけなので、負けた試行が1つも効かない。");
    Console.WriteLine();
    Console.WriteLine("**ここは `BattleResult.PlayerStarterFallen` を読む**"
        + "——出撃した駒だけを、**個体（`UnitState`）の同一性で**見る。");
    Console.WriteLine("**戦闘は1回も増えていない**（上の表とまったく同じ `BattleResult` から落としただけ）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 定義 | 分母 |");
    Console.WriteLine("|---|---|---|");
    Console.WriteLine("| `完全勝利(参考)` | 上の表と同じ値（`PlayerSurvivors >= 出撃数`） | 勝った試行 |");
    Console.WriteLine("| `無傷勝利` | **出撃した駒が1枚も欠けずに勝った**（`PlayerStarterFallen` が空） | **全試行** |");
    Console.WriteLine("| `実質無傷勝利` | 上から**自分の死が起動条件の駒**を免除したもの"
        + "（欠けたのが免除対象だけなら成功とみなす） | **全試行** |");
    Console.WriteLine("| `免除` | その行で免除した駒。`—` は免除対象が1枚もいない行 | — |");
    Console.WriteLine();
    Console.WriteLine($"**免除の判定は実装から引く**——`OnDeath` を上書きする札 "
        + $"**{SurviveScan.DeathTraits.Count} 本**"
        + $"（{SurviveScan.NameList(SurviveScan.DeathTraits)}）の保持者。");
    Console.WriteLine($"**`OnAnyDeath` / `OnAllyDeath`（他人の死を読む側）は混ぜない**"
        + $"——{SurviveScan.OtherDeathTraits.Count} 本"
        + $"（{SurviveScan.NameList(SurviveScan.OtherDeathTraits)}）は**1つも免除に使っていない**。");
    Console.WriteLine("混ぜると墓守リィカ・疫みのラウ・追い打ちのハギを含む死軸が丸ごと免除される。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 出撃 | 全試行 | 完全勝利(参考) | **無傷勝利** | **実質無傷勝利** | 免除 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|---|");
    for (int bi = 0; bi < builds.Length; bi++)
    {
        int n = qTrials[bi], nw = qWins[bi];
        string ex = qExcused[bi].Count == 0
            ? "—"
            : string.Join("・", builds[bi].F.Occupied()
                .Where(o => qExcused[bi].Contains(o.Def.Id)).Select(o => o.Def.Name));
        Console.WriteLine($"| {builds[bi].Name} | {qParty[bi]} | {n} "
            + (nw == 0 ? "| — " : $"| {qPerf[bi] * 100.0 / nw:F1}% ")
            + (n == 0
                ? "| — | — "
                : $"| **{qClean[bi] * 100.0 / n:F1}%** | **{qClean2[bi] * 100.0 / n:F1}%** ")
            + $"| {ex} |");
    }

    int tt = qTrials.Sum();
    if (tt > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"**全 {builds.Length} 行の通算**（全試行 {tt} ／ 勝った試行 {tw}）: "
            + $"完全勝利(参考) **{qPerf.Sum() * 100.0 / Math.Max(1, tw):F1}%** ／ "
            + $"無傷勝利 **{qClean.Sum() * 100.0 / tt:F1}%** ／ "
            + $"実質無傷勝利 **{qClean2.Sum() * 100.0 / tt:F1}%**。");
        Console.WriteLine();
        Console.WriteLine($"- 現行の「完全勝利」に数えられた {qPerf.Sum()} 試行のうち、"
            + $"**出撃した駒が1枚以上欠けているもの {qPerfDirty.Sum()} 件**"
            + $"（{qPerfDirty.Sum() * 100.0 / Math.Max(1, qPerf.Sum()):F1}%）"
            + "——**召喚体が欠けを埋めた戦**。");
        Console.WriteLine($"- 決着時に**湧いた駒が盤上に残っていた**試行 **{qSummonAlive.Sum()} 件**"
            + $"（全試行の {qSummonAlive.Sum() * 100.0 / tt:F1}%。"
            + "`PlayerSurvivors` ≠ 出撃数 − 欠けた数）——**湧いた駒が数えられていることの直接の証拠**。"
            + $"うち `PlayerSurvivors` が出撃数を上回った試行は **{qOver.Sum()} 件**"
            + "（＝現行の判定式を実際に通せる形）。");
        Console.WriteLine($"- 免除対象を1枚以上含む行 **{qExcused.Count(h => h.Count > 0)} / {builds.Length}**。");
    }

    // --- 第129期 段1 —— 波別の無傷勝利率 ---------------------------------------------
    Console.WriteLine();
    Console.WriteLine("### 波別の無傷勝利率（分母はその波の全試行）");
    Console.WriteLine();
    Console.WriteLine("`—` は分母 0 の波。**第一波は判定に使わない**（規約 (G10)）ので列にも出さない。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
        .Select(i => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
        .Select(_ => "---:|")));
    for (int bi = 0; bi < builds.Length; bi++)
    {
        var cells = new List<string>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
            cells.Add(qTrialsW[bi, w] == 0
                ? " — |"
                : $" {qCleanW[bi, w] * 100.0 / qTrialsW[bi, w]:F1}% |");
        Console.WriteLine($"| {builds[bi].Name} |" + string.Concat(cells));
    }

    // --- 第129期 段1 —— 決着ターンの分布 ---------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 決着ターンの分布（第129期 段1）");
    Console.WriteLine();
    Console.WriteLine("**勝率からは「安定した」と「運の幅が広がった」が区別できない**"
        + "（第128期の宿題。8ターン決着と9ターン全滅が同居しうる）。");
    Console.WriteLine("**分母は全試行**（勝ち負けを問わない）。`σ` は標本標準偏差、"
        + "`Q1`/`中`/`Q3` は四分位（線形補間なしの順位法）。");
    Console.WriteLine("**戦闘は1回も増えていない**（`BattleResult.Turns` を読んだだけ）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 第2〜5波 平均 | σ | Q1 | 中 | Q3 |"
        + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
            .Select(i => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|--:|--:|--:|--:|--:|"
        + string.Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count - 1).Select(_ => "---:|")));
    for (int bi = 0; bi < builds.Length; bi++)
    {
        List<int> all = qTurnAll[bi];
        var cells = new List<string>();
        for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
        {
            List<int> v = qTurnW[bi][w];
            cells.Add(v.Count == 0 ? " — |" : $" {SurviveScan.Mean(v):F2}±{SurviveScan.Sd(v):F2} |");
        }
        Console.WriteLine($"| {builds[bi].Name} "
            + (all.Count == 0
                ? "| — | — | — | — | — "
                : $"| {SurviveScan.Mean(all):F2} | {SurviveScan.Sd(all):F2} "
                  + $"| {SurviveScan.Quantile(all, 0.25)} | {SurviveScan.Quantile(all, 0.50)} "
                  + $"| {SurviveScan.Quantile(all, 0.75)} ")
            + "|" + string.Concat(cells));
    }
    return;
}

// engage モード: 会戦（部隊連戦・持ち越しあり）を「列 × 投入部隊数」で測る。
//
// compare は各波を独立した1戦として測るが、会戦は勝った部隊が生存駒の状態
// （HP・最大HPの損耗・蘇生回数・墓守の層-1）を持ち越して次の波と戦う。
// 主表は地点（3波）× 投入部隊数 1〜3。5波1本の順路では全編成が突破 0% に潰れて序列に
// ならず、部隊数を積んだ地点だけが 0〜100% に散る（第3期で切り替え。順路は参考、
// 逆順は第1削り専用へ格下げ）。投入部隊数は同一編成の複製。組み合わせ（別編成×別編成）は
// 多すぎるので測らない。
//
// 却下した案: 独立積（各波の独立勝率の積）・突破分布（0..N 抜きの試行数）・引分の列を残す——
// 会戦の効き目（独立積との差）は第2期で確認済みで役目を終えた。独立勝率そのものは
// docs/balance.md が持ち続けるので、ここに残すと waveCache と順路↔逆順一致検算の維持費だけが残る。
//
//     dotnet run --project BattleSim -c Release 0 engage [絞り込み] > docs/engage.md
if (focusId == "engage")
{
    var all = CompareBuilds();
    const int EngageSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 列は Name で引く（Columns の並び順は GodotApp の EngagementColumn が使うので当てにしない）
    EnemyCatalog.Column spot = EnemyCatalog.Columns.First(c => c.Name == "地点");
    EnemyCatalog.Column route = EnemyCatalog.Columns.First(c => c.Name == "順路");
    EnemyCatalog.Column rev = EnemyCatalog.Columns.First(c => c.Name == "逆順");

    // 1編成 × 1列 × 投入 nSquads 部隊（同一編成の複製）の一括計測。
    // 突破率は PlayerWon で数える（EnemySquadsCleared == N は相打ち全滅を突破に数えてしまう）。
    // 入場戦力・敵側検算は1部隊のときだけ集計する。「第 i 戦の入場 = PlayerEntries[i]、
    // 敵は毎回新規投入」という 1:1 の前提（負けた時点で会戦が終わる）が2部隊以上では崩れるため。
    (int Full, double Cleared, double Attr, int Draws, int[] Dist,
     double[] AliveSum, double[] HpRatioSum, int[] Reached,
     double[] EnemyEroded, int[] EnemyReached)
        Sweep(Formation f, IReadOnlyList<Formation> column, int nSquads)
    {
        int squads = column.Count;
        Formation[] playerColumn = Enumerable.Repeat(f, nSquads).ToArray();

        // HP割合の分母は**編成全体**の定義上総最大HP（不変値）。SquadEntry.DefMaxHpSum を
        // そのまま分母にする案は却下した——あれは「その戦闘に入った駒」だけの合計なので、
        // 死んだ駒が分子と分母から一緒に抜け、% が「部隊の残存戦力」ではなく「生き残りの
        // 健康度」に化ける（1体だけ全快で残った部隊が 100% に見える）。
        int playerDefTotal = f.Occupied().Sum(x => x.Def.MaxHp);
        int[] enemyDefTotal = column.Select(e => e.Occupied().Sum(x => x.Def.MaxHp)).ToArray();

        var dist = new int[squads + 1];
        int full = 0, draws = 0;
        double clearedSum = 0, attrSum = 0;
        var aliveSum = new double[squads];
        var hpRatioSum = new double[squads];
        var reached = new int[squads];
        var enemyEroded = new double[squads];
        var enemyReached = new int[squads];

        for (int seed = 0; seed < EngageSeeds; seed++)
        {
            EngagementResult r = EngagementEngine.Run(playerColumn, column, seed, verbose: false);
            dist[r.EnemySquadsCleared]++;
            if (r.PlayerWon) full++;
            clearedSum += r.EnemySquadsCleared;
            attrSum += r.FirstBattleAttrition;
            draws += r.Draws;

            if (nSquads != 1) continue;
            for (int b = 0; b < r.PlayerEntries.Count && b < squads; b++)
            {
                aliveSum[b] += r.PlayerEntries[b].Alive;
                hpRatioSum[b] += (double)r.PlayerEntries[b].HpSum / playerDefTotal;
                reached[b]++;

                // 敵側の分母はその戦闘の敵部隊の定義上総最大HP（味方1部隊では ei = b）
                enemyEroded[b] += 1.0 - (double)r.EnemyEntries[b].HpSum
                    / enemyDefTotal[r.Pairings[b].EnemySquad];
                enemyReached[b]++;
            }
        }
        return (full, clearedSum, attrSum, draws, dist,
                aliveSum, hpRatioSum, reached, enemyEroded, enemyReached);
    }

    // 主表1節ぶん: 突破率(1/2/3)・期待突破数(1/2/3)・非線形 → 入場戦力（1部隊）→ 敵側検算行。
    void EmitSection(EnemyCatalog.Column col)
    {
        int squads = col.Squads.Count;
        Console.WriteLine("### 突破率と期待突破数（1部隊 / 2部隊 / 3部隊）");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 突破率(1) | 突破率(2) | 突破率(3) | 期待突破数(1) | 期待突破数(2) | 期待突破数(3) | 非線形(2部隊/1部隊×2) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");

        var entryRows = new List<string>();
        var enemyErodedAll = new double[squads];
        var enemyReachedAll = new int[squads];

        // === 第140期 —— 3つの `Sweep` を編成をまたいで1つの平坦な並列パスに前出しした ===
        //
        // `Sweep` は (編成, 部隊列, 投入部隊数) の純関数で、内側で作る配列も呼び出しごとに新しい。
        // `EngagementEngine.Run` は `BattleEngine.Run` を繋ぐだけなので同じく seed 決定的。
        // **各ジョブは自分の添字にしか書かず、印字は従来どおり直列に `targets` の順で行う。**
        var egS = new (int Full, double Cleared, double Attr, int Draws, int[] Dist,
                       double[] AliveSum, double[] HpRatioSum, int[] Reached,
                       double[] EnemyEroded, int[] EnemyReached)[targets.Length, 3];
        Parallel.For(0, targets.Length * 3, k =>
        {
            int t = k / 3, n = k % 3;
            egS[t, n] = Sweep(targets[t].F, col.Squads, n + 1);
        });

        for (int t = 0; t < targets.Length; t++)
        {
            string name = targets[t].Name;
            var s1 = egS[t, 0];
            var s2 = egS[t, 1];
            var s3 = egS[t, 2];

            // 非線形 = 期待突破数(2部隊) ÷ (期待突破数(1部隊)×2)。1.00 超なら第1部隊の削りを
            // 第2部隊が拾えている。期待(1) が 0 の編成は分母が立たないので —（現状は出ない）。
            string nonlinear = s1.Cleared == 0 ? "—" : $"{s2.Cleared / (2 * s1.Cleared):F2}";

            Console.WriteLine($"| {name} | {s1.Full * 100.0 / EngageSeeds:F1}% | {s2.Full * 100.0 / EngageSeeds:F1}% "
                + $"| {s3.Full * 100.0 / EngageSeeds:F1}% | {s1.Cleared / EngageSeeds:F2} | {s2.Cleared / EngageSeeds:F2} "
                + $"| {s3.Cleared / EngageSeeds:F2} | {nonlinear} |");
            Console.Out.Flush();

            entryRows.Add($"| {name} |" + string.Concat(Enumerable.Range(0, squads).Select(b =>
                s1.Reached[b] == 0
                    ? $" — (0/{EngageSeeds}) |"
                    : $" {s1.AliveSum[b] / s1.Reached[b]:F1}体 {s1.HpRatioSum[b] * 100 / s1.Reached[b]:F0}%"
                      + $" ({s1.Reached[b]}/{EngageSeeds}) |")));
            for (int b = 0; b < squads; b++)
            {
                enemyErodedAll[b] += s1.EnemyEroded[b];
                enemyReachedAll[b] += s1.EnemyReached[b];
            }
        }

        Console.WriteLine();
        Console.WriteLine("### 入場戦力（味方・1部隊）");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(0, squads).Select(b => $" 第{b + 1}戦 |")));
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, squads).Select(_ => "---|")));
        foreach (string row in entryRows) Console.WriteLine(row);
        Console.WriteLine();
        Console.WriteLine("持ち越された敵部隊が削れていた割合の平均（全編成・全試行）: "
            + string.Join(" / ", Enumerable.Range(0, squads).Select(b => enemyReachedAll[b] == 0
                ? $"第{b + 1}戦 —"
                : $"第{b + 1}戦 {enemyErodedAll[b] * 100 / enemyReachedAll[b]:F0}%"))
            + "（味方1部隊では敵は毎回新規投入なので全戦 0%＝入場HP 100% のはず。ずれていたら実装がおかしい）");
    }

    Console.WriteLine("# 会戦");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 engage > docs/engage.md` の出力。手で編集しない。");
    Console.WriteLine($"各編成を3本の部隊列にぶつけ、それぞれ seed 0..{EngageSeeds - 1} の {EngageSeeds} 試行。");
    Console.WriteLine("投入部隊数 1〜3 は同一編成の複製（組み合わせは測らない）。");
    Console.WriteLine();
    Console.WriteLine("勝った部隊は生存駒の HP・最大HPの損耗・蘇生回数・墓守の層(-1) を持ち越して次の波と戦う。");
    Console.WriteLine("状態異常（毒・燃焼・痺れ・標的・破片）と攻撃力の一時変動は波の境界で消える。");
    Console.WriteLine();
    Console.WriteLine("部隊列は3本。敵の中身はどれも既存5波のままで、並びと長さだけが違う。");
    Console.WriteLine();
    foreach (EnemyCatalog.Column c in EnemyCatalog.Columns)
        Console.WriteLine($"- **{c.Name}**（{c.Squads.Count}部隊） — {c.Note}");
    Console.WriteLine();
    Console.WriteLine("### 表の読み方");
    Console.WriteLine();
    Console.WriteLine("- `突破率(n)` は n 部隊投入で列の全部隊を抜いた試行の割合。`期待突破数(n)` は抜いた部隊数の平均。");
    Console.WriteLine("- `非線形` は 期待突破数(2部隊) ÷ (期待突破数(1部隊)×2)。**1.00 を超えるなら第1部隊の削りを");
    Console.WriteLine("  第2部隊が拾えている**＝複数部隊制が噛み合っている証拠。期待突破数(1部隊) が列の長さに");
    Console.WriteLine("  近い編成は ×2 が列の長さを超えるので、頭打ちで 1.00 を下回る（弱いのではなく測り切れないだけ）。");
    Console.WriteLine("- `入場戦力` は各部隊戦に入る時点の味方の生存数と HP（**編成全体の定義上の**総最大HPに");
    Console.WriteLine("  対する割合。死んだ駒の枠も分母に残るので、% は部隊の残存戦力を表す。生き残りの健康度");
    Console.WriteLine("  ではない）。到達しなかった試行は分母から外し、到達率を併記する。1部隊投入の走行から集計する。");
    Console.WriteLine("- `第1削り` は最初の Battle で敵の先頭部隊の総 MaxHp を削った割合。**勝てなくても削れる編成**");
    Console.WriteLine("  （特攻隊）はここに出る。逆順の節にだけ載せる（順路・地点では第一波が全編成必勝で");
    Console.WriteLine("  一律 100% になり無情報）。");

    Console.WriteLine();
    Console.WriteLine($"## 地点（{spot.Squads.Count}部隊） — 標準の測定系");
    Console.WriteLine();
    Console.WriteLine("マップ上の1地点は敵1〜3部隊（design/concept_wave_engagement.md §7）。5波1本の順路では");
    Console.WriteLine("全編成が突破 0% に潰れるのに対し、この列は部隊数を積むと突破率が 0〜100% に散る——");
    Console.WriteLine("現時点で唯一、編成の序列として機能する分布なので主表とする。");
    Console.WriteLine();
    EmitSection(spot);

    Console.WriteLine();
    Console.WriteLine($"## 順路（{route.Squads.Count}部隊） — 参考。1地点としては長すぎる");
    Console.WriteLine();
    Console.WriteLine("第2期まで主表だった5波1本の列。全編成が突破 0%・期待突破数 1.00〜2.00 に潰れて序列として");
    Console.WriteLine("機能せず、コンセプト上も1地点は敵1〜3部隊なので参考へ降格した。第2戦→第3戦で駒も HP も");
    Console.WriteLine("一気に落ちる消耗（第二波が代金）の位置は、この列の入場戦力で読む。");
    Console.WriteLine();
    EmitSection(route);

    Console.WriteLine();
    Console.WriteLine("## 逆順 — 第1削り専用");
    Console.WriteLine();
    Console.WriteLine("強い波が先頭の列。**突破数の列は載せない**——逆順は全編成が 0 か 1 抜きで初戦＝第五波の");
    Console.WriteLine("勝敗しか測らず、突破数は docs/balance.md の第5波（独立勝率）の測り直しにしかならない。");
    Console.WriteLine("勝てない編成が先頭の強敵をどれだけ削るか（特攻隊の価値）だけをこの列で読む。");
    Console.WriteLine();
    Console.WriteLine("### 第1削り");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 第1削り |");
    Console.WriteLine("|---|--:|");
    foreach (var (name, f) in targets)
    {
        var m1 = Sweep(f, rev.Squads, 1);
        Console.WriteLine($"| {name} | {m1.Attr * 100 / EngageSeeds:F0}% |");
        Console.Out.Flush();
    }
    return;
}

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

// gradient モード: 勾配のある部隊列の候補を測る診断（第5期 Phase N）。
// 3波1列（地点サイズ）の各位置に 2〜3 案の候補波を組み、cost と同じ物差し
// （勝った試行の残HP% → 代金 = 100% − 残HP%）で測る。位置ごとの狙い:
//   第1波 = 安い(20〜30%)・範囲攻撃の編成に安い / 第2波 = 中(35〜50%)・偏らせない /
//   第3波 = 高い(50〜70%)・単体火力の編成に安い
// 3波の合計代金は 110〜150% を狙う（100% 以下だと1部隊で全抜きできて部隊数の判断が消え、
// 200% 超だと2部隊でも抜けず第2期の再来になる。第5期 §3-1）。
//
// 候補波はこのモードのローカル変数で組む。EnemyCatalog.Columns / Stages には足さない
// （採用はポン氏の判断待ち。handoff の接尾列と同じ「公開する列の集合を診断で汚さない」判断）。
// 診断用で docs/ には置かない（seats / handoff / cost と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 gradient [絞り込み]
if (focusId == "gradient")
{
    var all = CompareBuilds();
    const int GradSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // --- 候補波（位置ごとに 2〜3 案。第5期 §3-2 の制約: 1波6体まで / 貫き1枚 / 全体1枚 /
    //     断罪は入れない）。新 def は 農兵(levy) と 従軍司祭長(chaplain) の2つだけで、
    //     残りは既存 def の再利用（数値は触っていない）。 ---

    // 第1波: 農兵(30/8)の頭数だけで作る。1a/1b は体数の差。1c は「群れに斧1本」——
    // 敵側に薙ぎが1枚入ると味方の受け方（庇う・標的が効かない攻撃）が変わるので、
    // 範囲/単体の割れが「体数」由来か「敵の攻撃パターン」由来かを切り分ける対照。
    var w1 = new (string Name, Formation Enemy)[]
    {
        ("1a 農兵5", Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("1b 農兵5", Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("1c 農兵5+斧", Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Axeman, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
    };

    // 第2波: 既存 def の再利用だけで第一波と第二波の中間を作る（中間に新造の個性は要らない）。
    // 2a→2c の順に重くなる。2c の狙撃手は貫き1枚の上限内。
    var w2 = new (string Name, Formation Enemy)[]
    {
        ("2a 新兵3+斧", Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Recruit, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman)),
        ("2b 騎士混成", Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman)),
        ("2c 騎士2+狙撃", Formation.Build(front1: EnemyCatalog.Knight, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Archer)),
    };

    // 第3波: 少数高HP の精鋭。聖騎士長（第六波以降の素材・処刑持ち）と重装兵が素体。
    // 回復役の有無で性格が大きく変わるはずなので、司祭長入り(3b)となし(3a)の両方を測る。
    // 3c は2体の下限案（体数が減るほど範囲攻撃の意味が消え、単体火力有利が立つはず）。
    var w3 = new (string Name, Formation Enemy)[]
    {
        ("3a 精鋭3", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Warden)),
        ("3b 精鋭+司祭長", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Chaplain)),
        ("3c 精鋭2", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion)),
    };

    var cand = w1.Concat(w2).Concat(w3).ToList();

    Console.WriteLine($"# 勾配列の候補診断（seed 0..{GradSeeds - 1} の {GradSeeds} 試行）");
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

    var cells = EmitCostTables(targets, cand, GradSeeds);

    // --- 範囲持ち / 単体のみ の割れ（第1波候補の成功条件。第5期 §3-3） ---
    // 判定は HasAoe（ファイル末尾の共有ヘルパ）。個別の食い違いは代金の表の側で読む。

    Console.WriteLine();
    Console.WriteLine("### 範囲持ちと単体のみの代金（編成の Def.Pattern に薙ぎ/全体を含むか）");
    Console.WriteLine();
    Console.WriteLine("| 候補 | 範囲持ちの代金平均 | 単体のみの代金平均 | 差（単体 − 範囲） |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int w = 0; w < cand.Count; w++)
    {
        var groups = Enumerable.Range(0, targets.Length)
            .Where(t => cells[t, w].Wins > 0)
            .GroupBy(t => HasAoe(targets[t].F))
            .ToDictionary(g => g.Key, g => g.Average(t => (1 - cells[t, w].AvgHpPct) * 100));
        double aoe = groups.TryGetValue(true, out double a) ? a : double.NaN;
        double single = groups.TryGetValue(false, out double b) ? b : double.NaN;
        Console.WriteLine($"| {cand[w].Name} | {aoe:F1}% | {single:F1}% | {single - aoe:+0.0;-0.0}pt |");
    }
    int nAoe = targets.Count(t => HasAoe(t.F));
    Console.WriteLine();
    Console.WriteLine($"範囲持ち {nAoe} 編成 / 単体のみ {targets.Length - nAoe} 編成"
        + "（代金は各群とも勝率 > 0% の編成だけで平均）");

    // --- 組み合わせ列（全27通り × 投入部隊数 1〜2） ---
    // 合計代金は各候補波の代金平均の単純和（単独戦の値）。連戦の実際の消耗は期待突破数で見る。
    // 期待突破数・突破率は全編成の平均。非線形 = 期待(2) ÷ (期待(1)×2)（engage と同じ定義）。
    double[] candMean = Enumerable.Range(0, cand.Count).Select(w =>
        Enumerable.Range(0, targets.Length).Where(t => cells[t, w].Wins > 0)
            .Average(t => (1 - cells[t, w].AvgHpPct) * 100)).ToArray();

    Console.WriteLine();
    Console.WriteLine("### 組み合わせ列（第1波×第2波×第3波 の27通り × 投入部隊数1〜2・全編成平均）");
    Console.WriteLine();
    Console.WriteLine("| 列 | 合計代金 | 期待突破数(1) | 突破率(1) | 期待突破数(2) | 突破率(2) | 非線形 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    for (int i = 0; i < w1.Length; i++)
        for (int j = 0; j < w2.Length; j++)
            for (int k = 0; k < w3.Length; k++)
            {
                var column = new[] { w1[i].Enemy, w2[j].Enemy, w3[k].Enemy };
                long trials = (long)targets.Length * GradSeeds;
                double e1 = 0, e2 = 0;
                int full1 = 0, full2 = 0;
                foreach (var (_, f) in targets)
                {
                    Formation[] one = { f };
                    Formation[] two = { f, f };
                    for (int seed = 0; seed < GradSeeds; seed++)
                    {
                        EngagementResult r1 = EngagementEngine.Run(one, column, seed, verbose: false);
                        e1 += r1.EnemySquadsCleared;
                        if (r1.PlayerWon) full1++;
                        EngagementResult r2 = EngagementEngine.Run(two, column, seed, verbose: false);
                        e2 += r2.EnemySquadsCleared;
                        if (r2.PlayerWon) full2++;
                    }
                }
                double total = candMean[i] + candMean[3 + j] + candMean[6 + k];
                double exp1 = e1 / trials, exp2 = e2 / trials;
                Console.WriteLine($"| {w1[i].Name[..2]}/{w2[j].Name[..2]}/{w3[k].Name[..2]} | {total:F0}% "
                    + $"| {exp1:F2} | {full1 * 100.0 / trials:F1}% | {exp2:F2} | {full2 * 100.0 / trials:F1}% "
                    + $"| {(exp1 == 0 ? "—" : $"{exp2 / (2 * exp1):F2}")} |");
                Console.Out.Flush();
            }

    // --- 推奨列の編成別内訳 ---
    // 組み合わせ表を読んでから spotlight を差し替えて再実行する二段運用（診断モードなので
    // 出力は使うときにその場で吐く。docs/ に置かない）。
    // 1本目は推奨列（1b/2b/3a: 全3波が §3-1 の狙い帯に入り、波間の勾配 27→41→61 が
    // いちばん単調に開く）。2本目は第3波を司祭長入りに替えた対照（回復役で編成別の
    // 序列がどう動くかを見る）。
    var spotlight = new (string Name, Formation[] Column)[]
    {
        ("1b/2b/3a（推奨）", new[] { w1[1].Enemy, w2[1].Enemy, w3[0].Enemy }),
        ("1b/2b/3b（司祭長対照）", new[] { w1[1].Enemy, w2[1].Enemy, w3[1].Enemy }),
    };
    foreach (var (colName, column) in spotlight)
    {
        Console.WriteLine();
        Console.WriteLine($"### 編成別内訳: {colName}");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 期待突破数(1) | 突破率(1) | 期待突破数(2) | 突破率(2) |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var (name, f) in targets)
        {
            Formation[] one = { f };
            Formation[] two = { f, f };
            double e1 = 0, e2 = 0;
            int full1 = 0, full2 = 0;
            for (int seed = 0; seed < GradSeeds; seed++)
            {
                EngagementResult r1 = EngagementEngine.Run(one, column, seed, verbose: false);
                e1 += r1.EnemySquadsCleared;
                if (r1.PlayerWon) full1++;
                EngagementResult r2 = EngagementEngine.Run(two, column, seed, verbose: false);
                e2 += r2.EnemySquadsCleared;
                if (r2.PlayerWon) full2++;
            }
            Console.WriteLine($"| {name} | {e1 / GradSeeds:F2} | {full1 * 100.0 / GradSeeds:F1}% "
                + $"| {e2 / GradSeeds:F2} | {full2 * 100.0 / GradSeeds:F1}% |");
            Console.Out.Flush();
        }
    }
    return;
}

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

// flip モード: 高い波の「代金の向きの反転」を測る診断（第7期 Phase R）。
// 第6期で作れたのは「範囲に安い波」だけで、列全体が範囲編成に一様に傾いただけなら
// それは難度が下がったのと同じ——配分判断は生まれない。判断が立つのは**同じ列の中で
// 符号が反転するとき**だけなので、第3波の位置に「範囲に高くつく波」を作れるかを測る。
//
// 鏡像の原理（第6期の結論「向きの正体は1手で何体落ちるか」の裏返し）:
//   体数を減らす（範囲が撒く先が無い） / 個体HPを一撃圏の外に置く（撒いても撃破に
//   変換できない） / 1体あたりの攻撃を上げる（単体火力で1体落とすと減る量が大きい）
//
// 物差しは aim と完全に同じ（勝った試行の残HP% → 代金 = 100% − 残HP%、区分は HasAoe）。
// **符号を逆に読むだけで、指標の定義は変えない**——第6期の +8.7pt と直接比べられることが
// この表の意味なので、ここを触ったら比較が壊れる。
//
// **成功条件: `単体 − 範囲` が −8pt 以下**（＝範囲のほうが 8pt 以上高くつく）。
// 第1波の +8.7pt と対称の大きさ。帯は代金平均 50〜70%（第5期 §3-1 の第3波の狙い）で、
// 帯と反転が両立しない場合は反転を優先する（帯は体数で後から戻せるが、向きは戻せない）。
//
// 候補波は gradient / aim と同じくこのモードのローカル変数で組む（Stages / Columns には
// 足さない）。診断用で docs/ には置かない。
//
//     dotnet run --project BattleSim -c Release 0 flip [絞り込み]
if (focusId == "flip")
{
    var all = CompareBuilds();
    const int FlipSeeds = 200;   // gradient / aim と同じ。対照（3a/3b/3c）の検算が成立する条件

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // --- 候補波（第3波の位置だけ。第1波・第2波は第6期・第5期のまま触らない） ---
    // 制約は第5期・第6期と同じ（1波6体まで / 貫き1枚まで / 全体1枚まで / AttackPattern を
    // 増やさない / 新候補に範囲持ちの敵を入れない）。配置は前1→前3→中央→後1→後3 の順。
    //
    // 対照3案（3a/3b/3c）は gradient の w3 をそのまま写したもの。代金が第5期の
    // 61.0% / 52.5% / 44.3% と一致しなければ測り方が変わった証拠なので、先へ進まずに止まる。
    //
    // R0〜R6 は **攻16 固定・体数 × 個体HP の格子**。R0（鎖帷子32）は第6期の H2c と同じ素体で、
    // HP 軸 32 → 60 → 90 を1回の実行で繋ぐための橋（体数を4に揃えてある）。
    var cand = new (string Name, Formation Enemy)[]
    {
        ("3a 精鋭3（対照）", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Warden)),
        ("3b 精鋭+司祭長（対照）", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Chaplain)),
        ("3c 精鋭2（対照）", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion)),

        // HP 軸の起点。第6期 H2c（鎖5）の体数を4にしたもの。ここは +5.8pt 側（範囲に安い）
        // のはずで、そこから HP を厚くして符号が返るかを見る。
        ("R0 鎖4(32)", Formation.Build(front1: EnemyCatalog.ZealotMail, front3: EnemyCatalog.ZealotMail, center: EnemyCatalog.ZealotMail, back1: EnemyCatalog.ZealotMail)),

        // 個体HP 60（上位1割の打点 51.1 でも1発では落ちない最初の刻み）× 体数 4 / 3 / 2。
        ("R1 板金4(60)", Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate, center: EnemyCatalog.ZealotPlate, back1: EnemyCatalog.ZealotPlate)),
        ("R2 板金3(60)", Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate, center: EnemyCatalog.ZealotPlate)),
        ("R3 板金2(60)", Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate)),

        // 個体HP 90（上位1割の2発圏。最大打点 90.1 でようやく1発）× 体数 4 / 3 / 2。
        ("R4 重甲4(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat, back1: EnemyCatalog.ZealotGreat)),
        ("R5 重甲3(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat)),
        ("R6 重甲2(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat)),

        // 体数の上側（初回の格子を測ってから足した点）。初回は「体数を減らす」という
        // 鏡像の原理に従って 2〜4 体を測ったが、**結果は逆**だった——HP90 で
        // 2体 +2.8pt / 3体 +2.6pt / 4体 -2.5pt。体数が多いほど反転側へ動く。
        // 理屈は読める——体数が少ないと範囲攻撃は単体と同じになるだけで損をしない。
        // 損をするのは「倒しきれない相手がたくさん並んでいる」とき。その向きに伸ばして頑張る。
        ("R8 重甲5(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat, back1: EnemyCatalog.ZealotGreat, back3: EnemyCatalog.ZealotGreat)),
        ("R9 重甲5(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat, back1: EnemyCatalog.ZealotGreat, back3: EnemyCatalog.ZealotGreat)),
        // 体数の上側を HP60 側でも取る（体数と個体HP のどちらが効いているかの分離）。
        ("R10 板金5(60)", Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate, center: EnemyCatalog.ZealotPlate, back1: EnemyCatalog.ZealotPlate, back3: EnemyCatalog.ZealotPlate)),

        // 3軸（体数↑・個体HP↑・1体あたり攻撃↓）を全部重ねた点。攻撃だけを 16 → 10 に
        // 下げてある——R9（重甲5体・攻16）が 10編成を勝率 0% に落として打ち切りバイアスを
        // 拾ったので、同じ盤面を全編成が勝ち切れる高さに戻すための一手。
        ("R11 従卒5(90/攻10)", Formation.Build(front1: EnemyCatalog.ZealotSquire, front3: EnemyCatalog.ZealotSquire, center: EnemyCatalog.ZealotSquire, back1: EnemyCatalog.ZealotSquire, back3: EnemyCatalog.ZealotSquire)),
        ("R12 従卒5(90/攻10)", Formation.Build(front1: EnemyCatalog.ZealotSquire, front3: EnemyCatalog.ZealotSquire, center: EnemyCatalog.ZealotSquire, back1: EnemyCatalog.ZealotSquire, back3: EnemyCatalog.ZealotSquire)),

        // 処刑ありなしの対照（第7期 §2-4）。3a と数値は完全に同じで、聖騎士長の特性だけを
        // 落としてある。差が出れば「反転の一部は処刑が作っている」ことになる。
        ("R7 精鋭3・処刑なし（対照）", Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.ChampionPlain, center: EnemyCatalog.Warden)),
    };

    Console.WriteLine($"# 高い波の候補診断・代金の向きの反転（seed 0..{FlipSeeds - 1} の {FlipSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("第3波の位置の候補を、cost / gradient / aim と同じ物差し（勝った試行の残HP% →");
    Console.WriteLine("**代金 = 100% − 残HP%**）で測ったもの。**指標の定義は aim と同一で、符号を逆に読む。**");
    Console.WriteLine();
    Console.WriteLine("**成功条件: `単体 − 範囲` が −8pt 以下**（範囲のほうが 8pt 以上高くつく）。");
    Console.WriteLine("第6期の第1波は H2a +8.7pt / H2b +8.4pt / H2d +7.4pt（範囲に安い）。その鏡像。");
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

    var cells = EmitCostTables(targets, cand, FlipSeeds);

    // --- 候補まとめ（第7期 §2-3 の表。aim の表に体数の列を足しただけ） ---
    Console.WriteLine();
    Console.WriteLine("### 候補まとめ（体数 × 個体HP の2軸と、向き・ターン数）");
    Console.WriteLine();
    Console.WriteLine("| 候補 | 体数 | 総HP | 総攻/T | 代金平均 | 代金SD | 単体−範囲 | 平均ターン数 | 勝率0%の編成数 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    var split = new double[cand.Length];
    var meanCost = new double[cand.Length];
    var zeroWin = new int[cand.Length];
    for (int w = 0; w < cand.Length; w++)
    {
        int n = cand[w].Enemy.Occupied().Count();
        int hp = cand[w].Enemy.Occupied().Sum(x => x.Def.MaxHp);
        int atk = cand[w].Enemy.Occupied().Sum(x => x.Def.Attack);

        var live = Enumerable.Range(0, targets.Length).Where(t => cells[t, w].Wins > 0).ToArray();
        double Cost(int t) => (1 - cells[t, w].AvgHpPct) * 100;
        if (live.Length == 0)
        {
            zeroWin[w] = targets.Length;
            split[w] = double.NaN;
            Console.WriteLine($"| {cand[w].Name} | {n} | {hp} | {atk} | — | — | — | — | {targets.Length} |");
            continue;
        }
        double mean = live.Average(Cost);
        double sd = Math.Sqrt(live.Average(t => (Cost(t) - mean) * (Cost(t) - mean)));
        double turns = live.Average(t => cells[t, w].AvgTurns);

        var groups = live.GroupBy(t => HasAoe(targets[t].F))
            .ToDictionary(g => g.Key, g => g.Average(Cost));
        double aoe = groups.TryGetValue(true, out double a) ? a : double.NaN;
        double single = groups.TryGetValue(false, out double b) ? b : double.NaN;
        split[w] = single - aoe;
        meanCost[w] = mean;
        zeroWin[w] = targets.Length - live.Length;

        Console.WriteLine($"| {cand[w].Name} | {n} | {hp} | {atk} | {mean:F1}% | {sd:F1}pt "
            + $"| {split[w]:+0.0;-0.0}pt | {turns:F1} | {zeroWin[w]} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    int nAoeF = targets.Count(t => HasAoe(t.F));
    Console.WriteLine($"範囲持ち {nAoeF} 編成 / 単体のみ {targets.Length - nAoeF} 編成"
        + "（代金は各群とも勝率 > 0% の編成だけで平均）");
    Console.WriteLine();

    const double FlipThreshold = -8.0;
    var flipped = Enumerable.Range(0, cand.Length)
        .Where(w => !double.IsNaN(split[w]) && split[w] <= FlipThreshold).ToArray();
    Console.WriteLine(flipped.Length == 0
        ? $"**判定: 反転は取れていない。** `単体−範囲` が {FlipThreshold:F0}pt 以下の候補は無い。"
        : $"**判定: 反転が取れた候補がある** — {string.Join(" / ", flipped.Select(w => cand[w].Name))}");
    Console.WriteLine();

    // 帯（代金平均 50〜70%）と反転の両立。両立しない場合は反転を優先し、その旨を報告する
    // （第7期 §2-3。帯は体数で後から戻せるが、向きは戻せない）。
    foreach (int w in flipped)
        Console.WriteLine($"- {cand[w].Name}: 代金平均 {meanCost[w]:F1}%"
            + (meanCost[w] is >= 50 and <= 70 ? "（狙い帯 50〜70% に入っている）" : "（**狙い帯 50〜70% から外れている**）"));

    // 勝率 0% の編成が半数を超えたら、それは「高い波」ではなく「勝てない波」。
    // 第7期 §5-7 の停止条件なので、表の中で目に付くように出す。
    var unwinnable = Enumerable.Range(0, cand.Length).Where(w => zeroWin[w] * 2 > targets.Length).ToArray();
    if (unwinnable.Length > 0)
    {
        Console.WriteLine();
        Console.WriteLine("> **警告: 勝率 0% の編成が半数を超えた候補がある** — "
            + string.Join(" / ", unwinnable.Select(w => $"{cand[w].Name}（{zeroWin[w]}/{targets.Length}）"))
            + "。これは「高い波」ではなく「勝てない波」で、位置の役割から見直しが要る（第7期 §5-7）。");
    }

    // --- 範囲持ち枚数での単調性（第7期 §2-4。第6期と同じ表を逆向きに読む） ---
    Console.WriteLine();
    Console.WriteLine("### 範囲持ち枚数での代金（反転が取れた候補のみ。逆向きの単調性）");
    Console.WriteLine();
    if (flipped.Length == 0)
    {
        Console.WriteLine("反転が取れた候補が無いので省略（枚数で割っても二値区分より細かい情報は出ない）。");
    }
    else
    {
        Console.WriteLine("第6期は枚数が増えるほど代金が**下がる**ことを確認した。反転側では**上がる**はず。");
        Console.WriteLine();
        Console.WriteLine("| 候補 | 0枚 | 1枚 | 2枚以上 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (int w in flipped)
        {
            var by = Enumerable.Range(0, targets.Length).Where(t => cells[t, w].Wins > 0)
                .GroupBy(t => Math.Min(2, AoeCount(targets[t].F)))
                .ToDictionary(g => g.Key,
                    g => (Cost: g.Average(t => (1 - cells[t, w].AvgHpPct) * 100), N: g.Count()));
            string Cell(int k) => by.TryGetValue(k, out var v) ? $"{v.Cost:F1}%（{v.N}編成）" : "—";
            Console.WriteLine($"| {cand[w].Name} | {Cell(0)} | {Cell(1)} | {Cell(2)} |");
        }
    }

    // --- 処刑ありなしの対照（第7期 §2-4） ---
    Console.WriteLine();
    Console.WriteLine("### 処刑の有無（3a と R7 は数値が完全に同じで、特性だけが違う）");
    Console.WriteLine();
    Console.WriteLine("| 候補 | 代金平均 | 単体−範囲 | 平均ターン数 |");
    Console.WriteLine("|---|--:|--:|--:|");
    foreach (int w in new[] { 0, cand.Length - 1 })
    {
        var live = Enumerable.Range(0, targets.Length).Where(t => cells[t, w].Wins > 0).ToArray();
        double turns = live.Length == 0 ? 0 : live.Average(t => cells[t, w].AvgTurns);
        Console.WriteLine($"| {cand[w].Name} | {meanCost[w]:F1}% | {split[w]:+0.0;-0.0}pt | {turns:F1} |");
    }
    return;
}

// bridge モード: 代金の向きは編成の序列を動かすか（第7期 Phase S）。
//
// 代金に向きが出ても、突破数の序列が動くとは限らない。**第4期の逆順列がまさにそれ**で、
// 指標は大きく動いたのに実体は「第五波の独立勝率の測り直し」だった。同じ轍を踏まないために、
// 向きのある波を実際に列として組み、engage と同じ物差し（期待突破数・突破率、1部隊・2部隊）で
// 全編成を測って**順位が入れ替わるか**を見る。
//
// | 列 | 第1波 | 第2波 | 第3波 | 性格 |
// |---|---|---|---|---|
// | 平坦列 | 1b 農兵5（+3.1pt） | 2b 騎士混成 | 3a 精鋭3（-0.5pt） | 向きがほぼ無い（第5期の推奨列） |
// | 反転列 | H2a 裸5（+8.7pt） | 2b 騎士混成 | R11 従卒5（-8.4pt） | 列の中で符号が反転する |
//
// 第2波は両列で同じもの（2b 固定）にして交絡を減らす。反転列は2本測る——主表は指示書
// どおり H2a（+8.7pt・第6期の最大値）だが、H2a の代金は 29.8% で 1b の 27.3% より
// 2.5pt 高い。列全体の難度がずれると順位相関に床/天井の効きが混ざるので、代金が 1b と
// ほぼ同額（27.2%）で向きだけが違う H2d（+7.4pt）を**難度をそろえた対照**として並べる。
//
// 順位相関はスピアマン（同順位は平均順位で処理し、順位列のピアソン相関を取る）。
// 判定（第7期 §3-3）: 0.7 未満かつ範囲持ちが反転列で上がっていれば橋が架かった。
// 0.9 以上なら「代金には出たが結果には出ていない」＝第4期の逆順列と同じ形。
//
// 診断用で docs/ には置かない（seats / handoff / cost / gradient / aim / flip と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 bridge [絞り込み]
if (focusId == "bridge")
{
    var all = CompareBuilds();
    const int BridgeSeeds = 200;   // gradient / aim / flip と同じ。平坦列の検算が成立する条件

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 波はすべて gradient / aim / flip のローカル定義をそのまま写したもの（同じ並び・同じ配置）。
    // 値が動いたら測り方が変わった証拠なので、平坦列の期待突破数(1) = 2.05 / 突破率(1) = 6.4%
    // （第5期の推奨列）と突き合わせて止まる。
    Formation W1Levy5 = Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy);
    Formation W1ZealotBare5 = Formation.Build(front1: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare, center: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare, back3: EnemyCatalog.ZealotBare);
    Formation W1ZealotLeather4 = Formation.Build(front1: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather, center: EnemyCatalog.ZealotLeather, back1: EnemyCatalog.ZealotLeather);
    Formation W2Mixed = Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman);
    Formation W3Elite3 = Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Warden);
    Formation W3Squire5 = Formation.Build(front1: EnemyCatalog.ZealotSquire, front3: EnemyCatalog.ZealotSquire, center: EnemyCatalog.ZealotSquire, back1: EnemyCatalog.ZealotSquire, back3: EnemyCatalog.ZealotSquire);
    // 第8期 Phase V。第3波の代金だけを振る（体数・個体HP は R11 と同じで攻撃だけが違う）。
    Formation W3Porter5 = Formation.Build(front1: EnemyCatalog.ZealotPorter, front3: EnemyCatalog.ZealotPorter, center: EnemyCatalog.ZealotPorter, back1: EnemyCatalog.ZealotPorter, back3: EnemyCatalog.ZealotPorter);
    Formation W3Pilgrim5 = Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front3: EnemyCatalog.ZealotPilgrim, center: EnemyCatalog.ZealotPilgrim, back1: EnemyCatalog.ZealotPilgrim, back3: EnemyCatalog.ZealotPilgrim);

    var columns = new (string Name, string Note, Formation[] Squads)[]
    {
        ("平坦列", "1b 農兵5(+3.1pt) / 2b 騎士混成 / 3a 精鋭3(-0.5pt)。第5期の推奨列",
            new[] { W1Levy5, W2Mixed, W3Elite3 }),
        ("反転列", "H2a 裸5(+8.7pt) / 2b 騎士混成 / R11 従卒5(-8.4pt)。列の中で符号が反転する",
            new[] { W1ZealotBare5, W2Mixed, W3Squire5 }),
        ("反転列(難度そろえ)", "H2d 革4(+7.4pt・代金 27.2% は 1b とほぼ同額) / 2b / R11 従卒5",
            new[] { W1ZealotLeather4, W2Mixed, W3Squire5 }),
        // 第8期 Phase V。反転列と第1波・第2波は同じで、第3波の代金だけが違う3点。
        ("反転列(中)", "H2a 裸5 / 2b / 荷駄5(90/攻7・代金 54%)。合計を下げて境目に近づける",
            new[] { W1ZealotBare5, W2Mixed, W3Porter5 }),
        ("反転列(低)", "H2a 裸5 / 2b / 巡礼5(90/攻4・代金 42%)。**向きは -2.2pt しか無い**",
            new[] { W1ZealotBare5, W2Mixed, W3Pilgrim5 }),
        // 低の群差の対照。第1波だけを向きの無い 1b に戻した以外は反転列(低)と同じ。
        // 反転列(低) で範囲持ちの Δ が開いたとき、それが「列の向き（第1波 +8.7pt）が
        // 境目で結果に出た」のか「安い波は範囲持ちに有利なだけ」なのかを分ける。
        ("平坦列(低)", "1b 農兵5(+3.1pt) / 2b / 巡礼5。反転列(低) の群差の対照",
            new[] { W1Levy5, W2Mixed, W3Pilgrim5 }),
        // 第10期 Phase AB-0。上の6列には全体持ちも貫き持ちも1体もいないので、
        // チャージ化の前後でこの表が1つも動かない。測定台の骨格を保ったまま
        // 貫き1枚・全体1枚だけ入れ替えた列を足す（中身は ChargeBench() に寄せてある）。
        // この列の 単体−範囲 は -1.9pt しかないので**向きの判定には使えない**。
        // 測るのは時間軸（チャージ化の前後）で、第6〜8期の軸ではない。
        ("チャージ台", "H2a 裸5 / 2b の斧→狙撃手(貫き) / 巡礼3+詠唱兵(全体)・合計 116.6%。チャージ化の前後を測る台",
            ChargeBench()),
    };

    Console.WriteLine($"# 向きは序列を動かすか（seed 0..{BridgeSeeds - 1} の {BridgeSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("向きのある波を列として組み、engage と同じ物差し（期待突破数・突破率、投入部隊数 1〜2）で");
    Console.WriteLine("全編成を測ったもの。**見たいのは値の大小ではなく順位の入れ替わり**——第4期の逆順列は");
    Console.WriteLine("指標が大きく動いたのに中身が「第五波の独立勝率の測り直し」だった。");
    Console.WriteLine();
    Console.WriteLine("**検算: 平坦列の 期待突破数(1) = 2.05 / 突破率(1) = 6.4%**（第5期の推奨列 1b/2b/3a）。");
    Console.WriteLine("一致しなければこの表は読めない。");
    Console.WriteLine();
    foreach (var (name, note, _) in columns) Console.WriteLine($"- **{name}**: {note}");
    Console.WriteLine();

    // --- 計測 ---
    int nCol = columns.Length, nT = targets.Length;
    var exp1 = new double[nCol, nT];
    var full1 = new double[nCol, nT];
    var exp2 = new double[nCol, nT];
    var full2 = new double[nCol, nT];
    var deg1 = new double[nCol, nT];   // 突破度（第8期 Phase U）
    var deg2 = new double[nCol, nT];
    for (int c = 0; c < nCol; c++)
        for (int t = 0; t < nT; t++)
        {
            Formation[] one = { targets[t].F };
            Formation[] two = { targets[t].F, targets[t].F };
            double e1 = 0, e2 = 0, d1 = 0, d2 = 0;
            int w1 = 0, w2 = 0;
            for (int seed = 0; seed < BridgeSeeds; seed++)
            {
                EngagementResult r1 = EngagementEngine.Run(one, columns[c].Squads, seed, verbose: false);
                e1 += r1.EnemySquadsCleared;
                d1 += BreakthroughDegree(r1, columns[c].Squads.Length);
                if (r1.PlayerWon) w1++;
                EngagementResult r2 = EngagementEngine.Run(two, columns[c].Squads, seed, verbose: false);
                e2 += r2.EnemySquadsCleared;
                d2 += BreakthroughDegree(r2, columns[c].Squads.Length);
                if (r2.PlayerWon) w2++;
            }
            exp1[c, t] = e1 / BridgeSeeds;
            full1[c, t] = w1 * 100.0 / BridgeSeeds;
            exp2[c, t] = e2 / BridgeSeeds;
            full2[c, t] = w2 * 100.0 / BridgeSeeds;
            deg1[c, t] = d1 / BridgeSeeds;
            deg2[c, t] = d2 / BridgeSeeds;
        }

    // --- 列ごとの合計代金と、第3波の向き（第8期 Phase V §3-1・§3-3） ---
    // 波の代金は cost / gradient / aim / flip と同じ物差し（勝った試行の残HP% → 代金 =
    // 100% − 残HP%、区分は HasAoe）で、ここで測り直す。列の合計代金と突破度を同じ
    // 実行の中で並べないと、「境目に近づけたら相関が動いたか」を突き合わせられない。
    // **向きの無い列で橋を測っても判定にならない**ので、単体−範囲 を必ず併記する（§5-7）。
    Console.WriteLine("### 列の合計代金と、第3波の向き（同じ実行で測り直したもの）");
    Console.WriteLine();
    Console.WriteLine("代金は cost / aim / flip と同じ物差し（勝った試行の残HP% → 代金 = 100% − 残HP%）。");
    Console.WriteLine("`合計代金` は3波の代金の和で、部隊の容量（約 100%）と比べて読む。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 第1波 | 第2波 | 第3波 | 合計代金 | 第3波の 単体−範囲 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    foreach (var (name, _, squads) in columns)
    {
        var costs = squads.Select(w => WaveCost(targets, w, BridgeSeeds)).ToArray();
        Console.WriteLine($"| {name} | {costs[0].Mean:F1}% | {costs[1].Mean:F1}% | {costs[2].Mean:F1}% "
            + $"| {costs.Sum(c => c.Mean):F1}% | {costs[2].Split:+0.0;-0.0}pt |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine("`単体−範囲` がマイナスなら範囲持ちに高くつく（反転している）。**-4pt を下回ると");
    Console.WriteLine("向きが編成間のばらつき（SD 13pt 前後）に埋もれる**ので、その列は向きの判定に使えない。");
    Console.WriteLine();

    // --- 突破度の検算（列長1では最終戦＝初戦なので両者が一致するはず。第8期 §2-3） ---
    // 一致しなければ分母か更新位置がずれている。表を読む前にここで止まれるよう先に出す。
    double maxGap = 0;
    for (int t = 0; t < nT; t++)
        for (int seed = 0; seed < 20; seed++)
        {
            EngagementResult r = EngagementEngine.Run(new[] { targets[t].F }, new[] { W3Elite3 }, seed, verbose: false);
            maxGap = Math.Max(maxGap, Math.Abs(r.LastBattleAttrition - r.FirstBattleAttrition));
        }
    Console.WriteLine($"**検算（列長1）: |LastBattleAttrition − FirstBattleAttrition| の最大 = {maxGap:F6}**"
        + $"（{nT} 編成 × seed 0..19。0 でなければ突破度の分母がずれている）");
    Console.WriteLine();

    Console.WriteLine("### 列ごとの全編成平均（検算はこの表の平坦列を見る）");
    Console.WriteLine();
    Console.WriteLine("| 列 | 期待突破数(1) | 突破度(1) | 突破率(1) | 期待突破数(2) | 突破度(2) | 突破率(2) | 非線形 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    for (int c = 0; c < nCol; c++)
    {
        double a1 = Avg(c, exp1), a2 = Avg(c, exp2);
        Console.WriteLine($"| {columns[c].Name} | {a1:F2} | {Avg(c, deg1):F2} | {Avg(c, full1):F1}% "
            + $"| {a2:F2} | {Avg(c, deg2):F2} | {Avg(c, full2):F1}% "
            + $"| {(a1 == 0 ? "—" : $"{a2 / (2 * a1):F2}")} |");
    }
    double Avg(int c, double[,] m) => Enumerable.Range(0, nT).Average(t => m[c, t]);

    // --- 順位 ---
    // 期待突破数(1) の降順で順位を付ける（同値は平均順位）。1部隊で見るのは、2部隊だと
    // 突破率が 92〜100% に飽和して序列が潰れるため（第5期の持ち越し論点(2)）。
    var rank1 = new double[nCol][];
    var rank2 = new double[nCol][];
    var rankD1 = new double[nCol][];   // 突破度での順位（第8期 Phase U）
    var rankD2 = new double[nCol][];
    for (int c = 0; c < nCol; c++)
    {
        rank1[c] = AverageRanksDesc(Enumerable.Range(0, nT).Select(t => exp1[c, t]).ToArray());
        rank2[c] = AverageRanksDesc(Enumerable.Range(0, nT).Select(t => exp2[c, t]).ToArray());
        rankD1[c] = AverageRanksDesc(Enumerable.Range(0, nT).Select(t => deg1[c, t]).ToArray());
        rankD2[c] = AverageRanksDesc(Enumerable.Range(0, nT).Select(t => deg2[c, t]).ToArray());
    }

    Console.WriteLine();
    Console.WriteLine("### 編成別の期待突破数と順位（1部隊。順位は期待突破数(1) の降順・同値は平均順位）");
    Console.WriteLine();
    Console.WriteLine("`範` は Def.Pattern に薙ぎ/全体を含む編成（HasAoe。cost 以来ずっと同じ区分）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 範 | 平坦 期待 | 平坦 順位 | 反転 期待 | 反転 順位 | 順位差 "
        + "| 平坦 突破度 | 反転 突破度 | 突破度の順位差 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderBy(t => rank1[0][t]))
        Console.WriteLine($"| {targets[t].Name} | {(HasAoe(targets[t].F) ? "○" : "")} "
            + $"| {exp1[0, t]:F2} | {rank1[0][t]:F1} | {exp1[1, t]:F2} | {rank1[1][t]:F1} "
            + $"| {rank1[0][t] - rank1[1][t]:+0.0;-0.0} "
            + $"| {deg1[0, t]:F3} | {deg1[1, t]:F3} | {rankD1[0][t] - rankD1[1][t]:+0.0;-0.0} |");
    Console.WriteLine();
    Console.WriteLine("順位差はプラスが「反転列で上がった」（順位の数字が小さくなった）。");
    Console.WriteLine("突破度は**突破した部隊数 + 最後に負けた部隊戦での削り割合**（全抜きは列長ちょうど）。");

    // --- 順位相関 ---
    Console.WriteLine();
    Console.WriteLine("### 順位相関（スピアマン。同順位は平均順位、順位列のピアソン相関）");
    Console.WriteLine();
    Console.WriteLine("| 比較 | 期待突破数 1部隊 | 期待突破数 2部隊 | **突破度 1部隊** | **突破度 2部隊** "
        + "| 順位差の絶対値の平均(期待・1部隊) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    for (int c = 1; c < nCol; c++)
        Console.WriteLine($"| 平坦列 × {columns[c].Name} | {Pearson(rank1[0], rank1[c]):F2} | {Pearson(rank2[0], rank2[c]):F2} "
            + $"| {Pearson(rankD1[0], rankD1[c]):F2} | {Pearson(rankD2[0], rankD2[c]):F2} "
            + $"| {Enumerable.Range(0, nT).Average(t => Math.Abs(rank1[0][t] - rank1[c][t])):F1} |");
    Console.WriteLine();
    Console.WriteLine("判定の目安（第7期 §3-3）: 0.7 未満かつ範囲持ちが反転列で上がっていれば**橋が架かった**。");
    Console.WriteLine("0.9 以上なら**代金には出たが結果には出ていない**（第4期の逆順列と同じ形）。");

    // --- 同値塊の大きさ（順位相関を額面で読んではいけない理由） ---
    // 期待突破数は「ちょうど2波抜けて3波目で尽きる」に張り付く編成が多い。同値塊の中の
    // 並びは平均順位で潰してあるが、塊の外に1編成出入りするだけで塊全体の順位が動くので、
    // **順位相関は塊の大きさに引きずられる**。塊の頭数と値の幅を必ず併記する。
    Console.WriteLine();
    Console.WriteLine("### 指標の分解能（順位相関を額面で読まないための注記）");
    Console.WriteLine();
    Console.WriteLine("| 列 | 期待突破数がちょうど 2.00 の編成数 | 期待の同値編成数 | 最小 | 最大 | 幅 "
        + "| 突破度が整数の編成数 | 突破度の同値編成数 | 突破度 最小 | 最大 | 幅 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int c = 0; c < nCol; c++)
    {
        var v = Enumerable.Range(0, nT).Select(t => exp1[c, t]).ToArray();
        var d = Enumerable.Range(0, nT).Select(t => deg1[c, t]).ToArray();
        Console.WriteLine($"| {columns[c].Name} | {v.Count(x => x == 2.0)} | {nT - v.Distinct().Count()} "
            + $"| {v.Min():F2} | {v.Max():F2} | {v.Max() - v.Min():F2} "
            + $"| {d.Count(x => x == Math.Floor(x))} | {nT - d.Distinct().Count()} "
            + $"| {d.Min():F3} | {d.Max():F3} | {d.Max() - d.Min():F3} |");
    }
    Console.WriteLine();
    Console.WriteLine("`同値編成数` は「他の編成と値がぴったり並んでいる編成の数」（編成数 − 相異なる値の数）。");
    Console.WriteLine("順位相関はこの塊の大きさに引きずられるので、突破度で塊が消えているかをここで見る。");

    // --- 値そのものの相関（同値塊の影響を受けない側） ---
    // 順位は塊で暴れるが、期待突破数の値は暴れない。順位相関が低くて値相関が高いなら、
    // 「序列が入れ替わった」のではなく「分解能が無い指標を順位に潰した」だけ。
    Console.WriteLine();
    Console.WriteLine("| 比較 | 期待突破数(1) の値の相関 | 期待突破数(2) の値の相関 |");
    Console.WriteLine("|---|--:|--:|");
    for (int c = 1; c < nCol; c++)
    {
        var a1 = Enumerable.Range(0, nT).Select(t => exp1[0, t]).ToArray();
        var b1 = Enumerable.Range(0, nT).Select(t => exp1[c, t]).ToArray();
        var a2 = Enumerable.Range(0, nT).Select(t => exp2[0, t]).ToArray();
        var b2 = Enumerable.Range(0, nT).Select(t => exp2[c, t]).ToArray();
        Console.WriteLine($"| 平坦列 × {columns[c].Name} | {Pearson(a1, b1):F2} | {Pearson(a2, b2):F2} |");
    }

    // --- 動いた編成 上位5つ ---
    Console.WriteLine();
    Console.WriteLine("### 順位が最も動いた編成 上位5つ（平坦列 → 反転列）");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 範 | 平坦 順位 | 反転 順位 | 動き |");
    Console.WriteLine("|---|:-:|--:|--:|---|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => Math.Abs(rank1[0][t] - rank1[1][t])).Take(5))
    {
        double d = rank1[0][t] - rank1[1][t];
        Console.WriteLine($"| {targets[t].Name} | {(HasAoe(targets[t].F) ? "○" : "")} "
            + $"| {rank1[0][t]:F1} | {rank1[1][t]:F1} | {(d > 0 ? "↑" : "↓")}{Math.Abs(d):F1} |");
    }

    // --- 範囲持ちが反転列で上がっているか（狙いどおりの向きに動いたか） ---
    // 順位が動いても、動いたのが範囲/単体と無関係なら「向きではない別の要因」（地力・
    // 自傷の固定費など）が動かしている。第7期 §3-3 の3行目を判別するための表。
    Console.WriteLine();
    Console.WriteLine("### 範囲持ち / 単体のみ の平均順位（狙いどおりの向きに動いたか）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成数 | 平坦 平均順位 | 反転 平均順位 | 差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (bool aoe in new[] { true, false })
    {
        var grp = Enumerable.Range(0, nT).Where(t => HasAoe(targets[t].F) == aoe).ToArray();
        double a = grp.Average(t => rank1[0][t]), b = grp.Average(t => rank1[1][t]);
        Console.WriteLine($"| {(aoe ? "範囲持ち" : "単体のみ")} | {grp.Length} | {a:F1} | {b:F1} | {a - b:+0.0;-0.0} |");
    }
    Console.WriteLine();
    Console.WriteLine("差がプラスなら反転列で順位が上がっている（順位の数字が小さくなった）。");

    // 値そのもので同じことを見る。順位は同値塊で暴れるが、期待突破数の差は暴れない。
    // 「範囲持ちだけが得をした」なら群間で Δ に差が出るはずで、出ないなら動かしたのは
    // 向きではない（第7期 §3-3 の3行目）。
    Console.WriteLine();
    Console.WriteLine("### 期待突破数の変化 Δ（反転列 − 平坦列。値なので同値塊の影響を受けない）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成数 |" + string.Concat(Enumerable.Range(1, nCol - 1).Select(c => $" Δ {columns[c].Name} |")));
    Console.WriteLine("|---|--:|" + string.Concat(Enumerable.Range(1, nCol - 1).Select(_ => "--:|")));
    foreach (bool aoe in new[] { true, false })
    {
        var grp = Enumerable.Range(0, nT).Where(t => HasAoe(targets[t].F) == aoe).ToArray();
        Console.WriteLine($"| {(aoe ? "範囲持ち" : "単体のみ")} | {grp.Length} |"
            + string.Concat(Enumerable.Range(1, nCol - 1)
                .Select(c => $" {grp.Average(t => exp1[c, t] - exp1[0, t]):+0.000;-0.000} |")));
    }

    Console.WriteLine();
    Console.WriteLine("### 突破度の変化 Δ（同じ表を突破度で。整数に潰れない分だけ細かく出る）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成数 |" + string.Concat(Enumerable.Range(1, nCol - 1).Select(c => $" Δ {columns[c].Name} |")));
    Console.WriteLine("|---|--:|" + string.Concat(Enumerable.Range(1, nCol - 1).Select(_ => "--:|")));
    foreach (bool aoe in new[] { true, false })
    {
        var grp = Enumerable.Range(0, nT).Where(t => HasAoe(targets[t].F) == aoe).ToArray();
        Console.WriteLine($"| {(aoe ? "範囲持ち" : "単体のみ")} | {grp.Length} |"
            + string.Concat(Enumerable.Range(1, nCol - 1)
                .Select(c => $" {grp.Average(t => deg1[c, t] - deg1[0, t]):+0.000;-0.000} |")));
    }
    Console.WriteLine();
    Console.WriteLine("範囲持ちの Δ が単体のみの Δ より**大きい**（＝損が小さい）なら、向きが結果に届いている。");

    // --- 群を「範囲/単体」から「自傷率」へ入れ替える（第9期 Phase Y） ---
    //
    // 第6〜8期で、攻撃パターンによる代金の向きは結果に届かないと分かった（代金では ±8pt
    // 開くのに突破度の順位相関は 0.83〜0.94、群差は境目でだけ +0.125 波）。**列も物差しも
    // 変えず、群の定義だけを差し替える**——比較可能性を保つため、ここから上の出力は1行も動かさない。
    //
    // 群分けの連続量は自傷率（= 自傷分 ÷ (敵由来 + 自傷分)。第9期 Phase X の bill と同じ
    // 計算で、測定台 113% の上で測る）。攻撃パターンと違って**編成の内部構造に課金する軸**で、
    // 第5期に目視で見えた「自傷の固定費」がその正体かどうかをここで確かめる。
    Formation[] bench = BenchColumn113();
    bool benchOk = bench.Length == columns[4].Squads.Length
        && Enumerable.Range(0, bench.Length).All(i => SameFormation(bench[i], columns[4].Squads[i]));

    Console.WriteLine();
    Console.WriteLine("## 自傷率で群分けし直す（第9期 Phase Y）");
    Console.WriteLine();
    Console.WriteLine("**列も物差しも第8期のまま。群の定義だけを 範囲/単体 → 自傷率 に差し替えたもの。**");
    Console.WriteLine("自傷率 = 自傷分 ÷ (敵由来 + 自傷分)。測定台 113%（反転列(低)）で味方1部隊の会戦を");
    Console.WriteLine($"seed 0..{BridgeSeeds - 1} 回して測る（`bill` と同じ計算）。");
    Console.WriteLine();
    Console.WriteLine(benchOk
        ? "**検算（測定台）: 自傷率を測った列は 反転列(低) と完全に同一。**"
        : "**測定台が 反転列(低) と違う。自傷率と Δ が別の列の上で測られているので読んではいけない。**");
    Console.WriteLine();

    var selfHarm = Enumerable.Range(0, nT)
        .Select(t => MeasureBill(targets[t].F, bench, BridgeSeeds).SelfHarmRate * 100)
        .ToArray();

    // 地力: 平坦列(低) の突破度(1)。自傷率と地力が交絡していると
    // 「自傷型が弱い」のが自傷のせいなのかたまたま弱い編成なのかを分けられない（§3-3）。
    var grit = Enumerable.Range(0, nT).Select(t => deg1[5, t]).ToArray();

    var sorted = Enumerable.Range(0, nT).OrderByDescending(t => selfHarm[t]).ToArray();
    int third = nT / 3;
    var tier = new int[nT];                       // 0=高自傷 / 1=中 / 2=低
    for (int k = 0; k < nT; k++) tier[sorted[k]] = k < third ? 0 : k < nT - third ? 1 : 2;

    Console.WriteLine("### 自傷率の分布");
    Console.WriteLine();
    var shSorted = selfHarm.OrderBy(x => x).ToArray();
    double shMean = shSorted.Average();
    double shSd = Math.Sqrt(shSorted.Average(x => (x - shMean) * (x - shMean)));
    Console.WriteLine($"編成数 {nT} / 最小 {shSorted[0]:F1}% / 中央 {shSorted[nT / 2]:F1}% / 最大 {shSorted[^1]:F1}% "
        + $"/ 平均 {shMean:F1}% / 標準偏差 {shSd:F1}pt / ちょうど 0% の編成 {selfHarm.Count(x => x == 0)}");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 範 | 自傷率 | 三分位 | 地力（平坦列(低) 突破度） | Δ突破度 反転列(低) |");
    Console.WriteLine("|---|:-:|--:|:-:|--:|--:|");
    foreach (int t in sorted)
        Console.WriteLine($"| {targets[t].Name} | {(HasAoe(targets[t].F) ? "○" : "")} | {selfHarm[t]:F1}% "
            + $"| {(tier[t] == 0 ? "高" : tier[t] == 1 ? "中" : "低")} | {grit[t]:F3} "
            + $"| {deg1[4, t] - deg1[0, t]:+0.000;-0.000} |");
    Console.WriteLine();
    Console.WriteLine("**一様（標準偏差が小さい）なら群分けは意味を持たない。**三分位は自傷率の降順で");
    Console.WriteLine($"上から {third} / {nT - 2 * third} / {third} 編成に切る（自傷率 0% が同数以上あると下位群は同値塊になる）。");

    // --- 三分位の群差（第8期の 範囲/単体 の表と同じ形。直接並べて読めるようにする） ---
    Console.WriteLine();
    Console.WriteLine("### 突破度の変化 Δ（自傷率の三分位。上の 範囲持ち/単体のみ の表と同じ形）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成数 | 自傷率 平均 |"
        + string.Concat(Enumerable.Range(1, nCol - 1).Select(c => $" Δ {columns[c].Name} |")));
    Console.WriteLine("|---|--:|--:|" + string.Concat(Enumerable.Range(1, nCol - 1).Select(_ => "--:|")));
    for (int g = 0; g < 3; g++)
    {
        var grp = Enumerable.Range(0, nT).Where(t => tier[t] == g).ToArray();
        Console.WriteLine($"| {(g == 0 ? "高自傷" : g == 1 ? "中" : "低自傷")} | {grp.Length} "
            + $"| {grp.Average(t => selfHarm[t]):F1}% |"
            + string.Concat(Enumerable.Range(1, nCol - 1)
                .Select(c => $" {grp.Average(t => deg1[c, t] - deg1[0, t]):+0.000;-0.000} |")));
    }
    Console.WriteLine();
    var hi = Enumerable.Range(0, nT).Where(t => tier[t] == 0).ToArray();
    var lo = Enumerable.Range(0, nT).Where(t => tier[t] == 2).ToArray();
    double gap113 = hi.Average(t => deg1[4, t] - deg1[0, t]) - lo.Average(t => deg1[4, t] - deg1[0, t]);
    Console.WriteLine($"**測定台 113%（反転列(低)）での 高自傷 − 低自傷 = {gap113:+0.000;-0.000} 波。**");
    Console.WriteLine("第8期の 範囲持ち − 単体のみ は同じ列で **+0.125 波**（0.237 − 0.112）。この2つを並べて読む。");

    // --- 連続量どうしの相関（群平均より情報量が多い） ---
    Console.WriteLine();
    Console.WriteLine("### 自傷率と Δ突破度の相関（値のピアソン相関。群に潰さない）");
    Console.WriteLine();
    Console.WriteLine("| 列 | 自傷率 × Δ突破度(1部隊) | 自傷率 × Δ突破度(2部隊) | 自傷率 × その列の突破度(1) |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int c = 1; c < nCol; c++)
    {
        var d1 = Enumerable.Range(0, nT).Select(t => deg1[c, t] - deg1[0, t]).ToArray();
        var d2 = Enumerable.Range(0, nT).Select(t => deg2[c, t] - deg2[0, t]).ToArray();
        var lv = Enumerable.Range(0, nT).Select(t => deg1[c, t]).ToArray();
        Console.WriteLine($"| {columns[c].Name} | {Pearson(selfHarm, d1):F2} | {Pearson(selfHarm, d2):F2} "
            + $"| {Pearson(selfHarm, lv):F2} |");
    }

    // --- 交絡（§3-3）。自傷型が弱いのは自傷のせいか、たまたま弱い編成が自傷型なだけか ---
    // Δ を使っているのは地力を引くためだが、引き切れているかは偏相関で確かめる。
    Console.WriteLine();
    Console.WriteLine("### 自傷率と地力の交絡");
    Console.WriteLine();
    var gritFlat = Enumerable.Range(0, nT).Select(t => deg1[0, t]).ToArray();
    Console.WriteLine($"- 自傷率 × 地力（平坦列(低) の突破度(1)）: **{Pearson(selfHarm, grit):F2}**");
    Console.WriteLine($"- 自傷率 × 地力（平坦列 の突破度(1)。Δ の基準側）: {Pearson(selfHarm, gritFlat):F2}");
    Console.WriteLine();
    Console.WriteLine("| 列 | 自傷率 × Δ突破度 | 地力 × Δ突破度 | 地力を制御した偏相関（自傷率 × Δ） |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int c = 1; c < nCol; c++)
    {
        var d = Enumerable.Range(0, nT).Select(t => deg1[c, t] - deg1[0, t]).ToArray();
        double rSG = Pearson(selfHarm, grit), rSD = Pearson(selfHarm, d), rGD = Pearson(grit, d);
        double denom = (1 - rSG * rSG) * (1 - rGD * rGD);
        double partial = denom <= 0 ? double.NaN : (rSD - rSG * rGD) / Math.Sqrt(denom);
        Console.WriteLine($"| {columns[c].Name} | {rSD:F2} | {rGD:F2} | **{partial:F2}** |");
    }
    Console.WriteLine();
    Console.WriteLine("自傷率 × 地力 の絶対値が大きいなら、群差は自傷の効果ではなく地力の差を見ている");
    Console.WriteLine("かもしれない。Δ は地力を引くための差分だが、引き切れているかは偏相関で確かめる。");
    return;
}

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

// charge モード: 大技の発火率を測る診断（第10期 Phase AC）。
// チャージ化の最初の失敗の形は「周期が長すぎて大技が1回も出ないまま決着し、波がただ
// 半額になる」なので、代金や突破度より先に**実際に何回発火したか**を見る必要がある。
// 発火数は編成によって変わる——速攻編成は大技が来る前に終わらせるので、これは敵の性質
// ではなく編成の性質として出る。それがこの期の仮説そのもの。
//
// UnitTally.BigAttacks（倍率つきで振った回数）と Charges（溜めた回数）を数える。
// どちらも verbose 非依存なので 200 seed × 全編成をそのまま回せる。
// tally は Def.Id で引くので敵側の Id だけを拾う（bill と同じ検算を通す）。
// 診断用で docs/ には置かない（seats / handoff / cost / bill と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 charge [絞り込み]
if (focusId == "charge")
{
    var all = CompareBuilds();
    const int ChargeSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    Formation[] bench = ChargeBench();

    Console.WriteLine($"# 大技の発火率（seed 0..{ChargeSeeds - 1} の {ChargeSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("`発火/戦` は1戦あたり敵が倍率つきで振った回数、`溜め/戦` は溜めた回数。");
    Console.WriteLine("**全編成で発火が 0 に近いなら周期が長すぎる**——大技が出ないまま決着していて、");
    Console.WriteLine("波がただ半額になっただけになる（第10期 §4-3）。");
    Console.WriteLine();
    Console.WriteLine("`溜め` が立っているのに `発火` が 0 なら、溜めた次の手番が来る前に倒されている。");
    Console.WriteLine("チャージ化していない状態では両方 0 になる（この表は前後で比べるためのもの）。");
    Console.WriteLine();

    // --- 既存5波（独立戦） ---
    Console.WriteLine("## 既存5波（単独戦）");
    Console.WriteLine();
    Console.WriteLine("cost と同じ単独戦。チャージを持つ敵が出る波だけが動く。");
    Console.WriteLine();

    var waves = EnemyCatalog.Stages.Select((st, i) => (Name: $"第{i + 1}波", Enemy: st.Enemy)).ToList();

    Console.Write("| 編成 |");
    foreach (var (wn, _) in waves) Console.Write($" {wn} 発火/戦 |");
    Console.Write(" 平均ターン |");
    Console.WriteLine();
    Console.Write("|---|");
    foreach (var _ in waves) Console.Write("--:|");
    Console.WriteLine("--:|");

    foreach (var (name, f) in targets)
    {
        Console.Write($"| {name} |");
        double turnSum = 0;
        int turnN = 0;
        foreach (var (_, enemy) in waves)
        {
            var ids = enemy.Occupied().Select(x => x.Def.Id).ToHashSet();
            double big = 0;
            for (int seed = 0; seed < ChargeSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                    if (ids.Contains(id)) big += t.BigAttacks;
                turnSum += r.Turns; turnN++;
            }
            Console.Write($" {big / ChargeSeeds:F2} |");
        }
        Console.WriteLine($" {turnSum / Math.Max(1, turnN):F2} |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    // --- チャージ台（会戦・味方1部隊） ---
    Console.WriteLine("## チャージ台（会戦・味方1部隊）");
    Console.WriteLine();
    Console.WriteLine("bridge の7列目と同じ列（ChargeBench）。会戦なので3つの部隊戦を通算する。");
    Console.WriteLine("`到達` はその部隊戦まで会戦が続いた試行数で、発火はそこへ到達した試行の中の平均。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 発火/戦 | 溜め/戦 | 平均ターン | 第1戦 発火 | 第2戦 発火 | 第3戦 発火 | 第3戦 到達 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");

    // 味方と敵で Def.Id が衝突していると敵の発火に味方の分が混ざる。bill と同じ検算。
    var enemyIds = bench.SelectMany(w => w.Occupied().Select(x => x.Def.Id)).ToHashSet();
    var clash = targets.SelectMany(t => t.F.Occupied().Select(x => x.Def.Id))
                       .Where(enemyIds.Contains).Distinct().ToList();
    if (clash.Count > 0)
        Console.WriteLine($"| **Def.Id 衝突: {string.Join(", ", clash)} — この表は読めない** | | | | | | | |");

    foreach (var (name, f) in targets)
    {
        double big = 0, chg = 0, turns = 0;
        var bigB = new double[bench.Length];
        var reached = new int[bench.Length];
        for (int seed = 0; seed < ChargeSeeds; seed++)
        {
            EngagementResult r = EngagementEngine.Run(new[] { f }, bench, seed, verbose: false);
            for (int b = 0; b < r.Battles.Count; b++)
            {
                double e = 0;
                foreach ((string id, UnitTally t) in r.Battles[b].TallyByUnit)
                {
                    if (!enemyIds.Contains(id)) continue;
                    e += t.BigAttacks; chg += t.Charges;
                }
                big += e; turns += r.Battles[b].Turns;
                if (b < bench.Length) { reached[b]++; bigB[b] += e; }
            }
        }
        int battles = reached.Sum();
        Console.WriteLine($"| {name} | {big / Math.Max(1, battles):F2} | {chg / Math.Max(1, battles):F2} "
            + $"| {turns / Math.Max(1, battles):F2} "
            + string.Concat(Enumerable.Range(0, bench.Length)
                .Select(b => $"| {(reached[b] == 0 ? 0 : bigB[b] / reached[b]):F2} "))
            + $"| {reached[^1] * 100.0 / ChargeSeeds:F0}% |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    // --- チャージ化の前後（同じ実行の中で両方測る） -------------------------
    // 「前」は同じ敵の Actions を剥がした複製で作る。git を戻して測り直す運用にすると、
    // 前後の数字が別々の実行から来ることになり、後から再現できなくなる
    // （bridge が列の合計代金を自分の実行の中で測り直しているのと同じ判断）。
    //
    // Def.Id は複製しても同じ。会戦を別々に回すので tally が混ざることはない。
    Console.WriteLine("## チャージ化の前後（同じ台・同じ seed）");
    Console.WriteLine();
    Console.WriteLine("`前` は同じ敵から Actions だけを剥がした複製（毎ターン通常攻撃）。");
    Console.WriteLine("**平均火力は前後で同じ**（2周期 200% は (0+2)/2 = 1.0）なので、動いたぶんは");
    Console.WriteLine("すべて「火力の配り方」の効果——これが第10期の仮説そのもの。");
    Console.WriteLine();

    Formation[] plain = bench.Select(w =>
    {
        Formation c = w.Clone();
        foreach (var (slot, d) in w.Occupied()) c[slot] = StripActions(d);
        return c;
    }).ToArray();

    var degBefore = new double[targets.Length];
    var degAfter = new double[targets.Length];
    var turnBefore = new double[targets.Length];
    var turnAfter = new double[targets.Length];
    for (int t = 0; t < targets.Length; t++)
    {
        Formation[] one = { targets[t].F };
        for (int seed = 0; seed < ChargeSeeds; seed++)
        {
            EngagementResult b = EngagementEngine.Run(one, plain, seed, verbose: false);
            EngagementResult a = EngagementEngine.Run(one, bench, seed, verbose: false);
            degBefore[t] += BreakthroughDegree(b, plain.Length);
            degAfter[t] += BreakthroughDegree(a, bench.Length);
            turnBefore[t] += b.Battles.Sum(x => x.Turns);
            turnAfter[t] += a.Battles.Sum(x => x.Turns);
        }
        degBefore[t] /= ChargeSeeds; degAfter[t] /= ChargeSeeds;
        turnBefore[t] /= ChargeSeeds; turnAfter[t] /= ChargeSeeds;
    }

    Console.WriteLine("| 編成 | 範 | 前 突破度 | 後 突破度 | Δ | 前 順位 | 後 順位 | 順位差 | 前 総T | 後 総T |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|");
    double[] rb = AverageRanksDesc(degBefore), ra = AverageRanksDesc(degAfter);
    for (int t = 0; t < targets.Length; t++)
        Console.WriteLine($"| {targets[t].Name} | {(HasAoe(targets[t].F) ? "○" : "")} "
            + $"| {degBefore[t]:F3} | {degAfter[t]:F3} | {degAfter[t] - degBefore[t]:+0.000;-0.000} "
            + $"| {rb[t]:F1} | {ra[t]:F1} | {rb[t] - ra[t]:+0.0;-0.0} "
            + $"| {turnBefore[t]:F2} | {turnAfter[t]:F2} |");
    Console.WriteLine();

    // 順位相関の計算方法は第7期から変えない（スピアマン＝平均順位の列にピアソン）。
    // **1.0 に近いほど「順位が動いていない」＝ 9期分と同じ壁**。
    Console.WriteLine($"**前後の順位相関（スピアマン）= {Pearson(rb, ra):F2}**"
        + $"　値の相関 = {Pearson(degBefore, degAfter):F2}");
    Console.WriteLine();
    Console.WriteLine($"突破度の平均 {degBefore.Average():F3} → {degAfter.Average():F3}"
        + $"（{degAfter.Average() - degBefore.Average():+0.000;-0.000}）、"
        + $"編成間の SD {Sd(degBefore):F3} → {Sd(degAfter):F3}。");
    Console.WriteLine($"会戦の総ターン数 {turnBefore.Average():F2} → {turnAfter.Average():F2}"
        + $"（{turnAfter.Average() - turnBefore.Average():+0.00;-0.00}）。**大きく伸びていたら間延び**。");
    Console.WriteLine();

    // --- 群別（速攻 / 耐久 / 回復持ち。第10期 §4-2 の5番目） ---------------
    // 既存の区分は HasAoe（第5期〜）と自傷率の三分位（第9期）の2つしか無いので新しく定義する。
    //   速攻   = チャージ化前の会戦の総ターン数が短い側 1/3
    //   耐久   = 編成の定義上の総最大HP が大きい側 1/3（速攻と重なることはあり得る）
    //   回復持ち = 回復する特性を1つでも持つ駒を含む編成
    // 区分が重なるので排他にはしない（同じ編成が複数の群に出る）。
    Console.WriteLine("## 群別の代金（速攻 / 耐久 / 回復持ち）");
    Console.WriteLine();
    Console.WriteLine("区分はこの期で新しく定義したもの（既存は HasAoe と自傷率の三分位しか無い）。");
    Console.WriteLine("**排他ではない**——同じ編成が複数の群に出る。");
    Console.WriteLine();
    Console.WriteLine("- `速攻`: チャージ化前の会戦の総ターン数が短い側 1/3");
    Console.WriteLine("- `耐久`: 編成の定義上の総最大HP が大きい側 1/3");
    Console.WriteLine("- `回復持ち`: 味方を癒す／戻す特性（継ぎ当て・毒喰らい・移り木・継ぎ接ぎ）を持つ駒を含む編成");
    Console.WriteLine();

    int third = Math.Max(1, targets.Length / 3);
    var fast = Enumerable.Range(0, targets.Length).OrderBy(t => turnBefore[t]).Take(third).ToHashSet();
    var tanky = Enumerable.Range(0, targets.Length)
        .OrderByDescending(t => targets[t].F.Occupied().Sum(x => x.Def.MaxHp)).Take(third).ToHashSet();
    var healer = Enumerable.Range(0, targets.Length)
        .Where(t => targets[t].F.Occupied().Any(x => x.Def.Traits.Any(id =>
            id is TraitId.Mender or TraitId.Devour or TraitId.Drifter or TraitId.Reviver))).ToHashSet();

    Console.WriteLine("| 群 | 編成数 | 前 突破度 | 後 突破度 | Δ | 前 総T | 後 総T |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    void Group(string name, ICollection<int> ix)
    {
        if (ix.Count == 0) { Console.WriteLine($"| {name} | 0 | — | — | — | — | — |"); return; }
        double b = ix.Average(t => degBefore[t]), a = ix.Average(t => degAfter[t]);
        Console.WriteLine($"| {name} | {ix.Count} | {b:F3} | {a:F3} | {a - b:+0.000;-0.000} "
            + $"| {ix.Average(t => turnBefore[t]):F2} | {ix.Average(t => turnAfter[t]):F2} |");
    }
    Group("速攻", fast);
    Group("耐久", tanky);
    Group("回復持ち", healer);
    Group("どれでもない",
        Enumerable.Range(0, targets.Length).Where(t => !fast.Contains(t) && !tanky.Contains(t) && !healer.Contains(t)).ToList());
    Group("全編成", Enumerable.Range(0, targets.Length).ToList());
    Console.WriteLine();
    Console.WriteLine("**速攻と耐久で Δ の符号が割れるなら、時間軸が編成を割っている**（第10期 §4-3）。");
    Console.WriteLine("どの群も同じ向きに同じだけ動いているなら、チャージは全編成に一律の値引き／値上げでしかない。");
    Console.WriteLine();
    return;
}

// chain モード: 勝率だけでは見えない「連鎖の深さ」を測る。
// 「2枚で人並みに勝つ」編成と「5枚が畳みかけて無双する」編成は、勝率だけ見ると同じ100%になる。
// MaxEnemyKillsInOneTurn（1ターンで味方が何体倒したかの最大値）と、勝利時の決着ターン数を
// compare と同じ代表編成×全ステージで測って区別する。数値が大きいほど「畳みかけている」。
// timing モード: 味方側の行動パターンの「変種」を測る（第11期 Phase BC）。
//
// 第10期は敵だけがパターンを持ち、味方は毎ターン同じ行動を繰り返していた。相性が
// 片側にしか無いので「大技の前に回復を差す」が**そもそも表現できない**。Phase BB で
// ノノ・ミオを Skill へ移したので、ここでは**特性の数値を一切変えず、パターンだけが
// 違う変種**を並べて、いつ撃つかが結果を動かすかを見る。
//
//   N0 / M0 = [Skill]                （毎ターン。移行直後の形。UnitCatalog はこれ）
//   N1 / M1 = [Skill, Attack]        （隔ターン。手番の半分を攻撃に使う）
//   N2 / M2 = [Skill, Skill, Attack] （3ターン周期。敵の2周期と噛み合わない位相）
//
// 変種は UnitCatalog を書き換えずここでローカルに組む（gradient / aim と同じやり方）。
// 台は2種——チャージ台（bridge の7列目 = ChargeBench。第10期 AB-0）と既存5波。
// 第8期の「136% で測ると何も見えない」が効くので、片方だけでは判定できない。
// 診断用で docs/ には置かない。
//
//     dotnet run --project BattleSim -c Release 0 timing [絞り込み]
if (focusId == "timing")
{
    const int TimingSeeds = 200;
    var all = CompareBuilds();

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    // 変種の中身。ラベルは BB で入れたものをそのまま使う（台本の見た目を変えないため）。
    UnitAction Mend() => new(ActionKind.Skill, Label: "傷を繕っている");
    UnitAction Foul() => new(ActionKind.Skill, Label: "水を濁らせている");
    UnitAction Hit() => new(ActionKind.Attack);

    // V3 は §5-1 の表には無いが、**V1 の結果を読むために要る対照**。
    // V1 は敵の2周期とちょうど逆位相になり、大技ターンとの一致が全編成で 0.0% に
    // ロックされる（下の噛み合わせの表）。つまり V1 の落ち込みには「撃つ回数が半分」と
    // 「大技ターンに一度も乗らない」が混ざっていて、そのままでは分離できない。
    // V3 は**回数を V1 と揃えたまま位相だけ反転**させたもので、この2つを切り分ける。
    var variants = new (string Tag, UnitAction[] Nono, UnitAction[] Mio)[]
    {
        ("V0", new[] { Mend() },                new[] { Foul() }),
        ("V1", new[] { Mend(), Hit() },         new[] { Foul(), Hit() }),
        ("V2", new[] { Mend(), Mend(), Hit() }, new[] { Foul(), Foul(), Hit() }),
        ("V3", new[] { Hit(), Mend() },         new[] { Hit(), Foul() }),
    };

    Formation Swap(Formation f, int v)
    {
        Formation c = f.Clone();
        foreach (var (slot, d) in f.Occupied())
        {
            if (d.Id == "nono") c[slot] = WithActions(d, variants[v].Nono);
            else if (d.Id == "mio") c[slot] = WithActions(d, variants[v].Mio);
        }
        return c;
    }

    // 変種が実際に効く編成（ノノかミオを含む）。31編成のうち9編成しかないので、
    // **全編成の順位相関は自動的に 1.0 へ引っ張られる。** 第10期の 0.91 と方法を
    // 揃えた全編成の値と、変種が効く編成だけに絞った値の両方を出す。片方だけだと
    // 「動いていない」のか「動く駒が入っていないだけ」なのかが区別できない。
    bool Affected(Formation f) => f.Occupied().Any(x => x.Def.Id is "nono" or "mio");
    var affected = Enumerable.Range(0, targets.Length).Where(t => Affected(targets[t].F)).ToArray();

    Console.WriteLine($"# 行動パターンの変種（seed 0..{TimingSeeds - 1} の {TimingSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("**特性の係数は一切変えていない。** 違うのは行動パターンだけ。");
    Console.WriteLine();
    Console.WriteLine("| 変種 | ノノ | ミオ |");
    Console.WriteLine("|---|---|---|");
    Console.WriteLine("| V0 | `[繕い]` | `[濁し]` |");
    Console.WriteLine("| V1 | `[繕い, 攻撃]` | `[濁し, 攻撃]` |");
    Console.WriteLine("| V2 | `[繕い, 繕い, 攻撃]` | `[濁し, 濁し, 攻撃]` |");
    Console.WriteLine("| V3 | `[攻撃, 繕い]` | `[攻撃, 濁し]` | ← V1 と同じ回数・逆の位相（対照）");
    Console.WriteLine();
    Console.WriteLine($"変種が効く編成（ノノかミオを含む）: **{affected.Length} / {targets.Length}**");
    Console.WriteLine();

    // ---- 台1: チャージ台（会戦・味方1部隊） ------------------------------
    Formation[] bench = ChargeBench();

    var deg = new double[variants.Length][];
    var tot = new double[variants.Length][];
    for (int v = 0; v < variants.Length; v++)
    {
        deg[v] = new double[targets.Length];
        tot[v] = new double[targets.Length];
        for (int t = 0; t < targets.Length; t++)
        {
            Formation[] one = { Swap(targets[t].F, v) };
            for (int seed = 0; seed < TimingSeeds; seed++)
            {
                EngagementResult r = EngagementEngine.Run(one, bench, seed, verbose: false);
                deg[v][t] += BreakthroughDegree(r, bench.Length);
                tot[v][t] += r.Battles.Sum(x => x.Turns);
            }
            deg[v][t] /= TimingSeeds; tot[v][t] /= TimingSeeds;
        }
    }

    Console.WriteLine("## 台1: チャージ台（会戦・味方1部隊）");
    Console.WriteLine();
    Console.WriteLine("bridge の7列目と同じ列（ChargeBench）。突破度は第8期 Phase U と同じ定義。");
    Console.WriteLine("`*` が付いている行が変種の効く編成。他の22編成は3つの変種で完全に同じ値になる");
    Console.WriteLine("（差し替えていないので当然だが、**動いていないことの検算**になる）。");
    Console.WriteLine();
    Console.Write("| 編成 | * |");
    foreach (var (tag, _, _) in variants) Console.Write($" {tag} 突破度 |");
    foreach (var (tag, _, _) in variants.Skip(1)) Console.Write($" {tag}-V0 |");
    foreach (var (tag, _, _) in variants) Console.Write($" {tag} 総T |");
    Console.WriteLine();
    Console.Write("|---|:-:|");
    for (int i = 0; i < variants.Length * 3 - 1; i++) Console.Write("--:|");
    Console.WriteLine();
    for (int t = 0; t < targets.Length; t++)
    {
        Console.Write($"| {targets[t].Name} | {(Affected(targets[t].F) ? "*" : "")} |");
        for (int v = 0; v < variants.Length; v++) Console.Write($" {deg[v][t]:F3} |");
        for (int v = 1; v < variants.Length; v++) Console.Write($" {deg[v][t] - deg[0][t]:+0.000;-0.000} |");
        for (int v = 0; v < variants.Length; v++) Console.Write($" {tot[v][t]:F2} |");
        Console.WriteLine();
    }
    Console.WriteLine();

    // 順位相関の計算方法は第7期から変えない（スピアマン＝平均順位の列にピアソン）。
    double[] Sub(double[] v) => affected.Select(i => v[i]).ToArray();
    var rankAll = variants.Select((_, v) => AverageRanksDesc(deg[v])).ToArray();
    var rankSub = variants.Select((_, v) => AverageRanksDesc(Sub(deg[v]))).ToArray();

    Console.WriteLine($"**順位相関（全{targets.Length}編成）: "
        + string.Join(" / ", variants.Skip(1).Select((x, i) =>
            $"V0-{x.Tag} = {Pearson(rankAll[0], rankAll[i + 1]):F2}")) + "**　値の相関 = "
        + string.Join(" / ", variants.Skip(1).Select((_, i) => $"{Pearson(deg[0], deg[i + 1]):F2}")));
    Console.WriteLine();
    Console.WriteLine($"**順位相関（変種が効く{affected.Length}編成だけ）: "
        + string.Join(" / ", variants.Skip(1).Select((x, i) =>
            $"V0-{x.Tag} = {Pearson(rankSub[0], rankSub[i + 1]):F2}")) + "**　値の相関 = "
        + string.Join(" / ", variants.Skip(1).Select((_, i) =>
            $"{Pearson(Sub(deg[0]), Sub(deg[i + 1])):F2}")));
    Console.WriteLine();
    Console.WriteLine("突破度の平均 "
        + string.Join(" / ", variants.Select((x, v) => $"{x.Tag} {deg[v].Average():F3}"))
        + "、編成間の SD " + string.Join(" / ", variants.Select((_, v) => $"{Sd(deg[v]):F3}")) + "。");
    Console.WriteLine("会戦の総ターン数 "
        + string.Join(" / ", variants.Select((_, v) => $"{tot[v].Average():F2}"))
        + "。**大きく伸びていたら間延び**。");
    Console.WriteLine();

    // ---- 群差（回復持ち / 毒軸。第11期 §5-2 の2番目） --------------------
    // 第10期は 速攻 / 耐久 / 回復持ち で割った。ここでは変種が触るものに合わせて
    // 回復（ノノ）と毒（ミオ）で割る。**排他の2群にする**——第9期 0.131 波・
    // 第10期 0.064 波と並べるには「2つの群の Δ の差」が要るので、
    // 「どれでもない」を混ぜた3群以上にすると群差が定義できない。
    var healer = Enumerable.Range(0, targets.Length)
        .Where(t => targets[t].F.Occupied().Any(x => x.Def.Traits.Any(id =>
            id is TraitId.Mender or TraitId.Devour or TraitId.Drifter or TraitId.Reviver))).ToArray();
    var poison = Enumerable.Range(0, targets.Length)
        .Where(t => targets[t].F.Occupied().Any(x => x.Def.Traits.Any(id =>
            id is TraitId.Venom or TraitId.Miasma or TraitId.Amplifier or TraitId.Contagion))).ToArray();

    Console.WriteLine("## 群差（回復持ち / 毒軸）");
    Console.WriteLine();
    Console.WriteLine("- `回復持ち`: 味方を癒す／戻す特性（継ぎ当て・毒喰らい・移り木・継ぎ接ぎ）を含む");
    Console.WriteLine("- `毒軸`: 毒を作る／広げる特性（毒撃・瘴気・澱み・疫み）を含む");
    Console.WriteLine();
    Console.WriteLine("`群差` は2群の Δ の差。第9期の自傷率 0.131 波・第10期のチャージ 0.064 波と");
    Console.WriteLine("同じ物差しで、**これを下回るなら9期分で最も弱い軸**ということになる。");
    Console.WriteLine();
    Console.Write("| 群 | 編成数 |");
    foreach (var (tag, _, _) in variants) Console.Write($" {tag} |");
    foreach (var (tag, _, _) in variants.Skip(1)) Console.Write($" Δ({tag}-V0) |");
    Console.WriteLine();
    Console.Write("|---|--:|");
    for (int i = 0; i < variants.Length * 2 - 1; i++) Console.Write("--:|");
    Console.WriteLine();
    double GroupDelta(int[] ix, int v)
        => ix.Length == 0 ? 0 : ix.Average(t => deg[v][t]) - ix.Average(t => deg[0][t]);
    void Row(string name, int[] ix)
    {
        Console.Write($"| {name} | {ix.Length} |");
        for (int v = 0; v < variants.Length; v++)
            Console.Write(ix.Length == 0 ? " — |" : $" {ix.Average(t => deg[v][t]):F3} |");
        for (int v = 1; v < variants.Length; v++)
            Console.Write(ix.Length == 0 ? " — |" : $" {GroupDelta(ix, v):+0.000;-0.000} |");
        Console.WriteLine();
    }
    var nonHealer = Enumerable.Range(0, targets.Length).Where(t => !healer.Contains(t)).ToArray();
    var nonPoison = Enumerable.Range(0, targets.Length).Where(t => !poison.Contains(t)).ToArray();
    Row("回復持ち", healer);
    Row("非回復", nonHealer);
    Row("毒軸", poison);
    Row("非毒軸", nonPoison);
    Row("全編成", Enumerable.Range(0, targets.Length).ToArray());
    Console.WriteLine();
    Console.WriteLine("**群差（回復持ち − 非回復）: " + string.Join(" / ", variants.Skip(1).Select((x, i) =>
        $"{x.Tag} {GroupDelta(healer, i + 1) - GroupDelta(nonHealer, i + 1):+0.000;-0.000} 波")) + "**");
    Console.WriteLine("**群差（毒軸 − 非毒軸）: " + string.Join(" / ", variants.Skip(1).Select((x, i) =>
        $"{x.Tag} {GroupDelta(poison, i + 1) - GroupDelta(nonPoison, i + 1):+0.000;-0.000} 波")) + "**");
    Console.WriteLine();

    // ---- 台2: 既存5波（単独戦） -----------------------------------------
    Console.WriteLine("## 台2: 既存5波（単独戦）");
    Console.WriteLine();
    Console.WriteLine("compare と同じ台。変種が効く編成だけを出す（他は3つとも同じ値になる）。");
    Console.WriteLine($"セルは `{string.Join(" / ", variants.Select(x => x.Tag))}` の勝率。");
    Console.WriteLine();

    var waves = EnemyCatalog.Stages.Select((st, i) => (Name: $"第{i + 1}波", Enemy: st.Enemy)).ToList();
    Console.Write("| 編成 |");
    foreach (var (wn, _) in waves) Console.Write($" {wn} |");
    Console.WriteLine(" 平均T |");
    Console.Write("|---|");
    foreach (var _ in waves) Console.Write("---|");
    Console.WriteLine("--:|");

    var waveWin = new double[variants.Length][];
    for (int v = 0; v < variants.Length; v++) waveWin[v] = new double[waves.Count];

    foreach (int t in affected)
    {
        Console.Write($"| {targets[t].Name} |");
        var tAvg = new double[variants.Length];
        for (int w = 0; w < waves.Count; w++)
        {
            var cell = new double[variants.Length];
            for (int v = 0; v < variants.Length; v++)
            {
                Formation f = Swap(targets[t].F, v);
                int wins = 0; double turnSum = 0;
                for (int seed = 0; seed < TimingSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, waves[w].Enemy, seed, verbose: false);
                    if (r.PlayerWon) wins++;
                    turnSum += r.Turns;
                }
                cell[v] = wins * 100.0 / TimingSeeds;
                waveWin[v][w] += cell[v];
                tAvg[v] += turnSum / TimingSeeds / waves.Count;
            }
            Console.Write(" " + string.Join(" / ", cell.Select(x => x.ToString("F1"))) + " |");
        }
        Console.WriteLine(" " + string.Join(" / ", tAvg.Select(x => x.ToString("F1"))) + " |");
        Console.Out.Flush();
    }
    Console.Write("| **平均** |");
    for (int w = 0; w < waves.Count; w++)
        Console.Write(" " + string.Join(" / ", variants.Select((_, v) =>
            (waveWin[v][w] / affected.Length).ToString("F1"))) + " |");
    Console.WriteLine(" |");
    Console.WriteLine();

    // ---- 敵の大技ターンとの噛み合わせ（第11期 §5-2 の3番目） -------------
    //
    // 「詠唱兵・狙撃手が大技を撃つターンに、ノノの回復が乗っているか」を台本から数える。
    // 大技ターンの同定は Charge イベントを使う——溜めた駒が**次に攻撃したターン**が
    // 大技のターン。倍率を見て判定しないのは、イベントに載っているのが実ダメージで、
    // 攻撃力の変動と区別できないため。溜めた次の手番に痺れて飛ばされても正しく追える。
    //
    // 術のターンは Skill イベント。ChargeBench の敵は誰も Skill を持たないので、
    // Skill イベント = 移行した味方（ノノかミオ）の手番で確定する。
    //
    // ここだけ verbose:true で回す（台本が要る）。編成を9つに絞ってあるので許容範囲。
    Console.WriteLine("## 敵の大技ターンとの噛み合わせ");
    Console.WriteLine();
    Console.WriteLine("チャージ台で台本を取り、部隊戦ごとに数えた。");
    Console.WriteLine("`大技T/戦` は1部隊戦あたりの大技ターン数、`一致` はそのうち術も撃ったターンの割合、");
    Console.WriteLine("`素の術率` は全ターンのうち術を撃ったターンの割合（比較の基準線）。");
    Console.WriteLine();
    Console.WriteLine("**`一致` が `素の術率` から大きく離れるなら、位相が噛んでいる（または外れている）。**");
    Console.WriteLine("V1 は隔ターン・敵は2周期なので、ここが割れるなら一番大きい観測になる（第11期 §5-3）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 変種 | 大技T/戦 | 一致 | 素の術率 | 術T/戦 | 総T/戦 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|");

    // 回数と位相を分けて集計しておく（下のまとめで使う）。
    var castRate = new double[variants.Length][];
    var matchRate = new double[variants.Length][];
    for (int v = 0; v < variants.Length; v++)
    {
        castRate[v] = new double[targets.Length];
        matchRate[v] = new double[targets.Length];
    }

    foreach (int t in affected)
    {
        for (int v = 0; v < variants.Length; v++)
        {
            Formation[] one = { Swap(targets[t].F, v) };
            long bigT = 0, skillT = 0, allT = 0, hit = 0, battles = 0;
            for (int seed = 0; seed < TimingSeeds; seed++)
            {
                EngagementResult r = EngagementEngine.Run(one, bench, seed, verbose: true);
                foreach (BattleResult b in r.Battles)
                {
                    battles++;
                    var big = new HashSet<int>();
                    var skill = new HashSet<int>();
                    var turnsSeen = new HashSet<int>();
                    var charged = new HashSet<int>();   // 溜めた直後の駒
                    foreach (BattleEvent e in b.Events)
                    {
                        if (e.Turn > 0) turnsSeen.Add(e.Turn);
                        switch (e.Kind)
                        {
                            case BattleEventKind.Charge when e.ActorId is { } c:
                                charged.Add(c); break;
                            case BattleEventKind.Attack when e.ActorId is { } a && charged.Remove(a):
                                big.Add(e.Turn); break;
                            case BattleEventKind.Skill:
                                skill.Add(e.Turn); break;
                        }
                    }
                    bigT += big.Count; skillT += skill.Count; allT += turnsSeen.Count;
                    hit += big.Count(x => skill.Contains(x));
                }
            }
            castRate[v][t] = (double)skillT / Math.Max(1, battles);
            matchRate[v][t] = bigT == 0 ? 0 : hit * 100.0 / bigT;

            Console.WriteLine($"| {(v == 0 ? targets[t].Name : "")} | {variants[v].Tag} "
                + $"| {(double)bigT / Math.Max(1, battles):F2} "
                + $"| {(bigT == 0 ? "—" : $"{hit * 100.0 / bigT:F1}%")} "
                + $"| {(allT == 0 ? "—" : $"{skillT * 100.0 / allT:F1}%")} "
                + $"| {(double)skillT / Math.Max(1, battles):F2} "
                + $"| {(double)allT / Math.Max(1, battles):F2} |");
            Console.Out.Flush();
        }
    }
    Console.WriteLine();

    // ---- 回数 と 位相 のどちらが効いているか -----------------------------
    //
    // V1 と V3 は**周期が同じで位相だけ逆**（V1 は大技ターンと一致 0%、V3 は乗る側）。
    // ここが分離の要で、V1 と V3 の差はすべて位相のもの……ではない。戦闘が 3〜6 ターンで
    // 終わるので、V3 は初撃が T2 になるぶん **1戦あたりの発火回数そのものが減る**。
    // だから「術T/戦（回数）」と「一致（位相）」の両方を突破度に当てて、
    // どちらが順位を説明しているかを見る。
    //
    // **編成ごとに中心化してから相関を取る。** 生のまま 36点をまとめると、編成間の
    // 地力の差（突破度 1.8〜3.0）が変種の差（0.0〜0.3）を覆い隠して相関が 0 に潰れる。
    // 見たいのは「同じ編成の中で変種を替えたときに何が動くか」なので、各編成の平均を
    // 引いた偏差同士で当てる（第9期の群平均を引く扱いと同じ考え方）。
    double[] Centered(Func<int, int, double> pick) => affected.SelectMany(t =>
    {
        double m = Enumerable.Range(0, variants.Length).Average(v => pick(v, t));
        return Enumerable.Range(0, variants.Length).Select(v => pick(v, t) - m);
    }).ToArray();

    double[] cDeg = Centered((v, t) => deg[v][t]);
    double[] cCast = Centered((v, t) => castRate[v][t]);
    double[] cMatch = Centered((v, t) => matchRate[v][t]);

    Console.WriteLine("## 回数か、位相か");
    Console.WriteLine();
    Console.WriteLine($"変種が効く{affected.Length}編成 × {variants.Length}変種 = {cDeg.Length}点。");
    Console.WriteLine("**編成ごとに平均を引いた偏差で当てている**——生のままだと編成間の地力の差");
    Console.WriteLine("（突破度 1.8〜3.0）が変種の差（0.0〜0.3）を覆い隠して相関が 0 に潰れる。");
    Console.WriteLine();
    Console.WriteLine($"- 突破度 × 術T/戦（回数）: **{Pearson(cDeg, cCast):F2}**");
    Console.WriteLine($"- 突破度 × 一致（位相）: **{Pearson(cDeg, cMatch):F2}**");
    Console.WriteLine();
    Console.WriteLine("**V1 と V3 は周期が同じ（隔ターン）で位相だけ逆。**");
    Console.WriteLine($"一致は V1 {affected.Average(t => matchRate[1][t]):F1}% → "
        + $"V3 {affected.Average(t => matchRate[3][t]):F1}% と大きく開くのに、");
    Console.WriteLine($"突破度は V1 {affected.Average(t => deg[1][t]):F3} → V3 {affected.Average(t => deg[3][t]):F3}。");
    Console.WriteLine($"術T/戦は V1 {affected.Average(t => castRate[1][t]):F2} → V3 {affected.Average(t => castRate[3][t]):F2}。");
    Console.WriteLine();
    Console.WriteLine("**位相を大技ターンに乗せたほうが弱いなら、効いているのは回数のほう。**");
    Console.WriteLine("戦闘が短いので、周期を後ろにずらすと初撃が遅れて発火回数そのものが減る。");
    Console.WriteLine();
    return;
}

if (focusId == "power") { PowerDiag.Run(args, stageIndex); return; }

// bench モード: 台をまたぐ入れ替わりは構造的か（第13期 Phase DB）。
//
// 第12期は主の台（チャージ台・3波）と従の台（既存5波）で突破度の相関が r=0.69 / ρ=0.74 に
// 落ちるのを見つけた。第4〜11期がずっと当たっていた壁（順位相関 0.83〜1.00）が「同じ軸の
// 変種を比べていたことの結果」だった可能性がある——ただし**そもそもどれくらいなら「動いた」と
// 言えるのかの基準が無い**。乱数のばらつきだけでも相関は 1.00 未満になる。
//
// **要点は基準を先に測ること。** 同じ台を seed で半分に割り、前半と後半で突破度の相関を取る。
// これが「同じ条件を2回測ったときの一致度」＝測定の信頼性の上限で、台間の 0.74 はこれと
// 比べて初めて意味を持つ（§4-2）。
//
// 主と従は**長さ（3波 / 5波）と構成（チャージ台 / 順路）の両方が違う**ので交絡している。
// 1つずつ振った 2×2 の格子を組んで切り分ける（§4-3）:
//
//              長さ3        長さ5
//     主構成    T1          T4      （T4 = T1 + 既存の第4・5波）
//     従構成    T3          T2      （T3 = 順路の先頭3波、T2 = 順路5波）
//
//     長さ軸: T1↔T4（主構成で長さだけ違う） / T3↔T2（従構成で長さだけ違う）
//     構成軸: T3↔T1（長さ3で構成だけ違う） / T2↔T4（長さ5で構成だけ違う）
//
// 第12期が比べた主↔従は T1↔T2 で、**格子の対角線**——長さと構成が同時に動いている。
//
// 注意（§4-3）: 第10期のチャージは master に入っているので、既存5波も詠唱兵・狙撃手が
// 溜める世界になっている。**主と従の差は「チャージの有無」ではない。**
//
// 台は診断のローカルで組む（EnemyCatalog.Columns には足さない。第5期以来の方針）。
// T3 は既存の「地点」列と中身が同じになるが、ここでは「順路の先頭3波」であることが
// 台の意味なので、Columns から引かずに Stages から組み直している。
//
// 却下した案: 半割を偶奇だけで測る。EngagementEngine.Run は DeriveSeed(seed, battleIndex) で
// 部隊戦ごとの seed を作るので、偶奇に何か構造があると半割の値そのものが嘘になる。
// **前後半と偶奇の両方を出して、二つが一致することを確認材料にする。**
//
// 診断用で docs/ には置かない（power / timing / bill と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 bench [絞り込み]
if (focusId == "bench")
{
    const int BenchSeeds = 200;   // power と同じ
    var all = CompareBuilds();

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    var route = EnemyCatalog.Stages.Select(s => s.Enemy).ToArray();
    var benches = new (string Tag, string Name, string Note, IReadOnlyList<Formation> Squads)[]
    {
        ("T1", "チャージ台", "主の台そのまま。基準", ChargeBench()),
        ("T2", "順路5波", "既存5波。基準（従）", route),
        ("T3", "順路先頭3波", "**T2 と構成が同じで長さだけ違う**", route.Take(3).ToArray()),
        ("T4", "チャージ台+4・5波", "**T1 と長さだけ違う**（T1 + 既存の第4・5波）",
            ChargeBench().Concat(route.Skip(3)).ToArray()),
    };
    int nB = benches.Length;

    Console.WriteLine($"# 台をまたぐ入れ替わりは構造的か（seed 0..{BenchSeeds - 1} の {BenchSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("突破度は突破した部隊数 + 最後に負けた部隊戦での削り割合（0.0 〜 列長。第8期 Phase U）。");
    Console.WriteLine("投入部隊数は 1。**測定だけで、盤面は何も変えていない。**");
    Console.WriteLine();
    Console.WriteLine("| 台 | 中身 | 長さ | 構成 | 役割 |");
    Console.WriteLine("|:-:|---|--:|:-:|---|");
    foreach (var (tag, name, note, squads) in benches)
        Console.WriteLine($"| {tag} | {name} | {squads.Count} | {(tag is "T1" or "T4" ? "主" : "従")} | {note} |");
    Console.WriteLine();
    Console.WriteLine("```");
    Console.WriteLine("             長さ3        長さ5");
    Console.WriteLine("    主構成    T1          T4");
    Console.WriteLine("    従構成    T3          T2");
    Console.WriteLine("```");
    Console.WriteLine();
    Console.WriteLine("第12期が比べた 主↔従 は **T1↔T2 = 格子の対角線**で、長さと構成が同時に動いている。");
    Console.WriteLine();

    // --- 計測 ---
    // seed ごとの突破度を丸ごと持つ。半割は同じ計測から取り出すだけで済む
    // （2回走らせると、半割の値そのものに実行間のばらつきが乗る）。
    var per = new double[nB][][];         // [台][編成][seed]
    var dyn = new double[nB][][];         // [台][編成][動的特徴量]
    for (int b = 0; b < nB; b++)
    {
        per[b] = new double[nT][];
        dyn[b] = new double[nT][];
        for (int t = 0; t < nT; t++)
        {
            var m = MeasurePower(targets[t].F, benches[b].Squads, BenchSeeds);
            per[b][t] = m.PerSeed;
            dyn[b][t] = m.Dynamics;
        }
    }

    // 台ごとの編成別平均。相関はすべてこの 31 点の並びに対して取る。
    double[] Mean(int b, Func<int, bool> take) => Enumerable.Range(0, nT)
        .Select(t => Enumerable.Range(0, BenchSeeds).Where(take).Average(s => per[b][t][s])).ToArray();
    var full = Enumerable.Range(0, nB).Select(b => Mean(b, _ => true)).ToArray();

    // --- 0. 検算 ---
    Console.WriteLine("## 0. 検算");
    Console.WriteLine();
    Console.WriteLine("目的変数が天井（列長ちょうど＝全抜き）に張り付いた編成同士の差は測れていない。");
    Console.WriteLine("台ごとに何編成が潰れているかを、相関を読む前に出す。");
    Console.WriteLine();
    Console.WriteLine("`4波目に入った試行` は突破度 ≥ 3.0 の試行の割合（31編成 × 200 seed）。");
    Console.WriteLine("**長さ5の台でこれが 0% なら、4波目・5波目は一度も戦われていない**——");
    Console.WriteLine("その台は長さ3の台と同じ測定になり、長さの軸を振ったことにならない。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 長さ | 天井の編成数 | 突破度の幅 | 標準偏差 | 4波目に入った試行 |");
    Console.WriteLine("|:-:|--:|--:|---|--:|--:|");
    var reached4 = new double[nB];
    for (int b = 0; b < nB; b++)
    {
        int len = benches[b].Squads.Count;
        int ceil = full[b].Count(v => v >= len - 1e-9);
        reached4[b] = per[b].Sum(v => v.Count(x => x >= 3.0 - 1e-9)) * 100.0 / (nT * (double)BenchSeeds);
        Console.WriteLine($"| {benches[b].Tag} | {len} | {ceil} / {nT} "
            + $"| {full[b].Min():F3} 〜 {full[b].Max():F3} | {Sd(full[b]):F3} | {reached4[b]:F1}% |");
    }
    Console.WriteLine();
    // 長さを伸ばしても誰もそこまで届かないなら、長さの辺は「振ったつもり」で終わる。
    // 判定の前に、T2 と T3 の突破度が実際にどれだけ違うかを数えて出す。
    if (reached4[1] < 1.0)
    {
        int moved = Enumerable.Range(0, nT).Count(t => Math.Abs(full[1][t] - full[2][t]) > 1e-9);
        double maxMove = Enumerable.Range(0, nT).Max(t => Math.Abs(full[1][t] - full[2][t]));
        Console.WriteLine($"> **T2 で 4波目に入った試行は {reached4[1]:F1}% しかない。** 順路では 31 編成の");
        Console.WriteLine("> ほとんどが先頭3波を抜けないので、第4・第5波はほぼ戦われない——**T2 と T3 は");
        Console.WriteLine($"> 事実上同じ測定**で、突破度が動いた編成は {moved} / {nT}、最大でも {maxMove:F4} しか違わない。");
        Console.WriteLine("> したがって従構成側の長さの辺（T3 ↔ T2）はほとんど情報を持たない。長さの軸を実際に");
        Console.WriteLine("> 振れているのは主構成側（T1 ↔ T4）だけで、そちらも**動くのは T1 で天井に張り付いていた");
        Console.WriteLine($"> 編成に限られる**（T1 は {reached4[0]:F1}% の試行が全抜きで、その先が測れていない）。");
        Console.WriteLine();
    }

    // --- 1. 半割 = 測定の信頼性の上限 ---
    //
    // 半割は 100 seed 同士の一致度なので、**200 seed の測定の信頼性より低く出る**
    // （試行数が半分なら平均のばらつきは √2 倍）。台間の相関は 200 seed 同士なので、
    // そのまま並べると半割の側が不利になる。Spearman-Brown の補正
    //   r(2n) = 2·r(n) / (1 + r(n))
    // を掛けた値を「200 seed 相当の上限」として併記する。**補正前と後の両方を出す**
    // ——補正は仮定（両半分が同等・誤差が独立）を1つ置くので、生の値も見えている必要がある。
    Console.WriteLine("## 1. 半割 — 測定の信頼性の上限");
    Console.WriteLine();
    Console.WriteLine("**同じ台を seed で半分に割り、両半分で突破度の相関を取る。** 割り方は2種類:");
    Console.WriteLine();
    Console.WriteLine($"- **前後半**: 前半 = seed 0..{BenchSeeds / 2 - 1} / 後半 = seed {BenchSeeds / 2}..{BenchSeeds - 1}");
    Console.WriteLine("- **偶奇**: 偶数 seed / 奇数 seed");
    Console.WriteLine();
    Console.WriteLine("相関はどちらも**編成 31 点の並び**に対して取る（seed の並びではない）。");
    Console.WriteLine("`r` はピアソン、`ρ` はスピアマン（同順位は平均順位。power と同じ計算）。");
    Console.WriteLine();
    Console.WriteLine("半割は 100 seed 同士なので 200 seed の測定より一致度が低く出る。");
    Console.WriteLine("台間の相関は 200 seed 同士なので、そのまま並べると半割の側が不利になる——");
    Console.WriteLine("Spearman-Brown の補正 `r(2n) = 2r(n) / (1 + r(n))` を掛けた値を併記する");
    Console.WriteLine("（**補正は「両半分が同等・誤差が独立」を仮定するので、生の値も併記する**）。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 前後半 r | 前後半 ρ | 偶奇 r | 偶奇 ρ | 補正後 r | 補正後 ρ |");
    Console.WriteLine("|:-:|--:|--:|--:|--:|--:|--:|");
    var ceilingR = new double[nB];
    var ceilingRho = new double[nB];
    for (int b = 0; b < nB; b++)
    {
        var h1 = Correlate(Mean(b, s => s < BenchSeeds / 2), Mean(b, s => s >= BenchSeeds / 2));
        var h2 = Correlate(Mean(b, s => s % 2 == 0), Mean(b, s => s % 2 == 1));
        // 補正は2つの割り方の平均に掛ける（どちらか片方を選ぶ理由が無い）。
        double SB(double r) => 2 * r / (1 + r);
        ceilingR[b] = SB((h1.R + h2.R) / 2);
        ceilingRho[b] = SB((h1.Rho + h2.Rho) / 2);
        Console.WriteLine($"| {benches[b].Tag} | {h1.R:F3} | {h1.Rho:F3} | {h2.R:F3} | {h2.Rho:F3} "
            + $"| **{ceilingR[b]:F3}** | **{ceilingRho[b]:F3}** |");
    }
    Console.WriteLine();
    Console.WriteLine("**補正後の値が、この台でこの seed 数のとき序列がどこまで再現するかの上限。**");
    Console.WriteLine("台間の相関はこれを超えられない。0.9 を大きく割るなら 200 seed では");
    Console.WriteLine("そもそも編成の序列が安定していないことになり、過去の測定すべての精度が疑わしくなる（§6）。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 2. 台間の相関行列 ---
    Console.WriteLine("## 2. 突破度の台間相関（全組み合わせ）");
    Console.WriteLine();
    Console.WriteLine("**下三角がピアソン `r`、上三角がスピアマン `ρ`。** 対角は半割の補正後");
    Console.WriteLine("（＝その台自身との一致度の上限）を `r / ρ` で置いてある——**行を横に読めば、");
    Console.WriteLine("その台の上限と、他の台との一致度が同じ行に並ぶ。**");
    Console.WriteLine();
    Console.WriteLine("|  |" + string.Concat(benches.Select(x => $" {x.Tag} |")));
    Console.WriteLine("|:-:|" + string.Concat(benches.Select(_ => "--:|")));
    for (int i = 0; i < nB; i++)
    {
        var row = new List<string>();
        for (int j = 0; j < nB; j++)
        {
            if (i == j) row.Add($"*{ceilingR[i]:F2} / {ceilingRho[i]:F2}*");
            else row.Add($"{(j > i ? Correlate(full[i], full[j]).Rho : Correlate(full[i], full[j]).R):F2}");
        }
        Console.WriteLine($"| **{benches[i].Tag}** | {string.Join(" | ", row)} |");
    }
    Console.WriteLine();

    // --- 3. 長さか、構成か ---
    // 格子の4辺。1辺ごとに動いている変数は1つだけなので、辺の相関の低さが
    // そのままその変数の効き目になる。対角線（T1↔T2）は両方が動いた場合。
    var edges = new (string Label, int A, int B, string Axis)[]
    {
        ("T1 ↔ T4", 0, 3, "**長さ**（主構成・3波 → 5波）"),
        ("T3 ↔ T2", 2, 1, "**長さ**（従構成・3波 → 5波）"),
        ("T3 ↔ T1", 2, 0, "**構成**（長さ3・順路 → チャージ台）"),
        ("T2 ↔ T4", 1, 3, "**構成**（長さ5・順路 → チャージ台）"),
        ("T1 ↔ T2", 0, 1, "両方（第12期が比べた対角線）"),
    };

    Console.WriteLine("## 3. 長さか、構成か");
    Console.WriteLine();
    Console.WriteLine("格子の4辺は**動いている変数が1つだけ**なので、辺の相関の低さがその変数の効き目になる。");
    Console.WriteLine("最後の行は対角線（第12期が比べた組）で、両方が同時に動いている。");
    Console.WriteLine();
    Console.WriteLine("`上限` は両端の台の半割（補正後 ρ）の低いほう——**その対で望める最大の一致度**。");
    Console.WriteLine("`余地` = 上限 − ρ で、これが**測定のばらつきでは説明できない入れ替わりの量**。");
    Console.WriteLine();
    Console.WriteLine("| 対 | 動いた変数 | r | ρ | 上限(ρ) | 余地 | 平均\\|順位差\\| | 最大\\|順位差\\| |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    var ranks = Enumerable.Range(0, nB).Select(b => AverageRanksDesc(full[b])).ToArray();
    foreach (var (label, a, bb, axis) in edges)
    {
        var c = Correlate(full[a], full[bb]);
        double cap = Math.Min(ceilingRho[a], ceilingRho[bb]);
        var gaps = Enumerable.Range(0, nT).Select(t => Math.Abs(ranks[a][t] - ranks[bb][t])).ToArray();
        Console.WriteLine($"| {label} | {axis} | {c.R:F2} | {c.Rho:F2} | {cap:F2} "
            + $"| **{cap - c.Rho:F2}** | {gaps.Average():F1} | {gaps.Max():F1} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4. 順位はどう動いたか ---
    Console.WriteLine("## 4. 編成ごとの順位（1 が最良。同値は平均順位）");
    Console.WriteLine();
    Console.WriteLine("`長さ差` = 順位(短) − 順位(長)。**正なら長い台で順位が上がる**。");
    Console.WriteLine("`構成差` = 順位(順路) − 順位(チャージ台)。**正ならチャージ台で順位が上がる**。");
    Console.WriteLine("どちらも主構成側の辺（長さ差 = T1−T4 / 構成差 = T3−T1）で取る。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | T1 | T2 | T3 | T4 | 長さ差 | 構成差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    double[] dLen = Enumerable.Range(0, nT).Select(t => ranks[0][t] - ranks[3][t]).ToArray();
    double[] dComp = Enumerable.Range(0, nT).Select(t => ranks[2][t] - ranks[0][t]).ToArray();
    foreach (int t in Enumerable.Range(0, nT).OrderBy(t => ranks[0][t]))
        Console.WriteLine($"| {targets[t].Name} | {ranks[0][t]:F1} | {ranks[1][t]:F1} | {ranks[2][t]:F1} "
            + $"| {ranks[3][t]:F1} | {dLen[t]:+0.0;-0.0;0.0} | {dComp[t]:+0.0;-0.0;0.0} |");
    Console.WriteLine();

    // --- 5. 順位差は特徴量で説明できるか ---
    //
    // 入れ替わりが実在しても**何とも相関しない**なら、プレイヤーには「試すしかない」ものに
    // なる（§4-5 の2行目）。特徴量で予測できるなら、それが配分判断の素になる。
    // n = 31 なので単相関の一覧までにとどめる（第12期と同じ方針。多変量は 2 変数まで）。
    var statics = new (string Name, Func<Formation, double> Get)[]
    {
        ("体数",     f => f.Count),
        ("総HP",     f => f.Occupied().Sum(x => x.Def.MaxHp)),
        ("総攻",     f => f.Occupied().Sum(x => x.Def.Attack)),
        ("積",       f => (double)f.Occupied().Sum(x => x.Def.MaxHp) * f.Occupied().Sum(x => x.Def.Attack)),
        ("最薄HP",   f => f.Occupied().Min(x => x.Def.MaxHp)),
        ("後列HP",   f => f.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)),
        ("平均速度", f => f.Occupied().Average(x => x.Def.Speed)),
        ("範囲枚数", f => AoeCount(f)),
    };
    string[] dynNames = { "与ダメ/戦", "被ダメ/戦", "撃破/戦", "干渉/戦", "回復/戦", "自傷率", "与ダメ効率" };
    int nS = statics.Length, nF = nS + dynNames.Length;
    string[] featNames = statics.Select(s => s.Name).Concat(dynNames).ToArray();
    var feat = new double[nF][];
    for (int k = 0; k < nF; k++)
        feat[k] = Enumerable.Range(0, nT)
            .Select(t => k < nS ? statics[k].Get(targets[t].F) : dyn[0][t][k - nS]).ToArray();

    var cols = new (string Head, double[] V)[]
    {
        ("長さ差(主)", dLen),
        ("長さ差(従)", Enumerable.Range(0, nT).Select(t => ranks[2][t] - ranks[1][t]).ToArray()),
        ("構成差(3)",  dComp),
        ("構成差(5)",  Enumerable.Range(0, nT).Select(t => ranks[1][t] - ranks[3][t]).ToArray()),
        ("T1↔T2 差",  Enumerable.Range(0, nT).Select(t => ranks[1][t] - ranks[0][t]).ToArray()),
    };

    Console.WriteLine("## 5. 順位差は特徴量で説明できるか（単相関まで）");
    Console.WriteLine();
    Console.WriteLine("**動的特徴量は T1（主の台）で測った値を使う**——台ごとに違う値が出るので、");
    Console.WriteLine("どの台の値で説明するかを決めないと「入れ替わりを入れ替わりで説明する」ことになる。");
    Console.WriteLine("与ダメ・撃破・与ダメ効率は受け手側の定義（第13期 Phase DA）。");
    Console.WriteLine();
    Console.WriteLine("符号は §4 と同じ（正 = 長い台 / チャージ台で順位が上がる）。");
    Console.WriteLine("`T1↔T2 差` は 順位(T2) − 順位(T1) で、正なら T1（チャージ台・3波）で順位が上がる。");
    Console.WriteLine();
    Console.WriteLine("| 区分 | 特徴量 |" + string.Concat(cols.Select(c => $" {c.Head} |")));
    Console.WriteLine("|:-:|---|" + string.Concat(cols.Select(_ => "--:|")));
    foreach (int k in Enumerable.Range(0, nF)
                 .OrderByDescending(k => Math.Abs(Correlate(feat[k], cols[4].V).R)))
        Console.WriteLine($"| {(k < nS ? "静" : "動")} | {featNames[k]} |"
            + string.Concat(cols.Select(c => $" {Correlate(feat[k], c.V).R:+0.00;-0.00} |")));
    Console.WriteLine();
    Console.WriteLine("並びは `T1↔T2 差` との |r| の降順（第12期が見た入れ替わりそのもの）。");
    Console.WriteLine();

    // --- 6. 判定 ---
    // §4-5 の表のどの行に当たるかを、数字から機械的に選ぶ。文章で判定すると
    // 「読み方によってはこうも取れる」が残る。
    var diag = Correlate(full[0], full[1]);
    double capDiag = Math.Min(ceilingRho[0], ceilingRho[1]);
    double bestPred = Enumerable.Range(0, nF).Max(k => Math.Abs(Correlate(feat[k], cols[4].V).R));
    double lenGap = Math.Min(
        Math.Min(ceilingRho[0], ceilingRho[3]) - Correlate(full[0], full[3]).Rho,
        Math.Min(ceilingRho[2], ceilingRho[1]) - Correlate(full[2], full[1]).Rho);
    double compGap = Math.Min(
        Math.Min(ceilingRho[2], ceilingRho[0]) - Correlate(full[2], full[0]).Rho,
        Math.Min(ceilingRho[1], ceilingRho[3]) - Correlate(full[1], full[3]).Rho);

    Console.WriteLine("## 6. 判定（§4-5 のどの行か）");
    Console.WriteLine();
    Console.WriteLine($"- 半割の上限（T1 / T2 の低いほう・ρ）: **{capDiag:F2}**");
    Console.WriteLine($"- 台間（T1↔T2・ρ）: **{diag.Rho:F2}**");
    Console.WriteLine($"- 余地: **{capDiag - diag.Rho:F2}**");
    var top3 = Enumerable.Range(0, nF)
        .OrderByDescending(k => Math.Abs(Correlate(feat[k], cols[4].V).R)).Take(3)
        .Select(k => $"{featNames[k]} {Correlate(feat[k], cols[4].V).R:+0.00;-0.00}");
    Console.WriteLine($"- 順位差を最もよく当てる特徴量の |r|: **{bestPred:F2}**（上位3: {string.Join(" / ", top3)}）");
    Console.WriteLine($"- 長さ軸の余地（2辺の小さいほう）: **{lenGap:F2}** / 構成軸: **{compGap:F2}**");
    Console.WriteLine();
    // 「予測できる」の閾値 |r| ≥ 0.5（r² = 0.25）はこちらで置いた線で、測定から出た値ではない。
    // 上に上位3つの生の値を出してあるので、線の引き方を変えたければそこから読み直せる。
    string verdict = capDiag - diag.Rho < 0.05
        ? "**半割 ≈ 台間。入れ替わりはノイズ。** 10期分の壁の解釈は変わらない（§4-5 の3行目）"
        : bestPred >= 0.5
            ? "**半割 ≫ 台間で、入れ替わりが特徴量で予測できる。** 配分判断の素がある（§4-5 の1行目）"
            : "**半割 ≫ 台間だが、入れ替わりが何とも相関しない。** 実在するが予測できない（§4-5 の2行目）";
    Console.WriteLine(verdict + "。");
    Console.WriteLine("（`|r| ≥ 0.50` を「予測できる」の線に置いた。この線は測定から出たものではないので、");
    Console.WriteLine("上位3つの生の値と併せて読むこと。）");
    Console.WriteLine();
    Console.WriteLine($"長さと構成では、余地の大きい**{(lenGap > compGap ? "長さ" : "構成")}**のほうが入れ替わりを生んでいる"
        + $"（{lenGap:F2} 対 {compGap:F2}）。");
    Console.WriteLine();
    return;
}

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

// layout モード: compare の各編成についてメンバー固定のまま6スロットへの全配置を試し、
// 全ステージ平均勝率で並べる。「この編成をどう置くか」を人手の勘で決めないための道具。
// 編成名が示す狙い（隣接ペア・後列必須など）との突き合わせは人がやる。上位だけでなく
// 現行配置の順位も出すのはそのため。
// reseat モード: 指定した編成だけを全配置に展開し、候補を seed 200 で測り直す。
// layout の上位5件は seed 50 の値で並んでいるうえ、「ガルドは前列」「セッキは後列」のような
// 狙いの制約を無視するので、制約下の最良が表に載らないことがある。
// ここでは 全体上位 / 制約を満たす上位 / 現行 を混ぜたプールを作り、seed 200 で並べ直す。
// confirm モード: 配置差し替えの採否を「選定に使っていない seed」で確かめる。
// reseat の値は 20〜30 件の候補から最大を採ったものなので、選択バイアスで必ず上振れする。
// 実際 2026-08-21 の差し替えでは 逆しま+後備え が in-sample +0.9pt → out-of-sample -0.1pt と符号ごと反転し、
// この1件だけ不採用になった。旧配置もここに直書きしてあるのは、採用後に走らせても
// 全部 0 になって記録として役に立たなくなるのを避けるため。
if (focusId == "confirm")
{
    IReadOnlyList<EnemyCatalog.Stage> stages = EnemyCatalog.Stages;
    const int Base = 200, Seeds = 400;   // 選定に使った seed 0..199 とは重ならない範囲

    // **閾値は第46期に 2.0 → 5.0 へ上げた。** 第45期の `seats2` を全48行で測ると、
    // `reseat` の**1位と5位の差**（＝上位帯の内部変動）は 中央値 2.15pt・Q3 4.65pt で、
    // **2.0pt は 48行中 26行で上位帯の内部変動より小さい**——ほぼノイズを閾値にしていた。
    // 5.0pt は 38/48 行でその内部変動の外側に出る（1位−5位が 5pt 未満の行が 38）。
    // 併せて、採否は**この閾値を通った差**ではなく**上位5通りの次数**で読むこと（第45期の残件 D。
    // 1位そのものは別 seed 帯で 48行中28行で入れ替わるが、次数の一致率は 98%）。
    const double Threshold = 5.0;        // これ未満は誤差とみなして据え置く

    // (編成名, 旧配置, 候補配置)。候補は reseat の「狙いを満たす最良」。
    //
    // **いまここに載っているのは、席番号タイブレークの乱数化後に全32編成を reseat し直して
    // 出た唯一の候補（差 2pt 以上）で、追試の結果は +1.4pt の「据え置き」。**
    // つまり乱数化の前後で採るべき配置は変わらなかった、という記録そのもの。
    // X字化(盤面の対称化)に伴う振り直しぶん。旧盤面の座標で書かれた過去の追試は
    // 座標ごと無効になったので、ここでは持ち越していない。
    var picks = new (string Name, Formation Old, Formation New)[]
    {
        ("反撃改 (ドハ×カド)",
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Kado, center: UnitCatalog.Doha, back1: UnitCatalog.Nel, back3: UnitCatalog.Nono),
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Nel, center: UnitCatalog.Kado, back1: UnitCatalog.Doha, back3: UnitCatalog.Nono)),
        // 置き去り（ナラ）の新2編成ぶん。仮置きは「ナラを中央」だったが、reseat の 120通り全探索で
        // 中央はカドの席だと出た（被弾強化側は 42.1% → 91.7%）。速攻側は最良でも +1.4pt で、
        // 仮置きとの差が閾値未満（この2本を1回の追試で並べるために、据え置き側も載せてある）。
        ("置き去り×被弾強化",
            Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Golm, center: UnitCatalog.Nara, back1: UnitCatalog.Kado, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nara, center: UnitCatalog.Kado, back1: UnitCatalog.Mudo, back3: UnitCatalog.Vel)),
        // **この編成は第21期に compare から外した**（100/0/0/0/0 で情報が出ていなかった）。
        // 行は記録として残す——消すと「追試して据え置いた」事実まで消える。
        ("置き去り×速攻",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Borg, center: UnitCatalog.Nara, back1: UnitCatalog.Tou, back3: UnitCatalog.Sasa),
            Formation.Build(front1: UnitCatalog.Tou, front3: UnitCatalog.Sasa, center: UnitCatalog.Sero, back1: UnitCatalog.Borg, back3: UnitCatalog.Nara)),
        // route 診断（第19期）の V4。自傷の燃料をムドの被弾強化まで通す配置で、
        // seed 0..199 では -1.5pt と閾値の内側に入った。**閾値の境目なので追試が要る。**
        // reseat と違って勝率の探索から出た候補ではなく、「巨躯の被覆から出す」という
        // 人間側の狙いから組んだ席なので、採否は差の符号ではなく安定性で読む。
        ("置き去り×被弾強化 (route V4)",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nara, center: UnitCatalog.Kado, back1: UnitCatalog.Mudo, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Vel, front3: UnitCatalog.Nara, center: UnitCatalog.Kado, back1: UnitCatalog.Golm, back3: UnitCatalog.Mudo)),
        // 第20期の新1編成。仮置き（ナラ中央）は 86.8% で、reseat 1位はヴェルを中央に上げる形。
        // 「置き去り×被弾強化」で中央がカドの席だったのと同じで、**ナラは席を選ばない**
        // （速さで対象を選ぶので隣接も列も見ない）から、中央を要求する駒に譲るのが正しい。
        ("置き去り×死の連鎖",
            Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Mug, center: UnitCatalog.Nara, back1: UnitCatalog.Rica, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Nara, center: UnitCatalog.Vel, back1: UnitCatalog.Rica, back3: UnitCatalog.Mug)),
        // 第21期の差し替え行。仮置きは swap S4 の席そのまま（中央ナラ・34.4%）で、
        // reseat 1位はセロを中央へ上げてナラを後1へ下げる形。3期続けて同じ結論——
        // **ナラは席を選ばない**（速さで対象を選ぶので隣接も列も見ない）ので、
        // 中央を要求する駒に譲るのが正しい。ここでは狙撃のセロが中央に上がる。
        ("置き去り×分散回復",
            Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Gald, center: UnitCatalog.Nara, back1: UnitCatalog.Dolga, back3: UnitCatalog.Sasa),
            Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Gald, center: UnitCatalog.Sero, back1: UnitCatalog.Nara, back3: UnitCatalog.Dolga)),
        // 物理軸の連鎖・第1弾の新3編成（第26期）。旧＝計画書の仮置き（メンバーは組み直し後で同じ）、
        // 候補＝reseat 1位。3本とも狙い（ガルド前列）を満たす席が最良だったので、
        // 「狙いを満たす最良」と全体1位が食い違う行は無い。
        ("責め苦 (トウ×シガ)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Tou, center: UnitCatalog.Shiga, back1: UnitCatalog.Gan, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Tou, front3: UnitCatalog.Gald, center: UnitCatalog.Gan, back1: UnitCatalog.Shiga, back3: UnitCatalog.Dolga)),
        // ヒサは中央でなくてよい、と出た行。中央に置くと隣接次数4で最大HPのガルドが確実に
        // 標的になるが、reseat 1位はガンを前3へ上げてドルガを後3へ下げる形（標的はガルドのまま）。
        ("仇討ち (ヒサ×ザン)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Hisa, back1: UnitCatalog.Zan, back3: UnitCatalog.Gan),
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gan, center: UnitCatalog.Hisa, back1: UnitCatalog.Zan, back3: UnitCatalog.Dolga)),
        // 破片の検証台。候補ではヒビが中央（範囲の集まる席）、ゴルムが前3で後方を被覆し、
        // ザンは後3——**ザンが殴られにくい席ほど刃が出る**という読みと一致する。
        // 標的はヒサ(前1)の隣接＝中央ヒビ(55)と後1ドルガ(85)の最大でドルガに移る。
        ("仇討ち×砕け (ヒビ×ザン)",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Hibi, center: UnitCatalog.Hisa, back1: UnitCatalog.Zan, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Golm, center: UnitCatalog.Hibi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Zan)),
        // ザンの「1ターンに1回」撤去に伴う振り直し（第26期の追補）。**規則を変えたら席も測り直す**
        // ——上限があった頃は「殴られる回数」が出力に乗らなかったので、標的を誰に付けるかの
        // 価値が潰れていた。撤去後は**巨躯ゴルム(150)を中央に置いてそこへ標的を集める**形が
        // 最良になる（ヒサは前1で隣接＝中央ゴルムと後1ドルガ、最大HPはゴルム）。
        // 仇討ち (ヒサ×ザン) の方は撤去後も現行が「狙いを満たす最良」のままなので候補なし。
        ("仇討ち×砕け (ヒビ×ザン) / 上限撤去後",
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Golm, center: UnitCatalog.Hibi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Zan),
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Hibi, center: UnitCatalog.Golm, back1: UnitCatalog.Dolga, back3: UnitCatalog.Zan)),
        // 物理軸の連鎖・第2弾の新2編成（第28期）。旧＝計画書の仮置き（顔ぶれは組み直し後で同じ）、
        // 候補＝reseat 1位。**どちらもガルド・セッキを含まないので狙いの制約が無く、
        // 「狙いを満たす最良」と全体1位が一致する。**
        //
        // 裂き の旧は「ゴルム前3・ドルガ中央のまま キリとエグだけを入れ替えた形」。
        // **速さの順序は入れ替えても崩れない**（12 対 6 で決まり、隣接も列も見ない）のに
        // +15.1pt 動く——効いているのは順序ではなく受けの配り方で、キリが前1の的になり
        // エグが後3のゴルムの被覆に入る側が上。**「席を選ばない駒」でも席で15pt動く。**
        ("裂き (キリ×エグ)",
            Formation.Build(front1: UnitCatalog.Egu, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Kiri),
            Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Egu)),
        // 中央をヴェル（蘇生。守られて完走する側）に譲る形が最良で +31.5pt。
        // **リィカもエグも中央を要求しない**——墓守は死んだ味方の数だけを読み、抉りは
        // 傷を持つ敵を読むので、どちらも隣接も列も見ない。中央を要求しない駒が並んだら、
        // 完走することに価値がある駒に譲る（第20期・第21期のナラと同じ結論の3例目）。
        ("裂き×責め苦 (キリ×エグ×シガ)",
            Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Kiri, center: UnitCatalog.Rica, back1: UnitCatalog.Vel, back3: UnitCatalog.Egu),
            Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Kiri, center: UnitCatalog.Vel, back1: UnitCatalog.Rica, back3: UnitCatalog.Egu)),
        // 傷軸・第3弾の新2編成（第30期）。旧＝仮置き（ノミを前1に出した形）、候補＝reseat 1位。
        // どちらもガルド・セッキを含まないので「狙いを満たす最良」と全体1位が一致する。
        //
        // **どちらの候補もノミが中央**。刻みは供給と変換を1手に畳んでいるので隣接も列も読まないが、
        // **執着（対象選択の束縛）は「殴り続けられること」が価値**なので、中央＝次数4の席で
        // 巨躯ゴルムの被覆に入って長く立つ側が上に来る。第20期・第21期の「中央を要求しない駒が
        // 並んだら完走する側に譲る」の系列だが、**譲られる理由が蘇生ではなく執着**なのが新しい。
        ("刻み (ノミ単騎)",
            Formation.Build(front1: UnitCatalog.Nomi, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Gan),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gan, center: UnitCatalog.Nomi, back1: UnitCatalog.Vel, back3: UnitCatalog.Dolga)),
        ("刻み×抉り (ノミ×エグ)",
            Formation.Build(front1: UnitCatalog.Nomi, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Egu),
            Formation.Build(front1: UnitCatalog.Egu, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Vel)),
        // 傷軸・第4弾の試験台2本（第37期）。旧＝仮置き（既存行のエグ1枚をナタに差し替えただけ）、
        // 候補＝reseat 1位。どちらもガルド・セッキを含まないので「狙いを満たす最良」と全体1位が一致する。
        //
        // **どちらの候補もナタを前列へ出す形。** ナタは傷を追って標的を選ぶので隣接も列も読まないが、
        // 第30期の「中央を要求しない駒は完走する側に譲る」とは逆に出た——ナタは**傷持ちがいない間は
        // 手番を捨てる**ので、後列で長く立っても振る回数が増えない。増えるのは供給が回っている
        // 時間の側で、そこは書き手（キリ・ノミ）の生存で決まる。
        ("断ち (キリ×ナタ)",
            Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Nata),
            Formation.Build(front1: UnitCatalog.Nata, front3: UnitCatalog.Golm, center: UnitCatalog.Vel, back1: UnitCatalog.Kiri, back3: UnitCatalog.Dolga)),
        // 候補は**第38期の reseat 1位**（74.3%）に差し替えた。閾値待ちを入れて盤面が動いたので、
        // 第37期の候補（ヴェル↔ノミ の入れ替わった形・72.5%）はもう1位ではない。
        ("刻み×断ち (ノミ×ナタ)",
            Formation.Build(front1: UnitCatalog.Nata, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nata, center: UnitCatalog.Dolga, back1: UnitCatalog.Nomi, back3: UnitCatalog.Vel)),
        // 傷軸・第5弾の試験台2本（第39期）。旧＝仮置き（既存行のエグ1枚をハリに差し替えただけ）、
        // 候補＝reseat 1位。どちらもガルド・セッキを含まないので「狙いを満たす最良」と全体1位が一致する。
        //
        // **どちらの候補もハリを後1へ、ゴルムを前1へ。** ナタ（第37期）が前列へ出る形だったのと
        // 逆を向く——ハリは**傷持ちがいなくても普通に殴る**ので、後列で長く立つほど繕いの機会が増える。
        // 「手番を捨てない読み手」は第30期のノミと同じ側（完走する価値がある駒）に戻る。
        ("裂き×縫い (キリ×ハリ)",
            Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Hari),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Kiri, center: UnitCatalog.Dolga, back1: UnitCatalog.Hari, back3: UnitCatalog.Vel)),
        ("刻み×縫い (ノミ×ハリ)",
            Formation.Build(front1: UnitCatalog.Hari, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Vel),
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nomi, center: UnitCatalog.Dolga, back1: UnitCatalog.Hari, back3: UnitCatalog.Vel)),
        // 移動軸・弱体化軸の試験台（第41期）。旧＝仮置き（ハネを前3・ウツを中央）、候補＝reseat 1位。
        //
        // **候補はハネを後3の角へ下げる形。** 効果Bは隣接する生存味方**全員**の攻撃力を引くので、
        // 隣接次数がそのまま値段になる（角2体・中央4体）。しかも候補の席では
        // ハネの隣が**ガルド（Stoic で弾かれる＝代金ゼロ）とウツ（弱体化を3倍で利益に変える）**
        // の2体だけになり、**払う相手が1体もいない**。指示書 §6-2 が「ガルドが答えとして
        // 安すぎないか」を疑った形が、そのまま探索1位として出てきた。
        ("突き返し (ハネ×ウツ)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Hane, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Basa),
            Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hane)),
        // 第42期の台。仮置き（ガルド前1・ウツ前3・ドハ中央）は reseat 12位 63.0%、
        // 候補は reseat 1位 73.1%（ウツを前1へ、ドハを後1へ、ノノを中央へ）。
        ("分かち×逆しま (ドハ×ウツ)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Utsu, center: UnitCatalog.Doha, back1: UnitCatalog.Dolga, back3: UnitCatalog.Nono),
            Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald, center: UnitCatalog.Nono, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga)),
        // 第42期の集約。仮置き（ウケを前1＝台のウツの席）は reseat 21位 43.5%、
        // 候補は reseat 1位 56.0%（ウケを**中央＝隣接次数4**へ）。予測5の検証点。
        ("引き受け (ウケ×ドハ)",
            Formation.Build(front1: UnitCatalog.Uke, front3: UnitCatalog.Gald, center: UnitCatalog.Nono, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald, center: UnitCatalog.Uke, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga)),
        // 第43期の転嫁。仮置き（集約行と同じ席＝ワタ中央）は reseat 2位 77.6%、
        // 候補は reseat 1位 79.1%（ガルドとノノ／ドハとドルガをそれぞれ入れ替えた鏡像）。
        // **どちらもワタは中央**——上位8通りが全部ワタ中央で、角に落ちるのは19位から。
        ("渡し (ワタ×ドハ)",
            Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald, center: UnitCatalog.Wata, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Nono, center: UnitCatalog.Wata, back1: UnitCatalog.Dolga, back3: UnitCatalog.Doha)),
        // 驕り（第46期）。**新しい作法の1件目**——採否は「現行が `reseat` の上位5通りに入っているか」で決め、
        // 入っていない行だけを追試する（作法2）。`驕り (オゴ×ウケ)` は現行が 4位 なので候補なし。
        //
        // `驕り改 (オゴ×ウツ)` は現行（オゴ中央）が 21通り中 17位。上位5通りは**全部
        // 「ウツが中央・オゴが角」**で、値は 92.7 / 92.1 / 91.5 / 91.5 / 91.4 と 1.3pt の中に固まっている
        // ——**次数は一意（オゴ 2・ウツ 4）だが、その中のどれを採るかは測っても決まらない。**
        // 候補にはその帯の1位を置く（帯の中のどれでも同じ、という記録のためにこの注を残す）。
        //
        // **上位帯ではオゴが必ずウツの隣に来る**（角は必ず中央と隣接し、中央がウツだから）。
        // つまり採用される配置ではプラス側（2倍）が1回も発火しない——第46期 §3 を参照。
        ("驕り改 (オゴ×ウツ)",
            Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald, center: UnitCatalog.Ogo, back1: UnitCatalog.Doha, back3: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Doha, center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga, back3: UnitCatalog.Ogo)),
    };

    Console.WriteLine("## 採用候補の追試");
    Console.WriteLine();
    Console.WriteLine($"seed {Base}..{Base + Seeds - 1} の {Seeds} 試行。選定に使った seed 0..199 とは重ならない。");
    Console.WriteLine($"差が {Threshold:F0}pt 未満なら誤差とみなして据え置く。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 旧配置 | 候補 | 差 | 採否 |" + string.Concat(stages.Select((_, i) => $" 第{i + 1}波差 |")));
    Console.WriteLine("|---|--:|--:|--:|:-:|" + string.Concat(stages.Select(_ => "---:|")));

    foreach (var (name, oldF, newF) in picks)
    {
        double[] o = stages.Select(st => Rate(oldF, st.Enemy)).ToArray();
        double[] n = stages.Select(st => Rate(newF, st.Enemy)).ToArray();
        double gap = n.Average() - o.Average();
        Console.WriteLine($"| {name} | {o.Average():F1}% | {n.Average():F1}% | {gap:+0.0;-0.0}pt | {(gap >= Threshold ? "採用" : "据え置き")} |"
            + string.Concat(Enumerable.Range(0, stages.Count).Select(i => $" {n[i] - o[i]:+0.0;-0.0} |")));
        Console.Out.Flush();
    }
    return;

    double Rate(Formation f, Formation enemy)
    {
        int wins = 0;
        for (int seed = Base; seed < Base + Seeds; seed++)
            if (BattleEngine.Run(f, enemy, seed, verbose: false).PlayerWon) wins++;
        return wins * 100.0 / Seeds;
    }
}

// 隣接という条件がこの盤面で「席の値段」になっているかを調べる（第45期）。
//
// **新しい機構は1つも作らない。** `Traits.cs` / `UnitCatalog.cs` / `Stages` /
// `CompareBuilds()` は1行も触らず、既存の `reseat` の探索ロジックを写して
// **駒ごとに「どの席に置かれたか」**を集計するだけ。
//
// **既存の `seats` / `reseat` は書き換えていない**（別モードにしてある）。
//
// 問いは3つ。
//   Q1 席に値段が付いているか（`幅` ＝ 1位と最下位の勝率差）
//   Q2 **その値段は編成によって変わるか**（`席の分散`。これが本題）
//   Q3 原因は特性の設計か、盤面の形状か（**隣接も列も読まない駒**を対照に置いて切り分ける）
//
// サブモード:
//   seats2 degree           次数分布（Phase 0-2）と 角の対称性（Phase 0-3）。探索しない
//   seats2 list             対象・対照の選定（Phase 0-1 / 0-4）。戦闘を1回も回さない
//   seats2 [skip] [take]    探索本体。**行単位で切り出せる**（長時間ジョブなので分割する）
//
// **`reseat` との差は1点だけ**——検証プール（上位20 + 狙い上位10 + 現行）に
// **粗探索の最下位を1つ足してある**。`幅` を「120通りの1位と最下位の差」として
// 200 seed で測るために要る（`reseat` のプールは上位に偏っているので、
// そのままだと幅が過小になる）。**探索・検証の seed 帯とプールの作り方は写しのまま。**
if (focusId == "seats2")
{
    var s2Builds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> s2Stages = EnemyCatalog.Stages;
    const int S2Scan = 50;     // 粗探索。reseat / layout と揃える
    const int S2Verify = 200;  // 測り直し。compare と揃える
    const int S2TopOverall = 20;
    const int S2TopConstrained = 10;

    string s2Mode = args.Length > 2 ? args[2] : "";

    // 編成5枠だけを見た隣接次数（召喚枠を除く）。角4つが2・中央が4。
    static int S2Degree(int slot)
    {
        int n = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
            if (FormationRules.AreAdjacent(slot, i)) n++;
        return n;
    }

    // 鏡像写像。0↔1 / 3↔4（中央は不動点）。召喚枠は編成には現れない。
    static int S2Mirror(int slot) => slot switch { 0 => 1, 1 => 0, 3 => 4, 4 => 3, _ => slot };

    // --- 隣接を読む特性 / 列を読む特性 ------------------------------------------------------
    // **grep から機械的に作った一覧**（Traits.cs と BattleEngine.cs を AreAdjacent /
    // AreSameRowPair / IsLanePredecessor / DepthOf / Row. / SlotsOfRow で走査）。
    // 瘴気（Miasma）は**入っていない**——味方漏れは隣接ではなく味方全体（Traits.cs:2024）。
    // 呪詛漏れ・萎縮・号令も入っていない——あれらは味方全体に配り、
    // **隣接を読むのは拡散側（ガルドの Stoic / BattleContext.SupportTargets）**。
    var s2Adj = new HashSet<TraitId>
    {
        TraitId.Splash,      // 巻き込み（ボルグ）: コスト
        TraitId.Cinder,      // 火の粉（ボルグ）: コスト
        TraitId.Sacrifice,   // 生贄（リィカ）: コスト
        TraitId.Venom,       // 毒漏れ（スィド）: コスト
        TraitId.Thorns,      // 棘（カド）: コスト＋利得
        TraitId.ThornGuard,  // 棘守り（カド）: 利得。AreSameRowPair / IsLanePredecessor
        TraitId.Marker,      // 囃し立て（ヒサ）: 利得
        TraitId.Shove,       // 突き返し（ハネ）: コスト
        TraitId.Bear,        // 集約（ウケ）: 利得。判定は BattleEngine.Dull
        TraitId.Relay,       // 渡し（ワタ）: 利得。判定は BattleEngine.Dull
        TraitId.Overbear,    // 驕り（オゴ）: コスト＋利得。**ロスターで2枚目の非単調な読み手**（第46期）
                             // ——削る量は隣接数に比例（単調なコスト）だが、条件は隣接全員への AND
                             // なので「隣が誰か」で成立時刻が変わる
        TraitId.Goad,        // 駆り立て（カリ）: コスト＋利得。**ロスターで3枚目の非単調な読み手**（第52期）
                             // ——隣接する生存味方の CurrentAttack の最大を取り、**その1体に効果を当てる**
                             // （囃し立てと同型で、驕りのように1つのスカラーへ潰さない）
        TraitId.Stoic,       // 支援拒否（ガルド）: 中立。SupportTargets が隣へ流す
        TraitId.Loose,       // 散開（ササ）: 利得。**第106期に被弾クロックの弾きが付いた**（−35% の側は据え置き）
    };

    // 列（Row / DepthOf）を読む、あるいは席を書き換える特性。**対象でも対照でもない**
    // ——席に依存はするが「隣接」ではないので、混ぜると Q3 の切り分けが壊れる。
    var s2Row = new HashSet<TraitId>
    {
        TraitId.Coward, TraitId.Sniper,   // 臆病・後衛特化（セロ）
        TraitId.Colossus,                 // 巨躯（ゴルム）: DepthOf
        TraitId.Guardian,                 // 庇う（ガルド）: Row.Front
        TraitId.RearGuard,                // 後備え（セッキ）: Row.Back
        TraitId.Displaced,                // 軋み（ヨミ）: DepthOf
        TraitId.Shuffler,                 // 喧噪（バサ）: 席を書き換える
    };

    string S2Class(UnitDef d) =>
        d.Traits.Any(s2Adj.Contains) ? "隣接"
        : d.Traits.Any(s2Row.Contains) ? "列"
        : "無";

    // --- degree: Phase 0-2 と 0-3 -----------------------------------------------------------
    if (s2Mode == "degree")
    {
        Console.WriteLine("# 席の値段（seats2 degree）—— 次数分布と角の対称性");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 seats2 degree` の出力。");
        Console.WriteLine("**docs/ には置かない**（標準出力で読むだけ）。");
        Console.WriteLine();
        Console.WriteLine("## 1. 次数分布（`FormationRules.AreAdjacent` の表から。戦闘は回さない）");
        Console.WriteLine();
        Console.WriteLine("| 席 | 編成5枠のみ | 召喚枠込み |");
        Console.WriteLine("|---|--:|--:|");
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
        {
            int all = 0;
            for (int j = 0; j < FormationRules.TotalSlots; j++)
                if (FormationRules.AreAdjacent(i, j)) all++;
            Console.WriteLine($"| {i} {FormationRules.SeatNames[i]} | {S2Degree(i)} | {all} |");
        }
        Console.WriteLine();
        var degs = Enumerable.Range(0, FormationRules.PlayableSlotCount).Select(S2Degree).Distinct().OrderBy(x => x);
        Console.WriteLine($"**次数の取りうる値: {{{string.Join(", ", degs)}}}（{degs.Count()} 種類）**");
        Console.WriteLine();

        Console.WriteLine("## 2. 角の対称性（現行の配置 vs その鏡像）");
        Console.WriteLine();
        Console.WriteLine("鏡像写像は 0↔1 / 3↔4（中央は不動点）。**盤面のグラフとしては自己同型**なので、");
        Console.WriteLine("エンジンが完全に対称なら差は 0 になるはず。**タイブレークは乱数化済み**だが、");
        Console.WriteLine("README「まだ残っている非対称（未解決）」のとおり完全な同値ではない。");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{S2Verify - 1}（選定帯）と seed 200..599（別帯）の両方で測る。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 移動する駒 | 平均差(0..199) | 最大波差 | 平均差(200..599) | 最大波差 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|");

        var mvIds = new HashSet<TraitId> { TraitId.Coward, TraitId.Shuffler, TraitId.Displaced, TraitId.ThornGuard };
        double sumA = 0, sumB = 0, maxA = 0, maxB = 0;
        double sumMv = 0, sumNo = 0; int nMv = 0, nNo = 0;

        foreach (var b in s2Builds)
        {
            var mir = new Formation();
            foreach ((int slot, UnitDef d) in b.F.Occupied()) mir[S2Mirror(slot)] = d;
            bool moves = b.F.Occupied().Any(o => o.Def.Traits.Any(mvIds.Contains));

            double[] Run(int seed0, int n)
            {
                var diff = new double[s2Stages.Count];
                for (int w = 0; w < s2Stages.Count; w++)
                {
                    int a = 0, c = 0;
                    for (int seed = seed0; seed < seed0 + n; seed++)
                    {
                        if (BattleEngine.Run(b.F, s2Stages[w].Enemy, seed, false).PlayerWon) a++;
                        if (BattleEngine.Run(mir, s2Stages[w].Enemy, seed, false).PlayerWon) c++;
                    }
                    diff[w] = Math.Abs(a - c) * 100.0 / n;
                }
                return diff;
            }

            double[] dA = Run(0, S2Verify), dB = Run(200, 400);
            double avgA = dA.Average(), avgB = dB.Average();
            sumA += avgA; sumB += avgB;
            if (dA.Max() > maxA) maxA = dA.Max();
            if (dB.Max() > maxB) maxB = dB.Max();
            if (moves) { sumMv += avgA; nMv++; } else { sumNo += avgA; nNo++; }

            Console.WriteLine($"| {b.Name} | {(moves ? "○" : "")} | {avgA:0.00} | {dA.Max():0.0} "
                + $"| {avgB:0.00} | {dB.Max():0.0} |");
            Console.Out.Flush();
        }
        int n2 = s2Builds.Length;
        Console.WriteLine();
        Console.WriteLine($"**全 {n2} 行の平均差: {sumA / n2:0.00}pt（0..199） / {sumB / n2:0.00}pt（200..599）**"
            + $"。波ごとの最大差 {maxA:0.0}pt / {maxB:0.0}pt。");
        Console.WriteLine($"席を動かす駒あり **{nMv} 行**: {sumMv / Math.Max(1, nMv):0.00}pt ／ "
            + $"なし **{nNo} 行**: {sumNo / Math.Max(1, nNo):0.00}pt。");
        Console.WriteLine();
        return;
    }

    // --- list: Phase 0-1 / 0-4 の選定 -------------------------------------------------------
    if (s2Mode == "list")
    {
        Console.WriteLine("# 席の値段（seats2 list）—— 対象と対照の選定");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 seats2 list` の出力。**戦闘は1回も回さない。**");
        Console.WriteLine();
        Console.WriteLine("`隣接` = `AreAdjacent` / `AreSameRowPair` / `IsLanePredecessor` を読む特性を持つ駒。");
        Console.WriteLine("`列` = `Row` / `DepthOf` を読む、または席を書き換える駒（**対象でも対照でもない**）。");
        Console.WriteLine("`無` = どちらも読まない駒（**対照の母集団**）。");
        Console.WriteLine();
        Console.WriteLine($"**2行以上に出ていない駒は調査から外す**（同じ駒が複数の編成に出ていることが Q2 の前提）。");
        Console.WriteLine();

        var rowsOf = new Dictionary<string, List<string>>();
        var defOf = new Dictionary<string, UnitDef>();
        foreach (var b in s2Builds)
            foreach ((int _, UnitDef d) in b.F.Occupied())
            {
                if (!rowsOf.TryGetValue(d.Name, out var l)) rowsOf[d.Name] = l = new List<string>();
                l.Add(b.Name); defOf[d.Name] = d;
            }

        foreach (string cls in new[] { "隣接", "列", "無" })
        {
            var members = rowsOf.Keys.Where(k => S2Class(defOf[k]) == cls)
                .OrderByDescending(k => rowsOf[k].Count).ThenBy(k => k).ToList();
            Console.WriteLine($"## 分類 `{cls}`（{members.Count} 枚）");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 行数 | 調査対象 | 特性 |");
            Console.WriteLine("|---|--:|:-:|---|");
            foreach (string k in members)
                Console.WriteLine($"| {k} | {rowsOf[k].Count} | {(rowsOf[k].Count >= 2 ? "○" : "**外す**")} "
                    + $"| {string.Join(" / ", defOf[k].Traits)} |");
            Console.WriteLine();
        }

        // ロスターにいるが compare に1行も出ていない駒（＝行数0）
        var absent = UnitCatalog.All.Where(d => !rowsOf.ContainsKey(d.Name)).ToList();
        Console.WriteLine($"## `CompareBuilds()` に1行も出ていない駒（{absent.Count} 枚）");
        Console.WriteLine();
        foreach (var d in absent)
            Console.WriteLine($"- {d.Name}（{S2Class(d)}） — {string.Join(" / ", d.Traits)}");
        Console.WriteLine();
        return;
    }

    // --- 探索本体 ---------------------------------------------------------------------------
    int s2Skip = args.Length > 2 && int.TryParse(args[2], out int sk2) ? sk2 : 0;
    int s2Take = args.Length > 3 && int.TryParse(args[3], out int tk2) ? tk2 : s2Builds.Length;
    var s2Targets = s2Builds.Skip(s2Skip).Take(s2Take).ToArray();

    Console.WriteLine($"# 席の値段（seats2 {s2Skip} {s2Take}）");
    Console.WriteLine();
    Console.WriteLine($"粗探索 seed 0..{S2Scan - 1} の全 120 通り → 検証 seed 0..{S2Verify - 1}。");
    Console.WriteLine("**検証プールは `reseat` の写し（上位20 + 狙い上位10 + 現行）に");
    Console.WriteLine("粗探索の最下位を1つ足したもの**——`幅` を 120 通りの1位と最下位の差として測るため。");
    Console.WriteLine();
    Console.WriteLine("`#ROW` / `#MEM` 行は集計用の機械可読出力（タブ区切り）。");
    Console.WriteLine();

    foreach (var (name, bf) in s2Targets)
    {
        var members = bf.Occupied().Select(x => x.Def).ToList();

        var perms = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation();
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            perms.Add(f);
        }

        var scan = new int[perms.Count];
        for (int i = 0; i < perms.Count; i++)
        {
            int wins = 0;
            foreach (EnemyCatalog.Stage st in s2Stages)
                for (int seed = 0; seed < S2Scan; seed++)
                    if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
            scan[i] = wins;
        }

        var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
        int curIdx = order.First(i => SameFormation(perms[i], bf));

        // reseat の写し + 粗探索の最下位（幅を測るために足した1件）
        var pool = order.Take(S2TopOverall)
            .Concat(order.Where(i => S2MeetsIntent(perms[i])).Take(S2TopConstrained))
            .Append(curIdx)
            .Append(order[^1])
            .Distinct().ToList();

        double S2Avg(Formation f, int seed0, int n)
        {
            double avg = 0;
            foreach (EnemyCatalog.Stage st in s2Stages)
            {
                int wins = 0;
                for (int seed = seed0; seed < seed0 + n; seed++)
                    if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                avg += wins * 100.0 / n;
            }
            return avg / s2Stages.Count;
        }

        var verified = pool.Select(i => (Idx: i, Avg: S2Avg(perms[i], 0, S2Verify)))
            .OrderByDescending(x => x.Avg).ToList();

        // **別 seed 帯での測り直し（200..599 の 400 試行）。** 上位5通りだけを測り直して
        // 1位が入れ替わるかを見る——「最適席」が seed のばらつきで決まっているなら、
        // 席の分散を数えても分散を数えたことにならない。
        var reTop = verified.Take(5).Select(v => (v.Idx, Avg: S2Avg(perms[v.Idx], 200, 400)))
            .OrderByDescending(x => x.Avg).ToList();

        double width = verified[0].Avg - verified[^1].Avg;
        int curRank = verified.FindIndex(v => v.Idx == curIdx) + 1;
        var top5 = verified.Take(5).ToList();

        Console.WriteLine($"## {name}");
        Console.WriteLine();
        Console.WriteLine($"幅 **{width:F1}pt**（1位 {verified[0].Avg:F1}% ／ 最下位 {verified[^1].Avg:F1}%）"
            + $"・現行は検証 {curRank}/{verified.Count} 位（粗 {order.IndexOf(curIdx) + 1}/120 位）"
            + $"・追試（200..599）で1位が {(reTop[0].Idx == verified[0].Idx ? "**保つ**" : "**入れ替わる**")}");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 分類 | 最適席 | 次数 | 上位5の席（中央/角） | 現行席 | 追試の最適席 |");
        Console.WriteLine("|---|:-:|---|--:|---|---|---|");

        double curAvg = verified.First(v => v.Idx == curIdx).Avg;
        double fifthAvg = verified[Math.Min(4, verified.Count - 1)].Avg;
        Console.WriteLine($"#ROW\t{name}\t{width:0.000}\t{verified[0].Avg:0.000}\t{verified[^1].Avg:0.000}"
            + $"\t{order.IndexOf(curIdx) + 1}\t{curRank}\t{verified.Count}\t{curAvg:0.000}\t{fifthAvg:0.000}"
            + $"\t{(reTop[0].Idx == verified[0].Idx ? 1 : 0)}");

        foreach (UnitDef d in members)
        {
            int bestSlot = -1;
            foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                if (ReferenceEquals(dd, d)) bestSlot = slot;
            int curSlot = -1;
            foreach ((int slot, UnitDef dd) in bf.Occupied())
                if (ReferenceEquals(dd, d)) curSlot = slot;

            int mid = 0, corner = 0;
            foreach (var v in top5)
                foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                    if (ReferenceEquals(dd, d)) { if (S2Degree(slot) == 4) mid++; else corner++; }

            // 追試（200..599）で1位になった配置でのこの駒の席
            int reSlot = -1;
            foreach ((int slot, UnitDef dd) in perms[reTop[0].Idx].Occupied())
                if (ReferenceEquals(dd, d)) reSlot = slot;

            Console.WriteLine($"| {d.Name} | {S2Class(d)} | {FormationRules.SeatNames[bestSlot]} "
                + $"| {S2Degree(bestSlot)} | 中央{mid} / 角{corner} | {FormationRules.SeatNames[curSlot]} "
                + $"| {FormationRules.SeatNames[reSlot]} |");
            Console.WriteLine($"#MEM\t{name}\t{d.Name}\t{S2Class(d)}\t{bestSlot}\t{S2Degree(bestSlot)}"
                + $"\t{mid}\t{corner}\t{curSlot}\t{width:0.000}\t{reSlot}\t{S2Degree(reSlot)}");
        }
        Console.WriteLine();
        Console.Out.Flush();
    }
    return;

    // reseat と同じ「狙い」（ガルドは前列 / セッキは後列）。**写しのまま**。
    static bool S2MeetsIntent(Formation f)
    {
        foreach (var (slot, def) in f.Occupied())
        {
            if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
            if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
        }
        return true;
    }
}

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

if (focusId == "layout")
{
    var builds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> stages = EnemyCatalog.Stages;
    const int LayoutSeeds = 50;
    const int TopN = 5;
    const int VerifySeeds = 200;   // 探索で選んだ配置を測り直すときの試行数。compare と揃える

    // 波別最良の一覧を最後にまとめて出すための控え。[編成, 波] → (現行, 最良)
    var bestByStage = new (double Cur, double Best)[builds.Length, stages.Count];

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

    // === 第140期 —— 波別最良の測り直しを、印字ループの外の平坦な並列パスに前出しした ===
    //
    // **選び方（`ranked` / `cur` / `pool` / 候補数 8 / 現行を必ず混ぜる）は1文字も変えていない。**
    // 変えたのは「どの順で `Rate` を走らせるか」だけで、`Rate` は seed 決定的な純関数なので値は同一。
    //
    // 従来はここが**印字ループの中で完全に直列**だった——61編成 × 5波 × 最大9候補 × 200 seed。
    // しかも `curRate` は `pool` にも入っているので、**同じ配置・同じ波を毎回2度測っていた**。
    // ここでは (配置, 波) をキーに**重複を潰してから**測る。
    var lyRanked = new List<int>[builds.Length];
    var lyCur = new int[builds.Length];
    for (int b = 0; b < builds.Length; b++)
    {
        int bb = b;
        lyRanked[b] = Enumerable.Range(0, jobs.Count)
            .Where(i => jobs[i].BuildIdx == bb)
            .OrderByDescending(i => results[i].Sum())
            .ThenBy(i => jobs[i].PermIdx)   // 同点は配置の辞書式で若い方（決定的タイブレーク）
            .ToList();
        lyCur[b] = lyRanked[b].FindIndex(i => SameFormation(jobs[i].F, builds[bb].F));
    }

    // 必要な (配置, 波) の組を、印字と同じ規則で先に全部並べる。
    var lyPool = new List<int>[builds.Length, stages.Count];
    var lyNeed = new List<(int JobIdx, int St)>();
    var lySeen = new HashSet<(int, int)>();
    for (int b = 0; b < builds.Length; b++)
        for (int st = 0; st < stages.Count; st++)
        {
            int sx = st;
            const int Candidates = 8;
            var pool = lyRanked[b].OrderByDescending(i => results[i][sx])
                                  .ThenBy(i => jobs[i].PermIdx)
                                  .Take(Candidates)
                                  .Append(lyRanked[b][lyCur[b]])
                                  .Distinct()
                                  .ToList();
            lyPool[b, st] = pool;
            foreach (int i in pool.Append(lyRanked[b][lyCur[b]]))
                if (lySeen.Add((i, sx))) lyNeed.Add((i, sx));
        }

    var lyRateVal = new double[lyNeed.Count];
    Parallel.For(0, lyNeed.Count, k =>
    {
        var (i, sx) = lyNeed[k];
        int wins = 0;
        for (int seed = 0; seed < VerifySeeds; seed++)
            if (BattleEngine.Run(jobs[i].F, stages[sx].Enemy, seed, verbose: false).PlayerWon) wins++;
        lyRateVal[k] = wins * 100.0 / VerifySeeds;
    });
    var lyRate = new Dictionary<(int, int), double>(lyNeed.Count);
    for (int k = 0; k < lyNeed.Count; k++) lyRate[lyNeed[k]] = lyRateVal[k];

    Console.WriteLine("# 配置探索");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 layout` の出力。");
    Console.WriteLine($"compare の各編成をメンバー固定で全配置（5体=120通り / 4体=120通り）に展開し、");
    Console.WriteLine($"全{stages.Count}ステージ × seed 0..{LayoutSeeds - 1} の平均勝率で並べた上位{TopN}件と現行配置。");
    Console.WriteLine($"検証した配置: {jobs.Count:N0} 通り（{(long)jobs.Count * stages.Count * LayoutSeeds:N0} 戦）");

    for (int b = 0; b < builds.Length; b++)
    {
        int bb = b;
        var ranked = lyRanked[b];

        Console.WriteLine();
        Console.WriteLine($"## {builds[b].Name}");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 前1/前3 | 中央 | 後1/後3 | 平均 |"
            + string.Concat(stages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|--:|---|---|---|--:|" + string.Concat(stages.Select(_ => "---:|")));
        for (int rank = 0; rank < TopN && rank < ranked.Count; rank++)
            Console.WriteLine(LayoutRow($"{rank + 1}", jobs[ranked[rank]].F, results[ranked[rank]], LayoutSeeds));

        int cur = lyCur[b];
        Console.WriteLine(LayoutRow($"現行({cur + 1}位)", jobs[ranked[cur]].F, results[ranked[cur]], LayoutSeeds));

        // 波別最良。上の表は全ステージ平均を最大化する「一つの配置」を選ぶが、
        // 実プレイは波ごとに組み替えられる。この差を出さないと、平均最良の配置が
        // たまたま苦手な波で出した勝率を「その編成の限界」と読み違える（§2-10）。
        Console.WriteLine();
        Console.WriteLine("| 波 | 前1/前3 | 中央 | 後1/後3 | 現行 | 波別最良 |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        for (int st = 0; st < stages.Count; st++)
        {
            int sx = st;

            // seed 50 の探索は 720 通りの最大を取るので、上位は運で入れ替わる。
            // 1位だけを測り直すと「波別最良が現行より低い」という原理的にありえない行が出る
            // （実測で最大 8pt の逆転が出た）。候補を上位数件に広げ、現行も必ず混ぜて、
            // seed 200 で測り直した中の最良を採る。これで表は必ず単調になる。
            // **候補の作り方は上の前計算と同一**（第140期に `lyPool` へ移しただけ）。
            var pool = lyPool[b, st];

            double curRate = lyRate[(ranked[cur], sx)];
            int best = ranked[cur];
            double bestRate = curRate;
            foreach (int i in pool)
            {
                double r = lyRate[(i, sx)];
                if (r > bestRate) { bestRate = r; best = i; }
            }
            bestByStage[bb, sx] = (curRate, bestRate);

            Formation bf = jobs[best].F;
            Console.WriteLine($"| 第{sx + 1}波 | {NameOf(bf[0])}/{NameOf(bf[1])} | {NameOf(bf[2])} "
                + $"| {NameOf(bf[3])}/{NameOf(bf[4])} | {curRate:F1}% | {bestRate:F1}% |");
        }
    }

    // 一覧。docs/balance.md（現行配置で固定）と並べて読むためのもの。
    Console.WriteLine();
    Console.WriteLine("## 波別最良の一覧");
    Console.WriteLine();
    Console.WriteLine($"各セルは「現行配置 → その波だけの最良配置」。どちらも seed 0..{VerifySeeds - 1} で測り直した値。");
    Console.WriteLine("勝率表（`compare`）は現行配置に固定した値なので、左の数字がそちらと対応する。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(stages.Select((_, i) => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(stages.Select(_ => "---:|")));
    for (int b = 0; b < builds.Length; b++)
    {
        var cells = Enumerable.Range(0, stages.Count)
            .Select(st => $" {bestByStage[b, st].Cur:F1} → {bestByStage[b, st].Best:F1} |");
        Console.WriteLine($"| {builds[b].Name} |" + string.Concat(cells));
    }
    return;

    static string NameOf(UnitDef? d) => d?.Name ?? "−";
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
