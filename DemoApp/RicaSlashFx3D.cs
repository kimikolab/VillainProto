using Godot;
using System;

// 一打に一つの演出。光の弧・細い残光・購入素材の火花を同じ再生時計で進める。
public partial class RicaSlashFx3D : Node3D
{
    private Func<(bool Paused,double Speed)> _clock = null!;
    private ShaderMaterial[] _materials = Array.Empty<ShaderMaterial>();
    private Sprite3D[] _sparks = Array.Empty<Sprite3D>();
    private Vector3[] _velocities = Array.Empty<Vector3>();
    private double _age;
    private double _life = .52;
    internal bool IsBlunt { get; private set; }
    internal bool IsBladeContact { get; private set; }
    internal void Start(Vector3 point,string clip,Color tint,Camera3D camera,Func<(bool Paused,double Speed)> clock,float targetSpan=0,int stage=1)
    {
        _clock=clock;
        IsBlunt=clip=="rica_shovel";
        IsBladeContact=clip=="rica_return";
        stage=Math.Clamp(stage,1,3);
        float scale=stage==3?1.30f:stage==2?1.14f:1;
        float thickness=stage==3?3.1f:stage==2?2.0f:1;
        _life=IsBlunt||IsBladeContact?.24:stage==3?.64:stage==2?.58:.52;
        bool thrust=clip=="rica_thrust", descend=clip=="rica_descend", spin=clip=="rica_sweep";
        bool orbital=clip is "rica_crescent" or "rica_sweep" || IsBladeContact;
        float angle=clip switch { "rica_rise" => .70f, "rica_return" => -.68f, "rica_descend" => -1.03f, _ => 0 };
        var b=camera.GlobalBasis.Orthonormalized();
        float width=Math.Max(thrust?4.3f:descend?4.6f:4.2f,targetSpan+2.5f)*scale;
        var shader=new Shader {Code=Godot.FileAccess.GetFileAsString("res://assets/fx/rica_motion_v1/slash_arc.gdshader")};
        int count=IsBlunt||orbital?1:stage==3?(thrust?2:3):descend?2:1;_materials=new ShaderMaterial[count];
        for(int i=0;i<count;i++)
        {
            var mat=new ShaderMaterial {Shader=shader};_materials[i]=mat;
            mat.SetShaderParameter("tint",tint);mat.SetShaderParameter("kind",thrust?1:0);
            mat.SetShaderParameter("thickness",i>0&&spin?(i==1?1.6f:.9f):thickness);
            mat.SetShaderParameter("offset",i*1.4f);
            float a=angle+(spin?(i==1?.65f:i==2?-.72f:0):0);
            Vector3 x=b.X*Mathf.Cos(a)+b.Y*Mathf.Sin(a),y=-b.X*Mathf.Sin(a)+b.Y*Mathf.Cos(a);
            float height=(thrust?.65f:clip=="rica_crescent"?1.75f:spin?2.65f:3.1f)*scale;
            float layerWidth=width;
            Vector3 origin=point+b.Z*(.035f+i*.006f);
            if(orbital)
            {
                // 軌道は実際の剣先で描き、命中側には短い閃光だけを置く。
                mat.SetShaderParameter("kind",2);layerWidth=height=IsBladeContact?1.15f:stage>=3?1.8f:1.35f;
                x=b.X;y=b.Y;
            }
            if(IsBlunt)
            {
                mat.SetShaderParameter("kind",4);layerWidth=2.0f;height=1.55f;
                x=b.X;y=b.Y;origin=point+b.Z*.06f;
            }
            if(descend&&i==1)
            {
                height=1.05f*scale;origin-=Vector3.Up*.88f;x=b.X;y=b.Y;
                mat.SetShaderParameter("kind",3);mat.SetShaderParameter("thickness",1.8f);
            }
            if(stage==3&&(descend&&i==2||thrust&&i==1))
            {
                mat.SetShaderParameter("kind",2);layerWidth=height=2.6f;x=b.X;y=b.Y;
                origin=point+b.Z*.065f;
            }
            var quad=new MeshInstance3D {Mesh=new QuadMesh {Size=Vector2.One},MaterialOverride=mat,
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
            AddChild(quad);
            quad.GlobalTransform=new Transform3D(new Basis(x*layerWidth,y*height,b.Z),origin);
        }
        const string purchased="res://PolyBlocks/EffectBlocks/source_files/textures/sparkle.png";
        var texture=ResourceLoader.Exists(purchased)?GD.Load<Texture2D>(purchased):GD.Load<Texture2D>("res://assets/fx/death_light.svg");
        int sparks=IsBlunt?7:stage==3?14:stage==2?10:8;
        _sparks=new Sprite3D[sparks];_velocities=new Vector3[sparks];
        for(int k=0;k<_sparks.Length;k++)
        {
            float a=k*Mathf.Tau/_sparks.Length+.21f;
            var spark=new Sprite3D {Texture=texture,Shaded=false,NoDepthTest=true,
                Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,PixelSize=.00065f*scale};
            AddChild(spark);spark.GlobalPosition=point+b.Z*.06f;
            _sparks[k]=spark;_velocities[k]=(b.X*Mathf.Cos(a)*(2+k%3)+b.Y*Mathf.Sin(a)*(1.5f+k%2))*scale;
            spark.Modulate=tint.Lerp(Colors.White,.65f);
        }
    }
    public override void _Process(double delta)
    {
        var (paused,speed)=_clock();if(paused)return;
        _age+=delta*speed;
        foreach(var mat in _materials)mat.SetShaderParameter("progress",(float)(_age/_life));
        for(int k=0;k<_sparks.Length;k++)
        {
            _sparks[k].Position+=_velocities[k]*(float)(delta*speed);
            float fade=Mathf.Pow(Math.Max(0,1-(float)(_age/_life)),1.4f);
            var c=_sparks[k].Modulate;c.A=fade;_sparks[k].Modulate=c;
            _sparks[k].Scale=Vector3.One*(.55f+fade*.70f);
        }
        if(_age>=_life)QueueFree();
    }
}
