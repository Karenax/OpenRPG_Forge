"""Record real source/backend policy differences before enabling contacts."""
from pathlib import Path
import hashlib, json, re
from datetime import datetime, timezone

W = Path('F:/OpenRPG_ImportWork')
R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
O = W / 'gladiator-contact-policy-review-v479'
assert not O.exists()
native = W / 'gladiator-native-box-metric-v471/NATIVE-RESULT.v1.json'
proof = W / 'gladiator-native-box-delivery-v473/RESULT.v1.json'
backend = Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/bevy_rapier3d-0.36.0/src/geometry/collider.rs')
patched = R / 'third_party/rapier3d-0.35.0-glamx0.2/src/geometry/collider.rs'

def sha(p):
    with Path(p).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

sealed = json.loads(proof.read_text('utf-8-sig'))
assert sealed['status'] == 'three_native_box_query_sets_reopened'
assert sealed['input_pins'][str(native)] == sha(native)
data = json.loads(native.read_text('utf-8-sig'))
text = backend.read_text('utf-8')
match = re.search(r'impl Default for Friction\s*\{.*?coefficient:\s*([0-9.]+)', text, re.S)
assert match
friction_default = float(match.group(1))
assert len(data['boxes']) == 3
rows = []
for box in data['boxes']:
    material = box['implicit_material']
    assert material['original_reference_null']
    assert material['dynamic_friction'] == material['static_friction']
    assert material['friction_combine'] == 'Average'
    assert material['bounce_combine'] == 'Average'
    assert material['bounciness'] == 0
    assert not box['is_trigger'] and not box['provides_contacts']
    rows.append(dict(
        guid=box['source_guid'], file_id=box['source_file_id'],
        native_material_observation=material,
        native_layer=box['game_object_layer'], native_tag=box['game_object_tag'],
        native_contact_offset=box['contact_offset'],
        backend_friction_default=friction_default,
        default_friction_would_change_source=friction_default != material['dynamic_friction'],
        material_reference_must_remain_null=True,
        contact_offset_to_contact_skin_equivalence_qualified=False,
    ))
assert all(row['default_friction_would_change_source'] for row in rows)
O.mkdir()
result = dict(
    status='native_material_and_backend_default_divergence_documented',
    observed_utc=datetime.now(timezone.utc).isoformat(), rows=rows,
    input_pins={str(p): sha(p) for p in [Path(__file__), native, proof, backend, patched]},
    required_before_contact_admission=[
        'Use current source node entities and their composed placement transforms; preserve source identities.',
        'Keep collider activation independent of visual LOD selection and render admission.',
        'Preserve original null material refs; distinguish resolved project observations from authored material fields.',
        'Set friction and restitution explicitly from admitted provenance; never accept backend defaults silently.',
        'Verify contact distance policy by native/backend measurements; do not equate Unity contact offset with Rapier ContactSkin.',
        'Preserve layer and override fields, qualify interaction mapping and trigger/event behavior.',
        'Exercise actual Rapier context under translation, rotation and supported scaling, activation, removal and save/reopen.',
        'Qualify dynamics separately from direct point and ray intersection queries.',
    ],
    physics_world_integration_qualified=False,
    material_contact_simulation_qualified=False,
    vault_mutated=False, full_import=False,
)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
note = '''## Collisions Gladiator — écart de matériau identifié, 10 octobre 2026

Les trois références natives ont une friction statique et dynamique observée de 0.6000000238418579 ; le composant Friction de bevy_rapier3d 0.36.0 utilise 0.5 par défaut. Une activation physique avec les valeurs par défaut changerait donc les contacts. Les références source de matériaux restent nulles ; les valeurs observées doivent être conservées séparément avec leur provenance. L'équivalence entre contact offset Unity et ContactSkin Rapier reste à mesurer : aucune substitution automatique admise.

Preuve et critères du prochain raccordement : F:\\OpenRPG_ImportWork\\gladiator-contact-policy-review-v479\\RESULT.v1.json
La comparaison directe points/rayons v475 reste distincte. Aucun contact simulé, aucun parcours Studio et aucun nouvel asset complet qualifié par cette revue.

'''
for name in ['AVANCEMENT_ACTUEL.md', 'POINT_DE_REPRISE.md']:
    dashboard = W / 'SUIVI_LOTS_HIVEMIND' / name
    old = dashboard.read_bytes()
    (O / (name + '.before')).write_bytes(old)
    dashboard.write_bytes(note.encode('utf-8') + old)
print(json.dumps(dict(status=result['status'], boxes=len(rows))))
