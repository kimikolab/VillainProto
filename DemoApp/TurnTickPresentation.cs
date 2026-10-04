using BattleCore;
using System.Collections.Generic;
using System.Linq;

// TurnStart → 最初の写しだけを読む。死亡・延焼・割り込みを跨がず、駒ごとの数字をまとめて同時に表示する。
internal sealed class TurnTickBeat
{
    internal int TargetId;
    internal readonly List<int> Roots = new();
    internal readonly List<(bool Burn, bool Heal, int Amount, bool Brittle)> Numbers = new();
    internal bool Poison, InversePoison, Burn, FireHeal, FireDamage;
    internal int FirePulses;
    internal double Seconds => System.Math.Min(TickPresentation.MaxSeconds, 0.58 + 0.04 * System.Math.Max(0, FirePulses - 1));

    internal void AddNumber(bool burn, BattleEvent e)
    {
        bool heal = e.Kind == BattleEventKind.Heal;
        int i = Numbers.FindIndex(n => n.Burn == burn && n.Heal == heal);
        if (i < 0) Numbers.Add((burn, heal, e.Amount, e.BrittleExtra > 0));
        else
        {
            var n = Numbers[i];
            Numbers[i] = (burn, heal, n.Amount + e.Amount, n.Brittle || e.BrittleExtra > 0);
        }
        if (burn) { FireHeal |= heal; FireDamage |= !heal && e.Amount > 0; }
    }
}

internal sealed record TurnTickRange(int Start, int End, IReadOnlyList<TurnTickBeat> Beats,
    IReadOnlyDictionary<int, TurnTickBeat> Primary);

internal sealed class TurnTickPresentation
{
    internal readonly Dictionary<int, TurnTickRange> Starts = new();

    internal static bool IsRoot(BattleEvent e) =>
        TickPresentation.IsTick(e) && e.ActorId is null
        || e.Kind == BattleEventKind.FireArmor && e.Text is FireArmorLabels.Convert or FireArmorLabels.Mend;

    internal static TurnTickPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new TurnTickPresentation();
        bool turnHead = false;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.TurnStart) { turnHead = true; continue; }
            if (e.Kind is BattleEventKind.StatusSnapshot or BattleEventKind.StatSnapshot) turnHead = false;
            if (!turnHead || !IsRoot(e) || e.TargetId is null || e.Reaction || e.Relayed) continue;
            int start = i;
            var beats = new List<TurnTickBeat>();
            var primary = new Dictionary<int, TurnTickBeat>();
            BattleEvent root = e;
            TurnTickBeat? beat = null;
            BattleEvent? sip = null;
            for (; i < events.Count; i++)
            {
                e = events[i];
                if (e.Turn != root.Turn || e.Reaction || e.Relayed) break;
                if (IsRoot(e) && e.TargetId is int id)
                {
                    root = e;
                    sip = null;
                    beat = beats.FirstOrDefault(b => b.TargetId == id);
                    if (beat is null) { beat = new() { TargetId = id }; beats.Add(beat); }
                    beat.Roots.Add(i);
                    bool burn = e.Kind == BattleEventKind.FireArmor || e.Text == "燃焼";
                    if (burn)
                    {
                        beat.Burn = true;
                        beat.FirePulses++;
                        // 満タン・支援拒否でHealが無くても、台本の変換通知はオーラ。
                        beat.FireHeal |= e.Kind == BattleEventKind.FireArmor || e.SourceTrait == TraitId.Inverse;
                    }
                    else { beat.Poison = true; beat.InversePoison |= e.SourceTrait == TraitId.Inverse; }
                    primary[i] = beat;
                    continue;
                }
                if (beat is null) break;
                if (e.Kind == BattleEventKind.InverseSip && e.TargetId == root.TargetId)
                { sip = e; continue; }
                // 啜ったベニ自身の回復は、元の駒の数字へ混ぜない。
                if (sip is not null && e.Kind == BattleEventKind.Heal && e.TargetId == sip.ActorId && e.ActorId == sip.ActorId)
                    continue;
                // 啜り切れなかった回復の紅蓮化も、同じ刻みの帰結。ここで切ると1体ずつ待つ再生へ戻ってしまう。
                if (sip is not null && e.Kind == BattleEventKind.GurenGain
                    && e.ActorId == sip.ActorId && e.TargetId == sip.TargetId && e.SourceTrait == TraitId.Guren)
                    continue;
                bool outcome = sip is null && e.TargetId == root.TargetId &&
                    (e.Kind == BattleEventKind.Damage && e.ActorId is null
                    || e.Kind == BattleEventKind.Heal && e.ActorId ==
                        (root.SourceTrait == TraitId.Inverse ? root.InverterId : root.ActorId));
                if (outcome)
                {
                    beat.AddNumber(root.Kind == BattleEventKind.FireArmor || root.Text == "燃焼", e);
                    primary[i] = beat;
                    continue;
                }
                if (e.TargetId == root.TargetId && e.Kind is BattleEventKind.Sealed or BattleEventKind.HealBlocked)
                    continue;
                if (e.Kind == BattleEventKind.FireArmor && e.TargetId == root.TargetId
                    && e.Text is FireArmorLabels.Guard or FireArmorLabels.Ward) continue;
                if (e.Kind == BattleEventKind.FireLevel && e.Text == FireLevelLabels.Out) continue;
                // 死亡とそれに続く延焼、別の駒への反応などは通常の台本再生へ返す。
                break;
            }
            plan.Starts[start] = new(start, i, beats, primary);
            i--;
        }
        return plan;
    }
}
