"""Use the source-declared terminal interface instead of the Villa-specific count."""
from pathlib import Path
parent=Path(__file__).with_name('build_pirate_palm_material_ir_v530.py')
code=parent.read_text('utf-8').replace('pirate-palm-active-material-ir-v530','pirate-palm-active-material-ir-v531')
needle="O.mkdir();(O/'material-family-active-ir-v1').mkdir()"
injection="""expected=[o['m_Name'] for o in objects if o.get('m_Name','').startswith(('SurfaceDescription.','VertexDescription.'))]
assert len(expected)==len(set(expected))==13
assert 'SurfaceDescription.BentNormal' in expected
assert {'VertexDescription.Position','VertexDescription.Normal','VertexDescription.Tangent'}<=set(expected)
old_terminal_assert='assert len(terminals)==12'
assert source.count(old_terminal_assert)==1
source=source.replace(old_terminal_assert,'assert set(terminals)=='+repr(set(expected))+' and len(terminals)=='+str(len(expected)))
"""+needle
assert code.count(needle)==1;code=code.replace(needle,injection)
code=code.replace('[Path(__file__),proofpath,queuepath','[parent,Path(__file__),proofpath,queuepath')
code=code.replace("'Exact original vegetation shader GUID'", "'Exact original vegetation shader GUID','Exact thirteen source terminals, including BentNormal and all three vertex outputs'")
exec(compile(code,str(parent)+'#exact-source-terminal-interface','exec'),globals())
