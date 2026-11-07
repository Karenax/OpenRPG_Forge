"""Prepare exact source/target glTF accessor buffers for all House render/collider meshes."""
from pathlib import Path
import base64
import datetime
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-accessor-buffers-v665'
assert not O.exists()
def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))
def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

converted_path = W / 'house-converted-shared-native-geometry-v653/RESULT.v1.json'
native_path = W / 'house-native-prefab-geometry-v651/NATIVE-RESULT.v1.json'
checkpoint_path = W / 'house-geometry-checkpoint-v654/RESULT.v1.json'
converted, native, checkpoint = [read(p) for p in [converted_path, native_path, checkpoint_path]]
assert checkpoint['meshes'] == 42 and checkpoint['prefab_cards'] == 24
pins = {str(p): sha(p) for p in [converted_path, native_path, checkpoint_path, Path(__file__).resolve()]}
for proof in [converted, checkpoint]:
    for field in ['input_pins', 'output_pins']:
        for path, digest in proof.get(field, {}).items():
            assert sha(path) == digest, path
            pins[path] = digest
source_cases = {c['prefab_guid']: c for c in native['cases']}
semantics = {'Position':'POSITION', 'Normal':'NORMAL', 'Tangent':'TANGENT', 'TexCoord0':'TEXCOORD_0', 'TexCoord1':'TEXCOORD_1'}
O.mkdir()
outputs, cases = {}, []
for case in converted['cases']:
    original_case = source_cases[case['prefab_guid']]
    originals = {(m['identity']['guid'], m['identity']['file_id']):m for m in original_case['meshes']}
    meshes = []
    for mesh in case['meshes']:
        key = (mesh['identity']['guid'], mesh['identity']['file_id'])
        source = originals[key]
        folder = O / case['prefab_guid'] / key[0] / key[1]
        folder.mkdir(parents=True)
        target = {s['index']:Path(s['path']).read_bytes() for s in mesh['streams']}
        raw_source = {s['index']:base64.b64decode(s['bytes_base64'], validate=True) for s in source['streams']}
        strides = {s['index']:s['stride'] for s in mesh['streams']}
        descriptors = {'target':{}, 'source':{}}
        for a in mesh['attributes']:
            assert a['format'] == 'Float32' and a['semantic'] in semantics
            semantic, width, stride = semantics[a['semantic']], a['dimension'], strides[a['stream']]
            for lane, streams in [('target',target), ('source',raw_source)]:
                raw = streams[a['stream']]
                assert len(raw) == mesh['vertex_count'] * stride
                piece = b''.join(raw[i*stride+a['offset']:i*stride+a['offset']+4*width] for i in range(mesh['vertex_count']))
                path = folder / (semantic + '.' + lane + '.f32')
                path.write_bytes(piece)
                reopened = path.read_bytes()
                assert len(reopened) == mesh['vertex_count'] * width * 4
                for i in range(mesh['vertex_count']):
                    assert reopened[i*width*4:(i+1)*width*4] == raw[i*stride+a['offset']:i*stride+a['offset']+width*4]
                outputs[str(path)] = sha(path)
                descriptors[lane][semantic] = dict(path=str(path), sha256=sha(path), dimension=width, count=mesh['vertex_count'])
        assert set(descriptors['target']) == set(semantics.values())
        for uv in ['TEXCOORD_0','TEXCOORD_1']:
            assert descriptors['target'][uv]['sha256'] == descriptors['source'][uv]['sha256']
        meshes.append(dict(identity=mesh['identity'], attributes=descriptors['target'], original_attributes=descriptors['source'], converted_geometry=mesh))
    cases.append(dict(card_id=case['card_id'], prefab_guid=case['prefab_guid'], meshes=meshes, preserved_native_case=original_case))
assert len(cases) == 24 and sum(len(c['meshes']) for c in cases) == 42
assert len(outputs) == 420
assert all(sha(p) == h for p,h in {**pins, **outputs}.items())
result = dict(schema='hivemind.house-accessor-buffers.v665', utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), prefabs=24, meshes=42, attribute_buffers=420, cases=cases, input_pins=pins, output_pins=outputs, glb_created=False, renderer_installed=False, full_import=False)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
note = '''## House — états de passes et buffers GLB préparés (v664/v665)

- 72 propriétés de rendu source/native comparées ; 17 passes générées conservées avec blends et masques par attachment et headers stencil.
- 24 prefabs / 42 meshes : 420 buffers d'attributs source/cible décomposés et réouverts élément par élément. UV0/UV1 inchangés, références et données de collision conservées.
- Preuves : F:/OpenRPG_ImportWork/house-native-pass-states-v664/RESULT.v1.json et F:/OpenRPG_ImportWork/house-accessor-buffers-v665/RESULT.v1.json.
- Restent les matrices de scènes et GLB autonomes, l'admission des pipelines, les comparaisons GPU et la validation dans Studio. **Incrément complet : 0**.

'''
(O / 'LOT.md').write_text(note, 'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    path = W / 'SUIVI_LOTS_HIVEMIND' / name
    before = path.read_bytes()
    (O / (name + '.before')).write_bytes(before)
    path.write_bytes(note.encode('utf-8') + before)
print(json.dumps(dict(prefabs=24, meshes=42, buffers=420, receipt=str(O / 'RESULT.v1.json'))))
