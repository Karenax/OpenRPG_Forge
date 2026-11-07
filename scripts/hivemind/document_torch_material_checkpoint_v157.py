"""Record the exact material tranche with shader/runtime qualification pending."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');O=W/'torch-material-checkpoint-v157';T=W/'SUIVI_LOTS_HIVEMIND'
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not O.exists();inputs=[W/'medieval-torch-active-material-ir-v154/RESULT.v1.json',W/'medieval-torch-active-texture-bindings-v156/RESULT.v1.json'];ir,bindings=[json.loads(p.read_text('utf-8')) for p in inputs]
 assert len(ir['rows'])==3 and all(r['exit_code']==0 for r in ir['rows']);assert len(bindings['materials'])==3 and len(bindings['negative_controls'])==3
 for row in ir['rows']:assert sha(Path(row['path']))==row['sha256']
 for p,h in bindings['input_pins'].items():assert sha(Path(p))==h,p
 stamp=datetime.now(timezone.utc).isoformat();block=f'''## L06 Torche — trois matériaux compilés en IR, {stamp}

M_Wood_A, M_Metal et M_BasicTextured : **3 examinés, 3 graphes compilés, 0 refus de topologie**, chacun avec 15 nœuds actifs et deux branches résolues. Le compilateur existant est appliqué au propre ShaderGraph Medieval Kingdom, sans réutiliser les résultats matériaux d'un autre pack.

**15 sorties d'échantillonnage / 9 textures actives** réconciliées avec l'import Unity réel, grâce à la correspondance explicite des GUID originaux et privés. SHA des textures et .meta réouverts ; formats compressés, mipmaps, sRGB et paramètres de sampler natifs conservés. Trois contrôles de GUID croisés refusés. Les entrées nulles de masque/opacité restent conservées ; leurs branches sont désactivées dans ces matériaux source.

Fichiers sur F : [IR de chaque matériau](../medieval-torch-active-material-ir-v154/RESULT.v1.json) et [bindings natifs vérifiés](../medieval-torch-active-texture-bindings-v156/RESULT.v1.json). Cette tranche prouve la topologie et les références, pas l'exécution du shader. Restent l'export exact des mipmaps compressées, les valeurs/defaults natifs, le décodage des normales et des couleurs, puis rendu comparé et assemblage de la torche dans Forge. **0 nouvel asset complet qualifié.**

L01 Web v151 : contrôleur 3556 et Cargo 46532 observés actifs ; Cargo attend le check WASM 23972 dans le même cache. Aucun redémarrage. Sources du banc épinglées jusqu'au résultat terminal.

'''
 O.mkdir()
 for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
  p=T/name;old=p.read_bytes();(O/(name+'.before')).write_bytes(old);text=p.read_text('utf-8-sig');p.write_text(block+text,'utf-8')
 receipt=dict(status='three_torch_material_ir_and_native_texture_bindings_checkpoint_documented',finished_utc=stamp,materials=3,active_textures=9,full_import=False,input_pins={str(p):sha(p) for p in inputs},document_pins={str(T/n):sha(T/n) for n in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']})
 (O/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8');print(json.dumps(receipt))
if __name__=='__main__':main()
