"""Inventory all retained draw-mapping refusals without promoting any conversion."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');OUT=W/'townsmith-draw-binding-lot-plan-v2';assert not OUT.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={};groups={};rows=[];geometry_seen=set()
for directory,expected in [('townsmith-closed-prefab-bindings-v1',6),('townsmith-remaining-prefab-bindings-v1',31)]:
 receipt=W/directory/'RESULT.v1.json';d=read(receipt);pins[str(receipt)]=sha(receipt);assert d['material_draw_mapping_mismatches']==expected
 count=0
 for prefab in d['rows']:
  for binding in prefab['static_renderer_bindings']:
   if not binding['material_draw_mapping_requires_native_qualification']:continue
   count+=1;mesh=binding['source_mesh_identity'];materials=binding['original_material_slots'];assert mesh['exact_asset_identity_available']
   geometry=Path(binding['geometry_derivative'])/'GEOMETRY.v1.json';assert sha(geometry)==binding['geometry_receipt_sha256'];pins[str(geometry)]=sha(geometry);g=read(geometry);assert g['expected_identity']==dict(guid=mesh['guid'],file_id=mesh['file_id'])
   if geometry not in geometry_seen:
    geometry_seen.add(geometry)
    for path,h in g['source_stream_pins'].items():assert sha(Path(path))==h; pins[path]=h
    for name,item in g['derived_streams'].items():
     file=geometry.parent/name;assert file.stat().st_size==item['bytes'] and sha(file)==item['sha256'];pins[str(file)]=item['sha256']
   key=(mesh['guid'],mesh['file_id'],tuple((m.get('guid'),m.get('file_id'),m.get('is_null')) for m in materials))
   group=groups.setdefault(key,dict(case_id=f'L03.BINDINGS.{len(groups)+1:02d}',source_mesh_identity=mesh,original_material_slots=materials,source_submesh_count=binding['source_submesh_count'],occurrences=[],geometry_receipt=str(geometry),qualification='open'))
   assert group['source_submesh_count']==binding['source_submesh_count']
   occurrence=dict(asset_id=prefab['asset_id'],node_path=binding['node_path'],renderer_identity=binding['renderer_identity']);group['occurrences'].append(occurrence);rows.append(occurrence)
 assert count==expected,(directory,count)
assert len(rows)==37
pins[str(Path(__file__))]=sha(Path(__file__))
OUT.mkdir();result=dict(status='all_retained_townsmith_draw_mapping_refusals_grouped',finished_utc=datetime.now(timezone.utc).isoformat(),source_occurrences=37,distinct_cases=len(groups),distinct_geometry_receipts=len(geometry_seen),input_pins=pins,rehashed_pins=len(pins)-1,cases=list(groups.values()),material_render_qualified=False,studio_verified=False,full_import=False,complete_assets_delivered_increment=0)
(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
lines=['# Lots des affectations Townsmith restantes','', '37 occurrences regroupees par identite exacte de mesh et liste ordonnee de materiaux. Aucun refus leve par ce plan.','', '| Cas | Mesh source | Sous-meshes | Slots | Occurrences | Etat |','|---|---|---:|---:|---:|---|']
for group in groups.values():lines.append(f"| {group['case_id']} | {group['source_mesh_identity']['name']} | {group['source_submesh_count']} | {len(group['original_material_slots'])} | {len(group['occurrences'])} | A qualifier |")
lines+=['','Chaque cas requiert la capture Unity originale, le mapping de dessin natif, la comparaison des materiaux, puis les prefabs associes dans Studio. Les objets inactifs et LOD sont conserves.','', '[Identites, occurrences et SHA](RESULT.v1.json)']
(OUT/'LOTS.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','cases']}))
