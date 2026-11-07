"""Rehash all current registered model payloads on F:, without reconversion."""
from pathlib import Path
import datetime, hashlib, json, os, time

V=Path('F:/OpenRPG_AssetVault').resolve()
O=Path('F:/OpenRPG_ImportWork/current-model-payload-integrity-v761')
assert not O.exists()
O.mkdir()
def sha(path):
    with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def write(name,value):
    (O/name).write_text(json.dumps(value,indent=2),'utf-8')
def utc():return datetime.datetime.now(datetime.timezone.utc).isoformat()
started=time.monotonic()
write('JOB.v1.json',dict(pid=os.getpid(),script=str(Path(__file__).resolve()),started_utc=utc()))
pins={}
def read(path,expected=None):
    actual=sha(path)
    if expected is not None and actual!=expected:raise ValueError(f'Metadata hash mismatch: {path}')
    pins[str(path)]=actual
    return json.loads(Path(path).read_text('utf-8-sig'))
try:
    registry=read(V/'metadata/asset-import-registry.v1.json')
    groups={};cards=0
    for pack,entry in registry['packs'].items():
        root=(V/entry['path']).resolve()
        if not root.is_relative_to(V):raise ValueError('Metadata path outside Vault')
        data=read(root/'details.v1.json',entry['files']['details.v1.json'])
        if data['pack_id']!=pack:raise ValueError('Pack identity mismatch')
        for row in data['assets']:
            cards+=1
            key=(row['model_path'],row['model_sha256'])
            groups.setdefault(key,[]).append(dict(id=row['id'],pack=pack))
    write('INPUT-PINS.v1.json',pins)
    write('INVENTORY.v1.json',dict(model_cards=cards,unique_payload_references=len(groups),files=[dict(path=path,sha256=digest,cards=rows) for (path,digest),rows in groups.items()]))
    passed=0;failures=0;bytes_read=0
    with (O/'OBSERVATIONS.v1.jsonl').open('x',encoding='utf-8') as output:
        for i,((raw,expected),rows) in enumerate(groups.items(),1):
            record=dict(path=raw,expected_sha256=expected,cards=rows)
            try:
                p=Path(raw).resolve(strict=True)
                if not p.is_relative_to(V):raise ValueError('Payload outside F: Vault')
                before=p.stat();actual=sha(p);after=p.stat()
                record.update(actual_sha256=actual,bytes=before.st_size)
                bytes_read+=before.st_size
                if (before.st_size,before.st_mtime_ns)!=(after.st_size,after.st_mtime_ns):raise ValueError('Payload changed during read')
                if actual!=expected:raise ValueError('Payload SHA differs from metadata')
                record['status']='sha256_verified';passed+=1
            except (OSError,ValueError) as error:
                record.update(status='failed',error=str(error));failures+=1
            output.write(json.dumps(record)+'\n');output.flush()
            if i==1 or i%25==0 or i==len(groups):
                write('PROGRESS.v1.json',dict(checked_payloads=i,total_payloads=len(groups),passed=passed,failed=failures,bytes_read=bytes_read,elapsed_s=time.monotonic()-started,utc=utc()))
    changed=[p for p,h in pins.items() if sha(p)!=h]
    result=dict(status='current_registered_model_sha256_verified' if failures==0 and not changed else 'integrity_failures_or_inputs_changed',model_cards=cards,checked_unique_payloads=len(groups),passed=passed,failed=failures,changed_metadata_inputs=changed,bytes_read=bytes_read,elapsed_s=time.monotonic()-started,finished_utc=utc(),scope='Current registered model payload hashes and containment only',gltf_semantics_revalidated=False,all_sources_and_licenses_rehashed=False,entire_vault_transfer_verified=False,studio_visual_verified=False,full_import_increment=0)
    write('RESULT.v1.json',result);print(json.dumps(result))
except Exception as error:
    write('RESULT.v1.json',dict(status='audit_failed',error=str(error),finished_utc=utc(),full_import_increment=0))
    raise
