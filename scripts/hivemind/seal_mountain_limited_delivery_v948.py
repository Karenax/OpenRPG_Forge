"""Seal original previews only with product-visible limitations and reader PASS."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'mountain-original-limited-delivery-v948';S=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    ip=W/'mountain-original-limited-images-index-v946/RESULT.v1.json';rp=W/'mountain-original-limited-images-reader-v947/RESULT.v1.json';vp=W/'mountain-original-limited-images-v945/RESULT.v1.json'
    index,reader,review=[read(p) for p in [ip,rp,vp]]
    assert index['cards_updated']==3 and index['images_installed']==6 and index['source_license_and_readiness_unchanged'] and index['other_cards_unchanged']
    assert reader['status']=='reviewed_current_native_source_cards_search_filter_details_images_passed' and reader['cards_verified']==3 and reader['actual_exit_code']==0 and reader['reader_logic_unchanged']
    native_path=rp.parent/'NATIVE-RESULT.v1.json';native=read(native_path);assert sha(native_path)==reader['native_result_sha256']
    assert {r['id'] for r in native['rows']}=={r['id'] for r in review['rows']}
    for p,d in reader['input_pins'].items():assert sha(p)==d,p
    for p,d in index['preview_pins'].items():assert sha(p)==d,p
    pointer=V/'metadata/asset-source-support-registry.v1.json';state=read(pointer);assert state['revision']==index['new_catalog_revision']
    catalog=read(V/state['path']);by_id={c['id']:c for c in catalog['assets']}
    assert len(by_id)==12517 and sum(not a.get('preview') for a in by_id.values())==515
    prior=read(ip.parent/'prior-registry.json');assert sum(not a.get('preview') for a in read(V/prior['path'])['assets'])==518
    for row in review['rows']:
        card=by_id[row['id']];assert card['preview_source_limitations']==review['source_visual_limits'] and 'source-texture-uniform' in card['tags']
        assert 'RGBA zéro' in card['descriptions']['fr'] and 'zero RGBA' in card['descriptions']['en']
    O.mkdir(exist_ok=False)
    result=dict(status='three_original_mountain_previews_and_visible_source_limits_delivered',finished_utc=datetime.now(timezone.utc).isoformat(),cards_delivered=3,images_delivered=6,missing_primary_images_before=518,missing_primary_images_after=515,mountain_source_images_delivered_total=47,mountain_views_delivered_total=94,product_reader_verified=True,source_limit_descriptions_tags_and_metadata_verified=True,source_license_readiness_unchanged=True,input_pins={str(p):sha(p) for p in [ip,rp,vp,native_path,pointer,V/state['path']]},actual_editor_visual_verified=False,texture_repair_qualified=False,full_import_increment=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    note='''# Lot images Mountain complete pour ces 47 fiches — v948

47 fiches / 94 vues livrees : 44 fiches v922 + 3 sources originales avec limitations v948. Derniere transaction v946 conserve IDs, sources, licences, retouches et readiness. Les 3 images sources viennent des captures sans modification du lot v939, repetitions exactes ; les anciennes captures v875 ont des RAW differents et ne sont pas reutilisees silencieusement. Aucun diagnostic blanc/couleur ne figure dans le catalogue.

Deux cartes originales sont RGBA nul : T_FantasyTree01_BaseColor.png et T_Bark01_ARH.png, preuve dans les payloads archives v943. Limitation conservee dans preview_source_limitations, descriptions FR/EN et tag source-texture-uniform. Lecteur natif v947 : recherche par nom/pack, recherche textuelle de la limitation (3 resultats), details avec metadonnees originales, licence, descriptions, images SHA et refus de placement verifies. Sources toujours non converties/non placables, aucune retouche de texture, aucun verdict runtime/GUI/humain.

Images principales manquantes : 518 → 515. Prochaine action : inventaire courant des manquantes puis lot suivant selon recettes et preuves existantes, sans refaire ces 47 fiches. Garder la limitation source des trois arbres ouverte pour la conversion/retouche future.

---

'''
    p=S/'POINT_DE_REPRISE.md';old=p.read_bytes();(O/'before-POINT_DE_REPRISE.md').write_bytes(old);p.write_bytes(note.encode('utf-8')+old)
    for name in ['TABLEAU_DE_BORD_LOTS.md','SUIVI_LISIBLE.md']:
        p=S/name;old=p.read_bytes();(O/('before-'+name)).write_bytes(old);lines=old.decode('utf-8-sig').splitlines();done=set()
        for i,line in enumerate(lines):
            if line.startswith('| Images : lot livré |') and 'delivered' not in done:
                lines[i]='| Images : lot livré | Mountain : 47 fiches / 94 vues installees et relues par Forge ; dernier ajout 3 fiches / 6 vues avec limitations sources visibles v948. Images principales manquantes : 515. | Poursuivre les autres lots ; parcours GUI reel encore non verifie. |';done.add('delivered')
            if line.startswith('| Images : Mountain |') and 'mountain' not in done:
                lines[i]='| Images : Mountain | 47 fiches illustrees. Trois conservent leurs parties noires avec deux textures RGBA nulles prouvees dans l archive d origine ; mentions FR/EN, tags et metadata exposes par le lecteur v947. | Limitation source toujours ouverte pour conversion/retouche ; aucune substitution de texture. |';done.add('mountain')
        assert len(done)==2;p.write_text('\n'.join(lines)+'\n','utf-8')
    print(json.dumps({k:result[k] for k in ['status','cards_delivered','images_delivered','missing_primary_images_after']}))
if __name__=='__main__':main()
