using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    private Node3D? _somRoot;
    private readonly Dictionary<int, (Sprite3D Mote, Tween Rise)> _somRising = new();
    private readonly Dictionary<int, Vector3> _somPopPoints = new();
    internal int SomSummons, SomRains, SomStops, SomLights, SomVeils, SomImpacts, SomBreaks;
    internal int SomRisingCount => _somRising.Count;
    internal int SomVeilCount => _pawns.Values.Count(p => p.SomVeilAmount > 0);
    private Node3D SomRoot
    {
        get
        {
            if (_somRoot is null || !IsInstanceValid(_somRoot))
            { _somRoot = new Node3D(); _fxRoot.AddChild(_somRoot); }
            return _somRoot;
        }
    }

    internal void EndSomPresentation()
    {
        if (_somRoot is not null && IsInstanceValid(_somRoot)) { _somRoot.Hide(); _somRoot.QueueFree(); }
        _somRoot = null; _somRising.Clear(); _somPopPoints.Clear();
        foreach (var pawn in _pawns.Values) pawn.ClearSomPresentation();
        _attackAudio.StopSomSounds();
    }
    private void ResetSomPresentation()
    {
        EndSomPresentation();
        SomSummons = SomRains = SomStops = SomLights = SomVeils = SomImpacts = SomBreaks = 0;
    }

    internal void BeginSomSummon(BattlePawn3D som, int team, int slot, double speed, bool first)
    {
        SomSummons++;
        som.ShowMovementPortrait("som_summon", 1.05);
        var circle = ShockMarkFx.Sprite(SomRoot, PawnPosition(team, slot) + Vector3.Up * .065f,
            SomFx.Circle, 1.9f, SomFx.Violet);
        circle.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
        circle.Rotation = new Vector3(-Mathf.Pi / 2, 0, 0);
        circle.Scale = Vector3.One * .3f;
        var tween = circle.CreateTween();
        tween.TweenProperty(circle, "scale", Vector3.One, .18 / speed);
        tween.TweenInterval(.25 / speed);
        tween.TweenProperty(circle, "modulate", new Color(team == 0 ? UiKit.Player : UiKit.Enemy, 0), .42 / speed);
        tween.TweenCallback(Callable.From(circle.QueueFree));
        if (first) Float(som, "いでよ、我が忠実なる下僕よ！！", SomFx.Gold, false, 3.5f);
        _attackAudio.PlaySom(SomSound.Summon);
    }

    internal void SomBeastLooksBack(BattlePawn3D som, BattlePawn3D beast)
    {
        // 原画は体が右・頭が左。敵陣に立つ獣が一度ソムを見る。
        beast.ShowMovementPortrait("fodder_lookback", .42, flip: som.Team == 1);
    }
    internal void SomBeastSnubs(BattlePawn3D som, BattlePawn3D beast, bool first)
    {
        // 頭を敵の列へ向ける。拍が終わると通常の敵向きの待機姿へ戻る。
        beast.ShowMovementPortrait("fodder_snub", .48, flip: som.Team == 1);
        som.ShowMovementPortrait("som_stunned", .62);
        MakeGroundRing(beast.Home, beast.Team == 0 ? UiKit.Player : UiKit.Enemy, .7f, .34);
        if (first) Float(som, "……え？", SomFx.Violet, false, 3.1f);
        _attackAudio.PlaySom(SomSound.Snub);
    }

    internal void RaiseSomLight(int index, int? source, double speed)
    {
        if (_somRising.ContainsKey(index) || ElectricPoint(source) is not Vector3 point) return;
        _somPopPoints[index] = point;
        var mote = ShockMarkFx.Sprite(SomRoot, point, SomFx.Light, .30f, SomFx.Gold);
        var tween = mote.CreateTween();
        tween.TweenProperty(mote, "global_position", point + Vector3.Up * .65f, .24 / speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _somRising[index] = (mote, tween);
    }

    internal void GatherSomLight(BattlePawn3D som, IReadOnlyList<int> sources, bool blocked, double speed)
    {
        if (blocked) SomStops++; else SomRains++;
        Vector3 crown = som.GlobalPosition + Vector3.Up * (som.SomHeight + .7f);
        foreach (int source in sources)
        {
            Sprite3D? mote = null;
            if (_somRising.Remove(source, out var rising) && IsInstanceValid(rising.Mote))
            { rising.Rise.Kill(); mote = rising.Mote; }
            else if (_somPopPoints.TryGetValue(source, out Vector3 point))
                mote = ShockMarkFx.Sprite(SomRoot, point + Vector3.Up * .65f, SomFx.Light, .30f, SomFx.Gold);
            if (mote is null) continue;
            SomLights++;
            Vector3 end = blocked ? mote.GlobalPosition.Lerp(crown, .45f) : crown;
            SomFx.Flight(mote, end, .65f, .26 / speed, blocked);
        }
        _attackAudio.PlaySom(blocked ? SomSound.Stop : SomSound.Gather);
    }

    internal void RainSomLight(BattlePawn3D som, int count, IReadOnlySet<int> inverted, double speed)
    {
        Vector3 crown = som.GlobalPosition + Vector3.Up * (som.SomHeight + .7f);
        // 本数は小さな粒で示し、各粒の大きさと一斉の光量には上限を掛ける。
        int drops = Math.Clamp(count, 1, 18);
        foreach (var ally in _pawns.Values.Where(p => p.Team == som.Team && p.PresentationAlive))
        {
            bool murky = inverted.Contains(ally.InstanceId);
            for (int i = 0; i < drops; i++)
            {
                float offset = (i - (drops - 1) * .5f) * .12f;
                Vector3 end = ally.FxPoint + _camera.GlobalBasis.X * offset;
                var mote = ShockMarkFx.Sprite(SomRoot, crown, SomFx.Light, .27f, murky ? SomFx.Murky : SomFx.Gold);
                SomFx.Flight(mote, end, .75f + (i % 3) * .16f, (.28 + (i % 4) * .018) / speed);
            }
        }
        ShockMarkFx.Glow(SomRoot, crown, new Color(SomFx.Gold, .35f), .6f + Math.Min(count, 14) * .04f, .26 / speed);
        _attackAudio.PlaySom(SomSound.Rain);
    }

    internal void SetSomVeil(BattlePawn3D pawn, int amount, bool gained, double speed)
    {
        int old = pawn.SomVeilAmount;
        pawn.SetSomVeil(amount, gained);
        if (gained)
        {
            SomVeils++;
            ShockMarkFx.Sparks(SomRoot, pawn.FxPoint, SomFx.Gold, 4, .36f, .20 / speed);
            _attackAudio.PlaySom(SomSound.Veil);
        }
        else if (old > 0 && amount == 0 && pawn.PresentationAlive)
        {
            SomBreaks++;
            ShockMarkFx.Sparks(SomRoot, pawn.FxPoint, SomFx.Gold, 10, .8f, .32 / speed);
            _attackAudio.PlaySom(SomSound.Break);
        }
    }
    internal void SomVeilContact(BattlePawn3D pawn, double speed)
    {
        if (!pawn.PresentationAlive || pawn.SomVeilAmount <= 0) return;
        SomImpacts++; pawn.RippleSomVeil();
        ShockMarkFx.Sparks(SomRoot, pawn.FxPoint, SomFx.Gold, 5, .4f, .22 / speed);
        _attackAudio.PlaySom(SomSound.Impact);
    }
}
