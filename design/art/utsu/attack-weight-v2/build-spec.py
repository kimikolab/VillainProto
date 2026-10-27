"""原画上で確認した全身の基準点と保持時間。画像の変形はGodotの等比配置だけ。"""
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
poses = []
def pose(key, sheet, cell, head, ground, blades, body=None):
    item = dict(key=key, sheet=sheet, cell=cell, head=head, ground=ground, blades=blades)
    if body:
        item["body"] = dict(zip(["neck", "pelvis", "knee", "ankle"], body))
    poses.append(item)

pose("ready", "source-ready.png", 0, [[660,76],[660,296]], [512,1484], [[[795,497],[1013,194]],[[198,692],[10,1017]]], [[640,310],[590,740],[400,1040],[230,1390]])
pose("coil", "source-ground1.png", 0, [[554,19],[580,157]], [480,856], [[[355,111],[95,72]],[[735,470],[899,626]]], [[560,158],[520,430],[675,550],[805,778]])
pose("cut1", "source-ground1.png", 1, [[1400,48],[1414,198]], [1350,870], [[[1401,275],[1751,514]],[[1098,494],[889,592]]], [[1398,200],[1380,480],[1190,635],[1000,810]])
pose("pass1", "source-ground2.png", 0, [[685,110],[697,249]], [562,848], [[[800,545],[1018,774]],[[267,416],[21,599]]])
pose("cut2", "source-ground2.png", 1, [[1289,111],[1307,230]], [1398,842], [[[1058,520],[905,633]],[[1555,204],[1755,34]]])
pose("drive1", "source-drive.png", 0, [[582,59],[605,200]], [470,824], [[[350,125],[80,78]],[[746,496],[919,668]]])
pose("drive2", "source-drive.png", 1, [[1278,95],[1294,234]], [1375,836], [[[1054,463],[906,572]],[[1593,247],[1791,28]]])
pose("back3", "source-air.png", 0, [[400,130],[421,222]], [396,708], [[[247,335],[22,334]],[[344,130],[205,46]]])
pose("sweep3", "source-air.png", 1, [[785,184],[796,279]], [780,714], [[[917,277],[1074,270]],[[550,421],[480,499]]])
pose("launch4", "source-air.png", 2, [[377,779],[391,868]], [385,1416], [[[289,783],[108,741]],[[457,978],[613,1023]]])
pose("air4", "source-air.png", 3, [[925,846],[946,944]], [873,1425], [[[810,864],[638,804]],[[957,1081],[1075,1191]]])
pose("tuck5", "source-finish.png", 0, [[583,278],[571,363]], [485,640], [[[567,421],[731,497]],[[330,113],[72,29]]])
pose("drop5", "source-finish.png", 1, [[1247,134],[1259,225]], [1179,515], [[[1380,380],[1515,515]],[[978,144],[779,79]]])
pose("land", "source-recovery.png", 0, [[700,128],[718,266]], [560,838], [[[871,561],[1042,750]],[[263,416],[35,553]]])
pose("recover", "source-recovery.png", 1, [[1446,34],[1452,168]], [1410,854], [[[1589,360],[1738,99]],[[1143,474],[1010,656]]], [[1420,170],[1390,450],[1250,605],[1125,790]])

clips = []
def clip(hit, keep, keys, contact, vertical=None):
    count = sum(n for _, n in keys)
    clips.append(dict(name=f"utsu_hit{hit}_{'link' if keep else 'end'}_v2", hit=hit, keep=keep,
                      keys=keys, contact=contact, activeBlade=1 if hit in (2,4) else 0,
                      vertical=vertical or [0]*count, forward=[0]*count))

clip(1, True, [["ready",2],["coil",5],["drive1",2],["cut1",1],["pass1",4]], 9)
clip(1, False, [["ready",2],["coil",5],["drive1",2],["cut1",1],["pass1",3],["land",3],["recover",3],["ready",3]], 9)
clip(2, True, [["pass1",3],["drive2",2],["cut2",1],["back3",4]], 5)
clip(2, False, [["pass1",3],["drive2",2],["cut2",1],["back3",3],["land",3],["recover",3],["ready",3]], 5)
clip(3, True, [["back3",4],["sweep3",1],["launch4",4]], 4, [0,0,0,0,0,0,.02,.06,.12])
clip(3, False, [["back3",4],["sweep3",2],["land",3],["recover",3],["ready",3]], 4)
clip(4, True, [["launch4",4],["air4",2],["tuck5",3]], 4, [.12,.2,.34,.5,.66,.78,.87,.92,.94])
clip(4, False, [["launch4",4],["air4",5],["land",4],["recover",3],["ready",3]], 4, [.12,.2,.34,.5,.66,.78,.65,.43,.18,0,0,0,0,0,0,0,0,0,0])
clip(5, False, [["tuck5",3],["drop5",2],["land",4],["recover",3],["ready",3]], 3, [.94,.66,.32,0,0,0,0,0,0,0,0,0,0,0,0])

spec = dict(sourceId="utsu-weight-v2", canvas=[1280,960], anchor=[600,900], headPixels=110,
            clipScale=2, idleAnchor=[512,1484], fps=30,
            sheets={"source-ready.png":1,"source-ground1.png":2,"source-ground2.png":2,
                    "source-drive.png":2,"source-air.png":4,"source-finish.png":4,"source-recovery.png":2},
            poses=poses, clips=clips)
(HERE / "cel-animation.json").write_text(json.dumps(spec, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
print(f"UTSU_WEIGHT_SPEC_OK poses={len(poses)} clips={len(clips)} frames={sum(sum(n for _,n in c['keys']) for c in clips)}")
