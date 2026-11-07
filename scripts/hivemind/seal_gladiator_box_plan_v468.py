"""Publish a precise collider preparation checkpoint, never a runtime verdict."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');I=W/'gladiator-source-box-collider-plan-v467/RESULT.v1.json';O=W/'gladiator-box-plan-delivery-v468'
assert not O.exists()
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
result=json.loads(I.read_text('utf-8'));assert result['status']=='three_original_box_collider_local_geometries_derived_and_reopened' and result['colliders']==3 and result['corner_checks']==24
for p,h in result['input_pins'].items():assert sha(p)==h,p
result['input_pins'].update({str(I):sha(I),str(Path(__file__)):sha(Path(__file__))});result['sealed_utc']=datetime.now(timezone.utc).isoformat()
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — géométrie des trois colliders préparée, 10 octobre 2026

Trois BoxColliders originaux retrouvés dans les observations natives et les GLB courants. Centres, tailles et indicateurs enabled/trigger/providesContacts préservés. Le passage Unity vers Forge conserve exactement les bits float32 des 24 coins des boîtes ; les demi-dimensions reconstruisent les tailles source. Les références de matériaux physiques et les réglages de couches sont conservés sans valeur supposée. Aucun GLB ni fichier du Vault modifié.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-box-plan-delivery-v468\\RESULT.v1.json
Ce lot prépare les formes ; il ne valide ni Rapier, ni les matériaux physiques implicites, ni le contact offset, ni Studio. L'activation physique doit rester indépendante de la visibilité LOD. Prochaine étape collisions : mesurer les comportements natifs manquants et raccorder les formes au contrat physique commun. Le contrôle du cycle LOD v466 reste distinct et ne reçoit pas de verdict ici. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
 p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(status='source_box_plan_checkpoint_sealed',colliders=3,complete_asset_conversion_increment=0)))
