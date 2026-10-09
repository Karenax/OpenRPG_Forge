"""Measure exact foliage normal images after the GPU storage probe terminates."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'unity-delta-foliage-normal-reference-v1'
OLD = W / 'unity-villa-normal-sampling-reference-v1'
STORAGE = W / 'unity-delta-foliage-texture-storage-v2'
GPU = W / 'delta-foliage-texture-gpu-v3'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value, new=False):
    if new:
        with (ROOT / name).open('x', encoding='utf-8') as stream:
            json.dump(value, stream, indent=2)
    else:
        temp = ROOT / (name + '.tmp')
        temp.write_text(json.dumps(value, indent=2), encoding='utf-8')
        temp.replace(ROOT / name)


def check(pins):
    assert all(sha(path) == digest for path, digest in pins.items()), 'Reference input changed'


def prepare():
    assert not ROOT.exists()
    kernel_path = W / 'delta-vector-node-wgsl-v3/RESULT.v1.json'
    kernels = read(kernel_path)
    guids = {texture['guid'] for row in kernels['rows'] for texture in row['source_texture_bindings']
             if texture['requires_normal_decode']}
    assert len(guids) == 6
    capture = read(STORAGE / 'export/RESULT.json')
    source_prep = read(STORAGE / 'PREPARED.v1.json')
    project = Path(source_prep['project'])
    copies_by_path = {row['project_copy']: row for row in source_prep['copies']}
    assert read(STORAGE / 'TEXTURE-REOPEN.v1.json')['source_texture_storage_captures_verified'] == 18
    pins = {str(p): sha(p) for p in [kernel_path, STORAGE / 'export/RESULT.json',
        STORAGE / 'TEXTURE-REOPEN.v1.json', OLD / 'NormalSamplingSnapshot.cs', OLD / 'NormalSamplingReference.shader',
        OLD / 'verify_reference.py', Path(__file__).resolve()]}
    cases, copies = [], []
    for case in capture['cases']:
        if case['actual_guid'] not in guids:
            continue
        source = copies_by_path[str(project / case['asset_path'])]
        for key, digest in [('original', 'original_sha256'), ('original_meta', 'original_meta_sha256'),
                            ('project_copy', 'project_copy_sha256')]:
            assert sha(source[key]) == source[digest]
            pins[source[key]] = source[digest]
        stored = dict(asset_id=case['asset_id'], unity_graphics_format=case['graphics_format'],
            width=case['width'], height=case['height'], mips=case['mips'],
            resolved_sampler={key: case[key] for key in ['filter_mode', 'wrap_u', 'wrap_v', 'wrap_w', 'anisotropy', 'mip_bias']})
        copies.append(dict(source, expected_storage_row=stored))
        cases.append(dict(asset_id=case['asset_id'], asset_path=case['asset_path'],
                          expected_guid=case['actual_guid'], expected_file_id=case['actual_file_id']))
    assert len(cases) == 6
    helper = project / 'Assets/Editor/DeltaFoliageNormalSamplingSnapshotV1.cs'
    shader = project / 'Assets/OpenRPGFoliageProbe/NormalSamplingReference.shader'
    assert not helper.exists() and not shader.exists()
    # Only owned measurement code is added; existing imported images are reused.
    code = (OLD / 'NormalSamplingSnapshot.cs').read_text('utf-8-sig')
    assert code.count('namespace OpenRPGNormalSamplingProbe') == 1
    assert code.count('Assets/SourceTextures/') == 1
    code = code.replace('namespace OpenRPGNormalSamplingProbe', 'namespace OpenRPGDeltaFoliageNormalSamplingProbe')
    code = code.replace('Assets/SourceTextures/', 'Assets/DeltaFoliageTextures/')
    check(pins)
    ROOT.mkdir()
    shader.parent.mkdir()
    helper.write_text(code, encoding='utf-8')
    shutil.copyfile(OLD / 'NormalSamplingReference.shader', shader)
    verifier = (OLD / 'verify_reference.py').read_text('utf-8-sig')
    assert verifier.count('==14') == 1 and verifier.count("'verified_textures':14") == 1
    verifier = verifier.replace('==14', '==6').replace("'verified_textures':14", "'verified_textures':6")
    (ROOT / 'verify_reference.py').write_text(verifier, encoding='utf-8')
    for path in [helper, shader, ROOT / 'verify_reference.py']:
        pins[str(path)] = sha(path)
    save('manifest.json', dict(schema_version=1, output_root=str(ROOT / 'export'), cases=cases), new=True)
    pins[str(ROOT / 'manifest.json')] = sha(ROOT / 'manifest.json')
    pid = read(GPU / 'JOB.v1.json')['controller_pid']
    command = f'$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue;if($p){{$p.StartTime.ToUniversalTime().Ticks}};exit 0'
    birth = subprocess.check_output([PS, '-NoProfile', '-Command', command], text=True).strip()
    save('PREPARED.v1.json', dict(cases=6, project=str(project), manifest_sha256=sha(ROOT / 'manifest.json'),
        copies=copies, input_proofs=pins, wait_pid=pid, wait_birth_ticks=birth,
        expected_mips=sum(len(copy['expected_storage_row']['mips']) for copy in copies),
        purchased_scripts_copied_or_executed=False, original_shader_render_parity_qualified=False, full_import=False), new=True)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()), new=True)
    print(json.dumps(dict(controller_pid=child.pid, normal_textures=6, existing_images_reused=True)))


def run():
    state = dict(status='waiting_for_exact_foliage_gpu_controller', native_normal_textures_verified=0,
                 original_shadergraph_render_parity_qualified=False, forge_gpu_sampling_parity_qualified=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        check(prepared['input_proofs'])
        if prepared['wait_birth_ticks']:
            pid, birth = prepared['wait_pid'], prepared['wait_birth_ticks']
            command = f'while($true){{$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {birth}L){{break}};$p.WaitForExit(45000)|Out-Null}};exit 0'
            subprocess.run([PS, '-NoProfile', '-Command', command], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        previous = read(GPU / 'RUN-RESULT.v1.json')
        assert previous.get('finished_utc'), 'GPU controller absent without terminal proof'
        assert previous['execution_exit_code'] == previous['verifier_exit_code'] == 0
        state['previous_gpu_receipt_sha256'] = sha(GPU / 'RUN-RESULT.v1.json')
        count = subprocess.check_output([PS, '-NoProfile', '-Command', '@(Get-CimInstance Win32_Process -Filter "Name=\'Unity.exe\'").Count;exit 0'], text=True).strip()
        assert count == '0', 'Existing Unity process preserved'
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(W / 'unity-package-cache'))
        state['status'] = 'capturing_six_exact_foliage_normal_references'
        save('RUN-RESULT.v1.json', state)
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-force-d3d11', '-quit',
                '-projectPath', prepared['project'], '-executeMethod', 'OpenRPGDeltaFoliageNormalSamplingProbe.NormalSamplingSnapshot.Export',
                '--openrpg-manifest', str(ROOT / 'manifest.json'), '-logFile', str(ROOT / 'unity.log')],
                cwd=ROOT, env=env, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state['unity_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            state['unity_exit_code'] = child.wait()
        assert state['unity_exit_code'] == 0
        check(prepared['input_proofs'])
        with (ROOT / 'reopening.log').open('xb') as log:
            code = subprocess.run([sys.executable, '-B', str(ROOT / 'verify_reference.py'), str(ROOT)],
                cwd=ROOT, env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW).returncode
        state['verifier_exit_code'] = code
        assert code == 0
        report = read(ROOT / 'GPU-REFERENCE-REOPEN.v1.json')
        assert report['verified_textures'] == 6
        assert report['sampled_texel_lod_coordinates'] == prepared['expected_mips'] * 64 * 64
        state.update(status='six_native_foliage_normal_references_reopened', native_normal_textures_verified=6,
            reference_mips_verified=prepared['expected_mips'], native_reference_sha256=sha(ROOT / 'GPU-REFERENCE-REOPEN.v1.json'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
