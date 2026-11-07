"""Record verified controls and the saved-reader implementation still under test."""
from pathlib import Path
import datetime, json, subprocess
W=Path('F:/OpenRPG_ImportWork');O=W/'saved-physics-reader-progress-v768'
assert not O.exists()
controls=json.loads((W/'scene-physics-controls-tests-v757/RESULT.v1.json').read_text('utf-8'))
assert controls['exit_code']==0 and not controls['changed_inputs']
observations={}
for folder in ['saved-physics-reference-regressions-v765','saved-physics-native-clippy-v767','saved-physics-reader-v766']:
    root=W/folder
    if (root/'RESULT.v1.json').exists():
        observations[folder]=json.loads((root/'RESULT.v1.json').read_text('utf-8'))
    elif (root/'JOB.v1.json').exists():
        job=json.loads((root/'JOB.v1.json').read_text('utf-8'));pid=int(job['pid'])
        cmd=f'Get-CimInstance Win32_Process -Filter "ProcessId = {pid}" | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Compress'
        out=subprocess.run(['powershell.exe','-NoProfile','-Command',cmd],capture_output=True,text=True,check=True).stdout
        observations[folder]=dict(job=job,process=json.loads(out) if out.strip() else None)
    else:
        cmd='Get-CimInstance Win32_Process | Where-Object { $_.Name -eq "python.exe" -and $_.CommandLine -match "verify_saved_physics_reader_v766.py" } | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Compress'
        out=subprocess.run(['powershell.exe','-NoProfile','-Command',cmd],capture_output=True,text=True,check=True).stdout
        observations[folder]=dict(preparation_process=json.loads(out) if out.strip() else None)
O.mkdir()
result=dict(status='saved_physics_reader_implemented_under_verification',controls_test_result=controls,
    observed_jobs=observations,placement_reference_persistence_implemented=False,
    actual_studio_gui_verified=False,full_import_increment=0,utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
summary='''## Lecteur historique des paquets physiques ajouté, vérification en cours (v765-v768)

- Formulaire physique : test natif v757 TERMINÉ PASS, trois tests réussis, entrées épinglées inchangées. Saisies invalides, interactions symétriques/bornes, commande prévisualisée sans application et retouches conservées. Compilation WebGPU v758 conservée ; utilisation réelle GUI toujours non qualifiée.
- LibrarySavedPhysicsRequest ajouté : identité d'asset, SHA carrier/modèle, SHA manifeste et SHA paquet, sans chemin client. Dispatch sur le même IPC read_asset_physics ; requêtes courantes conservées. Le lecteur cherche le profil exact dans le snapshot courant puis l'historique épinglé, reprend licence/confinement/hash/transport et revérifie les registres avant réponse.
- La résolution historique de modèle réutilise maintenant la résolution d'asset ; une recherche physique distingue des profils différents pour un carrier identique. Aucun paquet découvert par nom, chemin de requête ou matrice implicite.
- Contrôles : suite native complète v765, Clippy bibliothèque et binaire Studio v767, banc privé v766 sur 24 carriers/paquets réels. Ce banc couvre référence courante puis historique après retrait de la déclaration physique du profil courant, recherche ne montrant que les cartes courantes, pins invalides et corruption. Il utilise une copie isolée ; ne pas déduire PASS avant les RESULT.v1.json.
- Preuve de reprise : F:/OpenRPG_ImportWork/saved-physics-reader-progress-v768/RESULT.v1.json contient les handles/processus observés. Sources avant changement conservées v764. Audit de modèles v761 toujours à observer via son propre PROGRESS/RESULT, sans relance sur timeout.
- Reste pour les placements : champ physique canonique et ses versions d'archive, pin depuis la sélection, lecture au chargement, montage sur le host et les objets d'édition communs, sauvegarde/réouverture dans Studio réel. Autres conversions/effets/images et qualification globale conservées dans l'objectif. Aucun nouvel asset entièrement qualifié ; incrément full-import 0, objectif actif.

'''
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name
    original=p.read_bytes();(O/name).write_bytes(original)
    p.write_text(summary+original.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='observed_jobs'}))
