using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record DemoOpening(
    int InstanceId,
    int Team,
    string UnitId,
    string Name,
    int Slot,
    int Hp,
    int MaxHp,
    int Attack,
    AttackPattern Pattern);

public partial class BattlefieldView : Control
{
    private TextureRect _background = null!;
    private ColorRect _wash = null!;
    private FxLayer _fx = null!;
    private Label _eyebrow = null!;
    private Label _headline = null!;
    private Label _subline = null!;
    private Label _centerBanner = null!;
    private Texture2D _atlas = null!;

    private readonly FormationSlot[] _slots = new FormationSlot[FormationRules.PlayableSlotCount];
    private readonly Dictionary<int, PawnView> _pawns = new();
    private bool _setupMode = true;

    public Action<int>? SetupSlotClicked;
    public Action<int>? SetupSlotRemoveRequested;
    public Action<int, string>? SetupUnitDropped;

    public IReadOnlyDictionary<int, PawnView> Pawns => _pawns;

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Pass;
        _atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");

        _background = new TextureRect
        {
            Texture = UiKit.LoadTexture("res://assets/grassland_battlefield.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        _wash = new ColorRect
        {
            Color = new Color(0.02f, 0.055f, 0.045f, 0.18f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _wash.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_wash);

        var titleShade = new ColorRect
        {
            Color = new Color(0.015f, 0.035f, 0.03f, 0.74f),
            Position = Vector2.Zero,
            Size = new Vector2(2000, 94),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(titleShade);

        _eyebrow = UiKit.Text("FORMATION CAMP", 10, UiKit.Gold);
        _eyebrow.Position = new Vector2(20, 13);
        _eyebrow.AddThemeConstantOverride("outline_size", 4);
        _eyebrow.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(_eyebrow);

        _headline = UiKit.Text("捨て駒たちの陣形", 24, Colors.White);
        _headline.Position = new Vector2(18, 28);
        _headline.AddThemeConstantOverride("outline_size", 6);
        _headline.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(_headline);

        _subline = UiKit.Text("5つの席は同じではない。欠点を、隣へ、前へ、後ろへ。", 11, UiKit.Muted);
        _subline.Position = new Vector2(20, 63);
        _subline.AddThemeConstantOverride("outline_size", 4);
        _subline.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(_subline);

        for (int i = 0; i < _slots.Length; i++)
        {
            int slot = i;
            var view = new FormationSlot();
            view.Configure(slot, _atlas);
            view.Clicked = s => SetupSlotClicked?.Invoke(s);
            view.RemoveRequested = s => SetupSlotRemoveRequested?.Invoke(s);
            view.Dropped = (s, id) => SetupUnitDropped?.Invoke(s, id);
            AddChild(view);
            _slots[i] = view;
        }

        _fx = new FxLayer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 80,
        };
        _fx.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_fx);

        _centerBanner = UiKit.Text("", 34, Colors.White);
        _centerBanner.SetAnchorsPreset(LayoutPreset.FullRect);
        _centerBanner.HorizontalAlignment = HorizontalAlignment.Center;
        _centerBanner.VerticalAlignment = VerticalAlignment.Center;
        _centerBanner.MouseFilter = MouseFilterEnum.Ignore;
        _centerBanner.AddThemeConstantOverride("outline_size", 10);
        _centerBanner.AddThemeColorOverride("font_outline_color", Colors.Black);
        _centerBanner.ZIndex = 120;
        _centerBanner.Visible = false;
        AddChild(_centerBanner);

        Resized += LayoutActors;
        CallDeferred(MethodName.LayoutActors);
    }

    public void SetSetupHeader(string stageName)
    {
        _eyebrow.Text = "FORMATION CAMP  /  配置フェイズ";
        _headline.Text = stageName;
        _subline.Text = "ロスターから5体を選び、X字の席へ配置してください";
    }

    public void UpdateFormation(IReadOnlyList<UnitDef?> formation)
    {
        _setupMode = true;
        _eyebrow.Text = "FORMATION CAMP  /  配置フェイズ";
        foreach (PawnView pawn in _pawns.Values) pawn.QueueFree();
        _pawns.Clear();
        _fx.ClearEffects();
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Visible = true;
            _slots[i].SetUnit(formation[i]);
        }
        LayoutActors();
    }

    public void BeginBattle(IReadOnlyList<DemoOpening> openings, string stageName)
    {
        _setupMode = false;
        foreach (FormationSlot slot in _slots) slot.Visible = false;
        foreach (PawnView pawn in _pawns.Values) pawn.QueueFree();
        _pawns.Clear();
        _fx.ClearEffects();

        _eyebrow.Text = "BATTLE  /  TURN 0";
        _headline.Text = stageName;
        _subline.Text = "Space: 一時停止   1–4: 再生速度";

        foreach (DemoOpening opening in openings)
        {
            var pawn = new PawnView();
            pawn.Configure(opening, _atlas);
            AddChild(pawn);
            MoveChild(pawn, GetChildCount() - 2);
            _pawns[opening.InstanceId] = pawn;
        }
        LayoutActors();
    }

    public void SetTurn(int turn)
    {
        _eyebrow.Text = $"BATTLE  /  TURN {turn}";
        foreach (PawnView pawn in _pawns.Values) pawn.SetStatus("");
    }

    public void SetSubline(string value) => _subline.Text = value;

    public PawnView? FindPawn(int? instanceId)
        => instanceId is { } id && _pawns.TryGetValue(id, out PawnView? pawn) ? pawn : null;

    public void AddSummon(DemoOpening opening)
    {
        var pawn = new PawnView();
        pawn.Configure(opening, _atlas);
        pawn.Modulate = new Color(1, 1, 1, 0);
        AddChild(pawn);
        MoveChild(pawn, GetChildCount() - 2);
        _pawns[opening.InstanceId] = pawn;
        LayoutActors();
        pawn.AnimateAppear();
    }

    public void MovePawn(PawnView pawn, int slot)
    {
        pawn.Slot = slot;
        pawn.AnimateMove(PawnTopLeft(pawn.Team, slot, PawnView.ViewSize));
    }

    public void Attack(
        PawnView? from,
        PawnView? to,
        AttackPattern pattern,
        IReadOnlyList<PawnView> impacted,
        bool reaction = false,
        bool friendly = false)
    {
        if (from is null || to is null) return;
        Color color = friendly ? UiKit.Violet : reaction ? UiKit.Gold : from.Team == 0 ? UiKit.Player : UiKit.Enemy;
        Vector2 a = CenterOf(from);
        Vector2 b = CenterOf(to);
        List<Vector2> hitPoints = impacted.Select(CenterOf).Distinct().ToList();
        if (hitPoints.Count == 0) hitPoints.Add(b);
        from.AnimateAttack((b - a).Normalized());
        switch (pattern)
        {
            case AttackPattern.Sweep:
                _fx.AddSweep(a, hitPoints, color);
                break;
            case AttackPattern.Pierce:
                _fx.AddPierce(a, b, hitPoints, color);
                break;
            case AttackPattern.All:
                _fx.AddVolley(a, hitPoints, color);
                break;
            default:
                _fx.AddBeam(a, b, color, reaction ? 4.8f : 3.5f);
                break;
        }
    }

    public void AttackCue(PawnView? pawn, string value, Color color)
    {
        if (pawn is null) return;
        var label = UiKit.Text($"【{value}】", 13, color);
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Size = new Vector2(150, 28);
        label.Position = pawn.Position + new Vector2(-17, -18);
        label.ZIndex = 112;
        label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(label);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(label, "position", label.Position + new Vector2(0, -24), 0.48)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0.0f, 0.48).SetDelay(0.18);
        tween.Finished += label.QueueFree;
    }

    public void DamagePopup(PawnView? pawn, int amount, string source, Color color, bool large = false)
    {
        if (pawn is null) return;
        int stack = GetChildren().Count(child => child.HasMeta("damage_popup_for")
            && child.GetMeta("damage_popup_for").AsInt32() == pawn.InstanceId);

        var panel = new PanelContainer
        {
            Size = new Vector2(194, large ? 43 : 38),
            Position = pawn.Position + new Vector2(PawnView.ViewSize.X * 0.5f - 97, 10 - Math.Min(2, stack) * 31),
            ZIndex = 114,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        panel.SetMeta("damage_popup_for", pawn.InstanceId);
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.025f, 0.035f, 0.03f, 0.92f), color, 2, 11));
        var label = UiKit.Text($"−{amount}   {source}", large ? 17 : 14, color);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        panel.AddChild(label);
        AddChild(panel);

        var tween = CreateTween().SetParallel();
        tween.TweenProperty(panel, "position", panel.Position + new Vector2(0, -58), 0.82)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(panel, "modulate:a", 0.0f, 0.82).SetDelay(0.28);
        tween.Finished += panel.QueueFree;
    }

    public void Impact(PawnView? pawn, Color color, bool status)
    {
        if (pawn is null) return;
        _fx.AddImpact(CenterOf(pawn), color, status);
    }

    public void Float(PawnView? pawn, string value, Color color, bool large = false)
    {
        if (pawn is null) return;
        var label = UiKit.Text(value, large ? 22 : 17, color);
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Size = new Vector2(150, 34);
        label.Position = pawn.Position + new Vector2(-13, 15);
        label.ZIndex = 110;
        label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(label);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(label, "position", label.Position + new Vector2(0, -52), 0.72)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0.0f, 0.72).SetDelay(0.18);
        tween.Finished += label.QueueFree;
    }

    public void ShowBanner(string value, Color color, double seconds = 1.0)
    {
        _centerBanner.Text = value;
        _centerBanner.AddThemeColorOverride("font_color", color);
        _centerBanner.Visible = true;
        _centerBanner.Modulate = Colors.White;
        _centerBanner.Scale = new Vector2(0.92f, 0.92f);
        _centerBanner.PivotOffset = Size * 0.5f;
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(_centerBanner, "scale", Vector2.One, 0.18)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_centerBanner, "modulate:a", 0.0f, 0.36).SetDelay(Math.Max(0.2, seconds - 0.36));
        tween.Finished += () => _centerBanner.Visible = false;
    }

    private static Vector2 CenterOf(PawnView pawn)
        => pawn.Position + PawnView.ViewSize * new Vector2(0.5f, 0.48f);

    private void LayoutActors()
    {
        if (Size.X < 100 || Size.Y < 100) return;
        if (_setupMode)
        {
            for (int slot = 0; slot < _slots.Length; slot++)
            {
                _slots[slot].Size = new Vector2(138, 168);
                _slots[slot].Position = SetupTopLeft(slot, _slots[slot].Size);
            }
        }
        else
        {
            foreach (PawnView pawn in _pawns.Values)
            {
                pawn.Size = PawnView.ViewSize;
                pawn.SetHome(PawnTopLeft(pawn.Team, pawn.Slot, PawnView.ViewSize));
            }
        }
    }

    private Vector2 SetupTopLeft(int slot, Vector2 viewSize)
    {
        float left = Math.Max(28, Size.X * 0.08f);
        float right = Math.Min(Size.X - viewSize.X - 28, Size.X * 0.73f);
        float center = (left + right) * 0.5f;
        float top = Math.Max(116, Size.Y * 0.17f);
        float bottom = Math.Min(Size.Y - viewSize.Y - 34, Size.Y * 0.66f);
        float mid = (top + bottom) * 0.5f;
        return slot switch
        {
            0 => new Vector2(right, top),
            1 => new Vector2(right, bottom),
            2 => new Vector2(center, mid),
            3 => new Vector2(left, top),
            _ => new Vector2(left, bottom),
        };
    }

    private Vector2 PawnTopLeft(int team, int slot, Vector2 viewSize)
    {
        float x = team == 0 ? PlayerX(slot) : 1.0f - PlayerX(slot);
        float y = SlotY(slot);
        return new Vector2(x * Size.X - viewSize.X * 0.5f, y * Size.Y - viewSize.Y * 0.5f);
    }

    private static float PlayerX(int slot) => slot switch
    {
        0 or 1 => 0.42f,
        2 => 0.27f,
        3 or 4 => 0.12f,
        5 or 6 => 0.195f,
        7 => 0.42f,
        8 => 0.12f,
        _ => 0.27f,
    };

    private static float SlotY(int slot) => slot switch
    {
        0 or 3 or 5 => 0.39f,
        1 or 4 or 6 => 0.72f,
        7 or 8 or 2 => 0.555f,
        _ => 0.555f,
    };
}

public partial class PawnView : Control
{
    public static readonly Vector2 ViewSize = new(116, 162);

    private Sprite2D _portrait = null!;
    private Label _name = null!;
    private Label _stats = null!;
    private Label _status = null!;
    private ProgressBar _hp = null!;
    private StyleBoxFlat _frame = null!;
    private Vector2 _home;
    private double _idle;
    private bool _dead;

    public int InstanceId { get; private set; }
    public int Team { get; private set; }
    public int Slot { get; set; }
    public int Hp { get; private set; }
    public int MaxHp { get; private set; }
    public int AttackValue { get; private set; }
    public AttackPattern Pattern { get; private set; }
    public string UnitName { get; private set; } = "";

    public void Configure(DemoOpening opening, Texture2D atlas)
    {
        InstanceId = opening.InstanceId;
        Team = opening.Team;
        Slot = opening.Slot;
        Hp = opening.Hp;
        MaxHp = Math.Max(1, opening.MaxHp);
        AttackValue = opening.Attack;
        Pattern = opening.Pattern;
        UnitName = opening.Name;
        Size = ViewSize;
        MouseFilter = MouseFilterEnum.Ignore;
        _idle = (UiKit.StableHash(opening.UnitId) % 360) * Math.PI / 180.0;

        Color teamColor = Team == 0 ? UiKit.Player : UiKit.Enemy;
        var shadow = new PanelContainer
        {
            Position = new Vector2(5, 8),
            Size = new Vector2(106, 132),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _frame = UiKit.Box(new Color(0.035f, 0.06f, 0.05f, 0.88f), teamColor, 2, 18);
        shadow.AddThemeStyleboxOverride("panel", _frame);
        AddChild(shadow);

        _portrait = new Sprite2D
        {
            Texture = UiKit.BattlePortrait(atlas, opening.UnitId),
            Position = new Vector2(58, 55),
            SelfModulate = UiKit.PortraitTint(opening.UnitId, Team == 1),
            Material = UiKit.PortraitCanvasMaterial(opening.UnitId),
        };
        Texture2D portrait = _portrait.Texture;
        float portraitScale = Math.Min(100.0f / Math.Max(1, portrait.GetWidth()), 100.0f / Math.Max(1, portrait.GetHeight()));
        _portrait.Scale = new Vector2(portraitScale, portraitScale);
        AddChild(_portrait);

        _status = UiKit.Text("", 11, UiKit.Gold);
        _status.Position = new Vector2(-6, -13);
        _status.Size = new Vector2(128, 24);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.AddThemeConstantOverride("outline_size", 5);
        _status.AddThemeColorOverride("font_outline_color", Colors.Black);
        _status.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_status);

        _name = UiKit.Text(opening.Name, 11, Colors.White);
        _name.Position = new Vector2(-10, 105);
        _name.Size = new Vector2(136, 23);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _name.AddThemeConstantOverride("outline_size", 5);
        _name.AddThemeColorOverride("font_outline_color", Colors.Black);
        _name.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_name);

        _hp = new ProgressBar
        {
            Position = new Vector2(7, 130),
            Size = new Vector2(102, 9),
            MinValue = 0,
            MaxValue = MaxHp,
            Value = Hp,
            ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _hp.AddThemeStyleboxOverride("background", UiKit.Box(new Color(0.03f, 0.04f, 0.035f, 0.95f), Colors.Black, 1, 3));
        _hp.AddThemeStyleboxOverride("fill", UiKit.Box(teamColor, null, 0, 3));
        AddChild(_hp);

        _stats = UiKit.Text($"{Hp}/{MaxHp}   攻{AttackValue}   {UiKit.PatternLabel(Pattern)}", 9, UiKit.Ink);
        _stats.Position = new Vector2(-8, 140);
        _stats.Size = new Vector2(132, 20);
        _stats.HorizontalAlignment = HorizontalAlignment.Center;
        _stats.AddThemeConstantOverride("outline_size", 4);
        _stats.AddThemeColorOverride("font_outline_color", Colors.Black);
        _stats.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_stats);
    }

    public void SetHome(Vector2 home)
    {
        _home = home;
        if (!HasMeta("moving")) Position = home;
    }

    public void SetHp(int hp)
    {
        Hp = Math.Clamp(hp, 0, MaxHp);
        _hp.Value = Hp;
        RefreshStats();
    }

    public void SetAttack(int attack, AttackPattern? pattern = null)
    {
        AttackValue = attack;
        if (pattern is { } p) Pattern = p;
        RefreshStats();
    }

    public void SetStatus(string value) => _status.Text = value;

    private void RefreshStats()
        => _stats.Text = $"{Hp}/{MaxHp}   攻{AttackValue}   {UiKit.PatternLabel(Pattern)}";

    public void AnimateAttack(Vector2 direction)
    {
        if (_dead) return;
        Vector2 start = _home;
        Vector2 thrust = start + direction * 24;
        var tween = CreateTween();
        tween.TweenProperty(this, "position", thrust, 0.09).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "position", start, 0.16).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public void AnimateHit()
    {
        if (_dead) return;
        Color before = Modulate;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", new Color(1.55f, 0.42f, 0.36f, 1), 0.06);
        tween.TweenProperty(this, "modulate", before, 0.18);
        var shake = CreateTween();
        shake.TweenProperty(_portrait, "position:x", 52.0f, 0.04);
        shake.TweenProperty(_portrait, "position:x", 64.0f, 0.04);
        shake.TweenProperty(_portrait, "position:x", 58.0f, 0.06);
    }

    public void AnimateHeal()
    {
        if (_dead) return;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", new Color(0.62f, 1.45f, 0.85f, 1), 0.10);
        tween.TweenProperty(this, "modulate", Colors.White, 0.26);
    }

    public void AnimateDeath()
    {
        if (_dead) return;
        _dead = true;
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "rotation", Team == 0 ? -0.22f : 0.22f, 0.45)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        tween.TweenProperty(this, "modulate", new Color(0.25f, 0.25f, 0.25f, 0.20f), 0.48);
        tween.TweenProperty(this, "position:y", Position.Y + 22, 0.48);
    }

    public void AnimateRevive()
    {
        _dead = false;
        Rotation = 0;
        Modulate = new Color(0.55f, 1.25f, 0.80f, 0);
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", Colors.White, 0.42)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    public void AnimateAppear()
    {
        Scale = new Vector2(0.42f, 0.42f);
        PivotOffset = ViewSize * 0.5f;
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "scale", Vector2.One, 0.38)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "modulate", Colors.White, 0.28);
    }

    public void AnimateMove(Vector2 target)
    {
        _home = target;
        SetMeta("moving", true);
        var tween = CreateTween();
        tween.TweenProperty(this, "position", target, 0.34)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.Finished += () => RemoveMeta("moving");
    }

    public override void _Process(double delta)
    {
        if (_dead || _portrait is null) return;
        _idle += delta * (Team == 0 ? 2.1 : 1.9);
        _portrait.Position = new Vector2(_portrait.Position.X, 55 + (float)Math.Sin(_idle) * 2.2f);
    }
}

public partial class FxLayer : Control
{
    private sealed class Beam
    {
        public Vector2 From;
        public Vector2 To;
        public Color Color;
        public float Width;
        public double Life = 0.38;
        public double MaxLife = 0.38;
    }

    private readonly List<Beam> _beams = new();

    private sealed class SweepFx
    {
        public Vector2 Origin;
        public List<Vector2> Targets = new();
        public Color Color;
        public double Life = 0.54;
        public double MaxLife = 0.54;
    }

    private sealed class PierceFx
    {
        public Vector2 Origin;
        public Vector2 End;
        public List<Vector2> Targets = new();
        public Color Color;
        public double Life = 0.58;
        public double MaxLife = 0.58;
    }

    private sealed class VolleyFx
    {
        public Vector2 Origin;
        public List<Vector2> Targets = new();
        public Color Color;
        public double Life = 0.64;
        public double MaxLife = 0.64;
    }

    private sealed class ImpactFx
    {
        public Vector2 Center;
        public Color Color;
        public bool Status;
        public double Life = 0.42;
        public double MaxLife = 0.42;
    }

    private readonly List<SweepFx> _sweeps = new();
    private readonly List<PierceFx> _pierces = new();
    private readonly List<VolleyFx> _volleys = new();
    private readonly List<ImpactFx> _impacts = new();

    public void AddBeam(Vector2 from, Vector2 to, Color color, float width)
    {
        _beams.Add(new Beam { From = from, To = to, Color = color, Width = width });
        QueueRedraw();
    }

    public void AddSweep(Vector2 origin, IReadOnlyList<Vector2> targets, Color color)
    {
        _sweeps.Add(new SweepFx { Origin = origin, Targets = targets.ToList(), Color = color });
        QueueRedraw();
    }

    public void AddPierce(Vector2 origin, Vector2 primary, IReadOnlyList<Vector2> targets, Color color)
    {
        Vector2 direction = (primary - origin).Normalized();
        if (direction == Vector2.Zero) direction = Vector2.Right;
        float farthest = Math.Max(80, (primary - origin).Dot(direction));
        foreach (Vector2 target in targets)
            farthest = Math.Max(farthest, (target - origin).Dot(direction));
        _pierces.Add(new PierceFx
        {
            Origin = origin,
            End = origin + direction * (farthest + 86),
            Targets = targets.ToList(),
            Color = color,
        });
        QueueRedraw();
    }

    public void AddVolley(Vector2 origin, IReadOnlyList<Vector2> targets, Color color)
    {
        _volleys.Add(new VolleyFx { Origin = origin, Targets = targets.ToList(), Color = color });
        QueueRedraw();
    }

    public void AddImpact(Vector2 center, Color color, bool status)
    {
        _impacts.Add(new ImpactFx { Center = center, Color = color, Status = status });
        QueueRedraw();
    }

    public void ClearEffects()
    {
        _beams.Clear();
        _sweeps.Clear();
        _pierces.Clear();
        _volleys.Clear();
        _impacts.Clear();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        bool changed = false;
        for (int i = _beams.Count - 1; i >= 0; i--)
        {
            _beams[i].Life -= delta;
            if (_beams[i].Life <= 0) _beams.RemoveAt(i);
            changed = true;
        }
        changed |= Advance(_sweeps, delta, fx => fx.Life, (fx, life) => fx.Life = life);
        changed |= Advance(_pierces, delta, fx => fx.Life, (fx, life) => fx.Life = life);
        changed |= Advance(_volleys, delta, fx => fx.Life, (fx, life) => fx.Life = life);
        changed |= Advance(_impacts, delta, fx => fx.Life, (fx, life) => fx.Life = life);
        if (changed) QueueRedraw();
    }

    private static bool Advance<T>(List<T> effects, double delta, Func<T, double> getLife, Action<T, double> setLife)
    {
        bool changed = effects.Count > 0;
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            double life = getLife(effects[i]) - delta;
            setLife(effects[i], life);
            if (life <= 0) effects.RemoveAt(i);
        }
        return changed;
    }

    public override void _Draw()
    {
        foreach (Beam beam in _beams)
        {
            float k = (float)(beam.Life / beam.MaxLife);
            Color glow = beam.Color;
            glow.A = k * 0.22f;
            DrawLine(beam.From, beam.To, glow, beam.Width + 8, true);
            Color core = beam.Color;
            core.A = Math.Min(1.0f, k * 1.8f);
            DrawLine(beam.From, beam.To, core, beam.Width, true);
            Vector2 dir = (beam.To - beam.From).Normalized();
            if (dir == Vector2.Zero) continue;
            Vector2 normal = new(-dir.Y, dir.X);
            float size = 6 + beam.Width;
            DrawColoredPolygon(new[]
            {
                beam.To,
                beam.To - dir * size * 2 + normal * size,
                beam.To - dir * size * 2 - normal * size,
            }, core);
        }

        foreach (SweepFx sweep in _sweeps) DrawSweep(sweep);
        foreach (PierceFx pierce in _pierces) DrawPierce(pierce);
        foreach (VolleyFx volley in _volleys) DrawVolley(volley);
        foreach (ImpactFx impact in _impacts) DrawImpact(impact);
    }

    private void DrawSweep(SweepFx fx)
    {
        if (fx.Targets.Count == 0) return;
        float k = (float)(fx.Life / fx.MaxLife);
        float progress = 1.0f - k;
        Vector2 aim = fx.Targets.Aggregate(Vector2.Zero, (sum, p) => sum + p) / fx.Targets.Count;
        float baseAngle = (aim - fx.Origin).Angle();
        float min = 0;
        float max = 0;
        float distance = 80;
        foreach (Vector2 target in fx.Targets)
        {
            Vector2 offset = target - fx.Origin;
            float relative = Mathf.Wrap(offset.Angle() - baseAngle, -Mathf.Pi, Mathf.Pi);
            min = Math.Min(min, relative);
            max = Math.Max(max, relative);
            distance = Math.Max(distance, offset.Length());
        }
        float spread = Math.Max(0.17f, (max - min) * 0.5f + 0.10f);
        float center = baseAngle + (min + max) * 0.5f;
        float start = center - spread;
        float end = center + spread;
        float radius = distance * (0.76f + progress * 0.18f);

        Color wash = fx.Color;
        wash.A = k * 0.13f;
        DrawColoredPolygon(new[]
        {
            fx.Origin,
            fx.Origin + Direction(start) * radius,
            fx.Origin + Direction(end) * radius,
        }, wash);

        Color glow = fx.Color;
        glow.A = k * 0.28f;
        DrawArc(fx.Origin, radius, start, end, 32, glow, 17, true);
        Color core = fx.Color;
        core.A = Math.Min(1.0f, k * 1.9f);
        DrawArc(fx.Origin, radius, start, end, 32, core, 5.5f, true);
        DrawArc(fx.Origin, Math.Max(8, radius - 17), start + 0.03f, end - 0.03f, 30, new Color(core, core.A * 0.55f), 2.2f, true);

        foreach (Vector2 target in fx.Targets)
            DrawCircle(target, 13 + progress * 18, new Color(core, k * 0.62f), false, 3.0f, true);
    }

    private void DrawPierce(PierceFx fx)
    {
        float k = (float)(fx.Life / fx.MaxLife);
        float progress = 1.0f - k;
        float travel = Math.Min(1.0f, progress * 1.8f);
        Vector2 full = fx.End - fx.Origin;
        Vector2 direction = full.Normalized();
        Vector2 normal = new(-direction.Y, direction.X);
        Vector2 head = fx.Origin.Lerp(fx.End, travel);

        Color glow = fx.Color;
        glow.A = k * 0.24f;
        DrawLine(fx.Origin, head, glow, 24, true);
        Color core = fx.Color;
        core.A = Math.Min(1.0f, k * 2.1f);
        DrawLine(fx.Origin, head, core, 6.5f, true);
        Color whiteCore = Colors.White;
        whiteCore.A = core.A * 0.72f;
        DrawLine(fx.Origin, head, whiteCore, 2.0f, true);
        DrawColoredPolygon(new[]
        {
            head + direction * 17,
            head - direction * 12 + normal * 11,
            head - direction * 12 - normal * 11,
        }, core);

        float reached = full.Length() * travel + 10;
        foreach (Vector2 target in fx.Targets)
        {
            if ((target - fx.Origin).Dot(direction) > reached) continue;
            DrawCircle(target, 18 + progress * 14, new Color(core, k * 0.7f), false, 4, true);
            DrawLine(target - normal * 22, target + normal * 22, new Color(core, k * 0.42f), 3, true);
        }
    }

    private void DrawVolley(VolleyFx fx)
    {
        float k = (float)(fx.Life / fx.MaxLife);
        float progress = 1.0f - k;
        Color color = fx.Color;
        foreach (Vector2 target in fx.Targets)
        {
            Color ray = color;
            ray.A = k * 0.22f;
            DrawLine(fx.Origin, fx.Origin.Lerp(target, Math.Min(1, progress * 2)), ray, 8, true);
            DrawCircle(target, 16 + progress * 28, new Color(color, k * 0.72f), false, 4.5f, true);
        }
        DrawCircle(fx.Origin, 24 + progress * 58, new Color(color, k * 0.58f), false, 5, true);
    }

    private void DrawImpact(ImpactFx fx)
    {
        float k = (float)(fx.Life / fx.MaxLife);
        float progress = 1.0f - k;
        Color color = new(fx.Color, Math.Min(1.0f, k * 1.7f));
        DrawCircle(fx.Center, 12 + progress * 26, color, false, fx.Status ? 3.0f : 4.5f, true);
        if (fx.Status)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.Tau / 6 + progress;
                Vector2 p = fx.Center + Direction(angle) * (17 + progress * 20);
                DrawCircle(p, 3.5f + progress * 2, new Color(color, k * 0.72f));
            }
        }
        else
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 dir = Direction(i * Mathf.Tau / 8 + 0.2f);
                DrawLine(fx.Center + dir * 11, fx.Center + dir * (25 + progress * 22), new Color(color, k * 0.72f), 2.2f, true);
            }
        }
    }

    private static Vector2 Direction(float angle) => new(Mathf.Cos(angle), Mathf.Sin(angle));
}
