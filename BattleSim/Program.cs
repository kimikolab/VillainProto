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

// chain モード: 勝率だけでは見えない「連鎖の深さ」を測る。
// 「2枚で人並みに勝つ」編成と「5枚が畳みかけて無双する」編成は、勝率だけ見ると同じ100%になる。
// MaxEnemyKillsInOneTurn（1ターンで味方が何体倒したかの最大値）と、勝利時の決着ターン数を
// compare と同じ代表編成×全ステージで測って区別する。数値が大きいほど「畳みかけている」。
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
    Console.WriteLine("| 編成 | 勝率 | 連鎖深度(平均) | 連鎖深度(最大) | 決着T(勝利時平均) |");
    Console.WriteLine("|---|--:|--:|--:|--:|");

    foreach (var (name, f) in builds)
    {
        int wins = 0, trials = 0;
        double killSum = 0;
        int killMax = 0;
        double turnSumOnWin = 0;

        foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
        {
            for (int seed = 0; seed < ChainSeeds; seed++)
            {
                var r = BattleEngine.Run(f, st.Enemy, seed, verbose: false);
                trials++;
                killSum += r.MaxEnemyKillsInOneTurn;
                if (r.MaxEnemyKillsInOneTurn > killMax) killMax = r.MaxEnemyKillsInOneTurn;
                if (r.PlayerWon) { wins++; turnSumOnWin += r.Turns; }
            }
        }

        double winRate = wins * 100.0 / trials;
        double killAvg = killSum / trials;
        double turnAvgOnWin = wins > 0 ? turnSumOnWin / wins : 0;
        Console.WriteLine($"| {name} | {winRate:F1}% | {killAvg:F2} | {killMax} | {turnAvgOnWin:F1} |");
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
    const double Threshold = 2.0;        // これ未満は誤差とみなして据え置く

    // (編成名, 旧配置, 候補配置)。候補は reseat の「狙いを満たす最良」。
    var picks = new (string Name, Formation Old, Formation New)[]
    {
        ("毒 (グザ×ミオ×ラウ)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sid, mid: UnitCatalog.Guza, back1: UnitCatalog.Mio, back2: UnitCatalog.Rau),
            Formation.Build(front2: UnitCatalog.Guza, front3: UnitCatalog.Gald, mid: UnitCatalog.Mio, back1: UnitCatalog.Sid, back2: UnitCatalog.Rau)),
        ("反撃 (ヒサ×カド)",
            Formation.Build(front2: UnitCatalog.Nono, front3: UnitCatalog.Gald, mid: UnitCatalog.Nel, back1: UnitCatalog.Hisa, back2: UnitCatalog.Kado),
            Formation.Build(front1: UnitCatalog.Hisa, front2: UnitCatalog.Kado, front3: UnitCatalog.Gald, mid: UnitCatalog.Nono, back2: UnitCatalog.Nel)),
        ("惨禍×被弾強化",
            Formation.Build(front1: UnitCatalog.Mudo, front2: UnitCatalog.Kado, front3: UnitCatalog.Hisa, mid: UnitCatalog.Sero, back2: UnitCatalog.Nono),
            Formation.Build(front1: UnitCatalog.Hisa, front2: UnitCatalog.Kado, front3: UnitCatalog.Mudo, mid: UnitCatalog.Sero, back1: UnitCatalog.Nono)),
        ("惨禍×死の連鎖",
            Formation.Build(front2: UnitCatalog.Kado, front3: UnitCatalog.Golm, mid: UnitCatalog.Rica, back1: UnitCatalog.Vel, back2: UnitCatalog.Zoto),
            Formation.Build(front2: UnitCatalog.Golm, front3: UnitCatalog.Kado, mid: UnitCatalog.Vel, back1: UnitCatalog.Rica, back2: UnitCatalog.Zoto)),
        ("溜め (ガン×ドルガ×カド)",
            Formation.Build(front1: UnitCatalog.Kado, front2: UnitCatalog.Gald, front3: UnitCatalog.Dolga, mid: UnitCatalog.Gan, back1: UnitCatalog.Hisa),
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, mid: UnitCatalog.Hisa, back1: UnitCatalog.Gan, back2: UnitCatalog.Kado)),
        ("溜め改 (クグ×バン×ガン)",
            Formation.Build(front1: UnitCatalog.Kado, front2: UnitCatalog.Kugu, mid: UnitCatalog.Ban, back1: UnitCatalog.Dolga, back2: UnitCatalog.Gan),
            Formation.Build(front2: UnitCatalog.Kado, front3: UnitCatalog.Kugu, mid: UnitCatalog.Gan, back1: UnitCatalog.Ban, back2: UnitCatalog.Dolga)),
        ("逆しま改 (クビ×ウツ)",
            Formation.Build(front1: UnitCatalog.Nel, front2: UnitCatalog.Golm, front3: UnitCatalog.Gald, mid: UnitCatalog.Utsu, back1: UnitCatalog.Kubi),
            Formation.Build(front2: UnitCatalog.Golm, front3: UnitCatalog.Gald, mid: UnitCatalog.Kubi, back1: UnitCatalog.Nel, back2: UnitCatalog.Utsu)),
        ("反撃改2 (ガン×カド)",
            Formation.Build(front1: UnitCatalog.Ban, front2: UnitCatalog.Kado, front3: UnitCatalog.Hisa, mid: UnitCatalog.Gan, back1: UnitCatalog.Doha),
            Formation.Build(front1: UnitCatalog.Doha, front2: UnitCatalog.Kado, front3: UnitCatalog.Ban, mid: UnitCatalog.Hisa, back1: UnitCatalog.Gan)),
        ("反撃改3 (カド×ハギ)",
            Formation.Build(front1: UnitCatalog.Hisa, front3: UnitCatalog.Gald, mid: UnitCatalog.Gan, back1: UnitCatalog.Kado, back2: UnitCatalog.Hagi),
            Formation.Build(front2: UnitCatalog.Gan, front3: UnitCatalog.Gald, mid: UnitCatalog.Hisa, back1: UnitCatalog.Hagi, back2: UnitCatalog.Kado)),
        ("散開耐久 (ササ×ドハ)",
            Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Gald, mid: UnitCatalog.Doha, back2: UnitCatalog.Dolga),
            Formation.Build(front1: UnitCatalog.Doha, front3: UnitCatalog.Gald, mid: UnitCatalog.Dolga, back1: UnitCatalog.Sasa)),
        ("逆しま+後備え",
            Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Golm, mid: UnitCatalog.Kubi, back1: UnitCatalog.Sekki, back2: UnitCatalog.Utsu),
            Formation.Build(front2: UnitCatalog.Golm, front3: UnitCatalog.Gald, mid: UnitCatalog.Kubi, back1: UnitCatalog.Sekki, back2: UnitCatalog.Utsu)),
        // ここからスィドの改修（被弾で毒を積む形）に伴う再探索ぶん。
        ("毒 (グザ×ミオ×ラウ)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sid, mid: UnitCatalog.Guza, back1: UnitCatalog.Mio, back2: UnitCatalog.Rau),
            Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Sid, mid: UnitCatalog.Guza, back1: UnitCatalog.Mio, back2: UnitCatalog.Rau)),
        ("澱み喰い (グザ×ヴィオ)",
            Formation.Build(front1: UnitCatalog.Sid, front3: UnitCatalog.Gald, mid: UnitCatalog.Guza, back1: UnitCatalog.Mio, back2: UnitCatalog.Vio),
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Guza, mid: UnitCatalog.Sid, back1: UnitCatalog.Vio, back2: UnitCatalog.Mio)),
        // ここからベニのマイナス（味方の毒が2倍に効く）追加に伴う再探索ぶん。
        ("毒+耐久 (ベニ×トウ)",
            Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Guza, mid: UnitCatalog.Tou, back1: UnitCatalog.Mio, back2: UnitCatalog.Beni),
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Guza, mid: UnitCatalog.Mio, back1: UnitCatalog.Beni, back2: UnitCatalog.Tou)),
        // ここから燃焼軸（熾のホタ）追加に伴う探索ぶん。
        ("燃焼 (ボルグ×ホタ)",
            Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Hota, front3: UnitCatalog.Mudo, mid: UnitCatalog.Borg, back1: UnitCatalog.Nono),
            Formation.Build(front1: UnitCatalog.Nono, front2: UnitCatalog.Gald, front3: UnitCatalog.Mudo, back1: UnitCatalog.Hota, back2: UnitCatalog.Borg)),
        // ここからリィカの覚醒（3層以上で攻撃が薙ぎに変わる）追加に伴う再探索ぶん。
        ("死の連鎖 (リィカ軸)",
            Formation.Build(front1: UnitCatalog.Mug, front2: UnitCatalog.Zoto, mid: UnitCatalog.Golm, back1: UnitCatalog.Rica, back2: UnitCatalog.Vel),
            Formation.Build(front2: UnitCatalog.Zoto, front3: UnitCatalog.Mug, mid: UnitCatalog.Golm, back1: UnitCatalog.Rica, back2: UnitCatalog.Vel))
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
        Console.WriteLine("| 粗順 | 狙 | 前1/前2/前3 | 中 | 後1/後2 | 平均 |"
            + string.Concat(stages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|--:|:-:|---|---|---|--:|" + string.Concat(stages.Select(_ => "---:|")));
        foreach (var v in verified)
        {
            Formation f = perms[v.Idx];
            static string N(UnitDef? d) => d?.Name ?? "−";
            bool isCur = SameFormation(f, build.F);
            string rank = $"{order.IndexOf(v.Idx) + 1}" + (isCur ? "★現行" : "");
            Console.WriteLine($"| {rank} | {(MeetsIntent(f) ? "○" : "×")} | {N(f[0])}/{N(f[1])}/{N(f[2])} | {N(f[3])} "
                + $"| {N(f[4])}/{N(f[5])} | {v.Avg:F1}% |" + string.Concat(v.Cells.Select(c => $" {c:F1}% |")));
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

        // 波別最良。上の表は全ステージ平均を最大化する「一つの配置」を選ぶが、
        // 実プレイは波ごとに組み替えられる。この差を出さないと、平均最良の配置が
        // たまたま苦手な波で出した勝率を「その編成の限界」と読み違える（§2-10）。
        Console.WriteLine();
        Console.WriteLine("| 波 | 前1/前2/前3 | 中 | 後1/後2 | 現行 | 波別最良 |");
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
            Console.WriteLine($"| 第{sx + 1}波 | {NameOf(bf[0])}/{NameOf(bf[1])}/{NameOf(bf[2])} | {NameOf(bf[3])} "
                + $"| {NameOf(bf[4])}/{NameOf(bf[5])} | {curRate:F1}% | {bestRate:F1}% |");
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
// ptrace モード: 毒軸の立ち上がりを見る。層は減衰しないので累積ダメージは時間の二乗で効く。
// 「間に合っていないのか、そもそも足りないのか」を切り分けるための道具。
// 各ターンの敵の総層数・敵の残数・味方の残数を並べ、決着ターンと突き合わせる。
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
// 発動しない）/ セロは前〜中（狙撃化には戦闘中に後退した実績が要る。最初から後列では発動しない）/
// ヒサの隣接で最大HPがカドになること（標的が逸れる）。
static (string Name, Formation F)[] CompareBuilds() => new (string, Formation)[]
{
    // セロは前2から中→後ろへ二段逃げて実績つきの貫きに変わる。ボルグは後1で前1ムドと縦隣接を保ち、巻き込みで育てる。
    // 探索1位はガルド中衛で庇いが死ぬので採らない（layout 2位）
    ("速攻 (ボルグ×ムド)",   Formation.Build(front1: UnitCatalog.Mudo, front2: UnitCatalog.Sero, front3: UnitCatalog.Gald, mid: UnitCatalog.Nel, back1: UnitCatalog.Borg)),
    // 脆いムグ・ゾトを前で死なせて連鎖を起こす。中衛ゴルムの吸いが隣のゾトを破裂まで運ぶ（layout 1位）
    // リィカの覚醒（薙ぎ化）追加に伴い reseat で再探索。ムグを前1→前3、ゾトを前2のまま前1を空ける形が上
    // （confirm 追試 +2.2pt、第5波 +10.8。第5波は元々連鎖の畳みかけが弱かった波）。
    ("死の連鎖 (リィカ軸)",  Formation.Build(front2: UnitCatalog.Zoto, front3: UnitCatalog.Mug, mid: UnitCatalog.Golm, back1: UnitCatalog.Rica, back2: UnitCatalog.Vel)),
    // スィドは前3。被弾で毒を積む形になったので前列に出す必要があり、孤立席なので味方漏れも消える。
    // 前1を空けてガルドを前2に寄せた形が上（+2.1pt / 第3波 +21.8）。
    // 注: この配置はスィドの味方漏れを完全に無効化している。ガルド・セッキと同じ「配置でマイナスが消える」形（README）
    ("毒 (グザ×ミオ×ラウ)", Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Sid, mid: UnitCatalog.Guza, back1: UnitCatalog.Mio, back2: UnitCatalog.Rau)),
    // 支援2枚を後列に下げ、痺れ粉は守られる中衛から撒く（layout 1位）
    // ベニのマイナス（味方の毒が2倍に効く）が入った分、毒を浴びる位置関係が変わった。
    // ミオを中衛へ上げ、ベニとトウを後列に下げた形が上（+4.0pt / 第5波 +15.8）
    ("毒+耐久 (ベニ×トウ)",  Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Guza, mid: UnitCatalog.Mio, back1: UnitCatalog.Beni, back2: UnitCatalog.Tou)),
    // 毒+耐久 の92%はベニ単独でもトウ単独でも出ない（ベニのみ 100/25/5/89/2、トウのみ 100/0/0/0/0）。
    // 効いているのは「毒の供給＋耐える手段」という型で、耐える側はトウでなくてもよい。
    // その裏を取るためのエントリ。トウをラウに差し替えた形。
    ("毒+ベニ+ラウ",       Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Sid, mid: UnitCatalog.Guza, back1: UnitCatalog.Rau, back2: UnitCatalog.Beni)),
    // ヴィオはターン開始時に味方の毒を全部吸い上げて攻撃力に変える。スィドの漏れとラウの拡散が
    // そのまま燃料になる形。ヴィオを2編成目に載せて、吸い上げが何を消しているかを見えるようにする。
    ("毒爆弾 (ラウ×ヴィオ)", Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Sid, mid: UnitCatalog.Guza, back1: UnitCatalog.Vio, back2: UnitCatalog.Rau)),
    // ヒサの隣接はカドだけ（後1↔後2）。ガルド(HP100>カド96)を隣に置くと標的が逸れる（layout 1位）
    ("反撃 (ヒサ×カド)",     Formation.Build(front2: UnitCatalog.Nono, front3: UnitCatalog.Gald, mid: UnitCatalog.Nel, back1: UnitCatalog.Hisa, back2: UnitCatalog.Kado)),
    // ヒサを前1へ回すと隣接はカドとノノになるが、標的は最大HPで選ばれるのでカドのままで狙いは崩れない。
    // カドを前2の中央に置くと巻き込みがヒサ・ムド・セロの3枚へ広がり、成長が速くなる（+7.1pt / 第5波 +19.3）。
    // 旧配置（ムド前1・ヒサ前3）はヒサの隣接をカドだけに絞る形だったが、カドの巻き込み先が2枚に減っていた（reseat 追試）
    ("惨禍×被弾強化",        Formation.Build(front1: UnitCatalog.Hisa, front2: UnitCatalog.Kado, front3: UnitCatalog.Mudo, mid: UnitCatalog.Sero, back1: UnitCatalog.Nono)),
    // 惨禍（味方全体の被ダメ5割増）は位置を問わないので、死の密度は隣接に頼らなくても出る。
    // リィカを後1へ下げて生贄をゾト1枚に絞り、中衛はヴェルに。リィカが開幕で自陣を削りすぎる形をやめた（+19.1pt / 第4波 +57.0）。
    // 旧配置（中衛リィカがカドとゾトを削る）は狙いとしては筋が通っていたが、第4波で 25% まで落ちていた（reseat 追試）
    ("惨禍×死の連鎖",        Formation.Build(front2: UnitCatalog.Golm, front3: UnitCatalog.Kado, mid: UnitCatalog.Vel, back1: UnitCatalog.Rica, back2: UnitCatalog.Zoto)),
    // ガルドは前列でないと庇えない。前1を空けてガルドとゴルムを前2・前3へ寄せた形が探索1位。セロは中衛から被弾後退（layout 1位）
    ("耐久 (ガルド×ノノ)",   Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Golm, mid: UnitCatalog.Sero, back1: UnitCatalog.Dolga, back2: UnitCatalog.Nono)),
    // ヒサを中衛に置くと横隣接が無く、深さ隣接の後2だけを指す。そこにカドを置けば標的は確定する。
    // カドを後2へ下げても囃し立てで被弾は来るので棘は回り、前列はガルドとドルガが受ける（+3.6pt / 第5波 +15.5）
    ("溜め (ガン×ドルガ×カド)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, mid: UnitCatalog.Hisa, back1: UnitCatalog.Gan, back2: UnitCatalog.Kado)),
    // グザの瘴気（味方全体に毒）は位置不問。ムドは前1で敵の攻撃も浴びて育ち、ガルドは前3で庇う。セロは中衛から被弾後退（layout 1位）
    ("毒→被弾強化 (グザ×ムド)", Formation.Build(front1: UnitCatalog.Mudo, front2: UnitCatalog.Guza, front3: UnitCatalog.Gald, mid: UnitCatalog.Sero, back1: UnitCatalog.Borg)),
    // ヴィオの吸い上げは全体対象で位置不問。スィドの毒漏れはむしろ燃料なので、中衛に置いて
    // 前後の隣接（後2のミオ）へわざと当てにいく。漏れを利益に反転する側と噛ませた形（+7.8pt / 第5波 +38.8）
    ("澱み喰い (グザ×ヴィオ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Guza, mid: UnitCatalog.Sid, back1: UnitCatalog.Vio, back2: UnitCatalog.Mio)),
    // 軋みの割り込み攻撃の追加後に再探索。セロが前1から中のヨミへ逃げ込んでヨミを前へ突き出し(+22)、その場で振らせる。
    // 以後はバサの入れ替えが割り込みを重ね、セロは二段目で後1のバサを突き飛ばして貫きに変わる（layout 1位）
    ("隊列崩し (バサ×ヨミ×セロ)", Formation.Build(front1: UnitCatalog.Sero, front2: UnitCatalog.Gan, front3: UnitCatalog.Gald, mid: UnitCatalog.Yomi, back1: UnitCatalog.Basa)),
    // 軋みの割り込み攻撃の追加後に再探索。セロが中衛から後1のヨミを突き飛ばして逃げ、ヨミは中衛へ突き出されて(+22)その場で振る。
    // 旧狙いの二段逃げ型（セロ前列→中のヨミ→後）は割り込み後も 48.8% 止まり（83位）。前列へ突き出されたヨミが削られるだけなので捨てた。
    // 探索1〜3位はガルド後列で庇いが死ぬので採らない（layout 4位）
    ("突き出し (セロ×ヨミ)",  Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald, mid: UnitCatalog.Sero, back1: UnitCatalog.Yomi, back2: UnitCatalog.Nel)),
    // 溜め役3体を敵から遠い後列と中衛へ、という狙いはそのまま。前1を空けてカド・クグを前2/前3へ寄せ、
    // 中衛をガンに替えた形が上（+2.1pt）。カドの巻き込み先はクグとガンで変わらない
    ("溜め改 (クグ×バン×ガン)", Formation.Build(front2: UnitCatalog.Kado, front3: UnitCatalog.Kugu, mid: UnitCatalog.Gan, back1: UnitCatalog.Ban, back2: UnitCatalog.Dolga)),
    // 軋みの割り込み攻撃の追加後に再探索。セロは前1から中のバサ、次に後1のヨミを順に突き飛ばして貫きに変わり、
    // 逃亡もバサの入れ替えも全部シオとヨミの燃料になる（layout 1位）
    ("移動改 (バサ×ヨミ×シオ)", Formation.Build(front1: UnitCatalog.Sero, front2: UnitCatalog.Shio, front3: UnitCatalog.Gald, mid: UnitCatalog.Basa, back1: UnitCatalog.Yomi)),
    // 呪詛は全体に漏れるのでウツの位置は不問。探索上位4件(80.8%)はガルド後列で庇いが死ぬので採らない。セロは中衛から被弾後退（layout 5位）
    ("逆しま (ネル×ウツ)",   Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald, mid: UnitCatalog.Sero, back1: UnitCatalog.Nel, back2: UnitCatalog.Utsu)),
    // 萎縮も呪詛も全体に効くので、守るべきは中衛のクビの方。ネルとウツを後列へ下げた（+3.1pt / 第5波 +14.5）。
    // 全体1位はガルドを後1に置く形（99.4%）だが庇いが死ぬので採らない。この差 +11.5pt は庇いの監査結果そのもの（README 参照）
    ("逆しま改 (クビ×ウツ)", Formation.Build(front2: UnitCatalog.Golm, front3: UnitCatalog.Gald, mid: UnitCatalog.Kubi, back1: UnitCatalog.Nel, back2: UnitCatalog.Utsu)),
    // 旧配置がそのまま全配置1位。ヒサの隣接（カド・ネル）で最大HPはカド（layout 1位）
    ("反撃改 (ドハ×カド)",   Formation.Build(front1: UnitCatalog.Hisa, front2: UnitCatalog.Kado, front3: UnitCatalog.Doha, mid: UnitCatalog.Nono, back1: UnitCatalog.Nel)),
    // ヒサを中衛へ。横隣接が無いので深さ隣接の前2＝カドだけを指す。前列3枚が受け、カドの巻き込みはドハ・バン・ヒサへ広がる
    // （+12.2pt / 第3波 +39.0）。旧配置はヒサ前3で標的は同じだが、前列が2枚しかなく第3波が 36% だった
    ("反撃改2 (ガン×カド)",  Formation.Build(front1: UnitCatalog.Doha, front2: UnitCatalog.Kado, front3: UnitCatalog.Ban, mid: UnitCatalog.Hisa, back1: UnitCatalog.Gan)),
    // ヒサを中衛へ。隣接はガン(前2)とカド(後2)だが、標的は最大HPで選ばれるのでカド。ガルドは前3で庇う
    // （+7.4pt / 第3波 +23.3）。ガルド前列の制約を外すと 73.1% まで伸びるが、差は +0.5pt なので制約を保つ側を採った
    ("反撃改3 (カド×ハギ)",  Formation.Build(front2: UnitCatalog.Gan, front3: UnitCatalog.Gald, mid: UnitCatalog.Hisa, back1: UnitCatalog.Hagi, back2: UnitCatalog.Kado)),
    // ハギは守られる中衛から追い打つ（位置不問）。前列3枚が受け、ミオは後1（layout: ガルド前列の最良）
    ("追撃×毒 (ハギ×グザ)",  Formation.Build(front1: UnitCatalog.Guza, front2: UnitCatalog.Gald, front3: UnitCatalog.Golm, mid: UnitCatalog.Hagi, back1: UnitCatalog.Mio)),
    // 死の連鎖にハギを足した形。2026-08-23 修正: 旧版はムグを残しヴェルを抜いていたため、
    // 死の連鎖の心臓部（継ぎ接ぎヴェルの蘇生による死体供給の倍加）が消えて第2波 98.0% → 32.5% まで
    // 落ちていた（原因はハギの1ターン1回制限ではなく、ヴェルを外したことそのもの）。
    // ムグを抜いてヴェルを残すと 95.0% まで戻る（分裂ムグの寄与は約3pt）。ハギの前列配置自体は
    // ほぼ無関係（ヴェルを残したままハギを前1に置いても95.0%）。配置は原型のスロットをそのまま流用。
    ("追撃×死 (ハギ×リィカ)", Formation.Build(front1: UnitCatalog.Hagi, front2: UnitCatalog.Zoto, mid: UnitCatalog.Golm, back1: UnitCatalog.Rica, back2: UnitCatalog.Vel)),
    // 軋みの割り込み攻撃の追加後に再探索。空き枠で孤立を作る散開の幾何はそのまま、ガルド(前1)とササ(前3)が孤立して-35%を受ける
    // （旧配置の左右鏡像 / layout 1位）
    ("移動改2 (ササ×ヨミ)",  Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sasa, mid: UnitCatalog.Basa, back2: UnitCatalog.Yomi)),
    // 孤立を2枚作る幾何は同じだが、孤立させる相手をガルドとドルガに替えた（旧配置はササ自身とガルド）。
    // ササは自分が孤立している必要がない。分かちは全体に効くのでドハは前1でよい（+2.8pt）
    ("散開耐久 (ササ×ドハ)", Formation.Build(front1: UnitCatalog.Doha, front3: UnitCatalog.Gald, mid: UnitCatalog.Dolga, back1: UnitCatalog.Sasa)),
    // セッキは後列でないと庇えない。探索上位は前列セッキで特性が死ぬので、後列制約下の最良を採る（layout 18位）
    ("死の連鎖+後備え", Formation.Build(front2: UnitCatalog.Zoto, front3: UnitCatalog.Golm, mid: UnitCatalog.Vel, back1: UnitCatalog.Rica, back2: UnitCatalog.Sekki)),
    // セロは中衛から後1のドルガを突き飛ばして逃げ込み、セッキが貫き以外の後列狙いを肩代わりして狙撃を守る。
    // セッキを後1に置く探索1位(86.0%)はセロがセッキを突き飛ばして後備えごと失うので採らない（layout 3位）
    ("後衛特化+後備え", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Golm, mid: UnitCatalog.Sero, back1: UnitCatalog.Dolga, back2: UnitCatalog.Sekki)),
    // ウツとセッキが後列、クビは守られる中衛。探索上位はセッキ前列＋ガルド中衛で両特性が死ぬので、制約下の最良を採る（layout 37位）
    ("逆しま+後備え",   Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Golm, mid: UnitCatalog.Kubi, back1: UnitCatalog.Sekki, back2: UnitCatalog.Utsu)),
    // 燃焼軸の受け皿編成。ホタ（熾火）は自分では着火できないので、ボルグの火の粉が唯一の火種。
    // 後1ホタと後2ボルグは同じ列で左右に隣接するので火は確実に回る。前列はノノとガルドで受け、
    // 火種と受け皿をまとめて後列に下げる形。reseat 1位を confirm で追試して採用
    // （seed 200..599 で +2.1pt / seed 600..1399 で +2.3pt）。
    // 中身は第4波を約3pt 差し出して第5波を約13pt 買う入れ替えで、全体が一様に伸びたわけではない。
    ("燃焼 (ボルグ×ホタ)", Formation.Build(front1: UnitCatalog.Nono, front2: UnitCatalog.Gald, front3: UnitCatalog.Mudo, back1: UnitCatalog.Hota, back2: UnitCatalog.Borg))
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
