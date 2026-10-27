"""採用済みv2の斬撃に、足の荷重・蹴り出し・踏着の全身差分を接続する。"""
import json
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = json.loads((HERE.parent / "attack-weight-v2/cel-animation.json").read_text(encoding="utf-8"))
spec["sourceId"] = "utsu-step-v3"
spec["sheets"] = {"../attack-weight-v2/" + key: count for key, count in spec["sheets"].items()}
for pose in spec["poses"]:
    pose["sheet"] = "../attack-weight-v2/" + pose["sheet"]

def pose(key, head, ground, blades, body):
    sheet = "source-" + key.replace("_", "-") + ".png"
    spec["sheets"][sheet] = 1
    spec["poses"].append(dict(key=key, sheet=sheet, cell=0, head=head, ground=ground, blades=blades,
                              body=dict(zip(["neck","pelvis","knee","ankle"], body))))

pose("step_load", [[729,164],[749,374]], [520,1446], [[[884,498],[1006,81]],[[214,764],[14,1085]]], [[710,385],[610,780],[394,1040],[145,1350]])
pose("step_push", [[1015,22],[1055,207]], [850,995], [[[1151,208],[1520,87]],[[455,494],[21,746]]], [[1005,215],[860,565],[575,760],[307,912]])
pose("step_plant", [[700,194],[723,391]], [520,1354], [[[457,290],[109,167]],[[889,880],[1019,1103]]], [[697,402],[580,807],[295,1010],[100,1265]])

for clip in spec["clips"]:
    clip["name"] = clip["name"].replace("_v2", "_v3")
    if clip["hit"] != 1:
        continue
    clip["keys"] = [["ready",2],["step_load",3],["step_push",4],["step_plant",3],["coil",2],["drive1",2],["cut1",1]]
    clip["keys"] += [["pass1",4]] if clip["keep"] else [["pass1",3],["land",3],["recover",3],["ready",3]]
    count = sum(n for _,n in clip["keys"])
    clip["contact"] = 16
    clip["vertical"] = [0] * count
    clip["forward"] = [0] * count
    # 0〜4:その場で荷重、5〜8:蹴り出して前進、9:踏着して停止。切り出しは16。
    clip["advance"] = [0,0,0,0,0,0,.18,.52,.86] + [1] * (count-9)

(HERE / "cel-animation.json").write_text(json.dumps(spec, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
measurements = {}
for pose in spec["poses"]:
    if "body" not in pose:
        continue
    body = pose["body"]
    scale = spec["headPixels"] / math.dist(*pose["head"])
    measurements[pose["key"]] = dict(head=spec["headPixels"], torso=round(math.dist(body["neck"],body["pelvis"])*scale,1),
                                    shin=round(math.dist(body["knee"],body["ankle"])*scale,1))
(HERE / "body-calibration.json").write_text(json.dumps(measurements,indent=2)+"\n", encoding="utf-8")
print(f"UTSU_STEP_SPEC_OK poses={len(spec['poses'])} frames={sum(sum(n for _,n in c['keys']) for c in spec['clips'])}")
