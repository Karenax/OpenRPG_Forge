"""Install original previews, preserving and exposing their measured limitations."""
from pathlib import Path
recipe=Path(__file__).with_name('apply_pirate_lod_thumbnail_batch03_v372.py')
code=recipe.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-index-v372','mountain-original-limited-images-index-v946').replace('pirate-lod-batch03-combined-review-v371','mountain-original-limited-images-v945')
code=code.replace('0<count<=16','0<count<=3').replace("'hivemind.pirate-island'","'hivemind.mountain-ancient-mountain'").replace('hivemind-pirate-island','hivemind-mountain-ancient-mountain')
anchor="  allowed={'names','descriptions','tags'}"
assert code.count(anchor)==1
addition="""  entry['preview_source_limitations']=review['source_visual_limits']
  entry['tags'].append('source-texture-uniform')
  entry['descriptions']['fr']+=' Limitation des sources originales : T_FantasyTree01_BaseColor.png et T_Bark01_ARH.png contiennent uniquement RGBA zéro, confirmé par leurs octets dans l’archive Unity originale. La vignette conserve les parties sombres sans les retoucher ; réparation des textures non réalisée.'
  entry['descriptions']['en']+=' Original source limitation: T_FantasyTree01_BaseColor.png and T_Bark01_ARH.png contain only zero RGBA, verified against their original Unity archive payloads. This thumbnail retains the dark parts without retouching; texture repair remains unresolved.'
"""
code=code.replace(anchor,addition+anchor)
code=code.replace('reviewed_pirate_source_cards_transactionally_indexed','original_mountain_thumbnails_and_source_limits_transactionally_indexed').replace('[Path(__file__).resolve(),HELPER,REVIEW]','[recipe,Path(__file__).resolve(),HELPER,REVIEW]')
exec(compile(code,str(recipe)+'#original-source-limits-visible-in-library','exec'),globals())
