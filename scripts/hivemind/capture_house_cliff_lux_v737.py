"""Capture only the two missing Cliff cards with explicit presentation lighting."""
from pathlib import Path
import importlib.util, json, shutil, subprocess, sys

BASE = Path(__file__).with_name('queue_house_thumbnail_batch_v1.py')
spec = importlib.util.spec_from_file_location('house_batch', BASE)
batch = importlib.util.module_from_spec(spec)
spec.loader.exec_module(batch)
OLD = batch.ROOT
ROOT = batch.W / 'house-cliff-lux-capture-v737'
CLASS = 'OpenRPGHouseCliffLuxV737'
IDS = {
    'hivemind.house-on-a-hill.prefab-sm-cliff-02-abc00000000011697853940912642466',
    'hivemind.house-on-a-hill.prefab-sm-cliff-03-abc00000000000394178604798078187',
}

def prepare():
    batch.no_unity()
    assert batch.UNITY.is_file() and not ROOT.exists()
    registry = batch.read(batch.V / 'metadata/asset-source-support-registry.v1.json')
    catalog_path = batch.V / registry['path']
    assert batch.sha(catalog_path) == registry['sha256']
    selected = [a for a in batch.read(catalog_path)['assets'] if a['id'] in IDS]
    assert len(selected) == 2 and all(not a.get('preview') for a in selected)
    prep = batch.read(OLD / 'PREPARED.v1.json')
    cases = [c for c in prep['cases'] if c['id'] in IDS]
    assert len(cases) == 2
    assert batch.read(OLD / 'RUN-RESULT.v1.json')['source_inputs_unchanged']
    helper = batch.HELPER.with_name(CLASS + '.cs')
    target = batch.PROJECT / 'Assets/Editor' / helper.name
    assert not helper.exists() and not target.exists()
    text = batch.HELPER.read_text('utf-8').replace('OpenRPGSourceLibraryThumbnailBatchV1', CLASS)
    # HDRP directional intensity is physical lux. Keep the source shader,
    # exposure, meshes, textures, camera framing and image conversion intact.
    assert text.count('light.intensity=1;') == 2
    text = text.replace('light.intensity=1;', 'light.intensity=10000;')
    needle = 'public string presentation_tonemapping="Neutral";'
    assert text.count(needle) == 1
    text = text.replace(needle, needle + 'public string presentation_lighting="Directional 10000 lux; camera-aligned; presentation only";')
    ROOT.mkdir()
    helper.write_text(text, 'utf-8')
    shutil.copyfile(helper, target)
    for i, case in enumerate(cases):
        folder = ROOT / f'case-{i:02d}'
        folder.mkdir()
        case['output'] = str(folder / 'NATIVE-RESULT.v1.json')
    prep.update(cases=cases, expected_cards=2, expected_images=4,
                presentation_directional_lux=10000, presentation_fixed_ev=8,
                images_installed_in_vault=False, full_import=False,
                sub_batch='L08.IMAGE.HOUSE.CLIFF.LUX.737')
    prep['input_pins'].update({str(p): batch.sha(p) for p in [
        Path(__file__).resolve(), BASE, helper, target, catalog_path,
        OLD / 'PREPARED.v1.json', OLD / 'RUN-RESULT.v1.json']})
    batch.write(ROOT / 'REQUEST.v1.json', dict(cases=cases, output=str(ROOT / 'NATIVE-RESULT.v1.json')))
    prep['input_pins'][str(ROOT / 'REQUEST.v1.json')] = batch.sha(ROOT / 'REQUEST.v1.json')
    batch.write(ROOT / 'PREPARED.v1.json', prep)
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
                                 cwd=ROOT, stdout=log, stderr=log,
                                 stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    batch.write(ROOT / 'JOB.v1.json', dict(pid=child.pid))
    print(json.dumps(dict(pid=child.pid, cards=2, images=4, presentation_lux=10000, output=str(ROOT))))

def run():
    text = BASE.read_text('utf-8')
    assert text.count("'OpenRPGSourceLibraryThumbnailBatchV1.Export'") == 1
    exec(compile(text.replace("'OpenRPGSourceLibraryThumbnailBatchV1.Export'", repr(CLASS + '.Export')), str(BASE), 'exec'), batch.__dict__)
    batch.ROOT = ROOT
    batch.run()

if __name__ == '__main__':
    prepare() if sys.argv[-1] == 'prepare' else run()
