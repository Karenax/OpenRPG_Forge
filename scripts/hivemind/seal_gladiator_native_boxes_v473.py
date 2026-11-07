"""Seal source-preserving native box references without promoting Rapier or Studio."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');I=W/'gladiator-native-box-reopen-v472/RESULT.v1.json';O=W/'gladiator-native-box-delivery-v473'
assert not O.exists()
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
result=json.loads(I.read_text('utf-8'));assert result['status']=='three_native_box_query_sets_reopened' and not result['comparison_errors'] and len(result['rows'])==3
assert result['closest_points']==63 and result['rays']==18
for p,h in result['input_pins'].items():assert sha(p)==h,p
for row in result['rows']:assert not row['disabled_direct_raycast_hit_observed'] and row['disabled_closest_returns_query_observed']
result['input_pins'].update({str(I):sha(I),str(Path(__file__)):sha(Path(__file__))});result['sealed_utc']=datetime.now(timezone.utc).isoformat()
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — références physiques natives réouvertes, 10 octobre 2026

Unity : 3 BoxColliders, 63 points et 18 rayons mesurés sur des clones temporaires. Identités, centres, dimensions et indicateurs concordent avec les sources ; projections et intersections concordent avec les boîtes à la tolérance fixée de 1e-6 relative ou absolue. Sources et copies privées inchangées, aucun script acheté exécuté, aucune simulation physique avancée. Désactivé : raycast sans hit et ClosestPoint retourne la requête. Les valeurs implicites observées dans ce banc sont friction statique/dynamique 0.6, rebond 0, combine Average et contact offset 0.01 ; elles ne sont pas des matériaux explicitement déclarés dans les assets. Les références nulles sont conservées.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-native-box-delivery-v473\\RESULT.v1.json
Restent adapter et comparer ces formes dans Rapier, puis vérifier leur intégration au placement commun et les comportements de jeu. Ce contrôle natif ne qualifie ni Rapier, ni le rendu, ni Studio. Le cycle LOD v469 reste une vérification distincte en cours. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
 p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(status='native_box_reference_checkpoint_sealed',points=63,rays=18,complete_asset_conversion_increment=0)))
