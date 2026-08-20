namespace BattleCore;

/// <summary>列。前列は狙われやすく、後列は前列が生きている間は狙われにくい。</summary>
public enum Row
{
    Front,
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
    /// <summary>貫き。前列を無視して後列を直接狙える。庇えず、標的にも釣られない。</summary>
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

    /// <summary>0..4。0,1,2 が前列、3,4 が後列。臆病などで戦闘中に変化する。</summary>
    public int Slot { get; set; }

    public int Hp { get; set; }
    public int MaxHp { get; set; }

    /// <summary>戦闘中に加算される攻撃力補正。バフ・デバフともにここへ入る。</summary>
    public int AtkBonus { get; set; }

    public IReadOnlyList<Trait> Traits { get; init; } = Array.Empty<Trait>();

    /// <summary>特性が自由に使えるカウンタ置き場。特性ごとにキーを分ける。</summary>
    public Dictionary<string, int> Counters { get; } = new();

    public bool IsAlive => Hp > 0;
    public Row Row => Slot < FormationRules.FrontSlots ? Row.Front : Row.Back;

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

public static class FormationRules
{
    public const int TotalSlots = 5;
    public const int FrontSlots = 3;

    public static Row RowOf(int slot) => slot < FrontSlots ? Row.Front : Row.Back;

    /// <summary>同じ列で隣り合っているか。巻き込み系の判定に使う。</summary>
    public static bool AreAdjacent(int a, int b)
    {
        if (a == b) return false;
        if (RowOf(a) != RowOf(b)) return false;
        return Math.Abs(a - b) == 1;
    }
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

    public static Formation Of(params UnitDef?[] defs)
    {
        var f = new Formation();
        for (int i = 0; i < defs.Length && i < FormationRules.TotalSlots; i++)
            f[i] = defs[i];
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
