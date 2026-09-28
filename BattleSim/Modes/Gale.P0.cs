using BattleCore;
using static Common;

// gale phase0 —— 第229期 Q0-1〜Q0-7（G0 ＝ 前段の規定だけで回る）。
static partial class GaleDiag
{
    static string Seat(int s) => FormationRules.SeatNames[s];

    /// <summary>転倒の出どころ（Stagger 転倒 の出来事の書き手と、その直前の見出し）。</summary>
    internal static readonly string[] StaggerSources = { "バサ突風", "ハネ吹っ飛ばし", "ハネ弾き返し", "ハネ突き返し", "その他" };
    /// <summary>敵を後ろの行へ動かした出どころ（追い風の条件）。</summary>
    internal static readonly string[] BackSources = { "バサ入れ替え", "ハネ吹っ飛ばし", "ハネ弾き返し", "ハネ突き崩し" };

    internal sealed class P0Agg
    {
        public long N, Wins, AllSurv;
        public readonly long[] StagAlly = new long[5], StagFoe = new long[5];
        public long StagFrontFoe, StagFrontAlly;         // 転んだときに前列にいた
        public long HoleAttacksOnFoe, HoleAttacksOnAlly;  // 穴が効く局面での（貫き・全体以外の）攻撃
        public long HoleTurnsFoe;                          // 敵陣に穴が開いたターンの数（ターン頭でなく出来事ごとに1度数える）
        public readonly long[] Back = new long[4], BackPair = new long[4], BackPairHp = new long[4];
        public long Retreats, RetreatThenBack, RetreatRear, RetreatRearHp;
        public double RetreatHpSum; public long RetreatHpN;

        public void Merge(P0Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv;
            for (int i = 0; i < 5; i++) { StagAlly[i] += o.StagAlly[i]; StagFoe[i] += o.StagFoe[i]; }
            StagFrontFoe += o.StagFrontFoe; StagFrontAlly += o.StagFrontAlly;
            HoleAttacksOnFoe += o.HoleAttacksOnFoe; HoleAttacksOnAlly += o.HoleAttacksOnAlly; HoleTurnsFoe += o.HoleTurnsFoe;
            for (int i = 0; i < 4; i++) { Back[i] += o.Back[i]; BackPair[i] += o.BackPair[i]; BackPairHp[i] += o.BackPairHp[i]; }
            Retreats += o.Retreats; RetreatThenBack += o.RetreatThenBack; RetreatRear += o.RetreatRear; RetreatRearHp += o.RetreatRearHp;
            RetreatHpSum += o.RetreatHpSum; RetreatHpN += o.RetreatHpN;
        }
        public double Per(long x) => (double)x / Math.Max(1, N);

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            N++;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var b = new Board(p.Concat(e), slot0);
            int? basa = p.FirstOrDefault(u => u.Def.Id == "basa")?.InstanceId;
            int? hane = p.FirstOrDefault(u => u.Def.Id == "hane")?.InstanceId;
            int haneMode = 0;   // 0 なし ／ 1 吹っ飛ばし ／ 2 弾き返し
            int holeTurn = -1;
            var retreated = new Dictionary<int, int>();   // 緊急退避で下げた駒 → ターン
            foreach (BattleEvent ev in r.Events)
            {
                // ---- 出来事の直前の盤面で読む ----
                // （ハネの見出しのリセットは別に見る——同じ switch に置くと穴の攻撃の枝が食われる）
                if (ev.Kind == BattleEventKind.Attack && ev.ActorId != hane) haneMode = 0;
                switch (ev.Kind)
                {
                    case BattleEventKind.Blast when ev.ActorId == hane: haneMode = 1; break;
                    case BattleEventKind.Spring when ev.ActorId == hane: haneMode = 2; break;
                    case BattleEventKind.TurnStart: haneMode = 0; break;
                    case BattleEventKind.Retreat when ev.TargetId is int rt: Retreats++; retreated[rt] = ev.Turn; break;
                    case BattleEventKind.Attack when ev.TargetId is int at && b.Team.ContainsKey(at)
                                                     && ev.Pattern is not AttackPattern.Pierce and not AttackPattern.All:
                        {
                            int tt = b.Team[at];
                            if (b.Hole(tt)) { if (tt == BattleContext.EnemyTeam) HoleAttacksOnFoe++; else HoleAttacksOnAlly++; }
                            break;
                        }
                    case BattleEventKind.Stagger when ev.Text == StaggerLabels.Fell && ev.TargetId is int st && b.Team.ContainsKey(st):
                        {
                            int src = ev.ActorId == basa ? 0 : ev.ActorId == hane ? (haneMode == 1 ? 1 : haneMode == 2 ? 2 : 3) : 4;
                            bool foe = b.Team[st] == BattleContext.EnemyTeam;
                            (foe ? StagFoe : StagAlly)[src]++;
                            if (b.RowOfUnit(st) == Row.Front) { if (foe) StagFrontFoe++; else StagFrontAlly++; }
                            if (ev.ActorId == hane) haneMode = 0;
                            break;
                        }
                    case BattleEventKind.Move when ev.TargetId is int mv && b.Team.GetValueOrDefault(mv, -1) == BattleContext.EnemyTeam
                                                   && (ev.ActorId == basa || ev.ActorId == hane) && ev.ActorId is not null:
                        {
                            int fromSlot = b.Slot[mv];
                            if (FormationRules.DepthOf(FormationRules.RowOf(ev.Slot)) <= FormationRules.DepthOf(FormationRules.RowOf(fromSlot))) break;
                            int src = ev.ActorId == basa ? 0 : haneMode == 1 ? 1 : haneMode == 2 ? 2 : 3;
                            Back[src]++;
                            bool pair = false, pairHp = false;
                            foreach (int lane in FormationRules.LanesOf(fromSlot))
                            {
                                var line = b.LaneAllies(BattleContext.PlayerTeam, lane);
                                if (line.Count < 2) continue;
                                pair = true;
                                if (line.Skip(1).Any(i => b.Hp[i] * 10 >= b.MaxHp[i] * 4)) pairHp = true;
                                // Q0-5: 同じターンに緊急退避で下げた駒が、この経路の最後尾の候補か
                                foreach (var (x, turn) in retreated)
                                {
                                    if (turn != ev.Turn || !b.Alive.Contains(x) || !line.Skip(1).Contains(x)) continue;
                                    RetreatThenBack++;
                                    if (line.Last() == x) { RetreatRear++; if (b.Hp[x] * 10 >= b.MaxHp[x] * 4) RetreatRearHp++; }
                                }
                            }
                            if (pair) BackPair[src]++;
                            if (pairHp) BackPairHp[src]++;
                            break;
                        }
                }
                b.Apply(ev);
                // 緊急退避の直後の HP（移り木の回復が乗った後）: 退避した駒の次の Heal/Damage までを1回だけ見るのではなく、
                // ターンの終わりに近い値として「同じターンの最後に見えた HP」を集める（下で集計）。
                if (ev.Kind == BattleEventKind.TurnStart)
                {
                    foreach (var (x, turn) in retreated.ToList())
                        if (turn == ev.Turn - 1 && b.Alive.Contains(x)) { RetreatHpSum += (double)b.Hp[x] / b.MaxHp[x]; RetreatHpN++; }
                }
                if (b.Hole(BattleContext.EnemyTeam) && holeTurn != ev.Turn) { holeTurn = ev.Turn; HoleTurnsFoe++; }
            }
        }
    }

    internal static P0Agg MeasureP0(Formation f, int w, EnemyScaleRule sc, int seeds = Seeds)
    {
        var parts = new P0Agg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var a = new P0Agg();
            var (r, p, e, slot0) = Fight(f, w, sc, i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new P0Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第229期 Phase 0（`0 gale phase0`・G0 ＝ 前段の規定）");
        Console.WriteLine();
        Console.WriteLine($"台: M-ハネ（228 H3 の1位）＝ {SeatsNamed(MHane228)} ／ 参考 雷 ＝ {SeatsNamed(Thunder)}");
        Console.WriteLine();

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1 転倒の出どころ（1戦あたり・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("転倒を立てる口（実装を読んだもの）: バサの突風（`ShufflerTrait.Blow`・薙ぎが当たった敵・20%）／ ハネの吹っ飛ばし（A）・弾き返し（殴った敵）・突き返し（H0 の手番・前段の後は吹っ飛ばしに置き換わる）／"
                          + " 喧噪の転倒（`ShuffleStagger.Advanced`・規定は混乱なので立たない）／ ササの身構えの転倒（`BraceRule.Stagger`・規定は false）。**味方に転倒を書く口は規定では 0 本**（身構えの転倒は第143期に採らなかった）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 全員生存 | " + string.Join(" | ", StaggerSources.Select(s => "敵に" + s)) + " | 味方に | 前列で転んだ（敵） | 敵陣に穴が開いたターン | 穴の局面の攻撃（敵へ ／ 味方へ） |");
        Console.WriteLine("|---|---|--:|" + string.Concat(StaggerSources.Select(_ => "--:|")) + "--:|--:|--:|---|");
        var benches = new (string Name, Formation F)[] { ("M-ハネ 228", MHane228), ("参考 雷", Thunder) };
        foreach (var (name, f) in benches)
            foreach (int s in new[] { 0, 1, 2 })
                foreach (int w in new[] { 4, 5, 0, 1, 2, 3 })
                {
                    if (name == "参考 雷" && s != 0) continue;
                    if (s != 0 && w != 4) continue;
                    var a = MeasureP0(f, w, Scales[s].Sc);
                    Console.WriteLine($"| {name} | {Scales[s].Name} {WaveNames[w]} | {F1(100.0 * a.AllSurv / a.N)} | " + string.Join(" | ", a.StagFoe.Select(x => F2(a.Per(x))))
                                      + $" | {F2(a.Per(a.StagAlly.Sum()))} | {(a.StagFoe.Sum() == 0 ? "—" : (100.0 * a.StagFrontFoe / a.StagFoe.Sum()).ToString("F0") + "%")} | {F2(a.Per(a.HoleTurnsFoe))} | {F2(a.Per(a.HoleAttacksOnFoe))} ／ {F2(a.Per(a.HoleAttacksOnAlly))} |");
                }
        Console.WriteLine();
        Console.WriteLine("「穴の局面の攻撃」＝ 狙われた側の陣の一番前の列が全員転倒していて、その後ろに誰か立っているときの（貫き・全体以外の）攻撃。**G3 で主目標の選び方が変わりうる攻撃の上限**。");
        Console.WriteLine();

        // compare 61 行（115/115・第1〜5波）
        Console.WriteLine("### `compare` 61 行（115/115・第1〜5波・seed 0..199）で転倒が付く行");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 転倒/戦（敵 ／ 味方） | 前列で（敵） | 穴の局面の攻撃（敵へ ／ 味方へ） |");
        Console.WriteLine("|---|---|---|--:|---|");
        var sc115 = Scales[2].Sc;
        int rowsAny = 0;
        foreach (var (rn, rf) in CompareBuilds())
        {
            bool any = false;
            for (int st = 0; st < 5; st++)
            {
                var parts = new P0Agg[Seeds];
                Parallel.For(0, Seeds, i =>
                {
                    var p = BattleEngine.Materialize(OldTune(OldYomiShio(OldGale(rf))), BattleContext.PlayerTeam);   // 第230期 前段: 第229期の規定に固定
                    var e = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, sc115);
                    // InstanceId は Run の中の Add で振られるので、席は Run の後に引き直す
                    var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
                    var r = BattleEngine.Run(p, e, i, verbose: true, shuffler: PreHole);
                    var a = new P0Agg();
                    a.Take(r, p, e, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
                    parts[i] = a;
                });
                var all = new P0Agg();
                foreach (var a in parts) all.Merge(a);
                long sf = all.StagFoe.Sum(), sa = all.StagAlly.Sum();
                if (sf + sa == 0) continue;
                any = true;
                Console.WriteLine($"| {rn} | 第{st + 1}波 | {F2(all.Per(sf))} ／ {F2(all.Per(sa))} | {(sf == 0 ? "—" : (100.0 * all.StagFrontFoe / sf).ToString("F0") + "%")} | {F2(all.Per(all.HoleAttacksOnFoe))} ／ {F2(all.Per(all.HoleAttacksOnAlly))} |");
            }
            if (any) rowsAny++;
        }
        Console.WriteLine();
        Console.WriteLine($"転倒が1度でも付いた行: **{rowsAny} / {CompareBuilds().Count()}**。それ以外の行は G3 で1ビットも動かない（転倒が付かなければ規則は読まれない）。");
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2 前列の規則の場所");
        Console.WriteLine();
        Console.WriteLine("- 判定は `BattleContext.PoolOf`（`BattleEngine.cs`）の1箇所: 前列（`Row.Front`）に生きている駒がいればその全員、いなければ中列、それも無ければ全員。**生死以外の条件は無い**（`foes` が生存者だけ）。");
        Console.WriteLine("- 使うのは 標的選択の鎖（`SelectTargetChain` の主目標・単体と薙ぎの中心）と `TargetPool`（断ちの `CanAct` と共有）。**貫きは `SelectPierceEntry` が経路を直接走るので通らない**、全体は主目標が全員。");
        Console.WriteLine("- 列は席の幾何（`FormationRules.RowOf`）で、陣形に依らない。召喚枠 ○前2 は前列・○後2 は後列・○中1/○中3 は中列——**9体の波の ○前2 の敵は前列の壁に数わる**（5体の波では空席）。");
        Console.WriteLine();
        Console.WriteLine("| 席 | 列 |");
        Console.WriteLine("|---|---|");
        foreach (int s in Enumerable.Range(0, 9)) Console.WriteLine($"| {Seat(s)} | {FormationRules.RowOf(s)} |");
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3 標的の介入の一覧（転倒した駒を除く判定を入れる場所）");
        Console.WriteLine();
        Console.WriteLine("| 介入 | 札 | 場所 | 入れるか |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine("| 前列の壁 | — | `PoolOf` | ○（転倒した駒は壁に数えない・狙われはする） |");
        Console.WriteLine("| 挑発 | `Decoy`（セロ） | 鎖の主目標の段（`DecoyTrait.Pick`） | ○ |");
        Console.WriteLine("| 後備え | `RearGuard`（セッキ） | 鎖（範囲・単体の2箇所） | ○ |");
        Console.WriteLine("| 庇う | `Guardian`（ガルド） | 鎖 | ○ |");
        Console.WriteLine("| 殉教 | `Martyr`（敵の殉教者） | 鎖 | ○ |");
        Console.WriteLine("| 棘守り（身代わり） | `ThornGuard`（カド） | 鎖 | ○ |");
        Console.WriteLine("| 範囲の盾 | `Footing`（バン） | `PerformAttack` の `ShieldHit` | ○ |");
        Console.WriteLine("| 受け流し | `Parry`（ガルド） | `ApplyDamageBody` | ○ |");
        Console.WriteLine("| 逸らし | `Deflect`（ソラ） | `ApplyDamageBody` の入口 | ○ |");
        Console.WriteLine("| 標（気を取られる） | `Marked` | 鎖の1段目 | ×（攻撃者の側の選好。標を付けられた駒が引き受けに出るのではない） |");
        Console.WriteLine("| 矢面の半減 | `Beckon`（ヒサ） | `ApplyDamageBody` | ×（半減を掛けるのはヒサで、受ける駒は選んでいない） |");
        Console.WriteLine("| 巨躯・分かち・棘守りの中継 | `Colossus` / `Sharer` | `ApplyDamageBody` | ×（標的選択ではなくダメージの再分配） |");
        Console.WriteLine("| 回避・必死の逃げ足 | `Evade` / `LastDodge`（セロ） | `ApplyDamageBody` | ×（引き受けではなく自分の身をかわす） |");
        Console.WriteLine();

        // ---- Q0-4 / Q0-5 ----
        Console.WriteLine("## Q0-4 追い風の実数 ／ Q0-5 緊急退避との重なり（M-ハネ 228・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("敵を後ろの行へ動かした回数（1戦あたり）と、そのとき敵がいた席の経路（中央は2本）で味方が2体以上いた割合 ／ さらに先頭以外に HP 4割以上の味方がいた割合（＝踏み込める味方がいた）。");
        Console.WriteLine();
        Console.WriteLine("| 倍率・波 | " + string.Join(" | ", BackSources) + " | 合計 | 味方2体以上 | 踏み込める | 緊急退避/戦 | 退避の同じターンに後ろへ動いた経路で退避した駒が候補 | うち最後尾 | うち4割以上 | 退避した駒の次のターン頭の HP |");
        Console.WriteLine("|---|" + string.Concat(BackSources.Select(_ => "--:|")) + "--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int s in new[] { 0, 1, 2 })
            foreach (int w in new[] { 4, 5, 0, 1, 2, 3 })
            {
                if (s != 0 && w != 4) continue;
                var a = MeasureP0(MHane228, w, Scales[s].Sc);
                long tot = a.Back.Sum();
                Console.WriteLine($"| {Scales[s].Name} {WaveNames[w]} | " + string.Join(" | ", a.Back.Select(x => F2(a.Per(x)))) + $" | {F2(a.Per(tot))} | "
                                  + $"{(tot == 0 ? "—" : (100.0 * a.BackPair.Sum() / tot).ToString("F0") + "%")} | {(tot == 0 ? "—" : (100.0 * a.BackPairHp.Sum() / tot).ToString("F0") + "%")} | "
                                  + $"{F2(a.Per(a.Retreats))} | {F2(a.Per(a.RetreatThenBack))} | {F2(a.Per(a.RetreatRear))} | {F2(a.Per(a.RetreatRearHp))} | {(a.RetreatHpN == 0 ? "—" : (100.0 * a.RetreatHpSum / a.RetreatHpN).ToString("F0") + "%")} |");
            }
        Console.WriteLine();

        // ---- Q0-7 ----
        Console.WriteLine("## Q0-7 台");
        Console.WriteLine();
        Console.WriteLine($"- M-ハネ（228 H3 の1位）: {SeatsNamed(MHane228)}（G4 の総当たりは実装の後に選ぶ）");
        Console.WriteLine($"- 参考 雷: {SeatsNamed(Thunder)}");
        foreach (var (n, f) in CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id is "basa" or "hane")))
            Console.WriteLine($"- compare {n}: {SeatsNamed(f)}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}

