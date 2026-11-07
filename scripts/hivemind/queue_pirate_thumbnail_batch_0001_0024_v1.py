"""Prepare a private Pirate bench and capture 24 unique LOD0 source prefabs."""
from pathlib import Path
from datetime import datetime, timezone
import json
import shutil
import subprocess
import sys

import queue_house_thumbnail_batch_v1 as capture

S = Path(__file__).parent
W, V = capture.W, capture.V
ROOT = W / 'pirate-source-thumbnail-batch-0001-0024-v1'
PROJECT = W / 'unity-library-source-thumbnail-pirate-bench-v1/project'
TEMPLATE = capture.PROJECT
MANIFEST = W / 'hivemind-run/pirate-island/source.v1.json'


def prepare():
    capture.no_unity()
    assert not ROOT.exists() and not PROJECT.parent.exists()
    ROOT.mkdir()
    capture.ROOT = ROOT
    capture.save(dict(status='private_source_bench_preparation_queued', full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
                                 cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL,
                                 creationflags=subprocess.CREATE_NO_WINDOW)
    capture.write(ROOT / 'JOB.v1.json', dict(controller_pid=child.pid,
                  created_utc=datetime.now(timezone.utc).isoformat()))
    print(json.dumps(dict(controller_pid=child.pid, expected_cards=24, root=str(ROOT))))


def run():
    capture.ROOT = ROOT
    capture.PROJECT = PROJECT
    try:
        capture.no_unity()
        capture.save(dict(status='private_source_bench_copying_verified_assets', full_import=False))
        manifest = capture.read(MANIFEST)
        assert manifest['archive_sha256'] == '5b713fd7d79048baf56d2468959d8db5110929d2e8a45b9f4f4c496dd754e543'
        registry = capture.read(V / 'metadata/asset-source-support-registry.v1.json')
        catalog_path = V / registry['path']
        assert capture.sha(catalog_path) == registry['sha256']
        catalog = capture.read(catalog_path)
        selected = sorted([a for a in catalog['assets'] if a.get('pack_id') == 'hivemind.pirate-island'
                           and not a.get('preview') and a.get('original_lod_level') == 0
                           and '/HDRP' in a.get('source_asset_path', '')
                           and a['source_asset_path'].endswith('.prefab')], key=lambda a: a['id'])[:24]
        assert len(selected) == 24 and len({a['source_asset_path'] for a in selected}) == 24
        PROJECT.mkdir(parents=True)
        pins = {str(p): capture.sha(p) for p in [Path(__file__), S / 'queue_house_thumbnail_batch_v1.py',
                                               MANIFEST, catalog_path, capture.HELPER]}
        for folder in ['Packages', 'ProjectSettings']:
            shutil.copytree(TEMPLATE / folder, PROJECT / folder)
            for p in (PROJECT / folder).rglob('*'):
                if p.is_file():
                    origin = TEMPLATE / p.relative_to(PROJECT)
                    assert p.read_bytes() == origin.read_bytes()
                    pins[str(origin)] = pins[str(p)] = capture.sha(p)
        omitted, copied = [], []
        for row in manifest['files']:
            source = MANIFEST.parent / row['stored_path']
            assert source.resolve().is_relative_to(MANIFEST.parent.resolve())
            assert capture.sha(source) == row['sha256'], source
            pins[str(source)] = row['sha256']
            rel = Path(row['stored_path']).relative_to('source')
            assert rel.parts[0] == 'Assets'
            # Never execute purchased CPU scripts or assembly definitions.
            asset_name = str(rel).removesuffix('.meta').lower()
            if Path(asset_name).suffix in {'.cs', '.dll', '.exe', '.asmdef', '.asmref', '.unitypackage'}:
                omitted.append(str(rel))
                continue
            target = PROJECT / rel
            assert target.resolve().is_relative_to(PROJECT.resolve())
            target.parent.mkdir(parents=True, exist_ok=True)
            assert not target.exists()
            shutil.copyfile(source, target)
            assert capture.sha(target) == row['sha256']
            # Unity can upgrade importer metadata in this disposable project.
            # Every original remains pinned; asset bytes must stay identical.
            if target.suffix != '.meta':
                pins[str(target)] = row['sha256']
            copied.append(str(rel))
        fixture_files = []
        for item in ['Assets/OpenRPGProbe', 'Assets/HDRPDefaultResources',
                     'Assets/DefaultVolumeProfile.asset', 'Assets/DefaultVolumeProfile.asset.meta']:
            origin = TEMPLATE / item
            sources = list(origin.rglob('*')) if origin.is_dir() else [origin]
            for source in sources:
                if not source.is_file():
                    continue
                assert source.suffix in {'.asset', '.meta'}, source
                target = PROJECT / source.relative_to(TEMPLATE)
                target.parent.mkdir(parents=True, exist_ok=True)
                if target.exists():
                    assert source.read_bytes() == target.read_bytes(), target
                else:
                    shutil.copyfile(source, target)
                assert source.read_bytes() == target.read_bytes()
                pins[str(source)] = capture.sha(source)
                if target.suffix != '.meta':
                    pins[str(target)] = capture.sha(target)
                fixture_files.append(str(target))
        helper = PROJECT / 'Assets/Editor' / capture.HELPER.name
        helper.parent.mkdir(parents=True, exist_ok=True)
        assert not helper.exists()
        shutil.copyfile(capture.HELPER, helper)
        pins[str(helper)] = capture.sha(helper)
        cases = []
        for i, asset in enumerate(selected):
            assert asset['source_archive_sha256'] == manifest['archive_sha256']
            assert (PROJECT / asset['source_asset_path']).is_file()
            for field, digest in [('source_file', 'source_sha256'), ('source_meta_file', 'source_meta_sha256')]:
                p = V / asset[field]
                assert p.resolve().is_relative_to(V.resolve()) and capture.sha(p) == asset[digest]
                pins[str(p)] = asset[digest]
            folder = ROOT / f'case-{i:02d}'
            folder.mkdir()
            cases.append(dict(id=asset['id'], prefab_path=asset['source_asset_path'],
                              prefab_guid=asset['source_guid'], output=str(folder / 'NATIVE-RESULT.v1.json')))
        request = ROOT / 'REQUEST.v1.json'
        capture.write(request, dict(cases=cases, output=str(ROOT / 'NATIVE-RESULT.v1.json')))
        pins[str(request)] = capture.sha(request)
        capture.write(ROOT / 'BENCH.v1.json', dict(project=str(PROJECT), copied_files=copied,
                      excluded_purchased_code=omitted, trusted_hdrp_fixture_files=fixture_files,
                      disposable_project_importer_metadata_may_upgrade=True,
                      sources_preserved=True, original_version_parity=False))
        capture.write(ROOT / 'PREPARED.v1.json', dict(sub_batch='L08.IMAGE.PIRATE.01', expected_cards=24,
                      expected_images=48, cases=cases, input_pins=pins, catalog_revision=registry['revision'],
                      source_files_verified=len(manifest['files']), presentation_only=True,
                      source_assets_executed=False, original_lod_level=0, full_import=False))
        capture.run()
    except Exception as exc:
        capture.save(dict(status='private_source_bench_preparation_refused', error=repr(exc),
                          full_import=False, images_installed_in_vault=False))


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
