using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// 共通デバフ演出を導入していない版でも鐘番だけを検査できる。
public partial class KubiBellCheck : Control
{
    private Main _main = null!;
    public override async void _Ready()
    {
        try
        {
            _main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(_main);
            _main.ViewerFastPlayback = true; _main.SetPlaybackSpeed(1000);
            var events = new BattleEvent[] {
                new() { Kind = BattleEventKind.Skill, Turn = 1, ActorId = 1, Text = "萎縮" },
                new() { Kind = BattleEventKind.StatusGain, Turn = 1, ActorId = 1, TargetId = 2, Text = StatusKeys.Daunted, Amount = 1 },
                new() { Kind = BattleEventKind.StatusGain, Turn = 1, ActorId = 1, TargetId = 3, Text = StatusKeys.Daunted, Amount = 1 },
                new() { Kind = BattleEventKind.TurnStart, Turn = 2 },
            };
            _main.EnterScripted(Openings(), events, "鐘の接続");
            await Until(() => !_main.IsPlaying);
            var field = _main.ViewerField;
            Require(field.KubiBellPlays == 1, "本番の通知入口で敵味方の付与1組につき鐘が1回");
            foreach (bool mirror in new[] { false, true })
            {
                field.Visible = true; field.BeginBattle(Openings(mirror), "鐘の時計", 0); field.SetDeathClock(false, 1);
                var source = field.FindPawn(1)!; var enemy = field.FindPawn(2)!; var ally = field.FindPawn(3)!;
                field.ShowKubiBell(source, new[] { enemy, enemy, ally });
                var effect = Descendants(field).OfType<KubiBellFx3D>().Single(f => !f.IsQueuedForDeletion());
                Require(field.KubiBellPlays == 1, "対象が重複しても鐘音は1回");
                await Until(() => effect.Age >= .09);
                field.SetDeathClock(true, 1); await Frame();
                double age = effect.Age, pose = source.KubiBellAge;
                var sprite = source.GetChildren().OfType<Sprite3D>().First();
                Vector3 rotation = sprite.Rotation, scale = sprite.Scale;
                await Wait(.12);
                Require(effect.Age == age && source.KubiBellAge == pose && effect.SoundPaused
                    && sprite.Rotation == rotation && sprite.Scale == scale, "停止中は波紋・音・震えを保持");
                field.SetDeathClock(false, 4); await Frame(); await Frame();
                Require(effect.Age > age && source.KubiBellAge > pose && !effect.SoundPaused, "途中の4倍速切替に追従");
                await Until(() => !GodotObject.IsInstanceValid(effect));
                await Until(() => source.KubiBellAge >= KubiBellFx3D.Duration);
                Require(source.Position == source.Home, "震えで編成の席を動かさない");
                field.ShowKubiBell(source, new[] { enemy, ally }); source.AnimateDeath();
                await Frame(); await Frame(); Require(NoBell(field), "死亡で鐘音と波紋が消える");
                field.BeginBattle(Openings(mirror), "再戦", 0); field.SetDeathClock(false, 1);
                source = field.FindPawn(1)!; enemy = field.FindPawn(2)!;
                if (!mirror)
                {
                    field.ShowKubiBell(source, new[] { enemy }); source.AnimateVictory();
                    await Frame(); await Frame(); Require(NoBell(field), "勝利で鐘音と波紋が消える");
                    field.BeginBattle(Openings(mirror), "再戦", 0); field.SetDeathClock(false, 1);
                    source = field.FindPawn(1)!; enemy = field.FindPawn(2)!;
                }
                field.ShowKubiBell(source, new[] { enemy }); field.SetDeathClock(true, 1);
                field.BeginBattle(Openings(mirror), "停止中の再戦", 0); await Frame();
                Require(NoBell(field), "停止中の再戦でも波紋を片付ける");
                source = field.FindPawn(1)!; field.SetDeathClock(false, 1);
                field.ShowKubiBell(source, new[] { field.FindPawn(2)! }); field.Visible = false; await Frame();
                Require(NoBell(field) && source.KubiBellAge >= KubiBellFx3D.Duration, "編成へ戻ると音と震えを残さない");
            }
            GD.Print("KUBI_BELL_STANDALONE_COMPLETE ok=True entry batch pause sound pose speed4 mirror death victory replay cancel=True");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static DemoOpening[] Openings(bool mirror = false) => new[] {
        new DemoOpening(1, mirror ? 1 : 0, "kubi", "萎縮のクビ", 3, 70, 70, 3, AttackPattern.Single, false),
        new DemoOpening(2, mirror ? 0 : 1, "knight", "近衛騎士", 0, 300, 300, 20, AttackPattern.Single, true),
        new DemoOpening(3, mirror ? 1 : 0, "nel", "呪詛官ネル", 2, 45, 45, 7, AttackPattern.Single, false),
    };
    private static bool NoBell(Node field) => !Descendants(field).OfType<KubiBellFx3D>().Any(f => !f.IsQueuedForDeletion());
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Until(Func<bool> done)
    {
        ulong end = Time.GetTicksMsec() + 30000;
        while (!done() && Time.GetTicksMsec() < end) { if (_main.ViewerPlaybackError is { } error) throw error; await Frame(); }
        Require(done(), "時間内に検査が完了");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
