namespace BattleCore;

/// <summary>列。前ほど狙われやすく、後ろは前が生きている間は狙われにくい。</summary>
public enum Row
{
    Front,
    Mid,
    Back
}

/// <summary>
/// 攻撃の届き方。隊列に意味を持たせる中核。
/// パターンは増やしても4つまでに留めること。1つ増えるたびに、
/// 庇う・標的・巻き込みなど既存の全特性との相互作用を監査する必要がある。
/// </summary>
public enum AttackPattern
{
    /// <summary>単体。庇う・標的の介入を受ける唯一のパターン。</summary>
    Single,
    /// <summary>薙ぎ。狙った敵と、その両隣（同じ列）にも当たる。</summary>
    Sweep,
    /// <summary>
    /// 貫き。レーン（縦一列）を前から後ろへ走り抜け、並んでいる敵すべてに当たる。
    /// 奥へ進むほど威力が落ちる。庇えず、標的にも釣られない。
    /// </summary>
    Pierce,
    /// <summary>全体。敵全員に当たる。</summary>
    All
}

/// <summary>ユニットの定義（不変データ）。カタログから読み込まれる想定。</summary>
public sealed class UnitDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int MaxHp { get; init; }
    public required int Attack { get; init; }
    public required int Speed { get; init; }

    /// <summary>付与されている特性のID。プラス・マイナスの区別は表示上のものでしかない。</summary>
    public required IReadOnlyList<TraitId> Traits { get; init; }

    public AttackPattern Pattern { get; init; } = AttackPattern.Single;

    /// <summary>編成画面で見せる説明文。</summary>
    public string PlusText { get; init; } = "";
    public string MinusText { get; init; } = "";
    public string Flavor { get; init; } = "";

    public override string ToString() => Name;
}

/// <summary>戦闘中のユニットの状態（可変）。</summary>
public sealed class UnitState
{
    public required UnitDef Def { get; init; }
    public required int TeamId { get; init; }

    /// <summary>
    /// この戦闘の中でだけ一意な連番。BattleContext.Add が振る。
    /// 胞子のように同じ UnitDef の駒が複数立つので、Def.Id では駒を指せない。
    /// 構造化イベント（BattleEvent）が「どの駒か」を指すための唯一の手段。
    /// </summary>
    public int InstanceId { get; internal set; }

    /// <summary>0..5。配置は FormationRules を参照。臆病などで戦闘中に変化する。</summary>
    public int Slot { get; set; }

    public int Hp { get; set; }
    public int MaxHp { get; set; }

    /// <summary>戦闘中に加算される攻撃力補正。バフ・デバフともにここへ入る。</summary>
    public int AtkBonus { get; set; }

    public IReadOnlyList<Trait> Traits { get; init; } = Array.Empty<Trait>();

    /// <summary>特性が自由に使えるカウンタ置き場。特性ごとにキーを分ける。</summary>
    public Dictionary<string, int> Counters { get; } = new();

    /// <summary>
    /// 戦闘中に一度でも後ろの列へ動かされたか。
    /// 「下がってから本領を発揮する」性質を、初期配置ではなく実績で判定するために使う。
    /// これが無いと、最初から後列に置くだけで代償を踏まずに後退後の性能が手に入る。
    /// </summary>
    public bool HasFallenBack { get; set; }

    public bool IsAlive => Hp > 0;
    public Row Row => FormationRules.RowOf(Slot);

    /// <summary>
    /// いま実際に使う攻撃パターン。定義上のパターンを特性が状況で書き換える。
    /// 参照は必ずこちらを使うこと。Def.Pattern を直接見ると状況変化が乗らない。
    /// </summary>
    public AttackPattern CurrentPattern
    {
        get
        {
            AttackPattern p = Def.Pattern;
            foreach (Trait t in Traits)
                p = t.ModifyPattern(this, p);
            return p;
        }
    }
    public string Name => Def.Name;

    /// <summary>支援・妨害を受け付けるか。受け付けない場合、バフもデバフも回復も通らない。</summary>
    public bool AcceptsSupport => !Traits.Any(t => t.BlocksSupport);

    public int CurrentAttack
    {
        get
        {
            int atk = Def.Attack + AtkBonus;
            foreach (Trait t in Traits)
                atk = t.ModifyAttack(this, atk);
            return Math.Max(0, atk);
        }
    }

    public bool HasTrait(TraitId id) => Traits.Any(t => t.Id == id);

    public int Counter(string key) => Counters.TryGetValue(key, out int v) ? v : 0;
    public void SetCounter(string key, int v) => Counters[key] = v;
}

/// <summary>
/// 盤面の形。前列3・中列1・後列2 の6枠。
///
///     後1(4)          前1(0)      レーン0 … 奥行き2
///     後2(5)  中(3)   前2(1)      レーン1 … 奥行き3
///                     前3(2)      レーン2 … 奥行き1
///
/// レーンごとに奥行きが違うのが要。深いレーンは減衰で守られ、浅いレーンは直撃を受ける。
/// 「同じ駒でも置き方で結果が変わる」を、貫きに対しても成立させるための形。
/// </summary>
public static class FormationRules
{
    public const int TotalSlots = 6;
    public const int LaneCount = 3;

    /// <summary>中列のスロット番号。盤面でただ一つしかない席。</summary>
    public const int MidSlot = 3;

    private static readonly Row[] RowTable =
        { Row.Front, Row.Front, Row.Front, Row.Mid, Row.Back, Row.Back };

    private static readonly int[] LaneTable = { 0, 1, 2, 1, 0, 1 };

    /// <summary>各レーンを前から後ろへ並べたもの。貫きの走査順そのもの。</summary>
    private static readonly int[][] LaneTracks =
    {
        new[] { 0, 4 },
        new[] { 1, 3, 5 },
        new[] { 2 }
    };

    public static Row RowOf(int slot) => RowTable[slot];
    public static int LaneOf(int slot) => LaneTable[slot];
    public static IReadOnlyList<int> LaneTrack(int lane) => LaneTracks[lane];

    /// <summary>前ほど小さい。押し出しと後退の向きを比べるために使う。</summary>
    public static int DepthOf(Row row) => row switch
    {
        Row.Front => 0,
        Row.Mid => 1,
        _ => 2
    };

    public static IEnumerable<int> SlotsOfRow(Row row)
    {
        for (int i = 0; i < TotalSlots; i++)
            if (RowTable[i] == row) yield return i;
    }

    /// <summary>同じ列で左右に並んでいるか。範囲攻撃が横へ広がる範囲。</summary>
    public static bool AreLateralNeighbors(int a, int b)
        => a != b && RowTable[a] == RowTable[b] && Math.Abs(LaneTable[a] - LaneTable[b]) == 1;

    /// <summary>同じレーンで前後に並んでいるか。</summary>
    public static bool AreDepthNeighbors(int a, int b)
    {
        if (a == b || LaneTable[a] != LaneTable[b]) return false;
        int[] track = LaneTracks[LaneTable[a]];
        return Math.Abs(Array.IndexOf(track, a) - Array.IndexOf(track, b)) == 1;
    }

    /// <summary>
    /// 隣接。左右と前後の両方を含む。
    ///
    /// 味方に及ぶもの（巻き込み・生贄・囃し立て・散開）は必ずこちらを見ること。
    /// 敵に及ぶもの（薙ぎの巻き込み・反撃の返し）は AreLateralNeighbors を見ること。
    /// この線引きを崩すと、範囲攻撃が縦へ広がって貫きと区別がつかなくなる。
    ///
    /// この定義により中列は前後2枠と接続する。通常攻撃からは守られるが、
    /// 味方のマイナスは一身に浴びる席になる。「隣接デメリットの捨て場」を作らないための措置。
    /// </summary>
    public static bool AreAdjacent(int a, int b)
        => AreLateralNeighbors(a, b) || AreDepthNeighbors(a, b);
}

/// <summary>編成。スロットに UnitDef を入れる。null は空きスロット。</summary>
public sealed class Formation
{
    private readonly UnitDef?[] _slots = new UnitDef?[FormationRules.TotalSlots];

    public UnitDef? this[int slot]
    {
        get => _slots[slot];
        set => _slots[slot] = value;
    }

    public int Count => _slots.Count(s => s is not null);

    public IEnumerable<(int Slot, UnitDef Def)> Occupied()
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] is { } d)
                yield return (i, d);
    }

    public Formation Clone()
    {
        var f = new Formation();
        for (int i = 0; i < _slots.Length; i++) f[i] = _slots[i];
        return f;
    }

    /// <summary>
    /// スロットを名前で指定して編成を作る。
    /// front1..front3 → スロット0..2（前列）、mid → スロット3（中列）、back1..back2 → スロット4..5（後列）。
    ///
    /// 旧 Of(params) は引数の並びとスロット番号の対応が暗黙で、
    /// 盤面の形が変わったときに黙って別物の編成になった（5枠→6枠で後列1枚目が全部中列に落ちた）。
    /// 編成定義では必ずこちらを使うこと。
    /// </summary>
    public static Formation Build(
        UnitDef? front1 = null, UnitDef? front2 = null, UnitDef? front3 = null,
        UnitDef? mid = null,
        UnitDef? back1 = null, UnitDef? back2 = null)
    {
        var f = new Formation();
        f[0] = front1;
        f[1] = front2;
        f[2] = front3;
        f[3] = mid;
        f[4] = back1;
        f[5] = back2;
        return f;
    }
}

/// <summary>ログ行の種類。UI はこれを見て色を決める。文字列を解析させないための型。</summary>
public enum LogKind
{
    System,        // 開始・終了
    Turn,          // ターン区切り
    Action,        // 通常の行動
    Damage,        // 敵への与ダメージ
    FriendlyFire,  // 味方への事故
    Status,        // 毒などの継続効果
    Trigger,       // 特性の発動
    Highlight,     // 見せ場（覚醒・破裂）
    Summon,        // 増援・蘇生
    Death          // 撃破
}

public sealed record LogLine(LogKind Kind, int Indent, string Text)
{
    public override string ToString() => new string(' ', Indent * 2) + Text;
}

/// <summary>
/// 1体の駒が1戦で何をしたかの集計。
///
/// ログや BattleEvent と違い <b>verbose に関係なく数える</b>。
/// 200 seed × 全ステージの一括シミュレーションで平均を取るのが用途なので、
/// ここを verbose で切ると測りたいときに測れない。
/// 盤面には一切影響しない（ただの足し算）。
/// </summary>
public sealed class UnitTally
{
    /// <summary>
    /// 攻撃を振った回数（<c>PerformAttack</c> を通った回数）。
    /// 反撃（棘）はここを通らない。反撃は <c>ApplyDamage</c> を直接呼ぶので
    /// <see cref="Interventions"/> の側に出る。この2つのズレ自体が情報になる
    /// （振らないのに干渉する＝反応型、振るのに干渉しない＝空振り）。
    /// </summary>
    public int Attacks;

    /// <summary>
    /// 実際にダメージを通した回数。攻撃・反撃・破裂・毒のどれでも、
    /// この駒が起点になって盤面が動いた回数を数える。**これが活動量の本体。**
    /// </summary>
    public int Interventions;

    /// <summary>敵に与えたダメージ。</summary>
    public int DamageToEnemy;

    /// <summary>味方に与えたダメージ。破裂・生贄・吸いはここに出る。</summary>
    public int DamageToAlly;

    /// <summary>受けたダメージ（敵味方を問わない）。</summary>
    public int DamageTaken;

    /// <summary>受けたダメージのうち味方由来のぶん。</summary>
    public int TakenFromAlly;

    /// <summary>
    /// 回復で実際に増えた HP（<c>ctx.Heal</c> が動かした分だけ。上限で切られた分は入らない）。
    /// 代金の分解（第9期 bill）が「払った HP」から差し引くために足したもので、
    /// <b>既存の出力には出さない</b>——pulse の表に列を増やすと第8期以前の出力と diff が出る。
    /// </summary>
    public int Healed;

    /// <summary>とどめを刺した敵の数。</summary>
    public int Kills;

    /// <summary>倒れた回数。蘇生されて再度倒れると2になる。</summary>
    public int Deaths;

    public void Add(UnitTally o)
    {
        Attacks += o.Attacks; Interventions += o.Interventions;
        DamageToEnemy += o.DamageToEnemy; DamageToAlly += o.DamageToAlly;
        DamageTaken += o.DamageTaken; TakenFromAlly += o.TakenFromAlly;
        Healed += o.Healed;
        Kills += o.Kills; Deaths += o.Deaths;
    }
}

/// <summary>
/// 構造化された戦闘イベントの種類。
///
/// LogLine（人が読む文字列）と対になる、機械が読む側の記録。
/// 戦闘画面は「誰が誰に何をしたか」を必要とするが、文字列からは復元できないので分けてある。
/// **文字列を解析して画面を作ってはいけない**（LogKind の原則と同じ）。
/// </summary>
public enum BattleEventKind
{
    TurnStart,   // ターンの区切り
    Attack,      // 振った（当たったかどうかとは別）
    Damage,      // 実際に減った
    Heal,
    Death,
    Summon,      // 新しい駒が盤面に出た
    Revive,      // 倒れていた駒が戻った
    Move,        // スロットが変わった
    Status,      // 毒・燃焼・痺れなどの継続効果が「働いた」（そのターン実際に削った量）
    Highlight,   // 見せ場（覚醒・破裂）。演出を差し込む位置の指示

    /// <summary>
    /// ターン開始時点で駒が負っている継続効果の「残量」。<see cref="Status"/> とは意味が違う
    /// （あちらは働いた記録、こちらは今いくつ乗っているか）ので種類を分けてある。
    ///
    /// 状態異常のカウンタは16箇所から書かれていて、書き込み側すべてに通知を挟むと
    /// Traits.cs を広く触ることになる（バランスが載っている場所なので触りたくない）。
    /// 継続効果はターン開始時にまとめて処理されるので、そこで1回スナップショットを撮れば足りる。
    /// **ターン中に積まれたぶんは次のターンの頭まで出ない。** 効き始めるのもそのときなので、
    /// 表示としてはむしろ揃っている。
    /// </summary>
    StatusSnapshot,

    /// <summary>
    /// ターン開始時点の攻撃力（<see cref="UnitState.CurrentAttack"/>）。
    /// 積み上げ系（墓守の三角数、溜め、被弾強化）は素の値から大きく離れるので、
    /// 素の値だけ見せると盤面で何が起きているか読めない。
    /// <see cref="StatusSnapshot"/> と同じ理由で、ターン頭に1回だけ写す。
    /// </summary>
    StatSnapshot
}

/// <summary>
/// 戦闘中に起きた一つの出来事。時間順に並んだこの列が、そのまま再生用の台本になる。
///
/// BattleEngine.Run は seed 決定的な純関数で戦闘を丸ごと計算し切るので、
/// 戦闘画面はリアルタイムのシミュレーションではなく**この列の再生**として書ける。
/// </summary>
public sealed class BattleEvent
{
    public required BattleEventKind Kind { get; init; }
    public required int Turn { get; init; }

    /// <summary>行為者の InstanceId。盤面全体に関わる出来事では null。</summary>
    public int? ActorId { get; init; }

    /// <summary>対象の InstanceId。</summary>
    public int? TargetId { get; init; }

    /// <summary>ダメージ量・回復量など。種類によって意味が変わる。</summary>
    public int Amount { get; init; }

    /// <summary>対象のこの出来事の直後のHP。バーの補間に使う。</summary>
    public int HpAfter { get; init; }

    /// <summary>味方への巻き込みか。色を変えるため。</summary>
    public bool FriendlyFire { get; init; }

    /// <summary>Move / Summon の行き先スロット。</summary>
    public int Slot { get; init; }

    /// <summary>
    /// Summon で新しく出た駒の陣営。
    /// 増援は初期盤面に載っていないので、再生側はこのイベントだけで駒を組み立てる必要がある。
    /// </summary>
    public int? Team { get; init; }

    /// <summary>Attack のときに実際に使われたパターン。</summary>
    public AttackPattern? Pattern { get; init; }

    /// <summary>Highlight / Status のフレーバー。演出の中身ではなく添え物として扱う。</summary>
    public string? Text { get; init; }
}

/// <summary>戦闘結果。UIはこれを表示するだけでよい。</summary>
public sealed class BattleResult
{
    public required bool PlayerWon { get; init; }
    public required int Turns { get; init; }
    public required IReadOnlyList<LogLine> Log { get; init; }

    /// <summary>味方の生存数。バランス調整の指標として使う。</summary>
    public required int PlayerSurvivors { get; init; }

    /// <summary>ユニットIDごとの与ダメージ合計。誰が働いたかを機械的に見るため。</summary>
    public required IReadOnlyDictionary<string, int> DamageByUnit { get; init; }

    /// <summary>
    /// ユニットIDごとの働きの内訳。「勝ったかどうか」ではなく「誰が何をしたか」を見る。
    /// 与ダメージだけだと、蘇生・萎縮のような出力を持たない駒が全員ゼロに潰れて
    /// 区別がつかない（`docs/pulse.md`）。
    /// </summary>
    public required IReadOnlyDictionary<string, UnitTally> TallyByUnit { get; init; }

    /// <summary>1ターンのうちに味方が倒した敵の数の最大値。「連鎖の深さ」の代理指標。</summary>
    public required int MaxEnemyKillsInOneTurn { get; init; }

    /// <summary>
    /// 構造化された出来事の列。再生用の台本。
    /// Log と同じく verbose=false のときは空（一括シミュレーションで積むと遅くなるため）。
    /// </summary>
    public required IReadOnlyList<BattleEvent> Events { get; init; }
}
