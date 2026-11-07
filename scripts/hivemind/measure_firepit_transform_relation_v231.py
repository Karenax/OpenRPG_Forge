"""Measure center versus corner transforms from the paired native acquisition."""
from pathlib import Path
import datetime,hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');I=W/'firepit-particle-transform-native-v230';O=W/'firepit-particle-transform-relation-v231'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
state=read(I/'RUN-RESULT.v1.json');assert state['unity_exit_code']==0 and state['native_geometry_verified'] and state['both_native_transform_modes_captured']
assert state['prior_336_geometry_observations_unchanged'] and state['prior_112_particle_frames_unchanged']
receipt=I/'INDEPENDENT-REOPEN.v1.json';assert sha(receipt)==state['independent_receipt_sha256']
reference=read(receipt);capture=I/'GEOMETRY.v1.json';assert sha(capture)==reference['capture_sha256']
report=read(capture);errors={k:0. for k in ['identity','linear_rotation_scale','full_transform','rotation_only','quad_center_linear_transform','quad_offset_unchanged']};rows=[];vertices=0
for case in report['cases']:
    for row in case['rows']:
        for g in row['geometry']:
            if not g['vertices']:continue
            p=np.array([[v[k] for k in 'xyz'] for v in g['positions']],dtype=np.float64)
            t=np.array([[v[k] for k in 'xyz'] for v in g['with_transform_positions']],dtype=np.float64)
            m=np.array([[g['renderer_local_to_world'][f'e{i}{j}'] for j in range(4)] for i in range(4)])
            a=m[:3,:3];scale=np.linalg.norm(a,axis=0);assert np.all(scale>0)
            candidates=dict(identity=p,linear_rotation_scale=p@a.T,full_transform=p@a.T+m[:3,3],rotation_only=p@(a/scale).T)
            pc=p.reshape(-1,4,3).mean(axis=1);tc=t.reshape(-1,4,3).mean(axis=1)
            local={k:float(np.max(np.abs(v-t))) for k,v in candidates.items()}
            local['quad_center_linear_transform']=float(np.max(np.abs(tc-pc@a.T)))
            local['quad_offset_unchanged']=float(np.max(np.abs((t.reshape(-1,4,3)-tc[:,None,:])-(p.reshape(-1,4,3)-pc[:,None,:]))))
            for k,v in local.items():errors[k]=max(errors[k],v)
            vertices+=len(p);rows.append(dict(node_path=case['node_path'],seed=row['seed'],requested_time=row['requested_time'],repetition=row['repetition'],fixture=g['fixture'],vertices=len(p),maximum_absolute_errors=local))
assert vertices==30144
pins={str(p):sha(p) for p in [Path(__file__),capture,receipt,I/'RUN-RESULT.v1.json',I/'TRANSFORM-MEASUREMENTS.v1.json',I/'PRIVATE-BENCH-DRIFT.v1.json']}
O.mkdir();result=dict(status='native_particle_center_and_billboard_offset_transform_relation_measured',input_pins=pins,nonempty_observations=len(rows),vertex_observations=vertices,maximum_absolute_errors=errors,rows=rows,prior_states_and_false_mode_geometry_unchanged=True,naive_full_mesh_transform_matches=False,source_world_translation_transport_verified=False,coordinate_mapping_to_forge_qualified=False,autonomous_runtime_qualified=False,gpu_rendered=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
block='''## Effets du foyer L06 — deux modes de transformation natifs mesurés (v230/v231)

Unity terminé avec code 0. **112 états et 336 géométries de référence inchangés**, couleurs/indices identiques entre modes ; 30 144 observations de sommets. Reçus et capture rehashés.

Une transformation globale du mesh ne reproduit pas le mode natif : erreur maximale 0,157928 pour rotation/échelle globale, 0,364217 avec translation globale. Les centres suivent la transformation linéaire (écart maximal 2,88511e-8) tandis que les offsets des coins restent inchangés (écart maximal 7,45058e-8). Mesures conservées sans seuil de qualification ajouté. Éviter la double échelle ; prochaine tranche : valider le transport de translation/mode de simulation puis convertir la construction des billboards et couleurs, raccorder textures et rendu d'effets au chargeur commun.

**Pas encore de simulation autonome, coordonnées Forge finales, rendu d'effets ou Studio qualifiés.** Le rendu statique v227 reste acquis. Sources et anciennes captures conservées ; incident de reçu restauré exactement, refus et évolution du banc privé documentés.

[Mesures](../firepit-particle-transform-relation-v231/RESULT.v1.json) · [Acquisition native](../firepit-particle-transform-native-v230/RUN-RESULT.v1.json).

'''
for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;old=p.read_text('utf-8-sig');(O/('BEFORE-'+name)).write_text(old,'utf-8');p.write_text(block+old,'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['rows','input_pins']}))
