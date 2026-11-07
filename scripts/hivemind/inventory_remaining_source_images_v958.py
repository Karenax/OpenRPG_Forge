"""Refresh the full missing-source-preview inventory after the verified delivery."""
from pathlib import Path

parent = Path(__file__).with_name('inventory_remaining_source_images_v949.py')
code = parent.read_text('utf-8').replace('remaining-source-images-v949', 'remaining-source-images-v958').replace('515', '493')
code = code.replace("input_pins={str(pointer):pin,str(catalog_path):catalog_sha}",
                    "input_pins={str(pointer):pin,str(catalog_path):catalog_sha,str(parent):sha(parent),str(Path(__file__).resolve()):sha(Path(__file__))}")
exec(compile(code, str(parent)+'#verified-upper-lod-delivery', 'exec'), globals())
