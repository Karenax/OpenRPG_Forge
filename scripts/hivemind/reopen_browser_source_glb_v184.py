"""Reopen the real Web draw/recovery and compare untouched source/native pixels."""
from pathlib import Path
from datetime import datetime, timezone
import ast
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw

W = Path('F:/OpenRPG_ImportWork')
R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
CAP = W / 'browser-source-glb-v183'
O = W / 'browser-source-glb-reopen-v184'
pins = {}

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

def read(path):
    pins[str(path)] = sha(path)
    return json.loads(Path(path).read_text('utf-8-sig'))

def pin(path, expected):
    assert sha(path) == expected, path
    pins[str(path)] = expected

def main():
    assert not O.exists()
    run = read(CAP / 'RUN-RESULT.v1.json')
    receipt = read(CAP / 'BROWSER-RESULT.v1.json')
    assert run['status'] == 'actual_common_browser_glb_binding_draw_and_recovery_passed'
    assert run['build_exit_code'] == run['bindgen_exit_code'] == 0
    assert pins[str(CAP / 'BROWSER-RESULT.v1.json')] == run['browser_receipt_sha256']
    assert receipt['gpu_errors'] == receipt['resource_receipt']['gpu_validation_errors'] == []
    assert receipt['draw_receipt']['consecutive_equal_readbacks'] >= 3
    assert receipt['recovery_consecutive_equal_readbacks'] >= 3 and receipt['recovery_pixels_byte_equal']
    resources = receipt['resource_receipt']; textures = resources['textures']
    assert resources['native_srgb_required_and_ready'] and resources['native_srgb_actual_binding_identity_verified']
    assert len(textures) == 10 and sum(t['mips'] for t in textures) == 120
    assert all(t['byte_equal'] and t['gpu_resident'] and t['actual_binding_identity_verified'] for t in textures)
    assert run['shader_diagnostics'] == {}
    pin(CAP / 'BROWSER-PACKED.rg11b10',receipt['packed_image_sha256'])
    assert (CAP / 'MATERIAL-CURVE.r32f').read_bytes() == bytes(receipt['material_curve']) == bytes(receipt['recovered_material_curve']) == (CAP / 'RECOVERED-MATERIAL-CURVE.r32f').read_bytes()
    for p in [CAP / 'MATERIAL-CURVE.r32f',CAP / 'RECOVERED-MATERIAL-CURVE.r32f']:pins[str(p)] = sha(p)
    assert (CAP / 'MATERIAL-CURVE.r32f').stat().st_size == 1024
    prepared = read(CAP / 'PREPARED.v1.json')
    for path,digest in prepared['input_pins'].items():pin(path,digest)
    base = R / 'scripts/hivemind/compare_townsmith_common_srgb_components_v93.py'
    tree = ast.parse(base.read_text('utf-8-sig'))
    node = next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name == 'packed_pixels')
    scope = {'np':np};exec(compile(ast.Module(body=[node],type_ignores=[]),str(base),'exec'),scope)
    decode = scope['packed_pixels']; pins[str(base)] = sha(base)
    native_root = W / 'townsmith-deferred-indirect-layer-0-v94'
    native = read(native_root / 'RUN-RESULT.v1.json')
    pin(native_root / 'FORGE-PACKED.rg11b10',native['forge_packed_image_sha256'])
    prior = read(W / 'townsmith-deferred-indirect-comparison-v94/RESULT.v1.json')
    source = W / 'townsmith-door-source-direct-controls-v18/door-nopost-linear.rgba16f'
    depth = W / 'townsmith-door-source-material-values-v41/door-nopost-linear.depth-3.r32f'
    pin(source,prior['input_pins'][str(source)]);pin(depth,prior['input_pins'][str(depth)])
    roi = (np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1]
    assert int(roi.sum()) == 34445
    src = np.fromfile(source,dtype='<f2').reshape(512,512,4)[::-1,:,:3].astype('float32')
    native_values = decode(native_root / 'FORGE-PACKED.rg11b10')
    browser = decode(CAP / 'BROWSER-PACKED.rg11b10')
    assert np.isfinite(src).all() and np.isfinite(native_values).all() and np.isfinite(browser).all()
    rows = []
    for name,a,b in [('source_vs_native',src,native_values),('source_vs_browser',src,browser),('native_vs_browser',native_values,browser)]:
        delta = np.abs(a-b)[roi]
        rows.append(dict(comparison=name,mean_rgb_error=float(delta.mean()),max_rgb_error=float(delta.max()),pixels_over_001=int((delta.max(axis=1)>.001).sum()),exact_channels_fraction=float((delta==0).mean())))
    O.mkdir();panel = Image.new('RGB',(1536,544),'#202020');draw = ImageDraw.Draw(panel)
    for index,(label,values) in enumerate([('Source Unity',src),('Forge native',native_values),('Forge WebGPU',browser)]):
        rgb = np.maximum(values,0)
        rgb = np.where(rgb<=.0031308,12.92*rgb,1.055*rgb**(1/2.4)-.055)
        pixels = np.round(np.clip(rgb,0,1)*255).astype('uint8');pixels[~roi]=45
        draw.text((index*512+8,8),label,fill='white');panel.paste(Image.fromarray(pixels),(index*512,28))
    image = O / 'source-native-browser.png';panel.save(image);pins[str(image)] = sha(image)
    pins[str(Path(__file__).resolve())] = sha(Path(__file__).resolve())
    result = dict(status='actual_web_material_draw_and_device_recovery_independently_reopened_source_fidelity_open',finished_utc=datetime.now(timezone.utc).isoformat(),texture_bindings_verified=10,native_mips_verified=120,raw_gpu_bytes=1048576,stable_readbacks=receipt['draw_receipt']['consecutive_equal_readbacks'],recovery_readbacks=receipt['recovery_consecutive_equal_readbacks'],recovery_pixels_byte_equal=True,shader_compilation_errors=0,source_fixed_roi_pixels=34445,rows=rows,pixel_error_threshold_unchanged=.001,source_pixels_applied_to_renderer=False,cpu_color_adjustment=False,preview_display_only='Standard linear-to-sRGB encoding; fixed source ROI; no exposure fit or pixel correction',agent_image_review=False,source_render_equivalence_qualified=False,studio_verified=False,full_import=False,input_pins=pins)
    (O / 'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))

if __name__ == '__main__':main()
