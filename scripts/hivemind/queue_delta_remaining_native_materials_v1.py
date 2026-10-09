"""Capture RocksTemperate and FirePit source instances; preserve all prior captures."""
from pathlib import Path
import copy
import json
import os
import re
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common
from verify_native_material_snapshot import reconcile
from verify_native_urp_material_snapshot import reconcile as independent_reconcile

ROOT = common.W / 'unity-delta-remaining-native-materials-v1'
SOURCE = common.W / 'hivemind-run/native-american-village/source'
WARM = common.W / 'unity-medieval-warm-native-hdrp-v2'
WANTED = {'abc00000000009294800760372203426', 'abc00000000014678987794877247210'}
common.ROOT = ROOT


def unity_idle():
    value = subprocess.check_output([str(common.PS), '-NoProfile', '-Command',
        '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'], text=True,
        creationflags=subprocess.CREATE_NO_WINDOW).strip()
    assert value == '0', 'Preserve existing Unity processes'


def prepare():
    assert not ROOT.exists()
    unity_idle()
    source_path = common.W / 'nested-prefab-delta-material-source-ir-v1/RESULT.v1.json'
    binding_path = common.W / 'nested-prefab-delta-graph-property-bindings-v1/RESULT.v1.json'
    kernel_path = common.W / 'nested-prefab-delta-material-wgsl-v1/RESULT.v1.json'
    topology_path = common.W / 'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json'
    sources = {r['material_identity']['guid']: r for r in common.read(source_path)['rows']}
    bindings = {r['material_identity']['guid']: r for r in common.read(binding_path)['rows']}
    kernels = {r['material_guid']: r for r in common.read(kernel_path)['rows']}
    topology = {r['material_guid']: r for r in common.read(topology_path)['rows']}
    warm = common.read(WARM / 'PREPARED.v2.json')
    assert common.read(WARM / 'RUN-RESULT.v2.json')['native_materials_verified'] == 3
    project = Path(warm['project'])
    helper = project / 'Assets/Editor/OriginalGraphMaterialSnapshotPreparedHDRPV6.cs'
    assert common.sha(helper) == warm['input_pins'][str(helper)]
    pins = {str(p): common.sha(p) for p in [Path(__file__).resolve(), source_path, binding_path,
        kernel_path, topology_path, WARM / 'PREPARED.v2.json', WARM / 'RUN-RESULT.v2.json', helper,
        Path(common.__file__), Path(sys.modules[reconcile.__module__].__file__),
        Path(sys.modules[independent_reconcile.__module__].__file__)]}
    for relative in ['ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/QualitySettings.asset',
                     'ProjectSettings/ProjectSettings.asset', 'Packages/manifest.json', 'Packages/packages-lock.json']:
        path = project / relative
        pins[str(path)] = common.sha(path)
    requests, requested_files = [], {}
    for guid in sorted(WANTED):
        source, binding, kernel, ir_row = sources[guid], bindings[guid], kernels[guid], topology[guid]
        assert kernel['actual_naga_exit_code'] == 0
        ir_path = Path(ir_row['output'])
        assert common.sha(ir_path) == ir_row['output_sha256'] == kernel['active_ir_sha256']
        pins[str(ir_path)] = ir_row['output_sha256']
        properties = {}
        for node in common.read(ir_path)['nodes']:
            if node['opcode'] != 'SampleTexture2DNode':
                continue
            value = next(v['value'] for v in node['inputs'] if v['name'] == 'Texture')
            assert value['provenance'].startswith('material:') and value['literal'].get('guid')
            name = value['provenance'].split(':', 1)[1]
            texture_guid = value['literal']['guid']
            assert name not in properties or properties[name] == texture_guid
            properties[name] = texture_guid
        assert set(properties.values()) == {r['guid'] for r in kernel['source_texture_bindings']}
        active_names = {r['reference_name'] for r in binding['bindings']
                        if r['source_property_object']['m_Type'].endswith('.Texture2DShaderProperty')}
        assert set(properties).issubset(active_names)
        envs = {}
        material = source['source_material_properties']
        for entry in material['m_SavedProperties']['m_TexEnvs']:
            for name, value in entry.items():
                assert name not in envs
                envs[name] = value
        textures = []
        for name, texture_guid in sorted(properties.items()):
            ref = envs[name]['m_Texture']
            assert ref['guid'] == texture_guid
            textures.append(dict(property=name, guid=texture_guid, file_id=str(ref['fileID'])))
        requests.append(dict(material_guid=guid, asset_path=source['material_identity']['asset_path'],
            expected_shader_guid=binding['graph']['source_shader_guid'],
            expected_shader_file_id=str(material['m_Shader']['fileID']), required_textures=textures))
        for entry in [source['source_material'], binding['graph']]:
            path = Path(entry['path'])
            assert common.sha(path) == entry['sha256']
            requested_files[str(path)] = entry['sha256']
            requested_files[str(path) + '.meta'] = common.sha(str(path) + '.meta')
        for texture in kernel['source_texture_bindings']:
            for entry in texture['source_files']:
                requested_files[entry['path']] = entry['sha256']
    assert len(requests) == 2 and sum(len(r['required_textures']) for r in requests) == 10
    index = {}
    for meta in (project / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})\s*$', meta.read_text('utf-8-sig'), re.M)
        if match:
            index.setdefault(match[1], []).append(meta.resolve())
    copies = []
    for original, digest in sorted(requested_files.items()):
        path = Path(original)
        assert path.resolve().is_relative_to(SOURCE.resolve()) and not path.is_symlink()
        assert path.suffix.lower() in ['.mat','.shadergraph','.png','.tga','.jpg','.jpeg','.dds','.meta']
        assert common.sha(path) == digest
        target = project / path.relative_to(SOURCE)
        assert target.resolve().is_relative_to((project / 'Assets').resolve()) and not target.is_symlink()
        if target.exists():
            assert common.sha(target) == digest, 'Existing private file differs: ' + str(target)
        if path.suffix == '.meta':
            match = re.search(r'^guid: ([0-9a-f]{32})\s*$', path.read_text('utf-8-sig'), re.M)
            assert match and all(p == target.resolve() for p in index.get(match[1], [])), 'GUID collision'
        copies.append(dict(original=original, target=str(target), sha256=digest, existed=target.exists()))
        pins[original] = digest
    common.check(pins)
    ROOT.mkdir()
    for row in copies:
        target = Path(row['target'])
        if not row['existed']:
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open('xb') as stream:
                stream.write(Path(row['original']).read_bytes())
        assert common.sha(target) == row['sha256']
        pins[str(target)] = row['sha256']
    common.save('manifest.v1.json', dict(output_path=str(ROOT / 'NATIVE-RESULT.v1.json'), cases=requests))
    pins[str(ROOT / 'manifest.v1.json')] = common.sha(ROOT / 'manifest.v1.json')
    common.save('PREPARED.v1.json', dict(project=str(project), input_pins=pins, copies=copies,
         requested_materials=2, requested_texture_bindings=10, source_scripts_executed=False,
         trusted_native_helper=str(helper), full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v1.json', dict(controller_pid=child.pid, created_utc=common.now()))
    print(json.dumps(dict(controller_pid=child.pid, original_materials=2, active_bindings=10)))


def run():
    state = dict(status='capturing_two_remaining_delta_native_materials', native_materials_verified=0,
        original_render_state_parity_qualified=False, gpu_render_parity_qualified=False, studio_verified=False, full_import=False)
    common.save('RUN-RESULT.v1.json', state)
    try:
        prepared = common.read(ROOT / 'PREPARED.v1.json')
        common.check(prepared['input_pins'])
        unity_idle()
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(common.OLD / 'upm-cache'))
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-force-d3d11', '-quit',
                '-projectPath', prepared['project'], '-executeMethod',
                'OpenRPGPreparedHDRPV6.OriginalGraphMaterialSnapshotPreparedHDRPV6.Export',
                '--openrpg-manifest', str(ROOT / 'manifest.v1.json'), '-logFile', str(ROOT / 'unity.log')],
                cwd=ROOT, env=env, stdin=subprocess.DEVNULL, stdout=out, stderr=err,
                creationflags=subprocess.CREATE_NO_WINDOW)
            state['unity_pid'] = child.pid
            common.save('RUN-RESULT.v1.json', state)
            state['actual_unity_exit_code'] = child.wait()
        assert state['actual_unity_exit_code'] == 0, 'Native capture refused; preserve exact logs'
        manifest, captured = common.read(ROOT / 'manifest.v1.json'), common.read(ROOT / 'NATIVE-RESULT.v1.json')
        assert len(reconcile(manifest, captured)) == len(independent_reconcile(manifest, captured)) == 2
        refused = []
        for name in ['wrong_shader','uncompiled_pass','missing_material','wrong_texture']:
            bad = copy.deepcopy(captured)
            if name == 'wrong_shader': bad['cases'][0]['shader_guid'] = '0'*32
            elif name == 'uncompiled_pass': bad['cases'][0]['passes'][0]['compiled'] = False
            elif name == 'missing_material': bad['cases'].pop()
            else:
                prop_name = manifest['cases'][0]['required_textures'][0]['property']
                next(r for r in bad['cases'][0]['properties'] if r['name'] == prop_name)['texture_guid'] = '0'*32
            try: independent_reconcile(manifest, bad)
            except ValueError as error: refused.append(dict(test=name, reason=str(error)))
            else: raise AssertionError('Altered native capture accepted: ' + name)
        common.check(prepared['input_pins'])
        common.save('INDEPENDENT-REOPEN.v1.json', dict(native_materials_verified=2, active_texture_bindings_verified=10,
             native_result_sha256=common.sha(ROOT / 'NATIVE-RESULT.v1.json'), refused_mutations=refused,
             original_and_private_source_pins_unchanged=True, original_render_state_comparison_pending=True,
             gpu_render_parity_qualified=False, full_import=False))
        state.update(status='two_remaining_delta_native_materials_and_ten_bindings_reopened',
            native_materials_verified=2, active_texture_bindings_verified=10,
            native_result_sha256=common.sha(ROOT / 'NATIVE-RESULT.v1.json'),
            independent_receipt_sha256=common.sha(ROOT / 'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error))
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
