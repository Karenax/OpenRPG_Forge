"""Seal a delivery checkpoint for converted geometry, with runtime limits."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re
W=Path('F:/OpenRPG_ImportWork');O=W/'torch-geometry-checkpoint-v153';T=W/'SUIVI_LOTS_HIVEMIND'
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not O.exists()
 p=W/'medieval-torch-converted-geometry-v152/RESULT.v1.json';r=json.loads(p.read_text('utf-8'));assert r['all_vertex_bytes_reopened_and_reverse_compared'] and r['all_indices_reopened_and_reverse_compared']
 for group in ['input_pins','output_pins']:
  for path,h in r[group].items():assert sha(Path(path))==h,path
 assert len(r['meshes'])==5 and not r['renderer_admitted']
 stamp=datetime.now(timezone.utc).isoformat();block=f'''## Tranche L06 Torche — géométrie convertie, relevé {stamp}

**5 maillages examinés, 5 convertis : quatre LOD et un collider.** 5 586 vertices, 4 419 triangles ; 18 fichiers binaires dérivés sur F. Buffers natifs Unity réouverts, données finies et indices bornés ; transformation de coordonnées et sens des triangles vérifiés par inversion exacte. UV, normales, tangentes, slots de matériaux, bindings du collider et seuils LOD conservés. Données de travail dans `F:\\OpenRPG_ImportWork\\medieval-torch-converted-geometry-v152` ; aucun nouveau modèle indexé comme utilisable.

Restent : conversion des trois matériaux source, assemblage de la hiérarchie avec l'effet, simulation autonome/simultanée et rendu, puis placement/édition/sauvegarde et runtime dans Studio. **0 nouvel asset complet qualifié.** [Géométrie native](../medieval-torch-native-geometry-v150/RUN-RESULT.v1.json) · [Conversion réversible](../medieval-torch-converted-geometry-v152/RESULT.v1.json).

**L01 Web : v141 terminé et refus conservé.** Le log révèle 17 samplers pour une limite WebGPU de 16 ; le reçu navigateur refuse aussi la validation de la courbe sRGB du device. La nouvelle base de matériau conserve phase/uniforme/spécialisation Bevy et retire ses seuls slots PBR inutilisés ; dix textures source et ciel préservés. Correction non qualifiée tant que le vrai dessin GPU ne passe pas. Check WASM actif avec toolchain 1.98 explicite et flags isolés, après un refus de cache contenant une métadonnée 1.99. Aucun cache supprimé. Relance complète v151 en préparation, même exemple réel et nouveaux snapshots.

[Refus Web](../browser-source-glb-v141/RUN-RESULT.v1.json) · [Contexte de relance](../browser-source-glb-v151/RETRY-CONTEXT.v1.json).

'''
 O.mkdir()
 for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
  file=T/name;before=file.read_bytes();(O/(name+'.before')).write_bytes(before);text=file.read_text('utf-8-sig');text=re.sub(r'\n{3,}','\n\n',text);file.write_text(block+text,'utf-8')
 result=dict(status='converted_torch_geometry_lot_checkpoint_reopened_and_documented',finished_utc=stamp,meshes_converted=5,full_import=False,input_pins={str(p):sha(p)},document_pins={str(T/n):sha(T/n) for n in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']})
 (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
if __name__=='__main__':main()
