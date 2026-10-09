"""Copy only original prefab/FBX/material data and the trusted capture helper.

The project is for native identities and poses, not shader/texture parity.
"""
import datetime
import json
from pathlib import Path
import re
import shutil
import time

from plan_villa_native_prefab_hierarchies import sha, SOURCE, HELPER, API

WORK = Path('F:/OpenRPG_ImportWork')
PLAN = WORK/'villa-native-prefab-hierarchy-plan-v1/PLAN.v1.json'
ROOT = WORK/'villa-native-prefab-hierarchy-project-v1'
PROJECT = ROOT/'project'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def save(name, value):
    target = ROOT/name
    temporary = target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    for attempt in range(40):
        try:
            temporary.replace(target)
            return
        except PermissionError:
            if attempt == 39:
                raise
            time.sleep(.25)


def main():
    assert not ROOT.exists(), 'Previous preparation preserved'
    plan = read(PLAN)
    assert plan['logical_scenes'] == 226 and plan['unique_prefab_roots'] == 76
    assert len(plan['requests']) == 76
    assert sha(HELPER) == plan['trusted_helper_sha256']
    for path, expected in plan['input_pins'].items():
        assert sha(Path(path)) == expected
    sources = dict(plan['source_pins'])
    material_index = {}
    for meta in SOURCE.rglob('*.mat.meta'):
        text = meta.read_text('utf-8-sig')
        match = re.search(r'^guid:\s*([0-9a-f]{32})\s*$', text, re.M)
        assert match, 'Malformed original material metadata'
        guid = match.group(1)
        assert guid not in material_index, 'Ambiguous original material GUID'
        material_index[guid] = Path(str(meta)[:-5])
    needed = {guid for row in plan['logical_scene_bindings']
              for binding in row['expected_source_renderer_bindings']
              for guid in binding['material_guids']}
    missing = []
    for guid in sorted(needed):
        if guid not in material_index:
            missing.append(guid)
            continue
        path = material_index[guid]
        sources[str(path)] = sha(path)
        sources[str(path)+'.meta'] = sha(Path(str(path)+'.meta'))
    assert not missing, 'Source material identities unresolved: ' + str(missing)
    ROOT.mkdir()
    state = {'status': 'copying_trusted_source_data', 'expected_prefab_roots': 76,
             'logical_scene_ids': 226, 'files_copied_verified': 0,
             'expected_source_files': len(sources), 'bytes_copied': 0,
             'native_captures_executed': False, 'purchased_scripts_copied_or_executed': False,
             'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        copies = []
        for name, expected in sources.items():
            source = Path(name)
            relative = source.relative_to(SOURCE)
            assert relative.parts[0] == 'Assets' and '..' not in relative.parts
            underlying = Path(str(source)[:-5]) if source.suffix.lower() == '.meta' else source
            assert underlying.suffix.lower() in ('.prefab', '.fbx', '.mat')
            assert sha(source) == expected
            target = PROJECT/relative
            target.parent.mkdir(parents=True, exist_ok=True)
            assert not target.exists()
            shutil.copy2(source, target)
            assert sha(target) == expected
            copies.append({'source':str(source),'source_sha256':expected,
                           'private_copy':str(target),'private_copy_sha256':expected})
            state['files_copied_verified'] += 1
            state['bytes_copied'] += target.stat().st_size
            save('RUN-RESULT.v1.json', state)
        editor = PROJECT/'Assets/OpenRPGImportProbe/Editor'
        editor.mkdir(parents=True)
        own_helper = editor/HELPER.name
        shutil.copy2(HELPER, own_helper)
        assert sha(own_helper) == plan['trusted_helper_sha256']
        (PROJECT/'Packages').mkdir()
        (PROJECT/'Packages/manifest.json').write_text('{"dependencies":{}}\n','utf-8')
        (PROJECT/'ProjectSettings').mkdir()
        (PROJECT/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.5.8f1\n','utf-8')
        requests = [{'asset_id':r['asset_id'],'asset_path':r['asset_path'],
                     'expected_guid':r['expected_guid'],'source_sha256':r['source_sha256']}
                    for r in plan['requests']]
        manifest = ROOT/'REQUESTS.v1.json'
        manifest.write_text(json.dumps({'schema_version':1,'cases':requests},indent=2),'utf-8')
        pins = {str(p):sha(p) for p in [Path(__file__).resolve(),PLAN,HELPER,API,own_helper,
                   manifest,PROJECT/'Packages/manifest.json',PROJECT/'ProjectSettings/ProjectVersion.txt']}
        for copy in copies:
            assert sha(Path(copy['source'])) == copy['source_sha256']
            assert sha(Path(copy['private_copy'])) == copy['private_copy_sha256']
        save('PREPARED.v1.json', {'project':str(PROJECT),'requests':str(manifest),
             'capture_output':str(ROOT/'NATIVE-HIERARCHY.v1.json'),
             'input_pins':pins,'copies':copies,'material_guids_preserved':sorted(needed),
             'prefab_aliases_plan':str(PLAN),'expected_native_prefab_roots':76,
             'expected_logical_scene_ids':226,'trusted_editor_helper':str(own_helper),
             'shader_texture_runtime_dependencies_not_admitted':True,
             'purchased_scripts_copied_or_executed':False})
        state.update(status='76_prefab_private_source_hierarchy_project_prepared',
                     material_identities_preserved=len(needed),
                     source_and_private_copies_reopened=True,
                     finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    except Exception as error:
        state.update(status='refused_or_partial',error=str(error))
    save('RUN-RESULT.v1.json',state)
    print(json.dumps(state))


if __name__ == '__main__':
    main()
