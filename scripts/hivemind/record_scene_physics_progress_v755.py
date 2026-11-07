"""Record actual persistence/contact results without upgrading import readiness."""
from pathlib import Path
import datetime, hashlib, json, re

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W / 'scene-physics-progress-v755'
assert not O.exists()

def read_result(directory):
    p = W / directory / 'RESULT.v1.json'
    value = json.loads(p.read_text('utf-8'))
    assert value['exit_code'] == 0
    assert not value.get('changed_inputs', [])
    assert value.get('unchanged_input_pins', True)
    return value

results = {name: read_result(name) for name in [
    'scene-physics-project-regressions-v751',
    'source-physics-contact-fix-v752',
    'studio-physics-package-check-v753',
    'source-physics-contacts-v754',
]}
log = (W / 'scene-physics-project-regressions-v751/cargo.log').read_text('utf-8')
counts = re.findall(r'test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored', log)
assert counts and all(int(failed) == 0 for _, failed, _ in counts)
passed = sum(int(passed) for passed, _, _ in counts)
ignored = sum(int(ignored) for _, _, ignored in counts)
contact_log = (W / 'source-physics-contacts-v754/cargo.log').read_text('utf-8')
contacts = [json.loads(line) for line in contact_log.splitlines() if line.startswith('{')][-1]
assert contacts['carriers'] == 24 and contacts['contact_policies'] == 22
assert contacts['empty_references'] == 2 and contacts['negative_controls'] == 226
assert contacts['source_offset_not_used_as_target_skin']
O.mkdir()
receipt = dict(status='scene_physics_persistence_and_contact_checks_verified', project_tests_passed=passed,
    project_tests_ignored=ignored, regression_suites=len(counts), source_checks=results, contacts=contacts,
    scene_head_version=15, archive_versions=[40, 41, 42],
    studio_visual_verified=False, placement_reopening_verified=False,
    native_simulation_qualified=False, full_import_increment=0,
    utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O / 'RESULT.v1.json').write_text(json.dumps(receipt, indent=2), 'utf-8')
summary = f'''## Réglages physiques persistants et contrôles terminés (v755)

- Les paramètres explicites de scène sont conservés dans la tête d'édition existante : ZoneCorrections.physics, tête v15, archives normale/compacte/delta v40/41/42. Champ absent omis ; anciens formats conservés. Validation, réouverture, annulation/rétablissement et retouches vérifiés par les deux tests zone_physics puis la suite projet complète.
- Suite projet v751 PASS : {passed} tests réussis, {ignored} ignoré(s), {len(counts)} suites, entrées épinglées inchangées. Studio projette la tête acceptée vers la ressource physique et invalide l'aperçu lors d'un changement ; saisie depuis l'interface encore à réaliser.
- Clippy moteur v744 est TERMINÉ EN ÉCHEC (deux comparaisons flottantes). Correction exacte par comparaison des bits ; source et log avant correction conservés sous source-physics-contact-fix-v752. Clippy réel v752 PASS, aucun changement des entrées. Contrôle de format du module PASS.
- Banc v754 PASS sur les 24 carriers réels : 22 politiques de contact, 2 références vides, 226 refus contrôlés. Décalage de contact source conservé, non utilisé comme épaisseur cible. Compilation réelle Studio WASM v753 PASS après la correction ; v750 PASS conservé avant celle-ci.
- Preuve consolidée : F:/OpenRPG_ImportWork/scene-physics-progress-v755/RESULT.v1.json. Pas de qualification de simulation Unity, rendu ou GUI Studio déduite ; placements avec paquet physique toujours refusés tant que leur sauvegarde/réouverture n'est pas raccordée.
- Suite : saisie des paramètres via commandes du produit, références physiques persistantes et montage sur placements communs, admission du rendu et validation réelle de l'éditeur ; poursuivre les autres conversions/effets et les 570 images principales manquantes du dernier inventaire. Aucun nouvel asset entièrement qualifié : incrément full-import 0. Objectif complet actif, tous les travaux et preuves sur F:.

'''
tracking = W / 'SUIVI_LOTS_HIVEMIND'
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    p = tracking / name
    original = p.read_bytes()
    (O / name).write_bytes(original)
    p.write_text(summary + original.decode('utf-8-sig'), 'utf-8')
print(json.dumps({k: v for k, v in receipt.items() if k != 'source_checks'}))
