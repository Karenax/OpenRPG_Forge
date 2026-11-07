"""Inventory the remaining primary source images after the third delivery."""
from pathlib import Path

inventory_recipe = Path(__file__).with_name('inventory_remaining_source_images_v968.py')
code = inventory_recipe.read_text('utf-8')
code = code.replace('remaining-source-images-v968', 'remaining-source-images-v991').replace("'469'", "'445'")
exec(compile(code, str(inventory_recipe)+'#third-source-delivery-inventory', 'exec'), globals())
