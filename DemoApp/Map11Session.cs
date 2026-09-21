using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// マップ 1-1 の通し（第169期）。**シーンを跨いで残る唯一の場所。**
//
// `CampaignSession`（既存の作戦マップ）とは**別の入れ物**にしてある——あちらは
// 「どこに居るか」と勝敗しか持っておらず、こちらは駒そのもの（`UnitState`）を持ち越す。
// 混ぜると、既存のデモ（単発の戦闘確認）が壊れる。
// =====================================================================================

public static class Map11Session
{
    public const string Scene = "res://Map11Main.tscn";

    /// <summary>
    /// <b>戦闘1回につき1行</b>（第170期 §1-5）。第169期の「出した順」は
    /// <b>送り先を変えたときだけ</b>1行足す作りだったので、戦闘回数と合わなかった
    /// ——3 回目の走行は戦闘 5 回に対して 3 行しか出ず、
    /// <b>どの隊が何戦目を抜いたか後から復元できなかった。</b>
    /// </summary>
    /// <param name="No">何戦目か（1 始まり）。</param>
    /// <param name="Squad">出した隊。</param>
    /// <param name="Road">道。</param>
    /// <param name="Foe">相手の部隊名（その道の何番目か込み）。</param>
    /// <param name="Won">抜いたか。</param>
    /// <param name="Alive">戦闘後にその隊に残った枚数。</param>
    /// <param name="HpPercent">同・残り HP 割合。</param>
    /// <param name="Notes">盤面ルールが実際にしたこと（<see cref="Map11Info.RuleNotes"/>）。</param>
    /// <param name="Turn">その戦闘が起きた作戦ターン（第174期。時間なしなら 0）。</param>
    /// <param name="Intercept">拠点の迎撃戦か（第174期）。</param>
    public sealed record Line(int No, string Squad, string Road, string Foe,
                              bool Won, int Alive, double HpPercent, IReadOnlyList<string> Notes,
                              int Turn, bool Intercept);

    public static Map11State? State { get; private set; }
    public static int Seed { get; private set; }
    public static List<Line> BattleLog { get; } = new();
    public static int Battles { get; private set; }

    /// <summary>いま戦闘シーンへ送り出している隊と区画。戻ってきた1回だけ精算する。</summary>
    public static int PendingSquad { get; private set; } = -1;
    public static Map11State.Node? PendingNode { get; private set; }
    /// <summary>いま送り出している戦闘が拠点の迎撃か（記録に印を付けるためだけ）。</summary>
    private static bool _pendingIntercept;

    public static string? LastOutcome { get; set; }

    public static bool Active => State is not null;
    public static bool HasPendingBattle => PendingNode is not null;

    /// <summary>新しい通しを始める。seed を渡さなければ毎回振り直す（結果画面に出す）。</summary>
    public static void Reset(int? seed = null)
    {
        Seed = seed ?? new Random().Next(0, 1_000_000);
        State = new Map11State(Seed, Map11.AdoptedTime);
        BattleLog.Clear();
        Battles = 0;
        PendingSquad = -1;
        PendingNode = null;
        _pendingIntercept = false;
        LastOutcome = null;
        ResetTurn();
    }

    public static void EnsureStarted()
    {
        if (State is null) Reset();
    }

    /// <summary>隊を道へ送る（プレイヤーの判断）。<b>記録は戦闘のたびに付ける</b>ので、ここでは残さない。</summary>
    public static void Send(int squad, int road)
    {
        if (State is null) return;
        if (State.Squads[squad].Lost) return;
        State.Send(squad, road);
    }

    /// <summary>
    /// 接敵。<b>持っている <c>UnitState</c> のリストをそのまま戦闘シーンへ渡す</b>
    /// ——`Materialize` はしない（傷も死者もそのまま持ち込む）。
    /// </summary>
    public static bool BeginBattle(int squad)
    {
        if (State is null || HasPendingBattle) return false;
        if (State.Prepare(squad) is not { } prep) return false;
        return Launch(squad, prep, intercept: false);
    }

    /// <summary>拠点の迎撃（第174期）。<b>道の上に出るわけではない</b>ので、専用の口を通す。</summary>
    private static bool BeginIntercept(int squad, Map11State.Node node)
    {
        if (State is null || HasPendingBattle) return false;
        if (State.PrepareIntercept(squad, node) is not { } prep) return false;
        return Launch(squad, prep, intercept: true);
    }

    private static bool Launch(int squad,
        (List<UnitState> Players, List<UnitState> Enemies, int Seed, Map11State.Node Node) prep,
        bool intercept)
    {
        PendingSquad = squad;
        PendingNode = prep.Node;
        _pendingIntercept = intercept;
        CampaignSession.BeginCarriedBattle(prep.Players, prep.Enemies, prep.Seed,
                                           prep.Node.Def.Name, prep.Node.Def.StageIndex);
        return true;
    }

    /// <summary>
    /// 戦闘から戻った1回だけ精算する（再生のやり直しでは二度走らない）。
    /// <paramref name="result"/> を渡すと、<b>盤面ルールが実際にしたこと</b>を記録に添える
    /// （第170期 §1-4）。<b>判定には1ビットも使わない——読むだけ。</b>
    /// </summary>
    public static void CompleteBattle(bool playerWon, BattleResult? result = null)
    {
        if (State is null || PendingNode is null) return;
        Map11State.Node node = PendingNode;
        int squad = PendingSquad;
        bool wasIntercept = _pendingIntercept;
        PendingNode = null;
        PendingSquad = -1;
        _pendingIntercept = false;

        Map11State.Squad sq = State.Squads[squad];
        string squadName = sq.Def.Name;
        int road = node.Def.Road;
        State.Resolve(squad, node, playerWon);
        Battles = State.Battles;

        var notes = result is null ? new List<string>() : Map11Info.RuleNotes(result);
        int maxHp = sq.Def.F.Occupied().Sum(o => o.Def.MaxHp);
        int alive = sq.Units?.Count(u => u.IsAlive) ?? 0;
        double hpPct = sq.Units is null || maxHp == 0
            ? 0 : 100.0 * sq.Units.Where(u => u.IsAlive).Sum(u => u.Hp) / maxHp;
        BattleLog.Add(new Line(Battles, squadName, Map11.RoadNames[road],
                               $"{node.Def.Index + 1}. {node.Def.Name}",
                               playerWon, alive, hpPct, notes,
                               State.Turn, wasIntercept));

        // **「抜けなかった」には2種類ある。** 全滅と、30 ターンで決着しない膠着。
        // 膠着は engine の上限ターン（`MaxTurns`）で、**同じ相手にもう一度当てても同じことが起きる**
        // ——判定は1つも足していないので、そう書いて引き返す判断をプレイヤーに返すだけ。
        LastOutcome = playerWon
            ? $"{squadName} が {node.Def.Name} を抜いた"
            : State.Squads[squad].Lost
                ? $"{squadName} は {node.Def.Name} に全滅した"
                : $"{squadName} は {node.Def.Name} と 30 ターン決着せず（両軍とも残っている）。"
                  + "もう一度当てても同じになる——引き返すか、別の隊を当てること";
    }

    // =================================================================================
    // 作戦ターン（第174期 段B2）
    //
    // **判定は1つも持たない。** 進む・戻る・休む・敵の前進・迎撃・陥落はすべて
    // `Map11State` にあり、ここがするのは**順番に呼んで、戦闘のところで止まること**だけ
    // ——戦闘はシーンを替えるので、1 作戦ターンの途中で中断して戻ってくる必要がある。
    // =================================================================================

    /// <summary>1 隊ぶんの命令（指示書 §2-1）。「組み直す」はその作戦ターンを使い切る（＝何もしない）。</summary>
    public enum Order { Wait, Advance, Back, Rest, Reform }

    /// <summary>作戦ターンを進めた結果。</summary>
    public enum StepKind { Done, Battle, Intercept, Fallen }

    public static Order[] Orders { get; private set; } = Array.Empty<Order>();
    /// <summary>「進む」で拠点から出るときの行き先（道の番号）。</summary>
    public static int[] OrderRoad { get; private set; } = Array.Empty<int>();

    /// <summary>いま作戦ターンの最中か（戦闘から戻ったら続きを流す）。</summary>
    public static bool TurnRunning { get; private set; }

    /// <summary>プレイヤーが迎撃に出す隊を選ぶ相手（null ＝ いま選ぶところではない）。</summary>
    public static Map11State.Node? InterceptNode { get; private set; }

    private static int _cursor;
    private static List<Map11State.Encounter>? _foes;
    private static int _foeIndex;

    private static void ResetTurn()
    {
        int n = Map11.Squads.Length;
        Orders = new Order[n];
        OrderRoad = Enumerable.Repeat(-1, n).ToArray();
        TurnRunning = false;
        InterceptNode = null;
        _cursor = 0;
        _foes = null;
        _foeIndex = 0;
    }

    public static void SetOrder(int squad, Order order, int road = -1)
    {
        if (squad < 0 || squad >= Orders.Length || TurnRunning) return;
        Orders[squad] = order;
        OrderRoad[squad] = road;
    }

    /// <summary>作戦ターンを始める。以後 <see cref="StepTurn"/> が <c>Done</c> を返すまで流す。</summary>
    public static void BeginTurn()
    {
        if (State is null || TurnRunning || State.Finished) return;
        TurnRunning = true;
        _cursor = 0;
        _foes = null;
        _foeIndex = 0;
    }

    /// <summary>
    /// 作戦ターンを1歩だけ進める。<b>戦闘に当たったらそこで止まって <c>Battle</c> を返す</b>
    /// ——呼ぶ側は戦闘シーンへ移り、戻ってきたらもう一度これを呼ぶ。
    /// </summary>
    public static StepKind StepTurn()
    {
        if (State is not { } st || !TurnRunning) return StepKind.Done;

        // ---- 1) 隊の行動（番号順に1つずつ） ----
        while (_cursor < st.Squads.Length)
        {
            int i = _cursor++;
            if (st.Finished) break;
            Map11State.Squad s = st.Squads[i];
            if (s.Lost) continue;
            switch (Orders[i])
            {
                case Order.Advance:
                    int road = s.Cell < 0 ? OrderRoad[i] : s.Road;
                    if (road < 0) break;
                    if (st.Advance(i, road) is { } node && BeginBattle(i)) return StepKind.Battle;
                    break;
                case Order.Back: st.StepBack(i); break;
                case Order.Rest: st.Rest(i); break;
            }
        }

        // ---- 2) 敵が動く ----
        _foes ??= st.AdvanceFoes();
        while (_foeIndex < _foes.Count && !st.Finished)
        {
            Map11State.Encounter e = _foes[_foeIndex];
            if (e.Intercept)
            {
                if (!e.Node.Cleared && st.NextInterceptor(e.Node) >= 0)
                {
                    InterceptNode = e.Node;
                    return StepKind.Intercept;
                }
                st.CloseIntercept(e.Node);
                InterceptNode = null;
                _foeIndex++;
                continue;
            }
            _foeIndex++;
            if (BeginBattle(e.Squad)) return StepKind.Battle;
        }

        TurnRunning = false;
        InterceptNode = null;
        _foes = null;
        _cursor = 0;
        // 次の作戦ターンの命令は「待つ」から始める（前のターンの命令を引きずらない）。
        for (int i = 0; i < Orders.Length; i++) { Orders[i] = Order.Wait; OrderRoad[i] = -1; }
        return st.Fallen ? StepKind.Fallen : StepKind.Done;
    }

    /// <summary>迎撃に出す隊を選んだ（プレイヤーの判断）。戦闘シーンへ移れたら true。</summary>
    public static bool ChooseInterceptor(int squad)
    {
        if (State is null || InterceptNode is not { } node) return false;
        if (!BeginIntercept(squad, node)) return false;
        return true;
    }

    /// <summary>その道の先頭の敵が、あと何作戦ターンで前進するか（時間なしなら null）。</summary>
    public static int? TurnsToAdvance(int road)
    {
        if (State is not { Time.On: true } st) return null;
        if (st.NextNode(road) is null) return null;
        int k = st.Time.AdvanceEvery;
        if (k <= 0) return null;
        return k - st.Turn % k;
    }

    /// <summary>盤上に残っている枚数（未出撃の隊は数えない）。</summary>
    public static int AliveOnMap =>
        State is null ? 0 : State.Squads.Sum(s => s.Units?.Count(u => u.IsAlive) ?? 0);

    /// <summary>まだ失っていない枚数（<b>未出撃の隊も数える</b>）。第169期に画面内で2通りに割れていた量。</summary>
    public static int AliveIncludingReserve =>
        State is null ? 0 : State.Squads.Sum(s =>
            s.Units is { } u ? u.Count(x => x.IsAlive)
            : s.Lost ? 0 : s.Def.F.Occupied().Count());
}
