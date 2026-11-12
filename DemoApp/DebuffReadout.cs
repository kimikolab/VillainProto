using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// 表示用の孤立した駒。弱体の収支を写し、1発の式はBattleCoreの同じ特性に問い合わせる。
// BattleContextに接続せず、盤面・乱数・元のUnitStateは一切動かさない。
internal sealed class DebuffReadout
{
    private readonly Dictionary<int, UnitState> _shadows = new();
    internal void Begin(IReadOnlyList<DemoOpening> openings)
    {
        _shadows.Clear();
        foreach (var o in openings.Where(o => o.UnitId is "mudo" or "utsu"))
        {
            var traits = o.Traits ?? (o.UnitId == "mudo" ? UnitCatalog.Mudo.Traits : UnitCatalog.Utsu.Traits);
            var def = new UnitDef { Id = o.UnitId, Name = o.Name, MaxHp = o.MaxHp, Attack = o.Attack, Speed = 0, Traits = traits };
            var state = new UnitState { Def = def, TeamId = o.Team, Slot = o.Slot, Hp = o.Hp, MaxHp = o.MaxHp,
                Traits = traits.Select(TraitCatalog.Get).ToArray() };
            // 既存Erupt/MudoCurseHeavyの暴発時の読み取りを使う。元の駒のカウンタではない。
            if (o.UnitId == "mudo") state.Counters[EruptTrait.BurstKey] = 1;
            _shadows[o.InstanceId] = state;
        }
    }
    internal void Read(BattleEvent e)
    {
        if (e.TargetId is not int id || !_shadows.TryGetValue(id, out var s)) return;
        if (e.Kind == BattleEventKind.StatusGain && e.Text == "dull") s.AtkBonus -= e.Amount;
        if (e.Kind == BattleEventKind.Whet) s.AtkBonus += e.Amount;
        if (e.Kind == BattleEventKind.Revive) s.AtkBonus = 0;
    }
    internal int? MudPower(int id) => _shadows.TryGetValue(id, out var s) && s.Def.Id == "mudo" ? s.CurrentAttack : null;
}
