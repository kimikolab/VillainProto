using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class DebuffCheck
{
    private async Task CheckKubiBell()
    {
        var field = _main.ViewerField; field.Visible = true;
        foreach (bool mirror in new[] { false, true })
        {
            field.BeginBattle(Openings(mirror), "鐘番の時計", 0); field.SetDeathClock(false, 1);
            var source = field.FindPawn(5)!; var enemy = field.FindPawn(6)!; var ally = field.FindPawn(3)!;
            var play = field.ShowKubiDaunt(new[] { enemy, enemy, ally }, source);
            Require(field.KubiDauntHits == 2 && field.KubiBellPlays == 1 && enemy.DebuffPoseAge == ally.DebuffPoseAge,
                "鐘音1回で実際の敵味方だけを同時に萎縮させ、重複対象は増やさない");
            var bell = Descendants(field).OfType<KubiBellFx3D>().Single(f => !f.IsQueuedForDeletion());
            await Until(() => bell.Age >= .09);
            field.SetDeathClock(true, 1); await Frame();
            double age = bell.Age, pose = source.KubiBellAge;
            var sprite = source.GetChildren().OfType<Sprite3D>().First();
            Vector3 rotation = sprite.Rotation, scale = sprite.Scale;
            await Wait(.12);
            Require(bell.Age == age && source.KubiBellAge == pose && bell.SoundPaused
                && sprite.Rotation == rotation && sprite.Scale == scale, "停止中は波紋・鐘音・クビの震えをすべて保持");
            field.SetDeathClock(false, 4); await Frame(); await Frame();
            Require(source.KubiBellAge > pose && bell.Age > age && !bell.SoundPaused, "4倍速への途中切替で鐘と震えが再開");
            await Until(() => play.IsCompleted && !GodotObject.IsInstanceValid(bell)); await play;
            await Until(() => source.KubiBellAge >= KubiBellFx3D.Duration);
            Require(source.Position == source.Home, "鐘を押さえる動きで編成の席を動かさない");
            foreach (bool victory in new[] { false, true })
            {
                if (victory && mirror) continue;
                var ending = field.ShowKubiDaunt(new[] { enemy, ally }, source);
                if (victory) source.AnimateVictory(); else source.AnimateDeath();
                await Frame(); await Frame();
                Require(!Descendants(field).OfType<KubiBellFx3D>().Any(f => !f.IsQueuedForDeletion()), "死亡・勝利で鐘音と波紋を消す");
                await Until(() => ending.IsCompleted);
                field.BeginBattle(Openings(mirror), "鐘番の再戦", 0); field.SetDeathClock(false, 1);
                source = field.FindPawn(5)!; enemy = field.FindPawn(6)!; ally = field.FindPawn(3)!;
            }
            var cancel = field.ShowKubiDaunt(new[] { enemy, ally }, source);
            field.SetDeathClock(true, 1); field.BeginBattle(Openings(mirror), "中断", 0);
            await Until(() => cancel.IsCompleted); await Frame();
            Require(!Descendants(field).OfType<KubiBellFx3D>().Any(f => !f.IsQueuedForDeletion()), "停止中の再戦で鐘音・波紋・待ち手を片付ける");
            source = field.FindPawn(5)!; field.SetDeathClock(false, 1);
            var leave = field.ShowKubiDaunt(new[] { field.FindPawn(6)! }, source);
            field.Visible = false; await Until(() => leave.IsCompleted); await Frame();
            Require(source.KubiBellAge >= KubiBellFx3D.Duration
                && !Descendants(field).OfType<KubiBellFx3D>().Any(f => !f.IsQueuedForDeletion()), "編成画面へ戻ると震えと音が残らない");
            field.Visible = true;
        }
        field.Visible = false;
        GD.Print("KUBI_BELL_CHECK_OK actual-targets simultaneous pause sound pose speed4 mirror death victory replay cancel=True");
    }

    private async Task CaptureKubiBell(BattlefieldView3D field, Func<string, Task> save)
    {
        foreach (bool mirror in new[] { false, true })
        foreach (double speed in new[] { 1d, 4d })
        {
            field.BeginBattle(Openings(mirror), "鐘番のクビ", 0); field.SetDeathClock(false, speed);
            await Wait(.2);
            var play = field.ShowKubiDaunt(new[] { field.FindPawn(6)!, field.FindPawn(7)!, field.FindPawn(3)! }, field.FindPawn(5));
            await Until(() => Descendants(field).OfType<KubiBellFx3D>().Any(f => !f.IsQueuedForDeletion() && f.Age >= .13));
            field.SetDeathClock(true, speed);
            await save($"kubi-bell-{(mirror ? "enemy" : "player")}-{speed}x.png");
            field.SetDeathClock(false, speed); await play; await Wait(.7);
        }
    }
}
