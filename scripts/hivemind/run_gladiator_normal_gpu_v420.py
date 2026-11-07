"""Re-run the actual GPU comparison with the corrected kernel helper."""
from pathlib import Path
S=Path(__file__).resolve().parent
recipe=S/'run_gladiator_normal_gpu_v417.py'
code=recipe.read_text('utf-8').replace('v417','v420').replace('gladiator-material-kernels-v414','gladiator-material-kernels-v419')
exec(compile(code,str(recipe)+'#corrected-mode2-helper','exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
