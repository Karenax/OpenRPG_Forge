from pathlib import Path
import json,hashlib,datetime
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'pirate-p13-thumbnail-delivery-v323'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not O.exists()
 ip=W/'pirate-p13-thumbnail-index-v321/RESULT.v1.json';rp=W/'pirate-p13-thumbnail-native-reader-v322/RESULT.v1.json';vp=W/'pirate-p13-combined-review-v320/RESULT.v1.json'
 index,reader,review=read(ip),read(rp),read(vp)
 assert index['cards_updated']==reader['cards_verified']==review['cards_verified']==15
 assert index['images_installed']==review['images_verified']==30
 assert reader['actual_exit_code']==0 and reader['status']=='reviewed_current_native_source_cards_search_filter_details_images_passed'
 pointer=V/'metadata/asset-source-support-registry.v1.json';registry=read(pointer);assert registry['revision']==index['new_catalog_revision'];assert sha(V/registry['path'])==registry['sha256']
 current=read(V/registry['path']);oldpointer=read(ip.parent/'prior-registry.json');prior=read(V/oldpointer['path']);old={a['id']:a for a in prior['assets']};new={a['id']:a for a in current['assets']};ids={a['id'] for a in index['cards']}
 assert len(ids)==15 and set(old)==set(new) and {i for i in old if old[i]!=new[i]}==ids
 for i in ids:
  assert {k:v for k,v in old[i].items() if k not in {'names','descriptions','tags'} and not k.startswith('preview')}=={k:v for k,v in new[i].items() if k not in {'names','descriptions','tags'} and not k.startswith('preview')}
 pins=review['input_pins']|reader['input_pins']|index['preview_pins']|index['controlled_vault_delta']['new_files']
 for p in [Path(__file__),ip,rp,vp,pointer,V/registry['path']]:pins[str(p)]=sha(p)
 for p,h in pins.items():assert sha(Path(p))==h,p
 before=sum(not a.get('preview') for a in prior['assets']);after=sum(not a.get('preview') for a in current['assets']);assert before-after==15 and after==742
 O.mkdir();result=dict(status='eight_source_cards_sixteen_images_and_native_library_verified',cards_delivered=15,images_delivered=30,source_primary_images_missing_before=before,source_primary_images_missing_after=after,current_native_reader_verified=True,source_license_readiness_and_other_cards_preserved=True,vault_delta_rehashed=True,full_vault_rescan=False,actual_studio_gui_verified=False,complete_conversion_increment=0,full_import=False,revision=registry['revision'],input_pins=pins,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
 header="## Lot images Pirate P13 livré — v315 à v323\n\n15 nouvelles fiches et 30 vues revues et indexées sur F. Recherche par nom, filtre pack/source_support, détails, licence, SHA des images et refus de placement des sources vérifiés par le lecteur natif courant. Images principales manquantes : 757 → 742. 18 vues de neuf prefabs herbe comparées exactement avant/après migration et répétition ; six autres prefabs admis hors matériaux en attente. Vitre conservée en refus : _DstBlend2 ajouté diffère du défaut. Animation du vent et rendu produit non qualifiés. Contrôle GUI tenté v305, initialisation impossible (Windows sandbox helper_unknown_error) ; vérification visuelle Studio reste ouverte. Les autres fiches, sources, licences et états de conversion restent identiques. GUI Studio et conversions complètes non qualifiées ; incrément d'assets entièrement convertis : 0. Preuve : F:/OpenRPG_ImportWork/pirate-p13-thumbnail-delivery-v323/RESULT.v1.json.\n\n"
 for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
  p=W/'SUIVI_LOTS_HIVEMIND'/name;raw=p.read_bytes();(O/(name+'.before')).write_bytes(raw);p.write_text(header+raw.decode('utf-8-sig'),'utf-8')
 print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
