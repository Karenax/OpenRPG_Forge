"""Read the four delivered cards and images through the actual native reader."""
from pathlib import Path
parent = Path(__file__).with_name('run_pirate_lod_thumbnail_batch03_native_reader_v373.py')
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-native-reader-v373', 'pirate-held-wood-images-native-reader-v836').replace('pirate-lod-thumbnail-batch03-index-v372', 'pirate-held-wood-images-index-v835').replace("0<index['cards_updated']<=16", "0<index['cards_updated']<=4")
code = code.replace("OLD=W/'house-bench-current-native-library-probe-v2'", "OLD=W/'current-native-library-images-v778'")
before = 'reader=original.replace(needle,replacement);assert reader.replace(replacement,needle)==original;'
after = '''phys_needle='mod physics_transport;';phys_replacement='#[path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src/asset_library_native/physics_transport.rs"] mod physics_transport;';assert original.count(phys_needle)==1;reader=original.replace(needle,replacement).replace(phys_needle,phys_replacement);assert reader.replace(replacement,needle).replace(phys_replacement,phys_needle)==original;'''
assert before in code
code = code.replace(before, after)
code = code.replace("'asset_library_native/history.rs','asset_library_model_wire.rs'", "'asset_library_native/history.rs','asset_library_native/physics_transport.rs','asset_library_model_wire.rs'")
code = code.replace('inputs=[Path(__file__).resolve(),INDEX,', 'inputs=[parent,Path(__file__).resolve(),INDEX,')
code = code.replace('Explicit path to the same product history module in the isolated crate.', 'Explicit paths to the same product history and physics modules in the isolated crate.')
exec(compile(code, str(parent)+'#four-delivered-wood-lod-cards', 'exec'), globals())
