using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 上段のモーションと接触コマの大剣閃。実際の攻撃入口、左右、倍速、一時停止、中断を検査する。
public partial class GaldSwordCheck : Control
{
    private BattlefieldView3D _field = null!;
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            _field = new BattlefieldView3D();
            _field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_field);
            var clip = BattlePawn3D.AttackClipOf("gald_cleave");
            Require(clip is not null && clip.Frames.Length == 28 && clip.Contact == 15, "採用した28コマと命中コマ");
            foreach (int team in new[] { 0, 1 })
            foreach (double speed in new[] { 1.0, 2.0 })
                await Attack(team, speed, clip!);
            await AdvanceAndRiposte();
            await PauseAndCancel();
            await DeathBeforeContact();
            GD.Print($"GALD_SWORD_CHECK_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GetTree().Paused = false;
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }

    private (BattlePawn3D gald, BattlePawn3D target, BattlePawn3D second) Begin(int team, double speed)
    {
        _field.BeginBattle(new DemoOpening[] {
            new(1, team, "gald", "ガルド", 2, 100, 100, 40, AttackPattern.Sweep, true),
            new(2, 1-team, "knight", "標的1", 0, 100, 100, 20, AttackPattern.Single, true),
            new(3, 1-team, "knight", "標的2", 1, 100, 100, 20, AttackPattern.Single, true),
        }, "ガルド・一刀両断", 0);
        foreach (var p in _field.Pawns.Values) p.AnimationSpeed = speed;
        return (_field.FindPawn(1)!, _field.FindPawn(2)!, _field.FindPawn(3)!);
    }

    private async Task Attack(int team, double speed, BattlePawn3D.AttackClip clip)
    {
        var (gald, target, second) = Begin(team, speed);
        Require(!gald.SwordDrawn && !gald.AttackClipPlaying, "盾を持つ段は上段モーションを流さない");
        gald.DrawSword(18);
        var sprite = gald.GetChildren().OfType<Sprite3D>().Single();
        var swordIdle = sprite.Texture;
        await Wait(0.08);
        int plays = _field.GaldAttackClipPlays, flashes = _field.GaldSwordFlashPlays;
        int contacts = 0;
        _field.AttackContact = hit => {
            contacts++;
            Require(gald.AttackClipFrame >= clip.Contact && _field.GaldSwordFlashPlays == flashes+1,
                "刃が当たるコマで剣閃と接触通知が揃う");
        };
        var attack = _field.Attack(gald, target, AttackPattern.Sweep, new[] {target,second}, advance:false);
        await Frame();
        Require(gald.AttackClipPlaying && !gald.IdleLoopPlaying && sprite.FlipH == (team == 1), "左右の向きと攻撃の開始");
        float feet = sprite.Position.Y - (clip.Anchor.Y-sprite.Texture.GetHeight()*0.5f)*sprite.PixelSize;
        Require(Math.Abs(feet-BattlePawn3D.PortraitGroundYForCheck) < 0.035, "上段の足元を地面に合わせる");
        await Wait(0.20/speed);
        Require(contacts == 0 && _field.GaldSwordFlashPlays == flashes, "溜め中は命中通知と剣閃を出さない");
        await Capture($"team{team}-speed{speed}-jodan");
        await attack;
        Require(_field.GaldAttackClipPlays == plays+1 && contacts == 2 && gald.AttackClipPlaying, "1回の斬撃で全対象へ各1通知し、振り切りを続ける");
        var flash = _field.LastGaldSwordFlash!;
        Require(flash.TargetCount == 2 && flash.SlashSize.Y >= 5 && flash.SlashSize.X >= 4.6f,
            "敵の身長を越える大きな剣閃");
        if (ResourceLoader.Exists(GaldSwordSlashFx.ImpactPath)) Require(flash.PurchasedImpacts == 2, "命中点に購入素材の火花を重ねる");
        await Wait(0.07/speed);
        await Capture($"team{team}-speed{speed}-hit");
        await gald.AttackClipFinished;
        await Wait(0.1);
        Require(sprite.Texture == swordIdle, "振り切り後は盾なしの構えに戻る");
        Require(_field.GaldSwordFlashPlays == flashes+1, "剣閃を二重に出さない");
    }

    private async Task PauseAndCancel()
    {
        var (gald,target,_) = Begin(0,1);
        gald.DrawSword(12);
        int before = _field.GaldSwordFlashPlays;
        var attack = _field.Attack(gald,target,AttackPattern.Sweep,new[]{target},advance:false);
        await Wait(0.18);
        int frame = gald.AttackClipFrame;
        GetTree().Paused = true;
        await Wait(0.18);
        Require(gald.AttackClipFrame == frame && _field.GaldSwordFlashPlays == before, "一時停止中は上段と命中待ちを止める");
        GetTree().Paused = false;
        gald.AnimationSpeed = 2;
        await attack;
        var flash = _field.LastGaldSwordFlash!;
        double elapsed = flash.Elapsed;
        GetTree().Paused = true;
        await Wait(0.15);
        Require(Math.Abs(flash.Elapsed-elapsed)<0.001, "一時停止中は剣閃と粒子も止まる");
        GetTree().Paused = false;
        gald.AnimationSpeed = 1;
        await Wait(0.1);
        Require(flash.Elapsed > elapsed && flash.Elapsed < elapsed+0.18, "途中の倍速変更を剣閃にも反映する");

        // 振り切り中の斬り返しでも新しいモーションを残し、早く席へ戻さない。
        int ripostes = _field.SwordRipostes;
        var riposte = _field.ShowSwordRiposte(gald,new[]{target},1);
        await Wait(0.15);
        Require(gald.AttackClipPlaying && _field.SwordRipostes == ripostes, "斬り返しにも上段の溜めがある");
        await riposte;
        Require(_field.SwordRipostes == ripostes+1 && gald.AttackClipFrame >= 15, "斬り返しを接触時に1回出す");
        await gald.AttackClipFinished;

        var canceled = _field.Attack(gald,target,AttackPattern.Sweep,new[]{target},advance:false);
        await Wait(0.12);
        before = _field.GaldSwordFlashPlays;
        _field.Hide();
        _field.Show();
        await canceled;
        await Wait(0.15);
        Require(!gald.AttackClipPlaying && _field.GaldSwordFlashPlays == before, "非表示で中断し、再表示しても古い剣閃を出さない");
        var reset = _field.Attack(gald,target,AttackPattern.Sweep,new[]{target},advance:false);
        await Wait(0.1);
        before = _field.GaldSwordFlashPlays;
        Begin(1,1);
        await reset;
        Require(_field.GaldSwordFlashPlays == before, "再戦で古い命中予約を残さない");
    }

    private async Task AdvanceAndRiposte()
    {
        var (gald,target,_) = Begin(0,1);
        gald.DrawSword(18);
        Vector3 home = gald.RestPosition;
        _field.AttackContact = null;
        var attack = _field.Attack(gald,target,AttackPattern.Sweep,new[]{target});
        while (!gald.AttackClipPlaying) await Frame();
        await Wait(0.15);
        await Capture("melee-jodan");
        await attack;
        Require(gald.Position.DistanceTo(home)>0.3f && gald.AttackClipPlaying, "接近して斬った位置で振り切りを続ける");
        await Wait(0.07);
        await Capture("melee-hit");
        var riposte = _field.ShowSwordRiposte(gald,new[]{target},1);
        await Wait(0.16);
        Require(gald.AttackClipPlaying && gald.Position.DistanceTo(home)>0.3f,
            "振り切り中の斬り返しで、新しい上段を残して早戻りを防ぐ");
        await riposte;
        await gald.AttackClipFinished;
        await Wait(0.4);
        Require(gald.Position.DistanceTo(home)<0.05f, "斬り返しも振り切ってから元の席へ戻る");
    }

    private async Task DeathBeforeContact()
    {
        var (gald,target,_) = Begin(0,1);
        gald.DrawSword(12);
        int before = _field.GaldSwordFlashPlays;
        var attack = _field.Attack(gald,target,AttackPattern.Sweep,new[]{target},advance:false);
        await Wait(0.1);
        _field.PlayDeath(gald);
        await attack;
        Require(!gald.AttackClipPlaying && _field.GaldSwordFlashPlays == before, "死亡で止め、命中前の剣閃を出さない");
    }

    private async Task Capture(string phase)
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(arg[14..]+"/"+phase+".png") == Error.Ok, "画像保存");
    }
    private async Task Frame() => await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Require(bool ok,string message) { if (!ok) throw new InvalidOperationException(message); _checks++; }
}
