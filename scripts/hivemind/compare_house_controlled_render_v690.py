"""Compare original unclipped Unity HDR to the actual Forge fixture, without fitting."""
from pathlib import Path
import datetime, hashlib, json
import numpy as np
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-controlled-render-comparison-v690'
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
gpu_path=W/'house-controlled-gpu-fixture-v679/RUN-RESULT.v1.json'
gpu=read(gpu_path)
assert gpu['status']=='controlled_house_gpu_draw_and_native_bc_bindings_verified', gpu['status']
assert not O.exists()
reference_path=W/'house-controlled-unity-reference-v689/RESULT.v1.json'
reference=read(reference_path)
native_path=gpu_path.with_name('NATIVE-RESULT.v1.json')
native=read(native_path)
forge_path=gpu_path.with_name('FORGE-LINEAR.rgba16f')
unity_path=Path(reference['raw_image'])
assert sha(native_path)==gpu['native_receipt_sha256']
assert sha(forge_path)==gpu['image_sha256']
assert sha(unity_path)==reference['raw_sha256']
pins={str(p):sha(p) for p in [Path(__file__),gpu_path,reference_path,native_path,forge_path,unity_path]}
for source in [gpu,reference]:
    for p,h in source['input_pins'].items():
        assert sha(p)==h,p
        pins[p]=h
assert native['gpu_draw_executed'] and native['consecutive_equal_readbacks']>=3
def pixels(p):
    assert p.stat().st_size==512*512*8
    a=np.fromfile(p,dtype='<f2').reshape(512,512,4).astype(np.float32)
    assert np.isfinite(a).all()
    return a
# Unity ReadPixels origin is bottom-left; the wgpu texture copy is top-left.
# This is a fixed origin conversion, never selected by best image agreement.
unity=np.flipud(pixels(unity_path));forge=pixels(forge_path)
delta=np.abs(forge[:,:,:3]-unity[:,:,:3])
threshold=1e-5+1e-5*np.abs(unity[:,:,:3])
fails=delta>threshold
def diagnostic(a):
    linear=np.maximum(a[:,:,:3],0)/256
    return np.clip(np.where(linear<=.0031308,12.92*linear,1.055*np.power(linear,1/2.4)-.055),0,1)
O.mkdir()
canvas=Image.new('RGB',(1536,548),'white');draw=ImageDraw.Draw(canvas)
for i,(a,label) in enumerate([(unity,'Unity HDR: display -8 stops'),(forge,'Forge HDR: display -8 stops')]):
    canvas.paste(Image.fromarray(np.rint(diagnostic(a)*255).astype(np.uint8)),(i*512,36))
    draw.text((i*512+8,8),label,fill='black')
maximum=float(delta.max());heat=delta.max(axis=2)/max(maximum,1e-30)
canvas.paste(Image.fromarray(np.rint(heat*255).astype(np.uint8)).convert('RGB'),(1024,36))
draw.text((1032,8),'Absolute RGB error: black=0, white='+str(maximum),fill='black')
figure_path=O/'COMPARISON.png';canvas.save(figure_path)
result=dict(schema='hivemind.house-controlled-render-comparison.v690',
            utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
            status='controlled_hdr_render_matches' if not fails.any() else 'controlled_hdr_render_mismatches_retained',
            compared_rgb_components=int(delta.size),failed_rgb_components=int(fails.sum()),
            failed_pixels=int(fails.any(axis=2).sum()),maximum_absolute_error=float(delta.max()),
            mean_absolute_error=float(delta.mean()),error_percentiles={str(p):float(np.percentile(delta,p)) for p in [50,90,95,99]},
            tolerance='abs 1e-5 + relative 1e-5 against unclipped Unity reference',
            origin_conversion='Unity ReadPixels bottom-left to wgpu top-left; fixed vertical flip',
            exposure_or_color_fit_applied=False,comparison_figure=str(figure_path),figure_sha256=sha(figure_path),
            input_pins=pins,unity_scene_parity=False,studio_verified=False,full_import=False)
assert all(sha(p)==h for p,h in pins.items())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note=f'''## House — comparaison HDR du banc de rendu (v690)

- Pixels Unity originaux comparés au vrai readback GPU Forge, sans ajustement d'exposition ni de couleur. Origine verticale convertie selon les API.
- Résultat : {result['failed_rgb_components']} composantes RGB et {result['failed_pixels']} pixels hors tolérance ; écart maximal {result['maximum_absolute_error']}.
- Preuve : F:/OpenRPG_ImportWork/house-controlled-render-comparison-v690/RESULT.v1.json. Les écarts restent à expliquer et corriger ; aucune qualification complète ni preuve Studio déduite.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes()
    (O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps({k:result[k] for k in ['status','failed_rgb_components','failed_pixels','maximum_absolute_error']}))
