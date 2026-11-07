"""Index the reviewed Mountain images with the unchanged skill transaction."""
from pathlib import Path
recipe=Path(__file__).with_name('apply_pirate_lod_thumbnail_batch03_v372.py')
code=recipe.read_text('utf-8')
code=code.replace('pirate-lod-thumbnail-batch03-index-v372','mountain-reviewed-images-index-v920').replace('pirate-lod-batch03-combined-review-v371','mountain-reviewed-images-v919')
code=code.replace('0<count<=16','0<count<=44').replace("'hivemind.pirate-island'","'hivemind.mountain-ancient-mountain'").replace('hivemind-pirate-island','hivemind-mountain-ancient-mountain')
code=code.replace('reviewed_pirate_source_cards_transactionally_indexed','reviewed_mountain_source_cards_transactionally_indexed')
code=code.replace('[Path(__file__).resolve(),HELPER,REVIEW]','[recipe,Path(__file__).resolve(),HELPER,REVIEW]')
exec(compile(code,str(recipe)+'#reviewed-mountain-immutable-transaction','exec'),globals())
