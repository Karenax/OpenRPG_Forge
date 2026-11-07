"""Record the excluded color-buffer hypothesis and the next measured checks."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-color-hypothesis-delivery-v931';S=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    fbx=W/'mountain-original-fbx-color-review-v927/RESULT.v1.json';measure=W/'mountain-vertex-color-measurements-v930/RESULT.v1.json'
    source,comparison=read(fbx),read(measure)
    assert len(source['files'])==2 and all(f['structure']['total_vertex_color_layers']==0 for f in source['files'])
    assert comparison['different_views']==0 and len(comparison['rows'])==6 and comparison['source_repeat_exact'] and comparison['diagnostic_repeat_exact']
    for proof in [source,comparison]:
        for p,d in proof['input_pins'].items():assert sha(p)==d,p
    assert sha(comparison['comparison_image'])==comparison['comparison_image_sha256']
    O.mkdir(exist_ok=False)
    for name in ['TABLEAU_DE_BORD_LOTS.md','SUIVI_LISIBLE.md']:
        p=S/name;old=p.read_bytes();(O/('before-'+name)).write_bytes(old);lines=old.decode('utf-8-sig').splitlines()
        for i,line in enumerate(lines):
            if line.startswith('| Images : Mountain |'):
                lines[i]='| Images : Mountain | 44 fiches / 88 vues livrees v922. Deux FBX originaux sans couleurs de sommets v927. Ajout de couleurs blanches sur clones : six vues RAW identiques v930, aucun asset modifie. | Hypothese couleurs de sommets ecartee dans cette capture. Trois troncs noirs toujours reserves ; mesurer UV, albedo et normales avec le shader SpeedTree reel. |';break
        else:raise AssertionError('Missing Mountain row')
        p.write_text('\n'.join(lines)+'\n','utf-8')
    p=S/'POINT_DE_REPRISE.md';old=p.read_bytes();(O/'before-POINT_DE_REPRISE.md').write_bytes(old)
    note='''# Arbres Mountain : hypothese des couleurs de sommets ecartee — v931

FBX originaux Tree02/Tree03 : parse binaire independant complet des noeuds (FBX 7300, 563 noeuds chacun), geometrie visible + ConvexHulls, aucun LayerElementColor, hashes des sources et copies identiques v927. Il ne s'agit pas d'une perte de couleurs lors de cet import Unity.

Experience v929 : 18 captures, 3 arbres, controles source/current-repeat/diagnostic-white-colors/diagnostic-repeat/repeat. Unity sortie 0, aucun changement des fichiers surveilles. Clones avec identites explicitement diagnostic-white-vertex-colors-clone-of ; topologie, indices, bounds et nombre de sommets gardes. Les six vues source/clone sont RAW identiques, aucun pixel RGB different v930. La planche comparative a ete effectivement inspectee. Donc ajouter des couleurs blanches ne corrige pas les troncs dans le rendu actuel ; ne pas appliquer cette modification inutile.

Prochaine mesure : UV et echantillonnage albedo par sous-maillage de tronc, puis normales/tangentes et chemin SpeedTree8InterpolatedNormals. La texture MainTex actuellement liee est un atlas de branches transparentes (T_MountainBranches_BaseColor), visuellement verifie ; ne pas supposer une erreur de binding sans mesure des UV. _ExtraTex null est un autre fait, pas encore une cause prouvee ; EFFECT_EXTRA_TEX=0 sur Branch04 presente aussi le tronc noir. Sous-graphe ColorAlpha : Library/PackageCache/com.unity.shadergraph@8d7376ec43ee/ShaderGraphLibrary/Nature/SpeedTree8ColorAlpha.shadersubgraph.

Trois arbres gardes en reserve. 44 fiches / 88 images Mountain deja livrees et lues par Forge v922 ; 518 images principales manquantes. Aucun nouvel asset full-import. Aucun job Unity de ce lot actif ; ne pas refaire les controles passes.

---

'''
    p.write_bytes(note.encode('utf-8')+old)
    result=dict(status='original_fbx_verified_white_color_correction_ruled_out',finished_utc=datetime.now(timezone.utc).isoformat(),input_pins={str(fbx):sha(fbx),str(measure):sha(measure),comparison['comparison_image']:sha(comparison['comparison_image'])},visual_comparison_agent_review=True,human_review=False,source_mutations=0,images_admitted=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(result['status'])
if __name__=='__main__':main()
