"""Seal 44 installed cards only after actual current product reader PASS."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'mountain-reviewed-images-delivery-v922';S=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    ip=W/'mountain-reviewed-images-index-v920/RESULT.v1.json';rp=W/'mountain-reviewed-images-native-reader-v921/RESULT.v1.json';vp=W/'mountain-reviewed-images-v919/RESULT.v1.json'
    index,reader,review=[read(p) for p in [ip,rp,vp]]
    assert index['cards_updated']==44 and index['images_installed']==88 and index['source_license_and_readiness_unchanged'] and index['other_cards_unchanged']
    assert reader['status']=='reviewed_current_native_source_cards_search_filter_details_images_passed' and reader['cards_verified']==44 and reader['actual_exit_code']==0 and reader['reader_logic_unchanged']
    native_path=rp.parent/'NATIVE-RESULT.v1.json';native=read(native_path)
    assert sha(native_path)==reader['native_result_sha256'] and native['cards_verified']==44
    assert {r['id'] for r in native['rows']}=={r['id'] for r in index['cards']}
    assert all(r['non_placeable_enforced'] and r['license']=='LicenseRef-Unity-Asset-Store-EULA' for r in native['rows'])
    for p,d in reader['input_pins'].items():assert sha(p)==d,p
    for p,d in index['preview_pins'].items():assert sha(p)==d,p
    pointer=V/'metadata/asset-source-support-registry.v1.json';state=read(pointer)
    assert state['revision']==index['new_catalog_revision']
    catalog=read(V/state['path']);assert len(catalog['assets'])==12517
    missing=sum(not a.get('preview') for a in catalog['assets']);assert missing==518
    prior=read(ip.parent/'prior-registry.json');before=read(V/prior['path']);assert sum(not a.get('preview') for a in before['assets'])==562
    by_id={a['id']:a for a in catalog['assets']};assert all(not by_id[r['id']].get('preview') for r in review['cards_refused'])
    O.mkdir(exist_ok=False)
    pins={str(p):sha(p) for p in [ip,rp,vp,native_path,pointer,V/state['path']]}
    row='| Images : lot livré | Mountain v922 : 44 fiches / 88 vues installees, recherche nom/pack, details, licences et PNG verifies par le lecteur natif actuel. Images principales manquantes : 562 → 518. | Parcours GUI reel encore non verifie ; poursuivre les lots manquants. |'
    mountain='| Images : Mountain | 47 fiches / 94 vues passent les controles RAW du SDK actuel. 44 fiches livrees au catalogue et relues via les fonctions natives Forge. | Trois troncs MountainTree02/03 restent presque noirs : diagnostic des materiaux et de leur eclairage. Pas de qualification conversion/runtime/GUI. |'
    documents={}
    for name in ['TABLEAU_DE_BORD_LOTS.md','SUIVI_LISIBLE.md']:
        p=S/name;old=p.read_bytes();(O/('before-'+name)).write_bytes(old)
        lines=old.decode('utf-8-sig').splitlines();changed=set()
        for i,line in enumerate(lines):
            for prefix,replacement in [('| Images : lot livré |',row),('| Images : Mountain |',mountain)]:
                if line.startswith(prefix) and prefix not in changed:lines[i]=replacement;changed.add(prefix)
        assert len(changed)==2
        p.write_text('\n'.join(lines)+'\n','utf-8');documents[str(p)]=sha(p)
    resume=S/'POINT_DE_REPRISE.md';old=resume.read_bytes();(O/'before-POINT_DE_REPRISE.md').write_bytes(old)
    note='''# Lot Mountain livre — v922

44 fiches / 88 images ajoutees au Vault. Manquantes : 562 → 518. Recherche par nom + filtre pack + source_support, details, licence, SHA PNG et refus de placement verifies par le lecteur natif actuel v921. IDs, sources, licences, retouches et readiness conserves par transaction v920. Les trois arbres sombres restent sans image et a diagnostiquer. Source_support reste non converti/non placable. GUI, rendu Forge et runtime non qualifies. Aucun nouveau full-import.

Prochaine action : examiner le shader du materiau M_Branch_Master_01 (GUID shader 4819724840ee9444f9da841b477038ce), textures T_MountainBranches_BaseColor / MRTA et leur eclairage sur les trois arbres. Aucun nouveau lot Unity actif pour Mountain. Ne pas refaire les captures/admissions deja validees.

---

'''
    resume.write_bytes(note.encode('utf-8')+old);documents[str(resume)]=sha(resume)
    result=dict(status='44_mountain_source_cards_88_images_and_current_native_library_verified',finished_utc=datetime.now(timezone.utc).isoformat(),cards_delivered=44,images_delivered=88,missing_primary_images_before=562,missing_primary_images_after=518,held_cards=review['cards_refused'],input_pins=pins,documents=documents,actual_editor_visual_verified=False,full_import_increment=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:result[k] for k in ['status','cards_delivered','images_delivered','missing_primary_images_after']}))
if __name__=='__main__':main()
