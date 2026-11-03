using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 1-2 に「盤の上で変わるもの」を足した門（第441期 §5-3・`--map12-change-smoke`）
//
// **画面を1つも作らない。** 1-2（途中到着・敵の予備隊・湧き S = 2）と `Map11State` / `Map11Orders` だけを回す。
// **(a)〜(f) と分解は第441期の盤（`MapStage.Stage12H441`・3 隊）**で回す（回帰の基準）。(a') だけは第442期 §0-2 で作り替えた。
// **ここには Godot の型を1つも使わない**（`Map12Smoke` と同じ扱い）。
//
//   (a) 開始の手札が 10 枚で保証 3 条件を満たす・到着の列が 5 枚
//   (b) A 刻ごとに手札が増え、5 枚届いたら止まる
//   (c) 最初の接敵の後、予備隊が「味方のいない経路」へ出発し、その経路を 1 刻 1 マス進む
//   (d) 予備隊が拠点に着いたら迎撃が立つ
//   (e) S = 2 で制圧の前に湧きが 1 以上
//   (f) 貪欲方針 100 seed で落ちない。踏破率・予備隊が着いた経路の分布・予備隊との戦闘が起きた率・湧いた数は**報告だけ**
//
// 第442期（§1・遊ぶ 1-2 ＝ `MapStage.Stage12`・隊 2 つ・中の道の先頭は 1 マス奥）:
//   (g) 既定の組み方で始めると、開始の 10 枚が 2 隊に配られ、到着の列は 5 枚のまま
//   (h) 3 隊目は作れない
//   (i) 中の道の先頭が拠点に着くのは 3 刻目（北・南は 2 刻目のまま）
//   貪欲 100 seed（2 隊とも出す ／ 1 隊残す）の数字は**報告だけ**（第441期と並べる）
// **第443期からは (g)〜(i) と第442期の表は第442期の盤（`MapStage.Stage12H442`）で回す**（回帰の基準）。
//
// 第443期（§1-3・遊ぶ 1-2 ＝ `MapStage.Stage12`・隊 3 つ・到着 2 刻ごと 2 枚・S=3・予備隊は第二波・先遣）:
//   (j) 同じ刻に 2 部隊が拠点に着いたとき、1 戦目の後は「迎撃の合間」（`Map11Session.InterceptBreak`）で、
//       `CanReform` が真・2 戦目の前に手札から入れ替えられる（1 戦目の前は合間ではない）
//   (k) 3 隊目は空で始まり、届いた駒で組める
//   (l) 既定の値（S=3・予備隊は第二波・先遣・隊 3・到着 2 刻ごと 2 枚）
//   5 つの盤 × 3 方針 × 100 seed の表（報告だけ・線は置かない）。器具の方針は迎撃の合間に何もしない
// **第444期からは (j)〜(l) と第443期の表は第443期の盤（`MapStage.Stage12H443`）で回す**（回帰の基準）。
//
// 第444期（§1-2・遊ぶ 1-2 ＝ `MapStage.Stage12`・中間拠点・見返りの増援・刻の上限 14 ＝ 勇者の予告）:
//   (m) 北の砦（N2）に隊が立ち、敵がいなければ味方のものになり、そこで `CanReform` が真・休める
//   (n) そこへ敵部隊が入り、出せる隊がいなければ敵に戻る
//   (o) 敵部隊を抜くと 1 枚・中間拠点を取ると 2 枚届き、届く先はその隊がいる拠点（道の上・迎撃なら味方の拠点）
//   (p) 刻 14 で負け（勇者が到着した）・予告の「あと n 刻」が 1 刻ごとに減る
//   (q) 籠城の方針（拠点で待ち、全隊が 5 枚になってから出る）の踏破は 0%（**予測**——外れても門は落とさない）
//   4 方針 × 盤（第443期の既定・この期の既定・この期から上限を外した盤）× 100 seed の表（報告だけ）
// **第445期からは (m)〜(q) と第444期の表は第444期の盤（`MapStage.Stage12H444`）で回す**（回帰の基準）。
//
// 第445期（§1-2・遊ぶ 1-2 ＝ `MapStage.Stage12`・ゴールド・制圧で勝ち・勇者の予告 12）:
//   (r) 敵部隊を抜くと金が増え、呼ぶと減って手札が 1 枚増え、候補が補充される
//   (s) 候補に盤上・手札の駒が出ない（100 seed・呼ぶたびに）
//   (t) 3 隊目の枠を買うと使える隊が 3 になり、3 隊目を組み直せる（買う前は組み直せず・出せない）
//   (u) 制圧の刻に勝ち（盤上に敵が残っていても）・勝った後は敵が動かない
//   (v) 戦闘の記録の HP の割合が 100% を超えない（分母はいまのロスター）
//   4 方針 × 100 seed（器具の「呼ぶ」は候補の先頭を買う）の表（報告だけ）
// **第446期からは (r)〜(v) と第445期の表は第445期の盤（`MapStage.Stage12H445`）で回す**（回帰の基準）。
//
// 第446期（§1・遊ぶ 1-2 ＝ `MapStage.Stage12`・開始 8・呼ぶ 4・枠 8（無料で 1 枚）・リロール 1・売る ＋2）:
//   (w) 開始の金が 8
//   (x) リロールで候補が入れ替わり、金が 1 減る（捨てた候補は列の最後へ・盤上と手札の駒は出ない）
//   (y) 枠を買うと候補から 1 枚が無料で入り、3 隊目に置ける
//   呼ぶ {3, 4, 5} × 枠 {6, 8} の 6 点 × 3 方針 × 100 seed（器具はリロールしない）の表（報告だけ）
// **第447期からは (w)〜(y) と第446期の表は第446期の盤（`MapStage.Stage12H446`）で回す**（回帰の基準）。
//
// 第447期（§1・遊ぶ 1-2 ＝ `MapStage.Stage12`・呼び戻す 6・リロール 2・隊の枠 4 つ（8 → 12）・「12 作戦ターン以内に制圧」）:
//   (z) 倒れた駒は候補に出ない・「呼び戻す」で HP 50% で手札に戻り金が 6 減る
//   (aa) 4 隊目の枠が 12 で買え、無料の 1 枚が入る
//   (ab) 刻 12 で「時間切れ」（勇者の予告は出ない）
//   3 方針 × 100 seed（器具は穴があるとき、倒れた駒がいれば先に呼び戻す）の表（報告だけ）
//
// 「貪欲」は第438・440期の門と同じ選び方（各隊は盤の上でいちばん近い敵部隊へ・敵が尽きたら敵の拠点へ）。
// 自分で組む通し（到着あり）で回す: 開始の 10 枚を第1隊・第2隊に 5 枚ずつ、届いた駒は拠点にいる第3隊へ。
// =====================================================================================

public static class Map12ChangeSmoke
{
    public const int Seeds = 100;

    public static bool Run(Action<string> write)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        MapStage stage = MapStage.Stage12H441;

        // ---- 保証の表（報告） ----
        {
            var t = MapDraw.Table;
            string N(IEnumerable<string> ids) => string.Join("・", ids.Select(id => MapDraw.Pool.First(d => d.Id == id).Name));
            write($"  保証の表（`Presets.Compare` から機械で引いた）: 回復役 {N(t.Healers)} ／ 前に立てる駒 {N(t.Front)}");
            write($"  芯 {t.Cores.Count} 組: {string.Join("・", t.Cores.Select(c => c.Row))}");
            Req(t.Healers.Count > 0 && t.Front.Count > 0 && t.Cores.Count > 0, "保証の表の 3 列がどれも空でない");
        }

        // ---- (a) 開始の手札 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            var miss = MapDraw.Missing(st.Bench.Select(u => u.Def));
            write($"  seed 0 の開始: 手札 {st.Bench.Count} 枚（{string.Join("・", st.Bench.Select(u => u.Def.Name))}）／ 到着の列 {st.Arrivals.Count} 枚"
                + $"（{string.Join(" → ", st.Arrivals.Select(u => u.Def.Name))}）／ 引き直し {st.OpeningRerolls} 回");
            Req(st.Bench.Count == 10 && st.Arrivals.Count == 5 && miss.Count == 0 && st.OpeningOk,
                "(a) 開始の手札は 10 枚で保証 3 条件を満たし、到着の列は 5 枚");
            int fail = 0, rerollSum = 0, rerollMax = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                MapDraw.Opening o = MapDraw.Draw(seed, stage.Arrival.Start, stage.Arrival.MinPer, stage.Arrival.MaxPer);
                if (!o.Ok) fail++;
                rerollSum += o.Rerolls;
                rerollMax = Math.Max(rerollMax, o.Rerolls);
            }
            write($"  {Seeds} seed の引き: 保証を満たせなかった {fail} seed ／ 引き直し 平均 {(double)rerollSum / Seeds:F2} 回・最大 {rerollMax} 回");
            // 第442期 §0-2: 到着のある盤の「既定」は**拠点にある駒だけ**を配る（第441期は列を配り切っていた）。
            var keep = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            Req(keep.UseDefaultSquads() && keep.Arrivals.Count == 5 && keep.Squads.Sum(s => s.Units?.Count ?? 0) == 10 && keep.Bench.Count == 0,
                "(a') 「既定」は到着の列を残す（開始の 10 枚だけを隊へ配る・第442期 §0-2 で作り替え）");
            var off = new Map11State(0, stage.Time, stage.Portal, draft: false, stage: stage);
            Req(off.Arrivals.Count == 0, "(a'') 既定の 3 隊の通し（draft でない）には到着の列が無い");
        }

        // ---- (b) A 刻ごとに届く ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            Deal(st, 0, 5);
            Deal(st, 1, 5);
            var trail = new List<string>();
            bool regular = true;
            for (int turn = 0; turn < 14 && !st.Finished; turn++)
            {
                int before = st.ArrivedCount;
                st.AdvanceFoes();
                int got = st.ArrivedCount - before;
                if (got > 0) trail.Add($"T{st.Turn}+{got}（{string.Join("・", st.LastArrived.Select(u => u.Def.Name))}）");
                bool due = st.Turn % stage.Arrival.Every == 0 && st.ArrivedCount - got < 5;
                if ((got > 0) != due || got > stage.Arrival.MaxPer || (got > 0 && got < Math.Min(stage.Arrival.MinPer, 5 - (st.ArrivedCount - got))))
                    regular = false;
            }
            write($"  到着: {string.Join(" ／ ", trail)} ／ 届いた総数 {st.ArrivedCount} ／ 手札 {st.Bench.Count} 枚 ／ 列 {st.Arrivals.Count}");
            Req(regular && st.ArrivedCount == 5 && st.Arrivals.Count == 0 && st.Bench.Count == 5,
                $"(b) {stage.Arrival.Every} 刻ごとに 1〜2 枚ずつ手札が増え、5 枚届いたら止まる");
        }

        // ---- (c)(d) 予備隊 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            var g = st.Graph;
            Deal(st, 0, 5);
            Deal(st, 1, 5);
            // 予備隊の経路を空けておく（固定の敵は北の 1 部隊だけ残す＝最初の接敵の相手）。
            foreach (Map11State.Node n in st.Nodes.SelectMany(r => r)) if (!ReferenceEquals(n, st.Nodes[0][0])) n.Cleared = true;
            Dest north = Dest.Deep(g, 0);
            bool waitedBefore = st.ReserveWaiting && st.PortalFoe() is { IsReserve: true };
            int firstBattleTurn = -1, targetAtDecision = -1;
            bool emptyAtDecision = false;
            var trail = new List<string>();
            Map11State.Encounter? arrival = null;
            int stepsOk = 0, stepsBad = 0;
            for (int guard = 0; guard < 20 && arrival is null && !st.Finished; guard++)
            {
                if (Map11Orders.Apply(st, 0, north, Map11Orders.Plan(st, 0, north)) is { } met) Fight(st, 0, met, false);
                if (firstBattleTurn < 0 && st.Battles > 0) firstBattleTurn = st.Turn;
                if (st.ReserveWaiting && st.Battles > 0)
                {
                    targetAtDecision = st.ReserveTarget();
                    emptyAtDecision = !st.Squads.Any(s => s.Units is { } u && u.Any(x => x.IsAlive) && s.Road == targetAtDecision);
                }
                int? from = st.ReserveWaiting ? null : st.Reserve!.Pos;
                var plan = st.PlannedFoeSteps();
                var enc = st.AdvanceFoes();
                Map11State.Node r = st.Reserve!;
                if (from is int f)
                {
                    int want = g.TowardHome(r.Def.Road, f);
                    bool planned = plan.Any(p => ReferenceEquals(p.Node, r));
                    if (planned ? r.Pos == want : r.Pos == f) stepsOk++; else stepsBad++;
                }
                if (!st.ReserveWaiting) trail.Add($"T{st.Turn}:{(r.Pos == g.Home ? "拠点" : r.Pos == g.Portal ? "敵の拠点" : g[r.Pos].Name)}");
                foreach (var e in enc)
                {
                    if (e.Intercept && ReferenceEquals(e.Node, r)) { arrival = e; break; }
                    if (e.Intercept)
                    {
                        while (!st.Finished && st.NextInterceptor(e.Node) is var j && j >= 0) { Fight(st, j, e.Node, true); if (e.Node.Cleared) break; }
                        st.CloseIntercept(e.Node);
                    }
                    else Fight(st, e.Squad, e.Node, false);
                }
            }
            Map11State.Node rv = st.Reserve!;
            write($"  予備隊: 待機（敵の拠点のマスに数える {waitedBefore}）→ 最初の戦闘 T{firstBattleTurn} → 出発 T{st.ReserveDepartTurn}"
                + $"・行き先 {st.LaneName(rv.Def.Road)}（決めた時点でその経路に味方なし {emptyAtDecision}）／ 道のり {string.Join(" → ", trail)}");
            Req(waitedBefore && firstBattleTurn >= 0 && st.ReserveDepartTurn >= firstBattleTurn && st.ReserveDepartTurn <= firstBattleTurn + 1
                && rv.Def.Road == targetAtDecision && emptyAtDecision && rv.Def.Road == 1,
                "(c) 最初の接敵の後、予備隊は「味方のいない経路」（北に第1隊・第2隊は拠点 → 中の道）へ出発する");
            Req(stepsOk >= 3 && stepsBad == 0, $"(c') 出発後は 1 刻 1 マス進む（照合 {stepsOk} 刻・食い違い {stepsBad}）");
            Req(arrival is not null && rv.Pos == g.Home && st.NextInterceptor(rv) >= 0,
                "(d) 予備隊が拠点に着くと迎撃が立つ（拠点の第2隊が受ける）");
        }

        // ---- (e)(f) 100 seed ----
        // 2 つの方針: 「2 隊とも出す」（第440期の貪欲そのまま）と「番を置く」（拠点に出せる隊が 1 つだけなら、その隊は残る）。
        foreach (bool guard in new[] { false, true })
        {
            string label = guard ? "貪欲＋番（拠点に 1 隊残す）" : "貪欲（2 隊とも出す）";
            var runs = new List<Once>();
            int crashed = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                try { runs.Add(Greedy(seed, stage, guard)); }
                catch (Exception ex)
                {
                    crashed++;
                    if (crashed <= 3) write($"  × {label} seed {seed}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            Once s0 = runs.FirstOrDefault(r => r.Seed == 0);
            write($"  {label} seed 0: 踏破 {s0.Won} ／ 陥落 {s0.Fallen} ／ 制圧 T{s0.CapturedTurn} ／ 制圧までに湧いた {s0.SpawnsBeforeCapture} ／ 湧いた計 {s0.Spawns} ／ "
                + $"予備隊 {(s0.ReserveLane < 0 ? "出発せず" : stage.LaneNames[s0.ReserveLane])}・戦闘 {s0.ReserveFought} ／ 届いた {s0.Arrived} 枚 ／ 戦闘 {s0.Battles} ／ 作戦ターン {s0.Turns}");
            // (e) 敵の拠点は拠点から 4 マス奥なので、制圧は早くても 4 刻目——S = 2 の最初の湧き（2 刻目）は必ずその前に来る。
            //     ここでは「制圧した通しはどれも、その前に 1 つ以上湧いていた」を数える（seed 0 だけでは制圧まで行かないことがある）。
            if (guard)
                Req(runs.Any(r => r.Captured) && runs.Where(r => r.Captured).All(r => r.SpawnsBeforeCapture >= 1),
                    $"(e) S = 2 で、制圧した通しはどれも制圧の前に湧きが 1 つ以上あった（{label}・制圧 {runs.Count(r => r.Captured)} 通し）");
            Req(crashed == 0, $"(f) {label}を {Seeds} seed 回して落ちない（数字は報告のみ・線は置かない）");
            if (runs.Count > 0)
            {
                double P(Func<Once, bool> f) => 100.0 * runs.Count(f) / runs.Count;
                var turns = runs.Select(r => r.Turns).OrderBy(x => x).ToArray();
                string lanes = string.Join(" ／ ", Enumerable.Range(-1, stage.LaneCount + 1)
                    .Select(l => $"{(l < 0 ? "出発せず" : stage.LaneNames[l])} {runs.Count(r => r.ReserveLane == l)}"));
                write($"  {label} × {runs.Count} seed: 踏破 {P(r => r.Won):F1}% ／ 陥落 {P(r => r.Fallen):F1}% ／ 制圧 {P(r => r.Captured):F1}% ／ "
                    + $"制圧の前に湧いた {P(r => r.SpawnsBeforeCapture >= 1):F1}% ／ 湧いた数 平均 {runs.Average(r => r.Spawns):F2} ／ 作戦ターン（中央値）{turns[turns.Length / 2]}"
                    + $" ／ 陥落した作戦ターン（中央値）{(runs.Any(r => r.Fallen) ? runs.Where(r => r.Fallen).Select(r => r.Turns).OrderBy(x => x).ElementAt(runs.Count(r => r.Fallen) / 2) : 0)}");
                write($"  予備隊の行き先: {lanes} ／ 予備隊との戦闘が起きた {P(r => r.ReserveFought):F1}%（うち拠点の迎撃 {P(r => r.ReserveAtHome):F1}%）／ "
                    + $"予備隊を倒した {P(r => r.ReserveCleared):F1}%");
                write($"  到着: 届いた枚数 平均 {runs.Average(r => r.Arrived):F2} ／ 第3隊が出撃した {P(r => r.ThirdOut):F1}% ／ 横の辺を通った手 平均 {runs.Average(r => r.Moves):F2}");
            }
        }
        // ---- 報告のみ: 3 つの変更を 1 つずつ入れた 1-2（貪欲＋番 × 100 seed） ----
        {
            MapStage Mix(bool arrival, bool reserve, bool s2) => new()
            {
                Id = stage.Id, Title = stage.Title, Board = stage.Board, Lanes = stage.Lanes, LaneNames = stage.LaneNames,
                Spawn = stage.Spawn, Time = stage.Time,
                Portal = s2 ? stage.Portal : MapStage.Stage12H440.Portal,
                Arrival = arrival ? stage.Arrival : ArrivalRule.Off,
                Reserve = reserve ? stage.Reserve : ReserveRule.Off,
            };
            foreach (var (name, st2) in new[]
                     {
                         ("第440期（3 つとも切る）", Mix(false, false, false)),
                         ("到着だけ", Mix(true, false, false)),
                         ("予備隊だけ", Mix(false, true, false)),
                         ("S=2 だけ", Mix(false, false, true)),
                         ("到着＋予備隊", Mix(true, true, false)),
                         ("第441期（3 つとも）", stage),
                     })
            {
                var runs = Enumerable.Range(0, Seeds).Select(seed => Greedy(seed, st2, guard: true)).ToList();
                double P(Func<Once, bool> f) => 100.0 * runs.Count(f) / runs.Count;
                var turns = runs.Select(r => r.Turns).OrderBy(x => x).ToArray();
                write($"  分解・{name}: 踏破 {P(r => r.Won):F1}% ／ 陥落 {P(r => r.Fallen):F1}% ／ 膠着 {P(r => !r.Won && !r.Fallen):F1}% ／ "
                    + $"湧いた数 {runs.Average(r => r.Spawns):F2} ／ 予備隊との戦闘 {P(r => r.ReserveFought):F1}% ／ 作戦ターン（中央値）{turns[turns.Length / 2]}");
            }
        }
        Phase442(write, Req);
        Phase443(write, Req);
        Phase444(write, Req);
        Phase445(write, Req);
        Phase446(write, Req);
        Phase447(write, Req);
        return ok;
    }

    /// <summary>第442期 §1 —— 遊ぶ 1-2（`MapStage.Stage12`）の (g)(h)(i) と、貪欲 100 seed（報告だけ）。</summary>
    private static void Phase442(Action<string> write, Action<bool, string> Req)
    {
        MapStage stage = MapStage.Stage12H442, old = MapStage.Stage12H441;
        write("  ---- 第442期の 1-2（`Stage12H442`・隊 2 つ・中の道の先頭は 1 マス奥） ----");

        // ---- (g) 既定の組み方 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            int benchBefore = st.Bench.Count;
            bool done = st.UseDefaultSquads();
            string Line(Map11State.Squad q) => $"{q.Label}（{string.Join("・", q.Units!.Select(u => $"{FormationRules.SeatNames[u.Slot]} {u.Def.Name}"))}）";
            write($"  seed 0 の既定の組み方: {string.Join(" ／ ", st.Squads.Select(Line))} ／ 手札 {st.Bench.Count} 枚 ／ 到着の列 {st.Arrivals.Count} 枚");
            int bad = 0, asDefined = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                var t = new Map11State(seed, stage.Time, stage.Portal, draft: true, stage: stage);
                if (!t.UseDefaultSquads() || t.Squads.Length != 2 || t.Squads.Any(q => q.Units?.Count != 5)
                    || t.Bench.Count != 0 || t.Arrivals.Count != 5) bad++;
                asDefined += t.Squads.Count(q => q.Label == q.Def.Name);
            }
            write($"  {Seeds} seed の既定の組み方: 定義どおりの 5 枚がそろって名前が既定に戻った隊 {asDefined} / {2 * Seeds}");
            Req(done && benchBefore == 10 && st.Squads.Length == 2 && st.Squads.All(q => q.Units?.Count == 5)
                && st.Bench.Count == 0 && st.Arrivals.Count == 5 && bad == 0,
                $"(g) 既定の組み方で始めると、開始の手札 10 枚が 2 隊に 5 枚ずつ配られ、到着の列は 5 枚のまま（{Seeds} seed で外れ {bad}）");
        }

        // ---- (h) 隊の数 ----
        {
            var draft = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            var fixedSq = new Map11State(0, stage.Time, stage.Portal, draft: false, stage: stage);
            var one1 = new Map11State(0, MapStage.Stage11.Time, MapStage.Stage11.Portal, draft: true, stage: MapStage.Stage11);
            var o441 = new Map11State(0, old.Time, old.Portal, draft: true, stage: old);
            write($"  隊の数: 1-2（第442期）自分で組む {draft.Squads.Length} ・ 既定 {fixedSq.Squads.Length} ／ 1-1 {one1.Squads.Length} ／ 1-2（第441期）{o441.Squads.Length}");
            Req(stage.SquadCap == 2 && draft.Squads.Length == 2 && fixedSq.Squads.Length == 2 && one1.Squads.Length == 3 && o441.Squads.Length == 3,
                "(h) 1-2 では 3 隊目が作れない（隊は 2 つ・1-1 と第441期の 1-2 は 3 つのまま）");
        }

        // ---- (i) 中の道の先頭が拠点に着く刻 ----
        {
            static int[] Steps(MapGraph g) => Enumerable.Range(0, g.Lanes.Count).Select(l =>
            {
                int pos = g.FoeStarts[l][0], n = 0;
                while (pos != g.Home && n < 20) { pos = g.TowardHome(l, pos); n++; }
                return n;
            }).ToArray();
            int[] now = Steps(stage.Board(true)), before = Steps(old.Board(true));
            // 盤の上でも確かめる: 中の道の 2 部隊だけを残し、隊は拠点（既定の組み方）で、先頭が何刻目に拠点へ入るか。
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            foreach (Map11State.Node n in st.Nodes.SelectMany(r => r)) if (n.Def.Road != 1) n.Cleared = true;
            Map11State.Node lead = st.Nodes[1][0];
            bool shared = lead.Pos == st.Nodes[1][1].Pos;
            int reached = -1;
            for (int k = 0; k < 6 && reached < 0 && !st.Finished; k++)
            {
                st.AdvanceFoes();
                if (lead.Pos == st.Graph.Home) reached = st.Turn;
            }
            write($"  拠点まで（刻）: 第442期 {string.Join(" ／ ", now.Select((x, l) => $"{stage.LaneNames[l]} {x}"))}"
                + $" ／ 第441期 {string.Join(" ／ ", before.Select((x, l) => $"{old.LaneNames[l]} {x}"))}"
                + $" ／ 盤の上で中の先頭が拠点に入った刻 T{reached}（始まりは先遣と同じマス {shared}）");
            Req(now[1] == 3 && now[0] == 2 && now[2] == 2 && before.All(x => x == 2) && reached == 3,
                "(i) 中の道の先頭が拠点に着くのは 3 刻目（北・南は 2 刻目のまま・第441期は 3 本とも 2 刻目）");
        }

        // ---- 報告のみ: 貪欲 100 seed（第441期の 2 つ ＋ 第442期の「分担＋戻り番」） ----
        foreach (var (guard, split) in new[] { (false, false), (true, false), (true, true) })
        {
            string label = split ? "分担＋戻り番（最初は 2 隊で別々の敵へ・接敵の後は拠点が空なら近い隊が戻る）"
                : guard ? "貪欲＋番（拠点に 1 隊残す）" : "貪欲（2 隊とも出す）";
            var runs = new List<Once>();
            int crashed = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                try { runs.Add(Greedy(seed, stage, guard, split)); }
                catch (Exception ex)
                {
                    crashed++;
                    if (crashed <= 3) write($"  × 第442期 {label} seed {seed}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            Req(crashed == 0, $"第442期の 1-2 を{label}で {Seeds} seed 回して落ちない（数字は報告のみ）");
            if (runs.Count == 0) continue;
            double P(Func<Once, bool> f) => 100.0 * runs.Count(f) / runs.Count;
            var turns = runs.Select(r => r.Turns).OrderBy(x => x).ToArray();
            var fallen = runs.Where(r => r.Fallen).Select(r => r.Turns).OrderBy(x => x).ToArray();
            string lanes = string.Join(" ／ ", Enumerable.Range(-1, stage.LaneCount + 1)
                .Select(l => $"{(l < 0 ? "出発せず" : stage.LaneNames[l])} {runs.Count(r => r.ReserveLane == l)}"));
            write($"  第442期 {label} × {runs.Count} seed: 踏破 {P(r => r.Won):F1}% ／ 陥落 {P(r => r.Fallen):F1}% ／ 膠着 {P(r => !r.Won && !r.Fallen):F1}% ／ "
                + $"制圧 {P(r => r.Captured):F1}% ／ 湧いた数 平均 {runs.Average(r => r.Spawns):F2} ／ 作戦ターン（中央値）{turns[turns.Length / 2]}"
                + $" ／ 陥落した作戦ターン（中央値）{(fallen.Length > 0 ? fallen[fallen.Length / 2] : 0)}");
            write($"    予備隊の行き先: {lanes} ／ 予備隊との戦闘 {P(r => r.ReserveFought):F1}%（うち拠点の迎撃 {P(r => r.ReserveAtHome):F1}%）／ "
                + $"届いた枚数 平均 {runs.Average(r => r.Arrived):F2} ／ 届いた駒を隊へ入れた 平均 {runs.Average(r => r.Refilled):F2} 枚 ／ 横の辺を通った手 平均 {runs.Average(r => r.Moves):F2}");
        }
    }

    /// <summary>第443期 §1-3 —— 遊ぶ 1-2（`MapStage.Stage12`）の (j)(k)(l) と、5 つの盤 × 3 方針（報告だけ）。</summary>
    private static void Phase443(Action<string> write, Action<bool, string> Req)
    {
        MapStage stage = MapStage.Stage12H443;
        write("  ---- 第443期の 1-2（`Stage12H443`・隊 3 つ・到着 2 刻ごと 2 枚・S=3・予備隊は第二波・先遣） ----");

        // ---- (j) 迎撃の合間（作戦ターンの流れ `Map11Session` で、画面を通さずに回す） ----
        {
            Map11Session.Reset(0, draft: true, stage: stage);
            Map11State st = Map11Session.State!;
            st.UseDefaultSquads();
            // 北と南の先頭だけを残す（どちらも 2 刻で拠点に着く）。隊はどれも拠点で待つ（行き先は拠点のまま）。
            foreach (Map11State.Node n in st.Nodes.SelectMany(r => r))
                if (!(n.Def.Index == 0 && n.Def.Road != 1)) n.Cleared = true;
            int fought = 0, turnOfFirst = -1, turnOfSecond = -1;
            bool breakBefore = false, breakAfter = false, reformAfter = false, swapped = false, swappedFought = false;
            string? swappedName = null;
            for (int guard = 0; guard < 8 && !st.Finished && fought < 2; guard++)
            {
                Map11Session.BeginTurn();
                while (!st.Finished)
                {
                    Map11Session.StepKind k = Map11Session.StepTurn();
                    if (k != Map11Session.StepKind.Intercept) break;
                    Map11State.Node node = Map11Session.InterceptNode!;
                    int pick = st.NextInterceptor(node);
                    if (fought == 0) { breakBefore = Map11Session.InterceptBreak; turnOfFirst = st.Turn; }
                    else if (fought == 1)
                    {
                        turnOfSecond = st.Turn;
                        breakAfter = Map11Session.InterceptBreak;
                        reformAfter = st.CanReform(pick);
                        int b = st.Bench.FindIndex(x => x.IsAlive);
                        int slot = FormationRules.PlayableSlots.FirstOrDefault(sl => st.UnitAt(pick, sl) is not { IsAlive: true },
                                                                               FormationRules.PlayableSlots[0]);
                        if (b >= 0)
                        {
                            UnitState fresh = st.Bench[b];
                            swapped = st.SwapWithBench(pick, slot, b);
                            swappedName = fresh.Def.Name;
                            swappedFought = st.Squads[pick].Units!.Contains(fresh);
                        }
                    }
                    if (!Map11Session.ChooseInterceptor(pick)) break;
                    BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!,
                                                      CampaignSession.CarriedSeed, verbose: false);
                    Map11Session.CompleteBattle(r.PlayerWon, r);
                    fought++;
                }
            }
            write($"  迎撃: 1 戦目 T{turnOfFirst}（その前は合間 {breakBefore}）→ 2 戦目 T{turnOfSecond}（合間 {breakAfter}・組み直せる {reformAfter}・"
                + $"手札から入れた {swappedName ?? "なし"} {swapped}・2 戦目の隊に入っている {swappedFought}）");
            Req(fought >= 2 && turnOfFirst == turnOfSecond && !breakBefore && breakAfter && reformAfter && swapped && swappedFought,
                "(j) 同じ刻に 2 部隊が拠点に着くと、1 戦目の後は迎撃の合間で、拠点の隊を組み直せ、2 戦目の前に手札から入れ替えられる（1 戦目の前は合間ではない）");
            Map11Session.Reset(0, draft: true, stage: MapStage.Stage11);
        }

        // ---- (k) 3 隊目は届いた駒で組む ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            int[] start = st.Squads.Select(q => q.Units?.Count ?? 0).ToArray();
            bool thirdEmpty = start.Length == 3 && start[2] == 0 && !st.CanSend(2);
            foreach (Map11State.Node n in st.Nodes.SelectMany(r => r)) n.Cleared = true;
            var trail = new List<string>();
            for (int t = 0; t < 6 && !st.Finished; t++)
            {
                int before = st.ArrivedCount;
                st.AdvanceFoes();
                if (st.ArrivedCount > before) trail.Add($"T{st.Turn}+{st.ArrivedCount - before}");
            }
            bool can = st.CanReform(2);
            Deal(st, 2, st.Bench.Count);
            write($"  3 隊目: 始め {string.Join(" ／ ", start.Select((c, i) => $"{st.Squads[i].Label} {c} 枚"))}（{st.Squads[2].Role}）・到着 {string.Join(" ", trail)}"
                + $" → 3 隊目 {st.Squads[2].Units?.Count ?? 0} 枚・出せる {st.CanSend(2)}");
            Req(thirdEmpty && start[0] == 5 && start[1] == 5 && st.ArrivedCount == 5 && can
                && st.Squads[2].Units?.Count == 5 && st.CanSend(2),
                "(k) 3 隊目は空で始まり（出せない）、刻 6 までに届いた 5 枚で組める");
        }

        // ---- (l) 既定の値 ----
        {
            write($"  既定: 隊 {stage.SquadCap} ／ 開始 {stage.Arrival.Start} 枚 ／ 到着 {stage.Arrival.Every} 刻ごと {stage.Arrival.MinPer}〜{stage.Arrival.MaxPer} 枚"
                + $" ／ 湧き S={stage.Portal.SpawnEvery} R={stage.Portal.WarpEvery} ／ 予備隊 {stage.Reserve.Name}（{stage.Reserve.Enemy?.Occupied().Count()} 体）");
            Req(stage.SquadCap == 3 && stage.Arrival.Start == 10 && stage.Arrival.Every == 2 && stage.Arrival.MinPer == 2 && stage.Arrival.MaxPer == 2
                && stage.Portal.SpawnEvery == 3 && stage.Portal.WarpEvery == 3 && stage.Reserve.Name == EnemyCatalog.Vanguards[0].Name
                && stage.Board == MapStage.Stage12H442.Board && stage.Lanes == MapStage.Stage12H442.Lanes,
                "(l) 既定は 隊 3・到着 2 刻ごと 2 枚・S=3・予備隊は第二波・先遣（盤と最初からいる 6 部隊は第442期のまま）");
        }

        // ---- 報告のみ: 5 つの盤 × 3 方針 × 100 seed ----
        MapStage Variant(MapStage b, PortalRule? portal = null, ReserveRule? reserve = null, int? cap = null) => new()
        {
            Id = b.Id, Title = b.Title, Board = b.Board, Lanes = b.Lanes, LaneNames = b.LaneNames, Spawn = b.Spawn, Time = b.Time,
            Portal = portal ?? b.Portal, Arrival = b.Arrival, Reserve = reserve ?? b.Reserve, SquadCap = cap ?? b.SquadCap,
        };
        var boards = new[]
        {
            ("(A) 第442期のまま", MapStage.Stage12H442),
            ("(B) 既定", stage),
            ("(C) 既定から S=2", Variant(stage, portal: MapStage.Stage12H442.Portal)),
            ("(D) 既定から予備隊を第三波", Variant(stage, reserve: MapStage.Stage12H442.Reserve)),
            ("(E) 既定から隊 2", Variant(stage, cap: 2)),
        };
        var policies = new[]
        {
            ("素直に出す", false, false),
            ("1 隊残す", true, false),
            ("分担＋戻り番", true, true),
        };
        write("");
        write("  | 盤 | 方針 | 踏破 | 陥落 | 膠着 | 制圧 | 拠点で 2 連戦 | 2 連戦目で全滅 | 湧いた数 | 予備隊との戦闘 | 届いた枚数 | 3 隊目が出た | 作戦ターン（中央値） |");
        write("  |---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        int crashed = 0;
        foreach (var (bname, board) in boards)
            foreach (var (pname, guard, split) in policies)
            {
                var runs = new List<Once>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    try { runs.Add(Greedy(seed, board, guard, split)); }
                    catch (Exception ex)
                    {
                        crashed++;
                        if (crashed <= 3) write($"  × 第443期 {bname} {pname} seed {seed}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                if (runs.Count == 0) continue;
                double P(Func<Once, bool> f) => 100.0 * runs.Count(f) / runs.Count;
                var turns = runs.Select(r => r.Turns).OrderBy(x => x).ToArray();
                write($"  | {bname} | {pname} | {P(r => r.Won):F0} | {P(r => r.Fallen):F0} | {P(r => !r.Won && !r.Fallen):F0} | {P(r => r.Captured):F0} | "
                    + $"{P(r => r.DoubleAtHome):F0} | {P(r => r.WipedOnSecond):F0} | {runs.Average(r => r.Spawns):F2} | {P(r => r.ReserveFought):F0} | "
                    + $"{runs.Average(r => r.Arrived):F2} | {(board.SquadCap > 2 ? P(r => r.ThirdOut).ToString("F0") : "—")} | {turns[turns.Length / 2]} |");
            }
        Req(crashed == 0, $"第443期の 5 つの盤 × 3 方針を {Seeds} seed ずつ回して落ちない（数字は報告のみ・線は置かない）");
    }

    /// <summary>第444期 §1-2 —— 遊ぶ 1-2（`MapStage.Stage12`）の (m)〜(q) と、4 方針の表（報告だけ）。</summary>
    private static void Phase444(Action<string> write, Action<bool, string> Req)
    {
        MapStage stage = MapStage.Stage12H444;
        write("  ---- 第444期の 1-2（`Stage12H444`・中間拠点・見返りの増援・刻の上限 14 ＝ 勇者の予告） ----");
        string Names(IEnumerable<Map11State.Delivery> ds) => string.Join("・", ds.Select(d => $"{d.Unit.Def.Name}（{d.Place}・{d.Reason}）"));

        // ---- (m)(n) 北の砦を取る・取り返される ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            var g = st.Graph;
            int fort = g.Lanes[0][^1];
            foreach (Map11State.Node n in st.Nodes[0]) n.Cleared = true;   // 北の道の敵を空けて歩いて入る
            Dest to = Dest.Of(g, fort);
            for (int k = 0; k < 6 && st.Squads[0].Pos != fort; k++) Map11Orders.Apply(st, 0, to, Map11Orders.Plan(st, 0, to));
            bool held = st.HeldOutposts.Contains(fort), reform = st.CanReform(0);
            int got = st.Deliveries.Count;
            st.Squads[0].Units![0].Hp = 1;                                 // 傷を付けて休めるか見る
            bool rest = st.Rest(0);
            write($"  (m) 第1隊が {g[fort].Name} に立つ: 味方 {held} ・ 組み直せる {reform} ・ 休めた {rest} ・ 届いた {Names(st.Deliveries)}");
            Req(g.IsOutpost(fort) && g[fort].Name == "北の砦" && held && reform && rest && got == 2
                && st.Deliveries.All(d => d.Place == "北の砦" && d.Reason == "北の砦を取った"),
                "(m) 北の砦（N2）に隊が立ち敵がいなければ味方のものになり、そこで組み直せ・休め、見返りの 2 枚が北の砦に届く");

            // 隊を拠点へ戻し（砦は味方のまま）、北の道の 2 部隊目を敵の拠点から歩かせて砦へ入れる。
            for (int k = 0; k < 6 && st.Squads[0].Pos != g.Home; k++) Map11Orders.Apply(st, 0, Dest.Home, Map11Orders.Plan(st, 0, Dest.Home));
            bool stillHeld = st.HeldOutposts.Contains(fort);
            Map11State.Node foe = st.Nodes[0][1];
            foe.Cleared = false;
            foe.Pos = g.Portal;
            st.AdvanceFoes();
            bool lost = foe.Pos == fort && !st.HeldOutposts.Contains(fort) && st.OutpostLosses == 1;
            write($"  (n) 隊が離れても砦は味方のまま {stillHeld} → {foe.Def.Name} が {g[foe.Pos].Name} に入った: 取り返された {lost}");
            Req(stillHeld && lost, "(n) 隊のいない中間拠点に敵部隊が入ると敵のものに戻る（隊が離れただけでは戻らない）");
        }

        // ---- (o) 見返り: 抜くと 1 枚・取ると 2 枚・届く先はその隊の拠点 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            var g = st.Graph;
            int fort = g.Lanes[0][^1];
            // 戦闘は回さず、相手を倒した形で精算する（どの seed でも同じになる）。
            void Win(int squad, Map11State.Node node, bool intercept)
            {
                var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
                foreach (UnitState u in prep!.Value.Enemies) u.Hp = 0;
                st.Resolve(squad, node, true, intercept);
            }
            Dest deep = Dest.Of(g, fort);
            var trail = new List<string>();
            for (int k = 0; k < 8 && !st.HeldOutposts.Contains(fort); k++)
            {
                int before = st.Deliveries.Count;
                if (Map11Orders.Apply(st, 0, deep, Map11Orders.Plan(st, 0, deep)) is { } met) Win(0, met, false);
                if (st.Deliveries.Count > before) trail.Add(Names(st.Deliveries.Skip(before)));
            }
            var road = st.Deliveries.ToList();
            // 拠点の迎撃で抜く: 南の道の先頭を拠点まで歩かせて第2隊で受ける。
            Map11State.Node south = st.Nodes[2][0];
            south.Pos = g.Home;
            int b0 = st.Deliveries.Count;
            Win(1, south, true);
            var home = st.Deliveries.Skip(b0).ToList();
            write($"  (o) 北の道を進む: {string.Join(" → ", trail)} ／ 拠点の迎撃で抜く: {Names(home)} ／ 列の残り {st.Arrivals.Count} 枚");
            bool killOnRoad = road.Count >= 1 && road[0].Place == "拠点" && road[0].Reason.EndsWith("を抜いた");
            bool fortTwo = road.Count(d => d.Reason == "北の砦を取った" && d.Place == "北の砦") == 2;
            bool killAtFort = road.Any(d => d.Place == "北の砦" && d.Reason.EndsWith("を抜いた"));
            Req(killOnRoad && fortTwo && killAtFort && road.Count == 4 && home.Count == 1 && home[0].Place == "拠点" && st.Arrivals.Count == 0,
                "(o) 敵部隊を抜くと 1 枚（道の上なら拠点に・取った中間拠点で抜けばそこに）・中間拠点を取ると 2 枚・迎撃なら拠点に。5 枚で尽きる");
        }

        // ---- (p) 刻 14 で勇者が着いて負け ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            foreach (Map11State.Node n in st.Nodes.SelectMany(r => r)) n.Cleared = true;
            var left = new List<int> { st.HeroTurnsLeft ?? -1 };
            for (int k = 0; k < 20 && !st.Finished; k++) { st.AdvanceFoes(); left.Add(st.HeroTurnsLeft ?? -1); }
            bool steady = left.Zip(left.Skip(1), (a, b) => a - b).All(d => d == 1);
            var one1 = new Map11State(0, MapStage.Stage11.Time, MapStage.Stage11.Portal, draft: true, stage: MapStage.Stage11);
            write($"  (p) 勇者の予告: {string.Join(" → ", left)} ／ 刻 {st.Turn} で終わり・勇者が到着 {st.HeroArrived}・陥落 {st.Fallen}・踏破 {st.Won} ／ 1-1 の予告 {(one1.HeroTurnsLeft is null ? "なし" : "あり")}");
            Req(stage.TurnCap == 14 && stage.HeroAtCap && left[0] == 14 && steady && st.Turn == 14 && st.Finished && st.HeroArrived && !st.Fallen && !st.Won
                && one1.HeroTurnsLeft is null && !MapStage.Stage12H443.HeroAtCap && MapStage.Stage12H443.TurnCap == Map11.TurnCap,
                "(p) 刻 14 で勇者が着いて負け・予告の「あと n 刻」は 14 から 1 刻ごとに減る（1-1 と第443期の盤には予告も上限 14 も無い）");
        }

        // ---- 報告のみ: 4 方針 × 盤 × 100 seed（(q) 籠城の予測を含む） ----
        MapStage NoCap(MapStage b) => new()
        {
            Id = b.Id, Title = b.Title, Board = b.Board, Lanes = b.Lanes, LaneNames = b.LaneNames, Spawn = b.Spawn, Time = b.Time,
            Portal = b.Portal, Arrival = b.Arrival, Reserve = b.Reserve, SquadCap = b.SquadCap,
        };
        var boards = new[]
        {
            ("第443期の既定", MapStage.Stage12H443),
            ("第444期の既定（上限 14）", stage),
            ("第444期から上限を外す（60）", NoCap(stage)),
        };
        var policies = new[]
        {
            ("1 隊残す", true, false, false),
            ("分担＋戻り番", true, true, false),
            ("籠城", true, true, true),
            ("素直に出す", false, false, false),
        };
        write("");
        write("  | 盤 | 方針 | 踏破 | 陥落 | 時間切れ | ほか | 制圧の刻（中央値・最小〜最大） | 刻 14 までに制圧 | 中間拠点を取った（平均） | 届いた枚数 | 籠城をやめて出た |");
        write("  |---|---|--:|--:|--:|--:|---|--:|--:|--:|--:|");
        int crashed = 0;
        double turtleWon = -1;
        foreach (var (bname, board) in boards)
            foreach (var (pname, guard, split, turtle) in policies)
            {
                var runs = new List<Once>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    try { runs.Add(Greedy(seed, board, guard, split, turtle)); }
                    catch (Exception ex)
                    {
                        crashed++;
                        if (crashed <= 3) write($"  × 第444期 {bname} {pname} seed {seed}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                if (runs.Count == 0) continue;
                double P(Func<Once, bool> f) => 100.0 * runs.Count(f) / runs.Count;
                var cap = runs.Where(r => r.Captured).Select(r => r.CapturedTurn).OrderBy(x => x).ToArray();
                string capText = cap.Length == 0 ? "—" : $"{cap[cap.Length / 2]}（{cap[0]}〜{cap[^1]}）";
                if (board == stage && turtle) turtleWon = P(r => r.Won);
                write($"  | {bname} | {pname} | {P(r => r.Won):F0} | {P(r => r.Fallen):F0} | {P(r => r.TimedOut):F0} | {P(r => !r.Won && !r.Fallen && !r.TimedOut):F0} | "
                    + $"{capText} | {P(r => r.Captured && r.CapturedTurn <= 14):F0} | {runs.Average(r => r.Outposts):F2} | {runs.Average(r => r.Arrived):F2} | "
                    + $"{(turtle ? P(r => r.Released).ToString("F0") : "—")} |");
            }
        Req(crashed == 0, $"第444期の 4 方針 × 3 つの盤を {Seeds} seed ずつ回して落ちない（数字は報告のみ・線は置かない）");
        write($"{(turtleWon == 0 ? "○" : "△")} (q) 予測「籠城の方針は第444期の既定で踏破 0%」→ 実測 {turtleWon:F0}%（予測なので門は落とさない）");
    }

    /// <summary>第445期 §1-2 —— 遊ぶ 1-2（`MapStage.Stage12`）の (r)〜(v) と、4 方針の表（報告だけ）。</summary>
    private static void Phase445(Action<string> write, Action<bool, string> Req)
    {
        MapStage stage = MapStage.Stage12H445;
        GoldRule gr = stage.Gold;
        write("  ---- 第445期の 1-2（`Stage12H445`・ゴールド・制圧で勝ち・勇者の予告 12） ----");
        void Win(Map11State st, int squad, Map11State.Node node, bool intercept)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Enemies) u.Hp = 0;
            st.Resolve(squad, node, true, intercept);
        }
        bool Clean(Map11State st) => st.Offers.All(o => !st.OnBoardOrHand(o)) && st.Offers.Select(o => o.Id).Distinct().Count() == st.Offers.Count;

        // ---- (r) 抜くと金・呼ぶと減る・候補の補充 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            int g0 = st.Gold, bench0 = st.Bench.Count;
            Map11State.Node lead = st.Nodes[0][0];
            Dest north = Dest.Of(st.Graph, lead.Pos);
            for (int k = 0; k < 4 && !lead.Cleared; k++)
                if (Map11Orders.Apply(st, 0, north, Map11Orders.Plan(st, 0, north)) is { } met) Win(st, 0, met, false);
            int afterKill = st.Gold;
            // 湧いた部隊は安い: 敵の拠点から 1 部隊湧かせて拠点の迎撃で抜く（湧くまで刻を進める）。
            // 予備隊も出発すると湧いた部隊の列に入るが、抜けば ＋6（湧いた部隊ではない）——湧いた部隊を選ぶ。
            for (int k = 0; k < 12 && !st.Spawns.Any(n => !n.IsReserve) && !st.Finished; k++) st.AdvanceFoes();
            int beforeSpawnKill = st.Gold, spawnGain = -1;
            if (!st.Spawns.Any(n => !n.IsReserve))
                write($"  (r) 湧かなかった: 刻 {st.Turn}・陥落 {st.Fallen}・終わり {st.Finished}・湧くまで {st.PortalCooldown}・湧いた数 {st.SpawnCount}");
            else
            {
                Map11State.Node sp = st.Spawns.First(n => !n.IsReserve);
                sp.Pos = st.Graph.Home;
                Win(st, 1, sp, true);
                spawnGain = st.Gold - beforeSpawnKill;
            }
            // 金を足して呼ぶ（器具の都合で稼ぎを早める——敵部隊をもう 1 つ抜く）。
            Map11State.Node south = st.Nodes[2][0];
            south.Pos = st.Graph.Home;
            Win(st, 1, south, true);
            var offers0 = st.Offers.Select(o => o.Name).ToList();
            string first = offers0[0];
            int gBuy = st.Gold;
            bool bought = st.Recruit(0);
            var offers1 = st.Offers.Select(o => o.Name).ToList();
            write($"  (r) 金: 開始 {g0} → 北の先頭を抜いて {afterKill}（＋{afterKill - g0}）・湧いた部隊を抜いて ＋{spawnGain} ・ もう 1 つ抜いて {gBuy}"
                + $" → 呼ぶ（{first}）{bought} → {st.Gold} ／ 手札 {bench0} → {st.Bench.Count} ／ 候補 {string.Join("・", offers0)} → {string.Join("・", offers1)}");
            Req(g0 == gr.Start && afterKill - g0 == gr.Kill && spawnGain == gr.SpawnKill && bought && st.Gold == gBuy - gr.Recruit
                && st.Bench.Count == bench0 + 1 && st.Bench[^1].Def.Name == first && offers1.Count == gr.Offers
                && offers1[1] == offers0[1] && offers1[2] == offers0[2] && offers1[0] != first,
                $"(r) 抜くと ＋{gr.Kill}（湧いた部隊 ＋{gr.SpawnKill}）・呼ぶと −{gr.Recruit} で手札が 1 枚増え、買った位置に列の次の 1 枚が入る");
        }

        // ---- (s) 候補に盤上・手札の駒が出ない ----
        {
            int bad = 0, buys = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                var st = new Map11State(seed, stage.Time, stage.Portal, draft: true, stage: stage);
                st.UseDefaultSquads();
                if (!Clean(st)) bad++;
                // 金を足して何度も呼ぶ（器具の都合・敵部隊を抜いた形で金を増やす）。
                foreach (Map11State.Node n in st.Nodes.SelectMany(r => r).Take(6))
                {
                    if (n.Cleared) continue;
                    n.Pos = st.Graph.Home;
                    Win(st, 0, n, true);
                    while (st.Recruit(0)) { buys++; if (!Clean(st)) bad++; }
                }
            }
            write($"  (s) {Seeds} seed × 呼べるだけ呼ぶ（計 {buys} 回）: 候補に盤上・手札の駒・重複が出た {bad} 回");
            Req(bad == 0 && buys > Seeds, "(s) 候補に盤上・手札にいる駒は出ない（同じキャラは盤上に 1 体まで）・候補の中にも重複が無い");
        }

        // ---- (t) 3 隊目の枠 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            bool lockedBefore = st.IsLocked(2) && !st.CanReform(2) && !st.CanSend(2) && st.SquadCap == gr.StartSquads;
            foreach (Map11State.Node n in st.Nodes.SelectMany(r => r).Take(2)) { n.Pos = st.Graph.Home; Win(st, 0, n, true); }
            int g1 = st.Gold;
            bool bought = st.BuySlot();
            bool open = st.SquadCap == 3 && !st.IsLocked(2) && st.CanReform(2) && st.Gold == g1 - gr.Slot;
            write($"  (t) 枠: 買う前 隊 {gr.StartSquads}・第3隊は組み直せず出せない {lockedBefore} → 金 {g1} で買う {bought} → 隊 {st.SquadCap}・第3隊を組み直せる {st.CanReform(2)}・金 {st.Gold}");
            Req(lockedBefore && bought && open && !st.CanBuySlot, $"(t) 3 隊目の枠を {gr.Slot} で買うと使える隊が 3 になり、3 隊目を組み直せる（買う前は組み直せず・出せない）");
        }

        // ---- (u) 制圧の刻に勝ち ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            // 中の道の 2 部隊を倒した形にして、第1隊を敵の拠点へ歩かせる（北と南の敵は残す）。
            foreach (Map11State.Node n in st.Nodes[1]) n.Cleared = true;
            Dest portal = Dest.Portal(st.Graph, 1);
            int steps = 0;
            for (; steps < 8 && !st.Captured; steps++)
                if (Map11Orders.Apply(st, 0, portal, Map11Orders.Plan(st, 0, portal)) is { } met) Win(st, 0, met, false);
            int left = st.FoesRemaining, turn = st.Turn;
            st.AdvanceFoes();
            bool frozen = st.Turn == turn && st.Won;
            var old = new Map11State(0, MapStage.Stage12H444.Time, MapStage.Stage12H444.Portal, draft: true, stage: MapStage.Stage12H444);
            write($"  (u) 第1隊が中の道から敵の拠点へ: 制圧 {st.Captured}・勝ち {st.Won}・終わり {st.Finished}・盤に残った敵部隊 {left}・勝った後に刻を進めても動かない {frozen}"
                + $" ／ 第444期の盤は制圧で勝ちにしない {!MapStage.Stage12H444.WinOnCapture}");
            Req(st.Captured && st.Won && st.Finished && left > 0 && frozen && !MapStage.Stage12H444.WinOnCapture && !MapStage.Stage11.WinOnCapture,
                "(u) 制圧の刻に勝ち（盤上に敵部隊が残っていても・残りは退く）・勝った後は敵が動かない（1-1 と第444期までの盤は今のまま）");
        }

        // ---- (v) 記録の HP の割合 ----
        {
            int over = 0, battles = 0;
            double max = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                Map11Session.Reset(seed, draft: true, stage: stage);
                Map11State st = Map11Session.State!;
                st.UseDefaultSquads();
                for (int i = 0; i < 2; i++) Map11Session.SetDestination(i, Dest.Deep(st.Graph, i == 0 ? 0 : 2));
                for (int guard = 0; guard < 6 && !st.Finished; guard++)
                {
                    Map11Session.BeginTurn();
                    while (!st.Finished)
                    {
                        Map11Session.StepKind k = Map11Session.StepTurn();
                        if (k == Map11Session.StepKind.Intercept && !Map11Session.ChooseInterceptor(st.NextInterceptor(Map11Session.InterceptNode!))) break;
                        if (k != Map11Session.StepKind.Battle && k != Map11Session.StepKind.Intercept) break;
                        BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!,
                                                          CampaignSession.CarriedSeed, verbose: false);
                        Map11Session.CompleteBattle(r.PlayerWon, r);
                        battles++;
                        double pct = Map11Session.BattleLog[^1].HpPercent;
                        max = Math.Max(max, pct);
                        if (pct > 100.0 + 1e-9) over++;
                    }
                }
            }
            Map11Session.Reset(0, draft: true, stage: MapStage.Stage11);
            write($"  (v) 20 seed の通しの戦闘 {battles} 回: 記録の HP の割合の最大 {max:F1}% ・ 100% を超えた {over} 回");
            Req(battles > 20 && over == 0, "(v) 戦闘の記録の HP の割合は 100% を超えない（分母はいまのロスターの最大 HP）");
        }

        // ---- 報告のみ: 4 方針 × 100 seed ----
        MapStage NoCap(MapStage b) => new()
        {
            Id = b.Id, Title = b.Title, Board = b.Board, Lanes = b.Lanes, LaneNames = b.LaneNames, Spawn = b.Spawn, Time = b.Time,
            Portal = b.Portal, Arrival = b.Arrival, Reserve = b.Reserve, SquadCap = b.SquadCap, WinOnCapture = b.WinOnCapture, Gold = b.Gold,
        };
        var boards = new[]
        {
            ("第444期の既定", MapStage.Stage12H444),
            ("第445期の既定（上限 12）", stage),
            ("第445期から上限を外す（60）", NoCap(stage)),
        };
        var policies = new[]
        {
            ("1 隊残す", true, false, false),
            ("分担＋戻り番", true, true, false),
            ("籠城", true, true, true),
            ("素直に出す", false, false, false),
        };
        write("");
        write("  | 盤 | 方針 | 踏破 | 陥落 | 時間切れ | ほか | 制圧の刻（中央値・最小〜最大） | 呼んだ枚数（平均・最大） | 3 隊目の枠を買った | 金の余り（中央値・最大） | 中間拠点を取った（平均） |");
        write("  |---|---|--:|--:|--:|--:|---|---|--:|---|--:|");
        int crashed = 0;
        foreach (var (bname, board) in boards)
            foreach (var (pname, guard, split, turtle) in policies)
            {
                var runs = new List<Once>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    try { runs.Add(Greedy(seed, board, guard, split, turtle)); }
                    catch (Exception ex)
                    {
                        crashed++;
                        if (crashed <= 3) write($"  × 第445期 {bname} {pname} seed {seed}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                if (runs.Count == 0) continue;
                double P(Func<Once, bool> f) => 100.0 * runs.Count(f) / runs.Count;
                var cap = runs.Where(r => r.Captured).Select(r => r.CapturedTurn).OrderBy(x => x).ToArray();
                string capText = cap.Length == 0 ? "—" : $"{cap[cap.Length / 2]}（{cap[0]}〜{cap[^1]}）";
                var gold = runs.Select(r => r.GoldLeft).OrderBy(x => x).ToArray();
                bool hasGold = board.Gold.On;
                write($"  | {bname} | {pname} | {P(r => r.Won):F0} | {P(r => r.Fallen):F0} | {P(r => r.TimedOut):F0} | {P(r => !r.Won && !r.Fallen && !r.TimedOut):F0} | "
                    + $"{capText} | {(hasGold ? $"{runs.Average(r => r.Recruits):F2}・{runs.Max(r => r.Recruits)}" : "—")} | "
                    + $"{(hasGold ? P(r => r.SlotBought).ToString("F0") : "—")} | {(hasGold ? $"{gold[gold.Length / 2]}・{gold[^1]}" : "—")} | {runs.Average(r => r.Outposts):F2} |");
            }
        Req(crashed == 0, $"第445期の 4 方針 × 3 つの盤を {Seeds} seed ずつ回して落ちない（数字は報告のみ・線は置かない）");
    }

    /// <summary>第446期 —— 遊ぶ 1-2（`MapStage.Stage12`）の (w)(x)(y) と、値の 6 点の表（報告だけ）。</summary>
    private static void Phase446(Action<string> write, Action<bool, string> Req)
    {
        MapStage stage = MapStage.Stage12H446;
        GoldRule gr = stage.Gold;
        write("  ---- 第446期の 1-2（`Stage12H446`・開始 8・呼ぶ 4・枠 8（無料で 1 枚）・リロール 1・売る ＋2） ----");
        void Win(Map11State st, int squad, Map11State.Node node, bool intercept)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Enemies) u.Hp = 0;
            st.Resolve(squad, node, true, intercept);
        }

        // ---- (w) 開始の金 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            var old = new Map11State(0, MapStage.Stage12H445.Time, MapStage.Stage12H445.Portal, draft: true, stage: MapStage.Stage12H445);
            write($"  (w) 開始の金 {st.Gold}（第445期の盤 {old.Gold}）・値 呼ぶ {gr.Recruit} ／ 枠 {gr.Slot} ／ リロール {gr.Reroll} ／ 売る ＋{gr.Sell} ／ 無料の 1 枚 {gr.SlotFreePick}");
            Req(st.Gold == 8 && old.Gold == 0 && gr.Recruit == 4 && gr.Slot == 8 && gr.Reroll == 1 && gr.Sell == 2 && gr.SlotFreePick
                && gr.Kill == 6 && gr.SpawnKill == 3 && gr.Outpost == 8,
                "(w) 開始の金は 8（第445期の盤は 0 のまま）・呼ぶ 4・枠 8・リロール 1・売る ＋2・稼ぎは変えない");
        }

        // ---- (x) リロール ----
        {
            int bad = 0, changed = 0, rolls = 0;
            string sample = "";
            for (int seed = 0; seed < Seeds; seed++)
            {
                var st = new Map11State(seed, stage.Time, stage.Portal, draft: true, stage: stage);
                st.UseDefaultSquads();
                for (int k = 0; k < 6; k++)
                {
                    var before = st.Offers.Select(o => o.Id).ToList();
                    int g0 = st.Gold;
                    if (!st.Reroll()) break;
                    rolls++;
                    var after = st.Offers.Select(o => o.Id).ToList();
                    if (after.Intersect(before).Any() || after.Count != gr.Offers || st.Gold != g0 - gr.Reroll) bad++;
                    else changed++;
                    if (st.Offers.Any(o => st.OnBoardOrHand(o))) bad++;
                    if (seed == 0 && k == 0) sample = $"{string.Join("・", before.Select(id => st.Offers.Concat(UnitCatalog.All).First(d => d.Id == id).Name))} → {string.Join("・", st.Offers.Select(o => o.Name))}";
                }
            }
            write($"  (x) リロール {rolls} 回（100 seed × 金が尽きるまで最大 6 回）: 3 枚とも入れ替わり金が {gr.Reroll} 減った {changed} 回・外れ {bad} 回 ／ seed 0 の 1 回目 {sample}");
            Req(bad == 0 && changed == rolls && rolls >= Seeds * 6,
                "(x) リロールで候補 3 枚が入れ替わり（前の 3 枚は出ない）、金が 1 減る・盤上と手札の駒は出ない");
        }

        // ---- (y) 枠と無料の 1 枚 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            Map11State.Node lead = st.Nodes[2][0];
            lead.Pos = st.Graph.Home;
            Win(st, 0, lead, true);                                         // 8 ＋ 6 ＝ 14
            int g0 = st.Gold, bench0 = st.Bench.Count;
            bool bought = st.BuySlot();
            int g1 = st.Gold, free1 = st.FreePicks;
            string name = st.Offers[1].Name;
            bool picked = st.Recruit(1);
            int g2 = st.Gold, free2 = st.FreePicks;
            int idx = st.Bench.FindIndex(u => u.Def.Name == name);
            bool placed = idx >= 0 && st.SwapWithBench(2, FormationRules.PlayableSlots[0], idx);
            bool inThird = st.Squads[2].Units?.Any(u => u.Def.Name == name) == true;
            write($"  (y) 金 {g0} で枠を買う {bought} → 金 {g1}・無料 {free1} → 候補 2 番目（{name}）を選ぶ {picked} → 金 {g2}・無料 {free2}・手札 {bench0} → {st.Bench.Count + (placed ? 1 : 0)}"
                + $" → 3 隊目の前1 に置いた {placed}（3 隊目に {name} {inThird}）");
            Req(bought && g1 == g0 - gr.Slot && free1 == 1 && picked && g2 == g1 && free2 == 0 && placed && inThird && st.CanSend(2),
                "(y) 枠を 8 で買うと候補から 1 枚が無料で入り（金は減らない）、3 隊目に置けて出せる");
        }

        // ---- 報告のみ: 呼ぶ {3,4,5} × 枠 {6,8} × 3 方針 × 100 seed ----
        MapStage With(int recruit, int slot) => new()
        {
            Id = stage.Id, Title = stage.Title, Board = stage.Board, Lanes = stage.Lanes, LaneNames = stage.LaneNames, Spawn = stage.Spawn,
            Time = stage.Time, Portal = stage.Portal, Arrival = stage.Arrival, Reserve = stage.Reserve, SquadCap = stage.SquadCap,
            TurnCap = stage.TurnCap, HeroAtCap = stage.HeroAtCap, WinOnCapture = stage.WinOnCapture, Gold = GoldRule.V446(recruit, slot),
        };
        var boards = new List<(string, MapStage)> { ("第445期（呼ぶ 10・枠 12）", MapStage.Stage12H445) };
        foreach (int r in new[] { 3, 4, 5 })
            foreach (int sl in new[] { 6, 8 })
                boards.Add(($"呼ぶ {r}・枠 {sl}{(r == 4 && sl == 8 ? "（既定）" : "")}", With(r, sl)));
        var policies = new[] { ("1 隊残す", true, false, false), ("分担＋戻り番", true, true, false), ("籠城", true, true, true) };
        write("");
        write("  | 盤 | 方針 | 踏破 | 陥落 | 時間切れ | 制圧の刻（中央値） | 呼んだ枚数（平均・最大） | リロール | 3 隊目が 5 枚になった | 金の余り（中央値・最大） |");
        write("  |---|---|--:|--:|--:|--:|---|--:|--:|---|");
        int crashed = 0;
        var thirdFull = new Dictionary<string, double>();
        foreach (var (bname, board) in boards)
            foreach (var (pname, guard, split, turtle) in policies)
            {
                var runs = new List<Once>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    try { runs.Add(Greedy(seed, board, guard, split, turtle)); }
                    catch (Exception ex)
                    {
                        crashed++;
                        if (crashed <= 3) write($"  × 第446期 {bname} {pname} seed {seed}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                if (runs.Count == 0) continue;
                double P(Func<Once, bool> f) => 100.0 * runs.Count(f) / runs.Count;
                var cap = runs.Where(r => r.Captured).Select(r => r.CapturedTurn).OrderBy(x => x).ToArray();
                var gold = runs.Select(r => r.GoldLeft).OrderBy(x => x).ToArray();
                thirdFull[$"{bname}|{pname}"] = P(r => r.ThirdFull);
                write($"  | {bname} | {pname} | {P(r => r.Won):F0} | {P(r => r.Fallen):F0} | {P(r => r.TimedOut):F0} | {(cap.Length == 0 ? "—" : cap[cap.Length / 2].ToString())} | "
                    + $"{runs.Average(r => r.Recruits):F2}・{runs.Max(r => r.Recruits)} | 0 | {P(r => r.ThirdFull):F0} | {gold[gold.Length / 2]}・{gold[^1]} |");
            }
        Req(crashed == 0, $"第446期の 7 つの盤 × 3 方針を {Seeds} seed ずつ回して落ちない（数字は報告のみ・線は置かない）");
        string[] pol = policies.Select(p => p.Item1).ToArray();
        bool up = pol.All(p => thirdFull[$"呼ぶ 4・枠 8（既定）|{p}"] > thirdFull[$"第445期（呼ぶ 10・枠 12）|{p}"]);
        write($"  3 隊目が 5 枚になった率: 第445期 → 既定 = {string.Join(" ／ ", pol.Select(p => $"{p} {thirdFull[$"第445期（呼ぶ 10・枠 12）|{p}"]:F0} → {thirdFull[$"呼ぶ 4・枠 8（既定）|{p}"]:F0}"))}");
        Req(up, "既定（呼ぶ 4・枠 8）は「3 隊目が 5 枚になった率」が 3 方針とも第445期より上がる");
    }

    /// <summary>第447期 —— 遊ぶ 1-2（`MapStage.Stage12`）の (z)(aa)(ab) と、3 方針の表（報告だけ）。</summary>
    private static void Phase447(Action<string> write, Action<bool, string> Req)
    {
        MapStage stage = MapStage.Stage12;
        GoldRule gr = stage.Gold;
        write("  ---- 第447期（遊ぶ 1-2・呼び戻す 6・リロール 2・隊の枠 4 つ（8 → 12）・12 作戦ターン以内に制圧） ----");
        // 相手を倒した形で精算する（seed に依らない）。lose に入れた味方は倒れた形にする。
        void Win(Map11State st, int squad, Map11State.Node node, bool intercept, int lose = -1)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Enemies) u.Hp = 0;
            if (lose >= 0) prep.Value.Players[lose].Hp = 0;
            st.Resolve(squad, node, true, intercept);
        }

        // ---- (z) 倒れた駒・呼び戻す ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            Map11State.Node lead = st.Nodes[2][0];
            lead.Pos = st.Graph.Home;
            UnitState victim = st.Squads[0].Units![0];
            Win(st, 0, lead, true, lose: 0);
            bool recorded = st.FallenUnits.Count == 1 && ReferenceEquals(st.FallenUnits[0], victim) && !st.Squads[0].Units!.Contains(victim);
            int offered = 0;
            for (int k = 0; k < 6; k++) { if (st.Offers.Any(o => o.Id == victim.Def.Id)) offered++; st.Reroll(); }
            // 金を足して（敵部隊をもう 1 つ抜いた形）呼び戻す。
            Map11State.Node north = st.Nodes[0][0];
            north.Pos = st.Graph.Home;
            Win(st, 1, north, true);
            int g0 = st.Gold, bench0 = st.Bench.Count;
            bool back = st.Revive(0);
            UnitState? got = st.Bench.LastOrDefault();
            write($"  (z) {victim.Def.Name} が倒れた: 記録 {recorded}・リロール 6 回で候補に出た {offered} 回 → 呼び戻す {back}: 金 {g0} → {st.Gold}・手札 {bench0} → {st.Bench.Count}"
                + $"・HP {got?.Hp}/{got?.MaxHp}・倒れた駒 {st.FallenUnits.Count} ／ 第446期の盤は呼び戻しが無い {MapStage.Stage12H446.Gold.Revive == 0}");
            Req(recorded && offered == 0 && back && st.Gold == g0 - gr.Revive && ReferenceEquals(got, victim) && got!.Hp == got.MaxHp * gr.RevivePercent / 100
                && st.FallenUnits.Count == 0 && MapStage.Stage12H446.Gold.Revive == 0,
                $"(z) 倒れた駒は候補に出ない・「呼び戻す」で HP {gr.RevivePercent}% で手札に戻り金が {gr.Revive} 減る（第446期までの盤には無い）");
        }

        // ---- (aa) 4 隊目の枠 ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            int p3 = st.SlotPrice;
            bool b3 = st.BuySlot();
            bool f3 = st.Recruit(0);
            // 金を貯める（敵部隊を抜いた形）。
            foreach (Map11State.Node n in st.Nodes.SelectMany(r => r).Take(3)) { n.Pos = st.Graph.Home; Win(st, 1, n, true); }
            int p4 = st.SlotPrice, g0 = st.Gold;
            bool b4 = st.BuySlot();
            int free = st.FreePicks;
            string name = st.Offers[0].Name;
            bool f4 = st.Recruit(0);
            int idx = st.Bench.FindIndex(u => u.Def.Name == name);
            bool placed = idx >= 0 && st.SwapWithBench(3, FormationRules.PlayableSlots[0], idx);
            write($"  (aa) 隊 {st.Squads.Length} つ（定義 {string.Join("・", st.Squads.Select(q => q.Def.Name))}）: 3 隊目の枠 {p3} {b3}・無料 {f3} → 金 {g0} で 4 隊目の枠 {p4} {b4}・無料 {free} → {name} {f4}"
                + $" → 4 隊目に置いた {placed}・使える隊 {st.SquadCap}・金 {st.Gold}");
            Req(st.Squads.Length == 4 && p3 == gr.Slot && b3 && f3 && p4 == gr.Slot + gr.SlotStep && p4 == 12 && b4 && free == 1 && f4 && st.Gold == g0 - p4
                && placed && st.SquadCap == 4 && st.CanSend(3),
                "(aa) 4 隊目の枠が 12 で買え（3 隊目 8 → 4 隊目 12）、無料の 1 枚が入って 4 隊目に置ける");
        }

        // ---- (ab) 12 作戦ターンで時間切れ ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            foreach (Map11State.Node n in st.Nodes.SelectMany(r => r)) n.Cleared = true;
            var left = new List<int> { st.TimeLimitLeft ?? -1 };
            for (int k = 0; k < 20 && !st.Finished; k++) { st.AdvanceFoes(); left.Add(st.TimeLimitLeft ?? -1); }
            var old = new Map11State(0, MapStage.Stage12H446.Time, MapStage.Stage12H446.Portal, draft: true, stage: MapStage.Stage12H446);
            write($"  (ab) 期限: {string.Join(" → ", left)} ／ 刻 {st.Turn} で終わり・時間切れ {st.TimeUp}・勇者の予告 {(st.HeroTurnsLeft is null ? "なし" : "あり")}"
                + $"・勇者が着いた {st.HeroArrived} ／ 第446期の盤: 勇者の予告 {(old.HeroTurnsLeft is null ? "なし" : "あり")}・期限の文 {(old.TimeLimitLeft is null ? "なし" : "あり")}");
            Req(stage.TurnCap == 12 && left[0] == 12 && st.Turn == 12 && st.Finished && st.TimeUp && !st.Won && st.HeroTurnsLeft is null && !st.HeroArrived
                && old.HeroTurnsLeft is not null && old.TimeLimitLeft is null,
                "(ab) 刻 12 で時間切れ（勇者の予告は出ない・「12 作戦ターン以内に制圧」の残りが 12 から減る）。第446期の盤は勇者の予告のまま");
        }

        // ---- 報告のみ: 3 方針 × 100 seed ----
        var boards = new[] { ("第446期の既定", MapStage.Stage12H446), ("第447期の既定", stage) };
        var policies = new[] { ("1 隊残す", true, false, false), ("分担＋戻り番", true, true, false), ("籠城", true, true, true) };
        write("");
        write("  | 盤 | 方針 | 踏破 | 陥落 | 時間切れ | 制圧の刻（中央値） | 呼んだ枚数（平均） | 呼び戻した枚数（平均・最大） | 呼び戻した通し | 3 隊目を開けた | 4 隊目を開けた | 金の余り（中央値） |");
        write("  |---|---|--:|--:|--:|--:|--:|---|--:|--:|--:|--:|");
        int crashed = 0;
        foreach (var (bname, board) in boards)
            foreach (var (pname, guard, split, turtle) in policies)
            {
                var runs = new List<Once>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    try { runs.Add(Greedy(seed, board, guard, split, turtle)); }
                    catch (Exception ex)
                    {
                        crashed++;
                        if (crashed <= 3) write($"  × 第447期 {bname} {pname} seed {seed}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                if (runs.Count == 0) continue;
                double P(Func<Once, bool> f) => 100.0 * runs.Count(f) / runs.Count;
                var cap = runs.Where(r => r.Captured).Select(r => r.CapturedTurn).OrderBy(x => x).ToArray();
                var gold = runs.Select(r => r.GoldLeft).OrderBy(x => x).ToArray();
                write($"  | {bname} | {pname} | {P(r => r.Won):F0} | {P(r => r.Fallen):F0} | {P(r => r.TimedOut):F0} | {(cap.Length == 0 ? "—" : cap[cap.Length / 2].ToString())} | "
                    + $"{runs.Average(r => r.Recruits):F2} | {runs.Average(r => r.Revives):F2}・{runs.Max(r => r.Revives)} | {P(r => r.Revives > 0):F0} | "
                    + $"{P(r => r.SquadsOpen >= 3):F0} | {P(r => r.SquadsOpen >= 4):F0} | {gold[gold.Length / 2]} |");
            }
        Req(crashed == 0, $"第447期の 2 つの盤 × 3 方針を {Seeds} seed ずつ回して落ちない（数字は報告のみ・線は置かない）");
    }

    /// <summary>手札の先頭から <paramref name="count"/> 枚を、その隊の空いた席（前1 から）へ配る。</summary>
    private static void Deal(Map11State st, int squad, int count)
    {
        for (int k = 0; k < count && st.Bench.Count > 0; k++)
        {
            int slot = FormationRules.PlayableSlots.FirstOrDefault(sl => st.UnitAt(squad, sl) is null, -1);
            if (slot < 0 || !st.SwapWithBench(squad, slot, 0)) return;
        }
    }

    /// <summary>
    /// 拠点にいる隊の倒れた駒・空いた席を、手札の生きている駒と替える（第442期・隊が 2 つの盤の貪欲）。替えた枚数を返す。
    /// </summary>
    private static int Refill(Map11State st, int squad)
    {
        int n = 0;
        foreach (int slot in FormationRules.PlayableSlots)
        {
            int b = st.Bench.FindIndex(x => x.IsAlive);
            if (b < 0) break;
            if (st.UnitAt(squad, slot) is { IsAlive: true }) continue;
            if (st.SwapWithBench(squad, slot, b)) n++;
        }
        return n;
    }

    private static void Fight(Map11State st, int squad, Map11State.Node node, bool intercept)
    {
        var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
        if (prep is not { } q) return;
        BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
        st.Resolve(squad, q.Node, r.PlayerWon, intercept);
    }

    private readonly record struct Once(int Seed, bool Won, bool Fallen, bool Captured, int CapturedTurn, int Turns,
                                        int Spawns, int SpawnsBeforeCapture, int ReserveLane, bool ReserveFought,
                                        bool ReserveAtHome, bool ReserveCleared, int Arrived, bool ThirdOut, int Moves, int Battles,
                                        int Refilled, bool DoubleAtHome, bool WipedOnSecond,
                                        bool TimedOut = false, int Outposts = 0, bool Released = false,
                                        int Recruits = 0, int GoldLeft = 0, bool SlotBought = false, bool ThirdFull = false,
                                        int Revives = 0, int SquadsOpen = 0);

    /// <summary>
    /// 貪欲方針で 1 マップ（自分で組む通し・到着あり）。開始の 10 枚を第1隊・第2隊へ 5 枚ずつ、届いた駒は拠点にいる第3隊へ配る。
    /// 第3隊は 5 枚そろうか、もう届く駒が無くなったら出る。各隊の行き先は盤の上でいちばん近い敵部隊（敵が尽きたら敵の拠点・制圧後は拠点）。
    /// <paramref name="guard"/> が真なら<b>番を置く</b>: 拠点にいて出せる隊がその隊しか無ければ、その隊は出ない（迎撃に残る）。
    /// <paramref name="split"/> が真なら（第442期・報告だけ）<b>分担＋戻り番</b>: 同じ刻に 2 隊が同じ敵を狙わない（若い隊から順に取る）。
    /// 番の規則は最初の接敵の後からで、そのとき拠点に出せる隊が 1 つもいなければ、拠点にいちばん近い隊が戻る。
    /// </summary>
    private static Once Greedy(int seed, MapStage stage, bool guard, bool split = false, bool turtle = false)
    {
        int recruits = 0;
        bool slotBought = false, thirdFull = false;
        int revives = 0;
        var st = new Map11State(seed, stage.Time, stage.Portal, draft: true, stage: stage);
        var g = st.Graph;
        Deal(st, 0, 5);
        Deal(st, 1, 5);
        int moves = 0, spawnsAtCapture = -1, refilled = 0, tickFights = 0;
        bool reserveFought = false, reserveAtHome = false, doubleAtHome = false, wipedOnSecond = false, released = false;
        Map11State.Node? reserve = st.Reserve;

        void Hit(int squad, Map11State.Node node, bool intercept)
        {
            if (node.IsReserve) { reserveFought = true; if (intercept) reserveAtHome = true; }
            Fight(st, squad, node, intercept);
            // 第443期: 同じ刻の拠点の迎撃が 2 戦目以降になったか・そこで全滅したか（報告だけ）。
            if (!intercept) return;
            if (++tickFights >= 2)
            {
                doubleAtHome = true;
                if (st.Squads[squad].Lost) wipedOnSecond = true;
            }
        }

        var taken = new HashSet<Map11State.Node>(ReferenceEqualityComparer.Instance);
        int homeGuard = -1;
        bool Ready(int k) => !st.Squads[k].Lost && st.CanSend(k);

        Dest DestFor(int i)
        {
            Map11State.Squad s = st.Squads[i];
            if (s.Lost) return Dest.Home;
            if ((i == 2 || (stage.Gold.On && i >= stage.Gold.StartSquads)) && s.Pos == g.Home
                && (stage.Gold.On ? s.Units?.Count(x => x.IsAlive) ?? 0 : s.Units?.Count ?? 0) < 5
                && (st.Arrivals.Count > 0 || stage.Gold.On)) return Dest.Home;
            if (split && i == homeGuard) return Dest.Home;
            // 第444期: 籠城——全隊（失った隊を除く）が生きた 5 枚になるまで拠点で待つ。そろったら分担＋戻り番で出る。
            if (turtle && !released) return Dest.Home;
            if (guard && (!split || st.Battles > 0) && s.Pos == g.Home && !st.Captured
                && !Enumerable.Range(0, st.Squads.Length).Any(k => k != i && st.Squads[k].Pos == g.Home && !st.Squads[k].Lost && st.CanSend(k)))
                return Dest.Home;
            Map11State.Node? best = null;
            int bestLen = int.MaxValue;
            for (int road = 0; road < g.Lanes.Count; road++)
                foreach (Map11State.Node n in st.FoesOn(road))
                {
                    if (n.Pos == g.Home || (split && taken.Contains(n))) continue;
                    int len = g.Path(s.Pos, s.Road, n.Pos, n.Pos == g.Portal ? n.Def.Road : -1).Count;
                    if (len > 0 && len < bestLen) { bestLen = len; best = n; }
                }
            if (best is not null && split) taken.Add(best);
            if (best is not null)
                return best.Pos == g.Portal ? Dest.Portal(g, best.Def.Road) : Dest.Of(g, best.Pos);
            if (!st.Captured) return Dest.Portal(g, s.Road >= 0 ? s.Road : 0);
            return Dest.Home;
        }

        while (!st.Finished)
        {
            // 届いた駒は拠点にいる第3隊へ（空いた席から）。隊が 2 つの盤（第442期）では、拠点にいる隊の倒れた駒・空いた席と替える。
            // 第445期: 金のある盤では、拠点の隊の穴（倒れた駒・空席）を候補の先頭で埋め、穴が無くなったら 3 隊目の枠を買う。
            if (stage.Gold.On)
            {
                int Holes() => Enumerable.Range(0, st.SquadCap).Where(st.CanReform)
                    .Sum(k => 5 - (st.Squads[k].Units?.Count(x => x.IsAlive) ?? 5));
                int Spare() => st.Bench.Count(x => x.IsAlive);
                // 第447期: 倒れた駒がいれば、呼ぶより先に呼び戻す（器具は軸の芯を見分けないので、倒れた順に）。
                while (Holes() > Spare() && Enumerable.Range(0, st.FallenUnits.Count).FirstOrDefault(st.CanRevive, -1) is var rv && rv >= 0)
                { st.Revive(rv); revives++; }
                while (Holes() > Spare() && st.Recruit(0)) recruits++;
                if (Holes() <= Spare() && st.BuySlot()) slotBought = true;
                while (Holes() > Spare() && st.Recruit(0)) recruits++;
            }
            if (turtle || stage.Gold.On) for (int k = 0; k < st.Squads.Length; k++) { if (st.CanReform(k)) refilled += Refill(st, k); }
            else if (st.Squads.Length > 2) { if (st.Bench.Count > 0 && st.CanReform(2)) Deal(st, 2, st.Bench.Count); }
            else for (int k = 0; k < st.Squads.Length; k++) if (st.CanReform(k)) refilled += Refill(st, k);
            // 第446期: 3 隊目が生きた 5 枚になったことがあるか（報告だけ）。
            if (st.Squads.Length > 2 && !st.IsLocked(2) && st.Squads[2].Units is { } third && third.Count(x => x.IsAlive) == 5) thirdFull = true;
            taken.Clear();
            homeGuard = -1;
            if (turtle && !released)
                // 第445期: 金のある盤では、まだ買っていない枠も「そろっていない隊」に数える（枠を買って 3 隊とも 5 枚になるまで待つ）。
                released = st.Squads.Where(q => !q.Lost && (stage.Gold.On || !st.IsLocked(q.Index)))
                                    .All(q => !st.IsLocked(q.Index) && q.Units is { } u && u.Count(x => x.IsAlive) == 5);
            if (split && st.Battles > 0 && !st.Captured
                && !Enumerable.Range(0, st.Squads.Length).Any(k => Ready(k) && st.Squads[k].Pos == g.Home))
                homeGuard = Enumerable.Range(0, st.Squads.Length).Where(Ready)
                    .OrderBy(k => g.Path(st.Squads[k].Pos, st.Squads[k].Road, g.Home, -1).Count).DefaultIfEmpty(-1).First();
            for (int i = 0; i < st.Squads.Length && !st.Finished; i++)
            {
                Dest dest = DestFor(i);
                Map11Orders.Order order = Map11Orders.Plan(st, i, dest);
                if (order == Map11Orders.Order.Move) moves++;
                if (Map11Orders.Apply(st, i, dest, order) is { } node) Hit(i, node, false);
                if (spawnsAtCapture < 0 && st.Captured) spawnsAtCapture = st.SpawnCount;
            }
            if (st.Finished) break;
            tickFights = 0;
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (st.Finished) break;
                if (!e.Intercept) { Hit(e.Squad, e.Node, false); continue; }
                while (!st.Finished && st.NextInterceptor(e.Node) is var j && j >= 0)
                {
                    Hit(j, e.Node, true);
                    if (e.Node.Cleared) break;
                }
                st.CloseIntercept(e.Node);
            }
            if (spawnsAtCapture < 0 && st.Captured) spawnsAtCapture = st.SpawnCount;
        }
        return new Once(seed, st.Won, st.Fallen, st.Captured, st.CapturedTurn + 1, st.Turn, st.SpawnCount,
                        Math.Max(0, spawnsAtCapture), reserve is null || st.ReserveDepartTurn < 0 ? -1 : reserve.Def.Road,
                        reserveFought, reserveAtHome, reserve?.Cleared == true, st.ArrivedCount,
                        st.Squads.Length > 2 && st.Squads[2].Deployed, moves, st.Battles, refilled,
                        doubleAtHome, wipedOnSecond, st.HeroArrived || (!st.Won && !st.Fallen && st.Turn >= stage.TurnCap),
                        st.OutpostCaptures, released, recruits, st.Gold, slotBought, thirdFull, revives, st.SquadCap);
    }
}
