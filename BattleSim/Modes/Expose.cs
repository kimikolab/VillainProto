using BattleCore;
using static Common;

// =====================================================================================
// expose モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "expose")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 expose
// =====================================================================================

static class ExposeDiag
{
public static void Run(string[] args, int stageIndex)
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
}
