using Godot;

/// <summary>
/// 駒の頭上の札（HP バー・名前・攻撃・状態アイコン）を画面の平面に描く。<b>表示専用。</b>
///
/// <para>3D の板（Label3D）に描いていたころは、遠い駒ほど縮み、立ち絵の頭に数字が重なって読めなかった。
/// ここでは駒の頭の位置を `Camera3D.UnprojectPosition` で画面へ写し、<b>画面上の大きさを固定</b>して描く。
/// 位置合わせは `BattlefieldView3D` が毎フレーム行う（駒はカメラを知らない）。</para>
///
/// <para><b>常時出すのはバー・状態アイコン・攻撃の増減だけ。</b>名前・HP の数字・攻撃は
/// 詳細（<see cref="Expanded"/>）に畳む——同名の農兵が9体並ぶと、文字の塊が駒より目立つため。
/// 詳細はカーソルを合わせた駒と、Alt を押している間の全員に出る（`BattlefieldView3D.PawnHuds`）。
/// 攻撃の増減は観察の対象なので畳まない。</para>
///
/// 原点は札の下辺の中央（＝駒の頭の真上）。
/// </summary>
public partial class PawnHud2D : Control
{
    private static readonly Color OutlineColor = new(0.005f, 0.01f, 0.008f, 0.9f);

    private readonly float _barWidth;
    private readonly float _barHeight;
    private readonly ColorRect _barTrail;
    private readonly ColorRect _barFill;
    private readonly ColorRect _scar;
    private int _openingMaxHp;
    internal float ScarFraction => _openingMaxHp == 0 ? 0 : 1f - _maxHp / (float)_openingMaxHp;
    private readonly PanelContainer _detail;
    private readonly Label _name;
    private readonly Label _numbers;
    private readonly Label _delta;
    private readonly Label _forecast;
    private int _hp, _maxHp, _attack;
    private string _pattern = "";
    private float _ratio = 1;
    private readonly Color _team;
    private Vector2 _nudge;
    private Tween? _trailTween;
    private Tween? _fadeTween;

    public StatusIconRow2D Icons { get; }
    public string DeltaText => _delta.Text;
    public bool DeltaVisible => _delta.Visible;
    public string ForecastText => _forecast.Text;
    /// <summary>駒の生死・勝利絵で出し入れする（画面外・カメラの裏は別に `BattlefieldView3D` が切る）。</summary>
    public bool Shown { get; private set; } = true;

    /// <summary>名前・HP の数字・攻撃を出しているか（カーソルを合わせた駒・Alt の間の全員）。</summary>
    public bool Expanded
    {
        get => _detail.Visible;
        set
        {
            if (_detail.Visible == value) return;
            _detail.Visible = value;
            Layout();
        }
    }

    public PawnHud2D(bool enemy, string name)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _barWidth = enemy ? 96 : 76;
        _barHeight = enemy ? 9 : 6;
        Color team = enemy ? UiKit.Enemy : UiKit.Player;
        _team = team;

        Rect(new Color(0.015f, 0.025f, 0.02f, 0.82f), -_barWidth * 0.5f - 1, -_barHeight - 1, _barWidth + 2, _barHeight + 2);
        _barTrail = Rect(new Color(1, 0.93f, 0.82f, 0.85f), -_barWidth * 0.5f, -_barHeight, _barWidth, _barHeight);
        _barFill = Rect(team.Lightened(0.08f), -_barWidth * 0.5f, -_barHeight, _barWidth, _barHeight);
        _scar = Rect(new Color("5f355f"), _barWidth * 0.5f, -_barHeight, 0, _barHeight);
        _scar.Draw += () => {
            for (float x = 1; x < _scar.Size.X; x += 5)
                _scar.DrawLine(new Vector2(x, 0), new Vector2(Mathf.Min(x + 3, _scar.Size.X), _barHeight), new Color("b384bd"), 1);
        };

        _detail = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        StyleBoxFlat box = UiKit.Box(new Color(0.02f, 0.035f, 0.03f, 0.82f), new Color(team, 0.55f), 1, 4);
        box.ContentMarginLeft = box.ContentMarginRight = 7;
        box.ContentMarginTop = 2;
        box.ContentMarginBottom = 3;
        _detail.AddThemeStyleboxOverride("panel", box);
        AddChild(_detail);
        var lines = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", -2);
        _detail.AddChild(lines);
        _name = UiKit.Text(name, 13, Colors.White);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        lines.AddChild(_name);
        _numbers = UiKit.Text("", 12, UiKit.Ink);
        _numbers.HorizontalAlignment = HorizontalAlignment.Center;
        lines.AddChild(_numbers);

        _delta = MakeLabel("", 13, UiKit.Hurt);
        _delta.Visible = false;
        _delta.Position = new Vector2(_barWidth * 0.5f + 4, -_barHeight * 0.5f - 10);

        Icons = new StatusIconRow2D();
        AddChild(Icons);

        // 溜めの予告（第125期 段3-b）。ターンをまたいで残るので、状態アイコンの列のさらに上に置く。
        _forecast = MakeLabel("", 13, UiKit.Gold);
        _forecast.Size = new Vector2(220, 18);
        _forecast.HorizontalAlignment = HorizontalAlignment.Center;
        _forecast.Visible = false;

        Layout();
    }

    public void SetForecast(string value)
    {
        _forecast.Text = value;
        _forecast.Visible = !string.IsNullOrWhiteSpace(value);
    }

    public override void _Process(double delta) => Layout();

    /// <summary>
    /// 札が画面上で占める範囲（原点＝札の下辺の中央からの相対）。重なり回避（`BattlefieldView3D.PawnHuds`）が読む。
    /// </summary>
    public Rect2 Footprint
    {
        get
        {
            Layout();
            float half = _barWidth * 0.5f + 1;
            float top = -_barHeight - 1;
            if (_detail.Visible)
            {
                half = Mathf.Max(half, _detail.Size.X * 0.5f);
                top = _detail.Position.Y;
            }
            if (Icons.Rows > 0)
            {
                half = Mathf.Max(half, Icons.Width * 0.5f);
                top = Icons.Position.Y - StatusIconRow2D.IconSize - (Icons.Rows - 1) * (StatusIconRow2D.IconSize + 2);
            }
            if (_forecast.Visible)
            {
                half = Mathf.Max(half, _forecast.GetMinimumSize().X * 0.5f);
                top = _forecast.Position.Y;
            }
            float right = half;
            if (_delta.Visible) right = Mathf.Max(right, _delta.Position.X + _delta.GetMinimumSize().X);
            return new Rect2(-half, top, half + right, 1 - top);
        }
    }

    /// <summary>
    /// 頭の真上からずらした量（画面の px）。重なり回避が決める。
    /// 大きくずれたときは、札から頭へ細い線を引いて持ち主を示す。
    /// </summary>
    public Vector2 Nudge
    {
        get => _nudge;
        set
        {
            bool before = _nudge.LengthSquared() > 36, after = value.LengthSquared() > 36;
            _nudge = value;
            if (before || after) QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_nudge.LengthSquared() <= 36) return;
        // 原点（バーの下）から、本来の頭の位置（−Nudge）へ。
        Color line = new(_team, 0.75f);
        DrawLine(new Vector2(0, 1), -_nudge, new Color(0, 0, 0, 0.55f), 3f, true);
        DrawLine(new Vector2(0, 1), -_nudge, line, 1.4f, true);
        DrawCircle(-_nudge, 2.6f, line);
    }

    /// <summary>下から バー → 詳細 → 状態アイコン → 予告 の順に積む。</summary>
    private void Layout()
    {
        float top = -_barHeight - 2;
        if (_detail.Visible)
        {
            Vector2 size = _detail.GetCombinedMinimumSize();
            _detail.Size = size;
            _detail.Position = new Vector2(-size.X * 0.5f, top - 2 - size.Y).Round();
            top = _detail.Position.Y;
        }
        Icons.Position = new Vector2(0, top - 3);
        if (_forecast.Visible)
            _forecast.Position = new Vector2(-110, Icons.Position.Y - Icons.Rows * (StatusIconRow2D.IconSize + 2) - 20);
    }

    public void SetHp(int hp, int maxHp)
    {
        if (_openingMaxHp == 0) _openingMaxHp = System.Math.Max(1, maxHp);
        _hp = hp;
        _maxHp = maxHp;
        RefreshNumbers();
        float capacity = Mathf.Clamp(maxHp / (float)_openingMaxHp, 0f, 1f);
        _scar.Position = new Vector2(_barWidth * (capacity - 0.5f), -_barHeight);
        _scar.Size = new Vector2(_barWidth * (1 - capacity), _barHeight);
        _scar.QueueRedraw();
        QueueRedraw();
        float ratio = Mathf.Clamp(hp / (float)_openingMaxHp, 0f, capacity);
        _barFill.Size = new Vector2(_barWidth * ratio, _barHeight);
        // 削れた分を一瞬だけ白く残して、どれだけ減ったかを目で追えるようにする。
        _trailTween?.Kill();
        if (ratio < _ratio && IsInsideTree())
        {
            _trailTween = CreateTween();
            _trailTween.TweenProperty(_barTrail, "size:x", _barWidth * ratio, 0.35).SetDelay(0.22)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        else _barTrail.Size = new Vector2(_barWidth * ratio, _barHeight);
        _ratio = ratio;
    }

    public void SetAttack(int attack, string pattern)
    {
        _attack = attack;
        _pattern = pattern.Trim('《', '》');
        RefreshNumbers();
    }

    private void RefreshNumbers() => _numbers.Text = $"HP {_hp}/{_maxHp}  攻{_attack} {_pattern}";

    public void SetAttackDelta(string text, Color color, bool visible)
    {
        _delta.Text = text;
        _delta.AddThemeColorOverride("font_color", color);
        _delta.Visible = visible;
    }

    /// <summary>倒れた・勝利絵に替わった・板で吹き飛んだ。<paramref name="fade"/> なら短く消える。</summary>
    public void Retire(bool fade)
    {
        Shown = false;
        _fadeTween?.Kill();
        if (fade && IsInsideTree())
        {
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(this, "modulate:a", 0f, 0.28);
        }
        else Modulate = new Color(1, 1, 1, 0);
    }

    public void Restore()
    {
        Shown = true;
        _fadeTween?.Kill();
        Modulate = Colors.White;
    }

    private ColorRect Rect(Color color, float x, float y, float w, float h)
    {
        var rect = new ColorRect { Color = color, Position = new Vector2(x, y), Size = new Vector2(w, h), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(rect);
        return rect;
    }

    private Label MakeLabel(string text, int size, Color color)
    {
        Label label = UiKit.Text(text, size, color);
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeColorOverride("font_outline_color", OutlineColor);
        label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(label);
        return label;
    }
}
