"""Update the readable lot table from verified controls, with prior snapshots."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-sdk-lot-progress-v917';S=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    paths=[W/'mountain-sdk-partial-review-v911/RESULT.v1.json',W/'mountain-waterfall-typed-review-v916/RESULT.v1.json']
    partial,waterfall=[read(p) for p in paths]
    assert partial['cards_verified']==46 and waterfall['cards_verified']==1 and waterfall['all_sdk_reconstructed_views_raw_exact']
    for result in [partial,waterfall]:
        for p,d in result['input_pins'].items():assert sha(p)==d,p
    O.mkdir(exist_ok=False);changes={}
    mountain='| Images : Mountain | 47 fiches / 94 vues : comparaison native RAW identique entre materiaux migres et parametres auteur reconstruits par le SDK HDRP actuel. 46 fiches scellees v911 ; cascade verifiee avec scalaires/vecteurs/couleurs types v916. Sources intactes. | Lever ou maintenir les trois reserves visuelles sur les troncs, puis indexation privee des images et verification du lecteur produit. Parite ancien SDK et rendu Forge non qualifies. |'
    for name in ['TABLEAU_DE_BORD_LOTS.md','SUIVI_LISIBLE.md']:
        p=S/name;before=p.read_bytes();(O/('before-'+name)).write_bytes(before)
        lines=before.decode('utf-8-sig').splitlines();found=False
        for i,line in enumerate(lines):
            if line.startswith('| Images : Mountain |'):
                lines[i]=mountain;found=True;break
        assert found
        for i,line in enumerate(lines):
            if line.startswith('Mis à jour :'):
                lines[i]='Mis à jour : '+datetime.now(timezone.utc).isoformat();break
        p.write_text('\n'.join(lines)+'\n','utf-8');changes[str(p)]={'before':hashlib.sha256(before).hexdigest(),'after':sha(p)}
    resume=S/'POINT_DE_REPRISE.md';before=resume.read_bytes();(O/'before-POINT_DE_REPRISE.md').write_bytes(before)
    note='''# Reprise Mountain — lot v917

47 fiches / 94 vues passent les comparaisons RAW avec les parametres auteur reconstruits par le SDK HDRP installe. Preuves : mountain-sdk-partial-review-v911/RESULT.v1.json (46 fiches), mountain-waterfall-typed-review-v916/RESULT.v1.json (cascade).

La cascade avait une ancienne entree scalaire _Speed et une entree vectorielle de meme nom. Les nouveaux gardes utilisent le type reel du shader et controlent les valeurs vectorielles/couleurs avant et apres ResetMaterialKeywords. Les six captures sont reussies. Une erreur du controleur sur la taille RAW a ete conservee ; le lecteur independant v916 a verifie les SHA, la vraie taille 512*512*8, les LOD, les repetitions et les gardes auteur sans relancer Unity.

Aucune nouvelle image admise : dernier compteur 562 images principales manquantes. Prochaine action : revue des trois troncs sombres signales v898, sceller le lot d'images admis, appliquer la transaction source_support puis verifier le lecteur natif. Ne pas confondre ces preuves d'images avec une conversion, une parite ancien SDK, un rendu Forge ou une validation GUI. Aucun nouveau full-import.

Tous les processus Unity de ces lots sont termines. Ne pas relancer les 46 fiches ni la cascade deja verifiee. Travail et preuves sur F uniquement.

---

'''
    resume.write_bytes(note.encode('utf-8')+before);changes[str(resume)]={'before':hashlib.sha256(before).hexdigest(),'after':sha(resume)}
    result=dict(status='mountain_47_cards_native_sdk_controls_documented',finished_utc=datetime.now(timezone.utc).isoformat(),cards_verified=47,views_verified=94,images_admitted=0,full_import=False,input_pins={str(p):sha(p) for p in paths},documents=changes)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],cards_verified=47,images_admitted=0)))
if __name__=='__main__':main()
