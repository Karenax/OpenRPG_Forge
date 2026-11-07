"""Deliver ten reviewed variants; explicitly retain the six material refusals."""
from pathlib import Path
import re
parent=Path(__file__).with_name('seal_pirate_lod_thumbnail_batch03_delivery_v374.py')
code=parent.read_text('utf-8')
for old,new in [
    ('pirate-lod-thumbnail-batch03-delivery-v374','pirate-lod-thumbnail-batch06-admitted-delivery-v522'),
    ('pirate-lod-thumbnail-batch03-index-v372','pirate-lod-thumbnail-batch06-admitted-index-v520'),
    ('pirate-lod-thumbnail-batch03-native-reader-v373','pirate-lod-thumbnail-batch06-admitted-reader-v521'),
    ('pirate-lod-batch03-combined-review-v371','pirate-lod-batch06-admitted-review-v519'),
    ("review['cards_verified']==16","review['cards_verified']==10"),
    ("review['images_verified']==32","review['images_verified']==20"),
    ('assert len(ids)==16','assert len(ids)==10'),
    ('before-after==16 and after==680','before==648 and before-after==10 and after==638'),
    ("status='sixteen_lod_source_cards_thirty_two_images_and_native_library_verified',cards_delivered=16,images_delivered=32", "status='ten_admitted_lod_cards_twenty_images_and_native_library_verified_six_refusals_retained',cards_delivered=10,images_delivered=20,cards_refused=6,refusals=review['refusals']"),
]:
    assert old in code,old
    code=code.replace(old,new)
needle='index,reader,review=read(ip),read(rp),read(vp)'
code=code.replace(needle,needle+"\n assert review['cards_refused']==len(review['refusals'])==6\n assert {r['id'] for r in review['refusals']}.isdisjoint(r['id'] for r in index['cards'])")
code=code.replace('for p in [Path(__file__),ip,rp,vp,pointer','for p in [parent,Path(__file__),ip,rp,vp,pointer')
header='## Lot images Pirate LOD 06 partiellement livre - v513 a v522\n\nDix variantes admises et vingt vues inspectees puis indexees sur F. Recherche, filtre pack/source_support, details, licence, images et refus de placement verifies par le lecteur natif courant. Images principales manquantes : 648 -> 638. Six variantes de trois palmiers restent refusees pour qualification dediee de migration materiau ; aucune fiche refusee modifiee. Sources, licences, IDs, readiness et autres fiches preserves. Pas de qualification de conversion complete ou GUI Studio. Preuve : F:/OpenRPG_ImportWork/pirate-lod-thumbnail-batch06-admitted-delivery-v522/RESULT.v1.json.\n\n'
code,count=re.subn(r' header="[^\n]*"',lambda m:' header='+repr(header),code)
assert count==1
exec(compile(code,str(parent)+'#ten-admitted-delivery-six-retained-refusals','exec'),globals())
