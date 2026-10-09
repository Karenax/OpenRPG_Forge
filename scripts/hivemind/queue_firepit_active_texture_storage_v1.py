"""Capture the one real active RA texture; preserve source and private metadata."""
from pathlib import Path
import ast
import datetime
import importlib.util
import os
import re
import runpy
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'unity-delta-firepit-active-texture-storage-v1'
PROJECT = W / 'unity-villa-texture-source-probe-linear-v2/project'
BASE = HERE / 'queue_delta_firepit_effect_materials_v1.py'
spec = importlib.util.spec_from_file_location('texture_job', BASE)
job = importlib.util.module_from_spec(spec)
spec.loader.exec_module(job)
job.ROOT = ROOT

def prepare():
    assert not ROOT.exists()
    job.idle()
    bindings_root = W / 'delta-firepit-legacy-material-bindings-v1'
    bindings = job.read(bindings_root / 'RESULT.v1.json')
    job.check(bindings['input_pins'])
    job.check(bindings['output_pins'])
    materials = [job.read(Path(r['binding'])) for r in bindings['material_bindings']]
    assert len(materials) == 2
    dependencies = [m['source_texture'] for m in materials]
    assert dependencies[0] == dependencies[1], 'Shared active texture reference differs'
    dependency = dependencies[0]
    image = Path(dependency['source'])
    meta = Path(str(image) + '.meta')
    guid = dependency['source_reference']['guid']
    assert guid == '63f624c8bd42f35409dde06b85d2d900'
    assert job.sha(image) == dependency['source_sha256']
    assert job.sha(meta) == materials[0]['source_texture_meta_sha256']
    for existing in (PROJECT / 'Assets').rglob('*.meta'):
        assert not re.search(r'^guid:\s*' + guid + r'\s*$', existing.read_text('utf-8-sig'), re.M), 'Preserve existing GUID; check prior capture'
    previous = W / 'unity-delta-foliage-texture-storage-v2/PREPARED.v1.json'
    previous_data = job.read(previous)
    helper = Path(previous_data['helper'])
    verifier = PROJECT.parent / 'verify_texture_export.py'
    assert job.sha(helper) == previous_data['exporter_sha256']
    assert job.sha(verifier) == previous_data['verifier_sha256']
    pins = dict(bindings['input_pins'])
    pins.update(bindings['output_pins'])
    for p in (PROJECT / 'Assets').rglob('*'):
        if p.is_file():
            pins[str(p)] = job.sha(p)
    for relative in ['ProjectSettings/ProjectSettings.asset', 'ProjectSettings/GraphicsSettings.asset',
                     'ProjectSettings/QualitySettings.asset', 'Packages/manifest.json', 'Packages/packages-lock.json']:
        p = PROJECT / relative
        if p.exists():
            pins[str(p)] = job.sha(p)
        else:
            assert relative == 'Packages/packages-lock.json'
    for p in (Path(__file__).resolve(), BASE, HERE / 'queue_delta_firepit_particle_behavior_v4.py',
              previous, helper, verifier, bindings_root / 'RESULT.v1.json', image, meta):
        pins[str(p)] = job.sha(p)
    job.check(pins)
    ROOT.mkdir()
    (ROOT / 'temp').mkdir()
    relative = Path('Assets/DeltaFirePitLegacyTexture') / guid / image.name
    target = PROJECT / relative
    target.parent.mkdir(parents=True)
    assert not target.exists() and not Path(str(target) + '.meta').exists()
    shutil.copyfile(image, target)
    target_meta = Path(str(target) + '.meta')
    shutil.copyfile(meta, target_meta)
    shutil.copyfile(meta, ROOT / 'PRIVATE-META-BEFORE.meta')
    assert job.sha(target) == job.sha(image) and job.sha(target_meta) == job.sha(meta)
    pins[str(target)] = job.sha(target)
    # This new owned copy is the only importer metadata allowed to serialize
    # when the proven helper temporarily enables readability. Retain both bytes.
    pins[str(ROOT / 'PRIVATE-META-BEFORE.meta')] = job.sha(meta)
    request = dict(asset_id='hivemind.native-american-village.texture-' + guid,
        asset_path=relative.as_posix(), expected_guid=guid, expected_file_id='2800000', expected_name=image.stem)
    job.save('manifest.json', dict(schema_version=1, output_root=str(ROOT / 'export'), cases=[request]))
    pins[str(ROOT / 'manifest.json')] = job.sha(ROOT / 'manifest.json')
    job.save('PREPARED.v1.json', dict(input_pins=pins, helper=str(helper), verifier=str(verifier),
        owned_private_meta=str(target_meta), owned_private_meta_before_sha256=job.sha(meta),
        source_original_png=str(image), source_original_meta=str(meta), material_instances=2,
        expected_texture_cases=1, source_texture_importer_parity_qualified=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    job.save('JOB.v1.json', dict(controller_pid=child.pid))
    print(__import__('json').dumps(dict(controller_pid=child.pid, active_texture_cases=1, material_instances=2, full_import=False)))

def run():
    state = dict(status='preparing_original_active_ra_texture_capture', textures_verified=0,
        gpu_sampling_qualified=False, texture_importer_parity_qualified=False, studio_verified=False, full_import=False)
    job.save('RUN-RESULT.v1.json', state)
    broker = None
    namespace = None
    try:
        prepared = job.read(ROOT / 'PREPARED.v1.json')
        job.check(prepared['input_pins'])
        assert job.sha(Path(prepared['owned_private_meta'])) == prepared['owned_private_meta_before_sha256']
        job.idle()
        broker_helper = HERE / 'queue_delta_firepit_particle_behavior_v4.py'
        tree = ast.parse(broker_helper.read_text('utf-8-sig'))
        selected = [n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom)) or
            isinstance(n, ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        namespace = dict(ROOT=ROOT, W=W, UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'), LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected, type_ignores=[]), str(broker_helper), 'exec'), namespace)
        broker, label = namespace['start_broker'](state)
        env = os.environ.copy()
        env.update(TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'), UPM_CACHE_ROOT=str(W / 'unity-package-cache'))
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(job.UNITY), '-batchmode', '-force-d3d11', '-quit',
                '-upmIpcPath', label, '-projectPath', str(PROJECT), '-executeMethod',
                'OpenRPGDeltaFoliageTexturesV1.TextureSourceSnapshot.Export', '--openrpg-manifest',
                str(ROOT / 'manifest.json'), '-logFile', str(ROOT / 'unity.log')], cwd=ROOT,
                env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_one_active_ra_texture_with_original_importer', unity_pid=child.pid)
            job.save('RUN-RESULT.v1.json', state)
            state['unity_exit_code'] = child.wait()
        assert state['unity_exit_code'] == 0, 'Native active texture capture refused'
        job.check(prepared['input_pins'])
        native = job.read(ROOT / 'export/RESULT.json')
        assert native['unity_version'] == '6000.5.8f1' and native['color_space'] == 'Linear'
        assert native['graphics_device_type'] == 'Direct3D11'
        assert native['total'] == native['matched'] == len(native['cases']) == 1 and native['failed'] == 0
        verifier = runpy.run_path(prepared['verifier'], run_name='trusted_texture_verifier')
        request = job.read(ROOT / 'manifest.json')['cases'][0]
        row = verifier['check_case'](request, native['cases'][0], ROOT / 'export/000')
        after = ROOT / 'PRIVATE-META-AFTER.meta'
        shutil.copyfile(prepared['owned_private_meta'], after)
        changed = job.sha(after) != prepared['owned_private_meta_before_sha256']
        output_pins = {str(p): job.sha(p) for p in (ROOT / 'export').rglob('*') if p.is_file()}
        output_pins[str(after)] = job.sha(after)
        job.check(prepared['input_pins'])
        job.save('INDEPENDENT-REOPEN.v1.json', dict(input_pins=prepared['input_pins'], output_pins=output_pins,
            textures_verified=1, row=row, source_originals_unchanged=True,
            changed_owned_private_importer_meta=changed, private_meta_before_sha256=prepared['owned_private_meta_before_sha256'],
            private_meta_after_sha256=job.sha(after), original_project_importer_parity_qualified=False,
            texture_orientation_qualified=False, gpu_sampling_qualified=False, full_import=False))
        state.update(status='one_active_ra_texture_and_all_native_mips_reopened', textures_verified=1,
            mips_verified=row['mip_count'], graphics_format=row['graphics_format'], texture_format=row['texture_format'],
            source_originals_unchanged=True, changed_owned_private_importer_meta=changed,
            independent_receipt_sha256=job.sha(ROOT / 'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified', error=type(error).__name__ + ': ' + str(error),
            diagnostic_traceback=traceback.format_exc())
    finally:
        if broker is not None:
            namespace['stop_broker'](broker, state)
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    job.save('RUN-RESULT.v1.json', state)

if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
