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
    /// <param name="AllyShock">第467期 §3: この戦闘で味方の雷・粉で倒れた駒の枚数（1 章だけ数える・<see cref="Map11Info.AllyShockDowns"/>・単発は 0）。</param>
    public sealed record Line(int No, string Squad, string Road, string Foe,
                              bool Won, int Alive, double HpPercent, IReadOnlyList<string> Notes,
                              int Turn, bool Intercept, int AllyShock = 0);

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

    /// <summary>
    /// 開始時の編成（第177期 §3）。<b>最初の作戦ターンが始まった瞬間に1度だけ写す</b>
    /// ——後で組み直しても、ここは「何で始めたか」を持ち続ける。
    /// 結果画面と <c>MAP11_LOG</c> に出し、<b>次の期の材料にする</b>（誰と誰を組んだか）。
    /// </summary>
    public static List<string> OpeningLines { get; } = new();

    /// <summary>まだ最初の作戦ターンを始めていない（＝手札から組んでいる最中）。</summary>
    public static bool Drafting { get; private set; }

    /// <summary>新しい通しを始める。seed を渡さなければ毎回振り直す（結果画面に出す）。</summary>
    /// <param name="draft">
    /// <b>自分で 15 枚から組むか</b>（第177期 §3・既定）。偽にすると第176期までと同じ
    /// 「既定の3隊で始める」形になる。
    /// </param>
    /// <param name="stage">ステージ（第440期）。<b>省けば 1-1</b>——器具と門はすべてこの既定で回る。</param>
    public static void Reset(int? seed = null, bool draft = true, MapStage? stage = null)
    {
        Seed = seed ?? new Random().Next(0, 1_000_000);
        RelicWorked.Clear();   // 第454期
        stage ??= MapStage.Stage11;
        // 第177期 §1: **敵の拠点（ワープポータル）を遊ぶ側に載せた。**
        // 第176期は `PortalRule.Off` のままで、起動して遊べるものは第175期と同じだった。
        // 第440期: 時間と拠点の規則は**ステージが持つ**（1-1 は `Map11.AdoptedTime` ／ `AdoptedPortal` のまま）。
        State = new Map11State(Seed, stage.Time, stage.Portal, draft, stage: stage);
        Drafting = draft;
        OpeningLines.Clear();
        BattleLog.Clear();
        Battles = 0;
        PendingSquad = -1;
        PendingNode = null;
        _pendingIntercept = false;
        LastOutcome = null;
        ResetTurn();
    }

    /// <summary>
    /// 編成画面で選んだステージ（第440期 §3-2・既定 1-1）。<b>選び直すと、次に作戦マップへ入ったときに通しを始め直す</b>
    /// （<see cref="EnsureStarted"/>）——同じステージのままなら、戦闘から戻っても通しは続く。
    /// </summary>
    public static MapStage Chosen { get; set; } = MapStage.Stage11;

    public static void EnsureStarted()
    {
        // 第460期 §A-1: 「1 章」を選んでいれば 1 章の通し（同じステージを遊んでいる間は続ける・違えば 1-1 から始める）。
        if (ChapterPicked)
        {
            // 第466期 §2: 「1 章（写しから）」は写しから始めた 1 章を続ける（無ければいちばん新しい写しから始め、画面が一覧を出す）。
            if (Chapter1.Active && State is not null && ReferenceEquals(State.Stage, Chapter1.CurrentStage) && (Chapter1.From is not null) == SnapPicked) return;
            if (SnapPicked && PlayLog.Starts().FirstOrDefault() is { } newest)
            {
                Chapter1.BeginFrom(newest, snapRule: false);
                SnapChoicePending = true;
                return;
            }
            Chapter1.Begin();
            return;
        }
        if (Chapter1.Active) Chapter1.End();
        if (State is null || !ReferenceEquals(State.Stage, Chosen)) Reset(stage: Chosen);
    }

    /// <summary>
    /// 編成画面のステージの札で「1 章」を選んでいるか（第460期 §A-1）。札の回り方は 1-1 → 1-2 → 1-3 → 1 章 → 1-1（<see cref="CyclePick"/>）。
    /// 単発の 1-1 ／ 1-2 ／ 1-3 は <see cref="Chosen"/> のまま。
    /// </summary>
    public static bool ChapterPicked { get; private set; }

    /// <summary>
    /// 「1 章（写しから）」を選んでいるか（第466期 §2）。第468期からは札では回らない——「1 章」の最初の窓の「写しから始める」で写しを選ぶと真（<see cref="PickChapterFrom"/>）。
    /// <b>写しを書く起動（<see cref="PlayLog.Enabled"/>）で、写しが 1 つ以上あるときだけ</b>回る（頭なしの門の回り方は第460期のまま）。
    /// </summary>
    public static bool SnapPicked { get; private set; }

    /// <summary>写しの一覧を出す（第466期・作戦の画面が読んで一覧の窓を開け、選ぶか閉じるかで偽に戻す）。</summary>
    public static bool SnapChoicePending { get; set; }

    /// <summary>「1 章」を選んで 1 章を始める（第460期・絵の口 `--map-chapter=N` だけが使う・遊ぶ起動は札の <see cref="CyclePick"/>）。</summary>
    public static void PickChapter(int? seed = null)
    {
        ChapterPicked = true;
        Chapter1.Begin(seed);
    }

    /// <summary>
    /// 札を「1 章（写しから）」にする（第466期・絵の口 `--map-snap-picker` だけが使う・遊ぶ起動は札の <see cref="CyclePick"/>）。
    /// <paramref name="root"/> の写しを一覧に出す（写しは書かない——読む置き場だけを決める）。
    /// </summary>
    public static void PickSnapList(string root)
    {
        PlayLog.UseForReading(root);
        ChapterPicked = true;
        SnapPicked = true;
        if (Chapter1.Active) Chapter1.End();
    }

    /// <summary>写しを選んで 1 章を始める（第466期 §2・一覧の窓と口 `--map-chapter-from=` が使う）。<paramref name="snapRule"/> が偽なら今の版。</summary>
    public static void PickChapterFrom(PlayLog.Snap snap, bool snapRule)
    {
        ChapterPicked = true;
        SnapPicked = true;
        SnapChoicePending = false;
        Chapter1.BeginFrom(snap, snapRule);
    }

    /// <summary>ステージの札の文（第460期・「マップ 1-2」／「1 章」）。</summary>
    public static string PickText => SnapPicked ? "1 章（写しから）" : ChapterPicked ? "1 章" : $"マップ {Chosen.Id}";

    /// <summary>ステージの札を 1 つ進める（第460期 §A-1・1-1 → 1-2 → 1-3 → 1 章 → 1-1）。</summary>
    public static void CyclePick()
    {
        var all = MapStage.All;
        // 第468期 §2-3: 5 番目の「1 章（写しから）」は外した（写しからは「1 章」の最初の窓の「写しから始める」）。回り方は第460期のまま。
        if (ChapterPicked) { ChapterPicked = false; SnapPicked = false; Chosen = all[0]; return; }
        int at = 0;
        for (int i = 0; i < all.Count; i++) if (ReferenceEquals(all[i], Chosen)) at = i;
        if (at == all.Count - 1) { ChapterPicked = true; return; }
        Chosen = all[at + 1];
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
    /// <param name="against">
    /// 当たる相手（<c>null</c> なら隊の位置から引く）。<b>第176期に足した</b>
    /// ——敵の拠点では「道の先頭の敵」と「いま同じマスにいる敵」が別物になりうるので、
    /// 命令を実行した側が見つけた相手をそのまま渡す。
    /// </param>
    public static bool BeginBattle(int squad, Map11State.Node? against = null)
    {
        if (State is null || HasPendingBattle) return false;
        if (State.Prepare(squad, against) is not { } prep) return false;
        return Launch(squad, prep, intercept: false);
    }

    /// <summary>拠点の迎撃（第174期）。<b>道の上に出るわけではない</b>ので、専用の口を通す（第455期: 絵の口 `hero,win` も通す）。</summary>
    internal static bool BeginIntercept(int squad, Map11State.Node node)
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
        string squadName = sq.Label;
        int road = node.Def.Road;
        // 第175期 §2: **迎撃で勝っても道中の回復は乗らない**（規則は `Map11State` が持つ）。
        int delivered = State.Deliveries.Count;
        int golds = State.GoldLog.Count;
        var unitsBefore = sq.Units?.ToList() ?? new List<UnitState>();   // 第454期: 札の持ち主を台本の番号で引くため（精算で倒れた駒は隊から外れる）
        State.Resolve(squad, node, playerWon, wasIntercept);
        Battles = State.Battles;
        if (wasIntercept && TurnRunning) _interceptFought = true;
        // 第457期: 本陣で勝って、まだそこに敵がいれば連戦の合間（次の `StepTurn` が `Siege` で止まる）。
        if (!wasIntercept && TurnRunning && playerWon && State.SiegeNext(squad) is { } next)
        {
            SiegeSquad = squad;
            SiegeNode = next;
            _siegeShown = false;
        }

        var notes = result is null ? new List<string>() : Map11Info.RuleNotes(result);
        // 第454期 §1: レリックが働いた回数（台本の `BattleEventKind.Relic`・表示だけ）。駒は戦闘の前の隊の駒（InstanceId はこの戦闘で振られた番号）。
        if (result is not null && RelicNotes(result, unitsBefore) is { Count: > 0 } relicNotes) notes.AddRange(relicNotes);
        // 第445期 §0-3: 分母は**いまのロスターの最大 HP**（画面の `SquadHpPercent` と同じ・第174期）。
        // 第444期までは隊の定義（カド隊など）の 5 枚で割っていたので、自分で組んだ隊では 100% を超えることがあった。表示だけ。
        int maxHp = sq.Units is { } roster ? roster.Sum(u => u.MaxHp) : sq.Def.F.Occupied().Sum(o => o.Def.MaxHp);
        int alive = sq.Units?.Count(u => u.IsAlive) ?? 0;
        double hpPct = sq.Units is null || maxHp == 0
            ? 0 : 100.0 * sq.Units.Where(u => u.IsAlive).Sum(u => u.Hp) / maxHp;
        // 第459期 §2: 記録の作戦ターンは画面と同じ数（`ShownTurn`・1 から）。時間なしの通しは 0（出さない）のまま。
        // 第467期 §3: 味方の雷・粉で倒れた枚数（1 章だけ・台本のある戦闘だけ——単発の記録は第466期のまま）。
        int allyShock = Chapter1.Active && result is not null
            ? Map11Info.AllyShockDowns(result, unitsBefore.Select(u => u.InstanceId).ToHashSet()) : 0;
        BattleLog.Add(new Line(Battles, squadName, State.LaneName(road),
                               $"{node.Def.Index + 1}. {node.Def.Name}",
                               playerWon, alive, hpPct, notes,
                               State.Time.On ? ShownTurn : 0, wasIntercept, allyShock));

        // **「抜けなかった」には2種類ある。** 全滅と、30 ターンで決着しない膠着。
        // 膠着は engine の上限ターン（`MaxTurns`）で、**同じ相手にもう一度当てても同じことが起きる**
        // ——判定は1つも足していないので、そう書いて引き返す判断をプレイヤーに返すだけ。
        LastOutcome = playerWon
            ? $"{squadName} が {node.Def.Name} を抜いた"
            : State.Squads[squad].Lost
                ? $"{squadName} は {node.Def.Name} に全滅した"
                : $"{squadName} は {node.Def.Name} と 30 ターン決着せず（両軍とも残っている）。"
                  + "もう一度当てても同じになる——引き返すか、別の隊を当てること";
        // 第444期 §0-2: この戦闘で届いた見返り（抜いた・中間拠点を取った）を同じトーストに添える。
        if (DeliveryText(State.Deliveries.Skip(delivered)) is { } got) LastOutcome += " ／ " + got;
        // 第445期 §0-1: この戦闘で動いた金（「＋6（第三波を抜いた）」）。
        if (GoldText(State.GoldLog.Skip(golds)) is { } gold) LastOutcome += " ／ " + gold;
    }

    /// <summary>このマップでレリックが働いた回数（第454期・駒の名前と札 → 回数・結果画面が読む・表示だけ）。</summary>
    public static Dictionary<(string Unit, TraitId Relic), int> RelicWorked { get; } = new();

    /// <summary>1 戦の台本からレリックが働いた回数を数え、「◆札（駒）: n 回」の行にする（<see cref="RelicWorked"/> にも足す）。</summary>
    internal static List<string> RelicNotes(BattleResult result, List<UnitState> squad)
    {
        var lines = new List<string>();
        foreach (var g in result.Events.Where(e => e.Kind == BattleEventKind.Relic && e.ActorId is not null)
                                       .GroupBy(e => (e.ActorId!.Value, e.Text)))
        {
            UnitState? u = squad.FirstOrDefault(x => x.InstanceId == g.Key.Item1);
            if (u is null || !Enum.TryParse(g.Key.Item2, out TraitId relic) || !RelicCatalog.IsRelic(relic)) continue;
            string name = RelicCatalog.Info(relic).Name;
            lines.Add($"◆{name}（{u.Def.Name}）: {g.Count()} 回");
            RelicWorked[(u.Def.Name, relic)] = RelicWorked.GetValueOrDefault((u.Def.Name, relic)) + g.Count();
        }
        // 第455期 §1-1: 持たせた札は 0 回でも出す（働かない札が読める）。
        foreach (UnitState u in squad.Where(x => x.Relic is TraitId r && RelicCatalog.IsRelic(r)))
        {
            TraitId relic = u.Relic!.Value;
            if (result.Events.Any(e => e.Kind == BattleEventKind.Relic && e.ActorId == u.InstanceId && e.Text == relic.ToString())) continue;
            lines.Add($"◆{RelicCatalog.Info(relic).Name}（{u.Def.Name}）: 0 回");
            RelicWorked.TryAdd((u.Def.Name, relic), 0);
        }
        return lines;
    }

    /// <summary>金の出入りの 1 文（第445期）。「＋6（第三波を抜いた）・＋8（北の砦を取った）」。無ければ null。</summary>
    public static string? GoldText(IEnumerable<Map11State.GoldEvent> events)
    {
        var parts = events.Select(e => $"{(e.Amount >= 0 ? "＋" : "−")}{Math.Abs(e.Amount)}（{e.Reason}）").ToList();
        return parts.Count == 0 ? null : string.Join("・", parts);
    }

    /// <summary>
    /// 見返りで届いた駒の 1 文（第444期 §0-2）。届く先と理由ごとにまとめる——
    /// 「ヒヨ・ドルガ が 北の砦 に着いた（見返り: 北の砦を取った）」。届いていなければ null。
    /// </summary>
    public static string? DeliveryText(IEnumerable<Map11State.Delivery> got)
    {
        var parts = got.GroupBy(d => (d.Place, d.Reason))
            .Select(g => $"{string.Join("・", g.Select(d => d.Unit.Def.Name))} が {g.Key.Place} に着いた（見返り: {g.Key.Reason}）")
            .ToList();
        return parts.Count == 0 ? null : string.Join(" ／ ", parts);
    }

    // =================================================================================
    // 作戦ターン（第174期 段B2）
    //
    // **判定は1つも持たない。** 進む・戻る・休む・敵の前進・迎撃・陥落はすべて
    // `Map11State` にあり、ここがするのは**順番に呼んで、戦闘のところで止まること**だけ
    // ——戦闘はシーンを替えるので、1 作戦ターンの途中で中断して戻ってくる必要がある。
    // =================================================================================

    /// <summary>作戦ターンを進めた結果。</summary>
    /// <remarks>第457期: <c>Siege</c> ＝ 本陣の連戦の合間（<see cref="SiegeNode"/>・続けて戦うか引くかを選ぶ）。</remarks>
    public enum StepKind { Done, Battle, Intercept, Fallen, Siege }

    /// <summary>
    /// 隊ごとの<b>行き先</b>（第175期 §1-1）。<b>第174期の「毎ターン置く命令」を置き換えた。</b>
    /// 命令は毎ターン <see cref="Map11Orders.Plan"/> が行き先から作るので、ここには持たない。
    /// <b>既定は拠点</b>（＝待つ）——一度行き先を決めた隊は、変えるまでそこへ向かい続ける。
    /// </summary>
    public static Dest[] Destinations { get; private set; } = Array.Empty<Dest>();

    /// <summary>いま作戦ターンの最中か（戦闘から戻ったら続きを流す）。</summary>
    public static bool TurnRunning { get; private set; }

    /// <summary>プレイヤーが迎撃に出す隊を選ぶ相手（null ＝ いま選ぶところではない）。</summary>
    public static Map11State.Node? InterceptNode { get; private set; }

    /// <summary>
    /// <b>迎撃の合間</b>（第443期 §0-1）。この作戦ターンに拠点の迎撃を 1 戦以上済ませ、<b>次の迎撃の相手を待っている</b>。
    /// このあいだは時計が止まったまま拠点で組み直せる（届いた駒を入れる・席を替える・迎撃に出す隊を替える）。
    /// <b>休むは使えない</b>（命令は隊の行動の段でしか実行されない・時間は進んでいない）。
    /// 組み直しそのものは <see cref="Map11State.CanReform"/>（拠点にいる隊）の規則のまま——ここは「いまが合間か」を言うだけ。
    /// 器具（`Map11Time` ／ `Map11Portal` ／ `Map12ChangeSmoke` の方針）はこの窓で何もしない。
    /// </summary>
    public static bool InterceptBreak => TurnRunning && InterceptNode is not null && _interceptFought;

    /// <summary>この作戦ターンに迎撃を 1 戦以上済ませたか（<see cref="InterceptBreak"/> の材料）。</summary>
    private static bool _interceptFought;

    /// <summary>本陣の連戦の合間で、次に当たる相手（第457期・null ＝ 合間ではない）。</summary>
    public static Map11State.Node? SiegeNode { get; private set; }
    /// <summary>本陣の連戦の合間にいる隊（-1 ＝ 合間ではない）。</summary>
    public static int SiegeSquad { get; private set; } = -1;
    private static bool _siegeShown;

    /// <summary>連戦の合間で「続けて戦う」（第457期）。戦闘を用意できたら true（呼ぶ側は接敵の窓へ）。</summary>
    public static bool ContinueSiege()
    {
        if (SiegeNode is not { } node || SiegeSquad < 0) return false;
        int squad = SiegeSquad;
        ClearSiege();
        return BeginBattle(squad, node);
    }

    /// <summary>連戦の合間で「引く」（第457期）。隊を入ってきた経路の中間拠点へ戻す（呼ぶ側は作戦ターンの続きを流す）。</summary>
    public static bool RetreatSiege()
    {
        if (State is null || SiegeSquad < 0) return false;
        int squad = SiegeSquad;
        ClearSiege();
        return State.SiegeRetreat(squad);
    }

    private static void ClearSiege() { SiegeNode = null; SiegeSquad = -1; _siegeShown = false; }

    /// <summary>いま流している作戦ターンの番号（第459期 §2・<see cref="BeginTurn"/> が写す・0 ＝ まだ 1 度も流していない）。</summary>
    private static int _playing;

    /// <summary>
    /// <b>画面に出す作戦ターン</b>（第459期 §2・1 から・<b>表示だけ</b>）。上の帯・時計・戦闘の記録はこの 1 つの数を読む。
    /// 流しているあいだと終わった後は<b>流している（流した）作戦ターン</b>、止まっているあいだは<b>次に流す作戦ターン</b>（<c>Turn + 1</c>）。
    /// <para><c>Map11State.Turn</c> は「敵が動いた回数」で、作戦ターンの途中（敵の前進）で 1 増える——第458期までは帯が <c>Turn + 1</c>、時計と記録が
    /// <c>Turn</c> を出していたので、同じ時に 1 ずれた数が並んでいた（観察ログ 458 (6)）。規則はこの数を読まない。</para>
    /// </summary>
    public static int ShownTurn
        => State is null ? 1 : (TurnRunning || State.Finished) && _playing > 0 ? _playing : State.Turn + 1;

    private static int _cursor;
    private static List<Map11State.Encounter>? _foes;
    private static int _foeIndex;

    private static void ResetTurn()
    {
        int n = State?.Squads.Length ?? Map11.Squads.Length;  // 第442期: 隊の数はステージが持つ
        Destinations = Enumerable.Repeat(Dest.Home, n).ToArray();
        TurnRunning = false;
        InterceptNode = null;
        _cursor = 0;
        _foes = null;
        _foeIndex = 0;
        _interceptFought = false;
        _playing = 0;
        ClearSiege();
    }

    /// <summary>行き先を決める（プレイヤーの判断）。<b>盤面は1ビットも動かない。</b></summary>
    public static void SetDestination(int squad, Dest dest)
    {
        if (squad < 0 || squad >= Destinations.Length || TurnRunning) return;
        Destinations[squad] = dest;
    }

    /// <summary>その隊がこの作戦ターンに何をするか（画面に出す1語）。</summary>
    public static string OrderText(int squad)
        => State is null ? "" : Map11Orders.Describe(State, squad, Destinations[squad]);

    /// <summary>作戦ターンを始める。以後 <see cref="StepTurn"/> が <c>Done</c> を返すまで流す。</summary>
    public static void BeginTurn()
    {
        if (State is null || TurnRunning || State.Finished) return;
        TakeOpening();
        TurnRunning = true;
        _playing = State.Turn + 1;   // 第459期 §2: この作戦ターンの番号（表示だけ）
        _cursor = 0;
        _foes = null;
        _foeIndex = 0;
        _interceptFought = false;
    }

    /// <summary>
    /// 作戦ターンを1歩だけ進める。<b>戦闘に当たったらそこで止まって <c>Battle</c> を返す</b>
    /// ——呼ぶ側は戦闘シーンへ移り、戻ってきたらもう一度これを呼ぶ。
    /// </summary>
    public static StepKind StepTurn()
    {
        if (State is not { } st || !TurnRunning) return StepKind.Done;

        // ---- 0) 第457期: 本陣の連戦の合間。1 度目は止まって窓を出す。答えずにもう一度呼ばれたら「続けて戦う」（器具と頭なしの通しのため） ----
        if (SiegeNode is not null)
        {
            if (!_siegeShown) { _siegeShown = true; return StepKind.Siege; }
            if (ContinueSiege()) return StepKind.Battle;
        }

        // ---- 1) 隊の行動（番号順に1つずつ） ----
        while (_cursor < st.Squads.Length)
        {
            int i = _cursor++;
            if (st.Finished) break;
            Map11State.Squad s = st.Squads[i];
            if (s.Lost) continue;
            // 第175期: **命令は行き先から作る**（規則は `Map11Orders` の1本・器具と共有）。
            Dest dest = Destinations[i];
            Map11Orders.Order order = Map11Orders.Plan(st, i, dest);
            if (Map11Orders.Apply(st, i, dest, order) is { } met && BeginBattle(i, met))
                return StepKind.Battle;
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
            if (BeginBattle(e.Squad, e.Node)) return StepKind.Battle;
        }

        TurnRunning = false;
        InterceptNode = null;
        _interceptFought = false;
        ClearSiege();
        _foes = null;
        _cursor = 0;
        // **行き先は引きずる**（第175期 §1-1「一度でも出した隊は、行き先を変えるまで前へ進み続ける」）
        // ——第174期はここで命令を「待つ」へ戻していた。それが「何も押さないことが有効な手」の正体だった。
        return st.Fallen ? StepKind.Fallen : StepKind.Done;
    }

    /// <summary>迎撃に出す隊を選んだ（プレイヤーの判断）。戦闘シーンへ移れたら true。</summary>
    public static bool ChooseInterceptor(int squad)
    {
        if (State is null || InterceptNode is not { } node) return false;
        if (!BeginIntercept(squad, node)) return false;
        return true;
    }

    /// <summary>
    /// 開始時の編成を1度だけ写す（第177期 §3）。<b>最初の作戦ターンの直前</b>
    /// ——組み直しは作戦ターンを使わないので、「進める」を押すまでは何度でも組み替えられる。
    /// </summary>
    public static void TakeOpening()
    {
        if (State is not { } st || OpeningLines.Count > 0) return;
        Drafting = false;
        PlayLog.OnFirst(st);   // 第466期: 1 章の最初の作戦ターンの直前の写し（書くだけ）
        foreach (Map11State.Squad s in st.Squads)
        {
            string body = s.Units is { Count: > 0 } u
                ? string.Join(" / ", u.OrderBy(x => x.Slot)
                    .Select(x => $"{FormationRules.SeatNames[x.Slot]} {x.Def.Name}"))
                : s.Units is null
                    ? string.Join(" / ", s.Def.F.Occupied()
                        .Select(o => $"{FormationRules.SeatNames[o.Slot]} {o.Def.Name}"))
                    : "（空）";
            OpeningLines.Add($"{s.Label}: {body}");
        }
        if (st.Bench.Count > 0)
            OpeningLines.Add("余り: " + string.Join(" / ", st.Bench.Select(x => x.Def.Name)));
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
