"""承認済み待機48コマの大鎌を追跡する。画像は読み取りだけで、UV座標を出力する。"""
import hashlib
import json
from pathlib import Path
import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
FRAMES = ROOT / "DemoApp/assets/portraits/battle/idle_loop/hagi"
files = sorted(FRAMES.glob("*.png"))
assert len(files) == 48
base = cv2.imread(str(files[0]), cv2.IMREAD_UNCHANGED)
h, w = base.shape[:2]
# 元絵の外周。刃の左右の先端から、金属の背を通る連続した輪郭。
points = np.float32([[168,83],[215,52],[274,30],[342,22],[412,25],[478,41],[535,69],
                    [579,117],[608,175],[626,245],[633,314],[626,386],[611,444],[595,487]])
base_contours,_ = cv2.findContours(np.uint8(base[:,:,3]>120)*255,cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_NONE)
base_boundary = np.concatenate([c[:,0] for c in base_contours if len(c)>100]).astype(np.float32)
points = np.array([base_boundary[np.argmin(np.sum((base_boundary-p)**2,axis=1))] for p in points])
region = np.zeros((h,w), np.uint8)
cv2.fillPoly(region,[np.int32([[150,0],[639,0],[639,510],[574,510],[559,257],
                             [473,160],[441,122],[324,88],[150,111]])],255)
mask = cv2.bitwise_and(region, np.uint8(base[:,:,3]>120)*255)
sift = cv2.SIFT_create(nfeatures=1800, contrastThreshold=.015)
key0, desc0 = sift.detectAndCompute(cv2.cvtColor(base,cv2.COLOR_BGRA2GRAY),mask)
wide = cv2.dilate(region, np.ones((41,41),np.uint8))
matcher = cv2.BFMatcher(cv2.NORM_L2)

def trace(im):
    gray=cv2.cvtColor(im,cv2.COLOR_BGRA2GRAY)
    key,desc=sift.detectAndCompute(gray,cv2.bitwise_and(wide,np.uint8(im[:,:,3]>120)*255))
    good=[a for a,b in matcher.knnMatch(desc0,desc,k=2) if a.distance < .72*b.distance]
    src=np.float32([key0[m.queryIdx].pt for m in good]);dst=np.float32([key[m.trainIdx].pt for m in good])
    affine,inliers=cv2.estimateAffinePartial2D(src,dst,method=cv2.RANSAC,ransacReprojThreshold=2.0)
    assert affine is not None and np.count_nonzero(inliers)>=8, (len(good),np.count_nonzero(inliers))
    assert .88 < np.linalg.norm(affine[:,0]) < 1.12, affine
    moved=cv2.transform(points[None],affine)[0]
    # 生成動画の局所的な形の揺れも、実際のアルファ外周へ吸着させる。
    contours,_=cv2.findContours(np.uint8(im[:,:,3]>120)*255,cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_NONE)
    boundary=np.concatenate([c[:,0] for c in contours if len(c)>100]).astype(np.float32)
    snapped=[];distances=[]
    for p in moved:
        distance=np.sum((boundary-p)**2,axis=1);at=int(np.argmin(distance));d=float(np.sqrt(distance[at]))
        assert d<28, (d,p)
        snapped.append(boundary[at]);distances.append(d)
    error=np.linalg.norm(cv2.transform(src[None],affine)[0]-dst,axis=1)[inliers[:,0]>0]
    return np.array(snapped),affine,int(np.count_nonzero(inliers)),float(np.median(error)),max(distances)

frames=[];qa=[]
for i,f in enumerate(files):
    im=cv2.imread(str(f),cv2.IMREAD_UNCHANGED)
    contour,a,n,error,snap=trace(im)
    frames.append({"file":f.name,"sha256":hashlib.sha256(f.read_bytes()).hexdigest(),
                   "contour":(contour/[w,h]).round(7).tolist()})
    qa.append({"frame":i,"inliers":n,"medianReprojectionPixels":round(error,4),"maxEdgeSnapPixels":round(snap,4)})
# 静止画も同じ輪郭へ合わせる。通常のゲーム再生では上の待機連番を使う。
still=cv2.imread(str(ROOT/'DemoApp/assets/portraits/battle/hagi_idle_right.png'),cv2.IMREAD_UNCHANGED)
resized=cv2.resize(still,(w,h),interpolation=cv2.INTER_AREA)
contour,*_=trace(resized)
data={"source":"hagi_body_v2_s3033_final", "fps":16, "size":[w,h],"still":(contour/[w,h]).round(7).tolist(),"frames":frames}
(FRAMES/'weapon.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8',newline='\n')
(HERE/'tracking-audit.json').write_text(json.dumps({"method":"SIFT similarity RANSAC + alpha boundary snapping; PNG read-only","frames":qa},indent=2)+'\n',encoding='utf8',newline='\n')
print('HAGI_IDLE_BLADE_TRACK_OK frames=48 maxMedianPixels='+str(max(q['medianReprojectionPixels'] for q in qa))
      +' maxSnapPixels='+str(max(q['maxEdgeSnapPixels'] for q in qa)))
