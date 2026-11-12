using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class DebuffCheck
{
    private void CheckCastReplay(BattleResult result)
    {
        var field = _main.ViewerField;
        var openings = _main.ViewerOpenings.ToDictionary(o => o.InstanceId);
        bool WrittenBy(BattleEvent e, string unit) => e.ActorId is int id && openings.TryGetValue(id, out var o) && o.UnitId == unit;
        var gains = result.Events.Where(e => e.Kind == BattleEventKind.StatusGain && e.Amount > 0).ToArray();
        int papers = gains.Count(e => WrittenBy(e, "nel") && e.Text == StatusKeys.Curse);
        int sinks = gains.Count(e => WrittenBy(e, "nel") && e.Text == "dull" && e.TargetId is int to
            && openings[to].Team != openings[e.ActorId!.Value].Team);
        int daunts = gains.Count(e => WrittenBy(e, "kubi") && e.Text == StatusKeys.Daunted);
        int directLeaks = gains.Count(e => WrittenBy(e, "nel") && e.Text == "dull" && e.TargetId is int to
            && openings[to].Team == openings[e.ActorId!.Value].Team);
        int scars = gains.Count(e => WrittenBy(e, "kari") && e.Text == "dull" && e.TargetId is int to
            && openings[to].Team != openings[e.ActorId!.Value].Team);
        var plan = DebuffPresentation.Build(result.Events, _main.ViewerOpenings);
        Require(field.NelTalismans == papers, $"ネルの実際の付与だけを呪符で描く expected={papers} actual={field.NelTalismans}");
        Require(field.NelSinkHits == sinks, "開戦・手番の敵全員の弱体が一度ずつ沈む");
        Require(field.NelMistBranches == plan.NelLeaks.Values.Sum(b => b.Targets.Length), "横取り前の漏れ元も既存通知から読む");
        Require(field.NelLeakReceipts == directLeaks, "開戦・手番の実際の味方の受け取りだけが沈む");
        Require(field.KubiDauntHits == daunts && field.KubiBatchPlays == plan.KubiGroups.Count,
            "クビの敵味方を同時に縮め、通知を重ねない");
        Require(field.KubiBellPlays == plan.KubiGroups.Count, "実台本のクビの付与1組につき鐘を1回だけ鳴らす");
        Require(field.KarzDullPlays == scars, "カーズの実際の攻撃後の弱体だけを銅色で描く");
        Require(field.KarzMarkPlays == gains.Count(e => WrittenBy(e, "kari") && e.Text == BattleContext.KarzMarkText),
            "刻む瞬間はkarz_markだけで一度ずつ描く");
        GD.Print($"DEBUFF_CAST_REPLAY_OK paper={papers} sinks={sinks} mist={field.NelMistBranches} daunt={daunts} karz={scars}");
    }

    private async Task CheckCastClock()
    {
        var field = _main.ViewerField; field.Visible = true; field.BeginBattle(Openings(), "時計", 0);
        field.SetDeathClock(false, 1);
        var source = field.FindPawn(3)!; var enemy = field.FindPawn(6)!; var ally = field.FindPawn(2)!;
        enemy.UseNelCurseTalisman(); enemy.SetStatusIcon(StatusKeys.Curse, true);
        var paper = field.ShowNelTalisman(source, enemy);
        var batch = field.ShowKubiDaunt(new[] { enemy, field.FindPawn(7)!, ally });
        Require(enemy.DebuffPoseAge == ally.DebuffPoseAge && enemy.DebuffPoseAge == 0, "敵と味方の萎縮が同じ拍で始まる");
        field.ShowNelLeak(source, new[] { ally, field.FindPawn(4)! });
        field.ShowKarzDull(enemy);
        await Frame(); field.SetDeathClock(true, 1);
        var effects = Descendants(field).OfType<DebuffFx3D>().ToArray();
        var ages = effects.Select(e => e.Age).ToArray(); double pose = enemy.DebuffPoseAge;
        await Wait(.12);
        Require(effects.Select(e => e.Age).SequenceEqual(ages) && enemy.DebuffPoseAge == pose && !paper.IsCompleted,
            "呪符・靄・刻印色の弱体・縮こまりが停止時計を共有");
        field.SetDeathClock(false, 2); await Until(() => paper.IsCompleted && batch.IsCompleted);
        await paper; await batch;
        Require(enemy.HasStatusIcon(StatusKeys.Curse) && enemy.NelCurseStyled && !enemy.HasCurseStain,
            "ネルの呪符にムドの泥を重ねない");
        enemy.BeginStatusSnapshot(); enemy.ReadStatusSnapshot(StatusKeys.LabelOf(StatusKeys.Curse), 1); enemy.CommitStatusSnapshot();
        Require(enemy.NelCurseStyled && !enemy.HasCurseStain, "ターンの写しでも呪符の形を保つ");
        enemy.SetStatusIcon(StatusKeys.Curse, false); await Frame(); await Frame();
        Require(!Descendants(field).OfType<DebuffTalisman3D>().Any(), "呪い解除で呪符を消す");
        enemy.SetStatusIcon(StatusKeys.Curse, true);
        Require(enemy.HasCurseStain && !enemy.NelCurseStyled, "別の付与は既存の泥へ戻る");
        field.BeginBattle(Openings(true), "反転", 0); field.SetDeathClock(false, 1);
        source = field.FindPawn(3)!; enemy = field.FindPawn(6)!;
        enemy.UseNelCurseTalisman(); enemy.SetStatusIcon(StatusKeys.Curse, true);
        var mirrored = field.ShowNelTalisman(source, enemy);
        await mirrored; Require(Descendants(field).OfType<DebuffTalisman3D>().Count() == 1, "反対陣営でも呪符を貼る");
        enemy.AnimateDeath(); await Frame(); await Frame();
        Require(!Descendants(field).OfType<DebuffTalisman3D>().Any(), "死亡で呪符を消す");
        field.BeginBattle(Openings(), "中断", 0); source = field.FindPawn(3)!; enemy = field.FindPawn(6)!;
        enemy.UseNelCurseTalisman(); enemy.SetStatusIcon(StatusKeys.Curse, true);
        var interrupted = field.ShowNelTalisman(source, enemy);
        field.SetDeathClock(true, 1); field.BeginBattle(Openings(), "再戦", 0);
        await Until(() => interrupted.IsCompleted); await Frame();
        Require(!Descendants(field).OfType<DebuffFx3D>().Any(), "中断時に待ち手を解放し、全素材を片付ける");
        GD.Print("DEBUFF_CAST_CLOCK_OK simultaneous paper mist shrink scar pause speed clear mirror death cancel=True");
    }

    private async Task CheckCastSeek()
    {
        var openings = Openings();
        openings[0] = openings[0] with { UnitId = "kari", Name = "祟りのカーズ", Traits = UnitCatalog.Kari.Traits };
        BattleEvent E(BattleEventKind kind, int actor, int? target = null, string? text = null, int amount = 0)
            => new() { Kind = kind, Turn = 1, ActorId = actor, TargetId = target, Text = text, Amount = amount };
        var events = new[] {
            E(BattleEventKind.Skill, 3, text: "呪う"),
            E(BattleEventKind.StatusGain, 3, 6, StatusKeys.Curse, 1),
            E(BattleEventKind.StatusGain, 3, 6, "dull", 4), E(BattleEventKind.StatusGain, 3, 7, "dull", 4),
            E(BattleEventKind.StatusGain, 3, 2, "dull", 2),
            E(BattleEventKind.Skill, 5, text: "萎縮"),
            E(BattleEventKind.StatusGain, 5, 6, StatusKeys.Daunted, 1), E(BattleEventKind.StatusGain, 5, 7, StatusKeys.Daunted, 1),
            E(BattleEventKind.StatusGain, 5, 3, StatusKeys.Daunted, 1),
            E(BattleEventKind.Skill, 1, text: "呪いを刻む"), E(BattleEventKind.StatusGain, 1, 6, "dull", 4),
            E(BattleEventKind.TurnStart, 3),
        };
        _main.EnterScripted(openings, events, "術のシーク"); await Until(() => !_main.IsPlaying);
        await _main.SeekTo(9);
        var field = _main.ViewerField;
        Require(field.NelTalismans == 1 && field.NelSinkHits == 2 && field.NelMistBranches == 1 && field.KubiDauntHits == 3,
            "シークで呪符・漏れ・萎縮の件数を復元");
        Require(field.FindPawn(6)!.NelCurseStyled && !field.FindPawn(6)!.HasCurseStain, "シークで紙の呪符を復元");
        Require(!Descendants(field).OfType<PopupLabel2D>().Any(p => p.Text.Contains("萎縮") || p.Text.Contains("呪う")
            || p.Text.Contains("呪いを刻む") || p.Text.Contains("なまり")), "術名・弱体量の浮き文字を重ねない");
        _main.ResumePlayback(); await Until(() => !_main.IsPlaying);
        Require(field.KarzDullPlays == 1, "カーズの攻撃後の対象を通知だけで読む");
        await _main.SeekTo(1); Require(field.NelTalismans == 0 && field.KubiDauntHits == 0, "巻き戻しで未来の演出を残さない");
        _main.ResumePlayback(); await Until(() => !_main.IsPlaying);
        GD.Print("DEBUFF_CAST_SEEK_OK paper leak simultaneous no-text karz-dull-only=True");
    }

    private async Task CaptureCasts(BattlefieldView3D field, Func<string, Task> save)
    {
        field.BeginBattle(Openings(), "", 0); field.SetDeathClock(false, 1); await Wait(.2);
        var source = field.FindPawn(3)!; var enemy = field.FindPawn(6)!;
        enemy.UseNelCurseTalisman(); enemy.SetStatusIcon(StatusKeys.Curse, true);
        await field.ShowNelTalisman(source, enemy);
        var sink = field.ShowNelSink(new[] { enemy, field.FindPawn(7)! });
        field.ShowNelLeak(source, new[] { field.FindPawn(1)!, field.FindPawn(4)!, field.FindPawn(2)! });
        await Freeze<DebuffMist3D>(); await save("nel-curses.png");
        field.SetDeathClock(false, 1); await sink; await Wait(.4);
        var daunt = field.ShowKubiDaunt(new[] { enemy, field.FindPawn(7)!, source }, field.FindPawn(5));
        await Freeze<DebuffPressure3D>(); await save("kubi-daunt.png");
        field.SetDeathClock(false, 1); await daunt; await Wait(.4);
        field.ShowKarzDull(enemy); await Freeze<DebuffKarzDull3D>(); await save("karz-dull.png");
        async Task Freeze<T>() where T : DebuffFx3D
        {
            await Until(() => Descendants(field).OfType<T>().Any(f => !f.IsQueuedForDeletion() && f.Age >= .10));
            field.SetDeathClock(true, 1);
        }
    }
}
