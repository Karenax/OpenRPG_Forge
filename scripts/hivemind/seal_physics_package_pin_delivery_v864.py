"""Seal current package pin checks, preserving their actual product limitations."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W/'physics-package-pin-delivery-v864'
assert not O.exists()

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

checks = {
    'physics-profile-pin-tests-v859': 'full_physics_package_substitution_refused',
    'physics-package-pin-native-v858': 'studio_full_physics_package_pin_clippy_verified',
    'physics-profile-pin-wasm-v860': 'studio_wasm_compile_verified',
}
pins = {str(Path(__file__)):sha(Path(__file__))}
receipts = {}
for root, status in checks.items():
    path = W/root/'RESULT.v1.json'
    result = json.loads(path.read_text('utf-8-sig'))
    assert result['exit_code'] == 0 and result['status'] == status
    assert result['changed_inputs'] == []
    input_path = path.parent/'INPUT-PINS.v1.json'
    inputs = json.loads(input_path.read_text('utf-8-sig'))
    for file, digest in inputs.items():
        assert file not in pins or pins[file] == digest
        pins[file] = digest
    for file in [path,input_path,path.parent/'cargo.log']:
        pins[str(file)] = sha(file)
    receipts[root] = dict(status=status,result_sha256=sha(path))
for path, digest in pins.items():
    assert sha(path) == digest, path
O.mkdir()
result = dict(status='complete_physics_package_pins_tests_native_clippy_and_wasm_verified',
              checks=receipts,input_pins=pins,current_inputs_rehashed=True,
              source_profiles_use_sha256=True,saved_requests_use_package_sha256=True,
              actual_studio_gui_verified=False,persistent_physics_mount_verified=False,
              runtime_qualified=False,full_import=False,complete_conversion_increment=0,
              finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
doc = R/'docs/planning/HIVEMIND_SAVED_PHYSICS_REFERENCE.md'
before = doc.read_bytes()
(O/'HIVEMIND_SAVED_PHYSICS_REFERENCE.before.md').write_bytes(before)
text = before.decode('utf-8-sig')
anchor = '## État et points de raccordement observés le 11 octobre\n\n'
assert text.count(anchor) == 1
paragraph = ('Lot v864 vérifié sur les sources courantes : le test refuse un autre paquet\n'
             'valide avec le même carrier/manifeste et des buffers différents ; Clippy\n'
             'Studio natif passe avec `-D warnings` ; Studio WebGPU compile. Les pins\n'
             'de chaque contrôle ont été réouverts et rehashés sans changement. La\n'
             'sélection, les détails, l’aperçu et la réouverture sauvegardée vérifient\n'
             'le SHA du paquet complet avant décodage. Preuve :\n'
             '`F:/OpenRPG_ImportWork/physics-package-pin-delivery-v864/RESULT.v1.json`.\n'
             'La compilation WebGPU conserve ses warnings ; aucune qualification de GUI,\n'
             'montage persistant, simulation ou import complet n’est déduite de ce lot.\n\n')
text = text.replace('Lot v855-v860 en contrôle :', 'Relevé antérieur v855-v860, avant clôture technique du lot v864 :')
doc.write_text(text.replace(anchor,anchor+paragraph),'utf-8')
header = ('## Lot pins physiques verifie - v855 a v864\n\n'
          'Paquet complet verifie dans les parcours de selection, details, apercu et reouverture. '
          'Test de substitution, Clippy natif et compilation WebGPU passent sur les sources courantes, '
          'pins rehashes sans changement. Montage persistant, simulation et GUI restent non qualifies. '
          'Dernier lot images livre : quatre fiches/huit vues v854 ; 562 images principales manquantes. '
          '47 candidats Mountain ne peuvent pas reutiliser les vignettes URP : dependances differentes, '
          'preuve v863. Increment entierement converti : 0. '
          'Preuve : F:/OpenRPG_ImportWork/physics-package-pin-delivery-v864/RESULT.v1.json.\n\n')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    path = W/'SUIVI_LOTS_HIVEMIND'/name
    raw = path.read_bytes()
    (O/(name+'.before')).write_bytes(raw)
    path.write_text(header+raw.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','checks']}))
