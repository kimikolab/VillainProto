using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int GurenReleases, GurenGains, SwordDraws, SwordRipostes, NumbSwings, VenomReturns, Spews;
    private int _specialGeneration;
    private static readonly Color Platinum = new("fff0c9");

    public void ShowGurenGain(BattlePawn3D? source, BattlePawn3D? beni, int remaining, double speed)
    {
        if (beni is null) return;
        GurenGains++;
        beni.SetGuren(remaining);
        if (source is not null)
            FlowDrops(source.FxPoint, beni.FlowerPoint, new Color("f1394c"), 0.24 / speed, false, pull: true);
    }

    public void ShowGurenRelease(BattlePawn3D? beni, IReadOnlyList<BattlePawn3D> targets, double speed)
    {
        if (beni is null) return;
        GurenReleases++;
        beni.SetGuren(0);
        beni.SetGurenRelease(true);
        // 一本の奔流を列の幅へ開く。各敵に撃った細線にはしない。
        foreach (var target in targets.OrderBy(p => p.GlobalPosition.Z))
        for (int k = 0; k < 12; k++)
        {
            var spread = new Vector3(Mathf.Sin(k * 2.4f) * 0.45f, (k % 4 - 1.5f) * 0.30f, Mathf.Cos(k * 1.7f) * 0.7f);
            SpecialsFx.Travel(_fxRoot, beni.FlowerPoint, target.FxPoint + spread,
                k % 3 == 0 ? new Color("913b98") : new Color("f44335"), 0.52 / speed,
                arc: 0.25f + k * 0.045f, size: 0.8f, delay: k * 0.008 / speed);
        }
    }

    public void ShowVenom(BattlePawn3D? source, BattlePawn3D? target, bool returned, double speed)
    {
        if (source is null || target is null) return;
        if (returned) VenomReturns++; else Spews++;
        // 反射は袋の高さから短く弾ける。能動の吐息は口元から高い弧。
        Vector3 from = source.FxPoint + Vector3.Up * (returned ? -0.3f : 0.65f);
        for (int k = 0; k < 7; k++)
            SpecialsFx.Travel(_fxRoot, from, target.FxPoint + Vector3.Up * (k - 3) * 0.13f,
                new Color("ac60dd"), (returned ? 0.22 : 0.36) / speed,
                kind: 3, arc: returned ? (k - 3) * 0.12f : 1.0f + k * 0.08f, size: returned ? 0.65f : 0.8f);
    }

    public async Task ShowSwordDraw(BattlePawn3D pawn, int scars, double speed)
    {
        SwordDraws++;
        int generation = _specialGeneration;
        // 盾だけが落ちる一拍を、抜剣の発光と分ける。
        var image = new Image();
        image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='96' height='128'><path d='M48 5L89 25 81 85 48 121 15 85 7 25z' fill='#7c7a76' stroke='#c4bdb0' stroke-width='5'/><path d='M48 9v105M12 29l69 55M18 83l66-51' stroke='#b6aca0' stroke-width='3'/><path d='M41 16l9 26-9 13 12 27-8 21M19 46l19 6-10 18' fill='none' stroke='#302929' stroke-width='3'/></svg>");
        var shield = new Sprite3D { Texture = ImageTexture.CreateFromImage(image), PixelSize = 0.009f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false,
            Position = _fxRoot.ToLocal(pawn.FxPoint + Vector3.Right * 0.42f) };
        _fxRoot.AddChild(shield);
        var tween = shield.CreateTween();
        tween.TweenInterval(0.12 / speed);
        tween.TweenProperty(shield, "position:y", 0.12f, 0.18 / speed);
        tween.Parallel().TweenProperty(shield, "rotation:z", 1.4f, 0.18 / speed);
        await ToSignal(GetTree().CreateTimer(Math.Max(0.01, 0.30 / speed)), SceneTreeTimer.SignalName.Timeout);
        if (generation != _specialGeneration || !IsInstanceValid(pawn) || !pawn.IsInsideTree()) return;
        pawn.DrawSword(scars);
        MakeBeam(pawn.FxPoint - Vector3.Up, pawn.FxPoint + Vector3.Up * 1.2f, Platinum,
            0.035f + Math.Clamp(scars / 24f, 0, 1) * 0.055f, 0.28 / speed);
        var fade = shield.CreateTween();
        fade.TweenInterval(0.22 / speed);
        fade.TweenProperty(shield, "scale", Vector3.One * 0.01f, 0.12 / speed);
        fade.TweenCallback(Callable.From(shield.QueueFree));
    }

    public void ShowSwordSlash(BattlePawn3D? source, IReadOnlyList<BattlePawn3D> targets, double speed, bool riposte = false, bool playSound = true)
    {
        if (source is null || targets.Count == 0) return;
        if (riposte) SwordRipostes++;
        var first = targets.OrderBy(p => p.FxPoint.Z).First();
        var last = targets.OrderBy(p => p.FxPoint.Z).Last();
        Vector3 side = targets.Count > 1 ? (last.FxPoint-first.FxPoint).Normalized() : Vector3.Right;
        Vector3 from = first.FxPoint - side * 1.05f;
        Vector3 to = last.FxPoint + side * 1.05f;
        if (riposte) { from += Vector3.Up * 0.65f; to -= Vector3.Up * 0.5f; }
        MakeBeam(from, to, new Color(Platinum, 0.3f), 0.36f, 0.22 / speed);
        MakeBeam(from, to, Platinum, 0.065f, 0.27 / speed);
        if (playSound) _attackAudio.PlayAttack(source.UnitId, source.Team, BattleCore.AttackPattern.Sweep, riposte, false);
    }

    public void ShowSwordParry(BattlePawn3D defender)
    {
        Vector3 center = defender.FxPoint;
        double duration = 0.23 / defender.AnimationSpeed;
        MakeBeam(center + new Vector3(-0.65f,-0.65f,0), center + new Vector3(0.65f,0.65f,0), Platinum, 0.075f, duration);
        MakeBeam(center + new Vector3(-0.6f,0.6f,0), center + new Vector3(0.6f,-0.6f,0), Colors.White, 0.06f, duration);
        _attackAudio.PlayParry();
    }
}
