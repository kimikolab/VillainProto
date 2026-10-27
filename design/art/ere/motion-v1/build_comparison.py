"""生成候補の連番を等倍時間の比較動画へまとめる。キャラクターのコマは変形しない。"""
from pathlib import Path
import argparse,json,hashlib
from PIL import Image,ImageDraw,ImageFont
import numpy as np
ROOT=Path(__file__).resolve().parent
SOURCE=Path("D:/comfyui/output/VillainProto/idle_loop")
def build(tags,title,labels,output,one_shot=False):
 frames=[[Image.open(p).convert("RGBA") for p in sorted((SOURCE/(tag+"_rgba")).glob("*.png"))] for tag in tags]
 if any(len(f)<16 for f in frames): raise ValueError("候補の連続コマが不足しています")
 font=ImageFont.truetype("C:/Windows/Fonts/meiryo.ttc",20)
 preview=[]
 still=Image.open("DemoApp/assets/portraits/battle/ere_idle_right.png").convert("RGBA")
 for n in range(96):
  page=Image.new("RGB",(640,516),(23,34,31)); draw=ImageDraw.Draw(page)
  for k,sequence in enumerate(frames):
   frame=(sequence[n-16] if 16<=n<16+len(sequence) else still) if one_shot else sequence[n%len(sequence)]
   frame=frame.resize((320,480),Image.Resampling.LANCZOS)
   page.paste(frame,(k*320,36),frame)
   draw.text((k*320+14,5),labels[k],font=font,fill=(226,237,222))
  preview.append(page)
 preview[0].save(ROOT/(output+".webp"),save_all=True,append_images=preview[1:],duration=63,loop=0,quality=90,method=4)
 swatches=Image.new("RGB",(1280,1032))
 for i in range(16): swatches.paste(preview[i*6].resize((320,258)),((i%4)*320,(i//4)*258))
 palette=swatches.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
 gif=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in preview]
 gif[0].save(ROOT/(output+".gif"),save_all=True,append_images=gif[1:],duration=63,loop=0,optimize=False)
 stats=[]
 for tag,sequence in zip(tags,frames):
  alpha=[np.array(im.getchannel("A")) for im in sequence]
  bottom=[int(np.where(a>128)[0].max()) for a in alpha]
  top=[int(np.where(a>128)[0].min()) for a in alpha]
  stats.append({"tag":tag,"frames":len(sequence),"fps":16,"size":[640,960],"top_range":int(np.ptp(top)),"sole_range":[min(bottom),max(bottom)],"sha256_first":hashlib.sha256((SOURCE/(tag+"_rgba")/sorted((SOURCE/(tag+"_rgba")).glob("*.png"))[0].name).read_bytes()).hexdigest()})
 (ROOT/(output+".json")).write_text(json.dumps({"title":title,"candidates":stats},ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
 print(output,[(s["tag"],s["frames"],s["sole_range"]) for s in stats])
if __name__=="__main__":
 p=argparse.ArgumentParser(); p.add_argument("kind",choices=["idle","song"]); p.add_argument("a");p.add_argument("b");args=p.parse_args()
 build([args.a,args.b],"エレの待機" if args.kind=="idle" else "エレのスキル",["A：待機" ,"B：待機"] if args.kind=="idle" else ["C：演奏","D：演奏"],"idle-AB" if args.kind=="idle" else "song-CD",one_shot=args.kind=="song")
