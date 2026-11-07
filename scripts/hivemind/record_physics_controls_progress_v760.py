"""Record compilation and a verified live native test; preserve previous tracking."""
from pathlib import Path
import datetime, json, subprocess

W=Path('F:/OpenRPG_ImportWork');O=W/'scene-physics-controls-progress-v760'
assert not O.exists()
compiled=json.loads((W/'studio-physics-controls-check-v758/RESULT.v1.json').read_text('utf-8'))
assert compiled['exit_code']==0 and not compiled['changed_inputs']
inventory=json.loads((W/'library-current-state-inventory-v759/RESULT.v1.json').read_text('utf-8'))
native=W/'scene-physics-controls-tests-v757'
if (native/'RESULT.v1.json').exists():
    test=json.loads((native/'RESULT.v1.json').read_text('utf-8'))
    native_state='terminé : '+test['status']
else:
    job=json.loads((native/'JOB.v1.json').read_text('utf-8'))
    pid=int(job['pid'])
    cmd=f'Get-CimInstance Win32_Process -Filter "ProcessId = {pid}" | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Compress'
    run=subprocess.run(['powershell.exe','-NoProfile','-Command',cmd],capture_output=True,text=True,check=True)
    test=json.loads(run.stdout)
    assert test['ProcessId']==pid and test['Name']=='cargo.exe'
    assert 'authoring_physics_request::tests' in test['CommandLine']
    native_state=f'en cours, cargo PID {pid} vérifié'
O.mkdir()
result=dict(status='physics_controls_implemented_wasm_compile_verified',wasm_check=compiled,
    native_test_observation=test, inventory=inventory,
    actual_studio_gui_verified=False, placement_reopening_verified=False,
    full_import_increment=0,utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
summary=f'''## Interface des réglages physiques de scène (v760)

- Onglet Projet : panneau Collisions de la scène ajouté au Studio. Choix explicite de la matrice, sélection parmi 32 couches et interactions dans les deux sens, épaisseur cible en mètres, origine/justification. Aucun paramètre implicite pour une scène sans réglages acceptés.
- Examiner et Examiner le retrait préparent les mêmes commandes PreviewZone/prepare_physics que la tête canonique. Appliquer puis Sauvegarder restent requis ; les modifications du brouillon annulent la proposition précédente. Le libellé de revue distingue le changement physique d'un changement de terrain. Les valeurs acceptées sont relues lors d'un changement de zone ou de réglage.
- Compilation Studio WebGPU v758 PASS sur les sources finales, entrées épinglées inchangées. Les tests natifs du helper réel (saisies invalides, symétrie/bornes des couches, pins et retouches, absence d'application avant revue) sont {native_state}. Log : F:/OpenRPG_ImportWork/scene-physics-controls-tests-v757/cargo.log. Ne pas relancer le même contrôle sur un timeout.
- Inventaire v759 relu et hashes des métadonnées contrôlés : {inventory['model_cards']} fiches modèle, {inventory['source_cards']} sources conservées, {inventory['total_unique_card_ids']} IDs uniques, {inventory['source_cards_missing_primary_preview']} images principales manquantes. Nombre d'objets logiques complet toujours inconnu ; déclarations de readiness ne valent pas qualification Studio/runtime.
- Preuve : F:/OpenRPG_ImportWork/scene-physics-controls-progress-v760/RESULT.v1.json. Contrôles de persistance/contact v755 conservés. Usage réel du formulaire, placements physiques sauvegardés, rendu et simulation restent ouverts. Aucun nouvel asset entièrement qualifié, incrément full-import 0.
- Suite : terminer les tests natifs sans modifier leurs entrées, puis épingler les paquets physiques dans les placements et les relire via les snapshots historiques de la bibliothèque ; montage sur le même host et les mêmes objets éditables. Poursuivre les conversions/effets/images et les vérifications réelles de l'éditeur. Objectif complet actif.

'''
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name
    original=p.read_bytes();(O/name).write_bytes(original)
    p.write_text(summary+original.decode('utf-8-sig'),'utf-8')
print(json.dumps(dict(status=result['status'],native_test=native_state,missing_images=inventory['source_cards_missing_primary_preview'])))
