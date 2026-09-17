using BattleCore;
using static Common;

// =====================================================================================
// sever モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "sever")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 sever
// =====================================================================================

static class SeverDiag
{
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
public static void Run(string[] args, int stageIndex)
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
}
