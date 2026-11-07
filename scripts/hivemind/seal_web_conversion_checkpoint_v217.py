"""Seal reusable packaging and real source assembly proofs without runtime promotion."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'source-graph-conversion-checkpoint-v217'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
packaging_path=W/'source-graph-web-guard-converter-reopen-v215/RESULT.v1.json'
assembly_path=W/'firepit-web-assembly-controller-v216/RUN-RESULT.v1.json'
packaging=read(packaging_path);assembly=read(assembly_path)
assert packaging['exact_gpu_reference_glb_identity'] and len(packaging['negative_cases'])==5
assert assembly['status']=='firepit_source_assembly_with_shared_web_packager_completed'
pins=packaging['input_pins']|assembly['input_pins']|assembly['output_pins']
for p,h in pins.items():assert sha(p)==h,p
browser_prepared=W/'browser-source-glb-v183/PREPARED.v1.json';browser=read(browser_prepared)
gold=Path('F:/OpenRPG_ImportWork/townsmith-web-normal-guard-carrier-v182/SM_Door-Single-B.web-normal-guard.candidate.glb')
assert browser['input_pins'][str(gold)]==sha(gold)
reopen_path=W/'firepit-complete-static-glb-v3/INDEPENDENT-REOPEN.v1.json';reopen=read(reopen_path)
assert reopen['nodes_reopened']==9 and reopen['embedded_blobs_reopened']==17 and reopen['geometry_stream_instances_reopened']==40
assert len(reopen['negative_controls'])==6
assert reopen['glb_sha256']==assembly['scene_row']['sha256']
prior=Path(assembly['scene_row']['glb'].replace('firepit-complete-static-glb-v3','firepit-complete-static-glb-v2'))
assert sha(prior)==reopen['glb_sha256']
assert assembly['scene_row']['normal_guard_packaging']['unchanged']
for p in [Path(__file__),S/'SOURCE_GRAPH_WEB_CONVERSION.md',packaging_path,assembly_path,reopen_path,browser_prepared,prior,S/'reopen_firepit_static_glb_v3.py']:
    pins[str(p)]=sha(p)
O.mkdir()
result=dict(status='shared_web_normal_guard_converter_and_versioned_source_assembly_sealed',
    web_reference_exact_identity=True,packaging_negative_cases=5,source_assembly_negative_cases=6,
    firepit_nodes=9,firepit_geometry_stream_instances=40,firepit_embedded_blobs=17,
    firepit_v2_v3_byte_identity=True,new_complete_assets=0,new_gpu_execution=False,
    current_reader_qualification=False,source_render_equivalence=False,studio_verified=False,full_import=False,
    input_pins=pins,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Conversion L01/L05 — garde WebGPU réutilisable et recette foyer versionnée

**v214/v215 : convertisseur commun `normalize_source_graph_glb.py` vérifié.** Reproduit octet pour octet le GLB Townsmith réellement exécuté dans WebGPU v183 (empreinte également vérifiée dans les inputs de ce run), sans nouvelle exécution GPU. Relance identique ; cinq cas inconnus/corrompus refusés. Seules les gardes NaN des ABI normales reconnues sont dérivées ; calculs acceptés, géométrie, textures et autres métadonnées conservés.

**v216/v217 : intégration dans `assemble_firepit_glb_v3.py`, v2 conservée.** Assemblage réel sur F: réussi, dérivé identique au foyer v2 car aucune garde n'y exige de correction. Réouverture source indépendante : neuf nœuds, dix-sept blobs, quarante instances de flux géométriques et six mutations refusées. Les deux renderers d'effets, LOD et composants restent conservés en métadonnées, sans promotion en validation runtime.

**Aucun nouvel asset complet qualifié.** Le convertisseur est une étape de packaging, pas une validation du lecteur canonique ni du rendu source/Forge. La comparaison source/Forge, effets/LOD, placement et GUI Studio restent ouverts. Prochaine action : actualiser les contrôles du lecteur produit pour les assemblages versionnés et poursuivre la fidélité de rendu, en parallèle du lot d'images suivant lorsque les ressources le permettent.

[Preuves réouvertes](../source-graph-conversion-checkpoint-v217/RESULT.v1.json) · [Convertisseur et contrôles](../source-graph-web-guard-converter-reopen-v215/RESULT.v1.json) · [Assemblage foyer](../firepit-web-assembly-controller-v216/RUN-RESULT.v1.json) · [Géométrie/hiérarchie](../firepit-complete-static-glb-v3/INDEPENDENT-REOPEN.v1.json).

'''
for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;text=p.read_text('utf-8-sig');(O/(name+'.before')).write_text(text,'utf-8');p.write_text(note+text,'utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
