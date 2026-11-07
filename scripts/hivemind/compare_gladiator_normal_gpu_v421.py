"""Keep the exact prior comparison criteria for the corrected mode2 helper."""
from pathlib import Path
S=Path(__file__).resolve().parent
recipe=S/'compare_gladiator_normal_gpu_v418.py'
code=recipe.read_text('utf-8').replace('gladiator-normal-gpu-v417','gladiator-normal-gpu-v420').replace('gladiator-normal-gpu-comparison-v418','gladiator-normal-gpu-comparison-v421')
exec(compile(code,str(recipe)+'#corrected-mode2-comparison','exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
