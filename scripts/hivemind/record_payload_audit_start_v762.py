"""Keep a restart-safe handoff for the full current model SHA audit."""
from pathlib import Path
import datetime, hashlib, json, subprocess
W=Path('F:/OpenRPG_ImportWork');O=W/'model-payload-audit-handoff-v762'
assert not O.exists()
audit=W/'current-model-payload-integrity-v761'
job=json.loads((audit/'JOB.v1.json').read_text('utf-8'))
progress=json.loads((audit/'PROGRESS.v1.json').read_text('utf-8'))
pid=int(job['pid'])
cmd=f'Get-CimInstance Win32_Process -Filter "ProcessId = {pid}" | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Compress'
run=subprocess.run(['powershell.exe','-NoProfile','-Command',cmd],capture_output=True,text=True,check=True)
process=json.loads(run.stdout)
assert process['ProcessId']==pid and 'audit_current_model_payloads_v761.py' in process['CommandLine']
O.mkdir()
source=Path(job['script']).read_bytes();(O/'audit_current_model_payloads_v761.py').write_bytes(source)
receipt=dict(status='current_model_integrity_audit_running',job=job,verified_process=process,observed_progress=progress,script_sha256_captured_after_start=hashlib.sha256(source).hexdigest(),utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),full_import_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
summary=f'''## Vérification intégrale des empreintes des modèles courants lancée (v761-v762)

- Audit v761 en lecture seule : 14 219 fiches, {progress['total_payloads']} références uniques fichier/empreinte. Relit les modèles du registre actuel, vérifie SHA-256 et confinement sur F:/OpenRPG_AssetVault, détecte une modification pendant lecture. Métadonnées épinglées avant le contrôle et revérifiées après. Aucune reconversion ni suppression.
- Processus Python PID {pid} confirmé actif. Dernier relevé : {progress['checked_payloads']} fichiers lus, {progress['passed']} empreintes conformes, {progress['failed']} échec(s), {progress['bytes_read']} octets lus. Ce relevé n'est pas le résultat final. Journal détaillé append-only OBSERVATIONS.v1.jsonl, compteur PROGRESS.v1.json et verdict attendu RESULT.v1.json sous F:/OpenRPG_ImportWork/current-model-payload-integrity-v761.
- Ne pas relancer un deuxième audit sur un timeout ou un compteur stable : vérifier le PID et le verdict. Les fichiers absents, chemins hors Vault et empreintes différentes sont conservés comme échecs exploitables dans le journal ; le contrôle poursuit les autres références.
- Portée exacte : modèles actuellement indexés. Cet audit ne prouve pas à lui seul le transfert intégral des originaux/licences, la sémantique glTF ni l'utilisation dans Studio. Qualification globale inchangée, incrément full-import 0.
- Test natif du formulaire v757 encore à observer (cargo PID 42384, session 54254). Ses entrées restent intactes ; compilation WebGPU du formulaire PASS v758. Suite après son résultat : lecture historique des paquets physiques et références de placements communes. Les 570 images restantes, autres conversions/effets et validation réelle Studio restent ouverts. Objectif complet actif.

'''
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name
    original=p.read_bytes();(O/name).write_bytes(original)
    p.write_text(summary+original.decode('utf-8-sig'),'utf-8')
print(json.dumps(dict(pid=pid,progress=progress)))
