"""Verify source-byte equivalence for nine URP cards, retaining distinct materials."""
import datetime
import hashlib
import json
from pathlib import Path
import re

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'villa-urp-no-png-source-equivalence-v1'
BATCH = W / 'library-no-png-source-batches-v1/L08.NO_PNG.05.v1.json'
BASE = W / 'hivemind-run/villa-forge/source.v1.json'
VARIANT = W / 'hivemind-run/villa-forge/variants/51e32236d3832c1f/source.v1.json'
NATIVE = W / 'villa-native-prefab-hierarchy-independent-reopen-v1/RESULT.v1.json'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def main():
    assert not ROOT.exists(), 'Prior equivalence evidence preserved'
    pins = {str(p): sha(p) for p in [BATCH, BASE, VARIANT, NATIVE, Path(__file__).resolve()]}
    batch, base, variant, native = [read(p) for p in [BATCH, BASE, VARIANT, NATIVE]]
    assert native['status'] == '226_native_prefab_pose_and_renderer_bindings_reopened'
    assert not native['changed_inputs'] and not native['refusals']
    by_native_id = {r['asset_id']: r for r in native['rows']}
    indexes = []
    for manifest in [base, variant]:
        index = {(r['guid'], r['kind']): r for r in manifest['files']}
        assert len(index) == len(manifest['files']), 'Ambiguous source manifest identity'
        indexes.append(index)

    def source(which, guid, kind, native_required=False):
        manifest_path = [BASE, VARIANT][which]
        row = indexes[which][guid, kind]
        path = manifest_path.parent / row['stored_path']
        assert path.resolve().is_relative_to(manifest_path.parent.resolve()), 'Source escaped manifest root'
        actual = sha(path)
        assert actual == row['sha256'] and path.stat().st_size == row['bytes'], 'Actual source differs from manifest'
        if native_required:
            assert native['input_pins'].get(str(path)) == actual, 'Base source not pinned by native proof'
        pins[str(path)] = actual
        return path, actual

    rows, source_comparisons, material_comparisons = [], {}, {}
    for card in batch['rows']:
        base_id = card['id'].replace(batch['pack_id'], 'hivemind.villa-forge', 1)
        assert base_id in by_native_id, 'Exact counterpart absent from native proof'
        guid = card['source_guid']
        source_guids = {guid}
        prefab, _ = source(1, guid, 'asset')
        mesh_guids, materials, unresolved = [], [], []
        for dependency in sorted(set(re.findall(rb'guid: ([a-f0-9]{32})', prefab.read_bytes()))):
            dependency = dependency.decode('ascii')
            target = indexes[1].get((dependency, 'asset'))
            if target is None:
                if dependency != '0' * 32:
                    unresolved.append(dependency)
                continue
            suffix = Path(target['source_path']).suffix.lower()
            if suffix == '.fbx':
                source_guids.add(dependency)
                mesh_guids.append(dependency)
            elif suffix == '.mat':
                pa, ha = source(0, dependency, 'asset')
                pb, hb = source(1, dependency, 'asset')
                material_comparisons[dependency] = dict(guid=dependency, base_path=str(pa),
                    variant_path=str(pb), base_sha256=ha, variant_sha256=hb,
                    bytes_identical=ha == hb, material_reuse_authorized=False)
                materials.append(dependency)
            else:
                unresolved.append(dependency)
        differences = []
        for dependency in sorted(source_guids):
            for kind in ['asset', 'asset.meta']:
                pa, ha = source(0, dependency, kind, native_required=True)
                pb, hb = source(1, dependency, kind)
                comparison = dict(guid=dependency, kind=kind, base_path=str(pa), variant_path=str(pb),
                                  base_sha256=ha, variant_sha256=hb, bytes_identical=ha == hb)
                source_comparisons[dependency + '/' + kind] = comparison
                if ha != hb:
                    differences.append(dependency + '/' + kind)
        rows.append(dict(id=card['id'], counterpart_id=base_id,
                         original_lod_level=card['original_lod_level'], mesh_guids=mesh_guids,
                         material_guids=materials, differing_source_components=differences,
                         unresolved_source_dependencies=unresolved,
                         source_pose_geometry_inputs_equivalent=bool(mesh_guids) and not differences and not unresolved,
                         native_pose_counterpart=by_native_id[base_id],
                         geometry_derivative_reuse_verified=False, material_reuse_authorized=False,
                         images_generated=False, studio_verified=False))
    assert len(rows) == 9
    assert all(sha(p) == h for p, h in pins.items()), 'Input changed during equivalence check'
    ROOT.mkdir()
    count = sum(r['source_pose_geometry_inputs_equivalent'] for r in rows)
    result = dict(status='villa_urp_source_equivalence_reopened', expected_cards=9,
                  source_equivalent_cards=count, distinct_prefabs=3,
                  source_comparisons=list(source_comparisons.values()),
                  material_comparisons=list(material_comparisons.values()), rows=rows, input_pins=pins,
                  next_action='For equivalent rows, reopen the corresponding geometry derivative and bind the URP materials separately; do not reuse HDRP materials or claim image/runtime parity.',
                  geometry_derivatives_converted=0, images_generated=0, assets_delivered=0,
                  gpu_rendered=False, studio_verified=False, full_import=False,
                  recorded_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(status=result['status'], expected=9, source_equivalent=count,
                         distinct_materials=len(material_comparisons), assets_delivered=0)))


if __name__ == '__main__':
    main()
