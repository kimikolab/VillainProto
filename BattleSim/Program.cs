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
    string[] seatName = { "前1", "前2", "前3", "中", "後1", "後2" }; // Formation.Build の引数と同じ 0..5

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
        ("1a 農兵6", Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Levy, front3: EnemyCatalog.Levy,
                                     mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back2: EnemyCatalog.Levy)),
        ("1b 農兵5", Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Levy, front3: EnemyCatalog.Levy,
                                     mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy)),
        ("1c 農兵5+斧", Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Axeman, front3: EnemyCatalog.Levy,
                                        mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back2: EnemyCatalog.Levy)),
    };

    // 第2波: 既存 def の再利用だけで第一波と第二波の中間を作る（中間に新造の個性は要らない）。
    // 2a→2c の順に重くなる。2c の狙撃手は貫き1枚の上限内。
    var w2 = new (string Name, Formation Enemy)[]
    {
        ("2a 新兵3+斧", Formation.Build(front1: EnemyCatalog.Recruit, front2: EnemyCatalog.Recruit, front3: EnemyCatalog.Recruit,
                                        mid: EnemyCatalog.Axeman)),
        ("2b 騎士混成", Formation.Build(front1: EnemyCatalog.Recruit, front2: EnemyCatalog.Knight, front3: EnemyCatalog.Recruit,
                                        mid: EnemyCatalog.Axeman)),
        ("2c 騎士2+狙撃", Formation.Build(front1: EnemyCatalog.Knight, front2: EnemyCatalog.Knight, front3: EnemyCatalog.Recruit,
                                          mid: EnemyCatalog.Archer)),
    };

    // 第3波: 少数高HP の精鋭。聖騎士長（第六波以降の素材・処刑持ち）と重装兵が素体。
    // 回復役の有無で性格が大きく変わるはずなので、司祭長入り(3b)となし(3a)の両方を測る。
    // 3c は2体の下限案（体数が減るほど範囲攻撃の意味が消え、単体火力有利が立つはず）。
    var w3 = new (string Name, Formation Enemy)[]
    {
        ("3a 精鋭3", Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion, front3: EnemyCatalog.Warden)),
        ("3b 精鋭+司祭長", Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion,
                                           mid: EnemyCatalog.Chaplain)),
        ("3c 精鋭2", Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion)),
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
            string[] seat = { "前1", "前2", "前3", "中", "後1", "後2" };
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
    // 配置は前1→前2→前3→中→後1→後2 の順に詰める（農兵候補と同じ規則）。
    //
    // 対照3案（1a/1b/1c）は gradient の w1 をそのまま写したもの。値が動いたら測り方が
    // 変わった証拠なので、先へ進まずに止まる（第6期 §2-5 の検算）。
    var cand = new (string Name, Formation Enemy)[]
    {
        ("1a 農兵6（対照）", Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Levy, front3: EnemyCatalog.Levy,
                                             mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back2: EnemyCatalog.Levy)),
        ("1b 農兵5（対照）", Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Levy, front3: EnemyCatalog.Levy,
                                             mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy)),
        ("1c 農兵5+斧（対照）", Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Axeman, front3: EnemyCatalog.Levy,
                                                mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back2: EnemyCatalog.Levy)),

        // H1 系: 高HP低攻。体数で総HPを積んで戦闘を伸ばす。H1a→H1c は体数だけの差で、
        // 総HP 270 / 225 / 180 と落ちる（H1c は農兵6と総HPが同じで総攻だけ半分の対照）。
        ("H1a 人足6", Formation.Build(front1: EnemyCatalog.Laborer, front2: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer,
                                      mid: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer, back2: EnemyCatalog.Laborer)),
        ("H1b 人足5", Formation.Build(front1: EnemyCatalog.Laborer, front2: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer,
                                      mid: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer)),
        ("H1c 人足4", Formation.Build(front1: EnemyCatalog.Laborer, front2: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer,
                                      mid: EnemyCatalog.Laborer)),

        // H2 系: 低HP高攻。H2a/H2b/H2c は体数5・総攻 80/T を固定して**個体HPだけ**を
        // 16 / 24 / 32 と振った軸（実測打点中央値の 2 / 3 / 4 発圏）。向きが出るとしたら
        // 「範囲で1手に複数落ちる」HP から出るはずで、その閾値を測定で挟む形。
        // H2d は体数を4に減らした案——向きが出たときに「体数を減らして代金の帯へ戻せるか」
        // （第6期 §3.3-3）を同じ実行で読むために置く。
        ("H2a 裸5(16)", Formation.Build(front1: EnemyCatalog.ZealotBare, front2: EnemyCatalog.ZealotBare, front3: EnemyCatalog.ZealotBare,
                                        mid: EnemyCatalog.ZealotBare, back1: EnemyCatalog.ZealotBare)),
        ("H2b 革5(24)", Formation.Build(front1: EnemyCatalog.ZealotLeather, front2: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather,
                                        mid: EnemyCatalog.ZealotLeather, back1: EnemyCatalog.ZealotLeather)),
        ("H2c 鎖5(32)", Formation.Build(front1: EnemyCatalog.ZealotMail, front2: EnemyCatalog.ZealotMail, front3: EnemyCatalog.ZealotMail,
                                        mid: EnemyCatalog.ZealotMail, back1: EnemyCatalog.ZealotMail)),
        ("H2d 革4(24)", Formation.Build(front1: EnemyCatalog.ZealotLeather, front2: EnemyCatalog.ZealotLeather, front3: EnemyCatalog.ZealotLeather,
                                        mid: EnemyCatalog.ZealotLeather)),

        // 中間点: 総HP × 1体あたり攻撃 の2軸で4点目を取る（低HP低攻=農兵 / 高HP低攻=H1 /
        // 低HP高攻=H2 / 中間=これ）。向きが軸のどちら側から出るかを単調性で読むための点。
        ("M1 傭兵5", Formation.Build(front1: EnemyCatalog.Drifter, front2: EnemyCatalog.Drifter, front3: EnemyCatalog.Drifter,
                                     mid: EnemyCatalog.Drifter, back1: EnemyCatalog.Drifter)),
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
            string[] seat = { "前1", "前2", "前3", "中", "後1", "後2" };
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
    // 増やさない / 新候補に範囲持ちの敵を入れない）。配置は前1→前2→前3→中→後1→後2 の順。
    //
    // 対照3案（3a/3b/3c）は gradient の w3 をそのまま写したもの。代金が第5期の
    // 61.0% / 52.5% / 44.3% と一致しなければ測り方が変わった証拠なので、先へ進まずに止まる。
    //
    // R0〜R6 は **攻16 固定・体数 × 個体HP の格子**。R0（鎖帷子32）は第6期の H2c と同じ素体で、
    // HP 軸 32 → 60 → 90 を1回の実行で繋ぐための橋（体数を4に揃えてある）。
    var cand = new (string Name, Formation Enemy)[]
    {
        ("3a 精鋭3（対照）", Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion,
                                             front3: EnemyCatalog.Warden)),
        ("3b 精鋭+司祭長（対照）", Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion,
                                                   mid: EnemyCatalog.Chaplain)),
        ("3c 精鋭2（対照）", Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion)),

        // HP 軸の起点。第6期 H2c（鎖5）の体数を4にしたもの。ここは +5.8pt 側（範囲に安い）
        // のはずで、そこから HP を厚くして符号が返るかを見る。
        ("R0 鎖4(32)", Formation.Build(front1: EnemyCatalog.ZealotMail, front2: EnemyCatalog.ZealotMail,
                                       front3: EnemyCatalog.ZealotMail, mid: EnemyCatalog.ZealotMail)),

        // 個体HP 60（上位1割の打点 51.1 でも1発では落ちない最初の刻み）× 体数 4 / 3 / 2。
        ("R1 板金4(60)", Formation.Build(front1: EnemyCatalog.ZealotPlate, front2: EnemyCatalog.ZealotPlate,
                                         front3: EnemyCatalog.ZealotPlate, mid: EnemyCatalog.ZealotPlate)),
        ("R2 板金3(60)", Formation.Build(front1: EnemyCatalog.ZealotPlate, front2: EnemyCatalog.ZealotPlate,
                                         front3: EnemyCatalog.ZealotPlate)),
        ("R3 板金2(60)", Formation.Build(front1: EnemyCatalog.ZealotPlate, front2: EnemyCatalog.ZealotPlate)),

        // 個体HP 90（上位1割の2発圏。最大打点 90.1 でようやく1発）× 体数 4 / 3 / 2。
        ("R4 重甲4(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat,
                                         front3: EnemyCatalog.ZealotGreat, mid: EnemyCatalog.ZealotGreat)),
        ("R5 重甲3(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat,
                                         front3: EnemyCatalog.ZealotGreat)),
        ("R6 重甲2(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat)),

        // 体数の上側（初回の格子を測ってから足した点）。初回は「体数を減らす」という
        // 鏡像の原理に従って 2〜4 体を測ったが、**結果は逆**だった——HP90 で
        // 2体 +2.8pt / 3体 +2.6pt / 4体 -2.5pt。体数が多いほど反転側へ動く。
        // 理屈は読める——体数が少ないと範囲攻撃は単体と同じになるだけで損をしない。
        // 損をするのは「倒しきれない相手がたくさん並んでいる」とき。その向きに伸ばして頑張る。
        ("R8 重甲5(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat,
                                         front3: EnemyCatalog.ZealotGreat, mid: EnemyCatalog.ZealotGreat,
                                         back1: EnemyCatalog.ZealotGreat)),
        ("R9 重甲6(90)", Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat,
                                         front3: EnemyCatalog.ZealotGreat, mid: EnemyCatalog.ZealotGreat,
                                         back1: EnemyCatalog.ZealotGreat, back2: EnemyCatalog.ZealotGreat)),
        // 体数の上側を HP60 側でも取る（体数と個体HP のどちらが効いているかの分離）。
        ("R10 板金6(60)", Formation.Build(front1: EnemyCatalog.ZealotPlate, front2: EnemyCatalog.ZealotPlate,
                                          front3: EnemyCatalog.ZealotPlate, mid: EnemyCatalog.ZealotPlate,
                                          back1: EnemyCatalog.ZealotPlate, back2: EnemyCatalog.ZealotPlate)),

        // 3軸（体数↑・個体HP↑・1体あたり攻撃↓）を全部重ねた点。攻撃だけを 16 → 10 に
        // 下げてある——R9（重甲6体・攻16）が 10編成を勝率 0% に落として打ち切りバイアスを
        // 拾ったので、同じ盤面を全編成が勝ち切れる高さに戻すための一手。
        ("R11 従卒6(90/攻10)", Formation.Build(front1: EnemyCatalog.ZealotSquire, front2: EnemyCatalog.ZealotSquire,
                                               front3: EnemyCatalog.ZealotSquire, mid: EnemyCatalog.ZealotSquire,
                                               back1: EnemyCatalog.ZealotSquire, back2: EnemyCatalog.ZealotSquire)),
        ("R12 従卒5(90/攻10)", Formation.Build(front1: EnemyCatalog.ZealotSquire, front2: EnemyCatalog.ZealotSquire,
                                               front3: EnemyCatalog.ZealotSquire, mid: EnemyCatalog.ZealotSquire,
                                               back1: EnemyCatalog.ZealotSquire)),

        // 処刑ありなしの対照（第7期 §2-4）。3a と数値は完全に同じで、聖騎士長の特性だけを
        // 落としてある。差が出れば「反転の一部は処刑が作っている」ことになる。
        ("R7 精鋭3・処刑なし（対照）", Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.ChampionPlain,
                                                       front3: EnemyCatalog.Warden)),
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
            string[] seat = { "前1", "前2", "前3", "中", "後1", "後2" };
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
// | 反転列 | H2a 裸5（+8.7pt） | 2b 騎士混成 | R11 従卒6（-8.4pt） | 列の中で符号が反転する |
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
    Formation W1Levy5 = Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Levy,
                                        front3: EnemyCatalog.Levy, mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy);
    Formation W1ZealotBare5 = Formation.Build(front1: EnemyCatalog.ZealotBare, front2: EnemyCatalog.ZealotBare,
                                              front3: EnemyCatalog.ZealotBare, mid: EnemyCatalog.ZealotBare,
                                              back1: EnemyCatalog.ZealotBare);
    Formation W1ZealotLeather4 = Formation.Build(front1: EnemyCatalog.ZealotLeather, front2: EnemyCatalog.ZealotLeather,
                                                 front3: EnemyCatalog.ZealotLeather, mid: EnemyCatalog.ZealotLeather);
    Formation W2Mixed = Formation.Build(front1: EnemyCatalog.Recruit, front2: EnemyCatalog.Knight,
                                        front3: EnemyCatalog.Recruit, mid: EnemyCatalog.Axeman);
    Formation W3Elite3 = Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion,
                                         front3: EnemyCatalog.Warden);
    Formation W3Squire6 = Formation.Build(front1: EnemyCatalog.ZealotSquire, front2: EnemyCatalog.ZealotSquire,
                                          front3: EnemyCatalog.ZealotSquire, mid: EnemyCatalog.ZealotSquire,
                                          back1: EnemyCatalog.ZealotSquire, back2: EnemyCatalog.ZealotSquire);
    // 第8期 Phase V。第3波の代金だけを振る（体数・個体HP は R11 と同じで攻撃だけが違う）。
    Formation W3Porter6 = Formation.Build(front1: EnemyCatalog.ZealotPorter, front2: EnemyCatalog.ZealotPorter,
                                          front3: EnemyCatalog.ZealotPorter, mid: EnemyCatalog.ZealotPorter,
                                          back1: EnemyCatalog.ZealotPorter, back2: EnemyCatalog.ZealotPorter);
    Formation W3Pilgrim6 = Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front2: EnemyCatalog.ZealotPilgrim,
                                           front3: EnemyCatalog.ZealotPilgrim, mid: EnemyCatalog.ZealotPilgrim,
                                           back1: EnemyCatalog.ZealotPilgrim, back2: EnemyCatalog.ZealotPilgrim);

    var columns = new (string Name, string Note, Formation[] Squads)[]
    {
        ("平坦列", "1b 農兵5(+3.1pt) / 2b 騎士混成 / 3a 精鋭3(-0.5pt)。第5期の推奨列",
            new[] { W1Levy5, W2Mixed, W3Elite3 }),
        ("反転列", "H2a 裸5(+8.7pt) / 2b 騎士混成 / R11 従卒6(-8.4pt)。列の中で符号が反転する",
            new[] { W1ZealotBare5, W2Mixed, W3Squire6 }),
        ("反転列(難度そろえ)", "H2d 革4(+7.4pt・代金 27.2% は 1b とほぼ同額) / 2b / R11 従卒6",
            new[] { W1ZealotLeather4, W2Mixed, W3Squire6 }),
        // 第8期 Phase V。反転列と第1波・第2波は同じで、第3波の代金だけが違う3点。
        ("反転列(中)", "H2a 裸5 / 2b / 荷駄6(90/攻7・代金 54%)。合計を下げて境目に近づける",
            new[] { W1ZealotBare5, W2Mixed, W3Porter6 }),
        ("反転列(低)", "H2a 裸5 / 2b / 巡礼6(90/攻4・代金 42%)。**向きは -2.2pt しか無い**",
            new[] { W1ZealotBare5, W2Mixed, W3Pilgrim6 }),
        // 低の群差の対照。第1波だけを向きの無い 1b に戻した以外は反転列(低)と同じ。
        // 反転列(低) で範囲持ちの Δ が開いたとき、それが「列の向き（第1波 +8.7pt）が
        // 境目で結果に出た」のか「安い波は範囲持ちに有利なだけ」なのかを分ける。
        ("平坦列(低)", "1b 農兵5(+3.1pt) / 2b / 巡礼6。反転列(低) の群差の対照",
            new[] { W1Levy5, W2Mixed, W3Pilgrim6 }),
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
    Console.WriteLine("列は 第8期の 反転列(低)（H2a 裸5 / 2b 騎士混成 / 巡礼6）。合計代金 113% の測定台。");
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
    //   第8期     bridge の 荷駄6 / 巡礼6（第3波の代金だけを振った点。攻10 は R11 と同じ）
    //   第10期    ChargeBench の第2波・第3波（第1波は H2a と同一なので重複させない）
    //
    // **現物が無くて入れられなかった候補が2つある。** 第8期に測った「攻5 版」（90/攻5）と
    // 「板金従卒6」（60/攻7）は `UnitCatalog` に `UnitDef` が残っていない（攻5 は刻みとして
    // 測っただけ、板金従卒は「却下した案」として文章にだけ残っている）。BattleCore を触らない
    // 作業なので**新しい敵は作らない**——集めるのは現物のある波だけにして、無い2つは出力に明記する。
    var waves = new (string Tag, string Era, string Name, Formation Enemy)[]
    {
        ("S1",  "既存",   "第一波 / 物見の兵",      EnemyCatalog.Stages[0].Enemy),
        ("S2",  "既存",   "第二波 / 巡礼騎士団",    EnemyCatalog.Stages[1].Enemy),
        ("S3",  "既存",   "第三波 / 討伐隊本隊",    EnemyCatalog.Stages[2].Enemy),
        ("S4",  "既存",   "第四波 / 城塞守備隊",    EnemyCatalog.Stages[3].Enemy),
        ("S5",  "既存",   "第五波 / 異端審問団",    EnemyCatalog.Stages[4].Enemy),

        // 第5期 gradient の w1 / w2 / w3。
        ("G1a", "第5期",  "1a 農兵6",
            Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Levy, front3: EnemyCatalog.Levy,
                            mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back2: EnemyCatalog.Levy)),
        ("G1b", "第5期",  "1b 農兵5",
            Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Levy, front3: EnemyCatalog.Levy,
                            mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy)),
        ("G1c", "第5期",  "1c 農兵5+斧",
            Formation.Build(front1: EnemyCatalog.Levy, front2: EnemyCatalog.Axeman, front3: EnemyCatalog.Levy,
                            mid: EnemyCatalog.Levy, back1: EnemyCatalog.Levy, back2: EnemyCatalog.Levy)),
        ("G2a", "第5期",  "2a 新兵3+斧",
            Formation.Build(front1: EnemyCatalog.Recruit, front2: EnemyCatalog.Recruit,
                            front3: EnemyCatalog.Recruit, mid: EnemyCatalog.Axeman)),
        ("G2b", "第5期",  "2b 騎士混成",
            Formation.Build(front1: EnemyCatalog.Recruit, front2: EnemyCatalog.Knight,
                            front3: EnemyCatalog.Recruit, mid: EnemyCatalog.Axeman)),
        ("G2c", "第5期",  "2c 騎士2+狙撃",
            Formation.Build(front1: EnemyCatalog.Knight, front2: EnemyCatalog.Knight,
                            front3: EnemyCatalog.Recruit, mid: EnemyCatalog.Archer)),
        ("G3a", "第5期",  "3a 精鋭3",
            Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion, front3: EnemyCatalog.Warden)),
        ("G3b", "第5期",  "3b 精鋭+司祭長",
            Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion, mid: EnemyCatalog.Chaplain)),
        ("G3c", "第5期",  "3c 精鋭2",
            Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.Champion)),

        // 第6期 aim。H1 系（高HP低攻）・H2 系（低HP高攻）・M1（中間点）。
        ("H1a", "第6期",  "H1a 人足6",
            Formation.Build(front1: EnemyCatalog.Laborer, front2: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer,
                            mid: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer, back2: EnemyCatalog.Laborer)),
        ("H1b", "第6期",  "H1b 人足5",
            Formation.Build(front1: EnemyCatalog.Laborer, front2: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer,
                            mid: EnemyCatalog.Laborer, back1: EnemyCatalog.Laborer)),
        ("H1c", "第6期",  "H1c 人足4",
            Formation.Build(front1: EnemyCatalog.Laborer, front2: EnemyCatalog.Laborer, front3: EnemyCatalog.Laborer,
                            mid: EnemyCatalog.Laborer)),
        ("H2a", "第6期",  "H2a 裸5(16)",
            Formation.Build(front1: EnemyCatalog.ZealotBare, front2: EnemyCatalog.ZealotBare,
                            front3: EnemyCatalog.ZealotBare, mid: EnemyCatalog.ZealotBare,
                            back1: EnemyCatalog.ZealotBare)),
        ("H2b", "第6期",  "H2b 革5(24)",
            Formation.Build(front1: EnemyCatalog.ZealotLeather, front2: EnemyCatalog.ZealotLeather,
                            front3: EnemyCatalog.ZealotLeather, mid: EnemyCatalog.ZealotLeather,
                            back1: EnemyCatalog.ZealotLeather)),
        ("H2c", "第6期",  "H2c 鎖5(32)",
            Formation.Build(front1: EnemyCatalog.ZealotMail, front2: EnemyCatalog.ZealotMail,
                            front3: EnemyCatalog.ZealotMail, mid: EnemyCatalog.ZealotMail,
                            back1: EnemyCatalog.ZealotMail)),
        ("H2d", "第6期",  "H2d 革4(24)",
            Formation.Build(front1: EnemyCatalog.ZealotLeather, front2: EnemyCatalog.ZealotLeather,
                            front3: EnemyCatalog.ZealotLeather, mid: EnemyCatalog.ZealotLeather)),
        ("M1",  "第6期",  "M1 傭兵5",
            Formation.Build(front1: EnemyCatalog.Drifter, front2: EnemyCatalog.Drifter, front3: EnemyCatalog.Drifter,
                            mid: EnemyCatalog.Drifter, back1: EnemyCatalog.Drifter)),

        // 第7期 flip。R0〜R6・R8〜R12 は 体数 × 個体HP の格子、R7 は処刑なしの対照。
        ("R0",  "第7期",  "R0 鎖4(32)",
            Formation.Build(front1: EnemyCatalog.ZealotMail, front2: EnemyCatalog.ZealotMail,
                            front3: EnemyCatalog.ZealotMail, mid: EnemyCatalog.ZealotMail)),
        ("R1",  "第7期",  "R1 板金4(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front2: EnemyCatalog.ZealotPlate,
                            front3: EnemyCatalog.ZealotPlate, mid: EnemyCatalog.ZealotPlate)),
        ("R2",  "第7期",  "R2 板金3(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front2: EnemyCatalog.ZealotPlate,
                            front3: EnemyCatalog.ZealotPlate)),
        ("R3",  "第7期",  "R3 板金2(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front2: EnemyCatalog.ZealotPlate)),
        ("R4",  "第7期",  "R4 重甲4(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat,
                            front3: EnemyCatalog.ZealotGreat, mid: EnemyCatalog.ZealotGreat)),
        ("R5",  "第7期",  "R5 重甲3(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat,
                            front3: EnemyCatalog.ZealotGreat)),
        ("R6",  "第7期",  "R6 重甲2(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat)),
        ("R7",  "第7期",  "R7 精鋭3・処刑なし（3a と数値は同じ）",
            Formation.Build(front1: EnemyCatalog.Warden, front2: EnemyCatalog.ChampionPlain, front3: EnemyCatalog.Warden)),
        ("R8",  "第7期",  "R8 重甲5(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat,
                            front3: EnemyCatalog.ZealotGreat, mid: EnemyCatalog.ZealotGreat,
                            back1: EnemyCatalog.ZealotGreat)),
        ("R9",  "第7期",  "R9 重甲6(90)",
            Formation.Build(front1: EnemyCatalog.ZealotGreat, front2: EnemyCatalog.ZealotGreat,
                            front3: EnemyCatalog.ZealotGreat, mid: EnemyCatalog.ZealotGreat,
                            back1: EnemyCatalog.ZealotGreat, back2: EnemyCatalog.ZealotGreat)),
        ("R10", "第7期",  "R10 板金6(60)",
            Formation.Build(front1: EnemyCatalog.ZealotPlate, front2: EnemyCatalog.ZealotPlate,
                            front3: EnemyCatalog.ZealotPlate, mid: EnemyCatalog.ZealotPlate,
                            back1: EnemyCatalog.ZealotPlate, back2: EnemyCatalog.ZealotPlate)),
        ("R11", "第7期",  "R11 従卒6(90/攻10)",
            Formation.Build(front1: EnemyCatalog.ZealotSquire, front2: EnemyCatalog.ZealotSquire,
                            front3: EnemyCatalog.ZealotSquire, mid: EnemyCatalog.ZealotSquire,
                            back1: EnemyCatalog.ZealotSquire, back2: EnemyCatalog.ZealotSquire)),
        ("R12", "第7期",  "R12 従卒5(90/攻10)",
            Formation.Build(front1: EnemyCatalog.ZealotSquire, front2: EnemyCatalog.ZealotSquire,
                            front3: EnemyCatalog.ZealotSquire, mid: EnemyCatalog.ZealotSquire,
                            back1: EnemyCatalog.ZealotSquire)),

        // 第8期 bridge。R11 と体数・個体HP は同じで攻撃だけが違う（代金を振った軸）。
        ("P6",  "第8期",  "荷駄6(90/攻7)",
            Formation.Build(front1: EnemyCatalog.ZealotPorter, front2: EnemyCatalog.ZealotPorter,
                            front3: EnemyCatalog.ZealotPorter, mid: EnemyCatalog.ZealotPorter,
                            back1: EnemyCatalog.ZealotPorter, back2: EnemyCatalog.ZealotPorter)),
        ("Q6",  "第8期",  "巡礼6(90/攻4)",
            Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front2: EnemyCatalog.ZealotPilgrim,
                            front3: EnemyCatalog.ZealotPilgrim, mid: EnemyCatalog.ZealotPilgrim,
                            back1: EnemyCatalog.ZealotPilgrim, back2: EnemyCatalog.ZealotPilgrim)),

        // 第10期 ChargeBench の第2波・第3波。**候補波の中で貫き・全体を持つのはここだけ**
        // （第6期以降の候補は「敵の攻撃型は測定の交絡になる」として単体で揃えてある）。
        ("C2",  "第10期", "チャージ台2波 新兵2+騎士+狙撃(貫き)", ChargeBench()[1]),
        ("C3",  "第10期", "チャージ台3波 巡礼3+詠唱兵(全体)",   ChargeBench()[2]),
    };
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
    Console.WriteLine("> 「板金従卒6」（60/攻7）は `UnitCatalog` に `UnitDef` が残っていない（前者は刻みとして");
    Console.WriteLine("> 測っただけ、後者は「却下した案」として文章にだけ残っている）。**BattleCore を触らない**");
    Console.WriteLine("> 作業なので新しい敵は作らず、集めるのは現物のある波だけにした。");
    Console.WriteLine();
    Console.WriteLine("| タグ | 出どころ | 波 | 体数 | 総HP | 総攻 | 中身（HP/攻/速/型） |");
    Console.WriteLine("|:-:|:-:|---|--:|--:|--:|---|");
    foreach (var (tag, era, name, enemy) in waves)
    {
        string[] seat = { "前1", "前2", "前3", "中", "後1", "後2" };
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
            Formation.Build(front2: UnitCatalog.Zoto, front3: UnitCatalog.Mug, mid: UnitCatalog.Golm, back1: UnitCatalog.Rica, back2: UnitCatalog.Vel)),
        // ここからガルドの「拡散」（味方全体に配られる強化・弱体を隣接へ流す）に伴う再探索ぶん。
        // 旧配置はどれも「拡散が空振りする」置き方だった（受け手が隣接していない）。
        ("速攻 (ボルグ×ムド)",
            Formation.Build(front1: UnitCatalog.Mudo, front2: UnitCatalog.Sero, front3: UnitCatalog.Gald, mid: UnitCatalog.Nel, back1: UnitCatalog.Borg),
            Formation.Build(front1: UnitCatalog.Mudo, front2: UnitCatalog.Nel, front3: UnitCatalog.Gald, mid: UnitCatalog.Borg, back1: UnitCatalog.Sero)),
        ("逆しま+後備え",
            Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Golm, mid: UnitCatalog.Kubi, back1: UnitCatalog.Sekki, back2: UnitCatalog.Utsu),
            Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Golm, front3: UnitCatalog.Kubi, back1: UnitCatalog.Utsu, back2: UnitCatalog.Sekki)),
        ("隊列崩し (バサ×ヨミ×セロ)",
            Formation.Build(front1: UnitCatalog.Sero, front2: UnitCatalog.Gan, front3: UnitCatalog.Gald, mid: UnitCatalog.Yomi, back1: UnitCatalog.Basa),
            Formation.Build(front1: UnitCatalog.Sero, front2: UnitCatalog.Gald, front3: UnitCatalog.Gan, mid: UnitCatalog.Yomi, back1: UnitCatalog.Basa)),
        ("溜め (ガン×ドルガ×カド)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, mid: UnitCatalog.Hisa, back1: UnitCatalog.Gan, back2: UnitCatalog.Kado),
            Formation.Build(front1: UnitCatalog.Dolga, front2: UnitCatalog.Gald, front3: UnitCatalog.Gan, mid: UnitCatalog.Kado, back2: UnitCatalog.Hisa)),
        ("反撃改3 (カド×ハギ)",
            Formation.Build(front2: UnitCatalog.Gan, front3: UnitCatalog.Gald, mid: UnitCatalog.Hisa, back1: UnitCatalog.Hagi, back2: UnitCatalog.Kado),
            Formation.Build(front1: UnitCatalog.Gan, front2: UnitCatalog.Gald, front3: UnitCatalog.Hagi, mid: UnitCatalog.Kado, back2: UnitCatalog.Hisa))
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
    ("速攻 (ボルグ×ムド)",   Formation.Build(front1: UnitCatalog.Mudo, front2: UnitCatalog.Nel, front3: UnitCatalog.Gald, mid: UnitCatalog.Borg, back1: UnitCatalog.Sero)),
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
    ("隊列崩し (バサ×ヨミ×セロ)", Formation.Build(front1: UnitCatalog.Sero, front2: UnitCatalog.Gald, front3: UnitCatalog.Gan, mid: UnitCatalog.Yomi, back1: UnitCatalog.Basa)),
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
    ("反撃改3 (カド×ハギ)",  Formation.Build(front1: UnitCatalog.Gan, front2: UnitCatalog.Gald, front3: UnitCatalog.Hagi, mid: UnitCatalog.Kado, back2: UnitCatalog.Hisa)),
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
    ("逆しま+後備え",   Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Golm, front3: UnitCatalog.Kubi, back1: UnitCatalog.Utsu, back2: UnitCatalog.Sekki)),
    // 燃焼軸の受け皿編成。ホタ（熾火）は自分では着火できないので、ボルグの火の粉が唯一の火種。
    // 後1ホタと後2ボルグは同じ列で左右に隣接するので火は確実に回る。前列はノノとガルドで受け、
    // 火種と受け皿をまとめて後列に下げる形。reseat 1位を confirm で追試して採用
    // （seed 200..599 で +2.1pt / seed 600..1399 で +2.3pt）。
    // 中身は第4波を約3pt 差し出して第5波を約13pt 買う入れ替えで、全体が一様に伸びたわけではない。
    ("燃焼 (ボルグ×ホタ)", Formation.Build(front1: UnitCatalog.Nono, front2: UnitCatalog.Gald, front3: UnitCatalog.Mudo, back1: UnitCatalog.Hota, back2: UnitCatalog.Borg)),
    // 範囲耐性。砕け盾のヒビ（範囲を浴びて破片を配る）を軸に据えた編成。
    // ガルドは Stoic で回復も強化も受け付けないが、破片は damage 側で消費されるので届く。
    // ドルガ（攻38・薙ぎだが2ターンに1回）は「強い。ただ遅い」という理由で外された駒で、
    // 守られて初めて完走できる。ablate でヒビを抜くと 92.2% → 大きく落ちる。
    //
    // 配置は reseat 1位（94.7%）ではなく狙いを優先して据え置き（92.2%）。
    // ヒビを前列に置き、ボルグと横に隣接させることが狙い。ボルグの薙ぎは味方も巻き込むが、
    // その巻き込みも CurrentPattern != Single なのでヒビの変換対象になる。
    // 探索1位はボルグを後列へ回してこの噛み合わせを捨てる形なので採らない。
    ("範囲耐性 (ヒビ×ボルグ)", Formation.Build(front1: UnitCatalog.Gald, front2: UnitCatalog.Hibi, front3: UnitCatalog.Borg, mid: UnitCatalog.Dolga, back1: UnitCatalog.Rica))
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
// 反転列(低) と同一（H2a 裸5 / 2b 騎士混成 / 巡礼6）で、bill（代金の分解）と
// bridge（自傷率での群分け）が同じ台の上で測るために1箇所へ寄せてある。
// bridge 側は列の定義を自分で持ったまま、この関数と一致することを検算する
// （列の定義を bridge から取り上げると第8期の出力との突き合わせが読めなくなる）。
static Formation[] BenchColumn113() => new[]
{
    Formation.Build(front1: EnemyCatalog.ZealotBare, front2: EnemyCatalog.ZealotBare,
                    front3: EnemyCatalog.ZealotBare, mid: EnemyCatalog.ZealotBare,
                    back1: EnemyCatalog.ZealotBare),
    Formation.Build(front1: EnemyCatalog.Recruit, front2: EnemyCatalog.Knight,
                    front3: EnemyCatalog.Recruit, mid: EnemyCatalog.Axeman),
    Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front2: EnemyCatalog.ZealotPilgrim,
                    front3: EnemyCatalog.ZealotPilgrim, mid: EnemyCatalog.ZealotPilgrim,
                    back1: EnemyCatalog.ZealotPilgrim, back2: EnemyCatalog.ZealotPilgrim),
};

// チャージ台（第10期 Phase AB-0）。**測定台 113% には全体持ちも貫き持ちも1体もいない**
// （裸5 / 新兵・騎士・戦斧兵 / 巡礼6）。第9期までは敵の攻撃型が測定の交絡になるので
// わざと外してあったが、第10期はその2種にチャージを付ける期なので、あの台の上で測ると
// チャージ化の前後で数字が1つも動かない。
//
// そこで測定台の骨格（第1波 裸5 / 第3波 巡礼者・合計代金 113% 帯）を保ったまま、
// 貫きを1枚（第2波の戦斧兵→狙撃手）、全体を1枚（第3波の巡礼者1体→詠唱兵）だけ入れ替えた列を
// 別に作る。**入れ替えであって追加ではない**ので、ステージ設計の「貫き1枚まで／全体1枚まで」
// （UnitCatalog.cs の第三波・第四波のコメント）を跨がない。
//
// 入れ替えで代金が上がった（巡礼6のまま詠唱兵を入れると合計 128.7%）ので、第3波の体数を
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
    Formation.Build(front1: EnemyCatalog.ZealotBare, front2: EnemyCatalog.ZealotBare,
                    front3: EnemyCatalog.ZealotBare, mid: EnemyCatalog.ZealotBare,
                    back1: EnemyCatalog.ZealotBare),
    // 2b 騎士混成 の戦斧兵（薙ぎ）を狙撃手（貫き）に。スロットはそのまま中衛。
    Formation.Build(front1: EnemyCatalog.Recruit, front2: EnemyCatalog.Knight,
                    front3: EnemyCatalog.Recruit, mid: EnemyCatalog.Archer),
    // 巡礼者を詠唱兵（全体）入りに。既存の第四波と同じくレーン1の最深部（後2）に置く。
    // 体数 4 は合計代金を 113% 帯へ戻すための刻み（上のコメント参照）。
    Formation.Build(front1: EnemyCatalog.ZealotPilgrim, front2: EnemyCatalog.ZealotPilgrim,
                    front3: EnemyCatalog.ZealotPilgrim, back2: EnemyCatalog.Chanter),
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
