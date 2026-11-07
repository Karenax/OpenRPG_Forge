"""Compare raw source/Forge pixels, retain refusal and display diagnostic views."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json
import numpy as np
from PIL import Image, ImageDraw

W=Path('F:/OpenRPG_ImportWork')
DRAW=W/'townsmith-paint-door-native-draw-v2'
SOURCE=W/'townsmith-door-source-render-context-v2'
ROOT=W/'townsmith-paint-door-native-draw-comparison-v2'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

assert not ROOT.exists()
run=read(DRAW/'RUN-RESULT.v1.json');native=read(DRAW/'NATIVE-RESULT.v1.json')
assert run['actual_exit_code']==0 and native['gpu_draw_executed'] and native['consecutive_equal_readbacks']>=3
assert sha(DRAW/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
pins=dict(read(DRAW/'PREPARED.v1.json')['input_pins'])
for p,h in pins.items():assert sha(Path(p))==h,p
for p in [Path(__file__),DRAW/'RUN-RESULT.v1.json',DRAW/'NATIVE-RESULT.v1.json',DRAW/'FORGE-LINEAR.rgba16f',
          DRAW/'RESOURCE-RESULT.v1.json',SOURCE/'RUN-RESULT.v1.json',SOURCE/'door-neutral.png',SOURCE/'door-linear.png']:
    pins[str(p)]=sha(p)
source=np.fromfile(SOURCE/'door-linear.rgba16f',dtype='<f2').reshape(512,512,4).astype(np.float32)
forge=np.fromfile(DRAW/'FORGE-LINEAR.rgba16f',dtype='<f2').reshape(512,512,4).astype(np.float32)
assert np.isfinite(source).all() and np.isfinite(forge).all()
# Unity ReadPixels rows start at the lower left; wgpu copy rows at the upper left.
# No translation, scale, rotation, exposure fit, threshold adjustment or best-fit flip.
source=np.flipud(source)
rgb_error=np.abs(source[:,:,:3]-forge[:,:,:3])
per_pixel=rgb_error.max(axis=2)
threshold=0.001
source_bg=source[0,0,:3];forge_bg=forge[0,0,:3]
source_mask=np.max(np.abs(source[:,:,:3]-source_bg),axis=2)>threshold
forge_mask=np.max(np.abs(forge[:,:,:3]-forge_bg),axis=2)>threshold
intersection=int((source_mask&forge_mask).sum());union=int((source_mask|forge_mask).sum())
ROOT.mkdir()
def neutral(x):
    x=np.maximum(x,np.float32(0))
    a,b,c,d,e,f=[np.float32(v) for v in [.2,.29,.24,.272,.02,.3]]
    def curve(v):return ((v*(a*v+c*b)+d*e)/(v*(a*v+b)+d*f))-e/f
    white=np.float32(1)/curve(np.float32(5.3))
    return curve(np.minimum(x,np.float32(435.18712))*white)*white
def srgb(x):
    x=np.clip(x,0,1)
    return np.where(x<=.0031308,x*12.92,1.055*np.power(x,1/2.4)-.055)
sheet=Image.new('RGB',(1024,548),(32,32,32));labels=ImageDraw.Draw(sheet)
for column,(array,label) in enumerate([(source,'Unity original — linear capture / diagnostic Neutral display'),
                                      (forge,'Forge real draw — same diagnostic display')]):
    pixels=np.rint(srgb(neutral(array[:,:,:3]))*255).astype(np.uint8)
    view=Image.fromarray(pixels)
    path=ROOT/('source-diagnostic.png' if column==0 else 'forge-diagnostic.png')
    view.save(path);pins[str(path)]=sha(path)
    sheet.paste(view,(column*512,36));labels.text((column*512+8,10),label,fill=(255,255,255))
sheet_path=ROOT/'comparison.png';sheet.save(sheet_path);pins[str(sheet_path)]=sha(sheet_path)
result=dict(status='actual_source_material_draw_compared_fidelity_unqualified',
    finished_utc=datetime.now(timezone.utc).isoformat(),lot='L03.TOWNSMITH.DESSIN',
    gpu_draw_executed=True,stable_native_readbacks=native['consecutive_equal_readbacks'],
    image_size=[512,512],source_row_convention='Unity bottom-to-top normalized once to wgpu top-to-bottom',
    rgb_threshold=threshold,changed_rgb_pixels=int((per_pixel>threshold).sum()),
    max_rgb_error=float(rgb_error.max()),mean_rgb_error=float(rgb_error.mean()),
    source_rgb_range=[float(source[:,:,:3].min()),float(source[:,:,:3].max())],
    forge_rgb_range=[float(forge[:,:,:3].min()),float(forge[:,:,:3].max())],
    source_background=source_bg.tolist(),forge_background=forge_bg.tolist(),
    source_nonbackground_pixels=int(source_mask.sum()),forge_nonbackground_pixels=int(forge_mask.sum()),
    silhouette_intersection_pixels=intersection,silhouette_union_pixels=union,
    silhouette_iou=intersection/union if union else 0,
    native_source_sampling_context='Unity ForceEnable captured; common explicit D3D policy used for this draw',
    pixel_fit_or_threshold_relaxation=False,display_images_are_diagnostic_only=True,
    source_lighting_parity_qualified=False,source_sampling_parity_qualified=False,
    source_render_parity_qualified=False,forge_editor_verified=False,
    full_import=False,complete_assets_delivered_increment=0,input_pins=pins)
(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
