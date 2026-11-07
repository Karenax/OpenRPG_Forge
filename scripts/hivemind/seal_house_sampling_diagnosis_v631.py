"""Publish the measured sampling diagnosis to the lot tracker, not a PASS."""
from pathlib import Path
import json,hashlib
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');A=W/'house-active-sampling-analysis-v630';O=W/'house-sampling-diagnosis-checkpoint-v631'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
r=json.loads((A/'RESULT.v1.json').read_text('utf-8-sig'));assert not O.exists()
assert r['native_uninstrumented_color_byte_exact'] and r['wgsl_uninstrumented_color_byte_exact']
assert all(p['different_components']==0 for p in r['planes'][2:])
assert r['planes'][0]['failed_components']==3206 and r['planes'][1]['failed_components']==5014
for p,h in r['input_pins'].items():assert sha(p)==h,p
header='''## House — écart localisé dans le prélèvement des couleurs (v631)

- 48 images comparées sur les six matériaux ; les coordonnées UV après parallax, leur profondeur et les dérivées fines/grossières sont **identiques bit à bit** dans le diagnostic.
- Les images de couleur finale du diagnostic sont identiques bit à bit à celles du contrôle non instrumenté, dans chaque moteur.
- 5 014 composantes de couleur brute dépassent 0,00001 avant contraste/teinte ; 3 206 composantes dépassent cette limite dans la couleur finale. La correction de la macro HDRP ne résout pas cet écart.
- Prochaine vérification : comparer, dans Unity, les textures natives compressées BC et leur transport RGBA float décodé, avec le même shader et les mêmes mipmaps. Le transport est une hypothèse à vérifier, pas encore une cause prouvée.
- Sources et importeurs privés inchangés. Les premiers diagnostics sur la couche inactive sont conservés ; seul v628/v629 mesure la couche active.
- Les 24 prefabs restent à qualifier dans Forge : profondeur, rendu/éclairage, colliders et accès Studio. **Aucune conversion complète ajoutée**.

'''
O.mkdir();(O/'LOT.md').write_text(header,'utf-8')
result=dict(status='house_sampling_diagnosis_checkpoint_retained',utc=datetime.now(timezone.utc).isoformat(),lot='L03.HOUSE.MAIN.01',materials=6,prefab_cards=24,input_pins={str(A/'RESULT.v1.json'):sha(A/'RESULT.v1.json'),str(A/'FRAME-PLANES.v1.json'):sha(A/'FRAME-PLANES.v1.json'),str(Path(__file__)):sha(Path(__file__))},transport_cause_proven=False,full_import=False,complete_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_text(header+before.decode('utf-8-sig'),'utf-8')
print(json.dumps(result))
