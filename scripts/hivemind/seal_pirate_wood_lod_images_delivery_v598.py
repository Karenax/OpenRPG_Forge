from pathlib import Path
parent=Path(__file__).with_name('seal_pirate_lod_thumbnail_batch03_delivery_v374.py')
code=parent.read_text("utf-8")
assert 'pirate-lod-thumbnail-batch03-delivery-v374' in code
code=code.replace('pirate-lod-thumbnail-batch03-delivery-v374','pirate-wood-lod-images-delivery-v598')
assert 'pirate-lod-thumbnail-batch03-index-v372' in code
code=code.replace('pirate-lod-thumbnail-batch03-index-v372','pirate-wood-lod-images-index-v596')
assert 'pirate-lod-thumbnail-batch03-native-reader-v373' in code
code=code.replace('pirate-lod-thumbnail-batch03-native-reader-v373','pirate-wood-lod-images-native-reader-v597')
assert 'pirate-lod-batch03-combined-review-v371' in code
code=code.replace('pirate-lod-batch03-combined-review-v371','pirate-wood-lod-images-sealed-v595')
assert '==16' in code
code=code.replace('==16','==24')
assert '==32' in code
code=code.replace('==32','==48')
assert 'before-after==24 and after==680' in code
code=code.replace('before-after==24 and after==680','before==596 and before-after==24 and after==572')
assert 'cards_delivered=16,images_delivered=32' in code
code=code.replace('cards_delivered=16,images_delivered=32','cards_delivered=24,images_delivered=48')
assert 'sixteen_lod_source_cards_thirty_two_images_and_native_library_verified' in code
code=code.replace('sixteen_lod_source_cards_thirty_two_images_and_native_library_verified','twenty_four_wood_lod_source_cards_forty_eight_images_and_native_library_verified')
assert 'Lot images Pirate LOD 03 livre - v368 a v374' in code
code=code.replace('Lot images Pirate LOD 03 livre - v368 a v374','Lot images bois Pirate LOD livre - v591 a v598')
assert '16 variantes LOD1/LOD2 et 32 vues' in code
code=code.replace('16 variantes LOD1/LOD2 et 32 vues','24 variantes LOD1/LOD2 et 48 vues')
assert 'huit controles LOD0' in code
code=code.replace('huit controles LOD0','douze controles LOD0')
assert '696 -> 680' in code
code=code.replace('696 -> 680','596 -> 572')
exec(compile(code,str(parent)+"#wood-lod-image-lot", "exec"),globals())
