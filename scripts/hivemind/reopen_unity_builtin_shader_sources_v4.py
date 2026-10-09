"""Record zero byte-equal includes honestly; retain all actual installed variants."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_unity_builtin_shader_sources_v3.py')
script=BASE.read_text('utf-8-sig')
before="ROOT=W/'unity-builtin-shaders-6000.5.8f1-v3'"
assert script.count(before)==1
script=script.replace(before,"ROOT=W/'unity-builtin-shaders-6000.5.8f1-v4'")
before="assert matches and differences,'Retain the observed include difference rather than claiming byte equality'"
assert script.count(before)==1
script=script.replace(before,"assert differences,'Retain the observed include differences rather than claiming byte equality'")
exec(compile(script,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
