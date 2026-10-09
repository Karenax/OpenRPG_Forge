"""Stage the measured RGorAG helper; preserve the earlier Detail program."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'detail-normal-mode2-bevy-candidate-v1'
OLD = W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4'

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def function(text, name):
    start = text.index('fn ' + name + '(')
    opening = text.index('{', start)
    depth = 1
    for i in range(opening + 1, len(text)):
        depth += (text[i] == '{') - (text[i] == '}')
        if depth == 0: return text[start:i + 1]
    raise ValueError('Incomplete helper')

def main():
    assert not ROOT.exists()
    parent_path = W / 'detail-native-default-bevy-compositions-v2/RESULT.v1.json'
    parent = read(parent_path)
    assert parent['actual_positive_compositions'] == 4 and not parent['failures']
    row = parent['rows'][0]
    original = Path(row['shader'])
    assert sha(original) == row['sha256']
    donor_path = W / 'delta-rocks-firepit-native-lit-v1/INDEPENDENT-REOPEN.v1.json'
    donor = read(donor_path)
    helpers = []
    pins = {str(p): sha(p) for p in [parent_path, original, donor_path, Path(__file__).resolve()]}
    for r in donor['rows']:
        for stage in ['forward', 'prepass']:
            p = Path(r[stage]); assert sha(p) == r[stage + '_sha256']
            pins[str(p)] = sha(p)
            t = p.read_text('utf-8-sig')
            helpers.append(tuple(function(t, n) for n in ['source_decode_normal', 'source_graph_normal_sample']))
    assert len(helpers) == 4 and len(set(helpers)) == 1
    assert 'if mode==2.0' in helpers[0][1]
    program = original.read_text('utf-8-sig')
    for name, replacement in zip(['source_decode_normal', 'source_graph_normal_sample'], helpers[0]):
        previous = function(program, name)
        assert program.count(previous) == 1
        program = program.replace(previous, replacement, 1)
    assert '@binding(120)' in program and 'output.native_default_mask_rg=source_native_default_scalars.lanes[0].xy;' in program
    old_path = OLD / 'RESULT.v1.json'; old = read(old_path)
    validator = OLD / 'validate_bevy_vertex.exe'; modules = OLD / 'module-list.txt'
    assert sha(validator) == old['validator_sha256']
    assert set(modules.read_text('utf-8-sig').splitlines()) == set(r['copy'] for r in old['bevy_primary_module_pins'])
    for m in old['bevy_primary_module_pins']:
        p = Path(m['copy']); assert sha(p) == m['sha256']; pins[str(p)] = sha(p)
    for p in [old_path, validator, modules]: pins[str(p)] = sha(p)
    ROOT.mkdir()
    output = ROOT / 'source_graph_vertex_and_prepass.wgsl'; output.write_text(program, 'utf-8')
    results = []
    for config in row['actual_stage_compositions']:
        name = config['permutation']
        with (ROOT / (name + '.stdout.log')).open('xb') as out, (ROOT / (name + '.stderr.log')).open('xb') as err:
            code = subprocess.run([str(validator), str(modules), str(output), *config['shader_defines']], stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW).returncode
        results.append(dict(permutation=name, actual_exit_code=code, shader_defines=config['shader_defines']))
    for p, h in pins.items(): assert sha(Path(p)) == h, p
    for p in [OLD / 'LICENSE.Bevy.MIT.txt', W / 'detail-native-default-stages-v1/LICENSE.Unity.MIT.txt']:
        if p.exists(): (ROOT / p.name).write_bytes(p.read_bytes())
    report = dict(status='candidate_composed' if all(r['actual_exit_code'] == 0 for r in results) else 'refused',
        material_guid=row['material_guid'], shader=str(output), shader_sha256=sha(output),
        actual_stage_compositions=results, helper_names=['source_decode_normal','source_graph_normal_sample'],
        identical_to_four_pinned_donor_stages=True, binding_120_preserved=True,
        normal_mode2_host_admission_required=True, normal_gpu_parity_qualified=False,
        renderer_installed=False, full_import=False, input_pins=pins,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(report, indent=2), 'utf-8')
    assert report['status'] == 'candidate_composed'
    print(json.dumps(dict(status=report['status'], actual_compositions=len(results), receipt=str(ROOT / 'RESULT.v1.json'))))

if __name__ == '__main__': main()
