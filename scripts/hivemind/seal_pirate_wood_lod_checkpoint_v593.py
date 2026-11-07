"""Retain the next owned-helper audit and document the measured lot boundary."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-wood-lod-checkpoint-v593'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
proofpath=W/'pirate-wood-lod-migration-review-v592/RESULT.v1.json';proof=read(proofpath)
assert proof['groups_verified']==12 and proof['lod_cases_verified']==36 and proof['view_comparisons_verified']==72
for p,h in proof['input_pins'].items():assert sha(Path(p))==h,p
priorpath=W/'pirate-palm-wind-native-reference-v558/HELPER-OWNERSHIP.v1.json';audit=read(priorpath)
assert not audit['asset_dll_found']
for helper in audit['helpers']:
    for key in ['private_path','controlled_source']:assert sha(Path(helper[key]))==helper['sha256'],helper[key]
name='PirateWoodLodMigrationV591.cs';private=PROJECT/'Assets/Editor'/name;controlled=W/'pirate-wood-lod-migration-v591'/name
assert sha(private)==sha(controlled)
audit['helpers'].append(dict(private_path=str(private),controlled_source=str(controlled),sha256=sha(private)))
assert {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
assert not any((PROJECT/'Assets').rglob('*.dll'))
audit.update(previous_audit=str(priorpath),checked_utc=datetime.now(timezone.utc).isoformat(),batch='pirate-wood-lod-migration-v591')
O.mkdir();(O/'HELPER-OWNERSHIP.v1.json').write_text(json.dumps(audit,indent=2),'utf-8')
result=dict(status='native_wood_lod_comparisons_checkpointed',proof=str(proofpath),proof_sha256=sha(proofpath),helper_audit_sha256=sha(O/'HELPER-OWNERSHIP.v1.json'),groups=12,lod_cases=36,view_comparisons=72,images_installed_in_vault=0,complete_conversion_increment=0,full_import=False,next_step='Reopen and visually review current capture PNGs, then permit only these exact source/mesh/material epochs in the image guard and index reviewed source cards. Never remove the global material refusal for conversion or original SDK parity.',finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
header='## Lot materiaux bois Pirate LOD - v591 a v593\n\nDouze groupes, 36 cas LOD0/1/2 et 72 comparaisons de vues natives verifies. Les 144 captures ont reussi ; rendus bruts import/source-stencil/repetition identiques octet par octet. Sources intactes, meshes LOD verifies distincts. Aucun nouveau fichier indexe dans le Vault, aucune conversion Forge complete ajoutee ; 596 fiches source sans image principale. Prochaine etape : revue visuelle des captures puis admission strictement liee aux epochs source, mesh et materiau de cette preuve. Les refus generaux et la non-qualification du SDK original, du rendu Forge et de la GUI restent conserves. Preuve : '+str(proofpath)+'.\n\n'
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;raw=p.read_bytes();(O/(name+'.before')).write_bytes(raw);p.write_text(header+raw.decode('utf-8-sig'),'utf-8')
print(json.dumps(result))
