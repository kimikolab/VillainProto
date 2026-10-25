using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

// 敵前へ移動する間だけ使う構え。攻撃の件数・接触待ちには含めない。
public partial class BattlePawn3D
{
    internal sealed record RicaApproach(Texture2D Texture,Vector2 Anchor,Vector2 IdleAnchor,float Scale,RicaAnchors Anchors);
    private static readonly Dictionary<int,RicaApproach?> RicaApproaches=new();
    internal bool RicaApproachActive { get; private set; }
    internal static RicaApproach? RicaApproachOf(int stage)
    {
        EnsureAnimationCacheShutdown();
        if(_animationCacheClosing)return null;
        if(RicaApproaches.TryGetValue(stage,out var cached))return cached;
        string key=$"rica_approach_stage{stage}",root=$"res://assets/portraits/battle/approach/{key}";
        if(!Godot.FileAccess.FileExists(root+"/layout.json"))return RicaApproaches[stage]=null;
        using var doc=JsonDocument.Parse(Godot.FileAccess.GetFileAsString(root+"/layout.json"));
        Vector2 V(string name)=>new(doc.RootElement.GetProperty(name)[0].GetSingle(),doc.RootElement.GetProperty(name)[1].GetSingle());
        return RicaApproaches[stage]=new(UiKit.LoadTexture(root+"/portrait.png",mipmaps:true),V("anchor"),V("idleAnchor"),
            doc.RootElement.GetProperty("scale").GetSingle(),RicaTrackOf(key)[0]);
    }
    internal void BeginRicaApproach()
    {
        // 連撃の移動は直前の振り抜き／空中姿勢を引き継ぐ。
        if(_unitId!="rica" || !DeathActive || RicaClipName is not null || RicaApproachOf(GraveStage) is null)return;
        RicaApproachActive=true;_idleLoopActive=false;RefreshBattlePortrait();
    }
    internal void EndRicaApproach(bool refresh=true)
    {
        if(!RicaApproachActive)return;
        RicaApproachActive=false;
        if(refresh && _alive && !_victory)RefreshBattlePortrait();
    }
    private bool ApplyRicaApproachPortrait()
    {
        if(!RicaApproachActive || _unitId!="rica" || !DeathActive)return false;
        if(RicaApproachOf(GraveStage) is not {} pose)return false;
        var idle=UiKit.BattlePortrait(_atlas,"rica");
        float idlePixel=_portraitHeight/Math.Max(1,idle.GetHeight()),pixel=idlePixel*pose.Scale;
        float side=Team==BattleCore.BattleContext.EnemyTeam?-1:1;
        _portraitOffsetX=side*(pose.IdleAnchor.X-idle.GetWidth()*.5f)*idlePixel
            -side*(pose.Anchor.X-pose.Texture.GetWidth()*.5f)*pixel;
        _portraitGroundDistance=(pose.Anchor.Y-pose.Texture.GetHeight()*.5f)*pixel;
        _portraitBaseY=PortraitGroundY+_portraitGroundDistance;
        _sprite.Texture=pose.Texture;_sprite.FlipH=side<0;_sprite.PixelSize=pixel;
        _sprite.Position=new Vector3(_portraitOffsetX,_portraitBaseY,0);
        _portraitMaterial.SetShaderParameter("portrait_texture",pose.Texture);
        _idleLoopActive=false;
        return true;
    }
}
