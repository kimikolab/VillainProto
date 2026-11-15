using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

// 3D のジオラマの駒とマスの台（発注書 §2-2 ／ §2-3）。<b>駒の見た目はこのファイルの `Map3DPiece` 1 つにまとめてある</b>
// ——差し替えるときは、`Show`（何を見せるか）・`Snap` ／ `Walk`（どこへ動くか）・`Selected` の 4 つの口を保ったまま中身を替える。
// 盤の規則は 1 行も読まない（呼ぶ側が名前・数・HP・位置を渡す）。

/// <summary>駒 1 つに見せる中身（呼ぶ側が盤から読んで渡す・駒は計算しない）。</summary>
internal readonly record struct Map3DPieceInfo(
    string Name, string? UnitId, bool Enemy, bool Hero, int Count, double HpPercent,
    string Note, Color NoteColor, bool Urgent, string Glyph, string Reward = "", bool Prize = false);

/// <summary>3D の層の文字（遊ぶ UI・名札）。1920×1080 を 1280×720 に縮めても読める大きさは呼ぶ側が決める。</summary>
internal static class Map3DFonts
{
    private static readonly string[] Names = { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" };
    internal static readonly SystemFont Regular = new() { FontNames = Names, AllowSystemFallback = true, FontWeight = 500 };
    internal static readonly SystemFont Bold = new() { FontNames = Names, AllowSystemFallback = true, FontWeight = 700 };
}

/// <summary>
/// 隊の駒。味方は<b>立ち絵の板＋台座＋名札</b>、敵部隊は<b>旗＋数＋名札</b>。名札は画面の平面（2D）に出すので、拡大しても文字の大きさは変わらない。
/// </summary>
public partial class Map3DPiece : Node3D
{
    // ポンが触る値（駒の大きさ）。
    internal const float BoardHeight = 4.6f;      // 立ち絵の板の高さ（基準の体格 2.5 の駒）
    internal const float BoardLean = 24f;         // 板を奥へ倒す角度（見下ろしのカメラに顔を向ける）
    internal const float BaseRadius = 1.10f;      // 台座の半径
    internal const float FlagHeight = 4.3f;       // 敵の旗の竿の高さ
    internal const float WalkSeconds = 0.4f;      // 1 マス歩く時間

    private static readonly Color AllyColor = Color.FromHtml("#55d5b8");
    private static readonly Color FoeColor = Color.FromHtml("#e96951");
    private static readonly Color HeroColor = Color.FromHtml("#f1c96b");
    private static readonly Dictionary<string, Texture2D?> Boards = new(StringComparer.Ordinal);

    private readonly Node3D _body = new();          // 歩くときに跳ねる部分（台座は跳ねない）
    private readonly Node3D _lean = new();          // 奥へ倒す部分（板・旗）
    private readonly MeshInstance3D _base = new();
    private readonly MeshInstance3D _rim = new();
    private readonly MeshInstance3D _ring = new();  // 選んだ隊の光る輪
    private readonly StandardMaterial3D _baseMaterial = new() { Roughness = 0.75f };
    private readonly StandardMaterial3D _rimMaterial = new() { Roughness = 0.5f, EmissionEnabled = true };
    private readonly StandardMaterial3D _ringMaterial = new()
    {
        AlbedoColor = HeroColor, EmissionEnabled = true, Emission = HeroColor, EmissionEnergyMultiplier = 2.6f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };
    private readonly Map3DPlate _plate = new();
    private Camera3D? _camera;
    private string _shape = "";
    private Vector3[] _path = Array.Empty<Vector3>();
    private float _pathLength, _walk = -1, _walkFor, _clock;
    private Vector3 _target;
    private float _top = BoardHeight;

    /// <summary>名札（2D）。置く場所は呼ぶ側が決める——台・橋・旗・ほかの名札と重ならない所を探すため（`Map11Main.PlaceLabels3D`）。</summary>
    internal Map3DPlate Plate => _plate;
    internal float Clock => _clock;
    /// <summary>歩き終えたときの位置。</summary>
    internal Vector3 Target => _target;
    internal bool Shown { get; private set; }
    internal bool Walking => _walk >= 0;
    internal bool Selected
    {
        get => _ring.Visible;
        set { _ring.Visible = value; _plate.Selected = value; }
    }

    internal static Map3DPiece Create(Node3D world, Control plates, Func<Camera3D> camera)
    {
        var piece = new Map3DPiece();
        world.AddChild(piece);
        plates.AddChild(piece._plate);
        piece._cameraOf = camera;
        return piece;
    }

    private Func<Camera3D>? _cameraOf;

    public override void _Ready()
    {
        _base.Mesh = new CylinderMesh { TopRadius = BaseRadius * 0.94f, BottomRadius = BaseRadius, Height = 0.26f, RadialSegments = 28 };
        _base.Position = new Vector3(0, 0.13f, 0);
        _base.MaterialOverride = _baseMaterial;
        AddChild(_base);
        _rim.Mesh = new TorusMesh { InnerRadius = BaseRadius * 0.86f, OuterRadius = BaseRadius * 0.98f, Rings = 28, RingSegments = 6 };
        _rim.Position = new Vector3(0, 0.26f, 0);
        _rim.MaterialOverride = _rimMaterial;
        _rim.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_rim);
        _ring.Mesh = new TorusMesh { InnerRadius = BaseRadius * 1.22f, OuterRadius = BaseRadius * 1.36f, Rings = 36, RingSegments = 6 };
        _ring.Position = new Vector3(0, 0.10f, 0);
        _ring.MaterialOverride = _ringMaterial;
        _ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _ring.Visible = false;
        AddChild(_ring);
        AddChild(_body);
        _lean.RotationDegrees = new Vector3(-BoardLean, 0, 0);
        _lean.Position = new Vector3(0, 0.26f, 0);
        _body.AddChild(_lean);
        Visible = false;
        _plate.Visible = false;
    }

    /// <summary>何を見せるか。<b>形（誰の板か・敵の旗か）が変わったときだけ作り直す。</b></summary>
    internal void Show(in Map3DPieceInfo info)
    {
        Shown = Visible = true;
        string shape = $"{info.Enemy}/{info.Hero}/{info.UnitId}/{info.Glyph}/{(info.Enemy ? info.Count : 0)}";
        if (shape != _shape)
        {
            _shape = shape;
            foreach (Node child in _lean.GetChildren()) { _lean.RemoveChild(child); child.QueueFree(); }
            foreach (Node3D peg in _pegs) peg.QueueFree();
            _pegs.Clear();
            Color team = info.Hero ? HeroColor : info.Enemy ? FoeColor : AllyColor;
            _baseMaterial.AlbedoColor = team.Darkened(0.72f);
            _rimMaterial.AlbedoColor = team;
            _rimMaterial.Emission = team;
            _rimMaterial.EmissionEnergyMultiplier = 0.6f;
            if (info.Enemy) BuildFlag(info, team); else BuildBoard(info);
        }
        _plate.Set(info, info.Hero ? HeroColor : info.Enemy ? FoeColor : AllyColor);
    }

    internal void Hide()
    {
        Shown = Visible = false;
        _plate.Visible = false;
        _walk = -1;
    }

    // ---------------- 味方: 立ち絵の板 ----------------

    /// <summary>
    /// 立ち絵（戦闘の待機絵 → 編成の絵の順に探す）を、縮めて細かい段（ミップ）を付けて持つ。原本は読むだけ。
    /// 透過の無い絵は板にできないので null（顔の丸い札に落とす）。
    /// </summary>
    internal static Texture2D? BoardOf(string id)
    {
        if (Boards.TryGetValue(id, out Texture2D? cached)) return cached;
        foreach (string path in new[]
        {
            $"res://assets/portraits/battle/{id}_ready_right.png", $"res://assets/portraits/battle/{id}_idle_right.png",
            $"res://assets/portraits/{id}.png",
        })
        {
            if (!Godot.FileAccess.FileExists(path)) continue;
            using Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
            if (image.IsEmpty()) continue;
            image.Convert(Image.Format.Rgba8);
            if (image.GetPixel(2, 2).A > 0.1f && image.GetPixel(image.GetWidth() - 3, 2).A > 0.1f) continue;   // 背景つき
            if (image.GetHeight() > 720)
                image.Resize(Math.Max(1, image.GetWidth() * 720 / image.GetHeight()), 720, Image.Interpolation.Lanczos);
            image.GenerateMipmaps();
            return Boards[id] = ImageTexture.CreateFromImage(image);
        }
        return Boards[id] = null;
    }

    private void BuildBoard(in Map3DPieceInfo info)
    {
        string id = info.UnitId ?? "";
        if (id.Length > 0 && BoardOf(id) is { } board)
        {
            float height = BoardHeight * Math.Clamp(UiKit.PortraitWorldHeight(id) / 2.5f, 0.72f, 1.2f);
            float pad = Math.Clamp(UiKit.BattlePortraitBottomPaddingRatio(id), 0, 0.3f) * height;
            _lean.AddChild(new Sprite3D
            {
                Texture = board,
                PixelSize = height / board.GetHeight(),
                Position = new Vector3(0, height / 2 - pad, 0),
                Shaded = false,
                DoubleSided = true,
                AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
                Modulate = new Color(0.97f, 0.94f, 0.92f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            });
            _top = height - pad;
            return;
        }
        // 立ち絵が無い駒（空席の隊を含む）: 竿の先に顔の丸い札か、名前の頭の 1 文字。
        _top = 2.4f;
        Token(id.Length > 0 ? UnitFaceIcon.TextureFor(id) : null, info.Glyph, new Vector3(0, 1.7f, 0), 1.5f, AllyColor);
        _lean.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 1.0f, RadialSegments = 6 },
            Position = new Vector3(0, 0.5f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#3a3028") },
        });
    }

    /// <summary>丸い札（顔の絵か 1 文字）。</summary>
    private void Token(Texture2D? face, string glyph, Vector3 at, float diameter, Color tint)
    {
        _lean.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = diameter / 2, BottomRadius = diameter / 2, Height = 0.06f, RadialSegments = 28 },
            Position = at + new Vector3(0, 0, -0.035f),
            RotationDegrees = new Vector3(90, 0, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = tint.Darkened(0.7f), Roughness = 0.8f },
        });
        if (face is not null)
            _lean.AddChild(new Sprite3D
            {
                Texture = face, PixelSize = diameter * 0.94f / face.GetHeight(), Position = at + new Vector3(0, 0, 0.01f),
                Shaded = false, AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        else
            _lean.AddChild(Letter(glyph, at + new Vector3(0, 0, 0.02f), diameter * 0.62f, Colors.White));
    }

    private static Label3D Letter(string text, Vector3 at, float height, Color color) => new()
    {
        Text = text, Position = at, Font = Map3DFonts.Bold, FontSize = 96, PixelSize = height / 96f,
        Modulate = color, OutlineModulate = new Color(0.05f, 0.02f, 0.02f, 0.95f), OutlineSize = 22,
        Shaded = false, DoubleSided = false, NoDepthTest = false,
        AlphaCut = Label3D.AlphaCutMode.Discard,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    // ---------------- 敵: 旗 ＋ 数 ----------------

    private void BuildFlag(in Map3DPieceInfo info, Color team)
    {
        float k = info.Hero ? 1.22f : 1f;
        float pole = FlagHeight * k;
        _top = pole * 0.98f;
        var woodMaterial = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#3a2c24"), Roughness = 0.9f };
        _lean.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = pole, RadialSegments = 6 },
            Position = new Vector3(0, pole / 2, -0.05f), MaterialOverride = woodMaterial,
        });
        _lean.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.9f * k, 0.09f, 0.09f) },
            Position = new Vector3(0, pole - 0.12f, 0), MaterialOverride = woodMaterial,
        });
        // 垂れ幕（縁は暗い色）。夕暮れでも色が読めるよう少し光らせる。
        Color cloth = info.Hero ? Color.FromHtml("#c9982e") : Color.FromHtml("#b3342b");
        float width = 1.7f * k, drop = 2.25f * k;
        var clothMaterial = new StandardMaterial3D
        {
            AlbedoColor = cloth, Roughness = 0.85f, EmissionEnabled = true, Emission = cloth, EmissionEnergyMultiplier = 0.38f,
        };
        _lean.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(width, drop, 0.05f) },
            Position = new Vector3(0, pole - 0.16f - drop / 2, 0.02f), MaterialOverride = clothMaterial,
        });
        _lean.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(width + 0.14f, drop + 0.14f, 0.03f) },
            Position = new Vector3(0, pole - 0.16f - drop / 2, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = cloth.Darkened(0.6f) },
        });
        // 紋（先頭の敵の顔か、部隊の 1 文字）と、数。
        float crestY = pole - 0.16f - drop * 0.36f;
        Token(info.UnitId is { Length: > 0 } id ? UnitFaceIcon.TextureFor(id) : null, info.Glyph,
            new Vector3(0, crestY, 0.09f), 1.18f * k, info.Hero ? HeroColor : FoeColor);
        _lean.AddChild(Letter(info.Count.ToString(CultureInfo.InvariantCulture), new Vector3(0, pole - 0.16f - drop * 0.81f, 0.07f), 0.66f * k,
            Color.FromHtml("#fff1d8")));
        // 足元に兵の小さな駒を数だけ並べる（6 まで）。
        var soldier = new StandardMaterial3D { AlbedoColor = info.Hero ? Color.FromHtml("#d9b450") : Color.FromHtml("#8a3a34"), Roughness = 0.7f };
        int pegs = Math.Clamp(info.Count, 0, 6);
        for (int i = 0; i < pegs; i++)
        {
            float a = Mathf.DegToRad(90 + (i - (pegs - 1) / 2f) * 34);
            var at = new Vector3(Mathf.Cos(a) * BaseRadius * 0.66f, 0.24f, Mathf.Sin(a) * BaseRadius * 0.66f);
            var peg = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.15f, Height = 0.46f, RadialSegments = 8 },
                Position = at, MaterialOverride = soldier,
            };
            _body.AddChild(peg);
            _pegs.Add(peg);
        }
    }

    private readonly List<Node3D> _pegs = new();

    // ---------------- 動き ----------------

    /// <summary>その場に置く（滑らせない）。</summary>
    internal void Snap(Vector3 at)
    {
        Position = _target = at;
        _walk = -1;
        _body.Position = Vector3.Zero;
        _body.Rotation = Vector3.Zero;
    }

    /// <summary>
    /// 道に沿って歩く（発注書 §2-3・0.4 秒ほど）。<paramref name="path"/> は通る点の列で、最後の点に止まる。
    /// 歩いている最中に呼ばれたら、今いる所から続けて歩く。
    /// </summary>
    internal void Walk(IReadOnlyList<Vector3> path, float seconds = WalkSeconds)
    {
        if (path.Count == 0) return;
        Vector3 goal = path[^1];
        if (_target.IsEqualApprox(goal)) return;
        _target = goal;
        if (Position.DistanceTo(goal) < 0.02f) { Snap(goal); return; }
        var points = new List<Vector3> { Position };
        foreach (Vector3 p in path) if (points[^1].DistanceTo(p) > 0.05f) points.Add(p);
        _path = points.ToArray();
        _pathLength = Map3DLayout.LengthOf(_path);
        _walk = 0;
        _walkFor = Math.Max(0.05f, seconds);
    }

    public override void _Process(double delta)
    {
        if (!Shown) return;
        _clock += (float)delta;
        if (_walk >= 0)
        {
            _walk += (float)delta / _walkFor;
            float t = Mathf.Clamp(_walk, 0, 1);
            float eased = t * t * (3 - 2 * t);
            Position = Map3DLayout.Along(_path, _pathLength * eased);
            // 3 歩ぶん跳ねる。体は進む向きへ少し傾く。
            float hop = Mathf.Abs(Mathf.Sin(t * Mathf.Pi * 3));
            _body.Position = new Vector3(0, hop * 0.30f * Mathf.Sin(t * Mathf.Pi), 0);
            Vector3 ahead = Map3DLayout.Along(_path, Math.Min(_pathLength, _pathLength * eased + 0.4f)) - Position;
            _body.Rotation = new Vector3(0, 0, -Mathf.Clamp(ahead.X, -1, 1) * 0.10f * Mathf.Sin(t * Mathf.Pi));
            if (_walk >= 1) Snap(_target);
        }
        if (_ring.Visible)
        {
            float pulse = 1 + 0.06f * Mathf.Sin(_clock * 4.2f);
            _ring.Scale = new Vector3(pulse, 1, pulse);
            _ringMaterial.EmissionEnergyMultiplier = 2.2f + 0.9f * Mathf.Sin(_clock * 4.2f);
        }
    }

    /// <summary>画面の上で駒が占める四角（押す領域を重ねるために呼ぶ側が使う）。</summary>
    internal Rect2 ScreenRect(Camera3D camera)
    {
        Vector3 at = GlobalPosition;
        float lean = Mathf.DegToRad(BoardLean);
        Vector2 foot = camera.UnprojectPosition(at);
        Vector2 head = camera.UnprojectPosition(at + new Vector3(0, 0.26f + _top * Mathf.Cos(lean), -_top * Mathf.Sin(lean)));
        Vector2 side = camera.UnprojectPosition(at + new Vector3(BaseRadius * 1.15f, 0, 0));
        float half = Math.Max(30, side.X - foot.X);
        return new Rect2(foot.X - half, head.Y, half * 2, Math.Max(40, foot.Y - head.Y));
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_plate)) _plate.QueueFree();
    }
}

/// <summary>
/// 駒の名札（2D）。名前・数・HP の棒と、その下の 1 行（「拠点まで あと n」など）。入力は受けない。
/// <b>大きさは添え書きの 1 行まで含む</b>（置く場所を探すとき、添え書きも重ねないため）。
/// </summary>
public partial class Map3DPlate : Control
{
    internal const int NameSize = 20, NoteSize = 19, CountSize = 18, RewardSize = 19;
    private const float BoxHeight = 52, NoteHeight = 27;
    private Map3DPieceInfo _info;
    private Color _team;
    private float _clock, _boxWidth = 132;
    internal bool Selected { get; set; }

    public Map3DPlate() { MouseFilter = MouseFilterEnum.Ignore; }

    internal void Set(in Map3DPieceInfo info, Color team)
    {
        if (_info == info && _team == team) return;
        _info = info; _team = team;
        Font font = Map3DFonts.Bold;
        // 第476期: 賞金（「賞金 ＋14」）は名前の右に金色で。
        float reward = info.Reward.Length > 0 ? font.GetStringSize(info.Reward, fontSize: RewardSize).X + 10 : 0;
        _boxWidth = Math.Max(132, font.GetStringSize(info.Name, fontSize: NameSize).X + reward + 28);
        float note = info.Note.Length > 0 ? font.GetStringSize(info.Note, fontSize: NoteSize).X + 12 : 0;
        Size = new Vector2(Math.Max(_boxWidth, note), BoxHeight + (info.Note.Length > 0 ? NoteHeight : 0));
        QueueRedraw();
    }

    /// <summary>左上を <paramref name="at"/> に置く。</summary>
    internal void Place(Vector2 at, float clock)
    {
        Position = at.Round();
        _clock = clock;
        if (_info.Urgent || Selected) QueueRedraw();
    }

    public override void _Draw()
    {
        float left = (Size.X - _boxWidth) / 2;
        var box = new Rect2(left, 0, _boxWidth, BoxHeight);
        Color edge = Selected ? Color.FromHtml("#ffd874") : _team;
        int width = Selected ? 3 : 2;
        DrawStyleBox(new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.09f, 0.88f), BorderColor = edge,
            BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
            CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9, CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9,
        }, box);
        Font font = Map3DFonts.Bold;
        Vector2 name = font.GetStringSize(_info.Name, fontSize: NameSize);
        float rewardWidth = _info.Reward.Length > 0 ? font.GetStringSize(_info.Reward, fontSize: RewardSize).X + 10 : 0;
        float nameLeft = left + (_boxWidth - name.X - rewardWidth) / 2;
        DrawString(font, new Vector2(nameLeft, 7 + font.GetAscent(NameSize)), _info.Name, fontSize: NameSize,
            modulate: Color.FromHtml("#f4f0dd"));
        if (_info.Reward.Length > 0)
            DrawString(font, new Vector2(nameLeft + name.X + 10, 8 + font.GetAscent(NameSize)), _info.Reward, fontSize: RewardSize,
                modulate: _info.Prize ? Color.FromHtml("#ffd24a") : Color.FromHtml("#d8c391"));
        // HP の棒と数。
        string count = _info.Count > 0 ? $"×{_info.Count}" : "—";
        Vector2 countSize = font.GetStringSize(count, fontSize: CountSize);
        float barWidth = _boxWidth - 24 - countSize.X - 8;
        var bar = new Rect2(left + 12, 36, barWidth, 8);
        DrawRect(bar, new Color(1, 1, 1, 0.16f));
        DrawRect(new Rect2(bar.Position, new Vector2(barWidth * (float)Math.Clamp(_info.HpPercent / 100.0, 0, 1), bar.Size.Y)),
            _info.Enemy ? Color.FromHtml("#ee7962") : Color.FromHtml("#71d7a1"));
        DrawString(font, new Vector2(left + _boxWidth - 12 - countSize.X, 30 + font.GetAscent(CountSize)), count, fontSize: CountSize,
            modulate: Color.FromHtml("#e8e2cc"));
        if (_info.Note.Length == 0) return;
        Vector2 note = font.GetStringSize(_info.Note, fontSize: NoteSize);
        Color tint = _info.NoteColor;
        if (_info.Urgent) tint = tint.Lerp(Colors.White, 0.25f + 0.25f * Mathf.Sin(_clock * 6));
        var at = new Vector2((Size.X - note.X) / 2, BoxHeight + 2 + font.GetAscent(NoteSize));
        DrawStringOutline(font, at, _info.Note, fontSize: NoteSize, size: 6, modulate: new Color(0.03f, 0.03f, 0.05f, 0.95f));
        DrawString(font, at, _info.Note, fontSize: NoteSize, modulate: tint);
    }
}

/// <summary>
/// マスの円い台（発注書 §2-2）。縁の色で<b>味方／敵／空</b>を見せ、選んだ隊の行ける先は金色に光る。判定は持たない（呼ぶ側が渡す）。
/// </summary>
public partial class Map3DCellMark : Node3D
{
    internal enum Owner { Empty, Ally, Enemy }

    private static readonly Color[] RimColors = { Color.FromHtml("#b9ad8c"), Color.FromHtml("#55d5b8"), Color.FromHtml("#e96951") };
    private readonly StandardMaterial3D _rimMaterial = new() { Roughness = 0.5f, EmissionEnabled = true };
    private readonly StandardMaterial3D _glowMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };
    private readonly MeshInstance3D _glow = new();
    private readonly MeshInstance3D _halo = new();
    private readonly StandardMaterial3D _haloMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true,
    };
    private float _clock;
    private bool _reach, _goal;
    internal float Radius { get; }

    private readonly StandardMaterial3D? _flagMaterial;

    /// <param name="outpost">第478期: 中間拠点の台（八角・外にもう 1 本の輪・持ち主の色の旗）。ふつうのマスと形で見分ける。</param>
    internal Map3DCellMark(float radius, bool outpost = false)
    {
        Radius = radius;
        if (outpost)
        {
            AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = radius * 1.12f, OuterRadius = radius * 1.24f, Rings = 8, RingSegments = 6 },
                Position = new Vector3(0, 0.14f, 0), Rotation = new Vector3(0, Mathf.Pi / 8, 0), MaterialOverride = _rimMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            _flagMaterial = new StandardMaterial3D { Roughness = 0.7f, EmissionEnabled = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
            var pole = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.09f, Height = 5.2f, RadialSegments = 8 },
                Position = new Vector3(-radius * 0.92f, 2.6f, radius * 0.55f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#3a2f25"), Roughness = 0.9f },
            };
            AddChild(pole);
            pole.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.7f, 1.05f, 0.06f) },
                Position = new Vector3(0.9f, 1.95f, 0), MaterialOverride = _flagMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius * 1.06f, Height = 0.16f, RadialSegments = outpost ? 8 : 36 },
            Rotation = new Vector3(0, outpost ? Mathf.Pi / 8 : 0, 0),
            Position = new Vector3(0, 0.10f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("#6f6a62"), Roughness = 0.95f },
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = radius * 0.86f, OuterRadius = radius * 0.97f, Rings = 40, RingSegments = 6 },
            Position = new Vector3(0, 0.17f, 0), MaterialOverride = _rimMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        _glow.Mesh = new CylinderMesh { TopRadius = radius * 0.84f, BottomRadius = radius * 0.84f, Height = 0.02f, RadialSegments = 36 };
        _glow.Position = new Vector3(0, 0.20f, 0);
        _glow.MaterialOverride = _glowMaterial;
        _glow.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _glow.Visible = false;
        AddChild(_glow);
        _halo.Mesh = new TorusMesh { InnerRadius = radius * 1.04f, OuterRadius = radius * 1.17f, Rings = 44, RingSegments = 6 };
        _halo.Position = new Vector3(0, 0.12f, 0);
        _halo.MaterialOverride = _haloMaterial;
        _halo.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _halo.Visible = false;
        AddChild(_halo);
        SetOwner(Owner.Empty);
    }

    internal void SetOwner(Owner owner)
    {
        Color c = RimColors[(int)owner];
        _rimMaterial.AlbedoColor = c;
        _rimMaterial.Emission = c;
        _rimMaterial.EmissionEnergyMultiplier = owner == Owner.Empty ? 0.12f : 0.55f;
    }

    /// <summary>中間拠点の旗の色（味方 ／ 敵・台の縁とは別に、隊や敵が載っていなくても持ち主を示す）。</summary>
    internal void SetFlag(bool mine)
    {
        if (_flagMaterial is null) return;
        Color c = RimColors[mine ? (int)Owner.Ally : (int)Owner.Enemy];
        _flagMaterial.AlbedoColor = c;
        _flagMaterial.Emission = c;
        _flagMaterial.EmissionEnergyMultiplier = 0.45f;
    }

    /// <summary><paramref name="reach"/> ＝ 選んだ隊が行ける先、<paramref name="goal"/> ＝ その隊の今の行き先。</summary>
    internal void SetReach(bool reach, bool goal)
    {
        _reach = reach; _goal = goal;
        _glow.Visible = _halo.Visible = reach || goal;
    }

    public override void _Process(double delta)
    {
        if (!_glow.Visible) return;
        _clock += (float)delta;
        float wave = 0.5f + 0.5f * Mathf.Sin(_clock * (_goal ? 5.0f : 3.2f));
        Color c = _goal ? Color.FromHtml("#ffe9a8") : Color.FromHtml("#ffd874");
        _glowMaterial.AlbedoColor = new Color(c.R, c.G, c.B, (_goal ? 0.20f : 0.07f) + 0.08f * wave);
        _glowMaterial.Emission = c;
        _glowMaterial.EmissionEnergyMultiplier = 0.4f;
        _haloMaterial.AlbedoColor = c;
        _haloMaterial.Emission = c;
        _haloMaterial.EmissionEnergyMultiplier = (_goal ? 2.2f : 1.1f) + 0.9f * wave;
        float swell = 1 + (_goal ? 0.05f : 0.025f) * wave;
        _halo.Scale = new Vector3(swell, 1, swell);
    }
}
