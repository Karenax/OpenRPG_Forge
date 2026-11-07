"""Repair the isolated Rust literal; preserve the failed v683 evidence."""
from pathlib import Path

parent = Path(__file__).with_name('run_house_rapier_box_queries_v683.py')
recipe = parent.read_text('utf-8-sig').replace('v683', 'v691')
anchor = "exec(compile(code,str(BASE)+'#house-explicit-box-query-boundary','exec'),globals())"
repair = '''
assert code.count('*.5')==1
code=code.replace('*.5','*0.5')
'''
assert recipe.count(anchor) == 1
recipe = recipe.replace(anchor, repair + anchor)
exec(compile(recipe,str(parent)+'#rust-literal-repair-v691','exec'),globals())
