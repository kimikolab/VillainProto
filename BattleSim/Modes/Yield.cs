using BattleCore;
using static Common;

// =====================================================================================
// yield モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "yield")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 yield
// =====================================================================================

static class YieldDiag
{
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
public static void Run(string[] args, int stageIndex)
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
        (TraitId.Pursuer, "追打"), (TraitId.Venom, "毒撃"), (TraitId.VenomHeavy, "毒撃"), (TraitId.Miasma, "瘴気"),
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
    var nono = rows.Where(r => r.Def.Id == UnitCatalog.Lili.Id).ToArray();
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
}
