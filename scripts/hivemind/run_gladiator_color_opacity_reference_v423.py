"""Capture the eight remaining textures, keeping the qualified normals untouched."""
from pathlib import Path
S=Path(__file__).resolve().parent
recipe=S/'run_gladiator_normal_reference_v415.py'
code=recipe.read_text('utf-8').replace('v415','v423').replace('V415','V423').replace('gladiator-normal-reference-v423','gladiator-color-opacity-reference-v423')
edits={
    "if b['requires_normal_decode']":"if not b['requires_normal_decode']",
    'assert len(bindings)==4':'assert len(bindings)==8',
    "assert row['format']=='DXT5'":"assert row['format'] in {'DXT1','RGB24','RGBA32'}",
    "replace('==14','==4')":"replace('==14','==8')",
    '''replace("'verified_textures':14","'verified_textures':4")''':'''replace("'verified_textures':14","'verified_textures':8")''',
    'expected_mips=52':'expected_mips=104',
    'normal_textures=4,mips=52':'color_opacity_textures=8,mips=104',
    "native['total'] == native['matched'] == 4":"native['total'] == native['matched'] == 8",
    'normal_textures=4':'color_opacity_textures=8',
    'four_native_normal_references':'eight_native_color_opacity_references',
    'four_source_normal_references':'eight_source_color_opacity_references'
}
for a,b in edits.items():
    assert a in code,a
    code=code.replace(a,b)
exec(compile(code,str(recipe)+'#remaining-eight-textures','exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
