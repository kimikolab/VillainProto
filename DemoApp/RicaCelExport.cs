using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

// 制作用の書き出し器。原画を部位に分けず、連結した全身セルをそのまま位置合わせする。
public partial class RicaCelExport : Node
{
    private sealed record Cell(int Label,int X,int Y,int Width,int Height,int Area);
    public override void _Ready()
    {
        try { var result=Export(); GD.Print($"RICA_CEL_EXPORT_OK clips={result.Clips} links={result.Links} whole-body drawings={result.Drawings}");GetTree().Quit(); }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static (int Clips,int Links,int Drawings) Export()
    {
        string repo=Path.GetFullPath(ProjectSettings.GlobalizePath("res://.."));
        string source=Path.Combine(repo,"design/art/rica/attack-motion-v1");
        PadAwakenedIdle(repo);
        using var spec=JsonDocument.Parse(File.ReadAllText(Path.Combine(source,"cel-animation.json")));
        int canvasW=spec.RootElement.GetProperty("canvas")[0].GetInt32(),canvasH=spec.RootElement.GetProperty("canvas")[1].GetInt32();
        int groundX=spec.RootElement.GetProperty("ground")[0].GetInt32(),groundY=spec.RootElement.GetProperty("ground")[1].GetInt32();
        var textures=new Dictionary<(int,int),(Image Image,object Anchor)>();
        var tracks=new Dictionary<(int,int),Dictionary<string,object>>();
        foreach(var stageSpec in spec.RootElement.GetProperty("stages").EnumerateArray())
        {
            int stage=stageSpec.GetProperty("stage").GetInt32();
            int poseOffset=stageSpec.TryGetProperty("poseOffset",out var offset)?offset.GetInt32():0;
            float baseScale=stageSpec.GetProperty("scale").GetSingle();
            string sheetPath=stageSpec.TryGetProperty("sheet",out var namedSheet)
                ?Path.Combine(repo,namedSheet.GetString()!):Path.Combine(source,$"clean-stage{stage}-sheet.png");
            using var sheet=Image.LoadFromFile(sheetPath);
            sheet.Convert(Image.Format.Rgba8);
            // 修正案から採用するのも全身セル単位。顔や腕などの部位は合成しない。
            if(stageSpec.TryGetProperty("overrides",out var alternatives))
                foreach(var alternative in alternatives.EnumerateArray())
                {
                    using var other=Image.LoadFromFile(Path.Combine(repo,alternative.GetProperty("sheet").GetString()!));
                    var r=alternative.GetProperty("rect");
                    var rect=new Rect2I(r[0].GetInt32(),r[1].GetInt32(),r[2].GetInt32(),r[3].GetInt32());
                    sheet.BlitRect(other,rect,rect.Position);
                }
            if(stageSpec.TryGetProperty("selectedPreview",out var selected))
                sheet.SavePng(Path.Combine(repo,selected.GetString()!));
            int w=sheet.GetWidth(),h=sheet.GetHeight();byte[] pixels=sheet.GetData();
            int[] labels=new int[w*h],queue=new int[w*h];var components=new List<Cell>();
            int label=0;
            for(int start=0;start<labels.Length;start++)
            {
                if(labels[start]!=0 || pixels[start*4+3]<=128)continue;
                label++;int first=0,last=1;queue[0]=start;labels[start]=label;
                int left=w,right=0,top=h,bottom=0,area=0;
                while(first<last)
                {
                    int p=queue[first++],x=p%w,y=p/w;area++;
                    left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                    void Visit(int q)
                    {if(labels[q]==0 && pixels[q*4+3]>128){labels[q]=label;queue[last++]=q;}}
                    if(x>0)Visit(p-1);if(x<w-1)Visit(p+1);if(y>0)Visit(p-w);if(y<h-1)Visit(p+w);
                }
                if(area>3000)components.Add(new(label,left,top,right-left+1,bottom-top+1,area));
            }
            int rows=stageSpec.TryGetProperty("rows",out var rowCount)?rowCount.GetInt32():2;
            var cells=components.OrderBy(c=>Math.Clamp((int)((c.Y+c.Height*.5f)*rows/h),0,rows-1))
                .ThenBy(c=>c.X+c.Width*.5f).ToArray();
            var anchors=stageSpec.GetProperty("anchors");
            if(cells.Length!=anchors.GetArrayLength())throw new InvalidOperationException($"stage{stage}: セル数 {cells.Length}");
            string poses=stageSpec.TryGetProperty("poseDirectory",out var namedPoses)
                ?Path.Combine(repo,namedPoses.GetString()!):Path.Combine(source,$"cel-stage{stage}");
            Directory.CreateDirectory(poses);
            for(int k=0;k<cells.Length;k++)
            {
                // 体を畳んだコマも頭の実寸で合わせる。全身の高さや幅では倍率を決めない。
                float scale=baseScale;
                bool calibrated=stageSpec.TryGetProperty("headGuides",out var headGuides);
                if(calibrated)
                {
                    if(headGuides.GetArrayLength()!=cells.Length)throw new InvalidOperationException("頭の物差しと全身セル数が一致しない");
                    var guide=headGuides[k];
                    var head=new Vector2(guide[1][0].GetSingle()-guide[0][0].GetSingle(),guide[1][1].GetSingle()-guide[0][1].GetSingle());
                    float target=spec.RootElement.GetProperty("proportionCalibration").GetProperty("targetHeadPixels").GetSingle();
                    if(head.Length()<20 || target<=0)throw new InvalidOperationException("頭の物差しが不正");
                    scale=target/head.Length();
                }
                Cell cell=cells[k];int margin=3;
                int x0=Math.Max(0,cell.X-margin),y0=Math.Max(0,cell.Y-margin);
                int cw=Math.Min(w-x0,cell.Width+margin*2),ch=Math.Min(h-y0,cell.Height+margin*2);
                var rgba=new byte[cw*ch*4];
                for(int cy=0;cy<ch;cy++)for(int cx=0;cx<cw;cx++)
                {
                    int sx=x0+cx,sy=y0+cy,p=sy*w+sx;
                    bool belongs=labels[p]==cell.Label;
                    if(!belongs)
                        for(int dy=-2;dy<=2 && !belongs;dy++)for(int dx=-2;dx<=2 && !belongs;dx++)
                            if(sx+dx>=0 && sx+dx<w && sy+dy>=0 && sy+dy<h && labels[(sy+dy)*w+sx+dx]==cell.Label)belongs=true;
                    if(belongs)Array.Copy(pixels,p*4,rgba,(cy*cw+cx)*4,4);
                }
                using var crop=Image.CreateFromData(cw,ch,false,Image.Format.Rgba8,rgba);
                crop.Resize(Math.Max(1,(int)MathF.Round(cw*scale)),Math.Max(1,(int)MathF.Round(ch*scale)),Image.Interpolation.Lanczos);
                float[] Point(int i)=>new[]{anchors[k][i][0].GetSingle(),anchors[k][i][1].GetSingle()};
                float[] ground=Point(3);
                int ox=(int)MathF.Round(groundX-(ground[0]-x0)*scale),oy=(int)MathF.Round(groundY-(ground[1]-y0)*scale);
                if(ox<0 || oy<0 || ox+crop.GetWidth()>canvasW || oy+crop.GetHeight()>canvasH)
                    throw new InvalidOperationException($"stage{stage} pose{k}: 全身セルが枠外");
                var canvas=Image.CreateEmpty(canvasW,canvasH,false,Image.Format.Rgba8);canvas.Fill(Colors.Transparent);
                canvas.BlitRect(crop,new Rect2I(0,0,crop.GetWidth(),crop.GetHeight()),new Vector2I(ox,oy));
                canvas.SavePng(Path.Combine(poses,$"pose{k:00}.png"));
                float[] Uv(int i){var p=Point(i);return new[]{(ox+(p[0]-x0)*scale)/canvasW,(oy+(p[1]-y0)*scale)/canvasH};}
                int poseIndex=k+poseOffset;
                tracks[(stage,poseIndex)]=new Dictionary<string,object>{{"socket",Uv(0)},{"tip",Uv(1)},{"eye",Uv(2)},{"shape",stage==0?0:stage==1?1:2}};
                if(calibrated)
                {
                    float[] HeadUv(int i)=>new[]{(ox+(headGuides[k][i][0].GetSingle()-x0)*scale)/canvasW,
                        (oy+(headGuides[k][i][1].GetSingle()-y0)*scale)/canvasH};
                    tracks[(stage,poseIndex)]["headTop"]=HeadUv(0);
                    tracks[(stage,poseIndex)]["headChin"]=HeadUv(1);
                }
                if(stageSpec.TryGetProperty("hiddenEyes",out var hidden))
                    tracks[(stage,poseIndex)]["eyesVisible"]=!hidden.EnumerateArray().Any(p=>p.GetInt32()==k);
                textures[(stage,poseIndex)]=(canvas,tracks[(stage,poseIndex)]);
            }
        }
        foreach(var clip in spec.RootElement.GetProperty("clips").EnumerateArray())
        {
            string key=clip.GetProperty("key").GetString()!,name="rica_"+key;
            string sourceTag=clip.TryGetProperty("source",out var clipSource)?clipSource.GetString()!:"rica-clean-cel-v1";
            int stage=clip.GetProperty("stage").GetInt32(),contact=clip.GetProperty("contact").GetInt32();
            var indices=new List<int>();
            foreach(var pose in clip.GetProperty("keys").EnumerateArray())
                indices.AddRange(Enumerable.Repeat(pose[0].GetInt32(),pose[1].GetInt32()));
            string dest=Path.Combine(repo,"DemoApp/assets/portraits/battle/attack_clip",name);Directory.CreateDirectory(dest);
            foreach(string old in Directory.GetFiles(dest,name+"_*.png"))File.Delete(old);
            var motion=new List<Dictionary<string,object>>();
            for(int i=0;i<indices.Count;i++)
            {
                var (image,_)=textures[(stage,indices[i])];image.SavePng(Path.Combine(dest,$"{name}_{i:000}.png"));
                var track=new Dictionary<string,object>(tracks[(stage,indices[i])]);
                if(clip.TryGetProperty("turn",out var turn))
                {
                    if(turn.GetArrayLength()!=indices.Count)throw new InvalidOperationException($"{name}: 回転角とコマ数が一致しない");
                    track["turn"]=turn[i].GetSingle();
                }
                if(clip.TryGetProperty("trailAlpha",out var trailAlpha))
                {
                    if(trailAlpha.GetArrayLength()!=indices.Count)throw new InvalidOperationException($"{name}: 回転残光の濃度とコマ数が一致しない");
                    track["trailAlpha"]=trailAlpha[i].GetSingle();
                }
                if(key=="thrust")track["shape"]=3;
                if(clip.TryGetProperty("shapes",out var shapes))
                {
                    if(shapes.GetArrayLength()!=indices.Count)throw new InvalidOperationException($"{name}: 武器形状とコマ数が一致しない");
                    track["shape"]=shapes[i].GetInt32();
                }
                motion.Add(track);
            }
            var info=new Dictionary<string,object>{
                {"source",sourceTag},{"frames",indices.Count},{"size",new[]{canvasW,canvasH}},{"fps",30.0},
                {"contact",contact},{"anchor",new[]{groundX,groundY}},{"idleAnchor",new[]{512,1496}},{"scale",2.80},
                {"referencePortrait","rica"},{"forward",indices.Select((_,i)=>MathF.Sin(Math.Min(1,i/(float)contact)*MathF.PI*.5f)*12).ToArray()},
                {"motion","全身原画のセルアニメーション。部位ワープ・クロスフェード・逆再生なし"},
            };
            if(clip.TryGetProperty("vertical",out var height))
            {
                var curve=height.EnumerateArray().Select(p=>p.GetSingle()).ToArray();
                if(curve.Length!=indices.Count)throw new InvalidOperationException($"{name}: 跳躍曲線とコマ数が一致しない");
                info["vertical"]=curve;
            }
            if(clip.TryGetProperty("forward",out var advance))
            {
                var curve=advance.EnumerateArray().Select(p=>p.GetSingle()).ToArray();
                if(curve.Length!=indices.Count)throw new InvalidOperationException($"{name}: 踏み込み曲線とコマ数が一致しない");
                info["forward"]=curve;
            }
            var options=new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
            File.WriteAllText(Path.Combine(dest,"clip.json"),JsonSerializer.Serialize(info,options)+"\n");
            File.WriteAllText(Path.Combine(dest,"motion.json"),JsonSerializer.Serialize(new{source=sourceTag,frames=motion},options)+"\n");
            GD.Print($"RICA_CEL_CLIP {name} frames={indices.Count} contact={contact}");
        }
        if(spec.RootElement.TryGetProperty("approaches",out var approaches))
            foreach(var approach in approaches.EnumerateArray())
            {
                int stage=approach.GetProperty("stage").GetInt32();
                var index=(approach.GetProperty("sourceStage").GetInt32(),approach.GetProperty("pose").GetInt32());
                string key=$"rica_approach_stage{stage}";
                string sourceTag=approach.TryGetProperty("source",out var approachSource)?approachSource.GetString()!:"rica-approach-cel-v8";
                string dest=Path.Combine(repo,"DemoApp/assets/portraits/battle/approach",key);Directory.CreateDirectory(dest);
                textures[index].Image.SavePng(Path.Combine(dest,"portrait.png"));
                var track=new Dictionary<string,object>(tracks[index]);track["shape"]=Math.Min(stage,2);
                var options=new JsonSerializerOptions{WriteIndented=true};
                File.WriteAllText(Path.Combine(dest,"layout.json"),JsonSerializer.Serialize(new{
                    source=sourceTag,anchor=new[]{groundX,groundY},idleAnchor=new[]{512,1496},scale=2.80,
                },options)+"\n");
                File.WriteAllText(Path.Combine(dest,"motion.json"),JsonSerializer.Serialize(new{
                    source=sourceTag,frames=new[]{track},
                },options)+"\n");
                GD.Print($"RICA_APPROACH_EXPORT stage={stage}");
            }
        foreach(var pair in textures.Values)pair.Image.Dispose();
        int links=spec.RootElement.GetProperty("clips").EnumerateArray().Count(c=>c.TryGetProperty("link",out var link)&&link.GetBoolean());
        return (spec.RootElement.GetProperty("clips").GetArrayLength()-links,links,textures.Count);
    }
    private static void PadAwakenedIdle(string repo)
    {
        string battle=Path.Combine(repo,"DemoApp/assets/portraits/battle");
        foreach(int stage in new[]{2,3})
        {
        using var original=Image.LoadFromFile(Path.Combine(battle,$"motion_sources/rica/v1/stage{stage}/idle.png"));
        using var padded=Image.CreateEmpty(1280,1920,false,Image.Format.Rgba8);
        padded.Fill(Colors.Transparent);padded.BlitRect(original,new Rect2I(0,0,1024,1536),new Vector2I(128,192));
        padded.SavePng(Path.Combine(battle,$"rica_stage{stage}_ready_right.png"));
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(battle,"motion_sources/rica/v1/manifest.json")));
        var pose=manifest.RootElement.GetProperty("poses").EnumerateArray()
            .Single(p=>p.GetProperty("stage").GetInt32()==stage && p.GetProperty("key").GetString()=="idle");
        float[] V(string key)=>new[]{pose.GetProperty(key)[0].GetSingle()*.8f+.1f,pose.GetProperty(key)[1].GetSingle()*.8f+.1f};
        string directory=Path.Combine(battle,"idle_layout");Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,$"rica_stage{stage}.json"),JsonSerializer.Serialize(new{
            heightScale=stage==2?1.4125f:1.30f,bottomPadding=(stage==2?207f:205f)/1920f,socket=V("weaponSocket"),tip=V("weaponTip"),eye=V("eyeAnchor"),
            note="元絵を縮めず周囲へ透明余白を追加。立ち絵の体格・足元を維持する。",
        },new JsonSerializerOptions{WriteIndented=true})+"\n");
        }
    }
}
