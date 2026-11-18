using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public partial class Map11Main
{
    // 通しに結び付けて記憶する。戦闘から戻るたびに登場演出を繰り返さない。
    private sealed class StageSeen
    {
        public int Hero;
        public bool Gate, Home;
        public HashSet<int> Outposts = new();
    }
    private static readonly ConditionalWeakTable<Map11State, StageSeen> StagingSeen = new();
    private MapStageCue? _stageCue;
    private Node3D? _gateLeft, _gateRight, _homeFlag, _homeFire;
    private Node3D? _homeRuins;
    private readonly List<Node3D> _homeFlames = new();
    private float _gateSpread, _stageTime;
    private readonly List<(Vector3 A, Vector3 B)> _chaseHops = new();
    private readonly List<Node3D> _chaseSteps = new();

    private void BuildStaging()
    {
        _stageCue = new MapStageCue { ZIndex = 60 };
        _uiRoot.AddChild(_stageCue);
        _stageCue.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        if (Map3DMode.Active) return;   // 3D: 旗・門・マスの目印はジオラマ（`Map3DStage`）とマスの台が持つ
        _homeFlag = new Node3D { Position = BasePos + new Vector3(-1, .4f, 0) };
        AddChild(_homeFlag);
        StageBox(_homeFlag, new Vector3(0, 2, 0), new Vector3(.18f, 4, .18f), UiKit.Muted);
        StageBox(_homeFlag, new Vector3(.8f, 3.4f, 0), new Vector3(1.6f, 1, .12f), PlayerColor);
        if (!St.Portal.On) return;
        var gate = new Node3D { Position = PortalPos + new Vector3(0, 0, -1.2f) };
        AddChild(gate);
        foreach (float x in new[] { -2.3f, 2.3f })
        {
            StageBox(gate, new Vector3(x, 1.8f, 0), new Vector3(1.1f, 3.6f, 1), DeadColor);
            StageBox(gate, new Vector3(x, 3.9f, 0), new Vector3(1.4f, .6f, 1.2f), GoldColor);
        }
        _gateLeft = new Node3D(); _gateRight = new Node3D();
        gate.AddChild(_gateLeft); gate.AddChild(_gateRight);
        foreach (var leaf in new[] { _gateLeft, _gateRight })
        {
            StageBox(leaf, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 3, .3f), Color.FromHtml("#493b32"));
            for (int i = 0; i < 3; i++)
                StageBox(leaf, new Vector3((i - 1) * .5f, 1.5f, .2f), new Vector3(.1f, 3, .1f), GoldColor);
        }
        _gateSpread = St.GateOpen ? 1 : 0;
        // 長い道にも全マスの目印を置く。橋は既存の Graph.Edges の描画を共用。
        foreach (var cell in St.Graph.Cells.Where(c => c.Kind != MapGraph.Kind.Home && c.Kind != MapGraph.Kind.Portal && c.Kind != MapGraph.Kind.Outpost))
        {
            var mark = new Node3D { Position = PosOf(cell.Id) };
            AddChild(mark);
            StageBox(mark, new Vector3(0, .10f, 0), new Vector3(1.4f, .16f, 1.4f), Color.FromHtml("#b5a57c"));
            if (St.Graph.Lanes.Count(l => l.Contains(cell.Id)) > 1)
                StageBox(mark, new Vector3(0, .2f, 0), new Vector3(2.3f, .15f, .4f), GoldColor);
        }
    }

    private static void StageBox(Node parent, Vector3 at, Vector3 size, Color color)
        => parent.AddChild(new MeshInstance3D { Position = at, Mesh = new BoxMesh { Size = size },
            MaterialOverride = CampaignMain.MakeMaterial(color, roughness: .85f) });

    private void RefreshStaging()
    {
        if (_stageCue is null) return;
        if (StagingSeen.TryGetValue(St, out var seen))
        {
            bool hero = seen.Hero < 0 && St.HeroTurn >= 0;
            bool home = !seen.Home && St.HomeLost;
            if (home) PlayStageCue(St.Graph.Home, MapStageCue.Kind.Fall);
            else if (HunterBoard && (hero || seen.Gate != St.GateOpen)) { }   // 第480期: 狩る者の盤の門と登場は、自分の演出（`Map11Main.Hunter.cs`）が持つ
            else if (hero && St.Hero is { } h)
                PlayStageCue(h.Pos, St.Stage.Hero.Chaser ? MapStageCue.Kind.Chaser : MapStageCue.Kind.Guard);
            else if (seen.Gate != St.GateOpen) PlayStageCue(St.Graph.Portal, St.GateOpen ? MapStageCue.Kind.Open : MapStageCue.Kind.Close);
            else if (St.HeldOutposts.Except(seen.Outposts).FirstOrDefault(-1) is var cell && cell >= 0)
                PlayStageCue(cell, MapStageCue.Kind.Capture);
        }
        else { seen = new StageSeen(); StagingSeen.Add(St, seen); }
        seen.Hero = St.HeroTurn; seen.Home = St.HomeLost; seen.Gate = St.GateOpen;
        seen.Outposts = St.HeldOutposts.ToHashSet();
        if (_homeFlag is not null) _homeFlag.RotationDegrees = new Vector3(0, 0, St.HomeLost ? 78 : 0);
        if (St.HomeLost && _homeRuins is null)
        {
            _homeRuins = new Node3D { Position = BasePos };
            AddChild(_homeRuins);
            StageBox(_homeRuins, new Vector3(0, .35f, 0), new Vector3(4.5f, .35f, 3), Color.FromHtml("#292c28"));
            for (int i = 0; i < 3; i++)
            {
                var flame = new Node3D { Position = new Vector3((i - 1) * 1.1f, .5f, -1.2f) };
                _homeRuins.AddChild(flame); _homeFlames.Add(flame);
                flame.AddChild(new MeshInstance3D
                {
                    Position = Vector3.Up * 1.6f,
                    Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = .7f, Height = 3.2f, RadialSegments = 5 },
                    MaterialOverride = CampaignMain.MakeMaterial(new Color(1, .25f, .04f), emission: new Color(1, .35f, .03f)),
                });
                flame.AddChild(new MeshInstance3D
                {
                    Position = new Vector3(0, 1, .3f),
                    Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = .4f, Height = 2, RadialSegments = 5 },
                    MaterialOverride = CampaignMain.MakeMaterial(GoldColor, emission: GoldColor),
                });
            }
        }
        if (St.HomeLost && _homeFire is null)
        {
            // 購入済み EffectBlocks v4 の炎。複製インスタンスのみ調整し、原本は変更しない。
            const string path = "res://PolyBlocks/EffectBlocks/assets/fire/fire_light.tscn";
            if (ResourceLoader.Exists(path))
            {
                _homeFire = GD.Load<PackedScene>(path).Instantiate<Node3D>();
                _homeFire.Position = BasePos + new Vector3(0, .3f, -1); _homeFire.Scale = Vector3.One * 3.2f;
                AddChild(_homeFire);
                StripStageScripts(_homeFire);
            }
        }
    }

    private static void StripStageScripts(Node node)
    {
        // 素材の点滅用スクリプトを外し、盤のシーンと一緒に破棄する。
        if (node.GetScript().VariantType != Variant.Type.Nil) node.SetScript(default);
        foreach (Node child in node.GetChildren()) StripStageScripts(child);
    }

    private void PlayStageCue(int cell, MapStageCue.Kind kind)
    {
        GD.Print($"MAP_STAGING_CUE stage={St.Stage.Id} turn={St.Turn} kind={kind} cell={cell}");
        _stageCue!.ShowCue(kind, _camera.UnprojectPosition(PosOf(cell)) / _uiScale);
        _stageCue.MoveToFront();
        MapStagingAudio.Play(this, kind);
    }

    private void ProcessStaging(double delta)
    {
        _stageTime += (float)delta;
        for (int i = 0; i < _homeFlames.Count; i++)
            _homeFlames[i].Scale = new Vector3(1, .85f + .18f * Mathf.Sin(_stageTime * 8 + i * 2), 1);
        _stageCue?.Tick(delta, Map11Clock.Fast ? 2 : 1);
        float target = St.GateOpen ? 1 : 0;
        _gateSpread = Mathf.MoveToward(_gateSpread, target, (float)delta * 3);
        if (_gateLeft is not null && _gateRight is not null)
        {
            _gateLeft.Position = new Vector3(-.9f - _gateSpread * 1.5f, 0, 0);
            _gateRight.Position = new Vector3(.9f + _gateSpread * 1.5f, 0, 0);
        }
        // 二列の足跡が進行方向へ波打つ。行き先は既存の追跡経路をそのまま使う。
        int needed = _chaseHops.Count * 4;
        while (_chaseSteps.Count < needed)
        {
            var foot = new Node3D(); AddChild(foot);
            StageBox(foot, Vector3.Zero, new Vector3(.48f, .12f, .85f), GoldColor);
            _chaseSteps.Add(foot);
        }
        for (int i = 0; i < _chaseSteps.Count; i++)
        {
            Node3D foot = _chaseSteps[i]; foot.Visible = i < needed && !_reform;
            if (!foot.Visible) continue;
            var (a, b) = _chaseHops[i / 4];
            Vector3 direction = (b - a).Normalized();
            Vector3 side = direction.Cross(Vector3.Up) * (i % 2 == 0 ? .45f : -.45f);
            foot.Position = a.Lerp(b, ((i % 4) + .5f) / 4) + side + Vector3.Up * .4f;
            foot.LookAt(foot.Position + direction, Vector3.Up);
            foot.Scale = Vector3.One * (.8f + .22f * Mathf.Sin(_stageTime * 6 - i));
        }
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
            || e is InputEventKey { Pressed: true, Keycode: Key.Escape }) _stageCue?.Dismiss();
    }
}

/// <summary>1.25 秒で消える、文字も入力遮断もない出来事の絵。</summary>
public partial class MapStageCue : Control
{
    public enum Kind { Guard, Chaser, Fall, Open, Close, Capture }
    private Kind _kind;
    private Vector2 _at;
    private float _age = 2;
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
    public void ShowCue(Kind kind, Vector2 at) { _kind = kind; _at = at; _age = 0; Visible = true; QueueRedraw(); }
    public void Dismiss() { _age = 2; Visible = false; }
    public void Tick(double delta, double speed)
    {
        if (_age >= 1.25f) return;
        // 初回のシェーダー準備で遅れた1フレームに、登場の全尺を飲ませない。
        _age += (float)(Math.Min(delta, .05) * Math.Max(1, speed));
        if (_age >= 1.25f) Visible = false;
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (_age >= 1.25f) return;
        float fade = Mathf.Clamp((1.25f - _age) * 4, 0, 1);
        Color gold = new(1, .79f, .36f, fade);
        Color ink = new(.06f, .09f, .10f, fade);
        bool hero = _kind is Kind.Guard or Kind.Chaser;
        if (hero || _kind == Kind.Fall) DrawRect(new Rect2(Vector2.Zero, Size), new Color(.02f, .03f, .05f, .32f * fade));
        float radius = 42 + Math.Min(_age, .5f) * 130;
        Color color = _kind == Kind.Fall || _kind == Kind.Close ? new Color(1, .30f, .16f, fade) : gold;
        DrawArc(_at, radius, 0, Mathf.Tau, 64, color, 5, true);
        Vector2 p = _at + new Vector2(0, -85);
        if (hero)
        {
            // 対になる紋章: 門番は垂直の剣、追う勇者は一歩前へ傾く剣と足跡。
            float arrive = Mathf.Clamp((_age - .20f) / .30f, 0, 1);
            p += new Vector2(_kind == Kind.Chaser ? -26 * arrive : 0, -65 * (1 - arrive));
            DrawColoredPolygon(new[] { p + new Vector2(0, -65), p + new Vector2(46, -30), p + new Vector2(38, 36), p + new Vector2(0, 65), p + new Vector2(-38, 36), p + new Vector2(-46, -30) }, gold);
            DrawCircle(p, 31, ink);
            DrawLine(p + new Vector2(0, -45), p + new Vector2(0, 34), gold, 9, true);
            DrawLine(p + new Vector2(-24, 14), p + new Vector2(24, 14), gold, 7, true);
            if (_kind == Kind.Chaser)
                for (int i = 0; i < 3; i++) DrawCircle(p + new Vector2(-60 - i * 20, 40 + (i % 2) * 16), 5, gold);
        }
        else if (_kind == Kind.Fall)
        {
            DrawLine(p + new Vector2(-34, 35), p + new Vector2(38, 60), color, 8, true);
            DrawColoredPolygon(new[] { p + new Vector2(32, 58), p + new Vector2(0, 46), p + new Vector2(13, 15) }, color);
        }
        // 登場の前半で開門。勇者の紋章と同じ画面で続けて見せる。
        if (hero || _kind is Kind.Open or Kind.Close)
        {
            float open = Mathf.Clamp(_age * 4, 0, 1);
            if (_kind == Kind.Close) open = 1 - open;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 at = _at + new Vector2(side * (20 + open * 45) - 16, -30);
                DrawRect(new Rect2(at, new Vector2(32, 60)), ink);
                DrawRect(new Rect2(at, new Vector2(32, 60)), color, false, 4);
            }
        }
        if (_kind == Kind.Capture)
        {
            DrawLine(p + new Vector2(-20, 20), p + new Vector2(-20, -35), color, 5);
            DrawColoredPolygon(new[] { p + new Vector2(-20, -35), p + new Vector2(25, -25), p + new Vector2(-20, -10) }, color);
        }
    }
}

internal static class MapStagingAudio
{
    // 仮音の差し替え先はここだけ。乱数・外部素材・戦闘音のタイミングに依存しない。
    private static readonly Dictionary<MapStageCue.Kind, AudioStreamWav> Clips = new();
    public static void Play(Node parent, MapStageCue.Kind kind)
    {
        if (!Clips.TryGetValue(kind, out var clip))
        {
            const int rate = 22050;
            byte[] data = new byte[rate / 2 * 2];
            double hz = kind is MapStageCue.Kind.Fall or MapStageCue.Kind.Close ? 130 : 392;
            for (int i = 0; i < data.Length / 2; i++)
            {
                double t = (double)i / rate;
                double envelope = Math.Min(1, t * 65) * Math.Exp(-t * 9);
                short sample = (short)(10000 * envelope * (Math.Sin(t * hz * Math.Tau) + .3 * Math.Sin(t * hz * 1.5 * Math.Tau)));
                data[i * 2] = (byte)(sample & 255); data[i * 2 + 1] = (byte)((sample >> 8) & 255);
            }
            Clips[kind] = clip = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Data = data };
        }
        var player = new AudioStreamPlayer { Stream = clip, VolumeDb = -10 };
        parent.AddChild(player); player.Finished += player.QueueFree; player.Play();
    }
}
