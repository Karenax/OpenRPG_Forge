"""Restore exact authored subgraph dependencies; retry only twelve refused cases.

The previous thirteen-case result is retained. Sources and graph semantics are
never rewritten, and the one successful Native American capture is not rerun.
"""
import json
import os
from pathlib import Path
import re
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common
from verify_native_material_snapshot import reconcile

ROOT = common.W / 'unity-pirate-exact-subgraph-native-v4'
OLD = common.W / 'unity-delta-warm-prepared-native-v3'
MANIFEST = common.W / 'hivemind-run/pirate-island/source.v1.json'
common.ROOT = ROOT


def no_unity():
    count = int(subprocess.check_output([str(common.PS), '-NoProfile', '-Command',
        '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'], text=True).strip())
    assert count == 0, 'A native Unity process owns the queue'


def graph_objects(path):
    text = path.read_text('utf-8-sig')
    decoder, offset, result = json.JSONDecoder(), 0, []
    while offset < len(text):
        while offset < len(text) and text[offset].isspace():
            offset += 1
        if offset == len(text):
            break
        item, offset = decoder.raw_decode(text, offset)
        assert isinstance(item, dict), 'Expected a graph object document'
        result.append(item)
    assert result, 'Empty graph'
    return result


def prepare():
    assert not ROOT.exists(), 'Previous receipts must be retained'
    no_unity()
    prep = common.read(OLD / 'PREPARED.v3.json')
    common.check(prep['input_pins'])
    native = common.read(OLD / 'NATIVE-RESULT.v3.json')
    requests = common.read(OLD / 'manifest.v3.json')['cases']
    assert (native['requested'], native['captured'], native['failed']) == (13, 1, 12)
    assert len(native['cases']) == len(requests) == 13
    refused, successes = [], []
    for request, row in zip(requests, native['cases']):
        assert row['material_guid'] == request['material_guid']
        if row['error']:
            assert request['expected_shader_guid'] == '1caa1cbf1210e6f46b59e4604b348b46'
            refused.append(request)
        else:
            successes.append((request, row))
    assert len(refused) == 12 and len(successes) == 1
    subset = dict(native, requested=1, captured=1, failed=0, cases=[successes[0][1]])
    verified_one = reconcile({'cases': [successes[0][0]]}, subset)
    manifest = common.read(MANIFEST)
    assert manifest['private_only'] and manifest['license'] == 'LicenseRef-Unity-Asset-Store-EULA'
    project = Path(prep['project'])
    source_root = Path(manifest['source_root'])
    indexed = {(row['guid'], row['kind']): row for row in manifest['files']}
    graph_paths, dependencies, queue = [], [], ['1caa1cbf1210e6f46b59e4604b348b46']
    visited = set()
    inputs = dict(prep['input_pins'])
    inputs[str(MANIFEST)] = common.sha(MANIFEST)
    while queue:
        guid = queue.pop(0)
        if guid in visited:
            continue
        visited.add(guid)
        relative = Path(manifest['guid_paths'][guid])
        assert relative.suffix in ['.shadergraph', '.shadersubgraph']
        source = source_root / relative
        assert source.resolve().is_relative_to(source_root.resolve()) and not source.is_symlink()
        objects = graph_objects(source)
        graph_paths.append(dict(guid=guid, path=str(relative), objects=len(objects)))
        for obj in objects:
            if obj.get('m_Type') != 'UnityEditor.ShaderGraph.SubGraphNode':
                continue
            reference = json.loads(obj['m_SerializedSubGraph'])['subGraph']
            dependency = reference['guid']
            assert dependency in manifest['guid_paths'], 'Authored subgraph source absent'
            queue.append(dependency)
            dependencies.append(dict(parent_guid=guid, subgraph_guid=dependency,
                file_id=str(reference['fileID']), node_id=obj['m_ObjectId']))
        for original, kind in [(source, 'asset'), (Path(str(source) + '.meta'), 'asset.meta')]:
            entry = indexed[(guid, kind)]
            expected = entry['sha256']
            assert common.sha(original) == expected, 'Original graph bytes differ from source inventory'
            inputs[str(original)] = expected
    guid_index = {}
    for meta in (project / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})\s*$', meta.read_text('utf-8-sig'), re.M)
        if match:
            guid_index.setdefault(match[1], []).append(meta)
    copies = []
    for row in graph_paths:
        source = source_root / row['path']
        target = project / row['path']
        assert target.resolve().is_relative_to((project / 'Assets').resolve())
        assert all(meta.resolve() == Path(str(target) + '.meta').resolve()
            for meta in guid_index.get(row['guid'], [])), 'Private GUID collision'
        for original, destination in [(source, target),
                (Path(str(source) + '.meta'), Path(str(target) + '.meta'))]:
            expected = inputs[str(original)]
            assert not destination.is_symlink()
            if destination.exists():
                assert common.sha(destination) == expected, 'Existing private graph changed'
            copies.append(dict(source=str(original), target=str(destination),
                sha256=expected, existed=destination.exists()))
    missing = [row for row in copies if not row['existed']]
    assert missing, 'Missing exact subgraph dependency not confirmed'
    log = OLD / 'unity.log'
    assert 'node.asset.keywords' in (project / 'Library/PackageCache/com.unity.shadergraph@8d7376ec43ee/Editor/Data/Graphs/GraphData.cs').read_text()
    assert 'GraphData.CollectShaderKeywords' in log.read_text('utf-8-sig')
    ROOT.mkdir()
    for row in missing:
        target = Path(row['target'])
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open('xb') as stream:
            stream.write(Path(row['source']).read_bytes())
        assert common.sha(target) == row['sha256']
    common.save('manifest.v4.json', dict(output_path=str(ROOT / 'NATIVE-RESULT.v4.json'), cases=refused))
    for path in [Path(__file__).resolve(), Path(common.__file__).resolve(),
            Path(sys.modules[reconcile.__module__].__file__).resolve(),
            OLD / 'NATIVE-RESULT.v3.json', OLD / 'manifest.v3.json', log,
            ROOT / 'manifest.v4.json']:
        inputs[str(path)] = common.sha(path)
    common.check(inputs)
    common.save('DELTA-REFUSAL-ANALYSIS.v1.json', dict(
        checked_utc=common.now(), original_requested=13, captured=1, refused=12,
        independently_reopened_successful_subset=verified_one, subset_scope='one_of_thirteen',
        root_cause='Authored SubGraphNode references absent private subgraph; native keyword collection dereferences node.asset.keywords',
        graph_dependencies=dependencies, copied_exact_source_dependencies=missing,
        previous_native_receipt=str(OLD / 'NATIVE-RESULT.v3.json'), input_pins=inputs,
        original_sources_unchanged=True, original_graph_semantics_rewritten=False,
        fallback_used=False, gpu_render_parity_qualified=False, full_import=False))
    common.save('PREPARED.v4.json', dict(project=str(project), input_pins=inputs,
        copies=copies, requested_materials=12, retained_successes_not_rerun=1,
        previous_native_receipt=str(OLD / 'NATIVE-RESULT.v3.json'),
        original_graph_semantics_rewritten=False, source_scripts_executed=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log_stream:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdout=log_stream, stderr=log_stream, stdin=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v4.json', dict(controller_pid=child.pid, created_utc=common.now()))
    print(dict(controller_pid=child.pid, requested=12, exact_dependencies_added=len(missing)))


def run():
    state = dict(status='prechecking_exact_pirate_subgraph_dependencies', captured=0,
        failed=12, native_materials_verified=0, independent_reopen_verified=False,
        gpu_render_parity_qualified=False, studio_verified=False, full_import=False)
    common.save('RUN-RESULT.v4.json', state)
    try:
        prep = common.read(ROOT / 'PREPARED.v4.json')
        common.check(prep['input_pins'])
        no_unity()
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(common.OLD / 'upm-cache'))
        args = ['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-force-d3d11', '-quit',
            '-projectPath', prep['project'], '-executeMethod',
            'OpenRPGPreparedHDRPV6.OriginalGraphMaterialSnapshotPreparedHDRPV6.Export',
            '--openrpg-manifest', str(ROOT / 'manifest.v4.json'), '-logFile', str(ROOT / 'unity.log')]
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err,
                stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_twelve_pirate_materials_with_exact_subgraph', unity_pid=child.pid)
            common.save('RUN-RESULT.v4.json', state)
            state['actual_unity_exit_code'] = child.wait()
        common.check(prep['input_pins'])
        native = common.read(ROOT / 'NATIVE-RESULT.v4.json')
        state.update(captured=native['captured'], failed=native['failed'],
            native_result_sha256=common.sha(ROOT / 'NATIVE-RESULT.v4.json'))
        assert state['actual_unity_exit_code'] == 0, 'Unity refused: retained per-material errors in native receipt'
        rows = reconcile(common.read(ROOT / 'manifest.v4.json'), native)
        assert len(rows) == 12
        state.update(status='twelve_pirate_original_material_passes_captured',
            native_materials_verified=12, original_sources_unchanged=True)
    except Exception as error:
        state.update(status='refused', error=f'{type(error).__name__}: {error}')
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v4.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
