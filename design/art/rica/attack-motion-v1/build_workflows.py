"""リィカの7打を始点終点指定で生成する。原画は変更せずComfyUIの節点で位置を合わせる。"""
import argparse
import json
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO / 'tools/comfy_idle_loop'))
import idle_loop as il

SOURCE = REPO / 'DemoApp/assets/portraits/battle/motion_sources/rica/v1'
MANIFEST = json.loads((SOURCE / 'manifest.json').read_text(encoding='utf-8'))
POSES = {(p['stage'], p['key']): p for p in MANIFEST['poses']}
CW, CH, W, H = 1120, 960, 896, 768
SEGMENTS = {
    'shovel': (0, 'windup', 'finish', 'One short heavy overhead diagonal shovel blow down toward the lower right. Push from the rear leg, rotate hips then shoulders, swing both arms down, and hold the low follow-through. No flourish.'),
    'crescent': (1, 'windup', 'finish', 'One broad horizontal two-handed shovel sweep from the left across the waist toward the right. Turn the hips and shoulders, then allow the cape to follow through. Keep the shovel level during the middle of the sweep.'),
    'rise': (2, 'windup', 'rise', 'One elegant rising two-handed cut: pull the shovel low on the left, extend the front knee and rotate hips, drive the spade diagonally upward to the right, finishing in the high follow-through shown.'),
    'return': (2, 'rise', 'finish', 'One quick reverse diagonal cut from the high right follow-through down across the body to the low right finish. Both hands guide the shaft, torso twists with the cut, cape counter-rotates.'),
    'thrust': (3, 'windup', 'thrust', 'One direct precise straight thrust to the right. Sink into the front leg and extend both arms, the physical shovel remains a straight rigid spear-like tool, then hold full extension. No slash.'),
    'sweep': (3, 'thrust', 'sweep', 'Retract the extended shovel and rotate the whole torso through one fast stylish sweeping cut to the right. Pivot on one boot, lift the other knee as shown at the end, cape and apron follow the spin. One continuous full-body motion.'),
    'descend': (3, 'air_raise', 'land', 'From the airborne raised-shovel pose, drive both arms and the straight shovel diagonally down toward the lower right while dropping into a deep planted landing. One strong downward finishing cut, front knee absorbs the landing, cape settles behind. No second jump.'),
}
CHAR = ('A single petite adult elf woman Rica with silver bob hair, one chest braid, charcoal hooded cape worn down, dark teal dress, worn tan apron, brown gloves and boots. '
        'She holds ONE physical shovel: rigid straight brown shaft, one small D grip and one steel pointed spade, unchanged length and shape. Both hands stay on its shaft. ')
RULES = ('Continuous natural articulated fighting-game motion between the two poses. Knees, hips, shoulders, elbows and wrists move together. '
         'Keep the same face and clothes, same body size, fixed three-quarter side camera facing right, no zoom, entire body and entire shovel visible, empty flat solid green background. '
         'No aura, no energy weapon, no glow, no flames, no trails, no effects, no enemy, no ground shadow. ')
NEG = ('static pose, frozen, crossfade, dissolve, double exposure, morphing, extra arms, extra legs, extra shovel, duplicate blade, bent shaft, flexible weapon, missing shovel, sword, spear, '
       'energy blade, aura, glow, magic, fire, changing costume, changing face, camera movement, zoom, cuts, cropped feet, cropped shovel, scenery, shadow on background, text, watermark')


def prepared(nid, stage, key):
    p = POSES[stage, key]
    # 目～足の長さで同じ体格へ合わせる。足底と腰の横位置を共通基準にする。
    sw, sh = MANIFEST['sourceSizes']['action']
    scale = 560 / ((p['groundAnchor'][1] - p['eyeAnchor'][1]) * sh)
    rw, rh = round(sw * scale), round(sh * scale)
    x = round(530 - p['groundAnchor'][0] * rw)
    y = round(880 - p['groundAnchor'][1] * rh)
    name = f'vp_rica_motion_v1_s{stage}_{key}.png'
    shutil.copyfile(SOURCE / p['file'], Path(il.COMFY) / 'input' / name)
    nodes = [
        (nid, 'LoadImage', {'image': name}, (0, 4), key),
        (nid+1, 'ImageScale', {'image': [nid, 0], 'upscale_method': 'lanczos', 'width': rw, 'height': rh, 'crop': 'disabled'}, (1, 4), None),
        (nid+2, 'InvertMask', {'mask': [nid, 1]}, (0, 5), None),
        (nid+3, 'MaskToImage', {'mask': [nid+2, 0]}, (1, 5), None),
        (nid+4, 'ImageScale', {'image': [nid+3, 0], 'upscale_method': 'lanczos', 'width': rw, 'height': rh, 'crop': 'disabled'}, (2, 5), None),
        (nid+5, 'ImageToMask', {'image': [nid+4, 0], 'channel': 'red'}, (3, 5), None),
        (nid+6, 'EmptyImage', {'width': CW, 'height': CH, 'batch_size': 1, 'color': 0x00B140}, (2, 4), None),
        (nid+7, 'ImageCompositeMasked', {'destination': [nid+6, 0], 'source': [nid+1, 0], 'x': x, 'y': y, 'resize_source': False, 'mask': [nid+5, 0]}, (3, 4), None),
        (nid+8, 'ImageScale', {'image': [nid+7, 0], 'upscale_method': 'lanczos', 'width': W, 'height': H, 'crop': 'disabled'}, (4, 4), None),
    ]
    uv = lambda v: [(x + v[0]*rw)*W/CW, (y + v[1]*rh)*H/CH]
    return nodes, {k: uv(p[k]) for k in ['weaponSocket', 'weaponTip', 'rearHand', 'frontHand', 'groundAnchor', 'eyeAnchor']}


def workflow(key, seed):
    stage, a, b, motion = SEGMENTS[key]
    tag = f'rica_{key}_v1_s{seed}'
    pa, aa = prepared(60, stage, a)
    pb, ab = prepared(80, stage, b)
    pos = CHAR + motion + RULES
    n = [
        (1, 'UNETLoader', {'unet_name': 'wan2.2_fun_inpaint_high_noise_14B_fp8_scaled.safetensors', 'weight_dtype': 'default'}, (0, 0), None),
        (2, 'UNETLoader', {'unet_name': 'wan2.2_fun_inpaint_low_noise_14B_fp8_scaled.safetensors', 'weight_dtype': 'default'}, (0, 1), None),
        (3, 'CLIPLoader', {'clip_name': 'umt5_xxl_fp8_e4m3fn_scaled.safetensors', 'type': 'wan', 'device': 'default'}, (0, 2), None),
        (4, 'VAELoader', {'vae_name': 'wan_2.1_vae.safetensors'}, (0, 3), None),
        *pa, *pb,
        (6, 'CLIPTextEncode', {'clip': [3, 0], 'text': pos}, (1, 0), None),
        (7, 'CLIPTextEncode', {'clip': [3, 0], 'text': NEG}, (1, 1), None),
        (8, 'ModelSamplingSD3', {'model': [1, 0], 'shift': 5.0}, (2, 0), None),
        (9, 'ModelSamplingSD3', {'model': [2, 0], 'shift': 5.0}, (2, 1), None),
        (10, 'WanFunInpaintToVideo', {'positive': [6, 0], 'negative': [7, 0], 'vae': [4, 0], 'width': W, 'height': H, 'length': 25, 'batch_size': 1, 'start_image': [68, 0], 'end_image': [88, 0]}, (2, 2), None),
        (11, 'KSamplerAdvanced', {'model': [8, 0], 'add_noise': 'enable', 'noise_seed': seed, 'steps': 20, 'cfg': 3.5, 'sampler_name': 'euler', 'scheduler': 'simple', 'positive': [10, 0], 'negative': [10, 1], 'latent_image': [10, 2], 'start_at_step': 0, 'end_at_step': 10, 'return_with_leftover_noise': 'enable'}, (3, 0), None),
        (12, 'KSamplerAdvanced', {'model': [9, 0], 'add_noise': 'disable', 'noise_seed': 0, 'steps': 20, 'cfg': 3.5, 'sampler_name': 'euler', 'scheduler': 'simple', 'positive': [10, 0], 'negative': [10, 1], 'latent_image': [11, 0], 'start_at_step': 10, 'end_at_step': 20, 'return_with_leftover_noise': 'disable'}, (3, 1), None),
        (13, 'VAEDecode', {'samples': [12, 0], 'vae': [4, 0]}, (3, 2), None),
        (14, 'CreateVideo', {'images': [13, 0], 'fps': 16.0}, (4, 0), None),
        (15, 'SaveVideo', {'video': [14, 0], 'filename_prefix': f'VillainProto/rica_motion/{tag}_raw', 'format': 'auto', 'codec': 'auto'}, (4, 1), None),
        (41, 'BiRefNetRMBG', {'image': [13, 0], 'model': 'BiRefNet_toonout', 'sensitivity': 1.0, 'mask_blur': 0, 'mask_offset': 0, 'invert_output': False, 'refine_foreground': False, 'background': 'Alpha', 'background_color': '#222222'}, (5, 0), None),
        (42, 'VPGreenKeyAlpha', {'images': [13, 0], 'mask': [41, 1], 'key_low': 25.0, 'key_high': 75.0, 'despill': True, 'edge_width': 12}, (6, 0), None),
        (43, 'SaveImage', {'images': [42, 0], 'filename_prefix': f'VillainProto/rica_motion/{tag}_rgba/f'}, (7, 0), None),
    ]
    (HERE / f'{tag}-workflow.json').write_text(json.dumps(il.api_graph(n), ensure_ascii=False, indent=2), encoding='utf-8')
    return tag, n, {'stage': stage, 'from': a, 'to': b, 'positive': pos, 'negative': NEG, 'seed': seed, 'size': [W, H], 'length': 25, 'startAnchors': aa, 'endAnchors': ab}


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--keys', default=','.join(SEGMENTS))
    p.add_argument('--seed', type=int, default=2022)
    p.add_argument('--submit', action='store_true')
    p.add_argument('--front', action='store_true', help='待ち行列が自分のリィカ生成だけのとき、検品の再生成を先にする')
    a = p.parse_args()
    jobs_path = HERE / 'jobs.json'
    jobs = json.loads(jobs_path.read_text(encoding='utf-8')) if jobs_path.exists() else {}
    for key in a.keys.split(','):
        tag, nodes, info = workflow(key, a.seed)
        if a.submit and tag not in jobs:
            if a.front:
                pending=il.http_json('/queue')['queue_pending']
                for item in pending:
                    graph=item[2]
                    if 'rica_' not in json.dumps(graph):
                        raise RuntimeError('他の仕事が待機しているため優先投入しない')
                info['prompt_id']=il.http_json('/prompt',{'prompt':il.api_graph(nodes),'client_id':'rica-motion-retry','front':True})['prompt_id']
            else:
                info['prompt_id'] = il.submit(nodes)
            jobs[tag] = info
            jobs_path.write_text(json.dumps(jobs, ensure_ascii=False, indent=2), encoding='utf-8')
            print('投入:', tag, info['prompt_id'], flush=True)
        elif tag not in jobs:
            jobs[tag] = info
    if not a.submit:
        (HERE / 'workflow-spec.json').write_text(json.dumps(jobs, ensure_ascii=False, indent=2), encoding='utf-8')
