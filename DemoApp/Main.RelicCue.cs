using BattleCore;
using Godot;
using System;

public partial class Main
{
    private void ObserveRelicCue(BattleEvent e, BattlePawn3D? actor)
    {
        if (e.Kind == BattleEventKind.Death) _battleField.FindPawn(e.TargetId)?.GetNodeOrNull<RelicWorkCue>("RelicWorkCue")?.Hide();
        if (e.Kind != BattleEventKind.Relic || actor is null || !_battleMode) return;
        int token = _playToken;
        var cue = actor.GetNodeOrNull<RelicWorkCue>("RelicWorkCue");
        if (cue is null)
        {
            cue = new RelicWorkCue { Name = "RelicWorkCue" };
            actor.AddChild(cue);
        }
        cue.Start(() => _paused, () => _speed, () => token == _playToken && _battleMode && actor.Visible);
    }
}

/// <summary>台本の Relic イベントだけを読む。待ち時間・戦闘規則を追加しない。</summary>
public partial class RelicWorkCue : Node3D
{
    private readonly Label3D _mark = new()
    {
        Text = "◆", FontSize = 76, PixelSize = .012f, OutlineSize = 10,
        Modulate = UiKit.Gold, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
    };
    private Func<bool> _paused = () => false, _live = () => false;
    private Func<double> _speed = () => 1;
    private float _age = 1;
    public override void _Ready() { AddChild(_mark); Visible = false; }
    public void Start(Func<bool> paused, Func<double> speed, Func<bool> live)
    { _paused = paused; _speed = speed; _live = live; _age = 0; Visible = true; Position = Vector3.Up * 4.3f; }
    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (!_live()) { Visible = false; return; }
        if (_paused()) return;
        _age += (float)(delta * Math.Max(.1, _speed()));
        if (_age >= .65f) { Visible = false; return; }
        Position = Vector3.Up * (4.3f + _age * .9f);
        _mark.Modulate = new Color(1, .8f, .35f, Mathf.Clamp((.65f - _age) * 5, 0, 1));
    }
}
