"""Seal four exact delivered source cards and their actual native reader proofs."""
from pathlib import Path
parent = Path(__file__).with_name('seal_pirate_lod_thumbnail_batch03_delivery_v374.py')
code = parent.read_text('utf-8')
replacements = {
    'pirate-lod-thumbnail-batch03-delivery-v374':'pirate-held-wood-images-delivery-v837',
    'pirate-lod-thumbnail-batch03-index-v372':'pirate-held-wood-images-index-v835',
    'pirate-lod-thumbnail-batch03-native-reader-v373':'pirate-held-wood-images-native-reader-v836',
    'pirate-lod-batch03-combined-review-v371':'pirate-held-wood-images-seal-v834',
    'before-after==16 and after==680':'before==570 and before-after==4 and after==566',
    '==16':'==4', '==32':'==8',
    'cards_delivered=16,images_delivered=32':'cards_delivered=4,images_delivered=8',
    'sixteen_lod_source_cards_thirty_two_images_and_native_library_verified':'four_exact_wood_lod_cards_eight_images_and_native_library_verified',
    'Lot images Pirate LOD 03 livre - v368 a v374':'Lot images bois exactes livre - v819 a v837',
    '16 variantes LOD1/LOD2 et 32 vues':'4 variantes LOD1/LOD2 et 8 vues',
    'huit controles LOD0':'deux controles LOD0',
    '696 -> 680':'570 -> 566',
}
for before, after in replacements.items():
    assert before in code, before
    code = code.replace(before, after)
code = code.replace('[Path(__file__),ip,rp,vp,pointer,', '[parent,Path(__file__),ip,rp,vp,pointer,')
exec(compile(code, str(parent)+'#exact-four-wood-source-card-delivery', 'exec'), globals())
