"""Read-only path/GUID/content preflight for reusing an already initialized project."""
import datetime
import hashlib
import json
from pathlib import Path
import re

W = Path('F:/OpenRPG_ImportWork')
PREP = W / 'unity-delta-null-material-defaults-v2/PREPARED.v1.json'
WARM = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
ROOT = W / 'delta-warm-hdrp-project-compatibility-v1'


def sha(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def guid(path):
    matches = re.findall(r'^guid:\s*([0-9a-f]{32})\s*$', Path(path).read_text('utf-8-sig'), re.M)
    if len(matches) != 1:
        raise ValueError('Exact source GUID absent: ' + str(path))
    return matches[0]


def main():
    assert not ROOT.exists(), 'Previous compatibility evidence retained'
    prep = json.loads(PREP.read_text('utf-8-sig'))
    pins = {str(PREP): sha(PREP), str(Path(__file__).resolve()): sha(__file__)}
    pins.update(prep['source_pins'])
    warm_guids = {}
    for meta in sorted((WARM / 'Assets').rglob('*.meta')):
        assert not meta.is_symlink()
        pins[str(meta)] = sha(meta)
        identity = guid(meta)
        warm_guids.setdefault(identity, []).append(meta)
    rows = []
    declarations = {}
    conflicts = []
    for project in prep['projects']:
        private = {Path(path): expected for path, expected in project['private_pins'].items()}
        pins.update(project['private_pins'])
        for source, expected in sorted(private.items()):
            assert sha(source) == expected and not source.is_symlink()
            relative = source.relative_to(Path(project['project']))
            target = WARM / relative
            state = 'new_path'
            target_hash = None
            if target.exists():
                assert target.is_file() and not target.is_symlink()
                target_hash = sha(target)
                pins[str(target)] = target_hash
                state = 'identical_existing_path' if target_hash == expected else 'conflicting_existing_path'
                if state == 'conflicting_existing_path':
                    conflicts.append(dict(kind='path_content', source=str(source), target=str(target)))
            identity = None
            matches = []
            if source.suffix == '.meta':
                identity = guid(source)
                original_asset = Path(str(source)[:-5])
                assert original_asset in private, 'Metadata has no prepared source asset'
                original_asset_hash = private[original_asset]
                declaration = (original_asset_hash, expected)
                if identity in declarations and declarations[identity] != declaration:
                    conflicts.append(dict(kind='delta_guid_content', guid=identity, source=str(source)))
                declarations[identity] = declaration
                for existing_meta in warm_guids.get(identity, []):
                    asset = Path(str(existing_meta)[:-5])
                    actual_hash = sha(asset) if asset.is_file() else None
                    if actual_hash is not None:
                        pins[str(asset)] = actual_hash
                    match = dict(meta=str(existing_meta), asset=str(asset),
                                 asset_sha256=actual_hash, meta_sha256=pins[str(existing_meta)],
                                 source_content_and_meta_identical=(actual_hash == original_asset_hash and
                                                                  pins[str(existing_meta)] == expected))
                    matches.append(match)
                    if not match['source_content_and_meta_identical']:
                        conflicts.append(dict(kind='warm_guid_content', guid=identity,
                                              source=str(source), existing=str(existing_meta)))
            rows.append(dict(pack_id=project['pack_id'], source=str(source), source_sha256=expected,
                             target=str(target), target_sha256=target_hash, path_state=state,
                             guid=identity, warm_guid_matches=matches))
    for path, expected in pins.items():
        assert sha(path) == expected, 'Input changed while inspecting: ' + path
    output = dict(schema='openrpg.delta-warm-project-compatibility.v1',
                  checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  status='warm_project_reuse_refused_by_source_collision' if conflicts else
                         'source_path_and_guid_content_compatible_native_context_still_required',
                  requested_materials=prep['requested_materials'], project_count=len(prep['projects']),
                  checked_prepared_files=len(rows), indexed_warm_guids=len(warm_guids),
                  conflicts=conflicts, rows=rows, input_pins=pins,
                  assets_copied=False, native_context_verified=False,
                  new_assets_converted=0, full_import=False)
    ROOT.mkdir()
    with (ROOT / 'RESULT.v1.json').open('x', encoding='utf-8') as out:
        json.dump(output, out, ensure_ascii=False, allow_nan=False, indent=2)
    print(json.dumps({key: output[key] for key in ['status', 'requested_materials',
                                                 'checked_prepared_files', 'indexed_warm_guids']}))
    print(json.dumps(dict(conflict_count=len(conflicts))))


if __name__ == '__main__':
    main()
