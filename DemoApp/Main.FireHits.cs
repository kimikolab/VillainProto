using BattleCore;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private FireHitPresentation _fireHits = new();
    private readonly HashSet<FireHitCue> _shownFireHits = new();

    private void IndexFireHits(IReadOnlyList<BattleEvent> events)
    {
        _fireHits = FireHitPresentation.Build(events);
        _shownFireHits.Clear();
        if (_fireHits.Unpaired.Count > 0)
            Godot.GD.PushWarning($"被弾の燃焼: 対応する着弾が台本から読めない Status {_fireHits.Unpaired.Count}件（通常の刻みで表示）");
    }

    private void FireHitContact(int index, BattlePawn3D pawn)
    {
        if (!_fireHits.Contacts.TryGetValue(index, out var cues)) return;
        foreach (var cue in cues.Where(c => c.Target == pawn.InstanceId))
        {
            if (!_shownFireHits.Add(cue)) continue;
            // 本体の数字も同じ接触へ寄せる。燃焼HP・死亡・反応は元の出来事の位置で処理する。
            if (cue.Damage >= 0 && _batchedDamageIndices.Add(cue.Damage))
            {
                var e = _result!.Events[cue.Damage];
                ShowDamage(cue.Damage, e, _battleField.FindPawn(e.ActorId), pawn, false);
            }
            _battleField.ShowFireHit(pawn, cue.Beat, _speed);
        }
    }

    private void FireHitDamage(int index, BattlePawn3D? target)
    {
        if (target is null) return;
        foreach (var (contact, cues) in _fireHits.Contacts)
            if (cues.Any(c => c.Damage == index)) FireHitContact(contact, target);
    }

    private bool PlayFireHitSource(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (!_fireHits.Sources.TryGetValue(index, out var cue)) return false;
        // 接触口の無い特性ダメージも省略しない。描画だけを行い、台本は元の順で通す。
        if (!_shownFireHits.Contains(cue) && target is not null) FireHitContact(cue.Contact, target);
        if (e.Kind == BattleEventKind.Status)
        {
            _tickPlays++;
            if (e.SourceTrait == TraitId.Inverse) _inverseTickPlays++;
            AppendLog($"  [{e.Text}] → {NameOf(e.TargetId)}");
        }
        else if (e.Kind == BattleEventKind.FireArmor)
        {
            _battleField.ShowFireCue(e, actor, target, _speed, groupedTick: true);
            AppendLog($"  [color=#ffbd72]{e.Text}[/color] → {NameOf(e.TargetId)}");
        }
        else
        {
            target?.SetHp(e.HpAfter);
            bool heal = e.Kind == BattleEventKind.Heal;
            AppendLog($"  [color=#{(heal ? UiKit.Heal : UiKit.Hurt).ToHtml(false)}]{(heal ? "＋" : "−")}{e.Amount}[/color] → {NameOf(e.TargetId)}");
        }
        return true;
    }
}
