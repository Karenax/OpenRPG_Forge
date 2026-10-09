"""Capture the six new foliage instances using the proven, retained warm HDRP project."""
from pathlib import Path
import json
import os
import re
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common
from verify_native_material_snapshot import reconcile

ROOT = common.W / 'unity-delta-foliage-native-materials-v1'
SOURCE = common.W / 'hivemind-run/native-american-village/source'
PRIOR = common.W / 'unity-delta-foliage-texture-storage-v2'
WARM = common.W / 'unity-medieval-warm-native-hdrp-v2'
common.ROOT = ROOT


def birth(pid):
    code = f'$p=Get-Process -Id {int(pid)} -ErrorAction SilentlyContinue; if($p){{$p.StartTime.ToUniversalTime().Ticks}}; exit 0'
    return subprocess.check_output([str(common.PS), '-NoProfile', '-Command', code], text=True,
        creationflags=subprocess.CREATE_NO_WINDOW).strip()


def prepare():
    assert not ROOT.exists()
    contracts_path = common.W / 'delta-foliage-render-contracts-v1/RESULT.v1.json'
    sources_path = common.W / 'nested-prefab-delta-material-source-ir-v1/RESULT.v1.json'
    kernels_path = common.W / 'delta-vector-node-wgsl-v3/RESULT.v1.json'
    contracts, sources, kernels = [common.read(path) for path in [contracts_path, sources_path, kernels_path]]
    assert contracts['contracts_reopened'] == len(contracts['rows']) == len(kernels['rows']) == 6
    source_by_id = {row['material_identity']['guid']: row for row in sources['rows']}
    kernel_by_id = {row['material_guid']: row for row in kernels['rows']}
    warm, warm_prepared = common.read(WARM / 'RUN-RESULT.v2.json'), common.read(WARM / 'PREPARED.v2.json')
    assert warm['native_materials_verified'] == 3
    project = Path(warm_prepared['project'])
    helper = project / 'Assets/Editor/OriginalGraphMaterialSnapshotPreparedHDRPV6.cs'
    assert common.sha(helper) == warm_prepared['input_pins'][str(helper)]
    current_unity = subprocess.check_output([str(common.PS), '-NoProfile', '-Command',
        'Get-CimInstance Win32_Process -Filter "Name=\'Unity.exe\'" | ForEach-Object CommandLine; exit 0'], text=True,
        creationflags=subprocess.CREATE_NO_WINDOW)
    assert str(project).lower().replace('\\', '/') not in current_unity.lower().replace('\\', '/'), 'Warm HDRP project already in use'
    previous_pid = common.read(PRIOR / 'JOB.v1.json')['controller_pid']
    previous_birth = birth(previous_pid)
    assert previous_birth or common.read(PRIOR / 'RUN-RESULT.v1.json').get('finished_utc')
    pins = {str(path): common.sha(path) for path in [contracts_path, sources_path, kernels_path,
        WARM / 'RUN-RESULT.v2.json', WARM / 'PREPARED.v2.json', helper,
        Path(__file__).resolve(), Path(common.__file__).resolve(), Path(sys.modules[reconcile.__module__].__file__).resolve()]}
    for path, expected in contracts['input_pins'].items():
        assert common.sha(path) == expected
        pins[path] = expected
    for relative in ['ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/QualitySettings.asset',
                     'ProjectSettings/ProjectSettings.asset', 'Packages/manifest.json', 'Packages/packages-lock.json']:
        path = project / relative
        pins[str(path)] = common.sha(path)
    requested_sources, requests = {}, []
    for contract in contracts['rows']:
        guid = contract['material_guid']
        source = source_by_id[guid]
        material = source['source_material_properties']
        textures = []
        required_guids = {row['guid'] for row in kernel_by_id[guid]['source_texture_bindings']}
        assert len(required_guids) == 3
        for entry in material['m_SavedProperties']['m_TexEnvs']:
            for name, value in entry.items():
                reference = value['m_Texture']
                if reference.get('guid') in required_guids:
                    textures.append(dict(property=name, guid=reference['guid'], file_id=str(reference['fileID'])))
        assert len(textures) == 3 and {row['guid'] for row in textures} == required_guids
        requests.append(dict(material_guid=guid, asset_path=source['material_identity']['asset_path'],
            expected_shader_guid=contract['shader_guid'], expected_shader_file_id=str(material['m_Shader']['fileID']),
            required_textures=textures))
        for key in ['original_material', 'original_graph']:
            path = Path(contract[key])
            requested_sources[str(path)] = contract[key + '_sha256']
            meta = Path(str(path) + '.meta')
            requested_sources[str(meta)] = common.sha(meta)
        for texture in kernel_by_id[guid]['source_texture_bindings']:
            for item in texture['source_files']:
                requested_sources[item['path']] = item['sha256']
    # Check every proposed copy and GUID before writing any private project file.
    index = {}
    for meta in (project / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})\s*$', meta.read_text('utf-8-sig'), re.M)
        if match:
            index.setdefault(match[1], []).append(meta.resolve())
    copies = []
    for name, expected in sorted(requested_sources.items()):
        path = Path(name)
        assert path.resolve().is_relative_to(SOURCE.resolve()) and not path.is_symlink()
        assert path.suffix.lower() in ['.mat', '.shadergraph', '.png', '.tga', '.jpg', '.jpeg', '.dds', '.meta']
        assert common.sha(path) == expected
        target = project / path.relative_to(SOURCE)
        assert target.resolve().is_relative_to((project / 'Assets').resolve()) and not target.is_symlink()
        if target.exists():
            assert common.sha(target) == expected, 'Existing private copy differs: ' + str(target)
        if path.suffix.lower() == '.meta':
            match = re.search(r'^guid: ([0-9a-f]{32})\s*$', path.read_text('utf-8-sig'), re.M)
            assert match and all(other == target.resolve() for other in index.get(match[1], [])), 'GUID collision'
        copies.append(dict(original=str(path), target=str(target), sha256=expected, existed=target.exists()))
        pins[str(path)] = expected
    ROOT.mkdir()
    for row in copies:
        path = Path(row['target'])
        if not row['existed']:
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open('xb') as stream:
                stream.write(Path(row['original']).read_bytes())
        assert common.sha(path) == row['sha256']
    common.save('manifest.v1.json', dict(output_path=str(ROOT / 'NATIVE-RESULT.v1.json'), cases=requests))
    pins[str(ROOT / 'manifest.v1.json')] = common.sha(ROOT / 'manifest.v1.json')
    common.check(pins)
    common.save('PREPARED.v1.json', dict(project=str(project), input_pins=pins, copies=copies, requested_materials=6,
        requested_texture_bindings=18, wait_pid=previous_pid, wait_birth_ticks=previous_birth,
        previous_controller_receipt=str(PRIOR / 'RUN-RESULT.v1.json'), trusted_native_helper=str(helper),
        source_scripts_executed=False, sources_and_existing_captures_preserved=True, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=common.now()))
    print(json.dumps(dict(controller_pid=child.pid, materials=6, texture_bindings=18, private_files=len(copies))))


def run():
    state = dict(status='waiting_for_exact_foliage_texture_controller', native_materials_verified=0,
        gpu_render_parity_qualified=False, studio_verified=False, full_import=False)
    common.save('RUN-RESULT.v1.json', state)
    try:
        prepared = common.read(ROOT / 'PREPARED.v1.json')
        if prepared['wait_birth_ticks']:
            script = (f'while($true){{$p=Get-Process -Id {prepared["wait_pid"]} -ErrorAction SilentlyContinue;'
                f'if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {prepared["wait_birth_ticks"]}){{break}};'
                'Start-Sleep -Seconds 10}; exit 0')
            subprocess.run([str(common.PS), '-NoProfile', '-Command', script], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        previous = common.read(prepared['previous_controller_receipt'])
        assert previous.get('finished_utc'), 'Previous capture controller has no terminal receipt'
        state['previous_texture_controller_status'] = previous['status']
        state['previous_texture_controller_receipt_sha256'] = common.sha(prepared['previous_controller_receipt'])
        common.check(prepared['input_pins'])
        count = subprocess.check_output([str(common.PS), '-NoProfile', '-Command',
            '@(Get-CimInstance Win32_Process -Filter "Name=\'Unity.exe\'").Count; exit 0'], text=True).strip()
        assert count == '0', 'Existing Unity process preserved; material capture not launched'
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(common.OLD / 'upm-cache'))
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-force-d3d11', '-quit',
                '-projectPath', prepared['project'], '-executeMethod', 'OpenRPGPreparedHDRPV6.OriginalGraphMaterialSnapshotPreparedHDRPV6.Export',
                '--openrpg-manifest', str(ROOT / 'manifest.v1.json'), '-logFile', str(ROOT / 'unity.log')], cwd=ROOT,
                env=env, stdin=subprocess.DEVNULL, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_six_original_foliage_material_instances', unity_pid=child.pid)
            common.save('RUN-RESULT.v1.json', state)
            state['actual_unity_exit_code'] = child.wait()
        assert state['actual_unity_exit_code'] == 0, 'Native capture refused; exact logs preserved'
        common.check(prepared['input_pins'])
        native = common.read(ROOT / 'NATIVE-RESULT.v1.json')
        rows = reconcile(common.read(ROOT / 'manifest.v1.json'), native)
        state.update(status='six_original_foliage_materials_and_eighteen_bindings_captured', native_materials_verified=len(rows),
            native_result_sha256=common.sha(ROOT / 'NATIVE-RESULT.v1.json'), independent_reopen_required=True,
            original_sources_unchanged=True, original_render_state_comparison_pending=True)
    except Exception as error:
        state.update(status='refused_or_unverified', error=str(error))
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
