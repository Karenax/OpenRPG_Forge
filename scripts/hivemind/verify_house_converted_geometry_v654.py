"""Independently compare converted attributes/triangles to native Unity buffers."""
from pathlib import Path
from datetime import datetime,timezone
import base64,hashlib,json,struct,math
W=Path('F:/OpenRPG_ImportWork');N=W/'house-native-prefab-geometry-v651';C=W/'house-converted-shared-native-geometry-v653';O=W/'house-geometry-checkpoint-v654'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists();proof=read(C/'RESULT.v1.json');native=read(N/'NATIVE-RESULT.v1.json');pins={}
for key in ['input_pins','output_pins']:
    for p,h in proof[key].items():assert sha(p)==h,p;pins[p]=h
by_case={c['prefab_guid']:c for c in native['cases']};rows=[];values=0;triangles=0
for c in proof['cases']:
    source=by_case[c['prefab_guid']];assert c['source_nodes']==source['nodes'] and c['source_bindings']==source['bindings'] and c['unassigned_mesh_colliders']==source['unassigned_mesh_colliders'] and c['source_component_snapshots']==source['component_snapshots']
    by_id={(m['identity']['guid'],m['identity']['file_id']):m for m in source['meshes']}
    for m in c['meshes']:
        raw=by_id[(m['identity']['guid'],m['identity']['file_id'])];streams={s['index']:base64.b64decode(s['bytes_base64'],validate=True) for s in raw['streams']};out={s['index']:Path(s['path']).read_bytes() for s in m['streams']};strides={s['index']:s['stride'] for s in raw['streams']};assert len(streams)==len(out)
        for s in out:assert len(out[s])==len(streams[s])
        for a in raw['attributes']:
            assert a['format']=='Float32';d=a['dimension'];s=a['stream']
            for i in range(raw['vertices']):
                offset=i*strides[s]+a['offset'];v=list(struct.unpack_from('<'+'f'*d,streams[s],offset));assert all(math.isfinite(x) for x in v)
                if a['semantic'] in ['Position','Normal','Tangent']:v[2]=-v[2]
                if a['semantic']=='Tangent':v[3]=-v[3]
                assert out[s][offset:offset+d*4]==struct.pack('<'+'f'*d,*v);values+=d
        ix=base64.b64decode(raw['index_bytes_base64'],validate=True);fmt={'UInt16':'H','UInt32':'I'}[raw['index_format']];indices=struct.unpack('<'+fmt*(len(ix)//struct.calcsize(fmt)),ix)
        for sub,p in zip(raw['submeshes'],m['primitives'],strict=True):
            data=Path(p['index_path']).read_bytes();target=struct.unpack('<'+'I'*(len(data)//4),data);original=indices[sub['index_start']:sub['index_start']+sub['index_count']]
            for j in range(0,len(original),3):assert target[j:j+3]==tuple(original[j+k]+sub['base_vertex'] for k in [0,2,1]);triangles+=1
        expected=[b for b in source['bindings'] if (b['mesh']['guid'],b['mesh']['file_id'])==(raw['identity']['guid'],raw['identity']['file_id'])];assert m['source_mesh_usages']==expected
        rows.append(dict(prefab_guid=c['prefab_guid'],mesh=raw['identity'],attributes_verified=True,indices_and_winding_verified=True,all_source_mesh_usages_preserved=True))
assert len(rows)==42 and triangles==proof['triangles']
for p in [Path(__file__),C/'RESULT.v1.json',N/'NATIVE-RESULT.v1.json',N/'RUN-RESULT.v1.json']:pins[str(p)]=sha(p)
O.mkdir();r=dict(status='house_twentyfour_prefab_native_geometry_independently_verified',utc=datetime.now(timezone.utc).isoformat(),lot='L03.HOUSE.MAIN.01',prefab_cards=24,meshes=42,attribute_components_compared=values,triangles_compared=triangles,render_mesh_bindings=proof['render_mesh_bindings'],collider_mesh_bindings=proof['collider_mesh_bindings'],unassigned_mesh_colliders=proof['unassigned_mesh_colliders'],rows=rows,input_pins=pins,source_nodes_bindings_and_components_preserved=True,source_inputs_unchanged=True,autonomous_glb_written=False,forge_render_verified=False,studio_verified=False,full_import=False,complete_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2),'utf-8')
header=f'''## House — géométrie des 24 prefabs convertie et vérifiée (v654)

- 42 meshes natifs référencés : {proof['vertex_count']:,} sommets, {triangles:,} triangles. Attributs Float32, UV0/UV1, normales, tangentes et indices comparés indépendamment après conversion de repère.
- 24 bindings de rendu, 19 bindings de collision, hiérarchies et sérialisations de composants conservés. Deux MeshColliders sans mesh explicitement préservés ; aucun collider inventé.
- Le catalogue v650 confirme que les références de rendu sont valides. L'échec v648 provenait d'un collider vide ; les noms provisoires de l'ancienne file d'import ne constituent pas une preuve de référence manquante.
- Six matériaux / textures BC déjà comparés sur GPU (v647). Prochaine tranche : shaders hôtes communs, profondeur POM, assemblage des GLB, rendus des 24 prefabs et accès Studio.
- Dérivés et preuves sur F:/OpenRPG_ImportWork/house-converted-shared-native-geometry-v653. Pas encore de GLB autonome ni de qualification complète. **Incrément complet : 0**.

'''
(O/'LOT.md').write_text(header,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_text(header+before.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:r[k] for k in ['status','prefab_cards','meshes','attribute_components_compared','triangles_compared','complete_conversion_increment']}))
