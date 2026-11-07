"""Seal the corrected Unity reference, including unclipped HDR pixels and camera checks."""
from pathlib import Path
import datetime, hashlib, json, struct
import numpy as np
from PIL import Image

W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-controlled-unity-reference-v689'
assert not O.exists()
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()
run_path = W / 'house-native-component-state-v688/RUN-RESULT.v1.json'
native_path = run_path.with_name('NATIVE-RESULT.v1.json')
raw_path = run_path.with_name('UNITY-LINEAR.rgba16f')
context_path = W / 'house-controlled-gpu-fixture-v679/CONTROLLED-CONTEXT.v1.json'
run, native, context = map(read, [run_path, native_path, context_path])
assert run['unity_exit_code'] == 0 and not run['source_changes'] and not run['private_prefab_changes']
assert sha(native_path) == run['native_sha256']
assert native['source_object_unchanged'] and native['gpu'] == 'Direct3D12'
assert native['postprocessing_enabled'] is False
assert native['actual_directional_light_intensity'] == context['light_intensity']
assert native['camera_field_of_view'] == context['fov']
assert len(native['images']) == len(native['renderers']) == 1
pins = {str(p): sha(p) for p in [Path(__file__),run_path,native_path,raw_path,context_path]}
for p,h in run['input_pins'].items():
    assert sha(p) == h,p
    pins[p] = h
image = native['images'][0]
assert sha(image['path']) == image['sha256']; pins[image['path']] = image['sha256']
assert image['width'] == image['height'] == 512
for observed, field in [('camera_position','camera_position'), ('target','bounds_center')]:
    for axis in 'xyz':
        expected = context[field][axis] * (-1 if axis == 'z' else 1)
        assert struct.pack('<f',image[observed][axis]) == struct.pack('<f',expected)
assert native['compiled_passes'] and all(p['compiled'] for p in native['compiled_passes'])
raw = raw_path.read_bytes()
assert len(raw) == 512*512*8
pixels = np.frombuffer(raw,dtype='<f2').reshape(512,512,4).astype(np.float32)
assert np.isfinite(pixels).all()
assert np.unique(pixels[:,:,:3].reshape(-1,3),axis=0).shape[0] > 100
# Diagnostic display only: a documented -8-stop transform of numeric HDR data.
# The measured raw source remains intact and is the sole comparison input.
rgb = np.maximum(pixels[:,:,:3],0) / 256
display = np.where(rgb <= .0031308,12.92*rgb,1.055*np.power(rgb,1/2.4)-.055)
display = np.clip(np.rint(display*255),0,255).astype(np.uint8)
O.mkdir()
image_path = O / 'HDR-DIAGNOSTIC-MINUS8EV.png'
Image.fromarray(np.flipud(display)).save(image_path)
result = dict(schema='hivemind.house-controlled-unity-reference.v689',
              utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              raw_image=str(raw_path),raw_sha256=sha(raw_path),
              camera_source_frame_verified=True,original_passes_compiled=len(native['compiled_passes']),
              pixels_finite=True,rgb_min=float(pixels[:,:,:3].min()),rgb_max=float(pixels[:,:,:3].max()),
              diagnostic_display=str(image_path),diagnostic_display_sha256=sha(image_path),
              diagnostic_transform='linear RGB / 256, sRGB encoding, vertical flip for display',
              input_pins=pins,forge_render_comparison_verified=False,
              unity_scene_parity=False,studio_verified=False,full_import=False)
assert all(sha(p)==h for p,h in pins.items())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note = '''## House — référence de rendu Unity contrôlée conservée (v689)

- Objet du banc GPU v679 capturé avec le shader et les textures Unity originaux, sur Direct3D12. Caméra convertie de repère vérifiée, éclairage de 1000 lux, post-traitement désactivé.
- Pixels HDR 512x512 RGBAHalf finis conservés sans écrêtage ; prévisualisation diagnostique à -8 stops séparée. Capture initiale v687 avec halo conservée, remplacée pour comparaison par v688.
- Unity terminé avec code 0 ; sources et prefabs privés inchangés. Preuve : F:/OpenRPG_ImportWork/house-controlled-unity-reference-v689/RESULT.v1.json.
- Comparaison au rendu Forge, physique complète et Studio encore à réaliser. Ce banc ne remplace pas une référence de scène source. Incrément complet : 0.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes()
    (O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps({k:result[k] for k in ['original_passes_compiled','rgb_min','rgb_max','diagnostic_display']}))
