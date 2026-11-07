"""Deliver the sixteen individually inspected HOUSE.05 source thumbnail views."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
observations = [
    'Two complete views of purple hydrangea flower heads with green leaves and thin stems; no silhouette clipping.',
    'Two complete views of a wall lamp with a curved bracket and rust-coloured surface with blue patches. Illumination behaviour remains unverified.',
    'Two complete views of a tall stone lighthouse, including base steps, balcony railing and top lantern. Runtime building behaviour remains unverified.',
    'Two complete views of a rounded terracotta pot with a wide opening and visible surface cracks.',
    'Two complete views of a squat terracotta pot with a raised rim and textured walls; original variant identity retained.',
    'Two complete views of a shallow terracotta bowl, with its interior and cracks visible.',
    'Two complete views of a tapered terracotta planter with rim, interior and cracked surface.',
    'Two complete views of a tapered terracotta planter variant. Visible rectangular texture boundaries are retained from the source capture; no retouch or runtime qualification.',
]
source = (S / 'index_house_images_0017_0024_v1.py').read_text(encoding='utf-8')
source = source.replace('house-source-thumbnail-batch-0017-0024', 'house-source-thumbnail-batch-0025-0032')
source = source.replace('L08.IMAGE.HOUSE.04', 'L08.IMAGE.HOUSE.05')
start = source.index('observations = [')
end = source.index('\nassert len(candidate', start)
source = source[:start] + 'observations = ' + repr(observations) + source[end:]
# Reuse the already corrected eight-card apply recipe, changing only its lot paths.
source = source.replace('apply_house_images_0009_0016_v1.py', 'apply_house_images_0017_0024_v1.py')
source = source.replace("target = S / 'apply_house_images_0017_0024_v1.py'", "target = S / 'apply_house_images_0025_0032_v1.py'")
source = source.replace("helper.replace('house-source-thumbnail-batch-0009-0016',", "helper.replace('house-source-thumbnail-batch-0017-0024',")
target = S / 'index_house_images_0025_0032_v1.py'
assert not target.exists()
target.write_text(source, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
for old_name, new_name in [
    ('run_house_images_0017_0024_native_v1.py', 'run_house_images_0025_0032_native_v1.py'),
    ('seal_house_images_0017_0024_v1.py', 'seal_house_images_0025_0032_v1.py'),
    ('record_house_registry_epoch_0017_0024_v1.py', 'record_house_registry_epoch_0025_0032_v1.py'),
]:
    code = (S / old_name).read_text(encoding='utf-8')
    if 'registry_epoch' in old_name:
        code = code.replace('house-source-thumbnail-batch-0017-0024-index', 'house-source-thumbnail-batch-0025-0032-index')
        code = code.replace('house-source-thumbnail-batch-0009-0016-delivery', 'house-source-thumbnail-batch-0017-0024-delivery')
        code = code.replace('house-registry-epoch-0017-0024', 'house-registry-epoch-0025-0032')
    else:
        code = code.replace('house-source-thumbnail-batch-0017-0024', 'house-source-thumbnail-batch-0025-0032')
        code = code.replace('L08.IMAGE.HOUSE.04', 'L08.IMAGE.HOUSE.05')
    target = S / new_name
    assert not target.exists()
    target.write_text(code, encoding='utf-8')
    subprocess.run([sys.executable, '-B', str(target)], check=True)
