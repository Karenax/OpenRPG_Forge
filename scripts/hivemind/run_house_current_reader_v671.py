"""Build current Forge readers and admit every House GLB with negative integrity controls."""
from pathlib import Path
BASE = Path(__file__).with_name('run_gladiator_current_reader_v445.py')
code = BASE.read_text('utf-8-sig')
changes = {
    'gladiator-current-product-reader-v445':'house-current-product-reader-v671',
    'gladiator-current-reader-v445':'house-current-reader-v671',
    'gladiator-autonomous-glb-v442/RESULT.v1.json':'house-autonomous-glb-v669/RESULT.v1.json',
    'gladiator-glb-independent-reopen-v444/RESULT.v1.json':'house-glb-independent-reopen-v670/RESULT.v1.json',
    "len(assembly['rows'])==len(reopened['rows'])==5":"len(assembly['rows'])==len(reopened['rows'])==24",
    "assert len(state['rows'])==5 and sum(r['primitives'] for r in state['rows'])==24\n    assert sum(r['triangles'] for r in state['rows'])==10000":"scenes=read(W/'house-scene-geometry-package-v667/RESULT.v1.json')\n    expected_primitives=sum(len(n['geometry']['primitives']) for s in scenes['rows'] for n in s['nodes'] if 'geometry' in n)\n    expected_triangles=sum(p['triangles'] for s in scenes['rows'] for n in s['nodes'] if 'geometry' in n for p in n['geometry']['primitives'])\n    assert len(state['rows'])==24 and sum(r['primitives'] for r in state['rows'])==expected_primitives\n    assert sum(r['triangles'] for r in state['rows'])==expected_triangles",
    'all_five_gladiator_glbs_admitted_by_current_product_readers':'all_twentyfour_house_glbs_admitted_by_current_product_readers',
}
for old,new in changes.items():
    assert old in code,old
    code=code.replace(old,new)
code=code.replace('gladiator_product_reader','house_product_reader').replace('gladiator_reader','house_reader')
code=code.replace("assembly, reopened = read(assembly_path), read(reopen_path)", "assembly, reopened = read(assembly_path), read(reopen_path)\nscene_path=W/'house-scene-geometry-package-v667/RESULT.v1.json'")
code=code.replace('assembly_path,reopen_path]:', 'assembly_path,reopen_path,scene_path,BASE]:')
exec(compile(code,str(BASE)+'#current-house-canonical-admission','exec'),globals())
