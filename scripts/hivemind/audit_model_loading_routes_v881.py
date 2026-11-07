"""Census current GLB declarations only; not renderer admission or a new hash audit."""
from pathlib import Path
from datetime import datetime, timezone
from collections import Counter
import hashlib, json, os, struct, time

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault').resolve()
A = W/'current-model-payload-integrity-v761'
O = W/'model-loading-routes-v881'
assert not O.exists()

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def write(name,value):
    value['utc'] = datetime.now(timezone.utc).isoformat()
    (O/name).write_text(json.dumps(value,indent=2),'utf-8')

audit = read(A/'RESULT.v1.json')
assert audit['status'] == 'current_registered_model_sha256_verified'
pins = read(A/'INPUT-PINS.v1.json')
for path,digest in pins.items():
    assert sha(path) == digest, path
inventory = read(A/'INVENTORY.v1.json')
pins.update({str(p):sha(p) for p in [Path(__file__),A/'RESULT.v1.json',A/'INVENTORY.v1.json']})
O.mkdir()
write('INPUT-PINS.v1.json',dict(pins=pins))
write('JOB.v1.json',dict(pid=os.getpid(),script=str(Path(__file__).resolve())))
counts = Counter();card_counts = Counter();by_pack = {};failed = []
started = time.monotonic()
with (O/'DECLARATIONS.v1.jsonl').open('x',encoding='utf-8') as output:
    for index,entry in enumerate(inventory['files'],1):
        row = dict(path=entry['path'],audit_sha256=entry['sha256'],cards=entry['cards'])
        try:
            path = Path(entry['path']).resolve(strict=True)
            assert path.is_relative_to(V)
            before = path.stat()
            with path.open('rb') as stream:
                header = stream.read(20)
                assert len(header) == 20
                magic,version,total,length,kind = struct.unpack('<4sIIII',header)
                assert magic == b'glTF' and version == 2 and total == before.st_size
                assert kind == 0x4e4f534a and 0 < length <= 64*1024*1024 and length % 4 == 0 and 20+length <= total
                encoded = stream.read(length)
                assert len(encoded) == length
            after = path.stat()
            assert (before.st_size,before.st_mtime_ns) == (after.st_size,after.st_mtime_ns)
            doc = json.loads(encoded)
            materials = doc.get('materials',[])
            source = [i for i,m in enumerate(materials) if 'OPENRPG_source_graph' in m.get('extensions',{})]
            primitives = Counter()
            for mesh in doc.get('meshes',[]):
                for primitive in mesh.get('primitives',[]):
                    material = primitive.get('material')
                    assert material is None or isinstance(material,int) and 0 <= material < len(materials)
                    primitives['source' if material in source else 'ordinary'] += 1
            route = 'declares_source_graph' if source else 'no_source_graph_declaration'
            row.update(status='declaration_read',route=route,json_bytes=length,
                       source_material_indices=source,primitive_counts=dict(primitives),
                       source_frame_declared='source_frame_binding' in doc.get('extras',{}),
                       extensions_required=doc.get('extensionsRequired',[]),
                       skins=len(doc.get('skins',[])),animations=len(doc.get('animations',[])),
                       actual_loader_admission_verified=False)
            counts[route] += 1;card_counts[route] += len(entry['cards'])
            for card in entry['cards']:
                by_pack.setdefault(card['pack'],Counter())[route] += 1
        except Exception as exc:
            row.update(status='declaration_refused',error=repr(exc))
            failed.append(dict(path=entry['path'],error=repr(exc)))
            counts['declaration_refused'] += 1;card_counts['declaration_refused'] += len(entry['cards'])
        output.write(json.dumps(row)+'\n');output.flush()
        if index == 1 or index % 100 == 0 or index == len(inventory['files']):
            write('PROGRESS.v1.json',dict(checked=index,total=len(inventory['files']),unique_payload_counts=dict(counts),card_counts=dict(card_counts),elapsed_s=time.monotonic()-started))
changed = [p for p,h in pins.items() if sha(p) != h]
result = dict(status='current_glb_declaration_census_completed' if not failed and not changed else 'census_refusals_preserved',
              unique_payload_counts=dict(counts),card_counts=dict(card_counts),by_pack={p:dict(c) for p,c in by_pack.items()},
              failures=failed,changed_metadata_inputs=changed,observations_sha256=sha(O/'DECLARATIONS.v1.jsonl'),
              source_render_programs_validated=False,whole_payload_hashes_repeated=False,
              scope='Current GLB JSON declaration classification only; does not authorize ordinary loading or source rendering',
              actual_studio_gui_verified=False,full_import=False,complete_conversion_increment=0)
write('RESULT.v1.json',result)
print(json.dumps({k:v for k,v in result.items() if k not in ['by_pack','failures']}))
