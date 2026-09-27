using BattleCore;
using static Common;

// sero phase0 —— Q0-1〜Q0-6（第223期）。**E0（今のセロ）だけで回る**——新しい札を1枚も読まない。
static partial class SeroDiag
{
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第223期 Phase 0 —— 逃げ上手のセロ（`0 sero phase0`）");
        Console.WriteLine();

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 回避を挟む場所");
        Console.WriteLine();
        Console.WriteLine("敵の攻撃の被弾は、どの口から来ても最後は `ApplyDamage` → `ApplyDamageCore` → `ApplyDamageBody` の1本に入る。");
        Console.WriteLine("`ApplyDamageBody` の頭は「1回の呼び出しにだけ効く札」（逸らし・感電・燃焼の刻みそのもの・叩きつけ・爆発）を読んで消し、");
        Console.WriteLine("生死と量の門（`!target.IsAlive || amount <= 0`）で返る。**回避はその直後・逸らし（第186期）より前に置く**——");
        Console.WriteLine("札を読んで消した後なので、回避して返っても次の呼び出しに札が漏れない。破片・受け流し・軛・`OnDamaged` はすべて後ろなので、回避した一撃は破片も減らさない。");
        Console.WriteLine();
        Console.WriteLine("条件は「**出どころが相手陣営** かつ 刻み（`burnTick`）・徴収（`levy`）・中継（`relayed`）・呪いの共有（`hexShare`）ではない」の1本で書ける:");
        Console.WriteLine();
        Console.WriteLine("| 口 | 出どころ | 札 | 回避 |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine("| 単体・薙ぎ（主目標と巻き込み）・全体（`PerformAttackBody`） | 攻撃した敵 | — | ○（自分に来た分だけ・1回の呼び出しに1回） |");
        Console.WriteLine("| 貫き（`ResolvePierce` の各段） | 攻撃した敵 | `pattern: Pierce` | ○ |");
        Console.WriteLine("| 反撃・割り込み・追い打ち・再行動 | 敵（`PerformAttack` を通る） | — | ○ |");
        Console.WriteLine("| 敵の術の直撃（雷 `StrikeThunder`・叩きつけ `MireSlam`・口づけ・板の反射 `ReflectPlank`・棘・仇指し） | 敵 | — | ○ |");
        Console.WriteLine("| 毒の刻み（`PoisonTickOnce`）・起爆（`DetonateOne`） | null | 燃焼は `burnTick` | ×（出どころが無い） |");
        Console.WriteLine("| 燃焼の刻み（`BurnTickOnce`） | null | `burnTick` | × |");
        Console.WriteLine("| 放電（`Discharge`）・澱みの爆発（`EnqueueBurst`） | 同じ陣営の隣（放電した駒・爆ぜた駒） | `isFriendlyFire` | ×（出どころが同じ陣営） |");
        Console.WriteLine("| 味方の刃（巻き込み・余波・同士討ち・生贄・吸い） | 同じ陣営 | `levy` ほか | × |");
        Console.WriteLine("| 肩代わりの中継（巨躯・分かち・棘守り） | 元の攻撃者 | `relayed` | ×（最初の受け手で決まった一撃の分け前） |");
        Console.WriteLine("| 呪いの共有 | 殴った敵 | `hexShare` | × |");
        Console.WriteLine("| 逸らし（ソラ）の受け渡し | 元の攻撃者（受け手と同じ陣営） | `isFriendlyFire` | ×（出どころが同じ陣営） |");
        Console.WriteLine();
        Console.WriteLine("**決めたこと（指示書に無かった）**: 回避した一撃では**攻撃した側の `OnAfterAttack`（毒撃・傷・標・鞭の怖気づき ほか）も走らせない**");
        Console.WriteLine("——「回避した一撃は0」を「当たらなかった」と読む。主目標が回避したかは `PerformAttack` の枠に1つだけ控える（入れ子は退避）。");
        Console.WriteLine("巻き込みを回避しても主目標に当たっていれば `OnAfterAttack` は走る（あの札は主目標にしか効かない）。");
        Console.WriteLine();

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 移動の供給（E0・前段の規定のシオ・ヨミ・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("セロが「隊列を動かされた」回数（`Move` の出来事でセロが対象・出どころを問わない）と、**回避の機会**（敵が出どころで刻み・中継でない被弾＝回避の判定が振られる回数）。");
        Console.WriteLine("段の見当は**外から動かされた分だけ**（E0 には回避の入れ替えが無い）。E1 では回避 1 回ごとに入れ替えが 1 回足される（隣がいれば）。");
        Console.WriteLine();
        var benches = new List<(string Name, Formation F)> { ("S1 ポン", BenchS1Pon) };
        foreach (var r in CompareRowsWithSero()) benches.Add(("S4 " + r.Name, r.F));
        Console.WriteLine("| 台 | 倍率 | 波 | 動かされた ／ 戦 | 出どころ（／ 戦） | T1 | T2 | T3 | T4 | T5 | 3回 到達（率 ／ T） | 6回 | 10回 | 回避の機会 ／ 戦 | 機会 × 0.30 | セロが落ちた（率 ／ T） |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|--:|---|---|---|--:|--:|---|");
        foreach (var (bn, f) in benches)
            for (int s = 0; s < Scales.Length; s++)
                foreach (var (gn, ws) in new (string, int[])[] { ("本編 第2〜5波", new[] { 0, 1, 2, 3 }), ("九/新兵", new[] { 4 }) })
                {
                    if (s == 0 && bn.StartsWith("S4") && gn == "九/新兵") { }   // すべて出す
                    var a = new MoveAgg();
                    foreach (int w in ws) a.Merge(MeasureMoves(f, WaveOf(w, Scales[s].Sc)));
                    string src = string.Join(" ", a.By.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {a.Per(kv.Value):F2}"));
                    string tt = string.Join(" | ", Enumerable.Range(1, 5).Select(t => F2(a.Per(a.ByTurn[t]))));
                    string R(int k) => $"{F1(100.0 * a.Reach[k] / a.N)}% ／ {F2(a.Reach[k] == 0 ? double.NaN : (double)a.ReachT[k] / a.Reach[k])}";
                    Console.WriteLine($"| {bn} | {Scales[s].Name} | {gn} | {F2(a.Per(a.Moves))} | {src} | {tt} | {R(0)} | {R(1)} | {R(2)} | {F2(a.Per(a.Chances))} | {F2(a.Per(a.Chances) * 0.30)} | {F1(100.0 * a.Fell / a.N)}% ／ {F2(a.Fell == 0 ? double.NaN : (double)a.FellT / a.Fell)} |");
                }
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 入れ替えの相手（隣の数）");
        Console.WriteLine();
        Console.WriteLine("「隣」は陣形の隣接表（`FormationShape.AreAdjacent`）。編成の5枠が全員生きているときの数。召喚枠の味方（胞子・餌）は入れ替えの相手にしない（決めたこと・シオの組み替え・ヒサの逃げと同じ）。");
        Console.WriteLine();
        foreach (var shape in new[] { FormationShape.X, FormationShape.Diamond })
        {
            Console.WriteLine($"- {shape.Name}: " + string.Join(" ／ ", Enumerable.Range(0, 5).Select(i =>
            {
                int si = shape.PlayableSlots[i];
                int n = Enumerable.Range(0, 5).Count(j => j != i && shape.AreAdjacent(si, shape.PlayableSlots[j]));
                return $"{shape.FrameNames[i]} {n}";
            })));
        }
        Console.WriteLine();
        Console.WriteLine("**X字は角の4席が隣 2・中央だけが隣 4**、パターン2は中衛の3席が多い。セロが角にいれば入れ替わりは「中央と、同じレーンの前後」のどちらか（1/2 ずつ）。");
        Console.WriteLine();
        Console.WriteLine("台ごとのセロの隣（開幕）と、崩れうるもの:");
        Console.WriteLine();
        Console.WriteLine("| 台 | セロの席 | 隣 | 庇い（前列のガルド）が隣 | 後列前提の駒が隣 |");
        Console.WriteLine("|---|---|---|---|---|");
        var seatSensitive = new HashSet<string> { "sekki", "rica", "dolga", "tomo", "lili", "tsugi" };
        foreach (var (bn, f) in benches.Append(("S3 状態の矢（仮の並び）", BenchS3Raw)))
        {
            var occ = f.Occupied().ToList();
            var sero = occ.First(o => o.Def.Id == "sero");
            var nb = occ.Where(o => o.Slot != sero.Slot && f.Shape.AreAdjacent(f.Shape.PlayableSlots[o.Slot], f.Shape.PlayableSlots[sero.Slot])).ToList();
            bool gald = nb.Any(o => o.Def.Id == "gald" && FormationRules.RowOf(f.Shape.PlayableSlots[o.Slot]) == Row.Front);
            var back = nb.Where(o => seatSensitive.Contains(o.Def.Id)).Select(o => o.Def.Name).ToList();
            Console.WriteLine($"| {bn} | {FormationRules.SeatNames[f.Shape.PlayableSlots[sero.Slot]]} | {string.Join("・", nb.Select(o => o.Def.Name))} | {(gald ? "**○**" : "—")} | {(back.Count > 0 ? string.Join("・", back) : "—")} |");
        }
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 追い撃ちの貫き（経路に属さない席）");
        Console.WriteLine();
        Console.WriteLine("X字の ○前2・○後2（9体の波の席 7・8）はどのレーンにも属さない（`LanesOf` が空）。攻撃してきた敵がそこにいれば**単体の1発**にする（指示書のとおり）。");
        Console.WriteLine("中央（両方のレーン）の敵なら、**生きている駒が多いレーン・同数なら添字の若いレーン**（乱数を引かない）。経路は前から後ろへ走るので、攻撃してきた敵より前の敵にも当たり、その敵には減衰が乗る。");
        Console.WriteLine("攻撃してきた敵が庇い・後備えに守られていても**介入の鎖は通さない**（撃ち返す相手は決まっている・ガルドの斬り返しと同じ）。");
        Console.WriteLine();
        var slotsWithLane = Enumerable.Range(0, 9).Select(s => (s, FormationShape.X.LanesOf(s).Count)).ToList();
        Console.WriteLine("- X字の席ごとのレーン数: " + string.Join(" ／ ", slotsWithLane.Select(x => $"{FormationRules.SeatNames[x.s]} {x.Count}")));
        Console.WriteLine();

        // ---------------- Q0-5 ----------------
        Console.WriteLine("## Q0-5 過去の測定");
        Console.WriteLine();
        Console.WriteLine("- `design/IDEAS_PENDING.md` (5)「回避という通貨」（第98期）: 軽減ではなく二値で「当たらない」を作る案。**確率を使わない書き方（回数制・条件制）を先に確かめよ**と書いてある。");
        Console.WriteLine("- R119（第135期）: 「1発を丸ごと無効化する」機構は受け流し（ガルド・回数制）の1枚だけ。**無効化を作るなら確率ではなく回数にする**——確率だと弾いた量が敵の火力に比例し、難易度カーブが平坦化する。");
        Console.WriteLine("  **この期の回避は確率**（ポンと決めた形）なので、R119 の懸念はそのまま当たる。表B の「回避した量」を敵の一撃の大きさ別に見る。");
        Console.WriteLine("- R155（第136期）: 無効化は被弾を入力にする札を飢えさせる。**セロ自身は被弾を読む札を持たない**が、回避した一撃では攻撃した側の `OnAfterAttack` も止める（Q0-1）ので、敵の毒撃・傷の書き手はセロに書けなくなる。");
        Console.WriteLine("- 撃ち返し: 棘（カド・反撃）・剣の段の斬り返し（ガルド・第198期・`ApplyDamage` 直）・仇指し（ザン）。**追い撃ちは `PerformAttack` を通す**（標的の鎖だけ飛ばす）——斬り返しと違い、痺れ毒・萎縮・澱み・§1 の +50% が今までどおり掛かり、貫きにできる。");
        Console.WriteLine("- セロの過去: 第1期から `Sniper` / `Coward`。第129期に延命台で密度を測った5枚の1枚（×0.74）。第189期にハネの勢い余っての入れ替えで構えが整う（`OverrunReaderSniper`）。回避・被弾無効の版はこれまで1度も無い。");
        Console.WriteLine();

        // ---------------- Q0-6 ----------------
        Console.WriteLine("## Q0-6 台");
        Console.WriteLine();
        Console.WriteLine("S2（庇いの無い移動軸）の5枚目は**規則で選ぶ**: ポンの席（S1）のガルドの席（前3）に置く駒を、庇う・肩代わりする駒（ガルド・ゴルム・ドハ・セッキ・ウケ・ワタ・ササ・カド・ヒサ・バン）と移動軸の4枚を除いた");
        Console.WriteLine("`UnitCatalog.All` から、**E0 × 本編 第2〜5波 ＋ 九/新兵 × 115/115 × seed 1000..1049** の勝ち数が最大の1枚（同値は全員生存 → ロスター順）。測る seed（0..199）とは別の帯。");
        Console.WriteLine();
        var cand = new List<(UnitDef D, int W, int Sv)>();
        foreach (UnitDef d in UnitCatalog.All)
        {
            if (NotForS2.Contains(d.Id)) continue;
            var f = BenchS1Pon.Clone();
            foreach (var (slot, x) in BenchS1Pon.Occupied()) if (x.Id == "gald") f[slot] = d;
            int w = 0, sv = 0;
            for (int wv = 0; wv < 5; wv++)
            {
                var q = DriftDiag.Quick(f, WaveOf(wv, Scales[0].Sc), 1000, 50);
                w += q.Wins; sv += q.Surv;
            }
            cand.Add((d, w, sv));
        }
        var ranked = cand.Select((c, i) => (c, i)).OrderByDescending(z => z.c.W).ThenByDescending(z => z.c.Sv).ThenBy(z => z.i).Select(z => z.c).ToList();
        Console.WriteLine($"候補 {ranked.Count} 枚。上位:");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 駒 | 勝ち数/250 | 全員生存/250 |");
        Console.WriteLine("|--:|---|--:|--:|");
        for (int i = 0; i < Math.Min(8, ranked.Count); i++) Console.WriteLine($"| {i + 1} | {ranked[i].D.Name} | {ranked[i].W} | {ranked[i].Sv} |");
        Console.WriteLine();
        Console.WriteLine($"**S2 の5枚目: {ranked[0].D.Name}**（`SeroDiag.S2Fifth` に固定する）。");
        Console.WriteLine();
        Console.WriteLine("台の顔ぶれ:");
        Console.WriteLine();
        Console.WriteLine($"- S1 ポンの移動改: {SeatsNamed(BenchS1Pon)}（席は E1 × 九/新兵 × 150/115 で総当たりした席も並べる）");
        Console.WriteLine($"- S2 庇いの無い移動軸: バサ・ヨミ・シオ・セロ・{ranked[0].D.Name}");
        Console.WriteLine($"- S3 状態の矢: ベニ・ミオ・カタ・セロ・クビ——**セロに状態を書く駒**はカタ（雷の代金＝隣に感電）・ベニ（開戦の撒き O4 は敵へ・手番の澱み分けと火は隣の味方へ）・ミオ（印の代金は隣へ）");
        Console.WriteLine($"- S4 `compare` のセロの行 {CompareRowsWithSero().Count} 行: " + string.Join(" ／ ", CompareRowsWithSero().Select(r => r.Name)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }

    internal sealed class MoveAgg
    {
        public long N, Moves, Chances, Fell, FellT;
        public readonly Dictionary<string, long> By = new();
        public readonly long[] ByTurn = new long[31];
        public readonly long[] Reach = new long[3], ReachT = new long[3];
        public void Merge(MoveAgg o)
        {
            N += o.N; Moves += o.Moves; Chances += o.Chances; Fell += o.Fell; FellT += o.FellT;
            foreach (var (k, v) in o.By) By[k] = By.GetValueOrDefault(k) + v;
            for (int i = 0; i < ByTurn.Length; i++) ByTurn[i] += o.ByTurn[i];
            for (int i = 0; i < 3; i++) { Reach[i] += o.Reach[i]; ReachT[i] += o.ReachT[i]; }
        }
        public double Per(long x) => (double)x / Math.Max(1, N);
    }

    static readonly int[] StageMoves = { 3, 6, 10 };

    static MoveAgg MeasureMoves(Formation f, Func<List<UnitState>> enemy)
    {
        var total = new MoveAgg();
        var gate = new object();
        Parallel.For(0, Seeds, () => new MoveAgg(), (j, _, a) =>
        {
            var player = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var r = BattleEngine.Run(player, enemy(), j, verbose: true);
            UnitState sero = player.First(u => u.Def.Id == "sero");
            var nameOf = player.ToDictionary(u => u.InstanceId, u => u.Def.Name);
            a.N++;
            int moves = 0;
            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind == BattleEventKind.Move && e.TargetId == sero.InstanceId)
                {
                    moves++; a.Moves++;
                    if (e.Turn < a.ByTurn.Length) a.ByTurn[e.Turn]++;
                    string src = e.ActorId is int id ? (id == sero.InstanceId ? "（自分・臆病）" : nameOf.TryGetValue(id, out var nm) ? nm : "敵") : "（不明）";
                    a.By[src] = a.By.GetValueOrDefault(src) + 1;
                    for (int k = 0; k < 3; k++) if (moves == StageMoves[k]) { a.Reach[k]++; a.ReachT[k] += e.Turn; }
                }
                else if (e.Kind == BattleEventKind.Damage && e.TargetId == sero.InstanceId && e.ActorId is int src2
                         && !nameOf.ContainsKey(src2) && !e.FriendlyFire && !e.Relayed)
                    a.Chances++;
                else if (e.Kind == BattleEventKind.Death && e.TargetId == sero.InstanceId) { a.Fell++; a.FellT += e.Turn; }
            }
            return a;
        }, a => { lock (gate) total.Merge(a); });
        return total;
    }
}
