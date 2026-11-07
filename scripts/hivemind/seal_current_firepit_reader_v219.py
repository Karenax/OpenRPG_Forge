"""Archive the current checker and restore the executable retained by old proofs."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,shutil
W=Path('F:/OpenRPG_ImportWork');O=W/'firepit-current-reader-reopen-v219';CHECK=W/'firepit-current-product-reader-v218'
OLD=W/'firepit-static-glb-product-check-v2';EXE=OLD/'target/debug/firepit-static-glb-product-check.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
run=read(CHECK/'RUN-RESULT.v1.json');assert run['status']=='current_product_firepit_glb_reader_and_integrity_controls_passed'
assert run['cargo_exit_code']==run['checker_exit_code']==0 and run['source_inputs_unchanged']
for p,h in run['input_pins'].items():assert sha(p)==h,p
assert sha(CHECK/'checker.stdout.log')==run['result_sha256']
result=run['result'];assert result['nodes']==9 and result['unique_mesh_primitives']==4 and result['material_contracts']==1
assert result['header_negative_control_passed'] and result['shader_integrity_negative_control_passed']
assert sha(Path(result['glb']))==result['sha256']
prior=CHECK/'previous-checker.exe';historical=read(OLD/'RESULT.v1.json');assert sha(prior)==historical['checker_sha256']
assert sha(EXE)==run['checker_sha256']
O.mkdir();current=O/'current-product-checker.exe';shutil.copyfile(EXE,current);assert sha(current)==run['checker_sha256']
# The owned controller has terminated. Preserve both versions; cache restoration
# does not remove either executable or any source/build evidence.
shutil.copyfile(prior,EXE);assert sha(EXE)==historical['checker_sha256']
pins=dict(run['input_pins'])
for p in [Path(__file__),CHECK/'RUN-RESULT.v1.json',CHECK/'checker.stdout.log',CHECK/'checker.stderr.log',CHECK/'cargo.log',prior,current,EXE,OLD/'RESULT.v1.json']:
    pins[str(p)]=sha(p)
receipt=dict(status='current_product_firepit_admission_reopened_and_both_checker_versions_preserved',
    nodes=9,mesh_primitives=4,material_contracts=1,glb_sha256=result['sha256'],
    header_refusal_verified=True,shader_integrity_refusal_verified=True,source_inputs_unchanged=True,
    current_checker=str(current),historical_checker_restored=True,actual_current_product_reader_passed=True,
    gpu_rendered=False,studio_verified=False,lod_runtime_qualified=False,effects_runtime_qualified=False,
    full_import=False,input_pins=pins,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
note='''## Foyer L05 — admission par le lecteur produit actuel validée

**v218/v219 PASS sur les sources actuelles épinglées de forge_assets/forge_core.** Compilation Cargo offline/locked réussie, puis exécution du vrai `read_source_graph_glb` sur le foyer v3 : neuf nœuds, quatre primitives de mesh et un contrat matériau acceptés. En-tête invalide et shader avec un octet corrompu refusés. Sources et GLB rehashés après exécution ; reçu réouvert indépendamment.

L'exécutable actuel est conservé dans la preuve v219 et l'ancien exécutable est rétabli à son chemin de cache, avec empreintes vérifiées, afin de préserver les preuves historiques. Le foyer reste sur F: dans ImportWork ; aucune indexation comme asset complet ni nouvelle preuve de rendu GPU acquise.

Cette admission qualifie la lecture du dérivé statique actuel. LOD/états actifs, deux renderers d'effets, rendu/fidélité Unity-Forge, placement et GUI Studio restent ouverts. Prochaine étape : exécuter le foyer par le chargement/rendu commun et vérifier les comportements et effets conservés, en poursuivant les lots d'images sans refaire ceux livrés.

[Admission réouverte](../firepit-current-reader-reopen-v219/RESULT.v1.json) · [Build et exécution courants](../firepit-current-product-reader-v218/RUN-RESULT.v1.json) · [Géométrie source](../firepit-complete-static-glb-v3/INDEPENDENT-REOPEN.v1.json).

'''
for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;text=p.read_text('utf-8-sig');(O/(name+'.before')).write_text(text,'utf-8');p.write_text(note+text,'utf-8')
print(json.dumps({k:v for k,v in receipt.items() if k!='input_pins'}))
