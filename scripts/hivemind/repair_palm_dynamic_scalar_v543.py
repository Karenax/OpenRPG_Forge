"""Count matrix-serialized dynamic defaults separately from already-bound material values."""
from pathlib import Path
parent=Path(__file__).with_name('repair_palm_dynamic_scalar_v542.py')
code=parent.read_text('utf-8').replace('v542','v543')
old="if i['slot_type'].endswith('.DynamicValueMaterialSlot') and 'literal' in i['value']]"
new="if i['slot_type'].endswith('.DynamicValueMaterialSlot') and isinstance(i['value'].get('literal'),dict)]"
assert code.count(old)==1
code=code.replace(old,new)
code=code.replace('for p in [Path(__file__),sdk,native,','for p in [parent,Path(__file__),sdk,native,')
exec(compile(code,str(parent)+'#matrix-serialized-defaults','exec'),globals())
