"""Retain an existing native test executable receipt without promoting full import."""
import base64
import datetime
import json
import sys
from pathlib import Path

report = json.loads(base64.b64decode(sys.argv[1]))
assert report['schema'] == 'openrpg.bc4-existing-compiled-test-executable.v1'
assert report['exit_code'] == 0 and report['tests_passed'] == 4
assert report['hashes_before'] == report['hashes_after']
assert not report['fresh_engine_build_claimed'] and not report['web_check_verified']
assert not report['studio_verified'] and not report['full_import']
assert 'test result: ok. 4 passed; 0 failed;' in report['stdout']
work = Path('F:/OpenRPG_ImportWork')
receipt = work / 'canonical-bc4-imported-texture-candidate-v2/DIRECT-EXISTING-EXECUTABLE-REOPEN.v1.json'
with receipt.open('x', encoding='utf-8') as output:
    json.dump(report, output, indent=2)
log = receipt.parent / 'native-storage-tests.direct-existing.v1.log'
with log.open('x', encoding='utf-8') as output:
    output.write(report['stdout'])
tracking = work / 'SUIVI_LOTS_HIVEMIND'
state_path = tracking / 'ETAT.v1.json'
state = json.loads(state_path.read_text(encoding='utf-8-sig'))
state['updated_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
batch = next(batch for batch in state['batches'] if batch['id'] == 'L01')
batch['bc4_existing_compiled_executable_tests'] = {
    'receipt': str(receipt), 'tests_passed': 4, 'exit_code': 0,
    'hashes_before': report['hashes_before'], 'hashes_after': report['hashes_after'],
    'current_native_web_gate_closed': False, 'full_import': False,
}
state_path.write_text(json.dumps(state, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
entry = ('\n\n## ' + state['updated_utc'] + ' - L01 : quatre tests BC4 du binaire existant passes\n'
    'Tests reels 4 passed/0 failed/exit0 sur executable forge_engine-f18f10d953019132.exe. '
    'SHA source/binaire avant/apres identiques, stdout exact et recu conserves. '
    'Aucun freshbuild/Web/pariteGPU/Studio revendique. Controleur41640 maintient validation courante,8380 attend garde. '
    'Placement/preview Studio utilisent GltfAssetLabel::Scene(0), raccordement SourceGraph commun restant. '
    'Ouverture Studio expiree, aucune fenetre observee ; seules requetes de lecture retirees, aucune conversion stoppee. '
    'Aucun nouvel asset livre.\n')
for journal in (tracking / 'JOURNAL.md', work / 'RUN_STATUS.md'):
    with journal.open('a', encoding='utf-8') as output:
        output.write(entry)
print(json.dumps({'tests_passed': 4, 'receipt': str(receipt), 'full_import': False}))
