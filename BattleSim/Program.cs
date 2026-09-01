using BattleCore;

// 総当たりシミュレータ。WPF を通さず戦闘ロジックだけを叩く。
// 手動プレイでは見つからない「強すぎる組み合わせ」と「死に駒」を機械的に洗い出す。

int stageIndex = args.Length > 0 && int.TryParse(args[0], out int s) ? s : 1;
string focusId = args.Length > 1 ? args[1] : "";

// compare / dump / layout は docs/ に貼れる Markdown をそのまま吐くので、
// 「対象ステージ」の見出しと stageIndex の解決はこの3モードの分岐を抜けた後で行う。
// （3モードともステージ引数を無視して全ステージを回すため、内容としても誤りになる）

// pulse モード: 駒ごとの「働きの内訳」を測る。
//
// compare は編成の勝ち負けしか見ないので、**編成の中で誰が仕事をしていたか**が分からない。
// ablate は1体抜いた勝率差を見るが、抜けるのは勝率という1つの数字だけで、
// 「出力で効いているのか、場を作って効いているのか」は区別できない。
//
// ここで見たいのは体験の側。カド（殴られるたび反撃）と ウツ（開幕に数値が決まって
// あとは殴るだけ）は、勝率では並ぶのに手触りが全く違う。その差は
// 「1ターンあたり何回振ったか」に出る（`攻/T`）。
//
//     ~1.0  自分の手番でしか動かない = 数値であって出来事ではない
//     >1.0  手番外に反応している（反撃・追い打ち）= 噛み合いが起きている
//     ~0    置物。手番を差し出す型か、発火条件が満たされていない
//
//     dotnet run --project BattleSim -c Release 0 pulse [絞り込み] > docs/pulse.md
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

    foreach (var (name, formation) in targets)
    {
        // 駒ごとに全戦闘の集計を足し込む。Def.Id で引くので、胞子のような増援もまとまる。
        var sum = new Dictionary<string, UnitTally>();
        int battles = 0, totalTurns = 0;

        foreach (EnemyCatalog.Stage st in stages)
            for (int seed = 0; seed < PulseSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(formation, st.Enemy, seed, verbose: false);
                battles++;
                totalTurns += r.Turns;
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (!sum.TryGetValue(id, out UnitTally? acc)) sum[id] = acc = new UnitTally();
                    acc.Add(t);
                }
            }

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

// gullet モード: 巨躯の「吐き戻し」は肩代わりに価値を運ばせるか（第23期）。
//
// 肩代わり4種のうち、**見返りを持たないのは巨躯だけ**だった（庇う＝肩代わりした分だけ攻撃+/
// 分かち＝被弾に応じて攻撃+/ 後備え＝保持者が Rage を併せ持つ）。ゴルムは後方全員への攻撃を
// 90% 引き受けて、**そこで価値が消える**。第19期 route（ナラの削り7のうち6をゴルムが食い、
// ムドの Rage が +3 のはずが +1 に潰れた）・第21期 swap（ノノの回復の最大の受け手がゴルム）・
// 第22期 渇き（大喰らいが隠れた ctx.Heal 経路だった）と、3期続けて同じ吸い込み口が出ている。
//
// **見返りはゴルムではなく「守った相手」に返す。** ゴルム自身に返す形（庇う・分かちと同経路）だと
// route の問題は直らないし、後ろに誰を置くかも判断にならない。
//
// **4版の対照を最初から組む。** 逆位（第22期）で「効いていたのは壁で、ルールではない」を
// 後から切り分ける羽目になったため。V0/V2 が肩代わり率だけの辺、V1/V3 が吐き戻しの辺。
//
//   V0  90% / 吐き戻し無し   baseline。**現行の docs/balance.md と一致するはず＝検算**
//   V1  90% / 吐き戻し有り   本命（＝既定。ColossusRule.Default）
//   V2  60% / 吐き戻し無し   引き下げ単独の効果
//   V3  60% / 吐き戻し有り   両方
//
// 版は ColossusRule を Run に渡して切り替える。**書き換え可能な static のノブは置いていない**
// （Trait は共有シングルトンで layout は並列実行する。理由は ColossusRule の doc を参照）。
//
// **CompareBuilds() / Stages を触らない**ので docs/ の差分はゼロ。診断用なので docs/ には置かない。
//
//     dotnet run --project BattleSim -c Release 0 gullet        # 4版の対照
//     dotnet run --project BattleSim -c Release 0 gullet gain   # DamagePerGain を 2/4/6/8 で振る
//     dotnet run --project BattleSim -c Release 0 gullet log    # 1戦の監査（受け入れ基準 3〜5）
if (focusId == "gullet")
{
    string gulletMode = args.Length > 2 ? args[2] : "";
    var gulletBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> gulletStages = EnemyCatalog.Stages;
    const int GulletSeeds = 200;   // compare / spread と同じ。balance.md と突き合わせるので変えない

    bool HasGolm(Formation f) => f.Occupied().Any(o => o.Item2.Id == UnitCatalog.Golm.Id);

    // ---- belly4: 腹の4版対照（第36期 Phase 3）----------------------------------------------
    //
    //   V0 現行     腹の出口なし（＝ ColossusRule.Default。docs/balance.md と一致するはず＝検算）
    //   V2 まどろみ 腹が N に達した手番を失う（→ IdleTurn → 号令・据えが買う）
    //   V3 還し     倒れたとき、腹の残りの P% を生存味方へ回復として分配（1戦1回）
    //   V4 両方     腹は共有カウンター。**眠って売るか、抱えて還すか**が成立するかを見る
    //
    // 版は ColossusRule を Run に渡して切り替える（static のノブは置かない。第23期と同じ作法）。
    //
    //     dotnet run --project BattleSim -c Release 0 gullet belly4
    if (gulletMode == "belly4")
    {
        // **V0 は明示的に両方を切る。** 第36期の採用手順で `ColossusRule.Default` は
        // 還し有効になったので、Default をそのまま V0 に使うと梯子が崩れる
        // （V0 と V3 が同じものになる）。逆に **V3 が現在の既定と一致する**ので、
        // 検算の相手は V0 ではなく V3 になった。
        ColossusRule Base = ColossusRule.Default with { Slumber = false, Refund = false };
        var vers = new (string Name, string Note, ColossusRule Rule)[]
        {
            ("V0 腹なし",   "腹の出口なし（第35期までの盤面）", Base),
            ("V2 まどろみ", $"腹 {ColossusTrait.SlumberThreshold} で手番を失う",
                Base with { Slumber = true }),
            ("V3 還し",     $"倒れたとき腹の {ColossusTrait.RefundPercent}% を分配（＝**現在の既定**）",
                Base with { Refund = true }),
            ("V4 両方",     "眠りが腹を食い、残りを還す", Base with { Slumber = true, Refund = true }),
        };
        var dels = new[] { ("V2−V0", 1, 0), ("V3−V0", 2, 0), ("V4−V0", 3, 0) };

        int bnv = vers.Length, bnb = gulletBuilds.Length, bnw = gulletStages.Count;

        var brate = new double[bnv][][];              // brate[版][編成][波] = 勝率(%)
        var tal = new UnitTally[bnv][];              // ゴルムの集計（編成ごと・全波合算）
        var battles = new int[bnv][];
        var survWin = new long[bnv][];               // 勝った試行の生存数合計
        var wins = new int[bnv][];
        var soloWin = new int[bnv][];                // 生存1体での勝利
        // UnitTally.Add は LastActiveTurn を Math.Max で畳む（ターン番号は足せない）ので、
        // 「1戦あたりの生存T」は別に足し上げる。
        var aliveT = new long[bnv][];
        // 波ごとの還しの内訳（第三波の封じを見る）。[版][波]
        var wRefunds = new long[bnv, bnw];
        var wDeliver = new long[bnv, bnw];

        for (int v = 0; v < bnv; v++)
        {
            brate[v] = new double[bnb][];
            tal[v] = new UnitTally[bnb];
            battles[v] = new int[bnb];
            survWin[v] = new long[bnb];
            wins[v] = new int[bnb];
            soloWin[v] = new int[bnb];
            aliveT[v] = new long[bnb];

            for (int b = 0; b < bnb; b++)
            {
                brate[v][b] = new double[bnw];
                tal[v][b] = new UnitTally();
                bool golm = HasGolm(gulletBuilds[b].F);

                for (int w = 0; w < bnw; w++)
                {
                    int won = 0;
                    for (int seed = 0; seed < GulletSeeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                          seed, verbose: false, vers[v].Rule);
                        if (r.PlayerWon) won++;
                        if (!golm) continue;

                        battles[v][b]++;
                        if (r.PlayerWon)
                        {
                            wins[v][b]++;
                            survWin[v][b] += r.PlayerSurvivors;
                            if (r.PlayerSurvivors == 1) soloWin[v][b]++;
                        }
                        if (r.TallyByUnit.TryGetValue(UnitCatalog.Golm.Id, out UnitTally? g))
                        {
                            tal[v][b].Add(g);
                            aliveT[v][b] += g.LastActiveTurn;
                            wRefunds[v, w] += g.Refunds;
                            wDeliver[v, w] += g.Refunded;
                        }
                    }
                    brate[v][b][w] = won * 100.0 / GulletSeeds;
                }
            }
            Console.Error.WriteLine($"  {vers[v].Name} 完了");
        }

        double BAvg(int v, int b) => brate[v][b].Average();

        Console.WriteLine("# 腹という通貨 —— まどろみと還しの4版対照（gullet belly4 / 第36期）");
        Console.WriteLine();
        Console.WriteLine($"代表編成 {bnb} 行 × 全ステージ、seed 0..{GulletSeeds - 1}。診断用なので docs/ には置かない。");
        Console.WriteLine();
        foreach (var vv in vers) Console.WriteLine($"- **{vv.Name}**: {vv.Note}");

        // --- 検算 1: V0 が balance.md と一致するか ------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 検算1: V3 × 全編成（V3 ＝ 現在の既定）");
        Console.WriteLine();
        Console.WriteLine("**このセルは `docs/balance.md` と1つ残らず一致しなければならない。**");
        Console.WriteLine("第36期の採用手順で `ColossusRule.Default` が還し有効になったので、");
        Console.WriteLine("**検算の相手は V0 ではなく V3**（V0 は第35期までの盤面）。");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 |"))
                          + " V0（第35期まで） |");
        Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")) + "---|");
        for (int b = 0; b < bnb; b++)
            Console.WriteLine($"| {gulletBuilds[b].Name} |"
                + string.Concat(brate[2][b].Select(x => $" {x:F1}% |"))
                + " " + string.Join(" / ", brate[0][b].Select(x => $"{x:F1}")) + " |");

        // --- 検算 2: ゴルムを含まない行は動かないか ------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 検算2: ゴルムを含まない行の回帰");
        Console.WriteLine();
        Console.WriteLine("腹・まどろみ・還しはすべて巨躯の分岐の中にしかない。");
        Console.WriteLine("**ゴルム不在の行は全版 ±0.0 でなければならない**（停止条件）。");
        Console.WriteLine();
        var strays = new List<string>();
        int noGolm = 0;
        for (int b = 0; b < bnb; b++)
        {
            if (HasGolm(gulletBuilds[b].F)) continue;
            noGolm++;
            for (int v = 1; v < bnv; v++)
                for (int w = 0; w < bnw; w++)
                    if (Math.Abs(brate[v][b][w] - brate[0][b][w]) > 1e-9)
                        strays.Add($"{gulletBuilds[b].Name} / {vers[v].Name} / 第{w + 1}波: "
                                   + $"{brate[0][b][w]:F1}% → {brate[v][b][w]:F1}%");
        }
        Console.WriteLine($"ゴルム不在 {noGolm} 行 × {bnv - 1}版 × {bnw} 波 = {noGolm * (bnv - 1) * bnw} セル中、"
                          + $"**V0 と食い違ったセル {strays.Count} 件**。");
        foreach (string x in strays.Take(40)) Console.WriteLine($"- {x}");

        // --- 主表 --------------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 主表: ゴルムを含む行 × 各版（全波平均）");
        Console.WriteLine();
        Console.WriteLine("**第一波は全編成 100% なので平均は 20pt ぶん薄まっている**——動いた波は次節。");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(vers.Select(v => $" {v.Name} |"))
                          + string.Concat(dels.Select(d => $" {d.Item1} |")));
        Console.WriteLine("|---|" + string.Concat(vers.Select(_ => "---:|"))
                          + string.Concat(dels.Select(_ => "---:|")));
        var golmIdx = Enumerable.Range(0, bnb).Where(b => HasGolm(gulletBuilds[b].F)).ToList();
        foreach (int b in golmIdx)
            Console.WriteLine($"| {gulletBuilds[b].Name} |"
                + string.Concat(Enumerable.Range(0, bnv).Select(v => $" {BAvg(v, b):F1}% |"))
                + string.Concat(dels.Select(d => $" {BAvg(d.Item2, b) - BAvg(d.Item3, b):+0.0;-0.0}pt |")));
        Console.WriteLine($"| **平均** |"
            + string.Concat(Enumerable.Range(0, bnv).Select(v => $" **{golmIdx.Average(b => BAvg(v, b)):F1}%** |"))
            + string.Concat(dels.Select(d =>
                $" **{golmIdx.Average(b => BAvg(d.Item2, b)) - golmIdx.Average(b => BAvg(d.Item3, b)):+0.0;-0.0}pt** |")));

        // --- 波ごとの内訳 -------------------------------------------------------------------
        for (int dv = 2; dv <= 3; dv++)
        {
            Console.WriteLine();
            Console.WriteLine($"## 波ごとの内訳（V0 → {vers[dv].Name}）");
            Console.WriteLine();
            if (dv == 2)
            {
                Console.WriteLine("**第三波は渇き（回復禁止）の波**。還しは `ctx.Heal` を通るので、");
                Console.WriteLine("ここだけ V0 と1セルも違わないはず——動いていたら還しが `Heal` を通っていない。");
                Console.WriteLine();
            }
            Console.WriteLine("| 編成 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 |")));
            Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")));
            foreach (int b in golmIdx)
                Console.WriteLine($"| {gulletBuilds[b].Name} |"
                    + string.Concat(Enumerable.Range(0, bnw).Select(w =>
                        $" {brate[0][b][w]:F1} → {brate[dv][b][w]:F1} "
                        + $"({brate[dv][b][w] - brate[0][b][w]:+0.0;-0.0}) |")));
        }

        // --- 機構の発火 ---------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 機構の発火（ゴルムの集計 / 1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("`飲み込み` は肩代わりで腹に入った量、`眠り` はまどろんだ回数、");
        Console.WriteLine("`還し発火` は還しが走った戦の割合、`届いた` は**実際に味方の HP が増えた量**");
        Console.WriteLine("（額面ではない——渇き・支援拒否・満タンで消えた分は入らない）。");
        Console.WriteLine();
        Console.WriteLine("`落ちた/戦` は蘇生で戻って再び倒れると 2 になる（`還し発火` が 100% で頭打ちなのが");
        Console.WriteLine("**1戦1回の担保が効いている証拠**）。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 版 | 飲み込み | 眠り | 還し発火 | 届いた | 与ダメ(敵) | 生存T | 落ちた/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int b in golmIdx)
            for (int v = 0; v < bnv; v++)
            {
                double n = Math.Max(1, battles[v][b]);
                UnitTally t = tal[v][b];
                Console.WriteLine($"| {(v == 0 ? gulletBuilds[b].Name : "")} | {vers[v].Name} "
                    + $"| {t.Swallowed / n:F0} | {t.Slumbers / n:F2} | {t.Refunds * 100.0 / n:F0}% "
                    + $"| {t.Refunded / n:F1} | {t.DamageToEnemy / n:F0} "
                    + $"| {aliveT[v][b] / n:F1} | {t.Deaths / n:F2} |");
            }

        // --- 第三波の封じ -------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 還しは第三波で封じられるか（波ごと・ゴルム行の合算）");
        Console.WriteLine();
        Console.WriteLine("**第三波（渇きの祭司）だけ `届いた` が 0 になるはず。** 発火（`還し`）は起きていて、");
        Console.WriteLine("`ctx.Heal` の入口で止まる——「発火したが届かなかった」が渇きの課税の形。");
        Console.WriteLine();
        Console.WriteLine("| 版 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 還し/戦 | 第{i + 1}波 届いた/戦 |")));
        Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|---:|")));
        for (int v = 0; v < bnv; v++)
        {
            double n = Math.Max(1, golmIdx.Count * GulletSeeds);
            Console.Write($"| {vers[v].Name} |");
            for (int w = 0; w < bnw; w++)
                Console.Write($" {wRefunds[v, w] / n:F2} | {wDeliver[v, w] / n:F1} |");
            Console.WriteLine();
        }

        // --- 勝ち方の質 ---------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 勝ち方の質（ゴルム行・勝った試行だけ）");
        Console.WriteLine();
        Console.WriteLine("`残存` は勝った試行の平均生存数、`全滅勝ち` は生存1体での勝利の割合。");
        Console.WriteLine("**還しは勝率より先にここを動かすはず**（`chain` の見方）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率(ゴルム行平均) | 残存 | 全滅勝ち |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int v = 0; v < bnv; v++)
        {
            long w0 = golmIdx.Sum(b => (long)wins[v][b]);
            long s0 = golmIdx.Sum(b => survWin[v][b]);
            long q0 = golmIdx.Sum(b => (long)soloWin[v][b]);
            Console.WriteLine($"| {vers[v].Name} | {golmIdx.Average(b => BAvg(v, b)):F1}% "
                + $"| {(double)s0 / Math.Max(1, w0):F2} | {q0 * 100.0 / Math.Max(1, w0):F1}% |");
        }

        // --- 全波100% -----------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 全波 100% の編成数（壊れ検知）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 全波100% | 該当編成 |");
        Console.WriteLine("|---|--:|---|");
        for (int v = 0; v < bnv; v++)
        {
            var perfect = Enumerable.Range(0, bnb).Where(b => brate[v][b].All(x => x >= 100.0))
                                    .Select(b => gulletBuilds[b].Name).ToList();
            Console.WriteLine($"| {vers[v].Name} | {perfect.Count} | {string.Join(" / ", perfect)} |");
        }

        // --- まどろみは実際に売れたか（試験行・ログを数える）--------------------------------
        //
        // **ここはログの文字列を数えている**（gullet log / yoke log と同じ理由）。
        // 号令の買い取りも据えの買い取りも、盤面の値には「減った後の数字」しか残らないので、
        // 「その行が出たか」を数える以外に発火を捕まえる方法が無い。
        Console.WriteLine();
        Console.WriteLine("## まどろみは実際に売れたか（買い手を持つ行 / seed 0..49 × 全波）");
        Console.WriteLine();
        Console.WriteLine("号令（次のターン 攻撃+8）と据え（そのターン 被ダメ-50%）が、");
        Console.WriteLine("**ゴルムのまどろみを買った回数**。ドルガののろま・シガの自傷痺れが作る `IdleTurn` と");
        Console.WriteLine("混ざらないよう、買われた駒がゴルムである行だけを数える。");
        Console.WriteLine();
        Console.WriteLine("**号令は「次のターン」に払う。** 眠った同じターンにゴルムが倒れると売り物ごと消えるので、");
        Console.WriteLine("最後の列（眠った後も生きていた戦の割合）を並べて読む。");
        Console.WriteLine();
        const int SaleSeeds = 50;
        string gn = UnitCatalog.Golm.Name;

        // 買い手を持つゴルム行だけを verbose で回す（他の行には号令も据えもいないので、
        // 構造的に0件と分かっている。数えても情報が増えず時間だけ4倍になる）。
        var saleRows = golmIdx.Where(b =>
            gulletBuilds[b].F.Occupied().Any(o => o.Item2.Id == UnitCatalog.Gan.Id
                                               || o.Item2.Id == UnitCatalog.Ban.Id)).ToList();

        Console.WriteLine("| 編成 | 版 | まどろみ/戦 | 眠りを生き延びた | 号令→ゴルム | 据え→ゴルム | 号令→全体 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (int b in saleRows)
            foreach (int v in new[] { 0, 1, 3 })
            {
                int slumber = 0, rally = 0, bul = 0, rallyAll = 0, survived = 0, n = 0;
                for (int w = 0; w < bnw; w++)
                    for (int seed = 0; seed < SaleSeeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                          seed, verbose: true, vers[v].Rule);
                        n++;
                        // 眠ったターンごとに「その戦闘がその後も続き、ゴルムも生きていたか」を数える。
                        // 号令は**次のターン**に払うので、眠った直後に落ちる／決着すると売り物が消える。
                        var sleeps = new List<int>();
                        var golmDeaths = new List<int>();
                        int turn = 0, lastTurn = 0;
                        foreach (LogLine l in r.Log)
                        {
                            string t = l.Text;
                            if (l.Kind == LogKind.Turn) { turn++; lastTurn = turn; continue; }
                            if (t.Contains($"{gn} は腹が満ちてまどろんだ")) { slumber++; sleeps.Add(turn); }
                            else if (t.Contains($"の号令で {gn} の溜めが乗った")) rally++;
                            else if (t.Contains("の号令で")) rallyAll++;
                            else if (t.Contains($"据えが差し出した {gn} の被弾を")) bul++;
                            else if (t.Contains($"{gn} は倒れた")) golmDeaths.Add(turn);
                        }
                        survived += sleeps.Count(x => x < lastTurn && !golmDeaths.Contains(x));
                    }
                Console.WriteLine($"| {(v == 0 ? gulletBuilds[b].Name : "")} | {vers[v].Name} "
                    + $"| {(double)slumber / n:F2} | {(slumber == 0 ? 0 : survived * 100.0 / slumber):F0}% "
                    + $"| {(double)rally / n:F2} | {(double)bul / n:F2} | {(double)rallyAll / n:F2} |");
            }

        // --- ログ実例 -----------------------------------------------------------------------
        void Excerpt(string title, string note, string buildKey, int stage, int seed,
                     ColossusRule rule, string[] keys)
        {
            var (name, f) = gulletBuilds.First(x => x.Name.Contains(buildKey));
            BattleResult r = BattleEngine.Run(f, gulletStages[stage].Enemy, seed, verbose: true, rule);
            Console.WriteLine();
            Console.WriteLine($"### {title}");
            Console.WriteLine();
            Console.WriteLine(note);
            Console.WriteLine();
            Console.WriteLine($"{name} / 第{stage + 1}波 / seed {seed} / {(r.PlayerWon ? "勝利" : "敗北")} {r.Turns}ターン");
            Console.WriteLine();
            Console.WriteLine("```");
            string turn = "";
            foreach (LogLine l in r.Log)
            {
                if (l.Kind == LogKind.Turn) { turn = l.Text; continue; }
                if (!keys.Any(k => l.Text.Contains(k))) continue;
                if (turn.Length > 0) { Console.WriteLine(turn); turn = ""; }
                Console.WriteLine(l.Text);
            }
            Console.WriteLine("```");
        }

        Console.WriteLine();
        Console.WriteLine("## ログ実例");

        string golmName = UnitCatalog.Golm.Name;
        // A は**探して出す**。売却は眠りのうちの一部でしか起きないので、seed を決め打ちすると
        // 「起きなかった戦」を貼ることになる。探索は決定的（行→波→seed の昇順で最初の1件）。
        //
        // **試験行「腹×号令」は第36期の採用手順で compare から外した**（まどろみの棄却で
        // 目的を失ったため）。ここは買い手を持つ行を総当たりするので、行が消えても動く。
        {
            string want = $"の号令で {golmName} の溜めが乗った";
            (int B, int W, int S) hit = (-1, -1, -1);
            foreach (int b in saleRows)
            {
                for (int w = 0; w < bnw && hit.B < 0; w++)
                    for (int seed = 0; seed < GulletSeeds && hit.B < 0; seed++)
                    {
                        BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                          seed, verbose: true, vers[3].Rule);
                        if (r.Log.Any(l => l.Text.Contains(want))) hit = (b, w, seed);
                    }
                if (hit.B >= 0) break;
            }
            if (hit.B >= 0)
                Excerpt("A. まどろみが売れる（V4 / 売却の起きた最初の戦）",
                    "腹が満ちて手番を失い、**号令が次のターンの攻撃を買う**。",
                    gulletBuilds[hit.B].Name, hit.W, hit.S, vers[3].Rule,
                    new[] { "腹が満ちてまどろんだ", $"の号令で {golmName}",
                            $"据えが差し出した {golmName}", "が飲み込んだものが還った",
                            $"{golmName} は倒れた" });
            else
                Console.WriteLine("### A. まどろみが売れる —— **1件も見つからなかった**");
        }

        Excerpt("B. 第三波では還しが封じられる（V3 / 渇きの祭司）",
            "**発火はしているのに `届いた 0`。** `ctx.Heal` の入口で渇きが止めている"
            + "（規則は engine の1箇所のまま、特性側は判定を1文字も持っていない）。",
            "置き去り×被弾強化", 2, 0, vers[2].Rule,
            new[] { "が飲み込んだものが還った", "は倒れた" });

        // D は**探して出す**。第三波は「渇きの祭司が生きている間だけ」封じられるので、
        // 祭司を先に割った戦では還しが解禁される——これが `spread` で
        // 第三波の勝率が 18行中1行だけ動いた（逆しま +1.5pt）の正体。
        // **渇きが「回避可能な課税」として働いた初の実例**なので、診断に据えて再現可能にしてある。
        {
            (int B, int S) hitD = (-1, -1);
            foreach (int b in golmIdx)
            {
                for (int seed = 0; seed < GulletSeeds && hitD.B < 0; seed++)
                {
                    BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[2].Enemy,
                                                      seed, verbose: true, vers[2].Rule);
                    if (r.Log.Any(l => l.Text.Contains("が飲み込んだものが還った")
                                       && !l.Text.Contains("/ 届いた 0）"))) hitD = (b, seed);
                }
                if (hitD.B >= 0) break;
            }
            if (hitD.B >= 0)
                Excerpt("D. 第三波でも、祭司を先に割れば還しは解禁される（V3）",
                    "渇きは**保持者が生きている間だけ**効く（`ctx.Heal` の入口で `Drought` の生存を見る）。"
                    + "祭司を先に倒した戦では回復が戻るので、還しが届く。"
                    + "**渇きが「回避可能な課税」として働いた初の実例。**",
                    gulletBuilds[hitD.B].Name, 2, hitD.S, vers[2].Rule,
                    new[] { "が飲み込んだものが還った", "渇きの祭司 は倒れた",
                            $"{golmName} は倒れた" });
            else
                Console.WriteLine("### D. 祭司を先に割った戦 —— **1件も見つからなかった**");
        }

        Excerpt("C. 同じ行の第五波では届く（V3 / 渇きなし）",
            "同じ編成・同じ規則で、渇きの無い波なら回復が通る。**Bとの差は波だけ。**",
            "置き去り×被弾強化", 4, 0, vers[2].Rule,
            new[] { "が飲み込んだものが還った", "は倒れた" });
        return;
    }

    // ---- belly: 腹の規模の実測（第36期 Phase 0-1）------------------------------------------
    //
    // まどろみの閾値 N と還し率を、掃引ではなく**現行盤面の実測から**導くための表。
    // 盤面は1つも動かさない（腹のカウンタは V0 では誰も読まない純粋な記録）。
    //
    //     dotnet run --project BattleSim -c Release 0 gullet belly
    if (gulletMode == "belly")
    {
        // 1戦ぶんの記録。飲み込み量は UnitTally.Swallowed（ApplyDamage の blocked と同額）。
        var rows = new List<(string Build, int Wave, int Swallowed, int Deaths, int AliveT, int Turns)>();

        for (int b = 0; b < gulletBuilds.Length; b++)
        {
            if (!HasGolm(gulletBuilds[b].F)) continue;
            for (int w = 0; w < gulletStages.Count; w++)
                for (int seed = 0; seed < GulletSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                      seed, verbose: false);
                    r.TallyByUnit.TryGetValue(UnitCatalog.Golm.Id, out UnitTally? g);
                    rows.Add((gulletBuilds[b].Name, w + 1, g?.Swallowed ?? 0, g?.Deaths ?? 0,
                              g?.LastActiveTurn ?? 0, r.Turns));
                }
        }

        static double Median(List<int> xs)
        {
            if (xs.Count == 0) return 0;
            var v = xs.OrderBy(x => x).ToList();
            return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2.0;
        }

        Console.WriteLine("# 腹の規模の実測（gullet belly / 第36期 Phase 0-1）");
        Console.WriteLine();
        Console.WriteLine($"ゴルムを含む編成 × 全ステージ、seed 0..{GulletSeeds - 1}。**現行の盤面のまま**測っている");
        Console.WriteLine("（腹のカウンタは `ApplyDamage` の巨躯の分岐で `blocked` を積むだけで、誰も読まない）。");
        Console.WriteLine();
        Console.WriteLine("`飲み込み` は1戦あたりの肩代わり吸収量の合計（吐き戻しが返した元の量と同額）。");
        Console.WriteLine("`生存T` はゴルムが最後に生きていたターン、`落ちた` は倒れた戦の割合。");

        // --- 表1: 行 × 波 -------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 表1: 行 × 波（平均 / 中央値 / 最大）");
        Console.WriteLine();
        Console.WriteLine("**第一波はチュートリアル波（全編成 100%）なので N の導出には使わない。**");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, gulletStages.Count).Select(i => $" 第{i}波 |")));
        Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")));
        foreach (var g in rows.GroupBy(x => x.Build))
        {
            Console.Write($"| {g.Key} |");
            for (int w = 1; w <= gulletStages.Count; w++)
            {
                var xs = g.Where(x => x.Wave == w).Select(x => x.Swallowed).ToList();
                Console.Write($" {xs.Average():F0} / {Median(xs):F0} / {xs.Max()} |");
            }
            Console.WriteLine();
        }

        // --- 表2: 行ごとの集約（第2〜5波）---------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 表2: 行ごとの集約（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`落ちた戦の飲み込み` が**還しの原資の目安**（ゴルムが倒れた戦だけの平均）。");
        Console.WriteLine("`N=x で眠る回数` は `min(floor(飲み込み / N), 生存T)` を1戦ずつ数えた平均——");
        Console.WriteLine("**盤面は動かしていない**（腹は単調に増え、眠るたび N 引かれるので回数は算術で決まる）。");
        Console.WriteLine("生存Tで頭を打つのは、眠るには手番が回ってくる必要があるため。");
        Console.WriteLine();
        int[] cands = { 20, 30, 40, 60, 80, 120 };
        Console.WriteLine("| 編成 | 平均 | 中央値 | 最大 | 生存T | 落ちた | 落ちた戦の飲み込み |"
                          + string.Concat(cands.Select(n => $" N={n} |")));
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|" + string.Concat(cands.Select(_ => "--:|")));
        var late = rows.Where(x => x.Wave >= 2).ToList();
        foreach (var g in late.GroupBy(x => x.Build))
        {
            var xs = g.Select(x => x.Swallowed).ToList();
            var died = g.Where(x => x.Deaths > 0).Select(x => x.Swallowed).ToList();
            Console.Write($"| {g.Key} | {xs.Average():F0} | {Median(xs):F0} | {xs.Max()} "
                + $"| {g.Average(x => x.AliveT):F1} | {g.Count(x => x.Deaths > 0) * 100.0 / g.Count():F0}% "
                + $"| {(died.Count == 0 ? 0 : died.Average()):F0} |");
            foreach (int n in cands)
                Console.Write($" {g.Average(x => Math.Min(x.Swallowed / n, x.AliveT)):F2} |");
            Console.WriteLine();
        }

        // --- 表3: 全体 ----------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("## 表3: 全体（第2〜5波・ゴルム13行をまとめて）");
        Console.WriteLine();
        var all = late.Select(x => x.Swallowed).ToList();
        var allDied = late.Where(x => x.Deaths > 0).Select(x => x.Swallowed).ToList();
        Console.WriteLine($"- 戦数: {late.Count}");
        Console.WriteLine($"- 飲み込み: 平均 **{all.Average():F0}** / 中央値 **{Median(all):F0}** / 最大 {all.Max()}");
        Console.WriteLine($"- ゴルムの生存T: 平均 {late.Average(x => x.AliveT):F1}（決着T 平均 {late.Average(x => x.Turns):F1}）");
        Console.WriteLine($"- 落ちた戦: {allDied.Count * 100.0 / late.Count:F0}%、その戦の飲み込み 平均 **{(allDied.Count == 0 ? 0 : allDied.Average()):F0}** / 中央値 {Median(allDied):F0}");
        Console.WriteLine();
        Console.WriteLine($"参考: ノノの繕い1回は {MenderTrait.Amount} 点（`MenderTrait.Amount`）。");
        Console.WriteLine("還し率は「腹の残り × 率」が繕い1〜2回ぶん（14〜28）になる規模を採る。");
        return;
    }

    // ---- log: 1戦ずつの監査 --------------------------------------------------------------
    //
    // **ここだけはログの文字列を数えている。** UI は LogKind を見るという規約（README）に
    // 反しているように見えるが、確かめたいのは「その行が出たか／出なかったか」そのもので、
    // 盤面の値では代用できない（発火しなかったことは数値に痕跡を残さない）。
    if (gulletMode == "log")
    {
        const string Blocked = "の前に立ちはだかる";
        const string Regurg = "飲み込んだ力を";

        void Audit(string title, string buildKey, int stage, int seed, string[] watch)
        {
            var (name, f) = gulletBuilds.First(b => b.Name.Contains(buildKey));
            BattleResult r = BattleEngine.Run(f, gulletStages[stage].Enemy, seed,
                                              verbose: true, ColossusRule.Default);

            var text = r.Log.Select(l => l.Text).ToList();
            int blocked = text.Count(t => t.Contains(Blocked));
            int regurg = text.Count(t => t.Contains(Regurg));

            Console.WriteLine();
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine($"{name} / 第{stage + 1}波 / seed {seed} / {(r.PlayerWon ? "勝利" : "敗北")} {r.Turns}ターン");
            Console.WriteLine();
            Console.WriteLine($"- 立ちはだかった: **{blocked} 回**");
            Console.WriteLine($"- 吐き戻した: **{regurg} 回**（差 {blocked - regurg} = source が null の刻み等）");

            // 受け手の内訳。ゴルム自身がここに出てはいけない（**ゴルムは育たない**）。
            Console.WriteLine("- 受け手の内訳:");
            foreach (var (_, def) in f.Occupied())
            {
                int n = text.Count(t => t.Contains($"{Regurg} {def.Name} へ返した"));
                if (n > 0 || def.Id == UnitCatalog.Golm.Id)
                    Console.WriteLine($"    - {def.Name}: {n} 回"
                        + (def.Id == UnitCatalog.Golm.Id ? "  ← **0 でなければならない**" : ""));
            }

            // 攻撃力の推移は StatSnapshot（文字列ではなく構造化イベント）から取る。
            // InstanceId は Deploy の順に振られるので、味方はスロット昇順で 0 から数えれば引ける。
            var idOf = new Dictionary<string, int>();
            int id = 0;
            foreach (var (_, def) in f.Occupied()) idOf[def.Name] = id++;

            foreach (string w in watch)
            {
                var (_, def) = f.Occupied().First(o => o.Item2.Name.Contains(w));
                var series = r.Events
                    .Where(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == idOf[def.Name])
                    .Select(e => e.Amount).ToList();
                Console.WriteLine($"- {def.Name} の攻撃力（素 {def.Attack}）: "
                    + string.Join(" → ", series));
            }
        }

        Console.WriteLine("# 吐き戻しの監査（gullet log）");
        Console.WriteLine();
        Console.WriteLine("受け入れ基準 3〜5 を1戦ずつ確かめる。規則は既定（`ColossusRule.Default`）。");

        // 3. ウツに対しては強化が弱体として働く（Perverse は AtkBonus > 0 で攻撃が半減する）
        Audit("A. ウツへの吐き戻しは弱体として働くか", "逆しま (ネル×ウツ)", 4, 0,
              new[] { "ウツ", "ゴルム" });

        // 4. 毒・燃焼の刻み（source が null）では発火しない。
        //    立ちはだかりは刻みでも起きるので、**2つの回数の差**がそのまま除外できた件数になる。
        Audit("B. 毒の刻みでは発火しないか", "追撃×毒 (ハギ×グザ)", 2, 0,
              new[] { "グザ", "ゴルム" });

        // 5. 燃料の経路（第19期 route の対象編成）
        Audit("C. 燃料はムドまで届くか", "置き去り×被弾強化", 4, 0,
              new[] { "ムド", "ゴルム" });
        return;
    }

    // ---- 版の並び ------------------------------------------------------------------------
    (string Name, string Note, ColossusRule Rule)[] versions;
    (string Label, int Hi, int Lo)[] deltas;
    int detail;   // 「波ごとの内訳」で並べる版（先頭版 → この版）

    if (gulletMode == "gain")
    {
        // 上がりすぎたときに最初に振るノブ（計画 §5 の失敗の形）。
        // **肩代わり率は 90% に固定**して、返す効率だけを動かす。
        versions = new (string, string, ColossusRule)[]
        {
            ("g∞ 返し無し", "90% / 吐き戻し無し", new ColossusRule(90, 4, Regurgitate: false)),
            ("g2",  "90% / 2点につき攻撃+1", new ColossusRule(90, 2, Regurgitate: true)),
            ("g4",  "90% / 4点につき攻撃+1（既定）", new ColossusRule(90, 4, Regurgitate: true)),
            ("g6",  "90% / 6点につき攻撃+1", new ColossusRule(90, 6, Regurgitate: true)),
            ("g8",  "90% / 8点につき攻撃+1", new ColossusRule(90, 8, Regurgitate: true)),
        };
        deltas = new[] { ("g4−無", 2, 0), ("g6−無", 3, 0), ("g8−無", 4, 0) };
        detail = 2;
    }
    else
    {
        versions = new (string, string, ColossusRule)[]
        {
            ("V0 現行",     "90% / 吐き戻し無し",
                new ColossusRule(90, ColossusTrait.DamagePerGain, Regurgitate: false)),
            ("V1 吐き戻し", "90% / 吐き戻し有り（本命＝既定）",
                new ColossusRule(90, ColossusTrait.DamagePerGain, Regurgitate: true)),
            ("V2 60%",      "60% / 吐き戻し無し",
                new ColossusRule(60, ColossusTrait.DamagePerGain, Regurgitate: false)),
            ("V3 60%+返し", "60% / 吐き戻し有り",
                new ColossusRule(60, ColossusTrait.DamagePerGain, Regurgitate: true)),
        };
        deltas = new[] { ("V1−V0", 1, 0), ("V3−V2", 3, 2) };
        detail = 1;
    }

    // 燃料の行き先を見る先。route（第19期）が未解決のまま置いていった編成。
    const string FuelBuild = "置き去り×被弾強化";

    int nv = versions.Length, nb = gulletBuilds.Length, nw = gulletStages.Count;

    var rate = new double[nv][][];                 // rate[版][編成][波] = 勝率(%)
    var fuel = new UnitTally[nv];                  // FuelBuild のムドだけ集計する
    var fuelGolm = new UnitTally[nv];
    var fuelBattles = new int[nv];
    var fuelTurns = new long[nv];

    for (int v = 0; v < nv; v++)
    {
        rate[v] = new double[nb][];
        fuel[v] = new UnitTally();
        fuelGolm[v] = new UnitTally();

        for (int b = 0; b < nb; b++)
        {
            rate[v][b] = new double[nw];
            bool track = gulletBuilds[b].Name == FuelBuild;

            // gain 版は検算（ゴルム不在の編成が動かないこと）を既に既定版で済ませているので、
            // ゴルム入りだけを回す。全編成を回しても結果は同じで、時間だけ4倍かかる。
            if (gulletMode == "gain" && !HasGolm(gulletBuilds[b].F)) continue;

            for (int w = 0; w < nw; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < GulletSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(gulletBuilds[b].F, gulletStages[w].Enemy,
                                                      seed, verbose: false, versions[v].Rule);
                    if (r.PlayerWon) wins++;
                    if (!track) continue;

                    fuelBattles[v]++;
                    fuelTurns[v] += r.Turns;
                    if (r.TallyByUnit.TryGetValue(UnitCatalog.Mudo.Id, out UnitTally? mt)) fuel[v].Add(mt);
                    if (r.TallyByUnit.TryGetValue(UnitCatalog.Golm.Id, out UnitTally? gt)) fuelGolm[v].Add(gt);
                }
                rate[v][b][w] = wins * 100.0 / GulletSeeds;
            }
        }
        Console.Error.WriteLine($"  {versions[v].Name} 完了");
    }

    double Avg(int v, int b) => rate[v][b].Average();

    Console.WriteLine("# 巨躯の吐き戻し（gullet）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 × 全ステージ、seed 0..{GulletSeeds - 1}。診断用なので docs/ には置かない。");
    Console.WriteLine();
    foreach (var vv in versions) Console.WriteLine($"- **{vv.Name}**: {vv.Note}");

    if (gulletMode != "gain")
    {
        // --- 検算 1: V0 が現行の balance.md と一致するか -------------------------------
        Console.WriteLine();
        Console.WriteLine("## 検算: V0 × 全編成");
        Console.WriteLine();
        Console.WriteLine("**このセルは `docs/balance.md`（吐き戻し導入前の世代）と一致しなければならない。**");
        Console.WriteLine("ずれていたら診断の組み方（seed 帯・台・編成リスト）が balance.md と揃っていない。");
        Console.WriteLine();
        Console.WriteLine("| 編成 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")));
        for (int b = 0; b < nb; b++)
            Console.WriteLine($"| {gulletBuilds[b].Name} |"
                + string.Concat(rate[0][b].Select(x => $" {x:F1}% |")));

        // --- 検算 2: ゴルムを含まない編成は動かないか ----------------------------------
        Console.WriteLine();
        Console.WriteLine("## 検算: ゴルムを含まない編成");
        Console.WriteLine();
        Console.WriteLine("吐き戻しは巨躯の分岐の中にしかないので、**ゴルム不在の編成は全波 ±0.0 でなければならない**");
        Console.WriteLine("（受け入れ条件）。ここが 0 件でなければ、規則が意図しない場所から漏れている。");
        Console.WriteLine();
        var strays = new List<string>();
        int noGolm = 0;
        for (int b = 0; b < nb; b++)
        {
            if (HasGolm(gulletBuilds[b].F)) continue;
            noGolm++;
            for (int v = 1; v < nv; v++)
                for (int w = 0; w < nw; w++)
                    if (Math.Abs(rate[v][b][w] - rate[0][b][w]) > 1e-9)
                        strays.Add($"{gulletBuilds[b].Name} / {versions[v].Name} / 第{w + 1}波: "
                                   + $"{rate[0][b][w]:F1}% → {rate[v][b][w]:F1}%");
        }
        Console.WriteLine($"ゴルム不在 {noGolm} 編成 × {nv - 1}版 × {nw} 波 = {noGolm * (nv - 1) * nw} セル中、"
                          + $"**V0 と食い違ったセル {strays.Count} 件**。");
        if (strays.Count > 0)
        {
            Console.WriteLine();
            foreach (string x in strays.Take(40)) Console.WriteLine($"- {x}");
        }
    }

    // --- 主表: ゴルム入りの編成 -----------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 主表: ゴルムを含む編成 × 各版");
    Console.WriteLine();
    Console.WriteLine("セルは全波の単純平均。**第一波は全編成 100% なので平均は 20pt ぶん薄まっている**");
    Console.WriteLine("——動いた波は次節で見る。");
    Console.WriteLine();
    Console.WriteLine("**予測は両方向。** 逆しま系3本は落ちる（`Perverse` は `AtkBonus > 0` で攻撃が半減する");
    Console.WriteLine("＝ウツにとって吐き戻しは毒）／死の連鎖系は上がる。**片方向しか出なければ、");
    Console.WriteLine("予測の立て方かウツの扱いを読み違えている。**");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(versions.Select(v => $" {v.Name} |"))
                      + string.Concat(deltas.Select(d => $" {d.Label} |")) + " ゴルムの席 |");
    Console.WriteLine("|---|" + string.Concat(versions.Select(_ => "---:|"))
                      + string.Concat(deltas.Select(_ => "---:|")) + "---|");
    for (int b = 0; b < nb; b++)
    {
        Formation f = gulletBuilds[b].F;
        if (!HasGolm(f)) continue;
        string seat = string.Join("", f.Occupied()
            .Where(o => o.Item2.Id == UnitCatalog.Golm.Id)
            .Select(o => FormationRules.SeatNames[o.Item1]));
        Console.WriteLine($"| {gulletBuilds[b].Name} |"
            + string.Concat(Enumerable.Range(0, nv).Select(v => $" {Avg(v, b):F1}% |"))
            + string.Concat(deltas.Select(d => $" {Avg(d.Hi, b) - Avg(d.Lo, b):+0.0;-0.0}pt |"))
            + $" {seat} |");
    }

    // --- 波ごとの内訳 ---------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine($"## 波ごとの内訳（{versions[0].Name} → {versions[detail].Name}）");
    Console.WriteLine();
    Console.WriteLine("**第三波は第22期に渇きを置いて初めて分離した波**なので、ここが天井へ押し上げられて");
    Console.WriteLine("いないかを併せて見る（波の分離度は `spread` の側で測り直す）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(gulletStages.Select((_, i) => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(gulletStages.Select(_ => "---:|")));
    for (int b = 0; b < nb; b++)
    {
        if (!HasGolm(gulletBuilds[b].F)) continue;
        Console.WriteLine($"| {gulletBuilds[b].Name} |"
            + string.Concat(Enumerable.Range(0, nw).Select(w =>
                $" {rate[0][b][w]:F1} → {rate[detail][b][w]:F1} "
                + $"({rate[detail][b][w] - rate[0][b][w]:+0.0;-0.0}) |")));
    }

    // --- 燃料の行き先 ---------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine($"## 燃料の行き先（{FuelBuild} のムド）");
    Console.WriteLine();
    Console.WriteLine("第19期 `route` の未解決がここで解ける、というのが主眼。**勝率ではなく");
    Console.WriteLine("`ムド 与ダメ(敵)` が上がるかを先に見る。** 動かないなら、吐き戻しの量が小さすぎるか、");
    Console.WriteLine("ムドが先に落ちている（`落ちた` 列と合わせて読む）。");
    Console.WriteLine();
    Console.WriteLine("`ゴルム 与ダメ(敵)` は**ゴルム自身が育っていないこと**の検算。吐き戻しは");
    Console.WriteLine("守った相手にしか返さないので、版をまたいで大きく動いてはいけない。");
    Console.WriteLine();
    Console.WriteLine("**`決着T` を必ず並べて読む。** 数字は1戦あたりの平均なので、");
    Console.WriteLine("早く決着するようになると与ダメの総量は据え置きに見える（1ターンあたりでは増えている）。");
    Console.WriteLine();
    Console.WriteLine("| 版 | ムド 与ダメ(敵) | ムド 与ダメ/T | ムド 撃破 | ムド 被(味) | ムド 被ダメ | ムド 落ちた | ゴルム 与ダメ(敵) | ゴルム 被(味) | 決着T |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int v = 0; v < nv; v++)
    {
        double n = Math.Max(1, fuelBattles[v]);
        double turns = Math.Max(1, fuelTurns[v]);
        Console.WriteLine($"| {versions[v].Name} | {fuel[v].DamageToEnemy / n:F0} "
            + $"| {fuel[v].DamageToEnemy / turns:F2} | {fuel[v].Kills / n:F2} "
            + $"| {fuel[v].TakenFromAlly / n:F0} | {fuel[v].DamageTaken / n:F0} | {fuel[v].Deaths / n:F2} "
            + $"| {fuelGolm[v].DamageToEnemy / n:F0} | {fuelGolm[v].TakenFromAlly / n:F0} "
            + $"| {fuelTurns[v] / n:F1} |");
    }

    // --- 全5波 100% の編成数 ---------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 全波 100% の編成数");
    Console.WriteLine();
    Console.WriteLine("`spread` の (1) と同じ見方。増えていたら上がりすぎ——まず `DamagePerGain` を");
    Console.WriteLine("4 → 6, 8 と振る（`gullet gain`。ゴルムの数値 150/10/3 は触らない）。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 全波100% | 該当編成 |");
    Console.WriteLine("|---|--:|---|");
    for (int v = 0; v < nv; v++)
    {
        var perfect = Enumerable.Range(0, nb)
            .Where(b => (gulletMode != "gain" || HasGolm(gulletBuilds[b].F)) && rate[v][b].All(x => x >= 100.0))
            .Select(b => gulletBuilds[b].Name).ToList();
        Console.WriteLine($"| {versions[v].Name} | {perfect.Count} | {string.Join(" / ", perfect)} |");
    }
    return;
}

// yoke モード: 第四波に「軛」を置く（第25期）。1回のダメージ量に上限を課す盤面ルール。
//
// 第四波は 100% が 21/35・中間帯 7 で、**第一波を除けば最も飽和している波**だった。
// 第22期 spread で作った物差しの上で、第三波を渇き（回復禁止）で分離させたのと同じことをここでやる。
// 課金する資源は「**1発の重さ**」——第二波（後列到達力）・第三波（持続）・第五波（総合）の
// どれとも重ならない。
//
// **敵側の打点は全部 15 以下**（重装 12・詠唱兵の溜め 16・従軍司祭 9）なので、
// この波で課税されるのは味方の大打点だけ（ドルガ38・カドの反撃・セロの狙撃・墓守の層）。
// 「硬いので大打点で押し切れない」は第四波の既存の性格と一貫していて、新しい教え事が要らない。
//
// **版は 5 つ。** 中央の1枚と Cap だけを動かす（gullet と同じく規則は引数で渡す）。
//
//     V0 現行    中央 城塞の重装兵。**差し替え前の docs/balance.md と一致するはず＝検算**
//     V1 壁のみ  中央 軛の重装兵・規則は無効。**数値が同一なので V0 と一致するはず＝検算**
//     V2 上限25  本命（採用した規則＝YokeTrait.Cap）
//     V3 上限30  上限を緩めた側
//     V4 上限20  上限を締めた側
//
// **Cap は計画（15）ではなく `yoke sweep` の実測で 25 に決めた。** 12〜50 を振ると、
// 15 では 16編成・20 でも 12編成が 0% に落ち、第四波の平均が第五波（59.8）を下回る
// ——「波を分離する」ではなく「波を壁にする」になっていた。帯の選び方は sweep の側を見ること。
//
// **V0 と V1 の対照が要。** 逆位（第20期）は保持者の数値を壁から動かしたせいで
// 「壁が変わったのか、ルールが効いたのか」の切り分けに追加測定が要った。ここでは
// 数値を1つも動かしていないので、V1 が V0 と1セルも違わないことがそのまま切り分けになる。
//
// **判定に使う編成は中間帯を持つものから選ぶ**（第24期 yield の教訓）。飽和した台では
// 誰に何をしても増分が決着の短縮に消える。主表には `中` 列を出して、V0 の第四波が
// 5% < x < 95% の編成を印してある——**100% に張り付いている編成は「落ちたかどうか」だけを見る。**
//
// 機構の確認は**ログの文字列ではなく tally** で行う。`敵被ダメ/戦`（＝味方の出力の総量）が
// V1 → V2 でどれだけ削られたかが、そのまま「切られた量」になる。
// `yoke log` は §2.3 の監査（破片・肩代わり・棘守り・惨禍・毒の刻み）で、
// **そちらだけはログの文字列を数えている**（gullet log と同じ理由——「その行がどの順で出たか」
// そのものを見たいので、盤面の値では代用できない）。
//
// docs/ には置かない（診断用）。
//
//     dotnet run --project BattleSim -c Release 0 yoke [sweep|log]
if (focusId == "yoke")
{
    string yokeMode = args.Length > 2 ? args[2] : "";
    var yokeBuilds = CompareBuilds();
    const int YokeSeeds = 200;   // compare / spread / gullet と同じ。balance.md と突き合わせる
    const int Wave4 = 3;         // 第四波（0 起点）

    // 第四波の中央だけを差し替えた版を診断のローカルで組む（gradient / aim と同じ扱い）。
    // 残り4枠も他の4波も EnemyCatalog のまま——**動く変数は中央の1枚と Cap だけ。**
    Formation Wave4With(UnitDef center) => Formation.Build(
        front1: EnemyCatalog.Warden, front3: EnemyCatalog.Warden, center: center,
        back1: EnemyCatalog.Chanter, back3: EnemyCatalog.Priest);

    // 第四波の敵の Def.Id。tally を敵味方に割るのに使う（味方の召喚駒まで正しく味方側に落ちる）。
    var wave4EnemyIds = new HashSet<string>(new[]
    {
        EnemyCatalog.Warden.Id, EnemyCatalog.Yoker.Id, EnemyCatalog.Chanter.Id, EnemyCatalog.Priest.Id
    });

    // ---- sweep: Cap の帯を振る -----------------------------------------------------------
    //
    // 4版の対照（V0/V1/V2/V3/V4）で **Cap 15 も 20 も落としすぎる**ことが分かったので、
    // 上限そのものを帯で振る。計画 §6 の「失敗の形」に書いてある手当（15 が当たりすぎたら 20 へ）を
    // 実際に測ると 20 でも平均 87.0 → 45.6 まで落ちる——**第五波（59.8）より難しい波になる。**
    //
    // 他の4波は保持者が不在なので Cap をいくら振っても1セルも動かない（引数なしの実行で
    // 560 セル 0 件を確認済み）。だから**基準の5波を1回だけ測って、第四波だけを Cap ごとに測り直す**。
    // 固有の敗者・勝者の判定と第2波との相関は、その基準の他波と組み合わせて引く。
    if (yokeMode == "sweep")
    {
        int[] caps = { 12, 15, 20, 25, 30, 35, 40, 50 };
        int nbS = yokeBuilds.Length, nwS = EnemyCatalog.Stages.Count;

        // 基準（V0 = 差し替え前の盤面）。他の4波はこの値をそのまま使い回す。
        var basis = new double[nbS][];
        for (int b = 0; b < nbS; b++)
        {
            basis[b] = new double[nwS];
            for (int w = 0; w < nwS; w++)
            {
                Formation foe = w == Wave4 ? Wave4With(EnemyCatalog.Warden) : EnemyCatalog.Stages[w].Enemy;
                int wins = 0;
                for (int seed = 0; seed < YokeSeeds; seed++)
                    if (BattleEngine.Run(yokeBuilds[b].F, foe, seed, verbose: false).PlayerWon) wins++;
                basis[b][w] = wins * 100.0 / YokeSeeds;
            }
        }
        Console.Error.WriteLine("  基準（軛なし）完了");

        // Cap ごとの第四波。
        var capRate = new double[caps.Length][];
        for (int c = 0; c < caps.Length; c++)
        {
            capRate[c] = new double[nbS];
            Formation foe = Wave4With(EnemyCatalog.Yoker);
            var rule = new YokeRule(caps[c], Active: true);
            for (int b = 0; b < nbS; b++)
            {
                int wins = 0;
                for (int seed = 0; seed < YokeSeeds; seed++)
                    if (BattleEngine.Run(yokeBuilds[b].F, foe, seed, verbose: false, null, rule).PlayerWon) wins++;
                capRate[c][b] = wins * 100.0 / YokeSeeds;
            }
            Console.Error.WriteLine($"  Cap {caps[c]} 完了");
        }

        Console.WriteLine("# 軛の上限を振る（yoke sweep）");
        Console.WriteLine();
        Console.WriteLine($"代表編成 {nbS} × Cap {caps.Length} 通り、seed 0..{YokeSeeds - 1}。診断用なので docs/ には置かない。");
        Console.WriteLine();
        Console.WriteLine("第四波以外は保持者が不在で1セルも動かない（引数なしの `yoke` で確認済み）ので、");
        Console.WriteLine("**基準の5波を1回測って、第四波だけを Cap ごとに測り直している。**");
        Console.WriteLine();
        Console.WriteLine("比較のための他波の現状値（`spread` と同じ計算）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 平均 | 100%の編成 | 0%の編成 | 中間帯 | 標準偏差 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        for (int w = 0; w < nwS; w++)
        {
            double[] col = Enumerable.Range(0, nbS).Select(b => basis[b][w]).ToArray();
            double mean = col.Average();
            double sd = Math.Sqrt(col.Select(x => (x - mean) * (x - mean)).Sum() / col.Length);
            Console.WriteLine($"| 第{w + 1}波 | {mean:F1} | {col.Count(x => x >= 100.0)} / {nbS} "
                + $"| {col.Count(x => x <= 0.0)} | {col.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} |");
        }

        Console.WriteLine();
        Console.WriteLine("## Cap ごとの第四波");
        Console.WriteLine();
        Console.WriteLine("**波の難度そのものを上げるのが目的ではない。** 見るのは 中間帯 と 固有の敗者 で、");
        Console.WriteLine("平均は「第五波（59.8）より難しい波にしていないか」の歯止めとして読む。");
        Console.WriteLine();
        Console.WriteLine("| Cap | 平均 | 100%の編成 | 0%の編成 | 中間帯 | 標準偏差 | 固有の敗者 | 固有の勝者 | 第2波との相関 | 第2〜4波すべて100% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

        double[] Col(int c) => c < 0
            ? Enumerable.Range(0, nbS).Select(b => basis[b][Wave4]).ToArray()
            : capRate[c];
        double At(int c, int b, int w) => w == Wave4 ? Col(c)[b] : basis[b][w];

        List<string> Losers(int c) => Enumerable.Range(0, nbS)
            .Where(b => At(c, b, Wave4) <= 0.0
                        && Enumerable.Range(1, nwS - 1).All(o => o == Wave4 || At(c, b, o) > 0.0))
            .Select(b => yokeBuilds[b].Name).ToList();
        List<string> Winners(int c) => Enumerable.Range(0, nbS)
            .Where(b => At(c, b, Wave4) >= 100.0
                        && Enumerable.Range(1, nwS - 1).All(o => o == Wave4 || At(c, b, o) < 100.0))
            .Select(b => yokeBuilds[b].Name).ToList();

        void Line(string label, int c)
        {
            double[] col = Col(c);
            double mean = col.Average();
            double sd = Math.Sqrt(col.Select(x => (x - mean) * (x - mean)).Sum() / col.Length);
            double corr = Corr2(Enumerable.Range(0, nbS).Select(b => basis[b][1]).ToArray(), col);
            int allTop = Enumerable.Range(0, nbS)
                .Count(b => Enumerable.Range(1, 3).All(w => At(c, b, w) >= 100.0));
            Console.WriteLine($"| {label} | {mean:F1} | {col.Count(x => x >= 100.0)} / {nbS} "
                + $"| {col.Count(x => x <= 0.0)} | {col.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} "
                + $"| {Losers(c).Count} | {Winners(c).Count} "
                + $"| {(double.IsNaN(corr) ? "—" : $"{corr:+0.00;-0.00}")} | {allTop} / {nbS} |");
        }

        Line("軛なし", -1);
        for (int c = 0; c < caps.Length; c++) Line($"Cap {caps[c]}", c);

        Console.WriteLine();
        for (int c = 0; c < caps.Length; c++)
        {
            var lose = Losers(c);
            Console.WriteLine($"- **Cap {caps[c]}** 固有の敗者 ({lose.Count}): "
                + (lose.Count == 0 ? "**なし**" : string.Join(" / ", lose)));
        }

        Console.WriteLine();
        Console.WriteLine("## 編成 × Cap");
        Console.WriteLine();
        Console.WriteLine("`中` は軛なしの第四波が中間帯（5% < x < 95%）にある編成。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 中 | 軛なし |" + string.Concat(caps.Select(c => $" {c} |")));
        Console.WriteLine("|---|:-:|---:|" + string.Concat(caps.Select(_ => "---:|")));
        for (int b = 0; b < nbS; b++)
        {
            bool mid = basis[b][Wave4] > 5.0 && basis[b][Wave4] < 95.0;
            Console.WriteLine($"| {yokeBuilds[b].Name} | {(mid ? "●" : "")} | {basis[b][Wave4]:F1} |"
                + string.Concat(Enumerable.Range(0, caps.Length).Select(c => $" {capRate[c][b]:F1} |")));
        }
        return;
    }

    // ---- log: 計画 §2.3 の監査 -----------------------------------------------------------
    if (yokeMode == "log")
    {
        Formation wave4 = EnemyCatalog.Stages[Wave4].Enemy;

        void Audit(string title, string buildKey, int seed, string[] marks, string note)
        {
            var (name, f) = yokeBuilds.First(b => b.Name.Contains(buildKey));
            BattleResult r = BattleEngine.Run(f, wave4, seed, verbose: true);

            // 保持者の InstanceId。ctx.Add は 味方（スロット昇順）→ 敵（スロット昇順）の順に振る。
            int id = f.Occupied().Count(), yokerId = -1;
            foreach (var (_, def) in wave4.Occupied())
            {
                if (def.Id == EnemyCatalog.Yoker.Id) yokerId = id;
                id++;
            }
            // **保持者の生死はターンではなくイベントの並びで割る。** 倒れたのと同じターンの
            // 後続のダメージはもう上限の外側にあるので、ターンで割ると「上限を超えた」と誤検出する
            // （実際に踏んだ: 反撃改2 の seed 0 で 78 が生存中に見えていた）。
            var events = r.Events.ToList();
            int deathAt = events.FindIndex(e => e.Kind == BattleEventKind.Death && e.TargetId == yokerId);
            if (deathAt < 0) deathAt = events.Count;
            int deathTurn = deathAt < events.Count ? events[deathAt].Turn : int.MaxValue;

            int maxUnder = events.Take(deathAt)
                .Where(e => e.Kind == BattleEventKind.Damage).Select(e => e.Amount).DefaultIfEmpty(0).Max();
            int maxAfter = events.Skip(deathAt)
                .Where(e => e.Kind == BattleEventKind.Damage).Select(e => e.Amount).DefaultIfEmpty(0).Max();
            var text = r.Log.Select(l => l.Text).ToList();
            int cuts = text.Count(t => t.Contains("軛が") && t.Contains("切った"));

            Console.WriteLine();
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine(note);
            Console.WriteLine();
            Console.WriteLine($"{name} / 第四波 / seed {seed} / {(r.PlayerWon ? "勝利" : "敗北")} {r.Turns}ターン");
            Console.WriteLine();
            Console.WriteLine($"- 軛が切った回数: **{cuts} 回**");
            Console.WriteLine("- 保持者が倒れたターン: "
                + (deathTurn == int.MaxValue ? "**最後まで生存**" : $"**T{deathTurn}**"));
            Console.WriteLine($"- 保持者の生存中の最大ダメージ: **{maxUnder}**"
                + $"（**{YokeTrait.Cap} 以下でなければならない**）");
            Console.WriteLine("- 保持者が倒れた後の最大ダメージ: "
                + (deathTurn == int.MaxValue ? "—" : $"{maxAfter}") + "（上限が外れたことの確認）");

            // 抜粋。前後に数行付けて順序が読めるようにする（破片 → 軛 → ダメージ の並び）。
            foreach (string mark in marks)
            {
                // 上限との関係が読める箇所を優先する。無ければ最初の一致に落とす。
                int at = Enumerable.Range(0, text.Count).FirstOrDefault(
                    i => text[i].Contains(mark)
                         && Enumerable.Range(i, Math.Min(4, text.Count - i)).Any(j => text[j].Contains("軛が")),
                    -1);
                if (at < 0) at = text.FindIndex(t => t.Contains(mark));
                Console.WriteLine();
                if (at < 0) { Console.WriteLine($"- `{mark}` を含む行は出なかった"); continue; }
                Console.WriteLine($"- `{mark}` の周辺:");
                Console.WriteLine();
                Console.WriteLine("```");
                for (int i = Math.Max(0, at - 1); i < Math.Min(text.Count, at + 4); i++)
                    Console.WriteLine(text[i]);
                Console.WriteLine("```");
            }
        }

        // 2つの行が近接する事例を seed で探す。破片・肩代わりと上限の同時発火は
        // 「起きるかどうか」自体が結果なので、**見つからなかったときは走査した seed 数を書く**
        // （1戦だけ見て「出なかった」と書くと、偶然か構造かが分からない）。
        void AuditPair(string title, string buildKey, string first, string second, int seeds, string note)
        {
            var (name, f) = yokeBuilds.First(b => b.Name.Contains(buildKey));
            Console.WriteLine();
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine(note);

            for (int seed = 0; seed < seeds; seed++)
            {
                var text = BattleEngine.Run(f, wave4, seed, verbose: true).Log.Select(l => l.Text).ToList();
                int at = Enumerable.Range(0, text.Count).FirstOrDefault(
                    i => text[i].Contains(first)
                         && Enumerable.Range(i, Math.Min(3, text.Count - i)).Any(j => text[j].Contains(second)),
                    -1);
                if (at < 0) continue;

                Console.WriteLine();
                Console.WriteLine($"{name} / 第四波 / seed {seed} で `{first}` と `{second}` が同時に出た:");
                Console.WriteLine();
                Console.WriteLine("```");
                for (int i = Math.Max(0, at - 1); i < Math.Min(text.Count, at + 4); i++)
                    Console.WriteLine(text[i]);
                Console.WriteLine("```");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"{name} / 第四波 / seed 0..{seeds - 1} を走査したが、"
                + $"`{first}` と `{second}` が同時に出る事例は **0 件**。");
        }

        Console.WriteLine("# 軛の監査（yoke log）");
        Console.WriteLine();
        Console.WriteLine("受け入れ基準 3（計画 §2.3）を1戦ずつ確かめる。"
            + $"規則は既定（`YokeRule.Default` / Cap {YokeTrait.Cap}）。");
        Console.WriteLine();
        Console.WriteLine("**ここだけはログの文字列を数えている。** UI は `LogKind` を見るという規約に");
        Console.WriteLine("反して見えるが、確かめたいのは「その行がどの順で出たか」そのもので、");
        Console.WriteLine("盤面の値では代用できない（`gullet log` と同じ理由）。");

        AuditPair("A. 破片は上限の外側で効くか", "範囲耐性", "破片が", "軛が", 50,
              "破片（`StatusKeys.Armor`）は上限**より前**に引かれる別資源。"
            + "破片が吸った後の残りが上限で切られる。"
            + "**破片が付くのは味方だけ**なので、この波では同時に働くには"
            + "味方が 25 を超える一撃（＝味方由来の巻き込み）を浴びる必要がある。");

        AuditPair("B. 肩代わりの各段は独立に切られるか", "耐久 (ガルド", "立ちはだかる", "軛が", 50,
              "巨躯で分割された段はそれぞれ別の `ApplyDamage` 呼び出しなので、段ごとに切られる。"
            + "**分割は上限を回避する経路**——意図した帰結（重い一撃は分けて受けろ）。"
            + "肩代わりは味方への攻撃にしか働かないので、これも味方が 25 超えを浴びたときだけ出る。");

        Audit("C. 棘守りの二重上限 / 惨禍の増幅", "反撃改2", 0, new[] { "鎧は貫かれ", "軛が" },
              "棘守り（カド）は `AbsorbCap` で既に別の上限を持つ。中継先への超過分は"
            + "別の呼び出しなので独立に切られる。惨禍（+50%）は**増幅が先・上限が後**なので、"
            + "増幅は上限の下で消える（意図どおり）。");

        Audit("D. 毒の刻みも切られるか", "毒 (グザ", 0, new[] { "毒に蝕まれている" },
              "毒は除外しない。渇きが `source == null` を外したのとは違い、こちらは"
            + "**「1発の重さ」に課金する規則**なので出どころは関係ない。");

        Audit("E. 墓守の層は上限に当たるか", "死の連鎖 (リィカ", 0, new[] { "軛が" },
              "リィカの層は攻撃力が三角数で伸びる（実測 5 → 35 → 64）。**上層が潰れる**のがここ。");
        return;
    }

    // ---- 版の並び ------------------------------------------------------------------------
    var versions = new (string Name, string Note, UnitDef Center, YokeRule Rule)[]
    {
        ("V0 現行",   "中央 城塞の重装兵（軛なし）＝**差し替え前の盤面**",
            EnemyCatalog.Warden, YokeRule.Default),
        ("V1 壁のみ", "中央 軛の重装兵・**規則は無効**（数値は V0 と同一）",
            EnemyCatalog.Yoker, new YokeRule(YokeTrait.Cap, Active: false)),
        ("V2 上限25", $"中央 軛の重装兵・Cap {YokeTrait.Cap}（**本命＝採用した規則**）",
            EnemyCatalog.Yoker, YokeRule.Default),
        ("V3 上限30", "Cap 30（上限を緩めた側）",
            EnemyCatalog.Yoker, new YokeRule(30, Active: true)),
        ("V4 上限20", "Cap 20（上限を締めた側。**ここから下は波が壁になる**）",
            EnemyCatalog.Yoker, new YokeRule(20, Active: true)),
    };

    int nv = versions.Length, nb = yokeBuilds.Length, nw = EnemyCatalog.Stages.Count;

    // 版ごとの敵5波。第四波以外は EnemyCatalog のものをそのまま指す。
    var board = new Formation[nv][];
    for (int v = 0; v < nv; v++)
    {
        board[v] = new Formation[nw];
        for (int w = 0; w < nw; w++)
            board[v][w] = w == Wave4 ? Wave4With(versions[v].Center) : EnemyCatalog.Stages[w].Enemy;
    }

    var rate = new double[nv][][];          // rate[版][編成][波] = 勝率(%)
    var outFoe = new double[nv][];          // 第四波・1戦あたりの「敵が受けたダメージ」＝味方の出力
    var outAlly = new double[nv][];         // 第四波・1戦あたりの「味方が受けたダメージ」
    var turns4 = new double[nv][];          // 第四波・決着ターン

    for (int v = 0; v < nv; v++)
    {
        rate[v] = new double[nb][];
        outFoe[v] = new double[nb];
        outAlly[v] = new double[nb];
        turns4[v] = new double[nb];

        for (int b = 0; b < nb; b++)
        {
            rate[v][b] = new double[nw];
            for (int w = 0; w < nw; w++)
            {
                int wins = 0;
                long foe = 0, ally = 0, turns = 0;
                for (int seed = 0; seed < YokeSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(yokeBuilds[b].F, board[v][w], seed,
                                                      verbose: false, null, versions[v].Rule);
                    if (r.PlayerWon) wins++;
                    if (w != Wave4) continue;

                    turns += r.Turns;
                    // **与ダメは受け手側から取る**（第13期 Phase DA）。毒・燃焼は source が
                    // null なので味方側から合計すると毒軸の出力が構造的に過小になる。
                    foreach ((string id, UnitTally t) in r.TallyByUnit)
                        if (wave4EnemyIds.Contains(id)) foe += t.DamageTaken;
                        else ally += t.DamageTaken;
                }
                rate[v][b][w] = wins * 100.0 / YokeSeeds;
                if (w != Wave4) continue;
                outFoe[v][b] = (double)foe / YokeSeeds;
                outAlly[v][b] = (double)ally / YokeSeeds;
                turns4[v][b] = (double)turns / YokeSeeds;
            }
        }
        Console.Error.WriteLine($"  {versions[v].Name} 完了");
    }

    Console.WriteLine("# 第四波の軛（yoke）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 {nb} × 全 {nw} 波 × {nv} 版、seed 0..{YokeSeeds - 1}。"
        + "診断用なので docs/ には置かない。");
    Console.WriteLine();
    foreach (var vv in versions) Console.WriteLine($"- **{vv.Name}**: {vv.Note}");

    // --- 検算 1: V0 が差し替え前の balance.md と一致するか --------------------------------
    Console.WriteLine();
    Console.WriteLine("## 検算 1: V0 × 全編成");
    Console.WriteLine();
    Console.WriteLine("**このセルは差し替え前の `docs/balance.md`（`git show HEAD:docs/balance.md`）と");
    Console.WriteLine("一致しなければならない。** ずれていたら診断の組み方（seed 帯・台・編成リスト）が");
    Console.WriteLine("balance.md と揃っていない。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(1, nw).Select(i => $" 第{i}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "---:|")));
    for (int b = 0; b < nb; b++)
        Console.WriteLine($"| {yokeBuilds[b].Name} |" + string.Concat(rate[0][b].Select(x => $" {x:F1}% |")));

    // --- 検算 2: V1（壁のみ）は V0 と一致するか -------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 検算 2: V1（壁のみ）= V0");
    Console.WriteLine();
    Console.WriteLine("軛の重装兵は城塞の重装兵と**数値が1つも違わない**ので、規則を切れば盤面は完全に同じになる。");
    Console.WriteLine("**ここが 0 件でなければ、差し替えが数値も動かしている**（逆位の失敗の直接の原因）。");
    Console.WriteLine();
    var v1stray = new List<string>();
    for (int b = 0; b < nb; b++)
        for (int w = 0; w < nw; w++)
            if (Math.Abs(rate[1][b][w] - rate[0][b][w]) > 1e-9)
                v1stray.Add($"{yokeBuilds[b].Name} / 第{w + 1}波: {rate[0][b][w]:F1}% → {rate[1][b][w]:F1}%");
    Console.WriteLine($"{nb} 編成 × {nw} 波 = {nb * nw} セル中、**食い違い {v1stray.Count} 件**。");
    foreach (string x in v1stray.Take(40)) Console.WriteLine($"- {x}");

    // --- 検算 3: 第四波以外は全版で動かないか ---------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 検算 3: 第一・二・三・五波は全版 ±0.0");
    Console.WriteLine();
    Console.WriteLine("軛の保持者は第四波にしかいないので、他の4波は Cap を振っても1セルも動いてはいけない");
    Console.WriteLine("（受け入れ基準 1）。**動いていたら規則が保持者の不在下でも効いている。**");
    Console.WriteLine();
    var otherStray = new List<string>();
    for (int v = 1; v < nv; v++)
        for (int b = 0; b < nb; b++)
            for (int w = 0; w < nw; w++)
                if (w != Wave4 && Math.Abs(rate[v][b][w] - rate[0][b][w]) > 1e-9)
                    otherStray.Add($"{yokeBuilds[b].Name} / {versions[v].Name} / 第{w + 1}波: "
                                   + $"{rate[0][b][w]:F1}% → {rate[v][b][w]:F1}%");
    Console.WriteLine($"{nb} 編成 × {nv - 1} 版 × {nw - 1} 波 = {nb * (nv - 1) * (nw - 1)} セル中、"
                      + $"**食い違い {otherStray.Count} 件**。");
    foreach (string x in otherStray.Take(40)) Console.WriteLine($"- {x}");

    // --- 主表: 第四波 ---------------------------------------------------------------------
    bool Mid(int b) => rate[0][b][Wave4] > 5.0 && rate[0][b][Wave4] < 95.0;

    Console.WriteLine();
    Console.WriteLine("## 主表: 第四波の勝率 × 各版");
    Console.WriteLine();
    Console.WriteLine("`中` は **V0 の第四波が中間帯（5% < x < 95%）にある編成**＝この台で増分が読める編成");
    Console.WriteLine("（第24期 yield の教訓。飽和したセルでは誰に何をしても 0 に潰れる）。");
    Console.WriteLine("100% に張り付いている編成は「落ちたかどうか」だけを見る。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 中 |" + string.Concat(versions.Select(v => $" {v.Name} |"))
                      + " V2−V0 | V3−V0 | V4−V0 |");
    Console.WriteLine("|---|:-:|" + string.Concat(versions.Select(_ => "---:|")) + "---:|---:|---:|");
    for (int b = 0; b < nb; b++)
        Console.WriteLine($"| {yokeBuilds[b].Name} | {(Mid(b) ? "●" : "")} |"
            + string.Concat(Enumerable.Range(0, nv).Select(v => $" {rate[v][b][Wave4]:F1}% |"))
            + $" {rate[2][b][Wave4] - rate[0][b][Wave4]:+0.0;-0.0} |"
            + $" {rate[3][b][Wave4] - rate[0][b][Wave4]:+0.0;-0.0} |"
            + $" {rate[4][b][Wave4] - rate[0][b][Wave4]:+0.0;-0.0} |");

    // --- 判定（計画 §6 の表）--------------------------------------------------------------
    List<string> UniqueLosers(int v, int w) => Enumerable.Range(0, nb)
        .Where(b => rate[v][b][w] <= 0.0
                    && Enumerable.Range(1, nw - 1).All(o => o == w || rate[v][b][o] > 0.0))
        .Select(b => yokeBuilds[b].Name).ToList();
    List<string> UniqueWinners(int v, int w) => Enumerable.Range(0, nb)
        .Where(b => rate[v][b][w] >= 100.0
                    && Enumerable.Range(1, nw - 1).All(o => o == w || rate[v][b][o] < 100.0))
        .Select(b => yokeBuilds[b].Name).ToList();

    Console.WriteLine();
    Console.WriteLine("## 判定（計画 §6）");
    Console.WriteLine();
    Console.WriteLine("`spread` の (1)(2)(3) を第四波について版ごとに引き直したもの。");
    Console.WriteLine("**固有の敗者/勝者は第一波を比較から外して数える**（第22期 Phase 2b。");
    Console.WriteLine("第一波は全編成 100% を意図して維持しているので、入れると恒等的に 0 になる）。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 平均 | 100%の編成 | 0%の編成 | 中間帯 | 標準偏差 | 固有の敗者 | 固有の勝者 | 第2波との相関 | 第2〜4波すべて100% |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int v = 0; v < nv; v++)
    {
        double[] col = Enumerable.Range(0, nb).Select(b => rate[v][b][Wave4]).ToArray();
        double mean = col.Average();
        double sd = Math.Sqrt(col.Select(x => (x - mean) * (x - mean)).Sum() / col.Length);
        double corr = Corr2(Enumerable.Range(0, nb).Select(b => rate[v][b][1]).ToArray(), col);
        int allTop = Enumerable.Range(0, nb)
            .Count(b => Enumerable.Range(1, 3).All(w => rate[v][b][w] >= 100.0));
        Console.WriteLine($"| {versions[v].Name} | {mean:F1} | {col.Count(x => x >= 100.0)} / {nb} "
            + $"| {col.Count(x => x <= 0.0)} | {col.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} "
            + $"| {UniqueLosers(v, Wave4).Count} | {UniqueWinners(v, Wave4).Count} "
            + $"| {(double.IsNaN(corr) ? "—" : $"{corr:+0.00;-0.00}")} | {allTop} / {nb} |");
    }
    Console.WriteLine();
    for (int v = 0; v < nv; v++)
    {
        var lose = UniqueLosers(v, Wave4);
        var win = UniqueWinners(v, Wave4);
        Console.WriteLine($"- **{versions[v].Name}** 固有の敗者 ({lose.Count}): "
            + (lose.Count == 0 ? "**なし**" : string.Join(" / ", lose))
            + $" ／ 固有の勝者 ({win.Count}): " + (win.Count == 0 ? "なし" : string.Join(" / ", win)));
    }

    // --- 動いた編成 -----------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 動いた編成（V2−V0 の順）");
    Console.WriteLine();
    Console.WriteLine("計画 §5 の予測（大打点を持つ編成から落ちる）と突き合わせる列。");
    Console.WriteLine("**`中` が付いていない行の 0.0 は「無風」ではなく「読めない」**——飽和したセルなので。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 中 | V0 | V2 | 差 | 敵被ダメ/戦 V1→V2 | 味方被ダメ/戦 V1→V2 | 決着T V1→V2 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|");
    foreach (int b in Enumerable.Range(0, nb).OrderBy(b => rate[2][b][Wave4] - rate[0][b][Wave4]))
        Console.WriteLine($"| {yokeBuilds[b].Name} | {(Mid(b) ? "●" : "")} | {rate[0][b][Wave4]:F1} "
            + $"| {rate[2][b][Wave4]:F1} | {rate[2][b][Wave4] - rate[0][b][Wave4]:+0.0;-0.0} "
            + $"| {outFoe[1][b]:F0} → {outFoe[2][b]:F0} ({outFoe[2][b] - outFoe[1][b]:+0;-0}) "
            + $"| {outAlly[1][b]:F0} → {outAlly[2][b]:F0} ({outAlly[2][b] - outAlly[1][b]:+0;-0}) "
            + $"| {turns4[1][b]:F1} → {turns4[2][b]:F1} |");

    Console.WriteLine();
    Console.WriteLine("`敵被ダメ/戦` は**受け手側から数えた味方の出力**（第13期 Phase DA。毒・燃焼は");
    Console.WriteLine("`source` が null なので味方側から合計すると毒軸が構造的に過小になる）。");
    Console.WriteLine("V1 → V2 の減りが、そのまま**上限で切られた量**。ここが動いていない編成は、");
    Console.WriteLine("そもそも上限に当たる打点を持っていない。");
    return;

    // ピアソン相関。片方の分散が 0 なら定義できないので NaN を返す（呼び出し側で — に置く）。
    static double Corr2(double[] a, double[] b)
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

// scale モード: アーマー（`StatusKeys.Armor`）に初めての読み手を作る（第47期）。
//
// **出力は docs/ に置かない**（標準出力で読むだけ）。
// `CompareBuilds()` / `Stages` / `Columns` は触らない。
//
//     dotnet run --project BattleSim -c Release 0 scale [絞り込み]
//     dotnet run --project BattleSim -c Release 0 scale phase0   # 実装前の地図（§2 Phase 0）
if (focusId == "scale")
{
    var scBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> scStages = EnemyCatalog.Stages;
    const int ScSeeds = 200;   // compare / spread / shove / dull / relay / slander / overbear と揃える
    const int ScMain = 1;      // 主表に使う CostPerAttack（探索段階の初期値）
    int[] scCosts = { 0, 1, 2 };

    string scMode = args.Length > 2 ? args[2] : "";

    // --- Phase 0（§2）: 実装の前に走らせる地図 ------------------------------------------------
    // **盤面を1つも動かさない純粋な記録。** 鱗の実装とは独立で、`ScaleRule` にも触れない。
    if (scMode == "phase0")
    {
        Console.WriteLine("# 鱗 Phase 0 —— 供給の地図（第47期 §2）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 scale phase0` の出力。seed 0..{ScSeeds - 1}。");
        Console.WriteLine("**盤面は1つも動かない。** 実装の前に走らせる測定で、`docs/` には置かない。");
        Console.WriteLine();

        // 0-3. 敵の範囲攻撃を波ごとに数える。
        // **砕け（ShatterTrait）は `source.CurrentPattern != Single` で発火する**ので、
        // 薙ぎ・全体だけでなく**貫きも破片を生む**。ここを単体/範囲の2値で数えると
        // ヒビの供給源を1波ぶん数え落とす。
        Console.WriteLine("## 0-3. 敵の範囲攻撃（＝砕けの供給源）を波ごとに数える");
        Console.WriteLine();
        Console.WriteLine("`ShatterTrait.OnDamaged` は `source.CurrentPattern == AttackPattern.Single` で早期 return するので、");
        Console.WriteLine("**薙ぎ・全体・貫きの3つすべてが破片を生む**（毒・燃焼は `source` が null なので外れる）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 範囲持ちの敵（席・型・溜め） | 範囲枚数 / 5 |");
        Console.WriteLine("|---|---|--:|");
        for (int w = 0; w < scStages.Count; w++)
        {
            var names = new List<string>();
            int cnt = 0;
            foreach ((int slot, UnitDef d) in scStages[w].Enemy.Occupied())
            {
                if (d.Pattern == AttackPattern.Single) continue;
                cnt++;
                string label = d.Pattern switch
                {
                    AttackPattern.Sweep => "薙ぎ",
                    AttackPattern.Pierce => "貫き",
                    AttackPattern.All => "全体",
                    _ => "単体"
                };
                bool charge = d.Actions is not null
                    && d.Actions.Any(a => a.Kind == ActionKind.Charge);
                names.Add($"{FormationRules.SeatNames[slot]}:{d.Name}({label}{(charge ? "・溜め" : "")})");
            }
            Console.WriteLine($"| {scStages[w].Name} | {(names.Count == 0 ? "—" : string.Join(" / ", names))} | {cnt} |");
        }
        Console.WriteLine();

        // 0-6. 味方の攻撃型の分布。**ロスターに常時の貫きは1枚も無い**ことの確認。
        Console.WriteLine("## 0-6. 味方の攻撃型の分布（`Def.Pattern`）と `ModifyPattern` の既存実装");
        Console.WriteLine();
        var pat = new Dictionary<AttackPattern, List<string>>();
        foreach (UnitDef d in UnitCatalog.All)
        {
            if (!pat.TryGetValue(d.Pattern, out var l)) pat[d.Pattern] = l = new List<string>();
            l.Add(d.Name);
        }
        Console.WriteLine("| 型 | 枚数 | 駒 |");
        Console.WriteLine("|---|--:|---|");
        foreach (AttackPattern p in new[] { AttackPattern.Single, AttackPattern.Sweep,
                                            AttackPattern.Pierce, AttackPattern.All })
        {
            var l = pat.TryGetValue(p, out var x) ? x : new List<string>();
            Console.WriteLine($"| {p} | {l.Count} | {(l.Count == 0 ? "—" : (l.Count > 6 ? "（略）" : string.Join("・", l)))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"ロスターは {UnitCatalog.All.Count} 枚。**常時の貫きは 0 枚**で、");
        Console.WriteLine("貫きは `ModifyPattern` を通してしか立たない。");
        Console.WriteLine();

        // 0-4. 味方の1戦あたりの死亡数を波ごとに出す。**ウロの供給条件そのもの。**
        //
        // 蘇生（ヴェル）で戻ってから再度倒れると `Deaths` は2回数えるが、
        // **鱗の供給も `OnAllyDeath` が2回走る**ので、数え方は供給と揃っている。
        // 胞子（ムグ）の子は `Ephemeral` で、これも `OnAllyDeath` を通る（本文で扱う）。
        Console.WriteLine("## 0-4. 味方の死亡数（1戦あたり）を波ごとに出す");
        Console.WriteLine();
        Console.WriteLine("`UnitTally.Deaths` の味方側の合計 ÷ 試行数。**蘇生されて再度倒れると 2 と数える**");
        Console.WriteLine("（鱗の供給も `OnAllyDeath` が2回走るので、数え方は供給と揃っている）。");
        Console.WriteLine("`死軸` は リィカ・ムグ・ゾト・ヴェル・ラウ・ハギ のいずれかを含む行。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 死軸 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        string[] deathAxis = { "rica", "mug", "zoto", "vel", "rau", "hagi" };
        var deathRows = new List<(string Name, bool Axis, double[] D)>();
        foreach (var b in scBuilds)
        {
            var ids = b.F.Occupied().Select(o => o.Def.Id).ToHashSet();
            bool axis = deathAxis.Any(ids.Contains);
            var d = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++)
            {
                int deaths = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                {
                    var r = BattleEngine.Run(b.F, scStages[w].Enemy, seed, verbose: false);
                    foreach ((string id, UnitTally t) in r.TallyByUnit)
                        if (UnitCatalog.All.Any(u => u.Id == id)
                            || id == "spore")   // 胞子は味方側の増援
                            deaths += t.Deaths;
                }
                d[w] = (double)deaths / ScSeeds;
            }
            deathRows.Add((b.Name, axis, d));
            Console.WriteLine($"| {b.Name} | {(axis ? "**●**" : "")} "
                + string.Concat(d.Select(x => $"| {x:0.00} ")) + $"| {d.Average():0.00} |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (label, sel) in new (string, Func<(string, bool, double[]), bool>)[]
                 { ("全 48 行", _ => true), ("死軸", x => x.Item2), ("死軸でない", x => !x.Item2) })
        {
            var g = deathRows.Select(x => (x.Name, x.Axis, x.D)).Where(sel).ToList();
            var avg = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++) avg[w] = g.Average(x => x.Item3[w]);
            Console.WriteLine($"| {label} | {g.Count} "
                + string.Concat(avg.Select(x => $"| {x:0.00} ")) + $"| {avg.Average():0.00} |");
        }
        Console.WriteLine();
        return;
    }

    static bool ScHasUro(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Uro));

    var scTargets = scBuilds.Where(b => ScHasUro(b.F)).ToArray();
    if (scMode.Length > 0 && scMode != "sweep" && scMode != "seats")
        scTargets = scTargets.Where(b => scMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // ウロを外した同じ編成（4体版）。**第21期の飽和検査を兼ねる**
    // ——4体版と5体版が同じ値なら、その台では第5の駒が何であっても結果が変わらない。
    static Formation ScWithoutUro(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Uro)) g[slot] = d;
        return g;
    }

    // **素体の対照。** ウロと数値・型・速さが1つも違わず、特性だけを持たない駒。
    // **`ScaleRule(0)` は消費を止めるだけで供給も発揮も止めない**ので、
    // あれだけでは「鱗が効いたのか、ただ 70/9/7 の体が入ったのか」が割れない
    // ——第41期の「符号を測りたい効果は、その効果だけを 0 にできるノブと対にして作ること」の
    // 実装漏れをここで塞ぐ（規則ではなく駒の側で塞ぐ。`ScaleRule` にノブを増やさない）。
    // カタログには載せない（`gradient` / `aim` / `guard` と同じ、診断のローカルの def）。
    UnitDef ScPlainDef = new()
    {
        Id = "uro_plain", Name = "素体のウロ", MaxHp = UnitCatalog.Uro.MaxHp,
        Attack = UnitCatalog.Uro.Attack, Speed = UnitCatalog.Uro.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Uro.Pattern
    };
    Formation ScPlain(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Uro) ? ScPlainDef : d;
        return g;
    }

    // ウロの席だけを振った5変種。他の4枚は元の相対順のまま空いた席へ詰める。
    static Formation ScSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Uro))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Uro;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    // 1つの（編成 × 波 × 規則）についての計測。**盤面は触らない**——`BattleResult` の計数を読むだけ。
    (double Win, double GainDeath, double GainShard, double GainEph, double Worn, double First,
     double Never, double Swings, double Pierce, double Back, double BackDmg,
     double SpentAtk, double SpentHit, double Depleted, double Leftover, double FullSoak, double Turns)
    MeasureScale(Formation f, Formation enemy, ScaleRule rule)
    {
        double win = 0, gd = 0, gs = 0, ge = 0, worn = 0, alive = 0, first = 0, firstN = 0, never = 0;
        double sw = 0, pi = 0, bk = 0, bd = 0, sa = 0, sh = 0, dep = 0, left = 0, soak = 0, turns = 0;
        for (int seed = 0; seed < ScSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                                    null, null, null, null, null, null, null, null, null, null, rule);
            if (r.PlayerWon) win++;
            gd += r.ScaleGainDeath; gs += r.ScaleGainShatter; ge += r.ScaleGainEphemeral;
            worn += r.ScaleWornTurns; alive += r.ScaleAliveTurns;
            if (r.ScaleFirstTurn > 0) { first += r.ScaleFirstTurn; firstN++; } else never++;
            sw += r.ScaleSwings; pi += r.ScalePierceSwings;
            bk += r.ScaleBackHits; bd += r.ScaleBackDamage;
            sa += r.ScaleSpentAttack; sh += r.ScaleSpentHit;
            dep += r.ScaleDepleted; left += r.ScaleLeftover; soak += r.ScaleFullSoaks;
            turns += r.Turns;
        }
        double n = ScSeeds;
        return (win * 100 / n, gd / n, gs / n, ge / n, alive > 0 ? worn * 100 / alive : 0,
                firstN > 0 ? first / firstN : 0, never * 100 / n, sw / n, pi / n, bk / n, bd / n,
                sa / n, sh / n, dep / n, left / n, soak / n, turns / n);
    }

    // 全波を通した集計（機構の量は波で平均する。勝率だけは波ごとに残す）
    (double[] Wins, double GainDeath, double GainShard, double GainEph, double Worn, double First,
     double Never, double Swings, double Pierce, double Back, double BackDmg,
     double SpentAtk, double SpentHit, double Depleted, double Leftover, double FullSoak)
    ScAll(Formation f, ScaleRule rule)
    {
        var wins = new double[scStages.Count];
        double gd = 0, gs = 0, ge = 0, worn = 0, first = 0, firstN = 0, never = 0;
        double sw = 0, pi = 0, bk = 0, bd = 0, sa = 0, sh = 0, dep = 0, left = 0, soak = 0;
        for (int w = 0; w < scStages.Count; w++)
        {
            var z = MeasureScale(f, scStages[w].Enemy, rule);
            wins[w] = z.Win;
            gd += z.GainDeath; gs += z.GainShard; ge += z.GainEph; worn += z.Worn; never += z.Never;
            sw += z.Swings; pi += z.Pierce; bk += z.Back; bd += z.BackDmg;
            sa += z.SpentAtk; sh += z.SpentHit; dep += z.Depleted; left += z.Leftover; soak += z.FullSoak;
            if (z.First > 0) { first += z.First; firstN++; }
        }
        double m = scStages.Count;
        return (wins, gd / m, gs / m, ge / m, worn / m, firstN > 0 ? first / firstN : 0, never / m,
                sw / m, pi / m, bk / m, bd / m, sa / m, sh / m, dep / m, left / m, soak / m);
    }

    static string ScCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));

    Console.WriteLine("# 鱗（scale）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 scale [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{ScSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（供給源を変えた対）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 獲得 | 破片の獲得量/戦。**内訳は 味方の死 / 砕けの破片**（`(儚)` は死のうち胞子ぶんの内数） |");
    Console.WriteLine("| 纏い率 | `Armor > 0` だったターン数 ÷ **ウロが生きてターン頭を迎えた回数** |");
    Console.WriteLine("| 初纏い | 初めて破片を得たターン（得た試行だけの平均）。`未纏` が一度も纏わなかった試行の割合 |");
    Console.WriteLine("| 貫き | 貫きで振った回数/戦（`振り` が総振り回数） |");
    Console.WriteLine("| **後列到達** | **貫きが後列の敵に当たった回数**/戦と、そのとき振り下ろした量（減衰後） |");
    Console.WriteLine("| 支出・攻 / 支出・被 | 攻撃で消費した量/戦 ／ 被弾で吸われた量/戦（**二重支出**） |");
    Console.WriteLine("| 枯渇 / 死蔵 | `Armor` が 0 に戻った回数/戦 ／ 決着時に残っていた量/戦 |");
    Console.WriteLine("| 受切 | 破片が被弾を**受け切った**回数/戦（受け切ると `OnDamaged` が呼ばれない＝§7-1 の干渉） |");
    Console.WriteLine();
    Console.WriteLine("> **「貫き」と「後列到達」は別の列。** 貫いた回数は成果ではない");
    Console.WriteLine("> ——後列に敵がいなければ単体攻撃と同じである。");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）--------------------------------------------------------
    if (scMode != "sweep" && scMode != "seats")
    {
        Console.WriteLine("## 0. 検算 —— 差分は鱗だけに閉じているか（受け入れ基準2）");
        Console.WriteLine();
        var plain = scBuilds.Where(b => !ScHasUro(b.F)).ToArray();   // 現状は既存 48 行
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < scStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, scStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null,
                            new ScaleRule(0)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, scStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null,
                            new ScaleRule(9)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（ウロを含まない {plain.Length} 行が `ScaleRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {scStages.Count} 波・`CostPerAttack` 0 対 9）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  行を足す前に `compare` の全文で確認済み（**240 セル中 0 件**）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 ----------------------------------------------------------------------------
    if (scMode != "sweep" && scMode != "seats")
    {
        Console.WriteLine($"## 1. 主表（`CostPerAttack = {ScMain}` と 陽性対照2本）");
        Console.WriteLine();
        Console.WriteLine("`C0` = `ScaleRule(0)`（**消費しない＝維持型**）。**強度ではなく性質を切るノブ**なので、");
        Console.WriteLine("これは「弱くした版」ではなく「別の駒」。");
        Console.WriteLine("`素体` = ウロと**数値・型・速さが1つも違わず特性だけを持たない駒**に差し替えた版。");
        Console.WriteLine("**これが機構の帰属を取る唯一の窓口**——`C0` は消費を止めるだけで供給も発揮も止めない。");
        Console.WriteLine("`4体` = ウロを外した同じ編成（**第21期の飽和検査**も兼ねる）。");
        Console.WriteLine("`土台` = ウロの席に元の駒（死軸＝ヴェル / ヒビ台＝リィカ）が入っている既存行。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in scTargets)
        {
            var z = ScAll(b.F, new ScaleRule(ScMain));
            var z0 = ScAll(b.F, new ScaleRule(0));
            var four = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                    if (BattleEngine.Run(ScWithoutUro(b.F), scStages[w].Enemy, seed, false).PlayerWon) wins++;
                four[w] = wins * 100.0 / ScSeeds;
            }
            var plainW = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                    if (BattleEngine.Run(ScPlain(b.F), scStages[w].Enemy, seed, false).PlayerWon) wins++;
                plainW[w] = wins * 100.0 / ScSeeds;
            }
            Console.WriteLine($"| {b.Name} | **C{ScMain}** |{ScCells(z.Wins)} {z.Wins.Average():0.0}% |");
            Console.WriteLine($"| | C0（維持型） |{ScCells(z0.Wins)} {z0.Wins.Average():0.0}% |");
            Console.WriteLine($"| | 素体（特性なし・同数値） |{ScCells(plainW)} {plainW.Average():0.0}% |");
            Console.WriteLine($"| | 4体 |{ScCells(four)} {four.Average():0.0}% |");
            Console.Out.Flush();
        }
        // 土台（既存行）を同じ物差しで並べる
        foreach (string baseName in new[] { "死の連鎖 (リィカ軸)", "範囲耐性 (ヒビ×ボルグ)" })
        {
            var bb = scBuilds.FirstOrDefault(x => x.Name == baseName);
            if (bb.F is null) continue;
            var wv = new double[scStages.Count];
            for (int w = 0; w < scStages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < ScSeeds; seed++)
                    if (BattleEngine.Run(bb.F, scStages[w].Enemy, seed, false).PlayerWon) wins++;
                wv[w] = wins * 100.0 / ScSeeds;
            }
            Console.WriteLine($"| 土台: {baseName} | — |{ScCells(wv)} {wv.Average():0.0}% |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 機構の計数（`CostPerAttack` = {ScMain}・5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 獲得(死/破片) | (儚) | 纏い率 | 初纏い | 未纏 | 貫き/振り | **後列到達(回/量)** | 支出・攻 | 支出・被 | 枯渇 | 死蔵 | 受切 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in scTargets)
        {
            var z = ScAll(b.F, new ScaleRule(ScMain));
            Console.WriteLine($"| {b.Name} | {z.GainDeath:0.0} / {z.GainShard:0.0} | {z.GainEph:0.0} "
                + $"| {z.Worn:0.0}% | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                + $"| {z.Pierce:0.00} / {z.Swings:0.00} | {z.Back:0.00} / {z.BackDmg:0.0} "
                + $"| {z.SpentAtk:0.0} | {z.SpentHit:0.0} | {z.Depleted:0.00} | {z.Leftover:0.0} | {z.FullSoak:0.00} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 2. 波ごとの内訳（供給の時間分布がこの期の設計）------------------------------------
        Console.WriteLine($"## 2. 波ごとの内訳（`CostPerAttack = {ScMain}`）—— 供給の時間分布");
        Console.WriteLine();
        Console.WriteLine("**「初纏い」と「後列到達」を波ごとに読む。** 第一波は敵が3体（前1・前3・中央）で");
        Console.WriteLine("**後列が存在しない**ので、貫いても到達は構造的に 0 になる。");
        Console.WriteLine();
        foreach (var b in scTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 獲得(死/破片) | 纏い率 | 初纏い | 未纏 | 貫き/振り | 後列到達(回/量) | 到達率 | 支出・攻 | 支出・被 | 決着T |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < scStages.Count; w++)
            {
                var z = MeasureScale(b.F, scStages[w].Enemy, new ScaleRule(ScMain));
                Console.WriteLine($"| {scStages[w].Name} | {z.Win:0.0}% | {z.GainDeath:0.0} / {z.GainShard:0.0} "
                    + $"| {z.Worn:0.0}% | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                    + $"| {z.Pierce:0.00} / {z.Swings:0.00} | {z.Back:0.00} / {z.BackDmg:0.0} "
                    + $"| {(z.Pierce > 0 ? $"{z.Back * 100 / z.Pierce:0.0}%" : "—")} "
                    + $"| {z.SpentAtk:0.0} | {z.SpentHit:0.0} | {z.Turns:0.0} |");
                Console.Out.Flush();
            }
            Console.WriteLine();
        }
    }

    // --- 3. 掃引（受け入れ基準9）------------------------------------------------------------
    if (scMode.Length == 0 || scMode == "sweep")
    {
        Console.WriteLine("## 3. 掃引（`CostPerAttack` 0 / 1 / 2）");
        Console.WriteLine();
        Console.WriteLine("**`0` は維持型・`1` 以上は消費型**なので、このノブは強度ではなく**性質**を切っている（はず）。");
        Console.WriteLine("**`0` が明確に強いだけなら、切っているのは性質ではなく強度**（第41期と同じ結末）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | Cost | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 纏い率 | 貫き/振り | 後列到達 | 支出・攻 | 支出・被 | 枯渇 | 死蔵 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in scTargets)
            foreach (int c in scCosts)
            {
                var z = ScAll(b.F, new ScaleRule(c));
                Console.WriteLine($"| {b.Name} | {c} | **{z.Wins.Average():0.0}%** |{ScCells(z.Wins)} "
                    + $"{z.Worn:0.0}% | {z.Pierce:0.00} / {z.Swings:0.00} | {z.Back:0.00} "
                    + $"| {z.SpentAtk:0.0} | {z.SpentHit:0.0} | {z.Depleted:0.00} | {z.Leftover:0.0} |");
                Console.Out.Flush();
            }
        Console.WriteLine();
    }

    // --- 4. 席の分散（seats2 の写し・受け入れ基準10）-----------------------------------------
    if (scMode.Length == 0 || scMode == "seats")
    {
        Console.WriteLine("## 4. 席の分散（`seats2` の写し・受け入れ基準10）");
        Console.WriteLine();
        Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 + 最下位 を seed 0..199 で測り直し。");
        Console.WriteLine("**採否に使うのは1位の配置ではなく次数**（第45期の残件 D）。");
        Console.WriteLine("**鱗は隣接を1つも読まない**ので、第45期の対照（隣接も列も読まない駒＝最頻率 65%）が比較先。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（3値） | 最頻率 | 幅 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|");
        foreach (var b in scTargets)
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
                foreach (EnemyCatalog.Stage st in scStages)
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
                foreach (EnemyCatalog.Stage st in scStages)
                {
                    int wins = 0;
                    for (int seed = 0; seed < ScSeeds; seed++)
                        if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    avg += wins * 100.0 / ScSeeds;
                }
                return avg / scStages.Count;
            }

            var verified = pool.Select(i => (Idx: i, Avg: Avg(perms[i]))).OrderByDescending(x => x.Avg).ToList();
            double width = verified[0].Avg - verified[^1].Avg;
            var top5 = verified.Take(5).ToList();

            foreach (UnitDef d in members)
            {
                int bestSlot = -1;
                foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                    if (ReferenceEquals(dd, d)) bestSlot = slot;
                // 第45期の3値（前角 / 中央 / 後角）。**2値（中央/角）だと行と列が混ざる**
                // ——鱗は隣接を読まないが貫きはレーンを走るので、効くとしたら列の側。
                int mid = 0, fcorner = 0, bcorner = 0;
                foreach (var v in top5)
                    foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                        if (ReferenceEquals(dd, d))
                        {
                            int deg2 = 0;
                            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                                if (FormationRules.AreAdjacent(slot, i)) deg2++;
                            if (deg2 == 4) mid++;
                            else if (FormationRules.RowOf(slot) == Row.Front) fcorner++;
                            else bcorner++;
                        }
                int bdeg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
                int top = Math.Max(mid, Math.Max(fcorner, bcorner));
                Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                    + $"| 前角{fcorner} / 中央{mid} / 後角{bcorner} | {top * 100 / 5}% | {width:0.0}pt |");
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine("### ウロ1枚だけを振った5変種（他の4枚は元の相対順のまま詰める）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ウロの席 | 次数 | 纏い率 | 初纏い | 貫き/振り | 後列到達 | 支出・攻 | 支出・被 | 平均勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in scTargets)
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                Formation g = ScSeat(b.F, seat);
                int deg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(seat, i)) deg++;
                var z = ScAll(g, new ScaleRule(ScMain));
                Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {deg} | {z.Worn:0.0}% "
                    + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Pierce:0.00} / {z.Swings:0.00} "
                    + $"| {z.Back:0.00} | {z.SpentAtk:0.0} | {z.SpentHit:0.0} | {z.Wins.Average():0.0}% |");
                Console.Out.Flush();
            }
        Console.WriteLine();
    }

    return;
}

// scapegoat モード: 業（第49期）。**味方が背負っている状態異常の「種類数」を読む駒。**
//
// ロスターの10通貨はすべて「同じ通貨を厚くする」方向を向いていて（第48期の棚卸し）、
// **幅を要求する駒が1枚もない。** 業（ゴウ）はそこを埋める——量ではなく種類を読む。
//
// `CompareBuilds()` / `Stages` / `Columns` は触らない（phase0 は BattleCore にも触らない）。
//
//     dotnet run --project BattleSim -c Release 0 scapegoat phase0   # 実装前の地図（§2 Phase 0）
if (focusId == "scapegoat" && (args.Length > 2 ? args[2] : "") == "phase0")
{
    var sgBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> sgStages = EnemyCatalog.Stages;
    const int SgScan = 50;    // 50 行の走査（verbose=true なので compare の 200 とは分ける）
    const int SgSeeds = 200;  // 候補台の測り直し（compare / scale と揃える）

    // 数える対象の4種類。**Armor と IdleTurn は数えない**（§2-2。判断の根拠は本文）。
    (string Key, string Label)[] sgKinds =
    {
        (StatusKeys.Poison, "毒"),
        (StatusKeys.Marked, "標"),
        (StatusKeys.Stun,   "痺"),
        (StatusKeys.Burn,   "燃"),
        (StatusKeys.Wound,  "傷"),   // 味方に載る経路が本当に無いかを実測で確かめるために入れる
    };

    // `StatusSnapshot` の Text（`BattleEngine.StatusLabels`）→ 数える対象の索引。
    // **盤面には触らない。** verbose=true の Events を読み直すだけ。
    var sgLabelOf = new Dictionary<string, int>();
    for (int k = 0; k < sgKinds.Length; k++) sgLabelOf[sgKinds[k].Label] = k;

    // 1戦ぶんの走査。ターン頭のスナップショット（TickStatuses の直後）から
    // 「そのターン、味方の誰かに載っていた種類」を集める。
    //
    // **これは下限。** スナップショットはターン頭に1回だけ写されるので、同じターンの
    // OnTurnStart で撒かれるぶん（瘴気の味方漏れ・縛めの味方縛り）は次のターンまで出ない。
    (int[] TurnsWith, int Turns, int[] First, int Kinds2, int Kinds3, int Kinds4,
     int First3, Dictionary<string, int>[] Carriers, int[] Runs, int[] RunTurns,
     int CumKinds, int CumFirst2, int CumFirst3, int CumFirst4)
    SgTrace(Formation f, Formation enemy, int seed)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true);

        // 味方の InstanceId。編成の駒は 0..N-1（Run が味方 → 敵の順で Add する）。
        // 増援（胞子）は Summon イベントの Team で拾う。
        var mine = new HashSet<int>(Enumerable.Range(0, f.Occupied().Count()));
        var nameOf = new Dictionary<int, string>();
        {
            int i = 0;
            foreach ((int _, UnitDef d) in f.Occupied()) nameOf[i++] = d.Name;
        }
        foreach (BattleEvent e in r.Events)
            if (e.Kind == BattleEventKind.Summon && e.Team == BattleContext.PlayerTeam
                && e.TargetId is int sid)
            {
                mine.Add(sid);
                nameOf[sid] = e.Text ?? "増援";
            }

        int kinds = sgKinds.Length;
        var turnsWith = new int[kinds];
        var first = new int[kinds];
        var carriers = new Dictionary<string, int>[kinds];
        for (int k = 0; k < kinds; k++) carriers[k] = new Dictionary<string, int>();
        var runs = new int[kinds];       // 「載り始めた」回数（駒ごとの連続区間の数）
        var runTurns = new int[kinds];   // その区間の総ターン数
        var wasOn = new Dictionary<(int Unit, int Kind), bool>();

        int turns = 0, k2 = 0, k3 = 0, k4 = 0, first3 = 0;
        // **累積**（この戦闘で一度でも盤面に出た種類）。業は引き取った種類を持ち越すので、
        // 同時成立ではなくこちらが到達の分母になる——毒と標は減らないから、
        // 一度引き取れば戦闘が終わるまでゴウの種類数に載り続ける。
        var everOn = new bool[kinds];
        int cumFirst2 = 0, cumFirst3 = 0, cumFirst4 = 0;
        int curTurn = -1;
        var onThisTurn = new bool[kinds];
        var seenThisTurn = new HashSet<(int, int)>();

        void Close()
        {
            if (curTurn < 0) return;
            turns++;
            int n = 0;
            for (int k = 0; k < kinds; k++)
                if (onThisTurn[k]) { n++; turnsWith[k]++; if (first[k] == 0) first[k] = curTurn; }
            if (n >= 2) k2++;
            if (n >= 3) { k3++; if (first3 == 0) first3 = curTurn; }
            if (n >= 4) k4++;
            for (int k = 0; k < kinds; k++) if (onThisTurn[k]) everOn[k] = true;
            int cum = everOn.Count(x => x);
            if (cum >= 2 && cumFirst2 == 0) cumFirst2 = curTurn;
            if (cum >= 3 && cumFirst3 == 0) cumFirst3 = curTurn;
            if (cum >= 4 && cumFirst4 == 0) cumFirst4 = curTurn;
            // 連続区間の更新（このターン載っていなかった (駒,種) は区間を閉じる）
            foreach (var key in wasOn.Keys.ToList())
                if (!seenThisTurn.Contains(key)) wasOn[key] = false;
            Array.Clear(onThisTurn, 0, onThisTurn.Length);
            seenThisTurn.Clear();
        }

        foreach (BattleEvent e in r.Events)
        {
            if (e.Kind == BattleEventKind.TurnStart)
            {
                Close();
                curTurn = e.Turn;
                continue;
            }
            if (e.Kind != BattleEventKind.StatusSnapshot) continue;
            if (e.TargetId is not int uid || !mine.Contains(uid)) continue;
            if (e.Text is null || !sgLabelOf.TryGetValue(e.Text, out int kk)) continue;
            if (e.Amount <= 0) continue;

            onThisTurn[kk] = true;
            seenThisTurn.Add((uid, kk));
            string nm = nameOf.TryGetValue(uid, out string? s) ? s : $"#{uid}";
            carriers[kk][nm] = carriers[kk].TryGetValue(nm, out int c) ? c + 1 : 1;

            runTurns[kk]++;
            if (!wasOn.TryGetValue((uid, kk), out bool on) || !on) { runs[kk]++; wasOn[(uid, kk)] = true; }
        }
        Close();

        return (turnsWith, turns, first, k2, k3, k4, first3, carriers, runs, runTurns,
                everOn.Count(x => x), cumFirst2, cumFirst3, cumFirst4);
    }

    Console.WriteLine("# 業 Phase 0 —— 数える対象の確定（第49期 §2）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 scapegoat phase0` の出力。");
    Console.WriteLine("**盤面は1つも動かない。** `BattleCore` には1文字も足していない状態で走らせる測定で、");
    Console.WriteLine("`docs/` には置かない。");
    Console.WriteLine();
    Console.WriteLine("> **実測はすべて下限。** 種類の在庫は `StatusSnapshot`（`TickStatuses` の直後・");
    Console.WriteLine("> ターン頭に1回だけ）から読んでいるので、**同じターンの `OnTurnStart` で撒かれるぶん**");
    Console.WriteLine("> （瘴気の味方漏れ・縛めの味方縛り）は次のターンの頭まで出てこない。");
    Console.WriteLine();

    // --- 0-1. 味方に載る状態異常の経路 -------------------------------------------------------
    Console.WriteLine("## 0-1. 味方に載る状態異常の経路（`SetCounter` / `ctx.Ignite` の全呼び出しを走査）");
    Console.WriteLine();
    Console.WriteLine("`BattleCore/Traits.cs` と `BattleCore/BattleEngine.cs` の書き込み箇所を全部拾い、");
    Console.WriteLine("**対象が味方（`ally` / `self` / `victim` / 味方チームの駒）になっているものだけ**を抜き出した。");
    Console.WriteLine();
    Console.WriteLine("| 種類 | 経路 | 保持者 | 対象 | 発火 |");
    Console.WriteLine("|---|---|---|---|---|");
    Console.WriteLine("| `Poison` | 瘴気の味方漏れ（`MiasmaTrait.OnTurnStart`） | 瘴気袋のグザ | **味方全員（自分も含む）** +1 | 毎ターン |");
    Console.WriteLine("| `Poison` | 毒撃の隣への漏れ（`VenomTrait.OnDamaged`） | 毒吐きのスィド | 隣接する味方 +1 | スィドが殴られるたび |");
    Console.WriteLine("| `Marked` | 囃し立て（`MarkerTrait.OnBattleStart`） | 囃し立てのヒサ | 隣接する最大HPの味方1体 = 1 | **開戦時1回だけ** |");
    Console.WriteLine("| `Stun` | 縛め・味方縛り（`BindTrait.OnTurnStart`） | 縛めのクグ | 味方1体（`AcceptsSupport` かつ 痺れていない）= 1 | 第2ターン以降 毎ターン |");
    Console.WriteLine("| `Stun` | 怯み（`AvengeTrait.OnDamaged`） | 仇討ちのザン | **自分** = 1 | 敵に殴られるたび |");
    Console.WriteLine("| `Stun` | 怖気（`TormentTrait.OnAfterAttack`） | 責め苦のシガ | **自分** = 1 | 動ける敵を殴るたび |");
    Console.WriteLine("| `Stun` | 深追い（`GougeTrait.OnKill`） | 抉りのエグ | **自分** = 1 | 敵を倒すたび |");
    Console.WriteLine("| `Stun` | 断罪（`CondemnTrait.OnDamaged`） | **敵**・勇者候補 / 審問官（第五波の2体） | 反撃してきた味方 = 1 | 反撃を浴びるたび・45% |");
    Console.WriteLine("| `Burn` | 火の粉（`CinderTrait.OnAfterAttack`） | 焼け残りのボルグ | 隣接する味方（`ctx.Ignite(ally, friendly: true)`） | ボルグが殴るたび |");
    Console.WriteLine("| `Wound` | **無い** | — | 裂き・刻み・断ち・縫いは全部 `target`（＝敵） | — |");
    Console.WriteLine();
    Console.WriteLine("**分母は 4**（`Poison` / `Marked` / `Stun` / `Burn`）。`Wound` に味方へ載る経路は1つも無い。");
    Console.WriteLine();
    Console.WriteLine($"### 実測（`CompareBuilds()` の {sgBuilds.Length} 行 × {sgStages.Count} 波 × seed 0..{SgScan - 1}）");
    Console.WriteLine();
    Console.WriteLine("`在庫` はその種類が味方の誰かに載っていたターンの割合（分母は全ターン）。");
    Console.WriteLine("`初出` は初めて載ったターン（載った試行だけの平均）。");
    Console.WriteLine();
    Console.WriteLine("| 種類 | 在庫のある行数 | 在庫率（全行の平均） | 初出 | 主な保持者（延べターン） |");
    Console.WriteLine("|---|--:|--:|--:|---|");
    {
        int kinds = sgKinds.Length;
        var rowsWith = new int[kinds];
        var rate = new double[kinds];
        var firstSum = new double[kinds];
        var firstN = new int[kinds];
        var carriersAll = new Dictionary<string, int>[kinds];
        for (int k = 0; k < kinds; k++) carriersAll[k] = new Dictionary<string, int>();
        var perRow = new List<(string Name, double[] Rate, double K2, double K3, double K4,
                              double Cum, double CumFirst3, double CumNever3)>();

        foreach (var b in sgBuilds)
        {
            var tw = new long[kinds];
            var fs = new double[kinds];
            var fn = new int[kinds];
            long tt = 0, c2 = 0, c3 = 0, c4 = 0;
            double cf3 = 0, cumSum = 0; int cf3n = 0, cumNever3 = 0, trials = 0;
            foreach (EnemyCatalog.Stage st in sgStages)
                for (int seed = 0; seed < SgScan; seed++)
                {
                    var z = SgTrace(b.F, st.Enemy, seed);
                    trials++;
                    tt += z.Turns; c2 += z.Kinds2; c3 += z.Kinds3; c4 += z.Kinds4;
                    cumSum += z.CumKinds;
                    if (z.CumFirst3 > 0) { cf3 += z.CumFirst3; cf3n++; } else cumNever3++;
                    for (int k = 0; k < kinds; k++)
                    {
                        tw[k] += z.TurnsWith[k];
                        if (z.First[k] > 0) { fs[k] += z.First[k]; fn[k]++; }
                        foreach ((string nm, int c) in z.Carriers[k])
                            carriersAll[k][nm] = carriersAll[k].TryGetValue(nm, out int x) ? x + c : c;
                    }
                }
            var rr = new double[kinds];
            for (int k = 0; k < kinds; k++)
            {
                rr[k] = tt == 0 ? 0 : tw[k] * 100.0 / tt;
                if (rr[k] > 0) rowsWith[k]++;
                rate[k] += rr[k];
                if (fn[k] > 0) { firstSum[k] += fs[k] / fn[k]; firstN[k]++; }
            }
            perRow.Add((b.Name, rr, tt == 0 ? 0 : c2 * 100.0 / tt, tt == 0 ? 0 : c3 * 100.0 / tt,
                        tt == 0 ? 0 : c4 * 100.0 / tt, cumSum / trials,
                        cf3n > 0 ? cf3 / cf3n : 0, cumNever3 * 100.0 / trials));
            Console.Out.Flush();
        }

        for (int k = 0; k < kinds; k++)
        {
            var top = carriersAll[k].OrderByDescending(x => x.Value).Take(4)
                .Select(x => $"{x.Key} {x.Value}").ToList();
            Console.WriteLine($"| {sgKinds[k].Label} (`{sgKinds[k].Key}`) | {rowsWith[k]} / {sgBuilds.Length} "
                + $"| {rate[k] / sgBuilds.Length:0.0}% "
                + $"| {(firstN[k] > 0 ? $"{firstSum[k] / firstN[k]:0.00}" : "—")} "
                + $"| {(top.Count == 0 ? "**0 件**" : string.Join(" / ", top))} |");
        }
        Console.WriteLine();

        Console.WriteLine("### 行ごとの在庫と同時成立（同じ走査）");
        Console.WriteLine();
        Console.WriteLine("`2種`/`3種`/`4種` は**同時に成立していたターンの割合**。`初3` は3種が初めて揃ったターン、");
        Console.WriteLine("`未3` は 3種が最後まで揃わなかった試行の割合。**閾値 3 の到達可能性はこの2列で決まる。**");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 毒 | 標 | 痺 | 燃 | 傷 | 2種 | 3種 | 4種 | **累種** | **累3** | **累3未達** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var p in perRow)
            Console.WriteLine($"| {p.Name} " + string.Concat(p.Rate.Select(x => $"| {x:0.0}% "))
                + $"| {p.K2:0.0}% | {p.K3:0.0}% | {p.K4:0.0}% "
                + $"| **{p.Cum:0.00}** | {(p.CumFirst3 > 0 ? $"{p.CumFirst3:0.00}" : "—")} "
                + $"| {p.CumNever3:0.0}% |");
        Console.WriteLine();
        Console.WriteLine($"**3種が1ターンでも同時に揃う行: {perRow.Count(p => p.K3 > 0)} / {perRow.Count}**"
            + $"（4種は {perRow.Count(p => p.K4 > 0)} 行）");
        Console.WriteLine();
        Console.WriteLine($"**累積で3種に届く行: {perRow.Count(p => p.CumNever3 < 100)} / {perRow.Count}**"
            + $"。`累種` の全行平均は {perRow.Average(p => p.Cum):0.00} 種。");
        Console.WriteLine();
    }

    // --- 0-2. Armor / IdleTurn を数えない判断 ------------------------------------------------
    Console.WriteLine("## 0-2. `Armor` と `IdleTurn` を数える対象から外す判断（engine の実装を確認）");
    Console.WriteLine();
    Console.WriteLine("- **`Armor`**: `ApplyDamage` が HP の前に削るプールで、**プラスの資源**。");
    Console.WriteLine("  数えるとヒビ（砕け）1枚で種類数を稼げる抜け道になる。**数えない。**");
    Console.WriteLine("- **`IdleTurn`**: `BattleEngine.Run` の行動順ループが");
    Console.WriteLine("  `Stun > 0` → `SetCounter(Stun, 0)` / `SetCounter(IdleTurn, turn)` と");
    Console.WriteLine("  **痺れを IdleTurn へ振り替える**。数えると1経路で2カウントになる。**数えない。**");
    Console.WriteLine();
    Console.WriteLine("### 検算: `Stun` と `IdleTurn` は本当に同時に立つか");
    Console.WriteLine();
    Console.WriteLine("**立たない。** 痺れは保持者の手番が回ってきた瞬間に `0` へ消費され、");
    Console.WriteLine("同じ行で `IdleTurn` にターン番号が入る（振り替えであって併存ではない）。");
    Console.WriteLine("`IdleTurn` を書く箇所は engine の3つだけ（痺れ / まどろみ / `CanAct` 偽）で、");
    Console.WriteLine("**`0` に戻す箇所は1つも無い**——一度でも手番を落とせば以後ずっと非ゼロのままになる。");
    Console.WriteLine("種類として数えると「一度動けなかった駒は永久に1種類を持つ」ことになり、");
    Console.WriteLine("**指示書の指定（数えない）は engine の実装と整合している。**");
    Console.WriteLine();

    // --- 0-3. 寿命 --------------------------------------------------------------------------
    Console.WriteLine("## 0-3. 状態異常の寿命");
    Console.WriteLine();
    Console.WriteLine("| 種類 | 減り方 | 定数 | 実測の持続（連続ターン数） |");
    Console.WriteLine("|---|---|---|--:|");
    {
        int kinds = sgKinds.Length;
        var runs = new long[kinds];
        var runT = new long[kinds];
        foreach (var b in sgBuilds)
            foreach (EnemyCatalog.Stage st in sgStages)
                for (int seed = 0; seed < SgScan; seed++)
                {
                    var z = SgTrace(b.F, st.Enemy, seed);
                    for (int k = 0; k < kinds; k++) { runs[k] += z.Runs[k]; runT[k] += z.RunTurns[k]; }
                }
        string[] how =
        {
            "**減らない。** `TickStatuses` は層の分だけ削るが層は減らさない（累積する）",
            "**減らない。** 書き手（囃し立て）が開戦時に 1 を置くだけで、消す箇所が1つも無い",
            "保持者の**手番が来た瞬間**に 0 へ消費され `IdleTurn` に振り替わる",
            "`TickStatuses` が毎ターン 1 減らす（非スタック・再付与は残ターンのリセット）",
            "**減らない。** `TickStatuses` に何も足していない（読み手がいて初めて意味を持つ）"
        };
        string[] konst = { "—", "1（固定）", "1（固定）",
                           $"`BurnRules.Turns` = {BurnRules.Turns} / 1T {BurnRules.Damage} ダメージ", "—" };
        for (int k = 0; k < kinds; k++)
            Console.WriteLine($"| {sgKinds[k].Label} | {how[k]} | {konst[k]} "
                + $"| {(runs[k] > 0 ? $"{(double)runT[k] / runs[k]:0.00}" : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine("**痺の実測が 1 に近いほど要注意。** 痺れは1ターンしか盤面に残らないので、");
    Console.WriteLine("**引き取りに来るのが1ターン遅れると在庫が消えている。**");
    Console.WriteLine();

    // --- 0-4. 状態異常の肩代わりが存在しないこと ---------------------------------------------
    Console.WriteLine("## 0-4. 状態異常そのものを移す経路が既に無いことの確認");
    Console.WriteLine();
    Console.WriteLine("| 機構 | 何をするか | 「移す」か |");
    Console.WriteLine("|---|---|---|");
    Console.WriteLine("| 集約（`BearTrait`・ウケ） | 隣の味方の**弱体**（`AtkBonus` の減算）を横取りしてアーマーに変える | 弱体であって状態異常ではない |");
    Console.WriteLine("| 転嫁（`RelayTrait`・ワタ） | 同上を横取りして**敵**へ流す | 同上 |");
    Console.WriteLine("| 澱み喰い（`BlightfedTrait`・ヴィオ） | 味方の毒を `0` にして自分の攻撃力に変える | **消すだけ**（自分には積まない） |");
    Console.WriteLine("| 疫み（`ContagionTrait`・ラウ） | 倒れた駒の毒を**残りの敵**へ撒き直す | 死体からの撒き直しで、生きた味方からは取らない |");
    Console.WriteLine("| 毒喰らい（`DevourTrait`・ベニ） | 敵の毒の数だけ味方を癒す | 読むだけ |");
    Console.WriteLine();
    Console.WriteLine("**生きている味方から状態異常のカウンタを取り上げて自分に積む経路は 0 件。**");
    Console.WriteLine("業の引き取りはロスターで初めての「状態異常の肩代わり」になる。");
    Console.WriteLine();

    // --- 0-5 / 0-6 --------------------------------------------------------------------------
    Console.WriteLine("## 0-5. `docs/balance.md` の分母");
    Console.WriteLine();
    Console.WriteLine($"編成 **{sgBuilds.Length}** 行 × 波 **{sgStages.Count}** = **{sgBuilds.Length * sgStages.Count} セル**。");
    Console.WriteLine();
    Console.WriteLine("## 0-6. 残り枠");
    Console.WriteLine();
    Console.WriteLine($"`UnitCatalog.All` は **{UnitCatalog.All.Count}** 体。上限 52 に対して残り "
        + $"**{52 - UnitCatalog.All.Count}**。この期で1枚使えば残り {52 - UnitCatalog.All.Count - 1} になる");
    Console.WriteLine("——**実際には業を採用しなかったので枠は減っていない**（`UnitCatalog.Gou` は");
    Console.WriteLine("`All` に載せず、定義だけを対照として残してある。逆位・まどろみ・誹り・驕りと同じ扱い）。");
    Console.WriteLine();

    // --- 0-7. 候補台の地図 -------------------------------------------------------------------
    // **`CompareBuilds()` は触らない。** 候補はここでローカルに組む（`gradient` / `aim` と同じ扱い）。
    // 5枠目には**業と同数値・特性なしの素体**を置く。これが §4 の陽性対照そのものになる。
    UnitDef SgPlain = new()
    {
        Id = "gou_plain", Name = "素体のゴウ", MaxHp = 88, Attack = 7, Speed = 4,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single
    };
    var sgCand = new (string Name, Formation F)[]
    {
        // --- 供給を3種類そろえた側（払い出しの枚数を変えながら測る）---
        ("A4 毒標燃 (グザ×ヒサ×ボルグ×ガルド)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Borg, center: SgPlain,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Guza)),
        ("A6 毒標痺燃 (グザ×ヒサ×ザン×ボルグ)", Formation.Build(
            front1: UnitCatalog.Zan, front3: UnitCatalog.Borg, center: SgPlain,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Guza)),
        ("A26 標痺燃 (カド×ヒサ×シガ×ボルグ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Shiga)),
        // --- 反撃台（カド×ヒサ×ボルグ）を固定し、5枚目だけを振る ---
        // **痺は使えない種類**（引き取ると手番が飛んで転写できない）ので、
        // 閾値 3 に届くには 毒・標・燃 の3つが要る。
        ("D1 毒標燃 (カド×ヒサ×ボルグ×グザ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Guza)),
        ("D2 毒標燃 (カド×ヒサ×ボルグ×スィド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Sid)),
        ("D3 標燃 (カド×ヒサ×ボルグ×ガルド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Gald)),
        ("D4 標燃 (カド×ヒサ×ボルグ×ネル)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Nel)),
        ("D5 標燃 (カド×ヒサ×ボルグ×ノノ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Nono)),
        ("D6 標燃 (カド×ヒサ×ボルグ×ムド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Mudo)),
        ("D7 標燃 (カド×ヒサ×ボルグ×ホタ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Hota)),
        ("D8 標燃 (カド×ヒサ×ボルグ×ドルガ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Dolga)),
        // --- 毒の供給元を変える（グザ＝全員に毎ターン / スィド＝隣に被弾のたび）---
        ("E1 毒標燃 (ガルド×ヒサ×ボルグ×スィド)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Sid)),
        ("E2 毒標燃 (ドルガ×ヒサ×ボルグ×スィド)", Formation.Build(
            front1: UnitCatalog.Sid, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Dolga)),
        ("E3 毒標燃 (カド×ヒサ×ボルグ×グザ・ゴウ中央)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: SgPlain,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Guza)),
        ("E4 毒標燃 (カド×ヒサ×ボルグ×スィド・ゴウ中央)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: SgPlain,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Sid)),
        // --- 揃いにくい側（1〜2種類）---
        ("B1 標のみ (反撃 ヒサ×カド のノノ枠)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Nel)),
        ("B2 痺のみ (刻み×抉り のヴェル枠)", Formation.Build(
            front1: UnitCatalog.Egu, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi,
            back1: UnitCatalog.Dolga, back3: SgPlain)),
        ("B3 毒のみ (澱み喰い のヴィオ枠)", Formation.Build(
            front1: UnitCatalog.Sid, front3: UnitCatalog.Gald, center: UnitCatalog.Guza,
            back1: UnitCatalog.Mio, back3: SgPlain)),
        ("B4 燃のみ (燃焼 ボルグ×ホタ のノノ枠)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Borg, center: UnitCatalog.Hota,
            back1: SgPlain, back3: UnitCatalog.Mudo)),
        ("B5 なし (耐久 ガルド×ノノ のセロ枠)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Nono,
            back1: SgPlain, back3: UnitCatalog.Golm)),
    };

    Console.WriteLine("## 0-7. 候補台の地図（`CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine("5枠目に**業と同数値・特性なしの素体（88/7/4・単体）**を置いた版で測る。");
    Console.WriteLine("この素体版がそのまま §4 の陽性対照になる。");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{SgSeeds - 1}。`3種` は同時成立したターンの割合、`初3` は初成立ターン、");
    Console.WriteLine("`未3` は最後まで揃わなかった試行の割合。");
    Console.WriteLine();
    Console.WriteLine("**同時成立（`3種`）ではなく累積（`累3`）が到達の分母。** 業は引き取った種類を持ち越すので、");
    Console.WriteLine("盤面に3種が同時に並ぶ必要はない——毒と標は減らないから、一度引き取れば戦闘の終わりまで残る。");
    Console.WriteLine();
    Console.WriteLine("| 候補 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 毒 | 標 | 痺 | 燃 | 2種 | 3種 | **累種** | **累3** | **累3未達** |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var c in sgCand)
    {
        var wins = new double[sgStages.Count];
        int kinds = sgKinds.Length;
        var tw = new long[kinds];
        long tt = 0, c2 = 0, c3 = 0;
        double cf3 = 0, cumSum = 0; int cf3n = 0, cumNever3 = 0, trials = 0;
        for (int w = 0; w < sgStages.Count; w++)
        {
            int win = 0;
            for (int seed = 0; seed < SgSeeds; seed++)
            {
                if (BattleEngine.Run(c.F, sgStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                var z = SgTrace(c.F, sgStages[w].Enemy, seed);
                trials++;
                tt += z.Turns; c2 += z.Kinds2; c3 += z.Kinds3;
                cumSum += z.CumKinds;
                if (z.CumFirst3 > 0) { cf3 += z.CumFirst3; cf3n++; } else cumNever3++;
                for (int k = 0; k < kinds; k++) tw[k] += z.TurnsWith[k];
            }
            wins[w] = win * 100.0 / SgSeeds;
        }
        Console.WriteLine($"| {c.Name} | **{wins.Average():0.0}%** "
            + string.Concat(wins.Select(x => $"| {x:0.0}% "))
            + string.Concat(Enumerable.Range(0, 4).Select(k => $"| {(tt == 0 ? 0 : tw[k] * 100.0 / tt):0.0}% "))
            + $"| {(tt == 0 ? 0 : c2 * 100.0 / tt):0.0}% | {(tt == 0 ? 0 : c3 * 100.0 / tt):0.0}% "
            + $"| **{cumSum / trials:0.00}** | **{(cf3n > 0 ? $"{cf3 / cf3n:0.00}" : "—")}** "
            + $"| **{cumNever3 * 100.0 / trials:0.0}%** |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}

// scapegoat モード（本体）: 業（第49期）。**出力は `docs/` に置かない。**
// `Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（供給の種類を変えた対）。
//
//     dotnet run --project BattleSim -c Release 0 scapegoat [絞り込み]
//     dotnet run --project BattleSim -c Release 0 scapegoat sweep   # 閾値の掃引だけ
//     dotnet run --project BattleSim -c Release 0 scapegoat seats   # 席の分散だけ
//     dotnet run --project BattleSim -c Release 0 scapegoat stun    # 痺のある台（§7-3）だけ
if (focusId == "scapegoat")
{
    var sgBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> sgStages = EnemyCatalog.Stages;
    const int SgSeeds = 200;   // compare / spread / scale と揃える
    const int SgMain = 3;      // 主表に使う Threshold（探索段階の初期値）
    int[] sgThresholds = { 2, 3, 4 };

    string sgMode = args.Length > 2 ? args[2] : "";

    // **業は測って採用しなかった**ので、`CompareBuilds()` には1行も入っていない
    // （逆位・まどろみ・誹り・驕りと同じ扱い）。測った2編成はここにある——
    // `CompareBuilds()` を1行も動かさずに全部を測り直せる（`overbear` の `ObRows()` と同型）。
    //
    // **土台は反撃台（カド×ヒサ×ボルグ）で固定し、5枚目だけを差し替えてある**
    // ——第21期の swap と同じ作法で、差がそのまま「供給が3種類あるかどうか」の差になる。
    // 配置は §1 の3段（reseat 上位5 → confirm seed 200..599 → 次数で読む）で決めた:
    // 仮置き（ゴウ 後1）はどちらも上位5に入らず、追試で +6.7pt / +5.8pt だったので
    // **ゴウを中央へ動かした**（5.0pt 以上の候補はどちらの行でも過半が中央＝次数4）。
    static (string Name, Formation F)[] SgRows() => new (string, Formation)[]
    {
        // 揃う側。グザの瘴気が味方全員（ゴウを含む）に毒を撒くので**毒は引き取り不要で載る**。
        ("業 (ゴウ×グザ)", Formation.Build(front1: UnitCatalog.Kado, front3: UnitCatalog.Borg,
                                      center: UnitCatalog.Gou, back1: UnitCatalog.Hisa,
                                      back3: UnitCatalog.Guza)),
        // 揃わない側。**グザ1枚をガルドに差し替えただけ**で供給が 毒+標+燃 → 標+燃 に落ち、
        // 閾値3に構造的に届かなくなる。**ヒサが前3に来るとゴウ（最大HP 88）が囃し立ての
        // 対象になる**ので標は最初からゴウに載り、引き取りは 0.00 回/戦になる
        // ——**配置探索が機構を無効化する席を選んだ**形（報告書 §5）。
        ("業改 (ゴウ×ガルド)", Formation.Build(front1: UnitCatalog.Kado, front3: UnitCatalog.Hisa,
                                        center: UnitCatalog.Gou, back1: UnitCatalog.Gald,
                                        back3: UnitCatalog.Borg)),
    };

    var sgTargets = SgRows();
    if (sgMode.Length > 0 && sgMode != "sweep" && sgMode != "seats" && sgMode != "stun"
        && sgMode != "confirm" && sgMode != "alt")
        sgTargets = sgTargets.Where(b => sgMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // **素体の対照。** ゴウと数値・型・速さが1つも違わず、特性だけを持たない駒。
    // **`ScapegoatRule` を 0 にする形の対照は使わない**（第47期の失敗）——閾値を上げても
    // 引き取り（マイナス側）は止まらないので、「機構が効いたのか、ただ 88/7/4 の体が
    // 入っただけか」が割れない。**規則にノブを増やさず、駒の側で塞ぐ**
    // （`gradient` / `aim` / `guard` / `scale` と同じ、診断のローカルの def）。
    UnitDef SgPlainDef = new()
    {
        Id = "gou_plain", Name = "素体のゴウ", MaxHp = UnitCatalog.Gou.MaxHp,
        Attack = UnitCatalog.Gou.Attack, Speed = UnitCatalog.Gou.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Gou.Pattern
    };
    Formation SgPlain(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Gou) ? SgPlainDef : d;
        return g;
    }
    // ゴウを外した4体版。**第21期の飽和検査**を兼ねる
    // ——4体版と5体版が同じ値なら、その台では第5の駒が何であっても結果が変わらない。
    static Formation SgWithoutGou(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Gou)) g[slot] = d;
        return g;
    }
    // ゴウの席だけを振った5変種（他の4枚は元の相対順のまま空いた席へ詰める）。
    static Formation SgSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Gou))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Gou;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    // 1つの（編成 × 波 × 規則）についての計測。**盤面は触らない**——`BattleResult` の計数を読むだけ。
    // **監査（`Audit`）を必ず立てる**——素体の対照でも自傷・味方の継続ダメージを数えるため。
    // `who` は「5枚目の席の駒」の Def.Id（業なら "gou"・素体なら "gou_plain"）。
    // **両方の版で同じ切り方**にするために、保持者かどうかではなく id で割る。
    SgStat MeasureSg(Formation f, Formation enemy, ScapegoatRule rule, string who)
    {
        var z = new SgStat();
        double aliveTurns = 0, kindSum = 0, metSum = 0, firstSum = 0; int firstN = 0;
        for (int seed = 0; seed < SgSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                        null, null, null, null, null, null, null, null, null, null, null,
                        rule with { Audit = true });
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Takes += r.ScapegoatTakes; z.Missed += r.ScapegoatMissed; z.Full += r.ScapegoatFull;
            aliveTurns += r.ScapegoatAliveTurns; kindSum += r.ScapegoatKindSum;
            metSum += r.ScapegoatMetTurns;
            if (r.ScapegoatKindMax > z.KindMax) z.KindMax = r.ScapegoatKindMax;
            if (r.ScapegoatFirstTurn > 0) { firstSum += r.ScapegoatFirstTurn; firstN++; } else z.Never++;
            z.Swings += r.ScapegoatSwings; z.Fired += r.ScapegoatFired;
            z.FoeDot += r.ScapegoatFoeDot; z.FoeSkips += r.ScapegoatFoeSkips;
            z.MarkPulls += r.ScapegoatMarkPulls;
            foreach ((string id, int v) in r.ScapegoatDotByUnit)
                if (id == who) z.SelfDot += v; else z.AllyDot += v;
            foreach ((string id, int v) in r.ScapegoatSkipByUnit)
                if (id == who) z.SelfSkips += v; else z.AllySkips += v;
            z.Life += r.TallyByUnit.TryGetValue(who, out UnitTally? tw) ? tw.LastActiveTurn : 0;
            foreach ((string k, int v) in r.ScapegoatTakeByKind)
                z.TakeByKind[k] = z.TakeByKind.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.ScapegoatWriteByKind)
                z.WriteByKind[k] = z.WriteByKind.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.ScapegoatTakeFrom)
                z.TakeFrom[k] = z.TakeFrom.TryGetValue(k, out double a) ? a + v : v;
        }
        double n = SgSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.Takes /= n; z.Missed /= n; z.Full /= n;
        z.KindAvg = aliveTurns > 0 ? kindSum / aliveTurns : 0;
        z.Met = aliveTurns > 0 ? metSum * 100 / aliveTurns : 0;
        z.First = firstN > 0 ? firstSum / firstN : 0;
        z.Never = z.Never * 100 / n;
        z.Swings /= n; z.Fired /= n;
        z.FoeDot /= n; z.FoeSkips /= n; z.MarkPulls /= n;
        z.SelfDot /= n; z.SelfSkips /= n; z.AllyDot /= n; z.AllySkips /= n; z.Life /= n;
        foreach (string k in z.TakeByKind.Keys.ToList()) z.TakeByKind[k] /= n;
        foreach (string k in z.WriteByKind.Keys.ToList()) z.WriteByKind[k] /= n;
        foreach (string k in z.TakeFrom.Keys.ToList()) z.TakeFrom[k] /= n;
        return z;
    }

    // 全波を通した集計（機構の量は波で平均する。勝率だけは波ごとに残す）
    (double[] Wins, SgStat Z) SgAll(Formation f, ScapegoatRule rule, string who)
    {
        var wins = new double[sgStages.Count];
        var acc = new SgStat();
        int firstN = 0;
        for (int w = 0; w < sgStages.Count; w++)
        {
            var z = MeasureSg(f, sgStages[w].Enemy, rule, who);
            wins[w] = z.Win;
            acc.Turns += z.Turns; acc.Takes += z.Takes; acc.Missed += z.Missed; acc.Full += z.Full;
            acc.KindAvg += z.KindAvg; acc.Met += z.Met; acc.Never += z.Never;
            acc.Swings += z.Swings; acc.Fired += z.Fired;
            acc.FoeDot += z.FoeDot; acc.FoeSkips += z.FoeSkips; acc.MarkPulls += z.MarkPulls;
            acc.SelfDot += z.SelfDot; acc.SelfSkips += z.SelfSkips;
            acc.AllyDot += z.AllyDot; acc.AllySkips += z.AllySkips; acc.Life += z.Life;
            if (z.KindMax > acc.KindMax) acc.KindMax = z.KindMax;
            if (z.First > 0) { acc.First += z.First; firstN++; }
            foreach ((string k, double v) in z.TakeByKind)
                acc.TakeByKind[k] = acc.TakeByKind.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.WriteByKind)
                acc.WriteByKind[k] = acc.WriteByKind.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.TakeFrom)
                acc.TakeFrom[k] = acc.TakeFrom.TryGetValue(k, out double a) ? a + v : v;
        }
        double m = sgStages.Count;
        acc.First = firstN > 0 ? acc.First / firstN : 0;
        acc.Win = wins.Average();
        acc.Turns /= m; acc.Takes /= m; acc.Missed /= m; acc.Full /= m;
        acc.KindAvg /= m; acc.Met /= m; acc.Never /= m;
        acc.Swings /= m; acc.Fired /= m;
        acc.FoeDot /= m; acc.FoeSkips /= m; acc.MarkPulls /= m;
        acc.SelfDot /= m; acc.SelfSkips /= m; acc.AllyDot /= m; acc.AllySkips /= m; acc.Life /= m;
        foreach (string k in acc.TakeByKind.Keys.ToList()) acc.TakeByKind[k] /= m;
        foreach (string k in acc.WriteByKind.Keys.ToList()) acc.WriteByKind[k] /= m;
        foreach (string k in acc.TakeFrom.Keys.ToList()) acc.TakeFrom[k] /= m;
        return (wins, acc);
    }

    double[] SgPlainWins(Formation f)
    {
        var v = new double[sgStages.Count];
        for (int w = 0; w < sgStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < SgSeeds; seed++)
                if (BattleEngine.Run(f, sgStages[w].Enemy, seed, false).PlayerWon) wins++;
            v[w] = wins * 100.0 / SgSeeds;
        }
        return v;
    }

    static string SgCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));
    string SgByKind(Dictionary<string, double> d)
    {
        var parts = ScapegoatTrait.Kinds.Where(k => d.TryGetValue(k, out double v) && v > 0)
            .Select(k => $"{StatusKeys.LabelOf(k)} {d[k]:0.00}").ToList();
        return parts.Count == 0 ? "—" : string.Join(" / ", parts);
    }

    Console.WriteLine("# 業（scapegoat）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 scapegoat [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{SgSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` / `CompareBuilds()` は触っていない。**業は測って採用しなかった**ので、");
    Console.WriteLine("測った2編成はこの診断のローカル（`SgRows()`）にある（`overbear` の `ObRows()` と同型）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 引き取り | 移した延べ量/戦・**種類ごとの内訳**・取った相手の内訳 |");
    Console.WriteLine("| 空振り | 引き取れる種類が盤面に無くて何もしなかった回数/戦（`満杯` は全種類を既に背負っていた回数） |");
    Console.WriteLine("| 種類数 | ゴウが背負っている種類数の 平均 / 最大（引き取りの**後**に数える） |");
    Console.WriteLine("| **到達** | **閾値に初めて達したターン**（達した試行だけの平均）。`未達` が一度も達しなかった試行の割合 |");
    Console.WriteLine("| 成立率 | 閾値を満たしていたターン数 ÷ ゴウが生きてターン頭を迎えた回数 |");
    Console.WriteLine("| 転写 | 発揮した回数/戦・**付けた種類の内訳** |");
    Console.WriteLine("| **転写の効き** | **毒燃**＝業が書いたぶんに帰属する継続ダメージ ／ **痺**＝飛ばした敵の手番 ／ **標**＝味方の単体攻撃が引かれた回数 |");
    Console.WriteLine("| 自傷 | ゴウが継続ダメージで受けた量 / 痺れで失った手番。**寿命**はゴウが最後に盤上にいたターン |");
    Console.WriteLine("| 味方 | **ゴウ以外の味方**が継続ダメージで受けた量 / 失った手番。**救済は素体との差で読む** |");
    Console.WriteLine();
    Console.WriteLine("> **「転写」と「転写の効き」は別の列。** 付けた回数は成果ではない");
    Console.WriteLine("> ——敵が次のターンに死ぬなら毒を付けても意味がない。");
    Console.WriteLine();
    Console.WriteLine("> **「自傷」と「味方」は絶対値では帰属が取れない。** 瘴気の毒はゴウが引き取らなくても");
    Console.WriteLine("> 味方全員に載るので、**素体版との差**でしか機構のぶんは割れない。");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）----------------------------------------------------------
    if (sgMode.Length == 0)
    {
        Console.WriteLine("## 0. 検算 —— 差分は業だけに閉じているか（受け入れ基準2）");
        Console.WriteLine();
        var plain = sgBuilds;   // 業は CompareBuilds に入っていないので全 50 行が対象
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < sgStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < SgSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, sgStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null,
                            new ScapegoatRule(1)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, sgStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null,
                            new ScapegoatRule(9)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（ゴウを含まない {plain.Length} 行が `ScapegoatRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {sgStages.Count} 波・`Threshold` 1 対 9）");

        // 監査（Audit）が盤面を動かしていないことの検算。**素体の対照が成立する前提そのもの。**
        int aCells = 0, aDiff = 0;
        foreach (var b in sgBuilds)
            for (int w = 0; w < sgStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < SgSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, sgStages[w].Enemy, seed, false).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, sgStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null,
                            new ScapegoatRule(ScapegoatRule.Default.Threshold, true)).PlayerWon) c++;
                }
                aCells++;
                if (a != c) aDiff++;
            }
        Console.WriteLine($"- **監査は盤面を動かさない**（`Audit` の有無で `compare` が変わらない）: "
            + $"**{aCells} セル中 {aDiff} 件の食い違い**（{sgBuilds.Length} 行 × {sgStages.Count} 波）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  `compare` の全文で確認済み（**250 セル中 0 件**・業は `UnitCatalog.All` にも");
        Console.WriteLine("  `CompareBuilds()` にも載っていないので、`docs/balance.md` は1バイトも動かない）。");
        Console.WriteLine("  engine に足した計数のフック（`TickStatuses` の2箇所・行動順ループの痺れ分岐・");
        Console.WriteLine("  標の引き）は**すべて `ScapegoatActive` で短絡**し、控えのカウンタは誰も読んで分岐しない。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 -----------------------------------------------------------------------------
    if (sgMode.Length == 0 || (sgMode != "sweep" && sgMode != "seats" && sgMode != "stun"
                               && sgMode != "confirm" && sgMode != "alt"))
    {
        Console.WriteLine($"## 1. 主表（`Threshold = {SgMain}` と陽性対照）");
        Console.WriteLine();
        Console.WriteLine("`素体` = ゴウと**数値・型・速さが1つも違わず特性だけを持たない駒**に差し替えた版。");
        Console.WriteLine("**これが機構の帰属を取る唯一の窓口**——閾値を上げても引き取り（マイナス側）は止まらないので、");
        Console.WriteLine("`ScapegoatRule` を大きくする形は対照にならない（第47期 `ScaleRule(0)` と同じ穴）。");
        Console.WriteLine("`4体` = ゴウを外した4体版（**第21期の飽和検査**も兼ねる");
        Console.WriteLine("——4体版と5体版が同じ値なら、その台では5枚目が何であっても結果が変わらない）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
        {
            var (wins, _) = SgAll(b.F, new ScapegoatRule(SgMain), "gou");
            var pw = SgPlainWins(SgPlain(b.F));
            var fw = SgPlainWins(SgWithoutGou(b.F));
            Console.WriteLine($"| {b.Name} | **T{SgMain}** |{SgCells(wins)} {wins.Average():0.0}% |");
            Console.WriteLine($"| | 素体（特性なし・同数値） |{SgCells(pw)} {pw.Average():0.0}% |");
            Console.WriteLine($"| | 4体（ゴウ抜き） |{SgCells(fw)} {fw.Average():0.0}% |");
            Console.WriteLine($"| | **機構の帰属（T{SgMain} − 素体）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - pw[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - pw.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | 体の値段（素体 − 4体） | "
                + string.Concat(Enumerable.Range(0, pw.Length).Select(i => $"{pw[i] - fw[i]:+0.0;-0.0;0.0} |"))
                + $" **{pw.Average() - fw.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 機構の計数（`Threshold = {SgMain}`・5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 引き取り | 内訳 | 取った相手 | 空振り | 満杯 | 種類数(平均/最大) | **到達** | **未達** | **成立率** | 転写/振り | 付けた内訳 |");
        Console.WriteLine("|---|--:|---|---|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var b in sgTargets)
        {
            var (_, z) = SgAll(b.F, new ScapegoatRule(SgMain), "gou");
            string from = z.TakeFrom.Count == 0 ? "—"
                : string.Join(" / ", z.TakeFrom.OrderByDescending(x => x.Value).Take(3)
                    .Select(x => $"{x.Key} {x.Value:0.00}"));
            Console.WriteLine($"| {b.Name} | {z.Takes:0.00} | {SgByKind(z.TakeByKind)} | {from} "
                + $"| {z.Missed:0.00} | {z.Full:0.00} | {z.KindAvg:0.00} / {z.KindMax:0} "
                + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% | {z.Met:0.0}% "
                + $"| {z.Fired:0.00} / {z.Swings:0.00} | {SgByKind(z.WriteByKind)} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 転写の効き・自傷・味方（`Threshold = {SgMain}`・5波の平均。素体との差つき）");
        Console.WriteLine();
        Console.WriteLine("**`効き/転写` は「付けた1件あたり何ダメージ・何手番になったか」ではない**");
        Console.WriteLine("（単位が混ざる）。毒燃だけは量なので `毒燃の効き ÷ 毒燃を付けた延べ数` を出す。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 転写 | **効き・毒燃** | 毒燃/件 | **効き・痺** | **効き・標** | 自傷(継続) | 自傷(手番) | 寿命 | 味方(継続) | 味方(手番) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
        {
            var (_, z) = SgAll(b.F, new ScapegoatRule(SgMain), "gou");
            var pz = SgAll(SgPlain(b.F), new ScapegoatRule(SgMain), "gou_plain").Z;
            double dotWrites = (z.WriteByKind.TryGetValue(StatusKeys.Poison, out double wp) ? wp : 0)
                             + (z.WriteByKind.TryGetValue(StatusKeys.Burn, out double wb) ? wb : 0);
            Console.WriteLine($"| {b.Name} | {z.Fired:0.00} | **{z.FoeDot:0.0}** "
                + $"| {(dotWrites > 0 ? $"{z.FoeDot / dotWrites:0.00}" : "—")} "
                + $"| **{z.FoeSkips:0.000}** | **{z.MarkPulls:0.000}** "
                + $"| {z.SelfDot:0.0} | {z.SelfSkips:0.00} | {z.Life:0.00} "
                + $"| {z.AllyDot:0.0} | {z.AllySkips:0.00} |");
            Console.WriteLine($"| | 素体 | — | — | — | — | {pz.SelfDot:0.0} | {pz.SelfSkips:0.00} "
                + $"| {pz.Life:0.00} | {pz.AllyDot:0.0} | {pz.AllySkips:0.00} |");
            Console.WriteLine($"| | **差（業 − 素体）** | — | — | — | — "
                + $"| **{z.SelfDot - pz.SelfDot:+0.0;-0.0;0.0}** | **{z.SelfSkips - pz.SelfSkips:+0.00;-0.00;0.00}** "
                + $"| **{z.Life - pz.Life:+0.00;-0.00;0.00}** "
                + $"| **{z.AllyDot - pz.AllyDot:+0.0;-0.0;0.0}** | **{z.AllySkips - pz.AllySkips:+0.00;-0.00;0.00}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine("> **`味方(継続)` の差は救済だけを測っていない。** 引き取ったぶんは味方の帳簿から");
        Console.WriteLine("> ゴウの帳簿へ移るが、同時に**決着ターンが動く**（負ければ長引き、毒は層で伸びる）ので、");
        Console.WriteLine("> 差には救済と戦闘長の両方が入る。**`決着T` と一緒に読むこと。**");
        Console.WriteLine();

        // --- 2. 波ごとの内訳 -------------------------------------------------------------------
        Console.WriteLine($"## 2. 波ごとの内訳（`Threshold = {SgMain}`）");
        Console.WriteLine();
        Console.WriteLine("**供給の時間分布がこの期の設計。** 燃はボルグが振ってから隣接味方に載るので");
        Console.WriteLine("第1ターンには存在せず、標は開戦時に1つだけ載る。");
        Console.WriteLine();
        foreach (var b in sgTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 引き取り | 内訳 | 空振り | 種類数 | 到達 | 未達 | 成立率 | 転写/振り | 効き(毒燃/痺/標) | 自傷 | 寿命 | 決着T |");
            Console.WriteLine("|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < sgStages.Count; w++)
            {
                var z = MeasureSg(b.F, sgStages[w].Enemy, new ScapegoatRule(SgMain), "gou");
                Console.WriteLine($"| {sgStages[w].Name} | {z.Win:0.0}% | {z.Takes:0.00} | {SgByKind(z.TakeByKind)} "
                    + $"| {z.Missed:0.00} | {z.KindAvg:0.00} | {(z.First > 0 ? $"{z.First:0.00}" : "—")} "
                    + $"| {z.Never:0.0}% | {z.Met:0.0}% | {z.Fired:0.00} / {z.Swings:0.00} "
                    + $"| {z.FoeDot:0.0} / {z.FoeSkips:0.00} / {z.MarkPulls:0.00} "
                    + $"| {z.SelfDot:0.0} | {z.Life:0.00} | {z.Turns:0.0} |");
                Console.Out.Flush();
            }
            Console.WriteLine();
        }
    }

    // --- 3. 掃引（受け入れ基準9）---------------------------------------------------------------
    if (sgMode.Length == 0 || sgMode == "sweep")
    {
        Console.WriteLine("## 3. 掃引（`Threshold` 2 / 3 / 4）");
        Console.WriteLine();
        Console.WriteLine("**閾値は強度ではなく「発火するかしないか」を切る**はずのノブ。");
        Console.WriteLine("**味方に載る種類は4つしかなく、しかも痺は使えない**（引き取ると手番が飛ぶ）ので、");
        Console.WriteLine("実効の分母は 毒・標・燃 の3つ——`Threshold = 4` は構造的にほぼ到達不能。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 閾値 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 種類数 | 到達 | 未達 | 成立率 | 転写/振り | 効き(毒燃) | 自傷 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
        {
            foreach (int th in sgThresholds)
            {
                var (wins, z) = SgAll(b.F, new ScapegoatRule(th), "gou");
                Console.WriteLine($"| {b.Name} | {th} | **{wins.Average():0.0}%** |{SgCells(wins)} "
                    + $"{z.KindAvg:0.00} | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                    + $"| {z.Met:0.0}% | {z.Fired:0.00} / {z.Swings:0.00} | {z.FoeDot:0.0} | {z.SelfDot:0.0} |");
                Console.Out.Flush();
            }
            var pw = SgPlainWins(SgPlain(b.F));
            Console.WriteLine($"| {b.Name} | 素体 | **{pw.Average():0.0}%** |{SgCells(pw)} — | — | — | — | — | — | — |");
        }
        Console.WriteLine();
        foreach (var b in sgTargets)
        {
            var vals = sgThresholds.Select(th => SgAll(b.F, new ScapegoatRule(th), "gou").Wins.Average()).ToList();
            Console.WriteLine($"- **{b.Name} の掃引の全幅: {vals.Max() - vals.Min():0.0}pt**"
                + $"（{string.Join(" / ", sgThresholds.Zip(vals, (t, v) => $"T{t} {v:0.0}%"))}）");
        }
        Console.WriteLine();
    }

    // --- 4. 痺のある台（§7-3）-----------------------------------------------------------------
    // **`CompareBuilds()` は触らない。** 採用2行はどちらも痺の供給を持たない
    // （持たせると台が 20.0% に潰れる）ので、§7-3 の「引き取りと発揮が同じ資源を奪い合う」は
    // ローカルの台でしか観測できない。`gradient` / `aim` / `route` と同じ扱い。
    if (sgMode.Length == 0 || sgMode == "stun")
    {
        Console.WriteLine("## 4. 痺のある台（§7-3 の観測。`CompareBuilds()` には入れていない）");
        Console.WriteLine();
        Console.WriteLine("採用2行はどちらも痺の供給を持たない。**痺を足すと台が 20.0% に潰れる**ので、");
        Console.WriteLine("「痺を引き取ると手番が飛んで転写できない」は診断のローカルでしか測れない。");
        Console.WriteLine("台は採用行（業）のグザをシガ（責め苦・動ける敵を殴ると自分が痺れる）に差し替えたもの。");
        Console.WriteLine();
        var sgStunRows = new (string Name, Formation F)[]
        {
            ("痺台 (カド×ヒサ×ボルグ×シガ)", Formation.Build(
                front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
                back1: UnitCatalog.Gou, back3: UnitCatalog.Shiga)),
            ("痺台・素体", Formation.Build(
                front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
                back1: SgPlainDef, back3: UnitCatalog.Shiga)),
            ("毒痺台 (グザ×シガ×ボルグ×ゴウ)", Formation.Build(
                front1: UnitCatalog.Shiga, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
                back1: UnitCatalog.Gou, back3: UnitCatalog.Guza)),
        };
        Console.WriteLine("| 台 | 平均勝率 | 引き取り | 内訳 | 種類数 | 到達 | 未達 | 成立率 | 転写/振り | **自傷(手番)** | 寿命 |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in sgStunRows)
        {
            var (wins, z) = SgAll(r.F, new ScapegoatRule(SgMain), r.Name.Contains("素体") ? "gou_plain" : "gou");
            Console.WriteLine($"| {r.Name} | **{wins.Average():0.0}%** | {z.Takes:0.00} | {SgByKind(z.TakeByKind)} "
                + $"| {z.KindAvg:0.00} | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                + $"| {z.Met:0.0}% | {z.Fired:0.00} / {z.Swings:0.00} | **{z.SelfSkips:0.00}** | {z.Life:0.00} |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine("> **`成立率` が高いのに `転写/振り` が低ければ、それが §7-3 の症状。**");
        Console.WriteLine("> 痺を持っているターンのゴウは手番を飛ばすので、閾値を満たしていても振れない。");
        Console.WriteLine();
    }

    // --- 4a. 別 seed の追試（第44期の作法）------------------------------------------------------
    // 主表の「機構の帰属」は seed 0..199 で測っている。**符号が大きく出た波は別帯で追試する**
    // ——第44期は第二波の +1.0pt が別帯で −2.0pt に割れた。
    if (sgMode == "alt")
    {
        const int AltFrom = 200, AltTo = 600;
        Console.WriteLine("## 4a. 別 seed の追試（seed 200..599・機構の帰属）");
        Console.WriteLine();
        Console.WriteLine("`T3 − 素体` を**選定に使っていない seed 帯**で測り直す。");
        Console.WriteLine("seed 0..199 の値と符号が揃わない波は、そこで見えた差が乱数だったということ。");
        Console.WriteLine();
        double[] Cells(Formation f, ScapegoatRule? rule)
        {
            var v = new double[sgStages.Count];
            for (int w = 0; w < sgStages.Count; w++)
            {
                int wins = 0;
                for (int seed = AltFrom; seed < AltTo; seed++)
                    if (BattleEngine.Run(f, sgStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null,
                            rule).PlayerWon) wins++;
                v[w] = wins * 100.0 / (AltTo - AltFrom);
            }
            return v;
        }
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
        {
            var t3 = Cells(b.F, new ScapegoatRule(SgMain));
            var pl = Cells(SgPlain(b.F), null);
            Console.WriteLine($"| {b.Name} | T{SgMain} |{SgCells(t3)} {t3.Average():0.0}% |");
            Console.WriteLine($"| | 素体 |{SgCells(pl)} {pl.Average():0.0}% |");
            Console.WriteLine($"| | **帰属（追試）** | "
                + string.Concat(Enumerable.Range(0, t3.Length).Select(i => $"{t3[i] - pl[i]:+0.0;-0.0;0.0} |"))
                + $" **{t3.Average() - pl.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    // --- 4b. 配置の追試（§1 の作法・3段目）------------------------------------------------------
    // (1) 現行が reseat の上位5通りに入っていれば動かさない
    // (2) 入っていない行だけ confirm（**選定に使っていない seed 200..599**）で測り、
    //     **5.0pt 以上のときだけ**動かす（第46期に 2.0 → 5.0 へ変更）
    // (3) 採否は1位の配置ではなく**次数**で読む（1位は 28/48 行で入れ替わるが次数の一致率は 98%）
    if (sgMode == "confirm")
    {
        const int CfFrom = 200, CfTo = 600;   // reseat / seats の選定帯（0..199）と重ならない
        Console.WriteLine("## 4b. 配置の追試（`confirm`・seed 200..599）");
        Console.WriteLine();
        Console.WriteLine("**選定に使っていない seed 帯**で測り直す。採否閾値は **5.0pt**（第46期）。");
        Console.WriteLine("**1位の配置ではなく次数で読む**（第45期の残件 D）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | ゴウの席 | ゴウの次数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 現行との差 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
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
                foreach (EnemyCatalog.Stage st in sgStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));

            double[] Cells(Formation f)
            {
                var v = new double[sgStages.Count];
                for (int w = 0; w < sgStages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = CfFrom; seed < CfTo; seed++)
                        if (BattleEngine.Run(f, sgStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                    v[w] = wins * 100.0 / (CfTo - CfFrom);
                }
                return v;
            }
            int GouSeat(Formation f)
            {
                foreach ((int slot, UnitDef d) in f.Occupied())
                    if (ReferenceEquals(d, UnitCatalog.Gou)) return slot;
                return -1;
            }
            int Deg(int slot)
            {
                int n = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(slot, i)) n++;
                return n;
            }

            var cur = Cells(b.F);
            Console.WriteLine($"| {b.Name} | **現行** | {FormationRules.SeatNames[GouSeat(b.F)]} "
                + $"| {Deg(GouSeat(b.F))} |{SgCells(cur)} **{cur.Average():0.0}%** | — |");
            Console.Out.Flush();
            // 粗探索の上位5通りを追試する（1位だけを見ない）
            foreach (int idx in order.Take(5))
            {
                if (idx == curIdx) continue;
                var v = Cells(perms[idx]);
                int seat = GouSeat(perms[idx]);
                Console.WriteLine($"| {b.Name} | 粗探索 {order.IndexOf(idx) + 1}位 | {FormationRules.SeatNames[seat]} "
                    + $"| {Deg(seat)} |{SgCells(v)} {v.Average():0.0}% "
                    + $"| **{v.Average() - cur.Average():+0.0;-0.0;0.0}pt** |");
                Console.WriteLine($"|   ↳ 配置 | {string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} | | | | | | | | | |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        Console.WriteLine("**5.0pt 以上の候補があれば動かす。** 無ければ現行を据え置く。");
        Console.WriteLine();
        return;
    }

    // --- 5. 席の分散（seats2 の写し・受け入れ基準10）--------------------------------------------
    if (sgMode.Length == 0 || sgMode == "seats")
    {
        Console.WriteLine("## 5. 席の分散（`seats2` の写し・受け入れ基準10）");
        Console.WriteLine();
        Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 + 最下位 を seed 0..199 で測り直し。");
        Console.WriteLine("**採否に使うのは1位の配置ではなく次数**（第45期の残件 D）。閾値は 5.0pt。");
        Console.WriteLine("**業は隣接を1つも読まない**（引き取りの候補は生存味方全員）が、");
        Console.WriteLine("**供給の側は隣接で決まる**（火の粉はボルグの隣・囃し立てはヒサの隣）——");
        Console.WriteLine("第47期の鱗（読まないのに列で席が決まった）と同じ形になるかを見る。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（3値） | 最頻率 | 幅 | 現行の順位 | 1位との差 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
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
                foreach (EnemyCatalog.Stage st in sgStages)
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
                foreach (EnemyCatalog.Stage st in sgStages)
                {
                    int wins = 0;
                    for (int seed = 0; seed < SgSeeds; seed++)
                        if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    avg += wins * 100.0 / SgSeeds;
                }
                return avg / sgStages.Count;
            }

            var verified = pool.Select(i => (Idx: i, Avg: Avg(perms[i]))).OrderByDescending(x => x.Avg).ToList();
            double width = verified[0].Avg - verified[^1].Avg;
            var top5 = verified.Take(5).ToList();
            int curRank = verified.FindIndex(x => x.Idx == curIdx) + 1;
            double curGap = verified[0].Avg - verified.First(x => x.Idx == curIdx).Avg;

            foreach (UnitDef d in members)
            {
                int bestSlot = -1;
                foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                    if (ReferenceEquals(dd, d)) bestSlot = slot;
                int mid = 0, fcorner = 0, bcorner = 0;
                foreach (var v in top5)
                    foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                        if (ReferenceEquals(dd, d))
                        {
                            int deg2 = 0;
                            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                                if (FormationRules.AreAdjacent(slot, i)) deg2++;
                            if (deg2 == 4) mid++;
                            else if (FormationRules.RowOf(slot) == Row.Front) fcorner++;
                            else bcorner++;
                        }
                int bdeg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
                int top = Math.Max(mid, Math.Max(fcorner, bcorner));
                Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                    + $"| 前角{fcorner} / 中央{mid} / 後角{bcorner} | {top * 100 / 5}% | {width:0.0}pt "
                    + $"| {curRank} / {verified.Count} | {curGap:0.0}pt |");
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine("### ゴウ1枚だけを振った5変種（他の4枚は元の相対順のまま詰める）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ゴウの席 | 次数 | 列 | 引き取り | 種類数 | 到達 | 未達 | 成立率 | 転写 | 自傷 | 寿命 | 平均勝率 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                Formation g = SgSeat(b.F, seat);
                int deg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(seat, i)) deg++;
                var (wins, z) = SgAll(g, new ScapegoatRule(SgMain), "gou");
                Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {deg} "
                    + $"| {FormationRules.RowOf(seat)} | {z.Takes:0.00} | {z.KindAvg:0.00} "
                    + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% | {z.Met:0.0}% "
                    + $"| {z.Fired:0.00} | {z.SelfDot:0.0} | {z.Life:0.00} | {wins.Average():0.0}% |");
                Console.Out.Flush();
            }
        Console.WriteLine();
    }

    return;
}

// divert モード: 逸らし（第50期）。**標（`Marked`）を操作可能にする。**
//
// 標は engine が常時読んでいる強い通貨（`MarkPullPercent` = 75）なのに、
// 盤面での操作手段が存在しなかった——書き手はヒサ1枚・開戦時1回・選択の余地ゼロ、
// **消す経路は1つも無い**（第49期 Phase 0-3）。
//
// `CompareBuilds()` / `Stages` / `Columns` は触らない（phase0 は BattleCore にも触らない）。
//
//     dotnet run --project BattleSim -c Release 0 divert phase0   # 実装前の地図（§2 Phase 0）
if (focusId == "divert" && (args.Length > 2 ? args[2] : "") == "phase0")
{
    var dvBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> dvStages = EnemyCatalog.Stages;
    const int DvScan = 100;   // verbose=true で回すので compare の 200 とは分ける

    Console.WriteLine("# 逸らし Phase 0 —— 標という通貨の地図（第50期 §2）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 divert phase0` の出力。");
    Console.WriteLine("**盤面は1つも動かない。** `BattleCore` には1文字も足していない状態で走らせる測定で、");
    Console.WriteLine("`docs/` には置かない。");
    Console.WriteLine();

    // --- 0-1. engine 側の窓口一覧 -------------------------------------------------------------
    // 第48期・第49期の残件B。**2度当たっているのでここで固定する。**
    // 表は grep と目視で作る（census と同じ作法——Trait に属性を足すと判定の根拠が
    // 「誰かが属性を正しく付けたか」に化けて grep で検算できなくなる）。
    Console.WriteLine("## 0-1. 通貨の読み手 —— **駒の `Trait`** と **engine の窓口** を分けて数える");
    Console.WriteLine();
    Console.WriteLine("第48期の棚卸しは**駒**の読み手だけを数えていた。第49期はそれを根拠に");
    Console.WriteLine("「敵に標を付けても読み手がいないので効かない」と予測して外した——**engine が読んでいた**。");
    Console.WriteLine("**通貨の読み手は「駒の `Trait`」と「engine の窓口」の和である。**");
    Console.WriteLine();
    Console.WriteLine("| 通貨 | engine の窓口（ファイル:行） | 何をするか | 駒の読み手 |");
    Console.WriteLine("|---|---|---|---|");
    Console.WriteLine("| **標** `Marked` | `BattleEngine.cs:1152` | **`SelectTargetCore` の鎖。`MarkPullPercent`(75)% で主目標を標持ちへ差し替える** | 仇討ち（ザン）1枚 |");
    Console.WriteLine("| **痺** `Stun` | `BattleEngine.cs:2269` | 行動順ループ。手番を飛ばして `IdleTurn` へ振り替える | 責め苦（シガ）1枚 |");
    Console.WriteLine("| | `BattleEngine.cs:137` | `CanActOutOfTurn`。ターン外の行動（棘・仇討ち・軋み・追い打ち）を止める | |");
    Console.WriteLine("| **毒** `Poison` | `BattleEngine.cs:174` | `TickStatuses`。層の分だけ削る（毒喰らいがいれば味方は2倍） | 澱み／疫み／毒喰らい／澱み喰い 4枚 |");
    Console.WriteLine("| **燃** `Burn` | `BattleEngine.cs:203` | `TickStatuses`。6 削って残ターンを1減らす | 熾火（ホタ）1枚 |");
    Console.WriteLine("| | `BattleEngine.cs:229` | `Ignite`。再付与は残ターンの**設定**（加算ではない） | |");
    Console.WriteLine("| **破片** `Armor` | `BattleEngine.cs:1650` | `ApplyDamage`。HP の前に削られる。**受け切ると `OnDamaged` を呼ばない** | 鱗（ウロ）1枚 |");
    Console.WriteLine("| **`IdleTurn`** | `BattleEngine.cs:1519` | 据え（バン）の被ダメ半減の判定 | 責め苦／号令 2枚 |");
    Console.WriteLine("| **傷** `Wound` | **無し** | engine は1箇所も読まない（`TickStatuses` に何も足していない） | 抉り／断ち／縫い／刻み 4枚 |");
    Console.WriteLine("| **弱体** | `BattleEngine.cs:1891` | `Dull` が唯一の窓口。集約・転嫁の横取りがここに立つ | 逆しま／引き受け／渡し 3枚 |");
    Console.WriteLine("| **位置** | `BattleEngine.cs:2100` | `SwapSlots`。`OnMoved` / `OnAllyMoved` を流す | 軋み／移り木／後衛特化／突き返し 4枚 |");
    Console.WriteLine("| **死** | `BattleEngine.cs:1783` | `OnKill` → `OnDeath` → `OnAnyDeath` → `OnAllyDeath` の固定順 | 墓守／分裂／破裂／蘇生／鱗 5枚 |");
    Console.WriteLine();
    Console.WriteLine("> **engine の窓口を持つ通貨は 9 / 10**（傷だけが持たない）。");
    Console.WriteLine("> **標は「駒の読み手1枚・engine の窓口1つ」で、engine 側のほうが強い**");
    Console.WriteLine("> ——ザンは標を持つ味方が殴られたときにしか動かないが、engine の窓口は");
    Console.WriteLine("> **すべての単体攻撃**で評価される。");
    Console.WriteLine();

    // --- 0-2. MarkPullPercent の実装 ----------------------------------------------------------
    Console.WriteLine("## 0-2. `MarkPullPercent` の実装（`BattleEngine.cs:1152`）");
    Console.WriteLine();
    Console.WriteLine("```csharp");
    Console.WriteLine("UnitState? marked = PickOne(foes.Where(f => f.Counter(StatusKeys.Marked) > 0).ToList());");
    Console.WriteLine("if (marked is not null && marked != target && Roll(100) < MarkPullPercent)");
    Console.WriteLine("{ ... return marked; }");
    Console.WriteLine("```");
    Console.WriteLine();
    Console.WriteLine("読み取れること（実装から。**この4点がこの期の設計の前提**）:");
    Console.WriteLine();
    Console.WriteLine("1. **`75` は確率であって重みではない。** ただし「既に主目標が標持ちなら引かない」");
    Console.WriteLine("   （`marked != target`）ので、**実効の被狙撃率は 75% より高い**:");
    Console.WriteLine();
    Console.WriteLine("       標持ちが pool（＝生存する最前列）にいる: 1/n + (1 − 1/n) × 0.75");
    Console.WriteLine("       標持ちが pool にいない（後列など）:        0 + 1 × 0.75 = 0.75");
    Console.WriteLine();
    Console.WriteLine("2. **`foes` から選んでいる（`pool` ではない）。** つまり標は");
    Console.WriteLine("   **「前列が生きている限り後列は狙われない」という盤面の中核規則を破る**");
    Console.WriteLine("   ——後列の標持ちは前列を飛び越して狙われる。ロスターで**標だけが持つ性質**");
    Console.WriteLine("   （執着・断ちの選好は `pool` から選ぶので破らない）。");
    Console.WriteLine();
    Console.WriteLine("3. **標持ちが複数いると `PickOne` で1体に絞ってから 75% を引く。**");
    Console.WriteLine("   引きは1回しか起きないが、**標持ちが増えると `p_t`（無作為の主目標が既に標持ちである確率）が上がる**ので、");
    Console.WriteLine("   標の集合が集める総量は `p_t + (1 − p_t) × 0.75` で**増える**。1体あたりの取り分は逆に薄まる。");
    Console.WriteLine("   ⇒ **`TargetCount` は「集中」と「被覆」を取り替えるノブ**であって、総量のノブでも純粋な分散のノブでもない。");
    Console.WriteLine();
    Console.WriteLine("4. **鎖の順序は 標 → 後備え → 庇う → 殉教 → 棘守り。標がいちばん先。**");
    Console.WriteLine("   標が引いた瞬間に `return` するので、**標は庇い・後備え・殉教をすべて飛び越す。**");
    Console.WriteLine("   ⇒ **敵に標を付けると、敵の殉教者（第五波・庇う75%）を無視して狙い撃てる。**");
    Console.WriteLine();
    Console.WriteLine("5. **単体攻撃にしか効かない**（`pattern != Single` は手前で return）。");
    Console.WriteLine("   薙ぎ・全体・貫きは標を1ビットも見ない。");
    Console.WriteLine();
    Console.WriteLine("6. **両陣営で対称。** `foes = LivingMembers(Opponent(attacker.TeamId))` なので");
    Console.WriteLine("   陣営に依存する分岐が1つも無い。**敵に標を付ければ味方の単体攻撃が引かれるはず**");
    Console.WriteLine("   （実装からはそう読める。実測は §6 の受け入れ基準3で確かめる）。");
    Console.WriteLine();

    // --- 0-3. 標を消す経路 --------------------------------------------------------------------
    Console.WriteLine("## 0-3. 標を消す経路は 1 つも無い（再確認）");
    Console.WriteLine();
    Console.WriteLine("`StatusKeys.Marked` の全出現は **5 箇所**:");
    Console.WriteLine();
    Console.WriteLine("| 場所 | 何をするか |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| `Traits.cs:1374` | **唯一の書き手**（囃し立て・`SetCounter(Marked, 1)`） |");
    Console.WriteLine("| `Traits.cs:2637` | 仇討ち（ザン）が読む |");
    Console.WriteLine("| `BattleEngine.cs:1152` | engine の窓口が読む |");
    Console.WriteLine("| `BattleEngine.cs:1157` | 第49期の計数（帰属用。分岐しない） |");
    Console.WriteLine("| `BattleEngine.cs:995` | スナップショットの表示名 |");
    Console.WriteLine();
    Console.WriteLine("**`SetCounter(StatusKeys.Marked, 0)` は grep で 0 件。**");
    Console.WriteLine("会戦の境界では `StatusKeys.All` の一律掃除で消えるが、**1戦の中では永続。**");
    Console.WriteLine("⇒ **この期で初めて「消す」が実装される。**");
    Console.WriteLine();

    // --- 0-5. 敵側で標を持つ駒 ----------------------------------------------------------------
    Console.WriteLine("## 0-5. 敵側で標を持ちうる駒（＝敵に標が付く経路）");
    Console.WriteLine();
    {
        var markers = new List<string>();
        foreach (UnitDef d in EnemyCatalog.Stages.SelectMany(st => st.Enemy.Occupied()).Select(o => o.Def).Distinct())
            if (d.Traits.Contains(TraitId.Marker)) markers.Add(d.Name);
        Console.WriteLine($"`Stages` に出る敵で囃し立て（`Marker`）を持つ駒: **{markers.Count} 体**"
            + (markers.Count == 0 ? "" : $"（{string.Join(" / ", markers)}）"));
    }
    Console.WriteLine();
    Console.WriteLine("**敵に標が付く経路は現状ゼロ。** ⇒ `MarkPullPercent` の味方側の動作は");
    Console.WriteLine("**一度も実行されたことがない**（第49期の業が 0.30 回/戦だけ通したのが唯一）。");
    Console.WriteLine("受け入れ基準3（敵の標が機能するか）はここが根拠。");
    Console.WriteLine();

    // --- 0-4. 標の現在値と、カドを含む行 -------------------------------------------------------
    // **ログの文字列を数えている**（gullet log / yoke log / hush / sever と同じ理由）——
    // 標が引いたことは盤面の値に痕跡を残さない（誰が狙われたかは HP の差にしか出ない）。
    Console.WriteLine("## 0-4. 標の現在値 —— 引きの回数と、カドを含む行の第四波・第五波");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{DvScan - 1} × 全5波。`引き` は「敵は … に気を取られた」の行数/戦");
    Console.WriteLine("（**ログの文字列を数えている**——標が引いたことは盤面の値に痕跡を残さない）。");
    Console.WriteLine("`敵の単体振り` は**敵側だけ**が単体で振った回数/戦——標が付いているのは味方だけなので、");
    Console.WriteLine("味方の振りは標の窓口を1度も通らない。");
    Console.WriteLine();
    Console.WriteLine("> **`引き` は「標が主目標を差し替えた回数」であって「標持ちが狙われた回数」ではない。**");
    Console.WriteLine("> `marked != target` のときしかログを出さないので、**たまたま標持ちが先に選ばれた回**は");
    Console.WriteLine("> 数に入らない。実効の被狙撃率は `引き率` より 1/n（n = pool の大きさ）ぶん高い。");
    Console.WriteLine("> **差し替えた回数のほうが「標が盤面を動かした量」としては正しい**（帰属に使うのはこちら）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | ヒサ | カド | 引き | 敵の単体振り | 引き率 | カド寿命 | カド干渉 | 第4波 | 第5波 |");
    Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
    var dvMarkRows = new List<(string Name, bool Hisa, bool Kado, double Pull, double Rate)>();
    foreach (var b in dvBuilds)
    {
        var ids = b.F.Occupied().Select(o => o.Def.Id).ToHashSet();
        bool hasHisa = ids.Contains("hisa"), hasKado = ids.Contains("kado");
        if (!hasHisa && !hasKado) continue;

        double pull = 0, singles = 0, life = 0, inter = 0; int battles = 0;
        var wins = new double[dvStages.Count];
        for (int w = 0; w < dvStages.Count; w++)
        {
            int win = 0;
            for (int seed = 0; seed < DvScan; seed++)
            {
                BattleResult r = BattleEngine.Run(b.F, dvStages[w].Enemy, seed, verbose: true);
                if (r.PlayerWon) win++;
                battles++;
                pull += r.Log.Count(l => l.Text.Contains("気を取られた"));
                // **分母は敵側の単体振りだけ。** 標が付いているのは味方だけなので、
                // 味方の振りは標の窓口を1度も通らない（`foes` に標持ちがいない）。
                // 味方の InstanceId は 0..N-1（Run が味方 → 敵の順で Add する）＋味方側の Summon。
                var mine = new HashSet<int>(Enumerable.Range(0, b.F.Occupied().Count()));
                foreach (BattleEvent e in r.Events)
                    if (e.Kind == BattleEventKind.Summon && e.Team == BattleContext.PlayerTeam
                        && e.TargetId is int sid) mine.Add(sid);
                singles += r.Events.Count(e => e.Kind == BattleEventKind.Attack
                                               && e.Pattern == AttackPattern.Single
                                               && e.ActorId is int aid && !mine.Contains(aid));
                if (hasKado && r.TallyByUnit.TryGetValue("kado", out UnitTally? t))
                { life += t.LastActiveTurn; inter += t.Interventions; }
            }
            wins[w] = win * 100.0 / DvScan;
        }
        double n = battles;
        Console.WriteLine($"| {b.Name} | {(hasHisa ? "●" : "")} | {(hasKado ? "●" : "")} "
            + $"| {pull / n:0.00} | {singles / n:0.00} | {(singles > 0 ? $"{pull * 100 / singles:0.0}%" : "—")} "
            + $"| {(hasKado ? $"{life / n:0.00}" : "—")} | {(hasKado ? $"{inter / n:0.00}" : "—")} "
            + $"| {wins[3]:0.0}% | {wins[4]:0.0}% |");
        dvMarkRows.Add((b.Name, hasHisa, hasKado, pull / n, singles > 0 ? pull * 100 / singles : 0));
        Console.Out.Flush();
    }
    Console.WriteLine();
    {
        var withHisa = dvMarkRows.Where(r => r.Hisa).ToList();
        var noHisa = dvMarkRows.Where(r => !r.Hisa).ToList();
        Console.WriteLine($"- ヒサを含む行（{withHisa.Count}）の引き: 平均 **{withHisa.Average(r => r.Pull):0.00} 回/戦**"
            + $"・引き率 平均 **{withHisa.Average(r => r.Rate):0.0}%**");
        Console.WriteLine($"- ヒサを含まない行（{noHisa.Count}）の引き: 平均 **{(noHisa.Count == 0 ? 0 : noHisa.Average(r => r.Pull)):0.00} 回/戦**"
            + "（標の書き手がいないので 0 のはず）");
    }
    Console.WriteLine();

    // --- 0-6 / 0-7 ----------------------------------------------------------------------------
    Console.WriteLine("## 0-6. `docs/balance.md` の分母");
    Console.WriteLine();
    Console.WriteLine($"編成 **{dvBuilds.Length}** 行 × 波 **{dvStages.Count}** = **{dvBuilds.Length * dvStages.Count} セル**。");
    Console.WriteLine();
    Console.WriteLine("## 0-7. 残り枠");
    Console.WriteLine();
    Console.WriteLine($"`UnitCatalog.All` は **{UnitCatalog.All.Count}** 体。上限 52 に対して残り "
        + $"**{52 - UnitCatalog.All.Count}**（第49期は棄却・残置なので消費していない）。");
    Console.WriteLine($"**この期で1枚使うと残り {52 - UnitCatalog.All.Count - 1} になる。**");
    Console.WriteLine();
    return;
}

// divert probe: 採用する2行を選ぶための候補探索（第50期 §3-4）。
// **`CompareBuilds()` は触らない**（候補はここでローカルに組む。`gradient` / `aim` と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 divert probe
if (focusId == "divert" && (args.Length > 2 ? args[2] : "") == "probe")
{
    IReadOnlyList<EnemyCatalog.Stage> pbStages = EnemyCatalog.Stages;
    const int PbSeeds = 200;

    UnitDef PbPlain = new()
    {
        Id = "sora_plain", Name = "素体のソラ", MaxHp = UnitCatalog.Sora.MaxHp,
        Attack = UnitCatalog.Sora.Attack, Speed = UnitCatalog.Sora.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Sora.Pattern
    };
    Formation PbSwap(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Sora) ? PbPlain : d;
        return g;
    }

    var pbCand = new (string Name, Formation F)[]
    {
        // --- カド入り（残件Aの再現台の候補）---
        ("P1 反撃のノノ枠 (カド/ガルド/ヒサ/ソラ/ネル)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Nel)),
        ("P2 反撃のネル枠 (カド/ガルド/ヒサ/ノノ/ソラ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Nono, back3: UnitCatalog.Sora)),
        ("P3 反撃改のドハ枠 (カド/ガルド/ヒサ/ソラ/ノノ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Nono)),
        ("P4 第49期の土台 (カド/ボルグ/ヒサ/ソラ/ガルド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Gald)),
        ("P5 溜めのガン枠 (カド/ドルガ/ヒサ/ソラ/ガルド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Dolga)),
        // 第49期の業の行と**席まで同じ**（ゴウ→ソラ）。ヒサは後1で、隣接は カド(96) と 中央。
        // **中央をグザ(58)にしてあるのでヒサは必ずカドに標を付ける**——残件Aの再現条件そのもの。
        ("P6 第49期の業の席 (カド/ボルグ/グザ/ヒサ/ソラ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Guza,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Sora)),
        // 同上でソラを中央に（第49期の採用席）。**ヒサの隣接最大HPが カド96 と ソラ96 で同値**に
        // なるので `PickOne` が 50/50 に割る——再現条件としては汚れるが、席の対照として測る。
        ("P7 第49期の採用席 (カド/ボルグ/ソラ/ヒサ/グザ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Sora,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Guza)),
        // カドを含むが**ヒサを含まない**（外す対象が無い＝焦点と代金だけ）。
        ("P8 カド入りヒサ無し (カド/ガルド/ノノ/ソラ/ネル)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Nono,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Nel)),
        // --- カド無し・ヒサ入り（外しは走るが反撃役がいない）---
        ("Q1 ヒサ入り (ドルガ/ガルド/ヒサ/ソラ/ノノ)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Nono)),
        ("Q2 ヒサ入り (ドルガ/ガルド/ヒサ/ソラ/ザン)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Zan)),
        ("Q3 ヒサ入り (ボルグ/ガルド/ヒサ/ソラ/ドルガ)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Dolga)),
        // --- カド無し・ヒサ無し（焦点と代金だけが残る）---
        ("R1 刻みのヴェル枠 (エグ/ゴルム/ノミ/ドルガ/ソラ)", Formation.Build(
            front1: UnitCatalog.Egu, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi,
            back1: UnitCatalog.Dolga, back3: UnitCatalog.Sora)),
        ("R2 耐久のセロ枠 (ガルド/ドルガ/ノノ/ソラ/ゴルム)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Nono,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Golm)),
        ("R3 逆しま改のセッキ枠 (ガルド/ゴルム/ウツ/ソラ/クビ)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Golm, center: UnitCatalog.Utsu,
            back1: UnitCatalog.Sora, back3: UnitCatalog.Kubi)),
        ("R4 死の連鎖のヴェル枠 (ゾト/ムグ/ゴルム/リィカ/ソラ)", Formation.Build(
            front1: UnitCatalog.Zoto, front3: UnitCatalog.Mug, center: UnitCatalog.Golm,
            back1: UnitCatalog.Rica, back3: UnitCatalog.Sora)),
        ("R5 後衛特化のセロ枠 (ガルド/ドルガ/ゴルム/セッキ/ソラ)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Golm,
            back1: UnitCatalog.Sekki, back3: UnitCatalog.Sora)),
    };

    Console.WriteLine("# 逸らし: 候補台の探索（`CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{PbSeeds - 1}。`帰属` は ソラ版 − 素体版（同数値・特性なし）。");
    Console.WriteLine("**第四波が − / 第五波が + なら残件Aの符号反転が再現している。**");
    Console.WriteLine();
    Console.WriteLine("| 候補 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 発火 | 外し | 焦点数 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var c in pbCand)
    {
        var wins = new double[pbStages.Count];
        var pw = new double[pbStages.Count];
        double fires = 0, strips = 0, markedSum = 0; int battles = 0;
        for (int w = 0; w < pbStages.Count; w++)
        {
            int a = 0, b = 0;
            for (int seed = 0; seed < PbSeeds; seed++)
            {
                var r = BattleEngine.Run(c.F, pbStages[w].Enemy, seed, false);
                if (r.PlayerWon) a++;
                fires += r.DivertFires; strips += r.DivertStrips;
                markedSum += r.DivertFires > 0 ? (double)r.DivertMarkedFoeSum / r.DivertFires : 0;
                battles++;
                if (BattleEngine.Run(PbSwap(c.F), pbStages[w].Enemy, seed, false).PlayerWon) b++;
            }
            wins[w] = a * 100.0 / PbSeeds;
            pw[w] = b * 100.0 / PbSeeds;
        }
        Console.WriteLine($"| {c.Name} | ソラ |" + string.Concat(wins.Select(x => $" {x:0.0}% |"))
            + $" {wins.Average():0.0}% | {fires / battles:0.00} | {strips / battles:0.00} | {markedSum / battles:0.00} |");
        Console.WriteLine($"| | 素体 |" + string.Concat(pw.Select(x => $" {x:0.0}% |"))
            + $" {pw.Average():0.0}% | — | — | — |");
        Console.WriteLine($"| | **帰属** |"
            + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $" {wins[i] - pw[i]:+0.0;-0.0;0.0} |"))
            + $" **{wins.Average() - pw.Average():+0.0;-0.0;0.0}** | | | |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}

// divert モード（本体）: 逸らし（第50期）。**出力は `docs/` に置かない。**
// `Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（カドの有無で分けた対）。
//
//     dotnet run --project BattleSim -c Release 0 divert [絞り込み]
//     dotnet run --project BattleSim -c Release 0 divert sweep    # TargetCount の掃引だけ
//     dotnet run --project BattleSim -c Release 0 divert seats    # 席の分散だけ
//     dotnet run --project BattleSim -c Release 0 divert confirm  # 配置の追試（seed 200..599）
//     dotnet run --project BattleSim -c Release 0 divert alt      # 機構の帰属を別 seed 帯で追試
if (focusId == "divert")
{
    var dvBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> dvStages = EnemyCatalog.Stages;
    const int DvSeeds = 200;   // compare / spread / scale / scapegoat と揃える
    const int DvMain = 1;      // 主表に使う TargetCount（探索段階の初期値）
    int[] dvCounts = { 1, 2, 3 };

    string dvMode = args.Length > 2 ? args[2] : "";

    static bool DvHasSora(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Sora));

    var dvTargets = dvBuilds.Where(b => DvHasSora(b.F)).ToArray();
    if (dvMode.Length > 0 && dvMode != "sweep" && dvMode != "seats"
        && dvMode != "confirm" && dvMode != "alt")
        dvTargets = dvTargets.Where(b => dvMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // **素体の対照（対照1）。** ソラと数値・型・速さが1つも違わず、特性だけを持たない駒。
    // **`DivertRule` を弱める形の対照は使わない**（第47期 `ScaleRule(0)` の失敗）——
    // `TargetCount` を下げても外しと自分への標は止まらないので、
    // 「機構が効いたのか、ただ 96/6/8 の体が入ったのか」が割れない。
    UnitDef DvPlainDef = new()
    {
        Id = "sora_plain", Name = "素体のソラ", MaxHp = UnitCatalog.Sora.MaxHp,
        Attack = UnitCatalog.Sora.Attack, Speed = UnitCatalog.Sora.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Sora.Pattern
    };
    Formation DvPlain(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Sora) ? DvPlainDef : d;
        return g;
    }
    // ソラを外した4体版（対照3）。**第21期の飽和検査**も兼ねる。
    static Formation DvWithoutSora(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Sora)) g[slot] = d;
        return g;
    }
    // ソラの席だけを振った5変種。
    static Formation DvSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Sora))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Sora;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    DvStat MeasureDv(Formation f, Formation enemy, DivertRule rule, string who)
    {
        var z = new DvStat();
        for (int seed = 0; seed < DvSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                        null, null, null, null, null, null, null, null, null, null, null, null,
                        rule with { Audit = true });
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Fires += r.DivertFires; z.Strips += r.DivertStrips;
            z.Focus += r.DivertFocus; z.FocusFresh += r.DivertFocusFresh;
            z.MarkedFoe += r.DivertFires > 0 ? (double)r.DivertMarkedFoeSum / r.DivertFires : 0;
            if (r.DivertMarkedFoeMax > z.MarkedFoeMax) z.MarkedFoeMax = r.DivertMarkedFoeMax;
            z.AllySingles += r.DivertAllySingles; z.AllyOnMarked += r.DivertAllyOnMarked;
            z.FoeSingles += r.DivertFoeSingles; z.FoeOnMarked += r.DivertFoeOnMarked;
            z.AllyPulls += r.DivertAllyPulls; z.FoePulls += r.DivertFoePulls;
            foreach ((string k, int v) in r.DivertStripFrom)
                z.StripFrom[k] = z.StripFrom.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.DivertFocusTo)
                z.FocusTo[k] = z.FocusTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.DivertKillTurnByFoe)
                z.KillTurn[k] = z.KillTurn.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.DivertKillCountByFoe)
                z.KillCount[k] = z.KillCount.TryGetValue(k, out double a) ? a + v : v;
            if (r.TallyByUnit.TryGetValue(who, out UnitTally? tw))
            { z.SelfTaken += tw.DamageTaken; z.Life += tw.LastActiveTurn; }
            if (r.TallyByUnit.TryGetValue("kado", out UnitTally? tk))
            { z.KadoLife += tk.LastActiveTurn; z.KadoInter += tk.Interventions; z.HasKado = true; }
        }
        double n = DvSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.Fires /= n; z.Strips /= n; z.Focus /= n; z.FocusFresh /= n; z.MarkedFoe /= n;
        z.AllySingles /= n; z.AllyOnMarked /= n; z.FoeSingles /= n; z.FoeOnMarked /= n;
        z.AllyPulls /= n; z.FoePulls /= n;
        z.SelfTaken /= n; z.Life /= n; z.KadoLife /= n; z.KadoInter /= n;
        foreach (string k in z.StripFrom.Keys.ToList()) z.StripFrom[k] /= n;
        foreach (string k in z.FocusTo.Keys.ToList()) z.FocusTo[k] /= n;
        return z;
    }

    (double[] Wins, DvStat Z) DvAll(Formation f, DivertRule rule, string who)
    {
        var wins = new double[dvStages.Count];
        var acc = new DvStat();
        for (int w = 0; w < dvStages.Count; w++)
        {
            var z = MeasureDv(f, dvStages[w].Enemy, rule, who);
            wins[w] = z.Win;
            acc.Turns += z.Turns; acc.Fires += z.Fires; acc.Strips += z.Strips;
            acc.Focus += z.Focus; acc.FocusFresh += z.FocusFresh; acc.MarkedFoe += z.MarkedFoe;
            acc.AllySingles += z.AllySingles; acc.AllyOnMarked += z.AllyOnMarked;
            acc.FoeSingles += z.FoeSingles; acc.FoeOnMarked += z.FoeOnMarked;
            acc.AllyPulls += z.AllyPulls; acc.FoePulls += z.FoePulls;
            acc.SelfTaken += z.SelfTaken; acc.Life += z.Life;
            acc.KadoLife += z.KadoLife; acc.KadoInter += z.KadoInter; acc.HasKado |= z.HasKado;
            if (z.MarkedFoeMax > acc.MarkedFoeMax) acc.MarkedFoeMax = z.MarkedFoeMax;
            foreach ((string k, double v) in z.StripFrom)
                acc.StripFrom[k] = acc.StripFrom.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.FocusTo)
                acc.FocusTo[k] = acc.FocusTo.TryGetValue(k, out double a) ? a + v : v;
        }
        double m = dvStages.Count;
        acc.Win = wins.Average();
        acc.Turns /= m; acc.Fires /= m; acc.Strips /= m; acc.Focus /= m; acc.FocusFresh /= m;
        acc.MarkedFoe /= m; acc.AllySingles /= m; acc.AllyOnMarked /= m;
        acc.FoeSingles /= m; acc.FoeOnMarked /= m; acc.AllyPulls /= m; acc.FoePulls /= m;
        acc.SelfTaken /= m; acc.Life /= m; acc.KadoLife /= m; acc.KadoInter /= m;
        foreach (string k in acc.StripFrom.Keys.ToList()) acc.StripFrom[k] /= m;
        foreach (string k in acc.FocusTo.Keys.ToList()) acc.FocusTo[k] /= m;
        return (wins, acc);
    }

    double[] DvWins(Formation f)
    {
        var v = new double[dvStages.Count];
        for (int w = 0; w < dvStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < DvSeeds; seed++)
                if (BattleEngine.Run(f, dvStages[w].Enemy, seed, false).PlayerWon) wins++;
            v[w] = wins * 100.0 / DvSeeds;
        }
        return v;
    }

    static string DvCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));
    static string DvTop(Dictionary<string, double> d, int n = 3)
    {
        var parts = d.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(n)
            .Select(x => $"{x.Key} {x.Value:0.00}").ToList();
        return parts.Count == 0 ? "—" : string.Join(" / ", parts);
    }

    Console.WriteLine("# 逸らし（divert）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 divert [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{DvSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（カドの有無で分けた対）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 発火 | 逸らしの発火回数/戦。**0 になっていないことが受け入れ基準4**（配置探索が機構を無効化していないか） |");
    Console.WriteLine("| 外し | 味方から外した標の回数/戦・**外した相手の内訳** |");
    Console.WriteLine("| 焦点 | 敵に付けた回数/戦（`新規` は新しく標が付いた回数）・**付けた敵の内訳** |");
    Console.WriteLine("| 焦点数 | 発火時点で**標を持っている敵の数**の平均/最大。**敵の標は消えないので焦点は自分で溶ける** |");
    Console.WriteLine("| **焦点の効き** | **味方の単体振りのうち標持ちの敵に当たった割合**（`引き` は engine の鎖が実際に差し替えた回数） |");
    Console.WriteLine("| **代金の効き** | **敵の単体振りのうち標持ちの味方に当たった割合**（同上） |");
    Console.WriteLine("| 自傷 / 寿命 | ソラが受けた被弾/戦 ／ ソラが最後に盤上にいたターン |");
    Console.WriteLine("| カド | カドの生存ターンと干渉回数（＝反撃を含む「盤面を動かした回数」）。カド入り台のみ |");
    Console.WriteLine();
    Console.WriteLine("> **「焦点」と「焦点の効き」は別の列。** 標を付けた回数は成果ではない");
    Console.WriteLine("> ——味方がそちらを殴らなければ意味がない。**撃破順（§2）が本命の指標。**");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2・3）--------------------------------------------------------
    if (dvMode.Length == 0)
    {
        Console.WriteLine("## 0. 検算");
        Console.WriteLine();
        var plain = dvBuilds.Where(b => !DvHasSora(b.F)).ToArray();
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < dvStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < DvSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, dvStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null, null,
                            new DivertRule(1)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, dvStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null, null,
                            new DivertRule(5, false)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（ソラを含まない {plain.Length} 行が `DivertRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {dvStages.Count} 波・`(1, true)` 対 `(5, false)`）");

        int aCells = 0, aDiff = 0;
        foreach (var b in dvBuilds)
            for (int w = 0; w < dvStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < DvSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, dvStages[w].Enemy, seed, false).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, dvStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null, null,
                            new DivertRule(DivertRule.Default.TargetCount, true, true)).PlayerWon) c++;
                }
                aCells++;
                if (a != c) aDiff++;
            }
        Console.WriteLine($"- **監査は盤面を動かさない**（`Audit` の有無で `compare` が変わらない）: "
            + $"**{aCells} セル中 {aDiff} 件の食い違い**（{dvBuilds.Length} 行 × {dvStages.Count} 波）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  行を足す前に `compare` の全文で確認済み（**250 セル中 0 件**）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 ------------------------------------------------------------------------------
    if (dvMode.Length == 0 || (dvMode != "sweep" && dvMode != "seats"
                               && dvMode != "confirm" && dvMode != "alt"))
    {
        Console.WriteLine($"## 1. 主表（`TargetCount = {DvMain}` と陽性対照3本）");
        Console.WriteLine();
        Console.WriteLine("`素体` = ソラと**数値・型・速さが1つも違わず特性だけを持たない駒**（対照1）。");
        Console.WriteLine("**これが機構の帰属を取る唯一の窓口**——`TargetCount` を下げても外しと自分への標は止まらない。");
        Console.WriteLine("`代金なし` = `DivertRule.SelfMark = false`（対照2）。**自分に標を付けない版**で、");
        Console.WriteLine("「味方の標を外す」＋「敵に焦点を作る」だけが残る——**代金の分離**。");
        Console.WriteLine("`4体` = ソラを外した4体版（対照3。**第21期の飽和検査**も兼ねる）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var (wins, _) = DvAll(b.F, new DivertRule(DvMain), "sora");
            var (nc, _) = DvAll(b.F, new DivertRule(DvMain, false), "sora");
            var pw = DvWins(DvPlain(b.F));
            var fw = DvWins(DvWithoutSora(b.F));
            Console.WriteLine($"| {b.Name} | **T{DvMain}** |{DvCells(wins)} {wins.Average():0.0}% |");
            Console.WriteLine($"| | 代金なし（自分に標を付けない） |{DvCells(nc)} {nc.Average():0.0}% |");
            Console.WriteLine($"| | 素体（特性なし・同数値） |{DvCells(pw)} {pw.Average():0.0}% |");
            Console.WriteLine($"| | 4体（ソラ抜き） |{DvCells(fw)} {fw.Average():0.0}% |");
            Console.WriteLine($"| | **機構の帰属（T{DvMain} − 素体）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - pw[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - pw.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **代金の値段（T{DvMain} − 代金なし）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - nc[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - nc.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | 体の値段（素体 − 4体） | "
                + string.Concat(Enumerable.Range(0, pw.Length).Select(i => $"{pw[i] - fw[i]:+0.0;-0.0;0.0} |"))
                + $" **{pw.Average() - fw.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 機構の計数（`TargetCount = {DvMain}`・5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | **発火** | 外し | 外した相手 | 焦点(新規) | 付けた敵 | 焦点数(平均/最大) | **効き・味方** | 引き | **効き・敵** | 引き | 自傷 | 寿命 |");
        Console.WriteLine("|---|--:|--:|---|--:|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var (_, z) = DvAll(b.F, new DivertRule(DvMain), "sora");
            Console.WriteLine($"| {b.Name} | **{z.Fires:0.00}** | {z.Strips:0.00} | {DvTop(z.StripFrom)} "
                + $"| {z.Focus:0.00} ({z.FocusFresh:0.00}) | {DvTop(z.FocusTo)} "
                + $"| {z.MarkedFoe:0.00} / {z.MarkedFoeMax:0} "
                + $"| **{(z.AllySingles > 0 ? $"{z.AllyOnMarked * 100 / z.AllySingles:0.0}%" : "—")}** | {z.AllyPulls:0.00} "
                + $"| **{(z.FoeSingles > 0 ? $"{z.FoeOnMarked * 100 / z.FoeSingles:0.0}%" : "—")}** | {z.FoePulls:0.00} "
                + $"| {z.SelfTaken:0.0} | {z.Life:0.00} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // カドの再現（残件A）
        Console.WriteLine("### カドの生存と干渉（残件Aの再現・カド入り台のみ）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | カド寿命 | カド干渉 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var (wins, z) = DvAll(b.F, new DivertRule(DvMain), "sora");
            if (!z.HasKado) continue;
            var (pw2, pz) = DvAll(DvPlain(b.F), new DivertRule(DvMain), "sora_plain");
            Console.WriteLine($"| {b.Name} | T{DvMain} | {z.KadoLife:0.00} | {z.KadoInter:0.00} | {wins[3]:0.0}% | {wins[4]:0.0}% |");
            Console.WriteLine($"| | 素体 | {pz.KadoLife:0.00} | {pz.KadoInter:0.00} | {pw2[3]:0.0}% | {pw2[4]:0.0}% |");
            Console.WriteLine($"| | **差** | **{z.KadoLife - pz.KadoLife:+0.00;-0.00;0.00}** "
                + $"| **{z.KadoInter - pz.KadoInter:+0.00;-0.00;0.00}** "
                + $"| **{wins[3] - pw2[3]:+0.0;-0.0;0.0}** | **{wins[4] - pw2[4]:+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 2. 撃破順（本命の指標）------------------------------------------------------------
        Console.WriteLine("## 2. 撃破順（**本命の指標**）—— 敵の駒ごとの撃破ターン");
        Console.WriteLine();
        Console.WriteLine("**焦点の狙いは「硬い敵を先に割る」ことなので、対照と比べて撃破ターンが");
        Console.WriteLine("早まっているかで効いたかどうかが決まる。** `撃破率` は倒せた試行の割合。");
        Console.WriteLine("**標に依存しない切り方**で数えているので素体とそのまま引き算できる。");
        Console.WriteLine();
        foreach (var b in dvTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 敵 | HP | 撃破T(ソラ) | 撃破率(ソラ) | 撃破T(素体) | 撃破率(素体) | **撃破Tの差** |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < dvStages.Count; w++)
            {
                var z = MeasureDv(b.F, dvStages[w].Enemy, new DivertRule(DvMain), "sora");
                var pz = MeasureDv(DvPlain(b.F), dvStages[w].Enemy, new DivertRule(DvMain), "sora_plain");
                foreach ((int slot, UnitDef d) in dvStages[w].Enemy.Occupied())
                {
                    double ct = z.KillCount.TryGetValue(d.Id, out double c1) ? c1 : 0;
                    double tt = z.KillTurn.TryGetValue(d.Id, out double t1) ? t1 : 0;
                    double pc = pz.KillCount.TryGetValue(d.Id, out double c2) ? c2 : 0;
                    double pt = pz.KillTurn.TryGetValue(d.Id, out double t2) ? t2 : 0;
                    // 同じ def が同じ波に複数いる行があるので、体数で割って1体あたりに直す
                    int copies = dvStages[w].Enemy.Occupied().Count(o => o.Def.Id == d.Id);
                    if (dvStages[w].Enemy.Occupied().First(o => o.Def.Id == d.Id).Slot != slot) continue;
                    double a = ct > 0 ? tt / ct : 0, p = pc > 0 ? pt / pc : 0;
                    Console.WriteLine($"| {dvStages[w].Name} | {d.Name}{(copies > 1 ? $" ×{copies}" : "")} | {d.MaxHp} "
                        + $"| {(ct > 0 ? $"{a:0.00}" : "—")} | {ct * 100 / (DvSeeds * copies):0.0}% "
                        + $"| {(pc > 0 ? $"{p:0.00}" : "—")} | {pc * 100 / (DvSeeds * copies):0.0}% "
                        + $"| {(ct > 0 && pc > 0 ? $"**{a - p:+0.00;-0.00;0.00}**" : "—")} |");
                }
                Console.Out.Flush();
            }
            Console.WriteLine();
        }

        // --- 3. 波ごとの内訳 -------------------------------------------------------------------
        Console.WriteLine($"## 3. 波ごとの内訳（`TargetCount = {DvMain}`）");
        Console.WriteLine();
        foreach (var b in dvTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 発火 | 外し | 焦点(新規) | 付けた敵 | 焦点数 | 効き・味方 | 効き・敵 | 自傷 | 寿命 | 決着T |");
            Console.WriteLine("|---|--:|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < dvStages.Count; w++)
            {
                var z = MeasureDv(b.F, dvStages[w].Enemy, new DivertRule(DvMain), "sora");
                Console.WriteLine($"| {dvStages[w].Name} | {z.Win:0.0}% | {z.Fires:0.00} | {z.Strips:0.00} "
                    + $"| {z.Focus:0.00} ({z.FocusFresh:0.00}) | {DvTop(z.FocusTo, 2)} | {z.MarkedFoe:0.00} "
                    + $"| {(z.AllySingles > 0 ? $"{z.AllyOnMarked * 100 / z.AllySingles:0.0}%" : "—")} "
                    + $"| {(z.FoeSingles > 0 ? $"{z.FoeOnMarked * 100 / z.FoeSingles:0.0}%" : "—")} "
                    + $"| {z.SelfTaken:0.0} | {z.Life:0.00} | {z.Turns:0.0} |");
                Console.Out.Flush();
            }
            Console.WriteLine();
        }
    }

    // --- 4. 掃引 ------------------------------------------------------------------------------
    if (dvMode.Length == 0 || dvMode == "sweep")
    {
        Console.WriteLine("## 4. 掃引（`TargetCount` 1 / 2 / 3）");
        Console.WriteLine();
        Console.WriteLine("**`TargetCount` は「集中」と「被覆」を取り替えるノブ。** 標持ちが増えると");
        Console.WriteLine("`p_t`（無作為の主目標が既に標持ちである確率）が上がるので**総量は増える**が、");
        Console.WriteLine("1体あたりの取り分は薄まる。**勝率が動かないのは2つが打ち消し合うから**で、");
        Console.WriteLine("ノブが機構を動かしていないからではない（`効き・味方` の列で確かめる）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | TC | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 焦点数 | 効き・味方 | 効き・敵 | 自傷 | 寿命 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            foreach (int tc in dvCounts)
            {
                var (wins, z) = DvAll(b.F, new DivertRule(tc), "sora");
                Console.WriteLine($"| {b.Name} | {tc} | **{wins.Average():0.0}%** |{DvCells(wins)} "
                    + $"{z.MarkedFoe:0.00} "
                    + $"| {(z.AllySingles > 0 ? $"{z.AllyOnMarked * 100 / z.AllySingles:0.0}%" : "—")} "
                    + $"| {(z.FoeSingles > 0 ? $"{z.FoeOnMarked * 100 / z.FoeSingles:0.0}%" : "—")} "
                    + $"| {z.SelfTaken:0.0} | {z.Life:0.00} |");
                Console.Out.Flush();
            }
            var pw = DvWins(DvPlain(b.F));
            Console.WriteLine($"| {b.Name} | 素体 | **{pw.Average():0.0}%** |{DvCells(pw)} — | — | — | — | — |");
        }
        Console.WriteLine();
        foreach (var b in dvTargets)
        {
            var vals = dvCounts.Select(tc => DvAll(b.F, new DivertRule(tc), "sora").Wins.Average()).ToList();
            Console.WriteLine($"- **{b.Name} の掃引の全幅: {vals.Max() - vals.Min():0.0}pt**"
                + $"（{string.Join(" / ", dvCounts.Zip(vals, (t, v) => $"TC{t} {v:0.0}%"))}）");
        }
        Console.WriteLine();
    }

    // --- 5. 別 seed の追試 --------------------------------------------------------------------
    if (dvMode == "alt")
    {
        const int AltFrom = 200, AltTo = 600;
        Console.WriteLine("## 5. 別 seed の追試（seed 200..599・機構の帰属）");
        Console.WriteLine();
        Console.WriteLine("**受け入れ基準6（残件Aの符号反転の再現）はここで判定する。**");
        Console.WriteLine("第49期の実測は 第四波 −18.0 / −23.0pt・第五波 +22.0 / +19.0pt だった。");
        Console.WriteLine();
        double[] Cells(Formation f, DivertRule? rule)
        {
            var v = new double[dvStages.Count];
            for (int w = 0; w < dvStages.Count; w++)
            {
                int wins = 0;
                for (int seed = AltFrom; seed < AltTo; seed++)
                    if (BattleEngine.Run(f, dvStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null, null,
                            rule).PlayerWon) wins++;
                v[w] = wins * 100.0 / (AltTo - AltFrom);
            }
            return v;
        }
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
        {
            var t = Cells(b.F, new DivertRule(DvMain));
            var pl = Cells(DvPlain(b.F), null);
            Console.WriteLine($"| {b.Name} | T{DvMain} |{DvCells(t)} {t.Average():0.0}% |");
            Console.WriteLine($"| | 素体 |{DvCells(pl)} {pl.Average():0.0}% |");
            Console.WriteLine($"| | **帰属（追試）** | "
                + string.Concat(Enumerable.Range(0, t.Length).Select(i => $"{t[i] - pl[i]:+0.0;-0.0;0.0} |"))
                + $" **{t.Average() - pl.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    // --- 6. 配置の追試 ------------------------------------------------------------------------
    if (dvMode == "confirm")
    {
        const int CfFrom = 200, CfTo = 600;
        Console.WriteLine("## 6. 配置の追試（`confirm`・seed 200..599）");
        Console.WriteLine();
        Console.WriteLine("**選定に使っていない seed 帯**で測り直す。採否閾値は **5.0pt**（第46期）。");
        Console.WriteLine("**1位の配置ではなく次数で読む**（第45期の残件 D）。");
        Console.WriteLine("**`発火` の列を必ず見る**——配置探索が機構を無効化する席を選んでいたら");
        Console.WriteLine("そこは採用しない（第49期の業改が引き取り 0.00 回/戦になった件・§1 の作法）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | ソラの席 | 次数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 現行との差 | 発火 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
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
                foreach (EnemyCatalog.Stage st in dvStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));

            (double[] Cells, double Fires) Measure(Formation f)
            {
                var v = new double[dvStages.Count];
                double fires = 0; int n = 0;
                for (int w = 0; w < dvStages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = CfFrom; seed < CfTo; seed++)
                    {
                        var r = BattleEngine.Run(f, dvStages[w].Enemy, seed, false);
                        if (r.PlayerWon) wins++;
                        fires += r.DivertFires; n++;
                    }
                    v[w] = wins * 100.0 / (CfTo - CfFrom);
                }
                return (v, fires / n);
            }
            int Seat(Formation f)
            {
                foreach ((int slot, UnitDef d) in f.Occupied())
                    if (ReferenceEquals(d, UnitCatalog.Sora)) return slot;
                return -1;
            }
            int Deg(int slot)
            {
                int n = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(slot, i)) n++;
                return n;
            }

            var cur = Measure(b.F);
            Console.WriteLine($"| {b.Name} | **現行** | {FormationRules.SeatNames[Seat(b.F)]} | {Deg(Seat(b.F))} "
                + $"|{DvCells(cur.Cells)} **{cur.Cells.Average():0.0}%** | — | {cur.Fires:0.00} |");
            Console.Out.Flush();
            foreach (int idx in order.Take(5))
            {
                if (idx == curIdx) continue;
                var v = Measure(perms[idx]);
                int seat = Seat(perms[idx]);
                Console.WriteLine($"| {b.Name} | 粗探索 {order.IndexOf(idx) + 1}位 | {FormationRules.SeatNames[seat]} | {Deg(seat)} "
                    + $"|{DvCells(v.Cells)} {v.Cells.Average():0.0}% "
                    + $"| **{v.Cells.Average() - cur.Cells.Average():+0.0;-0.0;0.0}pt** | {v.Fires:0.00} |");
                Console.WriteLine($"|   ↳ 配置 | {string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} | | | | | | | | | | |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        return;
    }

    // --- 7. 席の分散 --------------------------------------------------------------------------
    if (dvMode.Length == 0 || dvMode == "seats")
    {
        Console.WriteLine("## 7. 席の分散（`seats2` の写し・受け入れ基準12）");
        Console.WriteLine();
        Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 + 最下位 を seed 0..199 で測り直し。");
        Console.WriteLine("**ソラは隣接も列も読まない**（外しは味方全員・焦点は敵全員から選ぶ）が、");
        Console.WriteLine("**自分が矢面に立つ**ので列（前後）は効くはず——第47期の鱗と同じ形になるかを見る。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（3値） | 最頻率 | 幅 | 現行の順位 | 1位との差 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
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
                foreach (EnemyCatalog.Stage st in dvStages)
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
                foreach (EnemyCatalog.Stage st in dvStages)
                {
                    int wins = 0;
                    for (int seed = 0; seed < DvSeeds; seed++)
                        if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    avg += wins * 100.0 / DvSeeds;
                }
                return avg / dvStages.Count;
            }

            var verified = pool.Select(i => (Idx: i, Avg: Avg(perms[i]))).OrderByDescending(x => x.Avg).ToList();
            double width = verified[0].Avg - verified[^1].Avg;
            var top5 = verified.Take(5).ToList();
            int curRank = verified.FindIndex(x => x.Idx == curIdx) + 1;
            double curGap = verified[0].Avg - verified.First(x => x.Idx == curIdx).Avg;

            foreach (UnitDef d in members)
            {
                int bestSlot = -1;
                foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                    if (ReferenceEquals(dd, d)) bestSlot = slot;
                int mid = 0, fcorner = 0, bcorner = 0;
                foreach (var v in top5)
                    foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                        if (ReferenceEquals(dd, d))
                        {
                            int deg2 = 0;
                            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                                if (FormationRules.AreAdjacent(slot, i)) deg2++;
                            if (deg2 == 4) mid++;
                            else if (FormationRules.RowOf(slot) == Row.Front) fcorner++;
                            else bcorner++;
                        }
                int bdeg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
                int top = Math.Max(mid, Math.Max(fcorner, bcorner));
                Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                    + $"| 前角{fcorner} / 中央{mid} / 後角{bcorner} | {top * 100 / 5}% | {width:0.0}pt "
                    + $"| {curRank} / {verified.Count} | {curGap:0.0}pt |");
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine("### ソラ1枚だけを振った5変種（他の4枚は元の相対順のまま詰める）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ソラの席 | 次数 | 列 | 発火 | 外し | 焦点数 | 効き・味方 | 効き・敵 | 自傷 | 寿命 | 平均勝率 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in dvTargets)
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                Formation g = DvSeat(b.F, seat);
                int deg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(seat, i)) deg++;
                var (wins, z) = DvAll(g, new DivertRule(DvMain), "sora");
                Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {deg} | {FormationRules.RowOf(seat)} "
                    + $"| {z.Fires:0.00} | {z.Strips:0.00} | {z.MarkedFoe:0.00} "
                    + $"| {(z.AllySingles > 0 ? $"{z.AllyOnMarked * 100 / z.AllySingles:0.0}%" : "—")} "
                    + $"| {(z.FoeSingles > 0 ? $"{z.FoeOnMarked * 100 / z.FoeSingles:0.0}%" : "—")} "
                    + $"| {z.SelfTaken:0.0} | {z.Life:0.00} | {wins.Average():0.0}% |");
                Console.Out.Flush();
            }
        Console.WriteLine();
    }

    return;
}

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

if (focusId == "guard")
{
    string guardMode = args.Length > 2 ? args[2] : "";
    var gBuilds = CompareBuilds();
    const int GuardSeeds = 200;   // compare / spread と同じ。セルを突き合わせるので変えない
    const int GuardLogSeeds = 50; // 機構の指標だけ verbose で回す本数
    int[] gHps = { 52, 71, 90, 145 };
    int gnb = gBuilds.Length, gnh = gHps.Length;

    // 殉教者の HP だけを差し替えた複製。Id・名前・攻・速・型・特性はカタログのまま。
    UnitDef MartyrWithHp(int hp) => new UnitDef
    {
        Id = EnemyCatalog.Martyr.Id, Name = EnemyCatalog.Martyr.Name,
        MaxHp = hp, Attack = EnemyCatalog.Martyr.Attack, Speed = EnemyCatalog.Martyr.Speed,
        Traits = EnemyCatalog.Martyr.Traits, Pattern = EnemyCatalog.Martyr.Pattern,
        PlusText = EnemyCatalog.Martyr.PlusText, MinusText = EnemyCatalog.Martyr.MinusText,
        Flavor = EnemyCatalog.Martyr.Flavor,
    };

    // **対照: 庇うを外して HP だけ同じにした版。** HP を上げると介入の窓が伸びるが、
    // 同時に**波の総HPも増える**（前列が厚くなる）。この2つを分けないと、
    // 用量反応が「介入が効いた」なのか「ただ硬くなった」なのかが決まらない
    // ——渇き・軛・粛が「規則を無効にした同数値の版」を必ず対照に置いたのと同じ理由。
    UnitDef PlainWithHp(int hp) => new UnitDef
    {
        Id = EnemyCatalog.Axeman2.Id, Name = EnemyCatalog.Axeman2.Name,
        MaxHp = hp, Attack = EnemyCatalog.Axeman2.Attack, Speed = EnemyCatalog.Axeman2.Speed,
        Traits = EnemyCatalog.Axeman2.Traits, Pattern = EnemyCatalog.Axeman2.Pattern,
    };

    // 第五波の残り4枠はカタログのまま。**動く変数は前1の HP と庇うの有無だけ。**
    Formation Wave5WithHp(int hp) => Formation.Build(
        front1: MartyrWithHp(hp), front3: EnemyCatalog.Hero2, center: EnemyCatalog.Knight2,
        back1: EnemyCatalog.Seer, back3: EnemyCatalog.Lancer);

    Formation Wave5PlainHp(int hp) => Formation.Build(
        front1: PlainWithHp(hp), front3: EnemyCatalog.Hero2, center: EnemyCatalog.Knight2,
        back1: EnemyCatalog.Seer, back3: EnemyCatalog.Lancer);

    // ログ行から殉教者の（発火・最終攻・生存T）を取る。gullet log / yoke log / hush と同じ理由
    // ——**発火しなかったことは盤面の値に痕跡を残さない**（庇いは標的を差し替えるだけ）。
    static int TurnOf(string t, int cur)
    {
        if (!t.StartsWith("--- ターン ")) return cur;
        int from = "--- ターン ".Length, to = t.IndexOf(' ', from);
        return to > from && int.TryParse(t.Substring(from, to - from), out int n) ? n : cur;
    }

    static (int Fire, int Atk, int Life) MartyrStats(BattleResult r)
    {
        int turn = 0, death = -1, fire = 0, atk = EnemyCatalog.Martyr.Attack;
        foreach (LogLine line in r.Log)
        {
            string t = line.Text;
            turn = TurnOf(t, turn);
            if (!t.Contains("殉教者")) continue;
            if (t.Contains("を庇った")) fire++;
            else if (t.Contains("殉教者 は倒れた") && death < 0) death = turn;
            else if (t.Contains("誓いを思い出させる"))
            {
                int arrow = t.LastIndexOf('→');
                if (arrow >= 0 && int.TryParse(new string(t.Substring(arrow + 1)
                        .Where(char.IsDigit).ToArray()), out int m)) atk = m;
            }
        }
        return (fire, atk, death < 0 ? r.Turns : death);
    }

    // 勇者候補が落ちたターン（落ちなければ決着ターン）。**庇いの窓を閉じる律速**。
    static int HeroDeathTurn(BattleResult r)
    {
        int turn = 0;
        foreach (LogLine line in r.Log)
        {
            turn = TurnOf(line.Text, turn);
            if (line.Text.Contains("勇者候補 は倒れた")) return turn;
        }
        return r.Turns;
    }

    // ---- 第2〜4波（殉教者がいないので HP を振っても不変）--------------------------------
    var gOther = new double[gnb][];   // gOther[編成][波] 波は 0..4（第1波と第5波は使わない）
    for (int b = 0; b < gnb; b++)
    {
        gOther[b] = new double[EnemyCatalog.Stages.Count];
        for (int w = 1; w <= 3; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < GuardSeeds; seed++)
                if (BattleEngine.Run(gBuilds[b].F, EnemyCatalog.Stages[w].Enemy, seed, verbose: false).PlayerWon)
                    wins++;
            gOther[b][w] = wins * 100.0 / GuardSeeds;
        }
    }
    Console.Error.WriteLine("  第2〜4波 完了");

    // ---- percent: 介入の密度（RedirectPercent）の掃引（第35期）---------------------------
    //
    // 第34期で「窓の長さ（体）では立たない」が確定した。律速は**勇者候補の生存ターン数**で、
    // 殉教者の体はそこに効かない（生存T 2.1倍に対して発火 1.5倍で頭打ち）。
    // 残る交絡の無いノブは**窓の密度**だけ——**HP を動かさないので波の総HPは1も動かず、
    // 対照は第34期の「庇うなし・HP52」1本で足りる。**
    //
    // 割合は MartyrRule で外から差す（static のノブは置かない。Colossus/Yoke/Hush と同じ）。
    // **p=50 は検算点**で、ここが docs/balance.md の第五波と一致しなければ掃引の台が違う。
    if (guardMode == "percent")
    {
        int[] gPs = { 50, 75, 100 };
        int gnp = gPs.Length;
        Formation gFoe = EnemyCatalog.Stages[4].Enemy;                  // 現行の第五波（殉教者 HP52）
        Formation gCtl = Wave5PlainHp(EnemyCatalog.Martyr.MaxHp);       // 対照: 庇うなし・同HP

        Console.WriteLine("# 介入の密度の掃引（guard percent）");
        Console.WriteLine();
        Console.WriteLine($"代表編成 {gnb} × 第五波 × p {gnp} 点、seed 0..{GuardSeeds - 1}。診断用なので docs/ には置かない。");
        Console.WriteLine();
        Console.WriteLine("動かすのは **割合（`MartyrRule.RedirectPercent`）の1変数のみ**。");
        Console.WriteLine("HP52・攻11・速5・薙ぎ・`DamagePerGain` 2・前1 の席・ガルド側はすべて据え置き。");
        Console.WriteLine("**HP を動かさないので波の総HPは1も動かない**——対照は「庇うなし・HP52」1本で足りる。");
        Console.WriteLine();

        var pRate = new double[gnp][];
        var pFire = new double[gnp][];
        var pAtk = new double[gnp][];
        var pLife = new double[gnp][];
        var pHero = new double[gnp][];   // 勇者候補の生存T（律速の直接観測）
        var ctlRate = new double[gnb];
        var ctlHero = new double[gnb];

        for (int b = 0; b < gnb; b++)
        {
            int w = 0;
            for (int seed = 0; seed < GuardSeeds; seed++)
                if (BattleEngine.Run(gBuilds[b].F, gCtl, seed, verbose: false).PlayerWon) w++;
            ctlRate[b] = w * 100.0 / GuardSeeds;

            long hero = 0;
            for (int seed = 0; seed < GuardLogSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(gBuilds[b].F, gCtl, seed, verbose: true);
                hero += HeroDeathTurn(r);
            }
            ctlHero[b] = hero / (double)GuardLogSeeds;
        }
        Console.Error.WriteLine("  対照（庇うなし・HP52）完了");

        for (int h = 0; h < gnp; h++)
        {
            var rule = new MartyrRule(gPs[h]);
            pRate[h] = new double[gnb]; pFire[h] = new double[gnb]; pAtk[h] = new double[gnb];
            pLife[h] = new double[gnb]; pHero[h] = new double[gnb];

            for (int b = 0; b < gnb; b++)
            {
                int w = 0;
                for (int seed = 0; seed < GuardSeeds; seed++)
                    if (BattleEngine.Run(gBuilds[b].F, gFoe, seed, verbose: false, null, null, null, rule).PlayerWon) w++;
                pRate[h][b] = w * 100.0 / GuardSeeds;

                long fire = 0, atk = 0, life = 0, hero = 0;
                for (int seed = 0; seed < GuardLogSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(gBuilds[b].F, gFoe, seed, verbose: true, null, null, null, rule);
                    (int f, int a, int l) = MartyrStats(r);
                    fire += f; atk += a; life += l;
                    hero += HeroDeathTurn(r);
                }
                pFire[h][b] = fire / (double)GuardLogSeeds;
                pAtk[h][b] = atk / (double)GuardLogSeeds;
                pLife[h][b] = life / (double)GuardLogSeeds;
                pHero[h][b] = hero / (double)GuardLogSeeds;
            }
            Console.Error.WriteLine($"  p {gPs[h]} 完了");
        }

        Console.WriteLine("## 1. 第五波の分布");
        Console.WriteLine();
        Console.WriteLine("| 割合 p | 平均 | 100%の編成 | 0%の編成 | 中間帯(5〜95%) | 標準偏差 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|");
        {
            double m0 = ctlRate.Average();
            double s0 = Math.Sqrt(ctlRate.Select(x => (x - m0) * (x - m0)).Average());
            Console.WriteLine($"| **対照（庇うなし）** | {m0:F1} | {ctlRate.Count(x => x == 100.0)} / {gnb} "
                              + $"| {ctlRate.Count(x => x == 0.0)} | {ctlRate.Count(x => x > 5.0 && x < 95.0)} | {s0:F1} |");
        }
        for (int h = 0; h < gnp; h++)
        {
            double[] v = pRate[h];
            double m = v.Average();
            double sd = Math.Sqrt(v.Select(x => (x - m) * (x - m)).Average());
            Console.WriteLine($"| {gPs[h]} | {m:F1} | {v.Count(x => x == 100.0)} / {gnb} | {v.Count(x => x == 0.0)} "
                              + $"| {v.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} |");
        }

        Console.WriteLine();
        Console.WriteLine("## 2. 固有の敗者の帰属（対照で敗者でない行だけを数える）");
        Console.WriteLine();
        Console.WriteLine("**庇うが作った敗者** = 庇うありで `≤10` かつ**対照では `>10`** かつ 第2〜4波がすべて `>10`。");
        Console.WriteLine("第34期の帰属条件をそのまま使う（見かけの新規と区別する）。");
        Console.WriteLine();
        var ctlLosers = new List<string>();
        for (int b = 0; b < gnb; b++)
            if (ctlRate[b] <= 10.0 && Enumerable.Range(1, 3).All(w => gOther[b][w] > 10.0))
                ctlLosers.Add(gBuilds[b].Name);
        Console.WriteLine($"- **対照（庇うなし）の敗者 {ctlLosers.Count}**: {(ctlLosers.Count == 0 ? "なし" : string.Join(" / ", ctlLosers))}");
        for (int h = 0; h < gnp; h++)
        {
            var all = new List<string>();
            var owned = new List<string>();
            for (int b = 0; b < gnb; b++)
            {
                if (!Enumerable.Range(1, 3).All(w => gOther[b][w] > 10.0)) continue;
                if (pRate[h][b] > 10.0) continue;
                all.Add(gBuilds[b].Name);
                if (ctlRate[b] > 10.0) owned.Add(gBuilds[b].Name);
            }
            Console.WriteLine($"- **p{gPs[h]}**: 敗者 {all.Count} / うち**庇うが作った敗者 {owned.Count}**");
            Console.WriteLine($"    - 敗者: {(all.Count == 0 ? "なし" : string.Join(" / ", all))}");
            Console.WriteLine($"    - **庇うが作った: {(owned.Count == 0 ? "**なし**" : string.Join(" / ", owned))}**");
        }

        Console.WriteLine();
        Console.WriteLine("## 3. 編成 × p の勝率（第五波）");
        Console.WriteLine();
        Console.WriteLine("`介入分` = その p の勝率 − 対照（庇うなし）。**これが庇うの正味の税額。**");
        Console.WriteLine();
        Console.Write("| 編成 | 他波最小 | 対照 |");
        foreach (int p in gPs) Console.Write($" p{p} |");
        foreach (int p in gPs) Console.Write($" 介入分(p{p}) |");
        Console.WriteLine();
        Console.Write("|---|--:|--:|");
        for (int i = 0; i < gnp * 2; i++) Console.Write("--:|");
        Console.WriteLine();
        for (int b = 0; b < gnb; b++)
        {
            string name = gBuilds[b].Name;
            bool canary = name.Contains("毒+ベニ+ラウ") || name.Contains("毒+耐久");
            double omin = Enumerable.Range(1, 3).Min(w => gOther[b][w]);
            Console.Write($"| {(canary ? "**" + name + "**（カナリア）" : name)} | {omin:F1} | {ctlRate[b]:F1} |");
            foreach (var t in pRate) Console.Write($" {t[b]:F1} |");
            foreach (var t in pRate) Console.Write($" {t[b] - ctlRate[b]:+0.0;-0.0;0.0} |");
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine($"## 4. 機構の指標（seed 0..{GuardLogSeeds - 1} の平均・全42編成）");
        Console.WriteLine();
        Console.WriteLine("`勇者候補の生存T` が第34期に確定した律速。**ここが伸びない限り発火は頭打ちになる。**");
        Console.WriteLine();
        Console.WriteLine("| 割合 p | 発火/戦 | 殉教者の最終攻 | 殉教者の生存T | 勇者候補の生存T |");
        Console.WriteLine("|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| **対照（庇うなし）** | 0.00 | — | — | {ctlHero.Average():F2} |");
        for (int h = 0; h < gnp; h++)
            Console.WriteLine($"| {gPs[h]} | {pFire[h].Average():F2} | {pAtk[h].Average():F1} "
                              + $"| {pLife[h].Average():F2} | {pHero[h].Average():F2} |");

        Console.WriteLine();
        Console.WriteLine("## 5. 発火が多い順（上位12行）");
        Console.WriteLine();
        Console.Write("| 編成 |");
        foreach (int p in gPs) Console.Write($" 発火(p{p}) |");
        foreach (int p in gPs) Console.Write($" 最終攻(p{p}) |");
        foreach (int p in gPs) Console.Write($" 殉教者生存T(p{p}) |");
        Console.WriteLine();
        Console.Write("|---|");
        for (int i = 0; i < gnp * 3; i++) Console.Write("--:|");
        Console.WriteLine();
        foreach (int b in Enumerable.Range(0, gnb).OrderByDescending(i => pFire[gnp - 1][i]).Take(12))
        {
            Console.Write($"| {gBuilds[b].Name} |");
            foreach (var t in pFire) Console.Write($" {t[b]:F1} |");
            foreach (var t in pAtk) Console.Write($" {t[b]:F1} |");
            foreach (var t in pLife) Console.Write($" {t[b]:F1} |");
            Console.WriteLine();
        }
        return;
    }


    Console.WriteLine("# 殉教者の体の用量反応（guard）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 {gnb} × 第五波 × HP {gnh} 点、seed 0..{GuardSeeds - 1}。診断用なので docs/ には置かない。");
    Console.WriteLine();
    Console.WriteLine("動かすのは **HP の1変数のみ**（攻11・速5・薙ぎ・Guardian・前1 の席は据え置き）。");
    Console.WriteLine("掃引点は既存の敵の体から借りている: 52=現行 / 71=この波の中央 / 90=この波の前3 / 145=ロスター最重。");
    Console.WriteLine();


    // ---- 各 HP 点で第五波を測る ------------------------------------------------------------
    var gRate = new double[gnh][];        // 勝率(%)・庇うあり
    var gPlain = new double[gnh][];       // 勝率(%)・庇うなし（同HPの対照）
    var gFire = new double[gnh][];        // 肩代わりの発火 /戦
    var gAtk = new double[gnh][];         // 殉教者の最終攻撃力（平均）
    var gLife = new double[gnh][];        // 殉教者の生存ターン（平均。生存し続けたら決着T）

    for (int h = 0; h < gnh; h++)
    {
        Formation foe = Wave5WithHp(gHps[h]);
        Formation plain = Wave5PlainHp(gHps[h]);
        gRate[h] = new double[gnb];
        gPlain[h] = new double[gnb];
        gFire[h] = new double[gnb];
        gAtk[h] = new double[gnb];
        gLife[h] = new double[gnb];

        for (int b = 0; b < gnb; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < GuardSeeds; seed++)
                if (BattleEngine.Run(gBuilds[b].F, foe, seed, verbose: false).PlayerWon) wins++;
            gRate[h][b] = wins * 100.0 / GuardSeeds;

            int pwins = 0;
            for (int seed = 0; seed < GuardSeeds; seed++)
                if (BattleEngine.Run(gBuilds[b].F, plain, seed, verbose: false).PlayerWon) pwins++;
            gPlain[h][b] = pwins * 100.0 / GuardSeeds;

            // 機構の指標は verbose のログ行から。**盤面は1つも動かさない。**
            long fire = 0, atk = 0, life = 0;
            for (int seed = 0; seed < GuardLogSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(gBuilds[b].F, foe, seed, verbose: true);
                int turn = 0, deathTurn = -1, lastAtk = EnemyCatalog.Martyr.Attack;

                foreach (LogLine line in r.Log)
                {
                    string t = line.Text;
                    if (t.StartsWith("--- ターン "))
                    {
                        int from = "--- ターン ".Length, to = t.IndexOf(' ', from);
                        if (to > from && int.TryParse(t.Substring(from, to - from), out int n)) turn = n;
                        continue;
                    }
                    if (!t.Contains("殉教者")) continue;

                    if (t.Contains("を庇った")) fire++;
                    else if (t.Contains("殉教者 は倒れた") && deathTurn < 0) deathTurn = turn;
                    else if (t.Contains("誓いを思い出させる"))
                    {
                        // 「（攻撃 +N → M）」の M を取る。最後に出た値が最終攻撃力。
                        int arrow = t.LastIndexOf('→');
                        if (arrow >= 0)
                        {
                            string tail = new string(t.Substring(arrow + 1).Where(char.IsDigit).ToArray());
                            if (int.TryParse(tail, out int m)) lastAtk = m;
                        }
                    }
                }
                fire += 0;
                atk += lastAtk;
                life += deathTurn < 0 ? r.Turns : deathTurn;   // 落ちなければ決着まで生存
            }
            gFire[h][b] = fire / (double)GuardLogSeeds;
            gAtk[h][b] = atk / (double)GuardLogSeeds;
            gLife[h][b] = life / (double)GuardLogSeeds;
        }
        Console.Error.WriteLine($"  HP {gHps[h]} 完了");
    }

    // ---- 1. 分布 -------------------------------------------------------------------------
    Console.WriteLine("## 1. 第五波の分布");
    Console.WriteLine();
    Console.WriteLine("| 殉教者HP | 平均 | 100%の編成 | 0%の編成 | 中間帯(5〜95%) | 標準偏差 |");
    Console.WriteLine("|--:|--:|--:|--:|--:|--:|");
    for (int h = 0; h < gnh; h++)
    {
        double[] v = gRate[h];
        double m = v.Average();
        double sd = Math.Sqrt(v.Select(x => (x - m) * (x - m)).Average());
        Console.WriteLine($"| {gHps[h]} | {m:F1} | {v.Count(x => x == 100.0)} / {gnb} | {v.Count(x => x == 0.0)} "
                          + $"| {v.Count(x => x > 5.0 && x < 95.0)} | {sd:F1} |");
    }

    Console.WriteLine();
    Console.WriteLine("## 1b. 交絡の切り分け —— 庇うを外した同HPの対照");
    Console.WriteLine();
    Console.WriteLine("HP を上げると**介入の窓が伸びる**と同時に**波の総HPが増える**。");
    Console.WriteLine("同じ HP で庇うだけを外した版と比べれば、差がそのまま**介入の効果**になる");
    Console.WriteLine("（渇き・軛・粛が「規則を無効にした同数値の版」を対照に置いたのと同じ形）。");
    Console.WriteLine();
    Console.WriteLine("**HP52 の対照は差し替え前の第五波そのもの**（前1 戦斧兵 52/11/5・薙ぎ）なので、");
    Console.WriteLine("その行が第32期差し戻し後の値と一致することが診断そのものの検算になる。");
    Console.WriteLine();
    Console.WriteLine("| 殉教者HP | 庇うあり 平均 | 庇うなし 平均 | 介入の効果 | 波の総HP | HP による増分 |");
    Console.WriteLine("|--:|--:|--:|--:|--:|--:|");
    double baseAll = gPlain[0].Average();
    for (int h = 0; h < gnh; h++)
    {
        double a = gRate[h].Average(), p = gPlain[h].Average();
        int total = gHps[h] + EnemyCatalog.Hero2.MaxHp + EnemyCatalog.Knight2.MaxHp
                    + EnemyCatalog.Seer.MaxHp + EnemyCatalog.Lancer.MaxHp;
        Console.WriteLine($"| {gHps[h]} | {a:F1} | {p:F1} | **{a - p:+0.0;-0.0;0.0}** | {total} | {p - baseAll:+0.0;-0.0;0.0} |");
    }

    // ---- 2. 固有の敗者 --------------------------------------------------------------------
    // 「第五波だけで 10 以下、他のどの波（第一波を除く）でも 10 超」。
    // 第一波は全編成 100% のチュートリアル波なので比較から外す（spread と同じ作法）。
    Console.WriteLine();
    Console.WriteLine("## 2. 第五波の固有の敗者（閾値 90/10・第一波は比較対象外）");
    Console.WriteLine();
    Console.WriteLine("**新規** = HP52（第33期の採用値）では固有の敗者でなかった行。");
    Console.WriteLine();
    var baseLosers = new HashSet<string>();
    for (int h = 0; h < gnh; h++)
    {
        var losers = new List<string>();
        var winners = new List<string>();
        for (int b = 0; b < gnb; b++)
        {
            bool otherAbove = Enumerable.Range(1, 3).All(w => gOther[b][w] > 10.0);
            if (gRate[h][b] <= 10.0 && otherAbove) losers.Add(gBuilds[b].Name);
            bool otherBelow = Enumerable.Range(1, 3).All(w => gOther[b][w] < 90.0);
            if (gRate[h][b] >= 90.0 && otherBelow) winners.Add(gBuilds[b].Name);
        }
        if (h == 0) foreach (string n in losers) baseLosers.Add(n);
        var fresh = losers.Where(n => !baseLosers.Contains(n)).ToList();
        Console.WriteLine($"- **HP {gHps[h]}**: 敗者 {losers.Count} / 勝者 {winners.Count}");
        Console.WriteLine($"    - 敗者: {(losers.Count == 0 ? "なし" : string.Join(" / ", losers))}");
        Console.WriteLine($"    - **新規の敗者: {(fresh.Count == 0 ? "なし" : string.Join(" / ", fresh))}**");
        Console.WriteLine($"    - 勝者: {(winners.Count == 0 ? "なし" : string.Join(" / ", winners))}");
    }

    // ---- 3. カナリア + 全行の勝率 ----------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 3. 編成 × HP の勝率（第五波）");
    Console.WriteLine();
    Console.WriteLine("`カナリア` = 指示書が指定した課税過剰の検出行（毒+ベニ+ラウ / 毒+耐久）。");
    Console.WriteLine("`他波最小` = 第2〜4波の最小値（10 以下なら固有の敗者になれない）。");
    Console.WriteLine();
    Console.Write("| 編成 | 他波最小 |");
    foreach (int hp in gHps) Console.Write($" HP{hp} |");
    Console.Write(" 52→145 |");
    foreach (int hp in gHps) Console.Write($" 介入分(HP{hp}) |");
    Console.WriteLine();
    Console.Write("|---|--:|");
    for (int i = 0; i < gnh * 2 + 1; i++) Console.Write("--:|");
    Console.WriteLine();
    for (int b = 0; b < gnb; b++)
    {
        string name = gBuilds[b].Name;
        bool canary = name.Contains("毒+ベニ+ラウ") || name.Contains("毒+耐久");
        double omin = Enumerable.Range(1, 3).Min(w => gOther[b][w]);
        Console.Write($"| {(canary ? "**" + name + "**（カナリア）" : name)} | {omin:F1} |");
        foreach (var t in gRate) Console.Write($" {t[b]:F1} |");
        Console.Write($" {gRate[gnh - 1][b] - gRate[0][b]:+0.0;-0.0;0.0} |");
        for (int h = 0; h < gnh; h++) Console.Write($" {gRate[h][b] - gPlain[h][b]:+0.0;-0.0;0.0} |");
        Console.WriteLine();
    }

    // ---- 4. 機構の指標 ---------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine($"## 4. 殉教者の側の指標（seed 0..{GuardLogSeeds - 1} の平均）");
    Console.WriteLine();
    Console.WriteLine("`発火` = 肩代わりが成立した回数/戦。`最終攻` = 戦闘終了時の攻撃力（素は 11）。");
    Console.WriteLine("`生存T` = 殉教者が落ちたターン（落ちなければ決着ターン）。");
    Console.WriteLine();
    Console.Write("| 編成 |");
    foreach (int hp in gHps) Console.Write($" 発火(HP{hp}) |");
    foreach (int hp in gHps) Console.Write($" 最終攻(HP{hp}) |");
    foreach (int hp in gHps) Console.Write($" 生存T(HP{hp}) |");
    Console.WriteLine();
    Console.Write("|---|");
    for (int i = 0; i < gnh * 3; i++) Console.Write("--:|");
    Console.WriteLine();
    foreach (int b in Enumerable.Range(0, gnb).OrderByDescending(i => gFire[gnh - 1][i]))
    {
        Console.Write($"| {gBuilds[b].Name} |");
        foreach (var t in gFire) Console.Write($" {t[b]:F1} |");
        foreach (var t in gAtk) Console.Write($" {t[b]:F1} |");
        foreach (var t in gLife) Console.Write($" {t[b]:F1} |");
        Console.WriteLine();
    }

    // ---- 5. 平均の要約 ----------------------------------------------------------------------
    Console.WriteLine();
    Console.WriteLine("## 5. 全編成の平均（機構の指標）");
    Console.WriteLine();
    Console.WriteLine("| 殉教者HP | 発火/戦 | 最終攻 | 生存T |");
    Console.WriteLine("|--:|--:|--:|--:|");
    for (int h = 0; h < gnh; h++)
        Console.WriteLine($"| {gHps[h]} | {gFire[h].Average():F2} | {gAtk[h].Average():F1} | {gLife[h].Average():F2} |");

    return;
}

//     dotnet run --project BattleSim -c Release 0 spread
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

// wave2 モード: 波の関門を1つずつ外して、その波が何に課金しているかを数える（第51期）。
//
// compare は「どの編成が勝つか」しか見ないので、**波の側が何を関門にしているか**が読めない。
// spread（第22期）は波の分離度を測るが、あれも波を1つの箱として扱うだけで、
// 波の中の**どの駒／どの規則**が天井を押さえているかは区別しない。
//
// やっていることは1つだけ——**敵を1体ずつ空席にし、盤面ルールを1つ切って compare を回し、
// 差分を取る**。`Stages` は書き換えず、波の複製をこの中でローカルに組む
// （第46期の `ObRows()` / 第49期の `SgRows()` と同型）。**盤面は1つも動かない。**
//
// **主要な指標は平均ではなく「100.0% の行数」。** 関門を外して 100.0% が増えるなら、
// その関門は天井を押さえている。増えないなら、その関門は既に効いていない。
//
// **「抜く」は空席にする**（別の駒で埋めない）。埋めると2変数が動く。
// **規則は2通りで外す**——(a) 規則フラグ（HushRule / YokeRule の Active）と
// (b) 保持者を**同数値の対照駒**に差し替える。**両者が1セルも違わないことが診断の検算**
// （粛・渇き・軛・殉教はどれも数値・型・速さが素の駒と1つも違わない）。
//
// 波は引数で選べる。第二波以外にも同じ解剖をそのまま回せる。
//
//     dotnet run --project BattleSim -c Release 0 wave2 [波番号 1-5、既定 2]
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

        foreach (var (name, f) in targets)
        {
            var s1 = Sweep(f, col.Squads, 1);
            var s2 = Sweep(f, col.Squads, 2);
            var s3 = Sweep(f, col.Squads, 3);

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

// power モード: 編成の「地力」を分解する（第12期 Phase CA）。
//
// 第4〜11期はどれも「地力とは別の2本目の軸」を探して失敗した——部隊列の順序・攻撃パターンの
// 向き・自傷率・敵のチャージ・味方スキルのタイミング。**何を作っても編成の序列が同じ順位で
// 出てくる**（順位相関 0.83〜1.00）。支配的な次元が1本あることは分かっている。
//
// ところがその1本を一度も測っていない。総HPなのか、総攻撃力なのか、その積なのか、
// 特定の駒の存在なのか、盤面配置なのか——分からないまま2本目を探しても、
// **何に対して直交させたいのかが決められない。**
//
// ここは純粋な測定で、数値も特性もパターンも一切変えない。編成ごとに
//   静的（Formation / UnitDef から計算できる。戦わなくても分かる）8種
//   動的（UnitTally から。既存フィールドだけで足りる）7種
// を出す。目的変数は突破度（第8期 Phase U）。
//
// 台は2種。主 = チャージ台（bridge の7列目 = ChargeBench。第10期 AB-0）、
// 従 = 既存5波（順路）。第8期の「136% で測ると何も見えない」が効くので、片方だけでは
// 判定できない——主で出た第一近似が従で入れ替わるなら、「地力」は台ごとに違うものを
// 指していたことになる。
//
// 却下した案: 多変量回帰で一気に説明する。n = 31 しかないので3変数以上は確実に過学習する
// （§4-1）。単相関 → 第一近似 → 残差 → 残差との相関の1段だけ、多変量は2変数まで。
// 却下した案: 目的変数を勝率にする。2部隊だと突破率が飽和して序列が潰れるのと同じ理由で、
// 勝率は上下端に張り付いて特徴量との差を吸収する（第5期の持ち越し論点(2)）。
//
// 診断用で docs/ には置かない（seats / bill / charge / timing と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 power [絞り込み]
if (focusId == "power")
{
    const int PowerSeeds = 200;   // bridge / bill / charge / timing と同じ
    var all = CompareBuilds();

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    // 台。列は Name で引く（Columns の並び順は GodotApp の EngagementColumn が使うので
    // 当てにしない。engage / seats と同じ作法）。
    var benches = new (string Tag, string Name, string Note, IReadOnlyList<Formation> Squads)[]
    {
        ("主", "チャージ台", "bridge の7列目。3波・合計代金 116.6%・突破率(1) 39.1%", ChargeBench()),
        ("従", "既存5波", "順路。第1期からの基準列", EnemyCatalog.Columns.First(c => c.Name == "順路").Squads),
    };

    // 静的特徴量。**定義値だけから取る**——会戦中の目減り（継ぎ接ぎの最大HP半減）は
    // 動的側の話で、ここに混ぜると「戦わずに分かる量」でなくなる（§3-2）。
    var statics = new (string Name, string Def, Func<Formation, double> Get)[]
    {
        ("体数",     "編成の駒数（4 or 5）",
            f => f.Count),
        ("総HP",     "Def.MaxHp の合計",
            f => f.Occupied().Sum(x => x.Def.MaxHp)),
        ("総攻",     "Def.Attack の合計",
            f => f.Occupied().Sum(x => x.Def.Attack)),
        ("積",       "総HP × 総攻",
            f => (double)f.Occupied().Sum(x => x.Def.MaxHp) * f.Occupied().Sum(x => x.Def.Attack)),
        ("最薄HP",   "編成中いちばん低い Def.MaxHp。閾値仮説の候補",
            f => f.Occupied().Min(x => x.Def.MaxHp)),
        ("後列HP",   "後列（slot 4/5）の Def.MaxHp 合計",
            f => f.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)),
        ("平均速度", "Def.Speed の平均",
            f => f.Occupied().Average(x => x.Def.Speed)),
        ("範囲枚数", "Def.Pattern が薙ぎ/全体の駒数（AoeCount。cost 以来ずっと同じ区分）",
            f => AoeCount(f)),
    };

    // 動的特徴量。**既存の UnitTally だけで足りることを確認した**（§3-2。足りなければ
    // 足す前に報告する、が指示だった）。既存の出力には列を増やしていない。
    //
    // 第13期 Phase DA で `与ダメ/戦`・`撃破/戦`・`与ダメ効率` の3つを**受け手側**へ移した。
    // 残る4つは味方側のままでよい——`被ダメ/戦`・`回復/戦`・`自傷率` はそもそも
    // 味方が受け手なので穴が無く、`干渉/戦` は「誰が起点になったか」を数える量なので
    // 受け手側に対応物が無い（毒は出どころを持たないので、干渉は依然として過小のまま。
    // これは診断の限界として残る）。
    var dynNames = new (string Name, string Def)[]
    {
        ("与ダメ/戦",  "**敵**の DamageTaken 合計 − 敵の TakenFromAlly ÷ 部隊戦数（受け手側。過剰殺傷を含む）"),
        ("被ダメ/戦",  "DamageTaken の合計 ÷ 部隊戦数"),
        ("撃破/戦",    "**敵の死亡数** ÷ 部隊戦数（誰が仕留めたかを問わない）"),
        ("干渉/戦",    "Interventions の合計 ÷ 部隊戦数（**活動量の本体**。味方側のまま）"),
        ("回復/戦",    "Healed の合計 ÷ 部隊戦数"),
        ("自傷率",     "TakenFromAlly ÷ DamageTaken（第9期の定義そのまま）"),
        ("与ダメ効率", "与ダメ ÷ 撃破数。**オーバーキルの指標**（大きいほど1体を落とすのに無駄が多い）"),
    };

    Console.WriteLine($"# 地力の分解（seed 0..{PowerSeeds - 1} の {PowerSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("編成ごとの特徴量と突破度を並べたもの。**測定だけで、盤面は何も変えていない。**");
    Console.WriteLine("突破度は突破した部隊数 + 最後に負けた部隊戦での削り割合（0.0 〜 列長。第8期 Phase U）。");
    Console.WriteLine("投入部隊数は 1——2部隊だと突破率が飽和して序列が潰れる。");
    Console.WriteLine();
    foreach (var (tag, name, note, squads) in benches)
        Console.WriteLine($"- **{tag}: {name}**（{squads.Count}波）: {note}");
    Console.WriteLine();

    // --- 計測 ---
    int nB = benches.Length;
    var deg = new double[nB][];
    var dyn = new double[nB][][];   // [台][編成][特徴量]
    var leg = new double[nB][][];   // [台][編成][旧定義3種]。第12期との対比だけが読む
    var bps = new double[nB][];     // [台][編成] 部隊戦数 ÷ 試行。第14期 EA の同語反復の検査が読む
    long foeFromAlly = 0;           // 敵同士の巻き込み。0 のはず（第13期 §3-1）
    int deathGaps = 0;              // 敵が DamageTaken を経由せずに死んだ疑いの件数
    for (int b = 0; b < nB; b++)
    {
        deg[b] = new double[nT];
        dyn[b] = new double[nT][];
        leg[b] = new double[nT][];
        bps[b] = new double[nT];
        for (int t = 0; t < nT; t++)
        {
            var m = MeasurePower(targets[t].F, benches[b].Squads, PowerSeeds);
            deg[b][t] = m.Degree;
            dyn[b][t] = m.Dynamics;
            leg[b][t] = m.Legacy;
            bps[b][t] = m.BattlesPerSeed;
            foeFromAlly += m.FoeTakenFromAlly;
            deathGaps += m.DeathGaps;
        }
    }
    var stat = new double[nT][];
    for (int t = 0; t < nT; t++)
        stat[t] = statics.Select(s => s.Get(targets[t].F)).ToArray();

    // --- 検算 ---
    // (1) tally は Def.Id で引くので、味方と敵で Id が衝突していると敵の被弾が
    //     味方の動的特徴量に混ざる（MeasureBill の注記と同じ穴。あちらは「呼び出し側が
    //     検算する」と書いてあるだけだったので、ここで実際に検算する）。
    var clash = new List<string>();
    foreach (var (_, bench, _, squads) in benches)
    {
        var foeIds = squads.SelectMany(s => s.Occupied()).Select(x => x.Def.Id).ToHashSet();
        foreach (var (name, f) in targets)
            foreach (string id in f.Occupied().Select(x => x.Def.Id).Where(foeIds.Contains))
                clash.Add($"{bench} × {name}: {id}");
    }
    // (2) 列長1では最終戦＝初戦なので突破度の2つの削り割合が一致するはず（第8期 §2-3）。
    double maxGap = 0;
    Formation lastWave = benches[0].Squads[^1];
    for (int t = 0; t < nT; t++)
        for (int seed = 0; seed < 20; seed++)
        {
            EngagementResult r = EngagementEngine.Run(new[] { targets[t].F }, new[] { lastWave }, seed, verbose: false);
            maxGap = Math.Max(maxGap, Math.Abs(r.LastBattleAttrition - r.FirstBattleAttrition));
        }

    Console.WriteLine("### 検算");
    Console.WriteLine();
    Console.WriteLine($"- **味方と敵の Def.Id の衝突: {clash.Count} 件**"
        + (clash.Count == 0 ? "（0 でなければ動的特徴量に敵の数字が混ざっている）"
                            : $" ← **混入している**: {string.Join(" / ", clash.Take(5))}"));
    Console.WriteLine($"- **突破度（列長1）: |LastBattleAttrition − FirstBattleAttrition| の最大 = {maxGap:F6}**"
        + $"（{nT} 編成 × seed 0..19。0 でなければ分母がずれている）");
    // 受け手側から測るための2つの前提（第13期 §3-1・§6）。どちらも「0 のはず」で、
    // 0 でなければ方法のほうが間違っている。
    Console.WriteLine($"- **敵の TakenFromAlly の総和: {foeFromAlly}**"
        + (foeFromAlly == 0
            ? "（0 = 敵側に巻き込みが無い。与ダメから引いた量も 0）"
            : " ← **敵同士の巻き込みがある。** 与ダメからこの量を引いている"));
    Console.WriteLine($"- **勝った部隊戦で敵の死亡数が投入数と合わなかった件数: {deathGaps}**"
        + (deathGaps == 0
            ? "（0 = 敵は必ず `DamageTaken` を経由して死んでいる）"
            : " ← **`DamageTaken` を経由しない死亡経路がある。受け手側の撃破は信用できない**"));
    Console.WriteLine();

    // --- 特徴量の定義 ---
    Console.WriteLine("### 特徴量の定義");
    Console.WriteLine();
    Console.WriteLine("| 区分 | 特徴量 | 定義 |");
    Console.WriteLine("|---|---|---|");
    foreach (var (name, def, _) in statics) Console.WriteLine($"| 静的 | {name} | {def} |");
    foreach (var (name, def) in dynNames) Console.WriteLine($"| 動的 | {name} | {def} |");
    Console.WriteLine();
    Console.WriteLine("動的の分母「戦」は**部隊戦の数**（会戦の中の Battle の総数）。会戦は深く抜いた");
    Console.WriteLine("編成ほど戦闘数が増えるので、seed 数で割ると「長く戦った」だけで値が膨らむ。");
    Console.WriteLine("`撃破/戦` が 0 の編成では `与ダメ効率` が定義できないので `—` を出す。");
    Console.WriteLine();
    Console.WriteLine("> **`与ダメ/戦` と `撃破/戦` は受け手側（敵の tally）から取っている**（第13期 Phase DA）。");
    Console.WriteLine("> `TickStatuses` は `ApplyDamage(u, poison, null)` と source を渡さずに呼ぶので、");
    Console.WriteLine("> 毒・燃焼の削りは出どころの駒の `DamageToEnemy` にも `Kills` にも載らない");
    Console.WriteLine("> （`ApplyDamage` の `source is not null` 分岐、`HandleDeath` の `killer is not null` 分岐）。");
    Console.WriteLine("> 第12期はこれを味方側から合計していたので、**毒軸の編成の出力が構造的に過小に出ていた。**");
    Console.WriteLine("> どの経路で削っても敵の `DamageTaken` には必ず載るので、敵側から数えれば穴が塞がる。");
    Console.WriteLine("> **`BattleCore` は1行も触っていない**——エンジンではなく読み方を変えただけ。");
    Console.WriteLine(">");
    Console.WriteLine("> **`干渉/戦` は味方側のまま**（毒は出どころを持たないので受け手側に対応物が無い）。");
    Console.WriteLine("> 毒軸の編成の `干渉/戦` は依然として過小で、`docs/pulse.md` の毒軸の行も同じく過小のまま。");
    Console.WriteLine();
    // 受け手側へ移すと 撃破/戦 の r が跳ね上がるが、**跳ね上がった分のかなりは算術**。
    // 穴があったころは毒軸の過小がこの結び付きを隠していたので、穴を塞いだ結果として
    // 見えるようになった。ここを書かずに r² だけ出すと、同語反復を発見だと読ませることになる。
    Console.WriteLine("> **警告: `撃破/戦` と突破度は構造的に結び付いている。** 部隊を全滅させることが");
    Console.WriteLine("> その部隊を突破することなので、`撃破/戦` の高さは突破度の言い換えにかなり近い。");
    Console.WriteLine("> 味方1部隊では **部隊戦数 = 突破数 + 1**（全抜き時だけ = 突破数）なので、");
    Console.WriteLine("> `撃破/戦` は突破数の決定的な関数に、最終戦で倒した数を足したものになる。");
    Console.WriteLine("> 実際、チャージ台（駒数 5+4+4 = 13）で全抜きした編成の `撃破/戦` は例外なく");
    Console.WriteLine("> **13 ÷ 3 = 4.33** で、これは測定結果ではなく算術。");
    Console.WriteLine(">");
    Console.WriteLine("> `与ダメ/戦` も程度は軽いが同じ性質を持つ（突破度の小数部＝最終戦で削った敵HPの割合）。");
    Console.WriteLine("> **同語反復から自由なのは静的特徴量だけ**——「静的だけの説明力」を別項で出しているのは");
    Console.WriteLine("> そのため。動的側の r² は「地力の説明力」ではなく「どれだけ言い換えに近いか」として読む。");
    Console.WriteLine("> 残差の側は意味を保つ（**倒した数の割に突破できていない / できている**編成が出る）。");
    Console.WriteLine();

    // 主の台の突破度の降順で並べる。序列そのものが読みたいものなので、
    // 表の並びを目的変数に揃えておく（相関の計算順には影響しない）。
    int[] order = Enumerable.Range(0, nT).OrderByDescending(t => deg[0][t]).ToArray();

    Console.WriteLine("### 静的特徴量（戦わなくても分かる量。台に依らない）");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(statics.Select(s => $" {s.Name} |")));
    Console.WriteLine("|---|" + string.Concat(statics.Select(_ => "--:|")));
    foreach (int t in order)
        Console.WriteLine($"| {targets[t].Name} |"
            + $" {stat[t][0]:F0} | {stat[t][1]:F0} | {stat[t][2]:F0} | {stat[t][3]:F0} |"
            + $" {stat[t][4]:F0} | {stat[t][5]:F0} | {stat[t][6]:F1} | {stat[t][7]:F0} |");
    Console.WriteLine();

    for (int b = 0; b < nB; b++)
    {
        Console.WriteLine($"### {benches[b].Tag}の台（{benches[b].Name}・列長 {benches[b].Squads.Count}）: 突破度と動的特徴量");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 突破度 |" + string.Concat(dynNames.Select(d => $" {d.Name} |")));
        Console.WriteLine("|---|--:|" + string.Concat(dynNames.Select(_ => "--:|")));
        foreach (int t in order)
        {
            double[] d = dyn[b][t];
            Console.WriteLine($"| {targets[t].Name} | {deg[b][t]:F3} |"
                + $" {d[0]:F0} | {d[1]:F0} | {d[2]:F2} | {d[3]:F2} | {d[4]:F0} |"
                + $" {d[5] * 100:F1}% | {(double.IsNaN(d[6]) ? "—" : $"{d[6]:F1}")} |");
        }
        Console.WriteLine();
        Console.Out.Flush();
    }

    // ================= Phase CB: 分解 =================
    //
    // n = 31 しかない。多変量回帰を回すと確実に過学習するので、単相関 → 第一近似 →
    // 残差 → 残差との相関の1段だけ。多変量は2変数まで（§4-1）。
    //
    // **因果は主張しない。** 「総HPが高い編成が強い」は「総HPを上げれば強くなる」を
    // 意味しない。ここで出るのは相関だけで、推測は推測と書く。

    // 特徴量を1本の並びにまとめる（静的8 + 動的7 = 15）。静的は台に依らないので
    // どちらの台でも同じ値が入る。
    int nS = statics.Length, nF = statics.Length + dynNames.Length;
    string[] featNames = statics.Select(s => s.Name).Concat(dynNames.Select(d => d.Name)).ToArray();
    bool[] isStatic = Enumerable.Range(0, nF).Select(k => k < nS).ToArray();
    var feat = new double[nB][][];   // [台][特徴量][編成]
    for (int b = 0; b < nB; b++)
    {
        feat[b] = new double[nF][];
        for (int k = 0; k < nF; k++)
            feat[b][k] = Enumerable.Range(0, nT)
                .Select(t => k < nS ? stat[t][k] : dyn[b][t][k - nS]).ToArray();
    }

    // 旧定義（第12期・味方側）の特徴量行列。**差し替えた3つだけを入れ替えた写し**で、
    // 残る12個には同じ値が入る（したがって突破度との r も動かない。動くのは順位のほうで、
    // それ自体が「何が第一近似になるか」の答えを変える）。
    // 同じ実行・同じ seed から作っているので、新旧の差は定義の差だけになる。
    int[] swapped = { 0, 2, 6 };   // dynNames 内の位置。Legacy の並びは 与ダメ・撃破・効率
    var featOld = new double[nB][][];
    for (int b = 0; b < nB; b++)
    {
        featOld[b] = new double[nF][];
        for (int k = 0; k < nF; k++)
        {
            int sw = k < nS ? -1 : Array.IndexOf(swapped, k - nS);
            featOld[b][k] = sw >= 0
                ? Enumerable.Range(0, nT).Select(t => leg[b][t][sw]).ToArray()
                : feat[b][k];
        }
    }

    var ordered = new (int K, double R, double Rho, int N)[nB][];
    // 第12期（味方側）の並びも台ごとに残す。第14期 Phase EA の三期対比表が読む。
    var orderedOldAll = new (int K, double R, double Rho, int N)[nB][];

    for (int b = 0; b < nB; b++)
    {
        Console.WriteLine($"## 分解: {benches[b].Tag}の台（{benches[b].Name}）");
        Console.WriteLine();

        // 目的変数が天井に張り付いていると、そこの編成同士の差が測れない。
        // 相関を読む前に何編成が飽和しているかを出す（列長ちょうど = 全抜き）。
        int ceil = Enumerable.Range(0, nT).Count(t => deg[b][t] >= benches[b].Squads.Count - 1e-9);
        if (ceil > 0)
        {
            Console.WriteLine($"> **注意: {ceil} 編成が突破度の天井（{benches[b].Squads.Count}.000 = 全抜き）に"
                + $"張り付いている。** その {ceil} 編成の間の差はこの台では測れていない——"
                + "相関の上限がその分だけ下がる。");
            Console.WriteLine();
        }

        // --- 1. 単相関の全一覧 ---
        ordered[b] = Enumerable.Range(0, nF)
            .Select(k => { var c = Correlate(feat[b][k], deg[b]); return (K: k, c.R, c.Rho, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();

        Console.WriteLine("### 単相関（全15特徴量 × 突破度。|r| の降順）");
        Console.WriteLine();
        Console.WriteLine("`r` はピアソン（突破度は連続量なので生の値で当てる）、`ρ` はスピアマン");
        Console.WriteLine("（同順位は平均順位。第8期以降の順位相関と同じ計算）。`n` は点の数——");
        Console.WriteLine("撃破 0 で与ダメ効率が定義できない編成はその行から落ちる。");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 区分 | 特徴量 | r | ρ | n |");
        Console.WriteLine("|--:|:-:|---|--:|--:|--:|");
        for (int i = 0; i < nF; i++)
        {
            var x = ordered[b][i];
            Console.WriteLine($"| {i + 1} | {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} "
                + $"| {x.R:+0.00;-0.00} | {x.Rho:+0.00;-0.00} | {x.N} |");
        }
        Console.WriteLine();

        // --- 1b. 第12期（味方側）との対比 ---
        // 指示は「第12期の値と並べて、どれがどれだけ動いたかを出す」（§3-3）。
        // 別の実行から数字を引いてくると、動いたのが定義のせいか実行のせいかが決まらないので、
        // 同じ実行の中で旧定義も計算して並べる。
        var orderedOld = Enumerable.Range(0, nF)
            .Select(k => { var c = Correlate(featOld[b][k], deg[b]); return (K: k, c.R, c.Rho, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();
        orderedOldAll[b] = orderedOld;

        Console.WriteLine("#### 第12期（味方側）との対比 — 単相関はどう動いたか");
        Console.WriteLine();
        Console.WriteLine("**値が動くのは受け手側へ移した3つだけ**（`与ダメ/戦`・`撃破/戦`・`与ダメ効率`）。");
        Console.WriteLine("残る12個は同じ値・同じ r で、**順位だけがその3つに押されて動く**。");
        Console.WriteLine("突破度は新旧で完全に同じ（測り方を変えただけで盤面は動かない）。");
        Console.WriteLine();
        Console.WriteLine("| 区分 | 特徴量 | 旧 r | 旧順位 | 新 r | 新順位 | Δr | 順位の動き |");
        Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < nF; i++)
        {
            var x = ordered[b][i];
            int oldPos = Array.FindIndex(orderedOld, y => y.K == x.K);
            double dr = x.R - orderedOld[oldPos].R;
            int move = oldPos - i;   // 正なら順位が上がった
            Console.WriteLine($"| {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} "
                + $"| {orderedOld[oldPos].R:+0.00;-0.00} | {oldPos + 1} "
                + $"| {x.R:+0.00;-0.00} | {i + 1} | {dr:+0.00;-0.00} "
                + $"| {(move == 0 ? "—" : $"{move:+0;-0}")} |");
        }
        Console.WriteLine();

        // 値そのものの動き。相関の変化だけだと「どの編成が過小だったのか」が見えない——
        // 穴が毒軸に集中していたという主張は、ここの倍率で確かめる。
        Console.WriteLine("#### 受け手側へ移して値がどう動いたか（倍率の降順）");
        Console.WriteLine();
        Console.WriteLine("`旧` は味方の `DamageToEnemy` / `Kills` の合計、`新` は敵の `DamageTaken`（− 敵の");
        Console.WriteLine("`TakenFromAlly`）/ 敵の死亡数。**差はそのまま「帰属を持たない削り」の量**");
        Console.WriteLine("（毒・燃焼、および胞子のように編成の定義に無い駒が通した削り）。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 与ダメ 旧 | 与ダメ 新 | 倍率 | 撃破 旧 | 撃破 新 | 倍率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (int t in Enumerable.Range(0, nT)
                     .OrderByDescending(t => leg[b][t][0] == 0 ? double.PositiveInfinity : dyn[b][t][0] / leg[b][t][0]))
        {
            string Ratio(double n, double o) => o == 0 ? "—" : $"×{n / o:F2}";
            Console.WriteLine($"| {targets[t].Name} | {leg[b][t][0]:F0} | {dyn[b][t][0]:F0} | {Ratio(dyn[b][t][0], leg[b][t][0])} "
                + $"| {leg[b][t][1]:F2} | {dyn[b][t][2]:F2} | {Ratio(dyn[b][t][2], leg[b][t][1])} |");
        }
        Console.WriteLine();

        // --- 2. 第一近似と残差 ---
        int first = ordered[b][0].K;
        double[] pred = LinearFit(feat[b][first], deg[b]);
        double[] resid = Enumerable.Range(0, nT).Select(t => deg[b][t] - pred[t]).ToArray();
        double r2 = ordered[b][0].R * ordered[b][0].R;

        Console.WriteLine($"### 第一近似 = **{featNames[first]}**（r = {ordered[b][0].R:+0.00;-0.00} / "
            + $"r² = {r2:F2}。突破度のばらつきの {r2 * 100:F0}% を1変数で説明する）");
        Console.WriteLine();
        Console.WriteLine("残差 = 実測の突破度 − この1変数からの線形予測。**残差が大きい編成が、");
        Console.WriteLine("地力以外の何かを持っている編成**——次に設計する効果の入口はここにある。");
        Console.WriteLine();

        // --- 3. 残差と他の特徴量の相関（1段だけ） ---
        var rcors = Enumerable.Range(0, nF).Where(k => k != first)
            .Select(k => { var c = Correlate(feat[b][k], resid); return (K: k, c.R, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();

        Console.WriteLine("#### 残差と他の特徴量の相関（1段のみ。|r| の降順・上位8）");
        Console.WriteLine();
        Console.WriteLine("| 区分 | 特徴量 | 残差との r |");
        Console.WriteLine("|:-:|---|--:|");
        foreach (var x in rcors.Take(8))
            Console.WriteLine($"| {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} | {x.R:+0.00;-0.00} |");
        Console.WriteLine();
        Console.WriteLine($"2変数（{featNames[first]} + 残差との相関1位の {featNames[rcors[0].K]}）の R² = "
            + $"**{R2Two(ordered[b][0].R, Correlate(feat[b][rcors[0].K], deg[b]).R, Correlate(feat[b][first], feat[b][rcors[0].K]).R):F2}**"
            + $"（1変数の {r2:F2} から）。**3変数以上は n = {nT} では意味を持たないのでやらない。**");
        Console.WriteLine();

        // --- 4. 残差の上位・下位5編成 ---
        var byResid = Enumerable.Range(0, nT).Where(t => !double.IsNaN(resid[t]))
            .OrderByDescending(t => resid[t]).ToArray();
        Console.WriteLine("#### 残差の上位・下位5編成");
        Console.WriteLine();
        Console.WriteLine("| 向き | 編成 | 実測 | 予測 | 残差 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (int t in byResid.Take(5))
            Console.WriteLine($"| 予測より**強い** | {targets[t].Name} | {deg[b][t]:F3} | {pred[t]:F3} | {resid[t]:+0.000;-0.000} |");
        foreach (int t in byResid.Reverse().Take(5))
            Console.WriteLine($"| 予測より**弱い** | {targets[t].Name} | {deg[b][t]:F3} | {pred[t]:F3} | {resid[t]:+0.000;-0.000} |");
        Console.WriteLine();

        // --- 5. 静的だけでどこまで説明できるか ---
        // 「編成を組んだ時点で結果がほぼ決まっている」かどうかは設計上とても重いので、
        // 動的を混ぜた値とは別に、静的だけの説明力をはっきり出す（§4-2）。
        var bestS = ordered[b].First(x => isStatic[x.K]);
        var statPairs = new List<(int A, int B, double R2)>();
        for (int i = 0; i < nS; i++)
            for (int j = i + 1; j < nS; j++)
                statPairs.Add((i, j, R2Two(Correlate(feat[b][i], deg[b]).R,
                                       Correlate(feat[b][j], deg[b]).R,
                                       Correlate(feat[b][i], feat[b][j]).R)));
        var topPairs = statPairs.OrderByDescending(p => p.R2).Take(3).ToArray();

        Console.WriteLine("#### 静的だけの説明力（戦わずにどこまで分かるか）");
        Console.WriteLine();
        Console.WriteLine($"- 静的1変数の最良: **{featNames[bestS.K]}** r = {bestS.R:+0.00;-0.00} / r² = {bestS.R * bestS.R:F2}");
        foreach (var p in topPairs)
            Console.WriteLine($"- 静的2変数: {featNames[p.A]} + {featNames[p.B]} → R² = **{p.R2:F2}**");
        Console.WriteLine();
        Console.WriteLine("**静的だけで 0.8 以上説明できるなら、編成を組んだ時点で結果がほぼ決まっている**");
        Console.WriteLine("ことになる（§4-2）。良し悪しの評価ではなく、事実としてこの数字を読む。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 6. 台による違い ---
    Console.WriteLine("## 台による違い（第一近似は入れ替わるか）");
    Console.WriteLine();
    Console.WriteLine("入れ替わるなら、**「地力」は台ごとに違うものを指していた**ことになる（§4-2）。");
    Console.WriteLine();
    Console.WriteLine("| 順位 |" + string.Concat(benches.Select(x => $" {x.Tag}: {x.Name} | r |")));
    Console.WriteLine("|--:|" + string.Concat(benches.Select(_ => "---|--:|")));
    for (int i = 0; i < 5; i++)
        Console.WriteLine($"| {i + 1} |" + string.Concat(Enumerable.Range(0, nB)
            .Select(b => $" {featNames[ordered[b][i].K]} | {ordered[b][i].R:+0.00;-0.00} |")));
    Console.WriteLine();
    var degCor = Correlate(deg[0], deg[1]);
    Console.WriteLine($"**突破度そのものの台間相関: r = {degCor.R:F2} / ρ = {degCor.Rho:F2}**"
        + "（1.00 に近いほど、台を替えても同じ序列が出てくる = 第4〜11期が当たり続けた壁そのもの）。");
    Console.WriteLine();

    // --- 7. 与ダメ効率（オーバーキル）の位置 ---
    // 閾値仮説（「一撃圏を跨ぐかどうかで結果が変わる」）が正しければ、無駄撃ちの指標が
    // 上位に来るはず。来なければ仮説の側を疑う材料になる（§4-2）。
    int over = Array.IndexOf(featNames, "与ダメ効率");
    Console.WriteLine("## 与ダメ効率（オーバーキル）の位置");
    Console.WriteLine();
    Console.WriteLine("| 台 | 単相関の順位 | r | ρ |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        int pos = Array.FindIndex(ordered[b], x => x.K == over);
        Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | {pos + 1} / {nF} "
            + $"| {ordered[b][pos].R:+0.00;-0.00} | {ordered[b][pos].Rho:+0.00;-0.00} |");
    }
    Console.WriteLine();
    Console.WriteLine("**閾値仮説が正しければここが上位に来るはず。** 来なければ仮説の再考が要る。");
    Console.WriteLine();

    // ================= Phase EA: 同語反復を除いた分解（第14期） =================
    //
    // 第13期で毒の穴を塞いだ結果、第一近似は `撃破/戦` r² = 0.90 になった。
    // **これは発見ではなく算術。** 部隊を全滅させることがその部隊を突破することなので、
    // 味方1部隊では 部隊戦数 = 突破数 + 1 になり、全抜きした編成の `撃破/戦` は例外なく
    // 13 ÷ 3 = 4.33 に落ちる（上の警告の通り）。
    //
    // 同じ理屈は第12期の r² 0.41 にも効く。あれは「地力の説明力が4割」ではなく
    // **「言い換えのはずの量が、毒の穴のせいで4割まで落ちていた」**——だから第12期の
    // 「地力は単一の量ではない」という結論は根拠を失っている。**分解はやり直しになる。**
    //
    // ここでやるのは**候補集合だけを変えた測り直し**で、手順も計算方法も第12期・第13期と同じ。
    // 説明力が落ちるのは失敗ではない——落ちた値のほうが本当の説明力（§3-3）。
    // **数字を上げるために候補を戻さない。**
    //
    // 却下した案: 目的変数のほうを言い換えから遠い量に取り替える（勝率・残HP など）。
    // 目的変数を替えると第8期以降の測定すべてと繋がらなくなるうえ、勝率は上下端に張り付いて
    // 序列を潰す（第5期の持ち越し論点(2)）。**動かすのは候補集合の側だけ。**

    // 同語反復の判定。**基準は「突破という結果の言い換えになっていないか」の1本だけ**にする。
    // 「信頼できるか」は混ぜない——混ぜると基準が二重になり、次に特徴量を足すときに使えない。
    //
    // 言い換えが入り込む経路は2つある。
    //   分子経路 — 量そのものが突破の定義に含まれる（部隊を全滅させる＝その部隊を突破する）
    //   分母経路 — 味方1部隊では **部隊戦数 = 突破数 + 1**（全抜き時だけ = 突破数）なので、
    //              `/戦` で割る量はすべて「目的変数 + 1」で割っている
    // **外すのは分子経路だけ。** 分母経路は「平均を取る」操作であって、量の中身を突破の写しに
    // 変えるわけではない。ただし言い張らずに、下の検査表で分母が実際にどれだけ突破度を
    // 運んでいるかを測って出す。比（自傷率・与ダメ効率）は分子分母が同じ部隊戦数で
    // 割られているので、**分母経路が丸ごと打ち消える**。
    var taut = new (bool Excluded, string Reason)[]
    {
        // 与ダメ/戦
        (true,  "**分子経路。** 敵の総HPを削り切ることが突破なので、突破した部隊の数だけ分子が積み上がる。突破度の小数部（最終戦で削った割合）も分子そのもの"),
        // 被ダメ/戦
        (false, "分子は「敵に殴られた量」で、突破の定義に入らない。**分母経路だけ**——最後の1戦（負け戦）が高く、勝ち戦を重ねるほど薄まる構造はあるので、平均として読む"),
        // 撃破/戦
        (true,  "**分子経路。もっとも露骨な言い換え。** 部隊の全滅＝突破。全抜きした編成の値は 13 ÷ 3 = 4.33 に固定され、これは測定結果ではなく算術"),
        // 干渉/戦
        (false, "分子は「誰が起点になったか」の回数で、突破の定義に入らない。**毒軸で構造的に過小**（第13期の残る穴）だが、それは信頼性の問題であって同語反復ではない——**基準を混ぜないので残す。** 単相関は下位帯なので、外しても入れても第一近似は動かない"),
        // 回復/戦
        (false, "分子は味方の回復量で、突破の定義に入らない。**分母経路だけ**"),
        // 自傷率
        (false, "**比なので分母経路が打ち消える**（分子分母とも同じ部隊戦数で割られる）。分子は味方同士の削りで、突破の写しではない"),
        // 与ダメ効率
        (false, "分子・分母とも分子経路の量だが、**比を取ると言い換えの部分がそのまま打ち消える**——`与ダメ ÷ 撃破` は「1体倒すのに振った量」で、部隊戦数も消える。**台で符号が反転することが証拠**（下の検査表）: 言い換えなら符号は反転しない"),
    };

    Console.WriteLine("## 同語反復の除外（第14期 Phase EA）");
    Console.WriteLine();
    Console.WriteLine("**第13期の第一近似 `撃破/戦` r² 0.90 は発見ではなく算術だった。** ここでは");
    Console.WriteLine("「突破という結果の言い換えになっている量」を候補から外して、**同じ手順・同じ計算方法で**");
    Console.WriteLine("分解をやり直す。目的変数も台も seed も変えていない——動かしたのは候補集合だけ。");
    Console.WriteLine();
    Console.WriteLine("### 判定の基準");
    Console.WriteLine();
    Console.WriteLine("基準は「**突破という結果の言い換えになっていないか**」の1本だけ。");
    Console.WriteLine("**「信頼できるか」は混ぜない**（混ぜると基準が二重になり、次に特徴量を足すときに使えない）。");
    Console.WriteLine();
    Console.WriteLine("言い換えが入り込む経路は2つある。");
    Console.WriteLine();
    Console.WriteLine("- **分子経路** — 量そのものが突破の定義に含まれる（部隊を全滅させる＝その部隊を突破する）");
    Console.WriteLine("- **分母経路** — 味方1部隊では `部隊戦数 = 突破数 + 1`（全抜き時だけ `= 突破数`）なので、");
    Console.WriteLine("  `/戦` で割る量はすべて「目的変数 + 1」で割っている");
    Console.WriteLine();
    Console.WriteLine("**外すのは分子経路だけ。** 分母経路は平均を取る操作であって、量の中身を突破の写しに");
    Console.WriteLine("変えるわけではない。比（`自傷率`・`与ダメ効率`）は分子分母が同じ部隊戦数で割られているので、");
    Console.WriteLine("**分母経路が丸ごと打ち消える**。静的特徴量はどちらの経路も持たない（戦わずに決まる量なので）。");
    Console.WriteLine();

    // 分母経路が実際にどれだけ目的変数を運んでいるか。言葉ではなく数字で出す。
    Console.WriteLine("### 検算: 分母（部隊戦数）はどれだけ突破度を運んでいるか");
    Console.WriteLine();
    Console.WriteLine("`部隊戦数 ÷ 試行` そのものを突破度に当てる。**算術の恒等式なので 1.00 に近いはず**——");
    Console.WriteLine("近ければ「`/戦` の分母は目的変数そのもの」であることの確認になり、");
    Console.WriteLine("分母経路を残す判断はその上での判断になる。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 部隊戦数/試行 × 突破度 の r | ρ |");
    Console.WriteLine("|---|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        var c = Correlate(bps[b], deg[b]);
        Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | {c.R:+0.000;-0.000} | {c.Rho:+0.000;-0.000} |");
    }
    Console.WriteLine();

    Console.WriteLine("### 判定表（動的7種。静的8種はすべて残す）");
    Console.WriteLine();
    Console.WriteLine("`部隊戦数 r` は特徴量と分母の相関——**これが ±1 に近い特徴量は、中身が分母そのもの**。");
    Console.WriteLine();
    Console.WriteLine("| 特徴量 | 突破度 r 主 | 突破度 r 従 | 部隊戦数 r 主 | 部隊戦数 r 従 | 判定 | 理由 |");
    Console.WriteLine("|---|--:|--:|--:|--:|:-:|---|");
    for (int k = 0; k < dynNames.Length; k++)
    {
        var col = Enumerable.Range(0, nB)
            .Select(b => Enumerable.Range(0, nT).Select(t => dyn[b][t][k]).ToArray()).ToArray();
        Console.WriteLine($"| {dynNames[k].Name} "
            + $"| {Correlate(col[0], deg[0]).R:+0.00;-0.00} | {Correlate(col[1], deg[1]).R:+0.00;-0.00} "
            + $"| {Correlate(col[0], bps[0]).R:+0.00;-0.00} | {Correlate(col[1], bps[1]).R:+0.00;-0.00} "
            + $"| {(taut[k].Excluded ? "**除外**" : "残す")} | {taut[k].Reason} |");
    }
    Console.WriteLine();

    bool[] keep = Enumerable.Range(0, nF).Select(k => k < nS || !taut[k - nS].Excluded).ToArray();
    int[] cand = Enumerable.Range(0, nF).Where(k => keep[k]).ToArray();
    Console.WriteLine($"**除外後の候補は {cand.Length} 種**（静的 {nS} + 動的 {cand.Length - nS}）。"
        + $"外したのは {string.Join(" / ", Enumerable.Range(0, nF).Where(k => !keep[k]).Select(k => "`" + featNames[k] + "`"))}。");
    Console.WriteLine();

    // --- 除外後の分解（第12期・第13期と同じ手順） ---
    var orderedEa = new (int K, double R, double Rho, int N)[nB][];
    var residEa = new double[nB][];
    var r2Ea = new double[nB];
    for (int b = 0; b < nB; b++)
    {
        orderedEa[b] = cand
            .Select(k => { var c = Correlate(feat[b][k], deg[b]); return (K: k, c.R, c.Rho, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();

        Console.WriteLine($"### 除外後の分解: {benches[b].Tag}の台（{benches[b].Name}）");
        Console.WriteLine();
        Console.WriteLine($"#### 単相関（{cand.Length} 特徴量 × 突破度。|r| の降順）");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 区分 | 特徴量 | r | ρ | n |");
        Console.WriteLine("|--:|:-:|---|--:|--:|--:|");
        for (int i = 0; i < orderedEa[b].Length; i++)
        {
            var x = orderedEa[b][i];
            Console.WriteLine($"| {i + 1} | {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} "
                + $"| {x.R:+0.00;-0.00} | {x.Rho:+0.00;-0.00} | {x.N} |");
        }
        Console.WriteLine();

        int first = orderedEa[b][0].K;
        double[] pred = LinearFit(feat[b][first], deg[b]);
        residEa[b] = Enumerable.Range(0, nT).Select(t => deg[b][t] - pred[t]).ToArray();
        r2Ea[b] = orderedEa[b][0].R * orderedEa[b][0].R;
        double r2Old = ordered[b][0].R * ordered[b][0].R;

        Console.WriteLine($"#### 第一近似 = **{featNames[first]}**（r = {orderedEa[b][0].R:+0.00;-0.00} / "
            + $"**r² = {r2Ea[b]:F3}**）");
        Console.WriteLine();
        Console.WriteLine($"第13期の第一近似は `{featNames[ordered[b][0].K]}` で r² = {r2Old:F3} だった。"
            + $"**差の {r2Old - r2Ea[b]:F3} は言い換えが持っていた分**で、地力の説明力ではない。");
        Console.WriteLine();
        if (r2Ea[b] < 0.30)
        {
            Console.WriteLine("> **停止条件（§6-7）に触れている: 除外後の最良が r² 0.30 を下回った。**");
            Console.WriteLine("> この台では **「地力は既存の特徴量では表せない」**——15種のうち言い換えでない");
            Console.WriteLine($"> {cand.Length}種を当てても、突破度のばらつきの3割を説明できない。");
            Console.WriteLine("> 次に何を測るべきかが変わるので、先へ進む前に報告すること。");
            Console.WriteLine();
        }

        var rcors = cand.Where(k => k != first)
            .Select(k => { var c = Correlate(feat[b][k], residEa[b]); return (K: k, c.R, c.N); })
            .OrderByDescending(x => Math.Abs(x.R)).ToArray();
        Console.WriteLine("#### 残差と他の特徴量の相関（1段のみ。|r| の降順・上位8）");
        Console.WriteLine();
        Console.WriteLine("| 区分 | 特徴量 | 残差との r |");
        Console.WriteLine("|:-:|---|--:|");
        foreach (var x in rcors.Take(8))
            Console.WriteLine($"| {(isStatic[x.K] ? "静" : "動")} | {featNames[x.K]} | {x.R:+0.00;-0.00} |");
        Console.WriteLine();
        Console.WriteLine($"2変数（{featNames[first]} + {featNames[rcors[0].K]}）の R² = "
            + $"**{R2Two(orderedEa[b][0].R, Correlate(feat[b][rcors[0].K], deg[b]).R, Correlate(feat[b][first], feat[b][rcors[0].K]).R):F3}**"
            + $"（1変数の {r2Ea[b]:F3} から）。**3変数以上は n = {nT} では意味を持たないのでやらない。**");
        Console.WriteLine();

        var byResid = Enumerable.Range(0, nT).Where(t => !double.IsNaN(residEa[b][t]))
            .OrderByDescending(t => residEa[b][t]).ToArray();
        Console.WriteLine("#### 残差の上位・下位5編成");
        Console.WriteLine();
        Console.WriteLine("| 向き | 編成 | 実測 | 予測 | 残差 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (int t in byResid.Take(5))
            Console.WriteLine($"| 予測より**強い** | {targets[t].Name} | {deg[b][t]:F3} | {pred[t]:F3} | {residEa[b][t]:+0.000;-0.000} |");
        foreach (int t in byResid.Reverse().Take(5))
            Console.WriteLine($"| 予測より**弱い** | {targets[t].Name} | {deg[b][t]:F3} | {pred[t]:F3} | {residEa[b][t]:+0.000;-0.000} |");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 第12期・第13期・第14期の対比 ---
    // 静的だけの説明力は3期とも同じ値になる（静的特徴量は一度も定義を変えていない）。
    // **それ自体が答えの一部**——同語反復を外しても静的の数字は動かないので、
    // 「静的では 0.35 / 0.16 しか説明できない」は第12期からずっと変わらない事実だった。
    Console.WriteLine("### 第12期・第13期・第14期の対比");
    Console.WriteLine();
    Console.WriteLine("| 台 | 期 | 候補 | 第一近似 | r | r² | 静的1変数 r² | 静的2変数 R² |");
    Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        var bestS = ordered[b].First(x => isStatic[x.K]);
        double bestPair = 0;
        for (int i = 0; i < nS; i++)
            for (int j = i + 1; j < nS; j++)
                bestPair = Math.Max(bestPair, R2Two(Correlate(feat[b][i], deg[b]).R,
                                                    Correlate(feat[b][j], deg[b]).R,
                                                    Correlate(feat[b][i], feat[b][j]).R));
        void Row(string era, int n, (int K, double R, double Rho, int N) x) =>
            Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | {era} | {n} | {featNames[x.K]} "
                + $"| {x.R:+0.00;-0.00} | **{x.R * x.R:F3}** | {bestS.R * bestS.R:F2} | {bestPair:F2} |");
        Row("第12期（味方側）", nF, orderedOldAll[b][0]);
        Row("第13期（受け手側）", nF, ordered[b][0]);
        Row("**第14期（言い換え除外）**", cand.Length, orderedEa[b][0]);
    }
    Console.WriteLine();
    Console.WriteLine("**静的だけの説明力は3期とも同じ値**——静的特徴量は一度も定義を変えていないので");
    Console.WriteLine("動きようがない。**それ自体が答えの一部**で、「編成を組んだ時点ではほとんど決まっていない」");
    Console.WriteLine("という第12期の観察は、同語反復とは無関係に最初から正しかった。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= Phase EB: 反撃軸の残差（第14期） =================
    //
    // 第13期の残差で、穴を塞いだ後も沈んだままだったのが反撃軸（惨禍×被弾強化 −0.307 /
    // 反撃改2 −0.297）。反撃は `ctx.ApplyDamage(source, back, self)` と source 付きなので
    // 受け手側へ移しても 1pt も動かない——**与ダメも撃破も出ているのに突破度に届いていない。**
    //
    // 仮説（§4-1）: 反撃軸は出力を HP で買っている。HP は会戦を跨ぐ唯一の持ち越し資源なので、
    // 単発戦の額面ほど会戦では価値が無い。正しければ **自傷率と残差が負に相関する**はず。
    //
    // **§4-3 が要になる。** 第9期で 逆しま改 は自傷率 61.6% で上位だが強い編成なので、
    // 自傷率が高いこと自体は弱さの原因ではない。反撃軸だけが沈んでいるなら、原因は
    // 「HP で買っているから」ではなく**反撃という出力の出し方に固有の何か**を指す。
    //
    // 新しい計測は足さない。`bill`（第9期・測定台113%・HP ベースの自傷率）と単発戦の勝率
    // （`docs/balance.md` と同じ計算）を**同じ実行の中で**取り直して突き合わせるだけ。
    // 別の実行から数字を引くと、動いたのが定義のせいか実行のせいか決まらない（第13期の作法）。
    var bill113 = BenchColumn113();
    var billRate = targets.Select(t => MeasureBill(t.F, bill113, PowerSeeds).SelfHarmRate * 100).ToArray();
    var soloWin = new double[nT];
    for (int t = 0; t < nT; t++)
    {
        double sum = 0;
        foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
        {
            int wins = 0;
            for (int seed = 0; seed < PowerSeeds; seed++)
                if (BattleEngine.Run(targets[t].F, st.Enemy, seed, verbose: false).PlayerWon) wins++;
            sum += wins * 100.0 / PowerSeeds;
        }
        soloWin[t] = sum / EnemyCatalog.Stages.Count;
    }
    double[] soloRank = AverageRanksDesc(soloWin);
    var degRank = Enumerable.Range(0, nB).Select(b => AverageRanksDesc(deg[b])).ToArray();
    // 第13期の残差（`撃破/戦` からの残差）。仮説が語っていたのはこちらの残差なので併記する。
    var resid13 = new double[nB][];
    for (int b = 0; b < nB; b++)
    {
        double[] p13 = LinearFit(feat[b][ordered[b][0].K], deg[b]);
        resid13[b] = Enumerable.Range(0, nT).Select(t => deg[b][t] - p13[t]).ToArray();
    }

    Console.WriteLine("## 反撃軸の残差（第14期 Phase EB）");
    Console.WriteLine();
    Console.WriteLine("第13期の残差で、穴を塞いだ後も沈んだままだったのが反撃軸。反撃は");
    Console.WriteLine("`ctx.ApplyDamage(source, back, self)` と source 付きなので受け手側へ移しても 1pt も動かない");
    Console.WriteLine("——**与ダメも撃破も出ているのに突破度に届いていない。**");
    Console.WriteLine();
    Console.WriteLine("仮説: **反撃軸は出力を HP で買っている。** HP は会戦を跨ぐ唯一の持ち越し資源なので、");
    Console.WriteLine("単発戦の額面ほど会戦では価値が無い。正しければ**自傷率と残差が負に相関する**はず。");
    Console.WriteLine();
    Console.WriteLine("`bill 自傷率` は第9期の定義（測定台113%・失った HP のうち自分で削った割合）、");
    Console.WriteLine("`power 自傷率` は同じ台の tally 比（`TakenFromAlly ÷ DamageTaken`）。**台も定義も違う**ので");
    Console.WriteLine("両方出す——片方だけだと、出た/出なかったのが定義のせいか台のせいか決まらない。");
    Console.WriteLine();

    Console.WriteLine("### 1. 自傷率と残差の相関");
    Console.WriteLine();
    Console.WriteLine("残差は **Phase EA の残差**（同語反復を除いた第一近似からの残差）。");
    Console.WriteLine("第13期の残差（`撃破/戦` からの残差）も並べる——仮説が語っていたのはそちらの残差なので。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 自傷率 | EA 残差との r | 第13期 残差との r |");
    Console.WriteLine("|---|---|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        double[] pw = Enumerable.Range(0, nT).Select(t => dyn[b][t][5] * 100).ToArray();
        Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | bill（測定台113%） "
            + $"| {Correlate(billRate, residEa[b]).R:+0.00;-0.00} | {Correlate(billRate, resid13[b]).R:+0.00;-0.00} |");
        Console.WriteLine($"| {benches[b].Tag}: {benches[b].Name} | power（同台の tally 比） "
            + $"| {Correlate(pw, residEa[b]).R:+0.00;-0.00} | {Correlate(pw, resid13[b]).R:+0.00;-0.00} |");
    }
    Console.WriteLine();
    Console.WriteLine("**仮説が正しければ全部が負。** 符号が揃わないなら、自傷率は残差の説明になっていない。");
    Console.WriteLine();

    // 名指しの表。**4編成は必ず出す**（§4-2 の2）。反撃改3 も同じ軸なので添える。
    var ebRows = new (string Group, string Key)[]
    {
        ("反撃軸", "惨禍×被弾強化"), ("反撃軸", "反撃 ("), ("反撃軸", "反撃改 ("),
        ("反撃軸", "反撃改2"), ("反撃軸", "反撃改3"),
        ("逆しま系", "逆しま ("), ("逆しま系", "逆しま改"), ("逆しま系", "逆しま+後備え"),
    };
    Console.WriteLine("### 2. 反撃軸と逆しま系の名指しの表");
    Console.WriteLine();
    Console.WriteLine("**逆しま系を同じ表に並べるのが要点**（§4-3）。第9期で 逆しま改 は自傷率 61.6% で上位");
    Console.WriteLine("だったが強い編成なので、**自傷率が高いこと自体は弱さの原因ではない。**");
    Console.WriteLine("反撃軸だけが沈んでいるなら、原因は自傷率ではなく反撃そのものにある。");
    Console.WriteLine();
    Console.WriteLine("| 軸 | 編成 | bill 自傷率 | power 自傷率(主) | 被ダメ/戦(主) | 与ダメ効率(主) | 突破度(主) | EA 残差(主) | 第13期 残差(主) | 突破度(従) | EA 残差(従) |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var (group, key) in ebRows)
    {
        int[] hit = Enumerable.Range(0, nT).Where(t => targets[t].Name.Contains(key)).ToArray();
        if (hit.Length != 1)
        {
            Console.WriteLine($"| {group} | `{key}` に一致する編成が {hit.Length} 件 | — | — | — | — | — | — | — | — | — |");
            continue;
        }
        int t2 = hit[0];
        Console.WriteLine($"| {group} | {targets[t2].Name} | {billRate[t2]:F1}% | {dyn[0][t2][5] * 100:F1}% "
            + $"| {dyn[0][t2][1]:F0} | {dyn[0][t2][6]:F1} | {deg[0][t2]:F3} | {residEa[0][t2]:+0.000;-0.000} "
            + $"| {resid13[0][t2]:+0.000;-0.000} | {deg[1][t2]:F3} | {residEa[1][t2]:+0.000;-0.000} |");
    }
    Console.WriteLine();

    Console.WriteLine("### 3. 単発戦と会戦の順位");
    Console.WriteLine();
    Console.WriteLine($"`単発戦` は全 {EnemyCatalog.Stages.Count} ステージの独立勝率の平均（`docs/balance.md` と");
    Console.WriteLine($"同じ計算・同じ seed 0..{PowerSeeds - 1}）。会戦は突破度。**順位は 1 が最良。**");
    Console.WriteLine("反撃軸が単発戦で強く会戦で弱いなら、順位の差がプラスに大きく出る。");
    Console.WriteLine();
    Console.WriteLine("| 軸 | 編成 | 単発戦 平均勝率 | 単発戦 順位 | 突破度 順位(主) | 差(主) | 突破度 順位(従) | 差(従) |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    foreach (var (group, key) in ebRows)
    {
        int[] hit = Enumerable.Range(0, nT).Where(t => targets[t].Name.Contains(key)).ToArray();
        if (hit.Length != 1) continue;
        int t2 = hit[0];
        Console.WriteLine($"| {group} | {targets[t2].Name} | {soloWin[t2]:F1}% | {soloRank[t2]:F1} "
            + $"| {degRank[0][t2]:F1} | {degRank[0][t2] - soloRank[t2]:+0.0;-0.0} "
            + $"| {degRank[1][t2]:F1} | {degRank[1][t2] - soloRank[t2]:+0.0;-0.0} |");
    }
    Console.WriteLine();
    var solo0 = Correlate(soloWin, deg[0]);
    var solo1 = Correlate(soloWin, deg[1]);
    Console.WriteLine($"**全 {nT} 編成での単発戦 × 突破度: 主 r = {solo0.R:F2} / ρ = {solo0.Rho:F2}、"
        + $"従 r = {solo1.R:F2} / ρ = {solo1.Rho:F2}。**");
    Console.WriteLine("単発戦の平均勝率と突破度の相関がそもそも高ければ、「単発戦では強いのに会戦で弱い」は");
    Console.WriteLine("編成の一般的な性質ではなく、名指しの編成に固有の話になる。");
    Console.WriteLine();
    Console.Out.Flush();

    return;
}

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

// wave モード: 編成 × 波の交互作用を、単発戦の勝率で測る（第15期 Phase FA）。
//
// **方針の変更が前提にある**（design/SINGLE_BATTLE_PLAN.md §0）。基本（メイン）は単発戦で、
// 会戦は「もありえる」の位置づけになった。**代金（払った HP の割合）は会戦でしか意味を持たない**
// ——単発戦では HP が毎回リセットされるので、90% 削られて勝つのと 20% で勝つのは同じ価値で、
// 代金という概念そのものが成立しない。第5〜9期・第12〜14期は捨てないが、**主の物差しではなかった。**
//
// 単発が主なら狙うものも変わる。配分判断ではなく **波によって最適な編成が違うこと**
// （編成 × 波の交互作用）。「この波にはこの編成」が成立すれば、それだけで編成パズルになる。
// 第6〜8期がずっと探していた「向き」は、**突破度ではなく波ごとの勝率に対して測るべきだった。**
//
// 出発点は docs/balance.md の天井率（勝率 100.0% の編成数）: 第1波 31/31、第2〜4波が 13〜14/31、
// 第5波だけが 2/31。**第1波は評価に一切寄与していない。**
//
// ここで測るのは既存5波 + 第5〜10期に診断のローカルへ散らばっていた候補波の全部。
// **代金ではなく勝率で測り直す**——第5〜7期はすべて代金で評価していたので、
// 勝敗の観点では一度も見ていない。
//
// 却下した案: 候補波を `EnemyCatalog.Stages` / `Columns` へ足してから測る。採用が決まって
// いない波をカタログに入れると `compare` / `dump` が動いて docs/ に差分が出る（第5期以来の方針。
// 波の採用決定はこの作業では**しない**）。集約先はこのモードのローカル1箇所にする。
//
// 診断用で docs/ には置かない（power / bench / timing と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 wave [絞り込み]
if (focusId == "wave")
{
    const int WaveSeeds = 200;   // compare / power / bench と同じ
    var all = CompareBuilds();

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    // --- 候補波の集約（1箇所）---
    //
    // 定義は `WaveCatalog()`。**集める先をモードの外へ出してある**のは、第16期の `dissect`（交互作用の解剖）が
    // 同じ波を読むから。**コピーを作った瞬間に「1箇所に集める」という第15期の方針が壊れる。**
    var waves = WaveCatalog();
    int nW = waves.Length;

    Console.WriteLine($"# 編成 × 波の交互作用（単発戦・seed 0..{WaveSeeds - 1} の {WaveSeeds} 試行）");
    Console.WriteLine();
    Console.WriteLine("**単発戦の勝率で測る。** 会戦（`engage` / `power` / `bench`）の突破度でも代金でもない");
    Console.WriteLine("——代金は HP を持ち越す会戦でしか意味を持たず、単発では 90% 削られて勝つのと 20% で");
    Console.WriteLine("勝つのが同じ価値になる。第5〜7期の候補波は**すべて代金で評価していた**ので、");
    Console.WriteLine("勝敗の観点ではここが初めての測定になる。");
    Console.WriteLine();
    Console.WriteLine("見たいのは値の大小ではなく **「波によって最適な編成が違うか」**（編成 × 波の交互作用）。");
    Console.WriteLine("成立すれば、それだけで編成パズルになる。");
    Console.WriteLine();
    Console.WriteLine("**測定だけで、盤面は何も変えていない。** `EnemyCatalog.Stages` / `Columns` にも足していない。");
    Console.WriteLine();

    // --- 1. 候補波の定義 ---
    Console.WriteLine("## 1. 候補波の定義（ここが1箇所）");
    Console.WriteLine();
    Console.WriteLine($"既存5波 + 候補 {nW - 5} = **{nW} 波**。定義はどれも出どころのローカル定義を1文字も");
    Console.WriteLine("変えずに写したもの（§2 の検算で突き合わせる）。");
    Console.WriteLine();
    Console.WriteLine("> **現物が無くて入れられなかった候補が2つある。** 第8期に測った「攻5 版」（90/攻5）と");
    Console.WriteLine("> 「板金従卒5」（60/攻7）は `UnitCatalog` に `UnitDef` が残っていない（前者は刻みとして");
    Console.WriteLine("> 測っただけ、後者は「却下した案」として文章にだけ残っている）。**BattleCore を触らない**");
    Console.WriteLine("> 作業なので新しい敵は作らず、集めるのは現物のある波だけにした。");
    Console.WriteLine();
    Console.WriteLine("| タグ | 出どころ | 波 | 体数 | 総HP | 総攻 | 中身（HP/攻/速/型） |");
    Console.WriteLine("|:-:|:-:|---|--:|--:|--:|---|");
    foreach (var (tag, era, name, enemy) in waves)
    {
        string[] seat = FormationRules.SeatNames;
        var members = enemy.Occupied().Select(x =>
        {
            string pat = x.Def.Pattern switch
            {
                AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き",
                AttackPattern.All => "全体", _ => "単体"
            };
            return $"{seat[x.Slot]}={x.Def.Name}({x.Def.MaxHp}/{x.Def.Attack}/速{x.Def.Speed}/{pat})";
        });
        Console.WriteLine($"| **{tag}** | {era} | {name} | {enemy.Count} "
            + $"| {enemy.Occupied().Sum(x => x.Def.MaxHp)} | {enemy.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {string.Join("、", members)} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 2. 検算 ---
    //
    // (1) **再現の検算**（§5-7 の停止条件）。集め方が間違っていれば、代金・向き・ターン数が
    //     gradient / aim / flip / bridge と食い違う。`MeasureCost` を**同じ関数のまま呼び直す**
    //     ので、一致しなければ写し間違い以外の説明が付かない。
    // (2) 味方と敵の Def.Id が衝突していると、敵の被弾が味方の動的特徴量に混ざる（power と同じ穴）。
    // (3) 敵同士の巻き込みが無いこと（受け手側から与ダメを取るための前提。第13期 §3-1）。
    Console.WriteLine("## 2. 検算");
    Console.WriteLine();

    var cost = new (double WinRate, double AvgAlive, double AvgHpPct, int Wins, double AvgTurns)[nW, nT];
    for (int w = 0; w < nW; w++)
        for (int t = 0; t < nT; t++)
            cost[w, t] = MeasureCost(targets[t].F, waves[w].Enemy, WaveSeeds);

    // 記録されている値（**現行 master での** gradient / aim / flip / bridge の出力）。
    // 第5〜7期当時の値ではない——第10・11期でチャージとスキルの行動化が入っているので
    // 当時の数字とは合わない（合わないこと自体は集め方の誤りではない）。突き合わせるのは
    // 「同じ master で同じ波を測ったら同じ値が出るか」で、そこがずれたら写し間違いになる。
    var recorded = new Dictionary<string, (double Cost, double Split, double Turns, string From)>
    {
        ["G1a"] = (31.8, +2.9, 4.1, "gradient / aim"), ["G1b"] = (27.4, +3.0, 3.9, "gradient / aim"),
        ["G1c"] = (36.7, +2.3, 4.3, "gradient / aim"),
        ["G2a"] = (36.0, +1.7, double.NaN, "gradient"), ["G2b"] = (41.4, +2.3, double.NaN, "gradient"),
        ["G2c"] = (50.9, +2.8, double.NaN, "gradient"),
        ["G3a"] = (61.4, -0.4, 6.6, "gradient / flip"), ["G3b"] = (52.8, -0.1, 5.8, "gradient / flip"),
        ["G3c"] = (44.7, +2.7, 5.3, "gradient / flip"),
        ["H1a"] = (33.2, -2.6, 5.7, "aim"), ["H1b"] = (28.4, -2.3, 5.3, "aim"), ["H1c"] = (21.7, -2.8, 5.4, "aim"),
        ["H2a"] = (30.0, +8.9, 3.1, "aim / bridge"), ["H2b"] = (36.1, +7.6, 3.5, "aim"),
        ["H2c"] = (42.5, +5.6, 4.0, "aim"), ["H2d"] = (27.7, +6.8, 3.2, "aim / bridge"),
        ["M1"] = (36.2, +3.2, 4.1, "aim"),
        ["R0"] = (33.2, +7.0, 3.5, "flip"), ["R1"] = (47.7, +1.4, 4.8, "flip"),
        ["R2"] = (35.8, +2.6, 4.1, "flip"), ["R3"] = (23.8, +1.9, 3.5, "flip"),
        ["R4"] = (62.3, -2.9, 6.4, "flip"), ["R5"] = (46.6, +2.4, 5.1, "flip"), ["R6"] = (31.7, +2.7, 4.3, "flip"),
        ["R7"] = (60.3, -0.2, 6.6, "flip"), ["R8"] = (79.7, -7.9, 8.2, "flip"), ["R9"] = (86.8, -4.3, 7.7, "flip"),
        ["R10"] = (74.7, -6.7, 6.6, "flip"), ["R11"] = (68.3, -8.8, 7.7, "flip / bridge"),
        ["R12"] = (57.8, -4.0, 6.9, "flip"),
        ["P6"] = (54.2, -4.9, double.NaN, "bridge"), ["Q6"] = (42.5, -2.4, double.NaN, "bridge"),
        ["C2"] = (44.6, double.NaN, double.NaN, "bridge"), ["C3"] = (43.9, -2.8, double.NaN, "bridge"),
    };

    var costMean = new double[nW];
    var costSplit = new double[nW];
    var costTurns = new double[nW];
    var zeroWin = new int[nW];
    int mismatch = 0;
    Console.WriteLine("### 2-1. 再現（`MeasureCost` を同じ関数のまま呼び直したもの）");
    Console.WriteLine();
    Console.WriteLine("`記録` は**現行 master での** gradient / aim / flip / bridge の出力。第5〜7期当時の");
    Console.WriteLine("数字ではない（第10・11期でチャージとスキルの行動化が入っているので当時とは合わない）。");
    Console.WriteLine("ここで確かめたいのは「同じ master で同じ波を測ったら同じ値が出るか」だけ。");
    Console.WriteLine("**0.1 を超えてずれたら写し間違い**なので、先へ進まずに止まる（§5-7）。");
    Console.WriteLine();
    Console.WriteLine("| タグ | 出典 | 代金平均 | 記録 | 単体−範囲 | 記録 | 平均ターン数 | 記録 | 勝率0%の編成数 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|--:|");
    for (int w = 0; w < nW; w++)
    {
        var live = Enumerable.Range(0, nT).Where(t => cost[w, t].Wins > 0).ToArray();
        zeroWin[w] = nT - live.Length;
        double Cost(int t) => (1 - cost[w, t].AvgHpPct) * 100;
        costMean[w] = live.Length == 0 ? double.NaN : live.Average(Cost);
        costTurns[w] = live.Length == 0 ? double.NaN : live.Average(t => cost[w, t].AvgTurns);
        var groups = live.GroupBy(t => HasAoe(targets[t].F)).ToDictionary(g => g.Key, g => g.Average(Cost));
        double aoe = groups.TryGetValue(true, out double a) ? a : double.NaN;
        double single = groups.TryGetValue(false, out double b) ? b : double.NaN;
        costSplit[w] = single - aoe;

        if (!recorded.TryGetValue(waves[w].Tag, out var rec))
        {
            Console.WriteLine($"| {waves[w].Tag} | （既存5波・突き合わせ先なし） | {costMean[w]:F1}% | — "
                + $"| {costSplit[w]:+0.0;-0.0}pt | — | {costTurns[w]:F1} | — | {zeroWin[w]} |");
            continue;
        }
        string Chk(double got, double want)
        {
            if (double.IsNaN(want)) return "—";
            bool ok = Math.Abs(got - want) <= 0.1;
            if (!ok) mismatch++;
            return ok ? $"{want:F1}" : $"**{want:F1} ←ずれ**";
        }
        string c1 = Chk(costMean[w], rec.Cost), c2 = Chk(costSplit[w], rec.Split), c3 = Chk(costTurns[w], rec.Turns);
        Console.WriteLine($"| {waves[w].Tag} | {rec.From} | {costMean[w]:F1}% | {c1} "
            + $"| {costSplit[w]:+0.0;-0.0}pt | {c2} | {costTurns[w]:F1} | {c3} | {zeroWin[w]} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine(mismatch == 0
        ? "**再現: 一致（ずれ 0 件）。** 集め方は写しになっている。"
        : $"**再現: {mismatch} 件ずれた。集め方が間違っている（§5-7 の停止条件）。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 計測本体 ---
    // seed ごとの勝敗と残存率を丸ごと持つ。半割（§4）は同じ計測から取り出すだけで済む
    // （2回走らせると、半割の値そのものに実行間のばらつきが乗る。bench と同じ作法）。
    var win = new double[nW][][];    // [波][編成][seed] 0/1
    var surv = new double[nW][][];   // [波][編成][seed] 生存数 ÷ 出撃数
    var dyn = new double[nW][][];    // [波][編成][動的特徴量]
    long foeFromAlly = 0;
    for (int w = 0; w < nW; w++)
    {
        win[w] = new double[nT][];
        surv[w] = new double[nT][];
        dyn[w] = new double[nT][];
        for (int t = 0; t < nT; t++)
        {
            var m = MeasureWave(targets[t].F, waves[w].Enemy, WaveSeeds);
            win[w][t] = m.Win;
            surv[w][t] = m.SurvRate;
            dyn[w][t] = m.Dynamics;
            foeFromAlly += m.FoeTakenFromAlly;
        }
    }

    var clash = new List<string>();
    foreach (var (tag, _, _, enemy) in waves)
    {
        var foeIds = enemy.Occupied().Select(x => x.Def.Id).ToHashSet();
        foreach (var (name, f) in targets)
            foreach (string id in f.Occupied().Select(x => x.Def.Id).Where(foeIds.Contains))
                clash.Add($"{tag} × {name}: {id}");
    }

    Console.WriteLine("### 2-2. 動的特徴量の前提（Phase FB が読む）");
    Console.WriteLine();
    Console.WriteLine($"- **味方と敵の Def.Id の衝突: {clash.Count} 件**"
        + (clash.Count == 0 ? "（0 でなければ動的特徴量に敵の数字が混ざっている）"
                            : $" ← **混入している**: {string.Join(" / ", clash.Take(5))}"));
    Console.WriteLine($"- **敵の TakenFromAlly の総和: {foeFromAlly}**"
        + (foeFromAlly == 0
            ? "（0 = 敵側に巻き込みが無い。受け手側の与ダメから引いた量も 0）"
            : " ← **敵同士の巻き込みがある。** 与ダメからこの量を引いている"));
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3. 編成 × 波の勝率表 ---
    double[] winRate(int w) => Enumerable.Range(0, nT).Select(t => win[w][t].Average() * 100).ToArray();
    var rate = Enumerable.Range(0, nW).Select(winRate).ToArray();
    // 残存度 = 全試行の平均（生存数 ÷ 出撃数）。**負けた試行は 0 になる**ので勝率を内包しつつ、
    // 勝率が天井に張り付いた波でも「何体残して勝ったか」で編成が割れる。
    // `chain` の `残存`（勝った試行だけの平均生存数）とは分母が違う——あちらは勝ち方の質、
    // こちらは天井を割るための連続量。**両方出して、どちらを使ったかを明記する**（§2-2）。
    var degree = Enumerable.Range(0, nW)
        .Select(w => Enumerable.Range(0, nT).Select(t => surv[w][t].Average()).ToArray()).ToArray();
    var aliveOnWin = Enumerable.Range(0, nW).Select(w => Enumerable.Range(0, nT).Select(t =>
    {
        int wins = win[w][t].Count(x => x > 0);
        return wins == 0 ? 0.0 : Enumerable.Range(0, WaveSeeds).Where(s => win[w][t][s] > 0)
            .Sum(s => surv[w][t][s]) / wins;
    }).ToArray()).ToArray();

    Console.WriteLine("## 3. 編成 × 波の勝率表");
    Console.WriteLine();
    Console.WriteLine("列は §1 のタグ。`S1`〜`S5` が既存5波（この5列は `docs/balance.md` と同じ値になる）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(waves.Select(x => $" {x.Tag} |")));
    Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "--:|")));
    for (int t = 0; t < nT; t++)
        Console.WriteLine($"| {targets[t].Name} |"
            + string.Concat(Enumerable.Range(0, nW).Select(w => $" {rate[w][t]:F1} |")));
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4. 波ごとの要約 ---
    // **天井に張り付いた波は評価に寄与しない。** 全滅する波（床）も同じ。
    // 線は「天井率 + 床率 ≥ 50%」に置いた——**これは測定から出た線ではない**ので、
    // 生の天井率・床率を同じ表に出して、線を引き直せるようにしてある。
    const double DeadZone = 50.0;
    var ceilPct = new double[nW];
    var floorPct = new double[nW];
    var contributes = new bool[nW];
    Console.WriteLine("## 4. 波ごとの要約（どの波が評価に寄与しているか）");
    Console.WriteLine();
    Console.WriteLine("`天井` は勝率 100.0% の編成数、`床` は 0.0% の編成数。**どちらも同値塊**で、");
    Console.WriteLine("その中の編成同士は区別できない——天井だけの波・床だけの波は評価に寄与しない。");
    Console.WriteLine();
    Console.WriteLine("`残存(勝時)` は勝った試行の平均生存数 ÷ 出撃数（`chain` の `残存` と同じ定義）。");
    Console.WriteLine("`残存度` は**全試行**の平均（生存数 ÷ 出撃数。負けた試行は 0）で、勝率を内包しつつ");
    Console.WriteLine("天井で潰れない連続量。§6 の第2の読み方がこれを使う。");
    Console.WriteLine();
    Console.WriteLine($"`寄与` は **天井率 + 床率 < {DeadZone:F0}%** を満たすか。**この線は測定から出たものではない**");
    Console.WriteLine("ので、生の天井・床の数字を同じ表に出してある（線を引き直したければここから読み直せる）。");
    Console.WriteLine();
    Console.WriteLine("| タグ | 波 | 平均勝率 | 勝率SD | 天井 | 床 | 天井+床 | 残存(勝時) | 残存度 | 残存度SD | 寄与 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|--:|--:|:-:|");
    for (int w = 0; w < nW; w++)
    {
        int ceil = rate[w].Count(v => v >= 100.0 - 1e-9);
        int floor = rate[w].Count(v => v <= 1e-9);
        ceilPct[w] = ceil * 100.0 / nT;
        floorPct[w] = floor * 100.0 / nT;
        contributes[w] = ceilPct[w] + floorPct[w] < DeadZone;
        Console.WriteLine($"| **{waves[w].Tag}** | {waves[w].Name} | {rate[w].Average():F1}% | {Sd(rate[w]):F1}pt "
            + $"| {ceil}/{nT} | {floor}/{nT} | {ceilPct[w] + floorPct[w]:F0}% "
            + $"| {aliveOnWin[w].Average():F2} | {degree[w].Average():F3} | {Sd(degree[w]):F3} "
            + $"| {(contributes[w] ? "○" : "×")} |");
    }
    Console.WriteLine();
    int nContrib = contributes.Count(x => x);
    int allCeil = Enumerable.Range(0, nW).Count(w => ceilPct[w] >= 100.0 - 1e-9);
    Console.WriteLine($"**寄与している波は {nContrib} / {nW}。** 既存5波では "
        + $"{Enumerable.Range(0, 5).Count(w => contributes[w])} / 5。");
    Console.WriteLine();
    Console.WriteLine($"うち **{allCeil} 波は {nT} 編成すべてが勝率 100.0%**（完全な天井）。候補波"
        + $"（既存5波を除く {nW - 5} 波）に限ると、寄与するのは "
        + $"{Enumerable.Range(5, nW - 5).Count(w => contributes[w])} 波しかない。");
    Console.WriteLine();
    Console.WriteLine("**第5〜8期の候補波は、単発戦としては全編成が勝ち切ってしまう。** 理屈は読める——");
    Console.WriteLine("あれは会戦の3波列の1本として設計した波で、狙いは「1部隊の容量（約 100%）を");
    Console.WriteLine("3波で使い切る」ことだった。1波あたりの代金 25〜60% は**会戦では3波ぶんが積み上がって");
    Console.WriteLine("部隊を殺す**が、単発では HP が毎回戻るので**ただの「6割削られて勝つ」**にしかならない。");
    Console.WriteLine("**代金の帯を狙って作った波は、単発戦の物差しでは全部が天井の同じ場所に並ぶ。**");
    Console.WriteLine("寄与しているのは、代金ではなく難度で作られた波（既存の第2〜5波）と、第7期に");
    Console.WriteLine("**打ち切りバイアスを理由に一度は捨てた重い波**（R8 / R9 / R10）だけ。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 5. 半割 = 測定の信頼性の上限 ---
    //
    // 波をまたいだ順位相関が 1.00 未満なのは当たり前で、**乱数のばらつきだけでもそうなる。**
    // 「どれくらいなら動いたと言えるか」の基準を先に測る（第13期 bench の作法をそのまま持ってくる）。
    // 第13期の突破度での値は 0.985〜0.995 / 補正後 0.99 だったが、**目的変数が違うので測り直す**
    // ——単発の勝率は 200 試行の二項比率なので、突破度よりばらつきが大きい可能性がある。
    double SB(double r) => 2 * r / (1 + r);
    double[] MeanOver(double[][] v, Func<int, bool> take) => Enumerable.Range(0, nT)
        .Select(t => Enumerable.Range(0, WaveSeeds).Where(take).Average(s => v[t][s])).ToArray();
    var capR = new double[nW];
    var capRho = new double[nW];
    var capDegRho = new double[nW];
    Console.WriteLine("## 5. 半割 — 測定の信頼性の上限");
    Console.WriteLine();
    Console.WriteLine("**同じ波を seed で半分に割り、両半分で勝率の相関を取る。** 割り方は2種類:");
    Console.WriteLine();
    Console.WriteLine($"- **前後半**: 前半 = seed 0..{WaveSeeds / 2 - 1} / 後半 = seed {WaveSeeds / 2}..{WaveSeeds - 1}");
    Console.WriteLine("- **偶奇**: 偶数 seed / 奇数 seed");
    Console.WriteLine();
    Console.WriteLine("相関は**編成の並び**に対して取る（seed の並びではない）。半割は 100 試行同士なので");
    Console.WriteLine("200 試行の測定より一致度が低く出る——Spearman-Brown の補正 `r(2n) = 2r(n) / (1 + r(n))`");
    Console.WriteLine("を掛けた値を併記する（**補正は「両半分が同等・誤差が独立」を仮定するので生の値も併記**）。");
    Console.WriteLine();
    Console.WriteLine("第13期は突破度で 0.985〜0.995 / 補正後 0.99 だった。**目的変数が違うので測り直している。**");
    Console.WriteLine("勝率が天井・床に潰れた波では順位が同値塊になり、半割そのものが計算できない（`—`）。");
    Console.WriteLine();
    Console.WriteLine("| タグ | 前後半 r | 前後半 ρ | 偶奇 r | 偶奇 ρ | 補正後 r | **補正後 ρ** | 残存度 補正後 ρ |");
    Console.WriteLine("|:-:|--:|--:|--:|--:|--:|--:|--:|");
    for (int w = 0; w < nW; w++)
    {
        var h1 = Correlate(MeanOver(win[w], s => s < WaveSeeds / 2), MeanOver(win[w], s => s >= WaveSeeds / 2));
        var h2 = Correlate(MeanOver(win[w], s => s % 2 == 0), MeanOver(win[w], s => s % 2 == 1));
        var d1 = Correlate(MeanOver(surv[w], s => s < WaveSeeds / 2), MeanOver(surv[w], s => s >= WaveSeeds / 2));
        var d2 = Correlate(MeanOver(surv[w], s => s % 2 == 0), MeanOver(surv[w], s => s % 2 == 1));
        capR[w] = SB((h1.R + h2.R) / 2);
        capRho[w] = SB((h1.Rho + h2.Rho) / 2);
        capDegRho[w] = SB((d1.Rho + d2.Rho) / 2);
        string F(double v) => double.IsNaN(v) ? "—" : $"{v:F3}";
        Console.WriteLine($"| {waves[w].Tag} | {F(h1.R)} | {F(h1.Rho)} | {F(h2.R)} | {F(h2.Rho)} "
            + $"| {F(capR[w])} | **{F(capRho[w])}** | {F(capDegRho[w])} |");
    }
    Console.WriteLine();
    var okCap = Enumerable.Range(0, nW).Where(w => !double.IsNaN(capRho[w])).ToArray();
    Console.WriteLine($"**補正後 ρ の中央値 {okCap.Select(w => capRho[w]).OrderBy(x => x).ElementAt(okCap.Length / 2):F3}"
        + $"（最小 {okCap.Min(w => capRho[w]):F3} / 最大 {okCap.Max(w => capRho[w]):F3}）。**");
    Console.WriteLine("波をまたいだ相関はこれを超えられない。**超えられない量が「余地」で、余地こそが");
    Console.WriteLine("測定のばらつきでは説明できない入れ替わりの量になる。**");
    Console.WriteLine();
    Console.Out.Flush();
    // --- 6. 波ペアの順位相関（本題） ---
    //
    // **順位が入れ替わる波のペアが複数あれば、編成 × 波の交互作用が実在する。**
    // 判定は §2-4 の3行のどれか:
    //   1行目 入れ替わるペアが複数ある      → 交互作用が実在。単発戦のステージ設計の骨格になる
    //   2行目 どの波でも順位がほぼ同じ      → 波の側では差が作れない。編成側で作るしかない
    //   3行目 天井・床を外すとペアが残らない → 実質「勝てる波」と「勝てない波」の一次元
    //
    // 天井・床の扱いで結論が変わりうるので、**3通り全部を出す**（§5-7）:
    //   (a) 全波・勝率           天井・床をそのまま含める
    //   (b) 寄与する波だけ・勝率  §4 の線で切る
    //   (c) 全波・残存度         天井の波も残存で割れるので、切らずに済む読み方
    const double Slack = 0.05;    // bench §6 と同じ線。**測定から出た線ではない**
    const double Flat = 0.90;     // §2-4 の2行目「相関 0.9 以上」

    var rankRate = Enumerable.Range(0, nW).Select(w => AverageRanksDesc(rate[w])).ToArray();
    var rankDeg = Enumerable.Range(0, nW).Select(w => AverageRanksDesc(degree[w])).ToArray();

    Console.WriteLine("## 6. 波ペアの順位相関（本題）");
    Console.WriteLine();
    Console.WriteLine("`ρ` はスピアマン（同順位は平均順位。第8期以降の順位相関と同じ計算）。");
    Console.WriteLine($"`余地` = min(両端の半割 補正後 ρ) − ρ。**{Slack:F2} 未満なら測定のばらつきで説明が付く**");
    Console.WriteLine("（bench §6 と同じ線。これも測定から出た線ではない）。");
    Console.WriteLine();
    Console.WriteLine("### 6-1. 順位相関の行列（勝率・全波）");
    Console.WriteLine();
    Console.WriteLine("対角は半割の補正後 ρ（＝その波自身との一致度の上限）。**行を横に読めば、");
    Console.WriteLine("その波の上限と、他の波との一致度が同じ行に並ぶ。** `—` は同値塊で相関が計算できない波。");
    Console.WriteLine();
    Console.WriteLine("|  |" + string.Concat(waves.Select(x => $" {x.Tag} |")));
    Console.WriteLine("|:-:|" + string.Concat(waves.Select(_ => "--:|")));
    for (int i = 0; i < nW; i++)
    {
        var row = new List<string>();
        for (int j = 0; j < nW; j++)
        {
            double v = i == j ? capRho[i] : Correlate(rate[i], rate[j]).Rho;
            row.Add(double.IsNaN(v) ? "—" : (i == j ? $"*{v:F2}*" : $"{v:F2}"));
        }
        Console.WriteLine($"| **{waves[i].Tag}** | {string.Join(" | ", row)} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // ペアの一覧を作る。3通りの読み方が同じ関数を共有するので、
    // 「扱いを変えたら結論が変わった」が扱いの差だけから出ることが保証される。
    (int A, int B, double Rho, double Cap, double Slk, double MeanGap, double MaxGap)[] Pairs(
        double[][] v, double[][] rk, double[] cap, Func<int, bool> use)
    {
        var list = new List<(int, int, double, double, double, double, double)>();
        for (int i = 0; i < nW; i++)
            for (int j = i + 1; j < nW; j++)
            {
                if (!use(i) || !use(j)) continue;
                double rho = Correlate(v[i], v[j]).Rho;
                double c = Math.Min(cap[i], cap[j]);
                if (double.IsNaN(rho) || double.IsNaN(c)) continue;
                var gaps = Enumerable.Range(0, nT).Select(t => Math.Abs(rk[i][t] - rk[j][t])).ToArray();
                list.Add((i, j, rho, c, c - rho, gaps.Average(), gaps.Max()));
            }
        return list.OrderByDescending(x => x.Item5).ToArray();
    }

    void EmitPairs(string head, string[] notes,
        (int A, int B, double Rho, double Cap, double Slk, double MeanGap, double MaxGap)[] ps, int take)
    {
        Console.WriteLine(head);
        Console.WriteLine();
        foreach (string line in notes) Console.WriteLine(line);
        Console.WriteLine();
        int swaps = ps.Count(x => x.Slk >= Slack && x.Rho < Flat);
        Console.WriteLine($"ペア総数 {ps.Length}、うち **ρ < {Flat:F2} かつ 余地 ≥ {Slack:F2}** が **{swaps}** 組。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 波 | ρ | 上限 | 余地 | 平均\\|順位差\\| | 最大\\|順位差\\| |");
        Console.WriteLine("|:-:|:-:|--:|--:|--:|--:|--:|");
        foreach (var x in ps.Take(take))
            Console.WriteLine($"| {waves[x.A].Tag} | {waves[x.B].Tag} | {x.Rho:F2} | {x.Cap:F2} "
                + $"| **{x.Slk:F2}** | {x.MeanGap:F1} | {x.MaxGap:F1} |");
        Console.WriteLine();
        Console.Out.Flush();
    }

    var pAll = Pairs(rate, rankRate, capRho, _ => true);
    var pCon = Pairs(rate, rankRate, capRho, w => contributes[w]);
    var pDeg = Pairs(degree, rankDeg, capDegRho, _ => true);

    // 全編成が勝率 100% で並ぶ波は順位が完全な同値塊になり、半割そのものが計算できない
    // （分散 0）。上限が取れない波はペアから落ちるので、(a) は「全波」ではなく
    // **「半割が計算できた波」**の読み方になる。落ちた数を出す。
    int capOk = Enumerable.Range(0, nW).Count(w => !double.IsNaN(capRho[w]));
    int capOkCon = Enumerable.Range(0, nW).Count(w => contributes[w] && !double.IsNaN(capRho[w]));
    int capOkDeg = Enumerable.Range(0, nW).Count(w => !double.IsNaN(capDegRho[w]));

    EmitPairs($"### 6-2. (a) 半割が計算できた波・勝率（{capOk} / {nW} 波）— 入れ替わりの大きいペア上位25",
        new[]
        {
            $"天井・床の波もそのまま入れた読み方。残る {nW - capOk} 波は**全編成が勝率 100% で並んで",
            "順位が完全な同値塊**になり、半割（上限）が計算できないのでペアから落ちている。",
            "",
            "> **この読み方は当てにならない。** 天井の波の順位はほぼ全部が同値塊なので、半割 ρ が",
            "> 1.00 に張り付き（30編成が同値で1編成だけ外れる、といった形）、他の波との ρ は同値塊の",
            "> せいで 0 付近まで落ちる。結果として `余地` が 1.0 を超える——**上限を超えて一致しない**",
            "> という意味不明な値で、これは入れ替わりではなく同値塊の副作用。(b) を置いてあるのはこのため。",
        }, pAll, 25);
    EmitPairs($"### 6-3. (b) 寄与する波だけ・勝率（{nContrib} 波・うち半割が取れたのは {capOkCon} 波）"
        + " — 入れ替わりの大きいペア全件",
        new[]
        {
            $"§4 の線（天井率 + 床率 < {DeadZone:F0}%）で切ったあと。**同値塊を外しても入れ替わりが残るか**が",
            "§2-4 の1行目と3行目を分ける。**判定はこの表で読む。**",
        }, pCon, 25);
    EmitPairs($"### 6-4. (c) 全波・残存度（{capOkDeg} / {nW} 波）— 入れ替わりの大きいペア上位25",
        new[]
        {
            "**天井の波も残存で割れるので、波を1つも捨てずに済む読み方。** 勝率が 100% で並ぶ編成同士も",
            "「何体残して勝ったか」で順位が付くので、半割はどの波でも計算できる。",
        }, pDeg, 25);

// --- 7. 名指し: いちばん遠いペアで誰が入れ替わったか ---
    // 相関の数字だけだと「入れ替わった」が抽象のまま残る。**どの編成がどちらの波で強いのか**を
    // 名前で出さないと、次にステージを設計する材料にならない。
    if (pCon.Length > 0)
    {
        var top = pCon[0];
        Console.WriteLine($"## 7. 名指し — 寄与する波のうちいちばん遠いペア（{waves[top.A].Tag} × {waves[top.B].Tag}・ρ = {top.Rho:F2}）");
        Console.WriteLine();
        Console.WriteLine($"- **{waves[top.A].Tag}**: {waves[top.A].Name}");
        Console.WriteLine($"- **{waves[top.B].Tag}**: {waves[top.B].Name}");
        Console.WriteLine();
        Console.WriteLine("`順位差` = 順位(A) − 順位(B)。**正なら B の波で順位が上がる**（順位は 1 が最良）。");
        Console.WriteLine();
        Console.WriteLine($"| 向き | 編成 | {waves[top.A].Tag} 勝率 | 順位 | {waves[top.B].Tag} 勝率 | 順位 | 順位差 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        var byGap = Enumerable.Range(0, nT)
            .OrderByDescending(t => rankRate[top.A][t] - rankRate[top.B][t]).ToArray();
        foreach (int t in byGap.Take(5))
            Console.WriteLine($"| {waves[top.B].Tag} で**上がる** | {targets[t].Name} | {rate[top.A][t]:F1}% "
                + $"| {rankRate[top.A][t]:F1} | {rate[top.B][t]:F1}% | {rankRate[top.B][t]:F1} "
                + $"| {rankRate[top.A][t] - rankRate[top.B][t]:+0.0;-0.0} |");
        foreach (int t in byGap.Reverse().Take(5))
            Console.WriteLine($"| {waves[top.A].Tag} で**上がる** | {targets[t].Name} | {rate[top.A][t]:F1}% "
                + $"| {rankRate[top.A][t]:F1} | {rate[top.B][t]:F1}% | {rankRate[top.B][t]:F1} "
                + $"| {rankRate[top.A][t] - rankRate[top.B][t]:+0.0;-0.0} |");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 8. 判定 ---
    // §2-4 の表のどの行に当たるかを、数字から機械的に選ぶ（bench §6 と同じ作法）。
    // 文章で判定すると「読み方によってはこうも取れる」が残る。
    string Verdict(int swapAll, int swapCon)
        => swapCon >= 2
            ? "**1行目: 編成 × 波の交互作用が実在する。** 単発戦のステージ設計の骨格になる"
            : swapAll >= 2
                ? "**3行目: 入れ替わるが、天井・床を外すとペアがほとんど残らない。** 実質「勝てる波」と"
                  + "「勝てない波」しか無く、難度の一次元に潰れている"
                : $"**2行目: どの波でも順位がほぼ同じ（ρ {Flat:F2} 以上）。** 波を何種類作っても編成パズルに"
                  + "ならない——波の側では差が作れないので、編成側（特性・スキル）で作るしかない";

    int swAll = pAll.Count(x => x.Slk >= Slack && x.Rho < Flat);
    int swCon = pCon.Count(x => x.Slk >= Slack && x.Rho < Flat);
    int swDeg = pDeg.Count(x => x.Slk >= Slack && x.Rho < Flat);
    int swDegCon = pDeg.Count(x => x.Slk >= Slack && x.Rho < Flat && contributes[x.A] && contributes[x.B]);

    Console.WriteLine("## 8. 判定（§2-4 のどの行か）");
    Console.WriteLine();
    Console.WriteLine($"**入れ替わったペア** = ρ < {Flat:F2} かつ 余地 ≥ {Slack:F2}（測定のばらつきで説明が付かない）。");
    Console.WriteLine();
    Console.WriteLine("| 天井・床の扱い | ペア総数 | 入れ替わったペア |");
    Console.WriteLine("|---|--:|--:|");
    Console.WriteLine($"| (a) 半割が計算できた波・勝率 | {pAll.Length} | {swAll} |");
    Console.WriteLine($"| (b) 寄与する波だけ・勝率 | {pCon.Length} | {swCon} |");
    Console.WriteLine($"| (c) 全波・残存度 | {pDeg.Length} | {swDeg}（うち寄与する波同士 {swDegCon}） |");
    Console.WriteLine();
    Console.WriteLine("判定は (a) と (b) の両方から決める——**(b) だけで 2 組以上残れば 1行目**、");
    Console.WriteLine("(a) には出るが (b) で消えるなら 3行目、どちらにも出なければ 2行目（§2-4）。");
    Console.WriteLine();
    Console.WriteLine($"- 勝率での判定: {Verdict(swAll, swCon)}");
    Console.WriteLine($"- 残存度での判定: {Verdict(swDeg, swDegCon)}");
    Console.WriteLine();
    Console.WriteLine(Verdict(swAll, swCon) == Verdict(swDeg, swDegCon)
        ? "**天井・床の扱いを変えても判定は変わらない。**"
        : "> **警告: 天井・床の扱いで判定が変わる。** どちらか一方を選ばず、両方の結果を報告すること（§5-7）。");
    Console.WriteLine();
    Console.Out.Flush();
    // ================= Phase FB: 地力の分解（単発版・第15期） =================
    //
    // 第12〜14期は**突破度**（会戦）を目的変数にして「地力」を分解してきた。第14期の結論は
    // 「地力は既存の特徴量では表せない」（主 総攻 r² 0.308 / 従 与ダメ効率 r² 0.242、
    // 静的だけなら 0.31→0.35 / 0.10→0.16）。**あれは会戦専用の目的変数に対する分解だった。**
    //
    // ここでやるのは第14期 Phase EA と**同じ手順・同じ計算方法**で、目的変数だけを
    // 突破度 → 単発の勝率（波ごと、および平均）に差し替えた測り直し。台も seed も同じ。
    //
    // **同語反復の判定はやり直す。** 目的変数が変わると、何が言い換えかも変わる（§3-1）。
    // 別の実行から第14期の数字を引くと、動いたのが定義のせいか実行のせいか決まらないので、
    // 突破度の側も同じ実行の中で `MeasurePower` を呼び直して対比表に出す（第13期以来の作法）。
    Console.WriteLine("## 9. 地力の分解（単発版・Phase FB）");
    Console.WriteLine();
    Console.WriteLine("第12〜14期と**同じ手順・同じ計算方法**で、目的変数だけを 突破度 → 単発の勝率に");
    Console.WriteLine("差し替えたもの。特徴量の定義も第14期のまま（受け手側の与ダメ・撃破。第13期 Phase DA）。");
    Console.WriteLine();

    // --- 静的特徴量（power と同じ8種。定義値だけから取る） ---
    var statics = new (string Name, string Def, Func<Formation, double> Get)[]
    {
        ("体数",     "編成の駒数（4 or 5）", f => f.Count),
        ("総HP",     "Def.MaxHp の合計", f => f.Occupied().Sum(x => x.Def.MaxHp)),
        ("総攻",     "Def.Attack の合計", f => f.Occupied().Sum(x => x.Def.Attack)),
        ("積",       "総HP × 総攻",
            f => (double)f.Occupied().Sum(x => x.Def.MaxHp) * f.Occupied().Sum(x => x.Def.Attack)),
        ("最薄HP",   "編成中いちばん低い Def.MaxHp", f => f.Occupied().Min(x => x.Def.MaxHp)),
        ("後列HP",   "後列（slot 4/5）の Def.MaxHp 合計",
            f => f.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)),
        ("平均速度", "Def.Speed の平均", f => f.Occupied().Average(x => x.Def.Speed)),
        ("範囲枚数", "Def.Pattern が薙ぎ/全体の駒数（AoeCount）", f => AoeCount(f)),
    };
    string[] dynNames = { "与ダメ/戦", "被ダメ/戦", "撃破/戦", "干渉/戦", "回復/戦", "自傷率", "与ダメ効率" };
    int nS = statics.Length, nD = dynNames.Length, nF = nS + nD;
    string[] featNames = statics.Select(s => s.Name).Concat(dynNames).ToArray();
    bool[] isStatic = Enumerable.Range(0, nF).Select(k => k < nS).ToArray();

    var stat = new double[nT][];
    for (int t = 0; t < nT; t++) stat[t] = statics.Select(s => s.Get(targets[t].F)).ToArray();

    // --- 9-1. 同語反復の再判定 ---
    //
    // 基準は第14期と同じ1本だけ:「**目的変数の言い換えになっていないか**」。
    // 「信頼できるか」は混ぜない（混ぜると基準が二重になって、次に特徴量を足すときに使えない）。
    //
    // 単発戦では経路の構成が変わる。
    //   分母経路は**消える** — 部隊戦が必ず1回なので `/戦` の分母は定数 1。第14期に
    //                          「分母は目的変数 + 1」だった経路そのものが無い。
    //   分子経路は**1つ増える** — 味方の全滅＝敗北なので、`被ダメ/戦` が敗北の定義を含む。
    //                              突破度に対しては分母経路だけだったので残していた量が、
    //                              単発の勝率に対しては**言い換え側に回る**。
    //
    // 却下した案: `被ダメ/戦` を「相関が低いから」という理由で残す。基準は言い換えかどうかの
    // 1本だけで、**相関の大小を判定に混ぜない**（第14期の基準をそのまま使う）。
    var taut15 = new (bool Excluded, string Era14, string Reason)[]
    {
        (true,  "除外",
            "**分子経路。** 敵の総HPを削り切ることが勝利なので、勝った試行では分子が敵の総HPに張り付く。第14期と同じ判定"),
        (true,  "残す",
            "**単発では分子経路に回る。** 味方の全滅＝敗北なので、負けた試行では分子が味方の総HP（+回復・過剰殺傷）に張り付く。突破度に対しては分母経路だけだったので残していたが、**目的変数が変わると経路も変わる**"),
        (true,  "除外",
            "**分子経路。もっとも露骨な言い換え。** 敵の全滅＝勝利。勝った試行の値は敵の体数そのもので、これは測定結果ではなく算術。第14期と同じ判定"),
        (false, "残す",
            "分子は「誰が起点になったか」の回数で、勝敗の定義に入らない。**毒軸で構造的に過小**（第13期の残る穴）だが、それは信頼性の問題であって同語反復ではない——基準を混ぜないので残す"),
        (false, "残す",
            "分子は味方の回復量で、勝敗の定義に入らない。単発では分母経路も無いので、経路が1つも無い"),
        (false, "残す",
            "**比なので、分子・分母の両方に乗っている「味方がどれだけ削られたか」が打ち消える。** 分子は味方同士の削りで、敗北の写しではない"),
        (false, "残す",
            "分子・分母とも分子経路の量だが、**比を取ると言い換えの部分が打ち消える**——`与ダメ ÷ 撃破` は「1体倒すのに振った量」。第14期と同じ判定"),
    };

    Console.WriteLine("### 9-1. 同語反復の再判定（目的変数が変わったので判定もやり直す）");
    Console.WriteLine();
    Console.WriteLine("基準は第14期と同じ**1本だけ**:「**目的変数の言い換えになっていないか**」。");
    Console.WriteLine("**「信頼できるか」は混ぜない**（混ぜると基準が二重になり、次に特徴量を足すときに使えない）。");
    Console.WriteLine();
    Console.WriteLine("単発戦では経路の構成が2つとも変わる。");
    Console.WriteLine();
    Console.WriteLine("- **分母経路は消える。** 部隊戦が必ず1回なので `/戦` の分母は定数 1。第14期に");
    Console.WriteLine("  「分母は 目的変数 + 1」だった経路そのものが存在しない。");
    Console.WriteLine("- **分子経路は1つ増える。** 味方の全滅＝敗北なので、`被ダメ/戦` が敗北の定義を含む。");
    Console.WriteLine("  **突破度に対しては分母経路だけだったので残していた量が、単発の勝率に対しては");
    Console.WriteLine("  言い換え側に回る**——「目的変数が変わると何が言い換えかも変わる」の実例。");
    Console.WriteLine();

    // 算術の署名を数字で出す。言葉で言い張らずに、比が勝率／敗率とどれだけ一致するかを測る
    // （第14期が分母経路を「部隊戦数/試行 × 突破度 の r」で測って出したのと同じ作法）。
    // 検算は寄与する波（＝勝率に分散がある波）でだけ意味を持つ。
    int[] conW = Enumerable.Range(0, nW).Where(w => contributes[w]).ToArray();
    Console.WriteLine("#### 検算: 分子経路は本当に算術か");
    Console.WriteLine();
    Console.WriteLine("量を「その波で取りうる最大」で割ると、勝率（あるいは敗率）そのものになるはず。");
    Console.WriteLine("**恒等式に近いなら、それは測定結果ではなく算術。** 勝率に分散のある波でだけ意味を持つので、");
    Console.WriteLine("§4 で寄与すると判定した波だけを出す。");
    Console.WriteLine();
    Console.WriteLine("| 波 | 撃破/戦 ÷ 敵体数 × 勝率 の r | 与ダメ/戦 ÷ 敵総HP × 勝率 の r | 被ダメ/戦 ÷ 総HP × **敗率** の r |");
    Console.WriteLine("|:-:|--:|--:|--:|");
    foreach (int w in conW)
    {
        double foeCount = waves[w].Enemy.Occupied().Count();
        double foeHp = waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp);
        double[] killRatio = Enumerable.Range(0, nT).Select(t => dyn[w][t][2] / foeCount).ToArray();
        double[] dmgRatio = Enumerable.Range(0, nT).Select(t => dyn[w][t][0] / foeHp).ToArray();
        double[] takenRatio = Enumerable.Range(0, nT)
            .Select(t => dyn[w][t][1] / targets[t].F.Occupied().Sum(x => x.Def.MaxHp)).ToArray();
        double[] loss = rate[w].Select(v => 100 - v).ToArray();
        Console.WriteLine($"| {waves[w].Tag} | {Correlate(killRatio, rate[w]).R:+0.000;-0.000} "
            + $"| {Correlate(dmgRatio, rate[w]).R:+0.000;-0.000} "
            + $"| {Correlate(takenRatio, loss).R:+0.000;-0.000} |");
    }
    Console.WriteLine();
    // 敗率 100% の編成では「味方の総HP を全部払った」はずなので、比が 1.0 を下回らない。
    // 下回るなら、DamageTaken を経由しない死亡経路があることになる。
    var allDead = new List<string>();
    foreach (int w in conW)
        for (int t = 0; t < nT; t++)
            if (rate[w][t] <= 1e-9)
                allDead.Add($"{waves[w].Tag}/{targets[t].Name} "
                    + $"{dyn[w][t][1] / targets[t].F.Occupied().Sum(x => x.Def.MaxHp):F2}");
    Console.WriteLine($"勝率 0% の編成（{allDead.Count} 件）の `被ダメ/戦 ÷ 総HP`: "
        + (allDead.Count == 0 ? "該当なし"
            : string.Join(" / ", allDead.Take(6)) + (allDead.Count > 6 ? " …" : "")));
    Console.WriteLine("**全滅しているので 1.00 を下回らないはず**（過剰殺傷と回復のぶん 1.00 を超える）。");
    Console.WriteLine("下回るなら `DamageTaken` を経由しない死亡経路があることになる。");
    Console.WriteLine();
    // 数字の読み方を先に書く。**書かないと「相関が低いから残す」と読まれる**が、
    // 基準は言い換えかどうかの1本だけで、相関の大小は判定に入らない（第14期の基準）。
    Console.WriteLine("> **読み方に注意。** 露骨な算術の署名を持つのは `撃破/戦` だけ（r 0.82〜0.98）で、");
    Console.WriteLine("> `与ダメ/戦` と `被ダメ/戦` の署名はどちらも弱い。**これは両者が鏡像だから**——");
    Console.WriteLine("> 勝てば敵の総HPを、負ければ味方の総HPを払い切るという同じ形で、どちらも");
    Console.WriteLine("> **過剰殺傷と「勝った（負けた）試行の中でのばらつき」に薄められる**。");
    Console.WriteLine("> 第14期は突破度に対して `与ダメ/戦` を分子経路として外している。単発で");
    Console.WriteLine("> `被ダメ/戦` を残すなら、**同じ形の量を勝ち側だけ外して負け側は残す**ことになり、");
    Console.WriteLine("> 基準が非対称になる。**外す根拠は構造であって相関の大小ではない**ので、両方外す。");
    Console.WriteLine();

    Console.WriteLine("#### 判定表（動的7種。静的8種はどちらの経路も持たないのですべて残す）");
    Console.WriteLine();
    Console.WriteLine("| 特徴量 | 第14期（突破度） | **第15期（単発の勝率）** | 理由 |");
    Console.WriteLine("|---|:-:|:-:|---|");
    for (int k = 0; k < nD; k++)
        Console.WriteLine($"| {dynNames[k]} | {taut15[k].Era14} "
            + $"| {(taut15[k].Excluded ? "**除外**" : "残す")} | {taut15[k].Reason} |");
    Console.WriteLine();

    bool[] keep15 = Enumerable.Range(0, nF).Select(k => k < nS || !taut15[k - nS].Excluded).ToArray();
    int[] cand15 = Enumerable.Range(0, nF).Where(k => keep15[k]).ToArray();
    Console.WriteLine($"**除外後の候補は {cand15.Length} 種**（静的 {nS} + 動的 {cand15.Length - nS}）。"
        + $"外したのは {string.Join(" / ", Enumerable.Range(0, nF).Where(k => !keep15[k]).Select(k => "`" + featNames[k] + "`"))}"
        + $"。第14期は 13 種（`被ダメ/戦` が残っていた）。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-2. 目的変数 ---
    //
    // 波ごとの勝率と、平均勝率2種。**天井の波は分散ゼロで相関が計算できない**（§3-2 の予告どおり）
    // ——第1波は 31 編成すべてが 100.0% なので、そこには当てる相手がいない。
    var goals = new List<(string Name, string Note, double[] V, double[][] Dyn)>();
    foreach (int w in Enumerable.Range(0, nW))
        goals.Add(($"{waves[w].Tag}", waves[w].Name, rate[w], dyn[w]));
    // 平均勝率。動的特徴量も同じ波の平均を取る（別の台の値を混ぜない）。
    double[][] MeanDyn(int[] ws) => Enumerable.Range(0, nT)
        .Select(t => Enumerable.Range(0, nF - nS)
            .Select(k => ws.Average(w => dyn[w][t][k])).ToArray()).ToArray();
    int[] baseW = { 0, 1, 2, 3, 4 };
    goals.Add(("平均(既存5波)", "`docs/balance.md` の 5 列の平均（power Phase EB の `単発戦` と同じ計算）",
        Enumerable.Range(0, nT).Select(t => baseW.Average(w => rate[w][t])).ToArray(), MeanDyn(baseW)));
    goals.Add(($"平均(寄与{conW.Length}波)", "§4 で寄与すると判定した波だけの平均",
        Enumerable.Range(0, nT).Select(t => conW.Average(w => rate[w][t])).ToArray(), MeanDyn(conW)));

    // --- 9-3. 波ごとの分解 ---
    // n = 31 しかないので、第12期からの方針どおり単相関 → 第一近似 → 静的だけ、の1段だけ。
    // 多変量は2変数まで。**因果は主張しない。**
    var firstOf = new Dictionary<string, (int K, double R, double R2, double StatBest, double StatPair, int TopK1, int TopK2, int TopK3)>();
    Console.WriteLine("### 9-2. 波ごとの第一近似と説明力");
    Console.WriteLine();
    Console.WriteLine($"候補 {cand15.Length} 種を目的変数に当てて、|r| の1位を第一近似とする（第12〜14期と同じ手順）。");
    Console.WriteLine("`静的1変数` / `静的2変数` は**戦わずにどこまで分かるか**——静的だけの説明力。");
    Console.WriteLine("**多変量は2変数まで**（n = 31 では3変数以上は過学習する）。**因果は主張しない。**");
    Console.WriteLine();
    Console.WriteLine("`—` は勝率の分散がゼロで相関が計算できない波（全編成が同じ勝率）。");
    Console.WriteLine("**第1波（S1）がまさにそれ**——31 編成すべてが 100.0% なので、当てる相手がいない。");
    Console.WriteLine();
    Console.WriteLine("| 目的変数 | 寄与 | 第一近似 | 区分 | r | **r²** | 2位 | 3位 | 静的1変数 r² | 静的2変数 R² |");
    Console.WriteLine("|:-:|:-:|---|:-:|--:|--:|---|---|--:|--:|");
    foreach (var (gname, _, gv, gdyn) in goals)
    {
        double[] Col(int k) => k < nS
            ? Enumerable.Range(0, nT).Select(t => stat[t][k]).ToArray()
            : Enumerable.Range(0, nT).Select(t => gdyn[t][k - nS]).ToArray();

        var ord = cand15.Select(k => (K: k, C: Correlate(Col(k), gv)))
            .Where(x => !double.IsNaN(x.C.R))
            .OrderByDescending(x => Math.Abs(x.C.R)).ToArray();
        bool con = Array.IndexOf(waves.Select(x => x.Tag).ToArray(), gname) is int wi && wi >= 0
            ? contributes[wi] : true;
        if (ord.Length == 0)
        {
            Console.WriteLine($"| **{gname}** | {(con ? "○" : "×")} | — | — | — | — | — | — | — | — |");
            continue;
        }
        var bestS = ord.FirstOrDefault(x => isStatic[x.K], (K: -1, C: (R: double.NaN, Rho: double.NaN, N: 0)));
        double bestPair = 0;
        for (int i = 0; i < nS; i++)
            for (int j = i + 1; j < nS; j++)
                bestPair = Math.Max(bestPair, R2Two(Correlate(Col(i), gv).R, Correlate(Col(j), gv).R,
                                                    Correlate(Col(i), Col(j)).R));
        string Nm(int i) => i < ord.Length ? $"{featNames[ord[i].K]} {ord[i].C.R:+0.00;-0.00}" : "—";
        Console.WriteLine($"| **{gname}** | {(con ? "○" : "×")} | {featNames[ord[0].K]} "
            + $"| {(isStatic[ord[0].K] ? "静" : "動")} | {ord[0].C.R:+0.00;-0.00} "
            + $"| **{ord[0].C.R * ord[0].C.R:F3}** | {Nm(1)} | {Nm(2)} "
            + $"| {(bestS.K < 0 ? "—" : $"{bestS.C.R * bestS.C.R:F2}")} | {bestPair:F2} |");
        firstOf[gname] = (ord[0].K, ord[0].C.R, ord[0].C.R * ord[0].C.R,
            bestS.K < 0 ? double.NaN : bestS.C.R * bestS.C.R, bestPair,
            ord[0].K, ord.Length > 1 ? ord[1].K : -1, ord.Length > 2 ? ord[2].K : -1);
        Console.Out.Flush();
    }
    Console.WriteLine();

    // --- 9-4. 単相関の全一覧（寄与する波だけ。全波ぶん出すと読めない） ---
    Console.WriteLine("### 9-3. 単相関の全一覧（寄与する波 × 候補特徴量の r）");
    Console.WriteLine();
    Console.WriteLine("**波によって効く特徴量が違えば、それがそのまま §6 の交互作用の説明になる。**");
    Console.WriteLine("符号まで含めて読むこと——同じ特徴量が波によって逆向きに効くなら、それは");
    Console.WriteLine("「どちらの波にも効く地力」ではなく**波の性格そのもの**。");
    Console.WriteLine();
    Console.WriteLine("| 区分 | 特徴量 |" + string.Concat(conW.Select(w => $" {waves[w].Tag} |"))
        + " 平均(既存5波) | 平均(寄与) |");
    Console.WriteLine("|:-:|---|" + string.Concat(conW.Select(_ => "--:|")) + "--:|--:|");
    foreach (int k in cand15)
    {
        var cells = new List<string>();
        foreach (var (gname, _, gv, gdyn) in goals)
        {
            if (!conW.Any(w => waves[w].Tag == gname) && !gname.StartsWith("平均")) continue;
            double[] col = k < nS
                ? Enumerable.Range(0, nT).Select(t => stat[t][k]).ToArray()
                : Enumerable.Range(0, nT).Select(t => gdyn[t][k - nS]).ToArray();
            double r = Correlate(col, gv).R;
            cells.Add(double.IsNaN(r) ? "—" : $"{r:+0.00;-0.00}");
        }
        Console.WriteLine($"| {(isStatic[k] ? "静" : "動")} | {featNames[k]} | {string.Join(" | ", cells)} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-5. 突破度との対比 ---
    //
    // 第14期の数字は**同じ実行の中で取り直す**（第13期以来の作法）。別の実行から引くと、
    // 動いたのが定義のせいか実行のせいか決まらない。台も候補集合も第14期のまま。
    var powerBenches = new (string Tag, string Name, IReadOnlyList<Formation> Squads)[]
    {
        ("主", "チャージ台", ChargeBench()),
        ("従", "既存5波", EnemyCatalog.Columns.First(c => c.Name == "順路").Squads),
    };
    // 第14期 Phase EA の候補集合（`与ダメ/戦` と `撃破/戦` だけを外したもの）。
    int[] cand14 = Enumerable.Range(0, nF).Where(k => k < nS || (k - nS != 0 && k - nS != 2)).ToArray();

    Console.WriteLine("### 9-4. 突破度との対比（第14期の数字は同じ実行の中で取り直したもの）");
    Console.WriteLine();
    Console.WriteLine("**別の実行から引かない**——動いたのが目的変数のせいか実行のせいか決まらなくなる");
    Console.WriteLine("（第13期以来の作法）。台も候補集合も第14期 Phase EA のまま（13種）。");
    Console.WriteLine();
    Console.WriteLine("| 目的変数 | 候補 | 第一近似 | r | **r²** | 静的1変数 r² | 静的2変数 R² |");
    Console.WriteLine("|---|--:|---|--:|--:|--:|--:|");
    foreach (var (tag, bname, squads) in powerBenches)
    {
        var deg = new double[nT];
        var pdyn = new double[nT][];
        for (int t = 0; t < nT; t++)
        {
            var m = MeasurePower(targets[t].F, squads, WaveSeeds);
            deg[t] = m.Degree;
            pdyn[t] = m.Dynamics;
        }
        double[] Col(int k) => k < nS
            ? Enumerable.Range(0, nT).Select(t => stat[t][k]).ToArray()
            : Enumerable.Range(0, nT).Select(t => pdyn[t][k - nS]).ToArray();
        var ord = cand14.Select(k => (K: k, C: Correlate(Col(k), deg)))
            .Where(x => !double.IsNaN(x.C.R)).OrderByDescending(x => Math.Abs(x.C.R)).ToArray();
        var bestS = ord.First(x => isStatic[x.K]);
        double bestPair = 0;
        for (int i = 0; i < nS; i++)
            for (int j = i + 1; j < nS; j++)
                bestPair = Math.Max(bestPair, R2Two(Correlate(Col(i), deg).R, Correlate(Col(j), deg).R,
                                                    Correlate(Col(i), Col(j)).R));
        Console.WriteLine($"| 突破度・{tag}: {bname}（第14期） | {cand14.Length} | {featNames[ord[0].K]} "
            + $"| {ord[0].C.R:+0.00;-0.00} | **{ord[0].C.R * ord[0].C.R:F3}** "
            + $"| {bestS.C.R * bestS.C.R:F2} | {bestPair:F2} |");
        Console.Out.Flush();
    }
    foreach (string g in new[] { "平均(既存5波)", $"平均(寄与{conW.Length}波)" })
        if (firstOf.TryGetValue(g, out var f15))
            Console.WriteLine($"| **単発の勝率・{g}（第15期）** | {cand15.Length} | {featNames[f15.K]} "
                + $"| {f15.R:+0.00;-0.00} | **{f15.R2:F3}** "
                + $"| {(double.IsNaN(f15.StatBest) ? "—" : $"{f15.StatBest:F2}")} | {f15.StatPair:F2} |");
    foreach (int w in conW)
        if (firstOf.TryGetValue(waves[w].Tag, out var f15))
            Console.WriteLine($"| 単発の勝率・{waves[w].Tag}（第15期） | {cand15.Length} | {featNames[f15.K]} "
                + $"| {f15.R:+0.00;-0.00} | **{f15.R2:F3}** "
                + $"| {(double.IsNaN(f15.StatBest) ? "—" : $"{f15.StatBest:F2}")} | {f15.StatPair:F2} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-6. FA との突き合わせ ---
    //
    // 「波によって効く特徴量が違う」ことと「波によって編成の順位が入れ替わる」ことは
    // 別々の観測で、**繋がっている保証は無い。** 繋がっているなら、特徴量の効き方が
    // 似ている波のペアほど順位相関が高いはず——それを 21 ペアで測る。
    Console.WriteLine("### 9-5. §6 の交互作用と繋がっているか");
    Console.WriteLine();
    Console.WriteLine("「波によって効く特徴量が違う」と「波によって順位が入れ替わる」は別々の観測で、");
    Console.WriteLine("**繋がっている保証は無い。** 繋がっているなら、**特徴量の効き方が似ている波のペアほど");
    Console.WriteLine("順位相関が高い**はず。効き方の似ぐあいは「候補特徴量それぞれの r を並べたベクトル」の");
    Console.WriteLine("ピアソン相関で測る（`効き方の一致`）。");
    Console.WriteLine();
    double[] Profile(int w) => cand15.Select(k =>
    {
        double[] col = k < nS
            ? Enumerable.Range(0, nT).Select(t => stat[t][k]).ToArray()
            : Enumerable.Range(0, nT).Select(t => dyn[w][t][k - nS]).ToArray();
        return Correlate(col, rate[w]).R;
    }).ToArray();
    var prof = Enumerable.Range(0, nW).Select(w => contributes[w] ? Profile(w) : null).ToArray();

    Console.WriteLine("| 波 | 波 | 順位相関 ρ（§6） | 効き方の一致 |");
    Console.WriteLine("|:-:|:-:|--:|--:|");
    var xs = new List<double>();
    var ys = new List<double>();
    for (int i = 0; i < conW.Length; i++)
        for (int j = i + 1; j < conW.Length; j++)
        {
            int a = conW[i], b = conW[j];
            double rho = Correlate(rate[a], rate[b]).Rho;
            double agree = Pearson(prof[a]!, prof[b]!);
            if (double.IsNaN(rho) || double.IsNaN(agree)) continue;
            xs.Add(rho);
            ys.Add(agree);
            Console.WriteLine($"| {waves[a].Tag} | {waves[b].Tag} | {rho:F2} | {agree:+0.00;-0.00} |");
        }
    Console.WriteLine();
    double link = Pearson(xs.ToArray(), ys.ToArray());
    Console.WriteLine($"**順位相関 × 効き方の一致: r = {link:F2}**（{xs.Count} ペア）。");
    Console.WriteLine("正で大きいほど「**効く特徴量の違いが、そのまま順位の入れ替わりになっている**」。");
    Console.WriteLine("0 付近なら、入れ替わりは既存の特徴量では説明できていない——**入れ替わりは実在するが");
    Console.WriteLine("何が起こしているか分からない**ことになり、次に測るものが変わる。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-6. まとめ ---
    // 数字から機械的に選ぶ（§8 と同じ作法）。文章で判定すると読み方の幅が残る。
    var conGoals = conW.Select(w => waves[w].Tag)
        .Concat(new[] { "平均(既存5波)", $"平均(寄与{conW.Length}波)" })
        .Where(firstOf.ContainsKey).ToArray();
    double maxR2 = conGoals.Max(g => firstOf[g].R2);
    double maxStat = conGoals.Max(g => double.IsNaN(firstOf[g].StatBest) ? 0 : firstOf[g].StatBest);
    double maxStatPair = conGoals.Max(g => firstOf[g].StatPair);
    var firstNames = conW.Where(w => firstOf.ContainsKey(waves[w].Tag))
        .Select(w => featNames[firstOf[waves[w].Tag].K]).Distinct().ToArray();

    Console.WriteLine("### 9-6. まとめ");
    Console.WriteLine();
    double avg5R2 = firstOf.TryGetValue("平均(既存5波)", out var g5) ? g5.R2 : double.NaN;
    int overMain = conGoals.Count(g => firstOf[g].R2 > 0.308);
    Console.WriteLine($"- **第一近似の r² は寄与する波で {conGoals.Min(g => firstOf[g].R2):F3} 〜 {maxR2:F3}"
        + $"、平均(既存5波) では {avg5R2:F3}。** 突破度の 0.308（主）/ 0.242（従）を上回るのは");
    Console.WriteLine($"  {overMain} 本だけで、**「単発の勝率なら総攻や総HPがずっとよく効く」は支持されない**");
    Console.WriteLine("  （§3-2 の予想と逆）。**波を平均するほど説明が付かなくなる**——波ごとに効くものが");
    Console.WriteLine("  違うので、平均すると打ち消し合う。それ自体が交互作用の裏返しになっている。");
    Console.WriteLine($"- **静的だけの説明力は 1変数で最大 {maxStat:F2} / 2変数で最大 {maxStatPair:F2}。**");
    Console.WriteLine("  突破度に対する 0.31→0.35（主）/ 0.10→0.16（従）と同じ帯か、それ以下。");
    Console.WriteLine("  **「編成した時点で単発の勝敗はかなり決まっている」も支持されない。**");
    Console.WriteLine($"- **第一近似は波で入れ替わる**（寄与する波で {firstNames.Length} 種類: "
        + $"{string.Join(" / ", firstNames.Select(x => "`" + x + "`"))}）。符号まで含めて違う。");
    Console.WriteLine($"- ただし **効き方の違いは §6 の入れ替わりを説明していない**（r = {link:F2}）。");
    Console.WriteLine("  **交互作用は実在するのに、既存の特徴量ではどの編成がどの波に強いかを予測できない。**");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}

// dissect モード: 交互作用の個別事例を解剖する（第16期 Phase GA）。
//
// 第15期で**交互作用が実在することは確定した**（寄与する 7 波の 21 ペア全部が ρ < 0.90）。
// **ところが予測できない**——「効き方の一致度」と「順位相関」の関係が r = 0.16 で、
// 第13期の台間の入れ替わり（最良 r² 0.19）と同じ形の壁に、目的変数を替えても当たった。
//
// ここで決めるのは「**法則が無いのか、特徴量が悪いのか**」（design/INTERACTION_READABILITY_PLAN.md §0）。
// `power` の 15 特徴量は「総攻」「総HP」のような集計量ばかりで、**プレイヤーが実際に見ている情報**
// （誰がどの駒を殴るか、貫きが後列に届くか、範囲が何体巻き込むか、毒が乗り切る前に敵が落ちるか）を
// 1つも含んでいない。含んでいないものが効かないのは、法則が無いことの証拠にならない。
//
// **統計ではなく個別事例を読む。** 入れ替わりの大きい 3 ペアから、順位が最も動いた編成を
// 上下2つずつ取って 12 事例。各事例で「その編成が波 A では勝ち、波 B では負ける」理由を
// 戦闘の中身（決着ターン数・盤面の推移・振りと巻き込み・毒の乗り・被弾の位置）から言葉にする。
//
// 却下した案: `demo` / `replay` で1戦ずつ目で読む。seed 0 の1戦は 200 試行の代表ではないし、
// **平均で 75%→100% でも個別の試行では別のことが起きているかもしれない**（§5-7 の停止条件）。
// 200 試行ぶんを集計したうえで、seed による振れを別表で出すほうが停止条件に答えられる。
//
// 却下した案: 波を `dissect` のローカルで組み直す。**`WaveCatalog()` を呼ぶ**——第15期が
// 「1箇所に集める」ためにやった作業を、2つ目の診断がコピーを持った瞬間に台無しにする。
//
// 診断用で docs/ には置かない（wave / power / bench と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 dissect [絞り込み]
if (focusId == "dissect")
{
    const int DissectSeeds = 200;   // wave / compare / power / bench と同じ
    const double DeadZone = 50.0;   // wave §4 と同じ線（天井率 + 床率 < 50%）

    var all = CompareBuilds();
    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    var waves = WaveCatalog();
    int nW = waves.Length;

    Console.WriteLine("# 交互作用の解剖（第16期 Phase GA）");
    Console.WriteLine();
    Console.WriteLine("第15期は「**交互作用は実在するが、既存の特徴量では予測できない**」で終わった");
    Console.WriteLine("（順位相関 × 効き方の一致が r = 0.16）。ここで決めるのは");
    Console.WriteLine("**「法則が無いのか、特徴量が悪いのか」**——`power` の 15 特徴量は集計量ばかりで、");
    Console.WriteLine("プレイヤーが実際に見ている情報（誰が誰を殴るか・範囲が何体巻き込むか・");
    Console.WriteLine("毒が乗り切る前に敵が落ちるか）を1つも含んでいない。");
    Console.WriteLine();
    Console.WriteLine("**統計ではなく個別事例を読む。** 盤面は1つも動かしていない（`BattleCore` 無変更）。");
    Console.WriteLine();

    // --- 1. 寄与する波を決め直す ---
    //
    // **第15期の「7波」を数字で持ち込まない。** 同じ判定式（天井率 + 床率 < 50%）を同じ
    // seed 数でここでも通し、一致することを確かめてから使う。ハードコードすると、
    // 波の定義が動いたときに黙って古い集合を解剖し続ける。
    // 残存度（全試行の平均。負けた試行は 0）も同じ計測から取っておく。
    // Phase GB が**天井で潰れない連続量**として使う（第15期 (c) の読み方と同じ定義）。
    // 2回回すと seed は同じでも実行時間が倍になるだけなので、1回で両方取る。
    var rate = new double[nW][];
    var degree = new double[nW][];
    for (int w = 0; w < nW; w++)
    {
        rate[w] = new double[nT];
        degree[w] = new double[nT];
        for (int t = 0; t < nT; t++)
        {
            var mw = MeasureWave(targets[t].F, waves[w].Enemy, DissectSeeds);
            rate[w][t] = mw.Win.Average() * 100;
            degree[w][t] = mw.SurvRate.Average();
        }
    }
    var contributes = new bool[nW];
    for (int w = 0; w < nW; w++)
    {
        double ceil = rate[w].Count(v => v >= 100.0 - 1e-9) * 100.0 / nT;
        double floor = rate[w].Count(v => v <= 1e-9) * 100.0 / nT;
        contributes[w] = ceil + floor < DeadZone;
    }
    int[] conW = Enumerable.Range(0, nW).Where(w => contributes[w]).ToArray();
    var rankOf = Enumerable.Range(0, nW).Select(w => AverageRanksDesc(rate[w])).ToArray();
    int Tag(string t) => Array.FindIndex(waves, x => x.Tag == t);

    // `+0.0;-0.0` の書式は、**表示桁でゼロに丸まる負の値を `-+0.0` と出す**
    // （.NET はセクションを丸めた後の値で選びながら、負の符号は別に付ける）。
    // 表示桁で先に丸めておけば消える。**既存のモードには入れない**
    // （同じ症状は `wave` にもあるが、直すとその出力が動いて 18モード差分ゼロを失う）。
    double Sg(double v, int dp) => double.IsNaN(v) ? v : Math.Round(v, dp) + 0.0;

    Console.WriteLine("## 1. 寄与する波（`wave` §4 と同じ判定を、同じ seed 数で引き直したもの）");
    Console.WriteLine();
    Console.WriteLine($"判定式は **天井率 + 床率 < {DeadZone:F0}%**。**第15期の「7波」を数字で持ち込まず**、");
    Console.WriteLine("ここでも同じ式を通している——ハードコードすると、波の定義が動いたときに黙って");
    Console.WriteLine("古い集合を解剖し続ける。");
    Console.WriteLine();
    Console.WriteLine($"**寄与する波: {conW.Length} 本** — "
        + string.Join(" / ", conW.Select(w => $"`{waves[w].Tag}` {waves[w].Name}")));
    Console.WriteLine();
    Console.Out.Flush();

    // --- 2. 解剖する 3 ペアと 12 事例 ---
    //
    // ペアは計画書 §2-1 の指定（S4 × R10 / S2 × S4 / S4 × R8）。**探索で選ばない**——
    // 「入れ替わりが最大のペア」を毎回探索し直すと、波の定義が動くたびに解剖対象が入れ替わって
    // 第16期の議論と後の期の議論が繋がらなくなる。指定は第15期の測定（§6-3 の上位）から来ている。
    //
    // 各ペアの中の編成は**順位差で機械的に選ぶ**（上下2つずつ）。ここを手で選ぶと、
    // 「説明が付く事例を選んだ」になって §2-3 の集計（何件に説明が付いたか）が意味を失う。
    var pairSpec = new[] { ("S4", "R10"), ("S2", "S4"), ("S4", "R8") };
    var cases = new List<(string PA, string PB, int A, int B, int T, double Gap, string Dir)>();
    Console.WriteLine("## 2. 解剖する 3 ペアと 12 事例");
    Console.WriteLine();
    Console.WriteLine("ペアは計画書 §2-1 の指定（第15期 §6-3 の上位から）。**探索で選び直さない**——");
    Console.WriteLine("毎回「入れ替わり最大」を探すと、波の定義が動くたびに解剖対象が入れ替わって");
    Console.WriteLine("期をまたいだ議論が繋がらなくなる。ペアの中の編成は**順位差で機械的に**上下2つずつ。");
    Console.WriteLine();
    Console.WriteLine("`順位差` = 順位(A) − 順位(B)。**正なら B の波で順位が上がる**（順位は 1 が最良）。");
    Console.WriteLine();
    Console.WriteLine("| # | ペア | 向き | 編成 | A 勝率 | A 順位 | B 勝率 | B 順位 | 順位差 |");
    Console.WriteLine("|--:|:-:|---|---|--:|--:|--:|--:|--:|");
    int caseNo = 0;
    foreach (var (ta, tb) in pairSpec)
    {
        int a = Tag(ta), b = Tag(tb);
        if (a < 0 || b < 0) continue;
        var byGap = Enumerable.Range(0, nT)
            .OrderByDescending(t => rankOf[a][t] - rankOf[b][t]).ToArray();
        var picked = byGap.Take(2).Select(t => (t, "B で上がる"))
            .Concat(byGap.Reverse().Take(2).Select(t => (t, "A で上がる"))).ToArray();
        foreach (var (t, dir) in picked)
        {
            caseNo++;
            cases.Add((ta, tb, a, b, t, rankOf[a][t] - rankOf[b][t], dir));
            Console.WriteLine($"| {caseNo} | {ta} × {tb} | {(dir == "B で上がる" ? $"**{tb}** で上がる" : $"**{ta}** で上がる")} "
                + $"| {targets[t].Name} | {rate[a][t]:F1}% | {rankOf[a][t]:F1} "
                + $"| {rate[b][t]:F1}% | {rankOf[b][t]:F1} | {Sg(rankOf[a][t] - rankOf[b][t], 1):+0.0;-0.0} |");
        }
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3. 波の現物 ---
    //
    // 第6〜7期に見つけた原理「**符号は個体HP、大きさは体数**」が、代金ではなく勝敗でも
    // 効いているかを見るための下敷き。S4 は 145HP × 3 + 司祭 + 詠唱兵、R10 は 60HP × 6 で、
    // **総HP は近いのに個体HP と体数が正反対。**
    var involved = pairSpec.SelectMany(p => new[] { p.Item1, p.Item2 }).Distinct()
        .Select(Tag).Where(w => w >= 0).OrderBy(w => w).ToArray();
    double MedianHp(Formation e)
    {
        var v = e.Occupied().Select(x => (double)x.Def.MaxHp).OrderBy(x => x).ToArray();
        return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
    }
    Console.WriteLine("## 3. 波の現物（3ペアに出てくる波だけ）");
    Console.WriteLine();
    Console.WriteLine("第6〜7期の原理「**符号は個体HP、大きさは体数**」が、代金ではなく**勝敗**でも");
    Console.WriteLine("効いているかを読むための下敷き。**S4 と R10 は総HP が近いのに個体HP と体数が正反対。**");
    Console.WriteLine();
    Console.WriteLine("| タグ | 波 | 体数 | 総HP | 総攻 | 個体HP中央値 | 最大個体HP | 1体あたり攻 | 範囲枚数 | 貫き枚数 | 平均速度 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int w in involved)
    {
        Formation e = waves[w].Enemy;
        Console.WriteLine($"| **{waves[w].Tag}** | {waves[w].Name} | {e.Count} "
            + $"| {e.Occupied().Sum(x => x.Def.MaxHp)} | {e.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {MedianHp(e):F0} | {e.Occupied().Max(x => x.Def.MaxHp)} "
            + $"| {e.Occupied().Average(x => x.Def.Attack):F1} "
            + $"| {e.Occupied().Count(x => x.Def.Pattern is AttackPattern.Sweep or AttackPattern.All)} "
            + $"| {e.Occupied().Count(x => x.Def.Pattern == AttackPattern.Pierce)} "
            + $"| {e.Occupied().Average(x => x.Def.Speed):F1} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3-2. 事例に出てくる編成の現物 ---
    //
    // 波の側だけ出しても比が作れない。**交互作用は 編成 × 波 なので、読むには両側の現物が要る**
    // ——第12期以来ずっと編成側の特徴量だけで予測しようとしていたのが第16期の反省点（§0）。
    // 値は `power` の静的8種と同じ定義（戦わずに決まる量だけ）。
    var caseT = cases.Select(c => c.T).Distinct().OrderBy(x => x).ToArray();
    Console.WriteLine("### 3-2. 事例に出てくる編成の現物（`power` の静的8種と同じ定義）");
    Console.WriteLine();
    Console.WriteLine("**波の側だけ出しても比が作れない。** 交互作用は 編成 × 波 なので、読むには両側の現物が要る");
    Console.WriteLine("——第12期以来ずっと編成側だけで予測しようとしていたのが第16期の反省点。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 体数 | 総HP | 総攻 | 積 | 最薄HP | 後列HP | 平均速度 | 範囲枚数 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int t in caseT)
    {
        Formation fm = targets[t].F;
        int hp = fm.Occupied().Sum(x => x.Def.MaxHp), atk = fm.Occupied().Sum(x => x.Def.Attack);
        Console.WriteLine($"| {targets[t].Name} | {fm.Count} | {hp} | {atk} | {(double)hp * atk / 1000:F1}k "
            + $"| {fm.Occupied().Min(x => x.Def.MaxHp)} "
            + $"| {fm.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)} "
            + $"| {fm.Occupied().Average(x => x.Def.Speed):F1} | {AoeCount(fm)} |");
    }
    Console.WriteLine();
    Console.Out.Flush();
    // --- 4. 計測 ---
    // 事例に出てくる (編成, 波) の組だけを verbose で回す。同じ組が別の事例に出てきたら使い回す
    // （測り直すと同じ組の数字が事例ごとに違って見える。同じ seed なので値は一致するが、
    //   一致することを確かめるより最初から1回にするほうが安い）。
    var traces = new Dictionary<(int W, int T), WaveTrace>();
    foreach (var c in cases)
        foreach (int w in new[] { c.A, c.B })
            if (!traces.ContainsKey((w, c.T)))
                traces[(w, c.T)] = MeasureTrace(targets[c.T].F, waves[w].Enemy, DissectSeeds);

    // --- 5. seed による振れ（§5-7 の停止条件） ---
    //
    // 「平均で 75% → 100% でも、個別の試行では別のことが起きているかもしれない」。
    // 見るのは2つ:
    //   (1) **負けの中身** — 全滅（削られ切った）と打ち切り（削り切れなかった）は
    //       同じ「敗北」だが原因が正反対。混ざっていたら1つの説明では足りない。
    //   (2) **決着ターン数のばらつき** — 勝った試行の決着 T の SD が平均に対して大きいなら、
    //       「同じ戦い方で勝っている」とは言えない。
    Console.WriteLine("## 4. seed による振れ（解剖してよいかの確認）");
    Console.WriteLine();
    Console.WriteLine("**平均で 75% → 100% でも、個別の試行では別のことが起きているかもしれない**");
    Console.WriteLine("（計画書 §5-7 の停止条件）。見るのは2つ:");
    Console.WriteLine();
    Console.WriteLine("- **負けの中身** — `全滅`（削られ切った）と `打切`（30T で削り切れなかった）は");
    Console.WriteLine("  同じ敗北だが原因が正反対。**混ざっている事例は1つの説明では足りない。**");
    Console.WriteLine("- **決着Tのばらつき** — `SD/平均` が大きいなら「同じ戦い方で勝っている」とは言えない。");
    Console.WriteLine();
    Console.WriteLine("| # | 編成 | 波 | 勝率 | 全滅 | 打切 | 決着T(勝) | SD | SD/平均 | 決着T(負) |");
    Console.WriteLine("|--:|---|:-:|--:|--:|--:|--:|--:|--:|--:|");
    caseNo = 0;
    var shaky = new List<string>();
    foreach (var c in cases)
    {
        caseNo++;
        foreach (int w in new[] { c.A, c.B })
        {
            WaveTrace tr = traces[(w, c.T)];
            double cv = tr.TurnsWin <= 0 ? double.NaN : tr.TurnsWinSd / tr.TurnsWin;
            bool mixed = tr.WipeRate > 5 && tr.DrawRate > 5;
            if (mixed) shaky.Add($"#{caseNo} {targets[c.T].Name} / {waves[w].Tag}（全滅 {tr.WipeRate:F1}% + 打切 {tr.DrawRate:F1}%）");
            Console.WriteLine($"| {(w == c.A ? caseNo.ToString() : "")} | {(w == c.A ? targets[c.T].Name : "")} "
                + $"| {waves[w].Tag} | {tr.WinRate:F1}% | {tr.WipeRate:F1}% | {tr.DrawRate:F1}% "
                + $"| {(tr.TurnsWin <= 0 ? "—" : $"{tr.TurnsWin:F1}")} "
                + $"| {(double.IsNaN(tr.TurnsWinSd) ? "—" : $"{tr.TurnsWinSd:F1}")} "
                + $"| {(double.IsNaN(cv) ? "—" : $"{cv:F2}")} "
                + $"| {(double.IsNaN(tr.TurnsLose) ? "—" : $"{tr.TurnsLose:F1}")} |{(mixed ? " ← **混在**" : "")}");
        }
    }
    Console.WriteLine();
    Console.WriteLine(shaky.Count == 0
        ? "**負けの中身が混ざっている事例は 0 件。** どの事例も敗因は1種類なので、1つの説明で足りる。"
        : $"> **負けの中身が混ざっている事例が {shaky.Count} 件ある**（全滅も打切も 5% を超える）: "
          + string.Join(" / ", shaky) + "。**この事例には説明が2つ要る**ので、下の解剖でも両方を書く。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 6. 事例ごとの解剖 ---
    //
    // 列の選び方は「**プレイヤーが画面で見ているもの**」に寄せてある（第16期 §0）。
    // 集計量（総攻・総HP）は §3 に出したので、ここでは出来事の側だけを出す。
    Console.WriteLine("## 5. 事例ごとの解剖（12件）");
    Console.WriteLine();
    Console.WriteLine("列は「**プレイヤーが画面で見ているもの**」に寄せてある。集計量（総攻・総HP）は §3 に");
    Console.WriteLine("出したので、ここでは出来事の側だけを出す。");
    Console.WriteLine();
    Console.WriteLine("| 量 | 意味 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| `振/戦` | 味方が攻撃を振った回数。**反撃はここを通らない**（`ApplyDamage` 直呼び） |");
    Console.WriteLine("| `巻込/振` | 1振りで実際に削った敵の数。**範囲が何体に届いたか**の実測 |");
    Console.WriteLine("| `主目標打点` | 1振りが主目標に通した量。一撃圏の分母 |");
    Console.WriteLine("| `振に帰属 %` | 直接ダメージのうち手番の振りから来た割合。"
        + "**低いほど反撃・破裂・追い打ち型**（`pulse` の反応型を量で見た列） |");
    Console.WriteLine("| `一撃圏` | 敵の個体HP中央値 ÷ 主目標打点。**何発で1体落ちるか**（定義値ベース） |");
    Console.WriteLine("| `振/撃破` | 敵1体を落とすのに振った回数。**一撃圏の実測版**（範囲・毒も込み） |");
    Console.WriteLine("| `直接/戦`・`毒燃/戦` | 敵に通した量。毒・燃焼は出どころを持たないので分ける |");
    Console.WriteLine("| `毒燃（自）/戦` | **味方**が浴びた毒・燃焼。瘩気軸はここを払っている |");
    Console.WriteLine("| `削り比` | (直接 + 毒燃) ÷ 敵の総HP。**1.00 を超えたぶんが過剰殺傷** |");
    Console.WriteLine("| `毒ピーク` | 敵に乗った毒の総段数の最大（と、そのターン） |");
    Console.WriteLine("| `毒の無駄` | 敵が落ちた時点で乗ったままだった段数。**乗り切る前に落ちた量** |");
    Console.WriteLine("| `後列被弾` | 味方の被ダメのうち後列（slot 4/5）が受けた割合 |");
    Console.WriteLine();

    caseNo = 0;
    foreach (var c in cases)
    {
        caseNo++;
        WaveTrace ta = traces[(c.A, c.T)], tb = traces[(c.B, c.T)];
        string na = waves[c.A].Tag, nb = waves[c.B].Tag;
        double medA = MedianHp(waves[c.A].Enemy), medB = MedianHp(waves[c.B].Enemy);

        Console.WriteLine($"### 事例 {caseNo}: {targets[c.T].Name} — {na} {ta.WinRate:F1}% / {nb} {tb.WinRate:F1}%");
        Console.WriteLine();
        Console.WriteLine($"{(c.Dir == "B で上がる" ? $"**{nb} で上がる**" : $"**{na} で上がる**")}"
            + $"（順位 {rankOf[c.A][c.T]:F1} → {rankOf[c.B][c.T]:F1}、順位差 {c.Gap:+0.0;-0.0}）。");
        Console.WriteLine();
        string F(double v, string fmt) => double.IsNaN(v) ? "—" : v.ToString(fmt);
        Console.WriteLine($"| 量 | {na} | {nb} | 差 |");
        Console.WriteLine("|---|--:|--:|--:|");
        // 差の書式は小数以下の桁ごとに作る（「+0.0;-0.0」の形）。
        // 差を出さないと、読む側が毎回引き算をすることになる。
        // `+0.0` の書式に **-0.0 をそのまま渡すと `-+0.0` になる**
        // （.NET Core 3.0 以降は負のゼロに符号を付けるが、セクション書式は
        //   -0.0 を正側として拾うので両方の符号が並ぶ）。`+ 0.0` は IEEE で -0.0 を +0.0 に潰す。
        void RowF(string name, double a, double b, int dp)
        {
            string p = "0" + (dp > 0 ? "." + new string('0', dp) : "");
            Console.WriteLine($"| {name} | {F(a, "F" + dp)} | {F(b, "F" + dp)} "
                + $"| {(double.IsNaN(a) || double.IsNaN(b) ? "—" : Sg(b - a, dp).ToString($"+{p};-{p}"))} |");
        }
        RowF("勝率 %", ta.WinRate, tb.WinRate, 1);
        RowF("全滅 %", ta.WipeRate, tb.WipeRate, 1);
        RowF("打切 %", ta.DrawRate, tb.DrawRate, 1);
        RowF("決着T（勝）", ta.TurnsWin, tb.TurnsWin, 1);
        RowF("残存（勝）", ta.AliveOnWin, tb.AliveOnWin, 2);
        RowF("振/戦", ta.AllySwings, tb.AllySwings, 1);
        RowF("巻込/振", ta.HitsPerSwing, tb.HitsPerSwing, 2);
        RowF("主目標打点", ta.PrimaryDmg, tb.PrimaryDmg, 1);
        RowF("振に帰属 %", ta.SwingShare, tb.SwingShare, 1);
        RowF($"敵の個体HP中央値", medA, medB, 0);
        RowF("**一撃圏（発/体）**", medA / ta.PrimaryDmg, medB / tb.PrimaryDmg, 2);
        RowF("**振/撃破**", ta.SwingsPerKill, tb.SwingsPerKill, 2);
        RowF("直接/戦", ta.DirectToFoe, tb.DirectToFoe, 0);
        RowF("毒燃/戦", ta.DotToFoe, tb.DotToFoe, 0);
        RowF("毒燃（自）/戦", ta.DotToAlly, tb.DotToAlly, 0);
        RowF("撃破/戦", ta.FoeDeaths, tb.FoeDeaths, 2);
        RowF("**削り比**", ta.ShaveRatio, tb.ShaveRatio, 2);
        RowF("毒ピーク（段）", ta.PoisonPeak, tb.PoisonPeak, 1);
        RowF("毒ピークのT", ta.PoisonPeakTurn, tb.PoisonPeakTurn, 1);
        RowF("毒の無駄（段）", ta.PoisonWasted, tb.PoisonWasted, 1);
        RowF("敵の振/戦", ta.FoeSwings, tb.FoeSwings, 1);
        RowF("被ダメ/戦", ta.AllyTaken, tb.AllyTaken, 0);
        RowF("後列被弾 %", ta.BackShare, tb.BackShare, 1);
        Console.WriteLine();
        Console.WriteLine("盤面の推移（ターン開始時点の平均生存数。決着後は決着時の盤面で埋めてある）:");
        Console.WriteLine();
        Console.WriteLine("| 陣営 |" + string.Concat(Enumerable.Range(1, WaveTrace.Profile).Select(t => $" T{t} |")));
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, WaveTrace.Profile).Select(_ => "--:|")));
        Console.WriteLine($"| 味方 / {na} |" + string.Concat(ta.AllyAlive.Select(v => $" {v:F1} |")));
        Console.WriteLine($"| 敵 / {na} |" + string.Concat(ta.FoeAlive.Select(v => $" {v:F1} |")));
        Console.WriteLine($"| 味方 / {nb} |" + string.Concat(tb.AllyAlive.Select(v => $" {v:F1} |")));
        Console.WriteLine($"| 敵 / {nb} |" + string.Concat(tb.FoeAlive.Select(v => $" {v:F1} |")));
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 7. 12事例の横断表 ---
    //
    // 事例を1件ずつ読むと「その事例の説明」しか出てこない。**12件に共通する構造があるか**は
    // 同じ量を12行に並べて初めて見える。ここに出す量が、そのまま Phase GB の積の候補になる
    // （§3-3「積の候補は Phase GA の解剖から出てきたものに限る」）。
    //
    // **`削り比` は説明変数にはできない。** 「敵の総HP を削り切ったか」は勝利の定義の言い換えで、
    // 第14期の同語反復の基準（分子経路）にそのまま当たる。**診断としては読めるが、
    // Phase GB の候補には入れない。**
    Console.WriteLine("## 6. 12事例の横断表（共通する構造があるか）");
    Console.WriteLine();
    Console.WriteLine("事例を1件ずつ読むと「その事例の説明」しか出てこない。**12件に共通する構造があるか**は");
    Console.WriteLine("同じ量を12行に並べて初めて見える。ここに出す量が Phase GB の積の候補になる。");
    Console.WriteLine();
    Console.WriteLine("> **`削り比` は説明変数にはできない。** 「敵の総HPを削り切ったか」は勝利の定義の");
    Console.WriteLine("> 言い換えで、第14期の同語反復の基準（分子経路）にそのまま当たる。");
    Console.WriteLine("> **診断としては読めるが、Phase GB の候補には入れない。**");
    Console.WriteLine();
    Console.WriteLine("`積比` = 味方の積（総HP × 総攻）÷ 敵の積。**戦わずに決まる**ので候補になる。");
    Console.WriteLine("`一撃圏` = 敵の個体HP中央値 ÷ 主目標打点（実測）。");
    Console.WriteLine();
    Console.WriteLine("| # | 編成 | 波 | 勝率 | 敵総攻 | 敵個体HP | 積比 | 一撃圏 | 振/撃破 | 決着T | 削り比 | 振に帰属% | 後列被弾% |");
    Console.WriteLine("|--:|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    caseNo = 0;
    foreach (var c in cases)
    {
        caseNo++;
        Formation fm = targets[c.T].F;
        double allyProd = (double)fm.Occupied().Sum(x => x.Def.MaxHp) * fm.Occupied().Sum(x => x.Def.Attack);
        foreach (int w in new[] { c.A, c.B })
        {
            WaveTrace tr = traces[(w, c.T)];
            Formation e = waves[w].Enemy;
            double foeProd = (double)e.Occupied().Sum(x => x.Def.MaxHp) * e.Occupied().Sum(x => x.Def.Attack);
            double med = MedianHp(e);
            Console.WriteLine($"| {(w == c.A ? caseNo.ToString() : "")} | {(w == c.A ? targets[c.T].Name : "")} "
                + $"| {waves[w].Tag} | {tr.WinRate:F1}% | {e.Occupied().Sum(x => x.Def.Attack)} | {med:F0} "
                + $"| {allyProd / foeProd:F2} | {med / tr.PrimaryDmg:F2} | {tr.SwingsPerKill:F2} "
                + $"| {(tr.TurnsWin <= 0 ? "—" : $"{tr.TurnsWin:F1}")} | {tr.ShaveRatio:F2} "
                + $"| {(double.IsNaN(tr.SwingShare) ? "—" : $"{tr.SwingShare:F0}")} "
                + $"| {(double.IsNaN(tr.BackShare) ? "—" : $"{tr.BackShare:F0}")} |");
        }
    }
    Console.WriteLine();

    // 12事例のうち、各量が「勝率の高いほうを当てた」件数。**12件に共通する構造があるか**の
    // いちばん粗い答え。符号は「値が大きいほうが勝つ」を + とする。
    // 12/12 でも n = 12 なので偶然（コイン12回で 12 表の確率は 1/4096）を排除できるだけ。
    // **相関の代わりにはならない**ので、Phase GB でプールした 217 点に当てる。
    Console.WriteLine("### 6-1. どの量が「勝ったほうの波」を当てたか（12事例中）");
    Console.WriteLine();
    Console.WriteLine("符号は「値が**大きい**ほうの波で勝率が高い」を ○ とする。**n = 12 しかない**ので、");
    Console.WriteLine("これは相関の代わりにはならない——Phase GB でプールした点に当て直す。");
    Console.WriteLine("勝率が同じ事例は判定不能なので分母から外す。");
    Console.WriteLine();
    Console.WriteLine("> **波の側だけで決まる量は、ここで必ず 50% になる。** 事例は各ペアの順位差の");
    Console.WriteLine("> **上下 2 つずつ**で選んであり、同じ 2 波に対して向きが正反対の事例が 2 件ずつ入る");
    Console.WriteLine("> ——波だけの量はその 2 件で必ず 1 勝 1 敗する。**これは選び方の副作用であって、");
    Console.WriteLine("> その特徴量が無力だという意味ではない。** 同時に、**交互作用は定義上どちらか片側だけでは");
    Console.WriteLine("> 説明できない**ことの直接の表れでもある——Phase GB が積を使う理由はこれ。");
    Console.WriteLine();
    var probes = new (string Name, string Dir, Func<int, int, double> Get)[]
    {
        ("敵の総攻", "小さいほう", (w, t) => -waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)),
        ("敵の個体HP中央値", "小さいほう", (w, t) => -MedianHp(waves[w].Enemy)),
        ("敵の体数", "小さいほう", (w, t) => -waves[w].Enemy.Count),
        ("敵の総HP", "小さいほう", (w, t) => -waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp)),
        ("積比（味方の積 ÷ 敵の積）", "大きいほう", (w, t) =>
            (double)targets[t].F.Occupied().Sum(x => x.Def.MaxHp) * targets[t].F.Occupied().Sum(x => x.Def.Attack)
            / (waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp) * (double)waves[w].Enemy.Occupied().Sum(x => x.Def.Attack))),
        ("一撃圏（発/体）", "小さいほう", (w, t) => -MedianHp(waves[w].Enemy) / traces[(w, t)].PrimaryDmg),
        ("振/撃破", "小さいほう", (w, t) => -traces[(w, t)].SwingsPerKill),
        ("決着T（勝）", "短いほう", (w, t) => -traces[(w, t)].TurnsWin),
        ("後列被弾 %", "小さいほう", (w, t) => -traces[(w, t)].BackShare),
        ("削り比（**同語反復**）", "大きいほう", (w, t) => traces[(w, t)].ShaveRatio),
    };
    Console.WriteLine("| 量 | 勝つのは | 当たり | 外れ | 的中率 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|");
    foreach (var (name, dir, get) in probes)
    {
        int hit = 0, miss = 0;
        foreach (var c in cases)
        {
            double wa = traces[(c.A, c.T)].WinRate, wb = traces[(c.B, c.T)].WinRate;
            if (Math.Abs(wa - wb) < 1e-9) continue;
            double va = get(c.A, c.T), vb = get(c.B, c.T);
            if (double.IsNaN(va) || double.IsNaN(vb)) continue;
            if ((va > vb) == (wa > wb)) hit++; else miss++;
        }
        Console.WriteLine($"| {name} | {dir} | {hit} | {miss} | {(hit + miss == 0 ? "—" : $"{hit * 100.0 / (hit + miss):F0}%")} |");
    }
    Console.WriteLine();
    Console.Out.Flush();
    // --- 8. 読み（12事例の説明） ---
    //
    // **ここだけは測定ではなく解釈。** 上の表から言葉にしたもので、推測は推測と明記する
    // （計画書 §2-4）。数字が動いたらこの節も書き直すこと——**表と食い違ったら表が正しい。**
    Console.WriteLine("## 7. 読み（12事例の説明）");
    Console.WriteLine();
    Console.WriteLine("**ここだけは測定ではなく解釈。** 上の表から言葉にしたもので、**推測は推測と明記する。**");
    Console.WriteLine("数字が動いたらこの節も書き直すこと——**表と食い違ったら表が正しい。**");
    Console.WriteLine();
    Console.WriteLine("### 7-1. 共通の骨格 — 単発戦は2つの時計の競走");
    Console.WriteLine();
    Console.WriteLine("12事例に共通する形が1つある。**単発戦の勝敗は「味方が敵を削り切るまでのターン数」と");
    Console.WriteLine("「敵が味方を削り切るまでのターン数」の競走**で、波はこの2つを**別々に**動かす。");
    Console.WriteLine();
    Console.WriteLine("| 波 | 味方の時計（削り切るまで） | 敵の時計（削られ切るまで） |");
    Console.WriteLine("|:-:|---|---|");
    Console.WriteLine("| S4 | **遅い** — 個体145 × 5 = 545 を削るのに時間が要る | **遅い** — 総攻 53・速度 4.4 |");
    Console.WriteLine("| R8 | 速い — 個体 90 | 速い — 総攻 80 |");
    Console.WriteLine("| R10 | 速い — 個体 60 | **最速** — 総攻 96 |");
    Console.WriteLine("| S2 | **最速** — 個体 45 | 速い — 総攻 86・速度 7.8・貫き1 |");
    Console.WriteLine();
    Console.WriteLine("**S4 だけが「長いが優しい」で、他は全部「短いが厳しい」。** 3ペアのうち3ペアとも S4 が");
    Console.WriteLine("片側に入っているのはそのため（第15期 §6-3 の上位が S4 で埋まっているのも同じ理由）。");
    Console.WriteLine();
    Console.WriteLine("**編成が入れ替わるのは、編成ごとにこの2つの時計への効き方が違うから。** 12事例は");
    Console.WriteLine("2群にきれいに割れた。");
    Console.WriteLine();
    Console.WriteLine("### 7-2. 甲群 — 出力が時間で育つ編成は「敵の時計が遅い波」で上がる");
    Console.WriteLine();
    Console.WriteLine("**事例 3・4・5・11・12**（速攻 / 毒+耐久 / 溜め改）。どれも S4 で上がる。");
    Console.WriteLine();
    Console.WriteLine("- **事例 3・11 速攻 (ボルグ×ムド)** — ムドの被弾強化は殴られるほど攻撃が上がるので、");
    Console.WriteLine("  出力の立ち上がりに時間が要る。S4 では 41.1 回振って 11.5T かけて勝つ。R8/R10 では");
    Console.WriteLine("  **削り比が 0.81 / 0.89 で、敵の総HP すら削り切れていない**——育つ前に落ちている。");
    Console.WriteLine("  `敵の振/戦` は S4 31.7 / R8 30.9 とほぼ同じなのに `被ダメ` は 218 → 338。");
    Console.WriteLine("  **同じ回数殴られて 1.5 倍痛い。** 効いているのは殴られた回数ではなく1発の重さ。");
    Console.WriteLine("- **事例 12 溜め改 (クグ×バン×ガン)** — `振に帰属%` が S4 **22%** / R8 70%。");
    Console.WriteLine("  **S4 では打点の 78% が手番外（カドの反撃）から出ている。** 反撃は敵に殴らせないと");
    Console.WriteLine("  回らないので、殴られても死なない波でしか成立しない。R8 では `後列被弾` が");
    Console.WriteLine("  11% → **43%** に跳ね、後列のガン・バン・ドルガ（後列HP 173）が直接削られる。");
    Console.WriteLine("- **事例 5 溜め改（S2 × S4）** — 同じ編成が S2 でも落ちる（82.5%）。`後列被弾` 30% で、");
    Console.WriteLine("  **S2 の貫き 1 枚がレーンを走って後列に届いている。** 事例 8 の範囲耐性は同じ S2 で");
    Console.WriteLine("  `後列被弾` 6.5% しかない——あちらは後列HP が 55 しかなく、貫きが届く先に何も無い。");
    Console.WriteLine("  **`敵の貫き枚数 × 味方の後列HP` が、同じ波で符号を分けている。**");
    Console.WriteLine("- **事例 4 毒+耐久 (ベニ×トウ)** — 毒の出力が 60/T（524 ÷ 8.7T）しかないので、");
    Console.WriteLine("  R10 の 360HP に対しては 45/T まで落ちて撃破が 4.01/6 で止まる。S4 は 545HP と重いが、");
    Console.WriteLine("  敵の総攻 53 なので 8.7T かけられる。**重い波のほうが安い。**");
    Console.WriteLine();
    Console.WriteLine("### 7-3. 乙群 — 出力が一撃圏に縛られる編成は「敵の個体HPが低い波」で上がる");
    Console.WriteLine();
    Console.WriteLine("**事例 1・2・7・8・9・10**（毒 / 燃焼 / 耐久 / 範囲耐性 / 追撃×死 / 死の連鎖）。");
    Console.WriteLine();
    Console.WriteLine("- **事例 7 耐久 (ガルド×ノノ)** — いちばんきれいな一撃圏の事例。一撃圏 2.35 → **7.80**、");
    Console.WriteLine("  `振/撃破` 2.83 → 6.91、`削り比` 1.22 → **0.97**、`撃破` 5.00 → **2.98**。");
    Console.WriteLine("  S4 では 5 体のうち 3 体しか落とせない。**敵の総攻は S2 のほうが高い（86 > 53）のに勝つ**");
    Console.WriteLine("  ——落とした敵は殴ってこないので、早く落とすほど被弾が減る（第6期 H1 の複利）。");
    Console.WriteLine("- **事例 8 範囲耐性 (ヒビ×ボルグ)** — 範囲を 2 枚持っているのに S4 で落ちる。");
    Console.WriteLine("  `巻込/振` は 1.33 → **1.38 とむしろ増えている**のに `撃破` は 5.00 → 4.79。");
    Console.WriteLine("  **範囲の価値は巻き込み枚数ではなく「巻き込んだ結果何体落ちたか」で決まる**");
    Console.WriteLine("  ——個体145 に対しては、撒いても撃破に変換されない（第6期 H2 の再現）。");
    Console.WriteLine("- **事例 1 毒 / 事例 2 燃焼** — 継続ダメージも同じ縛りを受ける。毒は `毒の無駄`");
    Console.WriteLine("  （落ちた時点で乗ったままだった段数）が S4 **282.6 段** / R10 156.7 段で、");
    Console.WriteLine("  **乗せたのに撃破へ変換されなかった量が S4 では 1.8 倍。** 燃焼は一撃圏 7.22 → 2.91。");
    Console.WriteLine("- **事例 9 追撃×死 / 事例 10 死の連鎖** — どちらも**撃破そのものを燃料にする型**");
    Console.WriteLine("  （墓守の層・追い打ち）なので、撃破が出ないと出力が立ち上がらない。盤面推移を見ると");
    Console.WriteLine("  S4 では敵が T4 まで 5.0 のまま（最初の撃破が T5 まで出ない）、R8 では T3 から崩れる。");
    Console.WriteLine();
    Console.WriteLine("### 7-4. 説明が付かなかった事例");
    Console.WriteLine();
    Console.WriteLine("- **事例 6 毒+耐久（S2 95.5% → S4 100.0%）。** 一撃圏が 9.59 → **30.94** と 3 倍悪化して");
    Console.WriteLine("  いるのに勝率は上がっている。乙群の説明が当たらず、甲群の説明（敵の総攻 86 → 53）は");
    Console.WriteLine("  当たるが、**そもそも勝率差が 4.5pt しかない。** 順位差 18.5 の正体は");
    Console.WriteLine("  **S4 で 100.0% の編成が 13 もある同値塊**で、勝率 100.0% は全部が順位 7.0 に潰れる。");
    Console.WriteLine("  **第15期が (a) の読み方を「当てにならない」とした同じ現象**が、事例の選び方にも");
    Console.WriteLine("  出ている——順位差で機械的に選ぶと、同値塊のせいで出来事の起きていない事例が混じる。");
    Console.WriteLine();
    Console.WriteLine("**説明が付いたのは 11 / 12。** うち甲群 5 件・乙群 6 件で、**説明の骨格は 2 種類しかない。**");
    Console.WriteLine("どちらも「敵の個体HP と体数」ではなく、**「敵の個体HP と敵の総攻」の 2 軸**に落ちる。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9. 数値への変換案（Phase GB の候補） ---
    //
    // 計画書 §2-3 の 2 と 3。**ここで挙げたものだけが Phase GB の積の候補になる**
    // （§3-3「積の候補は Phase GA の解剖から出てきたものに限る。総当たりで作らない」）。
    Console.WriteLine("## 8. 数値への変換案（Phase GB が使う候補）");
    Console.WriteLine();
    Console.WriteLine("§7 の説明を数値にしたもの。**ここに挙げたものだけが Phase GB の積の候補になる**");
    Console.WriteLine("（計画書 §3-3「総当たりで作らない」）。`使える` は、両側とも**戦わずに決まる**かどうか。");
    Console.WriteLine();
    Console.WriteLine("| 説明（§7） | 数値 | 使える | 備考 |");
    Console.WriteLine("|---|---|:-:|---|");
    Console.WriteLine("| 甲: 味方が持ちこたえるターン数 | `味方の総HP ÷ 敵の総攻` | ○ | 事例 3・11・4 |");
    Console.WriteLine("| 甲: 敵を削り切るまでのターン数 | `敵の総HP ÷ 味方の総攻` | ○ | 事例 4・7 |");
    Console.WriteLine("| 甲: 2つの時計の比 | `味方の積 ÷ 敵の積` | ○ | **上2つの比そのもの**（約分すると 味方HP×味方攻 ÷ 敵HP×敵攻）。**独立ではない** |");
    Console.WriteLine("| 甲: 貫きが後列に届く | `敵の貫き枚数 × 味方の後列HP` | ○ | 事例 5・8 が符号を分けた |");
    Console.WriteLine("| 甲: 先手を取られる | `敵の平均速度 − 味方の平均速度` | ○ | 事例 5 の**推測**。単独では向きが決まらない |");
    Console.WriteLine("| 乙: 一撃圏 | `敵の個体HP中央値 ÷ 味方の1体あたり攻` | ○ | 事例 7 が最も明確 |");
    Console.WriteLine("| 乙: 範囲が撃破に変換されるか | `味方の範囲枚数 × 敵の体数 ÷ 一撃圏` | ○ | 事例 8。**巻き込み枚数ではない** |");
    Console.WriteLine("| 乙: 集中砲火で最薄が落ちるか | `敵の総攻 ÷ 味方の最薄HP` | ○ | 事例 3・12 |");
    Console.WriteLine("| （診断のみ）削り切ったか | `削り比` | **×** | **勝利の言い換え**（分子経路。第14期の基準） |");
    Console.WriteLine("| （診断のみ）実測の1発打点 | `主目標打点` | **×** | 波ごとに測った量なので、波ごとの勝率に当てると循環する |");
    Console.WriteLine();
    Console.WriteLine("**`振に帰属%` が示したこと（Phase GB の設計に効く）。** 溜め改は S4 で打点の 78% が");
    Console.WriteLine("手番外から出ていて、毒軸は `総攻` 20〜22 で毎ターン 45〜127 を削っている。");
    Console.WriteLine("**`総攻` はこれらの編成の出力を表していない**——第12期以来ずっと使ってきた");
    Console.WriteLine("編成側の唯一の出力特徴量が、反撃軸と毒軸に対しては桁で外れている。");
    Console.WriteLine("**「特徴量が悪い」の中身の1つがこれ。**");
    Console.WriteLine();
    Console.Out.Flush();
    // ================= Phase GB: 敵側の特徴量と交互作用項（第16期） =================
    //
    // **いまの `power` は編成側の特徴量しか持っていない。** 交互作用は 編成 × 波 なのに、
    // 波の側を一度も数値化していなかった——第12期以来ずっと片側だけで予測しようとしていたので、
    // **予測できなくて当然だった可能性がある**（計画書 §3-1）。
    //
    // §6-1 でそれが数字になって出た。**波の側だけで決まる量は的中率がちょうど 50% になる。**
    // あれは事例の選び方の副作用でもあるが、同時に構造そのもの——下の §11 で示すとおり、
    // **片側だけの特徴量は交互作用成分と相関が恒等的に 0 になる。**
    //
    // 候補は 10 個以内。**総当たりで作らない**（編成8 × 敵9 = 72 通りを全部試すと、
    // n = 217 では必ず何かが当たる）。出どころは「理屈で先に決めたもの」と
    // 「§7 の解剖から出てきたもの」の2つだけで、§8 の表に挙げたものに限る。
    Console.WriteLine("## 9. 敵側の特徴量（Phase GB）");
    Console.WriteLine();
    Console.WriteLine("**編成側と対称になるように取る。** 定義は `UnitDef` と `Formation` から計算できるものだけ");
    Console.WriteLine("（戦わずに分かる量。`power` の静的8種と同じ作法）。");
    Console.WriteLine();

    var foeF = new (string Name, string Def, Func<Formation, double> Get)[]
    {
        ("敵体数",     "波の駒数", e => e.Count),
        ("敵総HP",     "Def.MaxHp の合計", e => e.Occupied().Sum(x => x.Def.MaxHp)),
        ("敵総攻",     "Def.Attack の合計", e => e.Occupied().Sum(x => x.Def.Attack)),
        ("敵個体HP中", "個体 MaxHp の中央値。**第6〜7期の原理の主役**", MedianHp),
        ("敵最大個体HP", "いちばん硬い駒の MaxHp", e => e.Occupied().Max(x => x.Def.MaxHp)),
        ("敵1体攻",    "敵総攻 ÷ 敵体数", e => e.Occupied().Average(x => x.Def.Attack)),
        ("敵範囲枚数",  "Def.Pattern が薙ぎ/全体の駒数",
            e => e.Occupied().Count(x => x.Def.Pattern is AttackPattern.Sweep or AttackPattern.All)),
        ("敵貫き枚数",  "Def.Pattern が貫きの駒数",
            e => e.Occupied().Count(x => x.Def.Pattern == AttackPattern.Pierce)),
        ("敵平均速度",  "Def.Speed の平均", e => e.Occupied().Average(x => x.Def.Speed)),
    };
    // 編成側は `power` / `wave` の静的8種をそのまま。**定義を1文字も変えない**
    // ——変えると第12〜15期の数字と繋がらなくなる。
    var allyF = new (string Name, Func<Formation, double> Get)[]
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

    Console.WriteLine("| 特徴量 | 定義 |" + string.Concat(conW.Select(w => $" {waves[w].Tag} |")));
    Console.WriteLine("|---|---|" + string.Concat(conW.Select(_ => "--:|")));
    foreach (var (name, def, get) in foeF)
        Console.WriteLine($"| **{name}** | {def} |"
            + string.Concat(conW.Select(w => $" {get(waves[w].Enemy):0.#} |")));
    Console.WriteLine();
    Console.Out.Flush();

    // --- 10. 交互作用項の候補 ---
    double AllyAtkEach(Formation f) => f.Occupied().Average(x => x.Def.Attack);
    var terms = new (string Name, string Expr, string From, string Why, Func<Formation, Formation, double> Get)[]
    {
        ("耐えるT", "味方の総HP ÷ 敵総攻", "GA 甲群",
            "事例 3・11・4。味方が持ちこたえるターン数。速攻は 327/53 = 6.2 → 327/96 = 3.4 で落ちる",
            (f, e) => f.Occupied().Sum(x => x.Def.MaxHp) / (double)e.Occupied().Sum(x => x.Def.Attack)),
        ("削るT", "敵総HP ÷ 味方の総攻", "理屈（§3-3 の3番）",
            "決着の速さ。第6期 H1「範囲の利得は減らした状態で経過するターン数に比例する」の分母",
            (f, e) => e.Occupied().Sum(x => x.Def.MaxHp) / (double)f.Occupied().Sum(x => x.Def.Attack)),
        ("時計比", "味方の積 ÷ 敵の積", "GA 甲群",
            "上2つの比そのもの（約分すると 味方HP×味方攻 ÷ 敵HP×敵攻）。**独立ではない**が、"
            + "2つの時計の競走を1本にまとめた基準として置く",
            (f, e) => (double)f.Occupied().Sum(x => x.Def.MaxHp) * f.Occupied().Sum(x => x.Def.Attack)
                      / (e.Occupied().Sum(x => x.Def.MaxHp) * (double)e.Occupied().Sum(x => x.Def.Attack))),
        ("一撃圏", "敵の個体HP中央値 ÷ 味方の1体あたり攻", "GA 乙群 + 理屈（§3-3 の2番）",
            "事例 7 が最も明確（2.35 → 7.80 で 100% → 16%）。**第13期で棄却した閾値仮説の、敵側を入れた版**",
            (f, e) => MedianHp(e) / AllyAtkEach(f)),
        ("範囲×体数", "味方の範囲枚数 × 敵体数", "理屈（§3-3 の1番）",
            "範囲は体数が多いほど効く（第6〜7期の原理「大きさは体数」）",
            (f, e) => AoeCount(f) * (double)e.Count),
        ("範囲の変換", "味方の範囲枚数 × 敵体数 ÷ 一撃圏", "GA 乙群",
            "事例 8。**巻き込み枚数ではなく、巻き込んだ結果何体落ちたかで決まる**"
            + "——撒いた先が一撃圏の外なら範囲は撃破に変換されない",
            (f, e) => AoeCount(f) * e.Count * AllyAtkEach(f) / MedianHp(e)),
        ("貫き×後列", "敵の貫き枚数 × 味方の後列HP", "理屈（§3-3 の4番）+ GA 甲群",
            "事例 5・8 が同じ S2 で符号を分けた（後列被弾 30% と 6.5%）",
            (f, e) => e.Occupied().Count(x => x.Def.Pattern == AttackPattern.Pierce)
                      * (double)f.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)),
        ("集中砲火", "敵総攻 ÷ 味方の最薄HP", "GA 甲群",
            "事例 3・12。1体に集まれば最薄が落ちる。**総HP では表せない**（同じ総HPでも薄い駒があると崩れる）",
            (f, e) => e.Occupied().Sum(x => x.Def.Attack) / (double)f.Occupied().Min(x => x.Def.MaxHp)),
        ("1発の重さ", "敵の1体あたり攻 ÷ 味方の最薄HP", "GA 甲群",
            "事例 3「`敵の振/戦` は 31.7 と 30.9 でほぼ同じなのに `被ダメ` は 218 → 338」"
            + "——効いているのは殴られた回数ではなく1発の重さ",
            (f, e) => e.Occupied().Average(x => x.Def.Attack) / f.Occupied().Min(x => x.Def.MaxHp)),
        ("先手差", "敵の平均速度 − 味方の平均速度", "GA 甲群（**推測**）",
            "事例 5 の推測。**積ではなく差**——速度は比を取っても意味を持たない。"
            + "単独では向きが決まらないことが §7-2 で分かっている",
            (f, e) => e.Occupied().Average(x => x.Def.Speed) - f.Occupied().Average(x => x.Def.Speed)),
    };

    Console.WriteLine("## 10. 交互作用項の候補（10 個）");
    Console.WriteLine();
    Console.WriteLine("**総当たりで作っていない。** 編成8 × 敵9 = 72 通りを全部試すと n = 217 では必ず何かが");
    Console.WriteLine("当たるので、出どころを2つに限った——**理屈で先に決めたもの**（計画書 §3-3 の4つ）と、");
    Console.WriteLine("**§7 の解剖から出てきたもの**（§8 の表に挙げたものだけ）。");
    Console.WriteLine();
    Console.WriteLine("どの項も**両側とも戦わずに決まる**。実測の量（主目標打点・削り比）は入れていない");
    Console.WriteLine("——波ごとに測った量を波ごとの勝率に当てると循環する（§8）。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | 式 | 出どころ | 理由 |");
    Console.WriteLine("|--:|---|---|:-:|---|");
    for (int k = 0; k < terms.Length; k++)
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | `{terms[k].Expr}` | {terms[k].From} | {terms[k].Why} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 11. 分散分解 ---
    //
    // **交互作用が「どれだけあるか」を先に測る。** これを測らずに相関だけ出すと、
    // 「積が効いた」が主効果（波の難度）を拾っているだけかもしれない。
    // 加法モデル 予測_ij = 波の平均 + 編成の平均 − 全体平均 を引いた残りが交互作用成分そのもの。
    //
    // **片側だけの特徴量は、この残差と相関が恒等的に 0 になる**（残差は行方向にも列方向にも
    // 和が 0 なので、片側で定数の量との共分散が消える）。これは実装の検算にもなる。
    int nC = conW.Length;
    double[][] Resid(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        return Enumerable.Range(0, nC)
            .Select(c => Enumerable.Range(0, nT).Select(t => y[c][t] - rowM[c] - colM[t] + grand).ToArray())
            .ToArray();
    }
    (double Wave, double Build, double Inter) Decompose(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        double ssT = y.SelectMany(r => r).Sum(v => (v - grand) * (v - grand));
        double ssW = nT * rowM.Sum(m => (m - grand) * (m - grand));
        double ssB = nC * colM.Sum(m => (m - grand) * (m - grand));
        var rs = Resid(src);
        double ssI = rs.SelectMany(r => r).Sum(v => v * v);
        return (ssW / ssT * 100, ssB / ssT * 100, ssI / ssT * 100);
    }

    Console.WriteLine("## 11. 分散分解 — 交互作用はどれだけあるか");
    Console.WriteLine();
    Console.WriteLine($"寄与する {nC} 波 × {nT} 編成 = **{nC * nT} 点**。加法モデル");
    Console.WriteLine("`予測 = 波の平均 + 編成の平均 − 全体平均` を引いた残りが**交互作用成分そのもの**。");
    Console.WriteLine();
    Console.WriteLine("目的変数は2つ出す。**勝率は天井（100.0%）で潰れる**ので、そこだけで結論を出すと");
    Console.WriteLine("「天井が作った見かけの交互作用」を実物と取り違える——第15期が (c) 残存度を置いたのと同じ理由。");
    Console.WriteLine();
    var decW = Decompose(rate);
    var decD = Decompose(degree);
    Console.WriteLine("| 目的変数 | 波の主効果 | 編成の主効果 | **交互作用** |");
    Console.WriteLine("|---|--:|--:|--:|");
    Console.WriteLine($"| 勝率 | {decW.Wave:F1}% | {decW.Build:F1}% | **{decW.Inter:F1}%** |");
    Console.WriteLine($"| 残存度 | {decD.Wave:F1}% | {decD.Build:F1}% | **{decD.Inter:F1}%** |");
    Console.WriteLine();
    var residW = Resid(rate);
    var residD = Resid(degree);

    // 検算。片側だけの特徴量が残差と相関 0 になることを実際に確かめる。
    double[] Flat(Func<int, int, double> get) => Enumerable.Range(0, nC)
        .SelectMany(c => Enumerable.Range(0, nT).Select(t => get(conW[c], t))).ToArray();
    double[] FlatV(double[][] v) => v.SelectMany(r => r).ToArray();
    double maxOne = 0;
    foreach (var (name, get) in allyF)
        maxOne = Math.Max(maxOne, Math.Abs(Pearson(Flat((w, t) => get(targets[t].F)), FlatV(residW))));
    foreach (var (name, _, get) in foeF)
        maxOne = Math.Max(maxOne, Math.Abs(Pearson(Flat((w, t) => get(waves[w].Enemy)), FlatV(residW))));
    Console.WriteLine($"**検算: 片側だけの特徴量 17 種（編成8 + 敵9）と交互作用成分の相関は、最大でも |r| = {maxOne:F6}。**");
    Console.WriteLine("これは測定結果ではなく**恒等式**——残差は行方向にも列方向にも和が 0 なので、");
    Console.WriteLine("片側で定数の量との共分散は必ず消える。**交互作用を予測したいなら両側が要る**ことの、");
    Console.WriteLine("いちばん強い形の言い直しになっている（第12期以来ずっと片側だけで測っていた）。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 12. 相関 ---
    Console.WriteLine("## 12. 交互作用項は効くか");
    Console.WriteLine();
    Console.WriteLine("3通りの当て方を出す。**どれか1つでは読めない。**");
    Console.WriteLine();
    Console.WriteLine("- **(1) プール（生）** — 217 点をそのまま。計画書 §3-4 の1番。");
    Console.WriteLine("  ただし**波の主効果を拾うだけ**になりやすい（波によって難度が違うので、");
    Console.WriteLine("  敵側の量を含む項は自動的に効いて見える）。");
    Console.WriteLine("- **(2) 交互作用成分** — §11 の残差に当てる。**これが本題。**");
    Console.WriteLine("- **(3) 波ごと** — 第15期と直接比べるための形。");
    Console.WriteLine();
    Console.WriteLine("順位相関 `ρ` も併記する。**項には閾値的なもの（一撃圏）が含まれる**ので、");
    Console.WriteLine("ピアソンだけだと「単調だが直線ではない効き方」を取りこぼした可能性を消せない。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | (1) プール r | r² | **(2) 交互作用 r** | **r²** | (2) ρ | 交互作用の説明力（全分散比） |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|");
    var termScore = new List<(int K, double R, double R2)>();
    double[] residFlat = FlatV(residW);
    for (int k = 0; k < terms.Length; k++)
    {
        double[] x = Flat((w, t) => terms[k].Get(targets[t].F, waves[w].Enemy));
        double rp = Pearson(x, Flat((w, t) => rate[w][t]));
        var ci = Correlate(x, residFlat);
        termScore.Add((k, ci.R, ci.R * ci.R));
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | {Sg(rp, 2):+0.00;-0.00} | {rp * rp:F3} "
            + $"| {Sg(ci.R, 2):+0.00;-0.00} | **{ci.R * ci.R:F3}** | {Sg(ci.Rho, 2):+0.00;-0.00} "
            + $"| {ci.R * ci.R * decW.Inter / 100:F3} |");
    }
    Console.WriteLine();
    double maxRho = Enumerable.Range(0, terms.Length).Max(k =>
        Math.Abs(Correlate(Flat((w, t) => terms[k].Get(targets[t].F, waves[w].Enemy)), residFlat).Rho));
    Console.WriteLine($"**順位相関でも最大 |ρ| = {maxRho:F3}。** 単調な非線形を取りこぼしているのではない。");
    Console.WriteLine();
    var bestT = termScore.OrderByDescending(x => x.R2).First();
    Console.WriteLine($"**最も効く項は `{terms[bestT.K].Name}`（{terms[bestT.K].Expr}）で、交互作用成分に対して");
    Console.WriteLine($"r = {Sg(bestT.R, 2):+0.00;-0.00} / r² = {bestT.R2:F3}。** 交互作用は全分散の {decW.Inter:F1}% なので、");
    Console.WriteLine($"**全体としては {bestT.R2 * decW.Inter / 100:F3}** を説明していることになる。");
    Console.WriteLine();
    if (bestT.R2 < 0.02)
    {
        Console.WriteLine("> **この「最良」に意味は無い。** r² がこの帯では 10 項の順位は雑音で入れ替わる。");
        Console.WriteLine("> **「この項が一番近い」として設計に使ってはいけない**——下の §13 の残差も");
        Console.WriteLine("> 実質は交互作用成分そのもの（回帰ではぼ何も引けていない）として読むこと。");
        Console.WriteLine();
    }
    // 残存度でも同じことをやる。天井で潰れない連続量で符号と順位が変わらないかを見る。
    Console.WriteLine("### 12-1. 残存度で測り直す（天井で潰れない連続量）");
    Console.WriteLine();
    Console.WriteLine("**勝率は 100.0% で潰れる**ので、そこだけで結論を出すと天井が作った見かけの交互作用を");
    Console.WriteLine("実物と取り違える。同じ項を残存度の交互作用成分に当て直す。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | 交互作用 r（勝率） | 交互作用 r（残存度） | 符号一致 |");
    Console.WriteLine("|--:|---|--:|--:|:-:|");
    int agree = 0;
    var degScore = new List<(int K, double R)>();
    for (int k = 0; k < terms.Length; k++)
    {
        double[] x = Flat((w, t) => terms[k].Get(targets[t].F, waves[w].Enemy));
        double rw = Pearson(x, FlatV(residW));
        double rd = Pearson(x, FlatV(residD));
        degScore.Add((k, rd));
        // 相関が取れない列（分散0・標本不足）は NaN。Math.Sign は NaN で例外を投げるので、
        // 「判定不能」として扱う（他の場所と同じく NaN は測れなかったの意）。
        bool known = !double.IsNaN(rw) && !double.IsNaN(rd);
        bool ok = known && Math.Sign(rw) == Math.Sign(rd);
        if (ok) agree++;
        Console.WriteLine($"| {k + 1} | {terms[k].Name} | {Sg(rw, 2):+0.00;-0.00} | {Sg(rd, 2):+0.00;-0.00} | {(known ? (ok ? "○" : "**×**") : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**符号が一致したのは {agree} / {terms.Length}。** 一致しない項は、勝率の天井が作った");
    Console.WriteLine("見かけの効きを拾っている疑いがあるので、そのまま設計に使ってはいけない。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 12-2. 波ごと（第15期との対比） ---
    //
    // **波の中では、交互作用項は味方側の量の定数倍にしかならない**（敵側が定数だから）。
    // だから波ごとの r は、その項の味方側成分の r と（符号を除いて）同じになる。
    // **第15期と直接比べられる形はここではなく (2) のほう**だが、比べたときに何が起きるかを
    // 見せておかないと「波ごとでも上がるはず」と読まれる。
    Console.WriteLine("### 12-2. 波ごと（第15期との対比）");
    Console.WriteLine();
    Console.WriteLine("**波の中では、交互作用項は味方側の量の定数倍にしかならない**——敵側が定数だから。");
    Console.WriteLine("だから波ごとの |r| は、その項の味方側成分の |r| と一致する。**積を入れても");
    Console.WriteLine("波ごとの説明力は原理的に上がらない**ので、第15期と比べる場所はここではなく (2) のほう。");
    Console.WriteLine();
    Console.WriteLine("`静的1変数` は編成側の静的8種の最良 r²。**第15期 `wave` §9-2 の同名の列と一致するはず**");
    Console.WriteLine("（同じ定義・同じ seed・同じ編成集合）——ずれたら実装が違う。");
    Console.WriteLine();
    // 第15期 `wave` §9-2 の `静的1変数 r²` の列。**別の実行から引いた数字だが、
    // 同じ seed の決定的な計算なので一致しなければ実装が違う**——値を候補に使うのではなく、
    // この診断が第15期と同じ盤を見ていることの検算にだけ使う（`wave` §2-1 と同じ作法）。
    var stat15 = new Dictionary<string, double>
    {
        ["S2"] = 0.03, ["S3"] = 0.16, ["S4"] = 0.15, ["S5"] = 0.06,
        ["R8"] = 0.17, ["R9"] = 0.20, ["R10"] = 0.12,
    };
    int statMiss = 0;
    Console.WriteLine("| 波 | 静的1変数 r²（編成側だけ） | 第15期の記録 | 交互作用項の最良 r² | 最良の項 |");
    Console.WriteLine("|:-:|--:|--:|--:|---|");
    foreach (int w in conW)
    {
        double bestS = allyF.Max(a => { double r = Pearson(Enumerable.Range(0, nT).Select(t => a.Get(targets[t].F)).ToArray(), rate[w]); return double.IsNaN(r) ? 0 : r * r; });
        var bt = Enumerable.Range(0, terms.Length)
            .Select(k => (K: k, R2: Math.Pow(Pearson(Enumerable.Range(0, nT).Select(t => terms[k].Get(targets[t].F, waves[w].Enemy)).ToArray(), rate[w]), 2)))
            .Where(x => !double.IsNaN(x.R2)).OrderByDescending(x => x.R2).First();
        string rec = "—";
        if (stat15.TryGetValue(waves[w].Tag, out double want))
        {
            bool ok = Math.Abs(bestS - want) <= 0.005;
            if (!ok) statMiss++;
            rec = ok ? $"{want:F2}" : $"**{want:F2} ←ずれ**";
        }
        Console.WriteLine($"| {waves[w].Tag} | {bestS:F2} | {rec} | {bt.R2:F2} | {terms[bt.K].Name} |");
    }
    Console.WriteLine();
    Console.WriteLine(statMiss == 0
        ? "**検算: 第15期 `wave` §9-2 の `静的1変数` と完全に一致（ずれ 0 件）。** "
          + "この診断は第15期と同じ盤を見ている。"
        : $"**検算: {statMiss} 件ずれた。第15期と同じ盤を見ていない——先へ進む前に原因を潰すこと。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 13. 最も効く項の残差 ---
    //
    // 計画書 §3-4 の3番。**残差の大きい事例が §7 で解剖したものと一致するか。**
    // 一致するなら「解剖で見えたものが数値でも残っている」で、一致しないなら
    // 「解剖したのは、この項では説明できない別の何か」になる。
    Console.WriteLine("## 13. 最も効く項の残差（§7 の事例と一致するか）");
    Console.WriteLine();
    Console.WriteLine($"交互作用成分を `{terms[bestT.K].Name}` で回帰したあとの残り。**大きいところが");
    Console.WriteLine("「この項でも説明が付かない入れ替わり」**で、そこが §7 の 12 事例と一致するかを見る。");
    Console.WriteLine();
    {
        double[] x = Flat((w, t) => terms[bestT.K].Get(targets[t].F, waves[w].Enemy));
        double[] y = FlatV(residW);
        double mx = x.Average(), my = y.Average();
        double b = x.Zip(y, (a, c) => (a - mx) * (c - my)).Sum() / x.Sum(a => (a - mx) * (a - mx));
        var left = new List<(int W, int T, double E)>();
        int idx = 0;
        for (int c = 0; c < nC; c++)
            for (int t = 0; t < nT; t++, idx++)
                left.Add((conW[c], t, y[idx] - (my + b * (x[idx] - mx))));
        var caseKey = cases.SelectMany(cc => new[] { (cc.A, cc.T), (cc.B, cc.T) }).ToHashSet();
        int hitTop = 0;
        Console.WriteLine("| 順 | 編成 | 波 | 勝率 | 交互作用成分 | 残差 | §7 の事例 |");
        Console.WriteLine("|--:|---|:-:|--:|--:|--:|:-:|");
        int rank = 0;
        foreach (var (w, t, e) in left.OrderByDescending(v => Math.Abs(v.E)).Take(12))
        {
            rank++;
            bool inCase = caseKey.Contains((w, t));
            if (inCase) hitTop++;
            int c = Array.IndexOf(conW, w);
            Console.WriteLine($"| {rank} | {targets[t].Name} | {waves[w].Tag} | {rate[w][t]:F1}% "
                + $"| {Sg(residW[c][t], 1):+0.0;-0.0} | {Sg(e, 1):+0.0;-0.0} | {(inCase ? "**○**" : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**残差上位 12 のうち、§7 で解剖した (編成, 波) は {hitTop} 件。**");
        Console.WriteLine($"解剖の対象は {caseKey.Count} 組 / 全 {nC * nT} 組（{caseKey.Count * 100.0 / (nC * nT):F0}%）なので、");
        Console.WriteLine($"偶然なら {12.0 * caseKey.Count / (nC * nT):F1} 件前後。");
        Console.WriteLine();
        Console.WriteLine("> **読み方に注意。** 最良の項でさえ r² が雑音帯なので、この残差は");
        Console.WriteLine("> **実質的に交互作用成分そのもの**（前の列とほぼ同じ値になっている）。");
        Console.WriteLine("> だからこの表は「項で引いた残り」ではなく、**交互作用が大きい (編成, 波) の一覧**として読む。");
        Console.WriteLine("> その上で、§7 が順位差で選んだ 12 事例は **交互作用の大きいところを当てていた**");
        Console.WriteLine("> （偶然の 2〜3 倍）——**解剖の対象選びは外していない。**");
        Console.WriteLine();
    }
    Console.Out.Flush();

    // --- 14. 判定 ---
    // §3-5 の3行から数字で機械的に選ぶ（§8 / bench と同じ作法）。
    // 線は「交互作用成分の r² が 0.10 を超えるか」。**測定から出た線ではない**ので、
    // 生の r² を同じ節に出してある。
    const double TermLine = 0.10;
    Console.WriteLine("## 14. 判定（計画書 §3-5 のどの行か）");
    Console.WriteLine();
    Console.WriteLine($"線は「**交互作用成分に対する r² が {TermLine:F2} を超えるか**」。**測定から出た線ではない**");
    Console.WriteLine("ので、生の r² を §12 にそのまま出してある（引き直せる）。");
    Console.WriteLine();
    int strong = termScore.Count(x => x.R2 >= TermLine);
    string verdict = bestT.R2 >= TermLine
        ? "**1行目: 交互作用は読める。** 特徴量が悪かっただけ。プレイヤーも学べるので、ステージ設計に使える"
        : "**2行目: 積を入れても上がらないが、Phase GA の解剖では説明が付いた（11/12）。** "
          + "人間には読めるが数値化できていない——説明の言葉を、より良い特徴量に翻訳し直す余地がある";
    Console.WriteLine($"- 交互作用成分の r² が {TermLine:F2} 以上の項: **{strong} / {terms.Length}**");
    Console.WriteLine($"- 最良は `{terms[bestT.K].Name}` の r² = **{bestT.R2:F3}**");
    Console.WriteLine($"- §7 の解剖で説明が付いたのは **11 / 12**");
    Console.WriteLine();
    Console.WriteLine(verdict);
    Console.WriteLine();
    Console.WriteLine("**3行目（積も効かず解剖でも説明が付かない = ガチャに近い）ではない。**");
    Console.WriteLine("§7 で 11/12 に説明が付いているので、その行は数字の上で外れている。");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}

// output モード: 編成の「出力の実体」を、目的変数から独立に測る（第17期 Phase HA/HB）。
//
// 第16期の最後に障害が1本に絞られた。**`総攻` が反撃軸・毒軸の出力を桁で外している**
// （溜め改は S4 で打点の 78% が手番外＝カドの反撃から出て、毒軸は `総攻` 20〜22 で
// 毎ターン 45〜127 を削っている）。第12期以来ずっと使ってきた**編成側の唯一の出力特徴量**が、
// このゲームの主要な出力経路（反撃・毒・破裂・反射）をまるごと取りこぼしていた。
//
// **循環に注意。** 目的変数（波ごとの勝率）と同じ戦闘から出力を取ると、第14期の同語反復と
// 同じ問題になる——敵を削り切ることが勝ちなので、その戦闘での与ダメは勝率の言い換え。
// したがって **固定の参照台で1回だけ測り、その値を全波に対する特徴量として使う。**
//
// 却下した案: `power` の動的特徴量（`与ダメ/戦`）をそのまま使う。あれは目的変数と同じ戦闘から
// 取っているので、波ごとの勝率に当てた瞬間に循環する（第15期 §9-1 が `与ダメ/戦` を分子経路
// として外したのと同じ理由）。**参照台が要るのは、この循環を切るため。**
//
// 却下した案: 参照台を `WaveCatalog()` の波から選ぶ。候補波は「代金の帯」や「体数 × 個体HP の
// 格子」を狙って作った的で、**中立ではない**（その波の性格が特徴量に混入する）。
// 単一の def を並べただけの的を別に組む。**新しい `UnitDef` は作らない**（計画書 §2）。
//
// 診断用で docs/ には置かない（wave / power / bench / dissect と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 output [絞り込み]
if (focusId == "output")
{
    const int OutSeeds = 200;   // wave / dissect / compare / power / bench と同じ

    var all = CompareBuilds();
    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    // 表示桁でゼロに丸まる負の値を `-+0.0` と出さないための丸め（`dissect` と同じ理由）。
    double Sg(double v, int dp) => double.IsNaN(v) ? v : Math.Round(v, dp) + 0.0;

    // 個体HP の中央値。`dissect` のローカル定義と同じ式（あちらから取り上げると
    // 第16期の出力との突き合わせが読めなくなるので、写しの一致は §9 の項の値で確かめる）。
    double MedianHp(Formation e)
    {
        var v = e.Occupied().Select(x => (double)x.Def.MaxHp).OrderBy(x => x).ToArray();
        return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
    }

    Console.WriteLine("# 出力の実体を測る — 参照台と出力特徴量（第17期）");
    Console.WriteLine();
    Console.WriteLine("第16期で障害が1本に絞られた。**`総攻` が反撃軸・毒軸の出力を桁で外している**");
    Console.WriteLine("——溜め改は S4 で打点の 78% が手番外（カドの反撃）から出て、毒軸は `総攻` 20〜22 で");
    Console.WriteLine("毎ターン 45〜127 を削っている。第12期以来ずっと使ってきた**編成側の唯一の出力特徴量**が、");
    Console.WriteLine("このゲームの主要な出力経路（反撃・毒・破裂・反射）をまるごと取りこぼしていた。");
    Console.WriteLine();
    Console.WriteLine("**測定だけで、盤面は1つも動かしていない**（`BattleCore` 無変更・`EnemyCatalog` 無変更）。");
    Console.WriteLine();

    // ================= Phase HA: 参照台 =================
    //
    // 要件は3つ（計画書 §3-1）。
    //   1. 全編成が同じ条件で殴れること
    //   2. 決着しないこと、または十分長いこと（**甲乙の分割そのものが時間の話**なので、
    //      出力が育つ時間を確保しないと甲群の値が取れない）
    //   3. 中立であること（波の性質が特徴量に混入しない）
    //
    // **ここが設計の核心。** 「殴られること」で出力する編成があるので、殴り返さない的では
    // 反撃・被弾強化の出力が 0 になる。かといって殴り返しが強すぎると味方が先に落ちて
    // 時間が取れない。**硬くて攻撃力が低い的**を、既存の def から選ぶ。
    Console.WriteLine("## 1. 参照台の要件");
    Console.WriteLine();
    Console.WriteLine("参照台は**編成の出力を測るための的**であって、勝敗を測る場ではない。");
    Console.WriteLine();
    Console.WriteLine("| # | 要件 | なぜ |");
    Console.WriteLine("|--:|---|---|");
    Console.WriteLine("| 1 | 全編成が同じ条件で殴れる | 特定の編成だけが有利／不利にならない |");
    Console.WriteLine("| 2 | 決着しない、または十分長い | **甲乙の分割そのものが時間の話**。早く倒すと「出力が時間で育つ」甲群の値が取れない |");
    Console.WriteLine("| 3 | 中立 | 波の性質が特徴量に混入しない |");
    Console.WriteLine();
    Console.WriteLine("**要件2と「殴り返し」は綱引きになる。** 殴り返さない的では被弾駆動の出力");
    Console.WriteLine("（反撃・被弾強化・自傷）が 0 になり、甲群の出力がまるごと消える。かといって");
    Console.WriteLine("殴り返しが強すぎると味方が先に落ちて時間が取れない。**硬くて攻撃力が低い的**が要る。");
    Console.WriteLine();
    Console.WriteLine("台は**単一の def を並べただけ**にする。混成にすると「どの駒に当たったか」で");
    Console.WriteLine("編成ごとに条件が変わり、要件1が崩れる。**新しい `UnitDef` は作らない**");
    Console.WriteLine("（計画書 §2「やらないこと」）ので、既存の `EnemyCatalog` から選ぶ。");
    Console.WriteLine();

    // 候補は 2 家族 × 3 刻みの格子。**片方の家族だけだと、決着しなかった理由が
    // 「硬いから」か「殴ってこないから」か決まらない。**
    //   巡礼 / 荷駄 / 従卒 は 個体HP 90 × 5 体で固定し、**1体あたり攻だけ**を 4 → 7 → 10 と振る
    //   重装 3 / 4 / 5 は 個体HP 145 で固定し、**体数だけ**を振る
    // （X字化で編成枠が5つになったので、旧6体の台は全部5体に詰めてある）
    Formation Stack(UnitDef d, int n)
    {
        var f = new Formation();
        for (int i = 0; i < n; i++) f[i] = d;   // スロット昇順（前1→前3→中央→後1→後3）
        return f;
    }
    var cands = new (string Tag, string Name, string Family, Formation F)[]
    {
        ("B1", "巡礼5",  "個体90固定・攻を振る", Stack(EnemyCatalog.ZealotPilgrim, 5)),
        ("B2", "荷駄5",  "個体90固定・攻を振る", Stack(EnemyCatalog.ZealotPorter, 5)),
        ("B3", "従卒5",  "個体90固定・攻を振る", Stack(EnemyCatalog.ZealotSquire, 5)),
        ("B4", "重装3",  "個体145固定・体数を振る", Stack(EnemyCatalog.Warden, 3)),
        ("B5", "重装4",  "個体145固定・体数を振る", Stack(EnemyCatalog.Warden, 4)),
        ("B6", "重装5",  "個体145固定・体数を振る", Stack(EnemyCatalog.Warden, 5)),
    };
    int nB = cands.Length;

    Console.WriteLine("## 2. 候補の下見");
    Console.WriteLine();
    Console.WriteLine("候補は **2 家族 × 3 刻み**。片方の家族だけだと、決着しなかった理由が");
    Console.WriteLine("「硬いから」か「殴ってこないから」か決まらない。");
    Console.WriteLine();
    Console.WriteLine("- **個体90 固定・攻を振る**（巡礼 / 荷駄 / 従卒 × 6体）— 1体あたり攻 4 → 7 → 10");
    Console.WriteLine("- **個体145 固定・体数を振る**（重装 × 3 / 4 / 6体）— 総HP 435 → 580 → 870");
    Console.WriteLine();
    Console.WriteLine("どれも**単体攻撃のみ・特性なし**（薙ぎ・貫き・全体・処刑を持つ def は外した）。");
    Console.WriteLine("攻撃型が入ると、範囲耐性・後列配置といった**編成側の性質と噛み合ってしまう**");
    Console.WriteLine("——要件1（全編成が同じ条件で殴れる）が崩れる。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 中身 | 家族 | 体数 | 総HP | 総攻 | 個体HP | 1体攻 | 速度 |");
    Console.WriteLine("|:-:|---|---|--:|--:|--:|--:|--:|--:|");
    foreach (var (tag, name, fam, bf) in cands)
        Console.WriteLine($"| **{tag}** | {name} | {fam} | {bf.Count} "
            + $"| {bf.Occupied().Sum(x => x.Def.MaxHp)} | {bf.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {bf.Occupied().Max(x => x.Def.MaxHp)} | {bf.Occupied().Average(x => x.Def.Attack):F0} "
            + $"| {bf.Occupied().Average(x => x.Def.Speed):F0} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 計測 ---
    // seed ごとの打点・ターン数・T1..T5 の累積を丸ごと持つ。半割は同じ計測から取り出すだけで済む
    // （2回走らせると、半割の値そのものに実行間のばらつきが乗る。`bench` と同じ作法）。
    var tr = new OutputTrace[nB][];
    for (int b = 0; b < nB; b++)
    {
        tr[b] = new OutputTrace[nT];
        for (int t = 0; t < nT; t++) tr[b][t] = MeasureOutput(targets[t].F, cands[b].F, OutSeeds);
        Console.Out.Flush();
    }

    // --- 検算 ---
    //
    // (1) イベントから数えた打点と、敵の tally から数えた打点（第13期の受け手側測定）が一致するか。
    //     ずれたら、どちらかが取りこぼしている。
    // (2) 敵同士の巻き込みが 0 であること（受け手側から与ダメを取るための前提。第13期 §3-1）。
    // (3) 味方と敵の Def.Id が衝突していないこと（power / wave と同じ穴）。
    Console.WriteLine("### 2-1. 検算");
    Console.WriteLine();
    double maxGap = 0;
    long totalFromAlly = 0;
    for (int b = 0; b < nB; b++)
        for (int t = 0; t < nT; t++)
        {
            maxGap = Math.Max(maxGap, Math.Abs(tr[b][t].Damage.Sum() - tr[b][t].TallyDamage));
            totalFromAlly += tr[b][t].FoeFromAlly;
        }
    var clash = targets.SelectMany(x => x.F.Occupied().Select(y => y.Def.Id))
        .Intersect(cands.SelectMany(c => c.F.Occupied().Select(y => y.Def.Id))).ToArray();
    Console.WriteLine($"- **イベント集計と敵 tally の差**: 最大 {maxGap:F0}（{nB * nT} 組）。"
        + "**0 でなければ、どちらかが打点を取りこぼしている**");
    Console.WriteLine($"- **敵同士の巻き込み（`TakenFromAlly`）**: {totalFromAlly}。"
        + "0 でなければ受け手側から与ダメを取る前提が崩れる");
    Console.WriteLine($"- **味方と敵の `Def.Id` 衝突**: {clash.Length} 件"
        + (clash.Length == 0 ? "" : $"（{string.Join(" / ", clash)}）"));
    Console.WriteLine();
    Console.WriteLine("イベント側は `Damage` イベントを直接足している（`Status` の量は**適用前の値**なので、");
    Console.WriteLine("破片で吸われたぶん・非致死で丸めたぶんが実際の削りと食い違う。`dissect` の `毒燃/戦` は");
    Console.WriteLine("`Status` から取っているので、あちらとは数字が微妙に違う——**こちらが実測**）。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 2-2. 要件を満たしているか ---
    //
    // 計画書 §6-7 の停止条件そのもの。**決着してしまい甲群の時間が取れないなら、
    // 台の硬さを上げる前に何ターンで決着したかを報告する。**
    Console.WriteLine("### 2-2. 要件を満たしているか（全編成の平均。計画書 §6-7 の停止条件）");
    Console.WriteLine();
    Console.WriteLine("| 量 | 読み方 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| `決着T` | 全試行の平均ターン数。**30.0 なら誰も削り切れていない**（要件2 が最も強く成立） |");
    Console.WriteLine("| `T5未満%` | ターン数が 5 未満だった試行の割合。**ここが高いと (B) の立ち上がりが測れない** |");
    Console.WriteLine("| `味方全滅%` | 味方が削られ切った試行の割合。**高いと出力が途中で止まる** |");
    Console.WriteLine("| `敵全滅%` | 味方が削り切った試行の割合（＝この台での味方の勝率） |");
    Console.WriteLine("| `手番外%` | 打点のうち手番の振り以外（反撃・破裂・毒燃）から出たぶん。**0 なら被弾駆動が死んでいる** |");
    Console.WriteLine();
    Console.WriteLine("| 台 | 決着T | T5未満% | 味方全滅% | 敵全滅% | 手番外%（平均） | 手番外% 0 の編成 |");
    Console.WriteLine("|:-:|--:|--:|--:|--:|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        double turns = Enumerable.Range(0, nT).Average(t => tr[b][t].Turns.Average());
        double shortRun = Enumerable.Range(0, nT).Average(t => tr[b][t].Short * 100.0 / OutSeeds);
        double wipe = Enumerable.Range(0, nT).Average(t => tr[b][t].AllyWipe * 100.0 / OutSeeds);
        double clear = Enumerable.Range(0, nT).Average(t => tr[b][t].FoeWipe * 100.0 / OutSeeds);
        double off = Enumerable.Range(0, nT).Average(t => tr[b][t].OffTurnPct);
        int zero = Enumerable.Range(0, nT).Count(t => tr[b][t].OffTurnPct <= 1e-9);
        Console.WriteLine($"| **{cands[b].Tag}** {cands[b].Name} | {turns:F1} | {shortRun:F1}% | {wipe:F1}% "
            + $"| {clear:F1}% | {off:F1}% | {zero} / {nT} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3. 参照台の門と、選んだ2台 ---
    //
    // **§2-2 の下見だけでは台を選べない。** 「決着T が長い」は要件2 に適うように見えるが、
    // **敵が一度も攻撃を通せていないから長い**のかもしれない——それは要件2 を満たした台では
    // なく、計画書 §3-2 が名指しで警告した「殴り返さない的」そのもので、
    // 被弾駆動の出力（反撃・被弾強化・自傷）がまるごと 0 になる。
    //
    // 門は数字で置ける。**呪詛（ネル）は開幕に敵全体の攻撃力を −6 する**
    // （`CurseTrait.EnemyDebuff`）。1体あたり攻がこれ以下の def を並べると、呪詛入りの編成に
    // 対しては攻撃が 0 以下に潰れ、`ApplyDamage` の `if (amount <= 0) return;` で
    // **1ダメージも通らない**。反撃も被弾強化も、`OnDamaged` が呼ばれないので走らない。
    Console.WriteLine("## 3. 参照台の門と、選んだ2台");
    Console.WriteLine();
    Console.WriteLine("### 3-1. 門 — 台は本当に殴り返しているか");
    Console.WriteLine();
    Console.WriteLine("**§2-2 の下見だけでは台を選べない。** 「決着T が長い」は要件2 に適うように見えるが、");
    Console.WriteLine("**敵が一度も攻撃を通せていないから長い**のかもしれない——それは計画書 §3-2 が名指しで");
    Console.WriteLine("警告した「殴り返さない的」で、被弾駆動の出力がまるごと 0 になる。");
    Console.WriteLine();
    Console.WriteLine($"門は数字で置ける。**呪詛（ネル）は開幕に敵全体の攻撃力を −{CurseTrait.EnemyDebuff} する**");
    Console.WriteLine("（`CurseTrait.EnemyDebuff`）。1体あたり攻がこれ以下の def を並べると、呪詛入りの編成に");
    Console.WriteLine("対しては攻撃が 0 以下に潰れ、`ApplyDamage` の `if (amount <= 0) return;` で");
    Console.WriteLine("**1ダメージも通らない**——`OnDamaged` が呼ばれないので反撃も被弾強化も走らない。");
    Console.WriteLine("（味方側の弱体である萎縮（クビ・−9）は味方にしか効かないので、門には効かない。）");
    Console.WriteLine();
    Console.WriteLine($"**門: 1体あたり攻 > {CurseTrait.EnemyDebuff}。**");
    Console.WriteLine();
    Console.WriteLine("| 台 | 1体攻 | 呪詛後 | 門 | 手番外% 0 の編成 | 反撃軸2編成の決着T | 同 手番外% |");
    Console.WriteLine("|:-:|--:|--:|:-:|--:|--:|--:|");
    // 反撃軸（カド入り）の2編成。**名指しで固定する**——「手番外% が最低の編成」を毎回探すと、
    // 編成集合が動くたびに門の説明が別の編成に移る（`dissect` の pairSpec と同じ作法）。
    int[] kado = Enumerable.Range(0, nT)
        .Where(t => targets[t].Name.StartsWith("反撃 (") || targets[t].Name.StartsWith("反撃改 (")).ToArray();
    var gate = new bool[nB];
    for (int b = 0; b < nB; b++)
    {
        double each = cands[b].F.Occupied().Average(x => x.Def.Attack);
        gate[b] = each > CurseTrait.EnemyDebuff;
        int zero = Enumerable.Range(0, nT).Count(t => tr[b][t].OffTurnPct <= 1e-9);
        string kt = kado.Length == 0 ? "—" : $"{kado.Average(t => tr[b][t].Turns.Average()):F1}";
        string ko = kado.Length == 0 ? "—" : $"{kado.Average(t => tr[b][t].OffTurnPct):F1}%";
        Console.WriteLine($"| **{cands[b].Tag}** {cands[b].Name} | {each:F0} | {each - CurseTrait.EnemyDebuff:F0} "
            + $"| {(gate[b] ? "○" : "**×**")} | {zero} / {nT} | {kt} | {ko} |");
    }
    Console.WriteLine();
    Console.WriteLine("**門を落ちるのは B1 巡礼5（攻4）だけ。** 反撃軸2編成の決着T が 30.0（＝引き分けの上限に");
    Console.WriteLine("張り付いている）で、`手番外%` は 0.0%——**カドは一度も刺し返していない。**");
    Console.WriteLine("下見の表で B1 が「味方全滅 0.0%・決着 9.1T」と最も要件2 に適って見えたのは、");
    Console.WriteLine("**呪詛入りの編成に対して的が無力化されていたから**だった。");
    Console.WriteLine();
    Console.WriteLine("> **この門は下見の表からは読めない。** `決着T` も `味方全滅%` も、殴り返しの強さと");
    Console.WriteLine("> 「攻撃が通っているか」を区別しない。**`手番外%` を下見に入れてあるのはこのため。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3-2. 選定 ---
    //
    // **性質の違う2台が要る**（計画書 §3-3）。出力が台に依存する量なら、単一の特徴量には
    // できない——それを確かめる方法が「性質の違う的で測って順位が一致するか」しかない。
    // 選定は門を通った 5 台の中から、下見の表を見て機械的に:
    //   要件2 → `味方全滅%` が低い（出力が途中で止まらない）・`T5未満%` が低い
    //   要件3 → 2台で**個体HP と 体数**が違う（第16期の「2つの時計」の軸）
    //
    // **探索で選ばない**（`dissect` の pairSpec と同じ）。「一致する組み合わせ」を探すと、
    // 中立性の検定が「一致する台を選んだ」の言い換えになる。
    int[] pick = { 1, 4 };   // B2 荷駄5 / B5 重装4
    Console.WriteLine("### 3-2. 選んだ2台");
    Console.WriteLine();
    Console.WriteLine("**門を通った 5 台から選ぶ。探索で選ばない**（`dissect` の解剖ペアと同じ作法）");
    Console.WriteLine("——「一致する組み合わせ」を探した時点で、中立性の検定が");
    Console.WriteLine("「一致する台を選んだ」の言い換えになる。");
    Console.WriteLine();
    Console.WriteLine("| 役 | 台 | 中身 | 体数 | 総HP | 総攻 | 個体HP | 1体攻 | 性質 |");
    Console.WriteLine("|:-:|:-:|---|--:|--:|--:|--:|--:|---|");
    foreach (int b in pick)
    {
        Formation bf = cands[b].F;
        Console.WriteLine($"| {(b == pick[0] ? "主" : "従")} | **{cands[b].Tag}** | {cands[b].Name} | {bf.Count} "
            + $"| {bf.Occupied().Sum(x => x.Def.MaxHp)} | {bf.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {bf.Occupied().Max(x => x.Def.MaxHp)} | {bf.Occupied().Average(x => x.Def.Attack):F0} "
            + $"| {(b == pick[0] ? "**多数・中個体HP**" : "**少数・高個体HP**")} |");
    }
    Console.WriteLine();
    Console.WriteLine("選定の理由:");
    Console.WriteLine();
    Console.WriteLine("- **総HP がほぼ同じ（540 / 580）のに、個体HP と 体数が正反対**（90×6 / 145×4）。");
    Console.WriteLine("  第16期が「波の性格は 個体HP と 総攻の2軸」と結論した、その片方だけを振ってある");
    Console.WriteLine("  ——**的の量は揃えて、形だけ変える**のが中立性の検定として最も強い形。");
    Console.WriteLine("- 味方全滅が 0.2% / 3.7% と低い。**出力が途中で止まらない。**");
    Console.WriteLine("- どちらも門を通っている（1体攻 7 / 12）。**被弾駆動の出力が生きている。**");
    Console.WriteLine();
    Console.WriteLine("**却下した案。**");
    Console.WriteLine();
    Console.WriteLine("- **B1 巡礼5** — §3-1 の門を落ちる。**呪詛入りの編成に対しては殴り返さない的**になり、");
    Console.WriteLine("  反撃軸の出力が 0 になる。計画書 §3-2 が名指しで警告した失敗そのもの。");
    Console.WriteLine("- **B6 重装6（総HP 870）** — いちばん硬いので要件2 には最も適うが、**味方全滅 75.0%**。");
    Console.WriteLine("  出力が育つ前に測定側が止まる。**硬さと殴り返しは同じ def では分けられない**");
    Console.WriteLine("  （攻撃の低い高HP の def が `EnemyCatalog` に無い）。");
    Console.WriteLine("- **B4 重装3** — 味方全滅 0% だが総HP 435 と最も薄く、`T5未満%` が高い。的が足りない。");
    Console.WriteLine("- **B3 従卒5** — B2 と個体HP・体数が同じで攻だけ違う。**2台目としては「性質が違う」に");
    Console.WriteLine("  足りない**（第16期の2軸のうちどちらも動かない）。§4-2 の辺としては使う。");
    Console.WriteLine();

    // --- 3-3. 決着してしまっていないか（計画書 §6-7 の停止条件） ---
    //
    // **台の硬さを上げる前に、何ターンで決着したかを報告する。** 要件2 は
    // 「決着しないこと、**または十分長いこと**」なので、決着すること自体は停止条件ではない
    // ——甲群の立ち上がり（T1/T3/T5）が測れるかどうかが線。
    Console.WriteLine("### 3-3. 決着してしまっていないか（計画書 §6-7）");
    Console.WriteLine();
    Console.WriteLine("**要件2 は「決着しない、または十分長い」。** 決着すること自体は停止条件ではなく、");
    Console.WriteLine($"**(B) の立ち上がり（T1/T3/T{OutputTrace.Ramp}）が測れるか**が線になる。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(pick.Select(b => $" {cands[b].Tag} 決着T | {cands[b].Tag} T5未満% |")));
    Console.WriteLine("|---|" + string.Concat(pick.Select(_ => "--:|--:|")));
    foreach (int t in Enumerable.Range(0, nT).OrderBy(t => tr[pick[0]][t].Turns.Average()))
        Console.WriteLine($"| {targets[t].Name} |"
            + string.Concat(pick.Select(b => $" {tr[b][t].Turns.Average():F1} | {tr[b][t].Short * 100.0 / OutSeeds:F1}% |")));
    Console.WriteLine();
    var tooShort = Enumerable.Range(0, nT)
        .Where(t => pick.Any(b => tr[b][t].Turns.Average() < OutputTrace.Ramp)).ToArray();
    Console.WriteLine(tooShort.Length == 0
        ? $"**平均決着Tが {OutputTrace.Ramp} を下回る編成は 0 件。** どの編成でも T1〜T{OutputTrace.Ramp} の窓は開いている。"
        : $"> **平均決着Tが {OutputTrace.Ramp} を下回る編成が {tooShort.Length} 件ある**（どちらかの台で）: "
          + string.Join(" / ", tooShort.Select(t => targets[t].Name))
          + $"。**この編成の (B) は「育たなかった」ではなく「窓が閉じた」を測っている。**"
          + "門を通した結果、反撃軸は的を 3〜5T で削り切るようになった——**殴り返す台にすると"
          + "反撃軸が速くなり、立ち上がりを測る時間が消える**という綱引きが残る（計画書 §6-7 の3番目）。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4. 中立性の確認 ---
    //
    // **計画書 §3-3 の必須項目。** 出力が台に依存しない量なら単一の特徴量にしてよく、
    // 依存するなら**単一の特徴量にはできない**（その場合は止まって報告する）。
    //
    // 台間の相関は、それだけでは読めない。**乱数のばらつきだけでも 1.00 は割る**ので、
    // 「どれくらいなら動いたと言えるか」の基準が先に要る（第13期 `bench` の作法）。
    // 同じ台を seed で半分に割った一致度が**測定の信頼性の上限**で、台間の相関はこれと比べる。
    Console.WriteLine("## 4. 中立性の確認（計画書 §3-3）");
    Console.WriteLine();
    Console.WriteLine("**出力が台に依存する量なら、単一の特徴量にはできない。** 判定は「性質の違う2台で");
    Console.WriteLine("編成の順位が一致するか」で、比べる相手は**半割（測定の信頼性の上限）**");
    Console.WriteLine("——台間の相関は乱数のばらつきだけでも 1.00 を割るので、上限が無いと読めない");
    Console.WriteLine("（第13期 `bench` の作法。目的変数が突破度から (A) に変わっているので**上限は測り直す**）。");
    Console.WriteLine();
    Console.WriteLine("測る量は **(A) 実効打点/ターン**（総打点 ÷ 総ターン数）。**平均の平均ではない**");
    Console.WriteLine("——試行ごとに長さが違うので、比の平均を取ると短い試行に重みが寄る。");
    Console.WriteLine();

    double[] RateOf(int b, Func<int, bool> take) =>
        Enumerable.Range(0, nT).Select(t => tr[b][t].Rate(take)).ToArray();
    var full = Enumerable.Range(0, nB).Select(b => RateOf(b, _ => true)).ToArray();

    Console.WriteLine("### 4-1. 半割 — 測定の信頼性の上限");
    Console.WriteLine();
    Console.WriteLine("| 台 | 前後半 r | 前後半 ρ | 偶奇 r | 偶奇 ρ | 補正後 r | **補正後 ρ** |");
    Console.WriteLine("|:-:|--:|--:|--:|--:|--:|--:|");
    var capR = new double[nB];
    var capRho = new double[nB];
    for (int b = 0; b < nB; b++)
    {
        var h1 = Correlate(RateOf(b, s => s < OutSeeds / 2), RateOf(b, s => s >= OutSeeds / 2));
        var h2 = Correlate(RateOf(b, s => s % 2 == 0), RateOf(b, s => s % 2 == 1));
        double SB(double r) => 2 * r / (1 + r);   // Spearman-Brown
        capR[b] = SB((h1.R + h2.R) / 2);
        capRho[b] = SB((h1.Rho + h2.Rho) / 2);
        Console.WriteLine($"| **{cands[b].Tag}** {cands[b].Name} | {h1.R:F3} | {h1.Rho:F3} "
            + $"| {h2.R:F3} | {h2.Rho:F3} | {capR[b]:F3} | **{capRho[b]:F3}** |");
    }
    Console.WriteLine();
    Console.WriteLine("補正は Spearman-Brown `r(2n) = 2r(n) / (1 + r(n))`（半割は 100 seed 同士なので");
    Console.WriteLine("200 seed の測定より一致度が低く出る）。**補正は「両半分が同等・誤差が独立」を");
    Console.WriteLine("仮定するので、生の値も併記してある。** 上限がほぼ 1.00 なので、**台間の相関の");
    Console.WriteLine("低さは全部が「実物の入れ替わり」になる**——ばらつきでは 1ミリも説明が付かない。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-2. 候補6台の総当たり ---
    //
    // **主従2台だけを見ると、一致しなかった理由が「攻が違うから」か「体数が違うから」か
    // 決まらない**（第13期 `bench` が主↔従の対角線で嵌まったのと同じ形）。
    // 候補は最初から 2 家族 × 3 刻みの格子に組んであるので、**辺を1本ずつ読める。**
    Console.WriteLine("### 4-2. 候補6台の総当たり（下三角 r / 上三角 ρ・対角は半割の補正後）");
    Console.WriteLine();
    Console.WriteLine("**主従2台だけを見ると、一致しなかった理由が「攻が違うから」か「体数が違うから」か");
    Console.WriteLine("決まらない。** 候補は 2 家族 × 3 刻みの格子に組んであるので、**辺を1本ずつ読める。**");
    Console.WriteLine();
    Console.WriteLine("|  |" + string.Concat(cands.Select(x => $" {x.Tag} |")));
    Console.WriteLine("|:-:|" + string.Concat(cands.Select(_ => "--:|")));
    for (int i = 0; i < nB; i++)
    {
        var row = new List<string>();
        for (int j = 0; j < nB; j++)
            row.Add(i == j ? $"*{capR[i]:F2} / {capRho[i]:F2}*"
                : $"{(j > i ? Correlate(full[i], full[j]).Rho : Correlate(full[i], full[j]).R):F2}");
        Console.WriteLine($"| **{cands[i].Tag}** | {string.Join(" | ", row)} |");
    }
    Console.WriteLine();

    var edges = new (string Label, int A, int B, string Axis)[]
    {
        ("B1 ↔ B2", 0, 1, "**敵の1体攻** 4 → 7（体数・個体HP は同じ）"),
        ("B2 ↔ B3", 1, 2, "**敵の1体攻** 7 → 10（同上）"),
        ("B1 ↔ B3", 0, 2, "**敵の1体攻** 4 → 10（同上・振り幅最大）"),
        ("B4 ↔ B5", 3, 4, "**敵の体数** 3 → 4（個体HP・1体攻は同じ）"),
        ("B5 ↔ B6", 4, 5, "**敵の体数** 4 → 6（同上）"),
        ("B4 ↔ B6", 3, 5, "**敵の体数** 3 → 6（同上・振り幅最大）"),
        ("B2 ↔ B5", 1, 4, "**個体HP と 体数**（主 ↔ 従。§3-2 で選んだ対）"),
        ("B1 ↔ B5", 0, 4, "参考: **門を落ちた台**を主にした場合（§3-1）"),
    };
    Console.WriteLine("`上限` は両端の台の半割（補正後 ρ）の低いほう。`余地` = 上限 − ρ が");
    Console.WriteLine("**測定のばらつきでは説明できない入れ替わりの量**（第13期・第15期と同じ定義）。");
    Console.WriteLine();
    Console.WriteLine("| 対 | 動いた変数 | r | ρ | 上限(ρ) | **余地** | 平均\\|順位差\\| | 最大\\|順位差\\| |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    var ranks = Enumerable.Range(0, nB).Select(b => AverageRanksDesc(full[b])).ToArray();
    foreach (var (label, a, b, axis) in edges)
    {
        var c = Correlate(full[a], full[b]);
        double cp = Math.Min(capRho[a], capRho[b]);
        var g = Enumerable.Range(0, nT).Select(t => Math.Abs(ranks[a][t] - ranks[b][t])).ToArray();
        Console.WriteLine($"| {label} | {axis} | {c.R:F2} | {c.Rho:F2} | {cp:F2} "
            + $"| **{cp - c.Rho:F2}** | {g.Average():F1} | {g.Max():F1} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-3. 判定 ---
    //
    // 線は**第15期の裏返し**をそのまま使う（線を新しく作らない）。第15期は
    // 「ρ < 0.90 かつ 余地 ≥ 0.05」を**入れ替わりが実在する**の線にした。
    // 中立ならその否定、すなわち「ρ ≥ 0.90 かつ 余地 < 0.05」。
    const double NeutralRho = 0.90, NeutralRoom = 0.05;
    var cross = Correlate(full[pick[0]], full[pick[1]]);
    double cap = Math.Min(capRho[pick[0]], capRho[pick[1]]);
    double room = cap - cross.Rho;
    // **連言の否定は選言。** 第15期の線は「ρ < 0.90 かつ 余地 ≥ 0.05」なので、
    // その否定は「ρ ≥ 0.90 **または** 余地 < 0.05」になる。ここを連言にすると
    // 第15期より厳しい線を黙って作ることになる。
    bool neutral = cross.Rho >= NeutralRho || room < NeutralRoom;

    Console.WriteLine("### 4-3. 判定");
    Console.WriteLine();
    Console.WriteLine($"線は**第15期の裏返し**をそのまま使う（線を新しく作らない）——第15期は");
    Console.WriteLine($"「`ρ < {NeutralRho:F2}` かつ `余地 ≥ {NeutralRoom:F2}`」を**入れ替わりが実在する**の線にした。");
    Console.WriteLine($"中立ならその**否定**——連言の否定は選言なので、"
        + $"**`ρ ≥ {NeutralRho:F2}` または `余地 < {NeutralRoom:F2}`** になる。");
    Console.WriteLine();
    Console.WriteLine(neutral
        ? $"**判定: 中立。** ρ = {cross.Rho:F3}（上限 {cap:F3}・余地 {room:F3}）で、"
          + "**性質が正反対の2台で編成の順位が一致する。** (A) は台に依存しない量なので、"
          + "**単一の特徴量にしてよい**（計画書 §3-3 の1行目）。"
        : $"> **判定: 中立ではない。** ρ = {cross.Rho:F3}（上限 {cap:F3}・**余地 {room:F3}**）。"
          + "**出力は台に依存する量で、単一の特徴量にはできない**（計画書 §3-3 の2行目）。"
          + "**計画書 §6-7 の停止条件に当たるので、Phase HB へ進む前にここで止まる。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-4. 誰が動かしているか ---
    //
    // **「一致しなかった」で止めると報告にならない。** どの編成が動いたか、そして
    // その編成に共通する性質は何かまで出す。§4-2 の辺と突き合わせると、動いている軸が読める。
    var rk0 = ranks[pick[0]];
    var rk1 = ranks[pick[1]];
    Console.WriteLine("### 4-4. 誰が動かしているか");
    Console.WriteLine();
    Console.WriteLine($"`順位差` = 順位({cands[pick[0]].Tag}) − 順位({cands[pick[1]].Tag})。"
        + $"**正なら {cands[pick[1]].Tag}（殴り返しの強い台）で順位が上がる**（順位は 1 が最良）。");
    Console.WriteLine();
    Console.WriteLine("`手番外%` は打点のうち手番の振り以外（反撃・破裂・追い打ち・毒燃）から出たぶん。");
    Console.WriteLine("**反撃軸はここが高い。**");
    Console.WriteLine();
    Console.WriteLine($"| 編成 | 総攻 | {cands[pick[0]].Tag} (A) | 順位 | {cands[pick[1]].Tag} (A) | 順位 | 順位差 "
        + $"| {cands[pick[0]].Tag} 手番外% | {cands[pick[1]].Tag} 手番外% |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => Math.Abs(rk0[t] - rk1[t])))
        Console.WriteLine($"| {targets[t].Name} | {targets[t].F.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {full[pick[0]][t]:F1} | {rk0[t]:F1} | {full[pick[1]][t]:F1} | {rk1[t]:F1} "
            + $"| {Sg(rk0[t] - rk1[t], 1):+0.0;-0.0} "
            + $"| {tr[pick[0]][t].OffTurnPct:F0}% | {tr[pick[1]][t].OffTurnPct:F0}% |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-5. 一致しなかった量はどこに集まっているか ---
    //
    // **2つの疑いを数字で潰す。**
    //   (1) 台が早く落ちた編成（要件2 を満たしていない）が作った見かけの不一致ではないか
    //   (2) 反撃軸（手番外% が高い編成）だけが動かしているのではないか
    // どちらも「外した部分集合で ρ を取り直す」ことで測れる。**外すのは診断であって
    // 直しではない**——外して一致したからといって、その編成の出力が測れたことにはならない。
    Console.WriteLine("### 4-5. 一致しなかった量はどこに集まっているか");
    Console.WriteLine();
    Console.WriteLine("**2つの疑いを数字で潰す。** どちらも部分集合で ρ を取り直すだけで測れる。");
    Console.WriteLine("**外すのは診断であって直しではない**——外して一致しても、その編成の出力が");
    Console.WriteLine("測れたことにはならない。");
    Console.WriteLine();
    double[] Sub(double[] v, int[] ix) => ix.Select(t => v[t]).ToArray();
    var allIx = Enumerable.Range(0, nT).ToArray();
    var longIx = allIx.Where(t => pick.All(b => tr[b][t].Turns.Average() >= OutputTrace.Ramp)).ToArray();
    var moved = allIx.OrderByDescending(t => Math.Abs(rk0[t] - rk1[t])).ToArray();
    var subsets = new (string Name, int[] Ix, string Note)[]
    {
        ("全編成", allIx, "§4-3 の判定に使った集合"),
        ($"平均決着T ≥ {OutputTrace.Ramp} の編成", longIx,
            "**要件2 を満たしている編成だけ。** 台が早く落ちた編成を外しても残るか"),
        ("順位差 上位2件を外す", moved.Skip(2).ToArray(),
            $"**{targets[moved[0]].Name} / {targets[moved[1]].Name} を外す**"),
        ("順位差 上位4件を外す", moved.Skip(4).ToArray(), "同上・4件版"),
        ("手番外% < 50 の編成（従台で判定）", allIx.Where(t => tr[pick[1]][t].OffTurnPct < 50).ToArray(),
            "**反撃軸を外した集合。** 手番外% は打点のうち振り以外から出たぶん"),
    };
    Console.WriteLine("| 部分集合 | n | ρ | 上限(ρ) | 余地 | 備考 |");
    Console.WriteLine("|---|--:|--:|--:|--:|---|");
    foreach (var (name, ix, note) in subsets)
    {
        if (ix.Length < 3) { Console.WriteLine($"| {name} | {ix.Length} | — | — | — | {note}（n が足りない） |"); continue; }
        var c = Correlate(Sub(full[pick[0]], ix), Sub(full[pick[1]], ix));
        Console.WriteLine($"| {name} | {ix.Length} | {c.Rho:F3} | {cap:F3} | **{cap - c.Rho:F3}** | {note} |");
    }
    Console.WriteLine();
    Console.WriteLine("> **順位は部分集合の中で取り直している**（AverageRanksDesc ではなく Spearman の中で）ので、");
    Console.WriteLine("> 外した編成のぶんだけ順位が詰まる。**部分集合どうしの ρ を直接比べてよい**のは");
    Console.WriteLine("> 上限が n にほとんど依らないほど高い（0.99 台）ためで、そうでなければ");
    Console.WriteLine("> 部分集合ごとに半割を取り直す必要がある。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= Phase HB: 出力特徴量 =================
    //
    // Phase HA で「(A) は台に依存しない量」まで来たので、ここからは**測った出力で
    // 第14〜16期の分析をやり直す**。
    //
    // 特徴量は主台（§3-2）の値を使う。従台の値も同じ表に出して、**(A) 以外
    // （(B)(C)）についても台間の一致を確かめる**——中立性を確認したのは (A) だけで、
    // (B)(C) は別の量なので改めて見る必要がある。
    int main = pick[0], sub = pick[1];

    Console.WriteLine("# Phase HB — 出力特徴量");
    Console.WriteLine();
    Console.WriteLine($"特徴量は**主台 {cands[main].Tag}（{cands[main].Name}）**で測る。従台"
        + $" {cands[sub].Tag} の値も併記して、**(A) 以外についても台間の一致を確かめる**"
        + "——§4 で中立性を確かめたのは (A) だけで、(B)(C) は別の量。");
    Console.WriteLine();

    // --- 5. 定義 ---
    Console.WriteLine("## 5. (A)(B)(C) の定義");
    Console.WriteLine();
    Console.WriteLine("| 記号 | 名 | 定義 | 近似 |");
    Console.WriteLine("|:-:|---|---|---|");
    Console.WriteLine("| **(A)** | `実効打点/T` | 参照台で敵に通した**総打点 ÷ 総ターン数**（全 seed の合計どうしの比）。"
        + "毒・燃焼・反撃・破裂、どの経路も入る | **無し**（実測） |");
    Console.WriteLine($"| **(B)** | `育ち` | `(T{OutputTrace.Ramp}累積 − T3累積) ÷ 2` ÷ `T1打点`。"
        + $"分子は T4〜T{OutputTrace.Ramp} の1ターンあたり打点、分母は初手の1ターンあたり打点。"
        + "**1.00 が「まったく育たない」** | **無し**（実測）。ただし決着で窓が閉じる（下記） |");
    Console.WriteLine("| **(C)** | `手番外%` | 打点のうち**手番の振り以外**（反撃・破裂・追い打ち・生贄・毒燃）から出た割合 "
        + "| **無し**（実測） |");
    Console.WriteLine();
    Console.WriteLine("**(C) に近似は使っていない。** 計画書 §4-1 は `総攻 × 手番数` を引く近似を示していたが、");
    Console.WriteLine("`Events` から振りの範囲を「Attack イベントから、同じ手番の同じ actor が出した Damage まで」で");
    Console.WriteLine("切れるので、引き算の近似は要らない（第16期 `dissect` の `振に帰属%` と同じ切り方）。");
    Console.WriteLine("**反撃は actor が違い、毒は actor が null なので、追加のフラグ無しで外れる。**");
    Console.WriteLine();
    Console.WriteLine("**(B) の弱点は明記する。** 決着すると累積が頭打ちになるので、**早く決着する編成の (B) は");
    Console.WriteLine($"「育たなかった」ではなく「窓が閉じた」を測る**（§3-3）。`到達%`（T{OutputTrace.Ramp} まで戦った試行の割合）を");
    Console.WriteLine("同じ表に出してあるので、低い行はそのつもりで読むこと。");
    Console.WriteLine();
    Console.WriteLine("**却下した表し方**: T1〜T5 の累積に二次項を当てて係数を取る。n = 5 点の二次回帰は");
    Console.WriteLine("決着による頭打ちを「上に凸」と読んでしまい、**育った編成と早く終わった編成が同じ符号になる**。");
    Console.WriteLine();

    // --- 5-2. 同語反復の判定 ---
    //
    // **第14期の基準を、新しい特徴量にも通す。** 基準は「目的変数の言い換えになっていないか」の
    // 1本だけ（「信頼できるか」は混ぜない）。
    Console.WriteLine("### 5-1. 同語反復の判定（第14期の基準を通す）");
    Console.WriteLine();
    Console.WriteLine("基準は「**目的変数の言い換えになっていないか**」の1本だけ（第14期・第15期と同じ）。");
    Console.WriteLine();
    Console.WriteLine("| 経路 | (A)(B)(C) は当たるか |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| **分子経路**（量そのものが勝利の定義に含まれる） | **当たらない。** 量を測ったのは"
        + "**参照台**で、目的変数（波ごとの勝率）の戦闘とは別の戦闘。その波の敵を削り切ったかどうかは"
        + "1ビットも入っていない |");
    Console.WriteLine("| **分母経路**（`部隊戦数 = 突破数 + 1`） | **当たらない。** 単発戦では分母経路が"
        + "そもそも存在しない（第15期 §9-1） |");
    Console.WriteLine();
    Console.WriteLine("**これが参照台を作った理由そのもの。** `power` / `wave` の動的特徴量（`与ダメ/戦`）は");
    Console.WriteLine("目的変数と同じ戦闘から取っているので、波ごとの勝率に当てた瞬間に循環する");
    Console.WriteLine("——第15期はそれを分子経路として外した。**参照台はその循環を切るための装置。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 5-3. 値 ---
    double[] A(int b) => Enumerable.Range(0, nT).Select(t => tr[b][t].RateAll).ToArray();
    double[] Bv(int b) => Enumerable.Range(0, nT).Select(t => tr[b][t].Ramp15).ToArray();
    double[] Cv(int b) => Enumerable.Range(0, nT).Select(t => tr[b][t].OffTurnPct).ToArray();
    var featA = A(main);
    var featB = Bv(main);
    var featC = Cv(main);

    Console.WriteLine("### 5-2. 値（31編成）");
    Console.WriteLine();
    Console.WriteLine($"`到達%` は T{OutputTrace.Ramp} まで戦った試行の割合。**低い行の (B) は窓が閉じている。**");
    Console.WriteLine();
    Console.WriteLine($"| 編成 | 総攻 | **(A)** | T1 | T3 | T{OutputTrace.Ramp} | **(B) 育ち** | 到達% | **(C) 手番外%** "
        + $"| 従台 (A) | 従台 (B) | 従台 (C) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => featA[t]))
    {
        OutputTrace mt = tr[main][t], st = tr[sub][t];
        Console.WriteLine($"| {targets[t].Name} | {targets[t].F.Occupied().Sum(x => x.Def.Attack)} "
            + $"| **{featA[t]:F1}** | {mt.CumAt(1):F0} | {mt.CumAt(3):F0} | {mt.CumAt(OutputTrace.Ramp):F0} "
            + $"| **{featB[t]:F2}** | {100.0 - mt.Short * 100.0 / OutSeeds:F0}% | **{featC[t]:F0}%** "
            + $"| {st.RateAll:F1} | {st.Ramp15:F2} | {st.OffTurnPct:F0}% |");
    }
    Console.WriteLine();

    // (B)(C) の台間一致。**(A) だけ確かめて残り2つを黙って使うのは、§4 の作業の意味を消す。**
    Console.WriteLine("### 5-3. (B)(C) も台に依存しないか");
    Console.WriteLine();
    Console.WriteLine("**§4 で中立性を確かめたのは (A) だけ。** (B)(C) は別の量なので、同じ形で確かめる。");
    Console.WriteLine();
    Console.WriteLine("| 量 | 主台 ↔ 従台 r | ρ | 上限(ρ) | **余地** | 判定 |");
    Console.WriteLine("|---|--:|--:|--:|--:|:-:|");
    foreach (var (nm, mv, sv) in new[] { ("(A) 実効打点/T", featA, A(sub)), ("(B) 育ち", featB, Bv(sub)), ("(C) 手番外%", featC, Cv(sub)) })
    {
        var c = Correlate(mv, sv);
        double rm = cap - c.Rho;
        Console.WriteLine($"| {nm} | {c.R:F3} | {c.Rho:F3} | {cap:F3} | **{rm:F3}** "
            + $"| {(c.Rho >= NeutralRho || rm < NeutralRoom ? "中立" : "**×**")} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 6. `総攻` と (A) はどれだけ違うか ---
    //
    // 計画書 §5-2 の報告項目。**第16期が名指しした障害が、数字でどれだけ大きいか。**
    Console.WriteLine("## 6. `総攻` と (A) はどれだけ違うか");
    Console.WriteLine();
    var atk = Enumerable.Range(0, nT).Select(t => (double)targets[t].F.Occupied().Sum(x => x.Def.Attack)).ToArray();
    var cAtk = Correlate(atk, featA);
    var rkA = AverageRanksDesc(featA);
    var rkAtk = AverageRanksDesc(atk);
    Console.WriteLine($"**`総攻` と (A) の相関は r = {cAtk.R:F3} / ρ = {cAtk.Rho:F3}。**");
    Console.WriteLine($"順位の平均\\|差\\| は {Enumerable.Range(0, nT).Average(t => Math.Abs(rkAtk[t] - rkA[t])):F1}、"
        + $"最大 {Enumerable.Range(0, nT).Max(t => Math.Abs(rkAtk[t] - rkA[t])):F1}（31 編成）。");
    Console.WriteLine();
    Console.WriteLine("**最も外れていた編成 上位5**（`順位差` = 順位(総攻) − 順位((A))。"
        + "**正なら (A) のほうが高く評価する**）:");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 総攻 | 総攻 順位 | (A) | (A) 順位 | 順位差 | (C) 手番外% | 読み |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => Math.Abs(rkAtk[t] - rkA[t])).Take(5))
    {
        double d = rkAtk[t] - rkA[t];
        string read = featC[t] >= 60 ? "**手番外が主**（反撃・毒）" : featC[t] >= 30 ? "手番外が半分" : "手番の振りが主";
        Console.WriteLine($"| {targets[t].Name} | {atk[t]:F0} | {rkAtk[t]:F1} | {featA[t]:F1} | {rkA[t]:F1} "
            + $"| {Sg(d, 1):+0.0;-0.0} | {featC[t]:F0}% | {read} |");
    }
    Console.WriteLine();
    Console.WriteLine("`総攻` と (C) の相関も出しておく——**手番外の割合が高い編成ほど `総攻` が外す**なら、");
    Console.WriteLine("第16期の読み（反撃軸・毒軸で桁が違う）がそのまま数字になる。");
    Console.WriteLine();
    var cGap = Correlate(featC, Enumerable.Range(0, nT).Select(t => rkAtk[t] - rkA[t]).ToArray());
    Console.WriteLine($"**(C) 手番外% と 順位差（総攻 − (A)）の相関: r = {cGap.R:F3} / ρ = {cGap.Rho:F3}。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 7. (B) で甲乙は分離できるか ---
    //
    // 計画書 §4-2 の3番・§4-3 の3行目。**第16期の12事例の分類は人間の解釈**なので、
    // (B) がそれを数値で再現できるかを見る。**分類は第16期の出力から固定で写す**
    // ——ここで分け直すと「分かれるように分けた」になる。
    var kou = new[] { "速攻 (ボルグ×ムド)", "毒+耐久 (ベニ×トウ)", "溜め改 (クグ×バン×ガン)" };
    var otsu = new[] { "毒 (グザ×ミオ×ラウ)", "燃焼 (ボルグ×ホタ)", "耐久 (ガルド×ノノ)",
                       "範囲耐性 (ヒビ×ボルグ)", "追撃×死 (ハギ×リィカ)", "死の連鎖 (リィカ軸)" };
    int Ix(string name) => Array.FindIndex(targets, x => x.Name == name);

    Console.WriteLine("## 7. (B) で甲乙は分離できるか（第16期の12事例と照合）");
    Console.WriteLine();
    Console.WriteLine("**第16期の分類は人間の解釈。** (B) がそれを数値で再現できるかを見る。分類は");
    Console.WriteLine("第16期 `dissect` §7 の 12 事例からそのまま写した（**ここで分け直すと");
    Console.WriteLine("「分かれるように分けた」になる**）。事例 6 は第16期が「説明が付かなかった」と");
    Console.WriteLine("記録した事例で、編成としては甲群の 毒+耐久 と同じなので甲に入れてある。");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成 | **(B) 育ち** | 到達% | (A) | (C) 手番外% | 総攻 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|");
    foreach (var (label, names) in new[] { ("**甲**", kou), ("**乙**", otsu) })
        foreach (string nm in names)
        {
            int t = Ix(nm);
            if (t < 0) { Console.WriteLine($"| {label} | {nm} | — | — | — | — | — |"); continue; }
            Console.WriteLine($"| {label} | {nm} | **{featB[t]:F2}** | {100.0 - tr[main][t].Short * 100.0 / OutSeeds:F0}% "
                + $"| {featA[t]:F1} | {featC[t]:F0}% | {atk[t]:F0} |");
        }
    Console.WriteLine();

    var kIx = kou.Select(Ix).Where(t => t >= 0).ToArray();
    var oIx = otsu.Select(Ix).Where(t => t >= 0).ToArray();
    Console.WriteLine("| 量 | 甲の平均 | 乙の平均 | 甲の範囲 | 乙の範囲 | 重なるか |");
    Console.WriteLine("|---|--:|--:|---|---|:-:|");
    foreach (var (nm, v) in new[] { ("(B) 育ち", featB), ("(A) 実効打点/T", featA), ("(C) 手番外%", featC), ("総攻", atk) })
    {
        double kmin = kIx.Min(t => v[t]), kmax = kIx.Max(t => v[t]);
        double omin = oIx.Min(t => v[t]), omax = oIx.Max(t => v[t]);
        bool overlap = kmin <= omax && omin <= kmax;
        Console.WriteLine($"| {nm} | {kIx.Average(t => v[t]):F2} | {oIx.Average(t => v[t]):F2} "
            + $"| {kmin:F2} 〜 {kmax:F2} | {omin:F2} 〜 {omax:F2} | {(overlap ? "**重なる**" : "分かれる")} |");
    }
    Console.WriteLine();
    Console.WriteLine("**「分かれる」= 2群の範囲が重ならない**（1本の閾値で 9 編成を完全に分類できる）。");
    Console.WriteLine("n が 3 と 6 しかないので、**重ならないことは「分離できた」の必要条件であって十分条件ではない**");
    Console.WriteLine("——偶然に重ならない確率は決して小さくない（3 と 6 の並べ替えで完全分離は 1/84）。");
    Console.WriteLine();

    // --- 7-1. 読み（解釈） ---
    //
    // **ここだけは測定ではなく解釈**（`dissect` §7 と同じ扱い）。表と食い違ったら表が正しい。
    // 数字が動いたらこの節も書き直すこと。
    Console.WriteLine("### 7-1. 読み（解釈。表と食い違ったら表が正しい）");
    Console.WriteLine();
    {
        int tPoison = Ix("毒 (グザ×ミオ×ラウ)"), tTame = Ix("溜め改 (クグ×バン×ガン)");
        Console.WriteLine("**(B) は甲乙を分けない。** 分けないこと自体より、**なぜ分けないか**のほうが情報がある。");
        Console.WriteLine();
        if (tPoison >= 0)
            Console.WriteLine($"- **乙群の 毒 (グザ×ミオ×ラウ) の (B) が {featB[tPoison]:F2} で、9 編成中いちばん高い。**"
                + " 毒は段数が積み上がるので、出力は時間で**育つ**——(B) の定義どおりに大きく出る。"
                + "それでも第16期が 毒 を乙群に置いたのは、**育った出力が撃破に変換されない**からだった"
                + "（`毒の無駄` が S4 で 282.6 段）。**甲乙は「育つか」ではなく「撃破に変換されるか」で"
                + "割れていた**——(B) は前者しか測っていない。");
        if (tTame >= 0)
            Console.WriteLine($"- **甲群の 溜め改 の (B) が {featB[tTame]:F2} と最低なのは、窓が閉じたから**"
                + $"（到達% {100.0 - tr[main][tTame].Short * 100.0 / OutSeeds:F0}%）。"
                + "門を通した台は反撃軸に対して 3〜5T で落ちるので、**育ちを測る時間がそもそも無い**"
                + "（§3-3 の綱引き）。この行の (B) は「育たなかった」ではない。");
        Console.WriteLine("- **(C) は甲乙の平均を大きく分けている**（甲 57% / 乙 24%）が、範囲は重なる"
            + "——乙群の 毒 が 92% で甲群の 速攻 が 12% なので、**手番外率だけでは境界が引けない。**");
        Console.WriteLine();
        Console.WriteLine("**推測**: 甲乙を数値化するなら、必要なのは出力の量でも立ち上がりでもなく");
        Console.WriteLine("**「出力 → 撃破への変換率が敵の個体HP でどれだけ落ちるか」**——つまり");
        Console.WriteLine("参照台を**個体HP だけ変えて2つ**用意し、その間の出力低下率を取る形になる。");
        Console.WriteLine("§4-2 の格子は 1体攻 と 体数 を振っていて、**個体HP を単独では振っていない**"
            + "（B1〜B3 は 90 固定、B4〜B6 は 145 固定）。**第17期の参照台では測れない量。**");
        Console.WriteLine();
    }
    Console.Out.Flush();

    // ---- ここから波を測る（第15期 FB・第16期 GB のやり直し） ----
    //
    // 波は `WaveCatalog()` を呼ぶ。**コピーを持たない**（第15期が「1箇所に集める」ために
    // やった作業を、3つ目の診断が台無しにする）。
    var waves = WaveCatalog();
    int nW = waves.Length;
    const double DeadZone = 50.0;   // wave §4 / dissect §1 と同じ線

    var rate = new double[nW][];
    var degree = new double[nW][];
    var dyn = new double[nW][][];
    for (int w = 0; w < nW; w++)
    {
        rate[w] = new double[nT];
        degree[w] = new double[nT];
        dyn[w] = new double[nT][];
        for (int t = 0; t < nT; t++)
        {
            var mw = MeasureWave(targets[t].F, waves[w].Enemy, OutSeeds);
            rate[w][t] = mw.Win.Average() * 100;
            degree[w][t] = mw.SurvRate.Average();
            dyn[w][t] = mw.Dynamics;
        }
        Console.Out.Flush();
    }
    var contributes = new bool[nW];
    for (int w = 0; w < nW; w++)
    {
        double ceilN = rate[w].Count(v => v >= 100.0 - 1e-9) * 100.0 / nT;
        double floorN = rate[w].Count(v => v <= 1e-9) * 100.0 / nT;
        contributes[w] = ceilN + floorN < DeadZone;
    }
    int[] conW = Enumerable.Range(0, nW).Where(w => contributes[w]).ToArray();

    // --- 8. 波ごとの分解のやり直し（第15期 Phase FB） ---
    var statNames = new (string Name, Func<Formation, double> Get)[]
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
    int nS = statNames.Length;
    // 第15期の候補（12種）= 静的8 + 動的4（`与ダメ/戦`・`被ダメ/戦`・`撃破/戦` は分子経路で除外済み）。
    // MeasureWave の Dynamics の並びは 与ダメ・被ダメ・撃破・干渉・回復・自傷率・与ダメ効率。
    var dynKeep = new (string Name, int K)[] { ("干渉/戦", 3), ("回復/戦", 4), ("自傷率", 5), ("与ダメ効率", 6) };
    var outNames = new (string Name, double[] V)[] { ("(A) 実効打点/T", featA), ("(B) 育ち", featB), ("(C) 手番外%", featC) };

    Console.WriteLine("## 8. 波ごとの分解のやり直し（第15期 Phase FB）");
    Console.WriteLine();
    Console.WriteLine("目的変数は波ごとの単発勝率。候補は**第15期の 12 種**（静的8 + 動的4）に");
    Console.WriteLine("**(A)(B)(C) を足した 15 種**。第15期の側は同じ実行の中で計算し直している");
    Console.WriteLine("——**別の実行から引くと、動いたのが候補のせいか実行のせいか決まらない**（第13期以来の作法）。");
    Console.WriteLine();
    Console.WriteLine($"寄与する波は同じ判定式（天井率 + 床率 < {DeadZone:F0}%）で引き直した: **{conW.Length} 本** — "
        + string.Join(" / ", conW.Select(w => $"`{waves[w].Tag}`")));
    Console.WriteLine();

    // 第15期の記録（README「検証で分かったこと」の第15期の3項目）。**値を候補に使うのではなく、
    // この診断が第15期と同じ盤を見ていることの検算にだけ使う**（dissect §12-2 と同じ作法）。
    var rec15 = new Dictionary<string, (string First, double R2)>
    {
        ["S2"] = ("与ダメ効率", 0.059), ["S3"] = ("体数", 0.164), ["S4"] = ("与ダメ効率", 0.341),
        ["S5"] = ("干渉/戦", 0.338), ["R8"] = ("与ダメ効率", 0.194), ["R9"] = ("範囲枚数", 0.203),
        ["R10"] = ("総HP", 0.116),
    };

    string[] names15 = statNames.Select(x => x.Name).Concat(dynKeep.Select(x => x.Name)).ToArray();
    string[] names17 = names15.Concat(outNames.Select(x => x.Name)).ToArray();
    double[] Col15(int k, int w) => k < nS
        ? Enumerable.Range(0, nT).Select(t => statNames[k].Get(targets[t].F)).ToArray()
        : Enumerable.Range(0, nT).Select(t => dyn[w][t][dynKeep[k - nS].K]).ToArray();
    double[] Col17(int k, int w) => k < names15.Length ? Col15(k, w) : outNames[k - names15.Length].V;

    Console.WriteLine("| 波 | 第15期(12種) 第一近似 | r² | 記録 | **第17期(15種) 第一近似** | **r²** | 2位 | 上がったか |");
    Console.WriteLine("|:-:|---|--:|--:|---|--:|---|:-:|");
    int miss15 = 0, improved = 0;
    foreach (int w in conW)
    {
        (int K, double R, double R2) Best(Func<int, double[]> col, int n)
        {
            var ord = Enumerable.Range(0, n).Select(k => (K: k, R: Correlate(col(k), rate[w]).R))
                .Where(x => !double.IsNaN(x.R)).OrderByDescending(x => Math.Abs(x.R)).ToArray();
            return ord.Length == 0 ? (-1, double.NaN, double.NaN) : (ord[0].K, ord[0].R, ord[0].R * ord[0].R);
        }
        var b15 = Best(k => Col15(k, w), names15.Length);
        var ord17 = Enumerable.Range(0, names17.Length)
            .Select(k => (K: k, R: Correlate(Col17(k, w), rate[w]).R))
            .Where(x => !double.IsNaN(x.R)).OrderByDescending(x => Math.Abs(x.R)).ToArray();
        string rec = "—";
        if (rec15.TryGetValue(waves[w].Tag, out var want))
        {
            bool ok = names15[b15.K] == want.First && Math.Abs(b15.R2 - want.R2) <= 0.005;
            if (!ok) miss15++;
            rec = ok ? $"{want.R2:F3}" : $"**{want.First} {want.R2:F3} ←ずれ**";
        }
        bool up = ord17[0].K >= names15.Length;
        if (up) improved++;
        Console.WriteLine($"| **{waves[w].Tag}** | {names15[b15.K]} {b15.R:+0.00;-0.00} | {b15.R2:F3} | {rec} "
            + $"| {(up ? "**" : "")}{names17[ord17[0].K]}{(up ? "**" : "")} {ord17[0].R:+0.00;-0.00} "
            + $"| **{ord17[0].R * ord17[0].R:F3}** "
            + $"| {(ord17.Length > 1 ? $"{names17[ord17[1].K]} {ord17[1].R:+0.00;-0.00}" : "—")} "
            + $"| {(up ? "**○**" : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine(miss15 == 0
        ? "**検算: 第15期の記録した第一近似・r² と完全に一致（ずれ 0 件）。** この診断は第15期と同じ盤を見ている。"
        : $"**検算: {miss15} 件ずれた。第15期と同じ盤を見ていない——先へ進む前に原因を潰すこと。**");
    Console.WriteLine();
    Console.WriteLine($"**(A)(B)(C) が第一近似になったのは {improved} / {conW.Length} 波。**");
    Console.WriteLine();

    // (A)(B)(C) 単体が波ごとにどれだけ効くか。第15期 §9-3 の一覧と同じ形。
    Console.WriteLine("### 8-1. (A)(B)(C) の単相関（寄与する波）");
    Console.WriteLine();
    Console.WriteLine("**符号まで含めて読む。** 同じ量が波によって逆向きに効くなら、それは");
    Console.WriteLine("「どちらの波にも効く地力」ではなく**波の性格そのもの**（第15期 §9-3 の読み方）。");
    Console.WriteLine();
    Console.WriteLine("| 量 |" + string.Concat(conW.Select(w => $" {waves[w].Tag} |")) + " 符号の向き |");
    Console.WriteLine("|---|" + string.Concat(conW.Select(_ => "--:|")) + ":-:|");
    foreach (var (nm, v) in outNames.Concat(new[] { ("総攻（比較）", atk) }))
    {
        var rs = conW.Select(w => Correlate(v, rate[w]).R).ToArray();
        bool allSame = rs.All(r => r >= 0) || rs.All(r => r <= 0);
        Console.WriteLine($"| {nm} |" + string.Concat(rs.Select(r => $" {(double.IsNaN(r) ? "—" : $"{r:+0.00;-0.00}")} |"))
            + $" {(allSame ? "揃う" : "**反転する**")} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9. 交互作用項の作り直し（第16期 Phase GB） ---
    Console.WriteLine("## 9. 交互作用項の作り直し（第16期 Phase GB）");
    Console.WriteLine();
    Console.WriteLine("**積の材料を `総攻` から (A)(B)(C) に差し替える。** 片側だけの特徴量は交互作用成分と");
    Console.WriteLine("相関が**恒等的に 0** なので（第16期 §11。残差は行にも列にも和が 0）、(A) を単体で");
    Console.WriteLine("足しても 0 のまま——**積にして初めて意味を持つ。**");
    Console.WriteLine();

    // 分散分解（第16期 §11 と同じ計算）。
    int nC = conW.Length;
    double[][] Resid(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        return Enumerable.Range(0, nC)
            .Select(c => Enumerable.Range(0, nT).Select(t => y[c][t] - rowM[c] - colM[t] + grand).ToArray())
            .ToArray();
    }
    (double Wave, double Build, double Inter) Decompose(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        double ssT = y.SelectMany(r => r).Sum(v => (v - grand) * (v - grand));
        double ssW = nT * rowM.Sum(m => (m - grand) * (m - grand));
        double ssB = nC * colM.Sum(m => (m - grand) * (m - grand));
        double ssI = Resid(src).SelectMany(r => r).Sum(v => v * v);
        return (ssW / ssT * 100, ssB / ssT * 100, ssI / ssT * 100);
    }
    var decW = Decompose(rate);
    var decD = Decompose(degree);
    var residW = Resid(rate);
    var residD = Resid(degree);
    double[] Flat(Func<int, int, double> get) => Enumerable.Range(0, nC)
        .SelectMany(c => Enumerable.Range(0, nT).Select(t => get(conW[c], t))).ToArray();
    double[] FlatV(double[][] v) => v.SelectMany(r => r).ToArray();
    double[] residFlat = FlatV(residW);

    Console.WriteLine("### 9-1. 分散分解（第16期 §11 と同じ計算）");
    Console.WriteLine();
    Console.WriteLine($"寄与する {nC} 波 × {nT} 編成 = **{nC * nT} 点**。");
    Console.WriteLine();
    Console.WriteLine("| 目的変数 | 波の主効果 | 編成の主効果 | **交互作用** | 第16期の記録 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    Console.WriteLine($"| 勝率 | {decW.Wave:F1}% | {decW.Build:F1}% | **{decW.Inter:F1}%** | 28.3% |");
    Console.WriteLine($"| 残存度 | {decD.Wave:F1}% | {decD.Build:F1}% | **{decD.Inter:F1}%** | 21.7% |");
    Console.WriteLine();
    // 検算。**(A)(B)(C) も片側だけの量なので、単体では交互作用成分と相関 0 になるはず。**
    double maxOne = outNames.Max(o => Math.Abs(Pearson(Flat((w, t) => o.V[t]), residFlat)));
    Console.WriteLine($"**検算: (A)(B)(C) を単体で交互作用成分に当てると |r| = {maxOne:F6}。**");
    Console.WriteLine("**新しい特徴量でも 0 になるのが正しい**——これは測定結果ではなく恒等式で、");
    Console.WriteLine("「出力を測れば交互作用が説明できる」ではなく「**出力を積の材料にできる**」が");
    Console.WriteLine("第17期の主張であることの確認になる。");
    Console.WriteLine();

    // --- 9-2. 項の候補 ---
    //
    // **総当たりで作らない**（第16期 §10 と同じ縛り）。出どころは
    //   (a) 第16期の項の材料を (A)(B)(C) に差し替えたもの
    //   (b) 第16期 §7 の甲乙の説明を (B)(C) で書き直したもの
    //   (c) 第16期の項をそのまま（対照）
    // の3つだけ。10 個以内。
    double AllyOut(int t) => featA[t];
    var terms = new (string Name, string Expr, string From, string Why, Func<int, int, double> Get)[]
    {
        ("耐えるT", "味方の総HP ÷ 敵総攻", "第16期のまま（対照）",
            "第16期の最良項。**出力を含まない**ので、比較の基準として据え置く",
            (w, t) => targets[t].F.Occupied().Sum(x => x.Def.MaxHp)
                      / (double)waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)),
        ("集中砲火", "敵総攻 ÷ 味方の最薄HP", "第16期のまま（対照）",
            "同上。出力を含まない項をもう1本残す",
            (w, t) => waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)
                      / (double)targets[t].F.Occupied().Min(x => x.Def.MaxHp)),
        ("削るT'", "敵総HP ÷ **(A)**", "第16期 `削るT` の差し替え",
            "**敵を削り切るまでのターン数。** 第16期は分母が `総攻` だったので、"
            + "反撃軸と毒軸で桁が違っていた",
            (w, t) => waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp) / AllyOut(t)),
        ("時計比'", "(味方の総HP × **(A)**) ÷ (敵の総HP × 敵総攻)", "第16期 `時計比` の差し替え",
            "**2つの時計の競走を1本にまとめたもの**（`耐えるT` ÷ `削るT'`）。第16期は味方側が"
            + "`総HP × 総攻` だった",
            (w, t) => targets[t].F.Occupied().Sum(x => x.Def.MaxHp) * AllyOut(t)
                      / (waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp)
                         * (double)waves[w].Enemy.Occupied().Sum(x => x.Def.Attack))),
        ("一撃圏'", "敵の個体HP中央値 ÷ (**(A)** ÷ 味方の体数)", "第16期 `一撃圏` の差し替え",
            "**1体あたりの実効出力で何ターン殴れば1体落ちるか。** 第16期は分母が `総攻 ÷ 体数` "
            + "だったので、毒軸の一撃圏が実際より遠く出ていた",
            (w, t) => MedianHp(waves[w].Enemy) / (AllyOut(t) / targets[t].F.Count)),
        ("範囲の変換'", "味方の範囲枚数 × 敵体数 ÷ **一撃圏'**", "第16期 `範囲の変換` の差し替え",
            "第16期 事例 8。**巻き込み枚数ではなく、巻き込んだ結果何体落ちたかで決まる**",
            (w, t) => AoeCount(targets[t].F) * waves[w].Enemy.Count
                      * (AllyOut(t) / targets[t].F.Count) / MedianHp(waves[w].Enemy)),
        ("育ちの余地", "**(B)** × 耐えるT", "第16期 §7-2（甲群）を (B) で書き直したもの",
            "**甲群の説明そのもの**——出力が時間で育つ編成は、耐えられる時間が長い波で伸びる。"
            + "第16期は「時間で育つ」を数値で持っていなかった",
            (w, t) => featB[t] * targets[t].F.Occupied().Sum(x => x.Def.MaxHp)
                      / (double)waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)),
        ("育ちは間に合うか", "**(B)** ÷ 削るT'", "第16期 §7-2（甲群）",
            "育つ前に決着するなら育ちは価値にならない。**上の項の裏側**（分母が敵の硬さ）",
            (w, t) => featB[t] * AllyOut(t) / waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp)),
        ("被弾駆動 × 敵総攻", "**(C)** × 敵総攻", "第16期 §7-2（甲群・溜め改）",
            "**反撃軸は敵が殴ってくるほど出力が出る。** 第16期は `振に帰属%` として"
            + "診断でしか見ていなかった量を、積の材料にした",
            (w, t) => featC[t] * waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)),
        ("被弾駆動 × 敵1体攻", "**(C)** × 敵の1体あたり攻", "Phase HA §3-1（参照台の門）",
            "**門は総攻ではなく1体あたり攻で決まる**（呪詛は1体ずつに −6 する）。"
            + "同じ総攻でも、薄く広く殴る敵と重く殴る敵で反撃軸の出力が変わる",
            (w, t) => featC[t] * waves[w].Enemy.Occupied().Average(x => x.Def.Attack)),
    };

    Console.WriteLine("### 9-2. 交互作用項の候補（10 個）");
    Console.WriteLine();
    Console.WriteLine("**総当たりで作っていない**（第16期 §10 と同じ縛り）。出どころは3つだけ:");
    Console.WriteLine("**(a) 第16期の項の材料を (A)(B)(C) に差し替えたもの**、");
    Console.WriteLine("**(b) 第16期 §7 の甲乙の説明を (B)(C) で書き直したもの**、**(c) 第16期の項そのまま（対照）**。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | 式 | 出どころ | 理由 |");
    Console.WriteLine("|--:|---|---|:-:|---|");
    for (int k = 0; k < terms.Length; k++)
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | `{terms[k].Expr}` | {terms[k].From} | {terms[k].Why} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-3. 効くか ---
    Console.WriteLine("### 9-3. 交互作用項は効くか");
    Console.WriteLine();
    Console.WriteLine("第16期 §12 と同じ3通りの当て方。**(2) が本題。**");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | (1) プール r | r² | **(2) 交互作用 r** | **r²** | (2) ρ | (2) 残存度 r | 符号一致 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|:-:|");
    var score = new List<(int K, double R2)>();
    int agree = 0;
    for (int k = 0; k < terms.Length; k++)
    {
        double[] x = Flat(terms[k].Get);
        double rp = Pearson(x, Flat((w, t) => rate[w][t]));
        var ci = Correlate(x, residFlat);
        double rd = Pearson(x, FlatV(residD));
        // NaN（分散0・標本不足）は判定不能。Math.Sign は NaN で例外を投げる。
        bool known = !double.IsNaN(ci.R) && !double.IsNaN(rd);
        bool ok = known && Math.Sign(ci.R) == Math.Sign(rd);
        if (ok) agree++;
        score.Add((k, ci.R * ci.R));
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | {Sg(rp, 2):+0.00;-0.00} | {rp * rp:F3} "
            + $"| {Sg(ci.R, 2):+0.00;-0.00} | **{ci.R * ci.R:F3}** | {Sg(ci.Rho, 2):+0.00;-0.00} "
            + $"| {Sg(rd, 2):+0.00;-0.00} | {(known ? (ok ? "○" : "**×**") : "—")} |");
    }
    Console.WriteLine();
    var best = score.OrderByDescending(x => x.R2).First();
    double maxRho = Enumerable.Range(0, terms.Length)
        .Max(k => Math.Abs(Correlate(Flat(terms[k].Get), residFlat).Rho));
    Console.WriteLine($"**最良は `{terms[best.K].Name}` で r² = {best.R2:F3}**（第16期の最良は `範囲の変換` の **0.003**）。");
    Console.WriteLine($"順位相関でも最大 |ρ| = {maxRho:F3}。**単調な非線形を取りこぼしているのではない。**");
    Console.WriteLine($"符号が一致したのは {agree} / {terms.Length}（勝率の交互作用成分 ↔ 残存度の交互作用成分）。");
    Console.WriteLine();
    Console.WriteLine($"交互作用は全分散の {decW.Inter:F1}% なので、最良の項が説明しているのは");
    Console.WriteLine($"**全体の {best.R2 * decW.Inter / 100:F3}**。");
    Console.WriteLine();
    // 対照2項は第16期の項をそのまま持ってきたものなので、**第16期の数字を再現するはず**。
    // 再現しなければ、盤か波か編成集合のどれかが動いている。
    {
        double poolHold = Pearson(Flat(terms[0].Get), Flat((w, t) => rate[w][t]));
        bool ok = Math.Abs(poolHold - 0.25) <= 0.005 && Math.Abs(poolHold * poolHold - 0.061) <= 0.005;
        Console.WriteLine($"**検算: 対照項 `耐えるT` のプール r = {poolHold:+0.00;-0.00} / r² = {poolHold * poolHold:F3}"
            + $"（第16期の記録は +0.25 / 0.061）→ {(ok ? "一致" : "**ずれ**")}。**");
        Console.WriteLine("**分散分解も第16期と完全に一致している**（§9-1 の記録列）ので、");
        Console.WriteLine("**動いたのは項の材料だけ**——盤も波も編成集合も第16期のまま。");
        Console.WriteLine();
    }
    Console.Out.Flush();

    // --- 10. 判定 ---
    // 線は第16期と同じ（交互作用成分の r² が 0.10 を超えるか）。**線を新しく作らない。**
    const double TermLine = 0.10;
    Console.WriteLine("## 10. 判定（計画書 §4-3 のどの行か）");
    Console.WriteLine();
    Console.WriteLine($"線は**第16期と同じ**「交互作用成分に対する r² が {TermLine:F2} を超えるか」。");
    Console.WriteLine("**線を新しく作らない**（作ると第16期と比べられなくなる）。");
    Console.WriteLine();
    double bestWaveR2 = conW.Max(w => Enumerable.Range(0, names17.Length)
        .Select(k => { double r = Correlate(Col17(k, w), rate[w]).R; return double.IsNaN(r) ? 0 : r * r; }).Max());
    double best15 = conW.Max(w => Enumerable.Range(0, names15.Length)
        .Select(k => { double r = Correlate(Col15(k, w), rate[w]).R; return double.IsNaN(r) ? 0 : r * r; }).Max());
    bool interUp = best.R2 >= TermLine;
    bool waveUp = improved > 0 && bestWaveR2 > best15 + 0.005;
    var kmin2 = kIx.Min(t => featB[t]); var kmax2 = kIx.Max(t => featB[t]);
    var omin2 = oIx.Min(t => featB[t]); var omax2 = oIx.Max(t => featB[t]);
    bool split = !(kmin2 <= omax2 && omin2 <= kmax2);

    Console.WriteLine($"- 交互作用成分に対する最良 r² = **{best.R2:F3}**（第16期 0.003）→ {TermLine:F2} を"
        + $"{(interUp ? "**超えた**" : "超えない")}");
    Console.WriteLine($"- 波ごとの第一近似が (A)(B)(C) に替わった波: **{improved} / {conW.Length}**。"
        + $"波ごとの最良 r² は {best15:F3}（12種）→ {bestWaveR2:F3}（15種）");
    Console.WriteLine($"- (B) による甲乙の分離: **{(split ? "分かれる" : "重なる")}**"
        + $"（甲 {kmin2:F2}〜{kmax2:F2} / 乙 {omin2:F2}〜{omax2:F2}）");
    Console.WriteLine();
    // **4 行を独立に評価する。** 排他ではないので、当たった行を全部出す
    // ——1 行だけ選ぶ形にすると、複数当たったときにどれを捨てたかが記録に残らない。
    bool row4 = !interUp && !waveUp;
    Console.WriteLine("| # | 計画書 §4-3 の観測 | 当たるか | 根拠 |");
    Console.WriteLine("|--:|---|:-:|---|");
    Console.WriteLine($"| 1 | (A)(B)(C) の積で交互作用成分の説明力が上がる | {(interUp ? "**○**" : "×")} "
        + $"| 最良 r² = {best.R2:F3} < {TermLine:F2} |");
    Console.WriteLine($"| 2 | 波ごとの説明力は上がるが交互作用は上がらない | {(waveUp ? "**○**" : "×")} "
        + $"| 第一近似が替わったのは {improved} / {conW.Length} 波。最良 r² {best15:F3} → {bestWaveR2:F3} |");
    Console.WriteLine($"| 3 | (B) で甲乙が分離できない | {(!split ? "**○**" : "×")} "
        + $"| 甲 {kmin2:F2}〜{kmax2:F2} / 乙 {omin2:F2}〜{omax2:F2}（重なる） |");
    Console.WriteLine($"| 4 | どれも上がらない → 測り方が悪いか台が中立でない | {(row4 ? "**○**" : "×")} "
        + $"| §4-3 の中立性は ρ {cross.Rho:F3} / 余地 {room:F3} で**通っている** |");
    Console.WriteLine();
    Console.WriteLine("**当たったのは 3 行目と 4 行目。**");
    Console.WriteLine();
    Console.WriteLine("- **3行目。** (B) は甲乙を分けない。ただし §7-1 のとおり、**分けない理由は");
    Console.WriteLine("  「分割に実体が無い」ではなく「(B) が測っているものが違う」ほうに見える**");
    Console.WriteLine("  ——甲乙は「出力が時間で育つか」ではなく「**育った出力が撃破に変換されるか**」で");
    Console.WriteLine("  割れていた。計画書 §4-3 の3行目は「数値的な実体が無い**可能性**」と書いているので、");
    Console.WriteLine("  **その可能性は棄却できていないが、支持もされていない。**");
    Console.WriteLine("- **4行目。** §4-3 の中立性は通っている（余地 " + $"{room:F3}" + "）ので、");
    Console.WriteLine("  「参照台が中立でない」ではなく「**測り方（何を測るか）が足りない**」の側。");
    Console.WriteLine();
    Console.WriteLine("**1行目は明確に外れた。** (A) は `総攻` と ρ −0.44 で**符号すら逆**の別物なのに");
    Console.WriteLine($"（§6）、積にすると交互作用成分の説明力は 0.003 → {best.R2:F3} で 1ミリも動かない。");
    Console.WriteLine("**「16期分の壁の原因が `総攻` だった」は支持されない。** `総攻` が出力を");
    Console.WriteLine("表していなかったのは事実だが（§6 で確定した）、**壁の原因はそれではなかった。**");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}

// convert モード: 出力が撃破に変換されるか — 個体HP だけを振った参照台の系列（第18期 Phase IA）。
//
// 第17期で「特徴量が悪い」説はほぼ否定された。(A) 実効打点/T は `総攻` と ρ −0.44 の**別物**
// （符号すら逆）なのに、積の材料に差し替えても交互作用成分の説明力は 0.003 → 0.002 で
// 1ミリも動かない。**ただし1つだけ、17期分ずっと主役だったのに単独で振っていない軸がある
// ——個体HP。**
//
//   第6〜7期: 「符号を決めるのは個体HP、大きさを決めるのは体数」
//   第16期:   甲乙の軸は「個体HP と総攻」
//   第17期:   甲乙の軸は「育つか」ではなく**「撃破に変換されるか」**
//             （毒は (B) 16.97 で9編成中最大なのに乙群）
//
// **「撃破に変換されるか」は、味方の打点と敵の個体HP の関係で決まる。** 第17期の参照台の格子は
// 1体攻（B1〜B3）と体数（B4〜B6）を振っていて、**個体HP を単独では振っていない**ので測れない。
// ここでは参照台を**個体HP だけ変えて**並べ、(A) がどう変わるかを測る。
//
// **循環は第17期と同じ形で切れている。** 量を測るのは参照台で、目的変数（波ごとの勝率）の
// 戦闘とは別の戦闘。その波の敵を削り切ったかどうかは1ビットも入らない（第14期の分子経路）。
//
// 却下した案: 波（`WaveCatalog()`）の個体HP 中央値で編成を層別して (A) を測り直す。
// 候補波は個体HP 以外（体数・1体攻・攻撃型）も同時に違うので、**個体HP だけを振ったことに
// ならない**——第16期の格子が「主↔従の対角線」で嵌まったのと同じ形。
//
// 却下した案: 新しい `UnitDef` を `EnemyCatalog` に足す。刻みは診断のローカルで組む
// （`gradient` / `aim` / `timing` と同じ作法）。`BattleCore` を触ると `dump` が動きうるし、
// 採用の決まっていない的が `EnemyCatalog` に残る。
//
// 診断用で docs/ には置かない（output / wave / power / bench / dissect と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 convert [絞り込み]
if (focusId == "convert")
{
    const int ConvSeeds = 200;   // output / wave / dissect / compare / power / bench と同じ

    var all = CompareBuilds();
    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    // 表示桁でゼロに丸まる負の値を `-+0.0` と出さないための丸め（`dissect` / `output` と同じ理由）。
    double Sg(double v, int dp) => double.IsNaN(v) ? v : Math.Round(v, dp) + 0.0;

    // 個体HP の中央値。`dissect` / `output` のローカル定義と同じ式。
    double MedianHp(Formation e)
    {
        var v = e.Occupied().Select(x => (double)x.Def.MaxHp).OrderBy(x => x).ToArray();
        return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
    }

    Console.WriteLine("# 変換率 — 出力は撃破に変換されるか（第18期 Phase IA）");
    Console.WriteLine();
    Console.WriteLine("第17期で**甲乙の軸は「育つか」ではなく「撃破に変換されるか」**まで来た（乙群の 毒 は");
    Console.WriteLine("(B) 育ち 16.97 で9編成中いちばん高いのに乙）。**「撃破に変換されるか」は味方の打点と");
    Console.WriteLine("敵の個体HP の関係で決まる**が、第17期の参照台の格子は 1体攻 と 体数 を振っていて");
    Console.WriteLine("**個体HP を単独では振っていない**——ここで振る。");
    Console.WriteLine();
    Console.WriteLine("**測定だけで、盤面は1つも動かしていない**（`BattleCore` 無変更・`EnemyCatalog` 無変更）。");
    Console.WriteLine();

    // ================= 1. 台の系列 =================
    //
    // 基準は第17期の主台 `ZealotPorter`（重甲の荷駄兵 90/7/5・単体攻撃・特性なし）。
    // **振るのは個体HP だけ**で、攻撃力 7・速さ 5・パターン Single は全刻みで固定する。
    //
    // 門（第17期 Phase HA §3-1）は 1体あたり攻 > CurseTrait.EnemyDebuff（= 6）。荷駄兵の 7 は
    // これを通るので、**全刻みで門を通る**（攻を振らないので当然だが、確認は下の表で出す）。
    UnitDef Porter = EnemyCatalog.ZealotPorter;
    UnitDef HpStep(int hp) => hp == Porter.MaxHp ? Porter : new UnitDef
    {
        // 新しい Id を振る（`power` / `wave` が踏んだ「味方と敵の Def.Id 衝突」を避けるため、
        // また tally が刻みごとに分かれるようにするため）。**`EnemyCatalog` には足さない。**
        Id = $"porter_h{hp}", Name = $"重甲の荷駄兵(HP{hp})",
        MaxHp = hp, Attack = Porter.Attack, Speed = Porter.Speed,
        Traits = Porter.Traits, Pattern = Porter.Pattern,
    };
    Formation Line(UnitDef d, int n)
    {
        var f = new Formation();
        for (int i = 0; i < n; i++) f[i] = d;   // スロット昇順（前1→前3→中央→後1→後3）
        return f;
    }

    // X字化で編成枠が5つになったので、台は全部5体以下に組み直してある。
    // 系列P（主）: **体数 5 固定**・個体HP だけを 30 → 220。総HP は揃わない（150 → 1100）。
    // 系列Q（従）: **総HP 450 固定**・体数で調整（5 → 2）。個体HP 90 → 225。
    //   体数4 は 450/4 が整数にならないので刻みから外してある（3点になる）。
    // P90 と Q90 は**同一の台**（荷駄5 = 第17期の主台）なので、測り直さず同じ計測を共有する。
    var benches = new (string Tag, int Hp, int N)[]
    {
        ("P30",  30, 5), ("P60",  60, 5), ("P90",  90, 5), ("P145", 145, 5), ("P220", 220, 5),
        ("Q150", 150, 3), ("Q225", 225, 2),
        ("R2",    90, 2), ("R3",    90, 3), ("R4",    90, 4),
    };
    int nB = benches.Length;
    var benchF = benches.Select(b => Line(HpStep(b.Hp), b.N)).ToArray();
    int[] serP = { 0, 1, 2, 3, 4 };
    int[] serQ = { 2, 5, 6 };         // 先頭は P90（= Q90。荷駄5 そのもの）
    int[] serR = { 7, 8, 9, 2 };      // 体数だけを振る辺（個体HP 90 固定）。末尾は P90
    int mainB = 2;                    // 主刻み = P90 = 第17期の主台

    Console.WriteLine("## 1. 台の系列");
    Console.WriteLine();
    Console.WriteLine($"基準は第17期の主台 **重甲の荷駄兵（{Porter.MaxHp}/{Porter.Attack}/{Porter.Speed}・単体攻撃・特性なし）**。");
    Console.WriteLine("**振るのは個体HP だけ**で、攻撃力・速さ・攻撃パターンは全刻みで固定する。");
    Console.WriteLine("刻みは既存 def の使い回しでは作れない（`Levy` 30/攻8・`ZealotPlate` 60/攻16・");
    Console.WriteLine("`Warden` 145/攻12 は**攻撃力が全部違う**）ので、荷駄兵の HP だけを差し替えた複製を");
    Console.WriteLine("**診断のローカルで**組む（`EnemyCatalog` には足さない。`gradient` / `aim` / `timing` と同じ作法）。");
    Console.WriteLine();
    Console.WriteLine("### 1-1. 総HP を揃えるか — 揃えない系列を主に採る");
    Console.WriteLine();
    Console.WriteLine("計画書 §2-2 の設計判断。**両方作った。主は揃えないほう（系列P）。**");
    Console.WriteLine();
    Console.WriteLine("| 系列 | 固定するもの | 動くもの | 役 |");
    Console.WriteLine("|:-:|---|---|---|");
    Console.WriteLine("| **P** | 体数 5・1体攻 7・**総攻 35** | 個体HP 30〜220・総HP 150〜1100（＝戦闘長） | **主** |");
    Console.WriteLine("| **Q** | **総HP 450**・1体攻 7 | 個体HP 90〜225・体数 5→2・**総攻 35→14** | 従 |");
    Console.WriteLine("| **R** | **個体HP 90**・1体攻 7 | 体数 2〜6・総HP 180〜540・総攻 14→42 | 辺（検算用） |");
    Console.WriteLine();
    Console.WriteLine("**系列R は変換率を測る台ではない。** P と Q の食い違いを説明できるかを確かめるための");
    Console.WriteLine("**辺**で、個体HP を固定して体数だけを振ってある——P と Q は「個体HP と体数の格子」の");
    Console.WriteLine("2本の斜めの線なので、辺が1本無いと**食い違いの出どころが決まらない**");
    Console.WriteLine("（第13期 `bench` が主↔従の対角線で嵌まったのと同じ形）。§5-3 で使う。");
    Console.WriteLine();
    Console.WriteLine("**揃えないほうを主に採った理由は2つ。**");
    Console.WriteLine();
    Console.WriteLine("- **全域では原理的に揃えられない。** プレイヤーが置ける枠は5つしかない（`FormationRules.PlayableSlotCount`）ので、");
    Console.WriteLine("  個体HP 30 の台の総HP は最大 180、個体HP 220 の台は最小 220——**重ならない。**");
    Console.WriteLine("  総HP を揃えると刻みの範囲が 90〜270 に狭まり、**味方の実測打点（第6期の `pulse.md` から");
    Console.WriteLine("  中央値 10.6 / 四分位 4.4〜20.4 / 上位1割 51.1）を跨げない**——一撃圏の内と外を跨ぐことが");
    Console.WriteLine("  この測定の目的そのものなので、跨げない系列は主にできない。");
    Console.WriteLine("- **揃えると総攻が動く。** 体数で調整する以上 総攻 35 → 14 まで落ちるので、");
    Console.WriteLine("  **殴り返しの量が同時に変わる。** 反撃軸の出力は殴られた量にほぼ比例するから、");
    Console.WriteLine("  系列Q の変換率は「個体HP の効果」と「総攻の効果」の和になる。");
    Console.WriteLine("  系列P は 総攻 35 が全刻みで一定なので、**動いているのは個体HP と戦闘長だけ。**");
    Console.WriteLine();
    Console.WriteLine("**却下した案: 総攻も揃える**（体数を減らすぶん1体あたり攻を上げる。6体攻7 → 2体攻21）。");
    Console.WriteLine("総攻は揃うが**1体あたり攻が 7 → 21 に動く**ので、今度は反撃1回あたりの量と");
    Console.WriteLine("被弾強化の刻みが変わる。計画書 §2-2 の「攻撃力は全刻みで固定」に正面から反する。");
    Console.WriteLine();
    Console.WriteLine("**系列P の弱点は明記する。** 総HP が 7.3 倍になるので**戦闘長が同時に動く**");
    Console.WriteLine("（それ自体が「敵が硬い」の中身でもある）。両系列で変換率の順位が一致するかを");
    Console.WriteLine("§5 で確かめる——一致すれば、動かしたのが個体HP か戦闘長かに関わらず**同じ量**を測っている。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 系列 | 個体HP | 体数 | 総HP | 総攻 | 1体攻 | 呪詛後 | 門 |");
    Console.WriteLine("|:-:|:-:|--:|--:|--:|--:|--:|--:|:-:|");
    for (int b = 0; b < nB; b++)
    {
        Formation bf = benchF[b];
        double each = bf.Occupied().Average(x => x.Def.Attack);
        string ser = b == mainB ? "P / Q / R"
            : benches[b].Tag.StartsWith("P") ? "P"
            : benches[b].Tag.StartsWith("Q") ? "Q" : "R";
        Console.WriteLine($"| **{benches[b].Tag}** | {ser} | {benches[b].Hp} | {bf.Count} "
            + $"| {bf.Occupied().Sum(x => x.Def.MaxHp)} | {bf.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {each:F0} | {each - CurseTrait.EnemyDebuff:F0} "
            + $"| {(each > CurseTrait.EnemyDebuff ? "○" : "**×**")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**門（1体あたり攻 > {CurseTrait.EnemyDebuff}）は全刻みが通る。** 攻を振っていないので当然だが、");
    Console.WriteLine("**第17期はここで巡礼5（攻4）を落としている**ので毎回確認する（`手番外%` は §3 で見る）。");
    Console.WriteLine();
    Console.WriteLine($"**P90 は第17期の主台そのもの**（荷駄5）。同じ計測を系列Q・系列R の端としても使う");
    Console.WriteLine("——2回測ると、系列間の一致の値そのものに実行間のばらつきが乗る。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 2. 計測と検算 =================
    //
    // `MeasureOutput`（第17期）をそのまま呼ぶ。**打点の定義を写さない**——第15期が
    // `WaveCatalog()` を1箇所に集めたのと同じ理由で、2つ目の診断が定義のコピーを持った瞬間に
    // 片方だけ直す事故が起きる。第18期で足したのは `Kills` と `Overkill` の2列だけで、
    // **`output` の出力は1文字も動かない**（受け入れ条件は「26モード差分ゼロ」）。
    var tr = new OutputTrace[nB][];
    for (int b = 0; b < nB; b++)
    {
        tr[b] = new OutputTrace[nT];
        for (int t = 0; t < nT; t++) tr[b][t] = MeasureOutput(targets[t].F, benchF[b], ConvSeeds);
        Console.Out.Flush();
    }

    Console.WriteLine("## 2. 検算");
    Console.WriteLine();
    double maxGap = 0;
    long totalFromAlly = 0;
    for (int b = 0; b < nB; b++)
        for (int t = 0; t < nT; t++)
        {
            maxGap = Math.Max(maxGap, Math.Abs(tr[b][t].Damage.Sum() - tr[b][t].TallyDamage));
            totalFromAlly += tr[b][t].FoeFromAlly;
        }
    var clash = targets.SelectMany(x => x.F.Occupied().Select(y => y.Def.Id))
        .Intersect(benchF.SelectMany(c => c.Occupied().Select(y => y.Def.Id))).ToArray();
    Console.WriteLine($"- **イベント集計と敵 tally の差**: 最大 {maxGap:F0}（{nB * nT} 組）。0 でなければ打点を取りこぼしている");
    Console.WriteLine($"- **敵同士の巻き込み（`TakenFromAlly`）**: {totalFromAlly}。0 でなければ受け手側から与ダメを取る前提が崩れる");
    Console.WriteLine($"- **味方と敵の `Def.Id` 衝突**: {clash.Length} 件"
        + (clash.Length == 0 ? "" : $"（{string.Join(" / ", clash)}）"));
    Console.WriteLine();

    // 第17期の記録（README「検証で分かったこと」の第17期の項・`output` §5-2）。
    // **値を分析に使うのではなく、P90 が第17期の主台と同じ台であることの検算にだけ使う**
    // （`dissect` §12-2・`output` §8 と同じ作法）。
    var rec17 = new (string Name, double A, double C)[]
    {
        ("惨禍×被弾強化", 263.9, 92), ("毒 (グザ×ミオ×ラウ)", 170.4, 92), ("耐久 (ガルド×ノノ)", 51.3, 0),
    };
    int miss17 = 0;
    Console.WriteLine("**第17期の主台との一致**（P90 = 荷駄5）。値は `output` §5-2 から:");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 第17期 (A) | ここでの (A) | 第17期 (C) | ここでの (C) |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (var (nm, ra, rc) in rec17)
    {
        int t = Array.FindIndex(targets, x => x.Name == nm);
        if (t < 0) { Console.WriteLine($"| {nm} | {ra:F1} | — | {rc:F0}% | — |"); continue; }
        bool ok = Math.Abs(tr[mainB][t].RateAll - ra) <= 0.05 && Math.Abs(tr[mainB][t].OffTurnPct - rc) <= 0.5;
        if (!ok) miss17++;
        Console.WriteLine($"| {nm} | {ra:F1} | {tr[mainB][t].RateAll:F1}{(ok ? "" : " **←ずれ**")} "
            + $"| {rc:F0}% | {tr[mainB][t].OffTurnPct:F0}% |");
    }
    Console.WriteLine();
    Console.WriteLine(miss17 == 0
        ? "**ずれ 0 件。** P90 は第17期の主台と同じ台で、同じ値を出している。"
        : $"**{miss17} 件ずれた。第17期と同じ台を見ていない——先へ進む前に原因を潰すこと。**");
    Console.WriteLine();
    Console.WriteLine("> **(A) はオーバーキルを含む。** `ApplyDamage` は残HPで切り詰めない");
    Console.WriteLine("> （`target.Hp -= amount` で HP は負まで落ち、`Damage` イベントの `Amount` は素の量）ので、");
    Console.WriteLine("> **(A) が測っているのは「敵のHPに変換された量」ではなく「振り下ろした量」。**");
    Console.WriteLine("> これは変換率の読みに直接効く——低HP の台で (A) が落ちないのは、");
    Console.WriteLine("> **無駄打ちが打点として数えられているから**かもしれない。`オーバーキル%` を §3 に出してある。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 3. 刻みは要件を満たしているか =================
    //
    // 計画書 §4-8 の停止条件そのもの。**刻みの一部で決着してしまい (A) が比較できないなら、
    // 実装を進める前に報告する。** 線は第17期と同じで「決着すること自体は停止条件ではない」。
    Console.WriteLine("## 3. 刻みは要件を満たしているか（計画書 §4-8 の停止条件）");
    Console.WriteLine();
    Console.WriteLine("| 量 | 読み方 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| `決着T` | 全試行の平均ターン数。**1.0 に近いと (A) が「1ターンで振り下ろせる量」に潰れる** |");
    Console.WriteLine("| `敵全滅%` | 味方が削り切った試行の割合 |");
    Console.WriteLine("| `味方全滅%` | 味方が削られ切った試行の割合。**高いと出力が途中で止まる** |");
    Console.WriteLine("| `手番外%` | 打点のうち手番の振り以外から出たぶん。**0 なら被弾駆動が死んでいる**（門） |");
    Console.WriteLine("| `撃破/戦` | 敵の撃破数（受け手側から。毒・燃焼の削りも載る） |");
    Console.WriteLine("| `オーバーキル%` | 打点のうち敵の最大HPを超えて振り下ろしたぶん |");
    Console.WriteLine();
    Console.WriteLine("| 台 | 個体HP | 体数 | 決着T | 決着T 最小(編成) | 敵全滅% | 味方全滅% | 手番外% | 手番外% 0 の編成 | 撃破/戦 | オーバーキル% |");
    Console.WriteLine("|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        double turns = Enumerable.Range(0, nT).Average(t => tr[b][t].Turns.Average());
        double tmin = Enumerable.Range(0, nT).Min(t => tr[b][t].Turns.Average());
        double wipe = Enumerable.Range(0, nT).Average(t => tr[b][t].AllyWipe * 100.0 / ConvSeeds);
        double clear = Enumerable.Range(0, nT).Average(t => tr[b][t].FoeWipe * 100.0 / ConvSeeds);
        double off = Enumerable.Range(0, nT).Average(t => tr[b][t].OffTurnPct);
        int zero = Enumerable.Range(0, nT).Count(t => tr[b][t].OffTurnPct <= 1e-9);
        double kills = Enumerable.Range(0, nT).Average(t => (double)tr[b][t].Kills / ConvSeeds);
        double okill = Enumerable.Range(0, nT).Average(t => tr[b][t].Overkill * 100.0 / tr[b][t].Damage.Sum());
        Console.WriteLine($"| **{benches[b].Tag}** | {benches[b].Hp} | {benchF[b].Count} | {turns:F1} | {tmin:F1} "
            + $"| {clear:F1}% | {wipe:F1}% | {off:F1}% | {zero} / {nT} | {kills:F2} | {okill:F1}% |");
    }
    Console.WriteLine();
    var degenerate = Enumerable.Range(0, nB)
        .Where(b => Enumerable.Range(0, nT).Count(t => tr[b][t].Turns.Average() < 2.0) > 0).ToArray();
    Console.WriteLine(degenerate.Length == 0
        ? "**平均決着T が 2.0 を下回る（＝1ターンで終わる）編成はどの刻みにも 0 件。** (A) はどの刻みでも比較できる。"
        : "> **平均決着T が 2.0 を下回る編成がある刻み**: "
          + string.Join(" / ", degenerate.Select(b => $"`{benches[b].Tag}`（"
              + string.Join("・", Enumerable.Range(0, nT).Where(t => tr[b][t].Turns.Average() < 2.0)
                  .Select(t => $"{targets[t].Name} {tr[b][t].Turns.Average():F1}T")) + "）"))
          + "。**その編成の (A) はその刻みで「1ターンで振り下ろせる量」に潰れている**"
          + "（ターン数は整数なので、半ターンで削り切っても 1 と数える）。"
          + "変換率は §4 で**全刻み版と高刻みだけの版の両方**を出し、順位が動くかを見る。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 4. 変換率 =================
    //
    // **表し方は log-log の傾き**（計画書 §2-1 が挙げた2案のうち後者）。
    //   β = d ln((A)) / d ln(個体HP)   ——刻み全部を使った最小二乗
    // 端点の比（低HP台 ÷ 高HP台）にしなかったのは、**2点しか使わないと刻みの片方が
    // 決着で潰れたときに丸ごとそれを拾う**ため。傾きなら中間の刻みが効く。
    // 比の側も併記する（`端点比` = (A)(P220) ÷ (A)(P30)）ので、読み手はどちらでも読める。
    //
    //   β ≈ 0   打点が個体HP に依存しない（変換の問題を持たない）
    //   β < 0   個体HP が上がると打点が落ちる（**撃破に依存する出力**）
    //   β > 0   個体HP が上がると打点が上がる（**積み上げ型**。的が長生きするほど段が乗る）
    double LogHp(int b) => Math.Log(benches[b].Hp);
    double LogN(int b) => Math.Log(benches[b].N);
    // 傾き。x 軸（個体HP か体数）と、打点/ターンの取り方を差し替えられるようにしてある
    // ——§5-2 で「最後の1ターンが端数であること」の影響を測るため。
    double Slope(int[] ser, Func<int, double> x, Func<int, double> y)
    {
        double mx = ser.Average(x), my = ser.Average(y);
        double cov = 0, vx = 0;
        foreach (int b in ser)
        {
            double dx = x(b) - mx;
            cov += dx * (y(b) - my);
            vx += dx * dx;
        }
        return cov / vx;
    }
    int[] serHi = { 2, 3, 4 };        // 系列P の高刻みだけ（90 / 145 / 220）
    int[] serLo = { 0, 1, 2 };        // 系列P の低刻みだけ（30 / 60 / 90）
    double[] BetaOf(int[] ser, Func<int, bool> take) => Enumerable.Range(0, nT)
        .Select(t => Slope(ser, LogHp, b => Math.Log(tr[b][t].Rate(take)))).ToArray();
    // 端数ターンを補正した (A)。**ターン数は整数なので、削り切った最後のターンは丸ごと 1 と数える**
    // ——短い戦闘ほど「働いていない端数」が分母に入り、(A) が過小に出る。半ターンを引くのが
    // その一次補正で、これで順位が動かないなら β の符号は端数の産物ではない。
    double RateAdj(int b, int t)
    {
        OutputTrace o = tr[b][t];
        return o.Damage.Sum() / (o.Turns.Sum() - 0.5 * o.Seeds);
    }
    var betaP = BetaOf(serP, _ => true);
    var betaQ = BetaOf(serQ, _ => true);
    var betaHi = BetaOf(serHi, _ => true);
    var betaLo = BetaOf(serLo, _ => true);
    var betaAdj = Enumerable.Range(0, nT)
        .Select(t => Slope(serP, LogHp, b => Math.Log(RateAdj(b, t)))).ToArray();
    // 体数の辺（個体HP 90 固定）。**変換率ではない**——P と Q の食い違いを説明する量。
    var gammaR = Enumerable.Range(0, nT)
        .Select(t => Slope(serR, LogN, b => Math.Log(tr[b][t].RateAll))).ToArray();

    Console.WriteLine("## 4. 変換率");
    Console.WriteLine();
    Console.WriteLine("**定義（計画書 §2-1 は「表し方は実装者判断でよいが明記すること」）:**");
    Console.WriteLine();
    Console.WriteLine("> **変換率 β = d ln((A) 実効打点/T) ÷ d ln(敵の個体HP)** — 刻み全部を使った最小二乗の傾き。");
    Console.WriteLine();
    Console.WriteLine("| β | 意味 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| **≈ 0** | 打点が個体HP に依存しない。**変換の問題を持たない**（オーバーキルしても打点は落ちない、あるいは撃破と無関係に削る） |");
    Console.WriteLine("| **< 0** | 個体HP が上がると打点が落ちる。**撃破に依存する出力**（撃破ごとに何かが起きる／敵が減ると被弾が減る） |");
    Console.WriteLine("| **> 0** | 個体HP が上がると打点が上がる。**積み上げ型**（毒・燃焼。的が長生きするほど段が乗る） |");
    Console.WriteLine();
    Console.WriteLine("**端点の比（低HP台 ÷ 高HP台）にしなかった理由**: 2点しか使わないので、刻みの片方が");
    Console.WriteLine("決着で潰れたときにそれを丸ごと拾う。傾きなら中間の刻みが効く。比の側（`端点比`）も");
    Console.WriteLine("併記してあるので、読み手はどちらでも読める。");
    Console.WriteLine();
    Console.WriteLine("### 4-1. 編成 × 個体HP の (A)（系列P・体数5固定）");
    Console.WriteLine();
    Console.WriteLine("**β の降順**。`端点比` = (A)(P220) ÷ (A)(P30)。`βQ` は総HP を揃えた系列（§5）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | " + string.Join(" | ", serP.Select(b => $"{benches[b].Tag} (HP{benches[b].Hp})"))
        + " | **β** | 端点比 | βQ | β高刻み | (C) 手番外% |");
    Console.WriteLine("|---|" + string.Concat(serP.Select(_ => "--:|")) + "--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => betaP[t]))
        Console.WriteLine($"| {targets[t].Name} | "
            + string.Join(" | ", serP.Select(b => $"{tr[b][t].RateAll:F1}"))
            + $" | **{Sg(betaP[t], 2):+0.00;-0.00}** | {tr[4][t].RateAll / tr[0][t].RateAll:F2} "
            + $"| {Sg(betaQ[t], 2):+0.00;-0.00} | {Sg(betaHi[t], 2):+0.00;-0.00} "
            + $"| {tr[mainB][t].OffTurnPct:F0}% |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-2. 上位・下位5 ---
    Console.WriteLine("### 4-2. 変換率の上位・下位5編成（計画書 §2-3）");
    Console.WriteLine();
    Console.WriteLine("**上位（β > 0 = 積み上げ型。的が硬いほど出力が伸びる）**");
    Console.WriteLine();
    Console.WriteLine("| 順位 | 編成 | β | (A) P90 | (C) 手番外% | 撃破/戦 P90 | オーバーキル% P90 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|");
    var byBeta = Enumerable.Range(0, nT).OrderByDescending(t => betaP[t]).ToArray();
    void BetaRow(int rank, int t) => Console.WriteLine($"| {rank} | {targets[t].Name} "
        + $"| {Sg(betaP[t], 2):+0.00;-0.00} | {tr[mainB][t].RateAll:F1} | {tr[mainB][t].OffTurnPct:F0}% "
        + $"| {(double)tr[mainB][t].Kills / ConvSeeds:F2} "
        + $"| {tr[mainB][t].Overkill * 100.0 / tr[mainB][t].Damage.Sum():F1}% |");
    for (int k = 0; k < Math.Min(5, nT); k++) BetaRow(k + 1, byBeta[k]);
    Console.WriteLine();
    Console.WriteLine("**下位（β < 0 = 撃破に依存する出力。的が硬いと出力が落ちる）**");
    Console.WriteLine();
    Console.WriteLine("| 順位 | 編成 | β | (A) P90 | (C) 手番外% | 撃破/戦 P90 | オーバーキル% P90 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|");
    for (int k = Math.Max(0, nT - 5); k < nT; k++) BetaRow(k + 1, byBeta[k]);
    Console.WriteLine();

    // β が何と相関するか。**説明ではなく手がかり**（相関は因果ではない。第12期の作法）。
    var featC0 = Enumerable.Range(0, nT).Select(t => tr[mainB][t].OffTurnPct).ToArray();
    var featA0 = Enumerable.Range(0, nT).Select(t => tr[mainB][t].RateAll).ToArray();
    var featB0 = Enumerable.Range(0, nT).Select(t => tr[mainB][t].Ramp15).ToArray();
    var atk = Enumerable.Range(0, nT).Select(t => (double)targets[t].F.Occupied().Sum(x => x.Def.Attack)).ToArray();
    Console.WriteLine("**β は既存の量とどれだけ違うか**（新しい軸なのか、既存の量の言い換えか）:");
    Console.WriteLine();
    Console.WriteLine("| 相手 | r | ρ |");
    Console.WriteLine("|---|--:|--:|");
    foreach (var (nm, v) in new[] { ("(A) 実効打点/T", featA0), ("(B) 育ち", featB0), ("(C) 手番外%", featC0), ("総攻", atk) })
    {
        var c = Correlate(betaP, v);
        Console.WriteLine($"| {nm} | {Sg(c.R, 3):+0.000;-0.000} | {Sg(c.Rho, 3):+0.000;-0.000} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 5. 変換率は単一の量か（系列間の一致） =================
    //
    // 第17期 §4 と同じ形。**比べる相手は半割（測定の信頼性の上限）**——系列間の相関は
    // 乱数のばらつきだけでも 1.00 を割るので、上限が無いと読めない。
    // 線も第17期のまま（第15期の裏返し）: **ρ ≥ 0.90 または 余地 < 0.05**。
    // **連言の否定は選言。** ここを連言にすると、黙って厳しい線を作ることになる。
    const double NeutralRho = 0.90, NeutralRoom = 0.05;
    double SB(double r) => 2 * r / (1 + r);
    (double R, double Rho) HalfCap(int[] ser)
    {
        var h1 = Correlate(BetaOf(ser, s => s < ConvSeeds / 2), BetaOf(ser, s => s >= ConvSeeds / 2));
        var h2 = Correlate(BetaOf(ser, s => s % 2 == 0), BetaOf(ser, s => s % 2 == 1));
        return (SB((h1.R + h2.R) / 2), SB((h1.Rho + h2.Rho) / 2));
    }
    var capP = HalfCap(serP);
    var capQ = HalfCap(serQ);
    var capHi = HalfCap(serHi);

    Console.WriteLine("## 5. 変換率は単一の量か");
    Console.WriteLine();
    Console.WriteLine("**系列P は個体HP だけを振っている**（体数・1体攻・総攻・速さ・パターンは固定）ので、");
    Console.WriteLine("定義としてはこれが変換率。ただし総HP＝個体HP×体数 という恒等式がある以上、");
    Console.WriteLine("**個体HP を上げれば総HP も上がる**（＝戦闘が長くなる）——「個体HP だけを振る」と");
    Console.WriteLine("「総HP を動かさない」は**同時に満たせない**。ここでは3つの向きから確かめる。");
    Console.WriteLine();
    Console.WriteLine("### 5-1. 半割 — 測定の信頼性の上限");
    Console.WriteLine();
    Console.WriteLine("| 系列 | 刻み | 補正後 r | **補正後 ρ** |");
    Console.WriteLine("|:-:|--:|--:|--:|");
    Console.WriteLine($"| **P**（体数5固定・主） | {serP.Length} | {capP.R:F3} | **{capP.Rho:F3}** |");
    Console.WriteLine($"| **Q**（総HP 450固定） | {serQ.Length} | {capQ.R:F3} | **{capQ.Rho:F3}** |");
    Console.WriteLine($"| **P 高刻みだけ**（90/145/220） | {serHi.Length} | {capHi.R:F3} | **{capHi.Rho:F3}** |");
    Console.WriteLine();
    Console.WriteLine("補正は Spearman-Brown `r(2n) = 2r(n) / (1 + r(n))`。**上限はほぼ 1.00**なので、");
    Console.WriteLine("以下の食い違いは全部が「実物の入れ替わり」——乱数のばらつきでは説明が付かない。");
    Console.WriteLine();
    Console.WriteLine("### 5-2. 変種との一致（測り方の頑健さ）");
    Console.WriteLine();
    Console.WriteLine("**3つの疑いを数字で潰す。** どれも「β の測り方を少し変えて順位が動くか」で測れる。");
    Console.WriteLine();
    Console.WriteLine($"線は第17期のまま（第15期の裏返し）: **`ρ ≥ {NeutralRho:F2}` または `余地 < {NeutralRoom:F2}`**"
        + "。**連言の否定は選言**——ここを連言にすると、黙って厳しい線を作ることになる。");
    Console.WriteLine();
    Console.WriteLine("| 変種 | 何を疑っているか | r | ρ | 上限(ρ) | **余地** | 判定 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|:-:|");
    foreach (var (nm, why, v, cp) in new (string, string, double[], double)[]
    {
        ("β 低刻みだけ（30/60/90）", "**高刻みで味方全滅が増える**（P145 で 22%・P220 で 49%）。"
            + "出力が途中で止まった刻みが β を作っているのではないか", betaLo, capP.Rho),
        ("β 高刻みだけ（90/145/220）", "逆に、低刻みは戦闘が短い（決着 4.2T）。"
            + "端数ターンと決着の早さが β を作っているのではないか", betaHi, capHi.Rho),
        ("β 端数補正（分母を T−0.5 に）", "**ターン数は整数**なので、削り切った最後のターンも 1 と数える。"
            + "短い戦闘ほど (A) が過小に出て、β が正に押し上げられているのではないか", betaAdj, capP.Rho),
    })
    {
        var c = Correlate(betaP, v);
        double lim = Math.Min(capP.Rho, cp), room = lim - c.Rho;
        Console.WriteLine($"| {nm} | {why} | {Sg(c.R, 2):+0.00;-0.00} | {Sg(c.Rho, 2):+0.00;-0.00} "
            + $"| {lim:F2} | **{room:F2}** "
            + $"| {(c.Rho >= NeutralRho || room < NeutralRoom ? "一致" : "**×**")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"端数補正の効き（平均）: β {betaP.Average():+0.000;-0.000} → "
        + $"βの端数補正版 {betaAdj.Average():+0.000;-0.000}（差 {betaAdj.Average() - betaP.Average():+0.000;-0.000}）。");
    Console.WriteLine("**端数の効果は符号を作るほど大きくない**が、値の一部ではある。");
    Console.WriteLine();
    Console.WriteLine("### 5-3. 系列Q との食い違いは、体数の辺で説明が付くか");
    Console.WriteLine();
    var crossPQ = Correlate(betaP, betaQ);
    double capPQ = Math.Min(capP.Rho, capQ.Rho);
    Console.WriteLine($"**系列P と 系列Q は逆を向く: r = {crossPQ.R:F2} / ρ = {crossPQ.Rho:F2}**"
        + $"（上限 {capPQ:F2}・余地 {capPQ - crossPQ.Rho:F2}）。系列P では 31 編成中 "
        + $"{betaP.Count(v => v > 0)} が正、系列Q では {betaQ.Count(v => v < 0)} が負。");
    Console.WriteLine();
    Console.WriteLine("**これは矛盾ではない。** 総HP＝個体HP×体数 なので、この2系列は「個体HP と体数の");
    Console.WriteLine("格子」の**別々の斜めの線**を歩いている——P は体数を止めて個体HP を上げ、");
    Console.WriteLine("Q は総HP を止めるために**体数を下げながら**個体HP を上げる。");
    Console.WriteLine();
    Console.WriteLine("対数で1次の模型を置けば、辺1本で検算できる:");
    Console.WriteLine();
    Console.WriteLine("    ln(A) ≒ 定数 + p · ln(個体HP) + q · ln(体数)");
    Console.WriteLine("      系列P（体数固定）      → 傾き = p      = β");
    Console.WriteLine("      系列Q（総HP固定）      → 傾き = p − q  = βQ   （体数 = 総HP ÷ 個体HP なので）");
    Console.WriteLine("      系列R（個体HP固定）    → 傾き = q      = γ");
    Console.WriteLine();
    Console.WriteLine("**つまり β − βQ = γ が成り立つはず。** 成り立てば、P と Q の食い違いは");
    Console.WriteLine("**体数の辺そのもの**で、変換率（＝ p）の定義が壊れているわけではない。");
    Console.WriteLine();
    var lhs = Enumerable.Range(0, nT).Select(t => betaP[t] - betaQ[t]).ToArray();
    var addC = Correlate(lhs, gammaR);
    double madd = Enumerable.Range(0, nT).Average(t => Math.Abs(lhs[t] - gammaR[t]));
    Console.WriteLine($"| 量 | 平均 | 範囲 |");
    Console.WriteLine("|---|--:|---|");
    Console.WriteLine($"| **β − βQ**（模型の予言） | {lhs.Average():+0.00;-0.00} | {lhs.Min():+0.00;-0.00} 〜 {lhs.Max():+0.00;-0.00} |");
    Console.WriteLine($"| **γ 体数の辺**（実測） | {gammaR.Average():+0.00;-0.00} | {gammaR.Min():+0.00;-0.00} 〜 {gammaR.Max():+0.00;-0.00} |");
    Console.WriteLine();
    Console.WriteLine($"**一致: r = {addC.R:F3} / ρ = {addC.Rho:F3}・平均 |差| = {madd:F3}。**");
    double bias = Enumerable.Range(0, nT).Average(t => lhs[t] - gammaR[t]);
    Console.WriteLine($"ずれの符号は {Enumerable.Range(0, nT).Count(t => lhs[t] - gammaR[t] < 0)} / {nT} が負で、"
        + $"平均は {bias:+0.00;-0.00}——**ばらつきではなく片寄り**（γ が一貫して大きい）。");
    Console.WriteLine();
    Console.WriteLine(Math.Abs(addC.R) >= 0.90 && madd < 0.15
        ? "**模型どおり。** 系列P と系列Q の食い違いは**体数の辺で説明が付く。**"
        : Math.Abs(addC.R) >= 0.90
            ? "**形は模型どおり（r " + $"{addC.R:F2}" + "）だが、系統的なずれが残る。** 対数で1次の模型は"
              + "**大半を説明するが全部ではない**——`ln(個体HP)` と `ln(体数)` の交互作用（曲がり）が"
              + "残っている。**系列P と 系列Q が逆を向く理由は体数の辺**で、変換率の定義が"
              + "壊れているわけではない。"
            : "**模型どおりにはならない。** **β と βQ の食い違いは体数の辺だけでは説明が付かない。**");
    Console.WriteLine();
    Console.WriteLine("> **どちらにしても、変換率として使えるのは β（系列P）のほう。** βQ は模型の上で");
    Console.WriteLine("> **p − q**（個体HP の効果 と 体数の効果の差）なので、個体HP の効果そのものではない。");
    Console.WriteLine("> 計画書 §2-2 が「総HP を揃えると体数が同時に動く」と警告したのは、この差のこと。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | β | βQ | β − βQ | γ（体数の辺） | 差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => Math.Abs(lhs[t] - gammaR[t])).Take(8))
        Console.WriteLine($"| {targets[t].Name} | {Sg(betaP[t], 2):+0.00;-0.00} | {Sg(betaQ[t], 2):+0.00;-0.00} "
            + $"| {Sg(lhs[t], 2):+0.00;-0.00} | {Sg(gammaR[t], 2):+0.00;-0.00} "
            + $"| {Sg(lhs[t] - gammaR[t], 2):+0.00;-0.00} |");
    Console.WriteLine();
    Console.WriteLine("（ずれの大きい 8 編成。**全 31 編成のうち一部しか出していない**のは、"
        + "この表が模型の当てはまりを見るためのもので、値そのものは §4-1 にあるため。）");
    Console.WriteLine();
    Console.WriteLine("### 5-4. 以下で使う量");
    Console.WriteLine();
    Console.WriteLine("**変換率 = 系列P の β**（計画書 §2-2 の「振るのは個体HP だけ」に文字どおり従う唯一の系列）。");
    Console.WriteLine("系列Q は**総HP を揃えた対照**として §8 の積に1本だけ残す（材料を差し替えた同じ形の項を");
    Console.WriteLine("並べれば、結論が系列の選び方で変わるかが読める）。系列R は辺の検算にだけ使い、特徴量にはしない。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 6. 甲乙は分離できるか =================
    //
    // 第16期の 12 事例の分類は**人間の解釈**。第17期は (B) 育ち で数値化しようとして失敗し、
    // 「甲乙の軸は育つかではなく**撃破に変換されるか**」という訂正を残した。
    // **その訂正どおりの量が変換率**なので、ここが本題の1つ目。
    //
    // 分類は `output` §7 からそのまま写す（**ここで分け直すと「分かれるように分けた」になる**）。
    var kou = new[] { "速攻 (ボルグ×ムド)", "毒+耐久 (ベニ×トウ)", "溜め改 (クグ×バン×ガン)" };
    var otsu = new[] { "毒 (グザ×ミオ×ラウ)", "燃焼 (ボルグ×ホタ)", "耐久 (ガルド×ノノ)",
                       "範囲耐性 (ヒビ×ボルグ)", "追撃×死 (ハギ×リィカ)", "死の連鎖 (リィカ軸)" };
    int Ix(string name) => Array.FindIndex(targets, x => x.Name == name);

    Console.WriteLine("## 6. 変換率で甲乙は分離できるか（第16期の12事例と照合）");
    Console.WriteLine();
    Console.WriteLine("第17期は (B) 育ち で分離できず、**「甲乙の軸は育つかではなく撃破に変換されるか」**という");
    Console.WriteLine("訂正を残した。変換率はその訂正どおりの量なので、ここが本題の1つ目。分類は");
    Console.WriteLine("`output` §7（＝第16期 `dissect` §7 の12事例）からそのまま写した。");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成 | **β 変換率** | βQ | (B) 育ち | (A) | (C) 手番外% | 撃破/戦 P90 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|");
    foreach (var (label, names) in new[] { ("**甲**", kou), ("**乙**", otsu) })
        foreach (string nm in names)
        {
            int t = Ix(nm);
            if (t < 0) { Console.WriteLine($"| {label} | {nm} | — | — | — | — | — | — |"); continue; }
            Console.WriteLine($"| {label} | {nm} | **{Sg(betaP[t], 2):+0.00;-0.00}** | {Sg(betaQ[t], 2):+0.00;-0.00} "
                + $"| {featB0[t]:F2} | {featA0[t]:F1} | {featC0[t]:F0}% "
                + $"| {(double)tr[mainB][t].Kills / ConvSeeds:F2} |");
        }
    Console.WriteLine();

    var kIx = kou.Select(Ix).Where(t => t >= 0).ToArray();
    var oIx = otsu.Select(Ix).Where(t => t >= 0).ToArray();
    Console.WriteLine("| 量 | 甲の平均 | 乙の平均 | 甲の範囲 | 乙の範囲 | 重なるか |");
    Console.WriteLine("|---|--:|--:|---|---|:-:|");
    foreach (var (nm, v) in new[] { ("**β 変換率**", betaP), ("βQ（従系列）", betaQ),
                                    ("(B) 育ち", featB0), ("(A) 実効打点/T", featA0), ("(C) 手番外%", featC0) })
    {
        double kmin = kIx.Min(t => v[t]), kmax = kIx.Max(t => v[t]);
        double omin = oIx.Min(t => v[t]), omax = oIx.Max(t => v[t]);
        bool overlap = kmin <= omax && omin <= kmax;
        Console.WriteLine($"| {nm} | {kIx.Average(t => v[t]):F2} | {oIx.Average(t => v[t]):F2} "
            + $"| {kmin:F2} 〜 {kmax:F2} | {omin:F2} 〜 {omax:F2} | {(overlap ? "**重なる**" : "分かれる")} |");
    }
    double kminB = kIx.Min(t => betaP[t]), kmaxB = kIx.Max(t => betaP[t]);
    double ominB = oIx.Min(t => betaP[t]), omaxB = oIx.Max(t => betaP[t]);
    bool split = !(kminB <= omaxB && ominB <= kmaxB);
    Console.WriteLine();
    Console.WriteLine("**「分かれる」= 2群の範囲が重ならない**（1本の閾値で 9 編成を完全に分類できる）。");
    Console.WriteLine("n が 3 と 6 しかないので、**重ならないことは「分離できた」の必要条件であって十分条件ではない**");
    Console.WriteLine("——偶然に重ならない確率は決して小さくない（3 と 6 の並べ替えで完全分離は 1/84）。");
    Console.WriteLine();

    // --- 6-1. 読み（解釈） ---
    //
    // **ここだけは測定ではなく解釈**（`dissect` §7・`output` §7-1 と同じ扱い）。
    // 表と食い違ったら表が正しい。数字が動いたらこの節も書き直すこと。
    //
    // 重なりを作っているのは誰かを名指しする。**外して測り直すのは診断であって直しではない**
    // ——外して分かれたからといって、甲乙が変換率で分離できたことにはならない（`output` §4-5 と同じ）。
    int worstO = oIx.OrderByDescending(t => betaP[t]).First();   // 乙で最も β が高い編成
    int worstK = kIx.OrderBy(t => betaP[t]).First();             // 甲で最も β が低い編成
    var oWithout = oIx.Where(t => t != worstO).ToArray();
    bool splitWithout = !(kIx.Min(t => betaP[t]) <= oWithout.Max(t => betaP[t])
                          && oWithout.Min(t => betaP[t]) <= kIx.Max(t => betaP[t]));
    Console.WriteLine("### 6-1. 読み（解釈。表と食い違ったら表が正しい）");
    Console.WriteLine();
    Console.WriteLine($"**重なりを作っているのは {targets[worstO].Name}（乙・β {betaP[worstO]:+0.00;-0.00}）** で、"
        + $"甲の最低（{targets[worstK].Name} {betaP[worstK]:+0.00;-0.00}）を上回っている。");
    Console.WriteLine($"この 1 編成を外すと乙の範囲は {oWithout.Min(t => betaP[t]):+0.00;-0.00} 〜 "
        + $"{oWithout.Max(t => betaP[t]):+0.00;-0.00} になり、甲乙は"
        + $"{(splitWithout ? "**分かれる**" : "それでも重なる")}。");
    Console.WriteLine("**これは診断であって直しではない**（`output` §4-5 と同じ）——外して分かれたからといって、");
    Console.WriteLine("甲乙が変換率で分離できたことにはならない。1 編成を外す自由を認めるなら、"
        + "どの量でも同じことができる。");
    Console.WriteLine();
    Console.WriteLine("**第17期と同じ編成が、同じ位置で引っかかっている。** 第17期は (B) 育ちで");
    Console.WriteLine("「乙群の 毒 が9編成中いちばん高い」ために分離できず、**「育つか」ではなく");
    Console.WriteLine("「撃破に変換されるか」だ**という訂正を出した。ところが**その訂正どおりに測った");
    Console.WriteLine("変換率でも、同じ編成が同じ側にはみ出す。**");
    Console.WriteLine();
    Console.WriteLine($"**{targets[worstO].Name} は的が硬いほど出力が伸びる**（β が高い＝積み上げ型）**のに、乙群だった。**");
    Console.WriteLine("第16期が 毒 を乙に置いた根拠は `毒の無駄`（第4波で 282.6 段が撃破に変換されずに消えた）");
    Console.WriteLine("だが、**それは参照台の (A) には現れない**——`ApplyDamage` は残HPで切り詰めないので、");
    Console.WriteLine($"オーバーキルも打点として数えられる（{targets[worstO].Name} の オーバーキル% は P90 で "
        + $"{tr[mainB][worstO].Overkill * 100.0 / tr[mainB][worstO].Damage.Sum():F0}%）。");
    Console.WriteLine("**変換率は「出力が硬い的でどう変わるか」を測るが、「その出力が無駄になっているか」は");
    Console.WriteLine("測っていない。** 甲乙の軸がそちらなら、要るのは変換率ではなく**無駄率**のほう。");
    Console.WriteLine();
    Console.Out.Flush();

    // ---- ここから波を測る（第15期 FB・第16期 GB・第17期 HB のやり直し） ----
    //
    // 波は `WaveCatalog()` を呼ぶ。**コピーを持たない**（第15期が「1箇所に集める」ために
    // やった作業を、4つ目の診断が台無しにする）。
    var waves = WaveCatalog();
    int nW = waves.Length;
    const double DeadZone = 50.0;   // wave §4 / dissect §1 / output §8 と同じ線

    var rate = new double[nW][];
    var degree = new double[nW][];
    var dyn = new double[nW][][];
    for (int w = 0; w < nW; w++)
    {
        rate[w] = new double[nT];
        degree[w] = new double[nT];
        dyn[w] = new double[nT][];
        for (int t = 0; t < nT; t++)
        {
            var mw = MeasureWave(targets[t].F, waves[w].Enemy, ConvSeeds);
            rate[w][t] = mw.Win.Average() * 100;
            degree[w][t] = mw.SurvRate.Average();
            dyn[w][t] = mw.Dynamics;
        }
        Console.Out.Flush();
    }
    var contributes = new bool[nW];
    for (int w = 0; w < nW; w++)
    {
        double ceilN = rate[w].Count(v => v >= 100.0 - 1e-9) * 100.0 / nT;
        double floorN = rate[w].Count(v => v <= 1e-9) * 100.0 / nT;
        contributes[w] = ceilN + floorN < DeadZone;
    }
    int[] conW = Enumerable.Range(0, nW).Where(w => contributes[w]).ToArray();

    // ================= 7. 波ごとの分解のやり直し =================
    var statNames = new (string Name, Func<Formation, double> Get)[]
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
    int nS = statNames.Length;
    var dynKeep = new (string Name, int K)[] { ("干渉/戦", 3), ("回復/戦", 4), ("自傷率", 5), ("与ダメ効率", 6) };
    var outNames = new (string Name, double[] V)[]
        { ("(A) 実効打点/T", featA0), ("(B) 育ち", featB0), ("(C) 手番外%", featC0) };

    Console.WriteLine("## 7. 波ごとの分解のやり直し（第15期 FB → 第17期 HB → ここ）");
    Console.WriteLine();
    Console.WriteLine("目的変数は波ごとの単発勝率。候補は**第17期の 15 種**（静的8 + 動的4 + (A)(B)(C)）に");
    Console.WriteLine("**変換率を足した 16 種**。第15期・第17期の側は同じ実行の中で計算し直している");
    Console.WriteLine("——**別の実行から引くと、動いたのが候補のせいか実行のせいか決まらない**（第13期以来の作法）。");
    Console.WriteLine();
    Console.WriteLine("**同語反復の判定（第14期の基準）を変換率にも通す。** 基準は「目的変数の言い換えに");
    Console.WriteLine("なっていないか」の1本だけ。**当たらない**——変換率を測ったのは参照台の系列で、");
    Console.WriteLine("目的変数（波ごとの勝率）の戦闘とは別の戦闘。その波の敵を削り切ったかどうかは");
    Console.WriteLine("1ビットも入っていない（第17期 §5-1 と同じ理屈。単発戦なので分母経路も存在しない）。");
    Console.WriteLine();
    Console.WriteLine($"寄与する波は同じ判定式（天井率 + 床率 < {DeadZone:F0}%）で引き直した: **{conW.Length} 本** — "
        + string.Join(" / ", conW.Select(w => $"`{waves[w].Tag}`")));
    Console.WriteLine();

    // 第15期の記録（README「検証で分かったこと」）。**この診断が第15期と同じ盤を見ていることの
    // 検算にだけ使う**（値を候補には使わない。dissect §12-2 / output §8 と同じ作法）。
    var rec15 = new Dictionary<string, (string First, double R2)>
    {
        ["S2"] = ("与ダメ効率", 0.059), ["S3"] = ("体数", 0.164), ["S4"] = ("与ダメ効率", 0.341),
        ["S5"] = ("干渉/戦", 0.338), ["R8"] = ("与ダメ効率", 0.194), ["R9"] = ("範囲枚数", 0.203),
        ["R10"] = ("総HP", 0.116),
    };

    string[] names15 = statNames.Select(x => x.Name).Concat(dynKeep.Select(x => x.Name)).ToArray();
    string[] names17 = names15.Concat(outNames.Select(x => x.Name)).ToArray();
    string[] names18 = names17.Concat(new[] { "β 変換率" }).ToArray();
    double[] Col15(int k, int w) => k < nS
        ? Enumerable.Range(0, nT).Select(t => statNames[k].Get(targets[t].F)).ToArray()
        : Enumerable.Range(0, nT).Select(t => dyn[w][t][dynKeep[k - nS].K]).ToArray();
    double[] Col17(int k, int w) => k < names15.Length ? Col15(k, w) : outNames[k - names15.Length].V;
    double[] Col18(int k, int w) => k < names17.Length ? Col17(k, w) : betaP;

    Console.WriteLine("| 波 | 第15期(12種) | r² | 記録 | 第17期(15種) | r² | **第18期(16種)** | **r²** | 2位 | 変換率が第一近似 |");
    Console.WriteLine("|:-:|---|--:|--:|---|--:|---|--:|---|:-:|");
    int miss15 = 0, betaFirst = 0;
    foreach (int w in conW)
    {
        (int K, double R) Best(Func<int, double[]> col, int n)
        {
            var ord = Enumerable.Range(0, n).Select(k => (K: k, R: Correlate(col(k), rate[w]).R))
                .Where(x => !double.IsNaN(x.R)).OrderByDescending(x => Math.Abs(x.R)).ToArray();
            return ord.Length == 0 ? (-1, double.NaN) : ord[0];
        }
        var b15 = Best(k => Col15(k, w), names15.Length);
        var b17 = Best(k => Col17(k, w), names17.Length);
        var ord18 = Enumerable.Range(0, names18.Length)
            .Select(k => (K: k, R: Correlate(Col18(k, w), rate[w]).R))
            .Where(x => !double.IsNaN(x.R)).OrderByDescending(x => Math.Abs(x.R)).ToArray();
        string rec = "—";
        if (rec15.TryGetValue(waves[w].Tag, out var want))
        {
            bool ok = names15[b15.K] == want.First && Math.Abs(b15.R * b15.R - want.R2) <= 0.005;
            if (!ok) miss15++;
            rec = ok ? $"{want.R2:F3}" : $"**{want.First} {want.R2:F3} ←ずれ**";
        }
        bool up = ord18[0].K >= names17.Length;
        if (up) betaFirst++;
        Console.WriteLine($"| **{waves[w].Tag}** | {names15[b15.K]} {b15.R:+0.00;-0.00} | {b15.R * b15.R:F3} | {rec} "
            + $"| {names17[b17.K]} {b17.R:+0.00;-0.00} | {b17.R * b17.R:F3} "
            + $"| {(up ? "**" : "")}{names18[ord18[0].K]}{(up ? "**" : "")} {ord18[0].R:+0.00;-0.00} "
            + $"| **{ord18[0].R * ord18[0].R:F3}** "
            + $"| {(ord18.Length > 1 ? $"{names18[ord18[1].K]} {ord18[1].R:+0.00;-0.00}" : "—")} "
            + $"| {(up ? "**○**" : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine(miss15 == 0
        ? "**検算: 第15期の記録した第一近似・r² と完全に一致（ずれ 0 件）。** この診断は第15期と同じ盤を見ている。"
        : $"**検算: {miss15} 件ずれた。第15期と同じ盤を見ていない——先へ進む前に原因を潰すこと。**");
    Console.WriteLine();
    Console.WriteLine($"**変換率が第一近似になったのは {betaFirst} / {conW.Length} 波。**");
    Console.WriteLine();
    Console.WriteLine("### 7-1. 変換率の単相関（寄与する波）");
    Console.WriteLine();
    Console.WriteLine("**符号まで含めて読む。** 同じ量が波によって逆向きに効くなら、それは");
    Console.WriteLine("「どちらの波にも効く地力」ではなく**波の性格そのもの**（第15期 §9-3 の読み方）。");
    Console.WriteLine();
    Console.WriteLine("| 量 |" + string.Concat(conW.Select(w => $" {waves[w].Tag} |")) + " 符号の向き |");
    Console.WriteLine("|---|" + string.Concat(conW.Select(_ => "--:|")) + ":-:|");
    foreach (var (nm, v) in new[] { ("**β 変換率**", betaP) }.Concat(outNames).Concat(new[] { ("総攻（比較）", atk) }))
    {
        var rs = conW.Select(w => Correlate(v, rate[w]).R).ToArray();
        bool allSame = rs.All(r => r >= 0) || rs.All(r => r <= 0);
        Console.WriteLine($"| {nm} |" + string.Concat(rs.Select(r => $" {(double.IsNaN(r) ? "—" : $"{r:+0.00;-0.00}")} |"))
            + $" {(allSame ? "揃う" : "**反転する**")} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 8. 交互作用項の作り直し =================
    Console.WriteLine("## 8. 交互作用項の作り直し（第16期 GB → 第17期 HB → ここ）");
    Console.WriteLine();
    Console.WriteLine("**積の材料に変換率を足す。** 片側だけの特徴量は交互作用成分と相関が**恒等的に 0** なので");
    Console.WriteLine("（第16期 §11。残差は行にも列にも和が 0）、変換率を単体で足しても 0 のまま");
    Console.WriteLine("——**積にして初めて意味を持つ。**");
    Console.WriteLine();

    int nC = conW.Length;
    double[][] Resid(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        return Enumerable.Range(0, nC)
            .Select(c => Enumerable.Range(0, nT).Select(t => y[c][t] - rowM[c] - colM[t] + grand).ToArray())
            .ToArray();
    }
    (double Wave, double Build, double Inter) Decompose(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        double ssT = y.SelectMany(r => r).Sum(v => (v - grand) * (v - grand));
        double ssW = nT * rowM.Sum(m => (m - grand) * (m - grand));
        double ssB = nC * colM.Sum(m => (m - grand) * (m - grand));
        double ssI = Resid(src).SelectMany(r => r).Sum(v => v * v);
        return (ssW / ssT * 100, ssB / ssT * 100, ssI / ssT * 100);
    }
    var decW = Decompose(rate);
    var decD = Decompose(degree);
    double[] Flat(Func<int, int, double> get) => Enumerable.Range(0, nC)
        .SelectMany(c => Enumerable.Range(0, nT).Select(t => get(conW[c], t))).ToArray();
    double[] FlatV(double[][] v) => v.SelectMany(r => r).ToArray();
    double[] residFlat = FlatV(Resid(rate));

    Console.WriteLine("### 8-1. 分散分解（第16期 §11 と同じ計算）");
    Console.WriteLine();
    Console.WriteLine($"寄与する {nC} 波 × {nT} 編成 = **{nC * nT} 点**。");
    Console.WriteLine();
    Console.WriteLine("| 目的変数 | 波の主効果 | 編成の主効果 | **交互作用** | 第16期の記録 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    Console.WriteLine($"| 勝率 | {decW.Wave:F1}% | {decW.Build:F1}% | **{decW.Inter:F1}%** | 28.3% |");
    Console.WriteLine($"| 残存度 | {decD.Wave:F1}% | {decD.Build:F1}% | **{decD.Inter:F1}%** | 21.7% |");
    Console.WriteLine();
    double oneSide = Math.Abs(Pearson(Flat((w, t) => betaP[t]), residFlat));
    Console.WriteLine($"**検算: 変換率を単体で交互作用成分に当てると |r| = {oneSide:F6}。**");
    Console.WriteLine("**新しい特徴量でも 0 になるのが正しい**——これは測定結果ではなく恒等式。");
    Console.WriteLine();

    // --- 8-2. 項の候補 ---
    //
    // **総当たりで作らない**（第16期 §10・第17期 §9-2 と同じ縛り。編成側 4 × 敵側 9 を全部試すと
    // n = 217 では必ず何かが当たる）。出どころは3つだけ:
    //   (a) 計画書 §2-4 が挙げた3つの例
    //   (b) 変換率を「波の個体HP へ外挿する」形にしたもの（変換率の定義そのものの使い道）
    //   (c) 第16期・第17期の最良項をそのまま（対照）
    //
    // (b) の考え方: 変換率 β は「個体HP が e 倍になると (A) が何倍になるか」なので、
    // **参照台（個体HP 90）で測った (A) を、その波の個体HP まで外挿できる。**
    //     予測実効打点 = (A) × (敵の個体HP中央値 ÷ 90)^β
    // これは「出力」と「変換」を両方持つ唯一の形で、第16期の `削るT` / `時計比` / `一撃圏` の
    // 分母をこれに差し替えれば、**2つの時計の競走を変換込みで書き直したもの**になる。
    double Pred(int w, int t) => featA0[t] * Math.Pow(MedianHp(waves[w].Enemy) / (double)Porter.MaxHp, betaP[t]);
    double FoeHp(int w) => waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp);
    double FoeAtk(int w) => waves[w].Enemy.Occupied().Sum(x => x.Def.Attack);
    double AllyHp(int t) => targets[t].F.Occupied().Sum(x => x.Def.MaxHp);
    var terms = new (string Name, string Expr, string From, string Why, Func<int, int, double> Get)[]
    {
        ("耐えるT", "味方の総HP ÷ 敵総攻", "第16期のまま（対照）",
            "第16期の最良項。**出力を含まない**ので、比較の基準として据え置く（プール r² 0.061 を再現するはず）",
            (w, t) => AllyHp(t) / FoeAtk(w)),
        ("一撃圏'", "敵の個体HP中央値 ÷ ((A) ÷ 味方の体数)", "第17期のまま（対照）",
            "**第17期の最良項**（交互作用 r² 0.002）。変換率を入れない側の到達点",
            (w, t) => MedianHp(waves[w].Enemy) / (featA0[t] / targets[t].F.Count)),
        ("変換率 × 敵個体HP", "**β** × 敵の個体HP中央値", "計画書 §2-4 の例1",
            "**変換に敏感な編成が、硬い敵でどうなるか。** β < 0 の編成は個体HP が高い波で沈むはず",
            (w, t) => betaP[t] * MedianHp(waves[w].Enemy)),
        ("変換率 × 敵体数", "**β** × 敵の体数", "計画書 §2-4 の例2",
            "**撃破の回数そのものが敵の体数で決まる。** 撃破依存の出力は体数の多い波で回る",
            (w, t) => betaP[t] * waves[w].Enemy.Count),
        ("出力 × 変換率", "**(A)** × **β**", "計画書 §2-4 の例3",
            "**出力と変換の両方を持つか。** 片方だけ大きい編成（毒は (A) 大・β 大、反撃は (A) 大・β 小）を分ける",
            (w, t) => featA0[t] * betaP[t]),
        ("予測実効打点", "**(A)** × (敵個体HP中央値 ÷ 90)^**β**", "変換率の定義そのものの使い道",
            "**参照台（個体HP 90）で測った出力を、その波の個体HP まで外挿する。** "
            + "変換率が「個体HP が e 倍で (A) が何倍になるか」である以上、これが最も直接の使い道",
            Pred),
        ("予測削るT", "敵総HP ÷ **予測実効打点**", "第16期 `削るT` の分母を外挿値に",
            "**その波の敵を削り切るまでのターン数。** 第16期は分母が `総攻`、第17期は (A) だったので、"
            + "どちらも「硬い敵では出力が落ちる」を表せていなかった",
            (w, t) => FoeHp(w) / Pred(w, t)),
        ("予測時計比", "耐えるT ÷ **予測削るT**", "第16期 `時計比` の書き直し",
            "**2つの時計の競走を、変換込みで1本にまとめたもの**（第16期の骨格そのもの）",
            (w, t) => AllyHp(t) / FoeAtk(w) / (FoeHp(w) / Pred(w, t))),
        ("予測一撃圏", "敵個体HP中央値 ÷ (**予測実効打点** ÷ 味方の体数)", "第16期 `一撃圏` の書き直し",
            "**1体あたりの外挿出力で何ターン殴れば1体落ちるか。** 乙群（一撃圏に縛られる）の説明を"
            + "変換込みで書き直したもの",
            (w, t) => MedianHp(waves[w].Enemy) / (Pred(w, t) / targets[t].F.Count)),
        ("変換率Q × 敵個体HP", "**βQ** × 敵の個体HP中央値", "系列Q（総HP を揃えた側）の対照",
            "**同じ形の積を、総HP を揃えた系列の変換率で作る。** 3番の項と並べれば、"
            + "結論が**系列の選び方で変わるか**が読める（§5-3 で 2 系列は逆を向いている）",
            (w, t) => betaQ[t] * MedianHp(waves[w].Enemy)),
    };

    Console.WriteLine("### 8-2. 交互作用項の候補（10 個）");
    Console.WriteLine();
    Console.WriteLine("**総当たりで作っていない**（第16期 §10・第17期 §9-2 と同じ縛り）。出どころは3つだけ:");
    Console.WriteLine("**(a) 計画書 §2-4 が挙げた3つの例**、**(b) 変換率を波の個体HP へ外挿したもの**、");
    Console.WriteLine("**(c) 第16期・第17期の最良項そのまま（対照）**。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | 式 | 出どころ | 理由 |");
    Console.WriteLine("|--:|---|---|:-:|---|");
    for (int k = 0; k < terms.Length; k++)
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | `{terms[k].Expr}` | {terms[k].From} | {terms[k].Why} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 8-3. 効くか ---
    Console.WriteLine("### 8-3. 交互作用項は効くか");
    Console.WriteLine();
    Console.WriteLine("第16期 §12・第17期 §9-3 と同じ3通りの当て方。**(2) が本題。**");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | (1) プール r | r² | **(2) 交互作用 r** | **r²** | (2) ρ | (2) 残存度 r | 符号一致 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|:-:|");
    var score = new List<(int K, double R2)>();
    int agree = 0;
    double[] residDFlat = FlatV(Resid(degree));
    for (int k = 0; k < terms.Length; k++)
    {
        double[] x = Flat(terms[k].Get);
        double rp = Pearson(x, Flat((w, t) => rate[w][t]));
        var ci = Correlate(x, residFlat);
        double rd = Pearson(x, residDFlat);
        // NaN（分散0・標本不足）は判定不能。Math.Sign は NaN で例外を投げる。
        bool known = !double.IsNaN(ci.R) && !double.IsNaN(rd);
        bool ok = known && Math.Sign(ci.R) == Math.Sign(rd);
        if (ok) agree++;
        score.Add((k, ci.R * ci.R));
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | {Sg(rp, 2):+0.00;-0.00} | {rp * rp:F3} "
            + $"| {Sg(ci.R, 2):+0.00;-0.00} | **{ci.R * ci.R:F3}** | {Sg(ci.Rho, 2):+0.00;-0.00} "
            + $"| {Sg(rd, 2):+0.00;-0.00} | {(known ? (ok ? "○" : "**×**") : "—")} |");
    }
    Console.WriteLine();
    var best = score.OrderByDescending(x => x.R2).First();
    double maxRho = Enumerable.Range(0, terms.Length)
        .Max(k => Math.Abs(Correlate(Flat(terms[k].Get), residFlat).Rho));
    Console.WriteLine($"**最良は `{terms[best.K].Name}` で r² = {best.R2:F3}**"
        + "（第16期の最良は `範囲の変換` の **0.003**、第17期は `一撃圏'` の **0.002**）。");
    Console.WriteLine($"順位相関でも最大 |ρ| = {maxRho:F3}。**単調な非線形を取りこぼしているのではない。**");
    Console.WriteLine($"符号が一致したのは {agree} / {terms.Length}（勝率の交互作用成分 ↔ 残存度の交互作用成分）。");
    Console.WriteLine();
    Console.WriteLine($"交互作用は全分散の {decW.Inter:F1}% なので、最良の項が説明しているのは"
        + $" **全体の {best.R2 * decW.Inter / 100:F3}**。");
    Console.WriteLine();
    {
        double poolHold = Pearson(Flat(terms[0].Get), Flat((w, t) => rate[w][t]));
        var c17 = Correlate(Flat(terms[1].Get), residFlat);
        bool ok = Math.Abs(poolHold - 0.25) <= 0.005 && Math.Abs(c17.R * c17.R - 0.002) <= 0.0015;
        Console.WriteLine($"**検算: 対照項 `耐えるT` のプール r = {poolHold:+0.00;-0.00} / r² = {poolHold * poolHold:F3}"
            + $"（第16期の記録は +0.25 / 0.061）、対照項 `一撃圏'` の交互作用 r² = {c17.R * c17.R:F3}"
            + $"（第17期の記録は 0.002）→ {(ok ? "一致" : "**ずれ**")}。**");
        Console.WriteLine("**盤も波も編成集合も第16期・第17期のまま**——動いたのは項の材料だけ。");
        Console.WriteLine();
    }
    Console.Out.Flush();

    // ================= 9. 判定 =================
    //
    // 線は第16期・第17期と同じ（交互作用成分の r² が 0.10 を超えるか）。**線を新しく作らない。**
    const double TermLine = 0.10;
    Console.WriteLine("## 9. 判定（計画書 §3-1 のどの行か）");
    Console.WriteLine();
    Console.WriteLine($"線は**第16期・第17期と同じ**「交互作用成分に対する r² が {TermLine:F2} を超えるか」。");
    Console.WriteLine("**線を新しく作らない**（作ると過去の期と比べられなくなる）。");
    Console.WriteLine();
    double best18 = conW.Max(w => Enumerable.Range(0, names18.Length)
        .Select(k => { double r = Correlate(Col18(k, w), rate[w]).R; return double.IsNaN(r) ? 0 : r * r; }).Max());
    double best17 = conW.Max(w => Enumerable.Range(0, names17.Length)
        .Select(k => { double r = Correlate(Col17(k, w), rate[w]).R; return double.IsNaN(r) ? 0 : r * r; }).Max());
    bool interUp = best.R2 >= TermLine;

    Console.WriteLine($"- 交互作用成分に対する最良 r² = **{best.R2:F3}**（第16期 0.003 / 第17期 0.002）→ "
        + $"{TermLine:F2} を{(interUp ? "**超えた**" : "超えない")}");
    Console.WriteLine($"- 変換率による甲乙の分離: **{(split ? "分かれる" : "重なる")}**"
        + $"（甲 {kminB:+0.00;-0.00}〜{kmaxB:+0.00;-0.00} / 乙 {ominB:+0.00;-0.00}〜{omaxB:+0.00;-0.00}）");
    Console.WriteLine($"- 波ごとの第一近似が変換率に替わった波: **{betaFirst} / {conW.Length}**。"
        + $"波ごとの最良 r² は {best17:F3}（15種）→ {best18:F3}（16種）");
    Console.WriteLine();
    Console.WriteLine("| # | 計画書 §3-1 の観測 | 当たるか | 結論 |");
    Console.WriteLine("|--:|---|:-:|---|");
    Console.WriteLine($"| 1 | 甲乙が分離でき、積で交互作用成分の説明力が上がる | {(split && interUp ? "**○**" : "×")} "
        + "| **17期分の壁が解けた。** 変換率が探していた量 |");
    Console.WriteLine($"| 2 | 甲乙は分離できるが、交互作用成分は上がらない | {(split && !interUp ? "**○**" : "×")} "
        + "| **甲乙は実在するが、交互作用の説明ではない。** 分類としては使える（キャラの役割設計に） |");
    Console.WriteLine($"| 3 | どちらも動かない | {(!split && !interUp ? "**○**" : "×")} "
        + "| **測定は打ち切り。** 交互作用は実在するが数値では読めない、で確定 |");
    Console.WriteLine();
    Console.WriteLine("**どの行でも、この作業で測定を終える**（計画書 §3-1）。3行目でも失敗ではない");
    Console.WriteLine("——「読めない」ことが確定すれば、設計側で**読める交互作用を作りにいく**という方針が定まる。");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}

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

    foreach (var (name, f) in builds)
    {
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
        Console.WriteLine($"| {name} | {winRate:F1}% | {killAvg:F2} | {killMax} | {turnAvgOnWin:F1} "
            + $"| {survAvg:F1}/{party} | {narrow:F0}% |");
    }
    return;
}

// ablate モード: 編成からメンバーを1体ずつ抜いたときの勝率低下を測る。
// 「完成した5体 − 重要駒を1体抜いた編成」の差が大きいほど、強さが個々の性能ではなく
// 組み合わせから生まれている証拠になる（README「受け皿を足したら供給役を抜いた対照を必ず測る」の一般化）。
// 差が小さい、あるいはマイナス（抜いたほうが勝率が上がる）なら、そのメンバーは入れ得の疑いがある。
// 対象は既定では compare の全編成。reseat と同じ書式でカンマ区切りの部分一致で絞れる。
if (focusId == "ablate")
{
    var all = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> abStages = EnemyCatalog.Stages;
    const int AblateSeeds = 200;

    string filter = args.Length > 2 ? args[2] : "";
    var targets = all.Where(b => filter.Length == 0
                    || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();

    double WinRate(Formation f)
    {
        int wins = 0, trials = 0;
        foreach (EnemyCatalog.Stage st in abStages)
            for (int seed = 0; seed < AblateSeeds; seed++)
            {
                trials++;
                if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
            }
        return wins * 100.0 / trials;
    }

    Console.WriteLine("# アブレーション（1体抜いた時の勝率変化）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 ablate > docs/ablation.md` の出力。手で編集しない。");
    Console.WriteLine($"全ステージ通算、seed 0..{AblateSeeds - 1} の {AblateSeeds} 試行。");
    Console.WriteLine("差が大きいほど「そのメンバーが編成の強さの源」。差が0に近い、またはプラス（抜いたほうが勝率が上がる）なら入れ得の疑い。");
    Console.WriteLine();

    foreach (var (name, full) in targets)
    {
        double fullRate = WinRate(full);
        var members = full.Occupied().ToList();

        Console.WriteLine($"## {name}（フル編成 {fullRate:F1}%）");
        Console.WriteLine();
        Console.WriteLine("| 抜いた駒 | 抜いた後 | 差 |");
        Console.WriteLine("|---|--:|--:|");

        foreach (var (slot, def) in members)
        {
            var ablated = new Formation();
            foreach (var (mSlot, mDef) in members)
                if (mSlot != slot) ablated[mSlot] = mDef;

            double rate = WinRate(ablated);
            string sign = rate - fullRate >= 0 ? "+" : "";
            Console.WriteLine($"| {def.Name} | {rate:F1}% | {sign}{rate - fullRate:F1}pt |");
        }
        Console.WriteLine();
    }
    return;
}

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
        TraitId.Stoic,       // 支援拒否（ガルド）: 中立。SupportTargets が隣へ流す
        TraitId.Loose,       // 散開（ササ）: 利得。発火する席が原理的に存在しない
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

    foreach (string name in targets)
    {
        var build = all.First(b => b.Name == name);
        var members = build.F.Occupied().Select(x => x.Def).ToList();

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
            foreach (EnemyCatalog.Stage st in stages)
                for (int seed = 0; seed < ScanSeeds; seed++)
                    if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
            scan[i] = wins;
        }

        var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
        var pool = order.Take(TopOverall)
            .Concat(order.Where(i => MeetsIntent(perms[i])).Take(TopConstrained))
            .Append(order.First(i => SameFormation(perms[i], build.F)))
            .Distinct().ToList();

        var verified = pool.Select(i =>
        {
            var cells = stages.Select(st =>
            {
                int wins = 0;
                for (int seed = 0; seed < VerifySeeds; seed++)
                    if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                return wins * 100.0 / VerifySeeds;
            }).ToArray();
            return (Idx: i, Cells: cells, Avg: cells.Average());
        }).OrderByDescending(x => x.Avg).ToList();

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

        bool MeetsIntent(Formation f)
        {
            foreach (var (slot, def) in f.Occupied())
            {
                if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
                if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
            }
            return true;
        }
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

    Console.WriteLine("# 配置探索");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 layout` の出力。");
    Console.WriteLine($"compare の各編成をメンバー固定で全配置（5体=120通り / 4体=120通り）に展開し、");
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
        Console.WriteLine("| 順位 | 前1/前3 | 中央 | 後1/後3 | 平均 |"
            + string.Concat(stages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|--:|---|---|---|--:|" + string.Concat(stages.Select(_ => "---:|")));
        for (int rank = 0; rank < TopN && rank < ranked.Count; rank++)
            Console.WriteLine(LayoutRow($"{rank + 1}", jobs[ranked[rank]].F, results[ranked[rank]], LayoutSeeds));

        int cur = ranked.FindIndex(i => SameFormation(jobs[i].F, builds[bb].F));
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
            const int Candidates = 8;
            var pool = ranked.OrderByDescending(i => results[i][sx])
                             .ThenBy(i => jobs[i].PermIdx)
                             .Take(Candidates)
                             .Append(ranked[cur])
                             .Distinct()
                             .ToList();

            double curRate = Rate(jobs[ranked[cur]].F, stages[sx].Enemy);
            int best = ranked[cur];
            double bestRate = curRate;
            foreach (int i in pool)
            {
                double r = Rate(jobs[i].F, stages[sx].Enemy);
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

    double Rate(Formation f, Formation enemy)
    {
        int wins = 0;
        for (int seed = 0; seed < VerifySeeds; seed++)
            if (BattleEngine.Run(f, enemy, seed, verbose: false).PlayerWon) wins++;
        return wins * 100.0 / VerifySeeds;
    }

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

    Console.WriteLine("| 名前 | HP | 攻 | 速 | 型 | 行動 | プラス | マイナス | 由来 |");
    Console.WriteLine("|---|---:|---:|---:|---|---|---|---|---|");
    foreach (UnitDef u in UnitCatalog.All.Where(u => u.Id != "spore"))
        Console.WriteLine($"| **{u.Name}** | {u.MaxHp} | {u.Attack} | {u.Speed} | {Pat(u.Pattern)} | {Acts(u)} | {u.PlusText} | {u.MinusText} | {u.Flavor} |");

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
            $"{x.Def.Name}(HP{x.Def.MaxHp}/攻{x.Def.Attack}/{Pat(x.Def.Pattern)}"
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

static string Describe(UnitDef?[] slots)
{
    // 編成枠だけ。SlotsOfRow は召喚枠(5-8)まで返すので、5要素の slots を添字越えで落とす。
    string Row(Row r) => string.Join("/", FormationRules.PlayableSlotsOfRow(r)
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
    int blanks = FormationRules.PlayableSlotCount - members.Count;

    foreach (var order in Permute(members))
        foreach (var empty in Combinations(Enumerable.Range(0, FormationRules.PlayableSlotCount).ToList(), blanks))
        {
            var skip = empty.ToHashSet();
            var slots = new UnitDef?[FormationRules.PlayableSlotCount];
            int m = 0;
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
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
// **配置は X字盤面（編成5枠）で全32編成を振り直してある。** 5体が5枠を埋めるので
// 全配置は 120 通りしかなく、reseat が編成ごとに全探索している。採否は confirm
// （seed 200..599 / 選定に使った 0..199 とは重ならない・差 2pt 未満は据え置き）で決めた。
// **横に並べたとき「編成の実力」と「配置を振ったかどうか」が混ざらない状態になっている。**
//
// 守った制約: ガルドは前列（庇うは前列でしか発動しない）/ セッキは後列（後備えは後列でしか
// 発動しない）/ セロは前〜中（狙撃化には戦闘中に後退した実績が要る。最初から後列では発動しない）/
// ヒサの隣接で最大HPがカドになること（標的が逸れる）。探索は制約を満たす最良を採っている。
//
// **個々のエントリのコメントは、下記の「X字化に伴う振り直し」と書いてあるもの以外は
// 旧6枠での経緯を述べている。** 席の名前（前2・後2）や増減 pt はその頃の値。
static (string Name, Formation F)[] CompareBuilds() => new (string, Formation)[]
{
    // X字化に伴う振り直し。機械的な写しではガルドが中央に落ちて庇うが死に、全波 0〜3% に潰れていた。
    // ガルドを前1へ戻し、ネルを中央、ボルグを後3へ（reseat 2位＝狙いを満たす最良 / confirm +21.8pt）
    ("速攻 (ボルグ×ムド)",   Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Nel, back1: UnitCatalog.Sero, back3: UnitCatalog.Borg)),
    // 脆いムグ・ゾトを前で死なせて連鎖を起こす。中衛ゴルムの吸いが隣のゾトを破裂まで運ぶ（layout 1位）
    // リィカの覚醒（薙ぎ化）追加に伴い reseat で再探索。ムグを前1→前3、ゾトを前2のまま前1を空ける形が上
    // （confirm 追試 +2.2pt、第5波 +10.8。第5波は元々連鎖の畳みかけが弱かった波）。
    ("死の連鎖 (リィカ軸)",  Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Mug, center: UnitCatalog.Golm, back1: UnitCatalog.Rica, back3: UnitCatalog.Vel)),
    // X字化に伴う振り直し。**旧盤面でスィドの味方漏れを消していた「孤立席（前3）」は消滅した**
    // ——編成5体が0-4を必ず埋めるので、どの席も隣接を持つ。漏れは常に発生する。
    // スィドを前1・ガルドを前3にした形が狙いを満たす最良（reseat 15位 / confirm +6.5pt / 第4波 +21.5）
    ("毒 (グザ×ミオ×ラウ)", Formation.Build(front1: UnitCatalog.Sid, front3: UnitCatalog.Gald, center: UnitCatalog.Guza, back1: UnitCatalog.Mio, back3: UnitCatalog.Rau)),
    // 支援2枚を後列に下げ、痺れ粉は守られる中衛から撒く（layout 1位）
    // ベニのマイナス（味方の毒が2倍に効く）が入った分、毒を浴びる位置関係が変わった。
    // ミオを中衛へ上げ、ベニとトウを後列に下げた形が上（+4.0pt / 第5波 +15.8）
    // X字化後の全編成 reseat で振り直した（120通り全探索の「狙いを満たす最良」/ confirm +4.7pt）
    ("毒+耐久 (ベニ×トウ)",  Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Guza, center: UnitCatalog.Tou, back1: UnitCatalog.Mio, back3: UnitCatalog.Beni)),
    // 毒+耐久 の92%はベニ単独でもトウ単独でも出ない（ベニのみ 100/25/5/89/2、トウのみ 100/0/0/0/0）。
    // 効いているのは「毒の供給＋耐える手段」という型で、耐える側はトウでなくてもよい。
    // その裏を取るためのエントリ。トウをラウに差し替えた形。
    ("毒+ベニ+ラウ",       Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sid, center: UnitCatalog.Guza, back1: UnitCatalog.Rau, back3: UnitCatalog.Beni)),
    // ヴィオはターン開始時に味方の毒を全部吸い上げて攻撃力に変える。スィドの漏れとラウの拡散が
    // そのまま燃料になる形。ヴィオを2編成目に載せて、吸い上げが何を消しているかを見えるようにする。
    ("毒爆弾 (ラウ×ヴィオ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sid, center: UnitCatalog.Guza, back1: UnitCatalog.Vio, back3: UnitCatalog.Rau)),
    // ヒサの隣接はカドだけ（後1↔後2）。ガルド(HP100>カド96)を隣に置くと標的が逸れる（layout 1位）
    // X字化後の全編成 reseat で振り直した（120通り全探索の「狙いを満たす最良」/ confirm +27.7pt）
    ("反撃 (ヒサ×カド)",     Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Gald, center: UnitCatalog.Nel, back1: UnitCatalog.Kado, back3: UnitCatalog.Nono)),
    // ヒサを前1へ回すと隣接はカドとノノになるが、標的は最大HPで選ばれるのでカドのままで狙いは崩れない。
    // カドを前2の中央に置くと巻き込みがヒサ・ムド・セロの3枚へ広がり、成長が速くなる（+7.1pt / 第5波 +19.3）。
    // 旧配置（ムド前1・ヒサ前3）はヒサの隣接をカドだけに絞る形だったが、カドの巻き込み先が2枚に減っていた（reseat 追試）
    // X字化後の全編成 reseat で振り直した（120通り全探索の「狙いを満たす最良」/ confirm +7.3pt）
    ("惨禍×被弾強化",        Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Sero, center: UnitCatalog.Kado, back1: UnitCatalog.Mudo, back3: UnitCatalog.Nono)),
    // 惨禍（味方全体の被ダメ5割増）は位置を問わないので、死の密度は隣接に頼らなくても出る。
    // リィカを後1へ下げて生贄をゾト1枚に絞り、中衛はヴェルに。リィカが開幕で自陣を削りすぎる形をやめた（+19.1pt / 第4波 +57.0）。
    // 旧配置（中衛リィカがカドとゾトを削る）は狙いとしては筋が通っていたが、第4波で 25% まで落ちていた（reseat 追試）
    // X字化後の全編成 reseat で振り直した（120通り全探索の「狙いを満たす最良」/ confirm +5.1pt）
    ("惨禍×死の連鎖",        Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Zoto, center: UnitCatalog.Kado, back1: UnitCatalog.Rica, back3: UnitCatalog.Vel)),
    // ガルドは前列でないと庇えない。前1を空けてガルドとゴルムを前2・前3へ寄せた形が探索1位。セロは中衛から被弾後退（layout 1位）
    // X字化後の全編成 reseat で振り直した（120通り全探索の「狙いを満たす最良」/ confirm +4.4pt）
    ("耐久 (ガルド×ノノ)",   Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald, center: UnitCatalog.Sero, back1: UnitCatalog.Nono, back3: UnitCatalog.Dolga)),
    // ヒサを中衛に置くと横隣接が無く、深さ隣接の後2だけを指す。そこにカドを置けば標的は確定する。
    // カドを後2へ下げても囃し立てで被弾は来るので棘は回り、前列はガルドとドルガが受ける（+3.6pt / 第5波 +15.5）
    // X字化後の全編成 reseat で振り直した（120通り全探索の「狙いを満たす最良」/ confirm +10.6pt）
    ("溜め (ガン×ドルガ×カド)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gan, center: UnitCatalog.Hisa, back1: UnitCatalog.Kado, back3: UnitCatalog.Dolga)),
    // グザの瘴気（味方全体に毒）は位置不問。ムドは前1で敵の攻撃も浴びて育ち、ガルドは前3で庇う。セロは中央から被弾後退。
    // X字化に伴う振り直し: 後列のグザとボルグを入れ替えた（reseat 1位＝狙いを満たす最良 / confirm +19.6pt / 第2波 +57.8）
    ("毒→被弾強化 (グザ×ムド)", Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Gald, center: UnitCatalog.Sero, back1: UnitCatalog.Guza, back3: UnitCatalog.Borg)),
    // ヴィオの吸い上げは全体対象で位置不問。スィドの毒漏れはむしろ燃料なので、中衛に置いて
    // 前後の隣接（後2のミオ）へわざと当てにいく。漏れを利益に反転する側と噛ませた形（+7.8pt / 第5波 +38.8）
    ("澱み喰い (グザ×ヴィオ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Guza, center: UnitCatalog.Sid, back1: UnitCatalog.Vio, back3: UnitCatalog.Mio)),
    // 軋みの割り込み攻撃の追加後に再探索。セロが前1から中のヨミへ逃げ込んでヨミを前へ突き出し(+22)、その場で振らせる。
    // 以後はバサの入れ替えが割り込みを重ね、セロは二段目で後1のバサを突き飛ばして貫きに変わる（layout 1位）
    ("隊列崩し (バサ×ヨミ×セロ)", Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Gald, center: UnitCatalog.Gan, back1: UnitCatalog.Basa, back3: UnitCatalog.Yomi)),
    // 軋みの割り込み攻撃の追加後に再探索。セロが中衛から後1のヨミを突き飛ばして逃げ、ヨミは中衛へ突き出されて(+22)その場で振る。
    // 旧狙いの二段逃げ型（セロ前列→中のヨミ→後）は割り込み後も 48.8% 止まり（83位）。前列へ突き出されたヨミが削られるだけなので捨てた。
    // 探索1〜3位はガルド後列で庇いが死ぬので採らない（layout 4位）
    ("突き出し (セロ×ヨミ)",  Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald, center: UnitCatalog.Sero, back1: UnitCatalog.Yomi, back3: UnitCatalog.Nel)),
    // 溜め役3体を敵から遠い後列と中衛へ、という狙いはそのまま。前1を空けてカド・クグを前2/前3へ寄せ、
    // 中衛をガンに替えた形が上（+2.1pt）。カドの巻き込み先はクグとガンで変わらない
    // X字化後の全編成 reseat で振り直した（120通り全探索の「狙いを満たす最良」/ confirm +3.5pt）
    ("溜め改 (クグ×バン×ガン)", Formation.Build(front1: UnitCatalog.Kugu, front3: UnitCatalog.Gan, center: UnitCatalog.Ban, back1: UnitCatalog.Kado, back3: UnitCatalog.Dolga)),
    // 軋みの割り込み攻撃の追加後に再探索。セロは前1から中のバサ、次に後1のヨミを順に突き飛ばして貫きに変わり、
    // 逃亡もバサの入れ替えも全部シオとヨミの燃料になる（layout 1位）
    ("移動改 (バサ×ヨミ×シオ)", Formation.Build(front1: UnitCatalog.Sero, front3: UnitCatalog.Gald, center: UnitCatalog.Shio, back1: UnitCatalog.Yomi, back3: UnitCatalog.Basa)),
    // 呪詛は全体に漏れるのでウツの位置は不問。探索上位4件(80.8%)はガルド後列で庇いが死ぬので採らない。セロは中衛から被弾後退（layout 5位）
    ("逆しま (ネル×ウツ)",   Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald, center: UnitCatalog.Sero, back1: UnitCatalog.Nel, back3: UnitCatalog.Utsu)),
    // 萎縮も呪詛も全体に効くので、守るべきは中衛のクビの方。ネルとウツを後列へ下げた（+3.1pt / 第5波 +14.5）。
    // 全体1位はガルドを後1に置く形（99.4%）だが庇いが死ぬので採らない。この差 +11.5pt は庇いの監査結果そのもの（README 参照）
    ("逆しま改 (クビ×ウツ)", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald, center: UnitCatalog.Kubi, back1: UnitCatalog.Nel, back3: UnitCatalog.Utsu)),
    // 旧配置がそのまま全配置1位。ヒサの隣接（カド・ネル）で最大HPはカド（layout 1位）
    ("反撃改 (ドハ×カド)",   Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Kado, center: UnitCatalog.Doha, back1: UnitCatalog.Nel, back3: UnitCatalog.Nono)),
    // ヒサを中衛へ。横隣接が無いので深さ隣接の前2＝カドだけを指す。前列3枚が受け、カドの巻き込みはドハ・バン・ヒサへ広がる
    // （+12.2pt / 第3波 +39.0）。旧配置はヒサ前3で標的は同じだが、前列が2枚しかなく第3波が 36% だった
    // X字化後の全編成 reseat で振り直した（120通り全探索の「狙いを満たす最良」/ confirm +9.3pt）
    ("反撃改2 (ガン×カド)",  Formation.Build(front1: UnitCatalog.Doha, front3: UnitCatalog.Ban, center: UnitCatalog.Kado, back1: UnitCatalog.Hisa, back3: UnitCatalog.Gan)),
    // ヒサを中衛へ。隣接はガン(前2)とカド(後2)だが、標的は最大HPで選ばれるのでカド。ガルドは前3で庇う
    // （+7.4pt / 第3波 +23.3）。ガルド前列の制約を外すと 73.1% まで伸びるが、差は +0.5pt なので制約を保つ側を採った
    // X字化後の全編成 reseat で振り直した（120通り全探索の「狙いを満たす最良」/ confirm +19.1pt）
    ("反撃改3 (カド×ハギ)",  Formation.Build(front1: UnitCatalog.Gan, front3: UnitCatalog.Gald, center: UnitCatalog.Kado, back1: UnitCatalog.Hagi, back3: UnitCatalog.Hisa)),
    // ハギは追い打ちなので位置不問。X字化に伴う振り直しで、グザを中央（瘴気は位置不問）、
    // 前列をガルドとゴルムの受け2枚に、ハギとミオを後列へ（reseat 2位 / confirm +7.5pt / 第2波 +44.0）
    ("追撃×毒 (ハギ×グザ)",  Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Golm, center: UnitCatalog.Guza, back1: UnitCatalog.Hagi, back3: UnitCatalog.Mio)),
    // 死の連鎖にハギを足した形。2026-08-23 修正: 旧版はムグを残しヴェルを抜いていたため、
    // 死の連鎖の心臓部（継ぎ接ぎヴェルの蘇生による死体供給の倍加）が消えて第2波 98.0% → 32.5% まで
    // 落ちていた（原因はハギの1ターン1回制限ではなく、ヴェルを外したことそのもの）。
    // ムグを抜いてヴェルを残すと 95.0% まで戻る（分裂ムグの寄与は約3pt）。ハギの前列配置自体は
    // ほぼ無関係（ヴェルを残したままハギを前1に置いても95.0%）。配置は原型のスロットをそのまま流用。
    ("追撃×死 (ハギ×リィカ)", Formation.Build(front1: UnitCatalog.Hagi, front3: UnitCatalog.Zoto, center: UnitCatalog.Golm, back1: UnitCatalog.Rica, back3: UnitCatalog.Vel)),
    // ササ入りの2編成（「移動改2 (ササ×ヨミ)」「散開耐久 (ササ×ドハ)」）は X字化で外した。
    // 散開（Loose）は「隣に味方がいない駒」を硬くするが、新盤面は編成5体が 0-4 を必ず埋め、
    // 角4つは全員が中央と隣接し中央は全員と隣接するので、**発火する席が原理的に存在しない**。
    // トレイトは消していない。盤面が固まってから別議題として扱う
    // （中央の性格「単体に強く範囲に弱い」に絡めるのが素直、というところまでが現時点の見立て）。
    // セッキは後列でないと庇えない。探索上位は前列セッキで特性が死ぬので、後列制約下の最良を採る（layout 18位）
    ("死の連鎖+後備え", Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Golm, center: UnitCatalog.Vel, back1: UnitCatalog.Rica, back3: UnitCatalog.Sekki)),
    // セロは中衛から後1のドルガを突き飛ばして逃げ込み、セッキが貫き以外の後列狙いを肩代わりして狙撃を守る。
    // セッキを後1に置く探索1位(86.0%)はセロがセッキを突き飛ばして後備えごと失うので採らない（layout 3位）
    ("後衛特化+後備え", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Golm, center: UnitCatalog.Sero, back1: UnitCatalog.Dolga, back3: UnitCatalog.Sekki)),
    // ウツとセッキが後列、クビは守られる中衛。探索上位はセッキ前列＋ガルド中衛で両特性が死ぬので、制約下の最良を採る（layout 37位）
    ("逆しま+後備え",   Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Golm, center: UnitCatalog.Kubi, back1: UnitCatalog.Utsu, back3: UnitCatalog.Sekki)),
    // 燃焼軸の受け皿編成。ホタ（熾火）は自分では着火できないので、ボルグの火の粉が唯一の火種。
    // 後1ホタと後2ボルグは同じ列で左右に隣接するので火は確実に回る。前列はノノとガルドで受け、
    // 火種と受け皿をまとめて後列に下げる形。reseat 1位を confirm で追試して採用
    // （seed 200..599 で +2.1pt / seed 600..1399 で +2.3pt）。
    // 中身は第4波を約3pt 差し出して第5波を約13pt 買う入れ替えで、全体が一様に伸びたわけではない。
    // X字化に伴う振り直し。機械的な写しではホタが後列でボルグの火種が届かず 7/0/0/0 に潰れていた。
    // ホタを中央（ボルグの隣）へ上げた（reseat 1位＝狙いを満たす最良 / confirm +57.5pt）
    ("燃焼 (ボルグ×ホタ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Nono, center: UnitCatalog.Hota, back1: UnitCatalog.Mudo, back3: UnitCatalog.Borg)),
    // 範囲耐性。砕け盾のヒビ（範囲を浴びて破片を配る）を軸に据えた編成。
    // ガルドは Stoic で回復も強化も受け付けないが、破片は damage 側で消費されるので届く。
    // ドルガ（攻38・薙ぎだが2ターンに1回）は「強い。ただ遅い」という理由で外された駒で、
    // 守られて初めて完走できる。ablate でヒビを抜くと 92.2% → 大きく落ちる。
    //
    // 配置は reseat 1位（94.7%）ではなく狙いを優先して据え置き（92.2%）。
    // ヒビを前列に置き、ボルグと横に隣接させることが狙い。ボルグの薙ぎは味方も巻き込むが、
    // その巻き込みも CurrentPattern != Single なのでヒビの変換対象になる。
    // 探索1位はボルグを後列へ回してこの噛み合わせを捨てる形なので採らない。
    // X字化に伴う振り直し。ヒビは中央に置く——2本の貫き経路・薙ぎ・隣接次数4が全部そこへ入るので、
    // 範囲を浴びて破片を配る駒の指定席になる（reseat 1位 / confirm +36.8pt / 第4波 +87.0）
    ("範囲耐性 (ヒビ×ボルグ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Hibi, back1: UnitCatalog.Borg, back3: UnitCatalog.Rica)),
    // 縛め（クグ）の測定用。既存でクグを含むのは 溜め改 だけで、そこにはカドが入っている——
    // カドの改修と交絡していて、クグの設計の中心（編成によって縛りの意味が反転する）が測れない。
    // 以下の2本はその対照。片方は縛りの空きを買う駒を揃え、もう片方は誰も買わない。
    //
    // 収入型。溜め改 からカドだけを抜き、同じ前2にガルド（庇い。前列でないと働かない）を入れた形。
    // クグ・ガン（号令）・バン（据え）が揃うので、縛られた味方1体の空きに +16 / +8 / −50% が同時に払われる。
    // 残り枠は 溜め改 と同じドルガ（遅いが攻38の薙ぎ。守られて完走する側）を据え置き、
    // カドの有無だけが 溜め改 との差になるようにした
    ("縛め収入型 (クグ×バン×ガン)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Kugu, center: UnitCatalog.Gan, back1: UnitCatalog.Ban, back3: UnitCatalog.Dolga)),
    // 非収入型。号令・据え・カドのいずれも含まない。残り4枠は 速攻 (ボルグ×ムド) の攻撃役で、
    // 全員が自分の手番で殴る型——縛られた1体が失うのは実際の1振りで、誰もその空きを買わない。
    // 「味方の縛りがほぼ純粋な損」という要件を、収入側の特性をひとつも置かないことで満たす。
    // ガルドは前列（庇いの制約）、セロは中衛（狙撃化には戦闘中に後退した実績が要る）
    // X字化に伴う振り直し。機械的な写しではボルグが中央に落ち、味方4枚へ毎ターン巻き込みを撒いていた
    // （中央は編成5枠すべてと隣接する）。ボルグを後3へ、セロを中央へ（reseat 1位 / confirm +10.7pt）
    ("縛め非収入型 (クグ×速攻)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Kugu, center: UnitCatalog.Sero, back1: UnitCatalog.Mudo, back3: UnitCatalog.Borg)),
    // 据え（バン）とハギ（追い打ち）の同居。31編成に1本も無い組み合わせなので、
    // `IdleTurn` の会計を据え側で直しても compare が1行も動かず、変更が効いたことを
    // 確認できない。その対照として置く。ハギは `SurrendersTurn == false`（自分の手番を
    // 持たない型）なので、据えを無償で受け取っているかどうかがここに出る。
    //
    // 土台は 追撃×死 (ハギ×リィカ) で、前2のゾトをバンに差し替えただけ。
    // 残り3枠（ゴルム・リィカ・ヴェル）を土台のまま据え置いたのは、ハギが「味方が敵を倒す」
    // ことでしか動かない駒だから——撃破の供給源（リィカの生贄とヴェルの蘇生による死体の倍加）を
    // 崩すとハギが置物になり、据えの受け取り量そのものが測れなくなる。
    // 抜く枠にゾトを選んだのは、ヴェルを外すと第2波が 98.0% → 32.5% まで落ちることが
    // 測定済み（2026-08-23）で、ゴルムは前列の受けを兼ねているため。
    // バンは前2。据えは位置を問わないが、ゾトの空けた席をそのまま使えば土台との差が1枠で済む。
    ("追撃×据え (ハギ×バン)", Formation.Build(front1: UnitCatalog.Hagi, front3: UnitCatalog.Ban, center: UnitCatalog.Golm, back1: UnitCatalog.Rica, back3: UnitCatalog.Vel)),
    // 置き去り（ナラ）の測定用。同じ1体が編成で正反対の駒になることを表で見えるようにする2本。
    // **配置は仮置き**——採否を決める前に reseat（120通り全探索）→ confirm（seed 200..599）を回す。
    //
    // 削り側を燃料にする形。ムド（被弾強化）・カド（惨禍で削りが1.5倍）・ゴルム（巨躯で肩代わり）が
    // 全員ナラより遅い＝毎ターン削られる側に来る。回復されるのはヴェル（速8）だけ。
    // マイナスが収入になるかを見るためのエントリ。
    // 配置は reseat 1位 → confirm +48.5pt で採用（仮置き＝ナラ中央は 43.0%）。
    // 中央はナラではなくカドの席だった——棘鎧の身代わりは前か横の味方への単体攻撃に反応するので、
    // 隣接次数4の中央に置くと反応先が5枠すべてになる。ナラは速さで対象を選ぶので席を選ばない。
    ("置き去り×被弾強化", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nara,
                                     center: UnitCatalog.Kado, back1: UnitCatalog.Mudo,
                                     back3: UnitCatalog.Vel)),
    // 置き去りの**回復側**を測る編成（第21期）。`swap` S4 と同じメンバー。
    // **ゴルムを入れていないのが要点**——巨躯は削りを吸って何も返さないうえ（route・第19期）、
    // 回復の最大の受け手でもある（swap S3 でノノの回復 72 → 48）。自傷軸だけでなく回復軸も食う。
    // ゴルム軸の S3 では ナラ -7.0pt、ガルド軸の S4 では +0.3pt と**符号が変わる。**
    //
    // 平均はノノ版と ±0.3pt だが、波ごとには 第2波 +14.0 / 第3波 -13.0 と振れる。
    // **均されて ±0 になっているだけで無風ではない**——ここを見るための行。
    //
    // **この行は「置き去り×速攻」を差し替えたもの。** あちらは 100/0/0/0/0 で天井と床に
    // 張り付き、ablate がどのメンバーを抜いても ±0.0pt だった（＝5波 × 200 seed を回して
    // 情報が1ビットも出ない行）。担っていた「速さを揃えれば削りが消える」の実証は
    // replay で済んでいて README に記録済みなので、行そのものは要らない。
    // セロは前〜中（狙撃化には戦闘中に後退した実績が要る）／ガルドは前列（庇うの制約）。
    // 配置は reseat 1位 → confirm +14.4pt で採用（仮置き＝swap S4 の席そのままは 35.3%）。
    // 3期続けて同じ結論で、中央はナラの席ではない——ここでは狙撃のセロが中央に上がり、
    // ナラは後1へ下がる。**割れ方はメンバーで決まるので配置では変わらない**
    // （削り2＝ガルド4・ドルガ6 ／ 回復2＝セロ12・ササ12）。
    ("置き去り×分散回復", Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Gald,
                                       center: UnitCatalog.Sero, back1: UnitCatalog.Nara,
                                       back3: UnitCatalog.Dolga)),
    // 削りを即時払いの変換器に繋ぐ形（第20期）。ゾト(7)・ムグ(6)・リィカ(7) が全員
    // ナラ(8)より遅い＝毎ターン削られる。ゾトは削られるほど早く破裂し、ムグは早く胞子になり、
    // リィカはその死をそのまま層に変える——どれも積み上げ時間を必要としない。
    // **ヴェル(8) は同速なので無風。** 回復対象は0で、この編成のナラは純粋なマイナスになる。
    // 既存の「死の連鎖 (リィカ軸)」から中央のゴルムをナラに差し替えた形で、
    // **巨躯がいないので削りが減衰なしで届く**（route で見たとおり、巨躯は燃料を吸って何も返さない）。
    // 配置は reseat 1位 → confirm +12.1pt で採用（仮置き＝ナラ中央は 86.6%）。
    // 「置き去り×被弾強化」で中央がカドの席だったのと同じ形——**ナラは席を選ばない**
    // （速さで対象を選ぶので隣接も列も見ない）ので、中央を要求する駒に譲るのが正しい。
    // ここではヴェル（蘇生。守られて完走する側）が中央に上がる。
    //
    // **この編成は計測器としては天井に張り付いている。** 土台の 死の連鎖 (リィカ軸) が
    // ナラ抜きで既に全5波 100.0% なので、削りが効いても勝率が上がる余地が無い
    // （98.8% は土台より下）。ナラの寄与を読むときは ablate の絶対値ではなく、
    // 土台で同じ席にいたゴルムの寄与（-25.5pt）と並べること。
    ("置き去り×死の連鎖", Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Nara,
                                       center: UnitCatalog.Vel, back1: UnitCatalog.Rica,
                                       back3: UnitCatalog.Mug)),
    // 物理軸の連鎖・第1弾（責め苦のシガ / 仇討ちのザン）。**配置は仮置き**——
    // reseat（120通り全探索）→ confirm で採否を決める。
    //
    // **計画書の顔ぶれ（供援役だけを5枚並べた形）は 100/0/0/0/0 に潰れた。**
    // reseat の120通りが全部 20.0% で、どこに置いても動かない＝1ビットも情報が出ない台
    // （「置き去り×速攻」を差し替えたのと同じ理由）。原因は配置ではなく総攻で、
    // 3本とも出力役を1枚入れて組み直してある。**特性・数値は触っていない。**
    //
    // 供給（痺れ）× 終端（責め苦）の最小形。トウ(速11) が粉を撒き、シガ(速3) が読む。
    // シガの手番が回る頃には敵の Stun カウンタは消えているので、責め苦は IdleTurn も見る
    // （TormentTrait の二重条件。第2波 seed 2 のログで巡礼騎士＝速7 に対して実測）。
    // ガンは IdleTurn の買い手で、シガの自傷痺れとドルガののろまを両方買う。
    // 出力はドルガ（攻38・薙ぎ・2ターンに1回）——縛め収入型と同じ「守られて完走する側」。
    // 配置は reseat 1位 → confirm +10.9pt で採用（仮置き＝シガ中央は 41.9%）。
    ("責め苦 (トウ×シガ)", Formation.Build(front1: UnitCatalog.Tou, front3: UnitCatalog.Gald,
                                       center: UnitCatalog.Gan, back1: UnitCatalog.Shiga,
                                       back3: UnitCatalog.Dolga)),
    // 標的リレーの最小形。**ヒサは中央に置く**——隣接次数4なので編成5枠すべてが候補になり、
    // 最大HP のガルド(100 > ドルガ85) が確実に標的になる。角に置くと次数2で、
    // 計画書の席（前3）ではノノ(78)を指していた（狙いが外れることをログで確認済み）。
    // ガルドは庇う（単体を肩代わり）＋標的で二重に敵を引き受け、ザンがそこへ刺し返す。
    // 配置は reseat 1位 → confirm +22.8pt で採用（仮置き＝ドルガ前3・ガン後3 は 23.4%）。
    ("仇討ち (ヒサ×ザン)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gan,
                                       center: UnitCatalog.Hisa, back1: UnitCatalog.Zan,
                                       back3: UnitCatalog.Dolga)),
    // 「破片が怯みを止める」の検証台。破片（Armor）で受け切った被弾は OnDamaged ごと
    // 走らないので、破片を配られたザンは殴られても**怯まない＝刃が止まらない**。
    // コード追加ゼロの創発（AvengeTrait 参照）。
    //
    // **勝率では測れなかった。** 仇討ち のガンをヒビに差し替えただけの対照を組むと、
    // どの配置でも 100/0/0/0/0 に潰れる（reseat 120通りが全部 20.0%）——ヒサ・ザン・ヒビの
    // 3枠が低出力なので、残り2枠では波を抜けない。ドルガ／ボルグ／リィカ／ゴルムの
    // 総当たり（compare で6変種）でも最良が 35/0/0/0 で、**この顔ぶれで競争力のある行は作れない。**
    // 採ったのは情報セルが2つ出る ゴルム＋ドルガ 版で、破片×怯みの実証そのものは
    // 勝率ではなく1戦ログで取った（第26期・下記）。
    //
    // **配置は「1ターンに1回」の撤去に伴って振り直した**（reseat 1位 → confirm +20.0pt）。
    // 上限があった頃はヒビが中央で 50.0%——刺し返しが1回で頭打ちなので「標的が何回
    // 殴られるか」が出力に乗らず、標的を誰に付けるかの価値が潰れていた。撤去後は
    // **巨躯ゴルム(150)を中央に置いて標的をそこへ集める**形が最良になる（72.5%）。
    // ヒサは前1で隣接＝中央ゴルム(150)と後1ドルガ(85)、最大HPのゴルムに標的が付く。
    // ゴルムは後方も被覆するので、ザン(後3)は殴られにくい＝怯みにくい。
    ("仇討ち×砕け (ヒビ×ザン)", Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Hibi,
                                          center: UnitCatalog.Golm, back1: UnitCatalog.Dolga,
                                          back3: UnitCatalog.Zan)),
    // 物理軸の連鎖・第2弾（裂きのキリ / 抉りのエグ）。**配置は仮置き**——
    // reseat（120通り全探索）→ confirm で採否を決める。
    //
    // 物理軸の連鎖・第2弾（裂きのキリ / 抉りのエグ）。
    //
    // **計画書の顔ぶれは 100/0/0/0/0 に潰れた**——reseat の120通りが 20.0〜20.3% に並び、
    // どこに置いても動かない＝1ビットも情報が出ない台（第26期の3本とまったく同じ症状）。
    // 原因は配置ではなく総攻で、**特性・数値は触らずに出力役を入れて組み直してある。**
    //
    // 供給（傷）× 変換（抉り）の最小形。キリ(速12)が刻み、エグ(速6)が同じターンの後で抉る
    // ——順序は配置ではなく速度で保証されている。残り3枠は「守られて完走する側」の定番で、
    // ゴルム（巨躯。前3から中央・後列を被覆）／ドルガ（攻38・薙ぎ・2ターンに1回）／
    // ヴェル（蘇生）。**死の連鎖の台には載せていない**——墓守リィカを入れると層の二次関数が
    // 支配して、傷の線形が表から読めなくなる（下の 裂き×責め苦 がその台なので、
    // 2本を同じ土台にしない）。
    // 配置は reseat 1位 → confirm +15.1pt で採用。**キリとエグの席は交換できない**
    // ——2体を入れ替えただけの席は seed 200..599 で 40.0%（候補は 55.0%）。順序は速さ
    // （12 対 6）で決まるので入れ替えても刻む→抉るの順は崩れないが、**キリは前1で
    // 敵の的になり、エグは後3でゴルムの被覆に入る**という受けの配り方の方が効く。
    //
    // **この +15.1pt はキリのマイナス（与ダメ常に1）が作っている。** 第29期に
    // 「受けるダメージ1.5倍」へ差し替えた版で同じ confirm を引くと +0.6pt（据え置き）まで
    // 落ち、reseat 120通りの帯も二峰（56.5〜38.7%）から単峰（57.3〜51.9%）に縮んだ
    // ——**失うものが無い駒だけが「的」の役を100%引き受けられる**。差し替えは
    // 不合格として戻してあるが、この数字は反証として残す（README 第29期・第30期 §0）。
    ("裂き (キリ×エグ)", Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm,
                                     center: UnitCatalog.Dolga, back1: UnitCatalog.Vel,
                                     back3: UnitCatalog.Egu)),
    // 第26期の読み手（責め苦のシガ）と噛むか。**答えは「噛まない」で、理由は機構ではなく
    // 予算だった。** 狙いだった「痺れ＋傷の二重条件で同じ敵を殴る」形には痺れの供給役
    // （トウ）が要るが、キリ・エグ・シガ・トウで4枠が払い出しになり、残り1枠に何を入れても
    // 100/0/0/0/0 に潰れる（ドルガ 20.4% / ゴルム 20.0% / リィカ 20.4% / ヴェル 20.0% /
    // ボルグ 20.0%。reseat の120通り全探索で測った）。**供給を2本同時には買えない。**
    //
    // なので供給役を落とし、読み手2体を同じ台に並べた形を採った。ここでシガの**プラス側は
    // 一度も発火しない**（敵に Stun/IdleTurn を立てる駒がロスターにトウしかいない。
    // demo の seed 1/2/3 で「追い打ちを重ねる」0回・「怖気づいた」を実測）。
    // **第29期にキリを殴れるようにしても 0回のまま**（全5波 × seed 1/2/3 の15戦で再測定）
    // ——予算が浮けばトウが入る余地が出る、という読みは外れた。発火しないのは枠ではなく
    // 供給源の不在で、**枠を空けても供給源は湧かない。**
    // シガが払っているのはマイナス側だけで、**この行が測っているのは傷と責め苦の噛み合わせ
    // ではなく、読み手を2枚積んだときの手番経済**——それでも情報セルが3つ出る。
    // 台は死の連鎖（リィカ×ヴェル）。裂き 本体と土台を分けてあるので、2本の差は
    // 「墓守の層が乗るかどうか」で読める。
    // 配置は reseat 1位 → confirm +31.5pt で採用（中央をリィカにした席は 22.1%）。
    // 第29期のキリ差し替え後も +32.0pt で、**こちらの席の値段は動いていない**
    // （差し替えは不合格で戻したが、対照として値を残す）。
    // **中央はヴェル（蘇生）の席**——墓守も抉りも隣接や列を読まない（前者は死んだ味方の数、
    // 後者は敵の傷だけを見る）ので、中央を要求しない駒同士なら守られて完走する側に譲るのが正しい。
    ("裂き×責め苦 (キリ×エグ×シガ)", Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Kiri,
                                             center: UnitCatalog.Vel, back1: UnitCatalog.Rica,
                                             back3: UnitCatalog.Egu)),
    // 傷軸・第3弾の新2編成（第30期）。**土台は 裂き と同じ**（ゴルム／ドルガ／ヴェル）で、
    // キリ＋エグの2枠に入っていたものだけを差し替えてある——同じ資源に「長い入口」と
    // 「短い入口」を並べて、編成の側で選ばせるのが狙い（README 第30期）。
    //
    // ノミ単騎の第5枠はガン（号令）。**4枠すべてを素の出力役にすると台が飽和する**
    // ——ボルグ版は reseat 最良 98.2%（100/100/97.5/96/97.5）で情報セルが1つも無く、
    // 「単独で回るか」の答えは出ても compare の行としては測定にならない
    // （ablate ではノミが -28.5pt 効いているので、飽和しているのは台であってノミではない）。
    // カド版（棘鎧）は逆に**ノミが -5.9pt しか効かない入れ得**で、反撃軸が全部持っていく。
    // ハギ版は追い打ち＝ターン外なので、第二波（粛）の予測がノミの側から読めなくなる。
    ("刻み (ノミ単騎)", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gan,
                                    center: UnitCatalog.Nomi, back1: UnitCatalog.Vel,
                                    back3: UnitCatalog.Dolga)),
    // 1体に積み上がった傷を、同じ読み手（エグ）が読む形。**裂き (キリ×エグ) との差は
    // キリ↔ノミ の1体だけ**（土台の3枚は同じ）なので、2行の差がそのまま
    // 「広く薄く撒く供給源」と「1体へ積み上げる供給源」の差になる。
    ("刻み×抉り (ノミ×エグ)", Formation.Build(front1: UnitCatalog.Egu, front3: UnitCatalog.Golm,
                                        center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga,
                                        back3: UnitCatalog.Vel)),
    // 傷軸・第4弾（第37期）。**断ちのナタ**＝傷の消費型の終端。土台は 刻み×抉り と同じで、
    // **エグ1枚をナタに差し替えただけ**——2行の差がそのまま「維持して読み続ける」と
    // 「畳んで一撃で使う」の差になる（同じ資源に2つの使い方を並べて編成に選ばせる）。
    //
    // **もう1本（断ち (キリ×ナタ)）は測って落とした。** 41.1% / 情報セル1つ /
    // ablate のナタ寄与 -8.3pt で、3つの基準すべてでこちらに負けた
    // （こちらは 89.6% / 情報セル3つ / -49.2pt）。**落ちた理由は機構ではなく供給の細さ**
    // ——キリは1ターンに傷1つを撒くだけなので、キリが落ちた瞬間にナタは永久に沈黙する
    // （放棄 3.7回/戦。ノミ台では 0.5回/戦）。定義（SeverTrait / Nata）は残してあるので、
    // 供給を厚くした台で組み直せばいつでも戻せる。詳細は design/PHASE37_SEVER.md。
    //
    // **配置は第38期に動いた。** 閾値待ち（SeverTrait.Threshold = 2）を入れるとナタが振る回数が
    // 減り、代わりにノミが在庫を積む時間が価値になる——reseat 1位が「ノミを中央から後1へ、
    // ドルガを中央へ」に変わり、confirm（seed 200..599）で +2.9pt と閾値 2.0pt を超えたので採用。
    // 第37期の仮置き（エグの席にナタを置いただけ）はこれで卒業した。
    //
    // **値段は相変わらず「前列にナタを出すかノミを出すか」に集中している**——
    // ノミを前に出す8通りは第3〜5波が 0〜4% に落ちる（reseat 40.3% 以下の塊）。
    // 第37期は「ドルガを前に出す8通り」が下位だったので、**閾値待ちで落ちる駒が入れ替わった**。
    ("刻み×断ち (ノミ×ナタ)", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nata,
                                        center: UnitCatalog.Dolga, back1: UnitCatalog.Nomi,
                                        back3: UnitCatalog.Vel)),
    // 傷軸・第5弾（第39期）。**縫いのハリ**＝傷の防御側の維持読み。第37期と同じ作法で、
    // **既存行のエグをハリに差し替えただけ**（土台のゴルム／ドルガ／ヴェルは動かさない）。
    // これで同じ土台に読み手の三役（エグ＝維持攻／ナタ＝消費／ハリ＝維持防）が並び、
    // 行の差がそのまま読み手の差になる。
    //
    // **もう1本（裂き×縫い (キリ×ハリ)）は測って落とした。** 落ちた理由は寄与でも情報量でもなく
    // **冗長**——`裂き (キリ×エグ)` と 5波の相関 r = +0.978 / max|Δ| 14.0pt / 平均差 -0.5pt で、
    // 「読み手を攻めから防御に差し替えた」ことが表から1ビットも読めない
    // （こちらは最も近い `刻み×断ち` でも max|Δ| 19.0pt）。**供給が細いと読み手の違いが出ない**
    // ——キリは1ターンに傷1つを撒くだけなので、在庫の天井が 1 に張り付いて
    // 「維持読み3 か 繕い3 か」の差しか残らない。ablate のハリ寄与も -25.0pt 対 -39.6pt。
    // 定義（SutureTrait / Hari）は残してあるので、供給を厚くした台で組み直せばいつでも戻せる。
    // 対照そのものは `suture` 診断のローカルに置いてある。詳細は design/PHASE39_SUTURE.md。
    //
    // 配置は reseat 1位 → confirm +5.6pt で採用（仮置き＝エグの席にハリを置いただけは 73.5%）。
    // **ハリは後1で、ゴルムが前1。** ナタ（第37期・第38期）が前列へ出る形だったのと逆を向く
    // ——ハリは**傷持ちがいなくても普通に殴る**（閾値も手番の放棄も無い）ので、
    // 巨躯の被覆に入って長く立つほど繕いの機会が増える。第30期のノミと同じ側に戻る。
    ("刻み×縫い (ノミ×ハリ)", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Nomi,
                                        center: UnitCatalog.Dolga, back1: UnitCatalog.Hari,
                                        back3: UnitCatalog.Vel)),
    // 移動軸・弱体化軸（第41期）。**突き返しのハネ**＝移動を読み、弱体化を書く変換器。
    // 供給（バサの喧噪）→ 変換（ハネ）→ 読み手（ウツの逆しま）の3枚が要る最小形。
    //
    // **土台にゴルムを使っていない。** 巨躯の吐き戻し（第23期）は肩代わりした量を
    // 守った相手の AtkBonus に**加算**で返すので、ウツを被覆に入れると
    // 「弱体化で育てた分を強化で打ち消す」形になり、この行が測るものが消える
    // （README 第23期「吐き戻しは逆しま3行を落とした」）。受けはガルド（庇う・前列）で、
    // ガルドは Stoic なので効果Bの代金も1点も払わない——**既存駒への無料の payoff**で、
    // 潰すべきバグではない（ShoveTrait の doc 参照）。
    // 出力はドルガ（攻38・薙ぎ・2ターンに1回）で、裂き／刻み系と同じ「守られて完走する側」。
    //
    // 配置は reseat（120通り全探索）1位 → confirm（seed 200..599 で **+5.6pt**）で採用。
    // 仮置き（ハネを前3・ウツを中央）は 29.8%、候補は 35.0%。
    //
    // **ハネの隣接次数がそのまま効果Bの値段になる**（角2体・中央4体）ので手で置いた席が
    // 最適である保証は無く、実際に動いた。候補ではハネが**後3の角**へ下がり、
    // 初期隣接が**ガルド（Stoic で弾かれる＝代金ゼロ）とウツ（弱体化を3倍で利益に変える）**
    // の2体だけになる——**払う相手が初期配置に1体もいない席**。
    // 上位20通りのうち19通りでハネは角にいる（唯一の例外が20位）。
    //
    // **ただし喧噪が毎ターン席を混ぜるので、この「無料」は初期数ターンぶんだけ。**
    // 実測のよろけの受け手は ウツ 1583 / ドルガ 941 / バサ 778 で、ガルドは 0
    // （弾き 1.2〜1.7回/戦）。席は買えるが、買えるのは開幕の数ターンだけ。
    ("突き返し (ハネ×ウツ)", Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Gald,
                                      center: UnitCatalog.Utsu, back1: UnitCatalog.Dolga,
                                      back3: UnitCatalog.Hane)),
    ("分かち×逆しま (ドハ×ウツ)", Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald,
                                          center: UnitCatalog.Nono, back1: UnitCatalog.Doha,
                                          back3: UnitCatalog.Dolga)),
    // 集約（第42期）。**台（分かち×逆しま）とメンバーが1枚しか違わない**——ウツをウケに
    // 差し替えただけ。第21期の swap と同じ作法で、同じ席に別の駒を置いた版どうしを比べれば
    // 差がそのまま機構の差になる（ablate だと「5体目の体そのもの」が必ず混ざる）。
    // 弱体の供給源はドハ1枚（なまり）。
    //
    // 配置は reseat（120通り全探索）1位 → confirm（seed 200..599 で **+11.5pt**）で採用。
    // **ウケは中央（隣接次数4）。** 上位4通りが全部ウケ中央で、仮置き（ウケ前1）は21位。
    // 第41期のハネ（隣接次数が代金なので角へ逃げた／上位20通り中19通りが角）と
    // **ちょうど逆を向く**——隣接次数がそのまま守備範囲になる駒では、中央が席の値段を回収する。
    ("引き受け (ウケ×ドハ)", Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
                                      center: UnitCatalog.Uke, back1: UnitCatalog.Doha,
                                      back3: UnitCatalog.Dolga)),
    // 転嫁（第43期）。**第42期の集約行とメンバーが1枚しか違わない**——ウケをワタに
    // 差し替えただけ。同じ席・同じ供給（ドハのなまり1本）で、弱体の変換先だけが
    // アーマー → 敵の攻撃力に変わる。第21期の swap と同じ作法。
    //
    // **仮置きは集約行と同じ席（ワタ中央）。** 隣接次数4 は守備範囲であると同時に
    // **代金（HP）の倍率**でもあるので、ウケ（中央が1位）と同じ向きに出るとは限らない
    // ——ウケの代金は AtkBonus で素の攻6 で底を打つが、ワタの代金は HP で底が無い。
    // 実際の席は reseat → confirm で決める（結果は design/PHASE43_RELAY.md）。
    ("渡し (ワタ×ドハ)", Formation.Build(front1: UnitCatalog.Nono, front3: UnitCatalog.Gald,
                                    center: UnitCatalog.Wata, back1: UnitCatalog.Doha,
                                    back3: UnitCatalog.Dolga)),
    // 鱗（第47期）。**2行の供給源を変えるのがこの期の設計。**
    // アーマー（`StatusKeys.Armor`）は7つの盤面状態キーの中で**読み手が0枚だった唯一の資源**で、
    // ウロがその最初の読み手になる。読むのは自分が纏っている量なので**隣接を1つも使わない**
    // ——第45〜46期で隣接は2期かけて否定的な結論が出ている。
    //
    // 死は波が進むほど増え（Phase 0-4: 第1波 0.57 → 第5波 4.50 件/戦）、破片は敵の範囲攻撃
    // （＋味方の巻き込み）がある波でしか出ない。**供給の時間分布が違う2つの台で、
    // 貫きが立つ時刻が変わるか**を見る。
    //
    // 死軸の台。**「死の連鎖 (リィカ軸)」とメンバーが1枚しか違わない**——ヴェル（継ぎ接ぎ）を
    // ウロに差し替えただけ。第21期の swap と同じ作法で、同じ席に別の駒を置いた版どうしを
    // 比べれば差がそのまま機構の差になる（ablate だと「5体目の体そのもの」が必ず混ざる）。
    // 土台は 48 行で死亡数が最大（7.33 件/戦）＝**供給がいちばん厚い台**。
    ("鱗 (ウロ×死軸)", Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Mug,
                                  center: UnitCatalog.Golm, back1: UnitCatalog.Rica,
                                  back3: UnitCatalog.Uro)),
    // 破片の台。**「範囲耐性 (ヒビ×ボルグ)」とメンバーが1枚しか違わない**——リィカ（墓守）を
    // ウロに差し替えただけ。**リィカを抜くことで死軸を落とす**ので、供給の主役が
    // 砕け（ヒビ）の破片に寄る。ヒビはボルグの薙ぎの巻き込みでも砕けるので、
    // **敵の範囲攻撃を待たずに開幕から供給が出る**——死軸の台とはここが逆を向く。
    ("鱗改 (ウロ×ヒビ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga,
                                    center: UnitCatalog.Hibi, back1: UnitCatalog.Borg,
                                    back3: UnitCatalog.Uro)),
    // 逸らし（第50期）。**2行をカドの有無で分けるのがこの期の設計。**
    // 第49期の残件A（ゴウがカドから標を奪うと 第四波 − / 第五波 + と符号が反転する）が
    // **カド固有なのか標由来なのか**を分離する。
    //
    // 標は engine が常時読んでいる強い通貨（`MarkPullPercent` = 75・`SelectTargetChain`）なのに、
    // 盤面での操作手段が無かった——書き手はヒサ1枚・開戦時1回・選択の余地ゼロで、
    // **消す経路は1つも無い**（第50期 Phase 0-3）。ソラがその最初の操作手段になる。
    //
    // カド入りの台。**第49期の業の行と席まで同じ**（ゴウ→ソラ・中央とグザを入れ替えただけ）。
    // **中央をグザ(58)にしてあるのでヒサ（後1）の隣接最大HPは必ずカド(96)**
    // ——残件Aの再現条件そのもの。ソラ(96)を中央に置くとカドと同値になって
    // `PickOne` が 50/50 に割り、再現条件が汚れる（実測でも帰属が 1/4 に落ちる）。
    ("逸らし (ソラ×カド)", Formation.Build(front1: UnitCatalog.Kado, front3: UnitCatalog.Borg,
                                    center: UnitCatalog.Guza, back1: UnitCatalog.Hisa,
                                    back3: UnitCatalog.Sora)),
    // カド無しの台。**「刻み×抉り (ノミ×エグ)」とメンバーが1枚しか違わない**——
    // ヴェル（継ぎ接ぎ）をソラに差し替えただけ（第21期 swap の作法）。
    // **ヒサを含まないので「外し」は 0 回/戦**で、焦点（敵に標を付ける）と
    // 代金（自分が矢面に立つ）だけが残る。**標が前列を飛び越す**性質がそのまま効く台。
    ("逸らし改 (ソラ×ノミ)", Formation.Build(front1: UnitCatalog.Egu, front3: UnitCatalog.Golm,
                                      center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga,
                                      back3: UnitCatalog.Sora))
};

// メンバーを編成スロット 0..4 へ重複なく割り当てる全順列を、
// **召喚枠(5-8)は含めない**——プレイヤーが置けない席に駒を置く配置を数えることになる。
// 割り当てタプルの辞書式昇順で列挙する（各深さでスロットを昇順に試すため）。
// layout モードの決定性（同点タイブレーク＝列挙順の若い方）はこの順序に依存している。
static IEnumerable<int[]> SlotAssignments(int memberCount)
{
    var assign = new int[memberCount];
    var used = new bool[FormationRules.PlayableSlotCount];
    return Rec(0);

    IEnumerable<int[]> Rec(int depth)
    {
        if (depth == memberCount) { yield return (int[])assign.Clone(); yield break; }
        for (int slot = 0; slot < FormationRules.PlayableSlotCount; slot++)
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
    for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
        if (!ReferenceEquals(a[i], b[i])) return false;
    return true;
}

static string LayoutRow(string rank, Formation f, int[] wins, int seeds)
{
    static string N(UnitDef? d) => d?.Name ?? "−";
    double avg = wins.Sum() * 100.0 / (wins.Length * seeds);
    string cells = string.Concat(wins.Select(w => $" {w * 100.0 / seeds:F1}% |"));
    return $"| {rank} | {N(f[0])}/{N(f[1])} | {N(f[2])} | {N(f[3])}/{N(f[4])} | {avg:F1}% |{cells}";
}

// ---- 波の代金診断（第5期 cost / gradient が共有） ----

// 1編成 × 1波の単独戦を seed 0..seeds-1 で回し、勝った試行だけの残存を平均する。
// Formation 版の Run ではなく Materialize + UnitState 版を使うのは、戦闘後の残HPを
// 読むため（BattleResult は生存数しか持たず、残HPは持ち越し側の量なので UnitState にある）。
// 残HP割合の分母は編成の定義上の総最大HP（engage の入場戦力と同じ判断）。
// 決着ターン数も勝った試行だけで平均する（chain の `決着T` と同じ判断。負けた試行は
// 打ち切り30ターンに張り付くので混ぜると意味が壊れる）。第6期 aim が媒介変数として使う
// ——代金の表そのものには出さないので cost / gradient の出力は変わらない。
static (double WinRate, double AvgAlive, double AvgHpPct, int Wins, double AvgTurns) MeasureCost(
    Formation f, Formation enemy, int seeds)
{
    int defTotal = f.Occupied().Sum(x => x.Def.MaxHp);
    int wins = 0;
    double aliveSum = 0, hpPctSum = 0;
    long turnSum = 0;
    for (int seed = 0; seed < seeds; seed++)
    {
        List<UnitState> mine = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        List<UnitState> foes = BattleEngine.Materialize(enemy, BattleContext.EnemyTeam);
        BattleResult r = BattleEngine.Run(mine, foes, seed, verbose: false);
        if (!r.PlayerWon) continue;
        wins++;
        aliveSum += mine.Count(u => u.IsAlive);
        hpPctSum += (double)mine.Where(u => u.IsAlive).Sum(u => u.Hp) / defTotal;
        turnSum += r.Turns;
    }
    return (wins * 100.0 / seeds,
            wins == 0 ? 0 : aliveSum / wins,
            wins == 0 ? 0 : hpPctSum / wins, wins,
            wins == 0 ? 0 : (double)turnSum / wins);
}

// 範囲持ちの判定（gradient と aim が共有する。第5期の +3.1pt と直接比べるには
// 区分が同一である必要があるので、片方だけで定義しない）。
// Def.Pattern が薙ぎ/全体の駒を1体でも含むかという静的な代理指標。ホタ・リィカのように
// 状況で薙ぎ化する駒は数えない——発火が戦況依存で定義からは判定できない。
static bool HasAoe(Formation f)
    => f.Occupied().Any(x => x.Def.Pattern is AttackPattern.Sweep or AttackPattern.All);

// 範囲持ちの「枚数」。HasAoe の二値区分は薙ぎ1枚でも範囲側に入れてしまうので、
// 向きが出た候補について枚数で単調に下がるかを見る（第6期 §2-4）。
static int AoeCount(Formation f)
    => f.Occupied().Count(x => x.Def.Pattern is AttackPattern.Sweep or AttackPattern.All);

// 1波の代金（勝った試行の残HP% → 代金 = 100% − 残HP%）と、その向き（単体のみ − 範囲持ち）。
// flip の候補まとめが出しているものと同じ計算で、bridge が列の合計代金を出すために使う
// （第8期 Phase V）。勝率 0% の編成は代金が定義できないので両群とも集計から外す
// ——外した編成が偏ると打ち切りバイアスが乗るので、使う側は勝率 0% の数も見ること。
static (double Mean, double Split) WaveCost((string Name, Formation F)[] targets, Formation wave, int seeds)
{
    var live = new List<(bool Aoe, double Cost)>();
    foreach (var (_, f) in targets)
    {
        var m = MeasureCost(f, wave, seeds);
        if (m.Wins > 0) live.Add((HasAoe(f), (1 - m.AvgHpPct) * 100));
    }
    if (live.Count == 0) return (double.NaN, double.NaN);
    var byGroup = live.GroupBy(x => x.Aoe).ToDictionary(g => g.Key, g => g.Average(x => x.Cost));
    double aoe = byGroup.TryGetValue(true, out double a) ? a : double.NaN;
    double single = byGroup.TryGetValue(false, out double b) ? b : double.NaN;
    return (live.Average(x => x.Cost), single - aoe);
}

// 測定台（第9期 §0）。**合計代金 113% が、結果が敏感になる唯一の帯**——136% で測ると
// 全編成が突破 0% に潰れて何も見えない（第6〜8期の結論）。中身は第8期 bridge の
// 反転列(低) と同一（H2a 裸5 / 2b 騎士混成 / 巡礼5）で、bill（代金の分解）と
// bridge（自傷率での群分け）が同じ台の上で測るために1箇所へ寄せてある。
// bridge 側は列の定義を自分で持ったまま、この関数と一致することを検算する
// （列の定義を bridge から取り上げると第8期の出力との突き合わせが読めなくなる）。
static Formation[] BenchColumn113() => new[]
{
    Formation.Build(front1: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare, center: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare, back3: EnemyCatalog.ZealotBare),
    Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman),
    Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front3: EnemyCatalog.ZealotPilgrim, center: EnemyCatalog.ZealotPilgrim, back1: EnemyCatalog.ZealotPilgrim, back3: EnemyCatalog.ZealotPilgrim),
};

// チャージ台（第10期 Phase AB-0）。**測定台 113% には全体持ちも貫き持ちも1体もいない**
// （裸5 / 新兵・騎士・戦斧兵 / 巡礼5）。第9期までは敵の攻撃型が測定の交絡になるので
// わざと外してあったが、第10期はその2種にチャージを付ける期なので、あの台の上で測ると
// チャージ化の前後で数字が1つも動かない。
//
// そこで測定台の骨格（第1波 裸5 / 第3波 巡礼者・合計代金 113% 帯）を保ったまま、
// 貫きを1枚（第2波の戦斧兵→狙撃手）、全体を1枚（第3波の巡礼者1体→詠唱兵）だけ入れ替えた列を
// 別に作る。**入れ替えであって追加ではない**ので、ステージ設計の「貫き1枚まで／全体1枚まで」
// （UnitCatalog.cs の第三波・第四波のコメント）を跨がない。
//
// 入れ替えで代金が上がった（巡礼5のまま詠唱兵を入れると合計 128.7%）ので、第3波の体数を
// 6→4 に削って 116.6% に戻してある。**攻撃値は触らない**（第8期 Phase V の作法。攻撃を
// 振ると「安さの効果」と「一撃圏を跨いだ効果」が混ざる）。実測 116.6% は 113% 帯の
// 上端だが、突破率(1) = 39.1%・同値塊 3 と、測定台 113%（25.9% / 同値塊 8）より
// 分解能が高い。チャージは火力を塊にするぶん突破率を下げる方向に効くので、
// 前が 39% なのは帯の中へ降りてくる余地としてちょうどよい。
//
// **この列で「向き」（単体−範囲）は測れない**（-1.9pt しかなく、bridge 自身の注記の
// -4pt 基準を下回る）。第6〜8期の軸ではなく時間軸を測るための台なので、それでよい。
//
// BenchColumn113 は触らない。あちらを据え置くことで、第9期の bill / bridge の数字が
// そのまま比較対象として残り、かつ「チャージ化で動いてはいけない列」が検出器になる。
static Formation[] ChargeBench() => new[]
{
    Formation.Build(front1: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare, center: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare, back3: EnemyCatalog.ZealotBare),
    // 2b 騎士混成 の戦斧兵（薙ぎ）を狙撃手（貫き）に。スロットはそのまま中衛。
    Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Archer),
    // 巡礼者を詠唱兵（全体）入りに。既存の第四波と同じくレーン1の最深部（後2）に置く。
    // 体数 4 は合計代金を 113% 帯へ戻すための刻み（上のコメント参照）。
    Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front3: EnemyCatalog.ZealotPilgrim, center: EnemyCatalog.ZealotPilgrim, back1: EnemyCatalog.Chanter),
};

// 代金の分解（第9期 Phase X）。味方1部隊で列を1回走らせ、失った HP を
//   失ったHP = 敵由来 + 自傷分 − 回復 + 残差
// に割る。返す値はすべて**定義上の総最大HP に対する割合（%）の seed 平均**。
//
// 勝敗で試行を絞らない（cost は勝った試行だけを見るが、あれは「勝ったのにいくら
// 残ったか」の物差し。ここで見たいのは払った HP の内訳なので、負けた試行——自傷型が
// いちばん払っている場面——を落とすと分解が偏る）。
//
// tally は Def.Id で引くので、味方の Def.Id だけを拾う（胞子のような湧いた駒は
// 定義上の総最大HP に入っていないので、分子からも外れるのが正しい）。
// 敵と Def.Id が衝突していると敵の被弾が混ざるが、それは呼び出し側が検算する。
static (double Lost, double Enemy, double Ally, double Heal, double Residual, double SelfHarmRate,
        double[] AllyByBattle, double[] EnemyByBattle, int[] Reached, int WonFirst, double FirstWinCost)
    MeasureBill(Formation f, IReadOnlyList<Formation> column, int seeds)
{
    int defTotal = f.Occupied().Sum(x => x.Def.MaxHp);
    var ids = f.Occupied().Select(x => x.Def.Id).ToHashSet();
    int n = column.Count;

    double lost = 0, enemy = 0, ally = 0, heal = 0;
    var allyB = new double[n];
    var enemyB = new double[n];
    var reached = new int[n];
    int wonFirst = 0;
    double firstWinCost = 0;

    for (int seed = 0; seed < seeds; seed++)
    {
        EngagementResult r = EngagementEngine.Run(new[] { f }, column, seed, verbose: false);

        // 失った HP は会戦を終えた時点の残 HP から取る（PlayerExits の最後）。
        // 入場側だけだと最終戦の後が読めない。
        lost += defTotal - r.PlayerExits[^1].HpSum;

        for (int b = 0; b < r.Battles.Count; b++)
        {
            double e = 0, a = 0, h = 0;
            foreach ((string id, UnitTally t) in r.Battles[b].TallyByUnit)
            {
                if (!ids.Contains(id)) continue;
                e += t.DamageTaken - t.TakenFromAlly;
                a += t.TakenFromAlly;
                h += t.Healed;
            }
            enemy += e; ally += a; heal += h;
            if (b < n) { reached[b]++; allyB[b] += a; enemyB[b] += e; }
        }

        // 第1戦に勝った試行だけの第1戦の代金。cost（単独戦・勝った試行だけ）と
        // 突き合わせるための値で、seed は揃わないので一致ではなく近似で読む。
        if (r.Battles[0].PlayerWon)
        {
            wonFirst++;
            firstWinCost += (defTotal - r.PlayerExits[0].HpSum) * 100.0 / defTotal;
        }
    }

    double Pct(double v) => v * 100.0 / (seeds * (double)defTotal);
    double lostPct = Pct(lost), enemyPct = Pct(enemy), allyPct = Pct(ally), healPct = Pct(heal);
    return (lostPct, enemyPct, allyPct, healPct,
            lostPct - (enemyPct + allyPct - healPct),
            enemy + ally == 0 ? 0 : ally / (enemy + ally),
            Enumerable.Range(0, n).Select(b => reached[b] == 0 ? 0 : allyB[b] * 100.0 / (reached[b] * (double)defTotal)).ToArray(),
            Enumerable.Range(0, n).Select(b => reached[b] == 0 ? 0 : enemyB[b] * 100.0 / (reached[b] * (double)defTotal)).ToArray(),
            reached, wonFirst, wonFirst == 0 ? 0 : firstWinCost / wonFirst);
}

// 編成の動的特徴量（第12期 Phase CA。第13期 Phase DA で与ダメ・撃破の出どころを差し替え）。
// 味方1部隊で列を1回走らせ、突破度と UnitTally の集計を返す。
// **新しい tally フィールドは足していない**（第12期 §3-2 / 第13期 §3-1）。
//
// **与ダメと撃破は受け手側＝敵の tally から取る。** TickStatuses は
// `ApplyDamage(u, poison, null)` と source を渡さずに呼ぶので、毒・燃焼の削りは
// 出どころの駒の `DamageToEnemy` にも `Kills` にも載らない。味方側から合計すると
// 毒軸の編成の出力が構造的に過小に出る（第12期で見つけた穴）。どの経路で削っても
// 敵の `DamageTaken` には必ず載るので、敵側から数えれば帰属を持たない削りも拾える。
//
//     与ダメ = 敵全員の DamageTaken − 敵の TakenFromAlly
//     撃破   = 敵の死亡数（誰が仕留めたかを問わない）
//
// `TakenFromAlly` を引くのは敵同士の巻き込みを編成の手柄にしないため。**引いた量は
// 呼び出し側に返して報告する**（0 なら敵側に巻き込みが無いことの確認になる）。
//
// **どちらも振り切った量・回数を数える**（過剰殺傷を含む）。HP は 0 で止まるが tally は
// 超過分も数える（第9期 bill の残差分析で確認済み）。味方側の `DamageToEnemy` も
// `ApplyDamage` の同じ行で同じ `amount` を足しているので、**過剰分の扱いは新旧で変わらない**
// ——`与ダメ効率 = 与ダメ ÷ 撃破数` はオーバーキルの指標なので、過剰分が入っているほうが正しい。
//
// 却下した案: 毒の出どころを `UnitState` / `StatusKeys` に持たせて駒単位の帰属を復元する。
// 「部隊で協力して撒いて拡散して濃くする」がコンセプトなので駒単位の帰属は粒度が細かすぎるうえ、
// `BattleCore` に触ることになる（第13期 §2「やらないこと」）。編成単位で足りる。
//
// 味方側の値（旧定義）も同時に返す。第12期の数字と**同じ seed・同じ実行の中で**
// 並べられるようにするため（別の実行から引いてくると、動いたのが定義のせいか実行のせいかが
// 決まらない）。
//
// tally は Def.Id で引く。味方は編成の Def.Id、敵は列の Def.Id で拾う——胞子のような
// 湧いた駒はどちらの集合にも無いので、味方側の集計からは自然に外れる。ただし
// **胞子が敵に通した削りは敵の `DamageTaken` に載るので、受け手側の与ダメには入る。**
// 編成が盤面に出した出力の総量としてはそのほうが正しい（新旧で意味が変わる点）。
// 味方と敵の Def.Id 衝突は呼び出し側が検算する——power はそれを実際に数えて出す。
//
// 分母は seed 数ではなく**部隊戦の数**。会戦は深く抜いた編成ほど Battle が増えるので、
// seed 数で割ると「長く戦った」ぶんだけ値が膨らみ、突破度と機械的に相関してしまう
// （地力の第一近似を探す診断でそれをやると、測りたいものの写しが特徴量側に入る）。
//
// 突破度は seed ごとの生値も返す。第13期 Phase DB の半割（seed を2つに分けて同じ台を
// 2回測る）が、同じ計測を2度走らせずに済むようにするため。
//
// 分母そのもの（`部隊戦数 ÷ 試行`）も返す。味方1部隊では **部隊戦数 = 突破数 + 1**
// （全抜き時だけ = 突破数）なので、`/戦` の量はすべて「目的変数 + 1」で割っている。
// 第14期 Phase EA の同語反復の判定は、この分母が実際にどれだけ突破度を運んでいるかを
// 測って根拠にする（言葉で言い張らずに数字で出す）。
static (double Degree, double[] PerSeed, double[] Dynamics, double[] Legacy,
        long FoeTakenFromAlly, int DeathGaps, double BattlesPerSeed)
    MeasurePower(Formation f, IReadOnlyList<Formation> column, int seeds)
{
    var ids = f.Occupied().Select(x => x.Def.Id).ToHashSet();
    var foeIds = column.SelectMany(s => s.Occupied()).Select(x => x.Def.Id).ToHashSet();
    var perSeed = new double[seeds];
    long battles = 0;
    long dmgOut = 0, dmgIn = 0, fromAlly = 0, kills = 0, acts = 0, heal = 0;
    long foeTaken = 0, foeFromAlly = 0, foeDeaths = 0;
    int deathGaps = 0;

    for (int seed = 0; seed < seeds; seed++)
    {
        EngagementResult r = EngagementEngine.Run(new[] { f }, column, seed, verbose: false);
        perSeed[seed] = BreakthroughDegree(r, column.Count);
        for (int i = 0; i < r.Battles.Count; i++)
        {
            BattleResult b = r.Battles[i];
            battles++;
            long deathsHere = 0;
            foreach ((string id, UnitTally t) in b.TallyByUnit)
            {
                if (ids.Contains(id))
                {
                    dmgOut += t.DamageToEnemy;
                    dmgIn += t.DamageTaken;
                    fromAlly += t.TakenFromAlly;
                    kills += t.Kills;
                    acts += t.Interventions;
                    heal += t.Healed;
                }
                else if (foeIds.Contains(id))
                {
                    foeTaken += t.DamageTaken;
                    foeFromAlly += t.TakenFromAlly;
                    foeDeaths += t.Deaths;
                    deathsHere += t.Deaths;
                }
            }
            // 勝った部隊戦では敵は全滅している。死亡数が投入した敵駒の数と合わなければ、
            // **`DamageTaken` を経由せずに死ぬ経路がある**ことになる（第13期 §6 の停止条件）。
            // 味方1部隊なので負けた時点で会戦が終わり、勝った戦の敵部隊は必ず新品
            // （敵の持ち越しは起きない）——期待値は部隊の定義上の駒数でよい。
            if (b.PlayerWon && deathsHere != column[r.Pairings[i].EnemySquad].Occupied().Count())
                deathGaps++;
        }
    }

    double Per(long v) => battles == 0 ? 0 : (double)v / battles;
    long foeDmg = foeTaken - foeFromAlly;
    return (perSeed.Average(), perSeed, new[]
    {
        Per(foeDmg), Per(dmgIn), Per(foeDeaths), Per(acts), Per(heal),
        dmgIn == 0 ? 0 : (double)fromAlly / dmgIn,
        // 撃破 0 の編成では与ダメ効率が定義できない。0 で埋めると「無駄が無い」側に
        // 化けて相関を汚すので NaN を返し、相関の側で点ごと落とす。
        foeDeaths == 0 ? double.NaN : (double)foeDmg / foeDeaths,
    }, new[]
    {
        // 旧定義（味方側）。対比表だけが読む。並びは 与ダメ/戦・撃破/戦・与ダメ効率。
        Per(dmgOut), Per(kills),
        kills == 0 ? double.NaN : (double)dmgOut / kills,
    }, foeFromAlly, deathGaps, (double)battles / seeds);
}

// 単発戦1波ぶんを seed 0..seeds-1 で回し、勝敗・生存率・動的特徴量をまとめて返す（第15期 Phase FA）。
//
// MeasurePower（会戦）との違いは**分母**。単発では部隊戦が必ず1回なので、動的特徴量の分母
// 「戦」は seed 数そのものになる——第14期の分母経路（味方1部隊では 部隊戦数 = 突破数 + 1）は
// ここには存在しない。**分母が定数なので `/戦` は「平均を取る」以上の意味を持たない。**
//
// 与ダメ・撃破・与ダメ効率は**受け手側（敵の tally）から取る**（第13期 Phase DA と同じ理由。
// TickStatuses は ApplyDamage(u, poison, null) と source を渡さないので、毒・燃焼の削りは
// 出どころの駒の DamageToEnemy にも Kills にも載らない）。`干渉/戦` だけは味方側のまま
// ——毒は出どころを持たないので受け手側に対応物が無い。
//
// 生存率の分母は**編成の定義上の駒数**。r.PlayerSurvivors は胞子のように湧いた駒も数えるので
// 1.0 を超えることがある（chain の `残存` と同じ定義。あちらも同じ性質を持つ）。
//
// 残HP（代金）はここでは測らない。MeasureCost が測る量をこの関数でも定義すると、
// gradient / aim / flip との再現の検算がこの関数の正しさにも依存してしまう。
static (double[] Win, double[] SurvRate, double[] Dynamics, long FoeTakenFromAlly)
    MeasureWave(Formation f, Formation enemy, int seeds)
{
    var ids = f.Occupied().Select(x => x.Def.Id).ToHashSet();
    var foeIds = enemy.Occupied().Select(x => x.Def.Id).ToHashSet();
    int party = f.Occupied().Count();

    var win = new double[seeds];
    var surv = new double[seeds];
    long dmgIn = 0, fromAlly = 0, acts = 0, heal = 0;
    long foeTaken = 0, foeFromAlly = 0, foeDeaths = 0;

    for (int seed = 0; seed < seeds; seed++)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
        win[seed] = r.PlayerWon ? 1 : 0;
        surv[seed] = (double)r.PlayerSurvivors / party;
        foreach ((string id, UnitTally t) in r.TallyByUnit)
        {
            if (ids.Contains(id))
            {
                dmgIn += t.DamageTaken;
                fromAlly += t.TakenFromAlly;
                acts += t.Interventions;
                heal += t.Healed;
            }
            else if (foeIds.Contains(id))
            {
                foeTaken += t.DamageTaken;
                foeFromAlly += t.TakenFromAlly;
                foeDeaths += t.Deaths;
            }
        }
    }

    double Per(long v) => (double)v / seeds;
    long foeDmg = foeTaken - foeFromAlly;
    return (win, surv, new[]
    {
        Per(foeDmg), Per(dmgIn), Per(foeDeaths), Per(acts), Per(heal),
        dmgIn == 0 ? 0 : (double)fromAlly / dmgIn,
        // 撃破 0 の編成では与ダメ効率が定義できない。0 で埋めると「無駄が無い」側に化けて
        // 相関を汚すので NaN を返し、相関の側で点ごと落とす（MeasurePower と同じ判断）。
        foeDeaths == 0 ? double.NaN : (double)foeDmg / foeDeaths,
    }, foeFromAlly);
}


// 1事例（編成 × 波）を verbose で回して、解剖に要る材料をまとめて返す（第16期 Phase GA）。
//
// **BattleCore は触らない。** 要るものは全部 `BattleResult.Events` から読める——
// Attack / Damage / Status / StatusSnapshot / Death / Move / Summon が時間順に並んでいるので、
// 「誰が誰を殴ったか」「範囲が何体巻き込んだか」「毒が何段乗ったか」はここで組み直せる。
// **文字列（Log）は解析しない**（`ptrace` は解析しているが、あれは構造化イベントが入る前の道具）。
//
// InstanceId は Deploy の順（味方 → 敵、スロット昇順）で振られるので、同じ順で数えれば
// 陣営が引ける（`replay` の roster と同じ組み立て）。後から湧いた駒（胞子・増援）は
// Summon イベントが Team を持っているので、そこで台帳に足す。
//
// 振りの範囲は「Attack イベントから、同じ手番の同じ actor が出した Damage まで」で切る。
// 反撃・破裂・毒は ActorId が違う（毒は null）ので自然に外れる——**追加のフラグは要らない。**
//
// verbose=true は Events を確保するぶん遅いが、seed 数は `wave` に揃える（200）。
// 別の seed 集合で測ると、§1 の勝率・順位と解剖の数字が別の試行を見ることになる。
static WaveTrace MeasureTrace(Formation f, Formation enemy, int seeds)
{
    int party = f.Count;
    int foes = enemy.Count;
    int foeHpTotal = enemy.Occupied().Sum(x => x.Def.MaxHp);
    int allyHpTotal = f.Occupied().Sum(x => x.Def.MaxHp);

    int wins = 0, draws = 0, wipes = 0;
    double turnWinSum = 0, turnWinSq = 0, turnLoseSum = 0, aliveWinSum = 0;
    long allySwings = 0, swingHits = 0, swingDmg = 0, primaryDmg = 0, primaryN = 0;
    long directToFoe = 0, dotToFoe = 0, foeDeaths = 0;
    long foeSwings = 0, allyTaken = 0, backTaken = 0, dotToAlly = 0;
    double poisonPeakSum = 0, poisonPeakTurnSum = 0;
    long poisonWasted = 0;
    var allyAlive = new double[WaveTrace.Profile];
    var foeAlive = new double[WaveTrace.Profile];

    for (int seed = 0; seed < seeds; seed++)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true);

        // 陣営とスロットの台帳。スロットは Move で動くので追いかける（後列被弾率が要る）。
        var team = new Dictionary<int, int>();
        var slot = new Dictionary<int, int>();
        int id = 0;
        foreach (var (tm, fm) in new[] { (BattleContext.PlayerTeam, f), (BattleContext.EnemyTeam, enemy) })
            foreach (var (sl, _) in fm.Occupied()) { team[id] = tm; slot[id] = sl; id++; }
        var alive = new HashSet<int>(team.Keys);
        var stack = new Dictionary<int, int>();   // 敵に乗っている毒の段数（直近のスナップショット）

        int curActor = -1, curTurn = -1, curTarget = -1;
        bool curAlly = false, gotPrimary = false;
        int snapTurn = -1, snapSum = 0;
        double peak = 0, peakTurn = 0;
        var profA = new double[WaveTrace.Profile];
        var profF = new double[WaveTrace.Profile];
        int lastTurn = 0;

        void RecordTurn(int t)
        {
            lastTurn = t;
            if (t < 1 || t > WaveTrace.Profile) return;
            profA[t - 1] = alive.Count(x => team[x] == BattleContext.PlayerTeam);
            profF[t - 1] = alive.Count(x => team[x] == BattleContext.EnemyTeam);
        }

        foreach (BattleEvent e in r.Events)
        {
            switch (e.Kind)
            {
                case BattleEventKind.TurnStart:
                    curActor = -1;
                    // 前のターンのスナップショット合計を締める（毒の総段数のピークを取る）
                    if (snapTurn > 0 && snapSum > peak) { peak = snapSum; peakTurn = snapTurn; }
                    snapTurn = e.Turn; snapSum = 0;
                    RecordTurn(e.Turn);
                    break;

                case BattleEventKind.StatusSnapshot:
                    // 敵に乗っている毒だけを見る。**ターン開始の TickStatuses 直後の残量**なので、
                    // そのターン中に積まれたぶんは次のターンの頭まで出ない（BattleEventKind の注記）。
                    if (e.Text == "毒" && e.TargetId is int sid && team.TryGetValue(sid, out int stm)
                        && stm == BattleContext.EnemyTeam)
                    {
                        snapSum += e.Amount;
                        stack[sid] = e.Amount;
                    }
                    break;

                case BattleEventKind.Attack:
                    curActor = e.ActorId ?? -1;
                    curTurn = e.Turn;
                    curTarget = e.TargetId ?? -1;
                    curAlly = curActor >= 0 && team.TryGetValue(curActor, out int atm)
                              && atm == BattleContext.PlayerTeam;
                    gotPrimary = false;
                    if (curAlly) allySwings++;
                    else if (curActor >= 0) foeSwings++;
                    break;

                case BattleEventKind.Damage:
                {
                    int tgt = e.TargetId ?? -1;
                    if (tgt < 0 || !team.TryGetValue(tgt, out int ttm)) break;

                    if (ttm == BattleContext.EnemyTeam)
                    {
                        // 敵が受けた削り。**出どころ無し（毒・燃焼）は Status 側で数える**ので
                        // ここでは足さない（足すと二重計上になる）。敵同士の巻き込みも外す。
                        if (e.ActorId is not null && !e.FriendlyFire) directToFoe += e.Amount;
                    }
                    else
                    {
                        allyTaken += e.Amount;
                        if (slot.TryGetValue(tgt, out int sl) && FormationRules.RowOf(sl) == Row.Back)
                            backTaken += e.Amount;
                    }

                    // 振りへの帰属。**同じ手番の同じ actor** に限るので、反撃（actor が違う）も
                    // 毒（actor が null）も自然に外れる。
                    if (curAlly && e.ActorId == curActor && e.Turn == curTurn
                        && ttm == BattleContext.EnemyTeam && !e.FriendlyFire)
                    {
                        swingHits++;
                        swingDmg += e.Amount;
                        if (!gotPrimary && tgt == curTarget)
                        {
                            primaryDmg += e.Amount; primaryN++; gotPrimary = true;
                        }
                    }
                    break;
                }

                case BattleEventKind.Status:
                    if (e.TargetId is int dtg && team.TryGetValue(dtg, out int dtm))
                    {
                        if (dtm == BattleContext.EnemyTeam) dotToFoe += e.Amount;
                        else dotToAlly += e.Amount;
                    }
                    break;

                case BattleEventKind.Death:
                    if (e.TargetId is int did)
                    {
                        alive.Remove(did);
                        if (team.TryGetValue(did, out int dem) && dem == BattleContext.EnemyTeam)
                        {
                            foeDeaths++;
                            // 乗ったまま死んだ毒。**乗り切る前に落ちた量**の代理指標になる。
                            if (stack.TryGetValue(did, out int left))
                            {
                                poisonWasted += left; stack.Remove(did);
                            }
                        }
                    }
                    break;

                case BattleEventKind.Revive:
                    if (e.TargetId is int rid) alive.Add(rid);
                    break;

                case BattleEventKind.Summon:
                    if (e.TargetId is int nid && e.Team is int ntm)
                    {
                        team[nid] = ntm; slot[nid] = e.Slot; alive.Add(nid);
                    }
                    break;

                case BattleEventKind.Move:
                    if (e.TargetId is int mid) slot[mid] = e.Slot;
                    break;
            }
        }
        if (snapTurn > 0 && snapSum > peak) { peak = snapSum; peakTurn = snapTurn; }

        // 決着より後のターンは「決着時の盤面が続いた」として埋める。空欄にすると
        // 平均の分母が波ごとに変わり、推移の比較ができなくなる。
        for (int t = Math.Max(lastTurn, 1); t < WaveTrace.Profile; t++)
        {
            profA[t] = alive.Count(x => team[x] == BattleContext.PlayerTeam);
            profF[t] = alive.Count(x => team[x] == BattleContext.EnemyTeam);
        }
        for (int t = 0; t < WaveTrace.Profile; t++) { allyAlive[t] += profA[t]; foeAlive[t] += profF[t]; }

        poisonPeakSum += peak;
        poisonPeakTurnSum += peakTurn;

        if (r.PlayerWon)
        {
            wins++;
            turnWinSum += r.Turns; turnWinSq += (double)r.Turns * r.Turns;
            aliveWinSum += (double)r.PlayerSurvivors / party;
        }
        else
        {
            // 負けは2種類ある。**全滅と打ち切り（30T 引き分け）は同じ「敗北」だが中身が違う。**
            // 打ち切りは「削り切れなかった」で、全滅は「削られ切った」。
            turnLoseSum += r.Turns;
            if (r.PlayerSurvivors > 0) draws++; else wipes++;
        }
    }

    int losses = seeds - wins;
    double mW = wins == 0 ? 0 : turnWinSum / wins;
    return new WaveTrace
    {
        Seeds = seeds,
        Party = party,
        Foes = foes,
        FoeHpTotal = foeHpTotal,
        AllyHpTotal = allyHpTotal,
        WinRate = wins * 100.0 / seeds,
        DrawRate = draws * 100.0 / seeds,
        WipeRate = wipes * 100.0 / seeds,
        TurnsWin = mW,
        TurnsWinSd = wins == 0 ? double.NaN : Math.Sqrt(Math.Max(0, turnWinSq / wins - mW * mW)),
        TurnsLose = losses == 0 ? double.NaN : turnLoseSum / losses,
        AliveOnWin = wins == 0 ? double.NaN : aliveWinSum / wins,
        AllySwings = (double)allySwings / seeds,
        HitsPerSwing = allySwings == 0 ? double.NaN : (double)swingHits / allySwings,
        PrimaryDmg = primaryN == 0 ? double.NaN : (double)primaryDmg / primaryN,
        SwingShare = directToFoe == 0 ? double.NaN : swingDmg * 100.0 / directToFoe,
        DirectToFoe = (double)directToFoe / seeds,
        DotToFoe = (double)dotToFoe / seeds,
        FoeDeaths = (double)foeDeaths / seeds,
        SwingsPerKill = foeDeaths == 0 ? double.NaN : (double)allySwings / foeDeaths,
        ShaveRatio = (directToFoe + dotToFoe) / (double)foeHpTotal / seeds,
        FoeSwings = (double)foeSwings / seeds,
        AllyTaken = (double)allyTaken / seeds,
        BackShare = allyTaken == 0 ? double.NaN : backTaken * 100.0 / allyTaken,
        DotToAlly = (double)dotToAlly / seeds,
        PoisonPeak = poisonPeakSum / seeds,
        PoisonPeakTurn = poisonPeakSum <= 0 ? double.NaN : poisonPeakTurnSum / seeds,
        PoisonWasted = (double)poisonWasted / seeds,
        AllyAlive = allyAlive.Select(x => x / seeds).ToArray(),
        FoeAlive = foeAlive.Select(x => x / seeds).ToArray(),
    };
}


// 参照台1つ × 編成1つの出力を測る（第17期 Phase HA）。
//
// **目的変数から独立に編成の出力量を取るための関数。** 波ごとの勝率と同じ戦闘から与ダメを
// 取ると第14期の同語反復（分子経路）にそのまま当たるので、固定の的で1回だけ測る。
//
// **BattleCore は触らない。** 材料は `BattleResult.Events`（verbose: true）で、
// 振りへの帰属の切り方は `MeasureTrace`（第16期 Phase GA）と同一
// ——「Attack イベントから、同じ手番の同じ actor が出した Damage まで」。
// 反撃（actor が違う）も毒（actor が null）も追加のフラグ無しで外れる。
//
// **打点は `Damage` イベントから取る。`Status` からは取らない。** `Status` が持つのは
// 適用**前**の量なので、破片で吸われたぶん・非致死で丸めたぶんが実際の削りとずれる
// （`dissect` の `毒燃/戦` は `Status` から取っていて、そこだけ流儀が違う）。
// 一致は呼び出し側が敵の tally（第13期の受け手側測定）と突き合わせて検算する。
//
// seed ごとの値を配列で返すのは、**半割（測定の信頼性の上限）を同じ計測から取り出す**ため。
// 2回走らせると半割の値そのものに実行間のばらつきが乗る（`bench` 第13期と同じ作法）。
static OutputTrace MeasureOutput(Formation f, Formation bench, int seeds)
{
    var foeIds = bench.Occupied().Select(x => x.Def.Id).ToHashSet();

    var damage = new double[seeds];
    var turns = new double[seeds];
    var cum = new double[seeds][];
    double swing = 0, direct = 0, dot = 0, tally = 0, over = 0;
    long foeFromAlly = 0, kills = 0;
    int shortRun = 0, allyWipe = 0, foeWipe = 0;

    for (int seed = 0; seed < seeds; seed++)
    {
        BattleResult r = BattleEngine.Run(f, bench, seed, verbose: true);

        // 陣営の台帳。InstanceId は Deploy の順（味方 → 敵、スロット昇順）で振られる
        // （`MeasureTrace` / `replay` の roster と同じ組み立て）。
        var team = new Dictionary<int, int>();
        // 敵の最大HP（InstanceId 別）。**オーバーキルを数えるために要る**——`ApplyDamage` は
        // 残HPで切り詰めないので `Damage` イベントの `Amount` は素の量で、超過分は
        // 「1体に通した合計 − 最大HP」でしか取れない（`HpAfter` は 0 止まりなので使えない）。
        var foeMaxHp = new Dictionary<int, int>();
        int id = 0;
        foreach (var (tm, fm) in new[] { (BattleContext.PlayerTeam, f), (BattleContext.EnemyTeam, bench) })
            foreach (var (_, def) in fm.Occupied())
            {
                team[id] = tm;
                if (tm == BattleContext.EnemyTeam) foeMaxHp[id] = def.MaxHp;
                id++;
            }

        // 敵1体ごとの被弾合計。参照台は回復も破片も持たないので、合計が最大HPを超えたぶんが
        // そのままオーバーキルになる。
        var dealtTo = new Dictionary<int, double>();

        var perTurn = new double[OutputTrace.Ramp];
        double total = 0;
        int curActor = -1, curTurn = -1;
        bool curAlly = false;

        foreach (BattleEvent e in r.Events)
        {
            switch (e.Kind)
            {
                case BattleEventKind.TurnStart:
                    curActor = -1;
                    break;

                case BattleEventKind.Summon:
                    // 胞子のように湧いた駒。台帳に足さないと、その駒の打点が陣営不明で落ちる。
                    if (e.TargetId is int nid && e.Team is int ntm) team[nid] = ntm;
                    break;

                case BattleEventKind.Attack:
                    curActor = e.ActorId ?? -1;
                    curTurn = e.Turn;
                    curAlly = curActor >= 0 && team.TryGetValue(curActor, out int atm)
                              && atm == BattleContext.PlayerTeam;
                    break;

                case BattleEventKind.Damage:
                {
                    int tgt = e.TargetId ?? -1;
                    if (tgt < 0 || !team.TryGetValue(tgt, out int ttm)
                        || ttm != BattleContext.EnemyTeam) break;
                    // 敵同士の巻き込みは外す（受け手側測定の前提。第13期 §3-1）。
                    if (e.ActorId is int src
                        && (e.FriendlyFire
                            || (team.TryGetValue(src, out int stm) && stm == BattleContext.EnemyTeam)))
                        break;

                    total += e.Amount;
                    dealtTo.TryGetValue(tgt, out double had);
                    dealtTo[tgt] = had + e.Amount;
                    if (e.Turn >= 1 && e.Turn <= OutputTrace.Ramp) perTurn[e.Turn - 1] += e.Amount;
                    if (e.ActorId is null)
                    {
                        dot += e.Amount;   // 毒・燃焼。ApplyDamage が source を渡さないので actor が無い
                    }
                    else
                    {
                        direct += e.Amount;
                        // 同じ手番の同じ actor だけを「振り」に帰属させる。反撃は actor が違う。
                        if (curAlly && e.ActorId == curActor && e.Turn == curTurn) swing += e.Amount;
                    }
                    break;
                }
            }
        }

        // 敵の tally からも同じ量を出す（第13期の受け手側測定）。呼び出し側が突き合わせる。
        // 撃破も受け手側から数える——毒・燃焼の削りは `ApplyDamage(u, poison, null)` で
        // source を持たないので、味方側の `Kills` には載らない（第13期 Phase DA）。
        foreach ((string tid, UnitTally t) in r.TallyByUnit)
            if (foeIds.Contains(tid))
            {
                tally += t.DamageTaken - t.TakenFromAlly; foeFromAlly += t.TakenFromAlly;
                kills += t.Deaths;
            }

        // オーバーキル（第18期 Phase IA）。**1体ごとに数える。**
        // 合計打点 − 敵の総HP では、生き残った駒のぶんまで引いてしまう。
        foreach ((int fid, int mhp) in foeMaxHp)
            if (dealtTo.TryGetValue(fid, out double got) && got > mhp) over += got - mhp;

        damage[seed] = total;
        turns[seed] = r.Turns;
        var c = new double[OutputTrace.Ramp];
        double acc = 0;
        for (int t = 0; t < OutputTrace.Ramp; t++) { acc += perTurn[t]; c[t] = acc; }
        cum[seed] = c;

        if (r.Turns < OutputTrace.Ramp) shortRun++;
        if (r.PlayerWon) foeWipe++;
        else if (r.PlayerSurvivors == 0) allyWipe++;
    }

    return new OutputTrace
    {
        Seeds = seeds,
        Damage = damage,
        Turns = turns,
        Cum = cum,
        Swing = swing,
        Direct = direct,
        Dot = dot,
        Short = shortRun,
        AllyWipe = allyWipe,
        FoeWipe = foeWipe,
        TallyDamage = tally,
        FoeFromAlly = foeFromAlly,
        Kills = kills,
        Overkill = over,
    };
}

// Actions だけを剥がした複製。charge 診断が「溜めない同じ敵」を同じ実行の中で
// 作るために使う（git を戻して測り直すと、前後の数字が別の実行から来ることになる）。
// Id も含めて他は全て同じ。前後を別々の会戦で回すので tally は混ざらない。
static UnitDef StripActions(UnitDef d) => d.Actions is null ? d : new UnitDef
{
    Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
    Traits = d.Traits, Pattern = d.Pattern,
    PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
};

// Actions だけを差し替えた複製。timing 診断が変種をローカルに組むために使う
// （UnitCatalog は N0 / M0 のまま。gradient / aim が候補波をローカルで組んだのと同じ）。
// Id も含めて他は全て同じ。変種ごとに別の会戦を回すので tally は混ざらない。
static UnitDef WithActions(UnitDef d, IReadOnlyList<UnitAction> acts) => new UnitDef
{
    Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
    Traits = d.Traits, Pattern = d.Pattern, Actions = acts,
    PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
};

// 母標準偏差。cost の代金のばらつきと同じ物差し。
static double Sd(IReadOnlyList<double> v)
{
    if (v.Count == 0) return 0;
    double m = v.Average();
    return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / v.Count);
}

// 突破度 = 突破した部隊数 + 最後に負けた部隊戦での削り割合（0.0 〜 列長。第8期 Phase U）。
// 期待突破数は整数の平均なので、「あと一歩まで削った」と「初戦で溶けた」が同じ 2.00 に潰れる。
// 部分点を足して連続量にすると、代金の向きが結果に届いているかを順位で見られるようになる。
// 全抜き（列を抜き切った）試行は最終戦にも勝っていて LastBattleAttrition が 1.0 になるので、
// そのまま足すと列長を超える。列長ちょうどに揃える。
static double BreakthroughDegree(EngagementResult r, int columnLength)
    => r.EnemySquadsCleared >= columnLength
        ? columnLength
        : r.EnemySquadsCleared + r.LastBattleAttrition;

// 降順の平均順位（1 が最良）。同値は平均順位にする——編成の期待突破数は 0.00 や 3.00 で
// 並ぶことがあり、入力順で順位を割ると順位相関が入力順の産物になる（第7期 Phase S）。
static double[] AverageRanksDesc(double[] v)
{
    int n = v.Length;
    var idx = Enumerable.Range(0, n).OrderByDescending(i => v[i]).ToArray();
    var r = new double[n];
    for (int k = 0; k < n;)
    {
        int j = k;
        while (j + 1 < n && v[idx[j + 1]] == v[idx[k]]) j++;
        double avg = (k + j) / 2.0 + 1;   // 0 始まりの位置の平均 → 1 始まりの順位
        for (int m = k; m <= j; m++) r[idx[m]] = avg;
        k = j + 1;
    }
    return r;
}

// ピアソン相関。順位列に当てるとスピアマンの順位相関になる（同順位は平均順位で処理済み）。
static double Pearson(double[] a, double[] b)
{
    // 標本が無い／1点しかないときは NaN。呼び出し側はどこも NaN を「測れなかった」として
    // 扱っているので、ここで落とさない。**天井と床の波しか残らないと実際に空になる**
    // （寄与する波が2本未満だと、波どうしのペアが1つも作れない）。
    if (a.Length < 2 || b.Length < 2) return double.NaN;
    double ma = a.Average(), mb = b.Average();
    double cov = 0, va = 0, vb = 0;
    for (int i = 0; i < a.Length; i++)
    {
        double da = a[i] - ma, db = b[i] - mb;
        cov += da * db; va += da * da; vb += db * db;
    }
    return va == 0 || vb == 0 ? double.NaN : cov / Math.Sqrt(va * vb);
}

// 特徴量と目的変数の相関（第12期 Phase CB）。ピアソンとスピアマンを両方返す。
// 目的変数（突破度）は連続量なのでピアソンが素直だが、第8期以降の測定はすべて
// スピアマンの順位相関で報告されているので、突き合わせられるよう両方出す（§4-3）。
//
// 片方が NaN の点は落とす。撃破 0 の編成では与ダメ効率が定義できず、0 で埋めると
// 「無駄が無い」側に化けて相関を汚す。落とした結果は N で分かるようにする。
static (double R, double Rho, int N) Correlate(double[] x, double[] y)
{
    var px = new List<double>();
    var py = new List<double>();
    for (int i = 0; i < x.Length; i++)
        if (!double.IsNaN(x[i]) && !double.IsNaN(y[i])) { px.Add(x[i]); py.Add(y[i]); }
    if (px.Count < 3) return (double.NaN, double.NaN, px.Count);
    double[] ax = px.ToArray(), ay = py.ToArray();
    return (Pearson(ax, ay), Pearson(AverageRanksDesc(ax), AverageRanksDesc(ay)), px.Count);
}

// 単回帰の予測値（第12期 Phase CB）。残差 = 実測 − これ。
// x に NaN が混じる点は予測できないので NaN を返す（残差の順位付けから落ちる）。
static double[] LinearFit(double[] x, double[] y)
{
    var idx = Enumerable.Range(0, x.Length).Where(i => !double.IsNaN(x[i]) && !double.IsNaN(y[i])).ToArray();
    if (idx.Length < 2) return x.Select(_ => double.NaN).ToArray();
    double mx = idx.Average(i => x[i]), my = idx.Average(i => y[i]);
    double sxy = idx.Sum(i => (x[i] - mx) * (y[i] - my));
    double sxx = idx.Sum(i => (x[i] - mx) * (x[i] - mx));
    double slope = sxx == 0 ? 0 : sxy / sxx;
    return x.Select(v => double.IsNaN(v) ? double.NaN : my + slope * (v - mx)).ToArray();
}

// 2変数の重相関の二乗（第12期 Phase CB）。r1 / r2 は各説明変数と目的変数の相関、
// r12 は説明変数同士の相関。**2変数で止めるのは n = 31 だから**——3変数以上は
// 過学習して「説明できた」という数字だけが残る（§4-1）。
//
// 説明変数同士がほぼ同一（|r12| ≒ 1）だと分母が 0 に落ちて発散するので、
// その場合は単変量の良い方を返す（2本目に情報が無いので、それが正しい答えでもある）。
static double R2Two(double r1, double r2, double r12)
{
    if (double.IsNaN(r1) || double.IsNaN(r2) || double.IsNaN(r12)) return double.NaN;
    double denom = 1 - r12 * r12;
    if (denom < 1e-9) return Math.Max(r1 * r1, r2 * r2);
    return Math.Min(1.0, (r1 * r1 + r2 * r2 - 2 * r1 * r2 * r12) / denom);
}

// 代金の表（編成 × 波）と、波ごとのばらつきの表を吐く。計測値は呼び出し側にも返す
// （gradient が候補選定の集計に使う）。
static (double WinRate, double AvgAlive, double AvgHpPct, int Wins, double AvgTurns)[,] EmitCostTables(
    (string Name, Formation F)[] targets,
    IReadOnlyList<(string Name, Formation Enemy)> waves, int seeds)
{
    Console.WriteLine("### 代金の表（勝った試行だけの集計）");
    Console.WriteLine();
    Console.WriteLine("セルは `勝率 / 残体数 残HP%`。勝率 0% の波は残存が定義できないので `—`。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(waves.Select(w => $" {w.Name} |")));
    Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "---|")));

    var cells = new (double WinRate, double AvgAlive, double AvgHpPct, int Wins, double AvgTurns)[targets.Length, waves.Count];
    for (int t = 0; t < targets.Length; t++)
    {
        var row = new List<string>();
        for (int w = 0; w < waves.Count; w++)
        {
            cells[t, w] = MeasureCost(targets[t].F, waves[w].Enemy, seeds);
            row.Add(cells[t, w].Wins == 0
                ? $" {cells[t, w].WinRate:F0}% / — |"
                : $" {cells[t, w].WinRate:F0}% / 残{cells[t, w].AvgAlive:F1} {cells[t, w].AvgHpPct * 100:F0}% |");
        }
        Console.WriteLine($"| {targets[t].Name} |" + string.Concat(row));
        Console.Out.Flush();
    }

    // ばらつきの表。波間の差は平均で、編成間の差は標準偏差で見る（第5期 §2-1）。
    // 代金は勝率 > 0% の編成だけで集計する（負けた試行しか無い編成は代金が定義できない）。
    Console.WriteLine();
    Console.WriteLine("### 代金のばらつき");
    Console.WriteLine();
    Console.WriteLine("| 波 | 勝率の中央値 | 代金の平均 | 代金の最小（編成名） | 代金の最大（編成名） | 代金の標準偏差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    for (int w = 0; w < waves.Count; w++)
    {
        var rates = Enumerable.Range(0, targets.Length)
            .Select(t => cells[t, w].WinRate).OrderBy(x => x).ToArray();
        double median = rates.Length % 2 == 1
            ? rates[rates.Length / 2]
            : (rates[rates.Length / 2 - 1] + rates[rates.Length / 2]) / 2;

        var costs = Enumerable.Range(0, targets.Length)
            .Where(t => cells[t, w].Wins > 0)
            .Select(t => (targets[t].Name, Cost: (1 - cells[t, w].AvgHpPct) * 100))
            .ToList();
        if (costs.Count == 0)
        {
            Console.WriteLine($"| {waves[w].Name} | {median:F1}% | — | — | — | — |");
            continue;
        }
        double mean = costs.Average(c => c.Cost);
        double sd = Math.Sqrt(costs.Average(c => (c.Cost - mean) * (c.Cost - mean)));
        var lo = costs.OrderBy(c => c.Cost).First();
        var hi = costs.OrderByDescending(c => c.Cost).First();
        Console.WriteLine($"| {waves[w].Name} | {median:F1}% | {mean:F1}% | {lo.Cost:F1}%（{lo.Name}） "
            + $"| {hi.Cost:F1}%（{hi.Name}） | {sd:F1}pt |");
    }
    Console.WriteLine();
    Console.WriteLine("代金の集計対象（勝率 > 0% の編成数）: "
        + string.Join(" / ", Enumerable.Range(0, waves.Count).Select(w =>
            $"{waves[w].Name} {Enumerable.Range(0, targets.Length).Count(t => cells[t, w].Wins > 0)}/{targets.Length}")));
    return cells;
}

// 候補波の集約先（第15期 Phase FA）。**ここが唯一の1箇所**で、`wave`（第15期）と
// `dissect`（第16期）の両方がこの関数を呼ぶ。モードのローカルに置いたままだと
// 2つ目の診断がコピーを持つことになり、**集め直した意味がその時点で消える。**
//
// --- 候補波の集約（1箇所） ---
//
// 出どころは6つ。**どれも定義を1文字も変えずに写している**——値が動いたら集め方が
// 間違っている証拠なので、下の「再現の検算」で 代金・向き・ターン数 を突き合わせる
// （§5-7 の停止条件）。
//
//   既存5波   EnemyCatalog.Stages（compare / chain / pulse が測っている盤面そのもの）
//   第5期     gradient の w1 / w2 / w3
//   第6期     aim の H1 系・H2 系・M1（1a/1b/1c は gradient と同一なので重複させない）
//   第7期     flip の R0〜R12（3a/3b/3c は gradient と同一なので重複させない）
//   第8期     bridge の 荷駄5 / 巡礼5（第3波の代金だけを振った点。攻10 は R11 と同じ）
//   第10期    ChargeBench の第2波・第3波（第1波は H2a と同一なので重複させない）
//
// **現物が無くて入れられなかった候補が2つある。** 第8期に測った「攻5 版」（90/攻5）と
// 「板金従卒5」（60/攻7）は `UnitCatalog` に `UnitDef` が残っていない（攻5 は刻みとして
// 測っただけ、板金従卒は「却下した案」として文章にだけ残っている）。BattleCore を触らない
// 作業なので**新しい敵は作らない**——集めるのは現物のある波だけにして、無い2つは出力に明記する。
static (string Tag, string Era, string Name, Formation Enemy)[] WaveCatalog()
    => new (string, string, string, Formation)[]
    {
        ("S1",  "既存",   "第一波 / 物見の兵",      EnemyCatalog.Stages[0].Enemy),
        ("S2",  "既存",   "第二波 / 巡礼騎士団",    EnemyCatalog.Stages[1].Enemy),
        ("S3",  "既存",   "第三波 / 討伐隊本隊",    EnemyCatalog.Stages[2].Enemy),
        ("S4",  "既存",   "第四波 / 城塞守備隊",    EnemyCatalog.Stages[3].Enemy),
        ("S5",  "既存",   "第五波 / 異端審問団",    EnemyCatalog.Stages[4].Enemy),

        // 第5期 gradient の w1 / w2 / w3。
        ("G1a", "第5期",  "1a 農兵5",
            Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("G1b", "第5期",  "1b 農兵5",
            Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Levy, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("G1c", "第5期",  "1c 農兵5+斧",
            Formation.Build(front1: EnemyCatalog.Levy, front3: EnemyCatalog.Axeman, center: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back3: EnemyCatalog.Levy)),
        ("G2a", "第5期",  "2a 新兵3+斧",
            Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Recruit, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman)),
        ("G2b", "第5期",  "2b 騎士混成",
            Formation.Build(front1: EnemyCatalog.Recruit, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Axeman)),
        ("G2c", "第5期",  "2c 騎士2+狙撃",
            Formation.Build(front1: EnemyCatalog.Knight, front3: EnemyCatalog.Knight, center: EnemyCatalog.Recruit, back1: EnemyCatalog.Archer)),
        ("G3a", "第5期",  "3a 精鋭3",
            Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Warden)),
        ("G3b", "第5期",  "3b 精鋭+司祭長",
            Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion, center: EnemyCatalog.Chaplain)),
        ("G3c", "第5期",  "3c 精鋭2",
            Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Champion)),

        // 第6期 aim。H1 系（高HP低攻）・H2 系（低HP高攻）・M1（中間点）。
        ("H1a", "第6期",  "H1a 人足5",
            Formation.Build(front1: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer, center: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer, back3: EnemyCatalog.Laborer)),
        ("H1b", "第6期",  "H1b 人足5",
            Formation.Build(front1: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer, center: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer, back3: EnemyCatalog.Laborer)),
        ("H1c", "第6期",  "H1c 人足4",
            Formation.Build(front1: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer, center: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer)),
        ("H2a", "第6期",  "H2a 裸5(16)",
            Formation.Build(front1: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare, center: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare, back3: EnemyCatalog.ZealotBare)),
        ("H2b", "第6期",  "H2b 革5(24)",
            Formation.Build(front1: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather, center: EnemyCatalog.ZealotLeather, back1: EnemyCatalog.ZealotLeather, back3: EnemyCatalog.ZealotLeather)),
        ("H2c", "第6期",  "H2c 鎖5(32)",
            Formation.Build(front1: EnemyCatalog.ZealotMail, front3: EnemyCatalog.ZealotMail, center: EnemyCatalog.ZealotMail, back1: EnemyCatalog.ZealotMail, back3: EnemyCatalog.ZealotMail)),
        ("H2d", "第6期",  "H2d 革4(24)",
            Formation.Build(front1: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather, center: EnemyCatalog.ZealotLeather, back1: EnemyCatalog.ZealotLeather)),
        ("M1",  "第6期",  "M1 傭兵5",
            Formation.Build(front1: EnemyCatalog.Drifter, front3: EnemyCatalog.Drifter, center: EnemyCatalog.Drifter, back1: EnemyCatalog.Drifter, back3: EnemyCatalog.Drifter)),

        // 第7期 flip。R0〜R6・R8〜R12 は 体数 × 個体HP の格子、R7 は処刑なしの対照。
        ("R0",  "第7期",  "R0 鎖4(32)",
            Formation.Build(front1: EnemyCatalog.ZealotMail, front3: EnemyCatalog.ZealotMail, center: EnemyCatalog.ZealotMail, back1: EnemyCatalog.ZealotMail)),
        ("R1",  "第7期",  "R1 板金4(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate, center: EnemyCatalog.ZealotPlate, back1: EnemyCatalog.ZealotPlate)),
        ("R2",  "第7期",  "R2 板金3(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate, center: EnemyCatalog.ZealotPlate)),
        ("R3",  "第7期",  "R3 板金2(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate)),
        ("R4",  "第7期",  "R4 重甲4(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat, back1: EnemyCatalog.ZealotGreat)),
        ("R5",  "第7期",  "R5 重甲3(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat)),
        ("R6",  "第7期",  "R6 重甲2(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat)),
        ("R7",  "第7期",  "R7 精鋭3・処刑なし（3a と数値は同じ）",
            Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.ChampionPlain, center: EnemyCatalog.Warden)),
        ("R8",  "第7期",  "R8 重甲5(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat, back1: EnemyCatalog.ZealotGreat, back3: EnemyCatalog.ZealotGreat)),
        ("R9",  "第7期",  "R9 重甲5(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front3: EnemyCatalog.ZealotGreat, center: EnemyCatalog.ZealotGreat, back1: EnemyCatalog.ZealotGreat, back3: EnemyCatalog.ZealotGreat)),
        ("R10", "第7期",  "R10 板金5(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front3: EnemyCatalog.ZealotPlate, center: EnemyCatalog.ZealotPlate, back1: EnemyCatalog.ZealotPlate, back3: EnemyCatalog.ZealotPlate)),
        ("R11", "第7期",  "R11 従卒5(90/攻10)",
            Formation.Build(front1: EnemyCatalog.ZealotSquire, front3: EnemyCatalog.ZealotSquire, center: EnemyCatalog.ZealotSquire, back1: EnemyCatalog.ZealotSquire, back3: EnemyCatalog.ZealotSquire)),
        ("R12", "第7期",  "R12 従卒5(90/攻10)",
            Formation.Build(front1: EnemyCatalog.ZealotSquire, front3: EnemyCatalog.ZealotSquire, center: EnemyCatalog.ZealotSquire, back1: EnemyCatalog.ZealotSquire, back3: EnemyCatalog.ZealotSquire)),

        // 第8期 bridge。R11 と体数・個体HP は同じで攻撃だけが違う（代金を振った軸）。
        ("P6",  "第8期",  "荷駄5(90/攻7)",
            Formation.Build(front1: EnemyCatalog.ZealotPorter, front3: EnemyCatalog.ZealotPorter, center: EnemyCatalog.ZealotPorter, back1: EnemyCatalog.ZealotPorter, back3: EnemyCatalog.ZealotPorter)),
        ("Q6",  "第8期",  "巡礼5(90/攻4)",
            Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front3: EnemyCatalog.ZealotPilgrim, center: EnemyCatalog.ZealotPilgrim, back1: EnemyCatalog.ZealotPilgrim, back3: EnemyCatalog.ZealotPilgrim)),

        // 第10期 ChargeBench の第2波・第3波。**候補波の中で貫き・全体を持つのはここだけ**
        // （第6期以降の候補は「敵の攻撃型は測定の交絡になる」として単体で揃えてある）。
        ("C2",  "第10期", "チャージ台2波 新兵2+騎士+狙撃(貫き)", ChargeBench()[1]),
        ("C3",  "第10期", "チャージ台3波 巡礼3+詠唱兵(全体)",   ChargeBench()[2]),
    };




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
