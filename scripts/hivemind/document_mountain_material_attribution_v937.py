"""Correct the unsupported trunk-slot attribution using actual visual controls."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-material-attribution-review-v937';S=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    native_path=W/'mountain-native-uv-normal-diagnostic-v932/NATIVE-RESULT.v1.json';uv_path=W/'mountain-uv-albedo-measurements-v933/RESULT.v1.json';control_path=W/'mountain-albedo-measurements-v936/RESULT.v1.json'
    native,uv,control=[read(p) for p in [native_path,uv_path,control_path]]
    assert control['status']=='white_albedo_deltas_measured_repeats_unstable_no_causal_verdict' and not control['source_repeat_exact']
    for p,d in control['input_pins'].items():assert sha(p)==d,p
    for p,d in uv['input_pins'].items():assert sha(p)==d,p
    assert sha(control['comparison_image'])==control['comparison_image_sha256']
    slots=[]
    for card in native['cards']:
        for renderer in card['renderers']:
            for slot in renderer['slots']:
                material=slot['material'];slots.append(dict(id=card['id'],slot=slot['submesh'],material=material['path'],shader=material['shader'],vertices=slot['unique_vertices'],normal_zero=slot['zero_normals'],tangent_zero=slot['zero_tangents'],normal_length_min=slot['normal_length_min'],normal_length_max=slot['normal_length_max']))
    assert len(slots)==9 and all(s['normal_zero']==s['tangent_zero']==0 for s in slots)
    O.mkdir(exist_ok=False)
    result=dict(status='speedtree_foliage_attribution_corrected_generalorm_dark_geometry_pending',finished_utc=datetime.now(timezone.utc).isoformat(),slot_inventory=slots,agent_visual_observation='Actual white MainTex experiment changes the foliage into opaque white/green cards while the dark trunk shape remains. SpeedTree slot 0 UV/albedo observations cannot establish the cause of the trunk darkness. GeneralORM slots 1/2 remain the next targets. Repeats are unstable, so no isolated causal verdict from this run.',human_review=False,input_pins={str(p):sha(p) for p in [native_path,uv_path,control_path,Path(control['comparison_image'])]},source_mutations=0,images_admitted=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    note='''# Correction attribution materiaux Mountain — v937

IMPORTANT : les mesures SpeedTree du slot 0 concernent le feuillage, pas le tronc. L'experience MainTex blanche a ete visuellement inspectee : feuillage blanc/vert devenu opaque, silhouette noire du tronc inchangee. Les 85,8 % de sommets sur pixels sombres v933 concernent l'atlas de branches ; ne pas les employer pour expliquer le tronc. Les slots 1/2 utilisent S_GeneralORM : M_FantasyTree_01 et M_Bark_01, BaseMap/ORMMap/NormalMap declares.

UV, normales et tangentes exportes pour tous les slots v932, Unity sortie 0. Aucune normale/tangente nulle sur les neuf slots. Experience v935 : 18 captures, source et diagnostic finissent avec des repetitions differentes (jusqu'a 0,0742 pour source RGB). Resultat v936 explicitement instable, aucune qualification ou admission. Ne pas supprimer cet echec ou retenir son effet comme causalite isolee.

Prochain controle : material clones uniquement pour S_GeneralORM, changement diagnostique de _BaseMap, autres proprietes/shader/maillage/keywords/pass state gardes. Tester les repetitions exactes. Puis mesurer _ORMMap et _NormalStrength si necessaire. Aucun remappage vers un nouveau shader ou texture d'un autre pack.

Les trois arbres restent en reserve. 44 fiches / 88 images Mountain livrees v922, 518 manquantes au catalogue. Aucun nouvel asset full-import.

---

'''
    p=S/'POINT_DE_REPRISE.md';old=p.read_bytes();(O/'before-POINT_DE_REPRISE.md').write_bytes(old);p.write_bytes(note.encode('utf-8')+old)
    for name in ['TABLEAU_DE_BORD_LOTS.md','SUIVI_LISIBLE.md']:
        p=S/name;old=p.read_bytes();(O/('before-'+name)).write_bytes(old);lines=old.decode('utf-8-sig').splitlines()
        for i,line in enumerate(lines):
            if line.startswith('| Images : Mountain |'):
                lines[i]='| Images : Mountain | 44 fiches / 88 vues livrees. UV/normales/tangentes mesures v932. Controle MainTex SpeedTree : feuillage change, tronc noir conserve, repetitions instables v936. | Attribution corrigee v937 : cibler S_GeneralORM (slots 1/2), pas le slot SpeedTree du feuillage. Trois images toujours reservees. |';break
        else:raise AssertionError('Missing Mountain row')
        p.write_text('\n'.join(lines)+'\n','utf-8')
    print(result['status'])
if __name__=='__main__':main()
