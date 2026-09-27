using BattleCore;
using static Common;

// decoy phase0 —— Q0-1〜Q0-8（第226期）。**K0（前段の規定）だけで回る**——実装の前にコミットする。
static partial class DecoyDiag
{
    static readonly string[] Ids5 = { "hane", "basa", "yomi", "shio", "sero" };

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第226期 Phase 0 —— 回避盾のセロ・バサとハネの対・シオの段の時期（`0 decoy phase0`）");
        Console.WriteLine();
        Console.WriteLine("台: **M-ハネ（第225期の席）** " + SeatsNamed(MHane225) + "（シオは前段の規定＝J4・セロは規定）。参考: 雷の編成（仮の席）" + SeatsNamed(Thunder) + "。");
        Console.WriteLine($"seed 0..{Seeds - 1}。台本（`verbose: true`）から席・生死・移動を追う。");
        Console.WriteLine();

        Q01();
        Q02();
        Q0356();
        Q08();

        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    // ---------------------------------------------------------------------------------
    // Q0-1 標的の介入の順番（コードの順）と、M-ハネ・参考で実際に起きた介入の数
    // ---------------------------------------------------------------------------------
    static void Q01()
    {
        Console.WriteLine("## Q0-1 標的の介入の順番");
        Console.WriteLine();
        Console.WriteLine("`BattleContext.SelectTargetChain`（単体の一撃）の順。上ほど強い——**先に決まった段が後の段に上書きされるのではなく、後の段（介入）が主目標を差し替える**:");
        Console.WriteLine();
        Console.WriteLine("1. 的の固定（追い撃ち・乱れ撃ち＝第223期）——鎖を通さない");
        Console.WriteLine("2. 貫きは手前で分岐（突き・`SelectPierceEntry`）——以下の段を通らない");
        Console.WriteLine("3. **主目標**: 執着（ノミ）→ 断ちの選好（ナタ・ハリ）→ 見せしめ（シガ・動けない敵）→ `pool[Roll]`（前列 → 中列 → 全員）");
        Console.WriteLine("4. 薙ぎ・全体はここで後備え（セッキ）だけ見て返す");
        Console.WriteLine("5. **介入**（主目標を差し替える）: 標（`Marked`・75%・止めのトメは 100%）→ 後備え（後列の主目標だけ）→ 庇う（ガルド・前列）→ 殉教（敵）→ 棘守り（カド・構えているとき）");
        Console.WriteLine();
        Console.WriteLine("身代わり（カド）＝棘守り（5 の最後）。受け流し（ガルド）・逸らし（ソラ）は**標的を変えない**——`ApplyDamageBody` の中で当たった一撃の量を消す／半分を別の敵へ流す（逸らしは的を選び直さない）。");
        Console.WriteLine("ヒサの矢面（第184期）は標の段（標を付けた味方に被ダメ半減）。");
        Console.WriteLine();
        Console.WriteLine("**挑発の置き場所**: 3 の主目標の段の**最後**（`pool[Roll]` の代わり・見せしめの後）。セロが pool にいるときだけ主目標をセロにし、`Roll` を引かない。"
                          + "5 の介入はすべてその後ろに残るので、**既存の介入はどれも挑発より優先**（標・後備え・庇い・殉教・棘守りがセロから的を引き剥がせる）。"
                          + "攻撃者側の選好（執着・断ち・見せしめ）も挑発より優先。");
        Console.WriteLine();

        var sc = Scales[0].Sc;
        Console.WriteLine("M-ハネ・参考 × 九/新兵 × 200/200（K0）で起きた介入（1戦あたり・`Intercept` の出来事）:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 標 | 後備え | 庇う | 殉教 | 棘守り | 範囲の盾 | 敵の単体の一撃 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in new[] { ("M-ハネ（225）", MHane225), ("参考 雷（ポンの席）", Thunder) })
        {
            var cnt = new Dictionary<string, long>(); long single = 0;
            for (int s = 0; s < Seeds; s++)
            {
                var (r, p, e, _) = Fight(f, MainWave, sc, s);
                var enemyIds = e.Select(u => u.InstanceId).ToHashSet();
                foreach (var ev in r.Events)
                {
                    if (ev.Kind == BattleEventKind.Intercept && ev.Text is string t) cnt[t] = cnt.GetValueOrDefault(t) + 1;
                    if (ev.Kind == BattleEventKind.Attack && ev.ActorId is int a && enemyIds.Contains(a) && ev.Pattern == AttackPattern.Single) single++;
                }
            }
            string C(string l) => F2((double)cnt.GetValueOrDefault(l) / Seeds);
            Console.WriteLine($"| {name} | {C(InterceptLabels.Mark)} | {C(InterceptLabels.RearGuard)} | {C(InterceptLabels.Guardian)} | {C(InterceptLabels.Martyr)} | {C(InterceptLabels.ThornGuard)} | {C(InterceptLabels.RangeShield)} | {F2((double)single / Seeds)} |");
        }
        Console.WriteLine();
        Console.WriteLine("**M-ハネには介入を持つ駒が1枚もいない**——挑発はこの台では敵の単体の一撃を丸ごと引き寄せる（セロが pool にいる間）。");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Q0-2 セロの位置
    // ---------------------------------------------------------------------------------
    static void Q02()
    {
        Console.WriteLine("## Q0-2 セロが敵の単体攻撃の的になれる位置にいた割合（K0）");
        Console.WriteLine();
        Console.WriteLine("ターン ＝ ターンの頭（`TurnStart` の直前）にセロが生きていて pool（前列 → 中列 → 全員）にいたターンの割合。"
                          + "一撃 ＝ 敵の単体の一撃（`Attack`・`Single`）のうち、振る直前にセロが pool にいた割合。狙われた ＝ そのうち主目標がセロだった割合（今の `pool[Roll]`）。"
                          + "列 ＝ ターンの頭のセロの列（前 ／ 中 ／ 後 ／ 倒れ）。");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | ターン | 一撃 | 狙われた（pool にいたとき） | 前 ／ 中 ／ 後 ／ 倒れ | セロが pool に入った最初のターン（入った戦の平均） |");
        Console.WriteLine("|---|---|--:|--:|--:|---|--:|");
        foreach (var (scn, sc) in Scales)
            foreach (int w in new[] { 4, 5, 0, 1, 2, 3 })
            {
                if (scn != "200/200" && w != 4) continue;
                long turns = 0, inPool = 0, att = 0, attIn = 0, hitSero = 0, firstSum = 0, firstN = 0;
                long[] row = new long[4];
                for (int s = 0; s < Seeds; s++)
                {
                    var (r, p, e, slot0) = Fight(MHane225, w, sc, s);
                    int sero = p.First(u => u.Def.Id == "sero").InstanceId;
                    var enemyIds = e.Select(u => u.InstanceId).ToHashSet();
                    var tr = new Tracker(slot0, p);
                    int first = 0;
                    foreach (var ev in r.Events)
                    {
                        if (ev.Kind == BattleEventKind.TurnStart)
                        {
                            turns++;
                            bool alive = tr.Alive.Contains(sero);
                            if (!alive) row[3]++;
                            else row[FormationRules.DepthOf(FormationRules.RowOf(tr.Slot[sero]))]++;
                            if (alive && tr.Pool(true).Contains(sero)) { inPool++; if (first == 0) first = ev.Turn; }
                        }
                        if (ev.Kind == BattleEventKind.Attack && ev.ActorId is int a && enemyIds.Contains(a) && ev.Pattern == AttackPattern.Single && !ev.Reaction)
                        {
                            att++;
                            if (tr.Alive.Contains(sero) && tr.Pool(true).Contains(sero)) { attIn++; if (ev.TargetId == sero) hitSero++; }
                        }
                        tr.Apply(ev);
                    }
                    if (first > 0) { firstSum += first; firstN++; }
                }
                Console.WriteLine($"| {scn} | {WaveNames[w]} | {100.0 * inPool / Math.Max(1, turns):F1}% | {100.0 * attIn / Math.Max(1, att):F1}% | {100.0 * hitSero / Math.Max(1, attIn):F1}% | "
                                  + $"{100.0 * row[0] / turns:F0} ／ {100.0 * row[1] / turns:F0} ／ {100.0 * row[2] / turns:F0} ／ {100.0 * row[3] / turns:F0} | "
                                  + (firstN == 0 ? "—" : $"T{(double)firstSum / firstN:F1}（{100.0 * firstN / Seeds:F0}% の戦）") + " |");
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Q0-3 混乱の出どころ ／ Q0-5 敵の移動の累計 ／ Q0-6 止まり続け
    // ---------------------------------------------------------------------------------
    static void Q0356()
    {
        Console.WriteLine("## Q0-3 ／ Q0-5 ／ Q0-6 敵の乱れ（K0・M-ハネ 225 の席）");
        Console.WriteLine();
        Console.WriteLine("敵が動かされた ＝ 敵陣営の駒の `Move`（入れ替えなら2体で2回・出どころ ＝ `ActorId`）。前へ ＝ 行が前に変わった（`DepthOf` が減った）。"
                          + "混乱 ＝ 正気を失った（`Confused` 錯乱）／ 同士討ち（`Confused` 同士討ち＝混乱したまま振った）。");
        Console.WriteLine();
        string[] srcs = { "バサ", "ハネ", "セロ", "シオ", "ヨミ", "敵", "不明" };
        Console.WriteLine("| 倍率 | 波 | 敵が動かされた | " + string.Join(" | ", srcs) + " | 前へ | うちバサ以外で前へ | 錯乱 | 同士討ち | T1 末 ／ T2 末 ／ T3 末 ／ T4 末（累計） | 決着T |");
        Console.WriteLine("|---|---|--:|" + string.Concat(Enumerable.Repeat("--:|", srcs.Length)) + "--:|--:|--:|--:|---|--:|");
        var runs = new List<string>();
        foreach (var (scn, sc) in Scales)
            foreach (int w in new[] { 4, 5, 0, 1, 2, 3 })
            {
                if (scn != "200/200" && w != 4) continue;
                long moves = 0, adv = 0, advOther = 0, lost = 0, struck = 0, turns = 0;
                long[] bySrc = new long[srcs.Length];
                long[] cumAt = new long[5]; long[] cumN = new long[5];
                var totals = new List<int>();
                long stallStagger = 0, stallStun = 0, runSum = 0; int runMax = 0; long run2 = 0;
                for (int s = 0; s < Seeds; s++)
                {
                    var (r, p, e, slot0) = Fight(MHane225, w, sc, s);
                    turns += r.Turns;
                    var enemyIds = e.Select(u => u.InstanceId).ToHashSet();
                    var nameOf = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Id);
                    var tr = new Tracker(slot0, p);
                    int total = 0;
                    var cumByTurn = new int[31];
                    var lostTurns = new Dictionary<int, SortedSet<int>>();
                    foreach (var ev in r.Events)
                    {
                        if (ev.Kind == BattleEventKind.Move && ev.TargetId is int m && enemyIds.Contains(m))
                        {
                            moves++; total++; if (ev.Turn <= 30) cumByTurn[ev.Turn]++;
                            string src = ev.ActorId is int a && nameOf.TryGetValue(a, out var id)
                                ? (enemyIds.Contains(a) ? "敵" : id switch { "basa" => "バサ", "hane" => "ハネ", "sero" => "セロ", "shio" => "シオ", "yomi" => "ヨミ", _ => "不明" })
                                : "不明";
                            bySrc[Array.IndexOf(srcs, src)]++;
                            int before = tr.Slot[m];
                            if (FormationRules.DepthOf(FormationRules.RowOf(ev.Slot)) < FormationRules.DepthOf(FormationRules.RowOf(before)))
                            { adv++; if (src != "バサ") advOther++; }
                        }
                        if (ev.Kind == BattleEventKind.Confused && ev.TargetId is int c && enemyIds.Contains(c))
                        {
                            if (ev.Text == ConfusedLabels.Lost) lost++;
                            if (ev.Text == ConfusedLabels.Struck) { struck++; Lose(c, ev.Turn); }
                        }
                        if (ev.Kind == BattleEventKind.Stagger && ev.Text == StaggerLabels.Lost && ev.TargetId is int g && enemyIds.Contains(g)) { stallStagger++; Lose(g, ev.Turn); }
                        if (ev.Kind == BattleEventKind.Stun && ev.Text == StunLabels.Lost && ev.TargetId is int h && enemyIds.Contains(h)) { stallStun++; Lose(h, ev.Turn); }
                        tr.Apply(ev);
                    }
                    totals.Add(total);
                    for (int k = 1; k <= 4; k++) if (r.Turns >= k) { cumAt[k] += cumByTurn.Take(k + 1).Sum(); cumN[k]++; }
                    int mx = 0;
                    foreach (var set in lostTurns.Values)
                    {
                        int cur = 0, prev = -9;
                        foreach (int t in set) { cur = t == prev + 1 ? cur + 1 : 1; prev = t; if (cur > mx) mx = cur; }
                    }
                    runSum += mx; if (mx > runMax) runMax = mx; if (mx >= 2) run2++;

                    void Lose(int id, int t) { if (!lostTurns.TryGetValue(id, out var set)) lostTurns[id] = set = new(); set.Add(t); }
                }
                string cum = string.Join(" ／ ", Enumerable.Range(1, 4).Select(k => cumN[k] == 0 ? "—" : F1((double)cumAt[k] / cumN[k])));
                Console.WriteLine($"| {scn} | {WaveNames[w]} | {F2((double)moves / Seeds)} | " + string.Join(" | ", bySrc.Select(x => F2((double)x / Seeds))) + $" | {F2((double)adv / Seeds)} | {F2((double)advOther / Seeds)} | {F2((double)lost / Seeds)} | {F2((double)struck / Seeds)} | {cum} | {F2((double)turns / Seeds)} |");
                totals.Sort();
                runs.Add($"| {scn} | {WaveNames[w]} | {totals[totals.Count / 4]} ／ {totals[totals.Count / 2]} ／ {totals[3 * totals.Count / 4]} ／ {totals[^1]} | {F2((double)stallStagger / Seeds)} | {F2((double)stallStun / Seeds)} | {F2((double)struck / Seeds)} | {F2((double)runSum / Seeds)} | {100.0 * run2 / Seeds:F1}% | {runMax} |");
            }
        Console.WriteLine();
        Console.WriteLine("**Q0-3**: 今のバサは自分が入れ替えた敵のうち前へ出た敵にだけ混乱を立てる（`ShufflerTrait.Settle`）。**バサ以外の移動で前へ出た敵は混乱しない**——"
                          + "上の「うちバサ以外で前へ」が §3.1 で新しく混乱の候補になる数。混乱のキーは `StatusKeys.Confused`（二値）・上限はバサの私有キー `shuffleConfuseUsed`（`ShufflerRule.ConfuseUses` = 3・保持者1体・1戦あたり）。");
        Console.WriteLine();
        Console.WriteLine("### Q0-5 の分布と Q0-6（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("累計 ＝ 敵が動かされた回数の 1戦ごとの 四分位 ／ 中央値 ／ 四分位 ／ 最大。失った手番 ＝ 転倒 ／ 痺れ ／ 同士討ち（混乱のまま振った・手番を自軍に向けた）。"
                          + "続けて ＝ 同じ敵が続けて失った最大（戦ごとの最大を平均）／ 2以上の戦 ／ 最大。");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | 累計（四分位・中央・四分位・最大） | 転倒 | 痺れ | 同士討ち | 続けて（平均） | 2以上の戦 | 最大 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var l in runs) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine("**Q0-4**: `TormentTrait.IsBound`（シガの2倍・見せしめ・責め苦が共有）は 痺れ（`Stun` > 0）／ **そのターン手番を失った（`IdleTurn` == 今のターン）** ／ 組み付き ／ 竦み。"
                          + "**転倒（`Stagger` > 0）は入っていない**——転んだ敵が手番を失った<b>後</b>（同じターンのうち）だけ `IdleTurn` で数えられる。§3.2 は `Stagger` > 0 を足す（版の札の保持者がいる戦だけ）。");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Q0-8 台の駒
    // ---------------------------------------------------------------------------------
    static void Q08()
    {
        Console.WriteLine("## Q0-8 台の駒と敵");
        Console.WriteLine();
        Console.WriteLine("| 駒 | HP | 攻 | 速 | 札 |");
        Console.WriteLine("|---|--:|--:|--:|---|");
        foreach (var o in MHane225.Occupied())
            Console.WriteLine($"| {o.Def.Name} | {o.Def.MaxHp} | {o.Def.Attack} | {o.Def.Speed} | {string.Join(", ", o.Def.Traits)} |");
        Console.WriteLine();
        foreach (var (scn, sc) in Scales)
        {
            var e = WaveOf(MainWave, sc)();
            Console.WriteLine($"- 九/新兵 × {scn}: {e.Count} 体・HP {string.Join("/", e.Select(u => u.MaxHp).Distinct())}・攻 {string.Join("/", e.Select(u => u.Def.Attack).Distinct())}・速 {string.Join("/", e.Select(u => u.Def.Speed).Distinct())}"
                              + $"・1ターンの攻撃力の合計 {e.Sum(u => u.Def.Attack)}（型 {string.Join("/", e.Select(u => u.Def.Pattern).Distinct())}）");
        }
        var e2 = WaveOf(5, Scales[0].Sc)();
        Console.WriteLine($"- 九/農兵 × 200/200: {e2.Count} 体・HP {string.Join("/", e2.Select(u => u.MaxHp).Distinct())}・攻 {string.Join("/", e2.Select(u => u.Def.Attack).Distinct())}・1ターンの攻撃力の合計 {e2.Sum(u => u.Def.Attack)}");
        Console.WriteLine($"- 味方の最大HPの合計 {MHane225.Occupied().Sum(o => o.Def.MaxHp)}。");
        Console.WriteLine();

        // K0 の M-ハネ（225）× 3 倍率 × 九/新兵 の全員生存・勝率（予測の基準）
        Console.WriteLine("K0 の基準（M-ハネ 225 の席 × 九/新兵）:");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 勝率 | 全員生存 | 決着T | 落ちた駒 / 戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var (scn, sc) in Scales)
        {
            int wins = 0, surv = 0; long t = 0, fell = 0;
            for (int s = 0; s < Seeds; s++)
            {
                var (r, _, _, _) = Fight(MHane225, MainWave, sc, s, verbose: false);
                fell += r.PlayerStarterFallen.Count;
                if (!r.PlayerWon) continue;
                wins++; t += r.Turns; if (r.PlayerStarterFallen.Count == 0) surv++;
            }
            Console.WriteLine($"| {scn} | {100.0 * wins / Seeds:F1} | {100.0 * surv / Seeds:F1} | {(wins == 0 ? "—" : ((double)t / wins).ToString("F2"))} | {F2((double)fell / Seeds)} |");
        }
        Console.WriteLine();
    }
}
