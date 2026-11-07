"""Prepare exact source thumbnails and repeat controls; no Vault mutation or Unity launch."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, re, shutil

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W/'mountain-exact-source-capture-v873'
STAGE = W/'mountain-hdrp-capture-stage-v869/RESULT.v1.json'
BASE = R/'scripts/hivemind/candidates/SourceLibraryLodThumbnailBatchV353.cs'

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

assert not O.exists(), 'Preserve previous prepared capture'
stage = read(STAGE)
assert stage['status'] == 'isolated_mountain_hdrp_render_sources_staged_no_unity_launched'
assert stage['candidate_cards'] == 47 and stage['prefab_groups'] == 37
project = Path(stage['project'])
for path, digest in stage['input_pins'].items():
    assert sha(path) == digest, path
for copy in stage['copies']:
    assert sha(copy['target']) == copy['sha256'], copy['target']
helpers = [c for c in stage['copies'] if c['kind'] == 'controlled-helper']
assert len(helpers) == 2
assert set((project/'Assets').rglob('*.cs')) == {Path(c['target']) for c in helpers}
assert not list((project/'Assets').rglob('*.dll'))
source = BASE.read_text('utf-8-sig')
changes = {
    'OpenRPGSourceLibraryLodThumbnailBatchV353': 'OpenRPGSourceLibraryExactThumbnailBatchV873',
    'public int requested_lod,expected_empty_lod_slots;': 'public int requested_lod,expected_empty_lod_slots,expected_lod_groups;',
    'public string path,sha256;': 'public string path,sha256,raw_path,raw_sha256;',
    'if(sourceLodGroups.Length==0)throw new Exception("Source LOD group absent; cannot qualify a variant");':
        'if(sourceLodGroups.Length!=request.expected_lod_groups)throw new Exception("Source LOD group count differs from preserved prefab");\n'
        '   if(sourceLodGroups.Length==0&&request.requested_lod!=0)throw new Exception("A non-LOD source cannot represent a lower LOD variant");',
    'if(selectedLodRenderers.Count==0)throw new Exception("Requested LOD has no renderers");':
        'if(sourceLodGroups.Length==0)foreach(var renderer in fixture.GetComponentsInChildren<Renderer>())\n'
        '    if(renderer.enabled&&!renderer.forceRenderingOff){selectedLodRenderers.Add(renderer);lodRenderers.Add(renderer);}\n'
        '   if(selectedLodRenderers.Count==0)throw new Exception("Requested source selection has no renderers");',
    'raw.Apply();var colors=raw.GetPixels();':
        'raw.Apply();byte[] rawBytes=raw.GetRawTextureData();string rawPath=Path.Combine(Path.GetDirectoryName(request.output),"view-"+view+".rgba16f");'
        'if(File.Exists(rawPath))throw new Exception("Preserve raw capture");File.WriteAllBytes(rawPath,rawBytes);var colors=raw.GetPixels();',
    'new Row{path=path,sha256=Hash(bytes),view=view':
        'new Row{path=path,sha256=Hash(bytes),raw_path=rawPath,raw_sha256=Hash(rawBytes),view=view',
}
for before, after in changes.items():
    assert source.count(before) == 1, before
    source = source.replace(before, after)
cases = []
groups = {}
for card in sorted(stage['cards'], key=lambda c:(c['source_asset_path'],c['original_lod_level'])):
    prefab = project/card['source_asset_path']
    assert prefab.resolve().is_relative_to(project.resolve()) and sha(prefab) == card['source_sha256']
    text = prefab.read_text('utf-8-sig')
    lod_sections = re.findall(r'^--- !u!205.*?(?=^---|\Z)',text,re.M|re.S)
    empty = sum(len(re.findall(r'renderer: \{fileID: 0\}',s)) for s in lod_sections)
    assert lod_sections or card['original_lod_level'] == 0
    groups[card['source_asset_path']] = dict(lod_groups=len(lod_sections),empty_slots=empty)
    for label in ['warmup','current','repeat']:
        folder = O/f'case-{len(cases):03d}-{label}'
        cases.append(dict(id=card['id']+':'+label,source_card_id=card['id'],
                          prefab_path=card['source_asset_path'],prefab_guid=card['source_guid'],
                          requested_lod=card['original_lod_level'],expected_empty_lod_slots=empty,
                          expected_lod_groups=len(lod_sections),output=str(folder/'NATIVE-RESULT.v1.json')))
assert len(cases) == 141
O.mkdir();(O/'temp').mkdir()
helper = O/'SourceLibraryExactThumbnailBatchV873.cs'
helper.write_text(source,'utf-8')
target = project/'Assets/Editor'/helper.name
assert not target.exists()
shutil.copyfile(helper,target)
assert sha(helper) == sha(target)
for case in cases:
    Path(case['output']).parent.mkdir()
manifest = O/'manifest.json'
manifest.write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
pins = {str(p):sha(p) for p in [Path(__file__),STAGE,BASE,helper,target,manifest]}
pins.update({c['target']:c['sha256'] for c in stage['copies']})
result = dict(status='exact_lod_source_capture_prepared_not_launched',project=str(project),
              manifest=str(manifest),helper=str(helper),execute_method='OpenRPGSourceLibraryExactThumbnailBatchV873.Export',
              catalog_revision=stage['catalog_revision'],cases=cases,groups=groups,input_pins=pins,
              adaptations=changes,prefabs=37,source_cards=47,capture_cases=141,views=282,
              validation_required=['source GUIDs','exact LOD mesh identities','empty slots','compiled shader passes',
                                   'raw repeat comparison','PNG decode','SDK migration review','visual review','native library reader'],
              raw_repeat_qualified=False,sdk_migration_qualified=False,images_admitted=0,
              actual_studio_gui_verified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'PREPARED.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:result[k] for k in ['status','prefabs','source_cards','capture_cases','views']}))
