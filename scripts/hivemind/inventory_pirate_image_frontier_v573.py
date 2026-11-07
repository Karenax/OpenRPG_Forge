"""Classify uncaptured LOD image groups against retained material refusals before rendering."""
from pathlib import Path
import json,hashlib,re,datetime
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'pirate-image-frontier-v573'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
pointer=V/'metadata/asset-source-support-registry.v1.json';registry=read(pointer);catalogpath=V/registry['path']
assert sha(catalogpath)==registry['sha256'];catalog=read(catalogpath)['assets']
queuepath=W/'queue/pirate-island/prefabs.json';queue=read(queuepath)
guardpath=W/'pirate-lod-thumbnail-batch07-guard-v566/RESULT.v1.json';guard=read(guardpath)
pins={str(p):sha(p) for p in [Path(__file__),pointer,catalogpath,queuepath,guardpath]}
blocked={'a441b81a4efaff04a85744086c47c402'}
for p in guard['input_pins']:
    if 'guard-v' in p and p.endswith('RESULT.v1.json'):
        prior=read(p);pins[p]=sha(p);blocked.update(r['guid'] for r in prior.get('changes',[]))
    if 'pirate-private-material-migrations-reopen-v' in p and p.endswith('RESULT.v1.json'):
        prior=read(p);pins[p]=sha(p)
        for row in prior.get('material_changes',[]):
            if row.get('alpha_to_mask_keyword_moved_to_invalid'):
                meta=Path(row['path']).with_suffix('.mat.meta');pins[str(meta)]=sha(meta)
                blocked.add(re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text('utf-8-sig'),re.M)[1])
excluded=set()
for name in ['pirate-lod-thumbnail-batch06-v513','pirate-lod-thumbnail-batch07-v565']:
    p=W/name/'PREPARED.v1.json';pins[str(p)]=sha(p);excluded.update(c['prefab_path'] for c in read(p)['cases'])
pool={}
for card in catalog:
    path=card.get('source_asset_path','')
    if card.get('pack_id')=='hivemind.pirate-island' and '/HDRP' in path and path.endswith('.prefab'):
        pool.setdefault(path,{})[card.get('original_lod_level')]=card
eligible=[];held=[]
for path,rows in sorted(pool.items()):
    if path in excluded or not all(i in rows for i in [0,1,2]) or rows[1].get('preview') or rows[2].get('preview'):continue
    original=Path(queue['source_root'])/path;assert sha(original)==rows[0]['source_sha256']
    pins[str(original)]=sha(original)
    text=original.read_text('utf-8-sig');materials=set()
    for match in re.finditer(r'^  m_Materials:\s*\n((?:  -[^\n]*\n)+)',text,re.M):
        materials.update(re.findall(r'guid: ([a-f0-9]{32})',match[1]))
    # This preflight is for inline MeshRenderers only; do not infer unresolved
    # nested dependencies to be safe. Native capture remains authoritative.
    assert materials and materials<=set(queue['materials']),path
    affected=materials & blocked
    row=dict(source_path=path,source_sha256=sha(original),candidate_ids=[rows[i]['id'] for i in [1,2]],declared_material_guids=sorted(materials),blocked_material_guids=sorted(affected))
    (held if affected else eligible).append(row)
O.mkdir()
result=dict(status='uncaptured_lod_image_frontier_classified_by_declared_material_references',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),catalog_revision=registry['revision'],source_primary_images_missing=sum(not c.get('preview') for c in catalog),eligible_groups=eligible,held_groups=held,eligible_variants=2*len(eligible),held_variants=2*len(held),already_captured_paths=sorted(excluded),blocked_material_guids=sorted(blocked),input_pins=pins,full_import=False,catalog_changed=False,scope='Preflight routing only. Native capture, retained source guards, exact LOD identities and visual review are still required.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(eligible_groups=len(eligible),held_groups=len(held),next_names=[Path(r['source_path']).stem for r in eligible[:8]],held_material_counts={g:sum(g in r['blocked_material_guids'] for r in held) for g in blocked if any(g in r['blocked_material_guids'] for r in held)})))
