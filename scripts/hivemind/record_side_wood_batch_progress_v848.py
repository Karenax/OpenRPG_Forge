"""Document the next batch only from live processes and retained evidence."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, subprocess

W = Path('F:/OpenRPG_ImportWork')
O = W/'pirate-side-wood-batch-progress-v848'
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
assert not O.exists()
command = "@(Get-CimInstance Win32_Process | Where-Object { ($_.Name -eq 'python.exe' -and $_.CommandLine -match 'run_pirate_wood_side_held_lods_v845.py') -or $_.Name -eq 'Unity.exe' } | Select-Object ProcessId,Name,CommandLine) | ConvertTo-Json -Compress"
raw = subprocess.check_output([PS, '-NoProfile', '-Command', command], text=True)
processes = json.loads(raw)
assert processes, 'No live preparation/controller/Unity process'
if isinstance(processes, dict):
    processes = [processes]

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

frontier_path = W/'pirate-wood-side-held-frontier-v845/RESULT.v1.json'
diff_path = W/'pirate-side-wood-serialization-v846/RESULT.v1.json'
frontier = json.loads(frontier_path.read_text('utf-8-sig'))
diff = json.loads(diff_path.read_text('utf-8-sig'))
assert diff['status'] == 'wood01_entire_private_material_diff_explained'
assert len(frontier['held_groups']) == 2
O.mkdir()
result = dict(status='side_wood_batch_preparation_or_native_capture_live', live_processes=processes,
              groups=2, lod_cases=6, planned_candidate_cards=4, planned_candidate_views=8,
              material_diff_explained=True, native_capture_complete=False,
              images_admitted=0, complete_conversion_increment=0, full_import=False,
              input_pins={str(p):sha(p) for p in [Path(__file__),frontier_path,diff_path]},
              observed_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
header = ('## Lot bois panneaux lateraux en cours - v845 a v849\n\n'
          'ShackSide6m_03 et ShackSide8m_03 : six LOD exacts, deux controles LOD0, '
          'quatre fiches candidates et huit vues. Preparation/capture confirmee par processus vivant. '
          'Helper Unity V819 reutilise a hash identique ; aucun nouveau script dans le projet prive. '
          'Diff integral Wood01 explique (v846), verification native des valeurs par defaut et '
          'comparaison des rendus encore attendues. Aucun nouvel asset declare converti ni image admise. '
          'Dernier lot livre v843 : quatre fiches et huit vues ; 566 images principales restent manquantes. '
          'Preuves : F:/OpenRPG_ImportWork/pirate-side-wood-batch-progress-v848/RESULT.v1.json.\n\n')
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    path = W/'SUIVI_LOTS_HIVEMIND'/name
    before = path.read_bytes()
    (O/(name+'.before')).write_bytes(before)
    path.write_text(header + before.decode('utf-8-sig'), 'utf-8')
print(json.dumps({k:v for k,v in result.items() if k != 'input_pins'}))
