using BattleCore;
using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// 本番の駒と待機連番を使う、強化段階の比較撮影。戦闘判定は行わない。
public partial class HagiEmpowerCapture : Node3D
{
    public override async void _Ready()
    {
        try
        {
            var environment=new Godot.Environment { BackgroundMode=Godot.Environment.BGMode.Color,
                BackgroundColor=new Color(.025f,.033f,.046f), AmbientLightSource=Godot.Environment.AmbientSource.Color,
                AmbientLightColor=new Color(.72f,.79f,.74f), AmbientLightEnergy=.42f,
                TonemapMode=Godot.Environment.ToneMapper.Filmic, GlowEnabled=false };
            AddChild(new WorldEnvironment { Environment=environment });
            AddChild(new DirectionalLight3D { RotationDegrees=new Vector3(-34,-42,0),LightEnergy=1.12f });
            var camera=new Camera3D { Position=new Vector3(0,1.15f,6.5f),Fov=48,Current=true };
            AddChild(camera); camera.LookAt(new Vector3(0,1.15f,0),Vector3.Up);
            var canvas=new CanvasLayer(); AddChild(canvas);
            var font=new SystemFont { FontNames=new[]{"Yu Gothic","Noto Sans CJK JP","sans-serif"} };
            var atlas=UiKit.LoadTexture("res://assets/outcast_atlas.png");
            for(int i=0;i<4;i++)
            {
                var pawn=new BattlePawn3D(); AddChild(pawn);
                var def=UnitCatalog.Hagi;
                pawn.Configure(new DemoOpening(i+1,0,def.Id,def.Name,i,def.MaxHp,def.MaxHp,def.Attack,
                    AttackPattern.Single,def.Advances,def.Traits),atlas);
                pawn.Position=new Vector3(-3.3f+i*2.2f,0,0);
                pawn.Hud.Hide();
                foreach(var floorMark in pawn.GetChildren().OfType<MeshInstance3D>())floorMark.Hide();
                pawn.SetHagiState(new(i+1,i*3,0,0,Stage:i),()=>(false,1));
                Vector2 screen=camera.UnprojectPosition(pawn.GlobalPosition+Vector3.Up*2.65f);
                var label=new Label { Text=$"魂 {i*3}"+(i==0?"  未強化":$"  強化 {i}"),
                    Position=screen-new Vector2(135,0),Size=new Vector2(270,60),
                    HorizontalAlignment=HorizontalAlignment.Center,Modulate=i==0?UiKit.Muted:new Color(1,.44f,.35f) };
                label.AddThemeFontOverride("font",font); label.AddThemeFontSizeOverride("font_size",24); canvas.AddChild(label);
            }
            await Frames(30); int start=Engine.GetFramesDrawn();
            await Frames(60);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            string dir=ProjectSettings.GlobalizePath("res://../design/art/hagi/blade-empower-v1");
            Directory.CreateDirectory(dir);
            using var screenshot=GetViewport().GetTexture().GetImage();
            using var cropped=screenshot.GetRegion(new Rect2I(0,180,1440,500));
            cropped.SavePng(Path.Combine(dir,"stages.png"));
            await Frames(120); int end=Engine.GetFramesDrawn();
            File.WriteAllText(Path.Combine(dir,"stage-capture.json"),JsonSerializer.Serialize(new {
                fps=60,startFrame=start,endFrame=end,souls=new[]{0,3,6,9},
                source="本番のBattlePawn3Dと採用済み48コマの待機連番" },new JsonSerializerOptions { WriteIndented=true })+"\n");
            GD.Print($"HAGI_EMPOWER_CAPTURE_OK stages=0,3,6,9 start={start} end={end}"); GetTree().Quit();
        }
        catch(Exception error) { GD.PushError(error.ToString());GetTree().Quit(1); }
    }
    private async Task Frames(int count)
    {
        for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }
}
