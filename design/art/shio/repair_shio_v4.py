from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

p = Path(__file__).resolve().parent
base = Image.open(p / 'shio-standing-v1.png').convert('RGBA')
hair = Image.open(p / 'shio-standing-v2.png').convert('RGBA')
cutout = Image.open(p / 'shio-standing-v3.png').convert('RGBA')
mask = Image.new('L', base.size)
ImageDraw.Draw(mask).polygon([(508,162),(531,159),(538,176),(541,193),
    (548,208),(557,226),(551,246),(529,245),(510,229),(503,199)], fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(2))
result = np.array(Image.composite(hair, base, mask))
original = np.array(base)
alpha = np.array(cutout)[:,:,3]
# Use only the repaired alpha within the enclosed waist gap; retain v1 RGB.
region = np.zeros(alpha.shape, dtype=bool)
region[433:546, 477:565] = True
result[:,:,3][region] = np.minimum(result[:,:,3][region], alpha[region])
allowed = (np.array(mask) > 0) | region
assert np.array_equal(result[~allowed], original[~allowed])
assert np.array_equal(result[region,:3], original[region,:3])
Image.fromarray(result).save(p / 'shio-standing-v4.png')
preview = Image.new('RGBA', base.size, '#d4e5e8')
preview.alpha_composite(Image.fromarray(result))
preview.convert('RGB').save(p / 'shio-standing-v4-preview.jpg')
detail = Image.new('RGB',(660,390),'#d4e5e8')
for x, box in [(0,(480,145,575,265)),(330,(470,425,570,550))]:
    crop = preview.crop(box).resize((310,375))
    detail.paste(crop,(x,0))
detail.save(p / 'shio-standing-v4-detail.png')
print('Verified: all pixels outside hair patch and waist alpha region are identical to v1.')
print('Waist RGB unchanged. Gap alpha:', result[475,520,3], result[480,530,3])
