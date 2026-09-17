using BattleCore;
using static Common;

// =====================================================================================
// hush モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "hush")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 hush
// =====================================================================================

static class HushDiag
{
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
public static void Run(string[] args, int stageIndex)
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
}
