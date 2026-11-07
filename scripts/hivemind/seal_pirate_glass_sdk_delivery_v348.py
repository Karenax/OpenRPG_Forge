from pathlib import Path
import json,hashlib,datetime
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'pirate-glass-sdk-thumbnail-delivery-v348'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not O.exists()
 ip=W/'pirate-glass-sdk-thumbnail-index-v346/RESULT.v1.json';rp=W/'pirate-glass-sdk-thumbnail-native-reader-v347/RESULT.v1.json';vp=W/'pirate-glass-sdk-combined-review-v345/RESULT.v1.json'
 index,reader,review=read(ip),read(rp),read(vp)
 assert index['cards_updated']==reader['cards_verified']==review['cards_verified']==1
 assert index['images_installed']==review['images_verified']==2
 assert reader['actual_exit_code']==0 and reader['status']=='reviewed_current_native_source_cards_search_filter_details_images_passed'
 pointer=V/'metadata/asset-source-support-registry.v1.json';registry=read(pointer);assert registry['revision']==index['new_catalog_revision'];assert sha(V/registry['path'])==registry['sha256']
 current=read(V/registry['path']);oldpointer=read(ip.parent/'prior-registry.json');prior=read(V/oldpointer['path']);old={a['id']:a for a in prior['assets']};new={a['id']:a for a in current['assets']};ids={a['id'] for a in index['cards']}
 assert len(ids)==1 and set(old)==set(new) and {i for i in old if old[i]!=new[i]}==ids
 for i in ids:
  assert {k:v for k,v in old[i].items() if k not in {'names','descriptions','tags'} and not k.startswith('preview')}=={k:v for k,v in new[i].items() if k not in {'names','descriptions','tags'} and not k.startswith('preview')}
 pins=review['input_pins']|reader['input_pins']|index['preview_pins']|index['controlled_vault_delta']['new_files']
 for p in [Path(__file__),ip,rp,vp,pointer,V/registry['path']]:pins[str(p)]=sha(p)
 for p,h in pins.items():assert sha(Path(p))==h,p
 before=sum(not a.get('preview') for a in prior['assets']);after=sum(not a.get('preview') for a in current['assets']);assert before-after==1 and after==119
 O.mkdir();result=dict(status='one_source_card_two_images_and_native_library_verified',cards_delivered=1,images_delivered=2,source_primary_images_missing_before=before,source_primary_images_missing_after=after,current_native_reader_verified=True,source_license_readiness_and_other_cards_preserved=True,vault_delta_rehashed=True,full_vault_rescan=False,actual_studio_gui_verified=False,complete_conversion_increment=0,full_import=False,revision=registry['revision'],input_pins=pins,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
 header="## Fenetre Pirate : controle SDK livre - v341 a v348\n\n1 nouvelle fiche et 2 vues revues et indexees sur F. Recherche, filtre pack/source_support, details, licence, SHA des images et refus de placement verifies par le lecteur natif courant. Images principales manquantes : 719 -> 718. Refus historique _DstBlend2 conserve ; nouvelle preuve par execution de la politique HDRP transparente sur clone, controle negatif opaque, comparaison native et repetitions exactes. Equivalence avec ancien SDK, fidelite optique du verre et rendu OpenRPG non qualifies. Sources, licences, autres fiches et etats de conversion preserves. Controle GUI Studio et conversion complete restent ouverts ; increment entierement converti : 0. Preuve : F:/OpenRPG_ImportWork/pirate-glass-sdk-thumbnail-delivery-v348/RESULT.v1.json.\n\n"
 for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
  p=W/'SUIVI_LOTS_HIVEMIND'/name;raw=p.read_bytes();(O/(name+'.before')).write_bytes(raw);p.write_text(header+raw.decode('utf-8-sig'),'utf-8')
 print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
