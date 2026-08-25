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
