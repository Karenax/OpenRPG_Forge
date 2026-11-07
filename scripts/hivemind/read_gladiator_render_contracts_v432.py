"""Keep separate ColorMask declarations for each render target."""
from pathlib import Path
S=Path(__file__).resolve().parent;recipe=S/'read_gladiator_render_contracts_v431.py'
code=recipe.read_text('utf-8').replace('gladiator-render-contracts-v431','gladiator-render-contracts-v432')
old="matches=re.findall(r'^\\s*'+key+r'\\s+([^\\r\\n]+)',header,re.M);assert len(matches)<=1\n        if matches:state[key]=matches[0].strip()"
assert code.count(old)==1
new="matches=re.findall(r'^\\s*'+key+r'\\s+([^\\r\\n]+)',header,re.M);assert key=='ColorMask' or len(matches)<=1\n        if matches:state[key]=[m.strip() for m in matches] if key=='ColorMask' else matches[0].strip()"
code=code.replace(old,new)
exec(compile(code,str(recipe)+'#separate-render-target-color-masks','exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
