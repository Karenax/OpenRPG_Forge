"""Independent edited normal-map formula, native mip execution and isolated effect."""
import hashlib
import json
from pathlib import Path
import sys
import numpy as np
from PIL import Image

root, proof, report_path = map(Path, sys.argv[1:])
assert not report_path.exists()
archive = json.loads((root/'authored-relief-states-001/raised.json').read_text())
heights = np.fromfile(root/'source-017/terrain/heights.bin', dtype='<f4').reshape(2048,2048)
deltas = np.zeros((2049,2049), dtype=np.float64)
for item in archive['head']['corrections']['height_deltas']:
    deltas[item['sample_z'],item['sample_x']] = item['delta_m']
edited = (heights.astype(np.float64)+(deltas[:-1,:-1]+deltas[:-1,1:]
          +deltas[1:,:-1]+deltas[1:,1:])*.25).astype(np.float32).astype(np.float64)
horizontal = np.pad(edited, ((0,0),(1,1)), mode='edge')
vertical = np.pad(edited, ((1,1),(0,0)), mode='edge')
hx = (horizontal[:,2:]-horizontal[:,:-2])*.5
hz = (vertical[2:]-vertical[:-2])*.5
inverse = 1/np.sqrt(hx*hx+1+hz*hz)
rg = np.stack([(-hx*inverse*.5+.5)*255+.5,(-hz*inverse*.5+.5)*255+.5],axis=2).astype(np.uint8)
original = np.fromfile(root/'workspace-terrain-scene-001/terrain-normal.bin',dtype=np.uint8,
                       count=2048*2048*4).reshape(2048,2048,4)
changed = int(np.any(rg!=original[:,:,:2],axis=2).sum())
expected = original.copy()
expected[:,:,:2] = rg
digest = hashlib.sha256(expected.tobytes()).hexdigest()
log = (proof/'raised.log').read_text(encoding='utf-8')
assert f'changed_texels={changed}, base_sha256={digest}' in log
assert log.count('authored terrain normal mipmaps: 11 native passes complete') == 1
assert 'authored terrain normal mipmaps' not in (proof/'undo.log').read_text(encoding='utf-8')
previous = root/'relief-shadow-render-proof-001'
for name in ['raised.object-depth.bin','raised.shadow.bin']:
    assert (proof/name).read_bytes() == (previous/name).read_bytes(), name
a = np.array(Image.open(proof/'raised.png').convert('RGBA')).astype(np.int16)
b = np.array(Image.open(previous/'raised.png').convert('RGBA')).astype(np.int16)
diff = np.abs(a-b)
assert diff.any()
report = {'status':'pass','normal_base_sha256':digest,'normal_base_changed_texels':changed,
          'native_mip_passes':11,'object_depth_and_hill_shadow_unchanged':True,
          'lighting_changed_pixels':int(np.any(diff,axis=2).sum()),
          'maximum_channel_delta':int(diff.max()),'mean_channel_delta':float(diff.mean()),
          'scope':'Edited macro normal RG and executed GPU mip algorithm; per-mip source parity, AO and rock masks not qualified'}
report_path.write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
