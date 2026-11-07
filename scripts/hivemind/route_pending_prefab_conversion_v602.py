"""Join live pending cards to preserved component evidence for executable lots."""
from pathlib import Path
from collections import Counter,defaultdict
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'pending-prefab-conversion-routing-v602'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
sp=V/'metadata/asset-source-support-registry.v1.json';registry=read(sp);catalogpath=V/registry['path'];assert sha(catalogpath)==registry['sha256']
catalog=read(catalogpath)['assets'];pending=[a for a in catalog if a.get('reason')=='Complete prefab, shader and importer/frame qualification pending.']
cp=W/'collection-prefab-component-scope-v103/PREFAB_COMPONENTS.v1.json';pp=cp.with_name('RESULT.v1.json');proof=read(pp)
assert proof['refusal_count']==0 and sha(cp)==proof['input_pins'][str(cp)]
index=defaultdict(list)
for row in read(cp):index[(row['pack_id'],row['guid'],row['source_path'])].append(row)
routes=Counter();by_pack=defaultdict(Counter);rows=[];unmatched=[];candidate_groups={}
pins={str(p):sha(p) for p in [Path(__file__),sp,catalogpath,cp,pp]}
for card in pending:
    key=(card['pack_id'],card['source_guid'],card['source_asset_path']);matches=index[key]
    if len(matches)!=1:unmatched.append(dict(id=card['id'],key=key,matches=len(matches)));continue
    source=matches[0];assert source['declared_sha256']==card['source_sha256'] and source['source_payload_rehashed']
    flags=source['classification_flags'];counts=source['component_counts']
    if flags['effect_candidate']:route='effect_runtime_adapter'
    elif flags['rig_or_animation_candidate']:route='rig_or_animation_adapter'
    elif flags['nested_prefab']:route='nested_dependency_assembly'
    elif flags['custom_script_component']:route='custom_component_semantics'
    elif flags['terrain_component']:route='terrain_adapter'
    elif flags['audio_source']:route='audio_component_adapter'
    elif counts.get('Light',0):route='local_light_adapter'
    elif flags['static_mesh_component']:route='static_prefab_shader_frame_and_collider_qualification'
    else:route='special_component_review'
    routes[route]+=1;by_pack[card['pack_id']][route]+=1
    row=dict(id=card['id'],pack_id=card['pack_id'],source_guid=card['source_guid'],source_path=card['source_asset_path'],source_sha256=card['source_sha256'],source_file=card['source_file'],source_meta_file=card['source_meta_file'],source_meta_sha256=card['source_meta_sha256'],source_lod=card.get('original_lod_level'),route=route,component_counts=counts,material_bindings=card.get('material_bindings',[]),preview_available=bool(card.get('preview')),source_components_record_sha256=hashlib.sha256(json.dumps(source,sort_keys=True).encode('utf-8')).hexdigest())
    rows.append(row)
    # Exact shader/frame evidence remains required even for plain static cases.
    allowed={'GameObject','Transform','MeshFilter','MeshRenderer','LODGroup','BoxCollider'}
    if route.startswith('static_') and set(counts)<=allowed:
        candidate_groups.setdefault(key,[]).append(row)
assert not unmatched,unmatched[:5]
assert len(rows)==len(pending)==6812
selection=[]
for key,cards in sorted(candidate_groups.items()):
    if len(selection)==8:break
    # Select base pack HDRP cases with an image available for reference review.
    if '.variant-' in key[0] or '/HDRP' not in key[2] or not all(r['preview_available'] for r in cards):continue
    original=next(iter(index[key]))
    source=Path(original['stored_path']);assert sha(source)==original['declared_sha256'];pins[str(source)]=sha(source)
    for row in cards:
        p=V/row['source_file'];meta=V/row['source_meta_file'];assert sha(p)==row['source_sha256'] and sha(meta)==row['source_meta_sha256'];pins[str(p)]=sha(p);pins[str(meta)]=sha(meta)
    selection.append(dict(pack_id=key[0],source_guid=key[1],source_path=key[2],source_sha256=original['declared_sha256'],card_ids=[r['id'] for r in cards],lod_levels=[r['source_lod'] for r in cards],component_counts=original['component_counts'],material_bindings=cards[0]['material_bindings']))
assert len(selection)==8
assert sha(sp)==pins[str(sp)]
O.mkdir();result=dict(status='live_pending_prefabs_routed_against_verified_source_components',catalog_revision=registry['revision'],cards_routed=len(rows),route_counts=dict(routes),pack_route_counts={k:dict(v) for k,v in by_pack.items()},plain_static_source_groups=len(candidate_groups),selected_next_groups=selection,input_pins=pins,unmatched=unmatched,full_import=False,scope='Routing proof only. Exact source/card SHA and component evidence joined. Existing geometry derivatives are to be reused, not reconverted. No source card promoted or shader approximation admitted.',finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');(O/'ROUTED-CARDS.v1.json').write_text(json.dumps(rows,indent=2),'utf-8')
lines=['# Lot conversion prefab suivant','',f'{len(rows)} fiches source en attente rapprochees de leurs composants conserves.','']
for route,count in routes.most_common():lines.append(f'- {count} : {route}')
lines+=['','## Huit groupes selectionnes','']
for row in selection:lines.append(f"- {row['pack_id']} / {Path(row['source_path']).stem} : {len(row['card_ids'])} fiches, composants {row['component_counts']}.")
lines+=['','Suite : resoudre les recettes exactes de materiaux et les derives de meshes existants ; verifier les transforms, LOD et BoxCollider natifs ; assembler le prefab. Aucune conversion complete declaree par cet inventaire.']
(O/'LOT.md').write_text('\n'.join(lines)+'\n','utf-8')
print(json.dumps(dict(cards_routed=len(rows),route_counts=dict(routes),selected=[dict(pack=r['pack_id'],name=Path(r['source_path']).stem,cards=len(r['card_ids'])) for r in selection])))
