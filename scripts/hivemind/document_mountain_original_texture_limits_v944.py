"""Document verified source texture limitations and stable diagnostic controls."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-original-texture-limitations-v944';S=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    paths=[W/'mountain-generalorm-measurements-v940/RESULT.v1.json',W/'mountain-generalorm-channel-measurements-v942/RESULT.v1.json',W/'mountain-blank-source-archive-proof-v943/RESULT.v1.json']
    controls,channels,archive=[read(p) for p in paths]
    assert controls['source_repeat_exact'] and controls['diagnostic_repeat_exact'] and controls['different_views']==6
    assert all(r['rgba_all_zero'] and r['archived_sha256']==r['source_sha256'] for r in archive['rows']) and len(archive['rows'])==2
    for proof in [controls,channels]:
        for p,d in proof['input_pins'].items():assert sha(p)==d,p
    assert sha(controls['comparison_image'])==controls['comparison_image_sha256']
    for row in archive['rows']:assert sha(row['source'])==row['source_sha256'] and sha(row['source_meta'])==row['source_meta_sha256']
    O.mkdir(exist_ok=False)
    result=dict(status='two_original_zero_rgba_textures_proven_stable_generalorm_controls_reviewed',finished_utc=datetime.now(timezone.utc).isoformat(),source_texture_limits=archive['rows'],source_archive_sha256=archive['archive_sha256'],agent_visual_observation='GeneralORM BaseMap white control reveals bright branches while lower black trunk remains. Source and diagnostic repeats are exact. Two original source maps are all RGBA zero and their bytes match archive payloads. This proves source texture limitations; it does not establish every cause of lower trunk darkness or justify substituting another material/texture.',human_review=False,input_pins={str(p):sha(p) for p in paths+[Path(controls['comparison_image'])]},source_mutations=0,repair_applied=False,images_admitted=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    note='''# Limitation textures originales Mountain prouvee — v944

Deux fichiers 2048x2048 sont integralement RGBA (0,0,0,0) : T_FantasyTree01_BaseColor.png et T_Bark01_ARH.png. v943 a relu le SHA de l'archive originale de 1 880 409 254 octets (c26444ee...) puis compare les payloads GUID/asset dans le TAR avec les sources : identiques. Sources, copies Unity et archive conservees. Les cartes Normal sont non uniformes ; ne pas les decrire comme nulles.

GeneralORM : 18 captures v939, sortie Unity 0, aucun changement surveille. Six vues source et six vues diagnostiques repetables RAW a l'identique v940. BaseMap blanche sur clones seulement revele des branches claires mais la partie basse noire reste. La planche a ete effectivement inspectee. Donc la source a des cartes nulles confirmees ; toute la noirceur basse n'est pas encore expliquee. Aucun remplacement, remappage, modification de source ou image diagnostique admise.

Attribution : SpeedTree slot 0 = feuillage ; GeneralORM slot 1 M_FantasyTree_01 / slot 2 M_Bark_01. La mesure v942 donne des echantillons BaseMap slot 1 noirs a 100 %, ORM slot 2 RGBA nul, normales/tangentes presentes et unitaires. Des echantillons CPU aux sommets/centroides ne sont pas une evaluation GPU du shader.

Prochaine action : conserver cette limitation dans les fiches ; pour les trois vignettes source, decider leur admission avec mention explicite des textures originales nulles (aucune parite ancien SDK / conversion / retouche prouvee). Si une correction du tronc est poursuivie, mesurer le chemin ORM/normal/illumination de S_GeneralORM avec temoins sur clones ; ne pas inventer de texture ou emprunter une autre variante sans preuve de correspondance et provenance.

44 fiches / 88 images Mountain deja livrees v922. 518 images principales manquantes, dont les trois arbres toujours reserves. Aucun asset full-import supplementaire ; objectif complet actif. Aucun job Unity actif pour ces diagnostics.

---

'''
    p=S/'POINT_DE_REPRISE.md';old=p.read_bytes();(O/'before-POINT_DE_REPRISE.md').write_bytes(old);p.write_bytes(note.encode('utf-8')+old)
    for name in ['TABLEAU_DE_BORD_LOTS.md','SUIVI_LISIBLE.md']:
        p=S/name;old=p.read_bytes();(O/('before-'+name)).write_bytes(old);lines=old.decode('utf-8-sig').splitlines()
        for i,line in enumerate(lines):
            if line.startswith('| Images : Mountain |'):
                lines[i]='| Images : Mountain | 44 fiches / 88 vues livrees. Deux textures RGBA nulles confirmees identiques aux payloads de l archive originale v943 ; controles GeneralORM stables v940. | Documenter la limitation source sur les trois arbres ; aucune texture inventee/substituee. Admission des vignettes originales avec mention explicite de cette limite reste a traiter. |';break
        else:raise AssertionError('Missing Mountain row')
        p.write_text('\n'.join(lines)+'\n','utf-8')
    print(result['status'])
if __name__=='__main__':main()
