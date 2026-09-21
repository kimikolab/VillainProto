from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

p = Path(__file__).resolve().parent
base = Image.open(p / 'shio-idle-right-v1.png').convert('RGBA')
edit = Image.open(p / 'shio-idle-right-v2-edit-source.png').convert('RGBA')
mask = Image.new('L', base.size)
# Local replacement covers both old and new arm outlines and the refolded sleeve.
ImageDraw.Draw(mask).polygon([(470,335),(556,335),(578,376),(573,462),
    (550,517),(524,563),(473,615),(426,666),(409,717),(370,794),
    (320,891),(270,963),(216,958),(189,894),(203,772),(246,670),
    (278,605),(277,465),(362,435),(402,392)], fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(3))
result = Image.composite(edit, base, mask)
outside = np.array(mask) == 0
assert np.array_equal(np.array(result)[outside], np.array(base)[outside])
result.save(p / 'shio-idle-right-v2.png')
preview = Image.new('RGBA', base.size, '#d4e5e8')
preview.alpha_composite(result)
preview.convert('RGB').save(p / 'shio-idle-right-v2-preview.jpg')
print('Verified: pixels outside right arm/sleeve patch unchanged from battle v1.')
