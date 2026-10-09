"""Resume three cloth materials in the measured HDRP context, retaining v1."""
import os
from pathlib import Path
import re
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common
from verify_native_material_snapshot import reconcile

ROOT = common.W / 'unity-medieval-warm-native-hdrp-v2'
OLD = common.W / 'unity-medieval-cloth-material-source-v1'
WARM = common.W / 'unity-dungeon-native-prepared-pipeline-v6'
SOURCE_MANIFEST = common.W / 'hivemind-run/medieval-kingdom/source.v1.json'
common.ROOT = ROOT


def no_unity():
    assert int(subprocess.check_output([str(common.PS), '-NoProfile', '-Command',
        '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'], text=True).strip()) == 0, 'Unity queue still owned'


def prepare():
    resumed_empty_preparation = ROOT.exists()
    assert not resumed_empty_preparation or (ROOT.is_dir() and not any(ROOT.iterdir())), 'Previous receipts preserved'
    no_unity()
    pirate = common.read(common.W / 'unity-pirate-exact-subgraph-native-v4/INDEPENDENT-REOPEN.v1.json')
    assert pirate['native_materials_verified'] == 12
    common.check(pirate['input_pins'])
    warm = common.read(WARM / 'PREPARED.v6.json')
    project = Path(warm['project'])
    helper = project / 'Assets/Editor/OriginalGraphMaterialSnapshotPreparedHDRPV6.cs'
    assert common.sha(helper) == warm['input_pins'][str(helper)]
    old = common.read(OLD / 'PREPARED.v1.json')
    source_manifest = common.read(SOURCE_MANIFEST)
    assert source_manifest['private_only'] and source_manifest['license'] == 'LicenseRef-Unity-Asset-Store-EULA'
    source_root = Path(source_manifest['source_root'])
    indexed = {str((SOURCE_MANIFEST.parent / row['stored_path']).resolve()): row
        for row in source_manifest['files'] if row['kind'] in ['asset', 'asset.meta']}
    inputs = {}
    copies = []
    for path, expected in old['input_pins'].items():
        source = Path(path)
        if not source.resolve().is_relative_to(source_root.resolve()):
            continue
        assert not source.is_symlink() and source.suffix != '.cs' and not source.name.endswith('.cs.meta')
        assert indexed[str(source.resolve())]['sha256'] == expected
        assert common.sha(source) == expected
        target = project / source.relative_to(source_root)
        assert target.resolve().is_relative_to((project / 'Assets').resolve()) and not target.is_symlink()
        if target.exists():
            assert common.sha(target) == expected, 'Existing private asset differs'
        inputs[str(source)] = expected
        copies.append(dict(source=str(source), target=str(target), sha256=expected, existed=target.exists()))
    assert len(copies) == 26, 'Exact original source coverage changed'
    guid_index = {}
    for meta in (project / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})\s*$', meta.read_text('utf-8-sig'), re.M)
        if match:
            guid_index.setdefault(match[1], []).append(meta)
    for row in copies:
        if not row['source'].endswith('.meta'):
            continue
        guid = re.search(r'^guid: ([0-9a-f]{32})\s*$', Path(row['source']).read_text('utf-8-sig'), re.M)
        assert guid and all(p.resolve() == Path(row['target']).resolve()
            for p in guid_index.get(guid[1], [])), 'GUID collision'
    requests = common.read(OLD / 'manifest.v1.json')['cases']
    assert len(requests) == len({r['material_guid'] for r in requests}) == 3
    ROOT.mkdir(exist_ok=True)
    for row in copies:
        target = Path(row['target'])
        if not row['existed']:
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open('xb') as stream:
                stream.write(Path(row['source']).read_bytes())
        assert common.sha(target) == row['sha256']
    common.save('manifest.v2.json', dict(output_path=str(ROOT / 'NATIVE-RESULT.v2.json'), cases=requests))
    for path in [Path(__file__).resolve(), Path(common.__file__).resolve(),
            Path(sys.modules[reconcile.__module__].__file__).resolve(), SOURCE_MANIFEST,
            helper, ROOT / 'manifest.v2.json', OLD / 'manifest.v1.json']:
        inputs[str(path)] = common.sha(path)
    common.check(inputs)
    common.save('PREPARED.v2.json', dict(project=str(project), input_pins=inputs, copies=copies,
        requested_materials=3, prior_refusal_preserved=str(OLD / 'NATIVE-RESULT.v1.json'),
        resumed_empty_preparation=resumed_empty_preparation,
        unresolved_graph_defaults=old['unresolved_graph_defaults'], source_scripts_executed=False,
        original_graph_semantics_rewritten=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v2.json', dict(controller_pid=child.pid, created_utc=common.now()))
    print(dict(controller_pid=child.pid, requested=3, original_source_files=len(copies)))


def run():
    state = dict(status='prechecking_three_medieval_materials', native_materials_verified=0,
        gpu_render_parity_qualified=False, studio_verified=False, full_import=False)
    common.save('RUN-RESULT.v2.json', state)
    try:
        prep = common.read(ROOT / 'PREPARED.v2.json')
        common.check(prep['input_pins'])
        no_unity()
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(common.OLD / 'upm-cache'))
        args = ['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-force-d3d11', '-quit',
            '-projectPath', prep['project'], '-executeMethod',
            'OpenRPGPreparedHDRPV6.OriginalGraphMaterialSnapshotPreparedHDRPV6.Export',
            '--openrpg-manifest', str(ROOT / 'manifest.v2.json'), '-logFile', str(ROOT / 'unity.log')]
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err,
                stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_three_medieval_materials_in_prepared_warm_hdrp', unity_pid=child.pid)
            common.save('RUN-RESULT.v2.json', state)
            state['actual_unity_exit_code'] = child.wait()
        common.check(prep['input_pins'])
        native = common.read(ROOT / 'NATIVE-RESULT.v2.json')
        state.update(captured=native['captured'], failed=native['failed'],
            native_result_sha256=common.sha(ROOT / 'NATIVE-RESULT.v2.json'))
        assert state['actual_unity_exit_code'] == 0, 'Native errors retained in per-material receipt'
        rows = reconcile(common.read(ROOT / 'manifest.v2.json'), native)
        assert len(rows) == 3
        state.update(status='three_medieval_original_material_passes_captured',
            native_materials_verified=3, original_sources_unchanged=True)
    except Exception as error:
        state.update(status='refused', error=f'{type(error).__name__}: {error}')
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v2.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
