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

    /// <summary>0..5。配置は FormationRules を参照。臆病などで戦闘中に変化する。</summary>
    public int Slot { get; set; }

    public int Hp { get; set; }
    public int MaxHp { get; set; }

    /// <summary>戦闘中に加算される攻撃力補正。バフ・デバフともにここへ入る。</summary>
    public int AtkBonus { get; set; }

    public IReadOnlyList<Trait> Traits { get; init; } = Array.Empty<Trait>();

    /// <summary>特性が自由に使えるカウンタ置き場。特性ごとにキーを分ける。</summary>
    public Dictionary<string, int> Counters { get; } = new();

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
}
