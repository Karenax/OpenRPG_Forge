"""Do not query original material getters that initialize missing SDK defaults."""
from pathlib import Path
S=Path(__file__).resolve().parent;recipe=S/'run_gladiator_color_property_gpu_v428.py'
code=recipe.read_text('utf-8').replace('v428','v429').replace('V428','V429').replace('source_get_color','source_clone_get_color')
old='source_clone_get_color=source.GetColor("_Tint")';assert code.count(old)==1
code=code.replace(old,'source_clone_get_color=default(Color)')
old='try {clone.shader=shader;row.clone_get_color='
assert code.count(old)==1
code=code.replace(old,'try {row.source_clone_get_color=clone.GetColor("_Tint");clone.shader=shader;row.clone_get_color=')
exec(compile(code,str(recipe)+'#read-disposable-clone-only','exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
