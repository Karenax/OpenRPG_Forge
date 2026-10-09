"""Test Detail's staged helper on its one native normal image, not the colour image."""
from pathlib import Path
import datetime
import hashlib
import json
import shutil
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'detail-normal-decoder-gpu-v1'
BUILD = W / 'detail-texture-gpu-v1'
REFERENCE = W / 'detail-native-sampling-reference-v1'
CANDIDATE = W / 'detail-normal-mode2-bevy-candidate-v1'
OLD = W / 'source-graph-imported-normal-decoder-gpu-probe-v1'
BASE = Path(__file__).with_name('queue_delta_foliage_normal_decoder_gpu_v1.py')
PACKAGE = 'openrpg-detail-normal-decoder-gpu-v1'

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def save(p, value): p.write_text(json.dumps(value, indent=2), 'utf-8')
def once(text, before, after):
    assert text.count(before) == 1, before
    return text.replace(before, after)
def function(text, name):
    start = text.index('fn ' + name + '('); opening = text.index('{', start); depth = 1
    for i in range(opening + 1, len(text)):
        depth += (text[i] == '{') - (text[i] == '}')
        if depth == 0: return text[start:i + 1]
    raise ValueError('Incomplete helper')

def prepare():
    assert not ROOT.exists()
    raw_path = W / 'detail-source-raw-gpu-parity-v1/RESULT.v1.json'
    raw = read(raw_path); candidate_path = CANDIDATE / 'RESULT.v1.json'; c = read(candidate_path)
    assert raw['normal_texture_raw_sampling_verified'] and raw['common_measured_coordinate_mappings'] == ['identity']
    assert c['status'] == 'candidate_composed' and len(c['actual_stage_compositions']) == 4
    shader_path = Path(c['shader']); assert sha(shader_path) == c['shader_sha256']
    wanted = set(read(REFERENCE / 'PREPARED.v1.json')['normal_applicable_guids'])
    assert wanted == {'abc00000000017525077657258919455'}
    rows = [r for r in read(BUILD / 'INPUT.v1.json')['rows'] if r['unity_guid'] in wanted]
    assert len(rows) == 1 and len(rows[0]['mips']) == 12 and rows[0]['gpu_format'] == 'Bc3RgbaUnorm'
    pins = dict(read(BUILD / 'PREPARED.v1.json')['input_pins'])
    for p in [raw_path,candidate_path,shader_path,REFERENCE/'PREPARED.v1.json',REFERENCE/'GPU-REFERENCE-REOPEN.v1.json',REFERENCE/'export/RESULT.json',REFERENCE/'manifest.json',BASE,OLD/'compare.py',Path(__file__).resolve()]:
        pins[str(p)] = sha(p)
    helper = '\n\n'.join(function(shader_path.read_text('utf-8-sig'), n) for n in c['helper_names'])
    assert 'if mode==2.0' in helper
    ROOT.mkdir(); (ROOT / 'src').mkdir()
    main = (BUILD / 'src/main.rs').read_text('utf-8-sig')
    main = once(main, 'assert_eq!(results.len(), 2);', 'assert_eq!(results.len(), 1);')
    main = once(main, 'assert_eq!(total_mips, 24);', 'assert_eq!(total_mips, 12);')
    assert main.count('"gpu_sampled_textures":2') == 2
    main = main.replace('"gpu_sampled_textures":2', '"gpu_sampled_textures":1')
    main = main.replace('(-0.000001..=1.000001)', '(-1.000001..=1.000001)')
    main = once(main, '"normal_decode_qualified":false', '"normal_decode_qualified":false,"candidate_decoder":"mode2_rg_or_ag","decoded_normals_in_rgba_files":true')
    (ROOT / 'src/main.rs').write_text(main, 'utf-8')
    sample = (BUILD / 'src/sample.wgsl').read_text('utf-8-sig')
    expression = 'textureSampleLevel(source_image,source_sampler,uv,parameters.x)'
    sample = helper + '\n\n' + once(sample, expression, 'source_graph_normal_sample(' + expression + ',2.0)')
    (ROOT / 'src/sample.wgsl').write_text(sample, 'utf-8')
    for name in ['imported_texture.rs','sampler.rs']: shutil.copyfile(BUILD/'src'/name, ROOT/'src'/name)
    for name in ['Cargo.toml','Cargo.lock']:
        text = (BUILD/name).read_text('utf-8-sig')
        text = once(text, 'openrpg-delta-foliage-texture-gpu-v1', PACKAGE)
        (ROOT/name).write_text(text, 'utf-8')
    compare = (OLD / 'compare.py').read_text('utf-8-sig')
    compare = compare.replace("'unity-villa-normal-sampling-reference-v1'", "'detail-native-sampling-reference-v1'")
    compare = compare.replace("'source-graph-imported-normal-gpu-parity-v2/RESULT.v1.json'", "'detail-source-raw-gpu-parity-v1/RESULT.v1.json'")
    compare = once(compare, "ref['verified_textures']==14 and gpu['gpu_sampled_textures']==14 and gpu['sampled_mip_levels']==168", "ref['verified_textures']==2 and gpu['gpu_sampled_textures']==1 and gpu['sampled_mip_levels']==12")
    compare = once(compare, "proofs={r['asset_id']:r for r in ref['rows']}", "proofs={r['asset_id']:r for r in ref['rows'] if r['asset_id'] in expected}")
    compare = once(compare, 'len(native)==len(expected)==len(proofs)==14', 'len(native)==len(expected)==len(proofs)==1')
    compare = once(compare, "  asset=request['asset_id'];nr=native[asset];er=expected[asset];rr=proofs[asset]", "  asset=request['asset_id']\n  if asset not in expected:continue\n  nr=native[asset];er=expected[asset];rr=proofs[asset]")
    compare = once(compare, "check(levels==168,'Mip coverage differs')", "check(levels==12,'Mip coverage differs')")
    compare = once(compare, "'exact_normal_textures_compared':14", "'exact_normal_textures_compared':1")
    (ROOT / 'compare.py').write_text(compare, 'utf-8')
    for p in list((ROOT/'src').iterdir()) + [ROOT/'Cargo.toml',ROOT/'Cargo.lock',ROOT/'compare.py']:
        pins[str(p)] = sha(p)
    assert all(sha(Path(p)) == h for p,h in pins.items())
    save(ROOT/'INPUT.v1.json',dict(rows=rows,source_proof_pins=pins,full_import=False))
    pins[str(ROOT/'INPUT.v1.json')] = sha(ROOT/'INPUT.v1.json')
    save(ROOT/'PREPARED.v1.json',dict(input_pins=pins,expected_textures=1,expected_mips=12,explicit_mode=2,
        actual_detail_candidate_shader=str(shader_path),exact_candidate_helpers=True,colour_texture_excluded=True,renderer_installed=False,full_import=False))
    for p in CANDIDATE.glob('LICENSE.*'): shutil.copyfile(p,ROOT/p.name)
    with (ROOT/'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    save(ROOT/'JOB.v1.json',dict(controller_pid=child.pid,created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(controller_pid=child.pid,normal_textures=1,mips=12,receipt=str(ROOT/'JOB.v1.json'))))

def run():
    text = BASE.read_text('utf-8-sig')
    text = text.replace('openrpg-delta-foliage-normal-decoder-gpu-v1.exe', PACKAGE + '.exe')
    text = text.replace("report['exact_normal_textures_compared'] == 6 and report['mip_levels_compared'] == 75", "report['exact_normal_textures_compared'] == 1 and report['mip_levels_compared'] == 12")
    text = text.replace('native_normal_textures_verified=6', 'native_normal_textures_verified=1').replace('normal_mips_verified=75','normal_mips_verified=12')
    text = text.replace('six_material','detail_material').replace('six_exact_candidate_decoders','one_detail_candidate_decoder')
    ns = dict(__file__=str(Path(__file__).resolve()),__name__='detail_probe_base')
    exec(compile(text,str(BASE),'exec'),ns)
    ns['ROOT'] = ROOT
    ns['run']()

if __name__ == '__main__': run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
