"""Verify the thirteen actual emitted outputs, rather than the emitter capability table."""
from pathlib import Path
parent=Path(__file__).with_name('seal_pirate_palm_wgsl_delivery_v539.py')
code=parent.read_text('utf-8').replace('pirate-palm-wgsl-delivery-v539','pirate-palm-wgsl-delivery-v540')
old="assert len(r['terminal_fields'])==13"
new="""import re
assert set(i['terminals'])<=set(r['terminal_fields'])
program=Path(r['wgsl']).read_text('utf-8')
matches=re.findall(r'struct SourceMaterialOutputs\\s*\\{([^}]+)\\}',program)
assert len(matches)==1
actual_fields=re.findall(r'([A-Za-z_][A-Za-z_0-9]*)\\s*:',matches[0])
expected_fields={r['terminal_fields'][name][0] for name in i['terminals']}
assert len(actual_fields)==len(set(actual_fields))==13 and set(actual_fields)==expected_fields
assert 'bent_normal' in expected_fields"""
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace('[Path(__file__),bp,wp,ip','[parent,Path(__file__),bp,wp,ip')
exec(compile(code,str(parent)+'#actual-emitted-source-interface','exec'),globals())
